using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayComponentAbilityRequestHandle : IEquatable<GameplayComponentAbilityRequestHandle>
    {
        public GameplayComponentAbilityRequestHandle(int index, int generation)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "Component ability request handle index cannot be negative.");
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation), "Component ability request handle generation cannot be negative.");
            if ((index == 0) != (generation == 0))
                throw new ArgumentException("Component ability request handle must be either default or have both index and generation greater than zero.");

            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index > 0 && Generation > 0;

        public bool Equals(GameplayComponentAbilityRequestHandle other)
        {
            return Index == other.Index && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayComponentAbilityRequestHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Index * 397) ^ Generation;
            }
        }
    }

    public sealed class GameplayComponentAbilityRequest
    {
        private readonly GameplayEntityId[] _candidateEntityIds;

        public GameplayComponentAbilityRequest(
            GameplayEntityId casterEntityId,
            int abilityId,
            IReadOnlyList<GameplayEntityId> candidateEntityIds = null,
            GameplayComponentTargetQuery targetQuery = null)
        {
            if (!casterEntityId.IsValid)
                throw new ArgumentException("Component ability request caster entity id must be valid.", nameof(casterEntityId));
            if (abilityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(abilityId), "Component ability request ability id must be greater than zero.");

            CasterEntityId = casterEntityId;
            AbilityId = abilityId;
            TargetQuery = targetQuery;
            _candidateEntityIds = CopyCandidates(candidateEntityIds);
        }

        public GameplayEntityId CasterEntityId { get; }
        public int AbilityId { get; }
        public IReadOnlyList<GameplayEntityId> CandidateEntityIds => _candidateEntityIds;
        public GameplayComponentTargetQuery TargetQuery { get; }

        private static GameplayEntityId[] CopyCandidates(IReadOnlyList<GameplayEntityId> ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<GameplayEntityId>();

            var copy = new GameplayEntityId[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                if (!ids[i].IsValid)
                    throw new ArgumentException("Component ability request candidate entity ids must be valid.", nameof(ids));

                copy[i] = ids[i];
            }

            return copy;
        }
    }
}
