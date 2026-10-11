using System;
using System.Threading;

namespace MxFramework.Platform
{
    /// <summary>
    /// Provides a fixed-capacity, loss-aware lifecycle fact buffer.
    /// </summary>
    /// <remarks>
    /// NoAlloc after construction. The default implementation requires one serialized producer and one drain owner;
    /// callers must marshal cross-thread host callbacks before reporting facts.
    /// </remarks>
    public sealed class ApplicationLifecycleBuffer : IApplicationLifecycleReporter, IApplicationLifecycleSource
    {
        private sealed class DrainOwner : IApplicationLifecycleDrainOwner
        {
            private readonly ApplicationLifecycleBuffer _buffer;
            public DrainOwner(ApplicationLifecycleBuffer buffer) => _buffer = buffer;
            public ApplicationLifecycleSnapshot Current => _buffer._current;
            public long DroppedChangeCount => _buffer._droppedChangeCount;
            public bool TryDequeue(out ApplicationLifecycleChange change) => _buffer.TryDequeueCore(out change);
        }

        private readonly ApplicationLifecycleChange[] _changes;
        private int _head;
        private int _count;
        private ApplicationLifecycleSnapshot _current;
        private long _droppedChangeCount;
        private bool _resyncPending;
        private long _lostRevisionStart;
        private long _lostRevisionEnd;
        private long _lostChangeCount;
        private bool _sawSuspendInLostRange;
        private bool _sawQuittingInLostRange;
        private DrainOwner _drainOwner;

        /// <summary>
        /// Creates a lifecycle buffer from an explicit revision-zero initial snapshot.
        /// </summary>
        /// <remarks>AllocByDesign during initialization for the fixed backing array.</remarks>
        public ApplicationLifecycleBuffer(ApplicationLifecycleSnapshot initialSnapshot, int capacity)
        {
            if (initialSnapshot.Revision != 0)
            {
                throw new ArgumentException(
                    "The initial lifecycle snapshot must use revision zero.",
                    nameof(initialSnapshot));
            }

            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _current = initialSnapshot;
            _changes = new ApplicationLifecycleChange[capacity];
        }

        /// <summary>
        /// Gets the fixed ordinary-change capacity.
        /// </summary>
        public int Capacity => _changes.Length;

        public bool TryClaimDrain(out IApplicationLifecycleDrainOwner drainOwner)
        {
            if (Volatile.Read(ref _drainOwner) != null)
            {
                drainOwner = null;
                return false;
            }
            var owner = new DrainOwner(this);
            if (Interlocked.CompareExchange(ref _drainOwner, owner, null) != null)
            {
                drainOwner = null;
                return false;
            }
            drainOwner = owner;
            return true;
        }

        public bool ReportFocus(bool hasFocus)
        {
            if (_current.HasFocus == hasFocus)
                return false;

            ApplicationLifecycleSnapshot next = NextSnapshot(
                hasFocus,
                _current.IsSuspended,
                _current.IsQuitting);
            ReportEffectiveChange(ApplicationLifecycleChangeKind.FocusChanged, next);
            return true;
        }

        public bool ReportSuspend(bool isSuspended)
        {
            if (_current.IsSuspended == isSuspended)
                return false;

            ApplicationLifecycleSnapshot next = NextSnapshot(
                _current.HasFocus,
                isSuspended,
                _current.IsQuitting);
            ReportEffectiveChange(ApplicationLifecycleChangeKind.SuspendChanged, next);
            return true;
        }

        public bool ReportQuitting()
        {
            if (_current.IsQuitting)
                return false;

            ApplicationLifecycleSnapshot next = NextSnapshot(
                _current.HasFocus,
                _current.IsSuspended,
                true);
            ReportEffectiveChange(ApplicationLifecycleChangeKind.Quitting, next);
            return true;
        }

        private bool TryDequeueCore(out ApplicationLifecycleChange change)
        {
            if (_resyncPending)
            {
                change = new ApplicationLifecycleChange(
                    ApplicationLifecycleChangeKind.StateResyncRequired,
                    _current,
                    _lostRevisionStart,
                    _lostRevisionEnd,
                    _lostChangeCount,
                    _sawSuspendInLostRange,
                    _sawQuittingInLostRange);
                ClearPendingResync();
                return true;
            }

            if (_count == 0)
            {
                change = default;
                return false;
            }

            change = _changes[_head];
            _changes[_head] = default;
            _head = (_head + 1) % _changes.Length;
            _count--;
            return true;
        }

        private ApplicationLifecycleSnapshot NextSnapshot(
            bool hasFocus,
            bool isSuspended,
            bool isQuitting)
        {
            long revision = SaturatingIncrement(_current.Revision);
            return new ApplicationLifecycleSnapshot(hasFocus, isSuspended, isQuitting, revision);
        }

        private void ReportEffectiveChange(
            ApplicationLifecycleChangeKind kind,
            ApplicationLifecycleSnapshot snapshot)
        {
            bool revisionExhausted = snapshot.Revision == _current.Revision;
            _current = snapshot;
            var change = new ApplicationLifecycleChange(kind, snapshot, 0, 0, 0, false, false);

            if (_resyncPending)
            {
                FoldIntoPendingResync(change);
                return;
            }

            if (revisionExhausted || _count == _changes.Length)
            {
                BeginPendingResync(change);
                return;
            }

            int tail = (_head + _count) % _changes.Length;
            _changes[tail] = change;
            _count++;
        }

        private void BeginPendingResync(ApplicationLifecycleChange overflowChange)
        {
            _resyncPending = true;
            _lostRevisionStart = _count == 0
                ? overflowChange.Snapshot.Revision
                : _changes[_head].Snapshot.Revision;
            _lostRevisionEnd = overflowChange.Snapshot.Revision;
            _lostChangeCount = SaturatingAdd(_count, 1);
            _sawSuspendInLostRange = overflowChange.Snapshot.IsSuspended;
            _sawQuittingInLostRange = overflowChange.Snapshot.IsQuitting;

            for (int i = 0; i < _count; i++)
            {
                int index = (_head + i) % _changes.Length;
                ApplicationLifecycleChange queued = _changes[index];
                _sawSuspendInLostRange |= queued.Snapshot.IsSuspended;
                _sawQuittingInLostRange |= queued.Snapshot.IsQuitting;
                _changes[index] = default;
            }

            _droppedChangeCount = SaturatingAdd(_droppedChangeCount, _lostChangeCount);
            _head = 0;
            _count = 0;
        }

        private void FoldIntoPendingResync(ApplicationLifecycleChange change)
        {
            _lostRevisionEnd = change.Snapshot.Revision;
            _lostChangeCount = SaturatingIncrement(_lostChangeCount);
            _droppedChangeCount = SaturatingIncrement(_droppedChangeCount);
            _sawSuspendInLostRange |= change.Snapshot.IsSuspended;
            _sawQuittingInLostRange |= change.Snapshot.IsQuitting;
        }

        private void ClearPendingResync()
        {
            _resyncPending = false;
            _lostRevisionStart = 0;
            _lostRevisionEnd = 0;
            _lostChangeCount = 0;
            _sawSuspendInLostRange = false;
            _sawQuittingInLostRange = false;
        }

        private static long SaturatingIncrement(long value)
        {
            return value == long.MaxValue ? long.MaxValue : value + 1;
        }

        private static long SaturatingAdd(long value, long increment)
        {
            if (value >= long.MaxValue - increment)
                return long.MaxValue;

            return value + increment;
        }
    }
}
