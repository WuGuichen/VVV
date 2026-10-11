const DEFAULT_LIBRARY_ID = "MxUiFoundation";
const EXPECTED_API_VERSION = "mx.uiArtResources.authoring.v1";

const CATEGORY_OPTIONS = [
  "ButtonBase",
  "PanelFrame",
  "ButtonState",
  "Icon",
  "BarTrack",
  "BarFill",
  "Divider",
  "Badge",
  "InputBackground",
  "Decoration",
  "Ignored"
];

const CATEGORY_LABELS = {
  ButtonBase: "按钮底图",
  PanelFrame: "面板框体",
  ButtonState: "按钮状态",
  Icon: "图标",
  BarTrack: "进度条底",
  BarFill: "进度条填充",
  Divider: "分隔线",
  Badge: "徽标",
  InputBackground: "输入背景",
  Decoration: "装饰",
  Ignored: "忽略"
};

const USAGE_POLICY_OPTIONS = [
  "Fixed",
  "PreserveAspect",
  "Stretch",
  "HorizontalFill",
  "VerticalFill",
  "NineSlice",
  "Tile"
];

const USAGE_POLICY_LABELS = {
  Fixed: "固定尺寸",
  PreserveAspect: "保持比例",
  Stretch: "拉伸",
  HorizontalFill: "横向填充",
  VerticalFill: "纵向填充",
  NineSlice: "九宫格",
  Tile: "平铺"
};

const FORM_INPUT_IDS = [
  "categoryInput",
  "displayNameInput",
  "descriptionInput",
  "tagsInput",
  "usagePolicyInput",
  "designWidthInput",
  "designHeightInput",
  "scale9XInput",
  "scale9YInput",
  "scale9WidthInput",
  "scale9HeightInput",
  "preserveAspectInput",
  "requireAlphaInput",
  "requireNonOpaqueInput",
  "requireSameSizeInput",
  "allowStretchInput",
  "replaceTogetherInput",
  "minWidthInput",
  "minHeightInput"
];

const CREATE_INPUT_IDS = [
  "createResourceIdInput",
  "createRelativePathInput",
  "createDisplayNameInput",
  "createDescriptionInput",
  "createCategoryInput",
  "createTagsInput",
  "createPolicyInput",
  "createDesignWidthInput",
  "createDesignHeightInput",
  "createScale9XInput",
  "createScale9YInput",
  "createScale9WidthInput",
  "createScale9HeightInput",
  "createRequireAlphaInput",
  "createRequireNonOpaqueInput",
  "createRequireSameSizeInput",
  "createAllowStretchInput",
  "createReplaceTogetherInput",
  "createMinWidthInput",
  "createMinHeightInput"
];

const state = {
  libraries: [],
  libraryId: DEFAULT_LIBRARY_ID,
  artState: null,
  apiCapabilities: null,
  integrationPackages: [],
  integrationPlan: null,
  integrationJson: "",
  integrationMarkdown: "",
  selectedResourceId: "",
  selectedImageResourceId: "",
  selectedImageInfo: null,
  search: "",
  category: "",
  replacementDataUrl: "",
  replacementFileName: "",
  replacementFileMeta: "",
  replacementPlan: null,
  createDataUrl: "",
  createFileName: "",
  createImageInfo: null,
  createPlan: null,
  diagnosticsScope: "selected",
  inspectorTab: "basic",
  previewMode: "fit",
  checkerVisible: true,
  formDirty: false,
  formBaseline: "",
  toastTimer: 0
};

const el = {};

document.addEventListener("DOMContentLoaded", () => {
  for (const id of [
    "statusText", "editorStateBadge", "packageSelect", "refreshButton", "summaryStrip", "sliceResultText",
    "clearFiltersButton", "openCreateButton", "searchInput", "categoryFilter", "sliceList", "sliceTitle", "sliceBadge",
    "previewFrame", "slicePreview", "scale9Overlay", "previewFitButton", "previewActualButton",
    "checkerToggleButton", "openReplaceButton", "sliceIdText", "resourceIdText", "sourcePathText",
    "catalogHashText", "sliceIssueSummary",
    "categoryInput", "displayNameInput", "descriptionInput", "tagsInput", "usagePolicyInput",
    "designWidthInput", "designHeightInput", "scale9XInput", "scale9YInput", "scale9WidthInput",
    "scale9HeightInput", "preserveAspectInput", "requireAlphaInput", "requireNonOpaqueInput",
    "requireSameSizeInput", "allowStretchInput", "replaceTogetherInput", "minWidthInput", "minHeightInput",
    "saveButton", "discardButton", "editStatusText", "preflightDetails", "preflightCount", "preflightList",
    "integrationStatusBadge", "integrationPackageSelect", "generateIntegrationPlanButton", "integrationPlanOutput",
    "copyIntegrationRecipeButton", "downloadIntegrationJsonButton", "downloadIntegrationMarkdownButton",
    "replacementDropZone", "replacementInput", "replacementFileName", "replacementFileMeta",
    "planReplaceButton", "replaceButton", "planOutput", "replacementDrawer", "replacementBackdrop",
    "closeReplacementButton", "diagnosticDrawer", "diagnosticScopeControl", "diagnosticCount",
    "diagnosticsList", "createResourceIdInput", "createRelativePathInput", "createDisplayNameInput",
    "createDescriptionInput", "createCategoryInput", "createTagsInput", "createPolicyInput",
    "createDesignWidthInput", "createDesignHeightInput", "createScale9XInput", "createScale9YInput",
    "createScale9WidthInput", "createScale9HeightInput", "createRequireAlphaInput", "createRequireNonOpaqueInput",
    "createRequireSameSizeInput", "createAllowStretchInput", "createReplaceTogetherInput", "createMinWidthInput",
    "createMinHeightInput", "createDropZone", "createFileInput",
    "createFileName", "createFileMeta", "planCreateButton", "createResourceButton", "createPlanOutput",
    "createDrawer", "createBackdrop", "closeCreateButton", "toast"
  ]) {
    el[id] = document.getElementById(id);
  }

  const query = new URLSearchParams(window.location.search);
  state.libraryId = query.get("library") || query.get("package") || DEFAULT_LIBRARY_ID;

  setupOptions();
  bindEvents();
  refresh();
});

function setupOptions() {
  el.categoryFilter.innerHTML = `<option value="">全部分类</option>${renderOptions(CATEGORY_OPTIONS, categoryLabel)}`;
  el.categoryInput.innerHTML = renderOptions(CATEGORY_OPTIONS, categoryLabel);
  el.createCategoryInput.innerHTML = renderOptions(CATEGORY_OPTIONS, categoryLabel);
  el.usagePolicyInput.innerHTML = renderOptions(USAGE_POLICY_OPTIONS, usagePolicyLabel);
  el.createPolicyInput.innerHTML = renderOptions(USAGE_POLICY_OPTIONS, usagePolicyLabel);
  el.createCategoryInput.value = "ButtonBase";
  el.createPolicyInput.value = "NineSlice";
}

