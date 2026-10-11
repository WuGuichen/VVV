using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Audio.FMOD;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.StoryBridge;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.CombatBridge;
using MxFramework.Vfx.Unity;
using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestSliceCompositionBuilder
    {
        public static CharacterTestSliceComposition Build(
            CharacterTestSliceOptions options,
            Func<double> elapsedSecondsProvider,
            Action<RuntimeFrame, double> restoreRuntimeState,
            Action throwIfDisposed,
            Action throwIfAuthorityUnavailable = null)
        {
            options = options ?? new CharacterTestSliceOptions();
            var modules = new CharacterTestSliceModules();
            var composition = new CharacterTestSliceComposition(modules);
            try
            {
                // The composition owns incoming resources from entry, including failed construction.
                modules.AudioService = options.AudioService;
                modules.DisposeAudioService = options.DisposeAudioService;
                if (modules.DisposeAudioService && modules.AudioService is IDisposable audio)
                    composition.OwnCleanup("AudioService", audio.Dispose);
                modules.Resources = options.Resources ?? GlobalResourceRuntimeServices.Create();
                composition.OwnCleanup("Resources", modules.Resources.Dispose, first: true);
                options.IncomingOwnershipAccepted?.Invoke();
                BuildCore(options, elapsedSecondsProvider, restoreRuntimeState, throwIfDisposed, composition,
                    throwIfAuthorityUnavailable ?? throwIfDisposed);
                return composition;
            }
            catch (Exception failure)
            {
                var cleanupErrors = new List<Exception>();
                composition.Cleanup(cleanupErrors);
                if (cleanupErrors.Count != 0)
                {
                    cleanupErrors.Insert(0, failure);
                    throw new AggregateException("GameSlice construction and rollback failed.", cleanupErrors);
                }
                throw;
            }
        }

        private static void BuildCore(
            CharacterTestSliceOptions options,
            Func<double> elapsedSecondsProvider,
            Action<RuntimeFrame, double> restoreRuntimeState,
            Action throwIfDisposed,
            CharacterTestSliceComposition composition,
            Action throwIfAuthorityUnavailable)
        {
            CharacterTestSliceModules modules = composition.Modules;
            IRuntimeLogger logger = options.Logger ?? NullRuntimeLogger.Instance;
            CharacterTestStoryContent storyContent = options.TacticalStage != null
                ? new CharacterTestStoryContent(CharacterTestStoryFixture.CreateTacticalBattleGraph(), CharacterTestStoryText.CreateTextMap())
                : options.StoryContent ?? CharacterTestStoryFixture.CreateBootstrapContent();
            logger.Info("GameSlice", "Construct");

            modules.Clock = new RuntimeClock(RuntimeFrame.Zero);
            modules.TraceRecorder = options.EnableTraceFileWrite
                ? CharacterTestTraceRecorder.CreateDefault()
                : CharacterTestTraceRecorder.CreateInMemory();
            composition.OwnCleanup("TraceRecorder", modules.TraceRecorder.Dispose);
            modules.EnableCharacterSimulation = options.EnableCharacterSimulation;
            modules.SkillSlots = CharacterTestSkillSlotRuntime.CreateDefault();
            modules.AudioCueCatalog = options.AudioCueCatalog;
            modules.AudioCatalogStatus = CharacterTestAudioCatalogStatus.From(options.AudioCueCatalog);
            modules.ProceduralHitFeedbackOptions =
                options.ProceduralHitFeedbackOptions ?? CharacterTestProceduralHitFeedbackOptions.Default;
            modules.StoryContent = storyContent;
            modules.StoryDirector = new StoryDirector();
            modules.AbilityRuntime = composition.OwnRuntimeModule(options.FreshAbilityRuntime?.Invoke() ?? CharacterTestAbilityCatalogSource.CreateRuntimeModule());
            modules.BuffDefinitions = CharacterTestGuardAuraRuntime.CreateBuffDefinitions();
            modules.BuffPresentationDefinitions = CharacterTestGuardAuraRuntime.CreateBuffPresentationDefinitions();
            bool hasLevelGameplayTriggers =
                options.GameplayTriggerSceneCache?.TriggerEntryCount > 0;
            modules.Host = new RuntimeHost(new RuntimeHostOptions
            {
                ErrorPolicy = RuntimeHostErrorPolicy.FailFast
            });
            composition.OwnCleanup("RuntimeHost", composition.DisposeHost);
            modules.SceneFlowController = modules.Resources.SceneFlowController;
            if (modules.SceneFlowController != null)
                modules.SceneFlowModule = composition.OwnRuntimeModule(new SceneFlowRuntimeModule(modules.SceneFlowController, "CharacterTest.SceneFlow"));
            if (options.TacticalStage != null)
            {
                if (hasLevelGameplayTriggers) throw new InvalidOperationException("Tactical stage requires its exclusive battle Trigger composition.");
                modules.TacticalStage = composition.OwnRuntimeModule(new TacticalCard.TacticalCardStageRuntime(modules, options.TacticalStage, options.TacticalStageRunId, options.TacticalDeployment));
                var validator = new StoryRuntimeCommandValidator(modules.StoryDirector);
                modules.StoryModule = composition.OwnRuntimeModule(new StoryRuntimeModule(modules.StoryDirector,
                    RuntimeCommandBuffer.CreateBounded(validator, RuntimeFrame.Zero, 64), null, validator, modules.TacticalStage));
            }
            else if (hasLevelGameplayTriggers)
            {
                modules.LevelGameplayTriggerEvents =
                    new GameplayTriggerRuntimeEventSource(
                        CharacterTestLevelGameplayTriggerRuntimeComposition
                            .RuntimeEventCapacity);
                var storyValidator =
                    new StoryRuntimeCommandValidator(
                        modules.StoryDirector);
                RuntimeCommandBuffer storyCommands =
                    RuntimeCommandBuffer.CreateBounded(
                        storyValidator,
                        RuntimeFrame.Zero,
                        pendingCapacityLimit: 64);
                modules.StoryModule = composition.OwnRuntimeModule(new StoryRuntimeModule(
                    modules.StoryDirector,
                    storyCommands,
                    events: null,
                    validator: storyValidator,
                    commandResultSink:
                        new GameplayTriggerStoryCommandResultRelay(
                            modules.LevelGameplayTriggerEvents)));
            }
            else
            {
                modules.StoryModule = composition.OwnRuntimeModule(new StoryRuntimeModule(
                    modules.StoryDirector,
                    new RuntimeCommandBuffer(null, RuntimeFrame.Zero)));
            }
            modules.AbilityHotbar = new CharacterTestAbilityHotbarRuntime(modules.AbilityRuntime.Definitions);
            modules.CharacterRuntime = composition.OwnRuntimeModule(new CharacterTestCharacterRuntimeModule(
                modules.Resources,
                logger,
                actionConfigOptions: options.ActionConfigOptions,
                physicsRuntime: options.PhysicsRuntime,
                traceRecorder: modules.TraceRecorder,
                createViews: options.CreateCharacterViews,
                smoothNetworkSnapshotViews: options.SmoothNetworkSnapshotViews,
                audioService: options.AudioService,
                audioCueCatalog: options.AudioCueCatalog,
                abilityRuntimeModule: modules.AbilityRuntime,
                buffDefinitions: modules.BuffDefinitions,
                buffPresentationDefinitions: modules.BuffPresentationDefinitions,
                simulationTickRate: options.SimulationTickRate,
                proceduralHitFeedbackOptions: modules.ProceduralHitFeedbackOptions,
                aiBehaviorProfile: options.AiBehaviorProfile,
                movableNaturalObjectSpawnSpecs: options.MovableNaturalObjectSpawnSpecs,
                actionCatalog: options.ActionCatalog,
                actionRuntime: options.FreshActionRuntime?.Invoke()));

            modules.CharacterRuntime.SetPresentationAllyTeamId(options.PresentationAllyTeamId);
            if (hasLevelGameplayTriggers
                && !options.EnableCharacterSimulation)
            {
                modules.LevelGameplayTriggerStatus =
                    CharacterTestLevelGameplayTriggerRuntimeStatus.Failed(
                        options.GameplayTriggerSceneCache,
                        CharacterTestLevelGameplayTriggerStartupFailure
                            .CompositionRejected,
                        "Level Gameplay Triggers require CharacterTest character simulation.");
            }
            else if (hasLevelGameplayTriggers)
            {
                bool triggerCompositionCreated =
                    CharacterTestLevelGameplayTriggerRuntimeComposition
                        .TryCreate(
                            options.GameplayTriggerSceneCache,
                            modules.Host,
                            modules.StoryModule,
                            modules.CharacterRuntime,
                            modules.BuffDefinitions,
                            modules.LevelGameplayTriggerEvents,
                            out CharacterTestLevelGameplayTriggerRuntimeComposition
                                levelGameplayTriggers,
                            out CharacterTestLevelGameplayTriggerRuntimeStatus
                                triggerStatus);
                modules.LevelGameplayTriggers =
                    triggerCompositionCreated
                        ? levelGameplayTriggers
                        : null;
                if (modules.LevelGameplayTriggers != null)
                {
                    composition.OwnRuntimeModule(modules.LevelGameplayTriggers.GameplayModule);
                    composition.OwnRuntimeModule(modules.LevelGameplayTriggers.ReliableOutbox);
                }
                modules.LevelGameplayTriggerStatus = triggerStatus;
                if (!triggerCompositionCreated)
                {
                    logger.Warning(
                        "GameSlice",
                        "Level Gameplay Triggers failed closed: "
                        + triggerStatus.StartupFailure
                        + " "
                        + triggerStatus.StartupMessage);
                }
            }
            else
            {
                modules.LevelGameplayTriggerStatus =
                    CharacterTestLevelGameplayTriggerRuntimeStatus.Disabled(
                        options.GameplayTriggerSceneCache);
            }
            CharacterTestPerceptionFrameCache perceptionFrameCache =
                modules.CharacterRuntime.PerceptionFrameCache;
            CharacterTestNavigationSceneInputResult navigationInputResult =
                options.NavigationInputResult.Status != CharacterTestNavigationSceneInputStatus.None
                    ? options.NavigationInputResult
                    : options.PhysicsRuntime?.NavigationInputResult ?? default;
            modules.NavigationSession = CharacterTestNavigationSessionBuilder.Build(
                options.NavigationBuildInput ?? options.PhysicsRuntime?.NavigationBuildInput,
                options.PhysicsRuntime,
                navigationInputResult,
                modules.CharacterRuntime.ResolveMaximumNavigationAgentHalfExtents());
            modules.RuntimeCommands = composition.OwnRuntimeModule(new CharacterTestRuntimeCommandModule(
                modules.CharacterRuntime,
                logger,
                options.InitialCameraMode,
                perceptionFrameCache));
            modules.CharacterRuntime.SetLockedTargetEntityIdProvider(() =>
                modules.RuntimeCommands.TargetLockEnabled ? modules.RuntimeCommands.LockedTargetEntityId : 0);
            modules.CharacterRuntime.SetLockedCombatTargetProvider(() =>
                modules.RuntimeCommands.TargetLockEnabled
                    ? modules.RuntimeCommands.LockedCombatTarget
                    : default);

            CharacterTestSliceServices services = null;
            CharacterTestRuntimeAiPlannerProfile runtimeAiPlannerProfile =
                options.RuntimeAiPlannerProfile
                ?? CharacterTestRuntimeAiPlannerProfileSource.CreateDefault(options.AiBehaviorProfile);
            modules.RuntimeAiBehaviorLoadouts =
                options.RuntimeAiBehaviorLoadouts
                ?? CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime.CreateDefault(
                    runtimeAiPlannerProfile,
                    options.RuntimeAiPlannerProfileBindings,
                    logger);
            modules.CharacterRuntime.SetRuntimeAiBehaviorLoadouts(modules.RuntimeAiBehaviorLoadouts);
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> runtimeAiPlannerProfileBindings =
                CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime.CombineProfileBindings(
                    options.RuntimeAiPlannerProfileBindings,
                    modules.RuntimeAiBehaviorLoadouts);
            modules.NavigationService = CreateRuntimeAiNavigationService(
                options.PhysicsRuntime,
                options.AiBehaviorProfile,
                perceptionFrameCache,
                modules.NavigationSession);
            modules.RuntimeAiPlannerCommands = composition.OwnRuntimeModule(new CharacterTestRuntimeAiPlannerCommandModule(
                modules.CharacterRuntime,
                // Runtime AI Planner is part of the authoritative simulation. It must not inherit
                // the connected peer's optional entity-control grant.
                command => services.Commands.EnqueueAuthoritativeRuntimeCommand(command),
                logger,
                runtimeAiPlannerProfile,
                modules.NavigationService,
                modules.AbilityRuntime,
                modules.AbilityHotbar,
                runtimeAiPlannerProfileBindings,
                options.RuntimeAiPlannerProfileReport,
                modules.NavigationSession));
            modules.CharacterRuntime.SetRuntimeAiPlannerDodgeOutcomeSink(
                modules.RuntimeAiPlannerCommands);
            if (options.EnableLocalInputCommands && options.TacticalStage == null)
            {
                modules.LocalInputCommands = composition.OwnRuntimeModule(new CharacterTestLocalInputCommandModule(
                    modules.CharacterRuntime,
                    command => services.Commands.EnqueueRuntimeCommand(command),
                    logger,
                    options.InputProvider,
                    modules.AbilityHotbar,
                    clearCommandWarmupState: () => modules.RuntimeCommands.ClearWarmupSubmissionState()));
            }

            modules.VfxRuntime = composition.OwnRuntimeModule(new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer()));
            modules.CharacterRuntime.SetInteractionVfxRuntime(modules.VfxRuntime);
            modules.CharacterRuntime.CombatTraceSystem.SetVfxRuntime(modules.VfxRuntime);
            modules.ActionVfxBridge = composition.OwnRuntimeModule(new CharacterTestActionVfxBridgeModule(
                () => modules.CharacterRuntime?.Entities,
                modules.VfxRuntime,
                logger,
                (entityId, actionInstanceId) =>
                    modules.CharacterRuntime != null
                    && modules.CharacterRuntime.CombatTraceSystem.TryResolveChannelBeamAimDirection(
                        entityId,
                        actionInstanceId,
                        out var direction)
                        ? direction
                        : (MxFramework.Core.Math.FixVector3?)null));
            modules.LaserPathVfxBridge = composition.OwnRuntimeModule(new CharacterTestLaserPathVfxBridgeModule(
                () => modules.CharacterRuntime?.CombatTraceSystem?.LastFrameLaserPathVfxEvents,
                modules.VfxRuntime));
            modules.ChainLightningVfxBridge = composition.OwnRuntimeModule(new CharacterTestChainLightningVfxBridgeModule(
                () => modules.CharacterRuntime?.CombatTraceSystem?.LastFrameChainLightningVfxEvents,
                modules.VfxRuntime));
            var combatVfxBridge = new CombatVfxBridge(
                modules.VfxRuntime,
                CharacterTestVfxDefinitionSource.CreateTriggerResolver());
            modules.CombatVfxBridge = composition.OwnRuntimeModule(new CharacterTestCombatVfxBridgeModule(
                () => modules.CharacterRuntime?.CombatTraceSystem?.HitPresentationEvents,
                combatVfxBridge));
            modules.StatusVfxBridge = composition.OwnRuntimeModule(new CharacterTestStatusVfxBridgeModule(
                () => modules.CharacterRuntime?.World,
                modules.VfxRuntime));
            if (options.CreateCharacterViews
                && options.EnableCharacterSimulation
                && modules.ProceduralHitFeedbackOptions.Enabled)
            {
                modules.ProceduralHitFeedback = composition.OwnRuntimeModule(new CharacterTestProceduralHitFeedbackModule(
                    () => modules.CharacterRuntime?.CombatTraceSystem?.DamageResolvedEvents,
                    () => modules.CharacterRuntime?.Entities,
                    modules.CharacterRuntime,
                    modules.ProceduralHitFeedbackOptions,
                    CharacterTestGeneratedPackageSource.LoadPackageForEntity));
            }

            modules.VfxEventFanout = new VfxRuntimeEventFanout(modules.VfxRuntime);
            CombatProjectileUnityPresentationRegistry projectilePresentationRegistry =
                CharacterTestProjectilePresentationSource.CreateRegistry();
            if (options.CreateCharacterViews && modules.Resources?.ResourceManager != null)
            {
                modules.BladeTrailBindingSource = new CharacterTestBladeTrailBindingSource(modules.CharacterRuntime);
                modules.UnityVfxPresenter = new UnityVfxPresenter(
                    modules.VfxRuntime,
                    modules.Resources.ResourceManager,
                    usePooling: true,
                    anchorResolver: new CharacterTestVfxAnchorResolver(modules.CharacterRuntime.ViewBridge),
                    backendBindingSource: modules.BladeTrailBindingSource,
                    presentationBudgetSource: new CharacterTestVfxPresentationBudgetSource(modules.CharacterRuntime.ViewBridge.PresentationLod));
                composition.OwnCleanup("UnityVfxPresenter", modules.UnityVfxPresenter.Dispose);
                modules.UnityVfxPresenter.PrewarmDefinitions(poolSizePerDefinition: 1);
                modules.VfxEventFanout.AddSink(modules.UnityVfxPresenter);
                modules.ProjectilePresenter = new CombatProjectileUnityPresenter(
                    modules.Resources.ResourceManager,
                    projectilePresentationRegistry,
                    usePooling: true);
                composition.OwnCleanup("ProjectilePresenter", modules.ProjectilePresenter.Dispose);
                modules.ProjectilePresenter.PrewarmBindings(poolSizePerBinding: 1);
            }
            modules.CharacterRuntime.CombatTraceSystem.SetProjectileVfxBridge(
                new CombatProjectileVfxBridge(
                    projectilePresentationRegistry,
                    modules.VfxRuntime,
                    modules.ProjectilePresenter));

            modules.VfxPresentation = composition.OwnRuntimeModule(new CharacterTestVfxPresentationModule(modules.VfxEventFanout));
            modules.CharacterSpawnFlow = new CharacterTestCharacterSpawnFlow(
                modules.Resources,
                logger,
                modules.RuntimeCommands,
                modules.CharacterRuntime,
                () => modules.Clock.CurrentFrame);
            composition.OwnCleanup("CharacterSpawnFlow", modules.CharacterSpawnFlow.Release);
            modules.StoryFlowBridge = new CharacterTestStoryFlowBridge(
                modules.StoryModule,
                modules.Resources,
                logger,
                () => modules.Clock.CurrentFrame,
                options.BaseResourcePreloadGroupId,
                modules.CharacterSpawnFlow,
                options.StartupCharacterSpawnSpecs)
            {
                SuppressMissingStartupSpawnWarning = options.SuppressMissingStartupSpawnWarning
            };
            composition.OwnCleanup("StoryFlowBridge", modules.StoryFlowBridge.Dispose);
            if (options.MixedPlayerTacticalStage != null)
                modules.MixedPlayerTacticalStage = composition.OwnRuntimeModule(new TacticalCard.TacticalMixedPlayerCardRuntime(
                    modules, options.MixedPlayerTacticalStage, options.TacticalDeployment, detachedRestore: options.DetachedTacticalRestore, stageRunId: options.TacticalStageRunId));

            services = new CharacterTestSliceServices(
                modules,
                logger,
                new RuntimeLogBuffer(160),
                () => modules.Clock.CurrentFrame,
                elapsedSecondsProvider,
                restoreRuntimeState,
                throwIfDisposed,
                throwIfAuthorityUnavailable);

            composition.Services = services;
            if (modules.LevelGameplayTriggers != null)
                modules.LevelGameplayTriggers.AuthorityAvailabilityGuard = throwIfAuthorityUnavailable;
            IDisposable storyEventSubscription =
                modules.StoryDirector.SubscribeEvents(services.StoryFlow.OnStoryDirectorEvent);
            composition.OwnCleanup("StoryEventSubscription", storyEventSubscription.Dispose);
            modules.TacticalStage?.BindStoryFlow();
            RegisterRuntimeModules(composition);
            if (modules.LevelGameplayTriggers != null
                && !modules.LevelGameplayTriggers.TryVerifyComposition(
                    modules.Host,
                    out string triggerCompositionMessage))
            {
                throw new InvalidOperationException(
                    triggerCompositionMessage);
            }
            if (!options.DetachedTacticalRestore) composition.InitializeAndStartHost();
            logger.Info("GameSlice", "RuntimeHost started");
        }

        private static void RegisterRuntimeModules(CharacterTestSliceComposition composition)
        {
            CharacterTestSliceModules modules = composition.Modules;
            if (modules.TacticalStage != null) composition.RegisterRuntimeModule(modules.TacticalStage, profiled: false);
            if (modules.MixedPlayerTacticalStage != null) composition.RegisterRuntimeModule(modules.MixedPlayerTacticalStage, profiled: false);
            if (modules.LevelGameplayTriggers?.ReliableOutbox != null)
            {
                composition.RegisterRuntimeModule(
                    modules.LevelGameplayTriggers.ReliableOutbox, profiled: false);
            }
            if (modules.LocalInputCommands != null)
                composition.RegisterRuntimeModule(modules.LocalInputCommands);
            if (modules.RuntimeAiPlannerCommands != null)
                composition.RegisterRuntimeModule(modules.RuntimeAiPlannerCommands);
            if (modules.LevelGameplayTriggers?.ReliableOutbox != null || modules.TacticalStage != null)
                composition.RegisterRuntimeModule(modules.StoryModule, profiled: false);
            else
                composition.RegisterRuntimeModule(modules.StoryModule);
            composition.RegisterRuntimeModule(modules.RuntimeCommands);
            if (modules.AbilityRuntime != null)
                composition.RegisterRuntimeModule(modules.AbilityRuntime);
            if (modules.EnableCharacterSimulation)
                composition.RegisterRuntimeModule(modules.CharacterRuntime);
            if (modules.LevelGameplayTriggers != null)
            {
                composition.RegisterRuntimeModule(
                    modules.LevelGameplayTriggers.GameplayModule);
            }
            composition.RegisterRuntimeModule(modules.ActionVfxBridge);
            composition.RegisterRuntimeModule(modules.LaserPathVfxBridge);
            composition.RegisterRuntimeModule(modules.ChainLightningVfxBridge);
            composition.RegisterRuntimeModule(modules.CombatVfxBridge);
            composition.RegisterRuntimeModule(modules.StatusVfxBridge);
            if (modules.ProceduralHitFeedback != null)
                composition.RegisterRuntimeModule(modules.ProceduralHitFeedback);
            composition.RegisterRuntimeModule(modules.VfxRuntime);
            composition.RegisterRuntimeModule(modules.VfxPresentation);
            if (modules.AudioService != null)
                composition.RegisterRuntimeModule(
                    composition.OwnRuntimeModule(new AudioRuntimeModule(modules.AudioService, disposeAudioService: false)));
            if (modules.SceneFlowModule != null)
                composition.RegisterRuntimeModule(modules.SceneFlowModule);
        }

        private static ICharacterTestAiNavigationService CreateRuntimeAiNavigationService(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestAiBehaviorProfile aiBehaviorProfile,
            CharacterTestPerceptionFrameCache perceptionFrameCache,
            CharacterTestNavigationSession navigationSession)
        {
            return physicsRuntime != null && physicsRuntime.IsReady
                ? new CharacterTestCombatPhysicsNavigationService(
                    physicsRuntime,
                    aiBehaviorProfile,
                    perceptionFrameCache,
                    navigationSession)
                : null;
        }
    }

    internal sealed class CharacterTestProfiledRuntimeModule : IRuntimeModule
    {
        private readonly IRuntimeModule _inner;
        private readonly ProfilerMarker _tickMarker;

        public CharacterTestProfiledRuntimeModule(IRuntimeModule inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _tickMarker = new ProfilerMarker("CharacterTest.RuntimeModule." + _inner.ModuleId);
        }

        public string ModuleId => _inner.ModuleId;
        public int Priority => _inner.Priority;
        public RuntimeTickStage TickStage => _inner.TickStage;

        public void Initialize(RuntimeHostContext context)
        {
            _inner.Initialize(context);
        }

        public void Start(RuntimeHostContext context)
        {
            _inner.Start(context);
        }

        /// <summary>
        /// NoAlloc after construction. Adds a profiler marker around the wrapped runtime module tick.
        /// </summary>
        public void Tick(RuntimeTickContext context)
        {
            using (_tickMarker.Auto())
            {
                _inner.Tick(context);
            }
        }

        public void Stop(RuntimeHostContext context)
        {
            _inner.Stop(context);
        }

        public void Dispose()
        {
            _inner.Dispose();
        }
    }

    /// <summary>Startup/teardown ownership. Only successful Host registration transfers a module.</summary>
    internal sealed class CharacterTestSliceComposition : IDisposable
    {
        private readonly CharacterTestCleanupScope _cleanup = new CharacterTestCleanupScope();
        private readonly List<OwnedModule> _ownedModules = new List<OwnedModule>();
        private bool _disposed;

        public CharacterTestSliceComposition(CharacterTestSliceModules modules)
        {
            Modules = modules ?? throw new ArgumentNullException(nameof(modules));
        }

        public CharacterTestSliceModules Modules { get; }
        public CharacterTestSliceServices Services { get; internal set; }

        internal void OwnCleanup(string owner, Action cleanup, bool first = false) => _cleanup.Own(owner, cleanup, first);

        internal T OwnRuntimeModule<T>(T module) where T : IRuntimeModule
        {
            if (module == null) return module;
            _ownedModules.Add(new OwnedModule(module, _cleanup.Own(module.ModuleId, module.Dispose)));
            return module;
        }

        internal void RegisterRuntimeModule(IRuntimeModule module, bool profiled = true)
        {
            OwnedModule owned = null;
            for (int i = 0; i < _ownedModules.Count; i++)
                if (ReferenceEquals(_ownedModules[i].Module, module)) { owned = _ownedModules[i]; break; }
            if (owned == null)
                throw new InvalidOperationException("Cannot register a module without composition ownership.");
            Modules.Host.RegisterModule(profiled ? new CharacterTestProfiledRuntimeModule(module) : module);
            owned.Cleanup.Transfer();
            _ownedModules.Remove(owned);
        }

        internal void InitializeAndStartHost()
        {
            Modules.Host.Initialize();
            RequireHostState(RuntimeLifecycleState.Initialized, RuntimeHostOperation.Initialize);
            Modules.Host.Start();
            RequireHostState(RuntimeLifecycleState.Started, RuntimeHostOperation.Start);
        }

        private void RequireHostState(RuntimeLifecycleState expected, RuntimeHostOperation operation)
        {
            if (Modules.Host.State == expected) return;
            // Collecting policies still fail construction; never publish a faulted composition.
            for (int i = 0; i < Modules.Host.Errors.Count; i++)
                if (Modules.Host.Errors[i].Operation == operation)
                    throw new RuntimeHostException(Modules.Host.Errors[i]);
            throw new InvalidOperationException("RuntimeHost did not complete " + operation + ". State=" + Modules.Host.State);
        }

        internal void Cleanup(List<Exception> errors)
        {
            if (_disposed) return;
            _disposed = true;
            if (Modules.Host != null)
                CharacterTestCleanupScope.Attempt(errors, "RuntimeHost.Stop", StopHost);
            _cleanup.Cleanup(errors);
            if (Modules.Host != null)
                for (int i = 0; i < Modules.Host.Errors.Count; i++)
                {
                    RuntimeHostError error = Modules.Host.Errors[i];
                    if (error.Operation == RuntimeHostOperation.Stop || error.Operation == RuntimeHostOperation.Dispose)
                        errors.Add(new RuntimeHostException(error));
                }
            _ownedModules.Clear();
        }

        private void StopHost()
        {
            // Host retains each module error under both policies. Include them once in Cleanup.
            try { Modules.Host.Stop(); }
            catch (RuntimeHostException) { }
        }

        internal void DisposeHost()
        {
            try { Modules.Host.Dispose(); }
            catch (RuntimeHostException) { }
        }

        public void Dispose()
        {
            var errors = new List<Exception>();
            Cleanup(errors);
            CharacterTestCleanupScope.ThrowIfFailed(errors, "GameSlice composition cleanup failed.");
        }

        private sealed class OwnedModule
        {
            internal OwnedModule(IRuntimeModule module, CharacterTestCleanupScope.Entry cleanup)
            { Module = module; Cleanup = cleanup; }
            internal IRuntimeModule Module { get; }
            internal CharacterTestCleanupScope.Entry Cleanup { get; }
        }
    }

    /// <summary>LowFreqAlloc. Reverse ownership rollback, one attempt per entry, all failures retained.</summary>
    internal sealed class CharacterTestCleanupScope : IDisposable
    {
        private readonly List<Entry> _entries = new List<Entry>();
        private bool _disposed;

        internal Entry Own(string owner, Action cleanup, bool first = false)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CharacterTestCleanupScope));
            var entry = new Entry(owner, cleanup ?? throw new ArgumentNullException(nameof(cleanup)));
            if (first) _entries.Insert(0, entry);
            else _entries.Add(entry);
            return entry;
        }

        internal void Cleanup(List<Exception> errors)
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = _entries.Count - 1; i >= 0; i--)
                _entries[i].Attempt(errors);
            _entries.Clear();
        }

        public void Dispose()
        {
            var errors = new List<Exception>();
            Cleanup(errors);
            ThrowIfFailed(errors, "CharacterTest lifetime cleanup failed.");
        }

        internal static void Attempt(List<Exception> errors, string owner, Action cleanup)
        {
            try { cleanup(); }
            catch (Exception failure) { errors.Add(new InvalidOperationException(owner + " cleanup failed.", failure)); }
        }

        internal static void ThrowIfFailed(List<Exception> errors, string message)
        {
            if (errors.Count != 0) throw new AggregateException(message, errors);
        }

        internal sealed class Entry
        {
            private readonly string _owner;
            private Action _cleanup;
            internal Entry(string owner, Action cleanup) { _owner = owner; _cleanup = cleanup; }
            internal void Transfer() => _cleanup = null;
            internal void Attempt(List<Exception> errors)
            {
                Action cleanup = _cleanup;
                _cleanup = null;
                if (cleanup != null) CharacterTestCleanupScope.Attempt(errors, _owner, cleanup);
            }
        }
    }
}
