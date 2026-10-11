using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Settings.Input;

namespace MxFramework.Settings.UI
{
    public sealed class MxSettingsUiPresenter
    {
        private readonly MxSettingsService _settingsService;
        private readonly MxInputRebindingSettingsController _inputSettings;
        private readonly MxInputBindingTarget[] _inputBindingTargets;
        private readonly MxSettingsUiOptionViewModel[] _localizationOptions;
        private MxSettingsProfile _workingProfile;
        private bool _dirty;
        private MxSettingsUiOperationResult _lastOperationResult = MxSettingsUiOperationResult.Succeeded();

        public MxSettingsUiPresenter(
            MxSettingsService settingsService,
            MxInputRebindingSettingsController inputSettings = null,
            IReadOnlyList<MxInputBindingTarget> inputBindingTargets = null,
            IReadOnlyList<MxSettingsUiOptionViewModel> localizationOptions = null)
        {
            _settingsService = settingsService ?? new MxSettingsService(null);
            _inputSettings = inputSettings ?? new MxInputRebindingSettingsController(null, null);
            _inputBindingTargets = Copy(inputBindingTargets);
            _localizationOptions = CopyOptions(localizationOptions) ?? DefaultLocalizationOptions();
            _workingProfile = _settingsService.CurrentProfile ?? MxSettingsProfile.CreateDefault();
        }

        public MxSettingsProfile WorkingProfile => _workingProfile;
        public bool Dirty => _dirty;
        public bool InputDraftDirty => _inputSettings.HasPendingChanges;
        public bool InputEditSessionActive => _inputSettings.IsEditSessionActive;
        public MxSettingsUiOperationResult LastOperationResult => _lastOperationResult;

        public MxSettingsUiViewModel BuildViewModel()
        {
            var sections = new List<MxSettingsUiSectionViewModel>
            {
                BuildLocalizationSection(),
                BuildGraphicsSection(),
                BuildAudioSection(),
                BuildInputSection(out IReadOnlyList<MxSettingsIssue> inputIssues)
            };

            var issues = new List<MxSettingsIssue>();
            AddIssues(issues, _lastOperationResult.Issues);
            AddIssues(issues, inputIssues);

            return new MxSettingsUiViewModel(
                _workingProfile.ProfileId,
                _workingProfile.UpdatedUtc,
                _dirty,
                _inputSettings.IsRebinding,
                sections,
                issues);
        }

        public MxSettingsUiOperationResult Reload()
        {
            ReloadProfile();
            return _lastOperationResult;
        }

        public MxSettingsLoadResult ReloadProfile()
        {
            MxSettingsLoadResult result = _settingsService.Load();
            _workingProfile = _settingsService.CurrentProfile ?? MxSettingsProfile.CreateDefault();
            _dirty = false;
            _lastOperationResult = MxSettingsUiOperationResult.FromLoad(result);
            return result;
        }

        public MxSettingsUiOperationResult Apply()
        {
            ApplyWorkingProfile();
            return _lastOperationResult;
        }

        public MxSettingsApplyResult ApplyWorkingProfile()
        {
            MxSettingsApplyResult result = _settingsService.Apply(_workingProfile);
            _lastOperationResult = MxSettingsUiOperationResult.FromApply(result);
            return result;
        }

        public MxSettingsUiOperationResult Save()
        {
            SaveWorkingProfile();
            return _lastOperationResult;
        }

        public MxSettingsSaveResult SaveWorkingProfile()
        {
            MxSettingsSaveResult result = _settingsService.Save(_workingProfile);
            if (result != null && result.Success)
            {
                _workingProfile = _settingsService.CurrentProfile ?? _workingProfile;
                _dirty = false;
            }

            _lastOperationResult = MxSettingsUiOperationResult.FromSave(result);
            return result;
        }

        public MxSettingsUiOperationResult ResetDirty()
        {
            _workingProfile = _settingsService.CurrentProfile ?? MxSettingsProfile.CreateDefault();
            _dirty = false;
            _lastOperationResult = MxSettingsUiOperationResult.Succeeded();
            return _lastOperationResult;
        }

