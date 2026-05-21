const DEFAULT_PACKAGE = "Tools/MxFramework.Authoring/samples/character-iron-vanguard";

const API = {
  packages: "/api/authoring/animation/packages",
  load: packageRelative => `/api/authoring/animation/load?package=${encodeURIComponent(packageRelative)}`,
  save: "/api/authoring/animation/save",
  validate: "/api/authoring/animation/validate",
  compile: "/api/authoring/animation/compile",
  preview: "/api/authoring/animation/preview",
  pick: "/api/authoring/resources/pick",
  resolveSelection: "/api/authoring/resources/resolve-selection"
};

const FALLBACK_SOURCE_CLIP_SPEC = {
  fieldKey: "Animation.SourceClip",
  editorKind: "AnimationEditor",
  displayName: "Source Clip",
  acceptedKinds: ["animation"],
  acceptedUsages: ["animationClip", "animationClipGroup"],
  acceptedBindingKinds: ["UnityEditorOnlyAsset", "UnityAsset", "ResourceManagerAsset", "PackageResource"],
  preloadPolicy: "AnimationWarmup",
  outputKind: "ResourceSelectionRef"
};

const FALLBACK_EVENT_VFX_SPEC = {
  fieldKey: "Animation.EventVfx",
  editorKind: "AnimationEditor",
  displayName: "Event VFX",
  acceptedKinds: ["vfx"],
  acceptedUsages: ["vfxCue"],
  acceptedBindingKinds: ["ResourceManagerAsset", "PackageResource", "UnityAsset"],
  preloadPolicy: "VfxWarmup",
  outputKind: "ResourceSelectionRef"
};

const FALLBACK_EVENT_AUDIO_CUE_SPEC = {
  fieldKey: "Animation.EventAudioCue",
  editorKind: "AnimationEditor",
  displayName: "Event Audio Cue",
  acceptedKinds: ["audio"],
  acceptedUsages: ["audioCue", "fmodEvent"],
  acceptedBindingKinds: ["AudioCue", "AudioEventDefinition"],
  preloadPolicy: "AudioBank",
  outputKind: "AudioCueId"
};

const FALLBACK_AVATAR_MASK_SPEC = {
  fieldKey: "Animation.AvatarMask",
  editorKind: "AnimationEditor",
  displayName: "Avatar Mask",
  acceptedKinds: ["avatarMask"],
  acceptedUsages: ["avatarMask"],
  acceptedBindingKinds: ["UnityAsset", "ResourceManagerAsset", "PackageResource"],
  preloadPolicy: "AnimationWarmup",
  outputKind: "ResourceSelectionRef"
};

const FALLBACK_BAKE_ARTIFACT_SPEC = {
  fieldKey: "Animation.BakeArtifact",
  editorKind: "AnimationEditor",
  displayName: "Bake Artifact",
  acceptedKinds: ["generated", "config"],
  acceptedUsages: ["animationBakeArtifact"],
  acceptedBindingKinds: ["GeneratedPreviewOnly", "ResourceManagerAsset", "PackageResource"],
  preloadPolicy: "AnimationWarmup",
  outputKind: "ResourceSelectionRef"
};

const FALLBACK_COMPATIBILITY_PROFILE_SPEC = {
  fieldKey: "Animation.CompatibilityProfile",
  editorKind: "AnimationEditor",
  displayName: "Compatibility Profile",
  acceptedKinds: ["config"],
  acceptedUsages: ["animationCompatibilityProfile"],
  acceptedBindingKinds: ["ResourceManagerAsset", "PackageResource", "UnityAsset"],
  preloadPolicy: "PresentationCritical",
  outputKind: "ResourceSelectionRef"
};

const FALLBACK_ADDITIONAL_RESOURCE_SPEC = {
  fieldKey: "Animation.AdditionalWarmupResource",
  editorKind: "AnimationEditor",
  displayName: "Additional Warmup Resource",
  acceptedKinds: [],
  acceptedUsages: [],
  acceptedBindingKinds: [],
  preloadPolicy: "AnimationWarmup",
  outputKind: "ResourceSelectionRef"
};

const ROOT_MOTION_OPTIONS = ["Ignore", "MotionDelta", "ApplyToActorReference"];
const TIME_DOMAIN_OPTIONS = ["Seconds", "Normalized", "PresentationFrame", "CombatFrame"];
const PRELOAD_POLICY_OPTIONS = ["None", "SpawnCritical", "PresentationCritical", "EquipmentInitial", "AnimationWarmup", "VfxWarmup", "UiDeferred", "Audio", "AudioBank"];
const COORDINATE_CONVENTION_OPTIONS = ["", "YPositive/ZPositive/LeftHanded", "YPositive/ZPositive/RightHanded", "ZPositive/YPositive/LeftHanded"];
const PREVIEW_TARGET_OPTIONS = [
  { value: "skeleton", label: "Skeleton" },
  { value: "characterPackage", label: "Character Package" },
  { value: "modelResource", label: "Model Resource" }
];
const EVENT_KIND_OPTIONS = [
  { value: "Footstep", label: "footstep" },
  { value: "TraceOn", label: "trace on" },
  { value: "TraceOff", label: "trace off" },
  { value: "HitMarker", label: "hit marker" },
  { value: "Vfx", label: "VFX" },
  { value: "AudioCue", label: "AudioCue" },
  { value: "CameraCue", label: "camera cue" },
  { value: "Custom", label: "custom" }
];

let threeRuntimePromise = null;

const state = {
  packages: [],
  packageRelative: DEFAULT_PACKAGE,
  documentRelative: "",
  exists: false,
  canWrite: false,
  animation: null,
  fieldSpecs: {},
  validation: null,
  compileResult: null,
  selected: { kind: "package", setId: "", groupId: "", clipId: "" },
  blendEditor: { view: "1D", blendId: "" },
  timelineEditor: { timelineId: "" },
  previewWorkflow: { targetType: "skeleton" },
  preview3d: {
    loading: false,
    result: null,
    error: "",
    playing: false,
    selectedClipId: "",
    renderId: 0,
    cleanup: null,
    threeStatus: "idle"
  },
  resourcePicker: {
    open: false,
    clip: null,
    target: null,
    title: "",
    fieldSpec: null,
    rows: [],
    search: "",
    onlySelectable: true,
    loading: false,
    error: ""
  },
  lastMessage: "",
  errors: []
};

const el = {};

document.addEventListener("DOMContentLoaded", () => {
  cacheElements();
  bindEvents();
  const queryPackage = new URLSearchParams(window.location.search).get("package");
  if (queryPackage) state.packageRelative = queryPackage;
  refreshAll();
});

function cacheElements() {
  for (const id of [
    "serverStatus", "packageSelect", "setSelect", "refreshButton", "saveButton", "validateButton",
    "compileButton", "previewButton", "openResourceManagerButton", "openCharacterStudioButton", "statusStrip", "treeSummary",
    "addSetButton", "animationTree", "workspaceTitle", "workspaceSubtitle", "addGroupButton",
    "addClipButton", "mappingWorkspace", "inspectorSubtitle", "inspectorContent",
    "diagnosticsSummary", "copyDiagnosticsButton", "diagnosticsList", "resourcePickerOverlay",
    "resourcePickerTitle", "resourcePickerSubtitle", "closePickerButton", "resourcePickerSearch",
    "resourcePickerOnlySelectable", "resourcePickerList"
  ]) {
    el[id] = document.getElementById(id);
  }
}

function bindEvents() {
  el.refreshButton.addEventListener("click", refreshAll);
  el.packageSelect.addEventListener("change", event => {
    state.packageRelative = event.target.value;
    syncPackageQuery();
    loadAnimation();
  });
  el.setSelect.addEventListener("change", event => {
    selectSet(event.target.value);
  });
  el.saveButton.addEventListener("click", saveAnimation);
  el.validateButton.addEventListener("click", validateAnimation);
  el.compileButton.addEventListener("click", compileAnimation);
  el.previewButton.addEventListener("click", previewAnimation);
  el.addSetButton.addEventListener("click", addSet);
  el.addGroupButton.addEventListener("click", addGroup);
  el.addClipButton.addEventListener("click", addClip);
  el.copyDiagnosticsButton.addEventListener("click", copyDiagnostics);

  el.animationTree.addEventListener("click", event => {
    const button = event.target.closest("button[data-select-kind]");
    if (!button) return;
    selectNode(button.dataset.selectKind, button.dataset.setId || "", button.dataset.groupId || "", button.dataset.clipId || "");
  });

  el.mappingWorkspace.addEventListener("click", event => {
    const button = event.target.closest("button");
    if (!button) return;
    if (button.dataset.selectClip) {
      selectNode("clip", state.selected.setId, state.selected.groupId, button.dataset.selectClip);
    } else if (button.dataset.pickClip) {
      const clip = findClip(state.selected.setId, state.selected.groupId, button.dataset.pickClip);
      openSourceClipPicker(clip);
    } else if (button.dataset.removeClip) {
      removeClip(button.dataset.removeClip);
    } else if (button.dataset.blendView) {
      state.blendEditor.view = button.dataset.blendView;
      ensureBlendSelection(findGroup(state.selected.setId, state.selected.groupId));
      renderBlendEditorState();
    } else if (button.dataset.addBlend) {
      addBlend(button.dataset.addBlend);
    } else if (button.dataset.removeBlend) {
      removeBlend(button.dataset.removeBlend);
    } else if (button.dataset.addBlendPoint) {
      addBlendPoint(button.dataset.addBlendPoint);
    } else if (button.dataset.removeBlendPoint) {
      removeBlendPoint(button.dataset.removeBlendPoint, Number(button.dataset.pointIndex));
    } else if (button.dataset.addTimeline) {
      addTimeline();
    } else if (button.dataset.removeTimeline) {
      removeTimeline();
    } else if (button.dataset.addTimelineEvent) {
      addTimelineEvent();
    } else if (button.dataset.removeTimelineEvent) {
      removeTimelineEvent(Number(button.dataset.eventIndex));
    } else if (button.dataset.copyTimelineContext) {
      copyTimelineContext();
    } else if (button.dataset.runCompilerPreview) {
      previewAnimation();
    } else if (button.dataset.previewPlayToggle) {
      togglePreviewPlayback();
    } else if (button.dataset.previewClipId) {
      selectPreviewClip(button.dataset.previewClipId);
    } else if (button.dataset.pickEventResource) {
      const timeline = getSelectedTimeline(findGroup(state.selected.setId, state.selected.groupId));
      const eventItem = timeline ? (timeline.events || [])[Number(button.dataset.eventIndex)] : null;
      openEventResourcePicker(timeline, eventItem, button.dataset.pickEventResource);
    } else if (handleStructureButtonClick(button)) {
      return;
    } else if (handleSelectionButtonClick(button)) {
      return;
    }
  });
  el.mappingWorkspace.addEventListener("change", handleBlendEditorInput);
  el.mappingWorkspace.addEventListener("input", handleBlendEditorInput);
  el.mappingWorkspace.addEventListener("change", handleTimelineEditorInput);
  el.mappingWorkspace.addEventListener("input", handleTimelineEditorInput);
  el.mappingWorkspace.addEventListener("change", handlePreviewWorkflowInput);
  el.mappingWorkspace.addEventListener("change", handleStructureEditorInput);
  el.mappingWorkspace.addEventListener("input", handleStructureEditorInput);

  el.inspectorContent.addEventListener("input", handleInspectorInput);
  el.inspectorContent.addEventListener("change", handleInspectorInput);
  el.inspectorContent.addEventListener("click", event => {
    const pickerButton = event.target.closest("button[data-open-source-picker]");
    if (pickerButton) {
      const clip = findSelectedClip();
      openSourceClipPicker(clip);
      return;
    }
    const button = event.target.closest("button");
    if (button && handleSelectionButtonClick(button)) {
      return;
    }
  });

  el.closePickerButton.addEventListener("click", closeResourcePicker);
  el.resourcePickerSearch.addEventListener("input", event => {
    state.resourcePicker.search = event.target.value;
    renderResourcePicker();
  });
  el.resourcePickerOnlySelectable.addEventListener("change", event => {
    state.resourcePicker.onlySelectable = event.target.checked;
    renderResourcePicker();
  });
  el.resourcePickerList.addEventListener("click", event => {
    const button = event.target.closest("button[data-resource-index]");
    if (!button) return;
    chooseResourcePickerRow(Number(button.dataset.resourceIndex));
  });
}

async function refreshAll() {
  state.errors = [];
  el.serverStatus.textContent = "正在读取 Animation Authoring API...";
  await loadPackages();
  await loadAnimation();
}

async function loadPackages() {
  const packages = await readJson(API.packages, [], "动画包列表");
  state.packages = Array.isArray(packages) && packages.length > 0
    ? packages
    : [{ relative: DEFAULT_PACKAGE, packageId: "animation.iron_vanguard", displayName: "Iron Vanguard Animation" }];
  if (!state.packages.some(item => item.relative === state.packageRelative)) {
    state.packageRelative = state.packages[0].relative || DEFAULT_PACKAGE;
  }
}

async function loadAnimation() {
  const payload = await readJson(API.load(state.packageRelative), null, "动画包");
  if (!payload) {
    state.animation = createEmptyAnimationPackage();
    state.validation = null;
    state.fieldSpecs = {};
    render();
    return;
  }

  state.documentRelative = payload.documentRelative || "";
  state.exists = Boolean(payload.exists);
  state.canWrite = payload.canWrite !== false;
  state.animation = payload.package || createEmptyAnimationPackage();
  state.fieldSpecs = payload.fieldSpecs || {};
  state.validation = payload.validation || null;
  clearPreview3d("动画包已重新读取");
  ensureAnimationShape();
  ensureSelection();
  render();
}

async function saveAnimation() {
  ensureAnimationShape();
  const payload = await postJson(API.save, {
    package: state.packageRelative,
    animation: state.animation
  }, "保存动画包");
  if (payload?.package) {
    state.documentRelative = payload.documentRelative || state.documentRelative;
    state.exists = Boolean(payload.exists);
    state.animation = payload.package;
    state.fieldSpecs = payload.fieldSpecs || state.fieldSpecs;
    state.validation = payload.validation || state.validation;
    state.lastMessage = "已保存 animation_authoring.json";
    ensureSelection();
    render();
  }
}

async function validateAnimation() {
  const report = await postJson(API.validate, {
    package: state.packageRelative,
    animation: state.animation
  }, "校验动画包");
  if (report) {
    state.validation = report;
    state.lastMessage = "校验完成";
    render();
  }
}

async function compileAnimation() {
  ensureAnimationShape();
  const result = await postJson(API.compile, {
    package: state.packageRelative,
    animation: state.animation
  }, "编译动画包");
  if (result) {
    state.compileResult = result;
    const planCount = result.animationResourcePlan?.characterResourcePlan?.animationWarmup?.resources?.length
      || result.characterResourcePlan?.animationWarmup?.resources?.length
      || result.characterPlan?.animationWarmup?.resources?.length
      || 0;
    state.lastMessage = `编译预检完成：AnimationWarmup ${planCount}`;
    if (result.animationValidationReport || result.validationReport) state.validation = result.animationValidationReport || result.validationReport;
    render();
  }
}

async function previewAnimation() {
  ensureAnimationShape();
  cleanupPreviewViewport();
  state.preview3d.loading = true;
  state.preview3d.error = "";
  state.preview3d.result = null;
  state.preview3d.threeStatus = "loading";
  state.lastMessage = "正在请求编译预览";
  render();

  const result = await postJson(API.preview, {
    package: state.packageRelative,
    animation: state.animation
  }, "编译预览");

  state.preview3d.loading = false;
  if (!result) {
    state.preview3d.error = "编译预览失败，请查看诊断。";
    state.preview3d.threeStatus = "error";
    render();
    return;
  }

  state.preview3d.result = result;
  state.compileResult = result.compileResult || state.compileResult;
  if (result.animationValidationReport) state.validation = result.animationValidationReport;
  const clips = getPreviewAnimationClips(result);
  if (!clips.some(clip => getPreviewClipId(clip) === state.preview3d.selectedClipId)) {
    state.preview3d.selectedClipId = getPreviewClipId(clips[0]) || "";
  }
  state.lastMessage = `编译预览完成：${getPreviewResources(result).length} 个资源，${clips.length} 个 Clip`;
  render();
}

async function readJson(url, fallback, label) {
  try {
    const response = await fetch(url, { headers: { Accept: "application/json" } });
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return await response.json();
  } catch (error) {
    state.errors.push({ label, message: error.message });
    return fallback;
  }
}

async function postJson(url, body, label) {
  try {
    const response = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify(body)
    });
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return await response.json();
  } catch (error) {
    state.errors.push({ label, message: error.message });
    state.lastMessage = `${label}失败：${error.message}`;
    renderStatus();
    return null;
  }
}

function render() {
  renderPackageSelect();
  renderLinks();
  renderStatus();
  renderSetSelect();
  renderTree();
  renderWorkspace();
  renderInspector();
  renderDiagnostics();
}

function renderPackageSelect() {
  el.packageSelect.innerHTML = state.packages.map(item => {
    const label = `${item.displayName || item.packageId || item.relative} (${item.exists ? "已保存" : "草稿"})`;
    return `<option value="${escapeHtml(item.relative || "")}"${item.relative === state.packageRelative ? " selected" : ""}>${escapeHtml(label)}</option>`;
  }).join("");
}

function renderLinks() {
  const packageQuery = encodeURIComponent(state.packageRelative);
  el.openResourceManagerButton.href = `/Tools/MxFramework.ResourceLibrary/web/?package=${packageQuery}`;
  el.openCharacterStudioButton.href = `/Tools/MxFramework.CharacterStudio/web/?package=${packageQuery}`;
}

function renderStatus() {
  const animation = state.animation || {};
  const issueCount = getIssues().length;
  const setCount = Array.isArray(animation.sets) ? animation.sets.length : 0;
  const groupCount = getAllGroups().length;
  const clipCount = getAllClips().length;
  const profileCount = Array.isArray(animation.profiles) ? animation.profiles.length : 0;
  const layerCount = getAllLayers().length;
  const bindingCount = getAllActionBindings().length;
  const connected = state.errors.length === 0 || state.animation;
  el.serverStatus.textContent = connected
    ? `已连接 Authoring 服务；文档：${state.documentRelative || "未保存"}`
    : "未连接 Authoring 服务。请通过启动脚本打开。";

  el.statusStrip.innerHTML = [
    statusChip("Authoring", connected ? "已连接" : "未连接", connected ? "ok" : "error"),
    statusChip("AnimationPackage", animation.packageId || "未命名", "info"),
    statusChip("Sets", String(setCount), setCount > 0 ? "ok" : "warn"),
    statusChip("Groups", String(groupCount), groupCount > 0 ? "ok" : "warn"),
    statusChip("Clips", String(clipCount), clipCount > 0 ? "ok" : "warn"),
    statusChip("Profiles", String(profileCount), profileCount > 0 ? "ok" : "warn"),
    statusChip("Layers", String(layerCount), layerCount > 0 ? "ok" : "warn"),
    statusChip("Bindings", String(bindingCount), bindingCount > 0 ? "ok" : "warn"),
    statusChip("预览", getPreviewStatusLabel(), getPreviewStatusTone()),
    statusChip("Diagnostics", String(issueCount), issueCount === 0 ? "ok" : "warn"),
    state.lastMessage ? statusChip("操作", state.lastMessage, "info") : ""
  ].join("");
}

function renderSetSelect() {
  const sets = state.animation?.sets || [];
  el.setSelect.innerHTML = sets.length
    ? sets.map(set => `<option value="${escapeHtml(set.setId || "")}"${set.setId === state.selected.setId ? " selected" : ""}>${escapeHtml(set.displayName || set.setId || "set")}</option>`).join("")
    : `<option value="">未创建 Set</option>`;
}

function renderTree() {
  const sets = state.animation?.sets || [];
  const profiles = state.animation?.profiles || [];
  el.treeSummary.textContent = `${sets.length} 个 Set，${getAllGroups().length} 个 Group，${getAllClips().length} 个 Clip，${profiles.length} 个 Profile`;
  const packageNode = `
    <button type="button" class="${treeButtonClass("package", "", "", "")}" data-select-kind="package">
      <span>${escapeHtml(state.animation?.displayName || state.animation?.packageId || "Animation Package")}</span>
      <small>${profiles.length} profiles</small>
    </button>`;
  if (sets.length === 0) {
    el.animationTree.innerHTML = packageNode + emptyBlock("还没有 AnimationSet。");
    return;
  }

  el.animationTree.innerHTML = packageNode + sets.map(set => {
    const groups = set.groups || [];
    return `
      <section class="tree-set">
        <button type="button" class="${treeButtonClass("set", set.setId, "", "")}" data-select-kind="set" data-set-id="${escapeHtml(set.setId || "")}">
          <span>${escapeHtml(set.displayName || set.setId || "set")}</span>
          <small>${groups.length} groups</small>
        </button>
        <div class="tree-children">
          ${groups.map(group => `
            <button type="button" class="${treeButtonClass("group", set.setId, group.groupId, "")}" data-select-kind="group" data-set-id="${escapeHtml(set.setId || "")}" data-group-id="${escapeHtml(group.groupId || "")}">
              <span>${escapeHtml(group.displayName || group.groupId || "group")}</span>
              <small>${(group.clips || []).length} clips</small>
            </button>
            <div class="tree-children clips">
              ${(group.clips || []).map(clip => `
                <button type="button" class="${treeButtonClass("clip", set.setId, group.groupId, clip.clipId)}" data-select-kind="clip" data-set-id="${escapeHtml(set.setId || "")}" data-group-id="${escapeHtml(group.groupId || "")}" data-clip-id="${escapeHtml(clip.clipId || "")}">
                  <span>${escapeHtml(clip.displayName || clip.clipId || "clip")}</span>
                  <small>${escapeHtml(clip.rootMotionPolicy || "Ignore")}</small>
                </button>`).join("")}
            </div>`).join("")}
        </div>
      </section>`;
  }).join("");
}

