import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import * as THREE from "three";
import "../web/src/utils.js";
import "../web/src/bodyWorkspace.js";
import "../web/src/bodyCollisionPreview.js";
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
loadFunctions("../web/src/inspector.js", ["activateTreeNode", "selectPath", "leaveBaseAttributeWorkspace", "isTreeNodeActive",
  "findTarget", "target", "field", "positiveField", "identityField", "datalistField", "normalizeOptions", "editableFields", "getInspectorInputTarget", "shouldRebuildViewportForField"]);
loadFunctions("../web/src/contextPanels.js", ["renderLayoutState", "isWorkflowStageActive"]);
loadFunctions("../web/src/viewport3d.js", ["isCharacterPreviewLayerEnabled", "getActiveViewportTransformTarget", "syncHurtboxMeshVisibility"]);

globalThis.BODY_KIND_OPTIONS = ["Skeletal", "Primitive"];
globalThis.UNITY_GLTF_PREVIEW_POSE_ADAPTER = "unityGltfPreviewMirrorX";
globalThis.escapeHtml = value => String(value ?? "").replaceAll("&", "&amp;").replaceAll('"', "&quot;").replaceAll("<", "&lt;").replaceAll(">", "&gt;");
const geometry = {
  bodyProfile: { bodyKind: "Skeletal", heightMeters: 1.8, radiusMeters: 0.35, massKg: 76,
    defaultCapsule: { height: 1.6, radius: 0.3, center: { x: 0.12, y: 0.9, z: 0.2 } } },
  bodyParts: [{ partId: "torso", displayName: "躯干" }, { partId: "head", displayName: "头部" }, { partId: "empty", displayName: "无受击体部位" }],
  colliders: [{ colliderId: "torso.capsule", partId: "torso", shape: "Capsule" },
    { colliderId: "torso.box", partId: "torso", shape: "Box" }, { colliderId: "head.sphere", partId: "head", shape: "Sphere" },
    { colliderId: "orphan", partId: "missing", shape: "Sphere" }]
};
globalThis.state = { selectedPath: "overview", package: { geometry, manifest: { packageId: "000004", displayName: "Arcane Trickster" } },
  viewportMode: "weapon", contextDockCollapsed: false, activeContextTab: "binding", activeInspectorTab: "import",
  layers: { colliders: false, sockets: true, traces: true, weapons: true }, hurtboxDisplayMode: "all", hurtboxDisplayPartId: "head" };
const savedGeometry = JSON.stringify(geometry);
const savedLayers = structuredClone(state.layers);
const rootButton = { dataset: { path: "geometry/body" }, classList: { toggle(key, value) { this[key] = value; } } };
let navHtml = "", navButtons = [], lists = [], scopeSelect = null;
const host = {
  hidden: true,
  get innerHTML() { return navHtml; },
  set innerHTML(value) {
    navHtml = value;
    navButtons = [...value.matchAll(/<button\b[^>]*data-body-path="([^"]+)"[^>]*>/g)].map(match => ({
      dataset: { bodyPath: match[1] }, addEventListener(type, callback) { this[type] = callback; }, focus() {}, scrollIntoView() {}
    }));
    lists = [...value.matchAll(/data-body-list="([^"]+)"/g)].map(match => ({ dataset: { bodyList: match[1] }, scrollTop: 0 }));
    scopeSelect = value.includes("data-body-preview-scope") ? { addEventListener(type, callback) { this[type] = callback; } } : null;
  },
  querySelectorAll(selector) { return selector === "[data-body-list]" ? lists : selector === "button[data-body-path]" ? navButtons : []; },
  querySelector(selector) { return selector === "[data-body-preview-scope]" ? scopeSelect : lists.find(item => selector.includes(`"${item.dataset.bodyList}"`)) || null; }
};
const classes = {};
globalThis.el = { workspace: { classList: { toggle(key, value) { classes[key] = value; } } },
  treeCollapseButton: { setAttribute() {} }, packageTree: { querySelectorAll: () => [rootButton] } };
