const API_ROOT = "/api/authoring/ui-composer";
const EXPECTED_API_VERSION = "mx.uiComposer.authoring.v5";
const MAX_HISTORY = 50;

const state = {
  screens: [],
  screenId: "ui.screen.character_test.main_menu",
  serverState: null,
  workspace: null,
  resourceDraft: null,
  recipeDraft: null,
  resourceBaseline: "",
  recipeBaseline: "",
  selectedResourceId: "",
  selectedRecipeId: "",
  contextStack: [],
  blueprint: null,
  baseline: "",
  selectedNodeIds: [],
  primarySelectionId: "",
  selectedContainerId: "",
  selectionAnchorId: "",
  collapsedContainers: new Set(),
  lockedNodeIds: new Set(),
  hiddenNodeIds: new Set(),
  isolatedNodeIds: null,
  hierarchyQuery: "",
  hierarchyFilter: "all",
  diagnostics: [],
  history: [],
  future: [],
  zoom: 55,
  resolution: { width: 1920, height: 1080 },
  buttonState: "up",
  gridVisible: true,
  snapEnabled: true,
  gridSize: 8,
  adaptation: { scaleMode: "ScaleWithScreenSize", screenMathMode: "MatchWidthOrHeight", designResolutionX: 1920, designResolutionY: 1080 },
  drag: null,
  planConfirm: null,
  clipboard: null,
  recoveryCandidate: null,
  recoveryTimer: null,
  replaceReferences: null,
  spacePressed: false,
  canvasTool: "select",
  favoriteKeys: new Set(),
  recentKeys: [],
  busy: false
};

const el = {};

document.addEventListener("DOMContentLoaded", async () => {
  for (const id of [
    "statusText", "screenSelect", "dirtyBadge", "layerDirtySummary", "validateButton", "saveLayerButton", "saveButton", "syncButton", "applyButton", "publishButton", "commandPaletteButton", "reviewChangesButton", "topbarOverflowMenu", "toggleLeftPanelButton", "toggleInspectorButton",
    "hierarchyPanel", "resourcesPanel", "recipesPanel", "componentsPanel", "nodeCount", "hierarchyList", "hierarchySearchInput", "hierarchyFilterSelect", "toggleSelectedLockButton", "toggleSelectedVisibilityButton", "isolateSelectionButton", "resourceCount", "resourceSearchInput", "resourceList", "openResourceOperationsButton", "resourceOperationsDialog", "resourceOperationsFrame", "closeResourceOperationsButton", "recipeCount", "recipeSearchInput", "recipeList", "duplicateRecipeButton", "moveNodeUpButton", "moveNodeDownButton", "groupSelectionButton", "ungroupButton", "batchLayoutMenu",
    "undoButton", "redoButton", "selectToolButton", "panToolButton", "gridToggle", "snapToggle", "gridSizeSelect", "resolutionSelect", "fitCanvasButton", "focusSelectionButton", "compareResolutionsButton", "zoomRange", "zoomOutput",
    "canvasViewport", "physicalFrame", "designCanvas", "canvasMetrics", "selectionMetrics", "canvasRulerTop", "canvasRulerLeft", "canvasRulerCorner", "canvasDiagnosticSummary", "anchorMatrix",
    "inspectorTitle", "inspectorSubtitle", "deleteNodeButton", "xInput", "yInput", "widthInput", "heightInput",
    "horizontalAnchorSelect", "verticalAnchorSelect", "nameInput", "graphFields", "graphColorInput", "graphColorTextInput", "multiSelectionSummary", "containerFields", "containerModeSelect", "containerColumnsInput", "containerGapXInput", "containerGapYInput", "containerMainAlignSelect", "containerCrossAlignSelect", "paddingLeftInput", "paddingTopInput", "paddingRightInput", "paddingBottomInput",
    "cornerRadiusInput", "textFields", "textValueInput", "fontSizeInput", "fontFamilyInput", "textAlignSelect", "textColorInput", "textColorTextInput",
    "boldInput", "singleLineInput", "textInputToggle", "textRestrictInput", "textMaxLengthInput", "imageFields", "imageResourceSelect", "imageTintInput", "imageTintTextInput", "imageUsageSummary",
    "componentFields", "componentRecipeInput", "componentTitleInput", "componentIconSelect", "componentUsageSummary", "listFields", "listItemRecipeInput", "listLayoutSelect", "listSelectionModeSelect", "listLineGapInput", "listColumnGapInput", "listVirtualizedInput", "listItemUsageSummary", "bindingSlotSelect",
    "bindingDetails", "bindingContractList", "diagnosticsToggle", "diagnosticCount", "diagnosticsList",
    "contextInspector", "contextBackButton", "contextTitle", "contextSubtitle", "favoriteContextButton", "nodeInspectorShell", "resourceEditor", "resourcePreview", "resourceIdInput", "resourceDisplayNameInput", "resourceCategoryInput", "resourceDescriptionInput", "resourceTagsInput", "resourceWidthInput", "resourceHeightInput", "resourcePolicySelect", "resourceReferences", "recipeEditor", "recipeIdInput", "recipeDisplayNameInput", "recipeFguiNameInput", "recipeWidthInput", "recipeHeightInput", "recipeBaseResourceSelect", "recipeTitlePropertyInput", "recipeIconPropertyInput", "recipeOverColorInput", "recipeDownColorInput", "recipeTitleColorInput", "recipeIconColorInput", "recipeReferences", "deleteRecipeButton",
    "planDialog", "planDialogTitle", "planDialogSubtitle", "planSummary", "planDiagnostics", "planDiffs", "closePlanDialogButton",
    "cancelPlanButton", "confirmPlanButton", "recoveryDialog", "recoverySummary", "discardRecoveryButton", "restoreRecoveryButton", "resolutionCompareDialog", "resolutionCompareGrid", "closeResolutionCompareButton", "replaceReferencesDialog", "replaceReferencesSummary", "replaceReferencesSelect", "closeReplaceReferencesButton", "cancelReplaceReferencesButton", "confirmReplaceReferencesButton", "commandPaletteDialog", "commandSearchInput", "commandList", "closeCommandPaletteButton", "reviewDialog", "reviewSummary", "closeReviewButton", "closeReviewFooterButton", "createSnapshotButton", "snapshotList", "toggleReviewModeButton", "toastRegion"
  ]) el[id] = document.getElementById(id);

  bindStaticEvents();
  await Promise.all([loadAdaptation(), loadInitialState()]);
});

function bindStaticEvents() {
  loadLibraryPreferences();
  loadCanvasPreferences();
  el.gridToggle.checked = state.gridVisible;
  el.snapToggle.checked = state.snapEnabled;
  el.gridSizeSelect.value = String(state.gridSize);
  document.querySelectorAll("[data-rail-tab]").forEach(button => button.addEventListener("click", () => switchTabs("rail", button.dataset.railTab)));
  document.querySelectorAll("[data-inspector-tab]").forEach(button => button.addEventListener("click", () => switchTabs("inspector", button.dataset.inspectorTab)));
  document.querySelectorAll("[data-button-state]").forEach(button => button.addEventListener("click", () => {
    state.buttonState = button.dataset.buttonState;
    document.querySelectorAll("[data-button-state]").forEach(item => item.classList.toggle("active", item === button));
    renderCanvas();
  }));

  el.screenSelect.addEventListener("change", async event => {
    if (hasAnyDirty() && !window.confirm("当前工作台有未保存改动，确定切换界面吗？")) {
      el.screenSelect.value = state.screenId;
      return;
    }
    state.screenId = event.target.value;
    await loadScreenState();
  });
  el.validateButton.addEventListener("click", validateBlueprint);
  el.saveLayerButton.addEventListener("click", saveCurrentLayer);
  el.saveButton.addEventListener("click", saveBlueprint);
  el.syncButton.addEventListener("click", planSync);
  el.applyButton.addEventListener("click", planApply);
  el.publishButton.addEventListener("click", confirmPublish);
  el.commandPaletteButton.addEventListener("click", openCommandPalette);
  el.closeCommandPaletteButton.addEventListener("click", closeCommandPalette);
  el.commandSearchInput.addEventListener("input", renderCommandPalette);
  el.favoriteContextButton.addEventListener("click", toggleCurrentFavorite);
  el.reviewChangesButton.addEventListener("click", openReviewDialog);
  document.querySelectorAll(".overflow-menu-panel button").forEach(button => button.addEventListener("click", () => button.closest("details")?.removeAttribute("open")));
  el.toggleLeftPanelButton.addEventListener("click", () => toggleResponsivePanel("left"));
  el.toggleInspectorButton.addEventListener("click", () => toggleResponsivePanel("inspector"));
  el.closeReviewButton.addEventListener("click", closeReviewDialog);
  el.closeReviewFooterButton.addEventListener("click", closeReviewDialog);
  el.createSnapshotButton.addEventListener("click", createLocalSnapshot);
  el.toggleReviewModeButton.addEventListener("click", toggleReviewMode);
  el.resourceSearchInput.addEventListener("input", renderResourceList);
  el.recipeSearchInput.addEventListener("input", renderRecipeList);
  el.duplicateRecipeButton.addEventListener("click", duplicateSelectedRecipe);
  el.openResourceOperationsButton.addEventListener("click", openResourceOperations);
  el.closeResourceOperationsButton.addEventListener("click", closeResourceOperations);
  el.contextBackButton.addEventListener("click", navigateContextBack);
  el.deleteRecipeButton.addEventListener("click", deleteSelectedRecipe);
  [el.resourceDisplayNameInput, el.resourceCategoryInput, el.resourceDescriptionInput, el.resourceTagsInput, el.resourceWidthInput, el.resourceHeightInput].forEach(input => input.addEventListener("input", applyResourceEditor));
  el.resourcePolicySelect.addEventListener("change", applyResourceEditor);
  [el.recipeDisplayNameInput, el.recipeFguiNameInput, el.recipeWidthInput, el.recipeHeightInput, el.recipeOverColorInput, el.recipeDownColorInput, el.recipeTitleColorInput, el.recipeIconColorInput].forEach(input => input.addEventListener("input", applyRecipeEditor));
  [el.recipeBaseResourceSelect, el.recipeTitlePropertyInput, el.recipeIconPropertyInput].forEach(input => input.addEventListener("change", applyRecipeEditor));
  el.undoButton.addEventListener("click", undo);
  el.redoButton.addEventListener("click", redo);
  el.selectToolButton.addEventListener("click", () => setCanvasTool("select"));
  el.panToolButton.addEventListener("click", () => setCanvasTool("pan"));
  el.moveNodeUpButton.addEventListener("click", () => moveSelectedLayer(1));
  el.moveNodeDownButton.addEventListener("click", () => moveSelectedLayer(-1));
  el.groupSelectionButton.addEventListener("click", () => groupSelection("Free"));
  el.toggleSelectedLockButton.addEventListener("click", toggleSelectedLock);
  el.toggleSelectedVisibilityButton.addEventListener("click", toggleSelectedVisibility);
  el.isolateSelectionButton.addEventListener("click", toggleIsolation);
  el.hierarchySearchInput.addEventListener("input", event => { state.hierarchyQuery = event.target.value; renderHierarchy(); });
  el.hierarchyFilterSelect.addEventListener("change", event => { state.hierarchyFilter = event.target.value; renderHierarchy(); });
  el.ungroupButton.addEventListener("click", ungroupSelection);
  el.deleteNodeButton.addEventListener("click", deleteSelectedNode);
  el.gridToggle.addEventListener("change", event => { state.gridVisible = event.target.checked; saveCanvasPreferences(); renderCanvas(); });
  el.snapToggle.addEventListener("change", event => { state.snapEnabled = event.target.checked; saveCanvasPreferences(); renderCanvasStatus(); });
  el.gridSizeSelect.addEventListener("change", event => { state.gridSize = Number(event.target.value) || 8; saveCanvasPreferences(); renderCanvas(); });
  el.resolutionSelect.addEventListener("change", event => { state.resolution = parseResolution(event.target.value); renderCanvas(); });
  el.fitCanvasButton.addEventListener("click", fitCanvas);
  el.focusSelectionButton.addEventListener("click", focusSelection);
  el.compareResolutionsButton.addEventListener("click", openResolutionCompare);
  el.closeResolutionCompareButton.addEventListener("click", closeResolutionCompare);
  el.closeReplaceReferencesButton.addEventListener("click", closeReplaceReferences);
  el.cancelReplaceReferencesButton.addEventListener("click", closeReplaceReferences);
  el.confirmReplaceReferencesButton.addEventListener("click", confirmReplaceReferences);
  el.zoomRange.addEventListener("input", event => { state.zoom = Number(event.target.value); renderCanvas(); });
  el.diagnosticsToggle.addEventListener("click", () => {
    const expanded = el.diagnosticsToggle.getAttribute("aria-expanded") === "true";
    el.diagnosticsToggle.setAttribute("aria-expanded", String(!expanded));
    el.diagnosticsList.hidden = expanded;
  });
  el.closePlanDialogButton.addEventListener("click", closePlanDialog);
  el.cancelPlanButton.addEventListener("click", closePlanDialog);
  el.confirmPlanButton.addEventListener("click", async () => {
    const action = state.planConfirm;
    closePlanDialog();
    if (action) await action();
  });
  el.restoreRecoveryButton.addEventListener("click", restoreRecoveryDraft);
  el.discardRecoveryButton.addEventListener("click", discardRecoveryDraft);

  document.querySelectorAll("[data-create-type]").forEach(button => {
    button.addEventListener("click", () => createNode(button.dataset.createType, null, button.dataset));
    button.addEventListener("dragstart", event => event.dataTransfer.setData("application/x-mx-ui-node", JSON.stringify({ type: button.dataset.createType, resourceId: button.dataset.resourceId || "", recipeId: button.dataset.recipeId || "" })));
  });
  document.querySelectorAll("[data-layout-action]").forEach(button => button.addEventListener("click", () => applyBatchLayout(button.dataset.layoutAction)));
  document.querySelectorAll("[data-container-mode]").forEach(button => button.addEventListener("click", () => groupSelection(button.dataset.containerMode)));
  document.querySelectorAll("[data-template]").forEach(button => button.addEventListener("click", () => createTemplate(button.dataset.template)));
  el.canvasViewport.addEventListener("dragover", event => event.preventDefault());
  el.canvasViewport.addEventListener("drop", event => {
    event.preventDefault();
    const encoded = event.dataTransfer.getData("application/x-mx-ui-node");
    if (!encoded) return;
    try {
      const value = JSON.parse(encoded);
      createNode(value.type, canvasPoint(event.clientX, event.clientY), value);
    } catch { createNode(encoded, canvasPoint(event.clientX, event.clientY)); }
  });
  el.designCanvas.addEventListener("pointerdown", beginCanvasInteraction);
  window.addEventListener("pointermove", continueCanvasInteraction);
  window.addEventListener("pointerup", endCanvasInteraction);
  window.addEventListener("keydown", handleKeyboard);
  window.addEventListener("keyup", event => { if (event.code === "Space") { state.spacePressed = false; el.canvasViewport.classList.toggle("pan-ready", state.canvasTool === "pan"); } });
  el.canvasViewport.addEventListener("wheel", handleCanvasWheel, { passive: false });
  el.canvasViewport.addEventListener("scroll", renderRulers, { passive: true });
  window.addEventListener("resize", renderRulers);
  window.addEventListener("beforeunload", event => {
    if (!hasAnyDirty()) return;
    event.preventDefault();
    event.returnValue = "";
  });

  const layoutInputs = [el.xInput, el.yInput, el.widthInput, el.heightInput, el.horizontalAnchorSelect, el.verticalAnchorSelect];
  layoutInputs.forEach(input => input.addEventListener("change", applyLayoutForm));
  el.anchorMatrix.querySelectorAll("[data-anchor-h]").forEach(button => button.addEventListener("click", () => applyAnchorPreset(button.dataset.anchorH, button.dataset.anchorV)));
  el.nameInput.addEventListener("change", applyNameForm);
  [el.graphColorInput, el.graphColorTextInput, el.cornerRadiusInput].forEach(input => input.addEventListener("change", applyGraphForm));
  [el.textValueInput, el.fontFamilyInput, el.fontSizeInput, el.textAlignSelect, el.textColorInput, el.textColorTextInput, el.boldInput, el.singleLineInput, el.textInputToggle, el.textRestrictInput, el.textMaxLengthInput].forEach(input => input.addEventListener("change", applyTextForm));
  [el.imageResourceSelect, el.imageTintInput, el.imageTintTextInput].forEach(input => input.addEventListener("change", applyImageForm));
  [el.componentRecipeInput, el.componentTitleInput, el.componentIconSelect].forEach(input => input.addEventListener("change", applyComponentForm));
  [el.listItemRecipeInput, el.listLayoutSelect, el.listSelectionModeSelect, el.listLineGapInput, el.listColumnGapInput, el.listVirtualizedInput].forEach(input => input.addEventListener("change", applyListForm));
  el.bindingSlotSelect.addEventListener("change", applyBindingForm);
  [el.containerModeSelect, el.containerColumnsInput, el.containerGapXInput, el.containerGapYInput, el.containerMainAlignSelect, el.containerCrossAlignSelect, el.paddingLeftInput, el.paddingTopInput, el.paddingRightInput, el.paddingBottomInput].forEach(input => input.addEventListener("change", applyContainerForm));
}

