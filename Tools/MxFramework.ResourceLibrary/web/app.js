const DEFAULT_PACKAGE = "Tools/MxFramework.Authoring/samples/character-iron-vanguard";

const API = {
  packages: "/api/character/packages",
  resources: packageRelative => `/api/character/resources?package=${encodeURIComponent(packageRelative)}`,
  resourcePlan: (packageRelative, checkHashes = false) => {
    const suffix = checkHashes ? "&checkHashes=true" : "";
    return `/api/character/resource-plan?package=${encodeURIComponent(packageRelative)}${suffix}`;
  },
  inspect: (packageRelative, id) => `/api/character/resources/inspect?package=${encodeURIComponent(packageRelative)}&id=${encodeURIComponent(id)}`,
  importResource: "/api/character/resources/import",
  reimportResource: "/api/character/resources/reimport",
  replaceSource: "/api/character/resources/replace-source"
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

const FILTER_DEFAULTS = {
  search: "",
  kind: "all",
  usage: "all",
  sourceKind: "all",
  importStatus: "all",
  runtimeAvailability: "all",
  tag: "all",
  onlyReferenced: false,
  onlyOrphan: false,
  onlyRuntimeLoadable: false,
  onlyDiagnostics: false
};

const state = {
  packages: [],
  packageRelative: DEFAULT_PACKAGE,
  resourcesPayload: null,
  resourcePlanPayload: null,
  selectedResourceKey: "",
  activeTab: "overview",
  filters: { ...FILTER_DEFAULTS },
  inspectCache: new Map(),
  inspectState: { id: "", status: "idle", payload: null, error: "" },
  writeState: { status: "idle", action: "", error: "" },
  errors: [],
  lastActionMessage: ""
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
    "validateButton", "viewPlanButton", "statusStrip", "resourceSummary",
    "searchInput", "kindFilter", "usageFilter", "sourceFilter", "importFilter",
    "runtimeFilter", "tagFilter", "onlyReferenced", "onlyOrphan",
    "onlyRuntimeLoadable", "onlyDiagnostics", "clearFiltersButton", "resourceList",
    "previewTitle", "previewSubtitle", "previewBody", "resourcePlanPanel",
    "planSummary", "planGrid", "inspectorStatus", "inspectorContent",
    "resourceImportFileInput", "resourceReplaceFileInput", "importResourceButton",
    "reimportResourceButton", "replaceSourceButton", "deleteResourceButton",
    "editTagsButton", "writeActionStatus", "copyDetailJsonButton",
    "copyDiagnosticsJsonButton", "copyStatus"
  ]) {
    el[id] = document.getElementById(id);
  }
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
  el.viewPlanButton.addEventListener("click", () => {
    el.resourcePlanPanel.scrollIntoView({ behavior: "smooth", block: "start" });
  });

  const filterBindings = [
    ["searchInput", "search", "input"],
    ["kindFilter", "kind", "change"],
    ["usageFilter", "usage", "change"],
    ["sourceFilter", "sourceKind", "change"],
    ["importFilter", "importStatus", "change"],
    ["runtimeFilter", "runtimeAvailability", "change"],
    ["tagFilter", "tag", "change"]
  ];
  for (const [elementId, key, eventName] of filterBindings) {
    el[elementId].addEventListener(eventName, event => {
      state.filters[key] = event.target.value;
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
      render();
    });
  }
  el.clearFiltersButton.addEventListener("click", () => {
    state.filters = { ...FILTER_DEFAULTS };
    applyFilterControls();
    render();
  });

  el.resourceList.addEventListener("click", event => {
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
  el.importResourceButton.addEventListener("click", () => {
    el.resourceImportFileInput.value = "";
    el.resourceImportFileInput.click();
  });
  el.replaceSourceButton.addEventListener("click", () => {
    if (!getSelectedItem()) return;
    el.resourceReplaceFileInput.value = "";
    el.resourceReplaceFileInput.click();
  });
  el.reimportResourceButton.addEventListener("click", reimportSelectedResource);
  el.resourceImportFileInput.addEventListener("change", event => importResourceFile(event.target.files?.[0]));
  el.resourceReplaceFileInput.addEventListener("change", event => replaceSelectedResourceFile(event.target.files?.[0]));
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
    loadResourcePlan(false)
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

async function runResourceValidation() {
  state.lastActionMessage = "正在运行资源验证...";
  renderStatus();
  await loadResourcePlan(true);
  state.lastActionMessage = "资源验证已完成，详情见 resource plan 和 Diagnostics。";
  render();
}

async function importResourceFile(file) {
  if (!file) return;
  const kind = inferKindFromFileName(file.name);
  const usage = inferUsageForKind(kind);
  const bytesBase64 = await readFileAsBase64(file);
  await executeResourceWrite("import", API.importResource, {
    fileName: file.name,
    kind,
    usage,
    role: kind === "model" ? "preview" : "",
    bytesBase64
  }, "导入资源");
}

async function reimportSelectedResource() {
  const item = getSelectedItem();
  if (!item) return;
  const id = getResourceWriteId(item);
  if (!window.confirm(`重导资源 ${item.displayName || id}？`)) return;
  await executeResourceWrite("reimport", API.reimportResource, { id }, "重导资源");
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
  state.activeTab = "overview";
  state.inspectState = { id: "", status: "idle", payload: null, error: "" };
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
  el.resourceSummary.textContent = "正在读取资源库...";
  el.resourceList.innerHTML = emptyBlock("正在读取资源项");
  el.previewTitle.textContent = "资源摘要";
  el.previewSubtitle.textContent = "等待资源库数据";
  el.previewBody.innerHTML = "";
  el.planSummary.textContent = "正在读取 resource plan...";
  el.planGrid.innerHTML = "";
  el.inspectorStatus.textContent = "正在读取详情...";
  el.inspectorContent.innerHTML = emptyBlock("等待资源详情");
}

function render() {
  renderPackageSelect();
  renderStatus();
  renderFilters();
  renderBrowser();
  renderPreview();
  renderPlan();
  renderInspector();
  renderActions();
}

function renderPackageSelect() {
  el.packageSelect.innerHTML = state.packages.map(pkg => {
    const label = `${pkg.packageId || pkg.relative} (${pkg.version || pkg.kind || "character"})`;
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

  el.serverStatus.textContent = connected
    ? `已连接 Authoring 服务，当前角色包：${packageLabel}`
    : "未连接 Authoring 服务。请通过 Editor Hub 或启动脚本打开本工具。";

  const validationMessage = state.lastActionMessage
    ? statusChip("最近操作", state.lastActionMessage, "info")
    : "";
  el.statusStrip.innerHTML = [
    statusChip("Authoring", connected ? "已连接" : "未连接", connected ? "ok" : "error"),
    statusChip("资源项", String(items.length), items.length > 0 ? "ok" : "warn"),
    statusChip("诊断", String(diagnostics.length), diagnostics.some(d => getSeverity(d) === "Error") ? "error" : diagnostics.length > 0 ? "warn" : "ok"),
    statusChip("resource plan", planStatus, planStatus === "Ready" ? "ok" : state.resourcePlanPayload ? "warn" : "error"),
    validationMessage
  ].filter(Boolean).join("");
}

function renderFilters() {
  const items = getNormalizedItems();
  setSelectOptions(el.kindFilter, buildOptions(items, "kind"), state.filters.kind);
  setSelectOptions(el.usageFilter, buildOptions(items, "usage"), state.filters.usage);
  setSelectOptions(el.sourceFilter, buildOptions(items, "sourceKind"), state.filters.sourceKind);
  setSelectOptions(el.importFilter, buildOptions(items, "importStatus"), state.filters.importStatus);
  setSelectOptions(el.runtimeFilter, buildOptions(items, "runtimeAvailability"), state.filters.runtimeAvailability);
  setSelectOptions(el.tagFilter, buildTagOptions(items), state.filters.tag);
  applyFilterControls();
}

function renderBrowser() {
  const allItems = getNormalizedItems();
  const filtered = getFilteredItems(allItems);
  const selectedKey = state.selectedResourceKey;
  const diagnosticsCount = getAllDiagnostics().length;

  el.resourceSummary.textContent = allItems.length === 0
    ? "没有读取到资源库 API 数据。"
    : `显示 ${filtered.length} / ${allItems.length} 个资源项，${diagnosticsCount} 条诊断`;

  if (allItems.length === 0) {
    el.resourceList.innerHTML = emptyBlock("未读取到资源项。请确认 Authoring 服务已启动，并且当前包包含 resource_catalog.json。");
    return;
  }
  if (filtered.length === 0) {
    el.resourceList.innerHTML = emptyBlock("没有符合当前筛选条件的资源项。");
    return;
  }

  el.resourceList.innerHTML = filtered.map(item => {
    const active = item.key === selectedKey ? " active" : "";
    return `
      <button class="resource-row${active}" type="button" data-resource-key="${escapeHtml(item.key)}" role="option" aria-selected="${item.key === selectedKey}">
        <span class="resource-row-head">
          <strong>${escapeHtml(item.displayName || item.stableId || item.resourceKey || "resource")}</strong>
          <span>${escapeHtml(item.stableId || item.resourceKey || item.libraryItemId || "-")}</span>
        </span>
        <span class="resource-row-meta">
          ${smallBadge(item.kind || "unknown", "neutral")}
          ${smallBadge(item.usage || "-", "neutral")}
          ${smallBadge(item.sourceKind || "Unknown", "info")}
        </span>
        <span class="resource-row-meta">
          ${smallBadge(item.importStatus || "Unknown", toneForImportStatus(item.importStatus))}
          ${smallBadge(item.runtimeAvailability || "Unknown", toneForRuntime(item.runtimeAvailability))}
        </span>
        <span class="resource-row-foot">
          <span>引用 ${item.referenceCount}</span>
          <span>诊断 ${item.diagnosticCount}</span>
        </span>
      </button>`;
  }).join("");
}

function renderPreview() {
  const item = getSelectedItem();
  if (!item) {
    renderLibraryOverview();
    return;
  }

  el.previewTitle.textContent = item.displayName || item.libraryItemId || "资源详情";
  el.previewSubtitle.textContent = `${item.kind || "unknown"} / ${item.usage || "-"} / ${item.runtimeAvailability || "Unknown"}`;

  const preview = item.preview || {};
  const hasPreviewMetadata = Boolean(
    preview.thumbnailResourceKey
    || preview.previewMeshResourceKey
    || preview.previewCameraPresetId
    || preview.previewPoseId
    || preview.thumbnailUrl
    || preview.thumbnailPath
  );
  const previewLabel = hasPreviewMetadata
    ? renderKeyValueList([
      ["缩略图资源键", preview.thumbnailResourceKey || preview.thumbnailUrl || preview.thumbnailPath || "-"],
      ["预览 mesh", preview.previewMeshResourceKey || "-"],
      ["相机 preset", preview.previewCameraPresetId || "-"],
      ["预览姿势", preview.previewPoseId || "-"]
    ])
    : `<p class="empty-inline">未生成预览</p>`;

  el.previewBody.innerHTML = `
    <div class="preview-stage">
      <div class="preview-icon" aria-hidden="true">${escapeHtml(getKindInitial(item.kind))}</div>
      <div>
        <h3>${escapeHtml(kindTitle(item.kind))}</h3>
        ${previewLabel}
      </div>
    </div>
    <div class="summary-grid">
      ${metric("引用", item.referenceCount)}
      ${metric("诊断", item.diagnosticCount)}
      ${metric("导入状态", item.importStatus || "Unknown")}
      ${metric("运行时", item.runtimeAvailability || "Unknown")}
    </div>
    <div class="detail-card">
      <h3>轻量摘要</h3>
      ${renderKeyValueList([
        ["libraryItemId", item.libraryItemId || "-"],
        ["stableId", item.stableId || "-"],
        ["resourceKey", item.resourceKey || "-"],
        ["sourcePath", item.sourcePath || "-"],
        ["unityAssetPath", item.unityAssetPath || "-"],
        ["tags", item.tags.length > 0 ? item.tags.join(", ") : "-"]
      ])}
    </div>`;
}

function renderLibraryOverview() {
  const items = getNormalizedItems();
  const byKind = countBy(items, "kind");
  const byRuntime = countBy(items, "runtimeAvailability");

  el.previewTitle.textContent = "资源摘要";
  el.previewSubtitle.textContent = "选择左侧资源查看预览和关键状态";
  el.previewBody.innerHTML = `
    <div class="summary-grid">
      ${metric("资源项", items.length)}
      ${metric("运行时可用", items.filter(isRuntimeLoadable).length)}
      ${metric("已引用", items.filter(item => item.referenceCount > 0).length)}
      ${metric("有诊断", items.filter(item => item.diagnosticCount > 0).length)}
    </div>
    <div class="split-lists">
      <div class="detail-card">
        <h3>kind 分布</h3>
        ${renderCountList(byKind, "暂无 kind 数据")}
      </div>
      <div class="detail-card">
        <h3>runtime availability 分布</h3>
        ${renderCountList(byRuntime, "暂无 runtime availability 数据")}
      </div>
    </div>`;
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

function renderInspector() {
  for (const tab of el.inspectorTabs) {
    tab.classList.toggle("active", tab.dataset.tab === state.activeTab);
  }

  const item = getSelectedItem();
  if (!item) {
    el.inspectorStatus.textContent = "选择一个资源项";
    el.inspectorContent.innerHTML = emptyBlock("选择左侧资源后查看 Overview / Unity / Runtime / References / Diagnostics。");
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
}

function renderInspectorTab(tab, item, detail) {
  if (tab === "unity") return renderUnityTab(item, detail);
  if (tab === "runtime") return renderRuntimeTab(item, detail);
  if (tab === "references") return renderReferencesTab(detail);
  if (tab === "diagnostics") return renderDiagnosticsTab(detail);
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
    </section>
    <section class="inspector-section">
      <h3>原始 item JSON</h3>
      ${renderJsonBlock(item.raw)}
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

function renderDiagnosticsTab(detail) {
  const diagnostics = asArray(detail.diagnostics);
  return `
    <section class="inspector-section">
      <h3>Diagnostics</h3>
      ${renderDiagnosticsList(diagnostics)}
    </section>`;
}

function renderActions() {
  const item = getSelectedItem();
  const connected = Boolean(state.resourcesPayload);
  const writeBusy = state.writeState.status === "running";
  el.importResourceButton.disabled = !connected || writeBusy;
  el.reimportResourceButton.disabled = !connected || !item || writeBusy;
  el.replaceSourceButton.disabled = !connected || !item || writeBusy;
  el.deleteResourceButton.disabled = true;
  el.editTagsButton.disabled = true;
  el.importResourceButton.title = connected ? "通过 Authoring API 导入外部资源" : "Authoring 资源 API 未连接";
  el.reimportResourceButton.title = item ? "重新计算当前资源的导入状态和哈希" : "先选择一个资源项";
  el.replaceSourceButton.title = item ? "替换当前资源源文件，并保留 stableId/resourceKey" : "先选择一个资源项";
  if (state.writeState.status === "running") {
    el.writeActionStatus.textContent = "写入中：正在通过 Authoring API 更新资源库。";
  } else if (state.writeState.status === "error") {
    el.writeActionStatus.textContent = `写入失败：${state.writeState.error}`;
  } else if (item) {
    el.writeActionStatus.textContent = `当前目标：${item.displayName || getResourceWriteId(item)}；delete guard / tag update 仍锁定。`;
  } else {
    el.writeActionStatus.textContent = "已启用 import / reimport / replace-source；delete guard / tag update 仍锁定。";
  }
  el.copyDetailJsonButton.disabled = false;
  el.copyDiagnosticsJsonButton.disabled = false;
  el.copyDetailJsonButton.title = item ? "复制当前资源 inspect/fallback 详情" : "复制当前资源库摘要";
  el.copyDiagnosticsJsonButton.title = item ? "复制当前资源诊断" : "复制当前资源库全部诊断";
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

function inferKindFromFileName(fileName) {
  const format = inferFormatFromFileName(fileName);
  if (["glb", "gltf", "fbx"].includes(format)) return "model";
  if (["png", "jpg", "jpeg", "tga"].includes(format)) return "texture";
  if (["wav", "ogg"].includes(format)) return "audio";
  return "config";
}

function inferUsageForKind(kind) {
  if (kind === "model") return "previewMesh";
  if (kind === "texture") return "texture";
  if (kind === "audio") return "audioCue";
  if (kind === "animation") return "animationClipGroup";
  return "characterConfig";
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

function normalizeItem(raw, index) {
  const libraryItemId = stringValue(pick(raw, "libraryItemId", "id", "localId"));
  const stableId = stringValue(pick(raw, "stableId", "libraryItemStableId"));
  const resourceKey = stringValue(pick(raw, "resourceKey", "key"));
  const key = libraryItemId || stableId || resourceKey || `resource-${index}`;
  const tags = asArray(pick(raw, "tags")).map(String).filter(Boolean);
  const preview = pick(raw, "preview") || {};
  const diagnostics = asArray(pick(raw, "diagnostics"));
  const item = {
    key,
    raw,
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
    providerId: stringValue(pick(raw, "providerId", "provider")),
    hash: stringValue(pick(raw, "hash", "contentHash")),
    sourcePath: stringValue(pick(raw, "sourcePath", "relativePath", "path")),
    unityAssetPath: stringValue(pick(raw, "unityAssetPath")),
    fmodEventPath: stringValue(pick(raw, "fmodEventPath")),
    audioCueId: stringValue(pick(raw, "audioCueId")),
    audioEventDefinitionId: stringValue(pick(raw, "audioEventDefinitionId")),
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
    if (state.filters.kind !== "all" && item.kind !== state.filters.kind) return false;
    if (state.filters.usage !== "all" && item.usage !== state.filters.usage) return false;
    if (state.filters.sourceKind !== "all" && item.sourceKind !== state.filters.sourceKind) return false;
    if (state.filters.importStatus !== "all" && item.importStatus !== state.filters.importStatus) return false;
    if (state.filters.runtimeAvailability !== "all" && item.runtimeAvailability !== state.filters.runtimeAvailability) return false;
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
      item.sourceKind,
      item.importStatus,
      item.runtimeAvailability,
      item.resourceKey,
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
  if (state.inspectState.payload && state.inspectState.id) {
    return normalizeInspectPayload(state.inspectState.payload, item);
  }
  return buildFallbackInspect(item);
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
    const targetStableId = stringValue(pick(edge, "targetLibraryItemStableId", "targetStableId"));
    const targetResourceKey = stringValue(pick(edge, "targetResourceKey", "resourceKey"));
    return matchesItem(item, targetStableId, targetResourceKey);
  });
}

function getDiagnosticsForItem(item) {
  const diagnostics = [];
  diagnostics.push(...item.diagnostics);
  for (const diagnostic of getAllDiagnostics()) {
    const stableId = stringValue(pick(diagnostic, "libraryItemStableId", "targetLibraryItemStableId"));
    const resourceKey = stringValue(pick(diagnostic, "resourceKey", "targetResourceKey"));
    if (matchesItem(item, stableId, resourceKey)) {
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
  diagnostics.push(...resourcesDiagnostics, ...planDiagnostics, ...reportDiagnostics);
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

function isOrphanCandidate(item) {
  if (String(item.importStatus).toLowerCase() === "orphancandidate") return true;
  if (item.allDiagnostics.some(d => String(pick(d, "code")).toLowerCase().includes("orphan"))) return true;
  const graph = pick(state.resourcesPayload, "referenceGraph") || {};
  return asArray(pick(graph, "edges")).length > 0 && item.referenceCount === 0;
}

function matchesItem(item, stableId, resourceKey) {
  return Boolean(
    (stableId && item.stableId && stableId === item.stableId)
    || (resourceKey && item.resourceKey && resourceKey === item.resourceKey)
    || (resourceKey && item.libraryItemId && resourceKey === item.libraryItemId)
  );
}

function buildOptions(items, key) {
  const values = Array.from(new Set(items.map(item => item[key]).filter(Boolean))).sort(compareText);
  return [["all", "全部"], ...values.map(value => [value, value])];
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
    sourceFilter: "sourceKind",
    importFilter: "importStatus",
    runtimeFilter: "runtimeAvailability",
    tagFilter: "tag"
  }[id] || "";
}

function applyFilterControls() {
  el.searchInput.value = state.filters.search;
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
    return `
      <article class="diagnostic-row ${severity.toLowerCase()}">
        <div>
          <strong>${escapeHtml(severity)}</strong>
          <code>${escapeHtml(pick(diagnostic, "code") || "-")}</code>
        </div>
        <p>${escapeHtml(pick(diagnostic, "message") || pick(diagnostic, "description") || JSON.stringify(diagnostic))}</p>
        <small>${escapeHtml(pick(diagnostic, "suggestedFix") || pick(diagnostic, "sourceField") || "-")}</small>
      </article>`;
  }).join("")}</div>`;
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

function statusChip(label, value, tone) {
  return `<div class="status-chip ${tone || "neutral"}"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></div>`;
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
  return pkg?.packageId || state.packageRelative;
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
