const state = {
  package: new URLSearchParams(location.search).get("package") || "Tools/MxFramework.Authoring/samples/character-codex-flow-test",
  selectedCueId: new URLSearchParams(location.search).get("cue") || "",
  appScreen: initialAppScreen(),
  workspaceMode: initialAppScreen() === "matrix" ? "pairs" : "cues",
  selectedMaterialPairKey: new URLSearchParams(location.search).get("pair") || "",
  pairVariantFilter: "all",
  pairStatusFilter: "all",
  selectedPairVariant: "Impact",
  payload: null,
  manifest: null,
  savedManifestText: "",
  contentHash: "",
  preview: null,
  previewPoll: 0,
  waveformCache: new Map(),
  waveformRenderToken: 0,
  audioAnalysisMode: "source",
  audioAnalysisLoop: false,
  audioAnalysisPlayer: null,
  audioAnalysisTimer: 0,
  audioAnalysisSeek: 0,
  candidateWaveformRenderToken: 0,
  audioContext: null,
  generationHealth: null,
  generationHealthError: "",
  generationBootstrap: null,
  generationBootstrapPoll: 0,
  generationBriefs: new Map(),
  generationManifest: null,
  generationManifestHash: "",
  savedGenerationManifestText: "",
  physicalManifest: null,
  physicalManifestHash: "",
  savedPhysicalManifestText: "",
  physicalCoverage: null,
  physicalCoverageStale: false,
  physicalCoverageFamilyId: "",
  physicalCoverageCell: null,
  physicalResolve: null,
  physicalResolvePending: false,
  physicalPreview: null,
  physicalScenarios: new Map(),
  physicalAnalyses: new Map(),
  physicalAnalysisSequence: 0,
  physicalCaptures: new Map(),
  physicalCapturePoll: 0,
  physicalCaptureActiveRequestId: "",
  managedPhysicalEvent: null,
  managedPhysicalEventPoll: 0,
  managedPhysicalDrafts: new Map(),
  physicalResolveTimer: 0,
  initialRecipes: new Map(),
  generationJob: null,
  generationCueId: "",
  generationPoll: 0,
  selectedCandidates: new Set(),
  generationSessions: { format: "mx.audioGenerationSessions.v1", sessions: [] },
  generationSessionHash: "",
  generationJobs: [],
  pairPreview: null,
  pairCandidateSource: null,
  pairCandidateChoices: new Map(),
  pairCandidateLoads: new Map(),
  candidateReviews: new Map(),
  candidateEdits: new Map(),
  openCandidateEditors: new Set(),
  candidateEditSources: [],
  mixSources: [],
  mixDecodeCache: new Map(),
  generationTabs: new Map(),
  workspaceTabs: new Map(),
  audioApplication: null,
  audioApplicationPoll: 0,
  editorRenderPending: false,
  runtimeSyncBusy: false,
  collisionPromptStale: new Set(),
  search: "",
  categoryFilter: "all",
  statusFilter: "all",
  syncFilter: "all"
};

const COLLISION_MATERIALS = [
  ["auto","自动"],["generic_solid","通用固体"],["wood","木材"],["metal","金属"],["stone_concrete","石材 / 混凝土"],
  ["glass_ceramic","玻璃 / 陶瓷"],["hard_plastic","硬塑料"],["rubber","橡胶"],["cloth_leather","布料 / 皮革"],
  ["flesh","肉体"],["soil_sand","泥土 / 沙砾"],["ice","冰晶"],["magic_energy","魔法能量"],["custom","自定义"]
];
const COLLISION_OPTIONS = {
  massClass: [["very_light","极轻"],["light","轻"],["medium","中等"],["heavy","重"],["massive","极重"]],
  contactArea: [["point","点状"],["small","小"],["medium","中"],["large","大"],["broad","面接触"]],
  surface: [["polished","抛光"],["smooth","光滑"],["normal","普通"],["rough","粗糙"],["granular","颗粒"]],
  construction: [["solid","实心"],["hollow","空心"],["thin_shell","薄壳"],["soft_filled","软填充"]],
  intensity: [["very_light","轻微"],["light","偏轻"],["medium","中等"],["strong","强烈"],["extreme","极强"]],
  speed: [["slow","慢"],["medium","中等"],["fast","快"],["very_fast","极快"]],
  angle: [["glancing","擦碰"],["oblique","斜向"],["direct","正面"]],
  mode: [["impact","撞击"],["step","踩踏"],["land","落地"],["weapon_hit","武器命中"],["block","格挡"],["scrape","刮擦"],["crush","挤压"]]
};
const COLLISION_PAIR_PRESETS = [
  ["sole-stone","鞋底 × 石地"],["sole-wood","鞋底 × 木地"],["metal-metal","金属 × 金属"],
  ["metal-flesh","金属 × 肉体"],["wood-stone","木制物体 × 石材"],["hard-elastic","硬物体 × 弹性表面"],["semantic","通用物体 × 当前语义材质"]
];

const PHYSICAL_ANALYSIS_AXES = {
  "impactor.materialId": { label:"碰撞体材质", type:"material" },
  "receiver.materialId": { label:"受体材质", type:"material" },
  "impactor.mass": { label:"碰撞体质量", min:0, max:1, decimals:2 },
  "receiver.mass": { label:"受体质量", min:0, max:1, decimals:2 },
  "impactor.contactArea": { label:"碰撞体面积", min:0, max:1, decimals:2 },
  "receiver.contactArea": { label:"受体面积", min:0, max:1, decimals:2 },
  relativeSpeed: { label:"相对速度", min:0, max:20, unit:"m/s", decimals:1 },
  normalSpeed: { label:"法向速度", min:0, max:20, unit:"m/s", decimals:1 },
  tangentialSpeed: { label:"切向速度", min:0, max:20, unit:"m/s", decimals:1 },
  pressure: { label:"压力", min:0, max:1, decimals:2 },
  impulse: { label:"冲量", min:0, max:1, decimals:2 },
  intensity: { label:"冲击强度", min:0, max:1, decimals:2 },
  angleDegrees: { label:"接触角度", min:0, max:180, unit:"°", decimals:0 }
};

const el = Object.fromEntries([
  "serverStatus","actionFeedback","statusStrip","cueSummary","cueList","searchInput","categoryFilter","statusFilter","syncFilter","editorTitle","editorSubtitle","editorWaveformPanel","cueForm",
  "libraryTitle",
  "duplicateButton","archiveButton","deleteButton","diffPanel","referenceSummary","diagnosticsPanel","auditionPanel","runtimePanel","nextStepPanel",
  "reloadButton","saveButton","approveAllButton","publishButton","newCueButton","runtimeActivationPanel","dashboardPanel","dashboardContent",
  "breadcrumbTitle","pageKicker","pageTitle","pageDescription","pageActions","dashboardCount","cueCount","candidateCount","basisCount",
  "coverageCount","blockerCount","snapshotState","diagnosticCount","activationPercent","activationProgress","activationBasisSummary","activationMode"
].map(id => [id, document.getElementById(id)]));

document.addEventListener("DOMContentLoaded", () => {
  document.querySelectorAll("[data-app-screen]").forEach(button => button.addEventListener("click", () => setAppScreen(button.dataset.appScreen, button.dataset.screenTab || "")));
  document.querySelector("[data-copy-diagnostics]")?.addEventListener("click", copyRuntimeActivationDiagnostics);
  el.reloadButton.addEventListener("click", loadState);
  el.saveButton.addEventListener("click", saveDraft);
  el.approveAllButton.addEventListener("click", approveAll);
  el.publishButton.addEventListener("click", publishRuntime);
  el.newCueButton.addEventListener("click", createCue);
  el.duplicateButton.addEventListener("click", duplicateCue);
  el.archiveButton.addEventListener("click", toggleArchive);
  el.deleteButton.addEventListener("click", deleteCue);
  el.searchInput.addEventListener("input", event => { state.search = event.target.value; state.workspaceMode === "pairs" ? renderGlobalPairWorkspace() : renderCueList(); });
  el.categoryFilter.addEventListener("change", event => { state.categoryFilter = event.target.value; renderCueList(); });
  el.statusFilter.addEventListener("change", event => { state.statusFilter = event.target.value; state.workspaceMode === "pairs" ? renderGlobalPairWorkspace() : renderCueList(); });
  el.syncFilter.addEventListener("change", event => { state.syncFilter = event.target.value; renderCueList(); });
  el.cueForm.addEventListener("input", updateDraftFromForm);
  el.cueForm.addEventListener("change", updateDraftFromForm);
  el.cueForm.addEventListener("focusout", flushPendingEditorRender);
  el.cueForm.addEventListener("click", handleFormAction);
  loadState();
});
document.addEventListener("keydown", handleAudioKeyboardShortcut);

