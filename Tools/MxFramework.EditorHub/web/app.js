const DEFAULT_CHARACTER_PACKAGE = "Tools/MxFramework.Authoring/samples/character-iron-vanguard";

const state = {
  packages: [],
  packageRelative: DEFAULT_CHARACTER_PACKAGE,
  authoringState: null,
  characterState: null,
  resources: null,
  resourcePlan: null,
  diagnostics: [],
  diagnosticsSearch: "",
  diagnosticsFilter: "all",
  errors: []
};

const el = {};

document.addEventListener("DOMContentLoaded", () => {
  for (const id of [
    "serverStatus", "packageSelect", "refreshButton", "statusStrip", "toolGrid",
    "resourceSummary", "copyDiagnosticsButton", "resourceList", "planSummary", "planGrid",
    "diagnosticsSummary", "diagnosticsSearch", "diagnosticsFilter", "diagnosticsConsole"
  ]) {
    el[id] = document.getElementById(id);
  }

  const queryPackage = new URLSearchParams(window.location.search).get("package");
  if (queryPackage) state.packageRelative = queryPackage;

  el.refreshButton.addEventListener("click", refresh);
  el.packageSelect.addEventListener("change", event => {
    state.packageRelative = event.target.value;
    refresh({ keepPackages: true });
  });
  el.copyDiagnosticsButton.addEventListener("click", copyDiagnostics);
  el.diagnosticsSearch.addEventListener("input", event => {
    state.diagnosticsSearch = event.target.value;
    renderDiagnostics();
  });
  el.diagnosticsFilter.addEventListener("change", event => {
    state.diagnosticsFilter = event.target.value;
    renderDiagnostics();
  });

  refresh();
});

async function refresh(options = {}) {
  state.errors = [];
  el.serverStatus.textContent = "正在检测 Authoring 服务...";
  renderLoading();

  if (!options.keepPackages) {
    await loadPackages();
  }

  await Promise.all([
    loadAuthoringState(),
    loadCharacterState(),
    loadResources(),
    loadResourcePlan()
  ]);

  state.diagnostics = collectDiagnostics();
  render();
}

async function loadPackages() {
  const packages = await readJson("/api/character/packages", null, "角色包列表");
  if (Array.isArray(packages) && packages.length > 0) {
    state.packages = packages;
    if (!packages.some(pkg => pkg.relative === state.packageRelative)) {
      state.packageRelative = packages[0].relative;
    }
  } else {
    state.packages = [{ relative: DEFAULT_CHARACTER_PACKAGE, packageId: "iron_vanguard", kind: "character" }];
  }
}

async function loadAuthoringState() {
  state.authoringState = await readJson("/api/state", null, "Buff Authoring 状态");
}

async function loadCharacterState() {
  state.characterState = await readJson(`/api/character/state?package=${encodeURIComponent(state.packageRelative)}`, null, "CharacterStudio 状态");
}

async function loadResources() {
  state.resources = await readJson(`/api/authoring/resources?package=${encodeURIComponent(state.packageRelative)}`, null, "资源管理器状态");
}

async function loadResourcePlan() {
  state.resourcePlan = await readJson(`/api/authoring/resources/resource-plan?package=${encodeURIComponent(state.packageRelative)}`, null, "资源计划");
}

async function readJson(url, fallback, label) {
  try {
    const response = await fetch(url, { headers: { Accept: "application/json" } });
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return await response.json();
  } catch (error) {
    if (label) state.errors.push({ label, message: error.message });
    return fallback;
  }
}

function renderLoading() {
  el.statusStrip.innerHTML = statusChip("服务", "检测中", "pending");
  el.toolGrid.innerHTML = "";
  el.resourceSummary.textContent = "正在读取...";
  el.resourceList.innerHTML = "";
  el.planSummary.textContent = "正在读取...";
  el.planGrid.innerHTML = "";
  el.diagnosticsSummary.textContent = "正在扫描诊断信息...";
  el.diagnosticsConsole.innerHTML = "";
}

function render() {
  renderPackageSelect();
  renderStatus();
  renderTools();
  renderResources();
  renderPlan();
  renderDiagnostics();
}

