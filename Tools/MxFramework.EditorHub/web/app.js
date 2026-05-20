const DEFAULT_CHARACTER_PACKAGE = "Tools/MxFramework.Authoring/samples/character-iron-vanguard";

const state = {
  packages: [],
  packageRelative: DEFAULT_CHARACTER_PACKAGE,
  authoringState: null,
  characterState: null,
  resources: null,
  resourcePlan: null,
  diagnostics: [],
  errors: []
};

const el = {};

document.addEventListener("DOMContentLoaded", () => {
  for (const id of [
    "serverStatus", "packageSelect", "refreshButton", "statusStrip", "toolGrid",
    "resourceSummary", "copyDiagnosticsButton", "resourceList", "planSummary", "planGrid"
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
  state.resources = await readJson(`/api/character/resources?package=${encodeURIComponent(state.packageRelative)}`, null, "资源库状态");
}

async function loadResourcePlan() {
  state.resourcePlan = await readJson(`/api/character/resource-plan?package=${encodeURIComponent(state.packageRelative)}`, null, "资源计划");
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
}

function render() {
  renderPackageSelect();
  renderStatus();
  renderTools();
  renderResources();
  renderPlan();
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
  const planStatus = getResourcePlanStatus();

  el.serverStatus.textContent = connected
    ? `已连接本机 Authoring 服务，当前角色包：${characterPackage}`
    : "未连接 Authoring 服务。请通过启动脚本打开。";

  el.statusStrip.innerHTML = [
    statusChip("Authoring", connected ? "已连接" : "未连接", connected ? "ok" : "error"),
    statusChip("Buff Editor", state.authoringState ? "可用" : "不可用", state.authoringState ? "ok" : "warn"),
    statusChip("CharacterStudio", state.characterState?.package ? "可用" : "不可用", state.characterState?.package ? "ok" : "warn"),
    statusChip("资源项", String(resourceCount), resourceCount > 0 ? "ok" : "warn"),
    statusChip("资源计划", planStatus, planStatus === "Ready" ? "ok" : "warn")
  ].join("");
}

function renderTools() {
  const characterUrl = `/Tools/MxFramework.CharacterStudio/web/?package=${encodeURIComponent(state.packageRelative)}`;
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
        "用途: 角色配置、挂点、碰撞体、字段级资源选择"
      ]
    },
    {
      title: "资源库编辑器",
      subtitle: "独立资源库编辑器",
      status: "待实现",
      tone: "neutral",
      href: "",
      action: "待实现",
      details: [
        "当前可在下方查看资源库状态和运行时资源计划",
        "完整的导入、替换、删除、标签和引用图编辑应进入独立工具"
      ],
      disabled: true
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
    : "没有读取到资源库 API 数据。";

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
    el.copyDiagnosticsButton.textContent = "已复制";
    setTimeout(() => { el.copyDiagnosticsButton.textContent = "复制诊断"; }, 1200);
  } catch {
    window.prompt("复制诊断", text);
  }
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
