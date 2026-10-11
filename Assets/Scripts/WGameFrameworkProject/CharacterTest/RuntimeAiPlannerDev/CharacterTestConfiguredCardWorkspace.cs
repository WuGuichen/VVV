using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabDraftWorkspace
    {
        internal CharacterTestRuntimeAiLabBehaviorCardDraft AddConfiguredTestCard(CharacterTestConfiguredCard record)
        {
            var snapshot = CharacterTestTacticalPublishedSource.Read(record.Source);
            var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(Guid.Parse(record.Id).ToString("D"), string.Empty, record.Name, "Tactical", null);
            record.Metadata.Apply(card);
            if (snapshot.IsLegacy) card.SetTacticalSource(snapshot.CloneLegacy());
            else
            {
                var source = snapshot.CloneComposition();
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var definition in snapshot.CloneDefinitions())
                {
                    string original = definition.DefinitionId;
                    string prefix = "test." + record.Id + ".";
                    string id = original.StartsWith(prefix, StringComparison.Ordinal) ? original
                        : prefix + CharacterTestActionBrowserCatalog.Hash(Encoding.UTF8.GetBytes(original)).Substring(0, 20);
                    map.Add(original, id); definition.DefinitionId = id;
                    AddLoadedTacticalDefinition(definition);
                }
                foreach (var node in CharacterTestTacticalCompositionEdits.Nodes(source.Root))
                    if (node.Kind == "Reference") node.DefinitionId = map[node.DefinitionId];
                card.SetCompositionSource(source);
            }
            _cards[card.CardDraftId] = card; TouchWorkspace(); return card;
        }
    }
}
