// CharacterStudio split module. Registers functions on globalThis to preserve the original call graph.

function normalizeImportMaterialsPayload(payload) {
  const statePayload = payload || {};
  const document = normalizeImportMaterialsDocument(statePayload.document || statePayload);
  const inferredSourceManifestExists = statePayload.sourceManifestExists ?? (statePayload.documentKind === "sourceManifest" && Boolean(statePayload.sourceManifestRelative || statePayload.documentRelative));
  const sourceManifestExists = Boolean(inferredSourceManifestExists);
  const items = Array.isArray(statePayload.items)
    ? statePayload.items.map((item, index) => normalizeImportMaterialStatus(item, document.items[index], index))
    : document.items.map((item, index) => normalizeImportMaterialStatus({ item, status: "Unknown" }, item, index));
  const checkedSourceRoot = statePayload.checkedSourceRoot ?? document.sourceRoot;
  if (typeof importMaterialSourceIdentity === "function") for (const status of items) {
    if (!Object.prototype.hasOwnProperty.call(status, "checkedSourceIdentity")) status.checkedSourceIdentity = importMaterialSourceIdentity(status.item, checkedSourceRoot);
  }
  const integrityReceipt = normalizeImportMaterialsIntegrityReceipt(statePayload.integrityReceipt);
  return {
    ...statePayload,
    document,
    checkedSourceRoot,
    sourceManifestRelative: statePayload.sourceManifestRelative || statePayload.documentRelative || "",
    sourceManifestExists,
    sourceManifestItemCount: sourceManifestExists ? Number(statePayload.sourceManifestItemCount ?? document.items.length) : 0,
    stateRelative: statePayload.stateRelative || "",
    legacyDocumentRelative: statePayload.legacyDocumentRelative || "",
    legacyDocumentExists: Boolean(statePayload.legacyDocumentExists) || statePayload.documentKind === "legacyPackageDocument",
    documentKind: statePayload.documentKind || "",
    items,
    integrityReceipt,
    summary: statePayload.summary || summarizeImportMaterialStatuses(items, integrityReceipt),
    templates: Array.isArray(statePayload.templates) && statePayload.templates.length ? statePayload.templates : IMPORT_MATERIAL_TEMPLATES
  };
}

function normalizeImportMaterialsIntegrityReceipt(receipt) {
  const available = Boolean(receipt && typeof receipt === "object");
  const diagnostics = Array.isArray(receipt?.diagnostics) ? receipt.diagnostics.map(item => ({
    code: item?.code || "IMPORT_MATERIALS_INTEGRITY",
    severity: item?.severity || "Info",
    itemId: item?.itemId || "",
    resourceKey: item?.resourceKey || "",
    path: item?.path || "",
    message: item?.message || "",
    suggestedAction: item?.suggestedAction || ""
  })) : available ? [] : [{
    code: "INTEGRITY_RECEIPT_UNAVAILABLE",
    severity: "Error",
    itemId: "",
    resourceKey: "",
    path: "config/import_materials_state.json",
    message: "Authoring 服务没有返回原材料完整性回执。",
    suggestedAction: "重启 Authoring 服务后重新检查；缺少回执不能视为原材料导入成功。"
  }];
  return {
    available,
    schemaVersion: receipt?.schemaVersion || "1.0",
    status: available ? (receipt?.status || "NotApplicable") : "Unavailable",
    packageId: receipt?.packageId || "",
    sourceManifestRelative: receipt?.sourceManifestRelative || "",
    evaluatedUtc: receipt?.evaluatedUtc || "",
    sourceItemCount: Number(receipt?.sourceItemCount || 0),
    textureCount: Number(receipt?.textureCount || 0),
    expectedMaterialCount: Number(receipt?.expectedMaterialCount || 0),
    boundMaterialCount: Number(receipt?.boundMaterialCount || 0),
    rendererBindingCount: Number(receipt?.rendererBindingCount || 0),
    blockingIssueCount: Number(receipt?.blockingIssueCount ?? diagnostics.filter(item => item.severity === "Error").length),
    warningCount: Number(receipt?.warningCount ?? diagnostics.filter(item => item.severity === "Warning").length),
    diagnostics,
    materials: Array.isArray(receipt?.materials) ? receipt.materials.map(material => ({
      ...material,
      sourceMaterialName: material?.sourceMaterialName || "",
      materialResourceKey: material?.materialResourceKey || "",
      descriptorRelativePath: material?.descriptorRelativePath || "",
      surfaceMode: ["Opaque", "AlphaClip"].find(value => value.toLowerCase() === String(material?.surfaceMode || "Opaque").toLowerCase()) || material?.surfaceMode || "Opaque",
      alphaCutoff: Number.isFinite(Number(material?.alphaCutoff)) ? Number(material.alphaCutoff) : 0.5
    })) : []
  };
}

function normalizeImportMaterialsDocument(document) {
  const result = {
    schemaVersion: document?.schemaVersion || "1.0",
    sourceRoot: normalizeImportMaterialPath(document?.sourceRoot || ""),
    items: Array.isArray(document?.items) ? document.items.map((item, index) => normalizeImportMaterialItem(item, index)) : []
  };
  const used = new Set();
  for (const item of result.items) {
    let id = item.id || buildImportMaterialItemId(item);
    const base = id;
    let suffix = 2;
    while (used.has(id.toLowerCase())) id = `${base}-${suffix++}`;
    used.add(id.toLowerCase());
    item.id = id;
  }
  return result;
}

function normalizeImportMaterialItem(item, index = 0) {
  const source = normalizeImportMaterialSourceRef(item?.source);
  const target = normalizeImportMaterialTargetRef(item?.target);
  const sourcePath = normalizeImportMaterialPath(item?.sourcePath || (source.kind === "file" ? source.path : ""));
  const presetId = item?.presetId || inferImportMaterialPresetId(sourcePath, item?.bindTarget || target.bindTarget || "");
  return {
    id: item?.id || "",
    label: item?.label || importMaterialDefaultLabel(item?.sourcePath, index),
    sourcePath,
    source,
    target,
    presetId,
    slot: item?.slot || target.slot || "",
    resourceKey: item?.resourceKey || target.resourceKey || "",
    bindTarget: item?.bindTarget || target.bindTarget || inferImportMaterialBindTarget(presetId),
    textureSemantic: item?.textureSemantic || target.textureSemantic || "",
    materialSurfaceMode: target.materialSurfaceMode || "",
    alphaCutoff: target.alphaCutoff,
    required: item?.required !== false,
    lastImportedHash: item?.lastImportedHash || "",
    lastImportedUtc: item?.lastImportedUtc || ""
  };
}

function normalizeImportMaterialStatus(status, fallbackItem, index) {
  const item = normalizeImportMaterialItem(status?.item || fallbackItem || {}, index);
  return {
    ...status,
    index,
    id: status?.id || item.id,
    label: status?.label || item.label,
    item,
    status: status?.status || "Unknown",
    message: status?.message || "",
    sourceExists: Boolean(status?.sourceExists),
    sourceDirty: Boolean(status?.sourceDirty),
    resourceExists: Boolean(status?.resourceExists),
    unitySynced: Boolean(status?.unitySynced)
  };
}

function normalizeImportMaterialSourceCandidatesPayload(payload, sourceRoot = "", query = "") {
  const items = Array.isArray(payload?.items) ? payload.items.map(normalizeImportMaterialSourceCandidate) : [];
  return {
    success: payload?.success !== false,
    packageRelative: payload?.packageRelative || state.packageRelative,
    sourceRoot: normalizeImportMaterialPath(payload?.sourceRoot ?? sourceRoot),
    query: payload?.query ?? query,
    truncated: Boolean(payload?.truncated),
    roots: Array.isArray(payload?.roots) ? payload.roots.map(normalizeImportMaterialPath) : [],
    items,
    diagnostics: Array.isArray(payload?.diagnostics) ? payload.diagnostics : []
  };
}

function normalizeImportMaterialSourceCandidate(candidate) {
  return {
    id: candidate?.id || normalizeIdSegment(candidate?.displayPath || candidate?.sourcePath || "candidate"),
    sourcePath: normalizeImportMaterialPath(candidate?.sourcePath || ""),
    displayPath: normalizeImportMaterialPath(candidate?.displayPath || candidate?.sourcePath || ""),
    fileName: candidate?.fileName || String(candidate?.sourcePath || "").split(/[\\/]/).pop() || "",
    extension: String(candidate?.extension || "").replace(/^\./, "").toLowerCase(),
    sourceKind: candidate?.sourceKind || "",
    sourceScope: candidate?.sourceScope || "package",
    presetId: candidate?.presetId || inferImportMaterialPresetId(candidate?.sourcePath || ""),
    slotKey: candidate?.slotKey || "",
    bindTarget: candidate?.bindTarget || "",
    exists: candidate?.exists !== false,
    selectable: candidate?.selectable !== false,
    reason: candidate?.reason || "",
    diagnostics: Array.isArray(candidate?.diagnostics) ? candidate.diagnostics : []
  };
}

function isImportMaterialResourceRefItem(item) {
  return String(item?.source?.kind || "").toLowerCase() === "resourceref";
}

function hasImportMaterialSourceConfigured(item) {
  if (!item) return false;
  if (isImportMaterialResourceRefItem(item)) return Boolean(getResourceSelectionKey(item.source?.selection));
  return Boolean(normalizeImportMaterialPath(item.sourcePath || item.source?.path || ""));
}

function summarizeImportMaterialStatuses(items, integrityReceipt = null) {
  const summary = { total: items.length, clean: 0, changed: 0, notImported: 0, missing: 0, unityPending: 0, blocked: 0, dirty: 0 };
  for (const item of items) {
    if (item.status === "Clean") summary.clean++;
    else if (item.status === "Changed") summary.changed++;
    else if (item.status === "NotImported" || item.status === "NeedsCheck" || item.status === "Draft" || item.status === "Unknown") summary.notImported++;
    else if (item.status === "Missing") summary.missing++;
    else if (item.status === "Optional" || item.status === "OptionalMissing") continue;
    else if (item.status === "UnityPending") summary.unityPending++;
    else summary.blocked++;
  }
  summary.dirty = summary.changed + summary.notImported;
  summary.integrityStatus = integrityReceipt?.status || "NotApplicable";
  summary.integrityBlocking = Number(integrityReceipt?.blockingIssueCount || 0);
  summary.integrityWarnings = Number(integrityReceipt?.warningCount || 0);
  summary.blocked += summary.integrityBlocking;
  return summary;
}

function createEmptyImportMaterialsDocument() {
  return { schemaVersion: "1.0", sourceRoot: "", items: [] };
}

function convertCharacterSourceManifestToImportMaterialsDocument(manifest, manifestRelative) {
  const sourceRoot = normalizeImportMaterialPath(String(manifestRelative || "").replace(/\/character_sources\.json$/i, ""));
  return normalizeImportMaterialsDocument({
    schemaVersion: manifest?.schemaVersion || "1.0",
    sourceRoot,
    items: Array.isArray(manifest?.items) ? manifest.items.map(item => {
      const source = normalizeImportMaterialSourceRef(item?.source);
      const target = normalizeImportMaterialTargetRef(item?.target);
      return {
        id: item?.id || "",
        label: item?.label || "",
        sourcePath: source.kind === "file" ? source.path : "",
        source,
        target,
        presetId: item?.presetId || "",
        slot: item?.slot || target.slot || "",
        resourceKey: target.resourceKey || "",
        bindTarget: item?.bindTarget || target.bindTarget || "",
        textureSemantic: target.textureSemantic || "",
        materialSurfaceMode: target.materialSurfaceMode || "",
        alphaCutoff: target.alphaCutoff,
        required: item?.required !== false
      };
    }) : []
  });
}