        public void SetWorkingProfile(MxSettingsProfile profile, bool dirty = true)
        {
            _workingProfile = profile ?? MxSettingsProfile.CreateDefault();
            _dirty = dirty;
            _lastOperationResult = MxSettingsUiOperationResult.Succeeded();
        }

        public void SetGraphicsQualityPreset(MxGraphicsQualityPreset value)
        {
            MxGraphicsSettings graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            UpdateGraphics(new MxGraphicsSettings(
                value,
                graphics.ResolutionWidth,
                graphics.ResolutionHeight,
                graphics.ApplyWindowMode,
                graphics.WindowMode,
                graphics.VSyncCount,
                graphics.TargetFrameRate,
                graphics.RenderScale,
                graphics.AntiAliasingSamples,
                graphics.ShadowDistance,
                graphics.VolumetricFogQuality,
                graphics.UnityQualityLevel,
                graphics.MainLightShadowmapResolution,
                graphics.ShadowCascadeCount,
                graphics.AdditionalLightsShadowmapResolution,
                graphics.HdrOutput, graphics.DirectionalShadowQuality));
        }

        public void SetResolution(int width, int height)
        {
            MxGraphicsSettings graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            UpdateGraphics(new MxGraphicsSettings(
                graphics.QualityPreset,
                width,
                height,
                graphics.ApplyWindowMode,
                graphics.WindowMode,
                graphics.VSyncCount,
                graphics.TargetFrameRate,
                graphics.RenderScale,
                graphics.AntiAliasingSamples,
                graphics.ShadowDistance,
                graphics.VolumetricFogQuality,
                graphics.UnityQualityLevel,
                graphics.MainLightShadowmapResolution,
                graphics.ShadowCascadeCount,
                graphics.AdditionalLightsShadowmapResolution,
                graphics.HdrOutput, graphics.DirectionalShadowQuality));
        }

        public void SetWindowMode(MxWindowMode value, bool applyWindowMode = true)
        {
            MxGraphicsSettings graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            UpdateGraphics(new MxGraphicsSettings(
                graphics.QualityPreset,
                graphics.ResolutionWidth,
                graphics.ResolutionHeight,
                applyWindowMode,
                value,
                graphics.VSyncCount,
                graphics.TargetFrameRate,
                graphics.RenderScale,
                graphics.AntiAliasingSamples,
                graphics.ShadowDistance,
                graphics.VolumetricFogQuality,
                graphics.UnityQualityLevel,
                graphics.MainLightShadowmapResolution,
                graphics.ShadowCascadeCount,
                graphics.AdditionalLightsShadowmapResolution,
                graphics.HdrOutput, graphics.DirectionalShadowQuality));
        }

        public void SetVSyncCount(int value)
        {
            MxGraphicsSettings graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            UpdateGraphics(new MxGraphicsSettings(
                graphics.QualityPreset,
                graphics.ResolutionWidth,
                graphics.ResolutionHeight,
                graphics.ApplyWindowMode,
                graphics.WindowMode,
                value,
                graphics.TargetFrameRate,
                graphics.RenderScale,
                graphics.AntiAliasingSamples,
                graphics.ShadowDistance,
                graphics.VolumetricFogQuality,
                graphics.UnityQualityLevel,
                graphics.MainLightShadowmapResolution,
                graphics.ShadowCascadeCount,
                graphics.AdditionalLightsShadowmapResolution,
                graphics.HdrOutput, graphics.DirectionalShadowQuality));
        }

