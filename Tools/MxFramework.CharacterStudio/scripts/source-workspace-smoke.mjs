import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import "../web/src/utils.js";
import "../web/src/importMaterialsPanel.js";
import "../web/src/importMaterialsOperations.js";

const copy = sourceCopy;
globalThis.IMPORT_MATERIAL_TEMPLATES = [];
globalThis.IMPORT_MATERIAL_PRESETS = [];
globalThis.getResourceSelectionKey = ref => ref?.resourceKey || "";
globalThis.refreshBodyPartDamageDirtyFromCleanSnapshot = () => {};
globalThis.refreshDirtyFromCleanSnapshot = () => { state.dirty = !sourceValueEqual(state.package, JSON.parse(state.cleanPackageJson)); };
globalThis.normalizeResourceLibraryPayload = value => value;
globalThis.normalizeResourcePlanPayload = value => value;
for (const name of ["renderImportMaterialsPanel", "renderShellStatus", "renderContextDockState", "render", "persistCharacterStudioSessionState", "restoreCharacterStudioDomState"]) globalThis[name] = () => {};
globalThis.captureCharacterStudioUiSnapshot = () => ({ selectedPath: state.selectedPath });
globalThis.loadPackageState = () => { throw new Error("Source operations must not reload/reset the editor package"); };
const item = path => ({ id: "body-model", label: "Body model", presetId: "characterModel", required: true,
  sourcePath: path, source: { kind: "file", scope: "package", path }, bindTarget: "body", target: { bindTarget: "body", resourceKey: "model" } });
const doc = path => ({ schemaVersion: "1.0", sourceRoot: "ArtSource/Characters/fixture", items: [item(path)] });
const payload = (document, status = "Clean") => ({ sourceManifestExists: true, sourceManifestRelative: "ArtSource/Characters/fixture/character_sources.json",
  document: copy(document), items: document.items.map(item => ({ id: item.id, item: copy(item), status, sourceExists: status !== "Missing" })),
  integrityReceipt: { status: "NotApplicable", blockingIssueCount: 0, diagnostics: [] } });
const base = { manifest: { packageId: "fixture", configFiles: ["body.json"] }, baseAttributes: { hp: 100 },
  geometry: { bodyProfile: { modelRootStableId: "old-model" }, colliders: [{ colliderId: "head", radius: 0.15 }] },
  resourceCatalog: { entries: [{ resourceKey: "model", hash: "old" }] } };
const remote = copy(base);
remote.geometry.bodyProfile.modelRootStableId = "new-model";
remote.resourceCatalog.entries[0].hash = "new";
remote.resourceCatalog.entries.push({ resourceKey: "new-texture", hash: "texture-hash" });
remote.manifest.configFiles.push("new-material.json");
function setup() {
  globalThis.state = { packageRelative: "fixture", package: copy(base), packageRevision: "r1", cleanPackageJson: JSON.stringify(base),
    canWrite: true, apiAvailable: true, dirty: false, importMaterialsDirty: false,
    importMaterials: normalizeImportMaterialsPayload(payload(doc("old.fbx"))), importMaterialsSelectedIds: new Set(["body-model"]),
    importMaterialsOperation: null, sourceSyncConflictPaths: [], undoStack: [], redoStack: [], activeUndoTransaction: null,
    selectedPath: "geometry/colliders/head", viewportCameraState: { zoom: 7 }, activeContextTab: "materials",
    weaponLibrary: { draft: "unsaved weapon" }, weaponLibraryDirty: true,
    bodyPartDamage: { document: { draft: "unsaved part damage" }, packageRevision: "r1" }, bodyPartDamageDirty: true };
}
const response = (value, status = 200) => ({ ok: status >= 200 && status < 300, status, json: async () => copy(value) });
const tick = () => new Promise(resolve => setTimeout(resolve, 0));

