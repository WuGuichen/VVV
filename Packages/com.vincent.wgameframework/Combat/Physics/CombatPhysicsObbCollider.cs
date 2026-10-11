using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatPhysicsObbCollider : IEquatable<CombatPhysicsObbCollider>
    {
        public CombatPhysicsObbCollider(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            int layer,
            FixVector3 localCenter,
            FixVector3 halfExtents,
            FixVector3 axisX,
            FixVector3 axisY,
            FixVector3 axisZ,
            CombatSurfaceMaterial surfaceMaterial = default,
            int priority = 0)
        {
            if (bodyId.IsNone) throw new ArgumentException("Combat physics collider body id cannot be none.", nameof(bodyId));
            if (colliderId.IsNone) throw new ArgumentException("Combat physics collider id cannot be none.", nameof(colliderId));
            CombatPhysicsLayerMask.FromLayer(layer);
            CombatPhysicsObbMath.ValidateHalfExtents(halfExtents, nameof(halfExtents));
            CombatPhysicsObbMath.NormalizeAxes(axisX, axisY, axisZ, out FixVector3 normalizedX, out FixVector3 normalizedY, out FixVector3 normalizedZ);
            BodyId = bodyId;
            ColliderId = colliderId;
            Layer = layer;
            LocalCenter = localCenter;
            HalfExtents = halfExtents;
            AxisX = normalizedX;
            AxisY = normalizedY;
            AxisZ = normalizedZ;
            SurfaceMaterial = surfaceMaterial;
            Priority = priority;
        }

        public CombatBodyId BodyId { get; }
        public CombatColliderId ColliderId { get; }
        public int Layer { get; }
        public FixVector3 LocalCenter { get; }
        public FixVector3 HalfExtents { get; }
        public FixVector3 AxisX { get; }
        public FixVector3 AxisY { get; }
        public FixVector3 AxisZ { get; }
        public CombatSurfaceMaterial SurfaceMaterial { get; }
        public int Priority { get; }
        public FixVector3 GetWorldCenter(FixVector3 bodyPosition) => bodyPosition + LocalCenter;
        public void GetWorldAabb(FixVector3 bodyPosition, out FixVector3 min, out FixVector3 max) => CombatPhysicsObbMath.GetAabb(GetWorldCenter(bodyPosition), HalfExtents, AxisX, AxisY, AxisZ, out min, out max);

        public bool Equals(CombatPhysicsObbCollider other) => BodyId.Equals(other.BodyId) && ColliderId.Equals(other.ColliderId)
            && Layer == other.Layer && LocalCenter.Equals(other.LocalCenter) && HalfExtents.Equals(other.HalfExtents)
            && AxisX.Equals(other.AxisX) && AxisY.Equals(other.AxisY) && AxisZ.Equals(other.AxisZ)
            && SurfaceMaterial.Equals(other.SurfaceMaterial) && Priority == other.Priority;
        public override bool Equals(object obj) => obj is CombatPhysicsObbCollider other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BodyId.Value;
                hash = (hash * 397) ^ ColliderId.Value;
                hash = (hash * 397) ^ Layer;
                hash = (hash * 397) ^ LocalCenter.GetHashCode();
                hash = (hash * 397) ^ HalfExtents.GetHashCode();
                hash = (hash * 397) ^ AxisX.GetHashCode();
                hash = (hash * 397) ^ AxisY.GetHashCode();
                hash = (hash * 397) ^ AxisZ.GetHashCode();
                hash = (hash * 397) ^ SurfaceMaterial.GetHashCode();
                hash = (hash * 397) ^ Priority;
                return hash;
            }
        }
    }
}
