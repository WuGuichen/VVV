const fallbackPackages = [
  { relative: "Tools/MxFramework.Authoring/samples/buff-preview", packageId: "sample.buff.preview", kind: "Preview" },
  { relative: "Tools/MxFramework.Authoring/samples/buff-mod", packageId: "sample.buff.mod", kind: "Mod" }
];

const paths = {
  apiState: "/api/state",
  apiPatch: "/api/patch",
  apiReport: "/api/report",
  apiAiContext: "/api/ai-context",
  apiPackages: "/api/packages",
  apiValidateDraft: "/api/validate-draft",
  apiLocalization: "/api/localization",
  apiPreviewStatus: "/api/preview/status",
  apiPreviewRun: "/api/preview/run",
  apiModDiagnose: "/api/mod/diagnose",
  manifest: "/Tools/MxFramework.Authoring/samples/project-manifest/project-authoring-manifest.json"
};

const HIDDEN_IN_MOD = new Set(["DamageBaseTypeID"]);

const TYPE_GUIDANCE = {
  DamageByAttr: {
    title: "持续伤害 Buff",
    summary: "按固定间隔造成伤害。先填持续时间和触发间隔，再填伤害公式、伤害类型和元素表现。",
    fields: ["Duration", "HitCooldown", "Values", "DmgType", "EleType", "EleValue", "HitEffect"]
  }
};

const FIELD_PLACEHOLDER = {
  Id: "例如 100001",
  Name: "例如 buff.sample.fire.name",
  Desc: "例如 buff.sample.fire.desc",
  Duration: "例如 5000",
  HitCooldown: "例如 1000",
  AddNum: "例如 1",
  Values: "例如 caster.Attack * 0.35",
  EleValue: "例如 1",
  HitEffect: "例如 Effects/Hit/FireSmall"
};

const state = {
  manifest: null,
  mod: null,
  patch: null,
  validation: null,
  mergePreview: null,
  reportIndex: null,
  canWrite: false,
  dirty: false,
  statusMessage: "",
  selectedStepId: "",
  selectedBuffId: "",
  packages: [],
  selectedPackage: fallbackPackages[0].relative,
  validateDraftTimer: null,
  uiMode: "Mod",
  localizationPatch: { entries: [] },
  localizationDirty: false,
  previewStatus: null,
  previewResult: null,
  diagnoseLoading: false,
  diagnoseError: "",
  diagnoseSnapshot: null,
  diagnoseSnapshotJson: "",
  diagnoseContainers: ["Assets/StreamingAssets/MxFramework/Demo"],
  diagnoseLoadout: "Assets/StreamingAssets/MxFramework/Demo/mod_loadout.json",
  diagnoseIncludeAbsolutePaths: false
};

function withPackage(url) {
  const sep = url.includes("?") ? "&" : "?";
  return `${url}${sep}package=${encodeURIComponent(state.selectedPackage)}`;
}

function staticPathsForPackage(rel) {
  return {
    mod: `/${rel}/mod.json`,
    patch: `/${rel}/patches/buff.patch.json`,
    validation: `/${rel}/reports/validation_report.json`,
    mergePreview: `/${rel}/reports/merge_preview.json`,
    reportIndex: `/${rel}/reports/report_index.json`
  };
}

document.addEventListener("DOMContentLoaded", () => {
  document.getElementById("reloadButton").addEventListener("click", loadAll);
  document.getElementById("savePatchButton").addEventListener("click", savePatch);
  document.getElementById("generateReportButton").addEventListener("click", generateReport);
  document.getElementById("previewButton").addEventListener("click", previewRuntime);
  document.getElementById("exportRuntimePatchButton").addEventListener("click", exportRuntimePatch);
  document.getElementById("copyContextButton").addEventListener("click", copyStepContext);
  document.getElementById("newBuffButton").addEventListener("click", openNewBuffWizard);
  document.getElementById("newBuffCloseButton").addEventListener("click", closeNewBuffWizard);
  document.getElementById("newBuffCancelButton").addEventListener("click", closeNewBuffWizard);
  document.getElementById("newBuffConfirmButton").addEventListener("click", confirmNewBuffWizard);
  document.getElementById("newBuffId").addEventListener("input", () => suggestBuffName());
  document.getElementById("modeSelect").addEventListener("change", event => {
    state.uiMode = event.target.value === "Developer" ? "Developer" : "Mod";
    render();
  });
  document.getElementById("addLocalizationButton").addEventListener("click", addLocalizationRow);
  document.getElementById("saveLocalizationButton").addEventListener("click", saveLocalization);
  document.getElementById("diagnoseRefreshButton").addEventListener("click", () => refreshModDiagnose(false));
  document.getElementById("diagnoseCopyJsonButton").addEventListener("click", copyDiagnoseJson);
  bootstrap();
});

async function bootstrap() {
  const list = await readJson(paths.apiPackages, null);
  state.packages = Array.isArray(list) && list.length > 0 ? list : fallbackPackages;
  if (!state.packages.find(p => p.relative === state.selectedPackage)) {
    state.selectedPackage = state.packages[0].relative;
  }
  ensurePackageSelector();
  await loadAll();
}

function ensurePackageSelector() {
  let select = document.getElementById("packageSelect");
  if (!select) {
    const host = document.getElementById("packageSelectHost") || document.querySelector("header") || document.body;
    const wrapper = document.createElement("div");
    wrapper.className = "package-selector";
    wrapper.innerHTML = `<label>包：<select id="packageSelect"></select></label><span id="packageBadge" class="badge"></span>`;
    host.prepend(wrapper);
    select = wrapper.querySelector("#packageSelect");
    select.addEventListener("change", async event => {
      state.selectedPackage = event.target.value;
      await loadAll();
    });
  }
  select.innerHTML = "";
  state.packages.forEach(p => {
    const opt = new Option(`${p.packageId || p.relative} (${p.kind || "?"})`, p.relative, false, p.relative === state.selectedPackage);
    select.append(opt);
  });
}

async function loadAll() {
  const apiState = await readJson(withPackage(paths.apiState), null);
  const sp = staticPathsForPackage(state.selectedPackage);
  if (apiState) {
    applyLoadedState(apiState, true);
  } else {
    state.manifest = await readJson(paths.manifest);
    state.mod = await readJson(sp.mod);
    state.patch = await readJson(sp.patch);
    state.validation = await readJson(sp.validation, { issues: [] });
    state.mergePreview = await readJson(sp.mergePreview, []);
    state.reportIndex = await readJson(sp.reportIndex, null);
    state.canWrite = false;
    state.dirty = false;
    state.statusMessage = "静态预览模式：请通过 start-authoring-editor.sh 启动本地 API 后再保存。";
  }

  const workflow = getWorkflow();
  state.selectedStepId = workflow?.currentStepId || workflow?.steps?.[0]?.stepId || "";
  state.selectedBuffId = state.patch?.entries?.[0]?.id || "";

  state.localizationPatch = await readJson(withPackage(paths.apiLocalization), { entries: [] });
  if (!state.localizationPatch || !Array.isArray(state.localizationPatch.entries)) {
    state.localizationPatch = { entries: [] };
  }
  state.localizationDirty = false;
  await refreshModDiagnose(true);
  render();
}

