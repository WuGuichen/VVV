import "./src/state.js?v=source-workspace-v2";
import "./src/utils.js?v=raw-material-integrity-v1";
import "./src/sessionState.js?v=source-workspace-v2";
import "./src/api.js?v=palette-usage-authority-v1";
import "./src/surfacePaletteWorkbench.js?v=palette-usage-authority-v1";
import "./src/characterManager.js?v=character-manager-v2";
import "./src/importMaterialsPanel.js?v=source-workspace-v2";
import "./src/importMaterialsOperations.js?v=source-workspace-v2";
import "./src/resourcePicker.js?v=public-combined-v15";
import "./src/singleWeaponWorkbench.js?v=public-combined-v15";
import "./src/reactionConfigurationWorkbench.js?v=public-combined-v15";
import "./src/combinedWeaponWorkbench.js?v=character-action-slots-v1";
import "./src/bodyWorkspace.js?v=body-parts-v1";
import "./src/bodyPartsWorkbench.js?v=body-parts-v1";
import "./src/bodyMeasurements.js?v=body-overview-v1";
import "./src/bodyCollisionPreview.js?v=movement-primitives-v2";
import "./src/contextPanels.js?v=body-navigation-v1";
import "./src/humanoidHurtboxes.js?v=body-parts-v1";
import "./src/viewport3d.js?v=palette-usage-authority-v1";
import "./src/inspector.js?v=body-parts-v1";
import "./src/tacticalDefaultsWorkbench.js?v=tactical-defaults-v1";

function ensureReactionPanelShell() {
  // The reaction result lives in the consolidated runtime-validation panel.
}