async function loadInitialState() {
  try {
    const capabilities = await getJson(`${API_ROOT}/capabilities`);
    if (capabilities.apiVersion !== EXPECTED_API_VERSION) throw new Error(`Authoring 服务版本不匹配：${capabilities.apiVersion || "未知"}`);
    state.screens = await getJson(`${API_ROOT}/screens`);
    if (!Array.isArray(state.screens) || state.screens.length === 0) throw new Error("没有发现 UI Composer 蓝图。");
    const query = new URLSearchParams(window.location.search).get("screen");
    if (query && state.screens.some(item => item.screenId === query)) state.screenId = query;
    else if (!state.screens.some(item => item.screenId === state.screenId)) state.screenId = state.screens[0].screenId;
    renderScreenOptions();
    await loadScreenState();
  } catch (error) {
    setStatus(`加载失败：${error.message || error}`, "error");
    showToast(error.message || String(error), true);
  }
}

async function loadAdaptation() {
  try {
    const response = await fetch("/FGUIProject/settings/Adaptation.json", { cache: "no-store" });
    if (response.ok) state.adaptation = await response.json();
  } catch { }
}

async function loadScreenState() {
  setBusy(true, "正在加载界面蓝图...");
  try {
    state.workspace = await getJson(`${API_ROOT}/workspace?screen=${encodeURIComponent(state.screenId)}`);
    state.serverState = state.workspace.authoringState;
    state.resourceDraft = clone(state.serverState.resourceCatalog);
    state.recipeDraft = clone(state.serverState.componentRecipes);
    state.resourceBaseline = JSON.stringify(state.resourceDraft);
    state.recipeBaseline = JSON.stringify(state.recipeDraft);
    state.blueprint = clone(state.serverState.blueprint);
    state.baseline = stableBlueprint(state.blueprint);
    state.selectedNodeIds = state.blueprint.nodes?.length ? [state.blueprint.nodes[0].nodeId] : [];
    state.primarySelectionId = state.selectedNodeIds[0] || "";
    state.selectedContainerId = "";
    state.collapsedContainers = loadCollapsedContainers();
    loadViewPreferences();
    state.diagnostics = state.serverState.diagnostics || [];
    state.history = [];
    state.future = [];
    state.recoveryCandidate = readRecoveryDraft();
    setStatus(`已加载 ${state.serverState.screen.displayName}，目标 ${state.serverState.screen.packageId}/${state.serverState.screen.componentName}`);
    renderAll();
    presentRecoveryDraft();
  } finally {
    setBusy(false);
  }
}

function renderScreenOptions() {
  el.screenSelect.innerHTML = state.screens.map(item => `<option value="${escapeHtml(item.screenId)}">${escapeHtml(item.displayName)} · ${escapeHtml(item.packageId)}</option>`).join("");
  el.screenSelect.value = state.screenId;
}

function renderAll() {
  renderHierarchy();
  renderResourceList();
  renderRecipeList();
  renderCanvas();
  renderInspector();
  renderBindings();
  renderDiagnostics();
  updateDirtyUi();
}

function renderResourceList() {
  if (!el.resourceList) return;
  const query = (el.resourceSearchInput.value || "").trim().toLowerCase();
  const values = sortLibraryItems(resources().filter(item => !query || `${item.resourceId} ${item.displayName} ${item.category}`.toLowerCase().includes(query)), "resource");
  el.resourceCount.textContent = `${values.length} 个资源`;
  el.resourceList.innerHTML = values.map(item => {
    const uses = workspaceReferences("resource", item.resourceId).length;
    return `<button class="semantic-list-item${state.selectedResourceId === item.resourceId ? " active" : ""}" type="button" data-resource-key="${escapeHtml(item.resourceId)}"><span class="semantic-thumb" style="background-image:url('${resourceUrl(item)}')"></span><span><strong>${state.favoriteKeys.has(`resource:${item.resourceId}`) ? "★ " : ""}${escapeHtml(item.displayName || item.resourceId)}</strong><small>${escapeHtml(item.category)} · ${uses} 处引用</small></span></button>`;
  }).join("") || '<div class="empty-state">没有匹配的语义资源。</div>';
  el.resourceList.querySelectorAll("[data-resource-key]").forEach(button => button.addEventListener("click", () => openResourceEditor(button.dataset.resourceKey, true)));
}

function openResourceOperations() {
  if (hasAnyDirty()) { showToast("请先保存当前草稿，再进行 PNG 文件操作。", true); return; }
  el.resourceOperationsFrame.src = `/Tools/MxFramework.FairyGuiArtSlices/web/?library=${encodeURIComponent(state.resourceDraft.libraryId)}${state.selectedResourceId ? `&resource=${encodeURIComponent(state.selectedResourceId)}` : ""}`;
  el.resourceOperationsDialog.showModal();
}

async function closeResourceOperations() {
  el.resourceOperationsDialog.close(); el.resourceOperationsFrame.src = "about:blank"; await loadScreenState();
}

function renderRecipeList() {
  if (!el.recipeList) return;
  const query = (el.recipeSearchInput.value || "").trim().toLowerCase();
  const values = sortLibraryItems(recipes().filter(item => !query || `${item.componentId} ${item.displayName}`.toLowerCase().includes(query)), "recipe");
  el.recipeCount.textContent = `${values.length} 个配方`;
  el.recipeList.innerHTML = values.map(item => {
    const uses = workspaceReferences("recipe", item.componentId).length;
    const base = recipeBaseResource(item);
    return `<button class="semantic-list-item${state.selectedRecipeId === item.componentId ? " active" : ""}" type="button" data-recipe-key="${escapeHtml(item.componentId)}"><span class="semantic-thumb" style="background-image:url('${resourceUrl(base)}')"></span><span><strong>${state.favoriteKeys.has(`recipe:${item.componentId}`) ? "★ " : ""}${escapeHtml(item.displayName || item.componentId)}</strong><small>${escapeHtml(item.componentType || "Button")} · ${uses} 个实例</small></span></button>`;
  }).join("") || '<div class="empty-state">没有匹配的组件配方。</div>';
  el.recipeList.querySelectorAll("[data-recipe-key]").forEach(button => button.addEventListener("click", () => openRecipeEditor(button.dataset.recipeKey, true)));
}

function workspaceReferences(kind, id) { return (state.workspace?.references || []).filter(item => item.targetKind === kind && item.targetId === id); }

function showContext(kind) {
  el.contextInspector.hidden = false;
  el.nodeInspectorShell.hidden = true;
  el.resourceEditor.hidden = kind !== "resource";
  el.recipeEditor.hidden = kind !== "recipe";
}

function closeContext() {
  el.contextInspector.hidden = true;
  el.nodeInspectorShell.hidden = false;
  state.selectedResourceId = "";
  state.selectedRecipeId = "";
  renderResourceList(); renderRecipeList();
}

function openResourceEditor(resourceId, pushHistory) {
  const resource = findResource(resourceId); if (!resource) return;
  if (pushHistory) state.contextStack.push(currentContext());
  state.selectedResourceId = resourceId; state.selectedRecipeId = "";
  rememberLibraryItem(`resource:${resourceId}`);
  showContext("resource");
  el.contextTitle.textContent = resource.displayName || resource.resourceId;
  el.contextSubtitle.textContent = `语义资源 · ${resource.resourceId}`;
  el.resourceIdInput.value = resource.resourceId;
  el.resourceDisplayNameInput.value = resource.displayName || "";
  el.resourceCategoryInput.value = resource.category || "";
  el.resourceDescriptionInput.value = resource.description || "";
  el.resourceTagsInput.value = (resource.tags || []).join(", ");
  el.resourceWidthInput.value = resource.recommendedFairyGuiUsage?.designSize?.width || 1;
  el.resourceHeightInput.value = resource.recommendedFairyGuiUsage?.designSize?.height || 1;
  el.resourcePolicySelect.value = resource.recommendedFairyGuiUsage?.policy || "Fixed";
  el.resourcePreview.style.backgroundImage = `url('${resourceUrl(resource)}')`;
  renderReferenceList(el.resourceReferences, workspaceReferences("resource", resourceId));
  updateFavoriteButton();
  renderResourceList(); renderRecipeList(); updateDirtyUi();
}

function openRecipeEditor(recipeId, pushHistory) {
  const recipe = findRecipe(recipeId); if (!recipe) return;
  if (pushHistory) state.contextStack.push(currentContext());
  state.selectedRecipeId = recipeId; state.selectedResourceId = "";
  rememberLibraryItem(`recipe:${recipeId}`);
  showContext("recipe");
  el.contextTitle.textContent = recipe.displayName || recipe.componentId;
  const editable = recipe.componentType === "Button";
  el.contextSubtitle.textContent = `${editable ? "按钮配方" : "受控复合配方（只读）"} · ${recipe.componentId}`;
  el.recipeIdInput.value = recipe.componentId;
  el.recipeDisplayNameInput.value = recipe.displayName || "";
  el.recipeFguiNameInput.value = recipe.fairyGuiComponentName || "";
  el.recipeWidthInput.value = recipe.defaultSize?.width || 1;
  el.recipeHeightInput.value = recipe.defaultSize?.height || 1;
  populateResourceSelect(el.recipeBaseResourceSelect, recipe.resourceSlots?.find(item => item.slotId === "base")?.resourceId, false, "ButtonBase");
  el.recipeTitlePropertyInput.checked = (recipe.instanceProperties || []).includes("title");
  el.recipeIconPropertyInput.checked = (recipe.instanceProperties || []).includes("iconResourceId");
  el.recipeOverColorInput.value = recipe.statePresentation?.overOverlayColor || "";
  el.recipeDownColorInput.value = recipe.statePresentation?.downOverlayColor || "";
  el.recipeTitleColorInput.value = recipe.statePresentation?.titleColor || "";
  el.recipeIconColorInput.value = recipe.statePresentation?.iconColor || "";
  [el.recipeDisplayNameInput, el.recipeFguiNameInput, el.recipeWidthInput, el.recipeHeightInput, el.recipeBaseResourceSelect, el.recipeTitlePropertyInput, el.recipeIconPropertyInput, el.recipeOverColorInput, el.recipeDownColorInput, el.recipeTitleColorInput, el.recipeIconColorInput, el.deleteRecipeButton]
    .forEach(input => { input.disabled = !editable; });
  renderReferenceList(el.recipeReferences, workspaceReferences("recipe", recipeId));
  updateFavoriteButton();
  renderResourceList(); renderRecipeList(); updateDirtyUi();
}

function renderReferenceList(target, references) {
  const kind = state.selectedResourceId ? "resource" : "recipe";
  const id = state.selectedResourceId || state.selectedRecipeId;
  target.innerHTML = `<div class="section-label">使用位置</div>${references.map(item => `<button type="button" class="reference-item" data-ref-kind="${escapeHtml(item.sourceKind)}" data-ref-id="${escapeHtml(item.sourceId)}" data-ref-screen="${escapeHtml(item.sourceScreenId || "")}"><strong>${escapeHtml(item.sourceId)}</strong><small>${escapeHtml(item.sourceScreenId || item.sourceKind)} · ${escapeHtml(item.role)}</small></button>`).join("") || '<div class="empty-state">暂无引用。</div>'}${references.length ? `<button type="button" class="secondary batch-replace-button" data-batch-replace="${kind}" data-source-id="${escapeHtml(id)}">批量替换引用</button>` : ""}`;
  target.querySelectorAll("[data-ref-kind]").forEach(button => button.addEventListener("click", () => navigateReference(button.dataset.refKind, button.dataset.refId, button.dataset.refScreen)));
  target.querySelectorAll("[data-batch-replace]").forEach(button => button.addEventListener("click", () => openReplaceReferences(button.dataset.batchReplace, button.dataset.sourceId, references.length)));
}

async function navigateReference(kind, id, screenId) {
  if (kind === "recipe") { switchTabs("rail", "recipes"); openRecipeEditor(id, true); return; }
  if (kind === "node") {
    if (screenId && screenId !== state.screenId) {
      if (hasAnyDirty() && !window.confirm("切换使用界面会丢失未保存改动，是否继续？")) return;
      state.screenId = screenId; el.screenSelect.value = screenId; await loadScreenState();
    }
    state.contextStack.push(currentContext()); closeContext(); switchTabs("rail", "hierarchy"); selectNode(id);
  }
}

function currentContext() { return state.selectedResourceId ? { kind: "resource", id: state.selectedResourceId } : state.selectedRecipeId ? { kind: "recipe", id: state.selectedRecipeId } : { kind: "node", id: state.primarySelectionId }; }
function navigateContextBack() { const previous = state.contextStack.pop(); if (!previous || previous.kind === "node") { closeContext(); if (previous?.id) selectNode(previous.id); return; } if (previous.kind === "resource") openResourceEditor(previous.id, false); else openRecipeEditor(previous.id, false); }

function applyResourceEditor() {
  const resource = findResource(state.selectedResourceId); if (!resource) return;
  resource.displayName = el.resourceDisplayNameInput.value.trim(); resource.category = el.resourceCategoryInput.value.trim(); resource.description = el.resourceDescriptionInput.value.trim(); resource.tags = el.resourceTagsInput.value.split(",").map(value => value.trim()).filter(Boolean);
  resource.recommendedFairyGuiUsage ||= {}; resource.recommendedFairyGuiUsage.designSize ||= {};
  resource.recommendedFairyGuiUsage.designSize.width = Math.max(1, Number(el.resourceWidthInput.value) || 1); resource.recommendedFairyGuiUsage.designSize.height = Math.max(1, Number(el.resourceHeightInput.value) || 1); resource.recommendedFairyGuiUsage.policy = el.resourcePolicySelect.value;
  el.contextTitle.textContent = resource.displayName || resource.resourceId; renderResourceList(); renderCanvas(); updateDirtyUi();
}

function applyRecipeEditor() {
  const recipe = findRecipe(state.selectedRecipeId); if (!recipe) return;
  if (recipe.componentType !== "Button") return;
  recipe.displayName = el.recipeDisplayNameInput.value.trim(); recipe.fairyGuiComponentName = el.recipeFguiNameInput.value.trim(); recipe.defaultSize = { width: Math.max(1, Number(el.recipeWidthInput.value) || 1), height: Math.max(1, Number(el.recipeHeightInput.value) || 1) };
  recipe.resourceSlots = [{ slotId: "base", resourceId: el.recipeBaseResourceSelect.value }]; recipe.instanceProperties = [el.recipeTitlePropertyInput.checked ? "title" : "", el.recipeIconPropertyInput.checked ? "iconResourceId" : ""].filter(Boolean);
  recipe.statePresentation ||= { controllerName: "button", pages: ["up", "down", "over", "selectedOver"] };
  recipe.statePresentation.overOverlayColor = el.recipeOverColorInput.value.trim(); recipe.statePresentation.downOverlayColor = el.recipeDownColorInput.value.trim(); recipe.statePresentation.titleColor = el.recipeTitleColorInput.value.trim(); recipe.statePresentation.iconColor = el.recipeIconColorInput.value.trim();
  el.contextTitle.textContent = recipe.displayName || recipe.componentId; renderRecipeList(); renderCanvas(); updateDirtyUi();
}

function duplicateSelectedRecipe() {
  const source = findRecipe(state.selectedRecipeId) || recipes()[0]; if (!source) return;
  let index = 1; let id; do { id = `${source.componentId}.copy${index++}`; } while (findRecipe(id));
  const copy = clone(source); copy.componentId = id; copy.displayName = `${source.displayName || source.componentId} 副本`; copy.fairyGuiComponentName = `${source.fairyGuiComponentName || "Button"}Copy${index - 1}`;
  state.recipeDraft.recipes.push(copy); switchTabs("rail", "recipes"); openRecipeEditor(id, true); updateDirtyUi();
}

async function deleteSelectedRecipe() {
  const recipe = findRecipe(state.selectedRecipeId); if (!recipe) return;
  const response = await postJson(`${API_ROOT}/plan-delete-recipe`, { screen: state.screenId, recipeId: recipe.componentId });
  if (!response.ok) { showPlan("无法删除配方", "先从所有节点替换该配方。", response.data.plan, null); return; }
  state.recipeDraft.recipes = state.recipeDraft.recipes.filter(item => item.componentId !== recipe.componentId); closeContext(); updateDirtyUi();
}

