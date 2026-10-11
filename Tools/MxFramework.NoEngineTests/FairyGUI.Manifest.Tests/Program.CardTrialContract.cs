using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using MxFramework.Authoring.UiComposer;
using MxFramework.Game.CharacterTest;

namespace MxFramework.NoEngineTests.FairyGUI.Manifest
{
    internal static partial class Program
    {
        private static int WriteAiLabCardTrialContract()
        {
            string path = Path.Combine(FindRepositoryRoot(), "ArtSource/UIKits/MxUiFoundation/contracts/character_test_runtime_ai_lab.ui-composer-binding-contract.json");
            string source = File.ReadAllText(path);
            var contract = JsonNode.Parse(source).AsObject();
            var additions = new JsonArray();
            foreach (string name in new[] { CharacterTestAiLabCardTrialControlContract.Entry, CharacterTestAiLabCardTrialControlContract.Prototype })
            {
                var expected = new JsonObject { ["slotId"] = name, ["controlName"] = name, ["nodeType"] = "ComponentInstance",
                    ["required"] = true, ["bindPath"] = "", ["commandId"] = name == CharacterTestAiLabCardTrialControlContract.Entry
                        ? CharacterTestAiLabCardTrialControlContract.OpenCommand : "" };
                var current = contract["bindings"].AsArray().FirstOrDefault(item => item["slotId"]?.ToString() == name);
                if (current == null) additions.Add(expected);
                else Require(JsonNode.DeepEquals(current, expected), "Trial binding differs from reviewed C# identity: " + name);
            }
            source = InsertTrialContractItems(source, "bindings", additions);
            var components = new JsonArray();
            foreach (var spec in new[] { (CharacterTestAiLabCardTrialControlContract.Recipe, CharacterTestAiLabCardTrialControlContract.RootControls),
                (CharacterTestAiLabCardTrialControlContract.RowRecipe, CharacterTestAiLabCardTrialControlContract.RowControls),
                (CharacterTestAiLabCardTrialControlContract.ChoiceRecipe, CharacterTestAiLabCardTrialControlContract.ChoiceControls),
                (CharacterTestAiLabCardTrialControlContract.CardChoiceRecipe, CharacterTestAiLabCardTrialControlContract.CardChoiceControls),
                (CharacterTestAiLabCardTrialControlContract.HandCardRecipe, CharacterTestAiLabCardTrialControlContract.HandCardControls) })
            {
                var controls = new JsonArray();
                foreach (var control in spec.Item2)
                    controls.Add(new JsonObject { ["slotId"] = control.Name, ["controlName"] = control.Name,
                        ["nodeType"] = control.Kind == "component" || control.Kind == "view" ? "ComponentInstance" : control.Kind == "graph" ? "Graph"
                            : control.Kind == "list" ? "List" : "Text", ["required"] = true, ["bindPath"] = "", ["commandId"] = "" });
                var expected = new JsonObject { ["componentRecipeId"] = spec.Item1, ["bindings"] = controls };
                var current = contract["componentBindings"].AsArray().FirstOrDefault(item => item["componentRecipeId"]?.ToString() == spec.Item1);
                if (current == null) components.Add(expected);
                else if (!JsonNode.DeepEquals(current, expected))
                {
                    using var document = JsonDocument.Parse(source);
                    string prior = document.RootElement.GetProperty("componentBindings").EnumerateArray()
                        .Single(item => item.GetProperty("componentRecipeId").GetString() == spec.Item1).GetRawText();
                    source = source.Replace(prior, expected.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\n", "\n    "));
                }
            }
            source = InsertTrialContractItems(source, "componentBindings", components);
            if (!contract["focusOrder"].AsArray().Any(item => item.ToString() == CharacterTestAiLabCardTrialControlContract.Entry))
                source = InsertTrialContractItems(source, "focusOrder", new JsonArray(CharacterTestAiLabCardTrialControlContract.Entry));
            File.WriteAllText(path, source, new UTF8Encoding(false));
            Console.WriteLine("AI Lab card-trial entry contract generated from reviewed C# identities; existing bindings retained.");
            return 0;
        }

        private static string InsertTrialContractItems(string source, string property, JsonArray values)
        {
            if (values.Count == 0) return source;
            int start = source.IndexOf("\"" + property + "\"", StringComparison.Ordinal);
            int array = source.IndexOf('[', start);
            Require(start >= 0 && array >= 0, "Controlled array missing: " + property);
            string eol = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string insertion = eol + string.Join("," + eol, values.Select(item => string.Join(eol,
                item.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Split('\n').Select(line => "    " + line.TrimEnd('\r'))))) + ",";
            return source.Insert(array + 1, insertion);
        }

        private static int CheckAiLabCardTrialPlan()
        {
            string root = FindRepositoryRoot();
            var state = UiComposerService.ReadState(root, CharacterTestAiLabCardTrialControlContract.Screen);
            Require(state.ErrorCount == 0, "Lab source state: " + JsonSerializer.Serialize(state.Diagnostics));
            var plan = UiComposerService.PlanApply(root, state.Screen.ScreenId, PreparationHashes(state));
            Require(!plan.Blocked, "Lab source plan: " + JsonSerializer.Serialize(plan.Diagnostics));
            var panel = XDocument.Parse(plan.Materialization.PanelComponentXmlTemplate);
            Require(panel.Descendants("component").Any(item => (string)item.Attribute("name") == CharacterTestAiLabCardTrialControlContract.Entry), "Independent entry must materialize in the existing Lab package.");
            Require(panel.Descendants("component").Any(item => (string)item.Attribute("name") == CharacterTestAiLabCardTrialControlContract.Prototype
                && (string)item.Attribute("visible") == "false"), "The trial root must be an exported hidden prototype, opened only by its independent view.");
            var binding = state.BindingSlots.Single(item => item.ControlName == CharacterTestAiLabCardTrialControlContract.Entry);
            Require(binding.CommandId == CharacterTestAiLabCardTrialControlContract.OpenCommand, "Entry must retain the actual runtime command identity.");
            var trial = XDocument.Parse(plan.Materialization.Components.Single(item => item.ComponentRecipeId == CharacterTestAiLabCardTrialControlContract.Recipe).XmlTemplate);
            Require((string)trial.Root.Attribute("size") == "1920,1080", "Independent trial geometry must retain its authored resolution.");
            CheckTrialControls(trial, CharacterTestAiLabCardTrialControlContract.RootControls);
            var row = XDocument.Parse(plan.Materialization.Components.Single(item => item.ComponentRecipeId == CharacterTestAiLabCardTrialControlContract.RowRecipe).XmlTemplate);
            CheckTrialControls(row, CharacterTestAiLabCardTrialControlContract.RowControls);
            var choice = XDocument.Parse(plan.Materialization.Components.Single(item => item.ComponentRecipeId == CharacterTestAiLabCardTrialControlContract.ChoiceRecipe).XmlTemplate);
            CheckTrialControls(choice, CharacterTestAiLabCardTrialControlContract.ChoiceControls);
            var cardChoice = XDocument.Parse(plan.Materialization.Components.Single(item => item.ComponentRecipeId == CharacterTestAiLabCardTrialControlContract.CardChoiceRecipe).XmlTemplate);
            CheckTrialControls(cardChoice, CharacterTestAiLabCardTrialControlContract.CardChoiceControls);
            var handCard = XDocument.Parse(plan.Materialization.Components.Single(item => item.ComponentRecipeId == CharacterTestAiLabCardTrialControlContract.HandCardRecipe).XmlTemplate);
            CheckTrialControls(handCard, CharacterTestAiLabCardTrialControlContract.HandCardControls);
            Require((string)handCard.Root.Attribute("size") == "220,308", "Observation cards must use a compact measured wrapper around the shared native face.");
            Require(!row.Descendants("component").Any(item => new[] { "previousCard", "nextCard", "previousAction", "nextAction" }
                .Contains((string)item.Attribute("name"))), "Configuration must directly select cards and actions, without cycling controls.");
            var setupBackground = trial.Descendants("graph").Single(item => (string)item.Attribute("name") == "configurationBackground");
            Require((string)setupBackground.Attribute("size") == "1920,1080", "Configuration must occupy its independent full page.");
            var hand = trial.Descendants("list").Single(item => (string)item.Attribute("name") == "hand");
            Require((string)hand.Attribute("layout") == "row" && (string)hand.Attribute("overflow") == "scroll"
                && (string)hand.Attribute("scroll") == "horizontal", "Configured hand must use a horizontal scrolling list, with no three-card layout limit.");
            var config = trial.Descendants("list").Single(item => (string)item.Attribute("name") == "initialHand");
            Require((string)config.Attribute("overflow") == "scroll" && (string)config.Attribute("layout") == "row",
                "Initial cards must retain the formal horizontal card presentation.");
            Require(row.Descendants("component").Any(item => (string)item.Attribute("name") == "face"), "Configured cards must contain the formal authored face.");
            Require(trial.Descendants("list").Any(item => (string)item.Attribute("name") == "pickerCards"), "Card selection must use formal card tiles.");
            Require(plan.Materialization.Components.Any(item => item.ComponentRecipeId == "ui.component.runtime_ai_lab.card_trial_portrait"),
                "Roles must include the authored portrait loader component.");
            Require(plan.Materialization.Components.Any(item => item.ComponentRecipeId == "ui.component.tactical.player_card_face"), "Trial must materialize the formal shared card face.");
            Require(plan.Materialization.Components.Any(item => item.ComponentRecipeId == "ui.component.runtime_ai_lab.card_trial_button"), "Trial must materialize the controlled standard button recipe.");
            Console.WriteLine("AI Lab card-trial source plan passed: formal configuration/library/frozen card faces, role portrait components, direct pickers and reviewed C# controls. Apply/Publish/Unity interaction remain unverified.");
            return 0;
        }

        private static void CheckTrialControls(XDocument document, (string Name, string Kind)[] controls)
        {
            var children = document.Root.Element("displayList").Elements().ToArray();
            foreach (var control in controls)
            {
                var node = children.SingleOrDefault(item => (string)item.Attribute("name") == control.Name);
                Require(node != null && node.Name.LocalName == (control.Kind == "input" ? "text" : control.Kind == "view" ? "component" : control.Kind), "Reviewed trial control missing or wrong type: " + control.Name);
                if (control.Kind == "input") Require((string)node.Attribute("input") == "true", "Reviewed trial input must be an authored input: " + control.Name);
            }
        }
    }
}
