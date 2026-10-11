using System;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalStageFlowCoordinator
    {
        internal TacticalInteractionEffectAuthority InteractionEffects => _interactionActor?.Invoke()?.InteractionEffects;
        private CharacterTestCharacterRuntimeModule _interactionRuntime;
        private Func<CharacterTestCharacterEntity> _interactionActor, _interactionTarget;
        internal void ConfigureInteractionEffects(CharacterTestCharacterRuntimeModule runtime,
            Func<CharacterTestCharacterEntity> actor, Func<CharacterTestCharacterEntity> target)
        { _interactionRuntime = runtime; _interactionActor = actor; _interactionTarget = target; SyncInteractionTurn(); }
        private void SyncInteractionTurn()
        {
            var actor = _interactionActor?.Invoke();
            if (actor != null && actor.GameplayEntityId == _player && actor.CombatState.IsAlive && TurnIndex != 0)
                actor.InteractionEffects.AdvanceTurn(StageRunId, TurnIndex, _player);
        }
        private void ClearInteractionEffects()
        {
            var actor = _interactionActor?.Invoke();
            if (actor?.InteractionEffects?.Stage == StageRunId) actor.InteractionEffects.Clear();
        }
        private TacticalStageRejection ValidatePreparation(TacticalCardInstance card)
        {
            if (!InteractionPreparationPayload.Matches(card.Definition)) return ValidateTestPreparation();
            return ValidateInteractionPreparation(card, out _, out _, out _, out _);
        }
        private TacticalStageRejection ValidateInteractionPreparation(TacticalCardInstance card, out TacticalInteractionEffectAuthority authority,
            out TacticalInteractionKind kind, out MxFramework.Gameplay.GameplayEntityId targetId, out TacticalWorldPoint point)
        {
            authority = null; kind = default; targetId = default; point = default;
            if (card.Version == uint.MaxValue) return TacticalStageRejection.StaleSnapshot;
            var actor = _interactionActor?.Invoke(); var target = _interactionTarget?.Invoke();
            if (actor == null || _interactionRuntime == null || actor.GameplayEntityId != _player
                || !actor.CombatState.IsAlive || !_isEntityCurrent(_player)) return TacticalStageRejection.InvalidSpawnBinding;
            kind = ((InteractionPreparationPayload)((TacticalUtilityCardDefinition)card.Definition).Payload).Effect;
            authority = actor.InteractionEffects;
            if (authority.Stage != StageRunId || authority.Turn != TurnIndex) return TacticalStageRejection.WrongTurn;
            if (target != null && target.CombatState.IsAlive && target.Team.TeamId != actor.Team.TeamId && _isEntityCurrent(target.GameplayEntityId)) targetId = target.GameplayEntityId;
            point = new TacticalWorldPoint(actor.Transform.XMillimeters, actor.Transform.YMillimeters, actor.Transform.ZMillimeters);
            bool placementValid = kind != TacticalInteractionKind.ReturnMark || (actor.IsGrounded && actor.BattleState.CanStartNormalAction
                && actor.Action.State != CharacterTestActionStateId.Running && !actor.GroundPointTraction.IsActive && actor.PendingMotionRequestCount == 0
                && new CharacterTestTacticalPointNavigation(_interactionRuntime, actor, null).TryValidateGroundPlacement(actor.GameplayEntityId, point, out _));
            var failure = authority.Validate(kind, LastCompletedFrame, targetId, placementValid, defenseAvailable: _interactionRuntime.World.TryGetScatterPressure(actor, out var scatter) && scatter.HasValidState());
            if (failure == TacticalInteractionFailure.None) return TacticalStageRejection.None;
            ExecutionIssue = "interaction-preparation:" + failure;
            return failure == TacticalInteractionFailure.AlreadyUsedThisTurn ? TacticalStageRejection.PreparationAlreadyActive : TacticalStageRejection.UnsupportedPayload;
        }
        private TacticalStageRejection ConfirmInteractionPreparation(TacticalCardInstance card)
        {
            var rejection = ValidateInteractionPreparation(card, out var authority, out var kind, out var target, out var point);
            if (rejection != TacticalStageRejection.None) return Reject(rejection);
            uint sourceVersion = card.Version;
            var failure = _deck.CommitPendingTestPreparation(_ledger);
            if (failure != CardPlayFailure.None) return Reject(Map(failure));
            authority.ActivateValidated(kind, LastCompletedFrame, target, point.X, point.Y, point.Z, card.InstanceId, sourceVersion);
            ExecutionIssue = string.Empty; Changed(); return TacticalStageRejection.None;
        }
    }
}
