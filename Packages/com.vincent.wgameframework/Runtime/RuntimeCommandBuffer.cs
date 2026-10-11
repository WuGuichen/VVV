using System;
using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public readonly struct RuntimeCommandBufferSnapshot
    {
        private readonly RuntimeCommand[] _pendingCommands;

        public RuntimeCommandBufferSnapshot(
            RuntimeFrame currentFrame,
            long nextSequence,
            RuntimeCommand[] pendingCommands)
        {
            CurrentFrame = currentFrame;
            NextSequence = nextSequence < 0L ? 0L : nextSequence;
            _pendingCommands = pendingCommands != null
                ? (RuntimeCommand[])pendingCommands.Clone()
                : Array.Empty<RuntimeCommand>();
        }

        public RuntimeFrame CurrentFrame { get; }
        public long NextSequence { get; }
        public IReadOnlyList<RuntimeCommand> PendingCommands => _pendingCommands;
        public int PendingCount => _pendingCommands != null ? _pendingCommands.Length : 0;

        internal RuntimeCommand[] CopyPendingCommands()
        {
            return _pendingCommands != null
                ? (RuntimeCommand[])_pendingCommands.Clone()
                : Array.Empty<RuntimeCommand>();
        }
    }

    public sealed class RuntimeCommandBuffer
    {
        public const int UnboundedPendingCapacity = -1;

        private static readonly IComparer<RuntimeCommand> CommandComparer = new RuntimeCommandComparer();
        private static readonly Comparison<RuntimeCommand> CommandComparison = CommandComparer.Compare;
        private const string PendingCapacityExceededMessage =
            "Runtime command buffer hard pending capacity is exhausted.";
        private const string SequenceExhaustedMessage =
            "Runtime command buffer sequence is exhausted.";

        private readonly List<RuntimeCommand> _pending;
        private readonly IRuntimeCommandValidator _validator;
        private readonly int _pendingCapacityLimit;
        private long _nextSequence;

        public RuntimeCommandBuffer()
            : this(null, RuntimeFrame.Zero, initialCapacity: 0)
        {
        }

        /// <summary>
        /// LowFreqAlloc at construction for the requested pending-command capacity.
        /// </summary>
        public RuntimeCommandBuffer(int initialCapacity)
            : this(null, RuntimeFrame.Zero, initialCapacity)
        {
        }

        public RuntimeCommandBuffer(IRuntimeCommandValidator validator)
            : this(validator, RuntimeFrame.Zero, initialCapacity: 0)
        {
        }

        public RuntimeCommandBuffer(IRuntimeCommandValidator validator, RuntimeFrame startFrame)
            : this(validator, startFrame, initialCapacity: 0)
        {
        }

        /// <summary>
        /// LowFreqAlloc at construction for the requested pending-command capacity.
        /// Enqueue is NoAlloc while pending command count stays within this capacity.
        /// </summary>
        public RuntimeCommandBuffer(
            IRuntimeCommandValidator validator,
            RuntimeFrame startFrame,
            int initialCapacity)
            : this(
                validator,
                startFrame,
                initialCapacity,
                UnboundedPendingCapacity)
        {
        }

        private RuntimeCommandBuffer(
            IRuntimeCommandValidator validator,
            RuntimeFrame startFrame,
            int initialCapacity,
            int pendingCapacityLimit)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            if (pendingCapacityLimit < UnboundedPendingCapacity)
                throw new ArgumentOutOfRangeException(nameof(pendingCapacityLimit));
            if (pendingCapacityLimit != UnboundedPendingCapacity &&
                initialCapacity > pendingCapacityLimit)
                throw new ArgumentOutOfRangeException(
                    nameof(initialCapacity),
                    "Initial capacity cannot exceed the hard pending capacity.");

            _pending = new List<RuntimeCommand>(initialCapacity);
            _validator = validator;
            _pendingCapacityLimit = pendingCapacityLimit;
            CurrentFrame = startFrame;
        }

        public RuntimeFrame CurrentFrame { get; private set; }
        public int PendingCount => _pending.Count;

        /// <summary>Gets whether this buffer has an immutable hard pending-command limit.</summary>
        /// <remarks>NoAlloc.</remarks>
        public bool IsPendingCapacityBounded =>
            _pendingCapacityLimit != UnboundedPendingCapacity;

        /// <summary>Gets the hard pending-command limit, or -1 for legacy unbounded buffers.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int PendingCapacityLimit => _pendingCapacityLimit;

        /// <remarks>NoAlloc. Internal exact-composition validator identity.</remarks>
        internal IRuntimeCommandValidator ValidatorIdentity => _validator;

        /// <summary>Creates a hard-bounded command buffer and preallocates its full limit.</summary>
        /// <remarks>AllocByDesign during initialization.</remarks>
        public static RuntimeCommandBuffer CreateBounded(int pendingCapacityLimit)
        {
            return CreateBounded(null, RuntimeFrame.Zero, pendingCapacityLimit);
        }

        /// <summary>Creates a hard-bounded command buffer and preallocates its full limit.</summary>
        /// <remarks>AllocByDesign during initialization.</remarks>
        public static RuntimeCommandBuffer CreateBounded(
            IRuntimeCommandValidator validator,
            RuntimeFrame startFrame,
            int pendingCapacityLimit)
        {
            if (pendingCapacityLimit < 0)
                throw new ArgumentOutOfRangeException(nameof(pendingCapacityLimit));

            return new RuntimeCommandBuffer(
                validator,
                startFrame,
                pendingCapacityLimit,
                pendingCapacityLimit);
        }

        public RuntimeCommandBufferSnapshot CaptureSnapshot()
        {
            return new RuntimeCommandBufferSnapshot(
                CurrentFrame,
                _nextSequence,
                _pending.ToArray());
        }

        public void RestoreSnapshot(RuntimeCommandBufferSnapshot snapshot)
        {
            if (IsPendingCapacityBounded &&
                snapshot.PendingCount > _pendingCapacityLimit)
            {
                throw new InvalidOperationException(
                    "Runtime command buffer snapshot exceeds the target hard pending capacity.");
            }

            _pending.Clear();
            RuntimeCommand[] pending = snapshot.CopyPendingCommands();
            for (int i = 0; i < pending.Length; i++)
                _pending.Add(pending[i]);
            CurrentFrame = snapshot.CurrentFrame;
            _nextSequence = snapshot.NextSequence;
        }

        public void Reset(RuntimeFrame frame)
        {
            _pending.Clear();
            CurrentFrame = frame;
            _nextSequence = 0L;
        }

        public RuntimeCommandValidationResult Enqueue(RuntimeCommand command)
        {
            if (command.Frame < CurrentFrame)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.LateCommand,
                    command,
                    CurrentFrame,
                    "Runtime command frame is earlier than the current command buffer frame."));
            }

            if (command.CommandId < 0)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidCommandId,
                    command,
                    CurrentFrame,
                    "Runtime command id cannot be negative."));
            }

            if (_validator != null)
            {
                RuntimeCommandValidationResult validation = _validator.Validate(command);
                if (!validation.Success)
                {
                    return validation;
                }
            }

            if (IsPendingCapacityBounded &&
                _pending.Count >= _pendingCapacityLimit)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.PendingCapacityExceeded,
                    command,
                    CurrentFrame,
                    PendingCapacityExceededMessage));
            }

            if (_nextSequence == long.MaxValue)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.SequenceExhausted,
                    command,
                    CurrentFrame,
                    SequenceExhaustedMessage));
            }

            RuntimeCommand accepted = command.WithSequence(_nextSequence);
            _nextSequence++;
            _pending.Add(accepted);
            return RuntimeCommandValidationResult.Accepted(accepted);
        }

        /// <remarks>NoAlloc. Preflights one atomic-batch item without mutating buffer state.</remarks>
        internal RuntimeCommandValidationResult ValidateAtomicBatchItem(
            RuntimeCommand command,
            int commandIndex)
        {
            if (command.Frame < CurrentFrame)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.LateCommand,
                    command,
                    CurrentFrame,
                    "Runtime command frame is earlier than the current command buffer frame."));
            }

            if (command.CommandId < 0)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidCommandId,
                    command,
                    CurrentFrame,
                    "Runtime command id cannot be negative."));
            }

            if (_validator != null)
            {
                RuntimeCommandValidationResult validation = _validator.Validate(command);
                if (!validation.Success)
                    return validation;
            }

            if ((long)_pending.Count + commandIndex >= _pendingCapacityLimit)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.PendingCapacityExceeded,
                    command,
                    CurrentFrame,
                    PendingCapacityExceededMessage));
            }

            long availableSequences = long.MaxValue - _nextSequence;
            if (commandIndex >= availableSequences)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.SequenceExhausted,
                    command,
                    CurrentFrame,
                    SequenceExhaustedMessage));
            }

            return RuntimeCommandValidationResult.Accepted(command);
        }

        /// <remarks>NoAlloc. Caller must complete all atomic-batch preflight before invoking.</remarks>
        internal void CommitAtomicBatch(ReadOnlySpan<RuntimeCommand> commands)
        {
            for (int i = 0; i < commands.Length; i++)
            {
                RuntimeCommand accepted = commands[i].WithSequence(_nextSequence);
                _nextSequence++;
                _pending.Add(accepted);
            }
        }

        public IReadOnlyList<RuntimeCommand> DrainForFrame(RuntimeFrame frame)
        {
            var drained = new List<RuntimeCommand>();
            DrainForFrame(frame, drained);
            return drained;
        }

        public int DrainForFrame(RuntimeFrame frame, List<RuntimeCommand> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (frame < CurrentFrame)
            {
                throw new InvalidOperationException("Runtime command buffer cannot drain an earlier frame. CurrentFrame=" + CurrentFrame + ", Frame=" + frame + ".");
            }

            int countBefore = destination.Count;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                RuntimeCommand command = _pending[i];
                if (command.Frame <= frame)
                {
                    _pending.RemoveAt(i);
                    destination.Add(command);
                }
            }

            int drainedCount = destination.Count - countBefore;
            if (drainedCount > 1)
            {
                if (countBefore == 0) destination.Sort(CommandComparison);
                else
                {
                    // Preserve the caller's existing prefix. Command batches are small; insertion needs no comparer adapter.
                    for (int i = countBefore + 1; i < destination.Count; i++)
                    {
                        RuntimeCommand value = destination[i]; int j = i - 1;
                        while (j >= countBefore && CommandComparison(destination[j], value) > 0)
                        { destination[j + 1] = destination[j]; j--; }
                        destination[j + 1] = value;
                    }
                }
            }
            CurrentFrame = frame.Next();
            return drainedCount;
        }

        public void Clear()
        {
            _pending.Clear();
        }

        public int RemoveWhere(Predicate<RuntimeCommand> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            int removed = 0;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (!predicate(_pending[i]))
                    continue;

                _pending.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        private static int CompareCommands(RuntimeCommand left, RuntimeCommand right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            if (frame != 0)
            {
                return frame;
            }

            int source = left.SourceId.CompareTo(right.SourceId);
            if (source != 0)
            {
                return source;
            }

            int command = left.CommandId.CompareTo(right.CommandId);
            if (command != 0)
            {
                return command;
            }

            int target = left.TargetId.CompareTo(right.TargetId);
            if (target != 0)
            {
                return target;
            }

            return left.Sequence.CompareTo(right.Sequence);
        }

        private sealed class RuntimeCommandComparer : IComparer<RuntimeCommand>
        {
            public int Compare(RuntimeCommand left, RuntimeCommand right)
            {
                return CompareCommands(left, right);
            }
        }
    }
}