function renderPackageSelect() {
  el.packageSelect.innerHTML = state.packages.map(pkg => {
    const label = `${pkg.packageId || pkg.relative} (${pkg.kind || "character"})`;
    return `<option value="${escapeHtml(pkg.relative)}"${pkg.relative === state.packageRelative ? " selected" : ""}>${escapeHtml(label)}</option>`;
  }).join("");
}

function renderStatus() {
  const connected = state.errors.length === 0 || state.characterState || state.authoringState;
  const characterPackage = state.characterState?.package?.manifest?.packageId || selectedPackageLabel();
  const resourceCount = Array.isArray(state.resources?.items) ? state.resources.items.length : 0;
  const providerCount = Array.isArray(state.resources?.providers) ? state.resources.providers.length : 0;
  const planStatus = getResourcePlanStatus();

  el.serverStatus.textContent = connected
    ? `已连接本机 Authoring 服务；默认包上下文：${characterPackage}`
    : "未连接 Authoring 服务。请通过启动脚本打开。";

  el.statusStrip.innerHTML = [
    statusChip("Authoring", connected ? "已连接" : "未连接", connected ? "ok" : "error"),
    statusChip("Buff Editor", state.authoringState ? "可用" : "不可用", state.authoringState ? "ok" : "warn"),
    statusChip("CharacterStudio", state.characterState?.package ? "可用" : "不可用", state.characterState?.package ? "ok" : "warn"),
    statusChip("资源 providers", String(providerCount), providerCount > 0 ? "ok" : "warn"),
    statusChip("资源项", String(resourceCount), resourceCount > 0 ? "ok" : "warn"),
    statusChip("资源计划", planStatus, planStatus === "Ready" ? "ok" : "warn")
  ].join("");
}

function renderTools() {
  const characterUrl = `/Tools/MxFramework.CharacterStudio/web/?package=${encodeURIComponent(state.packageRelative)}`;
  const resourceLibraryUrl = `/Tools/MxFramework.ResourceLibrary/web/?package=${encodeURIComponent(state.packageRelative)}`;
  const tools = [
    {
      title: "Buff Authoring Editor",
      subtitle: "Buff / ModPackage 示例编辑器",
      status: state.authoringState ? "可打开" : "服务未就绪",
      tone: state.authoringState ? "ok" : "warn",
      href: "/Tools/MxFramework.Authoring.Editor/web/",
      action: "打开 Buff 编辑器",
      details: [
        `Package: ${state.authoringState?.package?.packageId || state.authoringState?.package?.id || "sample"}`,
        "用途: Buff 配置、校验报告、合并预览"
      ]
    },
    {
      title: "CharacterStudio",
      subtitle: "角色资源包与装配编辑器",
      status: state.characterState?.package ? "可打开" : "服务未就绪",
      tone: state.characterState?.package ? "ok" : "warn",
      href: characterUrl,
      action: "打开角色编辑器",
      details: [
        `Package: ${state.characterState?.package?.manifest?.packageId || selectedPackageLabel()}`,
        "用途: 角色配置、挂点、碰撞体、字段级资源选择；资源来自资源管理器"
      ]
    },
    {
      title: "资源管理器",
      subtitle: "全局 Authoring Resource Manager",
      status: state.resources ? "可打开" : "服务未就绪",
      tone: state.resources ? "ok" : "warn",
      href: resourceLibraryUrl,
      action: "打开资源管理器",
      details: [
        `包筛选: ${state.characterState?.package?.manifest?.packageId || selectedPackageLabel()}`,
        "用途: provider 状态、资源浏览、inspect 详情、引用关系、运行时计划诊断"
      ]
    }
  ];

  el.toolGrid.innerHTML = tools.map(tool => {
    const action = tool.disabled
      ? `<button type="button" disabled>${escapeHtml(tool.action)}</button>`
      : `<a class="button" href="${escapeHtml(tool.href)}">${escapeHtml(tool.action)}</a>`;
    return `
      <article class="tool-card">
        <div class="tool-head">
          <div>
            <h2>${escapeHtml(tool.title)}</h2>
            <p>${escapeHtml(tool.subtitle)}</p>
          </div>
          <span class="badge ${tool.tone}">${escapeHtml(tool.status)}</span>
        </div>
        <ul>${tool.details.map(item => `<li>${escapeHtml(item)}</li>`).join("")}</ul>
        <div class="tool-actions">${action}</div>
      </article>`;
  }).join("");
}

