using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Input;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// CharacterTest runtime session lifecycle shell.
    /// Domain modules and feature commands are exposed to this assembly through <see cref="Services"/>.
    /// </summary>
    public sealed partial class GameSlice : IDisposable
    {
        private static readonly ProfilerMarker HostTickMarker =
            new ProfilerMarker("CharacterTest.GameSlice.HostTick");
        private static readonly ProfilerMarker ProceduralEffectPreviewMarker =
            new ProfilerMarker("CharacterTest.GameSlice.ProceduralEffectPreview");

        private readonly CharacterTestSliceComposition _composition;
        private readonly IRuntimeLogger _logger;
        private double _elapsedSeconds;
        private bool _disposed;
        private readonly CharacterTestAuthorityTickState _authority = new CharacterTestAuthorityTickState();
        private readonly IDisposable _bootstrapSubscription;

        internal GameSlice(CharacterTestSliceOptions options = null)
        {
            using var startupProfile = CharacterTestStartupProfiler.CreateSlice.Auto();
            _composition = CharacterTestSliceCompositionBuilder.Build(
                options,
                () => _elapsedSeconds,
                RestoreRuntimeState,
                ThrowIfDisposed,
                ThrowIfAuthorityUnavailable);
            Services = _composition.Services;
            _logger = Services.Logger;
            try
            {
                if (options?.DetachedTacticalRestore == true) return;
                _bootstrapSubscription = options?.SubscribeBeforeStoryBootstrap?.Invoke(this);
                if (options?.EnableStoryBootstrap != false)
                    Services.StoryFlow.BootstrapStorySession(Tick);
                else
                {
                    Services.Modules.StoryFlowBridge.TryBootstrapStartupWithoutStory();
                }
            }
            catch (Exception failure)
            {
                _disposed = true;
                var errors = new List<Exception>();
                if (_bootstrapSubscription != null)
                    CharacterTestCleanupScope.Attempt(errors, "BootstrapSubscription", _bootstrapSubscription.Dispose);
                _composition.Cleanup(errors);
                if (errors.Count != 0)
                {
                    errors.Insert(0, failure);
                    throw new AggregateException("GameSlice bootstrap and rollback failed.", errors);
                }
                throw;
            }
        }

        internal GameSlice(
            IRuntimeLogger logger = null,
            CharacterTestStoryContent storyContent = null,
            GlobalResourceRuntimeServices resources = null,
            string baseResourcePreloadGroupId = "",
            CharacterTestCameraMode initialCameraMode = CharacterTestCameraMode.ThirdPersonControlled,
            CharacterTestCharacterActionConfigSourceOptions actionConfigOptions = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null,
            bool createCharacterViews = true,
            IInputProvider inputProvider = null,
            bool enableLocalInputCommands = true,
            bool smoothNetworkSnapshotViews = false,
            bool enableCharacterSimulation = true,
            IAudioService audioService = null,
            ICharacterTestAudioCueCatalog audioCueCatalog = null,
            bool disposeAudioService = false,
            int simulationTickRate = CharacterTestDeterministicUnits.DefaultTickRate,
            CharacterTestProceduralHitFeedbackOptions proceduralHitFeedbackOptions = null,
            CharacterTestAiBehaviorProfile aiBehaviorProfile = null,
            IReadOnlyList<CharacterTestMovableNaturalObjectSpawnSpec> movableNaturalObjectSpawnSpecs = null,
            IReadOnlyList<CharacterTestCharacterSpawnSpec> startupCharacterSpawnSpecs = null,
            CharacterTestRuntimeAiPlannerProfile runtimeAiPlannerProfile = null,
            CharacterTestRuntimeAiPlannerValidationReport runtimeAiPlannerProfileReport = null)
            : this(CharacterTestSliceOptions.FromLegacyArguments(
                logger,
                storyContent,
                resources,
                baseResourcePreloadGroupId,
                initialCameraMode,
                actionConfigOptions,
                physicsRuntime,
                createCharacterViews,
                inputProvider,
                enableLocalInputCommands,
                smoothNetworkSnapshotViews,
                enableCharacterSimulation,
                audioService,
                audioCueCatalog,
                disposeAudioService,
                simulationTickRate,
                proceduralHitFeedbackOptions,
                aiBehaviorProfile,
                movableNaturalObjectSpawnSpecs,
                startupCharacterSpawnSpecs,
                runtimeAiPlannerProfile,
                runtimeAiPlannerProfileReport))
        {
        }

        internal CharacterTestSliceServices Services { get; }
        internal CharacterTestLevelGameplayTriggerRuntimeStatus
            LevelGameplayTriggerStatus =>
                Services.Modules.LevelGameplayTriggerStatus;
        public RuntimeFrame CurrentFrame => Services.Modules.Clock.CurrentFrame;
        public double ElapsedSeconds => _elapsedSeconds;
        internal bool IsAuthoritySuspended => _authority.IsSuspended;
        internal Exception AuthorityFailure => _authority.Failure;
        internal RuntimeFrame FailedAuthorityFrame => _authority.FailedFrame;

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(double deltaTime)
        {
            ThrowIfAuthorityUnavailable();
            if (Services.Modules.TacticalStage != null && !Services.Modules.TacticalStage.Stage.CanStep) return;
            if (Services.Modules.MixedPlayerTacticalStage != null
                && !Services.Modules.MixedPlayerTacticalStage.CanStep) return;
            if (deltaTime < 0d)
                deltaTime = 0d;

            RuntimeFrame frame = Services.Modules.Clock.CurrentFrame;
            try
            {
                using (HostTickMarker.Auto())
                {
                    Services.Modules.Host.Tick(frame.Value, deltaTime, _elapsedSeconds);
                }
                Services.Modules.TacticalStage?.AfterWorldTick(frame, deltaTime, _elapsedSeconds);
                Services.Modules.MixedPlayerTacticalStage?.AfterWorldTick(frame);

                using (ProceduralEffectPreviewMarker.Auto())
                {
                    Services.Presentation.RefreshProceduralEffectPreviewSnapshot();
                }

                if (Services.Modules.StoryModule.LastCommandErrors.Count > 0)
                {
                    Services.LogBuffer.Clear()
                        .Append("Story command errors=")
                        .Append(Services.Modules.StoryModule.LastCommandErrors.Count)
                        .Append(" frame=")
                        .Append(frame.Value);
                    _logger.Warning("GameSlice", Services.LogBuffer);
                }

                Services.Modules.Clock.Step();
                _elapsedSeconds += deltaTime;
            }
            catch (Exception failure)
            {
                _authority.Suspend(frame, failure);
                throw;
            }
        }

        public RuntimeHostDiagnostics CaptureDiagnostics()
        {
            ThrowIfDisposed();
            return Services.Modules.Host.CaptureDiagnostics();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            var errors = new List<Exception>();
            CharacterTestCleanupScope.Attempt(errors, "Dispose log", () => _logger.Info("GameSlice", "Dispose"));
            if (_bootstrapSubscription != null)
                CharacterTestCleanupScope.Attempt(errors, "BootstrapSubscription", _bootstrapSubscription.Dispose);
            _composition.Cleanup(errors);
            CharacterTestCleanupScope.ThrowIfFailed(errors, "GameSlice cleanup failed.");
        }

        private void RestoreRuntimeState(RuntimeFrame frame, double elapsedSeconds)
        {
            ThrowIfAuthorityUnavailable();
            Services.Modules.Clock.Reset(frame);
            _elapsedSeconds = elapsedSeconds > 0d ? elapsedSeconds : 0d;
            Services.Modules.CharacterRuntime?.PerceptionFrameCache.ResetForAuthorityRestore(frame);
            Services.Modules.RuntimeAiPlannerCommands?.ResetAttackOpportunityCacheForRollbackReplay();
            Services.Modules.RuntimeCommands?.ResetForRollbackReplay(frame);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameSlice));
        }

        internal void ThrowIfAuthorityUnavailable()
        {
            ThrowIfDisposed();
            _authority.ThrowIfSuspended();
        }

        internal void SuspendAuthority(RuntimeFrame frame, Exception failure)
        {
            _authority.Suspend(frame, failure);
        }
    }
}
