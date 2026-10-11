// Resource Library split module.
// Core state, boot, loading, shared render flow, Story/Art glue, and common helpers.
// Loaded by index.html as a classic script; keep shared bindings global.
const DEFAULT_PACKAGE = "Tools/MxFramework.Authoring/samples/character-codex-flow-test";

const API = {
  packages: "/api/character/packages",
  resources: packageRelative => `/api/authoring/resources?package=${encodeURIComponent(packageRelative)}`,
  resourcePlan: (packageRelative, checkHashes = false) => {
    const suffix = checkHashes ? "&checkHashes=true" : "";
    return `/api/authoring/resources/resource-plan?package=${encodeURIComponent(packageRelative)}${suffix}`;
  },
  inspect: (packageRelative, id) => `/api/authoring/resources/inspect?package=${encodeURIComponent(packageRelative)}&id=${encodeURIComponent(id)}`,
  audioSemantics: packageRelative => `/api/authoring/resources/audio-semantics?package=${encodeURIComponent(packageRelative)}`,
  saveAudioSemantics: "/api/authoring/resources/audio-semantics/save",
  validateAudioReplacement: "/api/authoring/resources/audio-semantics/validate-replacement",
  buildProfile: buildTarget => `/api/authoring/resources/resource-mapping?buildTarget=${encodeURIComponent(buildTarget)}`,
  saveBuildProfile: "/api/authoring/resources/resource-mapping/manual/save",
  updateArtTask: "/api/authoring/resources/art-tasks/update",
  createArtTask: "/api/authoring/resources/art-tasks/create",
  updateStoryPacket: "/api/authoring/resources/story-packets/update",
  approveStoryPacket: "/api/authoring/resources/story-packets/approve",
  updateStoryCanon: "/api/authoring/resources/story-canon/update",
  updateStoryOutline: "/api/authoring/resources/story-outlines/update",
  approveStoryOutline: "/api/authoring/resources/story-outlines/approve-for-packet",
  bundlePlan: buildTarget => `/api/authoring/resources/resource-mapping/plan?buildTarget=${encodeURIComponent(buildTarget)}`,
  buildStatus: packageRelative => `/api/authoring/resources/global-build-status?package=${encodeURIComponent(packageRelative)}`,
  resourceMapping: buildTarget => `/api/authoring/resources/resource-mapping?buildTarget=${encodeURIComponent(buildTarget)}`,
  stageImport: "/api/authoring/resources/stage-import",
  importResource: "/api/authoring/resources/import",
  reimportResource: "/api/authoring/resources/reimport",
  replaceSource: "/api/authoring/resources/replace-source",
  commonResources: "/api/authoring/common-resources",
  syncCommonResources: "/api/authoring/common-resources/sync",
  saveCommonResource: "/api/authoring/common-resources/save"
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

const RESOURCE_FILE_EXTENSION_PATTERN = /\.(anim|asset|controller|overridecontroller|prefab|mat|json|glb|gltf|fbx|png|jpg|jpeg|tga|wav|ogg|mp3)$/i;

const RESOURCE_PROVIDER_PRIORITY = {
  characterPackage: 900,
  storySource: 870,
  artSource: 860,
  characterAction: 790,
  resourceMapping: 780,
  runtimeCatalog: 720,
  unityAssetDatabase: 420,
  unityProjectAssets: 120,
  fmod: 650,
  externalImportStaging: 80
};

const WORKSPACES = ["story", "art", "browse", "audio", "profile", "references", "graph", "build", "debug"];
const REFERENCES_EDUCATION_STORAGE_KEY = "mx.resourceLibrary.referencesEducationVisible.v1";
const GRAPH_NODE_LIMIT = 80;

const UNITY_WORKBENCH_GUIDANCE = [
  "Unity 菜单执行 Player 资源构建：",
  "1. MxFramework/Resources/Validate Global Resource Mapping",
  "2. MxFramework/Resources/Build Global Player Resources",
  "或打开：MxFramework/Resources/Open Global AssetBundle Builder"
].join("\n");

const state = {
  packages: [],
  packageRelative: DEFAULT_PACKAGE,
  resourcesPayload: null,
  resourcePlanPayload: null,
  commonResources: null,
  commonResourcesSyncing: false,
  commonResourceSaveState: { status: "idle", error: "" },
  buildProfilePayload: null,
  buildProfileDraft: null,
  bundlePlanPayload: null,
  buildStatusPayload: null,
  resourceMappingPayload: null,
  selectedResourceKey: "",
  storySelectedKey: "",
  storySelectedKeys: { canon: "", outline: "", packet: "" },
  storyPacketDrafts: {},
  storyPacketSaveState: { status: "idle", storySourceId: "", error: "", message: "" },
  storyCanonDrafts: {},
  storyCanonSaveState: { status: "idle", canonId: "", error: "", message: "" },
  storyOutlineDrafts: {},
  storyOutlineSaveState: { status: "idle", outlineId: "", error: "", message: "" },
  artSelectedKey: "",
  artFilters: window.MxResourceArtSourceStatus?.createDefaultFilters?.() || {
    search: "",
    family: "all",
    stage: "all",
    reviewStatus: "all",
    needsAction: "all"
  },
  artGraphZoom: 1,
  activeWorkspace: "story",
  artTaskDrafts: {},
  artTaskSaveState: { status: "idle", taskId: "", error: "", message: "" },
  artTaskCreateDraft: window.MxResourceArtSourceStatus?.createDefaultCreateDraft?.() || { kind: "task" },
  artTaskCreateState: { status: "idle", kind: "", taskId: "", workstreamId: "", error: "", message: "" },
  artUiPanels: { graph: false, create: false, filters: false },
  artImagePreview: null,
  artGraphDrag: null,
  activeTab: "overview",
  audioFilters: {
    search: "",
    bank: "all",
    spatial: "all",
    loop: "all",
    diagnostics: "all",
    referenced: "all"
  },
  audioSemanticPayload: null,
  audioSemanticApiState: "unavailable",
  audioSemanticApiError: "",
  audioInspectorTab: "overview",
  audioCueDetailExpanded: false,
  audioSemanticDrafts: {},
  audioSemanticSaveState: { status: "idle", audioCueId: "", error: "", message: "" },
  audioReplacementValidation: { status: "idle", audioCueId: "", payload: null, error: "" },
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
  referencesFilter: "current",
  referencesBundleRuleId: "",
  referencesSearch: "",
  referencesLastPatch: null,
  referencesEducationVisible: true,
  graphScope: "current",
  graphSearch: "",
  graphSelectedNodeId: "",
  graphShowOptional: true,
  graphColorMode: "recommendation",
  selectedImportPreset: "modelPreview",
  characterStudioReturn: { fieldKey: "", returnKind: "", usage: "", kind: "" },
  profileFieldHighlight: "",
  errors: [],
  lastActionMessage: "",
  buildProfileDirty: false,
  derivedCache: {
    normalizedSignature: null,
    normalizedItems: null,
    browserSource: null,
    browserItems: null
  },
  browserItemsByKey: new Map()
};

const el = {};

document.addEventListener("DOMContentLoaded", () => {
  cacheElements();
  const query = new URLSearchParams(window.location.search);
  const queryPackage = query.get("package");
  if (queryPackage) {
    state.packageRelative = queryPackage;
  }
  const queryWorkspace = query.get("workspace") || "";
  if (WORKSPACES.includes(queryWorkspace)) {
    state.activeWorkspace = queryWorkspace;
  }
  const querySelect = query.get("select") || "";
  if (querySelect) {
    state.selectedResourceKey = querySelect;
  }
  state.characterStudioReturn = {
    fieldKey: query.get("returnField") || "",
    returnKind: query.get("returnKind") || "",
    usage: query.get("usage") || "",
    kind: query.get("kind") || ""
  };
  state.referencesEducationVisible = loadReferenceEducationPreference();

  bindEvents();
  setWorkspace(state.activeWorkspace);
  loadAll();
});

function cacheElements() {
  for (const id of [
    "serverStatus", "packageSelect", "refreshButton", "openCharacterStudioButton",
    "saveBuildProfileButton", "openUnityWorkbenchButton", "statusStrip", "resourceSummary",
    "storyWorkspace",
    "artWorkspace",
    "audioWorkspace", "audioSummary", "audioSearchInput", "audioBankFilter", "audioSpatialFilter",
    "audioLoopFilter", "audioDiagnosticsFilter", "audioReferencedFilter", "audioProviderSummary",
    "audioBankList", "audioCueList", "audioImpactPanel", "openAudioBrowseButton", "copyAudioSummaryButton",
    "searchInput", "kindFilter", "usageFilter", "providerFilter", "sourceFilter", "importFilter",
    "runtimeFilter", "profileMembershipFilter", "runtimeReadyFilter", "tagFilter", "onlyReferenced", "onlyOrphan",
    "onlyRuntimeLoadable", "onlyDiagnostics", "clearFiltersButton", "resourceSortSelect", "activeFiltersBar",
    "advancedFilterBadge", "advancedFilters", "selectVisibleButton",
    "clearCheckedButton", "checkedSummary", "treeGroupModeSelect", "expandTreeButton",
    "collapseTreeButton", "resourceTree", "resourceListHeading", "resourceList",
    "browseWorkflowAssistant", "browsePanelTitle", "browsePanelSubtitle", "browseContextBody", "browseContextActions", "browseBatchBar",
    "profileWorkflowAssistant", "buildProfileSummary", "buildProfileContent", "profileContextActions", "profileBatchBar",
    "referencesReviewBar", "referencesSummary", "referencesScopeSearch", "referencesScopeHint", "referencesScopeExplanation", "referencesTargetList", "referencesGroupSummary", "referencesContent", "referencesAdviceSummary", "referencesAdviceContent",
    "graphSummary", "graphSearchInput", "graphShowOptional", "graphColorMode", "graphLegend", "graphViewportSummary", "graphFitButton", "copyGraphSummaryButton", "graphNotice", "graphCanvas", "graphInspectorSummary", "graphInspectorContent",
    "resourcePlanPanel", "planSummary", "planGrid", "bundlePlanSummary", "buildChecklist",
    "buildWorkflowAssistant", "bundlePlanContent", "buildDiagnostics", "validateButton", "refreshBundlePlanButton",
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
  el.referencesFilterButtons = Array.from(document.querySelectorAll("[data-refs-filter]"));
  el.graphScopeButtons = Array.from(document.querySelectorAll("[data-graph-scope]"));
  el.inspectorTabs = Array.from(document.querySelectorAll(".inspector-tabs button[data-tab]"));
}

function loadReferenceEducationPreference() {
  try {
    return window.localStorage?.getItem(REFERENCES_EDUCATION_STORAGE_KEY) !== "hidden";
  } catch {
    return true;
  }
}

function saveReferenceEducationPreference(visible) {
  try {
    window.localStorage?.setItem(REFERENCES_EDUCATION_STORAGE_KEY, visible ? "visible" : "hidden");
  } catch {
    // Local preference only. If storage is unavailable, keep the in-memory state.
  }
}

function bindEvents() {
  el.refreshButton.addEventListener("click", () => loadAll({ keepPackages: false }));
  el.packageSelect.addEventListener("change", event => {
    state.packageRelative = event.target.value;
    state.selectedResourceKey = "";
    state.storySelectedKey = "";
    state.storySelectedKeys = { canon: "", outline: "", packet: "" };
    state.storyPacketDrafts = {};
    state.storyPacketSaveState = { status: "idle", storySourceId: "", error: "", message: "" };
    state.artSelectedKey = "";
    state.inspectState = { id: "", status: "idle", payload: null, error: "" };
    state.inspectCache.clear();
    state.referencesBundleRuleId = "";
    state.referencesLastPatch = null;
    state.graphSelectedNodeId = "";
    state.errors = [];
    syncPackageQuery();
    loadPackageData();
  });

  el.validateButton.addEventListener("click", runResourceValidation);
  el.openUnityWorkbenchButton.addEventListener("click", showUnityWorkbenchGuidance);
  el.refreshBundlePlanButton.addEventListener("click", async () => {
    await loadBuildStatus();
    await Promise.all([loadBundlePlan(), loadBuildProfile()]);
    await loadResourceMapping();
    render();
  });
  el.copyBuildReportButton.addEventListener("click", copyBuildReport);
  el.bundlePlanContent.addEventListener("click", event => {
    const button = event.target.closest("[data-guide-action]");
    if (!button || button.disabled) return;
    handleGuideAction(button.dataset.guideAction);
  });

  for (const button of el.workspaceNavButtons) {
    button.addEventListener("click", () => {
      if (button.dataset.workspace === "debug" && getAllDiagnostics().length > 0) {
        state.activeTab = "diagnostics";
      }
      if (button.dataset.workspace === "references") {
        state.referencesBundleRuleId = "";
      }
      setWorkspace(button.dataset.workspace);
      render();
    });
  }

  if (el.artWorkspace) {
    el.artWorkspace.addEventListener("input", handleArtWorkspaceEvent);
    el.artWorkspace.addEventListener("change", handleArtWorkspaceEvent);
    el.artWorkspace.addEventListener("click", handleArtWorkspaceEvent);
    el.artWorkspace.addEventListener("pointerdown", handleArtGraphPointerDown);
    el.artWorkspace.addEventListener("mousedown", handleArtGraphMouseDown);
    el.artWorkspace.addEventListener("toggle", handleArtWorkspaceEvent, true);
  }

  if (el.storyWorkspace) {
    el.storyWorkspace.addEventListener("input", handleStoryWorkspaceEvent);
    el.storyWorkspace.addEventListener("change", handleStoryWorkspaceEvent);
    el.storyWorkspace.addEventListener("click", handleStoryWorkspaceEvent);
  }

  if (el.audioWorkspace) {
    el.audioWorkspace.addEventListener("input", handleAudioWorkspaceEvent);
    el.audioWorkspace.addEventListener("change", handleAudioWorkspaceEvent);
    el.audioWorkspace.addEventListener("click", handleAudioWorkspaceEvent);
  }
  document.addEventListener("pointermove", handleArtGraphPointerMove);
  document.addEventListener("pointerup", endArtGraphDrag);
  document.addEventListener("pointercancel", endArtGraphDrag);
  document.addEventListener("mousemove", handleArtGraphMouseMove);
  document.addEventListener("mouseup", endArtGraphMouseDrag);
  document.addEventListener("keydown", event => {
    if (typeof handleAudioKeyboardShortcut === "function" && handleAudioKeyboardShortcut(event)) return;
    if (event.key === "Escape" && state.artImagePreview) {
      state.artImagePreview = null;
      renderArtWorkspace();
    }
  });

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

  for (const button of el.referencesFilterButtons) {
    button.addEventListener("click", () => {
      state.referencesFilter = button.dataset.refsFilter || "current";
      renderReferencesWorkspace();
    });
  }
  el.referencesScopeSearch.addEventListener("input", event => {
    state.referencesSearch = event.target.value;
    renderReferencesWorkspace();
  });

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
  for (const root of [el.browseWorkflowAssistant, el.profileWorkflowAssistant, el.buildWorkflowAssistant]) {
    root.addEventListener("click", event => {
      const button = event.target.closest("[data-guide-action]");
      if (!button || button.disabled) return;
      handleGuideAction(button.dataset.guideAction);
    });
  }

  el.browseContextActions.addEventListener("click", handleContextActionClick);
  el.browseContextBody.addEventListener("click", handleContextActionClick);
  el.profileContextActions.addEventListener("click", handleContextActionClick);
  el.browseBatchBar.addEventListener("click", handleContextActionClick);
  el.profileBatchBar.addEventListener("click", handleContextActionClick);
  el.buildDiagnostics.addEventListener("click", handleContextActionClick);
  el.referencesReviewBar.addEventListener("click", handleReferencesActionClick);
  el.referencesScopeExplanation.addEventListener("click", handleReferencesActionClick);
  el.referencesTargetList.addEventListener("click", event => {
    const actionButton = event.target.closest("button[data-reference-action]");
    if (actionButton) {
      handleReferencesActionClick(event);
      return;
    }
    const button = event.target.closest("button[data-resource-key]");
    if (button) selectResource(button.dataset.resourceKey);
  });
  el.referencesContent.addEventListener("click", handleReferencesActionClick);
  el.referencesAdviceContent.addEventListener("click", handleReferencesActionClick);
  for (const button of el.graphScopeButtons) {
    button.addEventListener("click", () => {
      state.graphScope = button.dataset.graphScope || "current";
      state.graphSelectedNodeId = "";
      renderReferenceGraphWorkspace();
    });
  }
  el.graphSearchInput.addEventListener("input", event => {
    state.graphSearch = event.target.value;
    state.graphSelectedNodeId = "";
    renderReferenceGraphWorkspace();
  });
  el.graphShowOptional.addEventListener("change", event => {
    state.graphShowOptional = event.target.checked;
    state.graphSelectedNodeId = "";
    renderReferenceGraphWorkspace();
  });
  el.graphColorMode.addEventListener("change", event => {
    state.graphColorMode = event.target.value || "recommendation";
    renderReferenceGraphWorkspace();
  });
  el.graphFitButton.addEventListener("click", () => {
    state.graphSelectedNodeId = "";
    state.lastActionMessage = "关系图已适配当前视图。";
    renderReferenceGraphWorkspace();
  });
  el.copyGraphSummaryButton.addEventListener("click", copyReferenceGraphSummary);
  el.graphCanvas.addEventListener("click", handleReferenceGraphClick);
  el.graphInspectorContent.addEventListener("click", handleReferenceGraphActionClick);
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
      const actionButton = event.target.closest("button[data-action]");
      if (actionButton) handleContextActionClick(event);
    });
    root.addEventListener("change", event => {
      const toggle = event.target.closest("[data-profile-batch-enabled]");
      if (toggle) syncBuildProfileBatchField(toggle.dataset.profileBatchEnabled, toggle.checked);
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
    loadAudioSemantics(),
    loadResourcePlan(false),
    loadCommonResources(),
    loadBuildStatus()
  ]);
  await Promise.all([loadBuildProfile(), loadBundlePlan()]);
  await loadResourceMapping();
  const items = getBrowserItems();
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

  state.packages = [{ relative: DEFAULT_PACKAGE, packageId: "codex_flow_test", kind: "character" }];
}

async function loadResources() {
  try {
    state.resourcesPayload = await fetchJson(API.resources(state.packageRelative));
  } catch (error) {
    state.resourcesPayload = null;
    state.errors.push(apiError("资源库列表", error));
  }
}

async function loadAudioSemantics() {
  try {
    state.audioSemanticPayload = await fetchJson(API.audioSemantics(state.packageRelative));
    state.audioSemanticApiState = "ready";
    state.audioSemanticApiError = "";
    state.audioSemanticDrafts = {};
    state.audioSemanticSaveState = { status: "idle", audioCueId: "", error: "", message: "" };
    state.audioReplacementValidation = { status: "idle", audioCueId: "", payload: null, error: "" };
  } catch (error) {
    state.audioSemanticPayload = null;
    state.audioSemanticApiState = error.status === 404 ? "unavailable" : "error";
    state.audioSemanticApiError = error.message || "Audio semantic manifest API unavailable";
    if (error.status !== 404) {
      state.errors.push(apiError("Audio semantic manifest", error));
    }
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
    const buildTarget = requireEffectiveResourceBuildTarget();
    state.buildProfilePayload = await fetchJson(API.buildProfile(buildTarget));
    const normalizedSavedProfile = normalizeResourceMappingProfileForEditor(
      structuredCloneCompat(pick(state.buildProfilePayload, "profile") || {}));
    state.buildProfilePayload.profile = normalizedSavedProfile;
    state.buildProfileDraft = structuredCloneCompat(normalizedSavedProfile);
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
    state.bundlePlanPayload = await fetchJson(API.bundlePlan(requireEffectiveResourceBuildTarget()));
  } catch (error) {
    state.bundlePlanPayload = null;
    state.errors.push(apiError("Bundle Planner", error));
  }
}

async function loadBuildStatus() {
  try {
    state.buildStatusPayload = await fetchJson(API.buildStatus(state.packageRelative));
  } catch (error) {
    state.buildStatusPayload = null;
    state.errors.push(apiError("Global Build Status", error));
  }
}

async function loadResourceMapping() {
  try {
    const buildTarget = stringValue(pick(state.buildStatusPayload, "effectiveBuildTarget"));
    if (!buildTarget || buildTarget === "ActiveBuildTarget") {
      throw new Error("Unity build status did not provide a concrete effective build target.");
    }
    state.resourceMappingPayload = await fetchJson(API.resourceMapping(buildTarget));
  } catch (error) {
    state.resourceMappingPayload = {
      productionActive: true,
      readOnly: false,
      success: false,
      diagnostics: [{ severity: "Error", code: "ResourceMappingUnavailable", message: error.message || "Production resource mapping unavailable" }]
    };
  }
}

function requireEffectiveResourceBuildTarget() {
  const buildTarget = stringValue(pick(state.buildStatusPayload, "effectiveBuildTarget"));
  if (!buildTarget || buildTarget === "ActiveBuildTarget") {
    throw new Error("Unity build status did not provide a concrete effective build target.");
  }
  return buildTarget;
}

async function loadCommonResources() {
  try {
    state.commonResources = await fetchJson(API.commonResources);
  } catch (error) {
    state.commonResources = {
      success: false,
      unavailable: true,
      items: [],
      summary: { total: 0, ready: 0, changed: 0, blocked: 0, unityPending: 0 },
      message: error.message || "Public reusable resource API is unavailable."
    };
  }
}

function getCommonResourceManifestItemId(item) {
  const explicitId = firstString(pick(item, "itemId"), pick(item, "manifestItemId"));
  if (explicitId) return explicitId;
  const id = String(pick(item, "id") || "");
  const parts = id.split(":");
  return parts.length >= 3 ? parts.slice(2).join(":") : id;
}

function findCommonResourceItemForResource(itemOrKey) {
  const values = typeof itemOrKey === "string"
    ? [itemOrKey]
    : [
      itemOrKey?.resourceKey,
      itemOrKey?.runtimeResourceKey,
      itemOrKey?.providerResourceKey,
      itemOrKey?.packageResourceKey,
      itemOrKey?.stableId,
      itemOrKey?.libraryItemId
    ];
  const keys = new Set(values.map(value => String(value || "")).filter(Boolean));
  if (keys.size === 0) return null;
  return asArray(state.commonResources?.items).find(item => {
    const itemValues = [
      pick(item, "resourceKey"),
      pick(item, "resourceId"),
      pick(item, "id"),
      pick(item, "itemId"),
      pick(item, "manifestItemId")
    ].map(value => String(value || "")).filter(Boolean);
    return itemValues.some(value => keys.has(value));
  }) || null;
}

function isCommonResourceItem(item) {
  return Boolean(findCommonResourceItemForResource(item))
    || String(item?.providerId || "").toLowerCase() === "globalresourcebuildprofile";
}

async function syncCommonResourcesFromManager(options = {}) {
  if (state.commonResourcesSyncing) return;
  state.commonResourcesSyncing = true;
  state.lastActionMessage = "Syncing public reusable resources...";
  render();
  try {
    const payload = await postJson(API.syncCommonResources, {
      buildTarget: requireEffectiveResourceBuildTarget(),
      allChanged: options.allChanged !== false,
      itemIds: options.itemIds || [],
      force: Boolean(options.force)
    });
    state.commonResources = payload.state || state.commonResources;
    state.lastActionMessage = payload.success === false
      ? "Public resource sync completed with blockers."
      : "Public resources synced in Resource Manager.";
    await loadPackageData();
  } catch (error) {
    state.lastActionMessage = `Public resource sync failed: ${error.message}`;
    state.errors.push(apiError("Public resource sync", error));
  } finally {
    state.commonResourcesSyncing = false;
    render();
  }
}

async function saveCommonResourceScaleFromManager(resourceKey, scale) {
  const item = findCommonResourceItemForResource(resourceKey);
  const normalizedScale = Number(scale);
  if (!item || !Number.isFinite(normalizedScale) || normalizedScale <= 0) return;
  const importHints = structuredCloneCompat(pick(item, "importHints") || {});
  const pose = importHints.modelWrapperPose || {};
  importHints.modelWrapperPose = {
    parentKind: pose.parentKind || "ModelRoot",
    parentPath: pose.parentPath || "",
    position: pose.position || { x: 0, y: 0, z: 0 },
    rotation: pose.rotation || { x: 0, y: 0, z: 0, w: 1 },
    scale: { x: normalizedScale, y: normalizedScale, z: normalizedScale }
  };
  state.commonResourceSaveState = { status: "running", error: "" };
  state.lastActionMessage = "Saving public resource import hints...";
  render();
  try {
    const payload = await postJson(API.saveCommonResource, {
      manifestPath: pick(item, "manifestPath") || "",
      itemId: getCommonResourceManifestItemId(item),
      resourceKey: pick(item, "resourceKey") || resourceKey,
      importHints
    });
    state.commonResources = payload.state || state.commonResources;
    state.commonResourceSaveState = { status: "idle", error: "" };
    state.lastActionMessage = "Saved public resource import hints in Resource Manager.";
    await loadPackageData();
  } catch (error) {
    state.commonResourceSaveState = { status: "error", error: error.message };
    state.lastActionMessage = `Public resource import hint save failed: ${error.message}`;
    state.errors.push(apiError("Public resource import hints", error));
    render();
  }
}

async function runResourceValidation() {
  state.lastActionMessage = "正在运行资源验证...";
  renderStatus();
  await loadResourcePlan(true);
  state.lastActionMessage = "资源验证已完成，详情见 resource plan 和 Diagnostics。";
  render();
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
  state.graphSelectedNodeId = "";
  state.inspectState = { id: "", status: "idle", payload: null, error: "" };
  const filtered = sortResourceItems(getFilteredItems(getBrowserItems()));
  const treeRoot = indexResourceTree(buildResourceTree(filtered));
  const nodeId = findTreeNodeIdForItem(treeRoot, resourceKey);
  state.selectedTreeNodeId = nodeId;
  ensureTreeExpansionForNode(nodeId, treeRoot);
  renderSelectionChange();
  await loadInspectForSelection();
}

async function loadInspectForSelection() {
  const item = getSelectedItem();
  if (!item) return;

  const id = item.libraryItemId || item.stableId || item.resourceKey || item.key;
  const cacheKey = `${state.packageRelative}::${id}`;
  if (state.inspectCache.has(cacheKey)) {
    state.inspectState = { id, status: "ready", payload: state.inspectCache.get(cacheKey), error: "" };
    renderInspectDependentViews();
    return;
  }

  state.inspectState = { id, status: "loading", payload: buildFallbackInspect(item), error: "" };
  renderInspectDependentViews();

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
  renderInspectDependentViews();
}

function renderInspectDependentViews() {
  if (state.activeWorkspace === "story") {
    renderStoryWorkspace();
  }
  if (state.activeWorkspace === "art") {
    renderArtWorkspace();
  }
  if (state.activeWorkspace === "browse") {
    renderBrowseContext();
  }
  if (state.activeWorkspace === "audio") {
    renderAudioWorkspace();
  }
  if (state.activeWorkspace === "references") {
    renderReferencesWorkspace();
  }
  if (state.activeWorkspace === "graph") {
    renderReferenceGraphWorkspace();
  }
  if (state.activeWorkspace === "debug") {
    renderInspector();
    renderDebugPicker();
    renderRawJson();
  }
  renderContextActions();
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
  if (el.storyWorkspace) el.storyWorkspace.innerHTML = emptyBlock("正在读取 StorySource 状态…");
  if (el.artWorkspace) el.artWorkspace.innerHTML = emptyBlock("正在读取 ArtSource 状态…");
  el.browseWorkflowAssistant.innerHTML = "";
  el.profileWorkflowAssistant.innerHTML = "";
  el.buildWorkflowAssistant.innerHTML = "";
  el.statusStrip.innerHTML = statusChip("服务", "读取中", "pending");
  el.resourceSummary.textContent = "正在读取资源管理器...";
  el.checkedSummary.textContent = "已勾选 0 个资源";
  el.resourceTree.innerHTML = emptyBlock("正在读取目录…");
  el.resourceList.innerHTML = emptyBlock("正在读取资源项");
  el.browseWorkflowAssistant.innerHTML = "";
  el.browsePanelTitle.textContent = "当前选择";
  el.browsePanelSubtitle.textContent = "等待资源库数据";
  el.browseContextBody.innerHTML = "";
  if (el.audioSummary) el.audioSummary.textContent = "正在读取音频资源...";
  if (el.audioProviderSummary) el.audioProviderSummary.innerHTML = "";
  if (el.audioBankList) el.audioBankList.innerHTML = emptyBlock("正在读取 FMOD event 和 bank…");
  if (el.audioCueList) el.audioCueList.innerHTML = emptyBlock("正在读取 AudioCue…");
  if (el.audioImpactPanel) el.audioImpactPanel.innerHTML = "";
  el.planSummary.textContent = "正在读取 resource plan...";
  el.planGrid.innerHTML = "";
  el.profileWorkflowAssistant.innerHTML = "";
  el.buildProfileSummary.textContent = "正在读取 Global Resource Build Profile...";
  el.buildProfileContent.innerHTML = "";
  el.referencesReviewBar.innerHTML = "";
  el.referencesSummary.textContent = "正在读取引用关系...";
  el.referencesScopeHint.textContent = "";
  if (el.referencesScopeExplanation) el.referencesScopeExplanation.innerHTML = "";
  if (el.referencesScopeSearch) el.referencesScopeSearch.value = state.referencesSearch || "";
  el.referencesTargetList.innerHTML = "";
  el.referencesGroupSummary.textContent = "";
  el.referencesContent.innerHTML = "";
  el.referencesAdviceSummary.textContent = "";
  el.referencesAdviceContent.innerHTML = "";
  el.graphSummary.textContent = "正在读取关系图...";
  if (el.graphSearchInput) el.graphSearchInput.value = state.graphSearch || "";
  if (el.graphShowOptional) el.graphShowOptional.checked = state.graphShowOptional;
  if (el.graphColorMode) el.graphColorMode.value = state.graphColorMode;
  el.graphLegend.innerHTML = "";
  el.graphViewportSummary.textContent = "";
  el.graphNotice.innerHTML = "";
  el.graphCanvas.innerHTML = "";
  el.graphInspectorSummary.textContent = "等待关系图数据";
  el.graphInspectorContent.innerHTML = "";
  el.buildWorkflowAssistant.innerHTML = "";
  el.inspectorStatus.textContent = "正在读取详情...";
  el.inspectorContent.innerHTML = emptyBlock("等待资源详情");
}

function render() {
  renderPackageSelect();
  renderStatus();
  renderWorkspaceNav();
  renderImportPresetOptions();
  if (state.activeWorkspace === "story") {
    renderStoryWorkspace();
  }
  if (state.activeWorkspace === "art") {
    renderArtWorkspace();
  }
  if (state.activeWorkspace === "browse") {
    renderFilters();
    renderBrowser();
    renderBrowseContext();
  }
  if (state.activeWorkspace === "audio") {
    renderAudioWorkspace();
  }
  if (state.activeWorkspace === "profile") {
    renderBuildProfile();
  }
  if (state.activeWorkspace === "references") {
    renderReferencesWorkspace();
  }
  if (state.activeWorkspace === "graph") {
    renderReferenceGraphWorkspace();
  }
  if (state.activeWorkspace === "build") {
    renderPlan();
    renderBuildWorkspace();
  }
  if (state.activeWorkspace === "debug") {
    renderInspector();
    renderDebugPicker();
    renderRawJson();
  }
  renderContextActions();
}

function renderSelectionChange() {
  if (state.activeWorkspace === "story") {
    renderStoryWorkspace();
  }
  if (state.activeWorkspace === "art") {
    renderArtWorkspace();
  }
  if (state.activeWorkspace === "browse") {
    renderBrowser();
    renderBrowseContext();
  }
  if (state.activeWorkspace === "audio") {
    renderAudioWorkspace();
  }
  if (state.activeWorkspace === "profile") {
    renderBuildProfile();
  }
  if (state.activeWorkspace === "references") {
    renderReferencesWorkspace();
  }
  if (state.activeWorkspace === "graph") {
    renderReferenceGraphWorkspace();
  }
  if (state.activeWorkspace === "debug") {
    renderInspector();
    renderDebugPicker();
    renderRawJson();
  }
  renderContextActions();
}


function renderStoryWorkspace() {
  if (!el.storyWorkspace) return;
  const storyStatus = window.MxResourceStorySourceStatus;
  if (!storyStatus || typeof storyStatus.createModel !== "function" || typeof storyStatus.render !== "function") {
    el.storyWorkspace.innerHTML = emptyBlock("StorySource 状态模块未加载。");
    return;
  }
  const model = createStoryWorkspaceModel();
  state.storySelectedKey = model.selectedKey || "";
  state.storySelectedKeys = model.selectedKeys || state.storySelectedKeys || { canon: "", outline: "", packet: "" };
  el.storyWorkspace.innerHTML = storyStatus.render(model);
}

function createStoryWorkspaceModel() {
  const storyStatus = window.MxResourceStorySourceStatus;
  if (!storyStatus || typeof storyStatus.createModel !== "function") return null;
  return storyStatus.createModel(
    getNormalizedItems(),
    state.resourcesPayload,
    state.storySelectedKeys,
    state.storyPacketDrafts,
    state.storyPacketSaveState,
    state.storyCanonDrafts,
    state.storyCanonSaveState,
    state.storyOutlineDrafts,
    state.storyOutlineSaveState
  );
}

function handleStoryWorkspaceEvent(event) {
  const field = event.target.closest?.("[data-story-field][data-story-packet-id]");
  if (field && (event.type === "input" || event.type === "change")) {
    updateStoryPacketDraft(field.dataset.storyPacketId, field.dataset.storyField, field.value);
    return;
  }

  const arrayField = event.target.closest?.("[data-story-array-field][data-story-packet-id]");
  if (arrayField && (event.type === "input" || event.type === "change")) {
    updateStoryPacketArrayDraft(arrayField.dataset.storyPacketId, arrayField.dataset.storyArrayField, arrayField.value);
    return;
  }

  const canonField = event.target.closest?.("[data-story-canon-field][data-story-canon-id]");
  if (canonField && (event.type === "input" || event.type === "change")) {
    updateStoryCanonDraft(canonField.dataset.storyCanonId, canonField.dataset.storyCanonField, canonField.value);
    return;
  }

  const canonArrayField = event.target.closest?.("[data-story-canon-array-field][data-story-canon-id]");
  if (canonArrayField && (event.type === "input" || event.type === "change")) {
    updateStoryCanonArrayDraft(canonArrayField.dataset.storyCanonId, canonArrayField.dataset.storyCanonArrayField, canonArrayField.value);
    return;
  }

  const canonRelationsField = event.target.closest?.("[data-story-canon-relations-field][data-story-canon-id]");
  if (canonRelationsField && (event.type === "input" || event.type === "change")) {
    updateStoryCanonRelationsDraft(canonRelationsField.dataset.storyCanonId, canonRelationsField.value);
    return;
  }

  const outlineField = event.target.closest?.("[data-story-outline-field][data-story-outline-id]");
  if (outlineField && (event.type === "input" || event.type === "change")) {
    updateStoryOutlineDraft(outlineField.dataset.storyOutlineId, outlineField.dataset.storyOutlineField, outlineField.value);
    return;
  }

  const outlineArrayField = event.target.closest?.("[data-story-outline-array-field][data-story-outline-id]");
  if (outlineArrayField && (event.type === "input" || event.type === "change")) {
    updateStoryOutlineArrayDraft(outlineArrayField.dataset.storyOutlineId, outlineArrayField.dataset.storyOutlineArrayField, outlineArrayField.value);
    return;
  }

  const outlineBeatsField = event.target.closest?.("[data-story-outline-beats-field][data-story-outline-id]");
  if (outlineBeatsField && (event.type === "input" || event.type === "change")) {
    updateStoryOutlineBeatsDraft(outlineBeatsField.dataset.storyOutlineId, outlineBeatsField.value);
    return;
  }

  const outlineBeatField = event.target.closest?.("[data-story-outline-beat-field][data-story-outline-id][data-story-outline-beat-index]");
  if (outlineBeatField && (event.type === "input" || event.type === "change")) {
    updateStoryOutlineBeatDraft(
      outlineBeatField.dataset.storyOutlineId,
      outlineBeatField.dataset.storyOutlineBeatIndex,
      outlineBeatField.dataset.storyOutlineBeatField,
      outlineBeatField.value);
    return;
  }

  if (event.type !== "click") return;
  const canonButton = event.target.closest("button[data-story-canon-key]");
  if (canonButton && !canonButton.disabled) {
    state.storySelectedKeys = Object.assign({}, state.storySelectedKeys || {}, { canon: canonButton.dataset.storyCanonKey || "" });
    renderStoryWorkspace();
    return;
  }

  const outlineButton = event.target.closest("button[data-story-outline-key]");
  if (outlineButton && !outlineButton.disabled) {
    state.storySelectedKeys = Object.assign({}, state.storySelectedKeys || {}, { outline: outlineButton.dataset.storyOutlineKey || "" });
    renderStoryWorkspace();
    return;
  }

  const packetButton = event.target.closest("button[data-story-packet-key]");
  if (packetButton && !packetButton.disabled) {
    state.storySelectedKeys = Object.assign({}, state.storySelectedKeys || {}, { packet: packetButton.dataset.storyPacketKey || "" });
    renderStoryWorkspace();
    return;
  }

  const canonActionButton = event.target.closest("[data-story-canon-action][data-story-canon-id]");
  if (canonActionButton && !canonActionButton.disabled) {
    const canonId = canonActionButton.dataset.storyCanonId || "";
    if (canonActionButton.dataset.storyCanonAction === "reset") {
      clearStoryCanonDraft(canonId);
      return;
    }
    if (canonActionButton.dataset.storyCanonAction === "save") {
      saveStoryCanonDraft(canonId);
      return;
    }
  }

  const outlineActionButton = event.target.closest("[data-story-outline-action][data-story-outline-id]");
  if (outlineActionButton && !outlineActionButton.disabled) {
    const outlineId = outlineActionButton.dataset.storyOutlineId || "";
    if (outlineActionButton.dataset.storyOutlineAction === "reset") {
      clearStoryOutlineDraft(outlineId);
      return;
    }
    if (outlineActionButton.dataset.storyOutlineAction === "save") {
      saveStoryOutlineDraft(outlineId);
      return;
    }
    if (outlineActionButton.dataset.storyOutlineAction === "approve") {
      approveStoryOutline(outlineId);
      return;
    }
  }

  const outlineBeatActionButton = event.target.closest("[data-story-outline-beat-action][data-story-outline-id]");
  if (outlineBeatActionButton && !outlineBeatActionButton.disabled) {
    const outlineId = outlineBeatActionButton.dataset.storyOutlineId || "";
    if (outlineBeatActionButton.dataset.storyOutlineBeatAction === "add") {
      addStoryOutlineBeatDraft(outlineId);
      return;
    }
    if (outlineBeatActionButton.dataset.storyOutlineBeatAction === "remove") {
      removeStoryOutlineBeatDraft(outlineId, outlineBeatActionButton.dataset.storyOutlineBeatIndex);
      return;
    }
  }

  const actionButton = event.target.closest("[data-story-action][data-story-packet-id]");
  if (!actionButton || actionButton.disabled) return;
  const packetId = actionButton.dataset.storyPacketId || "";
  if (!packetId) return;

  if (actionButton.dataset.storyAction === "reset") {
    clearStoryPacketDraft(packetId);
    return;
  }
  if (actionButton.dataset.storyAction === "save") {
    saveStoryPacketDraft(packetId);
    return;
  }
  if (actionButton.dataset.storyAction === "approve") {
    approveStoryPacket(packetId);
  }
}

function updateStoryPacketDraft(packetId, field, value) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.updateDraftField !== "function") return;
  state.storyPacketDrafts = storyStatus.updateDraftField(model, state.storyPacketDrafts, packetId, field, value);
  state.storyPacketSaveState = { status: "idle", storySourceId: packetId || "", error: "", message: "" };
  renderStoryWorkspace();
}

