using System;
using System.Collections.Generic;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>One StageRun's lifecycle. Collaborators are fresh, exclusive, synchronous and non-reentrant.</summary>
    internal sealed partial class TacticalStageFlowCoordinator : ITacticalStageFlowCoordinator
    {
        private readonly TacticalTimeCoordinator _time;
        private readonly TacticalDeckManager _deck;
        private readonly IMomentumLedger _ledger;
        private readonly TacticalSingleCardDriver _driver;
        private readonly ITacticalPointProvider _pointProvider;
        private bool _preparingAutomaticInput;
        private readonly Func<GameplayEntityId, bool> _isEntityCurrent;
        private readonly Func<ulong, long, bool> _requestStoryRestart;
        private TacticalCardDefinition[] _definitions;
        private readonly TacticalStoryStepBinding _openingStep;
        private readonly TacticalStoryStepBinding[] _completionSteps;
        private readonly ISettleDetector _playerIngressDetector;
        private readonly ISettleDetector _opponentIngressDetector;
        private readonly int _seed, _ingressBudgetTicks;
        private readonly int _configuredInitialHandCount;
        // Baseline tactical resource for this StageRun. RebindDeck replaces it with the deployed
        // hero's loadout value; the constructor argument is only the stage-level fallback.
        private int _initialMomentum;
        private GameplayEntityId _player, _opponent;
        private long _playerBoundFrame, _opponentBoundFrame;
        private int _openingBeatInstance, _ingressElapsedTicks;
        private bool _initialized, _completionPending, _released;
        private bool _awaitingWorldPause;
        private TacticalStageEndTriggerBridge _endTriggers;
        private readonly int _storyTransferBudgetTicks;
        private long _storyTransferStartFrame = -1;
        internal bool IsLeavingSession { get; private set; }

        public ulong StageRunId { get; }
        public TacticalStagePhase Phase { get; private set; } = TacticalStagePhase.Booting;
        public TacticalStageHealth Health { get; private set; } = TacticalStageHealth.Running;
        public TacticalStageRejection LastRejection { get; private set; }
        public TacticalStageRejection Fault { get; private set; }
        public string ExecutionIssue { get; private set; } = string.Empty;
        public ulong SnapshotRevision { get; private set; } = 1;
        public ulong TurnIndex => _time.CurrentTurnIndex;
        public long LastCompletedFrame { get; private set; } = -1;
        public int IngressElapsedTicks => _ingressElapsedTicks;
        public bool CanStep => !_time.IsWorldPaused;
        public TacticalDeckManager Deck => _deck;
        public IMomentumLedger Ledger => _ledger;
        internal int InitialMomentum => _initialMomentum;
        public TacticalTimeCoordinator Time => _time;
        public TacticalSingleCardDriver Driver => _driver;
        internal bool CanRequestRecovery => !_released && Health == TacticalStageHealth.SuspendedByFault
            && _time.HasPauseReason(TacticalPauseReason.RecoveryTimeout)
            && !_time.HasPauseReason(TacticalPauseReason.ExecutionFault)
            && !_time.HasPauseReason(TacticalPauseReason.BattleEnded);
        /// <summary>Read-only presentation query including this stage's atomic Action replacement policy.</summary>
        /// <remarks>Uses the existing component-store query allocation contract; adds no query storage.</remarks>
        public CardPlayEvaluation EvaluateHandCard(TacticalCardInstance card)
        {
            int reservationCredit=_deck.PendingAction!=null && card?.Zone==CardZone.Hand?_ledger.Reserved:0;
            var availability = card == null ? default : _deck.EvaluateAvailability(card,reservationCredit);
            if(card!=null && availability.IsLegal && _deck.IsCardOnCooldown(card))
                availability=new CardPlayEvaluation(CardPlayFailure.CardOnCooldown,card.Definition.BaseMomentumCost,_ledger.Spendable);
            if (card != null && !availability.IsLegal)
                return new CardPlayEvaluation(availability.Failure, Math.Max(card.Definition.BaseMomentumCost,availability.EffectiveCost), _ledger.Spendable,
                    availability.BlockingActionId, availability.RemainingCooldownFrames, availability.SimulationTickRate);
            if (_released || Health != TacticalStageHealth.Running || Phase != TacticalStagePhase.Planning)
                return new CardPlayEvaluation(CardPlayFailure.WrongPhase, card?.Definition.BaseMomentumCost ?? 0, _ledger.Spendable);
            if (card != null && InteractionPreparationPayload.IsPreparation(card.Definition) && ValidatePreparation(card) != TacticalStageRejection.None)
                return new CardPlayEvaluation(CardPlayFailure.UnsupportedPayload, card.Definition.BaseMomentumCost, _ledger.Spendable);
            if (card != null && card.Zone == CardZone.Hand && _deck.Hand.Contains(card) &&
                (card.Definition is TacticalActionCardDefinition || InteractionPreparationPayload.IsPreparation(card.Definition)) && _deck.PendingAction != null)
                return new CardPlayEvaluation(card.Definition.BaseMomentumCost <= _ledger.Current
                    ? CardPlayFailure.None : CardPlayFailure.InsufficientMomentum,
                    Math.Max(card.Definition.BaseMomentumCost,availability.EffectiveCost), _ledger.Spendable);
            return _deck.EvaluateCard(card, _ledger);
        }
        internal bool IsReleased => _released;
        internal bool HasAutomaticPointProvider => _pointProvider?.Capability == TacticalPointProviderCapability.WorldPoint;
        internal TacticalInputStatus TryBuildAutomaticPointInput(ITacticalPointProvider provider, out TacticalExecutionInput input)
        {
            input = default;
            if (_released || Health != TacticalStageHealth.Running || Phase != TacticalStagePhase.Planning || _deck.PendingAction == null)
                return TacticalInputStatus.PointUnavailable;
            var card = _deck.PendingAction;
            var binding = new TacticalExecutionInputBinding(StageRunId, TurnIndex, card.InstanceId, card.Version, _player);
            return _driver.TryBuildAutomaticPointInput(provider, binding, out input);
        }
        internal bool TryGetRoster(out GameplayEntityId player, out GameplayEntityId opponent)
        { player = _player; opponent = _opponent; return !_released && player.IsValid && opponent.IsValid; }

        internal void BindEndTriggers(TacticalStageEndTriggerBridge bridge)
        {
            if (_endTriggers != null) throw new InvalidOperationException("A StageRun already owns its critical Trigger bridge.");
            _endTriggers = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        /// <summary>Called only after reliable activation acceptance or a formal restart request.</summary>
        internal void BeginStoryTransfer(long frame)
        {
            if (_released || Health == TacticalStageHealth.AwaitingStoryTransfer) return;
            ClearTestPreparation();
            _storyTransferStartFrame = frame;
            Health = TacticalStageHealth.AwaitingStoryTransfer;
            _time.BeginStoryTransfer();
            Changed();
        }

        /// <summary>Accepted Story cleanup/rebuild request; old critical faults must not pause its command consumers again.</summary>
        internal void BeginStoryRestart(long frame)
        {
            if (_released) return;
            IsLeavingSession = true;
            _completionPending = false;
            BeginStoryTransfer(frame);
        }

        /// <summary>Detach this session's gate without modifying cards, physics, or the menu owner.</summary>
        internal void ReleaseControl()
        {
            if (_released) return;
            ClearTestPreparation();
            _released = true;
            _driver.ReleaseControl();
            if (_deck.PendingAction != null) _deck.CancelPendingAction(_ledger);
            _completionPending = false;
            Health = TacticalStageHealth.AwaitingStoryTransfer;
            _time.BeginStoryTransfer();
        }

        internal void CompleteExternalOutcome(long completedFrame)
        {
            ClearTestPreparation();
            LastCompletedFrame = completedFrame;
            _completionPending = false;
            Phase = TacticalStagePhase.StageCompleted;
            Health = TacticalStageHealth.Running;
            _time.ForceSuspend(TacticalPauseReason.BattleEnded);
            Changed();
        }

        /// <summary>Cold setup only; neither deals cards nor pauses the ingress world.</summary>
        public TacticalStageFlowCoordinator(ulong stageRunId, TacticalTimeCoordinator time,
            TacticalDeckManager deck, IMomentumLedger ledger, TacticalSingleCardDriver driver,
            IReadOnlyList<TacticalCardDefinition> definitions, int seed,
            Func<GameplayEntityId, bool> isEntityCurrent, TacticalStoryStepBinding openingStep,
            int ingressBudgetTicks = 300, IReadOnlyList<TacticalStoryStepBinding> completionSteps = null,
            Func<ulong, long, bool> requestStoryRestart = null, int storyTransferBudgetTicks = 60,
            int initialMomentum = 50, ITacticalPointProvider pointProvider = null, bool allowConfigurableHandCapacity = false,
            int configuredInitialHandCount = 0)
        {
            if (stageRunId == 0) throw new ArgumentOutOfRangeException(nameof(stageRunId));
            if (ingressBudgetTicks <= 0) throw new ArgumentOutOfRangeException(nameof(ingressBudgetTicks));
            if (storyTransferBudgetTicks <= 0) throw new ArgumentOutOfRangeException(nameof(storyTransferBudgetTicks));
            _storyTransferBudgetTicks = storyTransferBudgetTicks;
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _deck = deck ?? throw new ArgumentNullException(nameof(deck));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _pointProvider = pointProvider;
            _isEntityCurrent = isEntityCurrent ?? throw new ArgumentNullException(nameof(isEntityCurrent));
            if (!_driver.Uses(_time, _deck, _ledger))
                throw new ArgumentException("Driver must use this session's time, deck and ledger.", nameof(driver));
            if (_time.CurrentTurnIndex != 0 || (!allowConfigurableHandCapacity && _deck.HandCapacity != 3) || _deck.Hand.Count != 0 ||
                _deck.DrawPile.Count != 0 || _deck.DiscardPile.Count != 0 || _deck.ExhaustPile.Count != 0 ||
                _deck.PendingAction != null || _deck.ResolvingAction != null || _ledger.Reserved != 0)
                throw new ArgumentException("A stage requires fresh collaborators and its configured hand capacity.");
            if (definitions == null || definitions.Count == 0)
                throw new ArgumentException("Confirm a nonempty deck before creating the battle.", nameof(definitions));
            if (configuredInitialHandCount < 0 || configuredInitialHandCount > _deck.HandCapacity ||
                configuredInitialHandCount > definitions.Count || (!allowConfigurableHandCapacity && configuredInitialHandCount != 0))
                throw new ArgumentOutOfRangeException(nameof(configuredInitialHandCount));
            _configuredInitialHandCount = configuredInitialHandCount;
            if (openingStep.GraphId <= 0) throw new ArgumentException("An opening Story binding is required.", nameof(openingStep));
            _definitions = new TacticalCardDefinition[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
                _definitions[i] = definitions[i] ?? throw new ArgumentException("Null card definition.", nameof(definitions));
            _completionSteps = new TacticalStoryStepBinding[completionSteps?.Count ?? 0];
            for (int i = 0; i < _completionSteps.Length; i++)
            {
                var binding = completionSteps[i];
                if (binding.GraphId != openingStep.GraphId || binding.StepId == openingStep.StepId)
                    throw new ArgumentException("Completion bindings must belong to this graph and differ from ingress.", nameof(completionSteps));
                _completionSteps[i] = binding;
            }
            StageRunId = stageRunId; _openingStep = openingStep; _seed = seed;
            _ingressBudgetTicks = ingressBudgetTicks; _requestStoryRestart = requestStoryRestart;
            _initialMomentum = ClampMomentum(initialMomentum);
            var config = new SettleDetectorConfig(requiredStableTicks: 3);
            _playerIngressDetector = new SettleDetector(config);
            _opponentIngressDetector = new SettleDetector(config);
        }

        internal bool RecognizesStoryStep(in TacticalStoryStepToken step)
        {
            if (_openingStep.Matches(in step)) return true;
            for (int i = 0; i < _completionSteps.Length; i++)
                if (_completionSteps[i].Matches(in step)) return true;
            return false;
        }

        /// <remarks>NoAlloc. The bridge drains copied events outside Director callbacks; never spawns here.</remarks>
        public void OnStoryStepStarted(in TacticalStoryStepToken step)
        {
            if (step.StageRunId != StageRunId) { Reject(TacticalStageRejection.WrongStage); return; }
            if (_released) { Reject(TacticalStageRejection.WrongPhase); return; }
            if (IsLeavingSession) { Reject(TacticalStageRejection.CriticalFlowPending); return; }
            if (!RecognizesStoryStep(in step)) { Reject(TacticalStageRejection.WrongStoryStep); return; }
            if (_openingStep.Matches(in step))
            {
                if (_openingBeatInstance != 0)
                {
                    Reject(step.BeatInstanceId == _openingBeatInstance
                        ? TacticalStageRejection.DuplicateStoryStep : TacticalStageRejection.WrongStoryStep);
                    return;
                }
                if (Phase != TacticalStagePhase.Booting || Health != TacticalStageHealth.Running)
                { Reject(TacticalStageRejection.WrongPhase); return; }
                _openingBeatInstance = step.BeatInstanceId;
                Changed();
                return;
            }
            if (_openingBeatInstance == 0 || step.BeatInstanceId < _openingBeatInstance)
            { Reject(TacticalStageRejection.WrongStoryStep); return; }
            if (_endTriggers != null && !_endTriggers.AcceptsCompletion(in step))
            { Reject(TacticalStageRejection.WrongStoryStep); return; }
            if (Phase == TacticalStagePhase.StageCompleted || _completionPending)
            { Reject(TacticalStageRejection.DuplicateStoryStep); return; }
            // A Story terminal is authoritative, but the freeze still waits for a full world boundary.
            _completionPending = true;
            BeginStoryTransfer(LastCompletedFrame);
            Changed();
        }

        /// <remarks>NoAlloc. Receipt source must be the original Spawn consumer after all bindings completed.</remarks>
        public void OnSpawnBound(ulong stageRunId, int slotIndex, GameplayEntityId entityId, long completedFrame)
        {
            if (stageRunId != StageRunId) { Reject(TacticalStageRejection.WrongStage); return; }
            if (_released) { Reject(TacticalStageRejection.WrongPhase); return; }
            if (_openingBeatInstance == 0 || Phase > TacticalStagePhase.AwaitingIngressSettle || Health != TacticalStageHealth.Running)
            { Reject(TacticalStageRejection.WrongPhase); return; }
            if (completedFrame < 0 || completedFrame < LastCompletedFrame)
            { Reject(TacticalStageRejection.StaleFrame); return; }
            if (slotIndex < 0 || slotIndex > 1 || !entityId.IsValid || !_isEntityCurrent(entityId))
            { ReportFault(TacticalStageRejection.InvalidSpawnBinding); return; }
            GameplayEntityId previous = slotIndex == 0 ? _player : _opponent;
            GameplayEntityId other = slotIndex == 0 ? _opponent : _player;
            if ((previous.IsValid && previous != entityId) || other == entityId)
            { ReportFault(TacticalStageRejection.InvalidSpawnBinding); return; }
            if (previous == entityId) { Reject(TacticalStageRejection.None); return; }
            if (slotIndex == 0) { _player = entityId; _playerBoundFrame = completedFrame; }
            else { _opponent = entityId; _opponentBoundFrame = completedFrame; }
            if (_player.IsValid && _opponent.IsValid)
            {
                Phase = TacticalStagePhase.AwaitingIngressSettle;
                _ingressElapsedTicks = 0;
                ResetIngress();
            }
            Changed();
        }

        /// <remarks>NoAlloc except the single initial deck construction. Supply complete, same-frame authority facts.</remarks>
        public void OnCompletedSimulationTick(long completedFrame, in SettleSample player, in SettleSample opponent)
        {
            if (_released || Phase == TacticalStagePhase.StageCompleted) return;
            if (completedFrame < 0 || completedFrame <= LastCompletedFrame)
            { ResetIngress(); _driver.ResetSettleWindow(); Reject(TacticalStageRejection.StaleFrame); return; }
            // Critical authority wins the fatal third stable tick, before any normal CompleteTurn/deal.
            bool completedWhileRunning = CanStep;
            _endTriggers?.ObserveCompletedTick(completedFrame);
            if (completedWhileRunning && Health == TacticalStageHealth.SuspendedByFault)
                LastCompletedFrame = completedFrame;
            if (_completionPending)
            {
                LastCompletedFrame = completedFrame;
                _completionPending = false;
                Phase = TacticalStagePhase.StageCompleted;
                Health = TacticalStageHealth.Running;
                _time.ForceSuspend(TacticalPauseReason.BattleEnded);
                Changed();
                return;
            }
            if (Health == TacticalStageHealth.AwaitingStoryTransfer)
            {
                if (!CanStep) return;
                LastCompletedFrame = completedFrame;
                if (_storyTransferStartFrame >= 0 && completedFrame - _storyTransferStartFrame >= _storyTransferBudgetTicks)
                    ReportFault(TacticalStageRejection.StoryTransferTimeout);
                return;
            }
            if (Health != TacticalStageHealth.Running || !CanStep) return;
            long elapsed = LastCompletedFrame < 0 ? 1 : completedFrame - LastCompletedFrame;
            LastCompletedFrame = completedFrame;
            if (_openingBeatInstance == 0) return;
            if ((_player.IsValid && !_isEntityCurrent(_player)) || (_opponent.IsValid && !_isEntityCurrent(_opponent)))
            { ReportFault(TacticalStageRejection.InvalidSpawnBinding); return; }
            if (Phase == TacticalStagePhase.Booting || Phase == TacticalStagePhase.AwaitingIngressSettle)
            {
                _ingressElapsedTicks = elapsed >= int.MaxValue - _ingressElapsedTicks
                    ? int.MaxValue : _ingressElapsedTicks + (int)elapsed;
                bool settled = false;
                if (Phase == TacticalStagePhase.AwaitingIngressSettle)
                {
                    bool valid = player.SimulationTick == completedFrame && opponent.SimulationTick == completedFrame &&
                        completedFrame >= _playerBoundFrame && completedFrame >= _opponentBoundFrame &&
                        player.NoActionStarted && opponent.NoActionStarted &&
                        !player.ActionTimelineCompleted && !opponent.ActionTimelineCompleted;
                    if (valid)
                    {
                        bool playerSettled = _playerIngressDetector.Sample(in player);
                        bool opponentSettled = _opponentIngressDetector.Sample(in opponent);
                        settled = playerSettled && opponentSettled;
                    }
                    else { ResetIngress(); Reject(TacticalStageRejection.StaleFrame); }
                }
                if (settled && _ingressElapsedTicks <= _ingressBudgetTicks) InitializePlanning();
                else if (_ingressElapsedTicks >= _ingressBudgetTicks)
                    ReportFault(Phase == TacticalStagePhase.Booting
                        ? TacticalStageRejection.SpawnTimeout : TacticalStageRejection.IngressTimeout);
                return;
            }
            if (Phase != TacticalStagePhase.Executing && Phase != TacticalStagePhase.TurnSettling) return;
            if (player.SimulationTick != completedFrame)
            { ReportFault(TacticalStageRejection.StaleFrame); return; }
            if (_driver.OnCompletedSimulationTick(in player))
            {
                ExecutionIssue = _driver.LastIssue;
                BeginPlanningPause();
                if (!string.IsNullOrEmpty(ExecutionIssue)) LastRejection = TacticalStageRejection.ExecutionRejected;
            }
            else if (_time.Phase == TacticalTurnPhase.Suspended)
            {
                ExecutionIssue = _driver.LastIssue;
                Health = TacticalStageHealth.SuspendedByFault; Fault = TacticalStageRejection.FaultSuspended; Changed();
            }
            else if (_time.Phase == TacticalTurnPhase.AwaitingSettle && Phase != TacticalStagePhase.TurnSettling)
            { Phase = TacticalStagePhase.TurnSettling; Changed(); }
        }

        /// <summary>NoAlloc. Synchronous authority hits use the current execution; delayed receipts must carry its ID.</summary>
        public bool TryApplyHitReward(long hitEventId, int momentumAmount)
            => TryApplyHitReward(_driver.ExecutionId, hitEventId, momentumAmount);

        /// <summary>NoAlloc. Rejects late execution receipts, repeated hits, faults and non-executing stages.</summary>
        public bool TryApplyHitReward(ulong executionId, long hitEventId, int momentumAmount)
        {
            if (_released || Health != TacticalStageHealth.Running || Phase != TacticalStagePhase.Executing ||
                !_driver.TryApplyHitReward(executionId, hitEventId, momentumAmount)) return false;
            Changed();
            return true;
        }

        // Called only by the control owner, including paused F+1 boundaries with no queued command.
        internal bool ReconcilePendingAvailability()
        {
            var pending=_deck.PendingAction;if(pending==null || _released || Health!=TacticalStageHealth.Running || Phase!=TacticalStagePhase.Planning)return false;
            var evaluation=_deck.EvaluateAvailability(pending);
            if(evaluation.IsLegal && _deck.IsCardOnCooldown(pending))evaluation=new CardPlayEvaluation(CardPlayFailure.CardOnCooldown,0,0);
            bool staleActor=!_isEntityCurrent(_player) || !_isEntityCurrent(_opponent);
            var input=_deck.PendingExecutionInput;
            bool invalidInput=input.Kind!=TacticalExecutionInputKind.None && _driver.ValidatePendingInput(pending,input)!=TacticalInputStatus.Valid;
            if(!staleActor && evaluation.IsLegal && !invalidInput)return false;
            var failure=_deck.CancelPendingAction(_ledger);
            if(failure!=CardPlayFailure.None){ReportFault(TacticalStageRejection.ExecutionRejected);return false;}
            ExecutionIssue="pending:"+(staleActor?"ActorChanged":invalidInput?"InputUnavailable":evaluation.Failure.ToString());
            Changed();Reject(staleActor||invalidInput?TacticalStageRejection.InvalidCard:Map(evaluation.Failure));return true;
        }

        /// <remarks>Only the command pump calls this method. Allocation follows the admission/query owners and fault diagnostics.</remarks>
        public TacticalStageRejection Apply(in TacticalStageCommand command)
        {
            if (_preparingAutomaticInput) return Reject(TacticalStageRejection.InvalidCommand);
            ReconcilePendingAvailability();
            if (command.StageRunId != StageRunId) return Reject(TacticalStageRejection.WrongStage);
            if (Health == TacticalStageHealth.AwaitingStoryTransfer) return Reject(TacticalStageRejection.CriticalFlowPending);
            if (_released) return Reject(TacticalStageRejection.WrongPhase);
            if (command.TurnIndex != TurnIndex) return Reject(TacticalStageRejection.WrongTurn);
            if (command.SnapshotRevision != SnapshotRevision) return Reject(TacticalStageRejection.StaleSnapshot);
            if (command.Kind < TacticalStageCommandKind.SelectAction || command.Kind > TacticalStageCommandKind.ConfirmAutomaticExecution)
                return Reject(TacticalStageRejection.InvalidCommand);
            if (command.Kind == TacticalStageCommandKind.RequestRestart) return Restart();
            if (command.Kind == TacticalStageCommandKind.RequestRecovery) return Recover();
            if (Health == TacticalStageHealth.SuspendedByFault) return Reject(TacticalStageRejection.FaultSuspended);
            if (Phase != TacticalStagePhase.Planning) return Reject(TacticalStageRejection.WrongPhase);

            TacticalCardInstance card = command.Kind == TacticalStageCommandKind.CancelPending ||
                command.Kind == TacticalStageCommandKind.ConfirmExecution || command.Kind == TacticalStageCommandKind.ConfirmAutomaticExecution || command.Kind == TacticalStageCommandKind.SetExecutionInput
                ? _deck.PendingAction : FindHand(command.CardInstanceId);
            if (card == null) return Reject(command.Kind == TacticalStageCommandKind.CancelPending ||
                command.Kind == TacticalStageCommandKind.ConfirmExecution || command.Kind == TacticalStageCommandKind.ConfirmAutomaticExecution || command.Kind == TacticalStageCommandKind.SetExecutionInput
                ? TacticalStageRejection.NoPendingAction : TacticalStageRejection.InvalidCard);
            if (card.InstanceId != command.CardInstanceId) return Reject(TacticalStageRejection.InvalidCard);
            if (card.Version != command.CardVersion) return Reject(TacticalStageRejection.StaleCardVersion);
            CardPlayFailure failure;
            switch (command.Kind)
            {
                case TacticalStageCommandKind.SelectAction:
                    if (!(card.Definition is TacticalActionCardDefinition) && !InteractionPreparationPayload.IsPreparation(card.Definition))
                        return Reject(TacticalStageRejection.InvalidCard);
                    if (InteractionPreparationPayload.IsPreparation(card.Definition))
                    {
                        var preparationRejection = ValidatePreparation(card);
                        if (preparationRejection != TacticalStageRejection.None) return Reject(preparationRejection);
                    }
                    // Check replacement affordability before cancelling the previous reservation.
                    if (card.Definition.BaseMomentumCost > _ledger.Current) return Reject(TacticalStageRejection.InsufficientMomentum);
                    var selection=_deck.EvaluateAvailability(card,_deck.PendingAction!=null?_ledger.Reserved:0);
                    if(!selection.IsLegal)return Reject(Map(selection.Failure));
                    if(_deck.IsCardOnCooldown(card))return Reject(TacticalStageRejection.InvalidCard);
                    if (_deck.PendingAction != null)
                    {
                        failure = _deck.CancelPendingAction(_ledger);
                        if (failure != CardPlayFailure.None) return Reject(Map(failure));
                    }
                    failure = _deck.SetPendingAction(card, _ledger);
                    break;
                case TacticalStageCommandKind.CancelPending:
                    failure = _deck.CancelPendingAction(_ledger);
                    break;
                case TacticalStageCommandKind.SetExecutionInput:
                    var expected = new TacticalExecutionInputBinding(StageRunId, TurnIndex, card.InstanceId, card.Version, _player);
                    var status = command.ExecutionInput.Binding.Equals(expected)
                        ? _driver.ValidatePendingInput(card, command.ExecutionInput) : TacticalInputStatus.StaleBinding;
                    if (status == TacticalInputStatus.Valid) status = _deck.SetPendingExecutionInput(command.ExecutionInput, expected);
                    if (status != TacticalInputStatus.Valid)
                    {
                        ExecutionIssue = "execution-input:" + status;
                        return Reject(TacticalStageRejection.InvalidExecutionInput);
                    }
                    ExecutionIssue = string.Empty;
                    failure = CardPlayFailure.None;
                    break;
                case TacticalStageCommandKind.PlayUtility:
                    failure = _deck.PlayUtilityCard(card, _ledger);
                    break;
                default:
                    if (InteractionPreparationPayload.IsPreparation(card.Definition))
                        return command.Kind == TacticalStageCommandKind.ConfirmExecution
                            ? (InteractionPreparationPayload.Matches(card.Definition) ? ConfirmInteractionPreparation(card) : ConfirmTestPreparation(card)) : Reject(TacticalStageRejection.InvalidCommand);
                    if (command.Kind == TacticalStageCommandKind.ConfirmAutomaticExecution
                        && _driver.GetRequiredInputKind(card) == TacticalExecutionInputKind.WorldPoint)
                    {
                        // Acquire a fresh point inside the existing command boundary for every attempt.
                        // A previously selected manual/automatic point is never an automatic fallback.
                        _deck.ClearPendingExecutionInput();
                        TacticalInputStatus automaticStatus;
                        TacticalExecutionInput automaticInput;
                        _preparingAutomaticInput = true;
                        try { automaticStatus = TryBuildAutomaticPointInput(_pointProvider, out automaticInput); }
                        catch (Exception)
                        { automaticStatus = TacticalInputStatus.PointUnavailable; automaticInput = default; }
                        finally { _preparingAutomaticInput = false; }
                        if (automaticStatus == TacticalInputStatus.Valid)
                            automaticStatus = _deck.SetPendingExecutionInput(automaticInput, automaticInput.Binding);
                        if (automaticStatus != TacticalInputStatus.Valid)
                        {
                            ExecutionIssue = "execution-input:" + automaticStatus;
                            return Reject(TacticalStageRejection.InvalidExecutionInput);
                        }
                    }
                    if (!_driver.TryCommit(LastCompletedFrame, out string issue))
                    {
                        ExecutionIssue = issue;
                        if (_time.Phase == TacticalTurnPhase.Suspended) ReportFault(TacticalStageRejection.ExecutionRejected);
                        return Reject(TacticalStageRejection.ExecutionRejected);
                    }
                    Phase = TacticalStagePhase.Executing;
                    ExecutionIssue = string.Empty;
                    failure = CardPlayFailure.None;
                    break;
            }
            if (failure != CardPlayFailure.None) return Reject(Map(failure));
            Changed();
            return LastRejection;
        }

        /// <summary>Original Spawn/Story owners report explicit failures here at a safe boundary.</summary>
        internal void ReportExecutionFault(string issue)
        {
            ExecutionIssue = issue ?? string.Empty;
            ReportFault(TacticalStageRejection.ExecutionRejected);
        }

        /// <summary>Original Spawn/Story owners report explicit failures here at a safe boundary.</summary>
        public void ReportFault(TacticalStageRejection fault)
        {
            if (_released || Phase == TacticalStagePhase.StageCompleted || Health == TacticalStageHealth.SuspendedByFault) return;
            Health = TacticalStageHealth.SuspendedByFault; Fault = fault;
            _time.ForceSuspend(fault == TacticalStageRejection.IngressTimeout || fault == TacticalStageRejection.SpawnTimeout
                || fault == TacticalStageRejection.StoryTransferTimeout
                ? TacticalPauseReason.RecoveryTimeout : TacticalPauseReason.ExecutionFault);
            ResetIngress(); Changed(); Reject(fault);
        }

        private void InitializePlanning()
        {
            if (_initialized) return;
            if (_ledger.Reserved != 0 ||
                (_ledger.Current > _initialMomentum && !_ledger.TrySpend(_ledger.Current - _initialMomentum)))
            { ReportFault(TacticalStageRejection.ExecutionRejected); return; }
            _deck.InitializeDeck(_definitions, _seed, _configuredInitialHandCount);
            _ledger.Grant(_initialMomentum - _ledger.Current);
            _time.Initialize();
            _initialized = true;
            BeginPlanningPause();
        }

        private void BeginPlanningPause()
        {
            SyncInteractionTurn();
            _awaitingWorldPause = !_time.IsWorldPaused;
            Phase = _awaitingWorldPause ? TacticalStagePhase.TurnSettling : TacticalStagePhase.Planning;
            Changed();
        }

        /// <summary>Publishes decision readiness only after the world rate has reached zero.</summary>
        internal void UpdatePauseTransition()
        {
            if (_released || !_awaitingWorldPause || Health != TacticalStageHealth.Running
                || Phase != TacticalStagePhase.TurnSettling || !_time.IsWorldPaused) return;
            _awaitingWorldPause = false;
            Phase = TacticalStagePhase.Planning;
            Changed();
            if (!string.IsNullOrEmpty(ExecutionIssue)) LastRejection = TacticalStageRejection.ExecutionRejected;
        }

        internal bool TryInitializeMixedRosterPlanning()
        {
            if (_released || Health != TacticalStageHealth.Running
                || Phase != TacticalStagePhase.AwaitingIngressSettle
                || !_player.IsValid || !_opponent.IsValid)
                return false;
            InitializePlanning();
            return Phase == TacticalStagePhase.Planning || _awaitingWorldPause;
        }

        /// <summary>
        /// Replaces the deck and its baseline resource. Deployment binds the hero loadout before planning
        /// initializes, so the value is staged first and applied either now (already planning) or by
        /// <see cref="InitializePlanning"/> later. Applying it only in the initialized branch used to
        /// discard the deployed hero's own momentum and silently reset every hero to the stage default.
        /// </summary>
        public void RebindDeck(IReadOnlyList<TacticalCardDefinition> definitions, int initialMomentum = 50, ITacticalPointProvider pointProvider = null)
        {
            if (definitions == null || definitions.Count == 0)
                throw new ArgumentException("A battle deck requires at least one definition.", nameof(definitions));
            _definitions = new TacticalCardDefinition[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
                _definitions[i] = definitions[i] ?? throw new ArgumentException("Null card definition.", nameof(definitions));
            int momentum = ClampMomentum(initialMomentum);
            ClearTestPreparation();
            _initialMomentum = momentum;
            if (_initialized)
            {
                _deck.InitializeDeck(_definitions, _seed, _configuredInitialHandCount);
                if (_ledger.Current < momentum) _ledger.Grant(momentum - _ledger.Current);
                else if (_ledger.Current > momentum) _ledger.TrySpend(_ledger.Current - momentum);
                Changed();
            }
        }

        /// <summary>Keeps the baseline inside the ledger's supported range; loadout data is untrusted.</summary>
        private static int ClampMomentum(int value) =>
            value < 0 ? 0 : value > MomentumLedger.Maximum ? MomentumLedger.Maximum : value;

        private TacticalStageRejection Recover()
        {
            if (!CanRequestRecovery)
                return Reject(TacticalStageRejection.RecoveryRejected);
            _time.RequestResume(TacticalPauseReason.RecoveryTimeout);
            _ingressElapsedTicks = 0; ResetIngress();
            if (Fault == TacticalStageRejection.StoryTransferTimeout)
            {
                Health = TacticalStageHealth.Running;
                BeginStoryTransfer(LastCompletedFrame);
                Fault = TacticalStageRejection.None;
                return LastRejection;
            }
            Health = TacticalStageHealth.Running; Fault = TacticalStageRejection.None;
            Changed(); return LastRejection;
        }

        private TacticalStageRejection Restart()
        {
            if (_endTriggers != null && !_endTriggers.CanLeaveSession) return Reject(TacticalStageRejection.CriticalFlowPending);
            if (_requestStoryRestart == null || LastCompletedFrame == long.MaxValue ||
                !_requestStoryRestart(StageRunId, LastCompletedFrame + 1))
                return Reject(TacticalStageRejection.RestartRejected);
            BeginStoryRestart(LastCompletedFrame);
            Changed(); return LastRejection;
        }

        private TacticalCardInstance FindHand(ulong id)
        {
            for (int i = 0; i < _deck.Hand.Count; i++) if (_deck.Hand[i].InstanceId == id) return _deck.Hand[i];
            return null;
        }

        private void ResetIngress() { _playerIngressDetector.Reset(); _opponentIngressDetector.Reset(); }
        private void Changed() { SnapshotRevision++; LastRejection = TacticalStageRejection.None; }
        private TacticalStageRejection Reject(TacticalStageRejection rejection) { LastRejection = rejection; return rejection; }
        private static TacticalStageRejection Map(CardPlayFailure failure)
        {
            switch (failure)
            {
                case CardPlayFailure.None: return TacticalStageRejection.None;
                case CardPlayFailure.InsufficientMomentum: return TacticalStageRejection.InsufficientMomentum;
                case CardPlayFailure.UnsupportedPayload: return TacticalStageRejection.UnsupportedPayload;
                case CardPlayFailure.NoPendingAction: return TacticalStageRejection.NoPendingAction;
                case CardPlayFailure.WrongPhase: return TacticalStageRejection.WrongPhase;
                default: return TacticalStageRejection.InvalidCard;
            }
        }
    }
}