async function loadState() {
  setServer("正在读取 Audio Authoring Manifest…", "");
  try {
    const payload = await api(`/api/authoring/audio/state?package=${encodeURIComponent(state.package)}`);
    state.payload = payload;
    state.manifest = structuredClone(payload.manifest || createManifest());
    normalizeManifest();
    state.contentHash = payload.contentHash || "";
    state.savedManifestText = stableText(state.manifest);
    state.generationManifest = structuredClone(payload.audioGeneration?.manifest || createGenerationManifest());
    normalizeGenerationManifest();
    state.generationManifestHash = payload.audioGeneration?.manifestHash || "";
    state.savedGenerationManifestText = stableText(state.generationManifest);
    state.physicalManifest = structuredClone(payload.audioPhysical?.manifest || createPhysicalManifest());
    normalizePhysicalManifest();
    state.physicalManifestHash = payload.audioPhysical?.manifestHash || "";
    state.savedPhysicalManifestText = stableText(state.physicalManifest);
    state.physicalCoverage = payload.audioPhysical?.coverage || null;
    state.physicalCoverageStale = false;
    state.physicalResolve = null;
    state.generationBriefs.clear();
    state.initialRecipes.clear();
    for (const recipe of state.generationManifest.recipes) {
      state.generationBriefs.set(recipe.audioCueId, recipe);
      state.initialRecipes.set(recipe.audioCueId, structuredClone(recipe));
    }
    if (!findCue(state.selectedCueId)) state.selectedCueId = state.manifest.cues[0]?.audioCueId || "";
    await loadGenerationSessions();
    await restoreGenerationShelf(state.selectedCueId);
    setServer("已连接 · Audio v2 / Physical v2", "ok");
    render();
    refreshGenerationHealth();
    restoreManagedPhysicalEventOperation();
  } catch (error) {
    setServer(error.message || "Authoring 服务不可用", "error");
    el.cueList.innerHTML = empty("请从 EditorHub 启动 Authoring 服务。");
  }
}

async function loadGenerationSessions() {
  try {
    const result = await api("/api/authoring/audio/generation/sessions");
    state.generationSessions = result.document || { format: "mx.audioGenerationSessions.v1", sessions: [] };
    state.generationSessions.sessions ||= [];
    state.generationSessionHash = result.hash || "";
    restoreLocalSession(state.selectedCueId);
  } catch (_) { state.generationSessions = { format: "mx.audioGenerationSessions.v1", sessions: [] }; state.generationSessionHash = ""; }
}

function currentSession(cueId = state.selectedCueId) {
  let session = state.generationSessions.sessions.find(item => item.audioCueId === cueId);
  if (!session) {
    session = { audioCueId: cueId, activeJobId: "", recipeHash: "", reviews: {}, candidateEdits: {}, adoptionCandidateIds: [], mixA: {}, mixB: {}, activeMix: "A" };
    state.generationSessions.sessions.push(session);
  }
  session.reviews ||= {}; session.candidateEdits ||= {}; session.adoptionCandidateIds ||= []; session.mixA ||= {}; session.mixB ||= {}; session.activeMix ||= "A";
  return session;
}

function restoreLocalSession(cueId) {
  const session = currentSession(cueId);
  state.selectedCandidates = new Set(session.adoptionCandidateIds || []);
  state.candidateReviews = new Map(Object.entries(session.reviews || {}));
  state.candidateEdits = new Map(Object.entries(session.candidateEdits || {}));
}

async function saveGenerationSession() {
  const session = currentSession();
  session.activeJobId = state.generationJob?.jobId || session.activeJobId || "";
  session.adoptionCandidateIds = [...state.selectedCandidates];
  session.reviews = Object.fromEntries(state.candidateReviews);
  session.candidateEdits = Object.fromEntries(state.candidateEdits);
  session.updatedAtUtc = new Date().toISOString();
  try {
    const result = await api("/api/authoring/audio/generation/sessions", { method: "POST", body: JSON.stringify({ document: state.generationSessions, expectedHash: state.generationSessionHash }) });
    state.generationSessionHash = result.hash || state.generationSessionHash;
  } catch (error) {
    if (error.code === "AUTH_AUDIO_GENERATION_SESSION_CONFLICT") await loadGenerationSessions();
  }
}

async function restoreGenerationShelf(cueId) {
  if (!cueId || state.generationHealth?.status === "offline") return;
  try {
    state.generationJobs = await api(`/api/authoring/audio/generation/jobs?audioCueId=${encodeURIComponent(cueId)}`);
    const wanted = currentSession(cueId).activeJobId;
    const job = state.generationJobs.find(item => item.jobId === wanted) || state.generationJobs[0];
    state.generationCueId = cueId;
    state.generationJob = job || null;
    if (job && !["Succeeded","Failed","Cancelled"].includes(job.status)) pollGenerationJob(job.jobId, cueId);
  } catch (_) { state.generationJobs = []; }
}

function render() {
  document.body.dataset.appScreen = state.appScreen;
  document.body.dataset.workbenchMode = state.workspaceMode;
  document.querySelectorAll("[data-app-screen]").forEach(button => {
    const active = button.dataset.appScreen === state.appScreen;
    button.classList.toggle("active", active);
    button.setAttribute("aria-current", active ? "page" : "false");
  });
  renderAppChrome();
  renderStatus();
  renderRuntimeActivation();
  renderDashboard();
  if (state.appScreen === "dashboard") {
    updateSaveIndicator();
    return;
  }
  if (state.appScreen === "matrix") renderGlobalPairWorkspace();
  else {
    const screenTab = APP_SCREENS[state.appScreen]?.tab;
    if (screenTab && state.selectedCueId && !state.workspaceTabs.has(state.selectedCueId)) state.workspaceTabs.set(state.selectedCueId, screenTab);
    renderCueList();
    renderEditor();
    renderInspector();
  }
  updateSaveIndicator();
}

function setWorkbenchMode(mode) {
  setAppScreen(mode === "pairs" ? "matrix" : "cues");
}

function setAppScreen(screen, tab = "") {
  if (!APP_SCREENS[screen]) return;
  stopCandidateMix();
  state.appScreen = screen;
  state.workspaceMode = screen === "matrix" ? "pairs" : "cues";
  const targetTab = tab || APP_SCREENS[screen]?.tab || "";
  if (targetTab && state.selectedCueId) state.workspaceTabs.set(state.selectedCueId, targetTab);
  const url = new URL(location.href);
  url.searchParams.set("screen", screen);
  if (screen === "matrix") url.searchParams.set("mode", "pairs"); else url.searchParams.delete("mode");
  url.searchParams.set("package", state.package);
  history.replaceState(null, "", url);
  render();
}

function updateSaveIndicator() {
  const dirty = isDirty();
  const parts = [];
  if (isSemanticDirty()) parts.push("Cue");
  if (isRecipeDirty()) parts.push("Recipe");
  if (isPhysicalDirty()) parts.push("Physical");
  el.saveButton.textContent = dirty ? `保存草稿 · ${parts.join(" / ")} 有改动` : "保存草稿";
  el.saveButton.disabled = !state.manifest || !dirty;
  updateRuntimeSyncControls();
}

function updateRuntimeSyncControls() {
  const disabled = state.runtimeSyncBusy || isDirty() || !state.manifest;
  el.publishButton.disabled = disabled;
  el.publishButton.textContent = state.runtimeSyncBusy ? "同步中…" : "一键同步到游戏";
  el.publishButton.title = state.runtimeSyncBusy
    ? "正在核对 FMOD Bank 并更新游戏运行时目录"
    : isDirty() ? "先保存当前草稿" : "同步当前 Applied 版本、FMOD Bank 与游戏运行时目录";
  document.querySelectorAll("[data-page-sync],[data-next-publish]").forEach(button => {
    button.disabled = disabled;
    button.textContent = state.runtimeSyncBusy ? "同步中…" : "一键同步到游戏";
  });
}

function renderStatus() {
  const cues = state.manifest?.cues || [];
  const active = cues.filter(item => item.lifecycle === "Active").length;
  const reviewed = cues.filter(cue => cue.lifecycle === "Active" && hasCurrentOptionalReview(cue)).length;
  const diagnostics = [
    ...(state.payload?.validation?.diagnostics || []),
    ...(state.payload?.audioPhysical?.validation?.diagnostics || [])
  ];
  const errors = diagnostics.filter(item => severity(item) === "Error").length;
  const runtimeSync = state.payload?.runtimeSync || {};
  el.statusStrip.innerHTML = [
    badge(`Cue ${cues.length}`, "info"), badge(`Active ${active}`, ""), badge(`可选试听记录 ${reviewed}/${active}`, "info"),
    badge(`游戏同步 ${numberValue(runtimeSync.syncedCount)}/${cues.length}`, numberValue(runtimeSync.pendingCount) ? "warn" : "ok"),
    runtimeSync.silentPlaceholderCount ? badge(`静音占位 ${runtimeSync.silentPlaceholderCount}`, "warn") : "",
    badge(`Event ${state.manifest?.eventDefinitions?.length || 0}`, ""),
    badge(`Pair ${state.physicalManifest?.materialPairs?.length || 0}`, state.physicalManifest?.materialPairs?.length ? "ok" : "warn"),
    badge(`Basis ${state.physicalManifest?.basisSounds?.length || 0}`, state.physicalManifest?.basisSounds?.length ? "ok" : "warn"),
    badge(`诊断 ${errors}`, errors ? "error" : "ok"),
    badge(state.payload?.snapshotStale ? "FMOD snapshot stale" : "FMOD snapshot current", state.payload?.snapshotStale ? "error" : "ok"),
    badge(state.generationHealth?.status === "ok" ? "SFX 生成服务就绪" : state.generationHealth?.status === "offline" ? "SFX 生成服务离线" : "正在检查 SFX 生成服务", state.generationHealth?.status === "ok" ? "ok" : "warn")
  ].join("");
}

