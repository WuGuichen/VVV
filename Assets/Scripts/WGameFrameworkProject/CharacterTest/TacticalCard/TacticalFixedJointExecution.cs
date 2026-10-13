using System;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal interface ITacticalFixedExecutionPort
    {
        bool TryCommitFixed(TacticalCardInstance card, TacticalDeckManager deck, TacticalTimeCoordinator time,
            MomentumLedger ledger, in TacticalExecutionInput input, out ulong executionId,
            out TacticalFixedCardPaymentCommit payment, out string issue);
        void CancelFixed(ulong executionId);
    }

    /// <summary>Cold fee projection only; never admission, payment or an execution start.</summary>
    internal interface ITacticalFixedFeeReadoutPort
    {
        bool TryReadFixedFees(TacticalCardInstance card, out TacticalFixedCardFeePlan fees, out string issue);
    }

    /// <summary>Fixed execution exit cleanup, independent of a released Stage's normal settle loop.</summary>
    internal interface ITacticalFixedCleanupPort
    {
        void SetFixedReleaseObserver(ulong executionId, Action onActorRetired);
        ExecutionObservation ReconcileFixedRelease(ulong executionId);
        void FinishFixedRelease(ulong executionId);
    }
    internal interface ITacticalFixedTerminalPort
    {
        bool IsNaturalFixedCompletion(ulong executionId);
    }

    public sealed partial class TacticalDeckManager
    {
        private TacticalFixedCardPaymentCommit _resolvingFixedPayment;
        internal ulong FixedPendingReservation(MomentumLedger ledger) => _initialized && ResolvingAction == null
            && PendingAction != null && ReferenceEquals(ledger, _pendingLedger) ? _reservationId : 0;

        internal bool TryPrepareFixedMove(TacticalCardInstance card, MomentumLedger ledger, out PreparedFixedMove prepared)
        {
            prepared = null;
            if (card == null || !ReferenceEquals(card, PendingAction) || card.Definition.FixedAssembly == null
                || card.Version >= uint.MaxValue - 1 || FixedPendingReservation(ledger) == 0) return false;
            prepared = new PreparedFixedMove(this, card, ledger); return true;
        }

        internal sealed class PreparedFixedMove
        {
            private readonly TacticalDeckManager _owner;
            private readonly TacticalCardInstance _card;
            private readonly MomentumLedger _ledger;
            private readonly uint _version;
            private readonly ulong _token;
            private bool _published;
            internal PreparedFixedMove(TacticalDeckManager owner, TacticalCardInstance card, MomentumLedger ledger)
            { _owner = owner; _card = card; _ledger = ledger; _version = card.Version; _token = owner._reservationId; }
            internal bool IsCurrent => !_published && ReferenceEquals(_owner.PendingAction, _card)
                && _card.Zone == CardZone.PendingAction && _card.Version == _version
                && _owner.FixedPendingReservation(_ledger) == _token;
            internal void Publish(TacticalFixedCardPaymentCommit payment)
            {
                if (!IsCurrent || !payment.IsPaid || payment.Record.ReservationToken != _token
                    || payment.Record.BattleCardInstance != _card.InstanceId)
                    throw new InvalidOperationException("fixed-joint:DeckParticipantChanged");
                _owner._resolvingLedger = _ledger; _owner._resolvingReservationId = _token;
                _owner._resolvingFixedPayment = payment;
                _owner.ResolvingAction = _card; _card.MoveTo(CardZone.Resolving);
                _owner.RecordCardPayment(payment.Record.Fees.ConfirmationCost);
                _owner.ClearPending(); _published = true;
            }
        }

        /// <summary>NoAlloc. Called only after the matching host is terminal and cleanup commands acknowledged.</summary>
        internal bool ReleaseFixedReservation(IMomentumLedger ledger, TacticalFixedCardPaymentCommit payment)
        {
            if (_resolvingReservationId == 0) return !payment.HasReservation;
            if (!ReferenceEquals(ledger, _resolvingLedger) || ResolvingAction == null
                || payment.Record.BattleCardInstance != ResolvingAction.InstanceId
                || payment.Record.ReservationToken != _resolvingReservationId || !payment.ReleaseUnexecutedBudget()) return false;
            _resolvingReservationId = 0; _resolvingLedger = null; return true;
        }

        private bool MarkFixedExhaustion()
        {
            var payment = _resolvingFixedPayment;
            return payment == null || !payment.Record.Assembly.DoubleEdgeParameters.Shattered
                || payment.Record.BattleCardInstance == ResolvingAction?.InstanceId
                    && payment.DoubleEdgeState != null && payment.DoubleEdgeState.MarkExhausted(payment.Record.DoubleEdgeKey);
        }
        /// <remarks>NoAlloc. Exact exit ACK after budget release. Does not open a refill window or finish a turn.</remarks>
        internal bool CompleteFixedAbnormal(TacticalFixedCardPaymentCommit payment)
        {
            if (!payment.Record.Assembly.HasDoubleEdge) return true;
            if (!ReferenceEquals(payment, _resolvingFixedPayment) || ResolvingAction == null
                || _resolvingReservationId != 0 || payment.Record.BattleCardInstance != ResolvingAction.InstanceId
                || !MarkFixedExhaustion()) return false;
            MoveAfterUse(ResolvingAction); ResolvingAction = null; _resolvingFixedPayment = null;
            return true;
        }
    }

    public sealed partial class TacticalTimeCoordinator
    {
        internal bool TryPrepareFixedTurn(TacticalDeckManager deck, out PreparedFixedTurn prepared)
        {
            prepared = null;
            if (!_initialized || _storyTransfer || Phase != TacticalTurnPhase.ReadyPaused || !IsWorldPaused
                || deck?.PendingAction == null || deck.ResolvingAction != null) return false;
            prepared = new PreparedFixedTurn(this, deck); return true;
        }
        internal sealed class PreparedFixedTurn
        {
            private readonly TacticalTimeCoordinator _owner;
            private readonly TacticalDeckManager _deck;
            private readonly ulong _turn;
            private readonly uint _pause, _suspend;
            private bool _published;
            internal PreparedFixedTurn(TacticalTimeCoordinator owner, TacticalDeckManager deck)
            { _owner = owner; _deck = deck; _turn = owner.CurrentTurnIndex; _pause = owner._pauseReasons; _suspend = owner._suspendReasons; }
            internal bool IsCurrent => !_published && !_owner._storyTransfer && _owner._initialized
                && _owner.Phase == TacticalTurnPhase.ReadyPaused && _owner.IsWorldPaused
                && _owner.CurrentTurnIndex == _turn && _owner._pauseReasons == _pause && _owner._suspendReasons == _suspend;
            internal void Publish()
            {
                if (!IsCurrent || _deck.ResolvingAction == null) throw new InvalidOperationException("fixed-joint:TimeParticipantChanged");
                var card = _deck.ResolvingAction;
                _owner._committedDeck = _deck; _owner._committedId = card.InstanceId;
                _owner._committedVersion = card.Version; _owner._actionStarted = false;
                _owner._detector.Reset(); _owner.Phase = TacticalTurnPhase.Executing; _published = true;
                // Last publication: all paid authority and the real host are visible before any world step.
                _owner.RequestResume(TacticalPauseReason.Ready);
            }
        }
    }
}
