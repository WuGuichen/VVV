using System;
using System.Linq;
using MxFramework.Editor.Levels;
namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestSceneCharacterLevelEditorModule
    {
        internal bool RevealLevelIssue(MxLevelEditorWindow window, string message)
        {
            EnsureLoaded(); RebuildRows();
            string id = CharacterTestLevelDiagnosticLocation.ActorId(message, _document);
            var row = _rows.FirstOrDefault(r => r.Id == id);
            if (row == null) return false;
            string field = "mx-level-behavior-group";
            var actor = _document.Characters[row.Index];
            var catalog = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (actor.BehaviorBinding != null && catalog.Success && catalog.GroupCatalog.TryGetExact(
                    actor.BehaviorBinding.GroupId, actor.BehaviorBinding.GroupRevision, out var group))
                foreach (var card in group.Cards.Where(c => c.Enabled))
                    if (catalog.CardCatalog.TryGetExact(card.StableCardId, card.CardRevision, out var published) && published.TacticalSource != null)
                        foreach (var slot in CharacterTestLevelBehaviorCompiler.Slots(published.TacticalSource))
                            if (message.Contains("/" + card.EntryId + "/" + slot, StringComparison.Ordinal))
                                field = "mx-level-slot-" + card.EntryId + "-" + slot;
            return window.RevealDocumentRow(ModuleId, "document", row.Key, field);
        }
    }
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        internal bool RevealLevelIssue(MxLevelEditorWindow window, string message)
        {
            EnsureLoaded(); RebuildRows();
            var row = _rows.FirstOrDefault(r => !string.IsNullOrEmpty(ResolveRowPath(r)) && message.Contains(ResolveRowPath(r), StringComparison.Ordinal));
            if (row == null) return false;
            return window.RevealDocumentRow(ModuleId, "document", row.Key);
        }
    }
}
