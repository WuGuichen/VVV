using System;

namespace MxFramework.Combat.Hit
{
    [Flags]
    public enum HitTargetStateFlags
    {
        None = 0,
        Alive = 1 << 0,
        Invincible = 1 << 1,
        Parrying = 1 << 2,
        Blocking = 1 << 3,
        SuperArmor = 1 << 4,
        /// <summary>
        /// The target remains alive, targetable, and physically present, but an
        /// overlapping attack does not commit a hit while this flag is active.
        /// </summary>
        Unhittable = 1 << 5,
    }
}
