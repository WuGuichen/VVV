using System;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Describes the stable, cross-feature identity families accepted by gameplay target queries.
    /// </summary>
    /// <remarks>
    /// Allocation: NoAlloc. A classified entity uses zero or one flag; a query may combine flags.
    /// </remarks>
    [Flags]
    public enum GameplayTargetIdentityTypes
    {
        None = 0,
        Character = 1 << 0,
        WorldObject = 1 << 1,
        SpatialPoint = 1 << 2,
        SpatialRegion = 1 << 3,
    }
}
