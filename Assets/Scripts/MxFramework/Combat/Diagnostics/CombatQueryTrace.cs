using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;

namespace MxFramework.Combat.Diagnostics
{
    public readonly struct CombatQueryTrace : IComparable<CombatQueryTrace>, IEquatable<CombatQueryTrace>
    {
        public CombatQueryTrace(CombatFrame frame, CombatQueryHeader query)
        {
            Frame = frame;
            Query = query;
        }

        public CombatFrame Frame { get; }

        public CombatQueryHeader Query { get; }

        public CombatHash AppendHash(CombatHash hash)
        {
            return hash
                .Add(Frame)
                .Add(Query.QueryId)
                .Add((int)Query.Kind)
                .Add(Query.SourceEntityId)
                .Add(Query.TraceId)
                .Add(Query.ActionId)
                .Add(Query.SourceOrder)
                .Add(Query.LayerMask.Value);
        }

        public int CompareTo(CombatQueryTrace other)
        {
            int compare = Frame.CompareTo(other.Frame);
            if (compare != 0)
            {
                return compare;
            }

            compare = Query.QueryId.CompareTo(other.Query.QueryId);
            if (compare != 0)
            {
                return compare;
            }

            return Query.ToSortKey(0).CompareTo(other.Query.ToSortKey(0));
        }

        public bool Equals(CombatQueryTrace other)
        {
            return Frame.Equals(other.Frame) && Query.Equals(other.Query);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatQueryTrace other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Frame.Value * 397) ^ Query.GetHashCode();
            }
        }
    }
}
