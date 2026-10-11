using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatPhysicsSphereCollider : IEquatable<CombatPhysicsSphereCollider>
    {
        public CombatPhysicsSphereCollider(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            int layer,
            FixVector3 localCenter,
            Fix64 radius,
            Fix64 surfaceFriction = default,
            int priority = 0)
            : this(
                bodyId,
                colliderId,
                layer,
                localCenter,
                radius,
                CombatSurfaceMaterial.FromFriction(surfaceFriction),
                priority)
        {
        }

        public CombatPhysicsSphereCollider(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            int layer,
            FixVector3 localCenter,
            Fix64 radius,
            CombatSurfaceMaterial surfaceMaterial,
            int priority = 0)
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
            if (radius < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Sphere collider radius cannot be negative.");
            }

            BodyId = bodyId;
            ColliderId = colliderId;
            Layer = layer;
            LocalCenter = localCenter;
            Radius = radius;
            SurfaceMaterial = surfaceMaterial;
            Priority = priority;
        }

        public CombatBodyId BodyId { get; }

        public CombatColliderId ColliderId { get; }

        public int Layer { get; }

        public FixVector3 LocalCenter { get; }

        public Fix64 Radius { get; }

        public CombatSurfaceMaterial SurfaceMaterial { get; }

        public int Priority { get; }

        public CombatSurfaceMaterialKind SurfaceMaterialKind => SurfaceMaterial.Kind;

        public string SurfaceMaterialStableId => SurfaceMaterial.StableId;

        public Fix64 SurfaceFriction => SurfaceMaterial.Friction;

        public Fix64 SurfaceElasticity => SurfaceMaterial.Elasticity;

        public Fix64 SurfaceFixedBounceHeight => SurfaceMaterial.FixedBounceHeight;

        public FixVector3 GetWorldCenter(FixVector3 bodyPosition)
        {
            return bodyPosition + LocalCenter;
        }

        public FixVector3 GetWorldMin(FixVector3 bodyPosition)
        {
            FixVector3 center = GetWorldCenter(bodyPosition);
            var extent = new FixVector3(Radius, Radius, Radius);
            return center - extent;
        }

        public FixVector3 GetWorldMax(FixVector3 bodyPosition)
        {
            FixVector3 center = GetWorldCenter(bodyPosition);
            var extent = new FixVector3(Radius, Radius, Radius);
            return center + extent;
        }

        public bool Equals(CombatPhysicsSphereCollider other)
        {
            return BodyId.Equals(other.BodyId)
                && ColliderId.Equals(other.ColliderId)
                && Layer == other.Layer
                && LocalCenter.Equals(other.LocalCenter)
                && Radius.Equals(other.Radius)
                && SurfaceMaterial.Equals(other.SurfaceMaterial)
                && Priority == other.Priority;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsSphereCollider other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BodyId.Value;
                hash = (hash * 397) ^ ColliderId.Value;
                hash = (hash * 397) ^ Layer;
                hash = (hash * 397) ^ LocalCenter.GetHashCode();
                hash = (hash * 397) ^ Radius.GetHashCode();
                hash = (hash * 397) ^ SurfaceMaterial.GetHashCode();
                hash = (hash * 397) ^ Priority;
                return hash;
            }
        }
    }
}