        public void SetTargetFrameRate(int value)
        {
            MxGraphicsSettings graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            UpdateGraphics(new MxGraphicsSettings(
                graphics.QualityPreset,
                graphics.ResolutionWidth,
                graphics.ResolutionHeight,
                graphics.ApplyWindowMode,
                graphics.WindowMode,
                graphics.VSyncCount,
                value,
                graphics.RenderScale,
                graphics.AntiAliasingSamples,
                graphics.ShadowDistance,
                graphics.VolumetricFogQuality,
                graphics.UnityQualityLevel,
                graphics.MainLightShadowmapResolution,
                graphics.ShadowCascadeCount,
                graphics.AdditionalLightsShadowmapResolution,
                graphics.HdrOutput, graphics.DirectionalShadowQuality));
        }

        public void SetRenderScale(float value)
        {
            MxGraphicsSettings graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            UpdateGraphics(new MxGraphicsSettings(
                graphics.QualityPreset,
                graphics.ResolutionWidth,
                graphics.ResolutionHeight,
                graphics.ApplyWindowMode,
                graphics.WindowMode,
                graphics.VSyncCount,
                graphics.TargetFrameRate,
                value,
                graphics.AntiAliasingSamples,
                graphics.ShadowDistance,
                graphics.VolumetricFogQuality,
                graphics.UnityQualityLevel,
                graphics.MainLightShadowmapResolution,
                graphics.ShadowCascadeCount,
                graphics.AdditionalLightsShadowmapResolution,
                graphics.HdrOutput, graphics.DirectionalShadowQuality));
        }

        public void SetDirectionalShadowQuality(MxGraphicsFeatureQuality value)
        {
            var graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            UpdateGraphics(CopyGraphics(graphics, directionalShadowQuality: value));
        }

        public void SetHdrOutput(MxHdrOutputSettings value)
        {
            MxGraphicsSettings graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            UpdateGraphics(CopyGraphics(graphics, hdrOutput: value ?? MxHdrOutputSettings.Default));
        }

        public void SetAntiAliasingSamples(int value)
        {
            MxGraphicsSettings graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            UpdateGraphics(CopyGraphics(
                graphics,
                antiAliasingSamples: value));
        }

        public void SetShadowDistance(float value)
        {
            MxGraphicsSettings graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            UpdateGraphics(CopyGraphics(
                graphics,
                shadowDistance: value));
        }

        public void SetMainLightShadowmapResolution(int value)
        {
            MxGraphicsSettings graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            UpdateGraphics(CopyGraphics(
                graphics,
                mainLightShadowmapResolution: value));
        }

        public void SetShadowCascadeCount(int value)
        {
            MxGraphicsSettings graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            UpdateGraphics(CopyGraphics(
                graphics,
                shadowCascadeCount: value));
        }

        public void SetAdditionalLightsShadowmapResolution(int value)
        {
            MxGraphicsSettings graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            UpdateGraphics(CopyGraphics(
                graphics,
                additionalLightsShadowmapResolution: value));
        }

        public void SetAudioBusVolume(int busId, float volume)
        {
            UpdateAudioBus(busId, volume, null);
        }

        public void SetAudioBusMuted(int busId, bool muted)
        {
            UpdateAudioBus(busId, null, muted);
        }

        public void SetLocalizationLocale(string localeId)
        {
            UpdateLocalization(new MxLocalizationSettings(localeId));
        }

        public MxSettingsUiOperationResult StartInputRebind(MxInputBindingTarget target)
        {
            MxInputSettingsOperationResult result =
                _inputSettings.StartRebind(target.ActionId, target.BindingIndex);
            _lastOperationResult = MxSettingsUiOperationResult.FromInput(result);
            return _lastOperationResult;
        }

        public MxSettingsUiOperationResult CancelInputRebind()
        {
            MxInputSettingsOperationResult result = _inputSettings.Cancel();
            _lastOperationResult = MxSettingsUiOperationResult.FromInput(result);
            return _lastOperationResult;
        }

        public MxSettingsUiOperationResult LoadInputOverrides()
        {
            MxInputSettingsOperationResult result = _inputSettings.Load();
            _lastOperationResult = MxSettingsUiOperationResult.FromInput(result);
            return _lastOperationResult;
        }

