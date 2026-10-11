using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatQueryResult : IComparable<CombatQueryResult>, IEquatable<CombatQueryResult>
    {
        public CombatQueryResult(
            CombatQueryHeader query,
            CombatEntityId targetEntityId,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId,
            Fix64 distance,
            FixVector3 point,
            FixVector3 normal,
            int targetPriority = 0)
            : this(
                query,
                targetEntityId,
                targetBodyId,
                targetColliderId,
                CombatPhysicsShapeKind.Aabb,
                distance,
                point,
                normal,
                targetPriority)
        {
        }

        public CombatQueryResult(
            CombatQueryHeader query,
            CombatEntityId targetEntityId,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId,
            CombatPhysicsShapeKind targetShapeKind,
            Fix64 distance,
            FixVector3 point,
            FixVector3 normal,
            int targetPriority = 0)
        {
            if (!IsTargetShapeKind(targetShapeKind))
            {
                throw new ArgumentOutOfRangeException(nameof(targetShapeKind), "Query target shape kind must be AABB, sphere, capsule, or OBB.");
            }

            if (distance < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(distance), "Query hit distance cannot be negative.");
            }

            Query = query;
            TargetEntityId = targetEntityId;
            TargetBodyId = targetBodyId;
            TargetColliderId = targetColliderId;
            TargetShapeKind = targetShapeKind;
            Distance = distance;
            Point = point;
            Normal = normal;
            TargetPriority = targetPriority;
        }

        public CombatQueryHeader Query { get; }

        public CombatEntityId TargetEntityId { get; }

        public CombatBodyId TargetBodyId { get; }

        public CombatColliderId TargetColliderId { get; }

        public CombatPhysicsShapeKind TargetShapeKind { get; }

        public Fix64 Distance { get; }

        public FixVector3 Point { get; }

        public FixVector3 Normal { get; }

        public int TargetPriority { get; }

        public CombatSortKey ToSortKey()
        {
            return new CombatSortKey(
                checked((int)Distance.RawValue),
                TargetEntityId,
                TargetBodyId,
                TargetColliderId,
                Query.TraceId,
                Query.ActionId,
                Query.SourceOrder);
        }

        public int CompareTo(CombatQueryResult other)
        {
            int compare = Distance.RawValue.CompareTo(other.Distance.RawValue);
            if (compare != 0)
            {
                return compare;
            }

            compare = other.TargetPriority.CompareTo(TargetPriority);
            if (compare != 0)
            {
                return compare;
            }

            compare = TargetEntityId.CompareTo(other.TargetEntityId);
            if (compare != 0)
            {
                return compare;
            }

            compare = TargetBodyId.CompareTo(other.TargetBodyId);
            if (compare != 0)
            {
                return compare;
            }

            compare = TargetColliderId.CompareTo(other.TargetColliderId);
            if (compare != 0)
            {
                return compare;
            }

            compare = TargetShapeKind.CompareTo(other.TargetShapeKind);
            if (compare != 0)
            {
                return compare;
            }

            compare = Query.TraceId.CompareTo(other.Query.TraceId);
            if (compare != 0)
            {
                return compare;
            }

            compare = Query.ActionId.CompareTo(other.Query.ActionId);
            if (compare != 0)
            {
                return compare;
            }

            return Query.SourceOrder.CompareTo(other.Query.SourceOrder);
        }

        public bool Equals(CombatQueryResult other)
        {
            return Query.Equals(other.Query)
                && TargetEntityId.Equals(other.TargetEntityId)
                && TargetBodyId.Equals(other.TargetBodyId)
                && TargetColliderId.Equals(other.TargetColliderId)
                && TargetShapeKind == other.TargetShapeKind
                && Distance.Equals(other.Distance)
                && Point.Equals(other.Point)
                && Normal.Equals(other.Normal)
                && TargetPriority == other.TargetPriority;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatQueryResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Query.GetHashCode();
                hash = (hash * 397) ^ TargetEntityId.Value;
                hash = (hash * 397) ^ TargetBodyId.Value;
                hash = (hash * 397) ^ TargetColliderId.Value;
                hash = (hash * 397) ^ (int)TargetShapeKind;
                hash = (hash * 397) ^ Distance.GetHashCode();
                hash = (hash * 397) ^ Point.GetHashCode();
                hash = (hash * 397) ^ Normal.GetHashCode();
                hash = (hash * 397) ^ TargetPriority;
                return hash;
            }
        }

        private static bool IsTargetShapeKind(CombatPhysicsShapeKind targetShapeKind)
        {
            return targetShapeKind == CombatPhysicsShapeKind.Aabb
                || targetShapeKind == CombatPhysicsShapeKind.Sphere
                || targetShapeKind == CombatPhysicsShapeKind.Capsule
                || targetShapeKind == CombatPhysicsShapeKind.Obb;
        }
    }

    public sealed class CombatQueryResultComparer : IComparer<CombatQueryResult>
    {
        public static readonly CombatQueryResultComparer Instance = new CombatQueryResultComparer();

        private CombatQueryResultComparer()
        {
        }

        public int Compare(CombatQueryResult left, CombatQueryResult right)
        {
            int compare = left.Distance.RawValue.CompareTo(right.Distance.RawValue);
            if (compare != 0)
            {
                return compare;
            }

            compare = right.TargetPriority.CompareTo(left.TargetPriority);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.TargetEntityId.Value.CompareTo(right.TargetEntityId.Value);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.TargetBodyId.Value.CompareTo(right.TargetBodyId.Value);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.TargetColliderId.Value.CompareTo(right.TargetColliderId.Value);
            if (compare != 0)
            {
                return compare;
            }

            compare = ((int)left.TargetShapeKind).CompareTo((int)right.TargetShapeKind);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.Query.TraceId.CompareTo(right.Query.TraceId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.Query.ActionId.CompareTo(right.Query.ActionId);
            if (compare != 0)
            {
                return compare;
            }

            return left.Query.SourceOrder.CompareTo(right.Query.SourceOrder);
        }
    }
}
