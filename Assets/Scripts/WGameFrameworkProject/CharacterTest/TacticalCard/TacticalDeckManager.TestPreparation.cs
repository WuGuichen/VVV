namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed partial class TacticalDeckManager
    {
        internal bool SupportsTestPreparation { get; }
        /// <summary>Actual card payments in this deck run, excluding released reservations and non-card grants.</summary>
        internal long PaidCardMomentum { get; private set; }

        private void RecordCardPayment(int amount)
        {
            PaidCardMomentum = PaidCardMomentum > long.MaxValue - amount ? long.MaxValue : PaidCardMomentum + amount;
        }

        /// <summary>Only the Stage may call after actor/duplicate validation. Does not enter Resolving, grant a turn or replenish.</summary>
        internal CardPlayFailure CommitPendingTestPreparation(IMomentumLedger ledger)
        {
            if (!SupportsTestPreparation && !(InteractionPreparationPayload.Matches(PendingAction?.Definition) && PendingAction.Definition.HasPublishedBehavior)) return CardPlayFailure.UnsupportedPayload;
            if (!_initialized || ResolvingAction != null) return CardPlayFailure.WrongPhase;
            if (PendingAction == null) return CardPlayFailure.NoPendingAction;
            if (!InteractionPreparationPayload.IsPreparation(PendingAction.Definition)) return CardPlayFailure.WrongCardKind;
            if (!ReferenceEquals(ledger, _pendingLedger)) return CardPlayFailure.InvalidLedger;
            if (!ledger.CommitReservation(_reservationId)) return CardPlayFailure.ReservationInvalid;
            TacticalCardInstance card = PendingAction;
            RecordCardPayment(card.Definition.BaseMomentumCost);
            ClearPending();
            MoveAfterUse(card);
            return CardPlayFailure.None;
        }
    }
}