function updateStoryPacketArrayDraft(packetId, field, value) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.updateDraftArrayField !== "function") return;
  state.storyPacketDrafts = storyStatus.updateDraftArrayField(model, state.storyPacketDrafts, packetId, field, value);
  state.storyPacketSaveState = { status: "idle", storySourceId: packetId || "", error: "", message: "" };
  renderStoryWorkspace();
}

function clearStoryPacketDraft(packetId) {
  const storyStatus = window.MxResourceStorySourceStatus;
  if (!storyStatus || typeof storyStatus.clearDraft !== "function") return;
  state.storyPacketDrafts = storyStatus.clearDraft(state.storyPacketDrafts, packetId);
  state.storyPacketSaveState = { status: "idle", storySourceId: packetId || "", error: "", message: "" };
  renderStoryWorkspace();
}

function updateStoryCanonDraft(canonId, field, value) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.updateCanonDraftField !== "function") return;
  state.storyCanonDrafts = storyStatus.updateCanonDraftField(model, state.storyCanonDrafts, canonId, field, value);
  state.storyCanonSaveState = { status: "idle", canonId: canonId || "", error: "", message: "" };
  renderStoryWorkspace();
}

function updateStoryCanonArrayDraft(canonId, field, value) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.updateCanonDraftArrayField !== "function") return;
  state.storyCanonDrafts = storyStatus.updateCanonDraftArrayField(model, state.storyCanonDrafts, canonId, field, value);
  state.storyCanonSaveState = { status: "idle", canonId: canonId || "", error: "", message: "" };
  renderStoryWorkspace();
}

