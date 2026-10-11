// CharacterStudio split module. Registers functions on globalThis to preserve the original call graph.

const CONTEXT_TABS = ["binding", "singleWeapons", "combinedWeapons", "surfacePalette", "materials", "validation"];
const CONTEXT_EDITOR_TABS = new Set(["singleWeapons", "combinedWeapons", "surfacePalette", "materials", "validation"]);
const CONTEXT_TAB_LABELS = {
  binding: "绑定",
  singleWeapons: "单一武器配置",
  combinedWeapons: "组合武器配置",
  surfacePalette: "角色材质",
  materials: "原材料",
  validation: "运行验证"
};
const WORKFLOW_STAGE_DEFINITIONS = [
  { key: "sources", label: "源资产", stageIds: ["CharacterPackage"], contextTab: "materials" },
  { key: "resourceBinding", label: "资源绑定", stageIds: ["ResourceBinding"], contextTab: "binding" },
  { key: "motion", label: "武器/动画/受击/动作", stageIds: ["AnimationBinding", "ReactionBinding", "ActionTimeline"], contextTab: "validation" },
  { key: "unityImport", label: "Unity 导入", stageIds: ["UnityImport"], inspectorTab: "import" },
  { key: "runtime", label: "入游戏验证", stageIds: ["RuntimeValidation"], contextTab: "validation" }
];

function render() {
  renderShellStatus();
  renderLayoutState();
  renderSummary();
  renderTree();
  renderPreviewControls();
  renderViewportModeBar();
  renderResourceBindingBar();
  renderWeaponConfigurationPanel();
  renderImportMaterialsPanel();
  renderSurfacePaletteWorkbench?.();
  renderResourcePicker();
  renderAbilityLoadoutPanel();
  renderAnimationConfigPanel();
  renderReactionProfilePanel();
  renderResourcePlanPreview();
  renderReadinessPanel();
  renderContextDockState();
  renderViewport();
  renderInspector();
  renderDiagnostics();
  renderImportStatus();
  renderInspectorTabState();
  scheduleCharacterStudioSessionPersistence?.();
}

function renderLayoutState() {
  if (!el.workspace || !el.treeCollapseButton) return;
  const attributes = state.selectedPath === "config/attributes";
  el.workspace.classList.toggle("tactical-workspace", state.selectedPath === "config/tactical");
  const bodySection = globalThis.getBodyWorkspaceSection?.() || "";
  if (attributes || bodySection) state.viewportMode = "character";
  const contextExpanded = !attributes && !bodySection && !state.contextDockCollapsed && state.viewportMode !== "weapon";
  el.workspace.classList.toggle("attribute-workspace", attributes);
  el.workspace.classList.toggle("body-workspace", Boolean(bodySection));
  el.workspace.classList.toggle("body-parts-workspace", bodySection === "parts");
  globalThis.renderBodyWorkspace?.();
  const previewControls = document.getElementById("attributePreviewControls");
  if (previewControls) previewControls.hidden = !attributes;
  const overlayButton = document.getElementById("attributeOverlayButton");
  if (overlayButton) {
    overlayButton.textContent = state.attributePreviewOverlays ? "隐藏辅助标记" : "显示辅助标记";
    overlayButton.setAttribute("aria-pressed", String(Boolean(state.attributePreviewOverlays)));
  }
  el.workspace.classList.toggle("tree-collapsed", state.treeCollapsed);
  el.workspace.classList.toggle("weapon-focus-mode", state.viewportMode === "weapon");
  el.workspace.classList.toggle("context-expanded", contextExpanded);
  el.treeCollapseButton.textContent = state.treeCollapsed ? ">" : "<";
  el.treeCollapseButton.title = state.treeCollapsed ? "展开资源树" : "收起资源树";
  el.treeCollapseButton.setAttribute("aria-label", el.treeCollapseButton.title);
}

function renderContextDockState() {
  if (!el.contextDock) return;
  const activeTab = normalizeContextTab(state.activeContextTab);
  state.activeContextTab = activeTab;
  const singleWeaponPreview = activeTab === "singleWeapons" && state.viewportMode === "weapon";
  const contextExpanded = state.selectedPath !== "config/attributes"
    && !globalThis.isBodyWorkspacePath?.()
    && !state.contextDockCollapsed && (state.viewportMode !== "weapon" || singleWeaponPreview);
  const contextEditorExpanded = contextExpanded && CONTEXT_EDITOR_TABS.has(activeTab)
    && !(activeTab === "singleWeapons" && singleWeaponPreview);
  document.body.classList.toggle("weapon-authoring-focus",
    singleWeaponPreview || (contextEditorExpanded && (activeTab === "singleWeapons" || activeTab === "combinedWeapons")));
  el.workspace?.classList.toggle("context-expanded", contextExpanded);
  el.workspace?.classList.toggle("context-editor-expanded", contextEditorExpanded);
  el.contextDock.classList.toggle("context-editor-expanded", !state.contextDockCollapsed && CONTEXT_EDITOR_TABS.has(activeTab));
  for (const tab of CONTEXT_TABS) {
    el.workspace?.classList.toggle(`context-tab-${tab}`, contextExpanded && activeTab === tab);
    el.contextDock.classList.toggle(`context-tab-${tab}`, !state.contextDockCollapsed && activeTab === tab);
  }
  el.contextDock.classList.toggle("is-collapsed", state.contextDockCollapsed);
  el.contextDockSummary.innerHTML = renderContextDockSummary();

  el.contextDockToggleButton.textContent = state.contextDockCollapsed ? "展开" : "收起";
  el.contextDockToggleButton.setAttribute("aria-expanded", String(!state.contextDockCollapsed));
  el.contextDockToggleButton.title = state.contextDockCollapsed ? "展开上下文面板" : "收起上下文面板";

  el.contextDockTabs?.querySelectorAll("button[data-context-tab]").forEach(button => {
    const isActive = button.dataset.contextTab === activeTab;
    button.classList.toggle("active", isActive);
    button.setAttribute("aria-selected", String(isActive));
  });
  el.contextDockBody?.querySelectorAll("[data-context-panel]").forEach(panel => {
    const isActive = panel.dataset.contextPanel === activeTab && !state.contextDockCollapsed;
    panel.hidden = !isActive;
    panel.classList.toggle("active", isActive);
  });
  renderRuntimeValidationTabs();
}

function renderRuntimeValidationTabs() {
  const allowed = ["animation", "ability", "reaction", "resources", "readiness"];
  const active = allowed.includes(state.activeRuntimeValidationTab) ? state.activeRuntimeValidationTab : "ability";
  state.activeRuntimeValidationTab = active;
  el.contextDockBody?.querySelectorAll("button[data-validation-tab]").forEach(button => {
    const selected = button.dataset.validationTab === active;
    button.classList.toggle("active", selected);
    button.setAttribute("aria-selected", String(selected));
  });
  el.contextDockBody?.querySelectorAll("[data-validation-panel]").forEach(panel => {
    panel.hidden = panel.dataset.validationPanel !== active;
  });
}

function normalizeContextTab(tab) {
  return CONTEXT_TABS.includes(tab) ? tab : "binding";
}

function renderContextDockSummary() {
  const binding = getResourceBindingView();
  const activeTab = normalizeContextTab(state.activeContextTab);
  const tabLabel = CONTEXT_TAB_LABELS[activeTab] || activeTab;
  const selectedPath = state.selectedPath || "overview";
  return `
    <strong title="${escapeHtml(tabLabel)}">${escapeHtml(tabLabel)}</strong>
    <span title="${escapeHtml(binding.fieldText)}">${escapeHtml(binding.fieldText)}</span>
    <span title="${escapeHtml(selectedPath)}">${escapeHtml(selectedPath)}</span>`;
}

function getPrimaryReadinessIssue(snapshot) {
  if (!snapshot) return null;
  const directIssues = [
    ...(Array.isArray(snapshot.blockers) ? snapshot.blockers : []),
    ...(Array.isArray(snapshot.warnings) ? snapshot.warnings : [])
  ];
  if (directIssues.length) return directIssues[0];
  const stages = sortReadinessStages(Array.isArray(snapshot.stages) ? snapshot.stages : []);
  for (const stage of stages) {
    const stageIssues = [
      ...(Array.isArray(stage?.blockers) ? stage.blockers : []),
      ...(Array.isArray(stage?.warnings) ? stage.warnings : [])
    ];
    if (stageIssues.length) return stageIssues[0];
  }
  return null;
}

function renderWorkflowBar() {
  if (!el.workflowBar) return;
  const view = buildWorkflowView();
  const summaryTitle = `${view.packageId} / ${view.overallStatus}`;
  el.workflowBar.innerHTML = `
    <div class="workflow-summary" title="${escapeHtml(summaryTitle)}">
      <strong>${escapeHtml(view.packageId)}</strong>
      <span class="badge ${escapeHtml(view.overallTone)}">${escapeHtml(formatWorkflowStatusLabel(view.overallStatus))}</span>
      <span>${escapeHtml(String(view.blockers))} 阻塞 / ${escapeHtml(String(view.warnings))} 警告</span>
    </div>
    <div class="workflow-stages" aria-label="角色创建阶段">
      ${view.stages.map(renderWorkflowStageChip).join("")}
    </div>
    <div class="workflow-next" title="${escapeHtml(view.nextAction.title || view.nextAction.detail || "")}">
      <span>下一步</span>
      <strong>${escapeHtml(view.nextAction.detail || view.nextAction.label)}</strong>
      ${renderWorkflowActionButton(view.nextAction, "workflow-primary-action")}
    </div>
    ${view.issueCards.length ? `<div class="workflow-issue-strip" aria-label="当前可处理问题">${view.issueCards.slice(0, 1).map(card => renderWorkflowIssueCard(card, { compact: true })).join("")}</div>` : ""}`;
}

function buildWorkflowView() {
  const snapshot = state.readiness;
  const packageId = snapshot?.packageId || state.package?.manifest?.packageId || "character";
  const blockerRecords = collectWorkflowIssueRecords(snapshot, "blocker");
  const warningRecords = collectWorkflowIssueRecords(snapshot, "warning");
  const stages = WORKFLOW_STAGE_DEFINITIONS.map(definition => buildWorkflowStageView(definition, snapshot, {
    blockerRecords,
    warningRecords
  }));
  const blockers = blockerRecords.length;
  const warnings = warningRecords.length;
  const overallStatus = snapshot?.overallStatus || (state.package ? "Pending" : "Loading");
  const view = {
    packageId,
    overallStatus,
    overallTone: getReadinessTone(overallStatus),
    blockers,
    warnings,
    stages,
    issueCards: buildWorkflowIssueCards(blockerRecords, warningRecords),
    nextAction: null
  };
  view.nextAction = buildWorkflowNextAction({ snapshot, stages, blockerRecords, warningRecords });
  return view;
}

function buildWorkflowStageView(definition, snapshot, options = {}) {
  const readinessStages = sortReadinessStages(Array.isArray(snapshot?.stages) ? snapshot.stages : [])
    .filter(stage => definition.stageIds.includes(stage?.id || ""));
  const effectiveBlockers = (options.blockerRecords || []).filter(record => definition.stageIds.includes(record?.stageId || ""));
  const effectiveWarnings = (options.warningRecords || []).filter(record => definition.stageIds.includes(record?.stageId || ""));
  const preferredStageId = effectiveBlockers[0]?.stageId || effectiveWarnings[0]?.stageId || "";
  const focusStage = readinessStages.find(stage => stage?.id === preferredStageId)
    || readinessStages.find(stage => getWorkflowStageTone(stage?.status) !== "ok")
    || readinessStages[0]
    || null;
  let status = combineWorkflowStageStatus(readinessStages);
  const rawIssueCount = readinessStages.reduce((count, stage) =>
    count + (stage?.blockers?.length || 0) + (stage?.warnings?.length || 0), 0);
  if (effectiveBlockers.length) {
    status = "Blocked";
  } else if (effectiveWarnings.length) {
    status = "Warning";
  } else if (rawIssueCount > 0 && getWorkflowStageTone(status) === "error") {
    status = definition.key === "runtime" ? "Deferred" : "Ready";
  }
  if (definition.key === "sources" && (state.importMaterialsDirty || globalThis.hasUnsavedImportMaterialsChanges?.())) {
    status = "Warning";
  } else if (definition.key === "resourceBinding" && state.dirty) {
    status = "Warning";
  }
  const target = getWorkflowActionTargetForStage(focusStage?.id || definition.stageIds[0], focusStage);
  const active = isWorkflowStageActive(definition, target);
  const evidence = definition.key === "unityImport"
    ? getUnityImportEvidenceView(focusStage, snapshot)
    : null;
  return {
    key: definition.key,
    id: focusStage?.id || definition.stageIds[0],
    label: definition.label,
    status,
    statusLabel: definition.key === "unityImport" && evidence?.source === "none"
      && ["pending", "warn"].includes(evidence?.tone) && !effectiveBlockers.length && !effectiveWarnings.length
      ? "待导入 Unity" : "",
    tone: getWorkflowStageTone(status),
    active,
    action: target,
    title: evidence?.detail
      || (rawIssueCount > 0 && !effectiveBlockers.length && !effectiveWarnings.length
        ? (definition.key === "runtime" ? "等待前置创作流程完成。" : `${definition.label}本身没有独立阻断项。`)
        : focusStage?.summary)
      || definition.label,
    evidenceLabel: evidence?.label || ""
  };
}

function combineWorkflowStageStatus(stages) {
  if (!stages.length) return "Pending";
  const statuses = stages.map(stage => String(stage?.status || "Pending"));
  if (statuses.some(status => getWorkflowStageTone(status) === "error")) return "Blocked";
  if (statuses.some(status => getWorkflowStageTone(status) === "warn")) return "Warning";
  if (statuses.every(status => getWorkflowStageTone(status) === "ok")) return "Ready";
  return statuses[0] || "Pending";
}

function isWorkflowStageActive(definition, target) {
  if (state.selectedPath === "config/attributes" || globalThis.isBodyWorkspacePath?.()) return false;
  if (target?.inspectorTab && state.activeInspectorTab === target.inspectorTab) return true;
  return Boolean(target?.contextTab && state.activeContextTab === target.contextTab);
}

function buildWorkflowNextAction({ snapshot, blockerRecords, warningRecords }) {
  const unityImportRunning = Boolean(state.unityImportTask?.running);
  const packageDirty = Boolean(state.dirty);
  const sourceManifestDirty = Boolean(state.importMaterialsDirty || globalThis.hasUnsavedImportMaterialsChanges?.());
  const weaponLibraryDirty = Boolean(state.weaponLibraryDirty);
  if (unityImportRunning) {
    return {
      action: "open-inspector",
      inspectorTab: "import",
      label: "查看进度",
      detail: "Unity 导入正在执行",
      title: "查看 Unity import task 和 Editor bridge 状态"
    };
  }
  if (packageDirty || sourceManifestDirty || weaponLibraryDirty) {
    return {
      action: "save",
      label: "保存",
      detail: weaponLibraryDirty ? "保存全局单一武器库和角色草稿" : packageDirty && sourceManifestDirty ? "保存 package 和源清单" : packageDirty ? "保存 package 改动" : "保存源清单改动",
      title: "先保存当前改动，再继续预检或 Unity 导入",
      disabled: !state.canWrite || !state.package
    };
  }
  if (!snapshot) {
    return {
      action: "reload",
      label: "刷新",
      detail: "读取 readiness 状态",
      title: "重新读取 Authoring service 的角色 readiness"
    };
  }
  if (blockerRecords.length) return buildWorkflowIssueAction(blockerRecords[0], "blocker");
  const unityStage = getReadinessStage("UnityImport");
  if (shouldOfferDeferredUnityImport(unityStage, { importBlocked: isImportBlocked() })) {
    return {
      action: "import",
      label: "Unity 导入",
      detail: "执行 Unity 导入验收",
      title: getUnityImportEvidenceView(unityStage, snapshot).detail
    };
  }
  if (warningRecords.length) return buildWorkflowIssueAction(warningRecords[0], "warning");
  if (!state.apiAvailable) {
    return {
      action: "reload",
      label: "刷新",
      detail: "连接 Authoring 服务",
      title: "Authoring service 不可用，刷新后重试"
    };
  }
  if (unityStage?.status !== "Ready" && !isImportBlocked()) {
    return {
      action: "import",
      label: "Unity 导入",
      detail: "继续 Unity 导入",
      title: "生成 Unity 资产并刷新 Prefab / CharacterAction"
    };
  }
  return {
    action: "open-context",
    contextTab: "validation",
    validationTab: "readiness",
    label: "查看",
    detail: "查看入游戏状态",
    title: "打开完整 readiness 阶段列表"
  };
}

function shouldOfferDeferredUnityImport(stage, options = {}) {
  return !options.hasBlockers
    && !options.importBlocked
    && String(stage?.status || "").toLowerCase() === "deferred";
}

function buildWorkflowIssueAction(record, severity) {
  const description = describeWorkflowIssue(record, severity);
  const target = getWorkflowActionTargetForStage(record.stageId, record.stage);
  const canLocate = record.path && record.path !== "validation";
  const detail = `${description.stageLabel}: ${description.title}`;
  const title = description.impact || description.message || detail;
  if (canLocate) {
    return {
      ...target,
      action: "locate",
      path: record.path,
      label: description.primaryLabel || (severity === "blocker" ? "定位阻塞" : "定位警告"),
      detail,
      title
    };
  }
  return {
    ...target,
    label: description.primaryLabel || `打开${getWorkflowStageActionLabel(record.stageId)}`,
    detail,
    title
  };
}

function buildWorkflowIssueCards(blockerRecords = [], warningRecords = [], limit = 2) {
  const cards = [
    ...blockerRecords.map(record => ({ record, severity: "blocker" })),
    ...warningRecords.map(record => ({ record, severity: "warning" }))
  ].map(item => {
    const description = describeWorkflowIssue(item.record, item.severity);
    return {
      ...description,
      severity: item.severity,
      tone: item.severity === "blocker" ? "error" : "warn",
      action: buildWorkflowIssueAction(item.record, item.severity)
    };
  });
  const seen = new Set();
  const unique = [];
  for (const card of cards) {
    const key = [
      card.severity,
      card.code || "",
      card.title || "",
      card.impact || "",
      card.technicalPath || ""
    ].join("|");
    if (seen.has(key)) continue;
    seen.add(key);
    unique.push(card);
    if (unique.length >= limit) break;
  }
  return unique;
}

function describeWorkflowIssue(record = {}, severity = "warning") {
  const stageId = record.stageId || inferWorkflowIssueStageId(record.issue || {}, "");
  const code = String(record.code || record.issue?.code || "").trim();
  const message = String(record.message || record.issue?.message || "").trim();
  const stageLabel = getWorkflowStageLabel(stageId);
  const severityLabel = severity === "blocker" ? "阻塞" : "需处理";
  const fallbackTitle = stageLabel ? `${stageLabel}${severityLabel}` : `问题${severityLabel}`;
  const base = {
    title: fallbackTitle,
    stageLabel,
    module: stageLabel || "角色创建",
    impact: message || "该检查项需要处理后，角色创建流程才能继续。",
    message,
    code: code || severityLabel,
    primaryLabel: getWorkflowIssuePrimaryLabel(stageId, severity),
    technicalPath: record.path || "",
    suggestedFix: String(record.issue?.suggestedFix || "").trim()
  };
  if (code === "COMBINED_WEAPON_CONFIGURATION_MISSING") {
    return {
      ...base,
      title: "需要创建组合武器配置",
      stageLabel: "组合武器",
      module: "组合武器配置",
      impact: "角色源模型可以预览，但尚未声明主手与副手配方，因此还不能生成可运行角色。",
      primaryLabel: "去组合武器",
      technicalPath: "config/character_application.json · combinedWeaponConfigurations"
    };
  }
  if (code === "COMBINED_WEAPON_CONFIGURATION_DEFAULT_INVALID") {
    return {
      ...base,
      title: "默认组合武器配置无效",
      stageLabel: "组合武器",
      module: "组合武器配置",
      impact: "默认配置必须指向一个已经存在的组合武器配置。",
      primaryLabel: "去组合武器",
      technicalPath: "config/character_application.json · defaultCombinedWeaponConfigurationId"
    };
  }
  if (code === "ANIMATION_AUTHORING_MISSING") {
    return {
      ...base,
      title: "缺少动画创作配置",
      module: "动画 / AnimationEditor",
      impact: "角色包尚未生成可绑定的 animation_authoring.json；已提取的动作源文件仍需转换和配置。",
      primaryLabel: "去动画配置"
    };
  }
  if (code === "CHARPKG_MISSING_PREVIEW_RESOURCE") {
    return {
      ...base,
      title: "缺少正式预览资源",
      module: "资源目录",
      impact: "当前 3D 源模型仍可预览；此警告只表示尚未登记供列表和外部工具使用的轻量缩略图或预览网格。",
      primaryLabel: "去资源绑定",
      technicalPath: "resource_catalog.json · preview"
    };
  }
  if (code === "GLOBAL_REACTION_CONFIGURATION_NOT_FOUND") {
    return {
      ...base,
      title: "缺少全局受击配置",
      module: "受击反应",
      impact: "当前角色尚未引用可用的全局受击配置；这不会阻止源模型预览，但会影响运行时受击表现。",
      primaryLabel: "去受击配置"
    };
  }
  if (code === "ABILITY_ACTION_BINDING_MISSING" || /ability.*action.*binding/i.test(`${code} ${message}`)) {
    return {
      ...base,
      title: "历史能力动作映射",
      module: "高级兼容诊断",
      impact: "这是冻结能力数据的只读兼容信息，不参与角色 Ready 或下一步。",
      primaryLabel: "查看高级兼容诊断"
    };
  }
  if (code === "ABILITY_RUNTIME_BINDING_DEFERRED" || /ability.*registry|runtime.*binding.*deferred|bindingcatalog/i.test(`${code} ${message}`)) {
    return {
      ...base,
      title: "历史能力运行时兼容信息",
      module: "高级兼容诊断",
      impact: "冻结能力数据尚无正式 Runtime 契约；该信息不阻止角色创建和导入。",
      primaryLabel: "查看高级兼容诊断"
    };
  }
  if (/reaction/i.test(`${code} ${message}`)) {
    return {
      ...base,
      title: "受击反应配置需要处理",
      module: "受击 / CharacterAction",
      impact: message || "受击槽位或反应 Profile 不完整，运行时受击表现可能缺失。",
      primaryLabel: "去受击配置"
    };
  }
  if (/animation/i.test(`${code} ${message}`)) {
    return {
      ...base,
      title: "动画绑定需要处理",
      module: "动画 / AnimationEditor",
      impact: message || "动画 Profile 或槽位绑定不完整，Unity 导入和运行时动作可能缺少动画资源。",
      primaryLabel: "去动画配置"
    };
  }
  if (/resource|model|source|body|texture/i.test(`${code} ${message}`)) {
    return {
      ...base,
      title: "资源绑定需要处理",
      module: "源文件 / Resource Manager",
      impact: message || "角色本地源文件或公共资源引用不完整，Unity 导入可能缺少资产。",
      primaryLabel: "去资源绑定"
    };
  }
  if (/unity|import|prefab|avatar/i.test(`${code} ${message}`)) {
    return {
      ...base,
      title: "Unity 导入需要处理",
      module: "Unity 导入",
      impact: message || "Unity 侧生成资产或 Prefab 状态未就绪，入游戏验证还不能完成。",
      primaryLabel: "看导入状态"
    };
  }
  return base;
}

function getWorkflowIssuePrimaryLabel(stageId, severity = "warning") {
  if (stageId === "ActionTimeline") return "去组合动作";
  if (stageId === "AbilityRuntimeBinding") return "查看高级兼容诊断";
  if (stageId === "ReactionBinding") return "去受击配置";
  if (stageId === "AnimationBinding") return "去动画配置";
  if (stageId === "UnityImport") return "看导入状态";
  if (stageId === "RuntimeValidation") return "看入游戏状态";
  if (stageId === "ResourceBinding" || stageId === "CharacterPackage") return "去资源绑定";
  return severity === "blocker" ? "定位阻塞" : "定位问题";
}

function renderWorkflowIssueCard(card, options = {}) {
  const compact = Boolean(options.compact);
  const meta = [
    card.module ? `模块：${card.module}` : "",
    card.code ? `技术码：${card.code}` : "",
    card.technicalPath ? `位置：${card.technicalPath}` : ""
  ].filter(Boolean);
  return `<article class="workflow-issue-card ${escapeHtml(card.tone || "warn")}${compact ? " compact" : ""}">
    <div>
      <span class="badge ${escapeHtml(card.tone || "warn")}">${escapeHtml(card.severity === "blocker" ? "阻塞" : "需处理")}</span>
      <strong>${escapeHtml(card.title || "需要处理的问题")}</strong>
    </div>
    <p>${escapeHtml(card.impact || card.message || "该问题会影响角色创建流程。")}</p>
    ${card.suggestedFix ? `<p class="workflow-issue-fix"><strong>建议：</strong>${escapeHtml(card.suggestedFix)}</p>` : ""}
    ${meta.length ? `<div class="workflow-issue-meta">${meta.map(item => `<span title="${escapeHtml(item)}">${escapeHtml(item)}</span>`).join("")}</div>` : ""}
    ${renderWorkflowActionButton(card.action, "workflow-issue-action")}
  </article>`;
}

function collectWorkflowIssueRecords(snapshot, severity) {
  if (!snapshot) return [];
  const records = [];
  const seen = new Set();
  const stages = sortReadinessStages(Array.isArray(snapshot.stages) ? snapshot.stages : []);
  const addRecord = (issue, fallbackStageId = "", stage = null) => {
    if (!issue) return;
    const stageId = inferWorkflowIssueStageId(issue, fallbackStageId);
    const path = normalizeWorkflowIssuePath(issue);
    const code = String(issue?.code || issue?.severity || "");
    const message = String(issue?.message || "");
    const key = `${severity}|${code}|${message}|${String(issue?.sourcePath || "")}`;
    if (seen.has(key)) return;
    seen.add(key);
    records.push({ issue, stage, stageId, path, code, message });
  };
  for (const stage of stages) {
    const issues = severity === "blocker"
      ? (Array.isArray(stage?.blockers) ? stage.blockers : [])
      : (Array.isArray(stage?.warnings) ? stage.warnings : []);
    issues.forEach(issue => addRecord(issue, stage?.id || "", stage));
  }
  const directIssues = severity === "blocker"
    ? (Array.isArray(snapshot.blockers) ? snapshot.blockers : [])
    : (Array.isArray(snapshot.warnings) ? snapshot.warnings : []);
  directIssues.forEach(issue => addRecord(issue));
  return suppressDependentWorkflowIssues(records);
}

function countWorkflowIssues(snapshot, severity) {
  return collectWorkflowIssueRecords(snapshot, severity).length;
}

function suppressDependentWorkflowIssues(records = []) {
  const hasMissingCombinedWeaponConfiguration = records.some(record =>
    record?.code === "COMBINED_WEAPON_CONFIGURATION_MISSING");
  const hasUnityImportRootBlocker = records.some(record =>
    record?.stageId === "UnityImport"
    && record?.code !== "RUNTIME_VALIDATION_IMPORT_FAILED");
  return records.filter(record => {
    if (hasMissingCombinedWeaponConfiguration
      && record?.code === "COMBINED_WEAPON_CONFIGURATION_DEFAULT_INVALID") return false;
    if (hasUnityImportRootBlocker
      && record?.code === "RUNTIME_VALIDATION_IMPORT_FAILED") return false;
    return true;
  });
}

function inferWorkflowIssueStageId(issue, fallbackStageId = "") {
  const explicit = firstNonEmpty(issue?.stageId, issue?.stage, issue?.readinessStage, issue?.sourceStageId);
  if (explicit) return explicit;
  const haystack = `${issue?.code || ""} ${issue?.sourceObjectPath || ""} ${issue?.sourcePath || ""}`.toLowerCase();
  if (haystack.includes("combined_weapon_configuration") || haystack.includes("combinedweaponconfiguration")) return "ActionTimeline";
  if (haystack.includes("ability")) return "AbilityRuntimeBinding";
  if (haystack.includes("reaction")) return "ReactionBinding";
  if (haystack.includes("animation")) return "AnimationBinding";
  if (haystack.includes("action")) return "ActionTimeline";
  if (haystack.includes("unity") || haystack.includes("import") || haystack.includes("prefab") || haystack.includes("avatar")) return "UnityImport";
  if (haystack.includes("runtime") || haystack.includes("spawn")) return "RuntimeValidation";
  if (haystack.includes("resource") || haystack.includes("model") || haystack.includes("body") || haystack.includes("source")) return "ResourceBinding";
  return fallbackStageId || "CharacterPackage";
}

function normalizeWorkflowIssuePath(issue) {
  const raw = firstNonEmpty(issue?.sourceObjectPath, issue?.sourcePath);
  if (!raw) return "";
  return typeof normalizeIssuePath === "function" ? normalizeIssuePath(raw) : raw;
}

