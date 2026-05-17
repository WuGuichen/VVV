using System;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Component-native posture pressure state with deterministic integer recovery.
    /// </summary>
    public struct GameplayPosturePressureComponent : IGameplayComponent, IEquatable<GameplayPosturePressureComponent>
    {
        public GameplayPosturePressureComponent(
            int maxPressure,
            int recoveryRate = 0,
            int recoveryDelayFrames = 0,
            int currentPressure = 0,
            long lastPressureFrame = 0L)
        {
            Validate(maxPressure, recoveryRate, recoveryDelayFrames, currentPressure, lastPressureFrame, PressureBand.Stable, false);

            MaxPressure = maxPressure;
            RecoveryRate = recoveryRate;
            RecoveryDelayFrames = recoveryDelayFrames;
            CurrentPressure = currentPressure;
            CurrentBand = ResolveBand(currentPressure, maxPressure, PressureBand.Stable);
            LastPressureFrame = lastPressureFrame;
            IsBroken = CurrentBand == PressureBand.Broken;
        }

        public int MaxPressure;
        public int RecoveryRate;
        public int RecoveryDelayFrames;
        public int CurrentPressure;
        public PressureBand CurrentBand;
        public long LastPressureFrame;
        public bool IsBroken;

        public int CurrentValue => CurrentPressure;
        public int MaxValue => MaxPressure;
        public PressureBand Band => CurrentBand;

        public bool HasValidState()
        {
            return IsValid(MaxPressure, RecoveryRate, RecoveryDelayFrames, CurrentPressure, LastPressureFrame, CurrentBand, IsBroken);
        }

        public bool Equals(GameplayPosturePressureComponent other)
        {
            return MaxPressure == other.MaxPressure
                && RecoveryRate == other.RecoveryRate
                && RecoveryDelayFrames == other.RecoveryDelayFrames
                && CurrentPressure == other.CurrentPressure
                && CurrentBand == other.CurrentBand
                && LastPressureFrame == other.LastPressureFrame
                && IsBroken == other.IsBroken;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayPosturePressureComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MaxPressure;
                hash = (hash * 397) ^ RecoveryRate;
                hash = (hash * 397) ^ RecoveryDelayFrames;
                hash = (hash * 397) ^ CurrentPressure;
                hash = (hash * 397) ^ (int)CurrentBand;
                hash = (hash * 397) ^ LastPressureFrame.GetHashCode();
                hash = (hash * 397) ^ (IsBroken ? 1 : 0);
                return hash;
            }
        }

        internal GameplayPosturePressureComponent ApplyDelta(int delta, long frame)
        {
            GameplayPosturePressureComponent updated = this;
            if (!updated.HasValidState())
                return updated;

            updated.CurrentPressure = AddClamped(updated.CurrentPressure, delta, updated.MaxPressure);
            updated.CurrentBand = ResolveBand(updated.CurrentPressure, updated.MaxPressure, CurrentBand);
            updated.IsBroken = updated.CurrentBand == PressureBand.Broken;
            if (delta > 0)
                updated.LastPressureFrame = frame;

            return updated;
        }

        internal GameplayPosturePressureComponent Recover(int amount)
        {
            GameplayPosturePressureComponent updated = this;
            if (!updated.HasValidState() || amount <= 0 || updated.CurrentPressure <= 0)
                return updated;

            updated.CurrentPressure = AddClamped(updated.CurrentPressure, -amount, updated.MaxPressure);
            updated.CurrentBand = ResolveBand(updated.CurrentPressure, updated.MaxPressure, CurrentBand);
            updated.IsBroken = updated.CurrentBand == PressureBand.Broken;
            return updated;
        }

        internal static PressureBand ResolveBand(int currentPressure, int maxPressure, PressureBand currentBand)
        {
            Validate(maxPressure, 0, 0, currentPressure, 0L, currentBand, currentBand == PressureBand.Broken);

            PressureBand upwardBand = ResolveBandWithoutHysteresis(currentPressure, maxPressure);
            if (upwardBand >= currentBand)
                return upwardBand;

            if (currentBand != PressureBand.Stable)
            {
                PressureHysteresis threshold = GetDefaultHysteresis(maxPressure, currentBand);
                if (currentPressure >= threshold.ExitThreshold)
                    return currentBand;
            }

            return upwardBand;
        }

        internal static PressureHysteresis GetDefaultHysteresis(int maxPressure, PressureBand band)
        {
            if (maxPressure <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPressure), "Posture pressure max value must be greater than zero.");

            switch (band)
            {
                case PressureBand.Pressed:
                    return new PressureHysteresis(Percent(maxPressure, 25), Percent(maxPressure, 20));
                case PressureBand.Cracked:
                    return new PressureHysteresis(Percent(maxPressure, 50), Percent(maxPressure, 45));
                case PressureBand.Critical:
                    return new PressureHysteresis(Percent(maxPressure, 75), Percent(maxPressure, 70));
                case PressureBand.Broken:
                    return new PressureHysteresis(maxPressure, Percent(maxPressure, 90));
                default:
                    return PressureHysteresis.None;
            }
        }

        private static int AddClamped(int currentValue, int delta, int maxValue)
        {
            long value = (long)currentValue + delta;
            if (value <= 0L)
                return 0;
            if (value >= maxValue)
                return maxValue;

            return (int)value;
        }

        private static PressureBand ResolveBandWithoutHysteresis(
            int currentPressure,
            int maxPressure)
        {
            if (maxPressure <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPressure), "Posture pressure max value must be greater than zero.");
            if (currentPressure < 0 || currentPressure > maxPressure)
                throw new ArgumentOutOfRangeException(nameof(currentPressure), "Posture pressure current value must be between zero and max value.");

            if (currentPressure >= maxPressure)
                return PressureBand.Broken;
            if (currentPressure >= Percent(maxPressure, 75))
                return PressureBand.Critical;
            if (currentPressure >= Percent(maxPressure, 50))
                return PressureBand.Cracked;
            if (currentPressure >= Percent(maxPressure, 25))
                return PressureBand.Pressed;

            return PressureBand.Stable;
        }

        private static int Percent(int maxPressure, int percent)
        {
            long value = ((long)maxPressure * percent + 99L) / 100L;
            if (value <= 0L)
                return 0;
            if (value > int.MaxValue)
                return int.MaxValue;

            return (int)value;
        }

        private static bool IsValid(
            int maxPressure,
            int recoveryRate,
            int recoveryDelayFrames,
            int currentPressure,
            long lastPressureFrame,
            PressureBand currentBand,
            bool isBroken)
        {
            if (maxPressure <= 0 || recoveryRate < 0 || recoveryDelayFrames < 0)
                return false;
            if (currentPressure < 0 || currentPressure > maxPressure || lastPressureFrame < 0L)
                return false;
            if (!Enum.IsDefined(typeof(PressureBand), currentBand))
                return false;

            return isBroken == (currentBand == PressureBand.Broken);
        }

        internal static void Validate(
            int maxPressure,
            int recoveryRate,
            int recoveryDelayFrames,
            int currentPressure,
            long lastPressureFrame,
            PressureBand currentBand,
            bool isBroken)
        {
            if (maxPressure <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPressure), "Posture pressure max value must be greater than zero.");
            if (recoveryRate < 0)
                throw new ArgumentOutOfRangeException(nameof(recoveryRate), "Posture pressure recovery rate cannot be negative.");
            if (recoveryDelayFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(recoveryDelayFrames), "Posture pressure recovery delay cannot be negative.");
            if (currentPressure < 0 || currentPressure > maxPressure)
                throw new ArgumentOutOfRangeException(nameof(currentPressure), "Posture pressure current value must be between zero and max value.");
            if (lastPressureFrame < 0L)
                throw new ArgumentOutOfRangeException(nameof(lastPressureFrame), "Posture pressure last pressure frame cannot be negative.");
            if (!Enum.IsDefined(typeof(PressureBand), currentBand))
                throw new ArgumentOutOfRangeException(nameof(currentBand), "Posture pressure band is not defined.");
            if (isBroken != (currentBand == PressureBand.Broken))
                throw new ArgumentException("Posture pressure broken flag must match the current band.", nameof(isBroken));
        }
    }
}
