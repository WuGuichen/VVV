const DEFAULT_PACKAGE = "Tools/MxFramework.Authoring/samples/character-iron-vanguard";
const LAYERS = { colliders: true, sockets: true, traces: true, weapons: true };
const LOADOUTS = [
  { id: "unarmed", label: "徒手", slots: [] },
  { id: "single_sword", label: "单手剑", slots: ["mainHand"] },
  { id: "sword_shield", label: "剑盾", slots: ["mainHand", "offHand"] }
];

const DEFAULT_ANIMATION_PROFILE_ID = "anim_profile.default";
const ANIMATION_PROFILE_SLOTS = [
  { slotId: "base", displayName: "基础 Profile", purpose: "默认姿势和基础表现", resourceHint: "locomotion", required: false },
  { slotId: "locomotion", displayName: "移动 Locomotion", purpose: "idle / walk / run 等移动表现", resourceHint: "locomotion", required: true },
  { slotId: "combat", displayName: "战斗 Combat", purpose: "攻击、防御、受击等战斗表现", resourceHint: "combat", required: false }
];

const MODEL_IMPORT_ROLES = {
  body: {
    label: "角色主体模型",
    title: "替换角色主体模型资源",
    pending: "正在导入角色主体模型",
    done: "角色主体模型已导入"
  },
  mainHand: {
    label: "主手槽武器模型",
    title: "替换当前 mainHand 槽引用的武器预览模型；不创建新的武器定义",
    pending: "正在导入主手槽武器模型",
    done: "主手槽武器模型已导入并绑定到 mainHand"
  },
  offHand: {
    label: "副手槽武器模型",
    title: "替换当前 offHand 槽引用的武器预览模型；不创建新的武器定义",
    pending: "正在导入副手槽武器模型",
    done: "副手槽武器模型已导入并绑定到 offHand"
  },
  preview: {
    label: "仅选中资源",
    title: "仅导入到资源目录，不自动挂到角色主体或武器槽",
    pending: "正在导入资源目录模型",
    done: "模型已导入资源目录"
  }
};

const MODEL_USAGE_OPTIONS = [
  { value: "characterModel", label: "角色主体模型" },
  { value: "weaponModel", label: "武器模型" },
  { value: "previewMesh", label: "仅预览模型" }
];

const RESOURCE_KIND_LABELS = {
  Model: "模型",
  Animation: "动画",
  Texture: "贴图",
  Material: "材质",
  AvatarMask: "AvatarMask",
  Vfx: "VFX",
  Audio: "音频",
  Config: "配置",
  Generated: "生成资源"
};

const PLAN_GROUPS = [
  { key: "spawnCritical", label: "SpawnCritical", failurePolicy: "FailSpawn" },
  { key: "equipmentInitial", label: "EquipmentInitial", failurePolicy: "UseFallbackEquipment" },
  { key: "animationWarmup", label: "AnimationWarmup", failurePolicy: "UseFallbackPose" },
  { key: "vfxWarmup", label: "VfxWarmup", failurePolicy: "SkipEffect" },
  { key: "uiDeferred", label: "UiDeferred", failurePolicy: "ShowPlaceholder" },
  { key: "audio", label: "Audio", failurePolicy: "MuteMissingCue" }
];

const RESOURCE_FIELD_SPECS = {
  body: {
    fieldKey: "Character.Model",
    editorKind: "CharacterStudio",
    displayName: "角色主体模型",
    acceptedKinds: ["Model"],
    acceptedUsages: ["characterModel"],
    acceptedProviderIds: ["runtimeCatalog", "characterPackage"],
    acceptedBindingKinds: ["ResourceManagerAsset"],
    requireRuntimeLoadable: true,
    requireUnityImported: true,
    allowIncompatibleWithWarning: false,
    preloadPolicy: "SpawnCritical",
    outputKind: "RuntimeResourceKey"
  },
  mainHand: {
    fieldKey: "Equipment.MainHand.Model",
    editorKind: "CharacterStudio",
    displayName: "主手武器模型",
    acceptedKinds: ["Model"],
    acceptedUsages: ["weaponModel"],
    acceptedProviderIds: ["runtimeCatalog", "characterPackage"],
    acceptedBindingKinds: ["ResourceManagerAsset"],
    requireRuntimeLoadable: true,
    requireUnityImported: true,
    allowIncompatibleWithWarning: true,
    compatibilityFilter: { slotId: "mainHand" },
    preloadPolicy: "EquipmentInitial",
    outputKind: "RuntimeResourceKey"
  },
  offHand: {
    fieldKey: "Equipment.OffHand.Model",
    editorKind: "CharacterStudio",
    displayName: "副手武器模型",
    acceptedKinds: ["Model"],
    acceptedUsages: ["weaponModel"],
    acceptedProviderIds: ["runtimeCatalog", "characterPackage"],
    acceptedBindingKinds: ["ResourceManagerAsset"],
    requireRuntimeLoadable: true,
    requireUnityImported: true,
    allowIncompatibleWithWarning: true,
    compatibilityFilter: { slotId: "offHand" },
    preloadPolicy: "EquipmentInitial",
    outputKind: "RuntimeResourceKey"
  },
  animationClip: {
    fieldKey: "Animation.Clip",
    editorKind: "CharacterStudio",
    displayName: "动画 Clip",
    acceptedKinds: ["Animation"],
    acceptedUsages: ["animationClip", "animationClipGroup"],
    acceptedProviderIds: ["unityProjectAssets", "unityAssetDatabase", "runtimeCatalog", "characterPackage"],
    acceptedBindingKinds: ["UnityEditorOnlyAsset", "UnityAsset", "ResourceManagerAsset", "PackageResource"],
    requireRuntimeLoadable: false,
    requireUnityImported: false,
    allowIncompatibleWithWarning: true,
    preloadPolicy: "AnimationWarmup",
    outputKind: "ResourceSelectionRef"
  },
  preview: {
    fieldKey: "CharacterStudio.Selection",
    editorKind: "CharacterStudio",
    displayName: "资源库浏览",
    acceptedKinds: ["Model", "Animation", "Texture", "Material", "Vfx", "Audio", "Config", "Generated"],
    acceptedUsages: [],
    acceptedProviderIds: [],
    acceptedBindingKinds: ["ResourceManagerAsset", "UnityEditorOnlyAsset", "AudioEventDefinition", "AudioCue", "GeneratedPreviewOnly"],
    requireRuntimeLoadable: false,
    requireUnityImported: false,
    allowIncompatibleWithWarning: true,
    preloadPolicy: "None",
    outputKind: "ResourceSelectionRef"
  }
};

const FIELD_GROUP_LABELS = {
  resource: "资源身份",
  modelTransform: "模型尺寸 / 旋转 / 位置修正",
  base: "基础属性",
  binding: "引用关系",
  animation: "动画配置",
  animationSlot: "动画槽位",
  selection: "资源选择",
  combat: "战斗映射",
  usage: "用途",
  localPose: "局部变换",
  poseParent: "局部父空间",
  shape: "形状尺寸",
  trace: "轨迹"
};

const BODY_PART_KIND_OPTIONS = ["Unknown", "Bone", "Primitive", "Virtual"];
const POSE_PARENT_KIND_OPTIONS = ["ModelRoot", "SkeletonRoot", "Bone", "Locator", "BodyPart", "Socket", "WorldPreview"];
const SOCKET_USAGE_OPTIONS = ["Unknown", "Weapon", "Vfx", "Camera", "Ui", "Gameplay"];
const SOCKET_HANDEDNESS_OPTIONS = [
  { value: "None", label: "无" },
  { value: "Left", label: "左手" },
  { value: "Right", label: "右手" },
  { value: "Both", label: "双手" }
];
const SOCKET_SIDE_OPTIONS = [
  { value: "Center", label: "中心" },
  { value: "Left", label: "左" },
  { value: "Right", label: "右" },
  { value: "Front", label: "前" },
  { value: "Back", label: "后" }
];
const EQUIP_SLOT_OPTIONS = [
  { value: "mainHand", label: "主手" },
  { value: "offHand", label: "副手" },
  { value: "twoHand", label: "双手" },
  { value: "naturalWeapon", label: "天然武器" }
];
const TRACE_SAMPLE_RULE_OPTIONS = ["LineSegment", "CapsuleSweep", "FixedSamples"];
const COMMON_BODY_PART_IDS = ["root", "torso", "head", "right_hand", "left_hand", "right_leg", "left_leg", "main_hand", "off_hand"];
const COMMON_SOCKET_IDS = ["mainHand", "offHand", "back", "headVfx", "camera", "uiAnchor"];
const COMMON_TAGS = [
  "body", "core", "critical", "hand", "leg", "main", "offhand", "stow",
  "weapon", "vfx", "weakPoint", "characterstudio-bind", "characterstudio-import", "converted-from-fbx"
];
const COMMON_ACTION_KEYS = ["primary", "secondary", "guard", "punch", "slash", "shield_guard"];
const COMMON_REACTION_GROUPS = ["reaction.humanoid.body", "reaction.humanoid.head", "reaction.humanoid.limb", "react.body"];

const PREVIEW_POSES = [
  { id: "bind", label: "绑定姿势" },
  { id: "guard", label: "持武防御" },
  { id: "attack", label: "挥击预览" },
  { id: "inspect", label: "展开检查" }
];

const PREVIEW_MOTIONS = [
  { id: "none", label: "静止" },
  { id: "breath", label: "呼吸" },
  { id: "weapon_sway", label: "武器摆动" },
  { id: "walk_cycle", label: "步行动作" }
];

const KIND_LABELS = {
  manifest: "清单",
  resources: "资源",
  resource: "资源",
  config: "配置",
  animationConfig: "动画",
  animationProfile: "动画",
  animationSlot: "动画槽",
  body: "身体",
  part: "部位",
  collider: "碰撞体",
  socket: "挂点",
  weapon: "武器",
  trace: "轨迹",
  validation: "诊断",
  issue: "问题"
};

const state = {
  packages: [],
  packageRelative: new URLSearchParams(window.location.search).get("package") || DEFAULT_PACKAGE,
  package: null,
  validation: null,
  compileResult: null,
  importResult: null,
  unityResourceCatalog: null,
  unityResourceCatalogPath: "",
  resourceLibrary: null,
  resourcePlan: null,
  selectedPath: "manifest",
  activeLoadout: "sword_shield",
  layers: { ...LAYERS },
  dirty: false,
  canWrite: false,
  apiAvailable: false,
  message: "",
  userSelectedPackage: Boolean(new URLSearchParams(window.location.search).get("package")),
  previewBones: [],
  previewBoneKey: "",
  activeBoneFieldPath: "",
  highlightedBoneValue: "",
  bonePickerOpen: false,
  previewPose: "bind",
  previewMotion: "none",
  viewportCameraState: null,
  skipNextCameraRemember: false,
  treeCollapsed: false,
  resourcePickerOpen: false,
  resourcePickerField: null,
  resourcePickerQuery: null,
  resourcePickerLoading: false
};

const el = {};
let threeRuntimePromise = null;
let viewportRenderId = 0;
let viewportCleanup = null;

document.addEventListener("DOMContentLoaded", () => {
  for (const id of [
    "packageSelect", "reloadButton", "saveButton", "compileButton", "importButton",
    "modelImportRole", "modelImportButton", "modelFileInput",
    "packageSummary", "packageTree", "dirtyBadge", "loadoutTabs", "viewport",
    "workspace", "treeCollapseButton", "previewPoseSelect", "previewMotionSelect", "resetCameraButton",
    "configCreateSelect", "configCreateButton",
    "resourceBindingTarget", "openResourcePickerButton", "clearModelBindingButton",
    "animationConfigPanel",
    "resourcePickerOverlay", "resourcePickerTitle", "resourcePickerSummary", "resourcePickerList", "closeResourcePickerButton",
    "resourcePlanPreview",
    "inspector", "diagnostics", "importStatus", "selectionBadge", "copyReportButton",
    "subtitle"
  ]) el[id] = document.getElementById(id);

  el.reloadButton.addEventListener("click", () => loadAll());
  el.saveButton.addEventListener("click", () => savePackage());
  el.compileButton.addEventListener("click", () => compilePackage());
  el.importButton.addEventListener("click", () => importUnity());
  el.modelImportButton.addEventListener("click", () => {
    if (!state.canWrite) {
      state.message = "静态预览不能导入模型。请通过 Authoring server 打开页面。";
      renderShellStatus();
      return;
    }
    el.modelFileInput.value = "";
    el.modelFileInput.click();
  });
  el.modelFileInput.addEventListener("change", () => {
    const file = el.modelFileInput.files?.[0];
    if (file) importModel(file);
  });
  el.modelImportRole.addEventListener("change", () => {
    updateModelImportTitle();
    renderResourceBindingBar();
    renderResourcePicker();
  });
  el.treeCollapseButton.addEventListener("click", () => {
    state.treeCollapsed = !state.treeCollapsed;
    renderLayoutState();
  });
  el.configCreateButton.addEventListener("click", () => createConfiguration(el.configCreateSelect.value));
  el.previewPoseSelect.addEventListener("change", event => {
    state.previewPose = event.target.value;
    renderViewport();
  });
  el.previewMotionSelect.addEventListener("change", event => {
    state.previewMotion = event.target.value;
    renderViewport();
  });
  el.resetCameraButton.addEventListener("click", () => {
    state.viewportCameraState = null;
    state.skipNextCameraRemember = true;
    renderViewport();
  });
  el.openResourcePickerButton.addEventListener("click", openResourcePicker);
  el.closeResourcePickerButton.addEventListener("click", closeResourcePicker);
  el.resourcePickerOverlay.addEventListener("click", event => {
    if (event.target === el.resourcePickerOverlay) closeResourcePicker();
  });
  el.resourcePickerList.addEventListener("click", event => {
    const button = event.target.closest("button[data-library-id]");
    if (!button) return;
    selectLibraryItem(button.dataset.libraryId);
  });
  el.clearModelBindingButton.addEventListener("click", clearCurrentModelBinding);
  el.animationConfigPanel?.addEventListener("click", event => {
    const pickButton = event.target.closest("button[data-animation-pick]");
    if (pickButton) {
      openAnimationSlotPicker(pickButton.dataset.profileId, pickButton.dataset.slotId);
      return;
    }
    const clearButton = event.target.closest("button[data-animation-clear]");
    if (clearButton) {
      clearAnimationSlotSelection(clearButton.dataset.profileId, clearButton.dataset.slotId);
      return;
    }
    const jumpButton = event.target.closest("button[data-animation-jump]");
    if (jumpButton) {
      selectPath(jumpButton.dataset.path);
    }
  });
  el.copyReportButton.addEventListener("click", () => copyReport());
  el.packageSelect.addEventListener("change", event => {
    state.packageRelative = event.target.value;
    state.userSelectedPackage = true;
    state.selectedPath = "manifest";
    loadAll();
  });
  document.getElementById("layerToggles").addEventListener("click", event => {
    const button = event.target.closest("button[data-layer]");
    if (!button) return;
    const layer = button.dataset.layer;
    state.layers[layer] = !state.layers[layer];
    button.classList.toggle("active", state.layers[layer]);
    renderViewport();
  });

  loadAll();
});

async function loadAll() {
  state.message = "Loading package...";
  renderShellStatus();
  await loadPackages();
  await loadPackageState();
  render();
}

async function loadPackages() {
  const apiPackages = await readJson("/api/character/packages", null);
  if (Array.isArray(apiPackages) && apiPackages.length > 0) {
    state.packages = apiPackages;
    state.apiAvailable = true;
  } else {
    state.packages = [{ relative: DEFAULT_PACKAGE, packageId: "iron_vanguard", kind: "character" }];
  }
  if (!state.userSelectedPackage && state.packages.length > 0) {
    state.packageRelative = state.packages[0].relative;
  } else if (!state.packages.some(pkg => pkg.relative === state.packageRelative)) {
    state.packageRelative = state.packages[0].relative;
  }
  el.packageSelect.innerHTML = state.packages.map(pkg => {
    const label = `${pkg.packageId || pkg.relative} (${pkg.version || pkg.kind || "character"})`;
    return `<option value="${escapeHtml(pkg.relative)}"${pkg.relative === state.packageRelative ? " selected" : ""}>${escapeHtml(label)}</option>`;
  }).join("");
}

async function loadPackageState() {
  state.previewBones = [];
  state.previewBoneKey = "";
  state.activeBoneFieldPath = "";
  state.highlightedBoneValue = "";
  state.bonePickerOpen = false;
  state.viewportCameraState = null;
  state.skipNextCameraRemember = true;
  const apiState = await readJson(`/api/character/state?package=${encodeURIComponent(state.packageRelative)}`, null);
  if (apiState && apiState.package) {
    state.package = clone(apiState.package);
    ensureAnimationAuthoringConfig(state.package);
    state.validation = apiState.validation || apiState.package.validationReport || { issues: [] };
    state.importResult = apiState.importReport || null;
    state.unityResourceCatalog = apiState.unityResourceCatalog || null;
    state.unityResourceCatalogPath = apiState.unityResourceCatalogPath || "";
    if (!state.unityResourceCatalog) {
      await loadUnityResourceCatalog();
    }
    await loadResourceLibraryAndPlan();
    state.canWrite = Boolean(apiState.canWrite);
    state.apiAvailable = true;
    state.dirty = false;
    state.message = "已连接 Authoring 服务。";
    return;
  }

  state.package = await readStaticPackage(state.packageRelative);
  ensureAnimationAuthoringConfig(state.package);
  state.validation = state.package.validationReport || { issues: [] };
  state.importResult = null;
  await loadUnityResourceCatalog();
  await loadResourceLibraryAndPlan();
  state.canWrite = false;
  state.apiAvailable = false;
  state.dirty = false;
  state.message = "静态预览：请启动 Authoring server 后再保存、预检、导入模型或导入 Unity。";
}

async function loadUnityResourceCatalog() {
  const packageId = state.package?.manifest?.packageId || "";
  state.unityResourceCatalog = null;
  state.unityResourceCatalogPath = "";
  if (!packageId) return;

  const catalogPath = `/Assets/MxFrameworkGenerated/CharacterPackages/${encodeURIComponent(packageId)}/config/unity_resource_catalog.json`;
  state.unityResourceCatalogPath = catalogPath.slice(1);
  state.unityResourceCatalog = await readJson(catalogPath, null);
}

async function loadResourceLibraryAndPlan() {
  const apiLibrary = await readJson(`/api/authoring/resources?package=${encodeURIComponent(state.packageRelative)}`, null);
  state.resourceLibrary = normalizeResourceLibraryPayload(apiLibrary);

  const apiPlan = await readJson(`/api/authoring/resources/resource-plan?package=${encodeURIComponent(state.packageRelative)}`, null);
  const packageId = state.package?.manifest?.packageId || "";
  const staticPlan = packageId
    ? await readJson(`/Assets/MxFrameworkGenerated/CharacterPackages/${encodeURIComponent(packageId)}/config/character_resource_plan.json`, null)
    : null;
  state.resourcePlan = normalizeResourcePlanPayload(apiPlan || staticPlan);
}

async function readStaticPackage(root) {
  const [manifest, resourceCatalog, bodyProfile, bodyParts, colliders, sockets, attachments, traces, appConfig, validation] = await Promise.all([
    readJson(`/${root}/manifest.json`, {}),
    readJson(`/${root}/resource_catalog.json`, { entries: [] }),
    readJson(`/${root}/geometry/body_geometry.json`, {}),
    readJson(`/${root}/geometry/body_parts.json`, { bodyParts: [] }),
    readJson(`/${root}/geometry/body_colliders.json`, { colliders: [] }),
    readJson(`/${root}/geometry/sockets.json`, { sockets: [] }),
    readJson(`/${root}/geometry/weapon_attachments.json`, { attachments: [] }),
    readJson(`/${root}/geometry/traces.json`, { traces: [] }),
    readJson(`/${root}/config/character_application.json`, {}),
    readJson(`/${root}/validation/last_report.json`, { issues: [] })
  ]);
  const pkg = {
    manifest,
    resourceCatalog,
    geometry: {
      schemaVersion: bodyParts.schemaVersion || colliders.schemaVersion || "1.0",
      bodyProfile,
      bodyParts: bodyParts.bodyParts || [],
      colliders: colliders.colliders || [],
      sockets: sockets.sockets || [],
      weaponAttachments: attachments.attachments || [],
      traces: traces.traces || []
    },
    applicationConfig: appConfig,
    validationReport: validation
  };
  ensureAnimationAuthoringConfig(pkg);
  return pkg;
}

function render() {
  renderShellStatus();
  renderLayoutState();
  renderSummary();
  renderTree();
  renderLoadouts();
  renderPreviewControls();
  renderResourceBindingBar();
  renderResourcePicker();
  renderAnimationConfigPanel();
  renderResourcePlanPreview();
  renderViewport();
  renderInspector();
  renderDiagnostics();
  renderImportStatus();
}

function renderLayoutState() {
  if (!el.workspace || !el.treeCollapseButton) return;
  el.workspace.classList.toggle("tree-collapsed", state.treeCollapsed);
  el.treeCollapseButton.textContent = state.treeCollapsed ? "›" : "‹";
  el.treeCollapseButton.title = state.treeCollapsed ? "展开资源包栏" : "折叠资源包栏";
  el.treeCollapseButton.setAttribute("aria-label", el.treeCollapseButton.title);
}

function renderShellStatus() {
  el.subtitle.textContent = state.message || "角色资源包外部装配工作台";
  el.dirtyBadge.textContent = state.dirty ? "dirty" : "clean";
  el.dirtyBadge.className = `badge ${state.dirty ? "warn" : "ok"}`;
  el.saveButton.disabled = !state.canWrite || !state.package;
  el.modelImportButton.disabled = !state.canWrite || !state.package;
  el.modelImportRole.disabled = !state.canWrite || !state.package;
  el.configCreateSelect.disabled = !state.canWrite || !state.package;
  el.configCreateButton.disabled = !state.canWrite || !state.package;
  el.openResourcePickerButton.disabled = !state.package || (el.modelImportRole?.value || "preview") === "preview";
  el.clearModelBindingButton.disabled = !state.canWrite || !state.package || el.modelImportRole.value === "preview";
  el.compileButton.disabled = !state.apiAvailable || !state.package;
  el.importButton.disabled = !state.apiAvailable || !state.package || state.dirty || isImportBlocked();
  updateModelImportTitle();
}

function getModelImportRole() {
  return MODEL_IMPORT_ROLES[el.modelImportRole?.value] || MODEL_IMPORT_ROLES.preview;
}

function updateModelImportTitle() {
  if (!el.modelImportButton || !el.modelImportRole) return;
  el.modelImportButton.title = `源资源导入：${getModelImportRole().title}。支持 GLB/GLTF；FBX 会先转换为 GLB。`;
}

