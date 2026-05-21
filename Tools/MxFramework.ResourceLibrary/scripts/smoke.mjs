import fs from "node:fs";
import path from "node:path";

const repoRoot = path.resolve(new URL("../../../", import.meta.url).pathname);
const toolRoot = path.join(repoRoot, "Tools/MxFramework.ResourceLibrary");
const required = [
  "web/index.html",
  "web/app.js",
  "web/styles.css",
  "scripts/smoke.mjs"
];

for (const relative of required) {
  assert(fs.existsSync(path.join(toolRoot, relative)), `missing ${relative}`);
}

const index = fs.readFileSync(path.join(toolRoot, "web/index.html"), "utf8");
const app = fs.readFileSync(path.join(toolRoot, "web/app.js"), "utf8");
const styles = fs.readFileSync(path.join(toolRoot, "web/styles.css"), "utf8");
const launcher = fs.readFileSync(path.join(toolRoot, "start-resource-library.sh"), "utf8");
const windowsLauncher = fs.readFileSync(path.join(toolRoot, "start-resource-library.bat"), "utf8");
const readme = fs.readFileSync(path.join(toolRoot, "README.md"), "utf8");

assert(index.includes("MxFramework 资源管理器"), "index should expose Chinese title");
assert(index.includes("resourceList") && index.includes("inspectorContent"), "index should render browser and inspector anchors");
assert(index.includes("Overview") && index.includes("Unity") && index.includes("Runtime") && index.includes("References") && index.includes("Diagnostics"), "index should expose required inspector tabs");
assert(index.includes("resourceImportFileInput") && index.includes("resourceImportFolderInput") && index.includes("resourceReplaceFileInput"), "write actions should provide hidden file inputs");
assert(index.includes("importPresetSelect") && index.includes("导入类型"), "write actions should expose typed import presets");
assert(index.includes("importResourceButton") && index.includes("importFolderButton") && index.includes("reimportResourceButton") && index.includes("replaceSourceButton"), "write actions should expose import/folder/reimport/replace buttons");
assert(index.includes("deleteResourceButton") && index.includes("editTagsButton") && index.includes("等待 reference graph delete guard"), "delete/tag actions should remain guarded");
assert(index.includes("复制详情 JSON") && index.includes("复制诊断 JSON"), "copy JSON actions should be visible");

assert(app.includes("/api/character/packages"), "app should call character packages API");
assert(app.includes("/api/authoring/resources?package="), "app should call authoring resource list API");
assert(app.includes("/api/authoring/resources/resource-plan?package="), "app should call authoring resource plan API");
assert(app.includes("/api/authoring/resources/inspect?package="), "app should call authoring inspect API defensively");
assert(app.includes("/api/authoring/resources/stage-import"), "app should call external import staging API before folder promotion");
assert(app.includes("/api/authoring/resources/import") && app.includes("/api/authoring/resources/reimport") && app.includes("/api/authoring/resources/replace-source"), "app should call authoring resource write API gates");
assert(!app.includes("/api/character/resources/import") && !app.includes("/api/character/resources/reimport") && !app.includes("/api/character/resources/replace-source"), "new Resource Manager writes should not use character-prefixed write APIs");
assert(app.includes("resourceId") && app.includes("sourceProviderId") && app.includes("providerBindings"), "app should understand authoring resource identity and provider bindings");
assert(app.includes("providerFilter") && app.includes("getProviders") && app.includes("provider 状态"), "app should expose provider filter and provider status");
assert(app.includes("fmodEventPath") && app.includes("audioCueId") && app.includes("audioEventDefinitionId"), "app should preserve FMOD audio provider metadata");
assert(app.includes("targetResourceId") && app.includes("targetProviderResourceKey") && app.includes("targetRuntimeResourceKey"), "app should match cross-consumer reference graph targets");
assert(app.includes("postJson") && app.includes("readFileAsBase64"), "app should post write requests with uploaded file bytes");
assert(app.includes("IMPORT_PRESETS") && app.includes("animationClipGroup") && app.includes("audioCue"), "app should define typed animation and audio import presets");
assert(app.includes('id: "animationClipGroup"') && app.includes('extensions: ["anim", "glb", "gltf", "fbx", "json"]'), "animation import preset should accept Unity .anim and FBX animation sources for conversion");
assert(app.includes("importResourceFolder") && app.includes("webkitRelativePath"), "app should support folder import with stable local ids");
assert(app.includes("isIgnoredImportFile") && app.includes(".meta") && app.includes("忽略元数据"), "folder import should ignore editor metadata files separately from skipped resources");
assert(app.includes("isImportableStagedItem") && app.includes("AUTH_RES_IMPORT_IGNORED_FILE") && app.includes("externalImportStaging"), "folder import should use staging diagnostics and provider items");
assert(app.includes("isStagedItemSupportedByPreset") && app.includes("buildImportRequestFromStagedItem(file, staged, preset)") && app.includes("跳过非匹配"), "folder import should filter and promote staged files by the selected import preset");
assert(app.includes("inspectCache.clear") && app.includes("loadPackageData"), "write responses should refresh resource data");
assert(app.includes("buildFallbackInspect") && app.includes("inspect endpoint 不可用"), "app should include inspect fallback behavior");
assert(app.includes("onlyRuntimeLoadable") && app.includes("onlyDiagnostics"), "app should include client-side filters");
assert(app.includes("navigator.clipboard.writeText"), "app should copy JSON through the clipboard when available");
assert(!app.includes("React") && !app.includes("createRoot") && !app.includes("vite"), "app should remain vanilla DOM/fetch JavaScript");

assert(styles.includes(".resource-browser") && styles.includes(".inspector-tabs") && styles.includes(".action-bar") && styles.includes(".import-preset-label"), "styles should cover browser, inspector tabs, action bar, and import preset");
assert(styles.includes("@media"), "styles should include responsive rules");
assert(launcher.includes("HEALTH_INSPECT_URL") && launcher.includes("is_resource_library_server_ready"), "launcher should require inspect API readiness");
assert(windowsLauncher.includes("HEALTH_INSPECT_URL"), "Windows launcher should require inspect API readiness");
assert(readme.includes("服务未就绪") && readme.includes("inspect API"), "README should explain service-not-ready and inspect compatibility");

console.log("ResourceLibrary smoke ok");

function assert(condition, message) {
  if (!condition) {
    console.error(message);
    process.exit(1);
  }
}
