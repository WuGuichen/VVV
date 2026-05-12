using System;

namespace MxFramework.Combat.Core
{
    public readonly struct CombatHash : IEquatable<CombatHash>
    {
        public const ulong OffsetBasis = 14695981039346656037UL;
        public const ulong Prime = 1099511628211UL;

        public static readonly CombatHash Empty = new CombatHash(OffsetBasis);

        public CombatHash(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public CombatHash Add(int value)
        {
            return Add(unchecked((uint)value));
        }

        public CombatHash Add(uint value)
        {
            unchecked
            {
                ulong hash = Value;
                hash = (hash ^ (value & 0xFFUL)) * Prime;
                hash = (hash ^ ((value >> 8) & 0xFFUL)) * Prime;
                hash = (hash ^ ((value >> 16) & 0xFFUL)) * Prime;
                hash = (hash ^ ((value >> 24) & 0xFFUL)) * Prime;
                return new CombatHash(hash);
            }
        }

        public CombatHash Add(ulong value)
        {
            unchecked
            {
                ulong hash = Value;
                for (int i = 0; i < 8; i++)
                {
                    hash = (hash ^ ((value >> (i * 8)) & 0xFFUL)) * Prime;
                }

                return new CombatHash(hash);
            }
        }

        public CombatHash Add(CombatFrame frame)
        {
            return Add(frame.Value);
        }

        public CombatHash Add(CombatEntityId entityId)
        {
            return Add(entityId.Value);
        }

        public CombatHash Add(CombatBodyId bodyId)
        {
            return Add(bodyId.Value);
        }

        public CombatHash Add(CombatColliderId colliderId)
        {
            return Add(colliderId.Value);
        }

        public CombatHash Add(CombatSortKey sortKey)
        {
            return Add(sortKey.Primary)
                .Add(sortKey.EntityId)
                .Add(sortKey.BodyId)
                .Add(sortKey.ColliderId)
                .Add(sortKey.TraceId)
                .Add(sortKey.ActionId)
                .Add(sortKey.SourceOrder);
        }

        public bool Equals(CombatHash other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatHash other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Value * 397) ^ (int)(Value >> 32);
            }
        }

        public override string ToString()
        {
            return Value.ToString("X16");
        }
    }
}
