const DEFAULT_CHARACTER_PACKAGE = "Tools/MxFramework.Authoring/samples/character-codex-flow-test";
const READINESS_STAGE_ORDER = ["CharacterPackage", "ResourceBinding", "AnimationBinding", "ReactionBinding", "ActionTimeline", "UnityImport", "RuntimeValidation", "AbilityRuntimeBinding"];
const state = {
  packages: [], packageRelative: "", readiness: null, characterState: null, resources: null, resourcePlan: null,
  actionDamageAnalysis: null, diagnostics: [], diagnosticsSearch: "", diagnosticsFilter: "all", errors: [], request: 0
};
const el = {};
document.addEventListener("DOMContentLoaded", () => {
  for (const id of ["serverStatus", "refreshButton", "packageSelect", "readinessSummary", "readinessFlow", "characterDetailsLink",
    "diagnosticsSummary", "diagnosticsSearch", "diagnosticsFilter", "diagnosticsConsole", "copyDiagnosticsButton"]) el[id] = document.getElementById(id);
  state.packageRelative = new URLSearchParams(window.location.search).get("package") || Hub.readPreferences().targets?.character || "";
  el.refreshButton.addEventListener("click", () => refresh({ reloadPackages: true }));
  el.packageSelect.addEventListener("change", () => {
    state.packageRelative = el.packageSelect.value;
    Hub.rememberTarget("character", state.packageRelative);
    const url = new URL(window.location.href);
    url.searchParams.set("package", state.packageRelative);
    window.history.replaceState(null, "", url);
    refresh();
  });
  el.diagnosticsSearch.addEventListener("input", () => { state.diagnosticsSearch = el.diagnosticsSearch.value; renderDiagnostics(); });
  el.diagnosticsFilter.addEventListener("change", () => { state.diagnosticsFilter = el.diagnosticsFilter.value; renderDiagnostics(); });
  el.copyDiagnosticsButton.addEventListener("click", copyDiagnostics);
  refresh({ reloadPackages: true });
});
async function loadPackages() {
  const packages = await Hub.readJson("/api/character/packages");
  if (!Array.isArray(packages)) throw new Error("角色列表无法读取。");
  state.packages = packages;
  if (!packages.some(pkg => pkg.relative === state.packageRelative)) {
    state.packageRelative = (packages.find(pkg => !pkg.isTemplate) || packages[0])?.relative || "";
  }
}
async function loadReadiness(packageRelative = state.packageRelative) {
  if (isSelectedTemplate(packageRelative)) return null;
  return Hub.readJson(`/api/character/readiness?package=${encodeURIComponent(packageRelative)}`);
}
async function loadResourcePlan(packageRelative = state.packageRelative) {
  if (isSelectedTemplate(packageRelative)) return null;
  return Hub.readJson(`/api/authoring/resources/resource-plan?package=${encodeURIComponent(packageRelative)}`);
}
function isSelectedTemplate(packageRelative = state.packageRelative) {
  return state.packages.some(pkg => pkg.relative === packageRelative && pkg.isTemplate === true);
}
function isHttpServed() { return window.location.protocol === "http:" || window.location.protocol === "https:"; }
function escapeHtml(value) { return Hub.escapeHtml(value); }
function emptyBlock(text) { return `<div class="empty">${escapeHtml(text)}</div>`; }
async function refresh({ reloadPackages = false } = {}) {
  const request = ++state.request;
  el.refreshButton.disabled = true;
  el.serverStatus.textContent = "正在读取诊断…";
  el.serverStatus.className = "service-status";
  el.readinessSummary.textContent = "正在读取…";
  el.readinessFlow.innerHTML = "";
  el.diagnosticsConsole.innerHTML = "";
  el.diagnosticsSummary.textContent = "正在读取…";
  el.copyDiagnosticsButton.disabled = true;
  state.diagnostics = [];
  try {
    if (reloadPackages || !state.packages.length) {
      el.packageSelect.disabled = true;
      await loadPackages();
    }
    if (request !== state.request) return;
    el.packageSelect.innerHTML = state.packages.map(pkg => `<option value="${escapeHtml(pkg.relative)}">${escapeHtml(Hub.packageLabel(pkg))}</option>`).join("");
    el.packageSelect.value = state.packageRelative;
    el.packageSelect.disabled = false;
    if (!state.packageRelative) throw new Error("没有角色包，请返回首页新建角色。");
    el.characterDetailsLink.href = Hub.toolUrl("character", state.packageRelative);
    const packageRelative = state.packageRelative;
    const encoded = encodeURIComponent(packageRelative);
    const tasks = [
      ["characterState", "角色源数据", () => Hub.readJson(`/api/character/state?package=${encoded}`)],
      ["readiness", "角色交付检查", () => loadReadiness(packageRelative)],
      ["resources", "资源引用", () => Hub.readJson(`/api/authoring/resources?package=${encoded}`)],
      ["resourcePlan", "资源计划", () => loadResourcePlan(packageRelative)],
      ["actionDamageAnalysis", "动作伤害分析", () => Hub.readJson("/api/character-actions/damage-analysis")]
    ];
    const results = await Promise.allSettled(tasks.map(([, , load]) => load()));
    if (request !== state.request) return;
    state.errors = [];
    results.forEach((result, index) => {
      const [key, label] = tasks[index];
      state[key] = result.status === "fulfilled" ? result.value : null;
      if (result.status === "rejected") state.errors.push({ label, message: result.reason.message });
    });
    state.diagnostics = collectDiagnostics();
    el.serverStatus.textContent = state.errors.length ? "部分诊断未能读取，请查看错误或刷新。" : "服务已连接";
    el.serverStatus.classList.add(state.errors.length ? "failed" : "connected");
    renderReadiness();
    renderDiagnostics();
  } catch (error) {
    if (request !== state.request) return;
    state.readiness = null;
    state.errors = [{ label: "服务", message: error.message }];
    state.diagnostics = [{ severity: "Error", code: "EDITOR_HUB_API_UNAVAILABLE", message: error.message }];
    el.serverStatus.textContent = "诊断读取失败，请刷新或返回首页重启服务。";
    el.serverStatus.classList.add("failed");
    renderReadiness();
    renderDiagnostics();
  } finally {
    if (request === state.request) {
      el.refreshButton.disabled = false;
      el.packageSelect.disabled = !state.packages.length;
      el.copyDiagnosticsButton.disabled = false;
    }
  }
}
function readinessLabel(status) {
  return ({ Ready: "已就绪", Deferred: "待完成", Blocked: "已阻断", Warning: "有警告", Unknown: "未确认", Missing: "缺失" })[status] || status || "未确认";
}
function getReadinessTone(status) {
  return status === "Ready" ? "ok" : status === "Blocked" ? "error" : status === "Warning" || status === "Deferred" ? "warn" : "pending";
}
function renderReadiness() {
  if (isSelectedTemplate()) {
    el.readinessSummary.textContent = "当前是角色模板：运行就绪检查在创建正式角色后执行。";
    el.readinessFlow.innerHTML = emptyBlock("角色源数据校验结果仍显示在诊断信息中。");
    return;
  }
  const snapshot = state.readiness;
  if (!snapshot) {
    el.readinessSummary.textContent = "未能读取角色交付检查。";
    el.readinessFlow.innerHTML = emptyBlock("请查看诊断错误并重新加载。");
    return;
  }
  el.readinessSummary.textContent = `${snapshot.packageId} · ${readinessLabel(snapshot.overallStatus)} · ${snapshot.blockers?.length || 0} 个阻断项，${snapshot.warnings?.length || 0} 个警告`;
  const stages = [...(snapshot.stages || [])].sort((a, b) => {
    const index = id => { const value = READINESS_STAGE_ORDER.indexOf(id); return value < 0 ? 99 : value; };
    return index(a.id) - index(b.id);
  });
  el.readinessFlow.innerHTML = stages.map(renderReadinessStage).join("") || emptyBlock("没有返回交付阶段。");
}
function renderReadinessStage(stage) {
  const status = stage.status || "Unknown";
  const characterUrl = Hub.toolUrl("character", state.packageRelative);
  const isImport = stage.id === "UnityImport" || stage.id === "RuntimeValidation";
  const url = isImport || stage.id === "ActionTimeline" ? characterUrl : stage.primaryToolUrl;
  const actionLabel = isImport ? "前往角色编辑器查看导入" : stage.id === "ActionTimeline" ? "查看角色动作" : stage.id === "ReactionBinding" ? "查看反应绑定" : stage.actionLabel || "打开对应工具";
  const issues = [...(stage.blockers || []), ...(stage.warnings || [])];
  return `<details class="readiness-stage" ${status === "Blocked" || status === "Warning" ? "open" : ""}>
    <summary><span class="stage-label">${escapeHtml(stage.id === "ReactionBinding" ? "反应绑定" : stage.label || stage.id)}</span><span class="badge ${getReadinessTone(status)}">${escapeHtml(readinessLabel(status))}</span></summary>
    <div class="stage-body">
      <p>${escapeHtml(stage.summary)}</p>
      ${issues.map(issue => `<p>${escapeHtml(issue.message)} <code>${escapeHtml(issue.code)}</code></p>`).join("")}
      ${url ? `<a class="text-link" href="${escapeHtml(url)}">${escapeHtml(actionLabel)} ↗</a>` : ""}
      <details class="evidence"><summary>查看技术证据</summary>
        ${Object.entries(stage.metrics || {}).map(([key, value]) => `<div><code>${escapeHtml(key)}=${escapeHtml(value)}</code></div>`).join("")}
        ${[...new Set([...(stage.sourcePaths || []), ...(stage.reportRefs || []).map(ref => ref.path)].filter(Boolean))].map(path => `<div><code>${escapeHtml(path)}</code></div>`).join("")}
      </details>
    </div>
  </details>`;
}
function collectDiagnostics() {
  const all = [];
  const append = (items, source) => {
    if (Array.isArray(items)) all.push(...items.map(item => ({ ...item, reportedBy: [source] })));
  };
  if (isSelectedTemplate()) {
    all.push({ severity: "Info", code: "EDITOR_HUB_TEMPLATE_CONTEXT", message: "当前是新建角色模板。源数据仍需校验；运行身份、Unity 导入与运行资源计划在创建正式角色后验证。" });
  }
  if (!isHttpServed()) {
    all.push({
      severity: "Warning",
      code: "EDITOR_HUB_STATIC_FILE_MODE",
      message: "Editor Hub is opened as a file:// page. Start it through Tools/MxFramework.EditorHub/start-editor-hub so local Authoring APIs are available."
    });
  }
  append(state.resources?.diagnostics, "资源引用");
  append(state.actionDamageAnalysis?.diagnostics, "动作伤害分析");
  append(state.resourcePlan?.diagnostics, "资源计划");
  append(state.resourcePlan?.resourceValidationReport?.diagnostics, "资源计划校验");
  append(state.characterState?.validation?.issues, "角色源数据");
  append(state.readiness?.blockers, "角色交付流");
  append(state.readiness?.warnings, "角色交付流");
  for (const error of state.errors) {
    all.push({ severity: "Error", code: "EDITOR_HUB_API_UNAVAILABLE", message: `${error.label}: ${error.message}` });
  }
  return collapseNoisyDiagnostics(deduplicateDiagnostics(all));
}

