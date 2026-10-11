const API = {
  packages: "/api/authoring/buffs/packages",
  options: "/api/authoring/buffs/options",
  load: "/api/authoring/buffs/load",
  descriptionPreview: "/api/authoring/buffs/description-preview",
  save: "/api/authoring/buffs/save",
  validate: "/api/authoring/buffs/validate",
  compile: "/api/authoring/buffs/compile",
  importUnity: "/api/authoring/buffs/import-unity",
};

const FALLBACK_OPTIONS = {
  durationFrameRate: 60,
  instantFrameRateBase: 30,
  instantFrames: 2,
  locales: [
    { id: "zh-CN", label: "简体中文" },
    { id: "en-US", label: "English" },
  ],
  durationKinds: [
    { id: "instant", label: "瞬时", description: "约 1/30 秒的最短可见生命周期。" },
    { id: "duration", label: "持续", description: "按秒编辑持续时间。" },
    { id: "permanent", label: "永久", description: "不按持续时间自动过期。" },
  ],
  stackingModes: [
    { id: "single", label: "不叠层 / 刷新时间", description: "重复获得时只刷新持续时间。" },
    { id: "stacking", label: "可叠层 / 刷新时间", description: "重复获得时增加层数并刷新持续时间。" },
  ],
  descriptionModes: [
    { id: "Auto", label: "自动生成 (Auto)", description: "根据效果、时间和叠层配置生成描述。" },
    { id: "Manual", label: "手写覆盖 (Manual)", description: "使用手写描述作为表现层文本。" },
  ],
  buffTypes: [
    { id: "empower", label: "强化", description: "提升角色能力或正向状态。" },
    { id: "weaken", label: "削弱", description: "降低角色能力或施加负向状态。" },
    { id: "damage", label: "伤害", description: "造成伤害或持续伤害的状态。" },
    { id: "restore", label: "恢复", description: "恢复生命、资源或其他可恢复状态。" },
    { id: "control", label: "控制", description: "限制行动、输入、移动或技能使用。" },
    { id: "damageReduction", label: "减伤", description: "降低受到的伤害或提高防护能力。" },
    { id: "mechanic", label: "机制", description: "承载规则、标记或特殊机制。" },
  ],
  attributes: [
    { attributeId: 1, displayName: "HP", label: "HP(1)", description: "生命值。" },
    { attributeId: 2, displayName: "Attack", label: "Attack(2)", description: "攻击力。" },
    { attributeId: 3, displayName: "Defense", label: "Defense(3)", description: "防御力。" },
    { attributeId: 4, displayName: "Posture", label: "Posture(4)", description: "韧性 / 姿态。" },
  ],
  stackPolicies: [
    { id: "RefreshDuration", label: "刷新持续时间 (RefreshDuration)" },
    { id: "Replace", label: "替换并重置 (Replace)" },
    { id: "Stack", label: "叠层并刷新 (Stack)" },
  ],
  relations: [
    { id: "Any", label: "任意目标 (Any)" },
    { id: "Self", label: "只作用自己 (Self)" },
    { id: "SameTeam", label: "同队目标 (SameTeam)" },
    { id: "Enemy", label: "敌方目标 (Enemy)" },
  ],
  auraLayerMasks: [
    { id: "dynamicCharacters", value: 256, label: "角色 / 动态目标 (256)" },
  ],
  fields: [],
};

const state = {
  packages: [],
  buffOptions: FALLBACK_OPTIONS,
  packageRelative: "",
  document: null,
  savedDocument: null,
  savedDocumentHash: "",
  draftRevision: "",
  validationReport: null,
  authoringDiff: null,
  compileResult: null,
  generatedDiff: null,
  importPlan: [],
  importResult: null,
  descriptionLocale: "zh-CN",
  autoDescriptionPreview: {},
  autoDescriptionPreviewLoading: false,
  autoDescriptionPreviewError: "",
  selected: { kind: "buff", index: 0 },
  dirty: false,
  hasSourceChanges: false,
  baselineHasSourceChanges: false,
  serverSupportsBuffTypes: false,
  validationStale: false,
  canWrite: false,
  loading: false,
  lastMessage: "",
};

const el = {};
let autoDescriptionPreviewTimer = 0;
let autoDescriptionPreviewRequestId = 0;

document.addEventListener("DOMContentLoaded", () => {
  bindElements();
  bindEvents();
  loadPackages();
});

function bindElements() {
  for (const id of [
    "serverStatus", "packageSelect", "reloadButton", "saveButton", "saveHint", "validateButton",
    "compileButton", "importButton", "statusStrip", "listSummary", "definitionList",
    "editorTitle", "editorSubtitle", "editorContent", "diagnosticsSummary",
    "diagnosticsList", "compileSummary", "compilePreview", "importSummary",
    "importPlan", "copyDiagnosticsButton", "diffSummary", "diffList", "discardAllButton"
  ]) {
    el[id] = document.getElementById(id);
  }
}

function bindEvents() {
  el.packageSelect.addEventListener("change", () => {
    if ((state.dirty || state.hasSourceChanges) && !window.confirm("当前 Buff 包有未保存修改，确定切换吗？")) {
      el.packageSelect.value = state.packageRelative;
      return;
    }
    loadPackage(el.packageSelect.value);
  });
  el.reloadButton.addEventListener("click", () => loadPackage(state.packageRelative));
  el.saveButton.addEventListener("click", saveDocument);
  el.validateButton.addEventListener("click", validateDocument);
  el.compileButton.addEventListener("click", compileDocument);
  el.importButton.addEventListener("click", importGenerated);
  el.copyDiagnosticsButton.addEventListener("click", copyDiagnostics);
  el.discardAllButton.addEventListener("click", discardAllChanges);

  el.definitionList.addEventListener("click", event => {
    const button = event.target.closest("[data-select-kind]");
    if (!button) return;
    state.selected = {
      kind: button.dataset.selectKind,
      index: Number.parseInt(button.dataset.selectIndex, 10) || 0,
    };
    render();
  });

  el.diffList.addEventListener("click", async event => {
    const button = event.target.closest("[data-diff-action]");
    if (!button) return;
    const path = button.dataset.path || "";
    if (button.dataset.diffAction === "jump") {
      jumpToPath(path);
    } else if (button.dataset.diffAction === "revert") {
      await revertField(path);
    }
  });

  el.diagnosticsList.addEventListener("click", event => {
    const item = event.target.closest("[data-diagnostic-path]");
    if (!item) return;
    jumpToPath(item.dataset.diagnosticPath || "");
  });

  el.editorContent.addEventListener("input", handleEditorInput);
  el.editorContent.addEventListener("change", handleEditorInput);
}

async function loadPackages() {
  setLoading(true, "正在读取 Buff 包列表...");
  try {
    const [options, packages] = await Promise.all([
      readJson(API.options),
      readJson(API.packages),
	    ]);
	    state.serverSupportsBuffTypes = Array.isArray(options?.buffTypes) && options.buffTypes.length > 0;
	    state.buffOptions = normalizeOptions(options);
	    state.descriptionLocale = preferredDescriptionLocale(state.descriptionLocale);
	    state.packages = packages;
    const queryPackage = new URLSearchParams(window.location.search).get("package");
    const firstPackage = queryPackage || state.packages[0]?.relative || "";
    renderPackageSelect();
    if (!state.serverSupportsBuffTypes) {
      state.lastMessage = "当前 Authoring 服务过旧，不支持 Buff 类型；请重启 editor serve 后再编辑。";
    }
    if (firstPackage) {
      await loadPackage(firstPackage);
    } else {
      state.lastMessage = "没有发现 Buff authoring package。";
      render();
    }
  } catch (error) {
    state.lastMessage = error.message;
    state.document = null;
    render();
  } finally {
    setLoading(false);
  }
}

async function loadPackage(packageRelative) {
  if (!packageRelative) return;
  setLoading(true, "正在读取 Buff 包...");
  try {
    const data = await readJson(`${API.load}?package=${encodeURIComponent(packageRelative)}`);
    state.packageRelative = data.package || packageRelative;
    state.document = data.document || null;
    state.savedDocument = cloneJson(state.document);
    state.savedDocumentHash = data.savedDocumentHash || "";
    state.draftRevision = data.draftRevision || "";
    state.validationReport = data.validationReport || null;
    state.authoringDiff = data.authoringDiff || cleanDiff("Authoring");
    state.hasSourceChanges = !!data.hasSourceChanges;
    state.baselineHasSourceChanges = state.hasSourceChanges;
    state.compileResult = null;
    state.generatedDiff = null;
	    state.importPlan = data.importPlan || [];
	    state.importResult = null;
	    state.autoDescriptionPreview = {};
	    state.autoDescriptionPreviewError = "";
	    state.autoDescriptionPreviewLoading = false;
	    state.canWrite = !!data.canWrite;
    state.dirty = false;
    state.validationStale = false;
    state.selected = { kind: "buff", index: 0 };
	    state.lastMessage = state.serverSupportsBuffTypes
      ? `已加载 ${data.packageId || state.packageRelative}`
      : "当前 Authoring 服务过旧，不支持 Buff 类型；请重启 editor serve 后再编辑。";
	    await refreshAutoDescriptionPreview({ renderAfter: false });
	    render();
  } catch (error) {
    state.lastMessage = error.message;
    state.document = null;
    render();
  } finally {
    setLoading(false);
  }
}

