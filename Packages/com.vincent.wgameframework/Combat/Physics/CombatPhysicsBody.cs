using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatPhysicsBody : IEquatable<CombatPhysicsBody>
    {
        public CombatPhysicsBody(CombatEntityId entityId, CombatBodyId bodyId, FixVector3 position)
        {
            if (entityId.IsNone)
            {
                throw new ArgumentException("Combat physics body entity id cannot be none.", nameof(entityId));
            }

            if (bodyId.IsNone)
            {
                throw new ArgumentException("Combat physics body id cannot be none.", nameof(bodyId));
            }

            EntityId = entityId;
            BodyId = bodyId;
            Position = position;
        }

        public CombatEntityId EntityId { get; }

        public CombatBodyId BodyId { get; }

        public FixVector3 Position { get; }

        public bool Equals(CombatPhysicsBody other)
        {
            return EntityId.Equals(other.EntityId)
                && BodyId.Equals(other.BodyId)
                && Position.Equals(other.Position);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsBody other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EntityId.Value;
                hash = (hash * 397) ^ BodyId.Value;
                hash = (hash * 397) ^ Position.GetHashCode();
                return hash;
            }
        }
    }
}
