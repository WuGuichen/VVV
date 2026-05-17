using System;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Component-native armor integrity state. Armor does not recover automatically.
    /// </summary>
    public struct GameplayArmorIntegrityComponent : IGameplayComponent, IEquatable<GameplayArmorIntegrityComponent>
    {
        public GameplayArmorIntegrityComponent(
            int maxIntegrity,
            int currentIntegrity = -1)
        {
            if (currentIntegrity < 0)
                currentIntegrity = maxIntegrity;

            Validate(maxIntegrity, currentIntegrity, currentIntegrity == 0);

            MaxIntegrity = maxIntegrity;
            CurrentIntegrity = currentIntegrity;
            IsBroken = currentIntegrity == 0;
        }

        public int MaxIntegrity;
        public int CurrentIntegrity;
        public bool IsBroken;

        public int CurrentValue => CurrentIntegrity;
        public int MaxValue => MaxIntegrity;

        public bool HasValidState()
        {
            return IsValid(MaxIntegrity, CurrentIntegrity, IsBroken);
        }

        public bool Equals(GameplayArmorIntegrityComponent other)
        {
            return MaxIntegrity == other.MaxIntegrity
                && CurrentIntegrity == other.CurrentIntegrity
                && IsBroken == other.IsBroken;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayArmorIntegrityComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MaxIntegrity;
                hash = (hash * 397) ^ CurrentIntegrity;
                hash = (hash * 397) ^ (IsBroken ? 1 : 0);
                return hash;
            }
        }

        public GameplayArmorIntegrityComponent ApplyDamage(int incomingDamage, out bool brokeThisHit)
        {
            if (incomingDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(incomingDamage), "Incoming armor damage cannot be negative.");

            brokeThisHit = false;
            GameplayArmorIntegrityComponent updated = this;
            if (!updated.HasValidState() || updated.IsBroken || incomingDamage == 0)
                return updated;

            int previousIntegrity = updated.CurrentIntegrity;
            updated.CurrentIntegrity = Math.Max(0, updated.CurrentIntegrity - incomingDamage);
            brokeThisHit = previousIntegrity > 0 && updated.CurrentIntegrity == 0;
            if (brokeThisHit)
                updated.IsBroken = true;

            return updated;
        }

        private static bool IsValid(int maxIntegrity, int currentIntegrity, bool isBroken)
        {
            if (maxIntegrity <= 0)
                return false;
            if (currentIntegrity < 0 || currentIntegrity > maxIntegrity)
                return false;

            return isBroken == (currentIntegrity == 0);
        }

        internal static void Validate(int maxIntegrity, int currentIntegrity, bool isBroken)
        {
            if (maxIntegrity <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxIntegrity), "Armor integrity max value must be greater than zero.");
            if (currentIntegrity < 0 || currentIntegrity > maxIntegrity)
                throw new ArgumentOutOfRangeException(nameof(currentIntegrity), "Armor integrity current value must be between zero and max value.");
            if (isBroken != (currentIntegrity == 0))
                throw new ArgumentException("Armor integrity broken flag must match current integrity.", nameof(isBroken));
        }
    }
}