function normalizeImportMaterialPath(path) {
  return String(path || "").replace(/\\/g, "/").trim().replace(/^\/+/, "");
}

function inferImportMaterialSourceScope(sourcePath) {
  const normalized = normalizeImportMaterialPath(sourcePath).toLowerCase();
  return normalized === "artsource/common" || normalized.startsWith("artsource/common/") ? "public" : "package";
}

function buildImportMaterialItemId(item) {
  return normalizeIdSegment(item.label || item.bindTarget || item.slot || item.sourcePath || "material");
}

function normalizeIdSegment(value) {
  const normalized = String(value || "item").toLowerCase().replace(/[^a-z0-9._-]+/g, "-").replace(/^-+|-+$/g, "");
  return normalized || "item";
}

function importMaterialDefaultLabel(sourcePath, index) {
  const fileName = String(sourcePath || "").split(/[\\/]/).pop() || "";
  const stem = fileName.replace(/\.[^.]+$/, "");
  return stem || `Material ${index + 1}`;
}

function inferImportMaterialPresetId(sourcePath, bindTarget = "") {
  if (bindTarget === "body") return "characterModel";
  if (bindTarget === "faceDetail") return "faceDetailTexture";
  const extension = String(sourcePath || "").split(".").pop()?.toLowerCase() || "";
  if (["glb", "gltf", "fbx"].includes(extension)) return "modelPreview";
  if (extension === "anim") return "animationClipGroup";
  if (["png", "jpg", "jpeg", "tga", "exr", "psd"].includes(extension)) return "texture";
  if (["wav", "ogg"].includes(extension)) return "audioCue";
  return "config";
}

function inferImportMaterialBindTarget(presetId) {
  const preset = IMPORT_MATERIAL_PRESETS.find(item => item.id === presetId);
  return preset?.bindTarget || "";
}

function getImportMaterialSlotDefinition(slotKey) {
  return IMPORT_MATERIAL_SLOT_DEFINITIONS.find(item => item.key === slotKey) || null;
}

function createImportMaterialSlotItem(definition, sourcePath = "") {
  return normalizeImportMaterialItem({
    id: definition.id,
    label: definition.label,
    sourcePath,
    presetId: definition.presetId,
    slot: definition.slot || "",
    bindTarget: definition.bindTarget || "",
    required: definition.required !== false
  });
}

function findImportMaterialSlotItem(document, definition) {
  if (!document || !definition) return null;
  const items = Array.isArray(document.items) ? document.items : [];
  return items.find(item => item.id === definition.id)
    || (definition.bindTarget ? items.find(item => item.bindTarget === definition.bindTarget) : null)
    || (definition.slot ? items.find(item => item.slot === definition.slot && item.presetId === definition.presetId) : null)
    || null;
}

function ensureImportMaterialSlotItem(slotKey) {
  const definition = getImportMaterialSlotDefinition(slotKey);
  if (!definition) return null;
  const document = getImportMaterialsDocument();
  let item = findImportMaterialSlotItem(document, definition);
  if (!item) {
    item = createImportMaterialSlotItem(definition);
    document.items.push(item);
  } else {
    item.label = item.label || definition.label;
    item.presetId = item.presetId || definition.presetId;
    item.slot = item.slot || definition.slot || "";
    item.bindTarget = item.bindTarget || definition.bindTarget || "";
    item.required = definition.required !== false;
  }
  return item;
}

function inferImportMaterialSlotKey(sourcePath) {
  const normalized = normalizeImportMaterialPath(sourcePath).toLowerCase();
  const fileName = normalized.split("/").pop() || normalized;
  const extension = fileName.split(".").pop() || "";
  const modelSource = ["fbx", "glb", "gltf", "blend"].includes(extension);
  const textureSource = ["png", "jpg", "jpeg", "tga", "exr", "psd"].includes(extension);
  if (textureSource && /(face[_-]?detail|facedetail)/i.test(normalized)) return "faceDetail";
  if (textureSource && /(body|albedo|basecolor|base_color|diffuse|texture)/i.test(normalized)) return "bodyTexture";
  if (modelSource && /(body|model|character|avatar|mesh)/i.test(normalized)) return "body";
  return "";
}

function renderImportMaterialsPanel() {
  if (!el.importMaterialsPanel) return;
  const payload = state.importMaterials || normalizeImportMaterialsPayload(null);
  const document = getImportMaterialsDocument();
  const statusById = new Map((Array.isArray(payload.items) ? payload.items : []).map(item => [item.id, item]));
  const items = document.items.map((item, index) => {
    const status = statusById.get(item.id) || { status: "Draft" };
    const checked = globalThis.getCheckedImportMaterialStatus?.(status, item, document.sourceRoot, payload.checkedSourceRoot ?? document.sourceRoot) || { ...status, item };
    return normalizeImportMaterialStatus(checked, item, index);
  });
  const summary = summarizeImportMaterialStatuses(items, payload.integrityReceipt);
  const templates = Array.isArray(payload.templates) && payload.templates.length ? payload.templates : IMPORT_MATERIAL_TEMPLATES;
  const busy = globalThis.isImportMaterialsBusy?.() || false;
  const operation = busy ? state.importMaterialsOperation.kind : "";
  const canWrite = state.canWrite && state.apiAvailable && !busy;
  const hasRows = document.items.length > 0;
  const sourceRoot = document.sourceRoot || "";
  const sourceManifestRelative = payload.sourceManifestRelative || payload.documentRelative || "";
  const stateRelative = payload.stateRelative || "";
  const slotCards = buildImportMaterialSlotStatuses(document, statusById, items);
  const slotItemIds = new Set(slotCards.map(card => card.status.id).filter(Boolean));
  const extraItems = items.filter(status => !slotItemIds.has(status.id));
  const selectedCount = getSelectedImportMaterialIds().length;
  const manifestState = getImportMaterialsManifestState(payload, document);
  const canSyncRows = canWrite && hasRows;
  const draftResultsPending = items.some(item => item.checkOutdated) || payload.integrityDraftPending;
  const blockers = payload.integrityReceipt?.diagnostics?.filter(item => item.severity === "Error") || [];
  const conflicts = state.sourceSyncConflictPaths || [];

  el.importMaterialsPanel.innerHTML = `
    <div class="import-materials-statusbar">
      <div class="import-materials-editor-title">
        <strong>角色模型与贴图来源</strong>
        <span>选择角色文件或引用公共资源，再同步到当前角色资源包。</span>
      </div>
      <div class="import-materials-summary">
        ${renderImportMaterialSummaryBadge("总数", summary.total)}
        ${renderImportMaterialSummaryBadge("已同步到包", summary.clean, "ok")}
        ${renderImportMaterialSummaryBadge("待处理", summary.dirty, summary.dirty ? "warn" : "")}
        ${renderImportMaterialSummaryBadge("缺失", summary.missing, summary.missing ? "error" : "")}
        ${renderImportMaterialSummaryBadge("Unity 待处理", summary.unityPending, summary.unityPending ? "pending" : "")}
        ${renderImportMaterialSummaryBadge("完整性阻塞", summary.integrityBlocking, summary.integrityBlocking ? "error" : "ok")}
        ${!payload.sourceManifestExists ? `<span class="badge warn">缺少清单</span>` : ""}
        ${payload.legacyDocumentExists && !payload.sourceManifestExists ? `<span class="badge pending">legacy</span>` : ""}
        ${state.importMaterialsDirty ? `<span class="badge warn">未保存</span>` : ""}
      </div>
      <div class="import-materials-actions">
        <button type="button" data-import-material-action="save" ${canWrite ? "" : "disabled"}>${operation === "save" ? "保存中…" : state.importMaterialsDirty ? "保存来源配置 *" : "保存来源配置"}</button>
        <button type="button" data-import-material-action="recheck"${busy || !state.apiAvailable ? " disabled" : ""} title="更新已保存来源的文件检查结果，保留未保存选择">${operation === "check" ? "检查中…" : "检查来源"}</button>
        <button type="button" data-import-material-action="syncChanged" ${canSyncRows ? "" : "disabled"}>${operation === "sync" ? "同步中…" : state.importMaterialsDirty || !payload.sourceManifestExists ? "保存并同步到角色资源包" : "同步到角色资源包"}</button>
      </div>
      <p class="source-operation-help">保存记录本页来源选择；检查保留草稿。同步会先保存本页来源配置，其他角色编辑继续保留。同步到资源包后，Unity 导入仍是后续独立步骤。</p>
    </div>
    ${conflicts.length ? `<section class="source-operation-conflicts" role="status"><strong>同步后有待核对的改动</strong><p>草稿值和撤销记录已保留。存在冲突或未能确认版本的文档会继续检查原版本，请先核对详情。</p><details><summary>待核对位置（${conflicts.length}）</summary>${conflicts.map(path => `<code>${escapeHtml(path)}</code>`).join("")}</details></section>` : ""}
    ${summary.missing || blockers.length ? `<div class="source-operation-problems" role="status"><strong>来源还有需要处理的问题</strong><span>${escapeHtml(blockers[0]?.message || "存在缺失文件，请查看对应来源卡片。")}</span></div>` : ""}
    ${draftResultsPending ? '<p class="source-operation-help">来源选择已有草稿改动。下方待检查项尚无当前结果；详细回执对应已保存来源。</p>' : ""}
    ${renderImportMaterialsManifestState(manifestState, payload, sourceManifestRelative, sourceRoot)}
    ${renderImportMaterialSlotCards(slotCards, canWrite)}
    ${payload.sourceManifestExists && document.items.length ? renderImportMaterialsExtraResources(extraItems, canWrite) : ""}
    <details class="source-audit-details"${summary.integrityBlocking || summary.integrityWarnings ? " open" : ""}><summary>来源检查详情${summary.integrityBlocking ? ` · ${summary.integrityBlocking} 项阻塞` : ""}</summary>${renderImportMaterialsIntegrityReceipt(payload.integrityReceipt)}</details>
    <details class="source-audit-details"><summary>公共资源库（按需管理）</summary>${renderCommonResourcesStatusBar()}</details>
    ${renderDefaultWeaponReferenceCards(canWrite)}
    <details class="source-audit-details"><summary>制作来源与文件记录</summary>${renderCharacterAuthoringSourcesSummary()}</details>
    ${renderImportMaterialsBatchTools()}
    ${renderImportMaterialsManifestDetails(templates, sourceManifestRelative, sourceRoot, stateRelative, canWrite)}
    ${renderImportMaterialsAdvancedList(items, selectedCount, canWrite)}
    ${renderImportMaterialSourcePicker()}
  `;
}

