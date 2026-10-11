using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;

namespace MxFramework.Combat.GameplayBridge
{
    /// <summary>
    /// Summary for the last combat gameplay aura buff tick.
    /// </summary>
    public readonly struct CombatGameplayAuraBuffTickSummary : IEquatable<CombatGameplayAuraBuffTickSummary>
    {
        public CombatGameplayAuraBuffTickSummary(
            int sourceCount,
            int queryCount,
            int appliedOrRefreshedCount,
            int rejectedTargetCount,
            int skippedMissingDefinitionCount,
            int skippedMissingCombatMapCount,
            int skippedMissingAnchorCount,
            int skippedMissingGameplayTargetCount)
        {
            SourceCount = sourceCount;
            QueryCount = queryCount;
            AppliedOrRefreshedCount = appliedOrRefreshedCount;
            RejectedTargetCount = rejectedTargetCount;
            SkippedMissingDefinitionCount = skippedMissingDefinitionCount;
            SkippedMissingCombatMapCount = skippedMissingCombatMapCount;
            SkippedMissingAnchorCount = skippedMissingAnchorCount;
            SkippedMissingGameplayTargetCount = skippedMissingGameplayTargetCount;
        }

        public int SourceCount { get; }
        public int QueryCount { get; }
        public int AppliedOrRefreshedCount { get; }
        public int RejectedTargetCount { get; }
        public int SkippedMissingDefinitionCount { get; }
        public int SkippedMissingCombatMapCount { get; }
        public int SkippedMissingAnchorCount { get; }
        public int SkippedMissingGameplayTargetCount { get; }

        public bool Equals(CombatGameplayAuraBuffTickSummary other)
        {
            return SourceCount == other.SourceCount
                && QueryCount == other.QueryCount
                && AppliedOrRefreshedCount == other.AppliedOrRefreshedCount
                && RejectedTargetCount == other.RejectedTargetCount
                && SkippedMissingDefinitionCount == other.SkippedMissingDefinitionCount
                && SkippedMissingCombatMapCount == other.SkippedMissingCombatMapCount
                && SkippedMissingAnchorCount == other.SkippedMissingAnchorCount
                && SkippedMissingGameplayTargetCount == other.SkippedMissingGameplayTargetCount;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatGameplayAuraBuffTickSummary other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceCount;
                hash = (hash * 397) ^ QueryCount;
                hash = (hash * 397) ^ AppliedOrRefreshedCount;
                hash = (hash * 397) ^ RejectedTargetCount;
                hash = (hash * 397) ^ SkippedMissingDefinitionCount;
                hash = (hash * 397) ^ SkippedMissingCombatMapCount;
                hash = (hash * 397) ^ SkippedMissingAnchorCount;
                hash = (hash * 397) ^ SkippedMissingGameplayTargetCount;
                return hash;
            }
        }
    }

    /// <summary>
    /// Applies and refreshes component-native buffs for targets inside caster-following combat auras.
    /// </summary>
    public sealed class CombatGameplayAuraBuffSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.bridge.combat.gameplay_aura_buff";

        private readonly CombatEntityGameplayMap _entityMap;
        private readonly GameplayComponentWorld _componentWorld;
        private readonly CombatPhysicsWorld _physicsWorld;
        private readonly CombatGameplayAuraBuffDefinitionRegistry _definitions;
        private readonly ICombatGameplayAuraAnchorProvider _anchorProvider;
        private readonly GameplayComponentTargetingService _targetingService;
        private readonly GameplayBuffRequestBuffer _buffRequests;
        private readonly List<GameplayComponentSnapshot<GameplayComponentAuraSourceSetComponent>> _sourceSnapshot =
            new List<GameplayComponentSnapshot<GameplayComponentAuraSourceSetComponent>>();
        private readonly List<CombatQueryResult> _queryResults = new List<CombatQueryResult>();
        private readonly List<GameplayComponentTargetCandidate> _targetCandidates = new List<GameplayComponentTargetCandidate>();
        private readonly HashSet<GameplayEntityId> _seenTargetIds = new HashSet<GameplayEntityId>();

        public CombatGameplayAuraBuffSystem(
            CombatEntityGameplayMap entityMap,
            GameplayComponentWorld componentWorld,
            CombatPhysicsWorld physicsWorld,
            CombatGameplayAuraBuffDefinitionRegistry definitions,
            ICombatGameplayAuraAnchorProvider anchorProvider,
            GameplayBuffRequestBuffer buffRequests,
            GameplayComponentTargetingService targetingService = null,
            string systemId = DefaultSystemId,
            GameplaySystemPhase phase = GameplaySystemPhase.Resolution,
            int priority = 45)
        {
            _entityMap = entityMap ?? throw new ArgumentNullException(nameof(entityMap));
            _componentWorld = componentWorld ?? throw new ArgumentNullException(nameof(componentWorld));
            _physicsWorld = physicsWorld ?? throw new ArgumentNullException(nameof(physicsWorld));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _anchorProvider = anchorProvider ?? throw new ArgumentNullException(nameof(anchorProvider));
            _buffRequests = buffRequests ?? throw new ArgumentNullException(nameof(buffRequests));
            _targetingService = targetingService ?? new GameplayComponentTargetingService();
            SystemId = systemId ?? string.Empty;
            Phase = phase;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase { get; }
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;
        public CombatGameplayAuraBuffTickSummary LastTickSummary { get; private set; }

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <remarks>NoAlloc after internal buffers have reached steady-state capacity.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            if (!IsEnabled)
                return;

            GameplayComponentWorld componentWorld = context.ComponentWorld ?? _componentWorld;
            if (!componentWorld.TryGetStore(out GameplayComponentStore<GameplayComponentAuraSourceSetComponent> sourceStore))
            {
                LastTickSummary = default;
                return;
            }

            var summary = new MutableSummary();
            _sourceSnapshot.Clear();
            sourceStore.CopyTo(_sourceSnapshot);

            for (int entityIndex = 0; entityIndex < _sourceSnapshot.Count; entityIndex++)
            {
                GameplayEntityId sourceEntityId = _sourceSnapshot[entityIndex].EntityId;
                if (!componentWorld.IsAlive(sourceEntityId))
                    continue;

                GameplayComponentAuraSourceSetComponent sources = _sourceSnapshot[entityIndex].Component;
                for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
                    ProcessSource(context, componentWorld, sourceEntityId, sources.GetAt(sourceIndex), ref summary);
            }

            LastTickSummary = summary.ToSummary();
            _sourceSnapshot.Clear();
            _queryResults.Clear();
            _targetCandidates.Clear();
            _seenTargetIds.Clear();
        }

        private void ProcessSource(
            GameplaySystemContext context,
            GameplayComponentWorld componentWorld,
            GameplayEntityId sourceEntityId,
            GameplayComponentAuraSourceEntry source,
            ref MutableSummary summary)
        {
            summary.SourceCount++;
            if (source.IsExpired(context.Frame))
                return;

            if (!_definitions.TryGet(source.AuraId, out CombatGameplayAuraBuffDefinition definition))
            {
                summary.SkippedMissingDefinitionCount++;
                return;
            }

            if (!_entityMap.TryGetCombatId(sourceEntityId, out CombatEntityId sourceCombatId))
            {
                summary.SkippedMissingCombatMapCount++;
                return;
            }

            if (!_anchorProvider.TryGetAuraCenter(sourceCombatId, out FixVector3 center))
            {
                summary.SkippedMissingAnchorCount++;
                return;
            }

            _queryResults.Clear();
            CombatSphereQuery query = BuildQuery(definition, sourceCombatId, center, summary.SourceCount);
            _physicsWorld.QuerySphere(query, _queryResults, includeSourceEntity: definition.IncludeCaster);
            summary.QueryCount++;

            GameplayComponentTargetingResult targetingResult = SelectTargets(
                componentWorld,
                sourceEntityId,
                definition,
                ref summary);
            summary.RejectedTargetCount += targetingResult.RejectedCount;

            IReadOnlyList<GameplayComponentTargetCandidate> selectedTargets = targetingResult.SelectedTargets;
            for (int i = 0; i < selectedTargets.Count; i++)
            {
                EnqueueBuffRequest(selectedTargets[i].EntityId, definition);
                summary.AppliedOrRefreshedCount++;
            }
        }

        private GameplayComponentTargetingResult SelectTargets(
            GameplayComponentWorld componentWorld,
            GameplayEntityId sourceEntityId,
            CombatGameplayAuraBuffDefinition definition,
            ref MutableSummary summary)
        {
            _targetCandidates.Clear();
            _seenTargetIds.Clear();

            for (int i = 0; i < _queryResults.Count; i++)
            {
                CombatQueryResult result = _queryResults[i];
                if (!_entityMap.TryGetGameplayId(result.TargetEntityId, out GameplayEntityId targetEntityId))
                {
                    summary.SkippedMissingGameplayTargetCount++;
                    continue;
                }

                if (!_seenTargetIds.Add(targetEntityId))
                    continue;

                if (!GameplayComponentTargetCandidates.TryCreateFromWorld(componentWorld, targetEntityId, out GameplayComponentTargetCandidate candidate))
                {
                    summary.SkippedMissingGameplayTargetCount++;
                    continue;
                }

                _targetCandidates.Add(candidate);
            }

            int casterTeamId = GetTeamId(componentWorld, sourceEntityId);
            var query = new GameplayComponentTargetQuery(
                sourceEntityId,
                casterTeamId,
                requireAlive: true,
                relationFilter: definition.RelationFilter);
            return _targetingService.Select(query, _targetCandidates);
        }

        private static CombatSphereQuery BuildQuery(
            CombatGameplayAuraBuffDefinition definition,
            CombatEntityId sourceCombatId,
            FixVector3 center,
            int sourceOrder)
        {
            var header = new CombatQueryHeader(
                queryId: definition.AuraId,
                kind: CombatQueryKind.Sphere,
                sourceEntityId: sourceCombatId,
                traceId: 0,
                actionId: definition.AuraId,
                sourceOrder: sourceOrder,
                layerMask: definition.LayerMask);
            return new CombatSphereQuery(header, center, definition.Radius);
        }

        private void EnqueueBuffRequest(
            GameplayEntityId targetEntityId,
            CombatGameplayAuraBuffDefinition definition)
        {
            _buffRequests.EnqueueApply(
                targetEntityId,
                definition.BuffId,
                sourceId: definition.AuraId,
                traceId: default,
                commandId: 0);
        }

        private static int GetTeamId(GameplayComponentWorld componentWorld, GameplayEntityId entityId)
        {
            if (componentWorld.TryGetStore(out GameplayComponentStore<GameplayTeamComponent> teams) &&
                teams.TryGet(entityId, out GameplayTeamComponent team))
            {
                return team.TeamId;
            }

            return 0;
        }

        private struct MutableSummary
        {
            public int SourceCount;
            public int QueryCount;
            public int AppliedOrRefreshedCount;
            public int RejectedTargetCount;
            public int SkippedMissingDefinitionCount;
            public int SkippedMissingCombatMapCount;
            public int SkippedMissingAnchorCount;
            public int SkippedMissingGameplayTargetCount;

            public CombatGameplayAuraBuffTickSummary ToSummary()
            {
                return new CombatGameplayAuraBuffTickSummary(
                    SourceCount,
                    QueryCount,
                    AppliedOrRefreshedCount,
                    RejectedTargetCount,
                    SkippedMissingDefinitionCount,
                    SkippedMissingCombatMapCount,
                    SkippedMissingAnchorCount,
                    SkippedMissingGameplayTargetCount);
            }
        }
    }
}