function applyLoadedState(data, canWrite) {
  state.manifest = data.manifest || null;
  state.mod = data.mod || null;
  state.patch = data.patch || null;
  state.validation = data.validation || { issues: [] };
  state.mergePreview = data.mergePreview || [];
  state.reportIndex = data.reportIndex || null;
  state.canWrite = Boolean(data.canWrite ?? canWrite);
  state.dirty = false;
  state.statusMessage = state.canWrite ? "本地 API 已连接，可以编辑并保存 Patch。" : "静态预览模式。";
}

async function readJson(path, fallback = null) {
  try {
    const response = await fetch(path, { cache: "no-store" });
    if (!response.ok) return fallback;
    return await response.json();
  } catch {
    return fallback;
  }
}

function render() {
  renderProjectSummary();
  renderPackageSummary();
  renderBuffList();
  renderWorkflowSteps();
  renderStepDetail();
  renderFields();
  renderEnumsAndReferences();
  renderValidation();
  renderMergePreview();
  renderRuntimePreview();
  renderLocalization();
  renderModDiagnose();
}

async function refreshModDiagnose(silent) {
  state.diagnoseLoading = true;
  state.diagnoseError = "";
  if (!silent) renderModDiagnose();
  try {
    const payload = {
      containers: state.diagnoseContainers,
      loadout: state.diagnoseLoadout,
      includeAbsolutePaths: state.diagnoseIncludeAbsolutePaths
    };
    const response = await fetch(paths.apiModDiagnose, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    if (!response.ok) {
      let message = `HTTP ${response.status}`;
      try {
        const errorData = await response.json();
        if (errorData?.error) message = errorData.error;
      } catch {}
      throw new Error(message);
    }
    const snapshot = await response.json();
    state.diagnoseSnapshot = snapshot;
    state.diagnoseSnapshotJson = JSON.stringify(snapshot, null, 2);
  } catch (error) {
    state.diagnoseError = error?.message || String(error);
  } finally {
    state.diagnoseLoading = false;
    if (!silent) renderModDiagnose();
  }
}

async function copyDiagnoseJson() {
  if (!state.diagnoseSnapshotJson) {
    state.statusMessage = "暂无可复制的诊断 JSON，请先刷新诊断。";
    renderValidation();
    return;
  }
  try {
    await navigator.clipboard.writeText(state.diagnoseSnapshotJson);
    state.statusMessage = "诊断 JSON 已复制。";
  } catch (error) {
    state.statusMessage = `复制诊断 JSON 失败：${error?.message || String(error)}`;
  }
  renderValidation();
}

function renderModDiagnose() {
  const configView = document.getElementById("diagnoseConfigView");
  const summaryView = document.getElementById("diagnoseSummaryView");
  const packagesView = document.getElementById("diagnosePackagesView");
  const loadoutView = document.getElementById("diagnoseLoadoutView");
  const loadPlanView = document.getElementById("diagnoseLoadPlanView");
  const overridesView = document.getElementById("diagnoseOverridesView");
  const issuesView = document.getElementById("diagnoseIssuesView");

  configView.innerHTML = "";
  summaryView.innerHTML = "";
  packagesView.innerHTML = "";
  loadoutView.innerHTML = "";
  loadPlanView.innerHTML = "";
  overridesView.innerHTML = "";
  issuesView.innerHTML = "";

  configView.append(
    keyValue("container", state.diagnoseContainers.join(", ")),
    keyValue("loadout", state.diagnoseLoadout || "(default-all)")
  );

  if (state.diagnoseLoading) {
    summaryView.append(empty("正在刷新诊断..."));
    return;
  }
  if (state.diagnoseError) {
    const row = document.createElement("div");
    row.className = "issue-row severity-Error";
    row.innerHTML = `<h3>诊断请求失败</h3><div>${state.diagnoseError}</div>`;
    issuesView.append(row);
    return;
  }
  const snapshot = state.diagnoseSnapshot;
  if (!snapshot) {
    summaryView.append(empty("尚未获取诊断结果。"));
    return;
  }

  const summary = snapshot.summary || {};
  summaryView.append(
    keyValue("success", String(Boolean(snapshot.success))),
    keyValue("discovered", summary.discovered ?? 0),
    keyValue("valid", summary.valid ?? 0),
    keyValue("invalid", summary.invalid ?? 0),
    keyValue("enabled", summary.enabled ?? 0),
    keyValue("ordered", summary.ordered ?? 0),
    keyValue("skipped", summary.skipped ?? 0),
    keyValue("overrides", summary.overrides ?? 0),
    keyValue("errors", summary.errors ?? 0),
    keyValue("warnings", summary.warnings ?? 0)
  );

  const packages = Array.isArray(snapshot.packages) ? snapshot.packages : [];
  if (packages.length === 0) {
    packagesView.append(empty("Packages: 空"));
  } else {
    packagesView.append(sectionTitle("Packages"));
    packages.forEach(pkg => {
      const row = document.createElement("div");
      row.className = "summary-row";
      row.innerHTML = `<strong>${pkg.packageKey || "-"}</strong><div class="hint">${pkg.packageId || "-"} / ${pkg.displayName || "-"} / ${pkg.version || "-"} / ${pkg.kind || "-"}</div><div class="hint">state: valid=${pkg.isValid} enabled=${pkg.isEnabled}</div>`;
      packagesView.append(row);
    });
  }

  const loadout = snapshot.loadout || {};
  loadoutView.append(sectionTitle("Loadout"));
  loadoutView.append(
    keyValue("profileId", loadout.profileId || "-"),
    keyValue("enabledPackageKeys", (loadout.enabledPackageKeys || []).join(", ") || "-")
  );

  const loadPlan = Array.isArray(snapshot.loadPlan) ? snapshot.loadPlan : [];
  loadPlanView.append(sectionTitle("Load Plan"));
  if (loadPlan.length === 0) {
    loadPlanView.append(empty("Load Plan: 空"));
  } else {
    loadPlan.forEach(item => {
      const row = document.createElement("div");
      row.className = "summary-row";
      row.innerHTML = `<strong>${item.packageKey || "-"}</strong><div class="hint">${item.packageId || "-"} / ${item.state || "-"}</div><div class="hint">skipReason: ${item.skipReason || "-"}</div>`;
      loadPlanView.append(row);
    });
  }

  const overrides = Array.isArray(snapshot.overrides) ? snapshot.overrides : [];
  overridesView.append(sectionTitle("Overrides"));
  if (overrides.length === 0) {
    overridesView.append(empty("Overrides: 空"));
  } else {
    overrides.forEach(ov => {
      const row = document.createElement("div");
      row.className = "summary-row";
      row.innerHTML = `<strong>${ov.configType || "-"} / ${ov.id ?? "-"}</strong><div class="hint">packageChain: ${(ov.packageChain || []).join(" -> ") || "-"}</div><div class="hint">winner: ${ov.winnerPackageKey || ov.winnerPackageId || "-"}</div>`;
      overridesView.append(row);
    });
  }

  const errors = Array.isArray(snapshot.errors) ? snapshot.errors : [];
  const warnings = Array.isArray(snapshot.warnings) ? snapshot.warnings : [];
  issuesView.append(sectionTitle("Issues"));
  const allIssues = [...errors, ...warnings];
  if (allIssues.length === 0) {
    issuesView.append(empty("Issues: 空"));
  } else {
    allIssues.forEach(issue => {
      const row = document.createElement("div");
      row.className = `issue-row severity-${issue.severity || "Info"}`;
      row.innerHTML = `<h3>${issue.severity || "Info"} / ${issue.code || "-"}</h3><div>${issue.message || "-"}</div><div class="hint">${issue.source || "-"}</div>`;
      issuesView.append(row);
    });
  }
}

function renderProjectSummary() {
  const el = document.getElementById("projectSummary");
  const manifest = state.manifest;
  if (!manifest) {
    el.textContent = "未读取到 Project Authoring Manifest";
    return;
  }
  el.textContent = `${manifest.displayName} / schema ${manifest.schemaVersion} / authoring ${manifest.authoringVersion}`;
}

function renderPackageSummary() {
  const container = document.getElementById("packageSummary");
  const mod = state.mod || {};
  const report = state.reportIndex;
  container.innerHTML = "";
  container.append(
    keyValue("包路径", state.selectedPackage),
    keyValue("包 ID", mod.packageId || "-"),
    keyValue("模式", mod.kind || "-"),
    keyValue("版本", mod.version || "-"),
    keyValue("Schema", mod.schemaVersion || "-"),
    keyValue("报告", report ? `${report.status} / ${report.files?.length || 0} files` : "未生成")
  );
  const badge = document.getElementById("packageBadge");
  if (badge) badge.textContent = `${mod.packageId || state.selectedPackage} / ${mod.kind || "?"} / ${state.canWrite ? "API" : "Static"}`;
}

function renderBuffList() {
  const container = document.getElementById("buffList");
  const entries = state.patch?.entries || [];
  container.innerHTML = "";
  if (entries.length === 0) {
    container.append(empty("没有 Buff 草稿"));
    return;
  }
  entries.forEach(entry => {
    const row = document.createElement("button");
    row.type = "button";
    row.className = `buff-row ${entry.id === state.selectedBuffId ? "active" : ""}`;
    row.innerHTML = `<strong>${entry.fields?.Name || entry.id}</strong><div class="subtle">${entry.fields?.Type || "-"} / ${entry.layer}</div>`;
    row.addEventListener("click", () => {
      state.selectedBuffId = entry.id;
      render();
    });
    container.append(row);
  });
}

function renderWorkflowSteps() {
  const container = document.getElementById("workflowSteps");
  const workflow = getWorkflow();
  container.innerHTML = "";
  if (!workflow) return;

  workflow.steps.forEach(step => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = `step-button ${step.stepId === state.selectedStepId ? "active" : ""}`;
    button.innerHTML = `<strong>${step.title}</strong><div class="subtle">${step.status} / ${step.actor}</div>`;
    button.addEventListener("click", () => {
      state.selectedStepId = step.stepId;
      render();
    });
    container.append(button);
  });
}

