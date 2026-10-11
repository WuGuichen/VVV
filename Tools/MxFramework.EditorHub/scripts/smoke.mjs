import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(fileURLToPath(new URL("../../../", import.meta.url)));
const hubRoot = path.join(repoRoot, "Tools/MxFramework.EditorHub");
const resourceLibraryRoot = path.join(repoRoot, "Tools/MxFramework.ResourceLibrary");
const animationEditorRoot = path.join(repoRoot, "Tools/MxFramework.AnimationEditor");
const buffWorkbenchRoot = path.join(repoRoot, "Tools/MxFramework.BuffWorkbench");
const uiComposerRoot = path.join(repoRoot, "Tools/MxFramework.FairyGuiUiComposer");
const required = [
  "web/index.html",
  "web/app.js",
  "web/common.js",
  "web/diagnostics.html",
  "web/diagnostics.js",
  "web/styles.css",
  "start-editor-hub.sh",
  "start-editor-hub.bat",
  "start-editor-hub.command",
  "README.md"
];
const requiredResourceLibrary = [
  "README.md",
  "start-resource-library.sh",
  "start-resource-library.bat",
  "start-resource-library.command"
];
const requiredAnimationEditor = [
  "README.md",
  "start-animation-editor.sh",
  "start-animation-editor.bat",
  "start-animation-editor.command",
  "scripts/smoke.mjs",
  "web/index.html",
  "web/app.js",
  "web/styles.css"
];
const requiredBuffWorkbench = [
  "README.md",
  "web/index.html",
  "web/app.js",
  "web/styles.css"
];
const requiredUiComposer = [
  "web/index.html",
  "web/app.js",
  "web/styles.css"
];

for (const relative of required) {
  const full = path.join(hubRoot, relative);
  assert(fs.existsSync(full), `missing ${relative}`);
}

for (const relative of requiredResourceLibrary) {
  const full = path.join(resourceLibraryRoot, relative);
  assert(fs.existsSync(full), `missing ResourceLibrary ${relative}`);
}

for (const relative of requiredAnimationEditor) {
  const full = path.join(animationEditorRoot, relative);
  assert(fs.existsSync(full), `missing AnimationEditor ${relative}`);
}

for (const relative of requiredBuffWorkbench) {
  const full = path.join(buffWorkbenchRoot, relative);
  assert(fs.existsSync(full), `missing BuffWorkbench ${relative}`);
}

for (const relative of requiredUiComposer) {
  const full = path.join(uiComposerRoot, relative);
  assert(fs.existsSync(full), `missing UI Composer ${relative}`);
}

const index = fs.readFileSync(path.join(hubRoot, "web/index.html"), "utf8");
const app = fs.readFileSync(path.join(hubRoot, "web/app.js"), "utf8");
const common = fs.readFileSync(path.join(hubRoot, "web/common.js"), "utf8");
const diagnosticsIndex = fs.readFileSync(path.join(hubRoot, "web/diagnostics.html"), "utf8");
const diagnosticsApp = fs.readFileSync(path.join(hubRoot, "web/diagnostics.js"), "utf8");
const allApp = app + common + diagnosticsApp;
const styles = fs.readFileSync(path.join(hubRoot, "web/styles.css"), "utf8");
const launcher = fs.readFileSync(path.join(hubRoot, "start-editor-hub.sh"), "utf8");
const windowsLauncher = fs.readFileSync(path.join(hubRoot, "start-editor-hub.bat"), "utf8");
const server = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.cs"), "utf8");
const buffServer = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.Buffs.cs"), "utf8");