function renderRuntimeActivation() {
  const activation = state.payload?.runtimeActivation;
  if (!activation) {
    el.runtimeActivationPanel.innerHTML = `<div><strong>运行时激活状态不可用</strong><span>刷新 Authoring 服务以获取权威 readiness。</span></div>`;
    return;
  }
  const steps = [
    { label:"采用素材", done:activation.activeBasisCount > 0 && activation.adoptedBasisCount === activation.activeBasisCount, detail:`${activation.adoptedBasisCount}/${activation.activeBasisCount} Active Basis` },
    { label:"覆盖校验", done:!activation.coverageMissing && !activation.coverageAmbiguous, detail:`Required ${activation.coverageRequired} · missing ${activation.coverageMissing} · ambiguous ${activation.coverageAmbiguous}` },
    { label:"原子发布", done:activation.generatedPair?.linked && activation.playerPair?.linked && activation.mirrorsMatch, detail:activation.publishReady ? "内容门禁已通过" : "等待内容门禁" },
    { label:"CharacterTest 加载", done:activation.ready, detail:activation.ready ? "GeneratedLinked" : "尚未激活" }
  ];
  const blockers = activation.blockers || [];
  const firstIncomplete = steps.findIndex(item => !item.done);
  el.runtimeActivationPanel.className = `runtime-activation ${activation.ready ? "ready" : activation.publishReady ? "publish-ready" : "blocked"}`;
  el.runtimeActivationPanel.innerHTML = `<header><div><p class="eyebrow">Phase 17 Runtime Activation</p><h2>${activation.ready ? "统一物理音频已激活" : activation.publishReady ? "已具备发布条件" : "统一物理音频尚未激活"}</h2><p>${activation.ready ? "Generated 与 Player Catalog pair 已联接，CharacterTest 可使用统一 resolver。" : `当前 ${blockers.length} 个技术阻断项；可选试听状态不参与发布门禁。`}</p></div><div class="runtime-activation-actions"><span class="activation-state">${activation.ready ? "GeneratedLinked" : activation.publishReady ? "PublishReady" : "Blocked"}</span><button type="button" data-activation-copy>复制诊断</button></div></header>
    <ol class="activation-steps">${steps.map((step,index)=>`<li class="${step.done ? "done" : index === firstIncomplete ? "current" : "locked"}"><span>${step.done ? "✓" : index + 1}</span><div><strong>${esc(step.label)}</strong><small>${esc(step.detail)}</small></div></li>`).join("")}</ol>
    ${blockers.length ? `<details class="activation-blockers"><summary>阻断项 ${blockers.length}</summary><div>${blockers.map(item=>`<article><div><strong>${esc(item.code || "AUTH_AUDIO_ACTIVATION_BLOCKED")}</strong><span>${esc(item.stage || "Readiness")}</span></div>${item.subjectId ? `<code>${esc(item.subjectId)}</code>` : ""}<p>${esc(item.message || "")}</p>${item.action ? `<small>下一步：${esc(item.action)}</small>` : ""}${findCue(item.subjectId) ? `<button type="button" data-activation-cue="${esc(item.subjectId)}">打开 Cue</button>` : ""}</article>`).join("")}</div></details>` : `<p class="notice ok">所有 Authoring、Catalog 与 mirror 门禁均已通过。</p>`}`;
  el.runtimeActivationPanel.querySelector("[data-activation-copy]")?.addEventListener("click", copyRuntimeActivationDiagnostics);
  el.runtimeActivationPanel.querySelectorAll("[data-activation-cue]").forEach(button => button.addEventListener("click", () => {
    state.appScreen = "cues";
    state.workspaceMode = "cues";
    selectCue(button.dataset.activationCue);
  }));
  updateRuntimeSyncControls();
}

async function copyRuntimeActivationDiagnostics() {
  const activation = state.payload?.runtimeActivation;
  if (!activation) return;
  const lines = [
    `Audio Runtime Activation: ${activation.ready ? "GeneratedLinked" : activation.publishReady ? "PublishReady" : "Blocked"}`,
    `Active Cue Ready: ${activation.readyCueCount}/${activation.activeCueCount}`,
    `Active Basis Adopted: ${activation.adoptedBasisCount}/${activation.activeBasisCount}`,
    `Active Basis Approved: ${activation.approvedBasisCount}/${activation.activeBasisCount}`,
    `Coverage: required=${activation.coverageRequired} missing=${activation.coverageMissing} ambiguous=${activation.coverageAmbiguous}`,
    `Catalog: generatedLinked=${Boolean(activation.generatedPair?.linked)} playerLinked=${Boolean(activation.playerPair?.linked)} mirrorsMatch=${Boolean(activation.mirrorsMatch)}`,
    ...(activation.blockers || []).map(item => `[${item.stage}] ${item.code}${item.subjectId ? ` ${item.subjectId}` : ""}: ${item.message} -> ${item.action}`)
  ];
  try {
    const copied = await copyTextToClipboard(lines.join("\n"));
    if (!copied) throw new Error("clipboard unavailable");
    setServer("已复制运行时激活诊断", "ok");
  } catch (_) {
    setServer("浏览器未允许复制，请展开阻断项手动复制。", "error");
  }
}

async function copyTextToClipboard(text) {
  if (navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch (_) {}
  }
  const textarea = document.createElement("textarea");
  textarea.value = text;
  textarea.setAttribute("readonly", "");
  textarea.style.position = "fixed";
  textarea.style.opacity = "0";
  document.body.appendChild(textarea);
  textarea.select();
  const copied = document.execCommand("copy");
  textarea.remove();
  return copied;
}

const CUE_CATEGORIES = [
  { id: "ui", label: "UI 音效" },
  { id: "gameplay", label: "玩法音效" },
  { id: "physical", label: "物理音效" },
  { id: "music", label: "音乐" },
  { id: "ambience", label: "环境音效" },
  { id: "voice", label: "语音" },
  { id: "demo", label: "Demo" },
  { id: "other", label: "其他" }
];

function cueCategoryId(cue) {
  const eventDef = findEvent(cue?.eventDefinitionId);
  const tags = new Set((cue?.tags || []).map(tag => String(tag).toLowerCase()));
  const cueId = String(cue?.audioCueId || "").toLowerCase();
  const busId = String(eventDef?.busId || "").toLowerCase();
  const eventPath = String(eventDef?.fmodEventPath || "").toLowerCase();
  if (cueId.startsWith("ui.") || tags.has("ui") || busId === "audio.bus.ui" || eventPath.includes("/ui/")) return "ui";
  if (cueId.includes(".physical.") || tags.has("physical") || eventPath.includes("/physicalaudio/") || eventPath.includes("/generated/physical/")) return "physical";
  if (tags.has("music") || busId === "audio.bus.music" || eventPath.includes("/music/")) return "music";
  if (tags.has("ambience") || tags.has("ambient") || busId === "audio.bus.ambience" || eventPath.includes("/ambience/")) return "ambience";
  if (tags.has("voice") || busId === "audio.bus.voice" || eventPath.includes("/voice/")) return "voice";
  if (tags.has("demo") || eventPath.includes("/demo/")) return "demo";
  if (cueId.startsWith("sfx.") || tags.has("gameplay") || tags.has("action") || busId === "audio.bus.sfx" || eventPath.includes("/gameplay/") || eventPath.includes("/charactertest/")) return "gameplay";
  return "other";
}