async function saveDocument() {
  if (!state.document || !state.packageRelative) return;
  flushEditorDraftFromDom();
  if (!canSaveCurrentDraft()) {
    state.lastMessage = saveActionReason();
    renderStatus();
    renderActions();
    return;
  }
  setLoading(true, "正在保存 Buff authoring JSON...");
  try {
    const result = await postJson(API.save, {
      package: state.packageRelative,
      document: state.document,
    });
    state.document = result.document || state.document;
    state.savedDocument = result.saved ? cloneJson(state.document) : state.savedDocument;
    state.savedDocumentHash = result.savedDocumentHash || state.savedDocumentHash;
    state.draftRevision = result.draftRevision || state.draftRevision;
    state.validationReport = result.validationReport || state.validationReport;
	    state.authoringDiff = result.authoringDiff || state.authoringDiff;
	    state.hasSourceChanges = !!result.hasSourceChanges;
	    state.baselineHasSourceChanges = state.hasSourceChanges;
	    state.dirty = !result.saved && !!state.authoringDiff?.hasChanges;
	    state.validationStale = false;
	    state.compileResult = null;
	    state.generatedDiff = null;
	    state.lastMessage = result.message || "保存完成。";
	    await refreshAutoDescriptionPreview({ renderAfter: false });
	    render();
  } catch (error) {
    state.lastMessage = error.message;
    render();
  } finally {
    setLoading(false);
  }
}

async function validateDocument() {
  if (!state.document || !state.packageRelative) return;
  if (!ensureServerSupportsBuffTypes()) return;
  flushEditorDraftFromDom();
  setLoading(true, "正在校验草稿...");
  try {
    const result = await postJson(API.validate, {
      package: state.packageRelative,
      document: state.document,
    });
    state.document = result.document || state.document;
    state.savedDocumentHash = result.savedDocumentHash || state.savedDocumentHash;
    state.draftRevision = result.draftRevision || state.draftRevision;
    state.validationReport = result.validationReport || state.validationReport;
	    state.authoringDiff = result.authoringDiff || state.authoringDiff;
	    state.hasSourceChanges = !!result.hasSourceChanges;
	    state.dirty = !!state.authoringDiff?.hasChanges;
	    state.validationStale = false;
	    state.lastMessage = result.success ? "校验通过。" : "校验存在阻断问题。";
	    await refreshAutoDescriptionPreview({ renderAfter: false });
	    render();
  } catch (error) {
    state.lastMessage = error.message;
    render();
  } finally {
    setLoading(false);
  }
}

async function compileDocument() {
  if (!state.document || !state.packageRelative) return;
  if (!ensureServerSupportsBuffTypes()) return;
  flushEditorDraftFromDom();
  setLoading(true, "正在编译草稿预览...");
  try {
    const result = await postJson(API.compile, {
      package: state.packageRelative,
      document: state.document,
    });
    state.compileResult = result.compileResult || null;
    state.generatedDiff = result.generatedDiff || null;
    state.validationReport = state.compileResult?.validationReport || state.validationReport;
    state.importPlan = result.importPlan || state.importPlan;
    state.lastMessage = result.success ? "编译预览完成，未写 generated 文件。" : "编译预览返回阻断问题。";
    render();
  } catch (error) {
    state.lastMessage = error.message;
    render();
  } finally {
    setLoading(false);
  }
}

async function importGenerated() {
  if (!ensureServerSupportsBuffTypes()) return;
  flushEditorDraftFromDom();
  if (!state.document || !state.packageRelative || state.dirty || state.hasSourceChanges) return;
  setLoading(true, "正在导入 generated runtime JSON...");
  try {
    const result = await postJson(API.importUnity, {
      package: state.packageRelative,
    });
    state.importResult = result;
    state.validationReport = result.validationReport || state.validationReport;
    state.lastMessage = result.message || "导入完成。";
    render();
  } catch (error) {
    state.lastMessage = error.message;
    render();
  } finally {
    setLoading(false);
  }
}

async function handleEditorInput(event) {
  const localeTarget = event.target.closest("[data-description-locale]");
  if (localeTarget) {
    if (event.type !== "change") return;
    state.descriptionLocale = preferredDescriptionLocale(localeTarget.value || "zh-CN");
    await refreshAutoDescriptionPreview({ renderAfter: true });
    return;
  }

  const durationKindTarget = event.target.closest("[data-duration-kind]");
  if (durationKindTarget) {
    if (event.type !== "change" || !state.document) return;
    applyDurationKind(durationKindTarget.dataset.basePath || "", durationKindTarget.value || "duration");
    markDirty({ rerenderEditor: true });
    return;
  }

  const stackingModeTarget = event.target.closest("[data-stacking-mode]");
  if (stackingModeTarget) {
    if (event.type !== "change" || !state.document) return;
    applyStackingMode(stackingModeTarget.dataset.basePath || "", stackingModeTarget.checked);
    markDirty({ rerenderEditor: true });
    return;
  }

  const target = event.target.closest("[data-path]");
  if (!target || !state.document) return;

  const buffTypeTarget = target.closest("[data-buff-type]");
  if (buffTypeTarget) {
    if (event.type !== "change") return;
    applyBuffType(buffTypeTarget.dataset.path || "", buffTypeTarget.dataset.buffType || "", buffTypeTarget.checked);
    markDirty({ rerenderEditor: true });
    return;
  }

  let value;
  if (target.type === "checkbox") {
    value = target.checked;
  } else if (target.dataset.number === "seconds") {
    const seconds = target.value === "" ? 0 : Number.parseFloat(target.value);
    const frameRate = durationFrameRate();
    value = Number.isFinite(seconds) ? Math.max(0, Math.round(seconds * frameRate)) : 0;
  } else if (target.dataset.number === "int") {
    value = target.value === "" ? 0 : Number.parseInt(target.value, 10);
  } else if (target.dataset.number === "float") {
    value = target.value === "" ? 0 : Number.parseFloat(target.value);
  } else {
    value = target.value;
  }

  const fieldKey = fieldKeyFromPath(target.dataset.path);
  if (fieldKey === "maxStackCount") {
    const ownerPath = parentPath(target.dataset.path);
    if (String(getPathValue(state.document, `${ownerPath}.stackPolicy`) || "") === "Stack") {
      value = Math.max(2, Number(value) || 2);
      target.value = String(value);
      setPathValue(state.document, `${ownerPath}.applyStackCount`, 1);
    }
  }

  setPathValue(state.document, target.dataset.path, value);
  markDirty({ rerenderEditor: shouldRerenderEditorForField(fieldKey) });
}

function markDirty(options = {}) {
  state.dirty = true;
  state.hasSourceChanges = true;
  state.validationStale = true;
  state.compileResult = null;
  state.generatedDiff = null;
  state.importResult = null;
  state.autoDescriptionPreviewError = "";
  if (options.rerenderEditor) {
    renderEditor();
  } else {
    refreshCurrentBuffPreview();
  }
  renderDefinitionList();
  renderStatusStrip();
  renderActions();
  renderDiffReview();
  renderCompilePreview();
  renderImportPlan();
  scheduleAutoDescriptionPreviewRefresh();
}

function flushEditorDraftFromDom() {
  if (!state.document || !el.editorContent) return false;
  let changed = false;

  const durationInputs = Array.from(el.editorContent.querySelectorAll("input[data-duration-kind]:checked"));
  for (const input of durationInputs) {
    const before = cloneJson(getPathValue(state.document, input.dataset.basePath || ""));
    applyDurationKind(input.dataset.basePath || "", input.value || "duration");
    const after = getPathValue(state.document, input.dataset.basePath || "");
    changed = changed || !valuesEqual(before, after);
  }

  const stackingInputs = Array.from(el.editorContent.querySelectorAll("input[data-stacking-mode]"));
  for (const input of stackingInputs) {
    const basePath = input.dataset.basePath || "";
    const before = cloneJson(getPathValue(state.document, basePath));
    applyStackingMode(basePath, input.checked);
    const after = getPathValue(state.document, basePath);
    changed = changed || !valuesEqual(before, after);
  }

  const buffTypeGroups = new Map();
  for (const input of Array.from(el.editorContent.querySelectorAll("input[data-buff-type][data-path]"))) {
    const path = input.dataset.path || "";
    if (!path) continue;
    if (!buffTypeGroups.has(path)) buffTypeGroups.set(path, []);
    if (input.checked) buffTypeGroups.get(path).push(input.dataset.buffType || "");
  }
  for (const [path, values] of buffTypeGroups) {
    changed = setDraftPathIfChanged(path, normalizeBuffTypes(values)) || changed;
  }

  const controls = Array.from(el.editorContent.querySelectorAll("input[data-path], select[data-path], textarea[data-path]"));
  for (const control of controls) {
    if (control.dataset.buffType) continue;
    const path = control.dataset.path || "";
    if (!path) continue;
    changed = setDraftPathIfChanged(path, readControlDraftValue(control)) || changed;
  }

  if (changed) {
    state.dirty = true;
    state.hasSourceChanges = true;
    state.validationStale = true;
    state.compileResult = null;
    state.generatedDiff = null;
    state.importResult = null;
    state.autoDescriptionPreviewError = "";
  }

  return changed;
}

function readControlDraftValue(control) {
  if (control.type === "checkbox") return control.checked;
  if (control.dataset.number === "seconds") {
    const seconds = control.value === "" ? 0 : Number.parseFloat(control.value);
    const frameRate = durationFrameRate();
    return Number.isFinite(seconds) ? Math.max(0, Math.round(seconds * frameRate)) : 0;
  }
  if (control.dataset.number === "int") {
    return control.value === "" ? 0 : Number.parseInt(control.value, 10);
  }
  if (control.dataset.number === "float") {
    return control.value === "" ? 0 : Number.parseFloat(control.value);
  }
  return control.value;
}

function setDraftPathIfChanged(path, value) {
  const before = getPathValue(state.document, path);
  if (valuesEqual(before, value)) return false;
  setPathValue(state.document, path, value);
  return true;
}

function valuesEqual(left, right) {
  return JSON.stringify(left ?? null) === JSON.stringify(right ?? null);
}

function render() {
  renderPackageSelect();
  renderStatus();
  renderStatusStrip();
  renderActions();
  renderDefinitionList();
  renderEditor();
  renderDiffReview();
  renderDiagnostics();
  renderCompilePreview();
  renderImportPlan();
}

function renderPackageSelect() {
  el.packageSelect.innerHTML = state.packages.map(item => {
    const label = `${item.packageId || item.relative} (${item.sourceKind || "package"})`;
    return `<option value="${escapeHtml(item.relative)}"${item.relative === state.packageRelative ? " selected" : ""}>${escapeHtml(label)}</option>`;
  }).join("");
}