function ensureWeaponConfigurationPanelShell() {
  const toolbar = document.querySelector(".topbar .toolbar");
  if (toolbar && !document.getElementById("activeCombinedWeaponConfigurationSelect")) {
    const label = document.createElement("label");
    label.className = "active-weapon-configuration";
    label.innerHTML = `<span>角色组合</span><select id="activeCombinedWeaponConfigurationSelect" title="武器、动画、受击、动作和预览共享当前组合"></select>`;
    toolbar.insertBefore(label, document.getElementById("packageSelect")?.nextSibling || toolbar.firstChild);
  }
  const tabs = document.getElementById("contextDockTabs");
  if (tabs && !tabs.querySelector('button[data-context-tab="singleWeapons"]')) {
    for (const [tab, text] of [
      ["singleWeapons", "单一武器"],
      ["combinedWeapons", "组合武器"]
    ]) {
      const button = document.createElement("button");
      button.type = "button";
      button.dataset.contextTab = tab;
      button.setAttribute("role", "tab");
      button.setAttribute("aria-selected", "false");
      button.textContent = text;
      tabs.insertBefore(button, tabs.querySelector('button[data-context-tab="materials"]'));
    }
  }
  if (tabs && !tabs.querySelector('button[data-context-tab="surfacePalette"]')) {
    const button = document.createElement("button");
    button.type = "button";
    button.dataset.contextTab = "surfacePalette";
    button.setAttribute("role", "tab");
    button.setAttribute("aria-selected", "false");
    button.textContent = "全局画板";
    tabs.insertBefore(button, tabs.querySelector('button[data-context-tab="materials"]'));
  }
  const body = document.getElementById("contextDockBody");
  if (body && !body.querySelector('[data-context-panel="singleWeapons"]')) {
    const single = document.createElement("section");
    single.className = "context-panel";
    single.dataset.contextPanel = "singleWeapons";
    single.hidden = true;
    single.innerHTML = `<div class="context-panel-head"><strong>单一武器配置</strong><span>全局复用武器规则；资源资产由 Resource Manager 管理</span></div><div id="singleWeaponConfigurationPanel" class="weapon-configuration-panel"></div>`;
    body.insertBefore(single, body.querySelector('[data-context-panel="materials"]'));
    const combined = document.createElement("section");
    combined.className = "context-panel";
    combined.dataset.contextPanel = "combinedWeapons";
    combined.hidden = true;
    combined.innerHTML = `<div class="context-panel-head"><strong>组合武器配置</strong><span>当前角色显式主手 / 副手 → 状态 / 动画 / 受击 / 动作</span></div><div id="combinedWeaponConfigurationPanel" class="weapon-configuration-panel"></div>`;
    body.insertBefore(combined, body.querySelector('[data-context-panel="materials"]'));
  }
  if (body && !body.querySelector('[data-context-panel="surfacePalette"]')) {
    const palette = document.createElement("section");
    palette.className = "context-panel surface-palette-context-panel";
    palette.dataset.contextPanel = "surfacePalette";
    palette.hidden = true;
    palette.innerHTML = `<div class="context-panel-head"><strong>全局 Color Palette + Surface Profile</strong><span>任意面都可组合任意规律颜色与 16 种材质表现</span></div><div id="surfacePaletteWorkbench" class="surface-palette-workbench"></div>`;
    body.insertBefore(palette, body.querySelector('[data-context-panel="materials"]'));
  }
}
document.addEventListener("DOMContentLoaded", () => {
  ensureWeaponConfigurationPanelShell();
  ensureReactionPanelShell();
  for (const id of [
    "packageSelect", "activeCombinedWeaponConfigurationSelect", "newCharacterButton", "duplicateCharacterButton", "archiveCharacterButton", "deleteCharacterButton", "migrateCharacterButton",
    "reloadButton", "discardReloadButton", "saveButton", "compileButton", "unityBuildTargetSelect", "importButton", "workflowBar",
    "resourceBindingRole",
    "packageSummary", "packageTree", "dirtyBadge", "viewport",
    "workspace", "topPanelContainer", "treeCollapseButton", "previewPoseSelect", "previewMotionSelect", "hurtboxDisplayControls", "hurtboxDisplayModeSelect", "hurtboxPartFilterLabel", "hurtboxPartFilterSelect", "hurtboxDisplaySummary", "hurtboxBindingReferenceButton", "hurtboxCoverageButton", "resetCameraButton",
    "viewWeaponConfigButton", "returnCharacterViewButton", "weaponModeChooseModelButton", "weaponModeEditTraceButton", "viewportModeStatus",
    "undoButton", "redoButton",
    "weaponTransformMoveButton", "weaponTransformRotateButton", "weaponTransformScaleButton", "symmetryEditToggleButton", "colliderSymmetrySyncButton",
    "configCreateSelect", "configCreateButton",
    "contextDock", "contextDockSummary", "contextDockTabs", "contextDockToggleButton", "contextDockBody",
    "resourceBindingTarget", "resourceBindingDetails", "openResourcePickerButton", "clearModelBindingButton",
    "importMaterialsPanel", "surfacePaletteWorkbench", "singleWeaponConfigurationPanel", "globalAnimationConfigurationPanel", "globalAbilityConfigurationPanel", "globalReactionConfigurationPanel", "combinedWeaponConfigurationPanel", "animationConfigPanel", "reactionProfilePanel",
    "resourcePickerOverlay", "resourcePickerTitle", "resourcePickerSummary", "resourcePickerContext",
    "resourcePickerDebug", "resourcePickerSearch", "resourcePickerUsageFilter", "resourcePickerProviderFilter", "resourcePickerClearFilters",
    "resourcePickerList", "closeResourcePickerButton",
    "characterManagerOverlay", "characterManagerTitle", "characterManagerSummary", "characterManagerSource",
    "characterManagerForm", "characterManagerIdentityFields", "characterManagerPackageId", "characterManagerDisplayName",
    "characterManagerTemplateField", "characterManagerTemplate", "characterManagerArchiveFields",
    "characterManagerArchiveWarning", "characterManagerArchiveConfirm", "characterManagerError",
    "closeCharacterManagerButton", "cancelCharacterManagerButton", "submitCharacterManagerButton",
    "resourcePlanPreview", "readinessFlow", "abilityLoadoutPanel",
    "inspector", "diagnostics", "importStatus", "selectionBadge", "copyReportButton", "inspectorTabs",
    "subtitle"
  ]) el[id] = document.getElementById(id);

  registerCharacterManagerEvents();
  wireSingleWeaponWorkbench();
  wireCombinedWeaponWorkbench();
  wireSurfacePaletteWorkbench?.();
  restoreCharacterStudioPreferences?.();
  window.addEventListener("message", async event => {
    const message = event?.data;
    if (message?.type !== "mx-global-configuration-saved" || message.configurationType !== "animation") return;
    if (state.weaponLibraryDirty) {
      state.message = "Animation Editor 已保存动画配置，但 CharacterStudio 仍有全局库草稿；请先处理草稿再刷新，避免覆盖。";
      renderShellStatus();
      return;
    }
    await loadGlobalWeaponLibrary();
    await loadWeaponConfigurations();
    state.message = `动画配置 ${message.configurationId || ""} 已从 Animation Editor 刷新。`;
    render();
  });
  registerCharacterStudioSessionProtection();
  el.reloadButton.addEventListener("click", async () => {
    const originalText = el.reloadButton.textContent;
    el.reloadButton.classList.add("is-busy");
    el.reloadButton.disabled = true;
    el.reloadButton.textContent = "刷新中…";
    try {
      await refreshExternalState();
    } catch (err) {
      console.error("reloadButton error:", err);
      showToast(`刷新状态异常：${err?.message || err}`, "error");
    } finally {
      el.reloadButton.classList.remove("is-busy");
      el.reloadButton.disabled = false;
      el.reloadButton.textContent = originalText;
      renderShellStatus();
    }
  });
  el.discardReloadButton.addEventListener("click", async () => {
    const originalText = el.discardReloadButton.textContent;
    el.discardReloadButton.classList.add("is-busy");
    el.discardReloadButton.disabled = true;
    el.discardReloadButton.textContent = "重载中…";
    try {
      await discardDraftAndReload();
    } catch (err) {
      console.error("discardReloadButton error:", err);
      showToast(`重载异常：${err?.message || err}`, "error");
    } finally {
      el.discardReloadButton.classList.remove("is-busy");
      el.discardReloadButton.disabled = false;
      el.discardReloadButton.textContent = originalText;
      renderShellStatus();
    }
  });
  el.saveButton.addEventListener("click", async () => {
    const originalText = el.saveButton.textContent;
    el.saveButton.classList.add("is-busy");
    el.saveButton.disabled = true;
    el.saveButton.textContent = "保存中…";
    try {
      await savePackage();
    } catch (err) {
      console.error("saveButton error:", err);
      showToast(`保存异常：${err?.message || err}`, "error");
    } finally {
      el.saveButton.classList.remove("is-busy");
      el.saveButton.textContent = originalText;
      renderShellStatus();
    }
  });
  el.compileButton.addEventListener("click", async () => {
    const originalText = el.compileButton.textContent;
    el.compileButton.classList.add("is-busy");
    el.compileButton.disabled = true;
    el.compileButton.textContent = "预检中…";
    try {
      await compilePackage();
    } catch (err) {
      console.error("compileButton error:", err);
      showToast(`预检异常：${err?.message || err}`, "error");
    } finally {
      el.compileButton.classList.remove("is-busy");
      el.compileButton.disabled = false;
      el.compileButton.textContent = originalText;
      renderShellStatus();
    }
  });
  el.unityBuildTargetSelect.value = state.unityImportBuildTarget;
  el.unityBuildTargetSelect.addEventListener("change", event => {
    state.unityImportBuildTarget = event.target.value;
    persistCharacterStudioPreferences?.();
    renderShellStatus();
  });
  el.importButton.addEventListener("click", async () => {
    const originalText = el.importButton.textContent;
    el.importButton.classList.add("is-busy");
    el.importButton.disabled = true;
    el.importButton.textContent = "导入中…";
    try {
      await importUnity();
    } catch (err) {
      console.error("importButton error:", err);
      showToast(`Unity 导入异常：${err?.message || err}`, "error");
    } finally {
      el.importButton.classList.remove("is-busy");
      el.importButton.disabled = false;
      el.importButton.textContent = originalText;
      renderShellStatus();
    }
  });
  document.addEventListener("click", handleWorkflowActionClick);
  el.resourceBindingRole.addEventListener("change", () => {
    updateResourceBindingControls();
    renderResourceBindingBar();
    renderResourcePicker();
  });
  el.treeCollapseButton.addEventListener("click", () => {
    state.treeCollapsed = !state.treeCollapsed;
    renderLayoutState();
  });
  el.configCreateButton.addEventListener("click", () => {
    if (el.configCreateSelect.value === "combinedWeapon") {
      state.activeContextTab = "combinedWeapons";
      state.contextDockCollapsed = false;
      applyWeaponConfigurationAction("new");
      return;
    }
    createConfiguration(el.configCreateSelect.value);
  });
  document.getElementById("addHumanoidHurtboxesButton")?.addEventListener("click", () => globalThis.addHumanoidHurtboxes?.());
  el.previewPoseSelect.addEventListener("change", event => {
    state.previewPose = event.target.value;
    renderViewport();
  });
  el.previewMotionSelect.addEventListener("change", event => {
    state.previewMotion = event.target.value;
    renderViewport();
  });
  el.hurtboxDisplayModeSelect?.addEventListener("change", event => {
    state.hurtboxDisplayMode = normalizeHurtboxDisplayMode(event.target.value);
    if (state.hurtboxDisplayMode !== "all" && state.hurtboxCoverageVisible) {
      state.hurtboxCoverageVisible = false;
      state.message = "六向覆盖热图需要全部受击体，已关闭热图并保留当前部位视图。";
      renderShellStatus();
    }
    renderPreviewControls();
    renderViewport();
    scheduleCharacterStudioSessionPersistence?.();
  });
  el.hurtboxPartFilterSelect?.addEventListener("change", event => {
    const partId = event.target.value || "";
    state.hurtboxDisplayMode = "part";
    state.hurtboxDisplayPartId = partId;
    const partPath = partId ? `geometry/body_parts/${partId}` : "";
    if (partPath && findTarget(partPath)?.value) selectPath(partPath);
    else {
      renderPreviewControls();
      renderViewport();
    }
    scheduleCharacterStudioSessionPersistence?.();
  });
  el.hurtboxBindingReferenceButton?.addEventListener("click", () => {
    state.hurtboxBindingReferenceVisible = !state.hurtboxBindingReferenceVisible;
    renderPreviewControls();
    renderViewport();
    scheduleCharacterStudioSessionPersistence?.();
  });
  el.hurtboxCoverageButton?.addEventListener("click", () => {
    if (!isHurtboxCoverageScopeAvailable()) {
      state.message = "六向覆盖热图需要先将受击体显示范围切换为“全部”。";
      renderShellStatus();
      return;
    }
    state.hurtboxCoverageVisible = !state.hurtboxCoverageVisible;
    el.hurtboxCoverageButton.classList.toggle("active", state.hurtboxCoverageVisible);
    el.hurtboxCoverageButton.setAttribute("aria-pressed", String(state.hurtboxCoverageVisible));
    renderViewport();
  });
  el.resetCameraButton.addEventListener("click", () => {
    clearCurrentViewportCameraState();
    state.skipNextCameraRemember = true;
    renderViewport();
  });
  el.viewWeaponConfigButton?.addEventListener("click", () => {
    const weapon = getCurrentOrFirstActiveWeaponAttachment();
    if (!weapon?.weaponId) {
      state.message = "The current view has no weapon configuration to inspect.";
      renderShellStatus();
      return;
    }
    enterWeaponViewport(getWeaponAttachmentIdentity(weapon));
  });
  el.returnCharacterViewButton?.addEventListener("click", () => returnToCharacterViewport());
  document.getElementById("attributeOverlayButton")?.addEventListener("click", () => {
    state.attributePreviewOverlays = !state.attributePreviewOverlays;
    renderLayoutState();
    renderViewport();
  });
  document.getElementById("attributeResetCameraButton")?.addEventListener("click", () => el.resetCameraButton?.click());
  el.weaponModeChooseModelButton?.addEventListener("click", () => {
    const weapon = getFocusedWeaponAttachment();
    if (!weapon?.weaponId) return;
    state.selectedPath = getWeaponAttachmentPath(weapon);
    renderTree();
    renderInspector();
    openResourcePickerForField("weaponRef");
  });
  el.weaponModeEditTraceButton?.addEventListener("click", () => {
    const weapon = getFocusedWeaponAttachment();
    if (!weapon?.traceId) {
      state.message = "The current weapon has no attack trace bound.";
      renderShellStatus();
      return;
    }
    const tracePath = `geometry/traces/${weapon.traceId}`;
    state.weaponTransformKey = `${tracePath}#startPose`;
    selectPath(tracePath, { preserveViewport: true });
  });  el.weaponTransformMoveButton?.addEventListener("click", () => setWeaponTransformMode("translate"));
  el.weaponTransformRotateButton?.addEventListener("click", () => setWeaponTransformMode("rotate"));
  el.weaponTransformScaleButton?.addEventListener("click", () => setWeaponTransformMode("scale"));
  el.symmetryEditToggleButton?.addEventListener("click", () => toggleSymmetryEdit());
  el.colliderSymmetrySyncButton?.addEventListener("click", () => synchronizeVisibleColliderSymmetryFromLeft());
  el.undoButton?.addEventListener("click", () => undoLastEdit());
  el.redoButton?.addEventListener("click", () => redoLastEdit());
  document.addEventListener("keydown", handleUndoRedoKeydown);
  document.addEventListener("keydown", handleViewportTransformHotkeys);
  el.openResourcePickerButton.addEventListener("click", openResourcePicker);
  el.closeResourcePickerButton.addEventListener("click", closeResourcePicker);
  el.resourcePickerOverlay.addEventListener("click", event => {
    const resourceManagerButton = event.target.closest("[data-resource-manager-action]");
    if (resourceManagerButton) {
      event.stopPropagation();
      if (resourceManagerButton.dataset.resourceManagerAction === "open-picker") {
        openResourceManagerForActivePicker();
      }
      return;
    }
    if (event.target === el.resourcePickerOverlay) closeResourcePicker();
  });
  el.resourcePickerSearch?.addEventListener("input", event => {
    state.resourcePickerSearchQuery = String(event.target.value || "").trim();
    renderResourcePicker();
  });
  el.resourcePickerUsageFilter?.addEventListener("change", event => {
    state.resourcePickerUsageFilter = String(event.target.value || "");
    renderResourcePicker();
  });
  el.resourcePickerProviderFilter?.addEventListener("change", event => {
    state.resourcePickerProviderFilter = String(event.target.value || "");
    renderResourcePicker();
  });
  el.resourcePickerClearFilters?.addEventListener("click", () => {
    resetResourcePickerFilters();
    renderResourcePicker();
  });
  el.resourcePickerList.addEventListener("click", event => {
    const target = event.target.closest("[data-library-id]");
    if (!target) return;
    selectLibraryItem(target.dataset.libraryId);
  });
  el.clearModelBindingButton.addEventListener("click", clearCurrentModelBinding);
  el.contextDockToggleButton?.addEventListener("click", () => {
    state.contextDockCollapsed = !state.contextDockCollapsed;
    renderContextDockState();
  });
  el.contextDockTabs?.addEventListener("click", event => {
    const button = event.target.closest("button[data-context-tab]");
    if (!button) return;
    const previousTab = state.activeContextTab;
    const nextTab = button.dataset.contextTab || "binding";
    globalThis.leaveBaseAttributeWorkspace?.();
    globalThis.leaveBodyWorkspace?.();
    state.activeContextTab = nextTab;
    state.contextDockCollapsed = false;
    if (nextTab === "singleWeapons") globalThis.activateSingleWeaponForEditing?.();
    else if (previousTab === "singleWeapons" && state.viewportMode === "weapon") globalThis.returnToCharacterViewport?.();
    else renderContextDockState();
  });
  el.contextDockBody?.addEventListener("click", async event => {
    const button = event.target.closest("button[data-validation-tab]");
    if (!button) return;
    state.activeRuntimeValidationTab = button.dataset.validationTab || "ability";
    if (state.activeRuntimeValidationTab === "ability" && !state.abilityChain) await loadAbilityChain();
    renderRuntimeValidationTabs();
  });
  el.abilityLoadoutPanel?.addEventListener("click", async event => {
    if (!event.target.closest("[data-load-frozen-ability-diagnostics]")) return;
    await loadAbilityChain();
    renderAbilityLoadoutPanel();
  });
  el.importMaterialsPanel?.addEventListener("click", event => handleImportMaterialsClick(event));
  el.importMaterialsPanel?.addEventListener("input", event => handleImportMaterialsInput(event));
  el.importMaterialsPanel?.addEventListener("change", event => handleImportMaterialsInput(event));
  el.animationConfigPanel?.addEventListener("click", event => {
    const pickButton = event.target.closest("button[data-animation-pick]");
    if (pickButton) {
      openAnimationSlotPicker(pickButton.dataset.profileId, pickButton.dataset.slotId);
      return;
    }
    const clearButton = event.target.closest("button[data-animation-clear]");
    if (clearButton) {
      clearAnimationSlotSelection(clearButton.dataset.profileId, clearButton.dataset.slotId);
      return;
    }
    const jumpButton = event.target.closest("button[data-animation-jump]");
    if (jumpButton) {
      selectPath(jumpButton.dataset.path);
    }
  });
  el.reactionProfilePanel?.addEventListener("click", event => {
    const selectButton = event.target.closest("button[data-reaction-select]");
    if (selectButton) {
      state.selectedReactionSlotId = selectButton.dataset.slotId || "";
      renderReactionProfilePanel();
      return;
    }
    const pickButton = event.target.closest("button[data-reaction-pick]");
    if (pickButton) {
      openReactionSlotPicker(pickButton.dataset.profileId, pickButton.dataset.slotId);
      return;
    }
    const clearButton = event.target.closest("button[data-reaction-clear]");
    if (clearButton) {
      clearReactionSlotSelection(clearButton.dataset.profileId, clearButton.dataset.slotId);
      return;
    }
    const regenerateButton = event.target.closest("button[data-reaction-regenerate]");
    if (regenerateButton) {
      regenerateReactionProfileConfig();
      return;
    }
    const jumpButton = event.target.closest("button[data-reaction-jump]");
    if (jumpButton) {
      selectPath(jumpButton.dataset.path);
      return;
    }
    const actionButton = event.target.closest("button[data-reaction-action]");
    if (actionButton) {
      showGeneratedReactionActionConfig(actionButton.dataset.profileId, actionButton.dataset.slotId);
      return;
    }
    const editorButton = event.target.closest("button[data-reaction-editor]");
    if (editorButton) {
      openReactionActionAuthoring(editorButton.dataset.profileId, editorButton.dataset.slotId);
    }
  });
  el.abilityLoadoutPanel?.addEventListener("click", event => {
    const runtimeButton = event.target.closest("button[data-ability-runtime]");
    if (runtimeButton) {
      openAbilityRuntimeDiagnostic(runtimeButton.dataset.abilityId);
      return;
    }
    const actionButton = event.target.closest("button[data-ability-action]");
    if (!actionButton) return;
    if (actionButton.dataset.abilityFallback === "1") {
      copyAbilityActionAuthoringFallback(actionButton.dataset.abilityId);
      return;
    }
    openAbilityActionAuthoring(actionButton.dataset.abilityId);
  });
  el.abilityLoadoutPanel?.addEventListener("change", event => {
    const scenarioSelect = event.target.closest("select[data-ability-scenario]");
    if (!scenarioSelect) return;
    setActiveCombinedWeaponConfiguration(scenarioSelect.value || "");
  });
  el.activeCombinedWeaponConfigurationSelect?.addEventListener("change", event =>
    setActiveCombinedWeaponConfiguration(event.target.value || ""));
  el.combinedWeaponConfigurationPanel?.addEventListener("change", event => {
    const slotSelect = event.target.closest("select[data-weapon-config-slot]");
    if (slotSelect) {
      setWeaponConfigurationSlot(slotSelect.dataset.weaponConfigSlot, slotSelect.value || "");
      return;
    }
    const field = event.target.closest("[data-weapon-config-field]");
    if (field) updateWeaponConfigurationField(field.dataset.weaponConfigField, field.value || "");
  });
  el.combinedWeaponConfigurationPanel?.addEventListener("click", event => {
    const button = event.target.closest("button[data-weapon-config-action]");
    if (button) applyWeaponConfigurationAction(button.dataset.weaponConfigAction);
  });
  for (const panel of [el.globalAnimationConfigurationPanel, el.globalAbilityConfigurationPanel, el.globalReactionConfigurationPanel]) {
    panel?.addEventListener("change", event => {
      const select = event.target.closest("select[data-global-config-select]");
      if (select) {
        setActiveGlobalConfiguration(select.dataset.globalConfigSelect, select.value || "");
        render();
        return;
      }
      const field = event.target.closest("[data-global-config-field]");
      if (field) updateGlobalConfigurationField(
        field.dataset.globalConfigType,
        field.dataset.globalConfigField,
        field.value || "");
      const abilityField = event.target.closest("[data-ability-entry-field]");
      if (abilityField) updateAbilityEntryField(abilityField, false);
    });
    panel?.addEventListener("click", event => {
      const button = event.target.closest("button[data-global-config-action]");
      if (button) applyGlobalConfigurationAction(
        button.dataset.globalConfigType,
        button.dataset.globalConfigAction);
      const abilityButton = event.target.closest("button[data-ability-entry-action]");
      if (abilityButton) applyAbilityEntryAction(abilityButton, false);
    });
  }
  el.combinedWeaponConfigurationPanel?.addEventListener("change", event => {
    const quickField = event.target.closest("[data-global-quick-field]");
    if (quickField) updateGlobalConfigurationField(
      quickField.dataset.globalConfigType,
      quickField.dataset.globalQuickField,
      quickField.value || "",
      true);
    const abilityField = event.target.closest("[data-ability-entry-field]");
    if (abilityField) updateAbilityEntryField(abilityField, true);
  });
  el.combinedWeaponConfigurationPanel?.addEventListener("click", event => {
    const button = event.target.closest("button[data-global-quick-action]");
    if (button) applyGlobalConfigurationAction(
      button.dataset.globalConfigType,
      button.dataset.globalQuickAction);
    const abilityButton = event.target.closest("button[data-ability-entry-action]");
    if (abilityButton) applyAbilityEntryAction(abilityButton, true);
  });
  for (const panel of [el.animationConfigPanel, el.abilityLoadoutPanel, el.reactionProfilePanel]) {
    panel?.addEventListener("change", event => {
      const quickField = event.target.closest("[data-global-quick-field]");
      if (quickField) updateGlobalConfigurationField(
        quickField.dataset.globalConfigType,
        quickField.dataset.globalQuickField,
        quickField.value || "",
        true);
      const abilityField = event.target.closest("[data-ability-entry-field]");
      if (abilityField) updateAbilityEntryField(abilityField, true);
    });
    panel?.addEventListener("click", event => {
      const button = event.target.closest("button[data-global-quick-action]");
      if (button) applyGlobalConfigurationAction(
        button.dataset.globalConfigType,
        button.dataset.globalQuickAction);
      const abilityButton = event.target.closest("button[data-ability-entry-action]");
      if (abilityButton) applyAbilityEntryAction(abilityButton, true);
    });
  }
  el.copyReportButton.addEventListener("click", () => copyReport());
  el.inspectorTabs?.addEventListener("click", event => {
    const button = event.target.closest("button[data-inspector-tab]");
    if (!button) return;
    state.activeInspectorTab = button.dataset.inspectorTab || "properties";
    renderInspectorTabState();
  });
  el.packageSelect.addEventListener("change", event => {
    persistCharacterStudioSessionState();
    state.packageRelative = event.target.value;
    state.userSelectedPackage = true;
    persistCharacterStudioPreferences?.();
    state.selectedPath = "overview";
    loadAll();
  });
  document.getElementById("layerToggles").addEventListener("click", event => {
    const button = event.target.closest("button[data-layer]");
    if (!button) return;
    const layer = button.dataset.layer;
    state.layers[layer] = !state.layers[layer];
    button.classList.toggle("active", state.layers[layer]);
    renderViewport();
  });

  window.addEventListener("message", handleResourceManagerReturnMessage);
  loadAll();
});

