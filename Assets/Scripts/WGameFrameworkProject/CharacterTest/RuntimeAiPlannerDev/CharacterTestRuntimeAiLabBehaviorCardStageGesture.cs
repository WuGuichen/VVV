using System;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabStageGesturePreview
    {
        internal CharacterTestRuntimeAiLabStageGesturePreview(
            bool active,
            CharacterTestRuntimeAiLabStageGestureKind kind,
            long epoch,
            int frameDelta,
            int primaryStartFrame,
            int primaryDurationFrames,
            bool hasActionConflict)
        {
            Active = active;
            Kind = kind;
            Epoch = epoch;
            FrameDelta = frameDelta;
            PrimaryStartFrame = primaryStartFrame;
            PrimaryDurationFrames = primaryDurationFrames;
            HasActionConflict = hasActionConflict;
        }

        internal bool Active { get; }
        internal CharacterTestRuntimeAiLabStageGestureKind Kind { get; }
        internal long Epoch { get; }
        internal int FrameDelta { get; }
        internal int PrimaryStartFrame { get; }
        internal int PrimaryDurationFrames { get; }
        internal bool HasActionConflict { get; }
    }

    internal sealed class CharacterTestRuntimeAiLabStageGestureSession
    {
        private string[] _stageIds = Array.Empty<string>();
        private int[] _startFrames = Array.Empty<int>();
        private int[] _durationFrames = Array.Empty<int>();
        private int[] _allStartFrames = Array.Empty<int>();
        private int[] _allDurationFrames = Array.Empty<int>();
        private bool[] _allOwnsPrimaryAction = Array.Empty<bool>();
        private bool[] _allSelected = Array.Empty<bool>();
        private int _stageCount;
        private int _allStageCount;
        private CharacterTestRuntimeAiLabStageEditIdentity _identity;
        private float _pointerStartX;
        private float _pixelsPerFrame;
        private bool _snapEnabled;
        private int _primaryIndex;
        private int _minimumFrameDelta;
        private int _maximumFrameDelta;
        private long _epoch;

        internal bool Active { get; private set; }
        internal CharacterTestRuntimeAiLabStageGestureKind Kind { get; private set; }
        internal long Epoch => _epoch;
        internal int StageCount => _stageCount;
        internal int FrameDelta { get; private set; }
        internal int MinimumFrameDelta => _minimumFrameDelta;
        internal int MaximumFrameDelta => _maximumFrameDelta;

        internal string GetStageId(int index) => index >= 0 && index < _stageCount
            ? _stageIds[index]
            : string.Empty;

        internal bool Begin(
            CharacterTestRuntimeAiLabStageGestureKind kind,
            in CharacterTestRuntimeAiLabStageEditIdentity identity,
            CharacterTestRuntimeAiLabStageSelectionState selection,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            string primaryStageId,
            float pointerX,
            float pixelsPerFrame,
            bool snapEnabled)
        {
            Cancel();
            int count = Math.Max(0, Math.Min(stageCount, stages?.Length ?? 0));
            if (!identity.IsValid || selection == null || count == 0 || pixelsPerFrame <= 0f)
                return false;
            if (kind == CharacterTestRuntimeAiLabStageGestureKind.Resize && selection.Count != 1)
                return false;

            EnsureCapacity(selection.Count);
            EnsureAllStageCapacity(count);
            _stageCount = 0;
            _allStageCount = count;
            _primaryIndex = -1;
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = stages[i];
                _allStartFrames[i] = stage?.StartFrame ?? 0;
                _allDurationFrames[i] = Math.Max(0, stage?.DurationFrames ?? 0);
                _allOwnsPrimaryAction[i] = stage?.OwnsPrimaryAction ?? false;
                _allSelected[i] = stage != null && selection.IsSelected(stage.StageId);
                if (stage == null || !_allSelected[i])
                    continue;
                _stageIds[_stageCount] = stage.StageId;
                _startFrames[_stageCount] = stage.StartFrame;
                _durationFrames[_stageCount] = Math.Max(0, stage.DurationFrames);
                if (string.Equals(stage.StageId, primaryStageId, StringComparison.Ordinal))
                    _primaryIndex = _stageCount;
                _stageCount++;
            }
            if (_stageCount == 0 || _primaryIndex < 0)
            {
                ClearCaptured();
                return false;
            }

            _identity = identity;
            _pointerStartX = pointerX;
            _pixelsPerFrame = pixelsPerFrame;
            _snapEnabled = snapEnabled;
            Kind = kind;
            ResolveFrameDeltaBounds();
            FrameDelta = 0;
            Active = true;
            AdvanceEpoch();
            return true;
        }

        /// <remarks>NoAlloc after Begin capacity is established.</remarks>
        internal CharacterTestRuntimeAiLabStageGesturePreview Update(
            in CharacterTestRuntimeAiLabStageEditIdentity identity,
            long expectedEpoch,
            float pointerX,
            bool invertSnap)
        {
            if (!Active || expectedEpoch != _epoch)
                return default;
            if (!_identity.Matches(identity))
            {
                Cancel();
                return default;
            }
            double raw = (pointerX - _pointerStartX) / Math.Max(0.0001f, _pixelsPerFrame);
            int delta = raw >= 0d
                ? (int)Math.Floor(raw + 0.5d)
                : (int)Math.Ceiling(raw - 0.5d);
            bool snap = _snapEnabled ^ invertSnap;
            if (snap)
                delta = Quantize(delta, 15);

            if (Kind == CharacterTestRuntimeAiLabStageGestureKind.Move)
                delta = Math.Max(_minimumFrameDelta, Math.Min(_maximumFrameDelta, delta));
            else if (Kind == CharacterTestRuntimeAiLabStageGestureKind.Resize)
                delta = Math.Max(_minimumFrameDelta, Math.Min(_maximumFrameDelta, delta));
            FrameDelta = delta;
            return new CharacterTestRuntimeAiLabStageGesturePreview(
                true,
                Kind,
                _epoch,
                delta,
                _startFrames[_primaryIndex] + (Kind == CharacterTestRuntimeAiLabStageGestureKind.Move ? delta : 0),
                _durationFrames[_primaryIndex] + (Kind == CharacterTestRuntimeAiLabStageGestureKind.Resize ? delta : 0),
                DetectActionConflict(delta));
        }

        internal bool TryFinish(
            in CharacterTestRuntimeAiLabStageEditIdentity identity,
            long expectedEpoch,
            out CharacterTestRuntimeAiLabStageGestureKind kind,
            out int frameDelta)
        {
            kind = Kind;
            frameDelta = FrameDelta;
            if (!Active || expectedEpoch != _epoch || !_identity.Matches(identity))
            {
                Cancel();
                return false;
            }
            Active = false;
            AdvanceEpoch();
            return true;
        }

        internal void Cancel()
        {
            if (Active || Kind != CharacterTestRuntimeAiLabStageGestureKind.None)
                AdvanceEpoch();
            Active = false;
            Kind = CharacterTestRuntimeAiLabStageGestureKind.None;
            FrameDelta = 0;
            _identity = default;
            _minimumFrameDelta = 0;
            _maximumFrameDelta = 0;
            ClearCaptured();
        }

        private void ResolveFrameDeltaBounds()
        {
            if (Kind == CharacterTestRuntimeAiLabStageGestureKind.Resize)
            {
                int start = _startFrames[_primaryIndex];
                int duration = _durationFrames[_primaryIndex];
                _minimumFrameDelta = 1 - duration;
                _maximumFrameDelta = Math.Max(_minimumFrameDelta, 36000 - start - duration);
                return;
            }

            int minimum = int.MinValue;
            int maximum = int.MaxValue;
            for (int i = 0; i < _stageCount; i++)
            {
                minimum = Math.Max(minimum, -_startFrames[i]);
                long end = (long)_startFrames[i] + _durationFrames[i];
                int boundary = end > int.MaxValue
                    ? int.MinValue
                    : end < int.MinValue ? int.MaxValue : (int)(36000L - end);
                maximum = Math.Min(maximum, boundary);
            }
            _minimumFrameDelta = minimum;
            _maximumFrameDelta = Math.Max(minimum, maximum);
        }

        private void ClearCaptured()
        {
            for (int i = 0; i < _stageCount; i++)
            {
                _stageIds[i] = string.Empty;
                _startFrames[i] = 0;
                _durationFrames[i] = 0;
            }
            _stageCount = 0;
            _allStageCount = 0;
            _primaryIndex = -1;
        }

        private bool DetectActionConflict(int frameDelta)
        {
            for (int i = 0; i < _allStageCount; i++)
            {
                if (!_allOwnsPrimaryAction[i])
                    continue;
                int firstStart = _allStartFrames[i]
                    + (_allSelected[i] && Kind == CharacterTestRuntimeAiLabStageGestureKind.Move ? frameDelta : 0);
                int firstDuration = _allDurationFrames[i]
                    + (_allSelected[i] && Kind == CharacterTestRuntimeAiLabStageGestureKind.Resize ? frameDelta : 0);
                long firstEnd = (long)firstStart + Math.Max(0, firstDuration);
                for (int j = i + 1; j < _allStageCount; j++)
                {
                    if (!_allOwnsPrimaryAction[j] || (!_allSelected[i] && !_allSelected[j]))
                        continue;
                    int secondStart = _allStartFrames[j]
                        + (_allSelected[j] && Kind == CharacterTestRuntimeAiLabStageGestureKind.Move ? frameDelta : 0);
                    int secondDuration = _allDurationFrames[j]
                        + (_allSelected[j] && Kind == CharacterTestRuntimeAiLabStageGestureKind.Resize ? frameDelta : 0);
                    long secondEnd = (long)secondStart + Math.Max(0, secondDuration);
                    if (firstStart < secondEnd && secondStart < firstEnd)
                        return true;
                }
            }
            return false;
        }

        private void EnsureCapacity(int count)
        {
            if (_stageIds.Length >= count)
                return;
            int capacity = Math.Max(count, Math.Max(8, _stageIds.Length * 2));
            Array.Resize(ref _stageIds, capacity);
            Array.Resize(ref _startFrames, capacity);
            Array.Resize(ref _durationFrames, capacity);
        }

        private void EnsureAllStageCapacity(int count)
        {
            if (_allStartFrames.Length >= count)
                return;
            int capacity = Math.Max(count, Math.Max(8, _allStartFrames.Length * 2));
            Array.Resize(ref _allStartFrames, capacity);
            Array.Resize(ref _allDurationFrames, capacity);
            Array.Resize(ref _allOwnsPrimaryAction, capacity);
            Array.Resize(ref _allSelected, capacity);
        }

        private void AdvanceEpoch()
        {
            _epoch = _epoch == long.MaxValue ? 1L : _epoch + 1L;
        }

        private static int Quantize(int value, int step)
        {
            int sign = value < 0 ? -1 : 1;
            int absolute = Math.Abs(value);
            return sign * ((absolute + step / 2) / step) * step;
        }
    }

    internal static class CharacterTestRuntimeAiLabStageEdgeAutoScroll
    {
        internal const float EdgeWidth = 28f;
        private const int TimelineFrameLimit = 36000;

        /// <remarks>NoAlloc.</remarks>
        internal static int Advance(
            int beginStartFrame,
            int visibleFrameSpan,
            int currentFrameOffset,
            float pointerLocalX,
            float viewportWidth,
            float pixelsPerFrame)
        {
            if (viewportWidth <= 0f || pixelsPerFrame <= 0f)
                return currentFrameOffset;

            int direction;
            float pressure;
            if (pointerLocalX < EdgeWidth)
            {
                direction = -1;
                pressure = Math.Min(1f, Math.Max(0f, (EdgeWidth - pointerLocalX) / EdgeWidth));
            }
            else if (pointerLocalX > viewportWidth - EdgeWidth)
            {
                direction = 1;
                pressure = Math.Min(1f, Math.Max(0f,
                    (pointerLocalX - (viewportWidth - EdgeWidth)) / EdgeWidth));
            }
            else
            {
                return currentFrameOffset;
            }

            float pixelStep = 2f + pressure * 10f;
            int frameStep = Math.Max(1, Math.Min(60,
                (int)Math.Ceiling(pixelStep / Math.Max(0.0001f, pixelsPerFrame))));
            int span = Math.Max(1, Math.Min(TimelineFrameLimit, visibleFrameSpan));
            int maximumStart = Math.Max(0, TimelineFrameLimit - span);
            long requestedStart = (long)beginStartFrame + currentFrameOffset + direction * frameStep;
            int nextStart = requestedStart <= 0L
                ? 0
                : requestedStart >= maximumStart ? maximumStart : (int)requestedStart;
            return nextStart - Math.Max(0, beginStartFrame);
        }
    }
}