function renderCharacterAuthoringSourcesSummary() {
  const source = state.authoringSources;
  if (!source) return "";
  const manifest = source.manifest || {};
  const items = Array.isArray(manifest.items) ? manifest.items : [];
  const issues = Array.isArray(source.validation?.diagnostics) ? source.validation.diagnostics : [];
  const blockers = issues.filter(item => item?.severity === "Error").length;
  return `<section class="import-materials-manifest-state">
    <header><strong>可选制作资产</strong><span class="badge ${blockers ? "error" : "ok"}">${escapeHtml(source.status || "NotDeclared")}</span></header>
    <p>Blender 源、模型图、里程碑、审计和 provenance 均为可选；一旦采用，将由独立制作清单与 SHA-256 管理。</p>
    <div class="import-materials-source-summary">
      <div><span>角色编号</span><code>${escapeHtml(source.characterId || "")}</code></div>
      <div><span>制作清单</span><code>${escapeHtml(source.manifestRelative || "未声明")}</code></div>
      <div><span>已管理文件</span><code>${items.length}</code></div>
      <div><span>制作清单阻塞</span><code>${blockers}</code></div>
    </div>
  </section>`;
}

function renderImportMaterialsIntegrityReceipt(receipt) {
  const normalized = normalizeImportMaterialsIntegrityReceipt(receipt);
  const statusLabels = {
    Ready: "原材料关系完整",
    ReadyWithWarnings: "可继续，但有警告",
    Blocked: "原材料导入不完整",
    NotApplicable: "未启用独立材质关系",
    Unavailable: "缺少完整性回执"
  };
  const tone = normalized.status === "Blocked" || normalized.status === "Unavailable"
    ? "error"
    : normalized.status === "ReadyWithWarnings" ? "warn" : normalized.status === "Ready" ? "ok" : "muted";
  const diagnostics = normalized.diagnostics;
  const materialEvidence = normalized.materials.length
    ? `<div class="import-materials-integrity-diagnostics">${normalized.materials.map(material => {
      const alphaDetail = material.surfaceMode === "AlphaClip" ? ` · cutoff ${material.alphaCutoff}` : "";
      const identity = material.descriptorRelativePath || material.materialResourceKey || "";
      return `<article class="import-materials-integrity-diagnostic info">
        <div><strong>${escapeHtml(material.sourceMaterialName || material.materialResourceKey || "Material")}</strong><code>${escapeHtml(`${material.surfaceMode}${alphaDetail}`)}</code></div>
        ${identity ? `<span>${escapeHtml(identity)}</span>` : ""}
      </article>`;
    }).join("")}</div>`
    : "";
  return `<section class="import-materials-integrity ${escapeHtml(tone)}">
    <div class="import-materials-integrity-head">
      <div>
        <strong>原材料导入回执</strong>
        <span>${escapeHtml(statusLabels[normalized.status] || normalized.status)}</span>
      </div>
      <div class="import-materials-summary">
        ${renderImportMaterialSummaryBadge("贴图", normalized.textureCount)}
        ${renderImportMaterialSummaryBadge("材质", `${normalized.boundMaterialCount}/${normalized.expectedMaterialCount}`, normalized.boundMaterialCount === normalized.expectedMaterialCount ? "ok" : "error")}
        ${renderImportMaterialSummaryBadge("源材质绑定", normalized.rendererBindingCount, normalized.rendererBindingCount === normalized.expectedMaterialCount ? "ok" : "error")}
        ${renderImportMaterialSummaryBadge("阻塞", normalized.blockingIssueCount, normalized.blockingIssueCount ? "error" : "ok")}
      </div>
    </div>
    ${materialEvidence}
    ${diagnostics.length ? `<div class="import-materials-integrity-diagnostics">${diagnostics.map(diagnostic => {
      const diagnosticTone = diagnostic.severity === "Error" ? "error" : diagnostic.severity === "Warning" ? "warn" : "info";
      const identity = diagnostic.itemId || diagnostic.resourceKey || diagnostic.path || "";
      return `<article class="import-materials-integrity-diagnostic ${diagnosticTone}">
        <div><strong>${escapeHtml(diagnostic.message || diagnostic.code)}</strong><code>${escapeHtml(diagnostic.code)}</code></div>
        ${identity ? `<span>${escapeHtml(identity)}</span>` : ""}
        ${diagnostic.suggestedAction ? `<p>${escapeHtml(diagnostic.suggestedAction)}</p>` : ""}
      </article>`;
    }).join("")}</div>` : normalized.status === "NotApplicable"
      ? `<div class="readiness-ok">清单未声明独立材质贴图；继续使用明确的 FBX / Unity 材质路径。</div>`
      : `<div class="readiness-ok">文件、材质描述、依赖图和源材质名绑定均已检查。</div>`}
    ${normalized.status === "Ready" || normalized.status === "ReadyWithWarnings"
      ? `<p class="import-materials-integrity-note">这里证明原材料关系完整；具体 Renderer 节点/槽位覆盖由 Unity 导入回执继续验收。</p>`
      : ""}
  </section>`;
}

function renderCommonResourcesStatusBar() {
  const payload = state.commonResources || null;
  const unavailable = Boolean(payload?.unavailable || payload?.success === false && !Array.isArray(payload?.items));
  const summary = payload?.summary || {};
  const total = Number(summary.total || 0);
  const synced = Number(summary.synced || summary.ready || summary.clean || 0);
  const changed = Number(summary.changed || summary.dirty || 0);
  const missing = Number(summary.missing || summary.blocked || 0);
  const unityPending = Number(summary.unityPending || summary.needsUnityImport || 0);
  const tone = unavailable || missing ? "error" : changed || unityPending ? "warn" : "ok";
  const title = unavailable
    ? (payload?.message || "公共资源 API 不可用。")
    : total ? `已登记 ${total} 个可选公共资源。` : "ArtSource/Common 下没有公共源清单。";
  return `<section class="common-resources-status ${escapeHtml(tone)}">
    <div>
      <strong>公共复用资源</strong>
      <span title="${escapeHtml(title)}">${escapeHtml(title)}</span>
    </div>
    <div class="import-materials-summary">
      ${renderImportMaterialSummaryBadge("总数", total)}
      ${renderImportMaterialSummaryBadge("已同步", synced, synced ? "ok" : "")}
      ${renderImportMaterialSummaryBadge("有改动", changed, changed ? "warn" : "")}
      ${renderImportMaterialSummaryBadge("缺失", missing, missing ? "error" : "")}
      ${renderImportMaterialSummaryBadge("Unity 待处理", unityPending, unityPending ? "pending" : "")}
    </div>
    <button type="button" data-import-material-action="openResourceManagerCommonResources" ${state.apiAvailable ? "" : "disabled"}>在 Resource Manager 中管理/修复</button>
  </section>`;
}

function renderImportMaterialSummaryBadge(label, value, tone = "") {
  return `<span class="badge ${escapeHtml(tone)}">${escapeHtml(label)} ${escapeHtml(String(value || 0))}</span>`;
}

function renderDefaultWeaponReferenceCards(canWrite) {
  const attachments = getDefaultWeaponReferenceCards();
  return `<details class="default-weapon-reference-section" aria-label="组合武器资源归属">
    <summary class="import-materials-section-heading">
      <div>
        <strong>组合武器资源归属</strong>
        <span>模型资源属于全局单一武器配置；当前角色只保存组合选择和本地挂接。</span>
      </div>
    </summary>
    <div class="default-weapon-reference-grid">
      ${attachments.map(card => renderDefaultWeaponReferenceCard(card, canWrite)).join("")}
    </div>
  </details>`;
}

function getDefaultWeaponReferenceCards() {
  const configuration = getActiveWeaponConfiguration();
  return [
    { slot: "mainHand", label: "主手", emptyHint: "weapon.unarmed（空手）" },
    { slot: "offHand", label: "副手", emptyHint: "weapon.unarmed（空手）" }
  ].map(definition => {
    const selected = configuration?.slots?.find(item => item?.slotId === definition.slot) || null;
    const singleWeapon = getSingleWeaponConfiguration(selected?.singleWeaponConfigurationId);
    const attachment = findDefaultWeaponAttachment(definition.slot);
    const key = getWeaponAttachmentResourceKey(attachment);
    const resource = key ? findResourceByKey(key) : null;
    return { ...definition, attachment, key, resource, selected, singleWeapon };
  });
}

function renderDefaultWeaponReferenceCard(card, canWrite) {
  const attachment = card.attachment || null;
  const singleWeaponId = card.selected?.singleWeaponConfigurationId || "weapon.unarmed";
  const configured = singleWeaponId === "weapon.unarmed" || Boolean(card.key);
  const tone = configured ? "ok" : "muted";
  const attachText = singleWeaponId === "weapon.unarmed"
    ? "空手不允许 attachment"
    : (attachment ? `${attachment.attachSocketId || "socket?"} / ${attachment.attachmentId || card.slot}` : "缺少角色本地 attachment");
  const resourceText = card.resource ? getResourceDisplayName(card.resource) : (card.key || card.emptyHint);
  return `<article class="default-weapon-reference-card ${escapeHtml(tone)}" data-default-weapon-slot="${escapeHtml(card.slot)}">
    <header>
      <div>
        <strong>${escapeHtml(card.label)}</strong>
        <span>${escapeHtml(card.slot)}</span>
      </div>
      <span class="badge ${escapeHtml(tone)}">${configured ? "已配置" : "缺少全局资源"}</span>
    </header>
    <div class="default-weapon-reference-body">
      <code title="${escapeHtml(resourceText)}">${escapeHtml(resourceText)}</code>
      <span title="${escapeHtml(attachText)}">${escapeHtml(attachText)}</span>
    </div>
    <div class="import-material-slot-actions">
      <button type="button" data-import-material-action="openSingleWeaponConfiguration" data-single-weapon-configuration-id="${escapeHtml(singleWeaponId)}" ${singleWeaponId !== "weapon.unarmed" ? "" : "disabled"}>打开单一武器配置</button>
    </div>
  </article>`;
}

function renderImportMaterialsBatchTools() {
  return `<details class="import-materials-batch">
    <summary>添加/检查源路径</summary>
    <div class="import-materials-paste">
      <textarea data-import-material-paths rows="3" placeholder="character_00.fbx&#10;body_albedo.png"></textarea>
      <button type="button" data-import-material-action="appendPasted">校验并加入清单</button>
    </div>
    ${renderImportMaterialPasteDiagnostics()}
  </details>`;
}

function renderImportMaterialsManifestDetails(templates, sourceManifestRelative, sourceRoot, stateRelative, canWrite) {
  return `<details class="import-materials-details">
    <summary>character_sources.json 详情</summary>
    <div class="import-materials-details-grid">
      <label class="field-label">源模板
        <select data-import-material-template>
          ${templates.map(template => `<option value="${escapeHtml(template.id)}">${escapeHtml(template.label)}</option>`).join("")}
        </select>
      </label>
      <button type="button" data-import-material-action="applyTemplate" ${canWrite ? "" : "disabled"}>应用到清单</button>
      <div>
        <span>源清单文件</span>
        <code title="${escapeHtml(sourceManifestRelative || "尚未保存")}">${escapeHtml(sourceManifestRelative || "尚未保存")}</code>
      </div>
      <div>
        <span>源目录</span>
        <code title="${escapeHtml(sourceRoot || "(角色源目录)")}">${escapeHtml(sourceRoot || "(角色源目录)")}</code>
      </div>
      <div>
        <span>同步状态文件</span>
        <code title="${escapeHtml(stateRelative || "未报告")}">${escapeHtml(stateRelative || "未报告")}</code>
      </div>
    </div>
  </details>`;
}

