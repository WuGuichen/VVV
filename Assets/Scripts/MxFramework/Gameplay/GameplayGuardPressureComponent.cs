using System;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Component-native guard pressure state with deterministic integer recovery.
    /// </summary>
    public struct GameplayGuardPressureComponent : IGameplayComponent, IEquatable<GameplayGuardPressureComponent>
    {
        public GameplayGuardPressureComponent(
            int maxPressure,
            int recoveryRate = 0,
            int recoveryDelayFrames = 0,
            int currentPressure = 0,
            long lastPressureFrame = 0L)
        {
            GameplayPosturePressureComponent.Validate(
                maxPressure,
                recoveryRate,
                recoveryDelayFrames,
                currentPressure,
                lastPressureFrame,
                PressureBand.Stable,
                false);

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

        public bool Equals(GameplayGuardPressureComponent other)
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
            return obj is GameplayGuardPressureComponent other && Equals(other);
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

        internal GameplayGuardPressureComponent ApplyDelta(int delta, bool isBlocking, long frame)
        {
            GameplayGuardPressureComponent updated = this;
            if (!updated.HasValidState())
                return updated;

            updated.CurrentPressure = AddClamped(updated.CurrentPressure, delta, updated.MaxPressure);
            updated.CurrentBand = ResolveBand(updated.CurrentPressure, updated.MaxPressure, CurrentBand);
            updated.IsBroken = updated.CurrentBand == PressureBand.Broken;
            if (delta > 0 || isBlocking)
                updated.LastPressureFrame = frame;

            return updated;
        }

        internal GameplayGuardPressureComponent Recover(int amount)
        {
            GameplayGuardPressureComponent updated = this;
            if (!updated.HasValidState() || amount <= 0 || updated.CurrentPressure <= 0)
                return updated;

            updated.CurrentPressure = AddClamped(updated.CurrentPressure, -amount, updated.MaxPressure);
            updated.CurrentBand = ResolveBand(updated.CurrentPressure, updated.MaxPressure, CurrentBand);
            updated.IsBroken = updated.CurrentBand == PressureBand.Broken;
            return updated;
        }

        internal static PressureBand ResolveBand(int currentPressure, int maxPressure, PressureBand currentBand)
        {
            return GameplayPosturePressureComponent.ResolveBand(currentPressure, maxPressure, currentBand);
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
            GameplayPosturePressureComponent.Validate(
                maxPressure,
                recoveryRate,
                recoveryDelayFrames,
                currentPressure,
                lastPressureFrame,
                currentBand,
                isBroken);
        }
    }
}
