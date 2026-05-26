import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const repoRoot = path.resolve(fileURLToPath(new URL("../../../", import.meta.url)));
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

runNodeSyntaxCheck(path.join(toolRoot, "web/app.js"));

assert(index.includes("MxFramework 资源管理器"), "index should expose Chinese title");
assert(index.includes("resourceList") && index.includes("resourceTree") && index.includes("treeGroupModeSelect"), "index should render resource tree and flat list");
assert(index.includes("inspectorContent"), "index should render inspector anchors");
assert(app.includes("buildPathTree") && app.includes("buildTaxonomyTree") && app.includes("getTreeScopedItems"), "app should build hierarchical resource trees");
assert(index.includes("Overview") && index.includes("Unity") && index.includes("Runtime") && index.includes("References") && index.includes("Diagnostics"), "index should expose required inspector tabs");
assert(index.includes("resourceImportFileInput") && index.includes("resourceImportFolderInput") && index.includes("resourceReplaceFileInput"), "write actions should provide hidden file inputs");
assert(index.includes("importPresetSelect") && index.includes("导入类型"), "write actions should expose typed import presets");
assert(index.includes("importResourceButton") && index.includes("importFolderButton") && index.includes("reimportResourceButton") && index.includes("replaceSourceButton"), "write actions should expose import/folder/reimport/replace buttons");
assert(index.includes("workspaceProfile") && index.includes("buildProfileContent") && index.includes("saveBuildProfileButton"), "profile workspace should expose build profile editor and save action");
assert(index.includes("data-workspace=\"browse\"") && index.includes("data-workspace=\"profile\"") && index.includes("data-workspace=\"build\"") && index.includes("data-workspace=\"debug\""), "index should expose four primary workspaces");
assert(app.includes('data-action="add-profile"') && app.includes('data-action="open-profile"'), "browse/profile contextual actions should use data-action hooks");
assert(!index.includes("bottom-panel-container") && !index.includes("action-bar"), "persistent bottom action bar should be removed");
assert(index.includes("profileMembershipFilter") && index.includes("runtimeReadyFilter"), "profile workflow should expose membership and runtime-ready filters");
assert(index.includes("selectVisibleButton") && index.includes("clearCheckedButton") && index.includes("checkedSummary"), "resource browser should expose explicit multi-select controls");
assert(app.includes("batch-add-profile") && app.includes("batch-remove-profile"), "batch profile actions should appear via contextual batch bars");
assert(index.includes("rawJsonSection") && index.includes("Raw JSON"), "debug workspace should expose collapsed raw JSON section");
assert(index.includes("openUnityWorkbenchButton") && index.includes("buildChecklist"), "build workspace should expose unity guidance and local chain checklist");
assert(index.includes("data-tab=\"build\""), "inspector should expose Build tab");
assert(!index.includes("deleteResourceButton") && !index.includes("editTagsButton"), "delete/tag actions should stay hidden until APIs are ready");
assert(index.includes("复制详情 JSON") && index.includes("复制诊断 JSON"), "copy JSON actions should be visible");