function renderWorkspace() {
  const set = findSet(state.selected.setId);
  const group = findGroup(state.selected.setId, state.selected.groupId);
  if (state.selected.kind === "package") {
    el.workspaceTitle.textContent = state.animation?.displayName || state.animation?.packageId || "Animation Package";
    el.workspaceSubtitle.textContent = "编辑动画 Profile、Slot 和包级运行时意图";
    el.mappingWorkspace.innerHTML = renderPackageRuntimeSections();
    return;
  }

  if (!set) {
    el.workspaceTitle.textContent = "Clip Mapping";
    el.workspaceSubtitle.textContent = "新增 Set 后开始配置动画组";
    el.mappingWorkspace.innerHTML = `${emptyBlock("还没有可编辑的 AnimationSet。")}${renderPackageRuntimeSections()}`;
    return;
  }

  if (!group) {
    el.workspaceTitle.textContent = set.displayName || set.setId || "AnimationSet";
    el.workspaceSubtitle.textContent = "选择或新增 Group 后编辑 Clip mapping";
    el.mappingWorkspace.innerHTML = `${emptyBlock("当前 Set 还没有 Group。")}${renderSetRuntimeSections(set)}${renderPackageRuntimeSections()}`;
    return;
  }

  const clips = group.clips || [];
  el.workspaceTitle.textContent = group.displayName || group.groupId || "AnimationGroup";
  ensureBlendSelection(group);
  el.workspaceSubtitle.textContent = `${group.usage || "custom"} / ${clips.length} clips / ${(group.blend1D || []).length + (group.blend2D || []).length} blends`;
  el.mappingWorkspace.innerHTML = `
    <section class="workspace-section">
      <div class="section-heading">
        <div>
          <h3>Clip Mapping</h3>
          <p>Blend point 只能引用当前 Group 内的本地 clipId。</p>
        </div>
        <button type="button" data-remove-group="1">删除 Group</button>
      </div>
      ${renderClipMappingTable(clips)}
    </section>
    ${renderBlendEditor(group)}
    ${renderTimelineEditor(group)}
    ${renderPreviewBakeCompatibilityWorkflow(set, group)}
    ${renderSetRuntimeSections(set)}
    ${renderPackageRuntimeSections()}`;
  requestAnimationFrame(renderCompilerPreviewViewport);
}

function renderClipMappingTable(clips) {
  if (clips.length === 0) return emptyBlock("当前 Group 还没有 Clip。先新增 Clip，再配置 Blend point 的本地 clipId。");
  return `
    <div class="clip-table" role="table" aria-label="Clip mapping table">
      <div class="clip-row clip-head" role="row">
        <span>Clip ID</span>
        <span>源资源</span>
        <span>Sub Clip</span>
        <span>Loop</span>
        <span>Speed</span>
        <span>RootMotionPolicy</span>
        <span>操作</span>
      </div>
      ${clips.map(clip => `
        <div class="clip-row ${state.selected.clipId === clip.clipId ? "active" : ""}" role="row">
          <button type="button" data-select-clip="${escapeHtml(clip.clipId || "")}">${escapeHtml(clip.clipId || "clip")}</button>
          <span title="${escapeHtml(getSelectionTitle(clip.sourceSelection))}">${escapeHtml(getSelectionTitle(clip.sourceSelection) || "未选择")}</span>
          <span>${escapeHtml(clip.sourceSubClipId || clip.sourceClipName || "-")}</span>
          <span>${clip.loop ? "是" : "否"}</span>
          <span>${escapeHtml(formatNumber(clip.speed ?? 1))}</span>
          <span>${escapeHtml(clip.rootMotionPolicy || "Ignore")}</span>
          <span class="row-actions">
            <button type="button" data-pick-clip="${escapeHtml(clip.clipId || "")}">选择源</button>
            <button type="button" data-remove-clip="${escapeHtml(clip.clipId || "")}">移除</button>
          </span>
        </div>`).join("")}
    </div>`;
}

function renderBlendEditor(group) {
  const kind = state.blendEditor.view === "2D" ? "2D" : "1D";
  const blends = getBlendList(group, kind);
  const selectedBlend = getSelectedBlend(group);
  const localClipIds = (group.clips || []).map(clip => clip.clipId).filter(Boolean);
  const diagnostics = selectedBlend ? getBlendDiagnostics(group, selectedBlend, kind) : [];

  return `
    <section class="workspace-section blend-editor" aria-label="Visual 1D and 2D Blend editor">
      <div class="section-heading blend-heading">
        <div>
          <h3>Blend Editor</h3>
          <p>${kind === "1D" ? "1D line" : "2D plane"} / local clipId references / cursor preview</p>
        </div>
        <div class="segmented-control" role="group" aria-label="Blend view">
          <button type="button" class="${kind === "1D" ? "active" : ""}" data-blend-view="1D">1D line</button>
          <button type="button" class="${kind === "2D" ? "active" : ""}" data-blend-view="2D">2D plane</button>
        </div>
      </div>

      <div class="blend-toolbar">
        <label class="blend-select-field">
          <span>Group blend definition</span>
          <select data-blend-select="${kind}" ${blends.length ? "" : "disabled"}>
            ${blends.length
              ? blends.map(blend => `<option value="${escapeHtml(blend.blendId || "")}"${blend === selectedBlend ? " selected" : ""}>${escapeHtml(blend.displayName || blend.blendId || "blend")}</option>`).join("")
              : `<option value="">未创建 ${kind} blend</option>`}
          </select>
        </label>
        <button type="button" data-add-blend="${kind}">新增 ${kind} Blend</button>
        ${selectedBlend ? `<button type="button" data-remove-blend="${kind}">删除 Blend</button>` : ""}
      </div>

      ${selectedBlend ? renderBlendDefinition(group, selectedBlend, kind, localClipIds, diagnostics) : emptyBlock(`当前 Group 还没有 ${kind} blend 定义。`)}
    </section>`;
}

function renderBlendDefinition(group, blend, kind, localClipIds, diagnostics) {
  const defaultClipMissing = blend.defaultClipId && !localClipIds.includes(blend.defaultClipId);
  return `
    <div class="blend-definition">
      <div class="blend-fields">
        ${blendField("blendId", "Blend ID", blend.blendId || "")}
        ${blendField("displayName", "显示名", blend.displayName || "")}
        ${kind === "1D"
          ? blendField("parameter", "Parameter", blend.parameter || "")
          : `${blendField("xParameter", "X Parameter", blend.xParameter || "")}${blendField("yParameter", "Y Parameter", blend.yParameter || "")}`}
        <label class="blend-field ${defaultClipMissing ? "invalid" : ""}">
          <span>Default Clip</span>
          ${clipSelect("defaultClipId", blend.defaultClipId || "", localClipIds, false)}
        </label>
      </div>
      <div class="blend-preview-state">
        <span>参数 cursor</span>
        <strong>${escapeHtml(getBlendCursorText(blend, kind))}</strong>
        <span>Default clip</span>
        <code>${escapeHtml(blend.defaultClipId || "未设置")}</code>
      </div>
      ${kind === "1D" ? renderBlend1DTrack(blend, localClipIds) : renderBlend2DPlane(blend, localClipIds)}
      ${renderBlendPoints(group, blend, kind, localClipIds)}
      ${renderBlendDiagnostics(diagnostics)}
    </div>`;
}

function renderBlend1DTrack(blend, localClipIds) {
  const points = blend.points || [];
  const values = points.map(point => Number(point.value || 0));
  const min = values.length ? Math.min(...values) : 0;
  const max = values.length ? Math.max(...values) : 1;
  const range = max - min || 1;
  return `
    <div class="blend-track" aria-label="1D line blend preview">
      <div class="track-axis">
        ${points.map((point, index) => {
          const left = clamp(((Number(point.value || 0) - min) / range) * 100, 0, 100);
          const missing = point.clipId && !localClipIds.includes(point.clipId);
          return `<span class="blend-point-marker ${missing ? "invalid" : ""}" style="left:${left}%;" title="${escapeHtml(point.clipId || "missing clipId")} / value ${escapeHtml(formatNumber(point.value || 0))}">${index + 1}</span>`;
        }).join("")}
      </div>
      <div class="axis-labels"><span>${escapeHtml(formatNumber(min))}</span><span>${escapeHtml(blend.parameter || "parameter")}</span><span>${escapeHtml(formatNumber(max))}</span></div>
    </div>`;
}

function renderBlend2DPlane(blend, localClipIds) {
  const points = blend.points || [];
  const xs = points.map(point => Number(point.x || 0));
  const ys = points.map(point => Number(point.y || 0));
  const minX = xs.length ? Math.min(...xs) : -1;
  const maxX = xs.length ? Math.max(...xs) : 1;
  const minY = ys.length ? Math.min(...ys) : -1;
  const maxY = ys.length ? Math.max(...ys) : 1;
  const rangeX = maxX - minX || 1;
  const rangeY = maxY - minY || 1;
  return `
    <div class="blend-plane" aria-label="2D plane blend preview">
      <div class="plane-axis x">${escapeHtml(blend.xParameter || "x")}</div>
      <div class="plane-axis y">${escapeHtml(blend.yParameter || "y")}</div>
      ${points.map((point, index) => {
        const left = clamp(((Number(point.x || 0) - minX) / rangeX) * 100, 0, 100);
        const bottom = clamp(((Number(point.y || 0) - minY) / rangeY) * 100, 0, 100);
        const missing = point.clipId && !localClipIds.includes(point.clipId);
        return `<span class="blend-plane-point ${missing ? "invalid" : ""}" style="left:${left}%; bottom:${bottom}%;" title="${escapeHtml(point.clipId || "missing clipId")} / (${escapeHtml(formatNumber(point.x || 0))}, ${escapeHtml(formatNumber(point.y || 0))})">${index + 1}</span>`;
      }).join("")}
    </div>`;
}

function renderBlendPoints(group, blend, kind, localClipIds) {
  const points = blend.points || [];
  return `
    <div class="blend-points ${kind === "2D" ? "two-axis" : "one-axis"}">
      <div class="blend-point-row blend-point-head">
        <span>clipId</span>
        ${kind === "1D" ? "<span>threshold / value</span>" : "<span>X</span><span>Y</span>"}
        <span>weight</span>
        <span>操作</span>
      </div>
      ${points.map((point, index) => {
        const missing = point.clipId && !localClipIds.includes(point.clipId);
        return `
          <div class="blend-point-row ${missing ? "invalid" : ""}">
            ${clipSelect("pointClipId", point.clipId || "", localClipIds, true, index)}
            ${kind === "1D"
              ? pointNumberField("value", point.value ?? 0, index, -1000, 1000, 0.01)
              : `${pointNumberField("x", point.x ?? 0, index, -1000, 1000, 0.01)}${pointNumberField("y", point.y ?? 0, index, -1000, 1000, 0.01)}`}
            ${pointNumberField("weight", point.weight ?? 1, index, 0, 10, 0.01)}
            <span class="row-actions">
              <button type="button" data-remove-blend-point="${kind}" data-point-index="${index}">移除</button>
            </span>
          </div>`;
      }).join("")}
      <button type="button" class="add-point-button" data-add-blend-point="${kind}">添加 Blend Point</button>
    </div>`;
}

function renderBlendDiagnostics(diagnostics) {
  if (diagnostics.length === 0) return `<div class="blend-diagnostics ok">Blend diagnostics: 当前定义可用于预览。</div>`;
  return `
    <div class="blend-diagnostics">
      <strong>Blend diagnostics</strong>
      ${diagnostics.map(item => `<p class="${escapeHtml(item.tone)}">${escapeHtml(item.message)}</p>`).join("")}
    </div>`;
}

function renderTimelineEditor(group) {
  ensureTimelineSelection(group);
  const timelines = getTimelineList(group);
  const timeline = getSelectedTimeline(group);
  return `
    <section class="workspace-section timeline-editor" aria-label="Timeline event editor">
      <div class="section-heading timeline-heading">
        <div>
          <h3>Timeline Events</h3>
          <p>按 Seconds / Normalized / PresentationFrame / CombatFrame 轨道编辑表现和战斗事件。</p>
        </div>
        <div class="timeline-actions">
          <button type="button" data-add-timeline="1">新增 Timeline</button>
          ${timeline ? `<button type="button" data-remove-timeline="1">删除 Timeline</button>` : ""}
          ${timeline ? `<button type="button" data-copy-timeline-context="1">复制 Event JSON</button>` : ""}
        </div>
      </div>

      <div class="timeline-toolbar">
        <label class="timeline-select-field">
          <span>Group timeline</span>
          <select data-timeline-select ${timelines.length ? "" : "disabled"}>
            ${timelines.length
              ? timelines.map(item => `<option value="${escapeHtml(item.timelineId || "")}"${item === timeline ? " selected" : ""}>${escapeHtml(item.displayName || item.timelineId || "timeline")}</option>`).join("")
              : `<option value="">未创建 timeline</option>`}
          </select>
        </label>
        ${timeline ? `<button type="button" data-add-timeline-event="1">添加 Event</button>` : ""}
      </div>

      ${timeline ? renderTimelineDefinition(group, timeline) : emptyBlock("当前 Group 还没有 Timeline。")}
    </section>`;
}

function renderTimelineDefinition(group, timeline) {
  const localClipIds = getLocalClipIds(group);
  const diagnostics = getTimelineDiagnostics(group, timeline);
  return `
    <div class="timeline-definition">
      <div class="timeline-fields">
        ${timelineField("timelineId", "Timeline ID", timeline.timelineId || "")}
        ${timelineField("displayName", "显示名", timeline.displayName || "")}
        <label class="timeline-field ${timeline.clipId && !localClipIds.includes(timeline.clipId) ? "invalid" : ""}">
          <span>Clip ID</span>
          ${timelineClipSelect("clipId", timeline.clipId || "", localClipIds)}
        </label>
        <label class="timeline-field">
          <span>默认 TimeDomain</span>
          ${timeDomainSelect("timeDomain", timeline.timeDomain || "Seconds", false)}
        </label>
        ${timelineMetadataNumberField("durationSeconds", "Clip Duration Seconds", getTimelineMetadata(timeline, "durationSeconds") || "", 0, 600, 0.01)}
        ${timelineMetadataNumberField("presentationFrameCount", "Presentation Frames", getTimelineMetadata(timeline, "presentationFrameCount") || "", 0, 100000, 1)}
        ${timelineMetadataNumberField("combatFrameCount", "Combat Frames", getTimelineMetadata(timeline, "combatFrameCount") || "", 0, 100000, 1)}
      </div>
      ${renderTimelineScrubber(group, timeline)}
      ${renderTimelineEventRows(group, timeline)}
      ${renderTimelineDiagnostics(diagnostics)}
    </div>`;
}

function renderTimelineScrubber(group, timeline) {
  const events = timeline.events || [];
  return `
    <div class="timeline-scrubber" aria-label="Timeline scrubber rows">
      ${TIME_DOMAIN_OPTIONS.map(domain => {
        const domainEvents = events
          .map((eventItem, index) => ({ eventItem, index }))
          .filter(({ eventItem }) => (eventItem.timeDomain || timeline.timeDomain || "Seconds") === domain);
        return `
          <div class="timeline-domain-row">
            <span class="domain-label">${escapeHtml(domain)}</span>
            <div class="domain-track">
              ${domainEvents.map(({ eventItem, index }) => {
                const left = clamp(getTimelineEventPercent(timeline, eventItem, domain), 0, 100);
                return `<button type="button" class="timeline-marker ${getTimelineEventTone(eventItem)}" style="left:${left}%;" data-event-index="${index}" title="${escapeHtml(eventItem.eventId || "event")} / ${escapeHtml(formatNumber(eventItem.time || 0))}">${index + 1}</button>`;
              }).join("")}
            </div>
          </div>`;
      }).join("")}
    </div>`;
}

function renderTimelineEventRows(group, timeline) {
  const events = timeline.events || [];
  const localClipIds = getLocalClipIds(group);
  if (events.length === 0) return emptyBlock("当前 Timeline 还没有 Event。");
  return `
    <div class="timeline-event-list">
      <div class="timeline-event-row timeline-event-head">
        <span>Event ID</span>
        <span>clipId</span>
        <span>Domain</span>
        <span>Time</span>
        <span>Kind</span>
        <span>ResourceSelection</span>
        <span>Payload JSON</span>
        <span>操作</span>
      </div>
      ${events.map((eventItem, index) => {
        const clipMissing = eventItem.clipId && !localClipIds.includes(eventItem.clipId);
        return `
          <div class="timeline-event-row ${clipMissing ? "invalid" : ""}">
            <input data-event-field="eventId" data-event-index="${index}" value="${escapeHtml(eventItem.eventId || "")}">
            ${timelineClipSelect("clipId", eventItem.clipId || "", localClipIds, true, index)}
            ${timeDomainSelect("timeDomain", eventItem.timeDomain || timeline.timeDomain || "Seconds", true, index)}
            <input type="number" data-type="number" data-event-field="time" data-event-index="${index}" min="-100000" max="100000" step="0.01" value="${escapeHtml(String(eventItem.time ?? 0))}">
            ${eventKindSelect(eventItem.eventKind || "custom", index)}
            <span class="event-resource-cell">
              <code>${escapeHtml(getSelectionTitle(eventItem.resourceSelection) || getTimelineAudioSelectionTitle(eventItem.resourceSelection) || "未选择")}</code>
              <span class="event-resource-actions">
                <button type="button" data-pick-event-resource="vfx" data-event-index="${index}">VFX</button>
                <button type="button" data-pick-event-resource="audioCue" data-event-index="${index}">AudioCue</button>
              </span>
            </span>
            <textarea data-event-field="payloadJson" data-event-index="${index}" spellcheck="false">${escapeHtml(eventItem.payloadJson || "")}</textarea>
            <span class="row-actions">
              <button type="button" data-remove-timeline-event="1" data-event-index="${index}">移除</button>
            </span>
          </div>`;
      }).join("")}
    </div>`;
}

function renderTimelineDiagnostics(diagnostics) {
  if (diagnostics.length === 0) return `<div class="timeline-diagnostics ok">Timeline diagnostics: 当前事件列表可用于后续编译。</div>`;
  return `
    <div class="timeline-diagnostics">
      <strong>Timeline diagnostics</strong>
      ${diagnostics.map(item => `<p class="${escapeHtml(item.tone)}">${escapeHtml(item.message)}</p>`).join("")}
    </div>`;
}

function renderPreviewBakeCompatibilityWorkflow(set, group) {
  const clip = findSelectedClip() || (group.clips || [])[0] || null;
  const blend = getSelectedBlend(group);
  const timeline = getSelectedTimeline(group);
  const target = getPreviewTargetSummary(state.previewWorkflow.targetType, set, group, clip);
  const bake = getBakeArtifactSummary(set, group, clip);
  const compatibility = getCompatibilityReport(set, group, clip, bake);

  return `
    <section class="workspace-section preview-bake-compatibility" aria-label="Preview Bake Compatibility workflow">
      <div class="section-heading preview-heading">
        <div>
          <h3>Preview / Bake / Compatibility</h3>
          <p>Preview 是编辑期辅助，不是 runtime authority，不写 Unity scene/prefab。</p>
        </div>
        <label class="preview-target-select">
          <span>Preview Target</span>
          <select data-preview-target-type>
            ${PREVIEW_TARGET_OPTIONS.map(option => `<option value="${escapeHtml(option.value)}"${option.value === state.previewWorkflow.targetType ? " selected" : ""}>${escapeHtml(option.label)}</option>`).join("")}
          </select>
        </label>
      </div>

      <div class="preview-workflow-grid">
        <article class="preview-card">
          <div class="preview-card-heading">
            <h4>Preview Target</h4>
            <span>${escapeHtml(target.label)}</span>
          </div>
          <dl class="preview-kv">
            <dt>resource reference</dt>
            <dd><code>${escapeHtml(target.reference || "未设置")}</code></dd>
            <dt>scope</dt>
            <dd>${escapeHtml(target.scope || "-")}</dd>
            <dt>ResourceFieldSpec</dt>
            <dd>${escapeHtml(target.fieldSpec || "复用已有 SourceSelection / compatibility profile 入口")}</dd>
          </dl>
        </article>

        <article class="preview-card">
          <div class="preview-card-heading">
            <h4>Reference Path</h4>
            <span>clip / blend / timeline</span>
          </div>
          <dl class="preview-kv">
            <dt>set/group/clip</dt>
            <dd><code>${escapeHtml(`${set.setId || "set"}/${group.groupId || "group"}/${clip?.clipId || "clip"}`)}</code></dd>
            <dt>blend/timeline</dt>
            <dd><code>${escapeHtml(`${state.blendEditor.view}:${blend?.blendId || "none"} / ${timeline?.timelineId || "none"}`)}</code></dd>
            <dt>rootMotionPolicy</dt>
            <dd>${escapeHtml(clip?.rootMotionPolicy || "Ignore")}</dd>
            <dt>source selection</dt>
            <dd><code>${escapeHtml(getSelectionTitle(clip?.sourceSelection) || "未选择")}</code></dd>
          </dl>
        </article>
      </div>

      ${renderBakeArtifactSummary(bake)}
      ${renderCompatibilityReport(compatibility)}
      ${renderCompilerBackedPreviewPanel(set, group, clip)}
    </section>`;
}