function updateStoryCanonRelationsDraft(canonId, value) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.updateCanonDraftRelations !== "function") return;
  state.storyCanonDrafts = storyStatus.updateCanonDraftRelations(model, state.storyCanonDrafts, canonId, value);
  state.storyCanonSaveState = { status: "idle", canonId: canonId || "", error: "", message: "" };
  renderStoryWorkspace();
}

function updateStoryOutlineDraft(outlineId, field, value) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.updateOutlineDraftField !== "function") return;
  state.storyOutlineDrafts = storyStatus.updateOutlineDraftField(model, state.storyOutlineDrafts, outlineId, field, value);
  state.storyOutlineSaveState = { status: "idle", outlineId: outlineId || "", error: "", message: "" };
  renderStoryWorkspace();
}

function updateStoryOutlineArrayDraft(outlineId, field, value) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.updateOutlineDraftArrayField !== "function") return;
  state.storyOutlineDrafts = storyStatus.updateOutlineDraftArrayField(model, state.storyOutlineDrafts, outlineId, field, value);
  state.storyOutlineSaveState = { status: "idle", outlineId: outlineId || "", error: "", message: "" };
  renderStoryWorkspace();
}

function updateStoryOutlineBeatsDraft(outlineId, value) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.updateOutlineDraftBeats !== "function") return;
  state.storyOutlineDrafts = storyStatus.updateOutlineDraftBeats(model, state.storyOutlineDrafts, outlineId, value);
  state.storyOutlineSaveState = { status: "idle", outlineId: outlineId || "", error: "", message: "" };
  renderStoryWorkspace();
}