function getImportMaterialsManifestState(payload, document) {
  if (!payload.sourceManifestExists) return payload.legacyDocumentExists ? "legacy" : "missing";
  if (!Array.isArray(document.items) || document.items.length === 0) return "empty";
  return "populated";
}

function renderImportMaterialsManifestState(manifestState, payload, sourceManifestRelative, sourceRoot) {
  if (manifestState === "populated") return "";
  const titleByState = {
    missing: "缺少 character_sources.json 清单",
    legacy: "检测到旧版 import_materials.json",
    empty: "character_sources.json 为空",
    staged: "源条目已暂存但尚未保存"
  };
  const bodyByState = {
    missing: "这个包还没有 ArtSource 源清单。先填写下方卡片、应用模板或添加检查过的源路径，再保存生成 character_sources.json。",
    legacy: "这个包仍在使用包内旧文档。保存清单会迁移到 ArtSource；下方卡片仍作为编辑槽位，只写入已配置来源。",
    empty: "清单存在但没有条目。添加源条目或应用模板后保存清单。",
    staged: "清单文件存在，但这些条目尚未作为已保存源列表提交。请先保存清单。"
  };
  return `<section class="import-materials-manifest-state ${escapeHtml(manifestState)}">
    <div>
      <strong>${escapeHtml(titleByState[manifestState] || "源清单状态")}</strong>
      <span>${escapeHtml(bodyByState[manifestState] || "")}</span>
    </div>
    <div>
      <span>目标清单</span>
      <code title="${escapeHtml(sourceManifestRelative || "未报告")}">${escapeHtml(sourceManifestRelative || "未报告")}</code>
      <span>源目录</span>
      <code title="${escapeHtml(sourceRoot || "(角色源目录)")}">${escapeHtml(sourceRoot || "(角色源目录)")}</code>
      ${payload.legacyDocumentExists ? `<span>旧版文档</span><code title="${escapeHtml(payload.legacyDocumentRelative || "")}">${escapeHtml(payload.legacyDocumentRelative || "config/import_materials.json")}</code>` : ""}
    </div>
  </section>`;
}

function buildImportMaterialSlotStatuses(document, statusById, statuses) {
  const items = Array.isArray(document?.items) ? document.items : [];
  const claimedItems = new Set();
  return IMPORT_MATERIAL_SLOT_DEFINITIONS.map(slotDefinition => {
    const item = items.find(candidate => !claimedItems.has(candidate) && getImportMaterialSlotDefinitionForItem(candidate)?.key === slotDefinition.key);
    if (!item) {
      const virtualItem = createImportMaterialSlotItem(slotDefinition);
      virtualItem.sourcePath = "";
      const status = normalizeImportMaterialStatus({
        item: virtualItem,
        id: virtualItem.id,
        label: virtualItem.label,
        status: slotDefinition.required === false ? "OptionalEmpty" : "RequiredMissing",
        message: slotDefinition.required === false
          ? "可选来源未配置，不会保存或同步。"
          : "必填来源未配置。"
      }, virtualItem, -1);
      return { definition: slotDefinition, status, existing: false };
    }

    claimedItems.add(item);
    const definition = {
      ...slotDefinition,
      required: item.required !== false
    };
    const sourceStatus = statuses.find(status => status.id === item.id) || statusById.get(item.id) || { item, status: "Unknown" };
    const status = normalizeImportMaterialStatus({ ...sourceStatus, item }, item, items.indexOf(item));
    if (!hasImportMaterialSourceConfigured(status.item)) {
      status.status = item.required !== false ? "RequiredMissing" : "OptionalEmpty";
      status.message = item.required !== false
        ? "必填来源未配置。"
        : "可选来源未配置，不会保存或同步。";
    } else if (status.status === "Unknown" || status.status === "Draft") {
      status.status = "NotImported";
    }
    return { definition, status, existing: true };
  });
}

function getImportMaterialSlotDefinitionForItem(item) {
  if (!item) return null;
  return IMPORT_MATERIAL_SLOT_DEFINITIONS.find(definition => item.id === definition.id)
    || IMPORT_MATERIAL_SLOT_DEFINITIONS.find(definition => definition.bindTarget && item.bindTarget === definition.bindTarget)
    || IMPORT_MATERIAL_SLOT_DEFINITIONS.find(definition => definition.slot && item.slot === definition.slot && item.presetId === definition.presetId)
    || IMPORT_MATERIAL_SLOT_DEFINITIONS.find(definition => item.presetId === definition.presetId && item.sourcePath && inferImportMaterialSlotKey(item.sourcePath) === definition.key)
    || null;
}

function getImportMaterialSlotDefinitionOrder(definition) {
  const index = IMPORT_MATERIAL_SLOT_DEFINITIONS.findIndex(item => item.key === definition?.key);
  return index >= 0 ? index : IMPORT_MATERIAL_SLOT_DEFINITIONS.length;
}

function renderImportMaterialSlotCards(cards, canWrite) {
  return `<section class="import-material-slot-grid" aria-label="源资源槽位">
    ${cards.map(card => renderImportMaterialSlotCard(card, canWrite)).join("")}
  </section>`;
}

function renderImportMaterialSlotCard(card, canWrite) {
  const { definition, status, existing } = card;
  const item = status.item || {};
  const displayStatus = getImportMaterialSlotDisplayStatus(status, definition);
  const tone = getImportMaterialStatusTone(displayStatus.status);
  const isResourceRef = isImportMaterialResourceRefItem(item);
  const resourceRefKey = isResourceRef ? getResourceSelectionKey(item.source?.selection) : "";
  const sourceDisplay = isResourceRef ? (resourceRefKey || "绑定已有公共资源") : (item.sourcePath || "选择角色源文件");
  const resolvedPath = isResourceRef
    ? (resourceRefKey || "未选择公共资源")
    : (status.resolvedSourcePath || item.sourcePath || "未选择源文件");
  const sourceKindText = isResourceRef ? "公共资源引用" : `${formatSourceScopeLabel(status.sourceScope || item.source?.scope || "package")}文件`;
  const thumbnail = isResourceRef ? "" : renderImportMaterialSourceThumbnail(item.sourcePath, status.sourceScope || item.source?.scope, status.sourceHash, definition.label);
  const syncDisabled = canWrite && hasImportMaterialSourceConfigured(item) ? "" : " disabled";
  const clearDisabled = canWrite && (existing || hasImportMaterialSourceConfigured(item)) ? "" : " disabled";
  const requiredText = definition.required !== false ? "必填" : "可选";
  const publicButton = `<button type="button" data-import-material-action="choosePublicSourceResource" data-import-material-slot="${escapeHtml(definition.key)}" ${canWrite ? "" : "disabled"}>${isResourceRef ? "更换公共引用" : "引用公共资源"}</button>`;
  return `<article class="import-material-slot-card ${escapeHtml(tone)}" data-import-material-slot="${escapeHtml(definition.key)}">
    <header>
      <div>
        <strong>${escapeHtml(definition.label)}</strong>
        ${item.label && item.label !== definition.label ? `<span>${escapeHtml(item.label)}</span>` : ""}
      </div>
      <span class="badge ${escapeHtml(tone)}" title="${escapeHtml(displayStatus.message || "")}">${escapeHtml(displayStatus.label)}</span>
    </header>
    <div class="import-material-slot-source">
      ${thumbnail}
      <code title="${escapeHtml(resolvedPath)}">${escapeHtml(sourceDisplay)}</code>
      <button type="button" data-import-material-action="openSourcePicker" data-import-material-slot="${escapeHtml(definition.key)}" ${canWrite ? "" : "disabled"}>${!isResourceRef && item.sourcePath ? "更换角色文件" : "选择角色文件"}</button>
      ${publicButton}
    </div>
    <div class="import-material-slot-meta">
      <span class="sync-badge muted">${escapeHtml(requiredText)}</span>
      <span class="sync-badge muted">${escapeHtml(sourceKindText)}</span>
    </div>
    ${displayStatus.message ? `<p class="source-slot-status-message">${escapeHtml(displayStatus.message)}</p>` : ""}
    <details class="source-slot-path"><summary>路径与引用详情</summary><code>${escapeHtml(resolvedPath)}</code></details>
    ${renderBodyFbxTextureDiagnostic(definition, status)}
    <div class="import-material-slot-actions">
      <button type="button" data-import-material-action="syncSlot" data-import-material-slot="${escapeHtml(definition.key)}"${syncDisabled}>同步此来源</button>
      <button type="button" data-import-material-action="clearSlot" data-import-material-slot="${escapeHtml(definition.key)}"${clearDisabled}>清空来源</button>
    </div>
  </article>`;
}

function getImportMaterialSlotDisplayStatus(status, definition) {
  const raw = status?.status || "";
  const configured = hasImportMaterialSourceConfigured(status?.item);
  if (configured && (status?.checkOutdated || raw === "NeedsCheck")) return { status: "NeedsCheck", label: "已选择，待检查", message: status?.message || "来源选择尚未检查。" };
  if (configured && (raw === "Missing" || raw === "OptionalMissing" || raw === "RequiredMissing")) return {
    status: definition?.required === false ? "OptionalMissing" : "Missing", label: definition?.required === false ? "可选文件缺失" : "文件缺失",
    message: status?.message || "已选择来源，但文件不可用。"
  };
  if (!configured && (raw === "OptionalEmpty" || raw === "Optional" || raw === "OptionalMissing")) {
    return {
      status: "OptionalEmpty",
      label: "可选空缺",
      message: status?.message || "可选来源未配置，不会保存或同步。"
    };
  }
  if (raw === "RequiredMissing" || raw === "Missing" || raw === "Blocked") {
    return {
      status: raw === "Blocked" ? "Blocked" : "RequiredMissing",
      label: raw === "Blocked" ? "阻塞" : "必填缺失",
      message: status?.message || "必填来源未配置。"
    };
  }
  if (raw === "Clean") return { status: "Clean", label: "已同步到资源包", message: status?.message || "来源已同步。" };
  if (raw === "Changed") return { status: "Changed", label: "来源变更，待同步", message: status?.message || "来源已变化。" };
  if (raw === "NotImported" || raw === "UnityPending") {
    return { status: raw, label: raw === "UnityPending" ? "待 Unity 导入" : "待同步", message: status?.message || "来源尚需同步。" };
  }
  if (definition?.required === false && !hasImportMaterialSourceConfigured(status?.item)) {
    return { status: "OptionalEmpty", label: "可选空缺", message: "可选来源未配置，不会保存或同步。" };
  }
  return { status: "NeedsCheck", label: "需要检查", message: status?.message || "来源状态需要重新检查。" };
}

function formatSourceScopeLabel(scope) {
  const normalized = String(scope || "").toLowerCase();
  if (normalized === "package") return "角色目录";
  if (normalized === "public") return "公共";
  if (normalized === "absolute") return "绝对路径";
  return scope || "包内";
}

