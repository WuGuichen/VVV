using System;
using System.Globalization;
using MxFramework.Config.Levels;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.CombatBridge;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.GameplayBridge;
using MxFramework.Gameplay.Triggers.StoryBridge;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// CharacterTest-owned composition seam that binds level-authored Trigger data to
    /// existing Runtime, Combat Physics, Gameplay, Buff, Story and SaveState contracts.
    /// </summary>
    internal sealed class CharacterTestLevelGameplayTriggerRuntimeComposition
    {
        internal const string GameplayModuleId =
            "CharacterTest.LevelGameplayTriggers";
        internal const int GameplayModulePriority = 110;
        internal const int RuntimeEventCapacity = 256;

        private readonly GameplayBuffRequestBuffer _buffRequests;
        private readonly GameplayTriggerEventRouterSystem _eventRouter;
        private readonly RuntimeHashCombiner _hashCombiner;
        private readonly RuntimeSaveStateCoordinator _saveCoordinator;
        private readonly RuntimeCheckpointBarrier _checkpointBarrier;

        private CharacterTestLevelGameplayTriggerRuntimeComposition(
            GameplayTriggerCompiledCatalogPlan plan,
            GameplayTriggerRuntime runtime,
            GameplayTriggerCombatSensorCatalog sensorCatalog,
            GameplayTriggerCombatSensorSystem sensor,
            CharacterTestGameplayTriggerAttributeThresholdCatalog
                attributeThresholdCatalog,
            CharacterTestGameplayTriggerAttributeThresholdSystem
                attributeThreshold,
            GameplayRuntimeModule gameplayModule,
            GameplayTriggerReliableOutboxModule outbox,
            GameplayBuffRequestBuffer buffRequests,
            GameplayTriggerEventRouterSystem eventRouter,
            RuntimeHashCombiner hashCombiner,
            RuntimeSaveStateCoordinator saveCoordinator,
            RuntimeCheckpointBarrier checkpointBarrier,
            CharacterTestLevelGameplayTriggerRuntimeStatus status)
        {
            Plan = plan;
            Runtime = runtime;
            SensorCatalog = sensorCatalog;
            Sensor = sensor;
            AttributeThresholdCatalog = attributeThresholdCatalog;
            AttributeThreshold = attributeThreshold;
            GameplayModule = gameplayModule;
            ReliableOutbox = outbox;
            _buffRequests = buffRequests;
            _eventRouter = eventRouter;
            _hashCombiner = hashCombiner;
            _saveCoordinator = saveCoordinator;
            _checkpointBarrier = checkpointBarrier;
            Status = status;
        }

        internal GameplayTriggerCompiledCatalogPlan Plan { get; }
        internal GameplayTriggerRuntime Runtime { get; }
        internal GameplayTriggerCombatSensorCatalog SensorCatalog { get; }
        internal GameplayTriggerCombatSensorSystem Sensor { get; }
        internal CharacterTestGameplayTriggerAttributeThresholdCatalog
            AttributeThresholdCatalog { get; }
        internal CharacterTestGameplayTriggerAttributeThresholdSystem
            AttributeThreshold { get; }
        internal GameplayRuntimeModule GameplayModule { get; }
        internal GameplayTriggerReliableOutboxModule ReliableOutbox { get; }
        internal CharacterTestLevelGameplayTriggerRuntimeStatus Status { get; }

        /// <summary>
        /// Rebuilds and verifies all immutable runtime objects from the scene-owned cache.
        /// </summary>
        /// <remarks>
        /// AllocByDesign. This method runs once at session composition and never from Tick.
        /// A rejected cache returns a typed failed-closed status and no partial composition.
        /// </remarks>
        internal static bool TryCreate(
            CharacterTestGameplayTriggerSceneCache cache,
            RuntimeHost host,
            StoryRuntimeModule storyModule,
            CharacterTestCharacterRuntimeModule characterRuntime,
            GameplayBuffDefinitionRegistry buffDefinitions,
            GameplayTriggerRuntimeEventSource eventSource,
            out CharacterTestLevelGameplayTriggerRuntimeComposition composition,
            out CharacterTestLevelGameplayTriggerRuntimeStatus status)
        {
            composition = null;
            if (cache == null || cache.TriggerEntryCount == 0)
            {
                status =
                    CharacterTestLevelGameplayTriggerRuntimeStatus.Disabled(cache);
                return true;
            }

            bool legacyRegionOnlyCache =
                cache.CacheVersion
                    == CharacterTestGameplayTriggerSceneCache
                        .LegacyRegionOnlyCacheVersion
                && cache.AttributeThresholdRowCount == 0
                && cache.AttributeThresholdCatalogHash == 0L;
            if ((!legacyRegionOnlyCache
                    && cache.CacheVersion
                        != CharacterTestGameplayTriggerSceneCache
                            .CurrentCacheVersion)
                || !string.Equals(
                    cache.LevelFormat,
                    MxLevelAuthoringDocument.CurrentFormat,
                    StringComparison.Ordinal)
                || cache.LevelSchemaVersion
                    != MxLevelAuthoringDocument.CurrentSchemaVersion
                || cache.ModuleSchemaVersion
                    != CharacterTestLevelGameplayTriggerAuthoringAdapter
                        .ModuleSchemaVersion)
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .CacheSchemaMismatch,
                    "Trigger scene cache schema is not supported.");
                return false;
            }
            if (host == null || storyModule == null || characterRuntime == null)
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .CompositionRejected,
                    "CharacterTest runtime composition is incomplete.");
                return false;
            }
            if (cache.SensorRowCount > 0
                && characterRuntime.PhysicsRuntime?.World == null)
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .CombatPhysicsUnavailable,
                    "CharacterTest Combat Physics world is unavailable.");
                return false;
            }
            if (buffDefinitions == null || eventSource == null)
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .CompositionRejected,
                    "Trigger action or diagnostics services are unavailable.");
                return false;
            }

            MxLevelAuthoringDocument envelope;
            try
            {
                envelope = MxLevelAuthoringJson.LoadFromJson(
                    cache.CanonicalTriggerEnvelopeJson);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                      || exception is InvalidOperationException
                      || exception is Newtonsoft.Json.JsonException)
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .CachePayloadInvalid,
                    "Trigger scene cache payload is invalid: "
                    + exception.Message);
                return false;
            }

            string rebuiltSourceHash =
                MxLevelAuthoringJson.ComputeContentHash(envelope);
            if (string.IsNullOrWhiteSpace(cache.TriggerSourceHash)
                || !string.Equals(
                    cache.TriggerSourceHash,
                    rebuiltSourceHash,
                    StringComparison.Ordinal))
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .CacheSourceHashMismatch,
                    "Trigger scene cache source hash does not match its payload.");
                return false;
            }

            if (!CharacterTestLevelGameplayTriggerAuthoringAdapter.TryExtract(
                    envelope,
                    out CharacterTestLevelGameplayTriggerAuthoringDocument
                        triggerDocument,
                    out bool modulePresent,
                    out LevelWorldObjectAuthoringValidationReport extractReport)
                || !modulePresent
                || triggerDocument?.Entries?.Count != cache.TriggerEntryCount)
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .CachePayloadInvalid,
                    FirstIssue(
                        extractReport,
                        "Trigger scene cache payload does not match its entry count."));
                return false;
            }

            GameplayTriggerCompileResult compile =
                CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .CompileRuntimeCache(
                        triggerDocument,
                        cache.TriggerCatalogHash,
                        out LevelWorldObjectAuthoringValidationReport
                            compileReport);
            if (compile?.Succeeded != true
                || compile.CompiledCatalog == null)
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .CompileRejected,
                    FirstIssue(
                        compileReport,
                        "Gameplay Trigger runtime compilation was rejected."));
                return false;
            }

            GameplayTriggerCompiledCatalogPlan plan =
                compile.CompiledCatalog;
            GameplayTriggerCombatSensorDescriptor[] descriptors;
            try
            {
                descriptors =
                    CharacterTestGameplayTriggerSceneCacheCompiler
                        .CreateDescriptors(cache.SensorRows);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                      || exception is InvalidOperationException)
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .SensorCatalogRejected,
                    "Trigger Combat sensor cache is invalid: "
                    + exception.Message);
                return false;
            }

            GameplayTriggerCombatSensorCatalog sensorCatalog = null;
            long sensorCatalogHash = 0L;
            if (descriptors.Length > 0)
            {
                if (!GameplayTriggerCombatSensorCatalog.TryCreate(
                        descriptors,
                        plan,
                        out sensorCatalog,
                        out GameplayTriggerCombatSensorCatalogResult
                            sensorResult))
                {
                    status = Failed(
                        cache,
                        CharacterTestLevelGameplayTriggerStartupFailure
                            .SensorCatalogRejected,
                        "Trigger Combat sensor catalog was rejected: "
                        + sensorResult.ErrorCode);
                    return false;
                }
                sensorCatalogHash = sensorCatalog.SensorCatalogHash;
            }
            if (sensorCatalogHash != cache.SensorCatalogHash)
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .SensorCatalogHashMismatch,
                    "Trigger Combat sensor catalog hash does not match the scene cache.");
                return false;
            }

            CharacterTestGameplayTriggerAttributeThresholdDescriptor[]
                attributeDescriptors;
            try
            {
                attributeDescriptors =
                    CharacterTestGameplayTriggerSceneCacheCompiler
                        .CreateAttributeThresholdDescriptors(
                            cache.AttributeThresholdRows);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                      || exception is InvalidOperationException)
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .AttributeThresholdCatalogRejected,
                    "Trigger attribute Threshold cache is invalid: "
                    + exception.Message);
                return false;
            }

            CharacterTestGameplayTriggerAttributeThresholdCatalog
                attributeThresholdCatalog = null;
            long attributeThresholdCatalogHash = 0L;
            if (attributeDescriptors.Length > 0)
            {
                if (!CharacterTestGameplayTriggerAttributeThresholdCatalog
                        .TryCreate(
                            attributeDescriptors,
                            plan,
                            out attributeThresholdCatalog,
                            out string attributeError))
                {
                    status = Failed(
                        cache,
                        CharacterTestLevelGameplayTriggerStartupFailure
                            .AttributeThresholdCatalogRejected,
                        attributeError);
                    return false;
                }
                attributeThresholdCatalogHash =
                    attributeThresholdCatalog.CatalogHash;
            }
            if (attributeThresholdCatalogHash
                    != cache.AttributeThresholdCatalogHash
                || descriptors.Length + attributeDescriptors.Length
                    != plan.SourceRequirementCount)
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .AttributeThresholdCatalogHashMismatch,
                    "Trigger source provider rows do not match the compiled catalog.");
                return false;
            }
            long combinedCatalogHash =
                legacyRegionOnlyCache
                    ? sensorCatalog?.CombinedCatalogHash ?? 0L
                    : CharacterTestGameplayTriggerSourceCatalogHash.Compute(
                        plan.GameplayTriggerCatalogHash,
                        sensorCatalogHash,
                        attributeThresholdCatalogHash);
            if (combinedCatalogHash != cache.CombinedCatalogHash)
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .SourceCatalogHashMismatch,
                    "Trigger combined source catalog hash does not match the scene cache.");
                return false;
            }

            GameplayTriggerRuntimeCapacityRequirements capacities =
                plan.CapacityRequirements;
            var runtimeProfile = new GameplayTriggerRuntimeCapacityProfile(
                capacities.RequiredInstances,
                capacities.MaxInputBatches,
                capacities.MaxInputItems,
                capacities.MaxRelations,
                capacities.MaxOccurrences,
                capacities.MaxCandidates,
                capacities.MaxRouteStates,
                capacities.MaxActivations,
                capacities.MaxIntents,
                capacities.MaxPreparedActions,
                capacities.MaxConditionStackDepth,
                capacities.MaxDeferredRecords,
                capacities.MaxReliableOutboxRecords);
            // Buff reservations remain alive until the same-frame intent
            // batch is applied. Size them for all intents, not the
            // per-candidate action preparation scratch space.
            int buffCapacity = Math.Max(
                1,
                capacities.MaxIntents);
            var buffRequests = new GameplayBuffRequestBuffer(
                buffCapacity,
                buffCapacity);
            if (!TryCreateActionBootstrap(
                    plan,
                    buffRequests,
                    includeStory: true,
                    out GameplayTriggerActionBootstrapBinding[]
                        actionBindings,
                    out string actionMessage))
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .ActionBootstrapRejected,
                    actionMessage);
                return false;
            }

            GameplayTriggerCatalogBootstrapResult bootstrap =
                GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                    plan,
                    actionBindings,
                    runtimeProfile);
            if (!bootstrap.Succeeded
                || !bootstrap.RuntimeOptions.HasValue)
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .ActionBootstrapRejected,
                    "Gameplay Trigger action bootstrap was rejected"
                    + DiagnosticSuffix(bootstrap));
                return false;
            }

            var runtime = new GameplayTriggerRuntime(
                bootstrap.Catalog,
                bootstrap.RuntimeOptions.Value,
                eventSource);
            GameplayTriggerCombatSensorSystem sensor = null;
            if (sensorCatalog != null
                && !GameplayTriggerCombatSensorSystem.TryCreate(
                    sensorCatalog,
                    runtime,
                    characterRuntime.PhysicsRuntime.World,
                    characterRuntime.World.CombatGameplayMap,
                    characterRuntime.World.GameplayComponentWorld,
                    out sensor,
                    out GameplayTriggerCombatSensorInitializationResult
                        sensorInitialization))
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .SensorCompositionRejected,
                    "Trigger Combat sensor composition was rejected: "
                    + sensorInitialization.ErrorCode);
                return false;
            }
            CharacterTestGameplayTriggerAttributeThresholdSystem
                attributeThreshold = attributeThresholdCatalog == null
                    ? null
                    : new
                        CharacterTestGameplayTriggerAttributeThresholdSystem(
                            attributeThresholdCatalog,
                            runtime,
                            characterRuntime.World);

            if (!TryCreateReliableOutbox(
                    plan,
                    runtime,
                    host,
                    storyModule,
                    out GameplayTriggerReliableOutboxModule outbox,
                    out string outboxMessage))
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .ActionBootstrapRejected,
                    outboxMessage);
                return false;
            }

            var observer =
                new GameplayTriggerRuntimeEventObserverBuffer(
                    RuntimeEventCapacity);
            var router = new GameplayTriggerEventRouterSystem(
                eventSource,
                new[]
                {
                    new GameplayTriggerRuntimeEventObserverRegistration(
                        "character-test.level-editor.play-monitor",
                        observer)
                });
            status =
                CharacterTestLevelGameplayTriggerRuntimeStatus.Ready(
                    cache,
                    sensor,
                    attributeThreshold,
                    observer,
                    capacities.MaxActivations,
                    plan.GameplayTriggerCatalogHash,
                    sensorCatalogHash,
                    attributeThresholdCatalogHash,
                    combinedCatalogHash);
            GameplaySystemPipeline pipeline = CreatePipeline(
                runtime,
                sensor,
                attributeThreshold,
                buffDefinitions,
                buffRequests,
                eventSource,
                router,
                status,
                capacities);
            var gameplayCommands = RuntimeCommandBuffer.CreateBounded(
                validator: null,
                RuntimeFrame.Zero,
                pendingCapacityLimit:
                    Math.Max(8, capacities.MaxInputBatches));
            var gameplayModule = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                gameplayCommands,
                tickWorldAutomatically: false,
                moduleId: GameplayModuleId,
                priority: GameplayModulePriority,
                systemPipeline: pipeline,
                componentWorld:
                    characterRuntime.World.GameplayComponentWorld);

            RegisterGameplaySchemas(
                characterRuntime.World.GameplayComponentWorld.Schemas);
            RuntimeHashCombiner hashCombiner = CreateHashCombiner(
                characterRuntime.World.GameplayComponentWorld,
                runtime,
                storyModule.Director,
                cache.CombinedCatalogHash);
            GameplayTriggerRuntimeSaveStateParticipant triggerSave =
                new GameplayTriggerRuntimeSaveStateParticipant(
                    runtime,
                    new RestoredGameplayEntityValidator(
                        characterRuntime.World.GameplayComponentWorld));
            if (!TryCreateSaveState(
                    cache,
                    host,
                    storyModule.Director,
                    characterRuntime.World.GameplayComponentWorld,
                    triggerSave,
                    out RuntimeSaveStateCoordinator saveCoordinator,
                    out RuntimeCheckpointBarrier checkpointBarrier,
                    out string saveMessage))
            {
                status = Failed(
                    cache,
                    CharacterTestLevelGameplayTriggerStartupFailure
                        .CompositionRejected,
                    saveMessage);
                return false;
            }

            composition =
                new CharacterTestLevelGameplayTriggerRuntimeComposition(
                    plan,
                    runtime,
                    sensorCatalog,
                    sensor,
                    attributeThresholdCatalog,
                    attributeThreshold,
                    gameplayModule,
                    outbox,
                    buffRequests,
                    router,
                    hashCombiner,
                    saveCoordinator,
                    checkpointBarrier,
                    status);
            return true;
        }

        /// <remarks>
        /// AllocByDesign. Used at the Created lifecycle gate after exact module registration.
        /// </remarks>
        internal bool TryVerifyComposition(
            RuntimeHost host,
            out string message)
        {
            if (GameplayTriggerCompositionVerifier.TryVerify(
                    Plan,
                    host,
                    Runtime,
                    ReliableOutbox,
                    out GameplayTriggerCompositionVerificationResult result))
            {
                message = string.Empty;
                return true;
            }

            message =
                "Gameplay Trigger composition verification failed with "
                + result.DiagnosticCount
                + " diagnostic(s).";
            return false;
        }

        /// <remarks>LowFreqAlloc. Hash contributors copy canonical snapshots.</remarks>
        internal long ComputeResultHash(RuntimeFrame frame)
        {
            return _hashCombiner.ComputeHash(frame);
        }

        /// <remarks>AllocByDesign. Captures only after a complete, quiescent Host Tick.</remarks>
        internal RuntimeSaveStateCoordinatorResult<RuntimeSaveState>
            CaptureCheckpoint()
        {
            AuthorityAvailabilityGuard?.Invoke();
            return _checkpointBarrier.CaptureCheckpoint();
        }

        internal Action AuthorityAvailabilityGuard { private get; set; }

        /// <remarks>
        /// AllocByDesign. Successful restore clears derived transport and sensor scheduling state;
        /// the next legal Tick resamples Combat occupancy and therefore does not duplicate Entered.
        /// </remarks>
        internal RuntimeSaveStateCoordinatorResult<bool>
            RestoreCheckpoint(RuntimeSaveState saveState)
        {
            AuthorityAvailabilityGuard?.Invoke();
            RuntimeSaveStateCoordinatorResult<bool> result =
                _saveCoordinator.RestoreSaveState(saveState);
            if (!result.Success)
                return result;

            _buffRequests.Clear();
            _eventRouter.ResetPresentationState();
            Status.ResetAfterRestore();
            return result;
        }

        /// <remarks>
        /// AllocByDesign. Internal test seam for proving incomplete CharacterTest action mappings
        /// fail before a runtime catalog is created.
        /// </remarks>
        internal static bool TryCreateActionBootstrap(
            GameplayTriggerCompiledCatalogPlan plan,
            GameplayBuffRequestBuffer buffRequests,
            bool includeStory,
            out GameplayTriggerActionBootstrapBinding[] bindings,
            out string message)
        {
            if (plan == null || buffRequests == null)
            {
                bindings =
                    Array.Empty<GameplayTriggerActionBootstrapBinding>();
                message =
                    "Gameplay Trigger action bootstrap inputs are missing.";
                return false;
            }

            bindings =
                new GameplayTriggerActionBootstrapBinding[
                    plan.ActionRequirementCount];
            for (int i = 0; i < plan.ActionRequirementCount; i++)
            {
                GameplayTriggerCompiledActionRequirement requirement =
                    plan.GetActionRequirement(i);
                if (requirement.DeliveryContract
                    == GameplayTriggerDeliveryContract.InternalDeferred)
                {
                    bindings[i] =
                        GameplayTriggerActionBootstrapBinding.FromInternal(
                            new GameplayTriggerInternalDeferredActionDescriptor(
                                requirement.ActionKindId,
                                requirement.InternalOperationKind,
                                requirement.SignalId));
                    continue;
                }

                if (requirement.ActionKindId
                    == GameplayTriggerGameplayBuffActionKinds
                        .ApplyComponentBuff
                    || requirement.ActionKindId
                    == GameplayTriggerGameplayBuffActionKinds
                        .RemoveComponentBuff)
                {
                    bindings[i] =
                        GameplayTriggerActionBootstrapBinding.FromSink(
                            new GameplayTriggerGameplayBuffActionSink(
                                requirement.ActionKindId,
                                buffRequests));
                    continue;
                }

                if (includeStory
                    && requirement.ActionKindId
                    == GameplayTriggerStoryCommandActionKinds.RaiseTrigger)
                {
                    bindings[i] =
                        GameplayTriggerActionBootstrapBinding.FromSink(
                            new GameplayTriggerStoryReliableCommandActionSink(
                                requirement.ActionKindId));
                    continue;
                }

                bindings =
                    Array.Empty<GameplayTriggerActionBootstrapBinding>();
                message =
                    "CharacterTest has no action bootstrap for kind "
                    + requirement.ActionKindId.Value
                    + ".";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static GameplaySystemPipeline CreatePipeline(
            GameplayTriggerRuntime runtime,
            GameplayTriggerCombatSensorSystem sensor,
            CharacterTestGameplayTriggerAttributeThresholdSystem
                attributeThreshold,
            GameplayBuffDefinitionRegistry buffDefinitions,
            GameplayBuffRequestBuffer buffRequests,
            GameplayTriggerRuntimeEventSource eventSource,
            GameplayTriggerEventRouterSystem router,
            CharacterTestLevelGameplayTriggerRuntimeStatus status,
            GameplayTriggerRuntimeCapacityRequirements capacities)
        {
            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(new GameplayTriggerDeferredSystem(runtime));
            pipeline.Add(
                new GameplayBuffCommandSystem(
                    buffDefinitions,
                    buffRequests));
            pipeline.Add(
                new GameplayTriggerCommandSystem(
                    runtime,
                    GameplayTriggerCommandSystem.DefaultSystemId,
                    GameplayTriggerCommandSystem.DefaultPriority,
                    Math.Max(1, capacities.MaxInputItems),
                    Math.Max(1, capacities.MaxInputItems),
                    Math.Max(1, capacities.MaxInputItems)));
            if (sensor != null)
                pipeline.Add(sensor);
            if (attributeThreshold != null)
                pipeline.Add(attributeThreshold);
            pipeline.Add(new GameplayTriggerEvaluationSystem(runtime));
            pipeline.Add(new GameplayComponentBuffCleanupSystem());
            pipeline.Add(
                new GameplayBuffApplicationSystem(
                    buffDefinitions,
                    buffRequests,
                    resultSink:
                        new GameplayTriggerGameplayBuffResultRelay(
                            eventSource)));
            pipeline.Add(
                new GameplayBuffEffectProjectionSystem(
                    buffDefinitions));
            pipeline.Add(router);
            pipeline.Add(
                new CharacterTestLevelGameplayTriggerDiagnosticsSystem(
                    status));
            return pipeline;
        }

        private static bool TryCreateReliableOutbox(
            GameplayTriggerCompiledCatalogPlan plan,
            GameplayTriggerRuntime runtime,
            RuntimeHost host,
            StoryRuntimeModule storyModule,
            out GameplayTriggerReliableOutboxModule outbox,
            out string message)
        {
            int reliableCount = 0;
            for (int i = 0; i < plan.ActionRequirementCount; i++)
            {
                if (plan.GetActionRequirement(i).DeliveryContract
                    == GameplayTriggerDeliveryContract.ReliableDeferred)
                {
                    reliableCount++;
                }
            }
            if (reliableCount == 0)
            {
                outbox = null;
                message = string.Empty;
                return true;
            }
            if (reliableCount != 1
                || !HasActionRequirement(
                    plan,
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger))
            {
                outbox = null;
                message =
                    "CharacterTest reliable Trigger action mapping is incomplete.";
                return false;
            }

            var sources =
                new RuntimeCommandSourceRegistry(initialCapacity: 1);
            if (!sources.TryRegister(
                    StoryRuntimeCommandSources.GameplayBridge,
                    RuntimeCommandSourceKind.Derived,
                    new[] { StoryRuntimeCommandIds.RaiseTrigger },
                    out RuntimeCommandSourceRegistrationResult registration))
            {
                outbox = null;
                message =
                    "Story Trigger command source registration failed: "
                    + registration.ErrorCode;
                return false;
            }

            var gate = new RuntimeCommandSubmissionGate(
                sources,
                storyModule.CommandBuffer,
                RuntimeCommandIngressKind.TrustedDerived);
            var transfer =
                new GameplayTriggerStoryReliableCommandTransfer(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    StoryRuntimeCommandSources.GameplayBridge,
                    host,
                    storyModule,
                    gate);
            outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[]
                {
                    transfer
                });
            message = string.Empty;
            return true;
        }

        private static bool TryCreateSaveState(
            CharacterTestGameplayTriggerSceneCache cache,
            RuntimeHost host,
            StoryDirector storyDirector,
            GameplayComponentWorld componentWorld,
            GameplayTriggerRuntimeSaveStateParticipant triggerSave,
            out RuntimeSaveStateCoordinator coordinator,
            out RuntimeCheckpointBarrier checkpointBarrier,
            out string message)
        {
            coordinator = null;
            checkpointBarrier = null;
            Func<long> completedFrame = () =>
                host.HasTickAttempt
                    ? Math.Max(0L, host.LastTickAttemptFrame)
                    : 0L;
            var descriptor =
                new RuntimeSaveStateRootHeaderDescriptor(
                    RuntimeSaveState.CurrentSchemaVersion,
                    "mxframework-character-test-p2.4",
                    "level-trigger-"
                    + cache.CombinedCatalogHash.ToString(
                        CultureInfo.InvariantCulture),
                    cache.AuthoringContentHash);
            var registry = new RuntimeSaveStateRegistry();
            if (!TryRegister(
                    registry.Register(
                        new RuntimeSaveStateRootHeaderProvider(descriptor)
                            .CreateParticipant()),
                    out message))
            {
                return false;
            }

            var gameplaySave =
                new GameplayComponentWorldSaveStateProvider(
                    componentWorld,
                    completedFrame);
            if (!TryRegister(
                    registry.Register(
                        GameplayComponentWorldSaveStateProvider.ModuleId,
                        order: 100,
                        gameplaySave,
                        gameplaySave),
                    out message)
                || !TryRegister(
                    registry.Register(
                        GameplayTriggerRuntimeSaveStateParticipant
                            .ParticipantId,
                        GameplayTriggerRuntimeSaveStateParticipant
                            .DefaultSaveStateOrder,
                        triggerSave,
                        triggerSave),
                    out message))
            {
                return false;
            }

            var storySave =
                new StoryRuntimeSaveStateProvider(
                    storyDirector,
                    completedFrame);
            if (!TryRegister(
                    registry.Register(
                        StoryRuntimeSaveStateProvider.ModuleId,
                        order: 300,
                        storySave,
                        storySave),
                    out message))
            {
                return false;
            }

            RuntimeSaveStateCoordinatorFactoryResult factory =
                RuntimeSaveStateCoordinator.CreateAuthoritative(registry);
            if (!factory.Success)
            {
                message =
                    "CharacterTest Trigger SaveState composition failed: "
                    + factory.Error;
                return false;
            }

            var quiescence =
                new RuntimeCheckpointQuiescenceRegistry();
            RuntimeCheckpointQuiescenceRegistryResult quiescenceResult =
                quiescence.Register(
                    GameplayTriggerRuntimeSaveStateParticipant.ParticipantId,
                    GameplayTriggerRuntimeSaveStateParticipant
                        .DefaultQuiescenceOrder,
                    triggerSave);
            if (!quiescenceResult.Success)
            {
                message =
                    "CharacterTest Trigger checkpoint registration failed: "
                    + quiescenceResult.Error;
                return false;
            }

            coordinator = factory.Coordinator;
            checkpointBarrier =
                new RuntimeCheckpointBarrier(
                    host,
                    coordinator,
                    quiescence);
            message = string.Empty;
            return true;
        }

        private static RuntimeHashCombiner CreateHashCombiner(
            GameplayComponentWorld componentWorld,
            GameplayTriggerRuntime runtime,
            StoryDirector storyDirector,
            long combinedCatalogHash)
        {
            return new RuntimeHashCombiner(
                new IRuntimeHashContributor[]
                {
                    new GameplayComponentWorldHashContributor(
                        componentWorld),
                    new GameplayTriggerRuntimeHashContributor(runtime),
                    new StoryRuntimeHashContributor(storyDirector),
                    new TriggerSceneCatalogHashContributor(
                        combinedCatalogHash)
                });
        }

        private static void RegisterGameplaySchemas(
            GameplayComponentSchemaRegistry schemas)
        {
            GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(
                schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(
                schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterSaveState(
                schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterDiagnostics(
                schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterRuntimeHash(
                schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(
                schemas);
            GameplayComponentBuffSchemaDescriptors.RegisterDiagnostics(
                schemas);
            GameplayComponentBuffSchemaDescriptors.RegisterRuntimeHash(
                schemas);
            GameplayComponentBuffSchemaDescriptors.RegisterSaveState(
                schemas);
            GameplayComponentModifierSchemaDescriptors.RegisterDiagnostics(
                schemas);
            GameplayComponentModifierSchemaDescriptors.RegisterRuntimeHash(
                schemas);
            GameplayComponentModifierSchemaDescriptors.RegisterSaveState(
                schemas);
            GameplayComponentAuraSourceSchemaDescriptors.RegisterDiagnostics(
                schemas);
            GameplayComponentAuraSourceSchemaDescriptors.RegisterRuntimeHash(
                schemas);
            GameplayComponentAuraSourceSchemaDescriptors.RegisterSaveState(
                schemas);
            GameplayAbilityCooldownComponentSchemaDescriptors
                .RegisterDiagnostics(schemas);
            GameplayAbilityCooldownComponentSchemaDescriptors
                .RegisterRuntimeHash(schemas);
            GameplayAbilityCooldownComponentSchemaDescriptors
                .RegisterSaveState(schemas);
        }

        private static bool TryRegister(
            RuntimeSaveStateRegistryResult result,
            out string message)
        {
            if (result.Success)
            {
                message = string.Empty;
                return true;
            }

            message =
                "CharacterTest Trigger SaveState participant registration failed: "
                + result.Error;
            return false;
        }

        private static bool HasActionRequirement(
            GameplayTriggerCompiledCatalogPlan plan,
            GameplayTriggerActionKindId actionKindId)
        {
            for (int i = 0; i < plan.ActionRequirementCount; i++)
            {
                if (plan.GetActionRequirement(i).ActionKindId
                    == actionKindId)
                {
                    return true;
                }
            }
            return false;
        }

        private static string FirstIssue(
            LevelWorldObjectAuthoringValidationReport report,
            string fallback)
        {
            if (report?.Issues == null || report.Issues.Count == 0)
                return fallback;
            LevelWorldObjectAuthoringIssue issue = report.Issues[0];
            return issue == null
                ? fallback
                : issue.Code + ": " + issue.Message;
        }

        private static string DiagnosticSuffix(
            GameplayTriggerCatalogBootstrapResult result)
        {
            if (result == null || result.DiagnosticCount == 0)
                return ".";
            GameplayTriggerConfigDiagnostic diagnostic =
                result.GetDiagnostic(0);
            return ": " + diagnostic.Code + " at " + diagnostic.Path + ".";
        }

        private static CharacterTestLevelGameplayTriggerRuntimeStatus Failed(
            CharacterTestGameplayTriggerSceneCache cache,
            CharacterTestLevelGameplayTriggerStartupFailure failure,
            string message)
        {
            return CharacterTestLevelGameplayTriggerRuntimeStatus.Failed(
                cache,
                failure,
                message);
        }

        private sealed class RestoredGameplayEntityValidator :
            IGameplayTriggerRestoreEntityValidator
        {
            private readonly GameplayComponentWorld _world;

            internal RestoredGameplayEntityValidator(
                GameplayComponentWorld world)
            {
                _world = world;
            }

            /// <remarks>NoAlloc.</remarks>
            public bool IsRestoredEntityValid(GameplayEntityId entityId)
            {
                return _world.IsAlive(entityId);
            }
        }

        private sealed class TriggerSceneCatalogHashContributor :
            IRuntimeHashContributor
        {
            internal const string StableContributorId =
                "character-test.level-gameplay-trigger.scene-catalog";

            private readonly long _combinedCatalogHash;

            internal TriggerSceneCatalogHashContributor(
                long combinedCatalogHash)
            {
                _combinedCatalogHash = combinedCatalogHash;
            }

            /// <remarks>NoAlloc.</remarks>
            private const int CombinedCatalogHashFieldId = 1;
            public int ContributorId => RuntimeHashContributorIds.GameplayTriggerSceneCatalog;
            public int HashSchemaId => RuntimeHashSchemaIds.GameplayTriggerSceneCatalog;
            public int HashSchemaVersion => RuntimeHashSchemaIds.Version1;

            /// <remarks>NoAlloc.</remarks>
            public void Contribute(
                RuntimeHashContext context,
                RuntimeHashAccumulator accumulator)
            {
                accumulator.AddLong(
                    CombinedCatalogHashFieldId,
                    _combinedCatalogHash);
            }
        }
    }
}