function normalizeResourceLibraryPayload(payload) {
  if (!payload) return null;
  if (Array.isArray(payload)) return { items: payload };
  if (Array.isArray(payload.items)) return payload;
  if (Array.isArray(payload.entries)) return { items: payload.entries };
  return null;
}

function normalizeResourcePlanPayload(payload) {
  if (!payload) return null;
  if (payload.characterResourcePlan) return payload.characterResourcePlan;
  if (payload.CharacterResourcePlan) return payload.CharacterResourcePlan;
  if (payload.characterStableId || payload.spawnCritical || payload.groups) return payload;
  return null;
}

function ensureAnimationAuthoringConfig(pkg) {
  if (!pkg) return null;
  const appConfig = pkg.applicationConfig || {};
  pkg.applicationConfig = appConfig;
  if (!Array.isArray(appConfig.resourceKeys)) appConfig.resourceKeys = [];
  if (!Array.isArray(appConfig.animationProfiles)) appConfig.animationProfiles = [];

  let profile = appConfig.animationProfiles.find(item => item?.profileId === DEFAULT_ANIMATION_PROFILE_ID)
    || appConfig.animationProfiles[0];
  if (!profile) {
    profile = {
      profileId: DEFAULT_ANIMATION_PROFILE_ID,
      displayName: "默认动画 Profile",
      description: "CharacterStudio 编辑期动画资源选择；最终动画归属由后续 Animation / Equipment authoring 决定。",
      slots: []
    };
    appConfig.animationProfiles.push(profile);
  }
  if (!Array.isArray(profile.slots)) profile.slots = [];

  for (const definition of ANIMATION_PROFILE_SLOTS) {
    let slot = profile.slots.find(item => item?.slotId === definition.slotId);
    if (!slot) {
      slot = createDefaultAnimationSlot(pkg, definition);
      profile.slots.push(slot);
    } else {
      normalizeAnimationSlot(slot, pkg, definition);
    }
  }
  profile.slots.sort((a, b) => {
    const ai = ANIMATION_PROFILE_SLOTS.findIndex(item => item.slotId === a.slotId);
    const bi = ANIMATION_PROFILE_SLOTS.findIndex(item => item.slotId === b.slotId);
    return (ai < 0 ? 99 : ai) - (bi < 0 ? 99 : bi);
  });
  return appConfig;
}

function createDefaultAnimationSlot(pkg, definition) {
  const resource = findDefaultAnimationResource(pkg, definition.resourceHint);
  const resourceKey = resource?.resourceKey || "";
  return {
    slotId: definition.slotId,
    displayName: definition.displayName,
    purpose: definition.purpose,
    resourceKey,
    preloadPolicy: "AnimationWarmup",
    required: Boolean(definition.required),
    resourceSelection: resource ? createPackageAnimationSelectionRef(resource) : {}
  };
}

function normalizeAnimationSlot(slot, pkg, definition = {}) {
  slot.slotId ||= definition.slotId || "";
  slot.displayName ||= definition.displayName || slot.slotId || "动画槽位";
  slot.purpose ||= definition.purpose || "";
  slot.resourceKey ||= "";
  slot.preloadPolicy ||= "AnimationWarmup";
  slot.required = Boolean(slot.required || definition.required);
  if (!slot.resourceSelection || typeof slot.resourceSelection !== "object") {
    const resource = findPackageResourceByKey(pkg, slot.resourceKey);
    slot.resourceSelection = resource ? createPackageAnimationSelectionRef(resource) : {};
  }
  if (!slot.resourceKey) {
    const selected = slot.resourceSelection || {};
    slot.resourceKey = selected.runtimeResourceKey || selected.packageResourceKey || "";
  }
  return slot;
}

function createPackageAnimationSelectionRef(resource) {
  return {
    resourceStableId: resource.stableId || "",
    sourceProviderId: "characterPackage",
    bindingKind: "PackageResource",
    expectedKind: "Animation",
    expectedUsage: resource.usage || "animationClipGroup",
    expectedHash: resource.hash || resource.hashes?.contentHash || "",
    runtimeResourceKey: "",
    providerResourceKey: resource.resourceKey || "",
    packageResourceKey: resource.resourceKey || "",
    unityGuid: "",
    unityAssetPath: "",
    audioCueId: "",
    audioEventDefinitionId: ""
  };
}

function findDefaultAnimationResource(pkg, hint) {
  const resources = (pkg?.resourceCatalog?.entries || []).filter(resource => mapResourceKind(resource.typeId) === "Animation");
  if (!resources.length) return null;
  if (hint) {
    const normalized = String(hint).toLowerCase();
    const matched = resources.find(resource => [
      resource.localId,
      resource.resourceKey,
      resource.stableId,
      resource.relativePath,
      ...(resource.tags || [])
    ].filter(Boolean).some(value => String(value).toLowerCase().includes(normalized)));
    if (matched) return matched;
  }
  return resources[0];
}

function findPackageResourceByKey(pkg, key) {
  if (!pkg || !key) return null;
  return (pkg.resourceCatalog?.entries || []).find(resource => resource?.resourceKey === key) || null;
}

function getDefaultAnimationProfile() {
  ensureAnimationAuthoringConfig(state.package);
  const profiles = state.package?.applicationConfig?.animationProfiles || [];
  return profiles.find(profile => profile?.profileId === DEFAULT_ANIMATION_PROFILE_ID) || profiles[0] || null;
}

function findAnimationSlot(profileId, slotId) {
  ensureAnimationAuthoringConfig(state.package);
  const profiles = state.package?.applicationConfig?.animationProfiles || [];
  const profile = profiles.find(item => item?.profileId === profileId) || profiles[0];
  const slot = profile?.slots?.find(item => item?.slotId === slotId) || null;
  return { profile, slot };
}

function getAnimationSlotPath(profileId, slotId) {
  return `config/animation/${encodeURIComponent(profileId || DEFAULT_ANIMATION_PROFILE_ID)}/slots/${encodeURIComponent(slotId || "")}`;
}

function renderAnimationConfigPanel() {
  if (!el.animationConfigPanel) return;
  const profile = getDefaultAnimationProfile();
  if (!profile) {
    el.animationConfigPanel.innerHTML = `<div class="empty">暂无动画 Profile。</div>`;
    return;
  }

  const animationResources = getResourceLibraryItems().filter(item => item.kind === "Animation");
  const runtimeReadyCount = animationResources.filter(item => item.runtimeAvailability === "RuntimeReady" || item.bindingKind === "PackageResource" || item.bindingKind === "ResourceManagerAsset").length;
  const slots = profile.slots || [];
  el.animationConfigPanel.innerHTML = `
    <div class="animation-profile-summary">
      <div><strong>${escapeHtml(profile.displayName || profile.profileId)}</strong><span>${escapeHtml(profile.profileId || DEFAULT_ANIMATION_PROFILE_ID)}</span></div>
      <div><strong>${escapeHtml(String(animationResources.length))}</strong><span>可见动画资源</span></div>
      <div><strong>${escapeHtml(String(runtimeReadyCount))}</strong><span>可编排资源</span></div>
    </div>
    <div class="animation-slot-list">
      ${slots.map(slot => renderAnimationSlotCard(profile, slot)).join("")}
    </div>`;
}

function renderAnimationSlotCard(profile, slot) {
  const resource = findAnimationSlotResource(slot);
  const selection = slot.resourceSelection || {};
  const selectedText = resource
    ? getResourceDisplayName(resource.resource || resource)
    : firstNonEmpty(slot.resourceKey, selection.runtimeResourceKey, selection.packageResourceKey, selection.providerResourceKey, selection.unityAssetPath, "未选择");
  const availability = resource?.runtimeAvailability || (selection.bindingKind === "UnityEditorOnlyAsset" ? "EditorOnly" : "");
  const keyText = firstNonEmpty(slot.resourceKey, selection.runtimeResourceKey, selection.packageResourceKey, selection.providerResourceKey, selection.unityAssetPath, "-");
  const path = getAnimationSlotPath(profile.profileId, slot.slotId);
  return `<article class="animation-slot-card">
    <div class="animation-slot-main">
      <div>
        <strong>${escapeHtml(slot.displayName || slot.slotId)}</strong>
        <span>${escapeHtml(slot.purpose || "动画资源槽位")}</span>
      </div>
      <button type="button" data-animation-jump="1" data-path="${escapeHtml(path)}" title="在属性栏查看该槽位">查看</button>
    </div>
    <div class="animation-slot-resource">
      <span>${escapeHtml(selectedText)}</span>
      ${availability ? `<span class="sync-badge ${escapeHtml(getResourceAvailabilityTone(availability))}">${escapeHtml(availability)}</span>` : ""}
    </div>
    <code title="${escapeHtml(keyText)}">${escapeHtml(keyText)}</code>
    <div class="animation-slot-actions">
      <button type="button" data-animation-pick="1" data-profile-id="${escapeHtml(profile.profileId)}" data-slot-id="${escapeHtml(slot.slotId)}">选择动画资源</button>
      <button type="button" data-animation-clear="1" data-profile-id="${escapeHtml(profile.profileId)}" data-slot-id="${escapeHtml(slot.slotId)}">清空</button>
    </div>
  </article>`;
}

function getResourceAvailabilityTone(value) {
  const normalized = String(value || "").toLowerCase();
  if (normalized.includes("ready") || normalized.includes("package")) return "ok";
  if (normalized.includes("editor") || normalized.includes("missing")) return "warn";
  return "muted";
}

function firstNonEmpty(...values) {
  for (const value of values) {
    if (value == null) continue;
    const text = String(value).trim();
    if (text) return text;
  }
  return "";
}

function findAnimationSlotResource(slot) {
  if (!slot) return null;
  const values = [
    slot.resourceKey,
    slot.resourceSelection?.runtimeResourceKey,
    slot.resourceSelection?.packageResourceKey,
    slot.resourceSelection?.providerResourceKey,
    slot.resourceSelection?.resourceStableId,
    slot.resourceSelection?.unityAssetPath
  ].filter(Boolean).map(String);
  if (!values.length) return null;
  return getResourceLibraryItems().find(item => values.some(value => getResourceIdentityValues(item).includes(value))) || null;
}

function renderResourceBindingBar() {
  if (!el.resourceBindingTarget) return;
  const targetRole = el.modelImportRole?.value || "preview";
  const roleInfo = getModelImportRole();
  const fieldSpec = getActiveResourceFieldSpec();
  const selectedResource = getSelectedModelResource();
  const selectedText = selectedResource
    ? `当前选择：${getResourceDisplayName(selectedResource)}`
    : "未选择模型资源";
  el.resourceBindingTarget.textContent = targetRole === "preview"
    ? "选择角色主体或武器字段后再打开资源选择器"
    : `${fieldSpec.fieldKey} / ${roleInfo.label}：${selectedText}`;
}

async function openResourcePicker() {
  if (!state.package) return;
  const role = el.modelImportRole?.value || "preview";
  if (role === "preview") {
    state.message = "请先选择角色主体模型、主手武器或副手武器字段。";
    renderShellStatus();
    return;
  }
  state.resourcePickerField = null;
  state.resourcePickerOpen = true;
  state.resourcePickerQuery = null;
  await loadResourcePickerQuery(getActiveResourceFieldSpec());
}

async function openResourcePickerForField(fieldPath) {
  if (!state.package || !fieldPath) return;
  const target = findTarget(state.selectedPath);
  if (!target?.value) return;
  const fieldSpec = getResourceFieldSpecForInspectorField(target, fieldPath);
  if (!fieldSpec) return;
  state.resourcePickerField = {
    kind: target.kind === "animationSlot" ? "animationSlot" : "",
    profileId: target.profileId || "",
    slotId: target.value?.slotId || "",
    targetPath: state.selectedPath,
    fieldPath,
    fieldSpec,
    title: `${target.label || target.kind}.${fieldPath}`
  };
  state.resourcePickerOpen = true;
  state.resourcePickerQuery = null;
  await loadResourcePickerQuery(fieldSpec);
}

async function openAnimationSlotPicker(profileId, slotId) {
  if (!state.package) return;
  const { profile, slot } = findAnimationSlot(profileId, slotId);
  if (!profile || !slot) return;
  const definition = ANIMATION_PROFILE_SLOTS.find(item => item.slotId === slot.slotId) || {};
  const fieldSpec = {
    ...RESOURCE_FIELD_SPECS.animationClip,
    fieldKey: `Animation.Profile.${slot.slotId}`,
    displayName: slot.displayName || definition.displayName || "动画资源",
    compatibilityFilter: { ...(RESOURCE_FIELD_SPECS.animationClip.compatibilityFilter || {}) }
  };
  state.resourcePickerField = {
    kind: "animationSlot",
    profileId: profile.profileId,
    slotId: slot.slotId,
    targetPath: getAnimationSlotPath(profile.profileId, slot.slotId),
    fieldPath: "resourceKey",
    fieldSpec,
    title: `${profile.displayName || profile.profileId}.${slot.displayName || slot.slotId}`
  };
  state.resourcePickerOpen = true;
  state.resourcePickerQuery = null;
  state.selectedPath = getAnimationSlotPath(profile.profileId, slot.slotId);
  renderTree();
  renderInspector();
  await loadResourcePickerQuery(fieldSpec);
}

function clearAnimationSlotSelection(profileId, slotId) {
  const { slot } = findAnimationSlot(profileId, slotId);
  if (!slot) return;
  slot.resourceKey = "";
  slot.resourceSelection = {};
  state.selectedPath = getAnimationSlotPath(profileId, slotId);
  state.dirty = true;
  state.message = `${slot.displayName || slot.slotId} 已清空引用；资源本体不会被删除。`;
  render();
}

function closeResourcePicker() {
  state.resourcePickerOpen = false;
  state.resourcePickerField = null;
  state.resourcePickerQuery = null;
  state.resourcePickerLoading = false;
  renderResourcePicker();
}

async function loadResourcePickerQuery(fieldSpec) {
  state.resourcePickerLoading = true;
  renderResourcePicker();
  const result = await postJson("/api/authoring/resources/pick", {
    package: state.packageRelative,
    fieldSpec: toAuthoringResourceFieldSpec(fieldSpec),
    context: buildResourceConsumerContext(fieldSpec),
    selection: {}
  }, null);
  state.resourcePickerQuery = result;
  state.resourcePickerLoading = false;
  renderResourcePicker();
}

function renderResourcePicker() {
  if (!el.resourcePickerOverlay || !el.resourcePickerList) return;
  el.resourcePickerOverlay.hidden = !state.resourcePickerOpen;
  if (!state.resourcePickerOpen) {
    el.resourcePickerList.innerHTML = "";
    return;
  }

  const request = getActiveResourcePickerRequest();
  const fieldSpec = request.fieldSpec;
  el.resourcePickerTitle.textContent = `选择${request.title}`;
  el.resourcePickerSummary.textContent = `${fieldSpec.fieldKey} / ${fieldSpec.outputKind} / ${fieldSpec.preloadPolicy}。资源来自 Authoring Resource Manager，当前角色只保存引用。`;
  if (state.resourcePickerLoading) {
    el.resourcePickerList.innerHTML = `<div class="empty">正在读取资源管理器候选项...</div>`;
    return;
  }

  const rows = getResourcePickerRows(fieldSpec);
  if (!rows.length) {
    el.resourcePickerList.innerHTML = `<div class="empty">没有符合当前字段契约的资源；请先在资源管理器导入或同步资源。</div>`;
    return;
  }

  el.resourcePickerList.innerHTML = rows.map(row => {
    const item = row.item;
    const selection = row.selection;
    const selected = item.path === state.selectedPath || getResourceIdentityValues(item).includes(getCurrentResourceFieldValue());
    const thumb = item.thumbnailUrl
      ? `<img src="${escapeHtml(item.thumbnailUrl)}" alt="${escapeHtml(item.displayName)}">`
      : `<span>${escapeHtml(getResourceInitial(item))}</span>`;
    const diagnostics = item.diagnostics?.length
      ? `${item.diagnostics.length} diagnostics`
      : "0 diagnostics";
    return `
      <button type="button" class="resource-card ${selected ? "active" : ""} ${escapeHtml(selection.tone)}" data-library-id="${escapeHtml(item.resourceId || item.libraryItemId || "")}" title="${escapeHtml(item.sourceName || item.stableId || item.displayName)}">
        <span class="resource-thumb">${thumb}</span>
        <span class="resource-info">
          <span class="resource-title"><strong>${escapeHtml(item.displayName)}</strong>${renderSyncBadge({ label: item.importStatusLabel, tone: item.importTone })}</span>
          <span>${escapeHtml(item.kindLabel)} / ${escapeHtml(item.usage || "usage?")} / ${escapeHtml(item.runtimeAvailability)}</span>
          <span>${escapeHtml(item.bindingKind)} / ${escapeHtml(item.sourceProviderId)} / refs ${escapeHtml(String(item.referenceCount || 0))} / ${escapeHtml(diagnostics)}</span>
          <span>${renderSelectionBadge(selection)} ${escapeHtml(selection.reason)}</span>
          <span class="resource-unity-path">${escapeHtml(item.unityAssetPath || item.sourceName || item.stableId || "未绑定 Unity 资产")}</span>
        </span>
      </button>`;
  }).join("");
}

function getActiveResourcePickerRequest() {
  if (state.resourcePickerField?.fieldSpec) {
    return {
      fieldSpec: state.resourcePickerField.fieldSpec,
      title: state.resourcePickerField.title || state.resourcePickerField.fieldSpec.displayName || "资源"
    };
  }
  const roleInfo = getModelImportRole();
  return { fieldSpec: getActiveResourceFieldSpec(), title: roleInfo.label };
}

function getActiveResourceFieldSpec() {
  const role = el.modelImportRole?.value || "preview";
  return RESOURCE_FIELD_SPECS[role] || RESOURCE_FIELD_SPECS.preview;
}

function getResourceFieldSpecForInspectorField(target, fieldPath) {
  if (target.kind === "animationSlot" && fieldPath === "resourceKey") {
    return {
      ...RESOURCE_FIELD_SPECS.animationClip,
      fieldKey: `Animation.Profile.${target.value?.slotId || "slot"}`,
      displayName: target.value?.displayName || "动画资源"
    };
  }
  if (target.kind === "weapon" && fieldPath === "previewResourceKey") {
    const slot = target.value?.equipSlot || "mainHand";
    const base = RESOURCE_FIELD_SPECS[slot] || RESOURCE_FIELD_SPECS.mainHand;
    return {
      ...base,
      fieldKey: `WeaponAttachment.${slot}.previewResourceKey`,
      displayName: "武器模型引用",
      compatibilityFilter: { ...(base.compatibilityFilter || {}), slotId: slot }
    };
  }
  return null;
}

function getResourcePickerRows(fieldSpec) {
  const queryItems = Array.isArray(state.resourcePickerQuery?.items) ? state.resourcePickerQuery.items : [];
  if (queryItems.length) {
    return queryItems
      .map(row => ({ item: normalizeResourceLibraryItem(row.item), selection: evaluateServerPickerItem(row, fieldSpec) }))
      .filter(row => row.item)
      .sort((a, b) => getResourceLibrarySortKey(a.item).localeCompare(getResourceLibrarySortKey(b.item)));
  }
  return getResourceLibraryItems().map(item => ({ item, selection: evaluateResourceFieldSelection(item, fieldSpec) }));
}

function evaluateServerPickerItem(row, spec) {
  const reasons = Array.isArray(row.reasons) ? row.reasons : [];
  const reason = reasons.map(item => item.code || item.message).filter(Boolean).join(" / ");
  if (row.selectable && row.hasWarnings) return { tone: "warn", label: "可选", reason: reason || `${spec.outputKind} / ${spec.preloadPolicy}` };
  if (row.selectable) return { tone: "match", label: "可选", reason: reason || `${spec.outputKind} / ${spec.preloadPolicy}` };
  return { tone: "blocked", label: "不可选", reason: reason || "不符合字段契约" };
}

function getCurrentResourceFieldValue() {
  if (!state.resourcePickerField) return "";
  if (state.resourcePickerField.kind === "animationSlot") {
    const { slot } = findAnimationSlot(state.resourcePickerField.profileId, state.resourcePickerField.slotId);
    return firstNonEmpty(
      slot?.resourceKey,
      slot?.resourceSelection?.runtimeResourceKey,
      slot?.resourceSelection?.packageResourceKey,
      slot?.resourceSelection?.providerResourceKey,
      slot?.resourceSelection?.resourceStableId,
      slot?.resourceSelection?.unityAssetPath);
  }
  const target = findTarget(state.resourcePickerField.targetPath);
  return getNested(target.value, state.resourcePickerField.fieldPath) || "";
}

function getResourceLibraryItems() {
  const apiItems = state.resourceLibrary?.items;
  const rawItems = Array.isArray(apiItems) && apiItems.length > 0
    ? apiItems
    : (state.package?.resourceCatalog?.entries || []);
  return rawItems
    .map(item => normalizeResourceLibraryItem(item))
    .filter(Boolean)
    .sort((a, b) => getResourceLibrarySortKey(a).localeCompare(getResourceLibrarySortKey(b)));
}

function normalizeResourceLibraryItem(item) {
  if (!item) return null;
  const resource = item.resource || item;
  const providerBindings = Array.isArray(item.providerBindings) ? item.providerBindings : [];
  const primaryBinding = getPrimaryResourceBinding(providerBindings);
  const sync = getResourceUnitySync(resource);
  const kind = normalizeResourceKind(item.kind || resource.kind || mapResourceKind(resource.typeId));
  const diagnostics = [
    ...(Array.isArray(item.diagnostics) ? item.diagnostics.map(formatDiagnosticText) : []),
    ...sync.diagnostics
  ].filter(Boolean);
  const referenceCount = item.referenceCount ?? collectResourceReferences(resource).length;
  const bindingKind = normalizeResourceBindingKind(item.bindingKind || item.runtimeBindingKind || primaryBinding?.bindingKind || inferRuntimeBindingKind(resource, kind));
  const runtimeBindingKind = bindingKind;
  const runtimeAvailability = item.runtimeAvailability || inferRuntimeAvailability(resource, runtimeBindingKind, sync);
  const resourceId = item.resourceId || item.libraryItemId || resource.libraryItemId || resource.stableId || resource.resourceKey || resource.localId || resource.relativePath;
  const sourceProviderId = item.sourceProviderId || inferSourceProviderId(item, resource);
  const runtimeResourceKey = item.runtimeResourceKey || firstBindingValue(providerBindings, "runtimeResourceKey") || (runtimeBindingKind === "ResourceManagerAsset" ? resource.resourceKey || "" : "");
  const providerResourceKey = item.providerResourceKey || firstBindingValue(providerBindings, "providerResourceKey") || firstBindingValue(providerBindings, "packageResourceKey") || resource.resourceKey || "";
  const packageResourceKey = item.packageResourceKey || firstBindingValue(providerBindings, "packageResourceKey") || (sourceProviderId === "characterPackage" ? resource.resourceKey || "" : "");
  const unityGuid = item.unityGuid || firstBindingValue(providerBindings, "unityGuid") || sync.unityGuid || "";
  const unityAssetPath = item.unityAssetPath || firstBindingValue(providerBindings, "unityAssetPath") || sync.unityAssetPath || "";
  const importStatus = item.importStatus || sync.status;
  const importTone = getResourceImportTone(importStatus, sync.tone);
  return {
    raw: item,
    resource,
    resourceId,
    libraryItemId: item.libraryItemId || resourceId,
    stableId: item.stableId || resource.stableId || resourceId,
    sourceProviderId,
    providerBindings,
    path: resource.resourceKey ? `resources/${resource.resourceKey}` : "",
    resourceKey: resource.resourceKey || item.resourceKey || "",
    runtimeResourceKey,
    providerResourceKey,
    packageResourceKey,
    unityGuid,
    displayName: item.displayName || getResourceDisplayName(resource),
    kind,
    kindLabel: RESOURCE_KIND_LABELS[kind] || kind,
    usage: item.usage || resource.usage || "",
    sourceKind: item.sourceKind || inferSourceKind(resource),
    bindingKind,
    runtimeBindingKind,
    runtimeAvailability,
    importStatus,
    importStatusLabel: item.importStatusLabel || importStatus || sync.label,
    importTone,
    referenceCount,
    diagnostics,
    thumbnailUrl: item.thumbnailUrl || getResourceThumbnailUrl(resource, state.package),
    sourceName: resource.provenance?.sourceFile || resource.relativePath || resource.localId || resource.resourceKey || "",
    unityAssetPath
  };
}