function renderCueList() {
  el.libraryTitle.textContent = "Cue 库";
  el.newCueButton.hidden = false;
  el.searchInput.placeholder = "搜索 Cue / 标签";
  el.categoryFilter.hidden = false;
  el.syncFilter.hidden = false;
  if (!CUE_CATEGORIES.some(category => category.id === state.categoryFilter)) state.categoryFilter = "all";
  el.categoryFilter.innerHTML = `<option value="all">全部分类</option>${CUE_CATEGORIES.map(category => `<option value="${category.id}">${category.label}</option>`).join("")}`;
  el.categoryFilter.value = state.categoryFilter;
  if (!["all","Draft","Active","Archived"].includes(state.statusFilter)) state.statusFilter = "all";
  el.statusFilter.innerHTML = `<option value="all">全部状态</option><option>Draft</option><option>Active</option><option>Archived</option>`;
  el.statusFilter.value = state.statusFilter;
  const syncOptions = [
    ["all","全部同步状态"],
    ["Synced","已同步游戏"],
    ["FmodApplied","已应用 FMOD"],
    ["BankOutOfSync","Bank 未同步"],
    ["CandidateOnly","仅候选"],
    ["SilentPlaceholder","静音占位"]
  ];
  if (!syncOptions.some(option => option[0] === state.syncFilter)) state.syncFilter = "all";
  el.syncFilter.innerHTML = syncOptions.map(option => `<option value="${option[0]}">${option[1]}</option>`).join("");
  el.syncFilter.value = state.syncFilter;
  const cues = (state.manifest?.cues || []).filter(cue => {
    const text = `${cue.audioCueId} ${cue.displayName} ${(cue.tags || []).join(" ")}`.toLowerCase();
    const sync = cueRuntimeSync(cue.audioCueId);
    return (!state.search || text.includes(state.search.toLowerCase()))
      && (state.categoryFilter === "all" || cueCategoryId(cue) === state.categoryFilter)
      && (state.statusFilter === "all" || cue.lifecycle === state.statusFilter)
      && (state.syncFilter === "all" || sync?.syncStatus === state.syncFilter);
  });
  el.cueSummary.textContent = `${cues.length} / ${state.manifest?.cues?.length || 0} 个 Cue`;
  el.cueList.innerHTML = cues.length ? CUE_CATEGORIES.map(category => {
    const categoryCues = cues.filter(cue => cueCategoryId(cue) === category.id);
    if (!categoryCues.length) return "";
    const rows = categoryCues.map(cue => {
      const status = cueStatus(cue.audioCueId);
      const sync = cueRuntimeSync(cue.audioCueId);
      return `<button class="cue-row ${cue.audioCueId === state.selectedCueId ? "active" : ""}" type="button" data-cue-id="${esc(cue.audioCueId)}">
        <strong>${esc(cue.displayName || cue.audioCueId)}</strong><small>${esc(cue.audioCueId)}</small><span class="meta">${badge(cue.lifecycle || "Draft", "")}${badge(status, tone(status))}${badge(sync?.syncLabel || "未跟踪", syncTone(sync?.syncStatus))}</span></button>`;
    }).join("");
    return `<section class="cue-group" data-cue-category="${category.id}" aria-label="${category.label}">
      <h3 class="cue-group-title"><span>${category.label}</span><small>${categoryCues.length}</small></h3>${rows}</section>`;
  }).join("") : empty("没有匹配的 Cue");
  el.cueList.querySelectorAll("[data-cue-id]").forEach(button => button.addEventListener("click", () => selectCue(button.dataset.cueId)));
}

function renderInspector() {
  if (state.workspaceMode === "pairs") return;
  const inspectorHead=document.querySelector(".next-step-card .panel-head");
  if(inspectorHead){inspectorHead.querySelector(".eyebrow").textContent="当前 Cue";inspectorHead.querySelector("h2").textContent="下一步";}
  const cue = selectedCue();
  if (!cue) {
    el.diffPanel.innerHTML = el.diagnosticsPanel.innerHTML = el.auditionPanel.innerHTML = el.runtimePanel.innerHTML = el.nextStepPanel.innerHTML = empty("未选择 Cue");
    el.editorWaveformPanel.innerHTML = `<div class="waveform-preview">${empty("选择 Cue 后显示当前采用素材波形")}</div>`;
    return;
  }
  const dirty = isDirty();
  const eventDef = findEvent(cue.eventDefinitionId);
  el.diffPanel.innerHTML = `<div class="kv"><div><span>manifest</span><strong>${dirty ? "有未保存改动" : "与磁盘一致"}</strong></div><div><span>当前 Cue</span><strong>${cueStatus(cue.audioCueId)}</strong></div><div><span>Event 共享</span><strong>${state.manifest.cues.filter(item => item.eventDefinitionId === cue.eventDefinitionId).length} 个 Cue</strong></div></div>`;
  const diagnostics = [
    ...(state.payload?.validation?.diagnostics || []),
    ...(state.payload?.audioPhysical?.validation?.diagnostics || [])
  ].filter(item => !item.runtimeResourceKey || item.runtimeResourceKey === cue.audioCueId || item.runtimeResourceKey === cue.eventDefinitionId || item.runtimeResourceKey === currentBasisForCue(cue)?.id);
  el.referenceSummary.textContent = `${referenceCount(cue.audioCueId)} 个引用 · ${diagnostics.length} 条诊断`;
  el.diagnosticsPanel.innerHTML = diagnostics.length ? renderDiagnosticsGrouped(diagnostics) : `<p class="notice ok">当前 Cue 没有相关诊断。</p>`;
  const evidence = cue.auditionEvidence || {};
  const previewReady = state.preview?.status === "succeeded" && state.preview?.audioCueId === cue.audioCueId;
  const eventState = (state.generationManifest?.eventStates || []).find(item => item.eventDefinitionId === cue.eventDefinitionId);
  const activeSet = (state.generationManifest?.adoptionSets || []).find(item => item.setId === eventState?.activeSetId);
  const basis = currentBasisForCue(cue);
  const runtimeSync = cueRuntimeSync(cue.audioCueId);
  const runtimeSynced = ["Synced", "SilentPlaceholder"].includes(runtimeSync?.syncStatus);
  const usesPhysicalWorkflow = Boolean(basis);
  const applied = activeSet?.status === "Applied";
  const basisReady = Boolean(basis && activeSet && sameId(basis.adoptionSetId,activeSet.setId));
  const coverageReady = basisReady && !state.physicalCoverageStale && !numberValue(state.physicalCoverage?.missingCellCount) && !numberValue(state.physicalCoverage?.ambiguousCellCount);
  const currentFmodEvent = (state.snapshot?.events || []).find(item =>
    sameId(item.path, eventDef?.fmodEventPath)
    || sameId(item.guid, eventDef?.fmodEventGuid));
  const approved = hasCurrentOptionalReview(cue);
  const steps = usesPhysicalWorkflow ? [
    { title: "应用到 FMOD", detail: applied ? `${activeSet.sources?.length || 0} 个素材 · ${String(activeSet.setId || "").slice(0, 14)}` : "先在候选页保存并应用替换", done: applied, action: applied ? "" : "candidates" },
    { title: "提升为基础音效", detail: basisReady ? `${basis.id} · 当前素材版本` : "Basis 已存在，但素材版本需要同步", done: basisReady, action: applied && !basisReady ? "basis" : "" },
    { title: "公共 Pair 覆盖", detail: coverageReady ? "Required Pair × Variant 已覆盖" : "在全局公共矩阵校验 Required、fallback 与歧义", done: coverageReady, action: basis && !coverageReady ? "pair-matrix" : "" },
    { title: "激活 Cue", detail: cue.lifecycle === "Active" ? "已进入运行时发布范围" : "设为 Active 并保存", done: cue.lifecycle === "Active", action: coverageReady && cue.lifecycle !== "Active" ? "activate" : "" },
    { title: "同步到游戏", detail: runtimeSync?.syncDetail || "写入 Generated 与 Player Catalog", done: runtimeSynced, action: coverageReady && cue.lifecycle === "Active" && !runtimeSynced ? "publish" : "" }
  ] : [
    { title: "沿用当前 FMOD Event", detail: currentFmodEvent ? `${currentFmodEvent.path} · 原始音频` : "当前 Bank 中找不到绑定 Event", done: Boolean(currentFmodEvent), action: "" },
    { title: "激活 Cue", detail: cue.lifecycle === "Active" ? "已进入运行时发布范围" : "设为 Active 并保存", done: cue.lifecycle === "Active", action: currentFmodEvent && cue.lifecycle !== "Active" ? "activate" : "" },
    { title: "同步到游戏", detail: runtimeSync?.syncDetail || "写入 Generated 与 Player Catalog", done: runtimeSynced, action: currentFmodEvent && cue.lifecycle === "Active" && !runtimeSynced ? "publish" : "" }
  ];
  const nextStepIndex = Math.max(0, steps.findIndex(step => !step.done));
  el.nextStepPanel.innerHTML = `<ol class="next-steps">${steps.map((step,index) => `<li class="${step.done ? "done" : index === nextStepIndex ? "current" : "locked"}"><span class="step-marker">${step.done ? "✓" : index + 1}</span><div><strong>${step.title}</strong><small>${esc(step.detail)}</small>${step.action ? `<button type="button" ${step.action === "publish" ? "data-next-publish" : step.action === "activate" ? "data-next-activate" : step.action === "pair-matrix" ? "data-next-pair-matrix" : ["candidates","basis"].includes(step.action) ? `data-next-workspace="${step.action}"` : `data-audition="${step.action}"`}>${step.action === "preview" ? "开始试听" : step.action === "approve" ? "批准当前版本" : step.action === "publish" ? "发布到运行时" : step.action === "activate" ? "设为 Active" : step.action === "basis" ? "提升 Basis" : step.action === "pair-matrix" ? "打开公共矩阵" : "前往候选与混合"}</button>` : ""}</div></li>`).join("")}</ol>`;
  el.auditionPanel.innerHTML = `<div class="kv"><div><span>状态</span><strong>${esc(evidence.status || "Needed")}</strong></div><div><span>Preview request</span><strong><code>${esc(evidence.previewRequestId || "—")}</code></strong></div><div><span>当前版本</span><strong><code>${esc(String(evidence.appliedSetId || activeSet?.setId || "—").slice(0, 14))}</code></strong></div></div>
    <div class="audition-actions"><button type="button" data-audition="preview">试听 FMOD${state.audioAnalysisMode === "fmod" ? " <kbd>Space</kbd>" : ""}</button><button type="button" data-audition="stop">停止</button><button type="button" data-audition="reject"${previewReady ? "" : " disabled"}>拒绝</button></div>
    <p class="notice ${state.preview?.status === "failed" ? "error" : state.preview?.status === "succeeded" ? "ok" : ""}" style="margin-top:8px">${esc(state.preview?.message || "可选质量流程：试听成功后可记录批准或拒绝；Event 绑定变化会使旧记录失效。")}</p>
    <p class="waveform-disclaimer">试听和批准只记录主观质量，不影响 Cue 激活、Catalog 生成或同步到游戏。</p>`;
  document.querySelectorAll("[data-audition]").forEach(button => button.addEventListener("click", () => auditionAction(button.dataset.audition)));
  el.nextStepPanel.querySelectorAll("[data-next-workspace]").forEach(button => button.addEventListener("click", () => setWorkspaceTab(button.dataset.nextWorkspace)));
  el.nextStepPanel.querySelectorAll("[data-next-pair-matrix]").forEach(button => button.addEventListener("click", () => locateBasisPair(basis)));
  el.nextStepPanel.querySelectorAll("[data-next-activate]").forEach(button => button.addEventListener("click", () => {
    cue.lifecycle = "Active";
    updateSaveIndicator();
    render();
  }));
  el.nextStepPanel.querySelectorAll("[data-next-publish]").forEach(button => button.addEventListener("click", publishRuntime));
  renderWaveformPreview(cue);
  const generated = state.payload?.generatedCatalog || {};
  const mirror = state.payload?.playerMirror || {};
  const physicalGenerated = state.payload?.audioPhysical?.generatedCatalog || {};
  const physicalMirror = state.payload?.audioPhysical?.playerMirror || {};
  el.runtimePanel.innerHTML = `<div class="kv"><div><span>游戏同步状态</span><strong>${esc(runtimeSync?.syncLabel || "未跟踪")}</strong></div><div><span>同步说明</span><strong>${esc(runtimeSync?.syncDetail || "尚无同步证据")}</strong></div><div><span>Global Audio Catalog</span><strong>${generated.exists ? "已生成" : "缺失"}</strong></div><div><span>Audio Player mirror</span><strong>${mirror.exists ? "已同步" : "缺失"}</strong></div><div><span>Physical Catalog</span><strong>${physicalGenerated.exists ? "已生成" : "缺失"}</strong></div><div><span>Physical Player mirror</span><strong>${physicalMirror.exists ? "已同步" : "缺失"}</strong></div><div><span>Basis</span><strong>${basis ? `${esc(basis.id)} · #${basis.runtimeBasisId}` : "尚未提升"}</strong></div><div><span>当前 Event ID</span><strong>${eventDef?.runtimeEventId || "—"}</strong></div><div><span>采用规则</span><strong>${cue.lifecycle === "Active" ? "同步后进入游戏目录" : "不发布"}</strong></div></div>`;
}

