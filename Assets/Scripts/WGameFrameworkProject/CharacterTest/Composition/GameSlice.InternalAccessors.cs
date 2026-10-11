using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.DebugUI;
using MxFramework.Diagnostics;
using MxFramework.Gameplay;
using MxFramework.Input;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class GameSlice
    {
        internal RuntimeHost Host => Services.Modules.Host;
        internal RuntimeClock Clock => Services.Modules.Clock;
        internal StoryDirector StoryDirector => Services.Modules.StoryDirector;
        internal StoryRuntimeModule StoryModule => Services.Modules.StoryModule;
        internal CharacterTestRuntimeCommandModule RuntimeCommands => Services.Modules.RuntimeCommands;
        internal CharacterTestRuntimeAiPlannerCommandModule RuntimeAiPlannerCommands =>
            Services.Modules.RuntimeAiPlannerCommands;
        internal AbilityRuntimeModule AbilityRuntime => Services.Modules.AbilityRuntime;
        internal CharacterTestSkillSlotRuntime SkillSlots => Services.Modules.SkillSlots;
        internal CharacterTestAbilityHotbarRuntime AbilityHotbar => Services.Modules.AbilityHotbar;
        internal GameplayBuffDefinitionRegistry BuffDefinitions => Services.Modules.BuffDefinitions;
        internal GameplayBuffPresentationDefinitionRegistry BuffPresentationDefinitions =>
            Services.Modules.BuffPresentationDefinitions;
        internal CharacterTestCharacterRuntimeModule CharacterRuntime => Services.Modules.CharacterRuntime;
        internal GlobalResourceRuntimeServices Resources => Services.Modules.Resources;
        internal IAudioService AudioService => Services.Modules.AudioService;
        internal SceneFlowController SceneFlowController => Services.Modules.SceneFlowController;
        internal bool HasSceneFlow => Services.Modules.HasSceneFlow;
        internal CharacterTestCharacterActionRuntime ActionRuntime => Services.Modules.ActionRuntime;
        internal CharacterTestCharacterCombatTraceSystem CombatTraceSystem => Services.Modules.CombatTraceSystem;
        internal VfxRuntimeModule VfxRuntime => Services.Modules.VfxRuntime;
        internal UnityVfxPresenter VfxPresenter => Services.Modules.UnityVfxPresenter;
        internal CombatProjectileUnityPresenter ProjectilePresenter => Services.Modules.ProjectilePresenter;
        internal bool ProceduralHitFeedbackEnabled => Services.Presentation.ProceduralHitFeedbackEnabled;
        internal CharacterTestProceduralHitFeedbackResult LastProceduralHitFeedbackResult =>
            Services.Presentation.LastProceduralHitFeedbackResult;
        internal CharacterTestProceduralEffectPreviewResult LastProceduralEffectPreviewResult =>
            Services.Presentation.LastProceduralEffectPreviewResult;
        internal CharacterTestCharacterActionReloadResult? LastActionReloadResult =>
            Services.Modules.LastActionReloadResult;
        internal CharacterTestCharacterMovementProfileRuntime MovementProfile => Services.Modules.MovementProfile;
        internal CharacterTestCharacterPhysicsRuntime PhysicsRuntime => Services.Modules.PhysicsRuntime;
        internal string LastGenesisMovableObjectSummary => Services.Modules.LastGenesisMovableObjectSummary;
        internal CharacterTestGenesisMovableNaturalObjectValidation LastGenesisMovableObjectValidation =>
            Services.Modules.LastGenesisMovableObjectValidation;
        internal string LastGenesisPotencyMechanicsSummary => Services.Modules.LastGenesisPotencyMechanicsSummary;
        internal CharacterTestGenesisPotencyMechanicsValidation LastGenesisPotencyMechanicsValidation =>
            Services.Modules.LastGenesisPotencyMechanicsValidation;
        internal string LastGenesisLivingBeingSummary => Services.Modules.LastGenesisLivingBeingSummary;
        internal CharacterTestGenesisLivingBeingValidation LastGenesisLivingBeingValidation =>
            Services.Modules.LastGenesisLivingBeingValidation;
        internal string LastGenesisLivingNaturalAttributesSummary =>
            Services.Modules.LastGenesisLivingNaturalAttributesSummary;
        internal CharacterTestGenesisLivingNaturalAttributesValidation LastGenesisLivingNaturalAttributesValidation =>
            Services.Modules.LastGenesisLivingNaturalAttributesValidation;
        internal string LastGenesisLivingNaturalAttributesRuntimeStateSummary =>
            Services.Modules.LastGenesisLivingNaturalAttributesRuntimeStateSummary;
        internal CharacterTestTraceRecorder TraceRecorder => Services.Modules.TraceRecorder;
        internal CharacterTestCameraMode CameraMode => Services.Modules.RuntimeCommands.CameraMode;
        internal bool TargetLockEnabled => Services.Modules.RuntimeCommands.TargetLockEnabled;
        internal int LockedTargetEntityId => Services.Modules.RuntimeCommands.LockedTargetEntityId;
        internal CharacterTestCombatTargetHandle LockedCombatTarget =>
            Services.Modules.RuntimeCommands.LockedCombatTarget;
        internal CharacterTestHeadPadDisplayMode HeadPadDisplayMode => Services.Modules.HeadPadDisplayMode;
        internal int PendingRuntimeCommandCount => Services.Modules.PendingRuntimeCommandCount;
        internal RuntimeEventQueueSnapshot RuntimeEventQueueSnapshot => Services.Modules.RuntimeEventQueueSnapshot;
        internal IReadOnlyList<CharacterTestRuntimeEvent> RecentRuntimeEvents => Services.Modules.RecentRuntimeEvents;
        internal StoryDirectorSnapshot StorySnapshot => Services.Modules.StorySnapshot;
        internal IReadOnlyList<CharacterTestCharacterEntity> CharacterEntities => Services.Modules.CharacterEntities;
        internal IReadOnlyList<CharacterTestMovableNaturalObjectState> MovableNaturalObjects =>
            Services.Modules.MovableNaturalObjects;
        internal CharacterTestMovableNaturalObjectSummary MovableNaturalObjectSummary =>
            Services.Modules.MovableNaturalObjectSummary;

        internal void SetCharacterMovementBasisProvider(ICharacterTestCharacterMovementBasisProvider provider)
        {
            Services.Presentation.SetCharacterMovementBasisProvider(provider);
        }

        internal void SetTargetLockMaxDistance(float maxDistance)
        {
            Services.Presentation.SetTargetLockMaxDistance(maxDistance);
        }

        internal void SetHeadPadDisplayMode(CharacterTestHeadPadDisplayMode displayMode)
        {
            Services.Presentation.SetHeadPadDisplayMode(displayMode);
        }

        internal bool SetProceduralHitFeedbackEnabled(bool enabled)
        {
            return Services.Presentation.SetProceduralHitFeedbackEnabled(enabled);
        }

        internal CharacterTestProceduralEffectPreviewResult TryApplyLockedTargetProceduralEffectPreview(
            CharacterTestProceduralEffectPreviewId effectId,
            string triggerName = "")
        {
            return Services.Presentation.TryApplyLockedTargetProceduralEffectPreview(effectId, triggerName);
        }

        internal void SetLocalInputProvider(IInputProvider provider)
        {
            Services.Commands.SetLocalInputProvider(provider);
        }

        internal void SetRuntimeCommandSubmitter(Func<RuntimeCommand, RuntimeCommandValidationResult> submitter)
        {
            Services.Commands.SetRuntimeCommandSubmitter(submitter);
        }

        internal void SetNetworkDiagnosticsProvider(Func<CharacterTestNetworkLoopbackDiagnostics> provider)
        {
            Services.Network.SetNetworkDiagnosticsProvider(provider);
        }

        internal CharacterTestNetworkLoopbackDiagnostics CaptureNetworkLoopbackDiagnostics()
        {
            return Services.Network.CaptureNetworkLoopbackDiagnostics();
        }

        internal RuntimeCommandValidationResult EnqueueRuntimeCommand(RuntimeCommand command)
        {
            return Services.Commands.EnqueueRuntimeCommand(command);
        }

        internal CharacterTestCharacterActionReloadResult ReloadCharacterActionConfig()
        {
            return Services.Commands.ReloadCharacterActionConfig();
        }

        internal RuntimeCommandValidationResult EnqueueSetCameraMode(
            CharacterTestCameraMode mode,
            int sourceId = CharacterTestRuntimeCommandSources.System,
            RuntimeTraceIdentity traceId = default)
        {
            return Services.Commands.EnqueueSetCameraMode(mode, sourceId, traceId);
        }

        internal RuntimeCommandValidationResult EnqueueToggleTargetLock(
            int primaryEntityId,
            int sourceId = CharacterTestRuntimeCommandSources.CameraInput,
            RuntimeTraceIdentity traceId = default)
        {
            return Services.Commands.EnqueueToggleTargetLock(primaryEntityId, sourceId, traceId);
        }

        internal RuntimeCommandValidationResult EnqueueSkillSlotInput(
            int slotIndex,
            int targetEntityId = 0,
            int sourceId = CharacterTestRuntimeCommandSources.SkillPanel,
            RuntimeTraceIdentity traceId = default)
        {
            return Services.Commands.EnqueueSkillSlotInput(slotIndex, targetEntityId, sourceId, traceId);
        }

        internal RuntimeCommandValidationResult EnqueueCastAbilitySlot(
            int targetEntityId,
            int slotNumber,
            int abilityId,
            int sourceId = CharacterTestRuntimeCommandSources.PlayerInput,
            RuntimeTraceIdentity traceId = default)
        {
            return Services.Commands.EnqueueCastAbilitySlot(
                targetEntityId,
                slotNumber,
                abilityId,
                sourceId,
                traceId);
        }

        internal RuntimeCommandValidationResult EnqueueSurfaceProbe(
            CharacterTestSurfaceProbeOperation operation,
            int targetEntityId = 0,
            int sourceId = CharacterTestRuntimeCommandSources.SurfaceProbePanel,
            RuntimeTraceIdentity traceId = default)
        {
            return Services.Commands.EnqueueSurfaceProbe(operation, targetEntityId, sourceId, traceId);
        }

        internal RuntimeCommandValidationResult EnqueueMovableNaturalObjectReset(
            int targetNumericId = 0,
            int sourceId = CharacterTestRuntimeCommandSources.DebugUi,
            RuntimeTraceIdentity traceId = default)
        {
            return Services.Commands.EnqueueMovableNaturalObjectReset(targetNumericId, sourceId, traceId);
        }

        internal RuntimeCommandValidationResult EnqueueMovableNaturalObjectPotencyInfluence(
            CharacterTestGenesisPotencyInfluenceKind influenceKind,
            int targetNumericId = 0,
            int sourceId = CharacterTestRuntimeCommandSources.DebugUi,
            RuntimeTraceIdentity traceId = default)
        {
            return Services.Commands.EnqueueMovableNaturalObjectPotencyInfluence(
                influenceKind,
                targetNumericId,
                sourceId,
                traceId);
        }

        internal SceneFlowResult RequestSceneLoad(
            string sceneKey,
            SceneFlowLoadMode loadMode = SceneFlowLoadMode.Single,
            bool unloadPreviousScene = false)
        {
            return Services.StoryFlow.RequestSceneLoad(sceneKey, loadMode, unloadPreviousScene);
        }

        internal SceneFlowResult RequestSceneUnload(string sceneKey)
        {
            return Services.StoryFlow.RequestSceneUnload(sceneKey);
        }

        internal SceneFlowSnapshot CaptureSceneFlowSnapshot()
        {
            return Services.StoryFlow.CaptureSceneFlowSnapshot();
        }

        internal FrameworkDebugSourceRegistry CreateDebugSourceRegistry(Func<bool> isPausedProvider = null)
        {
            return Services.Debug.CreateDebugSourceRegistry(isPausedProvider);
        }

        internal string CreatePerformanceSmokeReport(
            string scenarioName = null,
            FrameworkPerformanceCounterSnapshot uiSnapshot = null)
        {
            return Services.Debug.CreatePerformanceSmokeReport(scenarioName, uiSnapshot);
        }

        internal CharacterTestPerformanceSmokeScenarioResult RunPerformanceSmokeScenario(
            string scenarioName = null,
            int warmupFrameCount = CharacterTestPerformanceSmokeScenario.DefaultWarmupFrameCount,
            int captureFrameCount = CharacterTestPerformanceSmokeScenario.DefaultCaptureFrameCount,
            double deltaTimeSeconds = CharacterTestPerformanceSmokeScenario.DefaultDeltaTimeSeconds,
            Func<FrameworkPerformanceCounterSnapshot> uiSnapshotProvider = null)
        {
            return CharacterTestPerformanceSmokeScenario.Run(
                this,
                scenarioName,
                warmupFrameCount,
                captureFrameCount,
                deltaTimeSeconds,
                uiSnapshotProvider);
        }

        internal void TickPresentation(double deltaTime)
        {
            Services.Presentation.TickPresentation(deltaTime);
        }

        internal RuntimeCommandValidationResult RaiseTrigger(
            int triggerId,
            int sourceId = StoryRuntimeCommandSources.Input)
        {
            return Services.StoryFlow.RaiseTrigger(triggerId, sourceId);
        }
    }
}