function renderStepDetail() {
  const step = getSelectedStep();
  document.getElementById("stepTitle").textContent = step?.title || "流程";
  document.getElementById("stepDescription").textContent = step?.description || "";
  const meta = document.getElementById("stepMeta");
  meta.innerHTML = "";
  if (!step) return;
  meta.append(badge(step.status), badge(step.actor), badge(step.requiresUnity ? "需要 Unity" : "不需要 Unity"));
}

function renderFields() {
  const container = document.getElementById("fieldEditor");
  const schema = getBuffSchema();
  const entry = getSelectedPatchEntry();
  const fields = (schema?.fields || []).filter(field => isFieldVisible(field, entry));
  const values = entry?.fields || {};
  container.innerHTML = "";

  if (!schema || !entry) {
    container.append(empty("未选择 Buff 草稿"));
    return;
  }

  const buffType = entry.fields?.Type || "未选择";
  const intro = document.createElement("div");
  intro.className = "field-intro";
  const modeLine = state.uiMode === "Developer" ? "<div class=\"hint\">开发者模式 - 字段全可见</div>" : "";
  const guidance = TYPE_GUIDANCE[buffType];
  const guidanceLine = guidance
    ? `<div class="type-guidance"><b>${guidance.title}</b><span>${guidance.summary}</span></div>`
    : "";
  intro.innerHTML = `<strong>当前类型：${getEnumLabel("wgame.BuffType", buffType)}</strong>${guidanceLine}<div class="hint">只显示公共字段、堆叠持续字段、当前 Buff 类型专属字段和相关表现字段；隐藏字段会保留在 Patch 中，不会被自动删除。</div>${modeLine}`;
  container.append(intro);

  if (state.uiMode === "Developer") {
    const layerWrap = document.createElement("div");
    layerWrap.className = "field";
    const currentLayer = entry.layer || "Mod";
    layerWrap.innerHTML = `<label><span>Layer</span><small>开发者模式可写 Mod / Patch，禁止 Base</small></label>`;
    const layerSelect = document.createElement("select");
    ["Mod", "Patch"].forEach(layer => {
      layerSelect.append(new Option(layer, layer, layer === currentLayer, layer === currentLayer));
    });
    layerSelect.disabled = !state.canWrite;
    layerSelect.addEventListener("change", event => {
      entry.layer = event.target.value;
      state.dirty = true;
      render();
    });
    layerWrap.append(layerSelect);
    container.append(layerWrap);
  }

  const issuesByField = getIssuesByField(entry);
  const groups = groupFields(fields, buffType);
  groups.forEach(group => {
    const section = document.createElement("section");
    section.className = "field-section";
    const heading = document.createElement("div");
    heading.className = "field-section-heading";
    heading.innerHTML = `<h3>${group.displayName}</h3><span>${group.fields.length} 项</span>`;
    const grid = document.createElement("div");
    grid.className = "field-grid-inner";

    group.fields.forEach(field => {
      const wrapper = document.createElement("div");
      const value = values[field.name] || "";
      const fieldIssues = issuesByField[field.name] || [];
      const invalid = fieldIssues.some(issue => issue.severity === "Error");
      wrapper.className = `field ${invalid ? "invalid" : ""} ${isWideField(field) ? "field-wide" : ""}`;

      const label = document.createElement("label");
      const requiredMark = field.required ? "<em>必填</em>" : "";
      label.innerHTML = `<span>${field.displayName || field.name}${requiredMark}</span><small>${field.name}</small>`;

      const input = createEditableControl(field, value);
      input.disabled = !state.canWrite;
      input.addEventListener("input", event => updateField(field.name, event.target.value));
      input.addEventListener("change", event => updateField(field.name, event.target.value));
      const hint = document.createElement("div");
      hint.className = "hint";
      hint.textContent = createFieldHint(field, fieldIssues);

      wrapper.append(label, input, hint);
      grid.append(wrapper);
    });

    section.append(heading, grid);
    container.append(section);
  });

  const statusEl = document.getElementById("draftStatus");
  statusEl.textContent = state.canWrite
    ? (state.dirty ? "未保存" : "已保存")
    : "只读预览";
  
  statusEl.className = "status-pill " + (state.canWrite ? (state.dirty ? "status-dirty" : "status-saved") : "status-readonly");
}

