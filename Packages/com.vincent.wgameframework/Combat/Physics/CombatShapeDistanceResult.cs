using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    /// <summary>
    /// Deterministic capsule-to-shape surface-distance fact. Positive distance is
    /// reported only while the shapes are separated; touching and overlapping
    /// shapes have a zero surface gap.
    /// </summary>
    public readonly struct CombatShapeDistanceResult : IEquatable<CombatShapeDistanceResult>
    {
        internal CombatShapeDistanceResult(
            CombatShapeSeparationStatus status,
            CombatQueryHeader sourceQuery,
            CombatEntityId targetEntityId,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId,
            CombatColliderId targetCompoundLeafId,
            CombatPhysicsShapeKind targetShapeKind,
            Fix64 surfaceGap,
            FixVector3 sourceWitnessPoint,
            FixVector3 targetWitnessPoint,
            int targetPriority)
        {
            Status = status;
            SourceQuery = sourceQuery;
            TargetEntityId = targetEntityId;
            TargetBodyId = targetBodyId;
            TargetColliderId = targetColliderId;
            TargetCompoundLeafId = targetCompoundLeafId;
            TargetShapeKind = targetShapeKind;
            SurfaceGap = surfaceGap < Fix64.Zero ? Fix64.Zero : surfaceGap;
            SourceWitnessPoint = sourceWitnessPoint;
            TargetWitnessPoint = targetWitnessPoint;
            TargetPriority = targetPriority;
        }

        public CombatShapeSeparationStatus Status { get; }
        public CombatQueryHeader SourceQuery { get; }
        public CombatEntityId TargetEntityId { get; }
        public CombatBodyId TargetBodyId { get; }
        public CombatColliderId TargetColliderId { get; }
        public CombatColliderId TargetCompoundLeafId { get; }
        public CombatPhysicsShapeKind TargetShapeKind { get; }
        public Fix64 SurfaceGap { get; }
        public FixVector3 SourceWitnessPoint { get; }
        public FixVector3 TargetWitnessPoint { get; }
        public int TargetPriority { get; }

        public bool HasDistance => Status == CombatShapeSeparationStatus.Separated
            || Status == CombatShapeSeparationStatus.Touching
            || Status == CombatShapeSeparationStatus.Overlapping;

        public bool Equals(CombatShapeDistanceResult other)
        {
            return Status == other.Status
                && SourceQuery.Equals(other.SourceQuery)
                && TargetEntityId.Equals(other.TargetEntityId)
                && TargetBodyId.Equals(other.TargetBodyId)
                && TargetColliderId.Equals(other.TargetColliderId)
                && TargetCompoundLeafId.Equals(other.TargetCompoundLeafId)
                && TargetShapeKind == other.TargetShapeKind
                && SurfaceGap.Equals(other.SurfaceGap)
                && SourceWitnessPoint.Equals(other.SourceWitnessPoint)
                && TargetWitnessPoint.Equals(other.TargetWitnessPoint)
                && TargetPriority == other.TargetPriority;
        }

        public override bool Equals(object obj) =>
            obj is CombatShapeDistanceResult other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Status;
                hash = (hash * 397) ^ SourceQuery.GetHashCode();
                hash = (hash * 397) ^ TargetEntityId.Value;
                hash = (hash * 397) ^ TargetBodyId.Value;
                hash = (hash * 397) ^ TargetColliderId.Value;
                hash = (hash * 397) ^ TargetCompoundLeafId.Value;
                hash = (hash * 397) ^ (int)TargetShapeKind;
                hash = (hash * 397) ^ SurfaceGap.GetHashCode();
                hash = (hash * 397) ^ SourceWitnessPoint.GetHashCode();
                hash = (hash * 397) ^ TargetWitnessPoint.GetHashCode();
                hash = (hash * 397) ^ TargetPriority;
                return hash;
            }
        }
    }
}
