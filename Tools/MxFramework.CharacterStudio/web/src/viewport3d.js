// CharacterStudio split module. Registers functions on globalThis to preserve the original call graph.
import { normalizeBakedSurfacePreviewBinding, loadBakedSurfacePreview, applyBakedSurfacePreviewMaterials } from './bakedSurfacePreview.js';

const MODEL_WRAPPER_SCALE_PRESETS = [0.01, 0.1, 1, 10, 100];
const HURTBOX_MIN_DIMENSION = 0.001;
const CHARACTER_PALETTE_BINDING_V5 = "mx.characterPaletteBinding.v5";
const SURFACE_PALETTE_CATALOG_V3 = "mx.wgameSurfacePaletteCatalog.v3";
const SURFACE_PALETTE_CATALOG_API = "/api/authoring/palette/catalog";
let focusSelectedViewportTargetCallback = null;
let viewportSyncHurtboxShapeHandles = null;
let viewportSyncHurtboxBindingReference = null;
let viewportSyncBodyMeasurements = null;
let viewportSyncMovementCollision = null;

// ---------------------------------------------------------------------------
// character_palette_usage.json 归属校验
//
// usage.modelHash 是「模型版本标签」：Unity 导入时用导入后 Prefab 的 mesh 文件哈希与它比对。
// usage.tokenUsages 是从该模型网格统计出的面数分布。两者必须来自同一个模型版本 —— 服务端
// 无法重算面数，所以一旦对不上只能拒绝，不能改写。
//
// state.surfacePalettePreviewUsage 是全局单槽，历史上被武器 / 挂点预览覆盖过，于是角色 usage
// 里写进了武器模型哈希。现在要求：只有显式声明 usageSource 的本体挂载点可以写入，写入时必须
// 带上「包 + 本体资源 + 源模型哈希 + 加载代次」，保存前再复核一次。
// ---------------------------------------------------------------------------
let surfacePaletteUsageOwner = null;
let surfacePaletteUsageGeneration = 0;

function invalidateSurfacePalettePreviewUsage() {
  surfacePaletteUsageGeneration += 1;
  surfacePaletteUsageOwner = null;
  state.surfacePalettePreviewUsage = null;
}

// 当前加载代次。用于诊断与回归测试：扫描结果必须带上生成时刻的代次，代次不匹配即作废。
function getSurfacePaletteUsageGeneration() {
  return surfacePaletteUsageGeneration;
}

// 本体模型的 usage 来源描述。relativePath 必须来自资源目录声明，不能回退到预览路径：
// 预览可能加载 previewMesh（.glb）而不是本体（.fbx），两者的哈希不是一回事。
function buildSurfacePaletteUsageSource(bodyResource, packageUrl) {
  const resourceKey = String(bodyResource?.resourceKey || "");
  const relativePath = String(bodyResource?.relativePath || "").trim();
  if (!resourceKey || !relativePath) return null;
  return {
    resourceKey,
    relativePath,
    modelUrl: packageUrl(relativePath, getPreviewModelResourceHash(bodyResource))
  };
}

// 发布一次扫描结果。只有代次未被推进时才接受；返回 false 表示本次结果已作废。
function publishSurfacePalettePreviewUsage(pending) {
  if (!pending?.usage || !pending?.owner) return false;
  if (pending.owner.generation !== surfacePaletteUsageGeneration) return false;
  surfacePaletteUsageOwner = pending.owner;
  state.surfacePalettePreviewUsage = pending.usage;
  globalThis.renderSurfacePaletteWorkbench?.();
  return true;
}

function findPackageResourceByKey(resourceKey) {
  const entries = state.package?.resourceCatalog?.entries || [];
  return entries.find(entry => String(entry?.resourceKey || "") === String(resourceKey || "")) || null;
}

// 保存前复核：usage 必须仍然属于「当前包 + 当前本体资源 + 当前模型版本」，且没有被更新的加载取代。
function validateSurfacePaletteUsageOwner(usage) {
  const owner = surfacePaletteUsageOwner;
  if (!owner) return { ok: false, reason: "3D 预览尚未完成 Color + Surface Profile 扫描，暂不能生成 usage v2。" };
  if (owner.generation !== surfacePaletteUsageGeneration) {
    return { ok: false, reason: "角色包已重新加载，本次扫描结果已作废，请等待 3D 预览重新扫描后再保存。" };
  }
  if (owner.packageRelative !== String(state.packageRelative || "")) {
    return { ok: false, reason: "扫描结果属于其它角色包，已作废，请等待 3D 预览重新扫描后再保存。" };
  }
  const resource = findPackageResourceByKey(owner.bodyResourceKey);
  if (!resource) {
    return { ok: false, reason: `资源目录已不再声明本体模型 ${owner.bodyResourceKey}，本次扫描结果已作废。` };
  }
  if (String(resource.relativePath || "").trim() !== owner.bodyRelativePath) {
    return { ok: false, reason: `本体模型路径已由 ${owner.bodyRelativePath} 变更为 ${resource.relativePath}，本次扫描结果已作废。` };
  }
  if (!usage?.modelHash || usage.modelHash !== owner.modelHash) {
    return { ok: false, reason: "usage.modelHash 与本次扫描使用的模型不一致，已作废。" };
  }
  return { ok: true };
}

function isCharacterPreviewLayerEnabled(layer) {
  const bodySection = globalThis.getBodyWorkspaceSection?.();
  if (bodySection) return layer === "colliders" && bodySection === "parts";
  return Boolean(state.layers?.[layer]) && (layer === "weapons"
    || state.selectedPath !== "config/attributes" || Boolean(state.attributePreviewOverlays));
}

function buildWeaponVisualizationItems(pkg, activeSlots = [], syncResolver = null) {
  const geometry = pkg?.geometry || {};
  const slotList = Array.from(new Set((activeSlots || []).filter(Boolean).map(String)));
  if (slotList.length === 0) return [];

  const resources = pkg?.resourceCatalog?.entries || [];
  const sockets = geometry.sockets || [];
  const traces = geometry.traces || [];
  const items = [];
  const activeConfiguration = getActiveWeaponConfiguration();

  for (const slot of slotList) {
    const configuredSlot = (activeConfiguration?.slots || []).find(item => item?.slotId === slot);
    const attachment = configuredSlot
      ? buildWeaponAttachmentForSlot(configuredSlot, pkg, activeConfiguration?.combinedWeaponConfigurationId || "configuration")
      : null;
    const item = {
      slot,
      slotLabel: getEquipSlotLabel(slot),
      attachment,
      attachmentId: getWeaponAttachmentIdentity(attachment),
      weaponId: attachment?.weaponId || "",
      weaponPath: getWeaponAttachmentPath(attachment),
      socket: null,
      socketPath: "",
      resource: null,
      resourcePath: "",
      trace: null,
      tracePath: "",
      sync: null,
      issues: []
    };

    if (configuredSlot?.singleWeaponConfigurationId === "weapon.unarmed") {
      item.isUnarmed = true;
      item.weaponId = "weapon.unarmed";
      items.push(item);
      continue;
    }

    if (!attachment) {
      item.issues.push(weaponIssue("error", "未配置武器", `${getEquipSlotLabel(slot)} 没有武器挂接。`, ""));
      items.push(item);
      continue;
    }

    if (attachment.attachSocketId) {
      item.socket = sockets.find(socket => socket?.socketId === attachment.attachSocketId) || null;
      item.socketPath = item.socket ? `geometry/sockets/${item.socket.socketId}` : "";
      if (!item.socket) {
        item.issues.push(weaponIssue("error", "Socket 缺失", `找不到 socket ${attachment.attachSocketId}。`, item.weaponPath));
      }
    } else {
      item.issues.push(weaponIssue("error", "未绑定 Socket", "武器尚未选择挂接 socket。", item.weaponPath));
    }

    const attachmentResourceKey = getWeaponAttachmentResourceKey(attachment);
    if (attachmentResourceKey) {
      item.resource = findPreviewModelResourceByKey(attachmentResourceKey, resources);
      item.resourcePath = item.resource ? getPreviewModelResourceObjectPath(item.resource) : "";
      if (item.resource) {
        item.sync = typeof syncResolver === "function" ? syncResolver(item.resource) : null;
        const commonStatus = getCommonPreviewResourceStatus(item.resource);
        if (commonStatus && commonStatus !== "Ready") {
          const tone = item.resource.generatedExists === false ? "error" : "warn";
          item.issues.push(weaponIssue(tone, commonStatus, getCommonPreviewResourceMessage(item.resource), item.resourcePath || item.weaponPath));
        }
        if (item.sync?.status) {
          const normalized = String(item.sync.status).toLowerCase();
          if (normalized.includes("nounitycatalog") || normalized.includes("missingfromunitycatalog")) {
            item.issues.push(weaponIssue("warn", item.sync.label || "Unity 未同步", "模型资源没有可用的 Unity 同步记录。", item.resourcePath));
          }
        }
      } else {
        item.issues.push(weaponIssue("error", "模型缺失", `找不到模型资源 ${attachmentResourceKey}。`, item.weaponPath));
      }
    } else {
      item.issues.push(weaponIssue("warn", "未绑定模型", "武器尚未选择预览模型资源。", item.weaponPath));
    }

    if (attachment.traceId) {
      const definition = getSingleWeaponConfiguration(attachment.singleWeaponConfigurationId || attachment.weaponId);
      item.trace = definition?.traceProfiles?.find(trace => trace?.traceId === attachment.traceId)
        || traces.find(trace => trace?.traceId === attachment.traceId) || null;
      item.tracePath = item.trace ? `weapon-library/singleWeaponConfigurations/${encodeURIComponent(attachment.weaponId)}/traceProfiles/${encodeURIComponent(item.trace.traceId)}` : "";
      if (!item.trace) {
        item.issues.push(weaponIssue("error", "轨迹缺失", `找不到攻击轨迹 ${attachment.traceId}。`, item.weaponPath));
      }
    } else {
      item.issues.push(weaponIssue("info", "未绑定攻击轨迹", "当前武器没有启用攻击轨迹。", item.weaponPath));
    }

    if (item.trace) {
      if (item.trace.weaponId && item.trace.weaponId !== attachment.weaponId) {
        item.issues.push(weaponIssue("warn", "轨迹武器不匹配", `轨迹属于 ${item.trace.weaponId}；当前武器是 ${attachment.weaponId}。`, item.tracePath));
      }
      if (item.trace.equipSlot && item.trace.equipSlot !== attachment.equipSlot) {
        item.issues.push(weaponIssue("warn", "轨迹槽位不匹配", `轨迹槽位是 ${item.trace.equipSlot}；当前槽位是 ${attachment.equipSlot}。`, item.tracePath));
      }
    }

    items.push(item);
  }

  return items;
}

function findPreviewModelResourceByKey(resourceKey, resources = state.package?.resourceCatalog?.entries || []) {
  const key = String(resourceKey || "");
  if (!key) return null;
  const packageResource = (resources || []).find(resource => resource?.resourceKey === key);
  if (packageResource) return packageResource;
  return buildCommonPreviewModelResource(key);
}

function buildCommonPreviewModelResource(resourceKey) {
  const commonItem = (state.commonResources?.items || []).find(item => item?.resourceKey === resourceKey);
  if (!commonItem) return null;
  const kind = String(commonItem.resourceKind || "").toLowerCase();
  const usage = String(commonItem.usage || "").toLowerCase();
  if (kind && kind !== "model") return null;
  if (usage && usage !== "charactermodel" && usage !== "weaponmodel" && usage !== "previewmesh") return null;
  const relativePath = normalizePreviewPath(commonItem.rawGeneratedAssetPath || commonItem.generatedAssetPath || commonItem.sourcePath || "");
  if (!isPreviewModelPath(relativePath)) return null;
  const hash = commonItem.generatedHash || commonItem.sourceHash || "";
  commonItem.importHints = commonItem.importHints || {};
  return {
    resourceKey,
    stableId: `global.profile.${resourceKey}`,
    typeId: "model",
    localId: commonItem.displayName || resourceKey,
    displayName: commonItem.displayName || resourceKey,
    usage: commonItem.usage || "weaponModel",
    sourceFormat: getPreviewModelFormat(relativePath) || "",
    relativePath,
    hash,
    hashes: { contentHash: hash },
    provenance: { sourceFile: commonItem.sourcePath || "" },
    tags: ["public", "common", commonItem.kind || ""].filter(Boolean),
    importHints: commonItem.importHints,
    sourceProviderId: "resourceMapping",
    commonStatus: commonItem.status || "",
    commonMessage: commonItem.message || "",
    modelScaleDiagnostic: commonItem.modelScaleDiagnostic || null,
    generatedExists: commonItem.generatedExists !== false,
    generatedAssetPath: commonItem.generatedAssetPath || "",
    rawGeneratedAssetPath: commonItem.rawGeneratedAssetPath || commonItem.generatedAssetPath || "",
    unityRuntimeAssetPath: commonItem.unityRuntimeAssetPath || commonItem.generatedAssetPath || "",
    usesUnityWrapperPrefab: !!commonItem.usesUnityWrapperPrefab
  };
}

function getPreviewModelResourceObjectPath(resource) {
  if (!resource) return "";
  return resource.sourceProviderId === "resourceMapping"
    ? `commonResources/${resource.resourceKey}`
    : `resources/${resource.resourceKey}`;
}

function getCommonPreviewResourceStatus(resource) {
  return resource?.sourceProviderId === "resourceMapping" ? String(resource.commonStatus || "") : "";
}

function getCommonPreviewResourceMessage(resource) {
  return resource?.commonMessage
    || "已引用公共资源，但 Unity 导入 / Profile 登记尚未完全就绪。";
}

function getModelScaleDiagnostic(resource) {
  if (!resource) return null;
  if (resource.modelScaleDiagnostic) return resource.modelScaleDiagnostic;
  const item = findCommonResourceItemByKey(resource.resourceKey);
  return item?.modelScaleDiagnostic || null;
}

function shouldShowModelScaleRecommendation(resource) {
  const diagnostic = getModelScaleDiagnostic(resource);
  return ["RecommendedScale", "WrapperScaleMismatch"].includes(diagnostic?.status)
    && Number(diagnostic.recommendedScale || 0) > 0;
}

function formatModelScaleRecommendation(resource) {
  const diagnostic = getModelScaleDiagnostic(resource);
  if (!diagnostic?.status) return "";
  const unit = diagnostic.unitScaleFactor == null ? "" : `FBX UnitScaleFactor=${diagnostic.unitScaleFactor}`;
  const recommended = Number(diagnostic.recommendedScale || 0);
  const current = Number(diagnostic.currentScale || 0);
  const previewScale = Number(diagnostic.previewUnitScale || 0);
  if (diagnostic.status === "UnityConvertsUnits") {
    return `Unity Convert Units 处理 ${unit || "FBX 单位转换"}；CharacterStudio 以 ${previewScale || 1}x 预览原始 FBX，wrapper scale 可保持 ${current || 1}x。`;
  }
  if (diagnostic.status === "WrapperScaleMismatch" && recommended > 0) {
    return `Unity Convert Units 已处理 ${unit || "FBX 单位转换"}；建议将 wrapper scale 重置为 ${recommended}x。当前平均 scale 为 ${current || 1}。`;
  }
  if (diagnostic.status === "PreviewUnitScale") {
    return `CharacterStudio 以 ${previewScale || 1}x 预览原始 FBX。这个预览单位缩放不会保存到 wrapper pose。`;
  }
  if (diagnostic.status === "RecommendedScale" && recommended > 0) {
    return `${unit || "FBX 单位元数据"} 建议 wrapper scale=${recommended}。当前平均 scale 为 ${current || 1}。`;
  }
  if (diagnostic.status === "ScaleApplied") {
    return `${unit || "FBX 单位元数据"} 已体现在模型 wrapper scale 中。`;
  }
  return diagnostic.reason || "";
}

function getPreviewUnitScaleForResource(resource) {
  const diagnostic = getModelScaleDiagnostic(resource);
  const scale = Number(diagnostic?.previewUnitScale || 0);
  if (!Number.isFinite(scale) || scale <= 0) return 0;
  return ["UnityConvertsUnits", "PreviewUnitScale", "WrapperScaleMismatch"].includes(diagnostic?.status) ? scale : 0;
}

async function applyRecommendedModelScale(resource, options = {}) {
  if (!resource) return false;
  if (isCommonPreviewModelResource(resource)) {
    openResourceManagerForPublicResource(resource, {
      message: "公共资源缩放由 Resource Manager 管理。"
    });
    return false;
  }
  const diagnostic = getModelScaleDiagnostic(resource);
  const recommended = Number(diagnostic?.recommendedScale || 0);
  if (!(recommended > 0)) {
    state.message = "此资源没有可用的推荐模型缩放。";
    renderShellStatus();
    return false;
  }

  const pose = ensureModelWrapperPose(resource);
  const scale = roundTransformNumber(recommended);
  pose.scale.x = scale;
  pose.scale.y = scale;
  pose.scale.z = scale;
  state.dirty = true;
  refreshDirtyFromCleanSnapshot();
  state.message = `已应用推荐模型 wrapper scale：${scale}。`;
  renderShellStatus();

  if (!options.skipRender) render();
  return true;
}

async function applyRecommendedModelScaleForResourcePath(path) {
  const target = findTarget(path || state.selectedPath);
  return await applyRecommendedModelScale(target?.value);
}

function renderModelWrapperScalePresetButtons(resourcePath, label = "缩放预设") {
  if (!resourcePath) return "";
  return `<div class="weapon-data-actions model-scale-presets" aria-label="${escapeHtml(label)}">
    ${MODEL_WRAPPER_SCALE_PRESETS.map(scale => `<button type="button" data-apply-model-scale-preset="${escapeHtml(resourcePath)}" data-scale-preset="${escapeHtml(scale)}">${escapeHtml(scale)}x</button>`).join("")}
  </div>`;
}

function normalizeModelWrapperScalePreset(value) {
  const number = Number(value);
  if (!Number.isFinite(number) || number <= 0) return 1;
  return Math.min(1000, Math.max(0.0001, number));
}

async function applyModelWrapperScalePreset(resource, scaleValue, options = {}) {
  if (!resource) return false;
  if (isCommonPreviewModelResource(resource)) {
    openResourceManagerForPublicResource(resource, {
      message: "公共资源缩放预设由 Resource Manager 管理。"
    });
    return false;
  }
  const scale = roundTransformNumber(normalizeModelWrapperScalePreset(scaleValue));
  const pose = ensureModelWrapperPose(resource);
  pose.scale.x = scale;
  pose.scale.y = scale;
  pose.scale.z = scale;

  state.dirty = true;
  refreshDirtyFromCleanSnapshot();
  state.message = `已将模型 wrapper scale 设置为 ${scale}x。`;
  renderShellStatus();

  if (!options.skipRender) render();
  return true;
}

async function applyModelWrapperScalePresetForResourcePath(path, scaleValue) {
  const target = findTarget(path || state.selectedPath);
  return await applyModelWrapperScalePreset(target?.value, scaleValue);
}

function isScaleCloseTo(left, right) {
  const a = Number(left);
  const b = Number(right);
  if (!Number.isFinite(a) || !Number.isFinite(b)) return false;
  return Math.abs(a - b) <= Math.max(0.0001, Math.abs(b) * 0.05);
}

function getDefaultWeaponSlotList(pkg = state.package) {
  return (getActiveWeaponConfiguration()?.slots || []).map(item => item?.slotId).filter(Boolean);
}

function getDefaultWeaponSlotSet(pkg = state.package) {
  return new Set(getDefaultWeaponSlotList(pkg));
}

function getActiveWeaponAttachments(pkg = state.package) {
  const configuration = getActiveWeaponConfiguration();
  const configuredSlots = configuration?.slots || [];
  const result = [];
  for (const slot of configuredSlots) {
    const attachment = buildWeaponAttachmentForSlot(slot, pkg, configuration?.combinedWeaponConfigurationId || "configuration");
    if (attachment) result.push(attachment);
  }
  return result;
}

function buildWeaponAttachmentForSlot(configuredSlot, pkg = state.package, configurationId = "configuration") {
  const weaponId = configuredSlot?.singleWeaponConfigurationId || configuredSlot?.weaponId || "";
  if (!weaponId || weaponId === "weapon.unarmed") return null;
  const slotId = configuredSlot.slotId || "";
  const characterSlot = (pkg?.geometry?.weaponSlots || []).find(item => item?.slotId === slotId);
  const definition = getSingleWeaponConfiguration(weaponId);
  if (!characterSlot || !definition) return null;
  return {
    attachmentId: `weapon.attachment.${configurationId}.${slotId}.${weaponId}`,
    singleWeaponConfigurationId: weaponId,
    weaponId,
    slotId,
    equipSlot: slotId,
    attachSocketId: characterSlot.attachSocketId,
    localGripPose: characterSlot.slotPose || defaultLocalPose(),
    weaponModelPose: definition.modelPose || defaultLocalPose(),
    traceId: definition.traceProfiles?.[0]?.traceId || ""
  };
}

function weaponIssue(tone, label, message, path = "") {
  return { tone, label, message, path };
}

function getEquipSlotLabel(slot) {
  const option = EQUIP_SLOT_OPTIONS.find(item => item.value === slot);
  return option?.label || slot || "slot";
}

function getWeaponVisualizationTone(item) {
  if (item?.issues?.some(issue => issue.tone === "error")) return "error";
  if (item?.issues?.some(issue => issue.tone === "warn")) return "warn";
  return "ok";
}

function getWeaponVisualizationStatusText(item) {
  if (item?.isUnarmed) return "无需挂接";
  const issue = item?.issues?.find(entry => entry.tone === "error")
    || item?.issues?.find(entry => entry.tone === "warn")
    || item?.issues?.find(entry => entry.tone === "info");
  return issue?.label || "已绑定";
}

function hasEmbeddedWeaponSource(pkg = state.package) {
  const resources = pkg?.resourceCatalog?.entries || [];
  const resourceByKey = new Map(resources
    .filter(resource => resource?.resourceKey)
    .map(resource => [resource.resourceKey, resource]));
  const bodyModel = resources.find(resource =>
    String(resource?.typeId || "").toLowerCase() === "model"
    && String(resource?.usage || "").toLowerCase() === "charactermodel");
  if (!bodyModel) return false;
  return (bodyModel.dependencies || []).some(dependency => {
    const material = resourceByKey.get(dependency?.resourceKey || "");
    if (String(material?.typeId || "").toLowerCase() !== "material") return false;
    const identity = `${material?.resourceKey || ""} ${material?.localId || ""} ${material?.relativePath || ""} ${material?.provenance?.sourceFile || ""}`;
    return /(?:^|[._/\\-])weapon(?:[._/\\-]|$)/i.test(identity);
  });
}

function getConfiguredModelNodePaths(resource, metadataKeys = []) {
  const metadata = resource?.importHints?.metadata || {};
  const values = metadataKeys.flatMap(key => {
    const configured = metadata[key] ?? [];
    return Array.isArray(configured)
      ? configured
      : String(configured).split(/[;,\r\n]+/);
  });
  return [...new Set(values
    .map(value => String(value || "").replace(/\\/g, "/").replace(/^\/+|\/+$/g, "").trim())
    .filter(Boolean))];
}

function getEmbeddedWeaponNodePaths(resource) {
  return getConfiguredModelNodePaths(resource, ["embeddedWeaponNodePaths", "embeddedWeaponNodePath"]);
}

function getHiddenRendererNodePaths(resource) {
  return getConfiguredModelNodePaths(resource, ["hiddenRendererNodePaths"]);
}

function getRendererMaterialBindings(resource) {
  const metadata = resource?.importHints?.metadata || {};
  const configured = metadata.rendererMaterialBindingsJson ?? metadata.rendererMaterialBindings ?? [];
  let bindings = configured;
  if (typeof configured === "string") {
    const value = configured.trim();
    if (!value) return [];
    try {
      const parsed = JSON.parse(value);
      bindings = parsed?.bindings || parsed?.rendererMaterialBindings || [];
    } catch {
      return [];
    }
  } else if (!Array.isArray(configured)) {
    bindings = configured?.bindings || configured?.rendererMaterialBindings || [];
  }

  return (Array.isArray(bindings) ? bindings : [])
    .map(binding => ({
      rendererNodePath: String(binding?.rendererNodePath || "")
        .replace(/\\/g, "/")
        .replace(/^\/+|\/+$/g, "")
        .trim(),
      materialSlotIndex: Number.isFinite(Number(binding?.materialSlotIndex))
        ? Number(binding.materialSlotIndex)
        : -1,
      materialResourceKey: String(binding?.materialResourceKey || "").trim(),
      sourceMaterialName: String(binding?.sourceMaterialName || "").trim()
    }))
    .filter(binding => binding.materialResourceKey && (binding.rendererNodePath || binding.sourceMaterialName));
}

function getPreviewHiddenNodePaths(resource, hideEmbeddedWeapon = false) {
  const paths = getHiddenRendererNodePaths(resource);
  if (hideEmbeddedWeapon) paths.push(...getEmbeddedWeaponNodePaths(resource));
  return [...new Set(paths)];
}

function hidePreviewModelNodes(root, configuredPaths = []) {
  if (!root?.traverse) return [];
  const targets = getConfiguredModelNodePaths({
    importHints: { metadata: { hiddenRendererNodePaths: configuredPaths } }
  }, ["hiddenRendererNodePaths"]).map(path => path.toLowerCase());
  if (targets.length === 0) return [];

  const nodes = [];
  root.traverse(node => {
    if (!node || node === root) return;
    const segments = [];
    let current = node;
    while (current && current !== root) {
      if (current.name) segments.unshift(String(current.name));
      current = current.parent;
    }
    if (current !== root || segments.length === 0) return;
    const relativePath = segments.join("/").replace(/\\/g, "/").replace(/^\/+|\/+$/g, "");
    nodes.push({
      node,
      relativePath,
      normalizedPath: relativePath.toLowerCase(),
      leafName: String(node.name || "").toLowerCase()
    });
  });

  const hiddenPaths = [];
  for (const target of targets) {
    let matches = nodes.filter(item =>
      item.normalizedPath === target || item.normalizedPath.endsWith(`/${target}`));
    if (matches.length === 0) {
      const targetSegments = target.split("/").filter(Boolean);
      const leafName = targetSegments[targetSegments.length - 1] || "";
      const leafMatches = nodes.filter(item => item.leafName === leafName);
      if (leafMatches.length === 1) matches = leafMatches;
    }
    for (const match of matches) {
      match.node.visible = false;
      if (!hiddenPaths.includes(match.relativePath)) hiddenPaths.push(match.relativePath);
    }
  }
  root.userData = {
    ...(root.userData || {}),
    hiddenPreviewModelNodePaths: hiddenPaths
  };
  return hiddenPaths;
}

function hideEmbeddedPreviewModelNodes(root, configuredPaths = []) {
  const hiddenPaths = hidePreviewModelNodes(root, configuredPaths);
  if (root) {
    root.userData = {
      ...(root.userData || {}),
      hiddenEmbeddedWeaponNodePaths: hiddenPaths
    };
  }
  return hiddenPaths;
}

function renderWeaponDataOverlayHtml(items, activeSlots = []) {
  const rows = items.map(renderWeaponDataOverlayItem).join("");
  const embeddedWeaponSource = activeSlots.length === 0 && hasEmbeddedWeaponSource();
  const empty = activeSlots.length === 0
    ? embeddedWeaponSource
      ? "源模型包含武器网格，但尚未配置为框架武器。"
      : "没有配置默认武器。"
    : "已选默认武器槽位没有武器数据。";
  return `<aside class="weapon-data-overlay" aria-label="武器数据">
    <div class="weapon-data-head">
      <strong>武器数据</strong>
      <span>${escapeHtml(activeSlots.map(getEquipSlotLabel).join(" / ") || (embeddedWeaponSource ? "源模型内嵌" : "无"))}</span>
    </div>
    <div class="weapon-data-list">${rows || `<div class="weapon-data-empty">${escapeHtml(empty)}</div>`}</div>
  </aside>`;
}

function renderWeaponDataOverlayItem(item) {
  if (item.isUnarmed) return `<article class="weapon-data-item ok"><div class="weapon-data-title"><strong>${escapeHtml(item.slotLabel)}</strong><span>空手</span><span class="weapon-data-state ok">无需挂接</span></div><div class="weapon-data-issue ok">当前组合的合法空手配置，无需武器模型或挂接。</div></article>`;
  const tone = getWeaponVisualizationTone(item);
  const primaryPath = item.weaponPath || item.tracePath || item.socketPath || "";
  const issue = item.issues[0];
  const scaleHint = item.resourcePath && shouldShowModelScaleRecommendation(item.resource)
    ? `<div class="weapon-data-issue warn">${escapeHtml(formatModelScaleRecommendation(item.resource))}</div>`
    : "";
  const wrapperHint = renderPublicWeaponWrapperHint(item.resource);
  const publicResource = isCommonPreviewModelResource(item.resource);
  const modelActions = item.attachment?.__singleWeaponPreview
    ? `<button type="button" data-edit-weapon-model="${escapeHtml(item.attachmentId)}">编辑武器自身变换</button>`
    : item.resourcePath && publicResource
    ? `<button type="button" data-open-resource-manager-resource="${escapeHtml(item.resource?.resourceKey || "")}">在 Resource Manager 中编辑</button>`
    : item.resourcePath
      ? `<button type="button" data-edit-weapon-model="${escapeHtml(item.attachmentId)}">编辑模型变换</button><button type="button" data-auto-fit-weapon="${escapeHtml(item.attachmentId)}">自动适配武器尺寸</button>${shouldShowModelScaleRecommendation(item.resource) ? `<button type="button" data-apply-model-scale="${escapeHtml(item.resourcePath)}">应用推荐缩放</button>` : ""}`
    : "";
  const scalePresets = item.resourcePath && !publicResource ? renderModelWrapperScalePresetButtons(item.resourcePath) : "";
  return `<article class="weapon-data-item ${escapeHtml(tone)}">
    <div class="weapon-data-title">
      <strong>${escapeHtml(item.slotLabel)}</strong>
      <code title="${escapeHtml(item.weaponId || "未配置武器")}">${escapeHtml(item.weaponId || "未配置武器")}</code>
      <span class="weapon-data-state ${escapeHtml(tone)}">${escapeHtml(getWeaponVisualizationStatusText(item))}</span>
    </div>
    ${issue ? `<div class="weapon-data-issue ${escapeHtml(issue.tone)}">${escapeHtml(issue.message)}</div>` : ""}
    ${scaleHint}
    ${wrapperHint}
    ${item.weaponId ? `<button type="button" data-view-weapon="${escapeHtml(item.attachmentId)}">查看武器</button>${modelActions}` : (primaryPath ? `<button type="button" data-jump="${escapeHtml(primaryPath)}">查看配置</button>` : "")}
    ${scalePresets}
  </article>`;
}

function renderWeaponDataValue(label, value, jumpPath, actionLabel) {
  return `<div><span>${escapeHtml(label)}</span><strong title="${escapeHtml(value)}">${escapeHtml(value)}</strong>${jumpPath ? `<button type="button" data-jump="${escapeHtml(jumpPath)}">${escapeHtml(actionLabel)}</button>` : ""}</div>`;
}

function wireWeaponDataOverlay(root) {
  root?.querySelectorAll?.(".weapon-data-overlay [data-view-weapon]")?.forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      enterWeaponViewport(button.dataset.viewWeapon);
    });
  });
  root?.querySelectorAll?.(".weapon-data-overlay [data-edit-weapon-model]")?.forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      editWeaponModelTransformForWeapon(button.dataset.editWeaponModel);
    });
  });
  root?.querySelectorAll?.(".weapon-data-overlay [data-auto-fit-weapon]")?.forEach(button => {
    button.addEventListener("click", async event => {
      event.stopPropagation();
      await autoFitWeaponModelForWeapon(button.dataset.autoFitWeapon);
    });
  });
  root?.querySelectorAll?.(".weapon-data-overlay [data-open-resource-manager-resource]")?.forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      openResourceManagerForPublicResource(button.dataset.openResourceManagerResource);
    });
  });
  root?.querySelectorAll?.(".weapon-data-overlay [data-apply-model-scale]")?.forEach(button => {
    button.addEventListener("click", async event => {
      event.stopPropagation();
      await applyRecommendedModelScaleForResourcePath(button.dataset.applyModelScale);
    });
  });
  root?.querySelectorAll?.(".weapon-data-overlay [data-apply-model-scale-preset]")?.forEach(button => {
    button.addEventListener("click", async event => {
      event.stopPropagation();
      await applyModelWrapperScalePresetForResourcePath(button.dataset.applyModelScalePreset, button.dataset.scalePreset);
    });
  });
  root?.querySelectorAll?.(".weapon-data-overlay [data-jump]")?.forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      selectPath(button.dataset.jump);
    });
  });
}

function editWeaponModelTransformForWeapon(attachmentId, options = {}) {
  const weapon = findWeaponAttachmentById(attachmentId);
  const item = getWeaponVisualizationItemForWeapon(weapon);
  if (!weapon?.weaponId || (!item?.resourcePath && !weapon.__singleWeaponPreview)) {
    state.message = "没有可编辑的武器模型资源。";
    renderShellStatus();
    return null;
  }
  if (!weapon.__singleWeaponPreview && isCommonPreviewModelResource(item.resource)) {
    openResourceManagerForPublicResource(item.resource, {
      message: "公共武器模型变换由 Resource Manager 管理。"
    });
    return null;
  }

  teardownViewportForModeChange();
  if (state.viewportMode !== "weapon") {
    state.previousCharacterSelection = state.selectedPath || "manifest";
  }
  state.viewportMode = "weapon";
  state.focusedWeaponAttachmentId = getWeaponAttachmentIdentity(weapon);
  state.selectedPath = weapon.__singleWeaponPreview ? item.weaponPath : item.resourcePath;
  state.weaponTransformKey = weapon.__singleWeaponPreview
    ? `${item.weaponPath}#modelPose`
    : `${item.resourcePath}#modelWrapperPose`;
  if (options.mode) state.weaponTransformMode = options.mode;
  state.message = weapon.__singleWeaponPreview
    ? "正在编辑单一武器自身变换；角色主副手差异由角色槽位变换负责。"
    : isCommonPreviewModelResource(item.resource)
    ? "正在编辑公共武器资源变换，这会影响所有引用它的角色。"
    : "正在编辑武器模型变换。";
  render();
  return item;
}

async function autoFitWeaponModelForWeapon(attachmentId) {
  const item = editWeaponModelTransformForWeapon(attachmentId, { mode: "scale" });
  if (!item) return;
  const key = item.attachment?.__singleWeaponPreview
    ? `${item.weaponPath}#modelPose`
    : `${item.resourcePath}#modelWrapperPose`;
  state.pendingWeaponAutoFitKey = key;
  const current = viewportTransformTargets.get(key);
  if (current && viewportLiveThree) {
    state.pendingWeaponAutoFitKey = "";
    await autoFitViewportTransformTarget(viewportLiveThree, current);
  }
}

function refreshViewportOverlay(host) {
  if (!host) return;
  host.querySelectorAll(".weapon-data-overlay, .weapon-focus-overlay").forEach(node => node.remove());
  if (state.viewportMode === "weapon") {
    const item = getWeaponVisualizationItemForWeapon(getFocusedWeaponAttachment());
    host.insertAdjacentHTML("beforeend", renderWeaponFocusOverlayHtml(item));
    wireWeaponFocusOverlay(host);
    return;
  }
  const activeSlotList = getDefaultWeaponSlotList();
  const items = buildWeaponVisualizationItems(state.package, activeSlotList, getResourceUnitySync);
  host.insertAdjacentHTML("beforeend", renderWeaponDataOverlayHtml(items, activeSlotList));
  wireWeaponDataOverlay(host);
}

function refreshViewportLiveData() {
  if (!viewportLiveThree || !viewportLiveContent) {
    viewportRefreshOverlay?.();
    return;
  }
  for (const binding of viewportLivePoseBindings) {
    setObjectLocalPose(binding.THREE || viewportLiveThree, binding.object, binding.pose, { poseAdapter: binding.poseAdapter });
  }
  for (const binding of viewportLiveContactPointBindings) {
    const pose = createContactPointPreviewPose(binding.contactPoint, binding.motionSocket);
    setObjectLocalPose(binding.THREE || viewportLiveThree, binding.object, pose, { poseAdapter: binding.poseAdapter });
  }
  updatePoseLines(viewportLiveThree, viewportLiveContent);
  viewportRefreshOverlay?.();
  renderViewportModeBar();
}

function registerViewportPoseBinding(THREE, object, pose, options = {}) {
  if (!object || !pose) return;
  viewportLivePoseBindings.push({ THREE, object, pose, poseAdapter: options.poseAdapter || "" });
}

function registerViewportContactPointBinding(THREE, object, contactPoint, motionSocket, options = {}) {
  if (!object || !contactPoint) return;
  viewportLiveContactPointBindings.push({
    THREE,
    object,
    contactPoint,
    motionSocket: motionSocket || null,
    poseAdapter: options.poseAdapter || ""
  });
}

function refreshContactPointLiveBinding(contactPoint) {
  if (!contactPoint || !viewportLiveThree) return false;
  let refreshed = false;
  for (const binding of viewportLiveContactPointBindings) {
    if (binding.contactPoint !== contactPoint || !binding.object) continue;
    const pose = createContactPointPreviewPose(binding.contactPoint, binding.motionSocket);
    setObjectLocalPose(binding.THREE || viewportLiveThree, binding.object, pose, { poseAdapter: binding.poseAdapter });
    refreshed = true;
  }
  return refreshed;
}

function setObjectLocalPose(THREE, object, pose, options = {}) {
  if (!object || !pose) return;
  object.position.set(0, 0, 0);
  object.quaternion.identity();
  object.scale.set(1, 1, 1);
  applyLocalPose(THREE, object, pose, options);
}

function bindModelResource(resourceKey) {
  if (!resourceKey || !state.package) return;
  const resource = (state.package.resourceCatalog?.entries || []).find(entry => entry.resourceKey === resourceKey);
  if (!resource) return;
  ensureModelWrapperPose(resource);

  const role = el.resourceBindingRole?.value || "preview";
  const path = `resources/${resource.resourceKey}`;
  if (!state.canWrite || role === "preview") {
    state.selectedPath = path;
    state.resourcePickerOpen = false;
    state.message = role === "preview"
      ? `已选择资源：${getResourceDisplayName(resource)}`
      : "静态预览可以选择资源，但不能替换绑定。";
    renderTree();
    renderResourceBindingBar();
    renderResourcePicker();
    renderViewport();
    renderInspector();
    renderShellStatus();
    return;
  }

  if (role === "body") {
    bindBodyModelResource(resource);
  } else if (role === "mainHand" || role === "offHand") {
    if (!bindWeaponSlotResource(role, resource)) return;
  } else {
    state.selectedPath = path;
    render();
    return;
  }

  if (role === "body") {
    ensureApplicationResourceKey(resource.resourceKey);
  }
  touchModelResource(resource, role);
  state.selectedPath = path;
  state.dirty = true;
  state.resourcePickerOpen = false;
  state.message = `${getResourceDisplayName(resource)} 已绑定到${getResourceBindingRole().label}。资源资产仍由 Resource Manager 管理。`;
  render();
}

function clearCurrentModelBinding() {
  if (!state.canWrite || !state.package) return;
  const role = el.resourceBindingRole?.value || "preview";
  if (role === "preview") return;

  const removedKeys = [];
  if (role === "body") {
    for (const resource of getModelResources(state.package)) {
      if (isBodyModelBinding(resource, state.package)) {
        removedKeys.push(resource.resourceKey);
        removeTag(resource.tags, "body");
        removeApplicationResourceKey(resource.resourceKey);
      }
    }
  } else {
    const attachment = (state.package.geometry?.weaponAttachments || []).find(item => item.equipSlot === role);
    if (!attachment) {
      state.message = `没有 ${role} 武器挂接。`;
      renderShellStatus();
      return;
    }
    const attachmentKey = getWeaponAttachmentResourceKey(attachment);
    if (attachmentKey) removedKeys.push(attachmentKey);
    clearWeaponAttachmentSelection(attachment);
  }

  for (const resourceKey of removedKeys) {
    const resource = findModelResourceByKey(resourceKey);
    if (resource && role !== "body") {
      removeTag(resource.tags, role);
    }
  }

  state.selectedPath = role === "body" ? "geometry/body" : findWeaponPathBySlot(role);
  state.dirty = true;
  state.message = `${getResourceBindingRole().label}绑定已清空。资源资产没有被删除。`;
  render();
}

function createConfiguration(kind) {
  if (!state.canWrite || !state.package) {
    state.message = "静态预览不能创建配置；请启动 Authoring 服务。";
    renderShellStatus();
    return;
  }

  if (kind === "part" || kind === "collider") return createBodyWorkspaceItem(kind);
  ensureGeometryCollections();
  let path = "";
  let label = "";
  if (kind === "socket") {
    const item = createSocketConfig();
    state.package.geometry.sockets.push(item);
    path = `geometry/sockets/${item.socketId}`;
    label = "Socket";
  } else if (kind === "weapon") {
    const item = createWeaponConfig();
    state.package.geometry.weaponAttachments.push(item);
    path = getWeaponAttachmentPath(item);
    label = "武器挂接";
  } else if (kind === "trace") {
    const item = createTraceConfig();
    state.package.geometry.traces.push(item);
    path = `geometry/traces/${item.traceId}`;
    label = "攻击轨迹";
  } else if (kind === "contactPoint") {
    const item = createContactPointConfig();
    state.package.geometry.bodyPartContactPoints.push(item);
    if (state.symmetryEditEnabled) {
      const mirror = createMirroredContactPointConfig(item);
      if (mirror) state.package.geometry.bodyPartContactPoints.push(mirror);
    }
    path = `geometry/body_part_contact_points/${item.stableId}`;
    label = "身体接触音频点";
  } else {
    return;
  }

  state.selectedPath = path;
  state.dirty = true;
  state.message = kind === "contactPoint" && state.symmetryEditEnabled
    ? `已创建${label}；如果找到侧向匹配，也已创建镜像点。`
    : `已创建${label}。`;
  render();
}

function ensureGeometryCollections(pkg = state.package) {
  if (!pkg) return;
  pkg.geometry = pkg.geometry || {};
  const geometry = pkg.geometry;
  geometry.schemaVersion = geometry.schemaVersion || "1.0";
  geometry.bodyProfile = ensureBodyProfile(geometry.bodyProfile || {});
  geometry.bodyParts = Array.isArray(geometry.bodyParts) ? geometry.bodyParts : [];
  geometry.colliders = Array.isArray(geometry.colliders) ? geometry.colliders : [];
  geometry.sockets = Array.isArray(geometry.sockets) ? geometry.sockets : [];
  geometry.weaponAttachments = Array.isArray(geometry.weaponAttachments) ? geometry.weaponAttachments : [];
  for (const attachment of geometry.weaponAttachments) {
    if (!attachment) continue;
    attachment.slotId ||= attachment.equipSlot || "";
    attachment.equipSlot ||= attachment.slotId || "";
    attachment.attachmentId ||= `mount.${attachment.weaponId || "weapon"}.${attachment.slotId || "slot"}`;
  }
  geometry.traces = Array.isArray(geometry.traces) ? geometry.traces : [];
  geometry.bodyPartContactPoints = Array.isArray(geometry.bodyPartContactPoints) ? geometry.bodyPartContactPoints : [];
}

function ensureBodyProfile(profile = {}) {
  const body = (profile && typeof profile === "object") ? profile : {};
  body.profileId = String(body.profileId || "geometry.body");
  body.bodyKind = String(body.bodyKind || "Skeletal");
  body.bodyScale = numberOrDefault(body.bodyScale, 1);
  body.heightMeters = numberOrDefault(body.heightMeters, 1.8);
  body.radiusMeters = numberOrDefault(body.radiusMeters, 0.35);
  body.massKg = numberOrDefault(body.massKg, 75);
  body.defaultCapsule = (body.defaultCapsule && typeof body.defaultCapsule === "object") ? body.defaultCapsule : {};
  body.defaultCapsule.height = numberOrDefault(body.defaultCapsule.height, body.heightMeters);
  body.defaultCapsule.radius = numberOrDefault(body.defaultCapsule.radius, body.radiusMeters);
  body.defaultCapsule.center = (body.defaultCapsule.center && typeof body.defaultCapsule.center === "object") ? body.defaultCapsule.center : {};
  body.defaultCapsule.center.x = numberOrDefault(body.defaultCapsule.center.x, 0);
  body.defaultCapsule.center.y = numberOrDefault(body.defaultCapsule.center.y, 0.9);
  body.defaultCapsule.center.z = numberOrDefault(body.defaultCapsule.center.z, 0);
  body.defaultPhysicsProfileId = String(body.defaultPhysicsProfileId || "");
  body.modelRootStableId = String(body.modelRootStableId || "");
  body.skeletonRootStableId = String(body.skeletonRootStableId || "");
  body.locatorRootStableId = String(body.locatorRootStableId || "");
  return body;
}

function createBodyPartConfig(preferredParentId = "") {
  const partId = uniqueConfigId("part_new", (state.package.geometry.bodyParts || []).map(part => part?.partId));
  return {
    partId,
    displayName: "新身体部位",
    partKind: "Bone",
    parentPartId: preferredParentId || firstValue(bodyPartOptions()),
    bonePath: "",
    locatorId: "",
    defaultHitZoneId: `hit.${partId}`,
    reactionGroupId: "reaction.humanoid.limb",
    tags: []
  };
}

function createColliderConfig(preferredPartId = "") {
  const partId = preferredPartId || globalThis.getBodyWorkspaceSelection?.().part?.partId || firstValue(bodyPartOptions()) || "";
  const part = (state.package.geometry.bodyParts || []).find(item => item?.partId === partId);
  const colliderId = uniqueConfigId(`${partId || "body"}_sphere`, (state.package.geometry.colliders || []).map(collider => collider?.colliderId));
  return {
    colliderId,
    partId,
    hitZoneId: part?.defaultHitZoneId || "",
    shape: "Sphere",
    localPose: defaultLocalPose(part?.bonePath ? "Bone" : part?.locatorId ? "Locator" : "BodyPart", part?.bonePath || part?.locatorId || partId),
    size: { x: 0.25, y: 0.25, z: 0.25 },
    radius: 0.15,
    height: 0,
    priority: 10,
    isWeakPoint: false,
    damageMultiplierOverride: 1,
    impactPowerScaleOverride: 1
  };
}

function createSocketConfig() {
  const partId = firstValue(bodyPartOptions()) || "";
  const socketId = uniqueConfigId("socket_new", (state.package.geometry.sockets || []).map(socket => socket?.socketId));
  return {
    socketId,
    abstractBoneId: "",
    parentPartId: partId,
    bonePath: "",
    locatorPath: "",
    localPose: defaultLocalPose(partId ? "BodyPart" : "ModelRoot", partId),
    usage: "Weapon",
    mirrorPairSocketId: "",
    handedness: "None",
    sideTag: "Center",
    tags: ["weapon"]
  };
}

function createWeaponConfig() {
  const socketId = firstValue(socketOptions()) || "";
  const slot = nextEquipSlot();
  const singleWeaponConfigurationId = (state.weaponLibrary?.singleWeaponConfigurations || [])
    .find(item => item?.singleWeaponConfigurationId && item.singleWeaponConfigurationId !== "weapon.unarmed")
    ?.singleWeaponConfigurationId || "";
  const attachmentId = uniqueConfigId(`mount.${singleWeaponConfigurationId || "new"}.${slot || "new"}`, (state.package.geometry.weaponAttachments || []).map(attachment => attachment?.attachmentId));
  return {
    attachmentId,
    singleWeaponConfigurationId,
    slotId: slot || "mainHand",
    attachSocketId: socketId,
    localGripPose: defaultLocalPose(socketId ? "Socket" : "ModelRoot", socketId),
    traceId: "",
    traceStartSocketId: socketId,
    traceEndSocketId: "",
    traceRadius: 0.05,
    traceSampleRule: "CapsuleSweep"
  };
}

function createTraceConfig() {
  const weapon = (state.package.geometry.weaponAttachments || [])[0] || null;
  const socketId = weapon?.attachSocketId || firstValue(socketOptions()) || "";
  const traceId = uniqueConfigId("trace.new.blade", (state.package.geometry.traces || []).map(trace => trace?.traceId));
  return {
    traceId,
    weaponId: weapon?.weaponId || "",
    equipSlot: weapon?.equipSlot || "mainHand",
    startLocatorPath: "",
    endLocatorPath: "",
    startPose: defaultLocalPose(socketId ? "Socket" : "ModelRoot", socketId),
    endPose: {
      ...defaultLocalPose(socketId ? "Socket" : "ModelRoot", socketId),
      position: { x: 0, y: 0.8, z: 0 }
    },
    radius: 0.05,
    sampleRule: "CapsuleSweep",
    fixedSampleCount: 6,
    actionKeys: ["primary"]
  };
}

function createContactPointConfig() {
  const partId = getDefaultContactPointPartId();
  const contactPointId = nextNumericId((state.package.geometry.bodyPartContactPoints || []).map(item => item?.contactPointId));
  const sourceOrder = nextNumericId((state.package.geometry.bodyPartContactPoints || []).map(item => item?.sourceOrder));
  return {
    stableId: uniqueConfigId(`contact.${partId || "body"}.point`, (state.package.geometry.bodyPartContactPoints || []).map(item => item?.stableId)),
    contactPointId,
    bodyPartId: inferContactPointBodyPartId(partId, contactPointId),
    partId,
    localOffsetMeters: { x: 0, y: 0, z: 0 },
    radiusMeters: 0.09,
    layerMaskProfileId: "ground",
    usage: "PresentationAudio",
    authority: "PresentationOnly",
    activationMode: "Always",
    motionMode: "Static",
    motionSocketId: "",
    sourceOrder
  };
}

function createMirroredContactPointConfig(contactPoint) {
  if (!contactPoint) return null;
  const contactPoints = state.package?.geometry?.bodyPartContactPoints || [];
  const mirroredPartId = findMirroredBodyPartId(contactPoint.partId || "");
  const mirroredStableId = mirrorSideToken(contactPoint.stableId || "");
  const hasMirrorIdentity = (mirroredPartId && mirroredPartId !== contactPoint.partId)
    || (mirroredStableId && mirroredStableId !== contactPoint.stableId);
  if (!hasMirrorIdentity) return null;

  const contactPointId = nextNumericId(contactPoints.map(item => item?.contactPointId));
  const sourceOrder = nextNumericId(contactPoints.map(item => item?.sourceOrder));
  const offset = ensureContactPointOffset(contactPoint);
  return {
    stableId: uniqueConfigId(
      mirroredStableId || `contact.${mirroredPartId || "body"}.point`,
      contactPoints.map(item => item?.stableId)),
    contactPointId,
    bodyPartId: inferContactPointBodyPartId(mirroredPartId, contactPointId),
    partId: mirroredPartId || mirrorSideToken(contactPoint.partId || ""),
    localOffsetMeters: {
      x: roundTransformNumber(-numberOrDefault(offset.x, 0)),
      y: roundTransformNumber(numberOrDefault(offset.y, 0)),
      z: roundTransformNumber(numberOrDefault(offset.z, 0))
    },
    radiusMeters: roundTransformNumber(numberOrDefault(contactPoint.radiusMeters, 0.09)),
    layerMaskProfileId: contactPoint.layerMaskProfileId || "ground",
    usage: contactPoint.usage || "PresentationAudio",
    authority: contactPoint.authority || "PresentationOnly",
    activationMode: contactPoint.activationMode || "Always",
    motionMode: contactPoint.motionMode || "Static",
    motionSocketId: contactPoint.motionSocketId ? mirrorSideToken(contactPoint.motionSocketId) : "",
    sourceOrder
  };
}

function findMirroredBodyPartId(partId) {
  const mirrored = mirrorSideToken(partId || "");
  if (!mirrored || mirrored === partId) return "";
  const parts = state.package?.geometry?.bodyParts || [];
  const match = parts.find(part => part?.partId === mirrored);
  return match ? mirrored : "";
}

function getDefaultContactPointPartId() {
  const target = findTarget(state.selectedPath || "");
  const value = target?.value || {};
  const selectedPartId = value.partId || value.parentPartId || "";
  if (selectedPartId) return selectedPartId;
  return firstValue(bodyPartOptions()) || "";
}

function nextNumericId(values) {
  let max = 0;
  for (const value of values || []) {
    const numeric = Number(value);
    if (Number.isFinite(numeric) && numeric > max) max = numeric;
  }
  return max + 1;
}

function inferContactPointBodyPartId(partId, fallbackId) {
  const normalized = String(partId || "").toLowerCase();
  if (normalized.includes("left") && normalized.includes("leg")) return 701;
  if (normalized.includes("right") && normalized.includes("leg")) return 702;
  if (normalized.includes("left") && normalized.includes("hand")) return 703;
  if (normalized.includes("right") && normalized.includes("hand")) return 704;
  if (normalized.includes("head")) return 710;
  if (normalized.includes("torso") || normalized.includes("core")) return 720;
  return 700 + Math.max(1, Number(fallbackId) || 1);
}

function defaultLocalPose(parentKind = "ModelRoot", parentPath = "") {
  return {
    parentKind,
    parentPath: parentPath || "",
    position: { x: 0, y: 0, z: 0 },
    rotation: { x: 0, y: 0, z: 0, w: 1 },
    scale: { x: 1, y: 1, z: 1 },
    eulerHint: { x: 0, y: 0, z: 0 }
  };
}

function nextEquipSlot() {
  const used = new Set((state.package?.geometry?.weaponAttachments || []).map(item => item?.equipSlot).filter(Boolean));
  return ["mainHand", "offHand", "twoHand", "naturalWeapon"].find(slot => !used.has(slot)) || "mainHand";
}

function uniqueConfigId(base, existingValues) {
  const normalizedBase = String(base || "new").replace(/\s+/g, "_");
  const existing = new Set((existingValues || []).filter(Boolean).map(String));
  if (!existing.has(normalizedBase)) return normalizedBase;
  for (let i = 2; i < 1000; i++) {
    const candidate = `${normalizedBase}_${i}`;
    if (!existing.has(candidate)) return candidate;
  }
  return `${normalizedBase}_${Date.now()}`;
}

function ensureModelWrapperPose(resource) {
  resource.importHints = resource.importHints || {};
  return ensureLocalPose(resource.importHints, "modelWrapperPose", { parentKind: "ModelRoot", parentPath: "" });
}

function ensureLocalPose(owner, posePath, defaults = {}) {
  if (!owner) return null;
  let pose = getNested(owner, posePath);
  if (!pose) {
    pose = {};
    setNested(owner, posePath, pose);
  }
  ensurePoseObject(pose, defaults);
  return pose;
}

function ensurePoseObject(pose, defaults = {}) {
  if (!pose) return null;
  pose.parentKind = pose.parentKind || defaults.parentKind || "ModelRoot";
  pose.parentPath = pose.parentPath ?? defaults.parentPath ?? "";
  pose.position = pose.position || {};
  pose.rotation = pose.rotation || {};
  pose.scale = pose.scale || {};
  pose.eulerHint = pose.eulerHint || {};
  pose.position.x = Number(pose.position.x || 0);
  pose.position.y = Number(pose.position.y || 0);
  pose.position.z = Number(pose.position.z || 0);
  pose.rotation.x = Number(pose.rotation.x || 0);
  pose.rotation.y = Number(pose.rotation.y || 0);
  pose.rotation.z = Number(pose.rotation.z || 0);
  pose.rotation.w = Number(pose.rotation.w ?? 1);
  pose.scale.x = Number(pose.scale.x || 1);
  pose.scale.y = Number(pose.scale.y || 1);
  pose.scale.z = Number(pose.scale.z || 1);
  pose.eulerHint.x = Number(pose.eulerHint.x || 0);
  pose.eulerHint.y = Number(pose.eulerHint.y || 0);
  pose.eulerHint.z = Number(pose.eulerHint.z || 0);
  return pose;
}

function bindBodyModelResource(resource) {
  for (const entry of getModelResources(state.package)) {
    if (entry.resourceKey !== resource.resourceKey && isBodyModelBinding(entry, state.package)) {
      removeTag(entry.tags, "body");
      removeApplicationResourceKey(entry.resourceKey);
    }
  }
  resource.usage = "characterModel";
  addTagValue(resource, "body");
}

function bindWeaponSlotResource(slot, resource) {
  const attachments = state.package?.geometry?.weaponAttachments || [];
  const attachment = attachments.find(item => item.equipSlot === slot);
  if (!attachment) {
    state.message = `没有 ${slot} 武器挂接。请先创建挂接。`;
    renderShellStatus();
    return false;
  }

  const previousKey = getWeaponAttachmentResourceKey(attachment);
  const selection = createPackageResourceSelectionRef(resource, "weaponModel");
  applyWeaponAttachmentSelection(attachment, selection, resource.resourceKey);
  if (resource.usage !== "characterModel") {
    resource.usage = "weaponModel";
  }
  addTagValue(resource, "weapon");
  addTagValue(resource, slot);

  if (previousKey && previousKey !== resource.resourceKey) {
    const previousResource = findModelResourceByKey(previousKey);
    if (previousResource) {
      removeTag(previousResource.tags, slot);
    }
  }

  return true;
}

function findModelResourceByKey(resourceKey) {
  if (!resourceKey) return null;
  return getModelResources(state.package).find(resource => resource.resourceKey === resourceKey) || null;
}

function ensureApplicationResourceKey(resourceKey) {
  const appConfig = state.package.applicationConfig || {};
  state.package.applicationConfig = appConfig;
  appConfig.resourceKeys = Array.isArray(appConfig.resourceKeys) ? appConfig.resourceKeys : [];
  if (!appConfig.resourceKeys.includes(resourceKey)) appConfig.resourceKeys.push(resourceKey);
}

function removeApplicationResourceKey(resourceKey) {
  const resourceKeys = state.package?.applicationConfig?.resourceKeys;
  if (!Array.isArray(resourceKeys)) return;
  const index = resourceKeys.indexOf(resourceKey);
  if (index >= 0) resourceKeys.splice(index, 1);
}

function isBodyModelBinding(resource, pkg) {
  if (!resource?.resourceKey || resource.typeId !== "model") return false;
  if (hasTag(resource, "body")) return true;
  return resource.usage === "characterModel" && isApplicationResourceReference(resource.resourceKey, pkg);
}

function isApplicationResourceReference(resourceKey, pkg) {
  const resourceKeys = pkg?.applicationConfig?.resourceKeys;
  return Array.isArray(resourceKeys) && resourceKeys.includes(resourceKey);
}

function findWeaponPathBySlot(slot) {
  const configuredSlot = (getActiveWeaponConfiguration()?.slots || []).find(item => item?.slotId === slot);
  const attachment = configuredSlot ? findWeaponAttachmentById(configuredSlot.attachmentId) : null;
  return attachment ? getWeaponAttachmentPath(attachment) : "resources";
}

function touchModelResource(resource, role) {
  resource.provenance = resource.provenance || {};
  resource.provenance.modifiedUtc = new Date().toISOString();
  addTagValue(resource, "characterstudio-bind");
  addTagValue(resource, role);
}

function addTagValue(resource, tag) {
  if (!tag) return;
  resource.tags = Array.isArray(resource.tags) ? resource.tags : [];
  if (!resource.tags.some(existing => String(existing).toLowerCase() === String(tag).toLowerCase())) {
    resource.tags.push(tag);
  }
}

function hasTag(resource, tag) {
  if (!Array.isArray(resource?.tags)) return false;
  return resource.tags.some(existing => String(existing).toLowerCase() === String(tag).toLowerCase());
}

function removeTag(tags, tag) {
  if (!Array.isArray(tags)) return;
  const index = tags.findIndex(existing => String(existing).toLowerCase() === String(tag).toLowerCase());
  if (index >= 0) tags.splice(index, 1);
}

function renderPreviewControls() {
  if (!el.previewPoseSelect || !el.previewMotionSelect) return;
  el.previewPoseSelect.innerHTML = PREVIEW_POSES
    .map(pose => `<option value="${escapeHtml(pose.id)}"${pose.id === state.previewPose ? " selected" : ""}>${escapeHtml(pose.label)}</option>`)
    .join("");
  el.previewMotionSelect.innerHTML = PREVIEW_MOTIONS
    .map(motion => `<option value="${escapeHtml(motion.id)}"${motion.id === state.previewMotion ? " selected" : ""}>${escapeHtml(motion.label)}</option>`)
    .join("");

  const geometry = state.package?.geometry || {};
  const colliders = geometry.colliders || [];
  const parts = getHurtboxDisplayParts(geometry);
  const mode = normalizeHurtboxDisplayMode(state.hurtboxDisplayMode);
  const partId = ensureHurtboxDisplayPartId(geometry);
  const visible = getVisibleHurtboxColliders(colliders, geometry);
  if (el.hurtboxDisplayControls) {
    el.hurtboxDisplayControls.hidden = state.viewportMode !== "character" || !isCharacterPreviewLayerEnabled("colliders") || colliders.length === 0;
  }
  if (el.hurtboxDisplayModeSelect) el.hurtboxDisplayModeSelect.value = mode;
  if (el.hurtboxPartFilterLabel) el.hurtboxPartFilterLabel.hidden = mode !== "part";
  if (el.hurtboxPartFilterSelect) {
    el.hurtboxPartFilterSelect.innerHTML = parts
      .map(part => `<option value="${escapeHtml(part.partId)}"${part.partId === partId ? " selected" : ""}>${escapeHtml(part.displayName)} · ${part.count}</option>`)
      .join("");
    el.hurtboxPartFilterSelect.disabled = parts.length === 0;
  }
  if (el.hurtboxDisplaySummary) {
    if (mode === "selected" && visible.length === 0) el.hurtboxDisplaySummary.textContent = "未选择受击体";
    else el.hurtboxDisplaySummary.textContent = `${visible.length} / ${colliders.length}`;
  }
  if (el.hurtboxBindingReferenceButton) {
    const selectedCollider = getSelectedHurtboxCollider(geometry);
    const workspaceAvailable = state.viewportMode === "character" && isCharacterPreviewLayerEnabled("colliders") && colliders.length > 0;
    el.hurtboxBindingReferenceButton.hidden = !workspaceAvailable;
    el.hurtboxBindingReferenceButton.disabled = !workspaceAvailable;
    el.hurtboxBindingReferenceButton.classList.toggle("active", workspaceAvailable && Boolean(state.hurtboxBindingReferenceVisible));
    el.hurtboxBindingReferenceButton.setAttribute("aria-pressed", String(workspaceAvailable && Boolean(state.hurtboxBindingReferenceVisible)));
    if (!workspaceAvailable) {
      el.hurtboxBindingReferenceButton.title = "当前角色没有可编辑的受击体";
    } else if (!selectedCollider) {
      el.hurtboxBindingReferenceButton.title = state.hurtboxBindingReferenceVisible
        ? "绑定参考已开启；选择一个受击体即可显示参考"
        : "开启绑定参考；选择一个受击体后显示参考";
    } else {
      el.hurtboxBindingReferenceButton.title = state.hurtboxBindingReferenceVisible
        ? "隐藏选中受击体的绑定骨骼、有效父坐标系和 localPose 前后对照"
        : "显示选中受击体的绑定骨骼、有效父坐标系和 localPose 前后对照";
    }
  }
  if (el.hurtboxCoverageButton) {
    const coverageAvailable = isHurtboxCoverageScopeAvailable();
    el.hurtboxCoverageButton.disabled = !coverageAvailable;
    el.hurtboxCoverageButton.title = coverageAvailable
      ? "从前后左右上下六个方向比较当前动画帧的模型轮廓与 Hurtbox 覆盖"
      : "六向覆盖热图需要将受击体显示范围切换为“全部”";
  }
}

function normalizeHurtboxDisplayMode(value) {
  return value === "selected" || value === "all" ? value : "part";
}

function getHurtboxDisplayParts(geometry = state.package?.geometry || {}) {
  const colliders = geometry.colliders || [];
  const counts = new Map();
  for (const collider of colliders) {
    const partId = String(collider?.partId || "").trim();
    if (partId) counts.set(partId, (counts.get(partId) || 0) + 1);
  }
  const parts = [];
  const seen = new Set();
  for (const part of geometry.bodyParts || []) {
    const partId = String(part?.partId || "").trim();
    if (!partId || !counts.has(partId) || seen.has(partId)) continue;
    seen.add(partId);
    parts.push({
      partId,
      displayName: String(part?.displayName || partId),
      count: counts.get(partId)
    });
  }
  for (const [partId, count] of counts) {
    if (seen.has(partId)) continue;
    parts.push({ partId, displayName: partId, count });
  }
  return parts;
}

function getSelectedHurtboxCollider(geometry = state.package?.geometry || {}) {
  const prefix = "geometry/colliders/";
  const path = String(state.selectedPath || "");
  if (!path.startsWith(prefix)) return null;
  const colliderId = decodeURIComponent(path.slice(prefix.length));
  return (geometry.colliders || []).find(collider => String(collider?.colliderId || "") === colliderId) || null;
}

function getSelectedHurtboxPartId(geometry = state.package?.geometry || {}) {
  const collider = getSelectedHurtboxCollider(geometry);
  if (collider?.partId) return String(collider.partId);
  const prefix = "geometry/body_parts/";
  const path = String(state.selectedPath || "");
  if (!path.startsWith(prefix)) return "";
  const partId = decodeURIComponent(path.slice(prefix.length));
  return (geometry.bodyParts || []).some(part => String(part?.partId || "") === partId) ? partId : "";
}

function ensureHurtboxDisplayPartId(geometry = state.package?.geometry || {}) {
  if (globalThis.isBodyWorkspacePath?.()) return getBodyWorkspaceSelection(state.selectedPath, geometry).partId || state.hurtboxDisplayPartId || "";
  state.hurtboxDisplayMode = normalizeHurtboxDisplayMode(state.hurtboxDisplayMode);
  const parts = getHurtboxDisplayParts(geometry);
  const validIds = new Set(parts.map(part => part.partId));
  const selectedPartId = getSelectedHurtboxPartId(geometry);
  if (state.hurtboxDisplayMode === "part" && selectedPartId && validIds.has(selectedPartId)) {
    state.hurtboxDisplayPartId = selectedPartId;
  }
  if (!validIds.has(String(state.hurtboxDisplayPartId || ""))) {
    state.hurtboxDisplayPartId = parts[0]?.partId || "";
  }
  return state.hurtboxDisplayPartId;
}

function syncHurtboxDisplayPartToSelection(target) {
  if (globalThis.isBodyWorkspacePath?.()) return false;
  if (normalizeHurtboxDisplayMode(state.hurtboxDisplayMode) !== "part") return false;
  const partId = target?.kind === "part" || target?.kind === "collider"
    ? String(target?.value?.partId || "")
    : "";
  if (!partId || partId === state.hurtboxDisplayPartId) return false;
  state.hurtboxDisplayPartId = partId;
  return true;
}

function isHurtboxCoverageScopeAvailable() {
  const body = globalThis.getBodyWorkspaceSelection?.();
  if (body?.section) return body.section === "parts" && getBodyPartsPreviewScope() === "all";
  return state.hurtboxDisplayMode === "all";
}

function getVisibleHurtboxColliders(colliders = [], geometry = state.package?.geometry || {}) {
  if (globalThis.isBodyWorkspacePath?.()) return getBodyWorkspaceVisibleColliders(colliders, geometry);
  const mode = normalizeHurtboxDisplayMode(state.hurtboxDisplayMode);
  if (mode === "all") return colliders;
  if (mode === "selected") {
    const selected = getSelectedHurtboxCollider(geometry);
    return selected ? colliders.filter(collider => collider === selected || collider.colliderId === selected.colliderId) : [];
  }
  const partId = ensureHurtboxDisplayPartId(geometry);
  if (!partId) return colliders;
  return colliders.filter(collider => String(collider?.partId || "") === partId);
}

function syncHurtboxMeshVisibility(content, geometry = state.package?.geometry || {}) {
  const show = isCharacterPreviewLayerEnabled("colliders");
  const visibleIds = new Set(getVisibleHurtboxColliders(geometry.colliders || [], geometry).map(collider => collider.colliderId));
  content.traverse(object => {
    if (object.userData?.hurtbox) object.visible = show && visibleIds.has(object.userData.hurtbox.colliderId);
  });
}

function isViewportObjectVisible(object) {
  for (let current = object; current; current = current.parent) {
    if (!current.visible) return false;
  }
  return true;
}

function renderViewportModeBar() {
  if (!el.viewWeaponConfigButton || !el.returnCharacterViewButton || !el.viewportModeStatus) return;
  const isWeaponMode = state.viewportMode === "weapon";
  const focusedWeapon = getFocusedWeaponAttachment();
  const candidate = getCurrentOrFirstActiveWeaponAttachment();
  const canTransformSelection = canUseViewportTransformForSelection();
  const canRotateSelection = canRotateViewportTransformSelection();
  const selectedTarget = findTarget(state.selectedPath || "");
  if (el.weaponTransformScaleButton) {
    el.weaponTransformScaleButton.textContent = selectedTarget.kind === "collider" ? "尺寸" : "缩放";
    el.weaponTransformScaleButton.title = selectedTarget.kind === "collider"
      ? "R：使用形状专用尺寸手柄"
      : "R：缩放";
  }
  el.viewWeaponConfigButton.hidden = isWeaponMode;
  el.viewWeaponConfigButton.disabled = !candidate?.weaponId;
  el.returnCharacterViewButton.hidden = !isWeaponMode;
  if (el.weaponModeChooseModelButton) {
    el.weaponModeChooseModelButton.hidden = !isWeaponMode;
    el.weaponModeChooseModelButton.disabled = !focusedWeapon?.weaponId;
  }
  if (el.weaponModeEditTraceButton) {
    el.weaponModeEditTraceButton.hidden = !isWeaponMode;
    el.weaponModeEditTraceButton.disabled = !focusedWeapon?.traceId;
  }
  for (const item of [
    [el.weaponTransformMoveButton, "translate"],
    [el.weaponTransformRotateButton, "rotate"],
    [el.weaponTransformScaleButton, "scale"]
  ]) {
    const [button, mode] = item;
    if (!button) continue;
    const modeAvailable = mode !== "rotate" || canRotateSelection;
    button.hidden = !canTransformSelection || !modeAvailable;
    button.classList.toggle("active", state.weaponTransformMode === mode);
    button.disabled = !canTransformSelection || !modeAvailable;
  }
  if (el.symmetryEditToggleButton) {
    el.symmetryEditToggleButton.classList.toggle("active", Boolean(state.symmetryEditEnabled));
    el.symmetryEditToggleButton.setAttribute("aria-pressed", state.symmetryEditEnabled ? "true" : "false");
    el.symmetryEditToggleButton.disabled = !state.canWrite || !state.package;
    el.symmetryEditToggleButton.title = state.symmetryEditEnabled
      ? "对称编辑已启用；编辑一侧时会把受控变换和接触音频点字段镜像到配对侧。"
      : "为 Socket、武器握持、身体碰撞体和接触音频点启用对称编辑。";
  }
  if (el.colliderSymmetrySyncButton) {
    const visibleColliderCount = Array.from(viewportTransformTargets.values())
      .filter(item => item?.transformKind === "collider").length;
    const selectedCollider = getSelectedHurtboxCollider();
    el.colliderSymmetrySyncButton.disabled = !state.canWrite || visibleColliderCount < 2 || !selectedCollider;
    el.colliderSymmetrySyncButton.title = !selectedCollider
      ? "请先选择一侧受击框"
      : visibleColliderCount < 2
        ? "请先显示当前部位的左右受击框，再执行左→右同步"
        : "把选中部位族的左侧受击框按角色世界空间镜像到右侧";
  }
  if (isWeaponMode) {
    const transformLabel = getWeaponTransformStatusLabel();
    el.viewportModeStatus.textContent = focusedWeapon
      ? `武器视图：${getEquipSlotLabel(focusedWeapon.equipSlot)} / ${focusedWeapon.weaponId}${transformLabel ? ` / ${transformLabel}` : ""}`
      : "武器视图：没有聚焦武器";
  } else {
    const transformTarget = getActiveViewportTransformTarget();
    if (transformTarget?.transformKind === "collider") {
      const modeLabel = { translate: "移动", rotate: "旋转", scale: "尺寸" }[state.weaponTransformMode] || "移动";
      el.viewportModeStatus.textContent = `手动编辑受击体：${transformTarget.collider?.colliderId || transformTarget.label} / ${modeLabel}（W/E/R，F 聚焦）`;
    } else {
      el.viewportModeStatus.textContent = candidate
        ? `角色视图：可查看 ${getEquipSlotLabel(candidate.equipSlot)} / ${candidate.weaponId}`
        : hasEmbeddedWeaponSource()
          ? "角色视图：源模型内嵌武器，尚未配置为框架武器"
          : "角色视图：没有配置默认武器";
    }
  }
}

function canUseViewportTransformForSelection() {
  if (state.viewportMode === "weapon") return Boolean(getActiveViewportTransformTarget());
  const target = findTarget(state.selectedPath || "");
  return target.kind === "socket" || target.kind === "collider" || target.kind === "contactPoint";
}

function canRotateViewportTransformSelection() {
  if (getActiveViewportTransformTarget()?.transformKind === "traceScalar") return false;
  const target = findTarget(state.selectedPath || "");
  return target.kind !== "contactPoint";
}

function setWeaponTransformMode(mode) {
  const activeTarget = getActiveViewportTransformTarget();
  const normalized = activeTarget?.transformKind === "traceScalar"
    ? "translate"
    : (["translate", "rotate", "scale"].includes(mode) ? mode : "translate");
  state.weaponTransformMode = normalized;
  if (activeTarget?.transformKind === "collider" && normalized === "scale") {
    viewportTransformControls?.detach();
  } else {
    viewportTransformControls?.setMode(normalized);
    if (activeTarget?.object) viewportTransformControls?.attach(activeTarget.object);
  }
  viewportSyncHurtboxShapeHandles?.();
  renderViewportModeBar();
}

function toggleSymmetryEdit() {
  state.symmetryEditEnabled = !state.symmetryEditEnabled;
  state.message = state.symmetryEditEnabled
    ? "对称编辑已启用。受控 Socket、握持、碰撞体和接触音频点编辑会镜像到配对侧。"
    : "对称编辑已关闭。";
  renderViewportModeBar();
  renderShellStatus();
}

function synchronizeVisibleColliderSymmetryFromLeft() {
  if (!state.package || !viewportLiveThree || !viewportLiveContent) return 0;
  const visibleColliders = new Set(Array.from(viewportTransformTargets.values())
    .filter(item => item?.transformKind === "collider" && item.collider)
    .map(item => item.collider));
  const selectedCollider = getSelectedHurtboxCollider();
  const selectedFamily = getColliderMirrorFamily(selectedCollider);
  if (!selectedCollider || !selectedFamily) return 0;
  const sources = (state.package.geometry?.colliders || []).filter(collider =>
    visibleColliders.has(collider)
      && isLeftSideMirrorSource(collider)
      && getColliderMirrorFamily(collider) === selectedFamily);
  beginUndoTransaction("Sync visible colliders left to right", "collider-symmetry-sync");
  let synchronized = 0;
  for (const collider of sources) {
    const mirror = findMirrorCollider(collider);
    if (!mirror || !visibleColliders.has(mirror)) continue;
    if (mirrorColliderEdit(collider, "localPose")) synchronized++;
  }
  updateUndoTransactionAfter();
  commitUndoTransaction();
  refreshDirtyFromCleanSnapshot();
  state.message = synchronized > 0
    ? `已按角色空间从左向右同步 ${synchronized} 组受击框；尚未保存。`
    : "没有找到同时可见的左右受击框配对。";
  renderShellStatus();
  renderInspector();
  renderViewport();
  return synchronized;
}

function isLeftSideMirrorSource(collider) {
  const text = [collider?.colliderId, collider?.partId, collider?.hitZoneId]
    .map(value => String(value || ""))
    .join("/");
  return /Left|left|_l(?=$|[._\/-])|-l(?=$|[._\/-])|\.L(?=$|[._\/-])/.test(text);
}

function getColliderMirrorFamily(collider) {
  const text = [collider?.partId, collider?.colliderId]
    .map(value => String(value || ""))
    .join("/");
  return text.match(/(?:left|right)[._-]([a-z0-9]+)/i)?.[1]?.toLowerCase() || "";
}

function isViewportTransformHandleActive(transformControls) {
  return Boolean(transformControls?.dragging || transformControls?.axis);
}

function getCurrentOrFirstActiveWeaponAttachment() {
  const target = findTarget(state.selectedPath || "");
  if (target.kind === "weapon" && target.value?.weaponId) return target.value;
  const selectedTraceId = target.kind === "trace" ? target.value?.traceId : "";
  if (selectedTraceId) {
    const byTrace = (state.package?.geometry?.weaponAttachments || []).find(weapon => weapon?.traceId === selectedTraceId);
    if (byTrace) return byTrace;
  }
  return getActiveWeaponAttachments()[0] || null;
}

function getFocusedWeaponAttachment() {
  return findWeaponAttachmentById(state.focusedWeaponAttachmentId);
}

function getWeaponAttachmentIdentity(attachment) {
  return attachment?.attachmentId || attachment?.weaponId || "";
}

function getWeaponAttachmentPath(attachment) {
  const identity = getWeaponAttachmentIdentity(attachment);
  return identity ? `geometry/weapon_attachments/${encodeURIComponent(identity)}` : "";
}

function findWeaponAttachmentById(attachmentId) {
  if (!attachmentId) return null;
  if (String(attachmentId).startsWith("single:")) {
    const weaponId = String(attachmentId).slice("single:".length);
    const definition = getSingleWeaponConfiguration(weaponId);
    if (!definition) return null;
    return {
      attachmentId: `single:${weaponId}`,
      singleWeaponConfigurationId: weaponId,
      weaponId,
      slotId: "weaponLocal",
      equipSlot: "weaponLocal",
      attachSocketId: "",
      localGripPose: definition.modelPose || defaultLocalPose(),
      traceId: definition.traceProfiles?.[0]?.traceId || "",
      __singleWeaponPreview: true
    };
  }
  const activeAttachments = getActiveWeaponAttachments();
  const activeAttachment = activeAttachments.find(attachment => getWeaponAttachmentIdentity(attachment) === attachmentId);
  if (activeAttachment) return activeAttachment;
  const attachments = state.package?.geometry?.weaponAttachments || [];
  return attachments.find(attachment => attachment?.attachmentId === attachmentId)
    || attachments.find(attachment => !attachment?.attachmentId && attachment?.weaponId === attachmentId)
    || null;
}

function enterSingleWeaponViewport(singleWeaponConfigurationId) {
  const definition = getSingleWeaponConfiguration(singleWeaponConfigurationId);
  if (!definition || singleWeaponConfigurationId === "weapon.unarmed") return;
  teardownViewportForModeChange();
  state.viewportMode = "weapon";
  state.focusedWeaponAttachmentId = `single:${singleWeaponConfigurationId}`;
  state.selectedPath = `weapon-library/singleWeaponConfigurations/${encodeURIComponent(singleWeaponConfigurationId)}`;
  state.weaponTransformKey = `${state.selectedPath}#modelPose`;
  state.message = `正在预览单一武器：${definition.displayName || singleWeaponConfigurationId}`;
  render();
}

function enterWeaponViewport(attachmentId) {
  const weapon = findWeaponAttachmentById(attachmentId);
  if (!weapon?.weaponId) {
    state.message = "没有可查看的武器配置。";
    renderShellStatus();
    return;
  }
  teardownViewportForModeChange();
  if (state.viewportMode !== "weapon") {
    state.previousCharacterSelection = state.selectedPath || "manifest";
  }
  state.viewportMode = "weapon";
  state.focusedWeaponAttachmentId = getWeaponAttachmentIdentity(weapon);
  state.selectedPath = getWeaponAttachmentPath(weapon);
  state.weaponTransformKey = `${state.selectedPath}#localGripPose`;
  state.message = `正在查看武器：${getEquipSlotLabel(weapon.equipSlot)} / ${weapon.weaponId}`;
  render();
}

function returnToCharacterViewport() {
  teardownViewportForModeChange();
  state.viewportMode = "character";
  state.focusedWeaponAttachmentId = "";
  state.weaponTransformKey = "";
  const previous = state.previousCharacterSelection || state.selectedPath || "manifest";
  state.selectedPath = findTarget(previous).value ? previous : "manifest";
  state.previousCharacterSelection = "";
  state.message = "已返回角色视图。";
  render();
}

function teardownViewportForModeChange() {
  if (!viewportCleanup) return;
  viewportCleanup();
  viewportCleanup = null;
  viewportSelectionSync = null;
}

function renderViewport() {
  viewportSyncBodyMeasurements = null;
  viewportSyncMovementCollision = null;
  globalThis.setHumanoidHurtboxReference?.(null);
  renderPreviewControls();
  el.hurtboxCoverageButton?.classList.toggle("active", Boolean(state.hurtboxCoverageVisible));
  el.hurtboxCoverageButton?.setAttribute("aria-pressed", String(Boolean(state.hurtboxCoverageVisible)));
  if (!state.package) {
    viewportSelectionSync = null;
    el.viewport.innerHTML = `<div class="empty">没有加载资源包。</div>`;
    return;
  }

  if (viewportCleanup) {
    viewportCleanup();
    viewportCleanup = null;
  }

  viewportSelectionSync = null;
  viewportLivePoseBindings = [];
  viewportLiveContactPointBindings = [];
  viewportRefreshOverlay = null;
  viewportLiveContent = null;
  viewportLiveThree = null;
  viewportSyncHurtboxShapeHandles = null;
  viewportSyncHurtboxBindingReference = null;
  globalThis.viewportLiveBodyPreviewRoot = null;

  const renderId = ++viewportRenderId;
  el.viewport.innerHTML = `<div class="viewport3d" aria-label="3D 预览"><div class="viewport-status">正在加载 3D 预览...</div></div>`;
  renderThreeViewport(renderId).catch(error => {
    if (renderId !== viewportRenderId) return;
    renderSvgViewport(`3D 预览失败：${error instanceof Error ? error.message : String(error)}`);
  });
}

async function renderThreeViewport(renderId) {
  const runtime = await loadThreeRuntime();
  if (renderId !== viewportRenderId) return;

  const { THREE, GLTFLoader, FBXLoader, OrbitControls, TransformControls } = runtime;
  const host = el.viewport.querySelector(".viewport3d");
  if (!host) return;

  host.innerHTML = "";

  const scene = new THREE.Scene();
  const viewportBackground = 0x142033;
  scene.background = new THREE.Color(viewportBackground);

  const initialWidth = Math.max(1, host.clientWidth || 1);
  const initialHeight = Math.max(1, host.clientHeight || 1);
  const camera = new THREE.PerspectiveCamera(42, initialWidth / initialHeight, 0.01, 100);
  camera.position.set(2.35, 1.55, 3.15);

  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, preserveDrawingBuffer: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.setClearColor(viewportBackground, 1);
  if (THREE.SRGBColorSpace) renderer.outputColorSpace = THREE.SRGBColorSpace;
  if (THREE.ACESFilmicToneMapping !== undefined) renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.3;
  host.append(renderer.domElement);

  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.target.set(0, 0.95, 0);
  controls.minDistance = 0.75;
  controls.maxDistance = 8;

  scene.add(new THREE.HemisphereLight(0xd8e7f5, 0x182235, 1.35));
  scene.add(new THREE.AmbientLight(0xffffff, 0.42));
  const key = new THREE.DirectionalLight(0xf4f8ff, 1.85);
  key.position.set(2.5, 3.5, 2.5);
  scene.add(key);
  const fill = new THREE.DirectionalLight(0x8fc9ee, 0.85);
  fill.position.set(-2.1, 1.8, -1.4);
  scene.add(fill);
  const rim = new THREE.DirectionalLight(0xb7d9ff, 0.72);
  rim.position.set(0.4, 2.6, -3.2);
  scene.add(rim);
  scene.add(new THREE.GridHelper(3.2, 16, 0x45617d, 0x2a4058));

  const content = new THREE.Group();
  const pickables = [];
  const selectionVisuals = new Map();
  const transformTargets = new Map();
  viewportLiveContent = content;
  viewportLiveThree = THREE;
  viewportTransformTargets = transformTargets;
  scene.add(content);

  const registerSelectionVisual = (objectPath, applySelection) => {
    if (!objectPath || typeof applySelection !== "function") return;
    const visualList = selectionVisuals.get(objectPath);
    if (visualList) {
      visualList.push(applySelection);
    } else {
      selectionVisuals.set(objectPath, [applySelection]);
    }
  };

  const syncSelectionVisuals = selectedPath => {
    for (const [objectPath, visualList] of selectionVisuals.entries()) {
      const selected = globalThis.isBodyWorkspaceSelectionVisualSelected?.(objectPath, selectedPath) ?? objectPath === selectedPath;
      for (const applySelection of visualList) {
        applySelection(selected);
      }
    }
  };
  const registerTransformTarget = entry => registerViewportTransformTarget(THREE, transformTargets, entry);

  const previewLoaders = createPreviewModelLoaders(GLTFLoader, FBXLoader);
  const previewPackage = state.package;
  const previewPackageRelative = state.packageRelative;
  const packageUrl = (relative, version = "") => {
    const baseUrl = encodeURI(`/${previewPackageRelative}/${relative}`);
    return version ? `${baseUrl}?v=${encodeURIComponent(String(version))}` : baseUrl;
  };
  const previewModelUrl = (resource, relative, version = "") =>
    buildPreviewModelUrl(resource, relative, version, packageUrl);
  const resources = state.package.resourceCatalog?.entries || [];
  const packagePreviewMaterialBindings = await loadPackagePreviewMaterialBindings(THREE, resources, packageUrl);
  const bakedSurfaceRequested = state.viewportMode !== 'weapon'
    && (Boolean(previewPackage.bakedSurfaceBinding) || (previewPackage.bakedSurfaceIssues || []).length > 0);
  let packageBakedSurface = null;
  try {
    if (bakedSurfaceRequested) {
      if (previewPackage.bakedSurfaceIssues?.length) throw new Error(previewPackage.bakedSurfaceIssues.map(issue => issue.message || issue.Message).join('；'));
      normalizeBakedSurfacePreviewBinding(previewPackage.bakedSurfaceBinding);
      const catalog = await readJson(SURFACE_PALETTE_CATALOG_API, null);
      packageBakedSurface = await loadBakedSurfacePreview(THREE, previewPackage.bakedSurfaceBinding,
        (path, hash, isColor) => loadPackagePreviewTexture(THREE, new THREE.TextureLoader(), packageUrl, path, hash, { x: 1, y: 1 }, { x: 0, y: 0 }, isColor), catalog);
    }
  } catch (error) {
    disposePreviewObject(content);
    renderer.dispose();
    throw error;
  }
  // A baked body's UV1 stores signed hair flow, never palette addresses. Weapons choose their own palette below.
  const packageSurfacePalette = bakedSurfaceRequested ? null
    : await loadSurfacePalettePreview(THREE, previewPackage.paletteBinding, resources, packageUrl);
  if (renderId !== viewportRenderId) { packageBakedSurface?.dispose(); disposePreviewObject(content); renderer.dispose(); return; }
  host.dataset.bakedSurfacePreview = bakedSurfaceRequested ? 'ready' : 'disabled';
  host.dataset.palettePreview = !bakedSurfaceRequested && previewPackage.paletteBinding?.enabled
    ? packageSurfacePalette ? "ready" : "unavailable"
    : "disabled";
  const geometry = state.package.geometry || {};
  const visibleColliders = getVisibleHurtboxColliders(geometry.colliders || [], geometry);
  const socketsById = Object.fromEntries((geometry.sockets || []).map(socket => [socket.socketId, socket]));
  const activeSlotList = getDefaultWeaponSlotList();
  const activeAttachments = getActiveWeaponAttachments();
  const activeAttachmentIds = new Set(activeAttachments.map(item => item?.attachmentId));
  const activeTraceIds = new Set(activeAttachments.map(item => item?.traceId).filter(Boolean));
  const weaponVisualizationItems = buildWeaponVisualizationItems(state.package, activeSlotList, getResourceUnitySync);

  let loadedBody = false;
  let bodyPreviewRoot = null;
  let bodyPreviewAnimationMixer = null;
  let cameraMeasurementCallback = null;
  let viewportPoseAdapter = UNITY_GLTF_PREVIEW_POSE_ADAPTER;
  let bodyBoneRecords = [];
  let humanoidHurtboxReference = null;
  if (state.viewportMode === "weapon") {
    await addFocusedWeaponViewportContent({
      THREE,
      previewLoaders,
      host,
      content,
      pickables,
      resources,
      packageUrl,
      previewModelUrl,
      materialBindings: packagePreviewMaterialBindings,
      surfacePalette: packageSurfacePalette,
      registerSelectionVisual,
      registerTransformTarget
    });
  } else {
    const bodyRootKey = geometry.bodyProfile?.modelRootStableId || "";
    const bodyResource = resources.find(resource => isBodyModelBinding(resource, state.package))
      || resources.find(resource => bodyRootKey && (resource.resourceKey === bodyRootKey || resource.stableId === bodyRootKey));
    if (packageBakedSurface && (!bodyResource
        || normalizePreviewPath(bodyResource.relativePath) !== normalizePreviewPath(packageBakedSurface.binding.modelFile))) {
      packageBakedSurface.dispose();
      renderer.dispose();
      throw new Error('烘焙描述与当前本体模型不匹配，请重新发布角色。');
    }
    const bodyPreviewPaths = packageBakedSurface ? [packageBakedSurface.binding.modelFile]
      : resolvePreviewModelRelativePaths(bodyResource, resources);
    for (const bodyPreviewPath of bodyPreviewPaths) {
      loadedBody = await addPreviewModelResource({
        THREE,
        previewLoaders,
        content,
        pickables,
        url: previewModelUrl(bodyResource, bodyPreviewPath, packageBakedSurface?.binding.modelHash || getPreviewModelResourceHash(bodyResource)),
        usageSource: buildSurfacePaletteUsageSource(bodyResource, packageUrl),
        objectPath: `resources/${bodyResource.resourceKey}`,
        name: bodyResource.localId || bodyResource.resourceKey,
        wrapperPose: bodyResource.importHints?.modelWrapperPose,
        wrapperPoseAdapter: UNITY_GLTF_PREVIEW_POSE_ADAPTER,
        fitHeightMeters: geometry.bodyProfile?.heightMeters || geometry.bodyProfile?.defaultCapsule?.height || 0,
        bakedSurface: packageBakedSurface,
        surfacePalette: packageSurfacePalette,
        materialBindings: packagePreviewMaterialBindings,
        rendererMaterialBindings: getRendererMaterialBindings(bodyResource),
        hiddenNodePaths: getPreviewHiddenNodePaths(bodyResource, activeAttachments.length > 0),
        poseNodePaths: getSocketPoseNodePaths(geometry.sockets),
        boneSink: records => { bodyBoneRecords = records; },
        formatSink: format => { viewportPoseAdapter = getDefaultPoseAdapterForPreviewFormat(format); },
        transformTargetSink: ({ root }) => { bodyPreviewRoot = root; }
      }).catch(error => {
        disposePreviewObject(content);
        renderer.dispose();
        throw error;
      });
      if (loadedBody) break;
    }
    if (!loadedBody) addFallbackBody(THREE, content, pickables, geometry.bodyProfile, registerSelectionVisual);
    if (loadedBody && renderId === viewportRenderId) {
      humanoidHurtboxReference = {
        packageRelative: state.packageRelative,
        key: JSON.stringify([state.packageRelative, bodyResource?.resourceKey, getPreviewModelResourceHash(bodyResource),
          bodyResource?.importHints?.modelWrapperPose, geometry.bodyProfile?.heightMeters, viewportPoseAdapter]),
        bones: captureHumanoidHurtboxReference(THREE, content, bodyBoneRecords, viewportPoseAdapter)
      };
    }
    applyPreviewPose(THREE, bodyBoneRecords, state.previewPose);
    bodyPreviewAnimationMixer = await loadPackagePreviewAnimationMixer({
      THREE,
      previewLoaders,
      packageUrl,
      root: bodyPreviewRoot,
      pkg: state.package,
      motionId: state.previewMotion
    });
    globalThis.viewportLiveBodyPreviewRoot = bodyPreviewRoot;
    cameraMeasurementCallback = async () => {
      if (!bodyPreviewRoot) return null;
      const activeMixer = bodyPreviewAnimationMixer;
      const activeTime = Number(activeMixer?.time || 0);
      const activeTimeScale = Number(activeMixer?.timeScale ?? 1);
      if (activeMixer) activeMixer.timeScale = 0;
      const idleMixer = state.previewMotion === "source_idle"
        ? activeMixer
        : await loadPackagePreviewAnimationMixer({
          THREE,
          previewLoaders,
          packageUrl,
          root: bodyPreviewRoot,
          pkg: state.package,
          motionId: "source_idle"
        });
      try {
        idleMixer?.setTime(0);
        bodyPreviewRoot.updateMatrixWorld(true);
        return measureGameplayCameraBounds(THREE, bodyPreviewRoot);
      } finally {
        if (idleMixer && idleMixer !== activeMixer) {
          idleMixer.stopAllAction();
          idleMixer.uncacheRoot(bodyPreviewRoot);
        }
        if (activeMixer) {
          activeMixer.setTime(activeTime);
          activeMixer.timeScale = activeTimeScale;
        }
        bodyPreviewRoot.updateMatrixWorld(true);
      }
    };
    globalThis.viewportMeasureGameplayCameraIdleFrame = cameraMeasurementCallback;
    if (bodyPreviewRoot?.userData?.packagePreviewMaterialAudit) {
      host.dataset.packagePreviewMaterialAudit = JSON.stringify(bodyPreviewRoot.userData.packagePreviewMaterialAudit);
    }
    content.updateMatrixWorld(true);

    if (isCharacterPreviewLayerEnabled("colliders")) {
      addColliderMeshes(THREE, content, pickables, geometry.colliders || [], bodyBoneRecords, registerSelectionVisual, registerTransformTarget, viewportPoseAdapter);
      syncHurtboxMeshVisibility(content, geometry);
    }
    if (isCharacterPreviewLayerEnabled("sockets")) addSocketMeshes(THREE, content, pickables, geometry.sockets || [], bodyBoneRecords, registerSelectionVisual, registerTransformTarget, viewportPoseAdapter);
    if (isCharacterPreviewLayerEnabled("engagementAnchors")) addEngagementAnchorMeshes(THREE, content, pickables, getEngagementAnchorItems(geometry), registerSelectionVisual, registerTransformTarget, viewportPoseAdapter);
    if (isCharacterPreviewLayerEnabled("contactPoints")) addContactPointMeshes(THREE, content, pickables, geometry.bodyPartContactPoints || [], geometry.sockets || [], bodyBoneRecords, socketsById, registerSelectionVisual, registerTransformTarget, viewportPoseAdapter);
    // Weapon-local traces are rendered with their weapon root inside addWeaponMeshes so the
    // exact bone -> character slot -> weapon model transform order is preserved.
    if (isCharacterPreviewLayerEnabled("weapons")) {
      const weaponSurfacePalette = packageSurfacePalette || (packageBakedSurface && activeAttachments.length
        ? await loadDefaultSurfacePalettePreview(THREE, resources, packageUrl) : null);
      await addWeaponMeshes({
        THREE,
        previewLoaders,
        content,
        pickables,
        resources,
        packageUrl,
        previewModelUrl,
        socketsById,
        bodyBoneRecords,
        attachments: activeAttachments,
        materialBindings: packagePreviewMaterialBindings,
        surfacePalette: weaponSurfacePalette,
        registerSelectionVisual,
        poseAdapter: viewportPoseAdapter
      });
    }
    host.insertAdjacentHTML("beforeend", renderWeaponDataOverlayHtml(weaponVisualizationItems, activeSlotList));
    wireWeaponDataOverlay(host);
  }
  viewportRefreshOverlay = () => refreshViewportOverlay(host);
  if (renderId === viewportRenderId && state.selectedPath === "geometry/body") {
    viewportSyncBodyMeasurements = attachBodyMeasurementPreview(THREE, content, host, geometry.bodyProfile);
  }
  if (renderId === viewportRenderId && state.selectedPath === "geometry/body/capsule") {
    viewportSyncMovementCollision = attachMovementCollisionPreview(THREE, content, pickables, host, geometry.bodyProfile, viewportPoseAdapter);
  }

  content.updateMatrixWorld(true);
  syncPreviewBones(THREE, bodyBoneRecords);
  if (state.viewportMode === "character"
      && isCharacterPreviewLayerEnabled("colliders")
      && state.hurtboxCoverageVisible
      && isHurtboxCoverageScopeAvailable()
      && bodyPreviewRoot
      && visibleColliders.length > 0) {
    addHurtboxCoverageHeatmap(THREE, host, bodyPreviewRoot, content, renderer, scene);
  }
  if (state.viewportMode === "character" && state.bonePickerOpen && getActiveBoneFieldPath(findTarget(state.selectedPath))) {
    addBoneGizmos(THREE, scene, pickables, bodyBoneRecords);
  }
  const framingContent = state.viewportMode === "character" && bodyPreviewRoot
    ? bodyPreviewRoot
    : content;
  frameContent(THREE, camera, controls, framingContent, geometry.bodyProfile);
  restoreViewportCamera(THREE, camera, controls);
  if (state.selectedPath === "config/gameplay-camera" && state.package?.applicationConfig?.gameplayCameraProfile) {
    addGameplayCameraPreviewOverlay(
      THREE,
      content,
      state.package.applicationConfig.gameplayCameraProfile,
      state.gameplayCameraPreviewMode || "normal",
      initialWidth / initialHeight,
      bodyBoneRecords,
      viewportPoseAdapter);
  }
  let transformControls = null;
  if (TransformControls) {
    transformControls = new TransformControls(camera, renderer.domElement);
    transformControls.setMode(state.weaponTransformMode || "translate");
    transformControls.setSpace("local");
    transformControls.size = 0.72;
    transformControls.addEventListener("dragging-changed", async event => {
      controls.enabled = !event.value;
      if (event.value) {
        const target = getActiveViewportTransformTarget();
        if (target) beginViewportTransformEdit(target);
      } else {
        const target = getActiveViewportTransformTarget();
        if (target) {
          const result = commitWeaponTransformTarget(THREE, target);
          commitUndoTransaction();
          renderInspector();
          if (state.activeContextTab === "singleWeapons") renderSingleWeaponWorkbench();
          endViewportTransformEdit(target);
          if (result?.rebuildViewport) renderViewport();
        }
      }
    });
    transformControls.addEventListener("objectChange", () => {
      const target = getActiveViewportTransformTarget();
      if (target) commitWeaponTransformTarget(THREE, target, { live: true });
    });
    scene.add(transformControls);
    viewportTransformControls = transformControls;
  }
  const hurtboxShapeHandles = createHurtboxShapeHandleController({
    THREE,
    scene,
    camera,
    renderer,
    controls,
    host,
    transformControls
  });
  const hurtboxBindingReference = createHurtboxBindingReferenceController({
    THREE,
    scene,
    camera,
    renderer,
    host
  });
  viewportSyncHurtboxShapeHandles = () => hurtboxShapeHandles.sync();
  viewportSyncHurtboxBindingReference = () => hurtboxBindingReference.sync();
  syncSelectionVisuals(state.selectedPath || "");
  viewportSyncTransformControl = selectedPath => syncWeaponTransformControl(THREE, selectedPath || "");
  syncWeaponTransformControl(THREE, state.selectedPath || "");
  hurtboxBindingReference.sync();
  focusSelectedViewportTargetCallback = () => {
    const target = getActiveViewportTransformTarget();
    if (!target?.object) return false;
    target.object.updateWorldMatrix?.(true, true);
    const box = new THREE.Box3().setFromObject(target.object, true);
    if (box.isEmpty()) return false;

    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());
    const verticalFov = Math.max(0.01, Number(camera.fov || 42) * Math.PI / 180);
    const horizontalFov = 2 * Math.atan(Math.tan(verticalFov / 2) * Math.max(0.1, Number(camera.aspect || 1)));
    const limitingFov = Math.max(0.01, Math.min(verticalFov, horizontalFov));
    const maxSpan = Math.max(Number(size.x || 0), Number(size.y || 0), Number(size.z || 0), 0.1);
    const distance = Math.max(0.35, maxSpan / (2 * Math.tan(limitingFov / 2)) * 1.65);
    const viewDirection = camera.position.clone().sub(controls.target);
    if (viewDirection.lengthSq() < 0.000001) viewDirection.set(0.68, 0.24, 1);
    viewDirection.normalize();

    controls.target.copy(center);
    camera.position.copy(center).addScaledVector(viewDirection, distance);
    camera.near = Math.max(0.005, distance / 500);
    camera.far = Math.max(100, distance + maxSpan * 20);
    camera.updateProjectionMatrix();
    controls.update();
    rememberViewportCamera(camera, controls);
    return true;
  };
  viewportSelectionSync = selectedPath => {
    syncHurtboxMeshVisibility(content, geometry);
    renderPreviewControls();
    syncSelectionVisuals(selectedPath || "");
    syncWeaponTransformControl(THREE, selectedPath || "");
    hurtboxShapeHandles.sync();
    hurtboxBindingReference.sync();
  };

  const raycaster = new THREE.Raycaster();
  raycaster.params.Line = { threshold: 0.08 };
  const pointer = new THREE.Vector2();
  let lastColliderPick = { x: Number.NaN, y: Number.NaN, signature: "", index: -1, time: 0 };
  const onPointerDown = event => {
    if (state.selectedPath === "config/attributes") return;
    if (hurtboxShapeHandles.pointerDown(event)) return;
    if (isViewportTransformHandleActive(transformControls)) return;
    const rect = renderer.domElement.getBoundingClientRect();
    pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    pointer.y = -(((event.clientY - rect.top) / rect.height) * 2 - 1);
    raycaster.setFromCamera(pointer, camera);
    const hits = raycaster.intersectObjects(pickables, true).filter(hit => isViewportObjectVisible(hit.object));
    const boneHit = state.bonePickerOpen ? hits.find(item => findBonePick(item.object)) : null;
    if (boneHit) {
      const bonePick = findBonePick(boneHit.object);
      applyBonePick(bonePick);
      return;
    }
    if (state.bonePickerOpen) return;
    const selectableHits = hits.filter(item => findObjectPath(item.object));
    const colliderHits = [];
    const colliderPaths = new Set();
    for (const item of selectableHits) {
      const path = String(findObjectPath(item.object) || "");
      if (!path.startsWith("geometry/colliders/") || colliderPaths.has(path)) continue;
      colliderPaths.add(path);
      colliderHits.push(item);
    }
    let hit = colliderHits[0] || selectableHits[0];
    if (colliderHits.length > 1) {
      const signature = colliderHits.map(item => findObjectPath(item.object)).join("|");
      const now = performance.now();
      const repeated = now - lastColliderPick.time < 900
        && Math.hypot(event.clientX - lastColliderPick.x, event.clientY - lastColliderPick.y) <= 6
        && signature === lastColliderPick.signature;
      const index = repeated ? (lastColliderPick.index + 1) % colliderHits.length : 0;
      hit = colliderHits[index];
      lastColliderPick = { x: event.clientX, y: event.clientY, signature, index, time: now };
    } else {
      lastColliderPick = { x: event.clientX, y: event.clientY, signature: "", index: -1, time: performance.now() };
    }
    const pickedPath = hit ? findObjectPath(hit.object) : "";
    const objectPath = globalThis.getBodyWorkspacePickPath?.(pickedPath) ?? pickedPath;
    const transformKey = hit ? findWeaponTransformKey(hit.object) : "";
    if (transformKey) state.weaponTransformKey = transformKey;
    if (objectPath) selectPath(objectPath, { preserveViewport: true });
  };
  renderer.domElement.addEventListener("pointerdown", onPointerDown);
  const onPointerMove = event => hurtboxShapeHandles.pointerMove(event);
  const onPointerUp = event => hurtboxShapeHandles.pointerUp(event);
  const onPointerCancel = event => hurtboxShapeHandles.pointerCancel(event);
  const onKeyDown = event => {
    if (String(event.key || "") !== "Escape") return;
    if (hurtboxShapeHandles.cancelDrag("escape")) {
      event.preventDefault();
      event.stopPropagation();
    }
  };
  renderer.domElement.addEventListener("pointermove", onPointerMove);
  renderer.domElement.addEventListener("pointerup", onPointerUp);
  renderer.domElement.addEventListener("pointercancel", onPointerCancel);
  document.addEventListener("keydown", onKeyDown, true);

  const resize = () => {
    const width = Math.max(1, host.clientWidth);
    const height = Math.max(1, host.clientHeight);
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
  };
  const resizeObserver = new ResizeObserver(resize);
  resizeObserver.observe(host);
  resize();

  let frame = 0;
  const clock = new THREE.Clock();
  const animate = () => {
    const delta = clock.getDelta();
    const elapsed = clock.elapsedTime;
    if (bodyPreviewAnimationMixer) {
      bodyPreviewAnimationMixer.update(delta);
    } else {
      const fallbackMotion = state.previewMotion === "source_idle" ? "breath" : state.previewMotion;
      applyPreviewMotion(THREE, bodyBoneRecords, fallbackMotion, elapsed);
    }
    updateBoneParentGroups(THREE, content);
    updatePoseLines(THREE, content);
    hurtboxShapeHandles.updateVisuals();
    hurtboxBindingReference.updateVisuals();
    controls.update();
    renderer.render(scene, camera);
    frame = requestAnimationFrame(animate);
  };
  animate();

  viewportCleanup = () => {
    viewportSelectionSync = null;
    viewportSyncBodyMeasurements = null;
    viewportSyncMovementCollision = null;
    if (state.skipNextCameraRemember) {
      state.skipNextCameraRemember = false;
    } else {
      rememberViewportCamera(camera, controls);
    }
    cancelAnimationFrame(frame);
    resizeObserver.disconnect();
    renderer.domElement.removeEventListener("pointerdown", onPointerDown);
    renderer.domElement.removeEventListener("pointermove", onPointerMove);
    renderer.domElement.removeEventListener("pointerup", onPointerUp);
    renderer.domElement.removeEventListener("pointercancel", onPointerCancel);
    document.removeEventListener("keydown", onKeyDown, true);
    hurtboxShapeHandles.dispose();
    hurtboxBindingReference.dispose();
    transformControls?.detach();
    transformControls?.dispose();
    disposePreviewObject(content);
    renderer.dispose();
    viewportLivePoseBindings = [];
    viewportLiveContactPointBindings = [];
    viewportRefreshOverlay = null;
    viewportLiveContent = null;
    viewportLiveThree = null;
    focusSelectedViewportTargetCallback = null;
    viewportSyncHurtboxShapeHandles = null;
    viewportSyncHurtboxBindingReference = null;
    globalThis.viewportLiveBodyPreviewRoot = null;
    if (globalThis.viewportMeasureGameplayCameraIdleFrame === cameraMeasurementCallback)
      globalThis.viewportMeasureGameplayCameraIdleFrame = null;
    viewportTransformControls = null;
    viewportTransformTargets = new Map();
    viewportSyncTransformControl = null;
  };
  if (renderId === viewportRenderId) globalThis.setHumanoidHurtboxReference?.(humanoidHurtboxReference);
}

function renderSvgViewport(message = "") {
  viewportSyncBodyMeasurements = null;
  viewportSyncMovementCollision = null;
  viewportSelectionSync = null;
  const g = state.package.geometry || {};
  if (state.viewportMode === "weapon") {
    renderSvgWeaponViewport(message);
    return;
  }
  const selected = state.selectedPath;
  const activeSlotList = getDefaultWeaponSlotList();
  const attachments = getActiveWeaponAttachments();
  const activeTraceIds = new Set(attachments.map(item => item?.traceId).filter(Boolean));
  const traces = (g.traces || []).filter(t => activeTraceIds.has(t.traceId));
  const socketsById = Object.fromEntries((g.sockets || []).map(s => [s.socketId, s]));
  const weaponVisualizationItems = buildWeaponVisualizationItems(state.package, activeSlotList, getResourceUnitySync);
  const body = g.bodyProfile || {};
  const height = Number(body.heightMeters || body.defaultCapsule?.height || 1.8);
  const radius = Number(body.radiusMeters || body.defaultCapsule?.radius || 0.35);

  const shapes = [];
  shapes.push(`<rect x="46" y="${scaleY(height)}" width="8" height="${Math.max(8, height * 38)}" rx="4" fill="#dce8ea" stroke="#91a4ad" />`);
  shapes.push(`<ellipse cx="50" cy="${scaleY(height)}" rx="${Math.max(3, radius * 12)}" ry="4" fill="#e8f0f2" stroke="#91a4ad" />`);
  shapes.push(globalThis.renderMovementCollisionSvg?.() || "");

  if (isCharacterPreviewLayerEnabled("colliders")) {
    for (const c of getVisibleHurtboxColliders(g.colliders || [], g)) {
      const p = c.localPose?.position || {};
      const x = scaleX(p.x);
      const y = scaleY(p.y);
      const path = `geometry/colliders/${c.colliderId}`;
      const cls = (globalThis.isBodyWorkspaceSelectionVisualSelected?.(path, selected) ?? selected === path) ? "selected" : "";
      if (c.shape === "Box") {
        shapes.push(`<rect class="${cls}" data-object-path="${escapeHtml(path)}" x="${x - 4}" y="${y - 5}" width="8" height="10" fill="rgba(31,122,122,0.18)" stroke="#1f7a7a" />`);
      } else if (c.shape === "Capsule") {
        shapes.push(`<rect class="${cls}" data-object-path="${escapeHtml(path)}" x="${x - 5}" y="${y - 14}" width="10" height="28" rx="5" fill="rgba(31,122,122,0.16)" stroke="#1f7a7a" />`);
      } else {
        shapes.push(`<circle class="${cls}" data-object-path="${escapeHtml(path)}" cx="${x}" cy="${y}" r="${Math.max(3, Number(c.radius || 0.12) * 18)}" fill="rgba(31,122,122,0.16)" stroke="#1f7a7a" />`);
      }
    }
  }

  if (isCharacterPreviewLayerEnabled("sockets")) {
    for (const s of g.sockets || []) {
      const p = s.localPose?.position || {};
      const x = scaleX(p.x);
      const y = scaleY(p.y);
      const path = `geometry/sockets/${s.socketId}`;
      const cls = selected === path ? "selected" : "";
      shapes.push(`<g class="${cls}" data-object-path="${escapeHtml(path)}"><line x1="${x - 4}" y1="${y}" x2="${x + 4}" y2="${y}" stroke="#b46a1f"/><line x1="${x}" y1="${y - 4}" x2="${x}" y2="${y + 4}" stroke="#b46a1f"/><circle cx="${x}" cy="${y}" r="2.4" fill="#fff" stroke="#b46a1f"/></g>`);
    }
  }

  if (state.layers.weapons) {
    for (const a of attachments) {
      const socket = socketsById[a.attachSocketId];
      const p = socket?.localPose?.position || {};
      const x = scaleX(p.x) + (a.equipSlot === "offHand" ? -8 : 8);
      const y = scaleY(p.y) - 4;
      const path = getWeaponAttachmentPath(a);
      const cls = selected === path ? "selected" : "";
      shapes.push(`<g class="${cls}" data-object-path="${escapeHtml(path)}"><rect x="${x - 3}" y="${y - 13}" width="6" height="22" rx="2" fill="rgba(180,106,31,0.18)" stroke="#b46a1f"/><text x="${x + 5}" y="${y - 9}" font-size="3" fill="#734515">${escapeHtml(a.equipSlot || "")}</text></g>`);
    }
  }

  if (isCharacterPreviewLayerEnabled("traces")) {
    for (const t of traces) {
      const start = t.startPose?.position || {};
      const end = t.endPose?.position || {};
      const sx = scaleX(start.x) + 8;
      const sy = scaleY(start.y);
      const ex = scaleX(end.x) + 8;
      const ey = scaleY(end.y);
      const path = `geometry/traces/${t.traceId}`;
      const cls = selected === path ? "selected" : "";
      shapes.push(`<g class="${cls}" data-object-path="${escapeHtml(path)}"><line x1="${sx}" y1="${sy}" x2="${ex}" y2="${ey}" stroke="#c24141" stroke-width="${Math.max(0.8, Number(t.radius || 0.04) * 18)}" opacity="0.45"/><circle cx="${sx}" cy="${sy}" r="2" fill="#c24141"/><circle cx="${ex}" cy="${ey}" r="2" fill="#c24141"/></g>`);
    }
  }

  el.viewport.innerHTML = `<div class="viewport-fallback">${message ? `<div class="viewport-status">${escapeHtml(message)}</div>` : ""}<svg viewBox="0 0 100 100" role="img" aria-label="Character package viewport"><defs><pattern id="grid" width="10" height="10" patternUnits="userSpaceOnUse"><path d="M 10 0 L 0 0 0 10" fill="none" stroke="#edf1f3" stroke-width="0.6"/></pattern></defs><rect width="100" height="100" fill="url(#grid)"/><text x="4" y="7" font-size="3.5" fill="#61717f">${escapeHtml(state.package.manifest?.packageId || "character")}</text>${shapes.join("")}${globalThis.renderBodyMeasurementSvg?.() || ""}</svg>${renderWeaponDataOverlayHtml(weaponVisualizationItems, activeSlotList)}${globalThis.renderBodyMeasurementLegend?.() || ""}</div>`;
  if (state.selectedPath === "geometry/body") {
    viewportSyncBodyMeasurements = () => { renderSvgViewport(message); return true; };
  }
  if (state.selectedPath === "geometry/body/capsule") {
    el.viewport.querySelector(".viewport-fallback")?.insertAdjacentHTML("beforeend", renderMovementCollisionLegend());
    viewportSyncMovementCollision = () => { renderSvgViewport(message); return true; };
  }
  el.viewport.querySelectorAll("[data-object-path]").forEach(item => {
    item.addEventListener("click", event => {
      event.stopPropagation();
      const pickedPath = item.getAttribute("data-object-path");
      const path = globalThis.getBodyWorkspacePickPath?.(pickedPath) ?? pickedPath;
      if (path) selectPath(path);
    });
  });
  wireWeaponDataOverlay(el.viewport);
}

function renderSvgWeaponViewport(message = "") {
  const weapon = getFocusedWeaponAttachment();
  const item = getWeaponVisualizationItemForWeapon(weapon);
  const trace = item?.trace ? normalizeWeaponPreviewTrace(item.trace, weapon) : null;
  const traceStartPose = getPreviewPoseForAdapter(trace?.startPose, UNITY_GLTF_PREVIEW_POSE_ADAPTER);
  const traceEndPose = getPreviewPoseForAdapter(trace?.endPose, UNITY_GLTF_PREVIEW_POSE_ADAPTER);
  const traceShape = trace
    ? `<line x1="${scaleX(traceStartPose?.position?.x || 0)}" y1="${scaleY(traceStartPose?.position?.y || 0)}" x2="${scaleX(traceEndPose?.position?.x || 0)}" y2="${scaleY(traceEndPose?.position?.y || 0)}" stroke="#c24141" stroke-width="${Math.max(1, Number(trace.radius || 0.04) * 20)}" opacity="0.65"/><circle cx="${scaleX(traceStartPose?.position?.x || 0)}" cy="${scaleY(traceStartPose?.position?.y || 0)}" r="2" fill="#c24141"/><circle cx="${scaleX(traceEndPose?.position?.x || 0)}" cy="${scaleY(traceEndPose?.position?.y || 0)}" r="2" fill="#c24141"/>`
    : "";
  const weaponPath = getWeaponAttachmentPath(weapon);
  el.viewport.innerHTML = `<div class="viewport-fallback">${message ? `<div class="viewport-status">${escapeHtml(message)}</div>` : ""}<svg viewBox="0 0 100 100" role="img" aria-label="Weapon package viewport"><defs><pattern id="grid" width="10" height="10" patternUnits="userSpaceOnUse"><path d="M 10 0 L 0 0 0 10" fill="none" stroke="#edf1f3" stroke-width="0.6"/></pattern></defs><rect width="100" height="100" fill="url(#grid)"/><g ${weaponPath ? `data-object-path="${escapeHtml(weaponPath)}"` : ""}><rect x="47" y="48" width="6" height="28" rx="2" fill="rgba(180,106,31,0.32)" stroke="#b46a1f"/><rect x="46" y="23" width="8" height="32" rx="2" fill="rgba(224,150,75,0.24)" stroke="#e0964b"/><circle cx="50" cy="55" r="3" fill="#2fc2bd"/></g>${traceShape}<text x="4" y="7" font-size="3.5" fill="#61717f">${escapeHtml(weapon?.weaponId || "weapon")}</text></svg>${renderWeaponFocusOverlayHtml(item)}</div>`;
  el.viewport.querySelectorAll("[data-object-path]").forEach(node => {
    node.addEventListener("click", event => {
      event.stopPropagation();
      selectPath(node.getAttribute("data-object-path"), { preserveViewport: true });
    });
  });
  wireWeaponFocusOverlay(el.viewport);
}

async function loadThreeRuntime() {
  if (!threeRuntimePromise) {
    threeRuntimePromise = Promise.all([
      import("../../node_modules/three/build/three.module.js"),
      import("../../node_modules/three/examples/jsm/loaders/GLTFLoader.js"),
      import("../../node_modules/three/examples/jsm/loaders/FBXLoader.js?wgame-material-groups=1"),
      import("../../node_modules/three/examples/jsm/controls/OrbitControls.js"),
      import("../../node_modules/three/examples/jsm/controls/TransformControls.js")
    ]).then(([THREE, gltfModule, fbxModule, controlsModule, transformModule]) => ({
      THREE,
      GLTFLoader: gltfModule.GLTFLoader,
      FBXLoader: fbxModule.FBXLoader,
      OrbitControls: controlsModule.OrbitControls,
      TransformControls: transformModule.TransformControls
    }));
  }
  return threeRuntimePromise;
}

function createPreviewModelLoaders(GLTFLoader, FBXLoader) {
  return {
    gltf: new GLTFLoader(),
    fbx: FBXLoader ? new FBXLoader() : null
  };
}

function normalizePreviewPath(relativePath) {
  return String(relativePath || "").replace(/\\/g, "/").replace(/^\/+/, "");
}

function getPreviewModelFormat(relativePath) {
  const cleanPath = normalizePreviewPath(relativePath)
    .split(/[?#]/, 1)[0]
    .toLowerCase();
  if (cleanPath.endsWith(".glb") || cleanPath.endsWith(".gltf")) return "gltf";
  if (cleanPath.endsWith(".fbx")) return "fbx";
  return "";
}

function buildPreviewModelUrl(resource, relativePath, version = "", packageUrl) {
  const path = normalizePreviewPath(relativePath || resource?.relativePath || "");
  const hash = version || getPreviewModelResourceHash(resource);
  if (isRepoRelativePreviewPath(path)) {
    const baseUrl = encodeURI(`/${path}`);
    return hash ? `${baseUrl}?v=${encodeURIComponent(String(hash))}` : baseUrl;
  }
  return packageUrl(path, hash);
}

function getPreviewModelResourceHash(resource) {
  return resource?.hash || resource?.hashes?.contentHash || resource?.generatedHash || resource?.sourceHash || "";
}

function isRepoRelativePreviewPath(relativePath) {
  return /^(Assets|ArtSource)\//i.test(normalizePreviewPath(relativePath));
}

function isPreviewModelPath(relativePath) {
  return Boolean(getPreviewModelFormat(relativePath));
}

function getDefaultPoseAdapterForPreviewFormat(format) {
  format = String(format || "").toLowerCase();
  return format === "gltf" || format === "fbx" ? UNITY_GLTF_PREVIEW_POSE_ADAPTER : "";
}

function getPoseAdapterForPreviewFormat(format, requestedAdapter = "") {
  return usesUnityAuthoredPoseAdapterForPreviewFormat(format) ? requestedAdapter : "";
}

function usesUnityAuthoredPoseAdapterForPreviewFormat(format) {
  const normalized = String(format || "").toLowerCase();
  return normalized === "gltf" || normalized === "fbx";
}

function resolvePreviewModelRelativePaths(resource, resources) {
  if (!resource?.relativePath) return [];
  const directPath = String(resource.relativePath || "");
  const paths = [];
  const addPath = path => {
    const normalized = String(path || "");
    if (!normalized || !isPreviewModelPath(normalized)) return;
    if (paths.some(existing => existing.toLowerCase() === normalized.toLowerCase())) return;
    paths.push(normalized);
  };

  if (shouldPreferSourceFbxPreview(resource)) {
    for (const sourcePath of buildSourceFbxPreviewPaths(resource, directPath)) addPath(sourcePath);
    for (const siblingPath of buildSiblingPreviewModelPaths(directPath).filter(isFbxPreviewPath)) addPath(siblingPath);
  }
  addPath(directPath);
  for (const catalogPath of findCatalogPreviewModelPaths(directPath, resources)) addPath(catalogPath);
  for (const candidate of buildSiblingPreviewModelPaths(directPath)) addPath(candidate);

  return paths;
}

function shouldPreferSourceFbxPreview(resource) {
  const sourceFormat = String(resource?.sourceFormat || "").toLowerCase();
  const sourceFile = String(resource?.provenance?.sourceFile || "");
  const tags = Array.isArray(resource?.tags) ? resource.tags.map(tag => String(tag || "").toLowerCase()) : [];
  return sourceFormat === "fbx"
    || isFbxPreviewPath(sourceFile)
    || tags.includes("converted-from-fbx");
}

function buildSourceFbxPreviewPaths(resource, directPath) {
  const sourceFile = String(resource?.provenance?.sourceFile || "");
  if (!isFbxPreviewPath(sourceFile)) return [];
  const normalizedSource = sourceFile.replace(/\\/g, "/");
  if (normalizedSource.includes("/")) return [normalizedSource];
  const directory = String(directPath || "").replace(/\\/g, "/").replace(/\/?[^/]*$/, "");
  return directory ? [`${directory}/${normalizedSource}`] : [normalizedSource];
}

function findCatalogPreviewModelPaths(relativePath, resources) {
  const candidates = new Set(buildSiblingPreviewModelPaths(relativePath).map(path => path.toLowerCase()));
  const paths = [];
  for (const resource of resources || []) {
    const candidatePath = String(resource?.relativePath || "");
    if (candidatePath && candidates.has(candidatePath.toLowerCase()) && isPreviewModelPath(candidatePath)) {
      paths.push(candidatePath);
    }
  }

  return paths;
}

function buildSiblingPreviewModelPaths(relativePath) {
  const path = String(relativePath || "");
  const base = path.replace(/\.[^/.?#]+([?#].*)?$/i, "");
  if (!base || base === path) return [];
  return [`${base}.fbx`, `${base}.glb`, `${base}.gltf`];
}

function resolveGltfPreviewRelativePaths(resource, resources) {
  return resolvePreviewModelRelativePaths(resource, resources).filter(isGltfPreviewPath);
}

function findCatalogGltfPreviewPaths(relativePath, resources) {
  return findCatalogPreviewModelPaths(relativePath, resources).filter(isGltfPreviewPath);
}

function buildSiblingGltfPreviewPaths(relativePath) {
  return buildSiblingPreviewModelPaths(relativePath).filter(isGltfPreviewPath);
}

function isGltfPreviewPath(relativePath) {
  return /\.(glb|gltf)([?#].*)?$/i.test(String(relativePath || ""));
}

function isFbxPreviewPath(relativePath) {
  return /\.fbx([?#].*)?$/i.test(String(relativePath || ""));
}

async function loadPreviewModel(previewLoaders, url) {
  const format = getPreviewModelFormat(url);
  if (format === "gltf") {
    const gltf = await new Promise((resolve, reject) => previewLoaders.gltf.load(url, resolve, undefined, reject));
    const root = gltf.scene || gltf.scenes?.[0];
    if (!root) throw new Error("glTF preview asset does not contain a scene.");
    return {
      root,
      animations: gltf.animations || [],
      format,
      source: gltf
    };
  }
  if (format === "fbx") {
    if (!previewLoaders.fbx) throw new Error("FBXLoader is not available.");
    const root = await new Promise((resolve, reject) => previewLoaders.fbx.load(url, resolve, undefined, reject));
    return {
      root,
      animations: root.animations || [],
      format,
      source: root
    };
  }
  throw new Error(`Unsupported preview model format: ${url}`);
}

function normalizePreviewMaterialName(value) {
  return String(value || "")
    .replace(/^Material::/i, "")
    .replace(/\.material\.json$/i, "")
    .replace(/\.mat$/i, "")
    .replace(/[._\-\s]+\d+$/i, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "");
}

function getPreviewResourceFileName(value) {
  const parts = String(value || "").replace(/\\/g, "/").split("/");
  return parts[parts.length - 1] || "";
}

function normalizeSurfacePaletteBinding(binding) {
  if (!binding?.enabled) return null;
  const format = String(binding.format || "").trim();
  const schemaVersion = String(binding.schemaVersion || "").trim();
  if (format !== CHARACTER_PALETTE_BINDING_V5 || schemaVersion !== "5.0") return null;
  const atlasWidth = Number(binding.atlasWidth ?? 256);
  const atlasHeight = Number(binding.atlasHeight ?? 256);
  const colorAtlasResourceKey = String(binding.colorAtlasResourceKey || "").trim();
  const surfaceProfileSource = String(binding.surfaceProfileSource || "").trim();
  const faceDetailEnabled = Boolean(binding.faceDetailEnabled);
  const faceDetailTextureResourceKey = String(binding.faceDetailTextureResourceKey || "").trim();
  const faceDetailIntensity = Number(binding.faceDetailIntensity ?? 1);
  if (!Number.isInteger(atlasWidth)
      || !Number.isInteger(atlasHeight)
      || atlasWidth !== 256
      || atlasHeight !== 256
      || colorAtlasResourceKey !== "wgame.character.color_palette.shared"
      || String(binding.addressingMode || "").trim() !== "colorUvSurfaceProfileVertexAlpha"
      || surfaceProfileSource !== "paletteDataAlpha16"
      || Number(binding.surfaceProfileCount ?? 16) !== 16
      || (faceDetailEnabled && !faceDetailTextureResourceKey)
      || !Number.isFinite(faceDetailIntensity) || faceDetailIntensity < 0 || faceDetailIntensity > 1) {
    return null;
  }

  return {
    format: CHARACTER_PALETTE_BINDING_V5,
    atlasWidth,
    atlasHeight,
    colorAtlasResourceKey,
    surfaceProfileSource,
    cullMode: String(binding.cullMode || "Back").trim() || "Back",
    faceDetailEnabled,
    faceDetailTextureResourceKey,
    faceDetailIntensity,
    faceDetailTexture: null,
    catalogHash: "",
    texture: null
  };
}

function decodeSurfacePaletteBase64(value) {
  const binary = atob(String(value || ""));
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index++) bytes[index] = binary.charCodeAt(index);
  return bytes;
}

async function loadSurfacePalettePreview(THREE, binding, resources = [], packageUrl = null) {
  const surfacePalette = normalizeSurfacePaletteBinding(binding);
  if (!surfacePalette || !THREE?.DataTexture) return null;
  const catalog = await readJson(SURFACE_PALETTE_CATALOG_API, null);
  if (catalog?.format !== SURFACE_PALETTE_CATALOG_V3
      || catalog.width !== 256
      || catalog.height !== 256
      || catalog.colorCount !== 65536
      || catalog.surfaceProfileCount !== 16
      || catalog.colorData?.uniqueRgb8Count !== 65536
      || catalog.colorData?.aliasCount !== 0
      || !Array.isArray(catalog.surfaceProfiles)
      || catalog.surfaceProfiles.length !== 16) return null;
  let rgb8;
  try {
    rgb8 = decodeSurfacePaletteBase64(catalog.colorData?.rgb8Base64);
  } catch {
    return null;
  }
  if (rgb8.byteLength !== 256 * 256 * 3) return null;
  const pixels = new Uint8Array(256 * 256 * 4);
  for (let index = 0; index < 256 * 256; index++) {
    pixels[index * 4] = rgb8[index * 3];
    pixels[index * 4 + 1] = rgb8[index * 3 + 1];
    pixels[index * 4 + 2] = rgb8[index * 3 + 2];
    pixels[index * 4 + 3] = 255;
  }
  const texture = new THREE.DataTexture(pixels, 256, 256, THREE.RGBAFormat, THREE.UnsignedByteType);
  if (THREE.ClampToEdgeWrapping !== undefined) {
    texture.wrapS = THREE.ClampToEdgeWrapping;
    texture.wrapT = THREE.ClampToEdgeWrapping;
  }
  if (THREE.NearestFilter !== undefined) {
    texture.magFilter = THREE.NearestFilter;
    texture.minFilter = THREE.NearestFilter;
  }
  if (THREE.SRGBColorSpace !== undefined) texture.colorSpace = THREE.SRGBColorSpace;
  texture.generateMipmaps = false;
  texture.flipY = false;
  texture.needsUpdate = true;
  surfacePalette.texture = texture;
  surfacePalette.catalogHash = String(catalog.catalogHash || "");
  surfacePalette.catalog = catalog;
  if (surfacePalette.faceDetailEnabled) {
    const faceResource = (resources || []).find(item => item?.resourceKey === surfacePalette.faceDetailTextureResourceKey);
    if (!faceResource?.relativePath || !THREE?.TextureLoader || typeof packageUrl !== "function") return null;
    const faceTexture = await loadPackagePreviewTexture(
      THREE,
      new THREE.TextureLoader(),
      packageUrl,
      faceResource.relativePath,
      getPreviewModelResourceHash(faceResource),
      { x: 1, y: 1 },
      { x: 0, y: 0 },
      true);
    if (!faceTexture) return null;
    if (THREE.ClampToEdgeWrapping !== undefined) {
      faceTexture.wrapS = THREE.ClampToEdgeWrapping;
      faceTexture.wrapT = THREE.ClampToEdgeWrapping;
    }
    faceTexture.flipY = false;
    faceTexture.needsUpdate = true;
    surfacePalette.faceDetailTexture = faceTexture;
  }
  state.surfacePaletteCatalog = catalog;
  globalThis.renderSurfacePaletteWorkbench?.();
  return surfacePalette;
}

async function loadDefaultSurfacePalettePreview(THREE, resources = [], packageUrl = null) {
  const defaultBinding = {
    format: CHARACTER_PALETTE_BINDING_V5,
    atlasWidth: 256,
    atlasHeight: 256,
    colorAtlasResourceKey: "wgame.character.color_palette.shared",
    addressingMode: "colorUvSurfaceProfileVertexAlpha",
    surfaceProfileSource: "paletteDataAlpha16",
    surfaceProfileCount: 16,
    cullMode: "Back",
    faceDetailEnabled: false
  };
  return loadSurfacePalettePreview(THREE, defaultBinding, resources, packageUrl);
}

function getPalettePreviewSide(THREE, cullMode) {
  const normalized = String(cullMode || "Back").trim().toLowerCase();
  if (normalized === "off") return THREE.DoubleSide;
  if (normalized === "front") return THREE.BackSide;
  return THREE.FrontSide;
}

function buildSurfaceProfilePreviewGlsl(catalog) {
  const profiles = catalog?.surfaceProfiles || [];
  const cases = profiles.map((profile, index) => `
  if ( profileIndex < ${Number(index) + 0.5} ) {
    roughness = ${Number(profile.roughness ?? 0.7).toFixed(6)};
    metallic = ${Number(profile.metallic ?? 0).toFixed(6)};
    specular = ${Number(profile.specular ?? 0.04).toFixed(6)};
    rim = ${Number(profile.rim ?? 0.1).toFixed(6)};
    return;
  }`).join("");
  return `
void wgSurfaceProfileParams( float profileIndex, out float roughness, out float metallic, out float specular, out float rim ) {
  roughness = 0.7;
  metallic = 0.0;
  specular = 0.04;
  rim = 0.1;
${cases}
}`;
}

function createSurfacePalettePreviewMaterial(THREE, source, surfacePalette) {
  if (!THREE?.MeshStandardMaterial || !surfacePalette?.texture) return source?.clone?.() || source;
  const material = new THREE.MeshStandardMaterial({
    map: surfacePalette.texture,
    color: 0xffffff,
    roughness: 1,
    metalness: 1,
    side: getPalettePreviewSide(THREE, surfacePalette.cullMode),
    transparent: false,
    opacity: 1,
    depthTest: true,
    depthWrite: true,
    vertexColors: false
  });
  material.name = `${source?.name || "Palette"}_ColorPaletteSurfaceProfile`;
  const profileFunctions = buildSurfaceProfilePreviewGlsl(surfacePalette.catalog);
  material.onBeforeCompile = shader => {
    shader.uniforms ||= {};
    shader.uniforms.wgFaceDetailTex = { value: surfacePalette.faceDetailTexture };
    shader.uniforms.wgFaceDetailIntensity = { value: surfacePalette.faceDetailIntensity };
    shader.vertexShader = shader.vertexShader
      .replace("void main() {", `
attribute vec4 wgPaletteData;
varying vec4 vWgPaletteData;
attribute vec2 wgFaceDetailUv;
varying vec2 vWgFaceDetailUv;

void main() {
  vWgPaletteData = wgPaletteData;
  vWgFaceDetailUv = wgFaceDetailUv;
`);
    shader.fragmentShader = shader.fragmentShader
      .replace("void main() {", `
varying vec4 vWgPaletteData;
varying vec2 vWgFaceDetailUv;
uniform sampler2D wgFaceDetailTex;
uniform float wgFaceDetailIntensity;
${profileFunctions}

void main() {
  vec4 wgPaletteVertexData = vWgPaletteData;
  float wgSurfaceProfile = min( 15.0, floor( clamp( wgPaletteVertexData.a, 0.0, 0.999999 ) * 16.0 ) );
  float wgProfileRoughness;
  float wgProfileMetallic;
  float wgProfileSpecular;
  float wgProfileRim;
  wgSurfaceProfileParams( wgSurfaceProfile, wgProfileRoughness, wgProfileMetallic, wgProfileSpecular, wgProfileRim );
`)
      .replace("#include <color_fragment>", `
  diffuseColor.rgb *= mix( 0.72, 1.0, clamp( wgPaletteVertexData.r, 0.0, 1.0 ) );
`)
      .replace("#include <map_fragment>", `
#ifdef USE_MAP
  bool wgInvalidPaletteUv = any( lessThan( vMapUv, vec2( 0.0 ) ) ) || any( greaterThan( vMapUv, vec2( 1.0 ) ) );
  vec4 wgPaletteSample = texture2D( map, vMapUv );
  if ( wgInvalidPaletteUv ) {
    diffuseColor.rgb = vec3( 1.0, 0.0, 1.0 );
  } else {
    diffuseColor.rgb *= wgPaletteSample.rgb;
    ${surfacePalette.faceDetailTexture ? `bool wgValidFaceUv = all( greaterThanEqual( vWgFaceDetailUv, vec2( 0.0 ) ) ) && all( lessThanEqual( vWgFaceDetailUv, vec2( 1.0 ) ) );
    if ( wgValidFaceUv ) {
      vec4 wgFaceDetail = texture2D( wgFaceDetailTex, vWgFaceDetailUv );
      diffuseColor.rgb = mix( diffuseColor.rgb, wgFaceDetail.rgb, clamp( wgFaceDetail.a * wgFaceDetailIntensity, 0.0, 1.0 ) );
    }` : ""}
  }
#endif
`)
      .replace("#include <roughnessmap_fragment>", `
#include <roughnessmap_fragment>
  roughnessFactor = wgProfileRoughness;
`)
      .replace("#include <metalnessmap_fragment>", `
#include <metalnessmap_fragment>
  metalnessFactor = wgProfileMetallic;
`)
      .replace("#include <emissivemap_fragment>", `
#include <emissivemap_fragment>
  totalEmissiveRadiance += diffuseColor.rgb * clamp( wgPaletteVertexData.b, 0.0, 1.0 ) * 0.65;
  float wgFresnel = pow( 1.0 - clamp( dot( normalize( normal ), normalize( -vViewPosition ) ), 0.0, 1.0 ), 2.5 );
  totalEmissiveRadiance += diffuseColor.rgb * wgFresnel * wgProfileRim * clamp( wgPaletteVertexData.g, 0.0, 1.0 ) * 0.4;
`);
  };
  material.customProgramCacheKey = () => `WGameColorPaletteSurfaceProfilePreview.v3.${surfacePalette.catalogHash}.${surfacePalette.faceDetailTexture ? "face" : "plain"}`;
  material.userData = {
    ...(material.userData || {}),
    wgameSurfacePalette: true,
    paletteCatalogHash: surfacePalette.catalogHash,
    paletteAddressingMode: "colorUvSurfaceProfileVertexAlpha",
    paletteSurfaceProfileSource: surfacePalette.surfaceProfileSource,
    sourceMaterialName: source?.name || ""
  };
  material.needsUpdate = true;
  return material;
}

function previewLinearToData(value) {
  const clamped = Math.min(1, Math.max(0, Number(value) || 0));
  return clamped <= 0.0031308 ? clamped * 12.92 : 1.055 * Math.pow(clamped, 1 / 2.4) - 0.055;
}

function applySurfacePalettePreviewMaterials(THREE, root, surfacePalette) {
  if (!root?.traverse || !surfacePalette?.texture) {
    return { appliedCount: 0, missingPaletteUvCount: 0, missingPaletteDataCount: 0, missingFaceDetailUvCount: 0 };
  }
  const replacedMaterials = new Set();
  const replacedGeometries = new Set();
  const materialAudit = [];
  let appliedCount = 0;
  let missingPaletteUvCount = 0;
  let missingPaletteDataCount = 0;
  let missingFaceDetailUvCount = 0;
  root.traverse(object => {
    if (!object?.isMesh || !object.geometry || !object.material) return;
    const paletteUv = object.geometry.getAttribute?.("uv1");
    if (!paletteUv || paletteUv.itemSize < 2 || paletteUv.count <= 0) {
      missingPaletteUvCount++;
      return;
    }
    const paletteData = object.geometry.getAttribute?.("color");
    if (!paletteData || paletteData.itemSize < 4 || paletteData.count < paletteUv.count) {
      missingPaletteDataCount++;
      return;
    }
    const faceDetailUv = object.geometry.getAttribute?.("uv2");
    if (surfacePalette.faceDetailEnabled && (!faceDetailUv || faceDetailUv.itemSize < 2 || faceDetailUv.count < paletteUv.count)) {
      missingFaceDetailUvCount++;
      return;
    }

    const sourceGeometry = object.geometry;
    const geometry = sourceGeometry.clone();
    const uvValues = new Float32Array(paletteUv.count * 2);
    const dataValues = new Float32Array(paletteUv.count * 4);
    for (let index = 0; index < paletteUv.count; index++) {
      uvValues[index * 2] = paletteUv.getX(index);
      uvValues[index * 2 + 1] = paletteUv.getY(index);
      dataValues[index * 4] = previewLinearToData(paletteData.getX(index));
      dataValues[index * 4 + 1] = previewLinearToData(paletteData.getY(index));
      dataValues[index * 4 + 2] = previewLinearToData(paletteData.getZ(index));
      dataValues[index * 4 + 3] = paletteData.getW(index);
    }
    const AttributeType = THREE.Float32BufferAttribute || THREE.BufferAttribute;
    geometry.setAttribute("uv", new AttributeType(uvValues, 2));
    geometry.setAttribute("wgPaletteData", new AttributeType(dataValues, 4));
    const faceValues = new Float32Array(paletteUv.count * 2);
    for (let index = 0; index < paletteUv.count; index++) {
      faceValues[index * 2] = faceDetailUv ? faceDetailUv.getX(index) : -1;
      faceValues[index * 2 + 1] = faceDetailUv ? faceDetailUv.getY(index) : -1;
    }
    geometry.setAttribute("wgFaceDetailUv", new AttributeType(faceValues, 2));
    object.geometry = geometry;
    replacedGeometries.add(sourceGeometry);

    const sourceMaterials = Array.isArray(object.material) ? object.material : [object.material];
    const previewMaterials = sourceMaterials.map(source => {
      if (source) replacedMaterials.add(source);
      return createSurfacePalettePreviewMaterial(THREE, source, surfacePalette);
    });
    object.material = Array.isArray(object.material) ? previewMaterials : previewMaterials[0];
    appliedCount += previewMaterials.length;
    materialAudit.push({
      rendererNodePath: getPreviewNodeRelativePath(root, object),
      sourceMaterialNames: sourceMaterials.map(material => material?.name || ""),
      paletteCatalogHash: surfacePalette.catalogHash,
      paletteAddressingMode: "colorUvSurfaceProfileVertexAlpha",
      paletteUvAttribute: "uv1",
      surfaceProfileSource: surfacePalette.surfaceProfileSource,
      paletteDataAttribute: "color.rgba"
    });
  });
  for (const material of replacedMaterials) material.dispose?.();
  for (const geometry of replacedGeometries) geometry.dispose?.();
  root.userData = {
    ...(root.userData || {}),
    packagePreviewMaterials: {
      appliedCount,
      fallbackCount: 0,
      slotBindingCount: 0,
      surfacePalette: true,
      paletteCatalogHash: surfacePalette.catalogHash,
      missingPaletteUvCount,
      missingPaletteDataCount,
      missingFaceDetailUvCount
    },
    packagePreviewMaterialAudit: materialAudit
  };
  return { appliedCount, missingPaletteUvCount, missingPaletteDataCount, missingFaceDetailUvCount };
}

function buildSurfacePaletteUsageFromPreview(root, surfacePalette) {
  const catalog = surfacePalette?.catalog;
  const counts = new Map();
  let invalidFaceCount = 0;
  let inspectedFaceCount = 0;
  root?.traverse?.(object => {
    if (!object?.isMesh || !object.geometry) return;
    const geometry = object.geometry;
    const paletteUv = geometry.getAttribute?.("uv1");
    const paletteData = geometry.getAttribute?.("color");
    const position = geometry.getAttribute?.("position");
    if (!position) return;
    const indices = geometry.index?.array || null;
    const cornerCount = indices?.length || position.count;
    if (!paletteUv || paletteUv.itemSize < 2 || paletteUv.count < position.count
        || !paletteData || paletteData.itemSize < 4 || paletteData.count < position.count) {
      invalidFaceCount += Math.floor(cornerCount / 3);
      inspectedFaceCount += Math.floor(cornerCount / 3);
      return;
    }
    const colorIndexAtVertex = vertexIndex => {
      const u = paletteUv.getX(vertexIndex);
      const v = paletteUv.getY(vertexIndex);
      if (!Number.isFinite(u) || !Number.isFinite(v) || u < 0 || u > 1 || v < 0 || v > 1) return -1;
      const columnCenter = u * 256 - 0.5;
      const rowCenter = v * 256 - 0.5;
      if (Math.abs(columnCenter - Math.round(columnCenter)) > 0.001
          || Math.abs(rowCenter - Math.round(rowCenter)) > 0.001) return -1;
      const column = Math.round(columnCenter);
      const row = Math.round(rowCenter);
      if (column < 0 || column >= 256 || row < 0 || row >= 256) return -1;
      return row * 256 + column;
    };
    const profileAtVertex = vertexIndex => {
      const alpha = paletteData.getW(vertexIndex);
      if (!Number.isFinite(alpha) || alpha < 0 || alpha > 1) return -1;
      const alphaValue = alpha * 255;
      const alphaByte = Math.round(alphaValue);
      if (Math.abs(alphaValue - alphaByte) > 0.01 || alphaByte < 8 || alphaByte > 248 || (alphaByte - 8) % 16 !== 0) return -1;
      return (alphaByte - 8) / 16;
    };
    for (let corner = 0; corner + 2 < cornerCount; corner += 3) {
      inspectedFaceCount++;
      const firstVertex = indices ? indices[corner] : corner;
      const secondVertex = indices ? indices[corner + 1] : corner + 1;
      const thirdVertex = indices ? indices[corner + 2] : corner + 2;
      const firstColor = colorIndexAtVertex(firstVertex);
      const secondColor = colorIndexAtVertex(secondVertex);
      const thirdColor = colorIndexAtVertex(thirdVertex);
      const firstProfile = profileAtVertex(firstVertex);
      const secondProfile = profileAtVertex(secondVertex);
      const thirdProfile = profileAtVertex(thirdVertex);
      if (firstColor < 0 || firstColor !== secondColor || firstColor !== thirdColor
          || firstProfile < 0 || firstProfile !== secondProfile || firstProfile !== thirdProfile) {
        invalidFaceCount++;
        continue;
      }
      const logicalToken = firstColor * 16 + firstProfile;
      counts.set(logicalToken, (counts.get(logicalToken) || 0) + 1);
    }
  });
  return {
    format: "mx.characterPaletteUsage.v2",
    schemaVersion: "2.0",
    packageId: String(state.package?.manifest?.packageId || state.package?.packageId || ""),
    catalogHash: String(catalog?.catalogHash || ""),
    modelHash: "",
    paletteRepaintRequired: invalidFaceCount > 0,
    invalidFaceCount,
    inspectedFaceCount,
    tokenUsages: [...counts.entries()].sort((left, right) => left[0] - right[0]).map(([logicalToken, faceCount]) => {
      const colorIndex = Math.floor(logicalToken / 16);
      const surfaceProfile = logicalToken % 16;
      const red5 = (colorIndex >>> 11) & 31;
      const green6 = (colorIndex >>> 5) & 63;
      const blue5 = colorIndex & 31;
      const profileId = String(catalog?.surfaceProfiles?.[surfaceProfile]?.profileId || "");
      return {
      tokenId: `wgame.surface.v3.r${String(red5).padStart(2, "0")}.g${String(green6).padStart(2, "0")}.b${String(blue5).padStart(2, "0")}.${profileId}`,
      logicalToken,
      colorIndex,
      surfaceProfileId: profileId,
      faceCount,
      source: "character"
    }; })
  };
}

async function computePreviewResourceSha256(url) {
  if (!url || !globalThis.crypto?.subtle) return "";
  try {
    const response = await fetch(url, { cache: "no-store" });
    if (!response.ok) return "";
    const digest = await crypto.subtle.digest("SHA-256", await response.arrayBuffer());
    return `sha256:${[...new Uint8Array(digest)].map(value => value.toString(16).padStart(2, "0")).join("")}`;
  } catch {
    return "";
  }
}

function buildPackagePreviewMaterialBindings(resources = []) {
  const resourceByKey = new Map((resources || [])
    .filter(resource => resource?.resourceKey)
    .map(resource => [resource.resourceKey, resource]));
  const bindings = [];
  for (const materialResource of resources || []) {
    if (String(materialResource?.typeId || "").toLowerCase() !== "material") continue;
    const sourceFile = materialResource?.provenance?.sourceFile || materialResource?.relativePath || "";
    const materialName = getPreviewResourceFileName(sourceFile)
      .replace(/\.material\.json$/i, "")
      .replace(/\.mat$/i, "");
    const normalizedName = normalizePreviewMaterialName(materialName);
    if (!normalizedName) continue;

    const materialLocalId = String(materialResource?.localId || "").replace(/^material\./i, "");
    const textureCandidates = (materialResource?.dependencies || [])
      .map(dependency => resourceByKey.get(dependency?.resourceKey || ""))
      .filter(resource => String(resource?.typeId || "").toLowerCase() === "texture" && resource?.relativePath);
    const primaryTexture = textureCandidates.find(resource =>
      String(resource?.localId || "").replace(/^texture\./i, "") === materialLocalId)
      || textureCandidates.find(resource => !/(?:rim|mask|matcap)/i.test(`${resource?.localId || ""} ${resource?.resourceKey || ""}`))
      || textureCandidates[0];
    if (!primaryTexture && !materialResource?.relativePath) continue;

    bindings.push({
      materialName,
      normalizedName,
      materialResourceKey: materialResource.resourceKey || "",
      descriptorRelativePath: materialResource.relativePath || "",
      descriptorHash: getPreviewModelResourceHash(materialResource),
      textureResourceKey: primaryTexture?.resourceKey || "",
      textureRelativePath: primaryTexture?.relativePath || "",
      textureHash: getPreviewModelResourceHash(primaryTexture)
    });
  }
  return bindings;
}

async function loadPackagePreviewMaterialBindings(THREE, resources, packageUrl) {
  const bindings = buildPackagePreviewMaterialBindings(resources);
  if (!THREE?.TextureLoader || typeof packageUrl !== "function" || bindings.length === 0) return bindings;
  const resourceByKey = new Map((resources || [])
    .filter(resource => resource?.resourceKey)
    .map(resource => [resource.resourceKey, resource]));
  const loader = new THREE.TextureLoader();
  await Promise.all(bindings.map(async binding => {
    const descriptorPath = normalizePreviewPath(binding.descriptorRelativePath);
    if (descriptorPath) {
      try {
        const descriptorUrl = isRepoRelativePreviewPath(descriptorPath)
          ? encodeURI(`/${descriptorPath}`)
          : packageUrl(descriptorPath, binding.descriptorHash);
        const response = await fetch(descriptorUrl);
        if (response.ok) {
          binding.descriptor = await response.json();
          const descriptorTextures = binding.descriptor?.textures || [];
          const mainTextureBinding = descriptorTextures.find(item =>
            /^(?:_MainTex|_BaseMap|_BaseColorMap)$/i.test(String(item?.property || ""))
            && item?.resourceKey)
            || descriptorTextures.find(item => item?.resourceKey);
          const normalTextureBinding = descriptorTextures.find(item =>
            /^(?:_WgNormalTex|_NormalMap|_BumpMap)$/i.test(String(item?.property || ""))
            && item?.resourceKey);
          const maskTextureBinding = descriptorTextures.find(item =>
            /^(?:_WgMaskTex|_MaskMap|_MetallicGlossMap)$/i.test(String(item?.property || ""))
            && item?.resourceKey);
          binding.textureScale = mainTextureBinding?.scale || { x: 1, y: 1 };
          binding.textureOffset = mainTextureBinding?.offset || { x: 0, y: 0 };
          const mainTextureResource = resourceByKey.get(mainTextureBinding?.resourceKey || "");
          if (mainTextureResource?.relativePath) {
            binding.textureResourceKey = mainTextureResource.resourceKey || "";
            binding.textureRelativePath = mainTextureResource.relativePath;
            binding.textureHash = getPreviewModelResourceHash(mainTextureResource);
          }
          const normalTextureResource = resourceByKey.get(normalTextureBinding?.resourceKey || "");
          if (normalTextureResource?.relativePath) {
            binding.normalTextureResourceKey = normalTextureResource.resourceKey || "";
            binding.normalTextureRelativePath = normalTextureResource.relativePath;
            binding.normalTextureHash = getPreviewModelResourceHash(normalTextureResource);
          }
          const maskTextureResource = resourceByKey.get(maskTextureBinding?.resourceKey || "");
          if (maskTextureResource?.relativePath) {
            binding.maskTextureResourceKey = maskTextureResource.resourceKey || "";
            binding.maskTextureRelativePath = maskTextureResource.relativePath;
            binding.maskTextureHash = getPreviewModelResourceHash(maskTextureResource);
          }
        }
      } catch {
        binding.descriptor = null;
      }
    }

    binding.texture = await loadPackagePreviewTexture(
      THREE, loader, packageUrl, binding.textureRelativePath, binding.textureHash,
      binding.textureScale, binding.textureOffset, true);
    binding.normalTexture = await loadPackagePreviewTexture(
      THREE, loader, packageUrl, binding.normalTextureRelativePath, binding.normalTextureHash,
      { x: 1, y: 1 }, { x: 0, y: 0 }, false);
    binding.maskTexture = await loadPackagePreviewTexture(
      THREE, loader, packageUrl, binding.maskTextureRelativePath, binding.maskTextureHash,
      { x: 1, y: 1 }, { x: 0, y: 0 }, false);
  }));
  return bindings;
}

async function loadPackagePreviewTexture(THREE, loader, packageUrl, relativePath, hash, scale, offset, isColor) {
  const path = normalizePreviewPath(relativePath);
  if (!path) return null;
  const url = isRepoRelativePreviewPath(path) ? encodeURI(`/${path}`) : packageUrl(path, hash);
  try {
    const texture = await new Promise((resolve, reject) => loader.load(url, resolve, undefined, reject));
    if (texture) {
      if (isColor && THREE.SRGBColorSpace) texture.colorSpace = THREE.SRGBColorSpace;
      else if (!isColor && THREE.NoColorSpace !== undefined) texture.colorSpace = THREE.NoColorSpace;
      if (THREE.RepeatWrapping !== undefined) {
        texture.wrapS = THREE.RepeatWrapping;
        texture.wrapT = THREE.RepeatWrapping;
      }
      texture.repeat?.set?.(Number(scale?.x ?? 1), Number(scale?.y ?? 1));
      texture.offset?.set?.(Number(offset?.x ?? 0), Number(offset?.y ?? 0));
      texture.needsUpdate = true;
    }
    return texture;
  } catch {
    return null;
  }
}

function getPackagePreviewAnimationCandidates(pkg, motionId) {
  if (motionId !== "source_idle") return [];
  const entries = pkg?.animationInventory?.entries || [];
  const candidates = entries
    .filter(entry => /\.fbx$/i.test(String(entry?.packageRelativePath || "")))
    .map(entry => {
      const clips = entry?.clips || [];
      const preferredClip = clips.find(clip => /^S_Sp_Idle$/i.test(String(clip?.name || "")))
        || clips.find(clip => /^S_Idle$/i.test(String(clip?.name || "")))
        || clips.find(clip => String(clip?.suggestedRole || "").toLowerCase() === "locomotion.idle")
        || null;
      const score = /^S_Sp_Idle$/i.test(String(preferredClip?.name || ""))
        ? 0
        : /^S_Idle$/i.test(String(preferredClip?.name || ""))
          ? 1
          : preferredClip
            ? 2
            : 99;
      return {
        packageRelativePath: String(entry.packageRelativePath || ""),
        clipName: String(preferredClip?.name || ""),
        loop: preferredClip?.isLooping !== false,
        score
      };
    })
    .filter(candidate => candidate.score < 99)
    .sort((a, b) => a.score - b.score);
  if (candidates.length > 0) return candidates;
  return [
    { packageRelativePath: "source/unity/animations/Special/S_Sp_Idle.fbx", clipName: "S_Sp_Idle", loop: true, score: 0 },
    { packageRelativePath: "source/unity/animations/Normal/S_Idle.fbx", clipName: "S_Idle", loop: true, score: 1 }
  ];
}

async function loadPackagePreviewAnimationMixer({ THREE, previewLoaders, packageUrl, root, pkg, motionId }) {
  if (!THREE?.AnimationMixer || !root || typeof packageUrl !== "function") return null;
  const candidates = getPackagePreviewAnimationCandidates(pkg, motionId);
  for (const candidate of candidates) {
    try {
      const previewAsset = await loadPreviewModel(previewLoaders, packageUrl(candidate.packageRelativePath));
      const animations = previewAsset?.root?.animations || [];
      const clip = animations.find(item => item?.name === candidate.clipName) || animations[0];
      if (!clip) continue;
      const mixer = new THREE.AnimationMixer(root);
      const action = mixer.clipAction(clip);
      action.setLoop(candidate.loop ? THREE.LoopRepeat : THREE.LoopOnce, candidate.loop ? Infinity : 1);
      action.clampWhenFinished = !candidate.loop;
      action.play();
      mixer.update(0);
      root.userData = {
        ...(root.userData || {}),
        packagePreviewAnimation: {
          relativePath: candidate.packageRelativePath,
          clipName: clip.name || candidate.clipName,
          source: "raw-material-package"
        }
      };
      disposePreviewObject(previewAsset.root);
      return mixer;
    } catch {
      // Try the next authored idle candidate, then fall back to the lightweight breath preview.
    }
  }
  return null;
}

function findPreviewMaterialBinding(materialName, bindings) {
  const normalized = normalizePreviewMaterialName(materialName);
  if (!normalized) return null;
  return (bindings || []).find(binding => binding?.normalizedName === normalized)
    || (bindings || []).find(binding => binding?.normalizedName
      && (normalized.endsWith(binding.normalizedName) || binding.normalizedName.endsWith(normalized)))
    || null;
}

function getPreviewNodeRelativePath(root, node) {
  if (!root || !node || node === root) return "";
  const segments = [];
  let current = node;
  while (current && current !== root) {
    if (current.name) segments.unshift(String(current.name));
    current = current.parent;
  }
  return current === root ? segments.join("/").replace(/\\/g, "/").replace(/^\/+|\/+$/g, "") : "";
}

function findPreviewRendererMaterialBinding(root, object, materialSlotIndex, rendererBindings = [], sourceMaterialName = "") {
  const slotBindings = (rendererBindings || []).filter(binding =>
    Number(binding?.materialSlotIndex ?? -1) < 0
      || Number(binding?.materialSlotIndex) === materialSlotIndex);
  if (slotBindings.length === 0) return null;
  const normalizedSourceName = normalizePreviewMaterialName(sourceMaterialName);
  const sourceNameMatches = slotBindings.filter(binding =>
    normalizedSourceName
    && normalizePreviewMaterialName(binding?.sourceMaterialName) === normalizedSourceName);
  if (sourceNameMatches.length === 1) return sourceNameMatches[0];
  const relativePath = getPreviewNodeRelativePath(root, object).toLowerCase();
  if (!relativePath) return null;

  const pathMatches = slotBindings.filter(binding => {
    const target = String(binding?.rendererNodePath || "").toLowerCase();
    return target && (relativePath === target
      || relativePath.endsWith(`/${target}`)
      || target.endsWith(`/${relativePath}`));
  });
  if (pathMatches.length === 1) return pathMatches[0];

  const leafName = relativePath.split("/").filter(Boolean).at(-1) || "";
  const leafMatches = slotBindings.filter(binding =>
    String(binding?.rendererNodePath || "").toLowerCase().split("/").filter(Boolean).at(-1) === leafName);
  return leafMatches.length === 1 ? leafMatches[0] : null;
}

function getPreviewMaterialDescriptorFloat(descriptor, propertyName, fallback = 0) {
  const value = (descriptor?.floats || []).find(item => item?.name === propertyName)?.value;
  return Number.isFinite(Number(value)) ? Number(value) : fallback;
}

function getPreviewMaterialDescriptorColor(descriptor, propertyNames = []) {
  for (const propertyName of propertyNames) {
    const value = (descriptor?.colors || []).find(item => item?.name === propertyName)?.value;
    if (value && Number.isFinite(Number(value.r)) && Number.isFinite(Number(value.g)) && Number.isFinite(Number(value.b))) {
      return value;
    }
  }
  return null;
}

function resolvePackagePreviewSurfaceSettings(descriptor) {
  const declaredSurfaceMode = String(descriptor?.surfaceMode || "").trim().toLowerCase();
  const hasDeclaredSurfaceMode = declaredSurfaceMode === "opaque" || declaredSurfaceMode === "alphaclip";
  const clippingMode = getPreviewMaterialDescriptorFloat(descriptor, "_ClippingMode", 0);
  const renderQueue = Number(descriptor?.renderQueue ?? 2000);
  const keywords = (descriptor?.keywords || []).join(" ");
  const legacyCutout = clippingMode > 0
    || /(?:ALPHATEST|CLIPPING)/i.test(keywords)
    || (renderQueue >= 2450 && renderQueue < 3000);
  const isCutout = declaredSurfaceMode === "alphaclip" || (!hasDeclaredSurfaceMode && legacyCutout);
  const isTransparent = !isCutout && !hasDeclaredSurfaceMode && renderQueue >= 3000;
  const declaredAlphaCutoff = Number(descriptor?.alphaCutoff);
  const alphaCutoff = Number.isFinite(declaredAlphaCutoff)
    ? declaredAlphaCutoff
    : getPreviewMaterialDescriptorFloat(
      descriptor,
      "_Cutoff",
      getPreviewMaterialDescriptorFloat(descriptor, "_Clipping_Level", 0.5));
  return {
    surfaceMode: isCutout ? "AlphaClip" : isTransparent ? "Transparent" : "Opaque",
    isCutout,
    isTransparent,
    alphaCutoff: Math.max(0, Math.min(1, alphaCutoff))
  };
}

function createCleanPackagePreviewMaterial(THREE, source, binding) {
  const descriptor = binding?.descriptor || null;
  const colorValue = getPreviewMaterialDescriptorColor(descriptor, ["_BaseColor", "_Color"]);
  const color = new THREE.Color(
    Number(colorValue?.r ?? 1),
    Number(colorValue?.g ?? 1),
    Number(colorValue?.b ?? 1));
  const cullMode = getPreviewMaterialDescriptorFloat(descriptor, "_CullMode", 2);
  const side = cullMode === 0
    ? THREE.DoubleSide
    : cullMode === 1
      ? THREE.BackSide
      : THREE.FrontSide;
  const surface = resolvePackagePreviewSurfaceSettings(descriptor);
  const MaterialType = THREE.MeshToonMaterial || THREE.MeshLambertMaterial || THREE.MeshBasicMaterial;
  const material = new MaterialType({
    map: binding.texture,
    normalMap: binding.normalTexture || null,
    color,
    side,
    transparent: surface.isTransparent,
    opacity: surface.isTransparent ? Math.max(0, Math.min(1, Number(colorValue?.a ?? 1))) : 1,
    alphaTest: surface.isCutout
      ? Math.max(0.000001, surface.alphaCutoff)
      : 0,
    depthTest: true,
    depthWrite: !surface.isTransparent,
    // Vendor vertex colors carry toon/cloth mask data and are not albedo modulation.
    vertexColors: false
  });
  material.name = binding.materialName || source?.name || "PackagePreviewMaterial";
  material.userData = {
    ...(material.userData || {}),
    packagePreviewMaterial: binding.materialResourceKey,
    packagePreviewTexture: binding.textureResourceKey,
    packagePreviewNormalTexture: binding.normalTextureResourceKey || "",
    packagePreviewMaskTexture: binding.maskTextureResourceKey || "",
    packagePreviewSurfaceMode: surface.surfaceMode,
    packagePreviewAlphaCutoff: surface.alphaCutoff,
    packagePreviewSourceMaterial: source?.name || ""
  };
  material.needsUpdate = true;
  return material;
}

function applyPreviewMaterialBindings(THREE, root, bindings = [], rendererBindings = []) {
  if (!root?.traverse) return { appliedCount: 0, fallbackCount: 0 };
  const bindingsByResourceKey = new Map((bindings || [])
    .filter(binding => binding?.materialResourceKey)
    .map(binding => [binding.materialResourceKey, binding]));
  const replacedMaterials = new Set();
  let appliedCount = 0;
  let fallbackCount = 0;
  let slotBindingCount = 0;
  const materialAudit = [];
  root.traverse(object => {
    if (!object?.isMesh || !object.material) return;
    const sourceMaterials = Array.isArray(object.material) ? object.material : [object.material];
    const previewMaterials = sourceMaterials.map((source, materialSlotIndex) => {
      if (!source?.clone) return source;
      const rendererBinding = findPreviewRendererMaterialBinding(root, object, materialSlotIndex, rendererBindings, source?.name || "");
      const binding = findPreviewMaterialBinding(source.name, bindings)
        || bindingsByResourceKey.get(rendererBinding?.materialResourceKey || "");
      materialAudit.push({
        rendererNodePath: getPreviewNodeRelativePath(root, object),
        materialSlotIndex,
        sourceMaterialName: source?.name || "",
        rendererMaterialResourceKey: rendererBinding?.materialResourceKey || "",
        resolvedMaterialResourceKey: binding?.materialResourceKey || "",
        textureResourceKey: binding?.texture ? binding.textureResourceKey : ""
      });
      if (binding?.texture) {
        const material = createCleanPackagePreviewMaterial(THREE, source, binding);
        appliedCount++;
        if (rendererBinding) slotBindingCount++;
        replacedMaterials.add(source);
        return material;
      }

      const material = source.clone();
      if (!material.map && material.color) {
        const luminance = Number(material.color.r || 0) * 0.2126
          + Number(material.color.g || 0) * 0.7152
          + Number(material.color.b || 0) * 0.0722;
        if (luminance < 0.035) {
          material.color.setHex(0x8796a5);
          fallbackCount++;
        }
      }
      material.needsUpdate = true;
      replacedMaterials.add(source);
      return material;
    });
    object.material = Array.isArray(object.material) ? previewMaterials : previewMaterials[0];
  });
  for (const material of replacedMaterials) material.dispose?.();
  root.userData = {
    ...(root.userData || {}),
    packagePreviewMaterials: { appliedCount, fallbackCount, slotBindingCount },
    packagePreviewMaterialAudit: materialAudit
  };
  return { appliedCount, fallbackCount, slotBindingCount };
}

async function addPreviewModelResource({
  THREE,
  previewLoaders,
  content,
  pickables,
  url,
  usageSource = null,
  objectPath,
  name,
  position = null,
  bindingPose = null,
  attachmentPose = null,
  wrapperPose = null,
  bindingPoseAdapter = "",
  attachmentPoseAdapter = "",
  wrapperPoseAdapter = "",
  fitHeightMeters = 0,
  previewUnitScale = 0,
  bakedSurface = null,
  surfacePalette = null,
  materialBindings = [],
  rendererMaterialBindings = [],
  hiddenNodePaths = [],
  poseNodePaths = [],
  normalizeUnityWrapperSourceRoot = false,
  boneSink = null,
  formatSink = null,
  transformTargetSink = null
}) {
  let loadedRoot = null;
  try {
    const usageGeneration = surfacePaletteUsageGeneration;
    const previewAsset = await loadPreviewModel(previewLoaders, url);
    const root = previewAsset.root;
    loadedRoot = root;
    if (normalizeUnityWrapperSourceRoot) normalizePreviewSourceRootForUnityWrapper(root);
    hidePreviewModelNodes(root, hiddenNodePaths);
    const paletteResult = bakedSurface
      ? applyBakedSurfacePreviewMaterials(THREE, root, bakedSurface, buildSurfaceProfilePreviewGlsl(bakedSurface.catalog))
      : applySurfacePalettePreviewMaterials(THREE, root, surfacePalette);
    // character_palette_usage.json 只属于角色本体模型：Unity 侧用导入后 Prefab 的 mesh
    // 哈希与 usage.modelHash 比对。武器与挂点预览同样会应用 surface palette，若允许它们
    // 覆盖这个共享槽位，保存时就会把武器模型哈希写进角色 usage，导致 Unity 导入失败。
    // 只有显式传入 usageSource 的本体挂载点才是 usage 的归属者。
    let pendingPaletteUsage = null;
    if (!bakedSurface && surfacePalette && paletteResult.appliedCount > 0 && usageSource?.modelUrl) {
      const modelHash = await computePreviewResourceSha256(usageSource.modelUrl);
      // await 期间可能已经切包 / 重新加载；代次不一致时本次扫描结果必须整体丢弃，
      // 不能把新哈希贴到旧的面数分布上。
      if (modelHash && usageGeneration === surfacePaletteUsageGeneration) {
        const usage = buildSurfacePaletteUsageFromPreview(root, surfacePalette);
        usage.modelHash = modelHash;
        pendingPaletteUsage = {
          usage,
          owner: {
            generation: usageGeneration,
            packageRelative: String(state.packageRelative || ""),
            packageId: String(state.package?.manifest?.packageId || state.package?.packageId || ""),
            bodyResourceKey: usageSource.resourceKey,
            bodyRelativePath: usageSource.relativePath,
            modelHash
          }
        };
      }
    }
    if (!bakedSurface && paletteResult.appliedCount === 0) {
      applyPreviewMaterialBindings(THREE, root, materialBindings, rendererMaterialBindings);
    }
    root.name = name || objectPath;
    const boneRecords = collectBoneRecords(root, poseNodePaths);
    const bindingAdapter = getPoseAdapterForPreviewFormat(previewAsset.format, bindingPoseAdapter);
    const attachmentAdapter = getPoseAdapterForPreviewFormat(previewAsset.format, attachmentPoseAdapter);
    const wrapperAdapter = getPoseAdapterForPreviewFormat(previewAsset.format, wrapperPoseAdapter);
    const bindingRoot = new THREE.Group();
    bindingRoot.name = `${root.name || "model"}_binding`;
    if (position) bindingRoot.position.copy(position);
    applyLocalPose(THREE, bindingRoot, bindingPose, { poseAdapter: bindingAdapter });
    applyLocalPose(THREE, bindingRoot, attachmentPose, { poseAdapter: attachmentAdapter });

    const modelWrapper = new THREE.Group();
    modelWrapper.name = `${root.name || "model"}_wrapper`;
    setObjectLocalPose(THREE, modelWrapper, wrapperPose, { poseAdapter: wrapperAdapter });
    if (wrapperPose) registerViewportPoseBinding(THREE, modelWrapper, wrapperPose, { poseAdapter: wrapperAdapter });
    const previewUnitWrapper = new THREE.Group();
    previewUnitWrapper.name = `${root.name || "model"}_preview_units`;
    applyPreviewModelUnitScale(THREE, previewUnitWrapper, root, previewAsset.format, { fitHeightMeters, previewUnitScale });
    previewUnitWrapper.add(root);
    modelWrapper.add(previewUnitWrapper);
    bindingRoot.add(modelWrapper);

    makeSelectable(bindingRoot, objectPath, pickables);
    content.add(bindingRoot);
    if (boneSink) boneSink(boneRecords);
    formatSink?.(previewAsset.format);
    transformTargetSink?.({ bindingRoot, modelWrapper, previewUnitWrapper, root, format: previewAsset.format, previewAsset });
    // 只在整段挂载全部成功后才发布 usage，避免中途抛异常却留下半成品槽位。
    publishSurfacePalettePreviewUsage(pendingPaletteUsage);
    return true;
  } catch (error) {
    if (bakedSurface) { disposePreviewObject(loadedRoot); bakedSurface.dispose(); throw error; }
    return false;
  }
}

function normalizePreviewSourceRootForUnityWrapper(root) {
  if (!root) return null;
  const sourceRoot = root.isMesh || root.isSkinnedMesh
    ? root
    : Array.isArray(root.children) && root.children.length === 1
      ? root.children[0]
      : null;
  if (!sourceRoot?.position?.set || !sourceRoot?.quaternion?.identity) return null;

  sourceRoot.position.set(0, 0, 0);
  sourceRoot.quaternion.identity();
  sourceRoot.updateMatrix?.();
  sourceRoot.updateMatrixWorld?.(true);
  root.userData = {
    ...(root.userData || {}),
    unityWrapperSourceRootNormalized: true
  };
  return sourceRoot;
}

function applyPreviewModelUnitScale(THREE, previewUnitWrapper, root, format, options = {}) {
  if (format !== "fbx" || !previewUnitWrapper || !root) return 1;
  const explicitScale = Number(options?.previewUnitScale || 0);
  if (Number.isFinite(explicitScale) && explicitScale > 0 && (explicitScale < 0.999 || explicitScale > 1.001)) {
    previewUnitWrapper.scale.setScalar(explicitScale);
    previewUnitWrapper.userData.previewUnitScale = {
      source: "diagnostic",
      scale: explicitScale
    };
    return explicitScale;
  }
  const fitHeightMeters = Number(options?.fitHeightMeters || 0);
  const targetHeight = Number(fitHeightMeters || 0);
  if (!Number.isFinite(targetHeight) || targetHeight <= 0) return 1;
  root.updateWorldMatrix(true, true);
  const box = new THREE.Box3().setFromObject(root);
  if (box.isEmpty()) return 1;
  const size = box.getSize(new THREE.Vector3());
  const sourceHeight = Number(size.y || 0);
  if (!Number.isFinite(sourceHeight) || sourceHeight <= 0) return 1;
  const scale = targetHeight / sourceHeight;
  if (!Number.isFinite(scale) || scale <= 0) return 1;
  if (scale > 0.6 && scale < 1.4) return 1;
  previewUnitWrapper.scale.setScalar(scale);
  previewUnitWrapper.userData.previewUnitScale = {
    sourceHeight,
    targetHeight,
    scale
  };
  return scale;
}

async function addGltfResource({ loader, ...options }) {
  return addPreviewModelResource({
    ...options,
    previewLoaders: options.previewLoaders || { gltf: loader, fbx: null }
  });
}

function disposePreviewObject(root) {
  if (!root?.traverse) return;
  const geometries = new Set();
  const materials = new Set();
  const textures = new Set();
  const skeletons = new Set();
  root.traverse(object => {
    if (object.geometry?.dispose) geometries.add(object.geometry);
    if (object.skeleton?.dispose) skeletons.add(object.skeleton);
    const objectMaterials = Array.isArray(object.material)
      ? object.material
      : object.material
        ? [object.material]
        : [];
    for (const material of objectMaterials) {
      if (!material) continue;
      materials.add(material);
      for (const value of Object.values(material)) {
        if (value?.isTexture && value.dispose) textures.add(value);
      }
    }
  });
  for (const skeleton of skeletons) skeleton.dispose();
  for (const texture of textures) texture.dispose();
  for (const material of materials) material.dispose?.();
  for (const geometry of geometries) geometry.dispose();
}

// Capture before applying a preview pose or animation. Dimensions match the scale-free collider parents.
function captureHumanoidHurtboxReference(THREE, content, records, poseAdapter) {
  content.updateMatrixWorld(true);
  const contentInverse = content.matrixWorld.clone().invert();
  return records.filter(record => record.bone?.isBone).map(record => {
    const object = new THREE.Object3D();
    record.bone.updateWorldMatrix(true, false);
    new THREE.Matrix4().multiplyMatrices(contentInverse, record.bone.matrixWorld)
      .decompose(object.position, object.quaternion, object.scale);
    const pose = readObjectPoseForAdapter(THREE, object, poseAdapter);
    return { path: record.path, name: record.name, position: pose.position, rotation: pose.rotation };
  });
}
globalThis.captureHumanoidHurtboxReference = captureHumanoidHurtboxReference;

function collectBoneRecords(root, poseNodePaths = []) {
  const records = [];
  const requestedPoseNodePaths = new Set((poseNodePaths || [])
    .map(value => String(value || "").replace(/\\/g, "/").replace(/^\/+|\/+$/g, ""))
    .filter(Boolean));
  root.traverse(object => {
    const path = getPreviewBonePath(object, root);
    if (!path) return;
    const isRequestedPoseNode = requestedPoseNodePaths.has(path)
      || requestedPoseNodePaths.has(String(object.name || ""));
    if (!object.isBone && !isRequestedPoseNode) return;
    const parentPath = object.parent ? getPreviewBonePath(object.parent, root) : "";
    const depth = Math.max(0, path.split("/").length - 1);
    records.push({
      bone: object,
      name: object.name || path.split("/").pop() || path,
      path,
      alias: object.name ? `${object.isBone ? "bone" : "locator"}.${object.name}` : "",
      parentPath,
      depth,
      restQuaternion: object.quaternion.clone(),
      posedQuaternion: object.quaternion.clone()
    });
  });
  return records;
}

function getPreviewBonePath(object, root) {
  const names = [];
  let cursor = object;
  while (cursor && cursor !== root) {
    if (cursor.name) names.unshift(cursor.name);
    cursor = cursor.parent;
  }
  return names.join("/");
}

function syncPreviewBones(THREE, records) {
  const previewBones = (records || [])
    .map(record => {
      const point = new THREE.Vector3();
      record.bone?.getWorldPosition(point);
      return {
        name: record.name || "",
        path: record.path || "",
        alias: record.alias || "",
        parentPath: record.parentPath || "",
        depth: Number.isFinite(record.depth) ? record.depth : 0,
        position: {
          x: numberOrDefault(point.x, 0),
          y: numberOrDefault(point.y, 0),
          z: numberOrDefault(point.z, 0)
        }
      };
    })
    .filter(record => record.path);
  const key = JSON.stringify(previewBones.map(record => [
    record.path,
    record.alias,
    record.parentPath,
    record.depth,
    Number(record.position.x.toFixed(4)),
    Number(record.position.y.toFixed(4)),
    Number(record.position.z.toFixed(4))
  ]));
  if (key === state.previewBoneKey) return;
  state.previewBones = previewBones;
  state.previewBoneKey = key;
  renderInspector();
}

function addBoneGizmos(THREE, scene, pickables, boneRecords) {
  if (!boneRecords?.length) return;
  const selectedValue = getCurrentBoneSelectionValue();
  const group = new THREE.Group();
  group.name = "characterstudio_bone_picker";
  const pointGeometry = new THREE.SphereGeometry(0.018, 12, 8);
  const selectedPointGeometry = new THREE.SphereGeometry(0.034, 16, 10);
  for (const record of boneRecords) {
    const selected = isBoneRecordSelected(record, selectedValue);
    const point = new THREE.Vector3();
    record.bone.getWorldPosition(point);
    const material = new THREE.MeshBasicMaterial({
      color: selected ? 0xffa11f : 0x277f8e,
      transparent: true,
      opacity: selected ? 0.95 : 0.58,
      depthTest: false
    });
    const marker = new THREE.Mesh(selected ? selectedPointGeometry : pointGeometry, material);
    marker.position.copy(point);
    marker.renderOrder = selected ? 42 : 40;
    setBonePickData(marker, record);
    pickables.push(marker);
    group.add(marker);

    if (!record.bone.parent?.isBone) continue;
    const parent = new THREE.Vector3();
    record.bone.parent.getWorldPosition(parent);
    const line = new THREE.Line(
      new THREE.BufferGeometry().setFromPoints([parent, point]),
      new THREE.LineBasicMaterial({
        color: selected ? 0xffa11f : 0x277f8e,
        transparent: true,
        opacity: selected ? 0.9 : 0.36,
        depthTest: false
      })
    );
    line.renderOrder = selected ? 41 : 39;
    setBonePickData(line, record);
    pickables.push(line);
    group.add(line);
  }
  scene.add(group);
}

function setBonePickData(object, record) {
  object.userData.bonePick = {
    name: record.name || "",
    path: record.path || "",
    alias: record.alias || ""
  };
}

function applyPreviewPose(THREE, boneRecords, poseId) {
  if (!boneRecords?.length) return;
  resetPreviewBonePose(boneRecords);
  const rotate = (semanticKey, x = 0, y = 0, z = 0) => {
    const record = findPreviewBoneBySemantic(boneRecords, semanticKey);
    if (!record?.bone) return;
    record.bone.quaternion.multiply(new THREE.Quaternion().setFromEuler(new THREE.Euler(
      degreesToRadians(x),
      degreesToRadians(y),
      degreesToRadians(z),
      "XYZ"
    )));
  };

  if (poseId === "guard") {
    rotate("right_upper_arm", 0, 0, -28);
    rotate("left_upper_arm", 0, 0, 28);
    rotate("right_forearm", 0, 18, -42);
    rotate("left_forearm", 0, -18, 42);
    rotate("chest", 0, 10, 0);
  } else if (poseId === "attack") {
    rotate("right_upper_arm", -18, -16, -62);
    rotate("right_forearm", 8, 28, -36);
    rotate("left_upper_arm", 8, -8, 24);
    rotate("chest", 0, -18, 0);
    rotate("hips", 0, -8, 0);
  } else if (poseId === "inspect") {
    rotate("right_upper_arm", 0, 0, -86);
    rotate("left_upper_arm", 0, 0, 86);
    rotate("right_forearm", 0, 0, -8);
    rotate("left_forearm", 0, 0, 8);
    rotate("right_leg", 0, 0, -8);
    rotate("left_leg", 0, 0, 8);
  }

  for (const record of boneRecords) {
    record.posedQuaternion = record.bone.quaternion.clone();
  }
}

function resetPreviewBonePose(boneRecords) {
  for (const record of boneRecords || []) {
    if (record?.bone && record.restQuaternion) record.bone.quaternion.copy(record.restQuaternion);
  }
}

function applyPreviewMotion(THREE, boneRecords, motionId, time) {
  if (!boneRecords?.length || motionId === "none") return;
  for (const record of boneRecords) {
    if (record?.bone && record.posedQuaternion) record.bone.quaternion.copy(record.posedQuaternion);
  }

  const apply = (semanticKey, x = 0, y = 0, z = 0) => {
    const record = findPreviewBoneBySemantic(boneRecords, semanticKey);
    if (!record?.bone) return;
    record.bone.quaternion.multiply(new THREE.Quaternion().setFromEuler(new THREE.Euler(
      degreesToRadians(x),
      degreesToRadians(y),
      degreesToRadians(z),
      "XYZ"
    )));
  };

  if (motionId === "breath") {
    const sway = Math.sin(time * 2.1);
    apply("chest", 1.8 * sway, 0, 0);
    apply("neck", -0.9 * sway, 0, 0);
  } else if (motionId === "weapon_sway") {
    const sway = Math.sin(time * 3.2);
    apply("right_hand", 0, 6 * sway, 4 * sway);
    apply("left_hand", 0, -5 * sway, -3 * sway);
    apply("chest", 0, 2 * sway, 0);
  } else if (motionId === "walk_cycle") {
    const cycle = Math.sin(time * 4);
    apply("right_upper_arm", 14 * cycle, 0, 0);
    apply("left_upper_arm", -14 * cycle, 0, 0);
    apply("right_leg", -16 * cycle, 0, 0);
    apply("left_leg", 16 * cycle, 0, 0);
  }
}

function findPreviewBoneBySemantic(records, semanticKey) {
  return (records || []).find(record => getPreviewBoneSemanticKeys(record).includes(semanticKey)) || null;
}

function getPreviewBoneSemanticKey(record) {
  return getPreviewBoneSemanticKeys(record)[0] || "";
}

function getPreviewBoneSemanticKeys(record) {
  return getBoneSemanticKeys(`${record?.name || ""} ${(record?.path || "").split("/").filter(Boolean).pop() || ""}`);
}

function applyLocalPose(THREE, object, pose, options = {}) {
  if (!object || !pose) return;
  const previewPose = getPreviewPoseForAdapter(pose, options.poseAdapter);
  const position = previewPose.position || {};
  object.position.x += numberOrDefault(position.x, 0);
  object.position.y += numberOrDefault(position.y, 0);
  object.position.z += numberOrDefault(position.z, 0);

  const rotation = previewPose.rotation || {};
  const hasQuaternion = ["x", "y", "z"].some(key => Math.abs(numberOrDefault(rotation[key], 0)) > 0.000001)
    || Math.abs(numberOrDefault(rotation.w, 1) - 1) > 0.000001;
  if (hasQuaternion) {
    object.quaternion.multiply(new THREE.Quaternion(
      numberOrDefault(rotation.x, 0),
      numberOrDefault(rotation.y, 0),
      numberOrDefault(rotation.z, 0),
      numberOrDefault(rotation.w, 1)
    ).normalize());
  } else if (previewPose.eulerHint) {
    object.rotation.x += degreesToRadians(previewPose.eulerHint.x);
    object.rotation.y += degreesToRadians(previewPose.eulerHint.y);
    object.rotation.z += degreesToRadians(previewPose.eulerHint.z);
  }

  const scale = previewPose.scale || {};
  object.scale.x *= numberOrDefault(scale.x, 1);
  object.scale.y *= numberOrDefault(scale.y, 1);
  object.scale.z *= numberOrDefault(scale.z, 1);
}

function getPreviewPoseForAdapter(pose, poseAdapter = "") {
  if (!pose || poseAdapter !== UNITY_GLTF_PREVIEW_POSE_ADAPTER) return pose;
  return convertUnityPoseToPreviewPose(pose);
}

function convertUnityPoseToPreviewPose(pose) {
  const position = pose.position || {};
  const rotation = pose.rotation || {};
  const eulerHint = pose.eulerHint || {};
  const scale = pose.scale || {};
  return {
    ...pose,
    position: {
      ...position,
      x: -numberOrDefault(position.x, 0),
      y: numberOrDefault(position.y, 0),
      z: numberOrDefault(position.z, 0)
    },
    rotation: {
      ...rotation,
      x: numberOrDefault(rotation.x, 0),
      y: -numberOrDefault(rotation.y, 0),
      z: -numberOrDefault(rotation.z, 0),
      w: numberOrDefault(rotation.w, 1)
    },
    scale: {
      ...scale,
      x: numberOrDefault(scale.x, 1),
      y: numberOrDefault(scale.y, 1),
      z: numberOrDefault(scale.z, 1)
    },
    eulerHint: {
      ...eulerHint,
      x: numberOrDefault(eulerHint.x, 0),
      y: -numberOrDefault(eulerHint.y, 0),
      z: -numberOrDefault(eulerHint.z, 0)
    }
  };
}

function readObjectPoseForAdapter(THREE, object, poseAdapter = "") {
  const position = {
    x: object.position.x,
    y: object.position.y,
    z: object.position.z
  };
  const quaternion = object.quaternion.clone().normalize();
  if (poseAdapter === UNITY_GLTF_PREVIEW_POSE_ADAPTER) {
    position.x = -position.x;
    quaternion.set(quaternion.x, -quaternion.y, -quaternion.z, quaternion.w).normalize();
  }
  const euler = new THREE.Euler().setFromQuaternion(quaternion, "XYZ");
  return {
    position,
    scale: {
      x: object.scale.x,
      y: object.scale.y,
      z: object.scale.z
    },
    rotation: {
      x: quaternion.x,
      y: quaternion.y,
      z: quaternion.z,
      w: quaternion.w
    },
    eulerHint: {
      x: radiansToDegrees(euler.x),
      y: radiansToDegrees(euler.y),
      z: radiansToDegrees(euler.z)
    }
  };
}

function getEffectiveSocketPose(socket) {
  return getMutableSocketPose(socket);
}

function getMutableSocketPose(socket) {
  if (!socket) return null;
  socket.localPose = socket.localPose || {};
  return ensurePoseObject(socket.localPose, defaultSocketPoseParent(socket));
}

function getSocketBoneParentCandidates(socket) {
  return [socket?.locatorPath, socket?.bonePath, socket?.localPose?.parentPath]
    .filter(value => String(value || "").trim() !== "");
}

function getSocketPoseNodePaths(sockets = []) {
  return [...new Set((sockets || [])
    .flatMap(socket => getSocketBoneParentCandidates(socket))
    .map(value => String(value || "").trim())
    .filter(Boolean))];
}

function resolvePoseParentContent(THREE, content, pose, boneRecords, name, bonePathCandidates = [], options = {}) {
  if (pose?.parentKind !== "Bone") return content;
  const record = findBoneRecordForPose(boneRecords, pose, bonePathCandidates);
  if (!record?.bone) return content;

  const parent = new THREE.Group();
  parent.name = name || "bone_pose_parent";
  parent.userData.boneParent = {
    bone: record.bone,
    record,
    semanticKey: getSemanticFrameBoneKey(pose, record, options),
    inheritScale: options.inheritScale !== false
  };
  content.add(parent);
  syncBoneParentGroup(THREE, content, parent);
  return parent;
}

function updateBoneParentGroups(THREE, content) {
  content.traverse(object => {
    if (object.userData?.boneParent?.bone) syncBoneParentGroup(THREE, content, object);
  });
}

function syncBoneParentGroup(THREE, content, parent) {
  const boneParent = parent.userData?.boneParent;
  const bone = boneParent?.bone;
  if (!bone) return;
  if (syncSemanticBoneParentGroup(THREE, content, parent, boneParent)) return;
  content.updateMatrixWorld(true);
  bone.updateWorldMatrix(true, false);
  const localMatrix = new THREE.Matrix4()
    .copy(content.matrixWorld)
    .invert()
    .multiply(bone.matrixWorld);
  localMatrix.decompose(parent.position, parent.quaternion, parent.scale);
  if (parent.userData?.boneParent?.inheritScale === false) {
    parent.scale.set(1, 1, 1);
  }
}

function syncSemanticBoneParentGroup(THREE, content, parent, boneParent) {
  if (!boneParent?.semanticKey || !isSegmentBoneSemanticKey(boneParent.semanticKey)) return false;
  const bone = boneParent.bone;
  const record = boneParent.record || {};
  if (!bone) return false;

  content.updateMatrixWorld(true);
  bone.updateWorldMatrix(true, false);
  const origin = new THREE.Vector3().setFromMatrixPosition(bone.matrixWorld);
  const direction = getSemanticBoneFrameDirection(THREE, record, boneParent.semanticKey, origin);
  if (!direction) return false;

  const rawMatrix = bone.matrixWorld;
  const rawX = new THREE.Vector3().setFromMatrixColumn(rawMatrix, 0);
  const rawZ = new THREE.Vector3().setFromMatrixColumn(rawMatrix, 2);
  const xAxis = projectFrameAxis(THREE, rawX, direction)
    || projectFrameAxis(THREE, rawZ, direction)
    || getFallbackFrameXAxis(THREE, direction);
  const zAxis = new THREE.Vector3().crossVectors(xAxis, direction).normalize();
  if (zAxis.lengthSq() < 0.000001) return false;
  xAxis.crossVectors(direction, zAxis).normalize();

  const worldMatrix = new THREE.Matrix4().makeBasis(xAxis, direction, zAxis);
  worldMatrix.setPosition(origin);
  const localMatrix = new THREE.Matrix4()
    .copy(content.matrixWorld)
    .invert()
    .multiply(worldMatrix);
  localMatrix.decompose(parent.position, parent.quaternion, parent.scale);
  if (boneParent.inheritScale === false) parent.scale.set(1, 1, 1);
  return true;
}

function getSemanticBoneFrameDirection(THREE, record, semanticKey, origin) {
  const bone = record?.bone;
  if (!bone) return null;
  const child = findSemanticFrameChildBone(record, semanticKey);
  const target = new THREE.Vector3();
  if (child) {
    child.updateWorldMatrix(true, false);
    target.setFromMatrixPosition(child.matrixWorld);
    return normalizeFrameDirection(THREE, target.sub(origin));
  }

  const parentBone = bone.parent?.isBone ? bone.parent : null;
  if (!parentBone) return null;
  parentBone.updateWorldMatrix(true, false);
  target.setFromMatrixPosition(parentBone.matrixWorld);
  return normalizeFrameDirection(THREE, origin.clone().sub(target));
}

function findSemanticFrameChildBone(record, semanticKey) {
  const children = (record?.bone?.children || []).filter(child => child?.isBone);
  const expected = getSemanticFrameChildKeys(semanticKey);
  return children.find(child => {
    const keys = getBoneSemanticKeys(`${child.name || ""} ${getPreviewBonePath(child, record.bone) || ""}`);
    return expected.some(key => keys.includes(key));
  }) || children[0] || null;
}

function getSemanticFrameChildKeys(semanticKey) {
  if (semanticKey === "right_upper_arm") return ["right_forearm", "right_hand"];
  if (semanticKey === "left_upper_arm") return ["left_forearm", "left_hand"];
  if (semanticKey === "right_forearm") return ["right_hand"];
  if (semanticKey === "left_forearm") return ["left_hand"];
  if (semanticKey === "right_thigh") return ["right_shin", "right_foot"];
  if (semanticKey === "left_thigh") return ["left_shin", "left_foot"];
  if (semanticKey === "right_shin") return ["right_foot"];
  if (semanticKey === "left_shin") return ["left_foot"];
  return [];
}

function normalizeFrameDirection(THREE, direction) {
  if (!direction || direction.lengthSq() < 0.000001) return null;
  return direction.normalize();
}

function projectFrameAxis(THREE, axis, direction) {
  if (!axis || axis.lengthSq() < 0.000001) return null;
  const projected = axis.clone().sub(direction.clone().multiplyScalar(axis.dot(direction)));
  if (projected.lengthSq() < 0.000001) return null;
  return projected.normalize();
}

function getFallbackFrameXAxis(THREE, direction) {
  const reference = Math.abs(direction.dot(new THREE.Vector3(0, 1, 0))) > 0.92
    ? new THREE.Vector3(1, 0, 0)
    : new THREE.Vector3(0, 1, 0);
  return new THREE.Vector3().crossVectors(reference, direction).normalize();
}

function findBoneRecordForPose(records, pose, bonePathCandidates = []) {
  const candidates = [
    ...(Array.isArray(bonePathCandidates) ? bonePathCandidates : [bonePathCandidates]),
    pose?.parentPath
  ];
  for (const candidate of candidates) {
    const record = findBoneRecordForValue(records, candidate);
    if (record) return record;
  }
  return null;
}

function findBoneRecordForValue(records, value) {
  const normalized = String(value || "").trim();
  if (!normalized) return null;
  const exact = (records || []).find(record =>
    normalized === record.path
    || normalized === record.alias
    || normalized === record.name
  );
  if (exact) return exact;

  const requestedKeys = getBoneMatchKeys(normalized);
  if (requestedKeys.size === 0) return null;
  const ranked = (records || [])
    .map(record => ({ record, rank: getBoneRecordMatchRank(record, requestedKeys) }))
    .filter(item => item.rank > 0)
    .sort((a, b) => b.rank - a.rank || a.record.depth - b.record.depth);
  return ranked[0]?.record || null;
}

function getBoneRecordMatchRank(record, requestedKeys) {
  const nameKey = normalizeBoneMatchText(record.name);
  const aliasKey = normalizeBoneMatchText(String(record.alias || "").replace(/^bone\./i, ""));
  const pathKey = normalizeBoneMatchText((record.path || "").split("/").filter(Boolean).pop() || "");
  if (requestedKeys.has(nameKey) || requestedKeys.has(aliasKey) || requestedKeys.has(pathKey)) return 3;

  const semanticKeys = getBoneSemanticKeys(`${nameKey} ${aliasKey} ${pathKey}`);
  for (const semanticKey of semanticKeys) {
    if (requestedKeys.has(semanticKey) && isPrimarySemanticBoneRecord(record, semanticKey)) {
      return getSemanticBoneRecordRank(record, semanticKey);
    }
  }

  const recordKeys = getBoneMatchKeys(`${record.path || ""} ${record.alias || ""} ${record.name || ""}`);
  return Array.from(requestedKeys).some(key => recordKeys.has(key)) ? 1 : 0;
}

function getBoneMatchKeys(value) {
  const keys = new Set();
  const text = String(value || "");
  const lastSegment = text.split(/[\/\s]+/).filter(Boolean).pop() || text;
  for (const candidate of [text, lastSegment, text.replace(/^bone\./i, ""), lastSegment.replace(/^bone\./i, "")]) {
    const normalized = candidate.toLowerCase().replace(/[^a-z0-9]+/g, "");
    if (normalized) keys.add(normalized);
    for (const semantic of getBoneSemanticKeys(normalized)) {
      keys.add(semantic);
    }
  }
  return keys;
}

function normalizeBoneMatchText(value) {
  return String(value || "").toLowerCase().replace(/[^a-z0-9]+/g, "");
}

function getSemanticBoneKey(value) {
  return getBoneSemanticKeys(value)[0] || "";
}

function getBoneSemanticKeys(value) {
  const text = normalizeBoneMatchText(value);
  const keys = [];
  const add = key => {
    if (key && !keys.includes(key)) keys.push(key);
  };
  if (!text) return keys;

  if (/rightshoulder|rightupperarm|upperarmr|rupperarm|rightarm/.test(text)) add("right_upper_arm");
  if (/leftshoulder|leftupperarm|upperarml|lupperarm|leftarm/.test(text)) add("left_upper_arm");
  if (/rightforearm|rightlowerarm|forearmr|rforearm|lowerarmr|rlowerarm/.test(text)) add("right_forearm");
  if (/leftforearm|leftlowerarm|forearml|lforearm|lowerarml|llowerarm/.test(text)) add("left_forearm");
  if (/righthand|handr|rhand/.test(text)) add("right_hand");
  if (/lefthand|handl|lhand/.test(text)) add("left_hand");
  if (/rightupleg|rightupperleg|rightthigh|thighr|rthigh|upperlegr|rupperleg/.test(text)) {
    add("right_thigh");
    add("right_leg");
  }
  if (/leftupleg|leftupperleg|leftthigh|thighl|lthigh|upperlegl|lupperleg/.test(text)) {
    add("left_thigh");
    add("left_leg");
  }
  if (/rightlowerleg|rightshin|rightcalf|rightleg|shinr|rshin|calfr|rcalf|lowerlegr|rlowerleg/.test(text)) {
    add("right_shin");
    add("right_leg");
  }
  if (/leftlowerleg|leftshin|leftcalf|leftleg|shinl|lshin|calfl|lcalf|lowerlegl|llowerleg/.test(text)) {
    add("left_shin");
    add("left_leg");
  }
  if (/rightfoot|footr|rfoot/.test(text)) {
    add("right_foot");
    add("right_leg");
  }
  if (/leftfoot|footl|lfoot/.test(text)) {
    add("left_foot");
    add("left_leg");
  }
  if (/head|skull/.test(text)) add("head");
  if (/neck/.test(text)) add("neck");
  if (/chest|spine2|spine003|spine004|breast/.test(text)) add("chest");
  if (/spine1|spine|torso/.test(text)) add("spine");
  if (/hips|hip|pelvis/.test(text)) add("hips");
  return keys;
}

function getSemanticFrameBoneKey(pose, record, options = {}) {
  if (options.useSemanticBoneFrame === false) return "";
  if ((options.poseAdapter || "") === UNITY_GLTF_PREVIEW_POSE_ADAPTER) return "";
  const semanticKeys = getBoneSemanticKeys(`${pose?.parentPath || ""} ${record?.alias || ""} ${record?.name || ""} ${(record?.path || "").split("/").filter(Boolean).pop() || ""}`);
  return semanticKeys.find(isSegmentBoneSemanticKey) || "";
}

function isSegmentBoneSemanticKey(semanticKey) {
  return semanticKey === "right_upper_arm"
    || semanticKey === "left_upper_arm"
    || semanticKey === "right_forearm"
    || semanticKey === "left_forearm"
    || semanticKey === "right_thigh"
    || semanticKey === "left_thigh"
    || semanticKey === "right_shin"
    || semanticKey === "left_shin"
    || semanticKey === "right_hand"
    || semanticKey === "left_hand"
    || semanticKey === "right_foot"
    || semanticKey === "left_foot";
}

function isBroadBoneSemanticKey(semanticKey) {
  return semanticKey === "right_leg" || semanticKey === "left_leg";
}

function getSemanticBoneRecordRank(record, semanticKey) {
  if (isBroadBoneSemanticKey(semanticKey)) return 2;
  const nameText = normalizeBoneMatchText(record?.name || "");
  const pathText = normalizeBoneMatchText((record?.path || "").split("/").filter(Boolean).pop() || "");
  const candidates = [nameText, pathText].filter(Boolean);
  if (semanticKey === "right_upper_arm") {
    if (candidates.some(text => /^(rightarm|rightupperarm|upperarmr|rupperarm)$/.test(text))) return 2.75;
    if (candidates.some(text => /^(rightshoulder)$/.test(text))) return 2.25;
  }
  if (semanticKey === "left_upper_arm") {
    if (candidates.some(text => /^(leftarm|leftupperarm|upperarml|lupperarm)$/.test(text))) return 2.75;
    if (candidates.some(text => /^(leftshoulder)$/.test(text))) return 2.25;
  }
  return 2.5;
}

function isPrimarySemanticBoneRecord(record, semanticKey) {
  const keys = getBoneSemanticKeys(`${record?.name || ""} ${record?.alias || ""} ${(record?.path || "").split("/").filter(Boolean).pop() || ""}`);
  if (isBroadBoneSemanticKey(semanticKey)) return keys.includes(semanticKey);
  return keys[0] === semanticKey;
}

function degreesToRadians(value) {
  return Number(value || 0) * Math.PI / 180;
}

function radiansToDegrees(value) {
  return Number(value || 0) * 180 / Math.PI;
}

function roundTransformNumber(value, decimals = 6) {
  const number = Number(value);
  if (!Number.isFinite(number)) return 0;
  const factor = 10 ** decimals;
  return Math.round(number * factor) / factor;
}

function numberOrDefault(value, fallback) {
  const number = Number(value);
  return Number.isFinite(number) ? number : fallback;
}

function refreshBodyMeasurementPreview() {
  return viewportSyncBodyMeasurements?.() || false;
}

function attachBodyMeasurementPreview(THREE, content, host, body, createLabel = createTextSprite) {
  let guides = null;
  const refresh = () => {
    if (guides) {
      content.remove(guides);
      disposePreviewObject(guides);
      guides = null;
    }
    host.querySelector(".body-measurement-legend")?.remove();
    if (shouldShowBodyMeasurements()) {
      guides = createBodyMeasurementGuides(THREE, body, createLabel);
      content.add(guides);
      host.insertAdjacentHTML("beforeend", renderBodyMeasurementLegend());
    }
    return true;
  };
  refresh();
  return refresh;
}

function refreshMovementCollisionPreview() {
  return viewportSyncMovementCollision?.() || false;
}

function attachMovementCollisionPreview(THREE, content, pickables, host, body, poseAdapter = UNITY_GLTF_PREVIEW_POSE_ADAPTER) {
  let preview = null, pickMesh = null;
  const refresh = () => {
    if (preview) {
      content.remove(preview);
      disposePreviewObject(preview);
      const index = pickables.indexOf(pickMesh);
      if (index >= 0) pickables.splice(index, 1);
    }
    preview = createMovementCollisionPreviewGeometry(THREE, body, Boolean(state.movementSourceCapsuleVisible), poseAdapter, state.movementRegisteredBoundsVisible !== false);
    content.add(preview);
    pickMesh = preview.getObjectByName("movement-capsule-proxy");
    pickables.push(pickMesh);
    host.querySelector(".movement-collision-legend")?.remove();
    host.insertAdjacentHTML("beforeend", renderMovementCollisionLegend());
    return true;
  };
  refresh();
  return refresh;
}

function addFallbackBody(THREE, content, pickables, body = {}, registerSelectionVisual = null) {
  const height = Number(body.heightMeters || body.defaultCapsule?.height || 1.8);
  const radius = Number(body.radiusMeters || body.defaultCapsule?.radius || 0.34);
  const material = new THREE.MeshStandardMaterial({ color: 0xcddde0, roughness: 0.72, metalness: 0.05, emissive: 0x000000 });
  const applySelection = selected => {
    material.color.setHex(selected ? 0xd9a05a : 0xcddde0);
    material.emissive.setHex(selected ? 0x2e1c08 : 0x000000);
  };
  applySelection(state.selectedPath === "geometry/body");
  registerSelectionVisual?.("geometry/body", applySelection);
  const geometry = THREE.CapsuleGeometry
    ? new THREE.CapsuleGeometry(radius, Math.max(0.01, height - radius * 2), 8, 18)
    : new THREE.CylinderGeometry(radius, radius, height, 18);
  const mesh = new THREE.Mesh(geometry, material);
  mesh.position.y = height / 2;
  makeSelectable(mesh, "geometry/body", pickables);
  content.add(mesh);
}

function addColliderMeshes(THREE, content, pickables, colliders, bodyBoneRecords = [], registerSelectionVisual = null, registerTransformTarget = null, poseAdapter = UNITY_GLTF_PREVIEW_POSE_ADAPTER) {
  for (const collider of colliders) {
    const objectPath = `geometry/colliders/${collider.colliderId}`;
    const baseColor = getHurtboxColor(collider);
    const material = new THREE.MeshBasicMaterial({
      color: baseColor,
      transparent: true,
      opacity: 0.24,
      wireframe: true,
      side: THREE.DoubleSide,
      depthWrite: false
    });
    const applySelection = selected => {
      material.color.setHex(selected ? 0xffa11f : baseColor);
      material.opacity = selected ? 0.42 : 0.24;
      material.wireframe = !selected;
      material.needsUpdate = true;
    };
    applySelection(globalThis.isBodyWorkspaceSelectionVisualSelected?.(objectPath) ?? state.selectedPath === objectPath);
    registerSelectionVisual?.(objectPath, applySelection);
    const geometry = createHurtboxPreviewGeometry(THREE, collider);
    const mesh = new THREE.Mesh(geometry, material);
    mesh.userData.hurtbox = {
      colliderId: collider.colliderId || "",
      partId: collider.partId || "",
      hitZoneId: collider.hitZoneId || "",
      isWeakPoint: Boolean(collider.isWeakPoint)
    };
    const parent = resolvePoseParentContent(THREE, content, collider.localPose, bodyBoneRecords, `collider_${collider.colliderId}_parent`, [], { inheritScale: false, poseAdapter });
    setObjectLocalPose(THREE, mesh, collider.localPose, { poseAdapter });
    registerViewportPoseBinding(THREE, mesh, collider.localPose, { poseAdapter });
    makeSelectable(mesh, objectPath, pickables);
    const transformKey = `${objectPath}#localPose`;
    setWeaponTransformPick(mesh, transformKey);
    registerTransformTarget?.({
      key: transformKey,
      objectPath,
      object: mesh,
      pose: collider.localPose,
      poseAdapter,
      transformKind: "collider",
      collider,
      label: `${collider.colliderId || "collider"} collider`,
      defaults: { parentKind: collider.localPose?.parentKind || "BodyPart", parentPath: collider.localPose?.parentPath || collider.partId || "" }
    });
    parent.add(mesh);
  }
}

function getHurtboxColor(collider) {
  if (collider?.isWeakPoint) return 0xf04b4b;
  const key = String(collider?.hitZoneId || collider?.partId || collider?.colliderId || "hurtbox");
  let hash = 2166136261;
  for (let i = 0; i < key.length; i++) hash = Math.imul(hash ^ key.charCodeAt(i), 16777619);
  const palette = [0x1f9d8a, 0x2f80ed, 0x9b51e0, 0x27ae60, 0xf2994a, 0x56ccf2, 0xbb6bd9, 0x6fcf97];
  return palette[(hash >>> 0) % palette.length];
}

function getHurtboxShapeHandleDescriptors(collider = {}) {
  const shape = String(collider.shape || "Capsule").toLowerCase();
  const axes = [
    { axis: "x", sign: 1 }, { axis: "x", sign: -1 },
    { axis: "y", sign: 1 }, { axis: "y", sign: -1 },
    { axis: "z", sign: 1 }, { axis: "z", sign: -1 }
  ];
  if (shape === "box") return axes.map(item => ({ ...item, kind: "face" }));
  if (shape === "sphere") return axes.map(item => ({ ...item, kind: "radius" }));
  return [
    { axis: "y", sign: 1, kind: "height" },
    { axis: "y", sign: -1, kind: "height" },
    { axis: "x", sign: 1, kind: "radius" },
    { axis: "x", sign: -1, kind: "radius" },
    { axis: "z", sign: 1, kind: "radius" },
    { axis: "z", sign: -1, kind: "radius" }
  ];
}

function computeHurtboxShapeHandleEdit(collider = {}, handle = {}, dragDistance = 0, symmetric = false) {
  const shape = String(collider.shape || "Capsule").toLowerCase();
  const distance = Number.isFinite(Number(dragDistance)) ? Number(dragDistance) : 0;
  const axis = ["x", "y", "z"].includes(handle.axis) ? handle.axis : "x";
  const sign = Number(handle.sign) < 0 ? -1 : 1;
  const centerShift = { x: 0, y: 0, z: 0 };
  const result = {
    shape,
    size: {
      x: Math.max(HURTBOX_MIN_DIMENSION, numberOrDefault(collider.size?.x, 0.25)),
      y: Math.max(HURTBOX_MIN_DIMENSION, numberOrDefault(collider.size?.y, 0.25)),
      z: Math.max(HURTBOX_MIN_DIMENSION, numberOrDefault(collider.size?.z, 0.25))
    },
    radius: Math.max(HURTBOX_MIN_DIMENSION, numberOrDefault(collider.radius, 0.12)),
    height: Math.max(HURTBOX_MIN_DIMENSION, numberOrDefault(collider.height, 0.5)),
    centerShift,
    dimensionDelta: 0,
    symmetric: Boolean(symmetric)
  };

  if (shape === "box") {
    const start = result.size[axis];
    const next = Math.max(HURTBOX_MIN_DIMENSION, start + distance * (symmetric ? 2 : 1));
    result.size[axis] = roundTransformNumber(next);
    result.dimensionDelta = result.size[axis] - start;
    if (!symmetric) centerShift[axis] = sign * result.dimensionDelta * 0.5;
    return result;
  }

  if (shape === "capsule" && handle.kind === "height") {
    const start = result.height;
    const minimumHeight = result.radius * 2 + HURTBOX_MIN_DIMENSION;
    result.height = roundTransformNumber(Math.max(minimumHeight, start + distance * (symmetric ? 2 : 1)));
    result.dimensionDelta = result.height - start;
    if (!symmetric) centerShift.y = sign * result.dimensionDelta * 0.5;
    return result;
  }

  const startRadius = result.radius;
  result.radius = roundTransformNumber(Math.max(HURTBOX_MIN_DIMENSION, startRadius + distance));
  result.dimensionDelta = result.radius - startRadius;
  if (shape === "capsule") {
    result.height = roundTransformNumber(Math.max(result.height, result.radius * 2 + HURTBOX_MIN_DIMENSION));
  }
  return result;
}

function applyHurtboxShapeHandleEdit(collider, edit) {
  if (!collider || !edit) return;
  if (edit.shape === "box") {
    collider.size = {
      x: roundTransformNumber(edit.size.x),
      y: roundTransformNumber(edit.size.y),
      z: roundTransformNumber(edit.size.z)
    };
  } else {
    collider.radius = roundTransformNumber(edit.radius);
    if (edit.shape === "capsule") collider.height = roundTransformNumber(edit.height);
  }
  const pose = ensurePoseObject(collider.localPose || (collider.localPose = defaultLocalPose("BodyPart", collider.partId || "")));
  pose.scale.x = 1;
  pose.scale.y = 1;
  pose.scale.z = 1;
}

function rotateHurtboxCenterShift(centerShift = {}, quaternion = {}) {
  const x = numberOrDefault(centerShift.x, 0);
  const y = numberOrDefault(centerShift.y, 0);
  const z = numberOrDefault(centerShift.z, 0);
  const qx = numberOrDefault(quaternion.x, 0);
  const qy = numberOrDefault(quaternion.y, 0);
  const qz = numberOrDefault(quaternion.z, 0);
  const qw = numberOrDefault(quaternion.w, 1);
  const ix = qw * x + qy * z - qz * y;
  const iy = qw * y + qz * x - qx * z;
  const iz = qw * z + qx * y - qy * x;
  const iw = -qx * x - qy * y - qz * z;
  return {
    x: ix * qw + iw * -qx + iy * -qz - iz * -qy,
    y: iy * qw + iw * -qy + iz * -qx - ix * -qz,
    z: iz * qw + iw * -qz + ix * -qy - iy * -qx
  };
}

function createHurtboxPreviewGeometry(THREE, collider = {}) {
  const shape = String(collider.shape || "Capsule").toLowerCase();
  if (shape === "box") {
    return new THREE.BoxGeometry(
      Math.max(HURTBOX_MIN_DIMENSION, numberOrDefault(collider.size?.x, 0.25)),
      Math.max(HURTBOX_MIN_DIMENSION, numberOrDefault(collider.size?.y, 0.25)),
      Math.max(HURTBOX_MIN_DIMENSION, numberOrDefault(collider.size?.z, 0.25)));
  }
  const radius = Math.max(HURTBOX_MIN_DIMENSION, numberOrDefault(collider.radius, 0.12));
  if (shape === "capsule" && THREE.CapsuleGeometry) {
    const height = Math.max(radius * 2 + HURTBOX_MIN_DIMENSION, numberOrDefault(collider.height, 0.5));
    return new THREE.CapsuleGeometry(radius, Math.max(HURTBOX_MIN_DIMENSION, height - radius * 2), 8, 16);
  }
  return new THREE.SphereGeometry(radius, 20, 12);
}

function replaceHurtboxPreviewGeometry(THREE, target) {
  if (!target?.object || !target?.collider) return;
  const previous = target.object.geometry;
  target.object.geometry = createHurtboxPreviewGeometry(THREE, target.collider);
  previous?.dispose?.();
  target.object.scale.set(1, 1, 1);
  target.object.updateMatrixWorld(true);
}

function getHurtboxHandleLocalPosition(collider = {}, handle = {}) {
  const shape = String(collider.shape || "Capsule").toLowerCase();
  const axis = ["x", "y", "z"].includes(handle.axis) ? handle.axis : "x";
  const sign = Number(handle.sign) < 0 ? -1 : 1;
  const result = { x: 0, y: 0, z: 0 };
  if (shape === "box") result[axis] = sign * Math.max(HURTBOX_MIN_DIMENSION, numberOrDefault(collider.size?.[axis], 0.25)) * 0.5;
  else if (shape === "capsule" && handle.kind === "height") result.y = sign * Math.max(HURTBOX_MIN_DIMENSION, numberOrDefault(collider.height, 0.5)) * 0.5;
  else result[axis] = sign * Math.max(HURTBOX_MIN_DIMENSION, numberOrDefault(collider.radius, 0.12));
  return result;
}

function createHurtboxShapeHandleController({ THREE, scene, camera, renderer, controls, host }) {
  const group = new THREE.Group();
  group.name = "HurtboxShapeHandles";
  group.matrixAutoUpdate = false;
  group.visible = false;
  group.userData.excludeFromFraming = true;
  scene.add(group);

  const raycaster = new THREE.Raycaster();
  const pointer = new THREE.Vector2();
  const plane = new THREE.Plane();
  const handles = [];
  let activeTarget = null;
  let drag = null;
  let hoveredHandle = null;
  let overlay = null;

  const axisColor = axis => axis === "x" ? 0xff5c5c : axis === "y" ? 0x67d878 : 0x5f9dff;
  const setPointerRay = event => {
    const rect = renderer.domElement.getBoundingClientRect();
    if (!rect.width || !rect.height) return false;
    pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    pointer.y = -(((event.clientY - rect.top) / rect.height) * 2 - 1);
    raycaster.setFromCamera(pointer, camera);
    return true;
  };

  const clearHandles = () => {
    while (handles.length > 0) {
      const handle = handles.pop();
      group.remove(handle);
      handle.geometry?.dispose?.();
      handle.material?.dispose?.();
    }
    hoveredHandle = null;
  };

  const syncHandlePositions = () => {
    if (!activeTarget?.object || !activeTarget?.collider) return;
    activeTarget.object.updateWorldMatrix(true, false);
    group.matrix.copy(activeTarget.object.matrixWorld);
    group.matrixWorldNeedsUpdate = true;
    for (const handle of handles) {
      const position = getHurtboxHandleLocalPosition(activeTarget.collider, handle.userData.hurtboxSizeHandle);
      handle.position.set(position.x, position.y, position.z);
    }
    group.updateMatrixWorld(true);
  };

  const rebuildHandles = target => {
    clearHandles();
    activeTarget = target;
    if (!target?.object || !target?.collider) {
      group.visible = false;
      return;
    }
    for (const descriptor of getHurtboxShapeHandleDescriptors(target.collider)) {
      const geometry = descriptor.kind === "face"
        ? new THREE.BoxGeometry(1, 1, 1)
        : new THREE.SphereGeometry(0.62, 12, 8);
      const material = new THREE.MeshBasicMaterial({
        color: axisColor(descriptor.axis),
        transparent: true,
        opacity: 0.96,
        depthTest: false,
        depthWrite: false
      });
      const mesh = new THREE.Mesh(geometry, material);
      mesh.name = `HurtboxSizeHandle_${descriptor.kind}_${descriptor.axis}_${descriptor.sign > 0 ? "positive" : "negative"}`;
      mesh.renderOrder = 1000;
      mesh.userData.hurtboxSizeHandle = { ...descriptor };
      group.add(mesh);
      handles.push(mesh);
    }
    group.visible = true;
    syncHandlePositions();
  };

  const sync = () => {
    if (drag) return;
    const target = getActiveViewportTransformTarget();
    const shouldShow = state.viewportMode === "character"
      && state.weaponTransformMode === "scale"
      && target?.transformKind === "collider";
    if (!shouldShow) {
      activeTarget = null;
      group.visible = false;
      clearHandles();
      removeOverlay();
      return;
    }
    if (activeTarget !== target || handles.length === 0) rebuildHandles(target);
    else syncHandlePositions();
  };

  const updateVisuals = () => {
    if (!group.visible || !activeTarget?.object) return;
    syncHandlePositions();
    const viewportHeight = Math.max(1, renderer.domElement.clientHeight || host.clientHeight || 1);
    const groupScale = group.getWorldScale(new THREE.Vector3());
    const scaleCompensation = Math.max(Math.abs(groupScale.x), Math.abs(groupScale.y), Math.abs(groupScale.z), 0.000001);
    for (const handle of handles) {
      const world = handle.getWorldPosition(new THREE.Vector3());
      const distance = Math.max(0.01, camera.position.distanceTo(world));
      const worldPerPixel = 2 * distance * Math.tan(THREE.MathUtils.degToRad(camera.fov) * 0.5) / viewportHeight;
      const pixels = handle === hoveredHandle || drag?.handle === handle ? 12 : 9;
      const size = worldPerPixel * pixels / scaleCompensation;
      handle.scale.setScalar(size);
      handle.material.opacity = handle === hoveredHandle || drag?.handle === handle ? 1 : 0.92;
    }
  };

  const pickHandle = event => {
    if (!group.visible || handles.length === 0 || !setPointerRay(event)) return null;
    return raycaster.intersectObjects(handles, false)[0]?.object || null;
  };

  const buildStableDragPlane = (handle, descriptor) => {
    const point = handle.getWorldPosition(new THREE.Vector3());
    const localAxis = new THREE.Vector3(
      descriptor.axis === "x" ? descriptor.sign : 0,
      descriptor.axis === "y" ? descriptor.sign : 0,
      descriptor.axis === "z" ? descriptor.sign : 0);
    const worldAxis = localAxis.clone().transformDirection(activeTarget.object.matrixWorld).normalize();
    const view = camera.position.clone().sub(point).normalize();
    let normal = view.clone().addScaledVector(worldAxis, -view.dot(worldAxis));
    if (normal.lengthSq() < 0.000001) {
      const cameraUpWorld = camera.up.clone().transformDirection(camera.matrixWorld);
      normal = cameraUpWorld.clone().addScaledVector(worldAxis, -cameraUpWorld.dot(worldAxis));
    }
    if (normal.lengthSq() < 0.000001) {
      normal = new THREE.Vector3(1, 0, 0).cross(worldAxis);
      if (normal.lengthSq() < 0.000001) normal.set(0, 0, 1).cross(worldAxis);
    }
    plane.setFromNormalAndCoplanarPoint(normal.normalize(), point);
    return { point, localAxis };
  };

  const pointerDown = event => {
    if (drag || event.button !== 0 || !state.canWrite) return false;
    const handle = pickHandle(event);
    if (!handle || !activeTarget?.collider) return false;
    event.preventDefault();
    event.stopPropagation();
    const descriptor = handle.userData.hurtboxSizeHandle;
    const frame = buildStableDragPlane(handle, descriptor);
    const intersection = raycaster.ray.intersectPlane(plane, new THREE.Vector3()) || frame.point.clone();
    const parent = activeTarget.object.parent;
    const startPointParent = parent.worldToLocal(intersection.clone());
    const localAxisParent = frame.localAxis.clone().applyQuaternion(activeTarget.object.quaternion).normalize();
    drag = {
      pointerId: event.pointerId,
      handle,
      descriptor,
      parent,
      startPointParent,
      localAxisParent,
      startObjectPosition: activeTarget.object.position.clone(),
      startObjectQuaternion: activeTarget.object.quaternion.clone(),
      beforeCollider: cloneHurtboxValue(activeTarget.collider),
      lastEdit: null,
      changed: false,
      altKey: Boolean(event.altKey)
    };
    activeTarget.transformStart = captureViewportTransformStart(activeTarget);
    beginUndoTransaction(`调整受击体尺寸 ${activeTarget.collider.colliderId || ""}`, `hurtbox-shape:${activeTarget.key}`);
    controls.enabled = false;
    renderer.domElement.setPointerCapture?.(event.pointerId);
    showOverlay();
    updateOverlay(0, drag.altKey);
    return true;
  };

  const pointerMove = event => {
    if (!drag) {
      const next = pickHandle(event);
      if (next !== hoveredHandle) {
        hoveredHandle = next;
        renderer.domElement.style.cursor = next ? "grab" : "";
      }
      return false;
    }
    if (event.pointerId !== drag.pointerId || !setPointerRay(event)) return true;
    event.preventDefault();
    event.stopPropagation();
    const intersection = raycaster.ray.intersectPlane(plane, new THREE.Vector3());
    if (!intersection) return true;
    const pointParent = drag.parent.worldToLocal(intersection.clone());
    const distance = pointParent.sub(drag.startPointParent).dot(drag.localAxisParent);
    const symmetric = Boolean(event.altKey);
    const edit = computeHurtboxShapeHandleEdit(drag.beforeCollider, drag.descriptor, distance, symmetric);
    applyHurtboxShapeHandleEdit(activeTarget.collider, edit);
    const rotatedShift = rotateHurtboxCenterShift(edit.centerShift, drag.startObjectQuaternion);
    const shift = new THREE.Vector3(rotatedShift.x, rotatedShift.y, rotatedShift.z);
    activeTarget.object.position.copy(drag.startObjectPosition).add(shift);
    activeTarget.pose = activeTarget.collider.localPose;
    replaceHurtboxPreviewGeometry(THREE, activeTarget);
    commitWeaponTransformTarget(THREE, activeTarget, {
      live: true,
      shapeHandle: true,
      suppressSymmetry: true
    });
    drag.lastEdit = edit;
    drag.changed = Math.abs(edit.dimensionDelta) > 0.0000001 || shift.lengthSq() > 0.000000000001;
    drag.altKey = symmetric;
    syncHandlePositions();
    syncColliderInspectorLiveFields(activeTarget.collider);
    updateOverlay(edit.dimensionDelta, symmetric);
    return true;
  };

  const finishDrag = cancelled => {
    if (!drag) return false;
    const activeDrag = drag;
    drag = null;
    try { renderer.domElement.releasePointerCapture?.(activeDrag.pointerId); } catch { }
    controls.enabled = true;
    renderer.domElement.style.cursor = "";
    removeOverlay();
    if (cancelled) {
      restoreColliderFromSnapshot(activeTarget, activeDrag.beforeCollider);
      replaceHurtboxPreviewGeometry(THREE, activeTarget);
      setObjectLocalPose(THREE, activeTarget.object, activeTarget.collider.localPose, { poseAdapter: activeTarget.poseAdapter });
      cancelUndoTransaction();
      refreshDirtyFromCleanSnapshot();
      syncHandlePositions();
      renderInspector();
      state.message = `${activeTarget.collider.colliderId || "受击体"} 的尺寸拖动已取消。`;
      renderShellStatus();
      return true;
    }

    const mirrored = activeDrag.changed
      ? applySymmetryEdit(findTarget(activeTarget.objectPath || ""), getHurtboxShapeSymmetryField(activeTarget.collider, activeDrag.descriptor))
      : false;
    updateUndoTransactionAfter();
    commitUndoTransaction();
    activeTarget.transformStart = null;
    renderInspector();
    renderViewportModeBar();
    const symmetryStatus = state.symmetryEditEnabled
      ? (mirrored ? "；配对侧已同步" : "；未找到对称配对")
      : "";
    state.message = activeDrag.changed
      ? `${activeTarget.collider.colliderId || "受击体"} 尺寸已更新${symmetryStatus}；尚未保存。`
      : "受击体尺寸未变化。";
    renderShellStatus();
    sync();
    if (mirrored) setTimeout(() => renderViewport(), 0);
    return true;
  };

  const pointerUp = event => {
    if (!drag || event.pointerId !== drag.pointerId) return false;
    event.preventDefault();
    event.stopPropagation();
    return finishDrag(false);
  };

  const pointerCancel = event => {
    if (!drag || event.pointerId !== drag.pointerId) return false;
    event.preventDefault();
    event.stopPropagation();
    return finishDrag(true);
  };

  function showOverlay() {
    if (overlay?.isConnected) return;
    overlay = document.createElement("aside");
    overlay.className = "hurtbox-size-edit-overlay";
    overlay.setAttribute("aria-live", "polite");
    host.append(overlay);
  }

  function updateOverlay(delta, symmetric) {
    if (!overlay || !activeTarget?.collider) return;
    overlay.innerHTML = `<strong>${escapeHtml(activeTarget.collider.colliderId || "collider")}</strong><span>${escapeHtml(activeTarget.collider.partId || "未指定部位")}</span><span>${escapeHtml(describeHurtboxShapeDimensions(activeTarget.collider))}</span><span>变化 ${formatSignedMeters(delta)} · ${symmetric ? "中心对称" : "对侧固定"}</span>`;
  }

  function removeOverlay() {
    overlay?.remove?.();
    overlay = null;
  }

  const cancelDrag = () => finishDrag(true);
  const dispose = () => {
    if (drag) finishDrag(true);
    removeOverlay();
    clearHandles();
    group.removeFromParent();
  };

  return { sync, updateVisuals, pointerDown, pointerMove, pointerUp, pointerCancel, cancelDrag, dispose };
}

function getHurtboxBindingReferenceStatus(collider = {}, boneParent = null) {
  const pose = collider.localPose || {};
  const parentKind = String(pose.parentKind || "ModelRoot");
  const requestedPath = String(pose.parentPath || "").trim();
  if (parentKind === "Bone") {
    const record = boneParent?.record || null;
    const resolved = Boolean(boneParent?.bone && record);
    return {
      parentKind,
      requestedPath,
      resolved,
      resolvedPath: resolved ? String(record.path || record.alias || record.name || requestedPath) : "",
      reason: resolved ? "已解析到当前动画骨骼" : `无法解析骨骼 ${requestedPath || "（空路径）"}`,
      record
    };
  }
  return {
    parentKind,
    requestedPath,
    resolved: true,
    resolvedPath: requestedPath || parentKind,
    reason: `${parentKind} 使用当前实际父坐标系`,
    record: null
  };
}

function computeHurtboxBindingReferenceMatrices(
  THREE,
  parentWorldMatrix,
  pose = {},
  poseAdapter = UNITY_GLTF_PREVIEW_POSE_ADAPTER) {
  const referenceMatrix = parentWorldMatrix?.clone?.() || new THREE.Matrix4();
  const localObject = new THREE.Object3D();
  setObjectLocalPose(THREE, localObject, pose, { poseAdapter });
  localObject.updateMatrix();
  return {
    referenceMatrix,
    finalMatrix: referenceMatrix.clone().multiply(localObject.matrix)
  };
}

function selectHurtboxBindingReferenceNeighbors(THREE, bone = null) {
  if (!bone) return { parent: null, primaryChild: null };
  const parent = bone.parent?.isBone ? bone.parent : null;
  const children = (bone.children || [])
    .filter(child => child?.isBone)
    .slice()
    .sort((left, right) => String(left.name || "").localeCompare(String(right.name || "")));
  if (children.length <= 1) return { parent, primaryChild: children[0] || null };

  bone.updateWorldMatrix?.(true, false);
  const origin = bone.getWorldPosition(new THREE.Vector3());
  const incoming = parent
    ? origin.clone().sub(parent.getWorldPosition(new THREE.Vector3()))
    : null;
  const hasIncomingDirection = Boolean(incoming && incoming.lengthSq() > 1e-10);
  if (hasIncomingDirection) incoming.normalize();

  let primaryChild = children[0];
  let bestScore = Number.NEGATIVE_INFINITY;
  for (const child of children) {
    child.updateWorldMatrix?.(true, false);
    const outgoing = child.getWorldPosition(new THREE.Vector3()).sub(origin);
    const length = outgoing.length();
    const alignment = hasIncomingDirection && length > 1e-5
      ? incoming.dot(outgoing.multiplyScalar(1 / length))
      : 0;
    // Follow the incoming chain direction first. Length only breaks nearly
    // equal branches, so a long horn or finger cannot win on length alone.
    const score = alignment * 1000 + Math.min(length, 100);
    if (score > bestScore + 1e-6) {
      bestScore = score;
      primaryChild = child;
    }
  }
  return { parent, primaryChild };
}

function createHurtboxBindingReferenceController({ THREE, scene, camera, renderer, host }) {
  const group = new THREE.Group();
  group.name = "HurtboxBindingReference";
  group.userData.excludeFromFraming = true;
  scene.add(group);

  const boneMarker = new THREE.Mesh(
    new THREE.SphereGeometry(0.5, 14, 10),
    new THREE.MeshBasicMaterial({ color: 0x2ff4ff, transparent: true, opacity: 0.98, depthTest: false, depthWrite: false }));
  boneMarker.name = "HurtboxBindingBoneOrigin";
  boneMarker.renderOrder = 914;
  boneMarker.userData.hurtboxBindingHelper = true;
  group.add(boneMarker);

  const boneMarkerCore = new THREE.Mesh(
    new THREE.SphereGeometry(0.5, 10, 7),
    new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.98, depthTest: false, depthWrite: false }));
  boneMarkerCore.name = "HurtboxBindingBoneStartCore";
  boneMarkerCore.renderOrder = 915;
  boneMarkerCore.userData.hurtboxBindingHelper = true;
  group.add(boneMarkerCore);

  const connector = new THREE.Line(
    new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(), new THREE.Vector3()]),
    new THREE.LineDashedMaterial({ color: 0xffc857, dashSize: 0.08, gapSize: 0.045, transparent: true, opacity: 0.78, depthTest: false, depthWrite: false }));
  connector.name = "HurtboxBindingLocalPoseOffset";
  connector.renderOrder = 912;
  connector.userData.hurtboxBindingHelper = true;
  group.add(connector);

  const adjacencyGroup = new THREE.Group();
  adjacencyGroup.name = "HurtboxBindingAdjacentBones";
  adjacencyGroup.userData.hurtboxBindingHelper = true;
  group.add(adjacencyGroup);

  let activeTarget = null;
  let activeStatus = null;
  let activeBone = null;
  let adjacentVisuals = [];
  let adjacencyNames = { parent: "", primaryChild: "" };
  let overlay = null;
  let overlaySignature = "";
  let worldEvidenceSignature = "";
  const startLabel = document.createElement("span");
  startLabel.className = "hurtbox-binding-endpoint-label binding-start-label";
  startLabel.textContent = "起";
  startLabel.setAttribute("aria-hidden", "true");
  host.append(startLabel);
  const endLabel = document.createElement("span");
  endLabel.className = "hurtbox-binding-endpoint-label binding-end-label";
  endLabel.textContent = "终";
  endLabel.setAttribute("aria-hidden", "true");
  host.append(endLabel);

  const screenWorldSize = (position, pixels) => {
    const viewportHeight = Math.max(1, renderer.domElement.clientHeight || host.clientHeight || 1);
    const distance = Math.max(0.01, camera.position.distanceTo(position));
    return 2 * distance * Math.tan(THREE.MathUtils.degToRad(camera.fov) * 0.5) / viewportHeight * pixels;
  };

  const positionEndpointLabel = (label, position, visible = true) => {
    if (!visible || !position) {
      label.hidden = true;
      return;
    }
    const projected = position.clone().project(camera);
    if (!Number.isFinite(projected.x) || !Number.isFinite(projected.y) || projected.z < -1 || projected.z > 1) {
      label.hidden = true;
      return;
    }
    const canvas = renderer.domElement;
    const width = Math.max(1, canvas.clientWidth || host.clientWidth || 1);
    const height = Math.max(1, canvas.clientHeight || host.clientHeight || 1);
    label.hidden = false;
    label.style.left = `${(canvas.offsetLeft || 0) + (projected.x * 0.5 + 0.5) * width}px`;
    label.style.top = `${(canvas.offsetTop || 0) + (-projected.y * 0.5 + 0.5) * height}px`;
  };

  const clearAdjacency = () => {
    for (const item of adjacentVisuals) {
      item.line.removeFromParent();
      item.marker.removeFromParent();
      item.line.geometry?.dispose?.();
      item.line.material?.dispose?.();
      item.marker.geometry?.dispose?.();
      item.marker.material?.dispose?.();
    }
    adjacentVisuals = [];
    adjacencyNames = { parent: "", primaryChild: "" };
  };

  const rebuildAdjacency = bone => {
    clearAdjacency();
    if (!bone) return;
    const selected = selectHurtboxBindingReferenceNeighbors(THREE, bone);
    const neighbors = [
      selected.parent ? { bone: selected.parent, role: "parent" } : null,
      selected.primaryChild ? { bone: selected.primaryChild, role: "primaryChild" } : null
    ].filter(Boolean);
    adjacencyNames = {
      parent: String(selected.parent?.name || ""),
      primaryChild: String(selected.primaryChild?.name || "")
    };
    for (const neighbor of neighbors) {
      const isPrimaryChild = neighbor.role === "primaryChild";
      const line = new THREE.Line(
        new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(), new THREE.Vector3()]),
        new THREE.LineBasicMaterial({
          color: isPrimaryChild ? 0xffd166 : 0x568b94,
          transparent: true,
          opacity: isPrimaryChild ? 0.92 : 0.28,
          depthTest: false,
          depthWrite: false
        }));
      line.renderOrder = 908;
      line.userData.hurtboxBindingHelper = true;
      const marker = new THREE.Mesh(
        isPrimaryChild
          ? new THREE.OctahedronGeometry(0.5, 0)
          : new THREE.SphereGeometry(0.5, 10, 7),
        new THREE.MeshBasicMaterial({
          color: isPrimaryChild ? 0xffd166 : 0x4e747b,
          transparent: true,
          opacity: isPrimaryChild ? 0.98 : 0.42,
          depthTest: false,
          depthWrite: false
        }));
      marker.name = isPrimaryChild ? "HurtboxBindingBoneEnd" : "HurtboxBindingBoneParent";
      marker.renderOrder = 909;
      marker.userData.hurtboxBindingHelper = true;
      adjacencyGroup.add(line, marker);
      adjacentVisuals.push({ bone: neighbor.bone, role: neighbor.role, line, marker });
    }
  };

  const removeOverlay = () => {
    overlay?.remove?.();
    overlay = null;
    overlaySignature = "";
    worldEvidenceSignature = "";
    if (host?.dataset) {
      delete host.dataset.hurtboxBindingReference;
      delete host.dataset.hurtboxBindingReferenceWorld;
    }
  };

  const ensureOverlay = () => {
    if (overlay?.isConnected) return;
    overlay = document.createElement("aside");
    overlay.className = "hurtbox-binding-reference-overlay";
    overlay.setAttribute("aria-live", "polite");
    host.append(overlay);
  };

  const updateOverlay = () => {
    if (!activeTarget?.collider || !activeStatus) return;
    ensureOverlay();
    const collider = activeTarget.collider;
    const pose = collider.localPose || {};
    const payload = {
      colliderId: collider.colliderId || "",
      partId: collider.partId || "",
      parentKind: activeStatus.parentKind,
      requestedPath: activeStatus.requestedPath,
      resolvedPath: activeStatus.resolvedPath,
      resolved: activeStatus.resolved,
      reason: activeStatus.reason,
      parentBone: adjacencyNames.parent,
      primaryChildBone: adjacencyNames.primaryChild,
      localPosition: bindingReferenceVector(pose.position),
      localEuler: bindingReferenceVector(pose.eulerHint)
    };
    const signature = JSON.stringify(payload);
    if (signature === overlaySignature) return;
    overlaySignature = signature;
    host.dataset.hurtboxBindingReference = signature;
    const statusClass = activeStatus.resolved ? "binding-status-ok" : "binding-status-error";
    const resolvedBoneName = String(payload.resolvedPath || payload.requestedPath || payload.parentKind)
      .split(/[\\/]/)
      .filter(Boolean)
      .pop() || payload.parentKind;
    const chain = [payload.parentBone, resolvedBoneName, payload.primaryChildBone]
      .filter(Boolean)
      .join(" → ");
    overlay.innerHTML = `<header><strong>${escapeHtml(payload.colliderId || "collider")}</strong><span>${escapeHtml(payload.partId || "未指定部位")}</span><span class="${statusClass}">${activeStatus.resolved ? "绑定已解析" : "绑定失败"}</span></header><span class="binding-label">绑定骨骼</span><span class="binding-value">${escapeHtml(resolvedBoneName)}</span>${chain ? `<span class="binding-label">骨链方向</span><span class="binding-value">${escapeHtml(chain)}</span>` : ""}<span class="binding-label">局部位置</span><span class="binding-value">${escapeHtml(formatBindingReferenceVector(payload.localPosition, "m"))}</span><span class="binding-label">局部旋转</span><span class="binding-value">${escapeHtml(formatBindingReferenceVector(payload.localEuler, "°"))}</span><details class="binding-reference-paths"><summary>完整绑定路径</summary><span>请求</span><code>${escapeHtml(payload.requestedPath || "（无）")}</code><span>解析</span><code>${escapeHtml(payload.resolvedPath || payload.reason)}</code></details><span class="binding-reference-legend"><i class="legend-start"></i>青白：骨骼起点 <i class="legend-end"></i>黄色：骨骼终点 <i class="legend-parent"></i>灰色：父级 <i class="legend-offset"></i>虚线：受击体偏移</span>`;
  };

  const hideVisuals = () => {
    boneMarker.visible = false;
    boneMarkerCore.visible = false;
    connector.visible = false;
    adjacencyGroup.visible = false;
    startLabel.hidden = true;
    endLabel.hidden = true;
  };

  const sync = () => {
    const target = getActiveViewportTransformTarget();
    const shouldShow = state.viewportMode === "character"
      && Boolean(state.hurtboxBindingReferenceVisible)
      && target?.transformKind === "collider";
    if (!shouldShow) {
      activeTarget = null;
      activeStatus = null;
      activeBone = null;
      group.visible = false;
      clearAdjacency();
      removeOverlay();
      return;
    }

    activeTarget = target;
    const boneParent = target.object?.parent?.userData?.boneParent || null;
    activeStatus = getHurtboxBindingReferenceStatus(target.collider, boneParent);
    const nextBone = activeStatus.resolved && activeStatus.parentKind === "Bone" ? boneParent?.bone || null : null;
    if (nextBone !== activeBone) {
      activeBone = nextBone;
      rebuildAdjacency(activeBone);
    }
    group.visible = true;
    updateOverlay();
    updateVisuals();
  };

  const updateVisuals = () => {
    if (!group.visible || !activeTarget?.object || !activeTarget?.collider || !activeStatus) return;
    const parent = activeTarget.object.parent;
    if (!parent || !activeStatus.resolved) {
      hideVisuals();
      updateOverlay();
      return;
    }

    parent.updateWorldMatrix?.(true, false);
    activeTarget.object.updateWorldMatrix?.(true, false);
    connector.visible = true;
    adjacencyGroup.visible = Boolean(activeBone);

    const matrices = computeHurtboxBindingReferenceMatrices(
      THREE,
      parent.matrixWorld,
      activeTarget.collider.localPose,
      activeTarget.poseAdapter);
    const referencePosition = new THREE.Vector3();
    referencePosition.setFromMatrixPosition(matrices.referenceMatrix);

    const finalPosition = activeTarget.object.getWorldPosition(new THREE.Vector3());

    if (activeBone) {
      const bonePosition = activeBone.getWorldPosition(new THREE.Vector3());
      boneMarker.visible = true;
      boneMarker.position.copy(bonePosition);
      boneMarker.scale.setScalar(screenWorldSize(bonePosition, 12));
      boneMarkerCore.visible = true;
      boneMarkerCore.position.copy(bonePosition);
      boneMarkerCore.scale.setScalar(screenWorldSize(bonePosition, 4));
      positionEndpointLabel(startLabel, bonePosition);
      updateBindingReferenceLine(connector, bonePosition, finalPosition, true);
      let primaryChildPosition = null;
      for (const item of adjacentVisuals) {
        const neighborPosition = item.bone.getWorldPosition(new THREE.Vector3());
        updateBindingReferenceLine(item.line, bonePosition, neighborPosition, false);
        item.marker.position.copy(neighborPosition);
        item.marker.scale.setScalar(screenWorldSize(neighborPosition, item.role === "primaryChild" ? 12 : 5));
        if (item.role === "primaryChild") primaryChildPosition = neighborPosition;
      }
      positionEndpointLabel(endLabel, primaryChildPosition, Boolean(primaryChildPosition));
      updateWorldEvidence(referencePosition, finalPosition, bonePosition);
    } else {
      boneMarker.visible = false;
      boneMarkerCore.visible = false;
      startLabel.hidden = true;
      endLabel.hidden = true;
      updateBindingReferenceLine(connector, referencePosition, finalPosition, true);
      updateWorldEvidence(referencePosition, finalPosition, null);
    }
    updateOverlay();
  };

  const updateWorldEvidence = (referencePosition, finalPosition, rawBonePosition) => {
    if (!host?.dataset || !activeTarget?.collider || !activeStatus) return;
    const payload = {
      colliderId: activeTarget.collider.colliderId || "",
      resolved: activeStatus.resolved,
      referenceWorld: bindingReferenceRoundedVector(referencePosition),
      finalWorld: bindingReferenceRoundedVector(finalPosition),
      rawBoneWorld: rawBonePosition ? bindingReferenceRoundedVector(rawBonePosition) : null,
      adjacentBoneCount: adjacentVisuals.length,
      parentBone: adjacencyNames.parent,
      primaryChildBone: adjacencyNames.primaryChild
    };
    const signature = JSON.stringify(payload);
    if (signature === worldEvidenceSignature) return;
    worldEvidenceSignature = signature;
    host.dataset.hurtboxBindingReferenceWorld = signature;
  };

  const dispose = () => {
    removeOverlay();
    startLabel.remove();
    endLabel.remove();
    clearAdjacency();
    group.traverse(object => {
      object.geometry?.dispose?.();
      if (Array.isArray(object.material)) object.material.forEach(material => material?.dispose?.());
      else object.material?.dispose?.();
    });
    group.removeFromParent();
  };

  hideVisuals();
  return { sync, updateVisuals, dispose };
}

function getHurtboxBindingReferenceGeometrySignature(collider = {}) {
  return JSON.stringify({
    shape: String(collider.shape || "Capsule"),
    size: bindingReferenceVector(collider.size),
    radius: numberOrDefault(collider.radius, 0),
    height: numberOrDefault(collider.height, 0)
  });
}

function bindingReferenceVector(value = {}) {
  return {
    x: numberOrDefault(value?.x, 0),
    y: numberOrDefault(value?.y, 0),
    z: numberOrDefault(value?.z, 0)
  };
}

function bindingReferenceRoundedVector(value = {}) {
  return {
    x: Number(numberOrDefault(value?.x, 0).toFixed(5)),
    y: Number(numberOrDefault(value?.y, 0).toFixed(5)),
    z: Number(numberOrDefault(value?.z, 0).toFixed(5))
  };
}

function formatBindingReferenceVector(value = {}, suffix = "") {
  const format = component => `${Number(numberOrDefault(component, 0).toFixed(3))}${suffix}`;
  return `X ${format(value.x)} / Y ${format(value.y)} / Z ${format(value.z)}`;
}

function updateBindingReferenceLine(line, start, end, dashed = false) {
  const positions = line.geometry?.attributes?.position;
  if (!positions) return;
  positions.setXYZ(0, start.x, start.y, start.z);
  positions.setXYZ(1, end.x, end.y, end.z);
  positions.needsUpdate = true;
  line.geometry.computeBoundingSphere?.();
  if (dashed) line.computeLineDistances?.();
}

function getHurtboxShapeSymmetryField(collider, descriptor) {
  const shape = String(collider?.shape || "Capsule").toLowerCase();
  if (shape === "box") return "size";
  if (shape === "capsule" && descriptor?.kind === "height") return "height";
  return "radius";
}

function restoreColliderFromSnapshot(target, snapshot) {
  if (!target?.collider || !snapshot) return;
  for (const key of Object.keys(target.collider)) delete target.collider[key];
  Object.assign(target.collider, cloneHurtboxValue(snapshot));
  target.pose = target.collider.localPose;
  target.transformStart = null;
}

function cloneHurtboxValue(value) {
  return value === undefined ? undefined : JSON.parse(JSON.stringify(value));
}

function describeHurtboxShapeDimensions(collider = {}) {
  const shape = String(collider.shape || "Capsule");
  if (shape === "Box") {
    return `Box ${formatCompactMeters(collider.size?.x)} × ${formatCompactMeters(collider.size?.y)} × ${formatCompactMeters(collider.size?.z)}`;
  }
  if (shape === "Sphere") return `Sphere R ${formatCompactMeters(collider.radius)}`;
  return `Capsule R ${formatCompactMeters(collider.radius)} / H ${formatCompactMeters(collider.height)}`;
}

function formatCompactMeters(value) {
  const number = Number(value);
  return `${Number.isFinite(number) ? Number(number.toFixed(3)) : 0}m`;
}

function formatSignedMeters(value) {
  const number = Number(value);
  if (!Number.isFinite(number)) return "0m";
  const rounded = Number(number.toFixed(3));
  return `${rounded > 0 ? "+" : ""}${rounded}m`;
}

function syncColliderInspectorLiveFields(collider = {}) {
  if (!el.inspector) return;
  const values = {
    "localPose.position.x": collider.localPose?.position?.x,
    "localPose.position.y": collider.localPose?.position?.y,
    "localPose.position.z": collider.localPose?.position?.z,
    "localPose.scale.x": 1,
    "localPose.scale.y": 1,
    "localPose.scale.z": 1,
    "size.x": collider.size?.x,
    "size.y": collider.size?.y,
    "size.z": collider.size?.z,
    radius: collider.radius,
    height: collider.height
  };
  el.inspector.querySelectorAll("[data-field]").forEach(input => {
    const value = values[input.dataset.field];
    if (value !== undefined && Number.isFinite(Number(value))) input.value = String(roundTransformNumber(Number(value)));
  });
  const summary = el.inspector.querySelector("[data-collider-live-dimensions]");
  if (summary) summary.textContent = describeHurtboxShapeDimensions(collider);
}

function addHurtboxCoverageHeatmap(THREE, host, bodyPreviewRoot, content, renderer, scene) {
  const bodyMeshes = [];
  const hurtboxMeshes = [];
  bodyPreviewRoot.traverse(object => {
    if (object?.isMesh && object.visible && !findHurtboxOwner(object)) bodyMeshes.push(object);
  });
  content.traverse(object => {
    if (object?.isMesh && object.visible && object.userData?.hurtbox) hurtboxMeshes.push(object);
  });
  if (bodyMeshes.length === 0 || hurtboxMeshes.length === 0) return;

  bodyPreviewRoot.updateMatrixWorld(true);
  content.updateMatrixWorld(true);
  const bounds = new THREE.Box3();
  const scratch = new THREE.Box3();
  for (const mesh of bodyMeshes) {
    scratch.setFromObject(mesh);
    if (!scratch.isEmpty()) bounds.union(scratch);
  }
  for (const mesh of hurtboxMeshes) {
    scratch.setFromObject(mesh);
    if (!scratch.isEmpty()) bounds.union(scratch);
  }
  if (bounds.isEmpty()) return;

  const size = bounds.getSize(new THREE.Vector3());
  const margin = Math.max(size.x, size.y, size.z, 1) * 0.08;
  bounds.expandByScalar(margin);
  const directions = [
    coverageDirection("前", "z", bounds.max.z + margin, new THREE.Vector3(0, 0, -1), "x", "y", bounds),
    coverageDirection("后", "z", bounds.min.z - margin, new THREE.Vector3(0, 0, 1), "x", "y", bounds, true),
    coverageDirection("左", "x", bounds.min.x - margin, new THREE.Vector3(1, 0, 0), "z", "y", bounds),
    coverageDirection("右", "x", bounds.max.x + margin, new THREE.Vector3(-1, 0, 0), "z", "y", bounds, true),
    coverageDirection("上", "y", bounds.max.y + margin, new THREE.Vector3(0, -1, 0), "x", "z", bounds),
    coverageDirection("下", "y", bounds.min.y - margin, new THREE.Vector3(0, 1, 0), "x", "z", bounds, true)
  ];
  const resolution = 24;
  const raycaster = new THREE.Raycaster();
  raycaster.near = 0;
  raycaster.far = Math.max(size.x, size.y, size.z, 1) * 3 + margin * 2;
  const bodyHits = [];
  const hurtboxHits = [];
  const results = directions.map(view => ({ label: view.label, pixels: new Uint8Array(resolution * resolution) }));
  let bodySampleCount = 0;
  let hurtboxSampleCount = 0;
  let falseNegativeCount = 0;
  let falsePositiveCount = 0;
  const depthErrors = [];

  runHurtboxCoverageHeatmapGpuAsync(
    THREE,
    host,
    directions,
    resolution,
    renderer,
    scene,
    bounds,
    bodyMeshes,
    hurtboxMeshes);
  return;

  for (const view of directions) {
    const pixels = new Uint8Array(resolution * resolution);
    for (let y = 0; y < resolution; y++) {
      for (let x = 0; x < resolution; x++) {
        const u = (x + 0.5) / resolution;
        const v = (y + 0.5) / resolution;
        const origin = new THREE.Vector3();
        origin[view.axis] = view.origin;
        origin[view.uAxis] = THREE.MathUtils.lerp(view.uMin, view.uMax, view.flipU ? 1 - u : u);
        origin[view.vAxis] = THREE.MathUtils.lerp(view.vMin, view.vMax, 1 - v);
        raycaster.set(origin, view.direction);
        bodyHits.length = 0;
        hurtboxHits.length = 0;
        raycaster.intersectObjects(bodyMeshes, false, bodyHits);
        raycaster.intersectObjects(hurtboxMeshes, false, hurtboxHits);
        const modelHit = bodyHits.length > 0;
        const hurtboxHit = hurtboxHits.length > 0;
        const index = y * resolution + x;
        if (modelHit) bodySampleCount++;
        if (hurtboxHit) hurtboxSampleCount++;
        if (modelHit && hurtboxHit) {
          pixels[index] = 1;
          depthErrors.push(Math.abs(bodyHits[0].distance - hurtboxHits[0].distance));
        } else if (modelHit) {
          pixels[index] = 2;
          falseNegativeCount++;
        } else if (hurtboxHit) {
          pixels[index] = 3;
          falsePositiveCount++;
        }
      }
    }
    results.push({ label: view.label, pixels });
  }

  depthErrors.sort((left, right) => left - right);
  const falseNegativeRate = bodySampleCount > 0 ? falseNegativeCount * 100 / bodySampleCount : 0;
  const falsePositiveRate = hurtboxSampleCount > 0 ? falsePositiveCount * 100 / hurtboxSampleCount : 0;
  const p95Index = depthErrors.length > 0 ? Math.min(depthErrors.length - 1, Math.ceil(depthErrors.length * 0.95) - 1) : 0;
  const p95 = depthErrors.length > 0 ? depthErrors[p95Index] : 0;
  const tone = falseNegativeRate <= 0 && falsePositiveRate <= 5 && p95 <= 0.15
    ? "ok"
    : falseNegativeRate <= 0 && falsePositiveRate <= 10 && p95 <= 0.3 ? "warn" : "error";
  const overlay = document.createElement("section");
  overlay.className = "hurtbox-coverage-overlay";
  overlay.innerHTML = `<div class="hurtbox-coverage-head"><strong>当前动画帧 · 六向 Hurtbox 覆盖</strong><span class="hurtbox-coverage-summary ${tone}">FN ${falseNegativeRate.toFixed(1)}% / FP ${falsePositiveRate.toFixed(1)}% / 深度 P95 ${p95.toFixed(3)}m</span></div><div class="hurtbox-coverage-grid">${results.map((result, index) => `<div class="hurtbox-coverage-view"><canvas width="${resolution}" height="${resolution}" data-coverage-index="${index}"></canvas><span>${result.label}</span></div>`).join("")}</div><div class="hurtbox-coverage-legend"><span class="ok">■ 覆盖</span><span class="fn">■ 空洞</span><span class="fp">■ 外扩</span></div>`;
  host.append(overlay);
  for (let i = 0; i < results.length; i++) {
    const canvas = overlay.querySelector(`canvas[data-coverage-index="${i}"]`);
    const context = canvas?.getContext("2d");
    if (!context) continue;
    const image = context.createImageData(resolution, resolution);
    for (let pixel = 0; pixel < results[i].pixels.length; pixel++) {
      const color = coveragePixelColor(results[i].pixels[pixel]);
      const offset = pixel * 4;
      image.data[offset] = color[0];
      image.data[offset + 1] = color[1];
      image.data[offset + 2] = color[2];
      image.data[offset + 3] = color[3];
    }
    context.putImageData(image, 0, 0);
  }
  host.dataset.hurtboxCoverage = JSON.stringify({
    animation: state.previewMotion,
    falseNegativeRate: Number(falseNegativeRate.toFixed(3)),
    falsePositiveRate: Number(falsePositiveRate.toFixed(3)),
    firstHitDepthP95Meters: Number(p95.toFixed(4)),
    bodySampleCount,
    hurtboxSampleCount,
    status: tone
  });
}

function runHurtboxCoverageHeatmapGpuAsync(
  THREE,
  host,
  directions,
  resolution,
  renderer,
  scene,
  bounds,
  bodyMeshes,
  hurtboxMeshes) {
  const bodyLayer = 30;
  const hurtboxLayer = 29;
  const target = new THREE.WebGLRenderTarget(resolution, resolution, {
    depthBuffer: true,
    stencilBuffer: false
  });
  const depthMaterial = new THREE.MeshDepthMaterial({ depthPacking: THREE.RGBADepthPacking });
  const bodyPixels = new Uint8Array(resolution * resolution * 4);
  const hurtboxPixels = new Uint8Array(resolution * resolution * 4);
  const results = directions.map(view => ({ label: view.label, pixels: new Uint8Array(resolution * resolution) }));
  const center = bounds.getCenter(new THREE.Vector3());
  const size = bounds.getSize(new THREE.Vector3());
  const camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.01, Math.max(size.x, size.y, size.z, 1) * 4);
  const depthErrors = [];
  let bodySampleCount = 0;
  let hurtboxSampleCount = 0;
  let falseNegativeCount = 0;
  let falsePositiveCount = 0;
  let step = 0;

  const overlay = document.createElement("section");
  overlay.className = "hurtbox-coverage-overlay";
  overlay.innerHTML = `<div class="hurtbox-coverage-head"><strong>当前动画帧 · 六向 Hurtbox 覆盖</strong><span class="hurtbox-coverage-summary">正在采样 0 / ${directions.length * 2}</span></div><div class="hurtbox-coverage-grid">${results.map((result, index) => `<div class="hurtbox-coverage-view"><canvas width="${resolution}" height="${resolution}" data-coverage-index="${index}"></canvas><span>${result.label}</span></div>`).join("")}</div><div class="hurtbox-coverage-legend"><span class="ok">■ 覆盖</span><span class="fn">■ 空洞</span><span class="fp">■ 外扩</span></div>`;
  host.append(overlay);
  host.dataset.hurtboxCoverage = JSON.stringify({
    animation: state.previewMotion,
    status: "sampling",
    completedPasses: 0,
    totalPasses: directions.length * 2
  });

  const dispose = () => {
    depthMaterial.dispose();
    target.dispose();
  };
  const renderMask = (meshes, layer, pixels) => {
    const savedLayers = new Map();
    const savedMaterials = new Map();
    const temporaryMaterials = [];
    const savedTarget = renderer.getRenderTarget();
    const savedOverride = scene.overrideMaterial;
    const savedBackground = scene.background;
    const savedAutoClear = renderer.autoClear;
    const savedClearColor = renderer.getClearColor(new THREE.Color()).clone();
    const savedClearAlpha = renderer.getClearAlpha();
    try {
      for (const mesh of meshes) {
        savedLayers.set(mesh, mesh.layers.mask);
        savedMaterials.set(mesh, mesh.material);
        mesh.layers.set(layer);
        if (layer === bodyLayer) {
          const sources = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
          const replacements = sources.map(source => {
            const material = new THREE.MeshDepthMaterial({
              depthPacking: THREE.RGBADepthPacking,
              map: source?.map || null,
              alphaMap: source?.alphaMap || null,
              alphaTest: Number(source?.alphaTest || 0),
              side: source?.side ?? THREE.FrontSide
            });
            temporaryMaterials.push(material);
            return material;
          });
          mesh.material = Array.isArray(mesh.material) ? replacements : replacements[0];
        } else {
          mesh.material = depthMaterial;
        }
      }
      camera.layers.set(layer);
      scene.overrideMaterial = null;
      scene.background = null;
      renderer.autoClear = true;
      renderer.setClearColor(0xffffff, 1);
      renderer.setRenderTarget(target);
      renderer.clear(true, true, true);
      renderer.render(scene, camera);
      renderer.readRenderTargetPixels(target, 0, 0, resolution, resolution, pixels);
    } finally {
      for (const [mesh, mask] of savedLayers) mesh.layers.mask = mask;
      for (const [mesh, material] of savedMaterials) mesh.material = material;
      for (const material of temporaryMaterials) material.dispose();
      scene.overrideMaterial = savedOverride;
      scene.background = savedBackground;
      renderer.autoClear = savedAutoClear;
      renderer.setClearColor(savedClearColor, savedClearAlpha);
      renderer.setRenderTarget(savedTarget);
    }
  };
  const finish = () => {
    depthErrors.sort((left, right) => left - right);
    const falseNegativeRate = bodySampleCount > 0 ? falseNegativeCount * 100 / bodySampleCount : 0;
    const falsePositiveRate = hurtboxSampleCount > 0 ? falsePositiveCount * 100 / hurtboxSampleCount : 0;
    const p95Index = depthErrors.length > 0 ? Math.min(depthErrors.length - 1, Math.ceil(depthErrors.length * 0.95) - 1) : 0;
    const p95 = depthErrors.length > 0 ? depthErrors[p95Index] : 0;
    const tone = falseNegativeRate <= 0 && falsePositiveRate <= 5 && p95 <= 0.15
      ? "ok"
      : falseNegativeRate <= 0 && falsePositiveRate <= 10 && p95 <= 0.3 ? "warn" : "error";
    const summary = overlay.querySelector(".hurtbox-coverage-summary");
    if (summary) {
      summary.className = `hurtbox-coverage-summary ${tone}`;
      summary.textContent = `FN ${falseNegativeRate.toFixed(1)}% / FP ${falsePositiveRate.toFixed(1)}% / 深度 P95 ${p95.toFixed(3)}m`;
    }
    for (let i = 0; i < results.length; i++) {
      const canvas = overlay.querySelector(`canvas[data-coverage-index="${i}"]`);
      const context = canvas?.getContext("2d");
      if (!context) continue;
      const image = context.createImageData(resolution, resolution);
      for (let pixel = 0; pixel < results[i].pixels.length; pixel++) {
        const color = coveragePixelColor(results[i].pixels[pixel]);
        const sourceY = resolution - 1 - Math.floor(pixel / resolution);
        const targetPixel = sourceY * resolution + (pixel % resolution);
        const offset = targetPixel * 4;
        image.data[offset] = color[0];
        image.data[offset + 1] = color[1];
        image.data[offset + 2] = color[2];
        image.data[offset + 3] = color[3];
      }
      context.putImageData(image, 0, 0);
    }
    const measurement = {
      animation: state.previewMotion,
      falseNegativeRate: Number(falseNegativeRate.toFixed(3)),
      falsePositiveRate: Number(falsePositiveRate.toFixed(3)),
      firstHitDepthP95Meters: Number(p95.toFixed(4)),
      bodySampleCount,
      hurtboxSampleCount,
      status: tone
    };
    host.dataset.hurtboxCoverage = JSON.stringify(measurement);
    dispose();
  };
  const processPass = () => {
    if (!host.isConnected || !overlay.isConnected || !state.hurtboxCoverageVisible) {
      dispose();
      return;
    }
    const viewIndex = Math.floor(step / 2);
    const view = directions[viewIndex];
    const halfWidth = Math.max((view.uMax - view.uMin) * 0.5, 0.001);
    const halfHeight = Math.max((view.vMax - view.vMin) * 0.5, 0.001);
    camera.left = -halfWidth;
    camera.right = halfWidth;
    camera.bottom = -halfHeight;
    camera.top = halfHeight;
    camera.position.copy(center);
    camera.position[view.axis] = view.origin;
    camera.up.set(0, 1, 0);
    if (view.axis === "y") camera.up.set(0, 0, view.direction.y < 0 ? -1 : 1);
    camera.lookAt(center);
    camera.updateProjectionMatrix();
    camera.updateMatrixWorld(true);

    if ((step & 1) === 0) {
      renderMask(bodyMeshes, bodyLayer, bodyPixels);
    } else {
      renderMask(hurtboxMeshes, hurtboxLayer, hurtboxPixels);
      const pixels = results[viewIndex].pixels;
      for (let pixel = 0; pixel < pixels.length; pixel++) {
        const offset = pixel * 4;
        const modelHit = !isCoverageDepthClear(bodyPixels, offset);
        const hurtboxHit = !isCoverageDepthClear(hurtboxPixels, offset);
        if (modelHit) bodySampleCount++;
        if (hurtboxHit) hurtboxSampleCount++;
        if (modelHit && hurtboxHit) {
          pixels[pixel] = 1;
          depthErrors.push(Math.abs(unpackCoverageDepth(bodyPixels, offset) - unpackCoverageDepth(hurtboxPixels, offset)) * (camera.far - camera.near));
        } else if (modelHit) {
          pixels[pixel] = 2;
          falseNegativeCount++;
        } else if (hurtboxHit) {
          pixels[pixel] = 3;
          falsePositiveCount++;
        }
      }
    }
    step++;
    const summary = overlay.querySelector(".hurtbox-coverage-summary");
    if (summary) summary.textContent = `正在采样 ${step} / ${directions.length * 2}`;
    host.dataset.hurtboxCoverage = JSON.stringify({
      animation: state.previewMotion,
      status: "sampling",
      completedPasses: step,
      totalPasses: directions.length * 2
    });
    if (step >= directions.length * 2) finish();
    else requestAnimationFrame(processPass);
  };
  requestAnimationFrame(processPass);
}

async function confirmHurtboxAuthoring() {
  if (state.dirty) {
    state.message = "当前碰撞框仍有未保存修改；请先保存，再确认当前碰撞体。";
    renderShellStatus();
    return false;
  }
  const response = await fetch(`/api/character/hurtbox-fit-review?package=${encodeURIComponent(state.packageRelative)}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ acceptedAsAuthored: true, measurementStatus: "NotRequired" })
  });
  let result = null;
  try { result = await response.json(); } catch { result = null; }
  const draft = state.package?.hurtboxFitDraft;
  if (response.ok && result?.passed && draft) {
    const quality = draft.quality || (draft.quality = {});
    quality.falsePositiveStatus = "AcceptedAsAuthored";
    quality.acceptedAsAuthored = true;
    quality.measurementStatus = "NotRequired";
    draft.authoritativeReviewStatus = "Confirmed";
  }
  state.message = response.ok && result?.passed
    ? "已人工确认当前保存的 body_colliders.json；自动覆盖检测不参与导入门禁。"
    : `碰撞体人工确认失败：${result?.message || response.statusText || "未知错误"}`;
  renderShellStatus();
  return Boolean(response.ok && result?.passed);
}

function runHurtboxCoverageHeatmapGpu(
  THREE,
  host,
  directions,
  resolution,
  renderer,
  scene,
  bounds,
  bodyMeshes,
  hurtboxMeshes) {
  const bodyLayer = 30;
  const hurtboxLayer = 29;
  const savedLayers = new Map();
  const target = new THREE.WebGLRenderTarget(resolution, resolution, {
    depthBuffer: true,
    stencilBuffer: false
  });
  const depthMaterial = new THREE.MeshDepthMaterial({ depthPacking: THREE.RGBADepthPacking });
  const bodyPixels = new Uint8Array(resolution * resolution * 4);
  const hurtboxPixels = new Uint8Array(resolution * resolution * 4);
  const results = [];
  const center = bounds.getCenter(new THREE.Vector3());
  const size = bounds.getSize(new THREE.Vector3());
  const camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.01, Math.max(size.x, size.y, size.z, 1) * 4);
  const savedTarget = renderer.getRenderTarget();
  const savedOverride = scene.overrideMaterial;
  const savedAutoClear = renderer.autoClear;
  const savedClearColor = renderer.getClearColor(new THREE.Color()).clone();
  const savedClearAlpha = renderer.getClearAlpha();
  let bodySampleCount = 0;
  let hurtboxSampleCount = 0;
  let falseNegativeCount = 0;
  let falsePositiveCount = 0;
  const depthErrors = [];

  try {
    for (const mesh of bodyMeshes) {
      savedLayers.set(mesh, mesh.layers.mask);
      mesh.layers.set(bodyLayer);
    }
    for (const mesh of hurtboxMeshes) {
      savedLayers.set(mesh, mesh.layers.mask);
      mesh.layers.set(hurtboxLayer);
    }
    scene.overrideMaterial = depthMaterial;
    renderer.autoClear = true;
    renderer.setClearColor(0xffffff, 1);
    renderer.setRenderTarget(target);

    for (const view of directions) {
      const halfWidth = Math.max((view.uMax - view.uMin) * 0.5, 0.001);
      const halfHeight = Math.max((view.vMax - view.vMin) * 0.5, 0.001);
      camera.left = -halfWidth;
      camera.right = halfWidth;
      camera.bottom = -halfHeight;
      camera.top = halfHeight;
      camera.position.copy(center);
      camera.position[view.axis] = view.origin;
      camera.up.set(0, 1, 0);
      if (view.axis === "y") camera.up.set(0, 0, view.direction.y < 0 ? -1 : 1);
      camera.lookAt(center);
      camera.updateProjectionMatrix();
      camera.updateMatrixWorld(true);

      camera.layers.set(bodyLayer);
      renderer.clear(true, true, true);
      renderer.render(scene, camera);
      renderer.readRenderTargetPixels(target, 0, 0, resolution, resolution, bodyPixels);

      camera.layers.set(hurtboxLayer);
      renderer.clear(true, true, true);
      renderer.render(scene, camera);
      renderer.readRenderTargetPixels(target, 0, 0, resolution, resolution, hurtboxPixels);

      const pixels = new Uint8Array(resolution * resolution);
      for (let pixel = 0; pixel < pixels.length; pixel++) {
        const offset = pixel * 4;
        const modelHit = !isCoverageDepthClear(bodyPixels, offset);
        const hurtboxHit = !isCoverageDepthClear(hurtboxPixels, offset);
        if (modelHit) bodySampleCount++;
        if (hurtboxHit) hurtboxSampleCount++;
        if (modelHit && hurtboxHit) {
          pixels[pixel] = 1;
          const bodyDepth = unpackCoverageDepth(bodyPixels, offset);
          const hurtboxDepth = unpackCoverageDepth(hurtboxPixels, offset);
          depthErrors.push(Math.abs(bodyDepth - hurtboxDepth) * (camera.far - camera.near));
        } else if (modelHit) {
          pixels[pixel] = 2;
          falseNegativeCount++;
        } else if (hurtboxHit) {
          pixels[pixel] = 3;
          falsePositiveCount++;
        }
      }
      results.push({ label: view.label, pixels });
    }
  } finally {
    for (const [mesh, mask] of savedLayers) mesh.layers.mask = mask;
    scene.overrideMaterial = savedOverride;
    renderer.autoClear = savedAutoClear;
    renderer.setClearColor(savedClearColor, savedClearAlpha);
    renderer.setRenderTarget(savedTarget);
    depthMaterial.dispose();
    target.dispose();
  }

  depthErrors.sort((left, right) => left - right);
  const falseNegativeRate = bodySampleCount > 0 ? falseNegativeCount * 100 / bodySampleCount : 0;
  const falsePositiveRate = hurtboxSampleCount > 0 ? falsePositiveCount * 100 / hurtboxSampleCount : 0;
  const p95Index = depthErrors.length > 0 ? Math.min(depthErrors.length - 1, Math.ceil(depthErrors.length * 0.95) - 1) : 0;
  const p95 = depthErrors.length > 0 ? depthErrors[p95Index] : 0;
  const tone = falseNegativeRate <= 0 && falsePositiveRate <= 5 && p95 <= 0.15
    ? "ok"
    : falseNegativeRate <= 0 && falsePositiveRate <= 10 && p95 <= 0.3 ? "warn" : "error";
  const overlay = document.createElement("section");
  overlay.className = "hurtbox-coverage-overlay";
  overlay.innerHTML = `<div class="hurtbox-coverage-head"><strong>当前动画帧 · 六向 Hurtbox 覆盖</strong><span class="hurtbox-coverage-summary ${tone}">FN ${falseNegativeRate.toFixed(1)}% / FP ${falsePositiveRate.toFixed(1)}% / 深度 P95 ${p95.toFixed(3)}m</span></div><div class="hurtbox-coverage-grid">${results.map((result, index) => `<div class="hurtbox-coverage-view"><canvas width="${resolution}" height="${resolution}" data-coverage-index="${index}"></canvas><span>${result.label}</span></div>`).join("")}</div><div class="hurtbox-coverage-legend"><span class="ok">■ 覆盖</span><span class="fn">■ 空洞</span><span class="fp">■ 外扩</span></div>`;
  host.append(overlay);
  for (let i = 0; i < results.length; i++) {
    const canvas = overlay.querySelector(`canvas[data-coverage-index="${i}"]`);
    const context = canvas?.getContext("2d");
    if (!context) continue;
    const image = context.createImageData(resolution, resolution);
    for (let pixel = 0; pixel < results[i].pixels.length; pixel++) {
      const color = coveragePixelColor(results[i].pixels[pixel]);
      const sourceY = resolution - 1 - Math.floor(pixel / resolution);
      const targetPixel = sourceY * resolution + (pixel % resolution);
      const offset = targetPixel * 4;
      image.data[offset] = color[0];
      image.data[offset + 1] = color[1];
      image.data[offset + 2] = color[2];
      image.data[offset + 3] = color[3];
    }
    context.putImageData(image, 0, 0);
  }
  host.dataset.hurtboxCoverage = JSON.stringify({
    animation: state.previewMotion,
    falseNegativeRate: Number(falseNegativeRate.toFixed(3)),
    falsePositiveRate: Number(falsePositiveRate.toFixed(3)),
    firstHitDepthP95Meters: Number(p95.toFixed(4)),
    bodySampleCount,
    hurtboxSampleCount,
    status: tone
  });
}

function isCoverageDepthClear(pixels, offset) {
  return pixels[offset] === 255
    && pixels[offset + 1] === 255
    && pixels[offset + 2] === 255
    && pixels[offset + 3] === 255;
}

function unpackCoverageDepth(pixels, offset) {
  return pixels[offset] / (255 * 256 * 256 * 256)
    + pixels[offset + 1] / (255 * 256 * 256)
    + pixels[offset + 2] / (255 * 256)
    + pixels[offset + 3] / 255;
}

function runHurtboxCoverageHeatmapBatched(
  THREE,
  host,
  directions,
  resolution,
  raycaster,
  bodyMeshes,
  hurtboxMeshes) {
  const results = directions.map(view => ({ label: view.label, pixels: new Uint8Array(resolution * resolution) }));
  const bodyHits = [];
  const hurtboxHits = [];
  const depthErrors = [];
  const origin = new THREE.Vector3();
  const totalSamples = directions.length * resolution * resolution;
  let completedSamples = 0;
  let bodySampleCount = 0;
  let hurtboxSampleCount = 0;
  let falseNegativeCount = 0;
  let falsePositiveCount = 0;
  const samplesPerBatch = 1;

  const overlay = document.createElement("section");
  overlay.className = "hurtbox-coverage-overlay";
  overlay.innerHTML = `<div class="hurtbox-coverage-head"><strong>当前动画帧 · 六向 Hurtbox 覆盖</strong><span class="hurtbox-coverage-summary">正在分批采样…</span></div><div class="hurtbox-coverage-grid">${results.map((result, index) => `<div class="hurtbox-coverage-view"><canvas width="${resolution}" height="${resolution}" data-coverage-index="${index}"></canvas><span>${result.label}</span></div>`).join("")}</div><div class="hurtbox-coverage-legend"><span class="ok">■ 覆盖</span><span class="fn">■ 空洞</span><span class="fp">■ 外扩</span></div>`;
  host.append(overlay);
  host.dataset.hurtboxCoverage = JSON.stringify({
    animation: state.previewMotion,
    status: "sampling",
    completedSamples: 0,
    totalSamples
  });

  const processBatch = () => {
    if (!host.isConnected || !overlay.isConnected || !state.hurtboxCoverageVisible) return;
    const batchEnd = Math.min(totalSamples, completedSamples + samplesPerBatch);
    while (completedSamples < batchEnd) {
      const viewIndex = Math.floor(completedSamples / (resolution * resolution));
      const pixelIndex = completedSamples % (resolution * resolution);
      const y = Math.floor(pixelIndex / resolution);
      const x = pixelIndex % resolution;
      const view = directions[viewIndex];
      const u = (x + 0.5) / resolution;
      const v = (y + 0.5) / resolution;
      origin.set(0, 0, 0);
      origin[view.axis] = view.origin;
      origin[view.uAxis] = THREE.MathUtils.lerp(view.uMin, view.uMax, view.flipU ? 1 - u : u);
      origin[view.vAxis] = THREE.MathUtils.lerp(view.vMin, view.vMax, 1 - v);
      raycaster.set(origin, view.direction);
      bodyHits.length = 0;
      hurtboxHits.length = 0;
      raycaster.intersectObjects(bodyMeshes, false, bodyHits);
      raycaster.intersectObjects(hurtboxMeshes, false, hurtboxHits);
      const modelHit = bodyHits.length > 0;
      const hurtboxHit = hurtboxHits.length > 0;
      if (modelHit) bodySampleCount++;
      if (hurtboxHit) hurtboxSampleCount++;
      if (modelHit && hurtboxHit) {
        results[viewIndex].pixels[pixelIndex] = 1;
        depthErrors.push(Math.abs(bodyHits[0].distance - hurtboxHits[0].distance));
      } else if (modelHit) {
        results[viewIndex].pixels[pixelIndex] = 2;
        falseNegativeCount++;
      } else if (hurtboxHit) {
        results[viewIndex].pixels[pixelIndex] = 3;
        falsePositiveCount++;
      }
      completedSamples++;
    }

    const summary = overlay.querySelector(".hurtbox-coverage-summary");
    if (summary) summary.textContent = `采样 ${completedSamples} / ${totalSamples}`;
    host.dataset.hurtboxCoverage = JSON.stringify({
      animation: state.previewMotion,
      status: "sampling",
      completedSamples,
      totalSamples
    });
    if (completedSamples < totalSamples) {
      requestAnimationFrame(processBatch);
      return;
    }

    depthErrors.sort((left, right) => left - right);
    const falseNegativeRate = bodySampleCount > 0 ? falseNegativeCount * 100 / bodySampleCount : 0;
    const falsePositiveRate = hurtboxSampleCount > 0 ? falsePositiveCount * 100 / hurtboxSampleCount : 0;
    const p95Index = depthErrors.length > 0 ? Math.min(depthErrors.length - 1, Math.ceil(depthErrors.length * 0.95) - 1) : 0;
    const p95 = depthErrors.length > 0 ? depthErrors[p95Index] : 0;
    const tone = falseNegativeRate <= 0 && falsePositiveRate <= 5 && p95 <= 0.15
      ? "ok"
      : falseNegativeRate <= 0 && falsePositiveRate <= 10 && p95 <= 0.3 ? "warn" : "error";
    if (summary) {
      summary.className = `hurtbox-coverage-summary ${tone}`;
      summary.textContent = `FN ${falseNegativeRate.toFixed(1)}% / FP ${falsePositiveRate.toFixed(1)}% / 深度 P95 ${p95.toFixed(3)}m`;
    }
    for (let i = 0; i < results.length; i++) {
      const canvas = overlay.querySelector(`canvas[data-coverage-index="${i}"]`);
      const context = canvas?.getContext("2d");
      if (!context) continue;
      const image = context.createImageData(resolution, resolution);
      for (let pixel = 0; pixel < results[i].pixels.length; pixel++) {
        const color = coveragePixelColor(results[i].pixels[pixel]);
        const offset = pixel * 4;
        image.data[offset] = color[0];
        image.data[offset + 1] = color[1];
        image.data[offset + 2] = color[2];
        image.data[offset + 3] = color[3];
      }
      context.putImageData(image, 0, 0);
    }
    host.dataset.hurtboxCoverage = JSON.stringify({
      animation: state.previewMotion,
      falseNegativeRate: Number(falseNegativeRate.toFixed(3)),
      falsePositiveRate: Number(falsePositiveRate.toFixed(3)),
      firstHitDepthP95Meters: Number(p95.toFixed(4)),
      bodySampleCount,
      hurtboxSampleCount,
      status: tone
    });
  };
  requestAnimationFrame(processBatch);
}

function coverageDirection(label, axis, origin, direction, uAxis, vAxis, bounds, flipU = false) {
  return {
    label,
    axis,
    origin,
    direction,
    uAxis,
    vAxis,
    uMin: bounds.min[uAxis],
    uMax: bounds.max[uAxis],
    vMin: bounds.min[vAxis],
    vMax: bounds.max[vAxis],
    flipU
  };
}

function findHurtboxOwner(object) {
  let current = object;
  while (current) {
    if (current.userData?.hurtbox) return current;
    current = current.parent;
  }
  return null;
}

function coveragePixelColor(kind) {
  if (kind === 1) return [68, 215, 156, 210];
  if (kind === 2) return [255, 92, 105, 255];
  if (kind === 3) return [255, 178, 76, 255];
  return [7, 16, 26, 255];
}

function addContactPointMeshes(THREE, content, pickables, contactPoints, sockets = [], bodyBoneRecords = [], socketsById = {}, registerSelectionVisual = null, registerTransformTarget = null, poseAdapter = UNITY_GLTF_PREVIEW_POSE_ADAPTER) {
  for (let i = 0; i < (contactPoints || []).length; i++) {
    const contactPoint = contactPoints[i];
    if (!contactPoint) continue;
    const objectPath = getContactPointObjectPath(contactPoint, i);
    const motionSocket = resolveContactPointMotionSocket(contactPoint, sockets);
    const baseColor = getContactPointColor(contactPoint.activationMode);
    const material = new THREE.MeshBasicMaterial({
      color: baseColor,
      transparent: true,
      opacity: 0.28,
      depthWrite: false
    });
    const outlineMaterial = new THREE.MeshBasicMaterial({
      color: baseColor,
      transparent: true,
      opacity: 0.72,
      wireframe: true,
      depthWrite: false
    });
    const applySelection = selected => {
      const color = selected ? 0xffa11f : baseColor;
      material.color.setHex(color);
      outlineMaterial.color.setHex(color);
      material.opacity = selected ? 0.42 : 0.28;
      outlineMaterial.opacity = selected ? 0.95 : 0.72;
      material.needsUpdate = true;
      outlineMaterial.needsUpdate = true;
    };
    applySelection(state.selectedPath === objectPath);
    registerSelectionVisual?.(objectPath, applySelection);

    const radius = Math.max(0.005, Number(contactPoint.radiusMeters || 0.09));
    const group = new THREE.Group();
    const sphere = new THREE.Mesh(new THREE.SphereGeometry(radius, 24, 14), material);
    const outline = new THREE.Mesh(new THREE.SphereGeometry(radius, 24, 14), outlineMaterial);
    group.add(sphere);
    group.add(outline);
    const pose = createContactPointPreviewPose(contactPoint, motionSocket);
    setObjectLocalPose(THREE, group, pose, { poseAdapter });
    registerViewportPoseBinding(THREE, group, pose, { poseAdapter });
    registerViewportContactPointBinding(THREE, group, contactPoint, motionSocket, { poseAdapter });
    makeSelectable(group, objectPath, pickables);
    const transformKey = `${objectPath}#localOffsetMeters`;
    setWeaponTransformPick(group, transformKey);
    registerTransformTarget?.({
      key: transformKey,
      objectPath,
      object: group,
      pose,
      poseAdapter,
      transformKind: "contactPoint",
      contactPoint,
      label: `${contactPoint.stableId || contactPoint.contactPointId || "contact point"} presentation contact audio point`,
      defaults: getContactPointTransformDefaults(motionSocket)
    });
    // Authoring probes follow the displayed skeleton, including static poses.
    // Runtime bake tracks may use a different clip and must not drive this preview.
    const parent = motionSocket
      ? createPoseAnchor(THREE, content, getEffectiveSocketPose(motionSocket), bodyBoneRecords, socketsById, createContactPointMotionAnchorName(contactPoint, i), { inheritScale: false, poseAdapter })
      : content;
    parent.add(group);
  }
}

function createContactPointPreviewPose(contactPoint, motionSocket = null) {
  const offset = ensureContactPointOffset(contactPoint);
  return {
    parentKind: motionSocket ? "Socket" : "ModelRoot",
    parentPath: motionSocket?.socketId || "",
    position: {
      x: numberOrDefault(offset.x, 0),
      y: numberOrDefault(offset.y, 0),
      z: numberOrDefault(offset.z, 0)
    },
    rotation: { x: 0, y: 0, z: 0, w: 1 },
    scale: { x: 1, y: 1, z: 1 },
    eulerHint: { x: 0, y: 0, z: 0 }
  };
}

function resolveContactPointMotionSocket(contactPoint, sockets = []) {
  if (String(contactPoint?.motionMode || "Static") !== "BakedSocket") return null;
  const socketId = String(contactPoint?.motionSocketId || "");
  if (!socketId) return null;
  return (sockets || []).find(socket => socket?.socketId === socketId) || null;
}

function getContactPointTransformDefaults(motionSocket) {
  return motionSocket
    ? { parentKind: "Socket", parentPath: motionSocket.socketId || "" }
    : { parentKind: "ModelRoot", parentPath: "" };
}

function createContactPointMotionAnchorName(contactPoint, index = 0) {
  const raw = contactPoint?.stableId || contactPoint?.contactPointId || index;
  return `contact_point_${String(raw).replace(/[^a-zA-Z0-9_]/g, "_")}_motion`;
}

function ensureContactPointOffset(contactPoint) {
  if (!contactPoint.localOffsetMeters || typeof contactPoint.localOffsetMeters !== "object") {
    contactPoint.localOffsetMeters = { x: 0, y: 0, z: 0 };
  }
  contactPoint.localOffsetMeters.x = numberOrDefault(contactPoint.localOffsetMeters.x, 0);
  contactPoint.localOffsetMeters.y = numberOrDefault(contactPoint.localOffsetMeters.y, 0);
  contactPoint.localOffsetMeters.z = numberOrDefault(contactPoint.localOffsetMeters.z, 0);
  return contactPoint.localOffsetMeters;
}

function getContactPointObjectPath(contactPoint, index = 0) {
  const key = contactPoint?.stableId || contactPoint?.contactPointId || index;
  return `geometry/body_part_contact_points/${key}`;
}

function getContactPointColor(activationMode) {
  switch (String(activationMode || "Always")) {
    case "Disabled": return 0x7a7f87;
    case "ActionWindow": return 0xe6902e;
    case "ExplicitState": return 0x9063d8;
    case "MotionState": return 0x37a86b;
    case "Always":
    default:
      return 0x2f9eae;
  }
}

function addSocketMeshes(THREE, content, pickables, sockets, bodyBoneRecords = [], registerSelectionVisual = null, registerTransformTarget = null, poseAdapter = UNITY_GLTF_PREVIEW_POSE_ADAPTER) {
  for (const socket of sockets) {
    const objectPath = `geometry/sockets/${socket.socketId}`;
    const semantic = String(socket?.abstractBoneId || "");
    const baseColor = semantic ? 0x2f8ec4 : 0xb46a1f;
    const material = new THREE.MeshStandardMaterial({ color: baseColor, emissive: 0x000000 });
    const applySelection = selected => {
      material.color.setHex(selected ? 0xffa11f : baseColor);
      material.emissive.setHex(selected ? 0x442000 : 0x000000);
    };
    applySelection(state.selectedPath === objectPath);
    registerSelectionVisual?.(objectPath, applySelection);
    const mesh = new THREE.Mesh(new THREE.SphereGeometry(0.035, 16, 10), material);
    mesh.name = semantic ? `AbstractBone_${semantic}` : `Socket_${socket.socketId}`;
    mesh.userData.abstractBoneId = semantic;
    const pose = getMutableSocketPose(socket);
    const parent = resolvePoseParentContent(THREE, content, pose, bodyBoneRecords, `socket_${socket.socketId}_parent`, getSocketBoneParentCandidates(socket), { inheritScale: false, poseAdapter });
    setObjectLocalPose(THREE, mesh, pose, { poseAdapter });
    registerViewportPoseBinding(THREE, mesh, pose, { poseAdapter });
    makeSelectable(mesh, objectPath, pickables);
    const transformKey = `${objectPath}#localPose`;
    setWeaponTransformPick(mesh, transformKey);
    registerTransformTarget?.({
      key: transformKey,
      objectPath,
      object: mesh,
      pose,
      poseAdapter,
      label: `${socket.socketId || "socket"} socket`,
      defaults: defaultSocketPoseParent(socket)
    });
    parent.add(mesh);
  }
}

function addEngagementAnchorMeshes(THREE, content, pickables, anchors, registerSelectionVisual = null, registerTransformTarget = null, poseAdapter = UNITY_GLTF_PREVIEW_POSE_ADAPTER) {
  for (const anchor of anchors || []) {
    if (!anchor?.enabled) continue;
    const objectPath = `geometry/engagement_anchors/${encodeURIComponent(anchor.anchorId || "anchor")}`;
    const baseColor = 0x31c48d;
    const material = new THREE.MeshStandardMaterial({ color: baseColor, emissive: 0x062d21, depthTest: false });
    const lineMaterial = new THREE.LineBasicMaterial({ color: baseColor, depthTest: false });
    const applySelection = selected => {
      const color = selected ? 0xffa11f : baseColor;
      material.color.setHex(color);
      material.emissive.setHex(selected ? 0x442000 : 0x062d21);
      lineMaterial.color.setHex(color);
    };
    applySelection(state.selectedPath === objectPath);
    registerSelectionVisual?.(objectPath, applySelection);

    const group = new THREE.Group();
    group.name = `EngagementAnchor_${anchor.anchorId || "anchor"}`;
    const marker = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.08, 0.025, 20), material);
    marker.position.y = 0.0125;
    group.add(marker);
    const arrow = new THREE.Line(
      new THREE.BufferGeometry().setFromPoints([
        new THREE.Vector3(0, 0.035, 0),
        new THREE.Vector3(0, 0.035, 0.28)
      ]),
      lineMaterial);
    group.add(arrow);
    const tip = new THREE.Mesh(new THREE.ConeGeometry(0.045, 0.1, 12), material);
    tip.position.set(0, 0.035, 0.31);
    tip.rotation.x = Math.PI / 2;
    group.add(tip);

    const position = anchor.localPositionMillimeters || {};
    const pose = {
      parentKind: "ModelRoot",
      parentPath: "",
      position: {
        x: Number(position.x || 0) / 1000,
        y: Number(position.y || 0) / 1000,
        z: Number(position.z || 0) / 1000
      },
      eulerHint: { x: 0, y: Number(anchor.yawMilliDegrees || 0) / 1000, z: 0 },
      scale: { x: 1, y: 1, z: 1 }
    };
    setObjectLocalPose(THREE, group, pose, { poseAdapter });
    group.traverse(child => makeSelectable(child, objectPath, pickables));
    const transformKey = `${objectPath}#engagementAnchorPose`;
    setWeaponTransformPick(group, transformKey);
    registerTransformTarget?.({
      key: transformKey,
      objectPath,
      object: group,
      pose,
      poseAdapter,
      transformKind: "engagementAnchor",
      engagementAnchor: anchor,
      label: `${anchor.anchorId || "anchor"} engagement anchor`,
      defaults: { parentKind: "ModelRoot", parentPath: "" }
    });
    content.add(group);
  }
}

function computeTraceSpatialFrame(startValue = {}, endValue = {}) {
  const start = { x: Number(startValue.x || 0), y: Number(startValue.y || 0), z: Number(startValue.z || 0) };
  const end = { x: Number(endValue.x || 0), y: Number(endValue.y || 0), z: Number(endValue.z || 0) };
  let axis = { x: end.x - start.x, y: end.y - start.y, z: end.z - start.z };
  const length = Math.hypot(axis.x, axis.y, axis.z);
  if (length <= 0.000001) axis = { x: 0, y: 1, z: 0 };
  else axis = { x: axis.x / length, y: axis.y / length, z: axis.z / length };
  let perpendicular = { x: 1 - axis.x * axis.x, y: -axis.x * axis.y, z: -axis.x * axis.z };
  let perpendicularLength = Math.hypot(perpendicular.x, perpendicular.y, perpendicular.z);
  if (perpendicularLength <= 0.000001) {
    perpendicular = { x: -axis.z * axis.x, y: -axis.z * axis.y, z: 1 - axis.z * axis.z };
    perpendicularLength = Math.hypot(perpendicular.x, perpendicular.y, perpendicular.z) || 1;
  }
  perpendicular = {
    x: perpendicular.x / perpendicularLength,
    y: perpendicular.y / perpendicularLength,
    z: perpendicular.z / perpendicularLength
  };
  return {
    start,
    end,
    axis,
    perpendicular,
    length,
    midpoint: { x: (start.x + end.x) * 0.5, y: (start.y + end.y) * 0.5, z: (start.z + end.z) * 0.5 }
  };
}

function projectTraceScalarValue(field, handleValue = {}, startValue = {}, endValue = {}, proximalExtension = 0) {
  const frame = computeTraceSpatialFrame(startValue, endValue);
  const handle = { x: Number(handleValue.x || 0), y: Number(handleValue.y || 0), z: Number(handleValue.z || 0) };
  if (field === "proximalExtension") {
    return Math.max(0,
      (frame.start.x - handle.x) * frame.axis.x
      + (frame.start.y - handle.y) * frame.axis.y
      + (frame.start.z - handle.z) * frame.axis.z);
  }
  const extension = field === "proximalRadius" ? Math.max(0, Number(proximalExtension || 0)) : 0;
  const origin = field === "proximalRadius"
    ? {
        x: frame.start.x - frame.axis.x * extension * 0.5,
        y: frame.start.y - frame.axis.y * extension * 0.5,
        z: frame.start.z - frame.axis.z * extension * 0.5
      }
    : frame.midpoint;
  return Math.max(0, Math.abs(
    (handle.x - origin.x) * frame.perpendicular.x
    + (handle.y - origin.y) * frame.perpendicular.y
    + (handle.z - origin.z) * frame.perpendicular.z));
}

function addTraceMeshes(THREE, content, pickables, traces, registerSelectionVisual = null, bodyBoneRecords = [], socketsById = {}, registerTransformTarget = null, poseAdapter = UNITY_GLTF_PREVIEW_POSE_ADAPTER, options = {}) {
  for (const trace of traces) {
    trace.startPose ||= defaultLocalPose();
    trace.endPose ||= defaultLocalPose();
    const objectPath = options.objectPathPrefix
      ? `${options.objectPathPrefix}/${encodeURIComponent(trace.traceId || "trace")}`
      : `geometry/traces/${trace.traceId}`;
    const material = new THREE.LineBasicMaterial({ color: 0xc24141, linewidth: 2 });
    const markerMaterial = new THREE.MeshBasicMaterial({ color: 0xc24141, depthTest: false });
    const volumeMaterial = new THREE.MeshBasicMaterial({ color: 0xc24141, transparent: true, opacity: 0.16, depthWrite: false });
    const proximalMaterial = new THREE.MeshBasicMaterial({ color: 0x8b5cf6, transparent: true, opacity: 0.2, depthWrite: false });
    const handleMaterial = new THREE.MeshBasicMaterial({ color: 0xfbbf24, depthTest: false });
    const applySelection = selected => {
      material.color.setHex(selected ? 0xffa11f : 0xc24141);
      markerMaterial.color.setHex(selected ? 0xffa11f : 0xc24141);
      volumeMaterial.color.setHex(selected ? 0xffa11f : 0xc24141);
    };
    applySelection(state.selectedPath === objectPath);
    registerSelectionVisual?.(objectPath, applySelection);
    const startAnchor = createPoseAnchor(THREE, content, trace.startPose, bodyBoneRecords, socketsById, `trace_${trace.traceId}_start`, { poseAdapter });
    const endAnchor = createPoseAnchor(THREE, content, trace.endPose, bodyBoneRecords, socketsById, `trace_${trace.traceId}_end`, { poseAdapter });
    const startKey = `${objectPath}#startPose`;
    const endKey = `${objectPath}#endPose`;
    registerTransformTarget?.({
      key: startKey,
      objectPath,
      object: startAnchor,
      pose: trace.startPose,
      poseAdapter,
      label: "Trace start",
      defaults: { parentKind: trace.startPose?.parentKind || "ModelRoot", parentPath: trace.startPose?.parentPath || "" }
    });
    registerTransformTarget?.({
      key: endKey,
      objectPath,
      object: endAnchor,
      pose: trace.endPose,
      poseAdapter,
      label: "Trace end",
      defaults: { parentKind: trace.endPose?.parentKind || "ModelRoot", parentPath: trace.endPose?.parentPath || "" }
    });
    const line = new THREE.Line(
      new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(), new THREE.Vector3()]),
      material
    );
    line.userData.poseLine = { startAnchor, endAnchor };
    line.renderOrder = 22;
    makeSelectable(line, objectPath, pickables);
    content.add(line);
    const markerGeometry = new THREE.SphereGeometry(Math.max(0.018, Number(trace.radius || 0.04) * 0.45), 14, 8);
    const startMarker = new THREE.Mesh(markerGeometry, markerMaterial);
    const endMarker = new THREE.Mesh(markerGeometry, markerMaterial);
    startMarker.renderOrder = 23;
    endMarker.renderOrder = 23;
    makeSelectable(startMarker, objectPath, pickables);
    makeSelectable(endMarker, objectPath, pickables);
    setWeaponTransformPick(startMarker, startKey);
    setWeaponTransformPick(endMarker, endKey);
    startAnchor.add(startMarker);
    endAnchor.add(endMarker);

    if (!options.enableSpatialEditing) continue;

    const mainVolume = new THREE.Mesh(new THREE.CylinderGeometry(1, 1, 1, 18, 1, true), volumeMaterial);
    mainVolume.userData.traceVolume = { startAnchor, endAnchor, trace, radiusField: "radius", extension: false };
    mainVolume.renderOrder = 20;
    makeSelectable(mainVolume, objectPath, pickables);
    content.add(mainVolume);

    const proximalVolume = new THREE.Mesh(new THREE.CylinderGeometry(1, 1, 1, 18, 1, true), proximalMaterial);
    proximalVolume.userData.traceVolume = { startAnchor, endAnchor, trace, radiusField: "proximalRadius", extension: true };
    proximalVolume.renderOrder = 21;
    makeSelectable(proximalVolume, objectPath, pickables);
    content.add(proximalVolume);

    for (const [field, label] of [
      ["radius", "Trace radius"],
      ["proximalExtension", "Trace proximal extension"],
      ["proximalRadius", "Trace proximal radius"]
    ]) {
      const handle = new THREE.Mesh(new THREE.SphereGeometry(0.026, 14, 9), handleMaterial.clone());
      const key = `${objectPath}#${field}`;
      handle.userData.traceScalarHandle = { startAnchor, endAnchor, trace, field };
      handle.renderOrder = 25;
      makeSelectable(handle, objectPath, pickables);
      setWeaponTransformPick(handle, key);
      content.add(handle);
      registerTransformTarget?.({
        key,
        objectPath,
        object: handle,
        transformKind: "traceScalar",
        trace,
        traceScalarField: field,
        label
      });
    }
  }
}

function addWeaponDataMeshes(THREE, content, pickables, items, bodyBoneRecords = [], socketsById = {}, registerSelectionVisual = null) {
  const fallbackX = { mainHand: 0.45, offHand: -0.45, twoHand: 0, naturalWeapon: 0 };
  items.forEach((item, index) => {
    const objectPath = item.weaponPath || item.tracePath || item.socketPath || "";
    const tone = getWeaponVisualizationTone(item);
    const color = tone === "error" ? 0xef4444 : tone === "warn" ? 0xf59e0b : 0x2fc2bd;
    const anchor = item.attachment
      ? createPoseAnchor(THREE, content, getAttachmentGripPose(item.attachment), bodyBoneRecords, socketsById, `weapon_data_${item.weaponId || item.slot}`)
      : createFallbackWeaponDataAnchor(THREE, content, fallbackX[item.slot] ?? 0, 1.1 + index * 0.12);

    const label = createTextSprite(THREE, [
      `${item.slotLabel}${item.weaponId ? ` / ${item.weaponId}` : ""}`,
      getWeaponVisualizationStatusText(item)
    ], color);
    label.position.set(0, 0.16, 0);
    label.renderOrder = 48;
    if (objectPath) makeSelectable(label, objectPath, pickables);
    anchor.add(label);

    const markerMaterial = new THREE.MeshBasicMaterial({ color, depthTest: false, transparent: true, opacity: 0.88 });
    const marker = new THREE.Mesh(new THREE.SphereGeometry(tone === "ok" ? 0.026 : 0.036, 16, 10), markerMaterial);
    marker.renderOrder = 47;
    if (objectPath) makeSelectable(marker, objectPath, pickables);
    anchor.add(marker);

    if (item.socket && item.attachment) {
      const socketAnchor = createPoseAnchor(THREE, content, getEffectiveSocketPose(item.socket), bodyBoneRecords, socketsById, `weapon_data_${item.weaponId}_socket`);
      addDynamicLineBetweenAnchors(THREE, content, socketAnchor, anchor, color, objectPath, pickables, 32);
    }

    if (item.trace) {
      const startAnchor = createPoseAnchor(THREE, content, item.trace.startPose, bodyBoneRecords, socketsById, `weapon_data_${item.trace.traceId}_trace_start`);
      const endAnchor = createPoseAnchor(THREE, content, item.trace.endPose, bodyBoneRecords, socketsById, `weapon_data_${item.trace.traceId}_trace_end`);
      addDynamicLineBetweenAnchors(THREE, content, startAnchor, endAnchor, color, item.tracePath, pickables, 33);
      const traceLabel = createTextSprite(THREE, [
        `轨迹 / ${item.trace.traceId}`,
        `${item.trace.sampleRule || "default"} / r=${Number(item.trace.radius || 0).toFixed(3)}`
      ], color);
      traceLabel.position.set(0, 0.12, 0);
      traceLabel.renderOrder = 49;
      if (item.tracePath) makeSelectable(traceLabel, item.tracePath, pickables);
      endAnchor.add(traceLabel);
    }

    if (!objectPath) return;
    const applySelection = selected => {
      markerMaterial.color.setHex(selected ? 0xffa11f : color);
      marker.scale.setScalar(selected ? 1.35 : 1);
    };
    applySelection(state.selectedPath === objectPath);
    registerSelectionVisual?.(objectPath, applySelection);
  });
}

async function addFocusedWeaponViewportContent({ THREE, previewLoaders, host, content, pickables, resources, packageUrl, previewModelUrl = null, materialBindings = [], surfacePalette = null, registerSelectionVisual = null, registerTransformTarget = null }) {
  const weapon = getFocusedWeaponAttachment();
  const item = getWeaponVisualizationItemForWeapon(weapon);
  if (!weapon || !item) {
    addFocusedWeaponFallback(THREE, content, pickables, null, "没有武器配置");
    host.insertAdjacentHTML("beforeend", renderWeaponFocusOverlayHtml(null));
    wireWeaponFocusOverlay(host);
    return;
  }

  let effectiveSurfacePalette = surfacePalette;
  if (!effectiveSurfacePalette) {
    try {
      effectiveSurfacePalette = await loadDefaultSurfacePalettePreview(THREE, resources, packageUrl);
    } catch {}
  }

  let loaded = false;
  let weaponContent = content;
  if (weapon.__singleWeaponPreview) {
    const pose = weapon.localGripPose || defaultLocalPose();
    weaponContent = createPoseAnchor(THREE, content, pose, [], {}, `single_weapon_${weapon.weaponId}`);
    registerTransformTarget?.({
      key: `${item.weaponPath}#modelPose`, objectPath: item.weaponPath,
      object: weaponContent, pose, poseAdapter: UNITY_GLTF_PREVIEW_POSE_ADAPTER,
      label: "武器自身变换", defaults: { parentKind: "ModelRoot", parentPath: "" }
    });
  }
  if (item.resource?.relativePath) {
    const modelPose = ensureModelWrapperPose(item.resource);
    loaded = await addPreviewModelResource({
      THREE,
      previewLoaders,
      content: weaponContent,
      pickables,
      url: (previewModelUrl || ((resource, relative, version) => packageUrl(relative, version)))(item.resource, item.resource.relativePath, getPreviewModelResourceHash(item.resource)),
      objectPath: item.resourcePath || item.weaponPath,
      name: weapon.weaponId,
      wrapperPose: modelPose,
      wrapperPoseAdapter: UNITY_GLTF_PREVIEW_POSE_ADAPTER,
      normalizeUnityWrapperSourceRoot: !!item.resource.usesUnityWrapperPrefab,
      previewUnitScale: getPreviewUnitScaleForResource(item.resource),
      materialBindings,
      surfacePalette: effectiveSurfacePalette,
      transformTargetSink: ({ modelWrapper, format }) => {
        if (!item.resourcePath) return;
        const modelWrapperPoseAdapter = getPoseAdapterForPreviewFormat(format, UNITY_GLTF_PREVIEW_POSE_ADAPTER);
        registerTransformTarget?.({
          key: `${item.resourcePath}#modelWrapperPose`,
          objectPath: item.resourcePath,
          object: modelWrapper,
          pose: modelPose,
          poseAdapter: modelWrapperPoseAdapter,
          label: "模型 wrapper",
          defaults: { parentKind: "ModelRoot", parentPath: "" }
        });
      }
    });
  }
  if (!loaded) {
    addFocusedWeaponFallback(THREE, content, pickables, item.weaponPath, weapon.weaponId);
  }

  if (weapon.__singleWeaponPreview && state.singleWeaponPoseGuideVisible !== false) {
    await addSingleWeaponPoseCalibrationGuide({
      THREE,
      previewLoaders,
      content,
      resources,
      packageUrl,
      previewModelUrl,
      currentWeaponId: weapon.weaponId
    });
  }

  if (!weapon.__singleWeaponPreview)
    addWeaponGripReference(THREE, content, pickables, weapon, item.weaponPath, registerSelectionVisual, registerTransformTarget);
  if (weapon.__singleWeaponPreview) {
    const definition = getSingleWeaponConfiguration(weapon.weaponId);
    addTraceMeshes(
      THREE,
      weaponContent,
      pickables,
      definition?.traceProfiles || [],
      registerSelectionVisual,
      [],
      {},
      registerTransformTarget,
      UNITY_GLTF_PREVIEW_POSE_ADAPTER,
      {
        objectPathPrefix: `${item.weaponPath}/traceProfiles`,
        enableSpatialEditing: true
      });
  } else if (item.trace) {
    const focusedTrace = normalizeWeaponPreviewTrace(item.trace, weapon);
    addTraceMeshes(THREE, weaponContent, pickables, [focusedTrace], registerSelectionVisual, [], {}, registerTransformTarget);
  }
  host.insertAdjacentHTML("beforeend", renderWeaponFocusOverlayHtml(item));
  wireWeaponFocusOverlay(host);
}

function getSingleWeaponPoseReferenceCandidates() {
  return (state.weaponLibrary?.singleWeaponConfigurations || []).filter(definition =>
    definition?.singleWeaponConfigurationId !== "weapon.unarmed" && definition?.weaponRef);
}

function getSingleWeaponPoseReferenceDefinition() {
  const candidates = getSingleWeaponPoseReferenceCandidates();
  const selected = candidates.find(definition =>
    definition?.singleWeaponConfigurationId === state.singleWeaponPoseReferenceId);
  return selected || candidates[0] || null;
}

async function addSingleWeaponPoseCalibrationGuide({ THREE, previewLoaders, content, resources, packageUrl, previewModelUrl, currentWeaponId }) {
  const reference = getSingleWeaponPoseReferenceDefinition();
  const referenceGuidePose = clone(reference?.modelPose || defaultLocalPose());
  referenceGuidePose.scale = { x: 1, y: 1, z: 1 };
  const guide = createPoseAnchor(
    THREE,
    content,
    referenceGuidePose,
    [],
    {},
    "single_weapon_pose_calibration_guide");
  guide.name = "single_weapon_pose_calibration_guide";
  guide.userData.authoringGuide = true;

  const axisMaterial = new THREE.LineBasicMaterial({ color: 0x2dd4bf, transparent: true, opacity: 0.78, depthTest: false });
  const tickPoints = [];
  for (let index = -3; index <= 12; index++) {
    const y = index * 0.1;
    const halfWidth = index % 5 === 0 ? 0.18 : 0.09;
    tickPoints.push(new THREE.Vector3(0, y, -halfWidth), new THREE.Vector3(0, y, halfWidth));
  }
  const centerLine = new THREE.Line(
    new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(0, -0.35, 0), new THREE.Vector3(0, 1.25, 0)]),
    axisMaterial);
  centerLine.renderOrder = 60;
  guide.add(centerLine);
  const ticks = new THREE.LineSegments(new THREE.BufferGeometry().setFromPoints(tickPoints), axisMaterial.clone());
  ticks.renderOrder = 60;
  guide.add(ticks);

  const gripMaterial = new THREE.MeshBasicMaterial({ color: 0x14b8a6, transparent: true, opacity: 0.16, depthWrite: false });
  const grip = new THREE.Mesh(new THREE.CylinderGeometry(0.055, 0.055, 0.3, 18, 1, true), gripMaterial);
  grip.position.y = -0.15;
  grip.renderOrder = 55;
  guide.add(grip);
  const guard = new THREE.Mesh(
    new THREE.TorusGeometry(0.18, 0.008, 8, 40),
    new THREE.MeshBasicMaterial({ color: 0x5eead4, transparent: true, opacity: 0.72, depthTest: false }));
  guard.rotation.x = Math.PI / 2;
  guard.renderOrder = 61;
  guide.add(guard);
  const origin = new THREE.Mesh(
    new THREE.SphereGeometry(0.026, 14, 9),
    new THREE.MeshBasicMaterial({ color: 0xffffff, depthTest: false }));
  origin.renderOrder = 62;
  guide.add(origin);

  for (const [direction, color, label, position, length] of [
    [new THREE.Vector3(1, 0, 0), 0xef4444, "+X", new THREE.Vector3(0.24, 0, 0), 0.22],
    [new THREE.Vector3(0, 1, 0), 0x22c55e, "+Y 长度", new THREE.Vector3(0, 1.34, 0), 0.22],
    [new THREE.Vector3(0, 0, 1), 0x3b82f6, "+Z 正面朝向", new THREE.Vector3(0, 0, 0.48), 0.42]
  ]) {
    const arrow = new THREE.ArrowHelper(direction, new THREE.Vector3(), length, color, 0.07, 0.04);
    arrow.renderOrder = 63;
    arrow.line.material.depthTest = false;
    arrow.cone.material.depthTest = false;
    guide.add(arrow);
    const sprite = createTextSprite(THREE, [label], color);
    sprite.position.copy(position);
    sprite.scale.multiplyScalar(0.55);
    sprite.renderOrder = 64;
    guide.add(sprite);
  }
  const originLabel = createTextSprite(THREE, ["握持原点 / 护手面"], 0x5eead4);
  originLabel.position.set(0.27, -0.08, 0);
  originLabel.scale.multiplyScalar(0.58);
  originLabel.renderOrder = 64;
  guide.add(originLabel);

  if (!reference || reference.singleWeaponConfigurationId === currentWeaponId) return;
  const resourceKey = getResourceSelectionKey(reference.weaponRef);
  const resource = findPreviewModelResourceByKey(resourceKey, resources);
  if (!resource?.relativePath) return;
  const ghostRoot = createPoseAnchor(
    THREE,
    content,
    reference.modelPose || defaultLocalPose(),
    [],
    {},
    `weapon_pose_reference_${reference.singleWeaponConfigurationId}`);
  ghostRoot.userData.authoringGuide = true;
  const loaded = await addPreviewModelResource({
    THREE,
    previewLoaders,
    content: ghostRoot,
    pickables: [],
    url: (previewModelUrl || ((entry, relative, version) => packageUrl(relative, version)))(resource, resource.relativePath, getPreviewModelResourceHash(resource)),
    objectPath: "",
    name: `${reference.singleWeaponConfigurationId}_pose_reference`,
    wrapperPose: resource.importHints?.modelWrapperPose,
    wrapperPoseAdapter: UNITY_GLTF_PREVIEW_POSE_ADAPTER,
    normalizeUnityWrapperSourceRoot: !!resource.usesUnityWrapperPrefab,
    previewUnitScale: getPreviewUnitScaleForResource(resource)
  });
  if (!loaded) return;
  ghostRoot.traverse(object => {
    if (!object.isMesh || !object.material) return;
    const sourceMaterials = Array.isArray(object.material) ? object.material : [object.material];
    const ghostMaterials = sourceMaterials.map(source => {
      const material = source.clone();
      material.color?.setHex?.(0x67e8f9);
      material.transparent = true;
      material.opacity = 0.2;
      material.depthWrite = false;
      material.wireframe = true;
      return material;
    });
    object.material = Array.isArray(object.material) ? ghostMaterials : ghostMaterials[0];
    object.renderOrder = 52;
  });
}

function getWeaponVisualizationItemForWeapon(weapon) {
  if (!weapon?.weaponId) return null;
  if (weapon.__singleWeaponPreview) {
    const definition = getSingleWeaponConfiguration(weapon.weaponId);
    const resourceKey = getResourceSelectionKey(definition?.weaponRef);
    const resource = findPreviewModelResourceByKey(resourceKey);
    return {
      slot: "weaponLocal", slotLabel: "武器自身", attachment: weapon,
      attachmentId: weapon.attachmentId, weaponId: weapon.weaponId,
      weaponPath: `weapon-library/singleWeaponConfigurations/${encodeURIComponent(weapon.weaponId)}`,
      socket: null, socketPath: "", resource,
      resourcePath: resource ? getPreviewModelResourceObjectPath(resource) : "",
      trace: definition?.traceProfiles?.[0] || null,
      tracePath: definition?.traceProfiles?.[0] ? `weapon-library/singleWeaponConfigurations/${encodeURIComponent(weapon.weaponId)}/traceProfiles/${encodeURIComponent(definition.traceProfiles[0].traceId)}` : "",
      sync: resource ? getResourceUnitySync(resource) : null,
      issues: resource ? [] : [weaponIssue("warn", "未绑定模型", "请在单一武器配置中选择模型资源。")]
    };
  }
  const items = buildWeaponVisualizationItems(state.package, [weapon.equipSlot || "mainHand"], getResourceUnitySync);
  const attachmentId = getWeaponAttachmentIdentity(weapon);
  return items.find(item => item.attachmentId === attachmentId) || items[0] || null;
}

function addFocusedWeaponFallback(THREE, content, pickables, objectPath, label) {
  const material = new THREE.MeshStandardMaterial({ color: 0xb46a1f, transparent: true, opacity: 0.82, roughness: 0.62 });
  const grip = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.04, 0.28, 16), material);
  grip.rotation.x = Math.PI / 2;
  const blade = new THREE.Mesh(new THREE.BoxGeometry(0.055, 0.9, 0.035), material);
  blade.position.y = 0.52;
  const group = new THREE.Group();
  group.name = label || "weapon_placeholder";
  group.add(grip);
  group.add(blade);
  if (objectPath) makeSelectable(grip, objectPath, pickables);
  if (objectPath) makeSelectable(blade, objectPath, pickables);
  content.add(group);
}

function addWeaponGripReference(THREE, content, pickables, weapon, objectPath, registerSelectionVisual = null, registerTransformTarget = null) {
  const pose = getAttachmentGripPose(weapon);
  const anchor = createPoseAnchor(THREE, content, pose, [], {}, `weapon_focus_grip_${weapon.weaponId}`);
  const transformKey = `${objectPath}#localGripPose`;
  registerTransformTarget?.({
    key: transformKey,
    objectPath,
    object: anchor,
    pose,
    poseAdapter: UNITY_GLTF_PREVIEW_POSE_ADAPTER,
    label: "握持姿态",
    defaults: { parentKind: weapon?.attachSocketId ? "Socket" : "ModelRoot", parentPath: weapon?.attachSocketId || "" }
  });
  const material = new THREE.MeshBasicMaterial({ color: 0x2fc2bd, depthTest: false });
  const marker = new THREE.Mesh(new THREE.SphereGeometry(0.035, 16, 10), material);
  marker.renderOrder = 45;
  makeSelectable(marker, objectPath, pickables);
  setWeaponTransformPick(marker, transformKey);
  anchor.add(marker);
  addAxisLines(THREE, anchor, objectPath, pickables);
  const label = createTextSprite(THREE, ["握持", weapon.attachSocketId || "无 socket"], 0x2fc2bd);
  label.position.set(0, 0.13, 0);
  label.renderOrder = 50;
  if (objectPath) makeSelectable(label, objectPath, pickables);
  anchor.add(label);
}

function addAxisLines(THREE, parent, objectPath, pickables) {
  const axes = [
    { points: [new THREE.Vector3(), new THREE.Vector3(0.18, 0, 0)], color: 0xef4444 },
    { points: [new THREE.Vector3(), new THREE.Vector3(0, 0.18, 0)], color: 0x10b981 },
    { points: [new THREE.Vector3(), new THREE.Vector3(0, 0, 0.18)], color: 0x3b82f6 }
  ];
  for (const axis of axes) {
    const line = new THREE.Line(
      new THREE.BufferGeometry().setFromPoints(axis.points),
      new THREE.LineBasicMaterial({ color: axis.color, depthTest: false })
    );
    line.renderOrder = 44;
    makeSelectable(line, objectPath, pickables);
    parent.add(line);
  }
}

function normalizeWeaponPreviewTrace(trace, weapon) {
  const preview = clone(trace);
  preview.startPose = normalizeWeaponPreviewPose(preview.startPose, weapon);
  preview.endPose = normalizeWeaponPreviewPose(preview.endPose, weapon);
  return preview;
}

function normalizeWeaponPreviewPose(pose, weapon) {
  const previewPose = pose ? clone(pose) : defaultLocalPose();
  if (previewPose.parentKind === "Socket"
    && (!previewPose.parentPath || previewPose.parentPath === weapon?.attachSocketId || previewPose.parentPath === weapon?.equipSlot)) {
    previewPose.parentKind = "ModelRoot";
    previewPose.parentPath = "";
  }
  return previewPose;
}

function renderWeaponFocusOverlayHtml(item) {
  if (!item) {
    return `<aside class="weapon-focus-overlay"><strong>武器视图</strong><div class="weapon-data-issue error">没有找到武器配置。</div></aside>`;
  }
  const resourceText = item.resource ? getResourceDisplayName(item.resource) : (getWeaponAttachmentResourceKey(item.attachment) || "未绑定模型");
  const traceText = item.trace ? item.trace.traceId : (item.attachment?.traceId || "未绑定攻击轨迹");
  const singleDefinition = item.attachment?.__singleWeaponPreview
    ? getSingleWeaponConfiguration(item.weaponId)
    : null;
  const traceSummary = singleDefinition
    ? `${singleDefinition.traceProfiles?.length || 0} 条 Trace`
    : traceText;
  const issueRows = item.issues.length
    ? item.issues.map(issue => `<div class="weapon-data-issue ${escapeHtml(issue.tone)}">${escapeHtml(issue.message)}</div>`).join("")
    : `<div class="weapon-data-issue ok">武器挂接只选择攻击轨迹几何；动作窗口只启用或禁用命中检测。</div>`;
  const scaleHint = item.resourcePath && shouldShowModelScaleRecommendation(item.resource)
    ? `<div class="weapon-data-issue warn">${escapeHtml(formatModelScaleRecommendation(item.resource))}</div>`
    : "";
  const wrapperHint = renderPublicWeaponWrapperHint(item.resource);
  const scalePresets = item.resourcePath && !item.attachment?.__singleWeaponPreview ? renderModelWrapperScalePresetButtons(item.resourcePath) : "";
  const poseReferenceCandidates = singleDefinition ? getSingleWeaponPoseReferenceCandidates() : [];
  const selectedPoseReference = getSingleWeaponPoseReferenceDefinition();
  const referenceEuler = selectedPoseReference?.modelPose?.eulerHint || {};
  const referenceRotationText = `基准旋转 ${Number(referenceEuler.x || 0).toFixed(1)}° / ${Number(referenceEuler.y || 0).toFixed(1)}° / ${Number(referenceEuler.z || 0).toFixed(1)}°；+Z 为正面`;
  const poseGuideControls = singleDefinition ? `<div class="weapon-pose-guide-controls">
    <label><span>姿态参考武器</span><select data-single-weapon-pose-reference${poseReferenceCandidates.length ? "" : " disabled"}>${poseReferenceCandidates.map(definition => `<option value="${escapeHtml(definition.singleWeaponConfigurationId)}"${definition.singleWeaponConfigurationId === selectedPoseReference?.singleWeaponConfigurationId ? " selected" : ""}>${escapeHtml(definition.displayName || definition.singleWeaponConfigurationId)}</option>`).join("")}</select></label>
    <button type="button" data-toggle-single-weapon-pose-guide>${state.singleWeaponPoseGuideVisible === false ? "显示姿态基准" : "隐藏姿态基准"}</button>
    <span>${selectedPoseReference?.singleWeaponConfigurationId === item.weaponId ? "当前武器即参考基准" : `青色线框：${escapeHtml(selectedPoseReference?.displayName || "无参考模型")}`}<br>${escapeHtml(referenceRotationText)}</span>
  </div>` : "";
  const transformActions = item.attachment?.__singleWeaponPreview
    ? `<button type="button" data-edit-weapon-model="${escapeHtml(item.attachmentId)}">编辑武器自身变换</button>${item.resource ? "" : `<button type="button" data-pick-single-weapon-resource="${escapeHtml(item.weaponId)}">选择模型资源</button>`}`
    : item.resourcePath
      ? `<button type="button" data-edit-weapon-model="${escapeHtml(item.attachmentId)}">编辑模型导入修正</button><button type="button" data-auto-fit-weapon="${escapeHtml(item.attachmentId)}">自动适配武器尺寸</button>${shouldShowModelScaleRecommendation(item.resource) ? `<button type="button" data-apply-model-scale="${escapeHtml(item.resourcePath)}">应用推荐缩放</button>` : ""}`
      : "";
  return `<aside class="weapon-focus-overlay" aria-label="武器聚焦">
    <div class="weapon-data-head"><strong>${escapeHtml(singleDefinition?.displayName || "武器视图")}</strong><span>${escapeHtml(item.weaponId)}${singleDefinition ? ` / ${state.weaponLibraryDirty ? "草稿" : "已保存"}` : ""}</span></div>
    <div class="weapon-data-grid">
      ${renderWeaponDataValue("武器模型", resourceText, item.resourcePath, "编辑模型")}
      ${renderWeaponDataValue("攻击轨迹", traceSummary, item.tracePath, "编辑轨迹")}
      <div><span>半径</span><strong>${escapeHtml(item.trace ? Number(item.trace.radius || 0).toFixed(3) : "-")}</strong></div>
      <div><span>Unity</span><strong>${item.sync ? renderSyncBadge(item.sync) : "无数据"}</strong></div>
    </div>
    ${transformActions ? `<div class="weapon-data-actions">${transformActions}</div>` : ""}
    ${poseGuideControls}
    ${scalePresets}
    <div class="weapon-data-issue-list">${scaleHint}${wrapperHint}${issueRows}</div>
  </aside>`;
}

function renderPublicWeaponWrapperHint(resource) {
  if (!resource?.usesUnityWrapperPrefab) return "";
  const runtimePath = resource.unityRuntimeAssetPath || resource.generatedAssetPath || "";
  return `<div class="weapon-data-issue ok" title="${escapeHtml(runtimePath)}">Unity 使用 wrapper Prefab；CharacterStudio 同步归零源模型根姿态并应用同一份 modelWrapperPose。</div>`;
}

function wireWeaponFocusOverlay(root) {
  root?.querySelectorAll?.(".weapon-focus-overlay [data-jump]")?.forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      selectPath(button.dataset.jump, { preserveViewport: true });
    });
  });
  root?.querySelectorAll?.(".weapon-focus-overlay [data-edit-weapon-model]")?.forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      editWeaponModelTransformForWeapon(button.dataset.editWeaponModel);
    });
  });
  root?.querySelectorAll?.(".weapon-focus-overlay [data-pick-single-weapon-resource]")?.forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      globalThis.openSingleWeaponResourcePicker?.(button.dataset.pickSingleWeaponResource);
    });
  });
  root?.querySelectorAll?.(".weapon-focus-overlay [data-toggle-single-weapon-pose-guide]")?.forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      state.singleWeaponPoseGuideVisible = state.singleWeaponPoseGuideVisible === false;
      renderViewport();
    });
  });
  root?.querySelectorAll?.(".weapon-focus-overlay [data-single-weapon-pose-reference]")?.forEach(select => {
    select.addEventListener("change", event => {
      event.stopPropagation();
      state.singleWeaponPoseReferenceId = select.value || "";
      state.singleWeaponPoseGuideVisible = true;
      renderViewport();
    });
  });
  root?.querySelectorAll?.(".weapon-focus-overlay [data-auto-fit-weapon]")?.forEach(button => {
    button.addEventListener("click", async event => {
      event.stopPropagation();
      await autoFitWeaponModelForWeapon(button.dataset.autoFitWeapon);
    });
  });
  root?.querySelectorAll?.(".weapon-focus-overlay [data-apply-model-scale]")?.forEach(button => {
    button.addEventListener("click", async event => {
      event.stopPropagation();
      await applyRecommendedModelScaleForResourcePath(button.dataset.applyModelScale);
    });
  });
  root?.querySelectorAll?.(".weapon-focus-overlay [data-apply-model-scale-preset]")?.forEach(button => {
    button.addEventListener("click", async event => {
      event.stopPropagation();
      await applyModelWrapperScalePresetForResourcePath(button.dataset.applyModelScalePreset, button.dataset.scalePreset);
    });
  });
}

function createFallbackWeaponDataAnchor(THREE, content, x, y) {
  const anchor = new THREE.Group();
  anchor.position.set(x, y, 0);
  content.add(anchor);
  return anchor;
}

function getAttachmentGripPose(attachment) {
  const pose = attachment?.localGripPose || defaultLocalPose();
  if (!pose.parentKind) pose.parentKind = attachment?.attachSocketId ? "Socket" : "ModelRoot";
  if (!pose.parentPath && pose.parentKind === "Socket") pose.parentPath = attachment?.attachSocketId || "";
  return pose;
}

function createPoseAnchor(THREE, content, pose, bodyBoneRecords = [], socketsById = {}, name = "pose_anchor", options = {}) {
  const parent = resolvePoseParentContentAdvanced(THREE, content, pose, bodyBoneRecords, socketsById, `${name}_parent`, new Set(), [], options);
  const anchor = new THREE.Group();
  anchor.name = name;
  const poseAdapter = options.poseAdapter ?? UNITY_GLTF_PREVIEW_POSE_ADAPTER;
  setObjectLocalPose(THREE, anchor, pose, { poseAdapter });
  registerViewportPoseBinding(THREE, anchor, pose, { poseAdapter });
  parent.add(anchor);
  return anchor;
}

function resolvePoseParentContentAdvanced(THREE, content, pose, bodyBoneRecords, socketsById, name, visitedSockets = new Set(), bonePathCandidates = [], options = {}) {
  if (pose?.parentKind === "Socket") {
    const socketId = String(pose.parentPath || "");
    const socket = socketsById?.[socketId];
    if (!socket || visitedSockets.has(socketId)) return content;
    visitedSockets.add(socketId);
    const socketPose = getEffectiveSocketPose(socket);
    const socketParent = resolvePoseParentContentAdvanced(THREE, content, socketPose, bodyBoneRecords, socketsById, `${name}_${socketId}`, visitedSockets, getSocketBoneParentCandidates(socket), options);
    const socketGroup = new THREE.Group();
    socketGroup.name = `${name}_${socketId}_anchor`;
    const poseAdapter = options.poseAdapter ?? UNITY_GLTF_PREVIEW_POSE_ADAPTER;
    setObjectLocalPose(THREE, socketGroup, socketPose, { poseAdapter });
    registerViewportPoseBinding(THREE, socketGroup, socketPose, { poseAdapter });
    socketParent.add(socketGroup);
    return socketGroup;
  }
  return resolvePoseParentContent(THREE, content, pose, bodyBoneRecords, name, bonePathCandidates, options);
}

function addDynamicLineBetweenAnchors(THREE, content, startAnchor, endAnchor, color, objectPath, pickables, renderOrder = 30) {
  const line = new THREE.Line(
    new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(), new THREE.Vector3()]),
    new THREE.LineBasicMaterial({ color, transparent: true, opacity: 0.82, depthTest: false })
  );
  line.userData.poseLine = { startAnchor, endAnchor };
  line.renderOrder = renderOrder;
  if (objectPath) makeSelectable(line, objectPath, pickables);
  content.add(line);
  return line;
}

function updatePoseLines(THREE, content) {
  content.updateMatrixWorld(true);
  content.traverse(object => {
    const poseLine = object.userData?.poseLine;
    if (poseLine?.startAnchor && poseLine?.endAnchor && object.geometry?.attributes?.position) {
      const start = new THREE.Vector3();
      const end = new THREE.Vector3();
      poseLine.startAnchor.getWorldPosition(start);
      poseLine.endAnchor.getWorldPosition(end);
      const lineSpace = object.parent || content;
      lineSpace.worldToLocal(start);
      lineSpace.worldToLocal(end);
      const position = object.geometry.attributes.position;
      position.setXYZ(0, start.x, start.y, start.z);
      position.setXYZ(1, end.x, end.y, end.z);
      position.needsUpdate = true;
      object.geometry.computeBoundingSphere();
      return;
    }
    const volume = object.userData?.traceVolume;
    if (volume?.startAnchor && volume?.endAnchor) {
      updateTraceVolumeObject(THREE, object, volume, object.parent || content);
      return;
    }
    const handle = object.userData?.traceScalarHandle;
    if (handle?.startAnchor && handle?.endAnchor
      && !(viewportTransformControls?.dragging && viewportTransformControls.object === object))
      updateTraceScalarHandleObject(THREE, object, handle, object.parent || content);
  });
}

function readTraceAnchorFrame(THREE, startAnchor, endAnchor, content) {
    const start = new THREE.Vector3();
    const end = new THREE.Vector3();
  startAnchor.getWorldPosition(start);
  endAnchor.getWorldPosition(end);
  content.worldToLocal(start);
  content.worldToLocal(end);
  const values = computeTraceSpatialFrame(start, end);
  return {
    ...values,
    start: new THREE.Vector3(values.start.x, values.start.y, values.start.z),
    end: new THREE.Vector3(values.end.x, values.end.y, values.end.z),
    axis: new THREE.Vector3(values.axis.x, values.axis.y, values.axis.z),
    perpendicular: new THREE.Vector3(values.perpendicular.x, values.perpendicular.y, values.perpendicular.z),
    midpoint: new THREE.Vector3(values.midpoint.x, values.midpoint.y, values.midpoint.z)
  };
}

function updateTraceVolumeObject(THREE, object, binding, content) {
  const frame = readTraceAnchorFrame(THREE, binding.startAnchor, binding.endAnchor, content);
  const extension = Math.max(0, Number(binding.trace?.proximalExtension || 0));
  const radius = Math.max(0, Number(binding.trace?.[binding.radiusField] || 0));
  const start = binding.extension ? frame.start.clone().addScaledVector(frame.axis, -extension) : frame.start;
  const end = binding.extension ? frame.start : frame.end;
  const length = binding.extension ? extension : frame.length;
  object.visible = length > 0.000001 && radius > 0.000001;
  object.position.copy(start).add(end).multiplyScalar(0.5);
  object.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), frame.axis);
  object.scale.set(Math.max(radius, 0.0001), Math.max(length, 0.0001), Math.max(radius, 0.0001));
}

function updateTraceScalarHandleObject(THREE, object, binding, content) {
  const frame = readTraceAnchorFrame(THREE, binding.startAnchor, binding.endAnchor, content);
  const field = binding.field;
  const extension = Math.max(0, Number(binding.trace?.proximalExtension || 0));
  if (field === "proximalExtension") {
    object.position.copy(frame.start).addScaledVector(frame.axis, -extension);
  } else {
    const radius = Math.max(0, Number(binding.trace?.[field] || 0));
    const origin = field === "proximalRadius"
      ? frame.start.clone().addScaledVector(frame.axis, -extension * 0.5)
      : frame.midpoint;
    object.position.copy(origin).addScaledVector(frame.perpendicular, radius);
  }
}

function createTextSprite(THREE, lines, colorHex) {
  const canvas = document.createElement("canvas");
  const context = canvas.getContext("2d");
  const textLines = (Array.isArray(lines) ? lines : [lines]).map(line => String(line || "")).filter(Boolean);
  if (textLines.length === 0) textLines.push(" ");
  const font = "24px sans-serif";
  context.font = font;
  const width = Math.max(160, Math.ceil(Math.max(...textLines.map(line => context.measureText(line).width), 80) + 28));
  const height = Math.max(54, textLines.length * 28 + 18);
  canvas.width = width;
  canvas.height = height;
  context.font = font;
  context.fillStyle = "rgba(7, 13, 24, 0.88)";
  roundedRect(context, 0, 0, width, height, 10);
  context.fill();
  context.strokeStyle = `#${colorHex.toString(16).padStart(6, "0")}`;
  context.lineWidth = 3;
  roundedRect(context, 1.5, 1.5, width - 3, height - 3, 9);
  context.stroke();
  context.fillStyle = "#f0f4f9";
  context.textBaseline = "top";
  textLines.forEach((line, index) => {
    context.fillText(line, 14, 10 + index * 27);
  });
  const texture = new THREE.CanvasTexture(canvas);
  texture.needsUpdate = true;
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: texture, transparent: true, depthTest: false }));
  const scale = 0.0022;
  sprite.scale.set(width * scale, height * scale, 1);
  return sprite;
}

function roundedRect(context, x, y, width, height, radius) {
  const r = Math.min(radius, width / 2, height / 2);
  context.beginPath();
  context.moveTo(x + r, y);
  context.lineTo(x + width - r, y);
  context.quadraticCurveTo(x + width, y, x + width, y + r);
  context.lineTo(x + width, y + height - r);
  context.quadraticCurveTo(x + width, y + height, x + width - r, y + height);
  context.lineTo(x + r, y + height);
  context.quadraticCurveTo(x, y + height, x, y + height - r);
  context.lineTo(x, y + r);
  context.quadraticCurveTo(x, y, x + r, y);
  context.closePath();
}

async function addWeaponMeshes({ THREE, previewLoaders, content, pickables, resources, packageUrl, previewModelUrl = null, socketsById, bodyBoneRecords = [], attachments, materialBindings = [], surfacePalette = null, registerSelectionVisual = null, poseAdapter = UNITY_GLTF_PREVIEW_POSE_ADAPTER }) {
  for (const attachment of attachments) {
    const objectPath = getWeaponAttachmentPath(attachment);
    const resource = findPreviewModelResourceByKey(getWeaponAttachmentResourceKey(attachment), resources);
    const gripPose = getAttachmentGripPose(attachment);
    const directBoneGrip = gripPose?.parentKind === "Bone";
    const parent = directBoneGrip
      ? resolvePoseParentContent(THREE, content, gripPose, bodyBoneRecords, `weapon_${attachment.weaponId}_parent`, [], { inheritScale: false, poseAdapter })
      : createPoseAnchor(THREE, content, gripPose, bodyBoneRecords, socketsById, `weapon_${attachment.weaponId}_grip`, { inheritScale: false, poseAdapter });
    const bindingPose = directBoneGrip ? gripPose : null;
    const attachmentPose = attachment.weaponModelPose || null;
    const weaponRoot = attachmentPose
      ? createPoseAnchor(THREE, parent, attachmentPose, [], {}, `weapon_${attachment.weaponId}_model`, { inheritScale: false, poseAdapter: UNITY_GLTF_PREVIEW_POSE_ADAPTER })
      : parent;
    let loaded = false;
    if (resource?.relativePath) {
      loaded = await addPreviewModelResource({
        THREE,
        previewLoaders,
        content: weaponRoot,
        pickables,
        url: (previewModelUrl || ((entry, relative, version) => packageUrl(relative, version)))(resource, resource.relativePath, getPreviewModelResourceHash(resource)),
        objectPath,
        name: attachment.weaponId,
        bindingPose,
        attachmentPose: null,
        wrapperPose: resource.importHints?.modelWrapperPose,
        bindingPoseAdapter: poseAdapter,
        attachmentPoseAdapter: poseAdapter,
        wrapperPoseAdapter: UNITY_GLTF_PREVIEW_POSE_ADAPTER,
        normalizeUnityWrapperSourceRoot: !!resource.usesUnityWrapperPrefab,
        previewUnitScale: getPreviewUnitScaleForResource(resource),
        materialBindings,
        surfacePalette
      });
    }
    if (!loaded) {
      const material = new THREE.MeshStandardMaterial({ color: 0xb46a1f, transparent: true, opacity: 0.74, emissive: 0x000000 });
      const applySelection = selected => {
        material.color.setHex(selected ? 0xffa11f : 0xb46a1f);
        material.emissive.setHex(selected ? 0x2f1a08 : 0x000000);
      };
      applySelection(state.selectedPath === objectPath);
      registerSelectionVisual?.(objectPath, applySelection);
      const mesh = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.45, 0.08), material);
      applyLocalPose(THREE, mesh, bindingPose, { poseAdapter });
      applyLocalPose(THREE, mesh, null, { poseAdapter });
      mesh.position.x += attachment.equipSlot === "offHand" ? -0.12 : 0.12;
      makeSelectable(mesh, objectPath, pickables);
      weaponRoot.add(mesh);
    }
    if (isCharacterPreviewLayerEnabled("traces")) {
      const definition = getSingleWeaponConfiguration(attachment.singleWeaponConfigurationId || attachment.weaponId);
      addTraceMeshes(THREE, weaponRoot, pickables, definition?.traceProfiles || [], registerSelectionVisual, [], {}, null, poseAdapter);
    }
  }
}

async function measureGameplayCameraFromViewport() {
  if (typeof globalThis.viewportMeasureGameplayCameraIdleFrame === "function")
    return globalThis.viewportMeasureGameplayCameraIdleFrame();
  const THREE = viewportLiveThree;
  const root = globalThis.viewportLiveBodyPreviewRoot;
  if (!THREE || !root) return null;

  return measureGameplayCameraBounds(THREE, root);
}

function measureGameplayCameraBounds(THREE, root) {
  root.updateMatrixWorld(true);
  const inverseRoot = new THREE.Matrix4().copy(root.matrixWorld).invert();
  const bounds = new THREE.Box3();
  bounds.makeEmpty();
  const point = new THREE.Vector3();
  root.traverse(object => {
    if (!object?.visible || !object.geometry || (!object.isMesh && !object.isSkinnedMesh)) return;
    const geometry = object.geometry;
    if (!geometry.boundingBox) geometry.computeBoundingBox();
    const box = geometry.boundingBox;
    if (!box || box.isEmpty()) return;
    object.updateMatrixWorld(true);
    for (let x = 0; x < 2; x++) {
      for (let y = 0; y < 2; y++) {
        for (let z = 0; z < 2; z++) {
          point.set(
            x ? box.max.x : box.min.x,
            y ? box.max.y : box.min.y,
            z ? box.max.z : box.min.z)
            .applyMatrix4(object.matrixWorld)
            .applyMatrix4(inverseRoot);
          bounds.expandByPoint(point);
        }
      }
    }
  });
  if (bounds.isEmpty()) return null;

  const center = bounds.getCenter(new THREE.Vector3());
  const size = bounds.getSize(new THREE.Vector3());
  const roundCentimeter = value => Math.round(Number(value || 0) * 100) / 100;
  const centerValue = { x: roundCentimeter(center.x), y: roundCentimeter(center.y), z: roundCentimeter(center.z) };
  const extentsValue = {
    x: roundCentimeter(size.x * 0.525),
    y: roundCentimeter(size.y * 0.525),
    z: roundCentimeter(size.z * 0.525)
  };
  return {
    center: centerValue,
    extents: extentsValue,
    sizeText: `${roundCentimeter(size.x)} × ${roundCentimeter(size.y)} × ${roundCentimeter(size.z)} m`
  };
}

function addGameplayCameraPreviewOverlay(
  THREE,
  content,
  profile,
  mode,
  aspect,
  bodyBoneRecords = [],
  poseAdapter = UNITY_GLTF_PREVIEW_POSE_ADAPTER) {
  const centerValue = profile?.framingBoundsCenter || {};
  const extentsValue = profile?.framingBoundsExtents || {};
  const center = new THREE.Vector3(Number(centerValue.x || 0), Number(centerValue.y || 0), Number(centerValue.z || 0));
  const extents = new THREE.Vector3(
    Math.max(0.01, Number(extentsValue.x || 0.01)),
    Math.max(0.01, Number(extentsValue.y || 0.01)),
    Math.max(0.01, Number(extentsValue.z || 0.01)));
  const box = new THREE.Box3(center.clone().sub(extents), center.clone().add(extents));
  const boxHelper = new THREE.Box3Helper(box, 0x35e0c1);
  boxHelper.userData.gameplayCameraPreview = true;
  content.add(boxHelper);

  const focusPosition = resolveGameplayCameraPreviewFocus(
    THREE,
    content,
    profile,
    bodyBoneRecords,
    poseAdapter) || center;
  const focus = new THREE.Mesh(
    new THREE.SphereGeometry(Math.max(0.05, extents.length() * 0.015), 12, 8),
    new THREE.MeshBasicMaterial({ color: 0xffc857, depthTest: false }));
  focus.position.copy(focusPosition);
  focus.renderOrder = 20;
  content.add(focus);
  const crosshairSize = Math.max(0.12, extents.length() * 0.04);
  const crosshair = new THREE.LineSegments(
    new THREE.BufferGeometry().setFromPoints([
      new THREE.Vector3(-crosshairSize, 0, 0),
      new THREE.Vector3(crosshairSize, 0, 0),
      new THREE.Vector3(0, -crosshairSize, 0),
      new THREE.Vector3(0, crosshairSize, 0)
    ]),
    new THREE.LineBasicMaterial({ color: 0xffc857, depthTest: false }));
  crosshair.position.copy(focusPosition);
  crosshair.renderOrder = 21;
  crosshair.userData.gameplayCameraPreview = true;
  content.add(crosshair);

  const normal = profile?.normal || {};
  const locked = profile?.locked || {};
  const distance = mode === "locked"
    ? Math.max(Number(locked.minDistance || 0), Number(normal.distance || 2))
    : Number(normal.distance || 2);
  const pitch = Number(normal.pitch || 0) * Math.PI / 180;
  const yaw = Number(normal.yawOffset || 0) * Math.PI / 180;
  const camera = new THREE.PerspectiveCamera(
    Number(normal.fieldOfView || 60),
    Math.max(0.1, Number(aspect || 1)),
    0.05,
    Math.max(100, distance * 4));
  const horizontal = Math.cos(pitch) * distance;
  camera.position.set(
    focusPosition.x - Math.sin(yaw) * horizontal,
    focusPosition.y + Math.sin(pitch) * distance,
    focusPosition.z - Math.cos(yaw) * horizontal);
  camera.lookAt(focusPosition);
  camera.updateProjectionMatrix();
  camera.updateMatrixWorld(true);
  const cameraHelper = new THREE.CameraHelper(camera);
  cameraHelper.userData.gameplayCameraPreview = true;
  content.add(camera);
  content.add(cameraHelper);
}

function resolveGameplayCameraPreviewFocus(
  THREE,
  content,
  profile,
  bodyBoneRecords = [],
  poseAdapter = UNITY_GLTF_PREVIEW_POSE_ADAPTER) {
  const abstractBoneId = String(profile?.focusAbstractBoneId || "");
  const sockets = state.package?.geometry?.sockets || [];
  const socket = sockets.find(item => String(item?.abstractBoneId || "") === abstractBoneId);
  if (!socket) return null;

  const socketsById = Object.fromEntries(sockets.map(item => [item.socketId, item]));
  const anchor = createPoseAnchor(
    THREE,
    content,
    getEffectiveSocketPose(socket),
    bodyBoneRecords,
    socketsById,
    "gameplay_camera_focus_anchor",
    { inheritScale: false, poseAdapter });
  anchor.visible = false;
  anchor.userData.gameplayCameraPreview = true;
  updateBoneParentGroups(THREE, content);
  content.updateMatrixWorld(true);
  const worldPosition = anchor.getWorldPosition(new THREE.Vector3());
  return content.worldToLocal(worldPosition);
}

function frameContent(THREE, camera, controls, content, body = {}) {
  content?.updateMatrixWorld?.(true);
  const box = new THREE.Box3();
  box.makeEmpty();
  content?.traverse?.(object => {
    if (!object?.isMesh || !object.geometry) return;
    let current = object;
    while (current && current !== content.parent) {
      if (current.visible === false) return;
      if (current === content) break;
      current = current.parent;
    }
    const objectBox = new THREE.Box3().setFromObject(object, true);
    if (!objectBox.isEmpty()) box.union(objectBox);
  });
  if (!box.isEmpty()) {
    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());
    const sphere = box.getBoundingSphere(new THREE.Sphere());
    const verticalFov = Math.max(0.01, Number(camera.fov || 42) * Math.PI / 180);
    const horizontalFov = 2 * Math.atan(Math.tan(verticalFov / 2) * Math.max(0.1, Number(camera.aspect || 1)));
    const limitingFov = Math.max(0.01, Math.min(verticalFov, horizontalFov));
    const radius = Math.max(Number(sphere.radius || 0), 0.5);
    const maxSpan = Math.max(Number(size.x || 0), Number(size.y || 0), Number(size.z || 0), 1);
    const distance = Math.max(maxSpan * 1.25, maxSpan / (2 * Math.tan(limitingFov / 2)) * 1.12);
    const viewDirection = new THREE.Vector3(0.68, 0.24, 1).normalize();
    controls.target.copy(center);
    camera.position.copy(center).addScaledVector(viewDirection, distance);
    camera.near = Math.max(0.01, distance / 500);
    camera.far = Math.max(100, distance + radius * 12);
    camera.updateProjectionMatrix();
  } else {
    controls.target.set(0, Number(body.heightMeters || 1.8) / 2, 0);
  }
  controls.update();
}

function rememberViewportCamera(camera, controls) {
  if (!camera || !controls) return;
  const cameraState = {
    position: camera.position.toArray(),
    target: controls.target.toArray()
  };
  setCurrentViewportCameraState(cameraState);
}

function restoreViewportCamera(THREE, camera, controls) {
  const saved = getCurrentViewportCameraState();
  if (!saved?.position || !saved?.target) return;
  camera.position.fromArray(saved.position);
  controls.target.fromArray(saved.target);
  controls.update();
}

function getCurrentViewportCameraState() {
  ensureViewportCameraStore();
  if (state.viewportMode === "weapon") {
    return state.viewportCameraStates.weapons[state.focusedWeaponAttachmentId || ""] || null;
  }
  return state.viewportCameraStates.character || state.viewportCameraState || null;
}

function setCurrentViewportCameraState(cameraState) {
  ensureViewportCameraStore();
  if (state.viewportMode === "weapon") {
    const key = state.focusedWeaponAttachmentId || "";
    if (key) state.viewportCameraStates.weapons[key] = cameraState;
  } else {
    state.viewportCameraStates.character = cameraState;
    state.viewportCameraState = cameraState;
  }
}

function clearCurrentViewportCameraState() {
  ensureViewportCameraStore();
  if (state.viewportMode === "weapon") {
    const key = state.focusedWeaponAttachmentId || "";
    if (key) delete state.viewportCameraStates.weapons[key];
  } else {
    state.viewportCameraStates.character = null;
    state.viewportCameraState = null;
  }
}

function ensureViewportCameraStore() {
  state.viewportCameraStates = state.viewportCameraStates || { character: null, weapons: {} };
  state.viewportCameraStates.weapons = state.viewportCameraStates.weapons || {};
}

function registerViewportTransformTarget(THREE, targetMap, entry) {
  if (!entry?.key || !entry.object || (!entry.pose && entry.transformKind !== "traceScalar")) return;
  if (entry.pose) ensurePoseObject(entry.pose, entry.defaults || {});
  const target = {
    key: entry.key,
    objectPath: entry.objectPath || "",
    object: entry.object,
    pose: entry.pose,
    poseAdapter: entry.poseAdapter || "",
    transformKind: entry.transformKind || "",
    collider: entry.collider || null,
    contactPoint: entry.contactPoint || null,
    engagementAnchor: entry.engagementAnchor || null,
    trace: entry.trace || null,
    traceScalarField: entry.traceScalarField || "",
    transformStart: null,
    label: entry.label || entry.key,
    defaults: entry.defaults || {}
  };
  targetMap.set(target.key, target);
  setWeaponTransformPick(target.object, target.key);
  if (state.pendingWeaponAutoFitKey && state.pendingWeaponAutoFitKey === target.key) {
    state.pendingWeaponAutoFitKey = "";
    setTimeout(() => { void autoFitViewportTransformTarget(THREE, target); }, 0);
  }
}

function syncWeaponTransformControl(THREE, selectedPath) {
  if (!viewportTransformControls) return;
  const target = getViewportTransformTargetForPath(selectedPath);
  if (!target) {
    viewportTransformControls.detach();
    viewportSyncHurtboxShapeHandles?.();
    renderViewportModeBar();
    return;
  }
  state.weaponTransformKey = target.key;
  if (target.transformKind === "traceScalar") {
    state.weaponTransformMode = "translate";
  } else if (target.transformKind === "contactPoint" && state.weaponTransformMode === "rotate") {
    state.weaponTransformMode = "translate";
  } else if (target.transformKind === "engagementAnchor" && state.weaponTransformMode === "scale") {
    state.weaponTransformMode = "translate";
  }
  const usesDedicatedSizeHandles = target.transformKind === "collider" && state.weaponTransformMode === "scale";
  if (usesDedicatedSizeHandles) {
    viewportTransformControls.detach();
  } else {
    viewportTransformControls.setMode(state.weaponTransformMode || "translate");
    viewportTransformControls.setSpace("local");
    viewportTransformControls.attach(target.object);
  }
  viewportSyncHurtboxShapeHandles?.();
  renderViewportModeBar();
}

function getViewportTransformTargetForPath(selectedPath) {
  const path = String(selectedPath || "");
  const current = viewportTransformTargets.get(state.weaponTransformKey || "");
  if (current && (!path || current.objectPath === path)) return current;
  const defaultKey = getDefaultWeaponTransformKeyForPath(path);
  if (defaultKey && viewportTransformTargets.has(defaultKey)) return viewportTransformTargets.get(defaultKey);
  for (const target of viewportTransformTargets.values()) {
    if (target.objectPath === path) return target;
  }
  return null;
}

function getActiveViewportTransformTarget() {
  if (state.selectedPath === "config/attributes") return null;
  if (globalThis.isBodyWorkspacePath?.()) {
    return state.selectedPath.startsWith("geometry/colliders/") ? getViewportTransformTargetForPath(state.selectedPath) : null;
  }
  return viewportTransformTargets.get(state.weaponTransformKey || "") || getViewportTransformTargetForPath(state.selectedPath || "");
}

function getDefaultWeaponTransformKeyForPath(path) {
  const target = findTarget(path || "");
  if (target.kind === "collider") return `${path}#localPose`;
  if (target.kind === "contactPoint") return `${path}#localOffsetMeters`;
  if (target.kind === "engagementAnchor") return `${path}#engagementAnchorPose`;
  if (target.kind === "socket") return `${path}#localPose`;
  if (target.kind === "resource") return `${path}#modelWrapperPose`;
  if (target.kind === "weapon") return `${path}#localGripPose`;
  if (target.kind === "trace") return `${path}#startPose`;
  return "";
}

function getWeaponTransformStatusLabel() {
  const target = getActiveViewportTransformTarget();
  const modeLabel = {
    translate: "移动",
    rotate: "旋转",
    scale: "尺寸"
  }[state.weaponTransformMode] || "移动";
  return target ? `${modeLabel} ${target.label}` : `${modeLabel}：请选择 socket、collider、contact point、接战锚点、武器握持、轨迹点或模型 wrapper`;
}

function beginViewportTransformEdit(target) {
  if (!target) return;
  target.transformStart = captureViewportTransformStart(target);
  beginUndoTransaction(`Gizmo ${target.label}`, `gizmo:${target.key}`);
}

function endViewportTransformEdit(target) {
  if (target) target.transformStart = null;
}

function captureViewportTransformStart(target) {
  const collider = target?.collider || null;
  const contactPoint = target?.contactPoint || null;
  return {
    colliderRadius: numberOrDefault(collider?.radius, 0.12),
    colliderHeight: numberOrDefault(collider?.height, 0.5),
    colliderSize: {
      x: numberOrDefault(collider?.size?.x, 0.25),
      y: numberOrDefault(collider?.size?.y, 0.25),
      z: numberOrDefault(collider?.size?.z, 0.25)
    },
    contactPointRadius: numberOrDefault(contactPoint?.radiusMeters, 0.09)
  };
}

function commitWeaponTransformTarget(THREE, target, options = {}) {
  if (!THREE || !target?.object) return;
  if (target.transformKind === "traceScalar") {
    return commitTraceScalarTransformTarget(THREE, target, options);
  }
  if (!target.pose) return;
  if (target.transformKind === "contactPoint") {
    return commitContactPointTransformTarget(THREE, target, options);
  }
  if (target.transformKind === "engagementAnchor") {
    return commitEngagementAnchorTransformTarget(THREE, target, options);
  }

  const pose = ensurePoseObject(target.pose, target.defaults || {});
  const objectPose = readObjectPoseForAdapter(THREE, target.object, target.poseAdapter);
  pose.position.x = roundTransformNumber(objectPose.position.x);
  pose.position.y = roundTransformNumber(objectPose.position.y);
  pose.position.z = roundTransformNumber(objectPose.position.z);
  const colliderSizeChanged = options.shapeHandle || applyColliderSizeFromTransform(target, objectPose);
  if (!colliderSizeChanged) {
    pose.scale.x = roundTransformNumber(objectPose.scale.x);
    pose.scale.y = roundTransformNumber(objectPose.scale.y);
    pose.scale.z = roundTransformNumber(objectPose.scale.z);
  } else {
    pose.scale.x = 1;
    pose.scale.y = 1;
    pose.scale.z = 1;
  }

  const quaternion = objectPose.rotation;
  pose.rotation = {
    x: roundTransformNumber(quaternion.x, 8),
    y: roundTransformNumber(quaternion.y, 8),
    z: roundTransformNumber(quaternion.z, 8),
    w: roundTransformNumber(quaternion.w, 8)
  };
  pose.eulerHint = {
    x: roundTransformNumber(objectPose.eulerHint.x, 4),
    y: roundTransformNumber(objectPose.eulerHint.y, 4),
    z: roundTransformNumber(objectPose.eulerHint.z, 4)
  };

  const posePath = String(target.key || "").split("#")[1] || "";
  if (!options.suppressSymmetry) {
    applySymmetryEdit(findTarget(target.objectPath || ""), posePath, { live: Boolean(options.live) });
  }
  updateUndoTransactionAfter();
  markViewportTransformDirty(target);
  if (!colliderSizeChanged) refreshViewportLiveData();
  renderShellStatus();
  if (!options.live) renderViewportModeBar();
  if (!options.live) persistCommonResourceTransformTarget(target);
  return { rebuildViewport: colliderSizeChanged };
}

function commitEngagementAnchorTransformTarget(THREE, target, options = {}) {
  const anchor = target?.engagementAnchor;
  if (!anchor || !target.pose) return;
  const objectPose = readObjectPoseForAdapter(THREE, target.object, target.poseAdapter);
  anchor.localPositionMillimeters ||= { x: 0, y: 0, z: 0 };
  anchor.localPositionMillimeters.x = Math.round(objectPose.position.x * 1000);
  anchor.localPositionMillimeters.y = Math.round(objectPose.position.y * 1000);
  anchor.localPositionMillimeters.z = Math.round(objectPose.position.z * 1000);
  let yaw = Math.round(objectPose.eulerHint.y * 1000) % 360000;
  if (yaw < 0) yaw += 360000;
  anchor.yawMilliDegrees = yaw;
  anchor.referenceFrame = "ModelRoot";
  target.pose.position = {
    x: anchor.localPositionMillimeters.x / 1000,
    y: anchor.localPositionMillimeters.y / 1000,
    z: anchor.localPositionMillimeters.z / 1000
  };
  target.pose.eulerHint = { x: 0, y: yaw / 1000, z: 0 };
  target.pose.scale = { x: 1, y: 1, z: 1 };
  updateUndoTransactionAfter();
  markViewportTransformDirty(target);
  refreshViewportLiveData();
  renderInspector();
  renderShellStatus();
  if (!options.live) renderViewportModeBar();
}

function markViewportTransformDirty(target) {
  if (String(target?.objectPath || "").startsWith("weapon-library/singleWeaponConfigurations/")) {
    state.weaponLibraryDirty = true;
    state.weaponLibraryWriteConflict = null;
    return;
  }
  refreshDirtyFromCleanSnapshot();
}

function commitTraceScalarTransformTarget(THREE, target, options = {}) {
  const trace = target?.trace;
  const field = target?.traceScalarField;
  if (!trace || !["radius", "proximalExtension", "proximalRadius"].includes(field)) return;
  const binding = target.object?.userData?.traceScalarHandle;
  const coordinateSpace = target.object.parent || viewportLiveContent;
  if (!binding?.startAnchor || !binding?.endAnchor || !coordinateSpace) return;
  const frame = readTraceAnchorFrame(THREE, binding.startAnchor, binding.endAnchor, coordinateSpace);
  const nextValue = projectTraceScalarValue(
    field,
    target.object.position,
    frame.start,
    frame.end,
    trace.proximalExtension);
  trace[field] = roundTransformNumber(nextValue);
  updateUndoTransactionAfter();
  markViewportTransformDirty(target);
  updatePoseLines(THREE, viewportLiveContent);
  viewportRefreshOverlay?.();
  renderShellStatus();
  if (!options.live) renderViewportModeBar();
  return { rebuildViewport: false };
}

function commitContactPointTransformTarget(THREE, target, options = {}) {
  const contactPoint = target?.contactPoint;
  if (!THREE || !target?.object || !contactPoint) return;

  const objectPose = readObjectPoseForAdapter(THREE, target.object, target.poseAdapter);
  const offset = ensureContactPointOffset(contactPoint);
  offset.x = roundTransformNumber(objectPose.position.x);
  offset.y = roundTransformNumber(objectPose.position.y);
  offset.z = roundTransformNumber(objectPose.position.z);
  const radiusChanged = applyContactPointRadiusFromTransform(target, objectPose);
  applySymmetryEdit(findTarget(target.objectPath || ""), "localOffsetMeters", { live: Boolean(options.live) });
  if (radiusChanged) {
    target.object.scale.set(1, 1, 1);
    applySymmetryEdit(findTarget(target.objectPath || ""), "radiusMeters", { live: Boolean(options.live) });
  }

  updateUndoTransactionAfter();
  refreshDirtyFromCleanSnapshot();
  if (!radiusChanged) refreshViewportLiveData();
  renderShellStatus();
  if (!options.live) renderViewportModeBar();
  return { rebuildViewport: radiusChanged };
}

async function autoFitViewportTransformTarget(THREE, target, targetLongestAxisMeters = 0.9) {
  if (!THREE || !target?.object || !target.pose) return false;
  target.object.updateMatrixWorld(true);
  const box = new THREE.Box3().setFromObject(target.object);
  if (box.isEmpty()) {
    state.message = "无法自动适配武器尺寸：模型 bounds 为空。";
    renderShellStatus();
    return false;
  }

  const size = box.getSize(new THREE.Vector3());
  const longest = Math.max(size.x, size.y, size.z);
  if (!Number.isFinite(longest) || longest <= 0.0001) {
    state.message = "无法自动适配武器尺寸：模型 bounds 无效。";
    renderShellStatus();
    return false;
  }

  const ratio = Math.max(0.00001, targetLongestAxisMeters / longest);
  target.object.scale.multiplyScalar(ratio);
  commitWeaponTransformTarget(THREE, target);
  renderInspector();
  refreshViewportLiveData();
  state.message = `已将武器模型最长轴自动适配到约 ${targetLongestAxisMeters.toFixed(2)}m。`;
  renderShellStatus();
  return true;
}

function persistCommonResourceTransformTarget(target) {
  const objectPath = String(target?.objectPath || "");
  if (!objectPath.startsWith("commonResources/")) return;
  if (!String(target?.key || "").endsWith("#modelWrapperPose")) return;
  const resourceKey = decodeURIComponent(objectPath.slice("commonResources/".length));
  state.message = `${resourceKey || "公共资源"} 变换由 Resource Manager 管理。`;
  renderShellStatus();
}

function applyColliderSizeFromTransform(target, objectPose) {
  const collider = target?.collider;
  if (target?.transformKind !== "collider" || !collider || state.weaponTransformMode !== "scale") return false;

  const shape = String(collider.shape || "").toLowerCase();
  if (shape !== "sphere" && shape !== "capsule" && shape !== "box") return false;

  const start = target.transformStart || captureViewportTransformStart(target);
  const scale = objectPose.scale || {};
  const sx = Math.max(0.001, Math.abs(numberOrDefault(scale.x, 1)));
  const sy = Math.max(0.001, Math.abs(numberOrDefault(scale.y, 1)));
  const sz = Math.max(0.001, Math.abs(numberOrDefault(scale.z, 1)));

  if (shape === "box") {
    collider.size = collider.size || {};
    collider.size.x = roundTransformNumber(Math.max(0.001, start.colliderSize.x * sx));
    collider.size.y = roundTransformNumber(Math.max(0.001, start.colliderSize.y * sy));
    collider.size.z = roundTransformNumber(Math.max(0.001, start.colliderSize.z * sz));
    return true;
  }

  if (shape === "sphere") {
    const radiusScale = Math.max(sx, sy, sz);
    collider.radius = roundTransformNumber(Math.max(0.001, start.colliderRadius * radiusScale));
    return true;
  }

  const nextRadius = Math.max(0.001, start.colliderRadius * Math.max(sx, sz));
  const nextHeight = Math.max(nextRadius * 2 + 0.001, start.colliderHeight * sy);
  collider.radius = roundTransformNumber(nextRadius);
  collider.height = roundTransformNumber(nextHeight);
  return true;
}

function focusSelectedViewportTransformTarget() {
  return typeof focusSelectedViewportTargetCallback === "function"
    ? Boolean(focusSelectedViewportTargetCallback())
    : false;
}

function handleViewportTransformHotkeys(event) {
  if (event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey) return;
  const source = event.target;
  const tagName = String(source?.tagName || "").toLowerCase();
  if (source?.isContentEditable || tagName === "input" || tagName === "select" || tagName === "textarea") return;
  if (!canUseViewportTransformForSelection()) return;

  const key = String(event.key || "").toLowerCase();
  if (key === "w") {
    event.preventDefault();
    setWeaponTransformMode("translate");
  } else if (key === "e" && canRotateViewportTransformSelection()) {
    event.preventDefault();
    setWeaponTransformMode("rotate");
  } else if (key === "r") {
    event.preventDefault();
    setWeaponTransformMode("scale");
  } else if (key === "f") {
    event.preventDefault();
    if (!focusSelectedViewportTransformTarget()) {
      state.message = "当前选择没有可聚焦的 3D 受击体。";
      renderShellStatus();
    }
  }
}

function applyContactPointRadiusFromTransform(target, objectPose) {
  const contactPoint = target?.contactPoint;
  if (target?.transformKind !== "contactPoint" || !contactPoint || state.weaponTransformMode !== "scale") return false;

  const start = target.transformStart || captureViewportTransformStart(target);
  const scale = objectPose.scale || {};
  const sx = Math.max(0.001, Math.abs(numberOrDefault(scale.x, 1)));
  const sy = Math.max(0.001, Math.abs(numberOrDefault(scale.y, 1)));
  const sz = Math.max(0.001, Math.abs(numberOrDefault(scale.z, 1)));
  const radiusScale = Math.max(sx, sy, sz);
  contactPoint.radiusMeters = roundTransformNumber(Math.max(0.001, start.contactPointRadius * radiusScale));
  return true;
}

function applySymmetryEdit(target, fieldPath, options = {}) {
  if (!state.symmetryEditEnabled || !state.package || !target?.value) return false;
  const path = String(fieldPath || "");
  if (!isSymmetryEditableField(target, path)) return false;

  let changed = false;
  if (target.kind === "socket") {
    changed = mirrorSocketEdit(target.value, path);
  } else if (target.kind === "weapon") {
    changed = mirrorWeaponGripEdit(target.value);
  } else if (target.kind === "collider") {
    changed = mirrorColliderEdit(target.value, path);
  } else if (target.kind === "contactPoint") {
    changed = mirrorContactPointEdit(target.value, path);
  }

  if (!changed) {
    if (!options.live) {
      state.message = "Symmetry edit could not find a paired object.";
      renderShellStatus();
    }
    return false;
  }

  if (!options.live) {
    state.message = "Mirrored edit to paired side.";
    renderShellStatus();
  }
  return true;
}

function isSymmetryEditableField(target, path) {
  if (!target?.value || !path) return false;
  if (target.kind === "socket") return path === "bonePath" || path === "localPose" || path.startsWith("localPose.");
  if (target.kind === "weapon") return path === "localGripPose" || path.startsWith("localGripPose.");
  if (target.kind === "contactPoint") {
    return path === "partId"
      || path === "localOffsetMeters"
      || path.startsWith("localOffsetMeters.")
      || path === "radiusMeters"
      || path === "layerMaskProfileId"
      || path === "usage"
      || path === "authority"
      || path === "activationMode"
      || path === "motionMode"
      || path === "motionSocketId";
  }
  if (target.kind === "collider") {
    return path === "localPose"
      || path.startsWith("localPose.")
      || path === "shape"
      || path === "radius"
      || path === "height"
      || path === "size"
      || path.startsWith("size.");
  }
  return false;
}

function mirrorSocketEdit(socket, path = "") {
  const mirror = findMirrorSocket(socket);
  if (!mirror) return false;
  const sourcePose = getMutableSocketPose(socket);
  if (path === "bonePath" && sourcePose?.parentKind === "Bone") {
    sourcePose.parentPath = socket.bonePath || "";
  } else if (path === "localPose.parentPath" && sourcePose?.parentKind === "Bone") {
    socket.bonePath = sourcePose.parentPath || socket.bonePath || "";
  }
  mirrorPose(sourcePose, getMutableSocketPose(mirror));
  if ((path === "bonePath" || path === "localPose.parentPath") && sourcePose?.parentKind === "Bone") {
    const mirroredBonePath = mirrorSideToken(socket.bonePath || sourcePose.parentPath || "");
    if (mirroredBonePath) {
      mirror.bonePath = mirroredBonePath;
      if (mirror.localPose?.parentKind === "Bone") mirror.localPose.parentPath = mirroredBonePath;
    }
  }
  return true;
}

function mirrorWeaponGripEdit(attachment) {
  const mirror = findMirrorWeaponAttachment(attachment);
  if (!mirror) return false;
  mirrorPose(getAttachmentGripPose(attachment), getAttachmentGripPose(mirror));
  return true;
}

function mirrorColliderEdit(collider, path = "") {
  const mirror = findMirrorCollider(collider);
  if (!mirror) return false;
  const mirrorPoseTarget = mirror.localPose || (mirror.localPose = defaultLocalPose("Bone", mirror.parentPath || ""));
  const mirroredInCharacterSpace = path !== "localPose.parentKind"
    && path !== "localPose.parentPath"
    && mirrorColliderPoseInCharacterSpace(collider, mirror);
  if (!mirroredInCharacterSpace) mirrorPose(collider.localPose, mirrorPoseTarget);
  mirror.shape = collider.shape || mirror.shape || "Capsule";
  if (collider.size) mirror.size = clone(collider.size);
  if (collider.radius !== undefined) mirror.radius = collider.radius;
  if (collider.height !== undefined) mirror.height = collider.height;
  return true;
}

function mirrorColliderPoseInCharacterSpace(collider, mirror) {
  const THREE = viewportLiveThree;
  const content = viewportLiveContent;
  if (!THREE || !content || !collider || !mirror) return false;
  const targets = Array.from(viewportTransformTargets.values());
  const sourceTarget = targets.find(item => item?.transformKind === "collider" && item.collider === collider);
  const mirrorTarget = targets.find(item => item?.transformKind === "collider" && item.collider === mirror);
  if (!sourceTarget?.object?.parent || !mirrorTarget?.object?.parent) return false;

  setObjectLocalPose(THREE, sourceTarget.object, collider.localPose, { poseAdapter: sourceTarget.poseAdapter });
  content.updateMatrixWorld(true);
  sourceTarget.object.updateWorldMatrix(true, false);
  mirrorTarget.object.parent.updateWorldMatrix(true, false);
  const mirroredPose = computeMirroredLocalPoseAcrossContentX(
    THREE,
    sourceTarget.object.matrixWorld,
    content.matrixWorld,
    mirrorTarget.object.parent.matrixWorld,
    mirrorTarget.poseAdapter);
  if (!mirroredPose) return false;

  const targetPose = ensurePoseObject(mirror.localPose || (mirror.localPose = defaultLocalPose("Bone", "")));
  targetPose.parentKind = collider.localPose?.parentKind || targetPose.parentKind;
  targetPose.parentPath = mirrorSideToken(collider.localPose?.parentPath || "");
  targetPose.position = clone(mirroredPose.position);
  targetPose.rotation = clone(mirroredPose.rotation);
  targetPose.scale = clone(mirroredPose.scale);
  targetPose.eulerHint = clone(mirroredPose.eulerHint);
  setObjectLocalPose(THREE, mirrorTarget.object, targetPose, { poseAdapter: mirrorTarget.poseAdapter });
  mirrorTarget.object.updateMatrixWorld(true, false);
  return true;
}

function computeMirroredLocalPoseAcrossContentX(THREE, sourceWorldMatrix, contentWorldMatrix, targetParentWorldMatrix, poseAdapter = "") {
  if (!THREE || !sourceWorldMatrix || !contentWorldMatrix || !targetParentWorldMatrix) return null;
  const contentInverse = new THREE.Matrix4().copy(contentWorldMatrix).invert();
  const sourceInContent = new THREE.Matrix4().multiplyMatrices(contentInverse, sourceWorldMatrix);
  const reflection = new THREE.Matrix4().makeScale(-1, 1, 1);
  const mirroredInContent = new THREE.Matrix4()
    .copy(reflection)
    .multiply(sourceInContent)
    .multiply(reflection);
  const targetParentInContent = new THREE.Matrix4().multiplyMatrices(contentInverse, targetParentWorldMatrix);
  const targetLocal = new THREE.Matrix4()
    .copy(targetParentInContent)
    .invert()
    .multiply(mirroredInContent);
  const poseObject = new THREE.Object3D();
  targetLocal.decompose(poseObject.position, poseObject.quaternion, poseObject.scale);
  return readObjectPoseForAdapter(THREE, poseObject, poseAdapter);
}

function mirrorContactPointEdit(contactPoint, path = "") {
  const mirror = findMirrorContactPoint(contactPoint);
  if (!mirror) return false;
  const sourceOffset = ensureContactPointOffset(contactPoint);
  const targetOffset = ensureContactPointOffset(mirror);
  targetOffset.x = roundTransformNumber(-numberOrDefault(sourceOffset.x, 0));
  targetOffset.y = roundTransformNumber(numberOrDefault(sourceOffset.y, 0));
  targetOffset.z = roundTransformNumber(numberOrDefault(sourceOffset.z, 0));
  mirror.radiusMeters = roundTransformNumber(numberOrDefault(contactPoint.radiusMeters, mirror.radiusMeters || 0.09));
  mirror.layerMaskProfileId = contactPoint.layerMaskProfileId || mirror.layerMaskProfileId || "";
  mirror.usage = contactPoint.usage || mirror.usage || "PresentationAudio";
  mirror.authority = contactPoint.authority || mirror.authority || "PresentationOnly";
  mirror.activationMode = contactPoint.activationMode || mirror.activationMode || "Always";
  mirror.motionMode = contactPoint.motionMode || mirror.motionMode || "Static";
  mirror.motionSocketId = contactPoint.motionSocketId ? mirrorSideToken(contactPoint.motionSocketId) : "";
  if (path === "partId") {
    const mirroredPartId = findMirroredBodyPartId(contactPoint.partId || "") || mirrorSideToken(contactPoint.partId || "");
    if (mirroredPartId) {
      mirror.partId = mirroredPartId;
      mirror.bodyPartId = inferContactPointBodyPartId(mirroredPartId, mirror.contactPointId);
    }
  }
  refreshContactPointLiveBinding(mirror);
  return true;
}

function mirrorPose(sourcePose, targetPose) {
  const source = ensurePoseObject(sourcePose || defaultLocalPose());
  const target = ensurePoseObject(targetPose || defaultLocalPose());
  target.parentKind = source.parentKind;
  target.parentPath = mirrorSideToken(source.parentPath || "");
  target.position.x = roundTransformNumber(-numberOrDefault(source.position?.x, 0));
  target.position.y = roundTransformNumber(numberOrDefault(source.position?.y, 0));
  target.position.z = roundTransformNumber(numberOrDefault(source.position?.z, 0));
  target.rotation.x = roundTransformNumber(numberOrDefault(source.rotation?.x, 0), 8);
  target.rotation.y = roundTransformNumber(-numberOrDefault(source.rotation?.y, 0), 8);
  target.rotation.z = roundTransformNumber(-numberOrDefault(source.rotation?.z, 0), 8);
  target.rotation.w = roundTransformNumber(numberOrDefault(source.rotation?.w, 1), 8);
  target.scale.x = roundTransformNumber(numberOrDefault(source.scale?.x, 1));
  target.scale.y = roundTransformNumber(numberOrDefault(source.scale?.y, 1));
  target.scale.z = roundTransformNumber(numberOrDefault(source.scale?.z, 1));
  target.eulerHint.x = roundTransformNumber(numberOrDefault(source.eulerHint?.x, 0), 4);
  target.eulerHint.y = roundTransformNumber(-numberOrDefault(source.eulerHint?.y, 0), 4);
  target.eulerHint.z = roundTransformNumber(-numberOrDefault(source.eulerHint?.z, 0), 4);
}

function findMirrorSocket(socket) {
  const sockets = state.package?.geometry?.sockets || [];
  const explicitId = socket?.mirrorPairSocketId || "";
  if (explicitId) {
    const explicit = sockets.find(item => item?.socketId === explicitId);
    if (explicit) return explicit;
  }
  return findMirroredItem(sockets, socket, ["socketId", "parentPartId", "bonePath"], "socketId");
}

function findMirrorWeaponAttachment(attachment) {
  const attachments = state.package?.geometry?.weaponAttachments || [];
  const mirrorSlot = mirrorEquipSlot(attachment?.equipSlot || "");
  if (mirrorSlot) {
    const bySlot = attachments.find(item => item && item !== attachment && item.equipSlot === mirrorSlot);
    if (bySlot) return bySlot;
  }
  return findMirroredItem(attachments, attachment, ["weaponId", "attachSocketId"], "weaponId");
}

function findMirrorCollider(collider) {
  const colliders = state.package?.geometry?.colliders || [];
  return findMirroredItem(colliders, collider, ["colliderId", "partId", "hitZoneId", "localPose.parentPath"], "colliderId");
}

function findMirrorContactPoint(contactPoint) {
  const contactPoints = state.package?.geometry?.bodyPartContactPoints || [];
  if (!contactPoint) return null;
  const expectedStableId = mirrorSideToken(contactPoint.stableId || "");
  const expectedPartId = mirrorSideToken(contactPoint.partId || "");
  const expectedSocketId = mirrorSideToken(contactPoint.motionSocketId || "");
  let best = null;
  let bestScore = 0;
  for (const candidate of contactPoints) {
    if (!candidate || candidate === contactPoint) continue;
    let score = 0;
    if (expectedStableId
      && expectedStableId !== contactPoint.stableId
      && candidate.stableId === expectedStableId) {
      score += 8;
    }
    if (expectedPartId
      && expectedPartId !== contactPoint.partId
      && candidate.partId === expectedPartId) {
      score += 4;
    }
    if (expectedSocketId
      && expectedSocketId !== contactPoint.motionSocketId
      && candidate.motionSocketId === expectedSocketId) {
      score += 3;
    }
    if (expectedPartId
      && candidate.bodyPartId > 0
      && candidate.bodyPartId === inferContactPointBodyPartId(expectedPartId, candidate.contactPointId)) {
      score += 1;
    }
    if (score > bestScore) {
      best = candidate;
      bestScore = score;
    }
  }
  return best || null;
}

function findMirroredItem(items, source, fields, identityField) {
  if (!source) return null;
  const candidates = [];
  for (const item of items || []) {
    if (!item || item === source) continue;
    let score = 0;
    for (const field of fields) {
      const sourceValue = getNested(source, field) || "";
      const itemValue = getNested(item, field) || "";
      if (sourceValue && itemValue && mirrorSideToken(sourceValue) === itemValue) score += field === identityField ? 4 : 2;
    }
    if (score > 0) candidates.push({ item, score });
  }
  candidates.sort((a, b) => b.score - a.score);
  return candidates[0]?.item || null;
}

function mirrorEquipSlot(slot) {
  if (slot === "mainHand") return "offHand";
  if (slot === "offHand") return "mainHand";
  return mirrorSideToken(slot);
}

function mirrorSideToken(value) {
  let text = String(value || "");
  if (!text) return text;
  const replacements = [
    [/mainHand/g, "__MX_MIRROR_MAIN_HAND__"],
    [/offHand/g, "__MX_MIRROR_OFF_HAND__"],
    [/Right/g, "__MX_MIRROR_RIGHT__"],
    [/Left/g, "__MX_MIRROR_LEFT__"],
    [/right/g, "__MX_MIRROR_RIGHT_LOWER__"],
    [/left/g, "__MX_MIRROR_LEFT_LOWER__"],
    [/\.R(?=$|[._\/-])/g, ".__MX_MIRROR_DOT_R__"],
    [/\.L(?=$|[._\/-])/g, ".__MX_MIRROR_DOT_L__"],
    [/_r(?=$|[._\/-])/g, "__MX_MIRROR_UNDERSCORE_R__"],
    [/_l(?=$|[._\/-])/g, "__MX_MIRROR_UNDERSCORE_L__"],
    [/-r(?=$|[._\/-])/g, "__MX_MIRROR_DASH_R__"],
    [/-l(?=$|[._\/-])/g, "__MX_MIRROR_DASH_L__"]
  ];
  for (const [pattern, placeholder] of replacements) {
    text = text.replace(pattern, placeholder);
  }
  return text
    .replaceAll("__MX_MIRROR_MAIN_HAND__", "offHand")
    .replaceAll("__MX_MIRROR_OFF_HAND__", "mainHand")
    .replaceAll("__MX_MIRROR_RIGHT__", "Left")
    .replaceAll("__MX_MIRROR_LEFT__", "Right")
    .replaceAll("__MX_MIRROR_RIGHT_LOWER__", "left")
    .replaceAll("__MX_MIRROR_LEFT_LOWER__", "right")
    .replaceAll("__MX_MIRROR_DOT_R__", "L")
    .replaceAll("__MX_MIRROR_DOT_L__", "R")
    .replaceAll("__MX_MIRROR_UNDERSCORE_R__", "_l")
    .replaceAll("__MX_MIRROR_UNDERSCORE_L__", "_r")
    .replaceAll("__MX_MIRROR_DASH_R__", "-l")
    .replaceAll("__MX_MIRROR_DASH_L__", "-r");
}

function setWeaponTransformPick(object, transformKey) {
  if (!object || !transformKey) return;
  object.userData.weaponTransformKey = transformKey;
  object.traverse?.(child => { child.userData.weaponTransformKey = transformKey; });
}

function makeSelectable(object, objectPath, pickables) {
  object.userData.objectPath = objectPath;
  object.traverse?.(child => { child.userData.objectPath = objectPath; });
  pickables.push(object);
}

function findObjectPath(object) {
  let cursor = object;
  while (cursor) {
    if (cursor.userData?.objectPath) return cursor.userData.objectPath;
    cursor = cursor.parent;
  }
  return "";
}

function findWeaponTransformKey(object) {
  let cursor = object;
  while (cursor) {
    if (cursor.userData?.weaponTransformKey) return cursor.userData.weaponTransformKey;
    cursor = cursor.parent;
  }
  return "";
}

function findBonePick(object) {
  let cursor = object;
  while (cursor) {
    if (cursor.userData?.bonePick) return cursor.userData.bonePick;
    cursor = cursor.parent;
  }
  return null;
}

function renderBonePicker() {
  if (!state.bonePickerOpen) return "";
  const target = findTarget(state.selectedPath);
  const fieldPath = getActiveBoneFieldPath(target);
  const bones = getBonePickerRecords();
  if (!fieldPath || bones.length === 0) return "";

  const selectedValue = getNested(target.value, fieldPath) || state.highlightedBoneValue || "";
  const layout = layoutBonePicker(bones);
  const title = getBoneFieldLabel(fieldPath);
  const searchQuery = String(state.bonePickerSearchQuery || "").trim().toLowerCase();
  const zoom = Math.min(2.5, Math.max(0.75, numberOrDefault(state.bonePickerZoom, 1)));
  const matchesSearch = record => !searchQuery || getBoneSearchText(record).includes(searchQuery);
  const searchResults = bones.filter(matchesSearch);
  const lines = layout.nodes
    .filter(node => node.parent && layout.byPath.has(node.parent))
    .map(node => {
      const parent = layout.byPath.get(node.parent);
      return `<line data-bone-link-child="${escapeHtml(node.record.path)}" x1="${parent.x}" y1="${parent.y}" x2="${node.x}" y2="${node.y}" />`;
    }).join("");
  const nodes = layout.nodes.map(node => {
    const selected = isBoneRecordSelected(node.record, selectedValue);
    const matched = matchesSearch(node.record);
    const label = getBoneMapLabel(node.record, selected || Boolean(searchQuery && matched));
    const classes = ["bone-node", selected ? "selected" : "", searchQuery ? (matched ? "search-match" : "search-dim") : ""]
      .filter(Boolean).join(" ");
    return `<g class="${classes}" data-bone-path="${escapeHtml(node.record.path)}" data-bone-alias="${escapeHtml(node.record.alias || "")}" data-bone-name="${escapeHtml(node.record.name || "")}" transform="translate(${node.x} ${node.y})"><title>${escapeHtml(node.record.path)}</title><circle r="${selected ? 5.4 : 4.2}"></circle>${label ? `<text x="9" y="4">${escapeHtml(label)}</text>` : ""}</g>`;
  }).join("");
  const resultItems = searchResults.map(record => {
    const selected = isBoneRecordSelected(record, selectedValue);
    return `<button type="button" class="bone-picker-result ${selected ? "selected" : ""}" data-bone-path="${escapeHtml(record.path)}" data-bone-alias="${escapeHtml(record.alias || "")}" data-bone-name="${escapeHtml(record.name || "")}">
      <strong>${escapeHtml(record.name || record.path)}</strong>
      <code>${escapeHtml(record.path)}</code>
    </button>`;
  }).join("");
  const svgWidth = Math.round(layout.width * zoom);
  const svgHeight = Math.round(layout.height * zoom);

  return `
    <div class="bone-picker ${layout.mode === "map" ? "bone-map" : "bone-tree"}" aria-label="Bone picker">
      <div class="bone-picker-head">
        <strong>骨骼选择器</strong>
        <span>${escapeHtml(title)}${layout.mode === "map" ? " 2D" : ""}</span>
        <button type="button" data-picker-action="closeBonePicker">关闭</button>
      </div>
      <div class="bone-picker-toolbar">
        <input type="search" data-bone-picker-search value="${escapeHtml(state.bonePickerSearchQuery || "")}" placeholder="搜索骨骼名称或路径，例如 wing_03、tail、head" aria-label="搜索骨骼">
        ${layout.mode === "map" ? `<span class="bone-picker-rotate-hint">拖动画布旋转 · <span data-bone-picker-view-label>${formatBonePickerViewLabel()}</span></span>
        <button type="button" data-bone-picker-view="reset" title="恢复正视图">正视图</button>` : ""}
        <div class="bone-picker-zoom" aria-label="骨骼图缩放">
          <button type="button" data-bone-picker-zoom="out" title="缩小">−</button>
          <button type="button" data-bone-picker-zoom="reset" title="恢复 100%">${Math.round(zoom * 100)}%</button>
          <button type="button" data-bone-picker-zoom="in" title="放大">+</button>
        </div>
      </div>
      <div class="bone-picker-body">
        <div class="bone-picker-canvas" data-bone-picker-canvas>
          <svg viewBox="0 0 ${layout.width} ${layout.height}" width="${svgWidth}" height="${svgHeight}" style="width:${svgWidth}px;height:${svgHeight}px" role="img" aria-label="骨骼图；可滚动查看">
            <g class="bone-links">${lines}</g>
            <g class="bone-nodes">${nodes}</g>
          </svg>
        </div>
        <aside class="bone-picker-results" aria-label="骨骼搜索结果">
          <div class="bone-picker-results-head"><strong>${searchQuery ? "搜索结果" : "全部骨骼"}</strong><span>${searchResults.length}${bones.length > searchResults.length ? ` / ${bones.length}` : ""}</span></div>
          <div class="bone-picker-result-list">${resultItems || `<div class="bone-picker-empty">没有匹配的骨骼</div>`}</div>
        </aside>
      </div>
    </div>`;
}

function wireBonePicker(root) {
  const picker = root.querySelector(".bone-picker");
  root.querySelectorAll("[data-picker-action='closeBonePicker']").forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      state.bonePickerOpen = false;
      renderViewport();
      renderInspector();
    });
  });
  const searchInput = root.querySelector("[data-bone-picker-search]");
  searchInput?.addEventListener("input", () => {
    state.bonePickerSearchQuery = searchInput.value || "";
    renderInspector();
    const nextInput = document.querySelector("body > .bone-picker [data-bone-picker-search]")
      || el.inspector?.querySelector?.("[data-bone-picker-search]");
    nextInput?.focus({ preventScroll: true });
    nextInput?.setSelectionRange?.(nextInput.value.length, nextInput.value.length);
  });
  root.querySelectorAll("[data-bone-picker-zoom]").forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      const action = button.dataset.bonePickerZoom;
      const current = Math.min(2.5, Math.max(0.75, numberOrDefault(state.bonePickerZoom, 1)));
      state.bonePickerZoom = action === "reset"
        ? 1
        : Math.min(2.5, Math.max(0.75, current + (action === "in" ? 0.25 : -0.25)));
      renderInspector();
    });
  });
  root.querySelectorAll("[data-bone-picker-view='reset']").forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      state.bonePickerYaw = 0;
      state.bonePickerPitch = 0;
      updateBonePickerProjection(picker);
    });
  });
  const canvas = root.querySelector("[data-bone-picker-canvas]");
  if (picker?.classList.contains("bone-map") && canvas) {
    let pointerId = null;
    let lastX = 0;
    let lastY = 0;
    let dragDistance = 0;
    canvas.addEventListener("pointerdown", event => {
      if (event.button !== 0) return;
      pointerId = event.pointerId;
      lastX = event.clientX;
      lastY = event.clientY;
      dragDistance = 0;
      canvas.setPointerCapture?.(pointerId);
      canvas.classList.add("dragging");
      event.preventDefault();
    });
    canvas.addEventListener("pointermove", event => {
      if (event.pointerId !== pointerId) return;
      const deltaX = event.clientX - lastX;
      const deltaY = event.clientY - lastY;
      lastX = event.clientX;
      lastY = event.clientY;
      dragDistance += Math.abs(deltaX) + Math.abs(deltaY);
      state.bonePickerYaw = normalizeBonePickerAngle(numberOrDefault(state.bonePickerYaw, 0) + deltaX * 0.012);
      state.bonePickerPitch = Math.min(1.45, Math.max(-1.45, numberOrDefault(state.bonePickerPitch, 0) + deltaY * 0.012));
      updateBonePickerProjection(picker);
      event.preventDefault();
    });
    const finishRotation = event => {
      if (event.pointerId !== pointerId) return;
      if (dragDistance > 4) picker.dataset.bonePickerDragged = "true";
      canvas.releasePointerCapture?.(pointerId);
      canvas.classList.remove("dragging");
      pointerId = null;
    };
    canvas.addEventListener("pointerup", finishRotation);
    canvas.addEventListener("pointercancel", finishRotation);
  }
  root.querySelectorAll("[data-bone-path]").forEach(item => {
    item.addEventListener("click", event => {
      event.stopPropagation();
      if (picker?.dataset.bonePickerDragged === "true") {
        delete picker.dataset.bonePickerDragged;
        return;
      }
      applyBonePick({
        path: item.getAttribute("data-bone-path") || "",
        alias: item.getAttribute("data-bone-alias") || "",
        name: item.getAttribute("data-bone-name") || ""
      });
    });
  });
  if (picker && picker.parentElement !== document.body) document.body.appendChild(picker);
}

function updateBonePickerProjection(picker) {
  if (!picker?.classList.contains("bone-map")) return;
  const layout = layoutBonePicker(getBonePickerRecords());
  for (const node of layout.nodes) {
    const element = Array.from(picker.querySelectorAll(".bone-node")).find(item => item.getAttribute("data-bone-path") === node.record.path);
    if (element) element.setAttribute("transform", `translate(${node.x} ${node.y})`);
  }
  picker.querySelectorAll("[data-bone-link-child]").forEach(line => {
    const node = layout.byPath.get(line.getAttribute("data-bone-link-child") || "");
    const parent = node ? layout.byPath.get(node.parent) : null;
    if (!node || !parent) return;
    line.setAttribute("x1", parent.x);
    line.setAttribute("y1", parent.y);
    line.setAttribute("x2", node.x);
    line.setAttribute("y2", node.y);
  });
  const label = picker.querySelector("[data-bone-picker-view-label]");
  if (label) label.textContent = formatBonePickerViewLabel();
}

function formatBonePickerViewLabel() {
  const yaw = Math.round(numberOrDefault(state.bonePickerYaw, 0) * 180 / Math.PI);
  const pitch = Math.round(numberOrDefault(state.bonePickerPitch, 0) * 180 / Math.PI);
  return `水平 ${yaw}° · 俯仰 ${pitch}°`;
}

function normalizeBonePickerAngle(value) {
  const fullTurn = Math.PI * 2;
  return ((value + Math.PI) % fullTurn + fullTurn) % fullTurn - Math.PI;
}

function getBonePickerRecords() {
  if (state.previewBones.length > 0) return state.previewBones;
  return normalizeOptions(bonePathOptions()).map(option => boneRecordFromPath(option.value));
}

function boneRecordFromPath(path) {
  const value = String(path || "");
  const name = value.includes("/") ? value.split("/").filter(Boolean).pop() : value.replace(/^bone\./, "");
  const parentPath = value.includes("/") ? value.split("/").slice(0, -1).join("/") : "";
  return {
    name: name || value,
    path: value,
    alias: value.startsWith("bone.") ? value : "",
    parentPath,
    depth: Math.max(0, value.split("/").length - 1),
    position: null
  };
}

function layoutBonePicker(records) {
  if (records.filter(hasBonePosition).length >= 3) return layoutBoneMap(records);
  return layoutBoneTree(records);
}

function layoutBoneMap(records) {
  const width = 640;
  const height = 640;
  const margin = 52;
  const positioned = records.filter(hasBonePosition);
  const xs = positioned.map(record => record.position.x);
  const ys = positioned.map(record => record.position.y);
  const zs = positioned.map(record => numberOrDefault(record.position.z, 0));
  const minX = Math.min(...xs);
  const maxX = Math.max(...xs);
  const minY = Math.min(...ys);
  const maxY = Math.max(...ys);
  const minZ = Math.min(...zs);
  const maxZ = Math.max(...zs);
  const rangeX = Math.max(0.001, maxX - minX);
  const rangeY = Math.max(0.001, maxY - minY);
  const rangeZ = Math.max(0.001, maxZ - minZ);
  const scale = Math.min(width - margin * 2, height - margin * 2) / Math.max(rangeX, rangeY, rangeZ) * 0.92;
  const centerX = (minX + maxX) / 2;
  const centerY = (minY + maxY) / 2;
  const centerZ = (minZ + maxZ) / 2;
  const yaw = numberOrDefault(state.bonePickerYaw, 0);
  const pitch = numberOrDefault(state.bonePickerPitch, 0);
  const cosYaw = Math.cos(yaw);
  const sinYaw = Math.sin(yaw);
  const cosPitch = Math.cos(pitch);
  const sinPitch = Math.sin(pitch);
  const nodes = records
    .filter(hasBonePosition)
    .map(record => {
      const offsetX = record.position.x - centerX;
      const offsetY = record.position.y - centerY;
      const offsetZ = numberOrDefault(record.position.z, 0) - centerZ;
      const rotatedX = offsetX * cosYaw + offsetZ * sinYaw;
      const yawDepth = -offsetX * sinYaw + offsetZ * cosYaw;
      const rotatedY = offsetY * cosPitch - yawDepth * sinPitch;
      const depth = offsetY * sinPitch + yawDepth * cosPitch;
      return {
        record,
        parent: getBoneParentPath(record, records),
        x: Math.round((width / 2 + rotatedX * scale) * 10) / 10,
        y: Math.round((height / 2 - rotatedY * scale) * 10) / 10,
        depth
      };
    });
  const byPath = new Map(nodes.map(node => [node.record.path, node]));
  return { nodes, byPath, width, height, mode: "map" };
}

function layoutBoneTree(records) {
  const nodes = records.map((record, index) => {
    const depth = Math.min(7, Number.isFinite(record.depth) ? record.depth : Math.max(0, String(record.path || "").split("/").length - 1));
    return {
      record,
      parent: getBoneParentPath(record, records),
      x: 14 + depth * 28,
      y: 18 + index * 20
    };
  });
  const byPath = new Map(nodes.map(node => [node.record.path, node]));
  const width = Math.max(220, Math.max(...nodes.map(node => node.x), 0) + 150);
  const height = Math.max(76, nodes.length * 20 + 20);
  return { nodes, byPath, width, height, mode: "tree" };
}

function hasBonePosition(record) {
  const position = record?.position;
  return Number.isFinite(position?.x) && Number.isFinite(position?.y);
}

function getBoneMapLabel(record, selected = false) {
  const side = getBoneSideLabel(record);
  const kind = getBoneKindLabel(record);
  if (selected) return `${side}${kind || record.name || "bone"}`;
  if (!isPrimaryBoneLabel(record, kind)) return "";
  return `${side}${kind}`;
}

function isPrimaryBoneLabel(record, kind) {
  const name = getBoneNameSearchText(record);
  if (kind === "hand") return /(^|[._-])hand[lr]?\b|right\s*hand|left\s*hand|righthand|lefthand|wrist/.test(name);
  if (kind === "foot") return /(^|[._-])foot[lr]?\b|right\s*foot|left\s*foot|rightfoot|leftfoot|ankle/.test(name);
  if (kind === "head" || kind === "neck") return true;
  if (kind === "chest") return /chest/.test(name);
  if (kind === "hips") return /^(hip|hips|pelvis)$/.test(name);
  return false;
}

function getBoneSideLabel(record) {
  const text = getBoneSearchText(record);
  if (/(^|[^a-z])(left|lhand|handl|lfoot|footl|lshoulder|shoulderl|larm|arml|lleg|legl|lthigh|thighl|lshin|shinl|lpelvis|pelvisl|lefthand|leftfoot)([^a-z]|$)/i.test(text)) return "L ";
  if (/(^|[^a-z])(right|rhand|handr|rfoot|footr|rshoulder|shoulderr|rarm|armr|rleg|legr|rthigh|thighr|rshin|shinr|rpelvis|pelvisr|righthand|rightfoot)([^a-z]|$)/i.test(text)) return "R ";
  return "";
}

function getBoneKindLabel(record) {
  const text = getBoneNameSearchText(record) || getBoneSearchText(record);
  if (/head|skull/.test(text)) return "head";
  if (/neck/.test(text)) return "neck";
  if (/finger|thumb|pinky|ring|index|middle/.test(text)) return "finger";
  if (/hand|wrist|palm/.test(text)) return "hand";
  if (/foot|ankle|toe|heel/.test(text)) return "foot";
  if (/chest|breast/.test(text)) return "chest";
  if (/hip|hips|pelvis/.test(text)) return "hips";
  if (/spine/.test(text)) return "spine";
  return "";
}

function getBoneSearchText(record) {
  return `${record?.name || ""} ${record?.path || ""} ${record?.alias || ""}`.toLowerCase();
}

function getBoneNameSearchText(record) {
  return `${record?.name || ""} ${record?.alias || ""}`.toLowerCase();
}

function getBoneParentPath(record, records) {
  if (record.parentPath) return record.parentPath;
  const paths = new Set(records.map(item => item.path));
  const parts = String(record.path || "").split("/");
  while (parts.length > 1) {
    parts.pop();
    const parent = parts.join("/");
    if (paths.has(parent)) return parent;
  }
  return "";
}

function getActiveBoneFieldPath(target) {
  if (state.activeBoneFieldPath && isBoneFieldPathForTarget(target, state.activeBoneFieldPath)) {
    return state.activeBoneFieldPath;
  }
  return getDefaultBoneFieldPath(target);
}

function getDefaultBoneFieldPath(target) {
  if (!target?.value) return "";
  if (target.kind === "part") return "bonePath";
  if (target.kind === "socket") return "bonePath";
  if (target.kind === "collider" && target.value.localPose?.parentKind === "Bone") return "localPose.parentPath";
  if (target.kind === "weapon" && target.value.localGripPose?.parentKind === "Bone") return "localGripPose.parentPath";
  if (target.kind === "trace") {
    if (target.value.startPose?.parentKind === "Bone") return "startPose.parentPath";
    if (target.value.endPose?.parentKind === "Bone") return "endPose.parentPath";
  }
  return "";
}

function isBoneFieldPathForTarget(target, fieldPath) {
  if (!target?.value || !fieldPath) return false;
  if (fieldPath === "bonePath") return target.kind === "part" || target.kind === "socket";
  if (fieldPath.endsWith(".parentPath")) {
    const posePath = fieldPath.slice(0, -".parentPath".length);
    return getNested(target.value, `${posePath}.parentKind`) === "Bone";
  }
  return false;
}

function activateBoneField(fieldPath, open = true) {
  if (!fieldPath) return;
  const target = findTarget(state.selectedPath);
  const highlightedValue = getNested(target.value, fieldPath) || "";
  const shouldRefreshInspector = state.activeBoneFieldPath !== fieldPath || state.bonePickerOpen !== open;
  const shouldRestoreFocus = shouldRefreshInspector && document.activeElement?.dataset?.field === fieldPath;
  const shouldRefreshViewport = shouldRefreshInspector || state.highlightedBoneValue !== highlightedValue;
  state.activeBoneFieldPath = fieldPath;
  state.bonePickerOpen = open;
  state.highlightedBoneValue = highlightedValue;
  if (shouldRefreshViewport) renderViewport();
  if (shouldRefreshInspector) {
    renderInspector();
    if (shouldRestoreFocus) {
      const nextInput = Array.from(el.inspector.querySelectorAll("[data-field]")).find(input => input.dataset.field === fieldPath);
      nextInput?.focus({ preventScroll: true });
    }
  }
}

function applyBonePick(pick) {
  const target = findTarget(state.selectedPath);
  const fieldPath = getActiveBoneFieldPath(target);
  if (!fieldPath || !target.value) return;
  const value = getBoneValueForField(fieldPath, pick);
  setNested(target.value, fieldPath, value);
  afterInspectorFieldEdited(target, fieldPath);
  applySymmetryEdit(target, fieldPath);
  state.activeBoneFieldPath = fieldPath;
  state.bonePickerOpen = true;
  state.highlightedBoneValue = value;
  state.dirty = true;
  renderShellStatus();
  renderViewport();
  renderInspector();
}

function getBoneValueForField(fieldPath, pick) {
  if (fieldPath.endsWith(".parentPath")) return pick.alias || pick.path || pick.name || "";
  return pick.path || pick.alias || pick.name || "";
}

function getCurrentBoneSelectionValue() {
  const target = findTarget(state.selectedPath);
  const fieldPath = getActiveBoneFieldPath(target);
  return fieldPath ? (getNested(target.value, fieldPath) || state.highlightedBoneValue || "") : state.highlightedBoneValue;
}

function isBoneRecordSelected(record, selectedValue) {
  const value = String(selectedValue || "");
  if (!value) return false;
  if (value === record.path || value === record.alias || value === record.name) return true;
  if (state.previewBones.some(bone => value === bone.path || value === bone.alias || value === bone.name)) return false;
  const selectedKind = getBoneKindLabel({ name: value, path: value, alias: value });
  if (!selectedKind) return false;
  const selectedSide = getBoneSideLabel({ name: value, path: value, alias: value });
  return selectedKind === getBoneKindLabel(record) && selectedSide === getBoneSideLabel(record);
}

function getBoneFieldLabel(fieldPath) {
  if (fieldPath === "bonePath") return "Bone path";
  if (fieldPath.endsWith(".parentPath")) return "Parent bone path";
  return "Bone";
}

function toVector3(THREE, value = {}) {
  return new THREE.Vector3(Number(value.x || 0), Number(value.y || 0), Number(value.z || 0));
}

Object.assign(globalThis, {
  confirmHurtboxAuthoring,
  normalizeHurtboxDisplayMode,
  getHurtboxDisplayParts,
  getSelectedHurtboxPartId,
  ensureHurtboxDisplayPartId,
  syncHurtboxDisplayPartToSelection,
  getVisibleHurtboxColliders,
  isHurtboxCoverageScopeAvailable,
  MODEL_WRAPPER_SCALE_PRESETS,
  buildWeaponVisualizationItems,
  getDefaultWeaponSlotList,
  getDefaultWeaponSlotSet,
  weaponIssue,
  getEquipSlotLabel,
  getWeaponVisualizationTone,
  getWeaponVisualizationStatusText,
  renderWeaponDataOverlayHtml,
  hasEmbeddedWeaponSource,
  getConfiguredModelNodePaths,
  getEmbeddedWeaponNodePaths,
  getHiddenRendererNodePaths,
  getRendererMaterialBindings,
  getPreviewHiddenNodePaths,
  hidePreviewModelNodes,
  hideEmbeddedPreviewModelNodes,
  renderWeaponDataOverlayItem,
  renderWeaponDataValue,
  wireWeaponDataOverlay,
  findPreviewModelResourceByKey,
  buildCommonPreviewModelResource,
  getPreviewModelResourceObjectPath,
  getModelScaleDiagnostic,
  shouldShowModelScaleRecommendation,
  formatModelScaleRecommendation,
  renderModelWrapperScalePresetButtons,
  applyRecommendedModelScale,
  applyRecommendedModelScaleForResourcePath,
  applyModelWrapperScalePreset,
  applyModelWrapperScalePresetForResourcePath,
  editWeaponModelTransformForWeapon,
  enterSingleWeaponViewport,
  autoFitWeaponModelForWeapon,
  refreshViewportOverlay,
  refreshViewportLiveData,
  registerViewportPoseBinding,
  registerViewportContactPointBinding,
  setObjectLocalPose,
  bindModelResource,
  clearCurrentModelBinding,
  createConfiguration,
  ensureGeometryCollections,
  ensureBodyProfile,
  createBodyPartConfig,
  createColliderConfig,
  createSocketConfig,
  createWeaponConfig,
  createTraceConfig,
  createContactPointConfig,
  getDefaultContactPointPartId,
  nextNumericId,
  inferContactPointBodyPartId,
  defaultLocalPose,
  nextEquipSlot,
  uniqueConfigId,
  ensureModelWrapperPose,
  ensureLocalPose,
  ensurePoseObject,
  bindBodyModelResource,
  bindWeaponSlotResource,
  findModelResourceByKey,
  ensureApplicationResourceKey,
  removeApplicationResourceKey,
  isBodyModelBinding,
  isApplicationResourceReference,
  findWeaponPathBySlot,
  touchModelResource,
  addTagValue,
  hasTag,
  removeTag,
  renderPreviewControls,
  renderViewportModeBar,
  canUseViewportTransformForSelection,
  setWeaponTransformMode,
  toggleSymmetryEdit,
  synchronizeVisibleColliderSymmetryFromLeft,
  isViewportTransformHandleActive,
  getCurrentOrFirstActiveWeaponAttachment,
  getFocusedWeaponAttachment,
  getWeaponAttachmentIdentity,
  getWeaponAttachmentPath,
  findWeaponAttachmentById,
  enterWeaponViewport,
  returnToCharacterViewport,
  teardownViewportForModeChange,
  renderViewport,
  renderThreeViewport,
  renderSvgViewport,
  renderSvgWeaponViewport,
  loadThreeRuntime,
  createPreviewModelLoaders,
  getPreviewModelFormat,
  isPreviewModelPath,
  getDefaultPoseAdapterForPreviewFormat,
  getPoseAdapterForPreviewFormat,
  resolvePreviewModelRelativePaths,
  shouldPreferSourceFbxPreview,
  buildSourceFbxPreviewPaths,
  findCatalogPreviewModelPaths,
  buildSiblingPreviewModelPaths,
  resolveGltfPreviewRelativePaths,
  findCatalogGltfPreviewPaths,
  buildSiblingGltfPreviewPaths,
  isGltfPreviewPath,
  isFbxPreviewPath,
  loadPreviewModel,
  normalizePreviewMaterialName,
  buildPackagePreviewMaterialBindings,
  loadPackagePreviewMaterialBindings,
  resolvePackagePreviewSurfaceSettings,
  getPackagePreviewAnimationCandidates,
  loadPackagePreviewAnimationMixer,
  findPreviewMaterialBinding,
  findPreviewRendererMaterialBinding,
  applyPreviewMaterialBindings,
  addPreviewModelResource,
  normalizePreviewSourceRootForUnityWrapper,
  applyPreviewModelUnitScale,
  addGltfResource,
  disposePreviewObject,
  collectBoneRecords,
  getPreviewBonePath,
  syncPreviewBones,
  addBoneGizmos,
  setBonePickData,
  applyPreviewPose,
  resetPreviewBonePose,
  applyPreviewMotion,
  findPreviewBoneBySemantic,
  getPreviewBoneSemanticKey,
  getPreviewBoneSemanticKeys,
  applyLocalPose,
  getPreviewPoseForAdapter,
  convertUnityPoseToPreviewPose,
  readObjectPoseForAdapter,
  getEffectiveSocketPose,
  getMutableSocketPose,
  getSocketBoneParentCandidates,
  getSocketPoseNodePaths,
  resolvePoseParentContent,
  updateBoneParentGroups,
  syncBoneParentGroup,
  syncSemanticBoneParentGroup,
  getSemanticBoneFrameDirection,
  findSemanticFrameChildBone,
  getSemanticFrameChildKeys,
  normalizeFrameDirection,
  projectFrameAxis,
  getFallbackFrameXAxis,
  findBoneRecordForPose,
  findBoneRecordForValue,
  getBoneRecordMatchRank,
  getBoneMatchKeys,
  normalizeBoneMatchText,
  getSemanticBoneKey,
  getBoneSemanticKeys,
  getSemanticFrameBoneKey,
  isSegmentBoneSemanticKey,
  isBroadBoneSemanticKey,
  getSemanticBoneRecordRank,
  isPrimarySemanticBoneRecord,
  degreesToRadians,
  radiansToDegrees,
  roundTransformNumber,
  numberOrDefault,
  refreshMovementCollisionPreview,
  attachMovementCollisionPreview,
  refreshBodyMeasurementPreview,
  attachBodyMeasurementPreview,
  addFallbackBody,
  addColliderMeshes,
  getHurtboxShapeHandleDescriptors,
  computeHurtboxShapeHandleEdit,
  applyHurtboxShapeHandleEdit,
  rotateHurtboxCenterShift,
  getHurtboxHandleLocalPosition,
  restoreColliderFromSnapshot,
  getHurtboxBindingReferenceStatus,
  computeHurtboxBindingReferenceMatrices,
  selectHurtboxBindingReferenceNeighbors,
  getHurtboxBindingReferenceGeometrySignature,
  addContactPointMeshes,
  createContactPointPreviewPose,
  ensureContactPointOffset,
  getContactPointObjectPath,
  getContactPointColor,
  addSocketMeshes,
  addEngagementAnchorMeshes,
  addTraceMeshes,
  addWeaponDataMeshes,
  addFocusedWeaponViewportContent,
  getWeaponVisualizationItemForWeapon,
  addFocusedWeaponFallback,
  addWeaponGripReference,
  addAxisLines,
  normalizeWeaponPreviewTrace,
  normalizeWeaponPreviewPose,
  renderWeaponFocusOverlayHtml,
  wireWeaponFocusOverlay,
  createFallbackWeaponDataAnchor,
  getAttachmentGripPose,
  createPoseAnchor,
  resolvePoseParentContentAdvanced,
  addDynamicLineBetweenAnchors,
  updatePoseLines,
  createTextSprite,
  roundedRect,
  addWeaponMeshes,
  measureGameplayCameraFromViewport,
  addGameplayCameraPreviewOverlay,
  frameContent,
  rememberViewportCamera,
  restoreViewportCamera,
  getCurrentViewportCameraState,
  setCurrentViewportCameraState,
  clearCurrentViewportCameraState,
  ensureViewportCameraStore,
  registerViewportTransformTarget,
  syncWeaponTransformControl,
  getViewportTransformTargetForPath,
  getActiveViewportTransformTarget,
  getDefaultWeaponTransformKeyForPath,
  canRotateViewportTransformSelection,
  autoFitViewportTransformTarget,
  persistCommonResourceTransformTarget,
  getWeaponTransformStatusLabel,
  beginViewportTransformEdit,
  endViewportTransformEdit,
  captureViewportTransformStart,
  commitWeaponTransformTarget,
  commitContactPointTransformTarget,
  applyColliderSizeFromTransform,
  focusSelectedViewportTransformTarget,
  handleViewportTransformHotkeys,
  applyContactPointRadiusFromTransform,
  applySymmetryEdit,
  isSymmetryEditableField,
  mirrorSocketEdit,
  mirrorWeaponGripEdit,
  mirrorColliderEdit,
  mirrorContactPointEdit,
  mirrorPose,
  findMirrorSocket,
  findMirrorWeaponAttachment,
  findMirrorCollider,
  findMirrorContactPoint,
  findMirroredItem,
  mirrorEquipSlot,
  mirrorSideToken,
  setWeaponTransformPick,
  makeSelectable,
  findObjectPath,
  findWeaponTransformKey,
  findBonePick,
  renderBonePicker,
  wireBonePicker,
  getBonePickerRecords,
  boneRecordFromPath,
  layoutBonePicker,
  layoutBoneMap,
  layoutBoneTree,
  hasBonePosition,
  getBoneMapLabel,
  isPrimaryBoneLabel,
  getBoneSideLabel,
  getBoneKindLabel,
  getBoneSearchText,
  getBoneNameSearchText,
  getBoneParentPath,
  getActiveBoneFieldPath,
  getDefaultBoneFieldPath,
  isBoneFieldPathForTarget,
  activateBoneField,
  applyBonePick,
  getBoneValueForField,
  getCurrentBoneSelectionValue,
  isBoneRecordSelected,
  getBoneFieldLabel,
  toVector3,
  invalidateSurfacePalettePreviewUsage,
  getSurfacePaletteUsageGeneration,
  validateSurfacePaletteUsageOwner,
  buildSurfacePaletteUsageSource,
  publishSurfacePalettePreviewUsage
});

export {
  applySurfacePalettePreviewMaterials,
  buildSurfacePaletteUsageFromPreview,
  invalidateSurfacePalettePreviewUsage,
  getSurfacePaletteUsageGeneration,
  validateSurfacePaletteUsageOwner,
  buildSurfacePaletteUsageSource,
  publishSurfacePalettePreviewUsage,
  buildPackagePreviewMaterialBindings,
  collectBoneRecords,
  computeTraceSpatialFrame,
  findPreviewMaterialBinding,
  getPackagePreviewAnimationCandidates,
  getConfiguredModelNodePaths,
  getEmbeddedWeaponNodePaths,
  getHiddenRendererNodePaths,
  getRendererMaterialBindings,
  getPreviewHiddenNodePaths,
  hasEmbeddedWeaponSource,
  hidePreviewModelNodes,
  hideEmbeddedPreviewModelNodes,
  getSocketPoseNodePaths,
  normalizePreviewSourceRootForUnityWrapper,
  normalizePreviewMaterialName,
  normalizeSurfacePaletteBinding,
  resolvePackagePreviewSurfaceSettings,
  findPreviewRendererMaterialBinding,
  projectTraceScalarValue,
  getHurtboxShapeHandleDescriptors,
  computeHurtboxShapeHandleEdit,
  applyHurtboxShapeHandleEdit,
  rotateHurtboxCenterShift,
  getHurtboxHandleLocalPosition,
  restoreColliderFromSnapshot,
  getHurtboxBindingReferenceStatus,
  computeHurtboxBindingReferenceMatrices,
  selectHurtboxBindingReferenceNeighbors,
  getHurtboxBindingReferenceGeometrySignature,
  mirrorSideToken,
  computeMirroredLocalPoseAcrossContentX,
  isLeftSideMirrorSource
};