function renderHierarchy() {
  const allNodes = orderedNodes();
  const allContainers = state.blueprint?.containers || [];
  const visibleCount = allNodes.filter(hierarchyNodeMatches).length;
  el.nodeCount.textContent = visibleCount === allNodes.length
    ? `${allNodes.length} 个节点 · ${allContainers.length} 个容器`
    : `${visibleCount} / ${allNodes.length} 个节点`;
  el.hierarchySearchInput.value = state.hierarchyQuery;
  el.hierarchyFilterSelect.value = state.hierarchyFilter;
  el.hierarchyList.innerHTML = renderHierarchyLevel("");
  el.hierarchyList.querySelectorAll(".tree-toggle").forEach(button => button.addEventListener("click", event => {
    event.stopPropagation();
    const id = button.closest(".hierarchy-row").dataset.containerId;
    if (state.collapsedContainers.has(id)) state.collapsedContainers.delete(id); else state.collapsedContainers.add(id);
    saveCollapsedContainers(); renderHierarchy();
  }));
  el.hierarchyList.querySelectorAll(".hierarchy-row").forEach(row => {
    const viewIds = row.dataset.nodeId ? [row.dataset.nodeId] : descendantNodeIds(row.dataset.containerId);
    row.classList.toggle("locked", viewIds.length > 0 && viewIds.every(id => state.lockedNodeIds.has(id)));
    row.classList.toggle("hidden-node", viewIds.length > 0 && viewIds.every(id => state.hiddenNodeIds.has(id)));
    row.addEventListener("click", event => row.dataset.nodeId ? selectNode(row.dataset.nodeId, event) : selectContainer(row.dataset.containerId));
    row.addEventListener("dragstart", event => {
      row.classList.add("dragging");
      event.dataTransfer.setData("application/x-mx-ui-hierarchy", JSON.stringify({ nodeId: row.dataset.nodeId || "", containerId: row.dataset.containerId || "" }));
    });
    row.addEventListener("dragend", () => row.classList.remove("dragging"));
    row.addEventListener("dragover", event => { event.preventDefault(); row.classList.add("drop-target"); });
    row.addEventListener("dragleave", () => row.classList.remove("drop-target"));
    row.addEventListener("drop", event => {
      event.preventDefault();
      row.classList.remove("drop-target");
      moveHierarchyItem(event.dataTransfer.getData("application/x-mx-ui-hierarchy"), row.dataset.nodeId || "", row.dataset.containerId || "");
    });
  });
  const selectedIds = selectedViewNodeIds();
  el.toggleSelectedLockButton.classList.toggle("active", selectedIds.length > 0 && selectedIds.every(id => state.lockedNodeIds.has(id)));
  el.toggleSelectedVisibilityButton.classList.toggle("active", selectedIds.length > 0 && selectedIds.every(id => state.hiddenNodeIds.has(id)));
  el.isolateSelectionButton.classList.toggle("active", Boolean(state.isolatedNodeIds));
}

function renderHierarchyLevel(parentId) {
  const childContainers = (state.blueprint?.containers || []).filter(item => (item.parentContainerId || "") === parentId && hierarchyContainerMatches(item)).sort((a, b) => a.order - b.order || a.containerId.localeCompare(b.containerId));
  const childNodes = orderedNodes().filter(node => (node.parentContainerId || "") === parentId && hierarchyNodeMatches(node));
  const items = [
    ...childContainers.map(value => ({ kind: "container", order: value.order, id: value.containerId, value })),
    ...childNodes.map(value => ({ kind: "node", order: value.order, id: value.nodeId, value }))
  ].sort((a, b) => a.order - b.order || a.id.localeCompare(b.id));
  return items.map(item => {
    if (item.kind === "node") {
      const node = item.value, selected = state.selectedNodeIds.includes(node.nodeId), primary = node.nodeId === state.primarySelectionId;
      return `<div class="hierarchy-row ${primary ? "selected" : selected ? "multi-selected" : ""}${node.visible === false ? " blueprint-hidden" : ""}" draggable="true" data-node-id="${escapeHtml(node.nodeId)}">
        <span class="node-icon">${nodeTypeIcon(node.type)}</span><span class="node-label"><strong>${escapeHtml(node.name)}</strong><small>${escapeHtml(node.type)} · ${node.layout.width}×${node.layout.height}${node.visible === false ? " · 初始隐藏" : ""}</small></span>${node.bindingSlot ? `<span class="binding-dot" title="绑定：${escapeHtml(node.bindingSlot)}"></span>` : ""}</div>`;
    }
    const container = item.value, collapsed = state.collapsedContainers.has(container.containerId), selected = state.selectedContainerId === container.containerId;
    const count = directNodeDescendants(container.containerId).length;
    return `<div class="hierarchy-row container-row ${selected ? "selected" : ""}" draggable="true" data-container-id="${escapeHtml(container.containerId)}"><button class="tree-toggle" type="button" aria-label="${collapsed ? "展开" : "折叠"}">${collapsed ? "▸" : "▾"}</button><span class="node-label"><strong>${escapeHtml(container.name)}</strong><small>${escapeHtml(container.mode)} · ${count} 个节点</small></span><span class="node-icon">组</span></div>${collapsed ? "" : `<div class="hierarchy-children">${renderHierarchyLevel(container.containerId)}</div>`}`;
  }).join("");
}

function hierarchyNodeMatches(node) {
  const query = state.hierarchyQuery.trim().toLocaleLowerCase();
  const searchable = [node.name, node.nodeId, node.type, node.bindingSlot, node.image?.resourceId, node.component?.componentRecipeId, node.component?.iconResourceId, node.list?.itemComponentRecipeId]
    .filter(Boolean).join(" ").toLocaleLowerCase();
  if (query && !searchable.includes(query)) return false;
  if (state.hierarchyFilter === "unbound" && node.bindingSlot) return false;
  if (state.hierarchyFilter === "diagnostic" && !nodeHasDiagnostic(node.nodeId)) return false;
  if (state.hierarchyFilter === "container" && (node.parentContainerId || "") !== activeHierarchyContainerId()) return false;
  return true;
}

function hierarchyContainerMatches(container) {
  const query = state.hierarchyQuery.trim().toLocaleLowerCase();
  const ownMatch = !query || `${container.name || ""} ${container.containerId} ${container.mode || ""}`.toLocaleLowerCase().includes(query);
  if (state.hierarchyFilter === "container" && container.containerId !== activeHierarchyContainerId()) return false;
  if (ownMatch && state.hierarchyFilter !== "unbound" && state.hierarchyFilter !== "diagnostic") return true;
  return descendantNodeIds(container.containerId).some(id => {
    const node = state.blueprint.nodes.find(item => item.nodeId === id);
    return node && hierarchyNodeMatches(node);
  });
}

function activeHierarchyContainerId() {
  return state.selectedContainerId || selectedNode()?.parentContainerId || "";
}

function nodeHasDiagnostic(nodeId) {
  return (state.diagnostics || []).some(item => item.nodeId === nodeId || item.objectId === nodeId || String(item.path || item.location || "").includes(nodeId));
}

function renderCanvas() {
  if (!state.blueprint) return;
  const logical = logicalPreviewSize();
  const scale = state.zoom / 100;
  el.physicalFrame.style.width = `${logical.width * scale}px`;
  el.physicalFrame.style.height = `${logical.height * scale}px`;
  el.designCanvas.style.width = `${logical.width}px`;
  el.designCanvas.style.height = `${logical.height}px`;
  el.designCanvas.style.transform = `scale(${scale})`;
  el.designCanvas.classList.toggle("grid", state.gridVisible);
  el.designCanvas.classList.toggle("grid-minor-visible", state.gridSize * scale >= 7);
  el.designCanvas.style.setProperty("--grid-step", `${state.gridSize}px`);
  el.designCanvas.style.setProperty("--grid-major-step", `${state.gridSize * 5}px`);
  el.designCanvas.dataset.buttonState = state.buttonState;
  el.zoomOutput.value = `${state.zoom}%`;
  const deltaWidth = logical.width - state.blueprint.designResolution.width;
  const deltaHeight = logical.height - state.blueprint.designResolution.height;
  const materialized = MxComposerLayout.materialize(state.blueprint);
  const containerMarkup = (state.blueprint.containers || []).map(container => renderCanvasContainer(container, deltaWidth, deltaHeight)).join("");
  const marquee = state.drag?.mode === "marquee" ? renderMarquee(state.drag) : "";
  const visibleNodes = orderedNodes().filter(node => isNodeVisible(node.nodeId) && (node.visible !== false || state.selectedNodeIds.includes(node.nodeId)));
  el.designCanvas.innerHTML = containerMarkup + visibleNodes.map(node => renderCanvasNode(node, materialized.get(node.nodeId) || node.layout, deltaWidth, deltaHeight)).join("") + marquee;
  el.canvasMetrics.textContent = `设计 ${state.blueprint.designResolution.width}×${state.blueprint.designResolution.height} · 预览 ${state.resolution.width}×${state.resolution.height}`;
  const selected = selectedNode();
  el.selectionMetrics.textContent = state.selectedContainerId ? `容器 / ${selectedContainer()?.name || ""}` : state.selectedNodeIds.length > 1 ? `已选择 ${state.selectedNodeIds.length} 个节点` : selected ? nodeBreadcrumb(selected) : "未选择节点";
  renderCanvasStatus();
  requestAnimationFrame(renderRulers);
}

function renderCanvasStatus() {
  if (!el.canvasDiagnosticSummary) return;
  const count = state.diagnostics?.length || 0;
  const gridStatus = state.gridVisible ? `网格 ${state.gridSize}px` : "网格隐藏";
  const snapStatus = state.snapEnabled ? `吸附 ${state.gridSize}px` : "自由移动";
  el.canvasDiagnosticSummary.textContent = `缩放 ${state.zoom}% · ${gridStatus} · ${snapStatus} · 诊断 ${count}`;
}

function nodeBreadcrumb(node) {
  const values = [node.name];
  let parentId = node.parentContainerId;
  while (parentId) {
    const parent = (state.blueprint.containers || []).find(item => item.containerId === parentId);
    if (!parent) break;
    values.unshift(parent.name);
    parentId = parent.parentContainerId;
  }
  return values.join(" / ");
}

function renderRulers() {
  if (!state.blueprint || !el.canvasRulerTop || !el.canvasRulerLeft) return;
  const logical = logicalPreviewSize();
  const scale = state.zoom / 100;
  const majorStep = rulerStepForScale(scale);
  const minorStep = majorStep / 5;
  const rulerTopOffset = 64;
  const rulerThickness = 22;
  const rulerLeftWidth = 28;
  const scrollLeft = el.canvasViewport.scrollLeft;
  const scrollTop = el.canvasViewport.scrollTop;
  const frameLeft = el.physicalFrame.offsetLeft - scrollLeft;
  const frameTop = el.physicalFrame.offsetTop - scrollTop;
  Object.assign(el.canvasRulerTop.style, {
    left: `${scrollLeft + rulerLeftWidth}px`,
    top: `${scrollTop + rulerTopOffset}px`,
    width: `${Math.max(0, el.canvasViewport.clientWidth - rulerLeftWidth)}px`
  });
  Object.assign(el.canvasRulerLeft.style, {
    left: `${scrollLeft}px`,
    top: `${scrollTop + rulerTopOffset + rulerThickness}px`,
    height: `${Math.max(0, el.canvasViewport.clientHeight - rulerTopOffset - rulerThickness)}px`
  });
  Object.assign(el.canvasRulerCorner.style, {
    left: `${scrollLeft}px`,
    top: `${scrollTop + rulerTopOffset}px`
  });
  const horizontal = [];
  for (let value = 0; value <= logical.width; value += minorStep) {
    const major = value % majorStep === 0;
    const position = Math.round(frameLeft - rulerLeftWidth + value * scale);
    horizontal.push(`<span class="ruler-tick${major ? " major" : ""}${value === 0 ? " origin" : ""}" style="left:${position}px">${major ? `<span>${value}</span>` : ""}</span>`);
  }
  const vertical = [];
  for (let value = 0; value <= logical.height; value += minorStep) {
    const major = value % majorStep === 0;
    const position = Math.round(frameTop - rulerTopOffset - rulerThickness + value * scale);
    vertical.push(`<span class="ruler-tick${major ? " major" : ""}${value === 0 ? " origin" : ""}" style="top:${position}px">${major ? `<span>${value}</span>` : ""}</span>`);
  }
  el.canvasRulerTop.innerHTML = horizontal.join("");
  el.canvasRulerLeft.innerHTML = vertical.join("");
}

function rulerStepForScale(scale) {
  return [10, 20, 50, 100, 200, 500, 1000].find(step => step * scale >= 72) || 1000;
}

function renderMarquee(value) {
  const left = Math.min(value.startX, value.currentX), top = Math.min(value.startY, value.currentY);
  return `<div class="selection-marquee" style="left:${left}px;top:${top}px;width:${Math.abs(value.currentX - value.startX)}px;height:${Math.abs(value.currentY - value.startY)}px"></div>`;
}

function renderCanvasNode(node, sourceLayout, deltaWidth, deltaHeight) {
  const layout = adjustedLayout(sourceLayout, deltaWidth, deltaHeight);
  const selected = node.nodeId === state.primarySelectionId ? " selected" : state.selectedNodeIds.includes(node.nodeId) ? " multi-selected" : "";
  const bound = node.bindingSlot ? " binding-required" : "";
  const style = [`left:${layout.x}px`, `top:${layout.y}px`, `width:${layout.width}px`, `height:${layout.height}px`];
  let content = "";
  let kind = node.type.toLowerCase();
  if (node.type === "Graph") {
    style.push(`background:${fguiColorToCss(node.graph?.fillColor)}`, `border-radius:${node.graph?.cornerRadius || 0}px`);
  } else if (node.type === "Text") {
    style.push(`color:${fguiColorToCss(node.text?.color)}`, `font-size:${node.text?.fontSize || 16}px`, fontFamilyStyle(node.text?.font), `font-weight:${node.text?.bold ? 700 : 400}`, `justify-content:${alignToFlex(node.text?.align)}`, `text-align:${node.text?.align || "left"}`);
    if (node.text?.singleLine) style.push("white-space:nowrap", "text-overflow:ellipsis");
    content = escapeHtml(node.text?.value || "");
  } else if (node.type === "Image") {
    const resource = findResource(node.image?.resourceId);
    style.push(...semanticImageStyle(resource, node.image?.tint));
    content = `<span class="semantic-node-label">${escapeHtml(resource?.displayName || node.image?.resourceId || "语义图片")}</span>`;
  } else if (node.type === "List") {
    kind = "list";
    const recipe = findRecipe(node.list?.itemComponentRecipeId);
    const itemResource = recipeBaseResource(recipe);
    const direction = node.list?.layout === "Row" ? "row" : "column";
    const gap = Math.max(0, Number(direction === "row" ? node.list?.columnGap : node.list?.lineGap) || 0);
    const itemStyle = semanticImageStyle(itemResource, "#ffffffff").join(";");
    style.push(`--list-direction:${direction}`, `--list-gap:${gap}px`);
    content = `<div class="list-node-preview direction-${direction}">${[0, 1, 2].map(() => `<span class="list-node-item" style="${itemStyle}"></span>`).join("")}</div><span class="list-node-label">GList · ${escapeHtml(recipe?.displayName || node.list?.itemComponentRecipeId || "条目配方")}</span>`;
  } else {
    kind = "component";
    const recipe = findRecipe(node.component?.componentRecipeId);
    if (recipe?.componentType === "Composite") {
      kind = "component composite";
      style.push("background:transparent", "padding:0", "overflow:hidden");
      content = renderCompositePreview(recipe);
      return `<div class="canvas-node ${kind}${selected}${bound}" data-node-id="${escapeHtml(node.nodeId)}" style="${style.join(";")}">${content}${node.nodeId === state.primarySelectionId && state.selectedNodeIds.length === 1 ? '<span class="resize-handle" data-resize-handle="true"></span>' : ""}</div>`;
    }
    const baseResource = recipeBaseResource(recipe);
    style.push(...semanticImageStyle(baseResource, "#ffffffff"));
    const presentation = recipe?.statePresentation || {};
    style.push(
      `color:${fguiColorToCss(presentation.titleColor || "#f3f5f4")}`,
      `font-size:${presentation.titleFontSize || 22}px`, fontFamilyStyle(presentation.titleFont), `font-weight:${presentation.titleBold === false ? 400 : 700}`,
      `--component-over-overlay:${fguiColorToCss(presentation.overOverlayColor || "#1838b7a5")}`,
      `--component-down-overlay:${fguiColorToCss(presentation.downOverlayColor || "#5210181d")}`
    );
    const icon = findResource(node.component?.iconResourceId);
    content = `${icon ? `<img class="component-icon" src="${resourceUrl(icon)}" alt="">` : ""}<span>${escapeHtml(node.component?.title || "Button")}</span>`;
  }
  return `<div class="canvas-node ${kind}${selected}${bound}" data-node-id="${escapeHtml(node.nodeId)}" style="${style.join(";")}">${content}${node.nodeId === state.primarySelectionId && state.selectedNodeIds.length === 1 ? '<span class="resize-handle" data-resize-handle="true"></span>' : ""}</div>`;
}

function fontFamilyStyle(font) {
  // Font is authoring text; never interpolate CSS delimiters into the style attribute.
  const safe = String(font || "MxUI Sans SC").replace(/[^\p{L}\p{N} _-]/gu, "");
  const fallback = safe.includes("Serif") ? "Source Serif 4 Semibold" : "Source Sans 3";
  const shared = safe.includes("Serif") ? "MxUI Serif SC" : "MxUI Sans SC";
  return `font-family:'${safe}','${shared}','${fallback}',sans-serif`;
}

function renderCompositePreview(recipe) {
  const width = Math.max(1, recipe.defaultSize?.width || 1), height = Math.max(1, recipe.defaultSize?.height || 1);
  const nodes = (recipe.template?.nodes || []).filter(node => node.visible !== false).sort((a, b) => a.order - b.order || a.nodeId.localeCompare(b.nodeId));
  return `<div class="composite-preview" style="--composite-width:${width};--composite-height:${height}">${nodes.map(renderCompositeChild).join("")}</div>`;
}

