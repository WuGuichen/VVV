using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.DebugUI;
using MxFramework.DebugUI.Adapters;
using MxFramework.Diagnostics;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Input;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    internal static class SliceTraceKinds
    {
        internal const int CameraMode = 90843, TargetLockToggle = 90844, SkillPanelSlot = 90845,
            AbilitySlot = 90846, SurfaceProbe = 90847, MovableReset = 90848, MovablePotency = 90849,
            AuthorityPosePreview = 90850, Bootstrap = 90851, Trigger = 90852;
    }

    internal sealed class CharacterTestSliceServices
    {
        public CharacterTestSliceServices(
            CharacterTestSliceModules modules,
            IRuntimeLogger logger,
            RuntimeLogBuffer logBuffer,
            Func<RuntimeFrame> currentFrameProvider,
            Func<double> elapsedSecondsProvider,
            Action<RuntimeFrame, double> restoreRuntimeState,
            Action throwIfDisposed,
            Action throwIfAuthorityUnavailable = null)
        {
            Modules = modules ?? throw new ArgumentNullException(nameof(modules));
            Logger = logger ?? NullRuntimeLogger.Instance;
            LogBuffer = logBuffer ?? new RuntimeLogBuffer(160);
            CurrentFrameProvider = currentFrameProvider ?? throw new ArgumentNullException(nameof(currentFrameProvider));
            ElapsedSecondsProvider = elapsedSecondsProvider ?? (() => 0d);
            ThrowIfDisposed = throwIfDisposed ?? (() => { });
            Action authorityGuard = throwIfAuthorityUnavailable ?? ThrowIfDisposed;

            Commands = new CharacterTestSliceCommandServices(
                Modules,
                Logger,
                CurrentFrameProvider,
                authorityGuard);
            Network = new CharacterTestSliceNetworkServices(
                Modules,
                ElapsedSecondsProvider,
                restoreRuntimeState,
                authorityGuard);
            Presentation = new CharacterTestSlicePresentationServices(
                Modules,
                Commands,
                Logger,
                CurrentFrameProvider,
                ThrowIfDisposed);
            StoryFlow = new CharacterTestSliceStoryFlowServices(
                Modules,
                Logger,
                LogBuffer,
                CurrentFrameProvider,
                authorityGuard);
            Debug = new CharacterTestSliceDebugServices(Modules, this, ThrowIfDisposed);
        }

        public CharacterTestSliceModules Modules { get; }
        public CharacterTestSliceCommandServices Commands { get; }
        public CharacterTestSlicePresentationServices Presentation { get; }
        public CharacterTestSliceStoryFlowServices StoryFlow { get; }
        public CharacterTestSliceNetworkServices Network { get; }
        public CharacterTestSliceDebugServices Debug { get; }
        public RuntimeFrame CurrentFrame => CurrentFrameProvider();
        public double ElapsedSeconds => ElapsedSecondsProvider();
        public RuntimeHost Host => Modules.Host;
        public RuntimeClock Clock => Modules.Clock;
        public StoryDirector StoryDirector => Modules.StoryDirector;
        public StoryRuntimeModule StoryModule => Modules.StoryModule;
        public CharacterTestRuntimeCommandModule RuntimeCommands => Modules.RuntimeCommands;
        public AbilityRuntimeModule AbilityRuntime => Modules.AbilityRuntime;
        public CharacterTestSkillSlotRuntime SkillSlots => Modules.SkillSlots;
        public CharacterTestAbilityHotbarRuntime AbilityHotbar => Modules.AbilityHotbar;
        public GameplayBuffDefinitionRegistry BuffDefinitions => Modules.BuffDefinitions;
        public GameplayBuffPresentationDefinitionRegistry BuffPresentationDefinitions =>
            Modules.BuffPresentationDefinitions;
        public CharacterTestCharacterRuntimeModule CharacterRuntime => Modules.CharacterRuntime;
        public CharacterTestRuntimeAiPlannerCommandModule RuntimeAiPlannerCommands =>
            Modules.RuntimeAiPlannerCommands;
        public GlobalResourceRuntimeServices Resources => Modules.Resources;
        public IAudioService AudioService => Modules.AudioService;
        internal ICharacterTestAudioCueCatalog AudioCueCatalog => Modules.AudioCueCatalog;
        internal CharacterTestAudioCatalogStatus AudioCatalogStatus => Modules.AudioCatalogStatus;
        public SceneFlowController SceneFlowController => Modules.SceneFlowController;
        public bool HasSceneFlow => Modules.HasSceneFlow;
        public CharacterTestCharacterActionRuntime ActionRuntime => Modules.ActionRuntime;
        public CharacterTestCharacterCombatTraceSystem CombatTraceSystem => Modules.CombatTraceSystem;
        public VfxRuntimeModule VfxRuntime => Modules.VfxRuntime;
        public UnityVfxPresenter VfxPresenter => Modules.UnityVfxPresenter;
        public CombatProjectileUnityPresenter ProjectilePresenter => Modules.ProjectilePresenter;
        public bool ProceduralHitFeedbackEnabled => Presentation.ProceduralHitFeedbackEnabled;
        public CharacterTestProceduralHitFeedbackResult LastProceduralHitFeedbackResult =>
            Presentation.LastProceduralHitFeedbackResult;
        public CharacterTestProceduralEffectPreviewResult LastProceduralEffectPreviewResult =>
            Presentation.LastProceduralEffectPreviewResult;
        public CharacterTestCharacterActionReloadResult? LastActionReloadResult => Modules.LastActionReloadResult;
        public CharacterTestCharacterMovementProfileRuntime MovementProfile => Modules.MovementProfile;
        public CharacterTestCharacterPhysicsRuntime PhysicsRuntime => Modules.PhysicsRuntime;
        public CharacterTestInteractionDebugSnapshot InteractionDebugSnapshot => Modules.InteractionDebugSnapshot;
        public IReadOnlyList<CharacterTestInteractionEvent> LastFrameInteractionEvents =>
            Modules.LastFrameInteractionEvents;
        public CharacterTestInteractionPhysicsSummary InteractionPhysicsSummary =>
            Modules.InteractionPhysicsSummary;
        public CharacterTestInteractionVfxSummary InteractionVfxSummary =>
            Modules.InteractionVfxSummary;
        public CharacterTestInteractionAudioSummary InteractionAudioSummary =>
            Modules.InteractionAudioSummary;
        public CharacterTestPhysicalAudioSummary PhysicalAudioSummary =>
            Modules.PhysicalAudioSummary;
        public ContinuousPhysicalAudioSnapshot ContinuousPhysicalAudioSnapshot =>
            Modules.CharacterRuntime?.ContinuousPhysicalAudioSnapshot ?? default;
        public CharacterTestAudioIntentDrainSummary AudioIntentDrainSummary =>
            Modules.AudioIntentDrainSummary;
        internal AudioPresentationDrain AudioPresentationDrain =>
            Modules.CharacterRuntime?.AudioPresentationDrain;
        internal CharacterTestBodyPartContactDebugSnapshot BodyPartContactDebugSnapshot =>
            Modules.BodyPartContactDebugSnapshot;
        public CharacterTestInteractionRenderingSummary InteractionRenderingSummary =>
            Modules.InteractionRenderingSummary;
        public CharacterTestInteractionConsumerDispatchSummary InteractionConsumerSummary =>
            Modules.InteractionConsumerSummary;
        public CharacterTestInteractionPerformanceCounterSource InteractionPerformanceCounterSource =>
            Modules.InteractionPerformanceCounterSource;
        internal CharacterTestRuntimePerformanceCounterSource RuntimePerformanceCounterSource =>
            Modules.RuntimePerformanceCounterSource;
        public string LastGenesisMovableObjectSummary => Modules.LastGenesisMovableObjectSummary;
        public CharacterTestGenesisMovableNaturalObjectValidation LastGenesisMovableObjectValidation =>
            Modules.LastGenesisMovableObjectValidation;
        public string LastGenesisPotencyMechanicsSummary => Modules.LastGenesisPotencyMechanicsSummary;
        public CharacterTestGenesisPotencyMechanicsValidation LastGenesisPotencyMechanicsValidation =>
            Modules.LastGenesisPotencyMechanicsValidation;
        public string LastGenesisLivingBeingSummary => Modules.LastGenesisLivingBeingSummary;
        public CharacterTestGenesisLivingBeingValidation LastGenesisLivingBeingValidation =>
            Modules.LastGenesisLivingBeingValidation;
        public string LastGenesisLivingNaturalAttributesSummary =>
            Modules.LastGenesisLivingNaturalAttributesSummary;
        public CharacterTestGenesisLivingNaturalAttributesValidation LastGenesisLivingNaturalAttributesValidation =>
            Modules.LastGenesisLivingNaturalAttributesValidation;
        public string LastGenesisLivingNaturalAttributesRuntimeStateSummary =>
            Modules.LastGenesisLivingNaturalAttributesRuntimeStateSummary;
        public CharacterTestTraceRecorder TraceRecorder => Modules.TraceRecorder;
        public CharacterTestCameraMode CameraMode => Modules.RuntimeCommands.CameraMode;
        public bool TargetLockEnabled => Modules.RuntimeCommands.TargetLockEnabled;
        public int LockedTargetEntityId => Modules.RuntimeCommands.LockedTargetEntityId;
        internal CharacterTestCombatTargetHandle LockedCombatTarget =>
            Modules.RuntimeCommands.LockedCombatTarget;
        internal bool TryGetLockedCombatTargetSample(
            out CharacterTestLockedHurtboxDistanceSample sample)
        {
            sample = default;
            CharacterTestCombatTargetHandle handle = LockedCombatTarget;
            return handle.HasTarget
                && Modules.CharacterRuntime?.PerceptionFrameCache != null
                && Modules.CharacterRuntime.PerceptionFrameCache.TryGetLockedHurtboxDistance(
                    Modules.CharacterRuntime.PerceptionFrameCache.CurrentFrame,
                    handle,
                    out sample);
        }
        public CharacterTestHeadPadDisplayMode HeadPadDisplayMode => Modules.HeadPadDisplayMode;
        public int PendingRuntimeCommandCount => Modules.PendingRuntimeCommandCount;
        public RuntimeEventQueueSnapshot RuntimeEventQueueSnapshot => Modules.RuntimeEventQueueSnapshot;
        public IReadOnlyList<CharacterTestRuntimeEvent> RecentRuntimeEvents => Modules.RecentRuntimeEvents;
        public StoryDirectorSnapshot StorySnapshot => Modules.StorySnapshot;
        public IReadOnlyList<CharacterTestCharacterEntity> CharacterEntities => Modules.CharacterEntities;
        public IReadOnlyList<CharacterTestMovableNaturalObjectState> MovableNaturalObjects =>
            Modules.MovableNaturalObjects;
        public CharacterTestMovableNaturalObjectSummary MovableNaturalObjectSummary =>
            Modules.MovableNaturalObjectSummary;
        internal IRuntimeLogger Logger { get; }
        internal RuntimeLogBuffer LogBuffer { get; }
        internal Func<RuntimeFrame> CurrentFrameProvider { get; }
        internal Func<double> ElapsedSecondsProvider { get; }
        internal Action ThrowIfDisposed { get; }

        public RuntimeCommandValidationResult EnqueueRuntimeCommand(RuntimeCommand command) =>
            Commands.EnqueueRuntimeCommand(command);
        public void SetLocalInputProvider(IInputProvider provider) => Commands.SetLocalInputProvider(provider);
        public void SetRuntimeCommandSubmitter(Func<RuntimeCommand, RuntimeCommandValidationResult> submitter) =>
            Commands.SetRuntimeCommandSubmitter(submitter);
        public CharacterTestCharacterActionReloadResult ReloadCharacterActionConfig() =>
            Commands.ReloadCharacterActionConfig();
        public CharacterTestCharacterActionReloadResult ReloadCharacterActionConfig(
            CharacterTestCharacterActionConfigSourceOptions options) =>
            Commands.ReloadCharacterActionConfig(options);
        public RuntimeCommandValidationResult EnqueueSetCameraMode(
            CharacterTestCameraMode mode,
            int sourceId = CharacterTestRuntimeCommandSources.System,
            RuntimeTraceIdentity traceId = default) =>
            Commands.EnqueueSetCameraMode(mode, sourceId, traceId);
        public RuntimeCommandValidationResult EnqueueToggleTargetLock(
            int primaryEntityId,
            int sourceId = CharacterTestRuntimeCommandSources.CameraInput,
            RuntimeTraceIdentity traceId = default) =>
            Commands.EnqueueToggleTargetLock(primaryEntityId, sourceId, traceId);
        public RuntimeCommandValidationResult EnqueueSkillSlotInput(
            int slotIndex,
            int targetEntityId = 0,
            int sourceId = CharacterTestRuntimeCommandSources.SkillPanel,
            RuntimeTraceIdentity traceId = default) =>
            Commands.EnqueueSkillSlotInput(slotIndex, targetEntityId, sourceId, traceId);
        public RuntimeCommandValidationResult EnqueueCastAbilitySlot(
            int targetEntityId,
            int slotNumber,
            int abilityId,
            int sourceId = CharacterTestRuntimeCommandSources.PlayerInput,
            RuntimeTraceIdentity traceId = default) =>
            Commands.EnqueueCastAbilitySlot(targetEntityId, slotNumber, abilityId, sourceId, traceId);
        public RuntimeCommandValidationResult EnqueueSurfaceProbe(
            CharacterTestSurfaceProbeOperation operation,
            int targetEntityId = 0,
            int sourceId = CharacterTestRuntimeCommandSources.SurfaceProbePanel,
            RuntimeTraceIdentity traceId = default) =>
            Commands.EnqueueSurfaceProbe(operation, targetEntityId, sourceId, traceId);
        public RuntimeCommandValidationResult EnqueueMovableNaturalObjectReset(
            int targetNumericId = 0,
            int sourceId = CharacterTestRuntimeCommandSources.DebugUi,
            RuntimeTraceIdentity traceId = default) =>
            Commands.EnqueueMovableNaturalObjectReset(targetNumericId, sourceId, traceId);
        public RuntimeCommandValidationResult EnqueueMovableNaturalObjectPotencyInfluence(
            CharacterTestGenesisPotencyInfluenceKind influenceKind,
            int targetNumericId = 0,
            int sourceId = CharacterTestRuntimeCommandSources.DebugUi,
            RuntimeTraceIdentity traceId = default) =>
            Commands.EnqueueMovableNaturalObjectPotencyInfluence(
                influenceKind,
                targetNumericId,
                sourceId,
                traceId);
        public void SetCharacterMovementBasisProvider(ICharacterTestCharacterMovementBasisProvider provider) =>
            Presentation.SetCharacterMovementBasisProvider(provider);
        public void SetTargetLockMaxDistance(float maxDistance) =>
            Presentation.SetTargetLockMaxDistance(maxDistance);
        public void SetHeadPadDisplayMode(CharacterTestHeadPadDisplayMode displayMode) =>
            Presentation.SetHeadPadDisplayMode(displayMode);
        public void SetMovableNaturalObjectViewBridge(
            CharacterTestMovableNaturalObjectUnityViewBridge bridge) =>
            Presentation.SetMovableNaturalObjectViewBridge(bridge);
        public bool SetProceduralHitFeedbackEnabled(bool enabled) =>
            Presentation.SetProceduralHitFeedbackEnabled(enabled);
        public CharacterTestProceduralEffectPreviewResult TryApplyLockedTargetProceduralEffectPreview(
            CharacterTestProceduralEffectPreviewId effectId,
            string triggerName = "") =>
            Presentation.TryApplyLockedTargetProceduralEffectPreview(effectId, triggerName);
        public void TickPresentation(double deltaTime) => Presentation.TickPresentation(deltaTime);
        public SceneFlowResult RequestSceneLoad(
            string sceneKey,
            SceneFlowLoadMode loadMode = SceneFlowLoadMode.Single,
            bool unloadPreviousScene = false) =>
            StoryFlow.RequestSceneLoad(sceneKey, loadMode, unloadPreviousScene);
        public SceneFlowResult RequestSceneUnload(string sceneKey) => StoryFlow.RequestSceneUnload(sceneKey);
        public SceneFlowSnapshot CaptureSceneFlowSnapshot() => StoryFlow.CaptureSceneFlowSnapshot();
        public RuntimeCommandValidationResult RaiseTrigger(
            int triggerId,
            int sourceId = StoryRuntimeCommandSources.Input) =>
            StoryFlow.RaiseTrigger(triggerId, sourceId);
        public void SetNetworkDiagnosticsProvider(Func<CharacterTestNetworkLoopbackDiagnostics> provider) =>
            Network.SetNetworkDiagnosticsProvider(provider);
        public CharacterTestNetworkLoopbackDiagnostics CaptureNetworkLoopbackDiagnostics() =>
            Network.CaptureNetworkLoopbackDiagnostics();
        public FrameworkPerformanceCounterSnapshot CreateInteractionPerformanceCounterSnapshot() =>
            Modules.CreateInteractionPerformanceCounterSnapshot();
        internal FrameworkPerformanceCounterSnapshot CreateRuntimePerformanceCounterSnapshot() =>
            Modules.CreateRuntimePerformanceCounterSnapshot();
        public RuntimeHostDiagnostics CaptureDiagnostics() => Modules.Host.CaptureDiagnostics();
    }

    internal sealed class CharacterTestSliceModules
    {
        public RuntimeClock Clock { get; set; }
        public RuntimeHost Host { get; set; }
        public StoryDirector StoryDirector { get; set; }
        public StoryRuntimeModule StoryModule { get; set; }
        public GameplayTriggerRuntimeEventSource LevelGameplayTriggerEvents { get; set; }
        public CharacterTestLevelGameplayTriggerRuntimeComposition LevelGameplayTriggers { get; set; }
        public CharacterTestLevelGameplayTriggerRuntimeStatus LevelGameplayTriggerStatus { get; set; }
        public CharacterTestStoryContent StoryContent { get; set; }
        public GlobalResourceRuntimeServices Resources { get; set; }
        public CharacterTestStoryFlowBridge StoryFlowBridge { get; set; }
        internal TacticalCard.TacticalCardStageRuntime TacticalStage { get; set; }
        internal TacticalCard.TacticalMixedPlayerCardRuntime MixedPlayerTacticalStage { get; set; }
        public CharacterTestCharacterSpawnFlow CharacterSpawnFlow { get; set; }
        public AbilityRuntimeModule AbilityRuntime { get; set; }
        public CharacterTestSkillSlotRuntime SkillSlots { get; set; }
        public CharacterTestAbilityHotbarRuntime AbilityHotbar { get; set; }
        public GameplayBuffDefinitionRegistry BuffDefinitions { get; set; }
        public GameplayBuffPresentationDefinitionRegistry BuffPresentationDefinitions { get; set; }
        public CharacterTestCharacterRuntimeModule CharacterRuntime { get; set; }
        public CharacterTestNavigationSession NavigationSession { get; set; }
        public ICharacterTestAiNavigationService NavigationService { get; set; }
        public CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime RuntimeAiBehaviorLoadouts { get; set; }
        public CharacterTestRuntimeAiPlannerCommandModule RuntimeAiPlannerCommands { get; set; }
        public CharacterTestRuntimeCommandModule RuntimeCommands { get; set; }
        public CharacterTestLocalInputCommandModule LocalInputCommands { get; set; }
        public VfxRuntimeModule VfxRuntime { get; set; }
        public VfxRuntimeEventFanout VfxEventFanout { get; set; }
        public UnityVfxPresenter UnityVfxPresenter { get; set; }
        public CharacterTestBladeTrailBindingSource BladeTrailBindingSource { get; set; }
        public CombatProjectileUnityPresenter ProjectilePresenter { get; set; }
        public CharacterTestActionVfxBridgeModule ActionVfxBridge { get; set; }
        public CharacterTestLaserPathVfxBridgeModule LaserPathVfxBridge { get; set; }
        public CharacterTestChainLightningVfxBridgeModule ChainLightningVfxBridge { get; set; }
        public CharacterTestCombatVfxBridgeModule CombatVfxBridge { get; set; }
        public CharacterTestStatusVfxBridgeModule StatusVfxBridge { get; set; }
        public CharacterTestProceduralHitFeedbackModule ProceduralHitFeedback { get; set; }
        public CharacterTestVfxPresentationModule VfxPresentation { get; set; }
        public CharacterTestTraceRecorder TraceRecorder { get; set; }
        public SceneFlowController SceneFlowController { get; set; }
        public SceneFlowRuntimeModule SceneFlowModule { get; set; }
        public IAudioService AudioService { get; set; }
        public ICharacterTestAudioCueCatalog AudioCueCatalog { get; set; }
        public CharacterTestAudioCatalogStatus AudioCatalogStatus { get; set; }
        public bool DisposeAudioService { get; set; }
        public bool EnableCharacterSimulation { get; set; }
        public CharacterTestProceduralHitFeedbackOptions ProceduralHitFeedbackOptions { get; set; }
        private StoryDirector _storySnapshotSource;
        private StoryDirectorSnapshot _storySnapshot;
        private bool _storySnapshotDirty = true;

        public bool HasSceneFlow => SceneFlowController != null;
        public CharacterTestCharacterActionRuntime ActionRuntime => CharacterRuntime?.ActionRuntime;
        public CharacterTestCharacterCombatTraceSystem CombatTraceSystem => CharacterRuntime?.CombatTraceSystem;
        public CharacterTestCharacterMovementProfileRuntime MovementProfile =>
            CharacterRuntime != null
                ? CharacterRuntime.MovementProfile
                : CharacterTestCharacterMovementProfileSource.Load(null, null);
        public CharacterTestCharacterPhysicsRuntime PhysicsRuntime => CharacterRuntime?.PhysicsRuntime;
        public CharacterTestInteractionDebugSnapshot InteractionDebugSnapshot =>
            CharacterRuntime != null
                ? CharacterRuntime.InteractionDebugSnapshot
                : CharacterTestInteractionDebugSnapshot.Empty;
        public IReadOnlyList<CharacterTestInteractionEvent> LastFrameInteractionEvents =>
            CharacterRuntime != null
                ? CharacterRuntime.LastFrameInteractionEvents
                : Array.Empty<CharacterTestInteractionEvent>();
        public CharacterTestInteractionPhysicsSummary InteractionPhysicsSummary =>
            CharacterRuntime != null
                ? CharacterRuntime.InteractionPhysicsSummary
                : CharacterTestInteractionPhysicsSummary.Empty;
        public CharacterTestInteractionVfxSummary InteractionVfxSummary =>
            CharacterRuntime != null
                ? CharacterRuntime.InteractionVfxSummary
                : CharacterTestInteractionVfxSummary.Empty;
        public CharacterTestInteractionAudioSummary InteractionAudioSummary =>
            CharacterRuntime != null
                ? CharacterRuntime.InteractionAudioSummary
                : CharacterTestInteractionAudioSummary.Empty;
        public CharacterTestPhysicalAudioSummary PhysicalAudioSummary =>
            CharacterRuntime != null
                ? CharacterRuntime.PhysicalAudioSummary
                : CharacterTestPhysicalAudioSummary.Empty;
        public ContinuousPhysicalAudioSnapshot ContinuousPhysicalAudioSnapshot =>
            CharacterRuntime?.ContinuousPhysicalAudioSnapshot ?? default;
        public CharacterTestAudioIntentDrainSummary AudioIntentDrainSummary =>
            CharacterRuntime != null
                ? CharacterRuntime.AudioIntentDrainSummary
                : CharacterTestAudioIntentDrainSummary.Empty;
        internal CharacterTestBodyPartContactDebugSnapshot BodyPartContactDebugSnapshot =>
            CharacterRuntime != null
                ? CharacterRuntime.BodyPartContactDebugSnapshot
                : CharacterTestBodyPartContactDebugSnapshot.Empty;
        public CharacterTestInteractionRenderingSummary InteractionRenderingSummary =>
            CharacterRuntime != null
                ? CharacterRuntime.InteractionRenderingSummary
                : CharacterTestInteractionRenderingSummary.Empty;
        public CharacterTestInteractionConsumerDispatchSummary InteractionConsumerSummary =>
            CharacterRuntime != null
                ? CharacterRuntime.InteractionConsumerSummary
                : CharacterTestInteractionConsumerDispatchSummary.Empty;
        public CharacterTestInteractionPerformanceCounterSource InteractionPerformanceCounterSource =>
            CharacterRuntime != null ? CharacterRuntime.InteractionPerformanceCounterSource : null;
        internal CharacterTestRuntimePerformanceCounterSource RuntimePerformanceCounterSource =>
            CharacterRuntime != null ? CharacterRuntime.RuntimePerformanceCounterSource : null;
        public FrameworkPerformanceCounterSnapshot CreateInteractionPerformanceCounterSnapshot()
        {
            return CharacterRuntime != null
                ? CharacterRuntime.CreateInteractionPerformanceCounterSnapshot()
                : CharacterTestInteractionPerformanceCounterSource.Capture(
                    0,
                    0,
                    CharacterTestInteractionConsumerDispatchSummary.Empty,
                    CharacterTestInteractionPhysicsSummary.Empty,
                    CharacterTestInteractionVfxSummary.Empty,
                    CharacterTestInteractionAudioSummary.Empty,
                    CharacterTestInteractionRenderingSummary.Empty);
        }
        internal FrameworkPerformanceCounterSnapshot CreateRuntimePerformanceCounterSnapshot()
        {
            return CharacterRuntime != null
                ? CharacterRuntime.CreateRuntimePerformanceCounterSnapshot()
                : CharacterTestRuntimePerformanceCounterSource.Capture(
                    0,
                    0,
                    CharacterTestCharacterSpawnPerformanceSummary.Empty,
                    CharacterTestCharacterViewSyncPerformanceSummary.Empty,
                    default,
                    0,
                    0,
                    0);
        }
        public CharacterTestCharacterActionReloadResult? LastActionReloadResult =>
            CharacterRuntime?.LastActionReloadResult;
        public string LastGenesisMovableObjectSummary =>
            CharacterRuntime != null ? CharacterRuntime.LastGenesisMovableObjectSummary : string.Empty;
        public CharacterTestGenesisMovableNaturalObjectValidation LastGenesisMovableObjectValidation =>
            CharacterRuntime != null
                ? CharacterRuntime.LastGenesisMovableObjectValidation
                : CharacterTestGenesisMovableNaturalObjectValidation.Empty;
        public string LastGenesisPotencyMechanicsSummary =>
            CharacterRuntime != null ? CharacterRuntime.LastGenesisPotencyMechanicsSummary : string.Empty;
        public CharacterTestGenesisPotencyMechanicsValidation LastGenesisPotencyMechanicsValidation =>
            CharacterRuntime != null
                ? CharacterRuntime.LastGenesisPotencyMechanicsValidation
                : CharacterTestGenesisPotencyMechanicsValidation.Empty;
        public string LastGenesisLivingBeingSummary =>
            CharacterRuntime != null ? CharacterRuntime.LastGenesisLivingBeingSummary : string.Empty;
        public CharacterTestGenesisLivingBeingValidation LastGenesisLivingBeingValidation =>
            CharacterRuntime != null
                ? CharacterRuntime.LastGenesisLivingBeingValidation
                : CharacterTestGenesisLivingBeingValidation.Empty;
        public string LastGenesisLivingNaturalAttributesSummary =>
            CharacterRuntime != null ? CharacterRuntime.LastGenesisLivingNaturalAttributesSummary : string.Empty;
        public CharacterTestGenesisLivingNaturalAttributesValidation LastGenesisLivingNaturalAttributesValidation =>
            CharacterRuntime != null
                ? CharacterRuntime.LastGenesisLivingNaturalAttributesValidation
                : CharacterTestGenesisLivingNaturalAttributesValidation.Empty;
        public string LastGenesisLivingNaturalAttributesRuntimeStateSummary =>
            CharacterRuntime != null
                ? CharacterRuntime.LastGenesisLivingNaturalAttributesRuntimeStateSummary
                : string.Empty;
        public CharacterTestHeadPadDisplayMode HeadPadDisplayMode => CharacterRuntime.HeadPadDisplayMode;
        public IReadOnlyList<CharacterTestRuntimeEvent> RecentRuntimeEvents =>
            RuntimeCommands != null ? RuntimeCommands.RecentEvents : Array.Empty<CharacterTestRuntimeEvent>();
        public RuntimeEventQueueSnapshot RuntimeEventQueueSnapshot =>
            RuntimeCommands != null ? RuntimeCommands.EventQueueSnapshot : default;
        public int PendingRuntimeCommandCount =>
            RuntimeCommands != null ? RuntimeCommands.PendingCommandCount : 0;
        public StoryDirectorSnapshot StorySnapshot
        {
            get
            {
                StoryDirector source = StoryDirector;
                if (source == null)
                {
                    _storySnapshotSource = null;
                    _storySnapshot = null;
                    _storySnapshotDirty = true;
                    return default;
                }

                if (_storySnapshotDirty || _storySnapshot == null || !ReferenceEquals(_storySnapshotSource, source))
                {
                    _storySnapshotSource = source;
                    _storySnapshot = source.CreateSnapshot();
                    _storySnapshotDirty = false;
                    StorySnapshotCreateCountForTests++;
                }

                return _storySnapshot;
            }
        }

        internal int StorySnapshotCreateCountForTests { get; private set; }

        internal void InvalidateStorySnapshot()
        {
            _storySnapshotDirty = true;
        }

        public IReadOnlyList<CharacterTestCharacterEntity> CharacterEntities =>
            CharacterRuntime != null ? CharacterRuntime.Entities : Array.Empty<CharacterTestCharacterEntity>();
        public IReadOnlyList<CharacterTestMovableNaturalObjectState> MovableNaturalObjects =>
            CharacterRuntime != null
                ? CharacterRuntime.MovableNaturalObjects
                : Array.Empty<CharacterTestMovableNaturalObjectState>();
        public CharacterTestMovableNaturalObjectSummary MovableNaturalObjectSummary =>
            CharacterRuntime != null
                ? CharacterRuntime.MovableNaturalObjectSummary
                : CharacterTestMovableNaturalObjectSummary.Empty;
    }

    internal sealed class CharacterTestSliceCommandServices
    {
        private static readonly ProfilerMarker EnqueueRuntimeCommandMarker =
            new ProfilerMarker("CharacterTest.Commands.EnqueueRuntimeCommand");
        private static readonly ProfilerMarker EnqueueRuntimeCommandThrowIfDisposedMarker =
            new ProfilerMarker("CharacterTest.Commands.EnqueueRuntimeCommand.ThrowIfDisposed");
        private static readonly ProfilerMarker EnqueueRuntimeCommandCustomSubmitterMarker =
            new ProfilerMarker("CharacterTest.Commands.EnqueueRuntimeCommand.CustomSubmitter");
        private static readonly ProfilerMarker EnqueueRuntimeCommandRuntimeModuleMarker =
            new ProfilerMarker("CharacterTest.Commands.EnqueueRuntimeCommand.RuntimeModule");
        private static readonly ProfilerMarker EnqueueAuthoritativeRuntimeCommandMarker =
            new ProfilerMarker("CharacterTest.Commands.EnqueueAuthoritativeRuntimeCommand");

        private readonly CharacterTestSliceModules _modules;
        private readonly IRuntimeLogger _logger;
        private readonly Func<RuntimeFrame> _currentFrameProvider;
        private readonly Action _throwIfDisposed;
        private Func<RuntimeCommand, RuntimeCommandValidationResult> _runtimeCommandSubmitter;

        public CharacterTestSliceCommandServices(
            CharacterTestSliceModules modules,
            IRuntimeLogger logger,
            Func<RuntimeFrame> currentFrameProvider,
            Action throwIfDisposed)
        {
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _logger = logger ?? NullRuntimeLogger.Instance;
            _currentFrameProvider = currentFrameProvider ?? (() => RuntimeFrame.Zero);
            _throwIfDisposed = throwIfDisposed ?? (() => { });
        }

        public RuntimeCommandValidationResult EnqueueRuntimeCommand(RuntimeCommand command)
        {
            using (EnqueueRuntimeCommandMarker.Auto())
            {
                using (EnqueueRuntimeCommandThrowIfDisposedMarker.Auto())
                {
                    _throwIfDisposed();
                }

                if (_runtimeCommandSubmitter != null)
                {
                    using (EnqueueRuntimeCommandCustomSubmitterMarker.Auto())
                    {
                        return _runtimeCommandSubmitter(command);
                    }
                }

                using (EnqueueRuntimeCommandRuntimeModuleMarker.Auto())
                {
                    return _modules.RuntimeCommands.Enqueue(command);
                }
            }
        }

        /// <summary>
        /// Enqueues a command produced by the authoritative simulation without treating it as
        /// input submitted by the connected network peer.
        /// </summary>
        /// <remarks>NoAlloc while the runtime command buffer remains within its initialized capacity.</remarks>
        public RuntimeCommandValidationResult EnqueueAuthoritativeRuntimeCommand(RuntimeCommand command)
        {
            using (EnqueueAuthoritativeRuntimeCommandMarker.Auto())
            {
                _throwIfDisposed();
                return _modules.RuntimeCommands.Enqueue(command);
            }
        }

        public void SetLocalInputProvider(IInputProvider provider)
        {
            _throwIfDisposed();
            _modules.LocalInputCommands?.SetInputProvider(provider);
        }

        public void SetRuntimeCommandSubmitter(Func<RuntimeCommand, RuntimeCommandValidationResult> submitter)
        {
            _runtimeCommandSubmitter = submitter;
        }

        public CharacterTestCharacterActionReloadResult ReloadCharacterActionConfig()
        {
            return ReloadCharacterActionConfig(null);
        }

        public CharacterTestCharacterActionReloadResult ReloadCharacterActionConfig(
            CharacterTestCharacterActionConfigSourceOptions options)
        {
            _throwIfDisposed();
            if (_modules.CharacterRuntime == null)
            {
                return CharacterTestCharacterActionReloadResult.Rejected(
                    null,
                    CurrentFrame,
                    "Action config reload rejected: character runtime module is missing.");
            }

            return options == null
                ? _modules.CharacterRuntime.ReloadActionConfig(CurrentFrame)
                : _modules.CharacterRuntime.ReloadActionConfig(CurrentFrame, options);
        }

        public RuntimeCommandValidationResult EnqueueSetCameraMode(
            CharacterTestCameraMode mode,
            int sourceId = CharacterTestRuntimeCommandSources.System,
            RuntimeTraceIdentity traceId = default)
        {
            _throwIfDisposed();
            if (traceId.IsNone) traceId = new RuntimeTraceIdentity(SliceTraceKinds.CameraMode, sourceId, CurrentFrame.Value);
            return EnqueueRuntimeCommand(CharacterTestRuntimeCommandFactory.SetCameraMode(
                CurrentFrame,
                sourceId,
                mode,
                traceId));
        }

        public RuntimeCommandValidationResult EnqueueToggleTargetLock(
            int primaryEntityId,
            int sourceId = CharacterTestRuntimeCommandSources.CameraInput,
            RuntimeTraceIdentity traceId = default)
        {
            _throwIfDisposed();
            if (traceId.IsNone) traceId = new RuntimeTraceIdentity(SliceTraceKinds.TargetLockToggle, sourceId, CurrentFrame.Value);
            return EnqueueRuntimeCommand(CharacterTestRuntimeCommandFactory.ToggleTargetLock(
                CurrentFrame,
                sourceId,
                primaryEntityId,
                traceId));
        }

        public RuntimeCommandValidationResult EnqueueSkillSlotInput(
            int slotIndex,
            int targetEntityId = 0,
            int sourceId = CharacterTestRuntimeCommandSources.SkillPanel,
            RuntimeTraceIdentity traceId = default)
        {
            _throwIfDisposed();
            if (traceId.IsNone) traceId = new RuntimeTraceIdentity(SliceTraceKinds.SkillPanelSlot, sourceId, CurrentFrame.Value);
            if (!CharacterTestSkillSlotRuntime.IsValidSlotIndex(slotIndex))
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidPayload,
                    default,
                    CurrentFrame,
                    "Skill slot index is invalid."));
            }

            CharacterTestCharacterEntity target = targetEntityId > 0
                ? FindCharacterEntity(targetEntityId)
                : FindLocalPlayerEntity();
            if (target == null)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidPayload,
                    default,
                    CurrentFrame,
                    "Local CharacterTest player was not found."));
            }

            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetControlIntent(
                CurrentFrame,
                sourceId,
                target.Id.Value,
                target.Input.Move,
                target.Input.HasWorldMove ? target.Input.WorldMove : target.Input.Move,
                jumpPressed: false,
                jumpHeld: target.Input.JumpHeld,
                jumpReleased: false,
                attackPrimaryPressed: false,
                attackSecondaryPressed: false,
                debugSecondaryPressed: false,
                debugPrimaryPressed: false,
                traceId: traceId,
                skillSlotPressedMask: CharacterTestSkillSlotRuntime.ToMask(slotIndex));
            return EnqueueRuntimeCommand(command);
        }

        public RuntimeCommandValidationResult EnqueueCastAbilitySlot(
            int targetEntityId,
            int slotNumber,
            int abilityId,
            int sourceId = CharacterTestRuntimeCommandSources.PlayerInput,
            RuntimeTraceIdentity traceId = default)
        {
            _throwIfDisposed();
            if (traceId.IsNone) traceId = new RuntimeTraceIdentity(SliceTraceKinds.AbilitySlot, sourceId, CurrentFrame.Value);
            return EnqueueRuntimeCommand(CharacterTestRuntimeCommandFactory.CastAbilitySlot(
                CurrentFrame,
                sourceId,
                targetEntityId,
                slotNumber,
                abilityId,
                traceId));
        }

        public RuntimeCommandValidationResult EnqueueSurfaceProbe(
            CharacterTestSurfaceProbeOperation operation,
            int targetEntityId = 0,
            int sourceId = CharacterTestRuntimeCommandSources.SurfaceProbePanel,
            RuntimeTraceIdentity traceId = default)
        {
            _throwIfDisposed();
            if (traceId.IsNone) traceId = new RuntimeTraceIdentity(SliceTraceKinds.SurfaceProbe, sourceId, CurrentFrame.Value, (int)operation);
            CharacterTestCharacterEntity target = targetEntityId > 0
                ? FindCharacterEntity(targetEntityId)
                : FindLocalPlayerEntity();
            if (target == null)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidPayload,
                    default,
                    CurrentFrame,
                    "Surface probe target CharacterTest player was not found."));
            }

            return EnqueueRuntimeCommand(CharacterTestRuntimeCommandFactory.SurfaceProbe(
                CurrentFrame,
                sourceId,
                target.Id.Value,
                operation,
                traceId));
        }

        public RuntimeCommandValidationResult EnqueueMovableNaturalObjectReset(
            int targetNumericId = 0,
            int sourceId = CharacterTestRuntimeCommandSources.DebugUi,
            RuntimeTraceIdentity traceId = default)
        {
            _throwIfDisposed();
            if (traceId.IsNone) traceId = new RuntimeTraceIdentity(SliceTraceKinds.MovableReset, sourceId, CurrentFrame.Value);
            return EnqueueRuntimeCommand(CharacterTestRuntimeCommandFactory.MovableNaturalObjectReset(
                CurrentFrame,
                sourceId,
                targetNumericId,
                traceId));
        }

        public RuntimeCommandValidationResult EnqueueMovableNaturalObjectPotencyInfluence(
            CharacterTestGenesisPotencyInfluenceKind influenceKind,
            int targetNumericId = 0,
            int sourceId = CharacterTestRuntimeCommandSources.DebugUi,
            RuntimeTraceIdentity traceId = default)
        {
            _throwIfDisposed();
            if (traceId.IsNone) traceId = new RuntimeTraceIdentity(SliceTraceKinds.MovablePotency, sourceId, CurrentFrame.Value);
            return EnqueueRuntimeCommand(CharacterTestRuntimeCommandFactory.MovableNaturalObjectPotencyInfluence(
                CurrentFrame,
                sourceId,
                targetNumericId,
                influenceKind,
                traceId));
        }

        public CharacterTestCharacterEntity FindLocalPlayerEntity()
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _modules.CharacterEntities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entities[i];
            }

            return null;
        }

        public CharacterTestCharacterEntity FindCharacterEntity(int entityId)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _modules.CharacterEntities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id.Value == entityId)
                    return entities[i];
            }

            return null;
        }

        private RuntimeFrame CurrentFrame => _currentFrameProvider();
    }

    internal sealed class CharacterTestSlicePresentationServices
    {
        internal CharacterTestPresentationLod PresentationLod => _modules.CharacterRuntime?.ViewBridge.PresentationLod;
        private const int ProceduralEffectPreviewSnapshotRefreshBudgetFrames = 12;

        private readonly CharacterTestSliceModules _modules;
        private readonly CharacterTestSliceCommandServices _commands;
        private readonly IRuntimeLogger _logger;
        private readonly Func<RuntimeFrame> _currentFrameProvider;
        private readonly Action _throwIfDisposed;
        private CharacterTestMovableNaturalObjectUnityViewBridge _movableNaturalObjectViewBridge;
        private CharacterTestProceduralEffectPreviewResult _lastProceduralEffectPreviewResult =
            CharacterTestProceduralEffectPreviewResult.Empty;
        private int _proceduralEffectPreviewSnapshotRefreshFramesRemaining;

        public CharacterTestSlicePresentationServices(
            CharacterTestSliceModules modules,
            CharacterTestSliceCommandServices commands,
            IRuntimeLogger logger,
            Func<RuntimeFrame> currentFrameProvider,
            Action throwIfDisposed)
        {
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _logger = logger ?? NullRuntimeLogger.Instance;
            _currentFrameProvider = currentFrameProvider ?? (() => RuntimeFrame.Zero);
            _throwIfDisposed = throwIfDisposed ?? (() => { });
        }

        public bool ProceduralHitFeedbackEnabled =>
            _modules.ProceduralHitFeedback != null && _modules.ProceduralHitFeedback.Enabled;
        public CharacterTestProceduralHitFeedbackResult LastProceduralHitFeedbackResult =>
            _modules.ProceduralHitFeedback != null
                ? _modules.ProceduralHitFeedback.LastResult
                : CharacterTestProceduralHitFeedbackResult.Empty;
        public CharacterTestProceduralEffectPreviewResult LastProceduralEffectPreviewResult =>
            _lastProceduralEffectPreviewResult;

        public void SetCharacterMovementBasisProvider(ICharacterTestCharacterMovementBasisProvider provider)
        {
            _modules.CharacterRuntime?.SetMovementBasisProvider(provider);
        }

        public void SetTargetLockMaxDistance(float maxDistance)
        {
            _modules.RuntimeCommands?.SetTargetLockMaxDistance(maxDistance);
        }

        public void SetHeadPadDisplayMode(CharacterTestHeadPadDisplayMode displayMode)
        {
            _throwIfDisposed();
            _modules.CharacterRuntime.SetHeadPadDisplayMode(displayMode);
        }

        public void SetMovableNaturalObjectViewBridge(
            CharacterTestMovableNaturalObjectUnityViewBridge bridge)
        {
            _movableNaturalObjectViewBridge = bridge;
        }

        public bool SetProceduralHitFeedbackEnabled(bool enabled)
        {
            _throwIfDisposed();
            if (_modules.ProceduralHitFeedback == null)
                return false;

            _modules.ProceduralHitFeedback.SetEnabled(enabled);
            return true;
        }

        public CharacterTestProceduralEffectPreviewResult TryApplyLockedTargetProceduralEffectPreview(
            CharacterTestProceduralEffectPreviewId effectId,
            string triggerName = "")
        {
            _throwIfDisposed();
            RuntimeFrame frame = CurrentFrame;
            if (effectId == CharacterTestProceduralEffectPreviewId.None)
                return StoreProceduralEffectPreviewResult(new CharacterTestProceduralEffectPreviewResult(
                    effectId,
                    triggerName,
                    frame,
                    0,
                    accepted: false,
                    "No procedural effect preview was requested.",
                    CharacterAuthorityPoseSnapshot.Empty()));

            bool usingSelfFallback = false;
            int targetEntityId = _modules.RuntimeCommands.TargetLockEnabled
                ? _modules.RuntimeCommands.LockedTargetEntityId
                : 0;
            CharacterTestCharacterEntity target = targetEntityId > 0
                ? _commands.FindCharacterEntity(targetEntityId)
                : null;
            if (target == null && (!_modules.RuntimeCommands.TargetLockEnabled
                || _modules.RuntimeCommands.LockedTargetEntityId <= 0))
            {
                target = _commands.FindLocalPlayerEntity();
                targetEntityId = target != null ? target.Id.Value : 0;
                usingSelfFallback = target != null;
            }

            if (target == null)
            {
                bool missingLockedTarget = _modules.RuntimeCommands.TargetLockEnabled
                    && _modules.RuntimeCommands.LockedTargetEntityId > 0;
                return StoreProceduralEffectPreviewResult(new CharacterTestProceduralEffectPreviewResult(
                    effectId,
                    triggerName,
                    frame,
                    targetEntityId,
                    accepted: false,
                    missingLockedTarget
                        ? "Locked target entity was not found."
                        : "No locked target or local player is available for procedural effect preview.",
                    CharacterAuthorityPoseSnapshot.Empty(missingLockedTarget
                        ? CharacterAuthorityPoseIssue.LockedTargetNotFound
                        : CharacterAuthorityPoseIssue.TargetAndLocalPlayerUnavailable)));
            }

            RuntimeCommandValidationResult enqueueResult = _commands.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.ApplyAuthorityPosePreview(
                    frame,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    targetEntityId,
                    effectId,
                    new RuntimeTraceIdentity(SliceTraceKinds.AuthorityPosePreview, targetEntityId, frame.Value, (int)effectId)));
            _modules.CharacterRuntime.TryGetAuthorityPoseSnapshot(
                targetEntityId,
                out CharacterAuthorityPoseSnapshot snapshot);
            if (!enqueueResult.Success)
                return StoreProceduralEffectPreviewResult(new CharacterTestProceduralEffectPreviewResult(
                    effectId,
                    triggerName,
                    frame,
                    targetEntityId,
                    accepted: false,
                    enqueueResult.Error.Message,
                    snapshot));

            string message = "Authority pose preview command queued.";
            if (usingSelfFallback)
                message += " Target fallback: local player self.";
            return StoreProceduralEffectPreviewResult(new CharacterTestProceduralEffectPreviewResult(
                effectId,
                triggerName,
                frame,
                targetEntityId,
                accepted: true,
                message,
                snapshot));
        }

        public void TickPresentation(double deltaTime)
        {
            _throwIfDisposed();
            _modules.CharacterRuntime?.ViewBridge.PresentationLod.BeginPresentation();
            _modules.CharacterRuntime?.TickPresentation(deltaTime < 0d ? 0d : deltaTime, CurrentFrame);
            _modules.UnityVfxPresenter?.SyncActiveAnchors();
            _modules.UnityVfxPresenter?.TickPresentationBackends((float)Math.Max(0d, deltaTime));
            _modules.ProjectilePresenter?.TickRetiringViews();

            _movableNaturalObjectViewBridge?.Sync(_modules.MovableNaturalObjects);
        }

        internal bool TrySetWeaponBladeTrailStyleOverride(int entityId, int slotId, MxFramework.CharacterApplication.WeaponBladeTrailStyle style)
            => _modules.BladeTrailBindingSource?.TrySetStyleOverride(entityId, slotId, style) == true;

        internal bool ClearWeaponBladeTrailStyleOverride(int entityId, int slotId)
            => _modules.BladeTrailBindingSource?.ClearStyleOverride(entityId, slotId) == true;

        public void RefreshProceduralEffectPreviewSnapshot()
        {
            CharacterTestProceduralEffectPreviewResult result = _lastProceduralEffectPreviewResult;
            if (!result.HasResult || !result.Accepted || result.TargetEntityId <= 0)
                return;
            if (_proceduralEffectPreviewSnapshotRefreshFramesRemaining <= 0)
                return;

            if (!_modules.CharacterRuntime.TryGetAuthorityPoseSnapshot(
                    result.TargetEntityId,
                    out CharacterAuthorityPoseSnapshot snapshot))
            {
                _proceduralEffectPreviewSnapshotRefreshFramesRemaining = 0;
                return;
            }

            _lastProceduralEffectPreviewResult = new CharacterTestProceduralEffectPreviewResult(
                result.EffectId,
                result.TriggerName,
                result.Frame,
                result.TargetEntityId,
                accepted: true,
                result.Message,
                snapshot);
            _proceduralEffectPreviewSnapshotRefreshFramesRemaining--;
        }

        private CharacterTestProceduralEffectPreviewResult StoreProceduralEffectPreviewResult(
            CharacterTestProceduralEffectPreviewResult result)
        {
            _lastProceduralEffectPreviewResult = result;
            _proceduralEffectPreviewSnapshotRefreshFramesRemaining =
                result.HasResult && result.Accepted && result.TargetEntityId > 0
                    ? ProceduralEffectPreviewSnapshotRefreshBudgetFrames
                    : 0;
            return result;
        }

        private RuntimeFrame CurrentFrame => _currentFrameProvider();
    }

    internal sealed class CharacterTestSliceStoryFlowServices
    {
        private readonly CharacterTestSliceModules _modules;
        private readonly IRuntimeLogger _logger;
        private readonly RuntimeLogBuffer _logBuffer;
        private readonly Func<RuntimeFrame> _currentFrameProvider;
        private readonly Action _throwIfDisposed;

        public CharacterTestSliceStoryFlowServices(
            CharacterTestSliceModules modules,
            IRuntimeLogger logger,
            RuntimeLogBuffer logBuffer,
            Func<RuntimeFrame> currentFrameProvider,
            Action throwIfDisposed)
        {
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _logger = logger ?? NullRuntimeLogger.Instance;
            _logBuffer = logBuffer ?? new RuntimeLogBuffer(160);
            _currentFrameProvider = currentFrameProvider ?? (() => RuntimeFrame.Zero);
            _throwIfDisposed = throwIfDisposed ?? (() => { });
        }

        public void BootstrapStorySession(Action<double> tick)
        {
            StoryLoadGraphResult load = _modules.StoryDirector.TryLoadGraph(_modules.StoryContent.Graph);
            if (!load.Success)
            {
                _logBuffer.Clear().Append("CharacterTest graph load failed: ").Append(load.Message);
                _logger.Warning("Story", _logBuffer);
                return;
            }

            _logBuffer.Clear()
                .Append("CharacterTest graph loaded. graphId=")
                .Append(_modules.StoryContent.GraphId);
            _logger.Info("Story", _logBuffer);

            RuntimeCommandValidationResult enter = _modules.StoryModule.CommandBuffer.Enqueue(
                StoryRuntimeCommandFactory.RequestEnterBeat(
                    RuntimeFrame.Zero,
                    StoryRuntimeCommandSources.System,
                    _modules.StoryContent.GraphId,
                    _modules.StoryContent.EntryBeatId,
                    traceId: new RuntimeTraceIdentity(SliceTraceKinds.Bootstrap)));
            if (!enter.Success)
            {
                _logBuffer.Clear().Append("CharacterTest entry beat enqueue failed: ").Append(enter.Error.Message);
                _logger.Warning("Story", _logBuffer);
                return;
            }

            _logBuffer.Clear()
                .Append("Entry beat enqueued. beatId=")
                .Append(_modules.StoryContent.EntryBeatId);
            _logger.Info("Story", _logBuffer);
            tick?.Invoke(0d);
        }

        public void OnStoryDirectorEvent(StoryEvent evt)
        {
            _modules.InvalidateStorySnapshot();

            if (evt.Kind != StoryEventKind.StepStarted)
                return;

            if (!_modules.StoryContent.TryResolveStepText(evt.StepId, out string text))
                return;

            _logger.Info("Story", text);
        }

        public SceneFlowResult RequestSceneLoad(
            string sceneKey,
            SceneFlowLoadMode loadMode = SceneFlowLoadMode.Single,
            bool unloadPreviousScene = false)
        {
            _throwIfDisposed();
            if (_modules.SceneFlowController == null)
            {
                return SceneFlowResult.Failed(
                    sceneKey,
                    SceneFlowOperationType.Load,
                    new SceneFlowError(
                        SceneFlowErrorCode.DriverFailure,
                        sceneKey,
                        "CharacterTest scene flow is not available. Use Offline resource mode with a sceneAssetBundle catalog."));
            }

            return _modules.SceneFlowController.RequestLoad(
                new SceneFlowRequest(sceneKey, loadMode, unloadPreviousScene));
        }

        public SceneFlowResult RequestSceneUnload(string sceneKey)
        {
            _throwIfDisposed();
            if (_modules.SceneFlowController == null)
            {
                return SceneFlowResult.Failed(
                    sceneKey,
                    SceneFlowOperationType.Unload,
                    new SceneFlowError(
                        SceneFlowErrorCode.DriverFailure,
                        sceneKey,
                        "CharacterTest scene flow is not available. Use Offline resource mode with a sceneAssetBundle catalog."));
            }

            return _modules.SceneFlowController.RequestUnload(sceneKey);
        }

        public SceneFlowSnapshot CaptureSceneFlowSnapshot()
        {
            _throwIfDisposed();
            return _modules.SceneFlowController?.CaptureSnapshot();
        }

        public RuntimeCommandValidationResult RaiseTrigger(
            int triggerId,
            int sourceId = StoryRuntimeCommandSources.Input)
        {
            _throwIfDisposed();
            var command = StoryRuntimeCommandFactory.RaiseTrigger(
                _currentFrameProvider(),
                sourceId,
                triggerId,
                traceId: new RuntimeTraceIdentity(SliceTraceKinds.Trigger, sourceId, _currentFrameProvider().Value, triggerId));
            RuntimeCommandValidationResult result = _modules.StoryModule.CommandBuffer.Enqueue(command);
            if (result.Success)
            {
                _logBuffer.Clear()
                    .Append("RaiseTrigger enqueued. triggerId=")
                    .Append(triggerId)
                    .Append(", frame=")
                    .Append(_currentFrameProvider().Value);
                _logger.Info("GameSlice", _logBuffer);
            }
            else
            {
                _logBuffer.Clear()
                    .Append("RaiseTrigger rejected. triggerId=")
                    .Append(triggerId)
                    .Append(", error=")
                    .Append(result.Error.Message);
                _logger.Warning("GameSlice", _logBuffer);
            }

            return result;
        }
    }

    internal sealed class CharacterTestSliceNetworkServices
    {
        private readonly CharacterTestSliceModules _modules;
        private readonly Func<double> _elapsedSecondsProvider;
        private readonly Action<RuntimeFrame, double> _restoreRuntimeState;
        private readonly Action _throwIfDisposed;
        private Func<CharacterTestNetworkLoopbackDiagnostics> _networkDiagnosticsProvider;
        private bool _networkRollbackReplayActive;

        internal bool IsNetworkRollbackReplayActive => _networkRollbackReplayActive;

        public CharacterTestSliceNetworkServices(
            CharacterTestSliceModules modules,
            Func<double> elapsedSecondsProvider,
            Action<RuntimeFrame, double> restoreRuntimeState,
            Action throwIfDisposed)
        {
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _elapsedSecondsProvider = elapsedSecondsProvider ?? (() => 0d);
            _restoreRuntimeState = restoreRuntimeState ?? ((_, _) => { });
            _throwIfDisposed = throwIfDisposed ?? (() => { });
        }

        public void SetNetworkDiagnosticsProvider(Func<CharacterTestNetworkLoopbackDiagnostics> provider)
        {
            _networkDiagnosticsProvider = provider;
        }

        public CharacterTestNetworkLoopbackDiagnostics CaptureNetworkLoopbackDiagnostics()
        {
            return _networkDiagnosticsProvider != null
                ? _networkDiagnosticsProvider()
                : CharacterTestNetworkLoopbackDiagnostics.Unavailable;
        }

        public CharacterTestNetworkSnapshotParticipant CreateSnapshotParticipant(
            Func<string, int> controlledEntityProvider = null,
            Func<CharacterTestNetworkOwnershipEntry[]> ownershipProvider = null)
        {
            return new CharacterTestNetworkSnapshotParticipant(
                _modules.CharacterRuntime,
                _modules.RuntimeCommands,
                controlledEntityProvider,
                ownershipProvider,
                _modules.AbilityRuntime,
                _modules.RuntimeAiPlannerCommands,
                _throwIfDisposed);
        }

        public CharacterTestNetworkRollbackParticipant CreateRollbackParticipant(
            CharacterTestNetworkSnapshotParticipant snapshotParticipant)
        {
            return new CharacterTestNetworkRollbackParticipant(this, snapshotParticipant);
        }

        public CharacterTestNetworkRollbackState CaptureRollbackState(
            RuntimeFrame frame,
            CharacterTestNetworkSnapshotParticipant snapshotParticipant)
        {
            _throwIfDisposed();
            return new CharacterTestNetworkRollbackState(
                frame,
                _elapsedSecondsProvider(),
                snapshotParticipant.CaptureRollbackDocument(frame));
        }

        public bool RestoreRollbackState(
            RuntimeFrame frame,
            CharacterTestNetworkRollbackState rollbackState,
            CharacterTestNetworkSnapshotParticipant snapshotParticipant,
            out string failure)
        {
            failure = string.Empty;
            if (rollbackState == null)
            {
                failure = "CharacterTest rollback state type mismatch.";
                return false;
            }

            NetworkSnapshotApplyResult fixedAssemblyPreflight = snapshotParticipant.PreflightFixedAssemblyRollback(rollbackState.Document);
            if (!fixedAssemblyPreflight.Success) { failure = fixedAssemblyPreflight.Message; return false; }
            _restoreRuntimeState(frame, rollbackState.ElapsedSeconds);
            NetworkSnapshotApplyResult result = snapshotParticipant.ApplyRollbackDocument(
                frame,
                rollbackState.Document.CloneForRollback(),
                preserveLocalPredictedEntity: false);
            if (result.Success)
                return true;

            failure = result.Message;
            return false;
        }

        public void RestoreNetworkRollbackRuntimeState(RuntimeFrame frame, double elapsedSeconds)
        {
            _restoreRuntimeState(frame, elapsedSeconds);
        }

        public void SetNetworkRollbackReplayActive(bool active)
        {
            if (active) _throwIfDisposed();
            _networkRollbackReplayActive = active;
            _modules.LocalInputCommands?.SetSuppressed(active);
        }
    }

    internal sealed class CharacterTestNetworkRollbackState
    {
        public CharacterTestNetworkRollbackState(
            RuntimeFrame frame,
            double elapsedSeconds,
            CharacterTestNetworkSnapshotDocument document)
        {
            Frame = frame;
            ElapsedSeconds = elapsedSeconds;
            Document = document ?? new CharacterTestNetworkSnapshotDocument();
        }

        public RuntimeFrame Frame { get; }
        public double ElapsedSeconds { get; }
        public CharacterTestNetworkSnapshotDocument Document { get; }
    }

    internal sealed class CharacterTestSliceDebugServices
    {
        private readonly CharacterTestSliceModules _modules;
        private readonly CharacterTestSliceServices _services;
        private readonly Action _throwIfDisposed;

        public CharacterTestSliceDebugServices(
            CharacterTestSliceModules modules,
            CharacterTestSliceServices services,
            Action throwIfDisposed)
        {
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _throwIfDisposed = throwIfDisposed ?? (() => { });
        }

        public FrameworkDebugSourceRegistry CreateDebugSourceRegistry(Func<bool> isPausedProvider = null)
        {
            _throwIfDisposed();
            var registry = new FrameworkDebugSourceRegistry();
            registry.Register(new RuntimeHostDebugSource(_modules.Host, "RuntimeHost"));
            if (_modules.Resources?.ResourceManager != null)
                registry.Register(new ResourceDebugSource(_modules.Resources.ResourceManager, "Resources"));
            if (_modules.AbilityRuntime != null)
                registry.Register(new CharacterTestAbilityDebugSource(_services, "Ability"));
            if (_modules.RuntimeAiPlannerCommands != null)
                registry.Register(new CharacterTestRuntimeAiPlannerDebugSource(
                    _modules.RuntimeAiPlannerCommands,
                    "Runtime AI Planner"));
            if (_modules.VfxRuntime != null)
                registry.Register(new VfxDebugSource(_modules.VfxRuntime, "VFX"));
            registry.Register(new CharacterTestStoryDebugSource(_modules.StoryModule, "Story"));
            registry.Register(new FrameworkPerformanceCounterDebugSource(
                _services.CreateInteractionPerformanceCounterSnapshot,
                "CharacterTestInteractionPerformance"));
            registry.Register(new FrameworkPerformanceCounterDebugSource(
                _services.CreateRuntimePerformanceCounterSnapshot,
                "CharacterTestRuntimePerformance"));
            registry.Register(new CharacterTestSliceDebugSource(_services, isPausedProvider, "CharacterTest"));
            return registry;
        }

        /// <remarks>AllocByDesign: explicit diagnostics/profile evidence capture; do not call from Tick.</remarks>
        public string CreatePerformanceSmokeReport(
            string scenarioName = null,
            FrameworkPerformanceCounterSnapshot uiSnapshot = null)
        {
            _throwIfDisposed();
            return CharacterTestPerformanceCounterReport.CreateText(
                scenarioName,
                _services.CurrentFrame.Value,
                _services.CreateInteractionPerformanceCounterSnapshot(),
                _services.CreateRuntimePerformanceCounterSnapshot(),
                uiSnapshot);
        }
    }
}
