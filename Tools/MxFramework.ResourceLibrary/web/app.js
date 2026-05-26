const DEFAULT_PACKAGE = "Tools/MxFramework.Authoring/samples/character-iron-vanguard";

const API = {
  packages: "/api/character/packages",
  resources: packageRelative => `/api/authoring/resources?package=${encodeURIComponent(packageRelative)}`,
  resourcePlan: (packageRelative, checkHashes = false) => {
    const suffix = checkHashes ? "&checkHashes=true" : "";
    return `/api/authoring/resources/resource-plan?package=${encodeURIComponent(packageRelative)}${suffix}`;
  },
  inspect: (packageRelative, id) => `/api/authoring/resources/inspect?package=${encodeURIComponent(packageRelative)}&id=${encodeURIComponent(id)}`,
  buildProfile: "/api/authoring/resources/global-build-profile",
  saveBuildProfile: "/api/authoring/resources/global-build-profile/save",
  bundlePlan: packageRelative => `/api/authoring/resources/bundle-plan?package=${encodeURIComponent(packageRelative)}`,
  stageImport: "/api/authoring/resources/stage-import",
  importResource: "/api/authoring/resources/import",
  reimportResource: "/api/authoring/resources/reimport",
  replaceSource: "/api/authoring/resources/replace-source"
};

const PLAN_GROUPS = [
  ["spawnCritical", "SpawnCritical"],
  ["presentationCritical", "PresentationCritical"],
  ["equipmentInitial", "EquipmentInitial"],
  ["animationWarmup", "AnimationWarmup"],
  ["vfxWarmup", "VfxWarmup"],
  ["uiDeferred", "UiDeferred"],
  ["audio", "Audio"]
];

const IMPORT_PRESETS = [
  {
    id: "modelPreview",
    label: "模型预览/通用模型",
    kind: "model",
    usage: "previewMesh",
    role: "preview",
    extensions: ["glb", "gltf", "fbx"]
  },
  {
    id: "characterModel",
    label: "角色主体模型",
    kind: "model",
    usage: "characterModel",
    role: "body",
    extensions: ["glb", "gltf", "fbx"]
  },
  {
    id: "weaponMainHand",
    label: "主手武器模型",
    kind: "model",
    usage: "weaponModel",
    role: "mainHand",
    extensions: ["glb", "gltf", "fbx"]
  },
  {
    id: "weaponOffHand",
    label: "副手武器模型",
    kind: "model",
    usage: "weaponModel",
    role: "offHand",
    extensions: ["glb", "gltf", "fbx"]
  },
  {
    id: "animationClipGroup",
    label: "动画 Clip/Group",
    kind: "animation",
    usage: "animationClipGroup",
    role: "",
    extensions: ["anim", "glb", "gltf", "json"]
  },
  {
    id: "audioCue",
    label: "音频 Cue",
    kind: "audio",
    usage: "audioCue",
    role: "",
    extensions: ["wav", "ogg"]
  },
  {
    id: "texture",
    label: "贴图/图标",
    kind: "texture",
    usage: "texture",
    role: "",
    extensions: ["png", "jpg", "jpeg", "tga"]
  },
  {
    id: "config",
    label: "配置 JSON",
    kind: "config",
    usage: "characterConfig",
    role: "",
    extensions: ["json"]
  }
];

const FILTER_DEFAULTS = {
  search: "",
  kind: "all",
  usage: "all",
  providerId: "all",
  sourceKind: "all",
  importStatus: "all",
  runtimeAvailability: "all",
  profileMembership: "all",
  runtimeReady: "all",
  tag: "all",
  onlyReferenced: false,
  onlyOrphan: false,
  onlyRuntimeLoadable: false,
  onlyDiagnostics: false
};

const PROFILE_STATUS_ORDER = {
  notInProfile: 0,
  draftOnly: 1,
  modifiedInDraft: 2,
  removedInDraft: 3,
  saved: 4
};

const WORKSPACES = ["browse", "profile", "build", "debug"];

const UNITY_WORKBENCH_GUIDANCE = [
  "Unity 菜单执行 Player 资源构建：",
  "1. MxFramework/Resources/Validate Global Resource Build Profile",
  "2. MxFramework/Resources/Build Global Player Resource Catalog",
  "或打开：MxFramework/Resources/Open Global AssetBundle Builder"
].join("\n");

const state = {
  packages: [],
  packageRelative: DEFAULT_PACKAGE,
  resourcesPayload: null,
  resourcePlanPayload: null,
  buildProfilePayload: null,
  buildProfileDraft: null,
  bundlePlanPayload: null,
  selectedResourceKey: "",
  activeWorkspace: "browse",
  activeTab: "overview",
  filters: { ...FILTER_DEFAULTS },
  resourceSort: "name",
  quickFilter: "",
  treeGroupMode: "path",
  selectedTreeNodeId: "",
  expandedTreeNodes: new Set(),
  treeNodeKeys: new Map(),
  inspectCache: new Map(),
  inspectState: { id: "", status: "idle", payload: null, error: "" },
  writeState: { status: "idle", action: "", error: "" },
  checkedResourceKeys: new Set(),
  selectedBundleRuleId: "",
  bundleMemberSearch: "",
  selectedImportPreset: "modelPreview",
  profileFieldHighlight: "",
  errors: [],
  lastActionMessage: "",
  buildProfileDirty: false
};

const el = {};

document.addEventListener("DOMContentLoaded", () => {
  cacheElements();
  const queryPackage = new URLSearchParams(window.location.search).get("package");
  if (queryPackage) {
    state.packageRelative = queryPackage;
  }

  bindEvents();
  loadAll();
});

function cacheElements() {
  for (const id of [
    "serverStatus", "packageSelect", "refreshButton", "openCharacterStudioButton",
    "saveBuildProfileButton", "openUnityWorkbenchButton", "statusStrip", "resourceSummary",
    "searchInput", "kindFilter", "usageFilter", "providerFilter", "sourceFilter", "importFilter",
    "runtimeFilter", "profileMembershipFilter", "runtimeReadyFilter", "tagFilter", "onlyReferenced", "onlyOrphan",
    "onlyRuntimeLoadable", "onlyDiagnostics", "clearFiltersButton", "resourceSortSelect", "activeFiltersBar",
    "advancedFilterBadge", "advancedFilters", "selectVisibleButton",
    "clearCheckedButton", "checkedSummary", "treeGroupModeSelect", "expandTreeButton",
    "collapseTreeButton", "resourceTree", "resourceListHeading", "resourceList",
    "browsePanelTitle", "browsePanelSubtitle", "browseContextBody", "browseContextActions", "browseBatchBar",
    "buildProfileSummary", "buildProfileContent", "profileContextActions", "profileBatchBar",
    "resourcePlanPanel", "planSummary", "planGrid", "bundlePlanSummary", "buildChecklist",
    "bundlePlanContent", "buildDiagnostics", "validateButton", "refreshBundlePlanButton",
    "copyBuildReportButton", "debugPickerSummary", "debugResourcePicker",
    "inspectorStatus", "inspectorContent", "rawJsonSection", "rawJsonContent",
    "resourceImportFileInput", "resourceImportFolderInput", "resourceReplaceFileInput",
    "importPresetSelect", "importResourceButton", "importFolderButton",
    "reimportResourceButton", "replaceSourceButton", "writeActionStatus",
    "copyDetailJsonButton", "copyDiagnosticsJsonButton", "copyStatus"
  ]) {
    el[id] = document.getElementById(id);
  }
  el.workspaceViews = WORKSPACES.map(name => document.getElementById(`workspace${capitalize(name)}`));
  el.workspaceNavButtons = Array.from(document.querySelectorAll(".workspace-nav button[data-workspace]"));
  el.inspectorTabs = Array.from(document.querySelectorAll(".inspector-tabs button[data-tab]"));
}

function bindEvents() {
  el.refreshButton.addEventListener("click", () => loadAll({ keepPackages: false }));
  el.packageSelect.addEventListener("change", event => {
    state.packageRelative = event.target.value;
    state.selectedResourceKey = "";
    state.inspectState = { id: "", status: "idle", payload: null, error: "" };
    state.inspectCache.clear();
    state.errors = [];
    syncPackageQuery();
    loadPackageData();
  });

  el.validateButton.addEventListener("click", runResourceValidation);
  el.openUnityWorkbenchButton.addEventListener("click", showUnityWorkbenchGuidance);
  el.refreshBundlePlanButton.addEventListener("click", () => loadBundlePlan().then(render));
  el.copyBuildReportButton.addEventListener("click", copyBuildReport);

  for (const button of el.workspaceNavButtons) {
    button.addEventListener("click", () => {
      if (button.dataset.workspace === "debug" && getAllDiagnostics().length > 0) {
        state.activeTab = "diagnostics";
      }
      setWorkspace(button.dataset.workspace);
      render();
    });
  }

  const filterBindings = [
    ["searchInput", "search", "input"],
    ["kindFilter", "kind", "change"],
    ["usageFilter", "usage", "change"],
    ["providerFilter", "providerId", "change"],
    ["sourceFilter", "sourceKind", "change"],
    ["importFilter", "importStatus", "change"],
    ["runtimeFilter", "runtimeAvailability", "change"],
    ["profileMembershipFilter", "profileMembership", "change"],
    ["runtimeReadyFilter", "runtimeReady", "change"],
    ["tagFilter", "tag", "change"]
  ];
  for (const [elementId, key, eventName] of filterBindings) {
    el[elementId].addEventListener(eventName, event => {
      state.filters[key] = event.target.value;
      state.quickFilter = "";
      render();
    });
  }
  for (const [elementId, key] of [
    ["onlyReferenced", "onlyReferenced"],
    ["onlyOrphan", "onlyOrphan"],
    ["onlyRuntimeLoadable", "onlyRuntimeLoadable"],
    ["onlyDiagnostics", "onlyDiagnostics"]
  ]) {
    el[elementId].addEventListener("change", event => {
      state.filters[key] = event.target.checked;
      state.quickFilter = "";
      render();
    });
  }
  el.clearFiltersButton.addEventListener("click", () => {
    resetFilters();
    render();
  });

  el.resourceSortSelect.addEventListener("change", event => {
    state.resourceSort = event.target.value;
    renderBrowser();
    renderBrowseContext();
  });

  document.querySelector(".filter-quick-bar")?.addEventListener("click", event => {
    const chip = event.target.closest("[data-quick-filter]");
    if (!chip) return;
    applyQuickFilter(chip.dataset.quickFilter);
    render();
  });

  el.activeFiltersBar.addEventListener("click", event => {
    const chip = event.target.closest("[data-clear-filter]");
    if (!chip) return;
    if (chip.dataset.clearFilter === "__all__") {
      resetFilters();
    } else {
      clearFilterKey(chip.dataset.clearFilter);
    }
    render();
  });
  el.selectVisibleButton.addEventListener("click", selectVisibleResources);
  el.clearCheckedButton.addEventListener("click", () => {
    state.checkedResourceKeys.clear();
    state.lastActionMessage = "已清除资源勾选。";
    render();
  });

  el.treeGroupModeSelect.addEventListener("change", event => {
    state.treeGroupMode = event.target.value;
    state.selectedTreeNodeId = "";
    state.expandedTreeNodes.clear();
    renderBrowser();
  });
  el.expandTreeButton.addEventListener("click", () => {
    expandAllTreeNodes();
    renderBrowser();
  });
  el.collapseTreeButton.addEventListener("click", () => {
    state.expandedTreeNodes.clear();
    if (state.treeGroupMode === "path") state.expandedTreeNodes.add("path::__root__");
    else state.expandedTreeNodes.add("tax::__root__");
    renderBrowser();
  });
  el.resourceTree.addEventListener("click", handleResourceTreeClick);

  el.resourceList.addEventListener("click", event => {
    const checkbox = event.target.closest("input[data-check-resource-key]");
    if (checkbox) {
      toggleCheckedResource(checkbox.dataset.checkResourceKey, checkbox.checked);
      return;
    }
    const button = event.target.closest("button[data-resource-key]");
    if (!button) return;
    selectResource(button.dataset.resourceKey);
  });

  for (const tab of el.inspectorTabs) {
    tab.addEventListener("click", () => {
      state.activeTab = tab.dataset.tab;
      renderInspector();
    });
  }

  el.copyDetailJsonButton.addEventListener("click", copyDetailJson);
  el.copyDiagnosticsJsonButton.addEventListener("click", copyDiagnosticsJson);
  el.importPresetSelect.addEventListener("change", event => {
    state.selectedImportPreset = event.target.value;
    syncImportAccept();
    renderContextActions();
  });
  el.importResourceButton.addEventListener("click", () => {
    el.resourceImportFileInput.value = "";
    el.resourceImportFileInput.click();
  });
  el.importFolderButton.addEventListener("click", () => {
    el.resourceImportFolderInput.value = "";
    el.resourceImportFolderInput.click();
  });
  el.replaceSourceButton.addEventListener("click", () => {
    if (!getSelectedItem()) return;
    el.resourceReplaceFileInput.value = "";
    el.resourceReplaceFileInput.click();
  });
  el.reimportResourceButton.addEventListener("click", reimportSelectedResource);
  el.saveBuildProfileButton.addEventListener("click", saveBuildProfileDraft);
  el.statusStrip.addEventListener("click", event => {
    const button = event.target.closest("[data-status-action]");
    if (!button) return;
    if (button.dataset.statusAction === "open-diagnostics") openDiagnosticsView();
  });

  el.browseContextActions.addEventListener("click", handleContextActionClick);
  el.profileContextActions.addEventListener("click", handleContextActionClick);
  el.browseBatchBar.addEventListener("click", handleContextActionClick);
  el.profileBatchBar.addEventListener("click", handleContextActionClick);
  el.debugResourcePicker.addEventListener("click", event => {
    const button = event.target.closest("button[data-resource-key]");
    if (button) selectResource(button.dataset.resourceKey);
  });
  for (const root of [el.buildProfileContent, el.profileBatchBar]) {
    root.addEventListener("click", event => {
      const bundleButton = event.target.closest("[data-bundle-action]");
      if (bundleButton) {
        handleBundleProfileAction(bundleButton);
        return;
      }
      const applyButton = event.target.closest("[data-profile-batch-apply]");
      if (applyButton) applyBuildProfileBatchFields();
    });
    root.addEventListener("change", event => {
      const toggle = event.target.closest("[data-profile-batch-enabled]");
      if (toggle) syncBuildProfileBatchField(toggle.dataset.profileBatchEnabled, toggle.checked);
      const bundleControl = event.target.closest("[data-bundle-field]");
      if (bundleControl) updateSelectedBundleRuleField(bundleControl.dataset.bundleField, readControlValue(bundleControl), true);
      const control = event.target.closest("[data-profile-field]");
      if (control) updateSelectedBuildProfileField(control.dataset.profileField, control.value, true);
    });
    root.addEventListener("input", event => {
      const memberSearch = event.target.closest("[data-bundle-member-search]");
      if (memberSearch) {
        state.bundleMemberSearch = memberSearch.value;
        renderBuildProfile();
        return;
      }
      const bundleControl = event.target.closest("[data-bundle-field]");
      if (bundleControl) updateSelectedBundleRuleField(bundleControl.dataset.bundleField, readControlValue(bundleControl), false);
      const control = event.target.closest("[data-profile-field]");
      if (control) updateSelectedBuildProfileField(control.dataset.profileField, control.value, false);
    });
  }
  el.resourceImportFileInput.addEventListener("change", event => importResourceFile(event.target.files?.[0]));
  el.resourceImportFolderInput.addEventListener("change", event => importResourceFolder(Array.from(event.target.files || [])));
  el.resourceReplaceFileInput.addEventListener("change", event => replaceSelectedResourceFile(event.target.files?.[0]));
  renderImportPresetOptions();
  syncImportAccept();
}

async function loadAll(options = {}) {
  state.errors = [];
  state.lastActionMessage = "";
  renderLoading();

  if (!options.keepPackages) {
    await loadPackages();
  }
  await loadPackageData();
}

async function loadPackageData() {
  renderLoading();
  await Promise.all([
    loadResources(),
    loadResourcePlan(false),
    loadBuildProfile(),
    loadBundlePlan()
  ]);
  const items = getNormalizedItems();
  const previousSelection = state.selectedResourceKey;
  if (items.length === 0) {
    state.selectedResourceKey = "";
  } else if (!state.selectedResourceKey || !items.some(item => selectionMatchesItem(item, state.selectedResourceKey))) {
    state.selectedResourceKey = items[0].key;
  } else {
    state.selectedResourceKey = items.find(item => selectionMatchesItem(item, state.selectedResourceKey))?.key || state.selectedResourceKey;
  }
  if (previousSelection !== state.selectedResourceKey) {
    state.inspectState = { id: "", status: "idle", payload: null, error: "" };
  }
  if (state.selectedResourceKey) {
    loadInspectForSelection();
  }
  render();
}

async function loadPackages() {
  try {
    const packages = await fetchJson(API.packages);
    if (Array.isArray(packages) && packages.length > 0) {
      state.packages = packages;
      if (!packages.some(pkg => pkg.relative === state.packageRelative)) {
        state.packageRelative = packages[0].relative;
        syncPackageQuery();
      }
      return;
    }
  } catch (error) {
    state.errors.push(apiError("角色包列表", error));
  }

  state.packages = [{ relative: DEFAULT_PACKAGE, packageId: "iron_vanguard", kind: "character" }];
}

async function loadResources() {
  try {
    state.resourcesPayload = await fetchJson(API.resources(state.packageRelative));
  } catch (error) {
    state.resourcesPayload = null;
    state.errors.push(apiError("资源库列表", error));
  }
}

async function loadResourcePlan(checkHashes) {
  try {
    state.resourcePlanPayload = await fetchJson(API.resourcePlan(state.packageRelative, checkHashes));
  } catch (error) {
    state.resourcePlanPayload = null;
    state.errors.push(apiError("resource plan", error));
  }
}

async function loadBuildProfile() {
  try {
    state.buildProfilePayload = await fetchJson(API.buildProfile);
    state.buildProfileDraft = structuredCloneCompat(pick(state.buildProfilePayload, "profile") || {});
    state.buildProfileDirty = false;
  } catch (error) {
    state.buildProfilePayload = null;
    state.buildProfileDraft = null;
    state.buildProfileDirty = false;
    state.errors.push(apiError("Global Build Profile", error));
  }
}

async function loadBundlePlan() {
  try {
    state.bundlePlanPayload = await fetchJson(API.bundlePlan(state.packageRelative));
  } catch (error) {
    state.bundlePlanPayload = null;
    state.errors.push(apiError("Bundle Planner", error));
  }
}

async function runResourceValidation() {
  state.lastActionMessage = "正在运行资源验证...";
  renderStatus();
  await loadResourcePlan(true);
  state.lastActionMessage = "资源验证已完成，详情见 resource plan 和 Diagnostics。";
  render();
}

async function importResourceFile(file) {
  if (!file) return;
  const preset = getSelectedImportPreset();
  if (!isFileSupportedByPreset(file, preset)) {
    const formats = preset.extensions.map(extension => `.${extension}`).join(", ");
    state.lastActionMessage = `${preset.label} 只支持 ${formats}。`;
    render();
    return;
  }

  await executeResourceWrite("import", API.importResource, await buildImportRequest(file, preset, false), "导入资源");
}

async function importResourceFolder(files) {
  const candidates = Array.from(files || []);
  if (candidates.length === 0) return;
  const preset = getSelectedImportPreset();

  state.writeState = { status: "running", action: "stage-import", error: "" };
  state.lastActionMessage = `扫描文件夹：0 / ${candidates.length}，类型 ${preset.label}`;
  render();

  const staging = await stageImportFiles(candidates);
  const stagedItems = asArray(pick(staging, "items")).map((raw, index) => normalizeItem(raw, index));
  const diagnostics = asArray(pick(staging, "diagnostics"));
  const ignored = diagnostics.filter(diagnostic => pick(diagnostic, "code") === "AUTH_RES_IMPORT_IGNORED_FILE").length;
  const importable = stagedItems.filter(item => isImportableStagedItem(item) && isStagedItemSupportedByPreset(candidates, item, preset));
  const skipped = Math.max(0, candidates.length - ignored - importable.length);
  const suffix = formatFolderImportCountSuffix(skipped, ignored, preset);
  if (importable.length === 0) {
    state.writeState = { status: "idle", action: "", error: "" };
    state.lastActionMessage = `文件夹中没有可导入资源${suffix}。`;
    if (diagnostics.length > 0) {
      state.errors.push(apiError("导入预检", new Error(diagnostics.slice(0, 3).map(diagnostic => pick(diagnostic, "message") || pick(diagnostic, "code")).join("; "))));
    }
    render();
    return;
  }

  state.writeState = { status: "running", action: "folder-import", error: "" };
  state.lastActionMessage = `导入文件夹：0 / ${importable.length}${suffix}`;
  render();

  const failures = [];
  let selectedId = "";
  for (let i = 0; i < importable.length; i++) {
    const staged = importable[i];
    const file = findStagedSourceFile(candidates, staged);
    if (!file) {
      failures.push(`${staged.sourcePath || staged.displayName}: 找不到源文件`);
      continue;
    }
    state.lastActionMessage = `导入文件夹：${i + 1} / ${importable.length}${suffix}`;
    renderStatus();
    try {
      const payload = await postJson(API.importResource, {
        package: state.packageRelative,
        ...(await buildImportRequestFromStagedItem(file, staged, preset))
      });
      selectedId = stringValue(pick(payload, "selectedResourceKey", "selectedId")) || selectedId;
    } catch (error) {
      failures.push(`${getImportDisplayPath(file)}: ${error.message}`);
    }
  }

  state.inspectCache.clear();
  if (selectedId) {
    state.selectedResourceKey = selectedId;
  }
  state.writeState = failures.length > 0
    ? { status: "error", action: "folder-import", error: `${failures.length} 个文件导入失败` }
    : { status: "idle", action: "", error: "" };
  state.lastActionMessage = failures.length > 0
    ? `文件夹导入完成：成功 ${importable.length - failures.length}，失败 ${failures.length}${suffix}`
    : `文件夹导入完成：成功 ${importable.length}${suffix}`;
  if (failures.length > 0) {
    state.errors.push(apiError("文件夹导入", new Error(failures.slice(0, 3).join("; "))));
  }
  await loadPackageData();
}

