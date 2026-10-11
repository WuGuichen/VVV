using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers.CombatBridge
{
    /// <summary>
    /// Samples fixed world-space Combat queries and submits complete Trigger occupancy observations.
    /// </summary>
    public sealed class GameplayTriggerCombatSensorSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.triggers.combat_sensor";
        public const int DefaultPriority = 950;

        private readonly GameplayTriggerCombatSensorCatalog _catalog;
        private readonly GameplayTriggerRuntime _triggerRuntime;
        private readonly CombatPhysicsWorld _physicsWorld;
        private readonly ICombatEntityGameplayMapReader _entityMap;
        private readonly GameplayComponentWorld _componentWorld;
        private readonly List<CombatQueryResult> _queryResults;
        private readonly CombatEntityId[] _combatIds;
        private readonly GameplayEntityId[] _mappedGameplayIds;
        private readonly GameplayEntityId[] _subjects;
        private readonly GameplayComponentStore<GameplayTargetClassificationComponent>
            _classificationStore;
        private readonly GameplayComponentStore<GameplayTeamComponent> _teamStore;
        private readonly GameplayComponentStore<GameplayLifecycleComponent>
            _lifecycleStore;
        private readonly GameplayTriggerCombatSensorBindingTickResult[] _bindingResults;
        private RuntimeFrame _lastTickFrame;
        private bool _hasTicked;

        private GameplayTriggerCombatSensorSystem(
            GameplayTriggerCombatSensorCatalog catalog,
            GameplayTriggerRuntime triggerRuntime,
            CombatPhysicsWorld physicsWorld,
            ICombatEntityGameplayMapReader entityMap,
            GameplayComponentWorld componentWorld,
            string systemId,
            int priority)
        {
            _catalog = catalog;
            _triggerRuntime = triggerRuntime;
            _physicsWorld = physicsWorld;
            _entityMap = entityMap;
            _componentWorld = componentWorld;
            _queryResults = new List<CombatQueryResult>(catalog.MaxQueryHits);
            _combatIds = new CombatEntityId[catalog.MaxQueryHits];
            _mappedGameplayIds =
                new GameplayEntityId[catalog.MaxQueryHits];
            _subjects = new GameplayEntityId[catalog.MaxMappedSubjects];
            _classificationStore = componentWorld
                .GetOrCreateStore<GameplayTargetClassificationComponent>();
            _teamStore =
                componentWorld.GetOrCreateStore<GameplayTeamComponent>();
            _lifecycleStore =
                componentWorld.GetOrCreateStore<GameplayLifecycleComponent>();
            _bindingResults = new GameplayTriggerCombatSensorBindingTickResult[catalog.Count];
            SystemId = systemId;
            Priority = priority;
        }

        /// <remarks>NoAlloc.</remarks>
        public string SystemId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplaySystemPhase Phase => GameplaySystemPhase.Simulation;
        /// <remarks>NoAlloc.</remarks>
        public int Priority { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool IsEnabled { get; private set; } = true;
        /// <remarks>NoAlloc.</remarks>
        public int BindingResultCount => _bindingResults.Length;
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCombatSensorTickSummary LastTickSummary { get; private set; }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCombatSensorBindingTickResult GetLastBindingResult(int index) =>
            _bindingResults[index];

        /// <remarks>AllocByDesign. Allocates all fixed scratch required by steady-state Tick.</remarks>
        public static bool TryCreate(
            GameplayTriggerCombatSensorCatalog catalog,
            GameplayTriggerRuntime triggerRuntime,
            CombatPhysicsWorld physicsWorld,
            ICombatEntityGameplayMapReader entityMap,
            GameplayComponentWorld componentWorld,
            string systemId,
            int priority,
            out GameplayTriggerCombatSensorSystem system,
            out GameplayTriggerCombatSensorInitializationResult result)
        {
            system = null;
            if (catalog == null || triggerRuntime == null || physicsWorld == null ||
                entityMap == null || componentWorld == null)
            {
                result = Failure(GameplayTriggerCombatSensorInitializationErrorCode.InvalidArgument);
                return false;
            }
            if (string.IsNullOrWhiteSpace(systemId))
            {
                result = Failure(GameplayTriggerCombatSensorInitializationErrorCode.InvalidSystemId);
                return false;
            }
            if (priority >= GameplayTriggerEvaluationSystem.DefaultPriority)
            {
                result = Failure(
                    GameplayTriggerCombatSensorInitializationErrorCode.InvalidPriority,
                    expected: GameplayTriggerEvaluationSystem.DefaultPriority - 1L,
                    actual: priority);
                return false;
            }
            if (triggerRuntime.GameplayTriggerCatalogHash != catalog.GameplayTriggerCatalogHash)
            {
                result = Failure(
                    GameplayTriggerCombatSensorInitializationErrorCode.RuntimeCatalogMismatch,
                    expected: catalog.GameplayTriggerCatalogHash,
                    actual: triggerRuntime.GameplayTriggerCatalogHash);
                return false;
            }
            for (int i = 0; i < catalog.Count; i++)
            {
                GameplayTriggerCombatSensorDescriptor descriptor = catalog.GetDescriptor(i);
                if (!triggerRuntime.TryGetSourceEnabled(descriptor.SourceBindingId, out bool _))
                {
                    result = Failure(
                        GameplayTriggerCombatSensorInitializationErrorCode.UnknownSourceBinding,
                        i,
                        expected: descriptor.SourceBindingId.Value);
                    return false;
                }
            }

            system = new GameplayTriggerCombatSensorSystem(
                catalog,
                triggerRuntime,
                physicsWorld,
                entityMap,
                componentWorld,
                systemId,
                priority);
            result = GameplayTriggerCombatSensorInitializationResult.Succeeded;
            return true;
        }

        /// <remarks>AllocByDesign. Allocates all fixed scratch required by steady-state Tick.</remarks>
        public static bool TryCreate(
            GameplayTriggerCombatSensorCatalog catalog,
            GameplayTriggerRuntime triggerRuntime,
            CombatPhysicsWorld physicsWorld,
            ICombatEntityGameplayMapReader entityMap,
            GameplayComponentWorld componentWorld,
            out GameplayTriggerCombatSensorSystem system,
            out GameplayTriggerCombatSensorInitializationResult result) =>
            TryCreate(
                catalog,
                triggerRuntime,
                physicsWorld,
                entityMap,
                componentWorld,
                DefaultSystemId,
                DefaultPriority,
                out system,
                out result);

        /// <remarks>NoAlloc.</remarks>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <summary>Clears non-authoritative scheduling diagnostics at an exclusive bootstrap/restore boundary.</summary>
        /// <remarks>
        /// NoAlloc. Composition must call this after restoring Trigger authority before the next legal RuntimeFrame.
        /// Do not call concurrently with Tick.
        /// </remarks>
        public void ResetSchedulingState()
        {
            _hasTicked = false;
            _lastTickFrame = default;
            FillAll(GameplayTriggerCombatSensorTickStatus.None);
            LastTickSummary = default;
        }

        /// <remarks>
        /// NoAlloc after initialization and Combat broadphase warm-up. The fixed MaxQueryHits bound is
        /// checked against CombatPhysicsWorld.ColliderCount before the unbounded List-based Query API.
        /// </remarks>
        public void Tick(GameplaySystemContext context)
        {
            if (_hasTicked && context.Frame <= _lastTickFrame)
            {
                FillAll(GameplayTriggerCombatSensorTickStatus.DuplicateOrRegressedFrame);
                LastTickSummary = CreateUniformSummary(failed: true);
                return;
            }

            _lastTickFrame = context.Frame;
            _hasTicked = true;
            if (!IsEnabled)
            {
                FillAll(GameplayTriggerCombatSensorTickStatus.Disabled);
                LastTickSummary = CreateUniformSummary(failed: false);
                return;
            }

            if (!ReferenceEquals(context.ComponentWorld, _componentWorld))
            {
                FillAll(GameplayTriggerCombatSensorTickStatus.ContextMismatch);
                LastTickSummary = CreateUniformSummary(failed: true);
                return;
            }

            var summary = new MutableSummary(_catalog.Count);
            for (int descriptorIndex = 0; descriptorIndex < _catalog.Count; descriptorIndex++)
            {
                ProcessDescriptor(context.Frame, descriptorIndex, ref summary);
            }

            LastTickSummary = summary.ToImmutable();
        }

        private void ProcessDescriptor(
            RuntimeFrame frame,
            int descriptorIndex,
            ref MutableSummary summary)
        {
            GameplayTriggerCombatSensorDescriptor descriptor = _catalog.GetDescriptor(descriptorIndex);
            if (!_triggerRuntime.TryGetSourceEnabled(descriptor.SourceBindingId, out bool sourceEnabled))
            {
                _bindingResults[descriptorIndex] = CreateResult(
                    descriptor.SourceBindingId,
                    GameplayTriggerCombatSensorTickStatus.TriggerSubmitRejected,
                    GameplayTriggerErrorCode.UnknownSourceBinding);
                summary.FailedBindingCount++;
                return;
            }
            if (!sourceEnabled)
            {
                _bindingResults[descriptorIndex] = CreateResult(
                    descriptor.SourceBindingId,
                    GameplayTriggerCombatSensorTickStatus.Disabled);
                return;
            }
            if (_physicsWorld.ColliderCount > descriptor.MaxQueryHits)
            {
                _bindingResults[descriptorIndex] = CreateResult(
                    descriptor.SourceBindingId,
                    GameplayTriggerCombatSensorTickStatus.QueryCapacityExceeded);
                summary.FailedBindingCount++;
                return;
            }

            _queryResults.Clear();
            _physicsWorld.Query(descriptor.Query, _queryResults);
            summary.QueryCount++;
            int combatIdCount = 0;
            int subjectCount = 0;
            int mappedGameplayIdCount = 0;
            int duplicateCombatHits = 0;
            int duplicateGameplaySubjects = 0;
            int unmappedCombatEntities = 0;
            int staleGameplayEntities = 0;
            int filteredSubjects = 0;
            GameplayTargetRejectReason firstFilterRejectReason =
                GameplayTargetRejectReason.None;
            int firstFilterRejectDetailId = 0;
            GameplayTriggerCombatSensorTickStatus failure = GameplayTriggerCombatSensorTickStatus.None;

            for (int hitIndex = 0; hitIndex < _queryResults.Count; hitIndex++)
            {
                CombatEntityId combatId = _queryResults[hitIndex].TargetEntityId;
                if (ContainsCombatId(combatIdCount, combatId))
                {
                    duplicateCombatHits++;
                    continue;
                }
                _combatIds[combatIdCount++] = combatId;

                if (!_entityMap.TryGetGameplayId(combatId, out GameplayEntityId gameplayId))
                {
                    unmappedCombatEntities++;
                    continue;
                }
                if (!gameplayId.IsValid)
                {
                    failure = GameplayTriggerCombatSensorTickStatus.InvalidMappedGameplayEntity;
                    break;
                }
                if (!_componentWorld.IsAlive(gameplayId))
                {
                    staleGameplayEntities++;
                    continue;
                }
                if (ContainsGameplayId(
                        _mappedGameplayIds,
                        mappedGameplayIdCount,
                        gameplayId))
                {
                    duplicateGameplaySubjects++;
                    continue;
                }
                _mappedGameplayIds[mappedGameplayIdCount++] = gameplayId;
                if (!TryAcceptSubject(
                        descriptor.SubjectFilter,
                        gameplayId,
                        out GameplayTargetRejectReason filterRejectReason,
                        out int filterRejectDetailId))
                {
                    filteredSubjects++;
                    if (firstFilterRejectReason
                        == GameplayTargetRejectReason.None)
                    {
                        firstFilterRejectReason = filterRejectReason;
                        firstFilterRejectDetailId = filterRejectDetailId;
                    }

                    continue;
                }
                if (subjectCount >= descriptor.MaxMappedSubjects)
                {
                    failure = GameplayTriggerCombatSensorTickStatus.MappedSubjectCapacityExceeded;
                    break;
                }
                _subjects[subjectCount++] = gameplayId;
            }

            summary.DuplicateCombatHitCount += duplicateCombatHits;
            summary.DuplicateGameplaySubjectCount += duplicateGameplaySubjects;
            summary.UnmappedCombatEntityCount += unmappedCombatEntities;
            summary.StaleGameplayEntityCount += staleGameplayEntities;
            summary.FilteredSubjectCount += filteredSubjects;
            if (failure != GameplayTriggerCombatSensorTickStatus.None)
            {
                _bindingResults[descriptorIndex] = CreateResult(
                    descriptor.SourceBindingId,
                    failure,
                    GameplayTriggerErrorCode.None,
                    _queryResults.Count,
                    0,
                    duplicateCombatHits,
                    duplicateGameplaySubjects,
                    unmappedCombatEntities,
                    staleGameplayEntities,
                    filteredSubjects,
                    firstFilterRejectReason,
                    firstFilterRejectDetailId);
                summary.FailedBindingCount++;
                return;
            }

            SortSubjects(subjectCount);
            var subjects = new ReadOnlySpan<GameplayEntityId>(_subjects, 0, subjectCount);
            if (!_triggerRuntime.TrySubmitOccupancy(
                    frame,
                    descriptor.SourceBindingId,
                    subjects,
                    out GameplayTriggerResult triggerResult,
                    isComplete: true))
            {
                _bindingResults[descriptorIndex] = CreateResult(
                    descriptor.SourceBindingId,
                    GameplayTriggerCombatSensorTickStatus.TriggerSubmitRejected,
                    triggerResult.ErrorCode,
                    _queryResults.Count,
                    0,
                    duplicateCombatHits,
                    duplicateGameplaySubjects,
                    unmappedCombatEntities,
                    staleGameplayEntities,
                    filteredSubjects,
                    firstFilterRejectReason,
                    firstFilterRejectDetailId);
                summary.FailedBindingCount++;
                return;
            }

            _bindingResults[descriptorIndex] = CreateResult(
                descriptor.SourceBindingId,
                GameplayTriggerCombatSensorTickStatus.Submitted,
                GameplayTriggerErrorCode.None,
                _queryResults.Count,
                subjectCount,
                duplicateCombatHits,
                duplicateGameplaySubjects,
                unmappedCombatEntities,
                staleGameplayEntities,
                filteredSubjects,
                firstFilterRejectReason,
                firstFilterRejectDetailId);
            summary.SubmittedBatchCount++;
            summary.SubmittedSubjectCount += subjectCount;
        }

        private bool ContainsCombatId(int count, CombatEntityId combatId)
        {
            for (int i = 0; i < count; i++)
            {
                if (_combatIds[i].Equals(combatId))
                    return true;
            }
            return false;
        }

        private static bool ContainsGameplayId(
            GameplayEntityId[] ids,
            int count,
            GameplayEntityId gameplayId)
        {
            for (int i = 0; i < count; i++)
            {
                if (ids[i].Equals(gameplayId))
                    return true;
            }
            return false;
        }

        private bool TryAcceptSubject(
            in GameplayComponentTargetFilter filter,
            GameplayEntityId gameplayId,
            out GameplayTargetRejectReason rejectionReason,
            out int rejectionDetailId)
        {
            GameplayTargetClassificationComponent classification = default;
            _classificationStore?.TryGet(gameplayId, out classification);
            GameplayTeamComponent team = default;
            _teamStore?.TryGet(gameplayId, out team);
            GameplayLifecycleComponent lifecycle = default;
            _lifecycleStore?.TryGet(gameplayId, out lifecycle);
            return GameplayComponentTargetFilterEvaluator.TryAccept(
                in filter,
                gameplayId,
                team.TeamId,
                lifecycle.State,
                in classification,
                out rejectionReason,
                out rejectionDetailId);
        }

        private void SortSubjects(int count)
        {
            for (int i = 1; i < count; i++)
            {
                GameplayEntityId value = _subjects[i];
                int destination = i;
                while (destination > 0 && _subjects[destination - 1].CompareTo(value) > 0)
                {
                    _subjects[destination] = _subjects[destination - 1];
                    destination--;
                }
                _subjects[destination] = value;
            }
        }

        private void FillAll(GameplayTriggerCombatSensorTickStatus status)
        {
            for (int i = 0; i < _bindingResults.Length; i++)
            {
                _bindingResults[i] = CreateResult(_catalog.GetDescriptor(i).SourceBindingId, status);
            }
        }

        private GameplayTriggerCombatSensorTickSummary CreateUniformSummary(bool failed) =>
            new GameplayTriggerCombatSensorTickSummary(
                _catalog.Count,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                failed ? _catalog.Count : 0);

        private static GameplayTriggerCombatSensorBindingTickResult CreateResult(
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerCombatSensorTickStatus status,
            GameplayTriggerErrorCode triggerErrorCode = GameplayTriggerErrorCode.None,
            int queryHitCount = 0,
            int submittedSubjectCount = 0,
            int duplicateCombatHitCount = 0,
            int duplicateGameplaySubjectCount = 0,
            int unmappedCombatEntityCount = 0,
            int staleGameplayEntityCount = 0,
            int filteredSubjectCount = 0,
            GameplayTargetRejectReason firstFilterRejectReason =
                GameplayTargetRejectReason.None,
            int firstFilterRejectDetailId = 0) =>
            new GameplayTriggerCombatSensorBindingTickResult(
                sourceBindingId,
                status,
                triggerErrorCode,
                queryHitCount,
                submittedSubjectCount,
                duplicateCombatHitCount,
                duplicateGameplaySubjectCount,
                unmappedCombatEntityCount,
                staleGameplayEntityCount,
                filteredSubjectCount,
                firstFilterRejectReason,
                firstFilterRejectDetailId);

        private static GameplayTriggerCombatSensorInitializationResult Failure(
            GameplayTriggerCombatSensorInitializationErrorCode errorCode,
            int descriptorIndex = -1,
            long expected = 0L,
            long actual = 0L) =>
            new GameplayTriggerCombatSensorInitializationResult(
                false,
                errorCode,
                descriptorIndex,
                expected,
                actual);

        private struct MutableSummary
        {
            public MutableSummary(int bindingCount)
            {
                BindingCount = bindingCount;
                QueryCount = 0;
                SubmittedBatchCount = 0;
                SubmittedSubjectCount = 0;
                DuplicateCombatHitCount = 0;
                DuplicateGameplaySubjectCount = 0;
                UnmappedCombatEntityCount = 0;
                StaleGameplayEntityCount = 0;
                FilteredSubjectCount = 0;
                FailedBindingCount = 0;
            }

            public int BindingCount;
            public int QueryCount;
            public int SubmittedBatchCount;
            public int SubmittedSubjectCount;
            public int DuplicateCombatHitCount;
            public int DuplicateGameplaySubjectCount;
            public int UnmappedCombatEntityCount;
            public int StaleGameplayEntityCount;
            public int FilteredSubjectCount;
            public int FailedBindingCount;

            public GameplayTriggerCombatSensorTickSummary ToImmutable() =>
                new GameplayTriggerCombatSensorTickSummary(
                    BindingCount,
                    QueryCount,
                    SubmittedBatchCount,
                    SubmittedSubjectCount,
                    DuplicateCombatHitCount,
                    DuplicateGameplaySubjectCount,
                    UnmappedCombatEntityCount,
                    StaleGameplayEntityCount,
                    FilteredSubjectCount,
                    FailedBindingCount);
        }
    }
}
