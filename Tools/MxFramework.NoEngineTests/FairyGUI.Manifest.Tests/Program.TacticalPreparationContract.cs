using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using MxFramework.Authoring.UiComposer;
using MxFramework.UI.FairyGui;

namespace MxFramework.NoEngineTests.FairyGUI.Manifest
{
    internal static partial class Program
    {
        private static int CheckTacticalPreparation(bool sourceOnly = false)
        {
            string root = FindRepositoryRoot();
            var state = UiComposerService.ReadState(root, "ui.screen.character_test.tactical_deck");
            Require(state.ErrorCount == 0, "Preparation Composer state must satisfy its C# contract.");
            Require(state.BindingSlots.Count == 20 && state.Blueprint.Nodes.Length == 27, "The 20 root identities, six parameter prototypes and existing battle HUD prototype must be retained without a window-close control.");
            Require(!state.Blueprint.Nodes.Any(node => node.Name == "btnClose")
                && !state.BindingSlots.Any(slot => slot.ControlName == "btnClose"),
                "Preparation is a gameplay surface; return and exit belong to the existing flow menu.");
            var plan = UiComposerService.PlanApply(root, state.Screen.ScreenId, PreparationHashes(state));
            Require(!plan.Blocked, "Preparation apply preflight must pass.");
            Require(!XDocument.Parse(plan.Materialization.PanelComponentXmlTemplate).Descendants()
                .Any(node => node.Attribute("name")?.Value == "btnClose"),
                "The controlled preparation source plan must not materialize the retired close control.");
            var sharedFace = plan.Materialization.Components.Single(component => component.ComponentRecipeId == "ui.component.tactical.player_card_face");
            Require(!plan.Materialization.Components.Any(component => component.ComponentRecipeId == "ui.component.tactical.preparation.face"),
                "Preparation must use the existing AI Lab preview face instead of an independent face recipe.");
            var faceXml = XDocument.Parse(sharedFace.XmlTemplate);
            Require(faceXml.Root.Attribute("size").Value == "378,528", "The existing preview's native card proportions must be retained.");
            var faceChildren = faceXml.Root.Element("displayList").Elements().ToArray();
            foreach (string name in new[] { "skinMask", "background", "costPlate", "cost", "name", "icon", "swordIllustration", "qualityDiamond", "description", "behaviorHints", "focus" })
                Require(faceChildren.Any(child => child.Attribute("name")?.Value == name), "Shared preview field missing: " + name);
            Require(plan.Materialization.Resources.Any(resource => resource.ResourceId == "ui.card.imagegen.behavior.base"),
                "The existing authored preview skin must be included in the preparation package.");
            var wrapper = XDocument.Parse(plan.Materialization.Components.Single(component => component.ComponentRecipeId == "ui.component.tactical.preparation.card").XmlTemplate);
            Require(wrapper.Root.Attribute("size").Value == "394,594", "The native face requires its real footprint plus hover padding and binding controls.");
            foreach (string name in new[] { "TacticalApproachActionDetails", "TacticalBasicMovementDetails", "TacticalRetreatDetails", "TacticalOrbitDetails", "TacticalKeepDistanceDetails", "TacticalFacingStepDetails" })
                Require(plan.Materialization.Components.Any(component => component.ComponentName == name), "Shared parameter component missing: " + name);
            var battle = XDocument.Parse(plan.Materialization.Components.Single(component => component.ComponentRecipeId == "ui.component.tactical.battle_hud").XmlTemplate);
            var battleWindows = battle.Root.Element("displayList").Elements("list").Single(node => node.Attribute("name")?.Value == "parameterWindows");
            Require(battleWindows.Attribute("visible")?.Value == "false" && battleWindows.Attribute("overflow")?.Value == "scroll",
                "Battle must reuse the preparation parameter-list layout with scrolling and hidden initial state.");
            var evidenceDirectory = Path.Combine(root, sourceOnly ? ".agent/card-preview-wysiwyg" : ".agent/card-preview-alignment");
            Directory.CreateDirectory(evidenceDirectory);
            File.WriteAllText(Path.Combine(evidenceDirectory, "composer-plan.json"), JsonSerializer.Serialize(plan.Materialization, new JsonSerializerOptions { WriteIndented = true }));
            var buttons = plan.Materialization.Components.Where(component => component.XmlTemplate.Contains("name=\"overBase\"", StringComparison.Ordinal)).ToArray();
            Require(buttons.Length > 0, "Preparation must publish authored three-state buttons.");
            foreach (var component in buttons)
            {
            var button = XDocument.Parse(component.XmlTemplate);
            var images = button.Root.Element("displayList").Elements("image").ToArray();
            Require(images.Length == 3, "Standard menu buttons require actual three-state authored images.");
            Require(images[0].Element("gearDisplay").Attribute("pages").Value == "0"
                && images[1].Element("gearDisplay").Attribute("pages").Value == "2,3"
                && images[2].Element("gearDisplay").Attribute("pages").Value == "1", "Native button image states must match the FairyGUI controller.");
            }
            var legacyState = UiComposerService.ReadState(root, UiComposerService.DemoScreenId);
            var legacy = UiComposerService.PlanApply(root, legacyState.Screen.ScreenId, PreparationHashes(legacyState));
            Require(!legacy.Blocked, "Existing foundation button recipes must remain valid.");
            foreach (var component in legacy.Materialization.Components.Where(component => component.XmlTemplate.Contains("extention=\"Button\"", StringComparison.Ordinal)))
            {
                var xml = XDocument.Parse(component.XmlTemplate);
                Require(xml.Root.Element("displayList").Elements("image").Count() == 1
                    && xml.Root.Element("displayList").Elements("image").Single().Element("gearDisplay") == null,
                    "Recipes without authored over/down retain their existing single-image behavior.");
            }
            var target = FindTarget("character-test-tactical-deck-panel");
            if (!sourceOnly)
            {
                var generated = MxFairyGuiManifestGenerator.Generate(target.Spec, root);
                Require(generated.Success && File.ReadAllText(ResolveGeneratedPath(target)) == generated.SourceText, "Preparation generated manifest must be current.");
                var xml = XDocument.Load(Path.Combine(root, "FGUIProject/assets/MxCharacterTestTacticalDeckUi/Components/TacticalPreparationCard.xml"));
                Require(xml.Root.Attribute("size").Value == "394,594", "Published source must contain the reviewed native wrapper.");
            }
            Console.WriteLine(sourceOnly ? "Preparation source plan passed; Apply/Publish/Unity remain unverified." : "Preparation: native shared card face, parameter windows, root contract, apply preflight and generated manifest passed.");
            return 0;
        }