function renderStatus() {
  el.serverStatus.textContent = state.lastMessage || (state.document ? "Authoring 服务已连接" : "未连接 Authoring 服务");
}

function renderActions() {
  const busy = state.loading;
  const hasDocument = !!state.document;
  const blocked = !!state.validationReport?.hasBlockingIssues;
  const hasChanges = !!state.authoringDiff?.hasChanges;
  const hasSourceChanges = !!state.hasSourceChanges;
  const saveReady = canSaveCurrentDraft();
  const saveReason = saveActionReason();
  el.reloadButton.disabled = busy || !state.packageRelative;
  el.saveButton.disabled = busy || !hasDocument;
  el.saveButton.classList.toggle("soft-disabled", hasDocument && !busy && !saveReady);
  el.saveButton.setAttribute("aria-disabled", saveReady ? "false" : "true");
  el.saveButton.title = saveReason;
  if (el.saveHint) {
    el.saveHint.textContent = hasDocument ? saveReason : "";
    el.saveHint.classList.toggle("ok", saveReady);
    el.saveHint.classList.toggle("warn", hasDocument && !saveReady);
  }
  el.validateButton.disabled = busy || !hasDocument || !state.serverSupportsBuffTypes;
  el.validateButton.title = state.serverSupportsBuffTypes ? "校验草稿并刷新改动审查。" : "当前 Authoring 服务过旧，不支持 Buff 类型；请重启 editor serve。";
  el.compileButton.disabled = busy || !hasDocument || !state.serverSupportsBuffTypes;
  el.compileButton.title = state.serverSupportsBuffTypes ? "编译草稿预览，不写 generated 文件。" : "当前 Authoring 服务过旧，不支持 Buff 类型；请重启 editor serve。";
  el.importButton.disabled = busy || !hasDocument || state.dirty || state.hasSourceChanges || state.validationStale || blocked;
  el.importButton.title = state.dirty
    ? "当前有未保存修改，先保存并校验。"
    : state.hasSourceChanges
      ? "源文件需要先保存规范化结果。"
    : state.validationStale
      ? "当前校验已过期，先校验。"
    : blocked
      ? "当前校验阻断，不能导入 generated runtime JSON。"
      : "导入已保存 Buff 包生成的 runtime JSON。";
  el.discardAllButton.disabled = busy || !hasDocument || !state.dirty;
}

function saveActionReason() {
  if (state.loading) return "正在处理，稍后再保存。";
  if (!state.document || !state.packageRelative) return "先选择并加载 Buff 包。";
  if (!state.serverSupportsBuffTypes) return "当前 Authoring 服务过旧，不支持 Buff 类型；请重启 editor serve 后再保存。";
  if (!state.canWrite) return "当前 buff_authoring.json 不可写，不能保存。";
  if (state.validationStale) return "先点击“校验”，Ready 后才能保存。";
  if (!state.authoringDiff) return "先点击“校验”刷新改动审查。";
  if (state.validationReport?.hasBlockingIssues) return "存在阻断诊断，修复并重新校验后才能保存。";
  const hasChanges = !!state.authoringDiff?.hasChanges;
  const hasSourceChanges = !!state.hasSourceChanges;
  if (!hasChanges && !hasSourceChanges) return "当前没有需要保存的改动。";
  if (hasSourceChanges && !hasChanges) return "可保存：写回源文件规范化结果。";
  return "可保存：已校验的改动会写回 authoring JSON。";
}

function ensureServerSupportsBuffTypes() {
  if (state.serverSupportsBuffTypes) return true;
  state.lastMessage = "当前 Authoring 服务过旧，不支持 Buff 类型；请重启 editor serve 并强制刷新页面。";
  renderStatus();
  renderActions();
  return false;
}

function canSaveCurrentDraft() {
  const hasDocument = !!state.document;
  const blocked = !!state.validationReport?.hasBlockingIssues;
  const hasCurrentDiff = !!state.authoringDiff && !state.validationStale;
  const hasChanges = !!state.authoringDiff?.hasChanges;
  const hasSourceChanges = !!state.hasSourceChanges;
  return !state.loading
    && hasDocument
    && state.serverSupportsBuffTypes
    && state.canWrite
    && (state.dirty || hasSourceChanges)
    && !state.validationStale
    && !blocked
    && hasCurrentDiff
    && (hasChanges || hasSourceChanges);
}

function renderStatusStrip() {
  const report = state.validationReport;
  const status = report?.status || "Unknown";
  const issueCount = Array.isArray(report?.diagnostics) ? report.diagnostics.length : 0;
  const diffCount = state.authoringDiff?.changeCount || 0;
  const packageLabel = state.document?.packageId || state.packageRelative || "未选择";
  const dirtyLabel = state.dirty ? "未保存" : (state.hasSourceChanges ? "源需保存" : "已保存");
  el.statusStrip.innerHTML = [
    badge(packageLabel, "neutral"),
    badge(`Validation: ${status}`, report?.hasBlockingIssues ? "error" : "ok"),
    badge(`Diagnostics: ${issueCount}`, issueCount ? "warn" : "ok"),
    badge(state.validationStale ? "校验过期" : "校验有效", state.validationStale ? "warn" : "ok"),
    badge(`Diff: ${diffCount}`, diffCount ? "warn" : "ok"),
    badge(dirtyLabel, state.dirty || state.hasSourceChanges ? "warn" : "ok"),
    badge(state.canWrite ? "可写" : "只读", state.canWrite ? "ok" : "warn"),
    badge(state.serverSupportsBuffTypes ? "类型 API OK" : "服务过旧", state.serverSupportsBuffTypes ? "ok" : "warn"),
  ].join("");
}

function renderDefinitionList() {
  const buffs = state.document?.buffs || [];
  const auras = state.document?.auras || [];
  el.listSummary.textContent = `${buffs.length} Buff / ${auras.length} Aura`;
  const rows = [];
  rows.push(`<h3 class="section-title">Buffs</h3>`);
  rows.push(...buffs.map((buff, index) => definitionButton(
    "buff",
    index,
    buffListTitle(buff),
    buffListSubtitle(buff),
    [
      ...formatBuffTypeLabels(buff.types),
      formatDuration(buff.defaultDurationFrames || 0, !!buff.isPermanent),
      formatStackingSummary(buff),
      formatVisibility(buff.presentation)
    ])));
  rows.push(`<h3 class="section-title">Auras</h3>`);
  rows.push(...auras.map((aura, index) => definitionButton(
    "aura",
    index,
    `光环刷新 ${findBuffName(aura.buffId)}`,
    `${formatNumber(aura.radiusMeters || 0)}m / ${formatRelation(aura.relationFilter || "Any")}`,
    [
      aura.includeCaster !== false ? "包含施法者" : "不包含施法者",
      formatLayerMask(aura.layerMask || 0)
    ])));
  el.definitionList.innerHTML = rows.join("");
}

function definitionButton(kind, index, title, subtitle, chips) {
  const selected = state.selected.kind === kind && state.selected.index === index ? " selected" : "";
  return `<button class="definition-button${selected}" type="button" data-select-kind="${kind}" data-select-index="${index}">
    <strong>${escapeHtml(title)}</strong>
    <span>${escapeHtml(subtitle)}</span>
    <small>${(chips || []).filter(Boolean).map(item => `<em>${escapeHtml(item)}</em>`).join("")}</small>
  </button>`;
}

function renderEditor() {
  if (!state.document) {
    el.editorTitle.textContent = "未加载 Buff 包";
    el.editorSubtitle.textContent = "请通过 EditorHub 启动 Authoring 服务";
    el.editorContent.innerHTML = `<div class="empty-state">没有可编辑内容。</div>`;
    return;
  }

  if (state.selected.kind === "aura") {
    renderAuraEditor();
  } else {
    renderBuffEditor();
  }
}

function renderBuffEditor() {
  const buff = (state.document.buffs || [])[state.selected.index];
  if (!buff) {
    el.editorTitle.textContent = "未选择 Buff";
    el.editorSubtitle.textContent = "";
    el.editorContent.innerHTML = `<div class="empty-state">请选择一个 Buff。</div>`;
    return;
  }

  const basePath = `buffs.${state.selected.index}`;
  el.editorTitle.textContent = buff.displayName || buff.stableId || `Buff ${buff.buffId}`;
  el.editorSubtitle.textContent = summarizeBuffEffects(buff) || "未配置效果";
  el.editorContent.innerHTML = `
    <h3 class="section-title first">徽章与名称</h3>
    ${renderBadgeAndNameEditor(buff, `${basePath}.presentation`)}

    <h3 class="section-title">效果</h3>
    <div class="effect-list">
      ${(buff.effects || []).map((effect, effectIndex) => renderEffectEditor(effect, `${basePath}.effects.${effectIndex}`)).join("") || `<div class="empty-state">此 Buff 没有效果；v0 不新增结构。</div>`}
    </div>

    <h3 class="section-title">持续与叠层</h3>
    <div class="form-grid">
      ${durationKindControl(basePath, buff)}
      ${durationSecondsField("持续时间", `${basePath}.defaultDurationFrames`, buff.defaultDurationFrames || 0, durationKindOf(buff))}
      ${stackingModeControl(basePath, buff)}
      ${maxStackField(basePath, buff)}
    </div>

    <h3 class="section-title">显示规则</h3>
    ${renderDisplayRulesEditor(buff, `${basePath}.presentation`)}

    <h3 class="section-title">技术信息</h3>
    ${techDetails([
      ["buffId", buff.buffId],
      ["stableId", buff.stableId],
      ["defaultDurationFrames", buff.defaultDurationFrames],
      ["isPermanent", !!buff.isPermanent],
      ["stackPolicy", buff.stackPolicy],
      ["applyStackCount", buff.applyStackCount]
    ], "查看底层字段")}
  `;
}