function bindEvents() {
  el.refreshButton.addEventListener("click", () => {
    if (confirmDiscardDirty()) refresh();
  });

  el.packageSelect.addEventListener("change", event => {
    if (!confirmDiscardDirty()) {
      event.target.value = state.libraryId;
      return;
    }

    state.libraryId = event.target.value;
    state.selectedResourceId = "";
    refresh({ keepLibraries: true });
  });

  el.clearFiltersButton.addEventListener("click", () => {
    state.search = "";
    state.category = "";
    el.searchInput.value = "";
    el.categoryFilter.value = "";
    renderResourceList();
  });

  el.searchInput.addEventListener("input", event => {
    state.search = event.target.value;
    renderResourceList();
  });

  el.categoryFilter.addEventListener("change", event => {
    state.category = event.target.value;
    renderResourceList();
  });

  for (const id of FORM_INPUT_IDS) {
    el[id].addEventListener("input", syncFormDirty);
    el[id].addEventListener("change", syncFormDirty);
  }

  el.saveButton.addEventListener("click", saveSelectedResource);
  el.discardButton.addEventListener("click", discardFormEdits);

  for (const button of document.querySelectorAll("button[data-inspector-tab]")) {
    button.addEventListener("click", () => selectInspectorTab(button.dataset.inspectorTab));
  }

  el.integrationPackageSelect.addEventListener("change", resetIntegrationPlan);
  el.generateIntegrationPlanButton.addEventListener("click", generateIntegrationPlan);
  el.copyIntegrationRecipeButton.addEventListener("click", () => copyTextToClipboard(state.integrationMarkdown, "接入配方已复制。"));
  el.downloadIntegrationJsonButton.addEventListener("click", () => downloadTextRecipe(state.integrationJson, integrationFileName("json"), "application/json"));
  el.downloadIntegrationMarkdownButton.addEventListener("click", () => downloadTextRecipe(state.integrationMarkdown, integrationFileName("md"), "text/markdown"));

  el.previewFitButton.addEventListener("click", () => setPreviewMode("fit"));
  el.previewActualButton.addEventListener("click", () => setPreviewMode("actual"));
  el.checkerToggleButton.addEventListener("click", toggleCheckerboard);
  for (const button of document.querySelectorAll("button[data-copy-target]")) {
    button.addEventListener("click", () => copyMetadata(button.dataset.copyTarget));
  }
  for (const line of el.scale9Overlay.querySelectorAll("button[data-scale9-edge]")) {
    line.addEventListener("pointerdown", beginScale9Drag);
  }

  bindDropZone(el.replacementDropZone, el.replacementInput, loadReplacementFile);
  el.planReplaceButton.addEventListener("click", planReplacement);
  el.replaceButton.addEventListener("click", replacePng);
  el.openReplaceButton.addEventListener("click", () => openOperationDrawer("replacement"));
  el.closeReplacementButton.addEventListener("click", closeReplacementDrawer);
  el.replacementBackdrop.addEventListener("click", closeReplacementDrawer);

  bindDropZone(el.createDropZone, el.createFileInput, loadCreateFile);
  el.createResourceIdInput.addEventListener("input", syncCreateDefaultsFromId);
  el.createCategoryInput.addEventListener("change", syncCreateDefaultsFromCategory);
  el.createPolicyInput.addEventListener("change", syncCreatePolicyFields);
  for (const id of CREATE_INPUT_IDS) {
    el[id].addEventListener("input", clearCreatePlan);
    el[id].addEventListener("change", clearCreatePlan);
  }
  el.planCreateButton.addEventListener("click", planCreateResource);
  el.createResourceButton.addEventListener("click", createResource);
  el.openCreateButton.addEventListener("click", () => openOperationDrawer("create"));
  el.closeCreateButton.addEventListener("click", closeCreateDrawer);
  el.createBackdrop.addEventListener("click", closeCreateDrawer);

  for (const button of el.diagnosticScopeControl.querySelectorAll("button[data-scope]")) {
    button.addEventListener("click", () => {
      state.diagnosticsScope = button.dataset.scope;
      renderDiagnostics();
    });
  }

  el.preflightDetails.addEventListener("toggle", () => {
    if (!el.preflightDetails.open) return;
    requestAnimationFrame(() => el.preflightDetails.scrollIntoView({ block: "nearest" }));
  });

  document.addEventListener("keydown", event => {
    if (event.key === "Escape") {
      if (el.createDrawer.classList.contains("open")) closeCreateDrawer();
      else if (el.replacementDrawer.classList.contains("open")) closeReplacementDrawer();
      return;
    }

    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
      event.preventDefault();
      if (!el.saveButton.disabled) saveSelectedResource();
    }
  });

  window.addEventListener("beforeunload", event => {
    if (!state.formDirty && !state.replacementDataUrl && !hasCreateDraft()) return;
    event.preventDefault();
    event.returnValue = "";
  });
  window.addEventListener("resize", renderScale9Overlay);
  el.previewFrame.addEventListener("scroll", renderScale9Overlay);
}

function bindDropZone(dropZone, input, loader) {
  input.addEventListener("change", () => loader(input.files && input.files[0]));
  dropZone.addEventListener("dragover", event => {
    event.preventDefault();
    dropZone.classList.add("drag-over");
  });
  dropZone.addEventListener("dragleave", () => dropZone.classList.remove("drag-over"));
  dropZone.addEventListener("drop", event => {
    event.preventDefault();
    dropZone.classList.remove("drag-over");
    loader(event.dataTransfer?.files && event.dataTransfer.files[0]);
  });
}

function selectInspectorTab(tab) {
  state.inspectorTab = tab || "basic";
  for (const button of document.querySelectorAll("button[data-inspector-tab]")) {
    const selected = button.dataset.inspectorTab === state.inspectorTab;
    button.classList.toggle("selected", selected);
    button.setAttribute("aria-selected", selected ? "true" : "false");
  }
  for (const panel of document.querySelectorAll("[data-inspector-panel]")) {
    panel.hidden = panel.dataset.inspectorPanel !== state.inspectorTab;
  }
}

function setPreviewMode(mode) {
  state.previewMode = mode === "actual" ? "actual" : "fit";
  el.previewFrame.classList.toggle("actual-size", state.previewMode === "actual");
  el.previewFitButton.classList.toggle("selected", state.previewMode === "fit");
  el.previewActualButton.classList.toggle("selected", state.previewMode === "actual");
  requestAnimationFrame(renderScale9Overlay);
}

function toggleCheckerboard() {
  state.checkerVisible = !state.checkerVisible;
  el.previewFrame.classList.toggle("no-checker", !state.checkerVisible);
  el.checkerToggleButton.classList.toggle("selected", state.checkerVisible);
  el.checkerToggleButton.setAttribute("aria-pressed", state.checkerVisible ? "true" : "false");
}

function openOperationDrawer(kind) {
  const create = kind === "create";
  const drawer = create ? el.createDrawer : el.replacementDrawer;
  const backdrop = create ? el.createBackdrop : el.replacementBackdrop;
  if (!create && !findResource(state.selectedResourceId)) {
    showToast("请先选择一个资源。");
    return;
  }
  drawer.hidden = false;
  drawer.classList.add("open");
  drawer.inert = false;
  drawer.setAttribute("aria-hidden", "false");
  backdrop.hidden = false;
  document.body.classList.add("drawer-open");
  requestAnimationFrame(() => (create ? el.createFileInput : el.replacementInput).focus());
}

function closeOperationDrawer(kind) {
  const create = kind === "create";
  const drawer = create ? el.createDrawer : el.replacementDrawer;
  const backdrop = create ? el.createBackdrop : el.replacementBackdrop;
  drawer.classList.remove("open");
  drawer.inert = true;
  drawer.setAttribute("aria-hidden", "true");
  drawer.hidden = true;
  backdrop.hidden = true;
  if (!el.createDrawer.classList.contains("open") && !el.replacementDrawer.classList.contains("open")) {
    document.body.classList.remove("drawer-open");
  }
}

function closeReplacementDrawer() {
  if (state.replacementDataUrl && !window.confirm("放弃当前 PNG 替换候选文件？")) return;
  resetReplacementState();
  renderPlan(null, el.planOutput);
  renderSelectedResource();
  closeOperationDrawer("replacement");
}

function closeCreateDrawer() {
  if (hasCreateDraft() && !window.confirm("放弃当前新增资源草稿？")) return;
  resetCreateState();
  closeOperationDrawer("create");
}

function hasCreateDraft() {
  return Boolean(state.createDataUrl || el.createResourceIdInput.value.trim() || el.createRelativePathInput.value.trim()
    || el.createDisplayNameInput.value.trim() || el.createDescriptionInput.value.trim() || el.createTagsInput.value.trim());
}

async function copyMetadata(targetId) {
  const value = document.getElementById(targetId)?.textContent?.trim();
  if (!value || value === "-") return;
  await copyTextToClipboard(value, "已复制到剪贴板。");
}

async function copyTextToClipboard(value, successMessage) {
  if (!value) return;
  let copied = false;
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(value);
      copied = true;
    }
  } catch {
    copied = false;
  }
  if (!copied) copied = fallbackCopyText(value);
  showToast(copied ? successMessage : "复制失败，请手动选择文本。");
}

function fallbackCopyText(value) {
  const input = document.createElement("textarea");
  input.value = value;
  input.setAttribute("readonly", "");
  input.style.position = "fixed";
  input.style.opacity = "0";
  document.body.appendChild(input);
  input.select();
  const copied = document.execCommand("copy");
  input.remove();
  return copied;
}

