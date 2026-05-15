using System;

namespace MxFramework.Combat.Core
{
    public readonly struct CombatFixedStepBatch
    {
        internal CombatFixedStepBatch(
            long runtimeFrameIndex,
            double runtimeDeltaTime,
            double runtimeElapsedTime,
            double fixedDeltaTime,
            CombatFrame startFrame,
            CombatFrame endFrame,
            int stepCount,
            double remainingTime,
            bool maxStepLimitReached)
        {
            RuntimeFrameIndex = runtimeFrameIndex;
            RuntimeDeltaTime = runtimeDeltaTime;
            RuntimeElapsedTime = runtimeElapsedTime;
            FixedDeltaTime = fixedDeltaTime;
            StartFrame = startFrame;
            EndFrame = endFrame;
            StepCount = stepCount;
            RemainingTime = remainingTime;
            MaxStepLimitReached = maxStepLimitReached;
        }

        public long RuntimeFrameIndex { get; }

        public double RuntimeDeltaTime { get; }

        public double RuntimeElapsedTime { get; }

        public double FixedDeltaTime { get; }

        public CombatFrame StartFrame { get; }

        public CombatFrame EndFrame { get; }

        public int StepCount { get; }

        public double RemainingTime { get; }

        public bool MaxStepLimitReached { get; }

        public bool HasSteps => StepCount > 0;

        public CombatFrame GetStepFrame(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= StepCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stepIndex), "Step index is outside this fixed-step batch.");
            }

            return StartFrame.Add(stepIndex + 1);
        }
    }
}
