using System;
using System.Linq;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalMixedPlayerCardRuntime
    {
        internal CharacterTestTacticalExecutionAdapter PausedAdapter => _adapter;
        internal UnityTacticalTimeAdapter PausedWorldTime => _worldTime;

        internal void ValidatePausedRestoreBoundary()
        {
            SessionSnapshotCheck.Require(_bound && !_disposed && !PlayerDefeated && !OutcomeReady && _player != null && _target != null &&
                _player.CombatState.IsAlive && _target.CombatState.IsAlive && !_worldTime.IsTransitioning && _worldTime.IsSimulationPaused &&
                Stage.Phase == TacticalStagePhase.Planning && Stage.Time.Phase == TacticalTurnPhase.ReadyPaused &&
                _adapter != null && _adapter.CurrentExecutionId == 0 && !_session.IsExecuting && !_session.HasPendingActionCommand && Stage.Deck.ResolvingAction == null &&
                Stage.Deck.PendingExecutionInput.Kind == TacticalExecutionInputKind.None, "mixed-ready-paused-required");
            _adapter.ValidateIdleExecutionBoundary();
            CharacterTestPausedSessionCodec.ValidateDeployment(_deployment);
            SessionSnapshotCheck.Require(Stage.Deck.InstanceEvaluation != null &&
                ReferenceEquals(Stage.Deck.InstanceEvaluation.Target, _adapter), "actual-adapter-binding-required");
        }

        // Rebuild the actual production adapter against this new runtime, its commands, clock and restored ledger.
        internal void RestorePausedMixedState(CharacterTestPausedSessionDocument document)
        {
            SessionSnapshotCheck.Require(!_bound && _adapter == null && _worldTime.IsDetached, "fresh-mixed-owner-required");
            _player = _modules.CharacterRuntime.FindEntityById(document.PlayerRuntime);
            _target = _modules.CharacterRuntime.FindEntityById(document.TargetRuntime);
            SessionSnapshotCheck.Require(_player != null && _target != null && _player.Identity.CharacterId == _participant.CharacterId &&
                _player.Team.TeamId == _participant.TeamId && _player.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer &&
                _target.Team.TeamId != _player.Team.TeamId, "mixed-player-target-join");
            var ledger = (MomentumLedger)Stage.Ledger;
            ledger.RestoreSessionState(document.Motion.Single(m => m.Actor == _player.GameplayEntityId.Index &&
                m.Generation == _player.GameplayEntityId.Generation).Ledger);
            Stage.Deck.RestoreSessionState(document.Deck, _participant.Cards.ToArray(), ledger,
                () => _modules.Clock.CurrentFrame.Value, document.CompletedFrame);
            Stage.RestorePlanningSessionState(document.Stage, document.CompletedFrame);
            Commands.RestoreSessionState(document.Controls);
            var measurement = CharacterTestTacticalSummaryPreparation.StandardMeasurement();
            var binding = new CharacterTestTacticalCardBinding(new CharacterTestTacticalCardRuntimeInputs(
                _modules.CharacterRuntime, _player.Id.Value, new CharacterActionTacticalMetricsStore(), measurement));
            var points = new CharacterTestTacticalPointNavigation(_modules.CharacterRuntime, _player, _modules.NavigationSession);
            _session = new CharacterTestTacticalCardSession(_modules.CharacterRuntime, _player.Id.Value,
                binding, _modules.RuntimeCommands.Enqueue, points);
            _adapter = new CharacterTestTacticalExecutionAdapter(_modules.CharacterRuntime, _modules.RuntimeCommands,
                _session, null, _player, _worldTime, targetResolver: ResolveTarget,
                cooldownTracker: Stage.Deck.CooldownTracker, deployment: _deployment,
                publishedCatalogLoader: _deployment.CardTrial == null ? null : _ => _deployment.CardTrial.PublishedCatalog);
            _adapter.ConfigurePointInput(points, card => new TacticalExecutionInputBinding(Stage.StageRunId, Stage.TurnIndex,
                card.InstanceId, card.Version, _player.GameplayEntityId));
            _adapter.ConfigureRecallStage(Stage.StageRunId);
            _adapter.SetContext(null, _modules.Clock.CurrentFrame);
            _adapter.RestoreIdleExecutionIdentity(document.ExecutionIdentity, Stage.Deck, document.CompletedFrame);
            Stage.Deck.DefinitionEvaluation = _adapter.EvaluateCardAvailability;
            Stage.Deck.InstanceEvaluation = _adapter.EvaluateCardAvailability;
            _modules.CharacterRuntime.World.BindTacticalMotion(_player.Id.Value, Stage.Ledger);
            _bound = true;
            ValidatePausedRestoreBoundary();
        }
    }
}