function setActiveCombinedWeaponConfiguration(configurationId) {
  state.activeCombinedWeaponConfigurationId = configurationId || "";
  render();
}

function setActiveWeaponConfiguration(configurationId) {
  setActiveCombinedWeaponConfiguration(configurationId);
}

function ensureWeaponConfigurationCollections() {
  const app = state.package.applicationConfig ||= {};
  app.schemaVersion ||= "8.0";
  app.combinedWeaponConfigurationBindings = Array.isArray(app.combinedWeaponConfigurationBindings)
    ? app.combinedWeaponConfigurationBindings : [];
  const geometry = state.package.geometry ||= {};
  geometry.weaponSlots = Array.isArray(geometry.weaponSlots) ? geometry.weaponSlots : [];
  return app;
}

function ensureCharacterWeaponSlot(slotId) {
  const geometry = state.package.geometry ||= {};
  geometry.weaponSlots = Array.isArray(geometry.weaponSlots) ? geometry.weaponSlots : [];
  let slot = geometry.weaponSlots.find(item => item?.slotId === slotId);
  if (!slot) {
    slot = {
      slotId,
      attachSocketId: slotId,
      slotPose: {
        parentKind: "Socket", parentPath: slotId,
        position: { x: 0, y: 0, z: 0 }, rotation: { x: 0, y: 0, z: 0, w: 1 },
        scale: { x: 1, y: 1, z: 1 }, eulerHint: { x: 0, y: 0, z: 0 }
      }
    };
    geometry.weaponSlots.push(slot);
  }
  return slot;
}