function updateStoryOutlineBeatDraft(outlineId, index, field, value) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.updateOutlineDraftBeatField !== "function") return;
  state.storyOutlineDrafts = storyStatus.updateOutlineDraftBeatField(model, state.storyOutlineDrafts, outlineId, index, field, value);
  state.storyOutlineSaveState = { status: "idle", outlineId: outlineId || "", error: "", message: "" };
  renderStoryWorkspace();
}

function addStoryOutlineBeatDraft(outlineId) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.addOutlineDraftBeat !== "function") return;
  state.storyOutlineDrafts = storyStatus.addOutlineDraftBeat(model, state.storyOutlineDrafts, outlineId);
  state.storyOutlineSaveState = { status: "idle", outlineId: outlineId || "", error: "", message: "" };
  renderStoryWorkspace();
}

function removeStoryOutlineBeatDraft(outlineId, index) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.removeOutlineDraftBeat !== "function") return;
  state.storyOutlineDrafts = storyStatus.removeOutlineDraftBeat(model, state.storyOutlineDrafts, outlineId, index);
  state.storyOutlineSaveState = { status: "idle", outlineId: outlineId || "", error: "", message: "" };
  renderStoryWorkspace();
}

function clearStoryCanonDraft(canonId) {
  const storyStatus = window.MxResourceStorySourceStatus;
  if (!storyStatus || typeof storyStatus.clearDraft !== "function") return;
  state.storyCanonDrafts = storyStatus.clearDraft(state.storyCanonDrafts, canonId);
  state.storyCanonSaveState = { status: "idle", canonId: canonId || "", error: "", message: "" };
  renderStoryWorkspace();
}

function clearStoryOutlineDraft(outlineId) {
  const storyStatus = window.MxResourceStorySourceStatus;
  if (!storyStatus || typeof storyStatus.clearDraft !== "function") return;
  state.storyOutlineDrafts = storyStatus.clearDraft(state.storyOutlineDrafts, outlineId);
  state.storyOutlineSaveState = { status: "idle", outlineId: outlineId || "", error: "", message: "" };
  renderStoryWorkspace();
}

async function saveStoryPacketDraft(packetId) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.buildUpdateRequest !== "function") return;
  const request = storyStatus.buildUpdateRequest(model, state.storyPacketDrafts, packetId);
  if (!request) return;

  state.writeState = { status: "running", action: "save-story-packet", error: "" };
  state.storyPacketSaveState = { status: "running", storySourceId: packetId || "", error: "", message: "正在保存故事输入包..." };
  state.lastActionMessage = "正在保存故事输入包...";
  renderStoryWorkspace();

  try {
    const payload = await postJson(API.updateStoryPacket, request);
    state.storyPacketDrafts = storyStatus.clearDraft(state.storyPacketDrafts, packetId);
    state.storyPacketSaveState = {
      status: "success",
      storySourceId: packetId || "",
      error: "",
      message: payload.message || "故事输入包已保存。"
    };
    state.writeState = { status: "idle", action: "", error: "" };
    state.lastActionMessage = "故事输入包已保存。";
    state.inspectCache.clear();
    await loadResources();
  } catch (error) {
    const conflict = error.status === 409 || error.data?.code === "STORY_SOURCE_PACKET_UPDATE_CONFLICT";
    const serverError = getStorySourceServerError(error);
    state.storyPacketSaveState = {
      status: conflict ? "conflict" : "error",
      storySourceId: packetId || "",
      error: serverError,
      message: conflict ? "StorySource 已变化，请刷新后再保存。" : "故事输入包保存失败。"
    };
    state.writeState = { status: "error", action: "save-story-packet", error: serverError };
    state.lastActionMessage = `故事输入包保存失败：${serverError}`;
    state.errors.push(apiError("保存故事输入包", error));
  }

  renderStoryWorkspace();
}

async function saveStoryCanonDraft(canonId) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.buildCanonUpdateRequest !== "function") return;
  const request = storyStatus.buildCanonUpdateRequest(model, state.storyCanonDrafts, canonId);
  if (!request) return;

  state.writeState = { status: "running", action: "save-story-canon", error: "" };
  state.storyCanonSaveState = { status: "running", canonId: canonId || "", error: "", message: "正在保存世界观记忆..." };
  state.lastActionMessage = "正在保存世界观记忆...";
  renderStoryWorkspace();

  try {
    const payload = await postJson(API.updateStoryCanon, request);
    state.storyCanonDrafts = storyStatus.clearDraft(state.storyCanonDrafts, canonId);
    state.storyCanonSaveState = { status: "success", canonId: canonId || "", error: "", message: payload.message || "世界观记忆已保存。" };
    state.writeState = { status: "idle", action: "", error: "" };
    state.lastActionMessage = "世界观记忆已保存。";
    state.inspectCache.clear();
    await loadResources();
  } catch (error) {
    const conflict = error.status === 409 || error.data?.code === "STORY_SOURCE_PACKET_UPDATE_CONFLICT";
    const serverError = getStorySourceServerError(error);
    state.storyCanonSaveState = { status: conflict ? "conflict" : "error", canonId: canonId || "", error: serverError, message: conflict ? "StorySource 已变化，请刷新后再保存。" : "世界观记忆保存失败。" };
    state.writeState = { status: "error", action: "save-story-canon", error: serverError };
    state.lastActionMessage = `世界观记忆保存失败：${serverError}`;
    state.errors.push(apiError("保存世界观记忆", error));
  }

  renderStoryWorkspace();
}

async function saveStoryOutlineDraft(outlineId) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.buildOutlineUpdateRequest !== "function") return;
  const request = storyStatus.buildOutlineUpdateRequest(model, state.storyOutlineDrafts, outlineId);
  if (!request) return;

  state.writeState = { status: "running", action: "save-story-outline", error: "" };
  state.storyOutlineSaveState = { status: "running", outlineId: outlineId || "", error: "", message: "正在保存故事大纲..." };
  state.lastActionMessage = "正在保存故事大纲...";
  renderStoryWorkspace();

  try {
    const payload = await postJson(API.updateStoryOutline, request);
    state.storyOutlineDrafts = storyStatus.clearDraft(state.storyOutlineDrafts, outlineId);
    state.storyOutlineSaveState = { status: "success", outlineId: outlineId || "", error: "", message: payload.message || "故事大纲已保存。" };
    state.writeState = { status: "idle", action: "", error: "" };
    state.lastActionMessage = "故事大纲已保存。";
    state.inspectCache.clear();
    await loadResources();
  } catch (error) {
    const conflict = error.status === 409 || error.data?.code === "STORY_SOURCE_PACKET_UPDATE_CONFLICT";
    const serverError = getStorySourceServerError(error);
    state.storyOutlineSaveState = { status: conflict ? "conflict" : "error", outlineId: outlineId || "", error: serverError, message: conflict ? "StorySource 已变化，请刷新后再保存。" : "故事大纲保存失败。" };
    state.writeState = { status: "error", action: "save-story-outline", error: serverError };
    state.lastActionMessage = `故事大纲保存失败：${serverError}`;
    state.errors.push(apiError("保存故事大纲", error));
  }

  renderStoryWorkspace();
}

async function approveStoryOutline(outlineId) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.buildOutlineApproveRequest !== "function") return;
  const request = storyStatus.buildOutlineApproveRequest(model, outlineId);
  if (!request) return;

  state.writeState = { status: "running", action: "approve-story-outline", error: "" };
  state.storyOutlineSaveState = { status: "running", outlineId: outlineId || "", error: "", message: "正在批准故事大纲..." };
  state.lastActionMessage = "正在批准故事大纲...";
  renderStoryWorkspace();

  try {
    const payload = await postJson(API.approveStoryOutline, request);
    state.storyOutlineDrafts = storyStatus.clearDraft(state.storyOutlineDrafts, outlineId);
    state.storyOutlineSaveState = { status: "success", outlineId: outlineId || "", error: "", message: payload.message || "故事大纲已批准给输入包。" };
    state.writeState = { status: "idle", action: "", error: "" };
    state.lastActionMessage = "故事大纲已批准给输入包。";
    state.inspectCache.clear();
    await loadResources();
  } catch (error) {
    const conflict = error.status === 409 || error.data?.code === "STORY_SOURCE_PACKET_APPROVE_CONFLICT";
    const serverError = getStorySourceServerError(error);
    state.storyOutlineSaveState = { status: conflict ? "conflict" : "error", outlineId: outlineId || "", error: serverError, message: conflict ? "StorySource 已变化，请刷新后再批准。" : "故事大纲批准失败。" };
    state.writeState = { status: "error", action: "approve-story-outline", error: serverError };
    state.lastActionMessage = `故事大纲批准失败：${serverError}`;
    state.errors.push(apiError("批准故事大纲", error));
  }

  renderStoryWorkspace();
}

async function approveStoryPacket(packetId) {
  const storyStatus = window.MxResourceStorySourceStatus;
  const model = createStoryWorkspaceModel();
  if (!storyStatus || !model || typeof storyStatus.buildApproveRequest !== "function") return;
  const request = storyStatus.buildApproveRequest(model, packetId);
  if (!request) return;

  state.writeState = { status: "running", action: "approve-story-packet", error: "" };
  state.storyPacketSaveState = { status: "running", storySourceId: packetId || "", error: "", message: "正在批准给美术..." };
  state.lastActionMessage = "正在批准故事输入包...";
  renderStoryWorkspace();

  try {
    const payload = await postJson(API.approveStoryPacket, request);
    state.storyPacketDrafts = storyStatus.clearDraft(state.storyPacketDrafts, packetId);
    state.storyPacketSaveState = {
      status: "success",
      storySourceId: packetId || "",
      error: "",
      message: payload.message || "故事输入包已批准给美术。"
    };
    state.writeState = { status: "idle", action: "", error: "" };
    state.lastActionMessage = "故事输入包已批准给美术。";
    state.inspectCache.clear();
    await loadResources();
  } catch (error) {
    const conflict = error.status === 409 || error.data?.code === "STORY_SOURCE_PACKET_APPROVE_CONFLICT";
    const serverError = getStorySourceServerError(error);
    state.storyPacketSaveState = {
      status: conflict ? "conflict" : "error",
      storySourceId: packetId || "",
      error: serverError,
      message: conflict ? "StorySource 已变化，请刷新后再批准。" : "批准失败。"
    };
    state.writeState = { status: "error", action: "approve-story-packet", error: serverError };
    state.lastActionMessage = `故事输入包批准失败：${serverError}`;
    state.errors.push(apiError("批准故事输入包", error));
  }

  renderStoryWorkspace();
}

function renderArtWorkspace() {
  if (!el.artWorkspace) return;
  const artStatus = window.MxResourceArtSourceStatus;
  if (!artStatus || typeof artStatus.createModel !== "function" || typeof artStatus.render !== "function") {
    el.artWorkspace.innerHTML = emptyBlock("ArtSource 状态模块未加载。");
    return;
  }
  const model = createArtWorkspaceModel();
  state.artSelectedKey = model.selectedKey || "";
  el.artWorkspace.innerHTML = artStatus.render(model, state.artImagePreview);
}

function createArtWorkspaceModel() {
  const artStatus = window.MxResourceArtSourceStatus;
  if (!artStatus || typeof artStatus.createModel !== "function") return null;
  return artStatus.createModel(
    getNormalizedItems(),
    state.artFilters,
    state.artSelectedKey,
    state.resourcesPayload,
    state.artTaskDrafts,
    state.artTaskSaveState,
    state.artTaskCreateDraft,
    state.artTaskCreateState,
    state.artUiPanels,
    state.artGraphZoom
  );
}

function handleArtWorkspaceEvent(event) {
  const toggledPanel = event.target.closest?.("[data-art-panel]");
  if (toggledPanel && event.type === "toggle") {
    const panelKey = toggledPanel.dataset.artPanel || "";
    if (panelKey) state.artUiPanels[panelKey] = toggledPanel.open;
    return;
  }

  const filter = event.target.closest("[data-art-filter]");
  if (filter && (event.type === "input" || event.type === "change")) {
    state.artFilters[filter.dataset.artFilter] = filter.value;
    renderArtWorkspace();
    return;
  }

  const taskEditField = event.target.closest("[data-art-task-edit-field][data-art-task-id]");
  if (taskEditField && (event.type === "input" || event.type === "change")) {
    updateArtTaskDraft(taskEditField.dataset.artTaskId, taskEditField.dataset.artTaskEditField, taskEditField.value);
    return;
  }

  const taskAssetToggle = event.target.closest("[data-art-task-asset-toggle][data-art-task-id]");
  if (taskAssetToggle && event.type === "change") {
    updateArtTaskAssetDraft(taskAssetToggle.dataset.artTaskId, taskAssetToggle.dataset.artTaskAssetToggle, taskAssetToggle.checked);
    return;
  }

  const createField = event.target.closest("[data-art-create-field]");
  if (createField && (event.type === "input" || event.type === "change")) {
    updateArtCreateDraft(createField.dataset.artCreateField, createField.value);
    return;
  }

  const createAssetToggle = event.target.closest("[data-art-create-asset-toggle]");
  if (createAssetToggle && event.type === "change") {
    updateArtCreateAssetDraft(createAssetToggle.dataset.artCreateAssetToggle, createAssetToggle.checked);
    return;
  }

  if (event.type !== "click") return;
  const previewTarget = event.target.closest("[data-art-preview-url]");
  if (previewTarget) {
    openArtImagePreview(previewTarget);
    return;
  }

  const workstreamButton = event.target.closest("button[data-art-workstream-key]");
  if (workstreamButton && !workstreamButton.disabled) {
    selectArtWorkstreamTask(workstreamButton.dataset.artWorkstreamKey || "");
    return;
  }

  const openButton = event.target.closest("[data-art-open-url]");
  if (openButton && !openButton.disabled) {
    openStaticArtSourceUrl(openButton.dataset.artOpenUrl);
    return;
  }

  const actionButton = event.target.closest("[data-art-action]");
  if (actionButton && !actionButton.disabled) {
    handleArtWorkspaceAction(actionButton);
    return;
  }

  const taskButton = event.target.closest("button[data-art-task-key]");
  if (taskButton && !taskButton.disabled) {
    state.artSelectedKey = taskButton.dataset.artTaskKey || "";
    renderArtWorkspace();
    return;
  }

  const itemButton = event.target.closest("button[data-art-resource-key]");
  if (itemButton && !itemButton.disabled) {
    state.artSelectedKey = itemButton.dataset.artResourceKey || "";
    state.selectedResourceKey = state.artSelectedKey;
    state.inspectState = { id: "", status: "idle", payload: null, error: "" };
    renderArtWorkspace();
    loadInspectForSelection();
  }
}

