using System;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Lossless project control record. Identity fields retain their original ulong/uint widths.</summary>
    internal readonly struct TacticalQueuedCommand
    {
        public readonly long WorldBoundary;
        public readonly int Source;
        public readonly ulong Sequence;
        public readonly TacticalStageCommand Command;
        public TacticalQueuedCommand(long worldBoundary, int source, ulong sequence, in TacticalStageCommand command)
        { WorldBoundary = worldBoundary; Source = source; Sequence = sequence; Command = command; }
    }

    internal readonly struct TacticalCommandReceipt
    {
        public readonly TacticalQueuedCommand Submitted;
        public readonly TacticalStageRejection Rejection;
        public readonly ulong SnapshotRevision;
        public TacticalCommandReceipt(in TacticalQueuedCommand submitted, TacticalStageRejection rejection, ulong snapshotRevision)
        { Submitted = submitted; Rejection = rejection; SnapshotRevision = snapshotRevision; }
    }

    /// <summary>Single-threaded tactical control consumer, called from unscaled Update before any world tick.</summary>
    /// <remarks>NoAlloc after construction. Full command/result buffers apply backpressure; accepted entries are never overwritten.</remarks>
    internal sealed partial class TacticalCommandPump
    {
        private readonly TacticalStageFlowCoordinator _coordinator;
        private readonly TacticalQueuedCommand[] _pending;
        private readonly TacticalCommandReceipt[] _receipts;
        private readonly ulong[] _lastAcceptedSequence;
        private int _receiptHead;
        private bool _draining;
        public int PendingCount { get; private set; }
        public int ReceiptCount { get; private set; }
        public int Capacity => _pending.Length;
        internal bool HasSource(int source) => source >= 0 && source < _lastAcceptedSequence.Length;
        internal bool Uses(TacticalStageFlowCoordinator stage) => ReferenceEquals(_coordinator, stage);

        public TacticalCommandPump(TacticalStageFlowCoordinator coordinator, int capacity = 64, int sourceCapacity = 8)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (sourceCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(sourceCapacity));
            _pending = new TacticalQueuedCommand[capacity];
            _receipts = new TacticalCommandReceipt[capacity];
            _lastAcceptedSequence = new ulong[sourceCapacity];
        }

        public TacticalStageRejection TrySubmit(in TacticalStageCommand command, int source, ulong sequence)
        {
            if (_coordinator.LastCompletedFrame == long.MaxValue) return TacticalStageRejection.StaleFrame;
            var entry = new TacticalQueuedCommand(_coordinator.LastCompletedFrame + 1, source, sequence, in command);
            return TryEnqueue(in entry);
        }

        /// <summary>Replay/control adapter entry. Sources are published bounded slots; sequences start at one.</summary>
        public TacticalStageRejection TryEnqueue(in TacticalQueuedCommand entry)
        {
            if (entry.Source < 0 || entry.Source >= _lastAcceptedSequence.Length || entry.Sequence == 0 ||
                entry.Sequence <= _lastAcceptedSequence[entry.Source]) return TacticalStageRejection.InvalidSequence;
            if (entry.WorldBoundary < 0 || entry.WorldBoundary <= _coordinator.LastCompletedFrame)
                return TacticalStageRejection.StaleFrame;
            if (PendingCount == Capacity) return TacticalStageRejection.QueueFull;
            int index = PendingCount;
            while (index > 0 && Compare(in entry, _pending[index - 1]) < 0)
            { _pending[index] = _pending[index - 1]; index--; }
            _pending[index] = entry;
            PendingCount++;
            _lastAcceptedSequence[entry.Source] = entry.Sequence;
            return TacticalStageRejection.None;
        }

        /// <summary>Process F+1 control transactions while F remains the last completed world frame.</summary>
        public int Drain(int maxCommands = 32)
        {
            if (maxCommands <= 0) throw new ArgumentOutOfRangeException(nameof(maxCommands));
            if (_draining || _coordinator.LastCompletedFrame == long.MaxValue) return 0;
            _draining = true;
            try
            {
                _coordinator.ReconcilePendingAvailability();
                int processed = 0;
                long boundary = _coordinator.LastCompletedFrame + 1;
                while (PendingCount > 0 && processed < maxCommands && ReceiptCount < Capacity)
                {
                    TacticalQueuedCommand entry = _pending[0];
                    if (entry.WorldBoundary > boundary) break;
                    TacticalStageRejection result = entry.WorldBoundary == boundary
                        ? _coordinator.Apply(in entry.Command) : TacticalStageRejection.StaleFrame;
                    _receipts[(_receiptHead + ReceiptCount) % Capacity] =
                        new TacticalCommandReceipt(in entry, result, _coordinator.SnapshotRevision);
                    ReceiptCount++;
                    PendingCount--;
                    for (int i = 0; i < PendingCount; i++) _pending[i] = _pending[i + 1];
                    _pending[PendingCount] = default;
                    processed++;
                }
                return processed;
            }
            finally { _draining = false; }
        }

        public bool TryReadReceipt(out TacticalCommandReceipt receipt)
        {
            receipt = default;
            if (ReceiptCount == 0) return false;
            receipt = _receipts[_receiptHead];
            _receipts[_receiptHead] = default;
            _receiptHead = (_receiptHead + 1) % Capacity;
            ReceiptCount--;
            return true;
        }

        public TacticalQueuedCommand GetPending(int index)
        {
            if (index < 0 || index >= PendingCount) throw new ArgumentOutOfRangeException(nameof(index));
            return _pending[index];
        }

        public ulong GetLastAcceptedSequence(int source) => _lastAcceptedSequence[source];

        private static int Compare(in TacticalQueuedCommand left, in TacticalQueuedCommand right)
        {
            int value = left.WorldBoundary.CompareTo(right.WorldBoundary);
            if (value != 0) return value;
            value = left.Source.CompareTo(right.Source);
            return value != 0 ? value : left.Sequence.CompareTo(right.Sequence);
        }
    }
}