function renderCompositeChild(node) {
  const layout = node.layout || { x: 0, y: 0, width: 1, height: 1 };
  const style = [`left:${layout.x}px`, `top:${layout.y}px`, `width:${layout.width}px`, `height:${layout.height}px`];
  let content = "", kind = node.type.toLowerCase();
  if (node.type === "Graph") {
    style.push(`background:${fguiColorToCss(node.graph?.fillColor)}`, `border-radius:${node.graph?.cornerRadius || 0}px`);
  } else if (node.type === "Text") {
    style.push(`color:${fguiColorToCss(node.text?.color)}`, `font-size:${node.text?.fontSize || 16}px`, fontFamilyStyle(node.text?.font), `font-weight:${node.text?.bold ? 700 : 400}`, `justify-content:${alignToFlex(node.text?.align)}`, `text-align:${node.text?.align || "left"}`, "white-space:pre-line");
    content = escapeHtml(node.text?.value || "");
  } else if (node.type === "Image") {
    style.push(...semanticImageStyle(findResource(node.image?.resourceId), node.image?.tint));
  } else {
    kind = "component";
    const recipe = findRecipe(node.component?.componentRecipeId), presentation = recipe?.statePresentation || {};
    style.push(...semanticImageStyle(recipeBaseResource(recipe), "#ffffffff"), `color:${fguiColorToCss(presentation.titleColor || "#f3f5f4")}`, `font-size:${presentation.titleFontSize || 12}px`, fontFamilyStyle(presentation.titleFont), `font-weight:${presentation.titleBold === false ? 400 : 700}`);
    content = `<span>${escapeHtml(node.component?.title || "")}</span>`;
  }
  return `<div class="composite-child ${kind}" style="${style.join(";")}">${content}</div>`;
}

function renderCanvasContainer(container, deltaWidth, deltaHeight) {
  const origin = MxComposerLayout.containerAbsoluteOrigin(state.blueprint, container.containerId);
  const root = rootContainer(container);
  const adjusted = adjustedLayout({ x: origin.x, y: origin.y, width: container.layout.width, height: container.layout.height, horizontalAnchor: root.layout.horizontalAnchor, verticalAnchor: root.layout.verticalAnchor }, deltaWidth, deltaHeight);
  const selected = state.selectedContainerId === container.containerId ? " selected" : "";
  return `<div class="canvas-container${selected}" style="left:${adjusted.x}px;top:${adjusted.y}px;width:${adjusted.width}px;height:${adjusted.height}px"><span>${escapeHtml(container.name)} · ${escapeHtml(container.mode)}</span></div>`;
}

function renderInspector() {
  const node = selectedNode();
  const container = selectedContainer();
  const selection = selectedNodes();
  const controls = [el.xInput, el.yInput, el.widthInput, el.heightInput, el.horizontalAnchorSelect, el.verticalAnchorSelect, el.nameInput, el.graphColorInput, el.graphColorTextInput, el.cornerRadiusInput, el.textValueInput, el.fontFamilyInput, el.fontSizeInput, el.textAlignSelect, el.textColorInput, el.textColorTextInput, el.boldInput, el.singleLineInput, el.textInputToggle, el.textRestrictInput, el.textMaxLengthInput, el.imageResourceSelect, el.imageTintInput, el.imageTintTextInput, el.componentRecipeInput, el.componentTitleInput, el.componentIconSelect, el.listItemRecipeInput, el.listLayoutSelect, el.listSelectionModeSelect, el.listLineGapInput, el.listColumnGapInput, el.listVirtualizedInput, el.bindingSlotSelect];
  controls.forEach(control => control.disabled = !node && !container);
  el.deleteNodeButton.disabled = !selection.length || selection.some(item => isRequiredBinding(item.bindingSlot));
  el.moveNodeUpButton.disabled = !node;
  el.moveNodeDownButton.disabled = !node;
  el.groupSelectionButton.disabled = selection.length < 2;
  el.ungroupButton.disabled = !container;
  el.multiSelectionSummary.hidden = selection.length < 2;
  if (selection.length > 1) el.multiSelectionSummary.textContent = `已选择 ${selection.length} 个节点。尺寸与锚点会批量修改，名称、外观和绑定仅编辑主选择。`;
  el.containerFields.hidden = !container;
  if (container) {
    el.inspectorTitle.textContent = container.name;
    el.inspectorSubtitle.textContent = `${container.mode} · ${container.containerId}`;
    el.xInput.value = container.layout.x; el.yInput.value = container.layout.y; el.widthInput.value = container.layout.width; el.heightInput.value = container.layout.height;
    el.horizontalAnchorSelect.value = container.layout.horizontalAnchor; el.verticalAnchorSelect.value = container.layout.verticalAnchor; el.nameInput.value = container.name;
    el.containerModeSelect.value = container.mode; el.containerColumnsInput.value = container.columns; el.containerGapXInput.value = container.gapX; el.containerGapYInput.value = container.gapY;
    el.containerMainAlignSelect.value = container.mainAxisAlign; el.containerCrossAlignSelect.value = container.crossAxisAlign;
    el.paddingLeftInput.value = container.padding.left; el.paddingTopInput.value = container.padding.top; el.paddingRightInput.value = container.padding.right; el.paddingBottomInput.value = container.padding.bottom;
    el.graphFields.hidden = el.textFields.hidden = el.imageFields.hidden = el.componentFields.hidden = el.listFields.hidden = true;
    el.bindingSlotSelect.disabled = true;
    updateAnchorMatrix();
    return;
  }
  if (!node) {
    el.inspectorTitle.textContent = "未选择节点";
    el.inspectorSubtitle.textContent = "从层级或画布选择";
    el.graphFields.hidden = el.textFields.hidden = el.imageFields.hidden = el.componentFields.hidden = el.listFields.hidden = true;
    updateAnchorMatrix(false);
    return;
  }
  el.inspectorTitle.textContent = node.name;
  el.inspectorSubtitle.textContent = `${node.type} · ${node.nodeId}`;
  el.xInput.value = node.layout.x;
  el.yInput.value = node.layout.y;
  el.widthInput.value = node.layout.width;
  el.heightInput.value = node.layout.height;
  el.horizontalAnchorSelect.value = node.layout.horizontalAnchor;
  el.verticalAnchorSelect.value = node.layout.verticalAnchor;
  updateAnchorMatrix();
  el.nameInput.value = node.name;
  el.nameInput.disabled = Boolean(node.bindingSlot);
  el.graphFields.hidden = node.type !== "Graph";
  el.textFields.hidden = node.type !== "Text";
  el.imageFields.hidden = node.type !== "Image";
  el.componentFields.hidden = node.type !== "ComponentInstance";
  el.listFields.hidden = node.type !== "List";
  if (node.type === "Graph") {
    el.graphColorTextInput.value = node.graph.fillColor;
    el.graphColorInput.value = fguiColorToHtmlColor(node.graph.fillColor);
    el.cornerRadiusInput.value = node.graph.cornerRadius || 0;
  } else if (node.type === "Text") {
    el.textValueInput.value = node.text.value;
    el.fontSizeInput.value = node.text.fontSize;
    el.fontFamilyInput.value = node.text.font || "";
    el.textAlignSelect.value = node.text.align;
    el.textColorTextInput.value = node.text.color;
    el.textColorInput.value = fguiColorToHtmlColor(node.text.color);
    el.boldInput.checked = Boolean(node.text.bold);
    el.singleLineInput.checked = Boolean(node.text.singleLine);
    el.textInputToggle.checked = Boolean(node.text.input);
    el.textRestrictInput.value = node.text.restrict || "";
    el.textMaxLengthInput.value = node.text.maxLength || 0;
    el.textRestrictInput.disabled = !node.text.input;
    el.textMaxLengthInput.disabled = !node.text.input;
  } else if (node.type === "Image") {
    populateResourceSelect(el.imageResourceSelect, node.image.resourceId, false);
    el.imageTintTextInput.value = node.image.tint || "#ffffffff";
    el.imageTintInput.value = fguiColorToHtmlColor(node.image.tint || "#ffffffff");
    renderImageUsage(node);
  } else if (node.type === "ComponentInstance") {
    populateRecipeSelect(node.component.componentRecipeId);
    el.componentTitleInput.value = node.component.title;
    populateResourceSelect(el.componentIconSelect, node.component.iconResourceId, true, "Icon");
    renderComponentUsage(node);
  } else if (node.type === "List") {
    const list = node.list || {};
    populateListRecipeSelect(list.itemComponentRecipeId);
    el.listLayoutSelect.value = list.layout || "Column";
    el.listSelectionModeSelect.value = list.selectionMode || "None";
    el.listLineGapInput.value = list.lineGap || 0;
    el.listColumnGapInput.value = list.columnGap || 0;
    el.listVirtualizedInput.checked = list.virtualized === true;
    renderListItemUsage(node);
  }
  renderBindingSelect(node);
}

function renderBindingSelect(node) {
  const slots = state.serverState?.bindingSlots || [];
  const assigned = new Map((state.blueprint.nodes || []).filter(item => item.bindingSlot && item.nodeId !== node.nodeId).map(item => [item.bindingSlot, item.nodeId]));
  const currentRequired = isRequiredBinding(node.bindingSlot);
  const options = [`<option value="" ${currentRequired ? "disabled" : ""}>不绑定</option>`];
  slots.forEach(slot => {
    if (slot.nodeType !== node.type) return;
    const unavailable = assigned.has(slot.slotId);
    options.push(`<option value="${escapeHtml(slot.slotId)}" ${unavailable ? "disabled" : ""}>${escapeHtml(slot.slotId)} · ${escapeHtml(slot.controlName)}${slot.required ? " · 必需" : ""}</option>`);
  });
  el.bindingSlotSelect.innerHTML = options.join("");
  el.bindingSlotSelect.value = node.bindingSlot || "";
  const binding = slots.find(item => item.slotId === node.bindingSlot);
  el.bindingDetails.innerHTML = binding
    ? `<strong>${escapeHtml(binding.slotId)}</strong><br>控件名：${escapeHtml(binding.controlName)}<br>类型：${escapeHtml(binding.nodeType)}<br>绑定路径：${escapeHtml(binding.bindPath || "-")}<br>命令：${escapeHtml(binding.commandId || "只读文本绑定")}${binding.commandId ? `<br>反馈：${feedbackSummary(binding.feedback)}` : ""}`
    : "当前节点未连接运行时绑定槽。";
}

function feedbackSummary(feedback) {
  const policy = feedback || {};
  return `即时 ${escapeHtml(policy.activation || "None")} · 成功 ${escapeHtml(policy.success || "None")} · 失败 ${escapeHtml(policy.failure || "None")}`;
}

function renderBindings() {
  const slots = state.serverState?.bindingSlots || [];
  const nodes = state.blueprint?.nodes || [];
  el.bindingContractList.innerHTML = slots.map(slot => {
    const node = nodes.find(item => item.bindingSlot === slot.slotId);
    const feedback = slot.commandId ? `<small>${feedbackSummary(slot.feedback)}</small>` : "";
    return `<div class="binding-contract-item"><div><strong>${escapeHtml(slot.slotId)}</strong><small>${escapeHtml(slot.nodeType)} · ${escapeHtml(slot.commandId || slot.bindPath || "view model text")}</small>${feedback}</div><span class="${node ? "assigned" : "missing"}">${node ? escapeHtml(node.name) : "未分配"}</span></div>`;
  }).join("");
}

function renderDiagnostics() {
  const diagnostics = state.diagnostics || [];
  el.diagnosticCount.textContent = String(diagnostics.length);
  renderCanvasStatus();
  el.diagnosticsList.innerHTML = diagnostics.length
    ? diagnostics.map((item, index) => `<div class="diagnostic-row ${escapeHtml(item.severity || "info")}"><code>${escapeHtml(item.code || "-")}</code><span>${escapeHtml(item.message || "")}${item.path ? `<br><small>${escapeHtml(item.path)}</small>` : ""}</span><span class="diagnostic-actions"><button type="button" data-diagnostic-locate="${index}">定位</button>${diagnosticFixLabel(item) ? `<button type="button" data-diagnostic-fix="${index}">${diagnosticFixLabel(item)}</button>` : ""}</span></div>`).join("")
    : '<div class="empty-state">当前蓝图没有诊断。</div>';
  el.diagnosticsList.querySelectorAll("[data-diagnostic-locate]").forEach(button => button.addEventListener("click", () => locateDiagnostic(diagnostics[Number(button.dataset.diagnosticLocate)])));
  el.diagnosticsList.querySelectorAll("[data-diagnostic-fix]").forEach(button => button.addEventListener("click", () => fixDiagnostic(diagnostics[Number(button.dataset.diagnosticFix)])));
}

function diagnosticTarget(diagnostic) {
  const target = String(diagnostic?.nodeId || diagnostic?.objectId || diagnostic?.path || diagnostic?.location || "");
  const node = state.blueprint?.nodes?.find(item => target === item.nodeId || target.includes(item.nodeId) || target.includes(item.name));
  if (node) return { kind: "node", value: node };
  const recipe = recipes().find(item => target === item.componentId || target.includes(item.componentId));
  if (recipe) return { kind: "recipe", value: recipe };
  const resource = resources().find(item => target === item.resourceId || target.includes(item.resourceId));
  return resource ? { kind: "resource", value: resource } : null;
}

function locateDiagnostic(diagnostic) {
  const target = diagnosticTarget(diagnostic);
  if (!target) { showToast("该诊断指向文件或工作台整体，请查看路径并重新预检。", true); return; }
  if (target.kind === "node") { switchTabs("rail", "hierarchy"); selectNode(target.value.nodeId); focusSelection(); }
  else if (target.kind === "recipe") { switchTabs("rail", "recipes"); openRecipeEditor(target.value.componentId, true); }
  else { switchTabs("rail", "resources"); openResourceEditor(target.value.resourceId, true); }
}

function diagnosticFixLabel(diagnostic) {
  if (diagnostic?.code === "UICOMP007" && diagnosticTarget(diagnostic)?.kind === "node") return "修正尺寸";
  if (diagnostic?.code === "UICOMP212" && recipes().length) return "替换配方";
  if (diagnostic?.code === "UICOMP213") return "清空图标";
  if (diagnostic?.code === "UICOMP214" && resources().length) return "替换资源";
  return "";
}

function fixDiagnostic(diagnostic) {
  const target = diagnosticTarget(diagnostic);
  if (!target || target.kind !== "node") { locateDiagnostic(diagnostic); return; }
  commitMutation(() => {
    const node = target.value;
    if (diagnostic.code === "UICOMP007") { node.layout.width = Math.max(1, Number(node.layout.width) || 1); node.layout.height = Math.max(1, Number(node.layout.height) || 1); }
    else if (diagnostic.code === "UICOMP212") { if (node.list) node.list.itemComponentRecipeId = recipes()[0].componentId; else node.component.componentRecipeId = recipes()[0].componentId; }
    else if (diagnostic.code === "UICOMP213") node.component.iconResourceId = "";
    else if (diagnostic.code === "UICOMP214") node.image.resourceId = resources()[0].resourceId;
  });
  state.diagnostics = state.diagnostics.filter(item => item !== diagnostic);
  selectNode(target.value.nodeId);
  showToast("已应用可撤销修复，请重新统一预检。", false);
}

function updateDirtyUi() {
  const dirty = isDirty();
  const resourceDirty = isResourceDirty();
  const recipeDirty = isRecipeDirty();
  const anyDirty = dirty || resourceDirty || recipeDirty;
  el.layerDirtySummary.textContent = `资源${resourceDirty ? "未保存" : "干净"} · 配方${recipeDirty ? "未保存" : "干净"} · 蓝图${dirty ? "未保存" : "干净"}`;
  el.dirtyBadge.className = `status-badge ${anyDirty ? "dirty" : state.serverState?.errorCount ? "error" : "clean"}`;
  el.dirtyBadge.textContent = anyDirty ? "未保存" : state.serverState?.errorCount ? "有错误" : "干净";
  el.saveLayerButton.disabled = !anyDirty || state.busy;
  el.saveButton.disabled = !dirty || state.busy;
  el.applyButton.disabled = state.busy;
  el.syncButton.disabled = anyDirty || state.busy || !state.serverState?.sourceXmlHash;
  el.publishButton.disabled = anyDirty || state.busy || !state.serverState?.packageState;
  el.undoButton.disabled = state.history.length === 0;
  el.redoButton.disabled = state.future.length === 0;
  scheduleRecoveryDraft(anyDirty);
}

function recoveryStorageKey() { return `mx-ui-composer-recovery:${state.screenId}`; }

function readRecoveryDraft() {
  try {
    const value = JSON.parse(localStorage.getItem(recoveryStorageKey()) || "null");
    return value?.schemaVersion === "mx.uiComposer.recovery.v1" ? value : null;
  } catch { return null; }
}

function scheduleRecoveryDraft(anyDirty) {
  if (state.recoveryCandidate) return;
  if (state.recoveryTimer) clearTimeout(state.recoveryTimer);
  if (!anyDirty) { localStorage.removeItem(recoveryStorageKey()); state.recoveryTimer = null; return; }
  state.recoveryTimer = setTimeout(writeRecoveryDraft, 700);
}

function writeRecoveryDraft() {
  state.recoveryTimer = null;
  if (!hasAnyDirty()) return;
  localStorage.setItem(recoveryStorageKey(), JSON.stringify({
    schemaVersion: "mx.uiComposer.recovery.v1",
    savedAt: new Date().toISOString(),
    expected: currentAuthoringHashes(),
    resourceDraft: state.resourceDraft,
    recipeDraft: state.recipeDraft,
    blueprint: state.blueprint
  }));
}

