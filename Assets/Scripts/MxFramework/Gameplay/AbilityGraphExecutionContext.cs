using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public interface IAbilityGraphPhaseGate
    {
        bool IsPhaseActive(string phaseId);
    }

    public interface IAbilityGraphEventSink
    {
        void Publish(AbilityEvent evt);
    }

    /// <summary>Immutable inputs shared by one deterministic ability graph execution.</summary>
    public sealed class AbilityGraphExecutionContext
    {
        private readonly IRuntimeEntity[] _entities;
        private readonly int[] _candidateEntityIds;
        private readonly bool _hasExplicitCandidateEntityIds;
        private readonly GameplayTargetCandidate[] _targetCandidates;
        private readonly bool _hasExplicitTargetCandidates;

        public AbilityGraphExecutionContext(
            GameplayWorld world,
            int casterEntityId,
            int abilityId,
            IAbilityGraphEffectResolver effectResolver,
            GameplayTargetingService targetingService = null,
            IReadOnlyList<int> candidateEntityIds = null,
            IReadOnlyList<GameplayTargetCandidate> targetCandidates = null,
            IAbilityGraphEventSink eventSink = null,
            IAbilityGraphPhaseGate phaseGate = null)
            : this(
                world == null ? null : world.Entities.CreateSnapshot(),
                casterEntityId,
                abilityId,
                effectResolver,
                targetingService,
                candidateEntityIds,
                targetCandidates,
                eventSink,
                phaseGate)
        {
        }

        public AbilityGraphExecutionContext(
            IReadOnlyList<IRuntimeEntity> entities,
            int casterEntityId,
            int abilityId,
            IAbilityGraphEffectResolver effectResolver,
            GameplayTargetingService targetingService = null,
            IReadOnlyList<int> candidateEntityIds = null,
            IReadOnlyList<GameplayTargetCandidate> targetCandidates = null,
            IAbilityGraphEventSink eventSink = null,
            IAbilityGraphPhaseGate phaseGate = null)
        {
            _entities = CopyEntities(entities);
            CasterEntityId = casterEntityId;
            AbilityId = abilityId;
            EffectResolver = effectResolver;
            TargetingService = targetingService ?? new GameplayTargetingService();
            _candidateEntityIds = CopyIds(candidateEntityIds);
            _hasExplicitCandidateEntityIds = candidateEntityIds != null;
            _targetCandidates = CopyCandidates(targetCandidates);
            _hasExplicitTargetCandidates = targetCandidates != null;
            EventSink = eventSink;
            PhaseGate = phaseGate;
        }

        public int CasterEntityId { get; }
        public int AbilityId { get; }
        public IReadOnlyList<IRuntimeEntity> Entities => _entities;
        public IReadOnlyList<int> CandidateEntityIds => _candidateEntityIds;
        public bool HasExplicitCandidateEntityIds => _hasExplicitCandidateEntityIds;
        public IReadOnlyList<GameplayTargetCandidate> TargetCandidates => _targetCandidates;
        public bool HasExplicitTargetCandidates => _hasExplicitTargetCandidates;
        public GameplayTargetingService TargetingService { get; }
        public IAbilityGraphEffectResolver EffectResolver { get; }
        public IAbilityGraphEventSink EventSink { get; }
        public IAbilityGraphPhaseGate PhaseGate { get; }

        internal bool TryGetEntity(int entityId, out IRuntimeEntity entity)
        {
            if (entityId <= 0)
            {
                entity = null;
                return false;
            }

            for (int i = 0; i < _entities.Length; i++)
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

        internal IReadOnlyList<IRuntimeEntity> BuildTargetingRuntimeCandidates()
        {
            if (_hasExplicitCandidateEntityIds)
                return ResolveCandidateIds(_candidateEntityIds);

            return CopyEntities(_entities);
        }

        internal IReadOnlyList<IRuntimeEntity> BuildAbilityContextCandidates()
        {
            if (_hasExplicitTargetCandidates)
                return ResolveTargetCandidates(_targetCandidates, skipMissing: true);

            return BuildTargetingRuntimeCandidates();
        }

        internal bool TryResolveTargetCandidate(GameplayTargetCandidate candidate, out IRuntimeEntity entity)
        {
            entity = candidate.Entity;
            if (entity != null)
                return true;

            return TryGetEntity(candidate.EntityId, out entity);
        }

        private IReadOnlyList<IRuntimeEntity> ResolveCandidateIds(IReadOnlyList<int> entityIds)
        {
            if (entityIds == null || entityIds.Count == 0)
                return Array.Empty<IRuntimeEntity>();

            var entities = new List<IRuntimeEntity>(entityIds.Count);
            var addedEntityIds = new HashSet<int>();
            for (int i = 0; i < entityIds.Count; i++)
            {
                int entityId = entityIds[i];
                if (!addedEntityIds.Add(entityId))
                    continue;

                if (TryGetEntity(entityId, out IRuntimeEntity entity))
                    entities.Add(entity);
            }

            return entities.Count == 0 ? Array.Empty<IRuntimeEntity>() : entities.ToArray();
        }

        private IReadOnlyList<IRuntimeEntity> ResolveTargetCandidates(
            IReadOnlyList<GameplayTargetCandidate> candidates,
            bool skipMissing)
        {
            if (candidates == null || candidates.Count == 0)
                return Array.Empty<IRuntimeEntity>();

            var entities = new List<IRuntimeEntity>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (TryResolveTargetCandidate(candidates[i], out IRuntimeEntity entity))
                {
                    entities.Add(entity);
                }
                else if (!skipMissing)
                {
                    entities.Add(null);
                }
            }

            return entities.Count == 0 ? Array.Empty<IRuntimeEntity>() : entities.ToArray();
        }

        private static IRuntimeEntity[] CopyEntities(IReadOnlyList<IRuntimeEntity> entities)
        {
            if (entities == null || entities.Count == 0)
                return Array.Empty<IRuntimeEntity>();

            var copy = new IRuntimeEntity[entities.Count];
            for (int i = 0; i < entities.Count; i++)
                copy[i] = entities[i];

            return copy;
        }

        private static int[] CopyIds(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<int>();

            var copy = new int[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                copy[i] = ids[i];

            return copy;
        }

        private static GameplayTargetCandidate[] CopyCandidates(IReadOnlyList<GameplayTargetCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return Array.Empty<GameplayTargetCandidate>();

            var copy = new GameplayTargetCandidate[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
                copy[i] = candidates[i];

            return copy;
        }
    }
}
