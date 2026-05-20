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

assert(index.includes("MxFramework 资源库编辑器"), "index should expose Chinese title");
assert(index.includes("resourceList") && index.includes("inspectorContent"), "index should render browser and inspector anchors");
assert(index.includes("Overview") && index.includes("Unity") && index.includes("Runtime") && index.includes("References") && index.includes("Diagnostics"), "index should expose required inspector tabs");
assert(index.includes("等待 import API gate") && index.includes("等待 reference graph delete guard"), "write actions should be disabled with clear reasons");
assert(index.includes("复制详情 JSON") && index.includes("复制诊断 JSON"), "copy JSON actions should be visible");

assert(app.includes("/api/character/packages"), "app should call character packages API");
assert(app.includes("/api/character/resources?package="), "app should call resource list API");
assert(app.includes("/api/character/resource-plan?package="), "app should call resource plan API");
assert(app.includes("/api/character/resources/inspect?package="), "app should call inspect API defensively");
assert(app.includes("buildFallbackInspect") && app.includes("inspect endpoint 不可用"), "app should include inspect fallback behavior");
assert(app.includes("onlyRuntimeLoadable") && app.includes("onlyDiagnostics"), "app should include client-side filters");
assert(app.includes("navigator.clipboard.writeText"), "app should copy JSON through the clipboard when available");
assert(!app.includes("React") && !app.includes("createRoot") && !app.includes("vite"), "app should remain vanilla DOM/fetch JavaScript");

assert(styles.includes(".resource-browser") && styles.includes(".inspector-tabs") && styles.includes(".action-bar"), "styles should cover browser, inspector tabs, and action bar");
assert(styles.includes("@media"), "styles should include responsive rules");

console.log("ResourceLibrary smoke ok");

function assert(condition, message) {
  if (!condition) {
    console.error(message);
    process.exit(1);
  }
}
