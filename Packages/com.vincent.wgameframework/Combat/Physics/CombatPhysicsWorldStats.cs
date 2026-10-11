using System;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatPhysicsWorldStats : IEquatable<CombatPhysicsWorldStats>
    {
        public CombatPhysicsWorldStats(int revision, int bodyCount, int colliderCount)
        {
            Revision = revision;
            BodyCount = bodyCount;
            ColliderCount = colliderCount;
        }

        public int Revision { get; }

        public int BodyCount { get; }

        public int ColliderCount { get; }

        public bool Equals(CombatPhysicsWorldStats other)
        {
            return Revision == other.Revision
                && BodyCount == other.BodyCount
                && ColliderCount == other.ColliderCount;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsWorldStats other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Revision;
                hash = (hash * 397) ^ BodyCount;
                hash = (hash * 397) ^ ColliderCount;
                return hash;
            }
        }
    }
}