setup();
state.importMaterials.document.items[0] = item("new.fbx");
state.importMaterialsDirty = true;
state.package.baseAttributes.hp = 240;
state.dirty = true;
globalThis.fetch = async () => response(payload(doc("old.fbx"), "Missing"));
await checkCharacterSourceSelection();
assert.equal(state.importMaterials.document.items[0].sourcePath, "new.fbx");
assert(state.importMaterialsDirty);
assert.equal(state.package.baseAttributes.hp, 240);
assert.equal(state.importMaterials.items[0].status, "NeedsCheck");
assert.equal(getImportMaterialSlotDisplayStatus(state.importMaterials.items[0], { required: true }).label, "已选择，待检查");
setup();
await checkCharacterSourceSelection();
assert.equal(getImportMaterialSlotDisplayStatus(state.importMaterials.items[0], { required: true }).label, "文件缺失");
assert.equal(getImportMaterialStatusTone("Missing"), "error");
assert.equal(getImportMaterialSlotDisplayStatus({ item: item("bad.png"), status: "OptionalMissing" }, { required: false }).label, "可选文件缺失");

setup();
const beforeFailure = copy(state.importMaterials);
globalThis.fetch = async () => { throw new Error("offline"); };
assert.equal(await checkCharacterSourceSelection(), false);
assert.deepEqual(state.importMaterials, beforeFailure);
assert.equal(state.importMaterialsOperation, null);

// Responses for a previous package cannot replace the newly selected package or its source document.
setup();
let release;
globalThis.fetch = () => new Promise(resolve => { release = resolve; });
const pendingCheck = checkCharacterSourceSelection();
await tick();
state.packageRelative = "other";
state.package = { marker: "other draft" };
state.importMaterials = { document: doc("other.fbx") };
release(response(payload(doc("old.fbx"))));
await pendingCheck;
assert.equal(state.importMaterials.document.items[0].sourcePath, "other.fbx");
assert.equal(state.package.marker, "other draft");

// Saving acknowledges the submitted document while preserving any newer source edits.
setup();
state.importMaterialsDirty = true;
let savedRequest;
globalThis.fetch = (url, init) => {
  savedRequest = JSON.parse(init.body);
  return new Promise(resolve => { release = resolve; });
};
const pendingSave = runImportMaterialsOperation("save", (op, current) => saveCharacterSourceSelection(op, current));
await tick();
state.importMaterials.document.items[0] = item("edited-during-save.fbx");
release(response(payload(savedRequest.document, "NotImported")));
await pendingSave;
assert(state.importMaterialsDirty);
assert.equal(state.importMaterials.document.items[0].sourcePath, "edited-during-save.fbx");

// Distinct server changes are merged into the latest draft AND every undo/redo snapshot.
setup();
const attributes = state.package.baseAttributes;
state.package.baseAttributes.hp = 240;
state.package.geometry.colliders.push({ colliderId: "local-hurtbox", radius: 0.2 });
state.dirty = true;
state.undoStack = [{ label: "HP edit", beforePackage: JSON.stringify(base), afterPackage: JSON.stringify(state.package), beforeWeaponLibrary: "keep",
  beforeBodyPartDamage: JSON.stringify(state.bodyPartDamage), afterBodyPartDamage: JSON.stringify(state.bodyPartDamage) }];
state.redoStack = copy(state.undoStack);
state.activeUndoTransaction = copy(state.undoStack[0]);
const weaponDraft = state.weaponLibrary, partDraft = state.bodyPartDamage;
let stateReads = 0;
globalThis.fetch = async (url) => {
  if (url.startsWith("/api/character/state?")) return response({ package: ++stateReads === 1 ? base : remote, revision: stateReads === 1 ? "r1" : "r2" });
  if (url.startsWith("/api/character/body-part-damage?")) return response({ state: "Current", packageRevision: stateReads === 1 ? "r1" : "r2", document: { saved: true } });
  if (url.endsWith("/sync")) {
    state.package.baseAttributes.hp = 250; // An edit arriving while the sync request is in flight.
    return response({ success: true, state: payload(doc("old.fbx")) });
  }
  return response({ ready: true });
};
assert.equal(await syncCharacterSourceSelection({ allChanged: true }), true);
assert.equal(state.package.baseAttributes, attributes);
assert.equal(state.package.baseAttributes.hp, 250);
assert.equal(state.package.geometry.colliders.length, 2);
assert.equal(state.package.geometry.bodyProfile.modelRootStableId, "new-model");
assert(state.package.resourceCatalog.entries.some(entry => entry.resourceKey === "new-texture"));
assert.equal(state.weaponLibrary, weaponDraft);
assert.equal(state.bodyPartDamage, partDraft);
assert(state.weaponLibraryDirty && state.bodyPartDamageDirty && state.dirty);
assert.equal(state.packageRevision, "r2");
assert.equal(state.bodyPartDamage.packageRevision, "r2");
assert.equal(state.undoStack.length, 1);
assert.equal(state.redoStack.length, 1);
for (const entry of [state.undoStack[0], state.redoStack[0], state.activeUndoTransaction]) {
  for (const side of ["beforePackage", "afterPackage"]) assert(JSON.parse(entry[side]).resourceCatalog.entries.some(item => item.resourceKey === "new-texture"));
  assert.equal(entry.beforeWeaponLibrary, "keep");
  assert.equal(JSON.parse(entry.beforeBodyPartDamage).packageRevision, "r2");
  assert.equal(JSON.parse(entry.afterBodyPartDamage).packageRevision, "r2");
}
assert.equal(JSON.parse(state.cleanPackageJson).baseAttributes.hp, 100);
assert.deepEqual(state.viewportCameraState, { zoom: 7 });
assert.equal(state.selectedPath, "geometry/colliders/head");

