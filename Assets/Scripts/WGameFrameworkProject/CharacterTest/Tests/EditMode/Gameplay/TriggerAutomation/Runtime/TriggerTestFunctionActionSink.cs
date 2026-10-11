using System;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class TriggerTestFunctionActionSink :
        IGameplayTriggerActionSink
    {
        private readonly int[] _registeredCallIds;
        private readonly Slot[] _slots;
        private long _nextReservationSequence = 1L;
        private int _pendingCount;
        private int _reservedCount;

        internal TriggerTestFunctionActionSink(
            int[] registeredCallIds,
            int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _registeredCallIds = registeredCallIds == null
                ? Array.Empty<int>()
                : (int[])registeredCallIds.Clone();
            Array.Sort(_registeredCallIds);
            for (int i = 0; i < _registeredCallIds.Length; i++)
            {
                if (_registeredCallIds[i] <= 0 ||
                    i > 0 &&
                    _registeredCallIds[i - 1] == _registeredCallIds[i])
                {
                    throw new ArgumentException(
                        "Registered Trigger test call ids must be unique and positive.",
                        nameof(registeredCallIds));
                }
            }

            _slots = new Slot[capacity];
        }

        public GameplayTriggerActionKindId ActionKindId =>
            TriggerTestActionKindIds.FunctionCall;

        public GameplayTriggerDeliveryContract DeliveryContract =>
            GameplayTriggerDeliveryContract.SameFrameReserved;

        internal int PendingCount => _pendingCount;
        internal int ReservedCount => _reservedCount;
        internal int Capacity => _slots.Length;

        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TryPrepare(
            in GameplayTriggerActionPreparation preparation,
            out int token)
        {
            GameplayTriggerActionInstruction instruction =
                preparation.Instruction;
            if (instruction.ActionKindId != ActionKindId ||
                instruction.Payload0 <= 0L ||
                instruction.Payload0 > int.MaxValue ||
                instruction.Payload1 != 0L ||
                instruction.Payload2 != 0L)
            {
                token = 0;
                return false;
            }

            int callId = (int)instruction.Payload0;
            if (!ContainsCallId(callId))
            {
                token = 0;
                return false;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].State != SlotState.Free)
                    continue;

                _slots[i].State = SlotState.Reserved;
                _slots[i].CallId = callId;
                _slots[i].ReservationSequence = _nextReservationSequence++;
                _reservedCount++;
                token = i + 1;
                return true;
            }

            token = 0;
            return false;
        }

        /// <remarks>NoAlloc after initialization. Idempotent for stale tokens.</remarks>
        public void Abort(int token)
        {
            int index = token - 1;
            if (index < 0 ||
                index >= _slots.Length ||
                _slots[index].State != SlotState.Reserved)
            {
                return;
            }

            _slots[index] = default;
            _reservedCount--;
        }

        /// <remarks>NoAlloc after initialization. A valid prepared token commits without failure.</remarks>
        public void Commit(int token, in GameplayTriggerActionIntent intent)
        {
            int index = token - 1;
            if (index < 0 ||
                index >= _slots.Length ||
                _slots[index].State != SlotState.Reserved)
            {
                return;
            }

            _slots[index].Intent = intent;
            _slots[index].State = SlotState.Committed;
            _reservedCount--;
            _pendingCount++;
        }

        /// <remarks>NoAlloc after initialization. Returns committed calls in reservation order.</remarks>
        internal bool TryDequeue(out TriggerTestFunctionCallRecord record)
        {
            int selected = -1;
            long selectedSequence = long.MaxValue;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].State != SlotState.Committed ||
                    _slots[i].ReservationSequence >= selectedSequence)
                {
                    continue;
                }

                selected = i;
                selectedSequence = _slots[i].ReservationSequence;
            }

            if (selected < 0)
            {
                record = default;
                return false;
            }

            Slot slot = _slots[selected];
            record = new TriggerTestFunctionCallRecord(
                slot.CallId,
                slot.ReservationSequence,
                slot.Intent);
            _slots[selected] = default;
            _pendingCount--;
            return true;
        }

        private bool ContainsCallId(int callId)
        {
            int low = 0;
            int high = _registeredCallIds.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int comparison = _registeredCallIds[middle].CompareTo(callId);
                if (comparison == 0)
                    return true;
                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            return false;
        }

        private enum SlotState : byte
        {
            Free = 0,
            Reserved = 1,
            Committed = 2
        }

        private struct Slot
        {
            internal SlotState State;
            internal int CallId;
            internal long ReservationSequence;
            internal GameplayTriggerActionIntent Intent;
        }
    }

    internal readonly struct TriggerTestFunctionCallRecord
    {
        internal TriggerTestFunctionCallRecord(
            int callId,
            long reservationSequence,
            GameplayTriggerActionIntent intent)
        {
            CallId = callId;
            ReservationSequence = reservationSequence;
            Intent = intent;
        }

        internal int CallId { get; }
        internal long ReservationSequence { get; }
        internal GameplayTriggerActionIntent Intent { get; }
    }
}