function uniqueWeaponConfigurationId(base = "weapon_configuration") {
  const used = new Set((state.package?.applicationConfig?.combinedWeaponConfigurations || [])
    .map(item => item?.combinedWeaponConfigurationId));
  let index = 1;
  let candidate = base;
  while (used.has(candidate)) candidate = `${base}_${index++}`;
  return candidate;
}

function applyWeaponConfigurationAction(action) {
  if (globalThis.applyCombinedWeaponWorkbenchAction?.(action)) return;
  const app = ensureWeaponConfigurationCollections();
  const active = getActiveWeaponConfiguration();
  if (action === "new") {
    const id = uniqueWeaponConfigurationId();
    app.combinedWeaponConfigurations.push({
      combinedWeaponConfigurationId: id,
      displayName: "新组合武器配置",
      slots: [
        { slotId: "mainHand", singleWeaponConfigurationId: "weapon.unarmed" },
        { slotId: "offHand", singleWeaponConfigurationId: "weapon.unarmed" }
      ],
      animationConfigurationId: "",
      abilityConfigurationId: "",
      reactionConfigurationId: "",
      actionConfiguration: {
        actions: [],
        commandSequences: [],
        abilityActionBindings: [],
        reactionActionBindings: []
      }
    });
    normalizeWeaponConfiguration4(state.package);
    state.activeCombinedWeaponConfigurationId = id;
  } else if (action === "copy" && active) {
    const id = uniqueWeaponConfigurationId(`${active.combinedWeaponConfigurationId}_copy`);
    const copy = clone(active);
    copy.combinedWeaponConfigurationId = id;
    copy.displayName = `${active.displayName || active.combinedWeaponConfigurationId} 副本`;
    copy.actionConfiguration ||= {
      actions: [],
      commandSequences: [],
      abilityActionBindings: [],
      reactionActionBindings: []
    };
    app.combinedWeaponConfigurations.push(copy);
    normalizeWeaponConfiguration4(state.package);
    state.activeCombinedWeaponConfigurationId = id;
  } else if (action === "delete" && active) {
    if (app.combinedWeaponConfigurations.length <= 1) {
      state.message = "至少保留一个组合武器配置；空手必须显式选择 weapon.unarmed。";
      render();
      return;
    }
    app.combinedWeaponConfigurations = app.combinedWeaponConfigurations.filter(item => item !== active);
    if (app.defaultCombinedWeaponConfigurationId === active.combinedWeaponConfigurationId)
      app.defaultCombinedWeaponConfigurationId = app.combinedWeaponConfigurations[0].combinedWeaponConfigurationId;
    state.activeCombinedWeaponConfigurationId = app.defaultCombinedWeaponConfigurationId
      || app.combinedWeaponConfigurations[0].combinedWeaponConfigurationId;
  } else if (action === "default" && active) {
    app.defaultCombinedWeaponConfigurationId = active.combinedWeaponConfigurationId;
  }
  state.dirty = true;
  render();
}