function renderEffectEditor(effect, basePath) {
  const summary = summarizeEffect(effect);
  const payload = effect.kind === "Status"
    ? numberField("状态 ID", `${basePath}.statusId`, effect.statusId || 0, "int")
    : `${selectField("影响属性", `${basePath}.attributeId`, effect.attributeId || 0, attributeOptions())}
       ${numberField("数值变化", `${basePath}.addValue`, effect.addValue || 0, "int", "正数增加，负数减少。")}`;
  return `<article class="effect-card">
    <div class="effect-summary">
      <strong>${escapeHtml(summary || "未命名效果")}</strong>
      <span>${escapeHtml(effect.kind || "Effect")}</span>
    </div>
    <div class="form-grid">${payload}</div>
    ${techDetails([
      ["effectId", effect.effectId],
      ["kind", effect.kind]
    ], "技术信息")}
  </article>`;
}

function renderBadgeAndNameEditor(buff, basePath) {
  const presentation = buff?.presentation;
  if (!presentation) {
    return `<div class="empty-state">此 Buff 没有 presentation；v0 不新增结构。</div>`;
  }

  const descriptionMode = descriptionModeOf(presentation);
  const suggestedNameKey = buff?.stableId ? `${buff.stableId}.name` : "stableId.name";
  const manualEditor = descriptionMode === "Manual"
    ? `<div class="form-grid full">
        ${textareaField("手写效果描述", `${basePath}.description`, presentation.description || "", "写入 authoring JSON。")}
      </div>`
    : "";

  return `
  ${renderBadgePreview(buff)}
  ${buffTypesField("Buff 类型", parentPath(basePath) + ".types", buff?.types || [])}
  <div class="form-grid">
    ${textField("名称多语言 Key", `${basePath}.nameKey`, presentation.nameKey || "", `建议：${suggestedNameKey}`)}
    ${textField("Fallback 显示名称", parentPath(basePath) + ".displayName", buff?.displayName || "", "本地化 key 缺失或解析失败时使用。")}
	    ${textField("短名称", `${basePath}.shortName`, presentation.shortName || "")}
	    ${textField("图标资源键", `${basePath}.iconResourceKey`, presentation.iconResourceKey || "")}
	    ${selectField("描述来源", `${basePath}.descriptionMode`, descriptionMode, optionList("descriptionModes"))}
	    ${descriptionLocaleField()}
	  </div>
	  ${manualEditor}`;
}

function renderDisplayRulesEditor(buff, basePath) {
  const presentation = buff?.presentation;
  if (!presentation) {
    return `<div class="empty-state">此 Buff 没有 presentation；v0 不新增结构。</div>`;
  }

  return `<div class="form-grid">
    ${checkboxField("显示在玩家 HUD", `${basePath}.showOnPlayerHud`, presentation.showOnPlayerHud !== false)}
    ${checkboxField("显示在目标 HUD", `${basePath}.showOnTargetHud`, presentation.showOnTargetHud !== false)}
    ${checkboxField("显示在头顶条", `${basePath}.showOnHeadPad`, presentation.showOnHeadPad !== false)}
    ${numberField("显示优先级", `${basePath}.priority`, presentation.priority ?? 0, "int")}
  </div>`;
}

function renderBadgePreview(buff) {
  const presentation = buff?.presentation || {};
  const shortName = presentation.shortName || "BF";
  const displayName = buff?.displayName || buff?.stableId || `Buff ${buff?.buffId || 0}`;
  const nameKey = presentation.nameKey || "";
  const iconKey = presentation.iconResourceKey || "";
  const mode = descriptionModeOf(presentation);
  const description = displayDescriptionForBuff(buff);
  return `<article class="badge-preview" data-badge-preview="true">
    ${badgePreviewInner(shortName, displayName, nameKey, iconKey, mode, description, formatBuffTypeLabels(buff?.types))}
  </article>`;
}

function badgePreviewInner(shortName, displayName, nameKey, iconKey, mode, description, typeLabels = []) {
  const previewState = state.autoDescriptionPreviewLoading
    ? `预览语言：${formatLocaleLabel(state.descriptionLocale)} / 生成中`
    : `预览语言：${formatLocaleLabel(state.descriptionLocale)}`;
  return `<div class="badge-token ${iconKey ? "" : "missing"}">
      <span>${escapeHtml(shortName || "BF")}</span>
    </div>
    <div class="badge-preview-main">
      <div class="badge-preview-title">
        <strong>${escapeHtml(displayName || "未命名 Buff")}</strong>
        <span>${escapeHtml(shortName || "无短名")}</span>
      </div>
      <p>${escapeHtml(description || "当前配置没有可显示的效果描述。")}</p>
      <div class="preview-chips">
        ${(typeLabels || []).map(label => `<em>${escapeHtml(label)}</em>`).join("")}
        <em class="${nameKey ? "ok" : "warn"}">${escapeHtml(nameKey ? "名称 Key 已填写" : "缺名称 Key")}</em>
        <em class="${iconKey ? "ok" : "warn"}">${escapeHtml(iconKey ? iconKey : "缺图标 Key")}</em>
        <em>${escapeHtml(formatDescriptionMode(mode))}</em>
        <em>${escapeHtml(previewState)}</em>
        ${state.autoDescriptionPreviewError ? `<em class="warn">${escapeHtml(state.autoDescriptionPreviewError)}</em>` : ""}
      </div>
    </div>`;
}

function renderAuraEditor() {
  const aura = (state.document.auras || [])[state.selected.index];
  if (!aura) {
    el.editorTitle.textContent = "未选择 Aura";
    el.editorSubtitle.textContent = "";
    el.editorContent.innerHTML = `<div class="empty-state">请选择一个 Aura。</div>`;
    return;
  }

  const basePath = `auras.${state.selected.index}`;
  el.editorTitle.textContent = `光环：${findBuffName(aura.buffId)}`;
  el.editorSubtitle.textContent = `${formatNumber(aura.radiusMeters || 0)}m / ${formatRelation(aura.relationFilter || "Any")}`;
  el.editorContent.innerHTML = `
    ${techDetails([
      ["auraId", aura.auraId],
      ["buffId", aura.buffId],
      ["raw layerMask", aura.layerMask]
    ])}

    <h3 class="section-title">范围刷新</h3>
    <div class="form-grid">
      ${numberField("光环半径（米）", `${basePath}.radiusMeters`, aura.radiusMeters || 0, "float")}
      ${selectField("目标层预设", `${basePath}.layerMask`, aura.layerMask || 0, layerMaskOptions(aura.layerMask || 0))}
      ${selectField("目标关系", `${basePath}.relationFilter`, aura.relationFilter || "Any", optionList("relations"))}
      ${checkboxField("包含施法者", `${basePath}.includeCaster`, aura.includeCaster !== false)}
    </div>
  `;
}

function renderDiagnostics() {
  const diagnostics = state.validationReport?.diagnostics || [];
  setInspectorPanelVisible(el.diagnosticsList, diagnostics.length > 0);
  el.diagnosticsSummary.textContent = diagnostics.length
    ? `${diagnostics.length} 条诊断`
    : "没有诊断";
  el.diagnosticsList.innerHTML = diagnostics.length
    ? diagnostics.map(item => `<article class="diagnostic ${item.severity === "Error" ? "error" : ""}" data-diagnostic-path="${escapeHtml(normalizePath(item.path || ""))}">
        <strong>${escapeHtml(item.severity || "Info")} ${escapeHtml(item.code || "")}</strong>
        <span>${escapeHtml(labelForPath(item.path || ""))}</span>
        <code>${escapeHtml(item.path || "")}</code>
        <p>${escapeHtml(item.message || "")}</p>
      </article>`).join("")
    : `<div class="empty-state">校验 Ready 时这里保持为空。</div>`;
}

function renderDiffReview() {
  const heading = el.diffSummary?.closest(".panel-card")?.querySelector("h2");
  if (heading) heading.textContent = "安全状态";

  if (!state.document) {
    el.diffSummary.textContent = "尚未读取";
    el.diffList.innerHTML = renderSafetyOverview(cleanDiff("Authoring"));
    return;
  }

  if (state.validationStale) {
    const staleDiff = state.authoringDiff || cleanDiff("Authoring");
    el.diffSummary.textContent = "校验已过期";
    el.diffList.innerHTML = renderSafetyOverview(staleDiff);
    return;
  }

  const diff = state.authoringDiff || cleanDiff("Authoring");
  el.diffSummary.textContent = diff.hasChanges
    ? `${diff.changeCount || 0} 项未保存改动`
    : "没有未保存改动";
  el.diffList.innerHTML = renderSafetyOverview(diff);
}

function renderCompilePreview() {
  const result = state.compileResult;
  setInspectorPanelVisible(el.compilePreview, !!result);
  if (!result) {
    el.compileSummary.textContent = "尚未编译";
    el.compilePreview.innerHTML = `<div class="empty-state">点击“编译预览”查看输出数量和 JSON 片段。</div>`;
    return;
  }

  const buffCount = result.buffDefinitions?.definitions?.length || 0;
  const presentationCount = result.buffPresentationDefinitions?.definitions?.length || 0;
  const auraCount = result.auraBuffDefinitions?.definitions?.length || 0;
  const generatedDiffCount = state.generatedDiff?.changeCount || 0;
  el.compileSummary.textContent = `${buffCount} Buff / ${presentationCount} Presentation / ${auraCount} Aura`;
  el.compilePreview.innerHTML = `
    ${summaryRow("Buff Definitions", buffCount)}
    ${summaryRow("Presentation Definitions", presentationCount)}
    ${summaryRow("Aura Definitions", auraCount)}
    ${summaryRow("Generated Diff", generatedDiffCount)}
    ${renderGeneratedDiff()}
    ${jsonPreview("BuffDefinitions JSON", result.buffDefinitions || {})}
    ${jsonPreview("BuffPresentationDefinitions JSON", result.buffPresentationDefinitions || {})}
    ${jsonPreview("AuraBuffDefinitions JSON", result.auraBuffDefinitions || {})}
  `;
}

function renderGeneratedDiff() {
  const diff = state.generatedDiff || cleanDiff("Generated");
  if (!diff.hasChanges) {
    return `<div class="empty-state">Generated 输出与当前文件一致。</div>`;
  }
  return `<div class="generated-diff">
    ${renderDiffGroups(diff.groups || [], false)}
  </div>`;
}

