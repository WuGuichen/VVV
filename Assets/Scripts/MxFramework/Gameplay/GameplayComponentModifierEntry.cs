using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayComponentModifierEntry : IEquatable<GameplayComponentModifierEntry>
    {
        public GameplayComponentModifierEntry(int modifierId, int attributeId, int addValue, int sourceBuffId = 0)
        {
            if (modifierId <= 0)
                throw new ArgumentOutOfRangeException(nameof(modifierId), "Gameplay component modifier id must be greater than zero.");
            if (attributeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(attributeId), "Gameplay component modifier attribute id must be greater than zero.");
            if (sourceBuffId < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceBuffId), "Gameplay component modifier source buff id cannot be negative.");

            ModifierId = modifierId;
            AttributeId = attributeId;
            AddValue = addValue;
            SourceBuffId = sourceBuffId;
        }

        public int ModifierId { get; }
        public int AttributeId { get; }
        public int AddValue { get; }
        public int SourceBuffId { get; }

        public bool Equals(GameplayComponentModifierEntry other)
        {
            return ModifierId == other.ModifierId
                && AttributeId == other.AttributeId
                && AddValue == other.AddValue
                && SourceBuffId == other.SourceBuffId;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayComponentModifierEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ModifierId;
                hash = (hash * 397) ^ AttributeId;
                hash = (hash * 397) ^ AddValue;
                hash = (hash * 397) ^ SourceBuffId;
                return hash;
            }
        }
    }
}