function setWeaponConfigurationSlot(slotId, singleWeaponConfigurationId) {
  if (globalThis.setCombinedWeaponSlot) {
    globalThis.setCombinedWeaponSlot(slotId, singleWeaponConfigurationId);
    return;
  }
  const app = ensureWeaponConfigurationCollections();
  const configuration = getActiveWeaponConfiguration();
  if (!configuration) return;
  configuration.slots = Array.isArray(configuration.slots) ? configuration.slots : [];
  configuration.slots = configuration.slots.filter(item => item?.slotId !== slotId);
  const selectedId = singleWeaponConfigurationId || "weapon.unarmed";
  ensureCharacterWeaponSlot(slotId);
  configuration.slots.push({ slotId, singleWeaponConfigurationId: selectedId });
  normalizeWeaponConfiguration4(state.package);
  configuration.slots.sort((left, right) => String(left?.slotId || "").localeCompare(String(right?.slotId || "")));
  state.dirty = true;
  render();
}

function updateWeaponConfigurationField(field, value) {
  if (globalThis.updateCombinedWeaponField) {
    globalThis.updateCombinedWeaponField(field, value);
    return;
  }
  const configuration = getActiveWeaponConfiguration();
  if (!configuration || !["animationConfigurationId", "abilityConfigurationId", "reactionConfigurationId"].includes(field)) return;
  configuration[field] = value;
  state.dirty = true;
  render();
}