function renderImportPlan() {
  const writes = state.importResult?.writes || state.importPlan || [];
  setInspectorPanelVisible(el.importPlan, !!state.importResult);
  el.importSummary.textContent = state.importResult?.message || (state.dirty || state.hasSourceChanges ? "当前有未保存修改或规范化结果，不能导入" : "导入只读取已保存的 Ready 包");
  el.importPlan.innerHTML = writes.length
    ? writes.map(write => `<article class="import-path">
        <strong>${escapeHtml(write.kind || "Generated")}</strong>
        <code>${escapeHtml(write.relativePath || "")}</code>
      </article>`).join("")
    : `<div class="empty-state">尚未读取导入计划。</div>`;
}

function renderSafetyOverview(diff) {
  const diagnostics = state.validationReport?.diagnostics || [];
  const issueCount = diagnostics.length;
  const diffCount = state.validationStale ? "需校验" : (state.hasSourceChanges && !(diff?.changeCount || 0) ? "源需保存" : String(diff?.changeCount || 0));
  const compileLabel = state.compileResult
    ? `${state.compileResult.buffDefinitions?.definitions?.length || 0} Buff`
    : "未编译";
  const importLabel = state.dirty || state.hasSourceChanges || state.validationStale
    ? "导入禁用"
    : (state.validationReport?.hasBlockingIssues ? "导入阻断" : "可导入");
  const saveReady = canSaveCurrentDraft();
  const saveLabel = saveReady ? "可保存" : "保存未就绪";
  return `<div class="safety-overview">
    ${safetyItem("改动", diffCount, state.validationStale || state.hasSourceChanges || diff?.hasChanges ? "warn" : "ok")}
    ${safetyItem("诊断", issueCount ? String(issueCount) : "0", issueCount ? "warn" : "ok")}
    ${safetyItem("保存", saveLabel, saveReady ? "ok" : "muted")}
    ${safetyItem("编译", compileLabel, state.compileResult ? "ok" : "muted")}
    ${safetyItem("导入", importLabel, importLabel === "可导入" ? "ok" : "muted")}
  </div>
  ${renderDiffDrawer(diff)}
  ${renderImportPlanDrawer()}`;
}

function safetyItem(label, value, tone) {
  const normalized = tone === "ok" || tone === "warn" ? tone : "";
  return `<article class="safety-item ${normalized}">
    <span>${escapeHtml(label)}</span>
    <strong>${escapeHtml(value)}</strong>
  </article>`;
}

function renderDiffDrawer(diff) {
  if (state.validationStale) {
    return `<div class="compact-note">字段已修改。点击“校验”刷新 diff，保存会在 Ready 后开放。</div>`;
  }

  if (!diff?.hasChanges) {
    return `<div class="compact-note">草稿与保存基线一致。</div>`;
  }

  return `<details class="inspector-drawer">
    <summary>查看 ${escapeHtml(String(diff.changeCount || 0))} 项改动</summary>
    <div class="drawer-body">${renderDiffGroups(diff.groups || [], true)}</div>
  </details>`;
}

function renderImportPlanDrawer() {
  const writes = state.importResult?.writes || state.importPlan || [];
  if (!writes.length) return "";
  return `<details class="inspector-drawer">
    <summary>导入计划</summary>
    <div class="drawer-body import-plan compact">
      ${writes.map(write => `<article class="import-path">
        <strong>${escapeHtml(write.kind || "Generated")}</strong>
        <code>${escapeHtml(write.relativePath || "")}</code>
      </article>`).join("")}
    </div>
  </details>`;
}

function setInspectorPanelVisible(container, visible) {
  const card = container?.closest(".panel-card");
  if (card) {
    card.classList.toggle("is-hidden", !visible);
  }
}

function readonlyValue(label, value) {
  return `<div class="readonly-value"><span>${escapeHtml(label)}</span><code>${escapeHtml(String(value ?? ""))}</code></div>`;
}

function renderDiffGroups(groups, allowRevert) {
  return groups.map(group => `<section class="diff-group">
    <header>
      <strong>${escapeHtml(group.ownerLabel || group.ownerId || group.ownerKind || group.artifactKind || "Change")}</strong>
      <span>${escapeHtml(group.artifactKind || "")}${group.ownerKind ? ` / ${escapeHtml(group.ownerKind)}` : ""}</span>
    </header>
    ${(group.changes || []).map(change => renderDiffChange(change, allowRevert)).join("")}
  </section>`).join("");
}

function renderDiffChange(change, allowRevert) {
  const normalizedPath = normalizePath(change.path || "");
  const canRevert = allowRevert && change.changeKind === "Changed" && !!normalizedPath;
  const fieldLabel = labelForPath(change.path || change.field || "");
  return `<article class="diff-change">
    <div class="diff-main">
      <strong>${escapeHtml(changeKindLabel(change.changeKind))} ${escapeHtml(fieldLabel)}</strong>
      <code>${escapeHtml(change.path || "")}</code>
    </div>
    <div class="diff-values">
      <span>旧值</span><code>${escapeHtml(formatFieldValue(change.path || "", change.beforeValue || ""))}</code>
      <span>新值</span><code>${escapeHtml(formatFieldValue(change.path || "", change.afterValue || ""))}</code>
    </div>
    <div class="diff-actions">
      ${normalizedPath ? `<button type="button" data-diff-action="jump" data-path="${escapeHtml(normalizedPath)}">定位</button>` : ""}
      ${canRevert ? `<button type="button" data-diff-action="revert" data-path="${escapeHtml(normalizedPath)}">回退字段</button>` : ""}
    </div>
  </article>`;
}

function textField(label, path, value, hint = "") {
  return `<label class="form-field ${fieldClass(path)}">
    <span>${escapeHtml(label)}</span>
    <input type="text" value="${escapeHtml(value)}" data-path="${escapeHtml(path)}">
    ${fieldHelpMarkup(path, hint)}
    ${fieldDiagnosticsMarkup(path)}
  </label>`;
}

function textareaField(label, path, value, hint = "") {
  return `<label class="form-field ${fieldClass(path)}">
    <span>${escapeHtml(label)}</span>
    <textarea rows="3" data-path="${escapeHtml(path)}">${escapeHtml(value)}</textarea>
    ${fieldHelpMarkup(path, hint)}
    ${fieldDiagnosticsMarkup(path)}
  </label>`;
}

function numberField(label, path, value, kind, hint = "") {
  return `<label class="form-field ${fieldClass(path)}">
    <span>${escapeHtml(label)}</span>
    <input type="number" value="${escapeHtml(String(value ?? 0))}" data-number="${kind}" data-path="${escapeHtml(path)}">
    ${fieldHelpMarkup(path, hint)}
    ${fieldDiagnosticsMarkup(path)}
  </label>`;
}

function durationKindControl(basePath, buff) {
  const currentKind = durationKindOf(buff);
  const inputName = `duration-kind-${basePath.replace(/[^a-z0-9_-]/gi, "-")}`;
  return `<div class="form-field duration-kind-control ${durationFieldClass(basePath)}">
    <span>时间类型</span>
    <div class="segmented-control" role="radiogroup" aria-label="时间类型">
      ${optionList("durationKinds").map(option => {
        const value = String(option.id || "");
        const checked = value === currentKind ? " checked" : "";
        return `<label>
          <input type="radio" name="${escapeHtml(inputName)}" value="${escapeHtml(value)}" data-duration-kind="true" data-base-path="${escapeHtml(basePath)}"${checked}>
          <span>${escapeHtml(option.label || value)}</span>
        </label>`;
      }).join("")}
    </div>
    ${fieldHelpMarkup("durationKind", durationKindHint(buff, currentKind))}
    ${fieldDiagnosticsMarkup(`${basePath}.isPermanent`)}
    ${fieldDiagnosticsMarkup(`${basePath}.defaultDurationFrames`)}
  </div>`;
}

function durationSecondsField(label, path, frames, durationKind) {
  const seconds = framesToSeconds(frames || 0);
  const disabled = durationKind !== "duration";
  const hint = durationKind === "instant"
    ? `由“瞬时”固定为 ${instantFrames()} 帧。`
    : durationKind === "permanent"
      ? `永久时不编辑持续时间。`
      : `当前写回 ${frames || 0} 帧。`;
  return `<label class="form-field ${fieldClass(path)}">
    <span>${escapeHtml(label)}</span>
    <input type="number" min="0" step="0.01" value="${escapeHtml(seconds)}" data-number="seconds" data-path="${escapeHtml(path)}"${disabled ? " disabled" : ""}>
    ${fieldHelpMarkup(path, hint)}
    ${fieldDiagnosticsMarkup(path)}
  </label>`;
}

function stackingModeControl(basePath, buff) {
  const isStacking = isStackingBuff(buff);
  const hint = isStacking ? "重复获得增加 1 层并刷新时间。" : "重复获得只刷新时间。";
  return `<label class="checkbox-field ${stackingFieldClass(basePath)}">
    <input type="checkbox" ${isStacking ? "checked" : ""} data-stacking-mode="true" data-base-path="${escapeHtml(basePath)}">
    <span><strong>可叠层</strong>${fieldHelpMarkup("stackPolicy", hint)}</span>
    ${fieldDiagnosticsMarkup(`${basePath}.stackPolicy`)}
    ${fieldDiagnosticsMarkup(`${basePath}.applyStackCount`)}
  </label>`;
}

function maxStackField(basePath, buff) {
  const isStacking = isStackingBuff(buff);
  const path = `${basePath}.maxStackCount`;
  const value = isStacking ? Math.max(2, Number(buff.maxStackCount) || 2) : 1;
  const hint = isStacking
    ? "可叠层时生效。"
    : "不叠层时固定为 1。";
  return `<label class="form-field ${fieldClass(path)}">
    <span>最大层数</span>
    <input type="number" min="${isStacking ? "2" : "1"}" step="1" value="${escapeHtml(String(value))}" data-number="int" data-path="${escapeHtml(path)}"${isStacking ? "" : " disabled"}>
    ${fieldHelpMarkup(path, hint)}
    ${fieldDiagnosticsMarkup(path)}
  </label>`;
}