function renderCompilerBackedPreviewPanel(set, group, clip) {
  const preview = state.preview3d;
  const result = preview.result;
  const resources = getPreviewResources(result);
  const clips = getPreviewAnimationClips(result);
  const selectedClip = getSelectedPreviewClip();
  const selectedResource = selectedClip?.resource || getPreviewResourceForClip(clip, resources);
  const diagnostics = getPreviewDiagnostics(result);
  const glbResources = resources.filter(resource => isPreviewModelResource(resource));
  const missingResources = resources.filter(resource => resource.exists === false);
  const endpointLabel = API.preview;

  return `
    <article id="compilerPreviewPanel" class="workflow-report compiler-preview-panel" aria-label="编译器驱动 3D 预览">
      <div class="preview-card-heading">
        <h4>编译器驱动 3D 预览</h4>
        <span>${escapeHtml(preview.loading ? "编译中" : result ? "编译器结果" : "等待预览")}</span>
      </div>
      <div class="compiler-preview-layout">
        <div id="compilerPreviewViewport" class="compiler-preview-viewport" data-preview-state="${escapeHtml(preview.threeStatus || "idle")}">
          ${renderPreviewViewportFallback(selectedResource, preview)}
        </div>
        <div class="compiler-preview-sidebar">
          <div class="preview-control-row">
            <button id="runCompilerPreviewButton" type="button" data-run-compiler-preview="1">${preview.loading ? "正在编译..." : "运行编译预览"}</button>
            <button id="previewPlaybackToggle" type="button" data-preview-play-toggle="1" ${selectedClip ? "" : "disabled"}>${preview.playing ? "暂停" : "播放"}</button>
          </div>
          <dl class="preview-kv compact compiler-preview-kv">
            <dt>端点</dt>
            <dd><code>${escapeHtml(endpointLabel)}</code></dd>
            <dt>请求载荷</dt>
            <dd><code>{ package, animation }</code></dd>
            <dt>Set/Group/Clip</dt>
            <dd><code>${escapeHtml(`${set?.setId || "set"}/${group?.groupId || "group"}/${clip?.clipId || "clip"}`)}</code></dd>
            <dt>模型资源</dt>
            <dd>${escapeHtml(`${glbResources.length}/${resources.length}`)}</dd>
            <dt>缺失资源</dt>
            <dd>${escapeHtml(String(missingResources.length))}</dd>
          </dl>
          ${renderPreviewClipList(clips)}
        </div>
      </div>
      ${renderPreviewResourceStatus(resources, selectedResource)}
      ${renderWorkflowDiagnostics("预览诊断", diagnostics)}
    </article>`;
}

function renderPreviewViewportFallback(resource, preview) {
  if (preview.loading) {
    return `<div class="preview-viewport-fallback"><strong>正在请求编译器预览...</strong><span>端点会返回 compileResult、resource plan、clip registry 和 previewResources。</span></div>`;
  }
  if (preview.error) {
    return `<div class="preview-viewport-fallback error"><strong>预览不可用</strong><span>${escapeHtml(preview.error)}</span></div>`;
  }
  if (!preview.result) {
    return `<div class="preview-viewport-fallback"><strong>尚未运行编译预览</strong><span>点击“运行编译预览”，从编译器结果读取 GLB/model 资源和 Clip Registry。</span></div>`;
  }
  if (!resource) {
    return `<div class="preview-viewport-fallback warning"><strong>没有可显示的模型资源</strong><span>编译器返回了预览数据，但当前 Clip 没有关联 GLB/GLTF 资源。</span></div>`;
  }
  return `<div class="preview-viewport-fallback"><strong>正在初始化 Three.js 视口...</strong><span>${escapeHtml(resource.resourceKey || resource.stableId || resource.url || "model")}</span></div>`;
}

function renderPreviewClipList(clips) {
  if (!clips.length) return emptyBlock("编译器还没有返回 animationClipRegistry。");
  return `
      <div id="previewClipList" class="preview-clip-list" aria-label="编译后的动画 Clip">
      ${clips.map(clip => {
        const clipId = getPreviewClipId(clip);
        const resource = clip.resource || {};
        const active = clipId === state.preview3d.selectedClipId;
        return `
          <button type="button" class="preview-clip-row ${active ? "active" : ""}" data-preview-clip-id="${escapeHtml(clipId)}">
            <span>${escapeHtml(clip.displayName || clip.clipId || "clip")}</span>
            <code>${escapeHtml(resource.resourceKey || clip.runtimeResourceKey || "未绑定资源")}</code>
          </button>`;
      }).join("")}
    </div>`;
}

function renderPreviewResourceStatus(resources, selectedResource) {
  if (!resources.length) {
    return `<div id="previewResourceStatus" class="preview-resource-status empty-row">previewResources.resources 为空；请先检查编译输出和 resource_catalog.json。</div>`;
  }
  return `
    <div id="previewResourceStatus" class="preview-resource-status" role="table" aria-label="预览资源状态">
      <div class="preview-resource-row preview-resource-head"><span>resourceKey</span><span>kind/usage</span><span>路径</span><span>状态</span></div>
      ${resources.map(resource => {
        const selected = selectedResource && resource.resourceKey === selectedResource.resourceKey;
        return `
          <div class="preview-resource-row ${selected ? "active" : ""} ${resource.exists === false ? "missing" : ""}">
            <code>${escapeHtml(resource.resourceKey || resource.stableId || "-")}</code>
            <span>${escapeHtml(`${resource.kind || "-"}/${resource.usage || "-"}`)}</span>
            <code title="${escapeHtml(resource.projectRelativePath || resource.relativePath || resource.url || "")}">${escapeHtml(resource.projectRelativePath || resource.relativePath || resource.url || "-")}</code>
            <span>${escapeHtml(resource.exists === false ? "缺失" : isPreviewModelResource(resource) ? "GLB 可用" : "元数据")}</span>
          </div>`;
      }).join("")}
    </div>`;
}

function renderBakeArtifactSummary(summary) {
  const artifactRows = summary.artifacts.length
    ? summary.artifacts.map(artifact => `
      <div class="artifact-row">
        <span>${escapeHtml(artifact.source)}</span>
        <code>${escapeHtml(artifact.reference || "未设置")}</code>
        <span>${escapeHtml(artifact.expectedHash || "-")}</span>
        <span class="${escapeHtml(artifact.tone)}">${escapeHtml(artifact.status)}</span>
      </div>`).join("")
    : `<div class="artifact-row empty-row"><span>generatedArtifactSelections</span><code>未声明 Animation.BakeArtifact</code><span>-</span><span class="warning">pending</span></div>`;

  return `
    <article class="workflow-report bake-report">
      <div class="preview-card-heading">
        <h4>Bake Artifact Summary</h4>
        <span>${escapeHtml(summary.status)}</span>
      </div>
      <dl class="preview-kv compact">
        <dt>runtimeResourceKey</dt>
        <dd><code>${escapeHtml(summary.runtimeResourceKey || "未生成")}</code></dd>
        <dt>source hash</dt>
        <dd><code>${escapeHtml(summary.sourceHash || "unknown")}</code></dd>
        <dt>artifact hash</dt>
        <dd><code>${escapeHtml(summary.artifactHash || "unknown")}</code></dd>
      </dl>
      <div class="artifact-table" role="table" aria-label="Bake artifact summary">
        <div class="artifact-row artifact-head"><span>selection source</span><span>resource</span><span>expectedHash</span><span>hash/stale</span></div>
        ${artifactRows}
      </div>
      ${renderWorkflowDiagnostics("Bake diagnostics", summary.diagnostics)}
    </article>`;
}

function renderCompatibilityReport(report) {
  return `
    <article class="workflow-report compatibility-report">
      <div class="preview-card-heading">
        <h4>Skeleton / Avatar / Clip Compatibility</h4>
        <span>${escapeHtml(report.status)}</span>
      </div>
      <div class="compatibility-grid">
        <dl class="preview-kv compact">
          <dt>skeletonProfileId</dt>
          <dd><code>${escapeHtml(report.skeletonProfileId || "缺失")}</code></dd>
          <dt>avatarProfileId</dt>
          <dd><code>${escapeHtml(report.avatarProfileId || "缺失")}</code></dd>
          <dt>profile resource</dt>
          <dd><code>${escapeHtml(report.profileSelection || "未选择")}</code></dd>
        </dl>
        <dl class="preview-kv compact">
          <dt>missing bone</dt>
          <dd>${renderInlineList(report.missingBones)}</dd>
          <dt>missing socket</dt>
          <dd>${renderInlineList(report.missingSockets)}</dd>
          <dt>avatar path</dt>
          <dd>${renderInlineList(report.missingAvatarPaths)}</dd>
        </dl>
      </div>
      ${renderWorkflowDiagnostics("Compatibility diagnostics", report.diagnostics)}
    </article>`;
}

function renderWorkflowDiagnostics(title, diagnostics) {
  if (!diagnostics.length) {
    return `<div class="workflow-diagnostics ok"><strong>${escapeHtml(title)}</strong><p>当前 DTO 未报告阻塞问题。</p></div>`;
  }
  return `
    <div class="workflow-diagnostics">
      <strong>${escapeHtml(title)}</strong>
      ${diagnostics.map(item => `<p class="${escapeHtml(item.tone)}"><code>${escapeHtml(item.code || "INFO")}</code> ${escapeHtml(item.message || "")}</p>`).join("")}
    </div>`;
}

function renderInlineList(items) {
  return items.length
    ? items.map(item => `<code>${escapeHtml(item)}</code>`).join(" ")
    : `<span class="muted">未报告</span>`;
}

function getPreviewTargetSummary(targetType, set, group, clip) {
  const animation = state.animation || {};
  if (targetType === "characterPackage") {
    return {
      label: "Character Package",
      reference: state.packageRelative || animation.packageId || "",
      scope: `${animation.packageId || "animation package"} / ${set?.setId || "set"}`,
      fieldSpec: "package context"
    };
  }
  if (targetType === "modelResource") {
    return {
      label: "Model Resource",
      reference: getSelectionTitle(clip?.sourceSelection) || clip?.runtimeResourceKey || "",
      scope: `${group?.groupId || "group"} / ${clip?.clipId || "clip"}`,
      fieldSpec: "Animation.SourceClip"
    };
  }
  return {
    label: "Skeleton",
    reference: firstNonEmpty(animation.skeletonProfileId, set?.compatibility?.skeletonProfileId),
    scope: `${animation.packageId || "animation package"} / compatibility`,
    fieldSpec: "Animation.CompatibilityProfile"
  };
}

function getBakeArtifactSummary(set, group, clip) {
  const artifactSelections = [
    ...selectionEntries("clip.generatedArtifactSelections", clip?.generatedArtifactSelections),
    ...selectionEntries("set.warmup.generatedArtifactSelections", set?.warmup?.generatedArtifactSelections),
    ...selectionEntries("set.warmup.additionalResourceSelections", set?.warmup?.additionalResourceSelections)
  ];
  const sourceHash = firstNonEmpty(
    clip?.sourceSelection?.expectedHash,
    clip?.metadata?.sourceHash,
    clip?.metadata?.sourceClipHash,
    getSelectionTitle(clip?.sourceSelection)
  );
  const artifactHash = firstNonEmpty(
    clip?.metadata?.artifactHash,
    clip?.metadata?.bakeArtifactHash,
    clip?.metadata?.generatedConfigHash,
    artifactSelections.find(entry => entry.selection?.expectedHash)?.selection?.expectedHash
  );
  const diagnostics = [];
  const artifacts = artifactSelections.map(entry => {
    const expectedHash = entry.selection?.expectedHash || "";
    const stale = Boolean(expectedHash && sourceHash && expectedHash !== sourceHash);
    if (stale) {
      diagnostics.push({
        tone: "warning",
        code: "ANIM_BAKE_ARTIFACT_STALE",
        message: `${entry.source} expectedHash 与 source clip/profile/skeleton context 不一致。`
      });
    }
    return {
      source: entry.source,
      reference: getSelectionTitle(entry.selection),
      expectedHash,
      tone: stale ? "warning" : "ok",
      status: stale ? "stale" : expectedHash ? "hash tracked" : "no hash"
    };
  });

  if (!clip) {
    diagnostics.push({ tone: "warning", code: "ANIM_BAKE_CLIP_MISSING", message: "未选择 Clip，无法显示 bake artifact 上下文。" });
  }
  if (clip && !getSelectionTitle(clip.sourceSelection)) {
    diagnostics.push({ tone: "warning", code: "ANIM_BAKE_SOURCE_CLIP_MISSING", message: "当前 Clip 尚未选择源动画资源。" });
  }
  if (artifacts.length === 0) {
    diagnostics.push({ tone: "warning", code: "ANIM_BAKE_ARTIFACT_MISSING", message: "没有 generatedArtifactSelections；Bake 产物尚未声明或尚未生成。" });
  }

  return {
    runtimeResourceKey: clip?.runtimeResourceKey || "",
    sourceHash,
    artifactHash,
    artifacts,
    status: diagnostics.some(item => item.tone === "warning" || item.tone === "error") ? "needs review" : "clean",
    diagnostics
  };
}

function getCompatibilityReport(set, group, clip, bake) {
  const animation = state.animation || {};
  const compatibility = set?.compatibility || {};
  const diagnostics = [];
  const skeletonProfileId = firstNonEmpty(compatibility.skeletonProfileId, animation.skeletonProfileId);
  const avatarProfileId = firstNonEmpty(compatibility.avatarProfileId, animation.avatarProfileId);
  const profileSelection = firstNonEmpty(
    getSelectionTitle(compatibility.compatibilityProfileSelection),
    getSelectionTitle(compatibility.avatarMaskSelection)
  );
  const requiredBones = Array.isArray(compatibility.requiredBoneIds) ? compatibility.requiredBoneIds : [];
  const requiredSockets = Array.isArray(compatibility.requiredSocketIds) ? compatibility.requiredSocketIds : [];
  const missingBones = collectDiagnosticValues([animation, set, group, clip], ["bone", "requiredBone", "missingBone"]);
  const missingSockets = collectDiagnosticValues([animation, set, group, clip], ["socket", "requiredSocket", "missingSocket"]);
  const missingAvatarPaths = collectDiagnosticValues([animation, set, group, clip], ["avatar", "avatarPath", "missingAvatar"]);

  if (!skeletonProfileId) {
    diagnostics.push({ tone: "error", code: "ANIM_COMPAT_SKELETON_PROFILE_MISSING", message: "缺少 skeletonProfileId，骨骼路径缺失无法在运行前暴露。" });
  }
  if (!avatarProfileId) {
    diagnostics.push({ tone: "warning", code: "ANIM_COMPAT_AVATAR_PROFILE_MISSING", message: "缺少 avatarProfileId；Avatar/retargeting 路径需要补充。" });
  }
  if (!requiredBones.length) {
    diagnostics.push({ tone: "warning", code: "ANIM_COMPAT_REQUIRED_BONES_EMPTY", message: "未声明 requiredBoneIds，missing bone/socket 检查只能依赖外部诊断。" });
  }
  if (!requiredSockets.length) {
    diagnostics.push({ tone: "warning", code: "ANIM_COMPAT_REQUIRED_SOCKETS_EMPTY", message: "未声明 requiredSocketIds，挂点缺失不能在编辑期完全暴露。" });
  }

  const rootPolicies = new Set((set?.layers || []).map(layer => layer.rootMotionPolicy).filter(Boolean));
  const clipPolicy = clip?.rootMotionPolicy || "Ignore";
  if (rootPolicies.size > 0 && !rootPolicies.has(clipPolicy)) {
    diagnostics.push({
      tone: "warning",
      code: "ANIM_COMPAT_ROOT_MOTION_POLICY_MISMATCH",
      message: `Clip RootMotionPolicy ${clipPolicy} 与 Set layer policy ${Array.from(rootPolicies).join(", ")} 不一致。`
    });
  }
  diagnostics.push(...(bake?.diagnostics || []));

  return {
    skeletonProfileId,
    avatarProfileId,
    profileSelection,
    missingBones: missingBones.length ? missingBones : requiredBones.length ? [] : ["requiredBoneIds not declared"],
    missingSockets: missingSockets.length ? missingSockets : requiredSockets.length ? [] : ["requiredSocketIds not declared"],
    missingAvatarPaths: missingAvatarPaths.length ? missingAvatarPaths : avatarProfileId ? [] : ["avatarProfileId not declared"],
    status: diagnostics.some(item => item.tone === "error") ? "blocked" : diagnostics.length ? "needs review" : "clean",
    diagnostics
  };
}

function selectionEntries(source, selections) {
  return Array.isArray(selections)
    ? selections.map(selection => ({ source, selection }))
    : [];
}

function collectDiagnosticValues(sources, needles) {
  const values = new Set();
  for (const source of sources) {
    for (const diagnostic of source?.diagnostics || []) {
      const code = String(diagnostic.code || "").toLowerCase();
      const field = String(diagnostic.field || "").toLowerCase();
      const message = String(diagnostic.message || "").toLowerCase();
      if (!needles.some(needle => code.includes(needle.toLowerCase()) || field.includes(needle.toLowerCase()) || message.includes(needle.toLowerCase()))) {
        continue;
      }
      values.add(firstNonEmpty(diagnostic.field, diagnostic.sourceObjectPath, diagnostic.message, diagnostic.code));
    }
  }
  return Array.from(values);
}

function firstNonEmpty(...values) {
  return values.find(value => value != null && String(value).trim() !== "") || "";
}

function renderInspector() {
  const target = getSelectedTarget();
  if (!target.value) {
    el.inspectorSubtitle.textContent = "无可编辑对象";
    el.inspectorContent.innerHTML = emptyBlock("选择树中的 Set、Group 或 Clip。");
    return;
  }

  el.inspectorSubtitle.textContent = `${target.kind} / ${target.label}`;
  if (target.kind === "set") {
    el.inspectorContent.innerHTML = renderSetInspector(target.value);
  } else if (target.kind === "group") {
    el.inspectorContent.innerHTML = renderGroupInspector(target.value);
  } else if (target.kind === "clip") {
    el.inspectorContent.innerHTML = renderClipInspector(target.value);
  } else {
    el.inspectorContent.innerHTML = renderPackageInspector(state.animation);
  }
}

function renderPackageInspector(animation) {
  return `
    ${textField("packageId", "Package ID", animation.packageId || "")}
    ${textField("stableId", "Stable ID", animation.stableId || "")}
    ${textField("displayName", "显示名", animation.displayName || "")}
    ${textField("skeletonProfileId", "Skeleton Profile", animation.skeletonProfileId || "")}
    ${textField("avatarProfileId", "Avatar Profile", animation.avatarProfileId || "")}`;
}

function renderSetInspector(set) {
  return `
    ${textField("setId", "Set ID", set.setId || "")}
    ${textField("displayName", "显示名", set.displayName || "")}
    ${textField("version", "版本", set.version || "1.0")}
    ${textField("defaultClipId", "默认 Clip", set.defaultClipId || "")}
    ${textField("fallbackClipId", "Fallback Clip", set.fallbackClipId || "")}`;
}

function renderGroupInspector(group) {
  return `
    ${textField("groupId", "Group ID", group.groupId || "")}
    ${textField("displayName", "显示名", group.displayName || "")}
    ${textField("usage", "Usage", group.usage || "")}
    ${textArea("description", "说明", group.description || "")}`;
}