function renderBodyFbxTextureDiagnostic(definition, status) {
  if (definition?.key !== "body") return "";
  const diagnostic = status?.fbxTextureDiagnostic || null;
  const sourcePath = status?.item?.sourcePath || "";
  if (isImportMaterialResourceRefItem(status?.item)) {
    return `<div class="import-material-slot-hint muted"><strong>贴图来源</strong><span>这个身体模型来自公共资源。请在公共资源清单或 Unity 导入结果中检查贴图归属。</span></div>`;
  }
  const isFbx = /\.fbx$/i.test(sourcePath);
  if (!sourcePath) {
    return `<div class="import-material-slot-hint muted"><strong>贴图来源</strong><span>选择 FBX 身体模型后可检查内嵌或外部贴图引用。</span></div>`;
  }
  if (!isFbx) {
    return `<div class="import-material-slot-hint muted"><strong>贴图来源</strong><span>当前只支持检查 FBX 身体模型的贴图来源。</span></div>`;
  }
  const refs = Array.isArray(diagnostic?.externalTexturePaths) ? diagnostic.externalTexturePaths : [];
  const refText = refs.length ? ` 引用：${refs.slice(0, 3).join(", ")}${refs.length > 3 ? "..." : ""}` : "";
  const statusKey = diagnostic?.status || "unknown";
  if (statusKey === "embeddedTextureDetected" || diagnostic?.embeddedTextureDetected) {
    return `<div class="import-material-slot-hint ok"><strong>贴图来源</strong><span>检测到内嵌贴图数据。身体贴图可以留空；Unity 导入应使用 FBX 材质。${escapeHtml(refText)}</span></div>`;
  }
  if (statusKey === "externalTextureRefs" || diagnostic?.externalTextureRefs) {
    return `<div class="import-material-slot-hint warn"><strong>贴图来源</strong><span>检测到外部贴图引用。如需同步和缺失文件检查，请添加身体贴图或额外源。${escapeHtml(refText)}</span></div>`;
  }
  if (statusKey === "noTextureEvidence") {
    return `<div class="import-material-slot-hint muted"><strong>贴图来源</strong><span>未检测到内嵌贴图或外部贴图引用。如果模型需要跟踪贴图，请添加身体贴图；纯材质或顶点色可忽略。</span></div>`;
  }
  return `<div class="import-material-slot-hint pending"><strong>贴图来源</strong><span>${escapeHtml(diagnostic?.message || "检查文件后才能获取 FBX 贴图来源诊断。")}</span></div>`;
}

function renderImportMaterialsExtraResources(items, canWrite) {
  const problemItems = items.filter(status => status.status && status.status !== "Clean" && status.status !== "Optional");
  const cleanCount = Math.max(0, items.length - problemItems.length);
  const rows = problemItems.length
    ? problemItems.map(status => renderImportMaterialExtraRow(status, canWrite)).join("")
    : `<div class="import-materials-extra-note">${items.length ? `全部 ${escapeHtml(String(items.length))} 个额外源资源已同步。完整编辑请使用高级清单字段。` : "没有额外源资源。"}</div>`;
  return `<details class="import-materials-extra">
    <summary>
      <strong>额外源资源</strong>
      <span>${escapeHtml(String(items.length))} 总数 / ${escapeHtml(String(cleanCount))} 已同步</span>
      <button type="button" data-import-material-action="openSourcePicker" data-import-material-target="extra" ${canWrite ? "" : "disabled"}>添加来源</button>
    </summary>
    <div class="import-materials-extra-list">
      ${rows}
    </div>
  </details>`;
}

function renderImportMaterialExtraRow(status, canWrite) {
  const item = status.item || {};
  const tone = getImportMaterialStatusTone(status.status);
  const thumbnail = renderImportMaterialSourceThumbnail(item.sourcePath, status.sourceScope || item.source?.scope, status.sourceHash, item.label || item.sourcePath);
  const presetOptions = IMPORT_MATERIAL_PRESETS.map(preset => {
    const selectedOption = preset.id === item.presetId ? " selected" : "";
    return `<option value="${escapeHtml(preset.id)}"${selectedOption}>${escapeHtml(preset.label)}</option>`;
  }).join("");
  const syncDisabled = canWrite && item.sourcePath ? "" : " disabled";
  const sourceText = item.sourcePath || "未选择来源";
  return `<div class="import-material-extra-row" data-import-material-id="${escapeHtml(status.id)}">
    <span class="badge ${escapeHtml(tone)}" title="${escapeHtml(status.message || "")}">${escapeHtml(formatImportMaterialStatusLabel(status.status))}</span>
    ${thumbnail}
    <code title="${escapeHtml(status.resolvedSourcePath || item.sourcePath || "")}">${escapeHtml(sourceText)}</code>
    <select data-import-material-field="presetId">${presetOptions}</select>
    <button type="button" data-import-material-action="openSourcePicker" data-import-material-id="${escapeHtml(status.id)}" ${canWrite ? "" : "disabled"}>选择</button>
    <button type="button" data-import-material-action="syncOne" data-import-material-id="${escapeHtml(status.id)}"${syncDisabled}>同步</button>
    <button type="button" data-import-material-action="remove" data-import-material-id="${escapeHtml(status.id)}">移除</button>
  </div>`;
}

function renderImportMaterialPasteDiagnostics() {
  const diagnostics = Array.isArray(state.importMaterialUnrecognizedPaths) ? state.importMaterialUnrecognizedPaths : [];
  if (!diagnostics.length) return "";
  return `<div class="import-materials-paste-diagnostics">
    <strong>未识别路径</strong>
    ${diagnostics.map(item => `<span><code>${escapeHtml(item.path || "")}</code> ${escapeHtml(item.message || "")}</span>`).join("")}
  </div>`;
}

function renderImportMaterialSourcePicker() {
  const picker = state.importMaterialSourcePicker;
  if (!picker) return "";
  const payload = state.importMaterialSourceCandidates || normalizeImportMaterialSourceCandidatesPayload(null, getImportMaterialsDocument().sourceRoot || "", "");
  const candidates = getVisibleImportMaterialSourceCandidates(payload.items || [], picker);
  const typeOptions = getImportMaterialSourceCandidateTypeOptions(payload.items || []);
  const rootOptions = getImportMaterialSourceRootOptions(payload);
  const slotDefinition = getImportMaterialSlotDefinition(picker.slotKey);
  const title = slotDefinition ? `选择${slotDefinition.label}` : "选择源文件";
  return `<div class="import-material-source-picker" role="dialog" aria-modal="true">
    <div class="import-material-source-picker-card">
      <header>
        <div>
          <strong>${escapeHtml(title)}</strong>
          <span>源根目录：${escapeHtml(payload.sourceRoot || "(项目/包)")}</span>
        </div>
        <button type="button" data-import-material-action="closeSourcePicker" aria-label="关闭">×</button>
      </header>
      <div class="import-material-source-picker-filters">
        <input data-import-material-source-search value="${escapeHtml(state.importMaterialSourcePickerSearch || "")}" placeholder="搜索候选文件">
        <select data-import-material-source-root title="候选根目录">
          ${rootOptions.map(root => `<option value="${escapeHtml(root)}"${root === payload.sourceRoot ? " selected" : ""}>${escapeHtml(root || "(包内)")}</option>`).join("")}
        </select>
        <select data-import-material-source-type>
          <option value="">全部类型</option>
          ${typeOptions.map(type => `<option value="${escapeHtml(type)}"${type === state.importMaterialSourcePickerType ? " selected" : ""}>${escapeHtml(type)}</option>`).join("")}
        </select>
        <button type="button" data-import-material-action="reloadSourceCandidates">刷新</button>
      </div>
      ${payload.diagnostics?.length ? `<div class="import-material-source-diagnostics">${payload.diagnostics.map(item => `<span>${escapeHtml(item.message || item.code || "")}</span>`).join("")}</div>` : ""}
      ${payload.truncated ? `<div class="import-material-source-diagnostics"><span>候选列表已截断，请缩小源根目录或搜索范围。</span></div>` : ""}
      <div class="import-material-source-candidate-list">
        ${candidates.length ? candidates.map(candidate => renderImportMaterialSourceCandidate(candidate)).join("") : `<div class="import-materials-empty">没有匹配的项目/包内源文件。请调整源根目录，或把文件放到允许的目录下。</div>`}
      </div>
    </div>
  </div>`;
}

function renderImportMaterialSourceCandidate(candidate) {
  const disabled = candidate.selectable && state.canWrite ? "" : " disabled";
  const thumbnail = renderImportMaterialSourceThumbnail(candidate.sourcePath, candidate.sourceScope, "", candidate.fileName || candidate.sourcePath);
  const diagnostics = candidate.diagnostics?.length
    ? `<div class="import-material-source-candidate-diagnostics">${candidate.diagnostics.map(item => `<span>${escapeHtml(item.message || item.code || "")}</span>`).join("")}</div>`
    : "";
  return `<article class="import-material-source-candidate ${candidate.selectable ? "" : "blocked"}" data-import-material-candidate="${escapeHtml(candidate.id)}">
    ${thumbnail}
    <div class="import-material-source-candidate-main">
      <strong title="${escapeHtml(candidate.displayPath)}">${escapeHtml(candidate.fileName || candidate.sourcePath)}</strong>
      <code title="${escapeHtml(candidate.displayPath)}">${escapeHtml(candidate.displayPath || candidate.sourcePath)}</code>
      <span>${escapeHtml(candidate.reason || "")}</span>
      ${diagnostics}
    </div>
    <div class="import-material-source-candidate-tags">
      <span class="sync-badge muted">${escapeHtml(candidate.sourceScope || "package")}</span>
      <span class="sync-badge muted">${escapeHtml(candidate.sourceKind || "source")}</span>
      <span class="sync-badge muted">${escapeHtml(candidate.presetId || "preset")}</span>
      ${candidate.slotKey ? `<span class="sync-badge muted">${escapeHtml(candidate.slotKey)}</span>` : ""}
    </div>
    <button type="button" data-import-material-action="selectSourceCandidate" data-import-material-candidate="${escapeHtml(candidate.id)}"${disabled}>选择</button>
  </article>`;
}

function renderImportMaterialSourceThumbnail(sourcePath, sourceScope, cacheKey = "", label = "") {
  const url = getImportMaterialSourceThumbnailUrl(sourcePath, sourceScope, cacheKey);
  if (!url) return "";
  return `<span class="import-material-source-thumbnail" title="${escapeHtml(label || sourcePath || "图片来源")}"><img src="${escapeHtml(url)}" alt=""></span>`;
}

function getImportMaterialSourceThumbnailUrl(sourcePath, sourceScope, cacheKey = "") {
  const normalized = normalizeImportMaterialPath(sourcePath);
  if (!isImportMaterialBrowserImagePath(normalized)) return "";
  const params = new URLSearchParams();
  params.set("package", state.packageRelative || "");
  params.set("sourcePath", normalized);
  params.set("sourceScope", sourceScope || inferImportMaterialSourceScope(normalized));
  if (cacheKey) params.set("v", cacheKey);
  return `/api/character/import-materials/source-image?${params.toString()}`;
}

function isImportMaterialBrowserImagePath(sourcePath) {
  return /\.(png|jpe?g|webp|gif|bmp)$/i.test(String(sourcePath || ""));
}

function getVisibleImportMaterialSourceCandidates(candidates, picker) {
  const search = String(state.importMaterialSourcePickerSearch || "").trim().toLowerCase();
  const type = String(state.importMaterialSourcePickerType || "");
  return candidates.filter(candidate => {
    if (type && candidate.sourceKind !== type) return false;
    if (picker?.slotKey && !isImportMaterialCandidateCompatibleWithSlot(candidate, picker.slotKey)) return false;
    if (search && !getImportMaterialSourceCandidateSearchText(candidate).includes(search)) return false;
    return true;
  }).sort((left, right) => compareImportMaterialSourceCandidates(left, right, picker?.slotKey || ""));
}

