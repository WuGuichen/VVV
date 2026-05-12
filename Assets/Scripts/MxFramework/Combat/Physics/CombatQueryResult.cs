using System;
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
            FixVector3 normal)
        {
            if (distance < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(distance), "Query hit distance cannot be negative.");
            }

            Query = query;
            TargetEntityId = targetEntityId;
            TargetBodyId = targetBodyId;
            TargetColliderId = targetColliderId;
            Distance = distance;
            Point = point;
            Normal = normal;
        }

        public CombatQueryHeader Query { get; }

        public CombatEntityId TargetEntityId { get; }

        public CombatBodyId TargetBodyId { get; }

        public CombatColliderId TargetColliderId { get; }

        public Fix64 Distance { get; }

        public FixVector3 Point { get; }

        public FixVector3 Normal { get; }

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
                && Distance.Equals(other.Distance)
                && Point.Equals(other.Point)
                && Normal.Equals(other.Normal);
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
                hash = (hash * 397) ^ Distance.GetHashCode();
                hash = (hash * 397) ^ Point.GetHashCode();
                hash = (hash * 397) ^ Normal.GetHashCode();
                return hash;
            }
        }
    }
}