globalThis.document = { getElementById: id => id === "bodyWorkspaceNavigation" ? host : null };
const content = new THREE.Group();
const meshes = geometry.colliders.map(collider => {
  const mesh = new THREE.Mesh(new THREE.SphereGeometry(0.1), new THREE.MeshBasicMaterial());
  mesh.userData.hurtbox = collider;
  content.add(mesh);
  return mesh;
});
let rebuilds = 0, inspected = null;
globalThis.renderViewport = () => { rebuilds++; syncHurtboxMeshVisibility(content, geometry); };
globalThis.viewportSelectionSync = () => syncHurtboxMeshVisibility(content, geometry);
globalThis.renderInspector = () => { inspected = findTarget(state.selectedPath); };
globalThis.getDefaultBoneFieldPath = () => "";
globalThis.normalizeContextTab = value => value;
for (const name of ["renderResourceBindingBar", "renderResourcePicker", "renderContextDockState", "renderInspectorTabState", "renderWorkflowBar", "renderTree"]) globalThis[name] = () => {};
function click(path) {
  const button = navButtons.find(item => item.dataset.bodyPath === path);
  assert(button, `Visible navigation button: ${path}`);
  button.click();
}

// Main navigation must recover both the preview mode and inspector from any previous workbench.
activateTreeNode(rootButton);
assert.equal(state.activeInspectorTab, "properties");
assert.equal(state.viewportMode, "character");
assert.equal(state.contextDockCollapsed, true);
assert.equal(classes["body-workspace"], true);
assert.equal(rootButton.classList.active, true);
assert.equal(isWorkflowStageActive({}, { contextTab: "binding" }), false);
state.contextDockCollapsed = false;
assert.equal(isTreeNodeActive({ contextTab: "binding" }), false, "Restored stale workbench state cannot highlight two main categories.");
state.contextDockCollapsed = true;
assert.equal(inspected.value, geometry.bodyProfile);
assert.equal(getActiveViewportTransformTarget(), null);
assert(navHtml.includes("整体身体") && navHtml.includes("移动碰撞配置") && navHtml.includes("部位与受击体"));
for (const layer of Object.keys(savedLayers)) assert.equal(isCharacterPreviewLayerEnabled(layer), false);

// The capsule entry edits the SAME body object, while showing only capsule fields.
const bodyFields = editableFields("body", geometry.bodyProfile);
const overallFields = getBodyWorkspaceFields(inspected, bodyFields);
click("geometry/body/capsule");
assert.equal(inspected.kind, "body");
assert.equal(inspected.value, geometry.bodyProfile);
const capsuleFields = getBodyWorkspaceFields(inspected, bodyFields);
assert(capsuleFields.every(spec => spec.path.startsWith("defaultCapsule.")));
assert.equal(capsuleFields.length, 5);
assert.equal(overallFields.length + capsuleFields.length, bodyFields.length, "Field split must neither lose nor duplicate editable data.");
assert.equal(getInspectorInputTarget(inspected, "defaultCapsule.height").value, geometry.bodyProfile);
assert(shouldRebuildViewportForField(inspected, "defaultCapsule.center.x"));
assert.equal(rootButton.classList.active, true);
const capsule = createMovementCollisionPreviewGeometry(THREE, geometry.bodyProfile).getObjectByName("movement-capsule-proxy");
const bounds = new THREE.Box3().setFromObject(capsule);
assert(Math.abs(bounds.getSize(new THREE.Vector3()).y - 1.6) < 0.0001);
assert(Math.abs(bounds.getSize(new THREE.Vector3()).x - 0.6) < 0.0001);
assert(Math.abs(capsule.position.y - 0.9) < 0.0001);
assert.equal(capsule.userData.objectPath, "geometry/body/capsule");