function renderResources() {
  const items = Array.isArray(state.resources?.items) ? state.resources.items : [];
  const diagnostics = Array.isArray(state.resources?.diagnostics) ? state.resources.diagnostics : [];
  el.resourceSummary.textContent = items.length > 0
    ? `${items.length} 个资源项，${diagnostics.length} 条诊断`
    : "没有读取到资源管理器 API 数据。";

  if (items.length === 0) {
    el.resourceList.innerHTML = emptyBlock("未读取到资源项");
    return;
  }

  const topItems = items.slice(0, 8);
  el.resourceList.innerHTML = topItems.map(item => {
    const kind = item.kind || item.resourceKind || item.typeId || "unknown";
    const usage = item.usage || item.expectedUsage || "-";
    const runtime = item.runtimeAvailability || item.runtimeBindingKind || "Unknown";
    const status = item.importStatus || item.status || "Unknown";
    const title = item.displayName || item.stableId || item.resourceKey || item.libraryItemId || "resource";
    return `
      <div class="resource-row">
        <div>
          <strong>${escapeHtml(title)}</strong>
          <span>${escapeHtml(kind)} / ${escapeHtml(usage)}</span>
        </div>
        <div class="row-meta">
          <span>${escapeHtml(status)}</span>
          <span>${escapeHtml(runtime)}</span>
        </div>
      </div>`;
  }).join("") + (items.length > topItems.length ? `<div class="more">另有 ${items.length - topItems.length} 个资源项</div>` : "");
}

function renderPlan() {
  const plan = getCharacterResourcePlan();
  const groups = [
    ["SpawnCritical", plan?.spawnCritical],
    ["PresentationCritical", plan?.presentationCritical],
    ["EquipmentInitial", plan?.equipmentInitial],
    ["AnimationWarmup", plan?.animationWarmup],
    ["VfxWarmup", plan?.vfxWarmup],
    ["UiDeferred", plan?.uiDeferred],
    ["Audio", plan?.audio]
  ];

  const readable = groups.filter(([, group]) => group);
  el.planSummary.textContent = state.resourcePlan
    ? `状态 ${getResourcePlanStatus()}，${readable.length} 个计划分组`
    : "没有读取到资源计划 API 数据。";

  if (!state.resourcePlan) {
    el.planGrid.innerHTML = emptyBlock("未生成资源计划");
    return;
  }

  el.planGrid.innerHTML = groups.map(([name, group]) => renderPlanGroup(name, group)).join("");
}

function renderPlanGroup(name, group) {
  const resources = extractResourceKeys(group);
  const required = group?.required === true ? "必需" : (group?.required === false ? "可选" : "-");
  const failurePolicy = group?.failurePolicy || group?.policy || "-";
  return `
    <div class="plan-group">
      <div class="plan-title">
        <strong>${escapeHtml(name)}</strong>
        <span>${resources.length}</span>
      </div>
      <p>${escapeHtml(required)} / ${escapeHtml(failurePolicy)}</p>
      <ul>${resources.slice(0, 5).map(item => `<li>${escapeHtml(item)}</li>`).join("")}</ul>
      ${resources.length > 5 ? `<div class="more">另有 ${resources.length - 5} 项</div>` : ""}
    </div>`;
}

function extractResourceKeys(group) {
  if (!group) return [];
  if (Array.isArray(group)) return group.map(String);
  if (Array.isArray(group.resources)) return group.resources.map(item => typeof item === "string" ? item : (item.resourceKey || item.id || JSON.stringify(item)));
  if (Array.isArray(group.requiredCues)) return group.requiredCues.map(item => `cue:${item}`);
  if (Array.isArray(group.requiredBanks)) return group.requiredBanks.map(item => `bank:${item}`);
  return [];
}

