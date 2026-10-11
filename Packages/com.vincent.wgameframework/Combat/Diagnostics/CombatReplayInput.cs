using System;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Diagnostics
{
    public readonly struct CombatReplayInput : IComparable<CombatReplayInput>, IEquatable<CombatReplayInput>
    {
        public CombatReplayInput(CombatFrame frame, CombatEntityId entityId, int commandId, int value = 0, int sourceOrder = 0)
        {
            if (commandId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(commandId), "Command id cannot be negative.");
            }

            if (sourceOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOrder), "Source order cannot be negative.");
            }

            Frame = frame;
            EntityId = entityId;
            CommandId = commandId;
            Value = value;
            SourceOrder = sourceOrder;
        }

        public CombatFrame Frame { get; }

        public CombatEntityId EntityId { get; }

        public int CommandId { get; }

        public int Value { get; }

        public int SourceOrder { get; }

        public CombatHash AppendHash(CombatHash hash)
        {
            return hash
                .Add(Frame)
                .Add(EntityId)
                .Add(CommandId)
                .Add(Value)
                .Add(SourceOrder);
        }

        public int CompareTo(CombatReplayInput other)
        {
            int compare = Frame.CompareTo(other.Frame);
            if (compare != 0)
            {
                return compare;
            }

            compare = EntityId.CompareTo(other.EntityId);
            if (compare != 0)
            {
                return compare;
            }

            compare = SourceOrder.CompareTo(other.SourceOrder);
            if (compare != 0)
            {
                return compare;
            }

            return CommandId.CompareTo(other.CommandId);
        }

        public bool Equals(CombatReplayInput other)
        {
            return Frame.Equals(other.Frame)
                && EntityId.Equals(other.EntityId)
                && CommandId == other.CommandId
                && Value == other.Value
                && SourceOrder == other.SourceOrder;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatReplayInput other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.Value;
                hash = (hash * 397) ^ EntityId.Value;
                hash = (hash * 397) ^ CommandId;
                hash = (hash * 397) ^ Value;
                hash = (hash * 397) ^ SourceOrder;
                return hash;
            }
        }
    }
}
