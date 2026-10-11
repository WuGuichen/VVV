import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import * as THREE from "three";
import "../web/src/utils.js";
import "../web/src/bodyWorkspace.js";
import "../web/src/bodyPartsWorkbench.js";
import "../web/src/viewport3d.js";

function loadFunctions(file, names) {
  const source = fs.readFileSync(new URL(file, import.meta.url), "utf8");
  for (const name of names) {
    const start = source.indexOf(`function ${name}(`);
    assert(start >= 0, name);
    const end = source.indexOf("\nfunction ", start + 1);
    vm.runInThisContext(source.slice(start, end < 0 ? undefined : end));
  }
}
loadFunctions("../web/src/inspector.js", ["field", "positiveField", "integerField", "bodyPartOptions", "firstValue", "normalizeOptions",
  "commitInspectorField", "normalizeColliderIdAfterEdit", "normalizeColliderShapeDimensions", "renderColliderManualEditor", "describeColliderDimensions", "formatMeters",
  "normalizeFieldSpec", "renderField", "renderFieldSections", "renderFieldSectionHeading", "getInspectorInputTarget", "formatFieldValue",
  "renderFieldHint", "renderPickerAttribute", "renderTitleAttribute", "renderDatalist", "renderSelectOption", "safeDomId"]);
loadFunctions("../web/src/contextPanels.js", ["beginUndoTransaction", "updateUndoTransactionAfter", "commitUndoTransaction"]);
loadFunctions("../web/src/viewport3d.js", ["syncHurtboxMeshVisibility", "isCharacterPreviewLayerEnabled"]);
globalThis.FIELD_GROUP_LABELS = {};
globalThis.UNDO_HISTORY_LIMIT = 50;
globalThis.serializePackage = value => JSON.stringify(value);
globalThis.serializeBodyPartDamageState = () => "";
globalThis.captureUndoContext = () => ({ selectedPath: state.selectedPath });
globalThis.renderPathFieldViewer = () => "";
for (const name of ["renderUndoRedoState", "refreshDirtyFromCleanSnapshot", "renderShellStatus", "render", "renderViewportModeBar", "scheduleCharacterStudioSessionPersistence"]) globalThis[name] = () => {};
const geometry = { bodyProfile: { massKg: 76 },
  bodyParts: [{ partId: "torso", displayName: "躯干", bonePath: "bone.spine", defaultHitZoneId: "hit.torso" },
    { partId: "head", displayName: "头部", bonePath: "bone.head", locatorId: "head.locator", defaultHitZoneId: "hit.head" }],
  colliders: [{ colliderId: "torso_sphere", partId: "torso", shape: "Sphere", radius: 0.2, physicsLayer: "character.body" },
    { colliderId: "head_sphere", partId: "head", shape: "Sphere", radius: 0.15, physicsLayer: "character.body" }],
  weaponAttachments: [{ weaponId: "preserved" }] };
globalThis.state = { package: { geometry }, selectedPath: "geometry/body_parts/head", canWrite: true,
  activeInspectorTab: "diagnostics", contextDockCollapsed: false, layers: { colliders: false, weapons: true },
  hurtboxDisplayMode: "selected", hurtboxDisplayPartId: "torso", bodyPartsPreviewScope: "part", undoStack: [], redoStack: [] };
const originalGeometry = JSON.stringify(geometry);
assert(createBodyWorkspaceItem("collider"));
const added = geometry.colliders.at(-1);
assert.equal(added.partId, "head", "Creation uses the current part rather than the first part.");
assert.equal(added.hitZoneId, "hit.head");
assert.equal(added.localPose.parentKind, "Bone");
assert.equal(added.localPose.parentPath, "bone.head");
assert.equal(state.selectedPath, `geometry/colliders/${added.colliderId}`);
assert.equal(state.activeInspectorTab, "properties");
assert.equal(state.contextDockCollapsed, true);
assert.equal(state.undoStack.length, 1);
assert.equal(JSON.stringify(JSON.parse(state.undoStack[0].beforePackage).geometry), originalGeometry);
assert.deepEqual(geometry.weaponAttachments, [{ weaponId: "preserved" }]);
assert.equal(geometry.bodyProfile.massKg, 76);
assert(createBodyWorkspaceItem("part"));
const newPart = geometry.bodyParts.at(-1);
assert.equal(newPart.parentPartId, "head");
assert.equal(newPart.displayName, "新身体部位");
assert.equal(state.undoStack.length, 2);
assert.equal(JSON.parse(state.undoStack[1].beforePackage).geometry.bodyParts.length, 2);
state.canWrite = false;
assert.equal(createBodyWorkspaceItem("collider"), false);
assert.equal(state.undoStack.length, 2);
state.canWrite = true;
assert.equal(createBodyWorkspaceItem("collider", { partId: "missing" }), false);