        public MxSettingsUiOperationResult SaveInputOverrides()
        {
            MxInputSettingsOperationResult result = _inputSettings.Save();
            _lastOperationResult = MxSettingsUiOperationResult.FromInput(result);
            return _lastOperationResult;
        }

        public MxSettingsUiOperationResult ResetInputToDefault()
        {
            MxInputSettingsOperationResult result = _inputSettings.ResetToDefault();
            _lastOperationResult = MxSettingsUiOperationResult.FromInput(result);
            return _lastOperationResult;
        }

        public MxSettingsUiOperationResult BeginInputEditSession()
        {
            MxInputSettingsOperationResult result = _inputSettings.BeginEditSession();
            _lastOperationResult = MxSettingsUiOperationResult.FromInput(result);
            return _lastOperationResult;
        }

        public MxSettingsUiOperationResult CommitInputEditSession()
        {
            MxInputSettingsOperationResult result = _inputSettings.CommitEditSession();
            _lastOperationResult = MxSettingsUiOperationResult.FromInput(result);
            return _lastOperationResult;
        }

        public MxSettingsUiOperationResult RollbackInputEditSession()
        {
            MxInputSettingsOperationResult result = _inputSettings.RollbackEditSession();
            _lastOperationResult = MxSettingsUiOperationResult.FromInput(result);
            return _lastOperationResult;
        }

