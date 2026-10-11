using System;

namespace MxFramework.Combat.Core
{
    public sealed class CombatFrameClock
    {
        public CombatFrameClock()
            : this(CombatStepConfig.Default)
        {
        }

        public CombatFrameClock(CombatStepConfig config)
        {
            Config = config;
            CurrentFrame = CombatFrame.Zero;
        }

        public CombatStepConfig Config { get; }

        public CombatFrame CurrentFrame { get; private set; }

        public int StepCount => CurrentFrame.Value;

        public CombatFrame Step()
        {
            CurrentFrame = CurrentFrame.Next();
            return CurrentFrame;
        }

        public CombatFrame Step(int frames)
        {
            if (frames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frames), "Step count cannot be negative.");
            }

            CurrentFrame = CurrentFrame.Add(frames);
            return CurrentFrame;
        }

        public void Reset()
        {
            CurrentFrame = CombatFrame.Zero;
        }
    }
}
