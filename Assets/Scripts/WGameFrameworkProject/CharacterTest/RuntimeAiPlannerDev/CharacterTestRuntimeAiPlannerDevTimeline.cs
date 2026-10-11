using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiPlannerDevTimelineMode
    {
        Setup,
        Live,
        Paused,
        Review,
        LoopReview,
        Seek
    }

    internal sealed class CharacterTestRuntimeAiPlannerDevTimeline
    {
        public const int DefaultCapacity = 1800;
        public const int DefaultCheckpointInterval = 30;

        private static readonly float[] PlaybackSpeeds = { 0.1f, 0.25f, 0.5f, 1f, 2f, 4f };

        private readonly List<CharacterTestRuntimeAiPlannerDevTimelineRecord> _records;
        private readonly List<CharacterTestRuntimeAiLabDecisionTraceHandle> _pinnedBranchFutureTraces;
        private readonly CharacterTestRuntimeAiLabDecisionTraceStore _decisionTraces;
        private readonly int _checkpointInterval;
        private readonly int _capacity;
        private int _selectedIndex = -1;
        private int _playbackSpeedIndex = 3;
        private long _contentRevision;
        private CharacterTestRuntimeAiLabTimelineSessionIdentity _sessionIdentity;
        private bool _branchDecisionTraceTransactionActive;
        private int _branchDecisionTraceBaseFrame = -1;

        public CharacterTestRuntimeAiPlannerDevTimeline(
            int capacity = DefaultCapacity,
            int checkpointInterval = DefaultCheckpointInterval)
            : this(capacity, checkpointInterval, null)
        {
        }

        private CharacterTestRuntimeAiPlannerDevTimeline(
            int capacity,
            int checkpointInterval,
            CharacterTestRuntimeAiLabDecisionTraceStore decisionTraces)
        {
            _capacity = Math.Max(1, capacity);
            _checkpointInterval = Math.Max(1, checkpointInterval);
            _records = new List<CharacterTestRuntimeAiPlannerDevTimelineRecord>(_capacity);
            _pinnedBranchFutureTraces = new List<CharacterTestRuntimeAiLabDecisionTraceHandle>(_capacity);
            _decisionTraces = decisionTraces ?? new CharacterTestRuntimeAiLabDecisionTraceStore(_capacity);
            Mode = CharacterTestRuntimeAiPlannerDevTimelineMode.Setup;
        }

        public int Capacity => _capacity;
        public int CheckpointInterval => _checkpointInterval;
        public int Count => _records.Count;
        public int SelectedIndex => _selectedIndex;
        public long ContentRevision => _contentRevision;
        public CharacterTestRuntimeAiLabTimelineSessionIdentity SessionIdentity => _sessionIdentity;
        public bool HasSelection => _selectedIndex >= 0 && _selectedIndex < _records.Count;
        public CharacterTestRuntimeAiPlannerDevTimelineMode Mode { get; private set; }
        public float PlaybackSpeed => PlaybackSpeeds[_playbackSpeedIndex];
        public int PlaybackSpeedIndex => _playbackSpeedIndex;
        public bool LoopEnabled { get; private set; }
        public int LoopStartFrame { get; private set; } = -1;
        public int LoopEndFrame { get; private set; } = -1;
        public int EditBranchStartFrame { get; private set; } = -1;
        public bool HasLoopRange => LoopStartFrame >= 0 && LoopEndFrame > LoopStartFrame;
        internal bool BranchDecisionTraceTransactionActive => _branchDecisionTraceTransactionActive;

        public int LatestFrame
        {
            get
            {
                return _records.Count > 0
                    ? _records[_records.Count - 1].Frame
                    : 0;
            }
        }

        public int SelectedFrame
        {
            get
            {
                return HasSelection
                    ? _records[_selectedIndex].Frame
                    : 0;
            }
        }

        public int RetainedStartFrame => _records.Count > 0 ? _records[0].Frame : 0;

        public IReadOnlyList<CharacterTestRuntimeAiPlannerDevTimelineRecord> Records => _records;

        public void BeginSession(CharacterTestRuntimeAiLabTimelineSessionIdentity sessionIdentity)
        {
            if (!sessionIdentity.IsValid)
                throw new ArgumentException("Timeline session identity is invalid.", nameof(sessionIdentity));
            Clear();
            _sessionIdentity = sessionIdentity;
        }

        public void Clear()
        {
            for (int i = 0; i < _records.Count; i++)
                _decisionTraces.Release(_records[i].DecisionTraceHandle);
            for (int i = 0; i < _pinnedBranchFutureTraces.Count; i++)
            {
                _decisionTraces.Release(_pinnedBranchFutureTraces[i]);
                _decisionTraces.Unpin(_pinnedBranchFutureTraces[i]);
            }
            _records.Clear();
            _pinnedBranchFutureTraces.Clear();
            _branchDecisionTraceTransactionActive = false;
            _branchDecisionTraceBaseFrame = -1;
            _selectedIndex = -1;
            _playbackSpeedIndex = 3;
            LoopEnabled = false;
            LoopStartFrame = -1;
            LoopEndFrame = -1;
            EditBranchStartFrame = -1;
            _sessionIdentity = default;
            Mode = CharacterTestRuntimeAiPlannerDevTimelineMode.Setup;
            _contentRevision++;
        }

        public void SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode mode)
        {
            Mode = mode;
        }

        public float CyclePlaybackSpeed()
        {
            _playbackSpeedIndex++;
            if (_playbackSpeedIndex >= PlaybackSpeeds.Length)
                _playbackSpeedIndex = 0;
            _contentRevision++;
            return PlaybackSpeed;
        }

        public float AdjustPlaybackSpeed(int direction)
        {
            if (direction == 0)
                return PlaybackSpeed;

            int adjustedIndex = Math.Clamp(
                _playbackSpeedIndex + Math.Sign(direction),
                0,
                PlaybackSpeeds.Length - 1);
            if (adjustedIndex != _playbackSpeedIndex)
            {
                _playbackSpeedIndex = adjustedIndex;
                _contentRevision++;
            }
            return PlaybackSpeed;
        }

        public void SetPlaybackSpeed(float speed)
        {
            int bestIndex = 3;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < PlaybackSpeeds.Length; i++)
            {
                float delta = Math.Abs(PlaybackSpeeds[i] - speed);
                if (delta >= bestDelta)
                    continue;

                bestIndex = i;
                bestDelta = delta;
            }

            if (bestIndex != _playbackSpeedIndex)
            {
                _playbackSpeedIndex = bestIndex;
                _contentRevision++;
            }
        }

        public void MarkEditBranchStart()
        {
            EditBranchStartFrame = HasSelection ? SelectedFrame : LatestFrame;
        }

        public void ClearEditBranchStart()
        {
            EditBranchStartFrame = -1;
        }

        public bool SetLoopStartToSelection()
        {
            if (!HasSelection)
                return false;

            LoopStartFrame = SelectedFrame;
            if (LoopEndFrame >= 0 && LoopEndFrame <= LoopStartFrame)
                LoopEndFrame = -1;
            LoopEnabled = HasLoopRange && LoopEnabled;
            return true;
        }

        public bool SetLoopEndToSelection()
        {
            if (!HasSelection)
                return false;

            int frame = SelectedFrame;
            if (LoopStartFrame < 0 || frame <= LoopStartFrame)
                return false;

            LoopEndFrame = frame;
            return true;
        }

        public bool ToggleLoop()
        {
            if (!HasLoopRange)
            {
                LoopEnabled = false;
                return false;
            }

            LoopEnabled = !LoopEnabled;
            return true;
        }

        public bool TryValidateLoopRange(out string diagnostic)
        {
            if (!HasLoopRange)
            {
                diagnostic = "Set loop A and B before enabling loop.";
                return false;
            }

            int startIndex = FindIndexByFrame(LoopStartFrame);
            int endIndex = FindIndexByFrame(LoopEndFrame);
            if (startIndex < 0 || endIndex < 0 || endIndex <= startIndex)
            {
                diagnostic = "Loop A-B is outside the retained timeline.";
                return false;
            }

            int expectedFrame = LoopStartFrame;
            for (int i = startIndex; i <= endIndex; i++, expectedFrame++)
            {
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = _records[i];
                if (record.Frame != expectedFrame)
                {
                    diagnostic = "Loop A-B is missing RuntimeFrame " + expectedFrame + ".";
                    return false;
                }
            }

            diagnostic = string.Empty;
            return true;
        }

        public CharacterTestRuntimeAiPlannerDevTimelineRecord AddRecord(
            int frame,
            float realtimeSeconds,
            double elapsedSeconds,
            CharacterTestRuntimeAiPlannerSnapshot runtimeAiPlannerSnapshot,
            IReadOnlyList<RuntimeCommand> drainedCommands,
            CharacterTestAuthorityRollbackCheckpoint authorityCheckpoint,
            long stateHash,
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample runtimeEvents = default,
            CharacterTestAnimationInputPresentationState[] animationInputs = null,
            CharacterTestRuntimeAiPlannerStateSnapshot runtimeAiPlannerStateSnapshot = null,
            CharacterTestRuntimeAiLabDecisionTraceHandle decisionTraceHandle = default,
            bool hasReplayAuthorityEvidence = false,
            int replayAuthorityFrame = 0,
            long replayAuthorityHash = 0L,
            CharacterTestRuntimeAiLabFrameCaptureStorage prepared = null)
        {
            if (frame <= 0)
                frame = 0;

            TruncateRecordsAfterSelectedIfReviewing();

            int existingIndex = FindIndexByFrame(frame);
            if (existingIndex >= 0)
            {
                RemoveRange(existingIndex, _records.Count - existingIndex);
                if (_selectedIndex >= _records.Count)
                    _selectedIndex = _records.Count - 1;
            }

            bool hasCheckpoint = authorityCheckpoint != null;
            var frameSnapshot = prepared?.Frame ?? new CharacterTestRuntimeAiPlannerDevFrameSnapshot(
                frame,
                realtimeSeconds,
                runtimeAiPlannerSnapshot,
                runtimeEvents,
                runtimeAiPlannerStateSnapshot);
            var record = prepared?.Record ?? new CharacterTestRuntimeAiPlannerDevTimelineRecord();
            record.Initialize(
                frame,
                realtimeSeconds,
                elapsedSeconds,
                frameSnapshot,
                drainedCommands,
                authorityCheckpoint,
                hasCheckpoint,
                stateHash,
                animationInputs,
                _contentRevision + 1L,
                decisionTraceHandle,
                hasReplayAuthorityEvidence,
                replayAuthorityFrame,
                replayAuthorityHash, prepared);
            _records.Add(record);
            TrimOverflow();
            _selectedIndex = _records.Count - 1;
            _contentRevision++;
            return record;
        }

        public bool ShouldCreateCheckpoint(int frame)
        {
            if (_records.Count == 0)
                return true;
            int existingIndex = FindIndexByFrame(frame);
            if (existingIndex >= 0 && _records[existingIndex].HasCheckpoint)
                return true;

            return frame % _checkpointInterval == 0;
        }

        public bool SelectLatest()
        {
            if (_records.Count <= 0)
                return false;

            _selectedIndex = _records.Count - 1;
            return true;
        }

        public bool SelectFrame(int frame)
        {
            int index = FindIndexByFrame(frame);
            if (index < 0)
                return false;

            _selectedIndex = index;
            return true;
        }

        internal CharacterTestRuntimeAiPlannerDevTimeline Clone()
        {
            var clone = new CharacterTestRuntimeAiPlannerDevTimeline(
                _capacity,
                _checkpointInterval,
                _decisionTraces.Clone());
            clone._records.AddRange(_records);
            clone._selectedIndex = _selectedIndex;
            clone._playbackSpeedIndex = _playbackSpeedIndex;
            clone._contentRevision = _contentRevision;
            clone._sessionIdentity = _sessionIdentity;
            clone.Mode = Mode;
            clone.LoopEnabled = LoopEnabled;
            clone.LoopStartFrame = LoopStartFrame;
            clone.LoopEndFrame = LoopEndFrame;
            clone.EditBranchStartFrame = EditBranchStartFrame;
            clone._branchDecisionTraceTransactionActive = _branchDecisionTraceTransactionActive;
            clone._branchDecisionTraceBaseFrame = _branchDecisionTraceBaseFrame;
            clone._pinnedBranchFutureTraces.AddRange(_pinnedBranchFutureTraces);
            return clone;
        }

        internal bool BeginBranchDecisionTraceTransaction(int baseFrame)
        {
            if (_branchDecisionTraceTransactionActive
                || !HasSelection
                || SelectedFrame != baseFrame
                || _selectedIndex >= _records.Count - 1)
            {
                return false;
            }

            _pinnedBranchFutureTraces.Clear();
            for (int i = _selectedIndex + 1; i < _records.Count; i++)
            {
                CharacterTestRuntimeAiLabDecisionTraceHandle handle = _records[i].DecisionTraceHandle;
                if (!handle.IsValid)
                    continue;
                if (!_decisionTraces.Pin(handle))
                {
                    for (int j = 0; j < _pinnedBranchFutureTraces.Count; j++)
                        _decisionTraces.Unpin(_pinnedBranchFutureTraces[j]);
                    _pinnedBranchFutureTraces.Clear();
                    return false;
                }
                _pinnedBranchFutureTraces.Add(handle);
            }

            _branchDecisionTraceTransactionActive = true;
            _branchDecisionTraceBaseFrame = baseFrame;
            return true;
        }

        internal bool CommitBranchDecisionTraceTransaction()
        {
            if (!_branchDecisionTraceTransactionActive)
                return false;

            bool promoted = false;
            for (int i = 0; i < _records.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = _records[i];
                if (record.Frame <= _branchDecisionTraceBaseFrame)
                    continue;
                CharacterTestRuntimeAiLabDecisionTraceHandle handle = record.DecisionTraceHandle;
                if (handle.IsValid && !_decisionTraces.PromoteStaging(handle))
                    return false;
                promoted = true;
            }
            if (!promoted)
                return false;

            for (int i = 0; i < _pinnedBranchFutureTraces.Count; i++)
                _decisionTraces.Unpin(_pinnedBranchFutureTraces[i]);
            _pinnedBranchFutureTraces.Clear();
            _branchDecisionTraceTransactionActive = false;
            _branchDecisionTraceBaseFrame = -1;
            _contentRevision++;
            return true;
        }

        internal CharacterTestRuntimeAiLabDecisionTraceWriter BeginDecisionTraceCapture(
            long sessionGeneration,
            int frame,
            out CharacterTestRuntimeAiLabDecisionTraceHandle handle,
            int decisionFrame = -1)
        {
            return _decisionTraces.BeginCapture(
                sessionGeneration,
                frame,
                out handle,
                _branchDecisionTraceTransactionActive,
                decisionFrame);
        }

        internal long ComputeDecisionTraceRecoveryHash()
        {
            unchecked
            {
                long hash = _decisionTraces.ComputeRecoveryHash();
                hash = (hash * 397L) ^ (_branchDecisionTraceTransactionActive ? 1L : 0L);
                hash = (hash * 397L) ^ _branchDecisionTraceBaseFrame;
                hash = (hash * 397L) ^ _pinnedBranchFutureTraces.Count;
                for (int i = 0; i < _pinnedBranchFutureTraces.Count; i++)
                {
                    CharacterTestRuntimeAiLabDecisionTraceHandle handle = _pinnedBranchFutureTraces[i];
                    hash = (hash * 397L) ^ handle.Slot;
                    hash = (hash * 397L) ^ handle.SlotGeneration;
                    hash = (hash * 397L) ^ handle.SessionGeneration;
                    hash = (hash * 397L) ^ handle.Frame;
                    hash = (hash * 397L) ^ handle.DecisionFrame;
                }
                return hash;
            }
        }

        internal void ReleaseDecisionTrace(CharacterTestRuntimeAiLabDecisionTraceHandle handle)
        {
            _decisionTraces.Release(handle);
        }

        internal bool TryGetDecisionTrace(
            CharacterTestRuntimeAiPlannerDevTimelineRecord record,
            int entityId,
            out CharacterTestRuntimeAiLabDecisionTraceAgentView view)
        {
            view = default;
            return record != null
                && _decisionTraces.TryGetAgent(record.DecisionTraceHandle, entityId, out view);
        }

        public bool TryResolveNearestFrame(int frame, out int resolvedFrame, out bool clamped)
        {
            resolvedFrame = 0;
            clamped = false;
            if (_records.Count <= 0)
                return false;

            if (frame <= _records[0].Frame)
            {
                resolvedFrame = _records[0].Frame;
                clamped = frame != resolvedFrame;
                return true;
            }

            int lastIndex = _records.Count - 1;
            if (frame >= _records[lastIndex].Frame)
            {
                resolvedFrame = _records[lastIndex].Frame;
                clamped = frame != resolvedFrame;
                return true;
            }

            int previousFrame = _records[0].Frame;
            for (int i = 1; i < _records.Count; i++)
            {
                int nextFrame = _records[i].Frame;
                if (frame == nextFrame)
                {
                    resolvedFrame = frame;
                    return true;
                }
                if (frame < nextFrame)
                {
                    resolvedFrame = frame - previousFrame <= nextFrame - frame
                        ? previousFrame
                        : nextFrame;
                    clamped = resolvedFrame != frame;
                    return true;
                }
                previousFrame = nextFrame;
            }

            resolvedFrame = _records[lastIndex].Frame;
            clamped = true;
            return true;
        }

        public bool SelectRelativeFrame(int delta)
        {
            if (_records.Count <= 0)
                return false;

            int index = HasSelection ? _selectedIndex : _records.Count - 1;
            index += delta;
            if (index < 0)
                index = 0;
            else if (index >= _records.Count)
                index = _records.Count - 1;

            _selectedIndex = index;
            return true;
        }

        public bool TryGetSelected(out CharacterTestRuntimeAiPlannerDevTimelineRecord record)
        {
            if (!HasSelection)
            {
                record = null;
                return false;
            }

            record = _records[_selectedIndex];
            return true;
        }

        public bool TryGetLatest(out CharacterTestRuntimeAiPlannerDevTimelineRecord record)
        {
            if (_records.Count <= 0)
            {
                record = null;
                return false;
            }

            record = _records[_records.Count - 1];
            return true;
        }

        public bool TryGetFrame(int frame, out CharacterTestRuntimeAiPlannerDevTimelineRecord record)
        {
            int index = FindIndexByFrame(frame);
            if (index < 0)
            {
                record = null;
                return false;
            }

            record = _records[index];
            return true;
        }

        public bool TryFindCheckpointAtOrBefore(
            int frame,
            out CharacterTestRuntimeAiPlannerDevTimelineRecord checkpoint)
        {
            for (int i = _records.Count - 1; i >= 0; i--)
            {
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = _records[i];
                if (record.Frame > frame || !record.HasCheckpoint)
                    continue;

                checkpoint = record;
                return true;
            }

            checkpoint = null;
            return false;
        }

        public int FindIndexByFrame(int frame)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].Frame == frame)
                    return i;
            }

            return -1;
        }

        private void TruncateRecordsAfterSelectedIfReviewing()
        {
            if (EditBranchStartFrame < 0)
                return;
            if (!HasSelection)
                return;
            if (_selectedIndex >= _records.Count - 1)
                return;
            if (SelectedFrame != EditBranchStartFrame)
                return;

            RemoveRange(_selectedIndex + 1, _records.Count - _selectedIndex - 1);
            if (LoopStartFrame > LatestFrame)
            {
                LoopEnabled = false;
                LoopStartFrame = -1;
                LoopEndFrame = -1;
            }
            else if (LoopEndFrame > LatestFrame)
            {
                LoopEnabled = false;
                LoopEndFrame = -1;
            }
        }

        private void TrimOverflow()
        {
            while (_records.Count > _capacity)
            {
                _decisionTraces.Release(_records[0].DecisionTraceHandle);
                _records.RemoveAt(0);
                if (_selectedIndex > 0)
                    _selectedIndex--;
                else if (_selectedIndex == 0)
                    _selectedIndex = _records.Count > 0 ? 0 : -1;
            }
        }

        private void RemoveRange(int index, int count)
        {
            if (count <= 0)
                return;

            int end = index + count;
            for (int i = index; i < end; i++)
                _decisionTraces.Release(_records[i].DecisionTraceHandle);
            _records.RemoveRange(index, count);
        }

    }

    internal readonly struct CharacterTestRuntimeAiLabFrozenArray<T>
    {
        private readonly T[] _items;
        private readonly int _count;

        internal CharacterTestRuntimeAiLabFrozenArray(IReadOnlyList<T> source)
        {
            int count = source?.Count ?? 0;
            _count = count;
            if (count <= 0)
            {
                _items = Array.Empty<T>();
                return;
            }

            _items = new T[count];
            for (int i = 0; i < count; i++)
                _items[i] = source[i];
        }

        internal CharacterTestRuntimeAiLabFrozenArray(T[] owned, int count)
        {
            if (owned == null || count < 0 || count > owned.Length) throw new ArgumentOutOfRangeException(nameof(count));
            _items = owned;
            _count = count;
        }

        internal int Count => _count;
        internal T this[int index] => (uint)index < (uint)_count ? _items[index] : throw new ArgumentOutOfRangeException(nameof(index));

        internal T[] ToArray()
        {
            int count = Count;
            if (count == 0)
                return Array.Empty<T>();
            var copy = new T[count];
            Array.Copy(_items, copy, count);
            return copy;
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerDevTimelineRecord
    {
        public CharacterTestRuntimeAiPlannerDevTimelineRecord(
            int frame,
            float realtimeSeconds,
            double elapsedSeconds,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot frameSnapshot,
            IReadOnlyList<RuntimeCommand> drainedCommands,
            CharacterTestAuthorityRollbackCheckpoint authorityCheckpoint,
            bool hasCheckpoint,
            long stateHash,
            IReadOnlyList<CharacterTestAnimationInputPresentationState> animationInputs = null,
            long recordGeneration = 1L,
            CharacterTestRuntimeAiLabDecisionTraceHandle decisionTraceHandle = default,
            bool hasReplayAuthorityEvidence = false,
            int replayAuthorityFrame = 0,
            long replayAuthorityHash = 0L)
        {
            Initialize(frame, realtimeSeconds, elapsedSeconds, frameSnapshot, drainedCommands, authorityCheckpoint, hasCheckpoint, stateHash, animationInputs, recordGeneration, decisionTraceHandle, hasReplayAuthorityEvidence, replayAuthorityFrame, replayAuthorityHash);
        }

        internal CharacterTestRuntimeAiPlannerDevTimelineRecord() { }
        private bool _initialized;
        internal void Initialize(
            int frame,
            float realtimeSeconds,
            double elapsedSeconds,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot frameSnapshot,
            IReadOnlyList<RuntimeCommand> drainedCommands,
            CharacterTestAuthorityRollbackCheckpoint authorityCheckpoint,
            bool hasCheckpoint,
            long stateHash,
            IReadOnlyList<CharacterTestAnimationInputPresentationState> animationInputs = null,
            long recordGeneration = 1L,
            CharacterTestRuntimeAiLabDecisionTraceHandle decisionTraceHandle = default,
            bool hasReplayAuthorityEvidence = false,
            int replayAuthorityFrame = 0,
            long replayAuthorityHash = 0L,
            CharacterTestRuntimeAiLabFrameCaptureStorage prepared = null)
        {
            if (_initialized) throw new InvalidOperationException("A published capture cannot be overwritten.");
            _initialized = true;
            Frame = frame;
            RealtimeSeconds = realtimeSeconds;
            ElapsedSeconds = elapsedSeconds;
            FrameSnapshot = frameSnapshot ?? CharacterTestRuntimeAiPlannerDevFrameSnapshot.Empty;
            DrainedCommands = prepared != null ? prepared.CopyCommands(drainedCommands)
                : new CharacterTestRuntimeAiLabFrozenArray<RuntimeCommand>(drainedCommands);
            AuthorityCheckpoint = authorityCheckpoint;
            HasCheckpoint = hasCheckpoint && authorityCheckpoint != null;
            StateHash = stateHash;
            RecordGeneration = Math.Max(1L, recordGeneration);
            AnimationInputs = prepared != null
                ? new CharacterTestRuntimeAiLabFrozenArray<CharacterTestAnimationInputPresentationState>(prepared.AnimationInputs, prepared.AnimationInputs.Length)
                : new CharacterTestRuntimeAiLabFrozenArray<CharacterTestAnimationInputPresentationState>(animationInputs);
            PreparedBundle = prepared?.Bundle;
            DecisionTraceHandle = decisionTraceHandle;
            HasReplayAuthorityEvidence = hasReplayAuthorityEvidence;
            ReplayAuthorityFrame = replayAuthorityFrame;
            ReplayAuthorityHash = replayAuthorityHash;
        }

        internal CharacterTestRuntimeAiLabCommittedFrameBundle PreparedBundle { get; private set; }

        public int Frame { get; private set; }
        public float RealtimeSeconds { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public CharacterTestRuntimeAiPlannerDevFrameSnapshot FrameSnapshot { get; private set; }
        public CharacterTestRuntimeAiLabFrozenArray<RuntimeCommand> DrainedCommands { get; private set; }
        public CharacterTestAuthorityRollbackCheckpoint AuthorityCheckpoint { get; private set; }
        public bool HasCheckpoint { get; private set; }
        public long StateHash { get; private set; }
        public long RecordGeneration { get; private set; }
        public CharacterTestRuntimeAiLabFrozenArray<CharacterTestAnimationInputPresentationState> AnimationInputs { get; private set; }
        internal CharacterTestRuntimeAiLabDecisionTraceHandle DecisionTraceHandle { get; private set; }
        internal bool HasReplayAuthorityEvidence { get; private set; }
        internal int ReplayAuthorityFrame { get; private set; }
        internal long ReplayAuthorityHash { get; private set; }
    }
}