function handleArtGraphPointerDown(event) {
  if (event.button !== 0) return;
  if (state.artGraphDrag) return;
  const canvas = event.target.closest?.("[data-art-graph-canvas]");
  if (!canStartArtGraphDrag(event, canvas)) return;
  startArtGraphDrag(canvas, event.clientX, event.clientY, "pointer", event.pointerId);
  canvas.setPointerCapture?.(event.pointerId);
  event.preventDefault();
}

function handleArtGraphMouseDown(event) {
  if (event.button !== 0) return;
  if (state.artGraphDrag) return;
  const canvas = event.target.closest?.("[data-art-graph-canvas]");
  if (!canStartArtGraphDrag(event, canvas)) return;
  startArtGraphDrag(canvas, event.clientX, event.clientY, "mouse", null);
  event.preventDefault();
}

function canStartArtGraphDrag(event, canvas) {
  if (!canvas || !el.artWorkspace?.contains(canvas)) return false;
  if (event.target.closest("button, a, input, select, textarea, summary, label, [data-art-action], [data-art-workstream-key]")) return false;
  return true;
}

function startArtGraphDrag(canvas, clientX, clientY, inputType, pointerId) {
  state.artGraphDrag = {
    pointerId,
    inputType,
    canvas,
    startX: clientX,
    startY: clientY,
    scrollLeft: canvas.scrollLeft,
    scrollTop: canvas.scrollTop,
    moved: false
  };
  canvas.classList.add("dragging");
}

function handleArtGraphPointerMove(event) {
  const drag = state.artGraphDrag;
  if (!drag || drag.inputType !== "pointer" || drag.pointerId !== event.pointerId) return;
  updateArtGraphDrag(event.clientX, event.clientY);
  event.preventDefault();
}

function handleArtGraphMouseMove(event) {
  const drag = state.artGraphDrag;
  if (!drag || drag.inputType !== "mouse") return;
  updateArtGraphDrag(event.clientX, event.clientY);
  event.preventDefault();
}

function updateArtGraphDrag(clientX, clientY) {
  const drag = state.artGraphDrag;
  if (!drag) return;
  const dx = clientX - drag.startX;
  const dy = clientY - drag.startY;
  if (Math.abs(dx) > 2 || Math.abs(dy) > 2) drag.moved = true;
  drag.canvas.scrollLeft = drag.scrollLeft - dx;
  drag.canvas.scrollTop = drag.scrollTop - dy;
}

function endArtGraphDrag(event) {
  const drag = state.artGraphDrag;
  if (!drag || drag.inputType !== "pointer" || (event.pointerId != null && drag.pointerId !== event.pointerId)) return;
  drag.canvas.classList.remove("dragging");
  drag.canvas.releasePointerCapture?.(drag.pointerId);
  state.artGraphDrag = null;
}

function endArtGraphMouseDrag() {
  const drag = state.artGraphDrag;
  if (!drag || drag.inputType !== "mouse") return;
  drag.canvas.classList.remove("dragging");
  state.artGraphDrag = null;
}

function selectArtWorkstreamTask(workstreamId) {
  const id = String(workstreamId || "").trim();
  if (!id) return;
  const model = createArtWorkspaceModel();
  const filteredTask = model?.filteredTasks?.find(task => task.workstreamId === id);
  const fallbackTask = model?.allTasks?.find(task => task.workstreamId === id);
  const task = filteredTask || fallbackTask;
  if (!task) return;
  state.artSelectedKey = task.key || (task.taskId ? `task:${task.taskId}` : "");
  renderArtWorkspace();
}

function openArtImagePreview(target) {
  const url = String(target?.dataset?.artPreviewUrl || "").trim();
  if (!url) return;
  state.artImagePreview = {
    url,
    title: String(target.dataset.artPreviewTitle || "ArtSource 预览图").trim(),
    note: String(target.dataset.artPreviewNote || "").trim()
  };
  renderArtWorkspace();
}

function closeArtImagePreview() {
  if (!state.artImagePreview) return;
  state.artImagePreview = null;
  renderArtWorkspace();
}

function updateArtGraphZoom(delta) {
  const value = Number(state.artGraphZoom) || 1;
  setArtGraphZoom(value + delta);
}

function setArtGraphZoom(value, resetPan = false) {
  state.artGraphZoom = Math.min(1.8, Math.max(0.7, Math.round(Number(value || 1) * 10) / 10));
  state.artUiPanels.graph = true;
  if (!syncArtGraphZoomDom(resetPan)) renderArtWorkspace();
}

function syncArtGraphZoomDom(resetPan = false) {
  const graph = el.artWorkspace?.querySelector(".art-dependency-graph");
  if (!graph) return false;
  graph.open = true;
  const zoom = Number(state.artGraphZoom) || 1;
  const canvas = graph.querySelector(".art-graph-canvas");
  const content = graph.querySelector(".art-graph-content");
  const scale = graph.querySelector(".art-graph-scale");
  const percent = graph.querySelector(".art-graph-toolbar strong");
  const zoomOut = graph.querySelector("[data-art-action='art-graph-zoom-out']");
  const zoomIn = graph.querySelector("[data-art-action='art-graph-zoom-in']");
  const heightText = canvas?.style.getPropertyValue("--art-graph-height") || "240px";
  const height = Number.parseFloat(heightText) || 240;
  graph.style.setProperty("--art-graph-open-height", `${Math.round(height * zoom) + 98}px`);
  if (content) {
    content.style.width = `${Math.round(1000 * zoom)}px`;
    content.style.height = `${Math.round(height * zoom)}px`;
  }
  if (scale) {
    scale.style.setProperty("--art-graph-zoom", String(zoom));
    scale.style.setProperty("--art-graph-height", `${height}px`);
  }
  if (percent) percent.textContent = `${Math.round(zoom * 100)}%`;
  if (zoomOut) zoomOut.disabled = zoom <= 0.7;
  if (zoomIn) zoomIn.disabled = zoom >= 1.8;
  if (canvas && resetPan) {
    canvas.scrollLeft = 0;
    canvas.scrollTop = 0;
  }
  return true;
}

function openStaticArtSourceUrl(url) {
  const value = String(url || "").trim();
  if (!value) return;
  window.open(value, "_blank", "noopener");
}

function handleArtWorkspaceAction(button) {
  const action = button.dataset.artAction;
  if (action === "clear-filters") {
    state.artFilters = window.MxResourceArtSourceStatus?.createDefaultFilters?.() || {
      search: "",
      workstream: "all",
      taskStatus: "all",
      priority: "all",
      blocked: "all",
      family: "all",
      reviewStatus: "all"
    };
    renderArtWorkspace();
    return;
  }
  if (action === "art-graph-toggle") {
    state.artUiPanels.graph = !state.artUiPanels.graph;
    renderArtWorkspace();
    return;
  }
  if (action === "art-image-preview-close") {
    closeArtImagePreview();
    return;
  }
  if (action === "art-graph-zoom-in") {
    updateArtGraphZoom(0.1);
    return;
  }
  if (action === "art-graph-zoom-out") {
    updateArtGraphZoom(-0.1);
    return;
  }
  if (action === "art-graph-zoom-reset") {
    setArtGraphZoom(1, true);
    return;
  }
  if (action === "art-task-set-status") {
    updateArtTaskDraft(button.dataset.artTaskId, "status", button.dataset.artTaskValue || "");
    return;
  }
  if (action === "art-task-quick-unblock" || action === "art-task-quick-review" || action === "art-task-quick-complete") {
    applyArtTaskQuickAction(button.dataset.artTaskId, action);
    return;
  }
  if (action === "art-task-reset") {
    clearArtTaskDraft(button.dataset.artTaskId);
    return;
  }
  if (action === "art-task-save") {
    saveArtTaskDraft(button.dataset.artTaskId);
    return;
  }
  if (action === "art-create-set-kind") {
    updateArtCreateDraft("kind", button.dataset.artCreateKind || "task");
    return;
  }
  if (action === "art-create-reset") {
    resetArtCreateDraft();
    return;
  }
  if (action === "art-create-save") {
    saveArtCreateDraft();
    return;
  }
  if (action === "open-browse-art") {
    state.filters.providerId = "artSource";
    state.quickFilter = "";
    if (state.artSelectedKey && !state.artSelectedKey.startsWith("task:")) state.selectedResourceKey = state.artSelectedKey;
    setWorkspace("browse");
    render();
    return;
  }
  if (action === "open-debug") {
    const resourceKey = button.dataset.artResourceKey || state.artSelectedKey;
    if (resourceKey) {
      state.artSelectedKey = resourceKey;
      state.selectedResourceKey = resourceKey;
      state.inspectState = { id: "", status: "idle", payload: null, error: "" };
      loadInspectForSelection();
    }
    setWorkspace("debug");
    render();
  }
}

function updateArtCreateDraft(field, value) {
  const artStatus = window.MxResourceArtSourceStatus;
  const model = createArtWorkspaceModel();
  if (!artStatus || !model || typeof artStatus.updateCreateDraftField !== "function") return;
  state.artTaskCreateDraft = artStatus.updateCreateDraftField(model, state.artTaskCreateDraft, field, value);
  state.artTaskCreateState = { status: "idle", kind: field === "kind" ? value || "" : state.artTaskCreateDraft.kind || "", taskId: "", workstreamId: "", error: "", message: "" };
  renderArtWorkspace();
}

function updateArtCreateAssetDraft(assetId, checked) {
  const artStatus = window.MxResourceArtSourceStatus;
  const model = createArtWorkspaceModel();
  if (!artStatus || !model || typeof artStatus.updateCreateDraftAsset !== "function") return;
  state.artTaskCreateDraft = artStatus.updateCreateDraftAsset(model, state.artTaskCreateDraft, assetId, checked);
  state.artTaskCreateState = { status: "idle", kind: state.artTaskCreateDraft.kind || "", taskId: "", workstreamId: "", error: "", message: "" };
  renderArtWorkspace();
}

function resetArtCreateDraft() {
  const artStatus = window.MxResourceArtSourceStatus;
  state.artTaskCreateDraft = artStatus?.createDefaultCreateDraft?.() || { kind: "task" };
  state.artTaskCreateState = { status: "idle", kind: state.artTaskCreateDraft.kind || "", taskId: "", workstreamId: "", error: "", message: "" };
  renderArtWorkspace();
}

function updateArtTaskDraft(taskId, field, value) {
  const artStatus = window.MxResourceArtSourceStatus;
  const model = createArtWorkspaceModel();
  if (!artStatus || !model || typeof artStatus.updateTaskDraftField !== "function") return;
  state.artTaskDrafts = artStatus.updateTaskDraftField(model, state.artTaskDrafts, taskId, field, value);
  state.artTaskSaveState = { status: "idle", taskId: taskId || "", error: "", message: "" };
  renderArtWorkspace();
}

function updateArtTaskAssetDraft(taskId, assetId, checked) {
  const artStatus = window.MxResourceArtSourceStatus;
  const model = createArtWorkspaceModel();
  if (!artStatus || !model || typeof artStatus.updateTaskDraftAsset !== "function") return;
  state.artTaskDrafts = artStatus.updateTaskDraftAsset(model, state.artTaskDrafts, taskId, assetId, checked);
  state.artTaskSaveState = { status: "idle", taskId: taskId || "", error: "", message: "" };
  renderArtWorkspace();
}

function applyArtTaskQuickAction(taskId, action) {
  const artStatus = window.MxResourceArtSourceStatus;
  const model = createArtWorkspaceModel();
  if (!artStatus || !model || typeof artStatus.applyTaskQuickAction !== "function") return;
  state.artTaskDrafts = artStatus.applyTaskQuickAction(model, state.artTaskDrafts, taskId, action);
  state.artTaskSaveState = { status: "idle", taskId: taskId || "", error: "", message: "" };
  renderArtWorkspace();
}

function clearArtTaskDraft(taskId) {
  const artStatus = window.MxResourceArtSourceStatus;
  if (!artStatus || typeof artStatus.clearTaskDraft !== "function") return;
  state.artTaskDrafts = artStatus.clearTaskDraft(state.artTaskDrafts, taskId);
  state.artTaskSaveState = { status: "idle", taskId: taskId || "", error: "", message: "" };
  renderArtWorkspace();
}

async function saveArtTaskDraft(taskId) {
  const artStatus = window.MxResourceArtSourceStatus;
  const model = createArtWorkspaceModel();
  if (!artStatus || !model || typeof artStatus.buildTaskUpdateRequest !== "function") return;

  const request = artStatus.buildTaskUpdateRequest(model, state.artTaskDrafts, taskId);
  if (!request) {
    state.artTaskSaveState = { status: "idle", taskId: taskId || "", error: "", message: "当前任务没有未保存修改。" };
    renderArtWorkspace();
    return;
  }

  state.writeState = { status: "running", action: "save-art-task", error: "" };
  state.artTaskSaveState = { status: "running", taskId: taskId || "", error: "", message: "正在保存美术任务..." };
  state.lastActionMessage = "正在保存美术任务...";
  renderArtWorkspace();

  try {
    const payload = await postJson(API.updateArtTask, request);
    state.artTaskDrafts = artStatus.clearTaskDraft(state.artTaskDrafts, taskId);
    state.artTaskSaveState = {
      status: "success",
      taskId: taskId || "",
      error: "",
      message: `美术任务已保存：${payload.taskId || taskId}`
    };
    state.writeState = { status: "idle", action: "", error: "" };
    state.lastActionMessage = "美术任务已保存。";
    state.inspectCache.clear();
    await loadResources();
  } catch (error) {
    const conflict = error.status === 409 || error.data?.code === "ART_SOURCE_TASK_UPDATE_CONFLICT";
    state.artTaskSaveState = {
      status: conflict ? "conflict" : "error",
      taskId: taskId || "",
      error: error.message,
      message: conflict ? "任务文件已经变化，请刷新后再保存。" : "任务保存失败。"
    };
    state.writeState = { status: "error", action: "save-art-task", error: error.message };
    state.lastActionMessage = `美术任务保存失败：${error.data?.message || error.message}`;
    state.errors.push(apiError("保存美术任务", error));
  }

  renderArtWorkspace();
}

