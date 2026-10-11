namespace MxFramework.Combat.Animation
{
    public readonly struct CombatAnimationSnapshot
    {
        public CombatAnimationSnapshot(
            int runningActionCount,
            int activePhaseCount,
            int hitCandidateCount,
            long frameIndex)
        {
            RunningActionCount = runningActionCount;
            ActivePhaseCount = activePhaseCount;
            HitCandidateCount = hitCandidateCount;
            FrameIndex = frameIndex;
        }

        public int RunningActionCount { get; }

        public int ActivePhaseCount { get; }

        public int HitCandidateCount { get; }

        public long FrameIndex { get; }
    }
}