        private MxSettingsUiSectionViewModel BuildGraphicsSection()
        {
            MxGraphicsSettings graphics = _workingProfile.Graphics ?? MxGraphicsSettings.Default;
            var items = new List<MxSettingsUiItemViewModel>();
            IReadOnlyList<MxGraphicsSettingDescriptor> descriptors =
                MxGraphicsSettingsCatalog.PlayerVisibleDescriptors;

            for (int i = 0; i < descriptors.Count; i++)
            {
                switch (descriptors[i].Key)
                {
                    case MxGraphicsSettingKey.QualityPreset:
                        items.Add(new MxSettingsUiItemViewModel(
                            MxSettingsUiSectionKey.Graphics,
                            MxSettingsUiItemKey.GraphicsQualityPreset,
                            "graphics.qualityPreset",
                            "Quality",
                            ((int)graphics.QualityPreset).ToString(CultureInfo.InvariantCulture),
                            graphics.QualityPreset.ToString(),
                            MxSettingsUiControlKind.Select,
                            options: QualityPresetOptions()));
                        break;
                    case MxGraphicsSettingKey.Resolution:
                        items.Add(new MxSettingsUiItemViewModel(
                            MxSettingsUiSectionKey.Graphics,
                            MxSettingsUiItemKey.GraphicsResolution,
                            "graphics.resolution",
                            "Resolution",
                            graphics.ResolutionWidth + "x" + graphics.ResolutionHeight,
                            FormatResolution(graphics),
                            MxSettingsUiControlKind.Label));
                        break;
                    case MxGraphicsSettingKey.WindowMode:
                        items.Add(new MxSettingsUiItemViewModel(
                            MxSettingsUiSectionKey.Graphics,
                            MxSettingsUiItemKey.GraphicsWindowMode,
                            "graphics.windowMode",
                            "Window Mode",
                            ((int)graphics.WindowMode).ToString(CultureInfo.InvariantCulture),
                            graphics.ApplyWindowMode ? graphics.WindowMode.ToString() : "Current",
                            MxSettingsUiControlKind.Select,
                            options: WindowModeOptions()));
                        break;
                    case MxGraphicsSettingKey.VSync:
                        items.Add(new MxSettingsUiItemViewModel(
                            MxSettingsUiSectionKey.Graphics,
                            MxSettingsUiItemKey.GraphicsVSync,
                            "graphics.vSyncCount",
                            "VSync",
                            graphics.VSyncCount.ToString(CultureInfo.InvariantCulture),
                            FormatVSync(graphics.VSyncCount),
                            MxSettingsUiControlKind.Integer));
                        break;
                    case MxGraphicsSettingKey.TargetFrameRate:
                        items.Add(new MxSettingsUiItemViewModel(
                            MxSettingsUiSectionKey.Graphics,
                            MxSettingsUiItemKey.GraphicsTargetFrameRate,
                            "graphics.targetFrameRate",
                            "Frame Rate Cap",
                            graphics.TargetFrameRate.ToString(CultureInfo.InvariantCulture),
                            graphics.TargetFrameRate <= 0 ? "Current" : graphics.TargetFrameRate + " fps",
                            MxSettingsUiControlKind.Integer));
                        break;
                    case MxGraphicsSettingKey.RenderScale:
                        items.Add(new MxSettingsUiItemViewModel(
                            MxSettingsUiSectionKey.Graphics,
                            MxSettingsUiItemKey.GraphicsRenderScale,
                            "graphics.renderScale",
                            "Render Scale",
                            FormatFloat(graphics.RenderScale),
                            graphics.RenderScale <= 0f ? "Current" : FormatFloat(graphics.RenderScale),
                            MxSettingsUiControlKind.Float));
                        break;
                    case MxGraphicsSettingKey.AntiAliasingSamples:
                        items.Add(GraphicsIntegerItem(
                            MxSettingsUiItemKey.GraphicsAntiAliasingSamples,
                            "graphics.antiAliasingSamples",
                            "MSAA",
                            graphics.AntiAliasingSamples));
                        break;
                    case MxGraphicsSettingKey.ShadowDistance:
                        items.Add(new MxSettingsUiItemViewModel(
                            MxSettingsUiSectionKey.Graphics,
                            MxSettingsUiItemKey.GraphicsShadowDistance,
                            "graphics.shadowDistance",
                            "Shadow Distance",
                            FormatFloat(graphics.ShadowDistance),
                            graphics.ShadowDistance < 0f ? "Current" : FormatFloat(graphics.ShadowDistance),
                            MxSettingsUiControlKind.Float));
                        break;
                    case MxGraphicsSettingKey.MainLightShadowmapResolution:
                        items.Add(GraphicsIntegerItem(
                            MxSettingsUiItemKey.GraphicsMainLightShadowmapResolution,
                            "graphics.mainLightShadowmapResolution",
                            "Main Shadow Resolution",
                            graphics.MainLightShadowmapResolution));
                        break;
                    case MxGraphicsSettingKey.ShadowCascadeCount:
                        items.Add(GraphicsIntegerItem(
                            MxSettingsUiItemKey.GraphicsShadowCascadeCount,
                            "graphics.shadowCascadeCount",
                            "Shadow Cascades",
                            graphics.ShadowCascadeCount));
                        break;
                    case MxGraphicsSettingKey.AdditionalLightsShadowmapResolution:
                        items.Add(GraphicsIntegerItem(
                            MxSettingsUiItemKey.GraphicsAdditionalLightsShadowmapResolution,
                            "graphics.additionalLightsShadowmapResolution",
                            "Local Shadow Resolution",
                            graphics.AdditionalLightsShadowmapResolution));
                        break;
                    case MxGraphicsSettingKey.DirectionalShadowQuality:
                        items.Add(new MxSettingsUiItemViewModel(MxSettingsUiSectionKey.Graphics,
                            MxSettingsUiItemKey.GraphicsDirectionalShadowQuality, "graphics.directionalShadowQuality", "Shadow Quality",
                            ((int)graphics.DirectionalShadowQuality).ToString(CultureInfo.InvariantCulture),
                            graphics.DirectionalShadowQuality.ToString(), MxSettingsUiControlKind.Select,
                            options: new[] { Option(0, "Default"), Option(2, "Low"), Option(3, "Medium"), Option(4, "High") }));
                        break;
                    case MxGraphicsSettingKey.HdrOutput:
                        items.Add(new MxSettingsUiItemViewModel(MxSettingsUiSectionKey.Graphics,
                            MxSettingsUiItemKey.GraphicsHdrOutput, "graphics.hdrOutput.mode", "HDR Output",
                            ((int)graphics.HdrOutput.Mode).ToString(CultureInfo.InvariantCulture),
                            graphics.HdrOutput.Mode.ToString(), MxSettingsUiControlKind.Select));
                        break;
                    case MxGraphicsSettingKey.HdrPaperWhite:
                        items.Add(new MxSettingsUiItemViewModel(MxSettingsUiSectionKey.Graphics,
                            MxSettingsUiItemKey.GraphicsHdrPaperWhite, "graphics.hdrOutput.paperWhiteNits", "HDR Paper White",
                            FormatFloat(graphics.HdrOutput.PaperWhiteNits),
                            graphics.HdrOutput.PaperWhiteNits == 0 ? "Automatic" : FormatFloat(graphics.HdrOutput.PaperWhiteNits) + " nits",
                            MxSettingsUiControlKind.Float));
                        break;
                    case MxGraphicsSettingKey.HdrPeakBrightness:
                        items.Add(new MxSettingsUiItemViewModel(MxSettingsUiSectionKey.Graphics,
                            MxSettingsUiItemKey.GraphicsHdrPeakBrightness, "graphics.hdrOutput.peakNits", "HDR Peak Brightness",
                            FormatFloat(graphics.HdrOutput.PeakNits),
                            graphics.HdrOutput.PeakNits == 0 ? "Automatic" : FormatFloat(graphics.HdrOutput.PeakNits) + " nits",
                            MxSettingsUiControlKind.Float));
                        break;
                }
            }

            return new MxSettingsUiSectionViewModel(MxSettingsUiSectionKey.Graphics, "Graphics", items);
        }

