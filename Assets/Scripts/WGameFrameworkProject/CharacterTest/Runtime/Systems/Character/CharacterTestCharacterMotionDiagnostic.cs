using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestMotionSurfaceClassification
    {
        None = 0,
        Walkable = 1,
        Steep = 2,
        Ceiling = 3,
        Wall = 4,
    }

    /// <summary>
    /// Read-only, same-frame projection of one CharacterTest character motion step.
    /// </summary>
    /// <remarks>
    /// This is transient developer diagnostics. It does not affect future ticks and is not
    /// part of CharacterTest authority snapshots, hashes, replay payloads, or network state.
    /// </remarks>
    public readonly struct CharacterTestCharacterMotionDiagnostic :
        IEquatable<CharacterTestCharacterMotionDiagnostic>
    {
        internal CharacterTestCharacterMotionDiagnostic(
            RuntimeFrame frame,
            int entityId,
            bool hasPhysics,
            bool hasCollision,
            CombatEntityId targetEntityId,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId,
            CombatPhysicsShapeKind targetShapeKind,
            FixVector3 normal,
            CombatMotionCollisionFlags collisionFlags,
            Fix64 collisionDistance,
            Fix64 collisionFraction,
            CombatSurfaceMaterial surfaceMaterial,
            Fix64 groundMinNormalY,
            CharacterTestMotionSurfaceClassification surfaceClassification,
            FixVector3 requestedDelta,
            FixVector3 appliedDelta,
            FixVector3 resolvedVelocity,
            bool grounded,
            Fix64 groundSnapAppliedDistance)
        {
            Frame = frame;
            EntityId = entityId;
            HasPhysics = hasPhysics;
            HasCollision = hasCollision;
            TargetEntityId = targetEntityId;
            TargetBodyId = targetBodyId;
            TargetColliderId = targetColliderId;
            TargetShapeKind = targetShapeKind;
            Normal = normal;
            CollisionFlags = collisionFlags;
            CollisionDistance = collisionDistance;
            CollisionFraction = collisionFraction;
            SurfaceMaterial = surfaceMaterial;
            GroundMinNormalY = groundMinNormalY;
            SurfaceClassification = surfaceClassification;
            RequestedDelta = requestedDelta;
            AppliedDelta = appliedDelta;
            ResolvedVelocity = resolvedVelocity;
            Grounded = grounded;
            GroundSnapAppliedDistance = groundSnapAppliedDistance;
        }

        public RuntimeFrame Frame { get; }

        public int EntityId { get; }

        public bool HasPhysics { get; }

        public bool HasCollision { get; }

        public CombatEntityId TargetEntityId { get; }

        public CombatBodyId TargetBodyId { get; }

        public CombatColliderId TargetColliderId { get; }

        public CombatPhysicsShapeKind TargetShapeKind { get; }

        public FixVector3 Normal { get; }

        public CombatMotionCollisionFlags CollisionFlags { get; }

        public Fix64 CollisionDistance { get; }

        public Fix64 CollisionFraction { get; }

        public CombatSurfaceMaterial SurfaceMaterial { get; }

        public Fix64 GroundMinNormalY { get; }

        public CharacterTestMotionSurfaceClassification SurfaceClassification { get; }

        public FixVector3 RequestedDelta { get; }

        public FixVector3 AppliedDelta { get; }

        public FixVector3 ResolvedVelocity { get; }

        public bool Grounded { get; }

        public Fix64 GroundSnapAppliedDistance { get; }

        public bool Equals(CharacterTestCharacterMotionDiagnostic other)
        {
            return Frame.Equals(other.Frame)
                && EntityId == other.EntityId
                && HasPhysics == other.HasPhysics
                && HasCollision == other.HasCollision
                && TargetEntityId.Equals(other.TargetEntityId)
                && TargetBodyId.Equals(other.TargetBodyId)
                && TargetColliderId.Equals(other.TargetColliderId)
                && TargetShapeKind == other.TargetShapeKind
                && Normal.Equals(other.Normal)
                && CollisionFlags == other.CollisionFlags
                && CollisionDistance.Equals(other.CollisionDistance)
                && CollisionFraction.Equals(other.CollisionFraction)
                && SurfaceMaterial.Equals(other.SurfaceMaterial)
                && GroundMinNormalY.Equals(other.GroundMinNormalY)
                && SurfaceClassification == other.SurfaceClassification
                && RequestedDelta.Equals(other.RequestedDelta)
                && AppliedDelta.Equals(other.AppliedDelta)
                && ResolvedVelocity.Equals(other.ResolvedVelocity)
                && Grounded == other.Grounded
                && GroundSnapAppliedDistance.Equals(other.GroundSnapAppliedDistance);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestCharacterMotionDiagnostic other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ EntityId;
                hash = (hash * 397) ^ (HasPhysics ? 1 : 0);
                hash = (hash * 397) ^ (HasCollision ? 1 : 0);
                hash = (hash * 397) ^ TargetEntityId.GetHashCode();
                hash = (hash * 397) ^ TargetBodyId.GetHashCode();
                hash = (hash * 397) ^ TargetColliderId.GetHashCode();
                hash = (hash * 397) ^ (int)TargetShapeKind;
                hash = (hash * 397) ^ Normal.GetHashCode();
                hash = (hash * 397) ^ (int)CollisionFlags;
                hash = (hash * 397) ^ CollisionDistance.GetHashCode();
                hash = (hash * 397) ^ CollisionFraction.GetHashCode();
                hash = (hash * 397) ^ SurfaceMaterial.GetHashCode();
                hash = (hash * 397) ^ GroundMinNormalY.GetHashCode();
                hash = (hash * 397) ^ (int)SurfaceClassification;
                hash = (hash * 397) ^ RequestedDelta.GetHashCode();
                hash = (hash * 397) ^ AppliedDelta.GetHashCode();
                hash = (hash * 397) ^ ResolvedVelocity.GetHashCode();
                hash = (hash * 397) ^ (Grounded ? 1 : 0);
                hash = (hash * 397) ^ GroundSnapAppliedDistance.GetHashCode();
                return hash;
            }
        }
    }
}