function renderClipInspector(clip) {
  return `
    ${textField("clipId", "Clip ID", clip.clipId || "")}
    ${textField("displayName", "显示名", clip.displayName || "")}
    <label class="inspector-field">
      <span>SourceSelection</span>
      <div class="selection-line">
        <code>${escapeHtml(getSelectionTitle(clip.sourceSelection) || "未选择")}</code>
        <button type="button" data-open-source-picker="1">选择源动画</button>
      </div>
    </label>
    ${textField("sourceSubClipId", "SourceSubClipId", clip.sourceSubClipId || "")}
    ${textField("sourceClipName", "SourceClipName", clip.sourceClipName || "")}
    ${textField("runtimeResourceKey", "RuntimeResourceKey", clip.runtimeResourceKey || "")}
    <label class="inspector-field">
      <span>Loop</span>
      <select data-field="loop" data-type="boolean">
        <option value="false"${clip.loop ? "" : " selected"}>否</option>
        <option value="true"${clip.loop ? " selected" : ""}>是</option>
      </select>
    </label>
    ${numberField("speed", "Speed", clip.speed ?? 1, 0.01, 4, 0.01)}
    <label class="inspector-field">
      <span>RootMotionPolicy</span>
      <select data-field="rootMotionPolicy">
        ${ROOT_MOTION_OPTIONS.map(option => `<option value="${option}"${(clip.rootMotionPolicy || "Ignore") === option ? " selected" : ""}>${option}</option>`).join("")}
      </select>
    </label>
    ${textField("tagsText", "Tags", (clip.tags || []).join(", "))}
    ${renderSelectionListEditor("clip", "generatedArtifactSelections", clip.generatedArtifactSelections || [], "bakeArtifact", "Generated Artifact Selections")}
    ${textField("metadataText", "Metadata", metadataToText(clip.metadata), "key=value, key2=value2")}`;
}

function renderPackageRuntimeSections() {
  return `
    <section class="workspace-section structure-editor package-profile-editor" aria-label="Animation profiles and slots">
      <div class="section-heading">
        <div>
          <h3>Profiles / Slots</h3>
          <p>Profile 定义运行时默认 Set、默认 Group 和各动画槽位；角色编辑器只引用这些结果。</p>
        </div>
        <button type="button" data-add-profile="1">新增 Profile</button>
      </div>
      ${renderProfileEditor()}
    </section>`;
}

function renderSetRuntimeSections(set) {
  return `
    <section class="workspace-section structure-editor set-runtime-editor" aria-label="Set layers action bindings compatibility warmup">
      <div class="section-heading">
        <div>
          <h3>Set Runtime Structure</h3>
          <p>补齐运行时需要的 Layer、ActionBinding、Compatibility 和 Warmup，不再靠手写 JSON。</p>
        </div>
        <button type="button" data-remove-set="1">删除 Set</button>
      </div>
      ${renderLayerEditor(set)}
      ${renderActionBindingEditor(set)}
      ${renderCompatibilityWarmupEditor("set", set)}
    </section>`;
}

function renderProfileEditor() {
  const profiles = state.animation?.profiles || [];
  if (profiles.length === 0) {
    return emptyBlock("还没有 Animation Profile。新增后配置 Slot 与 Set/Group/Clip/Blend 的引用。");
  }

  return profiles.map((profile, profileIndex) => {
    const setIds = getSetIds();
    const groupIds = getGroupIds(profile.defaultSetId || setIds[0] || "");
    return `
      <article class="structure-block profile-block">
        <div class="structure-block-head">
          <div>
            <strong>${escapeHtml(profile.displayName || profile.profileId || "Animation Profile")}</strong>
            <span>${escapeHtml(profile.description || "profile slots and runtime defaults")}</span>
          </div>
          <button type="button" data-remove-profile="${profileIndex}">删除 Profile</button>
        </div>
        <div class="structure-grid profile-fields">
          ${structureTextField("profile", profileIndex, "profileId", "Profile ID", profile.profileId || "")}
          ${structureTextField("profile", profileIndex, "displayName", "显示名", profile.displayName || "")}
          ${structureTextField("profile", profileIndex, "description", "说明", profile.description || "")}
          ${structureSelectField("profile", profileIndex, "defaultSetId", "Default Set", profile.defaultSetId || "", setIds)}
          ${structureSelectField("profile", profileIndex, "defaultGroupId", "Default Group", profile.defaultGroupId || "", groupIds)}
        </div>
        ${renderProfileSlots(profile, profileIndex)}
        ${renderCompatibilityWarmupEditor("profile", profile, profileIndex)}
      </article>`;
  }).join("");
}

function renderProfileSlots(profile, profileIndex) {
  const slots = profile.slots || [];
  return `
    <div class="subsection-heading">
      <strong>Profile Slots</strong>
      <button type="button" data-add-profile-slot="${profileIndex}">新增 Slot</button>
    </div>
    ${slots.length ? `
      <div class="runtime-table profile-slot-table">
        <div class="runtime-row runtime-head">
          <span>Slot ID</span><span>显示名</span><span>Set</span><span>Group</span><span>Default Clip</span><span>Default Blend</span><span>Preload</span><span>Required</span><span>操作</span>
        </div>
        ${slots.map((slot, slotIndex) => {
          const setId = slot.setId || profile.defaultSetId || getSetIds()[0] || "";
          const groupId = slot.groupId || profile.defaultGroupId || getGroupIds(setId)[0] || "";
          return `
            <div class="runtime-row profile-slot-row">
              ${structureInlineInput("profileSlot", slotIndex, "slotId", slot.slotId || "", { profileIndex })}
              ${structureInlineInput("profileSlot", slotIndex, "displayName", slot.displayName || "", { profileIndex })}
              ${structureInlineSelect("profileSlot", slotIndex, "setId", setId, getSetIds(), { profileIndex })}
              ${structureInlineSelect("profileSlot", slotIndex, "groupId", groupId, getGroupIds(setId), { profileIndex })}
              ${structureInlineSelect("profileSlot", slotIndex, "defaultClipId", slot.defaultClipId || "", getClipIds(setId, groupId), { profileIndex })}
              ${structureInlineSelect("profileSlot", slotIndex, "defaultBlendId", slot.defaultBlendId || "", getBlendIds(setId, groupId), { profileIndex })}
              ${structureInlineSelect("profileSlot", slotIndex, "preloadPolicy", slot.preloadPolicy || "AnimationWarmup", PRELOAD_POLICY_OPTIONS, { profileIndex })}
              ${structureInlineBool("profileSlot", slotIndex, "required", Boolean(slot.required), { profileIndex })}
              <button type="button" data-remove-profile-slot="${slotIndex}" data-profile-index="${profileIndex}">移除</button>
            </div>`;
        }).join("")}
      </div>` : emptyBlock("当前 Profile 没有 Slot。")}`;
}

function renderLayerEditor(set) {
  const layers = set.layers || [];
  return `
    <article class="structure-block">
      <div class="structure-block-head">
        <div>
          <strong>Layers</strong>
          <span>Layer 定义 AvatarMask、RootMotionPolicy、权重和同步关系。</span>
        </div>
        <button type="button" data-add-layer="1">新增 Layer</button>
      </div>
      ${layers.length ? `
        <div class="runtime-table layer-table">
          <div class="runtime-row runtime-head">
            <span>Layer ID</span><span>显示名</span><span>用途</span><span>Weight</span><span>Additive</span><span>Sync Layer</span><span>RootMotionPolicy</span><span>AvatarMask</span><span>Tags</span><span>操作</span>
          </div>
          ${layers.map((layer, layerIndex) => `
            <div class="runtime-row layer-row">
              ${structureInlineInput("layer", layerIndex, "layerId", layer.layerId || "")}
              ${structureInlineInput("layer", layerIndex, "displayName", layer.displayName || "")}
              ${structureInlineInput("layer", layerIndex, "purpose", layer.purpose || "")}
              ${structureInlineNumber("layer", layerIndex, "weight", layer.weight ?? 1, 0, 1, 0.01)}
              ${structureInlineBool("layer", layerIndex, "additive", Boolean(layer.additive))}
              ${structureInlineSelect("layer", layerIndex, "syncLayerId", layer.syncLayerId || "", ["", ...layers.map(item => item.layerId).filter(id => id && id !== layer.layerId)])}
              ${structureInlineSelect("layer", layerIndex, "rootMotionPolicy", layer.rootMotionPolicy || "Ignore", ROOT_MOTION_OPTIONS)}
              ${renderSelectionCell("layer", "avatarMaskSelection", layer.avatarMaskSelection, "avatarMask", "选择 AvatarMask", { layerIndex })}
              ${structureInlineInput("layer", layerIndex, "tagsText", (layer.tags || []).join(", "))}
              <button type="button" data-remove-layer="${layerIndex}">移除</button>
            </div>`).join("")}
        </div>` : emptyBlock("当前 Set 没有 Layer。")}`;
}

function renderActionBindingEditor(set) {
  const bindings = set.actionBindings || [];
  return `
    <article class="structure-block">
      <div class="structure-block-head">
        <div>
          <strong>Action Bindings</strong>
          <span>ActionBinding 把移动、攻击、防御等动作映射到 Group / Clip / Blend / Timeline。</span>
        </div>
        <button type="button" data-add-action-binding="1">新增 Binding</button>
      </div>
      ${bindings.length ? `
        <div class="runtime-table action-binding-table">
          <div class="runtime-row runtime-head">
            <span>Binding ID</span><span>Action ID</span><span>显示名</span><span>Group</span><span>Clip</span><span>Blend</span><span>Timeline</span><span>Required</span><span>Tags</span><span>操作</span>
          </div>
          ${bindings.map((binding, bindingIndex) => {
            const groupId = binding.groupId || getGroupIds(set.setId)[0] || "";
            return `
              <div class="runtime-row action-binding-row">
                ${structureInlineInput("actionBinding", bindingIndex, "bindingId", binding.bindingId || "")}
                ${structureInlineInput("actionBinding", bindingIndex, "actionId", binding.actionId || "")}
                ${structureInlineInput("actionBinding", bindingIndex, "displayName", binding.displayName || "")}
                ${structureInlineSelect("actionBinding", bindingIndex, "groupId", groupId, getGroupIds(set.setId))}
                ${structureInlineSelect("actionBinding", bindingIndex, "clipId", binding.clipId || "", getClipIds(set.setId, groupId))}
                ${structureInlineSelect("actionBinding", bindingIndex, "blendId", binding.blendId || "", getBlendIds(set.setId, groupId))}
                ${structureInlineSelect("actionBinding", bindingIndex, "timelineId", binding.timelineId || "", getTimelineIds(set.setId, groupId))}
                ${structureInlineBool("actionBinding", bindingIndex, "required", Boolean(binding.required))}
                ${structureInlineInput("actionBinding", bindingIndex, "tagsText", (binding.tags || []).join(", "))}
                <button type="button" data-remove-action-binding="${bindingIndex}">移除</button>
              </div>`;
          }).join("")}
        </div>` : emptyBlock("当前 Set 没有 ActionBinding。")}`;
}

function renderCompatibilityWarmupEditor(scope, owner, profileIndex) {
  const compatibility = ensureCompatibility(owner);
  const warmup = ensureWarmup(owner);
  const ownerKind = scope === "profile" ? "profile" : "set";
  const context = scope === "profile" ? { profileIndex } : {};
  const clipIds = scope === "profile" ? getProfileClipIds(owner) : getSetClipIds(owner);
  const blendIds = scope === "profile" ? getProfileBlendIds(owner) : getSetBlendIds(owner);
  return `
    <div class="runtime-dual-grid">
      <article class="structure-block compact-block">
        <div class="structure-block-head">
          <div>
            <strong>Compatibility</strong>
            <span>骨架、Avatar、坐标系、必要骨骼和挂点。</span>
          </div>
        </div>
        <div class="structure-grid compatibility-fields">
          ${structureTextField(`${ownerKind}Compatibility`, 0, "compatibilityId", "Compatibility ID", compatibility.compatibilityId || "", context)}
          ${structureTextField(`${ownerKind}Compatibility`, 0, "skeletonProfileId", "Skeleton Profile", compatibility.skeletonProfileId || "", context)}
          ${structureTextField(`${ownerKind}Compatibility`, 0, "avatarProfileId", "Avatar Profile", compatibility.avatarProfileId || "", context)}
          ${structureSelectField(`${ownerKind}Compatibility`, 0, "coordinateConvention", "Coordinate Convention", compatibility.coordinateConvention || "", COORDINATE_CONVENTION_OPTIONS, context)}
          ${structureBoolField(`${ownerKind}Compatibility`, 0, "allowRetargeting", "Allow Retargeting", Boolean(compatibility.allowRetargeting), context)}
          ${structureTextField(`${ownerKind}Compatibility`, 0, "requiredBoneIdsText", "Required Bones", (compatibility.requiredBoneIds || []).join(", "), context)}
          ${structureTextField(`${ownerKind}Compatibility`, 0, "requiredSocketIdsText", "Required Sockets", (compatibility.requiredSocketIds || []).join(", "), context)}
        </div>
        <div class="selection-grid">
          ${renderSelectionField(`${ownerKind}Compatibility`, "compatibilityProfileSelection", compatibility.compatibilityProfileSelection, "compatibilityProfile", "Compatibility Profile", context)}
          ${renderSelectionField(`${ownerKind}Compatibility`, "avatarMaskSelection", compatibility.avatarMaskSelection, "avatarMask", "Avatar Mask", context)}
        </div>
      </article>
      <article class="structure-block compact-block">
        <div class="structure-block-head">
          <div>
            <strong>Warmup</strong>
            <span>声明运行前预热的 Clip、Blend、VFX、AudioCue 和生成产物。</span>
          </div>
        </div>
        <div class="structure-grid warmup-fields">
          ${structureTextField(`${ownerKind}Warmup`, 0, "warmupId", "Warmup ID", warmup.warmupId || "", context)}
          ${structureSelectField(`${ownerKind}Warmup`, 0, "preloadPolicy", "Preload Policy", warmup.preloadPolicy || "AnimationWarmup", PRELOAD_POLICY_OPTIONS, context)}
          ${structureBoolField(`${ownerKind}Warmup`, 0, "includeDefaultClip", "Include Default Clip", warmup.includeDefaultClip !== false, context)}
          ${structureBoolField(`${ownerKind}Warmup`, 0, "includeFallbackClip", "Include Fallback Clip", warmup.includeFallbackClip !== false, context)}
          ${structureBoolField(`${ownerKind}Warmup`, 0, "includeActionBindings", "Include Action Bindings", warmup.includeActionBindings !== false, context)}
          ${structureBoolField(`${ownerKind}Warmup`, 0, "includeBlendPoints", "Include Blend Points", warmup.includeBlendPoints !== false, context)}
          ${structureMultiSelectField(`${ownerKind}Warmup`, 0, "requiredClipIds", "Required Clips", warmup.requiredClipIds || [], clipIds, context)}
          ${structureMultiSelectField(`${ownerKind}Warmup`, 0, "requiredBlendIds", "Required Blends", warmup.requiredBlendIds || [], blendIds, context)}
        </div>
        ${renderSelectionListEditor(`${ownerKind}Warmup`, "avatarMaskSelections", warmup.avatarMaskSelections || [], "avatarMask", "Warmup Avatar Masks", context)}
        ${renderSelectionListEditor(`${ownerKind}Warmup`, "vfxSelections", warmup.vfxSelections || [], "eventVfx", "Warmup VFX", context)}
        ${renderSelectionListEditor(`${ownerKind}Warmup`, "audioCueSelections", warmup.audioCueSelections || [], "eventAudioCue", "Warmup AudioCue", context)}
        ${renderSelectionListEditor(`${ownerKind}Warmup`, "generatedArtifactSelections", warmup.generatedArtifactSelections || [], "bakeArtifact", "Warmup Generated Artifacts", context)}
        ${renderSelectionListEditor(`${ownerKind}Warmup`, "additionalResourceSelections", warmup.additionalResourceSelections || [], "additionalResource", "Additional Resources", context)}
      </article>
    </div>`;
}

function structureTextField(owner, index, field, label, value, context = {}) {
  return `
    <label class="structure-field">
      <span>${escapeHtml(label)}</span>
      ${structureInlineInput(owner, index, field, value, context)}
    </label>`;
}

function structureSelectField(owner, index, field, label, value, options, context = {}) {
  return `
    <label class="structure-field">
      <span>${escapeHtml(label)}</span>
      ${structureInlineSelect(owner, index, field, value, options, context)}
    </label>`;
}

function structureBoolField(owner, index, field, label, value, context = {}) {
  return `
    <label class="structure-field">
      <span>${escapeHtml(label)}</span>
      ${structureInlineBool(owner, index, field, value, context)}
    </label>`;
}

function structureMultiSelectField(owner, index, field, label, values, options, context = {}) {
  const selected = new Set(values || []);
  return `
    <label class="structure-field multi-select-field">
      <span>${escapeHtml(label)}</span>
      <select multiple data-structure-owner="${escapeHtml(owner)}" data-structure-index="${index}" data-structure-field="${escapeHtml(field)}" ${dataContextAttributes(context)}>
        ${(options || []).map(option => `<option value="${escapeHtml(option)}"${selected.has(option) ? " selected" : ""}>${escapeHtml(option)}</option>`).join("")}
      </select>
    </label>`;
}

function structureInlineInput(owner, index, field, value, context = {}) {
  return `<input data-structure-owner="${escapeHtml(owner)}" data-structure-index="${index}" data-structure-field="${escapeHtml(field)}" ${dataContextAttributes(context)} value="${escapeHtml(value)}">`;
}

function structureInlineNumber(owner, index, field, value, min, max, step, context = {}) {
  return `<input type="number" data-type="number" data-structure-owner="${escapeHtml(owner)}" data-structure-index="${index}" data-structure-field="${escapeHtml(field)}" ${dataContextAttributes(context)} min="${min}" max="${max}" step="${step}" value="${escapeHtml(String(value))}">`;
}

function structureInlineSelect(owner, index, field, value, options, context = {}) {
  const normalizedOptions = ["", ...(options || []).filter(option => option !== "")];
  if (value && !normalizedOptions.includes(value)) normalizedOptions.push(value);
  return `
    <select data-structure-owner="${escapeHtml(owner)}" data-structure-index="${index}" data-structure-field="${escapeHtml(field)}" ${dataContextAttributes(context)}>
      ${normalizedOptions.map(option => `<option value="${escapeHtml(option)}"${option === value ? " selected" : ""}>${escapeHtml(option || "未设置")}</option>`).join("")}
    </select>`;
}

function structureInlineBool(owner, index, field, value, context = {}) {
  return `
    <select data-structure-owner="${escapeHtml(owner)}" data-structure-index="${index}" data-structure-field="${escapeHtml(field)}" data-type="boolean" ${dataContextAttributes(context)}>
      <option value="false"${value ? "" : " selected"}>否</option>
      <option value="true"${value ? " selected" : ""}>是</option>
    </select>`;
}

function renderSelectionField(ownerKind, fieldName, selection, specKey, title, context = {}) {
  return `
    <div class="selection-card">
      <span>${escapeHtml(title)}</span>
      <code title="${escapeHtml(getSelectionTitle(selection))}">${escapeHtml(getSelectionTitle(selection) || "未选择")}</code>
      <div class="selection-actions">
        <button type="button" data-pick-selection="1" data-selection-owner="${escapeHtml(ownerKind)}" data-selection-field="${escapeHtml(fieldName)}" data-selection-spec="${escapeHtml(specKey)}" data-selection-title="${escapeHtml(title)}" ${dataContextAttributes(context)}>选择</button>
        <button type="button" data-clear-selection="1" data-selection-owner="${escapeHtml(ownerKind)}" data-selection-field="${escapeHtml(fieldName)}" ${dataContextAttributes(context)}>清空</button>
      </div>
    </div>`;
}

function renderSelectionCell(ownerKind, fieldName, selection, specKey, title, context = {}) {
  return `
    <span class="selection-cell">
      <code title="${escapeHtml(getSelectionTitle(selection))}">${escapeHtml(getSelectionTitle(selection) || "未选择")}</code>
      <button type="button" data-pick-selection="1" data-selection-owner="${escapeHtml(ownerKind)}" data-selection-field="${escapeHtml(fieldName)}" data-selection-spec="${escapeHtml(specKey)}" data-selection-title="${escapeHtml(title)}" ${dataContextAttributes(context)}>选择</button>
    </span>`;
}

function renderSelectionListEditor(ownerKind, fieldName, selections, specKey, title, context = {}) {
  const rows = (selections || []).map((selection, index) => `
    <div class="selection-list-row">
      <code title="${escapeHtml(getSelectionTitle(selection))}">${escapeHtml(getSelectionTitle(selection) || "未设置")}</code>
      <button type="button" data-remove-selection-list-item="1" data-selection-owner="${escapeHtml(ownerKind)}" data-selection-field="${escapeHtml(fieldName)}" data-selection-index="${index}" ${dataContextAttributes(context)}>移除</button>
    </div>`).join("");
  return `
    <div class="selection-list-editor">
      <div class="selection-list-head">
        <span>${escapeHtml(title)}</span>
        <button type="button" data-add-selection-list-item="1" data-selection-owner="${escapeHtml(ownerKind)}" data-selection-field="${escapeHtml(fieldName)}" data-selection-spec="${escapeHtml(specKey)}" data-selection-title="${escapeHtml(title)}" ${dataContextAttributes(context)}>添加</button>
      </div>
      ${rows || `<div class="selection-list-row empty-row"><code>未设置</code><span></span></div>`}
    </div>`;
}

function dataContextAttributes(context = {}) {
  return Object.entries(context)
    .filter(([, value]) => value !== undefined && value !== null && value !== "")
    .map(([key, value]) => `data-${escapeHtml(kebabCase(key))}="${escapeHtml(String(value))}"`)
    .join(" ");
}

