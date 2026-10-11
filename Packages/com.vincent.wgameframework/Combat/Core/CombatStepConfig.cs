using System;

namespace MxFramework.Combat.Core
{
    public readonly struct CombatStepConfig : IEquatable<CombatStepConfig>
    {
        public const int DefaultTicksPerSecond = 60;
        public const int DefaultMaxStepsPerUpdate = 8;

        public static readonly CombatStepConfig Default = new CombatStepConfig(
            DefaultTicksPerSecond,
            DefaultMaxStepsPerUpdate);

        public CombatStepConfig(int ticksPerSecond, int maxStepsPerUpdate)
        {
            if (ticksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), "Ticks per second must be positive.");
            }

            if (maxStepsPerUpdate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxStepsPerUpdate), "Max steps per update must be positive.");
            }

            TicksPerSecond = ticksPerSecond;
            MaxStepsPerUpdate = maxStepsPerUpdate;
        }

        public int TicksPerSecond { get; }

        public int MaxStepsPerUpdate { get; }

        public bool Equals(CombatStepConfig other)
        {
            return TicksPerSecond == other.TicksPerSecond
                && MaxStepsPerUpdate == other.MaxStepsPerUpdate;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatStepConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (TicksPerSecond * 397) ^ MaxStepsPerUpdate;
            }
        }
    }
}
