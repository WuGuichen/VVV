using System;
using System.Collections.Generic;

namespace MxFramework.Input
{
    public sealed class InputCommandQueue
    {
        private readonly List<InputCommand> _pending = new List<InputCommand>();
        private long _nextSequence;

        public long CurrentFrame { get; private set; }
        public int PendingCount => _pending.Count;

        public InputCommand Enqueue(InputCommand command)
        {
            if (!TryEnqueue(command, out InputCommand accepted))
            {
                throw new InvalidOperationException("Input command frame is earlier than the current input command queue frame.");
            }

            return accepted;
        }

        public bool TryEnqueue(InputCommand command, out InputCommand accepted)
        {
            if (command.Frame < CurrentFrame)
            {
                accepted = default;
                return false;
            }

            accepted = command.WithSequence(_nextSequence);
            _nextSequence++;
            _pending.Add(accepted);
            return true;
        }

        public IReadOnlyList<InputCommand> DrainForFrame(long frame)
        {
            var drained = new List<InputCommand>();
            DrainForFrame(frame, drained);
            return drained;
        }

        public int DrainForFrame(long frame, List<InputCommand> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (frame < CurrentFrame)
            {
                throw new InvalidOperationException("Input command queue cannot drain an earlier frame. CurrentFrame=" + CurrentFrame + ", Frame=" + frame + ".");
            }

            int start = destination.Count;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                InputCommand command = _pending[i];
                if (command.Frame <= frame)
                {
                    _pending.RemoveAt(i);
                    destination.Add(command);
                }
            }

            int count = destination.Count - start;
            if (count > 1)
            {
                destination.Sort(start, count, InputCommandComparer.Instance);
            }

            CurrentFrame = frame + 1L;
            return count;
        }

        public void Clear()
        {
            _pending.Clear();
        }

        public void Reset(long frame = 0L)
        {
            if (frame < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Input command queue frame cannot be negative.");
            }

            _pending.Clear();
            _nextSequence = 0L;
            CurrentFrame = frame;
        }

        private sealed class InputCommandComparer : IComparer<InputCommand>
        {
            public static readonly InputCommandComparer Instance = new InputCommandComparer();

            public int Compare(InputCommand x, InputCommand y)
            {
                int frame = x.Frame.CompareTo(y.Frame);
                if (frame != 0)
                {
                    return frame;
                }

                int source = x.SourceId.CompareTo(y.SourceId);
                if (source != 0)
                {
                    return source;
                }

                int intent = x.Intent.CompareTo(y.Intent);
                if (intent != 0)
                {
                    return intent;
                }

                int target = x.TargetId.CompareTo(y.TargetId);
                if (target != 0)
                {
                    return target;
                }

                return x.Sequence.CompareTo(y.Sequence);
            }
        }
    }
}