function getImportMaterialSourceCandidateTypeOptions(candidates) {
  return Array.from(new Set(candidates.map(candidate => candidate.sourceKind).filter(Boolean))).sort((a, b) => a.localeCompare(b));
}

function getImportMaterialSourceRootOptions(payload) {
  const roots = Array.isArray(payload?.roots) ? payload.roots.map(normalizeImportMaterialPath).filter(Boolean) : [];
  const current = normalizeImportMaterialPath(payload?.sourceRoot || "");
  if (current && !roots.some(root => root.toLowerCase() === current.toLowerCase())) roots.unshift(current);
  return roots.length ? roots : [current];
}

function isImportMaterialCandidateCompatibleWithSlot(candidate, slotKey) {
  if (!slotKey) return true;
  if (candidate.slotKey === slotKey) return true;
  if (candidate.slotKey && candidate.slotKey !== slotKey) return false;
  const kind = getImportMaterialCandidateKind(candidate);
  const text = getImportMaterialSourceCandidateSearchText(candidate);
  if (slotKey === "bodyTexture") return kind === "texture";
  if (slotKey === "body") {
    if (kind !== "model") return false;
    if (/(mainhand|main_hand|main-hand|offhand|off_hand|off-hand|weapon|sword|katana|shield|blade|axe|staff)/i.test(text)) return false;
    return candidate.presetId === "characterModel" || candidate.presetId === "modelPreview" || /(body|character|avatar|mesh|model)/i.test(text);
  }
  return true;
}

function compareImportMaterialSourceCandidates(left, right, slotKey = "") {
  return getImportMaterialCandidateScopeRank(left) - getImportMaterialCandidateScopeRank(right)
    || getImportMaterialCandidateSlotRank(left, slotKey) - getImportMaterialCandidateSlotRank(right, slotKey)
    || getImportMaterialCandidateExtensionRank(left, slotKey) - getImportMaterialCandidateExtensionRank(right, slotKey)
    || String(left.displayPath || left.sourcePath || "").localeCompare(String(right.displayPath || right.sourcePath || ""));
}

function getImportMaterialCandidateScopeRank(candidate) {
  const scope = String(candidate?.sourceScope || "").toLowerCase();
  if (scope === "package") return 0;
  if (scope === "public") return 1;
  if (scope === "resourceref") return 2;
  return 3;
}

function getImportMaterialCandidateSlotRank(candidate, slotKey) {
  if (!slotKey) return 0;
  if (candidate?.slotKey === slotKey) return 0;
  const text = getImportMaterialSourceCandidateSearchText(candidate);
  if (slotKey === "body" && candidate?.presetId === "characterModel") return 1;
  if (slotKey === "bodyTexture" && candidate?.presetId === "texture") return 1;
  return 2;
}

function getImportMaterialCandidateExtensionRank(candidate, slotKey) {
  const extension = String(candidate?.extension || "").toLowerCase();
  const preferred = {
    body: ["fbx", "glb", "gltf", "blend"],
    bodyTexture: ["png", "jpg", "jpeg", "tga", "exr", "psd"]
  }[slotKey] || [];
  const index = preferred.indexOf(extension);
  return index >= 0 ? index : 99;
}

function getImportMaterialCandidateKind(candidate) {
  const sourceKind = String(candidate?.sourceKind || "").toLowerCase();
  if (sourceKind === "model" || sourceKind === "texture") return sourceKind;
  const extension = String(candidate?.extension || "").toLowerCase();
  if (["fbx", "glb", "gltf", "blend"].includes(extension)) return "model";
  if (["png", "jpg", "jpeg", "tga", "exr", "psd"].includes(extension)) return "texture";
  return sourceKind;
}

function getImportMaterialSourceCandidateSearchText(candidate) {
  return [
    candidate.sourcePath,
    candidate.displayPath,
    candidate.fileName,
    candidate.sourceKind,
    candidate.sourceScope,
    candidate.presetId,
    candidate.slotKey,
    candidate.reason,
    ...(candidate.diagnostics || []).map(item => item.message || item.code || "")
  ].filter(Boolean).join(" ").toLowerCase();
}

function renderImportMaterialsAdvancedList(items, selectedCount, canWrite) {
  return `<details class="import-materials-advanced">
    <summary>高级清单字段（${escapeHtml(String(items.length))}）</summary>
    <div class="import-materials-advanced-toolbar">
      <button type="button" data-import-material-action="addRow">添加清单项</button>
      <button type="button" data-import-material-action="syncSelected" ${canWrite && selectedCount ? "" : "disabled"}>同步所选来源</button>
    </div>
    ${items.length ? renderImportMaterialsTable(items) : renderImportMaterialsEmpty([])}
  </details>`;
}

function renderImportMaterialsEmpty(templates) {
  const firstTemplate = templates[0];
  return `<div class="import-materials-empty">
    <strong>还没有源清单项。</strong>
    <span>选择${escapeHtml(firstTemplate?.label || "一个模板")}，或校验源路径后开始编辑 character_sources.json。</span>
  </div>`;
}

function renderImportMaterialsTable(items) {
  return `<div class="import-materials-table" role="table" aria-label="character_sources.json 清单字段">
    <div class="import-materials-row import-materials-head" role="row">
      <span></span><span>状态</span><span>标签</span><span>来源</span><span>预设</span><span>槽位</span><span>纹理语义</span><span>ResourceKey</span><span></span>
    </div>
    ${items.map(status => renderImportMaterialRow(status)).join("")}
  </div>`;
}

function renderImportMaterialRow(status) {
  const item = status.item || {};
  const selected = state.importMaterialsSelectedIds?.has(status.id);
  const tone = getImportMaterialStatusTone(status.status);
  const presetOptions = IMPORT_MATERIAL_PRESETS.map(preset => {
    const selectedOption = preset.id === item.presetId ? " selected" : "";
    return `<option value="${escapeHtml(preset.id)}"${selectedOption}>${escapeHtml(preset.label)}</option>`;
  }).join("");
  const surfaceControls = item.textureSemantic === "Color" ? `<label>Surface
      <select data-import-material-field="materialSurfaceMode">
        ${["Opaque", "AlphaClip"].map(value => `<option value="${value}"${value === (item.materialSurfaceMode || "Opaque") ? " selected" : ""}>${value}</option>`).join("")}
      </select>
    </label>
    ${item.materialSurfaceMode === "AlphaClip" ? `<label>Cutoff <input type="number" min="0" max="1" step="0.01" data-import-material-field="alphaCutoff" value="${escapeHtml(String(item.alphaCutoff ?? 0.5))}"></label>` : ""}` : "";
  return `<div class="import-materials-row" role="row" data-import-material-id="${escapeHtml(status.id)}">
    <label class="import-materials-check"><input type="checkbox" data-import-material-select="${escapeHtml(status.id)}" ${selected ? "checked" : ""}></label>
    <span class="badge ${escapeHtml(tone)}" title="${escapeHtml(status.message || "")}">${escapeHtml(formatImportMaterialStatusLabel(status.status))}</span>
    <input data-import-material-field="label" value="${escapeHtml(item.label || "")}">
    <input data-import-material-field="sourcePath" value="${escapeHtml(item.sourcePath || "")}" title="${escapeHtml(status.resolvedSourcePath || item.sourcePath || "")}">
    <select data-import-material-field="presetId">${presetOptions}</select>
    <input data-import-material-field="slot" value="${escapeHtml(item.slot || "")}" placeholder="locomotion">
    <select data-import-material-field="textureSemantic" title="Color=sRGB, Normal=NormalMap, Mask=线性">
      <option value=""${item.textureSemantic ? "" : " selected"}>未声明</option>
      ${["Color", "Normal", "Mask"].map(value => `<option value="${value}"${value === item.textureSemantic ? " selected" : ""}>${value}</option>`).join("")}
    </select>
    <input data-import-material-field="resourceKey" value="${escapeHtml(item.resourceKey || status.resourceKey || "")}" placeholder="auto">
    <div class="import-materials-row-actions">
      <label title="必填来源"><input type="checkbox" data-import-material-field="required" ${item.required !== false ? "checked" : ""}> 必填</label>
      <button type="button" data-import-material-action="syncOne" data-import-material-id="${escapeHtml(status.id)}">同步</button>
      <button type="button" data-import-material-action="remove" data-import-material-id="${escapeHtml(status.id)}">移除</button>
    </div>
    <div class="import-materials-row-meta">
      ${surfaceControls}
      <code title="${escapeHtml(status.resolvedSourcePath || "")}">${escapeHtml(status.resolvedSourcePath || "未解析")}</code>
      <span>${escapeHtml(status.resourceExists ? "资源已生成" : "未生成资源")}</span>
      <span>${escapeHtml(status.unitySynced ? (status.unityImportStatus || "Unity 已同步") : "Unity 待处理")}</span>
    </div>
  </div>`;
}

function getImportMaterialStatusTone(status) {
  if (status === "Clean") return "ok";
  if (status === "Changed" || status === "NotImported" || status === "UnityPending" || status === "Draft" || status === "OptionalMissing") return "warn";
  if (status === "Missing" || status === "RequiredMissing" || status === "Blocked") return "error";
  if (status === "Optional" || status === "OptionalEmpty") return "muted";
  return "pending";
}

function formatImportMaterialStatusLabel(status) {
  const normalized = String(status || "Unknown");
  const labels = {
    NeedsCheck: "待检查",
    Clean: "已同步",
    Changed: "有改动",
    NotImported: "未同步",
    UnityPending: "Unity 待处理",
    Draft: "草稿",
    Missing: "缺失",
    RequiredMissing: "必填缺失",
    Blocked: "阻塞",
    Optional: "可选",
    OptionalMissing: "可选缺失",
    OptionalEmpty: "可选空缺",
    Unknown: "未知"
  };
  return labels[normalized] || normalized;
}

function getImportMaterialsDocument() {
  if (!state.importMaterials) state.importMaterials = normalizeImportMaterialsPayload(null);
  state.importMaterials.document = normalizeImportMaterialsDocument(state.importMaterials.document || createEmptyImportMaterialsDocument());
  return state.importMaterials.document;
}

function getSelectedImportMaterialIds() {
  return Array.from(state.importMaterialsSelectedIds || new Set());
}

function markImportMaterialsDirty() {
  state.importMaterialsDirty = true;
  renderShellStatus();
}

function hasUnsavedImportMaterialsChanges() {
  return Boolean(state.importMaterialsDirty);
}

function reconcileImportMaterialSelection() {
  if (!(state.importMaterialsSelectedIds instanceof Set)) state.importMaterialsSelectedIds = new Set();
  const document = getImportMaterialsDocument();
  const valid = new Set(document.items.map(item => item.id));
  for (const id of Array.from(state.importMaterialsSelectedIds)) {
    if (!valid.has(id)) state.importMaterialsSelectedIds.delete(id);
  }
}

