using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Settings;
using MxFramework.Settings.Input;
using MxFramework.Settings.UI;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiSettingsPanelCommandSink :
        IMxUiCommandSink,
        IMxUiCommandOutcomeSource
    {
        internal const string VolumeDraftCommand = "character_test.settings.audio.volume.draft";
        private const string ApplyFailureCode = "settings.apply";
        private const string SaveFailureCode = "settings.save";
        private const string ReloadFailureCode = "settings.reload";
        private const string InputRebindFailureCode = "settings.input.rebind";
        private const string InputSaveFailureCode = "settings.input.save";
        private const string InputResetFailureCode = "settings.input.reset";

        private static readonly int[] AntiAliasingValues = { 0, 1, 2, 4, 8 };
        private static readonly int[] ShadowResolutionValues = { 0, 256, 512, 1024, 2048, 4096, 8192 };
        private static readonly int[] DirectionalShadowQualityValues = { 0, 2, 3, 4 };
        private static readonly int[] ShadowCascadeValues = { 0, 1, 2, 3, 4 };
        private static readonly int[] HdrPaperWhiteValues = { 0, 80, 100, 160, 200, 250, 300, 400 };
        private static readonly int[] HdrPeakValues = { 0, 400, 600, 800, 1000, 1500, 2000, 4000, 5000 };

        private readonly CharacterTestSettingsRuntime _runtime;
        private readonly CharacterTestLocalizationRuntime _localizationRuntime;
        private readonly Func<bool> _closePanel;
        private readonly MxUiCommandOutcomeChannel _outcomes =
            new MxUiCommandOutcomeChannel();

        public CharacterTestFairyGuiSettingsPanelCommandSink(
            CharacterTestSettingsRuntime runtime,
            Func<bool> closePanel = null,
            CharacterTestLocalizationRuntime localizationRuntime = null)
        {
            _runtime = runtime;
            _closePanel = closePanel;
            _localizationRuntime = localizationRuntime;
        }

        public int CommandCount { get; private set; }
        public int ApplyCount { get; private set; }
        public int SaveCount { get; private set; }
        public int ReloadCount { get; private set; }
        public int CloseCount { get; private set; }
        public MxUiCommand LastCommand { get; private set; }
        public MxSettingsApplyResult LastApplyResult { get; private set; } = MxSettingsApplyResult.Succeeded();
        public MxSettingsSaveResult LastSaveResult { get; private set; } = MxSettingsSaveResult.Succeeded();
        public MxSettingsLoadResult LastLoadResult { get; private set; } =
            MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault());
        public MxSettingsUiOperationResult LastInputResult { get; private set; } =
            MxSettingsUiOperationResult.Succeeded();
        public string LastStatusMessage { get; private set; } = string.Empty;
        public CharacterTestSettingsPage SelectedPage { get; private set; } =
            CharacterTestSettingsPage.Controls;
        public CharacterTestSettingsInputDevicePage SelectedInputDevice { get; private set; } =
            CharacterTestSettingsInputDevicePage.KeyboardMouse;
        public MxInputBindingTarget ActiveRebindTarget { get; private set; }

        public IMxUiCommandOutcomeBinding BindOutcomeSink(
            IMxUiCommandOutcomeSink sink)
        {
            return _outcomes.BindOutcomeSink(sink);
        }

        public void Enqueue(MxUiCommand command)
        {
            CommandCount++;
            LastCommand = command;

            if (command.CommandId == MxFairyGuiCommandIds.Cancel)
            {
                if (_runtime?.SettingsUi.BuildViewModel().IsRebinding == true)
                {
                    CancelActiveRebind();
                    return;
                }

                DiscardAndClose();
                return;
            }

            if (command.CommandId == CharacterTestFairyGuiSettingsPanelCommandIds.Close
                || command.CommandId == CharacterTestFairyGuiSettingsPanelCommandIds.CancelChanges)
            {
                DiscardAndClose();
                return;
            }

            if (_runtime == null)
            {
                LastStatusMessage = L("ui.character_test.settings.status.runtime_unavailable", "Settings runtime unavailable.");
                if (IsOutcomeCommand(command.CommandId))
                    CompleteOutcome(in command, false, "settings.runtime.unavailable");
                return;
            }

            switch (command.CommandId)
            {
                case VolumeDraftCommand:
                    if (command.Payload is CharacterTestSettingsVolumeDraft volume
                        && volume.BusId == _runtime.SelectedAudioBusId
                        && !float.IsNaN(volume.Value) && !float.IsInfinity(volume.Value))
                    {
                        _runtime.SettingsUi.SetAudioBusVolume(volume.BusId, Math.Max(0f, Math.Min(1f, volume.Value)));
                        LastStatusMessage = string.Empty;
                    }
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.Apply:
                    ApplyChanges(in command);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.Save:
                    SyncTextInputs(command.Payload as CharacterTestFairyGuiSettingsPanelEditPayload);
                    LastSaveResult = _runtime.SaveCurrentProfile();
                    _localizationRuntime?.SyncFromSettings(_runtime);
                    SaveCount++;
                    LastStatusMessage = FormatResult(L("ui.character_test.common.save", "Save"), LastSaveResult.Success, LastSaveResult.Issues);
                    CompleteOutcome(
                        in command,
                        LastSaveResult.Success,
                        SaveFailureCode);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.Reload:
                {
                    LastLoadResult = _runtime.ReloadProfile();
                    _localizationRuntime?.SyncFromSettings(_runtime);
                    ReloadCount++;
                    bool reloadSucceeded =
                        LastLoadResult.Success && !LastLoadResult.HasIssues;
                    LastStatusMessage = FormatResult(L("ui.character_test.settings.reload", "Reload"), reloadSucceeded, LastLoadResult.Issues);
                    CompleteOutcome(
                        in command,
                        reloadSucceeded,
                        ReloadFailureCode);
                    break;
                }
                case CharacterTestFairyGuiSettingsPanelCommandIds.AntiAliasingPrevious:
                    CycleAntiAliasing(-1);
                    LastStatusMessage = L("ui.character_test.settings.status.graphics_changed", "画面设置已更改。");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.AntiAliasingNext:
                    CycleAntiAliasing(1);
                    LastStatusMessage = L("ui.character_test.settings.status.graphics_changed", "画面设置已更改。");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.DirectionalShadowQualityPrevious:
                    CycleDirectionalShadowQuality(-1);
                    LastStatusMessage = L("ui.character_test.settings.status.graphics_changed", "画面设置已更改。");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.DirectionalShadowQualityNext:
                    CycleDirectionalShadowQuality(1);
                    LastStatusMessage = L("ui.character_test.settings.status.graphics_changed", "画面设置已更改。");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.ShadowCascadeCountPrevious:
                    CycleShadowCascades(-1);
                    LastStatusMessage = L("ui.character_test.settings.status.graphics_changed", "画面设置已更改。");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.ShadowCascadeCountNext:
                    CycleShadowCascades(1);
                    LastStatusMessage = L("ui.character_test.settings.status.graphics_changed", "画面设置已更改。");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.LocalShadowResolutionPrevious:
                    CycleLocalShadowResolution(-1);
                    LastStatusMessage = L("ui.character_test.settings.status.graphics_changed", "画面设置已更改。");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.LocalShadowResolutionNext:
                    CycleLocalShadowResolution(1);
                    LastStatusMessage = L("ui.character_test.settings.status.graphics_changed", "画面设置已更改。");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.LanguagePrevious:
                    SetLanguage(-1);
                    LastStatusMessage = L("ui.character_test.settings.status.language_changed", "Language changed.");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.LanguageNext:
                    SetLanguage(1);
                    LastStatusMessage = L("ui.character_test.settings.status.language_changed", "Language changed.");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.ToggleVSync:
                    ToggleVSync();
                    LastStatusMessage = L("ui.character_test.settings.status.vsync_changed", "VSync changed.");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.ToggleHdr:
                    ChangeHdr(0, 0);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.HdrPaperWhitePrevious:
                    ChangeHdr(1, -1);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.HdrPaperWhiteNext:
                    ChangeHdr(1, 1);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.HdrPeakPrevious:
                    ChangeHdr(2, -1);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.HdrPeakNext:
                    ChangeHdr(2, 1);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.AudioCategoryPrevious:
                    _runtime.SelectPreviousAudioBus();
                    LastStatusMessage = L("ui.character_test.settings.status.audio_category_changed", "Audio category changed.");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.AudioCategoryNext:
                    _runtime.SelectNextAudioBus();
                    LastStatusMessage = L("ui.character_test.settings.status.audio_category_changed", "Audio category changed.");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.ToggleMute:
                    ToggleMute();
                    LastStatusMessage = L("ui.character_test.settings.status.mute_changed", "Mute changed.");
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.StartInputRebind:
                    LastInputResult = StartInputRebind(command.Payload as CharacterTestFairyGuiSettingsPanelInputPayload);
                    LastStatusMessage = LastInputResult.Success
                        ? string.Empty
                        : FormatResult(L("ui.character_test.settings.input", "输入"), false, LastInputResult.Issues);
                    CompleteOutcome(
                        in command,
                        LastInputResult.Success,
                        InputRebindFailureCode);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.SaveInput:
                    LastInputResult = _runtime.SettingsUi.SaveInputOverrides();
                    LastStatusMessage = FormatResult(L("ui.character_test.settings.save_input", "Save input"), LastInputResult.Success, LastInputResult.Issues);
                    CompleteOutcome(
                        in command,
                        LastInputResult.Success,
                        InputSaveFailureCode);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.ResetInput:
                    LastInputResult = _runtime.SettingsUi.ResetInputToDefault();
                    ActiveRebindTarget = default;
                    LastStatusMessage = FormatResult(L("ui.character_test.settings.reset_input", "恢复默认"), LastInputResult.Success, LastInputResult.Issues);
                    CompleteOutcome(
                        in command,
                        LastInputResult.Success,
                        InputResetFailureCode);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.SelectGraphics:
                    SelectPage(CharacterTestSettingsPage.Graphics);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.SelectAudio:
                    SelectPage(CharacterTestSettingsPage.Audio);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.SelectControls:
                    SelectPage(CharacterTestSettingsPage.Controls);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.SelectKeyboardMouse:
                    SelectInputDevice(CharacterTestSettingsInputDevicePage.KeyboardMouse);
                    break;
                case CharacterTestFairyGuiSettingsPanelCommandIds.SelectGamepad:
                    SelectInputDevice(CharacterTestSettingsInputDevicePage.Gamepad);
                    break;
                default:
                    LastStatusMessage = L("ui.character_test.settings.status.unsupported_command", "Unsupported settings command")
                        + ": "
                        + command.CommandId;
                    break;
            }
        }

        private void CompleteOutcome(
            in MxUiCommand command,
            bool success,
            string failureCode)
        {
            if (command.InvocationId <= 0L)
                return;

            var outcome = new MxUiCommandOutcome(
                command.InvocationId,
                success
                    ? MxUiCommandOutcomeStatus.Success
                    : MxUiCommandOutcomeStatus.Failure,
                success ? null : failureCode);
            _outcomes.TryComplete(in outcome);
        }

        private static bool IsOutcomeCommand(string commandId)
        {
            return commandId == CharacterTestFairyGuiSettingsPanelCommandIds.Apply
                || commandId == CharacterTestFairyGuiSettingsPanelCommandIds.Save
                || commandId == CharacterTestFairyGuiSettingsPanelCommandIds.Reload
                || commandId == CharacterTestFairyGuiSettingsPanelCommandIds.StartInputRebind
                || commandId == CharacterTestFairyGuiSettingsPanelCommandIds.SaveInput
                || commandId == CharacterTestFairyGuiSettingsPanelCommandIds.ResetInput;
        }

        private void SyncTextInputs(CharacterTestFairyGuiSettingsPanelEditPayload payload)
        {
            if (payload == null)
                return;

            if (TryParseFloat(payload.RenderScaleText, out float renderScale))
                _runtime.SettingsUi.SetRenderScale(renderScale);

            if (TryParseFloat(payload.ShadowDistanceText, out float shadowDistance))
                _runtime.SettingsUi.SetShadowDistance(shadowDistance);

            if (TryParseFloat(payload.AudioVolumeText, out float audioVolume))
                _runtime.SettingsUi.SetAudioBusVolume(_runtime.SelectedAudioBusId, audioVolume);
        }

        private void CycleAntiAliasing(int direction)
        {
            MxGraphicsSettings graphics = _runtime.SettingsUi.WorkingProfile.Graphics ?? MxGraphicsSettings.Default;
            _runtime.SettingsUi.SetAntiAliasingSamples(CycleValue(
                AntiAliasingValues,
                graphics.AntiAliasingSamples,
                direction));
        }

        private void CycleDirectionalShadowQuality(int direction)
        {
            MxGraphicsSettings graphics = _runtime.SettingsUi.WorkingProfile.Graphics ?? MxGraphicsSettings.Default;
            _runtime.SettingsUi.SetDirectionalShadowQuality((MxGraphicsFeatureQuality)CycleValue(
                DirectionalShadowQualityValues, (int)graphics.DirectionalShadowQuality, direction));
        }

        private void CycleShadowCascades(int direction)
        {
            MxGraphicsSettings graphics = _runtime.SettingsUi.WorkingProfile.Graphics ?? MxGraphicsSettings.Default;
            _runtime.SettingsUi.SetShadowCascadeCount(CycleValue(
                ShadowCascadeValues,
                graphics.ShadowCascadeCount,
                direction));
        }

        private void CycleLocalShadowResolution(int direction)
        {
            MxGraphicsSettings graphics = _runtime.SettingsUi.WorkingProfile.Graphics ?? MxGraphicsSettings.Default;
            _runtime.SettingsUi.SetAdditionalLightsShadowmapResolution(CycleValue(
                ShadowResolutionValues,
                graphics.AdditionalLightsShadowmapResolution,
                direction));
        }

        private static int CycleValue(int[] values, int current, int direction)
        {
            int index = Array.IndexOf(values, current);
            if (index < 0)
                index = direction >= 0 ? 0 : values.Length - 1;
            else
                index = Wrap(index + direction, values.Length);
            return values[index];
        }

        private void ChangeHdr(int field, int direction)
        {
            var hdr = _runtime.SettingsUi.WorkingProfile.Graphics.HdrOutput;
            bool available = _runtime.HdrOutputStatus.Available;
            bool enabled = hdr.Mode == MxHdrOutputMode.Enabled ||
                hdr.Mode == MxHdrOutputMode.Default && _runtime.HdrOutputStatus.Active;
            if (!available && (field != 0 || hdr.Mode != MxHdrOutputMode.Enabled)) return;
            if (field != 0 && !enabled) return;
            var mode = field == 0 ? (enabled ? MxHdrOutputMode.Disabled : MxHdrOutputMode.Enabled) : MxHdrOutputMode.Enabled;
            float white = field == 1 ? CycleValue(HdrPaperWhiteValues, (int)hdr.PaperWhiteNits, direction) : hdr.PaperWhiteNits;
            float peak = field == 2 ? CycleValue(HdrPeakValues, (int)hdr.PeakNits, direction) : hdr.PeakNits;
            _runtime.SettingsUi.SetHdrOutput(new MxHdrOutputSettings(mode, white, peak));
            LastStatusMessage = L("ui.character_test.settings.status.graphics_changed", "画面设置已更改。");
        }

        private void ToggleVSync()
        {
            MxGraphicsSettings graphics =
                _runtime.SettingsUi.WorkingProfile.Graphics ?? MxGraphicsSettings.Default;
            _runtime.SettingsUi.SetVSyncCount(graphics.VSyncCount > 0 ? 0 : 1);
        }

        private void ToggleMute()
        {
            int busId = _runtime.SelectedAudioBusId;
            MxAudioBusSetting bus = ResolveAudioBus(_runtime.SettingsUi.WorkingProfile, busId);
            _runtime.SettingsUi.SetAudioBusMuted(busId, !bus.Muted);
        }

        private void SetLanguage(int direction)
        {
            MxSettingsUiItemViewModel item = FindLocalizationItem(_runtime.SettingsUi.BuildViewModel());
            IReadOnlyList<MxSettingsUiOptionViewModel> options =
                item?.Options ?? Array.Empty<MxSettingsUiOptionViewModel>();
            if (options.Count == 0)
                return;

            string current = (_runtime.SettingsUi.WorkingProfile.Localization ?? MxLocalizationSettings.Default).LocaleId;
            int index = 0;
            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].Value, current, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            index = Wrap(index + direction, options.Count);
            string next = options[index].Value;
            if (_localizationRuntime != null)
                _localizationRuntime.SetWorkingLocale(_runtime, next);
            else
                _runtime.SetLocalizationLocale(next);
        }

        private static MxSettingsUiItemViewModel FindLocalizationItem(MxSettingsUiViewModel view)
        {
            if (view == null)
                return null;

            for (int i = 0; i < view.Sections.Count; i++)
            {
                MxSettingsUiSectionViewModel section = view.Sections[i];
                if (section.Key != MxSettingsUiSectionKey.Localization)
                    continue;

                for (int j = 0; j < section.Items.Count; j++)
                {
                    if (section.Items[j].Key == MxSettingsUiItemKey.LocalizationLocale)
                        return section.Items[j];
                }
            }

            return null;
        }

        private MxSettingsUiOperationResult StartInputRebind(
            CharacterTestFairyGuiSettingsPanelInputPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Target.ActionId))
            {
                return MxSettingsUiOperationResult.FromInput(
                    MxInputSettingsOperationResult.Failed(new MxSettingsIssue(
                        MxSettingsIssueCode.InvalidTarget,
                        "settings.input.rebind",
                        "Input binding target is invalid.")));
            }

            MxSettingsUiOperationResult result = _runtime.SettingsUi.StartInputRebind(payload.Target);
            if (result.Success)
                ActiveRebindTarget = payload.Target;
            return result;
        }

        private void SelectPage(CharacterTestSettingsPage page)
        {
            CancelActiveRebindIfNeeded();
            SelectedPage = page;
            LastStatusMessage = string.Empty;
        }

        private void SelectInputDevice(CharacterTestSettingsInputDevicePage device)
        {
            CancelActiveRebindIfNeeded();
            SelectedInputDevice = device;
            LastStatusMessage = string.Empty;
        }

        private void CancelActiveRebindIfNeeded()
        {
            if (_runtime?.SettingsUi.BuildViewModel().IsRebinding == true)
                CancelActiveRebind();
            else
                ActiveRebindTarget = default;
        }

        private void CancelActiveRebind()
        {
            if (_runtime == null)
                return;

            LastInputResult = _runtime.SettingsUi.CancelInputRebind();
            ActiveRebindTarget = default;
            LastStatusMessage = LastInputResult.Success
                ? L("ui.character_test.settings.status.rebind_cancelled", "已取消按键监听。")
                : FormatResult(L("ui.character_test.settings.input", "输入"), false, LastInputResult.Issues);
        }

        private void ApplyChanges(in MxUiCommand command)
        {
            SyncTextInputs(command.Payload as CharacterTestFairyGuiSettingsPanelEditPayload);
            CancelActiveRebindIfNeeded();
            LastApplyResult = _runtime.ApplyCurrentProfile();
            ApplyCount++;
            bool success = LastApplyResult.Success;
            IReadOnlyList<MxSettingsIssue> issues = LastApplyResult.Issues;
            if (success)
            {
                LastSaveResult = _runtime.SaveCurrentProfile();
                SaveCount++;
                success = LastSaveResult.Success;
                issues = LastSaveResult.Issues;
            }

            if (success)
            {
                LastInputResult = _runtime.SettingsUi.CommitInputEditSession();
                success = LastInputResult.Success;
                issues = LastInputResult.Issues;
            }

            if (success)
            {
                LastInputResult = _runtime.SettingsUi.BeginInputEditSession();
                success = LastInputResult.Success;
                issues = LastInputResult.Issues;
            }

            if (success)
                _localizationRuntime?.SyncFromSettings(_runtime);

            LastStatusMessage = FormatResult(
                L("ui.character_test.common.apply", "应用"),
                success,
                issues);
            CompleteOutcome(in command, success, ApplyFailureCode);
        }

        private void DiscardAndClose()
        {
            if (_runtime != null)
            {
                if (_runtime.SettingsUi.BuildViewModel().IsRebinding)
                    _runtime.SettingsUi.CancelInputRebind();
                ActiveRebindTarget = default;
                _runtime.SettingsUi.RollbackInputEditSession();
                LastLoadResult = _runtime.ReloadProfile();
                _localizationRuntime?.SyncFromSettings(_runtime);
                ReloadCount++;
            }

            CloseCount++;
            _closePanel?.Invoke();
            LastStatusMessage = L("ui.character_test.settings.status.closed", "已取消更改。");
        }

        private static MxAudioBusSetting ResolveAudioBus(MxSettingsProfile profile, int busId)
        {
            IReadOnlyList<MxAudioBusSetting> buses =
                profile?.Audio?.BusSettings ?? Array.Empty<MxAudioBusSetting>();
            for (int i = 0; i < buses.Count; i++)
            {
                if (buses[i].BusId == busId)
                    return buses[i];
            }

            return new MxAudioBusSetting(busId, 1f, false);
        }

        private static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static int Wrap(int index, int count)
        {
            if (count <= 0)
                return 0;

            while (index < 0)
                index += count;
            while (index >= count)
                index -= count;
            return index;
        }

        private static string FormatResult(string operation, bool success, IReadOnlyList<MxSettingsIssue> issues)
        {
            if (success && (issues == null || issues.Count == 0))
                return operation + " ok.";

            if (issues != null && issues.Count > 0)
                return operation + ": " + issues[0].Code + " " + issues[0].Message;

            return operation + " failed.";
        }

        private string L(string key, string fallback)
        {
            return _localizationRuntime != null
                ? _localizationRuntime.Resolve(key, fallback)
                : fallback;
        }
    }
}
