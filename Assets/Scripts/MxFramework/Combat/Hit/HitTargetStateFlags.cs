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
    }
}
