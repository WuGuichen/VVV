using System.Globalization;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class CharacterTestTacticalExecutionAdapter : ITacticalFixedExecutionPort, ITacticalFixedFeeReadoutPort, ITacticalFixedCleanupPort, ITacticalFixedTerminalPort
    {
        private TacticalFixedCardPaymentCommit _doubleEdgePayment;
        public bool IsNaturalFixedCompletion(ulong executionId) => executionId == _executionId && !_aborted && !_faulted && _session.NaturallyCompleted;
        public bool TryCommitFixed(TacticalCardInstance card, TacticalDeckManager deck, TacticalTimeCoordinator time,
            MomentumLedger ledger, in TacticalExecutionInput input, out ulong executionId,
            out TacticalFixedCardPaymentCommit payment, out string issue)
        {
            executionId = 0; payment = null; issue = "fixed-joint:UnsupportedMainCard";
            if (!(card?.Definition is TacticalActionCardDefinition action) || action.FixedAssembly == null
                || !action.HasPublishedBehavior || action.PointMove != null || action.PositionRecall != null
                || _nextId == ulong.MaxValue) return false;
            if (!ValidateCard(card, out var facts, out issue)) return false;
            if (!_runtime.World.TryGetTacticalMotion(_entity.Id.Value, out var actualLedger)
                || !ReferenceEquals(actualLedger, ledger)) { issue = "fixed-joint:ActualLedgerRequired"; return false; }
            var effects = _entity.InteractionEffects;
            if (effects.Stage == 0 || effects.Turn != time.CurrentTurnIndex || effects.Actor != _entity.GameplayEntityId)
            { issue = "fixed-joint:ActualEffectsOwnerRequired"; return false; }
            var assembly = action.FixedAssembly;
            if (!TryPrepareFixedFees(action, true, out var published, out var source, out var inputs,
                out var program, out var proof, out var fees, out issue)) return false;
            if (!deck.TryPrepareFixedMove(card, ledger, out var deckMove) || !time.TryPrepareFixedTurn(deck, out var turn))
            { issue = "fixed-joint:DeckOrTimeChanged"; return false; }
            ulong proposedId = _nextId + 1;
            ulong activation = effects.AccessoryActivationCount == 0 ? 1
                : effects.AccessoryAt(effects.AccessoryActivationCount - 1).Identity.ActivationId + 1;
            if (activation == 0 || !TacticalFixedCardCommitManifest.TryPrepare(assembly, fees, card, effects.Stage, effects.Turn,
                _entity.GameplayEntityId, proposedId, proposedId, _frame.Value, effects.AccessoryReceiptSequence, activation,
                deck.FixedPendingReservation(ledger), input, out var manifest))
            { issue = "fixed-joint:ManifestRejected"; return false; }
            if (!TacticalFixedCardCommitManifest.PreparedPaymentAndAccessories.TryPrepare(manifest, card, ledger, effects,
                program.Execution, proof, out var core, out issue)) return false;
            if (!_session.TryPrepareFixedPublished(published.Metadata.DisplayName, published.ContentHash.ToString(CultureInfo.InvariantCulture),
                program, inputs, source, facts, _frame, out var host, out issue)) return false;
            var equipment = _entity.Equipment.ToSnapshot();
            var target = _runtime.FindEntityById(facts.TargetEntityId);
            TacticalDoubleEdgeBattleState.Prepared doubleEdge = null;
            var battleEffects = _runtime.World.DoubleEdgeState;
            if ((assembly.HasDoubleEdge || assembly.HasSecondBatch || assembly.HasThirdBatch) && !battleEffects.TryPrepare(manifest.DoubleEdgeKey, effects.Turn,
                target?.GameplayEntityId ?? default, assembly.DoubleEdgeParameters, assembly.SecondBatchParameters, assembly.ThirdBatchParameters,
                target != null && target.CombatState.IsAlive && target.Team.TeamId != _entity.Team.TeamId,
                _entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer
                    || _entity.Control.Owner == CharacterTestCharacterControlOwner.RemotePlayer, out doubleEdge))
            { issue = "fixed-joint:DoubleEdgeAdmissionRejected"; return false; }
            if (!host.IsCurrent || !deckMove.IsCurrent || !turn.IsCurrent || !core.IsCurrent
                || doubleEdge != null && !doubleEdge.IsCurrent
                || !ValidateCard(card, out var currentFacts, out issue) || !equipment.Equals(_entity.Equipment.ToSnapshot())
                || currentFacts.TargetEntityId != facts.TargetEntityId || !ReferenceEquals(target, _runtime.FindEntityById(currentFacts.TargetEntityId)))
            { if (string.IsNullOrEmpty(issue)) issue = "fixed-joint:PreparationChanged"; return false; }
            // No world step or callback between this complete check and the final time publication.
            if (!core.TryPublish(out payment)) { issue = "fixed-joint:CoreChanged"; return false; }
            if (doubleEdge != null) {
                if (!doubleEdge.TryPublish()) throw new System.InvalidOperationException("fixed-joint:DoubleEdgePublicationChanged");
                payment.DoubleEdgeState = battleEffects;
            }
            host.Publish(payment); BeginExecutionIdentity(card); deckMove.Publish(payment);
            _doubleEdgePayment = payment.DoubleEdgeState == null ? null : payment;
            _targetId = facts.TargetEntityId; _target = target; _rewardedHitCount = 0;
            _lastTick = _frame.Value; _actionInstance = null;
            _actionStarted = _timelineCompleted = _faulted = _aborted = false;
            _recallExecution = _tractionExecution = _pointExecution = false; _prepared = null;
            executionId = _executionId; LastIssue = issue = string.Empty;
            turn.Publish(); return true;
        }

        /// <remarks>LowFreqAlloc. Reuses the same compiled-program proof as confirmation, without starting
        /// a host or changing deck/ledger/effects. UI invokes on selection changes, never a simulation Tick.</remarks>
        public bool TryReadFixedFees(TacticalCardInstance card, out TacticalFixedCardFeePlan fees, out string issue)
        {
            fees = null; issue = "fixed-fee:Unavailable";
            if (!(card?.Definition is TacticalActionCardDefinition action) || action.FixedAssembly == null
                || !action.HasPublishedBehavior || action.PointMove != null || action.PositionRecall != null
                || !ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity)) return false;
            string savedIssue = LastIssue, savedWarning = LastWarning;
            try { return TryPrepareFixedFees(action, false, out _, out _, out _, out _, out _, out fees, out issue); }
            finally { LastIssue = savedIssue; LastWarning = savedWarning; }
        }

        private bool TryPrepareFixedFees(TacticalActionCardDefinition action, bool allowSummaryRepair,
            out CharacterTestRuntimeAiPlannerPublishedBehaviorCard published, out CharacterTacticalBoundCardInputs source,
            out CharacterTestTacticalCardInputs inputs, out CharacterTestRuntimeAiLabCompiledLogicProgram program,
            out CharacterTestFixedActionBudgetProof proof, out TacticalFixedCardFeePlan fees, out string issue)
        {
            source = null; inputs = null; program = null; proof = null; fees = null;
            if (!TryResolvePublished(action, out published, out issue)) return false;
            var assembly = action.FixedAssembly;
            source = new CharacterTacticalBoundCardInputs(_runtime, _entity.Id.Value, assembly.Bindings);
            source.WithRequirements(published.TacticalSource.InputRequirements);
            if (!published.TacticalSource.HasPlayerActionSlot || _deployment?.UsesLegacyFunctionalResolverFallback == true)
                source.UseFunctionalResolvers();
            // Confirmation may repair derived measurements before any payment or host publication.
            // Fee presentation remains read-only and must never start a probe.
            bool prepared = allowSummaryRepair ? source.TryPrepare(out inputs, out issue)
                : source.TryPrepareReadOnly(out inputs, out issue);
            if (!prepared || !published.TacticalSource.TryCompile(published.StableCardId, published.Metadata.DisplayName, published.Revision,
                    inputs, out program, out issue)) return false;
            if (!CharacterTestFixedActionBudgetProof.TryCreate(program.Execution,
                id => _runtime.ActionUseAuthority.TryGetFixedActionMomentumCost(_entity.Id.Value, id, out var cost, out _) ? (int?)cost : null,
                out proof, out issue)) return false;
            if (!TacticalFixedCardFeePlan.TryCreate(assembly, TacticalFixedCardPaymentTiming.CardAndAccessoriesThenActualActions,
                proof.MaximumMomentum, out fees)) { issue = "fixed-joint:FeeOverflow"; return false; }
            return true;
        }

        private System.Action _fixedActorRetired;
        private ulong _fixedReleaseId;
        private long _fixedCleanupFrame = -1;
        private bool _fixedReleaseObserved;

        // Delegates are created only on the cold ReleaseControl call; event handlers never allocate.
        public void SetFixedReleaseObserver(ulong executionId, System.Action onActorRetired)
        {
            if (executionId == 0 || executionId != _executionId || _retired || _admitted?.Definition.FixedAssembly == null) return;
            if (_fixedReleaseObserved) return;
            _fixedReleaseObserved = true; _fixedReleaseId = executionId; _fixedActorRetired = onActorRetired;
            _runtime.World.EntityRemoved += OnFixedReleaseEntityRemoved;
            _runtime.World.EntitiesCleared += OnFixedReleaseWorldCleared;
        }
        private void OnFixedReleaseEntityRemoved(CharacterTestCharacterEntityId id)
        { if (id.Value == _entity.Id.Value) _fixedActorRetired?.Invoke(); }
        private void OnFixedReleaseWorldCleared() => _fixedActorRetired?.Invoke();

        /// <remarks>NoAlloc. Only polls/ticks the stopped host. Actor absence is certified by this exact
        /// world's active identity query, never by an incoming command, DTO or UI boolean.</remarks>
        public ExecutionObservation ReconcileFixedRelease(ulong executionId)
        {
            if (!_fixedReleaseObserved || _fixedReleaseId != executionId || executionId != _executionId) return default;
            bool present = ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity);
            if (!present) {
                _session.Stop(_facts, _frame);
                return new ExecutionObservation(executionId, _actionStarted, false, true, true, true);
            }
            if (_session.IsExecuting && _fixedCleanupFrame != _frame.Value) {
                _fixedCleanupFrame = _frame.Value; _session.Tick(_facts, _frame);
            }
            ReadActionFacts();
            _session.ReconcileFixedHostTerminal();
            bool terminal = _session.State == CharacterTestRuntimeAiLabLogicRuntimeState.Completed
                || _session.State == CharacterTestRuntimeAiLabLogicRuntimeState.Failed || _aborted && !_session.IsExecuting;
            bool acknowledged = _commands.PendingCommandCount == 0 && !_session.HasPendingActionCommand;
            var owned = _session.AcknowledgedActionInstance ?? _actionInstance;
            var state = _session.AcknowledgedActionState;
            bool actionReleased = owned == null || state == MxFramework.CharacterAction.CharacterActionInstanceState.Finished
                || state == MxFramework.CharacterAction.CharacterActionInstanceState.Cancelled
                || state == MxFramework.CharacterAction.CharacterActionInstanceState.Interrupted;
            bool released = terminal && acknowledged && actionReleased && !_entity.Input.HasActiveMove
                && (!_entity.Input.BehaviorControl.IsPresent || _entity.Input.BehaviorControl.IsRelease);
            return new ExecutionObservation(executionId, _actionStarted, false, terminal, released, acknowledged);
        }
        public void FinishFixedRelease(ulong executionId)
        {
            if (!_fixedReleaseObserved || _fixedReleaseId != executionId) return;
            _runtime.World.EntityRemoved -= OnFixedReleaseEntityRemoved;
            _runtime.World.EntitiesCleared -= OnFixedReleaseWorldCleared;
            _fixedReleaseObserved = false; _fixedActorRetired = null; _fixedReleaseId = 0;
            // An abandoned Stage cannot be captured as a settled idle receipt or admitted again.
            _retired = true; _retiredReceiptSealed = false;
        }

        public void CancelFixed(ulong executionId)
        {
            if (executionId == 0 || executionId != _executionId || _admitted?.Definition.FixedAssembly == null || _retired) return;
            var stopFrame = _frame;
            if (_runtime.TryGetLastCompletedRuntimeFrame(out var completed) && completed.Value >= stopFrame.Value) {
                if (completed.Value >= int.MaxValue - 1) return;
                stopFrame = new MxFramework.Runtime.RuntimeFrame(completed.Value + 1);
            }
            // Completed-frame control identities were already consumed. Cleanup owns the next boundary.
            _doubleEdgePayment?.DoubleEdgeState.CompleteHost(_doubleEdgePayment.Record.DoubleEdgeKey,
                false, stopFrame.Value, 0, 0, 0);
            _aborted = true; _fixedCleanupFrame = stopFrame.Value; _session.Stop(_facts, stopFrame);
        }
    }
}