function renderDiagnostics() {
  const issues = getIssues();
  const errorCount = issues.filter(issue => issue.severity === "Error").length;
  const warningCount = issues.filter(issue => issue.severity === "Warning").length;
  el.diagnosticsSummary.textContent = issues.length
    ? `${issues.length} 条诊断，${errorCount} 错误，${warningCount} 警告`
    : "暂无诊断。";

  if (issues.length === 0) {
    el.diagnosticsList.innerHTML = emptyBlock("当前动画包没有诊断。");
    return;
  }

  el.diagnosticsList.innerHTML = issues.map(issue => `
    <article class="diagnostic-row ${String(issue.severity || "").toLowerCase()}">
      <strong>${escapeHtml(issue.severity || "Info")}</strong>
      <code>${escapeHtml(issue.code || "-")}</code>
      <span>${escapeHtml(issue.sourceObjectPath || issue.sourcePath || "")}</span>
      <p>${escapeHtml(issue.message || "")}</p>
    </article>`).join("");
}

function handleInspectorInput(event) {
  const field = event.target.dataset.field;
  if (!field) return;
  const target = getSelectedTarget();
  if (!target.value) return;

  let value = event.target.value;
  if (event.target.dataset.type === "boolean") value = value === "true";
  if (event.target.dataset.type === "number") value = Number(value);

  if (field === "tagsText") {
    target.value.tags = String(value).split(",").map(item => item.trim()).filter(Boolean);
  } else if (field === "metadataText") {
    target.value.metadata = textToMetadata(value);
  } else {
    target.value[field] = value;
  }

  if (field === "setId") state.selected.setId = value;
  if (field === "groupId") state.selected.groupId = value;
  if (field === "clipId") state.selected.clipId = value;
  renderSetSelect();
  renderTree();
  renderWorkspace();
}

function handleStructureButtonClick(button) {
  const set = findSet(state.selected.setId) || firstSet();
  if (button.dataset.addProfile !== undefined) {
    addProfile();
    return true;
  }
  if (button.dataset.removeSet !== undefined) {
    removeSet(state.selected.setId);
    return true;
  }
  if (button.dataset.removeGroup !== undefined) {
    removeGroup(state.selected.setId, state.selected.groupId);
    return true;
  }
  if (button.dataset.removeProfile !== undefined) {
    removeProfile(Number(button.dataset.removeProfile));
    return true;
  }
  if (button.dataset.addProfileSlot !== undefined) {
    addProfileSlot(Number(button.dataset.addProfileSlot));
    return true;
  }
  if (button.dataset.removeProfileSlot !== undefined) {
    removeProfileSlot(Number(button.dataset.profileIndex), Number(button.dataset.removeProfileSlot));
    return true;
  }
  if (button.dataset.addLayer !== undefined) {
    addLayer(set);
    return true;
  }
  if (button.dataset.removeLayer !== undefined) {
    removeLayer(set, Number(button.dataset.removeLayer));
    return true;
  }
  if (button.dataset.addActionBinding !== undefined) {
    addActionBinding(set);
    return true;
  }
  if (button.dataset.removeActionBinding !== undefined) {
    removeActionBinding(set, Number(button.dataset.removeActionBinding));
    return true;
  }
  return false;
}

function handleSelectionButtonClick(button) {
  if (button.dataset.pickSelection !== undefined) {
    const owner = resolveSelectionOwner(button.dataset);
    if (!owner) return true;
    openSelectionPicker(owner, button.dataset.selectionField, button.dataset.selectionSpec, button.dataset.selectionTitle || "选择资源");
    return true;
  }
  if (button.dataset.clearSelection !== undefined) {
    const owner = resolveSelectionOwner(button.dataset);
    if (owner) owner[button.dataset.selectionField] = {};
    render();
    return true;
  }
  if (button.dataset.addSelectionListItem !== undefined) {
    const owner = resolveSelectionOwner(button.dataset);
    if (!owner) return true;
    openSelectionListPicker(owner, button.dataset.selectionField, button.dataset.selectionSpec, button.dataset.selectionTitle || "添加资源");
    return true;
  }
  if (button.dataset.removeSelectionListItem !== undefined) {
    const owner = resolveSelectionOwner(button.dataset);
    const field = button.dataset.selectionField;
    const index = Number(button.dataset.selectionIndex);
    if (owner && Array.isArray(owner[field])) owner[field].splice(index, 1);
    render();
    return true;
  }
  return false;
}

function handleStructureEditorInput(event) {
  const ownerKind = event.target.dataset.structureOwner;
  const field = event.target.dataset.structureField;
  if (!ownerKind || !field) return;
  const target = resolveStructureTarget(event.target.dataset);
  if (!target) return;

  let value = event.target.value;
  if (event.target.multiple) {
    value = Array.from(event.target.selectedOptions).map(option => option.value).filter(Boolean);
  } else if (event.target.dataset.type === "boolean") {
    value = value === "true";
  } else if (event.target.dataset.type === "number") {
    value = Number(value);
  }

  if (field === "tagsText") {
    target.tags = csvToList(value);
  } else if (field === "requiredBoneIdsText") {
    target.requiredBoneIds = csvToList(value);
  } else if (field === "requiredSocketIdsText") {
    target.requiredSocketIds = csvToList(value);
  } else {
    target[field] = value;
  }

  if (event.type === "change") {
    normalizeStructureAfterEdit(ownerKind, target, field);
    render();
  } else {
    renderStatus();
  }
}

async function openSourceClipPicker(clip) {
  if (!clip) return;
  const fieldSpec = state.fieldSpecs?.sourceClip || FALLBACK_SOURCE_CLIP_SPEC;
  state.resourcePicker = {
    open: true,
    clip,
    target: { kind: "sourceClip", clip },
    title: "选择源动画 Clip",
    fieldSpec,
    rows: [],
    search: "",
    onlySelectable: true,
    loading: true,
    error: ""
  };
  renderResourcePicker();

  const result = await postJson(API.pick, {
    package: state.packageRelative,
    fieldSpec,
    context: {
      consumerKind: "AnimationEditor",
      consumerStableId: state.animation?.stableId || "",
      scopeId: state.animation?.stableId || state.packageRelative,
      packageId: state.animation?.packageId || "",
      packagePath: state.packageRelative
    }
  }, "查询 Animation.SourceClip");

  state.resourcePicker.loading = false;
  state.resourcePicker.rows = Array.isArray(result?.items) ? result.items : [];
  renderResourcePicker();
}

async function openEventResourcePicker(timeline, eventItem, eventKind) {
  if (!timeline || !eventItem) return;
  const normalizedKind = normalizeEventKind(eventKind || eventItem.eventKind);
  const fieldSpec = getEventResourceFieldSpec(normalizedKind);
  if (!fieldSpec) return;

  state.resourcePicker = {
    open: true,
    clip: null,
    target: { kind: "timelineEvent", timeline, eventItem, eventKind: normalizedKind },
    title: normalizedKind === "AudioCue" ? "选择 Event AudioCue / FMOD" : "选择 Event VFX",
    fieldSpec,
    rows: [],
    search: "",
    onlySelectable: true,
    loading: true,
    error: ""
  };
  renderResourcePicker();

  const result = await postJson(API.pick, {
    package: state.packageRelative,
    fieldSpec,
    context: {
      consumerKind: "AnimationEditor",
      consumerStableId: state.animation?.stableId || "",
      scopeId: state.animation?.stableId || state.packageRelative,
      packageId: state.animation?.packageId || "",
      packagePath: state.packageRelative,
      timelineId: timeline.timelineId || "",
      eventId: eventItem.eventId || ""
    }
  }, `查询 ${fieldSpec.fieldKey || "Animation.EventResource"}`);

  state.resourcePicker.loading = false;
  state.resourcePicker.rows = Array.isArray(result?.items) ? result.items : [];
  renderResourcePicker();
}

async function openSelectionPicker(owner, fieldName, specKey, title) {
  const fieldSpec = getSelectionFieldSpec(specKey);
  state.resourcePicker = {
    open: true,
    clip: null,
    target: { kind: "selectionField", owner, fieldName },
    title,
    fieldSpec,
    rows: [],
    search: "",
    onlySelectable: true,
    loading: true,
    error: ""
  };
  renderResourcePicker();
  await loadResourcePickerRows(fieldSpec, title);
}

async function openSelectionListPicker(owner, fieldName, specKey, title) {
  const fieldSpec = getSelectionFieldSpec(specKey);
  state.resourcePicker = {
    open: true,
    clip: null,
    target: { kind: "selectionList", owner, fieldName },
    title,
    fieldSpec,
    rows: [],
    search: "",
    onlySelectable: true,
    loading: true,
    error: ""
  };
  renderResourcePicker();
  await loadResourcePickerRows(fieldSpec, title);
}

async function loadResourcePickerRows(fieldSpec, label) {
  const result = await postJson(API.pick, {
    package: state.packageRelative,
    fieldSpec,
    context: getResourcePickerContext()
  }, `查询 ${label || fieldSpec?.fieldKey || "资源"}`);
  state.resourcePicker.loading = false;
  state.resourcePicker.rows = Array.isArray(result?.items) ? result.items : [];
  renderResourcePicker();
}

function closeResourcePicker() {
  state.resourcePicker.open = false;
  state.resourcePicker.target = null;
  renderResourcePicker();
}

function renderResourcePicker() {
  el.resourcePickerOverlay.classList.toggle("hidden", !state.resourcePicker.open);
  el.resourcePickerSearch.value = state.resourcePicker.search;
  el.resourcePickerOnlySelectable.checked = state.resourcePicker.onlySelectable;
  if (!state.resourcePicker.open) return;
  el.resourcePickerTitle.textContent = state.resourcePicker.title || "选择资源";
  el.resourcePickerSubtitle.textContent = state.resourcePicker.fieldSpec
    ? `${state.resourcePicker.fieldSpec.fieldKey} / ${state.resourcePicker.fieldSpec.outputKind} / ${state.resourcePicker.fieldSpec.preloadPolicy}`
    : "ResourceFieldSpec";

  if (state.resourcePicker.loading) {
    el.resourcePickerList.innerHTML = emptyBlock("正在读取资源候选...");
    return;
  }

  const rows = getFilteredPickerRows();
  if (rows.length === 0) {
    el.resourcePickerList.innerHTML = emptyBlock("没有匹配当前字段的资源。");
    return;
  }

  el.resourcePickerList.innerHTML = rows.map(({ row, originalIndex }) => {
    const item = row.item || {};
    const reasons = Array.isArray(row.reasons) ? row.reasons : [];
    return `
      <button type="button" class="picker-row ${row.selectable ? "selectable" : "blocked"}" data-resource-index="${originalIndex}"${row.selectable ? "" : " disabled"}>
        <span class="picker-main">
          <strong>${escapeHtml(item.displayName || item.stableId || "resource")}</strong>
          <small>${escapeHtml(item.kind || "-")} / ${escapeHtml(item.usage || "-")} / ${escapeHtml(item.bindingKind || "-")}</small>
        </span>
        <span class="picker-meta">
          <code>${escapeHtml(item.stableId || "")}</code>
          <small>${escapeHtml(row.selectable ? "可选" : (reasons[0]?.code || "不可选"))}</small>
        </span>
      </button>`;
  }).join("");
}

async function chooseResourcePickerRow(index) {
  const row = state.resourcePicker.rows[index];
  if (!row?.selectable || !row.item || !state.resourcePicker.target) return;

  const fieldSpec = state.resourcePicker.fieldSpec || state.fieldSpecs?.sourceClip || FALLBACK_SOURCE_CLIP_SPEC;
  const result = await postJson(API.resolveSelection, {
    package: state.packageRelative,
    fieldSpec,
    context: {
      consumerKind: "AnimationEditor",
      consumerStableId: state.animation?.stableId || "",
      scopeId: state.animation?.stableId || state.packageRelative,
      packageId: state.animation?.packageId || "",
      packagePath: state.packageRelative
    },
    selection: {
      resourceStableId: row.item.stableId || "",
      sourceProviderId: row.item.sourceProviderId || "",
      bindingKind: row.item.bindingKind || "None"
    }
  }, `解析 ${fieldSpec.fieldKey || "ResourceSelection"}`);

  if (!result?.accepted) {
    state.lastMessage = "资源选择未通过校验";
    renderStatus();
    return;
  }

  if (state.resourcePicker.target.kind === "sourceClip") {
    const clip = state.resourcePicker.target.clip;
    clip.sourceSelection = result.selection || {};
    clip.sourceClipName ||= row.item.displayName || row.item.stableId || "";
    if (!clip.sourceSubClipId) {
      clip.sourceSubClipId = getProviderData(row.item, "unitySubAssetKey") || getProviderData(row.item, "sourceSubClipId") || "";
    }
    state.lastMessage = "已选择源动画资源";
  } else if (state.resourcePicker.target.kind === "timelineEvent") {
    state.resourcePicker.target.eventItem.resourceSelection = result.selection || {};
    state.resourcePicker.target.eventItem.eventKind = state.resourcePicker.target.eventKind;
    updateEventPayloadTemplate(state.resourcePicker.target.eventItem);
    state.lastMessage = state.resourcePicker.target.eventKind === "AudioCue"
      ? "已选择 Timeline AudioCue"
      : "已选择 Timeline VFX";
  } else if (state.resourcePicker.target.kind === "selectionField") {
    state.resourcePicker.target.owner[state.resourcePicker.target.fieldName] = result.selection || {};
    state.lastMessage = `已选择 ${fieldSpec.displayName || fieldSpec.fieldKey || "资源"}`;
  } else if (state.resourcePicker.target.kind === "selectionList") {
    const owner = state.resourcePicker.target.owner;
    const fieldName = state.resourcePicker.target.fieldName;
    owner[fieldName] = Array.isArray(owner[fieldName]) ? owner[fieldName] : [];
    owner[fieldName].push(result.selection || {});
    state.lastMessage = `已添加 ${fieldSpec.displayName || fieldSpec.fieldKey || "资源"}`;
  }
  closeResourcePicker();
  render();
}

function getFilteredPickerRows() {
  const needle = state.resourcePicker.search.trim().toLowerCase();
  return state.resourcePicker.rows
    .map((row, originalIndex) => ({ row, originalIndex }))
    .filter(({ row }) => !state.resourcePicker.onlySelectable || row.selectable)
    .filter(({ row }) => {
      if (!needle) return true;
      const item = row.item || {};
      return [
        item.displayName, item.stableId, item.resourceId, item.sourceProviderId,
        item.kind, item.usage, item.bindingKind, getSelectionTitle({ resourceStableId: item.stableId })
      ].some(value => String(value || "").toLowerCase().includes(needle));
    });
}

function normalizeEventKind(kind) {
  const value = String(kind || "").toLowerCase();
  if (value === "audio" || value === "audiocue" || value === "fmod") return "audioCue";
  if (value === "vfx" || value === "visualeffect") return "vfx";
  return kind || "custom";
}

function getEventResourceFieldSpec(eventKind) {
  if (eventKind === "audioCue") {
    return state.fieldSpecs?.eventAudioCue || state.fieldSpecs?.eventAudioDefinition || FALLBACK_EVENT_AUDIO_CUE_SPEC;
  }
  if (eventKind === "vfx") return state.fieldSpecs?.eventVfx || FALLBACK_EVENT_VFX_SPEC;
  return null;
}

function getSelectionFieldSpec(specKey) {
  if (specKey === "sourceClip") return state.fieldSpecs?.sourceClip || FALLBACK_SOURCE_CLIP_SPEC;
  if (specKey === "avatarMask") return state.fieldSpecs?.avatarMask || FALLBACK_AVATAR_MASK_SPEC;
  if (specKey === "bakeArtifact") return state.fieldSpecs?.bakeArtifact || FALLBACK_BAKE_ARTIFACT_SPEC;
  if (specKey === "compatibilityProfile") return state.fieldSpecs?.compatibilityProfile || FALLBACK_COMPATIBILITY_PROFILE_SPEC;
  if (specKey === "eventVfx") return state.fieldSpecs?.eventVfx || FALLBACK_EVENT_VFX_SPEC;
  if (specKey === "eventAudioCue") return state.fieldSpecs?.eventAudioCue || state.fieldSpecs?.eventAudioDefinition || FALLBACK_EVENT_AUDIO_CUE_SPEC;
  return FALLBACK_ADDITIONAL_RESOURCE_SPEC;
}

function getResourcePickerContext() {
  return {
    consumerKind: "AnimationEditor",
    consumerStableId: state.animation?.stableId || "",
    scopeId: state.animation?.stableId || state.packageRelative,
    packageId: state.animation?.packageId || "",
    packagePath: state.packageRelative,
    skeletonStableId: state.animation?.skeletonProfileId || "",
    avatarStableId: state.animation?.avatarProfileId || ""
  };
}

function resolveSelectionOwner(dataset) {
  const owner = dataset.selectionOwner;
  const profileIndex = Number(dataset.profileIndex);
  const layerIndex = Number(dataset.layerIndex);
  if (owner === "clip") return findSelectedClip();
  if (owner === "layer") return (findSet(state.selected.setId)?.layers || [])[layerIndex];
  if (owner === "setCompatibility") return ensureCompatibility(findSet(state.selected.setId));
  if (owner === "setWarmup") return ensureWarmup(findSet(state.selected.setId));
  if (owner === "profileCompatibility") return ensureCompatibility((state.animation?.profiles || [])[profileIndex]);
  if (owner === "profileWarmup") return ensureWarmup((state.animation?.profiles || [])[profileIndex]);
  return null;
}

function resolveStructureTarget(dataset) {
  const owner = dataset.structureOwner;
  const index = Number(dataset.structureIndex);
  const profileIndex = Number(dataset.profileIndex);
  const set = findSet(state.selected.setId) || firstSet();
  if (owner === "profile") return (state.animation?.profiles || [])[index];
  if (owner === "profileSlot") return ((state.animation?.profiles || [])[profileIndex]?.slots || [])[index];
  if (owner === "layer") return (set?.layers || [])[index];
  if (owner === "actionBinding") return (set?.actionBindings || [])[index];
  if (owner === "setCompatibility") return ensureCompatibility(set);
  if (owner === "setWarmup") return ensureWarmup(set);
  if (owner === "profileCompatibility") return ensureCompatibility((state.animation?.profiles || [])[profileIndex]);
  if (owner === "profileWarmup") return ensureWarmup((state.animation?.profiles || [])[profileIndex]);
  return null;
}

function normalizeStructureAfterEdit(ownerKind, target, field) {
  if (ownerKind === "profile" && field === "profileId") {
    target.profileId ||= uniqueId("profile.base", (state.animation?.profiles || []).map(profile => profile.profileId));
  }
  if (ownerKind === "profileSlot") {
    target.preloadPolicy ||= "AnimationWarmup";
    if (field === "setId") {
      target.groupId = getGroupIds(target.setId)[0] || "";
      target.defaultClipId = getClipIds(target.setId, target.groupId)[0] || "";
      target.defaultBlendId = getBlendIds(target.setId, target.groupId)[0] || "";
    }
    if (field === "groupId") {
      target.defaultClipId = getClipIds(target.setId, target.groupId)[0] || "";
      target.defaultBlendId = getBlendIds(target.setId, target.groupId)[0] || "";
    }
  }
  if (ownerKind === "actionBinding" && field === "groupId") {
    target.clipId = getClipIds(state.selected.setId, target.groupId)[0] || "";
    target.blendId = getBlendIds(state.selected.setId, target.groupId)[0] || "";
    target.timelineId = getTimelineIds(state.selected.setId, target.groupId)[0] || "";
  }
}

function addSet() {
  ensureAnimationShape();
  const ids = state.animation.sets.map(set => set.setId);
  const setId = uniqueId("set.base", ids);
  state.animation.sets.push({
    setId,
    displayName: "Base Set",
    version: "1.0",
    defaultClipId: "",
    fallbackClipId: "",
    layers: [],
    groups: [],
    actionBindings: [],
    compatibility: {},
    warmup: {}
  });
  selectNode("set", setId, "", "");
  render();
}

function addProfile() {
  ensureAnimationShape();
  const profileId = uniqueId("profile.base", (state.animation.profiles || []).map(profile => profile.profileId));
  const set = firstSet();
  const group = set ? firstGroup(set.setId) : null;
  state.animation.profiles.push({
    profileId,
    displayName: "Base Profile",
    description: "Default runtime animation profile.",
    defaultSetId: set?.setId || "",
    defaultGroupId: group?.groupId || "",
    slots: [],
    compatibility: {},
    warmup: {},
    diagnostics: [],
    metadata: {}
  });
  state.selected.kind = "package";
  render();
}

function removeSet(setId) {
  if (!Array.isArray(state.animation?.sets) || !setId) return;
  state.animation.sets = state.animation.sets.filter(set => set.setId !== setId);
  for (const profile of state.animation.profiles || []) {
    if (profile.defaultSetId === setId) {
      profile.defaultSetId = "";
      profile.defaultGroupId = "";
    }
    for (const slot of profile.slots || []) {
      if (slot.setId === setId) {
        slot.setId = "";
        slot.groupId = "";
        slot.defaultClipId = "";
        slot.defaultBlendId = "";
      }
    }
  }
  ensureSelection();
  render();
}

function removeProfile(index) {
  if (!Array.isArray(state.animation?.profiles)) return;
  state.animation.profiles.splice(index, 1);
  render();
}

