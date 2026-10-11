using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MxFramework.Game.CharacterTest;

namespace MxFramework.NoEngineTests.FairyGUI.Manifest
{
    internal static partial class Program
    {
        private static int WriteTacticalPreviewContract()
        {
            string path = Path.Combine(FindRepositoryRoot(), "ArtSource/UIKits/MxUiFoundation/contracts/character_test_runtime_ai_lab.ui-composer-binding-contract.json");
            string original = File.ReadAllText(path);
            using var document = JsonDocument.Parse(original);
            var entries = document.RootElement.GetProperty("componentBindings").EnumerateArray()
                .Where(e => e.GetProperty("componentRecipeId").GetString() == CharacterTestTacticalPreviewControlContract.Recipe).ToArray();
            if (entries.Length > 1) throw new InvalidDataException("Duplicate preview component contract.");
            JsonObject component = entries.Length == 1 ? JsonNode.Parse(entries[0].GetRawText()).AsObject()
                : new JsonObject { ["componentRecipeId"] = CharacterTestTacticalPreviewControlContract.Recipe, ["bindings"] = new JsonArray() };
            var bindings = component["bindings"].AsArray();
            AddPreviewBinding(bindings, CharacterTestTacticalPreviewControlContract.ExpandSlot, CharacterTestTacticalPreviewControlContract.Expand, "ComponentInstance");
            AddPreviewBinding(bindings, CharacterTestTacticalPreviewControlContract.DividerSlot, CharacterTestTacticalPreviewControlContract.Divider, "Graph");
            AddPreviewBinding(bindings, "tactical_import", CharacterTestTacticalPreviewControlContract.Import, "ComponentInstance");
            AddPreviewBinding(bindings, "tactical_prepare", CharacterTestTacticalPreviewControlContract.Prepare, "ComponentInstance");
            AddPreviewBinding(bindings, "tactical_requery", CharacterTestTacticalPreviewControlContract.Requery, "ComponentInstance");
            AddPreviewBinding(bindings, "tactical_blank", CharacterTestTacticalPreviewControlContract.Blank, "ComponentInstance");
            AddPreviewBinding(bindings, "tactical_template", CharacterTestTacticalPreviewControlContract.Template, "ComponentInstance");
            AddPreviewBinding(bindings, "tactical_duplicate", CharacterTestTacticalPreviewControlContract.Duplicate, "ComponentInstance");
            string generated = component.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            string updated;
            if (entries.Length == 1) updated = original.Replace(entries[0].GetRawText(), generated);
            else
            {
                const string marker = "\"componentBindings\": [";
                int index = original.IndexOf(marker, StringComparison.Ordinal);
                if (index < 0) throw new InvalidDataException("Component binding array is unavailable.");
                updated = original.Insert(index + marker.Length, Environment.NewLine + generated + ",");
            }
            if (updated != original) File.WriteAllText(path, updated, new UTF8Encoding(false));
            WriteTacticalCompositionContract(path);
            Console.WriteLine("Tactical preview component contract generated from C# control identities.");
            return 0;
        }

        private static void WriteTacticalCompositionContract(string path)
        {
            string original = File.ReadAllText(path);
            var root = JsonNode.Parse(original).AsObject();
            var bindings = root["bindings"].AsArray();
            AddPreviewBinding(bindings, CharacterTestTacticalPreviewControlContract.CompositionPanel,
                CharacterTestTacticalPreviewControlContract.CompositionPanel, "ComponentInstance");
            AddPreviewBinding(bindings, CharacterTestTacticalPreviewControlContract.ActivationInspector,
                CharacterTestTacticalPreviewControlContract.ActivationInspector, "ComponentInstance");
            foreach (string label in new[] { CharacterTestTacticalPreviewControlContract.BindingLabel,
                CharacterTestTacticalPreviewControlContract.BindingValue, CharacterTestTacticalPreviewControlContract.RequiredText })
                AddPreviewBinding(bindings, label, label, "Text");
            var components = root["componentBindings"].AsArray();
            void Component(string recipe, Action<JsonArray> fill)
            {
                var found = components.OfType<JsonObject>().SingleOrDefault(c => (string)c["componentRecipeId"] == recipe);
                if (found == null) { found = new JsonObject { ["componentRecipeId"] = recipe, ["bindings"] = new JsonArray() }; components.Add(found); }
                fill(found["bindings"].AsArray());
            }
            Component(CharacterTestTacticalPreviewControlContract.CompositionRecipe, b => {
                foreach (string button in CharacterTestTacticalPreviewControlContract.CompositionButtons) AddPreviewBinding(b, button, button, "ComponentInstance");
                foreach (string list in new[] { "compositionList", "choiceList" }) AddPreviewBinding(b, list, list, "List");
                foreach (string label in new[] { "compositionTitle", "sharedImpactText" }) AddPreviewBinding(b, label, label, "Text");
            });
            Component(CharacterTestTacticalPreviewControlContract.CompositionNodeRecipe, b => {
                AddPreviewBinding(b, "title", "title", "Text"); AddPreviewBinding(b, "detail", "detail", "Text");
            });
            Component("ui.component.runtime_ai.behavior_recipe_inspector", b => {
                foreach (string field in new[] { "inputLocalFrame", "inputDurationFrames", "inputPriority", "inputOperationParameter" })
                    AddPreviewBinding(b, field + "Frame", field + "Frame", "Graph");
            });
            using var old = JsonDocument.Parse(original);
            string updated = original.Replace(old.RootElement.GetProperty("bindings").GetRawText(), bindings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }))
                .Replace(old.RootElement.GetProperty("componentBindings").GetRawText(), components.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            if (updated != original) File.WriteAllText(path, updated, new UTF8Encoding(false));
        }

        private static void AddPreviewBinding(JsonArray bindings, string slot, string control, string type)
        {
            var prior = bindings.OfType<JsonObject>().Where(b => (string)b["slotId"] == slot).ToArray();
            if (prior.Length > 1) throw new InvalidDataException("Duplicate binding slot: " + slot);
            var binding = new JsonObject { ["slotId"] = slot, ["controlName"] = control, ["nodeType"] = type,
                ["required"] = true, ["bindPath"] = "", ["commandId"] = "" };
            if (prior.Length == 1) bindings[bindings.IndexOf(prior[0])] = binding;
            else bindings.Add(binding);
        }
    }
}

