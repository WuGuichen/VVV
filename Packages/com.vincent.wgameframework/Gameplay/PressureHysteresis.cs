using System;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Enter and exit thresholds for pressure band hysteresis.
    /// </summary>
    public readonly struct PressureHysteresis : IEquatable<PressureHysteresis>
    {
        public PressureHysteresis(int enterThreshold, int exitThreshold)
        {
            if (enterThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(enterThreshold), "Pressure hysteresis enter threshold cannot be negative.");
            if (exitThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(exitThreshold), "Pressure hysteresis exit threshold cannot be negative.");
            if (exitThreshold > enterThreshold)
                throw new ArgumentException("Pressure hysteresis exit threshold cannot be greater than enter threshold.", nameof(exitThreshold));

            EnterThreshold = enterThreshold;
            ExitThreshold = exitThreshold;
        }

        public static PressureHysteresis None => default;

        public int EnterThreshold { get; }
        public int ExitThreshold { get; }

        public bool Equals(PressureHysteresis other)
        {
            return EnterThreshold == other.EnterThreshold
                && ExitThreshold == other.ExitThreshold;
        }

        public override bool Equals(object obj)
        {
            return obj is PressureHysteresis other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EnterThreshold;
                hash = (hash * 397) ^ ExitThreshold;
                return hash;
            }
        }
    }
}
