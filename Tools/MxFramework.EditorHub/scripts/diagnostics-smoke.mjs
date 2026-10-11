import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";

const code = fs.readFileSync(new URL("../web/diagnostics.js", import.meta.url), "utf8");
const requests = [];
const packages = [{ relative: "template", isTemplate: true }, { relative: "formal", isTemplate: false }];
const context = vm.createContext({
  console, URLSearchParams, setTimeout, AbortSignal,
  localStorage: { getItem: () => null },
  document: { addEventListener() {} },
  window: { location: { protocol: "http:", search: "" } },
  fetch: async url => { requests.push(url); return { ok: true, json: async () => url === "/api/character/packages" ? packages : {} }; }
});
vm.runInContext(fs.readFileSync(new URL("../web/common.js", import.meta.url), "utf8"), context);
vm.runInContext(code + "\nglobalThis.test = { state, el, loadPackages, loadReadiness, loadResourcePlan, collectDiagnostics, deduplicateDiagnostics, renderReadiness };", context);
const { test } = context;
await test.loadPackages();
assert.equal(test.state.packageRelative, "formal", "default should choose an actual character");
test.state.packageRelative = "template";
await test.loadPackages();
assert.equal(test.state.packageRelative, "template", "an explicit template selection must be respected");
requests.length = 0;
await test.loadReadiness();
await test.loadResourcePlan();
assert.equal(requests.length, 0, "a template must not be sent through runtime gates");
test.state.characterState = { validation: { issues: [{ severity: "Error", code: "INVALID_SOURCE", message: "source is broken" }] } };
let diagnostics = test.collectDiagnostics();
assert(diagnostics.some(x => x.code === "INVALID_SOURCE"), "template source errors must remain visible");
assert(diagnostics.some(x => x.code === "EDITOR_HUB_TEMPLATE_CONTEXT"), "template mode must be explained");
for (const key of ["readinessSummary", "readinessFlow"]) test.el[key] = {};
test.renderReadiness();
assert(test.el.readinessSummary.textContent.includes("模板"));
test.state.packageRelative = "formal";
await test.loadReadiness(); await test.loadResourcePlan();
assert.equal(requests.length, 2, "formal characters retain all runtime checks");
test.state.characterState = null;
const first = { severity: "Error", code: "CHAR_RUNTIME_ID_MISSING", message: "missing identity", sourceConfigKind: "Tools/RuntimeHash/ids.json", sourceField: "runtimeCharacterId" };
const second = { severity: "Error", code: first.code, message: first.message, sourcePath: "Tools/RuntimeHash/ids.json", sourceObjectPath: "runtimeCharacterId" };
test.state.resourcePlan = { resourceValidationReport: { diagnostics: [first] } };
test.state.readiness = { blockers: [second] };
diagnostics = test.collectDiagnostics();
assert.equal(diagnostics.length, 1);
assert.equal(diagnostics[0].reportedBy.length, 2, "deduplication must retain both reporting sources");
const different = test.deduplicateDiagnostics([first, { ...first, sourceField: "anotherField" }]);
assert.equal(different.length, 2, "different locations are separate issues");
test.state.resources = { diagnostics: [
  { code: "AUTH_RES_ORPHAN_CANDIDATE", severity: "Warning", resourceStableId: "resource.a", message: "not referenced" },
  { code: "AUTH_RES_ORPHAN_CANDIDATE", severity: "Warning", resourceStableId: "resource.b", message: "not referenced" },
  { code: "AUTH_RES_AUDIO_AUDITION_RECOMMENDED", severity: "Info", resourceStableId: "audio.a", message: "optional review" }
] };
test.state.readiness = null;
test.state.resourcePlan = null;
diagnostics = test.collectDiagnostics();
assert.equal(diagnostics.length, 2);
assert.equal(diagnostics.find(x => x.code === "AUTH_RES_ORPHAN_CANDIDATE").members.length, 2, "grouped diagnostics retain all affected resources");
assert.equal(diagnostics.find(x => x.code === "AUTH_RES_AUDIO_AUDITION_RECOMMENDED").severity, "Info", "optional audition must remain informational");
test.state.packageRelative = "formal";
test.state.readiness = { packageId: "formal", overallStatus: "Deferred", stages: [
  { id: "UnityImport", label: "Unity 导入", status: "Deferred", actionLabel: "执行 Unity 导入", primaryToolUrl: "/Tools/MxFramework.EditorHub/web/" }
] };
test.renderReadiness();
assert(test.el.readinessFlow.innerHTML.includes("CharacterStudio/web/"), "import follow-up must route to the actual owner");
assert(!test.el.readinessFlow.innerHTML.includes('href="/Tools/MxFramework.EditorHub/web/"'), "import must not loop to the launcher");
console.log("Editor Hub diagnostics behavior: PASS");