assert(index.includes("MxFramework 外部编辑器中心"), "hub title should be Chinese-first");
assert(index.includes("toolGrid") && index.includes("recentList"), "home should expose tools and recent work");
assert(!/id="(?:packageSelect|statusStrip|readinessFlow|resourceList|planGrid|diagnosticsConsole)"/.test(index), "home must not expose the old dashboard or global context selector");
assert(diagnosticsIndex.includes("diagnosticsConsole") && diagnosticsIndex.includes("diagnosticsFilter"), "diagnostics should have its own page");
assert(diagnosticsIndex.includes("readinessFlow"), "delivery evidence remains available on the diagnostics page");
assert(diagnosticsApp.includes("/api/authoring/resources?package=") && diagnosticsApp.includes("/api/authoring/resources/resource-plan?package="), "diagnostics should retain resource checks");
assert(diagnosticsApp.includes("/api/character/readiness?package=") && diagnosticsApp.includes("AbilityRuntimeBinding"), "diagnostics should retain character readiness checks");
for (const tool of ["CharacterStudio", "BuffWorkbench", "ResourceLibrary", "AnimationEditor", "FairyGuiUiComposer", "FairyGuiArtSlices", "AudioWorkbench"]) {
  assert(common.includes(`Tools/MxFramework.${tool}/web/`), `missing ${tool} entry`);
}
assert(app.includes("/api/authoring/buffs/packages") && app.includes("/api/authoring/ui-composer/screens"), "targets should be selected from current catalogs");
assert(diagnosticsApp.includes("/api/character-actions/damage-analysis"), "action damage diagnostics should remain available");
assert(!allApp.includes("Tools/MxFramework.DamageProfileEditor/web/"), "removed damage editor must not be linked");
assert(diagnosticsApp.includes("getFilteredDiagnostics") && diagnosticsApp.includes("collapseNoisyDiagnostics") && diagnosticsApp.includes("AUTH_RES_AUDIO_AUDITION_RECOMMENDED"), "diagnostic filters and optional audition grouping must survive");
assert(styles.includes(".tool-grid") && styles.includes(".recent-list") && styles.includes(".readiness-stage"), "home and diagnostics layouts must be styled");
assert(launcher.includes("dotnet --list-sdks") && launcher.includes("is_port_in_use"), "launcher should perform environment and port checks");
assert(launcher.includes("MxFramework.AudioGenerationToken") && launcher.includes("load_audio_generation_token") && launcher.includes("security find-generic-password"), "macOS launcher should restore the Audio Workbench generation token from Keychain");
assert(!launcher.includes("security find-generic-password -w"), "launcher must not print the Audio Workbench generation token");
assert(launcher.includes("/api/authoring/animation/packages") && launcher.includes("older Authoring server without Animation Editor APIs"), "launcher should reject stale servers missing Animation Editor APIs");
assert(launcher.includes("/api/character-actions/damage-analysis"), "launcher should reject stale servers missing Action Damage Analysis API");
assert(launcher.includes("/api/authoring/buffs/options"), "launcher should reject stale servers missing Buff Workbench designer metadata API");
assert(windowsLauncher.includes("/api/character-actions/damage-analysis"), "Windows launcher should reject stale servers missing Action Damage Analysis API");
assert(windowsLauncher.includes("/api/authoring/buffs/options"), "Windows launcher should reject stale servers missing Buff Workbench designer metadata API");
assert(server.includes("MxFramework.EditorHub/web/"), "Authoring server root should route to EditorHub");
assert(server.includes("/api/character/readiness"), "Authoring server should expose the character readiness API");
assert(buffServer.includes("/api/authoring/buffs/packages"), "Authoring server should expose Buff Workbench package API");
assert(buffServer.includes("/api/authoring/buffs/options"), "Authoring server should expose Buff Workbench designer metadata API");
const composerServer = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.UiComposer.cs"), "utf8");
assert(composerServer.includes("/api/authoring/ui-composer/state") && composerServer.includes("/api/authoring/ui-composer/plan-apply"), "Authoring server should expose UI Composer APIs");

console.log("EditorHub smoke ok");

function assert(condition, message) {
  if (!condition) {
    console.error(message);
    process.exit(1);
  }
}
