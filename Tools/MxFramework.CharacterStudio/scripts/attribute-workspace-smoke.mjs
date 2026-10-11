import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";

function loadFunctions(file, names) {
  const source = fs.readFileSync(new URL(file, import.meta.url), "utf8");
  for (const name of names) {
    const start = source.indexOf(`function ${name}(`);
    const end = source.indexOf("\nfunction ", start + 1);
    assert(start >= 0, name);
    vm.runInThisContext(source.slice(start, end < 0 ? undefined : end));
  }
}
loadFunctions("../web/src/viewport3d.js", ["isCharacterPreviewLayerEnabled", "getActiveViewportTransformTarget", "buildWeaponVisualizationItems",
  "weaponIssue", "getWeaponVisualizationTone", "getWeaponVisualizationStatusText", "renderWeaponDataOverlayItem"]);
loadFunctions("../web/src/inspector.js", ["field", "integerField", "editableFields", "formatAttributeTimeHint", "renderField", "renderFieldHint", "renderObjectTitle"]);
loadFunctions("../web/src/contextPanels.js", ["renderLayoutState", "isWorkflowStageActive", "buildWorkflowStageView"]);

globalThis.state = { selectedPath: "config/attributes", viewportMode: "weapon", treeCollapsed: false,
  contextDockCollapsed: false, attributePreviewOverlays: false,
  layers: { colliders: true, sockets: true, traces: true, contactPoints: true, engagementAnchors: true, weapons: true },
  package: { manifest: { packageId: "000004", displayName: "Arcane Trickster" } }
};
const layers = structuredClone(state.layers);
globalThis.viewportTransformTargets = new Map([["previous", { transformKind: "collider" }]]);
state.weaponTransformKey = "previous";
assert.equal(getActiveViewportTransformTarget(), null, "Attribute editing must not retain an active geometry gizmo.");
const classes = {};
const controls = { attributePreviewControls: {}, attributeOverlayButton: { setAttribute() {} } };
globalThis.el = { workspace: { classList: { toggle: (key, value) => { classes[key] = value; } } },
  treeCollapseButton: { setAttribute() {} } };
globalThis.document = { getElementById: id => controls[id] };
renderLayoutState();
assert.equal(classes["attribute-workspace"], true);
assert.equal(classes["context-expanded"], false);
assert.equal(state.viewportMode, "character");
assert.equal(controls.attributePreviewControls.hidden, false);
for (const name of Object.keys(layers)) assert.equal(isCharacterPreviewLayerEnabled(name), name === "weapons");
state.attributePreviewOverlays = true;
assert(isCharacterPreviewLayerEnabled("colliders"));
state.selectedPath = "geometry/body";
state.attributePreviewOverlays = false;
renderLayoutState();
assert.equal(classes["attribute-workspace"], false);
assert(isCharacterPreviewLayerEnabled("colliders"));
assert.deepEqual(state.layers, layers, "Workspace changes must preserve layer preferences.");
assert.equal(getActiveViewportTransformTarget().transformKind, "collider");

globalThis.escapeHtml = value => String(value ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;");
globalThis.normalizeFieldSpec = value => value;
globalThis.getInspectorInputTarget = target => target;
globalThis.getNested = (value, key) => value[key];
globalThis.formatFieldValue = value => String(value);
for (const name of ["renderPickerAttribute", "renderTitleAttribute", "renderPathFieldViewer", "renderDatalist"]) globalThis[name] = () => "";
const specs = editableFields("baseAttributes");
const recoverySpec = specs.find(spec => spec.path === "scatterRecoveryRate");
assert.equal(recoverySpec.unit, "点／模拟帧");
assert(recoverySpec.help.includes("散势债务"));
const fieldHtml = renderField({ kind: "baseAttributes", value: { scatterRecoveryDelayFrames: 360 } },
  specs.find(spec => spec.path === "scatterRecoveryDelayFrames"));
assert(fieldHtml.includes('class="attribute-unit">帧'));
assert(fieldHtml.includes("6 秒模拟时间"));
assert(fieldHtml.includes("最后一次受压后"));
assert.equal(formatAttributeTimeHint("cohesionBreakRecoveryDelayFrames", 270), "4.5 秒模拟时间（按 60Hz）");
assert.equal(formatAttributeTimeHint("cohesionBreakRecoveryDelayFrames", 0), "不启用定时恢复");
state.selectedPath = "config/attributes";
assert(!renderObjectTitle({ kind: "baseAttributes" }).includes("config/attributes"));
state.activeContextTab = "binding";
assert.equal(isWorkflowStageActive({}, { contextTab: "binding" }), false, "The previous binding stage must not stay selected on the attribute page.");

let configuration = { slots: [{ slotId: "offHand", singleWeaponConfigurationId: "weapon.unarmed" }] };
globalThis.getActiveWeaponConfiguration = () => configuration;
globalThis.buildWeaponAttachmentForSlot = () => null;
globalThis.getEquipSlotLabel = slot => slot === "offHand" ? "副手" : "主手";
globalThis.getWeaponAttachmentIdentity = () => "";
globalThis.getWeaponAttachmentPath = () => "";
const unarmed = buildWeaponVisualizationItems({}, ["offHand"])[0];
assert.equal(unarmed.issues.length, 0);
assert.equal(getWeaponVisualizationTone(unarmed), "ok");
assert.equal(getWeaponVisualizationStatusText(unarmed), "无需挂接");
assert(renderWeaponDataOverlayItem(unarmed).includes("合法空手"));
assert(!renderWeaponDataOverlayItem(unarmed).includes("data-view-weapon"));
configuration = { slots: [] };
assert.equal(getWeaponVisualizationTone(buildWeaponVisualizationItems({}, ["offHand"])[0]), "error");
configuration = { slots: [{ slotId: "offHand", singleWeaponConfigurationId: "missing" }] };
assert.equal(getWeaponVisualizationTone(buildWeaponVisualizationItems({}, ["offHand"])[0]), "error");

globalThis.sortReadinessStages = stages => stages;
globalThis.combineWorkflowStageStatus = stages => stages[0].status;
globalThis.getWorkflowStageTone = status => ({ Blocked: "error", Warning: "warn", Ready: "ok", Deferred: "pending" })[status];
globalThis.getWorkflowActionTargetForStage = () => ({});
let evidence = { source: "none", tone: "pending" };
globalThis.getUnityImportEvidenceView = () => evidence;
const definition = { key: "unityImport", stageIds: ["UnityImport"], label: "Unity 导入" };
const snapshot = { stages: [{ id: "UnityImport", status: "Deferred" }] };
assert.equal(buildWorkflowStageView(definition, snapshot).statusLabel, "待导入 Unity");
evidence = { source: "none", tone: "error" };
assert.equal(buildWorkflowStageView(definition, snapshot).statusLabel, "", "Import errors must not be relabeled as a normal pending import.");
console.log("Attribute workspace, units, explicit unarmed slots and import status checks passed.");
