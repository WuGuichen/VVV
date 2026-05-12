using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentAbilityRegistry
    {
        private readonly Dictionary<int, IGameplayComponentAbility> _abilities =
            new Dictionary<int, IGameplayComponentAbility>();

        public int Count => _abilities.Count;

        public void Register(IGameplayComponentAbility ability)
        {
            if (ability == null)
                throw new ArgumentNullException(nameof(ability));
            if (ability.AbilityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(ability), "Component ability id must be greater than zero.");
            if (_abilities.ContainsKey(ability.AbilityId))
                throw new InvalidOperationException($"Component ability id is already registered: {ability.AbilityId}.");

            _abilities.Add(ability.AbilityId, ability);
        }

        public bool TryGet(int abilityId, out IGameplayComponentAbility ability)
        {
            if (abilityId <= 0)
            {
                ability = null;
                return false;
            }

            return _abilities.TryGetValue(abilityId, out ability);
        }

        public IGameplayComponentAbility[] CreateSnapshot()
        {
            if (_abilities.Count == 0)
                return new IGameplayComponentAbility[0];

            var abilities = new List<IGameplayComponentAbility>(_abilities.Values);
            abilities.Sort(CompareAbilityId);
            return abilities.ToArray();
        }

        public void Clear()
        {
            _abilities.Clear();
        }

        private static int CompareAbilityId(IGameplayComponentAbility left, IGameplayComponentAbility right)
        {
            return left.AbilityId.CompareTo(right.AbilityId);
        }
    }
}