async function saveArtCreateDraft() {
  const artStatus = window.MxResourceArtSourceStatus;
  const model = createArtWorkspaceModel();
  if (!artStatus || !model || typeof artStatus.buildCreateRequest !== "function") return;

  const request = artStatus.buildCreateRequest(model, state.artTaskCreateDraft);
  if (!request) {
    const message = typeof artStatus.getCreateValidationMessage === "function"
      ? artStatus.getCreateValidationMessage(model, state.artTaskCreateDraft)
      : "创建表单不完整。";
    state.artTaskCreateState = { status: "error", kind: state.artTaskCreateDraft.kind || "", taskId: state.artTaskCreateDraft.taskId || "", workstreamId: state.artTaskCreateDraft.workstreamId || "", error: message, message };
    renderArtWorkspace();
    return;
  }

  state.writeState = { status: "running", action: "create-art-task", error: "" };
  state.artTaskCreateState = { status: "running", kind: request.kind || "", taskId: request.taskId || "", workstreamId: request.workstreamId || "", error: "", message: "正在创建美术任务..." };
  state.lastActionMessage = "正在创建美术任务...";
  renderArtWorkspace();

  try {
    const payload = await postJson(API.createArtTask, request);
    const createdKind = payload.kind || request.kind || "";
    const createdTaskId = payload.taskId || request.taskId || "";
    const createdWorkstreamId = payload.workstreamId || request.workstreamId || "";
    state.artTaskCreateDraft = artStatus.clearCreateDraft
      ? artStatus.clearCreateDraft(state.artTaskCreateDraft, payload)
      : (artStatus.createDefaultCreateDraft?.() || { kind: "task" });
    state.artTaskCreateState = {
      status: "success",
      kind: createdKind,
      taskId: createdTaskId,
      workstreamId: createdWorkstreamId,
      error: "",
      message: createdKind === "workstream" ? `已创建进程：${createdWorkstreamId}` : `已创建任务：${createdTaskId}`
    };
    if (createdTaskId) state.artSelectedKey = `task:${createdTaskId}`;
    state.writeState = { status: "idle", action: "", error: "" };
    state.lastActionMessage = createdKind === "workstream" ? "美术进程已创建。" : "美术任务已创建。";
    state.inspectCache.clear();
    await loadResources();
  } catch (error) {
    const conflict = error.status === 409 || error.data?.code === "ART_SOURCE_TASK_CREATE_CONFLICT";
    state.artTaskCreateState = {
      status: conflict ? "conflict" : "error",
      kind: state.artTaskCreateDraft.kind || "",
      taskId: state.artTaskCreateDraft.taskId || "",
      workstreamId: state.artTaskCreateDraft.workstreamId || "",
      error: error.message,
      message: conflict ? "任务文件已经变化，请刷新后再创建。" : "创建失败。"
    };
    state.writeState = { status: "error", action: "create-art-task", error: error.message };
    state.lastActionMessage = `美术任务创建失败：${error.data?.message || error.message}`;
    state.errors.push(apiError("创建美术任务", error));
  }

  renderArtWorkspace();
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
  if (action === "return-characterstudio") {
    returnSelectedResourceToCharacterStudio();
    return;
  }
  if (action === "sync-common-resources") {
    syncCommonResourcesFromManager({ allChanged: true });
    return;
  }
  if (action === "save-common-resource-scale") {
    const resourceKey = button.dataset.resourceKey || getSelectedItem()?.resourceKey || "";
    const input = document.querySelector(`[data-common-resource-scale="${cssEscapeCompat(resourceKey)}"]`);
    saveCommonResourceScaleFromManager(resourceKey, input?.value);
    return;
  }
  if (action === "open-profile") {
    setWorkspace("profile");
    render();
    return;
  }
  if (action === "open-references" || action === "open-references-current") {
    state.referencesFilter = "current";
    state.referencesBundleRuleId = "";
    setWorkspace("references");
    render();
    return;
  }
  if (action === "open-references-issues") {
    state.referencesFilter = "issues";
    state.referencesBundleRuleId = state.activeWorkspace === "profile" ? state.selectedBundleRuleId || "" : "";
    setWorkspace("references");
    render();
    return;
  }
  if (action === "open-graph-current") {
    state.graphScope = "current";
    state.graphSelectedNodeId = "";
    setWorkspace("graph");
    render();
    return;
  }
  if (action === "open-graph-issues") {
    state.graphScope = "issues";
    state.graphSelectedNodeId = "";
    setWorkspace("graph");
    render();
    return;
  }
  if (action === "open-build") {
    setWorkspace("build");
    render();
    return;
  }
  if (action === "save-profile") {
    saveBuildProfileDraft();
    return;
  }
  if (action === "show-candidates") {
    applyQuickFilter("needsProfile");
    setWorkspace("browse");
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

function buildCharacterStudioSelectionPayload(item) {
  if (!item) return null;
  const primaryBinding = getPrimaryProviderBinding(item.providerBindings || []);
  return {
    type: "mx-resource-manager-selection",
    package: state.packageRelative,
    fieldKey: state.characterStudioReturn?.fieldKey || "",
    returnKind: state.characterStudioReturn?.returnKind || "",
    resourceId: item.resourceId || "",
    libraryItemId: item.libraryItemId || "",
    resourceStableId: item.stableId || item.resourceKey || "",
    resourceKey: item.resourceKey || "",
    runtimeResourceKey: item.runtimeResourceKey || pick(primaryBinding, "runtimeResourceKey") || "",
    providerResourceKey: item.providerResourceKey || pick(primaryBinding, "providerResourceKey") || item.resourceKey || "",
    packageResourceKey: item.packageResourceKey || pick(primaryBinding, "packageResourceKey") || "",
    providerId: item.providerId || pick(primaryBinding, "providerId") || "",
    bindingKind: item.runtimeBindingKind || pick(primaryBinding, "bindingKind") || "ResourceManagerAsset",
    usage: item.usage || state.characterStudioReturn?.usage || "",
    kind: item.kind || state.characterStudioReturn?.kind || "",
    displayName: formatResourceDisplayName(item) || item.resourceKey || "",
    unityGuid: item.unityGuid || pick(primaryBinding, "unityGuid") || "",
    unityAssetPath: item.unityAssetPath || pick(primaryBinding, "unityAssetPath") || ""
  };
}

async function returnSelectedResourceToCharacterStudio() {
  const item = getSelectedItem();
  const payload = buildCharacterStudioSelectionPayload(item);
  if (!payload) return;
  if (window.opener && !window.opener.closed) {
    window.opener.postMessage(payload, window.location.origin);
    state.lastActionMessage = `Returned ${payload.resourceKey || payload.displayName} to CharacterStudio.`;
    renderStatus();
    renderContextActions();
    return;
  }
  await copyText(payload.resourceKey || payload.runtimeResourceKey || payload.providerResourceKey || "", "Copied resource key for CharacterStudio");
  state.lastActionMessage = "No CharacterStudio opener was available; copied the resource key instead.";
  renderStatus();
  renderContextActions();
}

function showUnityWorkbenchGuidance() {
  window.alert(UNITY_WORKBENCH_GUIDANCE);
}

async function copyBuildReport() {
  const plan = getBundlePlan();
  const profile = getBuildProfile();
  const status = getBuildStatus();
  const diagnostics = getBuildDiagnostics();
  const report = {
    package: state.packageRelative,
    profileSaved: !state.buildProfileDirty,
    readiness: getAuthorBuildReadiness(),
    buildStatus: status,
    bundlePlan: plan,
    validation: pick(state.buildProfilePayload, "validation"),
    diagnostics,
    diagnosticGroups: groupBuildDiagnosticsForAuthors(diagnostics),
    unityWorkbench: UNITY_WORKBENCH_GUIDANCE
  };
  await copyText(JSON.stringify(report, null, 2), "已复制构建总览");
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
  const sourceItems = getNormalizedItems();
  const items = getBrowserItems();
  const diagnostics = getAllDiagnostics();
  const connected = state.errors.length === 0 || Boolean(state.resourcesPayload || state.resourcePlanPayload);
  const planStatus = getPlanStatus();
  const packageLabel = getSelectedPackageLabel();
  const providers = getProviders();
  const unavailableProviders = providers.filter(provider => !provider.available);
  const buildProfile = getBuildProfile();
  const bundlePlan = getBundlePlan();
  const buildReadiness = getAuthorBuildReadiness();

  const serverStatusMessage = connected
    ? `已连接 Authoring 服务；当前工作上下文：${packageLabel}。资源归属和打包归属仍由 provider / Global Build Profile 决定。`
    : "未连接 Authoring 服务。请通过 Editor Hub 或启动脚本打开本工具。";
  el.serverStatus.textContent = serverStatusMessage;
  el.serverStatus.title = serverStatusMessage;

  const validationMessage = state.lastActionMessage
    ? statusChip("最近操作", state.lastActionMessage, "info")
    : "";
  el.statusStrip.innerHTML = [
    statusChip("Authoring", connected ? "已连接" : "未连接", connected ? "ok" : "error"),
    statusChip("资源", sourceItems.length === items.length ? String(items.length) : `${items.length}/${sourceItems.length}`, items.length > 0 ? "ok" : "warn"),
    statusChip("诊断", String(diagnostics.length), diagnostics.some(d => getSeverity(d) === "Error") ? "error" : diagnostics.length > 0 ? "warn" : "ok", diagnostics.length > 0 ? "open-diagnostics" : ""),
    statusChip("构建状态", getCompactBuildReadinessLabel(buildReadiness), buildReadiness.tone),
    validationMessage
  ].filter(Boolean).join("");
}

function getCompactBuildReadinessLabel(readiness) {
  if (!readiness) return "-";
  if (readiness.state === "readyForOfflineVerify") return "Offline 可验证";
  if (readiness.state === "needsUnityBuild") return "需要 Unity 构建";
  if (readiness.state === "possiblyStale") return "可能过期";
  if (readiness.state === "fixErrors") return "需修复";
  if (readiness.state === "needsSave") return "需保存";
  return readiness.label || "-";
}

function openDiagnosticsView() {
  state.activeTab = "diagnostics";
  setWorkspace("debug");
  render();
}

function renderWorkflowAssistant(root, guide) {
  if (!root || !guide) return;
  root.innerHTML = `
    <div class="workflow-current ${escapeHtml(guide.tone)}">
      <span>流程助手</span>
      <strong>${escapeHtml(guide.title)}</strong>
      <p>${escapeHtml(guide.description)}</p>
      ${guide.feedback ? `<p class="workflow-feedback">${escapeHtml(guide.feedback)}</p>` : ""}
    </div>
    <ol class="workflow-steps" aria-label="资源构建流程">
      ${guide.steps.map((step, index) => `
        <li>
          <button type="button" class="workflow-step ${escapeHtml(step.state || "waiting")}" data-guide-action="${escapeHtml(step.action || "")}"${step.disabled ? " disabled" : ""} title="${escapeHtml(step.hint || step.label)}">
            <span>${index + 1}</span>
            <strong>${escapeHtml(step.label)}</strong>
            <em>${escapeHtml(step.text || workflowStepText(step.state))}</em>
          </button>
        </li>`).join("")}
    </ol>
    <button type="button" class="workflow-next ${escapeHtml(guide.tone)}" data-guide-action="${escapeHtml(guide.action)}"${guide.disabled ? " disabled" : ""} title="${escapeHtml(guide.actionHint)}">
      ${escapeHtml(guide.actionLabel)}
    </button>`;
}

function renderActiveWorkflowAssistant() {
  if (state.activeWorkspace === "browse") {
    renderWorkflowAssistant(el.browseWorkflowAssistant, getBrowseGuideState());
  }
  if (state.activeWorkspace === "profile") {
    renderWorkflowAssistant(el.profileWorkflowAssistant, getProfileGuideState());
  }
  if (state.activeWorkspace === "build") {
    renderWorkflowAssistant(el.buildWorkflowAssistant, getBuildGuideState());
  }
}

function getBrowseGuideState() {
  const item = getSelectedItem();
  const checkedCount = state.checkedResourceKeys.size;
  const profileReady = Boolean(getBuildProfile());
  const profileEntry = item ? findDraftBuildProfileEntryForItem(item) : null;
  const deliveryState = item ? getDeliveryEntryState(item, profileEntry) : null;
  const bundleDone = Boolean(profileEntry) && deliveryState && !["需要 Bundle", "Bundle 缺失", "未加入 Profile"].includes(deliveryState.label);
  const profileSaved = profileReady && !state.buildProfileDirty;
  const buildChecked = isBuildPlanChecked();
  const unityReady = isUnityBuildReady();

  if (!state.resourcesPayload) {
    return workflowGuide(buildWorkflowSteps({ current: "select", selectedDone: false, bundleDone: false, profileSaved: false, buildChecked: false, unityReady: false }), "pending", "正在读取资源库", "等待 Authoring 服务返回资源数据。", "refresh", "刷新", "重新读取 Authoring 服务数据。");
  }

  if (!item && checkedCount === 0) {
    const candidates = getBrowserItems().filter(entry => getBuildProfileStatus(entry) === "notInProfile" && isRuntimeReadyCandidate(entry)).length;
    return workflowGuide(buildWorkflowSteps({ current: "select", selectedDone: false, bundleDone: false, profileSaved, buildChecked, unityReady }), "info", "先找推荐候选", `有 ${candidates} 个 runtime-ready 资源尚未加入 Profile。`, "showCandidates", "筛选推荐候选", "显示未加入 Profile 的 runtime-ready 资源。");
  }

  if (checkedCount > 0 && !item) {
    return workflowGuide(buildWorkflowSteps({ current: "bundle", selectedDone: true, bundleDone: false, profileSaved, buildChecked, unityReady }), "info", `已勾选 ${checkedCount} 个资源`, "下一步把可由 manual producer 管理的资源加入已有 Bundle policy。", "openProfile", "去分配 Bundle", "打开 Profile 工作区处理勾选资源。");
  }

  if (!profileReady) {
    return workflowGuide(buildWorkflowSteps({ current: "bundle", selectedDone: true, bundleDone: false, profileSaved: false, buildChecked, unityReady }), "warn", "等待 Profile 数据", "Authoring Server 尚未返回 Global Resource Build Profile。", "refresh", "刷新", "重新读取 Authoring 服务数据。");
  }

  if (!profileEntry) {
    const bundleAssignable = canAssignItemToInternalBundle(item);
    const hasBundle = getBuildProfileBundleRules().length > 0;
    const action = bundleAssignable && hasBundle ? "assignSelectedBundle" : "openProfile";
    return workflowGuide(buildWorkflowSteps({ current: "bundle", selectedDone: true, bundleDone: false, profileSaved, buildChecked, unityReady }), bundleAssignable ? "warn" : "info", "当前资源未加入 Profile", bundleAssignable ? "选择 Bundle 后加入草稿；保存后 Build 才会使用。" : `可作为配置引用加入 Profile。${describeBundleAssignBlocker(item)}`, action, bundleAssignable && hasBundle ? "加入到 Bundle" : "查看 Profile", bundleAssignable && hasBundle ? "加入当前选中 Bundle；没有选择时使用第一个 Bundle。" : "打开 Profile 工作区。");
  }

  if (!bundleDone) {
    return workflowGuide(buildWorkflowSteps({ current: "bundle", selectedDone: true, bundleDone: false, profileSaved, buildChecked, unityReady }), deliveryState.tone || "warn", deliveryState.label === "Bundle 缺失" ? "Bundle 规则缺失" : "需要分配 Bundle", `当前资源状态：${deliveryState.label}。`, "openProfile", "去分配 Bundle", "打开 Profile 工作区选择或修复 Bundle。");
  }

  if (state.buildProfileDirty) {
    return workflowGuide(buildWorkflowSteps({ current: "save", selectedDone: true, bundleDone: true, profileSaved: false, buildChecked, unityReady }), "warn", "Profile 草稿未保存", "保存后 Bundle Plan 和 Unity 构建才会使用这次修改。", "saveProfile", "保存 Profile", "通过 Authoring API 校验并保存 Profile。");
  }

  return workflowGuide(buildWorkflowSteps({ current: "build", selectedDone: true, bundleDone: true, profileSaved: true, buildChecked, unityReady }), "ok", "当前资源已可构建", "下一步查看 Build 的 Bundle Plan 和产物状态，或按 Unity 指引重新构建。", "openBuild", "查看 Build", "打开 Build 工作区。");
}

function getProfileGuideState() {
  const profileReady = Boolean(getBuildProfile());
  const bundleRules = getBuildProfileBundleRules();
  const unassigned = countUnassignedInternalEntries();
  const assigned = countAssignedInternalProfileEntries();
  const profileSaved = profileReady && !state.buildProfileDirty;
  const buildChecked = isBuildPlanChecked();
  const unityReady = isUnityBuildReady();

  if (!profileReady) {
    return workflowGuide(buildWorkflowSteps({ current: "bundle", selectedDone: Boolean(getSelectedItem()), bundleDone: false, profileSaved: false, buildChecked, unityReady }), "pending", "正在读取 Profile", "等待 Authoring Server 返回 Global Resource Build Profile。", "refresh", "刷新", "重新读取 Authoring 服务数据。");
  }

  if (bundleRules.length === 0) {
    return workflowGuide(buildWorkflowSteps({ current: "bundle", selectedDone: Boolean(getSelectedItem()), bundleDone: false, profileSaved, buildChecked, unityReady }), "info", "缺少 Bundle policy", "Bundle policy 由专属生产者维护；Resource Manager 不创建第二份规则权威。", "openProfile", "查看 Bundle policy", "在 Profile 工作区查看已有规则。");
  }

  if (unassigned > 0) {
    return workflowGuide(buildWorkflowSteps({ current: "bundle", selectedDone: true, bundleDone: false, profileSaved, buildChecked, unityReady }), "warn", "还有资源未分配 Bundle", `${unassigned} 个 internal runtime entry 还没有有效 Bundle。`, "openProfile", "处理 Bundle", "留在 Profile 工作区检查 Bundle 成员和当前资源字段。");
  }

  if (state.buildProfileDirty) {
    return workflowGuide(buildWorkflowSteps({ current: "save", selectedDone: true, bundleDone: assigned > 0, profileSaved: false, buildChecked, unityReady }), "warn", "Profile 草稿未保存", "保存后 Build 预览和 Unity 构建才会读取这些 Bundle 规则。", "saveProfile", "保存 Profile", "通过 Authoring API 校验并保存 Profile。");
  }

  const readiness = getAuthorBuildReadiness();
  if (readiness.state === "needsUnityBuild" || readiness.state === "possiblyStale") {
    return workflowGuide(buildWorkflowSteps({ current: "unity", selectedDone: true, bundleDone: assigned > 0, profileSaved: true, buildChecked, unityReady: false }), "warn", "Profile 已保存，等待 Unity 构建", readiness.nextActionLabel, "openUnity", "Unity 构建指引", "显示 Unity 菜单路径和构建步骤。");
  }
  if (readiness.state === "readyForOfflineVerify") {
    return workflowGuide(buildWorkflowSteps({ current: "build", selectedDone: true, bundleDone: assigned > 0, profileSaved: true, buildChecked, unityReady: true, offlineText: "待验证" }), "ok", "Profile 已保存", "可以去 Build 复制验证清单，再进入 Play Mode / Offline 模式验证 ResourceKey。", "openBuild", "查看 Build", "打开 Build 工作区。");
  }

  return workflowGuide(buildWorkflowSteps({ current: "build", selectedDone: true, bundleDone: assigned > 0, profileSaved: true, buildChecked, unityReady }), readiness.tone || "warn", readiness.state === "fixErrors" ? "Build 有必须处理项" : "检查 Build", readiness.nextActionLabel, "openBuild", readiness.state === "fixErrors" ? "查看必须处理" : "检查 Build", "打开 Build 工作区。");
}

function getBuildGuideState() {
  const readiness = getAuthorBuildReadiness();
  const profileSaved = Boolean(getBuildProfile()) && !state.buildProfileDirty;
  const assigned = countAssignedInternalProfileEntries();
  const buildChecked = isBuildPlanChecked();
  const unityReady = isUnityBuildReady();

  if (readiness.state === "needsSave") {
    return workflowGuide(buildWorkflowSteps({ current: "save", selectedDone: true, bundleDone: assigned > 0, profileSaved: false, buildChecked, unityReady }), "warn", "先保存 Profile", "Build 只预览已保存 Profile；当前草稿还没有进入 Bundle Plan。", "saveProfile", "保存 Profile", "通过 Authoring API 校验并保存 Profile。");
  }
  if (readiness.state === "fixErrors") {
    return workflowGuide(buildWorkflowSteps({ current: "build", selectedDone: true, bundleDone: assigned > 0, profileSaved, buildChecked: false, unityReady }), "error", "Build 有必须处理项", "错误/阻塞项需要先在诊断分组中处理；external 配置引用仍保持折叠降噪。", "openDiagnostics", "查看必须处理", "打开 Debug Diagnostics。");
  }
  if (readiness.state === "needsUnityBuild" || readiness.state === "possiblyStale") {
    return workflowGuide(buildWorkflowSteps({ current: "unity", selectedDone: true, bundleDone: assigned > 0, profileSaved, buildChecked, unityReady: false }), "warn", readiness.state === "possiblyStale" ? "产物可能过期" : "需要 Unity 构建", "Web 只读构建状态；真正的 AssetBundle 和 StreamingAssets 产物在 Unity 构建。", "openUnity", "Unity 构建指引", "显示 Unity 菜单路径和构建步骤。");
  }
  if (readiness.state === "readyForOfflineVerify") {
    return workflowGuide(buildWorkflowSteps({ current: "offline", selectedDone: true, bundleDone: assigned > 0, profileSaved, buildChecked, unityReady, offlineText: "待验证" }), "ok", "可 Offline 验证", "已有构建产物；进入 Play Mode / Offline 模式验证 ResourceKey 加载，验证前可复制清单。", "copyBuildReport", "复制验证清单", "复制 Profile、Bundle Plan、产物和诊断状态。");
  }
  return workflowGuide(buildWorkflowSteps({ current: "build", selectedDone: true, bundleDone: assigned > 0, profileSaved, buildChecked, unityReady }), "info", "刷新构建状态", readiness.nextActionLabel, "refreshBuild", "刷新 Build", "重新读取 Bundle Plan 和 Build Status。");
}

function workflowGuide(steps, tone, title, description, action, actionLabel, actionHint, disabled = false) {
  return {
    steps,
    tone,
    title,
    description,
    action,
    actionLabel,
    actionHint,
    disabled,
    feedback: state.lastActionMessage
  };
}

function buildWorkflowSteps(options = {}) {
  const current = options.current || "select";
  const definitions = [
    { id: "select", label: "选择资源", action: "showCandidates", done: options.selectedDone, hint: "进入 Browse 并筛选推荐候选。" },
    { id: "bundle", label: "分配 Bundle", action: "openProfile", done: options.bundleDone, hint: "进入 Profile 管理 Bundle 规则和成员。" },
    { id: "save", label: "保存 Profile", action: "saveProfile", done: options.profileSaved, hint: "保存当前 Global Resource Build Profile 草稿。" },
    { id: "build", label: "检查 Build", action: "refreshBuild", done: options.buildChecked, hint: "进入 Build 并刷新 Bundle Plan / Build Status。" },
    { id: "unity", label: "Unity 构建", action: "openUnity", done: options.unityReady, hint: "显示 Unity 构建菜单路径。" },
    { id: "offline", label: "Offline 验证", action: "copyBuildReport", done: false, hint: "复制验证清单；运行时验证需要在 Play Mode / Offline 模式完成。" }
  ];
  return definitions.map(definition => {
    let stateName = definition.done ? "done" : definition.id === current ? "current" : "waiting";
    let text = workflowStepText(stateName);
    if (definition.id === "offline" && options.offlineText) {
      text = options.offlineText;
      if (current !== "offline") stateName = "waiting";
    }
    return {
      ...definition,
      state: stateName,
      text,
      disabled: definition.action === "saveProfile" && !getBuildProfile()
    };
  });
}

function workflowStepText(stateName) {
  if (stateName === "done") return "完成";
  if (stateName === "current") return "当前";
  return "等待";
}

function isBuildPlanChecked() {
  const readiness = getAuthorBuildReadiness();
  const diagnosticGroups = groupBuildDiagnosticsForAuthors(getBuildDiagnostics());
  return Boolean(getBundlePlan()) && readiness.state !== "fixErrors" && diagnosticGroups.blocking.length === 0;
}

function isUnityBuildReady() {
  const buildStatus = getBuildStatus();
  const readiness = getAuthorBuildReadiness();
  return getRequiredBuildArtifactsOk(buildStatus) &&
    !Boolean(pick(buildStatus, "stale")) &&
    readiness.state === "readyForOfflineVerify";
}

async function handleGuideAction(action) {
  if (action === "refresh") {
    loadAll({ keepPackages: true });
    return;
  }
  if (action === "showCandidates") {
    applyQuickFilter("needsProfile");
    setWorkspace("browse");
    state.lastActionMessage = "已筛选 runtime-ready 且未加入 Profile 的资源。";
    render();
    return;
  }
  if (action === "openProfile") {
    setWorkspace("profile");
    render();
    return;
  }
  if (action === "assignSelectedBundle") {
    assignSelectedToBundle(state.selectedBundleRuleId || getBuildProfileBundleRules()[0]?.id || "");
    return;
  }
  if (action === "saveProfile") {
    saveBuildProfileDraft();
    return;
  }
  if (action === "openBuild") {
    setWorkspace("build");
    render();
    return;
  }
  if (action === "refreshBuild") {
    setWorkspace("build");
    state.lastActionMessage = "正在刷新 Bundle Plan 和 Build Status...";
    render();
    await Promise.all([loadBundlePlan(), loadBuildStatus()]);
    await loadResourceMapping();
    state.lastActionMessage = "Build 状态已刷新。";
    render();
    return;
  }
  if (action === "openDiagnostics") {
    openDiagnosticsView();
    return;
  }
  if (action === "openUnity") {
    showUnityWorkbenchGuidance();
    state.lastActionMessage = "已显示 Unity 构建指引。";
    renderActiveWorkflowAssistant();
    return;
  }
  if (action === "copyBuildReport") {
    copyBuildReport();
  }
}


function getNormalizedItems() {
  const cache = state.derivedCache;
  const signature = [
    state.resourcesPayload,
    state.resourcePlanPayload,
    state.buildProfilePayload,
    state.bundlePlanPayload,
    state.errors.length
  ];
  if (
    cache.normalizedItems
    && cache.normalizedSignature
    && cache.normalizedSignature[0] === signature[0]
    && cache.normalizedSignature[1] === signature[1]
    && cache.normalizedSignature[2] === signature[2]
    && cache.normalizedSignature[3] === signature[3]
    && cache.normalizedSignature[4] === signature[4]
  ) {
    return cache.normalizedItems;
  }

  const items = asArray(pick(state.resourcesPayload, "items", "entries", "Items", "Entries"));
  cache.normalizedSignature = signature;
  cache.normalizedItems = items.map((raw, index) => normalizeItem(raw, index));
  cache.browserSource = null;
  cache.browserItems = null;
  return cache.normalizedItems;
}

function getBrowserItems() {
  const normalized = getNormalizedItems();
  const cache = state.derivedCache;
  if (cache.browserItems && cache.browserSource === normalized) {
    return cache.browserItems;
  }

  cache.browserSource = normalized;
  cache.browserItems = createCanonicalResourceItems(normalized);
  return cache.browserItems;
}

function createCanonicalResourceItems(items) {
  const rows = asArray(items);
  if (rows.length <= 1) return rows.map(item => decorateCanonicalItem(item, [item]));

  const parents = rows.map((_, index) => index);
  const identityOwners = new Map();
  const find = index => {
    while (parents[index] !== index) {
      parents[index] = parents[parents[index]];
      index = parents[index];
    }
    return index;
  };
  const union = (left, right) => {
    const leftRoot = find(left);
    const rightRoot = find(right);
    if (leftRoot !== rightRoot) parents[rightRoot] = leftRoot;
  };

  rows.forEach((item, index) => {
    const identities = getCanonicalItemIdentities(item);
    for (const identity of identities) {
      if (identityOwners.has(identity)) {
        union(index, identityOwners.get(identity));
      } else {
        identityOwners.set(identity, index);
      }
    }
  });

  const groups = new Map();
  rows.forEach((item, index) => {
    const root = find(index);
    if (!groups.has(root)) groups.set(root, []);
    groups.get(root).push(item);
  });

  return Array.from(groups.values()).map(group => {
    const sorted = [...group].sort((a, b) => getCanonicalItemPriority(b) - getCanonicalItemPriority(a) || compareText(formatResourceDisplayName(a), formatResourceDisplayName(b)));
    return decorateCanonicalItem(sorted[0], sorted);
  });
}

function decorateCanonicalItem(primary, peers) {
  const foldedItems = asArray(peers).length > 0 ? asArray(peers) : [primary];
  const displayName = chooseCanonicalDisplayName(foldedItems);
  const identity = chooseCanonicalIdentity(foldedItems);
  const references = uniqueRows(foldedItems.flatMap(item => asArray(item.references)));
  const diagnostics = mergeDiagnostics(foldedItems.flatMap(item => asArray(item.allDiagnostics)), []);
  const providerIds = uniqueStrings(foldedItems.map(item => item.providerId).filter(Boolean));
  const runtimeAvailability = chooseBestRuntimeAvailability(foldedItems) || primary.runtimeAvailability;
  const runtimeBindingKind = chooseBestRuntimeBindingKind(foldedItems) || primary.runtimeBindingKind;
  return {
    ...primary,
    displayName,
    canonicalDisplayName: displayName,
    canonicalIdentity: identity,
    foldedItems,
    foldedCount: foldedItems.length,
    foldedAliases: uniqueStrings(foldedItems.flatMap(getResourceAliases)),
    providerIds,
    runtimeAvailability,
    runtimeBindingKind,
    references,
    referenceCount: references.length > 0 ? references.length : Math.max(...foldedItems.map(item => item.referenceCount || 0), 0),
    allDiagnostics: diagnostics,
    diagnosticCount: diagnostics.length,
    isOrphan: foldedItems.some(item => item.isOrphan)
  };
}

function getCanonicalItemIdentities(item) {
  const identities = [];
  const kind = normalizeLabelSegment(item.kind || "resource");
  for (const key of [item.canonicalIdentity, item.technicalIdentity]) {
    if (!key) continue;
    identities.push(`resource:${kind}:${String(key).toLowerCase()}`);
  }

  if (item.unityGuid) identities.push(`unity-guid:${kind}:${item.unityGuid.toLowerCase()}`);
  for (const path of [
    item.unityAssetPath,
    item.sourcePath,
    item.metadata?.unityAssetPath,
    item.metadata?.assetPath,
    item.metadata?.address,
    item.metadata?.sourceRelativePath,
    item.metadata?.relativePath,
    item.metadata?.targetRelativePath
  ]) {
    const normalized = normalizeResourcePath(path);
    if (normalized) identities.push(`path:${kind}:${normalized}`);
  }

  if (item.fmodEventPath) identities.push(`fmod-event:${item.fmodEventPath.toLowerCase()}`);
  if (item.audioCueId) identities.push(`audio-cue:${item.audioCueId.toLowerCase()}`);
  if (item.audioEventDefinitionId) identities.push(`audio-event:${item.audioEventDefinitionId.toLowerCase()}`);
  if (identities.length === 0) identities.push(`item:${item.key}`);
  return uniqueStrings(identities);
}

function getCanonicalItemPriority(item) {
  let score = RESOURCE_PROVIDER_PRIORITY[item.providerId] ?? 300;
  if (isRuntimeLoadable(item)) score += 180;
  if (item.canonicalIdentity) score += 140;
  if (item.canonicalDisplayName) score += 80;
  if (String(item.runtimeAvailability || "").toLowerCase().includes("editor")) score -= 60;
  if (looksLikeAssetPath(item.displayName)) score -= 80;
  return score;
}

function chooseCanonicalDisplayName(items) {
  const preferred = [...items].sort((a, b) => getCanonicalItemPriority(b) - getCanonicalItemPriority(a));
  for (const item of preferred) {
    if (item.canonicalDisplayName) return item.canonicalDisplayName;
  }
  return preferred[0]?.displayName || preferred[0]?.name || "resource";
}

function chooseCanonicalIdentity(items) {
  for (const item of [...items].sort((a, b) => getCanonicalItemPriority(b) - getCanonicalItemPriority(a))) {
    if (item.canonicalIdentity) return item.canonicalIdentity;
  }
  for (const item of items) {
    for (const value of [item.technicalIdentity, item.stableId, item.libraryItemId, item.resourceKey, item.key]) {
      if (value) return value;
    }
  }
  return "";
}

function formatResourceDisplayName(item) {
  return item?.canonicalDisplayName || item?.displayName || item?.name || "resource";
}

function formatResourceIdentity(item) {
  return item?.technicalIdentity || item?.canonicalIdentity || chooseCanonicalIdentity([item]) || "-";
}

function formatResourceProviderStack(item) {
  const providers = item?.providerIds?.length ? item.providerIds : uniqueStrings([item?.providerId].filter(Boolean));
  if (providers.length === 0) return "provider";
  return providers.length <= 2 ? providers.join(" + ") : `${providers.slice(0, 2).join(" + ")} +${providers.length - 2}`;
}

function getResourceAliases(item) {
  return uniqueStrings([
    item?.runtimeResourceKey,
    item?.packageResourceKey,
    item?.resourceKey,
    item?.providerResourceKey,
    item?.stableId,
    item?.libraryItemId
  ].map(value => stringValue(value).trim()).filter(Boolean));
}

function chooseBestRuntimeAvailability(items) {
  const values = items.map(item => item.runtimeAvailability).filter(Boolean);
  if (values.some(value => String(value).toLowerCase() === "runtimeready")) return "RuntimeReady";
  if (values.some(value => String(value).toLowerCase() === "audiocueonly")) return "AudioCueOnly";
  return values.find(value => !String(value).toLowerCase().includes("unknown")) || values[0] || "";
}

function chooseBestRuntimeBindingKind(items) {
  const values = items.map(item => item.runtimeBindingKind).filter(Boolean);
  return values.find(value => String(value).toLowerCase() === "resourcemanagerasset")
    || values.find(value => !String(value).toLowerCase().includes("editor"))
    || values[0]
    || "";
}

function uniqueRows(rows) {
  const seen = new Set();
  const result = [];
  for (const row of asArray(rows)) {
    const key = stableJson(row);
    if (seen.has(key)) continue;
    seen.add(key);
    result.push(row);
  }
  return result;
}

function uniqueStrings(values) {
  return Array.from(new Set(asArray(values).map(value => stringValue(value)).filter(Boolean)));
}

function looksLikeAssetPath(value) {
  const text = String(value || "");
  if (/(^|\/|\\)Assets[\\/]/i.test(text)) return true;
  if (/[\\/]/.test(text) && RESOURCE_FILE_EXTENSION_PATTERN.test(text)) return true;
  if (!RESOURCE_FILE_EXTENSION_PATTERN.test(text)) return false;
  return (text.split(/[\\/]/).pop() || text).split(".").length <= 2;
}

function normalizeResourcePath(value) {
  const text = stringValue(value).trim().replace(/\\/g, "/").replace(/^\/+/, "");
  if (!text) return "";
  return text.toLowerCase();
}

function stripResourceExtension(value) {
  return String(value || "").replace(RESOURCE_FILE_EXTENSION_PATTERN, "");
}


function normalizeItem(raw, index) {
  const resourceId = stringValue(pick(raw, "resourceId"));
  const providerBindings = asArray(pick(raw, "providerBindings"));
  const primaryBinding = getPrimaryProviderBinding(providerBindings);
  const primaryProviderData = pick(primaryBinding, "providerData") || {};
  const metadata = pick(raw, "metadata") || {};
  const libraryItemId = stringValue(pick(raw, "libraryItemId", "id", "localId")) || resourceId;
  const stableId = stringValue(pick(raw, "stableId", "libraryItemStableId", "resourceStableId"));
  const runtimeResourceKey = firstString(pick(raw, "runtimeResourceKey"), pick(metadata, "runtimeResourceKey"), pick(primaryBinding, "runtimeResourceKey"), firstBindingValue(providerBindings, "runtimeResourceKey"));
  const providerResourceKey = firstString(pick(raw, "providerResourceKey"), pick(metadata, "providerResourceKey"), pick(primaryBinding, "providerResourceKey"), firstBindingValue(providerBindings, "providerResourceKey"));
  const packageResourceKey = firstString(pick(raw, "packageResourceKey"), pick(metadata, "packageResourceKey"), pick(primaryBinding, "packageResourceKey"), firstBindingValue(providerBindings, "packageResourceKey"));
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
    canonicalDisplayName: firstString(pick(raw, "canonicalName"), pick(metadata, "canonicalName")),
    canonicalIdentity: firstString(pick(raw, "canonicalIdentity"), pick(metadata, "canonicalIdentity")),
    technicalIdentity: firstString(pick(raw, "technicalIdentity"), pick(metadata, "technicalIdentity")),
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
    sourcePath: firstString(
      pick(raw, "sourcePath", "relativePath", "path"),
      pick(metadata, "relativePath", "sourceRelativePath", "targetRelativePath", "assetPath", "address"),
      pick(primaryProviderData, "relativePath", "sourceRelativePath", "targetRelativePath", "assetPath", "address"),
      pick(primaryBinding, "externalSourcePath")
    ),
    unityAssetPath: firstString(
      pick(raw, "unityAssetPath"),
      pick(metadata, "unityAssetPath", "assetPath"),
      pick(primaryProviderData, "unityAssetPath", "assetPath"),
      pick(primaryBinding, "unityAssetPath")
    ),
    unityGuid: firstString(
      pick(raw, "unityGuid"),
      pick(metadata, "unityGuid", "unityAssetGuid"),
      pick(primaryProviderData, "unityGuid", "unityAssetGuid"),
      pick(primaryBinding, "unityGuid")
    ),
    fmodEventPath: stringValue(pick(raw, "fmodEventPath")) || stringValue(pick(primaryBinding, "fmodEventPath")),
    audioCueId: stringValue(pick(raw, "audioCueId")) || stringValue(pick(primaryProviderData, "audioCueId")),
    audioEventDefinitionId: stringValue(pick(raw, "audioEventDefinitionId")) || stringValue(pick(primaryProviderData, "audioEventDefinitionId")),
    audioSemanticStatus: stringValue(pick(metadata, "audioSemanticStatus")),
    audioSemanticPurpose: stringValue(pick(metadata, "audioSemanticPurpose")),
    audioSemanticContext: stringValue(pick(metadata, "audioSemanticContext")),
    audioSemanticTags: stringValue(pick(metadata, "audioSemanticTags")),
    sourcePreviewPath: stringValue(pick(metadata, "sourcePreviewPath")),
    auditionStatus: stringValue(pick(metadata, "auditionStatus")),
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
      item.canonicalDisplayName,
      item.canonicalIdentity,
      item.kind,
      item.usage,
      item.providerId,
      asArray(item.providerIds).join(" "),
      item.sourceKind,
      item.importStatus,
      item.runtimeAvailability,
      item.resourceKey,
      item.runtimeResourceKey,
      item.providerResourceKey,
      item.packageResourceKey,
      item.sourcePath,
      item.unityAssetPath,
      asArray(item.foldedAliases).join(" "),
      item.tags.join(" ")
    ].join(" ").toLowerCase().includes(search);
  });
}