function getPrimaryResourceBinding(bindings) {
  return bindings.find(binding => binding?.isPrimary) || bindings[0] || null;
}

function firstBindingValue(bindings, key) {
  const found = bindings.find(binding => binding && binding[key]);
  return found ? found[key] || "" : "";
}

function normalizeResourceKind(kind) {
  const normalized = String(kind || "").toLowerCase();
  if (normalized === "model") return "Model";
  if (normalized === "animation" || normalized === "animationclipgroup") return "Animation";
  if (normalized === "texture" || normalized === "preview") return "Texture";
  if (normalized === "material") return "Material";
  if (normalized === "avatarmask") return "AvatarMask";
  if (normalized === "vfx") return "Vfx";
  if (normalized === "audio") return "Audio";
  if (normalized === "config") return "Config";
  if (normalized === "generated") return "Generated";
  return kind || "Generated";
}

function normalizeResourceBindingKind(kind) {
  const value = String(kind || "");
  return value || "None";
}

function getResourceImportTone(status, fallbackTone = "") {
  const normalized = String(status || "").toLowerCase();
  if (["clean", "ready", "imported", "runtimeReady".toLowerCase()].includes(normalized)) return "ok";
  return fallbackTone || getUnityStatusTone(status);
}

function inferSourceProviderId(item, resource) {
  if (item?.sourceProviderId) return item.sourceProviderId;
  const sourceKind = item?.sourceKind || inferSourceKind(resource);
  if (sourceKind === "RuntimeCatalogAsset") return "runtimeCatalog";
  if (sourceKind === "UnityAsset") return "unityAssetDatabase";
  if (sourceKind === "FmodLibrary") return "fmod";
  if (sourceKind === "ExternalFile" || sourceKind === "PackageResource") return "characterPackage";
  return "generatedAssets";
}

function getResourceLibrarySortKey(item) {
  const binding = describeResourceBinding(item.resource, state.package);
  const rank = binding.includes("角色主体") ? "0" : binding.includes("mainHand") ? "1" : binding.includes("offHand") ? "2" : item.kind === "Model" ? "3" : "4";
  return `${rank}:${item.kind}:${item.displayName}`;
}

function mapResourceKind(typeId) {
  const normalized = String(typeId || "").toLowerCase();
  if (normalized === "model" || normalized === "mesh" || normalized === "prefab") return "Model";
  if (normalized === "animation" || normalized === "anim" || normalized === "clip") return "Animation";
  if (normalized === "texture" || normalized === "preview" || normalized === "sprite" || normalized === "image") return "Texture";
  if (normalized === "material") return "Material";
  if (normalized === "vfx" || normalized === "effect") return "Vfx";
  if (normalized === "audio" || normalized === "audioclip" || normalized === "fmod") return "Audio";
  if (normalized === "config" || normalized.endsWith("config")) return "Config";
  return "Generated";
}

function inferSourceKind(resource) {
  const sourceTool = String(resource?.provenance?.sourceTool || "").toLowerCase();
  if (sourceTool.includes("fmod")) return "FmodLibrary";
  if (resource?.importHints?.providerId === "unityAsset") return "UnityAsset";
  if (resource?.relativePath) return "ExternalFile";
  return "GeneratedAsset";
}

function inferRuntimeBindingKind(resource, kind) {
  const metadata = resource?.importHints?.metadata || {};
  if (kind === "Audio" && (metadata.audioCueId || metadata.fmodEventPath)) return "AudioCue";
  if (kind === "Audio") return "AudioEventDefinition";
  if (kind === "Texture" && resource?.usage === "previewThumbnail") return "GeneratedPreviewOnly";
  if (resource?.resourceKey && resource?.importHints?.providerId !== "editorOnly") return "ResourceManagerAsset";
  if (resource?.relativePath) return "UnityEditorOnlyAsset";
  return "None";
}

function inferRuntimeAvailability(resource, bindingKind, sync) {
  if (bindingKind === "AudioCue" || bindingKind === "AudioEventDefinition") return "AudioCueOnly";
  if (bindingKind === "GeneratedPreviewOnly") return "PreviewOnly";
  if (bindingKind === "UnityEditorOnlyAsset") return "EditorOnly";
  if (bindingKind !== "ResourceManagerAsset") return "Unknown";
  if (!resource?.resourceKey && !resource?.runtimeResourceKey) return "NotRuntimeLoadable";
  const tone = sync?.tone || "";
  if (tone === "error") return "RuntimeMissing";
  if (tone === "warn") return "RuntimeMissing";
  return "RuntimeReady";
}

function evaluateResourceFieldSelection(item, spec) {
  const reasons = [];
  let selectable = true;
  let warn = false;
  if (spec.acceptedKinds?.length && !containsIgnoreCase(spec.acceptedKinds, item.kind)) {
    selectable = false;
    reasons.push(`kind 不匹配：${item.kind}`);
  }
  if (spec.acceptedUsages?.length && !containsIgnoreCase(spec.acceptedUsages, item.usage)) {
    selectable = false;
    reasons.push(`usage 不匹配：${item.usage || "空"}`);
  }
  if (spec.acceptedProviderIds?.length && !spec.acceptedProviderIds.includes(item.sourceProviderId)) {
    selectable = false;
    reasons.push(`provider 不匹配：${item.sourceProviderId || "空"}`);
  }
  if (spec.acceptedBindingKinds?.length && !hasAcceptedBinding(item, spec.acceptedBindingKinds)) {
    selectable = false;
    reasons.push(`binding 不匹配：${item.bindingKind}`);
  }
  if (spec.requireRuntimeLoadable && item.runtimeAvailability !== "RuntimeReady") {
    warn = spec.allowIncompatibleWithWarning;
    selectable = selectable && warn;
    reasons.push(`runtime=${item.runtimeAvailability}`);
  }
  if (spec.requireUnityImported && item.importTone !== "ok") {
    warn = spec.allowIncompatibleWithWarning;
    selectable = selectable && warn;
    reasons.push(`import=${item.importStatusLabel}`);
  }
  if (item.diagnostics.some(text => /failed|失败|conflict|冲突|missing|缺失/i.test(text))) {
    warn = true;
  }
  if (selectable && !warn) return { tone: "match", label: "可选", reason: `${spec.outputKind} / ${spec.preloadPolicy}` };
  if (selectable && warn) return { tone: "warn", label: "可选", reason: reasons.join("；") || "有兼容性警告" };
  return { tone: "blocked", label: "不可选", reason: reasons.join("；") || "不符合字段契约" };
}

function containsIgnoreCase(values, value) {
  return values.some(item => String(item).toLowerCase() === String(value || "").toLowerCase());
}

function hasAcceptedBinding(item, acceptedKinds) {
  if (containsIgnoreCase(acceptedKinds, item.bindingKind)) return true;
  return (item.providerBindings || []).some(binding => containsIgnoreCase(acceptedKinds, binding?.bindingKind));
}

function renderSelectionBadge(selection) {
  const tone = selection.tone === "match" ? "ok" : selection.tone === "warn" ? "warn" : "error";
  return `<span class="selection-badge ${tone}">${escapeHtml(selection.label)}</span>`;
}

async function selectLibraryItem(libraryItemId) {
  const item = getResourceLibraryItems().find(candidate => candidate.resourceId === libraryItemId || candidate.libraryItemId === libraryItemId);
  if (!item) return;
  const request = getActiveResourcePickerRequest();
  const spec = request.fieldSpec;
  const localSelection = evaluateResourceFieldSelection(item, spec);
  let selectionRef = createResourceSelectionRef(item, spec);
  const resolved = await resolveResourceSelection(selectionRef, spec);
  if (resolved?.selection) selectionRef = normalizeResolvedSelectionRef(resolved.selection, selectionRef);
  const blocked = resolved ? resolved.accepted === false : localSelection.tone === "blocked";
  const reason = resolved ? formatSelectionReasons(resolved.reasons) : localSelection.reason;
  if (blocked) {
    state.message = `${item.displayName} 不能用于 ${spec.fieldKey}：${reason}`;
    renderResourcePicker();
    renderShellStatus();
    return;
  }
  if (state.resourcePickerField) {
    applyResourceSelectionToField(item, selectionRef);
    return;
  }
  const resolvedModelKey = selectionRef.runtimeResourceKey || selectionRef.packageResourceKey || selectionRef.providerResourceKey || item.resourceKey || "";
  if (item.kind === "Model" && resolvedModelKey) {
    bindModelResource(resolvedModelKey);
    return;
  }
  state.selectedPath = item.path || state.selectedPath;
  state.message = `${spec.fieldKey} 已选择 ${item.displayName}；ResourceSelectionRef=${selectionRef.resourceStableId}`;
  closeResourcePicker();
  renderTree();
  renderResourceBindingBar();
  renderInspector();
  renderShellStatus();
}

function applyResourceSelectionToField(item, selectionRef) {
  const picker = state.resourcePickerField;
  if (picker?.kind === "animationSlot") {
    applyResourceSelectionToAnimationSlot(item, selectionRef, picker);
    return;
  }
  const target = picker ? findTarget(picker.targetPath) : null;
  if (!target?.value) return;
  const value = selectionRef.runtimeResourceKey || selectionRef.packageResourceKey || selectionRef.providerResourceKey || item.resourceKey || "";
  setNested(target.value, picker.fieldPath, value);
  if (picker.fieldPath === "previewResourceKey") {
    setNested(target.value, "previewResourceSelection", selectionRef);
  }
  state.selectedPath = picker.targetPath;
  state.dirty = true;
  state.message = `${picker.title} 已引用 ${item.displayName}；资源本体仍归资源管理器。`;
  closeResourcePicker();
  render();
}

function applyResourceSelectionToAnimationSlot(item, selectionRef, picker) {
  const { profile, slot } = findAnimationSlot(picker.profileId, picker.slotId);
  if (!profile || !slot) return;
  const value = selectionRef.runtimeResourceKey || selectionRef.packageResourceKey || selectionRef.providerResourceKey || item.resourceKey || "";
  slot.resourceKey = selectionRef.runtimeResourceKey || selectionRef.packageResourceKey || item.resourceKey || "";
  slot.resourceSelection = selectionRef;
  slot.preloadPolicy = picker.fieldSpec?.preloadPolicy || "AnimationWarmup";
  if ((selectionRef.runtimeResourceKey || selectionRef.packageResourceKey) && slot.resourceKey) {
    ensureApplicationResourceKey(slot.resourceKey);
  }
  state.selectedPath = getAnimationSlotPath(profile.profileId, slot.slotId);
  state.dirty = true;
  state.message = `${slot.displayName || slot.slotId} 已引用 ${item.displayName}；${value ? "可进入资源计划或等待编译解析。" : "仅保存编辑期选择。"}`;
  closeResourcePicker();
  render();
}

function createResourceSelectionRef(item, spec) {
  const binding = getBindingForFieldSpec(item, spec);
  return {
    resourceStableId: item.stableId || "",
    sourceProviderId: item.sourceProviderId || "",
    bindingKind: binding?.bindingKind || item.bindingKind || "None",
    expectedKind: item.kind || "",
    expectedUsage: item.usage || "",
    expectedHash: binding?.hash || item.raw?.hash || "",
    runtimeResourceKey: binding?.runtimeResourceKey || item.runtimeResourceKey || "",
    providerResourceKey: binding?.providerResourceKey || item.providerResourceKey || "",
    packageResourceKey: binding?.packageResourceKey || item.packageResourceKey || "",
    unityGuid: binding?.unityGuid || item.unityGuid || "",
    unityAssetPath: binding?.unityAssetPath || item.unityAssetPath || "",
    audioCueId: item.raw?.audioCueId || binding?.providerData?.audioCueId || "",
    audioEventDefinitionId: item.raw?.audioEventDefinitionId || binding?.providerData?.audioEventDefinitionId || ""
  };
}

function getBindingForFieldSpec(item, spec) {
  const bindings = item.providerBindings || [];
  const accepted = spec.acceptedBindingKinds || [];
  const outputKind = spec.outputKind || "ResourceSelectionRef";
  const candidates = bindings.filter(binding => !accepted.length || containsIgnoreCase(accepted, binding?.bindingKind));
  const outputMatch = candidates.find(binding => bindingMatchesOutputKind(binding, outputKind));
  return outputMatch || candidates.find(binding => binding?.isPrimary) || candidates[0] || getPrimaryResourceBinding(bindings);
}

function bindingMatchesOutputKind(binding, outputKind) {
  if (!binding) return false;
  if (outputKind === "RuntimeResourceKey") return Boolean(binding.runtimeResourceKey);
  if (outputKind === "ProviderResourceKey") return Boolean(binding.providerResourceKey);
  if (outputKind === "PackageResourceKey") return Boolean(binding.packageResourceKey);
  if (outputKind === "UnityGuid") return Boolean(binding.unityGuid);
  if (outputKind === "UnityAssetPath") return Boolean(binding.unityAssetPath);
  if (outputKind === "AudioCueId") return Boolean(binding.providerData?.audioCueId);
  if (outputKind === "AudioEventDefinitionId") return Boolean(binding.providerData?.audioEventDefinitionId);
  return true;
}

async function resolveResourceSelection(selection, spec) {
  return await postJson("/api/authoring/resources/resolve-selection", {
    package: state.packageRelative,
    fieldSpec: toAuthoringResourceFieldSpec(spec),
    context: buildResourceConsumerContext(spec),
    selection
  }, null);
}

function normalizeResolvedSelectionRef(selection, fallback) {
  return { ...fallback, ...selection };
}

function formatSelectionReasons(reasons) {
  return (reasons || []).map(item => item.code || item.message).filter(Boolean).join(" / ");
}

function toAuthoringResourceFieldSpec(spec) {
  return {
    fieldKey: spec.fieldKey || "",
    editorKind: spec.editorKind || "CharacterStudio",
    displayName: spec.displayName || "",
    acceptedKinds: spec.acceptedKinds || [],
    acceptedUsages: spec.acceptedUsages || [],
    acceptedProviderIds: spec.acceptedProviderIds || [],
    acceptedSourceKinds: spec.acceptedSourceKinds || [],
    acceptedBindingKinds: spec.acceptedBindingKinds || [],
    requireRuntimeLoadable: Boolean(spec.requireRuntimeLoadable),
    requireUnityImported: Boolean(spec.requireUnityImported),
    allowIncompatibleWithWarning: Boolean(spec.allowIncompatibleWithWarning),
    compatibilityFilter: spec.compatibilityFilter || {},
    preloadPolicy: spec.preloadPolicy || "None",
    outputKind: spec.outputKind || "ResourceSelectionRef"
  };
}

function buildResourceConsumerContext(spec) {
  const picker = state.resourcePickerField || {};
  return {
    consumerKind: "CharacterStudio",
    consumerStableId: state.package?.manifest?.stableId || state.package?.manifest?.packageId || "",
    scopeId: state.resourceLibrary?.scopeId || "",
    packageId: state.package?.manifest?.packageId || "",
    packagePath: state.packageRelative,
    slotId: spec.compatibilityFilter?.slotId || "",
    providerFilterIds: spec.acceptedProviderIds || [],
    metadata: {
      selectedPath: state.selectedPath || "",
      pickerKind: picker.kind || "",
      animationProfileId: picker.profileId || "",
      animationSlotId: picker.slotId || ""
    }
  };
}

function getResourceIdentityValues(item) {
  return [
    item.resourceId,
    item.libraryItemId,
    item.stableId,
    item.resourceKey,
    item.runtimeResourceKey,
    item.providerResourceKey,
    item.packageResourceKey,
    item.unityGuid,
    item.unityAssetPath
  ].filter(Boolean).map(String);
}

function findResourceLibraryItemByKey(key) {
  if (!key) return null;
  return getResourceLibraryItems().find(item => getResourceIdentityValues(item).includes(String(key))) || null;
}

function renderResourcePlanPreview() {
  if (!el.resourcePlanPreview) return;
  const plan = getCharacterResourcePlan();
  if (!plan) {
    el.resourcePlanPreview.innerHTML = `<div class="empty">暂无资源计划。运行 Prefab 重建预检后可读取编译结果；当前使用资源库 fallback。</div>`;
    return;
  }
  el.resourcePlanPreview.innerHTML = PLAN_GROUPS.map(group => {
    const groupPlan = getPlanGroup(plan, group.key);
    const entries = normalizePlanEntries(groupPlan);
    const policy = groupPlan?.failurePolicy || group.failurePolicy;
    const required = groupPlan?.required === true ? "required" : "optional";
    const rows = entries.length
      ? entries.slice(0, 5).map(entry => renderPlanEntry(entry)).join("")
      : `<div class="plan-entry empty">无资源</div>`;
    const more = entries.length > 5 ? `<div class="plan-entry meta">+${entries.length - 5} more</div>` : "";
    return `<section class="plan-group">
      <div class="plan-group-head"><strong>${escapeHtml(group.label)}</strong><span>${escapeHtml(required)} / ${escapeHtml(policy)}</span></div>
      ${rows}${more}
    </section>`;
  }).join("");
}

function getCharacterResourcePlan() {
  return state.resourcePlan || buildFallbackResourcePlan();
}

function getPlanGroup(plan, key) {
  if (!plan) return null;
  if (plan[key]) return plan[key];
  const pascal = key.charAt(0).toUpperCase() + key.slice(1);
  if (plan[pascal]) return plan[pascal];
  if (Array.isArray(plan.groups)) {
    return plan.groups.find(group => String(group.key || group.name || "").toLowerCase() === key.toLowerCase()) || null;
  }
  return null;
}

function normalizePlanEntries(groupPlan) {
  if (!groupPlan) return [];
  const entries = [];
  for (const key of ["resources", "resourceKeys", "requiredResources"]) {
    for (const value of groupPlan[key] || []) {
      entries.push(normalizePlanEntry(value, "resource"));
    }
  }
  for (const value of groupPlan.requiredCues || groupPlan.audioCues || []) {
    entries.push(normalizePlanEntry(value, "audioCue"));
  }
  for (const value of groupPlan.requiredBanks || groupPlan.banks || []) {
    entries.push(normalizePlanEntry(value, "bank"));
  }
  for (const value of groupPlan.entries || []) {
    entries.push(normalizePlanEntry(value, value.kind || "resource"));
  }
  return entries.filter(entry => entry.id);
}

function normalizePlanEntry(value, kind) {
  if (typeof value === "object" && value !== null) {
    return {
      id: String(value.resourceKey || value.audioCueId || value.bank || value.id || ""),
      kind: value.kind || kind,
      status: value.status || value.runtimeAvailability || "",
      sizeBytes: value.sizeBytes ?? value.size ?? null,
      policy: value.failurePolicy || value.loadPolicy || ""
    };
  }
  return { id: String(value || ""), kind, status: "", sizeBytes: null, policy: "" };
}

function renderPlanEntry(entry) {
  const item = findResourceLibraryItemByKey(entry.id);
  const status = entry.status || item?.runtimeAvailability || (entry.kind === "resource" ? "PendingCompile" : "External");
  return `<div class="plan-entry">
    <span>${escapeHtml(entry.kind)}</span>
    <code title="${escapeHtml(entry.id)}">${escapeHtml(entry.id)}</code>
    <strong>${escapeHtml(status)}</strong>
  </div>`;
}

function buildFallbackResourcePlan() {
  const pkg = state.package;
  if (!pkg) return null;
  const bodyKeys = getModelResources(pkg).filter(resource => isBodyModelBinding(resource, pkg)).map(resource => resource.resourceKey);
  const equipmentKeys = (pkg.geometry?.weaponAttachments || []).map(attachment => attachment?.previewResourceKey).filter(Boolean);
  const selectedAnimationKeys = collectAnimationSlotResourceKeys(pkg);
  const catalogAnimationKeys = (pkg.resourceCatalog?.entries || []).filter(resource => mapResourceKind(resource.typeId) === "Animation").map(resource => resource.resourceKey).filter(Boolean);
  const animationKeys = Array.from(new Set([...selectedAnimationKeys, ...catalogAnimationKeys]));
  const uiKeys = (pkg.resourceCatalog?.entries || []).filter(resource => resource.usage === "previewThumbnail" || mapResourceKind(resource.typeId) === "Texture").map(resource => resource.resourceKey).filter(Boolean);
  const vfxKeys = (pkg.resourceCatalog?.entries || []).filter(resource => mapResourceKind(resource.typeId) === "Vfx").map(resource => resource.resourceKey).filter(Boolean);
  const audioKeys = (pkg.resourceCatalog?.entries || []).filter(resource => mapResourceKind(resource.typeId) === "Audio").map(resource => resource.resourceKey || resource.stableId).filter(Boolean);
  return {
    characterStableId: pkg.applicationConfig?.characterStableId || pkg.manifest?.packageId || "",
    spawnCritical: { required: true, resources: bodyKeys, failurePolicy: "FailSpawn" },
    equipmentInitial: { required: true, resources: equipmentKeys, failurePolicy: "UseFallbackEquipment" },
    animationWarmup: { required: false, resources: animationKeys, failurePolicy: "UseFallbackPose" },
    vfxWarmup: { required: false, resources: vfxKeys, failurePolicy: "SkipEffect" },
    uiDeferred: { required: false, resources: uiKeys, failurePolicy: "ShowPlaceholder" },
    audio: { required: false, requiredCues: audioKeys, failurePolicy: "MuteMissingCue" }
  };
}

