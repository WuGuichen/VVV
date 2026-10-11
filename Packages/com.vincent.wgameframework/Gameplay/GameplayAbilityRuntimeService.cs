using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Adapts entity/ability ids into an AbilityContext and invokes IAbility.Cast.</summary>
    public sealed class GameplayAbilityRuntimeService
    {
        public const string MissingCasterFailureReason = "MissingCaster";
        public const string MissingAbilityFailureReason = "MissingAbility";
        public const string EmptyCandidatesFailureReason = "EmptyCandidates";

        private readonly IReadOnlyList<IRuntimeEntity> _entities;
        private readonly GameplayAbilityRegistry _abilityRegistry;

        public GameplayAbilityRuntimeService(
            IReadOnlyList<IRuntimeEntity> entities,
            GameplayAbilityRegistry abilityRegistry)
        {
            _entities = entities ?? Array.Empty<IRuntimeEntity>();
            _abilityRegistry = abilityRegistry ?? throw new ArgumentNullException(nameof(abilityRegistry));
        }

        public GameplayAbilityRegistry AbilityRegistry => _abilityRegistry;

        public GameplayAbilityRuntimeResult Cast(GameplayAbilityCastRequest request)
        {
            if (!TryFindEntity(request.CasterEntityId, out IRuntimeEntity caster))
            {
                return GameplayAbilityRuntimeResult.Fail(
                    request,
                    GameplayAbilityRuntimeFailureCode.MissingCaster,
                    MissingCasterFailureReason);
            }

            if (!_abilityRegistry.TryGetAbility(request.AbilityId, out IAbility ability))
            {
                return GameplayAbilityRuntimeResult.Fail(
                    request,
                    GameplayAbilityRuntimeFailureCode.MissingAbility,
                    MissingAbilityFailureReason);
            }

            IReadOnlyList<IRuntimeEntity> candidates = BuildCandidates(request);
            if (candidates.Count == 0)
            {
                return GameplayAbilityRuntimeResult.Fail(
                    request,
                    GameplayAbilityRuntimeFailureCode.EmptyCandidates,
                    EmptyCandidatesFailureReason,
                    candidates);
            }

            AbilityCastResult castResult = ability.Cast(new AbilityContext(caster, candidates));
            return GameplayAbilityRuntimeResult.FromCast(request, castResult, candidates);
        }

        private IReadOnlyList<IRuntimeEntity> BuildCandidates(GameplayAbilityCastRequest request)
        {
            if (!request.HasExplicitCandidateIds)
                return CopyEntities(_entities);

            if (request.CandidateEntityIds.Count == 0)
                return Array.Empty<IRuntimeEntity>();

            var candidates = new List<IRuntimeEntity>(request.CandidateEntityIds.Count);
            var addedEntityIds = new HashSet<int>();
            for (int i = 0; i < request.CandidateEntityIds.Count; i++)
            {
                int entityId = request.CandidateEntityIds[i];
                if (!addedEntityIds.Add(entityId))
                    continue;

                if (TryFindEntity(entityId, out IRuntimeEntity entity))
                    candidates.Add(entity);
            }

            return candidates.Count == 0 ? Array.Empty<IRuntimeEntity>() : candidates.ToArray();
        }

        private bool TryFindEntity(int entityId, out IRuntimeEntity entity)
        {
            for (int i = 0; i < _entities.Count; i++)
            {
                IRuntimeEntity candidate = _entities[i];
                if (candidate != null && candidate.EntityId == entityId)
                {
                    entity = candidate;
                    return true;
                }
            }

            entity = null;
            return false;
        }

        private static IReadOnlyList<IRuntimeEntity> CopyEntities(IReadOnlyList<IRuntimeEntity> entities)
        {
            if (entities == null || entities.Count == 0)
                return Array.Empty<IRuntimeEntity>();

            IRuntimeEntity[] copy = new IRuntimeEntity[entities.Count];
            for (int i = 0; i < entities.Count; i++)
                copy[i] = entities[i];

            return copy;
        }
    }
}
