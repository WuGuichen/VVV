using System;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Animation
{
    internal static class CombatRuntimeFrameUtility
    {
        public static CombatFrame ToCombatFrame(long frameIndex)
        {
            if (frameIndex > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex), "Combat frame cannot exceed Int32.MaxValue.");
            }

            return new CombatFrame((int)frameIndex);
        }
    }
}