function getSelectedItem() {
  return getBrowserItems().find(item => selectionMatchesItem(item, state.selectedResourceKey)) || null;
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
  if (asArray(item?.foldedItems).some(peer => peer !== item && matchesItem(peer, stableId, resourceKey, resourceId))) return true;
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
  let copied = false;
  try {
    if (typeof navigator !== "undefined" && navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      copied = true;
    }
  } catch {
    copied = false;
  }

  if (!copied) {
    copied = copyTextWithHiddenTextarea(text);
  }

  if (copied) {
    setCopyStatus(successMessage);
    return;
  }

  try {
    if (typeof window.prompt === "function") {
      window.prompt("复制 JSON", text);
      setCopyStatus("已打开复制窗口");
      return;
    }
  } catch {
    // Browser automation and some hardened WebViews disable prompt; keep visible UI feedback.
  }

  setCopyStatus("复制失败：浏览器阻止剪贴板，请从 Debug 复制 JSON。");
}

function copyTextWithHiddenTextarea(text) {
  try {
    if (typeof document.execCommand !== "function") return false;
    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.setAttribute("readonly", "");
    textarea.style.position = "fixed";
    textarea.style.left = "-9999px";
    textarea.style.top = "0";
    document.body.appendChild(textarea);
    textarea.select();
    const copied = document.execCommand("copy");
    textarea.remove();
    return copied;
  } catch {
    return false;
  }
}