function handleImportMaterialsInput(event) {
  const rootInput = event.target.closest("[data-import-material-root]");
  if (rootInput) {
    getImportMaterialsDocument().sourceRoot = normalizeImportMaterialPath(rootInput.value);
    markImportMaterialsDirty();
    state.importMaterialSourceCandidates = null;
    state.importMaterialUnrecognizedPaths = [];
    if (event.type === "change") renderImportMaterialsPanel();
    return;
  }

  const selectInput = event.target.closest("[data-import-material-select]");
  if (selectInput) {
    const id = selectInput.dataset.importMaterialSelect;
    if (selectInput.checked) state.importMaterialsSelectedIds.add(id);
    else state.importMaterialsSelectedIds.delete(id);
    renderImportMaterialsPanel();
    return;
  }

  const sourceSearch = event.target.closest("[data-import-material-source-search]");
  if (sourceSearch) {
    state.importMaterialSourcePickerSearch = String(sourceSearch.value || "");
    renderImportMaterialsPanel();
    return;
  }

  const sourceType = event.target.closest("[data-import-material-source-type]");
  if (sourceType) {
    state.importMaterialSourcePickerType = String(sourceType.value || "");
    renderImportMaterialsPanel();
    return;
  }

  const sourceRootSelect = event.target.closest("[data-import-material-source-root]");
  if (sourceRootSelect) {
    state.importMaterialSourcePickerRoot = normalizeImportMaterialPath(sourceRootSelect.value || "");
    state.importMaterialSourceCandidates = null;
    state.message = "正在加载源文件候选...";
    renderShellStatus();
    loadImportMaterialSourceCandidates({ sourceRoot: state.importMaterialSourcePickerRoot, query: state.importMaterialSourcePickerSearch || "" })
      .finally(() => {
        state.message = "";
        renderShellStatus();
        renderImportMaterialsPanel();
      });
    return;
  }

  const fieldInput = event.target.closest("[data-import-material-field]");
  if (!fieldInput) return;
  const row = fieldInput.closest("[data-import-material-id]");
  const item = findImportMaterialItem(row?.dataset.importMaterialId);
  if (!item) return;
  const field = fieldInput.dataset.importMaterialField;
  if (field === "required") item.required = Boolean(fieldInput.checked);
  else if (field === "alphaCutoff") item.alphaCutoff = Number(fieldInput.value);
  else item[field] = field === "sourcePath" ? normalizeImportMaterialPath(fieldInput.value) : String(fieldInput.value || "");
  if (field === "presetId" && !item.bindTarget) item.bindTarget = inferImportMaterialBindTarget(item.presetId);
  if (field === "sourcePath") item.source = { kind: "file", scope: inferImportMaterialSourceScope(item.sourcePath), path: item.sourcePath, selection: null };
  if (field === "resourceKey" || field === "bindTarget" || field === "slot" || field === "textureSemantic" || field === "materialSurfaceMode" || field === "alphaCutoff") {
    item.target = {
      resourceKey: item.resourceKey || "",
      resourceKind: item.target?.resourceKind || "",
      resourceUsage: item.target?.resourceUsage || "",
      bindTarget: item.bindTarget || "",
      slot: item.slot || "",
      textureSemantic: item.textureSemantic || "",
      materialSurfaceMode: item.materialSurfaceMode || "",
      alphaCutoff: Number.isFinite(Number(item.alphaCutoff)) ? Number(item.alphaCutoff) : 0.5
    };
  }
  markImportMaterialsDirty();
  const shouldRender = event.type === "change" || fieldInput.type === "checkbox" || fieldInput.tagName === "SELECT";
  if (shouldRender) renderImportMaterialsPanel();
}

async function handleImportMaterialsClick(event) {
  const button = event.target.closest("button[data-import-material-action]");
  if (!button) return;
  const action = button.dataset.importMaterialAction;
  if (action === "applyTemplate") applyImportMaterialTemplate();
  else if (action === "addRow") addImportMaterialRow();
  else if (action === "appendPasted") await appendPastedImportMaterialPaths();
  else if (action === "remove") removeImportMaterialRow(button.dataset.importMaterialId);
  else if (action === "save") await saveImportMaterials();
  else if (action === "recheck") await loadImportMaterialsAndRender();
  else if (action === "syncChanged") await syncImportMaterials({ allChanged: true });
  else if (action === "openResourceManagerCommonResources") openResourceManagerCommonResources();
  else if (action === "syncSelected") await syncImportMaterials({ itemIds: getSelectedImportMaterialIds() });
  else if (action === "syncOne") await syncImportMaterials({ itemIds: [button.dataset.importMaterialId] });
  else if (action === "syncSlot") await syncImportMaterialSlot(button.dataset.importMaterialSlot);
  else if (action === "clearSlot") clearImportMaterialSlot(button.dataset.importMaterialSlot);
  else if (action === "choosePublicBodyModel" || action === "choosePublicSourceResource") await choosePublicImportMaterialResource(button.dataset.importMaterialSlot);
  else if (action === "openSingleWeaponConfiguration") openSingleWeaponConfiguration(button.dataset.singleWeaponConfigurationId);
  else if (action === "openSourcePicker") await openImportMaterialSourcePicker(button);
  else if (action === "closeSourcePicker") closeImportMaterialSourcePicker();
  else if (action === "reloadSourceCandidates") await reloadImportMaterialSourceCandidates();
  else if (action === "selectSourceCandidate") selectImportMaterialSourceCandidate(button.dataset.importMaterialCandidate);
}

async function choosePublicBodyModel(slotKey) {
  await choosePublicImportMaterialResource(slotKey);
}

async function choosePublicImportMaterialResource(slotKey) {
  const definition = getImportMaterialSlotDefinition(slotKey || "body");
  const item = definition ? findImportMaterialSlotItem(getImportMaterialsDocument(), definition) : null;
  await openImportMaterialResourceRefPicker(item?.id || "", definition?.key || "body");
}

async function chooseDefaultWeaponReference(slot) {
  const configuration = getActiveWeaponConfiguration();
  const selected = configuration?.slots?.find(item => item?.slotId === slot);
  if (!selected?.singleWeaponConfigurationId || selected.singleWeaponConfigurationId === "weapon.unarmed") {
    state.message = `${slot || "weapon"} 当前为 weapon.unarmed；空手没有模型资源。`;
    renderShellStatus();
    return;
  }
  openSingleWeaponConfiguration(selected.singleWeaponConfigurationId);
}

function clearDefaultWeaponReference(slot) {
  state.message = "武器资源属于全局单一武器配置，不能从角色原材料面板清空。";
  renderShellStatus();
}

function openSingleWeaponConfiguration(singleWeaponConfigurationId) {
  if (!singleWeaponConfigurationId || singleWeaponConfigurationId === "weapon.unarmed")
    return;
  state.activeContextTab = "singleWeapons";
  state.contextDockCollapsed = false;
  globalThis.activateSingleWeaponForEditing?.(singleWeaponConfigurationId);
}

function applyImportMaterialTemplate() {
  const templateId = el.importMaterialsPanel?.querySelector("[data-import-material-template]")?.value || IMPORT_MATERIAL_TEMPLATES[0].id;
  const templates = state.importMaterials?.templates || IMPORT_MATERIAL_TEMPLATES;
  const template = templates.find(item => item.id === templateId) || IMPORT_MATERIAL_TEMPLATES[0];
  const document = getImportMaterialsDocument();
  document.items = (template.items || []).map((item, index) => normalizeImportMaterialItem(clone(item), index));
  markImportMaterialsDirty();
  state.importMaterialsTemplateStaged = true;
  reconcileImportMaterialSelection();
  renderImportMaterialsPanel();
}

function addImportMaterialRow(sourcePath = "", options = {}) {
  const document = getImportMaterialsDocument();
  const item = normalizeImportMaterialItem({
    sourcePath,
    presetId: inferImportMaterialPresetId(sourcePath),
    label: importMaterialDefaultLabel(sourcePath, document.items.length)
  }, document.items.length);
  document.items.push(item);
  markImportMaterialsDirty();
  reconcileImportMaterialSelection();
  if (options.render !== false) renderImportMaterialsPanel();
  return item;
}

async function openImportMaterialSourcePicker(button) {
  state.importMaterialSourcePicker = {
    slotKey: button?.dataset?.importMaterialSlot || "",
    itemId: button?.dataset?.importMaterialId || "",
    target: button?.dataset?.importMaterialTarget || ""
  };
  state.importMaterialSourcePickerSearch = "";
  state.importMaterialSourcePickerType = "";
  state.importMaterialSourcePickerRoot = getImportMaterialsDocument().sourceRoot || "";
  state.message = "正在加载源文件候选...";
  renderShellStatus();
  await ensureImportMaterialSourceCandidates({ force: true, query: "" });
  state.message = "";
  renderShellStatus();
  renderImportMaterialsPanel();
}

function closeImportMaterialSourcePicker() {
  state.importMaterialSourcePicker = null;
  state.importMaterialSourcePickerSearch = "";
  state.importMaterialSourcePickerType = "";
  state.importMaterialSourcePickerRoot = "";
  renderImportMaterialsPanel();
}

async function reloadImportMaterialSourceCandidates() {
  state.message = "正在刷新源文件候选...";
  renderShellStatus();
  await ensureImportMaterialSourceCandidates({ force: true, query: state.importMaterialSourcePickerSearch || "" });
  state.message = "";
  renderShellStatus();
  renderImportMaterialsPanel();
}

function selectImportMaterialSourceCandidate(candidateId) {
  const candidate = findImportMaterialSourceCandidate(candidateId);
  if (!candidate || !candidate.selectable) return;
  applyImportMaterialSourceCandidate(candidate, state.importMaterialSourcePicker || {});
  state.importMaterialSourcePicker = null;
  markImportMaterialsDirty();
  state.importMaterialUnrecognizedPaths = [];
  reconcileImportMaterialSelection();
  renderImportMaterialsPanel();
}

function findImportMaterialSourceCandidate(candidateId) {
  const items = state.importMaterialSourceCandidates?.items || [];
  return items.find(item => item.id === candidateId) || null;
}

function applyImportMaterialSourceCandidate(candidate, picker = {}) {
  if (picker.slotKey) {
    const item = ensureImportMaterialSlotItem(picker.slotKey);
    const definition = getImportMaterialSlotDefinition(picker.slotKey);
    if (!item || !definition) return null;
    item.label = definition.label;
    item.sourcePath = candidate.sourcePath;
    item.source = { kind: "file", scope: candidate.sourceScope || "package", path: candidate.sourcePath, selection: null };
    item.target = { resourceKey: item.resourceKey || "", bindTarget: definition.bindTarget || "", slot: definition.slot || "" };
    item.presetId = definition.presetId;
    item.slot = definition.slot || "";
    item.bindTarget = definition.bindTarget || "";
    item.required = definition.required !== false;
    item.lastImportedHash = "";
    item.lastImportedUtc = "";
    return item;
  }

  const item = picker.itemId ? findImportMaterialItem(picker.itemId) : addImportMaterialRow("", { render: false });
  if (!item) return null;
  item.sourcePath = candidate.sourcePath;
  item.source = { kind: "file", scope: candidate.sourceScope || "package", path: candidate.sourcePath, selection: null };
  item.presetId = candidate.presetId || inferImportMaterialPresetId(candidate.sourcePath);
  item.bindTarget = candidate.bindTarget || inferImportMaterialBindTarget(item.presetId);
  item.slot = "";
  item.target = { resourceKey: item.resourceKey || "", bindTarget: item.bindTarget || "", slot: item.slot || "" };
  item.label = item.label || importMaterialDefaultLabel(candidate.sourcePath, getImportMaterialsDocument().items.length - 1);
  item.lastImportedHash = "";
  item.lastImportedUtc = "";
  return item;
}