async function reimportSelectedResource() {
  const item = getSelectedItem();
  if (!item) return;
  const id = getResourceWriteId(item);
  if (!window.confirm(`重导资源 ${item.displayName || id}？`)) return;
  await executeResourceWrite("reimport", API.reimportResource, { id }, "重导资源");
}

async function addSelectedToBuildProfile() {
  const item = getSelectedItem();
  const profile = ensureBuildProfileDraft();
  if (!item || !profile) return;
  if (findDraftBuildProfileEntryForItem(item)) {
    setWorkspace("profile");
    return;
  }
  if (!Array.isArray(profile.entries)) profile.entries = [];
  profile.entries.push(buildDraftBuildProfileEntryForItem(item));
  markBuildProfileDirty();
  state.lastActionMessage = "已加入构建 Profile 草稿，保存后生效。";
  setWorkspace("profile");
  render();
}

async function removeSelectedFromBuildProfile() {
  const item = getSelectedItem();
  const profile = getBuildProfile();
  if (!item || !profile || !Array.isArray(profile.entries)) return;
  const before = profile.entries.length;
  profile.entries = profile.entries.filter(entry => !profileEntryMatchesItem(entry, item));
  if (before !== profile.entries.length) markBuildProfileDirty();
  state.lastActionMessage = before === profile.entries.length ? "当前资源不在构建 Profile 中。" : "已从构建 Profile 草稿移除，保存后生效。";
  render();
}

async function addCheckedToBuildProfile() {
  const profile = ensureBuildProfileDraft();
  const checkedItems = getCheckedItems();
  if (!profile || checkedItems.length === 0) return;
  if (!Array.isArray(profile.entries)) profile.entries = [];
  let added = 0;
  for (const item of checkedItems) {
    if (findDraftBuildProfileEntryForItem(item)) continue;
    profile.entries.push(buildDraftBuildProfileEntryForItem(item));
    added++;
  }
  if (added > 0) markBuildProfileDirty();
  state.lastActionMessage = added > 0
    ? `已将 ${added} 个勾选资源加入 Build Profile 草稿，保存后生效。`
    : "已勾选资源都已在 Build Profile 草稿中。";
  if (added > 0) setWorkspace("profile");
  render();
}

async function removeCheckedFromBuildProfile() {
  const profile = getBuildProfile();
  const checkedItems = getCheckedItems();
  if (!profile || !Array.isArray(profile.entries) || checkedItems.length === 0) return;
  const before = profile.entries.length;
  profile.entries = profile.entries.filter(entry => !checkedItems.some(item => profileEntryMatchesItem(entry, item)));
  const removed = before - profile.entries.length;
  if (removed > 0) markBuildProfileDirty();
  state.lastActionMessage = removed > 0
    ? `已将 ${removed} 个勾选资源从 Build Profile 草稿移除，保存后生效。`
    : "已勾选资源当前不在 Build Profile 草稿中。";
  render();
}

function getActionBundleRuleId(button) {
  const container = button.closest(".context-actions, .batch-bar, .bundle-actions, .bundle-members-toolbar") || document;
  const select = container.querySelector("[data-bundle-action-select]");
  return select?.value || state.selectedBundleRuleId || "";
}

function assignSelectedToBundle(bundleRuleId) {
  const item = getSelectedItem();
  if (!item) return;
  const rule = ensureBundleRuleForAssignment(bundleRuleId);
  if (!rule) return;
  assignItemsToBundle([item], rule.id);
}

function assignCheckedToBundle(bundleRuleId) {
  const items = getCheckedItems();
  if (items.length === 0) {
    state.lastActionMessage = "No checked resources to assign.";
    render();
    return;
  }
  const rule = ensureBundleRuleForAssignment(bundleRuleId);
  if (!rule) return;
  assignItemsToBundle(items, rule.id);
}

function clearCheckedBundleAssignments() {
  const checkedItems = getCheckedItems();
  if (checkedItems.length === 0) return;
  let changed = 0;
  for (const item of checkedItems) {
    const entry = findDraftBuildProfileEntryForItem(item);
    if (!entry || !entry.bundleRule) continue;
    entry.bundleRule = "";
    changed++;
  }
  if (changed > 0) markBuildProfileDirty();
  state.lastActionMessage = changed > 0
    ? `Cleared bundle assignment from ${changed} checked resource(s).`
    : "Checked resources did not have bundle assignments.";
  render();
}

function assignItemsToBundle(items, bundleRuleId) {
  const profile = ensureBuildProfileDraft();
  if (!profile || !bundleRuleId) return;
  if (!Array.isArray(profile.entries)) profile.entries = [];
  let changed = 0;
  let added = 0;
  for (const item of items) {
    let entry = findDraftBuildProfileEntryForItem(item);
    if (!entry) {
      entry = buildDraftBuildProfileEntryForItem(item);
      profile.entries.push(entry);
      added++;
    }
    if (entry.bundleRule !== bundleRuleId || entry.deliveryMode !== "internal") {
      entry.bundleRule = bundleRuleId;
      entry.deliveryMode = "internal";
      if (entry.bundleOverrideMode === "forceExternal" || entry.bundleOverrideMode === "exclude") {
        entry.bundleOverrideMode = "none";
      }
      changed++;
    }
  }
  if (changed > 0 || added > 0) markBuildProfileDirty();
  state.selectedBundleRuleId = bundleRuleId;
  state.lastActionMessage = `Assigned ${items.length} resource(s) to bundle ${bundleRuleId}; added ${added} new profile entr${added === 1 ? "y" : "ies"}.`;
  setWorkspace("profile");
  render();
}

function ensureBundleRuleForAssignment(bundleRuleId) {
  const profile = ensureBuildProfileDraft();
  if (!profile) return null;
  if (!Array.isArray(profile.bundleRules)) profile.bundleRules = [];
  let rule = findBundleRuleById(bundleRuleId);
  if (rule) return rule;
  if (bundleRuleId) {
    state.lastActionMessage = `Bundle rule ${bundleRuleId} does not exist. Create it first.`;
    render();
    return null;
  }
  return createBundleRuleFromPrompt();
}

function createBundleRuleFromPrompt() {
  const profile = ensureBuildProfileDraft();
  if (!profile) return null;
  const id = window.prompt("Bundle id, for example character.spawn or ui.start_screen", suggestBundleRuleId());
  if (!id) return null;
  const normalizedId = normalizeBundleRuleId(id);
  if (!normalizedId) {
    state.lastActionMessage = "Bundle id is invalid.";
    render();
    return null;
  }
  if (findBundleRuleById(normalizedId)) {
    state.selectedBundleRuleId = normalizedId;
    return findBundleRuleById(normalizedId);
  }
  const bundleNameDefault = `global.${normalizeBundleNameSegment(normalizedId)}.assetbundle`;
  const bundleName = window.prompt("Bundle file name", bundleNameDefault) || bundleNameDefault;
  const rule = {
    id: normalizedId,
    bundleName: normalizeBundleName(bundleName),
    explicitKeys: [],
    matchLabels: [],
    matchDomains: [],
    matchPackageIds: [],
    compression: "lz4",
    buildTarget: "ActiveBuildTarget",
    includeDependencies: true,
    allowEmpty: false,
    providerData: {}
  };
  profile.bundleRules.push(rule);
  state.selectedBundleRuleId = rule.id;
  markBuildProfileDirty();
  state.lastActionMessage = `Created bundle ${rule.id}.`;
  render();
  return rule;
}

function applyBuildProfileBatchFields() {
  const checkedItems = getCheckedItems();
  const batch = readBuildProfileBatchFields();
  if (checkedItems.length === 0) {
    state.lastActionMessage = "未勾选资源，批量字段编辑未执行。";
    render();
    return;
  }
  if (batch.length === 0) {
    state.lastActionMessage = "没有启用可应用的批量字段；空输入不会覆盖现有值。";
    render();
    return;
  }

  let applied = 0;
  let skipped = 0;
  let changed = 0;
  for (const item of checkedItems) {
    const entry = findDraftBuildProfileEntryForItem(item);
    if (!entry) {
      skipped++;
      continue;
    }
    let entryChanged = false;
    for (const field of batch) {
      if (buildProfileBatchValuesEqual(entry[field.key], field.value)) continue;
      entry[field.key] = cloneBuildProfileBatchValue(field.value);
      entryChanged = true;
    }
    if (batch.some(field => field.key === "deliveryMode" && field.value !== "internal") && entry.bundleRule) {
      entry.bundleRule = "";
      entryChanged = true;
    }
    if (batch.some(field => field.key === "bundleRule" && field.value)) {
      entry.deliveryMode = "internal";
    }
    applied++;
    if (entryChanged) changed++;
  }

  if (changed > 0) markBuildProfileDirty();
  state.lastActionMessage = `批量字段编辑已应用 ${applied} 个 draft profile entry，跳过 ${skipped} 个未加入草稿的勾选资源。`;
  render();
}

function getProfileBatchRoot() {
  return !el.profileBatchBar.classList.contains("hidden") ? el.profileBatchBar : el.buildProfileContent;
}

function readBuildProfileBatchFields() {
  const fields = [];
  const root = getProfileBatchRoot();
  const controls = Array.from(root.querySelectorAll("[data-profile-batch-field]"));
  for (const control of controls) {
    const key = control.dataset.profileBatchField;
    const enabled = root.querySelector(`[data-profile-batch-enabled="${cssEscapeCompat(key)}"]`);
    if (!enabled?.checked) continue;
    if (control.disabled) continue;
    const rawValue = String(control.value || "");
    if (rawValue.trim() === "") continue;
    const value = key === "labels" || key === "preloadGroups"
      ? splitCsv(rawValue)
      : rawValue.trim();
    if (Array.isArray(value) && value.length === 0) continue;
    if (key === "bundleOverrideMode" && !isAllowedBuildProfileBatchOverrideMode(value)) continue;
    fields.push({ key, value });
  }
  return fields;
}

function isAllowedBuildProfileBatchOverrideMode(value) {
  return ["none", "forceStandalone", "forceExternal", "exclude"].includes(value);
}

function buildProfileBatchValuesEqual(left, right) {
  if (Array.isArray(left) || Array.isArray(right)) {
    if (!Array.isArray(left) || !Array.isArray(right)) return false;
    return left.length === right.length && left.every((value, index) => value === right[index]);
  }
  return left === right;
}

function cloneBuildProfileBatchValue(value) {
  return Array.isArray(value) ? [...value] : value;
}

function syncBuildProfileBatchField(field, enabled) {
  const control = getProfileBatchRoot().querySelector(`[data-profile-batch-field="${cssEscapeCompat(field)}"]`);
  if (control) control.disabled = !enabled;
}

function updateSelectedBuildProfileField(field, value, renderFeedback) {
  const item = getSelectedItem();
  const entry = item ? findDraftBuildProfileEntryForItem(item) : null;
  if (!entry) return;
  if (field === "labels" || field === "preloadGroups") {
    entry[field] = splitCsv(value);
  } else {
    entry[field] = value;
  }
  if (field === "deliveryMode" && value !== "internal") {
    entry.bundleRule = "";
    entry.bundleGroupHint = entry.bundleGroupHint || "";
    if (entry.bundleOverrideMode === "forceBundle") {
      entry.bundleOverrideMode = "none";
    }
  }
  if (field === "bundleRule") {
    entry.deliveryMode = "internal";
    state.selectedBundleRuleId = value || state.selectedBundleRuleId;
  }
  if (field === "resourceKeyId") {
    const parsed = parseProfileResourceKey(value);
    entry.resourceKey = {
      ...(entry.resourceKey || {}),
      packageId: parsed.packageId || pick(entry.resourceKey, "packageId") || "",
      type: parsed.type || pick(entry.resourceKey, "type") || "",
      id: parsed.id || "",
      variant: parsed.variant || pick(entry.resourceKey, "variant") || ""
    };
  }
  markBuildProfileDirty();
  if (renderFeedback) {
    render();
  } else {
    renderContextActions();
  }
}

async function saveBuildProfileDraft() {
  const profile = getBuildProfile();
  if (!profile) return;
  setWorkspace("profile");
  state.writeState = { status: "running", action: "save-build-profile", error: "" };
  state.lastActionMessage = "正在保存 Global Resource Build Profile...";
  state.profileFieldHighlight = "";
  render();
  try {
    const payload = await postJson(API.saveBuildProfile, { profile });
    state.buildProfilePayload = payload;
    state.buildProfileDraft = structuredCloneCompat(pick(payload, "profile") || profile);
    state.buildProfileDirty = false;
    await loadBundlePlan();
    state.writeState = { status: "idle", action: "", error: "" };
    state.lastActionMessage = "Global Resource Build Profile 已保存。";
  } catch (error) {
    state.writeState = { status: "error", action: "save-build-profile", error: error.message };
    state.lastActionMessage = `Profile 保存失败：${formatValidationSummary(error.data) || error.message}`;
    state.profileFieldHighlight = inferProfileValidationField(error.data);
    if (error.data) {
      state.buildProfilePayload = error.data;
      state.buildProfileDraft = structuredCloneCompat(pick(error.data, "profile") || profile);
      state.buildProfileDirty = true;
    }
    state.errors.push(apiError("保存 Global Build Profile", error));
  }
  render();
}

async function replaceSelectedResourceFile(file) {
  const item = getSelectedItem();
  if (!item || !file) return;

  const nextFormat = inferFormatFromFileName(file.name);
  const currentFormat = inferFormatFromFileName(item.sourcePath);
  let allowFormatChange = false;
  if (currentFormat && nextFormat && currentFormat !== nextFormat) {
    allowFormatChange = window.confirm(`源文件格式将从 .${currentFormat} 改为 .${nextFormat}，确认替换？`);
    if (!allowFormatChange) return;
  }

  const bytesBase64 = await readFileAsBase64(file);
  await executeResourceWrite("replace-source", API.replaceSource, {
    id: getResourceWriteId(item),
    fileName: file.name,
    role: inferModelRole(item),
    allowFormatChange,
    bytesBase64
  }, "替换源文件");
}

async function buildImportRequest(file, preset, fromFolder) {
  return {
    fileName: file.name,
    kind: preset.kind,
    usage: preset.usage,
    role: preset.role,
    localId: fromFolder ? buildFolderLocalId(file, preset) : "",
    tags: fromFolder ? ["resourcelibrary-folder-import", preset.id] : [preset.id],
    bytesBase64: await readFileAsBase64(file)
  };
}

async function stageImportFiles(files) {
  const stagedFiles = [];
  for (let i = 0; i < files.length; i++) {
    const file = files[i];
    state.lastActionMessage = `扫描文件夹：${i + 1} / ${files.length}`;
    renderStatus();
    const ignored = isIgnoredImportFile(file);
    stagedFiles.push({
      fileName: file.name || "",
      relativePath: getImportDisplayPath(file),
      sizeBytes: file.size || 0,
      bytesBase64: ignored ? "" : await readFileAsBase64(file)
    });
  }

  return postJson(API.stageImport, {
    package: state.packageRelative,
    sourceRootLabel: "browser-folder",
    files: stagedFiles
  });
}

function isImportableStagedItem(item) {
  return item?.sourceProviderId === "externalImportStaging"
    && item?.runtimeAvailability === "NotRuntimeLoadable"
    && item?.importStatus === "New"
    && stringValue(pick(item.metadata, "selectable")) === "true"
    && stringValue(pick(item.metadata, "supported")) === "true";
}

function isStagedItemSupportedByPreset(files, item, preset) {
  const file = findStagedSourceFile(files, item);
  return Boolean(file && isFileSupportedByPreset(file, preset));
}

function findStagedSourceFile(files, staged) {
  const relativePath = stringValue(pick(staged.metadata, "relativePath")) || staged.sourcePath || "";
  return files.find(file => getImportDisplayPath(file) === relativePath || file.name === relativePath || file.name === staged.displayName);
}

async function buildImportRequestFromStagedItem(file, staged, preset) {
  const detectedKind = stringValue(pick(staged.metadata, "detectedKind")) || staged.kind || "";
  const kind = preset?.kind || detectedKind || "config";
  const usage = preset?.usage || stringValue(pick(staged.metadata, "detectedUsage")) || staged.usage || "characterConfig";
  const role = preset?.role || inferStagedRole(kind, usage);
  const tags = ["resourcelibrary-folder-import", preset?.id || `auto-${kind}`];
  if (detectedKind && detectedKind !== kind) {
    tags.push(`detected-${detectedKind}`);
  }

  return {
    fileName: file.name,
    kind,
    usage,
    role,
    localId: buildFolderLocalId(file, preset || { id: kind }),
    tags,
    bytesBase64: await readFileAsBase64(file)
  };
}

function inferStagedRole(kind, usage) {
  if (kind !== "model") return "";
  if (usage === "characterModel") return "body";
  if (usage === "weaponModel") return "preview";
  return "preview";
}

async function executeResourceWrite(action, url, request, label) {
  state.writeState = { status: "running", action, error: "" };
  state.lastActionMessage = `${label}进行中...`;
  render();

  try {
    const payload = await postJson(url, { package: state.packageRelative, ...request });
    state.writeState = { status: "idle", action: "", error: "" };
    state.lastActionMessage = `${label}完成`;
    await applyWriteResponse(payload, request.id || request.resourceKey || "");
  } catch (error) {
    state.writeState = { status: "error", action, error: error.message };
    state.lastActionMessage = `${label}失败：${error.message}`;
    state.errors.push(apiError(label, error));
    render();
  }
}

async function applyWriteResponse(payload, fallbackSelection) {
  state.inspectCache.clear();
  const selectedId = stringValue(pick(payload, "selectedResourceKey", "selectedId")) || fallbackSelection || state.selectedResourceKey;
  if (pick(payload, "resources")) {
    state.resourcesPayload = pick(payload, "resources");
  }
  if (pick(payload, "resourcePlan")) {
    state.resourcePlanPayload = pick(payload, "resourcePlan");
  }
  if (selectedId) {
    state.selectedResourceKey = selectedId;
  }
  await loadPackageData();
}

async function selectResource(resourceKey) {
  state.selectedResourceKey = resourceKey;
  state.inspectState = { id: "", status: "idle", payload: null, error: "" };
  const filtered = sortResourceItems(getFilteredItems(getNormalizedItems()));
  const treeRoot = indexResourceTree(buildResourceTree(filtered));
  const nodeId = findTreeNodeIdForItem(treeRoot, resourceKey);
  state.selectedTreeNodeId = nodeId;
  ensureTreeExpansionForNode(nodeId, treeRoot);
  render();
  await loadInspectForSelection();
}

async function loadInspectForSelection() {
  const item = getSelectedItem();
  if (!item) return;

  const id = item.libraryItemId || item.stableId || item.resourceKey || item.key;
  const cacheKey = `${state.packageRelative}::${id}`;
  if (state.inspectCache.has(cacheKey)) {
    state.inspectState = { id, status: "ready", payload: state.inspectCache.get(cacheKey), error: "" };
    renderInspector();
    return;
  }

  state.inspectState = { id, status: "loading", payload: buildFallbackInspect(item), error: "" };
  renderInspector();

  try {
    const payload = await fetchJson(API.inspect(state.packageRelative, id));
    if (payload && payload.error) {
      throw new Error(payload.message || payload.error);
    }
    state.inspectCache.set(cacheKey, payload);
    state.inspectState = { id, status: "ready", payload, error: "" };
  } catch (error) {
    state.inspectState = {
      id,
      status: "fallback",
      payload: buildFallbackInspect(item),
      error: `inspect endpoint 不可用，已使用资源列表和 resource plan 推导详情：${error.message}`
    };
  }
  renderInspector();
}