// Conflicting values remain local, while the old revision protects a later save.
setup();
state.package.geometry.bodyProfile.modelRootStableId = "local-model";
state.dirty = true;
stateReads = 0;
await syncCharacterSourceSelection();
assert.equal(state.package.geometry.bodyProfile.modelRootStableId, "local-model");
assert.equal(state.packageRevision, "r1");
assert(state.sourceSyncConflictPaths.some(path => path.includes("modelRootStableId")));
assert(state.package.resourceCatalog.entries.some(item => item.resourceKey === "new-texture"));

// Independent document changes must not be hidden by blindly advancing their version witness.
setup();
const independentDraft = copy(state.bodyPartDamage);
const independentConflicts = applySourceSyncPackageChanges({ package: base, revision: "r1" }, { package: remote, revision: "r2" }, "r1", {
  bodyPartDamage: { before: { state: "Current", packageRevision: "r1", document: { wound: 10 } },
    after: { state: "Current", packageRevision: "r2", document: { wound: 20 } } }
});
assert.deepEqual(state.bodyPartDamage, independentDraft);
assert(independentConflicts.some(path => path.includes("部位伤害")));

// A 409 can carry partial writes and meaningful SDK diagnostics; do not discard that response.
setup();
stateReads = 0;
globalThis.fetch = async url => {
  if (url.startsWith("/api/character/state?")) return response({ package: ++stateReads === 1 ? base : remote, revision: stateReads === 1 ? "r1" : "r2" });
  if (url.startsWith("/api/character/body-part-damage?")) return response({ state: "Current", packageRevision: stateReads === 1 ? "r1" : "r2", document: { saved: true } });
  if (url.endsWith("/sync")) { const p = payload(doc("old.fbx")); p.integrityReceipt = { status: "Blocked", blockingIssueCount: 1, diagnostics: [{ severity: "Error", message: "缺少材质绑定" }] }; return response({ success: false, state: p }, 409); }
  return response({ ready: true });
};
assert.equal(await syncCharacterSourceSelection(), false);
assert.equal(state.importMaterials.integrityReceipt.status, "Blocked");
assert(state.message.includes("缺少材质绑定"));
assert.equal(state.package.resourceCatalog.entries[0].hash, "new");

// Failure to read the post-sync package must leave both the editor package and history intact.
setup();
state.package.baseAttributes.hp = 333;
state.dirty = true;
state.undoStack = [{ beforePackage: JSON.stringify(base), afterPackage: JSON.stringify(state.package) }];
const failedPackage = JSON.stringify(state.package), failedHistory = JSON.stringify(state.undoStack);
stateReads = 0;
globalThis.fetch = async url => {
  if (url.startsWith("/api/character/state?")) return ++stateReads === 1 ? response({ package: base, revision: "r1" }) : response({ message: "unavailable" }, 503);
  return response({ success: true, state: payload(doc("old.fbx")) });
};
assert.equal(await syncCharacterSourceSelection(), false);
assert.equal(JSON.stringify(state.package), failedPackage);
assert.equal(JSON.stringify(state.undoStack), failedHistory);
assert.equal(state.packageRevision, "r1");

