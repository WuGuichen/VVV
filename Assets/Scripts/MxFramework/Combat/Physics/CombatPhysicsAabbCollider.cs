using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatPhysicsAabbCollider : IEquatable<CombatPhysicsAabbCollider>
    {
        public CombatPhysicsAabbCollider(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            int layer,
            FixVector3 localMin,
            FixVector3 localMax)
        {
            if (bodyId.IsNone)
            {
                throw new ArgumentException("Combat physics collider body id cannot be none.", nameof(bodyId));
            }

            if (colliderId.IsNone)
            {
                throw new ArgumentException("Combat physics collider id cannot be none.", nameof(colliderId));
            }

            CombatPhysicsLayerMask.FromLayer(layer);
            if (localMin.X > localMax.X || localMin.Y > localMax.Y || localMin.Z > localMax.Z)
            {
                throw new ArgumentException("AABB collider local min cannot be greater than local max.");
            }

            BodyId = bodyId;
            ColliderId = colliderId;
            Layer = layer;
            LocalMin = localMin;
            LocalMax = localMax;
        }

        public CombatBodyId BodyId { get; }

        public CombatColliderId ColliderId { get; }

        public int Layer { get; }

        public FixVector3 LocalMin { get; }

        public FixVector3 LocalMax { get; }

        public FixVector3 GetWorldMin(FixVector3 bodyPosition)
        {
            return bodyPosition + LocalMin;
        }

        public FixVector3 GetWorldMax(FixVector3 bodyPosition)
        {
            return bodyPosition + LocalMax;
        }

        public bool Equals(CombatPhysicsAabbCollider other)
        {
            return BodyId.Equals(other.BodyId)
                && ColliderId.Equals(other.ColliderId)
                && Layer == other.Layer
                && LocalMin.Equals(other.LocalMin)
                && LocalMax.Equals(other.LocalMax);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsAabbCollider other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BodyId.Value;
                hash = (hash * 397) ^ ColliderId.Value;
                hash = (hash * 397) ^ Layer;
                hash = (hash * 397) ^ LocalMin.GetHashCode();
                hash = (hash * 397) ^ LocalMax.GetHashCode();
                return hash;
            }
        }
    }
}
