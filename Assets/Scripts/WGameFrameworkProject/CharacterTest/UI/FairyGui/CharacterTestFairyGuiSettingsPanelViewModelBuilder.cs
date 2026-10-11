using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Audio;
using MxFramework.Input;
using MxFramework.Settings;
using MxFramework.Settings.Input;
using MxFramework.Settings.UI;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiSettingsPanelViewModelBuilder
    {
        public static CharacterTestFairyGuiSettingsPanelViewModel Build(
            CharacterTestSettingsRuntime runtime,
            string statusOverride = "",
            IMxUiTextProvider textProvider = null)
        {
            return Build(
                runtime,
                CharacterTestSettingsPage.Controls,
                CharacterTestSettingsInputDevicePage.KeyboardMouse,
                default,
                statusOverride,
                textProvider);
        }

        public static CharacterTestFairyGuiSettingsPanelViewModel Build(
            CharacterTestSettingsRuntime runtime,
            CharacterTestSettingsPage selectedPage,
            CharacterTestSettingsInputDevicePage selectedInputDevice,
            MxInputBindingTarget activeRebindTarget,
            string statusOverride = "",
            IMxUiTextProvider textProvider = null)
        {
            if (runtime == null)
            {
                return new CharacterTestFairyGuiSettingsPanelViewModel
                {
                    Title = L(textProvider, "ui.character_test.settings.title", "设置"),
                    Subtitle = L(textProvider, "ui.character_test.settings.subtitle", "调整游戏体验与操作方式"),
                    PageTitle = L(textProvider, "ui.character_test.settings.controls", "按键设置"),
                    StatusText = L(
                        textProvider,
                        "ui.character_test.settings.status.runtime_unavailable",
                        "设置运行时不可用。"),
                    DirtyText = L(textProvider, "ui.character_test.status.unavailable", "不可用"),
                    SelectedPage = selectedPage,
                    SelectedInputDevice = selectedInputDevice,
                    CanEdit = false
                };
            }

            MxSettingsUiViewModel view = runtime.SettingsUi.BuildViewModel();
            MxSettingsProfile profile = runtime.SettingsUi.WorkingProfile ??
                MxSettingsProfile.CreateDefault();
            MxGraphicsSettings graphics = profile.Graphics ?? MxGraphicsSettings.Default;
            int audioBusId = runtime.SelectedAudioBusId;
            MxAudioBusSetting bus = ResolveAudioBus(profile, audioBusId);
            MxLocalizationSettings localization = profile.Localization ?? MxLocalizationSettings.Default;
            MxSettingsUiItemViewModel localeItem = FindItem(
                FindSection(view, MxSettingsUiSectionKey.Localization),
                MxSettingsUiItemKey.LocalizationLocale);
            bool dirty = view.Dirty || runtime.SettingsUi.InputDraftDirty;

            var viewModel = new CharacterTestFairyGuiSettingsPanelViewModel
            {
                Title = L(textProvider, "ui.character_test.settings.title", "设置"),
                Subtitle = L(textProvider, "ui.character_test.settings.subtitle", "调整游戏体验与操作方式"),
                PageTitle = PageTitle(selectedPage, textProvider),
                PageDescription = PageDescription(selectedPage, textProvider),
                InputInstruction = L(
                    textProvider,
                    "ui.character_test.settings.input_instruction",
                    "点击按键框开始重新绑定；监听时按 Esc 取消。"),
                ActionColumnTitle = L(textProvider, "ui.character_test.settings.column.action", "操作"),
                PrimaryColumnTitle = L(textProvider, "ui.character_test.settings.column.primary", "主要按键"),
                SecondaryColumnTitle = L(textProvider, "ui.character_test.settings.column.secondary", "备用按键"),
                StatusText = FormatStatus(view, dirty, statusOverride, textProvider),
                Dirty = dirty,
                DirtyText = dirty
                    ? L(textProvider, "ui.character_test.settings.dirty", "未应用")
                    : L(textProvider, "ui.character_test.settings.saved", "已保存"),
                IsRebinding = view.IsRebinding,
                SelectedPage = selectedPage,
                SelectedInputDevice = selectedInputDevice,
                QualityPreset = graphics.QualityPreset,
                QualityText = graphics.QualityPreset.ToString(),
                VSyncCount = graphics.VSyncCount,
                VSyncText = FormatVSync(graphics.VSyncCount, textProvider),
                RenderScaleText = graphics.RenderScale <= 0f
                    ? L(textProvider, "ui.character_test.settings.current", "当前")
                    : FormatFloat(graphics.RenderScale),
                AntiAliasingText = FormatOptionalInt(graphics.AntiAliasingSamples, textProvider),
                ShadowDistanceText = graphics.ShadowDistance < 0f
                    ? L(textProvider, "ui.character_test.settings.current", "当前")
                    : FormatFloat(graphics.ShadowDistance),
                DirectionalShadowQualityText = FormatDirectionalShadowQuality(graphics.DirectionalShadowQuality, textProvider),
                ShadowCascadeCountText = FormatOptionalInt(graphics.ShadowCascadeCount, textProvider),
                LocalShadowResolutionText = FormatOptionalInt(graphics.AdditionalLightsShadowmapResolution, textProvider),
                HdrModeText = (graphics.HdrOutput.Mode == MxHdrOutputMode.Enabled ||
                    graphics.HdrOutput.Mode == MxHdrOutputMode.Default && runtime.HdrOutputStatus.Active)
                    ? L(textProvider, "ui.character_test.settings.hdr.on", "开启") : L(textProvider, "ui.character_test.settings.hdr.off", "关闭"),
                HdrStatusText = FormatHdrStatus(runtime.HdrOutputStatus, textProvider),
                HdrPaperWhiteText = FormatHdrNits(graphics.HdrOutput.PaperWhiteNits, textProvider),
                HdrPeakText = FormatHdrNits(graphics.HdrOutput.PeakNits, textProvider),
                CanToggleHdr = runtime.HdrOutputStatus.Available || graphics.HdrOutput.Mode == MxHdrOutputMode.Enabled,
                CanCalibrateHdr = runtime.HdrOutputStatus.Available && (graphics.HdrOutput.Mode == MxHdrOutputMode.Enabled ||
                    graphics.HdrOutput.Mode == MxHdrOutputMode.Default && runtime.HdrOutputStatus.Active),
                ResolutionText = graphics.ResolutionWidth > 0 && graphics.ResolutionHeight > 0
                    ? graphics.ResolutionWidth + " x " + graphics.ResolutionHeight
                    : L(textProvider, "ui.character_test.settings.current", "当前"),
                WindowModeText = graphics.ApplyWindowMode
                    ? graphics.WindowMode.ToString()
                    : L(textProvider, "ui.character_test.settings.current", "当前"),
                LocaleId = localization.LocaleId,
                LocaleText = localeItem != null && !string.IsNullOrWhiteSpace(localeItem.ValueText)
                    ? localeItem.ValueText
                    : localization.LocaleId,
                AudioBusId = audioBusId,
                AudioCategoryText = FormatAudioBus(audioBusId),
                AudioVolume = bus.Volume,
                AudioVolumeText = FormatFloat(bus.Volume),
                AudioMuted = bus.Muted,
                AudioMutedText = bus.Muted
                    ? L(textProvider, "ui.character_test.settings.muted", "静音")
                    : L(textProvider, "ui.character_test.settings.on", "开启"),
                CanEdit = true
            };
            viewModel.SetInputRows(BuildInputRows(
                runtime,
                view,
                selectedInputDevice,
                activeRebindTarget,
                textProvider));
            return viewModel;
        }

        private static string FormatDirectionalShadowQuality(MxGraphicsFeatureQuality quality, IMxUiTextProvider textProvider)
        {
            switch (quality)
            {
                case MxGraphicsFeatureQuality.Low: return L(textProvider, "ui.character_test.settings.shadow_quality.low", "低");
                case MxGraphicsFeatureQuality.Medium: return L(textProvider, "ui.character_test.settings.shadow_quality.medium", "中");
                case MxGraphicsFeatureQuality.High: return L(textProvider, "ui.character_test.settings.shadow_quality.high", "高");
                default: return L(textProvider, "ui.character_test.settings.shadow_quality.default", "默认");
            }
        }

        private static IReadOnlyList<CharacterTestFairyGuiSettingsInputRowViewModel> BuildInputRows(
            CharacterTestSettingsRuntime runtime,
            MxSettingsUiViewModel view,
            CharacterTestSettingsInputDevicePage device,
            MxInputBindingTarget activeRebindTarget,
            IMxUiTextProvider textProvider)
        {
            MxSettingsUiSectionViewModel section = FindSection(view, MxSettingsUiSectionKey.Input);
            bool isRebinding = view?.IsRebinding == true;
            var rows = new List<CharacterTestFairyGuiSettingsInputRowViewModel>(12)
            {
                SectionRow(0, L(textProvider, "ui.character_test.settings.group.gameplay", "游戏操作"))
            };

            AddBindingRow(rows, section, device, isRebinding, activeRebindTarget,
                InputActionNames.GameplayMap, InputActionNames.Jump,
                L(textProvider, "ui.character_test.settings.action.jump", "跳跃"), 0, -1, 1, textProvider);
            AddBindingRow(rows, section, device, isRebinding, activeRebindTarget,
                InputActionNames.GameplayMap, InputActionNames.AttackPrimary,
                L(textProvider, "ui.character_test.settings.action.attack_primary", "主要攻击"), 0, -1, 1, textProvider);
            AddBindingRow(rows, section, device, isRebinding, activeRebindTarget,
                InputActionNames.GameplayMap, InputActionNames.AttackSecondary,
                L(textProvider, "ui.character_test.settings.action.attack_secondary", "次要攻击"), 0, -1, 1, textProvider);
            AddBindingRow(rows, section, device, isRebinding, activeRebindTarget,
                InputActionNames.GameplayMap, InputActionNames.Interact,
                L(textProvider, "ui.character_test.settings.action.interact", "互动"), 0, -1, 1, textProvider);
            AddBindingRow(rows, section, device, isRebinding, activeRebindTarget,
                InputActionNames.GameplayMap, InputActionNames.Dodge,
                L(textProvider, "ui.character_test.settings.action.dodge", "闪避"), 0, -1, 1, textProvider);
            AddBindingRow(rows, section, device, isRebinding, activeRebindTarget,
                InputActionNames.GameplayMap, InputActionNames.Sprint,
                L(textProvider, "ui.character_test.settings.action.sprint", "冲刺"), 0, -1, 1, textProvider);
            AddBindingRow(rows, section, device, isRebinding, activeRebindTarget,
                InputActionNames.GameplayMap, InputActionNames.Pause,
                L(textProvider, "ui.character_test.settings.action.pause", "暂停"), 0, -1, 1, textProvider);
            AddBindingRow(rows, section, device, isRebinding, activeRebindTarget,
                InputActionNames.GameplayMap, InputActionNames.TargetLock,
                L(textProvider, "ui.character_test.settings.action.target_lock", "锁定目标"), 0, 1, 2, textProvider);

            rows.Add(SectionRow(
                rows.Count,
                L(textProvider, "ui.character_test.settings.group.interface", "界面")));
            AddBindingRow(rows, section, device, isRebinding, activeRebindTarget,
                InputActionNames.UIMap, InputActionNames.Submit,
                L(textProvider, "ui.character_test.settings.action.submit", "确认"), 0, -1, 1, textProvider);
            AddBindingRow(rows, section, device, isRebinding, activeRebindTarget,
                InputActionNames.UIMap, InputActionNames.Cancel,
                L(textProvider, "ui.character_test.settings.action.cancel", "返回 / 取消"), 0, -1, 1, textProvider);
            return rows;
        }

        private static void AddBindingRow(
            ICollection<CharacterTestFairyGuiSettingsInputRowViewModel> rows,
            MxSettingsUiSectionViewModel section,
            CharacterTestSettingsInputDevicePage device,
            bool isRebinding,
            MxInputBindingTarget activeRebindTarget,
            string mapName,
            string actionName,
            string label,
            int keyboardPrimaryIndex,
            int keyboardSecondaryIndex,
            int gamepadPrimaryIndex,
            IMxUiTextProvider textProvider)
        {
            int rowIndex = rows.Count;
            int primaryIndex = device == CharacterTestSettingsInputDevicePage.KeyboardMouse
                ? keyboardPrimaryIndex
                : gamepadPrimaryIndex;
            int secondaryIndex = device == CharacterTestSettingsInputDevicePage.KeyboardMouse
                ? keyboardSecondaryIndex
                : -1;
            MxInputBindingTarget primary = Target(mapName, actionName, primaryIndex);
            MxInputBindingTarget secondary = secondaryIndex >= 0
                ? Target(mapName, actionName, secondaryIndex)
                : default;
            MxSettingsUiItemViewModel primaryItem = FindInputItem(section, primary);
            MxSettingsUiItemViewModel secondaryItem = secondaryIndex >= 0
                ? FindInputItem(section, secondary)
                : null;
            bool primaryListening = isRebinding && SameTarget(primary, activeRebindTarget);
            bool secondaryListening = isRebinding && SameTarget(secondary, activeRebindTarget);
            string unavailable = L(textProvider, "ui.character_test.settings.input_unavailable", "不可用");
            string listening = L(textProvider, "ui.character_test.settings.waiting_input", "等待输入…");

            rows.Add(new CharacterTestFairyGuiSettingsInputRowViewModel
            {
                RowIndex = rowIndex,
                Kind = CharacterTestSettingsInputRowKind.Binding,
                Label = label,
                PrimaryTarget = primary,
                SecondaryTarget = secondary,
                PrimaryValue = primaryListening
                    ? listening
                    : DisplayValue(primaryItem, unavailable),
                SecondaryValue = secondaryListening
                    ? listening
                    : secondaryIndex < 0 ? "—" : DisplayValue(secondaryItem, unavailable),
                PrimaryEnabled = primaryItem != null && primaryItem.Enabled && !isRebinding,
                SecondaryEnabled = secondaryItem != null && secondaryItem.Enabled && !isRebinding,
                PrimaryListening = primaryListening,
                SecondaryListening = secondaryListening
            });
        }

        private static CharacterTestFairyGuiSettingsInputRowViewModel SectionRow(
            int rowIndex,
            string label)
        {
            return new CharacterTestFairyGuiSettingsInputRowViewModel
            {
                RowIndex = rowIndex,
                Kind = CharacterTestSettingsInputRowKind.Section,
                Label = label
            };
        }

        private static string DisplayValue(MxSettingsUiItemViewModel item, string unavailable)
        {
            return item != null && !string.IsNullOrWhiteSpace(item.ValueText)
                ? item.ValueText
                : unavailable;
        }

        private static MxInputBindingTarget Target(
            string mapName,
            string actionName,
            int bindingIndex)
        {
            return new MxInputBindingTarget(mapName + "/" + actionName, bindingIndex);
        }

        private static bool SameTarget(MxInputBindingTarget left, MxInputBindingTarget right)
        {
            return left.BindingIndex >= 0
                && left.BindingIndex == right.BindingIndex
                && string.Equals(left.ActionId, right.ActionId, StringComparison.Ordinal);
        }

        private static string PageTitle(
            CharacterTestSettingsPage page,
            IMxUiTextProvider textProvider)
        {
            switch (page)
            {
                case CharacterTestSettingsPage.Graphics:
                    return L(textProvider, "ui.character_test.settings.graphics", "画面设置");
                case CharacterTestSettingsPage.Audio:
                    return L(textProvider, "ui.character_test.settings.audio", "音频设置");
                default:
                    return L(textProvider, "ui.character_test.settings.controls", "按键设置");
            }
        }

        private static string PageDescription(
            CharacterTestSettingsPage page,
            IMxUiTextProvider textProvider)
        {
            switch (page)
            {
                case CharacterTestSettingsPage.Graphics:
                    return L(textProvider, "ui.character_test.settings.graphics_description", "逐项调整画面、阴影与语言");
                case CharacterTestSettingsPage.Audio:
                    return L(textProvider, "ui.character_test.settings.audio_description", "调整各类声音的音量与静音状态");
                default:
                    return L(textProvider, "ui.character_test.settings.controls_description", "为键盘、鼠标或手柄重新分配操作");
            }
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

        private static string FormatAudioBus(int busId)
        {
            switch (busId)
            {
                case AudioMixBusIds.Master: return "Master";
                case AudioMixBusIds.Music: return "Music";
                case AudioMixBusIds.Sfx: return "SFX";
                case AudioMixBusIds.Ui: return "UI";
                case AudioMixBusIds.Ambience: return "Ambience";
                case AudioMixBusIds.Voice: return "Voice";
                default: return busId.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static MxSettingsUiSectionViewModel FindSection(
            MxSettingsUiViewModel view,
            MxSettingsUiSectionKey key)
        {
            if (view == null)
                return null;

            for (int i = 0; i < view.Sections.Count; i++)
            {
                if (view.Sections[i].Key == key)
                    return view.Sections[i];
            }

            return null;
        }

        private static MxSettingsUiItemViewModel FindInputItem(
            MxSettingsUiSectionViewModel section,
            MxInputBindingTarget target)
        {
            if (section == null || string.IsNullOrWhiteSpace(target.ActionId))
                return null;

            string id = "input." + target.ActionId + "[" + target.BindingIndex + "]";
            for (int i = 0; i < section.Items.Count; i++)
            {
                MxSettingsUiItemViewModel item = section.Items[i];
                if (string.Equals(item.Id, id, StringComparison.Ordinal))
                    return item;
            }

            return null;
        }

        private static string FormatHdrNits(float value, IMxUiTextProvider provider) => value <= 0
            ? L(provider, "ui.character_test.settings.hdr.auto", "自动") : FormatFloat(value) + " nit";

        private static string FormatHdrStatus(MxHdrOutputStatus status, IMxUiTextProvider provider)
        {
            switch (status.State)
            {
                case MxHdrOutputState.Hdr: return L(provider, "ui.character_test.settings.hdr.active", "当前输出：HDR");
                case MxHdrOutputState.Switching: return L(provider, "ui.character_test.settings.hdr.switching", "正在切换显示模式…");
                case MxHdrOutputState.Failed: return L(provider, "ui.character_test.settings.hdr.failed", "显示切换失败，请检查系统 HDR 设置。");
                case MxHdrOutputState.Sdr: return L(provider, "ui.character_test.settings.hdr.sdr", "当前输出：SDR");
                default: return L(provider, "ui.character_test.settings.hdr.unavailable", "当前输出：SDR\n此显示环境暂不支持 HDR。");
            }
        }

        private static MxSettingsUiItemViewModel FindItem(
            MxSettingsUiSectionViewModel section,
            MxSettingsUiItemKey key)
        {
            if (section == null)
                return null;

            for (int i = 0; i < section.Items.Count; i++)
            {
                MxSettingsUiItemViewModel item = section.Items[i];
                if (item.Key == key)
                    return item;
            }

            return null;
        }

        private static string FormatStatus(
            MxSettingsUiViewModel view,
            bool dirty,
            string statusOverride,
            IMxUiTextProvider textProvider)
        {
            if (view == null)
                return L(textProvider, "ui.character_test.settings.status.unavailable", "设置不可用。");
            if (view.IsRebinding)
                return L(textProvider, "ui.character_test.settings.status.waiting_input", "等待输入…");
            if (!string.IsNullOrWhiteSpace(statusOverride))
                return statusOverride;
            if (view.HasIssues)
                return view.Issues[0].Code + ": " + view.Issues[0].Message;
            return dirty
                ? L(textProvider, "ui.character_test.settings.status.unsaved", "有尚未应用的更改")
                : L(textProvider, "ui.character_test.settings.status.ready", "就绪");
        }

        private static string FormatVSync(int value, IMxUiTextProvider textProvider)
        {
            if (value < 0)
                return L(textProvider, "ui.character_test.settings.current", "当前");
            return value == 0
                ? L(textProvider, "ui.character_test.settings.off", "关闭")
                : value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatOptionalInt(int value, IMxUiTextProvider textProvider)
        {
            return value <= 0
                ? L(textProvider, "ui.character_test.settings.current", "当前")
                : value.ToString(CultureInfo.InvariantCulture);
        }

        private static string L(IMxUiTextProvider provider, string key, string fallback)
        {
            return CharacterTestUiText.Resolve(provider, key, fallback);
        }
    }
}
