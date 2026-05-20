import fs from "node:fs";
import path from "node:path";

const repoRoot = path.resolve(new URL("../../../", import.meta.url).pathname);
const hubRoot = path.join(repoRoot, "Tools/MxFramework.EditorHub");
const required = [
  "web/index.html",
  "web/app.js",
  "web/styles.css",
  "start-editor-hub.sh",
  "start-editor-hub.bat",
  "start-editor-hub.command",
  "README.md"
];

for (const relative of required) {
  const full = path.join(hubRoot, relative);
  assert(fs.existsSync(full), `missing ${relative}`);
}

const index = fs.readFileSync(path.join(hubRoot, "web/index.html"), "utf8");
const app = fs.readFileSync(path.join(hubRoot, "web/app.js"), "utf8");
const styles = fs.readFileSync(path.join(hubRoot, "web/styles.css"), "utf8");
const launcher = fs.readFileSync(path.join(hubRoot, "start-editor-hub.sh"), "utf8");
const server = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.cs"), "utf8");

assert(index.includes("MxFramework 外部编辑器中心"), "hub title should be Chinese-first");
assert(index.includes("toolGrid") && index.includes("resourceSummary"), "hub should expose tool and resource sections");
assert(index.includes("diagnosticsConsole") && index.includes("diagnosticsFilter"), "hub should expose the diagnostics console");
assert(app.includes("/api/character/resources") && app.includes("/api/character/resource-plan"), "hub should read resource APIs");
assert(app.includes("Tools/MxFramework.CharacterStudio/web/") && app.includes("Tools/MxFramework.Authoring.Editor/web/"), "hub should link existing editors");
assert(app.includes("renderDiagnostics") && app.includes("getFilteredDiagnostics"), "hub should render and filter diagnostics");
assert(styles.includes(".tool-grid") && styles.includes(".resource-panels"), "hub should style the main tool and resource layouts");
assert(launcher.includes("dotnet --list-sdks") && launcher.includes("is_port_in_use"), "launcher should perform environment and port checks");
assert(server.includes("MxFramework.EditorHub/web/"), "Authoring server root should route to EditorHub");

console.log("EditorHub smoke ok");

function assert(condition, message) {
  if (!condition) {
    console.error(message);
    process.exit(1);
  }
}
