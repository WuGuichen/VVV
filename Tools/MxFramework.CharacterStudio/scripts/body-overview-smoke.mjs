import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import * as THREE from "three";
import "../web/src/utils.js";
import "../web/src/bodyWorkspace.js";
import "../web/src/bodyMeasurements.js";
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
loadFunctions("../web/src/inspector.js", ["field", "positiveField", "editableFields", "normalizeFieldSpec", "renderField",
  "getInspectorInputTarget", "formatFieldValue", "renderFieldSections", "renderFieldSectionHeading", "renderSelectOption",
  "normalizeOptions", "safeDomId", "renderFieldHint", "renderPickerAttribute", "renderTitleAttribute", "renderDatalist", "commitInspectorField"]);
globalThis.FIELD_GROUP_LABELS = {};
globalThis.renderPathFieldViewer = () => "";
const body = { profileId: "geometry.000004.body", bodyKind: "Skeletal", bodyScale: 1.7,
  heightMeters: 1.86, radiusMeters: 0.34, massKg: 76,
  defaultCapsule: { height: 1.8, radius: 0.3, center: { x: 0, y: 0.9, z: 0 } },
  modelRootStableId: "model.root", skeletonRootStableId: "Armature", locatorRootStableId: "locators.root", defaultPhysicsProfileId: "physics.humanoid.medium" };
const snapshot = JSON.stringify(body);
globalThis.state = { selectedPath: "geometry/body", package: { geometry: { bodyProfile: body } }, bodyMeasurementsVisible: true, bodyAdvancedBindingsOpen: false };
const target = { kind: "body", value: body };
const allFields = editableFields("body", body);
const fields = getBodyWorkspaceFields(target, allFields);
let html = renderFieldSections(target, fields);
assert(html.indexOf("尺寸与质量") < html.indexOf("身体结构"));
assert(html.indexOf("身体结构") < html.indexOf("高级绑定"));
assert(html.includes('data-body-advanced-bindings>'), "Advanced references start collapsed.");
assert(html.includes('<option value="Skeletal" selected>骨骼角色</option>'));
assert(html.includes('data-field="heightMeters"') && html.includes('class="attribute-unit">m'));
assert(html.includes('data-field="massKg"') && html.includes('class="attribute-unit">kg'));
assert(html.includes("没有有效胶囊高度时使用此值"));
assert(html.includes("0 表示使用游戏默认质量"));
assert(html.includes("身体倍率（未启用）") && html.includes('1.7<span class="attribute-unit">×'));
assert(html.includes(body.profileId));
assert(!html.includes('data-field="profileId"') && !html.includes('data-field="bodyScale"'), "Inactive scale and stable identity cannot be edited from the inspector.");
for (const key of ["profileId", "bodyScale"]) {
  assert.equal(commitInspectorField(target, { dataset: { field: key }, value: "999" }), undefined);
}
assert.equal(JSON.stringify(body), snapshot, "Readonly controls must neither create history nor modify source fields.");

let persistenceCount = 0;
const details = { open: false, addEventListener(type, callback) { this[type] = callback; } };
globalThis.el = { inspector: { querySelector: selector => selector === "[data-body-advanced-bindings]" ? details : null } };
globalThis.scheduleCharacterStudioSessionPersistence = () => persistenceCount++;
bindBodyInspectorActions();
details.open = true;
details.toggle();
html = renderFieldSections(target, fields);
assert(html.includes("data-body-advanced-bindings open>"));
assert.equal(persistenceCount, 1);
state.selectedPath = "geometry/body/capsule";
const capsuleFields = getBodyWorkspaceFields(target, allFields);
assert.equal(fields.length + capsuleFields.length, allFields.length);
assert(capsuleFields.every(spec => spec.path.startsWith("defaultCapsule.")));
assert(!renderFieldSections(target, capsuleFields).includes("高级绑定"));
state.selectedPath = "geometry/body";

