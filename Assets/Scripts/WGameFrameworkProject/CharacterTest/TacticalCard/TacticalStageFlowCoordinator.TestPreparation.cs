namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalStageFlowCoordinator
    {
        private TacticalTestPreparationState _testPreparation;
        internal TacticalTestPreparationState TestPreparation => _testPreparation;

        private TacticalStageRejection ValidateTestPreparation()
        {
            if (!_deck.SupportsTestPreparation) return TacticalStageRejection.UnsupportedPayload;
            if (!_player.IsValid || !_isEntityCurrent(_player)) return TacticalStageRejection.InvalidSpawnBinding;
            return _testPreparation.IsActive ? TacticalStageRejection.PreparationAlreadyActive : TacticalStageRejection.None;
        }

        /// <summary>One paused control transaction. Validation precedes payment; publication cannot invoke external callbacks.</summary>
        private TacticalStageRejection ConfirmTestPreparation(TacticalCardInstance card)
        {
            TacticalStageRejection rejection = ValidateTestPreparation();
            if (rejection != TacticalStageRejection.None) return Reject(rejection);
            uint sourceVersion = card.Version;
            CardPlayFailure failure = _deck.CommitPendingTestPreparation(_ledger);
            if (failure != CardPlayFailure.None) return Reject(Map(failure));
            _testPreparation = new TacticalTestPreparationState(StageRunId, _player, card.InstanceId,
                sourceVersion, SnapshotRevision + 1, LastCompletedFrame, card.Definition.BaseMomentumCost);
            ExecutionIssue = string.Empty;
            Changed();
            return TacticalStageRejection.None;
        }

        private void ClearTestPreparation() { _testPreparation = default; ClearInteractionEffects(); }
    }
}