const GLOBAL_CONFIGURATION_TYPES = {
  animation: {
    collection: "animationConfigurations",
    idField: "animationConfigurationId",
    stateField: "activeAnimationConfigurationId",
    combinedField: "animationConfigurationId",
    prefix: "animation.new",
    create: () => ({ displayName: "新动画配置", description: "", rigType: "Humanoid", skeletonProfileId: "", avatarProfileId: "", sets: [], profiles: [] })
  },
  ability: {
    collection: "abilityConfigurations",
    idField: "abilityConfigurationId",
    stateField: "activeAbilityConfigurationId",
    combinedField: "abilityConfigurationId",
    prefix: "ability.new",
    create: () => ({ displayName: "新能力配置", description: "", abilities: [] })
  },
  reaction: {
    collection: "reactionConfigurations",
    idField: "reactionConfigurationId",
    stateField: "activeReactionConfigurationId",
    combinedField: "reactionConfigurationId",
    prefix: "reaction.new",
    create: () => ({ displayName: "新受击配置", description: "", defaultSlotId: "", slots: [] })
  }
};

function normalizeGlobalConfigurationId(value) {
  return String(value || "configuration").trim().toLowerCase()
    .replace(/[^a-z0-9._]+/g, "_").replace(/^_+|_+$/g, "") || "configuration";
}