function getWorkflowActionTargetForStage(stageId, stage = null) {
  if (stageId === "CharacterPackage") return { action: "open-context", contextTab: "materials" };
  if (stageId === "ResourceBinding") return { action: "open-context", contextTab: "binding" };
  if (stageId === "AnimationBinding") return { action: "open-context", contextTab: "validation", validationTab: "animation" };
  if (stageId === "ReactionBinding") return { action: "open-context", contextTab: "validation", validationTab: "reaction" };
  if (stageId === "ActionTimeline") return { action: "open-context", contextTab: "combinedWeapons" };
  if (stageId === "AbilityRuntimeBinding") return { action: "open-context", contextTab: "validation", validationTab: "ability" };
  if (stageId === "UnityImport") return { action: "open-inspector", inspectorTab: "import" };
  if (stageId === "RuntimeValidation") return { action: "open-context", contextTab: "validation", validationTab: "readiness" };
  if (stage?.primaryToolUrl) return { action: "open-url", url: stage.primaryToolUrl };
  return { action: "open-context", contextTab: "validation" };
}

function getWorkflowStageLabel(stageId) {
  const definition = WORKFLOW_STAGE_DEFINITIONS.find(item => item.stageIds.includes(stageId));
  return definition?.label || stageId || "流程";
}

function getWorkflowStageActionLabel(stageId) {
  if (stageId === "CharacterPackage") return "原材料";
  if (stageId === "ResourceBinding") return "绑定";
  if (stageId === "AnimationBinding") return "动画";
  if (stageId === "ReactionBinding") return "受击";
  if (stageId === "ActionTimeline") return "动作";
  if (stageId === "AbilityRuntimeBinding") return "高级兼容诊断";
  if (stageId === "UnityImport") return "Unity 导入";
  if (stageId === "RuntimeValidation") return "入游戏";
  return "入游戏";
}

function renderWorkflowStageChip(stage) {
  return `<button type="button" class="workflow-stage ${escapeHtml(stage.tone)}${stage.active ? " active" : ""}" ${renderWorkflowActionAttributes(stage.action)} title="${escapeHtml(stage.title || stage.label)}">
    <span>${escapeHtml(stage.label)}</span>
    <strong>${escapeHtml(stage.statusLabel || formatWorkflowStatusLabel(stage.status, stage.id))}</strong>
    ${stage.evidenceLabel ? `<small>${escapeHtml(stage.evidenceLabel)}</small>` : ""}
  </button>`;
}

function renderWorkflowActionButton(action, className = "") {
  const disabled = action.disabled ? " disabled" : "";
  const title = action.title || action.detail || action.label || "";
  return `<button type="button" class="${escapeHtml(className)}" ${renderWorkflowActionAttributes(action)} title="${escapeHtml(title)}"${disabled}>${escapeHtml(action.label || "打开")}</button>`;
}

function renderWorkflowActionAttributes(action = {}) {
  const attrs = [`data-workflow-action="${escapeHtml(action.action || "open-context")}"`];
  if (action.contextTab) attrs.push(`data-context-tab="${escapeHtml(action.contextTab)}"`);
  if (action.validationTab) attrs.push(`data-validation-tab="${escapeHtml(action.validationTab)}"`);
  if (action.inspectorTab) attrs.push(`data-inspector-tab="${escapeHtml(action.inspectorTab)}"`);
  if (action.path) attrs.push(`data-workflow-path="${escapeHtml(action.path)}"`);
  if (action.url) attrs.push(`data-url="${escapeHtml(action.url)}"`);
  return attrs.join(" ");
}

function getWorkflowStageTone(status) {
  const tone = getReadinessTone(status);
  return tone === "pending" ? "pending" : tone;
}

function formatWorkflowStatusLabel(status, stageId = "") {
  const value = String(status || "").toLowerCase();
  if (value === "ready") return "就绪";
  if (value === "blocked") return "阻塞";
  if (value === "warning") return "需处理";
  if (value === "deferred") return stageId === "UnityImport" ? "待导入 Unity" : "待继续";
  if (value === "loading") return "加载中";
  if (value === "pending") return "等待";
  if (value === "unknown") return "未知";
  return status || "等待";
}

function renderInspectorTabState() {
  if (!el.inspectorTabs) return;
  const activeTab = ["properties", "diagnostics", "import"].includes(state.activeInspectorTab)
    ? state.activeInspectorTab
    : "properties";
  state.activeInspectorTab = activeTab;
  el.inspectorTabs.querySelectorAll("button[data-inspector-tab]").forEach(button => {
    const isActive = button.dataset.inspectorTab === activeTab;
    button.classList.toggle("active", isActive);
    button.setAttribute("aria-selected", String(isActive));
  });
  document.querySelectorAll("[data-inspector-panel]").forEach(panel => {
    const isActive = panel.dataset.inspectorPanel === activeTab;
    panel.hidden = !isActive;
    panel.classList.toggle("active", isActive);
  });
}

function renderShellStatus() {
  const unityImportRunning = Boolean(state.unityImportTask?.running);
  const packageDirty = Boolean(state.dirty);
  const bodyPartDamageDirty = Boolean(state.bodyPartDamageDirty);
  const sourceManifestDirty = Boolean(state.importMaterialsDirty);
  const weaponLibraryDirty = Boolean(state.weaponLibraryDirty);
  const buildTargetMissing = !state.unityImportBuildTarget;
  const anyDirty = packageDirty || bodyPartDamageDirty || sourceManifestDirty || weaponLibraryDirty;
  const dirtyParts = [];
  if (packageDirty) dirtyParts.push("角色草稿");
  if (bodyPartDamageDirty) dirtyParts.push("部位伤害草稿");
  if (weaponLibraryDirty) dirtyParts.push("全局武器库");
  if (sourceManifestDirty) dirtyParts.push("源清单");
  const dirtyLabel = dirtyParts.length ? `${dirtyParts.join(" + ")}未保存` : "源数据已保存";
  el.subtitle.textContent = state.message || "角色资源包外部装配工作台。";
  el.dirtyBadge.textContent = dirtyLabel;
  el.dirtyBadge.className = `badge ${anyDirty ? "warn" : "ok"}`;
  el.saveButton.disabled = unityImportRunning || !state.canWrite || !state.package;
  el.resourceBindingRole.disabled = !state.package;
  el.configCreateSelect.disabled = !state.canWrite || !state.package;
  el.configCreateButton.disabled = !state.canWrite || !state.package;
  el.openResourcePickerButton.disabled = !state.package || (el.resourceBindingRole?.value || "preview") === "preview";
  el.clearModelBindingButton.disabled = !state.canWrite || !state.package || el.resourceBindingRole.value === "preview";
  el.compileButton.disabled = unityImportRunning || !state.apiAvailable || !state.package || anyDirty;
  el.compileButton.title = unityImportRunning
    ? "Unity 导入正在运行。"
    : anyDirty
      ? "Prefab 重建预检前请先保存全部角色、原材料、部位伤害和全局武器草稿。"
      : "运行 Character compile，预检当前已保存版本的 Prefab 重建输入。";
  el.unityBuildTargetSelect.disabled = unityImportRunning;
  el.importButton.disabled = unityImportRunning || !state.apiAvailable || !state.package || anyDirty || buildTargetMissing || isImportBlocked();
  el.importButton.textContent = unityImportRunning ? "Unity 导入中..." : "Unity 导入";
  el.importButton.title = unityImportRunning
    ? "正在同步生成的 Unity 资产、重建 Prefab，并刷新 CharacterAction 状态。"
    : anyDirty
      ? "Unity 导入前请先保存 package JSON 和角色源清单改动。"
      : buildTargetMissing
        ? "请先选择本次 Unity 导入使用的目标平台。"
        : "导入生成的 Unity 资产，并请求 Unity Editor 重建 Prefab、刷新 CharacterAction 状态。";
  el.importButton.classList.toggle("is-running", unityImportRunning);
  el.importButton.setAttribute("aria-busy", String(unityImportRunning));
  updateResourceBindingControls();
  renderUndoRedoState();
  renderWorkflowBar();
  scheduleCharacterStudioSessionPersistence?.();
}

function renderUndoRedoState() {
  if (el.undoButton) el.undoButton.disabled = !canUndo();
  if (el.redoButton) el.redoButton.disabled = state.redoStack.length === 0;
}

function canUndo() {
  return state.undoStack.length > 0 || hasPendingUndoTransactionChange();
}

function hasPendingUndoTransactionChange() {
  const tx = state.activeUndoTransaction;
  return Boolean(tx && (
    (tx.afterPackage && tx.afterPackage !== tx.beforePackage)
    || (tx.afterBodyPartDamage && tx.afterBodyPartDamage !== tx.beforeBodyPartDamage)
    || (tx.afterWeaponLibrary && tx.afterWeaponLibrary !== tx.beforeWeaponLibrary)));
}

function handleUndoRedoKeydown(event) {
  const key = String(event.key || "").toLowerCase();
  const modifier = event.ctrlKey || event.metaKey;
  if (!modifier) return;
  if (key === "z" && !event.shiftKey) {
    event.preventDefault();
    undoLastEdit();
  } else if ((key === "y" && !event.shiftKey) || (key === "z" && event.shiftKey)) {
    event.preventDefault();
    redoLastEdit();
  }
}

function resetUndoHistory() {
  state.undoStack = [];
  state.redoStack = [];
  state.activeUndoTransaction = null;
  state.cleanPackageJson = serializePackage(state.package);
  state.cleanBodyPartDamageJson = serializeBodyPartDamageDocument(state.bodyPartDamage);
  refreshDirtyFromCleanSnapshot();
  renderUndoRedoState();
}

function beginUndoTransaction(label, key = "") {
  if (!state.package) return null;
  const normalizedKey = String(key || "");
  if (state.activeUndoTransaction) {
    if (!normalizedKey || state.activeUndoTransaction.key === normalizedKey) {
      return state.activeUndoTransaction;
    }
    commitUndoTransaction();
  }
  state.activeUndoTransaction = {
    label: label || "Edit",
    key: normalizedKey,
    beforePackage: serializePackage(state.package),
    beforeBodyPartDamage: serializeBodyPartDamageState(state.bodyPartDamage),
    beforeBodyPartDamageDirty: Boolean(state.bodyPartDamageDirty),
    beforeWeaponLibrary: serializePackage(state.weaponLibrary),
    beforeWeaponLibraryDirty: Boolean(state.weaponLibraryDirty),
    beforeContext: captureUndoContext(),
    afterPackage: "",
    afterBodyPartDamage: "",
    afterBodyPartDamageDirty: false,
    afterWeaponLibrary: "",
    afterWeaponLibraryDirty: false,
    afterContext: null
  };
  return state.activeUndoTransaction;
}

function updateUndoTransactionAfter() {
  const tx = state.activeUndoTransaction;
  if (!tx || !state.package) return;
  tx.afterPackage = serializePackage(state.package);
  tx.afterBodyPartDamage = serializeBodyPartDamageState(state.bodyPartDamage);
  tx.afterBodyPartDamageDirty = Boolean(state.bodyPartDamageDirty);
  tx.afterWeaponLibrary = serializePackage(state.weaponLibrary);
  tx.afterWeaponLibraryDirty = Boolean(state.weaponLibraryDirty);
  tx.afterContext = captureUndoContext();
}

function commitUndoTransaction() {
  const tx = state.activeUndoTransaction;
  if (!tx) return false;
  updateUndoTransactionAfter();
  state.activeUndoTransaction = null;
  const packageChanged = tx.afterPackage && tx.afterPackage !== tx.beforePackage;
  const bodyPartDamageChanged = tx.afterBodyPartDamage !== tx.beforeBodyPartDamage;
  const weaponLibraryChanged = tx.afterWeaponLibrary && tx.afterWeaponLibrary !== tx.beforeWeaponLibrary;
  if (packageChanged || bodyPartDamageChanged || weaponLibraryChanged) {
    state.undoStack.push({
      label: tx.label,
      beforePackage: tx.beforePackage,
      afterPackage: tx.afterPackage,
      beforeBodyPartDamage: tx.beforeBodyPartDamage,
      afterBodyPartDamage: tx.afterBodyPartDamage,
      beforeBodyPartDamageDirty: tx.beforeBodyPartDamageDirty,
      afterBodyPartDamageDirty: tx.afterBodyPartDamageDirty,
      beforeWeaponLibrary: tx.beforeWeaponLibrary,
      afterWeaponLibrary: tx.afterWeaponLibrary,
      beforeWeaponLibraryDirty: tx.beforeWeaponLibraryDirty,
      afterWeaponLibraryDirty: tx.afterWeaponLibraryDirty,
      beforeContext: tx.beforeContext,
      afterContext: tx.afterContext || captureUndoContext()
    });
    if (state.undoStack.length > UNDO_HISTORY_LIMIT) {
      state.undoStack.splice(0, state.undoStack.length - UNDO_HISTORY_LIMIT);
    }
    state.redoStack = [];
    refreshDirtyFromCleanSnapshot();
    renderUndoRedoState();
    return true;
  }
  refreshDirtyFromCleanSnapshot();
  renderUndoRedoState();
  return false;
}

function cancelUndoTransaction() {
  state.activeUndoTransaction = null;
  renderUndoRedoState();
}

function undoLastEdit() {
  if (state.activeUndoTransaction) commitUndoTransaction();
  const item = state.undoStack.pop();
  if (!item) {
    state.message = "没有可撤销的编辑。";
    renderShellStatus();
    return;
  }
  state.redoStack.push(item);
  restoreHistoryItem(item.beforePackage, item.beforeContext, item.beforeWeaponLibrary,
    item.beforeWeaponLibraryDirty, item.beforeBodyPartDamage, item.beforeBodyPartDamageDirty);
  state.message = `已撤销 ${item.label || "编辑"}`;
  render();
}

function redoLastEdit() {
  if (state.activeUndoTransaction) commitUndoTransaction();
  const item = state.redoStack.pop();
  if (!item) {
    state.message = "没有可重做的编辑。";
    renderShellStatus();
    return;
  }
  state.undoStack.push(item);
  restoreHistoryItem(item.afterPackage, item.afterContext, item.afterWeaponLibrary,
    item.afterWeaponLibraryDirty, item.afterBodyPartDamage, item.afterBodyPartDamageDirty);
  state.message = `已重做 ${item.label || "编辑"}`;
  render();
}

function restoreHistoryItem(packageJson, context, weaponLibraryJson = "", weaponLibraryDirty = false,
  bodyPartDamageJson = "", bodyPartDamageDirty = false) {
  state.package = JSON.parse(packageJson);
  if (bodyPartDamageJson) state.bodyPartDamage = JSON.parse(bodyPartDamageJson);
  if (weaponLibraryJson) state.weaponLibrary = JSON.parse(weaponLibraryJson);
  normalizeWeaponConfiguration3(state.package);
  state.weaponLibraryDirty = Boolean(weaponLibraryDirty);
  state.bodyPartDamageDirty = Boolean(bodyPartDamageDirty);
  ensureAnimationAuthoringConfig(state.package);
  ensureGeometryCollections(state.package);
  restoreUndoContext(context);
  refreshDirtyFromCleanSnapshot();
}

function captureUndoContext() {
  return {
    selectedPath: state.selectedPath,
    viewportMode: state.viewportMode,
    focusedWeaponAttachmentId: state.focusedWeaponAttachmentId,
    weaponTransformKey: state.weaponTransformKey
  };
}

function restoreUndoContext(context = {}) {
  state.selectedPath = context.selectedPath || state.selectedPath || "overview";
  state.viewportMode = context.viewportMode || state.viewportMode || "character";
  state.focusedWeaponAttachmentId = context.focusedWeaponAttachmentId || "";
  state.weaponTransformKey = context.weaponTransformKey || "";
  if (state.viewportMode === "weapon") {
    state.previousCharacterSelection = state.previousCharacterSelection || "overview";
  }
}

function refreshDirtyFromCleanSnapshot() {
  if (!state.package) {
    state.dirty = false;
    return false;
  }
  if (!state.cleanPackageJson) {
    state.dirty = true;
    return true;
  }
  state.dirty = serializePackage(state.package) !== state.cleanPackageJson;
  refreshBodyPartDamageDirtyFromCleanSnapshot();
  return state.dirty;
}

function serializePackage(pkg) {
  return pkg ? JSON.stringify(pkg) : "";
}

function serializeBodyPartDamageState(snapshot) {
  return snapshot ? JSON.stringify(snapshot) : "";
}

function getResourceBindingRole() {
  return RESOURCE_BINDING_ROLES[el.resourceBindingRole?.value] || RESOURCE_BINDING_ROLES.preview;
}

function updateResourceBindingControls() {
  if (!el.resourceBindingRole) return;
  const role = getResourceBindingRole();
  const activeRole = el.resourceBindingRole?.value || "preview";
  const isPreview = activeRole === "preview";
  if (el.openResourcePickerButton) {
    el.openResourcePickerButton.textContent = isPreview ? "选择已有资源" : `绑定已有资源：${role.label}`;
    el.openResourcePickerButton.title = isPreview
      ? "绑定已有资源前，请先切到 Body、主手或副手。"
      : `请先准备角色本地源文件，或在 Resource Manager 准备公共资源，然后绑定到${role.label}。`;
  }
}

function ensureAnimationAuthoringConfig(pkg) {
  if (!pkg) return null;
  const appConfig = pkg.applicationConfig || {};
  pkg.applicationConfig = appConfig;
  if (!Array.isArray(appConfig.resourceKeys)) appConfig.resourceKeys = [];
  if (!Array.isArray(appConfig.animationGroups)) appConfig.animationGroups = [];
  if (!Array.isArray(appConfig.animationProfiles)) appConfig.animationProfiles = [];

  let profile = appConfig.animationProfiles.find(item => item?.profileId === DEFAULT_ANIMATION_PROFILE_ID)
    || appConfig.animationProfiles[0];
  if (!profile) {
    profile = {
      profileId: DEFAULT_ANIMATION_PROFILE_ID,
      displayName: "Default animation profile",
      description: "CharacterStudio stores profile references only. Edit groups, clips, blends, and timelines in AnimationEditor.",
      slots: []
    };
    appConfig.animationProfiles.push(profile);
  }  if (!Array.isArray(profile.slots)) profile.slots = [];
  profile.slots = profile.slots.filter(slot => !LEGACY_ANIMATION_PROFILE_SLOT_IDS.has(String(slot?.slotId || "").trim().toLowerCase()));

  for (const definition of ANIMATION_PROFILE_SLOTS) {
    let slot = profile.slots.find(item => item?.slotId === definition.slotId);
    if (!slot) {
      slot = createDefaultAnimationSlot(pkg, definition);
      profile.slots.push(slot);
    } else {
      normalizeAnimationSlot(slot, pkg, definition);
    }
  }
  profile.slots.sort((a, b) => {
    const ai = ANIMATION_PROFILE_SLOTS.findIndex(item => item.slotId === a.slotId);
    const bi = ANIMATION_PROFILE_SLOTS.findIndex(item => item.slotId === b.slotId);
    return (ai < 0 ? 99 : ai) - (bi < 0 ? 99 : bi);
  });
  ensureReactionAuthoringConfig(pkg, appConfig);
  return appConfig;
}

function createDefaultAnimationSlot(pkg, definition) {
  const resource = findDefaultAnimationResource(pkg, definition.resourceHint);
  const group = resource ? findAnimationGroupByResource(pkg, resource.resourceKey) : null;
  const resourceKey = resource?.resourceKey || "";
  return {
    slotId: definition.slotId,
    displayName: definition.displayName,
    purpose: definition.purpose,
    animationGroupId: group?.groupId || "",
    resourceKey,
    preloadPolicy: "AnimationWarmup",
    required: Boolean(definition.required),
    resourceSelection: resource ? createPackageAnimationSelectionRef(resource) : {}
  };
}

function normalizeAnimationSlot(slot, pkg, definition = {}) {
  slot.slotId ||= definition.slotId || "";
  slot.displayName ||= definition.displayName || slot.slotId || "Animation Slot";
  slot.purpose ||= definition.purpose || "";
  slot.animationGroupId ||= "";
  slot.resourceKey ||= "";
  slot.preloadPolicy ||= "AnimationWarmup";
  slot.required = Boolean(slot.required || definition.required);
  if (!slot.animationGroupId && slot.resourceKey) {
    const group = findAnimationGroupByResource(pkg, slot.resourceKey);
    if (group) slot.animationGroupId = group.groupId;
  }
  if (!slot.resourceSelection || typeof slot.resourceSelection !== "object") {
    const resource = findPackageResourceByKey(pkg, slot.resourceKey);
    slot.resourceSelection = resource ? createPackageAnimationSelectionRef(resource) : {};
  }
  if (!slot.resourceKey) {
    const selected = slot.resourceSelection || {};
    slot.resourceKey = selected.runtimeResourceKey || selected.packageResourceKey || "";
  }
  return slot;
}

function ensureReactionAuthoringConfig(pkg, appConfig = null) {
  if (!pkg) return null;
  const config = appConfig || pkg.applicationConfig || {};
  pkg.applicationConfig = config;
  if (usesCompilerOwnedReactionProjection(config)) return config;
  if (!Array.isArray(config.reactionProfiles)) config.reactionProfiles = [];

  let profile = config.reactionProfiles.find(item => item?.profileId === DEFAULT_REACTION_PROFILE_ID)
    || config.reactionProfiles[0];
  if (!profile) {
    profile = {
      profileId: DEFAULT_REACTION_PROFILE_ID,
      displayName: "Default reaction profile",
      description: "CharacterStudio edits the current ActionSet reaction profile. Saving writes reusable CharacterAction ReactionProfile and Reaction Actions.",
      defaultSlotId: "cohesionBreak",
      slots: []
    };
    config.reactionProfiles.push(profile);
  }
  profile.profileId ||= DEFAULT_REACTION_PROFILE_ID;
  profile.displayName ||= "Default reaction profile";
  profile.description ||= "CharacterStudio edits the current ActionSet reaction profile. Saving writes reusable CharacterAction ReactionProfile and Reaction Actions.";
  profile.defaultSlotId ||= "cohesionBreak";
  if (!Array.isArray(profile.slots)) profile.slots = [];

  for (const definition of REACTION_PROFILE_SLOTS) {
    let slot = profile.slots.find(item => item?.slotId === definition.slotId);
    if (!slot) {
      slot = createDefaultReactionSlot(pkg, definition);
      profile.slots.push(slot);
    } else {
      normalizeReactionSlot(slot, pkg, definition);
    }
  }
  profile.slots.sort((a, b) => {
    const ai = REACTION_PROFILE_SLOTS.findIndex(item => item.slotId === a.slotId);
    const bi = REACTION_PROFILE_SLOTS.findIndex(item => item.slotId === b.slotId);
    return (ai < 0 ? 99 : ai) - (bi < 0 ? 99 : bi);
  });
  return config;
}

function usesCompilerOwnedReactionProjection(appConfig = state.package?.applicationConfig) {
  const schemaVersion = String(appConfig?.schemaVersion || "").trim();
  const majorVersion = Number.parseInt(schemaVersion.split(".")[0], 10);
  return Number.isFinite(majorVersion) && majorVersion >= 8;
}

function createDefaultReactionSlot(pkg, definition) {
  return normalizeReactionSlot({
    slotId: definition.slotId,
    displayName: definition.displayName,
    trigger: definition.trigger,
    pressureBand: definition.pressureBand,
    reactionGroupId: definition.reactionGroupId || "",
    minImpactForce: definition.minImpactForce ?? null,
    maxImpactForce: definition.maxImpactForce ?? null,
    actionId: getGeneratedReactionActionId(pkg, definition),
    durationFrames: definition.durationFrames,
    priority: definition.priority,
    resourceKey: definition.defaultResourceKey || "",
    preloadPolicy: "AnimationWarmup",
    required: Boolean(definition.required),
    resourceSelection: {}
  }, pkg, definition);
}

function normalizeReactionSlot(slot, pkg, definition = {}) {
  slot.slotId ||= definition.slotId || "";
  slot.displayName ||= definition.displayName || slot.slotId || "Reaction Slot";
  slot.trigger ||= definition.trigger || "";
  slot.pressureBand ||= definition.pressureBand || "";
  slot.reactionGroupId ||= definition.reactionGroupId || "";
  slot.minImpactForce = normalizeNullableInteger(slot.minImpactForce, definition.minImpactForce ?? null);
  slot.maxImpactForce = normalizeNullableInteger(slot.maxImpactForce, definition.maxImpactForce ?? null);
  slot.actionId ||= getGeneratedReactionActionId(pkg, definition.slotId ? definition : slot);
  slot.durationFrames = Number.isFinite(Number(slot.durationFrames)) && Number(slot.durationFrames) > 0
    ? Math.round(Number(slot.durationFrames))
    : Number(definition.durationFrames || 18);
  slot.priority = Number.isFinite(Number(slot.priority))
    ? Math.round(Number(slot.priority))
    : Number(definition.priority || 0);
  slot.resourceKey ||= "";
  slot.preloadPolicy ||= "AnimationWarmup";
  slot.required = Boolean(slot.required || definition.required);
  if (!slot.resourceSelection || typeof slot.resourceSelection !== "object") {
    const resource = findPackageResourceByKey(pkg, slot.resourceKey);
    slot.resourceSelection = resource ? createPackageAnimationSelectionRef(resource) : {};
  }
  if (!slot.resourceKey) {
    const selected = slot.resourceSelection || {};
    slot.resourceKey = selected.runtimeResourceKey || selected.packageResourceKey || selected.providerResourceKey || "";
  }
  return slot;
}

function normalizeNullableInteger(value, fallback) {
  if (value === null || value === undefined || value === "") return fallback ?? null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.round(parsed) : (fallback ?? null);
}

function ensureAnimationGroups(pkg) {
  const appConfig = pkg?.applicationConfig;
  if (!appConfig) return [];
  if (!Array.isArray(appConfig.animationGroups)) {
    appConfig.animationGroups = [];
    return appConfig.animationGroups;
  }
  const groups = appConfig.animationGroups;
  for (const group of groups) {
    normalizeAnimationGroup(group, findPackageResourceByKey(pkg, group.sourceResourceKey));
  }
  return groups;
}

function normalizeAnimationGroup(group, resource = null) {
  group.groupId ||= resource?.localId || resource?.resourceKey || `animation.group.${Date.now()}`;
  group.displayName ||= toDisplayName(group.groupId);
  group.description ||= "";
  group.sourceResourceKey ||= resource?.resourceKey || "";
  group.animationPolicy ||= resource?.importHints?.animationPolicy || "splitByClipNames";
  group.defaultBlendSpaceId ||= "";
  group.clips = Array.isArray(group.clips) ? group.clips : [];
  group.blendSpaces = Array.isArray(group.blendSpaces) ? group.blendSpaces : [];
  for (const clip of group.clips) normalizeAnimationClipEntry(clip);
  for (const blendSpace of group.blendSpaces) normalizeAnimationBlendSpace(group, blendSpace);
}

function normalizeAnimationClipEntry(clip) {
  clip.clipId ||= normalizeConfigId(clip.sourceClipName || clip.displayName || "clip");
  clip.sourceClipName ||= clip.clipId;
  clip.displayName ||= toDisplayName(clip.clipId);
  clip.loop = Boolean(clip.loop);
  clip.rootMotionPolicy ||= "Ignore";
  clip.speed = Number.isFinite(Number(clip.speed)) ? Number(clip.speed) : 1;
}

function normalizeAnimationBlendSpace(group, blendSpace) {
  blendSpace.blendSpaceId ||= `${group.groupId}.blend`;
  blendSpace.displayName ||= toDisplayName(blendSpace.blendSpaceId);
  blendSpace.xParameter ||= "moveX";
  blendSpace.yParameter ||= "moveY";
  blendSpace.defaultClipId ||= group.clips[0]?.clipId || "";
  blendSpace.clips = Array.isArray(blendSpace.clips) ? blendSpace.clips : [];
  for (const point of blendSpace.clips) {
    point.clipId ||= group.clips[0]?.clipId || "";
    point.x = Number.isFinite(Number(point.x)) ? Number(point.x) : 0;
    point.y = Number.isFinite(Number(point.y)) ? Number(point.y) : 0;
  }
}

function getPackageAnimationResources(pkg) {
  return (pkg?.resourceCatalog?.entries || []).filter(resource =>
    resource?.resourceKey &&
    (mapResourceKind(resource.typeId) === "Animation" || resource.usage === "animationClipGroup"));
}

function findAnimationGroupByResource(pkg, resourceKey) {
  if (!pkg || !resourceKey) return null;
  return (pkg.applicationConfig?.animationGroups || []).find(group => group?.sourceResourceKey === resourceKey) || null;
}

function findAnimationGroupById(groupId) {
  ensureAnimationAuthoringConfig(state.package);
  return (state.package?.applicationConfig?.animationGroups || []).find(group => group?.groupId === groupId) || null;
}

function normalizeConfigId(value) {
  return String(value || "item")
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, "$1-$2")
    .replace(/[^a-zA-Z0-9]+/g, ".")
    .replace(/^\.+|\.+$/g, "")
    .toLowerCase() || "item";
}

function createUniqueConfigId(base, existingValues) {
  const normalizedBase = normalizeConfigId(base);
  const existing = new Set((existingValues || []).filter(Boolean).map(String));
  if (!existing.has(normalizedBase)) return normalizedBase;
  for (let index = 2; index < 1000; index++) {
    const candidate = `${normalizedBase}.${index}`;
    if (!existing.has(candidate)) return candidate;
  }
  return `${normalizedBase}.${Date.now()}`;
}

