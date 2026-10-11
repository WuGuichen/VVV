import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import * as THREE from "three";
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
loadFunctions("../web/src/inspector.js", ["activateTreeNode", "selectPath", "leaveBaseAttributeWorkspace", "isTreeNodeActive", "renderTree"]);
loadFunctions("../web/src/viewport3d.js", ["isCharacterPreviewLayerEnabled", "syncHurtboxMeshVisibility", "isViewportObjectVisible"]);
const geometry = {
  bodyParts: [{ partId: "torso" }, { partId: "head" }],
  colliders: [{ colliderId: "torso", partId: "torso" }, { colliderId: "head", partId: "head" }]
};
globalThis.state = { selectedPath: "overview", package: { geometry }, layers: { colliders: true }, hurtboxDisplayMode: "part", hurtboxDisplayPartId: "torso" };
const buttons = geometry.colliders.map(collider => ({
  dataset: { path: `geometry/colliders/${collider.colliderId}` },
  classList: { toggle(key, value) { this[key] = value; } }
}));
let treeWrites = 0;
let treeHtml = "";
const tree = {
  scrollTop: 275,
  querySelector: () => ({ open: true }),
  querySelectorAll: () => buttons,
  set innerHTML(value) { treeWrites++; treeHtml = value; },
  get innerHTML() { return treeHtml; }
};
buttons.forEach(button => button.addEventListener = () => {});
globalThis.el = { packageTree: tree };
globalThis.findTarget = path => ({ kind: "collider", value: geometry.colliders.find(c => path.endsWith(`/${c.colliderId}`)) });
globalThis.getDefaultBoneFieldPath = () => "";
globalThis.renderResourceBindingBar = () => {};
globalThis.renderResourcePicker = () => {};
let inspectorUpdates = 0;
globalThis.renderInspector = () => inspectorUpdates++;
let rebuilds = 0;
globalThis.renderViewport = () => rebuilds++;
const content = new THREE.Group();
const meshes = geometry.colliders.map(collider => {
  const mesh = new THREE.Mesh(new THREE.SphereGeometry(0.1), new THREE.MeshBasicMaterial());
  mesh.userData.hurtbox = collider;
  content.add(mesh);
  return mesh;
});
globalThis.viewportSelectionSync = () => syncHurtboxMeshVisibility(content, geometry);

activateTreeNode(buttons[1]);
assert.equal(rebuilds, 0, "Switching body parts must preserve the viewport");
assert.equal(state.hurtboxDisplayPartId, "head");
assert.deepEqual(meshes.map(mesh => mesh.visible), [false, true]);
assert.equal(isViewportObjectVisible(meshes[0]), false, "Hidden colliders cannot be picked");
assert.equal(buttons[1].classList.active, true);
assert.equal(treeWrites, 0, "Selection must preserve the existing tree DOM and expansion");
assert.equal(tree.scrollTop, 275);
assert.equal(inspectorUpdates, 1);

state.hurtboxDisplayMode = "selected";
activateTreeNode(buttons[0]);
assert.deepEqual(meshes.map(mesh => mesh.visible), [true, false]);
state.hurtboxDisplayMode = "all";
activateTreeNode(buttons[1]);
assert.deepEqual(meshes.map(mesh => mesh.visible), [true, true]);
assert.equal(rebuilds, 0);

// Special overlays still require rebuilding; unavailable live views still initialize.
selectPath("config/gameplay-camera", { preserveViewport: true });
assert.equal(rebuilds, 1);
state.selectedPath = "overview";
globalThis.viewportSelectionSync = null;
activateTreeNode(buttons[0]);
assert.equal(rebuilds, 2);

globalThis.buildTree = () => [];
globalThis.buildTechnicalTree = () => [];
globalThis.renderTreeButton = () => "";
renderTree();
assert(treeHtml.includes('class="debug-tree" open'), "Content rebuilds preserve tree expansion");
assert.equal(tree.scrollTop, 275);
const beforeAttributes = rebuilds;
const layerPreferences = structuredClone(state.layers);
state.viewportMode = "weapon";
state.contextDockCollapsed = false;
selectPath("config/attributes", { preserveViewport: true });
assert.equal(state.viewportMode, "character");
assert.equal(state.contextDockCollapsed, true);
assert.equal(rebuilds, beforeAttributes + 1, "Attribute entry must rebuild the clean preview.");
globalThis.normalizeContextTab = value => value;
globalThis.renderContextDockState = () => {};
globalThis.renderWorkflowBar = () => {};
activateTreeNode({ dataset: { contextTab: "binding" } });
assert.equal(state.selectedPath, "overview", "Opening another workbench must leave the attribute workspace.");
assert.equal(state.contextDockCollapsed, false);
assert.deepEqual(state.layers, layerPreferences);
console.log("Tree selection: no viewport/tree rebuild for ordinary selection; body filters, hidden picking, special overlays and tree state passed.");
