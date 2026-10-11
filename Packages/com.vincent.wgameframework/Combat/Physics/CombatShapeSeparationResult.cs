using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public enum CombatShapeSeparationStatus
    {
        InvalidShape = 0,
        UnsupportedShape = 1,
        Separated = 2,
        Touching = 3,
        Overlapping = 4
    }

    /// <summary>
    /// Deterministic narrow-phase fact for one capsule source and one target shape.
    /// Contact normals point from the target toward the source.
    /// </summary>
    public readonly struct CombatShapeSeparationResult :
        IComparable<CombatShapeSeparationResult>,
        IEquatable<CombatShapeSeparationResult>
    {
        internal CombatShapeSeparationResult(
            CombatShapeSeparationStatus status,
            CombatQueryHeader sourceQuery,
            CombatEntityId targetEntityId,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId,
            CombatColliderId targetCompoundLeafId,
            CombatPhysicsShapeKind targetShapeKind,
            FixVector3 normal,
            Fix64 penetration,
            FixVector3 contactPoint,
            int targetPriority)
        {
            Status = status;
            SourceQuery = sourceQuery;
            TargetEntityId = targetEntityId;
            TargetBodyId = targetBodyId;
            TargetColliderId = targetColliderId;
            TargetCompoundLeafId = targetCompoundLeafId;
            TargetShapeKind = targetShapeKind;
            Normal = normal;
            Penetration = penetration;
            ContactPoint = contactPoint;
            TargetPriority = targetPriority;
        }

        public CombatShapeSeparationStatus Status { get; }

        public CombatQueryHeader SourceQuery { get; }

        public CombatEntityId TargetEntityId { get; }

        public CombatBodyId TargetBodyId { get; }

        public CombatColliderId TargetColliderId { get; }

        /// <summary>Equals the collider id for a compound leaf; otherwise None.</summary>
        public CombatColliderId TargetCompoundLeafId { get; }

        public CombatPhysicsShapeKind TargetShapeKind { get; }

        public FixVector3 Normal { get; }

        public Fix64 Penetration { get; }

        public FixVector3 ContactPoint { get; }

        public int TargetPriority { get; }

        public bool IsOverlapping => Status == CombatShapeSeparationStatus.Overlapping;

        public bool IsTouching => Status == CombatShapeSeparationStatus.Touching;

        public bool HasContact => IsOverlapping || IsTouching;

        public bool IsFailure => Status == CombatShapeSeparationStatus.InvalidShape
            || Status == CombatShapeSeparationStatus.UnsupportedShape;

        public int CompareTo(CombatShapeSeparationResult other)
        {
            return CombatShapeSeparationResultComparer.CompareValues(this, other);
        }

        public bool Equals(CombatShapeSeparationResult other)
        {
            return Status == other.Status
                && SourceQuery.Equals(other.SourceQuery)
                && TargetEntityId.Equals(other.TargetEntityId)
                && TargetBodyId.Equals(other.TargetBodyId)
                && TargetColliderId.Equals(other.TargetColliderId)
                && TargetCompoundLeafId.Equals(other.TargetCompoundLeafId)
                && TargetShapeKind == other.TargetShapeKind
                && Normal.Equals(other.Normal)
                && Penetration.Equals(other.Penetration)
                && ContactPoint.Equals(other.ContactPoint)
                && TargetPriority == other.TargetPriority;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatShapeSeparationResult other && Equals(other);
        }

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
                hash = (hash * 397) ^ Normal.GetHashCode();
                hash = (hash * 397) ^ Penetration.GetHashCode();
                hash = (hash * 397) ^ ContactPoint.GetHashCode();
                hash = (hash * 397) ^ TargetPriority;
                return hash;
            }
        }
    }

    public sealed class CombatShapeSeparationResultComparer : IComparer<CombatShapeSeparationResult>
    {
        public static readonly CombatShapeSeparationResultComparer Instance =
            new CombatShapeSeparationResultComparer();

        private CombatShapeSeparationResultComparer()
        {
        }

        public int Compare(CombatShapeSeparationResult left, CombatShapeSeparationResult right)
        {
            return CompareValues(left, right);
        }

        internal static int CompareValues(
            CombatShapeSeparationResult left,
            CombatShapeSeparationResult right)
        {
            int compare = CombatPhysicsQuery.CompareHeaders(left.SourceQuery, right.SourceQuery);
            if (compare != 0) return compare;

            compare = left.SourceQuery.LayerMask.Value.CompareTo(right.SourceQuery.LayerMask.Value);
            if (compare != 0) return compare;

            compare = left.TargetEntityId.Value.CompareTo(right.TargetEntityId.Value);
            if (compare != 0) return compare;

            compare = left.TargetBodyId.Value.CompareTo(right.TargetBodyId.Value);
            if (compare != 0) return compare;

            compare = left.TargetColliderId.Value.CompareTo(right.TargetColliderId.Value);
            if (compare != 0) return compare;

            compare = left.TargetCompoundLeafId.Value.CompareTo(right.TargetCompoundLeafId.Value);
            if (compare != 0) return compare;

            compare = ((int)left.TargetShapeKind).CompareTo((int)right.TargetShapeKind);
            if (compare != 0) return compare;

            compare = ((int)left.Status).CompareTo((int)right.Status);
            if (compare != 0) return compare;

            compare = right.Penetration.RawValue.CompareTo(left.Penetration.RawValue);
            if (compare != 0) return compare;

            compare = right.TargetPriority.CompareTo(left.TargetPriority);
            if (compare != 0) return compare;

            compare = CompareVector(left.Normal, right.Normal);
            if (compare != 0) return compare;

            return CompareVector(left.ContactPoint, right.ContactPoint);
        }

        private static int CompareVector(FixVector3 left, FixVector3 right)
        {
            int compare = left.X.RawValue.CompareTo(right.X.RawValue);
            if (compare != 0) return compare;
            compare = left.Y.RawValue.CompareTo(right.Y.RawValue);
            return compare != 0 ? compare : left.Z.RawValue.CompareTo(right.Z.RawValue);
        }
    }
}
