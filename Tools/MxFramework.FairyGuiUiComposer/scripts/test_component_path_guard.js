"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

// Run the actual helper definitions without its Editor startup menu and watcher.
const helperPath = path.resolve(__dirname, "../../../FGUIProject/plugins/wgameframework-agent-helper/main.js");
const helper = fs.readFileSync(helperPath, "utf8");
const startup = helper.indexOf("\ninstallMenu();");
assert(startup > 0, "Expected helper startup boundary.");
let projectReads = 0;
const context = vm.createContext({
  CS: { FairyEditor: { App: { get project() { projectReads++; throw new Error("Editor accessed"); } } }, System: {} },
  console
});
vm.runInContext(helper.slice(0, startup), context, { filename: helperPath });

function request(paths) {
  return {
    schemaVersion: "mx.uiComposerOperation.v1",
    packageId: "MxUiFoundationDemo",
    materialization: {
      packageId: "MxUiFoundationDemo",
      packageSourcePath: "FGUIProject/assets/MxUiFoundationDemo",
      componentName: "UiFoundationDemoPanel",
      resources: [{ sourcePngPath: "ArtSource/UIKits/MxUiFoundation/images/button.png", targetPngRelativePath: "Images/MxUiFoundation/button.png" }],
      components: paths.map((componentRelativePath, i) => ({ componentRecipeId: `ui.component.test.${i}`, token: `TOKEN_${i}`, componentRelativePath, xmlTemplate: `<component size="${240 + i * 40},64"/>` }))
    }
  };
}

(async () => {
  for (const paths of [
    ["Components/GenericButton.xml", "Components/GenericButton.xml"],
    ["Components/GenericButton.xml", "Components/genericbutton.xml"],
    ["Components/First.xml", "Components/Second.xml", "Components/FIRST.xml"]
  ]) {
    const value = request(paths);
    assert.throws(() => context.validateComposerMaterialization(value), /component path collision: recipes 'ui.component.test.0' and 'ui.component.test./);
    await assert.rejects(context.materializeUiComposerPackage(value, () => assert.fail("Phase reached before collision rejection")), /component path collision/);
    assert.equal(projectReads, 0, "Duplicate payload must be rejected before accessing the Editor or filesystem.");
  }
  context.validateComposerMaterialization(request(["Components/GenericButton.xml", "Components/T07UniqueProbe.xml"]));
  context.validateComposerMaterialization(request([])); // Screens without recipe dependencies remain valid.
  assert.throws(() => context.validateComposerMaterialization(request(["Components/../Escape.xml"])), /component path is not allowed/);
  console.log("UI Composer helper component path guard tests passed (3 collisions, unique and empty controls, path safety).");
})().catch(error => { console.error(error); process.exitCode = 1; });