function createEditableControl(field, value) {
  if (field.type === "Enum") {
    const select = document.createElement("select");
    const enumDomain = getEnum(field.enumId);
    const options = enumDomain?.options || [];
    select.append(new Option(field.required ? "请选择" : "不设置", "", value === "", value === ""));
    if (options.length === 0) {
      select.append(new Option(value || "-", value || ""));
    } else {
      options.forEach(option => {
        const label = `${option.displayName} ${option.name}(${option.value})`;
        select.append(new Option(label, option.name, option.name === value, option.name === value));
      });
    }
    return select;
  }

  if (field.type === "Boolean") {
    const select = document.createElement("select");
    select.append(new Option("不设置", "", value === "", value === ""));
    select.append(new Option("是", "true", value === true || value === "true", value === true || value === "true"));
    select.append(new Option("否", "false", value === false || value === "false", value === false || value === "false"));
    return select;
  }

  const input = document.createElement("input");
  input.type = field.type === "Integer" || field.type === "Float" ? "number" : "text";
  if (field.type === "Float") input.step = "any";
  input.value = value;
  input.placeholder = FIELD_PLACEHOLDER[field.name] || (field.required ? "必填" : "可选");
  return input;
}

function updateField(fieldName, value) {
  const entry = getSelectedPatchEntry();
  if (!entry) return;
  entry.fields = entry.fields || {};
  entry.fields[fieldName] = value;
  if (fieldName === "Id") {
    entry.id = value;
    state.selectedBuffId = value;
  }
  if (fieldName === "Type") {
    state.selectedStepId = "type-fields";
  }
  state.dirty = true;
  state.statusMessage = "草稿已修改，保存后再生成报告。";
  scheduleDraftValidation();
  render();
}

function scheduleDraftValidation() {
  if (!state.canWrite) return;
  if (state.validateDraftTimer) clearTimeout(state.validateDraftTimer);
  state.validateDraftTimer = setTimeout(runDraftValidation, 300);
}

async function runDraftValidation() {
  if (!state.canWrite || !state.patch) return;
  try {
    const response = await fetch(withPackage(paths.apiValidateDraft), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(state.patch)
    });
    if (!response.ok) return;
    const data = await response.json();
    state.validation = { issues: data.issues || [] };
    renderValidation();
  } catch {
  }
}

function createFieldHint(field, issues = []) {
  if (issues.length > 0) {
    return issues.map(issue => issue.message).join("；");
  }
  const parts = [];
  parts.push(field.type);
  if (field.description) parts.push(field.description);
  if (field.enumId) parts.push(`enum: ${field.enumId}`);
  if (field.referenceSource) parts.push(`ref: ${field.referenceSource}`);
  if (field.unit) parts.push(`单位: ${field.unit}`);
  if (field.visibleWhenBuffTypes?.length) parts.push(`仅 ${field.visibleWhenBuffTypes.join(", ")}`);
  return parts.join(" / ");
}

function renderEnumsAndReferences() {
  const container = document.getElementById("enumReferenceView");
  container.innerHTML = "";
  const enums = state.manifest?.enums || [];
  const references = state.manifest?.references || [];

  enums.forEach(domain => {
    const row = document.createElement("div");
    row.className = "enum-row";
    const options = domain.options.map(o => `${o.displayName} ${o.name}(${o.value})`).join("，");
    row.innerHTML = `<h3>${domain.enumId}</h3><div class="subtle">${options}</div>`;
    container.append(row);
  });

  references.forEach(index => {
    const row = document.createElement("div");
    row.className = "reference-row";
    row.innerHTML = `<h3>${index.source}</h3><div class="subtle">${index.entries.length} references</div>`;
    index.entries.forEach(entry => {
      const item = document.createElement("div");
      item.className = "hint";
      item.textContent = `${entry.id} / ${entry.displayName} / ${entry.kind}`;
      row.append(item);
    });
    container.append(row);
  });
}

function renderValidation() {
  const container = document.getElementById("validationView");
  const issues = [...getLocalIssues(), ...(state.validation?.issues || [])];
  container.innerHTML = "";
  if (state.statusMessage) {
    const status = document.createElement("div");
    status.className = "issue-row severity-Info";
    status.innerHTML = `<h3>Info / EditorStatus</h3><div>${state.statusMessage}</div>`;
    container.append(status);
  }
  if (issues.length === 0) {
    container.append(empty(state.canWrite ? "没有校验报告；点击“生成报告”刷新。" : "没有校验报告；请通过本地 API 启动后生成报告。"));
    return;
  }
  issues.forEach(issue => {
    const row = document.createElement("div");
    row.className = `issue-row severity-${issue.severity}`;
    row.innerHTML = `<h3>${issue.severity} / ${issue.code}</h3><div>${issue.message}</div><div class="hint">${issue.source || "-"} / ${issue.rowId || "-"} / ${issue.field || "-"}</div>`;
    container.append(row);
  });
}

function getLocalIssues() {
  const schema = getBuffSchema();
  const entry = getSelectedPatchEntry();
  if (!schema || !entry) return [];
  const issues = [];
  (schema.fields || [])
    .filter(field => isFieldVisible(field, entry))
    .forEach(field => {
      validateFieldValue(entry, field).forEach(issue => issues.push(issue));
    });

  if (entry.fields?.Type === "DamageByAttr") {
    validateDamageByAttr(entry).forEach(issue => issues.push(issue));
  }

  return issues;
}

function getIssuesByField(entry) {
  const out = {};
  const allIssues = [...getLocalIssues(), ...(state.validation?.issues || [])];
  allIssues
    .filter(issue => issue.field && (!entry?.id || !issue.rowId || issue.rowId === "-" || issue.rowId === entry.id))
    .forEach(issue => {
      out[issue.field] = out[issue.field] || [];
      if (!out[issue.field].some(item => item.code === issue.code && item.message === issue.message)) {
        out[issue.field].push(issue);
      }
    });
  return out;
}

function validateFieldValue(entry, field) {
  const value = entry.fields?.[field.name] ?? "";
  const raw = String(value).trim();
  const source = entry.source || state.patch?.source || "BuffFactoryData";
  const rowId = entry.id || "-";
  const issues = [];

  if (field.required && raw === "") {
    issues.push({
      severity: "Error",
      code: "Editor.RequiredField",
      message: `${field.displayName || field.name} 是必填字段。`,
      source,
      rowId,
      field: field.name
    });
    return issues;
  }

  if (raw === "") return issues;

  if (field.type === "Integer" && !/^-?\d+$/.test(raw)) {
    issues.push({ severity: "Error", code: "Editor.IntegerInvalid", message: `${field.displayName || field.name} 必须是整数。`, source, rowId, field: field.name });
  } else if (field.type === "Float" && !Number.isFinite(Number(raw))) {
    issues.push({ severity: "Error", code: "Editor.FloatInvalid", message: `${field.displayName || field.name} 必须是数字。`, source, rowId, field: field.name });
  } else if (field.type === "Enum" && !isValidEnumValue(field.enumId, raw)) {
    issues.push({ severity: "Error", code: "Editor.EnumInvalid", message: `${field.displayName || field.name} 不是可选枚举值。`, source, rowId, field: field.name });
  }

  return issues;
}