async function fetchJson(url) {
  const response = await fetch(url, { headers: { Accept: "application/json" } });
  const text = await response.text();
  let data = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = { message: text.slice(0, 240) };
    }
  }
  if (!response.ok) {
    const message = data?.message || data?.error || response.statusText || "请求失败";
    const error = new Error(`${response.status} ${message}`);
    error.status = response.status;
    error.data = data;
    throw error;
  }
  return data;
}

async function postJson(url, body) {
  const response = await fetch(url, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify(body)
  });
  const text = await response.text();
  let data = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = { message: text.slice(0, 240) };
    }
  }
  if (!response.ok) {
    const message = data?.message || data?.error || response.statusText || "请求失败";
    const error = new Error(`${response.status} ${message}`);
    error.status = response.status;
    error.data = data;
    throw error;
  }
  return data;
}

function renderLoading() {
  el.serverStatus.textContent = "正在连接 Authoring 服务...";
  el.statusStrip.innerHTML = statusChip("服务", "读取中", "pending");
  el.resourceSummary.textContent = "正在读取资源管理器...";
  el.checkedSummary.textContent = "已勾选 0 个资源";
  el.resourceTree.innerHTML = emptyBlock("正在读取目录…");
  el.resourceList.innerHTML = emptyBlock("正在读取资源项");
  el.browsePanelTitle.textContent = "当前选择";
  el.browsePanelSubtitle.textContent = "等待资源库数据";
  el.browseContextBody.innerHTML = "";
  el.planSummary.textContent = "正在读取 resource plan...";
  el.planGrid.innerHTML = "";
  el.buildProfileSummary.textContent = "正在读取 Global Resource Build Profile...";
  el.buildProfileContent.innerHTML = "";
  el.inspectorStatus.textContent = "正在读取详情...";
  el.inspectorContent.innerHTML = emptyBlock("等待资源详情");
}

function render() {
  renderPackageSelect();
  renderStatus();
  renderWorkspaceNav();
  renderImportPresetOptions();
  renderFilters();
  renderBrowser();
  renderBrowseContext();
  renderBuildProfile();
  renderPlan();
  renderBuildWorkspace();
  renderInspector();
  renderDebugPicker();
  renderRawJson();
  renderContextActions();
}

function setWorkspace(workspace) {
  if (!WORKSPACES.includes(workspace)) return;
  state.activeWorkspace = workspace;
  for (const view of el.workspaceViews) {
    if (!view) continue;
    const active = view.dataset.workspace === workspace;
    view.classList.toggle("active", active);
    view.hidden = !active;
  }
  for (const button of el.workspaceNavButtons) {
    button.classList.toggle("active", button.dataset.workspace === workspace);
  }
  renderWorkspaceNav();
}

function renderWorkspaceNav() {
  const dirty = state.buildProfileDirty ? " *" : "";
  for (const button of el.workspaceNavButtons) {
    if (button.dataset.workspace === "profile" && state.buildProfileDirty) {
      button.setAttribute("title", "有未保存的 Profile 草稿");
    } else {
      button.removeAttribute("title");
    }
  }
  if (el.saveBuildProfileButton) {
    el.saveBuildProfileButton.textContent = state.buildProfileDirty ? "保存 Profile *" : "保存 Profile";
  }
}

function handleContextActionClick(event) {
  const bundleButton = event.target.closest("button[data-bundle-action]");
  if (bundleButton && !bundleButton.disabled) {
    handleBundleProfileAction(bundleButton);
    return;
  }
  const button = event.target.closest("button[data-action]");
  if (!button || button.disabled) return;
  const action = button.dataset.action;
  if (action === "view-debug") {
    setWorkspace("debug");
    render();
    return;
  }
  if (action === "open-profile") {
    setWorkspace("profile");
    render();
    return;
  }
  if (action === "add-profile") {
    addSelectedToBuildProfile();
    return;
  }
  if (action === "remove-profile") {
    removeSelectedFromBuildProfile();
    return;
  }
  if (action === "revert-profile") {
    revertSelectedBuildProfileDraft();
    return;
  }
  if (action === "batch-add-profile") {
    addCheckedToBuildProfile();
    return;
  }
  if (action === "assign-selected-bundle") {
    assignSelectedToBundle(getActionBundleRuleId(button));
    return;
  }
  if (action === "batch-assign-bundle") {
    assignCheckedToBundle(getActionBundleRuleId(button));
    return;
  }
  if (action === "batch-clear-bundle") {
    clearCheckedBundleAssignments();
    return;
  }
  if (action === "batch-remove-profile") {
    removeCheckedFromBuildProfile();
  }
}

function showUnityWorkbenchGuidance() {
  window.alert(UNITY_WORKBENCH_GUIDANCE);
}

async function copyBuildReport() {
  const plan = getBundlePlan();
  const profile = getBuildProfile();
  const report = {
    package: state.packageRelative,
    profileSaved: !state.buildProfileDirty,
    bundlePlan: plan,
    validation: pick(state.buildProfilePayload, "validation"),
    diagnostics: [
      ...asArray(pick(pick(state.buildProfilePayload, "validation"), "issues")),
      ...asArray(pick(plan, "diagnostics"))
    ],
    unityWorkbench: UNITY_WORKBENCH_GUIDANCE
  };
  await copyText(JSON.stringify(report, null, 2), "已复制 Build Report");
}

function revertSelectedBuildProfileDraft() {
  const item = getSelectedItem();
  const saved = item ? findSavedBuildProfileEntryForItem(item) : null;
  const profile = getBuildProfile();
  if (!item || !profile || !Array.isArray(profile.entries)) return;
  const draftEntry = findDraftBuildProfileEntryForItem(item);
  if (!draftEntry) return;
  if (saved) {
    const index = profile.entries.findIndex(entry => profileEntryMatchesItem(entry, item));
    if (index >= 0) profile.entries[index] = structuredCloneCompat(saved);
  } else {
    profile.entries = profile.entries.filter(entry => !profileEntryMatchesItem(entry, item));
  }
  markBuildProfileDirty();
  state.lastActionMessage = saved ? "已还原当前资源为已保存 Profile 字段。" : "已从草稿移除当前资源。";
  render();
}

function capitalize(value) {
  return String(value || "").charAt(0).toUpperCase() + String(value || "").slice(1);
}

function renderPackageSelect() {
  el.packageSelect.innerHTML = state.packages.map(pkg => {
    const label = formatPackageContextLabel(pkg);
    return `<option value="${escapeHtml(pkg.relative)}"${pkg.relative === state.packageRelative ? " selected" : ""}>${escapeHtml(label)}</option>`;
  }).join("");
  el.openCharacterStudioButton.href = `/Tools/MxFramework.CharacterStudio/web/?package=${encodeURIComponent(state.packageRelative)}`;
}

function renderStatus() {
  const items = getNormalizedItems();
  const diagnostics = getAllDiagnostics();
  const connected = state.errors.length === 0 || Boolean(state.resourcesPayload || state.resourcePlanPayload);
  const planStatus = getPlanStatus();
  const packageLabel = getSelectedPackageLabel();
  const providers = getProviders();
  const unavailableProviders = providers.filter(provider => !provider.available);
  const buildProfile = getBuildProfile();
  const bundlePlan = getBundlePlan();

  el.serverStatus.textContent = connected
    ? `已连接 Authoring 服务；当前工作上下文：${packageLabel}。资源归属和打包归属仍由 provider / Global Build Profile 决定。`
    : "未连接 Authoring 服务。请通过 Editor Hub 或启动脚本打开本工具。";

  const validationMessage = state.lastActionMessage
    ? statusChip("最近操作", state.lastActionMessage, "info")
    : "";
  el.statusStrip.innerHTML = [
    statusChip("Authoring", connected ? "已连接" : "未连接", connected ? "ok" : "error"),
    statusChip("providers", providers.length > 0 ? `${providers.length}` : "0", unavailableProviders.length > 0 ? "warn" : providers.length > 0 ? "ok" : "warn"),
    statusChip("资源项", String(items.length), items.length > 0 ? "ok" : "warn"),
    statusChip("诊断", String(diagnostics.length), diagnostics.some(d => getSeverity(d) === "Error") ? "error" : diagnostics.length > 0 ? "warn" : "ok", diagnostics.length > 0 ? "open-diagnostics" : ""),
    statusChip("resource plan", planStatus, planStatus === "Ready" ? "ok" : state.resourcePlanPayload ? "warn" : "error"),
    statusChip("build profile", `${asArray(pick(buildProfile, "entries")).length}`, buildProfile ? "ok" : "warn"),
    statusChip("bundle plan", `${asArray(pick(bundlePlan, "bundles")).length}`, bundlePlan ? "ok" : "warn"),
    validationMessage
  ].filter(Boolean).join("");
}

function openDiagnosticsView() {
  state.activeTab = "diagnostics";
  setWorkspace("debug");
  render();
}

function renderFilters() {
  const items = getNormalizedItems();
  setSelectOptions(el.kindFilter, buildOptions(items, "kind"), state.filters.kind);
  setSelectOptions(el.usageFilter, buildOptions(items, "usage"), state.filters.usage);
  setSelectOptions(el.providerFilter, buildOptions(items, "providerId"), state.filters.providerId);
  setSelectOptions(el.sourceFilter, buildOptions(items, "sourceKind"), state.filters.sourceKind);
  setSelectOptions(el.importFilter, buildOptions(items, "importStatus"), state.filters.importStatus);
  setSelectOptions(el.runtimeFilter, buildOptions(items, "runtimeAvailability"), state.filters.runtimeAvailability);
  setSelectOptions(el.tagFilter, buildTagOptions(items), state.filters.tag);
  applyFilterControls();
  renderFilterQuickBar();
  renderActiveFiltersBar();
}

function resetFilters() {
  state.filters = { ...FILTER_DEFAULTS };
  state.quickFilter = "";
  state.selectedTreeNodeId = getTreeRootId();
  applyFilterControls();
}

function applyQuickFilter(preset) {
  if (preset === "all") {
    resetFilters();
    return;
  }
  resetFilters();
  state.quickFilter = preset;
  if (preset === "needsProfile") {
    state.filters.profileMembership = "notInProfile";
    state.filters.runtimeReady = "runtimeReady";
  } else if (preset === "profileDraft") {
    state.filters.profileMembership = "all";
  } else if (preset === "runtimeReady") {
    state.filters.runtimeReady = "runtimeReady";
  } else if (preset === "hasDiagnostics") {
    state.filters.onlyDiagnostics = true;
  }
  applyFilterControls();
}

function clearFilterKey(key) {
  state.quickFilter = "";
  if (key === "search") state.filters.search = "";
  else if (key === "quickFilter") state.quickFilter = "";
  else if (key in FILTER_DEFAULTS) state.filters[key] = FILTER_DEFAULTS[key];
  applyFilterControls();
}

function renderFilterQuickBar() {
  document.querySelectorAll(".filter-quick-bar [data-quick-filter]").forEach(button => {
    button.classList.toggle("active", button.dataset.quickFilter === (state.quickFilter || "all"));
  });
}

function getActiveFilterLabels() {
  const labels = [];
  if (state.quickFilter && state.quickFilter !== "all") {
    const quickLabels = {
      needsProfile: "待加入 Profile",
      profileDraft: "Profile 草稿变更",
      runtimeReady: "Runtime 候选",
      hasDiagnostics: "有诊断"
    };
    labels.push({ key: "quickFilter", text: quickLabels[state.quickFilter] || state.quickFilter });
  }
  if (state.filters.search.trim()) labels.push({ key: "search", text: `搜索: ${state.filters.search.trim()}` });
  if (state.filters.kind !== "all") labels.push({ key: "kind", text: `类型: ${state.filters.kind}` });
  if (state.filters.usage !== "all") labels.push({ key: "usage", text: `用途: ${state.filters.usage}` });
  if (state.filters.providerId !== "all") labels.push({ key: "providerId", text: `来源: ${state.filters.providerId}` });
  if (state.filters.sourceKind !== "all") labels.push({ key: "sourceKind", text: `源: ${state.filters.sourceKind}` });
  if (state.filters.importStatus !== "all") labels.push({ key: "importStatus", text: `导入: ${state.filters.importStatus}` });
  if (state.filters.runtimeAvailability !== "all") labels.push({ key: "runtimeAvailability", text: `运行时: ${state.filters.runtimeAvailability}` });
  if (state.filters.profileMembership !== "all") labels.push({ key: "profileMembership", text: `Profile: ${formatProfileStatusLabel(state.filters.profileMembership)}` });
  if (state.filters.runtimeReady !== "all") labels.push({ key: "runtimeReady", text: state.filters.runtimeReady === "runtimeReady" ? "Runtime 候选" : "非 Runtime 候选" });
  if (state.filters.tag !== "all") labels.push({ key: "tag", text: `标签: ${state.filters.tag}` });
  if (state.filters.onlyReferenced) labels.push({ key: "onlyReferenced", text: "已引用" });
  if (state.filters.onlyOrphan) labels.push({ key: "onlyOrphan", text: "orphan" });
  if (state.filters.onlyRuntimeLoadable) labels.push({ key: "onlyRuntimeLoadable", text: "可加载" });
  if (state.filters.onlyDiagnostics) labels.push({ key: "onlyDiagnostics", text: "有诊断" });
  return labels;
}

function renderActiveFiltersBar() {
  const labels = getActiveFilterLabels();
  const advancedCount = countAdvancedFiltersActive();
  if (advancedCount > 0) {
    el.advancedFilterBadge.textContent = String(advancedCount);
    el.advancedFilterBadge.classList.remove("hidden");
    if (el.advancedFilters && !el.advancedFilters.open) {
      el.advancedFilters.open = true;
    }
  } else {
    el.advancedFilterBadge.classList.add("hidden");
  }

  if (labels.length === 0) {
    el.activeFiltersBar.classList.add("hidden");
    el.activeFiltersBar.innerHTML = "";
    return;
  }

  el.activeFiltersBar.classList.remove("hidden");
  el.activeFiltersBar.innerHTML = `
    <span class="active-filters-label">已启用</span>
    ${labels.map(label => `<button type="button" class="filter-chip active removable" data-clear-filter="${escapeHtml(label.key)}">${escapeHtml(label.text)} ×</button>`).join("")}
    <button type="button" class="filter-chip secondary-chip" data-clear-filter="__all__">清除全部</button>`;
}

function countAdvancedFiltersActive() {
  let count = 0;
  if (state.filters.usage !== "all") count++;
  if (state.filters.sourceKind !== "all") count++;
  if (state.filters.importStatus !== "all") count++;
  if (state.filters.runtimeAvailability !== "all") count++;
  if (state.filters.runtimeReady !== "all") count++;
  if (state.filters.tag !== "all") count++;
  if (state.filters.onlyReferenced) count++;
  if (state.filters.onlyOrphan) count++;
  if (state.filters.onlyRuntimeLoadable) count++;
  if (state.filters.onlyDiagnostics) count++;
  return count;
}

function formatProfileStatusLabel(status) {
  const labels = {
    notInProfile: "未入 Profile",
    saved: "已保存",
    draftOnly: "草稿新增",
    removedInDraft: "草稿移除",
    modifiedInDraft: "草稿已改",
    inProfile: "已在 Profile"
  };
  return labels[status] || status || "-";
}

function formatRuntimeLabel(value) {
  const text = String(value || "");
  if (text === "RuntimeLoadable") return "可加载";
  if (text === "NotRuntimeLoadable") return "不可加载";
  if (text === "Unknown") return "未知";
  return text || "-";
}

function sortResourceItems(items) {
  const sorted = [...items];
  if (state.resourceSort === "diagnostics") {
    sorted.sort((a, b) => b.diagnosticCount - a.diagnosticCount || compareText(a.displayName, b.displayName));
    return sorted;
  }
  if (state.resourceSort === "references") {
    sorted.sort((a, b) => b.referenceCount - a.referenceCount || compareText(a.displayName, b.displayName));
    return sorted;
  }
  if (state.resourceSort === "profile") {
    sorted.sort((a, b) => {
      const left = PROFILE_STATUS_ORDER[getBuildProfileStatus(a)] ?? 0;
      const right = PROFILE_STATUS_ORDER[getBuildProfileStatus(b)] ?? 0;
      return left - right || compareText(a.displayName, b.displayName);
    });
    return sorted;
  }
  sorted.sort((a, b) => compareText(a.displayName || a.stableId, b.displayName || b.stableId));
  return sorted;
}

function getTreeRootId() {
  return state.treeGroupMode === "taxonomy" ? "tax::__root__" : "path::__root__";
}

function getResourcePathSegments(item) {
  const raw = stringValue(item.sourcePath) || stringValue(item.unityAssetPath) || "";
  const normalized = raw.replace(/\\/g, "/").replace(/^\/+/, "");
  if (!normalized) return ["(未分类路径)"];
  const parts = normalized.split("/").filter(Boolean);
  if (parts.length === 0) return ["(未分类路径)"];
  const last = parts[parts.length - 1];
  if (/\.[a-z0-9]{1,8}$/i.test(last) && parts.length > 1) {
    return parts.slice(0, -1);
  }
  return parts;
}

function createTreeNode(id, label, kind, itemKeys = []) {
  return { id, label, kind, itemKeys: [...itemKeys], children: [] };
}

function buildResourceTree(items) {
  if (state.treeGroupMode === "taxonomy") {
    return buildTaxonomyTree(items);
  }
  return buildPathTree(items);
}

function buildPathTree(items) {
  const root = createTreeNode(getTreeRootId(), "全部资源", "root");
  const folderMap = new Map([[root.id, root]]);

  for (const item of items) {
    const segments = getResourcePathSegments(item);
    let parent = root;
    let pathSoFar = "";
    for (const segment of segments) {
      pathSoFar = pathSoFar ? `${pathSoFar}/${segment}` : segment;
      const id = `path::${pathSoFar}`;
      let node = folderMap.get(id);
      if (!node) {
        node = createTreeNode(id, segment, "folder");
        parent.children.push(node);
        folderMap.set(id, node);
      }
      parent = node;
    }
    parent.itemKeys.push(item.key);
  }

  sortTreeChildren(root);
  return root;
}

function buildTaxonomyTree(items) {
  const root = createTreeNode(getTreeRootId(), "全部资源", "root");
  const providerMap = new Map();

  for (const item of items) {
    const providerId = item.providerId || "unknown";
    const kind = item.kind || "unknown";
    const usage = item.usage || "-";
    let provider = providerMap.get(providerId);
    if (!provider) {
      provider = createTreeNode(`tax::${providerId}`, providerId, "provider");
      root.children.push(provider);
      providerMap.set(providerId, provider);
    }
    let kindNode = provider.children.find(child => child.label === kind);
    if (!kindNode) {
      kindNode = createTreeNode(`tax::${providerId}::${kind}`, kind, "kind");
      provider.children.push(kindNode);
    }
    let usageNode = kindNode.children.find(child => child.label === usage);
    if (!usageNode) {
      usageNode = createTreeNode(`tax::${providerId}::${kind}::${usage}`, usage, "usage");
      kindNode.children.push(usageNode);
    }
    usageNode.itemKeys.push(item.key);
  }

  sortTreeChildren(root);
  return root;
}

function sortTreeChildren(node) {
  node.children.sort((a, b) => compareText(a.label, b.label));
  for (const child of node.children) sortTreeChildren(child);
}

function indexResourceTree(root) {
  const nodeKeys = new Map();
  function walk(node) {
    const keys = new Set(node.itemKeys || []);
    for (const child of node.children) {
      walk(child);
      const childKeys = nodeKeys.get(child.id);
      if (childKeys) {
        for (const key of childKeys) keys.add(key);
      }
    }
    nodeKeys.set(node.id, keys);
    node.count = keys.size;
    return keys;
  }
  walk(root);
  state.treeNodeKeys = nodeKeys;
  return root;
}

function getTreeScopedItems(items) {
  const rootId = getTreeRootId();
  if (!state.selectedTreeNodeId || state.selectedTreeNodeId === rootId) return items;
  const keys = state.treeNodeKeys.get(state.selectedTreeNodeId);
  if (!keys || keys.size === 0) return items;
  return items.filter(item => keys.has(item.key));
}

function findTreeNode(root, nodeId) {
  if (root.id === nodeId) return root;
  for (const child of root.children) {
    const found = findTreeNode(child, nodeId);
    if (found) return found;
  }
  return null;
}

function findTreeNodeIdForItem(root, itemKey) {
  function walk(node) {
    for (const child of node.children) {
      const childHit = walk(child);
      if (childHit) return childHit;
    }
    if ((node.itemKeys || []).includes(itemKey)) return node.id;
    return "";
  }
  return walk(root) || root.id;
}

