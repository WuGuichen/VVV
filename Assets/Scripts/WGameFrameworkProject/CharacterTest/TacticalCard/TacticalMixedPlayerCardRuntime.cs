using System;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Player hand/time owner for a mixed player-plus-AI deployment. Battle outcome remains external.</summary>
    internal sealed partial class TacticalMixedPlayerCardRuntime : RuntimeModule, ITacticalExecutionPort, ITacticalExecutionInputPort, ITacticalPointSelectionPort, ITacticalBehindPreviewPort, ITacticalExecutionRetirementPort, ITacticalFixedExecutionPort, ITacticalFixedFeeReadoutPort, ITacticalFixedCleanupPort, ITacticalFixedTerminalPort
    {
        private readonly CharacterTestSliceModules _modules;
        private readonly TacticalDeployment _deployment;
        private readonly TacticalDeploymentParticipant _participant;
        private readonly UnityTacticalTimeAdapter _worldTime;
        private CharacterTestTacticalExecutionAdapter _adapter;
        private CharacterTestCharacterEntity _player;
        private CharacterTestCharacterEntity _target;
        private CharacterTestTacticalCardSession _session;
        private bool _bound, _disposed;

        internal TacticalMixedPlayerCardRuntime(CharacterTestSliceModules modules,
            CharacterTestTacticalStageAuthoringDocument config, TacticalDeployment deployment, ITacticalPointProvider pointProvider = null, bool detachedRestore = false, ulong stageRunId = 1)
            : base("CharacterTest.TacticalCard.MixedPlayer", RuntimeTickStage.PreSimulation, 0)
        {
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
            _participant = deployment.PlayerParticipant
                ?? throw new ArgumentException("Mixed player runtime requires one player participant.", nameof(deployment));
            _worldTime = new UnityTacticalTimeAdapter(config.NormalScale, 1f, 1f, detachedRestore);
            _worldTime.SetSimulationPaused(false);
            var time = new TacticalTimeCoordinator(_worldTime,
                new TacticalTurnPauseDetector());
            var trial = deployment.CardTrial;
            var deck = new TacticalDeckManager(trial?.HandCapacity ?? config.HandCapacity, enableTestPreparation: trial != null);
            for (int i = 0; i < _participant.Cards.Count; i++)
                if (_participant.Cards[i] is TacticalActionCardDefinition action && action.PointMove != null)
                {
                    deck.BindCooldownTracker(new TacticalCooldownTracker(), _participant.CharacterId,
                        () => _modules.Clock.CurrentFrame.Value);
                    break;
                }
            var ledger = new MomentumLedger();
            var driver = new TacticalSingleCardDriver(this, time, deck, ledger,
                config.PreActionBudgetTicks, config.ExecutionBudgetTicks);
            Stage = new TacticalStageFlowCoordinator(trial?.StageRunId ?? stageRunId, time, deck, ledger, driver,
                _participant.Cards, trial?.Seed ?? 42, IsEntityCurrent,
                new TacticalStoryStepBinding(CharacterTestStoryIds.BootstrapGraph,
                    CharacterTestStoryIds.Steps.OpenStartupUi, CharacterTestStoryIds.Actions.OpenStartupUi),
                config.IngressBudgetTicks, Array.Empty<TacticalStoryStepBinding>(),
                requestStoryRestart: null, config.StoryTransferBudgetTicks,
                initialMomentum: deployment.InitialMotion, pointProvider: pointProvider,
                allowConfigurableHandCapacity: trial != null, configuredInitialHandCount: trial?.InitialHandCount ?? 0);
            Commands = new TacticalCommandPump(Stage);
        }

        internal TacticalStageFlowCoordinator Stage { get; }
        internal TacticalCommandPump Commands { get; }
        internal CharacterTestCharacterEntity Player => _player;
        internal CharacterTestCharacterEntity CurrentTarget => ResolveTarget();
        internal bool PlayerDefeated { get; private set; }
        internal bool OutcomeReady { get; private set; }
        internal int WinningTeamId { get; private set; }
        internal bool IsDraw { get; private set; }
        internal bool CanStep => Stage.HasReleasedCleanup ? Stage.CanStep : !OutcomeReady && (PlayerDefeated || Stage.CanStep);

        internal void DrainControls()
        {
            if (_disposed || PlayerDefeated) return;
            if (Stage.Phase == TacticalStagePhase.Booting
                || Stage.Phase == TacticalStagePhase.AwaitingIngressSettle)
                _worldTime.SetSimulationPaused(false);
            _adapter?.SetContext(null, _modules.Clock.CurrentFrame);
            Commands.Drain();
            while (Commands.TryReadReceipt(out _)) { }
        }

        internal void TickWorldTimeTransition(float unscaledDeltaTime)
        {
            if (_disposed) return;
            _worldTime.TickTransition(unscaledDeltaTime);
            Stage.UpdatePauseTransition();
        }

        public override void Tick(RuntimeTickContext context)
        {
            if (_disposed) return;
            _adapter?.SetContext(null, new RuntimeFrame(context.FrameIndex));
            if (Stage.HasReleasedCleanup) { Stage.ReconcileReleasedCleanup(); return; }
            if (PlayerDefeated || OutcomeReady) return;
            if (Stage.Health == TacticalStageHealth.Running) _adapter?.Tick();
        }

        internal void AfterWorldTick(RuntimeFrame frame)
        {
            if (_disposed) return;
            Stage.ReconcileReleasedCleanup();
            if (OutcomeReady) { _worldTime.SetSimulationPaused(Stage.HasReleasedCleanup ? Stage.Time.IsWorldPaused : true); return; }
            if (_bound)
            {
                int team = 0; bool multiple = false;
                foreach (var entity in _modules.CharacterRuntime.Entities)
                {
                    if (!entity.CombatState.IsAlive) continue;
                    if (team == 0) team = entity.Team.TeamId;
                    else if (entity.Team.TeamId != team) { multiple = true; break; }
                }
                if (!multiple)
                {
                    WinningTeamId = team; IsDraw = team == 0; OutcomeReady = true;
                    Stage.CompleteExternalOutcome(frame.Value); _adapter?.ClearRecallStage();
                    _worldTime.SetSimulationPaused(Stage.HasReleasedCleanup ? Stage.Time.IsWorldPaused : true); return;
                }
            }
            if (PlayerDefeated) return;
            if (!_bound)
            {
                TryBind(frame);
                return;
            }
            if (!_player.CombatState.IsAlive)
            {
                Stage.ReleaseControl();
                _adapter?.DisposePointInput();
                _adapter?.ClearRecallStage();
                PlayerDefeated = true;
                _worldTime.SetSimulationPaused(false);
                return;
            }
            CharacterTestCharacterEntity target = ResolveTarget();
            _adapter.SetPhysicsSample(frame.Value, null, default,
                hasPendingImpulse: _player.PendingMotionRequestCount != 0,
                hasStableSupport: _player.IsGrounded);
            SettleSample playerSample = _adapter.BuildSettleSample(frame.Value, _player);
            SettleSample targetSample = Stage.Phase == TacticalStagePhase.Booting
                || Stage.Phase == TacticalStagePhase.AwaitingIngressSettle
                ? playerSample
                : target != null ? BuildTargetSample(frame.Value, target) : playerSample;
            Stage.OnCompletedSimulationTick(frame.Value, in playerSample, in targetSample);
            if (Stage.Phase == TacticalStagePhase.StageCompleted || Stage.Health != TacticalStageHealth.Running) _adapter?.ClearRecallStage();
        }

        private void TryBind(RuntimeFrame frame)
        {
            if (_modules.CharacterRuntime.Entities.Count != _deployment.Participants.Count) return;
            int participantIndex = -1;
            for (int i = 0; i < _deployment.Participants.Count; i++)
                if (ReferenceEquals(_deployment.Participants[i], _participant)) { participantIndex = i; break; }
            if (participantIndex < 0) return;
            for (int i = 0; i < _modules.CharacterRuntime.Entities.Count; i++)
            {
                CharacterTestCharacterEntity candidate = _modules.CharacterRuntime.Entities[i];
                if (candidate.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer
                    && candidate.Identity.CharacterId == _participant.CharacterId
                    && candidate.Team.TeamId == _participant.TeamId)
                { _player = candidate; break; }
            }
            _target = ResolveNearestHostile();
            if (_player == null || _target == null) return;

            var opening = new TacticalStoryStepToken(Stage.StageRunId,
                CharacterTestStoryIds.BootstrapGraph, 1,
                CharacterTestStoryIds.Steps.OpenStartupUi, CharacterTestStoryIds.Actions.OpenStartupUi);
            Stage.OnStoryStepStarted(in opening);
            Stage.OnSpawnBound(Stage.StageRunId, 0, _player.GameplayEntityId, frame.Value);
            Stage.OnSpawnBound(Stage.StageRunId, 1, _target.GameplayEntityId, frame.Value);
            _worldTime.SetSimulationPaused(false);
            var measurement = CharacterTestTacticalSummaryPreparation.StandardMeasurement();
            var store = new CharacterActionTacticalMetricsStore();
            var binding = new CharacterTestTacticalCardBinding(
                new CharacterTestTacticalCardRuntimeInputs(_modules.CharacterRuntime,
                    _player.Id.Value, store, measurement));
            var points = new CharacterTestTacticalPointNavigation(_modules.CharacterRuntime, _player, _modules.NavigationSession);
            _session = new CharacterTestTacticalCardSession(_modules.CharacterRuntime,
                _player.Id.Value, binding, _modules.RuntimeCommands.Enqueue, points);
            _adapter = new CharacterTestTacticalExecutionAdapter(_modules.CharacterRuntime,
                _modules.RuntimeCommands, _session, null, _player, _worldTime,
                targetResolver: ResolveTarget, cooldownTracker: Stage.Deck.CooldownTracker, deployment: _deployment,
                publishedCatalogLoader: _deployment.CardTrial == null ? null : _ => _deployment.CardTrial.PublishedCatalog);
            _adapter.ConfigurePointInput(points, card => new TacticalExecutionInputBinding(Stage.StageRunId, Stage.TurnIndex,
                card.InstanceId, card.Version, _player.GameplayEntityId));
            _adapter.ConfigureRecallStage(Stage.StageRunId);
            Stage.Deck.DefinitionEvaluation = _adapter.EvaluateCardAvailability;
            Stage.Deck.InstanceEvaluation = _adapter.EvaluateCardAvailability;
            _modules.CharacterRuntime.World.BindTacticalMotion(_player.Id.Value, Stage.Ledger);
            Stage.ConfigureInteractionEffects(_modules.CharacterRuntime, () => _player, ResolveTarget);
            _bound = true;
            if (!Stage.TryInitializeMixedRosterPlanning())
                Stage.ReportFault(TacticalStageRejection.InvalidSpawnBinding);
        }

        private CharacterTestCharacterEntity ResolveTarget()
        {
            if (_player == null) return null;
            if (_target != null && _target.CombatState.IsAlive && _target.Team.TeamId != _player.Team.TeamId)
                return _target;
            return _target = ResolveNearestHostile();
        }

        private CharacterTestCharacterEntity ResolveNearestHostile()
        {
            if (_player == null) return null;
            CharacterTestCharacterEntity best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < _modules.CharacterRuntime.Entities.Count; i++)
            {
                CharacterTestCharacterEntity candidate = _modules.CharacterRuntime.Entities[i];
                if (!candidate.CombatState.IsAlive || candidate.Team.TeamId == _player.Team.TeamId) continue;
                float dx = candidate.Transform.X - _player.Transform.X;
                float dz = candidate.Transform.Z - _player.Transform.Z;
                float distance = dx * dx + dz * dz;
                if (distance > bestDistance || (Math.Abs(distance - bestDistance) < 0.0001f
                    && best != null && candidate.Id.Value >= best.Id.Value)) continue;
                best = candidate; bestDistance = distance;
            }
            return best;
        }

        private static SettleSample BuildTargetSample(long frame, CharacterTestCharacterEntity target)
        {
            bool idle = target.Action.State != CharacterTestActionStateId.Running && !target.Input.HasActiveMove;
            return new SettleSample(frame, false, true, idle, target.CombatState.IsReacting,
                !target.IsGrounded, target.PendingMotionRequestCount != 0, !target.Input.HasActiveMove,
                true, target.IsGrounded, false,
                new UnityEngine.Vector3(target.HorizontalVelocity.x, target.VerticalVelocity,
                    target.HorizontalVelocity.y).magnitude,
                0f, noActionStarted: target.Action.State != CharacterTestActionStateId.Running);
        }

        public bool TryCommitFixed(TacticalCardInstance card, TacticalDeckManager deck, TacticalTimeCoordinator time,
            MomentumLedger ledger, in TacticalExecutionInput input, out ulong executionId,
            out TacticalFixedCardPaymentCommit payment, out string issue)
        {
            executionId = 0; payment = null; issue = "execution:ActorNotBound";
            return _adapter != null && _adapter.TryCommitFixed(card, deck, time, ledger, input, out executionId, out payment, out issue);
        }
        public bool TryReadFixedFees(TacticalCardInstance card, out TacticalFixedCardFeePlan fees, out string issue)
        {
            fees = null; issue = "fixed-fee:ActorNotBound";
            return _adapter != null && _adapter.TryReadFixedFees(card, out fees, out issue);
        }
        public void SetFixedReleaseObserver(ulong executionId, Action onActorRetired)
            => _adapter?.SetFixedReleaseObserver(executionId, onActorRetired);
        public ExecutionObservation ReconcileFixedRelease(ulong executionId)
            => _adapter?.ReconcileFixedRelease(executionId) ?? default;
        public void FinishFixedRelease(ulong executionId) => _adapter?.FinishFixedRelease(executionId);
        public void CancelFixed(ulong executionId) => _adapter?.CancelFixed(executionId);
        public bool IsNaturalFixedCompletion(ulong executionId) => _adapter?.IsNaturalFixedCompletion(executionId) == true;

        public bool TryPrepare(TacticalCardInstance card, out string issue)
        {
            if (_adapter != null) return _adapter.TryPrepare(card, out issue);
            issue = "execution:ActorNotBound"; return false;
        }
        public bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue)
        {
            if (_adapter != null) return _adapter.TryAdmit(card, out executionId, out issue);
            executionId = 0; issue = "execution:ActorNotBound"; return false;
        }
        public TacticalExecutionInputKind GetRequiredInputKind(TacticalCardInstance card) => _adapter?.GetRequiredInputKind(card) ?? TacticalExecutionInputKind.None;
        public TacticalInputStatus ValidateInput(TacticalCardInstance card, in TacticalExecutionInput input) => _adapter == null
            ? TacticalInputStatus.CharacterUnavailable : _adapter.ValidateInput(card, input);
        public bool TryPrepare(TacticalCardInstance card, in TacticalExecutionInput input, out string issue)
        { issue = "execution:ActorNotBound"; return _adapter != null && _adapter.TryPrepare(card, input, out issue); }
        public bool TryAdmit(TacticalCardInstance card, in TacticalExecutionInput input, out ulong executionId, out string issue)
        { executionId = 0; issue = "execution:ActorNotBound"; return _adapter != null && _adapter.TryAdmit(card, input, out executionId, out issue); }
        public bool TryGetPointOrigin(out TacticalWorldPoint origin)
        { origin = default; return _adapter != null && _adapter.TryGetPointOrigin(out origin); }
        public TacticalInputStatus TryPickGround(TacticalWorldPoint rayOrigin, TacticalWorldPoint rayDirection, out TacticalWorldPoint point)
        { point = default; return _adapter == null ? TacticalInputStatus.GroundUnavailable : _adapter.TryPickGround(rayOrigin, rayDirection, out point); }
        public ExecutionObservation Observe(ulong executionId) => _adapter?.Observe(executionId) ?? default;
        public void RetireSettledExecution(ulong executionId, in ExecutionObservation observation, long completedFrame)
            => _adapter.RetireSettledExecution(executionId, in observation, completedFrame);
        public bool TryPrepareBehindPreview(TacticalCardInstance card, out CharacterTestBehaviorExecutionMovement movement, out string issue)
        {
            movement = default; issue = "preview:ActorNotBound";
            return _adapter != null && _adapter.TryPrepareBehindPreview(card, out movement, out issue);
        }
        public bool TrySampleBehindPreview(TacticalCardInstance card, in CharacterTestBehaviorExecutionMovement movement,
            out TacticalWorldPoint origin, out CharacterTestRuntimeAiLabMovementTargetSample sample)
        {
            origin = default; sample = default;
            return _adapter != null && _adapter.TrySampleBehindPreview(card, movement, out origin, out sample);
        }
        public bool TryAbortPreAction(ulong executionId, string reason) =>
            _adapter != null && _adapter.TryAbortPreAction(executionId, reason);

        private bool IsEntityCurrent(GameplayEntityId id) =>
            _modules.CharacterRuntime.World.GameplayComponentWorld.IsAlive(id);
        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stage.ReleaseControl();
            _adapter?.DisposePointInput();
            _worldTime.Dispose();
        }
    }
}
