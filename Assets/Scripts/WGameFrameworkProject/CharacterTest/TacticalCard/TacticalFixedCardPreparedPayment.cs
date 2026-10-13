using System;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalFixedCardCommitManifest
    {
        /// <summary>Prepared core participant transaction: real ledger payment, complete accessory batch and
        /// confirmed journal. The Driver must additionally prepare deck/time and the actual execution host before
        /// invoking Publish. This participant alone does not start a host or authorize a playable joint commit.</summary>
        internal sealed class PreparedPaymentAndAccessories
        {
            private readonly TacticalFixedCardCommitManifest _prepared;
            private readonly TacticalCardInstance _card;
            private readonly MomentumLedger.PreparedFixedPayment _payment;
            private readonly TacticalInteractionEffectAuthority.PreparedConfirmedAccessoryBatch _effects;
            private readonly TacticalFixedCardPaymentCommit _receipt;
            private bool _published;

            private PreparedPaymentAndAccessories(TacticalFixedCardCommitManifest prepared,
                TacticalFixedCardCommitManifest confirmed, TacticalCardInstance card, MomentumLedger ledger,
                MomentumLedger.PreparedFixedPayment payment,
                TacticalInteractionEffectAuthority.PreparedConfirmedAccessoryBatch effects,
                CharacterTestFixedActionBudgetProof proof)
            {
                _prepared = prepared; _card = card;
                _payment = payment; _effects = effects;
                _receipt = new TacticalFixedCardPaymentCommit(confirmed, ledger, proof);
            }

            internal bool IsCurrent => !_published && _card.Zone == CardZone.PendingAction
                && _card.InstanceId == _prepared.BattleCardInstance && _card.Version == _prepared.CardVersion
                && ReferenceEquals(_card.Definition.FixedAssembly, _prepared.Assembly) && _payment.IsCurrent && _effects.IsCurrent;

            /// <remarks>LowFreqAlloc. All journal digests, arrays and effect capacity are prepared before the
            /// first ledger mutation. The provisional confirmed view stays private until actual payment succeeds.</remarks>
            internal static bool TryPrepare(TacticalFixedCardCommitManifest prepared, TacticalCardInstance card,
                MomentumLedger ledger, TacticalInteractionEffectAuthority effects,
                CharacterTestBehaviorExecutionProgram actualProgram, CharacterTestFixedActionBudgetProof proof,
                out PreparedPaymentAndAccessories transaction, out string issue)
            {
                transaction = null; issue = "fixed-payment:InvalidPreparedCommit";
                if (prepared == null || prepared.IsConfirmed || card == null || ledger == null || effects == null
                    || proof == null || !proof.Matches(actualProgram)
                    || !proof.UsesOnlyFrozenBindings(prepared.Assembly.Bindings)
                    || prepared.Fees.Timing != TacticalFixedCardPaymentTiming.CardAndAccessoriesThenActualActions
                    || prepared.Fees.MaximumActionBudget != proof.MaximumMomentum
                    || card.Zone != CardZone.PendingAction || card.Version != prepared.CardVersion
                    || card.InstanceId != prepared.BattleCardInstance || !ReferenceEquals(card.Definition.FixedAssembly, prepared.Assembly)
                    || effects.Stage != prepared.Stage || effects.Turn != prepared.Turn || effects.Actor != prepared.Actor
                    || effects.AccessoryReceiptSequence != prepared.ReceiptCursor) return false;
                if (!ledger.TryPrepareFixedPayment(prepared.ReservationToken, prepared.Fees.ConfirmationCost,
                    prepared.Fees.ReservedAfterConfirmation, out var payment))
                { issue = "fixed-payment:InsufficientOrChangedReservation"; return false; }

                var confirmed = new TacticalFixedCardCommitManifest(prepared.Assembly, prepared.Fees, prepared.Stage,
                    prepared.Turn, prepared.Actor, prepared.BattleCardInstance, prepared.CardVersion, prepared.CommitId,
                    prepared.ExecutionId, prepared.Frame, prepared.ReceiptCursor, prepared.ReservationToken,
                    prepared.Input, prepared._activations, true);
                var previous = effects.ConfirmedFixedAssembly;
                for (int p = 0; previous != null && p < previous.CommitCount; p++)
                {
                    var old = previous.CommitAt(p);
                    for (int a = 0; a < old.Assembly.Accessories.Count; a++)
                        for (int b = 0; b < prepared.Assembly.Accessories.Count; b++)
                            if (old.Assembly.Accessories[a].ItemInstance == prepared.Assembly.Accessories[b].ItemInstance
                                && old.Assembly.HeldCardInstance != prepared.Assembly.HeldCardInstance)
                            { issue = "fixed-payment:CopySharingPolicyUnspecified"; return false; }
                }
                var commits = new TacticalFixedCardCommitManifest[(previous?.CommitCount ?? 0) + 1];
                for (int i = 0; i < commits.Length - 1; i++) commits[i] = previous.CommitAt(i);
                commits[commits.Length - 1] = confirmed;
                if (!TacticalFixedAccessoryAdmissionAuthority.TryCreate(commits, true, out var authority)) return false;
                if (!effects.TryPrepareConfirmedAccessoryBatch(prepared._activations, authority, out var batch, out issue)) return false;
                transaction = new PreparedPaymentAndAccessories(prepared, confirmed, card, ledger, payment, batch, proof);
                issue = null; return true;
            }

            /// <summary>NoAlloc. Caller checks every prepared participant immediately before this non-reentrant
            /// boundary. A stale participant returns before any payment, activation or confirmed journal.</summary>
            internal bool TryPublish(out TacticalFixedCardPaymentCommit receipt)
            {
                receipt = null;
                if (!IsCurrent) return false;
                _payment.Publish(); _effects.Publish(); _receipt.MarkPaid(); _published = true;
                receipt = _receipt; return true;
            }
        }
    }

    /// <summary>Actual paid receipt, never an incoming DTO. Spending is only a trusted action-gateway seam;
    /// future wiring must correlate actor/generation/execution/command identity before calling it.</summary>
    internal sealed partial class TacticalFixedCardPaymentCommit
    {
        private readonly MomentumLedger _ledger;
        private readonly CharacterTestFixedActionBudgetProof _proof;
        internal TacticalFixedCardPaymentCommit(TacticalFixedCardCommitManifest record, MomentumLedger ledger,
            CharacterTestFixedActionBudgetProof proof) { Record = record; _ledger = ledger; _proof = proof; _actionScope = new CharacterTestFixedActionCostScope(this); }
        internal TacticalFixedCardCommitManifest Record { get; }
        internal TacticalDoubleEdgeBattleState DoubleEdgeState { get; set; }
        internal bool HasReservation => _ledger.HasFixedReservation(Record.ReservationToken);
        internal bool TrySpendActualAction(int actionId, int actualMomentumCost)
            => _paid && _proof.TryGetActionCost(actionId, out int proved) && proved == actualMomentumCost
                && _ledger.TrySpendFixedReservation(Record.ReservationToken, actualMomentumCost);
        internal bool ReleaseUnexecutedBudget() => _ledger.ReleaseReservation(Record.ReservationToken);
    }
}