function ensureTreeExpansionForNode(nodeId, root) {
  if (!nodeId || nodeId === root.id) {
    state.expandedTreeNodes.add(root.id);
    return;
  }
  if (state.treeGroupMode === "path") {
    state.expandedTreeNodes.add("path::__root__");
    if (nodeId === "path::__root__") return;
    const parts = nodeId.replace(/^path::/, "").split("/");
    let pathSoFar = "";
    for (const part of parts) {
      pathSoFar = pathSoFar ? `${pathSoFar}/${part}` : part;
      state.expandedTreeNodes.add(`path::${pathSoFar}`);
    }
    return;
  }
  state.expandedTreeNodes.add("tax::__root__");
  const rest = nodeId.replace(/^tax::/, "");
  const parts = rest.split("::");
  if (parts.length >= 1) state.expandedTreeNodes.add(`tax::${parts[0]}`);
  if (parts.length >= 2) state.expandedTreeNodes.add(`tax::${parts[0]}::${parts[1]}`);
  if (parts.length >= 3) state.expandedTreeNodes.add(nodeId);
}

function ensureDefaultTreeExpansion(root) {
  if (state.expandedTreeNodes.size > 0) return;
  state.expandedTreeNodes.add(root.id);
  for (const child of root.children) {
    state.expandedTreeNodes.add(child.id);
  }
}

function expandAllTreeNodes() {
  for (const nodeId of state.treeNodeKeys.keys()) {
    state.expandedTreeNodes.add(nodeId);
  }
}

function getTreeNodeLabel(node) {
  if (node.kind === "provider") return `来源 · ${node.label}`;
  if (node.kind === "kind") return `类型 · ${node.label}`;
  if (node.kind === "usage") return `用途 · ${node.label}`;
  return node.label;
}

function renderResourceTree(root, filteredCount) {
  ensureDefaultTreeExpansion(root);
  if (state.selectedResourceKey) {
    const nodeId = findTreeNodeIdForItem(root, state.selectedResourceKey);
    state.selectedTreeNodeId = state.selectedTreeNodeId || nodeId;
    ensureTreeExpansionForNode(nodeId, root);
  }
  if (!state.selectedTreeNodeId) {
    state.selectedTreeNodeId = root.id;
  }

  el.resourceTree.innerHTML = renderTreeNodeHtml(root, 0);
  el.treeGroupModeSelect.value = state.treeGroupMode;

  const selectedNode = findTreeNode(root, state.selectedTreeNodeId) || root;
  const scopedCount = getTreeScopedItems(sortResourceItems(getFilteredItems(getNormalizedItems()))).length;
  el.resourceListHeading.textContent = `${getTreeNodeLabel(selectedNode)} · ${scopedCount} 项`;
}

function renderTreeNodeHtml(node, depth) {
  const hasChildren = node.children.length > 0;
  const expanded = state.expandedTreeNodes.has(node.id);
  const selected = state.selectedTreeNodeId === node.id;
  const count = node.count ?? (node.itemKeys?.length || 0);
  const diagCount = countDiagnosticsInTreeNode(node);
  const childrenHtml = hasChildren && expanded
    ? node.children.map(child => renderTreeNodeHtml(child, depth + 1)).join("")
    : "";

  return `
    <div class="tree-node" role="treeitem" aria-expanded="${hasChildren ? expanded : undefined}" data-depth="${depth}">
      <div class="tree-node-row${selected ? " active" : ""}" data-tree-node-id="${escapeHtml(node.id)}">
        <button type="button" class="tree-toggle${hasChildren ? "" : " spacer"}" data-tree-toggle="${escapeHtml(node.id)}"${hasChildren ? ` aria-label="${expanded ? "折叠" : "展开"}"` : " tabindex=\"-1\" aria-hidden=\"true\""}>
          ${hasChildren ? (expanded ? "▾" : "▸") : ""}
        </button>
        <button type="button" class="tree-label" data-tree-select="${escapeHtml(node.id)}">
          <span class="tree-icon" aria-hidden="true">${treeNodeIcon(node)}</span>
          <span class="tree-text" title="${escapeHtml(getTreeNodeLabel(node))}">${escapeHtml(getTreeNodeLabel(node))}</span>
          <span class="tree-count">${count}</span>
          ${diagCount > 0 ? `<span class="tree-diag">${diagCount}</span>` : ""}
        </button>
      </div>
      ${childrenHtml ? `<div class="tree-children" role="group">${childrenHtml}</div>` : ""}
    </div>`;
}

function treeNodeIcon(node) {
  if (node.kind === "root") return "⌂";
  if (node.kind === "provider") return "◎";
  if (node.kind === "kind") return "▣";
  if (node.kind === "usage") return "◦";
  return "▣";
}

function countDiagnosticsInTreeNode(node) {
  const keys = state.treeNodeKeys.get(node.id);
  if (!keys || keys.size === 0) return 0;
  const byKey = new Map(getNormalizedItems().map(item => [item.key, item]));
  let total = 0;
  for (const key of keys) {
    total += byKey.get(key)?.diagnosticCount || 0;
  }
  return total;
}

function handleResourceTreeClick(event) {
  const toggle = event.target.closest("[data-tree-toggle]");
  if (toggle) {
    const nodeId = toggle.dataset.treeToggle;
    if (state.expandedTreeNodes.has(nodeId)) state.expandedTreeNodes.delete(nodeId);
    else state.expandedTreeNodes.add(nodeId);
    renderBrowser();
    return;
  }
  const select = event.target.closest("[data-tree-select]");
  if (!select) return;
  state.selectedTreeNodeId = select.dataset.treeSelect;
  renderBrowser();
  renderBrowseContext();
}

function renderBrowser() {
  const allItems = getNormalizedItems();
  const filtered = sortResourceItems(getFilteredItems(allItems));
  const treeRoot = indexResourceTree(buildResourceTree(filtered));
  const scoped = getTreeScopedItems(filtered);
  const selectedKey = state.selectedResourceKey;
  const diagnosticsCount = getAllDiagnostics().length;
  pruneCheckedResources(allItems);
  const visibleChecked = scoped.filter(item => state.checkedResourceKeys.has(item.key)).length;

  el.resourceSummary.textContent = allItems.length === 0
    ? "没有读取到 Authoring Resource Manager API 数据。"
    : `${filtered.length} / ${allItems.length} 资源 · ${scoped.length} 当前目录 · ${diagnosticsCount} 诊断`;
  el.checkedSummary.textContent = `已勾选 ${state.checkedResourceKeys.size} · 当前目录 ${visibleChecked} 可见`;
  el.selectVisibleButton.disabled = scoped.length === 0;
  el.clearCheckedButton.disabled = state.checkedResourceKeys.size === 0;

  if (allItems.length === 0) {
    el.resourceTree.innerHTML = emptyBlock("暂无目录");
    el.resourceList.innerHTML = emptyBlock("未读取到资源项。请确认 Authoring 服务已启动，并且至少一个 provider 可用。");
    return;
  }

  renderResourceTree(treeRoot, filtered.length);

  if (filtered.length === 0) {
    el.resourceList.innerHTML = emptyBlock("没有符合当前筛选条件的资源项。");
    return;
  }
  if (scoped.length === 0) {
    el.resourceList.innerHTML = emptyBlock("当前目录下没有资源，请选择其他目录节点。");
    return;
  }

  el.resourceList.innerHTML = scoped.map(item => {
    const active = item.key === selectedKey ? " active" : "";
    const checked = state.checkedResourceKeys.has(item.key);
    const profileStatus = getBuildProfileStatus(item);
    const profileLabel = formatProfileStatusLabel(profileStatus);
    const runtimeLabel = formatRuntimeLabel(item.runtimeAvailability);
    const deliveryState = getDeliveryEntryState(item, findDraftBuildProfileEntryForItem(item));
    const displayName = item.displayName || item.stableId || item.resourceKey || "resource";
    const identity = item.stableId || item.resourceKey || item.libraryItemId || "-";
    const typeLine = `${item.kind || "unknown"} · ${item.usage || "-"}`;
    const provider = item.providerId || "provider";
    const runtimeTone = toneForRuntime(item.runtimeAvailability);
    const profileTone = toneForBuildProfileStatus(profileStatus);
    const diagWarn = item.diagnosticCount > 0 ? " has-diagnostics" : "";
    return `
      <article class="resource-row${active}${checked ? " checked" : ""}${diagWarn}" role="option" aria-selected="${item.key === selectedKey}">
        <label class="resource-check" title="勾选用于批量 Profile 操作">
          <input type="checkbox" data-check-resource-key="${escapeHtml(item.key)}"${checked ? " checked" : ""}>
          <span>选择</span>
        </label>
        <button class="resource-row-main" type="button" data-resource-key="${escapeHtml(item.key)}">
          <div class="resource-row-top">
            <strong class="resource-name" title="${escapeHtml(displayName)}">${escapeHtml(displayName)}</strong>
            <span class="resource-inline-metrics">
              ${item.referenceCount > 0 ? `<span class="metric-pill" title="引用">引 ${item.referenceCount}</span>` : ""}
              ${item.diagnosticCount > 0 ? `<span class="metric-pill warn" title="诊断">诊 ${item.diagnosticCount}</span>` : ""}
            </span>
          </div>
          <div class="resource-row-sub">${escapeHtml(typeLine)} · ${escapeHtml(provider)}</div>
          <div class="resource-row-badges">
            <span class="status-pill ${profileTone}" title="Profile 状态">${escapeHtml(profileLabel)}</span>
            <span class="status-pill ${runtimeTone}" title="运行时">${escapeHtml(runtimeLabel)}</span>
            <span class="status-pill neutral" title="交付结果">${escapeHtml(deliveryState.label)}</span>
          </div>
          <div class="resource-row-id" title="${escapeHtml(identity)}">${escapeHtml(identity)}</div>
        </button>
      </article>`;
  }).join("");
}

function selectVisibleResources() {
  const filtered = sortResourceItems(getFilteredItems(getNormalizedItems()));
  const visibleItems = getTreeScopedItems(filtered);
  for (const item of visibleItems) {
    state.checkedResourceKeys.add(item.key);
  }
  state.lastActionMessage = `已选择当前目录可见 ${visibleItems.length} 个资源。`;
  render();
}

function toggleCheckedResource(key, checked) {
  if (checked) {
    state.checkedResourceKeys.add(key);
  } else {
    state.checkedResourceKeys.delete(key);
  }
  render();
}

function pruneCheckedResources(items) {
  const valid = new Set(items.map(item => item.key));
  for (const key of Array.from(state.checkedResourceKeys)) {
    if (!valid.has(key)) state.checkedResourceKeys.delete(key);
  }
}

function getCheckedItems() {
  const byKey = new Map(getNormalizedItems().map(item => [item.key, item]));
  return Array.from(state.checkedResourceKeys).map(key => byKey.get(key)).filter(Boolean);
}

function renderBrowseContext() {
  const item = getSelectedItem();
  const checkedCount = state.checkedResourceKeys.size;

  if (!item) {
    el.browsePanelTitle.textContent = "资源概览";
    el.browsePanelSubtitle.textContent = "点击左侧列表选择资源";
    const items = getNormalizedItems();
    const needsProfile = items.filter(entry => getBuildProfileStatus(entry) === "notInProfile" && isRuntimeReadyCandidate(entry)).length;
    el.browseContextBody.innerHTML = `
      <div class="browse-overview-grid">
        ${metric("全部资源", items.length)}
        ${metric("待加入 Profile", needsProfile)}
        ${metric("有诊断", items.filter(entry => entry.diagnosticCount > 0).length)}
        ${metric("Profile", state.buildProfileDirty ? "草稿未保存" : "已同步")}
      </div>
      <p class="profile-hint workflow-hint">推荐流程：筛选候选 → 加入 Profile → 保存 → Unity 构建 → Offline 验证</p>
      <p class="profile-hint">常用筛选：「待加入 Profile」可快速列出 runtime-ready 且未入 Profile 的资源。</p>`;
    el.browseBatchBar.classList.add("hidden");
    el.browseBatchBar.innerHTML = "";
    return;
  }

  const profileStatus = getBuildProfileStatus(item);
  const profileLabel = formatProfileStatusLabel(profileStatus);
  const deliveryState = getDeliveryEntryState(item, findDraftBuildProfileEntryForItem(item));
  const inProfile = Boolean(findDraftBuildProfileEntryForItem(item));
  el.browsePanelTitle.textContent = item.displayName || item.libraryItemId || "资源";
  el.browsePanelSubtitle.textContent = `${item.kind || "unknown"} · ${item.usage || "-"} · ${deliveryState.label}`;

  const nextStep = !inProfile
    ? "下一步：加入 Profile，并在 Profile 工作区配置交付方式。"
    : state.buildProfileDirty
      ? "下一步：保存 Profile，再到 Build 工作区确认 Bundle Plan。"
      : "下一步：到 Build 查看 Bundle Plan，或去 Unity 构建。";

  el.browseContextBody.innerHTML = `
    <div class="browse-status-strip">
      <span class="status-pill ${toneForBuildProfileStatus(profileStatus)}">${escapeHtml(profileLabel)}</span>
      <span class="status-pill ${toneForRuntime(item.runtimeAvailability)}">${escapeHtml(formatRuntimeLabel(item.runtimeAvailability))}</span>
      <span class="status-pill ${deliveryState.tone}">${escapeHtml(deliveryState.label)}</span>
    </div>
    <div class="browse-overview-grid">
      ${metric("引用", item.referenceCount)}
      ${metric("诊断", item.diagnosticCount)}
      ${metric("provider", item.providerId || "-")}
      ${metric("导入", item.importStatus || "-")}
    </div>
    <div class="detail-card compact-card identity-card">
      <h3>资源身份</h3>
      ${renderKeyValueList([
        ["resourceKey", item.resourceKey || "-"],
        ["stableId", item.stableId || "-"],
        ["sourcePath", item.sourcePath || "-"],
        ["unityAssetPath", item.unityAssetPath || "-"]
      ])}
    </div>
    <p class="profile-hint workflow-hint">${escapeHtml(nextStep)}</p>
    <p class="profile-hint">完整 Inspector / JSON 请打开 Debug 工作区。</p>`;

  if (checkedCount > 0) {
    el.browseBatchBar.classList.remove("hidden");
    el.browseBatchBar.innerHTML = `
      <span class="batch-bar-label">已选择 ${checkedCount} 个资源</span>
      ${renderBundleActionSelect("checkedBundleTarget")}
      <button type="button" data-action="batch-assign-bundle" class="primary-action">批量加入 Bundle</button>
      <button type="button" data-action="batch-clear-bundle" class="secondary">批量移出 Bundle</button>
      <button type="button" data-action="batch-add-profile" class="secondary">批量加入 Profile</button>
      <button type="button" data-action="batch-remove-profile" class="secondary">批量移出 Profile</button>`;
  } else {
    el.browseBatchBar.classList.add("hidden");
    el.browseBatchBar.innerHTML = "";
  }
}

function renderPlan() {
  const plan = getPlanDocument();
  const status = getPlanStatus();
  if (!state.resourcePlanPayload) {
    el.planSummary.textContent = "没有读取到 resource plan API 数据。";
    el.planGrid.innerHTML = emptyBlock("未生成运行时资源计划");
    return;
  }

  const groups = getPlanGroups();
  const totalResources = groups.reduce((sum, group) => sum + group.resources.length, 0);
  el.planSummary.textContent = `状态 ${status}，${groups.length} 个分组，${totalResources} 个资源引用`;

  el.planGrid.innerHTML = groups.map(group => `
    <article class="plan-group">
      <div class="plan-group-head">
        <strong>${escapeHtml(group.label)}</strong>
        <span>${group.resources.length}</span>
      </div>
      <p>${escapeHtml(group.required)} / ${escapeHtml(group.failurePolicy || "-")}</p>
      <ul>
        ${group.resources.slice(0, 6).map(resource => `<li>${escapeHtml(resource.resourceKey || resource.id || resource.cue || resource.bank || JSON.stringify(resource))}</li>`).join("")}
      </ul>
      ${group.resources.length > 6 ? `<div class="more">另有 ${group.resources.length - 6} 项</div>` : ""}
    </article>`).join("") || emptyBlock(plan ? "resource plan 没有资源分组" : "未生成 resource plan") ;
}

function renderBuildWorkspace() {
  const plan = getBundlePlan();
  const bundles = asArray(pick(plan, "bundles"));
  const diagnostics = [
    ...asArray(pick(pick(state.buildProfilePayload, "validation"), "issues")),
    ...asArray(pick(plan, "diagnostics"))
  ];
  const dirtyHint = state.buildProfileDirty
    ? "当前有未保存草稿；Bundle Plan 仍来自已保存 Profile，保存后刷新。"
    : "Bundle Plan 来自已保存 Profile。Web UI 不构建 AssetBundle，也不写 StreamingAssets。";

  el.bundlePlanSummary.textContent = dirtyHint;
  el.buildChecklist.innerHTML = renderBuildChecklist();
  el.bundlePlanContent.innerHTML = `
    <div class="summary-grid">
      ${metric("bundles", bundles.length)}
      ${metric("external", asArray(pick(plan, "externalEntries")).length)}
      ${metric("excluded", asArray(pick(plan, "excludedEntries")).length)}
      ${metric("diagnostics", diagnostics.length)}
    </div>
    ${renderBundlePlanSummary(bundles)}`;
  el.buildDiagnostics.innerHTML = `
    <div class="detail-card">
      <h3>构建诊断</h3>
      ${renderDiagnosticsList(diagnostics)}
    </div>
    <p class="profile-hint">${escapeHtml(UNITY_WORKBENCH_GUIDANCE)}</p>`;
}

function renderBuildChecklist() {
  const plan = getBundlePlan();
  const bundles = asArray(pick(plan, "bundles"));
  const diagnostics = [
    ...asArray(pick(pick(state.buildProfilePayload, "validation"), "issues")),
    ...asArray(pick(plan, "diagnostics"))
  ];
  const hasErrors = diagnostics.some(d => getSeverity(d) === "Error");
  const items = [
    { label: "Profile saved", ok: !state.buildProfileDirty && Boolean(getBuildProfile()) },
    { label: "Bundle Plan valid", ok: Boolean(plan) && !hasErrors },
    { label: "Catalog exists", ok: bundles.length > 0, hint: "预览：saved profile 已规划 bundle" },
    { label: "Preload groups exists", ok: bundles.some(b => asArray(pick(b, "preloadGroups")).length > 0), hint: "由 Unity Workbench 生成" },
    { label: "Bundle exists", ok: false, hint: "在 Unity StreamingAssets 中验证" },
    { label: "Build report no errors", ok: !hasErrors }
  ];
  return `<ul class="build-checklist-list">${items.map(item => `
    <li class="${item.ok ? "ok" : "pending"}">
      <span>${item.ok ? "✓" : "○"}</span>
      <div>
        <strong>${escapeHtml(item.label)}</strong>
        ${item.hint ? `<small>${escapeHtml(item.hint)}</small>` : ""}
      </div>
    </li>`).join("")}</ul>`;
}

function renderBuildProfile() {
  const profile = getBuildProfile();
  const entries = getBuildProfileEntries();
  const selected = getSelectedItem();
  const selectedEntry = selected ? findBuildProfileEntryForItem(selected) : null;
  const checkedCount = state.checkedResourceKeys.size;
  const bundles = getBuildProfileBundleRules();

  if (!profile) {
    el.buildProfileSummary.textContent = "没有读取到 Global Resource Build Profile API。";
    el.buildProfileContent.innerHTML = emptyBlock("Authoring Server 未暴露构建 Profile 状态。");
    return;
  }

  const dirtySuffix = state.buildProfileDirty ? "，草稿未保存" : "";
  const profileStateCounts = countBuildProfileStates(getNormalizedItems());
  ensureSelectedBundleRuleId();
  const selectedBundle = getSelectedBundleRule();
  el.buildProfileSummary.textContent = `${bundles.length} 个 Bundle，${entries.length} 个 profile entry${dirtySuffix} · 先定义 Bundle，再把资源加入 Bundle`;

  if (checkedCount > 0) {
    el.profileBatchBar.classList.remove("hidden");
    el.profileBatchBar.innerHTML = `
      <span class="batch-bar-label">已选择 ${checkedCount} 个资源 · 批量分包 / 字段编辑</span>
      ${renderBundleActionSelect("profileCheckedBundleTarget")}
      <button type="button" data-action="batch-assign-bundle" class="primary-action">批量加入 Bundle</button>
      <button type="button" data-action="batch-clear-bundle" class="secondary">批量移出 Bundle</button>
      ${renderBuildProfileBatchEditor()}`;
  } else {
    el.profileBatchBar.classList.add("hidden");
    el.profileBatchBar.innerHTML = "";
  }

  el.buildProfileContent.innerHTML = `
    <div class="profile-state-strip">
      ${["notInProfile", "saved", "draftOnly", "removedInDraft", "modifiedInDraft"].map(status => smallBadge(`${status} ${profileStateCounts[status] || 0}`, toneForBuildProfileStatus(status))).join("")}
    </div>
    <div class="bundle-profile-layout">
      <aside class="bundle-list-panel">
        <div class="bundle-panel-heading">
          <div>
            <h3>Bundle 定义</h3>
            <p>${bundles.length} rules · ${countUnassignedInternalEntries()} unassigned</p>
          </div>
          <button type="button" data-bundle-action="create-bundle" class="secondary">新建 Bundle</button>
        </div>
        <div class="bundle-rule-list">
          ${bundles.length === 0 ? emptyBlock("还没有 Bundle。先新建 Bundle，再加入资源。") : bundles.map(rule => renderBundleRuleCard(rule)).join("")}
        </div>
      </aside>
      <section class="bundle-members-panel">
        ${selectedBundle ? renderBundleMembers(selectedBundle) : emptyBlock("选择或新建 Bundle 后管理成员资源。")}
      </section>
      <aside class="bundle-settings-panel">
        ${selectedBundle ? renderBundleSettings(selectedBundle) : emptyBlock("Bundle 设置会在这里显示。")}
      </aside>
    </div>
    <details class="profile-resource-editor">
      <summary>当前资源 Profile 字段</summary>
      <div class="profile-editor-grid">
        <div class="detail-card profile-steps-card">
          ${selected ? renderBuildProfileEditor(selected, selectedEntry) : emptyBlock("请先在 Browse 工作区选择一个资源，或从 Browse 点击「加入到 Bundle」跳转至此。")}
        </div>
      </div>
    </details>`;
}

