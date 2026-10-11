// Usage: node scripts/preview-glb-authoring-only-smoke.mjs [<authoring-package-dir>]
//
// 专用预览 GLB 的 source-only 制作侧回归。
//
// 与其它 smoke 的差别：本用例不检查源码字符串，而是把真实的 web/src/resourcePicker.js
// 在桩全局下求值，直接调用其生产 helper（isAuthoringPreviewGlb / inferRuntimeBindingKind /
// inferRuntimeAvailability / formatResourceAvailabilityLabel），再用真实 three.js GLTFLoader
// 读取作者源 GLB，证明"源可读、不期望生成副本、不具有运行加载资格"三件事。
import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";
import assert from "node:assert/strict";
import { GLTFLoader } from "../node_modules/three/examples/jsm/loaders/GLTFLoader.js";

const repoRoot = path.resolve(import.meta.dirname, "..", "..", "..");
const packageDir = path.resolve(
  process.argv[2] || path.join(repoRoot, "Authoring", "CharacterPackages", "character-000008")
);
const catalogPath = path.join(packageDir, "resource_catalog.json");
assert(fs.existsSync(catalogPath), `resource catalog not found: ${catalogPath}`);

// ---- 1) 在桩全局下求值真实的前端脚本，取得生产 helper ----
const pickerSource = fs.readFileSync(
  path.join(import.meta.dirname, "..", "web", "src", "resourcePicker.js"),
  "utf8"
);
const stubElement = { style: {}, addEventListener() {}, removeEventListener() {}, setAttribute() {}, appendChild() {} };
const sandbox = {
  console,
  document: {
    addEventListener() {},
    querySelector() { return null; },
    createElement() { return stubElement; },
    createElementNS() { return stubElement; },
    body: { appendChild() {}, classList: { add() {}, remove() {} } }
  },
  window: { addEventListener() {}, location: { href: "" } }
};
sandbox.globalThis = sandbox;
sandbox.self = sandbox;
vm.createContext(sandbox);
vm.runInContext(pickerSource, sandbox, { filename: "resourcePicker.js" });

const { isAuthoringPreviewGlb, inferRuntimeBindingKind, inferRuntimeAvailability, formatResourceAvailabilityLabel } = sandbox;
for (const [name, value] of Object.entries({
  isAuthoringPreviewGlb, inferRuntimeBindingKind, inferRuntimeAvailability, formatResourceAvailabilityLabel
})) {
  assert.equal(typeof value, "function", `${name} must be a callable production helper`);
}

// ---- 2) 读真实作者源 catalog ----
const catalog = JSON.parse(fs.readFileSync(catalogPath, "utf8"));
const entries = catalog.entries || [];
const previewEntry = entries.find(entry => entry.usage === "previewMesh");
assert(previewEntry, "the authoring package must declare a previewMesh entry");
assert.match(previewEntry.relativePath, /\.glb$/i, "the authoring preview entry must be a GLB");

// ---- 3) 分类与运行可用性语义 ----
assert.equal(isAuthoringPreviewGlb(previewEntry), true, "previewMesh + GLB must classify as authoring-only");
const previewKind = inferRuntimeBindingKind(previewEntry, "Model");
const previewAvailability = inferRuntimeAvailability(previewEntry, previewKind, null);
assert.equal(previewKind, "AuthoringPreviewOnly", `expected AuthoringPreviewOnly, got ${previewKind}`);
assert.equal(previewAvailability, "AuthoringPreviewOnly", `expected AuthoringPreviewOnly, got ${previewAvailability}`);
assert.notEqual(previewAvailability, "RuntimeReady", "an authoring-only preview must not be reported as runtime ready");
assert.notEqual(previewAvailability, "RuntimeMissing", "a missing generated copy must not be reported as a runtime gap");
assert.match(
  formatResourceAvailabilityLabel(previewAvailability),
  /制作预览/,
  "the availability label must say the preview stays on the authoring side"
);

// 其它用途必须保持原行为。
for (const entry of entries) {
  if (entry === previewEntry) continue;
  if (entry.usage === "previewThumbnail") {
    assert.equal(isAuthoringPreviewGlb(entry), false, "previewThumbnail must not be reclassified");
    assert.equal(inferRuntimeBindingKind(entry, "Texture"), "GeneratedPreviewOnly", "previewThumbnail behaviour must not change");
  } else if (entry.usage === "characterModel" || entry.usage === "weaponModel") {
    assert.equal(isAuthoringPreviewGlb(entry), false, "a formal model must not be reclassified");
    assert.notEqual(inferRuntimeBindingKind(entry, "Model"), "AuthoringPreviewOnly", "a formal model must stay a runtime resource");
  }
}

