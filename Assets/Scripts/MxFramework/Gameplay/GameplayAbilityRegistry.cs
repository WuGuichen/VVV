using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Stable in-memory registry for runtime abilities keyed by ability id.</summary>
    public sealed class GameplayAbilityRegistry
    {
        public const string NullAbilityFailureReason = "NullAbility";
        public const string DuplicateAbilityIdFailureReason = "DuplicateAbilityId";

        private readonly SortedDictionary<int, IAbility> _abilities = new SortedDictionary<int, IAbility>();

        public int Count => _abilities.Count;

        public bool TryRegister(IAbility ability, out string failureReason)
        {
            if (ability == null)
            {
                failureReason = NullAbilityFailureReason;
                return false;
            }

            if (_abilities.ContainsKey(ability.AbilityId))
            {
                failureReason = DuplicateAbilityIdFailureReason;
                return false;
            }

            _abilities.Add(ability.AbilityId, ability);
            failureReason = null;
            return true;
        }

        public bool TryGetAbility(int abilityId, out IAbility ability)
        {
            return _abilities.TryGetValue(abilityId, out ability);
        }

        public IReadOnlyList<int> GetAbilityIds()
        {
            if (_abilities.Count == 0)
                return Array.Empty<int>();

            int[] ids = new int[_abilities.Count];
            int index = 0;
            foreach (KeyValuePair<int, IAbility> ability in _abilities)
                ids[index++] = ability.Key;

            return ids;
        }

        public IReadOnlyList<IAbility> GetAbilities()
        {
            if (_abilities.Count == 0)
                return Array.Empty<IAbility>();

            IAbility[] abilities = new IAbility[_abilities.Count];
            int index = 0;
            foreach (KeyValuePair<int, IAbility> ability in _abilities)
                abilities[index++] = ability.Value;

            return abilities;
        }

        public void Clear()
        {
            _abilities.Clear();
        }
    }
}