function ensureGlobalConfigurationCollection(type) {
  const definition = GLOBAL_CONFIGURATION_TYPES[type];
  if (!definition) return [];
  const library = state.weaponLibrary ||= {
    schemaVersion: "6.0",
    libraryId: "mx.global.single-weapons",
    singleWeaponConfigurations: [],
    combinedWeaponConfigurations: [],
    animationConfigurations: [],
    abilityConfigurations: [],
    reactionConfigurations: []
  };
  library.schemaVersion = "6.0";
  library[definition.collection] = Array.isArray(library[definition.collection])
    ? library[definition.collection] : [];
  return library[definition.collection];
}

function getGlobalConfiguration(type, preferCombined = false) {
  const definition = GLOBAL_CONFIGURATION_TYPES[type];
  if (!definition) return null;
  const activeCombined = preferCombined
    ? globalThis.getActiveCombinedWeaponBaseConfiguration?.() || getActiveWeaponConfiguration()
    : getActiveWeaponConfiguration();
  const id = preferCombined
    ? activeCombined?.[definition.combinedField]
    : state[definition.stateField];
  return ensureGlobalConfigurationCollection(type).find(item => item?.[definition.idField] === id) || null;
}

function setActiveGlobalConfiguration(type, id) {
  const definition = GLOBAL_CONFIGURATION_TYPES[type];
  if (definition) state[definition.stateField] = id || "";
}

function uniqueGlobalConfigurationId(type, base) {
  const definition = GLOBAL_CONFIGURATION_TYPES[type];
  const items = ensureGlobalConfigurationCollection(type);
  const used = new Set(items.map(item => item?.[definition.idField]));
  const normalizedBase = normalizeGlobalConfigurationId(base || definition.prefix);
  let id = normalizedBase;
  let index = 1;
  while (used.has(id)) id = `${normalizedBase}.${index++}`;
  return id;
}

async function applyGlobalConfigurationAction(type, action) {
  const definition = GLOBAL_CONFIGURATION_TYPES[type];
  if (!definition) return;
  const items = ensureGlobalConfigurationCollection(type);
  const quick = action === "copyRebind";
  const newRebind = action === "newRebind";
  const active = newRebind ? null : getGlobalConfiguration(type, quick);
  if (action === "save") {
    await saveGlobalWeaponLibrary();
    render();
    return;
  }
  if (action === "delete" && active) {
    const references = await readJson(`/api/weapon-library/references?configurationType=${encodeURIComponent(type)}&configurationId=${encodeURIComponent(active[definition.idField])}`, null);
    if ((references?.referenceCount || 0) > 0) {
      state.message = `不能删除：该配置仍影响 ${references.referenceCount} 个角色组合。`;
      render();
      return;
    }
    state.weaponLibrary[definition.collection] = items.filter(item => item !== active);
    state[definition.stateField] = state.weaponLibrary[definition.collection][0]?.[definition.idField] || "";
  } else if (action === "new" || action === "copy" || quick || newRebind) {
    const id = uniqueGlobalConfigurationId(type, active
      ? `${active[definition.idField]}.copy`
      : definition.prefix);
    const item = active ? clone(active) : definition.create();
    item[definition.idField] = id;
    if (active) item.displayName = `${active.displayName || active[definition.idField]} 副本`;
    items.push(item);
    state[definition.stateField] = id;
    if (quick || newRebind) {
      const combined = globalThis.getActiveCombinedWeaponBaseConfiguration?.() || getActiveWeaponConfiguration();
      if (combined) {
        combined[definition.combinedField] = id;
        state.weaponLibraryDirty = true;
      }
    }
  }
  state.weaponLibraryDirty = true;
  render();
}

function updateGlobalConfigurationField(type, field, value, preferCombined = false) {
  const definition = GLOBAL_CONFIGURATION_TYPES[type];
  const item = getGlobalConfiguration(type, preferCombined);
  if (!definition || !item) return;
  if (field === "displayName" || field === "description") item[field] = value;
  else if (field === "configurationJson") {
    try {
      const parsed = JSON.parse(value || "{}");
      const stableId = item[definition.idField];
      for (const key of Object.keys(item)) delete item[key];
      Object.assign(item, parsed);
      item[definition.idField] = stableId;
    } catch (error) {
      state.message = `${type} 配置 JSON 无效：${error.message}`;
      renderShellStatus();
      return;
    }
  }
  state.weaponLibraryDirty = true;
  render();
}

function updateAbilityEntryField(element, preferCombined) {
  const item = getGlobalConfiguration("ability", preferCombined);
  const index = Number(element?.dataset?.abilityEntryIndex);
  const field = element?.dataset?.abilityEntryField || "";
  const entry = item?.abilities?.[index];
  if (!entry || !field) return;
  if (field === "abilityId") entry.abilityId = Math.max(0, Number(element.value) || 0);
  else if (field === "tags") entry.tags = String(element.value || "").split(",").map(value => value.trim()).filter((value, tagIndex, all) => value && all.indexOf(value) === tagIndex);
  else entry[field] = element.value || "";
  state.weaponLibraryDirty = true;
  render();
}

