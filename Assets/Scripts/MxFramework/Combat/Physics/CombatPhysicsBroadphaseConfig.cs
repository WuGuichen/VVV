using System;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatPhysicsBroadphaseConfig : IEquatable<CombatPhysicsBroadphaseConfig>
    {
        public static readonly CombatPhysicsBroadphaseConfig Default =
            new CombatPhysicsBroadphaseConfig(Fix64.FromInt(4));

        public CombatPhysicsBroadphaseConfig(Fix64 cellSize)
        {
            if (cellSize <= Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Combat physics broadphase cell size must be positive.");
            }

            CellSize = cellSize;
        }

        public Fix64 CellSize { get; }

        public bool Equals(CombatPhysicsBroadphaseConfig other)
        {
            return CellSize.Equals(other.CellSize);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsBroadphaseConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            return CellSize.GetHashCode();
        }
    }
}