function validateDamageByAttr(entry) {
  const source = entry.source || state.patch?.source || "BuffFactoryData";
  const rowId = entry.id || "-";
  const fields = entry.fields || {};
  const issues = [];
  const duration = readInteger(fields.Duration);
  const cooldown = readInteger(fields.HitCooldown);
  const values = String(fields.Values || "").trim();

  if (duration !== null && duration <= 0) {
    issues.push({ severity: "Error", code: "Editor.DamageByAttr.Duration", message: "持续伤害 Buff 的持续时间必须大于 0 毫秒。", source, rowId, field: "Duration" });
  }

  if (cooldown !== null) {
    if (cooldown <= 0) {
      issues.push({ severity: "Error", code: "Editor.DamageByAttr.HitCooldown", message: "触发间隔必须大于 0 毫秒。", source, rowId, field: "HitCooldown" });
    } else if (duration !== null && duration > 0 && cooldown > duration) {
      issues.push({ severity: "Warning", code: "Editor.DamageByAttr.HitCooldownLong", message: "触发间隔超过持续时间，预览中可能不会产生伤害。", source, rowId, field: "HitCooldown" });
    }
  }

  if (values && !isSupportedDamageFormula(values)) {
    issues.push({ severity: "Error", code: "Editor.DamageByAttr.Values", message: "当前预览只支持固定数值，或 caster.Attack * 系数。", source, rowId, field: "Values" });
  }

  return issues;
}

function readInteger(value) {
  const raw = String(value ?? "").trim();
  if (!/^-?\d+$/.test(raw)) return null;
  return Number(raw);
}

function isSupportedDamageFormula(raw) {
  const text = String(raw || "").trim();
  if (Number.isFinite(Number(text))) return true;
  const match = /^caster\.Attack(?:\s*\*\s*(-?\d+(?:\.\d+)?))?$/i.exec(text);
  return Boolean(match);
}

function isValidEnumValue(enumId, raw) {
  const enumDomain = getEnum(enumId);
  if (!enumDomain || !Array.isArray(enumDomain.options) || enumDomain.options.length === 0) return true;
  return enumDomain.options.some(option => option.name === raw);
}

function isFieldVisible(field, entry) {
  if (state.uiMode === "Mod" && HIDDEN_IN_MOD.has(field.name)) return false;
  const visibleWhen = field.visibleWhenBuffTypes || [];
  if (visibleWhen.length === 0) return true;
  const buffType = entry?.fields?.Type || "";
  return visibleWhen.includes(buffType);
}

function groupFields(fields, buffType = "") {
  const groups = [];
  const guidanceOrder = TYPE_GUIDANCE[buffType]?.fields || [];
  fields.forEach(field => {
    const groupId = field.groupId || "default";
    let group = groups.find(item => item.groupId === groupId);
    if (!group) {
      group = {
        groupId,
        displayName: field.groupDisplayName || groupId,
        fields: []
      };
      groups.push(group);
    }
    group.fields.push(field);
  });
  groups.forEach(group => {
    group.fields.sort((a, b) => {
      const ai = guidanceOrder.indexOf(a.name);
      const bi = guidanceOrder.indexOf(b.name);
      if (ai >= 0 || bi >= 0) return (ai < 0 ? 999 : ai) - (bi < 0 ? 999 : bi);
      return 0;
    });
  });
  return groups;
}

function isWideField(field) {
  return field.type === "LocalizedText" || field.name === "Values" || field.name.endsWith("Effect") || field.isList;
}

function renderMergePreview() {
  const container = document.getElementById("mergePreviewView");
  const previews = Array.isArray(state.mergePreview) ? state.mergePreview : [];
  container.innerHTML = "";
  if (previews.length === 0) {
    container.append(empty("没有合并预览"));
    return;
  }

  previews.flatMap(preview => preview.records || []).forEach(record => {
    const row = document.createElement("div");
    row.className = "preview-row";
    row.innerHTML = `<h3>${record.changeKind} / ${record.source}:${record.id}</h3>`;
    Object.entries(record.fields || {}).forEach(([key, value]) => {
      row.append(keyValue(key, value));
    });
    container.append(row);
  });
}

function renderLocalization() {
  const container = document.getElementById("localizationView");
  container.innerHTML = "";

  const baseEntries = state.manifest?.localization || [];
  const patchEntries = state.localizationPatch?.entries || [];

  if (baseEntries.length === 0 && patchEntries.length === 0) {
    container.append(empty("没有多语言条目"));
    return;
  }

  baseEntries.forEach(entry => {
    const row = document.createElement("div");
    row.className = "locale-row";
    row.innerHTML = `<h3>${entry.key} <span class="badge base">项目内置</span></h3><div>${entry.zhCN || ""}</div><div class="subtle">${entry.enUS || ""}</div>`;
    container.append(row);
  });

  patchEntries.forEach((entry, index) => {
    const row = document.createElement("div");
    row.className = "locale-row editable";
    const header = document.createElement("h3");
    header.innerHTML = `<span>包内追加 #${index + 1}</span> <span class="badge patch">Patch</span>`;
    const keyInput = document.createElement("input");
    keyInput.type = "text";
    keyInput.placeholder = state.uiMode === "Mod" ? `mod.${state.mod?.packageId || "..."}.` : "key";
    keyInput.value = entry.key || "";
    keyInput.addEventListener("input", e => { entry.key = e.target.value; state.localizationDirty = true; });

    const zhArea = document.createElement("textarea");
    zhArea.placeholder = "zh-CN";
    zhArea.value = entry.zhCN || "";
    zhArea.addEventListener("input", e => { entry.zhCN = e.target.value; state.localizationDirty = true; });

    const enArea = document.createElement("textarea");
    enArea.placeholder = "en-US";
    enArea.value = entry.enUS || "";
    enArea.addEventListener("input", e => { entry.enUS = e.target.value; state.localizationDirty = true; });

    const actions = document.createElement("div");
    actions.className = "locale-actions";
    const removeBtn = document.createElement("button");
    removeBtn.type = "button";
    removeBtn.className = "secondary";
    removeBtn.textContent = "删除";
    removeBtn.addEventListener("click", () => {
      state.localizationPatch.entries.splice(index, 1);
      state.localizationDirty = true;
      renderLocalization();
    });
    actions.append(removeBtn);

    row.append(header, keyInput, zhArea, enArea, actions);
    container.append(row);
  });
}

function addLocalizationRow() {
  state.localizationPatch = state.localizationPatch || { entries: [] };
  state.localizationPatch.entries = state.localizationPatch.entries || [];
  const prefix = state.uiMode === "Mod" && state.mod?.packageId ? `mod.${state.mod.packageId}.` : "";
  state.localizationPatch.entries.push({ key: prefix, zhCN: "", enUS: "" });
  state.localizationDirty = true;
  renderLocalization();
}