function toDisplayName(value) {
  return String(value || "")
    .split(/[._/-]+/)
    .filter(Boolean)
    .map(part => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

function createPackageAnimationSelectionRef(resource) {
  return {
    resourceStableId: resource.stableId || "",
    sourceProviderId: "characterPackage",
    bindingKind: "PackageResource",
    expectedKind: "Animation",
    expectedUsage: resource.usage || "animationClipGroup",
    expectedHash: resource.hash || resource.hashes?.contentHash || "",
    runtimeResourceKey: "",
    providerResourceKey: resource.resourceKey || "",
    packageResourceKey: resource.resourceKey || "",
    unityGuid: "",
    unityAssetPath: "",
    audioCueId: "",
    audioEventDefinitionId: ""
  };
}

function findDefaultAnimationResource(pkg, hint) {
  const resources = (pkg?.resourceCatalog?.entries || []).filter(resource => mapResourceKind(resource.typeId) === "Animation");
  if (!resources.length) return null;
  if (hint) {
    const normalized = String(hint).toLowerCase();
    const matched = resources.find(resource => [
      resource.localId,
      resource.resourceKey,
      resource.stableId,
      resource.relativePath,
      ...(resource.tags || [])
    ].filter(Boolean).some(value => String(value).toLowerCase().includes(normalized)));
    if (matched) return matched;
  }
  return resources[0];
}

function findPackageResourceByKey(pkg, key) {
  if (!pkg || !key) return null;
  return (pkg.resourceCatalog?.entries || []).find(resource => resource?.resourceKey === key) || null;
}

function getDefaultAnimationProfile() {
  ensureAnimationAuthoringConfig(state.package);
  const profiles = state.package?.applicationConfig?.animationProfiles || [];
  return profiles.find(profile => profile?.profileId === DEFAULT_ANIMATION_PROFILE_ID) || profiles[0] || null;
}

function findAnimationSlot(profileId, slotId) {
  ensureAnimationAuthoringConfig(state.package);
  const profiles = state.package?.applicationConfig?.animationProfiles || [];
  const profile = profiles.find(item => item?.profileId === profileId) || profiles[0];
  const slot = profile?.slots?.find(item => item?.slotId === slotId) || null;
  return { profile, slot };
}

function getAnimationSlotPath(profileId, slotId) {
  return `config/animation/${encodeURIComponent(profileId || DEFAULT_ANIMATION_PROFILE_ID)}/slots/${encodeURIComponent(slotId || "")}`;
}

function getDefaultReactionProfile() {
  ensureReactionAuthoringConfig(state.package);
  const profiles = state.package?.applicationConfig?.reactionProfiles || [];
  return profiles.find(profile => profile?.profileId === DEFAULT_REACTION_PROFILE_ID) || profiles[0] || null;
}

function findReactionSlot(profileId, slotId) {
  ensureReactionAuthoringConfig(state.package);
  const profiles = state.package?.applicationConfig?.reactionProfiles || [];
  const profile = profiles.find(item => item?.profileId === profileId) || profiles[0];
  const slot = profile?.slots?.find(item => item?.slotId === slotId) || null;
  return { profile, slot };
}

function getReactionSlotPath(profileId, slotId) {
  return `config/reactions/${encodeURIComponent(profileId || DEFAULT_REACTION_PROFILE_ID)}/slots/${encodeURIComponent(slotId || "")}`;
}

function getCharacterActionPackageSegment(pkg = state.package) {
  return normalizeActionIdSegment(pkg?.manifest?.packageId || state.packageRelative || "character");
}

function getGeneratedReactionProfileStableId(pkg = state.package) {
  return `reaction.${getCharacterActionPackageSegment(pkg)}.default`;
}

function getGeneratedReactionActionId(pkg, slotOrDefinition) {
  const definition = typeof slotOrDefinition === "string"
    ? REACTION_PROFILE_SLOTS.find(item => item.slotId === slotOrDefinition)
    : slotOrDefinition || {};
  const suffix = definition.actionSuffix || `${normalizeActionIdSegment(definition.slotId || "reaction")}_react`;
  return `${getCharacterActionPackageSegment(pkg)}_${suffix}`;
}

function normalizeActionIdSegment(value) {
  return normalizeConfigId(value).replace(/\./g, "_");
}

function getCharacterActionPackageSourceFolder(kind, pkg = state.package) {
  const packageSegment = getCharacterActionPackageSegment(pkg);
  if (kind === "profiles") return `${CHARACTER_ACTION_HANDOFF.generatedRoot}/Profiles/${packageSegment}`;
  if (kind === "actionSets") return `${CHARACTER_ACTION_HANDOFF.generatedRoot}/ActionSets/${packageSegment}`;
  return CHARACTER_ACTION_HANDOFF.actionLibrarySourceDirectory;
}

function getAnimationGroupPath(groupId) {
  return `config/animation/groups/${encodeURIComponent(groupId || "")}`;
}

function getAnimationClipPath(groupId, clipId) {
  return `${getAnimationGroupPath(groupId)}/clips/${encodeURIComponent(clipId || "")}`;
}

function getAnimationBlendSpacePath(groupId, blendSpaceId) {
  return `${getAnimationGroupPath(groupId)}/blendSpaces/${encodeURIComponent(blendSpaceId || "")}`;
}

function getAnimationBlendPointPath(groupId, blendSpaceId, index) {
  return `${getAnimationBlendSpacePath(groupId, blendSpaceId)}/clips/${index}`;
}

function renderAnimationConfigPanel() {
  if (!el.animationConfigPanel) return;
  if (String(state.package?.applicationConfig?.schemaVersion || "") === "4.0") {
    renderCurrentCombinedConfigurationShortcut("animation", el.animationConfigPanel);
    return;
  }
  if (String(state.package?.applicationConfig?.schemaVersion || "") === "2.0") {
    renderWeaponConfigurationAnimationPanel();
    return;
  }
  const profile = getDefaultAnimationProfile();
  if (!profile) {
    el.animationConfigPanel.innerHTML = `<div class="empty">没有动画 Profile。</div>`;
    return;
  }

  const animationResources = getResourceLibraryItems().filter(item => item.kind === "Animation");
  const runtimeReadyCount = animationResources.filter(item => item.runtimeAvailability === "RuntimeReady" || item.bindingKind === "PackageResource" || item.bindingKind === "ResourceManagerAsset").length;
  const groups = state.package?.applicationConfig?.animationGroups || [];
  const slots = profile.slots || [];
  const animationEditorUrl = getAnimationEditorUrl();
  el.animationConfigPanel.innerHTML = `
    <div class="animation-profile-summary">
      <div><strong>${escapeHtml(profile.displayName || profile.profileId)}</strong><span>${escapeHtml(profile.profileId || DEFAULT_ANIMATION_PROFILE_ID)}</span></div>
      <div><strong>${escapeHtml(String(groups.length))}</strong><span>AnimationEditor 分组</span></div>
      <div><strong>${escapeHtml(String(animationResources.length))}</strong><span>可见动画资源</span></div>
      <div><strong>${escapeHtml(String(runtimeReadyCount))}</strong><span>Runtime 就绪</span></div>
    </div>
    <div class="animation-panel-actions">
      <a class="button" href="${escapeHtml(animationEditorUrl)}">打开 AnimationEditor</a>
    </div>
    <p class="field-help">CharacterStudio 保存 Profile、分组和资源引用。分组、剪辑、Blend 和时间线事件请在 AnimationEditor 中编辑。</p>
    <div class="animation-slot-list">
      ${groups.map(group => renderAnimationGroupCard(group)).join("")}
    </div>
    <div class="animation-slot-list">
      ${slots.map(slot => renderAnimationSlotCard(profile, slot)).join("")}
    </div>`;
}

function renderWeaponConfigurationAnimationPanel() {
  const configuration = getActiveWeaponConfiguration();
  const animation = state.package?.animationAuthoring || {};
  const profile = (animation.profiles || []).find(item => item?.profileId === configuration?.animationProfileId) || null;
  const set = (animation.sets || []).find(item => item?.setId === profile?.defaultSetId)
    || (animation.sets || []).find(item => (profile?.slots || []).some(slot => slot?.setId === item?.setId))
    || null;
  const groups = set?.groups || [];
  const clips = groups.flatMap(group => group?.clips || []);
  const warmup = profile?.warmup || set?.warmup || {};
  el.animationConfigPanel.innerHTML = `
    <div class="animation-profile-summary">
      <div><strong>${escapeHtml(configuration?.displayName || configuration?.combinedWeaponConfigurationId || "未选择组合武器配置")}</strong><span>当前组合武器配置</span></div>
      <div><strong>${escapeHtml(profile?.displayName || profile?.profileId || "缺失")}</strong><span>Animation Profile</span></div>
      <div><strong>${escapeHtml(set?.displayName || set?.setId || "缺失")}</strong><span>Set</span></div>
      <div><strong>${escapeHtml(String(clips.length))}</strong><span>Clips</span></div>
    </div>
    <div class="animation-panel-actions"><a class="button" href="${escapeHtml(getAnimationEditorUrl())}">打开 AnimationEditor</a></div>
    <p class="field-help">解析链：组合武器配置 → Animation Profile → Set / Group / Clip → Warmup / Runtime。通用 locomotion 只在所选 Profile 内作为 fallback。</p>
    <div class="animation-slot-list">
      <article class="animation-slot-card">
        <div class="animation-slot-main"><div><strong>${escapeHtml(profile?.profileId || configuration?.animationProfileId || "未绑定")}</strong><span>${escapeHtml(configuration?.animationProfileId || "animationProfileId 缺失")}</span></div></div>
        <code>${escapeHtml(`set=${set?.setId || "missing"} / groups=${groups.length} / warmup=${warmup.preloadPolicy || warmup.mode || "default"}`)}</code>
      </article>
      ${groups.map(group => `<article class="animation-slot-card"><div class="animation-slot-main"><div><strong>${escapeHtml(group.displayName || group.groupId)}</strong><span>${escapeHtml(group.groupId || "group")}</span></div></div><code>${escapeHtml(`${(group.clips || []).length} clips / ${(group.blendSpaces || []).length} blends`)}</code></article>`).join("")}
    </div>`;
}

function renderWeaponConfigurationPanel4() {
  const app = state.package?.applicationConfig || {};
  const configurations = app.combinedWeaponConfigurations || [];
  const singles = state.weaponLibrary?.singleWeaponConfigurations || [];
  const active = getActiveCombinedWeaponConfiguration();
  if (el.activeCombinedWeaponConfigurationSelect) {
    el.activeCombinedWeaponConfigurationSelect.innerHTML = configurations.map(item =>
      `<option value="${escapeHtml(item.combinedWeaponConfigurationId || "")}"${item === active ? " selected" : ""}>${escapeHtml(item.displayName || item.combinedWeaponConfigurationId || "未命名")}${item.combinedWeaponConfigurationId === app.defaultCombinedWeaponConfigurationId ? "（默认）" : ""}</option>`).join("");
    el.activeCombinedWeaponConfigurationSelect.disabled = configurations.length === 0;
  }
  renderSingleWeaponWorkbench();
  renderGlobalConfigurationEditors();
  if (typeof globalThis.renderCombinedWeaponWorkbench === "function") {
    globalThis.renderCombinedWeaponWorkbench();
    return;
  }
  if (!el.combinedWeaponConfigurationPanel) return;
  if (!active) {
    el.combinedWeaponConfigurationPanel.innerHTML = `<div class="empty">当前角色没有绑定公共组合配置。可新建公共草稿并绑定；主副手初始均为显式空手。</div><div class="weapon-configuration-actions"><button type="button" data-weapon-config-action="new">新建公共空白组合</button></div>`;
    return;
  }
  const snapshot = (state.combinedWeaponConfigurationsSnapshot?.combinedWeaponConfigurations || []).find(item =>
    item?.combinedWeaponConfigurationId === active.combinedWeaponConfigurationId);
  const packageId = state.package?.manifest?.packageId || "character";
  const generatedProfileId = state.characterActionCatalog?.profileId || `${packageId}.default`;
  const generatedActionSetId = (state.characterActionCatalog?.actionSets || []).find(item =>
    item?.combinedWeaponConfigurationId === active.combinedWeaponConfigurationId)?.actionSetId
    || active.combinedWeaponConfigurationId
    || `${packageId}.unconfigured`;
  const slotCard = (slotId, label) => {
    const slot = (active.slots || []).find(item => item?.slotId === slotId);
    const options = singles.filter(item => (item?.allowedSlots || []).includes(slotId)).map(item =>
      `<option value="${escapeHtml(item.singleWeaponConfigurationId || "")}"${item.singleWeaponConfigurationId === slot?.singleWeaponConfigurationId ? " selected" : ""}>${escapeHtml(item.displayName || item.singleWeaponConfigurationId)}</option>`).join("");
    return `<article class="weapon-configuration-slot"><label><span>${label}</span><select data-weapon-config-slot="${slotId}">${options}</select></label><small>${slot?.singleWeaponConfigurationId === "weapon.unarmed" ? "显式空手；禁止 attachment" : escapeHtml(slot?.attachmentId ? `角色挂载：${slot.attachmentId}` : "缺少角色本地 attachment")}</small></article>`;
  };
  el.combinedWeaponConfigurationPanel.innerHTML = `
    <div class="weapon-configuration-head"><div><strong>${escapeHtml(active.displayName || active.combinedWeaponConfigurationId)}</strong><span>${escapeHtml(active.combinedWeaponConfigurationId)} · ${active.combinedWeaponConfigurationId === app.defaultCombinedWeaponConfigurationId ? "默认配置" : "角色草稿"}</span></div><span class="sync-badge ${snapshot?.diagnostics?.length ? "error" : "ok"}">${snapshot?.diagnostics?.length ? "编译阻断" : "可生成"}</span></div>
    <div class="weapon-configuration-slots">${slotCard("mainHand", "主手单一武器配置")}${slotCard("offHand", "副手单一武器配置")}</div>
    <div class="weapon-configuration-contract">
      ${renderCombinedGlobalConfigurationSelector("animation", "动画配置", active.animationConfigurationId)}
      ${renderCombinedGlobalConfigurationSelector("reaction", "受击配置", active.reactionConfigurationId)}
      <div><span>Profile（编译器生成）</span><code>${escapeHtml(generatedProfileId)}</code></div>
      <div><span>ActionSet（编译器生成）</span><code>${escapeHtml(generatedActionSetId)}</code></div>
    </div>
    <div class="weapon-configuration-quick-edit">
      ${renderCombinedGlobalQuickEditor("animation", active.animationConfigurationId, snapshot?.animationReferences)}
      ${renderCombinedGlobalQuickEditor("reaction", active.reactionConfigurationId, snapshot?.reactionReferences)}
      ${renderPrivateActionConfiguration(active.actionConfiguration)}
    </div>
    <div class="weapon-configuration-actions"><button type="button" data-weapon-config-action="new">新建空白</button><button type="button" data-weapon-config-action="copy">复制组合</button><button type="button" data-weapon-config-action="delete">删除</button><button type="button" data-weapon-config-action="default"${active.combinedWeaponConfigurationId === app.defaultCombinedWeaponConfigurationId ? " disabled" : ""}>设为默认</button></div>
    <div class="weapon-definition-summary"><strong>公共组合是运行时选择基准</strong><span>角色通过 binding 选用公共组合；没有覆盖时继承公共行为默认，槽位始终来自公共组合。</span></div>
    ${snapshot?.diagnostics?.length ? `<div class="ability-chain-diagnostics">${snapshot.diagnostics.map(item => `<span>${escapeHtml(item.code)}: ${escapeHtml(item.message)}</span>`).join("")}</div>` : ""}`;
}

function renderCurrentCombinedConfigurationShortcut(type, panel) {
  const definition = getGlobalConfigurationUiDefinition(type);
  const combined = getActiveCombinedWeaponConfiguration();
  const snapshot = (state.combinedWeaponConfigurationsSnapshot?.combinedWeaponConfigurations || []).find(item =>
    item?.combinedWeaponConfigurationId === combined?.combinedWeaponConfigurationId);
  const references = type === "animation"
    ? snapshot?.animationReferences
    : type === "ability"
      ? snapshot?.abilityReferences
      : snapshot?.reactionReferences;
  panel.innerHTML = `<div class="weapon-configuration-head"><div><strong>当前组合 → ${definition.label}</strong><span>${escapeHtml(combined?.displayName || combined?.combinedWeaponConfigurationId || "未选择组合")}</span></div></div>${renderCombinedGlobalQuickEditor(type, combined?.[definition.combinedField] || "", references)}<p class="field-help">此一级面板不维护独立选择状态，始终跟随顶部当前组合。</p>`;
}

function renderCombinedGlobalConfigurationSelector(type, label, selectedId) {
  const definition = getGlobalConfigurationUiDefinition(type);
  const items = state.weaponLibrary?.[definition.collection] || [];
  const options = type === "animation" && globalThis.renderAnimationConfigurationOption
    ? items.map(item => globalThis.renderAnimationConfigurationOption(item, selectedId)).join("")
    : items.map(item => `<option value="${escapeHtml(item[definition.idField] || "")}"${item[definition.idField] === selectedId ? " selected" : ""}>${escapeHtml(item.displayName || item[definition.idField])}</option>`).join("");
  return `<label><span>${label}</span><select data-weapon-config-field="${definition.combinedField}"><option value="">未选择（允许保存草稿）</option>${options}</select></label>`;
}

function renderCombinedGlobalQuickEditor(type, id, references) {
  const definition = getGlobalConfigurationUiDefinition(type);
  const item = (state.weaponLibrary?.[definition.collection] || []).find(value => value?.[definition.idField] === id);
  if (!item) return `<article class="global-quick-editor blocked"><strong>${definition.label}</strong><span>未选择；当前组合只能保存为草稿，不能编译或 Unity 导入。</span></article>`;
  const referenceList = references || [];
  if (type === "ability") return renderAbilityConfigurationEditor(item, referenceList, true);
  return `<details class="global-quick-editor"><summary><strong>${definition.label}：${escapeHtml(item.displayName || id)}</strong><span>共享配置，修改会影响 ${referenceList.length} 个角色 / 组合</span></summary>
    ${renderGlobalReferenceList(referenceList)}
    <label><span>显示名称</span><input data-global-config-type="${type}" data-global-quick-field="displayName" value="${escapeHtml(item.displayName || "")}"></label>
    <label><span>完整共享配置 JSON</span><textarea rows="12" data-global-config-type="${type}" data-global-quick-field="configurationJson">${escapeHtml(JSON.stringify(item, null, 2))}</textarea></label>
    <div class="weapon-configuration-actions"><button type="button" data-global-config-type="${type}" data-global-quick-action="copyRebind">复制为新配置并重绑当前组合</button></div>
  </details>`;
}

function renderAbilityConfigurationEditor(item, references = [], compact = false) {
  const abilities = item?.abilities || [];
  const counts = value => abilities.filter(entry => entry?.[value[0]] === value[1]).length;
  const rows = abilities.map((entry, index) => `<article class="ability-semantic-entry">
    <header><div><strong>${escapeHtml(entry?.displayName || `Ability ${entry?.abilityId || "?"}`)}</strong><code>${escapeHtml(entry?.abilityId || "未设置 ID")}</code></div><div><span class="sync-badge">${escapeHtml(entry?.tier || "Unspecified")}</span><span class="sync-badge ${entry?.executionMode === "GameplayRuntime" ? "warn" : "ok"}">${escapeHtml(entry?.executionMode || "External")}</span><button type="button" class="danger" data-ability-entry-action="delete" data-ability-entry-index="${index}">删除</button></div></header>
    <div class="ability-semantic-grid">
      <label><span>Ability ID</span><input type="number" min="1" data-ability-entry-index="${index}" data-ability-entry-field="abilityId" value="${escapeHtml(entry?.abilityId || "")}"></label>
      <label><span>显示名称</span><input data-ability-entry-index="${index}" data-ability-entry-field="displayName" value="${escapeHtml(entry?.displayName || "")}" placeholder="例如：闪避"></label>
      <label><span>分类</span><select data-ability-entry-index="${index}" data-ability-entry-field="tier">${["Unspecified","Basic","Special","Passive"].map(value => `<option${entry?.tier === value ? " selected" : ""}>${value}</option>`).join("")}</select></label>
      <label><span>执行方式</span><select data-ability-entry-index="${index}" data-ability-entry-field="executionMode">${["ActionOnly","GameplayRuntime","External"].map(value => `<option${entry?.executionMode === value ? " selected" : ""}>${value}</option>`).join("")}</select></label>
      <label><span>触发方式</span><select data-ability-entry-index="${index}" data-ability-entry-field="triggerMode">${["Input","Event","Passive","Automatic","External"].map(value => `<option${entry?.triggerMode === value ? " selected" : ""}>${value}</option>`).join("")}</select></label>
      <label><span>动作绑定</span><select data-ability-entry-index="${index}" data-ability-entry-field="actionBindingMode">${["Required","Optional","None"].map(value => `<option${entry?.actionBindingMode === value ? " selected" : ""}>${value}</option>`).join("")}</select></label>
      <label><span>输入槽位</span><input data-ability-entry-index="${index}" data-ability-entry-field="slotId" value="${escapeHtml(entry?.slotId || "")}"></label>
      <label><span>Input Intent</span><input data-ability-entry-index="${index}" data-ability-entry-field="inputIntentId" value="${escapeHtml(entry?.inputIntentId || "")}"></label>
      <label><span>ActionRole</span><input data-ability-entry-index="${index}" data-ability-entry-field="actionRole" value="${escapeHtml(entry?.actionRole || "")}"></label>
      <label><span>标签（逗号分隔）</span><input data-ability-entry-index="${index}" data-ability-entry-field="tags" value="${escapeHtml((entry?.tags || []).join(", "))}"></label>
    </div>
    ${entry?.tier === "Unspecified" ? `<p class="field-help warning">用途待确认：请填写明确名称、分类和标签。</p>` : ""}
    ${entry?.executionMode === "GameplayRuntime" ? `<p class="field-help">该项明确要求 Gameplay Runtime Catalog；这是可选集成，不阻止编译或导入。</p>` : ""}
  </article>`).join("");
  return `<article class="global-quick-editor ability-workbench${compact ? " compact" : ""}"><header class="ability-workbench-head"><div><strong>能力配置：${escapeHtml(item?.displayName || item?.abilityConfigurationId)}</strong><code>${escapeHtml(item?.abilityConfigurationId || "")}</code></div><span>共享配置，影响 ${references.length} 个角色 / 组合</span></header>
    ${renderGlobalReferenceList(references)}
    <div class="ability-summary-strip"><span>基础 <b>${counts(["tier","Basic"])}</b></span><span>特殊 <b>${counts(["tier","Special"])}</b></span><span>被动 <b>${counts(["tier","Passive"])}</b></span><span>ActionOnly <b>${counts(["executionMode","ActionOnly"])}</b></span><span>Gameplay Runtime <b>${counts(["executionMode","GameplayRuntime"])}</b></span><span>External <b>${counts(["executionMode","External"])}</b></span></div>
    <div class="weapon-configuration-contract"><label><span>显示名称</span><input data-global-config-type="ability" data-global-quick-field="displayName" value="${escapeHtml(item?.displayName || "")}"></label><label><span>说明</span><input data-global-config-type="ability" data-global-quick-field="description" value="${escapeHtml(item?.description || "")}"></label></div>
    <div class="ability-semantic-list">${rows || `<div class="empty">当前配置没有能力。空能力配置是合法的，也可以直接让组合选择“未配置”。</div>`}</div>
    <div class="weapon-configuration-actions"><button type="button" data-ability-entry-action="new">新增能力</button><button type="button" data-global-config-type="ability" data-global-quick-action="copyRebind">复制为新配置并重绑当前组合</button></div>
    <details class="behavior-readonly-json"><summary>只读 JSON 审计</summary><pre>${escapeHtml(JSON.stringify(item, null, 2))}</pre></details>
  </article>`;
}

function renderPrivateActionConfiguration(configuration) {
  const value = configuration || { actions: [], commandSequences: [], abilityActionBindings: [], reactionActionBindings: [] };
  return `<details class="global-quick-editor"><summary><strong>公共默认 CharacterAction / ActionSet</strong><span>${(value.actions || []).length} 个动作；未覆盖角色自动继承</span></summary><p class="field-help">动作槽请使用结构化选择器修改；此处仅用于审计，不能直接编辑 JSON。</p><pre>${escapeHtml(JSON.stringify(value, null, 2))}</pre></details>`;
}

function getGlobalConfigurationUiDefinition(type) {
  return {
    animation: { collection: "animationConfigurations", idField: "animationConfigurationId", stateField: "activeAnimationConfigurationId", combinedField: "animationConfigurationId", label: "动画配置" },
    ability: { collection: "abilityConfigurations", idField: "abilityConfigurationId", stateField: "activeAbilityConfigurationId", combinedField: "abilityConfigurationId", label: "能力配置" },
    reaction: { collection: "reactionConfigurations", idField: "reactionConfigurationId", stateField: "activeReactionConfigurationId", combinedField: "reactionConfigurationId", label: "受击配置" }
  }[type];
}

function renderGlobalConfigurationEditors() {
  renderGlobalConfigurationEditor("animation", el.globalAnimationConfigurationPanel);
  renderGlobalConfigurationEditor("ability", el.globalAbilityConfigurationPanel);
  renderGlobalConfigurationEditor("reaction", el.globalReactionConfigurationPanel);
}

function renderGlobalConfigurationEditor(type, panel) {
  if (!panel) return;
  const definition = getGlobalConfigurationUiDefinition(type);
  const items = state.weaponLibrary?.[definition.collection] || [];
  const active = items.find(item => item?.[definition.idField] === state[definition.stateField]) || items[0] || null;
  if (!active) {
    panel.innerHTML = `<div class="empty">没有${definition.label}。新建后可完整编辑并被任意角色组合引用。</div><div class="weapon-configuration-actions"><button type="button" data-global-config-type="${type}" data-global-config-action="new">新建${definition.label}</button></div>`;
    return;
  }
  requestGlobalConfigurationReferences(type, active[definition.idField]);
  const references = state.globalConfigurationReferences?.[`${type}:${active[definition.idField]}`]?.references || [];
  if (type === "ability") {
    panel.innerHTML = `<div class="weapon-configuration-head"><div><strong>能力配置工作台</strong><span>结构化语义、触发、动作与执行方式</span></div><span class="sync-badge ${state.weaponLibraryDirty ? "warn" : "ok"}">${state.weaponLibraryDirty ? "全局库未保存" : "全局库已保存"}</span></div><label><span>当前配置</span><select data-global-config-select="ability">${items.map(item => `<option value="${escapeHtml(item.abilityConfigurationId || "")}"${item === active ? " selected" : ""}>${escapeHtml(item.displayName || item.abilityConfigurationId)}</option>`).join("")}</select></label>${renderAbilityConfigurationEditor(active, references)}<div class="weapon-configuration-actions"><button type="button" data-global-config-type="ability" data-global-config-action="new">新建配置</button><button type="button" data-global-config-type="ability" data-global-config-action="copy">复制配置</button><button type="button" data-global-config-type="ability" data-global-config-action="delete">删除配置</button><button type="button" data-global-config-type="ability" data-global-config-action="save"${state.weaponLibraryDirty ? "" : " disabled"}>保存全局库</button></div>`;
    return;
  }
  panel.innerHTML = `<div class="weapon-configuration-head"><div><strong>${definition.label}</strong><span>WeaponSource/global_weapon_library.json · Schema 6.0</span></div><span class="sync-badge ${state.weaponLibraryDirty ? "warn" : "ok"}">${state.weaponLibraryDirty ? "共享库未保存" : "共享库已保存"}</span></div>
    <label><span>当前配置</span><select data-global-config-select="${type}">${items.map(item => `<option value="${escapeHtml(item[definition.idField] || "")}"${item === active ? " selected" : ""}>${escapeHtml(item.displayName || item[definition.idField])}</option>`).join("")}</select></label>
    <div class="weapon-definition-summary"><strong>影响范围：${references.length} 个角色 / 组合</strong><span>快捷编辑和这里修改的是同一条共享配置。</span></div>
    ${renderGlobalReferenceList(references)}
    <div class="weapon-configuration-contract"><label><span>显示名称</span><input data-global-config-type="${type}" data-global-config-field="displayName" value="${escapeHtml(active.displayName || "")}"></label><label><span>说明</span><input data-global-config-type="${type}" data-global-config-field="description" value="${escapeHtml(active.description || "")}"></label></div>
    <label><span>完整配置 JSON</span><textarea rows="18" data-global-config-type="${type}" data-global-config-field="configurationJson">${escapeHtml(JSON.stringify(active, null, 2))}</textarea></label>
    <div class="weapon-configuration-actions"><button type="button" data-global-config-type="${type}" data-global-config-action="new">新建</button><button type="button" data-global-config-type="${type}" data-global-config-action="copy">复制</button><button type="button" data-global-config-type="${type}" data-global-config-action="delete">删除</button><button type="button" data-global-config-type="${type}" data-global-config-action="save"${state.weaponLibraryDirty ? "" : " disabled"}>保存全局库</button></div>`;
}

function renderGlobalReferenceList(references) {
  if (!references?.length) return `<p class="field-help">当前没有角色组合引用。</p>`;
  return `<div class="mini-chip-row">${references.map(item => `<span class="mini-chip">${escapeHtml(item.packageRelative || "角色")} / ${escapeHtml(item.combinedWeaponConfigurationId || "组合")}</span>`).join("")}</div>`;
}

function requestGlobalConfigurationReferences(type, id) {
  if (!id) return;
  const key = `${type}:${id}`;
  state.globalConfigurationReferences ||= {};
  if (Object.prototype.hasOwnProperty.call(state.globalConfigurationReferences, key)) return;
  state.globalConfigurationReferences[key] = null;
  readJson(`/api/weapon-library/references?configurationType=${encodeURIComponent(type)}&configurationId=${encodeURIComponent(id)}`, null).then(payload => {
    state.globalConfigurationReferences[key] = payload || { references: [], referenceCount: 0 };
    render();
  });
}

function renderWeaponConfigurationPanel() {
  const schemaVersion = String(state.package?.applicationConfig?.schemaVersion || "");
  if (["4.0", "5.0", "6.0", "7.0", "8.0"].includes(schemaVersion)) {
    renderWeaponConfigurationPanel4();
    return;
  }
  const app = state.package?.applicationConfig;
  const configurations = app?.combinedWeaponConfigurations || [];
  const definitions = state.weaponLibrary?.singleWeaponConfigurations || [];
  const active = getActiveCombinedWeaponConfiguration();
  if (el.activeCombinedWeaponConfigurationSelect) {
    el.activeCombinedWeaponConfigurationSelect.innerHTML = configurations.map(item =>
      `<option value="${escapeHtml(item.combinedWeaponConfigurationId || "")}"${item === active ? " selected" : ""}>${escapeHtml(item.displayName || item.combinedWeaponConfigurationId || "未命名")}${item.combinedWeaponConfigurationId === app?.defaultCombinedWeaponConfigurationId ? "（默认）" : ""}</option>`).join("");
    el.activeCombinedWeaponConfigurationSelect.disabled = configurations.length === 0;
  }
  renderSingleWeaponWorkbench();
  renderGlobalConfigurationEditors();
  if (!el.combinedWeaponConfigurationPanel) return;
  if (!active) {
    el.combinedWeaponConfigurationPanel.innerHTML = `<div class="empty">当前角色还没有组合武器配置。请新建组合，并为主手、副手显式选择普通单一武器或 weapon.unarmed。</div><div class="weapon-configuration-actions"><button type="button" data-weapon-config-action="new">新建组合武器配置</button></div>`;
    return;
  }
  const snapshot = (state.combinedWeaponConfigurationsSnapshot?.combinedWeaponConfigurations || []).find(item =>
    item?.combinedWeaponConfigurationId === active.combinedWeaponConfigurationId);
  const packageId = state.package?.manifest?.packageId || "character";
  const generatedProfileId = `${packageId}.default`;
  const generatedActionSetId = active.combinedWeaponConfigurationId || `${packageId}.unconfigured`;
  const slotCard = slotId => {
    const slot = (active.slots || []).find(item => item?.slotId === slotId);
    const options = definitions.filter(definition => (definition?.allowedSlots || []).includes(slotId)).map(definition =>
      `<option value="${escapeHtml(definition.singleWeaponConfigurationId || "")}"${definition.singleWeaponConfigurationId === slot?.singleWeaponConfigurationId ? " selected" : ""}>${escapeHtml(definition.displayName || definition.singleWeaponConfigurationId)}</option>`).join("");
    const label = slotId === "mainHand" ? "主手" : "副手";
    return `<article class="weapon-configuration-slot"><label><span>${label}</span><select data-weapon-config-slot="${slotId}">${options}</select></label><small>${escapeHtml(slot?.singleWeaponConfigurationId === "weapon.unarmed" ? "显式空手（无 attachment / 无资源实例）" : slot?.attachmentId ? `mount=${slot.attachmentId}` : "普通武器缺少角色本地 attachment")}</small></article>`;
  };
  el.combinedWeaponConfigurationPanel.innerHTML = `
    <div class="weapon-configuration-head"><div><strong>${escapeHtml(active.displayName || active.combinedWeaponConfigurationId)}</strong><span>${escapeHtml(active.combinedWeaponConfigurationId)}${active.combinedWeaponConfigurationId === app.defaultCombinedWeaponConfigurationId ? " / 默认" : " / 草稿"}</span></div><span class="sync-badge ${snapshot?.diagnostics?.length ? "error" : "ok"}">${snapshot?.diagnostics?.length ? "阻断" : "可编译"}</span></div>
    <div class="weapon-configuration-slots">${slotCard("mainHand")}${slotCard("offHand")}</div>
    <div class="weapon-configuration-contract">
      <div><span>Profile（编译器生成）</span><code>${escapeHtml(generatedProfileId)}</code></div>
      <div><span>ActionSet（编译器生成）</span><code>${escapeHtml(generatedActionSetId)}</code></div>
      <label><span>组合 AbilityLoadout</span><input data-weapon-config-field="abilityLoadoutId" value="${escapeHtml(active.abilityLoadoutId || "")}"></label>
    </div>
    <div class="weapon-configuration-actions"><button type="button" data-weapon-config-action="new">新建</button><button type="button" data-weapon-config-action="copy">复制</button><button type="button" data-weapon-config-action="delete">删除</button><button type="button" data-weapon-config-action="default"${active.combinedWeaponConfigurationId === app.defaultCombinedWeaponConfigurationId ? " disabled" : ""}>设为默认</button></div>
    <div class="weapon-definition-summary"><strong>组合只引用全局单一武器</strong><span>角色、动画、能力、动作和 Runtime 只选择组合 ID，不直接选择单一武器 ID。</span></div>
    ${snapshot?.diagnostics?.length ? `<div class="ability-chain-diagnostics">${snapshot.diagnostics.map(item => `<span>${escapeHtml(item.code)}: ${escapeHtml(item.message)}</span>`).join("")}</div>` : ""}`;
}

function renderAnimationGroupCard(group) {
  const resource = findPackageResourceByKey(state.package, group.sourceResourceKey);
  const path = getAnimationGroupPath(group.groupId);
  return `<article class="animation-slot-card">
    <div class="animation-slot-main">
      <div>
        <strong>${escapeHtml(group.displayName || group.groupId)}</strong>
        <span>${escapeHtml(group.groupId || "动画分组")} / AnimationEditor 管理</span>
      </div>
      <button type="button" data-animation-jump="1" data-path="${escapeHtml(path)}" title="查看动画分组">查看</button>
    </div>
    <div class="animation-slot-resource">
      <span>${escapeHtml(resource ? getResourceDisplayName(resource) : group.sourceResourceKey || "未绑定资源")}</span>
      <span class="sync-badge muted">${escapeHtml(group.animationPolicy || "manual")}</span>
    </div>
    <code>${escapeHtml(`${group.clips?.length || 0} clips / ${group.blendSpaces?.length || 0} blend spaces`)}</code>
    ${renderAnimationGroupChildren(group)}
  </article>`;
}

function getAnimationEditorUrl() {
  return `/Tools/MxFramework.AnimationEditor/web/?package=${encodeURIComponent(state.packageRelative || DEFAULT_PACKAGE)}`;
}

function renderAnimationGroupChildren(group) {
  const clips = (group.clips || []).slice(0, 8).map(clip =>
    `<span class="mini-chip">${escapeHtml(clip.clipId)}</span>`).join("");
  const blends = (group.blendSpaces || []).slice(0, 4).map(blend =>
    `<span class="mini-chip">${escapeHtml(blend.blendSpaceId)}</span>`).join("");
  return `<div class="mini-chip-row">${clips}${blends}</div>`;
}

function renderAnimationSlotCard(profile, slot) {
  const resource = findAnimationSlotResource(slot);
  const selection = slot.resourceSelection || {};
  const selectedText = resource
    ? getResourceDisplayName(resource.resource || resource)
    : firstNonEmpty(slot.resourceKey, selection.runtimeResourceKey, selection.packageResourceKey, selection.providerResourceKey, selection.unityAssetPath, "未选择");
  const availability = resource?.runtimeAvailability || (selection.bindingKind === "UnityEditorOnlyAsset" ? "EditorOnly" : "");
  const keyText = firstNonEmpty(slot.resourceKey, selection.runtimeResourceKey, selection.packageResourceKey, selection.providerResourceKey, selection.unityAssetPath, "-");
  const path = getAnimationSlotPath(profile.profileId, slot.slotId);
  return `<article class="animation-slot-card">
    <div class="animation-slot-main">
      <div>
        <strong>${escapeHtml(slot.displayName || slot.slotId)}</strong>
        <span>${escapeHtml(slot.purpose || "动画资源槽位")}</span>
      </div>
      <button type="button" data-animation-jump="1" data-path="${escapeHtml(path)}" title="查看动画槽位">查看</button>
    </div>
    <div class="animation-slot-resource">
      <span>${escapeHtml(selectedText)}</span>
      ${availability ? `<span class="sync-badge ${escapeHtml(getResourceAvailabilityTone(availability))}">${escapeHtml(formatWorkflowStatusLabel(availability) === availability ? availability : formatWorkflowStatusLabel(availability))}</span>` : ""}
    </div>
    <code title="${escapeHtml(keyText)}">${escapeHtml(keyText)}</code>
    <div class="animation-slot-actions">
      <button type="button" data-animation-pick="1" data-profile-id="${escapeHtml(profile.profileId)}" data-slot-id="${escapeHtml(slot.slotId)}">选择动画资源</button>
      <button type="button" data-animation-clear="1" data-profile-id="${escapeHtml(profile.profileId)}" data-slot-id="${escapeHtml(slot.slotId)}">清空</button>
    </div>
  </article>`;
}

function getResourceAvailabilityTone(value) {
  const normalized = String(value || "").toLowerCase();
  if (normalized.includes("ready") || normalized.includes("package")) return "ok";
  if (normalized.includes("editor") || normalized.includes("missing")) return "warn";
  return "muted";
}

function firstNonEmpty(...values) {
  for (const value of values) {
    if (value == null) continue;
    const text = String(value).trim();
    if (text) return text;
  }
  return "";
}

function findAnimationSlotResource(slot) {
  if (!slot) return null;
  const values = [
    slot.resourceKey,
    slot.resourceSelection?.runtimeResourceKey,
    slot.resourceSelection?.packageResourceKey,
    slot.resourceSelection?.providerResourceKey,
    slot.resourceSelection?.resourceStableId,
    slot.resourceSelection?.unityAssetPath
  ].filter(Boolean).map(String);
  if (!values.length) return null;
  return getResourceLibraryItems().find(item => values.some(value => getResourceIdentityValues(item).includes(value))) || null;
}

function renderReactionProfilePanel() {
  if (!el.reactionProfilePanel) return;
  if (usesCompilerOwnedReactionProjection()) {
    renderCompilerOwnedReactionProjectionPanel();
    return;
  }
  if (String(state.package?.applicationConfig?.schemaVersion || "") === "4.0") {
    renderCurrentCombinedConfigurationShortcut("reaction", el.reactionProfilePanel);
    return;
  }
  const profile = getDefaultReactionProfile();
  if (!profile) {
    el.reactionProfilePanel.innerHTML = `<div class="empty">没有受击动作组配置。</div>`;
    return;
  }

  const slots = profile.slots || [];
  const selectedCount = slots.filter(hasReactionSlotSelection).length;
  const generatedProfileId = getGeneratedReactionProfileStableId();
  const actionSetContext = getCurrentReactionActionSetContext();
  const actionSetProfileId = actionSetContext.actionSet?.reactionProfileId || "";
  const bindingTone = actionSetContext.bindingTone;
  const bindingText = actionSetContext.bindingLabel;
  const selectedSlot = getSelectedReactionSlot(profile);
  const profileName = firstNonEmpty(profile.displayName, profile.profileId) === "Default reaction profile"
    ? "默认受击动作组"
    : firstNonEmpty(profile.displayName, "默认受击动作组");
  el.reactionProfilePanel.innerHTML = `
    <div class="reaction-profile-panel">
      <div class="reaction-profile-summary" data-reaction-summary="1">
        ${renderReactionSummaryCell("当前动作组", profileName, profile.profileId || DEFAULT_REACTION_PROFILE_ID)}
        ${renderReactionSummaryCell("槽位", String(slots.length), "规则槽位")}
        ${renderReactionSummaryCell("已选动画", String(selectedCount), "动画资源")}
        ${renderReactionSummaryCell("生成 Profile", generatedProfileId, "ReactionProfile")}
        ${renderReactionSummaryCell("ActionSet 绑定", bindingText, "reactionProfileId", bindingTone)}
      </div>
      ${renderReactionBindingStatus(actionSetContext, generatedProfileId, actionSetProfileId)}
      <p class="field-help reaction-profile-help">当前武器 ActionSet 绑定的受击动作规则；命中造成破聚、Broken 后续命中、死亡等条件都会从这里选择动作。</p>
      <div class="reaction-workbench">
        <section class="reaction-matrix" aria-label="受击动作组规则矩阵">
          ${renderReactionBreakRow(profile, slots)}
          ${renderReactionImpactMatrix(profile, slots)}
          ${renderReactionReservedRow(profile, slots)}
        </section>
        ${renderReactionSlotDetail(profile, selectedSlot)}
      </div>
    </div>
  `;
}

function renderCompilerOwnedReactionProjectionPanel() {
  const combined = getActiveCombinedWeaponConfiguration();
  const actionSetId = combined?.combinedWeaponConfigurationId || "";
  const actionSet = getCharacterActionSets().find(item => item?.stableId === actionSetId) || null;
  const reactionProfileId = actionSet?.reactionProfileId || "";
  const reactionProfiles = Array.isArray(state.characterReactionProfiles?.reactionProfiles)
    ? state.characterReactionProfiles.reactionProfiles
    : [];
  const reactionProfile = reactionProfiles.find(item => item?.stableId === reactionProfileId) || null;
  const sourceConfigurationId = combined?.reactionConfigurationId || "";
  const sourceConfiguration = (state.weaponLibrary?.reactionConfigurations || []).find(item =>
    item?.reactionConfigurationId === sourceConfigurationId) || null;
  const rules = Array.isArray(reactionProfile?.rules) ? reactionProfile.rules : [];
  const ready = Boolean(actionSet && reactionProfile);
  const rulesHtml = rules.map(rule => `<article class="behavior-summary-card">
    <header><div><strong>${escapeHtml(rule.trigger || "Reaction")}</strong><code>${escapeHtml(rule.actionId || "未绑定动作")}</code></div><span class="sync-badge ok">P${escapeHtml(String(rule.priority ?? 0))}</span></header>
    <div class="behavior-summary-grid">
      <span>压力状态<strong>${escapeHtml(rule.currentScatterPressureBand || "任意")}</strong></span>
      <span>受击方向组<strong>${escapeHtml(rule.reactionGroupId || "默认")}</strong></span>
      <span>最小冲击<strong>${escapeHtml(rule.minImpactForce == null ? "-" : String(rule.minImpactForce))}</strong></span>
      <span>最大冲击<strong>${escapeHtml(rule.maxImpactForce == null ? "-" : String(rule.maxImpactForce))}</strong></span>
    </div>
  </article>`).join("");
  el.reactionProfilePanel.innerHTML = `<div class="reaction-profile-panel" data-readonly-surface="true">
    <div class="reaction-profile-summary" data-reaction-summary="1">
      ${renderReactionSummaryCell("当前组合", combined?.displayName || actionSetId || "未选择", actionSetId)}
      ${renderReactionSummaryCell("受击配置源", sourceConfiguration?.displayName || sourceConfigurationId || "未绑定", sourceConfigurationId)}
      ${renderReactionSummaryCell("生成 ActionSet", actionSet?.stableId || "未生成", "只读")}
      ${renderReactionSummaryCell("生成 ReactionProfile", reactionProfileId || "未生成", "只读", ready ? "ok" : "error")}
      ${renderReactionSummaryCell("运行规则", String(rules.length), "编译结果")}
    </div>
    <div class="reaction-binding-status reaction-readonly-notice" data-reaction-binding-status="1">
      <div class="reaction-binding-status-head"><strong>只读视图 · 不可编辑</strong><div class="reaction-readonly-badges"><span class="sync-badge muted">只读</span><span class="sync-badge ${ready ? "ok" : "error"}">${ready ? "已生成" : "生成缺失"}</span></div></div>
      <p class="field-help reaction-profile-help">本页只展示 Unity 与 CharacterTest 实际读取的编译结果，不提供编辑、保存或重新生成操作。需要修改时，请前往“组合武器 → 受击配置”。</p>
      ${ready ? "" : `<div class="reaction-binding-issue error">当前组合的编译产物不完整。请回到组合武器检查受击配置绑定，然后保存并重新编译；不要在这里补写生成 JSON。</div>`}
    </div>
    <div class="reaction-workbench">
      <section class="reaction-matrix" aria-label="受击运行规则只读列表">${rulesHtml || `<div class="empty">当前 ReactionProfile 没有运行规则。</div>`}</section>
      <aside class="reaction-detail">
        <div class="reaction-detail-head"><div><strong>编译来源（只读审计）</strong><span>Schema 8 权威链路</span></div></div>
        <div class="reaction-detail-fields">
          ${renderReactionDetailField("组合武器", actionSetId || "未选择", true)}
          ${renderReactionDetailField("受击配置", sourceConfigurationId || "未绑定", true)}
          ${renderReactionDetailField("ActionSet", actionSet?.stableId || "未生成", true)}
          ${renderReactionDetailField("ReactionProfile", reactionProfileId || "未生成", true)}
          ${renderReactionDetailField("默认动作", reactionProfile?.defaultActionId || "未生成", true)}
        </div>
        <details class="behavior-readonly-json"><summary>只读 JSON 审计</summary><pre>${escapeHtml(JSON.stringify({ actionSet, reactionProfile }, null, 2))}</pre></details>
      </aside>
    </div>
  </div>`;
}

function renderReactionSlotCard(profile, slot) {
  return renderReactionSlotTile(profile, slot);
}

function renderReactionSummaryCell(label, value, hint = "", badgeTone = "") {
  const valueHtml = badgeTone
    ? `<strong class="sync-badge ${escapeHtml(badgeTone)}" title="${escapeHtml(value)}">${escapeHtml(value)}</strong>`
    : `<strong title="${escapeHtml(value)}">${escapeHtml(value)}</strong>`;
  return `<div class="reaction-profile-summary-cell">
    ${valueHtml}
    <span>${escapeHtml(label)}${hint ? ` · ${escapeHtml(hint)}` : ""}</span>
  </div>`;
}

function renderReactionBindingStatus(context, generatedProfileId, actionSetProfileId) {
  const handoffIssues = getReactionHandoffIssues();
  const needsHandoffRepair = handoffIssues.length > 0 || actionSetProfileId === "reaction.default";
  const weaponLabel = context.weapon
    ? `${getEquipSlotLabel(context.weapon.equipSlot)} / ${context.weapon.weaponId || "weapon"}`
    : "未聚焦武器";
  const actionSetId = context.actionSet?.stableId || "未找到 ActionSet";
  const equipmentState = context.actionSet?.equipmentStateStableId || "未声明";
  const movementProfile = context.actionSet?.movementProfileId || "未声明";
  const sourceLabel = context.isFallback
    ? "使用第一个 ActionSet 作为只读回退"
    : context.matchReason;
  const issue = context.issue
    ? `<div class="reaction-binding-issue ${escapeHtml(context.bindingTone)}">${escapeHtml(context.issue)}</div>`
    : "";
  const handoffIssueHtml = handoffIssues.length
    ? `<div class="reaction-binding-issue warn">${handoffIssues.slice(0, 3).map(item => `<div><strong>${escapeHtml(item.code || "CharacterAction handoff")}</strong>: ${escapeHtml(item.message || "")}</div>`).join("")}</div>`
    : "";
  const repairAction = needsHandoffRepair
    ? `<div class="reaction-binding-actions"><button type="button" data-reaction-regenerate="1">保存/重新生成 Reaction Profile</button></div>`
    : "";
  return `<div class="reaction-binding-status" data-reaction-binding-status="1">
    <div class="reaction-binding-status-head">
      <strong>绑定目标</strong>
      <span class="sync-badge ${escapeHtml(context.bindingTone)}">${escapeHtml(context.bindingLabel)}</span>
    </div>
    <div class="reaction-binding-grid">
      ${renderReactionBindingField("当前武器", weaponLabel)}
      ${renderReactionBindingField("ActionSet", actionSetId, true)}
      ${renderReactionBindingField("装备态", equipmentState, true)}
      ${renderReactionBindingField("移动配置", movementProfile, true)}
      ${renderReactionBindingField("绑定字段", actionSetProfileId || "未绑定", true)}
      ${renderReactionBindingField("生成目标", generatedProfileId, true)}
      ${renderReactionBindingField("上下文", sourceLabel)}
    </div>
    ${issue}
    ${handoffIssueHtml}
    ${repairAction}
  </div>`;
}

function getReactionHandoffIssues() {
  const stages = Array.isArray(state.readiness?.stages) ? state.readiness.stages : [];
  const stage = stages.find(item => item?.id === "ReactionBinding");
  const issues = [
    ...(Array.isArray(stage?.blockers) ? stage.blockers : []),
    ...(Array.isArray(stage?.warnings) ? stage.warnings : [])
  ];
  return issues.filter(isReactionHandoffIssue);
}

function isReactionHandoffIssue(issue) {
  const code = String(issue?.code || "");
  return code.startsWith("CHARACTER_ACTION_REACTION_")
    || code === "CHARACTER_ACTION_ACTION_SETS_JSON_INVALID"
    || code === "CHARACTER_ACTION_LIBRARY_JSON_INVALID"
    || code === "CHARACTER_ACTION_LIBRARY_MISSING";
}

function renderReactionBindingField(label, value, code = false) {
  const safeValue = String(value || "-");
  return `<div class="reaction-binding-field">
    <span>${escapeHtml(label)}</span>
    ${code ? `<code title="${escapeHtml(safeValue)}">${escapeHtml(safeValue)}</code>` : `<strong title="${escapeHtml(safeValue)}">${escapeHtml(safeValue)}</strong>`}
  </div>`;
}

function renderReactionBreakRow(profile, slots) {
  const slot = getReactionSlotById(slots, "cohesionBreak");
  return `<div class="reaction-matrix-section">
    <div class="reaction-matrix-section-head">
      <strong>造成破聚时</strong>
      <span>本次命中触发破聚时选择的受击动作</span>
    </div>
    <div class="reaction-matrix-row is-single">
      <div class="reaction-matrix-row-label">
        <strong>造成破聚时</strong>
        <span>本次命中触发破聚</span>
      </div>
      ${renderReactionSlotTile(profile, slot, { wide: true })}
    </div>
  </div>`;
}

function renderReactionImpactMatrix(profile, slots) {
  return `<div class="reaction-matrix-section">
    <div class="reaction-matrix-section-head">
      <strong>破聚后命中</strong>
      <span>Broken 状态下按受击位置和冲击强度分流</span>
    </div>
    <div class="reaction-matrix-grid">
      <div class="reaction-matrix-header"></div>
      <div class="reaction-matrix-header">上段</div>
      <div class="reaction-matrix-header">中段</div>
      <div class="reaction-matrix-header">下段</div>
      ${renderReactionMatrixRow(profile, slots, "普通冲击", "0-59", ["hit.normal.high", "hit.normal.mid", "hit.normal.low"])}
      ${renderReactionMatrixRow(profile, slots, "强冲击", ">=60", ["hit.strong.high", "hit.strong.mid", "hit.strong.low"])}
    </div>
  </div>`;
}

function renderReactionMatrixRow(profile, slots, label, range, slotIds) {
  return `
    <div class="reaction-matrix-row-label">
      <strong>${escapeHtml(label)}</strong>
      <span>Impact ${escapeHtml(range)}</span>
    </div>
    ${slotIds.map(slotId => renderReactionSlotTile(profile, getReactionSlotById(slots, slotId))).join("")}`;
}

function renderReactionReservedRow(profile, slots) {
  const slot = getReactionSlotById(slots, "death");
  return `<div class="reaction-matrix-section">
    <div class="reaction-matrix-section-head">
      <strong>预留规则</strong>
      <span>当前 CharacterTest 尚未主动触发</span>
    </div>
    <div class="reaction-matrix-row is-single">
      <div class="reaction-matrix-row-label">
        <strong>死亡</strong>
        <span>Death</span>
      </div>
      ${renderReactionSlotTile(profile, slot, { wide: true })}
    </div>
  </div>`;
}

function renderReactionSlotTile(profile, slot, options = {}) {
  if (!slot) {
    return `<div class="reaction-slot-tile is-empty${options.wide ? " is-wide" : ""}">
      <strong>未配置</strong>
      <span>槽位缺失</span>
    </div>`;
  }
  const definition = getReactionSlotDefinition(slot);
  const summary = getReactionSlotSelectionSummary(slot);
  const displayName = getReactionSlotDisplayName(slot);
  const trigger = getReactionTriggerLabel(slot, definition);
  const selected = state.selectedReactionSlotId === slot.slotId;
  const selectedTone = summary.hasSelection ? "ok" : slot.required ? "warn" : "muted";
  const generationTone = summary.generationTone;
  return `<button type="button" class="reaction-slot-tile${selected ? " is-selected" : ""}${options.wide ? " is-wide" : ""}" data-reaction-select="1" data-slot-id="${escapeHtml(slot.slotId)}" title="${escapeHtml(summary.keyText)}">
    <span class="reaction-slot-title">${escapeHtml(displayName)}</span>
    <span class="reaction-slot-subtitle">${escapeHtml(trigger)}</span>
    <span class="reaction-slot-status">
      <span class="sync-badge ${escapeHtml(selectedTone)}">${escapeHtml(summary.selectionLabel)}</span>
      <span class="sync-badge ${escapeHtml(generationTone)}">${escapeHtml(summary.generationLabel)}</span>
    </span>
    <span class="reaction-slot-resource">${escapeHtml(summary.selectedText)}</span>
    <span class="reaction-slot-meta">
      <span>${escapeHtml(slot.actionId || getGeneratedReactionActionId(state.package, slot))}</span>
      <span>${escapeHtml(String(slot.durationFrames || definition.durationFrames || 0))}f</span>
      <span>P${escapeHtml(String(slot.priority || definition.priority || 0))}</span>
    </span>
  </button>`;
}

function renderReactionSlotDetail(profile, slot) {
  if (!slot) {
    return `<aside class="reaction-detail"><div class="empty">选择一个受击槽位查看详情。</div></aside>`;
  }
  const definition = getReactionSlotDefinition(slot);
  const resource = findReactionSlotResource(slot);
  const summary = getReactionSlotSelectionSummary(slot, resource);
  const selection = slot.resourceSelection || {};
  const path = getReactionSlotPath(profile.profileId, slot.slotId);
  const displayName = getReactionSlotDisplayName(slot);
  const generatedActionId = slot.actionId || getGeneratedReactionActionId(state.package, slot);
  const availability = summary.availability;
  return `<aside class="reaction-detail" data-reaction-detail="${escapeHtml(slot.slotId)}">
    <div class="reaction-detail-head">
      <div>
        <strong>${escapeHtml(displayName)}</strong>
        <span>${escapeHtml(slot.slotId)}</span>
      </div>
      <div class="reaction-detail-badges">
        <span class="sync-badge ${escapeHtml(summary.hasSelection ? "ok" : slot.required ? "warn" : "muted")}">${escapeHtml(summary.selectionLabel)}</span>
        <span class="sync-badge ${escapeHtml(summary.generationTone)}">${escapeHtml(summary.generationLabel)}</span>
      </div>
    </div>
    <div class="reaction-detail-fields">
      ${renderReactionDetailField("触发", getReactionTriggerLabel(slot, definition))}
      ${renderReactionDetailField("动画选择", summary.selectionLabel)}
      ${renderReactionDetailField("生成状态", summary.generationLabel)}
      ${renderReactionDetailField("Trigger", firstNonEmpty(slot.trigger, definition.trigger, "-"), true)}
      ${renderReactionDetailField("PressureBand", slot.pressureBand || definition.pressureBand || "-")}
      ${renderReactionDetailField("ReactionGroup", getReactionGroupLabel(slot.reactionGroupId || definition.reactionGroupId || ""))}
      ${renderReactionDetailField("Impact", formatReactionImpactRange(slot, definition))}
      ${renderReactionDetailField("ActionId", generatedActionId, true)}
      ${renderReactionDetailField("动画资源", summary.selectedText)}
      ${renderReactionDetailField("Resource Key", summary.keyText, true)}
      ${availability ? renderReactionDetailField("资源状态", availability) : ""}
      ${renderReactionDetailField("Duration", `${slot.durationFrames || definition.durationFrames || 0}f`)}
      ${renderReactionDetailField("Priority", String(slot.priority || definition.priority || 0))}
    </div>
    <div class="reaction-detail-resource" title="${escapeHtml(summary.keyText)}">
      <span>${escapeHtml(summary.selectedText)}</span>
      <code>${escapeHtml(summary.keyText)}</code>
    </div>
    <div class="reaction-detail-actions">
      <button type="button" data-reaction-pick="1" data-profile-id="${escapeHtml(profile.profileId)}" data-slot-id="${escapeHtml(slot.slotId)}">选择动画</button>
      <button type="button" data-reaction-clear="1" data-profile-id="${escapeHtml(profile.profileId)}" data-slot-id="${escapeHtml(slot.slotId)}">清空</button>
      <button type="button" data-reaction-jump="1" data-path="${escapeHtml(path)}">定位配置</button>
      <button type="button" data-reaction-action="1" data-profile-id="${escapeHtml(profile.profileId)}" data-slot-id="${escapeHtml(slot.slotId)}">查看生成动作</button>
      <button type="button" data-reaction-editor="1" data-profile-id="${escapeHtml(profile.profileId)}" data-slot-id="${escapeHtml(slot.slotId)}">打开 Unity 动作编辑器</button>
    </div>
  </aside>`;
}

function renderReactionDetailField(label, value, code = false) {
  const text = String(value || "-");
  return `<div class="reaction-detail-field">
    <span>${escapeHtml(label)}</span>
    ${code ? `<code title="${escapeHtml(text)}">${escapeHtml(text)}</code>` : `<strong title="${escapeHtml(text)}">${escapeHtml(text)}</strong>`}
  </div>`;
}

function getReactionSlotById(slots, slotId) {
  return (slots || []).find(slot => slot?.slotId === slotId) || null;
}

function getSelectedReactionSlot(profile) {
  const slots = profile?.slots || [];
  if (!slots.length) {
    state.selectedReactionSlotId = "";
    return null;
  }
  const selected = slots.find(slot => slot?.slotId === state.selectedReactionSlotId);
  if (selected) return selected;
  const fallback = slots.find(slot => slot?.slotId === profile.defaultSlotId)
    || slots.find(slot => slot?.slotId === "cohesionBreak")
    || slots[0];
  state.selectedReactionSlotId = fallback?.slotId || "";
  return fallback || null;
}

function getReactionSlotDefinition(slotOrId) {
  const slotId = typeof slotOrId === "string" ? slotOrId : slotOrId?.slotId;
  return REACTION_PROFILE_SLOTS.find(item => item.slotId === slotId) || {};
}

function getReactionSlotDisplayName(slotOrId) {
  const slotId = typeof slotOrId === "string" ? slotOrId : slotOrId?.slotId;
  const labels = {
    cohesionBreak: "造成破聚时",
    "hit.normal.high": "普通 · 上段",
    "hit.normal.mid": "普通 · 中段",
    "hit.normal.low": "普通 · 下段",
    "hit.strong.high": "强冲击 · 上段",
    "hit.strong.mid": "强冲击 · 中段",
    "hit.strong.low": "强冲击 · 下段",
    death: "死亡"
  };
  if (labels[slotId]) return labels[slotId];
  const slot = typeof slotOrId === "string" ? null : slotOrId;
  const definition = getReactionSlotDefinition(slotOrId);
  return slot?.displayName || definition.displayName || slotId || "受击槽位";
}

function getReactionSlotTreeLabel(slot) {
  const selected = hasReactionSlotSelection(slot) ? "已选" : "未选择";
  return `${getReactionSlotDisplayName(slot)} ${selected}`;
}

function getReactionTriggerLabel(slot, definition = {}) {
  const trigger = firstNonEmpty(slot?.trigger, definition.trigger, "Reaction");
  const pressureBand = firstNonEmpty(slot?.pressureBand, definition.pressureBand);
  if (trigger === "CohesionBreak") return "命中造成破聚";
  if (trigger === "Hit" && pressureBand === "Broken") return "Broken 状态下命中";
  if (trigger === "Death") return "死亡";
  const triggerLabels = {
    CohesionBreak: "破聚",
    Hit: "命中",
    Death: "死亡",
    Reaction: "反应"
  };
  return `${triggerLabels[trigger] || trigger}${pressureBand ? ` / ${pressureBand}` : ""}`;
}

function getReactionGroupLabel(value) {
  const labels = {
    "reaction.height.high": "上段",
    "reaction.height.mid": "中段",
    "reaction.height.low": "下段"
  };
  return value ? `${labels[value] || value} (${value})` : "-";
}

function hasReactionSlotSelection(slot) {
  if (!slot) return false;
  const selection = slot.resourceSelection || {};
  return Boolean(firstNonEmpty(
    slot.resourceKey,
    selection.runtimeResourceKey,
    selection.packageResourceKey,
    selection.providerResourceKey,
    selection.resourceStableId,
    selection.unityAssetPath));
}

function getReactionSlotSelectionSummary(slot, resource = findReactionSlotResource(slot)) {
  const selection = slot?.resourceSelection || {};
  const hasSelection = hasReactionSlotSelection(slot);
  const selectedText = resource
    ? getResourceDisplayName(resource.resource || resource)
    : hasSelection
      ? firstNonEmpty(slot.resourceKey, selection.runtimeResourceKey, selection.packageResourceKey, selection.providerResourceKey, selection.unityAssetPath)
      : "未选择";
  const availability = resource?.runtimeAvailability || (selection.bindingKind === "UnityEditorOnlyAsset" ? "EditorOnly" : "");
  const keyText = hasSelection
    ? firstNonEmpty(slot.resourceKey, selection.runtimeResourceKey, selection.packageResourceKey, selection.providerResourceKey, selection.unityAssetPath, "-")
    : "-";
  const generatedAction = findGeneratedReactionAction(slot);
  const actionId = slot?.actionId || getGeneratedReactionActionId(state.package, slot || {});
  const hasActionId = Boolean(actionId);
  const generatedProfileId = getGeneratedReactionProfileStableId(state.package);
  const actionSetContext = getCurrentReactionActionSetContext();
  const savedBinding = actionSetContext.actionSet?.reactionProfileId === generatedProfileId;
  const selectionLabel = hasSelection ? "已选动画" : "未选动画";
  let generationLabel = "未保存";
  let generationTone = "muted";
  if (generatedAction) {
    generationLabel = "已生成";
    generationTone = "ok";
  } else if (hasSelection && savedBinding && hasActionId) {
    generationLabel = "已保存待 Unity 导入";
    generationTone = "warn";
  } else if (hasSelection && hasActionId) {
    generationLabel = "未保存";
    generationTone = "warn";
  } else if (!hasSelection) {
    generationLabel = "未保存";
    generationTone = slot?.required ? "warn" : "muted";
  }
  return {
    hasSelection,
    selectedText,
    availability,
    keyText,
    generatedAction,
    selectionLabel,
    generationLabel,
    generationTone
  };
}

function formatReactionImpactRange(slot, definition = {}) {
  const min = slot.minImpactForce ?? definition.minImpactForce ?? null;
  const max = slot.maxImpactForce ?? definition.maxImpactForce ?? null;
  if (min === null && max === null) return "-";
  if (min !== null && max !== null) return `${min}-${max}`;
  if (min !== null) return `>=${min}`;
  return `<=${max}`;
}

function findReactionSlotResource(slot) {
  return findAnimationSlotResource(slot);
}

function findGeneratedReactionAction(slot) {
  const actionId = slot?.actionId || getGeneratedReactionActionId(state.package, slot || {});
  if (!actionId) return null;
  return getResourceLibraryItems().find(item =>
    item?.sourceProviderId === "characterAction" &&
    item?.usage === "characterAction" &&
    getResourceIdentityValues(item).includes(actionId));
}

function getPrimaryActionSetReactionProfileId() {
  return getCurrentReactionActionSetContext().actionSet?.reactionProfileId || "";
}

function getCurrentReactionActionSetContext() {
  const actionSets = getCharacterActionSets();
  const generatedProfileId = getGeneratedReactionProfileStableId(state.package);
  const weapon = getReactionContextWeapon();
  const candidates = buildReactionActionSetCandidates(weapon, actionSets);
  const matched = candidates.find(candidate => candidate.actionSet);
  const fallback = matched || {
    actionSet: actionSets[0] || null,
    matchReason: actionSets.length ? "未匹配当前武器，回退到第一个 ActionSet" : "未加载 ActionSet",
    isFallback: true
  };
  const actionSet = fallback.actionSet || null;
  const boundProfile = actionSet?.reactionProfileId || "";
  let bindingLabel = "未绑定";
  let bindingTone = "muted";
  if (boundProfile === generatedProfileId) {
    bindingLabel = "已绑定";
    bindingTone = "ok";
  } else if (boundProfile) {
    bindingLabel = "与生成目标不一致";
    bindingTone = "warn";
  }

  const hasDeclaredWeaponBinding = Boolean(actionSet?.equipmentStateStableId || actionSet?.movementProfileId)
    && !String(actionSet?.stableId || "").endsWith(".default");
  const issue = actionSet
    ? (hasDeclaredWeaponBinding
      ? ""
      : "当前配置未声明武器/装备态绑定。保存会写入该 ActionSet 的 reactionProfileId，但不能证明它只属于当前武器。")
    : "未找到当前武器 ActionSet。";
  return {
    weapon,
    actionSet,
    generatedProfileId,
    boundProfile,
    bindingLabel,
    bindingTone,
    matchReason: fallback.matchReason,
    isFallback: Boolean(fallback.isFallback),
    issue
  };
}

function getCharacterActionSets() {
  return Array.isArray(state.characterActionSets?.actionSets) ? state.characterActionSets.actionSets : [];
}

function getReactionContextWeapon() {
  return getFocusedWeaponAttachment()
    || getCurrentOrFirstActiveWeaponAttachment()
    || (state.package?.geometry?.weaponAttachments || [])[0]
    || null;
}

function buildReactionActionSetCandidates(weapon, actionSets) {
  if (!Array.isArray(actionSets) || actionSets.length === 0) return [];
  const weaponId = String(weapon?.weaponId || "");
  const equipSlot = String(weapon?.equipSlot || "");
  const normalizedWeapon = normalizeActionIdSegment(weaponId);
  const normalizedSlot = normalizeActionIdSegment(equipSlot);
  const packageSegment = getCharacterActionPackageSegment(state.package);
  const tests = [
    {
      matchReason: "按当前武器 id 匹配",
      predicate: actionSet => containsActionSetToken(actionSet, weaponId) || containsActionSetToken(actionSet, normalizedWeapon)
    },
    {
      matchReason: "按装备槽位匹配",
      predicate: actionSet => containsActionSetToken(actionSet, equipSlot) || containsActionSetToken(actionSet, normalizedSlot)
    },
    {
      matchReason: "按包默认 ActionSet 匹配",
      predicate: actionSet => String(actionSet?.stableId || "") === `${packageSegment}.default`
    }
  ];
  const candidates = [];
  for (const test of tests) {
    const actionSet = actionSets.find(item => test.predicate(item)) || null;
    if (actionSet) candidates.push({ actionSet, matchReason: test.matchReason, isFallback: false });
  }
  return candidates;
}

function containsActionSetToken(actionSet, token) {
  const normalized = String(token || "").trim().toLowerCase();
  if (!normalized) return false;
  const fields = [
    actionSet?.stableId,
    actionSet?.displayName,
    actionSet?.equipmentStateStableId,
    actionSet?.movementProfileId,
    actionSet?.characterStableId
  ].map(value => String(value || "").toLowerCase());
  return fields.some(value => value.includes(normalized));
}

function showGeneratedReactionActionConfig(profileId, slotId) {
  const { slot } = findReactionSlot(profileId, slotId);
  if (!slot) return;
  const generatedAction = findGeneratedReactionAction(slot);
  const sourcePath = generatedAction?.metadata?.sourcePath || getCharacterActionPackageSourceFolder("libraries");
  const actionId = slot.actionId || getGeneratedReactionActionId(state.package, slot);
  state.message = generatedAction
    ? `Generated reaction action ${actionId}: ${sourcePath}`
    : `Reaction action ${actionId} will be generated by Unity import: ${sourcePath}`;
  if (navigator.clipboard?.writeText) {
    navigator.clipboard.writeText(`actionId=${actionId}; ${sourcePath}`).catch(() => {});
  }
  renderShellStatus();
}

function openReactionActionAuthoring(profileId, slotId) {
  const { slot } = findReactionSlot(profileId, slotId);
  const actionId = slot?.actionId || getGeneratedReactionActionId(state.package, slot || {});
  const text = `actionId=${actionId}; profile=${getGeneratedReactionProfileStableId()}; Unity menu: MxFramework/Character/Character Action Authoring -> Reaction profile / action`;
  state.message = `Copied reaction action location: ${text}`;
  if (navigator.clipboard?.writeText) {
    navigator.clipboard.writeText(text).catch(() => {});
  }
  renderShellStatus();
}

function renderAbilityLoadoutPanel() {
  if (!el.abilityLoadoutPanel) return;
  if (["6.0", "7.0", "8.0"].includes(String(state.package?.applicationConfig?.schemaVersion || ""))) {
    renderFrozenAbilityCompatibilityPanel();
    return;
  }
  if (String(state.package?.applicationConfig?.schemaVersion || "") === "4.0") {
    renderCurrentCombinedConfigurationShortcut("ability", el.abilityLoadoutPanel);
    return;
  }
  const chain = getAbilityChainView();
  if (!chain.snapshot) {
    el.abilityLoadoutPanel.innerHTML = `
      <div class="empty ability-chain-unavailable">
        请启动或刷新 Authoring 服务。能力页需要编译后的装备方案、槽位输入、CharacterAction 和 Runtime 绑定快照，静态源文件不足以得出有效能力。
      </div>`;
    return;
  }
  if (!chain.scenario) {
    el.abilityLoadoutPanel.innerHTML = `<div class="empty">编译结果中没有可用的装备方案。</div>`;
    return;
  }

  const rows = chain.rows;
  const slotReady = rows.filter(row => row.slotStatus === "Ready").length;
  const actionReady = rows.filter(row => row.actionStatus === "Ready").length;
  const runtimeReady = rows.filter(row => row.runtimeStatus === "Ready").length;
  const incomplete = rows.filter(row => row.overallStatus !== "Ready").length;
  const sourceCounts = chain.scenario.sourceCounts || {};
  const scenarioOptions = chain.scenarios.map(scenario => {
    const selected = scenario.scenarioId === chain.scenario.scenarioId ? " selected" : "";
    const suffix = scenario.isDefault ? "（默认）" : "";
    return `<option value="${escapeHtml(String(scenario.scenarioId || ""))}"${selected}>${escapeHtml(scenario.displayName || scenario.stableId || scenario.scenarioId)}${suffix}</option>`;
  }).join("");
  const diagnostics = Array.isArray(chain.scenario.diagnostics) ? chain.scenario.diagnostics : [];

  el.abilityLoadoutPanel.innerHTML = `
    <div class="ability-scenario-bar">
      <label>
        <span>装备方案</span>
        <select data-ability-scenario>${scenarioOptions}</select>
      </label>
      <div class="ability-scenario-context">
        <strong>${escapeHtml(chain.scenario.stableId || "-")}</strong>
        <span>装备状态 ${escapeHtml(chain.scenario.equipmentStateStableId || "未解析")}</span>
      </div>
    </div>
    <div class="ability-summary">
      <div><strong>${escapeHtml(String(rows.length))}</strong><span>当前有效授予</span></div>
      <div><strong>${escapeHtml(String(slotReady))}</strong><span>已有槽位输入</span></div>
      <div><strong>${escapeHtml(String(actionReady))}</strong><span>已有动作绑定</span></div>
      <div><strong>${escapeHtml(String(runtimeReady))}</strong><span>可运行</span></div>
      <div><strong>${escapeHtml(String(incomplete))}</strong><span>待补齐</span></div>
    </div>
    <div class="ability-origin-summary">
      <span>基础 ${escapeHtml(String(sourceCounts.base || 0))}</span>
      <span>武器 ${escapeHtml(String(sourceCounts.weapon || 0))}</span>
      <span>装备状态 ${escapeHtml(String(sourceCounts.equipmentState || 0))}</span>
      <span>角色可达能力 ${escapeHtml(String(chain.snapshot.potentialAbilityCount || 0))}</span>
    </div>
    <p class="field-help">本页按编译产物和 resolver 顺序展示：角色基础 → 已装备武器 → 当前装备状态。CharacterTest 的 1～9 试玩技能栏不属于这套角色授予数据。</p>
    ${state.dirty ? `<div class="ability-chain-diagnostics"><span>当前角色草稿尚未保存；以下快照仍对应磁盘上的最近保存版本。</span></div>` : ""}
    ${diagnostics.length ? `<div class="ability-chain-diagnostics">${diagnostics.map(item => `<span>${escapeHtml(item.code || "ABILITY_CHAIN")}: ${escapeHtml(item.message || "")}</span>`).join("")}</div>` : ""}
    <div class="ability-row-list">${rows.length ? rows.map(renderAbilityRow).join("") : `<div class="empty">该装备方案没有有效能力。</div>`}</div>`;
}

function renderFrozenAbilityCompatibilityPanel() {
  const combined = getActiveCombinedWeaponConfiguration();
  const configurationId = combined?.abilityConfigurationId || "";
  const snapshot = (state.combinedWeaponConfigurationsSnapshot?.combinedWeaponConfigurations || []).find(item =>
    item?.combinedWeaponConfigurationId === combined?.combinedWeaponConfigurationId);
  const chain = getAbilityChainView();
  const rows = chain?.rows || [];
  el.abilityLoadoutPanel.innerHTML = `<article class="frozen-ability-compatibility"><header><div><strong>实验性能力兼容诊断</strong><span>Frozen / Experimental Compatibility</span></div><span class="sync-badge">只读</span></header><p>当前游戏尚未建立正式能力创作逻辑。此处仅用于核对历史数据，不参与角色完成条件，也不能创建、修改、复制或绑定能力。</p><div class="single-weapon-readonly-summary"><section><h5>历史引用</h5><dl><dt>Ability Configuration</dt><dd>${escapeHtml(configurationId || "未配置")}</dd><dt>Generated AbilityLoadout</dt><dd>${escapeHtml(snapshot?.generatedAbilityLoadoutId || "未生成")}</dd><dt>Ability IDs</dt><dd>${escapeHtml(rows.length ? rows.map(row => row.abilityId).join(", ") : "无")}</dd></dl></section></div>${state.abilityChain ? `<details class="behavior-readonly-json"><summary>兼容快照与诊断</summary><pre>${escapeHtml(JSON.stringify({ configurationId, scenario: chain.scenario || null, diagnostics: chain.scenario?.diagnostics || [] }, null, 2))}</pre></details>` : `<button type="button" data-load-frozen-ability-diagnostics>加载高级兼容诊断</button>`}</article>`;
}

function renderAbilityRow(row) {
  const openStatus = getAbilityActionOpenStatus(row.abilityId);
  const openTone = getAbilityActionOpenTone(openStatus);
  const displayName = `能力 ${row.abilityId}`;
  const grantDetail = formatAbilityGrantDetail(row);
  const slotDetail = row.slotStatus === "Ready"
    ? formatAbilitySlotBindings(row.slotBindings)
    : firstNonEmpty(row.slotMessage, "无槽位，可用于被动或事件能力");
  const actionDetail = formatAbilityActionDetail(row);
  const runtimeDetail = row.runtimeStatus === "Ready" ? "Gameplay registry 已注册" : firstNonEmpty(row.runtimeMessage, "需要显式 Runtime 映射");
  const actionButtons = renderAbilityNextActions(row, openStatus);
  const overallTone = row.overallStatus === "Ready" ? "ok" : row.overallStatus === "Deferred" ? "warn" : "error";

  return `<article class="ability-row ${escapeHtml(overallTone)}">
    <div class="ability-row-main">
      <div>
        <strong>${escapeHtml(displayName)} <small>#${escapeHtml(String(row.abilityId))}</small></strong>
        <span>CharacterAbilityId</span>
      </div>
      <span class="sync-badge ${escapeHtml(overallTone)}">${escapeHtml(formatAbilityOverallStatus(row.overallStatus))}</span>
      ${openStatus ? `<span class="sync-badge ${escapeHtml(openTone)}">${escapeHtml(getAbilityActionOpenStatusLabel(openStatus))}</span>` : ""}
    </div>
    <div class="ability-stage-grid">
      ${renderAbilityStage("授予", row.grantedStatus, grantDetail)}
      ${renderAbilityStage("槽位输入", row.slotStatus, slotDetail)}
      ${renderAbilityStage("角色动作", row.actionStatus, actionDetail)}
      ${renderAbilityStage("运行时实现", row.runtimeStatus, runtimeDetail)}
    </div>
    <div class="ability-row-meta">
      <span><b>槽位</b>${escapeHtml(formatAbilitySlotBindings(row.slotBindings))}</span>
      <span><b>来源</b>${escapeHtml(formatAbilityGrantSources(row.grantSources))}</span>
      <span><b>动作</b>${escapeHtml(actionDetail)}</span>
      <span><b>下一步</b>${escapeHtml(formatAbilityNextAction(row.nextAction))}</span>
    </div>
    <div class="ability-row-actions">${actionButtons}</div>
  </article>`;
}

function getAbilityChainView() {
  const snapshot = state.abilityChain;
  const scenarios = Array.isArray(snapshot?.scenarios) ? snapshot.scenarios : [];
  const selectedId = String(state.activeWeaponConfigurationId || snapshot?.defaultScenarioId || scenarios[0]?.scenarioId || "");
  const scenario = scenarios.find(item => String(item?.configurationId || item?.scenarioId || "") === selectedId)
    || scenarios.find(item => item?.isDefault)
    || scenarios[0]
    || null;
  return {
    snapshot: snapshot || null,
    scenarios,
    scenario,
    rows: Array.isArray(scenario?.abilities) ? scenario.abilities : []
  };
}

function renderAbilityStage(label, status, detail) {
  const tone = getAbilityStageTone(status);
  return `<div class="ability-stage ${escapeHtml(tone)}">
    <div><span>${escapeHtml(label)}</span><b>${escapeHtml(formatAbilityStageStatus(status))}</b></div>
    <p>${escapeHtml(detail || "-")}</p>
  </div>`;
}

function getAbilityStageTone(status) {
  if (status === "Ready") return "ok";
  if (status === "Deferred" || status === "Unbound") return "warn";
  return "error";
}

function formatAbilityStageStatus(status) {
  if (status === "Ready") return "已就绪";
  if (status === "Deferred") return "待接入";
  if (status === "Unbound") return "无槽位";
  if (status === "MissingTarget") return "动作缺失";
  if (status === "Missing") return "缺失";
  return status || "未知";
}

function formatAbilityOverallStatus(status) {
  if (status === "Ready") return "可运行";
  if (status === "Deferred") return "运行时待接入";
  return "链路未完成";
}

function formatAbilityGrantDetail(row) {
  const sources = formatAbilityGrantSources(row?.grantSources);
  const slots = formatAbilitySlotBindings(row?.slotBindings);
  return `${sources} · ${slots}`;
}

function formatAbilityGrantSources(sources) {
  const values = Array.isArray(sources) ? sources : [];
  if (!values.length) return "未知来源";
  return values.map(source => {
    const label = source?.sourceKind === "weapon" ? "武器" : source?.sourceKind === "equipmentState" ? "装备状态" : "基础";
    return `${label} / ${source?.sourceStableId || source?.loadoutStableId || "-"}`;
  }).join("；");
}

function formatAbilitySlotBindings(bindings) {
  const values = Array.isArray(bindings) ? bindings : [];
  if (!values.length) return "未绑定槽位";
  return values.map(binding => `${binding?.slotId || "-"} / ${binding?.inputIntentId || "无输入"}`).join("；");
}

function formatAbilityActionDetail(row) {
  const bindings = Array.isArray(row?.actionBindings) ? row.actionBindings : [];
  if (!bindings.length) return "未绑定 CharacterAction";
  return bindings.map(binding => binding?.actionExists
    ? binding.actionId || "未命名动作"
    : `${binding?.actionId || "空动作"}（目标缺失）`).join("；");
}

function formatAbilityNextAction(action) {
  if (action === "BindCharacterAction") return "绑定 CharacterAction";
  if (action === "RepairCharacterAction") return "修复动作目标";
  if (action === "RegisterGameplayBinding") return "注册 Gameplay 实现";
  return "验证能力链";
}

function renderAbilityNextActions(row, openStatus) {
  const abilityId = escapeHtml(String(row.abilityId));
  const disabled = openStatus?.status === "pending" ? " disabled" : "";
  const fallback = openStatus?.status === "failed" ? ` data-ability-fallback="1"` : "";
  const actionButton = `<button type="button" data-ability-action="1" data-ability-id="${abilityId}"${fallback}${disabled}>${escapeHtml(getAbilityActionOpenButtonLabel(openStatus))}</button>`;
  if (row.nextAction === "RegisterGameplayBinding") {
    return `<button type="button" data-ability-runtime="1" data-ability-id="${abilityId}">查看运行时缺口</button>${actionButton}`;
  }
  return actionButton;
}

function openAbilityRuntimeDiagnostic(abilityId) {
  state.message = `能力 ${abilityId} 仍需 CharacterGameplayAbilityBindingCatalog 显式映射。`;
  state.activeContextTab = normalizeContextTab("validation");
  state.contextDockCollapsed = false;
  renderContextDockState();
  renderWorkflowBar();
  renderShellStatus();
}

function getReadinessStage(stageId) {
  const stages = Array.isArray(state.readiness?.stages) ? state.readiness.stages : [];
  return stages.find(stage => stage?.id === stageId) || null;
}

function getAbilityActionOpenStatus(abilityId) {
  const key = String(abilityId || "").trim();
  return key ? state.abilityActionOpenRequests?.[key] || null : null;
}

function setAbilityActionOpenStatus(abilityId, status) {
  const key = String(abilityId || "").trim();
  if (!key) return;
  state.abilityActionOpenRequests = {
    ...(state.abilityActionOpenRequests || {}),
    [key]: status
  };
  renderAbilityLoadoutPanel();
  renderShellStatus();
}

function getAbilityActionOpenTone(status) {
  if (!status) return "";
  if (status.status === "opened") return "ok";
  if (status.status === "failed" || status.status === "timedOut") return "error";
  return "warn";
}

function getAbilityActionOpenStatusLabel(status) {
  if (!status) return "";
  if (status.status === "opened") return "已打开";
  if (status.status === "failed") return "失败";
  if (status.status === "timedOut") return "超时";
  return "打开中";
}

function getAbilityActionOpenButtonLabel(status) {
  if (!status) return "打开动作配置";
  if (status.status === "pending") return "打开中...";
  if (status.status === "opened") return "再次打开";
  if (status.status === "failed" || status.status === "timedOut") return "重试打开";
  return "打开动作配置";
}

async function openAbilityActionAuthoring(abilityId) {
  const numericAbilityId = Number(abilityId);
  if (!Number.isFinite(numericAbilityId) || numericAbilityId <= 0) {
    state.message = "abilityId 无效，无法打开动作配置。";
    renderShellStatus();
    return;
  }

  setAbilityActionOpenStatus(numericAbilityId, {
    status: "pending",
    requestId: "",
    message: "正在请求 Unity 打开动作配置..."
  });
  state.message = `正在请求 Unity 打开动作配置：abilityId=${numericAbilityId}`;
  renderShellStatus();

  try {
    const response = await fetch("/api/character-action/open", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(buildAbilityActionOpenPayload(numericAbilityId))
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const result = await response.json();
    const requestId = result?.requestId || "";
    setAbilityActionOpenStatus(numericAbilityId, {
      status: result?.status || "pending",
      requestId,
      message: result?.message || "Unity 打开请求已排队。"
    });
    state.message = "Unity 打开请求已排队，正在等待 Editor 确认。";
    renderShellStatus();
    if (requestId) pollAbilityActionOpenStatus(numericAbilityId, requestId, 0);
  } catch (error) {
    const message = `请求 Unity 动作配置失败：${error?.message || error}`;
    setAbilityActionOpenStatus(numericAbilityId, {
      status: "failed",
      requestId: "",
      message
    });
    copyAbilityActionAuthoringFallback(numericAbilityId, message);
  }
}

function buildAbilityActionOpenPayload(abilityId) {
  return {
    package: state.packageRelative,
    abilityId,
    profileSourceDirectory: getCharacterActionPackageSourceFolder("profiles"),
    actionSetSourceDirectory: getCharacterActionPackageSourceFolder("actionSets"),
    actionLibrarySourceDirectory: CHARACTER_ACTION_HANDOFF.actionLibrarySourceDirectory,
    actionSetStableId: getPrimaryCharacterActionSetStableId(),
    mode: "focusOnly"
  };
}

function getPrimaryCharacterActionSetStableId() {
  const actionSets = Array.isArray(state.characterActionSets?.actionSets) ? state.characterActionSets.actionSets : [];
  return actionSets[0]?.stableId || "";
}

function pollAbilityActionOpenStatus(abilityId, requestId, attempt) {
  window.setTimeout(async () => {
    const status = await readJson(`/api/character-action/open-status?id=${encodeURIComponent(requestId)}`, null);
    if (["opened", "failed", "timedOut"].includes(status?.status)) {
      setAbilityActionOpenStatus(abilityId, {
        status: status.status,
        requestId,
        message: status.message || ""
      });
      state.message = status.message || (status.status === "opened" ? "Unity 已打开动作配置。" : "Unity 未能打开动作配置。");
      renderShellStatus();
      return;
    }

    if (attempt >= 8) {
      const message = "Unity 尚未确认打开请求。请确认 Unity Editor 已打开且脚本已编译。";
      setAbilityActionOpenStatus(abilityId, {
        status: "failed",
        requestId,
        message
      });
      return;
    }

    pollAbilityActionOpenStatus(abilityId, requestId, attempt + 1);
  }, attempt === 0 ? 500 : 1000);
}

function copyAbilityActionAuthoringFallback(abilityId, message = "") {
  const text = `abilityId=${abilityId || ""}; Unity menu: MxFramework/Character/Character Action Authoring -> Action Set -> ability binding`;
  state.message = message ? `${message} 已复制 fallback 位置：${text}` : `已复制动作配置位置：${text}`;
  if (navigator.clipboard?.writeText) {
    navigator.clipboard.writeText(text).catch(() => {});
  }
  renderShellStatus();
}

function renderBindingStatusLine(label, value, mode = "") {
  const modeClass = mode ? ` is-${escapeHtml(mode)}` : "";
  return `<span class="binding-line${modeClass}"><span class="binding-key">${escapeHtml(label)}</span><strong title="${escapeHtml(value)}">${escapeHtml(value)}</strong></span>`;
}

function getBoundBodyModelResource(pkg) {
  const packageValue = pkg || state.package;
  if (!packageValue) return null;
  const resources = getModelResources(packageValue);
  const boundByRole = resources.find(resource => isBodyModelBinding(resource, packageValue));
  if (boundByRole) return boundByRole;

  const profileKey = packageValue.geometry?.bodyProfile?.modelRootStableId || "";
  if (!profileKey) return null;
  return resources.find(resource => resource.resourceKey === profileKey || resource.stableId === profileKey) || null;
}

function renderResourcePlanPreviewLegacy() {
  if (!el.resourcePlanPreview) return;
  const plan = getCharacterResourcePlan();
  if (!plan) {
    el.resourcePlanPreview.innerHTML = `<div class="empty">还没有资源计划。请运行 Prefab 预检或 Unity 导入来生成 Runtime 诊断。</div>`;
    return;
  }
  el.resourcePlanPreview.innerHTML = PLAN_GROUPS.map(group => {
    const groupPlan = getPlanGroup(plan, group.key);
    const entries = normalizePlanEntries(groupPlan);
    const policy = groupPlan?.failurePolicy || group.failurePolicy;
    const required = groupPlan?.required === true ? "必需" : "可选";
    const rows = entries.length
      ? entries.slice(0, 5).map(entry => renderPlanEntry(entry)).join("")
      : `<div class="plan-entry empty">没有条目。</div>`;
    const more = entries.length > 5 ? `<div class="plan-entry meta">另有 ${entries.length - 5} 项</div>` : "";
    return `<section class="plan-group">
      <div class="plan-group-head"><strong>${escapeHtml(group.label)}</strong><span>${escapeHtml(required)} / ${escapeHtml(policy)}</span></div>
      ${rows}${more}
    </section>`;
  }).join("");
}

function getCharacterResourcePlan() {
  return state.resourcePlan || buildFallbackResourcePlan();
}

function getPlanGroup(plan, key) {
  if (!plan) return null;
  if (plan[key]) return plan[key];
  const pascal = key.charAt(0).toUpperCase() + key.slice(1);
  if (plan[pascal]) return plan[pascal];
  if (Array.isArray(plan.groups)) {
    return plan.groups.find(group => String(group.key || group.name || "").toLowerCase() === key.toLowerCase()) || null;
  }
  return null;
}

function normalizePlanEntries(groupPlan) {
  if (!groupPlan) return [];
  const entries = [];
  for (const key of ["resources", "resourceKeys", "requiredResources"]) {
    for (const value of groupPlan[key] || []) {
      entries.push(normalizePlanEntry(value, "resource"));
    }
  }
  for (const value of groupPlan.requiredCues || groupPlan.audioCues || []) {
    entries.push(normalizePlanEntry(value, "audioCue"));
  }
  for (const value of groupPlan.requiredBanks || groupPlan.banks || []) {
    entries.push(normalizePlanEntry(value, "bank"));
  }
  for (const value of groupPlan.entries || []) {
    entries.push(normalizePlanEntry(value, value.kind || "resource"));
  }
  return entries.filter(entry => entry.id);
}

function normalizePlanEntry(value, kind) {
  if (typeof value === "object" && value !== null) {
    return {
      id: String(value.resourceKey || value.audioCueId || value.bank || value.id || ""),
      kind: value.kind || kind,
      status: value.status || value.runtimeAvailability || "",
      sizeBytes: value.sizeBytes ?? value.size ?? null,
      policy: value.failurePolicy || value.loadPolicy || ""
    };
  }
  return { id: String(value || ""), kind, status: "", sizeBytes: null, policy: "" };
}

function renderPlanEntryLegacy(entry) {
  const item = findResourceLibraryItemByKey(entry.id);
  const status = entry.status || item?.runtimeAvailability || (entry.kind === "resource" ? "PendingCompile" : "External");
  return `<div class="plan-entry">
    <span>${escapeHtml(entry.kind)}</span>
    <code title="${escapeHtml(entry.id)}">${escapeHtml(entry.id)}</code>
    <strong>${escapeHtml(status)}</strong>
  </div>`;
}

function renderResourcePlanPreview() {
  if (!el.resourcePlanPreview) return;
  const plan = getCharacterResourcePlan();
  if (!plan) {
    el.resourcePlanPreview.innerHTML = `<div class="empty">还没有资源计划。请运行 Prefab 预检或 Unity 导入来生成完整 Runtime 诊断。</div>`;
    return;
  }

  const groups = PLAN_GROUPS.map(definition => {
    const groupPlan = getPlanGroup(plan, definition.key);
    const entries = normalizePlanEntries(groupPlan);
    const failurePolicy = groupPlan?.failurePolicy || definition.defaultFailurePolicy;
    const runtimeRequired = groupPlan?.required === true || definition.categoryKey === "required";
    return {
      definition,
      entries,
      failurePolicy,
      runtimeRequired
    };
  });

  const categoryHtml = PLAN_CATEGORIES.map(category => {
    const categoryGroups = groups.filter(group => group.definition.categoryKey === category.key);
    if (!categoryGroups.length) return "";
    const totalEntries = categoryGroups.reduce((sum, group) => sum + group.entries.length, 0);
    const cards = categoryGroups.map(group => renderPlanGroupCard(group)).join("");
    return `<section class="resource-plan-category ${escapeHtml(category.key)}">
      <header class="resource-plan-category-head">
        <strong>${escapeHtml(category.label)}</strong>
        <span>${escapeHtml(category.description)}</span>
        <span class="resource-plan-count">${escapeHtml(String(totalEntries))}</span>
      </header>
      <div class="resource-plan-category-grid">${cards}</div>
    </section>`;
  }).join("");

  const explanation = `<p class="resource-plan-note">此面板仅用于诊断。实际导入和源数据请在 Resource Manager / AnimationEditor 中管理。</p>`;
  el.resourcePlanPreview.innerHTML = `${explanation}${categoryHtml || `<div class="empty">没有可用资源组。</div>`}`;
}

function renderReadinessPanel() {
  if (!el.readinessFlow) return;
  const snapshot = state.readiness;
  if (!snapshot) {
    el.readinessFlow.innerHTML = `<div class="empty">还没有加载 readiness 快照。请通过 Authoring 服务打开 CharacterStudio。</div>`;
    renderWorkflowBar();
    renderContextDockState();
    return;
  }

  const stages = sortReadinessStages(Array.isArray(snapshot.stages) ? snapshot.stages : []);
  const blockers = Array.isArray(snapshot.blockers) ? snapshot.blockers.length : 0;
  const warnings = Array.isArray(snapshot.warnings) ? snapshot.warnings.length : 0;
  const overall = snapshot.overallStatus || "Unknown";
  const summary = `<div class="readiness-summary">
    <strong>${escapeHtml(snapshot.packageId || state.package?.manifest?.packageId || "character")}</strong>
    <span class="badge ${escapeHtml(getReadinessTone(overall))}">${escapeHtml(formatWorkflowStatusLabel(overall))}</span>
    <span>${escapeHtml(String(blockers))} 阻塞 / ${escapeHtml(String(warnings))} 警告</span>
  </div>`;

  const rows = stages.length
    ? stages.map(renderReadinessStage).join("")
    : `<div class="empty">readiness 没有返回阶段。</div>`;

  el.readinessFlow.innerHTML = summary + renderImportMaterialsReadinessSummary() + renderCharacterClothReadinessSummary() + `<div class="readiness-stage-list">${rows}</div>`;
  el.readinessFlow.querySelectorAll("button[data-readiness-jump]").forEach(button => {
    button.addEventListener("click", () => selectPath(button.dataset.readinessJump));
  });
  el.readinessFlow.querySelectorAll("button[data-readiness-tab]").forEach(button => {
    button.addEventListener("click", () => {
      state.activeContextTab = button.dataset.readinessTab || "materials";
      state.contextDockCollapsed = false;
      renderContextDockState();
    });
  });
  el.readinessFlow.querySelector("button[data-cloth-magica-open]")?.addEventListener("click", openCharacterClothAuthoring);
  renderWorkflowBar();
  renderContextDockState();
}

function renderCharacterClothReadinessSummary() {
  const cloth = state.clothMagica;
  if (!cloth?.configured) {
    return `<div class="readiness-summary import-materials-readiness">
      <strong>头发 / 布料物理</strong>
      <span class="badge pending">未配置</span>
      <span>Cloth 0 / Collider 0</span>
      <button type="button" data-cloth-magica-open>在 Unity 中创建</button>
    </div>`;
  }
  const status = String(cloth.status || "Unknown");
  const tone = status === "Ready" ? "ok" : (status === "Blocked" ? "error" : "warn");
  const label = status === "Ready" ? "就绪" : status === "Disabled" ? "已禁用" : status === "UnityPending" ? "Unity 待重建" : "阻断";
  const presets = Array.isArray(cloth.presets) && cloth.presets.length ? cloth.presets.join(", ") : "-";
  return `<div class="readiness-summary import-materials-readiness">
    <strong>头发 / 布料物理</strong>
    <span class="badge ${escapeHtml(tone)}">${escapeHtml(label)}</span>
    <span>Cloth ${escapeHtml(String(cloth.clothCount || 0))}</span>
    <span>Collider ${escapeHtml(String(cloth.colliderCount || 0))}</span>
    <span title="${escapeHtml(presets)}">Preset ${escapeHtml(presets)}</span>
    <button type="button" data-cloth-magica-open>在 Unity 中编辑</button>
  </div>`;
}

async function openCharacterClothAuthoring() {
  if (!state.apiAvailable || !state.packageRelative) return;
  try {
    const response = await fetch(`/api/character/cloth-magica/open?package=${encodeURIComponent(state.packageRelative)}`, { method: "POST" });
    const result = await response.json();
    if (!response.ok) throw new Error(result?.message || `HTTP ${response.status}`);
    state.message = "已请求 Unity 打开 Cloth Authoring。";
  } catch (error) {
    state.message = `无法打开 Unity Cloth Authoring：${error.message || error}`;
  }
  renderShellStatus();
}

function renderImportMaterialsReadinessSummary() {
  const payload = state.importMaterials;
  const summary = payload?.summary || null;
  if (!summary || !summary.total) {
    return `<div class="readiness-summary import-materials-readiness">
      <strong>源清单</strong>
      <span class="badge pending">未配置</span>
      <button type="button" data-readiness-tab="materials">打开</button>
    </div>`;
  }

  const blockerCount = (summary.missing || 0) + (summary.blocked || 0);
  const dirtyCount = summary.dirty || 0;
  const unityPending = summary.unityPending || 0;
  const tone = blockerCount ? "error" : (dirtyCount || unityPending ? "warn" : "ok");
  const label = blockerCount ? "阻塞" : (dirtyCount ? "有改动" : (unityPending ? "Unity 待处理" : "就绪"));
  return `<div class="readiness-summary import-materials-readiness">
    <strong>源清单</strong>
    <span class="badge ${escapeHtml(tone)}">${escapeHtml(label)}</span>
    <span>${escapeHtml(String(blockerCount))} 缺失/阻塞</span>
    <span>${escapeHtml(String(dirtyCount))} 改动/新增</span>
    <span>${escapeHtml(String(unityPending))} Unity 待处理</span>
    <button type="button" data-readiness-tab="materials">打开</button>
  </div>`;
}

function renderReadinessStage(stage) {
  const status = stage?.status || "Unknown";
  const tone = getReadinessTone(status);
  const blockers = Array.isArray(stage?.blockers) ? stage.blockers : [];
  const warnings = Array.isArray(stage?.warnings) ? stage.warnings : [];
  const issues = [...blockers, ...warnings].slice(0, 3);
  const paths = [
    ...(Array.isArray(stage?.sourcePaths) ? stage.sourcePaths : []),
    ...(Array.isArray(stage?.reportRefs) ? stage.reportRefs.map(ref => ref?.path).filter(Boolean) : [])
  ].filter(Boolean).slice(0, 4);
  const metrics = Object.entries(stage?.metrics || {})
    .filter(([key]) => !["evidencesource", "receiptpath", "receiptstatus"].includes(String(key || "").toLowerCase()))
    .slice(0, 4);
  const evidenceHtml = stage?.id === "UnityImport" ? renderUnityImportEvidence(stage) : "";
  const action = stage?.primaryToolUrl
    ? `<a href="${escapeHtml(stage.primaryToolUrl)}" class="button readiness-action">${escapeHtml(stage.actionLabel || "打开")}</a>`
    : "";
  const issueHtml = issues.length
    ? `<div class="readiness-issues">${issues.map(renderReadinessIssue).join("")}</div>`
    : `<div class="readiness-ok">没有阻塞项。</div>`;
  const pathHtml = paths.length
    ? `<div class="readiness-paths">${paths.map(path => `<code title="${escapeHtml(path)}">${escapeHtml(path)}</code>`).join("")}</div>`
    : "";
  const metricsHtml = metrics.length
    ? `<div class="readiness-metrics">${metrics.map(([key, value]) => `<span>${escapeHtml(key)}=${escapeHtml(value)}</span>`).join("")}</div>`
    : "";

  return `<article class="readiness-stage-row ${escapeHtml(tone)}">
    <header>
      <div>
        <strong>${escapeHtml(stage?.label || stage?.id || "阶段")}</strong>
        <p>${escapeHtml(stage?.summary || "")}</p>
      </div>
      <span class="badge ${escapeHtml(tone)}">${escapeHtml(formatWorkflowStatusLabel(status))}</span>
    </header>
    ${evidenceHtml}
    ${metricsHtml}
    ${issueHtml}
    ${pathHtml}
    <div class="readiness-actions">${action}</div>
  </article>`;
}

function getUnityImportEvidenceView(stage, snapshot = globalThis.state?.readiness) {
  const metrics = stage?.metrics || {};
  const evidenceSource = firstNonEmpty(stage?.evidenceSource, metrics.evidenceSource, snapshot?.evidenceSource);
  const receiptPath = firstNonEmpty(stage?.receiptPath, metrics.receiptPath, snapshot?.receiptPath);
  const receiptStatus = firstNonEmpty(stage?.receiptStatus, metrics.receiptStatus, snapshot?.receiptStatus) || "Missing";
  const normalizedSource = evidenceSource.toLowerCase();
  const normalizedReceiptStatus = receiptStatus.toLowerCase();
  const stageStatus = String(stage?.status || "Unknown");
  const normalizedStageStatus = stageStatus.toLowerCase();
  const editorStatus = firstNonEmpty(stage?.editorStatus, metrics.editorStatus, snapshot?.editorStatus);
  const normalizedEditorStatus = editorStatus.toLowerCase();

  if (normalizedSource === "currentunitysession") {
    const sessionBlocked = normalizedStageStatus === "blocked" || normalizedEditorStatus === "failed" || normalizedEditorStatus === "error";
    const sessionReady = normalizedStageStatus === "ready" && (!normalizedEditorStatus || normalizedEditorStatus === "completed");
    if (sessionBlocked) {
      return {
        source: "currentUnitySession",
        label: "当前 Unity 会话（验收失败）",
        tone: "error",
        statusLabel: editorStatus || stageStatus,
        receiptPath,
        receiptStatus,
        detail: "本次 Unity Editor 会话未通过验收；当前失败优先于旧的持久验收凭据，请查看阻断诊断后重新导入。"
      };
    }
    if (!sessionReady) {
      return {
        source: "currentUnitySession",
        label: "当前 Unity 会话（验收中）",
        tone: "pending",
        statusLabel: editorStatus || stageStatus,
        receiptPath,
        receiptStatus,
        detail: normalizedEditorStatus === "pending"
          ? "本次 Unity Editor 验收已排队，尚未完成；完成前不视为 Ready。"
          : "本次 Unity Editor 正在验收当前 Compiler 身份；完成前不视为 Ready。"
      };
    }
    return {
      source: "currentUnitySession",
      label: "当前 Unity 会话",
      tone: "ok",
      statusLabel: editorStatus || stageStatus,
      receiptPath,
      receiptStatus,
      detail: "当前 packageId 与五类 Compiler hash 已由本次 Unity Editor 会话验收。"
    };
  }
  if (normalizedSource === "persistentreceipt") {
    return {
      source: "persistentReceipt",
      label: "持久验收凭据",
      tone: normalizedReceiptStatus === "current" ? "ok" : getReadinessTone(stageStatus),
      receiptPath,
      receiptStatus,
      detail: "当前 packageId 与五类 Compiler hash 已由可版本管理的 Unity 导入验收凭据证明。"
    };
  }

  if (normalizedReceiptStatus === "rejected") {
    return {
      source: "persistentReceipt",
      label: "上次 Unity 验收被拒绝",
      tone: "error",
      receiptPath,
      receiptStatus,
      detail: "回执格式有效，但至少一个 Unity Editor 验收阶段失败。请查看结构化阻断项，修复后重新运行 Unity Editor 验收。"
    };
  }

  if (normalizedReceiptStatus === "invalid" || normalizedReceiptStatus === "criticalartifactmissing") {
    return {
      source: "none",
      label: normalizedReceiptStatus === "invalid" ? "验收凭据无效" : "验收产物缺失",
      tone: "error",
      receiptPath,
      receiptStatus,
      detail: normalizedReceiptStatus === "invalid"
        ? "持久验收凭据损坏、package 不匹配或包含非法路径；请修复后重新导入。"
        : "凭据声明 Ready，但关键 Unity 产物不存在；请重新执行 Unity 导入。"
    };
  }

  const stale = normalizedReceiptStatus === "stale";
  return {
    source: "none",
    label: stale ? "源数据已更新，待导入 Unity" : "尚未执行 Unity 导入",
    tone: "pending",
    receiptPath,
    receiptStatus,
    detail: stale
      ? "保存后的源包版本比上次 Unity 验收更新。这是正常的待同步状态，重新执行一次 Unity 导入即可。"
      : "当前 Compiler 身份尚未经过 Unity 验收；这不是角色内容错误，请执行 Unity 导入。"
  };
}

function renderUnityImportEvidence(stage) {
  const evidence = getUnityImportEvidenceView(stage);
  const receiptStatus = evidence.receiptStatus || "Missing";
  const statusLabel = evidence.statusLabel || receiptStatus;
  const path = evidence.receiptPath
    ? `<code title="${escapeHtml(evidence.receiptPath)}">${escapeHtml(evidence.receiptPath)}</code>`
    : "";
  return `<div class="unity-import-evidence ${escapeHtml(evidence.tone)}">
    <span>验收来源</span>
    <strong>${escapeHtml(evidence.label)}</strong>
    <span class="badge ${escapeHtml(evidence.tone)}">${escapeHtml(statusLabel)}</span>
    <p>${escapeHtml(evidence.detail)}</p>
    ${path}
  </div>`;
}

function renderReadinessIssue(issue) {
  const jumpPath = normalizeIssuePath(issue?.sourceObjectPath || issue?.sourcePath || "");
  const severityText = String(issue?.severity || "").toLowerCase();
  const severity = severityText.includes("error") || severityText.includes("block") ? "blocker" : "warning";
  const record = {
    issue,
    stageId: inferWorkflowIssueStageId(issue, ""),
    path: jumpPath,
    code: String(issue?.code || issue?.gate || ""),
    message: String(issue?.message || "")
  };
  const card = {
    ...describeWorkflowIssue(record, severity),
    severity,
    tone: severity === "blocker" ? "error" : "warn",
    action: buildWorkflowIssueAction(record, severity)
  };
  return renderWorkflowIssueCard(card);
}

function getReadinessTone(status) {
  const value = String(status || "").toLowerCase();
  if (value === "ready") return "ok";
  if (value === "blocked") return "error";
  if (value === "warning" || value === "deferred") return "warn";
  return "pending";
}

function sortReadinessStages(stages) {
  return [...stages].sort((left, right) => {
    const leftIndex = READINESS_STAGE_ORDER.indexOf(left?.id || "");
    const rightIndex = READINESS_STAGE_ORDER.indexOf(right?.id || "");
    return (leftIndex < 0 ? 99 : leftIndex) - (rightIndex < 0 ? 99 : rightIndex);
  });
}

function renderPlanGroupCard(groupView) {
  const { definition, entries, failurePolicy, runtimeRequired } = groupView;
  const previewEntries = entries.length
    ? entries.slice(0, 4).map(entry => renderPlanEntry(entry)).join("")
    : `<div class="plan-entry empty">没有资源。</div>`;
  const more = entries.length > 4 ? `<div class="plan-entry meta">另有 ${entries.length - 4} 项</div>` : "";
  const consequence = describePlanConsequence(definition.consequence, failurePolicy);
  return `<article class="plan-group-card">
    <div class="plan-group-head">
      <strong>${escapeHtml(definition.authorLabel)}</strong>
      <span class="plan-importance ${escapeHtml(definition.categoryKey)}">${escapeHtml(definition.categoryLabel)}</span>
    </div>
    <p class="plan-group-summary">${escapeHtml(definition.authorSummary)}</p>
    <p class="plan-group-consequence"><span>缺失时：</span> ${escapeHtml(consequence)}</p>
    <div class="plan-entry-list">${previewEntries}${more}</div>
    <details class="plan-advanced">
      <summary>高级诊断</summary>
      <div class="plan-advanced-grid">
        <div><span>内部组</span><strong>${escapeHtml(definition.internalLabel)}</strong></div>
        <div><span>失败策略</span><strong>${escapeHtml(failurePolicy)}</strong></div>
        <div><span>Runtime 必需</span><strong>${runtimeRequired ? "是" : "否"}</strong></div>
        <div><span>条目数</span><strong>${escapeHtml(String(entries.length))}</strong></div>
      </div>
    </details>
  </article>`;
}

function describePlanConsequence(defaultText, failurePolicy) {
  const policy = String(failurePolicy || "").toLowerCase();
  if (policy === "failspawn") return "资产有效前会阻塞 Spawn。";
  if (policy === "usefallbackequipment") return "使用备用装备表现。";
  if (policy === "usefallbackpose") return "使用备用动画姿态。";
  if (policy === "skipeffect") return "跳过特效播放。";
  if (policy === "showplaceholder") return "显示占位 UI 资源。";
  if (policy === "mutemissingcue") return "静音缺失音频 cue。";
  return defaultText || "Runtime 使用 fallback 行为。";
}

function renderPlanEntry(entry) {
  const item = findResourceLibraryItemByKey(entry.id);
  const status = entry.status || item?.runtimeAvailability || (entry.kind === "resource" ? "PendingCompile" : "External");
  const kind = entry.kind || "resource";
  return `<div class="plan-entry">
    <span>${escapeHtml(kind)}</span>
    <code title="${escapeHtml(entry.id)}">${escapeHtml(entry.id)}</code>
    <strong>${escapeHtml(status)}</strong>
  </div>`;
}

function buildFallbackResourcePlan() {
  const pkg = state.package;
  if (!pkg) return null;
  const bodyKeys = getModelResources(pkg).filter(resource => isBodyModelBinding(resource, pkg)).map(resource => resource.resourceKey);
  const equipmentKeys = (pkg.geometry?.weaponAttachments || []).map(attachment => getWeaponAttachmentResourceKey(attachment)).filter(Boolean);
  const selectedAnimationKeys = collectAnimationSlotResourceKeys(pkg);
  const catalogAnimationKeys = (pkg.resourceCatalog?.entries || []).filter(resource => mapResourceKind(resource.typeId) === "Animation").map(resource => resource.resourceKey).filter(Boolean);
  const animationKeys = Array.from(new Set([...selectedAnimationKeys, ...catalogAnimationKeys]));
  const uiKeys = (pkg.resourceCatalog?.entries || []).filter(resource => resource.usage === "previewThumbnail" || mapResourceKind(resource.typeId) === "Texture").map(resource => resource.resourceKey).filter(Boolean);
  const vfxKeys = (pkg.resourceCatalog?.entries || []).filter(resource => mapResourceKind(resource.typeId) === "Vfx").map(resource => resource.resourceKey).filter(Boolean);
  const audioKeys = (pkg.resourceCatalog?.entries || []).filter(resource => mapResourceKind(resource.typeId) === "Audio").map(resource => resource.resourceKey || resource.stableId).filter(Boolean);
  return {
    characterStableId: pkg.applicationConfig?.characterStableId || pkg.manifest?.packageId || "",
    spawnCritical: { required: true, resources: bodyKeys, failurePolicy: "FailSpawn" },
    equipmentInitial: { required: true, resources: equipmentKeys, failurePolicy: "UseFallbackEquipment" },
    animationWarmup: { required: false, resources: animationKeys, failurePolicy: "UseFallbackPose" },
    vfxWarmup: { required: false, resources: vfxKeys, failurePolicy: "SkipEffect" },
    uiDeferred: { required: false, resources: uiKeys, failurePolicy: "ShowPlaceholder" },
    audio: { required: false, requiredCues: audioKeys, failurePolicy: "MuteMissingCue" }
  };
}

function collectAnimationSlotResourceKeys(pkg) {
  const keys = [];
  for (const group of pkg?.applicationConfig?.animationGroups || []) {
    if (group?.sourceResourceKey) keys.push(group.sourceResourceKey);
  }
  for (const profile of pkg?.applicationConfig?.animationProfiles || []) {
    for (const slot of profile?.slots || []) {
      const key = firstNonEmpty(
        slot.resourceKey,
        slot.resourceSelection?.runtimeResourceKey,
        slot.resourceSelection?.packageResourceKey);
      if (key) keys.push(key);
    }
  }
  return keys;
}

function cleanupUnusedAnimationResourceKey(resourceKey) {
  if (!resourceKey) return;
  const appConfig = state.package?.applicationConfig;
  if (!Array.isArray(appConfig?.resourceKeys) || appConfig.resourceKeys.length === 0) return;
  if (!appConfig.resourceKeys.includes(resourceKey)) return;
  const references = collectResourceReferences({ resourceKey }) || [];
  const hasExplicitReference = references.some(reference => !(reference.sourceConfigKind === "character" && reference.sourceField === "resourceKeys"));
  const resource = findPackageResourceByKey(state.package, resourceKey);
  if (!hasExplicitReference && !isBodyModelBinding(resource, state.package)) {
    removeApplicationResourceKey(resourceKey);
  }
}

function getModelResources(pkg) {
  return (pkg?.resourceCatalog?.entries || [])
    .filter(resource => resource && resource.typeId === "model" && resource.resourceKey)
    .sort((a, b) => getResourceSortKey(a, pkg).localeCompare(getResourceSortKey(b, pkg)));
}

function getResourceSortKey(resource, pkg) {
  const rank = isBodyModelBinding(resource, pkg)
    ? "0"
    : isWeaponResourceBinding(resource, pkg, "mainHand")
      ? "1"
      : isWeaponResourceBinding(resource, pkg, "offHand")
        ? "2"
        : "3";
  return `${rank}:${getResourceDisplayName(resource)}`;
}

function getResourceDisplayName(resource) {
  const source = resource.provenance?.sourceFile || resource.localId || resource.resourceKey || resource.relativePath || "model";
  const name = String(source).split("/").pop().replace(/\.(glb|gltf|fbx)$/i, "");
  return name || "model";
}

function getResourceInitial(resource) {
  const name = resource?.displayName || getResourceDisplayName(resource);
  return name.slice(0, 2).toUpperCase();
}

function describeResourceBinding(resource, pkg) {
  const bindings = [];
  if (isBodyModelBinding(resource, pkg)) bindings.push("Body model");
  for (const attachment of pkg?.geometry?.weaponAttachments || []) {
    if (getWeaponAttachmentResourceKey(attachment) === resource.resourceKey) {
      bindings.push(`${attachment.equipSlot || "slot"}:${attachment.weaponId || "weapon"}`);
    }
  }
  if (!bindings.length && isApplicationResourceReference(resource.resourceKey, pkg)) {
    bindings.push("Application resource reference");
  }
  return bindings.join(" / ");
}

function isWeaponResourceBinding(resource, pkg, equipSlot) {
  if (!resource?.resourceKey) return false;
  return (pkg?.geometry?.weaponAttachments || []).some(attachment =>
    getWeaponAttachmentResourceKey(attachment) === resource.resourceKey && String(attachment?.equipSlot || "") === equipSlot);
}

function getResourceThumbnailUrl(resource, pkg) {
  const previewKey = resource.preview?.thumbnailResourceKey || resource.preview?.placeholderResourceKey || "";
  const preview = (pkg?.resourceCatalog?.entries || []).find(entry => entry.resourceKey === previewKey);
  if (!preview?.relativePath) return "";
  return encodeURI(`/${state.packageRelative}/${preview.relativePath}`);
}

function getUnityCatalogEntries() {
  return Array.isArray(state.unityResourceCatalog?.entries) ? state.unityResourceCatalog.entries : [];
}

function getUnityImportReport() {
  return state.importResult?.report || state.importResult || null;
}

function getUnityImportReportFromResult(result) {
  if (!result) return null;
  if (result.report) return result.report;
  return Array.isArray(result.operations) ? result : null;
}

function classifyUnityImportOperation(operation) {
  const action = String(operation?.action || "").toLowerCase();
  if (action.includes("error") || action.includes("failed") || action.includes("conflict")) return "problem";
  if (action.includes("skipped")) return "upToDate";
  if (action.includes("added") || action.includes("updated") || action.includes("created") || action.includes("recreated") || action.includes("copied")) {
    return "changed";
  }
  return "other";
}

function getUnityImportOperationLabel(operation) {
  const category = classifyUnityImportOperation(operation);
  if (category === "problem") return "问题";
  if (category === "upToDate") return "已是最新";
  if (category === "changed") return "已变更";
  return operation?.action || "操作";
}

function getUnityImportOperationTone(operation) {
  const category = classifyUnityImportOperation(operation);
  if (category === "problem") return "error";
  if (category === "changed" || category === "upToDate") return "ok";
  return "";
}

function summarizeUnityImportOperations(report) {
  const operations = Array.isArray(report?.operations) ? report.operations : [];
  const summary = {
    total: operations.length,
    changed: 0,
    upToDate: 0,
    problem: 0,
    other: 0,
    visibleOperations: [],
    upToDateOperations: []
  };
  for (const operation of operations) {
    const category = classifyUnityImportOperation(operation);
    if (category === "changed") summary.changed++;
    else if (category === "upToDate") summary.upToDate++;
    else if (category === "problem") summary.problem++;
    else summary.other++;

    if (category === "upToDate") summary.upToDateOperations.push(operation);
    else summary.visibleOperations.push(operation);
  }
  if (typeof report?.addedCount === "number" || typeof report?.updatedCount === "number") {
    summary.changed = (report.addedCount || 0) + (report.updatedCount || 0);
  }
  if (typeof report?.skippedCount === "number") summary.upToDate = report.skippedCount;
  if (typeof report?.conflictCount === "number" || typeof report?.errorCount === "number") {
    summary.problem = (report.conflictCount || 0) + (report.errorCount || 0);
  }
  return summary;
}

function getUnityImportReportSummary(report) {
  const operations = summarizeUnityImportOperations(report);
  const status = report?.status || "Unknown";
  const ready = String(status).toLowerCase() === "ready";
  const upToDateOnly = ready && operations.changed === 0 && operations.problem === 0 && operations.upToDate > 0;
  if (operations.problem > 0) {
    return { title: status, label: `${operations.problem} 个问题`, tone: "error", operations };
  }
  if (upToDateOnly) {
    return { title: "Unity 导出已是最新", label: "已是最新", tone: "ok", operations };
  }
  if (operations.changed > 0) {
    return { title: status, label: `${operations.changed} 项变更`, tone: "ok", operations };
  }
  return { title: status, label: ready ? "就绪" : formatWorkflowStatusLabel(status), tone: ready ? "ok" : "warn", operations };
}

function startUnityImportTask() {
  state.unityImportTask = {
    running: true,
    status: "running",
    startedAt: new Date().toISOString(),
    finishedAt: "",
    message: "正在运行 character import-unity，然后请求 Unity Editor 刷新 Prefab 和 CharacterAction。",
    steps: buildUnityImportTaskSteps(null, { running: true })
  };
}

function finishUnityImportTask(success, result, responseOk = true) {
  const acceptance = buildUnityImportAcceptance(result);
  const commandSucceeded = responseOk && acceptance.status !== "failed";
  const status = success ? "success" : (commandSucceeded ? "attention" : "failed");
  const message = success
    ? "Unity 导入完成。资产写入、已是最新项、Prefab 重建和 CharacterAction 状态会分开展示。"
    : commandSucceeded
      ? "Unity 导入已结束，但 Unity Editor 验收未完成。请查看下方验收摘要。"
      : "Unity 导入失败。命令输出和报告状态保留在下方。";
  state.unityImportTask = {
    ...(state.unityImportTask || {}),
    running: false,
    status,
    responseOk,
    finishedAt: new Date().toISOString(),
    message,
    result,
    steps: buildUnityImportTaskSteps(result, { status, success })
  };
}

function buildUnityImportTaskSteps(result, options = {}) {
  const running = Boolean(options.running);
  const report = getUnityImportReportFromResult(result);
  const operations = Array.isArray(report?.operations) ? report.operations : [];
  const commandSucceeded = isUnityImportCommandSuccess(result, report);
  if (running) {
    return [
      unityImportStep("command", "运行 import-unity 命令", "running", "正在执行 Authoring CLI。"),
      unityImportStep("catalog", "写入 Unity Resource Catalog", "pending", "等待导入报告。"),
      unityImportStep("artifact", "生成 Unity 导入产物", "pending", "等待产物元数据。"),
      unityImportStep("resources", "同步 Unity 资产文件", "pending", "等待导入报告。"),
      unityImportStep("commonResources", "校验公共复用资源", "pending", "等待公共资源状态。"),
      unityImportStep("assetDatabase", "导入 Unity AssetDatabase", "pending", "等待 Unity Editor bridge。"),
      unityImportStep("avatarSource", "校验 Humanoid Avatar Source", "pending", "等待 Unity Editor Avatar source 状态。"),
      unityImportStep("prefab", "在 Unity Editor 中重建角色 Prefab", "pending", "等待 Unity Editor bridge。"),
      unityImportStep("weaponTraceBake", "烘焙武器命中轨迹", "pending", "等待 Unity Editor 轨迹烘焙校验。"),
      unityImportStep("socketBake", "烘焙动画挂点轨迹", "pending", "等待 Unity Editor 挂点烘焙校验。"),
      unityImportStep("hurtboxBake", "烘焙动画受击体", "pending", "等待 Unity Editor 受击体烘焙校验。"),
      unityImportStep("characterAction", "刷新 CharacterAction Profile / ActionSet", "pending", "等待 Unity Editor bridge。"),
      unityImportStep("refresh", "刷新 CharacterStudio 状态", "pending", "导入后重新加载 catalog、资源计划和 readiness。")
    ];
  }
  if (!result) return [];
  const operationSummary = summarizeUnityImportOperations(report);
  const catalogEvidence = hasImportOperation(operations, op =>
    String(op.kind || "").toLowerCase() === "unityresourcecatalog"
    || /unity_resource_catalog\.json/i.test(String(op.targetPath || op.sourcePath || "")));
  const resourceEvidence = hasImportOperation(operations, op => {
    const kind = String(op.kind || "").toLowerCase();
    return kind === "resourcefile" || kind === "packagecache" || kind === "generatedconfigpatch" || kind === "geometrybinding" || kind === "resourcemapping";
  });
  const artifactEvidence = hasImportOperation(operations, op =>
    Boolean(op.artifactHash || op.artifactStatus || (Array.isArray(op.artifactTransforms) && op.artifactTransforms.length)));
  const editorRebuild = getUnityEditorRebuildStatus(result);
  const editorRebuildStepStatus = getUnityEditorRebuildStepStatus(editorRebuild, commandSucceeded);
  const prefabStepStatus = editorRebuildStepStatus || (commandSucceeded ? "info" : "error");
  const actionStepStatus = getUnityEditorCharacterActionStepStatus(editorRebuild, commandSucceeded);
  const assetDatabaseStepStatus = getUnityEditorAssetDatabaseStepStatus(editorRebuild, commandSucceeded);
  const avatarSourceStepStatus = getUnityEditorAvatarSourceStepStatus(editorRebuild, commandSucceeded);
  const commonResourcesStepStatus = getCommonResourcesUnityImportStepStatus(commandSucceeded);
  const weaponTraceBakeStepStatus = getUnityEditorWeaponTraceBakeStepStatus(editorRebuild, commandSucceeded);
  const socketBakeStepStatus = getUnityEditorSocketBakeStepStatus(editorRebuild, commandSucceeded);
  const hurtboxBakeStepStatus = getUnityEditorHurtboxBakeStepStatus(editorRebuild, commandSucceeded);
  return [
    unityImportStep(
      "command",
      "运行 import-unity 命令",
      commandSucceeded ? "ok" : "error",
      getUnityImportCommandDetail(result, report)
    ),
    unityImportStep(
      "catalog",
      "写入 Unity Resource Catalog",
      catalogEvidence || state.unityResourceCatalog ? "ok" : (commandSucceeded ? "warn" : "error"),
      catalogEvidence ? "导入报告包含 Unity Resource Catalog 写入证据。" : (state.unityResourceCatalog ? "页面已重新加载 Unity Catalog。" : "导入报告未明确记录 Unity Catalog 写入证据。")
    ),
    unityImportStep(
      "artifact",
      "生成 Unity 导入产物",
      artifactEvidence || (report?.artifactCount || 0) > 0 ? "ok" : (commandSucceeded ? "info" : "error"),
      getUnityArtifactStepDetail(report, operationSummary)
    ),
    unityImportStep(
      "resources",
      "同步 Unity 资产文件",
      resourceEvidence || operationSummary.total > 0 ? "ok" : (commandSucceeded ? "info" : "error"),
      getUnityResourceSyncDetail(operationSummary)
    ),
    unityImportStep(
      "commonResources",
      "校验公共复用资源",
      commonResourcesStepStatus,
      getCommonResourcesUnityImportStepDetail()
    ),
    unityImportStep(
      "assetDatabase",
      "导入 Unity AssetDatabase",
      assetDatabaseStepStatus,
      getUnityAssetDatabaseStepDetail(editorRebuild, commandSucceeded)
    ),
    unityImportStep(
      "avatarSource",
      "校验 Humanoid Avatar Source",
      avatarSourceStepStatus,
      getUnityEditorAvatarSourceStepDetail(editorRebuild, commandSucceeded)
    ),
    unityImportStep(
      "prefab",
      "在 Unity Editor 中重建角色 Prefab",
      prefabStepStatus,
      editorRebuild ? getUnityEditorRebuildStepDetail(editorRebuild) : getUnityPostImportStepDetail("Prefab", { commandSucceeded })
    ),
    unityImportStep(
      "weaponTraceBake",
      "烘焙武器命中轨迹",
      weaponTraceBakeStepStatus,
      getUnityEditorWeaponTraceBakeStepDetail(editorRebuild, commandSucceeded)
    ),
    unityImportStep(
      "socketBake",
      "烘焙动画挂点轨迹",
      socketBakeStepStatus,
      getUnityEditorSocketBakeStepDetail(editorRebuild, commandSucceeded)
    ),
    unityImportStep(
      "hurtboxBake",
      "烘焙动画受击体",
      hurtboxBakeStepStatus,
      getUnityEditorHurtboxBakeStepDetail(editorRebuild, commandSucceeded)
    ),
    unityImportStep(
      "characterAction",
      "刷新 CharacterAction Profile / ActionSet",
      actionStepStatus,
      getUnityEditorCharacterActionStepDetail(editorRebuild, commandSucceeded)
    ),
    unityImportStep(
      "refresh",
      "刷新 CharacterStudio 状态",
      state.resourceLibrary || state.resourcePlan || state.readiness ? "ok" : (commandSucceeded ? "warn" : "error"),
      state.readiness ? "已重新加载资源计划 / readiness。" : "导入后没有可用 readiness 快照。"
    )
  ];
}

function buildUnityImportAcceptance(result, options = {}) {
  const running = Boolean(options.running);
  if (running) {
    return {
      status: "running",
      label: "运行中",
      tone: "warn",
      title: "Unity 导入进行中",
      summary: "Authoring 导入、Unity Editor AssetDatabase 导入、Prefab 重建和 CharacterAction 刷新仍在执行。",
      issues: []
    };
  }

  if (!result) {
    return {
      status: "unknown",
      label: "未知",
      tone: "warn",
      title: "没有 Unity 导入结果",
      summary: "运行 Unity 导入后会生成 Authoring 和 Unity Editor bridge 状态。",
      issues: []
    };
  }

  const report = getUnityImportReportFromResult(result);
  const commandSucceeded = isUnityImportCommandSuccess(result, report);
  const editorRebuild = getUnityEditorRebuildStatus(result);
  const steps = options.steps || buildUnityImportTaskSteps(result);
  const requiredStepIds = new Set(["command", "commonResources", "assetDatabase", "avatarSource", "prefab", "weaponTraceBake", "socketBake", "hurtboxBake", "characterAction"]);
  const requiredSteps = steps.filter(step => requiredStepIds.has(step.id));
  const commandStep = requiredSteps.find(step => step.id === "command");

  if (!commandSucceeded || commandStep?.status === "error") {
    return {
      status: "failed",
      label: "失败",
      tone: "error",
      title: "Authoring import-unity 失败",
      summary: commandStep?.detail || "Authoring import-unity 命令未成功完成。",
      issues: collectUnityImportAcceptanceIssues(requiredSteps)
    };
  }

  const editorStatus = String(editorRebuild?.status || "").toLowerCase();
  if (!editorRebuild) {
    return {
      status: "waiting",
      label: "等待 Unity Editor",
      tone: "warn",
      title: "缺少 Unity Editor 验收",
      summary: "文件写入可能已是最新，但 Unity Editor 没有返回 AssetDatabase、Avatar、Prefab 和 CharacterAction 验收。",
      issues: ["Unity Editor bridge 没有返回状态。"]
    };
  }
  if (editorStatus === "pending" || editorStatus === "running") {
    return {
      status: "waiting",
      label: "等待 Unity Editor",
      tone: "warn",
      title: "Unity Editor 验收仍在运行",
      summary: editorRebuild.message || "正在等待 Unity Editor bridge 完成 AssetDatabase 导入和 Prefab/Profile 刷新。",
      issues: collectUnityImportAcceptanceIssues(requiredSteps)
    };
  }
  if (editorStatus === "timeout" || editorStatus === "timedout") {
    return {
      status: "attention",
      label: "需处理",
      tone: "warn",
      title: "Unity Editor 验收超时",
      summary: editorRebuild.message || "Unity Editor bridge 完成前，CharacterStudio 已停止等待。",
      issues: collectUnityImportAcceptanceIssues(requiredSteps)
    };
  }
  if (editorStatus === "failed" || editorStatus === "error") {
    return {
      status: "failed",
      label: "失败",
      tone: "error",
      title: "Unity Editor 验收失败",
      summary: editorRebuild.message || "Unity Editor bridge 报告导入或 Prefab 重建失败。",
      issues: collectUnityImportAcceptanceIssues(requiredSteps)
    };
  }

  const errorSteps = requiredSteps.filter(step => step.status === "error");
  if (errorSteps.length) {
    return {
      status: "failed",
      label: "失败",
      tone: "error",
      title: "Unity Editor 验收失败",
      summary: "至少一个必需的 Unity Editor 验收阶段失败。",
      issues: collectUnityImportAcceptanceIssues(errorSteps)
    };
  }

  const attentionSteps = requiredSteps.filter(step => step.status === "warn" || step.status === "info" || step.status === "running" || !step.status);
  if (attentionSteps.length) {
    return {
      status: "attention",
      label: "需处理",
      tone: "warn",
      title: "Unity Editor 验收不完整",
      summary: "Authoring 文件写入已完成，但一个或多个 Editor 验收阶段没有报告就绪。",
      issues: collectUnityImportAcceptanceIssues(attentionSteps)
    };
  }

  return {
    status: "ready",
    label: "就绪",
    tone: "ok",
    title: "Unity 导入已验收",
    summary: "Authoring 文件、公共资源、AssetDatabase 导入、Humanoid Avatar、Prefab、默认武器、武器轨迹、动画挂点、受击体和 CharacterAction Profile/ActionSet 均已验收。",
    issues: []
  };
}

function collectUnityImportAcceptanceIssues(steps) {
  return (steps || [])
    .filter(step => step && step.status !== "ok")
    .map(step => `${step.label || step.id || "Unity 步骤"}: ${step.detail || getUnityImportStepLabel(step.status)}`)
    .slice(0, 6);
}

function getCommonResourcesUnityImportStepStatus(commandSucceeded) {
  const summary = state.commonResources?.summary || null;
  if (!summary) return commandSucceeded ? "info" : "error";
  const total = Number(summary.total || 0);
  if (!total) return "ok";
  const ready = Number(summary.ready || summary.synced || summary.clean || 0);
  const changed = Number(summary.changed || summary.dirty || 0);
  const unityPending = Number(summary.unityPending || summary.needsUnityImport || 0);
  const blocked = Number(summary.blocked || summary.missing || 0);
  if (blocked > 0) return "error";
  if (changed > 0 || unityPending > 0) return "warn";
  if (ready >= total) return "ok";
  return commandSucceeded ? "info" : "error";
}

function getCommonResourcesUnityImportStepDetail() {
  const summary = state.commonResources?.summary || null;
  if (!summary) return "公共资源状态未加载。";
  const total = Number(summary.total || 0);
  if (!total) return "没有找到公共复用资源清单。";
  const ready = Number(summary.ready || summary.synced || summary.clean || 0);
  const changed = Number(summary.changed || summary.dirty || 0);
  const unityPending = Number(summary.unityPending || summary.needsUnityImport || 0);
  const blocked = Number(summary.blocked || summary.missing || 0);
  return `就绪=${ready} 改动=${changed} Unity待处理=${unityPending} 阻塞=${blocked} 总数=${total}`;
}

function getUnityResourceSyncDetail(summary) {
  if (!summary.total) return "导入报告没有资源文件操作。";
  return `变更=${summary.changed} 已是最新=${summary.upToDate} 问题=${summary.problem} 总数=${summary.total}`;
}

function getUnityArtifactStepDetail(report, summary) {
  const artifactCount = Number(report?.artifactCount || 0);
  const transformCount = Number(report?.artifactTransformCount || 0);
  if (artifactCount > 0 || transformCount > 0) {
    return `产物文件=${artifactCount} 兼容转换=${transformCount}`;
  }
  if (summary?.total) {
    return "不需要生成兼容产物；源文件本身就是 Unity 导入产物。";
  }
  return "没有报告产物元数据。";
}

function getUnityAssetDatabaseStepDetail(editorRebuild, commandSucceeded) {
  if (!commandSucceeded) return "import-unity 失败，AssetDatabase 导入未运行。";
  if (!editorRebuild) return "正在等待 Unity Editor bridge 报告 AssetDatabase 导入和 Prefab 重建。";
  const parts = [];
  if (editorRebuild.status) parts.push(`editor=${editorRebuild.status}`);
  if (editorRebuild.updatedUtc) parts.push(editorRebuild.updatedUtc);
  if (editorRebuild.message) parts.push(editorRebuild.message);
  return parts.join(" / ") || "Unity Editor bridge 已确认导入。";
}

function getUnityEditorAssetDatabaseStepStatus(status, commandSucceeded) {
  if (!commandSucceeded) return "error";
  if (!status) return "info";
  const value = String(status.status || "").toLowerCase();
  if (value === "completed" || value === "done" || value === "success") return "ok";
  if (status.artifactHash || status.avatarSourceAssetPath || status.prefabPath || status.runtimePrefabPath) return "ok";
  if (value === "failed" || value === "error") return "error";
  if (value === "pending" || value === "running") return "running";
  if (value === "timeout" || value === "timedout") return "warn";
  return "info";
}

function getUnityPostImportStepStatus(commandSucceeded, hasEvidence) {
  if (!commandSucceeded) return "error";
  return hasEvidence ? "ok" : "info";
}

function getUnityPostImportStepDetail(label, options = {}) {
  if (!options.commandSucceeded) return `import-unity 失败，${label} 未运行。`;
  if (options.explicitEvidence) return `命令输出或报告包含 ${label} 证据。`;
  if (options.readinessEvidence) return `readiness 包含 ${label} 证据。`;
  if (options.spawnReadiness) return "readiness 显示角色可以 Spawn；Prefab 状态可视为可用。";
  return `${label} 没有作为文件写入操作报告；请以 Unity Editor bridge 状态或 readiness 为准。`;
}

function getUnityEditorRebuildStatus(result) {
  return result?.editorRebuildStatus || result?.editorRebuild || null;
}

function getUnityEditorRebuildStepStatus(status, commandSucceeded) {
  if (!status) return "";
  const value = String(status.status || "").toLowerCase();
  if (status.prefabPath) return "ok";
  if (value === "failed" || value === "error") return "error";
  if (value === "pending" || value === "running") return "running";
  if (value === "timeout" || value === "timedout") return "warn";
  if (value === "completed" || value === "done" || value === "success") {
    if (!status.prefabPath) return "error";
    const weaponValidation = getUnityEditorDefaultWeaponValidation(status);
    if (weaponValidation.status === "error") return "error";
    if (weaponValidation.status === "warn") return "warn";
    return "ok";
  }
  return commandSucceeded ? "info" : "error";
}

function getUnityEditorRebuildStepDetail(status) {
  if (!status) return "";
  const parts = [];
  if (status.status) parts.push(`editor=${status.status}`);
  if (status.prefabPath) parts.push(status.prefabPath);
  if (status.geometryBindingHash) parts.push(`geometry=${status.geometryBindingHash}`);
  if (status.artifactHash) parts.push(`artifact=${status.artifactHash}`);
  if (Number.isFinite(Number(status.socketCount))) parts.push(`sockets=${Number(status.socketCount)}`);
  if (Number.isFinite(Number(status.weaponAttachmentCount))) parts.push(`weaponAttachments=${Number(status.weaponAttachmentCount)}`);
  if (Number.isFinite(Number(status.mountedWeaponCount))) parts.push(`mountedWeapons=${Number(status.mountedWeaponCount)}`);
  const bodyMaterial = getUnityEditorBodyMaterialBindingDetail(status);
  if (bodyMaterial) parts.push(bodyMaterial);
  const weaponValidation = getUnityEditorDefaultWeaponValidation(status);
  if (weaponValidation.detail) parts.push(weaponValidation.detail);
  if (status.message) parts.push(status.message);
  if (Array.isArray(status.issues) && status.issues.length) parts.push(status.issues[0]);
  if (status.statusPath && !status.prefabPath) parts.push(status.statusPath);
  return parts.join(" / ") || "正在等待 Unity Editor Prefab 重建状态。";
}

function getUnityEditorBodyMaterialBindingDetail(status) {
  const bindingStatus = String(status?.bodyMaterialBindingStatus || "");
  if (!bindingStatus) return "";
  const parts = [`bodyMaterial=${bindingStatus}`];
  if (status.bodyMaterialTemplateId) parts.push(`template=${status.bodyMaterialTemplateId}`);
  if (status.bodyMaterialTextureResourceKey) parts.push(`texture=${status.bodyMaterialTextureResourceKey}`);
  if (Number.isFinite(Number(status.bodyMaterialRendererCount))) parts.push(`renderers=${Number(status.bodyMaterialRendererCount)}`);
  if (status.bodyMaterialPath) parts.push(status.bodyMaterialPath);
  return parts.join(" ");
}

function getExpectedDefaultWeaponResourceKeys() {
  const attachments = state.package?.geometry?.weaponAttachments || [];
  const keys = [];
  for (const attachment of attachments) {
    const key = getWeaponAttachmentResourceKey(attachment);
    if (key) keys.push(key);
  }
  keys.sort();
  return keys;
}

function getUnityEditorDefaultWeaponValidation(status) {
  const expected = getExpectedDefaultWeaponResourceKeys();
  const reported = Array.isArray(status?.defaultWeaponResourceIds)
    ? status.defaultWeaponResourceIds.map(value => String(value || "")).filter(Boolean).sort()
    : [];
  if (!expected.length) {
    return { status: "ok", detail: "默认武器=无预期项" };
  }
  if (!status) {
    return { status: "info", detail: `默认武器预期=${expected.join(", ")}` };
  }
  const mounted = Number(status.mountedWeaponCount);
  const mountedCount = Number.isFinite(mounted) ? mounted : reported.length;
  const missing = expected.filter(key => !reported.includes(key));
  if (mountedCount < expected.length || missing.length) {
    const missingText = missing.length ? `缺失=${missing.join(", ")}` : `已挂载=${mountedCount}/${expected.length}`;
    return { status: "error", detail: `默认武器 ${missingText}` };
  }
  return { status: "ok", detail: `默认武器=${reported.join(", ") || mountedCount}` };
}

function getUnityEditorAvatarSourceStepStatus(status, commandSucceeded) {
  if (!commandSucceeded) return "error";
  if (!status) return "info";
  const bridgeStatus = String(status.status || "").toLowerCase();
  if (bridgeStatus === "pending" || bridgeStatus === "running") return "running";
  const avatarStatus = String(status.avatarSourceHumanoidStatus || status.avatarSourceStatus || "").toLowerCase();
  if (!avatarStatus) {
    return bridgeStatus === "completed" || bridgeStatus === "done" || bridgeStatus === "success" ? "error" : "info";
  }
  if (avatarStatus.includes("valid") || avatarStatus.includes("human") || avatarStatus === "ready") return "ok";
  if (avatarStatus.includes("missing") || avatarStatus.includes("invalid") || avatarStatus.includes("nonhuman") || avatarStatus.includes("error")) return "error";
  if (bridgeStatus === "failed" || bridgeStatus === "error") return "error";
  return "info";
}

function getUnityEditorAvatarSourceStepDetail(status, commandSucceeded) {
  if (!commandSucceeded) return "import-unity 失败，Humanoid Avatar 校验未运行。";
  if (!status) return "正在等待 Unity Editor bridge 报告 Avatar Source 校验。";
  const parts = [];
  if (status.avatarSourceStatus) parts.push(`source=${status.avatarSourceStatus}`);
  if (status.avatarSourceHumanoidStatus) parts.push(`humanoid=${status.avatarSourceHumanoidStatus}`);
  if (status.avatarSourceAssetPath) parts.push(status.avatarSourceAssetPath);
  if (status.avatarSourceGuid) parts.push(`guid=${status.avatarSourceGuid}`);
  if (status.avatarSourceMessage) parts.push(status.avatarSourceMessage);
  return parts.join(" / ") || "Unity Editor bridge 没有报告 Humanoid Avatar Source 状态。";
}

function getUnityEditorCharacterActionStepStatus(status, commandSucceeded) {
  if (!commandSucceeded) return "error";
  if (!status) return "info";
  const value = String(status.status || "").toLowerCase();
  if (value === "pending" || value === "running") return "running";
  if (status.characterActionProfileReady && status.characterActionActionSetReady) return "ok";
  if (value === "failed" || value === "error") return "error";
  if (value === "completed" || value === "done" || value === "success") return "error";
  return "info";
}

function getUnityEditorCharacterActionStepDetail(status, commandSucceeded) {
  if (!commandSucceeded) return "import-unity 失败，CharacterAction Profile 同步未运行。";
  if (!status) return "正在等待 Unity Editor bridge 报告 CharacterAction Profile 同步。";
  const profile = status.characterActionProfileReady ? "profile=就绪" : "profile=缺失";
  const actionSet = status.characterActionActionSetReady ? "actionSet=就绪" : "actionSet=缺失";
  const paths = [status.characterActionProfilePath, status.characterActionSetPath].filter(Boolean).join(" / ");
  return [profile, actionSet, paths].filter(Boolean).join(" / ");
}

function getUnityEditorWeaponTraceBakeStepStatus(status, commandSucceeded) {
  if (!commandSucceeded) return "error";
  if (!status) return "info";
  const bridgeStatus = String(status.status || "").toLowerCase();
  if (bridgeStatus === "pending" || bridgeStatus === "running") return "running";
  const rawStatus = String(status.weaponTraceBakeStatus || "").toLowerCase();
  const issueCount = Number(status.weaponTraceBakeIssueCount || status.weaponTraceBakeMissingFrameCount || 0);
  if (rawStatus === "failed" || rawStatus === "error" || issueCount > 0) return "error";
  if (rawStatus === "ready" || rawStatus === "ok" || status.weaponTraceBakeReady === true) return "ok";
  if (bridgeStatus === "failed" || bridgeStatus === "error") return "error";
  if (bridgeStatus === "completed" || bridgeStatus === "done" || bridgeStatus === "success") return "info";
  return "info";
}

function getUnityEditorWeaponTraceBakeStepDetail(status, commandSucceeded) {
  if (!commandSucceeded) return "import-unity 失败，武器轨迹烘焙未运行。";
  if (!status) return "正在等待 Unity Editor bridge 报告武器轨迹烘焙校验。";
  const parts = [];
  if (status.weaponTraceBakeStatus) parts.push(`status=${status.weaponTraceBakeStatus}`);
  if (status.weaponTraceBakeHash) parts.push(`hash=${status.weaponTraceBakeHash}`);
  if (Number.isFinite(Number(status.weaponTraceBakeRequestCount))) parts.push(`requests=${Number(status.weaponTraceBakeRequestCount)}`);
  if (Number.isFinite(Number(status.weaponTraceBakeArtifactCount))) parts.push(`artifacts=${Number(status.weaponTraceBakeArtifactCount)}`);
  if (Number.isFinite(Number(status.weaponTraceBakeCheckedFrameCount))) parts.push(`frames=${Number(status.weaponTraceBakeCheckedFrameCount)}`);
  if (Number.isFinite(Number(status.weaponTraceBakeIssueCount))) parts.push(`issues=${Number(status.weaponTraceBakeIssueCount)}`);
  const issues = Array.isArray(status.weaponTraceBakeIssues) ? status.weaponTraceBakeIssues : [];
  if (issues.length) {
    const first = issues[0] || {};
    parts.push([
      first.code || "issue",
      first.actionStableId,
      first.clipKey,
      first.traceId,
      Number.isFinite(Number(first.frame)) ? `frame=${Number(first.frame)}` : ""
    ].filter(Boolean).join(" "));
  }
  return parts.join(" / ") || "Unity Editor bridge 没有报告武器轨迹烘焙状态。";
}

function getUnityEditorHurtboxBakeStepStatus(status, commandSucceeded) {
  if (!commandSucceeded) return "error";
  if (!status) return "info";
  const bridgeStatus = String(status.status || "").toLowerCase();
  if (bridgeStatus === "pending" || bridgeStatus === "running") return "running";
  const rawStatus = String(status.hurtboxBakeStatus || "").toLowerCase();
  const issueCount = Number(status.hurtboxBakeIssueCount || 0);
  if (rawStatus === "failed" || rawStatus === "error" || issueCount > 0) return "error";
  if (rawStatus === "ready" || rawStatus === "ok" || status.hurtboxBakeReady === true) return "ok";
  if (bridgeStatus === "failed" || bridgeStatus === "error") return "error";
  return "info";
}

function getUnityEditorSocketBakeStepStatus(status, commandSucceeded) {
  if (!commandSucceeded) return "error";
  if (!status) return "info";
  const bridgeStatus = String(status.status || "").toLowerCase();
  if (bridgeStatus === "pending" || bridgeStatus === "running") return "running";
  const rawStatus = String(status.socketBakeStatus || "").toLowerCase();
  const issueCount = Number(status.socketBakeIssueCount || 0);
  if (rawStatus === "failed" || rawStatus === "error" || issueCount > 0) return "error";
  if (rawStatus === "ready" || rawStatus === "ok" || status.socketBakeReady === true) return "ok";
  if (bridgeStatus === "failed" || bridgeStatus === "error") return "error";
  return "info";
}

function getUnityEditorSocketBakeStepDetail(status, commandSucceeded) {
  if (!commandSucceeded) return "import-unity 失败，动画挂点烘焙未运行。";
  if (!status) return "正在等待 Unity Editor bridge 报告动画挂点烘焙状态。";
  const parts = [];
  if (status.socketBakeStatus) parts.push(`status=${status.socketBakeStatus}`);
  if (status.socketBakeHash) parts.push(`hash=${status.socketBakeHash}`);
  if (Number.isFinite(Number(status.socketBakeRequestCount))) parts.push(`requests=${Number(status.socketBakeRequestCount)}`);
  if (Number.isFinite(Number(status.socketBakeClipCount))) parts.push(`clips=${Number(status.socketBakeClipCount)}`);
  if (Number.isFinite(Number(status.socketBakeSocketTrackCount))) parts.push(`tracks=${Number(status.socketBakeSocketTrackCount)}`);
  if (Number.isFinite(Number(status.socketBakeCheckedFrameCount))) parts.push(`frames=${Number(status.socketBakeCheckedFrameCount)}`);
  if (Number.isFinite(Number(status.socketBakeIssueCount))) parts.push(`issues=${Number(status.socketBakeIssueCount)}`);
  const issues = Array.isArray(status.socketBakeIssues) ? status.socketBakeIssues : [];
  if (issues.length) {
    const first = issues[0] || {};
    parts.push([first.code || "issue", first.clipKey, first.traceId, Number.isFinite(Number(first.frame)) ? `frame=${Number(first.frame)}` : ""].filter(Boolean).join(" "));
  }
  return parts.join(" / ") || "Unity Editor bridge 没有报告动画挂点烘焙状态。";
}

function getUnityEditorHurtboxBakeStepDetail(status, commandSucceeded) {
  if (!commandSucceeded) return "import-unity 失败，动画受击体烘焙未运行。";
  if (!status) return "正在等待 Unity Editor bridge 报告动画受击体烘焙状态。";
  const parts = [];
  if (status.hurtboxBakeStatus) parts.push(`status=${status.hurtboxBakeStatus}`);
  if (status.hurtboxBakeHash) parts.push(`hash=${status.hurtboxBakeHash}`);
  if (Number.isFinite(Number(status.hurtboxBakeClipCount))) parts.push(`clips=${Number(status.hurtboxBakeClipCount)}`);
  if (Number.isFinite(Number(status.hurtboxBakeColliderCount))) parts.push(`colliders=${Number(status.hurtboxBakeColliderCount)}`);
  if (Number.isFinite(Number(status.hurtboxBakeShapeCount))) parts.push(`tracks=${Number(status.hurtboxBakeShapeCount)}`);
  if (Number.isFinite(Number(status.hurtboxBakeCheckedFrameCount))) parts.push(`frames=${Number(status.hurtboxBakeCheckedFrameCount)}`);
  if (Number.isFinite(Number(status.hurtboxBakeIssueCount))) parts.push(`issues=${Number(status.hurtboxBakeIssueCount)}`);
  const issues = Array.isArray(status.hurtboxBakeIssues) ? status.hurtboxBakeIssues : [];
  if (issues.length) {
    const first = issues[0] || {};
    parts.push([first.code || "issue", first.clipKey, first.traceId, Number.isFinite(Number(first.frame)) ? `frame=${Number(first.frame)}` : ""].filter(Boolean).join(" "));
  }
  return parts.join(" / ") || "Unity Editor bridge 没有报告动画受击体烘焙状态。";
}

function unityImportStep(id, label, status, detail) {
  return { id, label, status, detail };
}

function hasImportOperation(operations, predicate) {
  return Array.isArray(operations) && operations.some(operation => {
    try {
      return predicate(operation);
    } catch {
      return false;
    }
  });
}

function buildUnityImportEvidenceText(result, report) {
  const parts = [];
  if (report) parts.push(JSON.stringify({
    status: report.status,
    targetRootPath: report.targetRootPath,
    reportPath: report.reportPath,
    operations: report.operations || []
  }));
  if (result?.reportOut) parts.push(String(result.reportOut));
  if (result?.stdout) parts.push(String(result.stdout));
  if (result?.stderr) parts.push(String(result.stderr));
  return parts.join("\n");
}

function isUnityImportCommandSuccess(result, report = null) {
  if (!result) return false;
  if (result.timedOut) return false;
  if (typeof result.exitCode === "number" && result.exitCode !== 0) return false;
  if (result.success === false) return false;
  const status = String(report?.status || result.status || "").toLowerCase();
  if (status.includes("error") || status.includes("failed")) return false;
  return result.success === true || Boolean(report);
}

function getUnityImportCommandDetail(result, report) {
  const bits = [];
  if (typeof result?.exitCode === "number") bits.push(`exit=${result.exitCode}`);
  if (result?.timedOut) bits.push("已超时");
  if (report?.status) bits.push(`report=${report.status}`);
  if (report?.targetRootPath) bits.push(report.targetRootPath);
  if (!bits.length) return "命令没有返回 exit/report 详情。";
  return bits.join(" / ");
}

function renderUnityImportTaskPanel() {
  const task = state.unityImportTask;
  const result = task?.result || state.importResult;
  const steps = task?.steps?.length ? task.steps : buildUnityImportTaskSteps(result);
  if (!task && !result && steps.length === 0) return "";
  const running = Boolean(task?.running);
  const status = running ? "running" : (task?.status || (isUnityImportCommandSuccess(result, getUnityImportReportFromResult(result)) ? "success" : "unknown"));
  const acceptance = buildUnityImportAcceptance(result, { running, steps });
  const tone = acceptance.tone || (status === "success" ? "ok" : status === "failed" ? "error" : status === "running" ? "warn" : "warn");
  const report = getUnityImportReportFromResult(result);
  const output = renderUnityImportCommandOutput(result);
  return `<section class="unity-import-task ${escapeHtml(status)} ${escapeHtml(acceptance.status)}">
    <header>
      <div>
        <strong>Unity 导入任务</strong>
        <p>${escapeHtml(task?.message || "最近一次 Unity 导入状态。")}</p>
      </div>
      <span class="badge ${escapeHtml(tone)}">${escapeHtml(acceptance.label || getUnityImportTaskLabel(status))}</span>
    </header>
    ${renderUnityImportAcceptance(acceptance)}
    <div class="unity-import-step-list">
      ${steps.map(renderUnityImportStep).join("")}
    </div>
    ${report?.reportPath ? `<div class="meta unity-import-report-path" title="${escapeHtml(report.reportPath)}">report=${escapeHtml(report.reportPath)}</div>` : ""}
    ${output}
  </section>`;
}

function renderUnityImportAcceptance(acceptance) {
  if (!acceptance) return "";
  const tone = acceptance.tone || "";
  const issues = Array.isArray(acceptance.issues) ? acceptance.issues.filter(Boolean) : [];
  return `<div class="unity-import-acceptance ${escapeHtml(tone)}">
    <div>
      <strong>${escapeHtml(acceptance.title || "Unity 导入验收")}</strong>
      <p>${escapeHtml(acceptance.summary || "")}</p>
    </div>
    ${issues.length
      ? `<ul>${issues.map(issue => `<li title="${escapeHtml(issue)}">${escapeHtml(issue)}</li>`).join("")}</ul>`
      : `<span class="unity-import-acceptance-clean">${escapeHtml(acceptance.status === "ready" ? "所有必需的 Editor 验收阶段都已报告就绪。" : "还没有报告阶段详情。")}</span>`}
  </div>`;
}

function renderUnityImportStep(step) {
  const tone = getUnityImportStepTone(step.status);
  return `<div class="unity-import-step ${escapeHtml(step.status || "pending")}">
    <span class="unity-import-step-dot ${escapeHtml(tone)}"></span>
    <strong>${escapeHtml(step.label || step.id || "步骤")}</strong>
    <span class="badge ${escapeHtml(tone)}">${escapeHtml(getUnityImportStepLabel(step.status))}</span>
    <p title="${escapeHtml(step.detail || "")}">${escapeHtml(step.detail || "")}</p>
  </div>`;
}

function getUnityImportTaskLabel(status) {
  if (status === "running") return "运行中";
  if (status === "success") return "完成";
  if (status === "attention") return "需处理";
  if (status === "failed") return "失败";
  return "未知";
}

function getUnityImportStepLabel(status) {
  if (status === "ok") return "完成";
  if (status === "warn") return "检查";
  if (status === "error") return "失败";
  if (status === "info") return "未报告";
  if (status === "running") return "运行中";
  return "等待";
}

function getUnityImportStepTone(status) {
  if (status === "ok") return "ok";
  if (status === "error") return "error";
  if (status === "running" || status === "warn") return "warn";
  return "";
}

function hasSpawnReadyEvidence() {
  const snapshot = state.readiness;
  if (!snapshot) return false;
  const status = String(snapshot.overallStatus || snapshot.status || "").toLowerCase();
  if (status === "ready" || status === "runtime ready" || status === "runtimeready") return true;
  return Array.isArray(snapshot.stages) && snapshot.stages.some(stage => {
    const stageName = String(stage.stage || stage.name || stage.id || "").toLowerCase();
    const stageStatus = String(stage.status || stage.state || "").toLowerCase();
    return (stageName.includes("spawn") || stageName.includes("runtime")) && stageStatus === "ready";
  });
}

function renderUnityImportCommandOutput(result) {
  if (!result) return "";
  const stdout = trimCommandOutput(result.stdout);
  const stderr = trimCommandOutput(result.stderr);
  if (!stdout && !stderr) return "";
  return `<details class="unity-import-output">
    <summary>命令输出</summary>
    ${stderr ? `<pre class="stderr">${escapeHtml(stderr)}</pre>` : ""}
    ${stdout ? `<pre>${escapeHtml(stdout)}</pre>` : ""}
  </details>`;
}

function trimCommandOutput(value) {
  const text = String(value || "").trim();
  if (!text) return "";
  const lines = text.split(/\r?\n/).filter(Boolean);
  return lines.slice(-18).join("\n");
}

function findUnityResourceEntry(resource) {
  if (!resource) return null;
  const entries = getUnityCatalogEntries();
  const resourceKey = String(resource.resourceKey || "");
  const stableId = String(resource.stableId || "");
  const relativePath = String(resource.relativePath || "");
  return entries.find(entry => {
    const providerData = entry?.providerData || {};
    return String(providerData.packageResourceKey || "") === resourceKey
      || String(entry?.id || "") === resourceKey
      || String(providerData.stableId || "") === stableId
      || String(providerData.sourceRelativePath || "") === relativePath
      || String(entry?.unityAssetPath || "").endsWith(relativePath);
  }) || null;
}

function findImportOperationForResource(resource, unityEntry = null) {
  const operations = getUnityImportReport()?.operations || [];
  if (!resource || operations.length === 0) return null;
  const sourcePath = String(resource.relativePath || "");
  const unityPath = getUnityAssetPath(unityEntry);
  return operations.find(operation => String(operation.sourcePath || "") === sourcePath)
    || operations.find(operation => unityPath && String(operation.targetPath || "") === unityPath)
    || operations.find(operation => sourcePath && String(operation.targetPath || "").endsWith(sourcePath));
}

function getUnityAssetPath(unityEntry) {
  return unityEntry?.unityAssetPath
    || unityEntry?.providerData?.unityAssetPath
    || unityEntry?.providerData?.assetPath
    || unityEntry?.address
    || "";
}

function getResourceUnitySync(resource) {
  if (resource?.sourceProviderId === "resourceMapping") {
    return getCommonResourceUnitySync(resource);
  }

  const entry = findUnityResourceEntry(resource);
  const operation = findImportOperationForResource(resource, entry);
  const rawStatus = entry?.importStatus
    || entry?.providerData?.importStatus
    || operation?.action
    || (state.unityResourceCatalog ? "MissingFromUnityCatalog" : "NoUnityCatalog");
  const unityAssetGuid = entry?.unityAssetGuid || entry?.providerData?.unityAssetGuid || "";
  const importerKind = entry?.importerKind || entry?.providerData?.importerKind || "";
  const unityAssetPath = getUnityAssetPath(entry);
  const diagnostics = getResourceUnityDiagnostics(resource, entry, operation);
  const status = String(rawStatus || "Unknown");
  return {
    entry,
    operation,
    status,
    label: getUnityStatusLabel(status),
    tone: getUnityStatusTone(status, diagnostics),
    unityAssetPath,
    unityAssetGuid,
    guidShort: unityAssetGuid ? unityAssetGuid.slice(0, 8) : "",
    unityMainObjectType: entry?.unityMainObjectType || entry?.providerData?.unityMainObjectType || "",
    importerKind,
    diagnostics
  };
}

function getCommonResourceUnitySync(resource) {
  const commonItem = findCommonResourceItemByKey(resource?.resourceKey) || null;
  const unityAssetGuid = commonItem?.unityGuid || resource?.unityAssetGuid || "";
  const unityRuntimeAssetPath = commonItem?.unityRuntimeAssetPath
    || resource?.unityRuntimeAssetPath
    || commonItem?.generatedAssetPath
    || resource?.generatedAssetPath
    || "";
  const rawStatus = commonItem?.status || resource?.commonStatus || "";
  const profileRuntimeLoadable = commonItem?.profileRuntimeLoadable !== false;
  const generatedExists = commonItem?.generatedExists !== false && resource?.generatedExists !== false;
  const runtimeReady = String(rawStatus || "").toLowerCase() === "ready"
    && profileRuntimeLoadable
    && generatedExists
    && Boolean(unityRuntimeAssetPath)
    && Boolean(unityAssetGuid);
  const status = runtimeReady ? "RuntimeReady" : (rawStatus || "NeedsUnityImport");
  const diagnostics = getCommonResourceUnityDiagnostics(resource, commonItem, {
    unityRuntimeAssetPath,
    unityAssetGuid,
    profileRuntimeLoadable,
    generatedExists
  });

  return {
    entry: commonItem ? {
      id: resource?.resourceKey || commonItem.resourceKey || "",
      importStatus: status,
      unityAssetGuid,
      unityAssetPath: unityRuntimeAssetPath,
      providerData: {
        sourceProviderId: "resourceMapping",
        commonResourceId: commonItem.resourceId || commonItem.id || "",
        manifestPath: commonItem.manifestPath || "",
        unityRuntimeAssetPath,
        rawGeneratedAssetPath: commonItem.rawGeneratedAssetPath || "",
        usesUnityWrapperPrefab: commonItem.usesUnityWrapperPrefab ? "true" : "false",
        weaponMaterialBindingStatus: commonItem.weaponMaterialBindingStatus || "",
        weaponMaterialTemplateId: commonItem.weaponMaterialTemplateId || "",
        weaponMaterialPath: commonItem.weaponMaterialPath || "",
        weaponMaterialTextureResourceKey: commonItem.weaponMaterialTextureResourceKey || "",
        weaponMaterialSource: commonItem.weaponMaterialSource || "",
        weaponMaterialRendererCount: String(commonItem.weaponMaterialRendererCount || 0)
      }
    } : null,
    operation: null,
    status,
    label: getUnityStatusLabel(status),
    tone: getUnityStatusTone(status, diagnostics),
    unityAssetPath: unityRuntimeAssetPath,
    unityAssetGuid,
    guidShort: unityAssetGuid ? unityAssetGuid.slice(0, 8) : "",
    unityMainObjectType: commonItem?.unityRuntimeAssetKind || "",
    importerKind: "resourceMapping",
    diagnostics
  };
}

function getCommonResourceUnityDiagnostics(resource, commonItem, availability) {
  const diagnostics = [];
  const resourceKey = resource?.resourceKey || commonItem?.resourceKey || "";

  if (state.commonResources?.unavailable) {
    diagnostics.push(state.commonResources.message || "公共复用资源 API 不可用。");
    return diagnostics;
  }

  if (!commonItem) {
    diagnostics.push(`公共资源 ${resourceKey || "resource"} 未在 ArtSource/Common 状态中找到。`);
    return diagnostics;
  }

  if (commonItem.message) diagnostics.push(commonItem.message);
  if (!availability.generatedExists) diagnostics.push("公共生成资产缺失；请同步公共资源。");
  if (!availability.profileRuntimeLoadable) diagnostics.push("公共资源在全局 Profile 中不可被 Runtime 加载。");
  if (!availability.unityRuntimeAssetPath) diagnostics.push("公共资源没有 Unity Runtime 资产路径。");
  if (!availability.unityAssetGuid) diagnostics.push("公共资源尚未登记 Unity GUID。");
  if (commonItem.usesUnityWrapperPrefab && availability.unityRuntimeAssetPath) {
    diagnostics.push(`公共武器通过 wrapper Prefab 解析：${availability.unityRuntimeAssetPath}`);
  }
  if (commonItem.weaponMaterialBindingStatus) {
    diagnostics.push(`公共武器材质：${commonItem.weaponMaterialBindingStatus}`);
  }
  if (commonItem.weaponMaterialTemplateId) diagnostics.push(`materialTemplate=${commonItem.weaponMaterialTemplateId}`);
  if (commonItem.weaponMaterialSource) diagnostics.push(`materialSource=${commonItem.weaponMaterialSource}`);
  if (commonItem.weaponMaterialTextureResourceKey) diagnostics.push(`materialTexture=${commonItem.weaponMaterialTextureResourceKey}`);
  if (commonItem.weaponMaterialRendererCount) diagnostics.push(`materialRenderers=${commonItem.weaponMaterialRendererCount}`);
  if (commonItem.weaponMaterialPath) diagnostics.push(`material=${commonItem.weaponMaterialPath}`);
  return diagnostics;
}

function getResourceUnityDiagnostics(resource, entry, operation) {
  const diagnostics = [];
  const report = getUnityImportReport();
  if (!state.unityResourceCatalog) {
    diagnostics.push(`Unity Resource Catalog 未加载：${state.unityResourceCatalogPath || "未知路径"}`);
  }
  if (state.unityResourceCatalog && !entry) {
    diagnostics.push("此源资源不在 Unity Resource Catalog 中。");
  }
  if (entry && !getUnityAssetPath(entry)) {
    diagnostics.push("Unity 资产路径为空。");
  }
  if (entry && !(entry.unityAssetGuid || entry.providerData?.unityAssetGuid)) {
    diagnostics.push("Unity asset GUID 为空；Unity AssetDatabase 可能尚未导入。");
  }
  if (operation?.message) {
    diagnostics.push(`${operation.action || operation.kind || "Import"}: ${operation.message}`);
  }
  const resourceKey = String(resource?.resourceKey || "");
  for (const issue of report?.issues || []) {
    const source = String(issue.sourcePath || issue.sourceObjectPath || "");
    const message = String(issue.message || "");
    if (source === resource?.relativePath || source === resourceKey || (resourceKey && message.includes(resourceKey))) {
      diagnostics.push(`${issue.code || issue.gate || "Issue"}: ${message}`);
    }
  }
  return diagnostics;
}

function getUnityStatusLabel(status) {
  const normalized = String(status || "").toLowerCase();
  if (normalized === "ready" || normalized === "runtimeready") return "就绪";
  if (normalized === "imported") return "已导入";
  if (normalized === "skipped") return "已是最新";
  if (normalized === "updated") return "已更新";
  if (normalized === "added" || normalized === "created") return "已添加";
  if (normalized.includes("pending")) return "等待";
  if (normalized.includes("sourcechanged")) return "有改动";
  if (normalized.includes("unitymissing")) return "Unity 缺失";
  if (normalized.includes("missingfromunitycatalog")) return "不在 catalog 中";
  if (normalized.includes("nounitycatalog")) return "无 catalog";
  if (normalized.includes("failed")) return "失败";
  if (normalized.includes("conflict")) return "冲突";
  return status || "未知";
}

function getUnityStatusTone(status, diagnostics = []) {
  const normalized = String(status || "").toLowerCase();
  if (normalized.includes("failed") || normalized.includes("conflict") || normalized.includes("blocked")) return "error";
  if (normalized.includes("missing") || normalized.includes("pending") || normalized.includes("sourcechanged")) return "warn";
  if (diagnostics.some(item => /empty|not loaded|not present/i.test(item))) return "warn";
  if (["clean", "ready", "runtimeready", "imported", "skipped", "updated", "added", "created"].includes(normalized)) return "ok";
  return "";
}

function renderSyncBadge(sync) {
  return `<span class="sync-badge ${escapeHtml(sync.tone)}">${escapeHtml(sync.label)}</span>`;
}

function getSelectedModelResource() {
  const selectedKey = state.selectedPath?.startsWith("resources/") ? state.selectedPath.slice(10) : "";
  if (!selectedKey) return null;
  return getModelResources(state.package).find(resource => resource.resourceKey === selectedKey) || null;
}

Object.assign(globalThis, {
  usesCompilerOwnedReactionProjection,
  render,
  renderRuntimeValidationTabs,
  renderCombinedGlobalQuickEditor,
  requestGlobalConfigurationReferences,
  renderPrivateActionConfiguration,
  renderLayoutState,
  renderContextDockState,
  normalizeContextTab,
  renderContextDockSummary,
  getPrimaryReadinessIssue,
  renderWorkflowBar,
  buildWorkflowView,
  buildWorkflowStageView,
  buildWorkflowNextAction,
  buildWorkflowIssueCards,
  describeWorkflowIssue,
  renderWorkflowIssueCard,
  renderWorkflowActionButton,
  renderWorkflowActionAttributes,
  getUnityImportEvidenceView,
  shouldOfferDeferredUnityImport,
  formatWorkflowStatusLabel,
  collectWorkflowIssueRecords,
  countWorkflowIssues,
  suppressDependentWorkflowIssues,
  inferWorkflowIssueStageId,
  renderInspectorTabState,
  renderShellStatus,
  renderUndoRedoState,
  canUndo,
  hasPendingUndoTransactionChange,
  handleUndoRedoKeydown,
  resetUndoHistory,
  beginUndoTransaction,
  updateUndoTransactionAfter,
  commitUndoTransaction,
  cancelUndoTransaction,
  undoLastEdit,
  redoLastEdit,
  restoreHistoryItem,
  captureUndoContext,
  restoreUndoContext,
  refreshDirtyFromCleanSnapshot,
  serializePackage,
  getResourceBindingRole,
  updateResourceBindingControls,
  ensureAnimationAuthoringConfig,
  createDefaultAnimationSlot,
  normalizeAnimationSlot,
  ensureReactionAuthoringConfig,
  createDefaultReactionSlot,
  normalizeReactionSlot,
  ensureAnimationGroups,
  normalizeAnimationGroup,
  normalizeAnimationClipEntry,
  normalizeAnimationBlendSpace,
  getPackageAnimationResources,
  findAnimationGroupByResource,
  findAnimationGroupById,
  normalizeConfigId,
  createUniqueConfigId,
  toDisplayName,
  createPackageAnimationSelectionRef,
  findDefaultAnimationResource,
  findPackageResourceByKey,
  getDefaultAnimationProfile,
  findAnimationSlot,
  getAnimationSlotPath,
  getDefaultReactionProfile,
  findReactionSlot,
  getReactionSlotPath,
  getCharacterActionPackageSegment,
  getGeneratedReactionProfileStableId,
  getGeneratedReactionActionId,
  normalizeActionIdSegment,
  getCharacterActionPackageSourceFolder,
  getAnimationGroupPath,
  getAnimationClipPath,
  getAnimationBlendSpacePath,
  getAnimationBlendPointPath,
  renderAnimationConfigPanel,
  renderAnimationGroupCard,
  getAnimationEditorUrl,
  renderAnimationGroupChildren,
  renderAnimationSlotCard,
  getResourceAvailabilityTone,
  firstNonEmpty,
  findAnimationSlotResource,
  renderReactionProfilePanel,
  renderReactionSlotCard,
  renderReactionSlotTile,
  renderReactionSlotDetail,
  getReactionSlotDefinition,
  getReactionSlotDisplayName,
  getReactionSlotTreeLabel,
  getCurrentReactionActionSetContext,
  findReactionSlotResource,
  findGeneratedReactionAction,
  getPrimaryActionSetReactionProfileId,
  showGeneratedReactionActionConfig,
  openReactionActionAuthoring,
  renderAbilityLoadoutPanel,
  renderAbilityRow,
  getAbilityChainView,
  getReadinessStage,
  getAbilityActionOpenStatus,
  setAbilityActionOpenStatus,
  getAbilityActionOpenTone,
  getAbilityActionOpenStatusLabel,
  getAbilityActionOpenButtonLabel,
  openAbilityRuntimeDiagnostic,
  openAbilityActionAuthoring,
  buildAbilityActionOpenPayload,
  getPrimaryCharacterActionSetStableId,
  pollAbilityActionOpenStatus,
  copyAbilityActionAuthoringFallback,
  renderBindingStatusLine,
  getBoundBodyModelResource,
  renderResourcePlanPreviewLegacy,
  getCharacterResourcePlan,
  getPlanGroup,
  normalizePlanEntries,
  normalizePlanEntry,
  renderPlanEntryLegacy,
  renderResourcePlanPreview,
  renderReadinessPanel,
  renderImportMaterialsReadinessSummary,
  renderReadinessStage,
  renderReadinessIssue,
  getReadinessTone,
  sortReadinessStages,
  renderPlanGroupCard,
  describePlanConsequence,
  renderPlanEntry,
  buildFallbackResourcePlan,
  collectAnimationSlotResourceKeys,
  cleanupUnusedAnimationResourceKey,
  getModelResources,
  getResourceSortKey,
  getResourceDisplayName,
  getResourceInitial,
  describeResourceBinding,
  isWeaponResourceBinding,
  getResourceThumbnailUrl,
  getUnityCatalogEntries,
  getUnityImportReport,
  getUnityImportReportFromResult,
  classifyUnityImportOperation,
  getUnityImportOperationLabel,
  getUnityImportOperationTone,
  summarizeUnityImportOperations,
  getUnityImportReportSummary,
  startUnityImportTask,
  finishUnityImportTask,
  buildUnityImportTaskSteps,
  buildUnityImportAcceptance,
  collectUnityImportAcceptanceIssues,
  getUnityResourceSyncDetail,
  getUnityArtifactStepDetail,
  getUnityAssetDatabaseStepDetail,
  getUnityEditorAssetDatabaseStepStatus,
  getCommonResourcesUnityImportStepStatus,
  getCommonResourcesUnityImportStepDetail,
  getUnityPostImportStepStatus,
  getUnityPostImportStepDetail,
  getUnityEditorRebuildStatus,
  getUnityEditorRebuildStepStatus,
  getUnityEditorRebuildStepDetail,
  getExpectedDefaultWeaponResourceKeys,
  getUnityEditorDefaultWeaponValidation,
  getUnityEditorAvatarSourceStepStatus,
  getUnityEditorAvatarSourceStepDetail,
  getUnityEditorWeaponTraceBakeStepStatus,
  getUnityEditorWeaponTraceBakeStepDetail,
  getUnityEditorSocketBakeStepStatus,
  getUnityEditorSocketBakeStepDetail,
  getUnityEditorHurtboxBakeStepStatus,
  getUnityEditorHurtboxBakeStepDetail,
  getUnityEditorCharacterActionStepStatus,
  getUnityEditorCharacterActionStepDetail,
  unityImportStep,
  hasImportOperation,
  buildUnityImportEvidenceText,
  isUnityImportCommandSuccess,
  getUnityImportCommandDetail,
  renderUnityImportTaskPanel,
  renderUnityImportAcceptance,
  renderUnityImportStep,
  getUnityImportTaskLabel,
  getUnityImportStepLabel,
  getUnityImportStepTone,
  hasSpawnReadyEvidence,
  renderUnityImportCommandOutput,
  trimCommandOutput,
  findUnityResourceEntry,
  findImportOperationForResource,
  getUnityAssetPath,
  getResourceUnitySync,
  getResourceUnityDiagnostics,
  getUnityStatusLabel,
  getUnityStatusTone,
  renderSyncBadge,
  getSelectedModelResource
});