function addProfileSlot(profileIndex) {
  const profile = (state.animation?.profiles || [])[profileIndex];
  if (!profile) return;
  profile.slots = Array.isArray(profile.slots) ? profile.slots : [];
  const setId = profile.defaultSetId || getSetIds()[0] || "";
  const groupId = profile.defaultGroupId || getGroupIds(setId)[0] || "";
  const slotId = uniqueId("slot.locomotion", profile.slots.map(slot => slot.slotId));
  profile.slots.push({
    slotId,
    displayName: "Locomotion",
    purpose: "Runtime animation slot.",
    setId,
    groupId,
    defaultClipId: getClipIds(setId, groupId)[0] || "",
    defaultBlendId: getBlendIds(setId, groupId)[0] || "",
    preloadPolicy: "AnimationWarmup",
    required: true
  });
  render();
}

function removeProfileSlot(profileIndex, slotIndex) {
  const profile = (state.animation?.profiles || [])[profileIndex];
  if (!profile || !Array.isArray(profile.slots)) return;
  profile.slots.splice(slotIndex, 1);
  render();
}

function addGroup() {
  const set = findSet(state.selected.setId) || firstSet();
  if (!set) return addSet();
  set.groups ||= [];
  const groupId = uniqueId("group.locomotion", set.groups.map(group => group.groupId));
  set.groups.push({
    groupId,
    displayName: "Locomotion",
    description: "",
    usage: "locomotion",
    clips: [],
    blend1D: [],
    blend2D: [],
    timelines: []
  });
  selectNode("group", set.setId, groupId, "");
  render();
}

function removeGroup(setId, groupId) {
  const set = findSet(setId);
  if (!set || !groupId) return;
  set.groups = (set.groups || []).filter(group => group.groupId !== groupId);
  for (const binding of set.actionBindings || []) {
    if (binding.groupId === groupId) {
      binding.groupId = "";
      binding.clipId = "";
      binding.blendId = "";
      binding.timelineId = "";
    }
  }
  for (const profile of state.animation.profiles || []) {
    if (profile.defaultSetId === setId && profile.defaultGroupId === groupId) profile.defaultGroupId = "";
    for (const slot of profile.slots || []) {
      if (slot.setId === setId && slot.groupId === groupId) {
        slot.groupId = "";
        slot.defaultClipId = "";
        slot.defaultBlendId = "";
      }
    }
  }
  ensureSelection();
  render();
}

function addLayer(set) {
  if (!set) return;
  set.layers = Array.isArray(set.layers) ? set.layers : [];
  const layerId = uniqueId("layer.base", set.layers.map(layer => layer.layerId));
  set.layers.push({
    layerId,
    displayName: "Base Layer",
    purpose: "fullBody",
    weight: 1,
    additive: false,
    syncLayerId: "",
    rootMotionPolicy: "Ignore",
    avatarMaskSelection: {},
    tags: [],
    metadata: {}
  });
  render();
}

function removeLayer(set, index) {
  if (!set || !Array.isArray(set.layers)) return;
  set.layers.splice(index, 1);
  render();
}

function addActionBinding(set) {
  if (!set) return;
  set.actionBindings = Array.isArray(set.actionBindings) ? set.actionBindings : [];
  const groupId = getGroupIds(set.setId)[0] || "";
  const bindingId = uniqueId("binding.locomotion", set.actionBindings.map(binding => binding.bindingId));
  set.actionBindings.push({
    bindingId,
    actionId: "locomotion.idle",
    displayName: "Locomotion Idle",
    groupId,
    clipId: getClipIds(set.setId, groupId)[0] || "",
    blendId: getBlendIds(set.setId, groupId)[0] || "",
    timelineId: getTimelineIds(set.setId, groupId)[0] || "",
    required: true,
    tags: [],
    metadata: {}
  });
  render();
}

function removeActionBinding(set, index) {
  if (!set || !Array.isArray(set.actionBindings)) return;
  set.actionBindings.splice(index, 1);
  render();
}

function addClip() {
  const group = findGroup(state.selected.setId, state.selected.groupId);
  if (!group) return addGroup();
  group.clips ||= [];
  const clipId = uniqueId("clip.idle", group.clips.map(clip => clip.clipId));
  group.clips.push({
    clipId,
    displayName: "Idle",
    sourceSelection: {},
    sourceSubClipId: "",
    sourceClipName: "",
    runtimeResourceKey: "",
    loop: true,
    speed: 1,
    rootMotionPolicy: "Ignore",
    tags: []
  });
  selectNode("clip", state.selected.setId, state.selected.groupId, clipId);
  render();
}

function removeClip(clipId) {
  const group = findGroup(state.selected.setId, state.selected.groupId);
  if (!group) return;
  group.clips = (group.clips || []).filter(clip => clip.clipId !== clipId);
  if (findSet(state.selected.setId)?.defaultClipId === clipId) findSet(state.selected.setId).defaultClipId = "";
  if (findSet(state.selected.setId)?.fallbackClipId === clipId) findSet(state.selected.setId).fallbackClipId = "";
  for (const blend of [...(group.blend1D || []), ...(group.blend2D || [])]) {
    if (blend.defaultClipId === clipId) blend.defaultClipId = "";
    blend.points = (blend.points || []).filter(point => point.clipId !== clipId);
  }
  for (const timeline of group.timelines || []) {
    if (timeline.clipId === clipId) timeline.clipId = "";
    for (const eventItem of timeline.events || []) {
      if (eventItem.clipId === clipId) eventItem.clipId = "";
    }
  }
  for (const binding of findSet(state.selected.setId)?.actionBindings || []) {
    if (binding.groupId === group.groupId && binding.clipId === clipId) binding.clipId = "";
  }
  for (const profile of state.animation.profiles || []) {
    for (const slot of profile.slots || []) {
      if (slot.setId === state.selected.setId && slot.groupId === group.groupId && slot.defaultClipId === clipId) slot.defaultClipId = "";
    }
  }
  if (state.selected.clipId === clipId) state.selected.clipId = group.clips[0]?.clipId || "";
  render();
}

function handleBlendEditorInput(event) {
  const group = findGroup(state.selected.setId, state.selected.groupId);
  if (!group) return;

  if (event.target.dataset.blendSelect) {
    state.blendEditor.view = event.target.dataset.blendSelect;
    state.blendEditor.blendId = event.target.value;
    renderBlendEditorState();
    return;
  }

  const blend = getSelectedBlend(group);
  if (!blend) return;

  if (event.target.dataset.blendField) {
    const field = event.target.dataset.blendField;
    blend[field] = event.target.dataset.type === "number" ? Number(event.target.value) : event.target.value;
    if (field === "blendId") state.blendEditor.blendId = blend.blendId;
    if (event.type === "change") renderBlendEditorState();
    return;
  }

  if (event.target.dataset.pointField) {
    const index = Number(event.target.dataset.pointIndex);
    const point = (blend.points || [])[index];
    if (!point) return;
    const field = event.target.dataset.pointField;
    point[field] = event.target.dataset.type === "number" ? Number(event.target.value) : event.target.value;
    if (event.type === "change") renderBlendEditorState();
  }
}

function addBlend(kind) {
  const group = findGroup(state.selected.setId, state.selected.groupId);
  if (!group) return;
  const normalizedKind = kind === "2D" ? "2D" : "1D";
  const blends = getBlendList(group, normalizedKind);
  const blendId = uniqueId(normalizedKind === "1D" ? "blend.speed" : "blend.move2d", blends.map(blend => blend.blendId));
  const firstClipId = (group.clips || []).find(clip => clip.clipId)?.clipId || "";
  const blend = normalizedKind === "1D"
    ? {
      blendId,
      displayName: "Speed Blend",
      parameter: "speed",
      defaultClipId: firstClipId,
      points: firstClipId ? [{ clipId: firstClipId, value: 0, weight: 1 }] : [],
      diagnostics: [],
      metadata: {}
    }
    : {
      blendId,
      displayName: "Move Blend",
      xParameter: "moveX",
      yParameter: "moveY",
      defaultClipId: firstClipId,
      points: firstClipId ? [{ clipId: firstClipId, x: 0, y: 0, weight: 1 }] : [],
      diagnostics: [],
      metadata: {}
    };
  blends.push(blend);
  state.blendEditor.view = normalizedKind;
  state.blendEditor.blendId = blendId;
  renderBlendEditorState();
}

function removeBlend(kind) {
  const group = findGroup(state.selected.setId, state.selected.groupId);
  if (!group) return;
  const normalizedKind = kind === "2D" ? "2D" : "1D";
  const key = normalizedKind === "1D" ? "blend1D" : "blend2D";
  const removedBlendId = state.blendEditor.blendId;
  group[key] = (group[key] || []).filter(blend => blend.blendId !== state.blendEditor.blendId);
  for (const binding of findSet(state.selected.setId)?.actionBindings || []) {
    if (binding.groupId === group.groupId && binding.blendId === removedBlendId) binding.blendId = "";
  }
  for (const profile of state.animation.profiles || []) {
    for (const slot of profile.slots || []) {
      if (slot.setId === state.selected.setId && slot.groupId === group.groupId && slot.defaultBlendId === removedBlendId) slot.defaultBlendId = "";
    }
  }
  state.blendEditor.blendId = "";
  ensureBlendSelection(group);
  renderBlendEditorState();
}

function addBlendPoint(kind) {
  const group = findGroup(state.selected.setId, state.selected.groupId);
  const blend = group ? getSelectedBlend(group) : null;
  if (!blend) return;
  blend.points ||= [];
  const clipId = (group.clips || []).find(clip => clip.clipId && !(blend.points || []).some(point => point.clipId === clip.clipId))?.clipId ||
    (group.clips || [])[0]?.clipId || "";
  if (kind === "2D") {
    blend.points.push({ clipId, x: blend.points.length, y: 0, weight: 1 });
  } else {
    blend.points.push({ clipId, value: blend.points.length, weight: 1 });
  }
  renderBlendEditorState();
}

function removeBlendPoint(kind, index) {
  const group = findGroup(state.selected.setId, state.selected.groupId);
  const blend = group ? getSelectedBlend(group) : null;
  if (!blend || !Array.isArray(blend.points)) return;
  blend.points.splice(index, 1);
  renderBlendEditorState();
}

function renderBlendEditorState() {
  renderStatus();
  renderWorkspace();
  renderDiagnostics();
}

function handleTimelineEditorInput(event) {
  const group = findGroup(state.selected.setId, state.selected.groupId);
  if (!group) return;

  if (event.target.dataset.timelineSelect !== undefined) {
    state.timelineEditor.timelineId = event.target.value;
    renderTimelineEditorState();
    return;
  }

  const timeline = getSelectedTimeline(group);
  if (!timeline) return;

  if (event.target.dataset.timelineField) {
    const field = event.target.dataset.timelineField;
    timeline[field] = event.target.dataset.type === "number" ? Number(event.target.value) : event.target.value;
    if (field === "timelineId") state.timelineEditor.timelineId = timeline.timelineId;
    if (event.type === "change") renderTimelineEditorState();
    return;
  }

  if (event.target.dataset.timelineMetadata) {
    timeline.metadata ||= {};
    const key = event.target.dataset.timelineMetadata;
    if (event.target.value === "") delete timeline.metadata[key];
    else timeline.metadata[key] = String(Number(event.target.value));
    if (event.type === "change") renderTimelineEditorState();
    return;
  }

  if (event.target.dataset.eventField) {
    const index = Number(event.target.dataset.eventIndex);
    const eventItem = (timeline.events || [])[index];
    if (!eventItem) return;
    const field = event.target.dataset.eventField;
    eventItem[field] = event.target.dataset.type === "number" ? Number(event.target.value) : event.target.value;
    if (field === "eventKind") {
      eventItem.eventKind = normalizeEventKind(eventItem.eventKind);
      updateEventPayloadTemplate(eventItem);
    }
    if (event.type === "change") renderTimelineEditorState();
  }
}

function addTimeline() {
  const group = findGroup(state.selected.setId, state.selected.groupId);
  if (!group) return;
  const timelines = getTimelineList(group);
  const timelineId = uniqueId("timeline.base", timelines.map(timeline => timeline.timelineId));
  const firstClipId = getLocalClipIds(group)[0] || "";
  const timeline = {
    timelineId,
    displayName: "Base Timeline",
    clipId: firstClipId,
    timeDomain: "Seconds",
    events: [],
    diagnostics: [],
    metadata: {}
  };
  timelines.push(timeline);
  state.timelineEditor.timelineId = timelineId;
  renderTimelineEditorState();
}

function removeTimeline() {
  const group = findGroup(state.selected.setId, state.selected.groupId);
  if (!group) return;
  const removedTimelineId = state.timelineEditor.timelineId;
  group.timelines = getTimelineList(group).filter(timeline => timeline.timelineId !== removedTimelineId);
  for (const binding of findSet(state.selected.setId)?.actionBindings || []) {
    if (binding.groupId === group.groupId && binding.timelineId === removedTimelineId) binding.timelineId = "";
  }
  state.timelineEditor.timelineId = "";
  ensureTimelineSelection(group);
  renderTimelineEditorState();
}

function addTimelineEvent() {
  const group = findGroup(state.selected.setId, state.selected.groupId);
  const timeline = group ? getSelectedTimeline(group) : null;
  if (!timeline) return;
  timeline.events ||= [];
  const eventId = uniqueId("event.marker", timeline.events.map(eventItem => eventItem.eventId));
  const eventItem = {
    eventId,
    clipId: timeline.clipId || getLocalClipIds(group)[0] || "",
    timeDomain: timeline.timeDomain || "Seconds",
    time: 0,
    eventKind: "Custom",
    resourceSelection: {},
    payloadJson: "",
    metadata: {}
  };
  updateEventPayloadTemplate(eventItem);
  timeline.events.push(eventItem);
  renderTimelineEditorState();
}

function removeTimelineEvent(index) {
  const timeline = getSelectedTimeline(findGroup(state.selected.setId, state.selected.groupId));
  if (!timeline || !Array.isArray(timeline.events)) return;
  timeline.events.splice(index, 1);
  renderTimelineEditorState();
}

async function copyTimelineContext() {
  const group = findGroup(state.selected.setId, state.selected.groupId);
  const timeline = getSelectedTimeline(group);
  if (!timeline) return;
  const context = {
    packageId: state.animation?.packageId || "",
    setId: state.selected.setId,
    groupId: state.selected.groupId,
    timeline,
    diagnostics: getTimelineDiagnostics(group, timeline)
  };
  try {
    await navigator.clipboard.writeText(JSON.stringify(context, null, 2));
    state.lastMessage = "Timeline Event JSON 已复制";
  } catch {
    state.lastMessage = "Timeline Event JSON 复制失败";
  }
  renderStatus();
}

function renderTimelineEditorState() {
  renderStatus();
  renderWorkspace();
  renderDiagnostics();
}

function handlePreviewWorkflowInput(event) {
  if (event.target.dataset.previewTargetType === undefined) {
    return;
  }
  state.previewWorkflow.targetType = event.target.value || "skeleton";
  renderWorkspace();
}

function getTimelineList(group) {
  if (!group) return [];
  group.timelines = Array.isArray(group.timelines) ? group.timelines : [];
  return group.timelines;
}

function ensureTimelineSelection(group) {
  if (!group) {
    state.timelineEditor.timelineId = "";
    return;
  }
  const timelines = getTimelineList(group);
  if (!timelines.some(timeline => timeline.timelineId === state.timelineEditor.timelineId)) {
    state.timelineEditor.timelineId = timelines[0]?.timelineId || "";
  }
}

function getSelectedTimeline(group) {
  ensureTimelineSelection(group);
  return getTimelineList(group).find(timeline => timeline.timelineId === state.timelineEditor.timelineId) || null;
}

function getBlendList(group, kind) {
  if (!group) return [];
  if (kind === "2D") {
    group.blend2D = Array.isArray(group.blend2D) ? group.blend2D : [];
    return group.blend2D;
  }
  group.blend1D = Array.isArray(group.blend1D) ? group.blend1D : [];
  return group.blend1D;
}

function ensureBlendSelection(group) {
  if (!group) {
    state.blendEditor.blendId = "";
    return;
  }
  const kind = state.blendEditor.view === "2D" ? "2D" : "1D";
  state.blendEditor.view = kind;
  const blends = getBlendList(group, kind);
  if (!blends.some(blend => blend.blendId === state.blendEditor.blendId)) {
    state.blendEditor.blendId = blends[0]?.blendId || "";
  }
}

function getSelectedBlend(group) {
  ensureBlendSelection(group);
  const blends = getBlendList(group, state.blendEditor.view);
  return blends.find(blend => blend.blendId === state.blendEditor.blendId) || null;
}

function blendField(field, label, value) {
  return `
    <label class="blend-field">
      <span>${escapeHtml(label)}</span>
      <input data-blend-field="${escapeHtml(field)}" value="${escapeHtml(value)}">
    </label>`;
}

function clipSelect(field, value, localClipIds, pointField, pointIndex) {
  const data = pointField
    ? `data-point-field="clipId" data-point-index="${pointIndex}"`
    : `data-blend-field="${escapeHtml(field)}"`;
  const options = [
    `<option value="">未设置</option>`,
    ...localClipIds.map(clipId => `<option value="${escapeHtml(clipId)}"${clipId === value ? " selected" : ""}>${escapeHtml(clipId)}</option>`)
  ];
  if (value && !localClipIds.includes(value)) {
    options.push(`<option value="${escapeHtml(value)}" selected>${escapeHtml(value)} (missing)</option>`);
  }
  return `<select ${data}>${options.join("")}</select>`;
}

function pointNumberField(field, value, pointIndex, min, max, step) {
  return `
    <input
      type="number"
      data-type="number"
      data-point-field="${escapeHtml(field)}"
      data-point-index="${pointIndex}"
      min="${min}"
      max="${max}"
      step="${step}"
      value="${escapeHtml(String(value))}">`;
}

function getBlendCursorText(blend, kind) {
  const points = blend.points || [];
  if (kind === "2D") {
    const xs = points.map(point => Number(point.x || 0));
    const ys = points.map(point => Number(point.y || 0));
    return `${blend.xParameter || "x"} ${formatRange(xs)} / ${blend.yParameter || "y"} ${formatRange(ys)}`;
  }
  return `${blend.parameter || "parameter"} ${formatRange(points.map(point => Number(point.value || 0)))}`;
}

function getBlendDiagnostics(group, blend, kind) {
  const localClipIds = new Set((group.clips || []).map(clip => clip.clipId).filter(Boolean));
  const diagnostics = [];
  const points = Array.isArray(blend.points) ? blend.points : [];
  const minPointCount = kind === "2D" ? 3 : 2;

  if (kind === "1D" && !blend.parameter) {
    diagnostics.push({ tone: "warning", code: "ANIM_BLEND_PARAMETER_MISSING", message: "1D Blend 缺少 parameter，运行时无法根据参数采样。" });
  }
  if (kind === "2D" && (!blend.xParameter || !blend.yParameter)) {
    diagnostics.push({ tone: "warning", code: "ANIM_BLEND_PARAMETER_MISSING", message: "2D Blend 需要 X/Y parameter。" });
  }
  if (points.length < minPointCount) {
    diagnostics.push({ tone: "warning", code: "ANIM_BLEND_POINT_COUNT_LOW", message: `${kind} Blend 至少需要 ${minPointCount} 个点才适合稳定采样。` });
  }
  if (blend.defaultClipId && !localClipIds.has(blend.defaultClipId)) {
    diagnostics.push({ tone: "error", code: "ANIM_BLEND_DEFAULT_CLIP_MISSING", message: `Default clip ${blend.defaultClipId} 不在当前 Group 的本地 clipId 列表中。` });
  }

  const seen = new Map();
  points.forEach((point, index) => {
    if (!point.clipId) {
      diagnostics.push({ tone: "error", code: "ANIM_BLEND_POINT_CLIP_EMPTY", message: `Point ${index + 1} 缺少本地 clipId。` });
    } else if (!localClipIds.has(point.clipId)) {
      diagnostics.push({ tone: "error", code: "ANIM_BLEND_POINT_CLIP_MISSING", message: `Point ${index + 1} 引用的 clipId ${point.clipId} 不存在于当前 Group。` });
    }

    const key = kind === "2D"
      ? `${roundKey(point.x)}:${roundKey(point.y)}`
      : roundKey(point.value);
    if (seen.has(key)) {
      diagnostics.push({ tone: "warning", code: "ANIM_BLEND_POINT_DUPLICATE", message: `Point ${seen.get(key) + 1} 和 Point ${index + 1} 坐标重复。` });
    } else {
      seen.set(key, index);
    }
  });

  return diagnostics;
}

function getLocalBlendIssues() {
  const issues = [];
  for (const set of state.animation?.sets || []) {
    for (const group of set.groups || []) {
      for (const blend of group.blend1D || []) {
        issues.push(...getBlendDiagnostics(group, blend, "1D").map(issue => toBlendIssue(issue, set, group, blend, "1D")));
      }
      for (const blend of group.blend2D || []) {
        issues.push(...getBlendDiagnostics(group, blend, "2D").map(issue => toBlendIssue(issue, set, group, blend, "2D")));
      }
    }
  }
  return issues;
}

