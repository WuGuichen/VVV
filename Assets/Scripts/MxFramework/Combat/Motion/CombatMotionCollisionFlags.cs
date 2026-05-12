using System;

namespace MxFramework.Combat.Motion
{
    [Flags]
    public enum CombatMotionCollisionFlags
    {
        None = 0,
        BlockedX = 1 << 0,
        BlockedY = 1 << 1,
        BlockedZ = 1 << 2,
        Grounded = 1 << 3,
        Ceiling = 1 << 4,
        Wall = 1 << 5,
        IterationLimit = 1 << 6,
    }
}
