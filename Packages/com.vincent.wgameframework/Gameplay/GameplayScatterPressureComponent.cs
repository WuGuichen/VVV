using System;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Component-native scatter pressure state with deterministic integer recovery.
    /// </summary>
    public struct GameplayScatterPressureComponent : IGameplayComponent, IEquatable<GameplayScatterPressureComponent>
    {
        public GameplayScatterPressureComponent(
            int maxScatterPressure,
            int recoveryRate = 0,
            int recoveryDelayFrames = 0,
            int currentScatterPressure = 0,
            long lastPressureFrame = 0L,
            int currentCohesionLimit = 0,
            int maxScatterDebt = 0,
            int brokenRecoveryDelayFrames = 0,
            long lastCohesionBreakFrame = -1L)
        {
            int safeCohesionLimit = currentCohesionLimit <= 0 ? maxScatterPressure : currentCohesionLimit;
            int safeMaxScatterDebt = ResolveMaxScatterDebt(maxScatterDebt, maxScatterPressure, safeCohesionLimit, currentScatterPressure);
            PressureBand band = ResolveBand(currentScatterPressure, safeCohesionLimit, PressureBand.Stable);
            bool isBroken = band == PressureBand.Broken;
            long safeLastCohesionBreakFrame = isBroken && brokenRecoveryDelayFrames > 0 && lastCohesionBreakFrame < 0L
                ? lastPressureFrame
                : lastCohesionBreakFrame;
            Validate(
                maxScatterPressure,
                recoveryRate,
                recoveryDelayFrames,
                currentScatterPressure,
                lastPressureFrame,
                band,
                isBroken,
                safeCohesionLimit,
                safeMaxScatterDebt,
                brokenRecoveryDelayFrames,
                safeLastCohesionBreakFrame);

            MaxScatterPressure = maxScatterPressure;
            MaxScatterDebt = safeMaxScatterDebt;
            CurrentCohesionLimit = safeCohesionLimit;
            RecoveryRate = recoveryRate;
            RecoveryDelayFrames = recoveryDelayFrames;
            BrokenRecoveryDelayFrames = brokenRecoveryDelayFrames;
            CurrentScatterPressure = currentScatterPressure;
            CurrentBand = band;
            LastPressureFrame = lastPressureFrame;
            LastCohesionBreakFrame = safeLastCohesionBreakFrame;
            IsBroken = isBroken;
        }

        public int MaxScatterPressure;
        public int MaxScatterDebt;
        public int CurrentCohesionLimit;
        public int RecoveryRate;
        public int RecoveryDelayFrames;
        public int BrokenRecoveryDelayFrames;
        public int CurrentScatterPressure;
        public PressureBand CurrentBand;
        public long LastPressureFrame;
        public long LastCohesionBreakFrame;
        public bool IsBroken;

        public int CurrentValue => CurrentScatterPressure;
        public int MaxValue => CurrentCohesionLimit;
        public int BaseCohesion => MaxScatterPressure;
        public int ScatterDebt => CurrentScatterPressure;
        public float CohesionBreakRisk => CurrentCohesionLimit <= 0 ? 0f : (float)CurrentScatterPressure / CurrentCohesionLimit;
        public PressureBand Band => CurrentBand;

        public bool HasValidState()
        {
            return IsValid(
                MaxScatterPressure,
                RecoveryRate,
                RecoveryDelayFrames,
                CurrentScatterPressure,
                LastPressureFrame,
                CurrentBand,
                IsBroken,
                CurrentCohesionLimit,
                MaxScatterDebt,
                BrokenRecoveryDelayFrames,
                LastCohesionBreakFrame);
        }

        public bool Equals(GameplayScatterPressureComponent other)
        {
            return MaxScatterPressure == other.MaxScatterPressure
                && MaxScatterDebt == other.MaxScatterDebt
                && CurrentCohesionLimit == other.CurrentCohesionLimit
                && RecoveryRate == other.RecoveryRate
                && RecoveryDelayFrames == other.RecoveryDelayFrames
                && BrokenRecoveryDelayFrames == other.BrokenRecoveryDelayFrames
                && CurrentScatterPressure == other.CurrentScatterPressure
                && CurrentBand == other.CurrentBand
                && LastPressureFrame == other.LastPressureFrame
                && LastCohesionBreakFrame == other.LastCohesionBreakFrame
                && IsBroken == other.IsBroken;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayScatterPressureComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MaxScatterPressure;
                hash = (hash * 397) ^ MaxScatterDebt;
                hash = (hash * 397) ^ CurrentCohesionLimit;
                hash = (hash * 397) ^ RecoveryRate;
                hash = (hash * 397) ^ RecoveryDelayFrames;
                hash = (hash * 397) ^ BrokenRecoveryDelayFrames;
                hash = (hash * 397) ^ CurrentScatterPressure;
                hash = (hash * 397) ^ (int)CurrentBand;
                hash = (hash * 397) ^ LastPressureFrame.GetHashCode();
                hash = (hash * 397) ^ LastCohesionBreakFrame.GetHashCode();
                hash = (hash * 397) ^ (IsBroken ? 1 : 0);
                return hash;
            }
        }

        internal GameplayScatterPressureComponent ApplyDelta(int delta, long frame)
        {
            GameplayScatterPressureComponent updated = this;
            if (!updated.HasValidState())
                return updated;
            if (updated.IsBroken && updated.BrokenRecoveryDelayFrames > 0 && delta != 0)
                return updated;

            bool wasBroken = updated.IsBroken;
            updated.MaxScatterDebt = ResolveMaxScatterDebt(updated.MaxScatterDebt, updated.MaxScatterPressure, updated.CurrentCohesionLimit, updated.CurrentScatterPressure);
            updated.CurrentScatterPressure = AddClamped(updated.CurrentScatterPressure, delta, updated.MaxScatterDebt);
            updated.CurrentBand = ResolveBand(updated.CurrentScatterPressure, updated.CurrentCohesionLimit, CurrentBand);
            updated.IsBroken = updated.CurrentBand == PressureBand.Broken;
            if (delta > 0)
                updated.LastPressureFrame = frame;
            if (!wasBroken && updated.IsBroken)
                updated.LastCohesionBreakFrame = frame;

            return updated;
        }

        internal GameplayScatterPressureComponent Recover(int amount)
        {
            GameplayScatterPressureComponent updated = this;
            if (!updated.HasValidState() || amount <= 0 || updated.CurrentScatterPressure <= 0)
                return updated;
            if (updated.IsBroken && updated.BrokenRecoveryDelayFrames > 0)
                return updated;

            updated.MaxScatterDebt = ResolveMaxScatterDebt(updated.MaxScatterDebt, updated.MaxScatterPressure, updated.CurrentCohesionLimit, updated.CurrentScatterPressure);
            updated.CurrentScatterPressure = AddClamped(updated.CurrentScatterPressure, -amount, updated.MaxScatterDebt);
            updated.CurrentBand = ResolveBand(updated.CurrentScatterPressure, updated.CurrentCohesionLimit, CurrentBand);
            updated.IsBroken = updated.CurrentBand == PressureBand.Broken;
            return updated;
        }

        internal GameplayScatterPressureComponent RecoverBrokenFully()
        {
            GameplayScatterPressureComponent updated = this;
            if (!updated.HasValidState() || !updated.IsBroken || updated.BrokenRecoveryDelayFrames <= 0)
                return updated;

            updated.CurrentScatterPressure = 0;
            updated.MaxScatterDebt = ResolveMaxScatterDebt(updated.MaxScatterDebt, updated.MaxScatterPressure, updated.CurrentCohesionLimit, updated.CurrentScatterPressure);
            updated.CurrentBand = PressureBand.Stable;
            updated.IsBroken = false;
            return updated;
        }

        internal GameplayScatterPressureComponent SetCurrentCohesionLimit(int cohesionLimit, long frame)
        {
            GameplayScatterPressureComponent updated = this;
            if (!updated.HasValidState())
                return updated;

            bool wasBroken = updated.IsBroken;
            updated.CurrentCohesionLimit = cohesionLimit <= 0 ? updated.MaxScatterPressure : cohesionLimit;
            updated.MaxScatterDebt = ResolveMaxScatterDebt(updated.MaxScatterDebt, updated.MaxScatterPressure, updated.CurrentCohesionLimit, updated.CurrentScatterPressure);
            if (wasBroken && updated.BrokenRecoveryDelayFrames > 0)
            {
                updated.CurrentBand = PressureBand.Broken;
                updated.IsBroken = true;
                return updated;
            }

            updated.CurrentBand = ResolveBand(updated.CurrentScatterPressure, updated.CurrentCohesionLimit, CurrentBand);
            updated.IsBroken = updated.CurrentBand == PressureBand.Broken;
            if (!wasBroken && updated.IsBroken)
                updated.LastCohesionBreakFrame = frame;
            return updated;
        }

        internal static PressureBand ResolveBand(int currentScatterPressure, int currentCohesionLimit, PressureBand currentBand)
        {
            Validate(
                1,
                0,
                0,
                currentScatterPressure,
                0L,
                currentBand,
                currentBand == PressureBand.Broken,
                currentCohesionLimit,
                Math.Max(currentScatterPressure, currentCohesionLimit));

            PressureBand upwardBand = ResolveBandWithoutHysteresis(currentScatterPressure, currentCohesionLimit);
            if (upwardBand >= currentBand)
                return upwardBand;

            if (currentBand != PressureBand.Stable)
            {
                PressureHysteresis threshold = GetDefaultHysteresis(currentCohesionLimit, currentBand);
                if (currentScatterPressure >= threshold.ExitThreshold)
                    return currentBand;
            }

            return upwardBand;
        }

        internal static PressureHysteresis GetDefaultHysteresis(int currentCohesionLimit, PressureBand band)
        {
            if (currentCohesionLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(currentCohesionLimit), "Scatter pressure cohesion limit must be greater than zero.");

            switch (band)
            {
                case PressureBand.Pressed:
                    return new PressureHysteresis(Percent(currentCohesionLimit, 35), Percent(currentCohesionLimit, 30));
                case PressureBand.Cracked:
                    return new PressureHysteresis(Percent(currentCohesionLimit, 65), Percent(currentCohesionLimit, 60));
                case PressureBand.Critical:
                    return new PressureHysteresis(Percent(currentCohesionLimit, 85), Percent(currentCohesionLimit, 80));
                case PressureBand.Broken:
                    return new PressureHysteresis(currentCohesionLimit, Percent(currentCohesionLimit, 95));
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
            int currentScatterPressure,
            int currentCohesionLimit)
        {
            if (currentCohesionLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(currentCohesionLimit), "Scatter pressure cohesion limit must be greater than zero.");
            if (currentScatterPressure < 0)
                throw new ArgumentOutOfRangeException(nameof(currentScatterPressure), "Scatter pressure current value cannot be negative.");

            if (currentScatterPressure >= currentCohesionLimit)
                return PressureBand.Broken;
            if (currentScatterPressure >= Percent(currentCohesionLimit, 85))
                return PressureBand.Critical;
            if (currentScatterPressure >= Percent(currentCohesionLimit, 65))
                return PressureBand.Cracked;
            if (currentScatterPressure >= Percent(currentCohesionLimit, 35))
                return PressureBand.Pressed;

            return PressureBand.Stable;
        }

        private static int Percent(int value, int percent)
        {
            long result = ((long)value * percent + 99L) / 100L;
            if (result <= 0L)
                return 0;
            if (result > int.MaxValue)
                return int.MaxValue;

            return (int)result;
        }

        private static int ResolveMaxScatterDebt(int maxScatterDebt, int maxScatterPressure, int currentCohesionLimit, int currentScatterPressure)
        {
            int resolved = maxScatterDebt <= 0 ? maxScatterPressure : maxScatterDebt;
            if (resolved < maxScatterPressure)
                resolved = maxScatterPressure;
            if (resolved < currentCohesionLimit)
                resolved = currentCohesionLimit;
            if (resolved < currentScatterPressure)
                resolved = currentScatterPressure;
            return resolved <= 0 ? 1 : resolved;
        }

        private static bool IsValid(
            int maxScatterPressure,
            int recoveryRate,
            int recoveryDelayFrames,
            int currentScatterPressure,
            long lastPressureFrame,
            PressureBand currentBand,
            bool isBroken,
            int currentCohesionLimit,
            int maxScatterDebt,
            int brokenRecoveryDelayFrames,
            long lastCohesionBreakFrame)
        {
            if (maxScatterPressure <= 0
                || currentCohesionLimit <= 0
                || maxScatterDebt <= 0
                || recoveryRate < 0
                || recoveryDelayFrames < 0
                || brokenRecoveryDelayFrames < 0)
            {
                return false;
            }
            if (maxScatterDebt < currentCohesionLimit || maxScatterDebt < maxScatterPressure)
                return false;
            if (currentScatterPressure < 0 || currentScatterPressure > maxScatterDebt || lastPressureFrame < 0L)
                return false;
            if (lastCohesionBreakFrame < -1L)
                return false;
            if (!PressureBandUtility.IsDefined(currentBand))
                return false;
            if (brokenRecoveryDelayFrames > 0 && isBroken && lastCohesionBreakFrame < 0L)
                return false;

            return isBroken == (currentBand == PressureBand.Broken);
        }

        internal static void Validate(
            int maxScatterPressure,
            int recoveryRate,
            int recoveryDelayFrames,
            int currentScatterPressure,
            long lastPressureFrame,
            PressureBand currentBand,
            bool isBroken,
            int currentCohesionLimit = 0,
            int maxScatterDebt = 0,
            int brokenRecoveryDelayFrames = 0,
            long lastCohesionBreakFrame = -1L)
        {
            if (currentCohesionLimit <= 0)
                currentCohesionLimit = maxScatterPressure;
            maxScatterDebt = ResolveMaxScatterDebt(maxScatterDebt, maxScatterPressure, currentCohesionLimit, currentScatterPressure);
            if (maxScatterPressure <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxScatterPressure), "Scatter pressure max value must be greater than zero.");
            if (currentCohesionLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(currentCohesionLimit), "Scatter pressure cohesion limit must be greater than zero.");
            if (maxScatterDebt < maxScatterPressure || maxScatterDebt < currentCohesionLimit)
                throw new ArgumentOutOfRangeException(nameof(maxScatterDebt), "Scatter pressure max debt must cover base pressure and current cohesion limit.");
            if (recoveryRate < 0)
                throw new ArgumentOutOfRangeException(nameof(recoveryRate), "Scatter pressure recovery rate cannot be negative.");
            if (recoveryDelayFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(recoveryDelayFrames), "Scatter pressure recovery delay cannot be negative.");
            if (brokenRecoveryDelayFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(brokenRecoveryDelayFrames), "Scatter pressure broken recovery delay cannot be negative.");
            if (currentScatterPressure < 0 || currentScatterPressure > maxScatterDebt)
                throw new ArgumentOutOfRangeException(nameof(currentScatterPressure), "Scatter pressure current value must be between zero and max debt.");
            if (lastPressureFrame < 0L)
                throw new ArgumentOutOfRangeException(nameof(lastPressureFrame), "Scatter pressure last pressure frame cannot be negative.");
            if (lastCohesionBreakFrame < -1L)
                throw new ArgumentOutOfRangeException(nameof(lastCohesionBreakFrame), "Scatter pressure last cohesion break frame cannot be less than -1.");
            if (!PressureBandUtility.IsDefined(currentBand))
                throw new ArgumentOutOfRangeException(nameof(currentBand), "Scatter pressure band is not defined.");
            if (isBroken != (currentBand == PressureBand.Broken))
                throw new ArgumentException("Scatter pressure broken flag must match the current band.", nameof(isBroken));
            if (brokenRecoveryDelayFrames > 0 && isBroken && lastCohesionBreakFrame < 0L)
                throw new ArgumentOutOfRangeException(nameof(lastCohesionBreakFrame), "Timed broken recovery requires a cohesion break frame.");
        }
    }
}