function renderBundleRuleCard(rule) {
  const stats = getBundleRuleStats(rule);
  const active = rule.id === state.selectedBundleRuleId;
  return `
    <button type="button" class="bundle-rule-card${active ? " active" : ""}" data-bundle-action="select-bundle" data-bundle-rule-id="${escapeHtml(rule.id)}">
      <strong>${escapeHtml(rule.id || "(missing id)")}</strong>
      <span>${escapeHtml(rule.bundleName || "-")}</span>
      <span class="bundle-rule-meta">${escapeHtml(rule.compression || "lz4")} · ${escapeHtml(rule.buildTarget || "ActiveBuildTarget")}</span>
      <div class="bundle-card-metrics">
        ${smallBadge(`${stats.internalCount} build`, stats.internalCount > 0 ? "ok" : "warn")}
        ${smallBadge(`${stats.externalCount} skipped`, stats.externalCount > 0 ? "warn" : "muted")}
        ${smallBadge(`${stats.missingUnityGuidCount} no guid`, stats.missingUnityGuidCount > 0 ? "error" : "muted")}
        ${smallBadge(`${stats.diagnosticsCount} diag`, stats.diagnosticsCount > 0 ? "warn" : "muted")}
      </div>
    </button>`;
}

function renderBundleMembers(rule) {
  const members = getBundleRuleMemberRows(rule);
  const filtered = filterBundleMemberRows(members);
  const stats = getBundleRuleStats(rule);
  return `
    <div class="bundle-members-heading">
      <div>
        <h3>${escapeHtml(rule.id)} 成员</h3>
        <p>${filtered.length} / ${members.length} resources · ${stats.dependencyBundleNames.length > 0 ? `depends on ${stats.dependencyBundleNames.join(", ")}` : "no bundle dependencies"}</p>
      </div>
      <div class="bundle-members-toolbar">
        <input data-bundle-member-search value="${escapeHtml(state.bundleMemberSearch)}" placeholder="搜索成员 resourceKey / path">
        <button type="button" data-bundle-action="assign-checked-selected-bundle" class="secondary">添加已勾选资源</button>
      </div>
    </div>
    <div class="bundle-member-table">
      ${filtered.length === 0 ? emptyBlock("当前 Bundle 没有匹配成员。") : filtered.map(row => renderBundleMemberRow(row)).join("")}
    </div>`;
}

function renderBundleMemberRow(row) {
  const item = row.item;
  const entry = row.entry;
  return `
    <article class="bundle-member-row">
      <div class="bundle-member-main">
        <strong>${escapeHtml(item?.displayName || pick(pick(entry, "resourceKey"), "id") || row.key)}</strong>
        <span>${escapeHtml(formatProfileResourceKey(pick(entry, "resourceKey")) || row.key)}</span>
      </div>
      <span>${escapeHtml(pick(pick(entry, "resourceKey"), "type", "typeId") || item?.kind || "-")}</span>
      <span>${escapeHtml(pick(pick(entry, "source"), "providerId") || item?.providerId || "-")}</span>
      <span>${escapeHtml(item?.runtimeAvailability || pick(entry, "deliveryMode") || "-")}</span>
      <span>${escapeHtml(asArray(pick(entry, "preloadGroups")).join(", ") || "-")}</span>
      <span>${item ? item.referenceCount : 0} refs · ${item ? item.diagnosticCount : 0} diag</span>
      <button type="button" class="secondary" data-bundle-action="remove-entry-bundle" data-entry-index="${row.index}">移出</button>
    </article>`;
}

function renderBundleSettings(rule) {
  const stats = getBundleRuleStats(rule);
  return `
    <div class="detail-card bundle-settings-card">
      <h3>Bundle 设置</h3>
      <div class="profile-form single-column">
        <label>
          <span>id</span>
          <input data-bundle-field="id" value="${escapeHtml(rule.id || "")}">
        </label>
        <label>
          <span>bundleName</span>
          <input data-bundle-field="bundleName" value="${escapeHtml(rule.bundleName || "")}">
        </label>
        <label>
          <span>compression</span>
          <select data-bundle-field="compression">${selectOptions(["lz4", "uncompressed", "lzma"], rule.compression || "lz4")}</select>
        </label>
        <label>
          <span>buildTarget</span>
          <input data-bundle-field="buildTarget" value="${escapeHtml(rule.buildTarget || "ActiveBuildTarget")}">
        </label>
        <label class="inline-check">
          <input type="checkbox" data-bundle-field="includeDependencies" ${rule.includeDependencies !== false ? "checked" : ""}>
          <span>include dependencies</span>
        </label>
        <label class="inline-check">
          <input type="checkbox" data-bundle-field="allowEmpty" ${rule.allowEmpty ? "checked" : ""}>
          <span>allow empty</span>
        </label>
      </div>
      <div class="summary-grid compact-summary">
        ${metric("build", stats.internalCount)}
        ${metric("skipped", stats.externalCount)}
        ${metric("no guid", stats.missingUnityGuidCount)}
        ${metric("diagnostics", stats.diagnosticsCount)}
      </div>
      <details class="advanced-filters">
        <summary class="advanced-filters-summary">Advanced match rules</summary>
        <div class="profile-form single-column">
          <label>
            <span>matchLabels</span>
            <input data-bundle-field="matchLabels" value="${escapeHtml(asArray(rule.matchLabels).join(", "))}">
          </label>
          <label>
            <span>matchDomains</span>
            <input data-bundle-field="matchDomains" value="${escapeHtml(asArray(rule.matchDomains).join(", "))}">
          </label>
          <label>
            <span>matchPackageIds</span>
            <input data-bundle-field="matchPackageIds" value="${escapeHtml(asArray(rule.matchPackageIds).join(", "))}">
          </label>
        </div>
      </details>
      <div class="bundle-actions">
        <button type="button" data-bundle-action="delete-selected-bundle" class="secondary">删除 Bundle 定义</button>
      </div>
    </div>`;
}

function renderBuildProfileEditor(item, entry) {
  const inProfile = Boolean(entry);
  const draft = entry || buildProfileEntryFromItem(item);
  const deliveryMode = pick(draft, "deliveryMode") || "internal";
  const isInternal = deliveryMode === "internal";
  const resourceKeyId = stringValue(pick(pick(draft, "resourceKey"), "id"));
  const keyValid = isValidProfileResourceKeyId(resourceKeyId);
  const deliveryState = getDeliveryEntryState(item, entry);
  const highlight = state.profileFieldHighlight;
  const saveStatus = getBuildProfileStatus(item);

  return `
    <div class="profile-status-line">
      ${smallBadge(deliveryState.label, deliveryState.tone)}
      ${smallBadge(saveStatus, toneForBuildProfileStatus(saveStatus))}
      ${smallBadge(inProfile ? "inProfile" : "notInProfile", inProfile ? "ok" : "warn")}
    </div>
    <section class="profile-step${highlight === "resourceKey" ? " field-highlight" : ""}">
      <h3>Step 1 · 资源身份</h3>
      <label class="profile-field-inline">
        <span>ResourceKey.id</span>
        <input data-profile-field="resourceKeyId" value="${escapeHtml(resourceKeyId)}" ${inProfile ? "" : "disabled"}>
        <small class="${keyValid ? "field-ok" : "field-error"}">${keyValid ? "格式有效" : "无效字符：仅允许 a-z、0-9、.、_、-"}</small>
      </label>
      ${renderKeyValueList([
        ["type", stringValue(pick(pick(draft, "resourceKey"), "type")) || item.kind || "-"],
        ["packageId", stringValue(pick(pick(draft, "resourceKey"), "packageId")) || "-"],
        ["source provider", stringValue(pick(pick(draft, "source"), "providerId")) || item.providerId || "-"],
        ["unityGuid / assetPath", `${pick(pick(draft, "source"), "unityGuid") || item.unityGuid || "-"} / ${item.unityAssetPath || "-"}`]
      ])}
    </section>
    <section class="profile-step${highlight === "deliveryMode" ? " field-highlight" : ""}">
      <h3>Step 2 · 交付模式</h3>
      <div class="profile-form">
        <label>
          <span>delivery mode</span>
          <select data-profile-field="deliveryMode" ${inProfile ? "" : "disabled"}>
            ${selectOptions(["internal", "external", "editorOnly", "excluded"], deliveryMode)}
          </select>
        </label>
        <label>
          <span>override mode</span>
          <select data-profile-field="bundleOverrideMode" ${inProfile ? "" : "disabled"}>
            ${selectOptions(["none", "forceBundle", "forceStandalone", "forceExternal", "exclude"], pick(draft, "bundleOverrideMode") || "none")}
          </select>
        </label>
        <label>
          <span>override value</span>
          <input data-profile-field="bundleOverrideValue" value="${escapeHtml(pick(draft, "bundleOverrideValue") || "")}" ${inProfile ? "" : "disabled"}>
        </label>
      </div>
    </section>
    <section class="profile-step${highlight === "bundleRule" ? " field-highlight" : ""}">
      <h3>Step 3 · Bundle 归属</h3>
      ${isInternal ? `
        <div class="profile-form">
          <label>
            <span>bundle</span>
            <select data-profile-field="bundleRule" ${inProfile ? "" : "disabled"}>
              <option value="">未分配 Bundle</option>
              ${getBuildProfileBundleRules().map(rule => `<option value="${escapeHtml(rule.id)}"${rule.id === pick(draft, "bundleRule") ? " selected" : ""}>${escapeHtml(rule.id)} · ${escapeHtml(rule.bundleName || "-")}</option>`).join("")}
            </select>
          </label>
          <label>
            <span>bundle group hint</span>
            <input data-profile-field="bundleGroupHint" value="${escapeHtml(pick(draft, "bundleGroupHint") || "")}" ${inProfile ? "" : "disabled"}>
          </label>
          <label>
            <span>labels</span>
            <input data-profile-field="labels" value="${escapeHtml(asArray(pick(draft, "labels")).join(", "))}" ${inProfile ? "" : "disabled"}>
          </label>
        </div>` : `<p class="profile-hint">deliveryMode 为 ${escapeHtml(deliveryMode)} 时不使用 internal bundle rule。</p>`}
    </section>
    <section class="profile-step">
      <h3>Step 4 · Preload</h3>
      <div class="profile-form">
        <label>
          <span>preload groups</span>
          <input data-profile-field="preloadGroups" value="${escapeHtml(asArray(pick(draft, "preloadGroups")).join(", "))}" ${inProfile ? "" : "disabled"}>
        </label>
      </div>
      <p class="profile-hint">fail policy 由 Resource Plan 分组决定；此处只声明 preload 意图。</p>
    </section>
    <section class="profile-step">
      <h3>Step 5 · 保存状态</h3>
      ${renderKeyValueList([
        ["profile state", saveStatus],
        ["delivery outcome", deliveryState.label],
        ["full resourceKey", formatProfileResourceKey(pick(draft, "resourceKey")) || item.resourceKey || "-"]
      ])}
    </section>`;
}

function renderBuildProfileBatchEditor() {
  const checkedItems = getCheckedItems();
  const editableCount = checkedItems.filter(item => findDraftBuildProfileEntryForItem(item)).length;
  const skippedCount = checkedItems.length - editableCount;
  return `
    <p class="profile-hint">只作用于已勾选且已有 draft profile entry 的资源；未启用字段和空输入不会覆盖现有值。</p>
    <div class="profile-batch-summary">
      ${smallBadge(`checked ${checkedItems.length}`, checkedItems.length > 0 ? "info" : "warn")}
      ${smallBadge(`editable ${editableCount}`, editableCount > 0 ? "ok" : "warn")}
      ${smallBadge(`skipped ${skippedCount}`, skippedCount > 0 ? "warn" : "muted")}
    </div>
    <div class="profile-batch-form">
      ${renderBuildProfileBatchField("deliveryMode", "delivery mode", "select", ["internal", "external", "editorOnly", "excluded"])}
      ${renderBuildProfileBatchField("bundleOverrideMode", "override mode", "select", ["none", "forceStandalone", "forceExternal", "exclude"])}
      ${renderBuildProfileBatchField("bundleGroupHint", "bundle group hint")}
      ${renderBuildProfileBatchField("bundleRule", "bundle", "select", getBuildProfileBundleRules().map(rule => rule.id))}
      ${renderBuildProfileBatchField("preloadGroups", "preload groups")}
      ${renderBuildProfileBatchField("labels", "labels")}
    </div>
    <button class="profile-batch-apply" type="button" data-profile-batch-apply ${editableCount > 0 ? "" : "disabled"}>应用到已勾选 draft entries</button>`;
}

function renderBuildProfileBatchField(field, label, kind = "text", options = []) {
  const control = kind === "select"
    ? `<select data-profile-batch-field="${escapeHtml(field)}" disabled>${selectOptions(options, options[0] || "")}</select>`
    : `<input data-profile-batch-field="${escapeHtml(field)}" value="" placeholder="留空不覆盖" disabled>`;
  return `
    <label class="profile-batch-field">
      <span class="profile-batch-toggle">
        <input type="checkbox" data-profile-batch-enabled="${escapeHtml(field)}">
        <span>${escapeHtml(label)}</span>
      </span>
      ${control}
    </label>`;
}

function renderBundleActionSelect(name) {
  const bundles = getBuildProfileBundleRules();
  const selected = state.selectedBundleRuleId || bundles[0]?.id || "";
  return `
    <select data-bundle-action-select="${escapeHtml(name)}" class="bundle-action-select">
      <option value="">新建 Bundle...</option>
      ${bundles.map(rule => `<option value="${escapeHtml(rule.id)}"${rule.id === selected ? " selected" : ""}>${escapeHtml(rule.id)} · ${escapeHtml(rule.bundleName || "-")}</option>`).join("")}
    </select>`;
}

function renderBundlePlanSummary(bundles) {
  if (bundles.length === 0) return emptyBlock("Planner 尚未生成内部 bundle。");
  return `<div class="bundle-list">${bundles.slice(0, 8).map(bundle => `
    <article class="bundle-row">
      <div class="bundle-row-head">
        <strong>${escapeHtml(pick(bundle, "bundleName") || "-")}</strong>
        <span>${escapeHtml(pick(bundle, "compression") || "-")} · ${asArray(pick(bundle, "includedResourceKeys")).length} resources · rule ${escapeHtml(pick(bundle, "bundleRuleId") || "-")}</span>
      </div>
      <small>${escapeHtml(asArray(pick(bundle, "dependencyBundleNames")).join(", ") || "no dependencies")}</small>
      <ul class="bundle-plan-resources">
        ${asArray(pick(bundle, "entries")).slice(0, 12).map(entry => `<li>${escapeHtml(pick(entry, "resourceKey") || pick(entry, "resourceId") || "-")}</li>`).join("")}
      </ul>
      ${asArray(pick(bundle, "entries")).length > 12 ? `<small>另有 ${asArray(pick(bundle, "entries")).length - 12} 项</small>` : ""}
    </article>`).join("")}</div>`;
}

function renderInspector() {
  for (const tab of el.inspectorTabs) {
    tab.classList.toggle("active", tab.dataset.tab === state.activeTab);
  }

  const item = getSelectedItem();
  if (!item) {
    el.inspectorStatus.textContent = "选择一个资源项";
    el.inspectorContent.innerHTML = state.activeTab === "diagnostics"
      ? renderDiagnosticsTab(null, null)
      : emptyBlock("选择左侧资源后查看 Overview / Unity / Runtime / References / Diagnostics。");
    return;
  }

  const detail = getCurrentDetail(item);
  const fallbackText = state.inspectState.status === "fallback" ? "inspect fallback" : state.inspectState.status;
  el.inspectorStatus.textContent = `${item.libraryItemId || item.stableId || item.resourceKey} · ${fallbackText}`;

  if (state.inspectState.status === "loading") {
    el.inspectorContent.innerHTML = emptyBlock("正在读取 inspect endpoint；列表和 plan 推导详情已可用。");
    return;
  }

  const notice = state.inspectState.error
    ? `<div class="notice warn">${escapeHtml(state.inspectState.error)}</div>`
    : "";
  el.inspectorContent.innerHTML = notice + renderInspectorTab(state.activeTab, item, detail);
  renderRawJson();
}

function renderDebugPicker() {
  const items = getFilteredItems(getNormalizedItems()).slice(0, 40);
  const selectedKey = state.selectedResourceKey;
  el.debugPickerSummary.textContent = items.length === 0
    ? "Browse 工作区暂无可见资源"
    : `显示 ${items.length} 个可见资源（与 Browse 筛选同步）`;
  if (items.length === 0) {
    el.debugResourcePicker.innerHTML = emptyBlock("请先在 Browse 中加载资源");
    return;
  }
  el.debugResourcePicker.innerHTML = items.map(item => `
    <button type="button" class="debug-picker-item${item.key === selectedKey ? " active" : ""}" data-resource-key="${escapeHtml(item.key)}">
      <strong>${escapeHtml(item.displayName || item.stableId || "resource")}</strong>
      <span>${escapeHtml(item.kind || "-")} · ${escapeHtml(getBuildProfileStatus(item))}${item.diagnosticCount > 0 ? ` · 诊断 ${item.diagnosticCount}` : ""}</span>
    </button>`).join("");
}

function renderRawJson() {
  const item = getSelectedItem();
  const detail = item ? getCurrentDetail(item) : null;
  const payload = item
    ? { item: item.raw, inspect: detail, buildProfile: findBuildProfileEntryForItem(item) }
    : {
      package: state.packageRelative,
      resources: getNormalizedItems().map(resource => resource.raw),
      resourcePlan: state.resourcePlanPayload,
      buildProfile: getBuildProfile()
    };
  el.rawJsonContent.innerHTML = renderJsonBlock(payload);
}

function renderInspectorTab(tab, item, detail) {
  if (tab === "unity") return renderUnityTab(item, detail);
  if (tab === "runtime") return renderRuntimeTab(item, detail);
  if (tab === "build") return renderBuildTab(item);
  if (tab === "references") return renderReferencesTab(detail);
  if (tab === "diagnostics") return renderDiagnosticsTab(detail, item);
  return renderOverviewTab(item, detail);
}

function renderOverviewTab(item) {
  return `
    <section class="inspector-section">
      <h3>Overview</h3>
      ${renderKeyValueList([
        ["libraryItemId", item.libraryItemId || "-"],
        ["stableId", item.stableId || "-"],
        ["displayName", item.displayName || "-"],
        ["kind / usage", `${item.kind || "-"} / ${item.usage || "-"}`],
        ["tags", item.tags.length > 0 ? item.tags.join(", ") : "-"],
        ["source kind", item.sourceKind || "-"],
        ["source path", item.sourcePath || "-"],
        ["import status", item.importStatus || "-"],
        ["runtime availability", item.runtimeAvailability || "-"]
      ])}
      <p class="profile-hint">完整 JSON 见下方 Raw JSON（默认折叠）。</p>
    </section>`;
}

function renderUnityTab(item, detail) {
  const unity = detail.unity || {};
  return `
    <section class="inspector-section">
      <h3>Unity 同步状态</h3>
      ${renderKeyValueList([
        ["unityAssetGuid", pick(unity, "unityAssetGuid", "guid") || "-"],
        ["unityAssetPath", pick(unity, "unityAssetPath", "assetPath") || item.unityAssetPath || "-"],
        ["importer kind", pick(unity, "importerKind", "importer") || "-"],
        ["main object type", pick(unity, "mainObjectType", "objectType") || "-"],
        ["last import operation", pick(unity, "lastImportOperation", "lastOperation") || "-"]
      ])}
    </section>
    <section class="inspector-section">
      <h3>sub-assets</h3>
      ${renderArrayTable(asArray(pick(unity, "subAssets", "subassets")), ["name", "type", "localId", "path"])}
    </section>
    <section class="inspector-section">
      <h3>Unity diagnostics</h3>
      ${renderDiagnosticsList(asArray(pick(unity, "diagnostics", "unityDiagnostics")))}
    </section>`;
}