function downloadTextRecipe(value, fileName, mimeType) {
  if (!value) return;
  const blob = new Blob([value], { type: `${mimeType};charset=utf-8` });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

function integrationFileName(extension) {
  const resourceId = (state.selectedResourceId || "resource").replace(/[^a-zA-Z0-9._-]+/g, "_");
  const packageId = (el.integrationPackageSelect.value || "package").replace(/[^a-zA-Z0-9._-]+/g, "_");
  return `${resourceId}.${packageId}.fairygui-integration.${extension}`;
}

function showToast(message) {
  window.clearTimeout(state.toastTimer);
  el.toast.textContent = message;
  el.toast.hidden = false;
  state.toastTimer = window.setTimeout(() => {
    el.toast.hidden = true;
  }, 2200);
}

async function refresh(options = {}) {
  setStatus("正在加载 UI 资源库...");
  state.formDirty = false;
  state.formBaseline = "";
  state.integrationPackages = [];
  resetIntegrationPlan();
  resetReplacementState();
  renderPlan(null, el.planOutput);

  state.apiCapabilities = await readJson("/api/authoring/ui-art/resources/capabilities", null);
  if (!state.apiCapabilities || state.apiCapabilities.apiVersion !== EXPECTED_API_VERSION) {
    state.artState = null;
    setStatus("Authoring Server 版本过旧或未启动。请从 EditorHub 重新启动当前版本服务。");
    render();
    return;
  }

  state.integrationPackages = await readJson("/api/authoring/ui-art/resources/fairygui-packages", []);

  if (!options.keepLibraries) {
    state.libraries = await readJson("/api/authoring/ui-art/resources/libraries", []);
    if (!state.libraries.some(lib => matchesLibrary(lib, state.libraryId))) {
      state.libraryId = state.libraries[0]?.libraryId || DEFAULT_LIBRARY_ID;
    }
  }

  state.artState = await readJson(`/api/authoring/ui-art/resources/state?library=${encodeURIComponent(state.libraryId)}`, null);
  if (!state.artState) {
    setStatus("Authoring API 不可用。请通过 EditorHub 启动外部编辑器。");
  } else {
    state.libraryId = state.artState.libraryId || state.libraryId;
    const firstResource = getResources()[0];
    if (!state.selectedResourceId || !findResource(state.selectedResourceId)) {
      state.selectedResourceId = firstResource?.resourceId || "";
    }
    setStatus(`已加载 ${state.artState.libraryId}，${getResources().length} 个手工资源。`);
  }

  render();
}

async function readJson(url, fallback) {
  try {
    const response = await fetch(url, { headers: { Accept: "application/json" }, cache: "no-store" });
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return await response.json();
  } catch (error) {
    console.warn(error);
    return fallback;
  }
}

async function postJson(url, payload) {
  try {
    const response = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify(payload)
    });

    const result = await response.json();
    result.httpOk = response.ok;
    return result;
  } catch (error) {
    console.error(error);
    return {
      httpOk: false,
      success: false,
      plan: {
        blocked: true,
        operationKind: "Plan",
        diagnostics: [{
          severity: "error",
          code: "WEB500",
          message: "Authoring Server 请求失败，请确认服务仍在运行后重试。",
          path: url
        }]
      }
    };
  }
}

function render() {
  renderLibrarySelect();
  renderSummary();
  renderResourceList();
  renderSelectedResource();
  renderDiagnostics();
  renderCreatePanel();
  renderIntegrationPanel();
  selectInspectorTab(state.inspectorTab);
  setPreviewMode(state.previewMode);
  updateDirtyUi();
}

function renderLibrarySelect() {
  const options = state.libraries.length ? state.libraries : [{ libraryId: state.libraryId, displayName: state.libraryId }];
  el.packageSelect.innerHTML = options.map(lib => {
    const value = lib.libraryId || lib.resourceRoot || lib.catalogPath;
    const label = lib.displayName || lib.libraryId || value;
    return `<option value="${escapeHtml(value)}"${matchesLibrary(lib, state.libraryId) ? " selected" : ""}>${escapeHtml(label)}</option>`;
  }).join("");
}

function renderSummary() {
  const art = state.artState;
  const resourceCount = getResources().length;
  const chips = [
    ["资源", String(resourceCount)],
    ["错误", String(art?.errorCount || 0)],
    ["警告", String(art?.warningCount || 0)]
  ];
  el.summaryStrip.innerHTML = chips
    .map(([label, value]) => `<div class="summary-chip ${label === "错误" ? "error" : label === "警告" ? "warning" : ""}"><strong>${escapeHtml(value)}</strong><span>${escapeHtml(label)}</span></div>`)
    .join("");
}

function renderResourceList() {
  const resources = getResources().filter(matchesFilters);
  const total = getResources().length;
  el.sliceResultText.textContent = `${resources.length} / ${total} 个资源`;
  el.clearFiltersButton.disabled = !state.search && !state.category;

  if (!resources.length) {
    el.sliceList.innerHTML = `<div class="usage-row"><strong>没有匹配资源</strong><span>调整筛选条件，或新增一张手工整理的 UI 切图。</span></div>`;
    return;
  }

  el.sliceList.innerHTML = resources.map(resource => {
    const label = resource.displayName || resource.resourceId;
    const selected = resource.resourceId === state.selectedResourceId ? " selected" : "";
    const tags = Array.isArray(resource.tags) ? resource.tags.join(", ") : "";
    const counts = getDiagnosticCounts(getDiagnosticsForResource(resource));
    const issuePill = counts.error > 0
      ? `<span class="slice-pill error">${counts.error} 个错误</span>`
      : counts.warning > 0
        ? `<span class="slice-pill warning">${counts.warning} 个警告</span>`
        : `<span class="slice-pill ok">干净</span>`;
    const tagText = tags ? `<span>${escapeHtml(tags)}</span>` : "";
    return `<button class="slice-button${selected}" type="button" data-resource-id="${escapeHtml(resource.resourceId)}"><strong>${escapeHtml(label)}</strong><span>${escapeHtml(categoryLabel(resource.category))} / ${escapeHtml(resource.relativePath || "-")}</span>${tagText}<div class="slice-pill-row"><span class="slice-pill">手工资源</span>${issuePill}</div></button>`;
  }).join("");

  for (const button of el.sliceList.querySelectorAll("button[data-resource-id]")) {
    button.addEventListener("click", () => {
      if (button.dataset.resourceId === state.selectedResourceId) return;
      if (!confirmDiscardDirty()) return;
      state.selectedResourceId = button.dataset.resourceId;
      state.formDirty = false;
      state.formBaseline = "";
      resetIntegrationPlan();
      resetReplacementState();
      render();
    });
  }
}

function renderSelectedResource() {
  const resource = findResource(state.selectedResourceId);
  const sourcePath = resource ? buildSourcePath(resource) : "";
  el.saveButton.disabled = !resource || !state.formDirty;
  el.discardButton.disabled = !resource || !state.formDirty;
  el.planReplaceButton.disabled = !resource || !state.replacementDataUrl;
  el.replaceButton.disabled = !state.replacementPlan || state.replacementPlan.blocked;
  el.openReplaceButton.disabled = !resource;

  if (!resource) {
    state.selectedImageResourceId = "";
    state.selectedImageInfo = null;
    el.sliceTitle.textContent = "未选择资源";
    el.sliceBadge.textContent = "-";
    el.slicePreview.style.display = "none";
    el.scale9Overlay.hidden = true;
    el.sliceIdText.textContent = "-";
    el.resourceIdText.textContent = "-";
    el.sourcePathText.textContent = "-";
    el.catalogHashText.textContent = "-";
    el.sliceIssueSummary.innerHTML = "";
    populateResourceForm(null);
    renderPreflight(null);
    return;
  }

  el.sliceTitle.textContent = resource.displayName || resource.resourceId;
  el.sliceBadge.textContent = categoryLabel(resource.category || "-");
  el.sliceIdText.textContent = resource.resourceId || "-";
  el.resourceIdText.textContent = resource.category || "-";
  el.sourcePathText.textContent = sourcePath || resource.relativePath || "-";
  el.catalogHashText.textContent = state.artState?.catalogHash || "-";

  if (sourcePath) {
    if (state.selectedImageResourceId !== resource.resourceId) {
      state.selectedImageResourceId = resource.resourceId;
      state.selectedImageInfo = null;
    }
    el.slicePreview.src = "/" + encodeURI(sourcePath);
    el.slicePreview.onload = () => {
      state.selectedImageInfo = {
        width: el.slicePreview.naturalWidth || el.slicePreview.width,
        height: el.slicePreview.naturalHeight || el.slicePreview.height
      };
      renderPreflight(findResource(state.selectedResourceId));
      updateDirtyUi();
      requestAnimationFrame(renderScale9Overlay);
    };
    el.slicePreview.onerror = () => {
      state.selectedImageInfo = null;
      el.slicePreview.style.display = "none";
      el.scale9Overlay.hidden = true;
      renderResourceIssueSummary(resource, "Authoring 静态服务暂时无法读取预览图片。");
    };
    el.slicePreview.style.display = "block";
  } else {
    el.slicePreview.style.display = "none";
  }

  if (!state.formDirty) populateResourceForm(resource);
  renderResourceIssueSummary(resource);
  renderPreflight(resource);
  requestAnimationFrame(renderScale9Overlay);
}

