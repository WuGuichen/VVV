using System;
using System.Collections;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Audio.Unity;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Runtime;
using MxFramework.Runtime.Unity;
using MxFramework.Rendering;
using Unity.Profiling;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// CharacterTest scene root. Owns Unity lifecycle and delegates runtime composition,
    /// menu/network control, and diagnostics to internal coordinators.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Game Manager")]
    public sealed partial class GameManager : MonoBehaviour
    {
        private const string SkipMainMenuArgument = "-character-test-skip-main-menu";

        private static readonly ProfilerMarker MainMenuInputMarker =
            new ProfilerMarker("CharacterTest.GameManager.MainMenuInput");
        private static readonly ProfilerMarker SimulationMarker =
            new ProfilerMarker("CharacterTest.GameManager.Simulation");
        private static readonly ProfilerMarker CameraPresentationMarker =
            new ProfilerMarker("CharacterTest.GameManager.CameraPresentation");
        private static readonly ProfilerMarker SlicePresentationMarker =
            new ProfilerMarker("CharacterTest.GameManager.SlicePresentation");
        private static readonly ProfilerMarker UiRefreshMarker =
            new ProfilerMarker("CharacterTest.GameManager.UiRefresh");
        private static readonly ProfilerMarker UiTickMarker =
            new ProfilerMarker("CharacterTest.GameManager.UiTick");

        [SerializeField] private CharacterTestSceneProfile _profile;
        [SerializeField] private Transform _worldObjectSceneRoot;
        [SerializeField] private CharacterTestWorldObjectSceneCache _worldObjectSceneCache =
            new CharacterTestWorldObjectSceneCache();
        [SerializeField] private CharacterTestGameplayTriggerSceneCache _gameplayTriggerSceneCache =
            new CharacterTestGameplayTriggerSceneCache();
        [SerializeField] private CharacterTestSceneCharacterCache _characterSceneCache =
            new CharacterTestSceneCharacterCache();
        [SerializeField] private CharacterTestGuidanceSceneCache _guidanceSceneCache =
            new CharacterTestGuidanceSceneCache();
        [SerializeField] private CharacterTestScenePhysicsSettings _physicsSettings =
            new CharacterTestScenePhysicsSettings();
        [SerializeField] private CharacterTestNavigationSceneSettings _navigationSettings =
            new CharacterTestNavigationSceneSettings();
        [SerializeField, HideInInspector] private MxSceneRenderingConfiguration _sceneRendering;

        private CharacterTestSceneProfile _runtimeProfile;
        [SerializeField] private string _initialLevelId = string.Empty;
        public string InitialLevelId => _initialLevelId;
        public void SetInitialLevelId(string levelId) => _initialLevelId = levelId ?? string.Empty;
        private CharacterTestRuntimeContext _context;
        private Action<double> _tickSimulationOnce;
        private CharacterTestPerformancePlayerReportRunner _performancePlayerReportRunner;
        private Coroutine _audioStartupCoroutine;
        private AudioStartupArtifactBundle _audioStartupArtifacts;
        private TacticalCommandPump _tacticalCommands;
        private TacticalStageFlowCoordinator _tacticalStage;
        private MxSceneRenderingSession _sceneRenderingSession;

        // Session composition owns Story subscriptions, Spawn receipts and completed physics samples.
        internal void BindTacticalControl(TacticalStageFlowCoordinator stage, TacticalCommandPump commands)
        {
            if ((stage == null) != (commands == null)) throw new ArgumentException("Bind both tactical collaborators together.");
            if (commands != null && !commands.Uses(stage)) throw new ArgumentException("Command pump belongs to another stage.");
            TacticalStageFlowCoordinator previous = _tacticalStage;
            _tacticalStage = stage;
            _tacticalCommands = commands;
            if (_context != null) _context.FixedTickDriver.Reset();
            if (!ReferenceEquals(previous, stage)) previous?.ReleaseControl();
        }

        private bool CanStepTacticalSimulation() => _context?.Slice?.IsAuthoritySuspended != true
            && (_context?.Slice?.Services.Modules.MixedPlayerTacticalStage?.CanStep
                ?? (_tacticalStage == null || _tacticalStage.CanStep));

        internal CharacterTestRuntimeContext RuntimeContext => _context;
        public string ActiveLevelId => _context?.ActiveLevelId ?? string.Empty;
        public void LoadLevel(string levelId)
        {
            if (_context?.SessionBuilder == null) throw new System.InvalidOperationException("Enter Play before loading a published level.");
            _context.SessionBuilder.LoadLevel(levelId);
        }
        internal async System.Threading.Tasks.Task<bool> ResetTacticalProgressAsync()
        {
            var service = CharacterTacticalServices.Player;
            if (service == null || _context?.SessionBuilder == null) return false;
            _tacticalStage?.ReleaseControl(); _tacticalStage = null; _tacticalCommands = null;
            _context.SessionBuilder.RetireForTacticalReset();
            bool reset = await service.ResetAsync();
            if (this != null && _context?.SessionBuilder != null)
            {
                if (GetComponent<TacticalCardSliceCoordinator>() != null)
                    _context.SessionBuilder.RebuildTacticalPreparationSession(reset ? "已清档，请重新整备并确认出战。" : service.SaveIssue);
                else _context.SessionBuilder.RebuildRuntimeSession(CharacterTestNetworkSessionKind.Offline,
                    CharacterTestNetworkMode.Disabled, null, reset ? "已清档重开。" : service.SaveIssue);
            }
            return reset;
        }
        internal CharacterTestSceneProfile Profile => _runtimeProfile;
        internal CharacterTestSceneProfile ConfiguredProfile => _profile;
        internal AudioStartupArtifactBundle AudioStartupArtifacts => _audioStartupArtifacts;
        public CharacterTestWorldObjectSceneCache WorldObjectSceneCache =>
            _worldObjectSceneCache ?? (_worldObjectSceneCache = new CharacterTestWorldObjectSceneCache());
        public CharacterTestGameplayTriggerSceneCache GameplayTriggerSceneCache =>
            _gameplayTriggerSceneCache
            ?? (_gameplayTriggerSceneCache =
                new CharacterTestGameplayTriggerSceneCache());
        public CharacterTestSceneCharacterCache CharacterSceneCache =>
            _characterSceneCache ?? (_characterSceneCache = new CharacterTestSceneCharacterCache());
        internal CharacterTestGuidanceSceneCache GuidanceSceneCache =>
            _guidanceSceneCache ?? (_guidanceSceneCache = new CharacterTestGuidanceSceneCache());
        public CharacterTestScenePhysicsSettings PhysicsSettings =>
            _physicsSettings ?? (_physicsSettings = new CharacterTestScenePhysicsSettings());
        public CharacterTestNavigationSceneSettings NavigationSettings =>
            _navigationSettings ?? (_navigationSettings = new CharacterTestNavigationSceneSettings());
        public Transform WorldObjectSceneRoot => _worldObjectSceneRoot != null ? _worldObjectSceneRoot : transform;
        public MxSceneRenderingConfiguration SceneRendering => _sceneRendering;

        /// <summary>LowFreqAlloc on first override. Changes presentation immediately without modifying weapon/action authority.</summary>
        public bool TrySetWeaponBladeTrailStyleOverride(int entityId, int equipmentSlotId,
            MxFramework.CharacterApplication.WeaponBladeTrailStyle style)
            => _context?.Slice?.Services.Presentation.TrySetWeaponBladeTrailStyleOverride(entityId, equipmentSlotId, style) == true;

        /// <summary>Restores the weapon's configured default for active trails and subsequent windows.</summary>
        public bool ClearWeaponBladeTrailStyleOverride(int entityId, int equipmentSlotId)
            => _context?.Slice?.Services.Presentation.ClearWeaponBladeTrailStyleOverride(entityId, equipmentSlotId) == true;

        // Editor authoring changes the scene-owned configuration through Undo before calling this method.
        public void SetSceneRendering(MxSceneRenderingConfiguration configuration)
        {
            if (configuration != null) configuration.AuthoringCreated = true;
            _sceneRendering = configuration;
            CharacterTestDirectionalShadowPolicy.Configure(configuration);
            if (Application.isPlaying && isActiveAndEnabled && _sceneRenderingSession == null &&
                configuration != null && (configuration.IndirectLightingEnabled || configuration.HeightFogEnabled || configuration.DustEnabled || configuration.DirectionalShadowsEnabled || configuration.Presentations?.Length > 0))
                _sceneRenderingSession = new MxSceneRenderingSession(() => _sceneRendering, gameObject.scene);
            else if (configuration == null || (!configuration.IndirectLightingEnabled && !configuration.HeightFogEnabled && !configuration.DustEnabled && !configuration.DirectionalShadowsEnabled && !(configuration.Presentations?.Length > 0)))
            { _sceneRenderingSession?.Dispose(); _sceneRenderingSession = null; }
            else _sceneRenderingSession?.Tick();
        }

        // Editor authoring supplies its pre-Play fingerprint result before static batching
        // replaces source meshes. Player builds reject stale data during scene processing.
        public void SetSceneRenderingSourceValidation(bool valid, string reason) =>
            _sceneRenderingSession?.SetSourceValidation(valid, reason);
        public void SetDirectionalShadowSourceValidation(bool valid, string reason) =>
            _sceneRenderingSession?.SetDirectionalShadowSourceValidation(valid, reason);

        internal void SetGuidanceSceneCache(CharacterTestGuidanceSceneCache cache)
        {
            _guidanceSceneCache = cache ?? new CharacterTestGuidanceSceneCache();
        }

        private void OnEnable()
        {
            SynchronizeShallowWater();
            CharacterTestDirectionalShadowPolicy.Configure(_sceneRendering);
            if (Application.isPlaying && _sceneRendering != null &&
                (_sceneRendering.IndirectLightingEnabled || _sceneRendering.HeightFogEnabled || _sceneRendering.DustEnabled || _sceneRendering.DirectionalShadowsEnabled || _sceneRendering.Presentations?.Length > 0))
                _sceneRenderingSession = new MxSceneRenderingSession(() => _sceneRendering, gameObject.scene);
            if (!Application.isEditor && AudioStreamingAssetsLoader.RequiresWebRequest)
            {
                _audioStartupCoroutine = StartCoroutine(BootstrapAudioAndInitialize());
                return;
            }

            InitializeRuntime();
        }

        private IEnumerator BootstrapAudioAndInitialize()
        {
            AudioStartupArtifactBundle artifacts = null;
            yield return AudioStartupArtifactLoader.Load(value => artifacts = value);
            _audioStartupCoroutine = null;
            if (!isActiveAndEnabled)
                yield break;
            if (artifacts == null || !artifacts.Success)
            {
                Debug.LogError(
                    "[CharacterTest.Audio] Required StreamingAssets startup artifacts failed to load: "
                    + (artifacts?.Error ?? "loader did not return a result."),
                    this);
                yield break;
            }

            _audioStartupArtifacts = artifacts;
            InitializeRuntime();
        }

        private void InitializeRuntime()
        {
            _tickSimulationOnce = TickSimulationOnce;
            _runtimeProfile = CharacterTestSceneProfile.ResolveStartupProfile(_profile);
            _context = new CharacterTestRuntimeContext(this, _runtimeProfile)
            {
                Logger = new UnityRuntimeLogger(this, "CharacterTest")
                {
                    Enabled = _runtimeProfile.LogConsole,
                    UseRichTextColors = _runtimeProfile.LogConsoleColors
                }
            };
            ConfigureLogColors();
            _context.FixedTickDriver.CanStepPredicate = CanStepTacticalSimulation;

            var sessionBuilder = new CharacterTestRuntimeSessionBuilder(this, _context, _runtimeProfile);
            var mainMenuCoordinator = new CharacterTestMainMenuCoordinator(_context, _runtimeProfile, sessionBuilder);
            var networkCoordinator = new CharacterTestNetworkCoordinator(_context, _runtimeProfile, sessionBuilder);
            var debugPresentationController =
                new CharacterTestDebugPresentationController(_context, _runtimeProfile, sessionBuilder);
            CharacterTestArenaSessionController arenaSessionController =
                GetComponent<CharacterTestArenaSessionController>();
            CharacterTestRuntimeAiPlannerDevSceneController runtimeAiPlannerDevSceneController =
                GetComponent<CharacterTestRuntimeAiPlannerDevSceneController>();

            _context.SessionBuilder = sessionBuilder;
            _context.MainMenuCoordinator = mainMenuCoordinator;
            _context.NetworkCoordinator = networkCoordinator;
            _context.DebugPresentationController = debugPresentationController;
            _context.ArenaSessionController = arenaSessionController;
            _context.RuntimeAiPlannerDevSceneController = runtimeAiPlannerDevSceneController;
            arenaSessionController?.Initialize(this, _context, sessionBuilder);
            runtimeAiPlannerDevSceneController?.Initialize(this, _context, sessionBuilder);

            sessionBuilder.ApplyTargetFrameRate();
            sessionBuilder.ConfigureLocalizationRuntime();
            sessionBuilder.ConfigureSettingsRuntime();
            sessionBuilder.ResolvePlayerUiInputProvider();
            bool skipMainMenuOnStart = HasCommandLineArgument(SkipMainMenuArgument);
            mainMenuCoordinator.Initialize();
            if (!string.IsNullOrWhiteSpace(_initialLevelId))
            {
                sessionBuilder.LoadLevel(_initialLevelId);
            }
            else if (runtimeAiPlannerDevSceneController != null)
            {
                runtimeAiPlannerDevSceneController.PrepareInitialSetupState(restorePersistedDraft: true);
                sessionBuilder.RebuildArenaSetupSession(
                    runtimeAiPlannerDevSceneController.MatchConfig,
                    "Runtime AI Lab setup.");
                StepSimulationOnceForRuntimeAiPlannerDev();
            }
            else if (arenaSessionController != null)
            {
                arenaSessionController.PrepareInitialSetupState();
                sessionBuilder.RebuildArenaSetupSession(
                    arenaSessionController.Config,
                    "Arena setup.");
            }
            else if (GetComponent<TacticalCardSliceCoordinator>() != null)
            {
                sessionBuilder.RebuildTacticalPreparationSession();
            }
            else
            {
                sessionBuilder.RebuildRuntimeSession(
                    CharacterTestNetworkSessionKind.Offline,
                    CharacterTestNetworkMode.Disabled,
                    null,
                    "Offline");
            }

            if ((skipMainMenuOnStart || _tacticalStage != null)
                && arenaSessionController == null
                && runtimeAiPlannerDevSceneController == null
                && GetComponent<TacticalCardSliceCoordinator>() == null)
            {
                mainMenuCoordinator.StartOrContinueFromMainMenu();
            }

            _performancePlayerReportRunner =
                CharacterTestPerformancePlayerReportRunner.TryCreateFromCommandLine(_context);
            networkCoordinator.ConfigureNetworkAutomationRunner();

            _context.LogBuffer.Clear()
                .Append("GameSlice created. startPaused=")
                .Append(_runtimeProfile.StartPaused)
                .Append(", showMainMenuOnStart=")
                .Append(CharacterTestUiDefaults.ShowMainMenuOnStart)
                .Append(", pauseRuntimeWhenMainMenuOpen=")
                .Append(CharacterTestUiDefaults.PauseRuntimeWhenMainMenuOpen)
                .Append(", networkMode=")
                .Append(CharacterTestNetworkMode.Disabled.ToString());
            _context.Logger.Info("GameManager", _context.LogBuffer);
        }

        private void OnDisable()
        {
            ReleaseShallowWater();
            _sceneRenderingSession?.Dispose();
            _sceneRenderingSession = null;
            if (_audioStartupCoroutine != null)
            {
                StopCoroutine(_audioStartupCoroutine);
                _audioStartupCoroutine = null;
            }
            ReleaseRuntime();
            _audioStartupArtifacts = null;
        }

        private void OnDestroy()
        {
            ReleaseShallowWater();
            _sceneRenderingSession?.Dispose();
            _sceneRenderingSession = null;
            ReleaseRuntime();
        }

        private void Update()
        {
            SynchronizeShallowWater()?.Tick();
            _sceneRenderingSession?.Tick();
            if (_context == null || _context.Slice == null)
                return;

            CharacterTestPerformancePlayerReportRunner performancePlayerReportRunner =
                _performancePlayerReportRunner;
            performancePlayerReportRunner?.BeginFrame();

            using (MainMenuInputMarker.Auto())
            {
                _context.MainMenuCoordinator.HandleMainMenuInput();
                _context.MainMenuCoordinator.RefreshEffectivePauseState();
            }
            bool cardTrialConfiguration = _context.RuntimeAiPlannerDevSceneController?.CardTrialConfigurationOpen == true;
            if (_tacticalStage != null && !_context.Slice.IsAuthoritySuspended)
            {
                if (_context.Paused || cardTrialConfiguration) _tacticalStage.Time.RequestPause(TacticalPauseReason.UserMenu);
                else _tacticalStage.Time.RequestResume(TacticalPauseReason.UserMenu);
            }

            if (!_context.Slice.IsAuthoritySuspended && !cardTrialConfiguration) _context.UiRuntime?.ProcessInput();

            // Update still runs at timeScale=0. Consume only tactical controls before world admission.
            bool tacticalWasPaused = !CanStepTacticalSimulation();
            var tacticalRuntime = _context.Slice.Services.Modules.TacticalStage;
            if (!_context.Slice.IsAuthoritySuspended)
            {
                if (tacticalRuntime != null)
                {
                    tacticalRuntime.TickWorldTimeTransition(Time.unscaledDeltaTime);
                    tacticalRuntime.DrainControls();
                }
                else if (_context.Slice.Services.Modules.MixedPlayerTacticalStage != null)
                {
                    _context.Slice.Services.Modules.MixedPlayerTacticalStage.TickWorldTimeTransition(Time.unscaledDeltaTime);
                    _context.Slice.Services.Modules.MixedPlayerTacticalStage.DrainControls();
                }
                else _tacticalCommands?.Drain();
            }
            SyncClothPresentationState();
            if (!_context.Slice.IsAuthoritySuspended && tacticalRuntime != null && tacticalRuntime.RestartReady)
            {
                _context.SessionBuilder.RebuildTacticalPreparationSession("已返回整备，请确认下一次出战。");
                return;
            }

            double deltaTime = tacticalWasPaused ? 0d : Mathf.Max(0f, Time.deltaTime);
            TickAudioMix((float)deltaTime);
            double fixedDeltaTime = _context.SessionBuilder.ResolveSimulationDeltaTime();
            bool tickedGameplayPresentation = false;
            if (!_context.Paused && !cardTrialConfiguration && !_context.Slice.IsAuthoritySuspended)
            {
                using (SimulationMarker.Auto())
                {
                    _context.DebugPresentationController.HandleProceduralEffectPreviewHotkeys();
                    if (UsesInteractiveLabPacing(_context.RuntimeAiPlannerDevSceneController != null,
                        _context.RuntimeAiPlannerDevSceneController?.ReplayInProgress == true, _context.NetworkRunner))
                        _context.FixedTickDriver.StepInteractive(deltaTime, fixedDeltaTime, _tickSimulationOnce);
                    else
                        _context.FixedTickDriver.Step(deltaTime, fixedDeltaTime, _tickSimulationOnce);
                }

                using (CameraPresentationMarker.Auto())
                {
                    _context.ThirdPersonCameraBridge?.TickPresentation((float)deltaTime, _context.Slice.CurrentFrame);
                }

                using (SlicePresentationMarker.Auto())
                {
                    PreparePresentationLod();
                    _context.Slice.Services.Presentation.TickPresentation(deltaTime);
                }

                tickedGameplayPresentation = true;
            }
            else if (!_context.Slice.IsAuthoritySuspended &&
                (_context.NetworkCoordinator.ShouldTickNetworkWhileNetworkPanelOpen()
                || _context.NetworkCoordinator.ShouldTickNetworkAutomation()))
            {
                using (SimulationMarker.Auto())
                {
                    _context.FixedTickDriver.Step(deltaTime, fixedDeltaTime, _tickSimulationOnce);
                }
            }

            _context.NetworkCoordinator.TryAutoEnterNetworkGameplayFromPanel();
            using (UiRefreshMarker.Auto())
            {
                _context.UiRuntime?.Refresh();
            }

            if (tickedGameplayPresentation)
            {
                using (UiTickMarker.Auto())
                {
                    _context.UiRuntime?.TickGameplayPresentation(_tacticalStage != null
                        ? Time.unscaledDeltaTime : (float)deltaTime);
                }
            }

            SyncClothPresentationState();
            performancePlayerReportRunner?.EndFrame();
        }

        private void LateUpdate() => SyncClothPresentationState();

        private void SyncClothPresentationState()
        {
            _context?.Slice?.CharacterRuntime?.ViewBridge?.ClothPresentation.SetPaused(
                _context.Paused || !CanStepTacticalSimulation());
        }

        private void TickAudioMix(float deltaTime)
        {
            AudioMixController mix = _context.AudioMixController;
            if (mix == null)
                return;

            AudioMixState target = CharacterTestAudioMixStatePolicy.Resolve(
                _context.MainMenuVisible,
                _context.UiRuntime?.IsMainMenuOpen ?? false,
                _context.UiRuntime?.FairyGuiStoryDialogHost?.IsOpen ?? false,
                _context.Paused,
                _context.HasEnteredGameplayFromMainMenu);
            if (mix.State != target)
            {
                mix.TransitionToState(target, 0.2f);
                AudioResult snapshotResult = _context.AudioMixSnapshotController?.SetState(target)
                    ?? AudioResult.Ok();
                if (snapshotResult.Failed)
                {
                    _context.Logger?.Warning(
                        "CharacterTest.Audio",
                        "Audio mix snapshot transition failed: " + snapshotResult.Message);
                }
            }

            AudioMusicStateController music = _context.AudioMusicStateController;
            if (music != null)
            {
                AudioMusicState musicTarget = CharacterTestAudioMixStatePolicy.ResolveMusic(
                    target,
                    _context.HasEnteredGameplayFromMainMenu,
                    music.State);
                if (music.State != musicTarget)
                {
                    AudioResult musicResult = music.TransitionTo(musicTarget);
                    if (musicResult.Failed)
                    {
                        _context.Logger?.Warning(
                            "CharacterTest.Audio",
                            "Audio music transition failed: " + musicResult.Message);
                    }
                }
            }

            AudioPersistentEventController ambience = _context.AudioAmbienceController;
            int ambienceTarget = _context.HasEnteredGameplayFromMainMenu
                && target != AudioMixState.Menu
                ? AudioProductEventIds.WindAmbience
                : 0;
            if (ambience != null && ambience.EventId != ambienceTarget)
            {
                AudioResult ambienceResult = ambience.TransitionTo(
                    ambienceTarget,
                    new AudioTraceIdentity(AudioTraceKindIds.Ambience, 0, 0, ambienceTarget));
                if (ambienceResult.Failed)
                {
                    _context.Logger?.Warning(
                        "CharacterTest.Audio",
                        "Audio ambience transition failed: " + ambienceResult.Message);
                }
            }
            mix.Tick(deltaTime);
        }

        internal static bool UsesInteractiveLabPacing(bool hasLab, bool replaying, ICharacterTestNetworkRunner runner)
            => hasLab && !replaying && (runner == null || runner is CharacterTestNetworkLoopbackRunner
                || runner is CharacterTestNetworkSessionRunner session && session.TransportMode == "loopback");

        private void TickSimulationOnce(double fixedDeltaTime)
        {
            if (!CanStepTacticalSimulation()) return;
            CharacterTestRuntimeAiPlannerDevSceneController lab =
                _context.RuntimeAiPlannerDevSceneController;
            if (lab != null
                && lab.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Completed
                && !lab.ReplayInProgress)
            {
                return;
            }

            if (!_context.Paused)
                _context.ThirdPersonCameraBridge?.PrepareForRuntimeTick();

            RuntimeFrame authorityFrame = _context.Slice.CurrentFrame;
            try
            {
                _context.NetworkCoordinator.TickAutomation();

                if (_context.NetworkRunner != null)
                {
                    _context.NetworkCoordinator.UpdateNetworkSyncProbeInputProviderBeforeNetworkTick();
                    _context.NetworkRunner.Tick(fixedDeltaTime);
                    _context.NetworkCoordinator.TickNetworkSyncProbe(fixedDeltaTime);
                    _context.NetworkCoordinator.RestoreNetworkSyncProbeInputProviderIfInactive();
                }
                else
                {
                    _context.Slice.Tick(fixedDeltaTime);
                }
            }
            catch (Exception failure)
            {
                _context.Slice.SuspendAuthority(authorityFrame, failure);
                SyncClothPresentationState();
                _context.FixedTickDriver.Reset();
                throw;
            }

            if (lab != null
                && lab.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running
                && !lab.ReplayInProgress)
            {
                _context.Slice.CharacterRuntime?.TickRuntimeAiLabReplayAnimationState(
                    fixedDeltaTime,
                    _context.Slice.CurrentFrame);
            }
            lab?.CaptureAfterAuthorityTick();
        }

        internal bool StepSimulationOnceForRuntimeAiPlannerDev()
        {
            if (_context == null || _context.Slice == null || _context.SessionBuilder == null || !CanStepTacticalSimulation())
                return false;

            double fixedDeltaTime = _context.SessionBuilder.ResolveSimulationDeltaTime();
            using (SimulationMarker.Auto())
            {
                _context.ThirdPersonCameraBridge?.PrepareForRuntimeTick();
                TickSimulationOnce(fixedDeltaTime);
            }

            using (CameraPresentationMarker.Auto())
            {
                _context.ThirdPersonCameraBridge?.TickPresentation(
                    (float)fixedDeltaTime,
                    _context.Slice.CurrentFrame);
            }

            using (SlicePresentationMarker.Auto())
            {
                _context.Slice.Services.Presentation.TickPresentation(fixedDeltaTime);
            }

            using (UiTickMarker.Auto())
            {
                _context.UiRuntime?.TickGameplayPresentation((float)fixedDeltaTime);
            }

            using (UiRefreshMarker.Auto())
            {
                _context.UiRuntime?.Refresh();
            }

            return true;
        }

        internal bool StepSimulationDataOnlyForRuntimeAiPlannerDev()
        {
            if (_context == null || _context.Slice == null || _context.SessionBuilder == null || !CanStepTacticalSimulation())
                return false;

            double fixedDeltaTime = _context.SessionBuilder.ResolveSimulationDeltaTime();
            using (SimulationMarker.Auto())
            {
                TickSimulationOnce(fixedDeltaTime);
            }

            return true;
        }

        internal bool StepRuntimeAiLabReplayFrameWithoutRendering(
            CharacterTestRuntimeAiLabFrozenArray<CharacterTestAnimationInputPresentationState> animationInputs = default)
        {
            if (!StepSimulationDataOnlyForRuntimeAiPlannerDev())
                return false;

            double fixedDeltaTime = _context.SessionBuilder.ResolveSimulationDeltaTime();
            if (animationInputs.Count > 0
                && !(_context.Slice.CharacterRuntime?.ApplyRuntimeAiLabAnimationInputs(
                    animationInputs,
                    _context.Slice.CurrentFrame) ?? false))
            {
                return false;
            }
            _context.Slice.CharacterRuntime?.TickRuntimeAiLabReplayAnimationState(
                fixedDeltaTime,
                _context.Slice.CurrentFrame);
            _context.Slice.CharacterRuntime?.TickRuntimeAiLabReplayAnimationState(
                0d,
                _context.Slice.CurrentFrame);
            return true;
        }

        internal bool RefreshPresentationForRuntimeAiPlannerDev(
            bool resubmitCharacterAnimation = true)
        {
            if (_context == null || _context.Slice == null || _context.SessionBuilder == null)
                return false;

            if (resubmitCharacterAnimation)
            {
                _context.Slice.CharacterRuntime?.ForceSyncRuntimeAiLabPresentation(
                    _context.Slice.CurrentFrame);
            }
            else
            {
                _context.Slice.CharacterRuntime?.ForceSyncRuntimeAiLabReplayPresentation(
                    _context.Slice.CurrentFrame);
            }
            using (CameraPresentationMarker.Auto())
            {
                _context.ThirdPersonCameraBridge?.TickPresentation(
                    0f,
                    _context.Slice.CurrentFrame);
            }

            using (SlicePresentationMarker.Auto())
            {
                _context.Slice.Services.Presentation.TickPresentation(0d);
            }

            using (UiTickMarker.Auto())
            {
                _context.UiRuntime?.TickGameplayPresentation(0f);
            }

            using (UiRefreshMarker.Auto())
            {
                _context.UiRuntime?.Refresh();
            }

            return true;
        }

        private void OnDrawGizmos()
        {
            _context?.DebugPresentationController?.DrawGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            _context?.DebugPresentationController?.DrawGizmosSelected();
        }

        private void ReleaseRuntime()
        {
            TacticalStageFlowCoordinator tactical = _tacticalStage;
            CharacterTestRuntimeContext context = _context;
            var performance = _performancePlayerReportRunner;
            _tacticalCommands = null;
            _tacticalStage = null;
            _performancePlayerReportRunner = null;
            _context = null;
            _runtimeProfile = null;
            _tickSimulationOnce = null;
            var errors = new List<Exception>();
            if (tactical != null)
                CharacterTestCleanupScope.Attempt(errors, "TacticalControl", tactical.ReleaseControl);
            if (context != null)
            {
                context.NetworkAutomationRunner = null;
                CharacterTestCleanupScope.Attempt(errors, "SessionBuilder", () => context.SessionBuilder?.ReleaseAll());
            }
            if (performance != null)
                CharacterTestCleanupScope.Attempt(errors, "PerformanceReportRunner", performance.Dispose);
            if (Time.timeScale <= 0f)
            {
                Time.timeScale = 1f;
            }
            CharacterTestCleanupScope.ThrowIfFailed(errors, "CharacterTest runtime cleanup failed.");
        }

        private void ConfigureLogColors()
        {
            if (_context?.Logger == null || _runtimeProfile == null)
                return;

            _context.Logger.UseCategoryHeaderColors = _runtimeProfile.LogCategoryColors;
            _context.Logger.SetCategoryHeaderColor("GameManager", _runtimeProfile.GameManagerLogColor);
            _context.Logger.SetCategoryHeaderColor("GameSlice", _runtimeProfile.GameSliceLogColor);
            _context.Logger.SetCategoryHeaderColor("Story", _runtimeProfile.StoryLogColor);
            _context.Logger.SetCategoryHeaderColor("Resources", _runtimeProfile.ResourcesLogColor);
        }

        private static bool HasCommandLineArgument(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
                return false;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