function collectAnimationSlotResourceKeys(pkg) {
  const keys = [];
  for (const profile of pkg?.applicationConfig?.animationProfiles || []) {
    for (const slot of profile?.slots || []) {
      const key = firstNonEmpty(
        slot.resourceKey,
        slot.resourceSelection?.runtimeResourceKey,
        slot.resourceSelection?.packageResourceKey);
      if (key) keys.push(key);
    }
  }
  return keys;
}

function getModelResources(pkg) {
  return (pkg?.resourceCatalog?.entries || [])
    .filter(resource => resource && resource.typeId === "model" && resource.resourceKey)
    .sort((a, b) => getResourceSortKey(a, pkg).localeCompare(getResourceSortKey(b, pkg)));
}

function getResourceSortKey(resource, pkg) {
  const binding = describeResourceBinding(resource, pkg);
  const rank = binding.includes("角色主体") ? "0" : binding.includes("mainHand") ? "1" : binding.includes("offHand") ? "2" : "3";
  return `${rank}:${getResourceDisplayName(resource)}`;
}

function getResourceDisplayName(resource) {
  const source = resource.provenance?.sourceFile || resource.localId || resource.resourceKey || resource.relativePath || "model";
  const name = String(source).split("/").pop().replace(/\.(glb|gltf|fbx)$/i, "");
  return name || "model";
}

function getResourceInitial(resource) {
  const name = resource?.displayName || getResourceDisplayName(resource);
  return name.slice(0, 2).toUpperCase();
}

function describeResourceBinding(resource, pkg) {
  const bindings = [];
  if (isBodyModelBinding(resource, pkg)) bindings.push("角色主体");
  for (const attachment of pkg?.geometry?.weaponAttachments || []) {
    if (attachment.previewResourceKey === resource.resourceKey) {
      bindings.push(`${attachment.equipSlot || "slot"}:${attachment.weaponId || "weapon"}`);
    }
  }
  if (!bindings.length && isApplicationResourceReference(resource.resourceKey, pkg)) {
    bindings.push("角色资源引用");
  }
  return bindings.join(" / ");
}

function getResourceThumbnailUrl(resource, pkg) {
  const previewKey = resource.preview?.thumbnailResourceKey || resource.preview?.placeholderResourceKey || "";
  const preview = (pkg?.resourceCatalog?.entries || []).find(entry => entry.resourceKey === previewKey);
  if (!preview?.relativePath) return "";
  return encodeURI(`/${state.packageRelative}/${preview.relativePath}`);
}

function getUnityCatalogEntries() {
  return Array.isArray(state.unityResourceCatalog?.entries) ? state.unityResourceCatalog.entries : [];
}

function getUnityImportReport() {
  return state.importResult?.report || state.importResult || null;
}

function findUnityResourceEntry(resource) {
  if (!resource) return null;
  const entries = getUnityCatalogEntries();
  const resourceKey = String(resource.resourceKey || "");
  const stableId = String(resource.stableId || "");
  const relativePath = String(resource.relativePath || "");
  return entries.find(entry => {
    const providerData = entry?.providerData || {};
    return String(providerData.packageResourceKey || "") === resourceKey
      || String(entry?.id || "") === resourceKey
      || String(providerData.stableId || "") === stableId
      || String(providerData.sourceRelativePath || "") === relativePath
      || String(entry?.unityAssetPath || "").endsWith(relativePath);
  }) || null;
}

function findImportOperationForResource(resource, unityEntry = null) {
  const operations = getUnityImportReport()?.operations || [];
  if (!resource || operations.length === 0) return null;
  const sourcePath = String(resource.relativePath || "");
  const unityPath = getUnityAssetPath(unityEntry);
  return operations.find(operation => String(operation.sourcePath || "") === sourcePath)
    || operations.find(operation => unityPath && String(operation.targetPath || "") === unityPath)
    || operations.find(operation => sourcePath && String(operation.targetPath || "").endsWith(sourcePath));
}

function getUnityAssetPath(unityEntry) {
  return unityEntry?.unityAssetPath
    || unityEntry?.providerData?.unityAssetPath
    || unityEntry?.providerData?.assetPath
    || unityEntry?.address
    || "";
}

function getResourceUnitySync(resource) {
  const entry = findUnityResourceEntry(resource);
  const operation = findImportOperationForResource(resource, entry);
  const rawStatus = entry?.importStatus
    || entry?.providerData?.importStatus
    || operation?.action
    || (state.unityResourceCatalog ? "MissingFromUnityCatalog" : "NoUnityCatalog");
  const unityAssetGuid = entry?.unityAssetGuid || entry?.providerData?.unityAssetGuid || "";
  const importerKind = entry?.importerKind || entry?.providerData?.importerKind || "";
  const unityAssetPath = getUnityAssetPath(entry);
  const diagnostics = getResourceUnityDiagnostics(resource, entry, operation);
  const status = String(rawStatus || "Unknown");
  return {
    entry,
    operation,
    status,
    label: getUnityStatusLabel(status),
    tone: getUnityStatusTone(status, diagnostics),
    unityAssetPath,
    unityAssetGuid,
    guidShort: unityAssetGuid ? unityAssetGuid.slice(0, 8) : "",
    unityMainObjectType: entry?.unityMainObjectType || entry?.providerData?.unityMainObjectType || "",
    importerKind,
    diagnostics
  };
}

function getResourceUnityDiagnostics(resource, entry, operation) {
  const diagnostics = [];
  const report = getUnityImportReport();
  if (!state.unityResourceCatalog) {
    diagnostics.push(`未读取到 Unity Resource Catalog：${state.unityResourceCatalogPath || "未知路径"}`);
  }
  if (state.unityResourceCatalog && !entry) {
    diagnostics.push("该源资源尚未出现在 Unity Resource Catalog。");
  }
  if (entry && !getUnityAssetPath(entry)) {
    diagnostics.push("Unity 资产路径为空。");
  }
  if (entry && !(entry.unityAssetGuid || entry.providerData?.unityAssetGuid)) {
    diagnostics.push("Unity asset GUID 为空，可能尚未完成 Unity AssetDatabase 导入。");
  }
  if (operation?.message) {
    diagnostics.push(`${operation.action || operation.kind || "Import"}: ${operation.message}`);
  }
  const resourceKey = String(resource?.resourceKey || "");
  for (const issue of report?.issues || []) {
    const source = String(issue.sourcePath || issue.sourceObjectPath || "");
    const message = String(issue.message || "");
    if (source === resource?.relativePath || source === resourceKey || (resourceKey && message.includes(resourceKey))) {
      diagnostics.push(`${issue.code || issue.gate || "Issue"}: ${message}`);
    }
  }
  return diagnostics;
}

function getUnityStatusLabel(status) {
  const normalized = String(status || "").toLowerCase();
  if (normalized === "imported") return "已导入";
  if (normalized === "skipped") return "已跳过";
  if (normalized === "updated") return "已更新";
  if (normalized === "added" || normalized === "created") return "已新增";
  if (normalized.includes("pending")) return "待导入";
  if (normalized.includes("sourcechanged")) return "源已变";
  if (normalized.includes("unitymissing")) return "Unity缺失";
  if (normalized.includes("missingfromunitycatalog")) return "未入Catalog";
  if (normalized.includes("nounitycatalog")) return "未读取Catalog";
  if (normalized.includes("failed")) return "失败";
  if (normalized.includes("conflict")) return "冲突";
  return status || "Unknown";
}

function getUnityStatusTone(status, diagnostics = []) {
  const normalized = String(status || "").toLowerCase();
  if (normalized.includes("failed") || normalized.includes("conflict") || normalized.includes("blocked")) return "error";
  if (normalized.includes("missing") || normalized.includes("pending") || normalized.includes("sourcechanged")) return "warn";
  if (diagnostics.some(item => item.includes("为空") || item.includes("未读取") || item.includes("尚未"))) return "warn";
  if (["clean", "ready", "runtimeready", "imported", "skipped", "updated", "added", "created"].includes(normalized)) return "ok";
  return "";
}

function renderSyncBadge(sync) {
  return `<span class="sync-badge ${escapeHtml(sync.tone)}">${escapeHtml(sync.label)}</span>`;
}

function bindModelResource(resourceKey) {
  if (!resourceKey || !state.package) return;
  const resource = (state.package.resourceCatalog?.entries || []).find(entry => entry.resourceKey === resourceKey);
  if (!resource) return;
  ensureModelWrapperPose(resource);

  const role = el.modelImportRole?.value || "preview";
  const path = `resources/${resource.resourceKey}`;
  if (!state.canWrite || role === "preview") {
    state.selectedPath = path;
    state.resourcePickerOpen = false;
    state.message = role === "preview"
      ? `已选中资源：${getResourceDisplayName(resource)}`
      : "静态预览只能选中资源，不能替换绑定。";
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
  state.message = `${getResourceDisplayName(resource)} 已设为${getModelImportRole().label}。保存后写入资源包。`;
  render();
}

function clearCurrentModelBinding() {
  if (!state.canWrite || !state.package) return;
  const role = el.modelImportRole?.value || "preview";
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
      state.message = `没有找到 ${role} 武器挂载项。`;
      renderShellStatus();
      return;
    }
    if (attachment.previewResourceKey) removedKeys.push(attachment.previewResourceKey);
    attachment.previewResourceKey = "";
  }

  for (const resourceKey of removedKeys) {
    const resource = findModelResourceByKey(resourceKey);
    if (resource && role !== "body") {
      removeTag(resource.tags, role);
    }
  }

  state.selectedPath = role === "body" ? "geometry/body" : findWeaponPathBySlot(role);
  state.dirty = true;
  state.message = `${getModelImportRole().label} 已移除。保存后写入资源包。`;
  render();
}

function createConfiguration(kind) {
  if (!state.canWrite || !state.package) {
    state.message = "静态预览不能新增配置。请启动 Authoring server。";
    renderShellStatus();
    return;
  }

  ensureGeometryCollections();
  let path = "";
  let label = "";
  if (kind === "part") {
    const item = createBodyPartConfig();
    state.package.geometry.bodyParts.push(item);
    path = `geometry/body_parts/${item.partId}`;
    label = "身体部位";
  } else if (kind === "collider") {
    const item = createColliderConfig();
    state.package.geometry.colliders.push(item);
    path = `geometry/colliders/${item.colliderId}`;
    label = "碰撞体";
  } else if (kind === "socket") {
    const item = createSocketConfig();
    state.package.geometry.sockets.push(item);
    path = `geometry/sockets/${item.socketId}`;
    label = "挂点";
  } else if (kind === "weapon") {
    const item = createWeaponConfig();
    state.package.geometry.weaponAttachments.push(item);
    path = `geometry/weapon_attachments/${item.weaponId}`;
    label = "武器配置";
  } else if (kind === "trace") {
    const item = createTraceConfig();
    state.package.geometry.traces.push(item);
    path = `geometry/traces/${item.traceId}`;
    label = "攻击轨迹";
  } else {
    return;
  }

  state.selectedPath = path;
  state.dirty = true;
  state.message = `已新增${label}。请在右侧属性栏补齐引用关系并保存。`;
  render();
}

function ensureGeometryCollections() {
  state.package.geometry = state.package.geometry || {};
  const geometry = state.package.geometry;
  geometry.schemaVersion = geometry.schemaVersion || "1.0";
  geometry.bodyParts = Array.isArray(geometry.bodyParts) ? geometry.bodyParts : [];
  geometry.colliders = Array.isArray(geometry.colliders) ? geometry.colliders : [];
  geometry.sockets = Array.isArray(geometry.sockets) ? geometry.sockets : [];
  geometry.weaponAttachments = Array.isArray(geometry.weaponAttachments) ? geometry.weaponAttachments : [];
  geometry.traces = Array.isArray(geometry.traces) ? geometry.traces : [];
}

function createBodyPartConfig() {
  const partId = uniqueConfigId("part_new", (state.package.geometry.bodyParts || []).map(part => part?.partId));
  return {
    partId,
    displayName: "新部位",
    partKind: "Bone",
    parentPartId: firstValue(bodyPartOptions()),
    bonePath: "",
    locatorId: "",
    defaultHitZoneId: `hit.${partId}`,
    reactionGroupId: "reaction.humanoid.limb",
    tags: []
  };
}

function createColliderConfig() {
  const partId = firstValue(bodyPartOptions()) || "";
  const part = (state.package.geometry.bodyParts || []).find(item => item?.partId === partId);
  const colliderId = uniqueConfigId(`${partId || "body"}_sphere`, (state.package.geometry.colliders || []).map(collider => collider?.colliderId));
  return {
    colliderId,
    partId,
    hitZoneId: part?.defaultHitZoneId || "",
    shape: "Sphere",
    localPose: defaultLocalPose(part?.locatorId ? "Locator" : "BodyPart", part?.locatorId || partId),
    size: { x: 0.25, y: 0.25, z: 0.25 },
    radius: 0.15,
    height: 0,
    priority: 10,
    isWeakPoint: false,
    damageMultiplierOverride: 1,
    postureDamageScaleOverride: 1
  };
}

function createSocketConfig() {
  const partId = firstValue(bodyPartOptions()) || "";
  const socketId = uniqueConfigId("socket_new", (state.package.geometry.sockets || []).map(socket => socket?.socketId));
  return {
    socketId,
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
  const weaponId = uniqueConfigId(`weapon.${slot || "new"}`, (state.package.geometry.weaponAttachments || []).map(weapon => weapon?.weaponId));
  const resourceKey = firstValue(modelResourceOptions()) || "";
  return {
    weaponId,
    equipSlot: slot || "mainHand",
    attachSocketId: socketId,
    localGripPose: defaultLocalPose(socketId ? "Socket" : "ModelRoot", socketId),
    previewResourceKey: resourceKey,
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
    state.message = `没有找到 ${slot} 武器挂载项，无法替换。`;
    renderShellStatus();
    return false;
  }

  const previousKey = attachment.previewResourceKey || "";
  attachment.previewResourceKey = resource.resourceKey;
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
  const attachment = (state.package?.geometry?.weaponAttachments || []).find(item => item.equipSlot === slot);
  return attachment?.weaponId ? `geometry/weapon_attachments/${attachment.weaponId}` : "resources";
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

function renderSummary() {
  const pkg = state.package;
  if (!pkg) {
    el.packageSummary.innerHTML = `<div class="empty">No package loaded.</div>`;
    return;
  }
  const geometry = pkg.geometry || {};
  const unityEntries = getUnityCatalogEntries();
  el.packageSummary.innerHTML = [
    summaryCell("资源包", pkg.manifest?.packageId || "-"),
    summaryCell("版本", pkg.manifest?.version || "-"),
    summaryCell("资源", (pkg.resourceCatalog?.entries || []).length),
    summaryCell("Unity资源", unityEntries.length || "未读取"),
    summaryCell("碰撞体", (geometry.colliders || []).length),
    summaryCell("挂点", (geometry.sockets || []).length),
    summaryCell("轨迹", (geometry.traces || []).length)
  ].join("");
}

function summaryCell(label, value) {
  return `<div><strong>${escapeHtml(String(value))}</strong>${escapeHtml(label)}</div>`;
}

function renderTree() {
  const nodes = buildTree(state.package);
  el.packageTree.innerHTML = nodes.map(node => {
    const active = node.path === state.selectedPath ? " active" : "";
    return `<button class="${active}" type="button" data-path="${escapeHtml(node.path)}" style="padding-left:${8 + node.depth * 14}px"><span class="kind">${escapeHtml(KIND_LABELS[node.kind] || node.kind)}</span><span class="label">${escapeHtml(node.label)}</span></button>`;
  }).join("");
  el.packageTree.querySelectorAll("button[data-path]").forEach(button => {
    button.addEventListener("click", () => selectPath(button.dataset.path));
  });
}

function buildTree(pkg) {
  if (!pkg) return [];
  const g = pkg.geometry || {};
  const animationProfile = getDefaultAnimationProfile();
  const nodes = [
    node("manifest", "manifest", "manifest", 0),
    node("config", "config", "角色配置", 0),
    node("config/animation", "animationConfig", "动画配置", 0),
    ...(animationProfile ? [
      node(`config/animation/${encodeURIComponent(animationProfile.profileId)}`, "animationProfile", animationProfile.profileId || "animation profile", 1),
      ...grouped((animationProfile.slots || []), "animationSlot", slot => getAnimationSlotPath(animationProfile.profileId, slot.slotId), slot => `${slot.slotId || "slot"}:${slot.resourceKey || "未选择"}`, 2)
    ] : []),
    node("geometry/body", "body", g.bodyProfile?.profileId || "body geometry", 0),
    ...grouped((g.bodyParts || []), "part", part => `geometry/body_parts/${part.partId}`, part => part.partId || "part", 1),
    ...grouped((g.colliders || []), "collider", collider => `geometry/colliders/${collider.colliderId}`, collider => collider.colliderId || "collider", 1),
    ...grouped((g.sockets || []), "socket", socket => `geometry/sockets/${socket.socketId}`, socket => socket.socketId || "socket", 1),
    ...grouped((g.weaponAttachments || []), "weapon", attachment => `geometry/weapon_attachments/${attachment.weaponId}`, attachment => `${attachment.equipSlot || "slot"}:${attachment.weaponId || "weapon"}`, 1),
    ...grouped((g.traces || []), "trace", trace => `geometry/traces/${trace.traceId}`, trace => trace.traceId || "trace", 1),
    node("validation", "validation", "诊断与门禁", 0),
    ...grouped((state.validation?.issues || []), "issue", (_, index) => `validation/issues/${index}`, issue => issue.code || issue.message || "issue", 1)
  ];
  return nodes;
}

function node(path, kind, label, depth) {
  return { path, kind, label, depth };
}

function grouped(items, kind, pathFn, labelFn, depth) {
  return items.map((item, index) => node(pathFn(item, index), kind, labelFn(item, index), depth));
}

function renderLoadouts() {
  el.loadoutTabs.innerHTML = LOADOUTS.map(loadout => `<button type="button" data-loadout="${loadout.id}" class="${loadout.id === state.activeLoadout ? "active" : ""}">${loadout.label}</button>`).join("");
  el.loadoutTabs.querySelectorAll("button").forEach(button => {
    button.addEventListener("click", () => {
      state.activeLoadout = button.dataset.loadout;
      renderLoadouts();
      renderViewport();
      renderDiagnostics();
    });
  });
}

function renderPreviewControls() {
  if (!el.previewPoseSelect || !el.previewMotionSelect) return;
  el.previewPoseSelect.innerHTML = PREVIEW_POSES
    .map(pose => `<option value="${escapeHtml(pose.id)}"${pose.id === state.previewPose ? " selected" : ""}>${escapeHtml(pose.label)}</option>`)
    .join("");
  el.previewMotionSelect.innerHTML = PREVIEW_MOTIONS
    .map(motion => `<option value="${escapeHtml(motion.id)}"${motion.id === state.previewMotion ? " selected" : ""}>${escapeHtml(motion.label)}</option>`)
    .join("");
}

function renderViewport() {
  if (!state.package) {
    el.viewport.innerHTML = `<div class="empty">未加载资源包。</div>`;
    return;
  }

  if (viewportCleanup) {
    viewportCleanup();
    viewportCleanup = null;
  }

  const renderId = ++viewportRenderId;
  el.viewport.innerHTML = `<div class="viewport3d" aria-label="3D 角色预览"><div class="viewport-status">正在加载 3D 预览...</div></div>`;
  renderThreeViewport(renderId).catch(error => {
    if (renderId !== viewportRenderId) return;
    renderSvgViewport(`3D 预览不可用：${error instanceof Error ? error.message : String(error)}`);
  });
}

async function renderThreeViewport(renderId) {
  const runtime = await loadThreeRuntime();
  if (renderId !== viewportRenderId) return;

  const { THREE, GLTFLoader, OrbitControls } = runtime;
  const host = el.viewport.querySelector(".viewport3d");
  if (!host) return;

  host.innerHTML = "";

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0xf8fafb);

  const camera = new THREE.PerspectiveCamera(42, 1, 0.01, 100);
  camera.position.set(2.35, 1.55, 3.15);

  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, preserveDrawingBuffer: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  host.append(renderer.domElement);

  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.target.set(0, 0.95, 0);
  controls.minDistance = 0.75;
  controls.maxDistance = 8;

  scene.add(new THREE.HemisphereLight(0xffffff, 0xb8c2c9, 1.8));
  const key = new THREE.DirectionalLight(0xffffff, 1.7);
  key.position.set(2.5, 3.5, 2.5);
  scene.add(key);
  scene.add(new THREE.GridHelper(3.2, 16, 0xd6dde2, 0xe6ecef));

  const content = new THREE.Group();
  const pickables = [];
  scene.add(content);

  const loader = new GLTFLoader();
  const packageUrl = relative => encodeURI(`/${state.packageRelative}/${relative}`);
  const resources = state.package.resourceCatalog?.entries || [];
  const geometry = state.package.geometry || {};
  const socketsById = Object.fromEntries((geometry.sockets || []).map(socket => [socket.socketId, socket]));
  const activeSlots = new Set((LOADOUTS.find(loadout => loadout.id === state.activeLoadout) || LOADOUTS[0]).slots);

  const bodyRootKey = geometry.bodyProfile?.modelRootStableId || "";
  const bodyResource = resources.find(resource => isBodyModelBinding(resource, state.package))
    || resources.find(resource => bodyRootKey && (resource.resourceKey === bodyRootKey || resource.stableId === bodyRootKey));
  let loadedBody = false;
  let bodyBoneRecords = [];
  if (bodyResource?.relativePath) {
    loadedBody = await addGltfResource({
      THREE,
      loader,
      content,
      pickables,
      url: packageUrl(bodyResource.relativePath),
      objectPath: `resources/${bodyResource.resourceKey}`,
      name: bodyResource.localId || bodyResource.resourceKey,
      wrapperPose: bodyResource.importHints?.modelWrapperPose,
      boneSink: records => { bodyBoneRecords = records; }
    });
  }
  if (!loadedBody) addFallbackBody(THREE, content, pickables, geometry.bodyProfile);
  applyPreviewPose(THREE, bodyBoneRecords, state.previewPose);
  content.updateMatrixWorld(true);

  if (state.layers.colliders) addColliderMeshes(THREE, content, pickables, geometry.colliders || [], bodyBoneRecords);
  if (state.layers.sockets) addSocketMeshes(THREE, content, pickables, geometry.sockets || [], bodyBoneRecords);
  if (state.layers.traces) addTraceMeshes(THREE, content, pickables, (geometry.traces || []).filter(trace => activeSlots.has(trace.equipSlot)));
  if (state.layers.weapons) {
    await addWeaponMeshes({
      THREE,
      loader,
      content,
      pickables,
      resources,
      packageUrl,
      socketsById,
      bodyBoneRecords,
      attachments: (geometry.weaponAttachments || []).filter(attachment => activeSlots.has(attachment.equipSlot))
    });
  }

  content.updateMatrixWorld(true);
  syncPreviewBones(THREE, bodyBoneRecords);
  if (state.bonePickerOpen && getActiveBoneFieldPath(findTarget(state.selectedPath))) {
    addBoneGizmos(THREE, scene, pickables, bodyBoneRecords);
  }
  frameContent(THREE, camera, controls, content, geometry.bodyProfile);
  restoreViewportCamera(THREE, camera, controls);

  const raycaster = new THREE.Raycaster();
  raycaster.params.Line = { threshold: 0.08 };
  const pointer = new THREE.Vector2();
  const onPointerDown = event => {
    const rect = renderer.domElement.getBoundingClientRect();
    pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    pointer.y = -(((event.clientY - rect.top) / rect.height) * 2 - 1);
    raycaster.setFromCamera(pointer, camera);
    const hits = raycaster.intersectObjects(pickables, true);
    const boneHit = state.bonePickerOpen ? hits.find(item => findBonePick(item.object)) : null;
    if (boneHit) {
      const bonePick = findBonePick(boneHit.object);
      applyBonePick(bonePick);
      return;
    }
    if (state.bonePickerOpen) return;
    const hit = hits.find(item => findObjectPath(item.object));
    const objectPath = hit ? findObjectPath(hit.object) : "";
    if (objectPath) selectPath(objectPath);
  };
  renderer.domElement.addEventListener("pointerdown", onPointerDown);

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
    applyPreviewMotion(THREE, bodyBoneRecords, state.previewMotion, clock.getElapsedTime());
    updateBoneParentGroups(THREE, content);
    controls.update();
    renderer.render(scene, camera);
    frame = requestAnimationFrame(animate);
  };
  animate();

  viewportCleanup = () => {
    if (state.skipNextCameraRemember) {
      state.skipNextCameraRemember = false;
    } else {
      rememberViewportCamera(camera, controls);
    }
    cancelAnimationFrame(frame);
    resizeObserver.disconnect();
    renderer.domElement.removeEventListener("pointerdown", onPointerDown);
    renderer.dispose();
  };
}