function currentAuthoringHashes() {
  return {
    catalogHash: state.serverState?.catalogHash || "",
    recipeCatalogHash: state.serverState?.recipeCatalogHash || "",
    blueprintHash: state.serverState?.blueprintHash || "",
    contractHash: state.serverState?.contractHash || ""
  };
}

function presentRecoveryDraft() {
  const draft = state.recoveryCandidate;
  if (!draft) return;
  const current = currentAuthoringHashes();
  const compatible = Object.keys(current).every(key => (draft.expected?.[key] || "") === current[key]);
  if (!compatible) {
    localStorage.removeItem(recoveryStorageKey());
    state.recoveryCandidate = null;
    showToast("检测到旧恢复草稿，但源文件已变化，已避免覆盖。", true);
    return;
  }
  const time = draft.savedAt ? new Date(draft.savedAt).toLocaleString("zh-CN", { hour12: false }) : "未知时间";
  el.recoverySummary.textContent = `保存于 ${time}，与当前资源、配方、蓝图及契约版本一致。`;
  if (typeof el.recoveryDialog.showModal === "function") el.recoveryDialog.showModal(); else el.recoveryDialog.setAttribute("open", "open");
}

function restoreRecoveryDraft() {
  const draft = state.recoveryCandidate;
  if (!draft) return;
  state.resourceDraft = clone(draft.resourceDraft);
  state.recipeDraft = clone(draft.recipeDraft);
  state.blueprint = clone(draft.blueprint);
  state.recoveryCandidate = null;
  closeRecoveryDialog();
  repairSelection();
  renderAll();
  showToast("已恢复本地草稿，请检查后正式保存。", false);
}

function discardRecoveryDraft() {
  localStorage.removeItem(recoveryStorageKey());
  state.recoveryCandidate = null;
  closeRecoveryDialog();
  showToast("已丢弃本地恢复草稿。", false);
}

function closeRecoveryDialog() {
  if (typeof el.recoveryDialog.close === "function") el.recoveryDialog.close(); else el.recoveryDialog.removeAttribute("open");
}

function switchTabs(group, value) {
  document.querySelectorAll(`[data-${group}-tab]`).forEach(button => button.classList.toggle("active", button.dataset[`${group}Tab`] === value));
  document.querySelectorAll(`[data-${group}-panel]`).forEach(panel => panel.classList.toggle("active", panel.dataset[`${group}Panel`] === value));
  if (group === "rail" && window.innerWidth < 1280) document.body.classList.add("left-panel-open");
}

function setCanvasTool(value) {
  state.canvasTool = value === "pan" ? "pan" : "select";
  el.selectToolButton.classList.toggle("active", state.canvasTool === "select");
  el.panToolButton.classList.toggle("active", state.canvasTool === "pan");
  el.canvasViewport.classList.toggle("pan-ready", state.canvasTool === "pan");
}

function toggleResponsivePanel(value) {
  const left = value === "left";
  const className = left ? "left-panel-open" : "inspector-open";
  const next = !document.body.classList.contains(className);
  document.body.classList.remove("left-panel-open", "inspector-open");
  if (next) document.body.classList.add(className);
}

function applyAnchorPreset(horizontal, vertical) {
  if (!selectedNode() && !selectedContainer()) return;
  el.horizontalAnchorSelect.value = horizontal;
  el.verticalAnchorSelect.value = vertical;
  applyLayoutForm();
}

function updateAnchorMatrix(enabled = true) {
  if (!el.anchorMatrix) return;
  const horizontal = el.horizontalAnchorSelect.value;
  const vertical = el.verticalAnchorSelect.value;
  el.anchorMatrix.querySelectorAll("[data-anchor-h]").forEach(button => {
    button.disabled = !enabled;
    button.classList.toggle("active", enabled && button.dataset.anchorH === horizontal && button.dataset.anchorV === vertical);
  });
}

function selectNode(nodeId, event = {}) {
  const orderedIds = visibleHierarchyNodeIds();
  if (event.shiftKey && state.selectionAnchorId) {
    const start = orderedIds.indexOf(state.selectionAnchorId);
    const end = orderedIds.indexOf(nodeId);
    if (start >= 0 && end >= 0) state.selectedNodeIds = orderedIds.slice(Math.min(start, end), Math.max(start, end) + 1);
  } else if (event.ctrlKey || event.metaKey) {
    state.selectedNodeIds = state.selectedNodeIds.includes(nodeId)
      ? state.selectedNodeIds.filter(id => id !== nodeId)
      : [...state.selectedNodeIds, nodeId];
    state.selectionAnchorId = nodeId;
  } else {
    state.selectedNodeIds = [nodeId];
    state.selectionAnchorId = nodeId;
  }
  state.primarySelectionId = state.selectedNodeIds.includes(nodeId) ? nodeId : state.selectedNodeIds.at(-1) || "";
  state.selectedContainerId = "";
  renderHierarchy();
  renderCanvas();
  renderInspector();
}

function selectContainer(containerId) {
  state.selectedContainerId = containerId;
  state.selectedNodeIds = [];
  state.primarySelectionId = "";
  renderHierarchy(); renderCanvas(); renderInspector();
}

function selectedNode() { return state.blueprint?.nodes?.find(node => node.nodeId === state.primarySelectionId) || null; }
function selectedNodes() { const ids = new Set(state.selectedNodeIds); return (state.blueprint?.nodes || []).filter(node => ids.has(node.nodeId)); }
function selectedContainer() { return (state.blueprint?.containers || []).find(container => container.containerId === state.selectedContainerId) || null; }
function orderedNodes() { return [...(state.blueprint?.nodes || [])].sort((a, b) => a.order - b.order || a.nodeId.localeCompare(b.nodeId)); }
function directNodeDescendants(containerId) { return (state.blueprint?.nodes || []).filter(node => node.parentContainerId === containerId); }
function rootContainer(container) { const map = new Map((state.blueprint?.containers || []).map(item => [item.containerId, item])); let current = container; while (current?.parentContainerId && map.has(current.parentContainerId)) current = map.get(current.parentContainerId); return current || container; }
function visibleHierarchyNodeIds() { return [...el.hierarchyList.querySelectorAll("[data-node-id]")].map(row => row.dataset.nodeId); }
function collapsedStorageKey() { return `mx.uiComposer.collapsed.${state.screenId}`; }
function loadCollapsedContainers() { try { return new Set(JSON.parse(localStorage.getItem(collapsedStorageKey()) || "[]")); } catch { return new Set(); } }
function saveCollapsedContainers() { localStorage.setItem(collapsedStorageKey(), JSON.stringify([...state.collapsedContainers].sort())); }
function viewStorageKey() { return `mx-ui-composer-view:${state.screenId}`; }
function loadViewPreferences() { try { const value = JSON.parse(localStorage.getItem(viewStorageKey()) || "{}"); state.lockedNodeIds = new Set(value.locked || []); state.hiddenNodeIds = new Set(value.hidden || []); } catch { state.lockedNodeIds = new Set(); state.hiddenNodeIds = new Set(); } state.isolatedNodeIds = null; }
function saveViewPreferences() { localStorage.setItem(viewStorageKey(), JSON.stringify({ locked: [...state.lockedNodeIds].sort(), hidden: [...state.hiddenNodeIds].sort() })); }
function isNodeVisible(nodeId) { return !state.hiddenNodeIds.has(nodeId) && (!state.isolatedNodeIds || state.isolatedNodeIds.has(nodeId)); }
function selectedViewNodeIds() { return state.selectedContainerId ? descendantNodeIds(state.selectedContainerId) : [...state.selectedNodeIds]; }
function descendantNodeIds(containerId) { const containers = new Set([containerId]); let changed = true; while (changed) { changed = false; (state.blueprint.containers || []).forEach(item => { if (containers.has(item.parentContainerId) && !containers.has(item.containerId)) { containers.add(item.containerId); changed = true; } }); } return (state.blueprint.nodes || []).filter(node => containers.has(node.parentContainerId)).map(node => node.nodeId); }
function toggleIds(target, ids) { const remove = ids.length > 0 && ids.every(id => target.has(id)); ids.forEach(id => remove ? target.delete(id) : target.add(id)); }
function toggleSelectedLock() { const ids = selectedViewNodeIds(); if (!ids.length) { showToast("请先选择节点或容器。", true); return; } toggleIds(state.lockedNodeIds, ids); saveViewPreferences(); renderAll(); }
function toggleSelectedVisibility() { const ids = selectedViewNodeIds(); if (!ids.length) { showToast("请先选择节点或容器。", true); return; } toggleIds(state.hiddenNodeIds, ids); saveViewPreferences(); renderAll(); }
function toggleIsolation() { if (state.isolatedNodeIds) state.isolatedNodeIds = null; else { const ids = selectedViewNodeIds(); if (!ids.length) { showToast("请先选择节点或容器。", true); return; } state.isolatedNodeIds = new Set(ids); } renderAll(); }

function commitMutation(mutator) {
  if (!state.blueprint) return;
  state.history.push(stableWorkspaceSnapshot());
  if (state.history.length > MAX_HISTORY) state.history.shift();
  state.future = [];
  mutator();
  normalizeBlueprintSchemaVersion();
  normalizeOrders();
  renderAll();
}

function undo() {
  if (!state.history.length) return;
  state.future.push(stableWorkspaceSnapshot());
  restoreWorkspaceSnapshot(state.history.pop());
  repairSelection();
  renderAll();
}

function redo() {
  if (!state.future.length) return;
  state.history.push(stableWorkspaceSnapshot());
  restoreWorkspaceSnapshot(state.future.pop());
  repairSelection();
  renderAll();
}

function createNode(type, point = null, options = {}) {
  if (!state.blueprint) return;
  const index = nextNodeIndex(type);
  const origin = point || { x: 820, y: 480 };
  const base = {
    nodeId: `${type === "ComponentInstance" ? "button" : type.toLowerCase()}_${index}`,
    name: `${type === "ComponentInstance" ? "button" : type.toLowerCase()}${index}`,
    type,
    order: state.blueprint.nodes.length,
    bindingSlot: "",
    parentContainerId: state.selectedContainerId || "",
    layout: { x: snap(origin.x), y: snap(origin.y), width: type === "ComponentInstance" ? 320 : type === "Image" ? 256 : type === "List" ? 420 : 220, height: type === "Text" ? 44 : type === "Graph" ? 120 : type === "Image" ? 256 : type === "List" ? 320 : 72, horizontalAnchor: "Left", verticalAnchor: "Top" }
  };
  if (type === "Graph") base.graph = { fillColor: "#ff24443e", cornerRadius: 6 };
  else if (type === "Text") base.text = { value: "新文本", fontSize: 18, color: "#fff0f4f2", align: "left", bold: false, singleLine: true, input: false, restrict: "", maxLength: 0 };
  else if (type === "Image") base.image = { resourceId: options.resourceId || firstResourceId(), tint: "#ffffffff" };
  else if (type === "List") base.list = { itemComponentRecipeId: options.recipeId || firstRecipeId(), layout: "Column", lineGap: 8, columnGap: 8, selectionMode: "None", overflow: "Scroll", virtualized: false };
  else base.component = { componentRecipeId: options.recipeId || firstRecipeId(), title: "新按钮", iconResourceId: "" };
  commitMutation(() => { state.blueprint.nodes.push(base); state.selectedNodeIds = [base.nodeId]; state.primarySelectionId = base.nodeId; state.selectedContainerId = ""; });
}

function deleteSelectedNode() {
  const nodes = selectedNodes();
  if (nodes.some(node => state.lockedNodeIds.has(node.nodeId))) { showToast("选择中包含锁定节点，已取消删除。", true); return; }
  if (!nodes.length || nodes.some(node => isRequiredBinding(node.bindingSlot))) { if (nodes.length) showToast("选择中包含必需绑定节点，已取消整批删除。", true); return; }
  const ids = new Set(nodes.map(node => node.nodeId));
  commitMutation(() => {
    state.blueprint.nodes = state.blueprint.nodes.filter(item => !ids.has(item.nodeId));
    state.selectedNodeIds = []; state.primarySelectionId = "";
  });
}

function moveSelectedLayer(delta) {
  const node = selectedNode();
  if (!node) return;
  if (state.lockedNodeIds.has(node.nodeId)) { showToast("节点已锁定。", true); return; }
  const nodes = orderedNodes();
  const index = nodes.findIndex(item => item.nodeId === node.nodeId);
  const target = Math.max(0, Math.min(nodes.length - 1, index + delta));
  if (target === index) return;
  commitMutation(() => {
    nodes.splice(index, 1);
    nodes.splice(target, 0, node);
    state.blueprint.nodes = nodes;
  });
}

function reorderNode(sourceId, targetId) {
  if (!sourceId || sourceId === targetId) return;
  const nodes = orderedNodes();
  const source = nodes.findIndex(item => item.nodeId === sourceId);
  const target = nodes.findIndex(item => item.nodeId === targetId);
  if (source < 0 || target < 0) return;
  commitMutation(() => {
    const [item] = nodes.splice(source, 1);
    nodes.splice(target, 0, item);
    state.blueprint.nodes = nodes;
  });
}

function moveHierarchyItem(encoded, targetNodeId, targetContainerId) {
  if (!encoded) return;
  try {
    const source = JSON.parse(encoded);
    if (source.nodeId) {
      const node = state.blueprint.nodes.find(item => item.nodeId === source.nodeId);
      if (!node) return;
      const targetNode = state.blueprint.nodes.find(item => item.nodeId === targetNodeId);
      commitMutation(() => { node.parentContainerId = targetContainerId || targetNode?.parentContainerId || ""; if (targetNode) node.order = targetNode.order; });
    } else if (source.containerId) {
      const container = (state.blueprint.containers || []).find(item => item.containerId === source.containerId);
      if (!container || container.containerId === targetContainerId) return;
      const descendants = new Set();
      const visit = id => (state.blueprint.containers || []).filter(item => item.parentContainerId === id).forEach(item => { descendants.add(item.containerId); visit(item.containerId); });
      visit(container.containerId);
      if (descendants.has(targetContainerId)) { showToast("不能把容器移动到自己的子容器。", true); return; }
      commitMutation(() => { container.parentContainerId = targetContainerId || ""; });
    }
  } catch { showToast("无法识别层级拖放数据。", true); }
}

function normalizeOrders() { orderedNodes().forEach((node, index) => { node.order = index; }); }

function repairSelection() {
  const existing = new Set(state.blueprint?.nodes?.map(node => node.nodeId) || []);
  state.selectedNodeIds = state.selectedNodeIds.filter(id => existing.has(id));
  if (!existing.has(state.primarySelectionId)) state.primarySelectionId = state.selectedNodeIds.at(-1) || "";
  if (!(state.blueprint?.containers || []).some(item => item.containerId === state.selectedContainerId)) state.selectedContainerId = "";
}

function groupSelection(mode) {
  try {
    let created;
    commitMutation(() => { created = MxComposerLayout.createContainer(state.blueprint, state.selectedNodeIds, mode); state.selectedNodeIds = []; state.primarySelectionId = ""; state.selectedContainerId = created.containerId; });
  } catch (error) { showToast(error.message || String(error), true); }
}

function ungroupSelection() {
  const container = selectedContainer();
  if (!container) return;
  commitMutation(() => { state.selectedNodeIds = MxComposerLayout.ungroup(state.blueprint, container.containerId); state.primarySelectionId = state.selectedNodeIds[0] || ""; state.selectedContainerId = ""; });
}

function applyBatchLayout(action) {
  try {
    commitMutation(() => {
      if (["left", "hcenter", "right", "top", "vcenter", "bottom"].includes(action)) MxComposerLayout.align(state.blueprint, state.selectedNodeIds, action);
      else if (action === "distributeHorizontal") MxComposerLayout.distribute(state.blueprint, state.selectedNodeIds, "horizontal");
      else if (action === "distributeVertical") MxComposerLayout.distribute(state.blueprint, state.selectedNodeIds, "vertical");
      else if (action === "sameWidth") MxComposerLayout.matchSize(state.blueprint, state.selectedNodeIds, state.primarySelectionId, "width");
      else if (action === "sameHeight") MxComposerLayout.matchSize(state.blueprint, state.selectedNodeIds, state.primarySelectionId, "height");
      else if (action === "sameSize") MxComposerLayout.matchSize(state.blueprint, state.selectedNodeIds, state.primarySelectionId, "both");
      else if (action === "sameHorizontalAnchor") selectedNodes().forEach(node => { node.layout.horizontalAnchor = selectedNode().layout.horizontalAnchor; });
      else if (action === "sameVerticalAnchor") selectedNodes().forEach(node => { node.layout.verticalAnchor = selectedNode().layout.verticalAnchor; });
    });
  } catch (error) { showToast(error.message || String(error), true); }
}

function applyContainerForm() {
  const container = selectedContainer();
  if (!container) return;
  commitMutation(() => Object.assign(container, {
    mode: el.containerModeSelect.value, columns: Math.max(1, numberValue(el.containerColumnsInput, container.columns)),
    gapX: Math.max(0, numberValue(el.containerGapXInput, container.gapX)), gapY: Math.max(0, numberValue(el.containerGapYInput, container.gapY)),
    mainAxisAlign: el.containerMainAlignSelect.value, crossAxisAlign: el.containerCrossAlignSelect.value,
    padding: { left: Math.max(0, numberValue(el.paddingLeftInput, container.padding.left)), top: Math.max(0, numberValue(el.paddingTopInput, container.padding.top)), right: Math.max(0, numberValue(el.paddingRightInput, container.padding.right)), bottom: Math.max(0, numberValue(el.paddingBottomInput, container.padding.bottom)) }
  }));
}

