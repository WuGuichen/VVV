using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayAttributeValue : IEquatable<GameplayAttributeValue>
    {
        public GameplayAttributeValue(int attributeId, int baseValue, int currentValue)
        {
            if (attributeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(attributeId), "Gameplay attribute id must be greater than zero.");

            AttributeId = attributeId;
            BaseValue = baseValue;
            CurrentValue = currentValue;
        }

        public int AttributeId { get; }
        public int BaseValue { get; }
        public int CurrentValue { get; }

        public bool Equals(GameplayAttributeValue other)
        {
            return AttributeId == other.AttributeId
                && BaseValue == other.BaseValue
                && CurrentValue == other.CurrentValue;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayAttributeValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AttributeId;
                hash = (hash * 397) ^ BaseValue;
                hash = (hash * 397) ^ CurrentValue;
                return hash;
            }
        }
    }
}
