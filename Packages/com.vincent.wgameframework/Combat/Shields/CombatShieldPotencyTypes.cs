using System;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Shields
{
    public enum CombatShieldPressureBand
    {
        Stable = 0,
        Pressed = 1,
        Cracked = 2,
        Critical = 3,
        Broken = 4
    }

    public enum CombatShieldImpactSourceKind
    {
        Unknown = 0,
        ProjectileImpact = 1,
        WeaponTrace = 2,
        AreaPulse = 3,
        Custom = 100
    }

    public enum CombatShieldPotencyChangeReason
    {
        Impact = 0,
        Recovery = 1,
        CohesionLimit = 2,
        Restore = 3
    }

    public readonly struct CombatShieldPotencyTriplet : IEquatable<CombatShieldPotencyTriplet>
    {
        public static readonly CombatShieldPotencyTriplet Zero = new CombatShieldPotencyTriplet(0, 0, 0);

        public CombatShieldPotencyTriplet(int motion, int cohesion, int scatter)
        {
            Motion = motion;
            Cohesion = cohesion;
            Scatter = scatter;
        }

        public int Motion { get; }
        public int Cohesion { get; }
        public int Scatter { get; }

        public bool IsZero => Motion == 0 && Cohesion == 0 && Scatter == 0;

        public CombatShieldPotencyTriplet ClampNonNegative()
        {
            return new CombatShieldPotencyTriplet(
                Motion < 0 ? 0 : Motion,
                Cohesion < 0 ? 0 : Cohesion,
                Scatter < 0 ? 0 : Scatter);
        }

        public CombatShieldPotencyTriplet AddClampedNonNegative(CombatShieldPotencyTriplet delta)
        {
            return new CombatShieldPotencyTriplet(
                AddClampedNonNegative(Motion, delta.Motion),
                AddClampedNonNegative(Cohesion, delta.Cohesion),
                AddClampedNonNegative(Scatter, delta.Scatter));
        }

        public CombatShieldPotencyTriplet RecoverDestabilization(int amount)
        {
            if (amount <= 0)
                return this;

            int remaining = amount;
            int scatter = Scatter;
            int motion = Motion;
            int scatterRecovery = Math.Min(scatter, remaining);
            scatter -= scatterRecovery;
            remaining -= scatterRecovery;
            if (remaining > 0)
                motion = Math.Max(0, motion - remaining);

            return new CombatShieldPotencyTriplet(motion, Cohesion, scatter);
        }

        public CombatHash AppendHash(CombatHash hash)
        {
            return hash.Add(Motion).Add(Cohesion).Add(Scatter);
        }

        public bool Equals(CombatShieldPotencyTriplet other)
        {
            return Motion == other.Motion
                && Cohesion == other.Cohesion
                && Scatter == other.Scatter;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatShieldPotencyTriplet other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Motion;
                hash = (hash * 397) ^ Cohesion;
                hash = (hash * 397) ^ Scatter;
                return hash;
            }
        }

        public override string ToString()
        {
            return Motion + "/" + Cohesion + "/" + Scatter;
        }

        public static CombatShieldPotencyTriplet operator +(
            CombatShieldPotencyTriplet left,
            CombatShieldPotencyTriplet right)
        {
            return new CombatShieldPotencyTriplet(
                AddSaturating(left.Motion, right.Motion),
                AddSaturating(left.Cohesion, right.Cohesion),
                AddSaturating(left.Scatter, right.Scatter));
        }

        public static CombatShieldPotencyTriplet operator -(
            CombatShieldPotencyTriplet left,
            CombatShieldPotencyTriplet right)
        {
            return new CombatShieldPotencyTriplet(
                AddSaturating(left.Motion, -right.Motion),
                AddSaturating(left.Cohesion, -right.Cohesion),
                AddSaturating(left.Scatter, -right.Scatter));
        }

        private static int AddClampedNonNegative(int value, int delta)
        {
            long result = (long)value + delta;
            if (result <= 0L)
                return 0;
            if (result >= int.MaxValue)
                return int.MaxValue;

            return (int)result;
        }

        private static int AddSaturating(int value, int delta)
        {
            long result = (long)value + delta;
            if (result <= int.MinValue)
                return int.MinValue;
            if (result >= int.MaxValue)
                return int.MaxValue;

            return (int)result;
        }
    }

    public readonly struct CombatShieldImpactMapping : IEquatable<CombatShieldImpactMapping>
    {
        public CombatShieldImpactMapping(
            string stableId,
            CombatShieldImpactSourceKind sourceKind,
            CombatShieldPotencyTriplet baseDelta,
            CombatShieldPotencyTriplet impactPowerScalePerThousand,
            CombatShieldPotencyTriplet damageScalePerThousand,
            CombatShieldPotencyTriplet minDelta,
            CombatShieldPotencyTriplet maxDelta)
        {
            if (!Enum.IsDefined(typeof(CombatShieldImpactSourceKind), sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind), "Shield impact source kind is not defined.");

            StableId = stableId ?? string.Empty;
            SourceKind = sourceKind;
            BaseDelta = baseDelta;
            ImpactPowerScalePerThousand = impactPowerScalePerThousand;
            DamageScalePerThousand = damageScalePerThousand;
            MinDelta = minDelta;
            MaxDelta = maxDelta;
        }

        public string StableId { get; }
        public CombatShieldImpactSourceKind SourceKind { get; }
        public CombatShieldPotencyTriplet BaseDelta { get; }
        public CombatShieldPotencyTriplet ImpactPowerScalePerThousand { get; }
        public CombatShieldPotencyTriplet DamageScalePerThousand { get; }
        public CombatShieldPotencyTriplet MinDelta { get; }
        public CombatShieldPotencyTriplet MaxDelta { get; }

        /// <remarks>NoAlloc.</remarks>
        public CombatShieldPotencyTriplet ResolveDelta(int damage, int impactPower)
        {
            if (damage < 0)
                throw new ArgumentOutOfRangeException(nameof(damage), "Shield impact damage cannot be negative.");
            if (impactPower < 0)
                throw new ArgumentOutOfRangeException(nameof(impactPower), "Shield impact power cannot be negative.");

            var resolved = new CombatShieldPotencyTriplet(
                ResolveAxis(BaseDelta.Motion, ImpactPowerScalePerThousand.Motion, impactPower, DamageScalePerThousand.Motion, damage),
                ResolveAxis(BaseDelta.Cohesion, ImpactPowerScalePerThousand.Cohesion, impactPower, DamageScalePerThousand.Cohesion, damage),
                ResolveAxis(BaseDelta.Scatter, ImpactPowerScalePerThousand.Scatter, impactPower, DamageScalePerThousand.Scatter, damage));

            return new CombatShieldPotencyTriplet(
                Clamp(resolved.Motion, MinDelta.Motion, MaxDelta.Motion),
                Clamp(resolved.Cohesion, MinDelta.Cohesion, MaxDelta.Cohesion),
                Clamp(resolved.Scatter, MinDelta.Scatter, MaxDelta.Scatter));
        }

        public bool Equals(CombatShieldImpactMapping other)
        {
            return string.Equals(StableId, other.StableId, StringComparison.Ordinal)
                && SourceKind == other.SourceKind
                && BaseDelta.Equals(other.BaseDelta)
                && ImpactPowerScalePerThousand.Equals(other.ImpactPowerScalePerThousand)
                && DamageScalePerThousand.Equals(other.DamageScalePerThousand)
                && MinDelta.Equals(other.MinDelta)
                && MaxDelta.Equals(other.MaxDelta);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatShieldImpactMapping other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StableId == null ? 0 : StringComparer.Ordinal.GetHashCode(StableId);
                hash = (hash * 397) ^ (int)SourceKind;
                hash = (hash * 397) ^ BaseDelta.GetHashCode();
                hash = (hash * 397) ^ ImpactPowerScalePerThousand.GetHashCode();
                hash = (hash * 397) ^ DamageScalePerThousand.GetHashCode();
                hash = (hash * 397) ^ MinDelta.GetHashCode();
                hash = (hash * 397) ^ MaxDelta.GetHashCode();
                return hash;
            }
        }

        private static int ResolveAxis(
            int baseValue,
            int impactScale,
            int impactPower,
            int damageScale,
            int damage)
        {
            long value = baseValue
                + ((long)impactScale * impactPower / 1000L)
                + ((long)damageScale * damage / 1000L);
            if (value <= int.MinValue)
                return int.MinValue;
            if (value >= int.MaxValue)
                return int.MaxValue;

            return (int)value;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (min > max)
                return value;
            if (value < min)
                return min;
            if (value > max)
                return max;

            return value;
        }
    }
}
