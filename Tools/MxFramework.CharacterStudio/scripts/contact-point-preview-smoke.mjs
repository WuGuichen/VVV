import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import * as THREE from "three";
import "../web/src/viewport3d.js";

globalThis.UNITY_GLTF_PREVIEW_POSE_ADAPTER = "unityGltfPreviewMirrorX";
globalThis.viewportLivePoseBindings = [];
globalThis.viewportLiveContactPointBindings = [];
const inspector = fs.readFileSync(new URL("../web/src/inspector.js", import.meta.url), "utf8");
const start = inspector.indexOf("function defaultSocketPoseParent(");
vm.runInThisContext(inspector.slice(start, inspector.indexOf("\nfunction ", start + 1)));

const packageRoot = new URL("../../../Authoring/CharacterPackages/character-000002/", import.meta.url);
const read = file => JSON.parse(fs.readFileSync(new URL(file, packageRoot), "utf8"));
const sockets = read("geometry/sockets.json").sockets;
const points = read("geometry/body_part_contact_points.json").contactPoints;
const bakeCatalog = JSON.parse(fs.readFileSync(new URL(
  "../../../Assets/MxFrameworkGenerated/CharacterPackages/000002/generated/weapon_trace_bake_catalog.json", import.meta.url), "utf8"));
const close = (actual, expected, message) => assert(actual.distanceTo(expected) < 1e-6, message);

for (const mode of ["none", "source_idle", "breath", "walk_cycle", "weapon_sway"]) {
  globalThis.state = { selectedPath: "", previewMotion: mode };
  const content = new THREE.Group();
  const records = points.map(point => {
    const socket = sockets.find(item => item.socketId === point.motionSocketId);
    const bone = new THREE.Bone();
    bone.name = socket.bonePath;
    bone.position.set(point.contactPointId, 0.5, 0.3);
    content.add(bone);
    return { bone, name: bone.name, path: bone.name, alias: bone.name };
  });
  const targets = [];
  addContactPointMeshes(THREE, content, [], structuredClone(points), sockets, records,
    Object.fromEntries(sockets.map(socket => [socket.socketId, socket])), null,
    target => targets.push(target), UNITY_GLTF_PREVIEW_POSE_ADAPTER, bakeCatalog, []);
  assert.equal(targets.length, 2);
  const positions = () => targets.map(target => target.object.getWorldPosition(new THREE.Vector3()));
  updateBoneParentGroups(THREE, content);
  const initial = positions();
  for (let frame = 0; frame < 120; frame++) updateBoneParentGroups(THREE, content);
  positions().forEach((position, i) => close(position, initial[i], `${mode}: stationary feet must not drift`));

  records[0].bone.position.x += 0.4;
  updateBoneParentGroups(THREE, content);
  close(positions()[0], initial[0].clone().add(new THREE.Vector3(0.4, 0, 0)), `${mode}: left probe follows left foot`);
  close(positions()[1], initial[1], `${mode}: right probe stays with right foot`);

  const pivot = records[0].bone.getWorldPosition(new THREE.Vector3());
  const beforeRotation = positions()[0];
  records[0].bone.quaternion.setFromAxisAngle(new THREE.Vector3(0, 1, 0), Math.PI / 2);
  updateBoneParentGroups(THREE, content);
  close(positions()[0], beforeRotation.sub(pivot).applyQuaternion(records[0].bone.quaternion).add(pivot),
    `${mode}: socket and contact offsets rotate with the foot`);
}
console.log("Contact point preview: Witch left/right probes follow displayed bones in all five modes; stationary, translation and rotation checks passed.");