        private static UiComposerExpectedHashes PreparationHashes(UiComposerAuthoringState state) => new UiComposerExpectedHashes
        {
            BlueprintHash = state.BlueprintHash, RecipeCatalogHash = state.RecipeCatalogHash,
            CatalogHash = state.CatalogHash, ContractHash = state.ContractHash,
            SourceXmlHash = state.SourceXmlHash, MappingHash = state.MappingHash
        };

        private static int WriteTacticalPreparationContract()
        {
            string path = Path.Combine(FindRepositoryRoot(), "ArtSource/UIKits/MxUiFoundation/contracts/character_test_tactical_deck.ui-composer-binding-contract.json");
            JsonObject contract = JsonNode.Parse(File.ReadAllText(path)).AsObject();
            var bindings = new JsonArray();
            string[] graphs = { "background", "deckPanel", "libraryPanel", "popupDismiss" };
            string[] texts = { "search", "emptyHint", "libraryEmpty", "toast" };
            string[] lists = { "deckList", "libraryList", "characterList", "popupList", "parameterWindows" };
            string[] components = { "entityHeader", "drawerHeader", "leftHandle", "topBar", "topHandle", "popupPanel", "toastBackground" };
            foreach (string name in graphs) AddPreparationBinding(bindings, name, "Graph");
            foreach (string name in texts) AddPreparationBinding(bindings, name, "Text");
            foreach (string name in lists) AddPreparationBinding(bindings, name, "List");
            foreach (string name in components) AddPreparationBinding(bindings, name, "ComponentInstance");
            contract["bindings"] = bindings;
            File.WriteAllText(path, contract.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
            Console.WriteLine("Tactical preparation contract generated from reviewed C# control identities.");
            return 0;
        }

        private static void AddPreparationBinding(JsonArray bindings, string name, string kind)
        {
            var binding = new JsonObject { ["slotId"] = name, ["controlName"] = name, ["nodeType"] = kind, ["required"] = true, ["bindPath"] = "", ["commandId"] = "" };
            bindings.Add(binding);
        }
    }
}