function renderScale9Overlay() {
  const resource = findResource(state.selectedResourceId);
  const model = resource ? (state.formDirty ? readFormModelFromInputs() : readFormModelFromResource(resource)) : null;
  const imageWidth = el.slicePreview.naturalWidth || 0;
  const imageHeight = el.slicePreview.naturalHeight || 0;
  const grid = model?.usage?.scale9Grid;
  const show = model?.usage?.policy === "NineSlice" && grid && imageWidth > 0 && imageHeight > 0
    && el.slicePreview.style.display !== "none";
  for (const field of document.querySelectorAll(".scale9-field")) {
    field.hidden = model?.usage?.policy !== "NineSlice";
  }
  if (!show) {
    el.scale9Overlay.hidden = true;
    return;
  }

  const imageRect = el.slicePreview.getBoundingClientRect();
  const frameRect = el.previewFrame.getBoundingClientRect();
  if (imageRect.width <= 0 || imageRect.height <= 0) {
    el.scale9Overlay.hidden = true;
    return;
  }

  el.scale9Overlay.hidden = false;
  el.scale9Overlay.style.left = `${imageRect.left - frameRect.left + el.previewFrame.scrollLeft}px`;
  el.scale9Overlay.style.top = `${imageRect.top - frameRect.top + el.previewFrame.scrollTop}px`;
  el.scale9Overlay.style.width = `${imageRect.width}px`;
  el.scale9Overlay.style.height = `${imageRect.height}px`;

  const left = Math.max(0, Math.min(1, grid.x / imageWidth));
  const right = Math.max(left, Math.min(1, (grid.x + grid.width) / imageWidth));
  const top = Math.max(0, Math.min(1, grid.y / imageHeight));
  const bottom = Math.max(top, Math.min(1, (grid.y + grid.height) / imageHeight));
  el.scale9Overlay.querySelector("[data-scale9-edge='left']").style.left = `${left * 100}%`;
  el.scale9Overlay.querySelector("[data-scale9-edge='right']").style.left = `${right * 100}%`;
  el.scale9Overlay.querySelector("[data-scale9-edge='top']").style.top = `${top * 100}%`;
  el.scale9Overlay.querySelector("[data-scale9-edge='bottom']").style.top = `${bottom * 100}%`;
}

function beginScale9Drag(event) {
  const edge = event.currentTarget.dataset.scale9Edge;
  const imageWidth = el.slicePreview.naturalWidth || 0;
  const imageHeight = el.slicePreview.naturalHeight || 0;
  const imageRect = el.slicePreview.getBoundingClientRect();
  if (!edge || imageWidth <= 0 || imageHeight <= 0 || imageRect.width <= 0 || imageRect.height <= 0) return;
  event.preventDefault();
  event.currentTarget.setPointerCapture?.(event.pointerId);

  const move = moveEvent => {
    const grid = readScale9GridFromInputs() || defaultScale9Grid(imageWidth, imageHeight);
    if (!grid) return;
    const next = { ...grid };
    if (edge === "left" || edge === "right") {
      const pixel = clamp(Math.round((moveEvent.clientX - imageRect.left) / imageRect.width * imageWidth), 0, imageWidth);
      if (edge === "left") {
        const fixedRight = grid.x + grid.width;
        next.x = clamp(pixel, 0, Math.max(0, fixedRight - 1));
        next.width = fixedRight - next.x;
      } else {
        const fixedLeft = grid.x;
        next.width = clamp(pixel, Math.min(imageWidth, fixedLeft + 1), imageWidth) - fixedLeft;
      }
    } else {
      const pixel = clamp(Math.round((moveEvent.clientY - imageRect.top) / imageRect.height * imageHeight), 0, imageHeight);
      if (edge === "top") {
        const fixedBottom = grid.y + grid.height;
        next.y = clamp(pixel, 0, Math.max(0, fixedBottom - 1));
        next.height = fixedBottom - next.y;
      } else {
        const fixedTop = grid.y;
        next.height = clamp(pixel, Math.min(imageHeight, fixedTop + 1), imageHeight) - fixedTop;
      }
    }
    writeScale9Grid(next);
    syncFormDirty();
  };
  const end = () => {
    window.removeEventListener("pointermove", move);
    window.removeEventListener("pointerup", end);
    window.removeEventListener("pointercancel", end);
  };
  window.addEventListener("pointermove", move);
  window.addEventListener("pointerup", end);
  window.addEventListener("pointercancel", end);
}

function writeScale9Grid(grid) {
  el.scale9XInput.value = grid.x;
  el.scale9YInput.value = grid.y;
  el.scale9WidthInput.value = grid.width;
  el.scale9HeightInput.value = grid.height;
}

function populateResourceForm(resource) {
  const model = readFormModelFromResource(resource);
  el.categoryInput.value = model.category;
  el.displayNameInput.value = model.displayName;
  el.descriptionInput.value = model.description;
  el.tagsInput.value = model.tags.join(", ");
  el.usagePolicyInput.value = model.usage.policy;
  el.designWidthInput.value = model.usage.designSize.width;
  el.designHeightInput.value = model.usage.designSize.height;
  el.scale9XInput.value = model.usage.scale9Grid?.x ?? "";
  el.scale9YInput.value = model.usage.scale9Grid?.y ?? "";
  el.scale9WidthInput.value = model.usage.scale9Grid?.width ?? "";
  el.scale9HeightInput.value = model.usage.scale9Grid?.height ?? "";
  el.preserveAspectInput.checked = model.usage.preserveAspect;
  el.requireAlphaInput.checked = model.replacement.requireAlphaChannel;
  el.requireNonOpaqueInput.checked = model.replacement.requireNonOpaqueAlpha;
  el.requireSameSizeInput.checked = model.replacement.requireSamePixelSize;
  el.allowStretchInput.checked = model.replacement.allowStretch;
  el.replaceTogetherInput.checked = model.replacement.replaceTogether;
  el.minWidthInput.value = model.replacement.minPixelWidth;
  el.minHeightInput.value = model.replacement.minPixelHeight;
  state.formBaseline = JSON.stringify(model);
  state.formDirty = false;
  updateDirtyUi();
}

function renderResourceIssueSummary(resource, extraMessage = "") {
  const counts = getDiagnosticCounts(getDiagnosticsForResource(resource));
  if (extraMessage) {
    el.sliceIssueSummary.innerHTML = `<div class="issue-summary-row warning">${escapeHtml(extraMessage)}</div>`;
    return;
  }

  if (counts.error > 0) {
    el.sliceIssueSummary.innerHTML = `<div class="issue-summary-row error">当前资源有 ${counts.error} 个阻断诊断。</div>`;
  } else if (counts.warning > 0) {
    el.sliceIssueSummary.innerHTML = `<div class="issue-summary-row warning">当前资源有 ${counts.warning} 个警告。</div>`;
  } else {
    el.sliceIssueSummary.innerHTML = `<div class="issue-summary-row ok">当前资源没有诊断问题。</div>`;
  }
}

