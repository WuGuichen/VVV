using System;
using MxFramework.Combat.Animation;
using MxFramework.Core.Math;

namespace MxFramework.Combat.GameplayBridge
{
    public static class CombatBreakLineAdapter
    {
        public static int CalculateBreakLine(int basePressure, CombatActionSupportProfile? supportProfile)
        {
            if (basePressure < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(basePressure), "Base pressure cannot be negative.");
            }

            if (!supportProfile.HasValue)
            {
                return basePressure;
            }

            CombatActionSupportProfile profile = supportProfile.Value;
            Fix64 baseLine = Fix64.FromInt(basePressure);
            Fix64 actionLine = baseLine * profile.SupportRate;
            Fix64 minLine = baseLine * profile.MinSupportRatio;
            Fix64 breakLine = Fix64.Max(baseLine, Fix64.Max(actionLine, minLine));
            return breakLine.ToInt();
        }

        public static bool IsInHyperArmorWindow(CombatActionSupportProfile profile, int localFrame)
        {
            if (localFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");
            }

            return profile.HasHyperArmorWindow && profile.HyperArmorWindow.Contains(localFrame);
        }
    }
}