async function saveLocalization() {
  if (!state.canWrite) {
    state.statusMessage = "当前是静态预览模式，不能保存多语言。";
    renderValidation();
    return;
  }
  const entries = (state.localizationPatch?.entries || []).filter(e => e.key && e.key.trim().length > 0);
  if (state.uiMode === "Mod") {
    const requiredPrefix = `mod.${state.mod?.packageId || ""}.`;
    const bad = entries.find(e => !e.key.startsWith(requiredPrefix));
    if (bad) {
      state.statusMessage = `Mod 模式下 key 必须以 '${requiredPrefix}' 开头：${bad.key}`;
      renderValidation();
      return;
    }
  }
  const url = withPackage(`${paths.apiLocalization}?mode=${encodeURIComponent(state.uiMode)}`);
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ entries })
  });
  if (!response.ok) {
    let msg = `HTTP ${response.status}`;
    try { const data = await response.json(); if (data?.error) msg = data.error; } catch {}
    state.statusMessage = `保存多语言失败：${msg}`;
    renderValidation();
    return;
  }
  state.localizationPatch = await response.json();
  if (!state.localizationPatch || !Array.isArray(state.localizationPatch.entries)) {
    state.localizationPatch = { entries: [] };
  }
  state.localizationDirty = false;
  state.statusMessage = "多语言 Patch 已保存。";
  renderLocalization();
  renderValidation();
}

async function copyStepContext() {
  const workflow = getWorkflow();
  const step = getSelectedStep();
  if (!workflow || !step) return;

  if (state.canWrite) {
    try {
      const url = withPackage(`${paths.apiAiContext}?workflow=${encodeURIComponent(workflow.workflowId)}&step=${encodeURIComponent(step.stepId)}&mode=${encodeURIComponent(state.uiMode)}`);
      const response = await fetch(url, { cache: "no-store" });
      if (response.ok) {
        const text = await response.text();
        navigator.clipboard?.writeText(text);
        state.statusMessage = "AI 步骤上下文已复制（含 schemaSlice / draftSlice）。";
        renderValidation();
        return;
      }
    } catch {
    }
  }

  const lines = [
    "MxFramework Authoring Step Context",
    `workflow=${workflow.workflowId}`,
    `title=${workflow.title}`,
    `category=${workflow.category}`,
    `mode=${workflow.mode}`,
    `targetSource=${workflow.target?.source || ""}`,
    `targetLayer=${workflow.target?.layer || ""}`,
    `step=${step.stepId}`,
    `stepTitle=${step.title}`,
    `actor=${step.actor}`,
    `status=${step.status}`,
    `description=${step.description}`,
    `inputs=${(step.inputs || []).join(", ")}`,
    `outputs=${(step.outputs || []).join(", ")}`,
    `checks=${(step.checks || []).join(", ")}`,
    `aiPromptHint=${step.aiPromptHint || ""}`
  ];
  navigator.clipboard?.writeText(lines.join("\n"));
  state.statusMessage = "步骤上下文已复制。";
  renderValidation();
}

async function savePatch() {
  if (!state.canWrite) {
    state.statusMessage = "当前是静态预览模式，不能保存。请使用 start-authoring-editor.sh 启动。";
    renderValidation();
    return;
  }

  normalizePatch();
  const response = await fetch(withPackage(paths.apiPatch), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(state.patch)
  });
  if (!response.ok) {
    state.statusMessage = `保存失败：HTTP ${response.status}`;
    renderValidation();
    return;
  }

  applyLoadedState(await response.json(), true);
  state.statusMessage = "Patch 已保存。";
  render();
}

async function generateReport() {
  if (!state.canWrite) {
    state.statusMessage = "当前是静态预览模式，不能生成报告。请使用 start-authoring-editor.sh 启动。";
    renderValidation();
    return;
  }
  if (state.dirty) {
    await savePatch();
  }

  const response = await fetch(withPackage(paths.apiReport), { method: "POST", body: "" });
  if (!response.ok) {
    state.statusMessage = `生成报告失败：HTTP ${response.status}`;
    renderValidation();
    return;
  }

  applyLoadedState(await response.json(), true);
  state.statusMessage = "报告已生成，校验和合并预览已刷新。";
  render();
}

async function previewRuntime() {
  if (!state.canWrite) {
    state.statusMessage = "当前是静态预览模式，不能连接运行时预览。请使用 start-authoring-editor.sh 启动。";
    renderValidation();
    return;
  }
  const entry = getSelectedPatchEntry();
  if (!entry) {
    state.statusMessage = "请先选择一个 Buff 草稿。";
    renderValidation();
    return;
  }

  if (state.dirty) {
    await savePatch();
  }
  state.statusMessage = "正在连接 Unity Preview Server 并加载当前 Patch...";
  renderValidation();
  renderRuntimePreview({ loading: true });

  const url = withPackage(`${paths.apiPreviewRun}?buff=${encodeURIComponent(entry.id || entry.fields?.Id || "")}&target=TestTarget&caster=TestCaster&stack=1&waitTicks=60`);
  try {
    const response = await fetch(url, { method: "POST", body: "" });
    state.previewResult = await response.json();
    state.previewStatus = {
      connected: Boolean(state.previewResult.connected),
      status: state.previewResult.status,
      message: state.previewResult.message,
      endpoint: state.previewResult.endpoint,
      port: state.previewResult.port,
      handshake: state.previewResult.handshake
    };
    state.statusMessage = state.previewResult.success
      ? "运行时预览完成。"
      : `运行时预览未完成：${state.previewResult.message || state.previewResult.status || "unknown"}`;
  } catch (error) {
    state.previewResult = { connected: false, success: false, status: "error", message: error?.message || String(error) };
    state.previewStatus = state.previewResult;
    state.statusMessage = "运行时预览请求失败：" + state.previewResult.message;
  }
  renderValidation();
  renderRuntimePreview();
}

async function exportRuntimePatch() {
  const btn = document.getElementById("exportRuntimePatchButton");
  btn.disabled = true;
  btn.textContent = "导出中...";

  try {
    const pkg = state.packageRelative || "";
    const mode = document.getElementById("modeSelect")?.value || "Mod";
    const body = JSON.stringify({ package: pkg, sourceId: "authoring_export", layer: mode === "Developer" ? "Patch" : "Mod" });
    const url = withPackage("/api/runtime-patch/export");

    const resp = await fetch(url, { method: "POST", headers: { "Content-Type": "application/json" }, body });
    const result = await resp.json();

    if (result.success) {
      state.statusMessage = `运行时 Patch 已导出 → ${result.outputPath}（${result.buffCount} Buff, ${result.modCount} Modifier）`;
      state.exportResult = result;
    } else {
      const errMsgs = (result.errors || []).map(e => `${e.field}: ${e.message}`).join("；");
      state.statusMessage = `导出失败：${errMsgs}`;
      state.exportResult = { success: false, errors: result.errors };
    }
  } catch (error) {
    state.statusMessage = "导出请求失败：" + (error?.message || String(error));
    state.exportResult = { success: false, errors: [{ field: "", message: error?.message || String(error) }] };
  }

  btn.disabled = false;
  btn.textContent = "导出运行时 Patch";
  renderValidation();
}