function renderPreflight(resource) {
  if (!resource) {
    el.preflightCount.textContent = "0";
    el.preflightList.innerHTML = `<div class="usage-row"><strong>未选择资源</strong><span>选择左侧资源后查看使用建议和替换约束。</span></div>`;
    return;
  }

  const model = state.formDirty ? readFormModelFromInputs() : readFormModelFromResource(resource);
  const validation = validateUsageAndReplacementModel(model, state.selectedImageInfo || model.usage.designSize);
  const rows = [];
  rows.push(makePreflightRow("info", "资源路径", resource.relativePath || "-", "资源库内相对路径，保持给 Development Agent 和工具链引用。"));
  if (model.usage.designSize.width > 0 && model.usage.designSize.height > 0) {
    rows.push(makePreflightRow("ok", "设计尺寸", `${model.usage.designSize.width}x${model.usage.designSize.height}`, "FairyGUI 使用时以这个尺寸作为默认参考。"));
  } else {
    rows.push(makePreflightRow("warning", "设计尺寸", "缺失", "建议填写正数，避免后续自动检查失去基准。"));
  }

  if (model.usage.policy === "NineSlice") {
    const grid = model.usage.scale9Grid;
    const gridDiagnostic = validation.find(item => item.code === "WEBVAL002" || item.code === "WEBVAL003");
    const validGrid = grid && !gridDiagnostic;
    rows.push(makePreflightRow(validGrid ? "ok" : "error", "九宫格", validGrid ? `${grid.x},${grid.y},${grid.width},${grid.height}` : gridDiagnostic?.message || "缺失", "NineSlice 必须声明位于 PNG 像素范围内的可拉伸中心区域。"));
  } else {
    rows.push(makePreflightRow("info", "使用策略", usagePolicyLabel(model.usage.policy), "非九宫格策略不会要求 scale9Grid。"));
  }

  if (model.replacement.requireAlphaChannel) {
    rows.push(makePreflightRow("ok", "Alpha 约束", "需要 Alpha 通道", "适合按钮、图标、面板框体等透明 UI 切图。"));
  } else {
    rows.push(makePreflightRow("warning", "Alpha 约束", "未要求 Alpha", "如果资源需要透明边缘，建议开启。"));
  }

  const alphaDiagnostic = validation.find(item => item.code === "WEBVAL004");
  if (alphaDiagnostic) {
    rows.push(makePreflightRow("error", "Alpha 约束", alphaDiagnostic.message, "需要透明像素时必须同时要求 Alpha 通道。"));
  }

  const counts = getDiagnosticCounts(getDiagnosticsForResource(resource));
  const severity = counts.error > 0 ? "error" : counts.warning > 0 ? "warning" : "ok";
  const text = counts.error > 0 ? `${counts.error} 个错误` : counts.warning > 0 ? `${counts.warning} 个警告` : "干净";
  rows.push(makePreflightRow(severity, "当前诊断", text, "来自 Authoring Core 的 catalog 校验结果。"));

  el.preflightCount.textContent = String(rows.length);
  el.preflightList.innerHTML = rows.join("");
}

function makePreflightRow(severity, title, value, detail) {
  return `<div class="usage-row ${escapeHtml(severity)}"><strong>${escapeHtml(title)}：${escapeHtml(value)}</strong><span>${escapeHtml(detail)}</span></div>`;
}

function renderDiagnostics() {
  for (const button of el.diagnosticScopeControl.querySelectorAll("button[data-scope]")) {
    button.classList.toggle("selected", button.dataset.scope === state.diagnosticsScope);
  }

  const selectedResource = findResource(state.selectedResourceId);
  const allDiagnostics = state.artState?.diagnostics || [];
  const diagnostics = state.diagnosticsScope === "selected" && selectedResource
    ? getDiagnosticsForResource(selectedResource)
    : allDiagnostics;

  el.diagnosticCount.textContent = String(diagnostics.length);
  if (!diagnostics.length) {
    const message = state.diagnosticsScope === "selected" ? "当前资源没有诊断问题。" : "资源库状态干净。";
    el.diagnosticsList.innerHTML = `<div class="diagnostic-row info"><strong>没有诊断</strong><span>${escapeHtml(message)}</span></div>`;
    return;
  }

  el.diagnosticsList.innerHTML = diagnostics.map(diagnostic => {
    const severity = diagnostic.severity || "info";
    const path = diagnostic.path || diagnostic.location?.file || "";
    return `<div class="diagnostic-row ${escapeHtml(severity)}"><strong>${escapeHtml(diagnostic.code || "-")} ${escapeHtml(diagnostic.name || "")}</strong><span>${escapeHtml(diagnostic.message || "")}</span><span>${escapeHtml(path)}</span></div>`;
  }).join("");
}

function renderIntegrationPanel() {
  const packages = state.integrationPackages || [];
  const previousValue = el.integrationPackageSelect.value;
  el.integrationPackageSelect.innerHTML = packages.length
    ? packages.map(item => `<option value="${escapeHtml(item.packageId)}">${escapeHtml(item.displayName || item.packageId)}</option>`).join("")
    : `<option value="">没有可用 FairyGUI 包</option>`;
  const preferred = packages.some(item => item.packageId === previousValue)
    ? previousValue
    : packages.some(item => item.packageId === "MxCharacterTestHud")
      ? "MxCharacterTestHud"
      : packages[0]?.packageId || "";
  el.integrationPackageSelect.value = preferred;

  const resource = findResource(state.selectedResourceId);
  el.generateIntegrationPlanButton.disabled = !resource || !preferred || state.formDirty;
  const hasRecipe = Boolean(state.integrationPlan && state.integrationJson && state.integrationMarkdown);
  el.copyIntegrationRecipeButton.disabled = !hasRecipe;
  el.downloadIntegrationJsonButton.disabled = !hasRecipe;
  el.downloadIntegrationMarkdownButton.disabled = !hasRecipe;

  if (!state.integrationPlan) {
    el.integrationStatusBadge.textContent = state.formDirty ? "请先保存" : "未生成";
    el.integrationPlanOutput.innerHTML = `<div class="integration-plan-summary"><strong>${state.formDirty ? "保存语义改动后再生成" : "等待生成接入计划"}</strong><span>计划只读取资源库与目标 FairyGUI 包。</span></div>`;
    return;
  }

  const plan = state.integrationPlan;
  el.integrationStatusBadge.textContent = integrationStatusLabel(plan.status);
  const diagnostics = plan.diagnostics || [];
  el.integrationPlanOutput.innerHTML = [
    `<div class="integration-plan-summary"><strong>${escapeHtml(integrationStatusLabel(plan.status))}</strong><span>${escapeHtml(plan.sourcePath || "-")} → ${escapeHtml(plan.suggestedTargetPath || "-")}</span><span>参数：${escapeHtml(integrationUsageStatusLabel(plan.usageStatus))}　发布：${plan.requiresFairyGuiPublish ? "需要" : "无需"}</span></div>`,
    ...diagnostics.map(item => `<div class="integration-diagnostic ${escapeHtml(item.severity || "info")}"><strong>${escapeHtml(item.code || "-")}</strong><span>${escapeHtml(item.message || "")}</span></div>`)
  ].join("");
}

async function generateIntegrationPlan() {
  const resource = findResource(state.selectedResourceId);
  const targetPackage = el.integrationPackageSelect.value;
  if (!resource || !targetPackage || state.formDirty) {
    renderIntegrationPanel();
    return;
  }

  el.generateIntegrationPlanButton.disabled = true;
  setStatus("正在生成 FairyGUI 接入计划...");
  const result = await postJson("/api/authoring/ui-art/resources/plan-fairygui-import", {
    library: state.artState?.libraryId || state.libraryId,
    resourceId: resource.resourceId,
    targetPackage
  });
  state.integrationPlan = result.plan || null;
  state.integrationJson = result.jsonRecipe || "";
  state.integrationMarkdown = result.markdownRecipe || "";
  setStatus(result.success ? "FairyGUI 接入计划已生成。" : "FairyGUI 接入计划被阻断。");
  renderIntegrationPanel();
}

function resetIntegrationPlan() {
  state.integrationPlan = null;
  state.integrationJson = "";
  state.integrationMarkdown = "";
  if (el.integrationPlanOutput) renderIntegrationPanel();
}

function integrationStatusLabel(value) {
  const labels = {
    NotImported: "未导入",
    ImportedExact: "已精确导入",
    ImportedEquivalentContent: "已有等价资源",
    ImportedFileMissing: "导入文件缺失",
    ContentDrift: "内容漂移",
    NameConflict: "名称冲突",
    OrphanTargetFile: "存在未登记文件",
    UnsafePackageImage: "包内路径不安全",
    Blocked: "已阻断"
  };
  return labels[value] || value || "未知";
}

function integrationUsageStatusLabel(value) {
  const labels = {
    Aligned: "一致",
    Drift: "漂移",
    Untracked: "未跟踪",
    NotImported: "未导入",
    Unknown: "未知"
  };
  return labels[value] || value || "未知";
}

async function saveSelectedResource() {
  const resource = findResource(state.selectedResourceId);
  if (!resource || !state.artState?.catalog) return;

  if (!state.formDirty) {
    setStatus("没有需要保存的资源库改动。");
    return;
  }

  const nextCatalog = cloneJson(state.artState.catalog);
  const nextResource = (nextCatalog.resources || []).find(item => item.resourceId === state.selectedResourceId);
  if (!nextResource) {
    renderPlan({ diagnostics: [{ severity: "error", code: "WEB002", message: "当前资源已经不在目录中。" }] }, el.planOutput);
    return;
  }

  applyFormModelToResource(nextResource, readFormModelFromInputs());
  el.saveButton.disabled = true;
  setStatus("正在保存资源库改动...");

  const result = await postJson("/api/authoring/ui-art/resources/save", {
    library: state.artState.libraryId,
    expectedCatalogHash: state.artState.catalogHash,
    catalog: nextCatalog
  });

  renderPlan(result.plan, el.planOutput);
  if (!result.httpOk || !result.success) {
    setStatus("资源库保存被阻断。");
    updateDirtyUi();
    return;
  }

  state.artState = result.state;
  state.formDirty = false;
  state.formBaseline = "";
  resetIntegrationPlan();
  setStatus("资源库改动已保存。");
  render();
}