function applyAbilityEntryAction(button, preferCombined) {
  const item = getGlobalConfiguration("ability", preferCombined);
  if (!item) return;
  item.abilities ||= [];
  const action = button?.dataset?.abilityEntryAction || "";
  const index = Number(button?.dataset?.abilityEntryIndex);
  if (action === "new") {
    const used = new Set(item.abilities.map(entry => Number(entry?.abilityId) || 0));
    let abilityId = 1;
    while (used.has(abilityId)) abilityId++;
    item.abilities.push({ abilityId, displayName: `Ability ${abilityId}`, tier: "Unspecified", tags: [], executionMode: "External", triggerMode: "External", slotId: "", inputIntentId: "", actionBindingMode: "None", actionRole: "" });
  } else if (action === "delete" && Number.isInteger(index) && index >= 0) {
    item.abilities.splice(index, 1);
  }
  state.weaponLibraryDirty = true;
  render();
}

async function handleWorkflowActionClick(event) {
  const button = event.target.closest("[data-workflow-action]");
  if (!button) return;
  if (button.disabled || button.classList.contains("disabled")) {
    showToast(button.title || "当前操作不可用", "warn");
    return;
  }
  event.preventDefault();
  const action = button.dataset.workflowAction || "";
  const isAsync = ["save", "reload", "compile", "import"].includes(action);
  const originalText = button.textContent;
  if (isAsync) {
    button.classList.add("is-busy");
    button.disabled = true;
    if (action === "save") button.textContent = "保存中…";
    else if (action === "reload") button.textContent = "刷新中…";
    else if (action === "compile") button.textContent = "预检中…";
    else if (action === "import") button.textContent = "导入中…";
  }
  try {
    if (action === "save") {
      await savePackage();
      return;
    }
    if (action === "reload") {
      await refreshExternalState();
      return;
    }
    if (action === "compile") {
      await compilePackage();
      return;
    }
    if (action === "import") {
      await importUnity();
      return;
    }
    if (action === "open-context") {
      openWorkflowContextTab(button.dataset.contextTab || "validation", button.dataset.validationTab || "");
      return;
    }
    if (action === "open-inspector") {
      openWorkflowInspectorTab(button.dataset.inspectorTab || "import");
      return;
    }
    if (action === "locate") {
      if (button.dataset.contextTab) openWorkflowContextTab(button.dataset.contextTab, button.dataset.validationTab || "");
      if (button.dataset.inspectorTab) openWorkflowInspectorTab(button.dataset.inspectorTab);
      if (button.dataset.workflowPath) selectPath(button.dataset.workflowPath);
      return;
    }
    if (action === "open-url") {
      const url = button.dataset.url || "";
      if (url) window.open(url, "_blank", "noopener");
    }
  } catch (err) {
    console.error(`Workflow action ${action} failed:`, err);
    showToast(`操作异常：${err?.message || err}`, "error");
  } finally {
    if (isAsync) {
      button.classList.remove("is-busy");
      button.disabled = false;
      button.textContent = originalText;
      if (typeof renderWorkflowBar === "function") renderWorkflowBar();
    }
  }
}

function openWorkflowContextTab(tab, validationTab = "") {
  globalThis.leaveBaseAttributeWorkspace?.();
  globalThis.leaveBodyWorkspace?.();
  state.activeContextTab = normalizeContextTab(tab || "validation");
  if (validationTab) state.activeRuntimeValidationTab = validationTab;
  state.contextDockCollapsed = false;
  if (state.activeContextTab === "singleWeapons") {
    globalThis.activateSingleWeaponForEditing?.();
    return;
  }
  if (state.viewportMode === "weapon") globalThis.returnToCharacterViewport?.();
  renderContextDockState();
  if (typeof renderWorkflowBar === "function") renderWorkflowBar();
}

function openWorkflowInspectorTab(tab) {
  globalThis.leaveBaseAttributeWorkspace?.();
  globalThis.leaveBodyWorkspace?.();
  state.activeInspectorTab = ["properties", "diagnostics", "import"].includes(tab) ? tab : "import";
  // Focused weapon workbenches intentionally hide the generic inspector. A workflow
  // action targeting that inspector must leave focus mode or the click appears inert.
  if (!state.contextDockCollapsed) state.contextDockCollapsed = true;
  renderLayoutState();
  renderContextDockState();
  renderInspectorTabState();
  if (typeof renderWorkflowBar === "function") renderWorkflowBar();
  const target = state.activeInspectorTab === "import"
    ? el.importStatus
    : document.querySelector(`[data-inspector-panel="${state.activeInspectorTab}"]`);
  target?.scrollIntoView?.({ behavior: "smooth", block: "nearest", inline: "nearest" });
  if (target) {
    target.setAttribute("tabindex", "-1");
    target.focus({ preventScroll: true });
    target.classList.add("workflow-target-flash");
    window.setTimeout(() => target.classList.remove("workflow-target-flash"), 1200);
  }
}

Object.assign(globalThis, {
  ensureCharacterWeaponSlot,
  getGlobalConfiguration,
  setActiveGlobalConfiguration,
  applyGlobalConfigurationAction,
  updateGlobalConfigurationField,
  GLOBAL_CONFIGURATION_TYPES
});

window.CharacterStudioTest = {
  buildTree,
  normalizeIssuePath,
  editableFields,
  quaternionFromEulerDegrees,
  mapResourceKind,
  inferRuntimeBindingKind,
  evaluateResourceFieldSelection,
  buildFallbackResourcePlan,
  buildWorkflowView,
  buildWeaponVisualizationItems,
  getGeneratedReactionActionId,
  getGeneratedReactionProfileStableId,
  ...globalThis.SingleWeaponWorkbenchTest,
  ...globalThis.CombinedWeaponWorkbenchTest
};

window.addEventListener("unhandledrejection", event => {
  console.error("Unhandled rejection:", event.reason);
  const msg = event.reason?.message || String(event.reason || "未知异步操作异常");
  showToast(msg, "error", { title: "脚本异常" });
});

window.addEventListener("error", event => {
  console.error("Window error:", event.error || event.message);
  const msg = event.message || "前端脚本运行异常";
  showToast(msg, "error", { title: "页面异常" });
});