function checkboxField(label, path, checked, hint = "") {
  return `<label class="checkbox-field ${fieldClass(path)}">
    <input type="checkbox" ${checked ? "checked" : ""} data-path="${escapeHtml(path)}">
    <span><strong>${escapeHtml(label)}</strong>${fieldHelpMarkup(path, hint)}</span>
    ${fieldDiagnosticsMarkup(path)}
  </label>`;
}

function selectField(label, path, value, options, hint = "") {
  const normalizedValue = String(value ?? "");
  const numberAttribute = isNumericSelectPath(path) ? ` data-number="int"` : "";
  return `<label class="form-field ${fieldClass(path)}">
    <span>${escapeHtml(label)}</span>
    <select data-path="${escapeHtml(path)}"${numberAttribute}>
      ${(options || []).map(option => {
        const optionValue = optionValueOf(option);
        const selected = String(optionValue) === normalizedValue ? " selected" : "";
        return `<option value="${escapeHtml(optionValue)}"${selected}>${escapeHtml(optionLabelOf(option))}</option>`;
      }).join("")}
    </select>
    ${fieldHelpMarkup(path, hint)}
    ${fieldDiagnosticsMarkup(path)}
	  </label>`;
}

function buffTypesField(label, path, values) {
  const selected = new Set(normalizeBuffTypes(values));
  const options = optionList("buffTypes");
  const disabled = state.serverSupportsBuffTypes ? "" : " disabled";
  const hint = state.serverSupportsBuffTypes
    ? "可多选；只表示玩家/策划感知，不表示来源或运行策略。"
    : "当前 Authoring 服务过旧，重启 editor serve 后才能编辑 Buff 类型。";
  return `<section class="buff-types-field ${fieldClass(path)}" tabindex="-1" data-path="${escapeHtml(path)}">
    <div class="buff-types-heading">
      <strong>${escapeHtml(label)}</strong>
      ${fieldHelpMarkup(path, hint)}
    </div>
    <div class="buff-type-grid">
      ${options.map(option => {
        const id = String(optionValueOf(option));
        const checked = selected.has(id) ? " checked" : "";
        return `<label class="type-chip">
          <input type="checkbox" data-path="${escapeHtml(path)}" data-buff-type="${escapeHtml(id)}"${checked}${disabled}>
          <span>${escapeHtml(optionLabelOf(option))}</span>
        </label>`;
      }).join("")}
    </div>
    ${fieldDiagnosticsMarkup(path)}
  </section>`;
}

function descriptionLocaleField() {
  const locales = optionList("locales");
  const value = preferredDescriptionLocale(state.descriptionLocale);
  return `<label class="form-field">
    <span>自动描述预览语言</span>
    <select data-description-locale="true">
      ${(locales || []).map(option => {
        const optionValue = optionValueOf(option);
        const selected = String(optionValue) === value ? " selected" : "";
        return `<option value="${escapeHtml(optionValue)}"${selected}>${escapeHtml(optionLabelOf(option))}</option>`;
      }).join("")}
    </select>
    ${fieldHelpMarkup("descriptionLocale", "只影响 Auto 描述预览，不写入 authoring JSON。")}
  </label>`;
}

function fieldClass(path) {
  return getDiagnosticsForPath(path).length ? "has-diagnostic" : "";
}

function durationFieldClass(basePath) {
  return [fieldClass(`${basePath}.isPermanent`), fieldClass(`${basePath}.defaultDurationFrames`)]
    .filter(Boolean)
    .join(" ");
}

function stackingFieldClass(basePath) {
  return [fieldClass(`${basePath}.stackPolicy`), fieldClass(`${basePath}.applyStackCount`)]
    .filter(Boolean)
    .join(" ");
}

function fieldDiagnosticsMarkup(path) {
  const diagnostics = getDiagnosticsForPath(path);
  if (!diagnostics.length) return "";
  return `<div class="field-diagnostics">
    ${diagnostics.map(item => `<p class="${escapeHtml(String(item.severity || "Info").toLowerCase())}">${escapeHtml(item.severity || "Info")} ${escapeHtml(item.code || "")}: ${escapeHtml(item.message || "")}</p>`).join("")}
  </div>`;
}

function getDiagnosticsForPath(path) {
  const normalized = normalizePath(path);
  return (state.validationReport?.diagnostics || []).filter(item => {
    const itemPath = normalizePath(item.path || "");
    return itemPath === normalized || itemPath.startsWith(`${normalized}.`);
  });
}

function fieldHelpMarkup(path, extra = "") {
  const description = fieldMeta(path)?.description || "";
  const summary = extra || shortFieldHint(path);
  const text = [summary, description].filter(Boolean).join(description && summary ? "\n\n" : "");
  return text
    ? `<span class="field-help-icon" role="button" tabindex="0" title="${escapeHtml(text)}" aria-label="${escapeHtml(text)}">?</span>`
    : "";
}

function shortFieldHint(pathOrField) {
  const field = fieldKeyFromPath(pathOrField);
  if (field === "nameKey") return "用于本地化显示名。";
  if (field === "displayName") return "本地化失败时显示。";
  if (field === "shortName") return "徽章上的短标签。";
  if (field === "iconResourceKey") return "图标资源 key。";
  if (field === "types") return "可多选分类。";
  if (field === "descriptionMode") return "默认自动生成。";
  if (field === "descriptionLocale") return "只切换预览语言。";
  if (field === "attributeId") return "选择要影响的属性。";
  if (field === "addValue") return "正数增加，负数减少。";
  if (field === "priority") return "越大越优先显示。";
  return "";
}

function techDetails(items, summary = "技术信息") {
  return `<details class="tech-details">
    <summary>${escapeHtml(summary)}</summary>
    <div class="readonly-grid">
      ${(items || []).map(item => readonlyValue(item[0], item[1])).join("")}
    </div>
  </details>`;
}

function normalizeOptions(payload) {
  const options = payload || {};
  const durationFrameRateValue = Number.isFinite(options.durationFrameRate) && options.durationFrameRate > 0
    ? options.durationFrameRate
    : FALLBACK_OPTIONS.durationFrameRate;
  const instantFrameRateBaseValue = Number.isFinite(options.instantFrameRateBase) && options.instantFrameRateBase > 0
    ? options.instantFrameRateBase
    : FALLBACK_OPTIONS.instantFrameRateBase;
  const instantFramesValue = Number.isFinite(options.instantFrames) && options.instantFrames > 0
    ? options.instantFrames
    : Math.max(1, Math.ceil(durationFrameRateValue / instantFrameRateBaseValue));
  return {
    durationFrameRate: durationFrameRateValue,
    instantFrameRateBase: instantFrameRateBaseValue,
	    instantFrames: instantFramesValue,
	    locales: Array.isArray(options.locales) && options.locales.length ? options.locales : FALLBACK_OPTIONS.locales,
	    durationKinds: Array.isArray(options.durationKinds) && options.durationKinds.length ? options.durationKinds : FALLBACK_OPTIONS.durationKinds,
    stackingModes: Array.isArray(options.stackingModes) && options.stackingModes.length ? options.stackingModes : FALLBACK_OPTIONS.stackingModes,
    descriptionModes: Array.isArray(options.descriptionModes) && options.descriptionModes.length ? options.descriptionModes : FALLBACK_OPTIONS.descriptionModes,
    buffTypes: Array.isArray(options.buffTypes) && options.buffTypes.length ? options.buffTypes : FALLBACK_OPTIONS.buffTypes,
    attributes: Array.isArray(options.attributes) && options.attributes.length ? options.attributes : FALLBACK_OPTIONS.attributes,
    stackPolicies: Array.isArray(options.stackPolicies) && options.stackPolicies.length ? options.stackPolicies : FALLBACK_OPTIONS.stackPolicies,
    relations: Array.isArray(options.relations) && options.relations.length ? options.relations : FALLBACK_OPTIONS.relations,
    auraLayerMasks: Array.isArray(options.auraLayerMasks) && options.auraLayerMasks.length ? options.auraLayerMasks : FALLBACK_OPTIONS.auraLayerMasks,
    fields: Array.isArray(options.fields) ? options.fields : [],
  };
}

function optionList(name) {
  return state.buffOptions?.[name] || FALLBACK_OPTIONS[name] || [];
}

function attributeOptions() {
  return optionList("attributes");
}

function preferredDescriptionLocale(value) {
  const locales = optionList("locales");
  const requested = String(value || "zh-CN");
  if (locales.some(item => String(optionValueOf(item)) === requested)) return requested;
  if (locales.some(item => String(optionValueOf(item)) === "zh-CN")) return "zh-CN";
  return String(optionValueOf(locales[0] || { id: "zh-CN" }));
}

function formatLocaleLabel(value) {
  const locale = preferredDescriptionLocale(value);
  const option = optionList("locales").find(item => String(optionValueOf(item)) === locale);
  return option?.label || locale;
}

function layerMaskOptions(currentValue) {
  const options = optionList("auraLayerMasks").slice();
  if (!options.some(option => Number(option.value) === Number(currentValue))) {
    options.push({
      id: "current",
      value: Number(currentValue) || 0,
      label: `当前自定义值 (${Number(currentValue) || 0})`,
      description: "当前 JSON 中已有的 raw layerMask；切换到安全预设后不再手填 raw 值。",
    });
  }
  return options;
}

function applyDurationKind(basePath, kind) {
  if (!basePath || !state.document) return;
  const normalizedKind = kind === "instant" || kind === "permanent" ? kind : "duration";
  const framesPath = `${basePath}.defaultDurationFrames`;
  const permanentPath = `${basePath}.isPermanent`;
  const currentFrames = Number(getPathValue(state.document, framesPath)) || 0;

  if (normalizedKind === "instant") {
    setPathValue(state.document, permanentPath, false);
    setPathValue(state.document, framesPath, instantFrames());
    return;
  }

  if (normalizedKind === "permanent") {
    setPathValue(state.document, permanentPath, true);
    return;
  }

  setPathValue(state.document, permanentPath, false);
  if (currentFrames <= instantFrames()) {
    setPathValue(state.document, framesPath, durationFrameRate());
  }
}