// Guides represent declared dimensions in metres; they do not apply the inactive BodyScale.
const guides = createBodyMeasurementGuides(THREE, body);
const positions = name => guides.getObjectByName(name).geometry.attributes.position;
const heightPositions = positions("reference-height");
assert(Math.abs(heightPositions.getY(1) - 1.86) < 0.00001);
assert.equal(heightPositions.getY(0), 0);
const radiusPositions = positions("reference-radius");
assert(Math.abs(radiusPositions.getX(1) - 0.34) < 0.00001);
assert.equal(positions("ground-origin").getY(0), 0);
const scaledData = getBodyMeasurementData({ ...body, bodyScale: 5 });
assert.deepEqual(scaledData, getBodyMeasurementData(body));
assert.equal(guides.getObjectByProperty("userData.objectPath", "geometry/body"), undefined);
guides.traverse(object => assert(!object.userData.objectPath, "Measurement guides cannot be selected as authoring geometry."));
assert(renderBodyMeasurementSvg().includes("1.86 m"));
assert(renderBodyMeasurementSvg().includes("r = 0.34 m"));
assert(renderBodyMeasurementLegend().includes("质量：76 kg"));
assert.equal(JSON.stringify(body), snapshot);

// Repeated updates and toggles replace and dispose only helper geometry.
const content = new THREE.Group();
const character = new THREE.Object3D();
character.position.set(0.2, 0.3, 0.4);
character.scale.setScalar(1.2);
content.add(character);
const characterTransform = character.matrix.clone();
let legend = "";
const host = { querySelector: () => ({ remove() { legend = ""; } }), insertAdjacentHTML(position, value) { legend = value; } };
const refresh = attachBodyMeasurementPreview(THREE, content, host, body, () => new THREE.Group());
const previousGuide = content.getObjectByName("body-dimension-guides");
let disposed = 0;
previousGuide.traverse(object => object.geometry?.addEventListener("dispose", () => disposed++));
body.heightMeters = 2.1;
body.radiusMeters = 0.5;
body.massKg = 0;
refresh();
assert.equal(disposed, 3);
assert(legend.includes("2.1 m") && legend.includes("0.5 m") && legend.includes("使用游戏默认质量"));
assert.equal(content.children.length, 2);
assert.deepEqual(character.matrix, characterTransform);
assert.deepEqual(character.scale.toArray(), [1.2, 1.2, 1.2]);
assert.deepEqual(body.defaultCapsule, JSON.parse(snapshot).defaultCapsule);
state.bodyMeasurementsVisible = false;
refresh();
assert.equal(content.children.length, 1);
assert.equal(legend, "");
assert.equal(renderBodyMeasurementSvg(), "");
state.bodyMeasurementsVisible = true;
state.selectedPath = "geometry/body/parts";
refresh();
assert.equal(content.children.length, 1, "Other workspaces cannot retain overall-body measurements.");

// The visible toolbar toggles the guides without navigation or a character rebuild.
state.selectedPath = "geometry/body";
let toggle, toolbarHtml = "", refreshCount = 0;
const toolbar = {
  querySelectorAll: () => [],
  querySelector: selector => selector === "[data-body-measurements-toggle]" ? toggle : null,
  set innerHTML(value) {
    toolbarHtml = value;
    toggle = { focus() {}, addEventListener(type, callback) { this[type] = callback; } };
  }
};
globalThis.document = { getElementById: () => toolbar };
globalThis.refreshBodyMeasurementPreview = () => { refreshCount++; return true; };
globalThis.renderViewport = () => { throw new Error("Guide toggle must preserve a loaded preview"); };
renderBodyWorkspace();
assert(toolbarHtml.includes("隐藏尺寸参考"));
toggle.click();
assert.equal(state.bodyMeasurementsVisible, false);
assert(toolbarHtml.includes("显示尺寸参考"));
toggle.click();
assert.equal(state.bodyMeasurementsVisible, true);
assert.equal(state.selectedPath, "geometry/body");
assert.equal(refreshCount, 2);
console.log("Body overview: grouped fields, readonly identity/scale, units/help, fold persistence, metric guides, source isolation, guide disposal and toolbar toggles passed.");
