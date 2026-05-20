import fs from "node:fs";
import path from "node:path";

const repoRoot = path.resolve(new URL("../../../", import.meta.url).pathname);
const sampleRoot = path.join(repoRoot, "Tools/MxFramework.Authoring/samples/character-iron-vanguard");
const unityGeneratedRoot = path.join(repoRoot, "Assets/MxFrameworkGenerated/CharacterPackages/iron_vanguard");
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
const requiredGenerated = [
  "config/unity_resource_catalog.json",
  "package_cache/import_report.json"
];

for (const relative of required) {
  const full = path.join(sampleRoot, relative);
  if (!fs.existsSync(full)) {
    console.error(`missing ${relative}`);
    process.exit(1);
  }
}
for (const relative of requiredGenerated) {
  const full = path.join(unityGeneratedRoot, relative);
  if (!fs.existsSync(full)) {
    console.error(`missing generated ${relative}`);
    process.exit(1);
  }
}

const manifest = readJson("manifest.json");
const resources = readJson("resource_catalog.json");
const colliders = readJson("geometry/body_colliders.json");
const sockets = readJson("geometry/sockets.json");
const attachments = readJson("geometry/weapon_attachments.json");
const traces = readJson("geometry/traces.json");
const unityCatalog = readGeneratedJson("config/unity_resource_catalog.json");
const importReport = readGeneratedJson("package_cache/import_report.json");
const appSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/app.js"), "utf8");
const indexSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/index.html"), "utf8");
const stylesSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/styles.css"), "utf8");

assert(manifest.packageId === "iron_vanguard", "manifest packageId should be iron_vanguard");
assert(Array.isArray(resources.entries) && resources.entries.length > 0, "resource catalog should have entries");
assert(Array.isArray(colliders.colliders) && colliders.colliders.length > 0, "colliders should exist");
assert(Array.isArray(sockets.sockets) && sockets.sockets.some(s => s.socketId === "mainHand"), "mainHand socket should exist");
assert(Array.isArray(attachments.attachments) && attachments.attachments.some(a => a.equipSlot === "mainHand"), "mainHand attachment should exist");
assert(Array.isArray(traces.traces) && traces.traces.some(t => t.traceId === "trace.iron_sword.blade"), "sword trace should exist");
assert(unityCatalog.packageId === manifest.packageId, "Unity resource catalog packageId should match manifest");
assert(Array.isArray(unityCatalog.entries) && unityCatalog.entries.length > 0, "Unity resource catalog should have entries");
assert(importReport.packageId === manifest.packageId, "Unity import report packageId should match manifest");
assert(Array.isArray(importReport.operations) && importReport.operations.some(op => op.kind === "unityResourceCatalog"), "Unity import report should include unityResourceCatalog operation");
assert(appSource.includes("RESOURCE_FIELD_SPECS"), "CharacterStudio should expose ResourceFieldSpec-driven selection");
assert(appSource.includes("resourcePickerOpen") && appSource.includes("openResourcePicker"), "CharacterStudio should open resources through a field-scoped picker");
assert(appSource.includes("createResourceSelectionRef") && appSource.includes("resourceStableId") && appSource.includes("runtimeResourceKey"), "CharacterStudio should create the new ResourceSelectionRef shape");
assert(appSource.includes("sourceProviderId") && appSource.includes("providerResourceKey") && appSource.includes("packageResourceKey"), "CharacterStudio selection should keep provider-local identity separate from runtime keys");
assert(appSource.includes("collectResourceReferences") && appSource.includes("previewResourceKey") && appSource.includes("dependencies") && appSource.includes("preview.thumbnailResourceKey"), "CharacterStudio reference collection should cover character, weapon, dependency, and preview references");
assert(indexSource.includes("resourcePickerOverlay"), "CharacterStudio should render the resource picker as an on-demand dialog");
assert(!indexSource.includes("modelResourceList"), "CharacterStudio should not keep a full resource library list in the main viewport");
assert(appSource.includes("SpawnCritical") && appSource.includes("EquipmentInitial") && appSource.includes("AnimationWarmup"), "CharacterStudio should render resource plan groups");
assert(stylesSource.includes("resource-plan-grid"), "CharacterStudio should style the resource plan preview");

const modelResources = resources.entries.filter(entry => entry.typeId === "model" && entry.resourceKey);
for (const resource of modelResources) {
  const unityEntry = unityCatalog.entries.find(entry =>
    entry.id === resource.resourceKey
    || entry.providerData?.packageResourceKey === resource.resourceKey
    || entry.providerData?.stableId === resource.stableId);
  assert(unityEntry, `Unity catalog should include ${resource.resourceKey}`);
  assert(unityEntry.unityAssetPath || unityEntry.providerData?.unityAssetPath, `${resource.resourceKey} should expose Unity asset path`);
  assert(unityEntry.importerKind || unityEntry.providerData?.importerKind, `${resource.resourceKey} should expose importer kind`);
  assert(unityEntry.importStatus || unityEntry.providerData?.importStatus, `${resource.resourceKey} should expose import status`);
}

const fallbackPlan = {
  spawnCritical: resources.entries.filter(entry => entry.usage === "characterModel").map(entry => entry.resourceKey),
  equipmentInitial: attachments.attachments.map(attachment => attachment.previewResourceKey).filter(Boolean),
  animationWarmup: resources.entries.filter(entry => entry.typeId === "animation").map(entry => entry.resourceKey),
  uiDeferred: resources.entries.filter(entry => entry.usage === "previewThumbnail").map(entry => entry.resourceKey)
};
assert(fallbackPlan.spawnCritical.length > 0, "fallback resource plan should include SpawnCritical resources");
assert(fallbackPlan.equipmentInitial.length > 0, "fallback resource plan should include EquipmentInitial resources");
assert(fallbackPlan.animationWarmup.length > 0, "fallback resource plan should include AnimationWarmup resources");
assert(fallbackPlan.uiDeferred.length > 0, "fallback resource plan should include UiDeferred resources");

console.log("CharacterStudio smoke ok");

function readJson(relative) {
  return JSON.parse(fs.readFileSync(path.join(sampleRoot, relative), "utf8"));
}

function readGeneratedJson(relative) {
  return JSON.parse(fs.readFileSync(path.join(unityGeneratedRoot, relative), "utf8"));
}

function assert(condition, message) {
  if (!condition) {
    console.error(message);
    process.exit(1);
  }
}
