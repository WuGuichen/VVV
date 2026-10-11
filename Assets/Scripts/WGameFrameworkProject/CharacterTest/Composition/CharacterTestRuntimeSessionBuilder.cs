using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FMODUnity;
using MxFramework.Audio;
using MxFramework.Audio.Config;
using MxFramework.Audio.FMOD;
using MxFramework.Audio.Physical;
using MxFramework.Audio.Physical.Config;
using MxFramework.Audio.Unity;
using MxFramework.DebugUI;
using MxFramework.Input;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Vfx.Unity;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestUiDefaults
    {
#if UNITY_EDITOR
        public static bool EnableDebugUi => true;
#else
        public static bool EnableDebugUi => false;
#endif
        public static bool EnableSkillPanel => false;
        public static bool EnableFairyGuiCombatHud => true;
        public static bool EnableFairyGuiFloatingCombatText => true;
        public static bool EnableFairyGuiGuidance => true;
        public static bool EnableFairyGuiStoryUi => true;
        public static bool EnableFairyGuiMainMenu => true;
        public static bool ShowMainMenuOnStart => true;
        public static bool PauseRuntimeWhenMainMenuOpen => true;
        public static bool ToggleMainMenuWithPauseInput => true;
        public static bool EnableFairyGuiNetworkPanel => true;
        public static bool EnableFairyGuiSettingsPanel => true;
        public static bool EnableFairyGuiAbilityBindingPanel => true;
        public static bool AutoOpenFairyGuiNetworkPanel => true;
        public static DebugUiVisibility DebugInitialVisibility => DebugUiVisibility.Hidden;
        public static bool DebugShowOnStart => false;
        public static int DebugInitialTabIndex => 0;
    }

    internal sealed partial class CharacterTestRuntimeSessionBuilder
    {
        private readonly MonoBehaviour _owner;
        private readonly GameManager _gameManager;
        private readonly CharacterTestRuntimeContext _context;
        private readonly CharacterTestSceneProfile _profile;
        private ulong _tacticalStageRunId;
        private bool _preparingTacticalCandidate;
        private TacticalCard.TacticalBattleSetup _tacticalPreparationOverride;
        private readonly int[] _arenaRuntimeAiPlannerProfileSlots =
            new int[CharacterTestArenaMatchConfig.SlotsPerTeam * 2];
        private readonly bool[] _arenaRuntimeAiPlannerProfileSlotAssigned =
            new bool[CharacterTestArenaMatchConfig.SlotsPerTeam * 2];

        public CharacterTestRuntimeSessionBuilder(
            MonoBehaviour owner,
            CharacterTestRuntimeContext context,
            CharacterTestSceneProfile profile)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _gameManager = owner as GameManager;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _profile = profile ?? CharacterTestSceneProfile.LoadDefault();
        }

        public void ApplyTargetFrameRate()
        {
            EnsureSimulationTimeScale();
            if (_context.HasPreviousTargetFrameRate)
                return;

            _context.PreviousTargetFrameRate = Application.targetFrameRate;
            _context.HasPreviousTargetFrameRate = true;
            Application.targetFrameRate = _profile.TargetFrameRate;
            _context.LogBuffer.Clear()
                .Append("TargetFrameRate applied. previous=")
                .Append(_context.PreviousTargetFrameRate)
                .Append(", current=")
                .Append(_profile.TargetFrameRate);
            _context.Logger?.Info("GameManager", _context.LogBuffer);
        }

        public void EnsureSimulationTimeScale()
        {
            if (Time.timeScale <= 0f)
            {
                Time.timeScale = 1f;
                _context.LogBuffer.Clear()
                    .Append("Simulation timeScale was non-positive; restored to 1.0.");
                _context.Logger?.Info("GameManager", _context.LogBuffer);
            }
        }

        public void RestoreTargetFrameRate()
        {
            if (!_context.HasPreviousTargetFrameRate)
                return;

            Application.targetFrameRate = _context.PreviousTargetFrameRate;
            _context.HasPreviousTargetFrameRate = false;
            _context.LogBuffer.Clear()
                .Append("TargetFrameRate restored. current=")
                .Append(_context.PreviousTargetFrameRate);
            _context.Logger?.Info("GameManager", _context.LogBuffer);
        }

        public double ResolveSimulationDeltaTime()
        {
            return 1d / Mathf.Max(1, _profile.TargetFrameRate);
        }

        public void ConfigureLocalizationRuntime()
        {
            _context.LocalizationRuntime = CharacterTestLocalizationRuntime.Create(
                ResolveLocalizationFilePath(),
                _context.Logger);
            _context.LocalizationRuntime.LocaleChanged += CharacterTestFairyGuiFonts.SetLocale;
        }

        public void ReleaseLocalizationRuntime()
        {
            if (_context.LocalizationRuntime != null)
                _context.LocalizationRuntime.LocaleChanged -= CharacterTestFairyGuiFonts.SetLocale;
            _context.LocalizationRuntime = null;
        }

        public void ConfigureSettingsRuntime()
        {
            _context.SettingsRuntime = CharacterTestSettingsRuntime.Create(
                _owner,
                ResolveSettingsFilePath(),
                _context.Logger,
                _context.LocalizationRuntime?.LocaleOptions);
            _context.LocalizationRuntime?.SyncFromSettings(_context.SettingsRuntime);
        }

        public void ReleaseSettingsRuntime()
        {
            CharacterTestSettingsRuntime settings = _context.SettingsRuntime;
            _context.SettingsRuntime = null;
            settings?.Dispose();
        }

        public IInputProvider ResolveMainMenuInputProvider()
        {
            if (!CharacterTestUiDefaults.EnableFairyGuiMainMenu
                || !CharacterTestUiDefaults.ToggleMainMenuWithPauseInput)
                return null;

            if (_context.InputProvider == null)
                _context.InputProvider = InputProviderResolver.ResolveOrCreateDefault(_owner);
            ApplyEditorMainMenuInputBindings(_context.InputProvider);
            return _context.InputProvider;
        }

        public IInputProvider ResolvePlayerUiInputProvider()
        {
            if (!CharacterTestUiDefaults.EnableFairyGuiMainMenu
                && !CharacterTestUiDefaults.EnableFairyGuiStoryUi
                && !CharacterTestUiDefaults.EnableFairyGuiNetworkPanel
                && _context.ArenaSessionController == null
                && _context.RuntimeAiPlannerDevSceneController == null
                && !ShouldEnableFairyGuiSettingsPanel())
                return null;

            if (_context.InputProvider == null)
                _context.InputProvider = InputProviderResolver.ResolveOrCreateDefault(_owner);

            ApplyEditorMainMenuInputBindings(_context.InputProvider);
            return _context.InputProvider;
        }

        public IInputProvider ResolveProceduralEffectPreviewInputProvider()
        {
            if (_context.InputProvider == null)
                _context.InputProvider = InputProviderResolver.ResolveOrCreateDefault(_owner);

            ApplyEditorMainMenuInputBindings(_context.InputProvider);
            return _context.InputProvider;
        }

        public bool ShouldEnableFairyGuiSettingsPanel()
        {
            return _context.SettingsRuntime != null
                && CharacterTestUiDefaults.EnableFairyGuiSettingsPanel;
        }

        public void RebuildRuntimeSession(
            CharacterTestNetworkSessionKind sessionKind,
            CharacterTestNetworkMode networkMode,
            CharacterTestNetworkSessionOptions options,
            string statusMessage,
            bool preserveNetworkPanel = false,
            CharacterTestArenaMatchConfig arenaMatchConfig = null,
            bool arenaSetupMode = false,
            TacticalCard.TacticalDeployment tacticalDeployment = null)
        {
            using var startupProfile = CharacterTestStartupProfiler.RebuildSession.Auto();
            // Observe legacy mixed sessions before retiring their Slice. They used to
            // expose run 1 without advancing this builder's allocation counter.
            _tacticalStageRunId = HighestKnownTacticalStageRunId();
            if (!_preparingTacticalCandidate)
            {
                _context.ActiveLevelId = string.Empty;
                _context.ActiveLevelContentHash = string.Empty;
                _context.ActiveLevelWorldCache = null;
            }
            _context.NetworkSyncProbe.Reset();
            if (!_preparingTacticalCandidate) _context.NetworkCoordinator?.ResetNetworkSyncProbeAutoRunSessionState();
            _context.NetworkGameplayEnterPending = false;
            _context.NetworkGameplayEntered = false;
            _context.FixedTickDriver.Reset();
            bool restoreMainMenuVisible = _context.MainMenuVisible;
            if (preserveNetworkPanel)
                _context.MainMenuVisible = false;

            var retirementErrors = new List<Exception>();
            if (!_preparingTacticalCandidate)
            {
                CharacterTestCleanupScope.Attempt(retirementErrors, "UiDetach", () => _context.UiRuntime?.DetachSlice());
                CharacterTestCleanupScope.Attempt(retirementErrors, "ThirdPersonCamera", ReleaseThirdPersonCamera);
                CharacterTestCleanupScope.Attempt(retirementErrors, "Slice", DisposeSlice);
            }
            CharacterTestCleanupScope.ThrowIfFailed(retirementErrors, "Previous CharacterTest session retirement failed.");

            var tacticalHud = _gameManager != null ? _gameManager.GetComponent<TacticalCardHud>() : null;
            bool tacticalPreparation = tacticalHud != null && !tacticalHud.IsCardTrialPresentation && tacticalDeployment == null;
            bool tacticalAutoBattle = tacticalDeployment?.Participants != null
                && tacticalDeployment.Participants.Any(value => !value.EntityId.StartsWith("legacy-", StringComparison.Ordinal));
            TacticalCard.CharacterTestTacticalStageAuthoringDocument tacticalStage = tacticalPreparation || tacticalAutoBattle ? null : LoadTacticalStage();
            TacticalCard.CharacterTestTacticalStageAuthoringDocument mixedPlayerTacticalStage =
                tacticalAutoBattle && tacticalDeployment.HasPlayerControlledParticipant
                    ? LoadTacticalStage(cardTrial: tacticalDeployment.CardTrial != null) : null;
            CharacterTestStoryContent storyContent = LoadStoryContent();
            var startup = new CharacterTestCleanupScope();
            try
            {
                GlobalResourceRuntimeServices resources = CreateResourceRuntimeServices();
                CharacterTestCleanupScope.Entry resourceOwnership = startup.Own("PreparedResources", resources.Dispose);
                CharacterTestCleanupScope.Entry audioOwnership = null;
                Action acceptPreparedOwnership = () => { resourceOwnership.Transfer(); audioOwnership?.Transfer(); };
                LogResourceRuntimeBootstrap(resources);
                CharacterTestCharacterPhysicsRuntime physicsRuntime = BuildPhysicsRuntime();
                CharacterTestNavigationSceneInputResult navigationInputResult;
                CharacterTestNavigationSessionBuildInput? navigationBuildInput =
                    CompileNavigationBuildInput(out navigationInputResult);
                physicsRuntime?.SetNavigationBuildInput(navigationBuildInput, navigationInputResult);
                CharacterTestRuntimeAiPlannerProfileLoadResult runtimeAiPlannerProfileLoad =
                    CharacterTestRuntimeAiPlannerProfileJson.LoadFromStreamingAssetsOrDefault(
                        _profile.RuntimeAiPlannerProfilePath);
                CharacterTestMovableNaturalObjectSpawnSpec[] movableNaturalObjectSpawnSpecs =
                    CollectMovableNaturalObjectSpawnSpecs();
                bool useArenaStartup = tacticalPreparation || arenaSetupMode || arenaMatchConfig != null;
                CharacterTestCharacterSpawnSpec[] startupCharacterSpawnSpecs = tacticalPreparation
                    ? BuildTacticalPreparationSpawnSpecs()
                    : tacticalAutoBattle
                        ? BuildTacticalAutoBattleSpawnSpecs(tacticalDeployment)
                    : useArenaStartup
                        ? Array.Empty<CharacterTestCharacterSpawnSpec>()
                        : CollectLevelCharacterSpawnSpecs();
                CharacterTestRuntimeAiPlannerProfileBinding[] runtimeAiPlannerProfileBindings =
                    BuildRuntimeAiPlannerProfileBindings(
                        startupCharacterSpawnSpecs,
                        out startupCharacterSpawnSpecs);
                CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtimeAiBehaviorLoadouts =
                    useArenaStartup && !tacticalPreparation
                        ? CreateArenaBehaviorLoadoutRuntime(
                            arenaMatchConfig,
                            runtimeAiPlannerProfileLoad.Profile,
                            runtimeAiPlannerProfileBindings)
                        : null;
                if (!arenaSetupMode
                    && arenaMatchConfig != null
                    && !arenaMatchConfig.TryBuildSpawnSpecs(
                        runtimeAiBehaviorLoadouts,
                        out startupCharacterSpawnSpecs,
                        out string arenaBuildMessage))
                {
                    startupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>();
                    _context.Logger?.Warning(
                        "GameManager",
                        "CharacterTest arena startup specs were not built: " + arenaBuildMessage);
                }
                runtimeAiPlannerProfileBindings = ReservePlayerBehaviorDeckProfileSlots(
                    startupCharacterSpawnSpecs,
                    runtimeAiPlannerProfileLoad,
                    runtimeAiPlannerProfileBindings,
                    out startupCharacterSpawnSpecs,
                    runtimeAiBehaviorLoadouts?.ProfileBindings);
                CaptureArenaRuntimeAiPlannerProfileSlots(
                    arenaMatchConfig,
                    startupCharacterSpawnSpecs);
                RegisterRuntimeAiBehaviorSelectionAliases(
                    runtimeAiBehaviorLoadouts,
                    startupCharacterSpawnSpecs);
                ICharacterTestAudioCueCatalog audioCueCatalog = null;
                IAudioService audioService = null;
                bool disposeAudioService = false;
                if (_profile.EnableAudioPlayback)
                {
                    audioCueCatalog = LoadAudioCueCatalog();
                    audioService = TryCreateDefaultAudioService(audioCueCatalog);
                    disposeAudioService = audioService != null;
                    if (audioService is IDisposable ownedAudio)
                        audioOwnership = startup.Own("PreparedAudio", ownedAudio.Dispose);
                }

                CharacterTestCameraMode initialCameraMode = ResolveInitialCameraMode(
                    arenaMatchConfig,
                    arenaSetupMode || tacticalPreparation || tacticalAutoBattle);
                CharacterTestCharacterActionCatalog actionCatalog = CharacterTestCharacterActionConfigSource.LoadCatalog();
                if (networkMode == CharacterTestNetworkMode.Disabled)
                {
                    _context.Slice = new GameSlice(new CharacterTestSliceOptions
                    {
                        Logger = _context.Logger,
                        TacticalStage = tacticalStage,
                        MixedPlayerTacticalStage = mixedPlayerTacticalStage,
                        TacticalDeployment = tacticalDeployment,
                        EnableCharacterSimulation = true,
                        EnableLocalInputCommands = !tacticalPreparation && !tacticalAutoBattle,
                        CreateCharacterViews = true,
                        TacticalStageRunId = tacticalStage != null || mixedPlayerTacticalStage != null
                            ? tacticalDeployment?.CardTrial?.StageRunId ?? ReserveCardTrialStageRunId()
                            : _tacticalStageRunId,
                        StoryContent = storyContent,
                        EnableStoryBootstrap = !tacticalPreparation && !tacticalAutoBattle,
                        Resources = resources,
                        IncomingOwnershipAccepted = acceptPreparedOwnership,
                        BaseResourcePreloadGroupId = _profile.BaseResourcePreloadGroupId,
                        InitialCameraMode = initialCameraMode,
                        ActionCatalog = actionCatalog,
                        PhysicsRuntime = physicsRuntime,
                        NavigationBuildInput = navigationBuildInput,
                        NavigationInputResult = navigationInputResult,
                        EnableTraceFileWrite = _profile.EnableTraceFileWrite,
                        AudioService = audioService,
                        AudioCueCatalog = audioCueCatalog,
                        DisposeAudioService = disposeAudioService,
                        RuntimeAiPlannerProfile = runtimeAiPlannerProfileLoad.Profile,
                        RuntimeAiPlannerProfileReport = runtimeAiPlannerProfileLoad.Report,
                        RuntimeAiPlannerProfileBindings = runtimeAiPlannerProfileBindings,
                        RuntimeAiBehaviorLoadouts = runtimeAiBehaviorLoadouts,
                        MovableNaturalObjectSpawnSpecs = tacticalPreparation ? Array.Empty<CharacterTestMovableNaturalObjectSpawnSpec>() : movableNaturalObjectSpawnSpecs,
                        StartupCharacterSpawnSpecs = startupCharacterSpawnSpecs,
                        GameplayTriggerSceneCache =
                            tacticalPreparation ? null : _gameManager?.GameplayTriggerSceneCache,
                        SuppressMissingStartupSpawnWarning =
                            tacticalPreparation || _context.RuntimeAiPlannerDevSceneController != null,
                        PresentationAllyTeamId = ResolvePresentationAllyTeamId(arenaMatchConfig)
                    });
                }
                else
                {
                    CharacterTestNetworkSessionFactoryOptions factoryOptions =
                        CreateNetworkSessionFactoryOptions(networkMode, options);
                    factoryOptions.ActionCatalog = actionCatalog;
                    factoryOptions.AudioService = audioService;
                    factoryOptions.AudioCueCatalog = audioCueCatalog;
                    factoryOptions.DisposeAudioService = disposeAudioService;
                    factoryOptions.RuntimeAiPlannerProfile = runtimeAiPlannerProfileLoad.Profile;
                    factoryOptions.RuntimeAiPlannerProfileReport = runtimeAiPlannerProfileLoad.Report;
                    factoryOptions.RuntimeAiPlannerProfileBindings = runtimeAiPlannerProfileBindings;
                    factoryOptions.RuntimeAiBehaviorLoadouts = runtimeAiBehaviorLoadouts;
                    factoryOptions.MovableNaturalObjectSpawnSpecs = movableNaturalObjectSpawnSpecs;
                    factoryOptions.StartupCharacterSpawnSpecs = startupCharacterSpawnSpecs;
                    factoryOptions.SuppressMissingStartupSpawnWarning =
                        _context.RuntimeAiPlannerDevSceneController != null;
                    factoryOptions.PresentationAllyTeamId = ResolvePresentationAllyTeamId(arenaMatchConfig);
                    if (_context.RuntimeAiPlannerDevSceneController != null
                        && networkMode == CharacterTestNetworkMode.Loopback)
                    {
                        factoryOptions.PeerControlMode =
                            CharacterTestNetworkPeerControlMode.Unbound;
                        factoryOptions.RuntimeAiPlannerSessionAuthority =
                            CreateRuntimeAiLabSessionAuthorityConfiguration(actionCatalog);
                    }
                    // The current dual-slice runners accept provisional input ownership at entry.
                    // Legacy endpoint factories keep their existing ownership contract.
                    if (networkMode == CharacterTestNetworkMode.Loopback
                        || networkMode == CharacterTestNetworkMode.UnityTransportLoopback)
                        factoryOptions.IncomingOwnershipAccepted = acceptPreparedOwnership;
                    else
                        acceptPreparedOwnership();
                    _context.NetworkRunner = CharacterTestNetworkSessionFactory.Create(
                        factoryOptions,
                        _context.Logger,
                        storyContent,
                        resources,
                        _profile.BaseResourcePreloadGroupId,
                        initialCameraMode,
                        physicsRuntime);
                    _context.Slice = _context.NetworkRunner.Slice;
                }

                if (_preparingTacticalCandidate)
                {
                    // Core creation/validation has succeeded. Live UI, camera and control ownership
                    // are untouched until the caller accepts this candidate.
                    startup.Dispose();
                    return;
                }
                LogTraceRecorderState();
                var tacticalRuntime = _context.Slice.Services.Modules.TacticalStage;
                var mixedPlayerRuntime = _context.Slice.Services.Modules.MixedPlayerTacticalStage;
                TacticalCard.TacticalStageFlowCoordinator playerStage = tacticalRuntime?.Stage ?? mixedPlayerRuntime?.Stage;
                TacticalCard.TacticalCommandPump playerCommands = tacticalRuntime?.Commands ?? mixedPlayerRuntime?.Commands;
                _gameManager?.BindTacticalControl(playerStage, playerCommands);
                _gameManager?.GetComponent<TacticalCardHud>()?.Bind(playerStage, playerCommands,
                    _context.Slice.Services.Modules.StoryFlowBridge, EnsureCursorController(), _context.InputProvider);
                _context.Slice.Services.Modules.AbilityHotbar?.LoadFromFile(ResolveAbilityHotbarFilePath());
                ConfigureMovableNaturalObjectViewBridge();
                _context.Slice.Services.Presentation.SetHeadPadDisplayMode(_profile.HeadPadDisplayMode);
                _context.ActiveNetworkSessionKind = sessionKind;
                _context.ActiveNetworkSessionOptions = options?.Clone()
                    ?? _context.NetworkCoordinator.CreateDefaultNetworkSessionOptions(sessionKind);
                _context.LastNetworkSessionMessage = string.IsNullOrWhiteSpace(statusMessage)
                    ? sessionKind.ToString()
                    : statusMessage;
                InitializeThirdPersonCamera(initialCameraMode, tacticalAutoBattle);
                CharacterTestRuntimeAiLabCameraResolver.NotifySceneCameraChanged(
                    UnityEngine.Camera.main);
                EnsureFmodStudioListener();
                IAudioService sessionAudioService = _context.Slice?.Services.Modules.AudioService;
                _context.AudioMixController = CreateAudioMixController(sessionAudioService);
                _context.AudioMixSnapshotController = sessionAudioService != null
                    ? new AudioMixSnapshotController(sessionAudioService)
                    : null;
                _context.AudioMusicStateController = CreateAudioMusicStateController(sessionAudioService);
                _context.AudioAmbienceController = sessionAudioService != null
                    ? new AudioPersistentEventController(sessionAudioService)
                    : null;
                _context.SettingsRuntime?.AttachAudioService(sessionAudioService, _context.AudioMixController);
                if (!tacticalPreparation && !tacticalAutoBattle)
                {
                    ConfigurePlayerBehaviorDeckService(
                        startupCharacterSpawnSpecs,
                        arenaMatchConfig,
                        networkMode == CharacterTestNetworkMode.Loopback
                            && _context.RuntimeAiPlannerDevSceneController != null,
                        actionCatalog: actionCatalog);
                }
                else
                {
                    _context.PlayerBehaviorDeckService = null;
                }
                if (tacticalRuntime == null) ConfigureUiRuntime(storyContent, tacticalPreparation || tacticalAutoBattle);
                if (preserveNetworkPanel)
                    RestoreNetworkPanelAfterRuntimeRebuild(restoreMainMenuVisible);
                // All provisional resource/audio ownership has moved to the published slice.
                startup.Dispose();
            }
            catch (Exception failure)
            {
                var errors = new List<Exception>();
                CharacterTestCleanupScope.Attempt(errors, "FailedSessionUi", ReleaseUiRuntime);
                CharacterTestCleanupScope.Attempt(errors, "FailedSessionCamera", ReleaseThirdPersonCamera);
                CharacterTestCleanupScope.Attempt(errors, "FailedSession", DisposeSlice);
                startup.Cleanup(errors);
                _context.MainMenuVisible = restoreMainMenuVisible;
                _context.LastNetworkSessionMessage = "Session startup failed: " + failure.Message;
                if (errors.Count != 0)
                {
                    errors.Insert(0, failure);
                    throw new AggregateException("CharacterTest session startup and rollback failed.", errors);
                }
                throw;
            }
        }

        internal TacticalCard.CharacterTestTacticalStageAuthoringDocument LoadTacticalStage(bool cardTrial = false)
        {
            var hud = _gameManager != null ? _gameManager.GetComponent<TacticalCardHud>() : null;
            if (hud == null || hud.IsCardTrialPresentation && !cardTrial) return null;
            var cache = _gameManager.CharacterSceneCache;
            // The independent Lab scene has no tactical scene cache. Its trial consumes only
            // the stage module from the existing published document; scene physics stays in the Lab.
            string path = Application.isEditor && !cardTrial ? cache.AuthoringJsonPath
                : Path.Combine(Application.streamingAssetsPath, "MxFramework/CharacterTest/tactical_slice.level.json");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new InvalidOperationException("Tactical scene requires its published level document.");
            var document = MxFramework.Config.Levels.MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(path));
            if (!cardTrial && MxFramework.Config.Levels.MxLevelAuthoringJson.ComputeContentHash(document) != cache.AuthoringContentHash)
                throw new InvalidOperationException("Tactical level document and scene cache hashes differ. Regenerate the scene.");
            if (!document.TryGetModule(TacticalCard.CharacterTestTacticalStageAuthoringDocument.ModuleId, out var module) ||
                module.SchemaVersion != TacticalCard.CharacterTestTacticalStageAuthoringDocument.CurrentSchemaVersion)
                throw new InvalidOperationException("Tactical level module is missing or unsupported.");
            return module.ReadPayload<TacticalCard.CharacterTestTacticalStageAuthoringDocument>();
        }

        public void RebuildArenaSetupSession(
            CharacterTestArenaMatchConfig arenaMatchConfig,
            string statusMessage)
        {
            RebuildRuntimeSession(
                CharacterTestNetworkSessionKind.Offline,
                CharacterTestNetworkMode.Disabled,
                null,
                string.IsNullOrWhiteSpace(statusMessage) ? "Arena setup." : statusMessage,
                preserveNetworkPanel: false,
                arenaMatchConfig: arenaMatchConfig != null ? arenaMatchConfig.Clone() : null,
                arenaSetupMode: true);
        }

        public void RebuildArenaSetupSession(string statusMessage)
        {
            RebuildArenaSetupSession(null, statusMessage);
        }

        public void RebuildArenaBattleSession(
            CharacterTestArenaMatchConfig arenaMatchConfig,
            string statusMessage)
        {
            RebuildRuntimeSession(
                CharacterTestNetworkSessionKind.Offline,
                CharacterTestNetworkMode.Disabled,
                null,
                string.IsNullOrWhiteSpace(statusMessage) ? "Arena battle running." : statusMessage,
                preserveNetworkPanel: false,
                arenaMatchConfig: arenaMatchConfig != null ? arenaMatchConfig.Clone() : null,
                arenaSetupMode: false);
        }

        public void RebuildRuntimeAiLabBattleSession(
            CharacterTestArenaMatchConfig arenaMatchConfig,
            string statusMessage)
        {
            RebuildRuntimeSession(
                CharacterTestNetworkSessionKind.Offline,
                CharacterTestNetworkMode.Loopback,
                null,
                string.IsNullOrWhiteSpace(statusMessage) ? "Runtime AI Lab running." : statusMessage,
                preserveNetworkPanel: false,
                arenaMatchConfig: arenaMatchConfig != null ? arenaMatchConfig.Clone() : null,
                arenaSetupMode: false);
        }

        private static CharacterTestRuntimeAiPlannerSessionAuthorityConfiguration
            CreateRuntimeAiLabSessionAuthorityConfiguration(
                CharacterTestCharacterActionCatalog actionCatalog = null)
        {
            using var startupProfile = CharacterTestStartupProfiler.SessionAuthority.Auto();
            if (!CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreateCatalog(
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
                    out _,
                    out string catalogError,
                    actionCatalog: actionCatalog))
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab replay authority catalog is unavailable: " + catalogError);
            }
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlockedResult =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    Array.Empty<string>());
            if (!unlockedResult.Success)
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab replay authority unlock collection is unavailable.");
            }
            int runtimeContractVersion =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
            return new CharacterTestRuntimeAiPlannerSessionAuthorityConfiguration(
                catalog,
                runtimeContractVersion,
                requesterPeerId => new CharacterTestRuntimeAiPlannerSessionAuthorityContext(
                    requesterPeerId,
                    Array.Empty<string>(),
                    unlockedResult.Collection,
                    catalog,
                    runtimeContractVersion,
                    selectionLocked: true));
        }

        private CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime CreateArenaBehaviorLoadoutRuntime(
            CharacterTestArenaMatchConfig arenaMatchConfig,
            CharacterTestRuntimeAiPlannerProfile runtimeProfile,
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> runtimeAiPlannerProfileBindings)
        {
            using var startupProfile = CharacterTestStartupProfiler.ArenaLoadouts.Auto();
            CharacterTestArenaBehaviorGroupDraftStore drafts = arenaMatchConfig?.BehaviorGroups;
            if (drafts != null)
            {
                return drafts.CreateRuntime(
                    runtimeProfile,
                    runtimeAiPlannerProfileBindings,
                    _context.Logger);
            }

            return CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime.CreateDefault(
                runtimeProfile,
                runtimeAiPlannerProfileBindings,
                _context.Logger);
        }

        private static int ResolvePresentationAllyTeamId(CharacterTestArenaMatchConfig arenaMatchConfig)
        {
            if (arenaMatchConfig == null)
                return 0;

            return arenaMatchConfig.PresentationTeamId == CharacterTestArenaMatchConfig.Team2Id
                ? CharacterTestArenaMatchConfig.Team2Id
                : CharacterTestArenaMatchConfig.Team1Id;
        }

        public void ReleaseAll()
        {
            var errors = new List<Exception>();
            CharacterTestCleanupScope.Attempt(errors, "UiRuntime", ReleaseUiRuntime);
            CharacterTestCleanupScope.Attempt(errors, "SettingsRuntime", ReleaseSettingsRuntime);
            CharacterTestCleanupScope.Attempt(errors, "LocalizationRuntime", ReleaseLocalizationRuntime);
            CharacterTestCleanupScope.Attempt(errors, "ThirdPersonCamera", ReleaseThirdPersonCamera);
            CharacterTestCleanupScope.Attempt(errors, "Slice", DisposeSlice);
            CharacterTestCleanupScope.Attempt(errors, "CursorController", ReleaseCursorController);
            _context.NetworkAutomationRunner = null;
            _context.InputProvider = null;
            CharacterTestCleanupScope.Attempt(errors, "TargetFrameRate", RestoreTargetFrameRate);
            CharacterTestCleanupScope.Attempt(errors, "TimeScale", EnsureSimulationTimeScale);
            CharacterTestCleanupScope.ThrowIfFailed(errors, "CharacterTest session cleanup failed.");
        }

        internal void RetireForTacticalReset()
        {
            var errors = new List<Exception>();
            CharacterTestCleanupScope.Attempt(errors, "UiRuntime", ReleaseUiRuntime);
            CharacterTestCleanupScope.Attempt(errors, "Slice", DisposeSlice);
            CharacterTestCleanupScope.ThrowIfFailed(errors, "CharacterTest tactical retirement failed.");
        }

        public SceneFlowResult RequestSceneLoad(
            string sceneKey,
            SceneFlowLoadMode loadMode = SceneFlowLoadMode.Single,
            bool unloadPreviousScene = false)
        {
            if (_context.Slice == null)
            {
                return SceneFlowResult.Failed(
                    sceneKey,
                    SceneFlowOperationType.Load,
                    new SceneFlowError(
                        SceneFlowErrorCode.InvalidRequest,
                        sceneKey,
                        "CharacterTest GameSlice is not running."));
            }

            SceneFlowResult result = _context.Slice.Services.StoryFlow.RequestSceneLoad(
                sceneKey,
                loadMode,
                unloadPreviousScene);
            LogSceneFlowRequest("load", result);
            return result;
        }

        public SceneFlowResult RequestSceneUnload(string sceneKey)
        {
            if (_context.Slice == null)
            {
                return SceneFlowResult.Failed(
                    sceneKey,
                    SceneFlowOperationType.Unload,
                    new SceneFlowError(
                        SceneFlowErrorCode.InvalidRequest,
                        sceneKey,
                        "CharacterTest GameSlice is not running."));
            }

            SceneFlowResult result = _context.Slice.Services.StoryFlow.RequestSceneUnload(sceneKey);
            LogSceneFlowRequest("unload", result);
            return result;
        }

        public SceneFlowSnapshot CaptureSceneFlowSnapshot()
        {
            return _context.Slice?.Services.StoryFlow.CaptureSceneFlowSnapshot();
        }

        private CharacterTestStoryContent LoadStoryContent()
        {
            using var startupProfile = CharacterTestStartupProfiler.LoadStory.Auto();
            return CharacterTestStoryContentLoader.Load(
                _profile.UseExternalStoryAuthoring,
                _profile.StoryDraftStreamingAssetsPath,
                _context.Logger);
        }

        private GlobalResourceRuntimeServices CreateResourceRuntimeServices()
        {
            using var startupProfile = CharacterTestStartupProfiler.Resources.Auto();
            var options = new GlobalResourceRuntimeOptions
            {
                Mode = ResolveResourceMode(_profile.ResourceMode, Application.isEditor),
                AllowMissingPreloadGroupInEditor = true
            };
            ResourceCatalog applicationCatalog = ConfigureCharacterTestVfxResources(
                out MemoryResourceProvider applicationProvider);
            GlobalResourceRuntimeServices services = null;
            try
            {
                services = GlobalResourceRuntimeServices.Create(options);
                if (applicationProvider != null)
                    services.RegisterOwnedProvider(applicationProvider);
                if (applicationCatalog != null)
                    RegisterApplicationVfxCatalog(services.ResourceManager, applicationCatalog);
                return services;
            }
            catch
            {
                services?.Dispose();
                applicationProvider?.Dispose();
                throw;
            }
        }

        /// <summary>Published blade trails own their key; scene memory supplies an unpublished fallback only.</summary>
        /// <remarks>AllocByDesign: session startup catalog composition, never presentation Tick.</remarks>
        internal static void RegisterApplicationVfxCatalog(ResourceManager manager, ResourceCatalog catalog)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var bladeTrailKey = new ResourceKey(CharacterTestBladeTrailVfxIds.ResourceId,
                ResourceTypeIds.GameObject, packageId: CharacterTestBladeTrailVfxIds.ResourcePackageId);
            var entries = new List<ResourceCatalogEntry>(catalog.Entries.Count);
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                ResourceKey key = entry.CreateKey(catalog.PackageId);
                if (key.Equals(bladeTrailKey) && manager.Contains(key))
                    continue;
                entries.Add(entry);
            }
            // Other collisions still go through ResourceManager's normal atomic rejection.
            manager.AddCatalog(new ResourceCatalog(catalog.CatalogId, catalog.PackageId, entries));
        }

        internal static GlobalResourceRuntimeMode ResolveResourceMode(
            GlobalResourceRuntimeMode configuredMode,
            bool isEditor)
        {
            if (configuredMode == GlobalResourceRuntimeMode.Editor && !isEditor)
                return GlobalResourceRuntimeMode.Offline;

            return configuredMode;
        }

        private ResourceCatalog ConfigureCharacterTestVfxResources(
            out MemoryResourceProvider applicationProvider)
        {
            applicationProvider = null;
            IVfxUnityPrefabBindingSource publishedBindings =
                VfxUnityPrefabBindingSourceRegistry.Current ?? _profile.PublishedVfxBindings;
            if (_profile.FireballVfxPrefab == null)
            {
                _context.Logger?.Warning(
                    "CharacterTest.VFX",
                    "CharacterTest VFX resources were not registered because the active "
                    + "CharacterTestSceneProfile has no Fireball VFX prefab. Assign the shared "
                    + "CharacterTest scene profile before starting a playable session.");
                return null;
            }

            applicationProvider = CharacterTestVfxDefinitionSource.CreateMemoryProvider(
                    _profile.FireballVfxPrefab,
                    _profile.FireballBurstFlightVfxPrefab,
                    _profile.FireballBurstImpactVfxPrefab,
                    _profile.FlameLanceFlightVfxPrefab,
                    _profile.FlameLanceImpactVfxPrefab,
                    _profile.BeamLanceBeamVfxPrefab,
                    _profile.BeamLanceImpactVfxPrefab,
                    _profile.BeamLanceChargeVfxPrefab,
                    _profile.ChannelBeamChargeVfxPrefab,
                    _profile.ChannelBeamBeamVfxPrefab,
                    _profile.ChannelBeamImpactVfxPrefab,
                    _profile.LaserLanceChargeVfxPrefab,
                    _profile.LaserLancePathVfxPrefab,
                    _profile.LaserLanceImpactVfxPrefab,
                    _profile.ChainLightningChargeVfxPrefab,
                    _profile.ChainLightningArcVfxPrefab,
                    _profile.ChainLightningImpactVfxPrefab,
                    _profile.PoisonBottleFlightVfxPrefab,
                    _profile.PoisonBottleShatterVfxPrefab,
                    _profile.PoisonBottlePoolVfxPrefab,
                    _profile.PoisonedTickVfxPrefab,
                    _profile.GuardAuraVfxPrefab,
                    _profile.PhysicalShieldVfxPrefab,
                    _profile.FireballExplosionImpactVfxPrefab,
                    _profile.TornadoFlightVfxPrefab,
                    _profile.TornadoImpactVfxPrefab,
                    _profile.TornadoChargeVfxPrefab,
                    _profile.TornadoTickVfxPrefab,
                    areaConfinementZonePrefab: _profile.AreaConfinementZoneVfxPrefab,
                    arcMineFlightPrefab: _profile.ArcMineFlightVfxPrefab,
                    arcMineDeployPrefab: _profile.ArcMineDeployVfxPrefab,
                    arcMineFusePrefab: _profile.ArcMineFuseVfxPrefab,
                    arcMineTriggerPrefab: _profile.ArcMineTriggerVfxPrefab,
                    publishedBindings: publishedBindings,
                    runeTurretDeployPrefab: _profile.RuneTurretDeployVfxPrefab,
                    runeTurretIdlePrefab: _profile.RuneTurretIdleVfxPrefab,
                    runeTurretFirePrefab: _profile.RuneTurretFireVfxPrefab,
                    runeTurretFlightPrefab: _profile.RuneTurretFlightVfxPrefab,
                    runeTurretImpactPrefab: _profile.RuneTurretImpactVfxPrefab,
                    runeTurretExpirePrefab: _profile.RuneTurretExpireVfxPrefab,
                    bladeTrailPrefab: _profile.BladeTrailVfxPrefab);
            return CharacterTestVfxDefinitionSource.CreateResourceCatalog();
        }

        private CharacterTestCameraMode ResolveInitialCameraMode(
            CharacterTestArenaMatchConfig arenaMatchConfig,
            bool arenaSetupMode)
        {
            if (arenaSetupMode)
                return CharacterTestCameraMode.MultiTargetAuto;

            if (arenaMatchConfig != null
                && CharacterTestArenaSessionController.IsArenaCameraMode(arenaMatchConfig.PreferredCameraMode))
            {
                return arenaMatchConfig.PreferredCameraMode;
            }

            return _profile.CameraMode;
        }

        private void InitializeThirdPersonCamera(CharacterTestCameraMode initialCameraMode,
            bool tacticalBattleAutoFraming = false)
        {
            if (!_profile.EnableThirdPersonCamera)
                return;

            _context.ThirdPersonCameraBridge = _owner.GetComponent<CharacterTestThirdPersonCameraRigBridge>();
            if (_context.ThirdPersonCameraBridge == null)
                _context.ThirdPersonCameraBridge = _owner.gameObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();

            _context.ThirdPersonCameraBridge.ConfigureRuntimeAiLabAutoFraming(
                _context.RuntimeAiPlannerDevSceneController != null);
            _context.ThirdPersonCameraBridge.ConfigureTacticalBattleAutoFraming(tacticalBattleAutoFraming);
            _context.ThirdPersonCameraBridge.CameraMode = initialCameraMode;
            _context.ThirdPersonCameraBridge.Bind(
                _context.Slice.Services,
                _context.Logger,
                null,
                EnsureCursorController());
            _context.Slice.Services.Presentation.SetCharacterMovementBasisProvider(_context.ThirdPersonCameraBridge);
        }

        private void EnsureFmodStudioListener()
        {
            if (!_profile.EnableAudioPlayback)
                return;

            UnityEngine.Camera listenerCamera = UnityEngine.Camera.main;
            if (listenerCamera == null)
            {
                _context.Logger?.Warning(
                    "CharacterTest.Audio",
                    "FMOD StudioListener was not configured: no main camera found.");
                return;
            }

            if (!listenerCamera.TryGetComponent(out StudioListener _))
            {
                listenerCamera.gameObject.AddComponent<StudioListener>();
                _context.Logger?.Info(
                    "CharacterTest.Audio",
                    "FMOD StudioListener added to " + listenerCamera.name + ".");
            }

            if (_context.RuntimeAiPlannerDevSceneController != null
                && !listenerCamera.TryGetComponent(out UnityEngine.AudioListener _))
            {
                listenerCamera.gameObject.AddComponent<UnityEngine.AudioListener>();
            }
        }

        private CharacterTestCharacterPhysicsRuntime BuildPhysicsRuntime()
        {
            using var startupProfile = CharacterTestStartupProfiler.Physics.Auto();
            CharacterTestWorldObjectSceneCache worldObjectCache = _gameManager?.WorldObjectSceneCache;
            if (worldObjectCache != null && worldObjectCache.StaticColliderInstanceCount > 0)
            {
                CharacterTestCharacterPhysicsRuntime cacheRuntime =
                    CharacterTestCombatPhysicsSceneBridge.BuildRuntime(
                        _gameManager.WorldObjectSceneRoot,
                        worldObjectCache,
                        _gameManager.PhysicsSettings);
                if (cacheRuntime == null)
                {
                    _context.Logger?.Info(
                        "GameManager",
                        "CharacterTest GameManager scene cache did not build a runtime world; using planar fallback motion.");
                    return null;
                }

                _context.LogBuffer.Clear()
                    .Append("CharacterTest framework physics enabled from GameManager scene cache. source=")
                    .Append(cacheRuntime.SourceDescription)
                    .Append(", staticColliders=")
                    .Append(cacheRuntime.StaticColliderCount)
                    .Append(", staticMask=")
                    .Append(cacheRuntime.StaticObstacleLayerMask.Value)
                    .Append(", dynamicCharacterMask=")
                    .Append(cacheRuntime.DynamicCharacterObstacleLayerMask.Value)
                    .Append(", motionMask=")
                    .Append(cacheRuntime.MotionObstacleLayerMask.Value);
                _context.Logger?.Info("GameManager", _context.LogBuffer);
                return cacheRuntime;
            }

            CharacterTestCombatPhysicsSceneBridge[] bridges =
                UnityEngine.Object.FindObjectsByType<CharacterTestCombatPhysicsSceneBridge>(UnityEngine.FindObjectsInactive.Exclude);
            if (bridges == null || bridges.Length == 0)
            {
                _context.Logger?.Info(
                    "GameManager",
                    "CharacterTest framework physics scene not configured; using planar fallback motion.");
                return null;
            }

            if (bridges.Length > 1)
            {
                _context.Logger?.Warning(
                    "GameManager",
                    "CharacterTest found multiple combat physics scene bridges; using planar fallback motion to avoid ambiguous runtime physics.");
                return null;
            }

            CharacterTestCharacterPhysicsRuntime runtime = bridges[0].BuildRuntime();
            if (runtime == null)
            {
                _context.Logger?.Info(
                    "GameManager",
                    "CharacterTest framework physics scene did not build a runtime world; using planar fallback motion.");
                return null;
            }

            _context.LogBuffer.Clear()
                .Append("CharacterTest framework physics enabled. source=")
                .Append(runtime.SourceDescription)
                .Append(", staticColliders=")
                .Append(runtime.StaticColliderCount)
                .Append(", staticMask=")
                .Append(runtime.StaticObstacleLayerMask.Value)
                .Append(", dynamicCharacterMask=")
                .Append(runtime.DynamicCharacterObstacleLayerMask.Value)
                .Append(", motionMask=")
                .Append(runtime.MotionObstacleLayerMask.Value);
            _context.Logger?.Info("GameManager", _context.LogBuffer);
            return runtime;
        }

        private CharacterTestNavigationSessionBuildInput? CompileNavigationBuildInput(
            out CharacterTestNavigationSceneInputResult result)
        {
            if (!CharacterTestNavigationSceneInputCompiler.TryCompile(
                    _gameManager?.NavigationSettings,
                    _gameManager?.WorldObjectSceneCache,
                    out CharacterTestNavigationSessionBuildInput input,
                    out result))
            {
                _context.LogBuffer.Clear()
                    .Append("CharacterTest navigation graph input unavailable. status=")
                    .Append(result.Status.ToString())
                    .Append(", sourceIndex=")
                    .Append(result.SourceIndex)
                    .Append(", stableId=")
                    .Append(result.StableId);
                _context.Logger?.Info("GameManager", _context.LogBuffer);
                return null;
            }

            _context.LogBuffer.Clear()
                .Append("CharacterTest navigation graph input compiled. revision=")
                .Append(input.Revision)
                .Append(", boundsMm=")
                .Append(input.MinimumXMillimeters)
                .Append(',')
                .Append(input.MinimumZMillimeters)
                .Append("..")
                .Append(input.MaximumXMillimeters)
                .Append(',')
                .Append(input.MaximumZMillimeters)
                .Append(", cellMm=")
                .Append(input.CellSizeMillimeters);
            _context.Logger?.Info("GameManager", _context.LogBuffer);
            return input;
        }

        private CharacterTestMovableNaturalObjectSpawnSpec[] CollectMovableNaturalObjectSpawnSpecs()
        {
            CharacterTestWorldObjectSceneCache cache = _gameManager?.WorldObjectSceneCache;
            if (cache != null && cache.MovableInstanceCount > 0)
            {
                CharacterTestMovableNaturalObjectSpawnSpec[] cacheSpecs =
                    cache.CollectMovableSpawnSpecs(out string[] cacheDiagnostics);
                if (cacheDiagnostics != null && cacheDiagnostics.Length > 0)
                {
                    for (int i = 0; i < cacheDiagnostics.Length; i++)
                        _context.Logger?.Warning("GameManager", "Movable scene cache diagnostic: " + cacheDiagnostics[i]);
                }

                _context.LogBuffer.Clear()
                    .Append("CharacterTest movable natural object GameManager scene cache enabled. count=")
                    .Append(cacheSpecs.Length);
                _context.Logger?.Info("GameManager", _context.LogBuffer);
                return cacheSpecs;
            }

            CharacterTestMovableNaturalObjectSceneSource source =
                ResolveMovableNaturalObjectSceneSource();
            CharacterTestMovableNaturalObjectSpawnSpec[] specs = source != null
                ? source.CollectSpawnSpecs()
                : Array.Empty<CharacterTestMovableNaturalObjectSpawnSpec>();
            _context.LogBuffer.Clear()
                .Append("CharacterTest movable natural object scene source ")
                .Append(source != null ? "enabled" : "missing")
                .Append(". count=")
                .Append(specs.Length);
            _context.Logger?.Info("GameManager", _context.LogBuffer);
            return specs;
        }

        private CharacterTestCharacterSpawnSpec[] CollectLevelCharacterSpawnSpecs()
        {
            CharacterTestSceneCharacterCache characterCache = _gameManager?.CharacterSceneCache;
            if (characterCache == null)
            {
                _context.Logger?.Warning(
                    "GameManager",
                    "CharacterTest scene character cache is missing; no startup characters will be spawned.");
                return Array.Empty<CharacterTestCharacterSpawnSpec>();
            }

            if (characterCache.CharacterInstanceCount <= 0)
            {
                _context.Logger?.Warning(
                    "GameManager",
                    "CharacterTest scene character cache is empty; use Level Editor > CharacterTest Characters > save and apply to scene.");
                return Array.Empty<CharacterTestCharacterSpawnSpec>();
            }

            CharacterTestCharacterSpawnSpec[] characterSpecs =
                characterCache.CollectCharacterSpawnSpecs(out string[] characterDiagnostics);
            if (characterDiagnostics != null && characterDiagnostics.Length > 0)
            {
                for (int i = 0; i < characterDiagnostics.Length; i++)
                    _context.Logger?.Warning("GameManager", "Scene character cache diagnostic: " + characterDiagnostics[i]);
            }

            _context.LogBuffer.Clear()
                .Append("CharacterTest startup characters from GameManager character cache. characters=")
                .Append(characterSpecs.Length);
            _context.Logger?.Info("GameManager", _context.LogBuffer);
            return characterSpecs;
        }

        private CharacterTestRuntimeAiPlannerProfileBinding[] BuildRuntimeAiPlannerProfileBindings(
            CharacterTestCharacterSpawnSpec[] sourceSpecs,
            out CharacterTestCharacterSpawnSpec[] assignedSpecs)
        {
            assignedSpecs = sourceSpecs ?? Array.Empty<CharacterTestCharacterSpawnSpec>();
            if (assignedSpecs.Length == 0)
                return Array.Empty<CharacterTestRuntimeAiPlannerProfileBinding>();

            string defaultPath = CharacterTestRuntimeAiPlannerProfileJson.NormalizeStreamingAssetsRelativePath(
                _profile.RuntimeAiPlannerProfilePath);
            var slotsByPath = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [defaultPath] = 0
            };
            var bindings = new List<CharacterTestRuntimeAiPlannerProfileBinding>();
            CharacterTestCharacterSpawnSpec[] remapped = null;
            for (int i = 0; i < assignedSpecs.Length; i++)
            {
                CharacterTestCharacterSpawnSpec spec = assignedSpecs[i];
                int slot = 0;
                if (spec.ControlOwner == CharacterTestCharacterControlOwner.AiControlled
                    && spec.RuntimeAiPlannerProfileSlot > 0
                    && string.IsNullOrWhiteSpace(spec.RuntimeAiPlannerProfilePath))
                {
                    slot = spec.RuntimeAiPlannerProfileSlot;
                    if (!bindings.Any(binding => binding.Slot == slot))
                    {
                        CharacterTestRuntimeAiPlannerProfileLoadResult load =
                            CharacterTestRuntimeAiPlannerProfileJson.LoadFromStreamingAssetsOrDefault(defaultPath);
                        bindings.Add(new CharacterTestRuntimeAiPlannerProfileBinding(
                            slot, load.SourcePath, load.Profile, load.Report));
                    }
                }
                else if (spec.ControlOwner == CharacterTestCharacterControlOwner.AiControlled
                    && !string.IsNullOrWhiteSpace(spec.RuntimeAiPlannerProfilePath))
                {
                    string profilePath =
                        CharacterTestRuntimeAiPlannerProfileJson.NormalizeStreamingAssetsRelativePath(
                            spec.RuntimeAiPlannerProfilePath);
                    if (!slotsByPath.TryGetValue(profilePath, out slot))
                    {
                        slot = bindings.Count + 1;
                        CharacterTestRuntimeAiPlannerProfileLoadResult load =
                            CharacterTestRuntimeAiPlannerProfileJson.LoadFromStreamingAssetsOrDefault(profilePath);
                        bindings.Add(new CharacterTestRuntimeAiPlannerProfileBinding(
                            slot,
                            load.SourcePath,
                            load.Profile,
                            load.Report));
                        slotsByPath.Add(profilePath, slot);
                        _context.LogBuffer.Clear()
                            .Append("CharacterTest Runtime AI Planner character profile loaded. slot=")
                            .Append(slot)
                            .Append(", path=")
                            .Append(load.SourcePath)
                            .Append(", fallback=")
                            .Append(load.UsedFallback ? "true" : "false");
                        _context.Logger?.Info("GameManager", _context.LogBuffer);
                    }
                }

                if (spec.RuntimeAiPlannerProfileSlot != slot)
                {
                    remapped ??= CopySpawnSpecs(assignedSpecs);
                    remapped[i] = spec.WithRuntimeAiPlannerProfileSlot(slot);
                }
            }

            if (remapped != null)
                assignedSpecs = remapped;
            return bindings.Count == 0
                ? Array.Empty<CharacterTestRuntimeAiPlannerProfileBinding>()
                : bindings.ToArray();
        }

        internal static CharacterTestRuntimeAiPlannerProfileBinding[] ReservePlayerBehaviorDeckProfileSlots(
            CharacterTestCharacterSpawnSpec[] sourceSpecs,
            CharacterTestRuntimeAiPlannerProfileLoadResult defaultProfile,
            CharacterTestRuntimeAiPlannerProfileBinding[] sourceBindings,
            out CharacterTestCharacterSpawnSpec[] assignedSpecs,
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> behaviorLoadoutBindings = null)
        {
            assignedSpecs = sourceSpecs ?? Array.Empty<CharacterTestCharacterSpawnSpec>();
            if (assignedSpecs.Length == 0)
                return sourceBindings ?? Array.Empty<CharacterTestRuntimeAiPlannerProfileBinding>();

            var characterIds = new List<int>();
            var originalProfileSlots = new List<int>();
            for (int i = 0; i < assignedSpecs.Length; i++)
            {
                CharacterTestCharacterSpawnSpec spec = assignedSpecs[i];
                if (spec.ControlOwner != CharacterTestCharacterControlOwner.AiControlled
                    || spec.TeamId != CharacterTestArenaMatchConfig.Team1Id
                    || spec.CharacterId <= 0)
                {
                    continue;
                }

                int originalProfileSlot = Math.Max(0, spec.RuntimeAiPlannerProfileSlot);
                bool reservationExists = false;
                for (int reservationIndex = 0;
                     reservationIndex < characterIds.Count;
                     reservationIndex++)
                {
                    if (characterIds[reservationIndex] == spec.CharacterId
                        && originalProfileSlots[reservationIndex] == originalProfileSlot)
                    {
                        reservationExists = true;
                        break;
                    }
                }
                if (reservationExists)
                    continue;

                characterIds.Add(spec.CharacterId);
                originalProfileSlots.Add(originalProfileSlot);
            }
            if (characterIds.Count == 0)
                return sourceBindings ?? Array.Empty<CharacterTestRuntimeAiPlannerProfileBinding>();

            for (int i = 1; i < characterIds.Count; i++)
            {
                int characterId = characterIds[i];
                int profileSlot = originalProfileSlots[i];
                int insertionIndex = i;
                while (insertionIndex > 0
                       && (characterIds[insertionIndex - 1] > characterId
                           || (characterIds[insertionIndex - 1] == characterId
                               && originalProfileSlots[insertionIndex - 1] > profileSlot)))
                {
                    characterIds[insertionIndex] = characterIds[insertionIndex - 1];
                    originalProfileSlots[insertionIndex] =
                        originalProfileSlots[insertionIndex - 1];
                    insertionIndex--;
                }
                characterIds[insertionIndex] = characterId;
                originalProfileSlots[insertionIndex] = profileSlot;
            }

            var bindings = new List<CharacterTestRuntimeAiPlannerProfileBinding>(
                (sourceBindings?.Length ?? 0) + characterIds.Count);
            int maximumSlot = 0;
            for (int i = 0; sourceBindings != null && i < sourceBindings.Length; i++)
            {
                CharacterTestRuntimeAiPlannerProfileBinding binding = sourceBindings[i];
                bindings.Add(binding);
                if (binding.Slot > maximumSlot)
                    maximumSlot = binding.Slot;
            }
            for (int i = 0; i < assignedSpecs.Length; i++)
            {
                int occupiedSlot = Math.Max(0, assignedSpecs[i].RuntimeAiPlannerProfileSlot);
                if (occupiedSlot > maximumSlot)
                    maximumSlot = occupiedSlot;
            }

            CharacterTestCharacterSpawnSpec[] remapped = CopySpawnSpecs(assignedSpecs);
            for (int characterIndex = 0; characterIndex < characterIds.Count; characterIndex++)
            {
                int characterId = characterIds[characterIndex];
                int originalSlot = originalProfileSlots[characterIndex];

                CharacterTestRuntimeAiPlannerProfile profile = defaultProfile.Profile;
                CharacterTestRuntimeAiPlannerValidationReport report = defaultProfile.Report;
                string sourcePath = defaultProfile.SourcePath;
                for (int i = 0; sourceBindings != null && i < sourceBindings.Length; i++)
                {
                    if (sourceBindings[i].Slot != originalSlot)
                        continue;
                    profile = sourceBindings[i].Profile;
                    report = sourceBindings[i].SourceReport;
                    sourcePath = sourceBindings[i].SourcePath;
                    break;
                }
                for (int i = 0;
                     behaviorLoadoutBindings != null && i < behaviorLoadoutBindings.Count;
                     i++)
                {
                    if (behaviorLoadoutBindings[i].Slot != originalSlot)
                        continue;
                    profile = behaviorLoadoutBindings[i].Profile;
                    report = behaviorLoadoutBindings[i].SourceReport;
                    sourcePath = behaviorLoadoutBindings[i].SourcePath;
                    break;
                }

                int reservedSlot = ++maximumSlot;
                bindings.Add(new CharacterTestRuntimeAiPlannerProfileBinding(
                    reservedSlot,
                    sourcePath,
                    profile,
                    report));
                for (int i = 0; i < remapped.Length; i++)
                {
                    CharacterTestCharacterSpawnSpec spec = remapped[i];
                    if (spec.CharacterId == characterId
                        && spec.ControlOwner == CharacterTestCharacterControlOwner.AiControlled
                        && spec.TeamId == CharacterTestArenaMatchConfig.Team1Id
                        && Math.Max(0, spec.RuntimeAiPlannerProfileSlot) == originalSlot)
                    {
                        remapped[i] = spec.WithRuntimeAiPlannerProfileSlot(reservedSlot);
                    }
                }
            }
            assignedSpecs = remapped;
            return bindings.ToArray();
        }

        private static void RegisterRuntimeAiBehaviorSelectionAliases(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtime,
            IReadOnlyList<CharacterTestCharacterSpawnSpec> startupSpecs)
        {
            if (runtime == null || startupSpecs == null)
                return;

            for (int i = 0; i < startupSpecs.Count; i++)
            {
                CharacterTestRuntimeAiBehaviorSelection selection =
                    startupSpecs[i].RuntimeAiBehaviorSelection;
                if (!selection.HasSelection)
                    continue;

                if (!runtime.TryRegisterSessionProfileSlotAlias(selection, out string message))
                {
                    throw new InvalidOperationException(
                        "Runtime AI Planner session profile slot alias registration failed: "
                        + message);
                }
            }
        }

        internal bool TryResolveArenaRuntimeAiPlannerProfileSlot(
            int teamId,
            int slotIndex,
            out int profileSlot)
        {
            int index = ResolveArenaProfileSlotIndex(teamId, slotIndex);
            if (index < 0 || !_arenaRuntimeAiPlannerProfileSlotAssigned[index])
            {
                profileSlot = 0;
                return false;
            }

            profileSlot = _arenaRuntimeAiPlannerProfileSlots[index];
            return true;
        }

        private void CaptureArenaRuntimeAiPlannerProfileSlots(
            CharacterTestArenaMatchConfig arenaMatchConfig,
            IReadOnlyList<CharacterTestCharacterSpawnSpec> startupSpecs)
        {
            Array.Clear(
                _arenaRuntimeAiPlannerProfileSlots,
                0,
                _arenaRuntimeAiPlannerProfileSlots.Length);
            Array.Clear(
                _arenaRuntimeAiPlannerProfileSlotAssigned,
                0,
                _arenaRuntimeAiPlannerProfileSlotAssigned.Length);
            if (arenaMatchConfig == null || startupSpecs == null)
                return;

            int spawnIndex = 0;
            if (!TryCaptureArenaTeamRuntimeAiPlannerProfileSlots(
                    arenaMatchConfig.Team1Slots,
                    CharacterTestArenaMatchConfig.Team1Id,
                    startupSpecs,
                    ref spawnIndex)
                || !TryCaptureArenaTeamRuntimeAiPlannerProfileSlots(
                    arenaMatchConfig.Team2Slots,
                    CharacterTestArenaMatchConfig.Team2Id,
                    startupSpecs,
                    ref spawnIndex)
                || spawnIndex != startupSpecs.Count)
            {
                Array.Clear(
                    _arenaRuntimeAiPlannerProfileSlotAssigned,
                    0,
                    _arenaRuntimeAiPlannerProfileSlotAssigned.Length);
            }
        }

        private bool TryCaptureArenaTeamRuntimeAiPlannerProfileSlots(
            IReadOnlyList<CharacterTestArenaSlotConfig> slots,
            int teamId,
            IReadOnlyList<CharacterTestCharacterSpawnSpec> startupSpecs,
            ref int spawnIndex)
        {
            for (int slotIndex = 0; slots != null && slotIndex < slots.Count; slotIndex++)
            {
                CharacterTestArenaSlotConfig slot = slots[slotIndex];
                if (slot == null || !slot.Enabled)
                    continue;
                if (spawnIndex >= startupSpecs.Count)
                    return false;

                CharacterTestCharacterSpawnSpec spec = startupSpecs[spawnIndex++];
                if (spec.TeamId != teamId || spec.CharacterId != slot.CharacterId)
                    return false;

                int index = ResolveArenaProfileSlotIndex(teamId, slotIndex);
                if (index < 0)
                    return false;
                _arenaRuntimeAiPlannerProfileSlots[index] =
                    Math.Max(0, spec.RuntimeAiPlannerProfileSlot);
                _arenaRuntimeAiPlannerProfileSlotAssigned[index] = true;
            }

            return true;
        }

        private static int ResolveArenaProfileSlotIndex(int teamId, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= CharacterTestArenaMatchConfig.SlotsPerTeam)
                return -1;
            if (teamId == CharacterTestArenaMatchConfig.Team1Id)
                return slotIndex;
            return teamId == CharacterTestArenaMatchConfig.Team2Id
                ? CharacterTestArenaMatchConfig.SlotsPerTeam + slotIndex
                : -1;
        }

        private static CharacterTestCharacterSpawnSpec[] CopySpawnSpecs(
            CharacterTestCharacterSpawnSpec[] specs)
        {
            if (specs == null || specs.Length == 0)
                return Array.Empty<CharacterTestCharacterSpawnSpec>();

            var copy = new CharacterTestCharacterSpawnSpec[specs.Length];
            Array.Copy(specs, copy, specs.Length);
            return copy;
        }

        private CharacterTestMovableNaturalObjectSceneSource ResolveMovableNaturalObjectSceneSource()
        {
            CharacterTestMovableNaturalObjectSceneSource[] sources =
                UnityEngine.Object.FindObjectsByType<CharacterTestMovableNaturalObjectSceneSource>(UnityEngine.FindObjectsInactive.Exclude);
            if (sources == null || sources.Length == 0)
                return null;

            if (sources.Length > 1)
            {
                _context.Logger?.Warning(
                    "GameManager",
                    "CharacterTest found multiple movable natural object scene sources; using none to avoid ambiguous object ids.");
                return null;
            }

            return sources[0];
        }

        private void ConfigureMovableNaturalObjectViewBridge()
        {
            _context.MovableNaturalObjectViewBridge = CreateLevelWorldViewBridge(
                _context.ActiveLevelWorldCache, _gameManager?.WorldObjectSceneCache,
                ResolveMovableNaturalObjectSceneSource);

            _context.Slice.Services.Presentation.SetMovableNaturalObjectViewBridge(
                _context.MovableNaturalObjectViewBridge);
            _context.MovableNaturalObjectViewBridge.Sync(_context.Slice.Services.MovableNaturalObjects);
        }

        private IAudioService TryCreateDefaultAudioService(ICharacterTestAudioCueCatalog catalog)
        {
            ICharacterTestAudioCueCatalog safeCatalog = catalog ?? CharacterTestAudioCueCatalog.CreateDefault();
            return CharacterTestAudioStartupFactory.TryCreate(() =>
            {
                AudioBankManifest bankManifest = LoadAudioBankManifest();
                var backend = new FmodAudioBackend(new FmodAudioBackendOptions
                    {
                        BankManifest = bankManifest,
                        FailOnMissingBus = false,
                        CaptureRecentCommands = true,
                        PreloadBusIds = new[] { CharacterTestAudioCueCatalog.DefaultBusId }
                    });
                try
                {
                    return new AudioService(safeCatalog, backend);
                }
                catch (Exception original)
                {
                    var errors = new List<Exception> { original };
                    CharacterTestCleanupScope.Attempt(errors, "audio backend", backend.Dispose);
                    if (errors.Count > 1) throw new AggregateException("CharacterTest audio construction rollback failed.", errors);
                    throw;
                }
            }, _context.Logger);
        }

        private AudioBankManifest LoadAudioBankManifest()
        {
            AudioStartupArtifactBundle artifacts = _gameManager?.AudioStartupArtifacts;
            if (!Application.isEditor && artifacts != null)
            {
                if (!AudioBankManifestJson.TryLoad(
                        artifacts.BankManifestJson,
                        out AudioBankManifest loaded,
                        out string artifactError))
                {
                    throw new InvalidOperationException(
                        "Audio Bank manifest failed to parse from platform loader: " + artifactError);
                }
                return loaded;
            }

            string relativePath = Path.Combine("MxFramework", "Audio", "audio_bank_manifest.json");
            string path = Application.isEditor
                ? Path.Combine(Application.dataPath, "MxFrameworkGenerated", "Audio", "audio_bank_manifest.json")
                : Path.Combine(Application.streamingAssetsPath, relativePath);
            if (!AudioBankManifestJson.TryLoadFile(path, out AudioBankManifest manifest, out string error))
                throw new InvalidOperationException("Audio Bank manifest failed to load: " + error);
            return manifest;
        }

        /// <remarks>LowFreqAlloc. Reads and parses exactly one catalog during session startup.</remarks>
        private ICharacterTestAudioCueCatalog LoadAudioCueCatalog()
        {
            using var startupProfile = CharacterTestStartupProfiler.LoadAudio.Auto();
            AudioStartupArtifactBundle artifacts = _gameManager?.AudioStartupArtifacts;
            if (!Application.isEditor && artifacts != null)
            {
                return ParseAudioCatalogPair(
                    artifacts.RuntimeCatalogJson,
                    artifacts.PhysicalCatalogJson,
                    allowLegacyFallback: false);
            }

            string relativePath = Path.Combine("MxFramework", "Audio", "audio_runtime_catalog.json");
            string catalogPath = Application.isEditor
                ? Path.Combine(Application.dataPath, "MxFrameworkGenerated", "Audio", "audio_runtime_catalog.json")
                : Path.Combine(Application.streamingAssetsPath, relativePath);
            string physicalRelativePath = Path.Combine(
                "MxFramework",
                "Audio",
                "audio_physical_runtime_catalog.json");
            string physicalCatalogPath = Application.isEditor
                ? Path.Combine(
                    Application.dataPath,
                    "MxFrameworkGenerated",
                    "Audio",
                    "audio_physical_runtime_catalog.json")
                : Path.Combine(Application.streamingAssetsPath, physicalRelativePath);
            if (File.Exists(catalogPath) && File.Exists(physicalCatalogPath))
            {
                string json = File.ReadAllText(catalogPath);
                string physicalJson = File.ReadAllText(physicalCatalogPath);
                return ParseAudioCatalogPair(json, physicalJson, allowLegacyFallback: Application.isEditor);
            }
            else
            {
                if (!Application.isEditor)
                {
                    throw new InvalidOperationException(
                        "Required Player audio catalog pair is incomplete. audio="
                        + catalogPath
                        + ", physical="
                        + physicalCatalogPath
                        + ".");
                }
                _context.Logger?.Warning(
                    "CharacterTest.Audio",
                    "Generated audio catalog pair is incomplete; using the complete legacy fallback without merging. "
                    + "audio="
                    + catalogPath
                    + ", physical="
                    + physicalCatalogPath
                    + ".");
            }

            return CharacterTestAudioCueCatalog.CreateLegacyFallback();
        }

        private ICharacterTestAudioCueCatalog ParseAudioCatalogPair(
            string json,
            string physicalJson,
            bool allowLegacyFallback)
        {
            bool audioLoaded = AudioRuntimeCatalogJson.TryLoad(
                json,
                out AudioDefinitionCatalog catalog,
                out string audioError);
            bool physicalLoaded = PhysicalAudioRuntimeCatalogJson.TryLoad(
                physicalJson,
                out PhysicalAudioDefinitionCatalog physicalCatalog,
                out string physicalError);
            string failure = string.Empty;
            if (audioLoaded && physicalLoaded)
            {
                string audioCatalogHash = ComputeTextSha256(json);
                if (string.Equals(
                        physicalCatalog.AudioCatalogHash,
                        audioCatalogHash,
                        StringComparison.Ordinal))
                {
                    if (CharacterTestAudioCueCatalog.TryFromRuntimeCatalog(
                            catalog,
                            physicalCatalog,
                            out CharacterTestAudioCueCatalog linkedCatalog,
                            out string linkError))
                    {
                        _context.Logger?.Info(
                            "CharacterTest.Audio",
                            "CharacterTest loaded generated audio and physical catalogs "
                            + catalog.ManifestHash
                            + " / "
                            + physicalCatalog.PhysicalManifestHash
                            + ".");
                        return linkedCatalog;
                    }
                    failure = "physical playback linking failed: " + linkError;
                }
                else
                {
                    failure = "physical catalog hash mismatch. expected="
                        + physicalCatalog.AudioCatalogHash
                        + ", actual="
                        + audioCatalogHash;
                }
            }
            else
            {
                failure = "catalog parse failed: audio=" + audioError + ", physical=" + physicalError;
            }

            if (!allowLegacyFallback)
                throw new InvalidOperationException("Required Player audio catalog pair is invalid: " + failure);

            _context.Logger?.Warning(
                "CharacterTest.Audio",
                "Generated audio catalog pair is invalid; using Editor legacy fallback: " + failure);
            return CharacterTestAudioCueCatalog.CreateLegacyFallback();
        }

        /// <remarks>LowFreqAlloc. Used only while validating generated catalog identity at startup.</remarks>
        private static string ComputeTextSha256(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2 + 7);
                builder.Append("sha256:");
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private void ReleaseThirdPersonCamera()
        {
            CharacterTestThirdPersonCameraRigBridge camera = _context.ThirdPersonCameraBridge;
            _context.ThirdPersonCameraBridge = null;
            var errors = new List<Exception>();
            CharacterTestCleanupScope.Attempt(errors, "MovementBasis", () =>
                _context.Slice?.Services.Presentation.SetCharacterMovementBasisProvider(null));
            if (camera != null)
                CharacterTestCleanupScope.Attempt(errors, "ThirdPersonCamera", camera.Clear);
            CharacterTestCleanupScope.ThrowIfFailed(errors, "CharacterTest camera cleanup failed.");
        }

        private void ReleaseMovableNaturalObjectViewBridge()
        {
            _context.Slice?.Services.Presentation.SetMovableNaturalObjectViewBridge(null);
            _context.MovableNaturalObjectViewBridge = null;
        }

        private void ConfigureUiRuntime(CharacterTestStoryContent storyContent, bool tacticalSession = false)
        {
            using var startupProfile = CharacterTestStartupProfiler.ConfigureUi.Auto();
            if (_context.Slice == null)
                return;

            bool runtimeAiLab = _context.RuntimeAiPlannerDevSceneController != null;
            bool arena = _context.ArenaSessionController != null;
            _context.Slice.CharacterRuntime?.SetForceHeadPadsVisible(runtimeAiLab);
            var options = new CharacterTestUiRuntimeOptions
            {
                Slice = _context.Slice,
                StoryContent = storyContent,
                IsPausedProvider = () => _context.Paused,
                EnableDebugUi = CharacterTestUiDefaults.EnableDebugUi && !runtimeAiLab,
                EnableSkillPanel = CharacterTestUiDefaults.EnableSkillPanel && !runtimeAiLab,
                DebugInitialVisibility = CharacterTestUiDefaults.DebugInitialVisibility,
                DebugShowOnStart = CharacterTestUiDefaults.DebugShowOnStart && !runtimeAiLab,
                DebugInitialTabIndex = CharacterTestUiDefaults.DebugInitialTabIndex,
                EnableFairyGuiCombatHud = CharacterTestUiDefaults.EnableFairyGuiCombatHud && !runtimeAiLab && !tacticalSession,
                EnableFairyGuiFloatingCombatText =
                    CharacterTestUiDefaults.EnableFairyGuiFloatingCombatText && !tacticalSession,
                EnableFairyGuiGuidance = CharacterTestUiDefaults.EnableFairyGuiGuidance && !runtimeAiLab && !tacticalSession,
                EnableFairyGuiStoryUi = CharacterTestUiDefaults.EnableFairyGuiStoryUi
                    && !arena
                    && !runtimeAiLab
                    && !tacticalSession,
                EnableFairyGuiMainMenu = CharacterTestUiDefaults.EnableFairyGuiMainMenu && !runtimeAiLab,
                AutoOpenMainMenu = _context.MainMenuVisible && !runtimeAiLab,
                EnableFairyGuiNetworkPanel = CharacterTestUiDefaults.EnableFairyGuiNetworkPanel && !runtimeAiLab,
                EnableFairyGuiSettingsPanel = ShouldEnableFairyGuiSettingsPanel() && !runtimeAiLab,
                EnableFairyGuiAbilityBindingPanel =
                    CharacterTestUiDefaults.EnableFairyGuiAbilityBindingPanel && !runtimeAiLab,
                EnableFairyGuiRuntimeAiBehaviorLoadoutPanel =
                    ShouldEnableRuntimeAiBehaviorLoadoutPanel(
                        runtimeAiLab,
                        _context.PlayerBehaviorDeckService != null),
                PlayerBehaviorDeckWorkbench = _context.PlayerBehaviorDeckService?.Workbench,
                PlayerBehaviorDeckService = _context.PlayerBehaviorDeckService,
                EnableFairyGuiArenaSetupPanel = arena,
                AutoOpenFairyGuiArenaSetupPanel = arena
                    && _context.ArenaSessionController.ShouldAutoOpenSetupPanel,
                EnableFairyGuiRuntimeAiPlannerDevPanel = runtimeAiLab,
                AutoOpenFairyGuiRuntimeAiPlannerDevPanel = runtimeAiLab
                    && _context.RuntimeAiPlannerDevSceneController.ShouldAutoOpenPanel,
                AutoOpenNetworkPanel = CharacterTestUiDefaults.EnableFairyGuiNetworkPanel
                    && CharacterTestUiDefaults.AutoOpenFairyGuiNetworkPanel
                    && (_gameManager == null || _gameManager.GetComponent<TacticalCardHud>() == null)
                    && !arena
                    && !runtimeAiLab
                    && !_context.MainMenuVisible,
                MainMenuController = _context.MainMenuCoordinator,
                NetworkSessionController = _context.NetworkCoordinator,
                NetworkLobbyController = _context.NetworkCoordinator,
                NetworkSessionStatusProvider = _context.NetworkCoordinator.CaptureCombatHudNetworkSessionStatus,
                SettingsRuntime = _context.SettingsRuntime,
                LocalizationRuntime = _context.LocalizationRuntime,
                TextProvider = _context.LocalizationRuntime?.TextProvider,
                InputProvider = ResolvePlayerUiInputProvider(),
                CursorController = EnsureCursorController(),
                CameraRigBridge = _context.ThirdPersonCameraBridge,
                ArenaSessionController = _context.ArenaSessionController,
                RuntimeAiPlannerDevSceneController = _context.RuntimeAiPlannerDevSceneController
            };

            if (_context.UiRuntime != null && _context.UiRuntime.TryRebind(options))
            {
                return;
            }

            ReleaseUiRuntime();
            _context.UiRuntime = new CharacterTestUiRuntime(_owner, _context.Logger);
            _context.UiRuntime.Configure(options);
        }

        private void ReleaseUiRuntime()
        {
            CharacterTestUiRuntime ui = _context.UiRuntime;
            _context.UiRuntime = null;
            ui?.Release();
        }

        private IMxCursorController EnsureCursorController()
        {
            if (_context.CursorController == null)
                _context.CursorController = new MxCursorController();

            return _context.CursorController;
        }

        private void ReleaseCursorController()
        {
            IDisposable cursor = _context.CursorController as IDisposable;
            _context.CursorController = null;
            cursor?.Dispose();
        }

        private void DisposeSlice()
        {
            using var startupProfile = CharacterTestStartupProfiler.DisposeSlice.Auto();
            GameSlice slice = _context.Slice;
            ICharacterTestNetworkRunner network = _context.NetworkRunner;
            AudioMixSnapshotController mixSnapshots = _context.AudioMixSnapshotController;
            AudioMusicStateController music = _context.AudioMusicStateController;
            AudioPersistentEventController ambience = _context.AudioAmbienceController;
            _context.Slice = null;
            _context.NetworkRunner = null;
            _context.AudioMixController = null;
            _context.AudioMixSnapshotController = null;
            _context.AudioMusicStateController = null;
            _context.AudioAmbienceController = null;
            _context.PlayerBehaviorDeckService = null;
            _context.MovableNaturalObjectViewBridge = null;
            var errors = new List<Exception>();
            if (!_preparingTacticalCandidate) CharacterTestCleanupScope.Attempt(errors, "TacticalControl", () => _gameManager?.BindTacticalControl(null, null));
            CharacterTestCleanupScope.Attempt(errors, "SettingsAudio", () => _context.SettingsRuntime?.AttachAudioService(null));
            if (mixSnapshots != null) CharacterTestCleanupScope.Attempt(errors, "AudioMixSnapshots", mixSnapshots.Dispose);
            if (music != null) CharacterTestCleanupScope.Attempt(errors, "AudioMusic", music.Dispose);
            if (ambience != null) CharacterTestCleanupScope.Attempt(errors, "AudioAmbience", ambience.Dispose);
            CharacterTestCleanupScope.Attempt(errors, "NetworkSyncProbe", _context.NetworkSyncProbe.Reset);
            if (!_preparingTacticalCandidate) CharacterTestCleanupScope.Attempt(errors, "NetworkCoordinator", () => _context.NetworkCoordinator?.ResetNetworkSyncProbeAutoRunSessionState());
            CharacterTestCleanupScope.Attempt(errors, "FixedTickDriver", _context.FixedTickDriver.Reset);
            if (slice != null)
                CharacterTestCleanupScope.Attempt(errors, "MovableObjectViewBridge", () => slice.Services.Presentation.SetMovableNaturalObjectViewBridge(null));
            if (network != null)
                CharacterTestCleanupScope.Attempt(errors, "NetworkRunner", network.Dispose);
            else if (slice != null)
                CharacterTestCleanupScope.Attempt(errors, "Slice", slice.Dispose);
            CharacterTestCleanupScope.ThrowIfFailed(errors, "CharacterTest slice retirement failed.");
        }

        private static AudioMixController CreateAudioMixController(IAudioService audioService)
        {
            if (audioService == null)
                return null;

            return new AudioMixController(audioService, new[]
            {
                new AudioMixSetting(AudioMixBusIds.Master, 1f),
                new AudioMixSetting(AudioMixBusIds.Music, 0.8f),
                new AudioMixSetting(AudioMixBusIds.Sfx, 1f),
                new AudioMixSetting(AudioMixBusIds.Ui, 1f),
                new AudioMixSetting(AudioMixBusIds.Ambience, 0.8f),
                new AudioMixSetting(AudioMixBusIds.Voice, 1f)
            });
        }

        private static AudioMusicStateController CreateAudioMusicStateController(IAudioService audioService)
        {
            if (audioService == null)
                return null;

            return new AudioMusicStateController(audioService, new[]
            {
                new AudioMusicStateBinding(AudioMusicState.Menu, AudioProductEventIds.MenuMusic),
                new AudioMusicStateBinding(AudioMusicState.Exploration, AudioProductEventIds.ExplorationMusic),
                new AudioMusicStateBinding(AudioMusicState.Combat, AudioProductEventIds.CombatMusic)
            });
        }

        private void ConfigurePlayerBehaviorDeckService(
            IReadOnlyList<CharacterTestCharacterSpawnSpec> startupSpecs,
            CharacterTestArenaMatchConfig arenaMatchConfig,
            bool allowLockedLabPresentation,
            CharacterTestCharacterActionCatalog actionCatalog = null)
        {
            using var startupProfile = CharacterTestStartupProfiler.PlayerDeck.Auto();
            _context.PlayerBehaviorDeckService = null;
            if (_context.Slice == null
                || !ShouldCreatePlayerBehaviorDeckService(
                    _context.NetworkRunner != null,
                    allowLockedLabPresentation))
            {
                return;
            }

            CharacterTestRuntimeAiPlannerPlayerDeckTarget[] targets = ResolvePlayerBehaviorDeckTargets(
                    startupSpecs,
                    arenaMatchConfig,
                    _context.Slice.Services.CharacterEntities);
            if (_gameManager != null && _gameManager.GetComponent<TacticalCardHud>() != null
                && _context.Slice.Services.Modules.TacticalStage == null)
            {
                var characters = CharacterTestPlayableCharacterCatalog.Discover();
                targets = new CharacterTestRuntimeAiPlannerPlayerDeckTarget[characters.Count];
                for (int i = 0; i < characters.Count; i++)
                    targets[i] = new CharacterTestRuntimeAiPlannerPlayerDeckTarget(characters[i].CharacterId, i,
                        displayName: characters[i].DisplayName);
            }
            if (targets.Length == 0)
            {
                _context.RuntimeAiPlannerDevSceneController?.ReportPlayerDeckLayerUnavailable(
                    "未找到可装配的队伍 1 角色。");
                if (ShouldWarnWhenPlayerBehaviorDeckTargetsAreMissing(allowLockedLabPresentation))
                {
                    _context.Logger?.Warning(
                        "RuntimeAiPlanner",
                        "Player behavior deck workbench was not configured because no AI-controlled Team 1 character is available.");
                }
                else
                {
                    _context.Logger?.Info(
                        "RuntimeAiPlanner",
                        "Player behavior deck workbench is not required because this scene has no AI-controlled Team 1 character.");
                }
                return;
            }
            string filePath = TacticalCard.CharacterTacticalServices.PersistentPath(
                "CharacterTest", "runtime_ai_planner_behavior_decks.json");
            if (!CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    filePath,
                    targets,
                    out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                    out string error,
                    actionCatalog: actionCatalog))
            {
                _context.RuntimeAiPlannerDevSceneController?.ReportPlayerDeckLayerUnavailable(error);
                _context.Logger?.Warning(
                    "RuntimeAiPlanner",
                    "Player behavior deck workbench was not configured: " + error);
                return;
            }

            _context.PlayerBehaviorDeckService = service;
            if (allowLockedLabPresentation)
                service.Workbench.LockSession();
            if (service.LoadWarning.Length > 0)
            {
                _context.Logger?.Warning(
                    "RuntimeAiPlanner",
                    "Player behavior deck save was ignored and the safe default was retained: "
                    + service.LoadWarning);
            }
        }

        internal static bool ShouldCreatePlayerBehaviorDeckService(
            bool hasNetworkRunner,
            bool allowLockedLabPresentation)
        {
            return !hasNetworkRunner || allowLockedLabPresentation;
        }

        internal static bool ShouldWarnWhenPlayerBehaviorDeckTargetsAreMissing(
            bool allowLockedLabPresentation)
        {
            return allowLockedLabPresentation;
        }

        internal static CharacterTestRuntimeAiPlannerPlayerDeckTarget[] ResolvePlayerBehaviorDeckTargets(
            IReadOnlyList<CharacterTestCharacterSpawnSpec> startupSpecs,
            CharacterTestArenaMatchConfig arenaMatchConfig,
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            var targets = new List<CharacterTestRuntimeAiPlannerPlayerDeckTarget>();
            if (startupSpecs != null && startupSpecs.Count > 0)
            {
                for (int i = 0; i < startupSpecs.Count; i++)
                {
                    CharacterTestCharacterSpawnSpec spec = startupSpecs[i];
                    if (spec.ControlOwner == CharacterTestCharacterControlOwner.AiControlled
                        && spec.TeamId == CharacterTestArenaMatchConfig.Team1Id)
                    {
                        AddPlayerBehaviorDeckTarget(
                            targets,
                            spec.CharacterId,
                            spec.RuntimeAiPlannerProfileSlot);
                    }
                }
            }
            else if (arenaMatchConfig != null)
            {
                for (int i = 0; i < arenaMatchConfig.Team1Slots.Count; i++)
                {
                    CharacterTestArenaSlotConfig slot = arenaMatchConfig.Team1Slots[i];
                    if (slot != null && slot.Enabled)
                        AddPlayerBehaviorDeckTarget(targets, slot.CharacterId, 0);
                }
            }
            else
            {
                for (int i = 0; entities != null && i < entities.Count; i++)
                {
                    CharacterTestCharacterEntity entity = entities[i];
                    if (entity != null
                        && entity.Control.Owner == CharacterTestCharacterControlOwner.AiControlled
                        && entity.Team.TeamId == CharacterTestArenaMatchConfig.Team1Id)
                    {
                        AddPlayerBehaviorDeckTarget(
                            targets,
                            entity.Identity.CharacterId,
                            entity.Control.RuntimeAiPlannerProfileSlot);
                    }
                }
            }
            targets.Sort((left, right) => left.CharacterId.CompareTo(right.CharacterId));
            return targets.ToArray();
        }

        internal static bool ShouldEnableRuntimeAiBehaviorLoadoutPanel(
            bool runtimeAiLab,
            bool hasPlayerBehaviorDeckService)
        {
            return !runtimeAiLab || hasPlayerBehaviorDeckService;
        }

        private static void AddPlayerBehaviorDeckTarget(
            List<CharacterTestRuntimeAiPlannerPlayerDeckTarget> targets,
            int characterId,
            int profileSlot)
        {
            if (characterId <= 0)
                return;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].CharacterId == characterId)
                    return;
            }
            targets.Add(new CharacterTestRuntimeAiPlannerPlayerDeckTarget(characterId, profileSlot));
        }

        private CharacterTestNetworkSessionFactoryOptions CreateNetworkSessionFactoryOptions(
            CharacterTestNetworkMode networkMode,
            CharacterTestNetworkSessionOptions sessionOptions = null)
        {
            bool hasExplicitSessionOptions = sessionOptions != null;
            sessionOptions = hasExplicitSessionOptions
                ? sessionOptions.Clone()
                : CreateDefaultSessionOptionsForNetworkMode(networkMode);
            if (!hasExplicitSessionOptions)
                ApplyProfileNetworkTuning(sessionOptions);

            CharacterTestNetworkEndpoint.ApplyEndpointOverrides(sessionOptions);
            return new CharacterTestNetworkSessionFactoryOptions
            {
                NetworkMode = networkMode,
                TickRate = _profile.TargetFrameRate,
                InputDelayFrames = Mathf.Max(0, sessionOptions.InputDelayFrames),
                SnapshotIntervalFrames = Mathf.Max(1, sessionOptions.SnapshotIntervalFrames),
                StateSyncPolicy = sessionOptions.StateSyncPolicy,
                HeartbeatIntervalFrames = Mathf.Max(1, sessionOptions.HeartbeatIntervalFrames),
                LoopbackLatencyFrames = _profile.NetworkLoopbackLatencyFrames,
                LoopbackJitterFrames = _profile.NetworkLoopbackJitterFrames,
                LoopbackDropRate = _profile.NetworkLoopbackDropRate,
                LoopbackReorderMessages = _profile.NetworkLoopbackReorderMessages,
                UnityTransportBindAddress =
                    CharacterTestNetworkCoordinator.NormalizeBindAddress(sessionOptions.BindAddress),
                UnityTransportRemoteAddress =
                    CharacterTestNetworkCoordinator.NormalizeRemoteAddress(sessionOptions.RemoteAddress),
                UnityTransportPort = CharacterTestNetworkCoordinator.NormalizePort(
                    sessionOptions.Port,
                    CharacterTestNetworkSessionOptions.DefaultPort),
                UnityTransportMaxConnections = _profile.UnityTransportMaxConnections,
                UnityTransportMaxPayloadBytes = _profile.UnityTransportMaxPayloadBytes,
                UnityTransportReliableWindowSize = _profile.UnityTransportReliableWindowSize,
                UnityTransportReceiveQueueCapacity = _profile.UnityTransportReceiveQueueCapacity,
                UnityTransportSendQueueCapacity = _profile.UnityTransportSendQueueCapacity,
                UnityTransportServerPeerId =
                    CharacterTestNetworkCoordinator.NormalizeServerPeerId(sessionOptions.ServerPeerId),
                UnityTransportClientPeerId =
                    CharacterTestNetworkCoordinator.NormalizeClientPeerId(sessionOptions.ClientPeerId),
                NetworkRoomId = CharacterTestNetworkCoordinator.NormalizeRoomId(sessionOptions.RoomId),
                NetworkRoomName = CharacterTestNetworkCoordinator.NormalizeRoomName(sessionOptions.RoomName),
                NetworkRoomMaxMembers =
                    CharacterTestNetworkCoordinator.NormalizeRoomMaxMembers(sessionOptions.RoomMaxMembers),
                NetworkRoomMinReadyMembers =
                    CharacterTestNetworkCoordinator.NormalizeRoomMinReadyMembers(sessionOptions.RoomMinReadyMembers),
                NetworkRoomClientDisplayName =
                    CharacterTestNetworkCoordinator.NormalizeRoomDisplayName(sessionOptions.DisplayName),
                NetworkRoomClientAutoReady = sessionOptions.RoomClientAutoReady,
                NetworkRoomServerAutoStartWhenReady = sessionOptions.RoomServerAutoStartWhenReady
            };
        }

        private static CharacterTestNetworkSessionOptions CreateDefaultSessionOptionsForNetworkMode(
            CharacterTestNetworkMode networkMode)
        {
            CharacterTestNetworkSessionOptions options;
            switch (CharacterTestNetworkSessionModeMapper.ToSessionKind(networkMode))
            {
                case CharacterTestNetworkSessionKind.Host:
                    options = CharacterTestNetworkSessionOptions.HostDefaults();
                    break;
                case CharacterTestNetworkSessionKind.Join:
                    options = CharacterTestNetworkSessionOptions.JoinDefaults();
                    break;
                default:
                    options = new CharacterTestNetworkSessionOptions();
                    break;
            }

            options.LaunchMode = CharacterTestNetworkSessionModeMapper.ToLaunchMode(networkMode);
            return options;
        }

        private void ApplyProfileNetworkTuning(CharacterTestNetworkSessionOptions options)
        {
            if (options == null)
                return;

            options.InputDelayFrames = Mathf.Max(0, _profile.NetworkInputDelayFrames);
            options.SnapshotIntervalFrames = Mathf.Max(1, _profile.NetworkSnapshotIntervalFrames);
            options.StateSyncPolicy = NetworkStateSyncPolicy.BootstrapOnly;
            options.HeartbeatIntervalFrames = Mathf.Max(1, _profile.NetworkHeartbeatIntervalFrames);
        }

        private void RestoreNetworkPanelAfterRuntimeRebuild(bool mainMenuVisible)
        {
            _context.MainMenuVisible = mainMenuVisible;
            _context.MainMenuCoordinator.RefreshEffectivePauseState();
            bool opened = _context.UiRuntime?.FairyGuiNetworkPanelHost != null
                && _context.UiRuntime.FairyGuiNetworkPanelHost.Open();
            if (!opened)
            {
                _context.Logger?.Warning(
                    "GameManager",
                    "CharacterTest network session changed, but the Network Panel could not be reopened.");
            }
        }

        private void LogResourceRuntimeBootstrap(GlobalResourceRuntimeServices resources)
        {
            if (resources == null)
                return;

            _context.LogBuffer.Clear()
                .Append("Global resource runtime initialized. mode=")
                .Append(resources.Mode.ToString())
                .Append(", configuredMode=")
                .Append(_profile.ResourceMode.ToString())
                .Append(", preferResourceSceneFlow=")
                .Append(_profile.PreferResourceManagedSceneFlow ? "true" : "false")
                .Append(", preloadGroup=")
                .Append(string.IsNullOrWhiteSpace(_profile.BaseResourcePreloadGroupId)
                    ? "<none>"
                    : _profile.BaseResourcePreloadGroupId)
                .Append(", catalogs=")
                .Append(resources.ResourceManager.CreateDebugSnapshot().CatalogCount)
                .Append(", sceneFlow=")
                .Append(resources.SceneFlowController != null ? "available" : "unavailable");
            _context.Logger?.Info("Resources", _context.LogBuffer);

            if (resources.HasBootstrapError)
            {
                _context.LogBuffer.Clear()
                    .Append("Global resource runtime bootstrap warning: ")
                    .Append(resources.BootstrapErrorMessage);
                _context.Logger?.Warning("Resources", _context.LogBuffer);
            }
        }

        private void LogTraceRecorderState()
        {
            CharacterTestTraceRecorder traceRecorder = _context.Slice?.Services?.TraceRecorder;
            if (traceRecorder == null)
                return;

            _context.LogBuffer.Clear()
                .Append("Trace recorder ")
                .Append(traceRecorder.WritesFile ? "writing to " : "file disabled")
                .Append(traceRecorder.WritesFile ? traceRecorder.FilePath : string.Empty);
            _context.Logger?.Info("GameManager", _context.LogBuffer);
        }

        private void LogSceneFlowRequest(string operation, SceneFlowResult result)
        {
            _context.LogBuffer.Clear()
                .Append("SceneFlow ")
                .Append(operation)
                .Append(" sceneKey=")
                .Append(string.IsNullOrWhiteSpace(result.SceneKey) ? "<empty>" : result.SceneKey)
                .Append(", status=")
                .Append(result.Status.ToString());
            if (!result.Error.IsNone)
            {
                _context.LogBuffer
                    .Append(", error=")
                    .Append(result.Error.Code.ToString())
                    .Append(": ")
                    .Append(result.Error.Message);
                _context.Logger?.Warning("GameManager", _context.LogBuffer);
                return;
            }

            _context.Logger?.Info("GameManager", _context.LogBuffer);
        }

        private static void ApplyEditorMainMenuInputBindings(IInputProvider inputProvider)
        {
#if UNITY_EDITOR
            var inputService = inputProvider as InputService;
            if (inputService == null)
                return;

            inputService.TryChangeBindingPath(
                InputActionNames.GameplayMap + "/" + InputActionNames.Pause,
                0,
                DefaultInputBindingCatalog.KeyboardPPath,
                DefaultInputBindingCatalog.KeyboardEscapePath);
            inputService.TryChangeBindingPath(
                InputActionNames.UIMap + "/" + InputActionNames.Cancel,
                0,
                DefaultInputBindingCatalog.KeyboardPPath,
                DefaultInputBindingCatalog.KeyboardEscapePath);
#endif
        }

        private static string ResolveSettingsFilePath()
        {
            return Path.Combine(
                Application.persistentDataPath,
                "MxFramework",
                "CharacterTest",
                "settings.json");
        }

        private static string ResolveAbilityHotbarFilePath()
        {
            return Path.Combine(
                Application.persistentDataPath,
                "MxFramework",
                "CharacterTest",
                "ability_hotbar.json");
        }

        private static string ResolveLocalizationFilePath()
        {
            return Path.Combine(
                Application.streamingAssetsPath,
                "MxFramework",
                "Localization",
                "character_test.tsv");
        }
    }

    /// <summary>Owns an optional audio service until initialization and publication diagnostics succeed.</summary>
    internal static class CharacterTestAudioStartupFactory
    {
        internal static IAudioService TryCreate(Func<IAudioService> create, IRuntimeLogger logger)
        {
            IAudioService candidate = null;
            try
            {
                candidate = create();
                AudioDebugSnapshot snapshot = candidate?.CaptureSnapshot();
                if (snapshot != null && snapshot.Initialized)
                {
                    logger?.Info("CharacterTest.Audio", "CharacterTest audio service initialized.");
                    return candidate;
                }
            }
            catch (Exception original)
            {
                var errors = new List<Exception> { original };
                if (candidate is IDisposable disposable)
                    CharacterTestCleanupScope.Attempt(errors, "audio startup", disposable.Dispose);
                if (errors.Count > 1 || original is AggregateException)
                    throw new AggregateException("CharacterTest audio startup rollback failed.", errors);
                logger?.Warning("CharacterTest.Audio", "CharacterTest audio disabled: " + original.Message);
                return null;
            }

            var cleanupErrors = new List<Exception>();
            if (candidate is IDisposable unavailable)
                CharacterTestCleanupScope.Attempt(cleanupErrors, "unavailable audio", unavailable.Dispose);
            CharacterTestCleanupScope.ThrowIfFailed(cleanupErrors, "CharacterTest unavailable audio cleanup failed.");
            logger?.Warning("CharacterTest.Audio", "CharacterTest audio disabled: FMOD backend is unavailable or not initialized.");
            return null;
        }
    }
}
