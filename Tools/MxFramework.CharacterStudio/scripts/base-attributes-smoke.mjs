import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";

const source = fs.readFileSync(new URL("../web/src/inspector.js", import.meta.url), "utf8");
for (const name of ["buildTree", "findTarget", "editableFields", "commitInspectorField", "readInspectorInputValue", "clampFieldNumber", "formatAttributeTimeHint"]) {
  const start = source.indexOf(`function ${name}(`);
  const end = source.indexOf("\nfunction ", start + 1);
  assert(start >= 0, name);
  vm.runInThisContext(source.slice(start, end < 0 ? undefined : end));
}
globalThis.state = { canWrite: true, selectedPath: "config/attributes", package: {
  geometry: { bodyProfile: { massKg: 76 } },
  applicationConfig: { defaultCombinedWeaponConfigurationId: "sword" }
} };
globalThis.node = (id, kind, label) => ({ id, kind, label });
globalThis.target = (kind, name, value) => ({ kind, name, value });
globalThis.integerField = (path, label, options) => ({ path, label, ...options });
globalThis.getNested = (value, path) => value[path];
globalThis.setNested = (value, path, next) => { value[path] = next; };
let beforeEdit;
globalThis.beginInspectorUndoTransaction = () => { beforeEdit = structuredClone(state.package); };
for (const name of ["afterInspectorFieldEdited", "applySymmetryEdit", "updateUndoTransactionAfter",
  "refreshDirtyFromCleanSnapshot", "renderShellStatus", "requestViewportUpdate"]) globalThis[name] = () => {};
globalThis.shouldRefreshInspectorForField = () => false;

assert(buildTree(state.package).some(item => item.id === "config/attributes"));
const current = findTarget("config/attributes");
assert.equal(current.value.hp, 100);
assert.equal(state.package.baseAttributes, undefined, "Viewing defaults must not create a draft.");
assert.equal(editableFields("baseAttributes").length, 7);
commitInspectorField(current, { value: "240", dataset: { field: "hp", type: "number", min: "1", max: "2147483647" } });
assert.equal(state.package.baseAttributes.hp, 240);
assert.equal(findTarget("config/attributes").value.hp, 240);
assert.deepEqual(state.package.geometry, beforeEdit.geometry);
assert.deepEqual(state.package.applicationConfig, beforeEdit.applicationConfig);
state.package = beforeEdit;
assert.equal(findTarget("config/attributes").value.hp, 100, "Undo must restore the unconfigured default.");
assert.equal(state.package.baseAttributes, undefined);
const timeHint = { dataset: { attributeTimeFor: "scatterRecoveryDelayFrames" }, textContent: "" };
globalThis.el = { inspector: { querySelectorAll: () => [timeHint] } };
commitInspectorField(findTarget("config/attributes"), {
  value: "120", dataset: { field: "scatterRecoveryDelayFrames", type: "number", min: "0", max: "2147483647" }
});
assert.equal(state.package.baseAttributes.scatterRecoveryDelayFrames, 120);
assert.equal(timeHint.textContent, "2 秒模拟时间（按 60Hz）");
console.log("CharacterStudio base attribute editing and ownership checks passed.");