function renderDiagnosticsGrouped(diagnostics) {
  const groups = [
    { title: "阻止发布", items: diagnostics.filter(item => severity(item) === "Error") },
    { title: "需要处理", items: diagnostics.filter(item => severity(item) === "Warning") },
    { title: "信息", items: diagnostics.filter(item => !["Error", "Warning"].includes(severity(item))) }
  ];
  return groups.filter(group => group.items.length).map(group => `<section class="diagnostic-group"><h3>${group.title} · ${group.items.length}</h3>${group.items.map(item => `<div class="diagnostic ${severity(item).toLowerCase()}"><strong>${esc(item.code || "diagnostic")}</strong><div>${esc(item.message || "")}</div>${item.suggestedFix ? `<small>下一步：${esc(item.suggestedFix)}</small>` : ""}</div>`).join("")}</section>`).join("");
}

function field(label, name, value, type = "text", options = [], wide = false) {
  const control = type === "textarea" ? `<textarea name="${name}">${esc(value ?? "")}</textarea>` : type === "select" ? `<select name="${name}">${options.map(item => `<option${item === value ? " selected" : ""}>${esc(item)}</option>`).join("")}</select>` : `<input name="${name}" type="${type}" value="${esc(value ?? "")}">`;
  return `<label class="field ${wide ? "wide" : ""}"><span>${esc(label)}</span>${control}</label>`;
}
function checkbox(label, name, checked) { return `<label class="field"><span>${esc(label)}</span><input name="${name}" type="checkbox"${checked ? " checked" : ""}></label>`; }

