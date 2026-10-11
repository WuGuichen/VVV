using System;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Animation
{
    public readonly struct CombatActionSupportProfile : IEquatable<CombatActionSupportProfile>
    {
        public CombatActionSupportProfile(
            Fix64 supportRate,
            Fix64 minSupportRatio,
            bool hasCohesionSupportWindow = false,
            CombatFrameRange cohesionSupportWindow = default)
        {
            if (supportRate < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(supportRate), "Support rate cannot be negative.");
            }

            if (minSupportRatio < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(minSupportRatio), "Minimum support ratio cannot be negative.");
            }

            if (!hasCohesionSupportWindow)
            {
                cohesionSupportWindow = CombatFrameRange.Empty;
            }

            SupportRate = supportRate;
            MinSupportRatio = minSupportRatio;
            HasCohesionSupportWindow = hasCohesionSupportWindow;
            CohesionSupportWindow = cohesionSupportWindow;
        }

        public Fix64 SupportRate { get; }

        public Fix64 MinSupportRatio { get; }

        public bool HasCohesionSupportWindow { get; }

        public CombatFrameRange CohesionSupportWindow { get; }

        public bool Equals(CombatActionSupportProfile other)
        {
            return SupportRate.Equals(other.SupportRate)
                && MinSupportRatio.Equals(other.MinSupportRatio)
                && HasCohesionSupportWindow == other.HasCohesionSupportWindow
                && CohesionSupportWindow.Equals(other.CohesionSupportWindow);
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
                hash = (hash * 397) ^ HasCohesionSupportWindow.GetHashCode();
                hash = (hash * 397) ^ CohesionSupportWindow.GetHashCode();
                return hash;
            }
        }
    }
}
