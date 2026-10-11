using MxFramework.Settings.Input;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiSettingsPanelBinder
    {
        private const float DesignWidth = 1280f;
        private const float DesignHeight = 800f;
        private static readonly Color32 BindingTextColor = new Color32(28, 49, 77, 255);
        private static readonly Color32 DisabledBindingTextColor = new Color32(93, 119, 138, 255);
        private static readonly Color32 SectionTextColor = new Color32(154, 119, 65, 255);
        private static readonly Color32 ListeningTextColor = new Color32(19, 79, 82, 255);
        private static readonly Color32 ActiveRowColor = new Color32(165, 218, 209, 90);
        private static readonly Color32 IdleRowColor = new Color32(201, 221, 234, 20);

        private static readonly string[] GraphicsPageChildren =
        {
            CharacterTestFairyGuiSettingsPanelIds.HdrLabel,
            CharacterTestFairyGuiSettingsPanelIds.HdrValue,
            CharacterTestFairyGuiSettingsPanelIds.HdrToggle,
            CharacterTestFairyGuiSettingsPanelIds.HdrStatus,
            CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhiteLabel,
            CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhiteValue,
            CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhitePrev,
            CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhiteNext,
            CharacterTestFairyGuiSettingsPanelIds.HdrPeakLabel,
            CharacterTestFairyGuiSettingsPanelIds.HdrPeakValue,
            CharacterTestFairyGuiSettingsPanelIds.HdrPeakPrev,
            CharacterTestFairyGuiSettingsPanelIds.HdrPeakNext,
            CharacterTestFairyGuiSettingsPanelIds.GraphicsHeader,
            "graphicsDivider",
            CharacterTestFairyGuiSettingsPanelIds.AntiAliasingLabel,
            CharacterTestFairyGuiSettingsPanelIds.AntiAliasingValue,
            CharacterTestFairyGuiSettingsPanelIds.AntiAliasingPrev,
            CharacterTestFairyGuiSettingsPanelIds.AntiAliasingNext,
            CharacterTestFairyGuiSettingsPanelIds.VSyncLabel,
            CharacterTestFairyGuiSettingsPanelIds.VSyncValue,
            CharacterTestFairyGuiSettingsPanelIds.VSyncToggle,
            CharacterTestFairyGuiSettingsPanelIds.RenderScaleLabel,
            "renderScaleInputBg",
            CharacterTestFairyGuiSettingsPanelIds.RenderScaleInput,
            CharacterTestFairyGuiSettingsPanelIds.ShadowDistanceLabel,
            "shadowDistanceInputBg",
            CharacterTestFairyGuiSettingsPanelIds.ShadowDistanceInput,
            CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityLabel,
            CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityValue,
            CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityPrev,
            CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityNext,
            CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountLabel,
            CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountValue,
            CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountPrev,
            CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountNext,
            CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionLabel,
            CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionValue,
            CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionPrev,
            CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionNext,
            CharacterTestFairyGuiSettingsPanelIds.ResolutionLabel,
            CharacterTestFairyGuiSettingsPanelIds.ResolutionValue,
            CharacterTestFairyGuiSettingsPanelIds.WindowModeLabel,
            CharacterTestFairyGuiSettingsPanelIds.WindowModeValue,
            CharacterTestFairyGuiSettingsPanelIds.LanguageLabel,
            CharacterTestFairyGuiSettingsPanelIds.LanguageValue,
            CharacterTestFairyGuiSettingsPanelIds.LanguagePrev,
            CharacterTestFairyGuiSettingsPanelIds.LanguageNext
        };

        private static readonly string[] AudioPageChildren =
        {
            CharacterTestFairyGuiSettingsPanelIds.AudioHeader,
            "audioDivider",
            CharacterTestFairyGuiSettingsPanelIds.AudioCategoryLabel,
            CharacterTestFairyGuiSettingsPanelIds.AudioCategoryValue,
            CharacterTestFairyGuiSettingsPanelIds.AudioCategoryPrevious,
            CharacterTestFairyGuiSettingsPanelIds.AudioCategoryNext,
            CharacterTestFairyGuiSettingsPanelIds.AudioVolumeLabel,
            "audioVolumeInputBg",
            CharacterTestFairyGuiSettingsPanelIds.AudioVolumeInput,
            CharacterTestFairyGuiSettingsPanelIds.AudioMutedValue,
            CharacterTestFairyGuiSettingsPanelIds.AudioMuteToggle
        };

        private static readonly string[] ControlsPageChildren =
        {
            CharacterTestFairyGuiSettingsPanelIds.ControlsHeader,
            CharacterTestFairyGuiSettingsPanelIds.KeyboardMouseTab,
            CharacterTestFairyGuiSettingsPanelIds.GamepadTab,
            CharacterTestFairyGuiSettingsPanelIds.InputInstruction,
            "inputHeaderBackground",
            CharacterTestFairyGuiSettingsPanelIds.ActionColumnTitle,
            CharacterTestFairyGuiSettingsPanelIds.PrimaryColumnTitle,
            CharacterTestFairyGuiSettingsPanelIds.SecondaryColumnTitle,
            CharacterTestFairyGuiSettingsPanelIds.InputList
            , "inputScrollTrack", "inputScrollThumb"
        };

        partial void OnAfterGeneratedBind(
            Fgui.GComponent root,
            CharacterTestFairyGuiSettingsPanelViewModel viewModel)
        {
            ApplyResponsiveLayout(root);
            ApplyPageVisibility(root, viewModel);
            BindInputList(root, viewModel);
            NormalizeDisabledButtonVisuals(root);
            ApplyFrostedTheme(root, viewModel);
            BindVolumeSlider(root, viewModel);
        }

        private static CharacterTestFairyGuiSettingsPanelEditPayload CreateEditPayload(
            Fgui.GComponent root)
        {
            return new CharacterTestFairyGuiSettingsPanelEditPayload
            {
                RenderScaleText = ReadText(
                    root,
                    CharacterTestFairyGuiSettingsPanelIds.RenderScaleInput,
                    string.Empty),
                ShadowDistanceText = ReadText(
                    root,
                    CharacterTestFairyGuiSettingsPanelIds.ShadowDistanceInput,
                    string.Empty),
                AudioVolumeText = ReadText(
                    root,
                    CharacterTestFairyGuiSettingsPanelIds.AudioVolumeInput,
                    string.Empty)
            };
        }

        private static CharacterTestFairyGuiSettingsPanelInputPayload CreateInputPayload(
            CharacterTestFairyGuiSettingsInputRowViewModel row,
            MxInputBindingTarget target)
        {
            return new CharacterTestFairyGuiSettingsPanelInputPayload
            {
                RowIndex = row?.RowIndex ?? -1,
                Target = target
            };
        }

        private void BindInputList(
            Fgui.GComponent root,
            CharacterTestFairyGuiSettingsPanelViewModel viewModel)
        {
            Fgui.GList list = root?.GetChild(CharacterTestFairyGuiSettingsPanelIds.InputList)?.asList;
            if (list == null || viewModel == null)
                return;

            list.layout = Fgui.ListLayoutType.SingleColumn;
            list.selectionMode = Fgui.ListSelectionMode.None;
            list.lineGap = 2;
            list.itemRenderer = (index, itemObject) =>
            {
                CharacterTestFairyGuiSettingsInputRowViewModel row =
                    index >= 0 && index < viewModel.InputRows.Count
                        ? viewModel.InputRows[index]
                        : null;
                Fgui.GComponent item = itemObject?.asCom;
                if (item == null || row == null)
                    return;

                bool section = row.IsSection;
                bool listening = row.PrimaryListening || row.SecondaryListening;
                itemObject.data = row;
                itemObject.SetSize(list.width, section ? 34f : 58f);
                itemObject.touchable = !section;
                itemObject.grayed = false;

                Fgui.GTextField label = item.GetChild("actionLabel")?.asTextField;
                if (label != null)
                {
                    label.text = section ? "◇  " + row.Label : row.Label ?? string.Empty;
                    label.color = section ? SectionTextColor : BindingTextColor;
                    var format = label.textFormat;
                    format.font = CharacterTestFairyGuiFonts.Control;
                    format.bold = true;
                    label.textFormat = format;
                }

                Fgui.GGraph background = item.GetChild("rowBackground")?.asGraph;
                if (background != null)
                {
                    background.visible = !section;
                    background.DrawRect(list.width, itemObject.height, 0, Color.clear,
                        listening ? ActiveRowColor : IdleRowColor);
                }

                Fgui.GObject activeBar = item.GetChild("activeBar");
                if (activeBar != null)
                    activeBar.visible = !section && listening;

                Fgui.GObject divider = item.GetChild("divider");
                if (divider != null)
                {
                    divider.visible = !section;
                    if (divider is Fgui.GGraph rule)
                        rule.DrawRect(list.width - 20f, 1f, 0, Color.clear, new Color32(153, 184, 198, 80));
                }

                BindInputSlot(
                    item.GetChild("primaryButton")?.asButton,
                    row,
                    row.PrimaryTarget,
                    row.PrimaryValue,
                    row.PrimaryEnabled,
                    row.PrimaryListening,
                    section);
                BindInputSlot(
                    item.GetChild("secondaryButton")?.asButton,
                    row,
                    row.SecondaryTarget,
                    row.SecondaryValue,
                    row.SecondaryEnabled,
                    row.SecondaryListening,
                    section);

                Fgui.GTextField cancelHint = item.GetChild("cancelHint")?.asTextField;
                if (cancelHint != null)
                {
                    cancelHint.text = listening ? "Esc 取消" : string.Empty;
                    cancelHint.visible = false;
                }
            };
            list.numItems = viewModel.InputRows.Count;
            ConfigureInputScrollIndicator(root, list);
        }

        private void BindInputSlot(
            Fgui.GButton button,
            CharacterTestFairyGuiSettingsInputRowViewModel row,
            MxInputBindingTarget target,
            string value,
            bool enabled,
            bool listening,
            bool section)
        {
            if (button == null)
                return;

            button.visible = !section;
            if (section)
            {
                button.onClick.Clear();
                return;
            }

            bool hasTarget = !string.IsNullOrWhiteSpace(target.ActionId) && target.BindingIndex >= 0;
            bool interactable = listening || (enabled && hasTarget);
            button.mode = Fgui.ButtonMode.Check;
            button.selected = listening;
            button.text = FormatSettingsBinding(value);
            button.enabled = interactable;
            // FairyGUI's grayscale material in this project does not participate in the
            // scroll-pane clip. Keeping disabled slots un-tinted prevents the material from
            // rendering cyan rectangles outside the list while touchability stays disabled.
            button.grayed = false;
            button.alpha = interactable ? 1f : 0.45f;
            StyleFrostedButton(button);
            button.tooltips = listening ? "按 Esc 取消重新绑定" : "点击修改按键";
            Fgui.GTextField title = button.GetChild("title")?.asTextField;
            if (title != null)
            {
                title.color = listening
                    ? ListeningTextColor
                    : interactable
                        ? BindingTextColor
                        : DisabledBindingTextColor;
            }
            button.onClick.Set(() =>
            {
                if (listening)
                {
                    _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        MxFairyGuiCommandIds.Cancel,
                        null));
                    return;
                }

                if (!enabled || !hasTarget)
                    return;

                _commandSink?.Enqueue(new MxUiCommand(
                    _viewId,
                    CharacterTestFairyGuiSettingsPanelCommandIds.StartInputRebind,
                    CreateInputPayload(row, target)));
            });
        }

        private static void ApplyPageVisibility(
            Fgui.GComponent root,
            CharacterTestFairyGuiSettingsPanelViewModel viewModel)
        {
            bool graphics = viewModel.SelectedPage == CharacterTestSettingsPage.Graphics;
            bool audio = viewModel.SelectedPage == CharacterTestSettingsPage.Audio;
            bool controls = viewModel.SelectedPage == CharacterTestSettingsPage.Controls;
            SetVisible(root, GraphicsPageChildren, graphics);
            SetVisible(root, AudioPageChildren, audio);
            SetVisible(root, ControlsPageChildren, controls);
            SetVisible(root, CharacterTestFairyGuiSettingsPanelIds.ResetInput, controls);
        }

        private static void SetVisible(Fgui.GComponent root, string[] names, bool visible)
        {
            for (int i = 0; i < names.Length; i++)
                SetVisible(root, names[i], visible);
        }

        private static void SetVisible(Fgui.GComponent root, string name, bool visible)
        {
            Fgui.GObject child = root?.GetChild(name);
            if (child != null)
                child.visible = visible;
        }

        private static void NormalizeDisabledButtonVisuals(Fgui.GComponent root)
        {
            if (root == null)
                return;

            for (int i = 0; i < root.numChildren; i++)
            {
                Fgui.GButton button = root.GetChildAt(i)?.asButton;
                if (button == null)
                    continue;

                bool interactable = button.enabled;
                button.grayed = false;
                button.alpha = interactable ? 1f : 0.45f;
            }
        }

        private void ApplyResponsiveLayout(Fgui.GComponent root)
        {
            if (root == null)
                return;

            float width = MxFairyGuiStageLayout.ResolveStageWidth(root, DesignWidth);
            float height = MxFairyGuiStageLayout.ResolveStageHeight(root, DesignHeight);
            root.SetSize(width, height);
            SetChildBounds(root, "scrim", 0f, 0f, width, height);

            ApplyScaledSettingsLayout(root, width, height);
        }

        private static void SetChildBounds(
            Fgui.GComponent root,
            string childName,
            float x,
            float y,
            float width,
            float height)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child == null)
                return;

            child.SetXY(x, y);
            child.SetSize(width, height);
        }
    }
}