function renderRuntimeTab(item, detail) {
  const runtime = detail.runtime || {};
  const plans = normalizeInspectPlans(asArray(detail.plans));
  return `
    <section class="inspector-section">
      <h3>Runtime binding</h3>
      ${renderKeyValueList([
        ["runtime binding kind", pick(runtime, "runtimeBindingKind", "bindingKind") || item.runtimeBindingKind || "-"],
        ["resourceKey", pick(runtime, "resourceKey", "id") || item.resourceKey || "-"],
        ["provider id", pick(runtime, "providerId", "provider") || item.providerId || "-"],
        ["address", pick(runtime, "address", "assetPath") || "-"],
        ["asset type", pick(runtime, "assetType", "type", "typeId") || item.kind || "-"],
        ["hash", pick(runtime, "hash") || item.hash || "-"],
        ["preload policy", plans.map(plan => plan.groupName || plan.group || plan.preloadPolicy).filter(Boolean).join(", ") || "-"]
      ])}
    </section>
    <section class="inspector-section">
      <h3>included runtime plan groups</h3>
      ${renderArrayTable(plans, ["groupName", "group", "required", "failurePolicy", "resourceKey", "preloadPolicy"])}
    </section>
    <section class="inspector-section">
      <h3>runtime JSON</h3>
      ${renderJsonBlock(runtime)}
    </section>`;
}

function renderBuildTab(item) {
  const entry = findDraftBuildProfileEntryForItem(item);
  const savedEntry = findSavedBuildProfileEntryForItem(item);
  const planEntry = findBundlePlanEntryForItem(item);
  const profileStatus = getBuildProfileStatus(item);
  return `
    <section class="inspector-section">
      <h3>Global Build Profile</h3>
      ${entry ? renderKeyValueList([
        ["status", profileStatus],
        ["deliveryMode", pick(entry, "deliveryMode") || "internal"],
        ["bundleOverrideMode", pick(entry, "bundleOverrideMode") || "none"],
        ["bundleOverrideValue", pick(entry, "bundleOverrideValue") || "-"],
        ["bundleGroupHint", pick(entry, "bundleGroupHint") || "-"],
        ["bundleRule", pick(entry, "bundleRule") || "-"],
        ["preloadGroups", asArray(pick(entry, "preloadGroups")).join(", ") || "-"],
        ["labels", asArray(pick(entry, "labels")).join(", ") || "-"]
      ]) : savedEntry ? renderKeyValueList([
        ["status", profileStatus],
        ["saved deliveryMode", pick(savedEntry, "deliveryMode") || "internal"],
        ["draft", "已从草稿移除，保存后生效"]
      ]) : emptyBlock("当前资源尚未加入 Global Resource Build Profile。")}
    </section>
    <section class="inspector-section">
      <h3>Bundle Planner</h3>
      ${planEntry ? renderKeyValueList([
        ["bundleName", pick(planEntry, "bundleName") || "-"],
        ["reason", pick(planEntry, "reason") || "-"],
        ["deliveryMode", pick(planEntry, "deliveryMode") || "-"],
        ["sourceProviderId", pick(planEntry, "sourceProviderId") || "-"]
      ]) : emptyBlock("当前资源没有 planner 输出。")}
    </section>
    <section class="inspector-section">
      <h3>Profile entry JSON</h3>
      ${renderJsonBlock(entry || {})}
    </section>`;
}

function normalizeInspectPlans(plans) {
  return plans.map(plan => {
    const resource = pick(plan, "resource") || {};
    return {
      ...resource,
      ...plan,
      groupName: stringValue(pick(plan, "groupName", "group")) || stringValue(pick(resource, "groupName", "group")),
      resourceKey: stringValue(pick(plan, "resourceKey", "id", "cue", "bank"))
        || stringValue(pick(resource, "resourceKey", "id", "cue", "bank"))
    };
  });
}

function renderReferencesTab(detail) {
  const references = asArray(detail.references);
  return `
    <section class="inspector-section">
      <h3>References</h3>
      ${references.length === 0 ? emptyBlock("没有引用关系。") : renderArrayTable(references, [
        "sourceConfigKind",
        "sourceStableId",
        "sourceField",
        "targetLibraryItemStableId",
        "targetResourceKey",
        "bindingKind",
        "isRequiredAtRuntime",
        "preloadPolicy"
      ])}
    </section>`;
}

function renderDiagnosticsTab(detail, item) {
  const globalDiagnostics = getAllDiagnostics();
  const itemDiagnostics = detail ? asArray(detail.diagnostics) : [];
  return `
    <section class="inspector-section diagnostics-overview">
      <h3>Global Diagnostics</h3>
      ${renderDiagnosticsSummary(globalDiagnostics)}
      ${renderDiagnosticsList(globalDiagnostics)}
    </section>
    <section class="inspector-section">
      <h3>${item ? "当前资源 Diagnostics" : "当前资源 Diagnostics"}</h3>
      ${item ? `<p class="profile-hint">${escapeHtml(item.displayName || item.stableId || item.resourceKey || "当前资源")}</p>` : ""}
      ${renderDiagnosticsList(itemDiagnostics)}
    </section>`;
}

function renderContextActions() {
  const item = getSelectedItem();
  const preset = getSelectedImportPreset();
  const connected = Boolean(state.resourcesPayload);
  const writeBusy = state.writeState.status === "running";
  const profileReady = Boolean(getBuildProfile());
  const profileEntry = item ? findDraftBuildProfileEntryForItem(item) : null;
  const checkedItems = getCheckedItems();
  const checkedAddable = checkedItems.some(checkedItem => !findDraftBuildProfileEntryForItem(checkedItem));
  const checkedRemovable = checkedItems.some(checkedItem => Boolean(findDraftBuildProfileEntryForItem(checkedItem)));

  el.importResourceButton.disabled = !connected || writeBusy;
  el.importFolderButton.disabled = !connected || writeBusy;
  el.reimportResourceButton.disabled = !connected || !item || writeBusy;
  el.replaceSourceButton.disabled = !connected || !item || writeBusy;
  el.saveBuildProfileButton.disabled = !profileReady || writeBusy;
  el.saveBuildProfileButton.title = "通过 Authoring API 校验并保存 Global Resource Build Profile";

  el.browseContextActions.innerHTML = item ? `
    ${renderBundleActionSelect("selectedBundleTarget")}
    <button type="button" data-action="assign-selected-bundle" class="primary-action"${!profileReady || writeBusy ? " disabled" : ""}>加入到 Bundle</button>
    <button type="button" data-action="add-profile" class="secondary"${profileEntry || !profileReady || writeBusy ? " disabled" : ""}>仅加入 Profile</button>
    <button type="button" data-action="open-profile" class="primary-action secondary-action"${!profileReady || writeBusy ? " disabled" : ""}>编辑 Profile</button>
    <button type="button" data-action="view-debug" class="secondary">Debug 详情</button>
    <button type="button" data-action="remove-profile" class="secondary"${!profileEntry || writeBusy ? " disabled" : ""}>移出</button>
  ` : `<span class="action-group-label">← 请先在左侧列表选择资源</span>`;

  el.profileContextActions.innerHTML = `
    <span class="action-group-label">Profile 操作</span>
    <button type="button" data-bundle-action="create-bundle" class="primary-action"${!profileReady || writeBusy ? " disabled" : ""}>新建 Bundle</button>
    ${renderBundleActionSelect("profileSelectedBundleTarget")}
    <button type="button" data-action="assign-selected-bundle"${!item || !profileReady || writeBusy ? " disabled" : ""}>当前资源加入 Bundle</button>
    <button type="button" data-action="add-profile"${!item || profileEntry || !profileReady || writeBusy ? " disabled" : ""}>仅加入 Profile</button>
    <button type="button" data-action="remove-profile"${!item || !profileEntry || writeBusy ? " disabled" : ""}>移出 Profile</button>
    <button type="button" data-action="revert-profile" class="secondary"${!item || writeBusy ? " disabled" : ""}>还原当前资源草稿</button>
  `;

  if (state.writeState.status === "running") {
    el.writeActionStatus.textContent = "写入中：正在通过 Authoring API 更新资源库。";
  } else if (state.writeState.status === "error") {
    el.writeActionStatus.textContent = `写入失败：${state.writeState.error}`;
  } else if (item) {
    el.writeActionStatus.textContent = `导入类型：${preset.label}；目标：${item.displayName || getResourceWriteId(item)}；Profile：${getBuildProfileStatus(item)}。`;
  } else {
    el.writeActionStatus.textContent = `导入类型：${preset.label}。`;
  }

  el.copyDetailJsonButton.disabled = false;
  el.copyDiagnosticsJsonButton.disabled = false;
  el.copyDetailJsonButton.title = item ? "复制当前资源 inspect/fallback 详情" : "复制当前资源库摘要";
  el.copyDiagnosticsJsonButton.title = item ? "复制当前资源诊断" : "复制当前资源库全部诊断";

  void checkedAddable;
  void checkedRemovable;
}

function getDeliveryEntryState(item, entry) {
  const mode = entry ? pick(entry, "deliveryMode") : "notInProfile";
  if (!entry) return { label: "not in profile", tone: "neutral" };
  if (mode === "external") return { label: "external", tone: "warn" };
  if (mode === "editorOnly") return { label: "editor only", tone: "info" };
  if (mode === "excluded") return { label: "excluded", tone: "error" };
  const override = pick(entry, "bundleOverrideMode");
  if (override === "exclude") return { label: "excluded", tone: "error" };
  if (override === "forceExternal") return { label: "external", tone: "warn" };
  if (mode === "internal" && override !== "forceStandalone" && override !== "forceBundle") {
    const ruleId = pick(entry, "bundleRule") || "";
    if (!ruleId) return { label: "needs bundle", tone: "warn" };
    if (!findBundleRuleById(ruleId)) return { label: "missing bundle", tone: "error" };
  }
  return { label: "will build", tone: "ok" };
}

function inferProfileValidationField(payload) {
  const issues = asArray(pick(pick(payload, "validation"), "issues"));
  const message = issues.map(issue => String(pick(issue, "message") || pick(issue, "code") || "")).join(" ").toLowerCase();
  if (message.includes("resourcekey") || message.includes("invalid characters")) return "resourceKey";
  if (message.includes("bundle rule")) return "bundleRule";
  if (message.includes("delivery")) return "deliveryMode";
  return "";
}

function renderImportPresetOptions() {
  const selected = IMPORT_PRESETS.some(preset => preset.id === state.selectedImportPreset)
    ? state.selectedImportPreset
    : IMPORT_PRESETS[0].id;
  state.selectedImportPreset = selected;
  el.importPresetSelect.innerHTML = IMPORT_PRESETS
    .map(preset => `<option value="${escapeHtml(preset.id)}"${preset.id === selected ? " selected" : ""}>${escapeHtml(preset.label)}</option>`)
    .join("");
  syncImportAccept();
}

function syncImportAccept() {
  const preset = getSelectedImportPreset();
  const accept = preset.extensions.map(extension => `.${extension}`).join(",");
  el.resourceImportFileInput.accept = accept;
  el.resourceImportFolderInput.accept = accept;
}

function getResourceWriteId(item) {
  return item?.libraryItemId || item?.stableId || item?.resourceKey || item?.key || "";
}

function selectionMatchesItem(item, selection) {
  return Boolean(
    item.key === selection ||
    item.libraryItemId === selection ||
    item.stableId === selection ||
    item.resourceKey === selection
  );
}

function getSelectedImportPreset() {
  return IMPORT_PRESETS.find(preset => preset.id === state.selectedImportPreset) || IMPORT_PRESETS[0];
}

function isFileSupportedByPreset(file, preset) {
  const format = inferFormatFromFileName(file.name);
  return preset.extensions.includes(format);
}

function isIgnoredImportFile(file) {
  const filePath = getImportDisplayPath(file);
  const name = String(file?.name || filePath).toLowerCase();
  if (!name) return true;
  if (name.endsWith(".meta") || name === ".ds_store" || name === "thumbs.db") return true;
  return filePath.split(/[\\/]+/).some(segment => segment.startsWith(".") && segment !== ".");
}

function formatFolderImportCountSuffix(skipped, ignored, preset) {
  const parts = [`类型 ${preset?.label || "自动识别"}`];
  if (skipped > 0) parts.push(`跳过非匹配 ${skipped}`);
  if (ignored > 0) parts.push(`忽略元数据 ${ignored}`);
  return parts.length > 0 ? `，${parts.join("，")}` : "";
}

function inferFormatFromFileName(fileName) {
  const value = String(fileName || "");
  const index = value.lastIndexOf(".");
  return index >= 0 ? value.slice(index + 1).toLowerCase() : "";
}

function inferModelRole(item) {
  if (!item || item.kind !== "model") return "";
  if (item.usage === "characterModel") return "body";
  if (item.tags.includes("mainHand") || item.resourceKey.includes("mainhand")) return "mainHand";
  if (item.tags.includes("offHand") || item.resourceKey.includes("offhand")) return "offHand";
  return "preview";
}

function buildFolderLocalId(file, preset) {
  const path = getImportDisplayPath(file).replace(/\.[^.]+$/, "");
  const segments = path.split(/[\\/]+/).map(normalizeLocalIdSegment).filter(Boolean);
  const suffix = segments.join(".");
  return suffix || "";
}

function normalizeLocalIdSegment(value) {
  return String(value || "")
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, "$1-$2")
    .replace(/[^a-zA-Z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .toLowerCase();
}

function getImportDisplayPath(file) {
  return file.webkitRelativePath || file.name || "";
}

function readFileAsBase64(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result || "");
      resolve(result.includes(",") ? result.split(",").pop() : result);
    };
    reader.onerror = () => reject(reader.error || new Error("读取文件失败"));
    reader.readAsDataURL(file);
  });
}

function getNormalizedItems() {
  const items = asArray(pick(state.resourcesPayload, "items", "entries", "Items", "Entries"));
  return items.map((raw, index) => normalizeItem(raw, index));
}

function getBuildProfile() {
  return state.buildProfileDraft || pick(state.buildProfilePayload, "profile") || null;
}

function ensureBuildProfileDraft() {
  if (!state.buildProfileDraft) {
    state.buildProfileDraft = structuredCloneCompat(pick(state.buildProfilePayload, "profile") || {
      schemaVersion: 1,
      profileId: "global.default",
      catalogId: "global.runtime",
      entries: [],
      bundleRules: [],
      preloadGroups: []
    });
  }
  if (!Array.isArray(state.buildProfileDraft.entries)) state.buildProfileDraft.entries = [];
  if (!Array.isArray(state.buildProfileDraft.bundleRules)) state.buildProfileDraft.bundleRules = [];
  return state.buildProfileDraft;
}

function markBuildProfileDirty() {
  state.buildProfileDirty = true;
}

function getBuildProfileEntries() {
  return asArray(pick(getBuildProfile(), "entries"));
}

function getBuildProfileBundleRules() {
  return asArray(pick(getBuildProfile(), "bundleRules")).filter(Boolean);
}

function findBundleRuleById(id) {
  if (!id) return null;
  return getBuildProfileBundleRules().find(rule => rule.id === id) || null;
}

function getSelectedBundleRule() {
  return findBundleRuleById(state.selectedBundleRuleId) || getBuildProfileBundleRules()[0] || null;
}

function ensureSelectedBundleRuleId() {
  const rules = getBuildProfileBundleRules();
  if (rules.length === 0) {
    state.selectedBundleRuleId = "";
    return;
  }
  if (!state.selectedBundleRuleId || !rules.some(rule => rule.id === state.selectedBundleRuleId)) {
    state.selectedBundleRuleId = rules[0].id || "";
  }
}

function getSavedBuildProfileEntries() {
  return asArray(pick(pick(state.buildProfilePayload, "profile"), "entries"));
}

function getDraftBuildProfileEntries() {
  return asArray(pick(state.buildProfileDraft, "entries"));
}

function getBundlePlan() {
  return pick(state.bundlePlanPayload, "plan") || null;
}

function findBuildProfileEntryForItem(item) {
  return findDraftBuildProfileEntryForItem(item);
}

function findDraftBuildProfileEntryForItem(item) {
  return getDraftBuildProfileEntries().find(entry => profileEntryMatchesItem(entry, item)) || null;
}

function findSavedBuildProfileEntryForItem(item) {
  return getSavedBuildProfileEntries().find(entry => profileEntryMatchesItem(entry, item)) || null;
}

function profileEntryMatchesItem(entry, item) {
  const key = pick(entry, "resourceKey") || {};
  const source = pick(entry, "source") || {};
  const entryCanonical = normalizeProfileResourceKey(key);
  const itemCanonical = normalizeProfileResourceKey(buildProfileResourceKeyFromItem(item));
  if (entryCanonical && itemCanonical) return entryCanonical === itemCanonical;

  const sourceRuntimeKey = stringValue(pick(source, "runtimeResourceKey", "providerResourceKey", "packageResourceKey"));
  return Boolean(
    (sourceRuntimeKey && [item.resourceKey, item.runtimeResourceKey, item.providerResourceKey, item.packageResourceKey].includes(sourceRuntimeKey))
    || (source.unityGuid && item.unityGuid && source.unityGuid === item.unityGuid)
    || (source.unityAssetPath && item.unityAssetPath && source.unityAssetPath === item.unityAssetPath)
  );
}

function findBundlePlanEntryForItem(item) {
  const plan = getBundlePlan();
  const candidates = [
    ...asArray(pick(plan, "externalEntries")),
    ...asArray(pick(plan, "excludedEntries")),
    ...asArray(pick(plan, "bundles")).flatMap(bundle => asArray(pick(bundle, "entries")))
  ];
  return candidates.find(entry => {
    const key = stringValue(pick(entry, "resourceKey"));
    const id = stringValue(pick(entry, "resourceId"));
    return matchesItem(item, "", key, id) || id === item.resourceKey || id === item.stableId;
  }) || null;
}

function buildProfileEntryFromItem(item) {
  const resourceKey = buildProfileResourceKeyFromItem(item);
  const detail = getCurrentDetail(item);
  const authoring = pick(detail, "authoring") || {};
  const runtime = pick(detail, "runtime") || {};
  const authoringBindings = asArray(pick(authoring, "providerBindings"));
  const primaryBinding = getPrimaryProviderBinding(authoringBindings.length > 0 ? authoringBindings : item.providerBindings);
  const providerData = pick(runtime, "providerData") || pick(primaryBinding, "providerData") || item.metadata || {};
  const preloadGroups = getRuntimePreloadGroupsForItem(item);
  const domainLabel = item.kind ? `domain.${normalizeLabelSegment(item.kind)}` : "domain.resource";
  const bundleHint = preloadGroups.length > 0
    ? normalizeLabelSegment(preloadGroups[0])
    : item.usage ? normalizeLabelSegment(item.usage) : item.kind ? normalizeLabelSegment(item.kind) : "misc";
  const deliveryMode = inferDeliveryModeForItem(item);
  const isInternalDelivery = deliveryMode === "internal";
  return {
    resourceKey,
    source: {
      providerId: item.providerId || "unknown",
      unityAssetPath: item.unityAssetPath || "",
      unityGuid: item.unityGuid || "",
      runtimeResourceKey: stringValue(pick(runtime, "runtimeResourceKey", "resourceKey")) || item.runtimeResourceKey || item.resourceKey || "",
      externalSourcePath: stringValue(pick(authoring, "sourcePath")) || item.sourcePath || "",
      providerData
    },
    labels: [domainLabel],
    bundleRule: "",
    deliveryMode,
    bundleOverrideMode: "none",
    bundleOverrideValue: "",
    bundleGroupHint: isInternalDelivery ? bundleHint : "",
    preloadGroups,
    dependencies: [],
    providerData: {},
    runtimeLoadable: isRuntimeLoadable(item) || isRuntimeRequired(item),
    editorOnly: String(item.runtimeAvailability || "").toLowerCase().includes("editor")
  };
}

function buildDraftBuildProfileEntryForItem(item) {
  const savedEntry = findSavedBuildProfileEntryForItem(item);
  return savedEntry ? structuredCloneCompat(savedEntry) : buildProfileEntryFromItem(item);
}

function buildProfileResourceKeyFromItem(item) {
  const detail = getCurrentDetail(item);
  const runtime = pick(detail, "runtime") || {};
  const planResource = getPrimaryRuntimePlanResource(item);
  const providerData = pick(runtime, "providerData") || item.metadata || {};
  const parsed = parseProfileResourceKey(item.resourceKey || item.runtimeResourceKey || item.providerResourceKey || item.packageResourceKey || "");
  const type = stringValue(pick(planResource, "typeId", "type")) || parsed.type || stringValue(pick(runtime, "assetType")) || item.kind || "Object";
  const id = firstValidProfileResourceKeyId(
    stringValue(pick(planResource, "resourceKey", "id")),
    stringValue(pick(runtime, "resourceKey", "runtimeResourceKey", "providerResourceKey", "packageResourceKey")),
    parsed.id,
    stringValue(pick(providerData, "runtimeResourceKey", "providerResourceKey", "packageResourceKey")),
    item.runtimeResourceKey,
    item.providerResourceKey,
    item.packageResourceKey,
    item.stableId,
    item.libraryItemId,
    item.displayName,
    item.resourceId
  );
  return {
    id,
    type,
    typeId: type,
    variant: stringValue(pick(planResource, "variant")) || parsed.variant || stringValue(pick(providerData, "variant")),
    packageId: stringValue(pick(planResource, "packageId")) || parsed.packageId || stringValue(pick(providerData, "packageId")) || getSelectedPackageLabel()
  };
}