function renderSvgViewport(message = "") {
  const g = state.package.geometry || {};
  const selected = state.selectedPath;
  const activeSlots = new Set((LOADOUTS.find(l => l.id === state.activeLoadout) || LOADOUTS[0]).slots);
  const attachments = (g.weaponAttachments || []).filter(a => activeSlots.has(a.equipSlot));
  const traces = (g.traces || []).filter(t => activeSlots.has(t.equipSlot));
  const socketsById = Object.fromEntries((g.sockets || []).map(s => [s.socketId, s]));
  const body = g.bodyProfile || {};
  const height = Number(body.heightMeters || body.defaultCapsule?.height || 1.8);
  const radius = Number(body.radiusMeters || body.defaultCapsule?.radius || 0.35);

  const shapes = [];
  shapes.push(`<rect x="46" y="${scaleY(height)}" width="8" height="${Math.max(8, height * 38)}" rx="4" fill="#dce8ea" stroke="#91a4ad" />`);
  shapes.push(`<ellipse cx="50" cy="${scaleY(height)}" rx="${Math.max(3, radius * 12)}" ry="4" fill="#e8f0f2" stroke="#91a4ad" />`);

  if (state.layers.colliders) {
    for (const c of g.colliders || []) {
      const p = c.localPose?.position || {};
      const x = scaleX(p.x);
      const y = scaleY(p.y);
      const path = `geometry/colliders/${c.colliderId}`;
      const cls = selected === path ? "selected" : "";
      if (c.shape === "Box") {
        shapes.push(`<rect class="${cls}" data-object-path="${escapeHtml(path)}" x="${x - 4}" y="${y - 5}" width="8" height="10" fill="rgba(31,122,122,0.18)" stroke="#1f7a7a" />`);
      } else if (c.shape === "Capsule") {
        shapes.push(`<rect class="${cls}" data-object-path="${escapeHtml(path)}" x="${x - 5}" y="${y - 14}" width="10" height="28" rx="5" fill="rgba(31,122,122,0.16)" stroke="#1f7a7a" />`);
      } else {
        shapes.push(`<circle class="${cls}" data-object-path="${escapeHtml(path)}" cx="${x}" cy="${y}" r="${Math.max(3, Number(c.radius || 0.12) * 18)}" fill="rgba(31,122,122,0.16)" stroke="#1f7a7a" />`);
      }
    }
  }

  if (state.layers.sockets) {
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
      const path = `geometry/weapon_attachments/${a.weaponId}`;
      const cls = selected === path ? "selected" : "";
      shapes.push(`<g class="${cls}" data-object-path="${escapeHtml(path)}"><rect x="${x - 3}" y="${y - 13}" width="6" height="22" rx="2" fill="rgba(180,106,31,0.18)" stroke="#b46a1f"/><text x="${x + 5}" y="${y - 9}" font-size="3" fill="#734515">${escapeHtml(a.equipSlot || "")}</text></g>`);
    }
  }

  if (state.layers.traces) {
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

  el.viewport.innerHTML = `<div class="viewport-fallback">${message ? `<div class="viewport-status">${escapeHtml(message)}</div>` : ""}<svg viewBox="0 0 100 100" role="img" aria-label="Character package viewport"><defs><pattern id="grid" width="10" height="10" patternUnits="userSpaceOnUse"><path d="M 10 0 L 0 0 0 10" fill="none" stroke="#edf1f3" stroke-width="0.6"/></pattern></defs><rect width="100" height="100" fill="url(#grid)"/><text x="4" y="7" font-size="3.5" fill="#61717f">${escapeHtml(state.package.manifest?.packageId || "character")}</text>${shapes.join("")}</svg></div>`;
  el.viewport.querySelectorAll("[data-object-path]").forEach(item => {
    item.addEventListener("click", event => {
      event.stopPropagation();
      selectPath(item.getAttribute("data-object-path"));
    });
  });
}

async function loadThreeRuntime() {
  if (!threeRuntimePromise) {
    threeRuntimePromise = Promise.all([
      import("../node_modules/three/build/three.module.js"),
      import("../node_modules/three/examples/jsm/loaders/GLTFLoader.js"),
      import("../node_modules/three/examples/jsm/controls/OrbitControls.js")
    ]).then(([THREE, loaderModule, controlsModule]) => ({
      THREE,
      GLTFLoader: loaderModule.GLTFLoader,
      OrbitControls: controlsModule.OrbitControls
    }));
  }
  return threeRuntimePromise;
}

async function addGltfResource({ THREE, loader, content, pickables, url, objectPath, name, position = null, bindingPose = null, attachmentPose = null, wrapperPose = null, boneSink = null }) {
  try {
    const gltf = await new Promise((resolve, reject) => loader.load(url, resolve, undefined, reject));
    const root = gltf.scene;
    root.name = name || objectPath;
    const boneRecords = collectBoneRecords(root);
    const bindingRoot = new THREE.Group();
    bindingRoot.name = `${root.name || "model"}_binding`;
    if (position) bindingRoot.position.copy(position);
    applyLocalPose(THREE, bindingRoot, bindingPose);
    applyLocalPose(THREE, bindingRoot, attachmentPose);

    const modelWrapper = new THREE.Group();
    modelWrapper.name = `${root.name || "model"}_wrapper`;
    applyLocalPose(THREE, modelWrapper, wrapperPose);
    modelWrapper.add(root);
    bindingRoot.add(modelWrapper);

    makeSelectable(bindingRoot, objectPath, pickables);
    content.add(bindingRoot);
    if (boneSink) boneSink(boneRecords);
    return true;
  } catch {
    return false;
  }
}

function collectBoneRecords(root) {
  const records = [];
  root.traverse(object => {
    if (!object.isBone) return;
    const path = getPreviewBonePath(object, root);
    if (!path) return;
    const parentPath = object.parent?.isBone ? getPreviewBonePath(object.parent, root) : "";
    const depth = Math.max(0, path.split("/").length - 1);
    records.push({
      bone: object,
      name: object.name || path.split("/").pop() || path,
      path,
      alias: object.name ? `bone.${object.name}` : "",
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
  return (records || []).find(record => getPreviewBoneSemanticKey(record) === semanticKey) || null;
}

function getPreviewBoneSemanticKey(record) {
  const value = normalizeBoneMatchText(`${record?.name || ""} ${(record?.path || "").split("/").filter(Boolean).pop() || ""}`);
  if (/rightupperarm|upperarmr|rupperarm|rightarm/.test(value)) return "right_upper_arm";
  if (/leftupperarm|upperarml|lupperarm|leftarm/.test(value)) return "left_upper_arm";
  if (/rightforearm|forearmr|rforearm/.test(value)) return "right_forearm";
  if (/leftforearm|forearml|lforearm/.test(value)) return "left_forearm";
  if (/righthand|handr|rhand/.test(value)) return "right_hand";
  if (/lefthand|handl|lhand/.test(value)) return "left_hand";
  if (/rightleg|legr|rleg|rightthigh|thighr/.test(value)) return "right_leg";
  if (/leftleg|legl|lleg|leftthigh|thighl/.test(value)) return "left_leg";
  if (/chest|spine003|spine004|breast/.test(value)) return "chest";
  if (/neck/.test(value)) return "neck";
  if (/hips|hip|pelvis/.test(value)) return "hips";
  return "";
}

function applyLocalPose(THREE, object, pose) {
  if (!object || !pose) return;
  const position = pose.position || {};
  object.position.x += numberOrDefault(position.x, 0);
  object.position.y += numberOrDefault(position.y, 0);
  object.position.z += numberOrDefault(position.z, 0);

  const rotation = pose.rotation || {};
  const hasQuaternion = ["x", "y", "z"].some(key => Math.abs(numberOrDefault(rotation[key], 0)) > 0.000001)
    || Math.abs(numberOrDefault(rotation.w, 1) - 1) > 0.000001;
  if (hasQuaternion) {
    object.quaternion.multiply(new THREE.Quaternion(
      numberOrDefault(rotation.x, 0),
      numberOrDefault(rotation.y, 0),
      numberOrDefault(rotation.z, 0),
      numberOrDefault(rotation.w, 1)
    ).normalize());
  } else if (pose.eulerHint) {
    object.rotation.x += degreesToRadians(pose.eulerHint.x);
    object.rotation.y += degreesToRadians(pose.eulerHint.y);
    object.rotation.z += degreesToRadians(pose.eulerHint.z);
  }

  const scale = pose.scale || {};
  object.scale.x *= numberOrDefault(scale.x, 1);
  object.scale.y *= numberOrDefault(scale.y, 1);
  object.scale.z *= numberOrDefault(scale.z, 1);
}

function getEffectiveSocketPose(socket) {
  if (!socket) return null;
  const pose = socket.localPose ? clone(socket.localPose) : {};
  if (pose.parentKind) return pose;
  if (socket.bonePath) {
    pose.parentKind = "Bone";
    pose.parentPath = socket.bonePath;
  } else if (socket.locatorPath) {
    pose.parentKind = "Locator";
    pose.parentPath = socket.locatorPath;
  } else if (socket.parentPartId) {
    pose.parentKind = "BodyPart";
    pose.parentPath = socket.parentPartId;
  }
  return pose;
}

function resolvePoseParentContent(THREE, content, pose, boneRecords, name) {
  if (pose?.parentKind !== "Bone") return content;
  const record = findBoneRecordForValue(boneRecords, pose.parentPath);
  if (!record?.bone) return content;

  const parent = new THREE.Group();
  parent.name = name || "bone_pose_parent";
  parent.userData.boneParent = { bone: record.bone };
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
  const bone = parent.userData?.boneParent?.bone;
  if (!bone) return;
  content.updateMatrixWorld(true);
  bone.updateWorldMatrix(true, false);
  const localMatrix = new THREE.Matrix4()
    .copy(content.matrixWorld)
    .invert()
    .multiply(bone.matrixWorld);
  localMatrix.decompose(parent.position, parent.quaternion, parent.scale);
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

  const semanticKey = getSemanticBoneKey(`${nameKey} ${aliasKey} ${pathKey}`);
  if (semanticKey && requestedKeys.has(semanticKey) && isPrimarySemanticBoneRecord(record, semanticKey)) return 2;

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
    const semantic = getSemanticBoneKey(normalized);
    if (semantic) keys.add(semantic);
  }
  return keys;
}

function normalizeBoneMatchText(value) {
  return String(value || "").toLowerCase().replace(/[^a-z0-9]+/g, "");
}

function getSemanticBoneKey(value) {
  if (!value) return "";
  if (/righthand|handr|rhand/.test(value)) return "right_hand";
  if (/lefthand|handl|lhand/.test(value)) return "left_hand";
  if (/rightfoot|footr|rfoot/.test(value)) return "right_foot";
  if (/leftfoot|footl|lfoot/.test(value)) return "left_foot";
  if (/head|skull/.test(value)) return "head";
  if (/neck/.test(value)) return "neck";
  if (/chest|breast/.test(value)) return "chest";
  if (/hips|hip|pelvis/.test(value)) return "hips";
  return "";
}

function isPrimarySemanticBoneRecord(record, semanticKey) {
  const text = normalizeBoneMatchText(`${record.name || ""} ${(record.path || "").split("/").filter(Boolean).pop() || ""}`);
  if (semanticKey === "right_hand") return /^(righthand|handr|rhand)$/.test(text);
  if (semanticKey === "left_hand") return /^(lefthand|handl|lhand)$/.test(text);
  if (semanticKey === "right_foot") return /^(rightfoot|footr|rfoot)$/.test(text);
  if (semanticKey === "left_foot") return /^(leftfoot|footl|lfoot)$/.test(text);
  if (semanticKey === "head") return /^(head|skull)$/.test(text);
  if (semanticKey === "neck") return /^neck$/.test(text);
  if (semanticKey === "chest") return /^(chest|breast)$/.test(text);
  if (semanticKey === "hips") return /^(hips|hip|pelvis)$/.test(text);
  return false;
}

function degreesToRadians(value) {
  return Number(value || 0) * Math.PI / 180;
}

function numberOrDefault(value, fallback) {
  const number = Number(value);
  return Number.isFinite(number) ? number : fallback;
}

function addFallbackBody(THREE, content, pickables, body = {}) {
  const height = Number(body.heightMeters || body.defaultCapsule?.height || 1.8);
  const radius = Number(body.radiusMeters || body.defaultCapsule?.radius || 0.34);
  const material = new THREE.MeshStandardMaterial({ color: 0xcddde0, roughness: 0.72, metalness: 0.05 });
  const geometry = THREE.CapsuleGeometry
    ? new THREE.CapsuleGeometry(radius, Math.max(0.01, height - radius * 2), 8, 18)
    : new THREE.CylinderGeometry(radius, radius, height, 18);
  const mesh = new THREE.Mesh(geometry, material);
  mesh.position.y = height / 2;
  makeSelectable(mesh, "geometry/body", pickables);
  content.add(mesh);
}

function addColliderMeshes(THREE, content, pickables, colliders, bodyBoneRecords = []) {
  for (const collider of colliders) {
    const objectPath = `geometry/colliders/${collider.colliderId}`;
    const selected = state.selectedPath === objectPath;
    const material = new THREE.MeshBasicMaterial({
      color: selected ? 0xffa11f : 0x1f7a7a,
      transparent: true,
      opacity: selected ? 0.42 : 0.24,
      wireframe: !selected,
      depthWrite: false
    });
    let geometry;
    if (collider.shape === "Box") {
      const size = collider.size || {};
      geometry = new THREE.BoxGeometry(Number(size.x || 0.25), Number(size.y || 0.25), Number(size.z || 0.25));
    } else if (collider.shape === "Capsule" && THREE.CapsuleGeometry) {
      const radius = Number(collider.radius || 0.12);
      const height = Number(collider.height || 0.5);
      geometry = new THREE.CapsuleGeometry(radius, Math.max(0.01, height - radius * 2), 8, 16);
    } else {
      geometry = new THREE.SphereGeometry(Number(collider.radius || 0.12), 20, 12);
    }
    const mesh = new THREE.Mesh(geometry, material);
    const parent = resolvePoseParentContent(THREE, content, collider.localPose, bodyBoneRecords, `collider_${collider.colliderId}_parent`);
    applyLocalPose(THREE, mesh, collider.localPose);
    makeSelectable(mesh, objectPath, pickables);
    parent.add(mesh);
  }
}

function addSocketMeshes(THREE, content, pickables, sockets, bodyBoneRecords = []) {
  for (const socket of sockets) {
    const objectPath = `geometry/sockets/${socket.socketId}`;
    const selected = state.selectedPath === objectPath;
    const material = new THREE.MeshStandardMaterial({ color: selected ? 0xffa11f : 0xb46a1f, emissive: selected ? 0x442000 : 0x000000 });
    const mesh = new THREE.Mesh(new THREE.SphereGeometry(0.035, 16, 10), material);
    const pose = getEffectiveSocketPose(socket);
    const parent = resolvePoseParentContent(THREE, content, pose, bodyBoneRecords, `socket_${socket.socketId}_parent`);
    applyLocalPose(THREE, mesh, pose);
    makeSelectable(mesh, objectPath, pickables);
    parent.add(mesh);
  }
}

function addTraceMeshes(THREE, content, pickables, traces) {
  for (const trace of traces) {
    const objectPath = `geometry/traces/${trace.traceId}`;
    const selected = state.selectedPath === objectPath;
    const points = [
      toVector3(THREE, trace.startPose?.position),
      toVector3(THREE, trace.endPose?.position)
    ];
    const line = new THREE.Line(
      new THREE.BufferGeometry().setFromPoints(points),
      new THREE.LineBasicMaterial({ color: selected ? 0xffa11f : 0xc24141, linewidth: 2 })
    );
    makeSelectable(line, objectPath, pickables);
    content.add(line);
  }
}

async function addWeaponMeshes({ THREE, loader, content, pickables, resources, packageUrl, socketsById, bodyBoneRecords = [], attachments }) {
  for (const attachment of attachments) {
    const objectPath = `geometry/weapon_attachments/${attachment.weaponId}`;
    const socket = socketsById[attachment.attachSocketId];
    const resource = resources.find(entry => entry.resourceKey === attachment.previewResourceKey);
    const socketPose = getEffectiveSocketPose(socket);
    const gripPose = attachment.localGripPose || null;
    const directBoneGrip = gripPose?.parentKind === "Bone";
    const parentPose = directBoneGrip ? gripPose : socketPose;
    const bindingPose = directBoneGrip ? gripPose : socketPose;
    const attachmentPose = directBoneGrip ? null : gripPose;
    const parent = resolvePoseParentContent(THREE, content, parentPose, bodyBoneRecords, `weapon_${attachment.weaponId}_parent`);
    let loaded = false;
    if (resource?.relativePath) {
      loaded = await addGltfResource({
        THREE,
        loader,
        content: parent,
        pickables,
        url: packageUrl(resource.relativePath),
        objectPath,
        name: attachment.weaponId,
        bindingPose,
        attachmentPose,
        wrapperPose: resource.importHints?.modelWrapperPose
      });
    }
    if (!loaded) {
      const selected = state.selectedPath === objectPath;
      const material = new THREE.MeshStandardMaterial({ color: selected ? 0xffa11f : 0xb46a1f, transparent: true, opacity: 0.74 });
      const mesh = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.45, 0.08), material);
      applyLocalPose(THREE, mesh, bindingPose);
      applyLocalPose(THREE, mesh, attachmentPose);
      mesh.position.x += attachment.equipSlot === "offHand" ? -0.12 : 0.12;
      makeSelectable(mesh, objectPath, pickables);
      parent.add(mesh);
    }
  }
}

function frameContent(THREE, camera, controls, content, body = {}) {
  const box = new THREE.Box3().setFromObject(content);
  if (!box.isEmpty()) {
    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());
    const radius = Math.max(size.x, size.y, size.z, 1);
    controls.target.copy(center);
    controls.target.y = Math.max(0.8, center.y);
    camera.position.set(center.x + radius * 1.2, controls.target.y + radius * 0.55, center.z + radius * 1.7);
    camera.near = Math.max(0.01, radius / 100);
    camera.far = Math.max(100, radius * 20);
    camera.updateProjectionMatrix();
  } else {
    controls.target.set(0, Number(body.heightMeters || 1.8) / 2, 0);
  }
  controls.update();
}

function rememberViewportCamera(camera, controls) {
  if (!camera || !controls) return;
  state.viewportCameraState = {
    position: camera.position.toArray(),
    target: controls.target.toArray()
  };
}

function restoreViewportCamera(THREE, camera, controls) {
  const saved = state.viewportCameraState;
  if (!saved?.position || !saved?.target) return;
  camera.position.fromArray(saved.position);
  controls.target.fromArray(saved.target);
  controls.update();
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
  const lines = layout.nodes
    .filter(node => node.parent && layout.byPath.has(node.parent))
    .map(node => {
      const parent = layout.byPath.get(node.parent);
      return `<line x1="${parent.x}" y1="${parent.y}" x2="${node.x}" y2="${node.y}" />`;
    }).join("");
  const nodes = layout.nodes.map(node => {
    const selected = isBoneRecordSelected(node.record, selectedValue);
    const label = getBoneMapLabel(node.record, selected);
    return `<g class="bone-node ${selected ? "selected" : ""}" data-bone-path="${escapeHtml(node.record.path)}" data-bone-alias="${escapeHtml(node.record.alias || "")}" data-bone-name="${escapeHtml(node.record.name || "")}" transform="translate(${node.x} ${node.y})"><title>${escapeHtml(node.record.path)}</title><circle r="${selected ? 4.2 : 3.1}"></circle>${label ? `<text x="7" y="3">${escapeHtml(label)}</text>` : ""}</g>`;
  }).join("");

  return `
    <div class="bone-picker ${layout.mode === "map" ? "bone-map" : "bone-tree"}" aria-label="骨骼选择">
      <div class="bone-picker-head">
        <strong>骨骼</strong>
        <span>${escapeHtml(title)}${layout.mode === "map" ? " · 2D" : ""}</span>
        <button type="button" data-picker-action="closeBonePicker">收起</button>
      </div>
      <svg viewBox="0 0 ${layout.width} ${layout.height}" role="img" aria-label="Skeleton picker">
        <g class="bone-links">${lines}</g>
        <g class="bone-nodes">${nodes}</g>
      </svg>
    </div>`;
}

function wireBonePicker(root) {
  root.querySelectorAll('[data-picker-action="closeBonePicker"]').forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      state.bonePickerOpen = false;
      renderViewport();
      renderInspector();
    });
  });
  root.querySelectorAll("[data-bone-path]").forEach(item => {
    item.addEventListener("click", event => {
      event.stopPropagation();
      applyBonePick({
        path: item.getAttribute("data-bone-path") || "",
        alias: item.getAttribute("data-bone-alias") || "",
        name: item.getAttribute("data-bone-name") || ""
      });
    });
  });
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
  const trimmed = records.slice(0, 128);
  if (trimmed.filter(hasBonePosition).length >= 3) return layoutBoneMap(trimmed);
  return layoutBoneTree(trimmed);
}

