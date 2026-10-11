using System;
using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabSetupSelectorBinder
    {
        private void RenderSetupCharacterPickerRows(Fgui.GComponent root)
        {
            CharacterTestRuntimeAiLabSetupCharacterOptionViewModel[] source =
                _setupBehaviorGroupPickerViewModel.SetupCharacterOptions
                ?? Array.Empty<CharacterTestRuntimeAiLabSetupCharacterOptionViewModel>();
            if (_recentSetupCharacterIds.Count == 0)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] != null && source[i].Selected)
                    {
                        _recentSetupCharacterIds.Add(source[i].CharacterId);
                        break;
                    }
                }
            }

            var filtered = new List<CharacterTestRuntimeAiLabSetupCharacterOptionViewModel>(
                source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                CharacterTestRuntimeAiLabSetupCharacterOptionViewModel option = source[i];
                if (option == null || !MatchesSetupCharacterPickerFilter(option))
                    continue;
                if (!string.IsNullOrWhiteSpace(_setupBehaviorGroupPickerSearch)
                    && option.DisplayName.IndexOf(
                        _setupBehaviorGroupPickerSearch,
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                filtered.Add(option);
            }

            int maxOffset = Mathf.Max(0, filtered.Count - SetupBehaviorGroupPickerRowCapacity);
            _setupBehaviorGroupPickerMaxOffset = maxOffset;
            _setupBehaviorGroupPickerOffset = Mathf.Clamp(
                _setupBehaviorGroupPickerOffset,
                0,
                maxOffset);
            SetSetupPickerText(
                root,
                SetupBehaviorGroupPickerCount,
                filtered.Count.ToString());

            int visibleRowCount = Mathf.Min(filtered.Count, SetupBehaviorGroupPickerRowCapacity);
            float panelBottom = visibleRowCount > 0
                ? SetupBehaviorGroupPickerRowsY
                    + visibleRowCount * SetupBehaviorGroupPickerRowStride
                    - (SetupBehaviorGroupPickerRowStride - SetupBehaviorGroupPickerRowHeight)
                    + SetupBehaviorGroupPickerPanelBottomPadding
                : SetupBehaviorGroupPickerRowsY + 92f;
            SetObjectGeometry(
                GetSetupPickerChild(root, SetupBehaviorGroupPickerBackground),
                0f,
                0f,
                SetupBehaviorGroupPickerPanelWidth,
                panelBottom);
            ResizeSetupSelectorPopup(SetupBehaviorGroupPickerPanelWidth, panelBottom);
            Fgui.GObject empty = GetSetupPickerChild(root, SetupBehaviorGroupPickerEmpty);
            if (empty != null)
            {
                empty.visible = filtered.Count == 0;
                empty.sortingOrder = 110;
                empty.SetPosition(empty.x, SetupBehaviorGroupPickerRowsY + 24f, empty.z);
            }

            ConfigureSetupBehaviorGroupPickerScrollbar(
                root,
                filtered.Count,
                maxOffset,
                panelBottom);

            for (int rowIndex = 0; rowIndex < SetupBehaviorGroupPickerRowCapacity; rowIndex++)
            {
                int optionIndex = _setupBehaviorGroupPickerOffset + rowIndex;
                CharacterTestRuntimeAiLabSetupCharacterOptionViewModel option =
                    optionIndex < filtered.Count ? filtered[optionIndex] : null;
                Fgui.GButton row = GetSetupPickerChild(
                    root,
                    SetupBehaviorGroupPickerRow(rowIndex))?.asButton;
                Fgui.GComponent visual = GetSetupPickerChild(
                    root,
                    SetupBehaviorGroupPickerRowVisual(rowIndex))?.asCom;
                if (row == null)
                    continue;
                row.visible = option != null;
                row.sortingOrder = 110 + rowIndex * 2;
                if (visual != null)
                {
                    visual.visible = option != null;
                    visual.sortingOrder = row.sortingOrder + 1;
                    visual.touchable = false;
                    visual.SetPosition(
                        visual.x,
                        SetupBehaviorGroupPickerRowsY
                            + rowIndex * SetupBehaviorGroupPickerRowStride,
                        visual.z);
                }
                row.SetPosition(
                    row.x,
                    SetupBehaviorGroupPickerRowsY
                        + rowIndex * SetupBehaviorGroupPickerRowStride,
                    row.z);
                if (option == null)
                    continue;

                row.text = string.Empty;
                row.mode = Fgui.ButtonMode.Check;
                row.selected = option.Selected;
                row.grayed = false;
                BindTooltip(row, string.Empty);
                BindSetupCharacterPickerRowVisual(visual, option);
                int characterId = option.CharacterId;
                row.onClick.Set(() =>
                {
                    _recentSetupCharacterIds.Remove(characterId);
                    _recentSetupCharacterIds.Insert(0, characterId);
                    if (_recentSetupCharacterIds.Count > 8)
                        _recentSetupCharacterIds.RemoveAt(8);
                    _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                            .SelectSetupCharacterPrefix
                            + characterId,
                        null));
                });
            }

            BindSetupBehaviorGroupPickerFilter(root, 0, "全部");
            BindSetupBehaviorGroupPickerFilter(root, 1, "最近");
            BindSetupBehaviorGroupPickerFilter(root, 2, "当前");
            BindSetupBehaviorGroupPickerFilter(root, 3, "可用");
        }

        private void ConfigureSetupBehaviorGroupPickerScrollbar(
            Fgui.GComponent root,
            int filteredCount,
            int maxOffset,
            float panelBottom)
        {
            Fgui.GObject track = GetSetupPickerChild(
                root,
                SetupBehaviorGroupPickerScrollTrack);
            Fgui.GObject thumb = GetSetupPickerChild(
                root,
                SetupBehaviorGroupPickerScrollThumb);
            bool visible = filteredCount > SetupBehaviorGroupPickerRowCapacity
                && maxOffset > 0
                && track != null
                && thumb != null;
            if (track != null)
                track.visible = visible;
            if (thumb != null)
                thumb.visible = visible;
            if (!visible)
                return;

            float trackHeight = Mathf.Max(
                32f,
                panelBottom
                    - SetupBehaviorGroupPickerScrollTrackY
                    - SetupBehaviorGroupPickerPanelBottomPadding);
            track.SetPosition(track.x, SetupBehaviorGroupPickerScrollTrackY, track.z);
            track.SetSize(track.width, trackHeight);
            float thumbHeight = Mathf.Max(
                28f,
                trackHeight * SetupBehaviorGroupPickerRowCapacity / filteredCount);
            thumb.SetSize(thumb.width, thumbHeight);
            float travel = Mathf.Max(1f, trackHeight - thumbHeight);
            float normalized = _setupBehaviorGroupPickerOffset / (float)maxOffset;
            thumb.SetPosition(
                track.x,
                track.y + Mathf.Round(travel * normalized),
                thumb.z);
            track.sortingOrder = 140;
            thumb.sortingOrder = 141;
        }

        private static void BindSetupBehaviorGroupPickerRowVisual(
            Fgui.GComponent visual,
            CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel option)
        {
            if (visual == null || option == null)
                return;

            Fgui.GGraph accent = visual.GetChild("accent")?.asGraph;
            if (accent != null)
            {
                accent.visible = option.Selected;
                accent.color = new Color32(75, 194, 164, 255);
            }
            Fgui.GGraph badgeBackground = visual.GetChild("badgeBackground")?.asGraph;
            if (badgeBackground != null)
            {
                badgeBackground.color = option.Selected
                    ? new Color32(37, 76, 74, 255)
                    : new Color32(31, 46, 56, 255);
            }
            Fgui.GTextField monogram = visual.GetChild("monogram")?.asTextField;
            if (monogram != null)
            {
                monogram.text = option.MonogramText ?? string.Empty;
                monogram.color = option.Available
                    ? new Color32(221, 231, 234, 255)
                    : new Color32(112, 124, 131, 255);
            }
            Fgui.GTextField name = visual.GetChild("name")?.asTextField;
            if (name != null)
            {
                name.text = option.DisplayName ?? string.Empty;
                name.color = option.Available
                    ? new Color32(239, 244, 245, 255)
                    : new Color32(112, 124, 131, 255);
            }
            Fgui.GTextField publishStatus = visual.GetChild("publishStatus")?.asTextField;
            if (publishStatus != null)
            {
                publishStatus.visible = true;
                publishStatus.text = option.PublishStatusText ?? string.Empty;
                switch (option.PublishState)
                {
                    case CharacterTestRuntimeAiLabPublishUxState.GameAvailable:
                        publishStatus.color = new Color32(105, 215, 140, 255);
                        break;
                    case CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges:
                        publishStatus.color = new Color32(255, 176, 74, 255);
                        break;
                    case CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly:
                        publishStatus.color = new Color32(120, 184, 255, 255);
                        break;
                    default:
                        publishStatus.color = new Color32(136, 151, 158, 255);
                        break;
                }
            }
            Fgui.GTextField count = visual.GetChild("count")?.asTextField;
            if (count != null)
            {
                count.visible = true;
                count.text = option.CardCount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                count.color = option.Available
                    ? new Color32(201, 213, 220, 255)
                    : new Color32(104, 116, 123, 255);
            }
            Fgui.GObject cardIcon = visual.GetChild("cardIcon");
            if (cardIcon != null)
            {
                cardIcon.visible = true;
                cardIcon.alpha = option.Available ? 0.9f : 0.38f;
            }
            Fgui.GGraph statusDot = visual.GetChild("statusDot")?.asGraph;
            if (statusDot != null)
            {
                statusDot.visible = !option.HasValidationProblems
                    && option.PublishState != CharacterTestRuntimeAiLabPublishUxState.Unavailable;
                switch (option.PublishState)
                {
                    case CharacterTestRuntimeAiLabPublishUxState.GameAvailable:
                        statusDot.color = new Color32(83, 213, 176, 255);
                        break;
                    case CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges:
                        statusDot.color = new Color32(255, 176, 74, 255);
                        break;
                    default:
                        statusDot.color = new Color32(120, 184, 255, 255);
                        break;
                }
            }
            Fgui.GObject blocked = visual.GetChild("blockedIcon");
            if (blocked != null)
            {
                blocked.visible = option.HasValidationProblems || !option.Available;
                blocked.alpha = 0.9f;
            }
        }

        private static void BindSetupCharacterPickerRowVisual(
            Fgui.GComponent visual,
            CharacterTestRuntimeAiLabSetupCharacterOptionViewModel option)
        {
            if (visual == null || option == null)
                return;

            Fgui.GGraph accent = visual.GetChild("accent")?.asGraph;
            if (accent != null)
            {
                accent.visible = option.Selected;
                accent.color = new Color32(75, 194, 164, 255);
            }
            Fgui.GGraph badgeBackground = visual.GetChild("badgeBackground")?.asGraph;
            if (badgeBackground != null)
            {
                badgeBackground.color = option.Selected
                    ? new Color32(37, 76, 74, 255)
                    : new Color32(31, 46, 56, 255);
            }
            Fgui.GTextField monogram = visual.GetChild("monogram")?.asTextField;
            if (monogram != null)
            {
                monogram.text = option.MonogramText ?? string.Empty;
                monogram.color = new Color32(221, 231, 234, 255);
            }
            Fgui.GTextField name = visual.GetChild("name")?.asTextField;
            if (name != null)
            {
                name.text = option.DisplayName ?? string.Empty;
                name.color = new Color32(239, 244, 245, 255);
            }
            Fgui.GTextField count = visual.GetChild("count")?.asTextField;
            if (count != null)
            {
                count.text = string.Empty;
                count.visible = false;
            }
            Fgui.GObject cardIcon = visual.GetChild("cardIcon");
            if (cardIcon != null)
                cardIcon.visible = false;
            Fgui.GGraph statusDot = visual.GetChild("statusDot")?.asGraph;
            if (statusDot != null)
            {
                statusDot.visible = true;
                statusDot.color = option.Selected
                    ? new Color32(83, 213, 176, 255)
                    : new Color32(72, 171, 149, 230);
            }
            Fgui.GObject blocked = visual.GetChild("blockedIcon");
            if (blocked != null)
                blocked.visible = false;
        }

        private bool MatchesSetupCharacterPickerFilter(
            CharacterTestRuntimeAiLabSetupCharacterOptionViewModel option)
        {
            switch (_setupBehaviorGroupPickerFilter)
            {
                case 1:
                    return _recentSetupCharacterIds.Contains(option.CharacterId);
                case 2:
                    return option.Selected;
                default:
                    return true;
            }
        }

        private bool MatchesSetupBehaviorGroupPickerFilter(
            CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel option)
        {
            switch (_setupBehaviorGroupPickerFilter)
            {
                case 1:
                    return _recentSetupBehaviorGroupDraftIds.Contains(SetupBehaviorGroupOptionKey(option));
                case 2:
                    return option.Selected;
                case 3:
                    return option.Available;
                default:
                    return true;
            }
        }

        internal static string SetupBehaviorGroupPickerRow(int rowIndex)
        {
            return "btnSetupBehaviorGroupPickerRow" + rowIndex;
        }

        internal static string SetupBehaviorGroupPickerRowVisual(int rowIndex)
        {
            return "setupBehaviorGroupPickerRowVisual" + rowIndex;
        }


        internal Fgui.GComponent SharedContent => _setupSelectorPopup;
        internal Fgui.GComponent CharacterPopupHost => _setupCharacterPopupHost;

        internal void Bind(Fgui.GComponent root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            BindSetupBehaviorGroupPicker(root, viewModel);
        }

        internal void ResetForRoot()
        {
            ClosePopup(_setupCharacterPopupHost);
            ClosePopup(_setupBehaviorGroupPopupHost);
            _setupSelectorPopup?.RemoveFromParent();
            _setupCharacterPopupHost?.Dispose();
            _setupBehaviorGroupPopupHost?.Dispose();
            _setupSelectorPopup?.Dispose();
            _setupCharacterPopupHost = null;
            _setupBehaviorGroupPopupHost = null;
            _setupSelectorPopup = null;
            _setupBehaviorGroupPickerOpen = false;
            _setupCharacterPickerOpen = false;
            _setupBehaviorGroupPickerSearch = string.Empty;
            _setupBehaviorGroupPickerFilter = 0;
            _setupBehaviorGroupPickerOffset = 0;
            _setupBehaviorGroupPickerMaxOffset = 0;
            _setupBehaviorGroupPickerViewModel = null;
        }

        private void ClosePopup(Fgui.GComponent popup)
        {
            if (popup != null && _popupService?.IsOpen(popup) == true)
                _popupService.Hide(popup, MxFairyGuiPopupCloseReason.OwnerClosed);
        }

        private void BindButton(Fgui.GComponent root, string childName, string label, bool enabled, string commandId, bool selected = false, bool checkMode = false)
        {
            Fgui.GButton button = root.GetChild(childName)?.asButton;
            if (button == null) return;
            if (checkMode) button.mode = Fgui.ButtonMode.Check;
            button.text = label ?? string.Empty; button.enabled = enabled; button.grayed = !enabled;
            button.selected = selected; button.visible = true; CenterButtonContent(button, 10f);
            button.onClick.Set(() => { if (enabled) _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, null)); });
        }

        private void BindTooltip(Fgui.GObject target, string text, MxFairyGuiTooltipOptions options = default)
        {
            if (_tooltipsSuppressed()) { _rootSession.UnbindTooltip(target); return; }
            _rootSession.BindTooltip(target, text, options);
        }

        private static void CenterButtonContent(Fgui.GButton button, float gap) => CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.CenterButtonContent(button, gap);
        private static void ApplyGeneratedRuntimeAiIcon(Fgui.GButton button, string resourceName) => CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.ApplyGeneratedRuntimeAiIcon(button, resourceName);
        private static void BindText(Fgui.GComponent root, string childName, string value) => CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.BindText(root, childName, value);
        private static void SetObjectVisible(Fgui.GComponent root, string childName, bool visible) { var child = root?.GetChild(childName); if (child != null) child.visible = visible; }

    }
}