function setCopyStatus(message) {
  el.copyStatus.textContent = message;
  state.lastActionMessage = message;
  renderStatus();
  renderActiveWorkflowAssistant();
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
  if (value.includes("scene")) return "场景资源";
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

function getStorySourceServerError(error) {
  const diagnostics = getStorySourceServerDiagnostics(error);
  const data = error && error.data;
  const apiMessage = (data && (data.message || data.Message || data.error || data.Error)) || "";
  if (diagnostics) {
    return apiMessage ? `${apiMessage}：${diagnostics}` : diagnostics;
  }
  return apiMessage || error?.message || String(error);
}

function getStorySourceServerDiagnostics(error) {
  const data = error && error.data;
  const diagnostics = Array.isArray(data?.diagnostics) ? data.diagnostics : Array.isArray(data?.Diagnostics) ? data.Diagnostics : [];
  return diagnostics
    .map(diagnostic => diagnostic && (diagnostic.message || diagnostic.Message || diagnostic.code || diagnostic.Code))
    .filter(Boolean)
    .slice(0, 5)
    .join("；");
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

function firstString(...values) {
  for (const value of values) {
    const text = stringValue(value).trim();
    if (text) return text;
  }
  return "";
}

function firstBindingValue(bindings, ...keys) {
  for (const binding of asArray(bindings)) {
    for (const key of keys) {
      const value = stringValue(pick(binding, key)).trim();
      if (value) return value;
    }
  }
  return "";
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