function inferDeliveryModeForItem(item) {
  if (isRuntimeRequired(item)) return "internal";
  if (!isRuntimeLoadable(item)) return "excluded";
  if (["runtimeCatalog", "fmod", "externalImportStaging"].includes(item.providerId)) return "external";
  return "internal";
}

function getRuntimePreloadGroupsForItem(item) {
  return Array.from(new Set(asArray(pick(getCurrentDetail(item), "plans"))
    .map(plan => stringValue(pick(plan, "groupName", "group", "preloadPolicy")))
    .filter(Boolean)));
}

function getPrimaryRuntimePlanResource(item) {
  for (const plan of asArray(pick(getCurrentDetail(item), "plans"))) {
    const resource = pick(plan, "resource") || plan;
    if (pick(resource, "resourceKey", "id")) return resource;
  }
  return {};
}

function isRuntimeRequired(item) {
  const detail = getCurrentDetail(item);
  if (asArray(pick(detail, "plans")).some(plan => pick(plan, "required") === true)) return true;
  return asArray(pick(detail, "references")).some(reference => pick(reference, "isRequiredAtRuntime") === true);
}

function getBuildProfileStatus(item) {
  const savedEntry = findSavedBuildProfileEntryForItem(item);
  const draftEntry = findDraftBuildProfileEntryForItem(item);
  if (!savedEntry && !draftEntry) return "notInProfile";
  if (!savedEntry && draftEntry) return "draftOnly";
  if (savedEntry && !draftEntry) return "removedInDraft";
  if (!profileEntriesEqual(savedEntry, draftEntry)) return "modifiedInDraft";
  return "saved";
}

function countBuildProfileStates(items) {
  const counts = {
    notInProfile: 0,
    saved: 0,
    draftOnly: 0,
    removedInDraft: 0,
    modifiedInDraft: 0
  };
  for (const item of items) {
    const status = getBuildProfileStatus(item);
    counts[status] = (counts[status] || 0) + 1;
  }
  return counts;
}

function getBundleRuleMemberRows(rule) {
  if (!rule) return [];
  const items = getNormalizedItems();
  return getDraftBuildProfileEntries()
    .map((entry, index) => ({ entry, index, item: items.find(item => profileEntryMatchesItem(entry, item)) || null }))
    .filter(row => pick(row.entry, "bundleRule") === rule.id)
    .map(row => ({
      ...row,
      key: formatProfileResourceKey(pick(row.entry, "resourceKey")) || pick(pick(row.entry, "resourceKey"), "id") || String(row.index)
    }));
}

function filterBundleMemberRows(rows) {
  const needle = state.bundleMemberSearch.trim().toLowerCase();
  if (!needle) return rows;
  return rows.filter(row => {
    const item = row.item;
    const text = [
      row.key,
      item?.displayName,
      item?.resourceKey,
      item?.sourcePath,
      item?.unityAssetPath,
      pick(pick(row.entry, "source"), "unityAssetPath")
    ].filter(Boolean).join(" ").toLowerCase();
    return text.includes(needle);
  });
}

function getBundleRuleStats(rule) {
  const rows = getBundleRuleMemberRows(rule);
  const planBundle = getBundlePlanBundleForRule(rule);
  const externalCount = rows.filter(row => pick(row.entry, "deliveryMode") !== "internal").length;
  const plannedInternalCount = asArray(pick(planBundle, "entries")).length;
  const missingUnityGuidCount = rows.filter(row => {
    const provider = pick(pick(row.entry, "source"), "providerId");
    return provider === "unityAssetDatabase" && !pick(pick(row.entry, "source"), "unityGuid");
  }).length;
  const diagnosticsCount = rows.reduce((sum, row) => sum + (row.item?.diagnosticCount || 0), 0)
    + asArray(pick(planBundle, "diagnostics")).length;
  return {
    memberCount: rows.length,
    internalCount: Math.max(rows.length - externalCount, plannedInternalCount),
    externalCount,
    missingUnityGuidCount,
    diagnosticsCount,
    dependencyBundleNames: asArray(pick(planBundle, "dependencyBundleNames"))
  };
}

function getBundlePlanBundleForRule(rule) {
  if (!rule) return null;
  const bundles = asArray(pick(getBundlePlan(), "bundles"));
  return bundles.find(bundle => pick(bundle, "bundleRuleId") === rule.id || pick(bundle, "bundleName") === rule.bundleName) || null;
}

function countUnassignedInternalEntries() {
  const rules = new Set(getBuildProfileBundleRules().map(rule => rule.id));
  return getDraftBuildProfileEntries().filter(entry => {
    const mode = pick(entry, "deliveryMode") || "internal";
    const override = pick(entry, "bundleOverrideMode") || "none";
    if (mode !== "internal") return false;
    if (override === "forceStandalone" || override === "forceBundle") return false;
    const bundleRule = pick(entry, "bundleRule") || "";
    return !bundleRule || !rules.has(bundleRule);
  }).length;
}

function handleBundleProfileAction(button) {
  const action = button.dataset.bundleAction;
  if (action === "create-bundle") {
    createBundleRuleFromPrompt();
    return;
  }
  if (action === "select-bundle") {
    state.selectedBundleRuleId = button.dataset.bundleRuleId || "";
    state.bundleMemberSearch = "";
    render();
    return;
  }
  if (action === "assign-checked-selected-bundle") {
    assignCheckedToBundle(state.selectedBundleRuleId);
    return;
  }
  if (action === "remove-entry-bundle") {
    clearEntryBundleAssignment(Number(button.dataset.entryIndex));
    return;
  }
  if (action === "delete-selected-bundle") {
    deleteSelectedBundleRule();
  }
}

function clearEntryBundleAssignment(index) {
  const profile = getBuildProfile();
  const entry = profile?.entries?.[index];
  if (!entry) return;
  entry.bundleRule = "";
  markBuildProfileDirty();
  state.lastActionMessage = "Removed resource from current Bundle.";
  render();
}

function deleteSelectedBundleRule() {
  const profile = getBuildProfile();
  const id = state.selectedBundleRuleId;
  if (!profile || !Array.isArray(profile.bundleRules) || !id) return;
  if (!window.confirm(`Delete Bundle definition ${id}? Resource entries will keep no bundle assignment.`)) return;
  profile.bundleRules = profile.bundleRules.filter(rule => rule.id !== id);
  for (const entry of getDraftBuildProfileEntries()) {
    if (entry.bundleRule === id) entry.bundleRule = "";
  }
  state.selectedBundleRuleId = "";
  markBuildProfileDirty();
  state.lastActionMessage = `Deleted Bundle ${id}.`;
  render();
}

function updateSelectedBundleRuleField(field, value, renderFeedback) {
  const rule = getSelectedBundleRule();
  if (!rule) return;
  const previousId = rule.id;
  if (field === "id") {
    const nextId = normalizeBundleRuleId(value);
    if (!nextId) return;
    rule.id = nextId;
    for (const entry of getDraftBuildProfileEntries()) {
      if (entry.bundleRule === previousId) entry.bundleRule = nextId;
    }
    state.selectedBundleRuleId = nextId;
  } else if (["matchLabels", "matchDomains", "matchPackageIds"].includes(field)) {
    rule[field] = splitCsv(value);
  } else if (field === "includeDependencies" || field === "allowEmpty") {
    rule[field] = Boolean(value);
  } else if (field === "bundleName") {
    rule.bundleName = normalizeBundleName(value);
  } else {
    rule[field] = value;
  }
  markBuildProfileDirty();
  if (renderFeedback) render();
}

function readControlValue(control) {
  if (control.type === "checkbox") return control.checked;
  return control.value;
}

function toneForBuildProfileStatus(status) {
  if (status === "saved") return "ok";
  if (status === "draftOnly" || status === "modifiedInDraft") return "info";
  if (status === "removedInDraft") return "warn";
  if (status === "conflict" || status === "missing") return "error";
  return "neutral";
}

function profileEntriesEqual(left, right) {
  return stableJson(left || {}) === stableJson(right || {});
}

function normalizeItem(raw, index) {
  const resourceId = stringValue(pick(raw, "resourceId"));
  const providerBindings = asArray(pick(raw, "providerBindings"));
  const primaryBinding = getPrimaryProviderBinding(providerBindings);
  const primaryProviderData = pick(primaryBinding, "providerData") || {};
  const metadata = pick(raw, "metadata") || {};
  const libraryItemId = stringValue(pick(raw, "libraryItemId", "id", "localId")) || resourceId;
  const stableId = stringValue(pick(raw, "stableId", "libraryItemStableId", "resourceStableId"));
  const runtimeResourceKey = stringValue(pick(raw, "runtimeResourceKey")) || stringValue(pick(primaryBinding, "runtimeResourceKey"));
  const providerResourceKey = stringValue(pick(raw, "providerResourceKey")) || stringValue(pick(primaryBinding, "providerResourceKey"));
  const packageResourceKey = stringValue(pick(raw, "packageResourceKey")) || stringValue(pick(primaryBinding, "packageResourceKey"));
  const resourceKey = stringValue(pick(raw, "resourceKey", "key")) || runtimeResourceKey || packageResourceKey || providerResourceKey;
  const key = resourceId || libraryItemId || stableId || resourceKey || `resource-${index}`;
  const tags = asArray(pick(raw, "tags")).map(String).filter(Boolean);
  const preview = pick(raw, "preview") || {};
  const diagnostics = asArray(pick(raw, "diagnostics"));
  const item = {
    key,
    raw,
    resourceId,
    libraryItemId,
    stableId,
    displayName: stringValue(pick(raw, "displayName", "name", "localId")) || libraryItemId || resourceKey,
    kind: stringValue(pick(raw, "kind", "resourceKind", "typeId", "type")),
    usage: stringValue(pick(raw, "usage", "expectedUsage")),
    sourceKind: stringValue(pick(raw, "sourceKind")),
    runtimeBindingKind: stringValue(pick(raw, "runtimeBindingKind", "bindingKind")),
    importStatus: stringValue(pick(raw, "importStatus", "status")),
    runtimeAvailability: stringValue(pick(raw, "runtimeAvailability", "runtimeStatus")),
    resourceKey,
    runtimeResourceKey,
    providerResourceKey,
    packageResourceKey,
    providerId: stringValue(pick(raw, "providerId", "provider", "sourceProviderId")) || stringValue(pick(primaryBinding, "providerId")),
    hash: stringValue(pick(raw, "hash", "contentHash")) || stringValue(pick(primaryBinding, "hash")) || stringValue(pick(metadata, "contentHash")),
    sourcePath: stringValue(pick(raw, "sourcePath", "relativePath", "path")) || stringValue(pick(metadata, "relativePath")) || stringValue(pick(primaryBinding, "externalSourcePath")),
    unityAssetPath: stringValue(pick(raw, "unityAssetPath")) || stringValue(pick(primaryBinding, "unityAssetPath")),
    unityGuid: stringValue(pick(raw, "unityGuid")) || stringValue(pick(primaryBinding, "unityGuid")),
    fmodEventPath: stringValue(pick(raw, "fmodEventPath")) || stringValue(pick(primaryBinding, "fmodEventPath")),
    audioCueId: stringValue(pick(raw, "audioCueId")) || stringValue(pick(primaryProviderData, "audioCueId")),
    audioEventDefinitionId: stringValue(pick(raw, "audioEventDefinitionId")) || stringValue(pick(primaryProviderData, "audioEventDefinitionId")),
    providerBindings,
    metadata,
    tags,
    preview,
    diagnostics
  };
  item.references = getReferencesForItem(item);
  item.referenceCount = numericValue(pick(raw, "referenceCount"), item.references.length);
  item.allDiagnostics = getDiagnosticsForItem(item);
  item.diagnosticCount = numericValue(pick(raw, "diagnosticCount"), item.allDiagnostics.length);
  item.isOrphan = isOrphanCandidate(item);
  return item;
}

function getFilteredItems(items) {
  const search = state.filters.search.trim().toLowerCase();
  return items.filter(item => {
    if (state.quickFilter === "profileDraft") {
      const status = getBuildProfileStatus(item);
      if (!["draftOnly", "modifiedInDraft", "removedInDraft"].includes(status)) return false;
    }
    if (state.filters.kind !== "all" && item.kind !== state.filters.kind) return false;
    if (state.filters.usage !== "all" && item.usage !== state.filters.usage) return false;
    if (state.filters.providerId !== "all" && item.providerId !== state.filters.providerId) return false;
    if (state.filters.sourceKind !== "all" && item.sourceKind !== state.filters.sourceKind) return false;
    if (state.filters.importStatus !== "all" && item.importStatus !== state.filters.importStatus) return false;
    if (state.filters.runtimeAvailability !== "all" && item.runtimeAvailability !== state.filters.runtimeAvailability) return false;
    const profileStatus = getBuildProfileStatus(item);
    if (state.filters.profileMembership === "inProfile" && profileStatus === "notInProfile") return false;
    if (!["all", "inProfile"].includes(state.filters.profileMembership) && profileStatus !== state.filters.profileMembership) return false;
    if (state.filters.runtimeReady === "runtimeReady" && !isRuntimeReadyCandidate(item)) return false;
    if (state.filters.runtimeReady === "notRuntimeReady" && isRuntimeReadyCandidate(item)) return false;
    if (state.filters.tag !== "all" && !item.tags.includes(state.filters.tag)) return false;
    if (state.filters.onlyReferenced && item.referenceCount === 0) return false;
    if (state.filters.onlyOrphan && !item.isOrphan) return false;
    if (state.filters.onlyRuntimeLoadable && !isRuntimeLoadable(item)) return false;
    if (state.filters.onlyDiagnostics && item.diagnosticCount === 0) return false;
    if (!search) return true;
    return [
      item.libraryItemId,
      item.stableId,
      item.displayName,
      item.kind,
      item.usage,
      item.providerId,
      item.sourceKind,
      item.importStatus,
      item.runtimeAvailability,
      item.resourceKey,
      item.runtimeResourceKey,
      item.providerResourceKey,
      item.packageResourceKey,
      item.sourcePath,
      item.unityAssetPath,
      item.tags.join(" ")
    ].join(" ").toLowerCase().includes(search);
  });
}

function getSelectedItem() {
  return getNormalizedItems().find(item => item.key === state.selectedResourceKey) || null;
}

function getCurrentDetail(item) {
  if (state.inspectState.payload && state.inspectState.id && inspectStateMatchesItem(item)) {
    return normalizeInspectPayload(state.inspectState.payload, item);
  }
  return buildFallbackInspect(item);
}

function inspectStateMatchesItem(item) {
  const id = state.inspectState.id || "";
  return Boolean(item && (
    item.key === id
    || item.libraryItemId === id
    || item.stableId === id
    || item.resourceKey === id
    || item.resourceId === id
  ));
}

function normalizeInspectPayload(payload, item) {
  const fallback = buildFallbackInspect(item);
  return {
    packageId: pick(payload, "packageId") || fallback.packageId,
    item: pick(payload, "item") || item.raw,
    authoring: pick(payload, "authoring") || fallback.authoring,
    unity: pick(payload, "unity") || fallback.unity,
    runtime: pick(payload, "runtime") || fallback.runtime,
    references: asArray(pick(payload, "references")).length > 0 ? asArray(pick(payload, "references")) : fallback.references,
    plans: asArray(pick(payload, "plans")).length > 0 ? asArray(pick(payload, "plans")) : fallback.plans,
    diagnostics: mergeDiagnostics(asArray(pick(payload, "diagnostics")), fallback.diagnostics),
    raw: payload
  };
}

function buildFallbackInspect(item) {
  const runtimeEntry = findRuntimeCatalogEntry(item);
  const plans = getPlanMembership(item);
  return {
    packageId: pick(state.resourcesPayload, "packageId") || pick(state.resourcePlanPayload, "packageId") || getSelectedPackageLabel(),
    item: item.raw,
    authoring: {
      sourcePath: item.sourcePath,
      tags: item.tags,
      sourceKind: item.sourceKind
    },
    unity: {
      unityAssetPath: item.unityAssetPath,
      importStatus: item.importStatus,
      diagnostics: item.allDiagnostics.filter(diagnostic => String(pick(diagnostic, "code", "Code")).includes("UNITY"))
    },
    runtime: {
      runtimeBindingKind: item.runtimeBindingKind,
      runtimeAvailability: item.runtimeAvailability,
      resourceKey: item.resourceKey,
      providerId: item.providerId || pick(runtimeEntry, "provider"),
      address: pick(runtimeEntry, "address"),
      assetType: pick(runtimeEntry, "type", "typeId"),
      hash: item.hash || pick(runtimeEntry, "hash"),
      runtimeCatalogEntry: runtimeEntry || null
    },
    references: item.references,
    plans,
    diagnostics: item.allDiagnostics,
    fallback: true
  };
}

function getReferencesForItem(item) {
  const graph = pick(state.resourcesPayload, "referenceGraph") || {};
  const edges = asArray(pick(graph, "edges"));
  return edges.filter(edge => {
    const targetStableId = stringValue(pick(edge, "targetLibraryItemStableId", "targetStableId", "targetResourceStableId"));
    const targetResourceKey = stringValue(pick(edge, "targetResourceKey", "resourceKey", "targetProviderResourceKey", "targetRuntimeResourceKey"));
    const targetResourceId = stringValue(pick(edge, "targetResourceId"));
    return matchesItem(item, targetStableId, targetResourceKey, targetResourceId);
  });
}

function getDiagnosticsForItem(item) {
  const diagnostics = [];
  diagnostics.push(...item.diagnostics);
  for (const diagnostic of getAllDiagnostics()) {
    const stableId = stringValue(pick(diagnostic, "libraryItemStableId", "resourceStableId", "targetLibraryItemStableId"));
    const resourceKey = stringValue(pick(diagnostic, "resourceKey", "runtimeResourceKey", "targetResourceKey"));
    const resourceId = stringValue(pick(diagnostic, "resourceId", "targetResourceId"));
    if (matchesItem(item, stableId, resourceKey, resourceId)) {
      diagnostics.push(diagnostic);
    }
  }
  return mergeDiagnostics(diagnostics, []);
}

function getAllDiagnostics() {
  const diagnostics = [];
  const resourcesDiagnostics = asArray(pick(state.resourcesPayload, "diagnostics"));
  const planDiagnostics = asArray(pick(getPlanDocument(), "diagnostics"));
  const reportDiagnostics = asArray(pick(getValidationReport(), "diagnostics"));
  const buildProfileDiagnostics = asArray(pick(pick(state.buildProfilePayload, "validation"), "issues"));
  const bundlePlanDiagnostics = asArray(pick(getBundlePlan(), "diagnostics"));
  diagnostics.push(...resourcesDiagnostics, ...planDiagnostics, ...reportDiagnostics, ...buildProfileDiagnostics, ...bundlePlanDiagnostics);
  for (const error of state.errors) {
    diagnostics.push({
      severity: "Error",
      code: "RESOURCE_LIBRARY_API_UNAVAILABLE",
      message: `${error.label}: ${error.message}`,
      suggestedFix: "确认 Authoring 服务正在运行，并且当前路径在仓库根目录内。"
    });
  }
  return mergeDiagnostics(diagnostics, []);
}

function getPlanDocument() {
  return pick(state.resourcePlanPayload, "characterResourcePlan", "plan") || null;
}

function getRuntimeCatalog() {
  return pick(state.resourcePlanPayload, "runtimeResourceCatalog") || {};
}

function getValidationReport() {
  return pick(state.resourcePlanPayload, "resourceValidationReport") || {};
}

function getPlanStatus() {
  return stringValue(pick(getValidationReport(), "status")) || (state.resourcePlanPayload ? "Unknown" : "Missing");
}

function getPlanGroups() {
  const plan = getPlanDocument();
  if (!plan) return [];
  return PLAN_GROUPS.map(([key, label]) => {
    const group = pick(plan, key);
    if (!group) return null;
    const resources = getPlanGroupResources(group, label);
    return {
      key,
      label,
      required: pick(group, "required") === true ? "必需" : pick(group, "required") === false ? "可选" : "-",
      failurePolicy: stringValue(pick(group, "failurePolicy")),
      resources
    };
  }).filter(Boolean);
}

function getPlanGroupResources(group, label) {
  if (Array.isArray(group)) {
    return group.map(resourceKey => ({ group: label, resourceKey: String(resourceKey) }));
  }
  const resources = asArray(pick(group, "resources")).map(resource => {
    if (typeof resource === "string") return { group: label, resourceKey: resource };
    return { group: label, ...resource };
  });
  if (label === "Audio") {
    resources.push(...asArray(pick(group, "requiredCues")).map(cue => ({ group: label, cue: String(cue), resourceKey: String(cue), preloadPolicy: "Audio" })));
    resources.push(...asArray(pick(group, "requiredBanks")).map(bank => ({ group: label, bank: String(bank), resourceKey: String(bank), preloadPolicy: "AudioBank" })));
  }
  return resources.map(resource => ({
    required: pick(group, "required") === true ? "true" : pick(group, "required") === false ? "false" : "",
    failurePolicy: stringValue(pick(group, "failurePolicy")),
    preloadPolicy: label,
    ...resource
  }));
}