function applyStackingMode(basePath, isStacking) {
  if (!basePath || !state.document) return;
  if (isStacking) {
    const currentMax = Number(getPathValue(state.document, `${basePath}.maxStackCount`)) || 1;
    setPathValue(state.document, `${basePath}.stackPolicy`, "Stack");
    setPathValue(state.document, `${basePath}.applyStackCount`, 1);
    setPathValue(state.document, `${basePath}.maxStackCount`, Math.max(2, currentMax));
    return;
  }

  setPathValue(state.document, `${basePath}.stackPolicy`, "RefreshDuration");
  setPathValue(state.document, `${basePath}.applyStackCount`, 1);
  setPathValue(state.document, `${basePath}.maxStackCount`, 1);
}

function applyBuffType(path, type, checked) {
  if (!path || !type || !state.document) return;
  const current = normalizeBuffTypes(getPathValue(state.document, path));
  const next = new Set(current);
  if (checked) {
    next.add(type);
  } else {
    next.delete(type);
  }
  setPathValue(state.document, path, normalizeBuffTypes(Array.from(next)));
}

function optionValueOf(option) {
  if (option && typeof option === "object") {
    if (Object.prototype.hasOwnProperty.call(option, "attributeId")) return option.attributeId;
    if (Object.prototype.hasOwnProperty.call(option, "value")) return option.value;
    if (Object.prototype.hasOwnProperty.call(option, "id")) return option.id;
  }
  return option ?? "";
}

function optionLabelOf(option) {
  if (option && typeof option === "object") {
    return option.label || option.displayName || String(optionValueOf(option) || "(auto)");
  }
  return String(option || "(auto)");
}

function isNumericSelectPath(path) {
  const field = fieldKeyFromPath(path);
  return field === "attributeId" || field === "layerMask";
}

function shouldRerenderEditorForField(fieldKey) {
  return [
    "descriptionMode",
    "defaultDurationFrames",
    "isPermanent",
    "stackPolicy",
    "maxStackCount",
    "applyStackCount",
    "attributeId",
    "addValue",
    "statusId",
    "types",
  ].includes(fieldKey);
}

function refreshCurrentBuffPreview() {
  if (!state.document || state.selected.kind !== "buff") return;
  const buff = (state.document.buffs || [])[state.selected.index];
  if (!buff) return;

  el.editorTitle.textContent = buff.displayName || buff.stableId || `Buff ${buff.buffId}`;
  el.editorSubtitle.textContent = summarizeBuffEffects(buff) || "未配置效果";

  const preview = el.editorContent.querySelector("[data-badge-preview]");
  if (!preview) return;

  const presentation = buff.presentation || {};
  preview.innerHTML = badgePreviewInner(
    presentation.shortName || "BF",
    buff.displayName || buff.stableId || `Buff ${buff.buffId || 0}`,
    presentation.nameKey || "",
    presentation.iconResourceKey || "",
    descriptionModeOf(presentation),
    displayDescriptionForBuff(buff),
    formatBuffTypeLabels(buff.types));
}

function fieldMeta(pathOrField) {
  const field = fieldKeyFromPath(pathOrField);
  return (state.buffOptions?.fields || []).find(item => item.field === field) || null;
}

function fieldKeyFromPath(pathOrField) {
  const parts = normalizePath(pathOrField).split(".").filter(Boolean);
  return parts.length ? parts[parts.length - 1] : String(pathOrField || "");
}

function labelForPath(pathOrField) {
  if (normalizePath(pathOrField).split(".").includes("types")) return "Buff 类型";
  const field = fieldKeyFromPath(pathOrField);
  return fieldMeta(field)?.label || field || "字段";
}

function durationFrameRate() {
  const rate = state.buffOptions?.durationFrameRate || FALLBACK_OPTIONS.durationFrameRate;
  return rate > 0 ? rate : 60;
}

function instantFrameRateBase() {
  const base = state.buffOptions?.instantFrameRateBase || FALLBACK_OPTIONS.instantFrameRateBase;
  return base > 0 ? base : 30;
}

function instantFrames() {
  const configured = state.buffOptions?.instantFrames || FALLBACK_OPTIONS.instantFrames;
  if (configured > 0) return configured;
  return Math.max(1, Math.ceil(durationFrameRate() / instantFrameRateBase()));
}

function durationKindOf(buff) {
  if (buff?.isPermanent) return "permanent";
  const frames = Number(buff?.defaultDurationFrames) || 0;
  return frames <= instantFrames() ? "instant" : "duration";
}

function durationKindHint(buff, kind) {
  const frames = Number(buff?.defaultDurationFrames) || 0;
  if (kind === "instant") {
    return `保存为 ${instantFrames()} 帧 / ${framesToSeconds(instantFrames())} 秒。`;
  }
  if (kind === "permanent") {
    return `不会自动过期；保留当前 ${frames} 帧用于回切。`;
  }
  return `按秒编辑，写回 ${durationFrameRate()}fps 帧数。`;
}

function framesToSeconds(frames) {
  return formatNumber((Number(frames) || 0) / durationFrameRate(), 3);
}

function formatNumber(value, precision = 2) {
  const number = Number(value);
  if (!Number.isFinite(number)) return "0";
  const rounded = Math.round(number * Math.pow(10, precision)) / Math.pow(10, precision);
  return Number.isInteger(rounded) ? String(rounded) : String(rounded);
}

function formatDuration(frames, isPermanent, detailed = false) {
  if (isPermanent) return "永久";
  const frameValue = Number(frames) || 0;
  if (frameValue <= instantFrames()) {
    return detailed ? `瞬时 (${frameValue}帧 / ${framesToSeconds(frameValue)}秒)` : "瞬时";
  }
  return `持续 ${framesToSeconds(frameValue)}秒 (${frameValue}帧)`;
}

function formatStackPolicy(value) {
  return isStackPolicyStacking(value) ? "可叠层 / 刷新时间" : "不叠层 / 刷新时间";
}

function formatDescriptionMode(value) {
  return formatStringOption("descriptionModes", value || "Auto");
}

function isStackingBuff(buff) {
  return isStackPolicyStacking(buff?.stackPolicy) && (Number(buff?.maxStackCount) || 1) > 1;
}

function isStackPolicyStacking(value) {
  return String(value || "") === "Stack";
}

function formatStackingSummary(buff) {
  return isStackingBuff(buff)
    ? `最多 ${Math.max(2, Number(buff.maxStackCount) || 2)} 层 / 刷新时间`
    : "不叠层 / 刷新时间";
}

function formatRelation(value) {
  return formatStringOption("relations", value);
}

function formatLayerMask(value) {
  const number = Number(value) || 0;
  const option = optionList("auraLayerMasks").find(item => Number(item.value) === number);
  return option?.label || `自定义层 (${number})`;
}

function formatAttribute(value) {
  const number = Number(value) || 0;
  const option = attributeOptions().find(item => Number(item.attributeId) === number);
  return option?.label || `Attribute(${number})`;
}

function formatStringOption(listName, value) {
  const text = String(value ?? "");
  const option = optionList(listName).find(item => String(item.id ?? "") === text);
  return option?.label || (text || "自动推断");
}

function normalizeBuffTypes(values) {
  const raw = Array.isArray(values) ? values : [];
  const selected = new Set(raw.map(item => String(item || "").trim()).filter(Boolean));
  const ordered = [];
  for (const option of optionList("buffTypes")) {
    const id = String(optionValueOf(option));
    if (selected.has(id)) {
      ordered.push(id);
      selected.delete(id);
    }
  }
  for (const id of selected) ordered.push(id);
  return ordered;
}

function formatBuffTypeLabels(values) {
  return normalizeBuffTypes(values).map(value => formatBuffType(value));
}

function formatBuffTypes(values) {
  const labels = formatBuffTypeLabels(values);
  return labels.length ? labels.join(" / ") : "未分类";
}

function formatBuffType(value) {
  return formatStringOption("buffTypes", value);
}

function formatVisibility(presentation) {
  if (!presentation) return "无表现配置";
  const targets = [];
  if (presentation.showOnPlayerHud !== false) targets.push("玩家 HUD");
  if (presentation.showOnTargetHud !== false) targets.push("目标 HUD");
  if (presentation.showOnHeadPad !== false) targets.push("头顶条");
  return targets.length ? targets.join(" / ") : "不显示";
}

function findBuffName(buffId) {
  const buff = (state.document?.buffs || []).find(item => Number(item.buffId) === Number(buffId));
  return buff?.displayName || buff?.stableId || `Buff ${buffId || 0}`;
}

function buffListTitle(buff) {
  const displayName = buff?.displayName || buff?.stableId || `Buff ${buff?.buffId || 0}`;
  const shortName = buff?.presentation?.shortName || "";
  return shortName ? `${shortName} · ${displayName}` : displayName;
}

function buffListSubtitle(buff) {
  return displayDescriptionForBuff(buff) || summarizeBuffEffects(buff);
}

function summarizeBuffEffects(buff) {
  const effects = (buff?.effects || []).map(summarizeEffect).filter(Boolean);
  return effects.length ? effects.join("; ") : "无效果";
}

function summarizeEffect(effect) {
  if (!effect) return "";
  if (effect.kind === "AttributeModifier") {
    const value = Number(effect.addValue) || 0;
    return `${formatAttribute(effect.attributeId)} ${value >= 0 ? "+" : ""}${value}`;
  }
  if (effect.kind === "Status") {
    return `状态 ${effect.statusId || 0}`;
  }
  return effect.kind || "Effect";
}

function changeKindLabel(kind) {
  if (kind === "Added") return "新增";
  if (kind === "Removed") return "移除";
  return "修改";
}