function layoutBoneMap(records) {
  const width = 320;
  const height = 320;
  const margin = 26;
  const positioned = records.filter(hasBonePosition);
  const xs = positioned.map(record => record.position.x);
  const ys = positioned.map(record => record.position.y);
  const minX = Math.min(...xs);
  const maxX = Math.max(...xs);
  const minY = Math.min(...ys);
  const maxY = Math.max(...ys);
  const rangeX = Math.max(0.001, maxX - minX);
  const rangeY = Math.max(0.001, maxY - minY);
  const scale = Math.min((width - margin * 2) / rangeX, (height - margin * 2) / rangeY) * 0.92;
  const centerX = (minX + maxX) / 2;
  const centerY = (minY + maxY) / 2;
  const nodes = records
    .filter(hasBonePosition)
    .map(record => ({
      record,
      parent: getBoneParentPath(record, records),
      x: Math.round((width / 2 + (record.position.x - centerX) * scale) * 10) / 10,
      y: Math.round((height / 2 - (record.position.y - centerY) * scale) * 10) / 10
    }));
  const byPath = new Map(nodes.map(node => [node.record.path, node]));
  return { nodes, byPath, width, height, mode: "map" };
}

function layoutBoneTree(records) {
  const nodes = records.slice(0, 96).map((record, index) => {
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
  if (selected) return `${side}${kind || record.name || "骨骼"}`;
  if (!isPrimaryBoneLabel(record, kind)) return "";
  return `${side}${kind}`;
}

function isPrimaryBoneLabel(record, kind) {
  const name = getBoneNameSearchText(record);
  if (kind === "手") return /(^|[._-])hand[lr]?\b|right\s*hand|left\s*hand|righthand|lefthand|wrist/.test(name);
  if (kind === "脚") return /(^|[._-])foot[lr]?\b|right\s*foot|left\s*foot|rightfoot|leftfoot|ankle/.test(name);
  if (kind === "头" || kind === "颈") return true;
  if (kind === "胸") return /chest/.test(name);
  if (kind === "髋") return /^(hip|hips|pelvis)$/.test(name);
  return false;
}

function getBoneSideLabel(record) {
  const text = getBoneSearchText(record);
  if (/(^|[^a-z])(left|lhand|handl|lfoot|footl|lshoulder|shoulderl|larm|arml|lleg|legl|lthigh|thighl|lshin|shinl|lpelvis|pelvisl|lefthand|leftfoot)([^a-z]|$)/i.test(text)) return "左";
  if (/(^|[^a-z])(right|rhand|handr|rfoot|footr|rshoulder|shoulderr|rarm|armr|rleg|legr|rthigh|thighr|rshin|shinr|rpelvis|pelvisr|righthand|rightfoot)([^a-z]|$)/i.test(text)) return "右";
  return "";
}

function getBoneKindLabel(record) {
  const text = getBoneNameSearchText(record) || getBoneSearchText(record);
  if (/head|skull/.test(text)) return "头";
  if (/neck/.test(text)) return "颈";
  if (/finger|thumb|pinky|ring|index|middle/.test(text)) return "手指";
  if (/hand|wrist|palm/.test(text)) return "手";
  if (/foot|ankle|toe|heel/.test(text)) return "脚";
  if (/chest|breast/.test(text)) return "胸";
  if (/hip|hips|pelvis/.test(text)) return "髋";
  if (/spine/.test(text)) return "躯干";
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
  if (fieldPath === "bonePath") return "骨骼路径";
  if (fieldPath.endsWith(".parentPath")) return "父空间骨骼";
  return "骨骼";
}

function toVector3(THREE, value = {}) {
  return new THREE.Vector3(Number(value.x || 0), Number(value.y || 0), Number(value.z || 0));
}

function renderInspector() {
  const target = findTarget(state.selectedPath);
  el.selectionBadge.textContent = target.kind || "none";
  if (!target.value) {
    el.inspector.innerHTML = `<div class="empty">请选择一个资源包对象。</div>`;
    return;
  }
  normalizeInspectorTarget(target);
  const fields = editableFields(target.kind, target.value);
  if (fields.length === 0) {
    el.inspector.innerHTML = `${renderObjectTitle(target)}<pre>${escapeHtml(JSON.stringify(target.value, null, 2))}</pre>`;
    return;
  }
  el.inspector.innerHTML = `${renderObjectTitle(target)}${renderFieldSections(target, fields)}`;
  const boneInputs = [];
  el.inspector.querySelectorAll("[data-field]").forEach(input => {
    if (input.dataset.picker === "bone") {
      boneInputs.push(input);
      input.addEventListener("focus", () => activateBoneField(input.dataset.field));
      input.addEventListener("click", () => activateBoneField(input.dataset.field));
    }
    input.addEventListener("input", () => {
      commitInspectorField(target, input);
    });
    input.addEventListener("change", () => {
      const value = commitInspectorField(target, input);
      if (input.dataset.type === "number" && value !== undefined) {
        input.value = formatFieldValue(value, "number");
      }
    });
  });
  if (!boneInputs.some(input => input.dataset.field === state.activeBoneFieldPath)) {
    state.activeBoneFieldPath = boneInputs[0]?.dataset.field || "";
    state.bonePickerOpen = false;
  }
  el.inspector.querySelectorAll('[data-picker-action="openBonePicker"]').forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      activateBoneField(button.dataset.pickerField);
    });
  });
  el.inspector.querySelectorAll('[data-picker-action="openResourcePicker"]').forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      openResourcePickerForField(button.dataset.pickerField);
    });
  });
  wireBonePicker(el.inspector);
  el.inspector.querySelectorAll("[data-inspector-action]").forEach(button => {
    button.addEventListener("click", () => {
      if (button.dataset.inspectorAction !== "resetModelWrapperPose") return;
      if (target.kind !== "resource" || target.value?.typeId !== "model") return;
      resetModelWrapperPose(target.value);
      state.dirty = true;
      state.message = "模型变换修正已重置。保存后写入资源包。";
      render();
    });
  });
  el.inspector.querySelectorAll("button[data-jump]").forEach(button => {
    button.addEventListener("click", () => selectPath(button.dataset.jump));
  });
}

function renderObjectTitle(target) {
  return `<div class="object-title"><strong>${escapeHtml(target.label)}</strong><span>${escapeHtml(state.selectedPath)}</span></div>${renderInspectorContext(target)}`;
}

function renderInspectorContext(target) {
  if (target.kind === "animationSlot") return renderAnimationSlotReferenceContext(target.value);
  if (target.kind === "weapon") return renderWeaponReferenceContext(target.value);
  if (target.kind === "resource" && target.value) {
    const modelContext = target.value?.typeId === "model" ? renderModelResourceContext(target.value) : "";
    return `${modelContext}${renderResourceSyncContext(target.value)}`;
  }
  return "";
}

function renderAnimationSlotReferenceContext(slot) {
  const resource = findAnimationSlotResource(slot);
  const selection = slot?.resourceSelection || {};
  const selectedText = resource
    ? getResourceDisplayName(resource.resource || resource)
    : firstNonEmpty(slot?.resourceKey, selection.runtimeResourceKey, selection.packageResourceKey, selection.providerResourceKey, selection.unityAssetPath, "未选择");
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>动画槽位引用</strong><span>该槽位保存 ResourceSelectionRef；资源由 Resource Manager 负责准备。</span></div>
    <div class="reference-row"><span>当前资源</span><strong>${escapeHtml(selectedText)}</strong></div>
    <div class="reference-row"><span>绑定类型</span><strong>${escapeHtml(selection.bindingKind || resource?.bindingKind || "未解析")}</strong></div>
    <div class="reference-row"><span>预热分组</span><strong>${escapeHtml(slot?.preloadPolicy || "AnimationWarmup")}</strong></div>
    ${resource?.path ? renderReferenceRow("资源详情", resource.displayName, resource.path, "打开资源") : ""}
  </section>`;
}

function renderWeaponReferenceContext(weapon) {
  const resource = findResourceByKey(weapon.previewResourceKey);
  const socket = findSocketById(weapon.attachSocketId);
  const trace = findTraceById(weapon.traceId);
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>武器引用关系</strong><span>武器配置只保存引用；模型、挂点、轨迹仍是独立配置。</span></div>
    ${renderReferenceRow("模型资源", resource ? getResourceDisplayName(resource) : weapon.previewResourceKey || "未绑定", resource ? `resources/${resource.resourceKey}` : "", "编辑模型尺寸/旋转")}
    ${renderReferenceRow("绑定挂点", socket ? socket.socketId : weapon.attachSocketId || "未绑定", socket ? `geometry/sockets/${socket.socketId}` : "", "编辑挂点")}
    ${renderReferenceRow("攻击轨迹", trace ? trace.traceId : weapon.traceId || "未绑定", trace ? `geometry/traces/${trace.traceId}` : "", "编辑轨迹")}
  </section>`;
}

