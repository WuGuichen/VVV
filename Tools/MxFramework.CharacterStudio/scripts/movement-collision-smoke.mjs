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
const fixture = JSON.parse(fs.readFileSync(new URL("./fixtures/movement-collision-runtime.json", import.meta.url), "utf8"));
for (const item of fixture.cases) {
  const body = { defaultCapsule: { height: item.height, radius: item.radius, center: { y: item.center } } };
  const view = getMovementCollisionPreview(body);
  const actual = view.effective;
  for (const key of ["standingHeightMm", "radiusMm", "centerMm", "boxHeightMm"]) assert.equal(actual[key], item[key], `${JSON.stringify(item)} ${key}`);
  assert.equal(view.proxy.height, item.boxHeightMm / 1000, "CapsuleProxy.FromHalfExtents shares the registered bounds height.");
  assert.equal(view.proxy.radius, item.radiusMm / 1000);
  assert.equal(view.proxy.center.y, view.registeredBounds.center.y);
  assert(Math.abs((view.proxy.segmentEndY - view.proxy.segmentStartY) - (view.proxy.height - 2 * view.proxy.radius)) < 0.000001);
}
const fallback = getMovementCollisionPreview({ heightMeters: 2.2, radiusMeters: 0.4, defaultCapsule: { height: 0, radius: 0, center: { y: 0 } } });
assert.equal(fallback.effective.height, 2.2);
assert.equal(fallback.effective.radius, 0.4);
assert.equal(fallback.effective.center.y, 1.1);
assert.equal(getMovementCollisionPreview({}).effective.height, 1.86);

const body = { profileId: "body.test", bodyScale: 1, heightMeters: 1.86, radiusMeters: 0.34,
  defaultCapsule: { height: 1.86, radius: 0.34, center: { x: 0.2, y: 5, z: -0.3 } } };
const snapshot = JSON.stringify(body);
globalThis.state = { selectedPath: "geometry/body/capsule", canWrite: true, package: { geometry: { bodyProfile: body } },
  movementSourceCapsuleVisible: false, undoStack: [], redoStack: [] };
globalThis.UNITY_GLTF_PREVIEW_POSE_ADAPTER = "unityGltfPreviewMirrorX";
const content = new THREE.Group(), pickables = [];
let legend = "", summary = "";
const host = { querySelector: () => ({ remove() { legend = ""; } }), insertAdjacentHTML(where, html) { legend = html; } };
const refresh = attachMovementCollisionPreview(THREE, content, pickables, host, body, "none");
const box = content.getObjectByName("movement-standing-box");
const proxy = content.getObjectByName("movement-capsule-proxy");
assert.equal(proxy.geometry.type, "CapsuleGeometry", "The moving proxy is shown as an actual capsule, separately from the world collider.");
assert.equal(box.geometry.type, "BoxGeometry");
assert.equal(pickables[0], proxy, "Preview picking targets the moving proxy.");
assert.deepEqual(box.position.toArray(), [0, 1.52, 0], "Runtime box ignores XZ and limits standing Y.");
assert.deepEqual(proxy.position.toArray(), box.position.toArray());
const size = new THREE.Box3().setFromObject(box).getSize(new THREE.Vector3());
assert(Math.abs(size.x - 0.68) < 0.00001 && Math.abs(size.y - 1.86) < 0.00001);
const proxySize = new THREE.Box3().setFromObject(proxy).getSize(new THREE.Vector3());
assert(proxySize.distanceTo(size) < 0.00001, "Distinct shapes share the same outer dimensions.");
assert.equal(content.getObjectByName("movement-source-capsule"), undefined);
assert.equal(pickables.length, 1);
assert(legend.includes("移动胶囊代理") && legend.includes("注册阻挡盒体") && legend.includes("0.59 m"));
summary = renderMovementCollisionInspectorSummary();
assert(summary.includes("原值继续保留") && summary.includes("配置值") && summary.includes("站立参数"));
assert(summary.includes("高度减半径之间"));
assert(summary.includes("轴对齐盒体障碍") && summary.includes("角色包围盒进行盒体扫掠"));
assert(summary.includes("旋转盒体障碍") && summary.includes("胶囊代理进行扫掠"));
assert(summary.includes("水平圆形判定结合高度重叠"));
assert(!summary.includes("默认站立生效边界") && !legend.includes("游戏移动边界（盒体）"));
assert.equal(JSON.stringify(body), snapshot);
let disposed = 0;
box.geometry.addEventListener("dispose", () => disposed++);
proxy.geometry.addEventListener("dispose", () => disposed++);
state.movementSourceCapsuleVisible = true;
refresh();
assert.equal(disposed, 2);
assert.equal(pickables.length, 1, "Refreshing replaces the old pick target.");
assert.deepEqual(content.getObjectByName("movement-source-capsule").position.toArray(), [0.2, 5, -0.3]);
assert(legend.includes("源胶囊参考"));
assert(renderMovementCollisionSvg().includes("stroke-dasharray"));
state.movementRegisteredBoundsVisible = false;
refresh();
assert.equal(content.getObjectByName("movement-standing-box"), undefined);
assert(content.getObjectByName("movement-capsule-proxy"));
assert(content.getObjectByName("movement-source-capsule"));
assert(!renderMovementCollisionSvg().includes('data-movement-primitive="registered-box"'));
assert(renderMovementCollisionSvg().includes('data-movement-primitive="capsule-proxy"'));
assert(renderMovementCollisionSvg().includes('data-movement-primitive="source-capsule"'));
assert(!legend.includes("紫色"));
state.movementRegisteredBoundsVisible = true;
refresh();
assert(content.getObjectByName("movement-standing-box"));
assert(renderMovementCollisionSvg().includes('data-movement-primitive="registered-box"'));
body.defaultCapsule.height = 0.4;
refresh();
assert.equal(content.getObjectByName("movement-source-capsule"), undefined, "Invalid source capsule is omitted, not silently enlarged.");
assert(getMovementCollisionPreview(body).notes.some(note => note.includes("半径已缩小")));
assert.equal(getMovementCollisionPreview(body).effective.height, 0.4);
assert.equal(getMovementCollisionPreview(body).effective.radius, 0.2);
assert(!renderMovementCollisionSvg().includes("stroke-dasharray"));
Object.assign(body, JSON.parse(snapshot));

