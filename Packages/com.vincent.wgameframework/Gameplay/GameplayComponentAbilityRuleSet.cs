using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentAbilityRuleSet
    {
        private static readonly GameplayAbilityCost[] EmptyCosts = new GameplayAbilityCost[0];

        public GameplayComponentAbilityRuleSet(long cooldownFrames = 0L, IReadOnlyList<GameplayAbilityCost> costs = null)
        {
            if (cooldownFrames < 0L)
                throw new ArgumentOutOfRangeException(nameof(cooldownFrames), "Component ability cooldown frames cannot be negative.");

            CooldownFrames = cooldownFrames;
            Costs = CopySortedCosts(costs);
        }

        public static GameplayComponentAbilityRuleSet Empty { get; } = new GameplayComponentAbilityRuleSet();

        public long CooldownFrames { get; }
        public IReadOnlyList<GameplayAbilityCost> Costs { get; }
        public bool IsEmpty => CooldownFrames == 0L && Costs.Count == 0;

        private static GameplayAbilityCost[] CopySortedCosts(IReadOnlyList<GameplayAbilityCost> costs)
        {
            if (costs == null || costs.Count == 0)
                return EmptyCosts;

            var copy = new GameplayAbilityCost[costs.Count];
            for (int i = 0; i < costs.Count; i++)
                copy[i] = costs[i];

            Array.Sort(copy, CompareCosts);
            for (int i = 1; i < copy.Length; i++)
            {
                if (copy[i - 1].AttributeId == copy[i].AttributeId)
                    throw new ArgumentException("Component ability costs cannot contain duplicate attribute ids.", nameof(costs));
            }

            return copy;
        }

        private static int CompareCosts(GameplayAbilityCost left, GameplayAbilityCost right)
        {
            return left.AttributeId.CompareTo(right.AttributeId);
        }
    }
}