async function loadReplacementFile(file) {
  state.replacementDataUrl = "";
  state.replacementFileName = "";
  state.replacementFileMeta = "";
  state.replacementPlan = null;
  renderPlan(null, el.planOutput);
  updateReplacementFileUi();
  renderSelectedResource();
  if (!file) return;

  if (file.type && file.type !== "image/png") {
    renderPlan({ diagnostics: [{ severity: "error", code: "WEB003", message: "替换候选文件必须是 PNG。" }] }, el.planOutput);
    return;
  }

  state.replacementDataUrl = await readFileAsDataUrl(file);
  state.replacementFileName = file.name || "replacement.png";
  const info = await readImageInfoFromDataUrl(state.replacementDataUrl);
  state.replacementFileMeta = info ? `${info.width}x${info.height}，${formatBytes(file.size)}` : formatBytes(file.size);
  updateReplacementFileUi();
  setStatus(`已载入替换候选文件：${state.replacementFileName}`);
  renderSelectedResource();
}

async function planReplacement() {
  const resource = findResource(state.selectedResourceId);
  if (!resource || !state.replacementDataUrl) {
    renderPlan({ diagnostics: [{ severity: "error", code: "WEB001", message: "请先选择资源和 PNG 文件。" }] }, el.planOutput);
    return;
  }

  const result = await postReplacement("/api/authoring/ui-art/resources/plan-replace");
  state.replacementPlan = result.plan;
  renderPlan(result.plan, el.planOutput);
  renderSelectedResource();
}

async function replacePng() {
  const result = await postReplacement("/api/authoring/ui-art/resources/replace-png");
  renderPlan(result.plan, el.planOutput);
  if (result.success && result.state) {
    state.artState = result.state;
    resetIntegrationPlan();
    resetReplacementState();
    closeOperationDrawer("replacement");
    setStatus("PNG 已替换。进入 FairyGUI 包并发布前，仍需通过 FairyGUI Editor 完成导入/发布。");
    render();
  } else {
    setStatus("PNG 替换被阻断。");
  }
}

async function postReplacement(url) {
  return await postJson(url, {
    library: state.artState.libraryId,
    expectedCatalogHash: state.artState.catalogHash,
    resourceId: state.selectedResourceId,
    bytesBase64: state.replacementDataUrl
  });
}

async function loadCreateFile(file) {
  state.createDataUrl = "";
  state.createFileName = "";
  state.createImageInfo = null;
  state.createPlan = null;
  renderPlan(null, el.createPlanOutput);
  renderCreatePanel();
  if (!file) return;

  if (file.type && file.type !== "image/png") {
    renderPlan({ diagnostics: [{ severity: "error", code: "WEB010", message: "新增资源文件必须是 PNG。" }] }, el.createPlanOutput);
    return;
  }

  state.createDataUrl = await readFileAsDataUrl(file);
  state.createFileName = file.name || "resource.png";
  state.createImageInfo = await readImageInfoFromDataUrl(state.createDataUrl);
  fillCreateDefaultsFromFile(file, state.createImageInfo);
  renderCreatePanel();
  setStatus(`已载入新增 PNG：${state.createFileName}`);
}

function fillCreateDefaultsFromFile(file, imageInfo = state.createImageInfo, applyImagePolicyDefaults = true) {
  const baseName = stripExtension(file.name || "resource.png");
  const slug = slugify(baseName);
  const category = el.createCategoryInput.value || "ButtonBase";
  if (!el.createResourceIdInput.value.trim()) {
    el.createResourceIdInput.value = category === "ButtonBase"
      ? `ui.button.${slug}.base`
      : `ui.resource.${slug}`;
  }

  if (!el.createRelativePathInput.value.trim()) {
    const folder = category === "ButtonBase" ? "images/buttons" : "images/imports";
    el.createRelativePathInput.value = `${folder}/${slug}.png`;
  }

  if (!el.createDisplayNameInput.value.trim()) {
    el.createDisplayNameInput.value = baseName.replace(/[_-]+/g, " ");
  }

  if (!el.createDescriptionInput.value.trim()) {
    el.createDescriptionInput.value = "手工整理的 UI 切图资源；请补充实际用途和视觉边界。";
  }

  if (!el.createTagsInput.value.trim()) {
    el.createTagsInput.value = category === "ButtonBase" ? "button" : "draft";
  }

  if (imageInfo) {
    if (!el.createDesignWidthInput.value) el.createDesignWidthInput.value = imageInfo.width;
    if (!el.createDesignHeightInput.value) el.createDesignHeightInput.value = imageInfo.height;
    if (!el.createMinWidthInput.value) el.createMinWidthInput.value = imageInfo.width;
    if (!el.createMinHeightInput.value) el.createMinHeightInput.value = imageInfo.height;
    if (applyImagePolicyDefaults) {
      el.createRequireAlphaInput.checked = Boolean(imageInfo.hasNonOpaqueAlpha);
      el.createRequireNonOpaqueInput.checked = Boolean(imageInfo.hasNonOpaqueAlpha);
    }
    applyDefaultCreateScale9Grid();
  }
}

function syncCreateDefaultsFromId() {
  if (el.createRelativePathInput.value.trim()) return;
  const slug = slugify(el.createResourceIdInput.value.trim().split(".").filter(Boolean).pop() || "resource");
  const folder = el.createCategoryInput.value === "ButtonBase" ? "images/buttons" : "images/imports";
  el.createRelativePathInput.value = `${folder}/${slug}.png`;
}

function syncCreateDefaultsFromCategory() {
  if (!state.createFileName) return;
  fillCreateDefaultsFromFile({ name: state.createFileName }, state.createImageInfo, false);
  renderCreatePanel();
}

function syncCreatePolicyFields() {
  const policy = el.createPolicyInput.value;
  el.createAllowStretchInput.checked = policy === "NineSlice" || policy === "Stretch";
  if (policy === "NineSlice") applyDefaultCreateScale9Grid();
  renderCreatePanel();
}

function applyDefaultCreateScale9Grid() {
  if (el.createPolicyInput.value !== "NineSlice" || !state.createImageInfo) return;
  const grid = defaultScale9Grid(state.createImageInfo.width, state.createImageInfo.height);
  if (!grid) return;
  if (!el.createScale9XInput.value) el.createScale9XInput.value = grid.x;
  if (!el.createScale9YInput.value) el.createScale9YInput.value = grid.y;
  if (!el.createScale9WidthInput.value) el.createScale9WidthInput.value = grid.width;
  if (!el.createScale9HeightInput.value) el.createScale9HeightInput.value = grid.height;
}

async function planCreateResource() {
  if (!state.createDataUrl) {
    renderPlan({ diagnostics: [{ severity: "error", code: "WEB011", message: "请先选择一张新的 PNG。" }] }, el.createPlanOutput);
    return;
  }

  const result = await postCreate("/api/authoring/ui-art/resources/plan-create");
  state.createPlan = result.plan;
  renderPlan(result.plan, el.createPlanOutput);
}

async function createResource() {
  if (!state.createDataUrl) return;
  const result = await postCreate("/api/authoring/ui-art/resources/create");
  renderPlan(result.plan, el.createPlanOutput);
  if (result.success && result.state) {
    state.artState = result.state;
    state.selectedResourceId = buildCreateResourcePayload().resourceId;
    resetIntegrationPlan();
    resetCreateState();
    closeOperationDrawer("create");
    setStatus("新增资源已写入资源库。FairyGUI 使用仍需在 FairyGUI Editor 内完成导入和发布。");
    render();
  } else {
    setStatus("新增资源被预检阻断。");
  }
}

async function postCreate(url) {
  return await postJson(url, {
    library: state.artState?.libraryId || state.libraryId,
    expectedCatalogHash: state.artState?.catalogHash || "",
    resource: buildCreateResourcePayload(),
    bytesBase64: state.createDataUrl
  });
}