function renderModelResourceContext(resource) {
  const owners = (state.package?.geometry?.weaponAttachments || [])
    .filter(weapon => weapon?.previewResourceKey === resource.resourceKey);
  const ownerText = owners.length
    ? owners.map(weapon => `${weapon.equipSlot || "slot"}:${weapon.weaponId}`).join(" / ")
    : (isBodyModelBinding(resource, state.package) ? "角色主体" : "未被角色或武器引用");
  const packagePath = `${state.packageRelative}/${resource.relativePath || ""}`;
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>模型资源</strong><span>这里调整的是模型外层包裹节点的位置、旋转和缩放。</span></div>
    <div class="reference-row"><span>当前引用</span><strong>${escapeHtml(ownerText)}</strong></div>
    <div class="reference-row"><span>包内源文件</span><code>${escapeHtml(packagePath)}</code></div>
  </section>`;
}

function renderResourceSyncContext(resource) {
  const sync = getResourceUnitySync(resource);
  const report = getUnityImportReport();
  const operation = sync.operation;
  const diagnostics = sync.diagnostics;
  const diagnosticsHtml = diagnostics.length
    ? `<div class="sync-diagnostics">${diagnostics.map(item => `<div>${escapeHtml(item)}</div>`).join("")}</div>`
    : `<div class="meta">暂无同步诊断。</div>`;
  return `<section class="reference-card sync-card">
    <div class="reference-card-head"><strong>Unity 同步状态</strong><span>来自导入报告和 unity_resource_catalog；这里只展示状态，不修改 Unity 资产。</span></div>
    <div class="reference-row"><span>状态</span><strong>${renderSyncBadge(sync)} ${escapeHtml(sync.status)}</strong></div>
    <div class="reference-row"><span>Unity 路径</span><code title="${escapeHtml(sync.unityAssetPath)}">${escapeHtml(sync.unityAssetPath || "未生成")}</code></div>
    <div class="reference-row"><span>GUID</span><code>${escapeHtml(sync.unityAssetGuid || "未记录")}</code></div>
    <div class="reference-row"><span>Importer</span><strong>${escapeHtml(sync.importerKind || "未记录")}</strong></div>
    <div class="reference-row"><span>主对象</span><strong>${escapeHtml(sync.unityMainObjectType || "未记录")}</strong></div>
    <div class="reference-row"><span>最近操作</span><strong>${escapeHtml(operation ? `${operation.action || operation.kind || "operation"} / ${operation.writePolicy || "-"}` : "无报告操作")}</strong></div>
    <div class="reference-row"><span>报告</span><code title="${escapeHtml(report?.reportPath || state.importResult?.reportOut || "")}">${escapeHtml(report?.reportPath || state.importResult?.reportOut || "未生成")}</code></div>
    ${diagnosticsHtml}
  </section>`;
}

function renderReferenceRow(label, value, jumpPath, actionLabel) {
  return `<div class="reference-row"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong>${jumpPath ? `<button type="button" data-jump="${escapeHtml(jumpPath)}">${escapeHtml(actionLabel || "打开")}</button>` : ""}</div>`;
}

function findResourceByKey(resourceKey) {
  if (!resourceKey) return null;
  const packageResource = (state.package?.resourceCatalog?.entries || []).find(resource => resource?.resourceKey === resourceKey);
  if (packageResource) return packageResource;
  const libraryItem = findResourceLibraryItemByKey(resourceKey);
  return libraryItem?.resource || null;
}

function collectResourceReferences(resource) {
  const references = [];
  const key = resource?.resourceKey || "";
  const stableId = resource?.stableId || "";
  if (!key && !stableId) return references;
  if (isApplicationResourceReference(key, state.package)) {
    references.push({ sourceConfigKind: "character", sourceField: "resourceKeys", preloadPolicy: "SpawnCritical" });
  }
  for (const attachment of state.package?.geometry?.weaponAttachments || []) {
    if (attachment?.previewResourceKey === key) {
      references.push({ sourceConfigKind: "weapon", sourceStableId: attachment.weaponId, sourceField: "previewResourceKey", preloadPolicy: "EquipmentInitial" });
    }
  }
  for (const profile of state.package?.applicationConfig?.animationProfiles || []) {
    for (const slot of profile?.slots || []) {
      const selected = [
        slot?.resourceKey,
        slot?.resourceSelection?.runtimeResourceKey,
        slot?.resourceSelection?.packageResourceKey,
        slot?.resourceSelection?.providerResourceKey,
        slot?.resourceSelection?.resourceStableId
      ].filter(Boolean);
      if (selected.includes(key) || selected.includes(stableId)) {
        references.push({
          sourceConfigKind: "animation",
          sourceStableId: profile.profileId,
          sourceField: `slots/${slot.slotId}`,
          preloadPolicy: slot.preloadPolicy || "AnimationWarmup"
        });
      }
    }
  }
  for (const entry of state.package?.resourceCatalog?.entries || []) {
    if (entry === resource) continue;
    for (const dependency of entry.dependencies || []) {
      if (dependency?.resourceKey === key || dependency?.stableId === stableId) {
        references.push({ sourceConfigKind: "resource", sourceStableId: entry.stableId || entry.resourceKey, sourceField: "dependencies", preloadPolicy: "AnimationWarmup" });
      }
    }
    const preview = entry.preview || {};
    if ([preview.thumbnailResourceKey, preview.previewMeshResourceKey, preview.placeholderResourceKey].includes(key)) {
      references.push({ sourceConfigKind: "resource", sourceStableId: entry.stableId || entry.resourceKey, sourceField: "preview", preloadPolicy: "UiDeferred" });
    }
  }
  return references;
}

function formatDiagnosticText(issue) {
  if (!issue) return "";
  if (typeof issue === "string") return issue;
  return [issue.severity, issue.code || issue.gate, issue.message].filter(Boolean).join(": ");
}

function findSocketById(socketId) {
  if (!socketId) return null;
  return (state.package?.geometry?.sockets || []).find(socket => socket?.socketId === socketId) || null;
}

function findTraceById(traceId) {
  if (!traceId) return null;
  return (state.package?.geometry?.traces || []).find(trace => trace?.traceId === traceId) || null;
}

function normalizeInspectorTarget(target) {
  if (target.kind === "resource" && target.value?.typeId === "model") {
    ensureModelWrapperPose(target.value);
  } else if (target.kind === "collider") {
    ensureLocalPose(target.value, "localPose", { parentKind: "BodyPart", parentPath: target.value.partId || "" });
  } else if (target.kind === "socket") {
    ensureLocalPose(target.value, "localPose", defaultSocketPoseParent(target.value));
  } else if (target.kind === "weapon") {
    ensureLocalPose(target.value, "localGripPose", { parentKind: "Socket", parentPath: target.value.attachSocketId || "" });
  } else if (target.kind === "trace") {
    ensureLocalPose(target.value, "startPose", { parentKind: "Locator", parentPath: target.value.startLocatorPath || "" });
    ensureLocalPose(target.value, "endPose", { parentKind: "Locator", parentPath: target.value.endLocatorPath || "" });
  }
}

function defaultSocketPoseParent(socket) {
  if (socket?.bonePath) return { parentKind: "Bone", parentPath: socket.bonePath };
  if (socket?.locatorPath) return { parentKind: "Locator", parentPath: socket.locatorPath };
  if (socket?.parentPartId) return { parentKind: "BodyPart", parentPath: socket.parentPartId };
  return { parentKind: "ModelRoot", parentPath: "" };
}

function editableFields(kind, value = null) {
  if (kind === "resource" && value?.typeId === "model") return [
    field("usage", { label: "资源用途", type: "select", options: MODEL_USAGE_OPTIONS, group: "resource", help: "声明模型是主体、武器、动画或通用资源，会影响预览和可选替换目标。" }),
    modelPositionField("importHints.modelWrapperPose.position.x", "位置 X"),
    modelPositionField("importHints.modelWrapperPose.position.y", "位置 Y"),
    modelPositionField("importHints.modelWrapperPose.position.z", "位置 Z"),
    modelRotationField("importHints.modelWrapperPose.eulerHint.x", "旋转 X"),
    modelRotationField("importHints.modelWrapperPose.eulerHint.y", "旋转 Y"),
    modelRotationField("importHints.modelWrapperPose.eulerHint.z", "旋转 Z"),
    modelScaleField("importHints.modelWrapperPose.scale.x", "缩放 X"),
    modelScaleField("importHints.modelWrapperPose.scale.y", "缩放 Y"),
    modelScaleField("importHints.modelWrapperPose.scale.z", "缩放 Z")
  ];
  if (kind === "animationProfile") return [
    identityField("profileId", "Profile ID", animationProfileIdSuggestions(), "稳定动画 Profile ID；装备状态后续会引用它。", "animation"),
    field("displayName", { label: "显示名", group: "animation", help: "给主创看的动画配置名称。" }),
    field("description", { label: "说明", group: "animation", help: "描述这个 Profile 适用的角色、装备或状态。" })
  ];
  if (kind === "animationSlot") return [
    field("slotId", { label: "槽位 ID", type: "select", options: ANIMATION_PROFILE_SLOTS.map(slot => ({ value: slot.slotId, label: slot.displayName })), group: "animationSlot", help: "当前 Profile 内的动画资源槽位。" }),
    field("displayName", { label: "显示名", group: "animationSlot", help: "给主创看的槽位名称。" }),
    field("purpose", { label: "用途说明", group: "animationSlot", help: "说明该槽位用于移动、战斗、基础姿势或后续扩展。" }),
    field("resourceKey", { label: "动画资源", type: "select", options: animationResourceOptions("未选择"), group: "selection", picker: "resource", help: "通过资源选择器选择动画资源；不要手填未知 ResourceKey。" }),
    field("preloadPolicy", { label: "预热分组", type: "select", options: ["AnimationWarmup"], group: "selection", help: "动画资源进入运行时资源计划时使用 AnimationWarmup 分组。" }),
    field("required", { label: "Spawn 必需", type: "select", options: [{ value: "false", label: "否" }, { value: "true", label: "是" }], dataType: "boolean", group: "selection", help: "必需槽位缺失时后续编译可升级为错误。" })
  ];
  if (kind === "part") return [
    identityField("partId", "部位 ID", bodyPartIdSuggestions(), "稳定部位 ID；被碰撞体、挂点和父部位引用。"),
    field("displayName", { label: "显示名", group: "base", help: "给主创看的名称，不参与引用匹配。" }),
    field("partKind", { label: "部位类型", type: "select", options: BODY_PART_KIND_OPTIONS, group: "binding", help: "Bone 跟随骨骼，Primitive 用于简单体，Virtual 用于逻辑部位。" }),
    field("parentPartId", { label: "父部位", type: "select", options: bodyPartOptions("无"), group: "binding", help: "选择身体层级中的父部位。" }),
    bonePathField("bonePath", "代表骨骼路径", "绑定骨骼角色时建议从已有骨骼路径中选择。"),
    locatorField("locatorId", "代表 Locator", "骨骼别名、locator 或 primitive anchor；会被局部父空间引用。"),
    datalistField("defaultHitZoneId", "默认命中区域", hitZoneOptions(), "部位默认命中区域，可被碰撞体覆盖。", "combat"),
    datalistField("reactionGroupId", "受击反应组", reactionGroupOptions(), "受击反应分组，建议复用已有分组。", "combat"),
    tagsField("tags", "可选标签", tagOptions(), "标签用于项目自定义筛选和生成规则；不影响基础引用关系。")
  ];
  if (kind === "collider") return [
    field("shape", { label: "碰撞形状", type: "select", options: ["Capsule", "Box", "Sphere"], group: "base", help: "当前运行时导入只使用 Capsule / Box / Sphere。" }),
    field("partId", { label: "身体部位", type: "select", options: bodyPartOptions(), group: "binding", help: "选择碰撞体绑定并跟随的身体部位。" }),
    datalistField("hitZoneId", "命中区域", hitZoneOptions(), "命中区域 ID；通常选择部位默认 hit zone。"),
    poseParentKindField("localPose.parentKind"),
    poseParentPathField("localPose.parentPath", "父空间路径", value?.localPose?.parentKind),
    positionField("localPose.position.x", "中心 X", "localPose"),
    positionField("localPose.position.y", "中心 Y", "localPose"),
    positionField("localPose.position.z", "中心 Z", "localPose"),
    rotationField("localPose.eulerHint.x", "旋转 X"),
    rotationField("localPose.eulerHint.y", "旋转 Y"),
    rotationField("localPose.eulerHint.z", "旋转 Z"),
    localScaleField("localPose.scale.x", "局部缩放 X"),
    localScaleField("localPose.scale.y", "局部缩放 Y"),
    localScaleField("localPose.scale.z", "局部缩放 Z"),
    sizeField("size.x", "盒体尺寸 X"),
    sizeField("size.y", "盒体尺寸 Y"),
    sizeField("size.z", "盒体尺寸 Z"),
    positiveField("radius", "半径", { max: 10, step: 0.01, unit: "m", group: "shape" }),
    positiveField("height", "高度", { max: 10, step: 0.01, unit: "m", group: "shape" }),
    integerField("priority", "优先级", { min: 0, max: 1000, group: "base" }),
    field("isWeakPoint", { label: "是否弱点", type: "select", options: [{ value: "false", label: "否" }, { value: "true", label: "是" }], dataType: "boolean", group: "base", help: "弱点会影响命中解析和伤害倍率。" }),
    positiveField("damageMultiplierOverride", "伤害倍率", { max: 100, step: 0.01, group: "base" })
  ];
  if (kind === "socket") return [
    identityField("socketId", "挂点 ID", socketIdSuggestions(), "稳定挂点 ID；武器挂接、轨迹起点/终点会引用它。"),
    field("parentPartId", { label: "父部位", type: "select", options: bodyPartOptions("无"), group: "binding", help: "选择挂点默认跟随的身体部位。" }),
    bonePathField("bonePath", "骨骼路径", "该挂点跟随的骨骼路径；优先从候选中选择。"),
    locatorField("locatorPath", "Locator 路径", "模型内 locator 或导入时约定的挂点路径。"),
    poseParentKindField("localPose.parentKind"),
    poseParentPathField("localPose.parentPath", "父空间路径", value?.localPose?.parentKind),
    positionField("localPose.position.x", "局部位置 X", "localPose"),
    positionField("localPose.position.y", "局部位置 Y", "localPose"),
    positionField("localPose.position.z", "局部位置 Z", "localPose"),
    rotationField("localPose.eulerHint.x", "局部旋转 X"),
    rotationField("localPose.eulerHint.y", "局部旋转 Y"),
    rotationField("localPose.eulerHint.z", "局部旋转 Z"),
    field("usage", { label: "默认用途", type: "select", options: SOCKET_USAGE_OPTIONS, group: "usage", help: "用于过滤可选挂接、自动绑定和校验提示，不是硬性权限；不确定时选 Unknown。" }),
    field("mirrorPairSocketId", { label: "镜像挂点", type: "select", options: socketOptions("无"), group: "usage", help: "左右手或左右侧挂点可互相引用，便于镜像编辑。" }),
    field("handedness", { label: "左右手", type: "select", options: SOCKET_HANDEDNESS_OPTIONS, group: "usage", help: "声明该挂点适用左手、右手、双手或无手性。" }),
    field("sideTag", { label: "侧向标签", type: "select", options: SOCKET_SIDE_OPTIONS, group: "usage", help: "用于区分左、右、前、后或中心侧向。" }),
    tagsField("tags", "可选标签", tagOptions(), "标签用于项目自定义筛选，例如 main/offhand/stow；不确定可以留空。")
  ];
  if (kind === "weapon") return [
    identityField("weaponId", "武器 ID", weaponIdSuggestions(), "稳定武器配置 ID；动画、轨迹和预览资源会围绕它关联。"),
    field("equipSlot", { label: "装备槽", type: "select", options: EQUIP_SLOT_OPTIONS, group: "base", help: "选择武器占用的角色装备槽。" }),
    field("attachSocketId", { label: "绑定挂点", type: "select", options: socketOptions(), group: "binding", help: "选择武器预览和运行时挂接的角色挂点。" }),
    poseParentKindField("localGripPose.parentKind"),
    poseParentPathField("localGripPose.parentPath", "父空间路径", value?.localGripPose?.parentKind),
    positionField("localGripPose.position.x", "握持偏移 X", "localPose"),
    positionField("localGripPose.position.y", "握持偏移 Y", "localPose"),
    positionField("localGripPose.position.z", "握持偏移 Z", "localPose"),
    rotationField("localGripPose.eulerHint.x", "握持旋转 X"),
    rotationField("localGripPose.eulerHint.y", "握持旋转 Y"),
    rotationField("localGripPose.eulerHint.z", "握持旋转 Z"),
    field("previewResourceKey", { label: "预览模型资源", type: "select", options: modelResourceOptions("无"), group: "binding", help: "选择该武器引用的模型资源；清空只解除引用，不删除资源。" }),
    field("traceId", { label: "攻击轨迹 ID", type: "select", options: traceOptions("无"), group: "trace", help: "选择该武器使用的攻击轨迹配置。" }),
    field("traceStartSocketId", { label: "轨迹起点挂点", type: "select", options: socketOptions("继承绑定挂点"), group: "trace", help: "选择轨迹起点挂点；为空时继承绑定挂点。" }),
    field("traceEndSocketId", { label: "轨迹终点挂点", type: "select", options: socketOptions("无"), group: "trace", help: "可选终点挂点，用于更明确的武器攻击段。" }),
    positiveField("traceRadius", "轨迹半径", { max: 5, step: 0.01, unit: "m", group: "trace" }),
    field("traceSampleRule", { label: "轨迹采样规则", type: "select", options: TRACE_SAMPLE_RULE_OPTIONS, group: "trace", help: "选择运行时如何从起点到终点生成攻击命中采样。" })
  ];
  if (kind === "trace") return [
    identityField("traceId", "轨迹 ID", traceIdSuggestions(), "稳定攻击轨迹 ID；武器配置可按 ID 引用。"),
    field("weaponId", { label: "武器 ID", type: "select", options: weaponOptions(), group: "base", help: "选择此轨迹归属的武器配置。" }),
    field("equipSlot", { label: "装备槽", type: "select", options: EQUIP_SLOT_OPTIONS, group: "base", help: "选择该轨迹在角色装备状态中的槽位。" }),
    locatorField("startLocatorPath", "起点 Locator", "轨迹起点 locator；优先选择已有 locator。"),
    locatorField("endLocatorPath", "终点 Locator", "轨迹终点 locator；优先选择已有 locator。"),
    poseParentKindField("startPose.parentKind", "起点父空间"),
    poseParentPathField("startPose.parentPath", "起点父路径", value?.startPose?.parentKind),
    positionField("startPose.position.x", "起点 X", "localPose"),
    positionField("startPose.position.y", "起点 Y", "localPose"),
    positionField("startPose.position.z", "起点 Z", "localPose"),
    rotationField("startPose.eulerHint.x", "起点旋转 X"),
    rotationField("startPose.eulerHint.y", "起点旋转 Y"),
    rotationField("startPose.eulerHint.z", "起点旋转 Z"),
    poseParentKindField("endPose.parentKind", "终点父空间"),
    poseParentPathField("endPose.parentPath", "终点父路径", value?.endPose?.parentKind),
    positionField("endPose.position.x", "终点 X", "localPose"),
    positionField("endPose.position.y", "终点 Y", "localPose"),
    positionField("endPose.position.z", "终点 Z", "localPose"),
    rotationField("endPose.eulerHint.x", "终点旋转 X"),
    rotationField("endPose.eulerHint.y", "终点旋转 Y"),
    rotationField("endPose.eulerHint.z", "终点旋转 Z"),
    positiveField("radius", "轨迹半径", { max: 5, step: 0.01, unit: "m", group: "trace" }),
    field("sampleRule", { label: "采样规则", type: "select", options: TRACE_SAMPLE_RULE_OPTIONS, group: "trace", help: "LineSegment 线段，CapsuleSweep 胶囊扫掠，FixedSamples 固定采样点。" }),
    integerField("fixedSampleCount", "固定采样数", { min: 1, max: 64, group: "trace" }),
    tagsField("actionKeys", "动作 Key", actionKeyOptions(), "选择会触发该轨迹的动作 key。")
  ];
  return [];
}

function withEmptyOption(options, label = "无") {
  return [{ value: "", label }, ...options];
}

function bodyPartOptions(emptyLabel = "") {
  const options = (state.package?.geometry?.bodyParts || [])
    .filter(part => part?.partId)
    .map(part => ({ value: part.partId, label: part.displayName ? `${part.displayName} (${part.partId})` : part.partId }));
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function socketOptions(emptyLabel = "") {
  const options = (state.package?.geometry?.sockets || [])
    .filter(socket => socket?.socketId)
    .map(socket => ({ value: socket.socketId, label: socket.socketId }));
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function traceOptions(emptyLabel = "") {
  const options = (state.package?.geometry?.traces || [])
    .filter(trace => trace?.traceId)
    .map(trace => ({ value: trace.traceId, label: trace.traceId }));
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function modelResourceOptions(emptyLabel = "") {
  const options = getResourceLibraryItems()
    .filter(item => item.kind === "Model")
    .map(item => ({
      value: item.runtimeResourceKey || item.packageResourceKey || item.providerResourceKey || item.resourceKey,
      label: `${item.displayName} (${item.sourceProviderId})`
    }))
    .filter(option => option.value);
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function animationResourceOptions(emptyLabel = "") {
  const options = getResourceLibraryItems()
    .filter(item => item.kind === "Animation")
    .map(item => ({
      value: item.runtimeResourceKey || item.packageResourceKey || item.providerResourceKey || item.resourceKey || item.unityAssetPath,
      label: `${item.displayName} (${item.sourceProviderId})`
    }))
    .filter(option => option.value);
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function animationProfileIdSuggestions() {
  return collectOptions(
    DEFAULT_ANIMATION_PROFILE_ID,
    (state.package?.applicationConfig?.animationProfiles || []).map(profile => profile?.profileId)
  );
}

function weaponOptions(emptyLabel = "") {
  const options = collectOptions(
    (state.package?.geometry?.weaponAttachments || []).map(attachment => attachment?.weaponId),
    (state.package?.geometry?.traces || []).map(trace => trace?.weaponId)
  );
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function bodyPartIdSuggestions() {
  return collectOptions(COMMON_BODY_PART_IDS, (state.package?.geometry?.bodyParts || []).map(part => part?.partId));
}

function socketIdSuggestions() {
  return collectOptions(COMMON_SOCKET_IDS, (state.package?.geometry?.sockets || []).map(socket => socket?.socketId));
}

function weaponIdSuggestions() {
  return collectOptions(
    (state.package?.geometry?.weaponAttachments || []).map(attachment => attachment?.weaponId),
    (state.package?.geometry?.traces || []).map(trace => trace?.weaponId),
    getModelResources(state.package).filter(resource => resource.usage === "weaponModel").map(resource => resource.localId || resource.resourceKey)
  );
}

function traceIdSuggestions() {
  return collectOptions(
    (state.package?.geometry?.traces || []).map(trace => trace?.traceId),
    (state.package?.geometry?.weaponAttachments || []).map(attachment => attachment?.traceId)
  );
}

function bonePathOptions() {
  return collectOptions(
    state.previewBones.flatMap(bone => [bone.path, bone.alias]),
    (state.package?.geometry?.bodyParts || []).map(part => part?.bonePath),
    (state.package?.geometry?.sockets || []).map(socket => socket?.bonePath),
    collectPoseParentPaths("Bone")
  );
}

function locatorPathOptions() {
  return collectOptions(
    (state.package?.geometry?.bodyParts || []).map(part => part?.locatorId),
    (state.package?.geometry?.sockets || []).flatMap(socket => [socket?.locatorPath]),
    (state.package?.geometry?.traces || []).flatMap(trace => [trace?.startLocatorPath, trace?.endLocatorPath]),
    collectPoseParentPaths("Locator")
  );
}

function hitZoneOptions() {
  return collectOptions(
    (state.package?.geometry?.bodyParts || []).map(part => part?.defaultHitZoneId),
    (state.package?.geometry?.colliders || []).map(collider => collider?.hitZoneId)
  );
}

function reactionGroupOptions() {
  return collectOptions(
    COMMON_REACTION_GROUPS,
    (state.package?.geometry?.bodyParts || []).map(part => part?.reactionGroupId)
  );
}

function tagOptions() {
  return collectOptions(
    COMMON_TAGS,
    (state.package?.geometry?.bodyParts || []).flatMap(part => part?.tags || []),
    (state.package?.geometry?.sockets || []).flatMap(socket => socket?.tags || []),
    (state.package?.resourceCatalog?.entries || []).flatMap(resource => resource?.tags || [])
  );
}

function actionKeyOptions() {
  return collectOptions(COMMON_ACTION_KEYS, (state.package?.geometry?.traces || []).flatMap(trace => trace?.actionKeys || []));
}

function collectPoseParentPaths(parentKind) {
  const geometry = state.package?.geometry || {};
  const poses = [
    ...(geometry.colliders || []).map(item => item?.localPose),
    ...(geometry.sockets || []).map(item => item?.localPose),
    ...(geometry.weaponAttachments || []).map(item => item?.localGripPose),
    ...(geometry.traces || []).flatMap(item => [item?.startPose, item?.endPose])
  ];
  return poses
    .filter(pose => pose?.parentKind === parentKind)
    .map(pose => pose.parentPath);
}

function collectOptions(...sources) {
  const seen = new Set();
  const values = [];
  for (const source of sources) {
    const items = Array.isArray(source) ? source : [source];
    for (const item of items) {
      if (item == null) continue;
      const value = String(item).trim();
      if (!value || seen.has(value)) continue;
      seen.add(value);
      values.push({ value, label: value });
    }
  }
  values.sort((a, b) => a.label.localeCompare(b.label));
  return values;
}

function identityField(path, label, suggestions, help, group = "base") {
  return datalistField(path, label, suggestions, help, group);
}

function datalistField(path, label, suggestions, help, group = "base") {
  const first = normalizeOptions(suggestions)[0];
  return field(path, {
    label,
    group,
    suggestions,
    placeholder: first ? first.value : "",
    help
  });
}

function bonePathField(path, label, help) {
  const spec = datalistField(path, label, bonePathOptions(), help, "binding");
  spec.picker = "bone";
  return spec;
}

function locatorField(path, label, help) {
  return datalistField(path, label, locatorPathOptions(), help, "binding");
}

function field(path, options = {}) {
  return {
    path,
    type: options.type || "text",
    dataType: options.dataType || options.type || "text",
    resourceFieldRole: options.resourceFieldRole || "",
    label: options.label || path,
    options: options.options || null,
    suggestions: options.suggestions || null,
    group: options.group || "base",
    min: options.min,
    max: options.max,
    step: options.step,
    unit: options.unit || "",
    fallback: options.fallback,
    placeholder: options.placeholder || "",
    help: options.help || "",
    picker: options.picker || ""
  };
}

function positionField(path, label, group = "localPose") {
  return field(path, { label, type: "number", min: -10, max: 10, step: 0.01, unit: "m", group, fallback: 0 });
}

function modelPositionField(path, label) {
  return positionField(path, label, "modelTransform");
}

function modelRotationField(path, label) {
  return rotationField(path, label, "modelTransform");
}

function modelScaleField(path, label) {
  return field(path, { label, type: "number", min: 0.001, max: 100, step: 0.01, group: "modelTransform", fallback: 1 });
}

function rotationField(path, label, group = "localPose") {
  return field(path, { label, type: "number", min: -360, max: 360, step: 1, unit: "deg", group, fallback: 0 });
}

function localScaleField(path, label, group = "localPose") {
  return field(path, { label, type: "number", min: 0.001, max: 100, step: 0.01, group, fallback: 1 });
}

function poseParentKindField(path, label = "父空间类型") {
  return field(path, {
    label,
    type: "select",
    options: POSE_PARENT_KIND_OPTIONS,
    group: "poseParent",
    help: "选择局部位置、旋转和缩放相对的父空间；切换后父路径会改成对应的可选项。"
  });
}

function poseParentPathField(path, label = "父空间路径", parentKind = "") {
  if (parentKind === "BodyPart") {
    return field(path, { label, type: "select", options: bodyPartOptions("无"), group: "poseParent", help: "选择该局部姿态相对的身体部位。" });
  }
  if (parentKind === "Socket") {
    return field(path, { label, type: "select", options: socketOptions("无"), group: "poseParent", help: "选择该局部姿态相对的挂点。" });
  }
  if (parentKind === "Bone") {
    return field(path, { label, group: "poseParent", suggestions: bonePathOptions(), help: "选择该局部姿态相对的骨骼路径。", picker: "bone" });
  }
  if (parentKind === "Locator") {
    return field(path, { label, group: "poseParent", suggestions: locatorPathOptions(), help: "选择该局部姿态相对的 locator 路径。" });
  }
  return field(path, { label, group: "poseParent", help: "ModelRoot / SkeletonRoot / WorldPreview 通常不需要填写路径。" });
}

function tagsField(path, label, options = tagOptions(), help = "", group = "base") {
  return field(path, { label, type: "multiSelect", dataType: "stringList", options, group, help });
}

function sizeField(path, label) {
  return positiveField(path, label, { max: 10, step: 0.01, unit: "m", group: "shape" });
}

function positiveField(path, label, options = {}) {
  return field(path, {
    label,
    type: "number",
    min: options.min ?? 0,
    max: options.max ?? 100,
    step: options.step ?? 0.01,
    unit: options.unit || "",
    group: options.group || "base",
    fallback: options.fallback ?? 0
  });
}

function integerField(path, label, options = {}) {
  return field(path, {
    label,
    type: "number",
    min: options.min ?? 0,
    max: options.max ?? 1000,
    step: 1,
    group: options.group || "base",
    fallback: options.fallback ?? 0
  });
}

function renderFieldSections(target, fields) {
  const groups = [];
  for (const fieldSpec of fields) {
    const spec = normalizeFieldSpec(fieldSpec);
    let group = groups.find(item => item.key === spec.group);
    if (!group) {
      group = { key: spec.group, fields: [] };
      groups.push(group);
    }
    group.fields.push(spec);
  }

  return groups.map(group => {
    const heading = renderFieldSectionHeading(target, group.key);
    return `<section class="field-section">${heading}<div class="field-grid">${group.fields.map(fieldSpec => renderField(target, fieldSpec)).join("")}</div></section>`;
  }).join("");
}

function renderFieldSectionHeading(target, groupKey) {
  const label = FIELD_GROUP_LABELS[groupKey] || groupKey || "属性";
  const canResetModelPose = target.kind === "resource" && target.value?.typeId === "model" && groupKey === "modelTransform";
  const action = canResetModelPose
    ? `<button type="button" data-inspector-action="resetModelWrapperPose" title="重置模型包裹节点的位置、旋转和缩放">重置变换</button>`
    : "";
  return `<div class="field-section-head"><h3>${escapeHtml(label)}</h3>${action}</div>`;
}

function renderField(target, fieldSpec) {
  const spec = normalizeFieldSpec(fieldSpec);
  const value = getNested(target.value, spec.path);
  const label = spec.unit ? `${spec.label} (${spec.unit})` : spec.label;
  if (spec.type === "select") {
    const normalized = typeof value === "boolean" ? String(value) : (value || "");
    const options = spec.options || [];
    const select = `<select data-field="${escapeHtml(spec.path)}" data-type="${escapeHtml(spec.dataType)}"${renderPickerAttribute(spec)}${renderTitleAttribute(spec)}>${options.map(option => renderSelectOption(option, normalized)).join("")}</select>`;
    if (spec.picker === "resource") {
      const picker = `<button type="button" class="picker-button" data-picker-action="openResourcePicker" data-picker-field="${escapeHtml(spec.path)}">选择</button>`;
      return `<div class="field field-wide"><label>${escapeHtml(label)}</label><div class="field-control">${select}${picker}</div>${renderFieldHint(spec)}</div>`;
    }
    return `<div class="field"><label>${escapeHtml(label)}</label>${select}${renderFieldHint(spec)}</div>`;
  }
  if (spec.type === "multiSelect") {
    const selected = new Set(Array.isArray(value) ? value.map(item => String(item)) : String(value || "").split(",").map(item => item.trim()).filter(Boolean));
    const options = normalizeOptions(spec.options || []);
    return `<div class="field field-wide"><label>${escapeHtml(label)}</label><div class="choice-list">${options.map(option => renderCheckboxOption(spec, option, selected)).join("")}</div>${renderFieldHint(spec)}</div>`;
  }
  const inputType = spec.type === "number" ? "number" : "text";
  const datalistId = spec.suggestions ? `list-${safeDomId(spec.path)}` : "";
  const attrs = [
    `type="${escapeHtml(inputType)}"`,
    `data-field="${escapeHtml(spec.path)}"`,
    `data-type="${escapeHtml(spec.dataType)}"`,
    `data-fallback="${escapeHtml(String(spec.fallback ?? (spec.type === "number" ? 0 : "")))}"`
  ];
  const picker = renderPickerAttribute(spec);
  if (picker) attrs.push(picker);
  const title = renderTitleAttribute(spec);
  if (title) attrs.push(title);
  if (spec.placeholder) attrs.push(`placeholder="${escapeHtml(spec.placeholder)}"`);
  if (datalistId) attrs.push(`list="${escapeHtml(datalistId)}"`);
  if (spec.min !== undefined) attrs.push(`min="${escapeHtml(String(spec.min))}"`, `data-min="${escapeHtml(String(spec.min))}"`);
  if (spec.max !== undefined) attrs.push(`max="${escapeHtml(String(spec.max))}"`, `data-max="${escapeHtml(String(spec.max))}"`);
  if (spec.step !== undefined) attrs.push(`step="${escapeHtml(String(spec.step))}"`);
  if (spec.type === "number") attrs.push(`inputmode="decimal"`);
  const displayValue = formatFieldValue(value, spec.dataType);
  if (spec.picker === "bone") {
    const isOpen = state.bonePickerOpen && state.activeBoneFieldPath === spec.path;
    const className = isOpen ? "field field-wide" : "field";
    const picker = `<button type="button" class="picker-button" data-picker-action="openBonePicker" data-picker-field="${escapeHtml(spec.path)}">选择</button>`;
    return `<div class="${className}"><label>${escapeHtml(label)}</label><div class="field-control"><input ${attrs.join(" ")} value="${escapeHtml(displayValue)}">${picker}</div>${renderDatalist(datalistId, spec.suggestions)}${renderFieldHint(spec)}${isOpen ? renderBonePicker() : ""}</div>`;
  }
  return `<div class="field"><label>${escapeHtml(label)}</label><input ${attrs.join(" ")} value="${escapeHtml(displayValue)}">${renderDatalist(datalistId, spec.suggestions)}${renderFieldHint(spec)}</div>`;
}

function normalizeFieldSpec(fieldSpec) {
  if (!Array.isArray(fieldSpec)) return fieldSpec;
  const [path, type = "text", options = null] = fieldSpec;
  return field(path, {
    type,
    dataType: path === "isWeakPoint" ? "boolean" : type,
    options,
    label: path
  });
}

function renderSelectOption(option, normalizedValue) {
  const normalized = typeof option === "object" && option !== null
    ? { value: option.value, label: option.label || option.value }
    : { value: option, label: option };
  return `<option value="${escapeHtml(normalized.value)}"${String(normalized.value) === String(normalizedValue) ? " selected" : ""}>${escapeHtml(normalized.label)}</option>`;
}

function renderCheckboxOption(spec, option, selected) {
  const value = String(option.value ?? "");
  const id = `choice-${safeDomId(spec.path)}-${safeDomId(value)}`;
  return `<label class="choice" for="${escapeHtml(id)}"><input id="${escapeHtml(id)}" type="checkbox" value="${escapeHtml(value)}" data-field="${escapeHtml(spec.path)}" data-type="${escapeHtml(spec.dataType)}"${selected.has(value) ? " checked" : ""}><span>${escapeHtml(option.label || value)}</span></label>`;
}

function renderDatalist(id, suggestions) {
  if (!id || !suggestions) return "";
  const options = normalizeOptions(suggestions);
  if (options.length === 0) return "";
  return `<datalist id="${escapeHtml(id)}">${options.map(option => `<option value="${escapeHtml(option.value)}">${escapeHtml(option.label || option.value)}</option>`).join("")}</datalist>`;
}

function renderFieldHint(spec) {
  return spec.help ? `<span class="field-help">${escapeHtml(spec.help)}</span>` : "";
}

function renderTitleAttribute(spec) {
  return spec.help ? ` title="${escapeHtml(spec.help)}"` : "";
}

function renderPickerAttribute(spec) {
  return spec.picker ? ` data-picker="${escapeHtml(spec.picker)}"` : "";
}

function normalizeOptions(options) {
  return (options || []).map(option => typeof option === "object" && option !== null
    ? { value: String(option.value ?? ""), label: String(option.label || option.value || "") }
    : { value: String(option ?? ""), label: String(option ?? "") });
}

function safeDomId(value) {
  return String(value || "field").replace(/[^a-zA-Z0-9_-]+/g, "-");
}

function commitInspectorField(target, input) {
  const value = readInspectorInputValue(input);
  if (value === undefined) return undefined;
  const rawNumber = input.dataset.type === "number" ? Number(input.value) : NaN;
  if (Number.isFinite(rawNumber) && rawNumber !== value) {
    input.value = formatFieldValue(value, "number");
  }
  setNested(target.value, input.dataset.field, value);
  afterInspectorFieldEdited(target, input.dataset.field);
  if (input.dataset.picker === "bone") {
    state.activeBoneFieldPath = input.dataset.field;
    state.highlightedBoneValue = value;
  }
  state.dirty = true;
  renderShellStatus();
  renderViewport();
  if (shouldRefreshInspectorForField(input.dataset.field)) renderInspector();
  if (input.dataset.field === "usage") {
    renderResourceBindingBar();
    renderResourcePicker();
  }
  if (target.kind === "animationSlot") {
    renderAnimationConfigPanel();
    renderResourcePlanPreview();
    renderTree();
  }
  return value;
}

function readInspectorInputValue(input) {
  const type = input.dataset.type || "text";
  if (type === "number") {
    const fallback = Number(input.dataset.fallback || 0);
    const raw = Number(input.value);
    const value = Number.isFinite(raw) ? raw : fallback;
    return clampFieldNumber(value, input);
  }
  if (type === "boolean") return input.value === "true";
  if (type === "stringList" && input.type === "checkbox") {
    return Array.from(input.closest(".field")?.querySelectorAll('input[type="checkbox"][data-field]') || [])
      .filter(item => item.checked)
      .map(item => item.value);
  }
  if (type === "stringList") {
    return input.value
      .split(",")
      .map(item => item.trim())
      .filter(Boolean);
  }
  return input.value;
}

function clampFieldNumber(value, input) {
  let number = value;
  const min = input.dataset.min === undefined ? NaN : Number(input.dataset.min);
  const max = input.dataset.max === undefined ? NaN : Number(input.dataset.max);
  if (Number.isFinite(min)) number = Math.max(min, number);
  if (Number.isFinite(max)) number = Math.min(max, number);
  return number;
}

function formatFieldValue(value, type) {
  if (value == null) return "";
  if (type === "number") {
    const number = Number(value);
    return Number.isFinite(number) ? String(Number(number.toFixed(6))) : "";
  }
  if (type === "stringList") {
    return Array.isArray(value) ? value.join(", ") : String(value || "");
  }
  return String(value);
}

function afterInspectorFieldEdited(target, path) {
  const posePath = getPosePathFromEulerField(path);
  if (posePath) syncPoseRotationFromEuler(target.value, posePath);
  if (path.endsWith(".parentKind")) applyPoseParentDefault(target, path.slice(0, -".parentKind".length));
  if (target.kind === "animationSlot" && path === "resourceKey") {
    const item = findResourceLibraryItemByKey(target.value.resourceKey);
    if (item) {
      target.value.resourceSelection = createResourceSelectionRef(item, RESOURCE_FIELD_SPECS.animationClip);
      if (target.value.resourceSelection.runtimeResourceKey || target.value.resourceSelection.packageResourceKey) {
        ensureApplicationResourceKey(target.value.resourceKey);
      }
    } else if (!target.value.resourceKey) {
      target.value.resourceSelection = {};
    }
  }
}

function shouldRefreshInspectorForField(path) {
  return path.endsWith(".parentKind") || path === "resourceKey";
}

function applyPoseParentDefault(target, posePath) {
  const pose = getNested(target.value, posePath);
  if (!pose) return;
  if (pose.parentKind === "BodyPart") {
    pose.parentPath = target.value.partId || target.value.parentPartId || firstValue(bodyPartOptions()) || "";
  } else if (pose.parentKind === "Socket") {
    pose.parentPath = target.value.attachSocketId || firstValue(socketOptions()) || "";
  } else if (pose.parentKind === "Bone") {
    pose.parentPath = target.value.bonePath || firstValue(bonePathOptions()) || "";
    state.activeBoneFieldPath = `${posePath}.parentPath`;
    state.bonePickerOpen = true;
    state.highlightedBoneValue = pose.parentPath;
  } else if (pose.parentKind === "Locator") {
    const traceLocator = posePath === "endPose" ? target.value.endLocatorPath : target.value.startLocatorPath;
    pose.parentPath = target.value.locatorPath || target.value.locatorId || traceLocator || firstValue(locatorPathOptions()) || "";
  } else {
    pose.parentPath = "";
  }
}

function firstValue(options) {
  const first = normalizeOptions(options)[0];
  return first ? first.value : "";
}

function resetModelWrapperPose(resource) {
  const pose = ensureModelWrapperPose(resource);
  pose.position = { x: 0, y: 0, z: 0 };
  pose.rotation = { x: 0, y: 0, z: 0, w: 1 };
  pose.scale = { x: 1, y: 1, z: 1 };
  pose.eulerHint = { x: 0, y: 0, z: 0 };
}

function syncModelWrapperRotationFromEuler(resource) {
  const pose = ensureModelWrapperPose(resource);
  syncPoseQuaternion(pose);
}

function getPosePathFromEulerField(path) {
  const marker = ".eulerHint.";
  const index = path.indexOf(marker);
  return index >= 0 ? path.slice(0, index) : "";
}

function syncPoseRotationFromEuler(owner, posePath) {
  const pose = ensureLocalPose(owner, posePath);
  syncPoseQuaternion(pose);
}

function syncPoseQuaternion(pose) {
  if (!pose) return;
  pose.rotation = quaternionFromEulerDegrees(
    pose.eulerHint?.x || 0,
    pose.eulerHint?.y || 0,
    pose.eulerHint?.z || 0
  );
}

function quaternionFromEulerDegrees(xDegrees, yDegrees, zDegrees) {
  const x = degreesToRadians(xDegrees) / 2;
  const y = degreesToRadians(yDegrees) / 2;
  const z = degreesToRadians(zDegrees) / 2;
  const c1 = Math.cos(x);
  const c2 = Math.cos(y);
  const c3 = Math.cos(z);
  const s1 = Math.sin(x);
  const s2 = Math.sin(y);
  const s3 = Math.sin(z);
  const quaternion = {
    x: s1 * c2 * c3 + c1 * s2 * s3,
    y: c1 * s2 * c3 - s1 * c2 * s3,
    z: c1 * c2 * s3 + s1 * s2 * c3,
    w: c1 * c2 * c3 - s1 * s2 * s3
  };
  const length = Math.hypot(quaternion.x, quaternion.y, quaternion.z, quaternion.w) || 1;
  return {
    x: Number((quaternion.x / length).toFixed(8)),
    y: Number((quaternion.y / length).toFixed(8)),
    z: Number((quaternion.z / length).toFixed(8)),
    w: Number((quaternion.w / length).toFixed(8))
  };
}

function renderDiagnostics() {
  const validationIssues = state.validation?.issues || [];
  const gate = state.compileResult?.gateReport;
  const gateIssues = gate?.issues || [];
  const status = state.compileResult?.status || (validationIssues.length ? "Validation" : "Ready");
  const rows = [`<div class="status-line"><strong>Status</strong><span class="badge ${isImportBlocked() ? "error" : "ok"}">${escapeHtml(status)}</span></div>`];
  if (gate) {
    rows.push(`<div class="meta">source=${escapeHtml(state.compileResult.hashes?.sourcePackageHash || "-")} mapping=${escapeHtml(state.compileResult.hashes?.resourceMappingHash || "-")}</div>`);
  }
  const issues = [...validationIssues, ...gateIssues];
  if (issues.length === 0) {
    rows.push(`<div class="empty">暂无诊断。</div>`);
  } else {
    for (const issue of issues) {
      const cls = issue.severity === "Error" || issue.gate === "ImportBlocked" || issue.gate === "ExportBlocked" ? "error" : "warning";
      const issuePath = normalizeIssuePath(issue.sourceObjectPath || issue.sourcePath || "");
      rows.push(`<div class="diagnostic-item ${cls}"><strong>${escapeHtml(issue.code || issue.gate || "issue")}</strong><div class="meta">${escapeHtml(issue.message || "")}</div><div class="meta">${escapeHtml(issue.sourceObjectPath || issue.sourcePath || "")}</div>${issuePath ? `<button type="button" data-jump="${escapeHtml(issuePath)}">定位</button>` : ""}</div>`);
    }
  }
  el.diagnostics.innerHTML = rows.join("");
  el.diagnostics.querySelectorAll("button[data-jump]").forEach(button => {
    button.addEventListener("click", () => selectPath(button.dataset.jump));
  });
  renderShellStatus();
}

function renderImportStatus() {
  const report = getUnityImportReport();
  const unityEntries = getUnityCatalogEntries();
  const importedCount = unityEntries.filter(entry => String(entry.importStatus || entry.providerData?.importStatus || "").toLowerCase() === "imported").length;
  const catalogSummary = state.unityResourceCatalog
    ? `<div class="status-line"><strong>Unity Catalog</strong><span class="badge ok">${unityEntries.length} entries / imported ${importedCount}</span></div><div class="meta">${escapeHtml(state.unityResourceCatalogPath || "-")}</div>`
    : `<div class="status-line"><strong>Unity Catalog</strong><span class="badge warn">未读取</span></div><div class="meta">${escapeHtml(state.unityResourceCatalogPath || "Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/unity_resource_catalog.json")}</div>`;
  if (!report) {
    el.importStatus.innerHTML = `${catalogSummary}<div class="empty">暂无 Unity 导入报告。</div>`;
    return;
  }
  const operations = report.operations || [];
  el.importStatus.innerHTML = [
    catalogSummary,
    `<div class="status-line"><strong>${escapeHtml(report.status || (state.importResult.success ? "Ready" : "Unknown"))}</strong><span class="badge">${escapeHtml(report.targetRootPath || state.importResult.reportOut || "-")}</span></div>`,
    `<div class="meta">added=${report.addedCount ?? "-"} updated=${report.updatedCount ?? "-"} skipped=${report.skippedCount ?? "-"} conflicts=${report.conflictCount ?? "-"}</div>`,
    `<div class="meta">report=${escapeHtml(report.reportPath || state.importResult.reportOut || "-")}</div>`,
    ...operations.slice(0, 8).map(op => `<div class="operation-item"><strong>${escapeHtml(op.action || op.kind || "operation")}</strong><span class="sync-badge ${escapeHtml(getUnityStatusTone(op.action || op.kind || ""))}">${escapeHtml(op.kind || "operation")}</span><div class="meta">${escapeHtml(op.targetPath || op.sourcePath || "")}</div>${op.message ? `<div class="meta">${escapeHtml(op.message)}</div>` : ""}</div>`)
  ].join("");
}

async function savePackage() {
  if (!state.canWrite) {
    state.message = "静态预览不能保存。请启动 Authoring server。";
    renderShellStatus();
    return;
  }
  const response = await fetch(`/api/character/save?package=${encodeURIComponent(state.packageRelative)}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ package: state.package })
  });
  if (!response.ok) {
    state.message = await response.text();
    renderShellStatus();
    return;
  }
  const data = await response.json();
  state.package = data.package;
  state.validation = data.validation;
  await loadResourceLibraryAndPlan();
  state.dirty = false;
  state.message = "资源包已保存并完成校验。";
  render();
}

