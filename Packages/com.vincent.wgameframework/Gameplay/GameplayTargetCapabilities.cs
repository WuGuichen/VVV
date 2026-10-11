using System;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Describes stable capabilities that may be combined independently of target identity.
    /// </summary>
    /// <remarks>Allocation: NoAlloc.</remarks>
    [Flags]
    public enum GameplayTargetCapabilities
    {
        None = 0,
        Damageable = 1 << 0,
        Movable = 1 << 1,
        Static = 1 << 2,
    }
}