click("geometry/body/parts");
assert.equal(inspected.kind, "bodyPartsOverview");
assert.equal(isCharacterPreviewLayerEnabled("colliders"), true, "Body navigation exposes hurtboxes even if another workspace hid that layer.");
assert.deepEqual(meshes.map(mesh => mesh.visible), [true, true, true, true]);
assert(navHtml.includes("无受击体部位") && navHtml.includes("orphan"));
const beforeParts = rebuilds;
state.activeInspectorTab = "diagnostics";
click("geometry/body_parts/torso");
assert.equal(state.activeInspectorTab, "properties");
assert.equal(inspected.value, geometry.bodyParts[0]);
assert.equal(rootButton.classList.active, true);
assert.deepEqual(meshes.map(mesh => mesh.visible), [true, true, false, false]);
assert.equal(isBodyWorkspaceSelectionVisualSelected("geometry/colliders/torso.box"), true);
assert(!navButtons.some(item => item.dataset.bodyPath === "geometry/colliders/head.sphere"));
host.querySelector('[data-body-list="parts"]').scrollTop = 40;
click("geometry/colliders/torso.box");
assert.equal(host.querySelector('[data-body-list="parts"]').scrollTop, 40);
assert.equal(inspected.value, geometry.colliders[1]);
assert(navHtml.includes("躯干 → torso.box"));
assert.equal(rootButton.classList.active, true);
assert.equal(isBodyWorkspaceSelectionVisualSelected("geometry/colliders/torso.capsule"), false);
assert.equal(isHurtboxCoverageScopeAvailable(), false);
globalThis.PREVIEW_POSES = [];
globalThis.PREVIEW_MOTIONS = [];
el.previewPoseSelect = {};
el.previewMotionSelect = {};
el.hurtboxBindingReferenceButton = { classList: { toggle() {} }, setAttribute() {} };
renderPreviewControls();
assert.equal(el.hurtboxBindingReferenceButton.disabled, false, "Binding helpers must follow effective workspace visibility, not hidden legacy layer preferences.");
state.hurtboxDisplayMode = "part";
assert.equal(ensureHurtboxDisplayPartId(geometry), "torso");
assert.equal(state.hurtboxDisplayPartId, "head", "Hidden legacy controls must not mutate saved filters.");
state.hurtboxDisplayMode = "all";
scopeSelect.change({ target: { value: "all" } });
assert.equal(isHurtboxCoverageScopeAvailable(), true, "Coverage analysis remains available for all-hurtbox preview.");
assert.equal(state.selectedPath, "geometry/colliders/torso.box", "Changing display scope must not navigate away from the current collider.");
assert.deepEqual(meshes.map(mesh => mesh.visible), [true, true, true, true]);
assert(!navButtons.some(item => item.dataset.bodyPath === "geometry/colliders/head.sphere"), "Preview scope does not change the part's own collider list.");
scopeSelect.change({ target: { value: "part" } });
assert.equal(isHurtboxCoverageScopeAvailable(), false);
assert.deepEqual(meshes.map(mesh => mesh.visible), [true, true, false, false]);
assert.equal(rebuilds, beforeParts, "Selecting within body parts keeps the camera and scene; only visibility/selection change.");
assert.equal(getBodyWorkspacePickPath("resources/model"), "", "Clicking the rendered model cannot unexpectedly open resource editing.");
assert.equal(getBodyWorkspacePickPath("geometry/colliders/torso.box"), "geometry/colliders/torso.box");

click("geometry/body_parts/empty");
assert(navHtml.includes("当前范围没有受击体"));
assert(meshes.every(mesh => !mesh.visible));
click("geometry/body/parts");
click("geometry/colliders/orphan");
assert(navHtml.includes("未登记部位 · missing"));
assert.equal(inspected.value, geometry.colliders[3]);
click("geometry/body");
assert.equal(inspected.value, geometry.bodyProfile);

activateTreeNode({ dataset: { contextTab: "binding" } });
assert.equal(state.selectedPath, "overview");
assert.equal(state.contextDockCollapsed, false);
assert.equal(host.hidden, true);
assert.equal(classes["body-workspace"], false);
assert.equal(isCharacterPreviewLayerEnabled("colliders"), false);
assert.equal(isCharacterPreviewLayerEnabled("weapons"), true);
assert.deepEqual(state.layers, savedLayers);
assert.equal(state.hurtboxDisplayMode, "all");
assert.equal(state.hurtboxDisplayPartId, "head");
assert.equal(JSON.stringify(geometry), savedGeometry, "Browsing and capsule preview must not alter source data.");
console.log("Body workspace: entry recovery, list clicks, capsule source/preview, parent selection, scoped picking, empty/orphan entries and preference preservation passed.");