async function appendPastedImportMaterialPaths() {
  const textarea = el.importMaterialsPanel?.querySelector("[data-import-material-paths]");
  const paths = String(textarea?.value || "")
    .split(/\r?\n/)
    .map(value => normalizeImportMaterialPath(value))
    .filter(Boolean);
  if (!paths.length) return;

  const payload = await ensureImportMaterialSourceCandidates({ force: true, query: "" });
  const diagnostics = [];
  let appliedCount = 0;
  for (const sourcePath of paths) {
    const candidate = matchImportMaterialSourceCandidate(sourcePath, payload.items || []);
    if (!candidate) {
      diagnostics.push({ path: sourcePath, message: "没有匹配的项目/包内候选。请检查源根目录或文件位置。" });
      continue;
    }
    if (!candidate.selectable) {
      diagnostics.push({ path: sourcePath, message: "已匹配候选，但当前版本不能直接同步。" });
      continue;
    }

    const slotKey = candidate.slotKey || inferImportMaterialSlotKey(candidate.sourcePath || candidate.displayPath);
    const slotItem = slotKey ? ensureImportMaterialSlotItem(slotKey) : null;
    if (slotItem && !slotItem.sourcePath) {
      applyImportMaterialSourceCandidate(candidate, { slotKey });
    } else {
      applyImportMaterialSourceCandidate(candidate, { target: "extra" });
    }
    appliedCount++;
  }
  state.importMaterialUnrecognizedPaths = diagnostics;
  if (appliedCount) markImportMaterialsDirty();
  reconcileImportMaterialSelection();
  renderImportMaterialsPanel();
  if (textarea) textarea.value = "";
}

function matchImportMaterialSourceCandidate(path, candidates) {
  const normalized = normalizeImportMaterialPath(path).toLowerCase();
  return candidates.find(candidate => {
    const sourcePath = normalizeImportMaterialPath(candidate.sourcePath).toLowerCase();
    const displayPath = normalizeImportMaterialPath(candidate.displayPath).toLowerCase();
    return sourcePath === normalized || displayPath === normalized || displayPath.endsWith(`/${normalized}`);
  }) || null;
}

function removeImportMaterialRow(id) {
  const document = getImportMaterialsDocument();
  document.items = document.items.filter(item => item.id !== id);
  state.importMaterialsSelectedIds.delete(id);
  markImportMaterialsDirty();
  renderImportMaterialsPanel();
}

function clearImportMaterialSlot(slotKey) {
  const definition = getImportMaterialSlotDefinition(slotKey);
  if (!definition) return;
  const document = getImportMaterialsDocument();
  const item = findImportMaterialSlotItem(document, definition);
  if (!item) return;
  document.items = document.items.filter(candidate => candidate !== item);
  state.importMaterialsSelectedIds.delete(item.id);
  markImportMaterialsDirty();
  renderImportMaterialsPanel();
}

async function syncImportMaterialSlot(slotKey) {
  const definition = getImportMaterialSlotDefinition(slotKey);
  const item = definition ? findImportMaterialSlotItem(getImportMaterialsDocument(), definition) : null;
  if (!hasImportMaterialSourceConfigured(item)) {
    state.message = "同步前需要先选择源文件或公共资源引用。";
    renderShellStatus();
    renderImportMaterialsPanel();
    return;
  }
  await syncImportMaterials({ itemIds: [item.id] });
}

function findImportMaterialItem(id) {
  return getImportMaterialsDocument().items.find(item => item.id === id);
}

async function loadImportMaterialsAndRender() {
  return checkCharacterSourceSelection();
}

async function saveImportMaterials(options = {}) {
  if (!state.canWrite || !state.apiAvailable) {
    if (!options.suppressMessage) {
      state.message = "Authoring 服务未连接，不能保存 character_sources.json。";
      renderShellStatus();
    }
    return false;
  }
  return runImportMaterialsOperation("save", (operation, current) => saveCharacterSourceSelection(operation, current, options));
}

async function saveImportMaterialsIfDirty(options = {}) {
  if (!hasUnsavedImportMaterialsChanges()) return true;
  return await saveImportMaterials(options);
}

async function syncImportMaterials(options = {}) {
  if (!state.canWrite || !state.apiAvailable) return;
  return syncCharacterSourceSelection(options);
}

function openResourceManagerCommonResources() {
  openResourceManagerForPublicResource("", {
    workspace: "browse",
    message: "已打开 Resource Manager。公共资源同步和修复在那里处理。"
  });
}

function formatImportMaterialOperationLabel(label) {
  const key = String(label || "");
  const labels = {
    "sync character sources": "同步角色源文件",
    compile: "Prefab 预检",
    "Unity import": "Unity 导入"
  };
  return labels[key] || key;
}

function prepareImportMaterialsDocumentForSave(document) {
  const normalized = normalizeImportMaterialsDocument(document);
  normalized.items = normalized.items
    .filter(item => item.required !== false || hasImportMaterialSourceConfigured(item))
    .map(item => {
    item.source = item.source || { kind: "file", scope: inferImportMaterialSourceScope(item.sourcePath), path: item.sourcePath, selection: null };
    if (item.source.kind === "resourceRef") {
      item.source.scope = item.source.scope || "public";
      item.source.path = "";
      item.sourcePath = "";
    } else if (item.source.kind === "file") {
      item.source.scope = item.source.scope || inferImportMaterialSourceScope(item.sourcePath);
      item.source.path = item.source.path || item.sourcePath;
    }
    item.target = {
      ...normalizeImportMaterialTargetRef(item.target),
      resourceKey: item.resourceKey || item.target?.resourceKey || "",
      bindTarget: item.bindTarget || item.target?.bindTarget || "",
      slot: item.slot || item.target?.slot || "",
      textureSemantic: item.textureSemantic || item.target?.textureSemantic || "",
      materialSurfaceMode: item.materialSurfaceMode || item.target?.materialSurfaceMode || "",
      alphaCutoff: item.alphaCutoff ?? item.target?.alphaCutoff ?? 0.5
    };
    return item;
  });
  return normalized;
}

function normalizeImportMaterialSourceRef(source) {
  if (!source) return { kind: "file", scope: "package", path: "", selection: null };
  return {
    kind: source.kind || "file",
    scope: source.scope || "package",
    path: normalizeImportMaterialPath(source.path || ""),
    selection: source.selection || null
  };
}

function normalizeImportMaterialTargetRef(target) {
  if (!target) return { resourceKey: "", resourceKind: "", resourceUsage: "", bindTarget: "", slot: "", textureSemantic: "", materialSurfaceMode: "", alphaCutoff: 0.5 };
  return {
    resourceKey: target.resourceKey || "",
    resourceKind: target.resourceKind || "",
    resourceUsage: target.resourceUsage || "",
    bindTarget: target.bindTarget || "",
    slot: target.slot || "",
    textureSemantic: ["Color", "Normal", "Mask"].find(value => value.toLowerCase() === String(target.textureSemantic || "").toLowerCase()) || "",
    materialSurfaceMode: ["Opaque", "AlphaClip"].find(value => value.toLowerCase() === String(target.materialSurfaceMode || "").toLowerCase()) || target.materialSurfaceMode || "",
    alphaCutoff: Number.isFinite(Number(target.alphaCutoff)) ? Number(target.alphaCutoff) : 0.5
  };
}

Object.assign(globalThis, {
  normalizeImportMaterialsPayload,
  normalizeImportMaterialsDocument,
  normalizeImportMaterialItem,
  normalizeImportMaterialStatus,
  normalizeImportMaterialSourceCandidatesPayload,
  normalizeImportMaterialSourceCandidate,
  summarizeImportMaterialStatuses,
  createEmptyImportMaterialsDocument,
  convertCharacterSourceManifestToImportMaterialsDocument,
  normalizeImportMaterialPath,
  inferImportMaterialSourceScope,
  buildImportMaterialItemId,
  normalizeIdSegment,
  importMaterialDefaultLabel,
  inferImportMaterialPresetId,
  inferImportMaterialBindTarget,
  getImportMaterialSlotDefinition,
  createImportMaterialSlotItem,
  findImportMaterialSlotItem,
  ensureImportMaterialSlotItem,
  inferImportMaterialSlotKey,
  renderImportMaterialsPanel,
  renderImportMaterialSummaryBadge,
  renderDefaultWeaponReferenceCards,
  getDefaultWeaponReferenceCards,
  renderCommonResourcesStatusBar,
  renderDefaultWeaponReferenceCard,
  renderImportMaterialsBatchTools,
  renderImportMaterialsManifestDetails,
  getImportMaterialsManifestState,
  renderImportMaterialsManifestState,
  buildImportMaterialSlotStatuses,
  getImportMaterialSlotDefinitionForItem,
  getImportMaterialSlotDefinitionOrder,
  renderImportMaterialSlotCards,
  renderImportMaterialSlotCard,
  getImportMaterialSlotDisplayStatus,
  renderBodyFbxTextureDiagnostic,
  renderImportMaterialSourceThumbnail,
  getImportMaterialSourceThumbnailUrl,
  isImportMaterialBrowserImagePath,
  renderImportMaterialsExtraResources,
  renderImportMaterialExtraRow,
  renderImportMaterialPasteDiagnostics,
  renderImportMaterialSourcePicker,
  renderImportMaterialSourceCandidate,
  getVisibleImportMaterialSourceCandidates,
  getImportMaterialSourceCandidateTypeOptions,
  getImportMaterialSourceRootOptions,
  isImportMaterialCandidateCompatibleWithSlot,
  compareImportMaterialSourceCandidates,
  getImportMaterialCandidateScopeRank,
  getImportMaterialCandidateSlotRank,
  getImportMaterialCandidateExtensionRank,
  getImportMaterialCandidateKind,
  getImportMaterialSourceCandidateSearchText,
  renderImportMaterialsAdvancedList,
  renderImportMaterialsEmpty,
  renderImportMaterialsTable,
  renderImportMaterialRow,
  getImportMaterialStatusTone,
  getImportMaterialsDocument,
  getSelectedImportMaterialIds,
  markImportMaterialsDirty,
  hasUnsavedImportMaterialsChanges,
  reconcileImportMaterialSelection,
  handleImportMaterialsInput,
  handleImportMaterialsClick,
  chooseDefaultWeaponReference,
  clearDefaultWeaponReference,
  applyImportMaterialTemplate,
  addImportMaterialRow,
  openImportMaterialSourcePicker,
  closeImportMaterialSourcePicker,
  reloadImportMaterialSourceCandidates,
  selectImportMaterialSourceCandidate,
  findImportMaterialSourceCandidate,
  applyImportMaterialSourceCandidate,
  appendPastedImportMaterialPaths,
  matchImportMaterialSourceCandidate,
  removeImportMaterialRow,
  clearImportMaterialSlot,
  syncImportMaterialSlot,
  choosePublicBodyModel,
  choosePublicImportMaterialResource,
  findImportMaterialItem,
  loadImportMaterialsAndRender,
  saveImportMaterials,
  saveImportMaterialsIfDirty,
  syncImportMaterials,
  openResourceManagerCommonResources,
  prepareImportMaterialsDocumentForSave,
  normalizeImportMaterialSourceRef,
  normalizeImportMaterialTargetRef
});