function applyLayoutForm() {
  const node = selectedNode();
  const container = selectedContainer();
  if (!node && !container) return;
  commitMutation(() => {
    if (container) {
      Object.assign(container.layout, { x: numberValue(el.xInput, container.layout.x), y: numberValue(el.yInput, container.layout.y), width: Math.max(1, numberValue(el.widthInput, container.layout.width)), height: Math.max(1, numberValue(el.heightInput, container.layout.height)), horizontalAnchor: el.horizontalAnchorSelect.value, verticalAnchor: el.verticalAnchorSelect.value });
      return;
    }
    const width = Math.max(1, numberValue(el.widthInput, node.layout.width)), height = Math.max(1, numberValue(el.heightInput, node.layout.height));
    selectedNodes().forEach(item => Object.assign(item.layout, { width, height, horizontalAnchor: el.horizontalAnchorSelect.value, verticalAnchor: el.verticalAnchorSelect.value }));
    node.layout.x = numberValue(el.xInput, node.layout.x); node.layout.y = numberValue(el.yInput, node.layout.y);
  });
}

function applyNameForm() {
  const node = selectedNode();
  const container = selectedContainer();
  if (container) { const value = el.nameInput.value.trim(); if (value) commitMutation(() => { container.name = value; }); return; }
  if (!node || node.bindingSlot) return;
  const value = el.nameInput.value.trim();
  if (!value || state.blueprint.nodes.some(item => item.nodeId !== node.nodeId && item.name === value)) { showToast("控件名称不能为空或重复。", true); renderInspector(); return; }
  commitMutation(() => { node.name = value; });
}

function applyGraphForm(event) {
  const node = selectedNode();
  if (!node?.graph) return;
  commitMutation(() => {
    if (event.target === el.graphColorInput) node.graph.fillColor = htmlColorToFgui(el.graphColorInput.value, node.graph.fillColor);
    else if (event.target === el.graphColorTextInput) node.graph.fillColor = normalizeFguiColor(el.graphColorTextInput.value, node.graph.fillColor);
    node.graph.cornerRadius = Math.max(0, numberValue(el.cornerRadiusInput, node.graph.cornerRadius));
  });
}

function applyTextForm(event) {
  const node = selectedNode();
  if (!node?.text) return;
  commitMutation(() => {
    node.text.value = el.textValueInput.value;
    node.text.font = el.fontFamilyInput.value.trim();
    node.text.fontSize = Math.max(8, numberValue(el.fontSizeInput, node.text.fontSize));
    node.text.align = el.textAlignSelect.value;
    if (event.target === el.textColorInput) node.text.color = htmlColorToFgui(el.textColorInput.value, node.text.color);
    else if (event.target === el.textColorTextInput) node.text.color = normalizeFguiColor(el.textColorTextInput.value, node.text.color);
    node.text.bold = el.boldInput.checked;
    node.text.singleLine = el.singleLineInput.checked;
    node.text.input = el.textInputToggle.checked;
    node.text.restrict = node.text.input ? el.textRestrictInput.value : "";
    node.text.maxLength = node.text.input ? Math.max(0, numberValue(el.textMaxLengthInput, node.text.maxLength || 0)) : 0;
  });
}

function applyImageForm(event) {
  const node = selectedNode();
  if (!node?.image) return;
  commitMutation(() => {
    node.image.resourceId = el.imageResourceSelect.value;
    if (event.target === el.imageTintInput) node.image.tint = htmlColorToFgui(el.imageTintInput.value, node.image.tint);
    else if (event.target === el.imageTintTextInput) node.image.tint = normalizeFguiColor(el.imageTintTextInput.value, node.image.tint);
  });
}

function applyComponentForm() {
  const node = selectedNode();
  if (!node?.component) return;
  commitMutation(() => {
    node.component.componentRecipeId = el.componentRecipeInput.value;
    node.component.title = el.componentTitleInput.value;
    node.component.iconResourceId = el.componentIconSelect.value;
  });
}

function applyListForm() {
  const node = selectedNode();
  if (!node?.list) return;
  commitMutation(() => {
    node.list.itemComponentRecipeId = el.listItemRecipeInput.value;
    node.list.layout = el.listLayoutSelect.value;
    node.list.selectionMode = el.listSelectionModeSelect.value;
    node.list.lineGap = Math.max(0, numberValue(el.listLineGapInput, node.list.lineGap || 0));
    node.list.columnGap = Math.max(0, numberValue(el.listColumnGapInput, node.list.columnGap || 0));
    node.list.overflow = "Scroll";
    node.list.virtualized = el.listVirtualizedInput.checked;
  });
}

function applyBindingForm() {
  const node = selectedNode();
  if (!node) return;
  const slotId = el.bindingSlotSelect.value;
  const slot = (state.serverState.bindingSlots || []).find(item => item.slotId === slotId);
  if (slot && state.blueprint.nodes.some(item => item.nodeId !== node.nodeId && item.bindingSlot === slotId)) { showToast("该绑定槽已被其他节点占用。", true); renderInspector(); return; }
  commitMutation(() => {
    node.bindingSlot = slotId;
    if (slot) node.name = slot.controlName;
  });
}

function beginCanvasInteraction(event) {
  if (event.button === 1 || state.spacePressed || state.canvasTool === "pan") {
    event.preventDefault();
    state.drag = { mode: "pan", startX: event.clientX, startY: event.clientY, scrollLeft: el.canvasViewport.scrollLeft, scrollTop: el.canvasViewport.scrollTop };
    el.canvasViewport.classList.add("panning");
    return;
  }
  const target = event.target.closest(".canvas-node");
  if (!target) {
    if (event.target !== el.designCanvas) return;
    const point = canvasPoint(event.clientX, event.clientY);
    state.selectedContainerId = "";
    state.drag = { mode: "marquee", startX: point.x, startY: point.y, currentX: point.x, currentY: point.y };
    renderCanvas();
    return;
  }
  event.preventDefault();
  if (!state.selectedNodeIds.includes(target.dataset.nodeId) || event.ctrlKey || event.metaKey || event.shiftKey) selectNode(target.dataset.nodeId, event);
  const node = selectedNode();
  if (!node) return;
  if (!event.target.dataset.resizeHandle && isAutoLayoutChild(node)) { showToast("自动布局子节点的位置由容器控制，请调整容器或先解组。", true); return; }
  const movable = selectedNodes().filter(item => !isAutoLayoutChild(item) && !state.lockedNodeIds.has(item.nodeId));
  state.drag = {
    nodeId: node.nodeId,
    mode: event.target.dataset.resizeHandle ? "resize" : "move",
    startX: event.clientX,
    startY: event.clientY,
    layout: clone(node.layout),
    layouts: new Map(movable.map(item => [item.nodeId, clone(item.layout)])),
    blueprintSnapshot: stableWorkspaceSnapshot(),
    historyCommitted: false
  };
  target.setPointerCapture?.(event.pointerId);
}

function continueCanvasInteraction(event) {
  if (!state.drag) return;
  if (state.drag.mode === "pan") {
    el.canvasViewport.scrollLeft = state.drag.scrollLeft - (event.clientX - state.drag.startX);
    el.canvasViewport.scrollTop = state.drag.scrollTop - (event.clientY - state.drag.startY);
    return;
  }
  if (state.drag.mode === "marquee") {
    const point = canvasPoint(event.clientX, event.clientY); state.drag.currentX = point.x; state.drag.currentY = point.y; renderCanvas(); return;
  }
  const node = state.blueprint.nodes.find(item => item.nodeId === state.drag.nodeId);
  const scale = state.zoom / 100;
  const dx = (event.clientX - state.drag.startX) / scale;
  const dy = (event.clientY - state.drag.startY) / scale;
  let nextWidth = node.layout.width;
  let nextHeight = node.layout.height;
  let nextX = node.layout.x;
  let nextY = node.layout.y;
  if (state.drag.mode === "resize") {
    nextWidth = Math.max(16, snap(state.drag.layout.width + dx, event.altKey));
    nextHeight = Math.max(16, snap(state.drag.layout.height + dy, event.altKey));
  } else {
    nextX = snap(state.drag.layout.x + dx, event.altKey);
    nextY = snap(state.drag.layout.y + dy, event.altKey);
  }
  if (nextWidth === node.layout.width && nextHeight === node.layout.height
    && nextX === node.layout.x && nextY === node.layout.y) return;
  if (!state.drag.historyCommitted) {
    state.history.push(state.drag.blueprintSnapshot);
    if (state.history.length > MAX_HISTORY) state.history.shift();
    state.future = [];
    state.drag.historyCommitted = true;
  }
  if (state.drag.mode === "resize") { node.layout.width = nextWidth; node.layout.height = nextHeight; }
  else state.drag.layouts.forEach((layout, id) => { const item = state.blueprint.nodes.find(candidate => candidate.nodeId === id); item.layout.x = snap(layout.x + dx, event.altKey); item.layout.y = snap(layout.y + dy, event.altKey); });
  renderCanvas();
  renderInspector();
  updateDirtyUi();
}

function endCanvasInteraction() {
  if (!state.drag) return;
  if (state.drag.mode === "pan") { state.drag = null; el.canvasViewport.classList.remove("panning"); return; }
  if (state.drag.mode === "marquee") {
    const left = Math.min(state.drag.startX, state.drag.currentX), top = Math.min(state.drag.startY, state.drag.currentY), right = Math.max(state.drag.startX, state.drag.currentX), bottom = Math.max(state.drag.startY, state.drag.currentY);
    const materialized = MxComposerLayout.materialize(state.blueprint);
    state.selectedNodeIds = orderedNodes().filter(node => { const value = materialized.get(node.nodeId) || node.layout; return value.x < right && value.x + value.width > left && value.y < bottom && value.y + value.height > top; }).map(node => node.nodeId);
    state.primarySelectionId = state.selectedNodeIds.at(-1) || "";
  }
  state.drag = null;
  renderAll();
}

function isAutoLayoutChild(node) { const parent = (state.blueprint.containers || []).find(item => item.containerId === node.parentContainerId); return parent && parent.mode !== "Free"; }

function handleKeyboard(event) {
  if (event.target.matches("input, textarea, select")) return;
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") { event.preventDefault(); openCommandPalette(); return; }
  if (!event.ctrlKey && !event.metaKey && event.key.toLowerCase() === "v") { setCanvasTool("select"); return; }
  if (!event.ctrlKey && !event.metaKey && event.key.toLowerCase() === "h") { setCanvasTool("pan"); return; }
  if (event.code === "Space") { event.preventDefault(); state.spacePressed = true; el.canvasViewport.classList.add("pan-ready"); return; }
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "c") { event.preventDefault(); copySelection(); return; }
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "v") { event.preventDefault(); pasteSelection(); return; }
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "d") { event.preventDefault(); duplicateSelection(); return; }
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "z") { event.preventDefault(); event.shiftKey ? redo() : undo(); return; }
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "y") { event.preventDefault(); redo(); return; }
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "a") { event.preventDefault(); const parent = selectedNode()?.parentContainerId || selectedContainer()?.parentContainerId || ""; state.selectedNodeIds = orderedNodes().filter(node => (node.parentContainerId || "") === parent).map(node => node.nodeId); state.primarySelectionId = state.selectedNodeIds.at(-1) || ""; state.selectedContainerId = ""; renderAll(); return; }
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "g") { event.preventDefault(); event.shiftKey ? ungroupSelection() : groupSelection("Free"); return; }
  if (event.key.toLowerCase() === "f") { event.preventDefault(); focusSelection(); return; }
  if (event.key === "Home") { event.preventDefault(); fitCanvas(); return; }
  if (event.key === "Escape") {
    const openMenu = document.querySelector(".overflow-menu[open]");
    if (openMenu || document.body.classList.contains("left-panel-open") || document.body.classList.contains("inspector-open")) {
      openMenu?.removeAttribute("open");
      document.body.classList.remove("left-panel-open", "inspector-open");
      return;
    }
    state.selectedNodeIds = []; state.primarySelectionId = ""; state.selectedContainerId = ""; renderAll(); return;
  }
  const directions = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] };
  if (directions[event.key]) {
    const nodes = selectedNodes().filter(node => !isAutoLayoutChild(node) && !state.lockedNodeIds.has(node.nodeId));
    if (!nodes.length) return;
    event.preventDefault();
    const amount = event.shiftKey ? 10 : 1;
    const [x, y] = directions[event.key];
    commitMutation(() => nodes.forEach(node => { node.layout.x += x * amount; node.layout.y += y * amount; }));
  } else if (event.key === "Delete") deleteSelectedNode();
}

function handleCanvasWheel(event) {
  if (!event.ctrlKey && !event.metaKey) return;
  event.preventDefault();
  state.zoom = Math.max(25, Math.min(100, state.zoom + (event.deltaY < 0 ? 5 : -5)));
  el.zoomRange.value = String(state.zoom);
  renderCanvas();
}

function openResolutionCompare() {
  const resolutions = [[1280, 720], [1920, 1080], [2560, 1440], [3440, 1440]];
  const materialized = MxComposerLayout.materialize(state.blueprint);
  el.resolutionCompareGrid.innerHTML = resolutions.map(([width, height]) => {
    const logical = logicalPreviewSizeFor({ width, height });
    const deltaWidth = logical.width - state.blueprint.designResolution.width;
    const deltaHeight = logical.height - state.blueprint.designResolution.height;
    const scale = Math.min(420 / logical.width, 236 / logical.height);
    const nodes = orderedNodes().filter(node => isNodeVisible(node.nodeId)).map(node => renderCanvasNode(node, materialized.get(node.nodeId) || node.layout, deltaWidth, deltaHeight)).join("");
    return `<section class="resolution-preview"><header><strong>${width}×${height}</strong><span>${logical.width}×${logical.height} 逻辑画布</span></header><div class="resolution-preview-viewport"><div class="resolution-preview-canvas" style="width:${logical.width}px;height:${logical.height}px;transform:scale(${scale})">${nodes}</div></div></section>`;
  }).join("");
  if (typeof el.resolutionCompareDialog.showModal === "function") el.resolutionCompareDialog.showModal(); else el.resolutionCompareDialog.setAttribute("open", "open");
}

function closeResolutionCompare() {
  if (typeof el.resolutionCompareDialog.close === "function") el.resolutionCompareDialog.close(); else el.resolutionCompareDialog.removeAttribute("open");
}

function openReplaceReferences(kind, sourceId, count) {
  const options = kind === "resource" ? resources().filter(item => item.resourceId !== sourceId) : recipes().filter(item => item.componentId !== sourceId);
  if (!options.length) { showToast("没有可用的替换目标。", true); return; }
  state.replaceReferences = { kind, sourceId };
  el.replaceReferencesSummary.textContent = `${sourceId} 当前有 ${count} 处引用。替换操作可撤销。`;
  el.replaceReferencesSelect.innerHTML = options.map(item => `<option value="${escapeHtml(kind === "resource" ? item.resourceId : item.componentId)}">${escapeHtml(item.displayName || item.resourceId || item.componentId)}</option>`).join("");
  if (typeof el.replaceReferencesDialog.showModal === "function") el.replaceReferencesDialog.showModal(); else el.replaceReferencesDialog.setAttribute("open", "open");
}

function confirmReplaceReferences() {
  const operation = state.replaceReferences;
  if (!operation) return;
  const targetId = el.replaceReferencesSelect.value;
  commitMutation(() => {
    if (operation.kind === "recipe") state.blueprint.nodes.forEach(node => { if (node.component?.componentRecipeId === operation.sourceId) node.component.componentRecipeId = targetId; });
    else {
      state.blueprint.nodes.forEach(node => { if (node.image?.resourceId === operation.sourceId) node.image.resourceId = targetId; if (node.component?.iconResourceId === operation.sourceId) node.component.iconResourceId = targetId; });
      recipes().forEach(recipe => (recipe.resourceSlots || []).forEach(slot => { if (slot.resourceId === operation.sourceId) slot.resourceId = targetId; }));
    }
  });
  closeReplaceReferences();
  showToast("已批量替换当前工作台引用，请统一预检。", false);
}

function closeReplaceReferences() {
  state.replaceReferences = null;
  if (typeof el.replaceReferencesDialog.close === "function") el.replaceReferencesDialog.close(); else el.replaceReferencesDialog.removeAttribute("open");
}