// Do not synchronize an older submission if the source choice changes during the automatic save.
setup();
state.importMaterialsDirty = true;
let syncPosts = 0;
globalThis.fetch = async (url, init) => {
  if (url.startsWith("/api/character/state?")) return response({ package: base, revision: "r1" });
  if (url.endsWith("/save")) {
    const submitted = JSON.parse(init.body).document;
    state.importMaterials.document.items[0] = item("newer-than-submission.fbx");
    return response(payload(submitted));
  }
  if (url.endsWith("/sync")) syncPosts++;
  return response({ success: true });
};
assert.equal(await syncCharacterSourceSelection(), false);
assert.equal(syncPosts, 0);
assert(state.importMaterialsDirty);
assert.equal(state.importMaterials.document.items[0].sourcePath, "newer-than-submission.fbx");

// Array additions/deletions on different records merge without replacing the user's record.
const b = [{ resourceKey: "a", hash: 1 }, { resourceKey: "b", hash: 1 }];
const l = [{ resourceKey: "a", hash: 2 }, { resourceKey: "b", hash: 1 }, { resourceKey: "local", hash: 7 }];
const r = [{ resourceKey: "a", hash: 1 }, { resourceKey: "remote", hash: 8 }];
const conflicts = [];
const merged = mergeSourceSyncDelta(b, l, r, conflicts, "entries");
assert.equal(merged.find(item => item.resourceKey === "a").hash, 2);
assert(!merged.some(item => item.resourceKey === "b"));
assert(merged.some(item => item.resourceKey === "local") && merged.some(item => item.resourceKey === "remote"));
assert.equal(conflicts.length, 0);

const textureDoc = doc("body.png");
Object.assign(textureDoc.items[0], { presetId: "texture", textureSemantic: "Color", materialSurfaceMode: "AlphaClip", alphaCutoff: 0.2,
  target: { slot: "body", textureSemantic: "Color", materialSurfaceMode: "AlphaClip", alphaCutoff: 0.2, resourceUsage: "Surface" } });
const prepared = prepareImportMaterialsDocumentForSave(textureDoc).items[0];
assert.equal(prepared.target.materialSurfaceMode, "AlphaClip");
assert.equal(prepared.target.alphaCutoff, 0.2);
assert.equal(prepared.target.textureSemantic, "Color");
assert.equal(prepared.target.resourceUsage, "Surface");

// Check the real panel's ordering and terminology without claiming a browser visual test.
const code = fs.readFileSync(new URL("../web/src/importMaterialsPanel.js", import.meta.url), "utf8");
for (const name of ["renderImportMaterialsPanel", "renderImportMaterialSlotCard", "isImportMaterialResourceRefItem", "hasImportMaterialSourceConfigured", "formatSourceScopeLabel"]) {
  const start = code.indexOf(`function ${name}(`), end = code.indexOf("\nfunction ", start + 1);
  vm.runInThisContext(code.slice(start, end));
}
setup();
globalThis.IMPORT_MATERIAL_SLOT_DEFINITIONS = [{ key: "body", id: "body-model", label: "角色模型", presetId: "characterModel", bindTarget: "body", required: true }];
globalThis.el = { importMaterialsPanel: { innerHTML: "" } };
for (const name of ["renderCharacterAuthoringSourcesSummary", "renderCommonResourcesStatusBar", "renderDefaultWeaponReferenceCards", "renderImportMaterialsExtraResources",
  "renderImportMaterialsBatchTools", "renderImportMaterialsManifestDetails", "renderImportMaterialsAdvancedList", "renderImportMaterialSourcePicker", "renderBodyFbxTextureDiagnostic", "renderImportMaterialSourceThumbnail"]) globalThis[name] = () => "";
globalThis.renderImportMaterialsIntegrityReceipt = () => '<div id="receipt-evidence">Receipt</div>';
globalThis.renderImportMaterialSlotCards = cards => cards.map(card => renderImportMaterialSlotCard(card, true)).join("");
renderImportMaterialsPanel();
const html = el.importMaterialsPanel.innerHTML;
assert(html.indexOf('data-import-material-slot="body"') < html.indexOf('id="receipt-evidence"'));
assert(html.includes("保存来源配置") && html.includes("检查来源") && html.includes("同步到角色资源包"));
assert(html.includes("Unity 导入仍是后续独立步骤"));
assert(html.includes("角色模型") && html.includes("公共资源库（按需管理）"));
console.log("Source workspace: draft checks, delayed saves, role switching, sync merges, undo/redo preservation, conflicts, partial 409 evidence, source metadata and card order passed.");