async function importModel(file, options = {}) {
  if (!state.canWrite) {
    state.message = "静态预览不能导入模型。请启动 Authoring server。";
    renderShellStatus();
    return;
  }

  const extension = file.name.split(".").pop()?.toLowerCase() || "";
  if (!["glb", "gltf", "fbx"].includes(extension)) {
    state.message = "仅支持导入 .glb、.gltf 或 .fbx 模型。";
    renderShellStatus();
    return;
  }

  const role = el.modelImportRole.value;
  const roleInfo = getModelImportRole();
  const replacing = Boolean(options.resourceKey);
  state.message = extension === "fbx"
    ? `${replacing ? "正在替换选中资源" : roleInfo.pending}，并转换 FBX：${file.name}`
    : `${replacing ? "正在替换选中资源" : roleInfo.pending}：${file.name}`;
  renderShellStatus();
  try {
    const bytesBase64 = await readFileAsBase64(file);
    const response = await fetch(`/api/character/import-model?package=${encodeURIComponent(state.packageRelative)}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        fileName: file.name,
        role,
        resourceKey: options.resourceKey || "",
        bytesBase64
      })
    });
    if (!response.ok) {
      state.message = await response.text();
      renderShellStatus();
      return;
    }
    const data = await response.json();
    state.package = data.package;
    state.validation = data.validation || data.package?.validationReport || { issues: [] };
    state.importResult = data.importReport || state.importResult;
    state.dirty = false;
    state.canWrite = Boolean(data.canWrite);
    state.apiAvailable = true;
    state.selectedPath = options.resourceKey
      ? `resources/${options.resourceKey}`
      : findImportedModelPath(role, data.package, file.name) || state.selectedPath;
    state.message = extension === "fbx"
      ? `${replacing ? "选中资源已替换" : roleInfo.done}，FBX 已转换为 GLB：${file.name}`
      : `${replacing ? "选中资源已替换" : roleInfo.done}：${file.name}`;
    render();
  } catch (error) {
    state.message = `模型导入失败：${error instanceof Error ? error.message : String(error)}`;
    renderShellStatus();
  }
}

function getSelectedModelResource() {
  const selectedKey = state.selectedPath?.startsWith("resources/") ? state.selectedPath.slice(10) : "";
  if (!selectedKey) return null;
  return getModelResources(state.package).find(resource => resource.resourceKey === selectedKey) || null;
}

function readFileAsBase64(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.addEventListener("load", () => {
      const value = String(reader.result || "");
      resolve(value.includes(",") ? value.slice(value.indexOf(",") + 1) : value);
    });
    reader.addEventListener("error", () => reject(reader.error || new Error("File read failed.")));
    reader.readAsDataURL(file);
  });
}

function findImportedModelPath(role, pkg, sourceFileName) {
  const resources = pkg?.resourceCatalog?.entries || [];
  if (role === "body") {
    const entry = resources.find(resource => resource.usage === "characterModel" || resource.localId === "model.body");
    return entry?.resourceKey ? `resources/${entry.resourceKey}` : "geometry/body";
  }

  if (role === "mainHand" || role === "offHand") {
    const attachment = (pkg?.geometry?.weaponAttachments || []).find(item => item.equipSlot === role);
    if (attachment?.previewResourceKey && resources.some(resource => resource.resourceKey === attachment.previewResourceKey)) {
      return `resources/${attachment.previewResourceKey}`;
    }
    return attachment?.weaponId ? `geometry/weapon_attachments/${attachment.weaponId}` : "";
  }

  const bySource = resources.find(resource => resource.provenance?.sourceFile === sourceFileName);
  if (bySource?.resourceKey) return `resources/${bySource.resourceKey}`;
  return "";
}

async function compilePackage() {
  const data = await readJson(`/api/character/compile?package=${encodeURIComponent(state.packageRelative)}&checkHashes=false`, null);
  if (!data) {
    state.message = "预检失败，或 Authoring server 不可用。";
    renderShellStatus();
    return;
  }
  state.compileResult = data;
  state.resourcePlan = normalizeResourcePlanPayload(data.resourcePlan || data.characterResourcePlan || data.plan) || state.resourcePlan;
  state.message = `Prefab 重建预检状态：${data.status || "Unknown"}`;
  renderResourcePlanPreview();
  renderDiagnostics();
}

async function importUnity() {
  if (isImportBlocked()) return;
  try {
    const response = await fetch(`/api/character/import-unity?package=${encodeURIComponent(state.packageRelative)}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ package: state.packageRelative, unityRoot: "Assets/MxFrameworkGenerated/CharacterPackages", checkHashes: false, dryRun: false })
    });
    state.importResult = await response.json();
    await loadUnityResourceCatalog();
    await loadResourceLibraryAndPlan();
    state.message = response.ok && state.importResult.success ? "Unity 导入完成，报告和同步状态已刷新。" : "Unity 导入失败。";
    renderShellStatus();
    renderResourceBindingBar();
    renderResourcePicker();
    renderResourcePlanPreview();
    renderInspector();
    renderImportStatus();
  } catch (error) {
    state.message = `Unity 导入请求失败：${error instanceof Error ? error.message : String(error)}`;
    renderShellStatus();
  }
}

async function copyReport() {
  const text = JSON.stringify({
    validation: state.validation,
    compile: state.compileResult,
    import: state.importResult,
    resourceLibrary: state.resourceLibrary || { items: getResourceLibraryItems().map(item => ({ libraryItemId: item.libraryItemId, stableId: item.stableId, kind: item.kind, usage: item.usage, runtimeAvailability: item.runtimeAvailability, referenceCount: item.referenceCount })) },
    resourcePlan: getCharacterResourcePlan(),
    unityResourceCatalogPath: state.unityResourceCatalogPath,
    unityResourceCatalog: state.unityResourceCatalog
  }, null, 2);
  await navigator.clipboard?.writeText(text);
  state.message = "诊断、Unity 导入和同步报告已复制。";
  renderShellStatus();
}

function selectPath(path) {
  if (!path) return;
  state.selectedPath = path;
  const target = findTarget(path);
  state.activeBoneFieldPath = getDefaultBoneFieldPath(target);
  state.bonePickerOpen = false;
  state.resourcePickerField = null;
  state.resourcePickerQuery = null;
  state.highlightedBoneValue = state.activeBoneFieldPath ? (getNested(target.value, state.activeBoneFieldPath) || "") : "";
  renderTree();
  renderResourceBindingBar();
  renderResourcePicker();
  renderViewport();
  renderInspector();
}

function findTarget(path) {
  const pkg = state.package || {};
  const g = pkg.geometry || {};
  if (path === "manifest") return target("manifest", "Manifest", pkg.manifest);
  if (path === "resources") return target("resources", "Resource Catalog", pkg.resourceCatalog);
  if (path.startsWith("resources/")) return target("resource", path.slice(10), (pkg.resourceCatalog?.entries || []).find(x => x.resourceKey === path.slice(10)));
  if (path === "config") return target("config", "Character Application", pkg.applicationConfig);
  if (path === "config/animation") return target("animationConfig", "Animation Profiles", pkg.applicationConfig?.animationProfiles || []);
  if (path.startsWith("config/animation/")) {
    const parts = path.split("/");
    const profileId = decodeURIComponent(parts[2] || "");
    const profile = (pkg.applicationConfig?.animationProfiles || []).find(item => item?.profileId === profileId) || getDefaultAnimationProfile();
    if (parts.length <= 3) return target("animationProfile", profile?.profileId || "Animation Profile", profile, { profileId: profile?.profileId || "" });
    const slotId = decodeURIComponent(parts[4] || "");
    const slot = (profile?.slots || []).find(item => item?.slotId === slotId);
    return target("animationSlot", slot?.displayName || slotId || "Animation Slot", slot, { profileId: profile?.profileId || "" });
  }
  if (path === "geometry/body") return target("body", "Body Geometry", g.bodyProfile);
  if (path.startsWith("geometry/body_parts/")) return target("part", path.split("/").pop(), (g.bodyParts || []).find(x => x.partId === path.split("/").pop()));
  if (path.startsWith("geometry/colliders/")) return target("collider", path.split("/").pop(), (g.colliders || []).find(x => x.colliderId === path.split("/").pop()));
  if (path.startsWith("geometry/sockets/")) return target("socket", path.split("/").pop(), (g.sockets || []).find(x => x.socketId === path.split("/").pop()));
  if (path.startsWith("geometry/weapon_attachments/")) return target("weapon", path.split("/").pop(), (g.weaponAttachments || []).find(x => x.weaponId === path.split("/").pop()));
  if (path.startsWith("geometry/traces/")) return target("trace", path.split("/").pop(), (g.traces || []).find(x => x.traceId === path.split("/").pop()));
  if (path.startsWith("validation/issues/")) return target("issue", path.split("/").pop(), (state.validation?.issues || [])[Number(path.split("/").pop())]);
  return target("", path, null);
}

function target(kind, label, value, extra = {}) {
  return { kind, label, value, ...extra };
}

function normalizeIssuePath(sourceObjectPath) {
  if (!sourceObjectPath) return "validation";
  const path = String(sourceObjectPath);
  if (path === "characterStableId" || path === "manifest" || path.endsWith("manifest.json")) return "manifest";
  if (path.startsWith("geometry/bodyColliders/")) return path.replace("geometry/bodyColliders/", "geometry/colliders/");
  if (path.startsWith("geometry/body_colliders/")) return path.replace("geometry/body_colliders/", "geometry/colliders/");
  if (path.startsWith("geometry/weaponAttachments/")) return path.replace("geometry/weaponAttachments/", "geometry/weapon_attachments/");
  if (path.startsWith("geometry/weapon_attachments/")) return path;
  if (path.startsWith("geometry/sockets/") || path.startsWith("geometry/traces/") || path.startsWith("geometry/colliders/")) return path;
  if ((state.package?.resourceCatalog?.entries || []).some(entry => entry.resourceKey === path)) return `resources/${path}`;
  return path.startsWith("validation/") ? path : "validation";
}

function scaleX(x = 0) {
  return 50 + Number(x || 0) * 42;
}

function scaleY(y = 0) {
  return 90 - Number(y || 0) * 39;
}

function getNested(obj, path) {
  return path.split(".").reduce((acc, key) => acc == null ? undefined : acc[key], obj);
}

function setNested(obj, path, value) {
  const parts = path.split(".");
  let cursor = obj;
  for (let i = 0; i < parts.length - 1; i++) {
    if (cursor[parts[i]] == null) cursor[parts[i]] = {};
    cursor = cursor[parts[i]];
  }
  cursor[parts[parts.length - 1]] = value;
}

function isImportBlocked() {
  const gate = state.compileResult?.gateReport;
  if (gate?.importBlocked || gate?.exportBlocked) return true;
  const issues = [
    ...(state.validation?.issues || []),
    ...(gate?.issues || [])
  ];
  return issues.some(issue => issue.gate === "ImportBlocked" || issue.gate === "ExportBlocked");
}

async function readJson(path, fallback = null) {
  try {
    const response = await fetch(path, { cache: "no-store" });
    if (!response.ok) return fallback;
    return await response.json();
  } catch {
    return fallback;
  }
}

async function postJson(path, body, fallback = null) {
  try {
    const response = await fetch(path, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    if (!response.ok) return fallback;
    return await response.json();
  } catch {
    return fallback;
  }
}

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, ch => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "\"": "&quot;",
    "'": "&#39;"
  }[ch]));
}

window.CharacterStudioTest = {
  buildTree,
  normalizeIssuePath,
  editableFields,
  quaternionFromEulerDegrees,
  mapResourceKind,
  inferRuntimeBindingKind,
  evaluateResourceFieldSelection,
  buildFallbackResourcePlan
};
