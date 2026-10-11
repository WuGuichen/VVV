using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatPhysicsCapsuleCollider : IEquatable<CombatPhysicsCapsuleCollider>
    {
        public CombatPhysicsCapsuleCollider(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            int layer,
            FixVector3 localPointA,
            FixVector3 localPointB,
            Fix64 radius,
            Fix64 surfaceFriction = default,
            int priority = 0)
            : this(
                bodyId,
                colliderId,
                layer,
                localPointA,
                localPointB,
                radius,
                CombatSurfaceMaterial.FromFriction(surfaceFriction),
                priority)
        {
        }

        public CombatPhysicsCapsuleCollider(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            int layer,
            FixVector3 localPointA,
            FixVector3 localPointB,
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
                throw new ArgumentOutOfRangeException(nameof(radius), "Capsule collider radius cannot be negative.");
            }

            BodyId = bodyId;
            ColliderId = colliderId;
            Layer = layer;
            LocalPointA = localPointA;
            LocalPointB = localPointB;
            Radius = radius;
            SurfaceMaterial = surfaceMaterial;
            Priority = priority;
        }

        public CombatBodyId BodyId { get; }

        public CombatColliderId ColliderId { get; }

        public int Layer { get; }

        public FixVector3 LocalPointA { get; }

        public FixVector3 LocalPointB { get; }

        public Fix64 Radius { get; }

        public CombatSurfaceMaterial SurfaceMaterial { get; }

        public int Priority { get; }

        public CombatSurfaceMaterialKind SurfaceMaterialKind => SurfaceMaterial.Kind;

        public string SurfaceMaterialStableId => SurfaceMaterial.StableId;

        public Fix64 SurfaceFriction => SurfaceMaterial.Friction;

        public Fix64 SurfaceElasticity => SurfaceMaterial.Elasticity;

        public Fix64 SurfaceFixedBounceHeight => SurfaceMaterial.FixedBounceHeight;

        public FixVector3 GetWorldPointA(FixVector3 bodyPosition)
        {
            return bodyPosition + LocalPointA;
        }

        public FixVector3 GetWorldPointB(FixVector3 bodyPosition)
        {
            return bodyPosition + LocalPointB;
        }

        public FixVector3 GetWorldMin(FixVector3 bodyPosition)
        {
            FixVector3 pointA = GetWorldPointA(bodyPosition);
            FixVector3 pointB = GetWorldPointB(bodyPosition);
            var extent = new FixVector3(Radius, Radius, Radius);
            return Min(pointA, pointB) - extent;
        }

        public FixVector3 GetWorldMax(FixVector3 bodyPosition)
        {
            FixVector3 pointA = GetWorldPointA(bodyPosition);
            FixVector3 pointB = GetWorldPointB(bodyPosition);
            var extent = new FixVector3(Radius, Radius, Radius);
            return Max(pointA, pointB) + extent;
        }

        public bool Equals(CombatPhysicsCapsuleCollider other)
        {
            return BodyId.Equals(other.BodyId)
                && ColliderId.Equals(other.ColliderId)
                && Layer == other.Layer
                && LocalPointA.Equals(other.LocalPointA)
                && LocalPointB.Equals(other.LocalPointB)
                && Radius.Equals(other.Radius)
                && SurfaceMaterial.Equals(other.SurfaceMaterial)
                && Priority == other.Priority;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsCapsuleCollider other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BodyId.Value;
                hash = (hash * 397) ^ ColliderId.Value;
                hash = (hash * 397) ^ Layer;
                hash = (hash * 397) ^ LocalPointA.GetHashCode();
                hash = (hash * 397) ^ LocalPointB.GetHashCode();
                hash = (hash * 397) ^ Radius.GetHashCode();
                hash = (hash * 397) ^ SurfaceMaterial.GetHashCode();
                hash = (hash * 397) ^ Priority;
                return hash;
            }
        }

        private static FixVector3 Min(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Min(left.X, right.X),
                Fix64.Min(left.Y, right.Y),
                Fix64.Min(left.Z, right.Z));
        }

        private static FixVector3 Max(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Max(left.X, right.X),
                Fix64.Max(left.Y, right.Y),
                Fix64.Max(left.Z, right.Z));
        }
    }
}
