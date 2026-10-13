using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Input;
using MxFramework.Runtime;
using MxFramework.Settings;
using MxFramework.Settings.Graphics;
using MxFramework.Settings.Input;
using MxFramework.Settings.UI;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Code-only settings host for CharacterTest. It owns no scene assets and does not create audio services.
    /// </summary>
    public sealed class CharacterTestSettingsRuntime : IDisposable
    {
        private const string LogCategory = "CharacterTest.Settings";
        private const string AudioApplierId = "settings.audio.character_test";
        private static readonly MxAudioBusSetting[] AudioDefaults =
        {
            new MxAudioBusSetting(AudioMixBusIds.Master, 1f, false),
            new MxAudioBusSetting(AudioMixBusIds.Music, 0.8f, false),
            new MxAudioBusSetting(AudioMixBusIds.Sfx, 1f, false),
            new MxAudioBusSetting(AudioMixBusIds.Ui, 1f, false),
            new MxAudioBusSetting(AudioMixBusIds.Ambience, 0.8f, false),
            new MxAudioBusSetting(AudioMixBusIds.Voice, 1f, false)
        };
        private CharacterTestAudioSettingsApplier _audioSettingsApplier;
        private int _selectedAudioBusIndex;
        private IMxHdrOutputRuntime _hdrOutput;
        private IDisposable _ownedHdrOutput;
        private MxDirectionalShadowSettingsApplier _directionalShadowSettings;
        private CharacterTestWaterSettingsApplier _waterSettings;

        public CharacterTestSettingsRuntime(
            string settingsFilePath,
            MxSettingsService settingsService,
            MxInputRebindingSettingsController inputSettings,
            MxSettingsLoadResult lastLoadResult,
            IMxSettingsApplier graphicsSettingsApplier = null,
            MxSettingsUiPresenter settingsUi = null,
            IReadOnlyList<MxInputBindingTarget> inputBindingTargets = null,
            IReadOnlyList<MxSettingsUiOptionViewModel> localizationOptions = null)
        {
            SettingsFilePath = settingsFilePath ?? string.Empty;
            SettingsService = settingsService ?? new MxSettingsService(null);
            InputSettings = inputSettings ?? new MxInputRebindingSettingsController(null, null);
            InputBindingTargets = CopyInputBindingTargets(inputBindingTargets);
            LastLoadResult = lastLoadResult ??
                MxSettingsLoadResult.Succeeded(SettingsService.CurrentProfile);
            GraphicsSettingsApplier = graphicsSettingsApplier;
            LastApplyResult = MxSettingsApplyResult.Succeeded();
            LastSaveResult = MxSettingsSaveResult.Succeeded();

            RegisterApplierIfMissing(SettingsService, GraphicsSettingsApplier);
            SettingsUi = settingsUi ?? new MxSettingsUiPresenter(
                SettingsService,
                InputSettings,
                InputBindingTargets,
                localizationOptions);
            EnsureCharacterTestDefaultAudioBus(false);
        }

        public string SettingsFilePath { get; }
        public MxSettingsService SettingsService { get; }
        public MxInputRebindingSettingsController InputSettings { get; }
        public IReadOnlyList<MxInputBindingTarget> InputBindingTargets { get; }
        public MxSettingsUiPresenter SettingsUi { get; }
        public IMxSettingsApplier GraphicsSettingsApplier { get; }
        public IMxSettingsApplier AudioSettingsApplier => _audioSettingsApplier;
        public MxSettingsLoadResult LastLoadResult { get; private set; }
        public MxSettingsApplyResult LastApplyResult { get; private set; }
        public MxSettingsSaveResult LastSaveResult { get; private set; }
        public MxSettingsProfile CurrentProfile => SettingsUi.WorkingProfile;
        public int SelectedAudioBusId => AudioMixBusIds.PlayerAdjustable[_selectedAudioBusIndex];
        public MxHdrOutputStatus HdrOutputStatus => _hdrOutput?.Status ?? new MxHdrOutputStatus(MxHdrOutputState.Unavailable);

        public void AttachHdrOutput(IMxHdrOutputRuntime runtime, bool ownsRuntime = false)
        {
            if (_hdrOutput != null) throw new InvalidOperationException("HDR output is already attached.");
            _hdrOutput = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _ownedHdrOutput = ownsRuntime ? runtime as IDisposable : null;
            var applier = new MxHdrOutputSettingsApplier(runtime);
            RegisterApplierIfMissing(SettingsService, applier);
            // Restore only display preferences here; the existing graphics apply policy is unchanged.
            applier.Apply(SettingsService.CurrentProfile);
        }

        public void AttachDirectionalShadowSettings(Func<MxFramework.Rendering.MxSceneRenderingConfiguration> configuration)
        {
            if (_directionalShadowSettings != null) throw new InvalidOperationException("Shadow settings are already attached.");
            _directionalShadowSettings = new MxDirectionalShadowSettingsApplier(configuration);
            RegisterApplierIfMissing(SettingsService, _directionalShadowSettings);
            LastApplyResult = _directionalShadowSettings.Apply(SettingsService.CurrentProfile);
        }

        public void Dispose()
        {
            _waterSettings?.Dispose(); _waterSettings = null;
            _directionalShadowSettings?.Dispose(); _directionalShadowSettings = null;
            _ownedHdrOutput?.Dispose(); _ownedHdrOutput = null;
        }

        public void AttachWaterSettings(Func<CharacterTestShallowWaterConfiguration> configuration)
        {
            if (_waterSettings != null) throw new InvalidOperationException("Water settings are already attached.");
            _waterSettings = new CharacterTestWaterSettingsApplier(configuration);
            RegisterApplierIfMissing(SettingsService, _waterSettings);
            LastApplyResult = _waterSettings.Apply(SettingsService.CurrentProfile);
        }

        public void SelectPreviousAudioBus()
        {
            SelectAudioBus(-1);
        }

        public void SelectNextAudioBus()
        {
            SelectAudioBus(1);
        }

        public void AttachAudioService(IAudioService audioService, AudioMixController audioMixController = null)
        {
            bool wasDirty = SettingsUi.Dirty;
            EnsureCharacterTestDefaultAudioBus(wasDirty);

            if (_audioSettingsApplier == null)
            {
                if (audioService == null)
                    return;

                _audioSettingsApplier = new CharacterTestAudioSettingsApplier(AudioApplierId);
                RegisterApplierIfMissing(SettingsService, _audioSettingsApplier);
            }

            _audioSettingsApplier.Attach(audioService, audioMixController);
            MxSettingsProfile audioProfile = wasDirty
                ? SettingsService.CurrentProfile
                : SettingsUi.WorkingProfile;
            LastApplyResult = _audioSettingsApplier.Apply(audioProfile);
        }

        public MxSettingsLoadResult ReloadProfile()
        {
            LastLoadResult = SettingsUi.ReloadProfile();
            EnsureCharacterTestDefaultAudioBus(false);
            return LastLoadResult;
        }

        public MxSettingsApplyResult ApplyCurrentProfile()
        {
            LastApplyResult = SettingsUi.ApplyWorkingProfile();
            return LastApplyResult;
        }

        public MxSettingsSaveResult SaveCurrentProfile()
        {
            LastSaveResult = SettingsUi.SaveWorkingProfile();
            return LastSaveResult;
        }

        public MxSettingsSaveResult SaveCurrentProfile(MxSettingsProfile profile)
        {
            SettingsUi.SetWorkingProfile(profile);
            return SaveCurrentProfile();
        }

        public void SetLocalizationLocale(string localeId)
        {
            SettingsUi.SetLocalizationLocale(localeId);
        }

        private void SelectAudioBus(int direction)
        {
            int count = AudioMixBusIds.PlayerAdjustable.Length;
            if (count == 0)
                return;

            _selectedAudioBusIndex = (_selectedAudioBusIndex + direction) % count;
            if (_selectedAudioBusIndex < 0)
                _selectedAudioBusIndex += count;
        }

        public static CharacterTestSettingsRuntime Create(
            MonoBehaviour owner,
            string settingsFilePath,
            IRuntimeLogger logger = null,
            IReadOnlyList<MxSettingsUiOptionViewModel> localizationOptions = null)
        {
            var service = new MxSettingsService(new MxSettingsJsonFileStore(settingsFilePath));
            MxSettingsLoadResult loadResult = service.Load();
            LogLoadResult(loadResult, settingsFilePath, logger);

            MxInputRebindingSettingsController inputSettings = CreateInputSettings(owner, logger);
            var runtime = new CharacterTestSettingsRuntime(
                settingsFilePath,
                service,
                inputSettings,
                loadResult,
                new MxUnityGraphicsSettingsApplier(
                    new MxUnityGraphicsRuntime(),
                    new MxUnityGraphicsSettingsApplyOptions(
                        applyTargetFrameRate: false,
                        applyQualityLevel: false)),
                inputBindingTargets: CreateDefaultInputBindingTargets(),
                localizationOptions: localizationOptions);
            runtime.AttachHdrOutput(new MxUnityHdrOutputRuntime(), ownsRuntime: true);
            if (owner is GameManager manager)
            {
                runtime.AttachDirectionalShadowSettings(() => manager.SceneRendering);
                runtime.AttachWaterSettings(() => manager.ShallowWater);
            }
            return runtime;
        }

        public static IReadOnlyList<MxInputBindingTarget> CreateDefaultInputBindingTargets()
        {
            return new[]
            {
                Binding(InputActionNames.GameplayMap, InputActionNames.Jump, 0),
                Binding(InputActionNames.GameplayMap, InputActionNames.Jump, 1),
                Binding(InputActionNames.GameplayMap, InputActionNames.AttackPrimary, 0),
                Binding(InputActionNames.GameplayMap, InputActionNames.AttackPrimary, 1),
                Binding(InputActionNames.GameplayMap, InputActionNames.AttackSecondary, 0),
                Binding(InputActionNames.GameplayMap, InputActionNames.AttackSecondary, 1),
                Binding(InputActionNames.GameplayMap, InputActionNames.Interact, 0),
                Binding(InputActionNames.GameplayMap, InputActionNames.Interact, 1),
                Binding(InputActionNames.GameplayMap, InputActionNames.Dodge, 0),
                Binding(InputActionNames.GameplayMap, InputActionNames.Dodge, 1),
                Binding(InputActionNames.GameplayMap, InputActionNames.Sprint, 0),
                Binding(InputActionNames.GameplayMap, InputActionNames.Sprint, 1),
                Binding(InputActionNames.GameplayMap, InputActionNames.Pause, 0),
                Binding(InputActionNames.GameplayMap, InputActionNames.Pause, 1),
                Binding(InputActionNames.GameplayMap, InputActionNames.TargetLock, 0),
                Binding(InputActionNames.GameplayMap, InputActionNames.TargetLock, 1),
                Binding(InputActionNames.GameplayMap, InputActionNames.TargetLock, 2),
                Binding(InputActionNames.UIMap, InputActionNames.Submit, 0),
                Binding(InputActionNames.UIMap, InputActionNames.Submit, 1),
                Binding(InputActionNames.UIMap, InputActionNames.Cancel, 0),
                Binding(InputActionNames.UIMap, InputActionNames.Cancel, 1)
            };
        }

        private static MxInputRebindingSettingsController CreateInputSettings(
            MonoBehaviour owner,
            IRuntimeLogger logger)
        {
            IInputProvider inputProvider = InputProviderResolver.ResolveOrCreateDefault(owner);
            var inputService = inputProvider as InputService;
            if (inputService == null)
            {
                logger?.Warning(
                    LogCategory,
                    "Input settings unavailable because the resolved input provider is not InputService.");
                return new MxInputRebindingSettingsController(null, null);
            }

            _ = inputService.Snapshot;
            return new MxInputRebindingSettingsController(
                inputService.Rebinding,
                inputService.BindingDisplay);
        }

        private static void LogLoadResult(
            MxSettingsLoadResult result,
            string settingsFilePath,
            IRuntimeLogger logger)
        {
            if (logger == null || result == null)
                return;

            if (!result.HasIssues)
            {
                logger.Info(LogCategory, "Settings loaded from " + settingsFilePath + ".");
                return;
            }

            for (int i = 0; i < result.Issues.Count; i++)
            {
                MxSettingsIssue issue = result.Issues[i];
                string message = issue.Code + " " + issue.Path + ": " + issue.Message;
                if (issue.Code == MxSettingsIssueCode.MissingFile)
                    logger.Info(LogCategory, message);
                else
                    logger.Warning(LogCategory, message);
            }
        }

        private static void RegisterApplierIfMissing(
            MxSettingsService settingsService,
            IMxSettingsApplier applier)
        {
            if (settingsService == null || applier == null)
                return;

            IReadOnlyList<IMxSettingsApplier> appliers = settingsService.Appliers;
            for (int i = 0; i < appliers.Count; i++)
            {
                IMxSettingsApplier existing = appliers[i];
                if (ReferenceEquals(existing, applier))
                    return;

                if (existing != null &&
                    string.Equals(existing.ApplierId, applier.ApplierId, StringComparison.Ordinal))
                {
                    return;
                }
            }

            settingsService.RegisterApplier(applier);
        }

        private static MxInputBindingTarget Binding(
            string mapName,
            string actionName,
            int bindingIndex)
        {
            return new MxInputBindingTarget(mapName + "/" + actionName, bindingIndex);
        }

        private static MxInputBindingTarget[] CopyInputBindingTargets(
            IReadOnlyList<MxInputBindingTarget> targets)
        {
            if (targets == null || targets.Count == 0)
                return Array.Empty<MxInputBindingTarget>();

            var copy = new MxInputBindingTarget[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                copy[i] = targets[i];
            return copy;
        }

        private void EnsureCharacterTestDefaultAudioBus(bool dirty)
        {
            MxSettingsProfile profile = SettingsUi.WorkingProfile ?? MxSettingsProfile.CreateDefault();
            MxAudioSettings migrated = MxAudioSettingsMigration.Migrate(profile.Audio, AudioDefaults);
            IReadOnlyList<MxAudioBusSetting> existing = profile.Audio?.BusSettings ?? Array.Empty<MxAudioBusSetting>();
            if (AudioSettingsEqual(existing, migrated.BusSettings))
                return;
            SettingsUi.SetWorkingProfile(profile.WithAudio(migrated), dirty);
        }

        private static bool AudioSettingsEqual(
            IReadOnlyList<MxAudioBusSetting> left,
            IReadOnlyList<MxAudioBusSetting> right)
        {
            if (left == null || right == null || left.Count != right.Count)
                return false;
            for (int i = 0; i < left.Count; i++)
            {
                if (left[i].BusId != right[i].BusId
                    || left[i].Volume != right[i].Volume
                    || left[i].Muted != right[i].Muted)
                    return false;
            }
            return true;
        }

        private sealed class CharacterTestAudioSettingsApplier : IMxSettingsApplier
        {
            private IAudioService _audioService;
            private AudioMixController _audioMixController;

            public CharacterTestAudioSettingsApplier(string applierId)
            {
                ApplierId = string.IsNullOrWhiteSpace(applierId) ? AudioApplierId : applierId;
            }

            public string ApplierId { get; }

            public void Attach(IAudioService audioService, AudioMixController audioMixController)
            {
                _audioService = audioService;
                _audioMixController = audioMixController;
            }

            public MxSettingsApplyResult Apply(MxSettingsProfile profile)
            {
                IReadOnlyList<MxAudioBusSetting> buses =
                    profile?.Audio?.BusSettings ?? Array.Empty<MxAudioBusSetting>();
                if (buses.Count == 0)
                    return MxSettingsApplyResult.Succeeded();

                if (_audioService == null)
                {
                    return MxSettingsApplyResult.Failed(new MxSettingsIssue(
                        MxSettingsIssueCode.AdapterUnavailable,
                        ApplierId,
                        "CharacterTest audio service is not configured."));
                }

                if (_audioMixController != null)
                {
                    var settings = new AudioMixSetting[buses.Count];
                    for (int i = 0; i < buses.Count; i++)
                    {
                        MxAudioBusSetting bus = buses[i];
                        settings[i] = new AudioMixSetting(bus.BusId, bus.Volume, bus.Muted);
                    }
                    AudioResult mixResult = _audioMixController.ApplyUserSettings(settings);
                    return mixResult.Success
                        ? MxSettingsApplyResult.Succeeded()
                        : MxSettingsApplyResult.Failed(ToIssue("$.audio", 0, mixResult));
                }

                var issues = new List<MxSettingsIssue>();
                for (int i = 0; i < buses.Count; i++)
                {
                    MxAudioBusSetting bus = buses[i];
                    string busPath = "$.audio.busSettings[" + i + "]";
                    AudioResult volumeResult = _audioService.SetBusVolume(bus.BusId, bus.Volume);
                    if (volumeResult.Failed)
                    {
                        issues.Add(ToIssue(busPath + ".volume", bus.BusId, volumeResult));
                        if (volumeResult.ErrorCode == AudioErrorCode.InvalidBus)
                            continue;
                    }

                    AudioResult mutedResult = _audioService.SetBusMuted(bus.BusId, bus.Muted);
                    if (mutedResult.Failed)
                        issues.Add(ToIssue(busPath + ".muted", bus.BusId, mutedResult));
                }

                return issues.Count == 0
                    ? MxSettingsApplyResult.Succeeded()
                    : MxSettingsApplyResult.Failed(issues);
            }

            private static MxSettingsIssue ToIssue(string path, int busId, AudioResult result)
            {
                return new MxSettingsIssue(
                    MxSettingsIssueCode.ApplyFailed,
                    path,
                    "Failed to apply CharacterTest audio bus "
                    + busId
                    + ": "
                    + result.ErrorCode
                    + " "
                    + result.Message);
            }
        }
    }
}
