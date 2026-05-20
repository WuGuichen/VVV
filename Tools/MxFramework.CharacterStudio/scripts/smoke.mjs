import fs from "node:fs";
import path from "node:path";

const repoRoot = path.resolve(new URL("../../../", import.meta.url).pathname);
const sampleRoot = path.join(repoRoot, "Tools/MxFramework.Authoring/samples/character-iron-vanguard");
const required = [
  "manifest.json",
  "resource_catalog.json",
  "config/character_application.json",
  "geometry/body_geometry.json",
  "geometry/body_parts.json",
  "geometry/body_colliders.json",
  "geometry/sockets.json",
  "geometry/weapon_attachments.json",
  "geometry/traces.json",
  "validation/last_report.json"
];

for (const relative of required) {
  const full = path.join(sampleRoot, relative);
  if (!fs.existsSync(full)) {
    console.error(`missing ${relative}`);
    process.exit(1);
  }
}

const manifest = readJson("manifest.json");
const resources = readJson("resource_catalog.json");
const colliders = readJson("geometry/body_colliders.json");
const sockets = readJson("geometry/sockets.json");
const attachments = readJson("geometry/weapon_attachments.json");
const traces = readJson("geometry/traces.json");

assert(manifest.packageId === "iron_vanguard", "manifest packageId should be iron_vanguard");
assert(Array.isArray(resources.entries) && resources.entries.length > 0, "resource catalog should have entries");
assert(Array.isArray(colliders.colliders) && colliders.colliders.length > 0, "colliders should exist");
assert(Array.isArray(sockets.sockets) && sockets.sockets.some(s => s.socketId === "mainHand"), "mainHand socket should exist");
assert(Array.isArray(attachments.attachments) && attachments.attachments.some(a => a.equipSlot === "mainHand"), "mainHand attachment should exist");
assert(Array.isArray(traces.traces) && traces.traces.some(t => t.traceId === "trace.iron_sword.blade"), "sword trace should exist");

console.log("CharacterStudio smoke ok");

function readJson(relative) {
  return JSON.parse(fs.readFileSync(path.join(sampleRoot, relative), "utf8"));
}

function assert(condition, message) {
  if (!condition) {
    console.error(message);
    process.exit(1);
  }
}