assert(app.includes("/api/character/packages"), "app should call character packages API");
assert(app.includes("/api/authoring/resources?package="), "app should call authoring resource list API");
assert(app.includes("/api/authoring/resources/resource-plan?package="), "app should call authoring resource plan API");
assert(app.includes("/api/authoring/resources/inspect?package="), "app should call authoring inspect API defensively");
assert(app.includes("/api/authoring/resources/stage-import"), "app should call external import staging API before folder promotion");
assert(app.includes("/api/authoring/resources/import") && app.includes("/api/authoring/resources/reimport") && app.includes("/api/authoring/resources/replace-source"), "app should call authoring resource write API gates");
assert(app.includes("/api/authoring/resources/global-build-profile") && app.includes("/api/authoring/resources/bundle-plan?package="), "app should call global build profile and bundle planner APIs");
assert(app.includes("saveBuildProfileDraft") && app.includes("findBuildProfileEntryForItem") && app.includes("bundleOverrideMode"), "app should support profile membership and planner intent editing");
assert(app.includes("activeWorkspace") && app.includes("setWorkspace") && app.includes('setWorkspace("profile")'), "app should navigate across browse/profile/build/debug workspaces");
assert(app.includes("getDeliveryEntryState") && app.includes("will build"), "app should expose delivery outcome labels for profile entries");
assert(app.includes("inferProfileValidationField") && app.includes("field-highlight"), "app should highlight profile fields on save validation failure");
assert(app.includes("deliveryMode") && app.includes('entry.bundleRule = ""'), "non-internal delivery modes should clear bundle rule in draft edits");
assert(app.includes("checkedResourceKeys: new Set()") && app.includes("selectVisibleResources") && app.includes("toggleCheckedResource"), "app should keep explicit checked resources separate from inspected resource selection");
assert(app.includes("addCheckedToBuildProfile") && app.includes("removeCheckedFromBuildProfile"), "app should support batch add/remove of checked resources to the profile draft");
assert(app.includes("applyBuildProfileBatchFields") && app.includes("data-profile-batch-enabled") && app.includes("data-profile-batch-field"), "app should support explicit opt-in batch field edits for checked draft profile entries");
assert(app.includes('renderBuildProfileBatchField("bundleOverrideMode", "override mode", "select", ["none", "forceStandalone", "forceExternal", "exclude"])'), "batch override mode should exclude forceBundle while preserving single-entry editing");
assert(app.includes("splitCsv(rawValue)") && app.includes("Array.isArray(value) && value.length === 0"), "batch labels and preload groups should replace arrays through splitCsv and ignore empty parsed inputs");
assert(app.includes("profileMembership") && app.includes("runtimeReady") && app.includes("isRuntimeReadyCandidate"), "app should filter by Build Profile membership and runtime-ready candidates");
assert(app.includes("notInProfile") && app.includes("saved") && app.includes("draftOnly") && app.includes("removedInDraft") && app.includes("modifiedInDraft"), "app should expose draft-vs-saved Build Profile state labels");
assert(app.includes("Bundle Plan 来自已保存 Profile") && app.includes("Web UI 不构建 AssetBundle，也不写 StreamingAssets"), "app should describe Bundle Plan as saved-profile preview only");
assert(!app.includes("/api/character/resources/import") && !app.includes("/api/character/resources/reimport") && !app.includes("/api/character/resources/replace-source"), "new Resource Manager writes should not use character-prefixed write APIs");
assert(!app.includes("/api/authoring/resources/build-assetbundle") && !app.includes("/api/authoring/resources/write-streaming-assets"), "web UI should not add backend build or StreamingAssets write routes");
assert(app.includes("resourceId") && app.includes("sourceProviderId") && app.includes("providerBindings"), "app should understand authoring resource identity and provider bindings");
assert(app.includes("providerFilter") && app.includes("getProviders") && app.includes("renderProviderList"), "app should expose provider filter and provider status");
assert(app.includes("fmodEventPath") && app.includes("audioCueId") && app.includes("audioEventDefinitionId"), "app should preserve FMOD audio provider metadata");
assert(app.includes("targetResourceId") && app.includes("targetProviderResourceKey") && app.includes("targetRuntimeResourceKey"), "app should match cross-consumer reference graph targets");
assert(app.includes("postJson") && app.includes("readFileAsBase64"), "app should post write requests with uploaded file bytes");
assert(app.includes("IMPORT_PRESETS") && app.includes("animationClipGroup") && app.includes("audioCue"), "app should define typed animation and audio import presets");
assert(app.includes('id: "animationClipGroup"') && app.includes('extensions: ["anim", "glb", "gltf", "json"]'), "animation import preset should accept Unity .anim, GLB/GLTF, and JSON animation sources without treating FBX as animation");
assert(app.includes("importResourceFolder") && app.includes("webkitRelativePath"), "app should support folder import with stable local ids");
assert(app.includes("isIgnoredImportFile") && app.includes(".meta") && app.includes("忽略元数据"), "folder import should ignore editor metadata files separately from skipped resources");
assert(app.includes("isImportableStagedItem") && app.includes("AUTH_RES_IMPORT_IGNORED_FILE") && app.includes("externalImportStaging"), "folder import should use staging diagnostics and provider items");
assert(app.includes("isStagedItemSupportedByPreset") && app.includes("buildImportRequestFromStagedItem(file, staged, preset)") && app.includes("跳过非匹配"), "folder import should filter and promote staged files by the selected import preset");
assert(app.includes("inspectCache.clear") && app.includes("loadPackageData"), "write responses should refresh resource data");
assert(app.includes("buildFallbackInspect") && app.includes("inspect endpoint 不可用"), "app should include inspect fallback behavior");
assert(app.includes("onlyRuntimeLoadable") && app.includes("onlyDiagnostics"), "app should include client-side filters");
assert(app.includes("navigator.clipboard.writeText"), "app should copy JSON through the clipboard when available");
assert(!app.includes("React") && !app.includes("createRoot") && !app.includes("vite"), "app should remain vanilla DOM/fetch JavaScript");

