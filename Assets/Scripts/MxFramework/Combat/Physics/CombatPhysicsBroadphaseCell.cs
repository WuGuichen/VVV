using System;

namespace MxFramework.Combat.Physics
{
    internal readonly struct CombatPhysicsBroadphaseCell : IEquatable<CombatPhysicsBroadphaseCell>
    {
        public CombatPhysicsBroadphaseCell(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public bool Equals(CombatPhysicsBroadphaseCell other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsBroadphaseCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ Z;
                return hash;
            }
        }
    }
}