// 边界：previewMesh 但非 GLB 不分类；大小写与前导点不敏感。
assert.equal(isAuthoringPreviewGlb({ usage: "previewMesh", relativePath: "previews/x.fbx" }), false, "previewMesh FBX must not classify");
assert.equal(isAuthoringPreviewGlb({ usage: "previewMesh", relativePath: "previews/x.GLB" }), true, "extension matching must be case-insensitive");
assert.equal(isAuthoringPreviewGlb({ usage: "previewMesh", sourceFormat: ".glb" }), true, "a leading dot must be tolerated");
assert.equal(isAuthoringPreviewGlb({ usage: "previewMesh" }), false, "an entry without path or format must not classify");

// ---- 4) 制作源可读：用真实 loader 读源 GLB ----
// GLTFLoader 面向浏览器，node 下需要补齐它引用的全局（与 fbx-preview-draw-smoke.mjs 同一做法）。
globalThis.self = {
  URL: { createObjectURL: () => "blob:preview-glb-smoke", revokeObjectURL: () => {} },
  webkitURL: { createObjectURL: () => "blob:preview-glb-smoke", revokeObjectURL: () => {} }
};
globalThis.window = { URL: globalThis.URL };
globalThis.document = {
  createElementNS() { return { style: {}, addEventListener() {}, removeEventListener() {}, setAttribute() {} }; },
  createElement() { return { style: {}, addEventListener() {}, removeEventListener() {}, setAttribute() {} }; }
};
globalThis.createImageBitmap = async () => ({ width: 1, height: 1, close() {} });

const sourceGlb = path.join(packageDir, previewEntry.relativePath);
assert(fs.existsSync(sourceGlb), `the authoring source GLB must exist: ${sourceGlb}`);
const bytes = fs.readFileSync(sourceGlb);
const gltf = await new GLTFLoader().parseAsync(
  bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength),
  ""
);
let meshes = 0;
gltf.scene.traverse(object => { if (object.isMesh) meshes++; });
assert(meshes > 0, "the authoring source GLB must contain at least one mesh");

// ---- 5) 界面三态：制作源可读 / 不生成 Unity 副本 / 无运行加载资格 ----
// 三态明细抽成纯函数（无跨模块依赖），因此可以直接调用并断言内容。
const { authoringOnlyPreviewDetailRows } = sandbox;
assert.equal(typeof authoringOnlyPreviewDetailRows, "function", "authoringOnlyPreviewDetailRows must be callable");
const detailRows = authoringOnlyPreviewDetailRows();
assert.equal(detailRows.length, 3, "the authoring-only detail must state exactly three things");
const detailText = detailRows.join("");
for (const expected of ["制作源", "可读取", "Unity 副本", "不生成", "运行加载", "不具备资格"]) {
  assert.ok(detailText.includes(expected), `the authoring-only detail must state: ${expected}`);
}
assert.ok(
  !/Runtime 就绪/.test(detailText),
  "an authoring-only preview must not claim runtime readiness"
);

// ---- 6) 报告生成侧现状（本用例不修改生成包） ----
const generatedRoot = path.join(
  repoRoot, "Assets", "MxFrameworkGenerated", "CharacterPackages",
  path.basename(packageDir).replace(/^character-/, "")
);
const generatedCopy = path.join(generatedRoot, previewEntry.relativePath);

console.log(JSON.stringify({
  status: "PASS",
  package: path.relative(repoRoot, packageDir).replace(/\\/g, "/"),
  previewResourceKey: previewEntry.resourceKey,
  previewRelativePath: previewEntry.relativePath,
  bindingKind: previewKind,
  runtimeAvailability: previewAvailability,
  availabilityLabel: formatResourceAvailabilityLabel(previewAvailability),
  sourceGlbBytes: bytes.length,
  sourceMeshes: meshes,
  generatedCopyPresent: fs.existsSync(generatedCopy),
  note: "generatedCopyPresent 只作观察报告，本用例不修改生成包"
}, null, 2));
