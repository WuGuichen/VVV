using System;
using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public sealed class RuntimeEventQueue<T> where T : struct
    {
        private const int DefaultInitialCapacity = 8;
        private static readonly Comparison<Entry> EntryComparison = CompareEntries;

        private readonly List<Entry> _pending;
        private readonly List<Entry> _drained;
        private long _nextSequence;

        public RuntimeEventQueue()
            : this(DefaultInitialCapacity)
        {
        }

        public RuntimeEventQueue(int initialCapacity)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _pending = new List<Entry>(initialCapacity);
            _drained = new List<Entry>(initialCapacity);
        }

        public int PendingCount => _pending.Count;

        public void Enqueue(RuntimeFrame frame, in T evt)
        {
            _pending.Add(new Entry(frame, _nextSequence, evt));
            _nextSequence++;
        }

        public int Drain(RuntimeFrame frame, List<T> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            _drained.Clear();
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                Entry entry = _pending[i];
                if (entry.Frame <= frame)
                {
                    _pending.RemoveAt(i);
                    _drained.Add(entry);
                }
            }

            _drained.Sort(EntryComparison);
            for (int i = 0; i < _drained.Count; i++)
            {
                output.Add(_drained[i].Event);
            }

            int drainedCount = _drained.Count;
            _drained.Clear();
            return drainedCount;
        }

        public RuntimeEventQueueSnapshot CreateSnapshot()
        {
            if (_pending.Count == 0)
            {
                return new RuntimeEventQueueSnapshot(
                    pendingCount: 0,
                    oldestFrame: RuntimeFrame.Zero,
                    newestFrame: RuntimeFrame.Zero,
                    nextSequence: _nextSequence,
                    eventTypeName: EventTypeName);
            }

            RuntimeFrame oldestFrame = _pending[0].Frame;
            RuntimeFrame newestFrame = _pending[0].Frame;
            for (int i = 1; i < _pending.Count; i++)
            {
                RuntimeFrame pendingFrame = _pending[i].Frame;
                if (pendingFrame < oldestFrame)
                {
                    oldestFrame = pendingFrame;
                }

                if (pendingFrame > newestFrame)
                {
                    newestFrame = pendingFrame;
                }
            }

            return new RuntimeEventQueueSnapshot(
                pendingCount: _pending.Count,
                oldestFrame: oldestFrame,
                newestFrame: newestFrame,
                nextSequence: _nextSequence,
                eventTypeName: EventTypeName);
        }

        public void Clear()
        {
            _pending.Clear();
            _drained.Clear();
            _nextSequence = 0L;
        }

        private static int CompareEntries(Entry left, Entry right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            if (frame != 0)
            {
                return frame;
            }

            return left.Sequence.CompareTo(right.Sequence);
        }

        private static string EventTypeName => typeof(T).FullName ?? typeof(T).Name;

        private readonly struct Entry
        {
            public Entry(RuntimeFrame frame, long sequence, in T evt)
            {
                Frame = frame;
                Sequence = sequence;
                Event = evt;
            }

            public RuntimeFrame Frame { get; }
            public long Sequence { get; }
            public T Event { get; }
        }
    }
}
