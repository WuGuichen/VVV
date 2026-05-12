using System;

namespace MxFramework.Combat.Core
{
    public readonly struct CombatSortKey : IComparable<CombatSortKey>, IEquatable<CombatSortKey>
    {
        public CombatSortKey(
            int primary,
            CombatEntityId entityId,
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            int traceId,
            int actionId,
            int sourceOrder)
        {
            Primary = primary;
            EntityId = entityId;
            BodyId = bodyId;
            ColliderId = colliderId;
            TraceId = traceId;
            ActionId = actionId;
            SourceOrder = sourceOrder;
        }

        public int Primary { get; }

        public CombatEntityId EntityId { get; }

        public CombatBodyId BodyId { get; }

        public CombatColliderId ColliderId { get; }

        public int TraceId { get; }

        public int ActionId { get; }

        public int SourceOrder { get; }

        public int CompareTo(CombatSortKey other)
        {
            int compare = Primary.CompareTo(other.Primary);
            if (compare != 0)
            {
                return compare;
            }

            compare = EntityId.CompareTo(other.EntityId);
            if (compare != 0)
            {
                return compare;
            }

            compare = BodyId.CompareTo(other.BodyId);
            if (compare != 0)
            {
                return compare;
            }

            compare = ColliderId.CompareTo(other.ColliderId);
            if (compare != 0)
            {
                return compare;
            }

            compare = TraceId.CompareTo(other.TraceId);
            if (compare != 0)
            {
                return compare;
            }

            compare = ActionId.CompareTo(other.ActionId);
            if (compare != 0)
            {
                return compare;
            }

            return SourceOrder.CompareTo(other.SourceOrder);
        }

        public bool Equals(CombatSortKey other)
        {
            return Primary == other.Primary
                && EntityId.Equals(other.EntityId)
                && BodyId.Equals(other.BodyId)
                && ColliderId.Equals(other.ColliderId)
                && TraceId == other.TraceId
                && ActionId == other.ActionId
                && SourceOrder == other.SourceOrder;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatSortKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Primary;
                hash = (hash * 397) ^ EntityId.Value;
                hash = (hash * 397) ^ BodyId.Value;
                hash = (hash * 397) ^ ColliderId.Value;
                hash = (hash * 397) ^ TraceId;
                hash = (hash * 397) ^ ActionId;
                hash = (hash * 397) ^ SourceOrder;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Primary}:{EntityId.Value}:{BodyId.Value}:{ColliderId.Value}:{TraceId}:{ActionId}:{SourceOrder}";
        }
    }
}
