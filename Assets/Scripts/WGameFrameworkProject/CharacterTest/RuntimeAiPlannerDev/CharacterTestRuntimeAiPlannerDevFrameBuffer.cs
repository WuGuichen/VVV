using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerDevFrameBuffer
    {
        public const int DefaultCapacity = 1800;

        private readonly CharacterTestRuntimeAiPlannerDevFrameSnapshot[] _frames;
        private int _start;
        private int _count;
        private int _selectedOffset;

        public CharacterTestRuntimeAiPlannerDevFrameBuffer(int capacity = DefaultCapacity)
        {
            _frames = new CharacterTestRuntimeAiPlannerDevFrameSnapshot[Math.Max(1, capacity)];
            _selectedOffset = -1;
        }

        public int Capacity => _frames.Length;
        public int Count => _count;
        public int SelectedIndex => _selectedOffset;

        public bool HasSelection => _selectedOffset >= 0 && _selectedOffset < _count;

        public CharacterTestRuntimeAiPlannerDevFrameSnapshot Latest
        {
            get
            {
                if (_count <= 0)
                    return CharacterTestRuntimeAiPlannerDevFrameSnapshot.Empty;

                return _frames[PhysicalIndex(_count - 1)] ?? CharacterTestRuntimeAiPlannerDevFrameSnapshot.Empty;
            }
        }

        public void Clear()
        {
            Array.Clear(_frames, 0, _frames.Length);
            _start = 0;
            _count = 0;
            _selectedOffset = -1;
        }

        public void Capture(
            int frame,
            float realtimeSeconds,
            CharacterTestRuntimeAiPlannerSnapshot runtimeAiPlannerSnapshot,
            bool truncateFuture = false,
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample runtimeEvents = default,
            CharacterTestRuntimeAiPlannerStateSnapshot runtimeAiPlannerStateSnapshot = null,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot preparedSnapshot = null)
        {
            if (truncateFuture && _selectedOffset >= 0 && _selectedOffset + 1 < _count)
                _count = _selectedOffset + 1;

            var snapshot = preparedSnapshot ?? new CharacterTestRuntimeAiPlannerDevFrameSnapshot(
                frame,
                realtimeSeconds,
                runtimeAiPlannerSnapshot,
                runtimeEvents,
                runtimeAiPlannerStateSnapshot);
            int existingOffset = FindOffsetByFrame(frame);
            if (existingOffset >= 0)
            {
                // A post-tick force capture refines the already captured authority frame.
                // Keep one immutable snapshot per RuntimeFrame and mirror Timeline.AddRecord's
                // identity semantics so frame counts and seeks remain truthful. A review-time
                // replacement does not discard later history unless truncateFuture already did so.
                _frames[PhysicalIndex(existingOffset)] = snapshot;
                _selectedOffset = existingOffset;
                return;
            }
            if (_count < _frames.Length)
            {
                _frames[PhysicalIndex(_count)] = snapshot;
                _count++;
            }
            else
            {
                _frames[_start] = snapshot;
                _start = (_start + 1) % _frames.Length;
            }

            _selectedOffset = _count - 1;
        }

        private int FindOffsetByFrame(int frame)
        {
            if (_count <= 0)
                return -1;

            CharacterTestRuntimeAiPlannerDevFrameSnapshot latest =
                _frames[PhysicalIndex(_count - 1)];
            if (latest != null && latest.Frame == frame)
                return _count - 1;

            CharacterTestRuntimeAiPlannerDevFrameSnapshot oldest = _frames[PhysicalIndex(0)];
            if (oldest == null || latest == null || frame < oldest.Frame || frame > latest.Frame)
                return -1;

            int low = 0;
            int high = _count - 2;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                CharacterTestRuntimeAiPlannerDevFrameSnapshot snapshot =
                    _frames[PhysicalIndex(middle)];
                if (snapshot == null)
                    return -1;
                if (snapshot.Frame == frame)
                    return middle;
                if (snapshot.Frame < frame)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            return -1;
        }

        public bool TryGetSelected(out CharacterTestRuntimeAiPlannerDevFrameSnapshot snapshot)
        {
            if (!HasSelection)
            {
                snapshot = CharacterTestRuntimeAiPlannerDevFrameSnapshot.Empty;
                return false;
            }

            snapshot = _frames[PhysicalIndex(_selectedOffset)]
                ?? CharacterTestRuntimeAiPlannerDevFrameSnapshot.Empty;
            return true;
        }

        public bool SelectLatest()
        {
            if (_count <= 0)
                return false;

            _selectedOffset = _count - 1;
            return true;
        }

        public bool SelectPrevious()
        {
            if (_count <= 0)
                return false;

            if (_selectedOffset < 0)
                _selectedOffset = _count - 1;
            else if (_selectedOffset > 0)
                _selectedOffset--;

            return true;
        }

        public bool SelectNext()
        {
            if (_count <= 0)
                return false;

            if (_selectedOffset < 0)
                _selectedOffset = _count - 1;
            else if (_selectedOffset + 1 < _count)
                _selectedOffset++;

            return true;
        }

        public bool SelectFrame(int frame)
        {
            for (int i = 0; i < _count; i++)
            {
                CharacterTestRuntimeAiPlannerDevFrameSnapshot snapshot = _frames[PhysicalIndex(i)];
                if (snapshot == null || snapshot.Frame != frame)
                    continue;

                _selectedOffset = i;
                return true;
            }

            return false;
        }

        public bool ReplaceFrame(
            int frame,
            float realtimeSeconds,
            CharacterTestRuntimeAiPlannerSnapshot runtimeAiPlannerSnapshot,
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample runtimeEvents = default,
            CharacterTestRuntimeAiPlannerStateSnapshot runtimeAiPlannerStateSnapshot = null)
        {
            for (int i = 0; i < _count; i++)
            {
                int index = PhysicalIndex(i);
                CharacterTestRuntimeAiPlannerDevFrameSnapshot snapshot = _frames[index];
                if (snapshot == null || snapshot.Frame != frame)
                    continue;

                _frames[index] = new CharacterTestRuntimeAiPlannerDevFrameSnapshot(
                    frame,
                    realtimeSeconds,
                    runtimeAiPlannerSnapshot,
                    runtimeEvents,
                    runtimeAiPlannerStateSnapshot);
                return true;
            }

            return false;
        }

        public CharacterTestRuntimeAiPlannerDevFrameSnapshot GetAt(int index)
        {
            if (index < 0 || index >= _count)
                return CharacterTestRuntimeAiPlannerDevFrameSnapshot.Empty;

            return _frames[PhysicalIndex(index)] ?? CharacterTestRuntimeAiPlannerDevFrameSnapshot.Empty;
        }

        internal CharacterTestRuntimeAiPlannerDevFrameBuffer Clone()
        {
            var clone = new CharacterTestRuntimeAiPlannerDevFrameBuffer(_frames.Length);
            for (int i = 0; i < _count; i++)
                clone._frames[i] = _frames[PhysicalIndex(i)];
            clone._start = 0;
            clone._count = _count;
            clone._selectedOffset = _selectedOffset;
            return clone;
        }

        private int PhysicalIndex(int logicalIndex)
        {
            return (_start + logicalIndex) % _frames.Length;
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerDevFrameSnapshot
    {
        public static readonly CharacterTestRuntimeAiPlannerDevFrameSnapshot Empty =
            new CharacterTestRuntimeAiPlannerDevFrameSnapshot(
                0,
                0f,
                new CharacterTestRuntimeAiPlannerSnapshot(
                    available: false,
                    profileId: string.Empty,
                    agents: Array.Empty<CharacterTestRuntimeAiPlannerAgentSnapshot>(),
                    validationReport: string.Empty));

        public CharacterTestRuntimeAiPlannerDevFrameSnapshot(
            int frame,
            float realtimeSeconds,
            CharacterTestRuntimeAiPlannerSnapshot runtimeAiPlannerSnapshot,
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample runtimeEvents = default,
            CharacterTestRuntimeAiPlannerStateSnapshot runtimeAiPlannerStateSnapshot = null)
        {
            Initialize(frame, realtimeSeconds, runtimeAiPlannerSnapshot, runtimeEvents, runtimeAiPlannerStateSnapshot);
        }

        internal CharacterTestRuntimeAiPlannerDevFrameSnapshot() { }
        private bool _initialized;
        internal bool IsInitialized => _initialized;
        internal void Initialize(
            int frame,
            float realtimeSeconds,
            CharacterTestRuntimeAiPlannerSnapshot runtimeAiPlannerSnapshot,
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample runtimeEvents = default,
            CharacterTestRuntimeAiPlannerStateSnapshot runtimeAiPlannerStateSnapshot = null)
        {
            if (_initialized) throw new InvalidOperationException("A published capture cannot be overwritten.");
            _initialized = true;
            Frame = frame;
            RealtimeSeconds = realtimeSeconds;
            RuntimeAiPlannerSnapshot = runtimeAiPlannerSnapshot;
            RuntimeEvents = runtimeEvents;
            RuntimeAiPlannerStateSnapshot = runtimeAiPlannerStateSnapshot;
        }

        public int Frame { get; private set; }
        public float RealtimeSeconds { get; private set; }
        public CharacterTestRuntimeAiPlannerSnapshot RuntimeAiPlannerSnapshot { get; private set; }
        public CharacterTestRuntimeAiPlannerStateSnapshot RuntimeAiPlannerStateSnapshot { get; private set; }
        public CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample RuntimeEvents { get; private set; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> Agents =>
            RuntimeAiPlannerSnapshot != null
                ? RuntimeAiPlannerSnapshot.Agents
                : Array.Empty<CharacterTestRuntimeAiPlannerAgentSnapshot>();
    }
}
