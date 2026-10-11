using System;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Shields
{
    public readonly struct CombatShieldPotencyComponent : IEquatable<CombatShieldPotencyComponent>
    {
        public CombatShieldPotencyComponent(
            CombatEntityId shieldEntityId,
            CombatEntityId ownerEntityId,
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            CombatShieldPotencyTriplet basePotency,
            int baseCohesionLimit,
            int recoveryRate = 0,
            int recoveryDelayFrames = 0)
            : this(
                shieldEntityId,
                ownerEntityId,
                bodyId,
                colliderId,
                basePotency,
                basePotency,
                baseCohesionLimit,
                baseCohesionLimit,
                recoveryRate,
                recoveryDelayFrames,
                lastImpactFrame: 0L,
                CombatShieldPressureBand.Stable,
                isBroken: false,
                resolveBand: true)
        {
        }

        public CombatShieldPotencyComponent(
            CombatEntityId shieldEntityId,
            CombatEntityId ownerEntityId,
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            CombatShieldPotencyTriplet basePotency,
            CombatShieldPotencyTriplet currentPotency,
            int baseCohesionLimit,
            int currentCohesionLimit,
            int recoveryRate,
            int recoveryDelayFrames,
            long lastImpactFrame,
            CombatShieldPressureBand currentBand,
            bool isBroken)
            : this(
                shieldEntityId,
                ownerEntityId,
                bodyId,
                colliderId,
                basePotency,
                currentPotency,
                baseCohesionLimit,
                currentCohesionLimit,
                recoveryRate,
                recoveryDelayFrames,
                lastImpactFrame,
                currentBand,
                isBroken,
                resolveBand: false)
        {
        }

        private CombatShieldPotencyComponent(
            CombatEntityId shieldEntityId,
            CombatEntityId ownerEntityId,
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            CombatShieldPotencyTriplet basePotency,
            CombatShieldPotencyTriplet currentPotency,
            int baseCohesionLimit,
            int currentCohesionLimit,
            int recoveryRate,
            int recoveryDelayFrames,
            long lastImpactFrame,
            CombatShieldPressureBand currentBand,
            bool isBroken,
            bool resolveBand)
        {
            if (shieldEntityId.IsNone)
                throw new ArgumentException("Shield entity id cannot be none.", nameof(shieldEntityId));
            if (ownerEntityId.IsNone)
                throw new ArgumentException("Shield owner entity id cannot be none.", nameof(ownerEntityId));
            if (bodyId.IsNone)
                throw new ArgumentException("Shield body id cannot be none.", nameof(bodyId));
            if (colliderId.IsNone)
                throw new ArgumentException("Shield collider id cannot be none.", nameof(colliderId));
            if (baseCohesionLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(baseCohesionLimit), "Shield base cohesion limit must be positive.");
            if (currentCohesionLimit <= 0)
                currentCohesionLimit = baseCohesionLimit;
            if (recoveryRate < 0)
                throw new ArgumentOutOfRangeException(nameof(recoveryRate), "Shield recovery rate cannot be negative.");
            if (recoveryDelayFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(recoveryDelayFrames), "Shield recovery delay cannot be negative.");
            if (lastImpactFrame < 0L)
                throw new ArgumentOutOfRangeException(nameof(lastImpactFrame), "Shield last impact frame cannot be negative.");
            if (!IsDefined(currentBand))
                throw new ArgumentOutOfRangeException(nameof(currentBand), "Shield pressure band is not defined.");

            ShieldEntityId = shieldEntityId;
            OwnerEntityId = ownerEntityId;
            BodyId = bodyId;
            ColliderId = colliderId;
            BasePotency = basePotency.ClampNonNegative();
            CurrentPotency = currentPotency.ClampNonNegative();
            BaseCohesionLimit = baseCohesionLimit;
            CurrentCohesionLimit = currentCohesionLimit;
            RecoveryRate = recoveryRate;
            RecoveryDelayFrames = recoveryDelayFrames;
            LastImpactFrame = lastImpactFrame;
            CurrentBand = resolveBand
                ? ResolveBand(ComputeShieldPressure(CurrentPotency), currentCohesionLimit, CombatShieldPressureBand.Stable)
                : currentBand;
            IsBroken = resolveBand ? CurrentBand == CombatShieldPressureBand.Broken : isBroken;

            ValidateBandMatchesBroken(CurrentBand, IsBroken);
        }

        public CombatEntityId ShieldEntityId { get; }
        public CombatEntityId OwnerEntityId { get; }
        public CombatBodyId BodyId { get; }
        public CombatColliderId ColliderId { get; }
        public CombatShieldPotencyTriplet BasePotency { get; }
        public CombatShieldPotencyTriplet CurrentPotency { get; }
        public int BaseCohesionLimit { get; }
        public int CurrentCohesionLimit { get; }
        public int RecoveryRate { get; }
        public int RecoveryDelayFrames { get; }
        public long LastImpactFrame { get; }
        public CombatShieldPressureBand CurrentBand { get; }
        public bool IsBroken { get; }
        public int ShieldPressure => ComputeShieldPressure(CurrentPotency);
        public float CohesionBreakRisk => CurrentCohesionLimit <= 0 ? 0f : (float)ShieldPressure / CurrentCohesionLimit;

        /// <remarks>NoAlloc.</remarks>
        public CombatShieldPotencyComponent ApplyDelta(CombatShieldPotencyTriplet delta, long frame)
        {
            if (IsBroken)
                return this;
            if (frame < 0L)
                throw new ArgumentOutOfRangeException(nameof(frame), "Shield impact frame cannot be negative.");

            int previousPressure = ShieldPressure;
            CombatShieldPotencyTriplet potency = CurrentPotency.AddClampedNonNegative(delta);
            CombatShieldPressureBand band = ResolveBand(ComputeShieldPressure(potency), CurrentCohesionLimit, CurrentBand);
            return new CombatShieldPotencyComponent(
                ShieldEntityId,
                OwnerEntityId,
                BodyId,
                ColliderId,
                BasePotency,
                potency,
                BaseCohesionLimit,
                CurrentCohesionLimit,
                RecoveryRate,
                RecoveryDelayFrames,
                ComputeShieldPressure(potency) > previousPressure ? frame : LastImpactFrame,
                band,
                band == CombatShieldPressureBand.Broken);
        }

        /// <remarks>NoAlloc.</remarks>
        public CombatShieldPotencyComponent Recover(int amount)
        {
            if (IsBroken || amount <= 0 || ShieldPressure <= 0)
                return this;

            CombatShieldPotencyTriplet potency = CurrentPotency.RecoverDestabilization(amount);
            CombatShieldPressureBand band = ResolveBand(ComputeShieldPressure(potency), CurrentCohesionLimit, CurrentBand);
            return new CombatShieldPotencyComponent(
                ShieldEntityId,
                OwnerEntityId,
                BodyId,
                ColliderId,
                BasePotency,
                potency,
                BaseCohesionLimit,
                CurrentCohesionLimit,
                RecoveryRate,
                RecoveryDelayFrames,
                LastImpactFrame,
                band,
                band == CombatShieldPressureBand.Broken);
        }

        /// <remarks>NoAlloc.</remarks>
        public CombatShieldPotencyComponent SetCurrentCohesionLimit(int cohesionLimit)
        {
            int resolvedCohesionLimit = cohesionLimit <= 0 ? BaseCohesionLimit : cohesionLimit;
            CombatShieldPressureBand band = ResolveBand(ShieldPressure, resolvedCohesionLimit, CurrentBand);
            return new CombatShieldPotencyComponent(
                ShieldEntityId,
                OwnerEntityId,
                BodyId,
                ColliderId,
                BasePotency,
                CurrentPotency,
                BaseCohesionLimit,
                resolvedCohesionLimit,
                RecoveryRate,
                RecoveryDelayFrames,
                LastImpactFrame,
                band,
                band == CombatShieldPressureBand.Broken);
        }

        public CombatHash AppendHash(CombatHash hash)
        {
            return CurrentPotency.AppendHash(BasePotency.AppendHash(hash
                .Add(ShieldEntityId)
                .Add(OwnerEntityId)
                .Add(BodyId)
                .Add(ColliderId)
                .Add(BaseCohesionLimit)
                .Add(CurrentCohesionLimit)
                .Add(RecoveryRate)
                .Add(RecoveryDelayFrames)
                .Add(unchecked((ulong)LastImpactFrame))
                .Add((int)CurrentBand)
                .Add(IsBroken ? 1 : 0)));
        }

        public bool Equals(CombatShieldPotencyComponent other)
        {
            return ShieldEntityId.Equals(other.ShieldEntityId)
                && OwnerEntityId.Equals(other.OwnerEntityId)
                && BodyId.Equals(other.BodyId)
                && ColliderId.Equals(other.ColliderId)
                && BasePotency.Equals(other.BasePotency)
                && CurrentPotency.Equals(other.CurrentPotency)
                && BaseCohesionLimit == other.BaseCohesionLimit
                && CurrentCohesionLimit == other.CurrentCohesionLimit
                && RecoveryRate == other.RecoveryRate
                && RecoveryDelayFrames == other.RecoveryDelayFrames
                && LastImpactFrame == other.LastImpactFrame
                && CurrentBand == other.CurrentBand
                && IsBroken == other.IsBroken;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatShieldPotencyComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ShieldEntityId.Value;
                hash = (hash * 397) ^ OwnerEntityId.Value;
                hash = (hash * 397) ^ BodyId.Value;
                hash = (hash * 397) ^ ColliderId.Value;
                hash = (hash * 397) ^ BasePotency.GetHashCode();
                hash = (hash * 397) ^ CurrentPotency.GetHashCode();
                hash = (hash * 397) ^ BaseCohesionLimit;
                hash = (hash * 397) ^ CurrentCohesionLimit;
                hash = (hash * 397) ^ RecoveryRate;
                hash = (hash * 397) ^ RecoveryDelayFrames;
                hash = (hash * 397) ^ LastImpactFrame.GetHashCode();
                hash = (hash * 397) ^ (int)CurrentBand;
                hash = (hash * 397) ^ (IsBroken ? 1 : 0);
                return hash;
            }
        }

        internal static bool IsDefined(CombatShieldPressureBand band)
        {
            return band >= CombatShieldPressureBand.Stable && band <= CombatShieldPressureBand.Broken;
        }

        internal static CombatShieldPressureBand ResolveBand(
            int shieldPressure,
            int currentCohesionLimit,
            CombatShieldPressureBand currentBand)
        {
            if (shieldPressure < 0)
                throw new ArgumentOutOfRangeException(nameof(shieldPressure), "Shield pressure cannot be negative.");
            if (currentCohesionLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(currentCohesionLimit), "Shield cohesion limit must be positive.");
            if (!IsDefined(currentBand))
                throw new ArgumentOutOfRangeException(nameof(currentBand), "Shield pressure band is not defined.");

            CombatShieldPressureBand upward = ResolveBandWithoutHysteresis(shieldPressure, currentCohesionLimit);
            if (upward >= currentBand)
                return upward;

            if (currentBand != CombatShieldPressureBand.Stable)
            {
                int exitThreshold = GetExitThreshold(currentCohesionLimit, currentBand);
                if (shieldPressure >= exitThreshold)
                    return currentBand;
            }

            return upward;
        }

        internal static int ComputeShieldPressure(CombatShieldPotencyTriplet potency)
        {
            long pressure = (long)potency.Motion + potency.Scatter - potency.Cohesion;
            if (pressure <= 0L)
                return 0;
            if (pressure >= int.MaxValue)
                return int.MaxValue;

            return (int)pressure;
        }

        private static CombatShieldPressureBand ResolveBandWithoutHysteresis(
            int shieldPressure,
            int currentCohesionLimit)
        {
            if (shieldPressure >= currentCohesionLimit)
                return CombatShieldPressureBand.Broken;
            if (shieldPressure >= Percent(currentCohesionLimit, 85))
                return CombatShieldPressureBand.Critical;
            if (shieldPressure >= Percent(currentCohesionLimit, 65))
                return CombatShieldPressureBand.Cracked;
            if (shieldPressure >= Percent(currentCohesionLimit, 35))
                return CombatShieldPressureBand.Pressed;

            return CombatShieldPressureBand.Stable;
        }

        private static int GetExitThreshold(int currentCohesionLimit, CombatShieldPressureBand band)
        {
            switch (band)
            {
                case CombatShieldPressureBand.Pressed:
                    return Percent(currentCohesionLimit, 30);
                case CombatShieldPressureBand.Cracked:
                    return Percent(currentCohesionLimit, 60);
                case CombatShieldPressureBand.Critical:
                    return Percent(currentCohesionLimit, 80);
                case CombatShieldPressureBand.Broken:
                    return Percent(currentCohesionLimit, 95);
                default:
                    return 0;
            }
        }

        private static int Percent(int value, int percent)
        {
            long result = ((long)value * percent + 99L) / 100L;
            if (result <= 0L)
                return 0;
            if (result >= int.MaxValue)
                return int.MaxValue;

            return (int)result;
        }

        private static void ValidateBandMatchesBroken(CombatShieldPressureBand band, bool isBroken)
        {
            if (isBroken != (band == CombatShieldPressureBand.Broken))
                throw new ArgumentException("Shield broken flag must match the current pressure band.");
        }
    }
}
