// Resource Library split module.
// Audio workspace for FMOD / AudioCue status aggregation and semantic manifest edits.
// Loaded by index.html as a classic script; keep shared bindings global.
const AUDIO_PROVIDER_STATE_LABELS = {
  ready: "FMOD 来源正常",
  diagnostics: "FMOD 来源有问题",
  stale: "FMOD 来源已过期",
  unavailable: "FMOD 来源不可用"
};
const AUDIO_SEMANTIC_STATUS_LABELS = {
  Missing: ["缺少语义", "warn"],
  Draft: ["语义草稿", "warn"],
  ReviewNeeded: ["待人工试听", "info"],
  Ready: ["语义就绪", "ok"],
  Rejected: ["试听拒绝", "error"]
};
const AUDIO_PREVIEW_EXTENSIONS = ["wav", "ogg", "mp3"];
const AUDIO_INSPECTOR_TABS = [
  ["overview", "概览"],
  ["semantic", "语义"],
  ["replacement", "替换"],
  ["technical", "技术"]
];
let audioPreviewElement = null;
let audioPreviewState = {
  resourceKey: "",
  status: "idle",
  message: "",
  url: ""
};

function handleAudioWorkspaceEvent(event) {
  const replacementTarget = event.target.closest?.("[data-audio-replacement-target]");
  if (replacementTarget && event.type === "change") {
    updateAudioReplacementTarget(replacementTarget);
    return;
  }

  const semanticField = event.target.closest?.("[data-audio-semantic-field]");
  if (semanticField && (event.type === "input" || event.type === "change")) {
    updateAudioSemanticDraftFromControl(semanticField);
    if (event.type === "change") renderAudioWorkspace();
    return;
  }

  if (event.type === "input" && event.target.id === "audioSearchInput") {
    state.audioFilters.search = event.target.value;
    renderAudioWorkspace();
    return;
  }

  if (event.type === "change") {
    const mapping = {
      audioBankFilter: "bank",
      audioSpatialFilter: "spatial",
      audioLoopFilter: "loop",
      audioDiagnosticsFilter: "diagnostics",
      audioReferencedFilter: "referenced"
    };
    const key = mapping[event.target.id];
    if (key) {
      state.audioFilters[key] = event.target.value;
      renderAudioWorkspace();
      return;
    }
  }

  if (event.type !== "click") return;
  const copyFieldButton = event.target.closest("[data-audio-copy-text]");
  if (copyFieldButton && !copyFieldButton.disabled) {
    copyText(copyFieldButton.dataset.audioCopyText || "", "已复制音频字段");
    return;
  }

  const inspectorTab = event.target.closest("[data-audio-inspector-tab]");
  if (inspectorTab && !inspectorTab.disabled) {
    state.audioInspectorTab = inspectorTab.dataset.audioInspectorTab || "overview";
    renderAudioWorkspace();
    return;
  }

  const toggleButton = event.target.closest("[data-audio-toggle]");
  if (toggleButton && !toggleButton.disabled) {
    if (toggleButton.dataset.audioToggle === "cue-detail") {
      state.audioCueDetailExpanded = !state.audioCueDetailExpanded;
      renderAudioWorkspace();
      return;
    }
  }

  const resourceButton = event.target.closest("[data-audio-resource-key]");
  if (resourceButton && !resourceButton.disabled) {
    selectResource(resourceButton.dataset.audioResourceKey);
    return;
  }

  const actionButton = event.target.closest("[data-audio-action]");
  if (!actionButton || actionButton.disabled) return;
  handleAudioAction(actionButton.dataset.audioAction);
}

function handleAudioKeyboardShortcut(event) {
  if (!event || state.activeWorkspace !== "audio") return false;
  if (event.defaultPrevented || event.metaKey || event.ctrlKey || event.altKey) return false;
  if (event.key !== " " && event.code !== "Space") return false;
  if (isAudioShortcutTextInput(event.target)) return false;
  event.preventDefault();
  playSelectedAudioPreview();
  return true;
}

function isAudioShortcutTextInput(target) {
  if (!target || target === document.body) return false;
  if (target.isContentEditable) return true;
  if (target.closest?.("[data-audio-resource-key]")) return false;
  const tag = String(target.tagName || "").toLowerCase();
  if (tag === "input" || tag === "textarea" || tag === "select") return true;
  if (tag === "button" || tag === "a" || tag === "summary") return true;
  return Boolean(target.closest?.("input, textarea, select, button, a, summary, [contenteditable='true']"));
}

function handleAudioAction(action) {
  if (action === "open-browse-audio") {
    state.filters = { ...state.filters, ...FILTER_DEFAULTS };
    state.filters.kind = getPreferredAudioBrowseKind();
    state.quickFilter = "";
    setWorkspace("browse");
    render();
    return;
  }

  if (action === "copy-summary") {
    copyAudioSummary();
    return;
  }

  if (action === "open-audio-workbench") {
    const row = createAudioWorkspaceModel().selectedRow;
    const url = new URL("/Tools/MxFramework.AudioWorkbench/web/", window.location.origin);
    url.searchParams.set("package", state.packageRelative);
    if (row?.audioCueId) url.searchParams.set("cue", row.audioCueId);
    window.location.href = `${url.pathname}${url.search}`;
    return;
  }

  if (action === "open-references-current") {
    state.referencesFilter = "current";
    state.referencesBundleRuleId = "";
    setWorkspace("references");
    render();
    return;
  }

  if (action === "open-build") {
    setWorkspace("build");
    render();
    return;
  }

  if (action === "open-profile") {
    setWorkspace("profile");
    render();
    return;
  }

  if (action === "open-debug") {
    state.activeTab = "overview";
    setWorkspace("debug");
    render();
    return;
  }

  if (action === "focus-packaging-gaps") {
    applyAudioFilterPreset({ referenced: "notAudioPlan" }, isAudioPackagingGap);
    return;
  }

  if (action === "focus-diagnostics") {
    applyAudioFilterPreset({ diagnostics: "issues" }, row => row.diagnostics.some(isAudioActionableDiagnostic));
    return;
  }

  if (action === "focus-unreferenced") {
    applyAudioFilterPreset({ referenced: "unreferenced" }, row => row.references.length === 0);
    return;
  }

  if (action === "focus-unbound-bank") {
    applyAudioFilterPreset({ bank: "未绑定 Bank" }, row => row.banks.includes("未绑定 Bank"));
    return;
  }

  if (action === "focus-stale") {
    applyAudioFilterPreset({ diagnostics: "stale" }, row => row.cacheStale);
    return;
  }

  if (action === "play-preview") {
    playSelectedAudioPreview();
    return;
  }

  if (action === "stop-preview") {
    stopAudioPreview("已停止试听");
    return;
  }

  if (action === "copy-selected-json") {
    copySelectedAudioJson();
    return;
  }

  if (action === "save-semantics") {
    saveSelectedAudioSemanticDraft();
    return;
  }

  if (action === "validate-replacement") {
    validateSelectedAudioReplacement();
    return;
  }

  if (action === "reset-semantics-draft") {
    resetSelectedAudioSemanticDraft();
  }
}

function applyAudioFilterPreset(overrides, rowPredicate) {
  state.audioFilters = {
    search: "",
    bank: "all",
    spatial: "all",
    loop: "all",
    diagnostics: "all",
    referenced: "all",
    ...overrides
  };

  const rows = getNormalizedItems()
    .filter(isAudioResourceItem)
    .map(createAudioRow)
    .sort(compareAudioRows);
  const row = rows.find(rowPredicate);
  if (row) state.selectedResourceKey = row.item.key;
  renderAudioWorkspace();
}