        private MxSettingsUiSectionViewModel BuildLocalizationSection()
        {
            MxLocalizationSettings localization =
                _workingProfile.Localization ?? MxLocalizationSettings.Default;
            MxSettingsUiOptionViewModel selected = FindOption(_localizationOptions, localization.LocaleId);
            string valueText = selected != null ? selected.Label : localization.LocaleId;
            var items = new[]
            {
                new MxSettingsUiItemViewModel(
                    MxSettingsUiSectionKey.Localization,
                    MxSettingsUiItemKey.LocalizationLocale,
                    "localization.localeId",
                    "Language",
                    localization.LocaleId,
                    valueText,
                    MxSettingsUiControlKind.Select,
                    options: _localizationOptions)
            };

            return new MxSettingsUiSectionViewModel(MxSettingsUiSectionKey.Localization, "Localization", items);
        }

        private MxSettingsUiSectionViewModel BuildAudioSection()
        {
            IReadOnlyList<MxAudioBusSetting> buses =
                (_workingProfile.Audio ?? MxAudioSettings.Empty).BusSettings;
            var items = new List<MxSettingsUiItemViewModel>(buses.Count * 2);
            for (int i = 0; i < buses.Count; i++)
            {
                MxAudioBusSetting bus = buses[i];
                string busId = bus.BusId.ToString(CultureInfo.InvariantCulture);
                items.Add(new MxSettingsUiItemViewModel(
                    MxSettingsUiSectionKey.Audio,
                    MxSettingsUiItemKey.AudioBusVolume,
                    "audio.bus." + busId + ".volume",
                    "Bus " + busId + " Volume",
                    FormatFloat(bus.Volume),
                    FormatFloat(bus.Volume),
                    MxSettingsUiControlKind.Float));
                items.Add(new MxSettingsUiItemViewModel(
                    MxSettingsUiSectionKey.Audio,
                    MxSettingsUiItemKey.AudioBusMuted,
                    "audio.bus." + busId + ".muted",
                    "Bus " + busId + " Muted",
                    bus.Muted ? "true" : "false",
                    bus.Muted ? "Muted" : "On",
                    MxSettingsUiControlKind.Toggle));
            }

            return new MxSettingsUiSectionViewModel(MxSettingsUiSectionKey.Audio, "Audio", items);
        }