function getPlanMembership(item) {
  const groups = getPlanGroups();
  const matches = [];
  for (const group of groups) {
    for (const resource of group.resources) {
      if (matchesItem(item, stringValue(pick(resource, "stableId")), stringValue(pick(resource, "resourceKey", "id", "cue", "bank")))) {
        matches.push({ group: group.label, required: group.required, failurePolicy: group.failurePolicy, ...resource });
      }
    }
  }
  return matches;
}

function findRuntimeCatalogEntry(item) {
  const entries = asArray(pick(getRuntimeCatalog(), "entries"));
  return entries.find(entry => {
    const providerData = pick(entry, "providerData") || {};
    return matchesItem(
      item,
      stringValue(pick(providerData, "stableId")),
      stringValue(pick(entry, "id", "resourceKey")) || stringValue(pick(providerData, "packageResourceKey"))
    );
  }) || null;
}

function isRuntimeLoadable(item) {
  const availability = String(item.runtimeAvailability || "").toLowerCase();
  if (availability === "runtimeready" || availability === "audiocueonly") return true;
  if (availability.includes("missing") || availability.includes("editoronly") || availability.includes("previewonly") || availability.includes("notruntime")) return false;
  const binding = String(item.runtimeBindingKind || "").toLowerCase();
  return binding === "resourcemanagerasset" || binding === "audiocue" || binding === "audioeventdefinition";
}

function isRuntimeReadyCandidate(item) {
  if (isRuntimeLoadable(item)) return true;
  if (String(item.runtimeAvailability || "").toLowerCase() === "runtimeready") return true;
  return item.providerBindings.some(binding => {
    const runtimeKey = stringValue(pick(binding, "runtimeResourceKey"));
    const bindingKind = String(pick(binding, "runtimeBindingKind", "bindingKind") || "").toLowerCase();
    return Boolean(runtimeKey || bindingKind === "resourcemanagerasset" || bindingKind === "audiocue" || bindingKind === "audioeventdefinition");
  });
}

function isOrphanCandidate(item) {
  if (String(item.importStatus).toLowerCase() === "orphancandidate") return true;
  if (item.allDiagnostics.some(d => String(pick(d, "code")).toLowerCase().includes("orphan"))) return true;
  const graph = pick(state.resourcesPayload, "referenceGraph") || {};
  return asArray(pick(graph, "edges")).length > 0 && item.referenceCount === 0;
}

function matchesItem(item, stableId, resourceKey, resourceId = "") {
  return Boolean(
    (resourceId && item.resourceId && resourceId === item.resourceId)
    || (stableId && item.stableId && stableId === item.stableId)
    || (resourceKey && item.resourceKey && resourceKey === item.resourceKey)
    || (resourceKey && item.runtimeResourceKey && resourceKey === item.runtimeResourceKey)
    || (resourceKey && item.providerResourceKey && resourceKey === item.providerResourceKey)
    || (resourceKey && item.packageResourceKey && resourceKey === item.packageResourceKey)
    || (resourceKey && item.libraryItemId && resourceKey === item.libraryItemId)
  );
}

function getPrimaryProviderBinding(bindings) {
  const rows = asArray(bindings);
  return rows.find(binding => pick(binding, "isPrimary") === true) || rows[0] || {};
}

function buildOptions(items, key) {
  const values = Array.from(new Set(items.map(item => item[key]).filter(Boolean))).sort(compareText);
  return [["all", "全部"], ...values.map(value => [value, value])];
}

function getProviders() {
  return asArray(pick(state.resourcesPayload, "providers", "Providers")).map(raw => ({
    providerId: stringValue(pick(raw, "providerId", "ProviderId", "id")) || "unknown",
    displayName: stringValue(pick(raw, "displayName", "DisplayName", "name")) || stringValue(pick(raw, "providerId", "ProviderId", "id")) || "unknown",
    sourceKind: stringValue(pick(raw, "sourceKind", "SourceKind")),
    available: pick(raw, "available", "Available") !== false,
    status: stringValue(pick(raw, "status", "Status")) || "Unknown",
    diagnosticCode: stringValue(pick(raw, "diagnosticCode", "DiagnosticCode")),
    message: stringValue(pick(raw, "message", "Message"))
  }));
}

function renderProviderList(providers) {
  return `<ul class="compact-list">${providers.map(provider => `
    <li>
      <span>${escapeHtml(provider.displayName)}</span>
      <strong>${escapeHtml(provider.available ? provider.status : provider.diagnosticCode || provider.status)}</strong>
    </li>`).join("")}</ul>`;
}

function buildTagOptions(items) {
  const tags = Array.from(new Set(items.flatMap(item => item.tags))).sort(compareText);
  return [["all", "全部"], ...tags.map(tag => [tag, tag])];
}

function setSelectOptions(select, options, selected) {
  const hasSelected = options.some(([value]) => value === selected);
  const valueToUse = hasSelected ? selected : "all";
  select.innerHTML = options.map(([value, label]) => `<option value="${escapeHtml(value)}"${value === valueToUse ? " selected" : ""}>${escapeHtml(label)}</option>`).join("");
  if (!hasSelected) {
    const key = selectToFilterKey(select.id);
    if (key) state.filters[key] = "all";
  }
}

function selectToFilterKey(id) {
  return {
    kindFilter: "kind",
    usageFilter: "usage",
    providerFilter: "providerId",
    sourceFilter: "sourceKind",
    importFilter: "importStatus",
    runtimeFilter: "runtimeAvailability",
    profileMembershipFilter: "profileMembership",
    runtimeReadyFilter: "runtimeReady",
    tagFilter: "tag"
  }[id] || "";
}

function applyFilterControls() {
  el.searchInput.value = state.filters.search;
  el.profileMembershipFilter.value = state.filters.profileMembership;
  el.runtimeReadyFilter.value = state.filters.runtimeReady;
  el.resourceSortSelect.value = state.resourceSort;
  el.treeGroupModeSelect.value = state.treeGroupMode;
  el.onlyReferenced.checked = state.filters.onlyReferenced;
  el.onlyOrphan.checked = state.filters.onlyOrphan;
  el.onlyRuntimeLoadable.checked = state.filters.onlyRuntimeLoadable;
  el.onlyDiagnostics.checked = state.filters.onlyDiagnostics;
}

function copyDetailJson() {
  const item = getSelectedItem();
  const detail = item ? getCurrentDetail(item) : {
    package: state.packageRelative,
    resources: getNormalizedItems().map(resource => resource.raw),
    resourcePlan: state.resourcePlanPayload
  };
  copyText(JSON.stringify(detail, null, 2), "已复制详情 JSON");
}

function copyDiagnosticsJson() {
  const item = getSelectedItem();
  const diagnostics = item ? getCurrentDetail(item).diagnostics : getAllDiagnostics();
  copyText(JSON.stringify({
    package: state.packageRelative,
    resource: item ? {
      libraryItemId: item.libraryItemId,
      stableId: item.stableId,
      resourceKey: item.resourceKey
    } : null,
    diagnostics
  }, null, 2), "已复制诊断 JSON");
}

async function copyText(text, successMessage) {
  try {
    await navigator.clipboard.writeText(text);
    setCopyStatus(successMessage);
  } catch {
    window.prompt("复制 JSON", text);
    setCopyStatus("已打开复制窗口");
  }
}

function setCopyStatus(message) {
  el.copyStatus.textContent = message;
  window.setTimeout(() => {
    if (el.copyStatus.textContent === message) {
      el.copyStatus.textContent = "";
    }
  }, 1600);
}

function renderKeyValueList(rows) {
  return `<dl class="kv-list">${rows.map(([key, value]) => `
    <div>
      <dt>${escapeHtml(key)}</dt>
      <dd>${escapeHtml(value ?? "-")}</dd>
    </div>`).join("")}</dl>`;
}

function renderArrayTable(rows, keys) {
  const data = asArray(rows);
  if (data.length === 0) return emptyBlock("暂无数据");
  return `
    <div class="table-wrap">
      <table>
        <thead><tr>${keys.map(key => `<th>${escapeHtml(key)}</th>`).join("")}</tr></thead>
        <tbody>
          ${data.map(row => `<tr>${keys.map(key => `<td>${escapeHtml(formatCell(pick(row, key)))}</td>`).join("")}</tr>`).join("")}
        </tbody>
      </table>
    </div>`;
}

function renderDiagnosticsList(diagnostics) {
  const rows = asArray(diagnostics);
  if (rows.length === 0) return emptyBlock("没有诊断信息。");
  return `<div class="diagnostics-list">${rows.map(diagnostic => {
    const severity = getSeverity(diagnostic);
    const source = formatDiagnosticSource(diagnostic);
    return `
      <article class="diagnostic-row ${severity.toLowerCase()}">
        <div>
          <strong>${escapeHtml(severity)}</strong>
          <code>${escapeHtml(pick(diagnostic, "code") || "-")}</code>
        </div>
        <p>${escapeHtml(pick(diagnostic, "message") || pick(diagnostic, "description") || JSON.stringify(diagnostic))}</p>
        <small>${escapeHtml(source)}</small>
        ${pick(diagnostic, "suggestedFix") ? `<small class="diagnostic-fix">${escapeHtml(pick(diagnostic, "suggestedFix"))}</small>` : ""}
      </article>`;
  }).join("")}</div>`;
}

function renderDiagnosticsSummary(diagnostics) {
  const rows = asArray(diagnostics);
  if (rows.length === 0) return emptyBlock("没有全局诊断。");
  const severityCounts = {};
  const codeCounts = {};
  for (const diagnostic of rows) {
    const severity = getSeverity(diagnostic);
    const code = pick(diagnostic, "code") || "UNKNOWN";
    severityCounts[severity] = (severityCounts[severity] || 0) + 1;
    codeCounts[code] = (codeCounts[code] || 0) + 1;
  }
  return `
    <div class="diagnostics-summary">
      ${metric("total", rows.length)}
      ${metric("errors", severityCounts.Error || 0)}
      ${metric("warnings", severityCounts.Warning || 0)}
      ${metric("info", severityCounts.Info || 0)}
    </div>
    <details class="diagnostics-code-summary">
      <summary>按诊断代码汇总</summary>
      ${renderCountList(codeCounts, "没有诊断代码。")}
    </details>`;
}

function formatDiagnosticSource(diagnostic) {
  const parts = [
    pick(diagnostic, "providerId"),
    pick(diagnostic, "resourceKey", "runtimeResourceKey", "targetResourceKey"),
    pick(diagnostic, "resourceStableId", "libraryItemStableId", "targetLibraryItemStableId"),
    pick(diagnostic, "sourceStableId"),
    pick(diagnostic, "sourceField")
  ].map(value => stringValue(value)).filter(Boolean);
  return parts.length > 0 ? parts.join(" · ") : "-";
}

function renderJsonBlock(value) {
  return `<pre class="json-block">${escapeHtml(JSON.stringify(value ?? {}, null, 2))}</pre>`;
}

function renderCountList(map, emptyText) {
  const entries = Object.entries(map).sort((a, b) => b[1] - a[1] || compareText(a[0], b[0]));
  if (entries.length === 0) return emptyBlock(emptyText);
  return `<ul class="count-list">${entries.map(([key, count]) => `<li><span>${escapeHtml(key || "unknown")}</span><strong>${count}</strong></li>`).join("")}</ul>`;
}

function countBy(items, key) {
  const result = {};
  for (const item of items) {
    const value = item[key] || "unknown";
    result[value] = (result[value] || 0) + 1;
  }
  return result;
}

function metric(label, value) {
  return `<div class="metric"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></div>`;
}

function smallBadge(text, tone) {
  return `<span class="small-badge ${tone || "neutral"}">${escapeHtml(text)}</span>`;
}

function statusChip(label, value, tone, action = "") {
  const attrs = action
    ? ` data-status-action="${escapeHtml(action)}" title="点击查看诊断详情"`
    : "";
  const tag = action ? "button" : "div";
  return `<${tag} class="status-chip ${tone || "neutral"}${action ? " clickable" : ""}"${attrs}><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></${tag}>`;
}

function emptyBlock(text) {
  return `<div class="empty">${escapeHtml(text)}</div>`;
}

function toneForImportStatus(status) {
  const value = String(status || "").toLowerCase();
  if (value === "clean") return "ok";
  if (value.includes("failed") || value.includes("missing") || value.includes("conflict")) return "error";
  if (value.includes("changed") || value.includes("new") || value.includes("manual") || value.includes("orphan")) return "warn";
  return "neutral";
}

function toneForRuntime(status) {
  const value = String(status || "").toLowerCase();
  if (value === "runtimeready" || value === "audiocueonly") return "ok";
  if (value.includes("missing") || value.includes("notruntime")) return "error";
  if (value.includes("editor") || value.includes("preview") || value.includes("unknown")) return "warn";
  return "neutral";
}

function getSeverity(diagnostic) {
  const raw = String(pick(diagnostic, "severity", "level", "type") || "Info").toLowerCase();
  if (raw.includes("error")) return "Error";
  if (raw.includes("warn")) return "Warning";
  return "Info";
}

function getKindInitial(kind) {
  const value = String(kind || "R").trim();
  return value ? value.slice(0, 2).toUpperCase() : "R";
}

function kindTitle(kind) {
  const value = String(kind || "").toLowerCase();
  if (value.includes("model")) return "模型资源";
  if (value.includes("animation")) return "动画资源";
  if (value.includes("audio")) return "音频资源";
  if (value.includes("texture")) return "贴图资源";
  if (value.includes("material")) return "材质资源";
  if (value.includes("vfx")) return "VFX 资源";
  if (value.includes("config")) return "配置资源";
  if (value.includes("generated") || value.includes("preview")) return "生成资源";
  return "资源项";
}

function getSelectedPackageLabel() {
  const pkg = state.packages.find(item => item.relative === state.packageRelative);
  return pkg ? formatPackageContextLabel(pkg) : state.packageRelative;
}

function formatPackageContextLabel(pkg) {
  if (!pkg) return state.packageRelative;
  const kind = pkg.kind || "Character";
  const id = pkg.packageId || pkg.relative || "context";
  const version = pkg.version ? ` ${pkg.version}` : "";
  return `${kind} · ${id}${version}`;
}

function syncPackageQuery() {
  const url = new URL(window.location.href);
  url.searchParams.set("package", state.packageRelative);
  window.history.replaceState({}, "", url.toString());
}

function apiError(label, error) {
  return { label, message: error?.message || String(error) };
}

function pick(source, ...keys) {
  if (!source) return undefined;
  for (const key of keys) {
    if (Object.prototype.hasOwnProperty.call(source, key)) return source[key];
    const pascal = key.charAt(0).toUpperCase() + key.slice(1);
    if (Object.prototype.hasOwnProperty.call(source, pascal)) return source[pascal];
  }
  return undefined;
}

function asArray(value) {
  return Array.isArray(value) ? value : [];
}

function stringValue(value) {
  if (value === null || value === undefined) return "";
  return String(value);
}

function numericValue(value, fallback) {
  const number = Number(value);
  return Number.isFinite(number) ? number : fallback;
}

function formatCell(value) {
  if (value === null || value === undefined || value === "") return "-";
  if (Array.isArray(value)) return value.join(", ");
  if (typeof value === "object") return JSON.stringify(value);
  return String(value);
}

function formatProfileResourceKey(key) {
  if (!key) return "";
  const packageId = stringValue(pick(key, "packageId"));
  const type = stringValue(pick(key, "type", "typeId"));
  const id = stringValue(pick(key, "id"));
  const variant = stringValue(pick(key, "variant"));
  return `${packageId}:${type}:${id}:${variant}`;
}

function normalizeProfileResourceKey(key) {
  if (!key) return "";
  const parsed = typeof key === "string" ? parseProfileResourceKey(key) : key;
  const id = stringValue(pick(parsed, "id")).trim();
  const type = stringValue(pick(parsed, "type", "typeId")).trim();
  if (!id || !type) return "";
  return [
    stringValue(pick(parsed, "packageId")).trim(),
    type,
    id,
    stringValue(pick(parsed, "variant")).trim()
  ].join(":");
}

function stableJson(value) {
  if (Array.isArray(value)) return `[${value.map(stableJson).join(",")}]`;
  if (value && typeof value === "object") {
    return `{${Object.keys(value).sort().map(key => `${JSON.stringify(key)}:${stableJson(value[key])}`).join(",")}}`;
  }
  return JSON.stringify(value);
}

function parseProfileResourceKey(value) {
  const text = String(value || "");
  const parts = text.split(":");
  if (parts.length === 4) {
    return {
      packageId: parts[0] || "",
      type: parts[1] || "",
      id: parts[2] || "",
      variant: parts[3] || ""
    };
  }

  return {
    packageId: "",
    type: "",
    id: text,
    variant: ""
  };
}

function firstValidProfileResourceKeyId(...values) {
  for (const value of values) {
    const text = stringValue(value).trim();
    if (isValidProfileResourceKeyId(text)) return text;
  }

  for (const value of values) {
    const normalized = normalizeProfileResourceKeyId(value);
    if (normalized) return normalized;
  }

  return "resource.unnamed";
}

function isValidProfileResourceKeyId(value) {
  return /^[a-z0-9._-]+$/.test(String(value || ""));
}

function normalizeProfileResourceKeyId(value) {
  return String(value || "")
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, "$1.$2")
    .replace(/[^a-zA-Z0-9._-]+/g, ".")
    .replace(/^\.+|\.+$/g, "")
    .toLowerCase();
}

function formatValidationSummary(payload) {
  const issues = asArray(pick(pick(payload, "validation"), "issues"));
  if (issues.length === 0) return "";
  return issues.slice(0, 2).map(issue => pick(issue, "message") || pick(issue, "code") || "validation issue").join("; ");
}

function selectOptions(values, selected) {
  return values.map(value => `<option value="${escapeHtml(value)}"${value === selected ? " selected" : ""}>${escapeHtml(value)}</option>`).join("");
}

function splitCsv(value) {
  return String(value || "")
    .split(",")
    .map(part => part.trim())
    .filter(Boolean);
}

function suggestBundleRuleId() {
  const item = getSelectedItem();
  const preload = item ? getRuntimePreloadGroupsForItem(item)[0] : "";
  const basis = preload || item?.usage || item?.kind || "new.bundle";
  return normalizeBundleRuleId(basis) || "new.bundle";
}

function normalizeBundleRuleId(value) {
  return normalizeBundleNameSegment(value).replace(/^global\./, "").replace(/\.assetbundle$/, "");
}

function normalizeBundleName(value) {
  const raw = String(value || "").trim();
  if (!raw) return "global.misc.assetbundle";
  return normalizeBundleNameSegment(raw);
}

function normalizeBundleNameSegment(value) {
  const text = String(value || "").trim().toLowerCase();
  let result = "";
  let lastWasSeparator = false;
  for (const char of text) {
    const allowed = (char >= "a" && char <= "z") || (char >= "0" && char <= "9");
    if (allowed) {
      result += char;
      lastWasSeparator = false;
      continue;
    }
    if ((char === "." || char === "_" || char === "-" || /\s/.test(char)) && !lastWasSeparator) {
      result += ".";
      lastWasSeparator = true;
    }
  }
  result = result.replace(/^\.+|\.+$/g, "");
  return result || "misc";
}

function cssEscapeCompat(value) {
  if (window.CSS?.escape) return window.CSS.escape(value);
  return String(value || "").replace(/"/g, "\\\"");
}

function normalizeLabelSegment(value) {
  return String(value || "misc")
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, "$1-$2")
    .replace(/[^a-zA-Z0-9]+/g, ".")
    .replace(/^\.+|\.+$/g, "")
    .toLowerCase() || "misc";
}

function structuredCloneCompat(value) {
  if (typeof structuredClone === "function") return structuredClone(value);
  return JSON.parse(JSON.stringify(value ?? null));
}

function mergeDiagnostics(primary, secondary) {
  const seen = new Set();
  const result = [];
  for (const diagnostic of [...asArray(primary), ...asArray(secondary)]) {
    const key = JSON.stringify([
      pick(diagnostic, "severity"),
      pick(diagnostic, "code"),
      pick(diagnostic, "libraryItemStableId"),
      pick(diagnostic, "resourceKey"),
      pick(diagnostic, "sourceField"),
      pick(diagnostic, "message")
    ]);
    if (seen.has(key)) continue;
    seen.add(key);
    result.push(diagnostic);
  }
  return result;
}

function compareText(a, b) {
  return String(a).localeCompare(String(b), "zh-Hans-CN");
}

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, ch => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "\"": "&quot;",
    "'": "&#39;"
  })[ch]);
}
