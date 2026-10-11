namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestTacticalAuthoringPreview _cachedTacticalPreview;
        private bool _hasTacticalPreviewCache;
        internal long TacticalPreviewBuildCount { get; private set; }
        internal long TacticalPreviewCacheHits { get; private set; }
        private (object Workspace, int WorkspaceRevision, object Card, int CardRevision,
            object Slice, object Frozen, long PresentationRevision, long References,
            int Team, int Slot, int Character, int Agent, int Session, bool Historical, long Content, long Actor) _tacticalPreviewCacheKey;

        private (object Workspace, int WorkspaceRevision, object Card, int CardRevision,
            object Slice, object Frozen, long PresentationRevision, long References,
            int Team, int Slot, int Character, int Agent, int Session, bool Historical, long Content, long Actor) CaptureTacticalPreviewCacheKey(
                CharacterTestRuntimeAiLabBehaviorCardDraft card, CharacterTestTacticalFrozenSource frozen)
            => (DraftWorkspace, DraftWorkspace?.CurrentRevision ?? -1, card, card?.CurrentRevision ?? -1,
                _context?.Slice, frozen, TacticalPresentationRevision, _behaviorCardReferences?.StableHash ?? 0L,
                _selectedSetupTeamId, _selectedSetupSlotIndex,
                _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0,
                ResolveSelectedAgentEntityId(), _timelineSessionGeneration, IsHistoricalFrameSelected, CharacterTestTacticalPreparationCatalog.Revision, _tacticalActorRevision);
    }
}