function buildCreateResourcePayload() {
  const policy = el.createPolicyInput.value || "NineSlice";
  const grid = policy === "NineSlice" ? {
    x: toNonNegativeInt(el.createScale9XInput.value),
    y: toNonNegativeInt(el.createScale9YInput.value),
    width: toNonNegativeInt(el.createScale9WidthInput.value),
    height: toNonNegativeInt(el.createScale9HeightInput.value)
  } : null;
  return {
    resourceId: el.createResourceIdInput.value.trim(),
    relativePath: normalizeRelativePath(el.createRelativePathInput.value.trim()),
    category: el.createCategoryInput.value || "ButtonBase",
    displayName: el.createDisplayNameInput.value.trim(),
    description: el.createDescriptionInput.value.trim(),
    tags: splitTags(el.createTagsInput.value),
    recommendedFairyGuiUsage: {
      policy,
      designSize: {
        width: toNonNegativeInt(el.createDesignWidthInput.value),
        height: toNonNegativeInt(el.createDesignHeightInput.value)
      },
      scale9Grid: grid,
      preserveAspect: policy === "PreserveAspect"
    },
    replacement: {
      requireAlphaChannel: el.createRequireAlphaInput.checked,
      requireNonOpaqueAlpha: el.createRequireNonOpaqueInput.checked,
      requireSamePixelSize: el.createRequireSameSizeInput.checked,
      allowStretch: el.createAllowStretchInput.checked,
      minPixelWidth: toNonNegativeInt(el.createMinWidthInput.value),
      minPixelHeight: toNonNegativeInt(el.createMinHeightInput.value),
      replaceTogether: el.createReplaceTogetherInput.checked
    }
  };
}

function renderCreatePanel() {
  const nineSlice = el.createPolicyInput.value === "NineSlice";
  for (const field of document.querySelectorAll(".create-nine-slice-field")) {
    field.hidden = !nineSlice;
  }

  el.createFileName.textContent = state.createFileName || "选择新的 PNG";
  if (state.createImageInfo) {
    el.createFileMeta.textContent = `${state.createImageInfo.width}x${state.createImageInfo.height}，将写入 ${el.createRelativePathInput.value || "未填写路径"}`;
  } else {
    el.createFileMeta.textContent = "新增资源会写入资源库目录，不会写 FairyGUI XML";
  }

  const blocked = !state.createPlan || state.createPlan.blocked;
  el.createResourceButton.disabled = !state.createPlan || blocked || state.createPlan.operationKind !== "CreateResource";
}

function renderPlan(plan, target) {
  if (!plan) {
    target.innerHTML = `<div class="plan-row info"><strong>暂无计划</strong><span>运行保存、替换或新增预检后，会在这里显示会改什么和是否阻断。</span></div>`;
    if (target === el.planOutput) el.replaceButton.disabled = true;
    if (target === el.createPlanOutput) el.createResourceButton.disabled = true;
    return;
  }

  const diagnostics = plan.diagnostics || [];
  const blocked = Boolean(plan.blocked) || diagnostics.some(diagnostic => diagnostic.blocking || diagnostic.severity === "error");
  target.innerHTML = [
    `<div class="plan-row ${blocked ? "error" : "info"}"><strong>${escapeHtml(operationLabel(plan.operationKind))} ${blocked ? "已阻断" : "可执行"}</strong><span>目录=${yesNo(plan.wouldModifyCatalog)} PNG=${yesNo(plan.wouldModifyPng)} XML=${yesNo(plan.wouldModifyFairyGuiXml)} 需要发布=${yesNo(plan.requiresFairyGuiPublish)}</span><span>${escapeHtml(plan.targetPath || plan.sourcePath || "")}</span></div>`,
    ...diagnostics.map(diagnostic => `<div class="plan-row ${escapeHtml(diagnostic.severity || "info")}"><strong>${escapeHtml(diagnostic.code || "-")}</strong><span>${escapeHtml(diagnostic.message || "")}</span><span>${escapeHtml(diagnostic.path || "")}</span></div>`)
  ].join("");

  if (target === el.planOutput) {
    el.replaceButton.disabled = plan.operationKind !== "ReplaceExistingPng" || blocked;
  }

  if (target === el.createPlanOutput) {
    el.createResourceButton.disabled = plan.operationKind !== "CreateResource" || blocked;
  }
}

function readFormModelFromResource(resource) {
  const replacement = resource?.replacement || {};
  const usage = resource?.recommendedFairyGuiUsage || {};
  return {
    category: resource?.category || CATEGORY_OPTIONS[0],
    displayName: resource?.displayName || "",
    description: resource?.description || "",
    tags: Array.isArray(resource?.tags) ? [...resource.tags] : [],
    usage: {
      policy: usage.policy || "NineSlice",
      designSize: {
        width: toNonNegativeInt(usage.designSize?.width),
        height: toNonNegativeInt(usage.designSize?.height)
      },
      scale9Grid: usage.scale9Grid ? {
        x: toNonNegativeInt(usage.scale9Grid.x),
        y: toNonNegativeInt(usage.scale9Grid.y),
        width: toNonNegativeInt(usage.scale9Grid.width),
        height: toNonNegativeInt(usage.scale9Grid.height)
      } : null,
      preserveAspect: Boolean(usage.preserveAspect)
    },
    replacement: {
      requireAlphaChannel: Boolean(replacement.requireAlphaChannel),
      requireNonOpaqueAlpha: Boolean(replacement.requireNonOpaqueAlpha),
      requireSamePixelSize: Boolean(replacement.requireSamePixelSize),
      allowStretch: Boolean(replacement.allowStretch),
      replaceTogether: Boolean(replacement.replaceTogether),
      minPixelWidth: toNonNegativeInt(replacement.minPixelWidth),
      minPixelHeight: toNonNegativeInt(replacement.minPixelHeight)
    }
  };
}

function readFormModelFromInputs() {
  const policy = el.usagePolicyInput.value || "NineSlice";
  return {
    category: el.categoryInput.value || CATEGORY_OPTIONS[0],
    displayName: el.displayNameInput.value.trim(),
    description: el.descriptionInput.value.trim(),
    tags: splitTags(el.tagsInput.value),
    usage: {
      policy,
      designSize: {
        width: toNonNegativeInt(el.designWidthInput.value),
        height: toNonNegativeInt(el.designHeightInput.value)
      },
      scale9Grid: readScale9GridFromInputs(),
      preserveAspect: el.preserveAspectInput.checked
    },
    replacement: {
      requireAlphaChannel: el.requireAlphaInput.checked,
      requireNonOpaqueAlpha: el.requireNonOpaqueInput.checked,
      requireSamePixelSize: el.requireSameSizeInput.checked,
      allowStretch: el.allowStretchInput.checked,
      replaceTogether: el.replaceTogetherInput.checked,
      minPixelWidth: toNonNegativeInt(el.minWidthInput.value),
      minPixelHeight: toNonNegativeInt(el.minHeightInput.value)
    }
  };
}

function readScale9GridFromInputs() {
  const x = toNonNegativeInt(el.scale9XInput.value);
  const y = toNonNegativeInt(el.scale9YInput.value);
  const width = toNonNegativeInt(el.scale9WidthInput.value);
  const height = toNonNegativeInt(el.scale9HeightInput.value);
  if (x === 0 && y === 0 && width === 0 && height === 0) return null;
  return { x, y, width, height };
}

function applyFormModelToResource(resource, model) {
  resource.category = model.category;
  resource.displayName = model.displayName;
  resource.description = model.description;
  resource.tags = model.tags;
  resource.recommendedFairyGuiUsage = {
    policy: model.usage.policy,
    designSize: model.usage.designSize,
    scale9Grid: model.usage.scale9Grid,
    preserveAspect: model.usage.preserveAspect
  };
  resource.replacement = {
    requireAlphaChannel: model.replacement.requireAlphaChannel,
    requireNonOpaqueAlpha: model.replacement.requireNonOpaqueAlpha,
    requireSamePixelSize: model.replacement.requireSamePixelSize,
    allowStretch: model.replacement.allowStretch,
    minPixelWidth: model.replacement.minPixelWidth,
    minPixelHeight: model.replacement.minPixelHeight,
    replaceTogether: model.replacement.replaceTogether
  };
}

function syncFormDirty() {
  const next = JSON.stringify(readFormModelFromInputs());
  state.formDirty = next !== state.formBaseline;
  state.replacementPlan = null;
  resetIntegrationPlan();
  renderPlan(null, el.planOutput);
  renderPreflight(findResource(state.selectedResourceId));
  updateDirtyUi();
  renderScale9Overlay();
}

function discardFormEdits() {
  const resource = findResource(state.selectedResourceId);
  populateResourceForm(resource);
  renderPreflight(resource);
  renderScale9Overlay();
}