function renderRuntimePreview(override = null) {
  const container = document.getElementById("runtimePreviewView");
  if (!container) return;
  container.innerHTML = "";
  if (override?.loading) {
    container.append(empty("正在请求运行时预览..."));
    return;
  }

  const result = state.previewResult;
  if (!result) {
    container.append(empty("尚未运行预览。"));
    return;
  }

  const preview = result.result || null;
  const metadata = preview?.configMetadata || result.load?.configMetadata || {};
  const previewMode = preview?.previewMode || result.previewMode || result.error?.previewMode || "-";
  const stateName = getPreviewStateName(result, preview);
  const summary = previewRow(`预览状态 / ${stateName.label}`, `preview-state preview-state-${stateName.kind}`);
  const badgeLine = document.createElement("div");
  badgeLine.className = "badge-row";
  badgeLine.append(
    badge(result.connected ? "connected" : "unavailable"),
    badge(result.status || "-"),
    badge(`mode ${previewMode}`)
  );
  if (previewMode === "dummy") badgeLine.append(badge("fallback"));
  if (preview?.truncated) badgeLine.append(badge("truncated"));
  summary.append(badgeLine);
  summary.append(
    keyValue("连接", result.connected ? "已连接" : "未连接"),
    keyValue("状态", result.status || "-"),
    keyValue("previewMode", previewMode)
  );
  if (result.endpoint) summary.append(keyValue("端点", result.endpoint));
  if (result.message) summary.append(keyValue("消息", result.message));
  if (result.code !== undefined) summary.append(keyValue("错误码", result.code));
  if (result.reason) summary.append(keyValue("原因", result.reason));
  container.append(summary);

  if (result.load) {
    const load = previewRow("Patch / Config Metadata");
    load.append(
      keyValue("loaded", (result.load.loadedPatchIds || []).join(", ") || "-"),
      keyValue("warnings", (result.load.mergeWarnings || []).join(", ") || "-"),
      keyValue("elapsedMs", result.load.elapsedMs ?? "-")
    );
    container.append(load);
  }

  if (metadata && hasPreviewMetadata(metadata)) {
    const meta = previewRow("Config Metadata");
    meta.append(
      keyValue("sourceId", metadata.sourceId || "-"),
      keyValue("layer", metadata.layer || "-"),
      keyValue("loadedPatchIds", joinPreviewList(metadata.loadedPatchIds)),
      keyValue("changedConfigIds", joinPreviewList(metadata.changedConfigIds)),
      keyValue("failedConfigIds", joinPreviewList(metadata.failedConfigIds)),
      keyValue("mergeWarnings", joinPreviewList(metadata.mergeWarnings))
    );
    container.append(meta);
  }

  if (preview) {
    const row = previewRow(`Buff 结果 / ${preview.appliedBuffId || "-"}`);
    row.append(
      keyValue("success", preview.success ? "true" : "false"),
      keyValue("loadedPatchIds", joinPreviewList(preview.loadedPatchIds)),
      keyValue("buffSnapshots", (preview.buffSnapshots || []).length),
      keyValue("attributeChanges", (preview.attributeChanges || []).length),
      keyValue("damageTicks", (preview.damageTicks || []).length),
      keyValue("statusChanges", (preview.statusChanges || []).length),
      keyValue("truncated", preview.truncated ? "true" : "false")
    );
    const perf = preview.performance || {};
    row.append(keyValue("performance", `load ${perf.loadMs ?? 0}ms / apply ${perf.applyMs ?? 0}ms / ticks ${perf.tickCount ?? 0} / total ${perf.totalMs ?? 0}ms`));
    container.append(row);

    appendPreviewCollection(container, "Buff Snapshots", preview.buffSnapshots, item =>
      `${item.ownerId || "-"} / buff ${item.buffId || "-"} / stack ${item.stack ?? "-"} / ${item.remainingMs ?? "-"}ms left / caster ${item.casterId || "-"}`
    );
    appendPreviewCollection(container, "Attribute Changes", preview.attributeChanges, item =>
      `${item.ownerId || "-"} / ${item.attribute || "-"}: ${item.before ?? "-"} -> ${item.after ?? "-"} / ${item.deltaSource || "-"}`
    );
    appendPreviewCollection(container, "Damage Ticks", preview.damageTicks, item =>
      `${item.buffId || "-"} / tick ${item.tickIndex ?? "-"} / ${item.amount ?? "-"} / ${item.damageType || "-"} / ${item.elementType || "-"}`
    );
    appendPreviewCollection(container, "Status Changes", preview.statusChanges, item =>
      `${item.ownerId || "-"} / ${item.status || "-"} / ${item.applied ? "applied" : "removed"}`
    );
  }

  const errors = collectPreviewErrors(result, preview);
  if (errors.length > 0) {
    const errorBox = previewRow("Errors", "preview-errors");
    errors.forEach(error => {
      errorBox.append(keyValue(`code ${error.code ?? result.code ?? "-"}`, [
        error.reason,
        error.previewMode,
        error.buffId ? `buff ${error.buffId}` : "",
        error.targetId ? `target ${error.targetId}` : "",
        error.message || result.message || ""
      ].filter(Boolean).join(" / ")));
    });
    container.append(errorBox);
  }

  const logs = mergePreviewLogs(result.result?.logs, result.logs?.logs);
  if (logs.length > 0) {
    const logBox = previewRow("运行时日志");
    logs.slice(0, 12).forEach(log => {
      const item = document.createElement("div");
      item.className = "hint";
      item.textContent = `#${log.seq ?? "-"} ${log.level || "info"} ${log.message || ""}`;
      logBox.append(item);
    });
    container.append(logBox);
  }
}

function getPreviewStateName(result, preview) {
  if (result.status === "unavailable" || !result.connected) return { kind: "unavailable", label: "Unavailable" };
  if (preview?.success || result.success) {
    if ((preview?.previewMode || result.previewMode) === "dummy") return { kind: "fallback", label: "Dummy Fallback" };
    if ((preview?.previewMode || result.previewMode) === "scene") return { kind: "success", label: "Scene Preview" };
    return { kind: "success", label: "Success" };
  }
  if (result.status === "failed" || result.error || preview?.success === false) return { kind: "failed", label: "Failed" };
  return { kind: "loading", label: result.status || "Unknown" };
}

function previewRow(title, extraClass = "") {
  const row = document.createElement("div");
  row.className = `preview-row ${extraClass}`.trim();
  const heading = document.createElement("h3");
  heading.textContent = title;
  row.append(heading);
  return row;
}

function hasPreviewMetadata(metadata) {
  return Boolean(metadata.sourceId || metadata.layer ||
    (metadata.loadedPatchIds || []).length ||
    (metadata.changedConfigIds || []).length ||
    (metadata.failedConfigIds || []).length ||
    (metadata.mergeWarnings || []).length);
}

function joinPreviewList(values) {
  return Array.isArray(values) && values.length > 0 ? values.join(", ") : "-";
}