assert(styles.includes(".resource-browser") && styles.includes(".inspector-tabs") && styles.includes(".workspace-nav") && styles.includes(".import-preset-label") && styles.includes(".profile-workspace-panel"), "styles should cover browser, workspace nav, import preset, and profile workspace UI");
assert(styles.includes(".context-actions") && styles.includes(".batch-bar") && styles.includes(".build-checklist"), "styles should cover contextual actions and build checklist");
assert(!styles.includes(".action-bar") && !styles.includes(".bottom-panel-container"), "styles should not retain persistent bottom action bar layout");
assert(styles.includes(".selection-toolbar") && styles.includes(".resource-check") && styles.includes(".profile-state-strip"), "styles should cover multi-select and profile state feedback");
assert(styles.includes(".profile-batch-form") && styles.includes(".profile-batch-toggle") && styles.includes(".profile-batch-apply"), "styles should cover build profile batch field editing controls");
assert(styles.includes("@media"), "styles should include responsive rules");
assert(launcher.includes("HEALTH_INSPECT_URL") && launcher.includes("is_resource_library_server_ready"), "launcher should require inspect API readiness");
assert(windowsLauncher.includes("HEALTH_INSPECT_URL"), "Windows launcher should require inspect API readiness");
assert(readme.includes("服务未就绪") && readme.includes("inspect API"), "README should explain service-not-ready and inspect compatibility");
assert(readme.includes("选择可见") && readme.includes("notInProfile") && readme.includes("removedInDraft") && readme.includes("Bundle Plan 是预览面"), "README should document multi-select, draft state labels, and preview-only Bundle Plan behavior");
assert(!readme.includes("YooAsset 是默认路线") && !readme.includes("Addressables"), "README should not claim a new default resource backend");

runBuildProfileBatchBehaviorSmoke();

console.log("ResourceLibrary smoke ok");

function assert(condition, message) {
  if (!condition) {
    console.error(message);
    process.exit(1);
  }
}

function runNodeSyntaxCheck(filePath) {
  const result = spawnSync(process.execPath, ["--check", filePath], { encoding: "utf8" });
  if (result.status !== 0) {
    const output = [result.stdout, result.stderr].filter(Boolean).join("\n").trim();
    assert(false, `JavaScript syntax check failed for ${path.relative(toolRoot, filePath)}:\n${output}`);
  }
}

function runBuildProfileBatchBehaviorSmoke() {
  const runtime = buildAppBatchRuntime();
  const entries = [
    {
      resourceKey: "pkg:Texture:hero:",
      deliveryMode: "internal",
      bundleOverrideMode: "forceBundle",
      bundleGroupHint: "characters",
      bundleRule: "character",
      labels: ["old.label"],
      preloadGroups: ["old.preload"]
    },
    {
      resourceKey: "pkg:Audio:theme:",
      deliveryMode: "internal",
      bundleOverrideMode: "none",
      bundleGroupHint: "music",
      bundleRule: "audio",
      labels: ["audio.old"],
      preloadGroups: ["music.old"]
    }
  ];

  runtime.reset(entries, [
    { key: "deliveryMode", value: "external", enabled: true },
    { key: "bundleOverrideMode", value: "forceBundle", enabled: true },
    { key: "bundleGroupHint", value: "   ", enabled: true },
    { key: "bundleRule", value: "should-not-apply", enabled: true, disabled: true },
    { key: "labels", value: "runtime, featured,  ", enabled: true },
    { key: "preloadGroups", value: "warmup, hero", enabled: true }
  ]);
  runtime.applyBuildProfileBatchFields();

  assert(runtime.state.lastActionMessage.includes("已应用 2") && runtime.state.lastActionMessage.includes("跳过 1"), "checked draft entries should be applied and missing entries skipped");
  assert(runtime.state.buildProfileDirty === true, "real batch edits should mark the profile dirty");
  assert(entries[0].deliveryMode === "external" && entries[1].deliveryMode === "external", "deliveryMode should update checked draft entries");
  assert(entries[0].bundleOverrideMode === "forceBundle", "existing forceBundle should remain when batch override mode is not explicitly allowed");
  assert(entries[0].bundleGroupHint === "characters", "blank bundleGroupHint should not overwrite existing values");
  assert(entries[0].bundleRule === "character", "disabled bundleRule should not overwrite existing values");
  assert(arrayEquals(entries[0].labels, ["runtime", "featured"]) && arrayEquals(entries[0].preloadGroups, ["warmup", "hero"]), "labels and preloadGroups should replace whole arrays from CSV");

  runtime.reset(entries, [{ key: "bundleOverrideMode", value: "forceExternal", enabled: true }], ["hero"]);
  runtime.applyBuildProfileBatchFields();

  assert(runtime.state.buildProfileDirty === true && entries[0].bundleOverrideMode === "forceExternal", "allowed batch override mode should explicitly replace existing forceBundle");

  runtime.reset(entries, [
    { key: "deliveryMode", value: "external", enabled: true },
    { key: "bundleOverrideMode", value: "forceExternal", enabled: true },
    { key: "labels", value: "runtime, featured", enabled: true },
    { key: "preloadGroups", value: "warmup, hero", enabled: true }
  ], ["hero"]);
  runtime.applyBuildProfileBatchFields();

  assert(runtime.state.lastActionMessage.includes("已应用 1") && runtime.state.buildProfileDirty === false, "no-op batches should apply to draft entries without marking them dirty");

  runtime.reset(entries, []);
  runtime.applyBuildProfileBatchFields();
  assert(runtime.state.lastActionMessage.includes("没有启用可应用"), "disabled or blank-only batches should not overwrite and should report no applicable fields");
}