// Readonly source XZ fields cannot trigger a write even through the generic input handler.
loadFunctions("../web/src/inspector.js", ["field", "positiveField", "editableFields", "commitInspectorField"]);
const fields = editableFields("body", body).filter(spec => spec.path.startsWith("defaultCapsule."));
assert.equal(fields.length, 5);
for (const key of ["defaultCapsule.center.x", "defaultCapsule.center.z"]) {
  assert(fields.find(spec => spec.path === key).readOnly);
  assert.equal(commitInspectorField({ kind: "body", value: body }, { dataset: { field: key }, value: "0" }), undefined);
}
assert.equal(JSON.stringify(body), snapshot);

// Alignment uses the existing package undo transaction; only center.y changes.
loadFunctions("../web/src/contextPanels.js", ["beginUndoTransaction", "updateUndoTransactionAfter", "commitUndoTransaction"]);
globalThis.UNDO_HISTORY_LIMIT = 50;
globalThis.serializePackage = value => JSON.stringify(value);
globalThis.serializeBodyPartDamageState = () => "";
globalThis.captureUndoContext = () => ({ selectedPath: state.selectedPath });
for (const name of ["renderUndoRedoState", "refreshDirtyFromCleanSnapshot", "renderShellStatus", "renderInspector", "scheduleCharacterStudioSessionPersistence"]) globalThis[name] = () => {};
globalThis.refreshMovementCollisionPreview = refresh;
assert.equal(alignMovementCollisionToGround(), true);
assert.equal(state.undoStack.length, 1);
assert.equal(body.defaultCapsule.center.y, 0.93);
assert.equal(body.defaultCapsule.center.x, 0.2);
assert.equal(body.defaultCapsule.center.z, -0.3);
assert.equal(getMovementCollisionPreview(body).effective.bottom, 0);
assert.equal(alignMovementCollisionToGround(), false, "Already grounded data is not rewritten.");
assert.equal(state.undoStack.length, 1);
assert.equal(JSON.parse(state.undoStack[0].beforePackage).geometry.bodyProfile.defaultCapsule.center.y, 5);
assert.equal(JSON.parse(state.undoStack[0].afterPackage).geometry.bodyProfile.defaultCapsule.center.y, 0.93);
state.canWrite = false;
body.defaultCapsule.center.y = 5;
assert.equal(alignMovementCollisionToGround(), false);
assert.equal(body.defaultCapsule.center.y, 5);

// The source overlay toggle is a view preference, not a source edit or navigation.
let toggle, boundsToggle, navHtml = "", updates = 0;
const nav = { querySelectorAll: () => [],
  querySelector: selector => selector === "[data-movement-source-toggle]" ? toggle : selector === "[data-movement-bounds-toggle]" ? boundsToggle : null,
  set innerHTML(value) {
    navHtml = value;
    toggle = { focus() {}, addEventListener(type, callback) { this[type] = callback; } };
    boundsToggle = { focus() {}, addEventListener(type, callback) { this[type] = callback; } };
  } };
globalThis.document = { getElementById: () => nav };
globalThis.refreshMovementCollisionPreview = () => { updates++; return true; };
globalThis.renderViewport = () => { throw new Error("Loaded movement preview should refresh without rebuilding the character"); };
state.movementSourceCapsuleVisible = false;
renderBodyWorkspace();
assert(navHtml.includes("移动碰撞配置") && navHtml.includes("显示源胶囊参考"));
toggle.click();
assert.equal(state.movementSourceCapsuleVisible, true);
assert.equal(state.selectedPath, "geometry/body/capsule");
assert.equal(updates, 1);
assert(navHtml.includes("隐藏注册盒体"));
boundsToggle.click();
assert.equal(state.movementRegisteredBoundsVisible, false);
assert.equal(state.movementSourceCapsuleVisible, true, "The two reference layers are independent.");
assert(navHtml.includes("显示注册盒体"));
boundsToggle.click();
assert.equal(state.movementRegisteredBoundsVisible, true);
assert.equal(updates, 3);
assert.equal(JSON.stringify(body), snapshot);
console.log(`Movement collision: ${fixture.cases.length} recorded Unity dimension fixtures matched; capsule proxy/world AABB/source distinction, branch descriptions, alignment undo and independent view toggles passed. Sweep behavior was not tested.`);