function appendPreviewCollection(container, title, values, formatter) {
  if (!Array.isArray(values) || values.length === 0) return;
  const row = previewRow(title);
  values.slice(0, 16).forEach(item => {
    const div = document.createElement("div");
    div.className = "hint";
    div.textContent = formatter(item || {});
    row.append(div);
  });
  if (values.length > 16) row.append(keyValue("more", `${values.length - 16} hidden`));
  container.append(row);
}

function collectPreviewErrors(result, preview) {
  const errors = [];
  if (Array.isArray(preview?.errors)) errors.push(...preview.errors);
  if (Array.isArray(result.error?.data?.result?.errors)) errors.push(...result.error.data.result.errors);
  if (result.error && errors.length === 0) {
    errors.push({
      code: result.error.code,
      message: result.error.message,
      reason: result.error.reason,
      previewMode: result.error.previewMode
    });
  }
  return errors;
}

function mergePreviewLogs(...groups) {
  const seen = new Set();
  const logs = [];
  groups.forEach(group => {
    if (!Array.isArray(group)) return;
    group.forEach(log => {
      const key = `${log.seq ?? ""}|${log.level || ""}|${log.message || ""}`;
      if (seen.has(key)) return;
      seen.add(key);
      logs.push(log);
    });
  });
  return logs;
}

function normalizePatch() {
  state.patch = state.patch || {};
  state.patch.schemaVersion = state.patch.schemaVersion || "1.0";
  state.patch.source = state.patch.source || "BuffFactoryData";
  state.patch.entries = state.patch.entries || [];
  state.patch.entries.forEach(entry => {
    entry.operation = entry.operation || "Upsert";
    entry.source = entry.source || state.patch.source;
    if (state.uiMode === "Mod") {
      entry.layer = "Mod";
    } else {
      entry.layer = entry.layer === "Patch" ? "Patch" : "Mod";
    }
    entry.fields = entry.fields || {};
    entry.id = entry.id || entry.fields.Id || "";
    if (entry.id && !entry.fields.Id) entry.fields.Id = entry.id;
  });
}

function getWorkflow() {
  return state.manifest?.workflows?.[0] || null;
}

function getSelectedStep() {
  const workflow = getWorkflow();
  return workflow?.steps?.find(step => step.stepId === state.selectedStepId) || workflow?.steps?.[0] || null;
}

function getBuffSchema() {
  return state.manifest?.schemas?.find(schema => schema.schemaId === "BuffFactoryData") || null;
}

function getEnum(enumId) {
  return state.manifest?.enums?.find(domain => domain.enumId === enumId) || null;
}

function getEnumLabel(enumId, value) {
  const option = getEnum(enumId)?.options?.find(item => item.name === value);
  if (!option) return value || "-";
  return `${option.displayName} ${option.name}`;
}

function getSelectedPatchEntry() {
  const entries = state.patch?.entries || [];
  return entries.find(entry => entry.id === state.selectedBuffId) || entries[0] || null;
}

function keyValue(key, value) {
  const row = document.createElement("div");
  row.className = "summary-row key-value";
  const keyEl = document.createElement("div");
  keyEl.className = "key";
  keyEl.textContent = key;
  const valueEl = document.createElement("div");
  valueEl.className = "value";
  valueEl.textContent = value === undefined || value === null ? "-" : String(value);
  row.append(keyEl, valueEl);
  return row;
}

function badge(text) {
  const el = document.createElement("span");
  el.className = "badge";
  el.textContent = text;
  return el;
}

function empty(text) {
  const el = document.createElement("div");
  el.className = "empty";
  el.textContent = text;
  return el;
}

function sectionTitle(text) {
  const el = document.createElement("h3");
  el.textContent = text;
  return el;
}

function openNewBuffWizard() {
  const modal = document.getElementById("newBuffModal");
  const select = document.getElementById("newBuffType");
  select.innerHTML = "";
  const enumDomain = getEnum("wgame.BuffType");
  const options = enumDomain?.options || [];
  if (options.length === 0) {
    ["DamageByAttr", "Numerical", "Condition", "ChangeAttr", "Positive", "Status"].forEach(name =>
      select.append(new Option(name, name)));
  } else {
    options.forEach(opt => select.append(new Option(`${opt.displayName} ${opt.name}(${opt.value})`, opt.name)));
  }
  document.getElementById("newBuffId").value = "";
  document.getElementById("newBuffName").value = "";
  document.getElementById("newBuffWarning").textContent = "";
  modal.classList.remove("hidden");
}

function closeNewBuffWizard() {
  document.getElementById("newBuffModal").classList.add("hidden");
}

function suggestBuffName() {
  const id = document.getElementById("newBuffId").value.trim();
  if (!id) return;
  const nameInput = document.getElementById("newBuffName");
  if (!nameInput.value || nameInput.dataset.auto === "1") {
    const pkgId = state.mod?.packageId || "package";
    nameInput.value = `mod.${pkgId}.buff.${id}.name`;
    nameInput.dataset.auto = "1";
  }
  document.getElementById("newBuffWarning").textContent = "";
}

function confirmNewBuffWizard() {
  const buffType = document.getElementById("newBuffType").value;
  const id = document.getElementById("newBuffId").value.trim();
  const name = document.getElementById("newBuffName").value.trim();
  const warn = document.getElementById("newBuffWarning");

  if (!buffType) { warn.textContent = "请选择 BuffType。"; return; }
  if (!id) { warn.textContent = "Buff Id 不能为空。"; return; }
  state.patch = state.patch || { schemaVersion: "1.0", source: "BuffFactoryData", entries: [] };
  state.patch.entries = state.patch.entries || [];
  if (state.patch.entries.some(e => e.id === id)) {
    warn.textContent = `Buff Id '${id}' 已存在，请换一个。`;
    return;
  }

  const defaults = defaultFieldsForBuffType(buffType);
  const entry = {
    operation: "Upsert",
    source: state.patch.source || "BuffFactoryData",
    layer: "Mod",
    id,
    fields: { Id: id, Type: buffType, Name: name || `mod.${state.mod?.packageId || "package"}.buff.${id}.name`, ...defaults }
  };
  state.patch.entries.push(entry);
  state.selectedBuffId = id;
  state.dirty = true;
  state.statusMessage = `已新增 Buff 草稿 ${id}（${buffType}），保存后再生成报告。`;
  closeNewBuffWizard();
  render();
  scheduleDraftValidation();
}

function defaultFieldsForBuffType(buffType) {
  const schema = getBuffSchema();
  if (!schema) return {};
  const out = {};
  (schema.fields || []).forEach(field => {
    const visibleWhen = field.visibleWhenBuffTypes || [];
    const matches = visibleWhen.length === 0 || visibleWhen.includes(buffType);
    if (!matches || !field.required) return;
    if (field.type === "Enum") {
      const opts = getEnum(field.enumId)?.options || [];
      if (opts.length > 0) out[field.name] = opts[0].name;
    } else if (field.type === "Integer" || field.type === "Float") {
      out[field.name] = field.name === "Duration" ? "5000" : "0";
    } else {
      out[field.name] = "";
    }
  });
  return out;
}
