import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "../../..");

function requireValue(condition, message) {
  if (!condition) throw new Error(message);
}

function readJson(relative) {
  return JSON.parse(fs.readFileSync(path.join(root, relative), "utf8"));
}

for (const packageId of ["000001", "000002", "000003"]) {
  const sourceRoot = `Authoring/CharacterPackages/character-${packageId}`;
  const generatedRoot = `Assets/MxFrameworkGenerated/CharacterPackages/${packageId}`;
  const manifest = readJson(`${sourceRoot}/manifest.json`);
  const receipt = readJson(`${generatedRoot}/package_cache/unity_editor_import_receipt.json`);
  const profile = readJson(`Assets/MxFrameworkGenerated/CharacterActions/Profiles/${packageId}/character_action_profile.json`);
  requireValue(manifest.packageId === packageId, `${packageId} source manifest identity is invalid.`);
  requireValue(receipt.ready === true && receipt.packageId === packageId, `${packageId} Unity import receipt is not ready.`);
  requireValue(profile.profiles?.length > 0, `${packageId} CharacterAction profile is missing.`);
  requireValue(!profile.profiles.some(item =>
    item.actionLibraryIds?.some(id => ["codex_flow_test", "combat_girl_sword", "wgame_palette_mage_v1", "dragon14"].includes(id))),
  `${packageId} still loads a deleted character action library.`);
}

for (const packageId of ["codex_flow_test", "combat_girl_sword", "wgame_palette_mage_v1", "dragon14"]) {
  requireValue(!fs.existsSync(path.join(root, `Assets/MxFrameworkGenerated/CharacterPackages/${packageId}`)),
    `Deleted generated character package still exists: ${packageId}`);
  requireValue(!fs.existsSync(path.join(root, `Assets/MxFrameworkGenerated/CharacterActions/Profiles/${packageId}`)),
    `Deleted CharacterAction profile still exists: ${packageId}`);
}

const commonActions = readJson("Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json");
const promotedIds = new Set(commonActions.actions
  .map(action => Number(action.actionId))
  .filter(id => id >= 950000 && id <= 950022));
requireValue(promotedIds.size === 23, "The preserved common sword action range is incomplete.");

const runtimeCatalogText = fs.readFileSync(
  path.join(root, "Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json"),
  "utf8");
requireValue(runtimeCatalogText.includes("char.000001.model.body"), "000001 is missing from the Player runtime catalog.");
requireValue(runtimeCatalogText.includes("char.000002.model.body"), "000002 is missing from the Player runtime catalog.");
requireValue(runtimeCatalogText.includes("char.000003.model.body"), "000003 is missing from the Player runtime catalog.");
for (const packageId of ["codex_flow_test", "combat_girl_sword", "wgame_palette_mage_v1", "dragon14"]) {
  requireValue(!runtimeCatalogText.includes(`char.${packageId}.`),
    `Deleted character key remains in the Player runtime catalog: ${packageId}`);
  requireValue(!runtimeCatalogText.includes(`CharacterPackages/${packageId}`),
    `Deleted character address remains in the Player runtime catalog: ${packageId}`);
}

const editorServerSource = fs.readFileSync(
  path.join(root, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.cs"),
  "utf8");
requireValue(editorServerSource.includes("Authoring/CharacterPackages/character-000001"),
  "CharacterStudio default package is not the numeric character 000001.");
const stateSource = fs.readFileSync(path.join(root, "Tools/MxFramework.CharacterStudio/web/src/state.js"), "utf8");
const apiSource = fs.readFileSync(path.join(root, "Tools/MxFramework.CharacterStudio/web/src/api.js"), "utf8");
requireValue(stateSource.includes('DEFAULT_PACKAGE = "Authoring/CharacterPackages/character-000001"'),
  "CharacterStudio browser fallback does not use 000001.");
requireValue(apiSource.includes("apiPackages.filter(pkg => pkg.isTemplate !== true)"),
  "CharacterStudio package selector still exposes non-playable templates as characters.");

console.log("CharacterStudio numeric-character smoke passed: 000001/000002/000003 ready; legacy playable identities removed; public actions preserved.");