function libraryPreferencesKey() { return "mx-ui-composer-library-preferences"; }
function loadLibraryPreferences() { try { const value = JSON.parse(localStorage.getItem(libraryPreferencesKey()) || "{}"); state.favoriteKeys = new Set(value.favorites || []); state.recentKeys = value.recents || []; } catch { state.favoriteKeys = new Set(); state.recentKeys = []; } }
function saveLibraryPreferences() { localStorage.setItem(libraryPreferencesKey(), JSON.stringify({ favorites: [...state.favoriteKeys].sort(), recents: state.recentKeys.slice(0, 20) })); }
function canvasPreferencesKey() { return "mx-ui-composer-canvas-preferences"; }
function loadCanvasPreferences() {
  try {
    const value = JSON.parse(localStorage.getItem(canvasPreferencesKey()) || "{}");
    state.gridVisible = value.gridVisible ?? true;
    state.snapEnabled = value.snapEnabled ?? true;
    state.gridSize = [4, 8, 16].includes(value.gridSize) ? value.gridSize : 8;
  } catch {
    state.gridVisible = true;
    state.snapEnabled = true;
    state.gridSize = 8;
  }
}
function saveCanvasPreferences() { localStorage.setItem(canvasPreferencesKey(), JSON.stringify({ gridVisible: state.gridVisible, snapEnabled: state.snapEnabled, gridSize: state.gridSize })); }
function rememberLibraryItem(key) { state.recentKeys = [key, ...state.recentKeys.filter(item => item !== key)].slice(0, 20); saveLibraryPreferences(); }
function sortLibraryItems(values, kind) { const recent = new Map(state.recentKeys.map((key, index) => [key, index])); return [...values].sort((a, b) => { const aId = kind === "resource" ? a.resourceId : a.componentId, bId = kind === "resource" ? b.resourceId : b.componentId; const aKey = `${kind}:${aId}`, bKey = `${kind}:${bId}`; return Number(state.favoriteKeys.has(bKey)) - Number(state.favoriteKeys.has(aKey)) || (recent.get(aKey) ?? 999) - (recent.get(bKey) ?? 999) || aId.localeCompare(bId); }); }
function toggleCurrentFavorite() { const key = state.selectedResourceId ? `resource:${state.selectedResourceId}` : state.selectedRecipeId ? `recipe:${state.selectedRecipeId}` : ""; if (!key) return; if (state.favoriteKeys.has(key)) state.favoriteKeys.delete(key); else state.favoriteKeys.add(key); saveLibraryPreferences(); updateFavoriteButton(); renderResourceList(); renderRecipeList(); }
function updateFavoriteButton() { const key = state.selectedResourceId ? `resource:${state.selectedResourceId}` : state.selectedRecipeId ? `recipe:${state.selectedRecipeId}` : ""; const active = key && state.favoriteKeys.has(key); el.favoriteContextButton.textContent = active ? "★" : "☆"; el.favoriteContextButton.classList.toggle("active", Boolean(active)); }

const composerCommands = [
  { id: "validate", label: "统一预检", hint: "检查资源、配方、蓝图和 hash", run: validateBlueprint },
  { id: "save", label: "保存当前层", hint: "保存当前未保存草稿", run: saveCurrentLayer },
  { id: "fit", label: "适应全部", hint: "Home", run: fitCanvas },
  { id: "focus", label: "聚焦选择", hint: "F", run: focusSelection },
  { id: "compare", label: "多分辨率对比", hint: "四种桌面分辨率", run: openResolutionCompare },
  { id: "template-buttons", label: "插入三按钮操作组", hint: "常用模板", run: () => createTemplate("button-stack") }
];
function openCommandPalette() { renderCommandPalette(); if (typeof el.commandPaletteDialog.showModal === "function") el.commandPaletteDialog.showModal(); else el.commandPaletteDialog.setAttribute("open", "open"); requestAnimationFrame(() => el.commandSearchInput.focus()); }
function closeCommandPalette() { el.commandSearchInput.value = ""; if (typeof el.commandPaletteDialog.close === "function") el.commandPaletteDialog.close(); else el.commandPaletteDialog.removeAttribute("open"); }
function renderCommandPalette() { const query = (el.commandSearchInput?.value || "").trim().toLocaleLowerCase(); const commands = composerCommands.filter(item => !query || `${item.label} ${item.hint}`.toLocaleLowerCase().includes(query)); el.commandList.innerHTML = commands.map(item => `<button type="button" data-command="${item.id}"><strong>${item.label}</strong><span>${item.hint}</span></button>`).join("") || '<div class="empty-state">没有匹配命令。</div>'; el.commandList.querySelectorAll("[data-command]").forEach(button => button.addEventListener("click", () => { const command = composerCommands.find(item => item.id === button.dataset.command); closeCommandPalette(); setTimeout(() => command?.run(), 0); })); }

function createTemplate(templateId) {
  if (templateId !== "button-stack") return;
  const recipeId = findRecipe("ui.component.button.command.neutral")?.componentId || firstRecipeId();
  const start = nextNodeIndex("ComponentInstance");
  const nodes = [0, 1, 2].map(index => ({ nodeId: `button_${start + index}`, name: `button${start + index}`, type: "ComponentInstance", order: state.blueprint.nodes.length + index, bindingSlot: "", parentContainerId: "", layout: { x: 800, y: 390 + index * 88, width: 320, height: 72, horizontalAnchor: "Center", verticalAnchor: "Middle" }, component: { componentRecipeId: recipeId, title: `操作 ${index + 1}`, iconResourceId: "" } }));
  commitMutation(() => { state.blueprint.nodes.push(...nodes); state.selectedNodeIds = nodes.map(node => node.nodeId); state.primarySelectionId = nodes.at(-1).nodeId; });
  showToast("已插入三按钮操作组，可继续分组或绑定命令。", false);
}

function snapshotStorageKey() { return `mx-ui-composer-snapshots:${state.screenId}`; }
function readSnapshots() { try { return JSON.parse(localStorage.getItem(snapshotStorageKey()) || "[]"); } catch { return []; } }
function writeSnapshots(values) { localStorage.setItem(snapshotStorageKey(), JSON.stringify(values.slice(0, 10))); }
function createLocalSnapshot() { const values = readSnapshots(); values.unshift({ id: `${Date.now()}`, createdAt: new Date().toISOString(), workspace: stableWorkspaceSnapshot() }); writeSnapshots(values); renderReviewDialog(); showToast("已创建本地工作台快照。", false); }
function restoreLocalSnapshot(id) { const snapshot = readSnapshots().find(item => item.id === id); if (!snapshot) return; state.history.push(stableWorkspaceSnapshot()); restoreWorkspaceSnapshot(snapshot.workspace); repairSelection(); closeReviewDialog(); renderAll(); showToast("已恢复快照，当前内容仍未保存。", false); }
function deleteLocalSnapshot(id) { writeSnapshots(readSnapshots().filter(item => item.id !== id)); renderReviewDialog(); }

function openReviewDialog() { renderReviewDialog(); if (typeof el.reviewDialog.showModal === "function") el.reviewDialog.showModal(); else el.reviewDialog.setAttribute("open", "open"); }
function closeReviewDialog() { if (typeof el.reviewDialog.close === "function") el.reviewDialog.close(); else el.reviewDialog.removeAttribute("open"); }
function renderReviewDialog() {
  const baselineBlueprint = JSON.parse(state.baseline || "{}");
  const baselineResources = JSON.parse(state.resourceBaseline || "{}");
  const baselineRecipes = JSON.parse(state.recipeBaseline || "{}");
  const groups = [
    diffCollection("资源", baselineResources.resources || [], resources(), item => item.resourceId),
    diffCollection("配方", baselineRecipes.recipes || [], recipes(), item => item.componentId),
    diffCollection("节点", baselineBlueprint.nodes || [], state.blueprint.nodes || [], item => item.nodeId),
    diffCollection("容器", baselineBlueprint.containers || [], state.blueprint.containers || [], item => item.containerId)
  ];
  el.reviewSummary.innerHTML = groups.map(group => `<section class="review-group"><header><strong>${group.label}</strong><span>新增 ${group.added.length} · 修改 ${group.changed.length} · 删除 ${group.removed.length}</span></header><div>${[...group.added.map(id => `＋ ${id}`), ...group.changed.map(id => `~ ${id}`), ...group.removed.map(id => `－ ${id}`)].map(value => `<code>${escapeHtml(value)}</code>`).join("") || '<span class="empty-state">无变化</span>'}</div></section>`).join("");
  const snapshots = readSnapshots();
  el.snapshotList.innerHTML = snapshots.map(item => `<div class="snapshot-row"><span><strong>${new Date(item.createdAt).toLocaleString("zh-CN", { hour12: false })}</strong><small>${item.id}</small></span><span><button type="button" data-restore-snapshot="${item.id}">恢复</button><button type="button" data-delete-snapshot="${item.id}">删除</button></span></div>`).join("") || '<div class="empty-state">尚未创建本地快照。</div>';
  el.snapshotList.querySelectorAll("[data-restore-snapshot]").forEach(button => button.addEventListener("click", () => restoreLocalSnapshot(button.dataset.restoreSnapshot)));
  el.snapshotList.querySelectorAll("[data-delete-snapshot]").forEach(button => button.addEventListener("click", () => deleteLocalSnapshot(button.dataset.deleteSnapshot)));
  el.toggleReviewModeButton.textContent = document.body.classList.contains("review-mode") ? "退出只读评审" : "进入只读评审";
}
function diffCollection(label, before, after, getId) { const beforeMap = new Map(before.map(item => [getId(item), JSON.stringify(item)])); const afterMap = new Map(after.map(item => [getId(item), JSON.stringify(item)])); return { label, added: [...afterMap.keys()].filter(id => !beforeMap.has(id)), removed: [...beforeMap.keys()].filter(id => !afterMap.has(id)), changed: [...afterMap.keys()].filter(id => beforeMap.has(id) && beforeMap.get(id) !== afterMap.get(id)) }; }
function toggleReviewMode() { document.body.classList.toggle("review-mode"); renderReviewDialog(); }

function fitCanvas() {
  if (!state.blueprint) return;
  const logical = logicalPreviewSize();
  const width = Math.max(1, el.canvasViewport.clientWidth - 68);
  const height = Math.max(1, el.canvasViewport.clientHeight - 68);
  state.zoom = Math.max(25, Math.min(100, Math.floor(Math.min(width / logical.width, height / logical.height) * 100)));
  el.zoomRange.value = String(state.zoom);
  renderCanvas();
  requestAnimationFrame(() => {
    el.canvasViewport.scrollLeft = Math.max(0, (el.physicalFrame.offsetWidth - el.canvasViewport.clientWidth) / 2 + 34);
    el.canvasViewport.scrollTop = Math.max(0, (el.physicalFrame.offsetHeight - el.canvasViewport.clientHeight) / 2 + 34);
  });
}

function focusSelection() {
  const ids = selectedViewNodeIds();
  if (!ids.length) { showToast("请先选择要聚焦的节点或容器。", true); return; }
  const materialized = MxComposerLayout.materialize(state.blueprint);
  const layouts = ids.map(id => materialized.get(id) || state.blueprint.nodes.find(node => node.nodeId === id)?.layout).filter(Boolean);
  if (!layouts.length) return;
  const left = Math.min(...layouts.map(value => value.x));
  const top = Math.min(...layouts.map(value => value.y));
  const right = Math.max(...layouts.map(value => value.x + value.width));
  const bottom = Math.max(...layouts.map(value => value.y + value.height));
  const width = Math.max(1, el.canvasViewport.clientWidth - 120);
  const height = Math.max(1, el.canvasViewport.clientHeight - 120);
  state.zoom = Math.max(25, Math.min(100, Math.floor(Math.min(width / Math.max(1, right - left), height / Math.max(1, bottom - top)) * 100)));
  el.zoomRange.value = String(state.zoom);
  renderCanvas();
  requestAnimationFrame(() => {
    const scale = state.zoom / 100;
    el.canvasViewport.scrollLeft = Math.max(0, ((left + right) / 2) * scale - el.canvasViewport.clientWidth / 2 + 34);
    el.canvasViewport.scrollTop = Math.max(0, ((top + bottom) / 2) * scale - el.canvasViewport.clientHeight / 2 + 34);
  });
}

function copySelection() {
  const payload = MxComposerClipboard.capture(state.blueprint, state.selectedNodeIds, state.selectedContainerId);
  if (!payload.nodes.length && !payload.containers.length) { showToast("请先选择节点或容器。", true); return; }
  state.clipboard = payload;
  showToast(`已复制 ${payload.nodes.length} 个节点${payload.containers.length ? `、${payload.containers.length} 个容器` : ""}。`, false);
}

function pasteSelection() {
  if (!state.clipboard) { showToast("当前会话剪贴板为空。", true); return; }
  let result;
  commitMutation(() => { result = MxComposerClipboard.paste(state.blueprint, state.clipboard, 16); });
  selectPastedResult(result);
}

function duplicateSelection() {
  const payload = MxComposerClipboard.capture(state.blueprint, state.selectedNodeIds, state.selectedContainerId);
  if (!payload.nodes.length && !payload.containers.length) { showToast("请先选择节点或容器。", true); return; }
  state.clipboard = payload;
  let result;
  commitMutation(() => { result = MxComposerClipboard.paste(state.blueprint, payload, 16); });
  selectPastedResult(result);
}

function selectPastedResult(result) {
  state.selectedContainerId = result.containerId || "";
  state.selectedNodeIds = result.containerId ? [] : result.nodeIds;
  state.primarySelectionId = state.selectedNodeIds.at(-1) || "";
  renderAll();
}

async function validateBlueprint() {
  setBusy(true, "正在统一预检资源、配方与蓝图...");
  try {
    const response = await postJson(`${API_ROOT}/plan-workspace`, workspacePayload());
    state.diagnostics = response.data.plan?.diagnostics || [];
    renderDiagnostics();
    showPlan("统一预检", "按资源、配方、节点和 hash 顺序检查。", response.data.plan, null);
  } finally { setBusy(false); }
}

async function saveCurrentLayer() {
  try {
    if (isResourceDirty()) { await saveResourceDraft(); return; }
    if (isRecipeDirty()) { await saveRecipeDraft(); return; }
    if (isDirty()) await saveBlueprint();
  } catch (error) { showToast(error.message || String(error), true); }
}

async function saveResourceDraft() {
  const response = await postJson("/api/authoring/ui-art/resources/save", { library: state.resourceDraft.libraryId, expectedCatalogHash: state.serverState.catalogHash, catalog: state.resourceDraft });
  if (!response.ok) { showPlan("资源保存被阻断", "刷新或修正资源诊断后重试。", response.data.plan, null); throw new Error("资源保存被阻断"); }
  await loadScreenState();
  showToast("资源草稿已保存。", false);
}

async function saveRecipeDraft() {
  const response = await postJson(`${API_ROOT}/save-recipes`, { screen: state.screenId, expectedRecipeCatalogHash: state.serverState.recipeCatalogHash, recipes: state.recipeDraft });
  if (!response.ok) { showPlan("配方保存被阻断", "修正按钮配方后重试。", response.data.plan, null); throw new Error("配方保存被阻断"); }
  await loadScreenState();
  showToast("配方草稿已保存。", false);
}

async function saveBlueprint() {
  setBusy(true, "正在保存蓝图...");
  try {
    const response = await postJson(`${API_ROOT}/save`, { screen: state.screenId, expectedBlueprintHash: state.serverState.blueprintHash, expectedContractHash: state.serverState.contractHash, blueprint: state.blueprint });
    if (!response.ok) { showPlan("保存被阻断", "处理诊断后重试。", response.data.plan, null); return; }
    applyServerState(response.data.state);
    showToast("蓝图已保存，FairyGUI 源文件尚未修改。", false);
  } finally { setBusy(false); }
}

async function planApply() {
  setBusy(true, "正在生成统一应用计划...");
  try {
    const response = await postJson(`${API_ROOT}/plan-workspace`, workspacePayload());
    showPlan("应用并发布", `先保存三层草稿，再修改 ${state.serverState.screen.packageId} 并发布。`, response.data.plan, response.ok ? executeWorkspaceApply : null);
  } finally { setBusy(false); }
}

async function executeWorkspaceApply() {
  setBusy(true, "正在保存工作台并排队 FairyGUI Apply...");
  try {
    const response = await postJson(`${API_ROOT}/apply-workspace`, { ...workspacePayload(), launchEditor: true });
    if (!response.ok) { showPlan("应用被阻断", "刷新或修正三层诊断后重试。", response.data.plan, null); return; }
    await pollOperation(response.data.operation.operationId);
    await executePublish();
  } finally { setBusy(false); }
}

async function executeApply() {
  setBusy(true, "正在排队 FairyGUI Apply 操作...");
  try {
    const response = await postJson(`${API_ROOT}/apply`, { ...operationPayload(), launchEditor: true });
    if (!response.ok) { showPlan("Apply 被阻断", "刷新后重新生成计划。", response.data.plan, null); return; }
    showToast(response.data.launch?.started ? "已启动 FairyGUI Editor，正在应用。" : "操作已排队，请保持 FairyGUI Editor 打开。", false);
    await pollOperation(response.data.operation.operationId);
  } finally { setBusy(false); }
}

async function planSync() {
  if (hasAnyDirty()) { showToast("请先保存或撤销当前工作台改动，再从 FairyGUI 同步。", true); return; }
  setBusy(true, "正在比较 FairyGUI 源文件...");
  try {
    const response = await postJson(`${API_ROOT}/plan-sync`, operationPayload());
    showPlan("从 FairyGUI 同步", "只回读 V1 支持的布局和视觉字段。", response.data.plan, response.ok ? executeSync : null);
  } finally { setBusy(false); }
}

async function executeSync() {
  setBusy(true, "正在同步蓝图...");
  try {
    const response = await postJson(`${API_ROOT}/sync`, operationPayload());
    if (!response.ok) { showPlan("同步被阻断", "未知 FairyGUI 属性不会被静默删除。", response.data.plan, null); return; }
    applyServerState(response.data.state);
    showToast("FairyGUI 支持字段已同步回蓝图。", false);
  } finally { setBusy(false); }
}