function toBlendIssue(issue, set, group, blend, kind) {
  return {
    severity: issue.tone === "error" ? "Error" : "Warning",
    code: issue.code,
    sourceObjectPath: `${set.setId || "set"}/${group.groupId || "group"}/${kind}/${blend.blendId || "blend"}`,
    setId: set.setId || "",
    groupId: group.groupId || "",
    blendId: blend.blendId || "",
    message: issue.message
  };
}

function timelineField(field, label, value) {
  return `
    <label class="timeline-field">
      <span>${escapeHtml(label)}</span>
      <input data-timeline-field="${escapeHtml(field)}" value="${escapeHtml(value)}">
    </label>`;
}

function timelineMetadataNumberField(key, label, value, min, max, step) {
  return `
    <label class="timeline-field">
      <span>${escapeHtml(label)}</span>
      <input type="number" data-timeline-metadata="${escapeHtml(key)}" min="${min}" max="${max}" step="${step}" value="${escapeHtml(String(value))}" placeholder="auto">
    </label>`;
}

function timelineClipSelect(field, value, localClipIds, eventField, eventIndex) {
  const data = eventField
    ? `data-event-field="${escapeHtml(field)}" data-event-index="${eventIndex}"`
    : `data-timeline-field="${escapeHtml(field)}"`;
  const options = [
    `<option value="">未设置</option>`,
    ...localClipIds.map(clipId => `<option value="${escapeHtml(clipId)}"${clipId === value ? " selected" : ""}>${escapeHtml(clipId)}</option>`)
  ];
  if (value && !localClipIds.includes(value)) {
    options.push(`<option value="${escapeHtml(value)}" selected>${escapeHtml(value)} (missing)</option>`);
  }
  return `<select ${data}>${options.join("")}</select>`;
}

function timeDomainSelect(field, value, eventField, eventIndex) {
  const data = eventField
    ? `data-event-field="${escapeHtml(field)}" data-event-index="${eventIndex}"`
    : `data-timeline-field="${escapeHtml(field)}"`;
  return `<select ${data}>${TIME_DOMAIN_OPTIONS.map(option => `<option value="${option}"${option === value ? " selected" : ""}>${option}</option>`).join("")}</select>`;
}

function eventKindSelect(value, eventIndex) {
  const normalized = normalizeEventKind(value);
  return `
    <select data-event-field="eventKind" data-event-index="${eventIndex}">
      ${EVENT_KIND_OPTIONS.map(option => `<option value="${escapeHtml(option.value)}"${option.value === normalized ? " selected" : ""}>${escapeHtml(option.label)}</option>`).join("")}
    </select>`;
}

function getLocalClipIds(group) {
  return (group?.clips || []).map(clip => clip.clipId).filter(Boolean);
}

function getTimelineMetadata(timeline, key) {
  return timeline?.metadata?.[key] || "";
}

function getTimelineDurationSeconds(timeline, group) {
  const clip = (group?.clips || []).find(item => item.clipId === timeline?.clipId);
  return Number(timeline?.metadata?.durationSeconds || clip?.metadata?.durationSeconds || 1);
}

function getTimelineFrameCount(timeline, domain) {
  const key = domain === "CombatFrame" ? "combatFrameCount" : "presentationFrameCount";
  return Number(timeline?.metadata?.[key] || 60);
}

function getTimelineEventPercent(timeline, eventItem, domain) {
  const time = Number(eventItem.time || 0);
  if (domain === "Normalized") return time * 100;
  if (domain === "PresentationFrame" || domain === "CombatFrame") {
    const max = getTimelineFrameCount(timeline, domain) || 1;
    return (time / max) * 100;
  }
  const max = getTimelineDurationSeconds(timeline, findGroup(state.selected.setId, state.selected.groupId)) || 1;
  return (time / max) * 100;
}

function getTimelineEventTone(eventItem) {
  const kind = normalizeEventKind(eventItem.eventKind);
  if (kind === "AudioCue") return "audio";
  if (kind === "Vfx") return "vfx";
  if (kind === "TraceOn" || kind === "TraceOff") return "trace";
  if (kind === "HitMarker") return "hit";
  return "";
}

function getTimelineAudioSelectionTitle(selection) {
  if (!selection) return "";
  return selection.audioCueId || selection.audioEventDefinitionId || selection.fmodEventPath || selection.fmodEventGuid || "";
}

function updateEventPayloadTemplate(eventItem) {
  if (!eventItem || eventItem.payloadJson) return;
  const kind = normalizeEventKind(eventItem.eventKind);
  if (kind === "Footstep") eventItem.payloadJson = "{\"surface\":\"default\"}";
  else if (kind === "TraceOn") eventItem.payloadJson = "{\"traceId\":\"main\"}";
  else if (kind === "TraceOff") eventItem.payloadJson = "{\"traceId\":\"main\"}";
  else if (kind === "HitMarker") eventItem.payloadJson = "{\"marker\":\"hit\"}";
  else if (kind === "CameraCue") eventItem.payloadJson = "{\"cue\":\"impact\"}";
  else if (kind === "Vfx") eventItem.payloadJson = "{\"attach\":\"socket\"}";
  else if (kind === "AudioCue") eventItem.payloadJson = "{\"audioCueMode\":\"FMOD\"}";
}

function getTimelineDiagnostics(group, timeline) {
  const diagnostics = [];
  const localClipIds = new Set(getLocalClipIds(group));
  const duration = getTimelineDurationSeconds(timeline, group);
  if (!timeline.clipId) {
    diagnostics.push({ tone: "error", code: "ANIM_TIMELINE_CLIP_EMPTY", message: "Timeline 缺少本地 clipId。" });
  } else if (!localClipIds.has(timeline.clipId)) {
    diagnostics.push({ tone: "error", code: "ANIM_TIMELINE_CLIP_MISSING", message: `Timeline clipId ${timeline.clipId} 不存在于当前 Group。` });
  }

  const eventIds = new Map();
  for (const [index, eventItem] of (timeline.events || []).entries()) {
    if (!eventItem.eventId) {
      diagnostics.push({ tone: "error", code: "ANIM_TIMELINE_EVENT_ID_EMPTY", message: `Event ${index + 1} 缺少 eventId。` });
    } else if (eventIds.has(eventItem.eventId)) {
      diagnostics.push({ tone: "warning", code: "ANIM_TIMELINE_EVENT_ID_DUPLICATE", message: `Event ${eventIds.get(eventItem.eventId) + 1} 和 Event ${index + 1} 使用重复 eventId ${eventItem.eventId}。` });
    } else {
      eventIds.set(eventItem.eventId, index);
    }

    const clipId = eventItem.clipId || timeline.clipId || "";
    if (!clipId) {
      diagnostics.push({ tone: "error", code: "ANIM_TIMELINE_EVENT_CLIP_EMPTY", message: `Event ${index + 1} 缺少 clipId。` });
    } else if (!localClipIds.has(clipId)) {
      diagnostics.push({ tone: "error", code: "ANIM_TIMELINE_EVENT_CLIP_MISSING", message: `Event ${index + 1} 引用的 clipId ${clipId} 不存在于当前 Group。` });
    }

    const domain = eventItem.timeDomain || timeline.timeDomain || "Seconds";
    const time = Number(eventItem.time || 0);
    if (domain === "Normalized" && (time < 0 || time > 1)) {
      diagnostics.push({ tone: "error", code: "ANIM_TIMELINE_EVENT_NORMALIZED_RANGE", message: `Event ${index + 1} normalized time 必须在 0..1。` });
    } else if ((domain === "PresentationFrame" || domain === "CombatFrame") && time < 0) {
      diagnostics.push({ tone: "error", code: "ANIM_TIMELINE_EVENT_FRAME_NEGATIVE", message: `Event ${index + 1} frame 不能小于 0。` });
    } else if (domain === "Seconds" && (time < 0 || time > duration)) {
      diagnostics.push({ tone: "error", code: "ANIM_TIMELINE_EVENT_SECONDS_RANGE", message: `Event ${index + 1} seconds 超出 clip duration ${formatNumber(duration)}。` });
    }

    const kind = normalizeEventKind(eventItem.eventKind);
    if ((kind === "Vfx" || kind === "AudioCue") && !hasEventResourceSelection(eventItem)) {
      diagnostics.push({ tone: "warning", code: "ANIM_TIMELINE_EVENT_RESOURCE_MISSING", message: `Event ${index + 1} 是 ${kind}，但还没有选择资源。` });
    }
  }
  return diagnostics;
}

function hasEventResourceSelection(eventItem) {
  return Boolean(getSelectionTitle(eventItem.resourceSelection) || getTimelineAudioSelectionTitle(eventItem.resourceSelection));
}

function normalizeEventKind(value) {
  const normalized = String(value || "").trim().toLowerCase().replaceAll(".", "").replaceAll("_", "").replaceAll(" ", "");
  if (normalized === "footstep") return "Footstep";
  if (normalized === "traceon") return "TraceOn";
  if (normalized === "traceoff") return "TraceOff";
  if (normalized === "hitmarker" || normalized === "hit") return "HitMarker";
  if (normalized === "vfx" || normalized === "visualeffect") return "Vfx";
  if (normalized === "audiocue" || normalized === "fmod" || normalized === "fmodevent") return "AudioCue";
  if (normalized === "cameracue" || normalized === "camera") return "CameraCue";
  return "Custom";
}

function getEventResourceFieldSpec(kind) {
  if (kind === "Vfx") return state.fieldSpecs?.eventVfx || FALLBACK_EVENT_VFX_SPEC;
  if (kind === "AudioCue") return state.fieldSpecs?.eventAudioCue || FALLBACK_EVENT_AUDIO_CUE_SPEC;
  return null;
}

function getLocalTimelineIssues() {
  const issues = [];
  for (const set of state.animation?.sets || []) {
    for (const group of set.groups || []) {
      for (const timeline of group.timelines || []) {
        issues.push(...getTimelineDiagnostics(group, timeline).map(issue => ({
          severity: issue.tone === "error" ? "Error" : "Warning",
          code: issue.code,
          sourceObjectPath: `${set.setId || "set"}/${group.groupId || "group"}/timeline/${timeline.timelineId || "timeline"}`,
          setId: set.setId || "",
          groupId: group.groupId || "",
          eventId: "",
          message: issue.message
        })));
      }
    }
  }
  return issues;
}

function getLocalReferenceIssues() {
  const issues = [];
  for (const set of state.animation?.sets || []) {
    const groupIds = new Set((set.groups || []).map(group => group.groupId).filter(Boolean));
    const setClipIds = new Set(getSetClipIds(set));
    const setBlendIds = new Set(getSetBlendIds(set));

    if (set.defaultClipId && !setClipIds.has(set.defaultClipId)) {
      issues.push(refIssue("Error", "ANIM_REF_SET_DEFAULT_CLIP_MISSING", `${set.setId}/defaultClipId`, set.setId, "", set.defaultClipId, "Set defaultClipId 不存在于当前 Set 的任何 Group。"));
    }
    if (set.fallbackClipId && !setClipIds.has(set.fallbackClipId)) {
      issues.push(refIssue("Warning", "ANIM_REF_SET_FALLBACK_CLIP_MISSING", `${set.setId}/fallbackClipId`, set.setId, "", set.fallbackClipId, "Set fallbackClipId 不存在于当前 Set 的任何 Group。"));
    }

    for (const binding of set.actionBindings || []) {
      const group = findGroup(set.setId, binding.groupId);
      if (binding.groupId && !groupIds.has(binding.groupId)) {
        issues.push(refIssue("Error", "ANIM_REF_BINDING_GROUP_MISSING", `${set.setId}/binding/${binding.bindingId}`, set.setId, binding.groupId, binding.actionId, "ActionBinding groupId 不存在。"));
        continue;
      }
      const groupClipIds = new Set(getClipIds(set.setId, binding.groupId));
      const groupBlendIds = new Set(getBlendIds(set.setId, binding.groupId));
      const groupTimelineIds = new Set(getTimelineIds(set.setId, binding.groupId));
      if (binding.clipId && !groupClipIds.has(binding.clipId)) {
        issues.push(refIssue("Error", "ANIM_REF_BINDING_CLIP_MISSING", `${set.setId}/binding/${binding.bindingId}`, set.setId, binding.groupId, binding.clipId, "ActionBinding clipId 不存在于所选 Group。"));
      }
      if (binding.blendId && !groupBlendIds.has(binding.blendId)) {
        issues.push(refIssue("Error", "ANIM_REF_BINDING_BLEND_MISSING", `${set.setId}/binding/${binding.bindingId}`, set.setId, binding.groupId, binding.blendId, "ActionBinding blendId 不存在于所选 Group。"));
      }
      if (binding.timelineId && !groupTimelineIds.has(binding.timelineId)) {
        issues.push(refIssue("Error", "ANIM_REF_BINDING_TIMELINE_MISSING", `${set.setId}/binding/${binding.bindingId}`, set.setId, binding.groupId, binding.timelineId, "ActionBinding timelineId 不存在于所选 Group。"));
      }
    }

    for (const requiredClipId of set.warmup?.requiredClipIds || []) {
      if (!setClipIds.has(requiredClipId)) {
        issues.push(refIssue("Warning", "ANIM_REF_WARMUP_CLIP_MISSING", `${set.setId}/warmup/requiredClipIds`, set.setId, "", requiredClipId, "Warmup requiredClipIds 引用了不存在的 Clip。"));
      }
    }
    for (const requiredBlendId of set.warmup?.requiredBlendIds || []) {
      if (!setBlendIds.has(requiredBlendId)) {
        issues.push(refIssue("Warning", "ANIM_REF_WARMUP_BLEND_MISSING", `${set.setId}/warmup/requiredBlendIds`, set.setId, "", requiredBlendId, "Warmup requiredBlendIds 引用了不存在的 Blend。"));
      }
    }
  }

  const setIds = new Set(getSetIds());
  for (const profile of state.animation?.profiles || []) {
    if (profile.defaultSetId && !setIds.has(profile.defaultSetId)) {
      issues.push(refIssue("Error", "ANIM_REF_PROFILE_SET_MISSING", `profile/${profile.profileId}`, profile.defaultSetId, profile.defaultGroupId, profile.defaultSetId, "Profile defaultSetId 不存在。"));
    }
    const profileGroupIds = new Set(getGroupIds(profile.defaultSetId));
    if (profile.defaultGroupId && !profileGroupIds.has(profile.defaultGroupId)) {
      issues.push(refIssue("Error", "ANIM_REF_PROFILE_GROUP_MISSING", `profile/${profile.profileId}`, profile.defaultSetId, profile.defaultGroupId, profile.defaultGroupId, "Profile defaultGroupId 不存在于 defaultSetId。"));
    }
    for (const slot of profile.slots || []) {
      const setId = slot.setId || profile.defaultSetId;
      const groupId = slot.groupId || profile.defaultGroupId;
      if (setId && !setIds.has(setId)) {
        issues.push(refIssue("Error", "ANIM_REF_SLOT_SET_MISSING", `profile/${profile.profileId}/slot/${slot.slotId}`, setId, groupId, setId, "Slot setId 不存在。"));
        continue;
      }
      if (groupId && !getGroupIds(setId).includes(groupId)) {
        issues.push(refIssue("Error", "ANIM_REF_SLOT_GROUP_MISSING", `profile/${profile.profileId}/slot/${slot.slotId}`, setId, groupId, groupId, "Slot groupId 不存在于 setId。"));
      }
      if (slot.defaultClipId && !getClipIds(setId, groupId).includes(slot.defaultClipId)) {
        issues.push(refIssue("Error", "ANIM_REF_SLOT_CLIP_MISSING", `profile/${profile.profileId}/slot/${slot.slotId}`, setId, groupId, slot.defaultClipId, "Slot defaultClipId 不存在于所选 Group。"));
      }
      if (slot.defaultBlendId && !getBlendIds(setId, groupId).includes(slot.defaultBlendId)) {
        issues.push(refIssue("Error", "ANIM_REF_SLOT_BLEND_MISSING", `profile/${profile.profileId}/slot/${slot.slotId}`, setId, groupId, slot.defaultBlendId, "Slot defaultBlendId 不存在于所选 Group。"));
      }
    }
  }

  return issues;
}

function refIssue(severity, code, path, setId, groupId, value, message) {
  return {
    severity,
    code,
    sourceObjectPath: path,
    setId: setId || "",
    groupId: groupId || "",
    message: `${message} (${value || "empty"})`
  };
}

function selectSet(setId) {
  const set = findSet(setId);
  selectNode("set", set?.setId || "", "", "");
  render();
}

function selectNode(kind, setId, groupId, clipId) {
  state.selected = { kind, setId, groupId, clipId };
  if (kind === "set") {
    state.selected.groupId = firstGroup(setId)?.groupId || "";
    state.selected.clipId = "";
  }
  if (kind === "group") {
    state.selected.clipId = "";
  }
  state.blendEditor.blendId = "";
  state.timelineEditor.timelineId = "";
}

function ensureAnimationShape() {
  state.animation ||= createEmptyAnimationPackage();
  state.animation.schemaVersion ||= "1.0";
  state.animation.sets = Array.isArray(state.animation.sets) ? state.animation.sets : [];
  state.animation.profiles = Array.isArray(state.animation.profiles) ? state.animation.profiles : [];
  for (const profile of state.animation.profiles) {
    profile.slots = Array.isArray(profile.slots) ? profile.slots : [];
    profile.compatibility = ensureCompatibility(profile);
    profile.warmup = ensureWarmup(profile);
    profile.diagnostics = Array.isArray(profile.diagnostics) ? profile.diagnostics : [];
    profile.metadata ||= {};
    for (const slot of profile.slots) {
      slot.preloadPolicy ||= "AnimationWarmup";
      if (slot.required == null) slot.required = false;
    }
  }
  for (const set of state.animation.sets) {
    set.layers = Array.isArray(set.layers) ? set.layers : [];
    set.groups = Array.isArray(set.groups) ? set.groups : [];
    set.actionBindings = Array.isArray(set.actionBindings) ? set.actionBindings : [];
    set.compatibility = ensureCompatibility(set);
    set.warmup = ensureWarmup(set);
    set.diagnostics = Array.isArray(set.diagnostics) ? set.diagnostics : [];
    set.metadata ||= {};
    for (const layer of set.layers) {
      layer.rootMotionPolicy ||= "Ignore";
      if (layer.weight == null) layer.weight = 1;
      layer.avatarMaskSelection ||= {};
      layer.tags = Array.isArray(layer.tags) ? layer.tags : [];
      layer.metadata ||= {};
    }
    for (const binding of set.actionBindings) {
      binding.tags = Array.isArray(binding.tags) ? binding.tags : [];
      binding.metadata ||= {};
      if (binding.required == null) binding.required = false;
    }
    for (const group of set.groups) {
      group.clips = Array.isArray(group.clips) ? group.clips : [];
      group.blend1D = Array.isArray(group.blend1D) ? group.blend1D : [];
      group.blend2D = Array.isArray(group.blend2D) ? group.blend2D : [];
      group.timelines = Array.isArray(group.timelines) ? group.timelines : [];
      for (const blend of group.blend1D) {
        blend.points = Array.isArray(blend.points) ? blend.points : [];
        blend.diagnostics = Array.isArray(blend.diagnostics) ? blend.diagnostics : [];
        blend.metadata ||= {};
        for (const point of blend.points) {
          if (point.weight == null) point.weight = 1;
          if (point.value == null) point.value = 0;
        }
      }
      for (const blend of group.blend2D) {
        blend.points = Array.isArray(blend.points) ? blend.points : [];
        blend.diagnostics = Array.isArray(blend.diagnostics) ? blend.diagnostics : [];
        blend.metadata ||= {};
        for (const point of blend.points) {
          if (point.weight == null) point.weight = 1;
          if (point.x == null) point.x = 0;
          if (point.y == null) point.y = 0;
        }
      }
      for (const clip of group.clips) {
        clip.sourceSelection ||= {};
        clip.tags = Array.isArray(clip.tags) ? clip.tags : [];
        clip.generatedArtifactSelections = Array.isArray(clip.generatedArtifactSelections) ? clip.generatedArtifactSelections : [];
        clip.rootMotionPolicy ||= "Ignore";
        if (clip.speed == null) clip.speed = 1;
        clip.metadata ||= {};
        clip.diagnostics = Array.isArray(clip.diagnostics) ? clip.diagnostics : [];
      }
      for (const timeline of group.timelines) {
        timeline.timeDomain ||= "Seconds";
        timeline.events = Array.isArray(timeline.events) ? timeline.events : [];
        timeline.diagnostics = Array.isArray(timeline.diagnostics) ? timeline.diagnostics : [];
        timeline.metadata ||= {};
        for (const eventItem of timeline.events) {
          eventItem.clipId ||= timeline.clipId || "";
          eventItem.timeDomain ||= timeline.timeDomain || "Seconds";
          if (eventItem.time == null) eventItem.time = 0;
          eventItem.eventKind = normalizeEventKind(eventItem.eventKind);
          eventItem.resourceSelection ||= {};
          eventItem.payloadJson ||= "";
          eventItem.metadata ||= {};
        }
      }
    }
  }
}

