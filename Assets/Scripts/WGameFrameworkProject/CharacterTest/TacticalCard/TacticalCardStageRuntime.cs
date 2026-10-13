using System;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>One GameSlice's tactical composition. All world work stays on its original RuntimeHost.</summary>
    internal sealed partial class TacticalCardStageRuntime : RuntimeModule, ITacticalExecutionInputPort, ITacticalPointSelectionPort, ITacticalBehindPreviewPort, ITacticalExecutionRetirementPort, ITacticalFixedExecutionPort, ITacticalFixedFeeReadoutPort, ITacticalFixedCleanupPort, ITacticalFixedTerminalPort, IStoryRuntimeCommandResultSink
    {
        private readonly CharacterTestSliceModules _modules;
        private readonly CharacterTestTacticalStageAuthoringDocument _config;
        private readonly TacticalDeployment _deployment;
        private readonly UnityTacticalTimeAdapter _worldTime;
        private readonly TacticalStoryBridge _story;
        private readonly IDisposable _storySubscription;
        private RuntimeHostContext _hostContext;
        private CharacterTestTacticalExecutionAdapter _adapter;
        private CharacterTestCharacterEntity _player, _opponent;
        private CharacterTestTacticalCardSession _session;
        private bool _disposed, _restartRequested;
        private int _previousOpponentYaw;
        private long _opponentPoseFrame = -1;

        internal TacticalStageFlowCoordinator Stage { get; }
        internal TacticalCommandPump Commands { get; }
        internal CharacterTestTacticalExecutionAdapter Adapter => _adapter;
        internal CharacterTestCharacterEntity Player => _player;
        internal CharacterTestCharacterEntity Opponent => _opponent;
        internal bool RestartReady { get; private set; }
        internal SettleSample LastPlayerSample { get; private set; }
        internal SettleSample LastOpponentSample { get; private set; }
        internal TacticalCommandReceipt LastReceipt { get; private set; }
        internal int ReceiptCount { get; private set; }

        internal TacticalCardStageRuntime(CharacterTestSliceModules modules,
            CharacterTestTacticalStageAuthoringDocument config, ulong runId, TacticalDeployment deployment, ITacticalPointProvider pointProvider = null)
            : base("CharacterTest.TacticalCard", RuntimeTickStage.PreSimulation, 0)
        {
            _modules = modules;
            _deployment = deployment ?? throw new ArgumentNullException(nameof(deployment), "Confirm deployment before creating a battle.");
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (config.InitialMomentum != 50 || config.HandCapacity != 3 || config.RequiredStableTicks != 3 ||
                config.SimultaneousOutcomePolicy != "DefeatWinsSameFrame" || config.StageResetPolicy != "ResetSessionAndDeck")
                throw new ArgumentException("Unsupported tactical stage composition contract.", nameof(config));
            _worldTime = new UnityTacticalTimeAdapter(config.NormalScale, 1f, 1f);
            _worldTime.SetSimulationPaused(false);
            var time = new TacticalTimeCoordinator(_worldTime, new TacticalTurnPauseDetector());
            var deck = new TacticalDeckManager(config.HandCapacity);
            for (int i = 0; i < deployment.Cards.Count; i++)
            {
                if (!(deployment.Cards[i] is TacticalActionCardDefinition action) || action.PointMove == null) continue;
                // Explicit point deployments use the existing cooldown owner; ordinary deployments retain their current wiring.
                deck.BindCooldownTracker(new TacticalCooldownTracker(), deployment.PlayerCharacterId, () => _modules.Clock.CurrentFrame.Value);
                break;
            }
            var ledger = new MomentumLedger();
            var driver = new TacticalSingleCardDriver(this, time, deck, ledger,
                preActionBudgetTicks: config.PreActionBudgetTicks, executionBudgetTicks: config.ExecutionBudgetTicks);
            Stage = new TacticalStageFlowCoordinator(runId, time, deck, ledger, driver, deployment.Cards, 42, IsEntityCurrent,
                new TacticalStoryStepBinding(CharacterTestStoryIds.BootstrapGraph, CharacterTestStoryIds.Steps.OpenStartupUi,
                    CharacterTestStoryIds.Actions.OpenStartupUi), config.IngressBudgetTicks,
                new[]
                {
                    new TacticalStoryStepBinding(CharacterTestStoryIds.BootstrapGraph, CharacterTestStoryIds.Steps.TacticalBattleVictory, CharacterTestStoryIds.Actions.TacticalBattleVictory),
                    new TacticalStoryStepBinding(CharacterTestStoryIds.BootstrapGraph, CharacterTestStoryIds.Steps.TacticalBattleDefeat, CharacterTestStoryIds.Actions.TacticalBattleDefeat)
                }, RequestRestart, config.StoryTransferBudgetTicks, initialMomentum: deployment.InitialMotion, pointProvider: pointProvider);
            Commands = new TacticalCommandPump(Stage);
            _story = new TacticalStoryBridge(modules.StoryDirector, Stage);
            _storySubscription = modules.StoryDirector.SubscribeEvents(OnStoryEvent);
        }

        internal void BindStoryFlow()
        {
            _modules.StoryFlowBridge.BindTacticalBattle(Stage.StageRunId, CharacterTestStoryIds.BootstrapGraph,
                CanLeaveBattle, RequestRestart, RequestRestart, Stage.BeginStoryRestart);
        }

        public override void Initialize(RuntimeHostContext context) => _hostContext = context;

        /// <summary>NoAlloc. Called by unscaled input owner even while the world is paused.</summary>
        internal void DrainControls()
        {
            _adapter?.SetContext(null, _modules.Clock.CurrentFrame);
            Commands.Drain();
            while (Commands.TryReadReceipt(out var receipt)) { LastReceipt = receipt; ReceiptCount++; }
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
            _outbox?.Tick(context);
            _modules.RuntimeAiPlannerCommands?.SetBootstrapDecisionsSuppressed(true);
            _adapter?.SetContext(null, new RuntimeFrame(context.FrameIndex));
            if (Stage.HasReleasedCleanup) { Stage.ReconcileReleasedCleanup(); return; }
            if (Stage.Health == TacticalStageHealth.Running) _adapter?.Tick();
        }

        /// <summary>After the sole Host has completed all combat, physics and presentation modules.</summary>
        internal void AfterWorldTick(RuntimeFrame frame, double delta, double elapsed)
        {
            if (_disposed) return;
            Stage.ReconcileReleasedCleanup();
            _story.Drain();
            if (_player == null && Stage.Health == TacticalStageHealth.Running && _modules.CharacterRuntime.Entities.Count == 2)
            {
                try { BindSpawnedRoster(frame); }
                catch (Exception error) when (error is InvalidOperationException || error is ArgumentException || error is System.IO.IOException)
                { Stage.ReportExecutionFault(error.Message); }
            }
            if (_triggerPipeline != null)
            {
                var world = _modules.CharacterRuntime.World.GameplayComponentWorld;
                var context = new GameplaySystemContext(frame, delta, elapsed, _triggerWorld, Array.Empty<RuntimeCommand>(),
                    world.Events, _triggerCommandState, world);
                _triggerPipeline.Tick(context);
            }
            if (_adapter != null)
            {
                _adapter.SetPhysicsSample(frame.Value, null, default, hasPendingImpulse: _player.PendingMotionRequestCount != 0,
                    hasStableSupport: _player.IsGrounded);
                var sample = _adapter.BuildSettleSample(frame.Value, _player);
                LastPlayerSample = Stage.Phase == TacticalStagePhase.AwaitingIngressSettle
                    ? IngressSample(in sample, _player) : sample;
                float angularSpeed = _opponentPoseFrame + 1 == frame.Value
                    ? Mathf.Abs(CharacterTestDeterministicMath.ShortestAngleDelta(_previousOpponentYaw, _opponent.Transform.YawMilliDegrees)) * (Mathf.Deg2Rad * 60f / 1000f)
                    : float.PositiveInfinity;
                _opponentPoseFrame = frame.Value; _previousOpponentYaw = _opponent.Transform.YawMilliDegrees;
                LastOpponentSample = new SettleSample(frame.Value, false, true,
                    _opponent.Action.State != CharacterTestActionStateId.Running && !_opponent.Input.HasActiveMove,
                    _opponent.CombatState.IsReacting, !_opponent.IsGrounded, _opponent.PendingMotionRequestCount != 0,
                    !_opponent.Input.HasActiveMove, _modules.RuntimeCommands.PendingCommandCount == 0,
                    _opponent.IsGrounded, false, new Vector3(_opponent.HorizontalVelocity.x, _opponent.VerticalVelocity, _opponent.HorizontalVelocity.y).magnitude,
                    angularSpeed, noActionStarted: _opponent.Action.State != CharacterTestActionStateId.Running);
            }
            var player = LastPlayerSample; var opponent = LastOpponentSample;
            Stage.OnCompletedSimulationTick(frame.Value, in player, in opponent);
            if (Stage.Phase == TacticalStagePhase.StageCompleted || Stage.Health != TacticalStageHealth.Running) _adapter?.ClearRecallStage();
        }

        private SettleSample IngressSample(in SettleSample sample, CharacterTestCharacterEntity entity)
            => new SettleSample(sample.SimulationTick, false, true, sample.NeutralPoseReached, sample.HasReactionOrRecovery,
                sample.HasPendingLanding, sample.HasPendingMotionOrImpulse, !entity.Input.HasActiveMove,
                _modules.RuntimeCommands.PendingCommandCount == 0, sample.HasStableSupport, sample.HasNewDestabilizingContact,
                sample.RelativeLinearSpeed, sample.RelativeAngularSpeed, noActionStarted: entity.Action.State != CharacterTestActionStateId.Running);

        private void BindSpawnedRoster(RuntimeFrame frame)
        {
            var entities = _modules.CharacterRuntime.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer) _player = entities[i];
                else if (entities[i].Control.Owner == CharacterTestCharacterControlOwner.AiControlled) _opponent = entities[i];
            }
            if (_player == null || _opponent == null || !_modules.CharacterRuntime.TryGetLastCompletedRuntimeFrame(out var completed) || completed != frame)
            { Stage.ReportFault(TacticalStageRejection.InvalidSpawnBinding); return; }
            Stage.OnSpawnBound(Stage.StageRunId, 0, _player.GameplayEntityId, frame.Value);
            Stage.OnSpawnBound(Stage.StageRunId, 1, _opponent.GameplayEntityId, frame.Value);
            BindOutcomeTriggers();
            if (_player.Identity.CharacterId != _deployment.PlayerCharacterId || _opponent.Identity.CharacterId != _deployment.OpponentCharacterId)
                throw new InvalidOperationException("出战角色与确认配置不一致。");
            var measurement = CharacterTestTacticalSummaryPreparation.StandardMeasurement();
            var store = new CharacterActionTacticalMetricsStore();
            var binding = new CharacterTestTacticalCardBinding(new CharacterTestTacticalCardRuntimeInputs(_modules.CharacterRuntime, _player.Id.Value, store, measurement));
            var points = new CharacterTestTacticalPointNavigation(_modules.CharacterRuntime, _player, _modules.NavigationSession);
            _session = new CharacterTestTacticalCardSession(_modules.CharacterRuntime, _player.Id.Value, binding, _modules.RuntimeCommands.Enqueue, points);
            _adapter = new CharacterTestTacticalExecutionAdapter(_modules.CharacterRuntime, _modules.RuntimeCommands, _session,
                null, _player, _worldTime, targetResolver: ResolveOpponent, cooldownTracker: Stage.Deck.CooldownTracker, deployment: _deployment);
            _adapter.ConfigurePointInput(points, card => new TacticalExecutionInputBinding(Stage.StageRunId, Stage.TurnIndex,
                card.InstanceId, card.Version, _player.GameplayEntityId));
            _adapter.ConfigureRecallStage(Stage.StageRunId);
            Stage.Deck.DefinitionEvaluation = _adapter.EvaluateCardAvailability;
            Stage.Deck.InstanceEvaluation = _adapter.EvaluateCardAvailability;
            _modules.CharacterRuntime.World.BindTacticalMotion(_player.Id.Value, Stage.Ledger);
            Stage.ConfigureInteractionEffects(_modules.CharacterRuntime, () => _player, ResolveOpponent);
        }

        private CharacterTestCharacterEntity ResolveOpponent() => _opponent;
        private bool IsEntityCurrent(GameplayEntityId id) => _modules.CharacterRuntime != null && _modules.CharacterRuntime.World.GameplayComponentWorld.IsAlive(id);
        private bool CanLeaveBattle() => _end == null || _end.CanLeaveSession;
        internal bool CanReturnToPreparation => !_disposed && CanLeaveBattle();
        private bool RequestRestart(ulong run, long frame)
        {
            if (_disposed || _restartRequested || run != Stage.StageRunId || !CanLeaveBattle()) return false;
            var result = _modules.StoryModule.CommandBuffer.Enqueue(StoryRuntimeCommandFactory.RaiseTrigger(new RuntimeFrame(frame),
                StoryRuntimeCommandSources.System, CharacterTestStoryIds.Triggers.LevelStartRegion));
            if (!result.Success) return false;
            _restartRequested = true;
            return true;
        }

        private void OnStoryEvent(StoryEvent evt)
        {
            if (_restartRequested && evt.Kind == StoryEventKind.StepStarted && evt.GraphId == CharacterTestStoryIds.BootstrapGraph &&
                evt.StepId == CharacterTestStoryIds.Steps.LevelStartRegionLine) RestartReady = true;
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
            issue = "execution:ActorNotBound";
            return _adapter != null && _adapter.TryPrepare(card, out issue);
        }
        public bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue)
        {
            executionId = 0; issue = "execution:ActorNotBound";
            return _adapter != null && _adapter.TryAdmit(card, out executionId, out issue);
        }
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
        public bool TryAbortPreAction(ulong executionId, string reason) => _adapter != null && _adapter.TryAbortPreAction(executionId, reason);
        public void Publish(in StoryRuntimeCommandResult result) { _end?.Publish(in result); }

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _adapter?.DisposePointInput();
            _modules.RuntimeAiPlannerCommands?.SetBootstrapDecisionsSuppressed(false);
            _story.Dispose(); _storySubscription.Dispose(); _end?.Dispose(); _outbox?.Dispose();
            Stage.ReleaseControl(); _worldTime.Dispose();
        }
    }
}
