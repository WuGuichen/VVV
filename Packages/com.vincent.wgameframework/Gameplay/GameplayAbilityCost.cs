using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayAbilityCost : IEquatable<GameplayAbilityCost>
    {
        public GameplayAbilityCost(int attributeId, int amount)
        {
            if (attributeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(attributeId), "Gameplay ability cost attribute id must be greater than zero.");
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Gameplay ability cost amount cannot be negative.");

            AttributeId = attributeId;
            Amount = amount;
        }

        public int AttributeId { get; }
        public int Amount { get; }

        public bool Equals(GameplayAbilityCost other)
        {
            return AttributeId == other.AttributeId && Amount == other.Amount;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayAbilityCost other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (AttributeId * 397) ^ Amount;
            }
        }
    }
}