function handleAudioKeyboardShortcut(event) {
  if (event.code !== "Space" || event.repeat || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
  if(state.workspaceMode==="pairs"){
    if(event.target instanceof Element&&event.target.closest('input, textarea, select, a, [contenteditable="true"]'))return;
    const context=selectedMaterialPairContext(),basis=context&&pairBasisSounds(context.pair,state.selectedPairVariant)[0];
    if(!basis)return;
    event.preventDefault();previewPairSelection();return;
  }
  if(isAudioShortcutInput(event.target))return;
  const cue = selectedCue();
  if (!cue || !findEvent(cue.eventDefinitionId)) return;
  event.preventDefault();
  if((state.appScreen === "cues" || state.appScreen === "candidates") && state.audioAnalysisMode !== "fmod"){
    toggleAudioAnalysisPlayback(cue);
    return;
  }
  auditionAction("preview");
}

function isAudioShortcutInput(target) {
  return target instanceof Element && Boolean(target.closest('input, textarea, select, button, a, [contenteditable="true"]'));
}

function updateDraftFromForm(event) {
  const cue = selectedCue(); if (!cue || !event.target.name) return;
  const value = event.target.type === "checkbox" ? event.target.checked : event.target.value;
  const name = event.target.name;
  if (name === "tags") cue.tags = csv(value);
  else if (name.startsWith("replacement.")) {
    cue.replacementContract ||= replacementContract();
    const key = name.slice("replacement.".length);
    cue.replacementContract[key] = key === "requiredParameters" ? csv(value) : key.endsWith("Ms") ? Number(value || 0) : value;
  } else cue[name] = value;
  if (name === "eventDefinitionId") cue.auditionEvidence = { status: "Needed" };
  updateSaveIndicator();
  if (name === "displayName") el.editorTitle.textContent = cue.displayName || cue.audioCueId;
  if (event.type !== "change") return;

  renderCueList();
  renderInspector();
  if (name === "lifecycle") renderStatus();
  if (name === "eventDefinitionId" || name === "lifecycle") renderEditor({ force: true });
}

function isCueFormEditing() {
  const active = document.activeElement;
  return active instanceof Element
    && el.cueForm.contains(active)
    && active.matches('input, textarea, select, [contenteditable="true"]');
}

function flushPendingEditorRender() {
  setTimeout(() => {
    if (state.editorRenderPending && !isCueFormEditing()) renderEditor({ force: true });
  }, 0);
}

async function handleFormAction(event) {
  const action = event.target.closest("[data-action]")?.dataset.action;
  if (action !== "refresh-snapshot") return;
  try { await api("/api/authoring/audio/refresh-fmod-snapshot", { method: "POST", body: "{}" }); setServer("已请求 Unity 刷新 FMOD snapshot", "ok"); }
  catch (error) { setServer(error.message, "error"); }
}

function createCue() {
  const id = state.manifest.idAllocation.nextCueId++;
  const cue = { audioCueId: `audio.cue.new.${id}`, runtimeCueId: id, displayName: "新 Audio Cue", purpose: "", context: "", tags: [], sourcePreviewPath: "", eventDefinitionId: state.manifest.eventDefinitions[0]?.id || "", playMode: "OneShot", missingPolicy: "Mute", defaultParameters: [], replacementContract: replacementContract(), auditionEvidence: { status: "Needed" }, lifecycle: "Draft", metadata: {} };
  state.manifest.cues.push(cue); selectCue(cue.audioCueId);
}
function duplicateCue() {
  const source = selectedCue(); if (!source) return;
  const cue = structuredClone(source); const id = state.manifest.idAllocation.nextCueId++;
  cue.audioCueId = `${source.audioCueId}.copy.${id}`; cue.runtimeCueId = id; cue.displayName = `${source.displayName} Copy`; cue.lifecycle = "Draft"; cue.auditionEvidence = { status: "Needed" };
  state.manifest.cues.push(cue); selectCue(cue.audioCueId);
}
function toggleArchive() { const cue = selectedCue(); if (!cue) return; cue.lifecycle = cue.lifecycle === "Archived" ? "Draft" : "Archived"; render(); }
function deleteCue() {
  const cue = selectedCue(); if (!cue || cue.lifecycle !== "Archived" || referenceCount(cue.audioCueId) > 0) return;
  state.manifest.idAllocation.retiredCueIds ||= []; state.manifest.idAllocation.retiredCueIds.push(cue.runtimeCueId);
  state.manifest.cues = state.manifest.cues.filter(item => item !== cue); state.selectedCueId = state.manifest.cues[0]?.audioCueId || ""; render();
}

async function saveDraft() {
  try {
    setServer("正在保存 Authoring manifest…", "");
    const result = await api("/api/authoring/audio/save", { method: "POST", body: JSON.stringify({
      package: state.package,
      expectedHash: state.contentHash,
      manifest: state.manifest,
      expectedGenerationHash: state.generationManifestHash,
      generationManifest: state.generationManifest,
      expectedPhysicalHash: state.physicalManifestHash,
      physicalManifest: state.physicalManifest
    }) });
    state.manifest = result.manifest;
    state.contentHash = result.currentHash;
    state.savedManifestText = stableText(state.manifest);
    state.generationManifestHash = result.generationManifestHash || state.generationManifestHash;
    state.physicalManifestHash = result.physicalManifestHash || state.physicalManifestHash;
    setServer(result.message, "ok");
    await loadState();
  } catch (error) { setServer(error.message, "error"); }
}
async function publishRuntime() {
  if (state.runtimeSyncBusy) return;
  if (isDirty()) {
    const message = "请先保存草稿，再同步到游戏。";
    setServer(message, "error");
    showActionFeedback("error", "还不能同步", message);
    return;
  }
  state.runtimeSyncBusy = true;
  updateRuntimeSyncControls();
  showActionFeedback("busy", "正在同步到游戏", "正在核对当前 FMOD Bank，并更新游戏运行时目录。");
  try {
    setServer("正在同步当前 FMOD 版本、Bank 与运行时目录…", "");
    const result = await api("/api/authoring/audio/publish", { method: "POST", body: JSON.stringify({ package: state.package, expectedHash: state.contentHash, expectedGenerationHash: state.generationManifestHash, expectedPhysicalHash: state.physicalManifestHash, developmentPublishAll: true }) });
    await loadState();
    const runtimeSync = state.payload?.runtimeSync || {};
    const hasPending = numberValue(runtimeSync.pendingCount) > 0 || numberValue(result.outOfSyncSetCount) > 0;
    const metrics = [
      ["有声已同步", numberValue(runtimeSync.syncedCount)],
      ["静音占位", numberValue(runtimeSync.silentPlaceholderCount)],
      ["仍待同步", numberValue(runtimeSync.pendingCount)]
    ];
    const title = hasPending ? "同步完成，但仍有待处理项" : "已同步到游戏";
    setServer(title, hasPending ? "error" : "ok");
    showActionFeedback(hasPending ? "error" : "ok", title, result.message, metrics, hasPending);
  }
  catch (error) {
    const detail = error.detail ? `${error.message}（${error.detail}）` : error.message;
    setServer("同步失败", "error");
    showActionFeedback("error", "同步失败", detail || "未能更新游戏运行时目录。", [], true);
  }
  finally {
    state.runtimeSyncBusy = false;
    updateRuntimeSyncControls();
  }
}
async function approveAll() {
  if (isDirty()) { setServer("请先保存当前草稿，再批量记录质量确认。", "error"); return; }
  try {
    setServer("正在批量记录可选质量确认…", "");
    const result = await api("/api/authoring/audio/bulk-approve", {
      method: "POST",
      body: JSON.stringify({ package: state.package, expectedHash: state.contentHash })
    });
    setServer(result.message, "ok");
    await loadState();
  } catch (error) { setServer(error.message, "error"); }
}
async function directAdoptCurrentAudio() {
  if (isDirty()) { setServer("请先保存当前草稿，再直接采用。", "error"); return; }
  try {
    setServer("正在登记并采用当前 FMOD Event…", "");
    const result = await api("/api/authoring/audio/direct-adopt-current", {
      method: "POST",
      body: JSON.stringify({
        package: state.package,
        expectedHash: state.contentHash,
        expectedGenerationHash: state.generationManifestHash,
        expectedPhysicalHash: state.physicalManifestHash
      })
    });
    setServer(result.message, "ok");
    await loadState();
    await publishRuntime();
  } catch (error) { setServer(error.message, "error"); }
}

async function auditionAction(action) {
  const cue = selectedCue(); const eventDef = cue && findEvent(cue.eventDefinitionId); if (!cue || !eventDef) return;
  if (action === "approve" || action === "reject") {
    if (state.preview?.status !== "succeeded" || state.preview.audioCueId !== cue.audioCueId) return;
    const eventState=(state.generationManifest?.eventStates||[]).find(item=>item.eventDefinitionId===cue.eventDefinitionId),appliedSet=(state.generationManifest?.adoptionSets||[]).find(item=>item.setId===eventState?.activeSetId);
    const basis = currentBasisForCue(cue);
    cue.auditionEvidence = { status: action === "approve" ? "Approved" : "Rejected", previewRequestId: state.preview.requestId, eventBindingHash: state.preview.eventBindingHash, snapshotHash: state.preview.snapshotHash, appliedSetId:eventState?.activeSetId||"",bankHash:appliedSet?.bankHash||"",audioBasisId:basis?.id||"",runtimeBasisId:basis?.runtimeBasisId||0,physicalManifestHash:state.physicalManifestHash,reviewedAtUtc: new Date().toISOString() };
    if (basis) basis.auditionBindingHash = state.preview.eventBindingHash || "";
    render(); return;
  }
  const endpoint = action === "stop" ? "/api/authoring/audio/stop-preview" : "/api/authoring/audio/preview";
  try {
    const eventBindingHash = await computeBindingHash(cue, eventDef);
    const result = await api(endpoint, { method: "POST", body: JSON.stringify({ package: state.package, audioCueId: cue.audioCueId, fmodEventPath: eventDef.fmodEventPath, fmodEventGuid: eventDef.fmodEventGuid, parameters: Object.fromEntries((cue.defaultParameters || []).map(item => [item.name, item.value])), position: { x: 0, y: 1, z: 2 }, eventBindingHash }) });
    state.preview = { ...result, audioCueId: cue.audioCueId }; renderInspector(); if (action !== "stop") pollPreview(result.requestId, cue.audioCueId);
  } catch (error) { state.preview = { status: "failed", message: error.message, audioCueId: cue.audioCueId }; renderInspector(); }
}
async function pollPreview(requestId, audioCueId) {
  clearTimeout(state.previewPoll);
  try {
    const result = await api(`/api/authoring/audio/preview-status?id=${encodeURIComponent(requestId)}`);
    state.preview = { ...result, audioCueId }; renderInspector();
    if (result.status === "pending") state.previewPoll = setTimeout(() => pollPreview(requestId, audioCueId), 500);
  } catch (error) { state.preview = { status: "failed", message: error.message, audioCueId }; renderInspector(); }
}

async function selectCue(id) { stopCandidateMix(); stopAudioAnalysisPlayback(); state.selectedCueId = id; restoreLocalSession(id); await restoreGenerationShelf(id); const url = new URL(location.href); url.searchParams.set("cue", id); url.searchParams.set("package", state.package); history.replaceState(null, "", url); render(); }
function selectedCue() { return findCue(state.selectedCueId); }
function findCue(id) { return state.manifest?.cues?.find(item => item.audioCueId === id); }
function findEvent(id) { return state.manifest?.eventDefinitions?.find(item => item.id === id); }
function cueState(id) { return state.payload?.cueStates?.find(item => item.audioCueId === id); }
function cueRuntimeSync(id) { return cueState(id); }
function cueStatus(id) { return cueState(id)?.status || findCue(id)?.lifecycle || "Draft"; }
function referenceCount(id) { return Number(cueState(id)?.referenceCount || 0); }
function bindingHash(id) { return cueState(id)?.bindingHash || ""; }
function hasCurrentOptionalReview(cue) {
  if (!cue) return false;
  const evidence = cue.auditionEvidence || {};
  const eventState = (state.generationManifest?.eventStates || []).find(item => item.eventDefinitionId === cue.eventDefinitionId);
  const activeSet = (state.generationManifest?.adoptionSets || []).find(item => item.setId === eventState?.activeSetId);
  const basis = currentBasisForCue(cue);
  return evidence.status === "Approved"
    && sameId(evidence.eventBindingHash, bindingHash(cue.audioCueId))
    && sameId(evidence.snapshotHash, state.payload?.snapshotHash)
    && (!activeSet || evidence.appliedSetId === activeSet.setId)
    && (!basis || evidence.audioBasisId === basis.id)
    && (!basis || evidence.appliedSetId === basis.adoptionSetId);
}
async function computeBindingHash(cue, eventDef) {
  const parts = [cue.audioCueId, cue.eventDefinitionId, eventDef.fmodEventPath, eventDef.fmodEventGuid, eventDef.is3D ? "True" : "False", eventDef.isLoop ? "True" : "False", String(eventDef.minDistance || 0), String(eventDef.maxDistance || 0), ...(eventDef.parameters || []).map(item => item.name || "")];
  const bytes = new TextEncoder().encode(parts.join("|"));
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  const hex = [...new Uint8Array(digest)].map(value => value.toString(16).padStart(2, "0")).join("");
  return `sha256:${hex}`;
}
function tone(status) { return status === "Ready" ? "ok" : status === "Rejected" ? "error" : status === "ReviewNeeded" ? "warn" : "info"; }
function syncTone(status) { return status === "Synced" ? "ok" : status === "SilentPlaceholder" || status === "FmodApplied" ? "warn" : status === "BankOutOfSync" ? "error" : "info"; }
function severity(item) { return String(item?.severity || item?.Severity || "Info"); }
function replacementContract() { return { requireSameSpatial: true, requireSameLoop: true, minLengthMs: 0, maxLengthMs: 0, requiredParameters: [], requiredBanks: ["Master"] }; }
function normalizeManifest() { state.manifest.idAllocation ||= { nextCueId: 500001, nextEventId: 500001, nextBusId: 500001, nextParameterId: 500001, retiredCueIds: [], retiredEventIds: [], retiredBusIds: [], retiredParameterIds: [] }; state.manifest.cues ||= []; state.manifest.eventDefinitions ||= []; state.manifest.buses ||= []; }
function createManifest() { return { schemaVersion: 2, manifestId: "mxframework.audio.semantic", idAllocation: {}, buses: [], eventDefinitions: [], cues: [], metadata: {} }; }
function csv(value) { return String(value || "").split(",").map(item => item.trim()).filter(Boolean); }
function stableText(value) { return JSON.stringify(value); }
function isSemanticDirty() { return Boolean(state.manifest) && stableText(state.manifest) !== state.savedManifestText; }
function isRecipeDirty() { return Boolean(state.generationManifest) && stableText(state.generationManifest) !== state.savedGenerationManifestText; }
function isPhysicalDirty() { return Boolean(state.physicalManifest) && stableText(state.physicalManifest) !== state.savedPhysicalManifestText; }
function isDirty() { return isSemanticDirty() || isRecipeDirty() || isPhysicalDirty(); }
function createGenerationManifest() { return { format:"mx.audioGenerationManifest.v3", schemaVersion:"3.0", updatedAtUtc:"", recipes:[], entries:[], adoptionSets:[], eventStates:[] }; }
function createPhysicalManifest() { return { format:"mx.audioPhysicalAuthoringManifest.v2", schemaVersion:"2.0", idAllocation:{ nextMaterialId:510001,nextMaterialPairId:550001,nextFamilyId:520001,nextBasisId:530001,nextResponseProfileId:540001,retiredMaterialIds:[],retiredMaterialPairIds:[],retiredFamilyIds:[],retiredBasisIds:[],retiredResponseProfileIds:[] }, materialProfiles:[], materialPairs:[], families:[], basisSounds:[], responseProfiles:[], scenarioPresets:[], coverageRequirements:[] }; }
function normalizePhysicalManifest() {
  state.physicalManifest ||= createPhysicalManifest();
  state.physicalManifest.format ||= "mx.audioPhysicalAuthoringManifest.v2";
  state.physicalManifest.schemaVersion ||= "2.0";
  state.physicalManifest.idAllocation ||= {};
  state.physicalManifest.idAllocation.nextMaterialPairId ||= 550001;
  state.physicalManifest.idAllocation.retiredMaterialPairIds ||= [];
  state.physicalManifest.materialProfiles ||= [];
  state.physicalManifest.materialPairs ||= [];
  state.physicalManifest.families ||= [];
  state.physicalManifest.basisSounds ||= [];
  state.physicalManifest.responseProfiles ||= [];
  state.physicalManifest.scenarioPresets ||= [];
  state.physicalManifest.coverageRequirements ||= [];
}
function normalizeGenerationManifest() { state.generationManifest ||= createGenerationManifest(); state.generationManifest.format="mx.audioGenerationManifest.v3"; state.generationManifest.schemaVersion="3.0"; state.generationManifest.recipes ||= []; state.generationManifest.entries ||= []; state.generationManifest.adoptionSets ||= [];state.generationManifest.eventStates ||= []; state.generationManifest.recipes.forEach(recipe=>{normalizeCollisionRecipe(recipe,findCue(recipe.audioCueId));normalizeRecipeCounts(recipe);}); }
function normalizeRecipeCounts(recipe) { if(!recipe)return; recipe.layers ||= [];recipe.durationMode ||= "LegacyFixed";recipe.durationCharacter ||= "Auto";recipe.decayProfile ||= "Natural";recipe.maxDurationSeconds=Number(recipe.maxDurationSeconds||recipe.targetDurationSeconds||.8); recipe.generationProfile ||= "Standard"; recipe.layerTemplate ||= inferLayerTemplate(recipe); const enabled=recipe.layerTemplate==="BodyOnly"?["body"]:recipe.layerTemplate==="TransientBody"?["transient","body"]:["transient","body","tail"]; for(const layer of recipe.layers){layer.enabled=enabled.includes(layer.name);layer.gainDb=Math.max(-24,Math.min(6,Number(layer.gainDb||0)));layer.delayMs=Math.max(0,Math.min(250,Number(layer.delayMs||0)));layer.takes=Math.max(1,Math.min(12,Number(layer.takes||1)));layer.adoptionTakes=Math.max(1,Math.min(layer.takes,Number(layer.adoptionTakes||recipe.adoptionCountPerLayer||1)));layer.maxDurationSeconds=Number(layer.maxDurationSeconds||recipe.maxDurationSeconds);} recipe.variationCount=recipe.layers.filter(item=>item.enabled).reduce((sum,item)=>sum+item.takes,0); }
function generationTotal(recipe) { normalizeRecipeCounts(recipe); return (recipe?.layers||[]).filter(item=>item.enabled).reduce((sum,item)=>sum+item.takes,0); }
function adoptionTotal(recipe) { normalizeRecipeCounts(recipe); return (recipe?.layers||[]).filter(item=>item.enabled).reduce((sum,item)=>sum+item.adoptionTakes,0); }
function upsertRecipe(recipe) { normalizeGenerationManifest(); const index=state.generationManifest.recipes.findIndex(item=>item.audioCueId===recipe.audioCueId); if(index>=0) state.generationManifest.recipes[index]=recipe; else state.generationManifest.recipes.push(recipe); state.generationBriefs.set(recipe.audioCueId,recipe); updateSaveIndicator(); }
function savedRecipe(id) { try { return JSON.parse(state.savedGenerationManifestText || "{}").recipes?.find(item=>item.audioCueId===id); } catch { return null; } }
function adoptRecipeIdentity(recipe,cue,eventDef) { const previousKind=recipe.collisionProfile?.kind||"";recipe.audioCueId=cue.audioCueId;recipe.eventDefinitionId=eventDef.id;recipe.bindingHash=bindingHash(cue.audioCueId);const targetKind=detectCollisionKind(cue,recipe);if(!targetKind)recipe.collisionProfile=null;else if(!recipe.collisionProfile||previousKind!==targetKind)recipe.collisionProfile=semanticCollisionProfile(cue,recipe);recipe.revision=1;recipe.history=[];recipe.updatedAtUtc=new Date().toISOString();recipe.promptModified=true;recipe.derivationProvider="Copied";normalizeCollisionRecipe(recipe,cue);if(recipe.collisionProfile)state.collisionPromptStale.add(cue.audioCueId); }
function applyHistory(cue,item) { const recipe=state.generationBriefs.get(cue.audioCueId); if(!recipe||!item)return; recipe.intentZh=item.intentZh; recipe.positivePrompt=item.positivePrompt; recipe.negativePrompt=item.negativePrompt; recipe.durationMode=item.durationMode||"LegacyFixed";recipe.durationCharacter=item.durationCharacter||"Auto";recipe.decayProfile=item.decayProfile||"Natural";recipe.maxDurationSeconds=Number(item.maxDurationSeconds||recipe.targetDurationSeconds||0);recipe.targetDurationSeconds=recipe.maxDurationSeconds;recipe.collisionProfile=structuredClone(item.collisionProfile||recipe.collisionProfile); recipe.layers=structuredClone(item.layers||recipe.layers); recipe.generationProfile=item.generationProfile||"Custom"; normalizeCollisionRecipe(recipe,cue); normalizeRecipeCounts(recipe); recipe.promptModified=true; recipe.derivationProvider="HistoryRestore"; recipe.updatedAtUtc=new Date().toISOString(); }
function restoreRecipeHistory(revision) { const cue=selectedCue(); const item=state.generationBriefs.get(cue?.audioCueId)?.history?.find(entry=>entry.revision===revision); if(cue&&item){applyHistory(cue,item);renderEditor();} }
function recipeSelectAction(kind,value) { if(!value)return; const cue=selectedCue(),eventDef=cue&&findEvent(cue.eventDefinitionId),recipe=cue&&state.generationBriefs.get(cue.audioCueId); if(!cue||!eventDef||!recipe)return; if(kind==="reuse"){const source=state.generationManifest.recipes.find(item=>item.audioCueId===value);if(source){const copy=structuredClone(source);adoptRecipeIdentity(copy,cue,eventDef);upsertRecipe(copy);}} else applyRecipePreset(recipe,value); renderEditor(); }
function applyRecipePreset(recipe,name) { const presets={Footstep:{category:"Footstep",targetDurationSeconds:.4,maxDurationSeconds:.4,adoptionCountPerLayer:6,layers:[{name:"body",takes:12,adoptionTakes:6,maxDurationSeconds:.4,prompt:"material-specific footstep body, natural foley variation, dry close-mic"}]},Collision:{category:"ObjectCollision",targetDurationSeconds:.3,maxDurationSeconds:.3,adoptionCountPerLayer:4,layers:[{name:"body",takes:8,adoptionTakes:4,maxDurationSeconds:.3,prompt:"object collision transient and weighted material body, dry isolated"}]},Landing:{category:"Landing",targetDurationSeconds:.55,maxDurationSeconds:.55},"Weapon Hit":{category:"WeaponHit",targetDurationSeconds:.45,maxDurationSeconds:.45},"Magic Explosion":{category:"Explosion",targetDurationSeconds:.5,maxDurationSeconds:.5,soundDirection:"realistic impact foundation with restrained magical energy"}}; Object.assign(recipe,presets[name]||{}); recipe.durationMode="NaturalDecay";recipe.durationCharacter="Auto";recipe.decayProfile=inferredDecayProfile(recipe); if(["Landing","Weapon Hit","Magic Explosion"].includes(name)){const limits=name==="Landing"?[.18,.55,.55]:name==="Weapon Hit"?[.15,.45,.45]:[.18,.5,.5];recipe.layers=[{name:"transient",takes:6,adoptionTakes:4,maxDurationSeconds:limits[0],prompt:"sharp material-specific initial contact"},{name:"body",takes:6,adoptionTakes:4,maxDurationSeconds:limits[1],prompt:"weighted impact body with controlled low-mid energy"},{name:"tail",takes:6,adoptionTakes:4,maxDurationSeconds:limits[2],prompt:"short debris or restrained fantasy-energy decay without baked reverb"}];} recipe.collisionProfile=name==="Magic Explosion"?null:semanticCollisionProfile(selectedCue(),recipe);recipe.generationProfile="Standard";normalizeCollisionRecipe(recipe,selectedCue());normalizeRecipeCounts(recipe);if(recipe.collisionProfile)rebuildCollisionPrompts(recipe);recipe.derivationProvider="Preset:"+name;recipe.updatedAtUtc=new Date().toISOString(); }
function currentWorkspaceTab(cue=selectedCue()) { if(!cue)return "semantic"; const stored=state.workspaceTabs.get(cue.audioCueId); if(stored)return stored; const hasCandidates=state.generationCueId===cue.audioCueId&&(state.generationJob?.candidates?.length||0)>0; return hasCandidates?"candidates":"semantic"; }
function setWorkspaceTab(tab) { const cue=selectedCue(); if(!cue)return; stopCandidateMix(); state.workspaceTabs.set(cue.audioCueId,tab); if(["recipe","generate","candidates"].includes(tab)){state.generationTabs.set(cue.audioCueId,tab);state.appScreen="candidates";}else if(tab==="basis")state.appScreen="basis";else if(tab==="scenario")state.appScreen="preview";else state.appScreen="cues";state.workspaceMode="cues";const url=new URL(location.href);url.searchParams.set("screen",state.appScreen);url.searchParams.delete("mode");history.replaceState(null,"",url);render(); }
function setGenerationTab(tab) { setWorkspaceTab(tab); }
function standardLayerCounts(recipe) { const category=String(recipe?.category||"").toLowerCase(); const takes=category==="footstep"?12:category==="objectcollision"?8:6; const adoption=category==="footstep"?6:4; return {takes,adoption}; }
function applyGenerationProfile(profile) { const cue=selectedCue(),recipe=cue&&state.generationBriefs.get(cue.audioCueId); if(!recipe||profile==="Custom")return; const standard=standardLayerCounts(recipe); for(const layer of recipe.layers.filter(item=>item.enabled)){layer.takes=profile==="Quick"?Math.max(1,Math.ceil(standard.takes/2)):profile==="Explore"?12:standard.takes; layer.adoptionTakes=profile==="Quick"?Math.max(1,Math.min(layer.takes,Math.ceil(standard.adoption/2))):Math.min(layer.takes,standard.adoption);} recipe.generationProfile=profile; normalizeRecipeCounts(recipe); recipe.updatedAtUtc=new Date().toISOString(); updateSaveIndicator(); renderEditor(); }
function applyAdoptionPolicy(policy) { const cue=selectedCue(),recipe=cue&&state.generationBriefs.get(cue.audioCueId);if(!recipe)return;const standard=standardLayerCounts(recipe);for(const layer of recipe.layers.filter(item=>item.enabled)){layer.adoptionTakes=policy==="Single"?1:policy==="Varied"?Math.min(layer.takes,6):Math.min(layer.takes,standard.adoption);}recipe.generationProfile="Custom";normalizeRecipeCounts(recipe);recipe.updatedAtUtc=new Date().toISOString();updateSaveIndicator();renderEditor(); }
function updateGenerationLayerCount(index,kind,value) { const cue=selectedCue(),recipe=cue&&state.generationBriefs.get(cue.audioCueId),layer=recipe?.layers?.[index]; if(!layer)return; const next=Math.trunc(Number(value||0)); if(kind==="takes"){layer.takes=Math.max(1,Math.min(12,next));layer.adoptionTakes=Math.min(layer.takes,Math.max(1,layer.adoptionTakes));}else layer.adoptionTakes=Math.max(1,Math.min(layer.takes,next)); recipe.generationProfile="Custom"; normalizeRecipeCounts(recipe); recipe.updatedAtUtc=new Date().toISOString(); updateSaveIndicator(); renderEditor(); }
function shortPromptDiff(oldValue,newValue) { if(oldValue===newValue)return "提示词相同"; return `${String(oldValue||"").slice(0,90)} → ${String(newValue||"").slice(0,90)}`; }
function badge(text, toneName = "") { return `<span class="badge ${toneName}">${esc(text)}</span>`; }
function empty(text) { return `<div class="empty">${esc(text)}</div>`; }
function esc(value) { return String(value ?? "").replace(/[&<>"']/g, char => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[char])); }
function setServer(message, toneName) { el.serverStatus.textContent = message; el.serverStatus.className = `server-pill ${toneName || ""}`; }
function showActionFeedback(toneName, title, message, metrics = [], retryable = false) {
  if (!el.actionFeedback) return;
  const icon = toneName === "ok" ? "✓" : toneName === "error" ? "!" : "↻";
  el.actionFeedback.hidden = false;
  el.actionFeedback.className = `action-feedback ${toneName}`;
  el.actionFeedback.innerHTML = `<span class="action-feedback-icon" aria-hidden="true">${icon}</span><div class="action-feedback-copy"><strong>${esc(title)}</strong><p>${esc(message)}</p>${metrics.length ? `<div class="action-feedback-metrics">${metrics.map(([label,value])=>`<span><b>${esc(value)}</b>${esc(label)}</span>`).join("")}</div>` : ""}</div><div class="action-feedback-actions">${retryable ? `<button type="button" data-feedback-retry>重试</button>` : ""}<button type="button" data-feedback-close aria-label="关闭提示">×</button></div>`;
  el.actionFeedback.querySelector("[data-feedback-close]")?.addEventListener("click", hideActionFeedback);
  el.actionFeedback.querySelector("[data-feedback-retry]")?.addEventListener("click", publishRuntime);
}
function hideActionFeedback() {
  if (!el.actionFeedback) return;
  el.actionFeedback.hidden = true;
  el.actionFeedback.innerHTML = "";
}
async function api(path, options = {}) {
  const response = await fetch(path, { headers: { "Content-Type": "application/json" }, ...options });
  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    const error = new Error(payload.message || payload.error || `${response.status} ${response.statusText}`);
    error.code = payload.code || payload.error || "HTTP_ERROR";
    error.detail = payload.detail || "";
    error.retryable = Boolean(payload.retryable);
    error.status = response.status;
    throw error;
  }
  return payload;
}