function deduplicateDiagnostics(diagnostics) {
  const byIdentity = new Map();
  for (const item of diagnostics) {
    const path = item.sourcePath || (String(item.sourceConfigKind || "").includes("/") ? item.sourceConfigKind : "");
    const field = item.sourceField || item.sourceObjectPath || item.field || "";
    const identity = item.resourceStableId || item.resourceKey || item.runtimeResourceKey || item.resourceId || item.libraryItemStableId || item.sourceStableId || item.providerId || "";
    const key = JSON.stringify([getDiagnosticSeverity(item), item.code || item.id || item.ruleId || "", path.replaceAll("\\", "/"), field, identity, item.message || item.description || ""]);
    const previous = byIdentity.get(key);
    if (previous) previous.reportedBy = [...new Set([...(previous.reportedBy || []), ...(item.reportedBy || [])])];
    else byIdentity.set(key, { ...item });
  }
  return [...byIdentity.values()];
}

function collapseNoisyDiagnostics(diagnostics) {
  const collapsedCodes = new Set([
    "AUTH_RES_ORPHAN_CANDIDATE",
    "AUTH_RES_AUDIO_AUDITION_REQUIRED",
    "AUTH_RES_AUDIO_AUDITION_RECOMMENDED"
  ]);
  const result = [];
  const groups = new Map();

  for (const diagnostic of diagnostics) {
    const code = diagnostic?.code || diagnostic?.id || diagnostic?.ruleId || "-";
    if (!collapsedCodes.has(code)) {
      result.push(diagnostic);
      continue;
    }

    if (!groups.has(code)) groups.set(code, []);
    groups.get(code).push(diagnostic);
  }

  for (const [code, group] of groups) {
    const first = group[0] || {};
    const samples = group
      .map(item => item.runtimeResourceKey || item.resourceStableId || item.resourceId || item.sourceField || "")
      .filter(Boolean)
      .slice(0, 5);
    const label = code === "AUTH_RES_ORPHAN_CANDIDATE"
      ? "resources have no incoming references in the current context"
      : "Active Cues have no current optional Unity/FMOD audition review";
    result.push({
      severity: code === "AUTH_RES_ORPHAN_CANDIDATE" ? "Warning" : "Info",
      code,
      message: `${group.length} ${label}.${samples.length > 0 ? ` Examples: ${samples.join(", ")}.` : ""}`,
      affectedCount: group.length,
      samples,
      suggestedFix: first.suggestedFix || "",
      members: group
    });
  }

  return result;
}


