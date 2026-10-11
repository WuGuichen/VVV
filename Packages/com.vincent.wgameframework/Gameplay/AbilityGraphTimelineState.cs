using System;

namespace MxFramework.Gameplay
{
    public readonly struct AbilityGraphTimelineState : IEquatable<AbilityGraphTimelineState>
    {
        private readonly string _timelineId;

        public AbilityGraphTimelineState(
            string timelineId,
            string currentPhaseId,
            int elapsedFramesInPhase,
            long totalElapsedFrames,
            bool isCompleted)
            : this(timelineId, new AbilityGraphPhaseId(currentPhaseId), elapsedFramesInPhase, totalElapsedFrames, isCompleted)
        {
        }

        public AbilityGraphTimelineState(
            string timelineId,
            AbilityGraphPhaseId currentPhaseId,
            int elapsedFramesInPhase,
            long totalElapsedFrames,
            bool isCompleted)
        {
            _timelineId = timelineId ?? string.Empty;
            CurrentPhaseId = currentPhaseId;
            ElapsedFramesInPhase = elapsedFramesInPhase;
            TotalElapsedFrames = totalElapsedFrames;
            IsCompleted = isCompleted;
        }

        public string TimelineId => _timelineId ?? string.Empty;
        public AbilityGraphPhaseId CurrentPhaseId { get; }
        public int ElapsedFramesInPhase { get; }
        public long TotalElapsedFrames { get; }
        public bool IsCompleted { get; }
        public bool IsEmpty => string.IsNullOrEmpty(TimelineId) && CurrentPhaseId.IsEmpty && ElapsedFramesInPhase == 0 && TotalElapsedFrames == 0 && !IsCompleted;

        public bool IsInPhase(string phaseId)
        {
            return IsInPhase(new AbilityGraphPhaseId(phaseId));
        }

        public bool IsInPhase(AbilityGraphPhaseId phaseId)
        {
            return !IsCompleted && CurrentPhaseId == phaseId;
        }

        public bool Equals(AbilityGraphTimelineState other)
        {
            return string.Equals(TimelineId, other.TimelineId, StringComparison.Ordinal)
                && CurrentPhaseId == other.CurrentPhaseId
                && ElapsedFramesInPhase == other.ElapsedFramesInPhase
                && TotalElapsedFrames == other.TotalElapsedFrames
                && IsCompleted == other.IsCompleted;
        }

        public override bool Equals(object obj)
        {
            return obj is AbilityGraphTimelineState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = DeterministicStringHash(TimelineId);
                hash = (hash * 397) ^ CurrentPhaseId.GetHashCode();
                hash = (hash * 397) ^ ElapsedFramesInPhase;
                hash = (hash * 397) ^ TotalElapsedFrames.GetHashCode();
                hash = (hash * 397) ^ (IsCompleted ? 1 : 0);
                return hash;
            }
        }

        internal AbilityGraphTimelineState WithElapsed(int elapsedFramesInPhase, long totalElapsedFrames)
        {
            return new AbilityGraphTimelineState(TimelineId, CurrentPhaseId, elapsedFramesInPhase, totalElapsedFrames, IsCompleted);
        }

        internal AbilityGraphTimelineState WithCurrentPhase(AbilityGraphPhaseId currentPhaseId, long totalElapsedFrames)
        {
            return new AbilityGraphTimelineState(TimelineId, currentPhaseId, 0, totalElapsedFrames, false);
        }

        internal AbilityGraphTimelineState WithCompleted(int elapsedFramesInPhase, long totalElapsedFrames)
        {
            return new AbilityGraphTimelineState(TimelineId, CurrentPhaseId, elapsedFramesInPhase, totalElapsedFrames, true);
        }

        private static int DeterministicStringHash(string value)
        {
            unchecked
            {
                int hash = (int)2166136261;
                value = value ?? string.Empty;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash ^ value[i]) * 16777619;

                return hash;
            }
        }
    }
}