        private MxSettingsUiSectionViewModel BuildInputSection(out IReadOnlyList<MxSettingsIssue> issues)
        {
            issues = Array.Empty<MxSettingsIssue>();
            if (_inputBindingTargets.Length == 0)
                return new MxSettingsUiSectionViewModel(MxSettingsUiSectionKey.Input, "Input", Array.Empty<MxSettingsUiItemViewModel>());

            MxInputBindingListResult result = _inputSettings.ListBindings(_inputBindingTargets);
            issues = result.Issues;
            var items = new List<MxSettingsUiItemViewModel>(result.Bindings.Count);
            for (int i = 0; i < result.Bindings.Count; i++)
            {
                MxInputBindingInfo binding = result.Bindings[i];
                string id = "input." + binding.Target.ActionId + "[" + binding.Target.BindingIndex + "]";
                items.Add(new MxSettingsUiItemViewModel(
                    MxSettingsUiSectionKey.Input,
                    MxSettingsUiItemKey.InputBinding,
                    id,
                    binding.Target.ActionId,
                    binding.DisplayString,
                    binding.DisplayString,
                    MxSettingsUiControlKind.Command,
                    enabled: _inputSettings.CanRebind));
            }

            return new MxSettingsUiSectionViewModel(MxSettingsUiSectionKey.Input, "Input", items);
        }

        private void UpdateGraphics(MxGraphicsSettings graphics)
        {
            _workingProfile = _workingProfile.WithGraphics(graphics);
            _dirty = true;
        }

        private static MxSettingsUiItemViewModel GraphicsIntegerItem(
            MxSettingsUiItemKey key,
            string settingPath,
            string label,
            int value)
        {
            return new MxSettingsUiItemViewModel(
                MxSettingsUiSectionKey.Graphics,
                key,
                settingPath,
                label,
                value.ToString(CultureInfo.InvariantCulture),
                value <= 0 ? "Current" : value.ToString(CultureInfo.InvariantCulture),
                MxSettingsUiControlKind.Integer);
        }

        private static MxGraphicsSettings CopyGraphics(
            MxGraphicsSettings graphics,
            int? antiAliasingSamples = null,
            float? shadowDistance = null,
            int? mainLightShadowmapResolution = null,
            int? shadowCascadeCount = null,
            int? additionalLightsShadowmapResolution = null,
            MxHdrOutputSettings hdrOutput = null,
            MxGraphicsFeatureQuality? directionalShadowQuality = null)
        {
            return new MxGraphicsSettings(
                graphics.QualityPreset,
                graphics.ResolutionWidth,
                graphics.ResolutionHeight,
                graphics.ApplyWindowMode,
                graphics.WindowMode,
                graphics.VSyncCount,
                graphics.TargetFrameRate,
                graphics.RenderScale,
                antiAliasingSamples ?? graphics.AntiAliasingSamples,
                shadowDistance ?? graphics.ShadowDistance,
                graphics.VolumetricFogQuality,
                graphics.UnityQualityLevel,
                mainLightShadowmapResolution ?? graphics.MainLightShadowmapResolution,
                shadowCascadeCount ?? graphics.ShadowCascadeCount,
                additionalLightsShadowmapResolution ?? graphics.AdditionalLightsShadowmapResolution,
                hdrOutput ?? graphics.HdrOutput, directionalShadowQuality ?? graphics.DirectionalShadowQuality);
        }