function formatFieldValue(path, value) {
  const field = fieldKeyFromPath(path);
  if (value === "") return "空";
  if (field === "defaultDurationFrames") return formatDuration(Number(value) || 0, false, true);
  if (field === "attributeId") return formatAttribute(value);
  if (field === "types") return formatBuffTypes(String(value || "").split(",").map(item => item.trim()).filter(Boolean));
  if (field === "stackPolicy") return formatStackPolicy(value);
  if (field === "descriptionMode") return formatDescriptionMode(value);
  if (field === "relationFilter") return formatRelation(value);
  if (field === "layerMask") return formatLayerMask(value);
  if (field === "radiusMeters") return `${formatNumber(value)}m`;
  if (field === "addValue") {
    const number = Number(value) || 0;
    return `${number >= 0 ? "+" : ""}${number}`;
  }
  if (field === "isPermanent") {
    return String(value).toLowerCase() === "true" ? "永久" : "非永久";
  }
  if (["includeCaster", "showOnPlayerHud", "showOnTargetHud", "showOnHeadPad"].includes(field)) {
    return String(value).toLowerCase() === "true" ? "是" : "否";
  }
  return String(value ?? "");
}

function descriptionModeOf(presentation) {
  const mode = String(presentation?.descriptionMode || "");
  if (mode === "Auto" || mode === "Manual") return mode;
  return presentation?.description ? "Manual" : "Auto";
}

function displayDescriptionForBuff(buff) {
  const presentation = buff?.presentation || {};
  const mode = descriptionModeOf(presentation);
  if (mode === "Manual" && String(presentation.description || "").trim()) {
    return presentation.description;
  }

  return autoDescriptionPreviewForBuff(buff)
    || (state.autoDescriptionPreviewLoading ? "正在生成自动效果描述..." : "")
    || (state.autoDescriptionPreviewError ? "自动效果描述预览不可用" : "");
}

function autoDescriptionPreviewForBuff(buff) {
  if (!buff) return "";
  const key = autoDescriptionPreviewKey(buff.stableId || "", buff.buffId || 0);
  return state.autoDescriptionPreview?.[key] || "";
}

function autoDescriptionPreviewKey(stableId, buffId) {
  return `${stableId || ""}#${Number(buffId) || 0}`;
}

function summaryRow(label, value) {
  return `<article class="summary-row"><strong>${escapeHtml(label)}</strong><code>${escapeHtml(String(value))}</code></article>`;
}

function jsonPreview(label, value) {
  return `<section class="json-section">
    <strong>${escapeHtml(label)}</strong>
    <pre class="json-preview">${escapeHtml(JSON.stringify(value || {}, null, 2))}</pre>
  </section>`;
}

function badge(label, tone) {
  const normalized = tone === "ok" || tone === "warn" || tone === "error" ? tone : "";
  return `<span class="badge ${normalized}">${escapeHtml(label)}</span>`;
}

function cleanDiff(scope) {
  return {
    scope: scope || "",
    hasChanges: false,
    changeCount: 0,
    groups: [],
  };
}

function cloneJson(value) {
  return value == null ? value : JSON.parse(JSON.stringify(value));
}

function normalizePath(path) {
  return String(path || "")
    .replace(/\[(\d+)\]/g, ".$1")
    .replace(/^\./, "");
}

function parentPath(path) {
  const parts = normalizePath(path).split(".").filter(Boolean);
  parts.pop();
  return parts.join(".");
}

function setPathValue(root, path, value) {
  const parts = normalizePath(path).split(".").filter(Boolean);
  if (!parts.length) return;
  let target = root;
  for (let i = 0; i < parts.length - 1; i++) {
    const key = parsePathPart(parts[i]);
    if (target[key] == null) target[key] = {};
    target = target[key];
  }
  target[parsePathPart(parts[parts.length - 1])] = value;
}

function getPathValue(root, path) {
  const parts = normalizePath(path).split(".").filter(Boolean);
  let target = root;
  for (const part of parts) {
    if (target == null) return undefined;
    target = target[parsePathPart(part)];
  }
  return target;
}

function parsePathPart(part) {
  return /^\d+$/.test(part) ? Number.parseInt(part, 10) : part;
}

function jumpToPath(path) {
  const normalized = normalizePath(path);
  selectPathOwner(normalized);
  render();
  window.requestAnimationFrame(() => focusPath(normalized));
}

async function revertField(path) {
  if (!state.document || !state.savedDocument) return;
  const normalized = normalizePath(path);
  const savedValue = getPathValue(state.savedDocument, normalized);
  if (typeof savedValue === "undefined") {
    state.lastMessage = "保存基线中没有这个字段，不能单字段回退。";
    renderStatus();
    return;
  }

  setPathValue(state.document, normalized, cloneJson(savedValue));
  state.dirty = true;
  state.hasSourceChanges = true;
  state.validationStale = true;
  state.compileResult = null;
  state.generatedDiff = null;
  state.importResult = null;
  state.lastMessage = "已回退字段，正在刷新 diff。";
  render();
  await validateDocument();
  jumpToPath(normalized);
}

function discardAllChanges() {
  if (!state.savedDocument) return;
  if (state.dirty && !window.confirm("放弃当前 Buff 包的全部未保存修改？")) return;
  state.document = cloneJson(state.savedDocument);
  state.dirty = false;
  state.hasSourceChanges = !!state.baselineHasSourceChanges;
  state.validationStale = false;
  state.authoringDiff = cleanDiff("Authoring");
  state.compileResult = null;
  state.generatedDiff = null;
  state.importResult = null;
  state.autoDescriptionPreview = {};
  state.autoDescriptionPreviewError = "";
  state.lastMessage = "已放弃全部未保存修改。";
  render();
  scheduleAutoDescriptionPreviewRefresh(0);
}

function selectPathOwner(path) {
  const parts = normalizePath(path).split(".");
  const list = parts[0];
  const index = Number.parseInt(parts[1], 10);
  if (!Number.isFinite(index)) return;
  if (list === "buffs") {
    state.selected = { kind: "buff", index };
  } else if (list === "auras") {
    state.selected = { kind: "aura", index };
  }
}

function focusPath(path) {
  const normalized = normalizePath(path);
  const fields = Array.from(el.editorContent.querySelectorAll("[data-path]"));
  const target = fields.find(node => normalizePath(node.dataset.path || "") === normalized)
    || fields.find(node => normalizePath(node.dataset.path || "").startsWith(`${normalized}.`));
  if (!target) return;
  target.scrollIntoView({ block: "center", behavior: "smooth" });
  target.focus({ preventScroll: true });
  const wrapper = target.closest(".form-field, .checkbox-field, .buff-types-field");
  if (wrapper) {
    wrapper.classList.add("field-focus");
    window.setTimeout(() => wrapper.classList.remove("field-focus"), 1400);
  }
}

function setLoading(value, message = "") {
  state.loading = value;
  if (message) state.lastMessage = message;
  renderStatus();
  renderActions();
  renderDiffReview();
}

function scheduleAutoDescriptionPreviewRefresh(delay = 160) {
  window.clearTimeout(autoDescriptionPreviewTimer);
  if (!state.document || !state.packageRelative) return;
  state.autoDescriptionPreviewLoading = true;
  autoDescriptionPreviewTimer = window.setTimeout(() => {
    refreshAutoDescriptionPreview({ renderAfter: true });
  }, Math.max(0, delay));
}

async function refreshAutoDescriptionPreview(options = {}) {
  if (!state.document || !state.packageRelative) return;
  const renderAfter = options.renderAfter !== false;
  const requestId = ++autoDescriptionPreviewRequestId;
  state.descriptionLocale = preferredDescriptionLocale(state.descriptionLocale);
  state.autoDescriptionPreviewLoading = true;
  state.autoDescriptionPreviewError = "";
  if (renderAfter) {
    refreshCurrentBuffPreview();
    renderDefinitionList();
  }

  try {
    const result = await postJson(API.descriptionPreview, {
      package: state.packageRelative,
      locale: state.descriptionLocale,
      document: state.document,
    });
    if (requestId !== autoDescriptionPreviewRequestId) return;

    state.descriptionLocale = preferredDescriptionLocale(result.locale || state.descriptionLocale);
    if (Array.isArray(result.locales) && result.locales.length) {
      state.buffOptions = {
        ...state.buffOptions,
        locales: result.locales,
      };
      state.descriptionLocale = preferredDescriptionLocale(state.descriptionLocale);
    }

    const preview = {};
    for (const item of result.buffs || []) {
      preview[autoDescriptionPreviewKey(item.stableId || "", item.buffId || 0)] = item.description || "";
    }
    state.autoDescriptionPreview = preview;
  } catch (error) {
    if (requestId !== autoDescriptionPreviewRequestId) return;
    state.autoDescriptionPreview = {};
    state.autoDescriptionPreviewError = error.message || "自动效果描述预览失败。";
  } finally {
    if (requestId !== autoDescriptionPreviewRequestId) return;
    state.autoDescriptionPreviewLoading = false;
    if (renderAfter) {
      renderDefinitionList();
      refreshCurrentBuffPreview();
      renderStatus();
    }
  }
}

async function readJson(url) {
  const response = await fetch(url, { headers: { Accept: "application/json" }, cache: "no-store" });
  return readJsonResponse(response, url);
}

async function postJson(url, body) {
  const response = await fetch(url, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body || {}),
  });
  return readJsonResponse(response, url);
}

async function readJsonResponse(response, url) {
  const text = await response.text();
  let payload = null;
  if (text) {
    try {
      payload = JSON.parse(text);
    } catch {
      throw new Error(`${url} returned non-JSON response (${response.status})`);
    }
  }
  if (!response.ok) {
    const message = payload?.message || payload?.error || `${url} failed (${response.status})`;
    throw new Error(message);
  }
  return payload;
}

async function copyDiagnostics() {
  const text = JSON.stringify(state.validationReport || {}, null, 2);
  try {
    await navigator.clipboard.writeText(text);
    state.lastMessage = "已复制诊断 JSON。";
  } catch {
    state.lastMessage = "复制失败；浏览器没有开放剪贴板权限。";
  }
  renderStatus();
}

function escapeHtml(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}