// A single scoped selector controls all three modes without overwriting legacy preferences.
state.selectedPath = `geometry/colliders/${added.colliderId}`;
const content = new THREE.Group();
const meshes = geometry.colliders.map(collider => {
  const mesh = new THREE.Mesh(new THREE.SphereGeometry(0.1), new THREE.MeshBasicMaterial());
  mesh.userData.hurtbox = collider;
  content.add(mesh);
  return mesh;
});
let rebuilds = 0;
globalThis.viewportSelectionSync = () => syncHurtboxMeshVisibility(content, geometry);
globalThis.renderViewport = () => { rebuilds++; };
let scopeControl, createButtons = [], templateButton, navHtml = "";
const nav = {
  querySelectorAll: selector => selector === "[data-body-create]" ? createButtons : [],
  querySelector: selector => selector === "[data-body-preview-scope]" ? scopeControl : selector === "[data-humanoid-add]" ? templateButton : null,
  set innerHTML(value) {
    navHtml = value;
    scopeControl = { focus() {}, addEventListener(type, callback) { this[type] = callback; } };
    templateButton = { addEventListener(type, callback) { this[type] = callback; } };
    createButtons = ["part", "collider"].map(kind => ({ dataset: { bodyCreate: kind }, addEventListener(type, callback) { this[type] = callback; } }));
  }
};
globalThis.document = { getElementById: () => nav };
renderBodyWorkspace();
assert(navHtml.includes("新增部位") && navHtml.includes("添加受击体") && navHtml.includes("补齐人形受击体"));
for (const [mode, expected] of [["selected", [false, false, true]], ["part", [false, true, true]], ["all", [true, true, true]]]) {
  scopeControl.change({ target: { value: mode } });
  assert.deepEqual(meshes.map(mesh => mesh.visible), expected);
  assert.equal(state.selectedPath, `geometry/colliders/${added.colliderId}`);
}
assert.equal(rebuilds, 0);
assert.equal(state.hurtboxDisplayMode, "selected");
assert.equal(state.hurtboxDisplayPartId, "torso");
assert.equal(state.layers.colliders, false);
assert(!renderColliderManualEditor(added).includes('data-collider-edit-action="show-'));
let templateCalls = 0;
globalThis.addHumanoidHurtboxes = () => templateCalls++;
templateButton.click();
assert.equal(templateCalls, 1, "The visible template entry calls the existing generator.");
createButtons.find(button => button.dataset.bodyCreate === "collider").click();
assert.equal(geometry.colliders.at(-1).partId, "head");

// Source identity/layer values are displayed accurately and are not rewritten by ordinary editing.
const collider = geometry.colliders[0];
const sourceFields = [field("colliderId"), field("partId", { type: "select", options: [] }),
  field("shape", { type: "select", options: ["Sphere", "Capsule", "Box"] }),
  positiveField("radius", "Radius", { unit: "m" }), positiveField("damageMultiplierOverride", "Damage", {}),
  field("physicsLayer", { type: "select", options: ["CharacterHurtbox"] })];