        private void UpdateAudioBus(int busId, float? volume, bool? muted)
        {
            IReadOnlyList<MxAudioBusSetting> existing =
                (_workingProfile.Audio ?? MxAudioSettings.Empty).BusSettings;
            var buses = new List<MxAudioBusSetting>(existing.Count + 1);
            bool replaced = false;
            for (int i = 0; i < existing.Count; i++)
            {
                MxAudioBusSetting bus = existing[i];
                if (bus.BusId != busId)
                {
                    buses.Add(bus);
                    continue;
                }

                buses.Add(new MxAudioBusSetting(
                    busId,
                    volume.HasValue ? volume.Value : bus.Volume,
                    muted.HasValue ? muted.Value : bus.Muted));
                replaced = true;
            }

            if (!replaced)
            {
                buses.Add(new MxAudioBusSetting(
                    busId,
                    volume.HasValue ? volume.Value : 1f,
                    muted.HasValue && muted.Value));
            }

            _workingProfile = _workingProfile.WithAudio(new MxAudioSettings(buses));
            _dirty = true;
        }

        private void UpdateLocalization(MxLocalizationSettings localization)
        {
            _workingProfile = _workingProfile.WithLocalization(localization);
            _dirty = true;
        }

        private static void AddIssues(List<MxSettingsIssue> target, IReadOnlyList<MxSettingsIssue> issues)
        {
            if (target == null || issues == null)
                return;

            for (int i = 0; i < issues.Count; i++)
            {
                if (!issues[i].IsNone)
                    target.Add(issues[i]);
            }
        }

        private static MxInputBindingTarget[] Copy(IReadOnlyList<MxInputBindingTarget> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<MxInputBindingTarget>();

            var copy = new MxInputBindingTarget[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }

        private static MxSettingsUiOptionViewModel[] CopyOptions(IReadOnlyList<MxSettingsUiOptionViewModel> values)
        {
            if (values == null || values.Count == 0)
                return null;

            var copy = new MxSettingsUiOptionViewModel[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i] ?? new MxSettingsUiOptionViewModel(string.Empty, string.Empty);
            return copy;
        }

        private static MxSettingsUiOptionViewModel[] DefaultLocalizationOptions()
        {
            return new[]
            {
                new MxSettingsUiOptionViewModel(MxLocalizationSettings.DefaultLocaleId, "Chinese (Simplified)"),
                new MxSettingsUiOptionViewModel("en-US", "English")
            };
        }

        private static IReadOnlyList<MxSettingsUiOptionViewModel> QualityPresetOptions()
        {
            return new[]
            {
                Option((int)MxGraphicsQualityPreset.Custom, "Custom"),
                Option((int)MxGraphicsQualityPreset.Low, "Low"),
                Option((int)MxGraphicsQualityPreset.Medium, "Medium"),
                Option((int)MxGraphicsQualityPreset.High, "High"),
                Option((int)MxGraphicsQualityPreset.Ultra, "Ultra")
            };
        }

        private static IReadOnlyList<MxSettingsUiOptionViewModel> WindowModeOptions()
        {
            return new[]
            {
                Option((int)MxWindowMode.Windowed, "Windowed"),
                Option((int)MxWindowMode.FullscreenWindow, "Fullscreen Window"),
                Option((int)MxWindowMode.ExclusiveFullscreen, "Exclusive Fullscreen"),
                Option((int)MxWindowMode.MaximizedWindow, "Maximized Window")
            };
        }

        private static MxSettingsUiOptionViewModel Option(int value, string label)
        {
            return new MxSettingsUiOptionViewModel(
                value.ToString(CultureInfo.InvariantCulture),
                label);
        }

        private static MxSettingsUiOptionViewModel FindOption(
            IReadOnlyList<MxSettingsUiOptionViewModel> options,
            string value)
        {
            if (options == null)
                return null;

            for (int i = 0; i < options.Count; i++)
            {
                MxSettingsUiOptionViewModel option = options[i];
                if (option != null && string.Equals(option.Value, value, StringComparison.Ordinal))
                    return option;
            }

            return null;
        }

        private static string FormatResolution(MxGraphicsSettings graphics)
        {
            return graphics.ResolutionWidth > 0 && graphics.ResolutionHeight > 0
                ? graphics.ResolutionWidth + " x " + graphics.ResolutionHeight
                : "Current";
        }

        private static string FormatVSync(int value)
        {
            if (value < 0)
                return "Current";
            return value == 0 ? "Off" : value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