function renderDiagnostics() {
  const diagnostics = getFilteredDiagnostics();
  const total = state.diagnostics.length;
  const errorCount = state.diagnostics.filter(item => getDiagnosticSeverity(item) === "Error").length;
  const warningCount = state.diagnostics.filter(item => getDiagnosticSeverity(item) === "Warning").length;
  el.diagnosticsSummary.textContent = total ? `${total} 组诊断，${errorCount} 个错误，${warningCount} 个警告` : "当前没有诊断信息。";
  el.diagnosticsConsole.innerHTML = diagnostics.map(diagnostic => {
    const severity = getDiagnosticSeverity(diagnostic);
    const label = ({ Error: "错误", Warning: "警告", Info: "提示" })[severity];
    const tone = ({ Error: "error", Warning: "warn", Info: "pending" })[severity];
    let message = diagnostic.message || diagnostic.description || diagnostic.detail || "";
    if (diagnostic.code === "AUTH_RES_ORPHAN_CANDIDATE") message = `${diagnostic.affectedCount} 个资源在当前角色上下文没有引用；不表示它们可删除或在其它角色中无用。`;
    if (diagnostic.code === "AUTH_RES_AUDIO_AUDITION_RECOMMENDED") message = `${diagnostic.affectedCount} 个音效尚无当前版本的可选试听记录；不会因此阻断发布。`;
    return `<article class="diag-row">
      <div class="diag-header"><span class="badge ${tone}">${label}</span><code>${escapeHtml(diagnostic.code || diagnostic.id || diagnostic.ruleId || "-")}</code></div>
      <p class="diag-message">${escapeHtml(message)}</p>
      <details><summary class="muted">查看完整诊断${diagnostic.affectedCount ? `（${diagnostic.affectedCount} 项）` : ""}</summary><pre>${escapeHtml(JSON.stringify(diagnostic.members || diagnostic, null, 2))}</pre></details>
    </article>`;
  }).join("") || emptyBlock(total ? "没有符合过滤条件的诊断。" : "没有诊断信息。");
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


async function copyDiagnostics() {
  const text = JSON.stringify({ package: state.packageRelative, diagnostics: state.diagnostics }, null, 2);
  try {
    await navigator.clipboard.writeText(text);
    el.copyDiagnosticsButton.textContent = "已复制";
    setTimeout(() => { el.copyDiagnosticsButton.textContent = "复制 JSON 诊断"; }, 1200);
  } catch { window.prompt("复制诊断", text); }
}