const target = { kind: "collider", value: collider };
const fields = getBodyPartEditorFields(target, sourceFields);
let html = renderFieldSections(target, fields);
assert(html.includes("所属部位与空间绑定") && html.includes("形状尺寸") && html.includes("受击规则"));
assert(html.includes("data-body-part-advanced>"));
assert(html.includes("character.body") && html.includes("CharacterHurtbox"));
assert(!html.includes('data-field="physicsLayer"') && !html.includes('data-field="colliderId"'));
assert(html.includes("胶囊") && html.includes('class="attribute-unit">×'));
assert.equal(commitInspectorField(target, { dataset: { field: "physicsLayer" }, value: "CharacterHurtbox" }), undefined);
assert.equal(collider.physicsLayer, "character.body");
const oldId = collider.colliderId;
collider.shape = "Capsule";
normalizeColliderShapeDimensions(collider);
normalizeColliderIdAfterEdit(collider, "shape", "Sphere", oldId);
assert.equal(collider.colliderId, oldId, "Shape changes preserve stable references even for legacy shape-suffixed IDs.");
collider.partId = "head";
normalizeColliderIdAfterEdit(collider, "partId", "torso", oldId);
assert.equal(collider.colliderId, oldId);
const partFields = getBodyPartEditorFields({ kind: "part", value: geometry.bodyParts[0] }, [field("partId"), field("displayName"), field("parentPartId", { type: "select", options: [] })]);
assert(partFields.find(spec => spec.path === "partId").readOnly);
assert(partFields.find(spec => spec.path === "parentPartId").options.find(option => option.value === "torso").disabled);

// The new template button shares readiness and the existing API/undo path.
const templateControl = {}, oldTemplateControl = {}, templateStatus = {};
globalThis.document = {
  getElementById: id => id === "addHumanoidHurtboxesButton" ? oldTemplateControl : null,
  querySelectorAll: selector => selector === "[data-humanoid-add], [data-humanoid-retry]" ? [templateControl]
    : selector === "[data-humanoid-status]" ? [templateStatus] : []
};
globalThis.renderInspector = () => {};
globalThis.selectPath = path => { state.selectedPath = path; };
await import("../web/src/humanoidHurtboxes.js");
state.packageRelative = "fixture";
state.viewportMode = "character";
state.canWrite = true;
updateHumanoidHurtboxButton();
assert(templateControl.disabled && templateStatus.textContent.includes("等待"));
setHumanoidHurtboxReference({ key: "fixture-bones", packageRelative: "fixture", bones: [{ path: "bone.head" }] });
assert.equal(templateControl.disabled, false);
const preserved = JSON.stringify(geometry.colliders);
const oldCount = geometry.colliders.length;
const undoCount = state.undoStack.length;
globalThis.fetch = async (url, request) => {
  assert.equal(url, "/api/character/humanoid-hurtboxes");
  assert.equal(JSON.stringify(JSON.parse(request.body).geometry.colliders), preserved);
  return { ok: true, status: 200, json: async () => ({ success: true, roles: [], addedParts: [],
    addedColliders: [{ colliderId: "template.head", partId: "head", shape: "Capsule", radius: 0.15, height: 0.3 }], preservedColliderCount: oldCount }) };
};
await addHumanoidHurtboxes();
assert.equal(JSON.stringify(geometry.colliders.slice(0, oldCount)), preserved);
assert.equal(state.selectedPath, "geometry/colliders/template.head");
assert.equal(getBodyPartsPreviewScope(), "all");
assert.equal(state.activeInspectorTab, "properties");
assert.equal(state.undoStack.length, undoCount + 1);
assert.equal(state.layers.colliders, false);
assert.equal(state.hurtboxDisplayMode, "selected");
globalThis.fetch = async () => ({ ok: false, status: 422, json: async () => ({ success: false, issues: ["请选择缺少的骨骼"] }) });
await addHumanoidHurtboxes();
assert.equal(state.selectedPath, "geometry/body/parts", "Template errors stay in the body workspace with retry/mapping controls.");
assert.equal(state.undoStack.length, undoCount + 1);
assert(state.message.includes("请选择缺少的骨骼"));
console.log("Body parts workbench: selected-part creation/undo, unified scopes, template readiness/API success and failure, grouped fields, source-layer display and stable identity passed.");
