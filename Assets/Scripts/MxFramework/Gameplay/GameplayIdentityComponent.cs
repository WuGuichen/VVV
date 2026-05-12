using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayIdentityComponent : IGameplayComponent, IEquatable<GameplayIdentityComponent>
    {
        public GameplayIdentityComponent(int definitionId, int variantId = 0)
        {
            if (definitionId < 0)
                throw new ArgumentOutOfRangeException(nameof(definitionId), "Definition id cannot be negative.");
            if (variantId < 0)
                throw new ArgumentOutOfRangeException(nameof(variantId), "Variant id cannot be negative.");

            DefinitionId = definitionId;
            VariantId = variantId;
        }

        public int DefinitionId { get; }
        public int VariantId { get; }
        public bool IsNone => DefinitionId == 0 && VariantId == 0;

        public bool Equals(GameplayIdentityComponent other)
        {
            return DefinitionId == other.DefinitionId && VariantId == other.VariantId;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayIdentityComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (DefinitionId * 397) ^ VariantId;
            }
        }
    }
}
