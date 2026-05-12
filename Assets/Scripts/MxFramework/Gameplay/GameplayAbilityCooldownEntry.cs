using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayAbilityCooldownEntry : IEquatable<GameplayAbilityCooldownEntry>
    {
        public GameplayAbilityCooldownEntry(int abilityId, long endFrame)
        {
            if (abilityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(abilityId), "Gameplay ability cooldown ability id must be greater than zero.");
            if (endFrame < 0L)
                throw new ArgumentOutOfRangeException(nameof(endFrame), "Gameplay ability cooldown end frame cannot be negative.");

            AbilityId = abilityId;
            EndFrame = endFrame;
        }

        public int AbilityId { get; }
        public long EndFrame { get; }

        public bool Equals(GameplayAbilityCooldownEntry other)
        {
            return AbilityId == other.AbilityId && EndFrame == other.EndFrame;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayAbilityCooldownEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (AbilityId * 397) ^ EndFrame.GetHashCode();
            }
        }
    }
}