function confirmPublish() {
  const plan = {
    operation: "PublishFairyGui",
    blocked: false,
    wouldPublish: true,
    targetPaths: [state.serverState?.bindingContract?.target?.publishedPackagePath || "FairyGUI 发布目标"],
    diagnostics: [],
    diffs: []
  };
  showPlan("发布 FairyGUI 包", "发布使用 genCode=false，不改 generated binder。", plan, executePublish);
}

async function executePublish() {
  setBusy(true, "正在排队 FairyGUI 发布操作...");
  try {
    const response = await postJson(`${API_ROOT}/publish`, { ...operationPayload(), launchEditor: true });
    if (!response.ok) { showPlan("发布被阻断", "先完成 Apply 或同步。", response.data.plan, null); return; }
    showToast(response.data.launch?.started ? "已启动 FairyGUI Editor，正在发布。" : "发布已排队。", false);
    await pollOperation(response.data.operation.operationId);
  } finally { setBusy(false); }
}

async function pollOperation(operationId) {
  const deadline = Date.now() + 120000;
  while (Date.now() < deadline) {
    const status = await getJson(`${API_ROOT}/operation-status?operationId=${encodeURIComponent(operationId)}`);
    setStatus(status.progress?.details ? `${status.status} · ${status.progress.details}` : `FairyGUI 操作：${status.status}`);
    if (status.status === "Succeeded") {
      showToast(status.result?.message || "FairyGUI 操作完成。", false);
      await loadScreenState();
      return;
    }
    if (status.status === "Failed") throw new Error(status.result?.message || "FairyGUI 操作失败。");
    await delay(800);
  }
  throw new Error("等待 FairyGUI 操作超时。请检查编辑器和 .agent 结果。 ");
}

function showPlan(title, subtitle, plan, confirm) {
  const value = plan || { blocked: true, diagnostics: [{ code: "UICOMP", severity: "error", message: "没有返回操作计划。" }] };
  el.planDialogTitle.textContent = title;
  el.planDialogSubtitle.textContent = subtitle;
  el.planSummary.innerHTML = `<div class="plan-card"><strong>${escapeHtml(value.operation || title)}</strong>蓝图：${value.wouldModifyBlueprint ? "修改" : "不修改"} · FairyGUI 源：${value.wouldModifyFairyGuiSource ? "修改" : "不修改"} · 发布：${value.wouldPublish ? "是" : "否"}</div>${(value.targetPaths || []).map(path => `<div class="plan-card"><strong>目标</strong>${escapeHtml(path)}</div>`).join("")}`;
  el.planDiagnostics.innerHTML = (value.diagnostics || []).map(item => `<div class="plan-card"><strong>${escapeHtml(item.code || item.severity || "诊断")}</strong>${escapeHtml(item.message || "")}</div>`).join("");
  el.planDiffs.innerHTML = (value.diffs || []).map(item => `<div class="plan-card"><strong>${escapeHtml(item.change)} · ${escapeHtml(item.nodeId)}</strong>${escapeHtml(item.before || "-")} → ${escapeHtml(item.after || "-")}</div>`).join("");
  state.planConfirm = value.blocked ? null : confirm;
  el.confirmPlanButton.disabled = !state.planConfirm;
  el.confirmPlanButton.textContent = value.blocked ? "已阻断" : "确认";
  if (typeof el.planDialog.showModal === "function") el.planDialog.showModal(); else el.planDialog.setAttribute("open", "open");
}

function closePlanDialog() {
  state.planConfirm = null;
  if (typeof el.planDialog.close === "function") el.planDialog.close(); else el.planDialog.removeAttribute("open");
}

function applyServerState(next) {
  state.serverState = next;
  state.blueprint = clone(next.blueprint);
  state.baseline = stableBlueprint(state.blueprint);
  state.diagnostics = next.diagnostics || [];
  state.history = [];
  state.future = [];
  localStorage.removeItem(recoveryStorageKey());
  repairSelection();
  if (!selectedNode() && !selectedContainer()) { state.selectedNodeIds = state.blueprint.nodes?.length ? [state.blueprint.nodes[0].nodeId] : []; state.primarySelectionId = state.selectedNodeIds[0] || ""; }
  renderAll();
}

function operationPayload() {
  return {
    screen: state.screenId,
    expected: {
      blueprintHash: state.serverState.blueprintHash,
      recipeCatalogHash: state.serverState.recipeCatalogHash,
      catalogHash: state.serverState.catalogHash,
      contractHash: state.serverState.contractHash,
      sourceXmlHash: state.serverState.sourceXmlHash,
      mappingHash: state.serverState.mappingHash
    }
  };
}

function workspacePayload() {
  return { ...operationPayload(), resources: state.resourceDraft, recipes: state.recipeDraft, blueprint: state.blueprint };
}

function setBusy(value, message = "") {
  state.busy = value;
  if (message) setStatus(message);
  [el.screenSelect, el.validateButton, el.saveLayerButton, el.saveButton, el.syncButton, el.applyButton, el.publishButton].forEach(control => control.disabled = value);
  updateDirtyUi();
}

function setStatus(message, tone = "normal") {
  el.statusText.textContent = message;
  el.statusText.style.color = tone === "error" ? "var(--danger)" : "";
}

function showToast(message, error) {
  const toast = document.createElement("div");
  toast.className = `toast${error ? " error" : ""}`;
  toast.textContent = message;
  el.toastRegion.appendChild(toast);
  setTimeout(() => toast.remove(), 4200);
}

async function getJson(url) {
  const response = await fetch(url, { headers: { Accept: "application/json" }, cache: "no-store" });
  const text = await response.text();
  const data = text ? JSON.parse(text) : {};
  if (!response.ok) throw new Error(data.message || data.error || `${response.status} ${response.statusText}`);
  return data;
}

async function postJson(url, payload) {
  const response = await fetch(url, { method: "POST", headers: { "Content-Type": "application/json", Accept: "application/json" }, body: JSON.stringify(payload) });
  const text = await response.text();
  let data = {};
  try { data = text ? JSON.parse(text) : {}; } catch { data = { error: text }; }
  return { ok: response.ok, status: response.status, data };
}

function logicalPreviewSize() {
  return logicalPreviewSizeFor(state.resolution);
}

function logicalPreviewSizeFor(resolution) {
  const design = state.blueprint?.designResolution || { width: 1920, height: 1080 };
  const previewRatio = resolution.width / resolution.height;
  const designRatio = design.width / design.height;
  if (previewRatio >= designRatio) return { width: Math.round(design.height * previewRatio), height: design.height };
  return { width: design.width, height: Math.round(design.width / previewRatio) };
}

function adjustedLayout(layout, deltaWidth, deltaHeight) {
  const value = { ...layout };
  if (layout.horizontalAnchor === "Center") value.x += Math.round(deltaWidth / 2);
  else if (layout.horizontalAnchor === "Right") value.x += deltaWidth;
  else if (layout.horizontalAnchor === "Stretch") value.width += deltaWidth;
  if (layout.verticalAnchor === "Middle") value.y += Math.round(deltaHeight / 2);
  else if (layout.verticalAnchor === "Bottom") value.y += deltaHeight;
  else if (layout.verticalAnchor === "Stretch") value.height += deltaHeight;
  return value;
}

function canvasPoint(clientX, clientY) {
  const rect = el.physicalFrame.getBoundingClientRect();
  const scale = state.zoom / 100;
  return { x: Math.max(0, (clientX - rect.left) / scale), y: Math.max(0, (clientY - rect.top) / scale) };
}

function resources() { return state.resourceDraft?.resources || []; }
function recipes() { return state.recipeDraft?.recipes || []; }
function findResource(resourceId) { return resources().find(item => item.resourceId === resourceId) || null; }
function findRecipe(recipeId) { return recipes().find(item => item.componentId === recipeId) || null; }
function firstResourceId() { return resources()[0]?.resourceId || ""; }
function firstRecipeId() { return recipes()[0]?.componentId || ""; }
function resourceUrl(resource) {
  if (!resource) return "";
  const root = state.serverState?.resourceCatalog?.resourceRoot || "ArtSource/UIKits/MxUiFoundation";
  return `/${encodeURI(`${root}/${resource.relativePath}`).replace(/'/g, "%27").replace(/\"/g, "%22")}`;
}
function recipeBaseResource(recipe) {
  const slot = recipe?.resourceSlots?.find(item => item.slotId === "base");
  return findResource(slot?.resourceId);
}
function semanticImageStyle(resource, tint) {
  if (!resource) return ["background:#1b2328"];
  const usage = resource.recommendedFairyGuiUsage || {};
  const url = resourceUrl(resource);
  const styles = [`--semantic-tint:${fguiColorToCss(tint || "#ffffffff")}`];
  if (usage.policy === "NineSlice" && usage.scale9Grid) {
    const size = usage.designSize || {};
    const grid = usage.scale9Grid;
    const right = Math.max(0, (size.width || 0) - grid.x - grid.width);
    const bottom = Math.max(0, (size.height || 0) - grid.y - grid.height);
    styles.push(`border-image-source:url('${url}')`, `border-image-slice:${grid.y} ${right} ${bottom} ${grid.x} fill`, `border-width:${grid.y}px ${right}px ${bottom}px ${grid.x}px`, "border-style:solid");
  } else {
    const fit = usage.preserveAspect || usage.policy === "PreserveAspect" ? "contain" : "100% 100%";
    styles.push(`background-image:url('${url}')`, `background-size:${fit}`, "background-position:center", "background-repeat:no-repeat");
  }
  return styles;
}
function populateResourceSelect(select, selectedId, allowEmpty, category = "") {
  const values = resources().filter(item => !category || item.category === category);
  select.innerHTML = `${allowEmpty ? '<option value="">无</option>' : ""}${values.map(item => `<option value="${escapeHtml(item.resourceId)}">${escapeHtml(item.displayName || item.resourceId)} · ${escapeHtml(item.resourceId)}</option>`).join("")}`;
  select.value = selectedId || "";
}
function populateRecipeSelect(selectedId) {
  el.componentRecipeInput.innerHTML = recipes().map(item => `<option value="${escapeHtml(item.componentId)}">${escapeHtml(item.displayName || item.componentId)}</option>`).join("");
  el.componentRecipeInput.value = selectedId || "";
}
function populateListRecipeSelect(selectedId) {
  el.listItemRecipeInput.innerHTML = recipes().map(item => `<option value="${escapeHtml(item.componentId)}">${escapeHtml(item.displayName || item.componentId)}</option>`).join("");
  el.listItemRecipeInput.value = selectedId || "";
}
function renderImageUsage(node) {
  const resource = findResource(node.image?.resourceId);
  const usage = resource?.recommendedFairyGuiUsage || {};
  const grid = usage.scale9Grid;
  el.imageUsageSummary.innerHTML = resource ? `<span class="semantic-thumb" style="background-image:url('${resourceUrl(resource)}')"></span><div><strong>${escapeHtml(resource.displayName || resource.resourceId)}</strong><small>${escapeHtml(usage.policy || "-")}${grid ? ` · ${grid.x},${grid.y},${grid.width},${grid.height}` : ""}</small></div>` : "未找到语义资源";
}
function renderComponentUsage(node) {
  const recipe = findRecipe(node.component?.componentRecipeId);
  const resource = recipeBaseResource(recipe);
  const icon = findResource(node.component?.iconResourceId);
  el.componentUsageSummary.innerHTML = recipe ? `<span class="semantic-thumb" style="background-image:url('${resourceUrl(resource)}')"></span><div><strong>${escapeHtml(recipe.displayName || recipe.componentId)}</strong><small>${escapeHtml(resource?.resourceId || "-")}${icon ? ` · ${escapeHtml(icon.displayName)}` : " · 无图标"}</small><div class="inline-reference-actions"><button type="button" data-open-recipe="${escapeHtml(recipe.componentId)}">编辑配方</button>${resource ? `<button type="button" data-open-resource="${escapeHtml(resource.resourceId)}">查看底图</button>` : ""}${icon ? `<button type="button" data-open-resource="${escapeHtml(icon.resourceId)}">查看图标</button>` : ""}</div></div>` : "未找到组件配方";
  el.componentUsageSummary.querySelectorAll("[data-open-recipe]").forEach(button => button.addEventListener("click", () => { state.contextStack.push(currentContext()); switchTabs("rail", "recipes"); openRecipeEditor(button.dataset.openRecipe, false); }));
  el.componentUsageSummary.querySelectorAll("[data-open-resource]").forEach(button => button.addEventListener("click", () => { state.contextStack.push(currentContext()); switchTabs("rail", "resources"); openResourceEditor(button.dataset.openResource, false); }));
}
function renderListItemUsage(node) {
  const recipe = findRecipe(node.list?.itemComponentRecipeId);
  const resource = recipeBaseResource(recipe);
  el.listItemUsageSummary.innerHTML = recipe ? `<span class="semantic-thumb" style="background-image:url('${resourceUrl(resource)}')"></span><div><strong>${escapeHtml(recipe.displayName || recipe.componentId)}</strong><small>默认条目 · ${escapeHtml(node.list?.virtualized === true ? "运行时虚拟列表" : "普通列表")}</small><div class="inline-reference-actions"><button type="button" data-open-list-recipe="${escapeHtml(recipe.componentId)}">编辑配方</button></div></div>` : "未找到条目组件配方。";
  el.listItemUsageSummary.querySelectorAll("[data-open-list-recipe]").forEach(button => button.addEventListener("click", () => { state.contextStack.push(currentContext()); switchTabs("rail", "recipes"); openRecipeEditor(button.dataset.openListRecipe, false); }));
}

function parseResolution(value) { const [width, height] = value.split("x").map(Number); return { width, height }; }
function numberValue(input, fallback) { const value = Number(input.value); return Number.isFinite(value) ? Math.round(value) : fallback; }
function snap(value, bypass = false) { return state.snapEnabled && !bypass ? Math.round(value / state.gridSize) * state.gridSize : Math.round(value); }
function clone(value) { return JSON.parse(JSON.stringify(value)); }
function stableBlueprint(value) { return JSON.stringify(value); }
function stableWorkspaceSnapshot() { return JSON.stringify({ snapshotVersion: 1, blueprint: state.blueprint, resourceDraft: state.resourceDraft, recipeDraft: state.recipeDraft }); }
function restoreWorkspaceSnapshot(value) { const parsed = JSON.parse(value); if (parsed?.snapshotVersion === 1) { state.blueprint = parsed.blueprint; state.resourceDraft = parsed.resourceDraft; state.recipeDraft = parsed.recipeDraft; } else state.blueprint = parsed; }
function isDirty() { return Boolean(state.blueprint) && stableBlueprint(state.blueprint) !== state.baseline; }
function isResourceDirty() { return Boolean(state.resourceDraft) && JSON.stringify(state.resourceDraft) !== state.resourceBaseline; }
function isRecipeDirty() { return Boolean(state.recipeDraft) && JSON.stringify(state.recipeDraft) !== state.recipeBaseline; }
function hasAnyDirty() { return isDirty() || isResourceDirty() || isRecipeDirty(); }
function isRequiredBinding(slotId) { return Boolean((state.serverState?.bindingSlots || []).find(item => item.slotId === slotId)?.required); }
function nextNodeIndex(type) { let i = 1; const prefix = type === "ComponentInstance" ? "button" : type.toLowerCase(); while (state.blueprint.nodes.some(node => node.nodeId === `${prefix}_${i}`)) i++; return i; }
function normalizeBlueprintSchemaVersion() { if ((state.blueprint?.nodes || []).some(node => node.type === "List")) state.blueprint.schemaVersion = "mx.uiScreenBlueprint.v3"; else if ((state.blueprint?.containers || []).length && state.blueprint.schemaVersion === "mx.uiScreenBlueprint.v1") state.blueprint.schemaVersion = "mx.uiScreenBlueprint.v2"; }
function nodeTypeIcon(type) { return type === "Graph" ? "■" : type === "Text" ? "T" : type === "Image" ? "▧" : type === "List" ? "≡" : "B"; }
function alignToFlex(value) { return value === "center" ? "center" : value === "right" ? "flex-end" : "flex-start"; }
function delay(ms) { return new Promise(resolve => setTimeout(resolve, ms)); }
function escapeHtml(value) { return String(value ?? "").replace(/[&<>'"]/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" })[char]); }

function fguiColorToCss(value) {
  const text = String(value || "#ffffffff").replace("#", "");
  if (text.length === 8) {
    const alpha = parseInt(text.slice(0, 2), 16) / 255;
    return `rgba(${parseInt(text.slice(2, 4), 16)}, ${parseInt(text.slice(4, 6), 16)}, ${parseInt(text.slice(6, 8), 16)}, ${alpha.toFixed(3)})`;
  }
  return `#${text.slice(-6).padStart(6, "f")}`;
}

function fguiColorToHtmlColor(value) { const text = String(value || "#ffffff").replace("#", ""); return `#${text.length === 8 ? text.slice(2) : text.slice(-6)}`; }
function htmlColorToFgui(value, previous) { const alpha = String(previous || "#ff").replace("#", "").length === 8 ? String(previous).replace("#", "").slice(0, 2) : "ff"; return `#${alpha}${String(value).replace("#", "")}`; }
function normalizeFguiColor(value, fallback) { const text = String(value || "").trim(); return /^#[0-9a-fA-F]{8}$/.test(text) ? text.toLowerCase() : /^#[0-9a-fA-F]{6}$/.test(text) ? `#ff${text.slice(1).toLowerCase()}` : fallback; }