function buildAppBatchRuntime() {
  const snippets = [
    "applyBuildProfileBatchFields",
    "readBuildProfileBatchFields",
    "getProfileBatchRoot",
    "isAllowedBuildProfileBatchOverrideMode",
    "buildProfileBatchValuesEqual",
    "cloneBuildProfileBatchValue",
    "splitCsv",
    "cssEscapeCompat"
  ].map(extractFunctionBlock).join("\n\n");
  const createRuntime = Function("sandbox", `
    const state = sandbox.state;
    const el = sandbox.el;
    const window = {};
    const getCheckedItems = sandbox.getCheckedItems;
    const findDraftBuildProfileEntryForItem = sandbox.findDraftBuildProfileEntryForItem;
    const markBuildProfileDirty = sandbox.markBuildProfileDirty;
    const render = sandbox.render;
    ${snippets}
    return { applyBuildProfileBatchFields };
  `);

  const state = { buildProfileDirty: false, lastActionMessage: "" };
  const checkedCatalog = {
    hero: { resourceKey: "pkg:Texture:hero:" },
    theme: { resourceKey: "pkg:Audio:theme:" },
    missing: { resourceKey: "pkg:Prefab:missing:" }
  };
  const sandbox = {
    state,
    el: { buildProfileContent: createBatchForm([]), profileBatchBar: createBatchForm([]) },
    checkedItems: [checkedCatalog.hero, checkedCatalog.theme, checkedCatalog.missing],
    entries: [],
    getCheckedItems() {
      return sandbox.checkedItems;
    },
    findDraftBuildProfileEntryForItem(item) {
      return sandbox.entries.find(entry => entry.resourceKey === item.resourceKey) || null;
    },
    markBuildProfileDirty() {
      state.buildProfileDirty = true;
    },
    render() {}
  };
  const runtime = createRuntime(sandbox);
  return {
    state,
    applyBuildProfileBatchFields: runtime.applyBuildProfileBatchFields,
    reset(entries, fields, checkedKeys = ["hero", "theme", "missing"]) {
      state.buildProfileDirty = false;
      state.lastActionMessage = "";
      sandbox.entries = entries;
      sandbox.checkedItems = checkedKeys.map(key => checkedCatalog[key]);
      const form = createBatchForm(fields);
      sandbox.el.buildProfileContent = form;
      sandbox.el.profileBatchBar = form;
      sandbox.el.profileBatchBar.classList = { contains: () => false };
    }
  };
}

function createBatchForm(fields) {
  const controls = fields.map(field => ({
    dataset: { profileBatchField: field.key },
    value: field.value,
    disabled: Boolean(field.disabled)
  }));
  const toggles = new Map(fields.map(field => [field.key, { checked: Boolean(field.enabled) }]));
  return {
    querySelectorAll(selector) {
      return selector === "[data-profile-batch-field]" ? controls : [];
    },
    querySelector(selector) {
      const fieldMatch = selector.match(/data-profile-batch-enabled="([^"]+)"/);
      if (fieldMatch) return toggles.get(fieldMatch[1]) || null;
      return null;
    }
  };
}

function extractFunctionBlock(name) {
  const signature = `function ${name}`;
  const start = app.indexOf(signature);
  assert(start >= 0, `missing app function ${name}`);
  const bodyStart = app.indexOf("{", start);
  assert(bodyStart >= 0, `missing app function body ${name}`);
  let depth = 0;
  for (let index = bodyStart; index < app.length; index++) {
    const char = app[index];
    if (char === "{") depth++;
    if (char === "}") {
      depth--;
      if (depth === 0) return app.slice(start, index + 1);
    }
  }
  assert(false, `unterminated app function ${name}`);
}

function arrayEquals(left, right) {
  return Array.isArray(left)
    && Array.isArray(right)
    && left.length === right.length
    && left.every((value, index) => value === right[index]);
}