function updateDirtyUi() {
  const resource = findResource(state.selectedResourceId);
  const model = resource ? readFormModelFromInputs() : null;
  const blocked = model ? validateUsageAndReplacementModel(model, state.selectedImageInfo || model.usage.designSize).some(item => item.severity === "error") : true;
  el.saveButton.disabled = !state.formDirty || !resource || blocked;
  el.discardButton.disabled = !state.formDirty || !findResource(state.selectedResourceId);
  const statusText = state.formDirty ? "未保存" : state.artState ? "已保存" : "干净";
  el.editStatusText.textContent = statusText;
  el.editStatusText.className = `state-note ${state.formDirty ? "dirty" : "saved"}`;
  el.editorStateBadge.textContent = state.formDirty ? "未保存" : "干净";
  el.editorStateBadge.className = `state-badge ${state.formDirty ? "dirty" : "clean"}`;
}

function validateUsageAndReplacementModel(model, imageInfo) {
  if (!model) return [];
  const diagnostics = [];
  const width = Number(imageInfo?.width || 0);
  const height = Number(imageInfo?.height || 0);
  if (model.usage.designSize.width <= 0 || model.usage.designSize.height <= 0) {
    diagnostics.push({ severity: "warning", code: "WEBVAL001", message: "设计尺寸应为正数。" });
  }

  if (model.usage.policy === "NineSlice") {
    const grid = model.usage.scale9Grid;
    if (!grid || grid.width <= 0 || grid.height <= 0) {
      diagnostics.push({ severity: "error", code: "WEBVAL002", message: "九宫格区域不能为空。" });
    } else if (width > 0 && height > 0 && (grid.x < 0 || grid.y < 0 || grid.x + grid.width > width || grid.y + grid.height > height)) {
      diagnostics.push({ severity: "error", code: "WEBVAL003", message: `九宫格超出 ${width}x${height} PNG 范围。` });
    }
  }

  if (model.replacement.requireNonOpaqueAlpha && !model.replacement.requireAlphaChannel) {
    diagnostics.push({ severity: "error", code: "WEBVAL004", message: "透明像素约束需要同时启用 Alpha 通道约束。" });
  }

  return diagnostics;
}

function updateReplacementFileUi() {
  el.replacementFileName.textContent = state.replacementFileName || "PNG 候选文件";
  el.replacementFileMeta.textContent = state.replacementFileMeta || "未选择文件";
}

function resetReplacementState() {
  state.replacementDataUrl = "";
  state.replacementFileName = "";
  state.replacementFileMeta = "";
  state.replacementPlan = null;
  if (el.replacementInput) el.replacementInput.value = "";
  updateReplacementFileUi();
}

function resetCreateState() {
  state.createDataUrl = "";
  state.createFileName = "";
  state.createImageInfo = null;
  state.createPlan = null;
  if (el.createFileInput) el.createFileInput.value = "";
  el.createResourceIdInput.value = "";
  el.createRelativePathInput.value = "";
  el.createDisplayNameInput.value = "";
  el.createDescriptionInput.value = "";
  el.createTagsInput.value = "";
  el.createCategoryInput.value = "ButtonBase";
  el.createPolicyInput.value = "NineSlice";
  el.createDesignWidthInput.value = "";
  el.createDesignHeightInput.value = "";
  el.createScale9XInput.value = "";
  el.createScale9YInput.value = "";
  el.createScale9WidthInput.value = "";
  el.createScale9HeightInput.value = "";
  el.createRequireAlphaInput.checked = false;
  el.createRequireNonOpaqueInput.checked = false;
  el.createRequireSameSizeInput.checked = true;
  el.createAllowStretchInput.checked = true;
  el.createReplaceTogetherInput.checked = false;
  el.createMinWidthInput.value = "";
  el.createMinHeightInput.value = "";
  renderPlan(null, el.createPlanOutput);
  renderCreatePanel();
}

function clearCreatePlan() {
  if (!state.createPlan) {
    renderCreatePanel();
    return;
  }

  state.createPlan = null;
  renderPlan(null, el.createPlanOutput);
  renderCreatePanel();
}

function getResources() {
  return state.artState?.resources || state.artState?.catalog?.resources || [];
}

function findResource(resourceId) {
  return getResources().find(resource => resource.resourceId === resourceId) || null;
}

function matchesFilters(resource) {
  if (state.category && resource.category !== state.category) return false;
  const haystack = [
    resource.resourceId,
    resource.relativePath,
    resource.category,
    resource.displayName,
    ...(Array.isArray(resource.tags) ? resource.tags : [])
  ].join(" ").toLowerCase();
  return haystack.includes(state.search.trim().toLowerCase());
}

function getDiagnosticsForResource(resource) {
  if (!resource) return [];
  const diagnostics = state.artState?.diagnostics || [];
  return diagnostics.filter(diagnostic => diagnostic.resourceId === resource.resourceId);
}

function getDiagnosticCounts(diagnostics) {
  const counts = { error: 0, warning: 0, info: 0 };
  for (const diagnostic of diagnostics || []) {
    if (diagnostic.severity === "error") counts.error += 1;
    else if (diagnostic.severity === "warning") counts.warning += 1;
    else counts.info += 1;
  }
  return counts;
}

function buildSourcePath(resource) {
  const root = state.artState?.resourceRoot || "";
  return normalizeRelativePath(`${root}/${resource.relativePath || ""}`);
}

function defaultScale9Grid(width, height) {
  if (!width || !height) return null;
  const x = Math.max(1, Math.floor(width * 0.1));
  const y = Math.max(1, Math.floor(height * 0.28));
  return {
    x,
    y,
    width: Math.max(1, width - x * 2),
    height: Math.max(1, height - y * 2)
  };
}

function categoryLabel(value) {
  return CATEGORY_LABELS[value] || value || "未分类";
}

function usagePolicyLabel(value) {
  return USAGE_POLICY_LABELS[value] || value || "未设置";
}

function operationLabel(value) {
  if (value === "SaveCatalogSemantics") return "保存目录语义";
  if (value === "ReplaceExistingPng") return "替换现有 PNG";
  if (value === "CreateResource") return "新增资源";
  if (value === "RegisterDraftResource") return "登记草稿资源";
  if (value === "RegisterDraftSlice") return "登记草稿资源";
  if (value === "Plan") return "计划";
  return value || "计划";
}

function yesNo(value) {
  return value ? "是" : "否";
}

function splitTags(value) {
  return (value || "")
    .split(",")
    .map(item => item.trim())
    .filter(Boolean);
}

function toNonNegativeInt(value) {
  const number = Number.parseInt(value, 10);
  return Number.isFinite(number) && number > 0 ? number : 0;
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

function normalizeRelativePath(value) {
  return (value || "").replace(/\\/g, "/").replace(/^\/+/, "").replace(/\/+/g, "/").trim();
}

function stripExtension(fileName) {
  return (fileName || "").replace(/\.[^.]+$/, "");
}

function slugify(value) {
  const slug = (value || "resource")
    .toLowerCase()
    .replace(/[^a-z0-9._-]+/g, "_")
    .replace(/^_+|_+$/g, "");
  return slug || "resource";
}

function formatBytes(bytes) {
  if (!bytes) return "0 B";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function readFileAsDataUrl(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result || ""));
    reader.onerror = () => reject(reader.error || new Error("FileReader failed."));
    reader.readAsDataURL(file);
  });
}

function readImageInfoFromDataUrl(dataUrl) {
  return new Promise(resolve => {
    const image = new Image();
    image.onload = () => {
      const width = image.naturalWidth || image.width;
      const height = image.naturalHeight || image.height;
      let hasNonOpaqueAlpha = false;
      try {
        const canvas = document.createElement("canvas");
        canvas.width = width;
        canvas.height = height;
        const context = canvas.getContext("2d", { willReadFrequently: true });
        context.drawImage(image, 0, 0);
        const pixels = context.getImageData(0, 0, width, height).data;
        for (let i = 3; i < pixels.length; i += 4) {
          if (pixels[i] < 255) {
            hasNonOpaqueAlpha = true;
            break;
          }
        }
      } catch (error) {
        console.warn(error);
      }
      resolve({ width, height, hasNonOpaqueAlpha });
    };
    image.onerror = () => resolve(null);
    image.src = dataUrl;
  });
}

function renderOptions(values, labeler) {
  return values.map(value => `<option value="${escapeHtml(value)}">${escapeHtml(labeler(value))}</option>`).join("");
}

function cloneJson(value) {
  return JSON.parse(JSON.stringify(value));
}

function matchesLibrary(library, value) {
  return library.libraryId === value || library.resourceRoot === value || library.catalogPath === value;
}

function confirmDiscardDirty() {
  if (!state.formDirty) return true;
  return window.confirm("当前资源有未保存改动，确定放弃吗？");
}

function setStatus(text) {
  el.statusText.textContent = text;
}

function escapeHtml(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}
