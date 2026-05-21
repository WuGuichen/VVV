const DEFAULT_PACKAGE = "Tools/MxFramework.Authoring/samples/character-iron-vanguard";

const API = {
  packages: "/api/authoring/animation/packages",
  load: packageRelative => `/api/authoring/animation/load?package=${encodeURIComponent(packageRelative)}`,
  save: "/api/authoring/animation/save",
  validate: "/api/authoring/animation/validate",
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

const ROOT_MOTION_OPTIONS = ["Ignore", "MotionDelta", "ApplyToActorReference"];

const state = {
  packages: [],
  packageRelative: DEFAULT_PACKAGE,
  documentRelative: "",
  exists: false,
  canWrite: false,
  animation: null,
  fieldSpecs: {},
  validation: null,
  selected: { kind: "package", setId: "", groupId: "", clipId: "" },
  resourcePicker: {
    open: false,
    clip: null,
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
    "openResourceManagerButton", "openCharacterStudioButton", "statusStrip", "treeSummary",
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
    }
  });

  el.inspectorContent.addEventListener("input", handleInspectorInput);
  el.inspectorContent.addEventListener("change", handleInspectorInput);
  el.inspectorContent.addEventListener("click", event => {
    const pickerButton = event.target.closest("button[data-open-source-picker]");
    if (pickerButton) {
      const clip = findSelectedClip();
      openSourceClipPicker(clip);
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
  el.treeSummary.textContent = `${sets.length} 个 Set，${getAllGroups().length} 个 Group，${getAllClips().length} 个 Clip`;
  if (sets.length === 0) {
    el.animationTree.innerHTML = emptyBlock("还没有 AnimationSet。");
    return;
  }

  el.animationTree.innerHTML = sets.map(set => {
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
  if (!set) {
    el.workspaceTitle.textContent = "Clip Mapping";
    el.workspaceSubtitle.textContent = "新增 Set 后开始配置动画组";
    el.mappingWorkspace.innerHTML = emptyBlock("还没有可编辑的 AnimationSet。");
    return;
  }

  if (!group) {
    el.workspaceTitle.textContent = set.displayName || set.setId || "AnimationSet";
    el.workspaceSubtitle.textContent = "选择或新增 Group 后编辑 Clip mapping";
    el.mappingWorkspace.innerHTML = emptyBlock("当前 Set 还没有 Group。");
    return;
  }

  const clips = group.clips || [];
  el.workspaceTitle.textContent = group.displayName || group.groupId || "AnimationGroup";
  el.workspaceSubtitle.textContent = `${group.usage || "custom"} / ${clips.length} clips`;
  if (clips.length === 0) {
    el.mappingWorkspace.innerHTML = emptyBlock("当前 Group 还没有 Clip。");
    return;
  }

  el.mappingWorkspace.innerHTML = `
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
    ${textField("tagsText", "Tags", (clip.tags || []).join(", "))}`;
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

async function openSourceClipPicker(clip) {
  if (!clip) return;
  state.resourcePicker = {
    open: true,
    clip,
    rows: [],
    search: "",
    onlySelectable: true,
    loading: true,
    error: ""
  };
  renderResourcePicker();

  const fieldSpec = state.fieldSpecs?.sourceClip || FALLBACK_SOURCE_CLIP_SPEC;
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

function closeResourcePicker() {
  state.resourcePicker.open = false;
  renderResourcePicker();
}

function renderResourcePicker() {
  el.resourcePickerOverlay.classList.toggle("hidden", !state.resourcePicker.open);
  el.resourcePickerSearch.value = state.resourcePicker.search;
  el.resourcePickerOnlySelectable.checked = state.resourcePicker.onlySelectable;
  if (!state.resourcePicker.open) return;

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
  if (!row?.selectable || !row.item || !state.resourcePicker.clip) return;

  const fieldSpec = state.fieldSpecs?.sourceClip || FALLBACK_SOURCE_CLIP_SPEC;
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
  }, "解析 Animation.SourceClip");

  if (!result?.accepted) {
    state.lastMessage = "资源选择未通过校验";
    renderStatus();
    return;
  }

  const clip = state.resourcePicker.clip;
  clip.sourceSelection = result.selection || {};
  clip.sourceClipName ||= row.item.displayName || row.item.stableId || "";
  if (!clip.sourceSubClipId) {
    clip.sourceSubClipId = getProviderData(row.item, "unitySubAssetKey") || getProviderData(row.item, "sourceSubClipId") || "";
  }
  state.lastMessage = "已选择源动画资源";
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
  if (state.selected.clipId === clipId) state.selected.clipId = group.clips[0]?.clipId || "";
  render();
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
}

function ensureAnimationShape() {
  state.animation ||= createEmptyAnimationPackage();
  state.animation.schemaVersion ||= "1.0";
  state.animation.sets = Array.isArray(state.animation.sets) ? state.animation.sets : [];
  state.animation.profiles = Array.isArray(state.animation.profiles) ? state.animation.profiles : [];
  for (const set of state.animation.sets) {
    set.layers = Array.isArray(set.layers) ? set.layers : [];
    set.groups = Array.isArray(set.groups) ? set.groups : [];
    set.actionBindings = Array.isArray(set.actionBindings) ? set.actionBindings : [];
    for (const group of set.groups) {
      group.clips = Array.isArray(group.clips) ? group.clips : [];
      group.blend1D = Array.isArray(group.blend1D) ? group.blend1D : [];
      group.blend2D = Array.isArray(group.blend2D) ? group.blend2D : [];
      group.timelines = Array.isArray(group.timelines) ? group.timelines : [];
      for (const clip of group.clips) {
        clip.sourceSelection ||= {};
        clip.tags = Array.isArray(clip.tags) ? clip.tags : [];
        clip.rootMotionPolicy ||= "Ignore";
        if (clip.speed == null) clip.speed = 1;
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

function getIssues() {
  const issues = [];
  if (Array.isArray(state.validation?.issues)) issues.push(...state.validation.issues);
  if (Array.isArray(state.animation?.diagnostics)) issues.push(...state.animation.diagnostics);
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
  return selection.resourceStableId || selection.runtimeResourceKey || selection.providerResourceKey ||
    selection.packageResourceKey || selection.unityAssetPath || selection.unityGuid || "";
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

function textField(field, label, value) {
  return `<label class="inspector-field"><span>${escapeHtml(label)}</span><input data-field="${escapeHtml(field)}" value="${escapeHtml(value)}"></label>`;
}

function numberField(field, label, value, min, max, step) {
  return `<label class="inspector-field"><span>${escapeHtml(label)}</span><input type="number" data-type="number" data-field="${escapeHtml(field)}" min="${min}" max="${max}" step="${step}" value="${escapeHtml(String(value))}"></label>`;
}

function textArea(field, label, value) {
  return `<label class="inspector-field"><span>${escapeHtml(label)}</span><textarea data-field="${escapeHtml(field)}">${escapeHtml(value)}</textarea></label>`;
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
