using System;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatQueryHeader : IEquatable<CombatQueryHeader>
    {
        public CombatQueryHeader(
            int queryId,
            CombatQueryKind kind,
            CombatEntityId sourceEntityId,
            int traceId,
            int actionId,
            int sourceOrder,
            CombatPhysicsLayerMask layerMask)
        {
            if (queryId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queryId), "Query id cannot be negative.");
            }

            if (traceId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(traceId), "Trace id cannot be negative.");
            }

            if (actionId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionId), "Action id cannot be negative.");
            }

            if (sourceOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOrder), "Source order cannot be negative.");
            }

            QueryId = queryId;
            Kind = kind;
            SourceEntityId = sourceEntityId;
            TraceId = traceId;
            ActionId = actionId;
            SourceOrder = sourceOrder;
            LayerMask = layerMask;
        }

        public int QueryId { get; }

        public CombatQueryKind Kind { get; }

        public CombatEntityId SourceEntityId { get; }

        public int TraceId { get; }

        public int ActionId { get; }

        public int SourceOrder { get; }

        public CombatPhysicsLayerMask LayerMask { get; }

        public CombatSortKey ToSortKey(int primary)
        {
            return new CombatSortKey(
                primary,
                SourceEntityId,
                CombatBodyId.None,
                CombatColliderId.None,
                TraceId,
                ActionId,
                SourceOrder);
        }

        public bool Equals(CombatQueryHeader other)
        {
            return QueryId == other.QueryId
                && Kind == other.Kind
                && SourceEntityId.Equals(other.SourceEntityId)
                && TraceId == other.TraceId
                && ActionId == other.ActionId
                && SourceOrder == other.SourceOrder
                && LayerMask.Equals(other.LayerMask);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatQueryHeader other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = QueryId;
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ SourceEntityId.Value;
                hash = (hash * 397) ^ TraceId;
                hash = (hash * 397) ^ ActionId;
                hash = (hash * 397) ^ SourceOrder;
                hash = (hash * 397) ^ LayerMask.Value;
                return hash;
            }
        }
    }
}