function collectDiagnostics() {
  const all = [];
  if (Array.isArray(state.resources?.diagnostics)) all.push(...state.resources.diagnostics);
  if (Array.isArray(state.resourcePlan?.diagnostics)) all.push(...state.resourcePlan.diagnostics);
  if (Array.isArray(state.resourcePlan?.resourceValidationReport?.diagnostics)) all.push(...state.resourcePlan.resourceValidationReport.diagnostics);
  if (Array.isArray(state.characterState?.validation?.issues)) all.push(...state.characterState.validation.issues);
  for (const error of state.errors) {
    all.push({ severity: "Error", code: "EDITOR_HUB_API_UNAVAILABLE", message: `${error.label}: ${error.message}` });
  }
  return all;
}

function renderDiagnostics() {
  const diagnostics = getFilteredDiagnostics();
  const total = state.diagnostics.length;
  const errorCount = state.diagnostics.filter(item => getDiagnosticSeverity(item) === "Error").length;
  const warningCount = state.diagnostics.filter(item => getDiagnosticSeverity(item) === "Warning").length;

  el.diagnosticsSummary.textContent = total === 0
    ? "当前没有诊断信息。"
    : `${total} 条诊断，${errorCount} 个错误，${warningCount} 个警告`;

  if (diagnostics.length === 0) {
    const message = total === 0 ? "没有诊断信息" : "没有符合过滤条件的诊断";
    el.diagnosticsConsole.innerHTML = emptyBlock(message);
    return;
  }

  el.diagnosticsConsole.innerHTML = diagnostics.map(diagnostic => {
    const severity = getDiagnosticSeverity(diagnostic);
    const tone = severity.toLowerCase();
    const code = diagnostic.code || diagnostic.id || diagnostic.ruleId || "-";
    const message = diagnostic.message || diagnostic.description || diagnostic.detail || JSON.stringify(diagnostic);
    return `
      <div class="diag-row">
        <div class="col-type"><span class="diag-level-badge ${tone}">${escapeHtml(severity)}</span></div>
        <div class="col-code" title="${escapeHtml(code)}">${escapeHtml(code)}</div>
        <div class="col-msg">${escapeHtml(message)}</div>
      </div>`;
  }).join("");
}

function getFilteredDiagnostics() {
  const filter = state.diagnosticsFilter;
  const search = state.diagnosticsSearch.trim().toLowerCase();
  return state.diagnostics.filter(diagnostic => {
    const severity = getDiagnosticSeverity(diagnostic);
    if (filter !== "all" && severity !== filter) return false;
    if (!search) return true;
    return JSON.stringify(diagnostic).toLowerCase().includes(search);
  });
}

function getDiagnosticSeverity(diagnostic) {
  const raw = String(diagnostic?.severity || diagnostic?.level || diagnostic?.type || "Info").toLowerCase();
  if (raw.includes("error")) return "Error";
  if (raw.includes("warn")) return "Warning";
  return "Info";
}

function getCharacterResourcePlan() {
  return state.resourcePlan?.plan
    || state.resourcePlan?.characterResourcePlan
    || null;
}

function getResourcePlanStatus() {
  return state.resourcePlan?.status
    || state.resourcePlan?.resourceValidationReport?.status
    || (state.resourcePlan ? "Unknown" : "Missing");
}

async function copyDiagnostics() {
  const text = JSON.stringify({
    package: state.packageRelative,
    diagnostics: state.diagnostics
  }, null, 2);
  try {
    await navigator.clipboard.writeText(text);
    setCopyDiagnosticsLabel("已复制");
    setTimeout(() => { setCopyDiagnosticsLabel("复制 JSON 诊断"); }, 1200);
  } catch {
    window.prompt("复制诊断", text);
  }
}

function setCopyDiagnosticsLabel(text) {
  const label = el.copyDiagnosticsButton.querySelector("span");
  if (label) {
    label.textContent = text;
    return;
  }
  el.copyDiagnosticsButton.textContent = text;
}

function statusChip(label, value, tone) {
  return `<div class="status-chip ${tone}"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></div>`;
}

function emptyBlock(text) {
  return `<div class="empty">${escapeHtml(text)}</div>`;
}

function selectedPackageLabel() {
  const pkg = state.packages.find(item => item.relative === state.packageRelative);
  return pkg?.packageId || state.packageRelative;
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