function ensureSelection() {
  const set = findSet(state.selected.setId) || firstSet();
  const group = set ? (findGroup(set.setId, state.selected.groupId) || firstGroup(set.setId)) : null;
  const clip = group ? (findClip(set.setId, group.groupId, state.selected.clipId) || (group.clips || [])[0]) : null;
  state.selected = {
    kind: clip ? "clip" : group ? "group" : set ? "set" : "package",
    setId: set?.setId || "",
    groupId: group?.groupId || "",
    clipId: clip?.clipId || ""
  };
}

function createEmptyAnimationPackage() {
  return {
    schemaVersion: "1.0",
    packageId: "animation.package",
    stableId: "anim.package",
    displayName: "Animation Package",
    skeletonProfileId: "",
    avatarProfileId: "",
    sets: [],
    profiles: [],
    diagnostics: [],
    metadata: {}
  };
}

function getSelectedTarget() {
  if (state.selected.kind === "clip") {
    const clip = findSelectedClip();
    return { kind: "clip", value: clip, label: clip?.clipId || "" };
  }
  if (state.selected.kind === "group") {
    const group = findGroup(state.selected.setId, state.selected.groupId);
    return { kind: "group", value: group, label: group?.groupId || "" };
  }
  if (state.selected.kind === "set") {
    const set = findSet(state.selected.setId);
    return { kind: "set", value: set, label: set?.setId || "" };
  }
  return { kind: "package", value: state.animation, label: state.animation?.packageId || "" };
}

function firstSet() {
  return (state.animation?.sets || [])[0] || null;
}

function firstGroup(setId) {
  return (findSet(setId)?.groups || [])[0] || null;
}

function findSet(setId) {
  return (state.animation?.sets || []).find(set => set?.setId === setId) || null;
}

function findGroup(setId, groupId) {
  return (findSet(setId)?.groups || []).find(group => group?.groupId === groupId) || null;
}

function findClip(setId, groupId, clipId) {
  return (findGroup(setId, groupId)?.clips || []).find(clip => clip?.clipId === clipId) || null;
}

function findSelectedClip() {
  return findClip(state.selected.setId, state.selected.groupId, state.selected.clipId);
}

function getAllGroups() {
  return (state.animation?.sets || []).flatMap(set => set.groups || []);
}

function getAllClips() {
  return getAllGroups().flatMap(group => group.clips || []);
}

function getAllLayers() {
  return (state.animation?.sets || []).flatMap(set => set.layers || []);
}

function getAllActionBindings() {
  return (state.animation?.sets || []).flatMap(set => set.actionBindings || []);
}

function getSetIds() {
  return (state.animation?.sets || []).map(set => set.setId).filter(Boolean);
}

function getGroupIds(setId) {
  return (findSet(setId)?.groups || []).map(group => group.groupId).filter(Boolean);
}

function getClipIds(setId, groupId) {
  return (findGroup(setId, groupId)?.clips || []).map(clip => clip.clipId).filter(Boolean);
}

function getBlendIds(setId, groupId) {
  const group = findGroup(setId, groupId);
  return [
    ...(group?.blend1D || []).map(blend => blend.blendId).filter(Boolean),
    ...(group?.blend2D || []).map(blend => blend.blendId).filter(Boolean)
  ];
}

function getTimelineIds(setId, groupId) {
  return (findGroup(setId, groupId)?.timelines || []).map(timeline => timeline.timelineId).filter(Boolean);
}

function getSetClipIds(set) {
  return (set?.groups || []).flatMap(group => (group.clips || []).map(clip => clip.clipId)).filter(Boolean);
}

function getSetBlendIds(set) {
  return (set?.groups || []).flatMap(group => [
    ...(group.blend1D || []).map(blend => blend.blendId),
    ...(group.blend2D || []).map(blend => blend.blendId)
  ]).filter(Boolean);
}

function getProfileClipIds(profile) {
  const ids = new Set();
  for (const slot of profile?.slots || []) {
    if (slot.defaultClipId) ids.add(slot.defaultClipId);
    for (const clipId of getClipIds(slot.setId || profile.defaultSetId, slot.groupId || profile.defaultGroupId)) ids.add(clipId);
  }
  return Array.from(ids);
}

function getProfileBlendIds(profile) {
  const ids = new Set();
  for (const slot of profile?.slots || []) {
    if (slot.defaultBlendId) ids.add(slot.defaultBlendId);
    for (const blendId of getBlendIds(slot.setId || profile.defaultSetId, slot.groupId || profile.defaultGroupId)) ids.add(blendId);
  }
  return Array.from(ids);
}

function ensureCompatibility(owner) {
  if (!owner) return {};
  owner.compatibility ||= {};
  owner.compatibility.requiredBoneIds = Array.isArray(owner.compatibility.requiredBoneIds) ? owner.compatibility.requiredBoneIds : [];
  owner.compatibility.requiredSocketIds = Array.isArray(owner.compatibility.requiredSocketIds) ? owner.compatibility.requiredSocketIds : [];
  owner.compatibility.compatibilityProfileSelection ||= {};
  owner.compatibility.avatarMaskSelection ||= {};
  owner.compatibility.diagnostics = Array.isArray(owner.compatibility.diagnostics) ? owner.compatibility.diagnostics : [];
  owner.compatibility.metadata ||= {};
  return owner.compatibility;
}

function ensureWarmup(owner) {
  if (!owner) return {};
  owner.warmup ||= {};
  owner.warmup.preloadPolicy ||= "AnimationWarmup";
  if (owner.warmup.includeDefaultClip == null) owner.warmup.includeDefaultClip = true;
  if (owner.warmup.includeFallbackClip == null) owner.warmup.includeFallbackClip = true;
  if (owner.warmup.includeActionBindings == null) owner.warmup.includeActionBindings = true;
  if (owner.warmup.includeBlendPoints == null) owner.warmup.includeBlendPoints = true;
  owner.warmup.requiredClipIds = Array.isArray(owner.warmup.requiredClipIds) ? owner.warmup.requiredClipIds : [];
  owner.warmup.requiredBlendIds = Array.isArray(owner.warmup.requiredBlendIds) ? owner.warmup.requiredBlendIds : [];
  owner.warmup.avatarMaskSelections = Array.isArray(owner.warmup.avatarMaskSelections) ? owner.warmup.avatarMaskSelections : [];
  owner.warmup.vfxSelections = Array.isArray(owner.warmup.vfxSelections) ? owner.warmup.vfxSelections : [];
  owner.warmup.audioCueSelections = Array.isArray(owner.warmup.audioCueSelections) ? owner.warmup.audioCueSelections : [];
  owner.warmup.generatedArtifactSelections = Array.isArray(owner.warmup.generatedArtifactSelections) ? owner.warmup.generatedArtifactSelections : [];
  owner.warmup.additionalResourceSelections = Array.isArray(owner.warmup.additionalResourceSelections) ? owner.warmup.additionalResourceSelections : [];
  owner.warmup.diagnostics = Array.isArray(owner.warmup.diagnostics) ? owner.warmup.diagnostics : [];
  owner.warmup.metadata ||= {};
  return owner.warmup;
}

function getIssues() {
  const issues = [];
  if (Array.isArray(state.validation?.issues)) issues.push(...state.validation.issues);
  if (Array.isArray(state.preview3d.result?.diagnostics)) issues.push(...state.preview3d.result.diagnostics);
  if (Array.isArray(state.animation?.diagnostics)) issues.push(...state.animation.diagnostics);
  issues.push(...getLocalBlendIssues());
  issues.push(...getLocalTimelineIssues());
  issues.push(...getLocalReferenceIssues());
  for (const error of state.errors) {
    issues.push({
      severity: "Error",
      code: "ANIM_EDITOR_API_ERROR",
      sourceObjectPath: error.label,
      message: error.message
    });
  }
  return issues;
}

function getSelectionTitle(selection) {
  if (!selection) return "";
  return selection.audioCueId || selection.audioEventDefinitionId || selection.resourceStableId || selection.runtimeResourceKey || selection.providerResourceKey ||
    selection.packageResourceKey || selection.unityAssetPath || selection.unityGuid || "";
}

function clearPreview3d(reason) {
  cleanupPreviewViewport();
  state.preview3d.loading = false;
  state.preview3d.result = null;
  state.preview3d.error = "";
  state.preview3d.playing = false;
  state.preview3d.selectedClipId = "";
  state.preview3d.threeStatus = reason ? "idle" : state.preview3d.threeStatus;
}

function cleanupPreviewViewport() {
  if (typeof state.preview3d.cleanup === "function") {
    state.preview3d.cleanup();
  }
  state.preview3d.cleanup = null;
  state.preview3d.renderId += 1;
}

function togglePreviewPlayback() {
  if (!getSelectedPreviewClip()) return;
  state.preview3d.playing = !state.preview3d.playing;
  state.lastMessage = state.preview3d.playing ? "预览播放中" : "预览已暂停";
  renderStatus();
  renderWorkspace();
}

function selectPreviewClip(clipId) {
  state.preview3d.selectedClipId = clipId || "";
  state.preview3d.playing = false;
  state.lastMessage = "已切换预览 Clip";
  renderStatus();
  renderWorkspace();
}

function getPreviewStatusLabel() {
  if (state.preview3d.loading) return "编译中";
  if (state.preview3d.error) return "失败";
  if (state.preview3d.result) return state.preview3d.playing ? "播放中" : "已就绪";
  return "未运行";
}

function getPreviewStatusTone() {
  if (state.preview3d.error) return "error";
  if (state.preview3d.loading || state.preview3d.result) return "ok";
  return "warn";
}

function getPreviewResources(result = state.preview3d.result) {
  return Array.isArray(result?.previewResources?.resources) ? result.previewResources.resources : [];
}

function getPreviewAnimationClips(result = state.preview3d.result) {
  return Array.isArray(result?.previewResources?.animationClips) ? result.previewResources.animationClips : [];
}

function getPreviewDiagnostics(result = state.preview3d.result) {
  const diagnostics = [];
  if (Array.isArray(result?.diagnostics)) diagnostics.push(...result.diagnostics.map(normalizePreviewDiagnostic));
  if (Array.isArray(result?.compileResult?.Diagnostics)) diagnostics.push(...result.compileResult.Diagnostics.map(normalizePreviewDiagnostic));
  if (state.preview3d.error) diagnostics.push({ tone: "error", code: "ANIM_PREVIEW_ENDPOINT_FAILED", message: state.preview3d.error });
  if (result && getPreviewResources(result).length === 0) {
    diagnostics.push({ tone: "warning", code: "ANIM_PREVIEW_RESOURCES_EMPTY", message: "previewResources.resources 为空，3D 视口只能显示占位状态。" });
  }
  return diagnostics;
}

function normalizePreviewDiagnostic(issue) {
  const severity = String(issue?.severity || issue?.Severity || "").toLowerCase();
  return {
    tone: severity === "error" ? "error" : severity === "warning" ? "warning" : "info",
    code: issue?.code || issue?.Code || "ANIM_PREVIEW_DIAGNOSTIC",
    message: issue?.message || issue?.Message || JSON.stringify(issue)
  };
}

function getPreviewClipId(clip) {
  if (!clip) return "";
  return `${clip.setId || ""}/${clip.groupId || ""}/${clip.clipId || ""}`;
}

function getSelectedPreviewClip() {
  const clips = getPreviewAnimationClips();
  return clips.find(clip => getPreviewClipId(clip) === state.preview3d.selectedClipId) || clips[0] || null;
}

function getPreviewResourceForClip(clip, resources) {
  const key = clip?.runtimeResourceKey || clip?.sourceSelection?.runtimeResourceKey || clip?.sourceSelection?.providerResourceKey || "";
  if (!key) return null;
  return resources.find(resource => resource.resourceKey === key || resource.stableId === key) || null;
}

function isPreviewModelResource(resource) {
  const path = String(resource?.url || resource?.relativePath || resource?.projectRelativePath || "").toLowerCase();
  const kind = String(resource?.kind || "").toLowerCase();
  const usage = String(resource?.usage || "").toLowerCase();
  return resource?.exists !== false && (path.endsWith(".glb") || path.endsWith(".gltf") || kind.includes("model") || usage.includes("model"));
}

async function renderCompilerPreviewViewport() {
  const host = document.getElementById("compilerPreviewViewport");
  if (!host || !state.preview3d.result || state.preview3d.loading) return;
  cleanupPreviewViewport();
  const renderId = ++state.preview3d.renderId;
  const selectedClip = getSelectedPreviewClip();
  const resource = selectedClip?.resource || getPreviewResourceForClip(findSelectedClip(), getPreviewResources());
  if (!resource || !isPreviewModelResource(resource) || !resource.url) {
    state.preview3d.threeStatus = "fallback";
    host.dataset.previewState = "fallback";
    return;
  }

  state.preview3d.threeStatus = "loading";
  host.dataset.previewState = "loading";
  try {
    await renderThreePreviewViewport(host, resource, selectedClip, renderId);
  } catch (error) {
    if (renderId !== state.preview3d.renderId) return;
    state.preview3d.threeStatus = "fallback";
    host.dataset.previewState = "fallback";
    host.innerHTML = `<div class="preview-viewport-fallback warning"><strong>Three.js 预览不可用</strong><span>${escapeHtml(error instanceof Error ? error.message : String(error))}</span></div>`;
  }
}

async function renderThreePreviewViewport(host, resource, clip, renderId) {
  const { THREE, GLTFLoader, OrbitControls } = await loadThreeRuntime();
  if (renderId !== state.preview3d.renderId) return;

  host.innerHTML = "";
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x0b111d);

  const camera = new THREE.PerspectiveCamera(42, 1, 0.01, 100);
  camera.position.set(2.2, 1.5, 3);

  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  host.append(renderer.domElement);

  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.target.set(0, 0.9, 0);
  controls.minDistance = 0.5;
  controls.maxDistance = 8;

  scene.add(new THREE.HemisphereLight(0xddeeff, 0x1f2a38, 1.7));
  const key = new THREE.DirectionalLight(0xffffff, 1.8);
  key.position.set(2.5, 3.5, 2);
  scene.add(key);
  scene.add(new THREE.GridHelper(3.2, 16, 0x34515f, 0x1f2a38));

  const content = new THREE.Group();
  scene.add(content);

  let disposed = false;
  let frame = 0;
  let resizeObserver = null;
  const disposeLocal = () => {
    if (disposed) return;
    disposed = true;
    if (frame) cancelAnimationFrame(frame);
    if (resizeObserver) resizeObserver.disconnect();
    controls.dispose();
    renderer.dispose();
    disposeThreeObject(THREE, scene);
  };
  state.preview3d.cleanup = disposeLocal;

  const loader = new GLTFLoader();
  const gltf = await new Promise((resolve, reject) => loader.load(resource.url, resolve, undefined, reject));
  if (renderId !== state.preview3d.renderId) {
    disposeThreeObject(THREE, gltf.scene);
    disposeLocal();
    return;
  }
  const model = gltf.scene;
  model.name = resource.resourceKey || resource.stableId || clip?.clipId || "previewModel";
  content.add(model);
  framePreviewContent(THREE, camera, controls, content);

  const label = document.createElement("div");
  label.className = "preview-viewport-label";
  label.textContent = `${clip?.displayName || clip?.clipId || "Clip"} / ${resource.resourceKey || "model"}`;
  host.append(label);

  const resize = () => {
    const width = Math.max(1, host.clientWidth);
    const height = Math.max(1, host.clientHeight);
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
  };
  resizeObserver = new ResizeObserver(resize);
  resizeObserver.observe(host);
  resize();

  const clock = new THREE.Clock();
  const animate = () => {
    if (disposed) return;
    const elapsed = clock.getElapsedTime();
    if (state.preview3d.playing) {
      model.rotation.y = Math.sin(elapsed * 1.4) * 0.18;
    }
    controls.update();
    renderer.render(scene, camera);
    frame = requestAnimationFrame(animate);
  };
  animate();
  state.preview3d.threeStatus = "ready";
  host.dataset.previewState = "ready";
  state.preview3d.cleanup = disposeLocal;
}

async function loadThreeRuntime() {
  if (!threeRuntimePromise) {
    threeRuntimePromise = Promise.all([
      import("three"),
      import("three/addons/loaders/GLTFLoader.js"),
      import("three/addons/controls/OrbitControls.js")
    ]).then(([THREE, loaderModule, controlsModule]) => ({
      THREE,
      GLTFLoader: loaderModule.GLTFLoader,
      OrbitControls: controlsModule.OrbitControls
    }));
  }
  return threeRuntimePromise;
}

function framePreviewContent(THREE, camera, controls, content) {
  const box = new THREE.Box3().setFromObject(content);
  if (box.isEmpty()) {
    controls.target.set(0, 0.9, 0);
    camera.position.set(2.2, 1.5, 3);
    return;
  }
  const size = box.getSize(new THREE.Vector3());
  const center = box.getCenter(new THREE.Vector3());
  const maxDim = Math.max(size.x, size.y, size.z, 0.8);
  const distance = maxDim * 2.2;
  controls.target.copy(center);
  camera.position.copy(center).add(new THREE.Vector3(distance * 0.8, distance * 0.55, distance));
  camera.near = Math.max(0.01, distance / 100);
  camera.far = Math.max(50, distance * 10);
  camera.updateProjectionMatrix();
}

function disposeThreeObject(THREE, root) {
  root.traverse(object => {
    if (object.geometry) object.geometry.dispose();
    const materials = Array.isArray(object.material) ? object.material : object.material ? [object.material] : [];
    for (const material of materials) {
      for (const key of Object.keys(material)) {
        const value = material[key];
        if (value && value.isTexture) value.dispose();
      }
      material.dispose();
    }
  });
}

function getProviderData(item, key) {
  for (const binding of item?.providerBindings || []) {
    if (binding?.providerData && binding.providerData[key]) return binding.providerData[key];
  }
  return item?.metadata?.[key] || "";
}

function syncPackageQuery() {
  const url = new URL(window.location.href);
  url.searchParams.set("package", state.packageRelative);
  window.history.replaceState(null, "", url.toString());
}

function statusChip(label, value, tone) {
  return `<span class="status-chip ${tone || "info"}"><strong>${escapeHtml(label)}</strong>${escapeHtml(value)}</span>`;
}

function treeButtonClass(kind, setId, groupId, clipId) {
  const active = state.selected.kind === kind &&
    state.selected.setId === setId &&
    state.selected.groupId === groupId &&
    state.selected.clipId === clipId;
  return `tree-node ${kind} ${active ? "active" : ""}`;
}

function textField(field, label, value, placeholder = "") {
  return `<label class="inspector-field"><span>${escapeHtml(label)}</span><input data-field="${escapeHtml(field)}" value="${escapeHtml(value)}" placeholder="${escapeHtml(placeholder)}"></label>`;
}

function numberField(field, label, value, min, max, step) {
  return `<label class="inspector-field"><span>${escapeHtml(label)}</span><input type="number" data-type="number" data-field="${escapeHtml(field)}" min="${min}" max="${max}" step="${step}" value="${escapeHtml(String(value))}"></label>`;
}

function textArea(field, label, value) {
  return `<label class="inspector-field"><span>${escapeHtml(label)}</span><textarea data-field="${escapeHtml(field)}">${escapeHtml(value)}</textarea></label>`;
}

function csvToList(value) {
  if (Array.isArray(value)) return value.filter(Boolean);
  return String(value || "").split(",").map(item => item.trim()).filter(Boolean);
}

function metadataToText(metadata) {
  return Object.entries(metadata || {}).map(([key, value]) => `${key}=${value}`).join(", ");
}

function textToMetadata(value) {
  const result = {};
  for (const token of String(value || "").split(",")) {
    const [key, ...rest] = token.split("=");
    const normalizedKey = key?.trim();
    if (!normalizedKey) continue;
    result[normalizedKey] = rest.join("=").trim();
  }
  return result;
}

function kebabCase(value) {
  return String(value || "").replace(/[A-Z]/g, match => `-${match.toLowerCase()}`).replace(/^-/, "");
}

function emptyBlock(text) {
  return `<div class="empty">${escapeHtml(text)}</div>`;
}

function uniqueId(base, existing) {
  const used = new Set((existing || []).filter(Boolean));
  if (!used.has(base)) return base;
  for (let i = 2; i < 1000; i++) {
    const value = `${base}.${i}`;
    if (!used.has(value)) return value;
  }
  return `${base}.${Date.now()}`;
}

function formatNumber(value) {
  return Number(value || 0).toLocaleString("en-US", { maximumFractionDigits: 3 });
}

function formatRange(values) {
  if (!values.length) return "未设置";
  const min = Math.min(...values);
  const max = Math.max(...values);
  return min === max ? formatNumber(min) : `${formatNumber(min)}..${formatNumber(max)}`;
}

function roundKey(value) {
  return String(Math.round(Number(value || 0) * 1000) / 1000);
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

async function copyDiagnostics() {
  const text = JSON.stringify({ validation: state.validation, errors: state.errors }, null, 2);
  try {
    await navigator.clipboard.writeText(text);
    state.lastMessage = "诊断 JSON 已复制";
  } catch {
    state.lastMessage = "复制失败";
  }
  renderStatus();
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
