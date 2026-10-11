using System;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>
    /// Single-threaded ledger with one action reservation, including zero-cost reservations.
    /// Tokens are ledger-local; stale tokens never release or commit a later reservation.
    /// All operations are allocation-free. Invalid amounts fail without mutation.
    /// </summary>
    public interface IMomentumLedger
    {
        int Current { get; }
        int Reserved { get; }
        int Spendable { get; }
        bool TryReserve(int amount, out ulong reservationId);
        bool ReleaseReservation(ulong reservationId);
        bool CommitReservation(ulong reservationId);
        bool TrySpend(int amount);
        void Grant(int amount);
    }

    /// <summary>Maintains 0 &lt;= Reserved &lt;= Current &lt;= 100, with no hot-path allocations.</summary>
    public sealed partial class MomentumLedger : IMomentumLedger
    {
        public const int Maximum = 100;
        private ulong _nextReservationId;
        private ulong _reservationId;
        public int Current { get; private set; }
        public int Reserved { get; private set; }
        public int Spendable => Current - Reserved;

        internal void AddFixedAssemblyHash(ref MxFramework.Runtime.RuntimeHashAccumulator hash)
        {
            hash.AddInt(358, Current); hash.AddInt(359, Reserved);
            hash.AddLong(360, unchecked((long)_nextReservationId)); hash.AddLong(361, unchecked((long)_reservationId));
        }

        /// <summary>Initial resources are clamped into the supported range.</summary>
        public MomentumLedger(int current = 0)
        {
            Current = Math.Max(0, Math.Min(Maximum, current));
        }

        public bool TryReserve(int amount, out ulong reservationId)
        {
            reservationId = 0;
            if (_reservationId != 0 || amount < 0 || amount > Spendable || _nextReservationId == ulong.MaxValue)
                return false;
            _reservationId = ++_nextReservationId;
            Reserved = amount;
            reservationId = _reservationId;
            return true;
        }

        public bool ReleaseReservation(ulong reservationId)
        {
            if (reservationId == 0 || reservationId != _reservationId) return false;
            Reserved = 0;
            _reservationId = 0;
            return true;
        }

        public bool CommitReservation(ulong reservationId)
        {
            if (reservationId == 0 || reservationId != _reservationId) return false;
            Current -= Reserved;
            Reserved = 0;
            _reservationId = 0;
            return true;
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > Spendable) return false;
            Current -= amount;
            return true;
        }

        /// <summary>Negative grants are ignored; saturation avoids integer overflow.</summary>
        public void Grant(int amount)
        {
            if (amount <= 0) return;
            Current += Math.Min(amount, Maximum - Current);
        }
    }
}
