import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(fileURLToPath(new URL("../../../", import.meta.url)));
const hubRoot = path.join(repoRoot, "Tools/MxFramework.EditorHub");
const resourceLibraryRoot = path.join(repoRoot, "Tools/MxFramework.ResourceLibrary");
const animationEditorRoot = path.join(repoRoot, "Tools/MxFramework.AnimationEditor");
const required = [
  "web/index.html",
  "web/app.js",
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

const index = fs.readFileSync(path.join(hubRoot, "web/index.html"), "utf8");
const app = fs.readFileSync(path.join(hubRoot, "web/app.js"), "utf8");
const styles = fs.readFileSync(path.join(hubRoot, "web/styles.css"), "utf8");
const launcher = fs.readFileSync(path.join(hubRoot, "start-editor-hub.sh"), "utf8");
const server = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.cs"), "utf8");

assert(index.includes("MxFramework 外部编辑器中心"), "hub title should be Chinese-first");
assert(index.includes("toolGrid") && index.includes("resourceSummary"), "hub should expose tool and resource sections");
assert(index.includes("diagnosticsConsole") && index.includes("diagnosticsFilter"), "hub should expose the diagnostics console");
assert(app.includes("/api/authoring/resources?package=") && app.includes("/api/authoring/resources/resource-plan?package="), "hub should read Authoring Resource Manager APIs");
assert(app.includes("Tools/MxFramework.CharacterStudio/web/") && app.includes("Tools/MxFramework.Authoring.Editor/web/"), "hub should link existing editors");
assert(app.includes("Tools/MxFramework.ResourceLibrary/web/"), "hub should link Resource Library editor");
assert(app.includes("Tools/MxFramework.AnimationEditor/web/") && app.includes("/api/authoring/animation/packages"), "hub should link Animation Editor and read animation package API");
assert(app.includes("getAnimationEditorStatus") && app.includes("isAnimationEditorApiReady") && app.includes("EDITOR_HUB_STATIC_FILE_MODE"), "hub should separate Animation Editor entry availability from Authoring API readiness");
assert(app.includes("入口存在") && app.includes("API未连接") && app.includes("当前动画包"), "hub should explain Animation Editor API states without marking the tool unavailable");
assert(app.includes("资源管理器") && app.includes("工作上下文") && app.includes("资源 providers"), "hub should present Resource Manager as global resource center with scoped context");
assert(!app.includes('action: "待实现"') && !app.includes("disabled: true"), "Resource Library card should be enabled");
assert(app.includes("renderDiagnostics") && app.includes("getFilteredDiagnostics"), "hub should render and filter diagnostics");
assert(styles.includes(".tool-grid") && styles.includes(".resource-panels"), "hub should style the main tool and resource layouts");
assert(launcher.includes("dotnet --list-sdks") && launcher.includes("is_port_in_use"), "launcher should perform environment and port checks");
assert(launcher.includes("/api/authoring/animation/packages") && launcher.includes("older Authoring server without Animation Editor APIs"), "launcher should reject stale servers missing Animation Editor APIs");
assert(server.includes("MxFramework.EditorHub/web/"), "Authoring server root should route to EditorHub");

console.log("EditorHub smoke ok");

function assert(condition, message) {
  if (!condition) {
    console.error(message);
    process.exit(1);
  }
}
