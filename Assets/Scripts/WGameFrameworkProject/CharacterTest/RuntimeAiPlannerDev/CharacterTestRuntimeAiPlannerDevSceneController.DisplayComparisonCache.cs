using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private readonly Dictionary<(object Group, int Entry), (bool Changed, string Diagnostic)> _displayCardComparisons =
            new Dictionary<(object, int), (bool, string)>();
        private readonly Dictionary<object, (bool Changed, string Diagnostic)> _displayGroupComparisons =
            new Dictionary<object, (bool, string)>();
        private (object Workspace, int Revision, object Catalog, long Cards, long Groups, long References, long Presentation) _displayComparisonContext;

        private void EnsureDisplayComparisonContext()
        {
            EnsureBehaviorCardReferences();
            var state = ProjectCatalogState;
            var key = ((object)DraftWorkspace, DraftWorkspace?.CurrentRevision ?? -1, (object)state,
                state?.CardCatalog?.CatalogHash ?? 0L, state?.GroupCatalog?.CatalogHash ?? 0L,
                _behaviorCardReferences?.StableHash ?? 0L, TacticalPresentationRevision);
            if (key.Equals(_displayComparisonContext)) return;
            _displayComparisonContext = key;
            _displayCardComparisons.Clear(); _displayGroupComparisons.Clear();
        }

        // UI status only. Save/test/publish continue to call the uncached comparison methods.
        internal bool HasUnpublishedBehaviorCardContentForDisplay(CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            int entry, out string diagnostic)
        {
            EnsureDisplayComparisonContext();
            var key = ((object)group, entry);
            if (!_displayCardComparisons.TryGetValue(key, out var result))
            {
                bool changed = HasUnpublishedBehaviorCardContent(group, entry, out string issue);
                result = (changed, issue); _displayCardComparisons[key] = result;
            }
            diagnostic = result.Diagnostic; return result.Changed;
        }

        internal bool HasUnpublishedBehaviorGroupContentForDisplay(CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            out string diagnostic)
        {
            if (group == null) return HasUnpublishedBehaviorGroupContent(group, out diagnostic);
            EnsureDisplayComparisonContext();
            if (!_displayGroupComparisons.TryGetValue(group, out var result))
            {
                bool changed = HasUnpublishedBehaviorGroupContent(group, out string issue);
                result = (changed, issue); _displayGroupComparisons[group] = result;
            }
            diagnostic = result.Diagnostic; return result.Changed;
        }
    }
}
