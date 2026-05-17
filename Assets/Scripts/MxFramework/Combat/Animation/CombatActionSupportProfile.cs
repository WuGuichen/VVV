using System;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Animation
{
    public readonly struct CombatActionSupportProfile : IEquatable<CombatActionSupportProfile>
    {
        public CombatActionSupportProfile(
            Fix64 supportRate,
            Fix64 minSupportRatio,
            bool hasHyperArmorWindow = false,
            CombatFrameRange hyperArmorWindow = default)
        {
            if (supportRate < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(supportRate), "Support rate cannot be negative.");
            }

            if (minSupportRatio < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(minSupportRatio), "Minimum support ratio cannot be negative.");
            }

            if (!hasHyperArmorWindow)
            {
                hyperArmorWindow = CombatFrameRange.Empty;
            }

            SupportRate = supportRate;
            MinSupportRatio = minSupportRatio;
            HasHyperArmorWindow = hasHyperArmorWindow;
            HyperArmorWindow = hyperArmorWindow;
        }

        public Fix64 SupportRate { get; }

        public Fix64 MinSupportRatio { get; }

        public bool HasHyperArmorWindow { get; }

        public CombatFrameRange HyperArmorWindow { get; }

        public bool Equals(CombatActionSupportProfile other)
        {
            return SupportRate.Equals(other.SupportRate)
                && MinSupportRatio.Equals(other.MinSupportRatio)
                && HasHyperArmorWindow == other.HasHyperArmorWindow
                && HyperArmorWindow.Equals(other.HyperArmorWindow);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatActionSupportProfile other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SupportRate.GetHashCode();
                hash = (hash * 397) ^ MinSupportRatio.GetHashCode();
                hash = (hash * 397) ^ HasHyperArmorWindow.GetHashCode();
                hash = (hash * 397) ^ HyperArmorWindow.GetHashCode();
                return hash;
            }
        }
    }
}
