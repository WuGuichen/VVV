using MxFramework.Runtime;
using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Frame-local legacy queue plus an independent bounded reservation lane.</summary>
    public sealed class GameplayBuffRequestBuffer
    {
        private readonly List<QueuedRequest> _legacyRequests;
        private readonly ReservationSlot[] _reservationSlots;
        private readonly int[] _freeSlots;
        private readonly int[] _committedSlots;
        private int _freeCount;
        private int _preparedCount;
        private int _committedCount;
        private int _reservedOrdinalCount;
        private long _nextOrdinal;
        private bool _ordinalExhausted;

        /// <remarks>AllocByDesign. Preserves the historical unbounded legacy-only behavior.</remarks>
        public GameplayBuffRequestBuffer()
            : this(0, 0)
        {
        }

        /// <remarks>AllocByDesign. Preallocates the fixed reservation lane and optional legacy queue capacity.</remarks>
        public GameplayBuffRequestBuffer(int reservationCapacity, int legacyPrewarmCapacity = 0)
            : this(reservationCapacity, legacyPrewarmCapacity, 1L, false)
        {
        }

        internal GameplayBuffRequestBuffer(
            int reservationCapacity,
            int legacyPrewarmCapacity,
            long nextOrdinal,
            bool ordinalExhausted)
        {
            if (reservationCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(reservationCapacity));
            if (legacyPrewarmCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(legacyPrewarmCapacity));
            if (reservationCapacity > int.MaxValue - legacyPrewarmCapacity)
                throw new ArgumentOutOfRangeException(nameof(legacyPrewarmCapacity));
            if (nextOrdinal <= 0L)
                throw new ArgumentOutOfRangeException(nameof(nextOrdinal));
            if (ordinalExhausted && nextOrdinal != long.MaxValue)
                throw new ArgumentException("An exhausted ordinal must keep the long.MaxValue sentinel.", nameof(nextOrdinal));

            ReservationCapacity = reservationCapacity;
            LegacyPrewarmCapacity = legacyPrewarmCapacity;
            _legacyRequests = new List<QueuedRequest>(legacyPrewarmCapacity);
            _reservationSlots = new ReservationSlot[reservationCapacity];
            _freeSlots = new int[reservationCapacity];
            _committedSlots = new int[reservationCapacity];
            _freeCount = reservationCapacity;
            for (int i = 0; i < reservationCapacity; i++)
                _freeSlots[i] = reservationCapacity - i - 1;
            _nextOrdinal = nextOrdinal;
            _ordinalExhausted = ordinalExhausted;
        }

        public int Count => _legacyRequests.Count + _committedCount;
        public int ReservationCapacity { get; }
        public int PreparedReservationCount => _preparedCount;
        public int CommittedReservationCount => _committedCount;
        public int LegacyPrewarmCapacity { get; }
        internal int PrewarmedDrainCapacity => ReservationCapacity + LegacyPrewarmCapacity;

        public void EnqueueApply(
            GameplayEntityId targetEntityId,
            int buffId,
            int sourceId = 0,
            RuntimeTraceIdentity traceId = default,
            int commandId = 0)
        {
            EnqueueLegacy(new GameplayBuffApplicationRequest(
                GameplayBuffApplicationRequestKind.Apply,
                targetEntityId,
                buffId,
                sourceId,
                traceId,
                commandId));
        }

        /// <summary>Enqueues a legacy request with optional application-result correlation.</summary>
        /// <remarks>LowFreqAlloc on queue growth, identical ordering to EnqueueApply/EnqueueRemove.</remarks>
        public void Enqueue(in GameplayBuffApplicationRequest request) => EnqueueLegacy(request);

        public void EnqueueRemove(
            GameplayEntityId targetEntityId,
            int buffId,
            int sourceId = 0,
            RuntimeTraceIdentity traceId = default,
            int commandId = 0)
        {
            EnqueueLegacy(new GameplayBuffApplicationRequest(
                GameplayBuffApplicationRequestKind.Remove,
                targetEntityId,
                buffId,
                sourceId,
                traceId,
                commandId));
        }

        /// <summary>Reserves one invisible fixed slot and deterministic visible-order capacity.</summary>
        /// <remarks>NoAlloc after initialization. Returns false atomically when full or ordinal-exhausted.</remarks>
        public bool TryReserve(
            in GameplayBuffApplicationRequest request,
            out GameplayBuffRequestReservationToken token)
        {
            token = default;
            if (!IsValidRequest(in request) || _freeCount == 0 || !TryReserveOrdinal())
                return false;

            int slotIndex = _freeSlots[--_freeCount];
            ReservationSlot slot = _reservationSlots[slotIndex];
            if (slot.Generation == long.MaxValue)
            {
                _reservedOrdinalCount--;
                return false;
            }

            long generation = slot.Generation + 1L;
            slot.Generation = generation;
            slot.Ordinal = 0L;
            slot.Request = request;
            slot.State = ReservationState.Prepared;
            _reservationSlots[slotIndex] = slot;
            _preparedCount++;
            token = new GameplayBuffRequestReservationToken(slotIndex, generation);
            return true;
        }

        /// <remarks>NoAlloc and idempotent. Default, stale and foreign tokens fail closed.</remarks>
        public void AbortReservation(GameplayBuffRequestReservationToken token)
        {
            if (!TryGetPreparedSlot(token, out int slotIndex))
                return;

            ReservationSlot slot = _reservationSlots[slotIndex];
            slot.Request = default;
            slot.Ordinal = 0L;
            slot.State = ReservationState.Free;
            _reservationSlots[slotIndex] = slot;
            _preparedCount--;
            _reservedOrdinalCount--;
            if (slot.Generation != long.MaxValue)
                _freeSlots[_freeCount++] = slotIndex;
        }

        /// <remarks>NoAlloc, no-fail and exactly-once. Invalid or duplicate tokens are no-ops.</remarks>
        public void CommitReservation(
            GameplayBuffRequestReservationToken token,
            in GameplayBuffRequestCorrelation correlation)
        {
            if (!TryGetPreparedSlot(token, out int slotIndex))
                return;

            ReservationSlot slot = _reservationSlots[slotIndex];
            GameplayBuffApplicationRequest request = slot.Request;
            slot.Ordinal = TakeReservedOrdinal();
            slot.Request = new GameplayBuffApplicationRequest(
                request.Kind,
                request.TargetEntityId,
                request.BuffId,
                request.SourceId,
                request.TraceId,
                request.CommandId,
                correlation);
            slot.State = ReservationState.Committed;
            _reservationSlots[slotIndex] = slot;
            _preparedCount--;

            int insert = _committedCount;
            while (insert > 0 &&
                   _reservationSlots[_committedSlots[insert - 1]].Ordinal > slot.Ordinal)
            {
                _committedSlots[insert] = _committedSlots[insert - 1];
                insert--;
            }
            _committedSlots[insert] = slotIndex;
            _committedCount++;
        }

        /// <remarks>AllocByDesign. Prepared reservations are intentionally invisible.</remarks>
        public GameplayBuffApplicationRequest[] CreateSnapshot()
        {
            int count = Count;
            if (count == 0)
                return Array.Empty<GameplayBuffApplicationRequest>();

            var snapshot = new GameplayBuffApplicationRequest[count];
            CopyVisibleTo(snapshot, null);
            return snapshot;
        }

        /// <remarks>NoAlloc when the supplied output has sufficient prewarmed capacity.</remarks>
        public int DrainTo(IList<GameplayBuffApplicationRequest> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int count = Count;
            CopyVisibleTo(null, output);
            _legacyRequests.Clear();
            for (int i = 0; i < _committedCount; i++)
                ReleaseCommittedSlot(_committedSlots[i]);
            _committedCount = 0;
            return count;
        }

        /// <summary>Clears all transient work at an exclusive reset/restore boundary.</summary>
        /// <remarks>NoAlloc. Also resets visible ordering only after the buffer becomes empty.</remarks>
        public void Clear()
        {
            _legacyRequests.Clear();
            _preparedCount = 0;
            _committedCount = 0;
            _reservedOrdinalCount = 0;
            _freeCount = 0;
            for (int i = 0; i < _reservationSlots.Length; i++)
            {
                ReservationSlot slot = _reservationSlots[i];
                slot.Request = default;
                slot.Ordinal = 0L;
                slot.State = ReservationState.Free;
                _reservationSlots[i] = slot;
                if (slot.Generation != long.MaxValue)
                    _freeSlots[_freeCount++] = i;
            }
            _nextOrdinal = 1L;
            _ordinalExhausted = false;
        }

        private void EnqueueLegacy(GameplayBuffApplicationRequest request)
        {
            if (!TryTakeUnreservedOrdinal(out long ordinal))
                throw new InvalidOperationException(
                    "Gameplay Buff request ordering is exhausted while work is still pending.");
            _legacyRequests.Add(new QueuedRequest(ordinal, request));
        }

        private bool TryReserveOrdinal()
        {
            if (!EnsureOrdinalAvailable())
                return false;

            long remaining = long.MaxValue - _nextOrdinal + 1L;
            if (remaining <= _reservedOrdinalCount)
                return false;

            _reservedOrdinalCount++;
            return true;
        }

        private bool TryTakeUnreservedOrdinal(out long ordinal)
        {
            if (!EnsureOrdinalAvailable())
            {
                ordinal = 0L;
                return false;
            }

            long remaining = long.MaxValue - _nextOrdinal + 1L;
            if (remaining <= _reservedOrdinalCount)
            {
                ordinal = 0L;
                return false;
            }

            ordinal = TakeNextOrdinal();
            return true;
        }

        private long TakeReservedOrdinal()
        {
            _reservedOrdinalCount--;
            return TakeNextOrdinal();
        }

        private long TakeNextOrdinal()
        {
            long ordinal = _nextOrdinal;
            if (_nextOrdinal == long.MaxValue)
                _ordinalExhausted = true;
            else
                _nextOrdinal++;
            return ordinal;
        }

        private bool EnsureOrdinalAvailable()
        {
            if (!_ordinalExhausted)
                return true;
            if (!IsCompletelyEmpty)
                return false;

            _nextOrdinal = 1L;
            _ordinalExhausted = false;
            return true;
        }

        private bool IsCompletelyEmpty =>
            _legacyRequests.Count == 0 && _preparedCount == 0 && _committedCount == 0;

        private static bool IsValidRequest(in GameplayBuffApplicationRequest request)
        {
            return (request.Kind == GameplayBuffApplicationRequestKind.Apply ||
                    request.Kind == GameplayBuffApplicationRequestKind.Remove) &&
                   request.TargetEntityId.IsValid &&
                   request.BuffId > 0 &&
                   request.SourceId >= 0;
        }

        private bool TryGetPreparedSlot(
            GameplayBuffRequestReservationToken token,
            out int slotIndex)
        {
            slotIndex = token.SlotIndex;
            if (!token.IsValid || slotIndex < 0 || slotIndex >= _reservationSlots.Length)
                return false;
            ReservationSlot slot = _reservationSlots[slotIndex];
            return slot.State == ReservationState.Prepared && slot.Generation == token.Generation;
        }

        private void CopyVisibleTo(
            GameplayBuffApplicationRequest[] snapshot,
            IList<GameplayBuffApplicationRequest> output)
        {
            int legacyIndex = 0;
            int committedIndex = 0;
            int write = 0;
            while (legacyIndex < _legacyRequests.Count || committedIndex < _committedCount)
            {
                bool takeLegacy = committedIndex >= _committedCount ||
                                  (legacyIndex < _legacyRequests.Count &&
                                   _legacyRequests[legacyIndex].Ordinal <
                                   _reservationSlots[_committedSlots[committedIndex]].Ordinal);
                GameplayBuffApplicationRequest request;
                if (takeLegacy)
                {
                    request = _legacyRequests[legacyIndex++].Request;
                }
                else
                {
                    request = _reservationSlots[_committedSlots[committedIndex++]].Request;
                }

                if (snapshot != null)
                    snapshot[write] = request;
                else
                    output.Add(request);
                write++;
            }
        }

        private void ReleaseCommittedSlot(int slotIndex)
        {
            ReservationSlot slot = _reservationSlots[slotIndex];
            slot.Request = default;
            slot.Ordinal = 0L;
            slot.State = ReservationState.Free;
            _reservationSlots[slotIndex] = slot;
            if (slot.Generation != long.MaxValue)
                _freeSlots[_freeCount++] = slotIndex;
        }

        private readonly struct QueuedRequest
        {
            public QueuedRequest(long ordinal, GameplayBuffApplicationRequest request)
            {
                Ordinal = ordinal;
                Request = request;
            }

            public long Ordinal { get; }
            public GameplayBuffApplicationRequest Request { get; }
        }

        private struct ReservationSlot
        {
            public long Generation;
            public long Ordinal;
            public GameplayBuffApplicationRequest Request;
            public ReservationState State;
        }

        private enum ReservationState : byte
        {
            Free = 0,
            Prepared = 1,
            Committed = 2
        }
    }
}
