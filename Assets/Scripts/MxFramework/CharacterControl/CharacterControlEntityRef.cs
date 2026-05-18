using System;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;

namespace MxFramework.CharacterControl
{
    public readonly struct CharacterControlEntityRef : IEquatable<CharacterControlEntityRef>
    {
        public CharacterControlEntityRef(
            int stableId,
            GameplayEntityId gameplayEntityId,
            CombatEntityId combatEntityId,
            CombatBodyId combatBodyId)
        {
            if (stableId < 0)
                throw new ArgumentOutOfRangeException(nameof(stableId), "Character stable id cannot be negative.");

            StableId = stableId;
            GameplayEntityId = gameplayEntityId;
            CombatEntityId = combatEntityId;
            CombatBodyId = combatBodyId;
        }

        public int StableId { get; }

        public GameplayEntityId GameplayEntityId { get; }

        public CombatEntityId CombatEntityId { get; }

        public CombatBodyId CombatBodyId { get; }

        public bool IsValid => StableId > 0 || GameplayEntityId.IsValid || !CombatEntityId.IsNone || !CombatBodyId.IsNone;

        public bool HasGameplayEntity => GameplayEntityId.IsValid;

        public bool HasCombatEntity => !CombatEntityId.IsNone;

        public bool HasCombatBody => !CombatBodyId.IsNone;

        public static CharacterControlEntityRef FromGameplay(GameplayEntityId gameplayEntityId, int stableId = 0)
        {
            return new CharacterControlEntityRef(stableId, gameplayEntityId, CombatEntityId.None, CombatBodyId.None);
        }

        public static CharacterControlEntityRef FromCombat(CombatEntityId combatEntityId, CombatBodyId combatBodyId, int stableId = 0)
        {
            return new CharacterControlEntityRef(stableId, default, combatEntityId, combatBodyId);
        }

        public static CharacterControlEntityRef FromGameplayAndCombat(
            GameplayEntityId gameplayEntityId,
            CombatEntityId combatEntityId,
            CombatBodyId combatBodyId,
            int stableId = 0)
        {
            return new CharacterControlEntityRef(stableId, gameplayEntityId, combatEntityId, combatBodyId);
        }

        public bool Equals(CharacterControlEntityRef other)
        {
            return StableId == other.StableId
                && GameplayEntityId.Equals(other.GameplayEntityId)
                && CombatEntityId.Equals(other.CombatEntityId)
                && CombatBodyId.Equals(other.CombatBodyId);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterControlEntityRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StableId;
                hash = (hash * 397) ^ GameplayEntityId.GetHashCode();
                hash = (hash * 397) ^ CombatEntityId.GetHashCode();
                hash = (hash * 397) ^ CombatBodyId.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return "Stable=" + StableId
                + " Gameplay=" + GameplayEntityId
                + " Combat=" + CombatEntityId
                + " Body=" + CombatBodyId;
        }
    }
}
