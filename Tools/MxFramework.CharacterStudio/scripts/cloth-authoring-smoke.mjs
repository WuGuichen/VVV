import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");
const read = relative => fs.readFileSync(path.join(root, relative), "utf8");
const assert = (condition, message) => { if (!condition) throw new Error(message); };

const state = read("Tools/MxFramework.CharacterStudio/web/src/state.js");
const api = read("Tools/MxFramework.CharacterStudio/web/src/api.js");
const panels = read("Tools/MxFramework.CharacterStudio/web/src/contextPanels.js");
const server = read("Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.CharacterClothMagica.cs");
const config = JSON.parse(read("Authoring/CharacterPackages/character-wgame-palette-mage-v1/config/character_cloth_magica.json"));

assert(state.includes("clothMagica: null"), "CharacterStudio state must own clothMagica status.");
assert(api.includes("state.clothMagica = apiState.clothMagica || null"), "Package state must hydrate clothMagica.");
assert(panels.includes("renderCharacterClothReadinessSummary"), "Readiness must render the Cloth status card.");
assert(panels.includes("data-cloth-magica-open"), "Cloth status card must expose the Unity handoff button.");
assert(panels.includes("/api/character/cloth-magica/open"), "Unity handoff must use the typed Authoring API.");
assert(server.includes("ReadCharacterClothMagicaState"), "Authoring server must expose a Cloth status reader.");
assert(server.includes("CharacterCloth/open_request.json"), "Authoring server must write the Unity open request.");
assert(config.schemaVersion === "1.0" && config.backend === "MagicaCloth2", "Maintained character Cloth config identity is invalid.");
assert(config.cloths?.length === 1 && config.cloths[0].colliders?.length >= 1, "Maintained character Cloth config is missing its cloth/collider declaration.");

console.log("Character Cloth authoring smoke passed.");
