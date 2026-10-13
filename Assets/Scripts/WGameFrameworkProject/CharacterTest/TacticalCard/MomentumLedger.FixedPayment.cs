using System;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed partial class MomentumLedger
    {
        internal bool HasFixedReservation(ulong token) => token != 0 && token == _reservationId;

        /// <summary>NoAlloc. Spend only the named reservation; ordinary actions cannot borrow this credit.</summary>
        internal bool TrySpendFixedReservation(ulong token, int amount)
        {
            if (!HasFixedReservation(token) || amount < 0 || amount > Reserved) return false;
            Current -= amount; Reserved -= amount; return true;
        }

        /// <summary>Cold preparation of an exact payment/reservation replacement. Does not pay or reserve.
        /// The joint owner must publish every participant without a callback or simulation step.</summary>
        internal bool TryPrepareFixedPayment(ulong token, int confirmationCost, int actionBudget,
            out PreparedFixedPayment prepared)
        {
            prepared = null;
            if (!HasFixedReservation(token) || confirmationCost < 0 || actionBudget < 0
                || (long)confirmationCost + actionBudget > Current) return false;
            prepared = new PreparedFixedPayment(this, confirmationCost, actionBudget); return true;
        }

        internal sealed class PreparedFixedPayment
        {
            private readonly MomentumLedger _owner;
            private readonly int _current, _reserved, _confirmation, _budget;
            private readonly ulong _token, _next;
            private bool _published;
            internal PreparedFixedPayment(MomentumLedger owner, int confirmation, int budget)
            {
                _owner = owner; _current = owner.Current; _reserved = owner.Reserved;
                _token = owner._reservationId; _next = owner._nextReservationId;
                _confirmation = confirmation; _budget = budget;
            }
            internal bool IsCurrent => !_published && _owner.Current == _current && _owner.Reserved == _reserved
                && _owner._reservationId == _token && _owner._nextReservationId == _next;
            internal void Publish()
            {
                if (!IsCurrent) throw new InvalidOperationException("fixed-payment:PreparedLedgerChanged");
                _owner.Current -= _confirmation; _owner.Reserved = _budget;
                // Keep even a zero-budget token until the execution owner acknowledges cleanup.
                _published = true;
            }
        }
    }
}
