// Usage: node scripts/fbx-preview-draw-smoke.mjs <model.fbx>
import fs from "node:fs";
import assert from "node:assert/strict";
import { FBXLoader } from "../node_modules/three/examples/jsm/loaders/FBXLoader.js";
import * as THREE from "../node_modules/three/build/three.module.js";

globalThis.document = { createElementNS() { return { style: {}, addEventListener() {}, removeEventListener() {}, setAttribute() {} }; } };
globalThis.window = { URL: globalThis.URL };
assert(process.argv[2], "Pass the FBX file to verify.");
const bytes = fs.readFileSync(process.argv[2]);
const root = new FBXLoader().parse(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), "");
let meshes = 0;
let drawnVertices = 0;
root.traverse(object => {
  if (!object.isMesh) return;
  meshes++;
  const geometry = object.geometry;
  const count = geometry.index?.count ?? geometry.attributes.position.count;
  if (Array.isArray(object.material)) {
    assert(geometry.groups.length > 0, `${object.name}: material array has no draw groups`);
    let covered = 0;
    for (const group of geometry.groups) {
      assert(object.material[group.materialIndex], `${object.name}: invalid material slot ${group.materialIndex}`);
      assert.equal(group.start, covered, "draw groups must cover vertices continuously");
      covered += group.count;
    }
    assert.equal(covered, count, `${object.name}: draw coverage differs from geometry`);
  }
  drawnVertices += count;
});
assert(meshes > 0 && drawnVertices > 0);
root.updateMatrixWorld(true);
const size = new THREE.Box3().setFromObject(root).getSize(new THREE.Vector3());
assert([size.x, size.y, size.z].every(Number.isFinite));
console.log(JSON.stringify({ status: "PASS", meshes, drawnVertices, size: size.toArray() }));
