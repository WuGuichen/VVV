using System;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Connects one admitted request to deck/time authority without creating another action timeline.</summary>
    public sealed partial class TacticalSingleCardDriver
    {
        private readonly ITacticalExecutionPort _execution;
        private readonly TacticalTimeCoordinator _coordinator;
        private readonly TacticalDeckManager _deck;
        private readonly IMomentumLedger _ledger;
        private readonly int _preActionBudget;
        private readonly int _executionBudget;
        private long _lastTick;
        private int _elapsedTicks;
        private bool _started;
        private bool _physicalStarted;
        private bool _deferPayment;
        private bool _timeoutRaised;
        private TacticalFixedCardPaymentCommit _fixedPayment;
        private bool _fixedReleaseRequested;
        private TacticalPauseReason? _fixedFaultAfterCleanup;
        private readonly Action _onFixedActorRetired;
        internal bool HasReleasedCleanup => _fixedReleaseRequested && _fixedPayment != null && ExecutionId != 0;
        public ulong ExecutionId { get; private set; }
        public string LastIssue { get; private set; } = string.Empty;

        internal ExecutionObservation ObserveOwnedExecution(ulong executionId) => _execution.Observe(executionId);

        /// <remarks>LowFreqAlloc before confirmation; confirmed fees are read from the actual paid receipt.</remarks>
        internal bool TryReadFixedFees(TacticalCardInstance card, out TacticalFixedCardFeePlan fees, out string issue)
        {
            fees = null; issue = "fixed-fee:OwnerUnavailable";
            if (card?.Definition.FixedAssembly == null) return false;
            if (_fixedPayment?.IsPaid == true && _fixedPayment.Record.BattleCardInstance == card.InstanceId)
            { fees = _fixedPayment.Record.Fees; issue = string.Empty; return true; }
            return _execution is ITacticalFixedFeeReadoutPort port && port.TryReadFixedFees(card, out fees, out issue);
        }

        internal bool Uses(TacticalTimeCoordinator time, TacticalDeckManager deck, IMomentumLedger ledger)
            => ReferenceEquals(_coordinator, time) && ReferenceEquals(_deck, deck) && ReferenceEquals(_ledger, ledger);

        public TacticalSingleCardDriver(ITacticalExecutionPort execution, TacticalTimeCoordinator coordinator,
            TacticalDeckManager deck, IMomentumLedger ledger, int preActionBudgetTicks = 240, int executionBudgetTicks = 300)
        {
            _execution = execution ?? throw new ArgumentNullException(nameof(execution));
            _onFixedActorRetired = OnFixedActorRetired;
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _deck = deck ?? throw new ArgumentNullException(nameof(deck));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            if (preActionBudgetTicks <= 0) throw new ArgumentOutOfRangeException(nameof(preActionBudgetTicks));
            if (executionBudgetTicks <= 0) throw new ArgumentOutOfRangeException(nameof(executionBudgetTicks));
            _preActionBudget = preActionBudgetTicks;
            _executionBudget = executionBudgetTicks;
        }

        /// <summary>Cold admission boundary. All collaborators must be synchronous, single-threaded and non-reentrant.</summary>
        internal TacticalExecutionInputKind GetRequiredInputKind(TacticalCardInstance card)
            => _execution is ITacticalExecutionInputPort inputs ? inputs.GetRequiredInputKind(card) : TacticalExecutionInputKind.None;

        internal TacticalInputStatus ValidatePendingInput(TacticalCardInstance card, in TacticalExecutionInput input)
        {
            if (!(_execution is ITacticalExecutionInputPort inputs)) return TacticalInputStatus.WrongInputKind;
            var required = inputs.GetRequiredInputKind(card);
            if (required == TacticalExecutionInputKind.None || input.Kind != required)
                return input.Kind == TacticalExecutionInputKind.None ? TacticalInputStatus.MissingInput : TacticalInputStatus.WrongInputKind;
            return inputs.ValidateInput(card, input);
        }

        internal TacticalInputStatus ValidateConfirmationInput(TacticalCardInstance card)
            => ValidatePendingInput(card, CaptureConfirmationInput());

        internal TacticalInputStatus TryBuildAutomaticPointInput(ITacticalPointProvider provider,
            TacticalExecutionInputBinding binding, out TacticalExecutionInput input)
        {
            input = default;
            var card = _deck.PendingAction;
            var rules = (card?.Definition as TacticalActionCardDefinition)?.PointMove;
            if (rules == null || GetRequiredInputKind(card) != TacticalExecutionInputKind.WorldPoint)
                return TacticalInputStatus.WrongInputKind;
            if (binding.CardInstance != card.InstanceId || binding.CardVersion != card.Version)
                return TacticalInputStatus.StaleBinding;
            if (!TryGetPointOrigin(out var origin)) return TacticalInputStatus.CharacterUnavailable;
            var request = new TacticalPointProviderRequest(binding, origin, rules.Input);
            var status = TacticalPointProviderInput.TryCreate(provider, request, out var candidate);
            if (status != TacticalInputStatus.Valid) return status;
            status = ValidatePendingInput(card, candidate);
            if (status == TacticalInputStatus.Valid) input = candidate;
            return status;
        }

        private TacticalExecutionInput CaptureConfirmationInput()
        {
            var input = _deck.PendingExecutionInput;
            // The owner captures the confirmation center; only the selected world point is carried forward.
            if (input.Kind == TacticalExecutionInputKind.WorldPoint && TryGetPointOrigin(out var origin))
                return new TacticalExecutionInput(input.Binding, input.Source, input.Point, origin);
            return input;
        }

        internal bool TryPrepareBehindPreview(TacticalCardInstance card, out CharacterTestBehaviorExecutionMovement movement, out string issue)
        {
            movement = default; issue = string.Empty;
            return _execution is ITacticalBehindPreviewPort preview && preview.TryPrepareBehindPreview(card, out movement, out issue);
        }
        internal bool TrySampleBehindPreview(TacticalCardInstance card, in CharacterTestBehaviorExecutionMovement movement,
            out TacticalWorldPoint origin, out CharacterTestRuntimeAiLabMovementTargetSample sample)
        {
            origin = default; sample = default;
            return _execution is ITacticalBehindPreviewPort preview && preview.TrySampleBehindPreview(card, movement, out origin, out sample);
        }

        internal bool TryGetPointOrigin(out TacticalWorldPoint origin)
        {
            origin = default;
            return _execution is ITacticalPointSelectionPort points && points.TryGetPointOrigin(out origin);
        }
        internal TacticalInputStatus TryPickGround(TacticalWorldPoint origin, TacticalWorldPoint direction, out TacticalWorldPoint point)
        {
            point = default;
            return _execution is ITacticalPointSelectionPort points ? points.TryPickGround(origin, direction, out point)
                : TacticalInputStatus.GroundUnavailable;
        }

        public bool TryCommit(long currentTick, out string issue)
        {
            issue = string.Empty;
            if (ExecutionId != 0 || _coordinator.Phase != TacticalTurnPhase.ReadyPaused || _deck.PendingAction == null)
            { issue = "execution:NotReady"; return false; }
            var card = _deck.PendingAction;
            ulong id;
            if (card.Definition.FixedAssembly != null)
            {
                if (!(_execution is ITacticalFixedExecutionPort fixedPort) || !(_ledger is MomentumLedger actualLedger))
                { issue = "execution:FixedAssemblyOwnerUnavailable"; return false; }
                if (!fixedPort.TryCommitFixed(card, _deck, _coordinator, actualLedger, CaptureConfirmationInput(),
                    out id, out _fixedPayment, out issue)) return false;
                ExecutionId = id; _lastTick = currentTick; _elapsedTicks = 0;
                _started = _physicalStarted = _deferPayment = _timeoutRaised = false; LastIssue = string.Empty;
                return true;
            }
            if (_execution is ITacticalExecutionInputPort inputs && inputs.GetRequiredInputKind(card) != TacticalExecutionInputKind.None)
            {
                var input = CaptureConfirmationInput();
                var status = ValidatePendingInput(card, input);
                if (status != TacticalInputStatus.Valid) { issue = "execution-input:" + status; return false; }
                if (!inputs.TryPrepare(card, input, out issue) || !inputs.TryAdmit(card, input, out id, out issue)) return false;
            }
            else if (!_execution.TryPrepare(card, out issue) || !_execution.TryAdmit(card, out id, out issue)) return false;
            if (id == 0)
            { _coordinator.ForceSuspend(TacticalPauseReason.ExecutionFault); issue = "execution:InvalidAdmission"; return false; }
            bool deferPayment = (card.Definition as TacticalActionCardDefinition)?.DefersPaymentUntilStarted == true;
            if (!_coordinator.TryCommitTurn(_deck, _ledger, deferPayment, out _))
            {
                // No world step occurred after admission; a conforming port can safely retract this request.
                if (!_execution.TryAbortPreAction(id, "execution:CommitRejected"))
                    _coordinator.ForceSuspend(TacticalPauseReason.ExecutionFault);
                issue = "execution:CommitRejected";
                return false;
            }
            ExecutionId = id;
            _lastTick = currentTick;
            _elapsedTicks = 0;
            _started = false;
            _physicalStarted = false;
            _deferPayment = deferPayment;
            _timeoutRaised = false;
            LastIssue = string.Empty;
            return true;
        }

        /// <summary>NoAlloc. Only a current, running execution may pay an authoritative hit through its optional port.</summary>
        public bool TryApplyHitReward(ulong executionId, long hitEventId, int momentumAmount)
            => executionId != 0 && executionId == ExecutionId && !_coordinator.IsWorldPaused &&
                _coordinator.Phase == TacticalTurnPhase.Executing && _execution is ITacticalHitRewardPort rewards &&
                rewards.TryApplyHitReward(executionId, hitEventId, momentumAmount, _ledger);

        internal void ResetSettleWindow() => _coordinator.ResetSettleWindow();

        private bool ApplyExecutionStart(in ExecutionObservation observation, long tick)
        {
            if (!observation.ExecutionStarted || _started) return true;
            if (_deferPayment && !_deck.SettleResolvingReservation(_ledger, true))
            {
                LastIssue = "execution:ReservationCommitRejected";
                _coordinator.ForceSuspend(TacticalPauseReason.ExecutionFault); return false;
            }
            _started = true;
            if (_deck.ResolvingAction?.Definition is TacticalActionCardDefinition actionDef)
                _deck.CooldownTracker?.TriggerCooldown(_deck.CharacterId, actionDef,
                    observation.ExecutionStartedTick >= 0 ? observation.ExecutionStartedTick : tick);
            return true;
        }

        /// <summary>NoAlloc. Reconcile an already accepted move before releasing a stage's remaining reservation.</summary>
        internal void ReleaseControl()
        {
            if (_fixedPayment != null && ExecutionId != 0)
            {
                if (!_fixedReleaseRequested) {
                    _fixedReleaseRequested = true;
                    (_execution as ITacticalFixedCleanupPort)?.SetFixedReleaseObserver(ExecutionId, _onFixedActorRetired);
                    (_execution as ITacticalFixedExecutionPort)?.CancelFixed(ExecutionId);
                }
                ReconcileReleasedControl(); return;
            }
            if (!_deferPayment || ExecutionId == 0) return;
            var observation = _execution.Observe(ExecutionId);
            if (!ApplyExecutionStart(in observation, _lastTick)) return;
            if (!_started && !_deck.SettleResolvingReservation(_ledger, false))
                _coordinator.ForceSuspend(TacticalPauseReason.ExecutionFault);
        }

        private void OnFixedActorRetired() => ReconcileReleasedControl();

        // Stop commands are consumed at the next runtime boundary. Keep the existing exit lane
        // open until its matching terminal/release ACK, rather than freezing those consumers now.
        private void SuspendExecutionFault(TacticalPauseReason reason)
        {
            if (_fixedPayment?.Record.Assembly.HasDoubleEdge != true) {
                _coordinator.ForceSuspend(reason); return;
            }
            _fixedFaultAfterCleanup = reason;
            ReleaseControl();
        }

        /// <summary>NoAlloc. Called from the runtime exit lane or the actual world's actor-retirement event.
        /// Does not complete a turn, refill a hand, refund confirmation fees or grant completion rewards.</summary>
        internal bool ReconcileReleasedControl()
        {
            if (!HasReleasedCleanup) return true;
            var cleanup = _execution as ITacticalFixedCleanupPort;
            var observation = cleanup != null ? cleanup.ReconcileFixedRelease(ExecutionId) : _execution.Observe(ExecutionId);
            if (observation.ExecutionId != ExecutionId || !observation.BehaviorTerminal
                || !observation.OwnedIntentsReleased || !observation.CommandsAcknowledged) return false;
            if (!_deck.ReleaseFixedReservation(_ledger, _fixedPayment)) {
                LastIssue = "execution:FixedBudgetReleaseRejected"; return false;
            }
            if (!_deck.CompleteFixedAbnormal(_fixedPayment)) {
                LastIssue = "execution:FixedExhaustionRejected"; return false;
            }
            cleanup?.FinishFixedRelease(ExecutionId);
            ExecutionId = 0; _fixedPayment = null; _fixedReleaseRequested = false;
            if (_fixedFaultAfterCleanup.HasValue) {
                var reason = _fixedFaultAfterCleanup.Value; _fixedFaultAfterCleanup = null;
                _coordinator.ForceSuspend(reason);
                // This execution was abandoned and its card may be exhausted. Recovery must not
                // resume the old completion receipt; only the existing stage restart can replace it.
                if (reason == TacticalPauseReason.RecoveryTimeout)
                    _coordinator.ForceSuspend(TacticalPauseReason.ExecutionFault);
            }
            return true;
        }

        /// <summary>NoAlloc. Invoke after the entire simulation tick, including reward and receipt handling.</summary>
        public bool OnCompletedSimulationTick(in SettleSample sample)
        {
            if (HasReleasedCleanup) { ReconcileReleasedControl(); return false; }
            if (ExecutionId == 0 || _coordinator.IsWorldPaused) return false;
            if (sample.SimulationTick <= _lastTick) { ResetSettleWindow(); return false; }
            long elapsed = sample.SimulationTick - _lastTick;
            _lastTick = sample.SimulationTick;
            _elapsedTicks = elapsed < 0 || elapsed >= int.MaxValue - _elapsedTicks
                ? int.MaxValue : _elapsedTicks + (int)elapsed;
            var observation = _execution.Observe(ExecutionId);
            if (observation.ExecutionId != ExecutionId)
            {
                LastIssue = string.IsNullOrEmpty(observation.Issue) ? "execution:ObservationMismatch" : observation.Issue;
                SuspendExecutionFault(TacticalPauseReason.ExecutionFault); return false;
            }
            if (!string.IsNullOrEmpty(observation.Issue)) LastIssue = observation.Issue;
            if (_fixedPayment?.Record.Assembly.HasDoubleEdge == true && observation.BehaviorTerminal
                && !(_execution is ITacticalFixedTerminalPort terminal && terminal.IsNaturalFixedCompletion(ExecutionId))) {
                SuspendExecutionFault(TacticalPauseReason.ExecutionFault); return false;
            }
            if (!ApplyExecutionStart(in observation, sample.SimulationTick)) return false;
            if (observation.ActionStarted && !_physicalStarted)
            {
                _physicalStarted = true;
                _coordinator.NotifyActionStarted();
            }
            if (!_started && !observation.BehaviorTerminal && _elapsedTicks >= _preActionBudget)
                _execution.TryAbortPreAction(ExecutionId, "execution:PreActionTimeout");
            if (observation.BehaviorTerminal) _coordinator.NotifyExecutionTerminal();
            if (_deferPayment && !_started && observation.BehaviorTerminal && observation.OwnedIntentsReleased
                && observation.CommandsAcknowledged && !_deck.SettleResolvingReservation(_ledger, false))
            { LastIssue = "execution:ReservationReleaseRejected"; _coordinator.ForceSuspend(TacticalPauseReason.ExecutionFault); return false; }
            if (_fixedPayment != null && observation.BehaviorTerminal)
            {
                if (!observation.OwnedIntentsReleased || !observation.CommandsAcknowledged) return false;
                if (!_deck.ReleaseFixedReservation(_ledger, _fixedPayment))
                { LastIssue = "execution:FixedBudgetReleaseRejected"; _coordinator.ForceSuspend(TacticalPauseReason.ExecutionFault); return false; }
            }
            // Lifecycle facts come from the matching port, never from an uncorrelated physics sample.
            var correlated = new SettleSample(sample.SimulationTick, observation.ActionTimelineCompleted,
                observation.BehaviorTerminal, sample.NeutralPoseReached, sample.HasReactionOrRecovery,
                sample.HasPendingLanding, sample.HasPendingMotionOrImpulse, observation.OwnedIntentsReleased,
                observation.CommandsAcknowledged, sample.HasStableSupport, sample.HasNewDestabilizingContact,
                sample.RelativeLinearSpeed, sample.RelativeAngularSpeed,
                observation.BehaviorTerminal && !_physicalStarted && observation.CommandsAcknowledged,
                playerCanActFreely: sample.PlayerCanActFreely);
            var resolvingCard = _deck.ResolvingAction;
            if (_coordinator.EvaluateSettle(in correlated, _deck))
            {
                if (resolvingCard?.Definition is TacticalActionCardDefinition actionCard)
                {
                    if (!_started && !_deferPayment && _fixedPayment == null)
                    {
                        // No physical action or accepted point movement started.
                        // Two-stage settlement: full refund of committed momentum cost.
                        _ledger.Grant(actionCard.BaseMomentumCost);
                    }
                    else if (observation.ActionTimelineCompleted && actionCard.MomentumReward > 0)
                    {
                        // Action completed its authored timeline: grant card-level MomentumReward.
                        _ledger.Grant(actionCard.MomentumReward);
                    }
                }
                if (_execution is ITacticalExecutionRetirementPort retirement)
                    retirement.RetireSettledExecution(ExecutionId, in observation, sample.SimulationTick);
                ExecutionId = 0; _fixedPayment = null;
                return true;
            }
            if (!_timeoutRaised && _elapsedTicks >= _executionBudget)
            {
                _timeoutRaised = true;
                LastIssue = "execution:RecoveryTimeout";
                SuspendExecutionFault(TacticalPauseReason.RecoveryTimeout);
            }
            return false;
        }
    }
}
