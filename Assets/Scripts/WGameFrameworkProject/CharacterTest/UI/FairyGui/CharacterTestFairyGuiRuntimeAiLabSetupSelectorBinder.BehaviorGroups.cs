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
        private void RenderSetupBehaviorGroupPicker(Fgui.GComponent root)
        {
            bool visible = (_setupBehaviorGroupPickerOpen || _setupCharacterPickerOpen)
                && _setupBehaviorGroupPickerViewModel != null
                && _setupBehaviorGroupPickerViewModel.WorkspaceMode
                    == CharacterTestRuntimeAiLabWorkspaceMode.Setup;
            string[] fixedObjects =
            {
                SetupBehaviorGroupPickerBackground,
                SetupBehaviorGroupPickerSearchBackground,
                SetupBehaviorGroupPickerSearchIcon,
                SetupBehaviorGroupPickerCount,
                SetupBehaviorGroupPickerSearchInput,
                SetupBehaviorGroupPickerEmpty,
                SetupBehaviorGroupPickerScrollTrack,
                SetupBehaviorGroupPickerScrollThumb,
                "btnSetupBehaviorGroupPickerFilter0",
                "btnSetupBehaviorGroupPickerFilter1",
                "btnSetupBehaviorGroupPickerFilter2",
                "btnSetupBehaviorGroupPickerFilter3"
            };
            for (int i = 0; i < fixedObjects.Length; i++)
            {
                Fgui.GObject child = GetSetupPickerChild(root, fixedObjects[i]);
                if (child == null)
                    continue;
                child.visible = visible;
                child.sortingOrder = 90 + i;
            }
            SetSetupPickerObjectVisible(
                root,
                SetupBehaviorGroupPickerActionBar,
                visible && !_setupCharacterPickerOpen);
            SetSetupPickerObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupPickerCreate,
                visible && !_setupCharacterPickerOpen);
            SetSetupPickerObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupPickerDelete,
                visible && !_setupCharacterPickerOpen);
            SetSetupBehaviorGroupEditorVisible(root, visible && !_setupCharacterPickerOpen);
            if (!visible)
            {
                for (int rowIndex = 0; rowIndex < SetupBehaviorGroupPickerRowCapacity; rowIndex++)
                {
                    SetSetupPickerObjectVisible(
                        root,
                        SetupBehaviorGroupPickerRow(rowIndex),
                        false);
                    SetSetupPickerObjectVisible(
                        root,
                        SetupBehaviorGroupPickerRowVisual(rowIndex),
                        false);
                }
                return;
            }

            if (_setupCharacterPickerOpen)
            {
                RenderSetupCharacterPickerRows(root);
                return;
            }

            CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel[] source =
                _setupBehaviorGroupPickerViewModel.SetupBehaviorGroupOptions
                ?? Array.Empty<CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel>();
            if (_recentSetupBehaviorGroupDraftIds.Count == 0)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] != null && source[i].Selected)
                    {
                        _recentSetupBehaviorGroupDraftIds.Add(SetupBehaviorGroupOptionKey(source[i]));
                        break;
                    }
                }
            }
            var filtered = new List<CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel>(
                source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel option = source[i];
                if (option == null || !MatchesSetupBehaviorGroupPickerFilter(option))
                    continue;
                if (!string.IsNullOrWhiteSpace(_setupBehaviorGroupPickerSearch)
                    && option.DisplayName.IndexOf(
                        _setupBehaviorGroupPickerSearch,
                        StringComparison.OrdinalIgnoreCase) < 0
                    && option.UnavailableReasonText.IndexOf(
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
            float listBottom = visibleRowCount > 0
                ? SetupBehaviorGroupPickerRowsY
                    + visibleRowCount * SetupBehaviorGroupPickerRowStride
                    - (SetupBehaviorGroupPickerRowStride - SetupBehaviorGroupPickerRowHeight)
                    + SetupBehaviorGroupPickerPanelBottomPadding
                : SetupBehaviorGroupPickerRowsY + 92f;
            float panelBottom = listBottom
                + SetupBehaviorGroupPickerActionBarGap
                + SetupBehaviorGroupGroupPickerActionBarHeight;
            panelBottom = Mathf.Max(panelBottom, SetupBehaviorGroupPickerMinimumEditorHeight);
            SetObjectGeometry(
                GetSetupPickerChild(root, SetupBehaviorGroupPickerBackground),
                0f,
                0f,
                SetupBehaviorGroupPickerExpandedWidth,
                panelBottom);
            ResizeSetupSelectorPopup(SetupBehaviorGroupPickerExpandedWidth, panelBottom);
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
                listBottom);
            RenderSetupBehaviorGroupPickerActionBar(root, panelBottom);
            RenderSetupBehaviorGroupEditor(root, panelBottom);

            for (int rowIndex = 0; rowIndex < SetupBehaviorGroupPickerRowCapacity; rowIndex++)
            {
                int optionIndex = _setupBehaviorGroupPickerOffset + rowIndex;
                CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel option =
                    optionIndex < filtered.Count ? filtered[optionIndex] : null;
                string childName = SetupBehaviorGroupPickerRow(rowIndex);
                Fgui.GButton row = GetSetupPickerChild(root, childName)?.asButton;
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
                    // The authored visual is rendered above the transparent button. Make the
                    // visible surface own pointer activation instead of relying on click-through
                    // behavior across the native popup stack.
                    visual.touchable = option != null;
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
                row.grayed = !option.Available;
                BindTooltip(
                    row,
                    BuildSetupBehaviorGroupOptionTooltip(option),
                    new MxFairyGuiTooltipOptions(
                        MxFairyGuiTooltipPlacement.Left,
                        allowPin: false));
                BindSetupBehaviorGroupPickerRowVisual(visual, option);
                string groupDraftId = option.GroupDraftId;
                int arenaDraftId = option.ArenaBehaviorGroupDraftId;
                bool available = option.Available;
                Fgui.EventCallback1 selectBehaviorGroup = _ =>
                    ActivateSetupBehaviorGroupOption(groupDraftId, arenaDraftId, available);
                row.onClick.Set(selectBehaviorGroup);
                visual?.onClick.Set(selectBehaviorGroup);
            }

            BindSetupBehaviorGroupPickerFilter(root, 0, "全部");
            BindSetupBehaviorGroupPickerFilter(root, 1, "最近");
            BindSetupBehaviorGroupPickerFilter(root, 2, "当前");
            BindSetupBehaviorGroupPickerFilter(root, 3, "可用");
        }

        private static string BuildSetupBehaviorGroupOptionTooltip(
            CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel option)
        {
            if (option == null)
                return string.Empty;

            var lines = new List<string>(3);
            if (!string.IsNullOrWhiteSpace(option.PublishStatusText))
                lines.Add(option.PublishStatusText);
            if (option.HasValidationProblems
                && !string.IsNullOrWhiteSpace(option.ValidationStatusText))
            {
                lines.Add(option.ValidationStatusText);
            }
            if (!option.Available
                && !string.IsNullOrWhiteSpace(option.UnavailableReasonText)
                && !lines.Contains(option.UnavailableReasonText))
            {
                lines.Add(option.UnavailableReasonText);
            }
            return string.Join("\n", lines);
        }

        private void ActivateSetupBehaviorGroupOption(
            string groupDraftId,
            int arenaBehaviorGroupDraftId,
            bool available)
        {
            if (!available)
                return;

            string optionKey = !string.IsNullOrWhiteSpace(groupDraftId)
                ? groupDraftId
                : "arena:" + arenaBehaviorGroupDraftId;
            _recentSetupBehaviorGroupDraftIds.Remove(optionKey);
            _recentSetupBehaviorGroupDraftIds.Insert(0, optionKey);
            if (_recentSetupBehaviorGroupDraftIds.Count > 8)
                _recentSetupBehaviorGroupDraftIds.RemoveAt(8);

            DiscardSetupBehaviorGroupEditorBuffer();
            _commandSink?.Enqueue(new MxUiCommand(
                _viewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                    .SelectSetupBehaviorGroupPrefix
                    + arenaBehaviorGroupDraftId
                    + "|"
                    + groupDraftId,
                null));
        }

        private static string SetupBehaviorGroupOptionKey(
            CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel option) =>
            !string.IsNullOrWhiteSpace(option?.GroupDraftId)
                ? option.GroupDraftId
                : "arena:" + (option?.ArenaBehaviorGroupDraftId ?? 0);

        private void RenderSetupBehaviorGroupPickerActionBar(
            Fgui.GComponent root,
            float panelBottom)
        {
            float actionBarY = panelBottom
                - SetupBehaviorGroupGroupPickerActionBarHeight;
            Fgui.GObject actionBar = GetSetupPickerChild(
                root,
                SetupBehaviorGroupPickerActionBar);
            SetObjectGeometry(
                actionBar,
                0f,
                actionBarY,
                SetupBehaviorGroupPickerPanelWidth,
                SetupBehaviorGroupGroupPickerActionBarHeight);
            if (actionBar != null)
                actionBar.sortingOrder = 150;

            Fgui.GButton createButton = GetSetupPickerChild(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupPickerCreate)?.asButton;
            SetObjectGeometry(createButton, 16f, actionBarY + 12f, 250f, 40f);
            if (createButton != null)
                createButton.sortingOrder = 151;

            Fgui.GButton deleteButton = GetSetupPickerChild(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupPickerDelete)?.asButton;
            SetObjectGeometry(deleteButton, 304f, actionBarY + 12f, 250f, 40f);
            if (deleteButton != null)
                deleteButton.sortingOrder = 151;
        }

        private void BindSetupBehaviorGroupEditor(Fgui.GComponent root)
        {
            EnsureSetupBehaviorGroupEditorBuffer(_setupBehaviorGroupPickerViewModel);
            BindSetupBehaviorGroupEditorInput(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorName,
                64,
                singleLine: true,
                () => _setupBehaviorGroupEditorDisplayName,
                value => _setupBehaviorGroupEditorDisplayName = value);
            BindSetupBehaviorGroupEditorInput(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorDescription,
                512,
                singleLine: false,
                () => _setupBehaviorGroupEditorDescription,
                value => _setupBehaviorGroupEditorDescription = value);
            BindSetupBehaviorGroupEditorInput(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTags,
                256,
                singleLine: true,
                () => _setupBehaviorGroupEditorTagsText,
                value => _setupBehaviorGroupEditorTagsText = value);
            _setupBehaviorGroupEditorForceInputRefresh = false;

            Fgui.GButton saveButton = GetSetupPickerChild(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorSave)?.asButton;
            if (saveButton == null)
                return;
            saveButton.onClick.Set(() =>
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                    _setupBehaviorGroupPickerViewModel;
                if (viewModel?.SetupBehaviorGroupEditorIsLocalDraft != true
                    && (!_setupBehaviorGroupEditorDirty
                        || !HasSetupBehaviorGroupEditorChanges(viewModel)))
                    return;
                if (!TryValidateSetupBehaviorGroupEditor(viewModel, out string diagnostic))
                {
                    _setupBehaviorGroupEditorStatus = diagnostic;
                    RenderSetupBehaviorGroupEditor(root, _setupSelectorPopup?.height ?? 0f);
                    return;
                }
                _commandSink?.Enqueue(new MxUiCommand(
                    _viewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .SaveSetupBehaviorGroupBasicInfo,
                    new CharacterTestRuntimeAiLabSetupBehaviorGroupBasicInfoPayload
                    {
                        GroupDraftId = viewModel.SetupBehaviorGroupEditorGroupDraftId,
                        StableGroupId = viewModel.SetupBehaviorGroupEditorStableId,
                        SourceRevision = viewModel.SetupBehaviorGroupEditorSourceRevision,
                        SourceContentHash = viewModel.SetupBehaviorGroupEditorSourceContentHash,
                        DisplayName = _setupBehaviorGroupEditorDisplayName,
                        Description = _setupBehaviorGroupEditorDescription,
                        TagsText = _setupBehaviorGroupEditorTagsText,
                        IsLocalDraft = viewModel.SetupBehaviorGroupEditorIsLocalDraft
                    }));
                EnsureSetupBehaviorGroupEditorBuffer(_setupBehaviorGroupPickerViewModel);
                _setupBehaviorGroupEditorStatus = _setupBehaviorGroupPickerViewModel?.StatusText
                    ?? string.Empty;
                RenderSetupBehaviorGroupEditor(root, _setupSelectorPopup?.height ?? 0f);
            });
        }

        private void BindSetupBehaviorGroupEditorInput(
            Fgui.GComponent root,
            string childName,
            int maxLength,
            bool singleLine,
            Func<string> getValue,
            Action<string> setValue)
        {
            Fgui.GTextInput input = GetSetupPickerChild(root, childName) as Fgui.GTextInput;
            if (input == null)
                return;
            input.maxLength = maxLength;
            input.singleLine = singleLine;
            if (_setupBehaviorGroupEditorForceInputRefresh
                || !Application.isPlaying
                || !input.focused)
            {
                _setupBehaviorGroupEditorBindingInput = true;
                try
                {
                    input.text = getValue() ?? string.Empty;
                }
                finally
                {
                    _setupBehaviorGroupEditorBindingInput = false;
                }
            }
            input.onChanged.Set(() =>
            {
                if (_setupBehaviorGroupEditorBindingInput)
                    return;
                setValue(input.text ?? string.Empty);
                _setupBehaviorGroupEditorDirty = HasSetupBehaviorGroupEditorChanges(
                    _setupBehaviorGroupPickerViewModel);
                _setupBehaviorGroupEditorStatus = _setupBehaviorGroupEditorDirty
                    ? "未保存修改"
                    : string.Empty;
                RenderSetupBehaviorGroupEditor(root, _setupSelectorPopup?.height ?? 0f);
            });
        }

        private void RenderSetupBehaviorGroupEditor(Fgui.GComponent root, float panelHeight)
        {
            if (_setupCharacterPickerOpen || !_setupBehaviorGroupPickerOpen || panelHeight <= 0f)
                return;
            EnsureSetupBehaviorGroupEditorBuffer(_setupBehaviorGroupPickerViewModel);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                _setupBehaviorGroupPickerViewModel;
            SetObjectGeometry(
                GetSetupPickerChild(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorPanel),
                SetupBehaviorGroupEditorX,
                0f,
                SetupBehaviorGroupEditorWidth,
                panelHeight);
            SetObjectGeometry(
                GetSetupPickerChild(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTitle),
                594f,
                22f,
                342f,
                30f);
            SetObjectGeometry(
                GetSetupPickerChild(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorIdentity),
                594f,
                60f,
                342f,
                54f);
            SetObjectGeometry(
                GetSetupPickerChild(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorNameLabel),
                594f,
                128f,
                342f,
                22f);
            SetObjectGeometry(
                GetSetupPickerChild(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorName),
                594f,
                154f,
                342f,
                44f);
            SetObjectGeometry(
                GetSetupPickerChild(root, SetupBehaviorGroupEditorNameBackground),
                594f,
                154f,
                342f,
                44f);
            SetObjectGeometry(
                GetSetupPickerChild(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorDescriptionLabel),
                594f,
                212f,
                342f,
                22f);
            SetObjectGeometry(
                GetSetupPickerChild(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorDescription),
                594f,
                238f,
                342f,
                112f);
            SetObjectGeometry(
                GetSetupPickerChild(root, SetupBehaviorGroupEditorDescriptionBackground),
                594f,
                238f,
                342f,
                112f);
            SetObjectGeometry(
                GetSetupPickerChild(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTagsLabel),
                594f,
                366f,
                342f,
                22f);
            SetObjectGeometry(
                GetSetupPickerChild(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTags),
                594f,
                392f,
                342f,
                44f);
            SetObjectGeometry(
                GetSetupPickerChild(root, SetupBehaviorGroupEditorTagsBackground),
                594f,
                392f,
                342f,
                44f);
            SetObjectGeometry(
                GetSetupPickerChild(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorStatus),
                594f,
                448f,
                342f,
                Mathf.Max(26f, panelHeight - 518f));
            Fgui.GButton saveButton = GetSetupPickerChild(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorSave)?.asButton;
            SetObjectGeometry(saveButton, 594f, panelHeight - 58f, 342f, 40f);

            SetSetupPickerText(root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTitle,
                "行为组基本信息");
            SetSetupPickerText(root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorIdentity,
                viewModel?.SetupBehaviorGroupEditorIdentityText);
            SetSetupPickerText(root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorNameLabel,
                "名称");
            SetSetupPickerText(root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorDescriptionLabel,
                "描述");
            SetSetupPickerText(root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTagsLabel,
                "标签  ·  使用逗号分隔");
            string status = _setupBehaviorGroupEditorStatus;
            if (string.IsNullOrWhiteSpace(status) && viewModel?.CanEditSetupBehaviorGroupBasicInfo != true)
                status = viewModel?.SetupBehaviorGroupEditorDisabledReasonText;
            SetSetupPickerText(root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorStatus,
                status);

            bool editable = viewModel?.CanEditSetupBehaviorGroupBasicInfo == true;
            SetSetupBehaviorGroupEditorInputEnabled(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorName,
                editable);
            SetSetupBehaviorGroupEditorInputEnabled(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorDescription,
                editable);
            SetSetupBehaviorGroupEditorInputEnabled(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTags,
                editable);
            if (saveButton != null)
            {
                bool canSave = TryValidateSetupBehaviorGroupEditor(viewModel, out _)
                    && (viewModel?.SetupBehaviorGroupEditorIsLocalDraft == true
                        || (_setupBehaviorGroupEditorDirty
                            && HasSetupBehaviorGroupEditorChanges(viewModel)));
                saveButton.text = viewModel?.SetupBehaviorGroupEditorSaveLabel ?? "保存修改";
                saveButton.enabled = canSave;
                saveButton.grayed = !canSave;
                saveButton.touchable = canSave;
                saveButton.sortingOrder = 180;
            }
            for (int i = 0; i < SetupBehaviorGroupEditorChildNames.Length; i++)
            {
                Fgui.GObject child = GetSetupPickerChild(root, SetupBehaviorGroupEditorChildNames[i]);
                if (child != null)
                    child.sortingOrder = 170 + i;
            }
        }

        private void SetSetupBehaviorGroupEditorInputEnabled(
            Fgui.GComponent root,
            string childName,
            bool enabled)
        {
            Fgui.GTextInput input = GetSetupPickerChild(root, childName) as Fgui.GTextInput;
            if (input == null)
                return;
            input.editable = enabled;
            input.touchable = enabled;
            input.grayed = !enabled;
        }

        private void SetSetupBehaviorGroupEditorVisible(Fgui.GComponent root, bool visible)
        {
            for (int i = 0; i < SetupBehaviorGroupEditorChildNames.Length; i++)
                SetSetupPickerObjectVisible(root, SetupBehaviorGroupEditorChildNames[i], visible);
        }

        private void EnsureSetupBehaviorGroupEditorBuffer(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            string sourceKey = BuildSetupBehaviorGroupEditorSourceKey(viewModel);
            if (string.Equals(sourceKey, _setupBehaviorGroupEditorSourceKey, StringComparison.Ordinal))
                return;
            ResetSetupBehaviorGroupEditorBuffer(viewModel);
        }

        private void ResetSetupBehaviorGroupEditorBuffer(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            _setupBehaviorGroupEditorSourceKey = BuildSetupBehaviorGroupEditorSourceKey(viewModel);
            _setupBehaviorGroupEditorDisplayName = viewModel?.SetupBehaviorGroupEditorDisplayName
                ?? string.Empty;
            _setupBehaviorGroupEditorDescription = viewModel?.SetupBehaviorGroupEditorDescription
                ?? string.Empty;
            _setupBehaviorGroupEditorTagsText = viewModel?.SetupBehaviorGroupEditorTagsText
                ?? string.Empty;
            _setupBehaviorGroupEditorDirty = false;
            _setupBehaviorGroupEditorForceInputRefresh = true;
            _setupBehaviorGroupEditorStatus = viewModel?.StatusText != null
                && viewModel.StatusText.StartsWith("已保存行为组基本信息", StringComparison.Ordinal)
                    ? viewModel.StatusText
                    : string.Empty;
        }

        private void DiscardSetupBehaviorGroupEditorBuffer()
        {
            _setupBehaviorGroupEditorSourceKey = string.Empty;
            _setupBehaviorGroupEditorDisplayName = string.Empty;
            _setupBehaviorGroupEditorDescription = string.Empty;
            _setupBehaviorGroupEditorTagsText = string.Empty;
            _setupBehaviorGroupEditorStatus = string.Empty;
            _setupBehaviorGroupEditorBindingInput = false;
            _setupBehaviorGroupEditorDirty = false;
            _setupBehaviorGroupEditorForceInputRefresh = true;
        }

        private static string BuildSetupBehaviorGroupEditorSourceKey(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            if (viewModel == null)
                return string.Empty;
            return viewModel.SetupBehaviorGroupEditorGroupDraftId
                + "|" + viewModel.SetupBehaviorGroupEditorSourceRevision
                + "|" + viewModel.SetupBehaviorGroupEditorSourceContentHash;
        }

        private bool TryValidateSetupBehaviorGroupEditor(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            out string diagnostic)
        {
            if (viewModel?.CanEditSetupBehaviorGroupBasicInfo != true)
            {
                diagnostic = viewModel?.SetupBehaviorGroupEditorDisabledReasonText
                    ?? "当前行为组不可编辑。";
                return false;
            }
            string name = _setupBehaviorGroupEditorDisplayName?.Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                diagnostic = "名称不能为空。";
                return false;
            }
            if (name.Length > 64)
            {
                diagnostic = "名称不能超过 64 个字符。";
                return false;
            }
            if ((_setupBehaviorGroupEditorDescription?.Trim().Length ?? 0) > 512)
            {
                diagnostic = "描述不能超过 512 个字符。";
                return false;
            }
            if ((_setupBehaviorGroupEditorTagsText?.Length ?? 0) > 256)
            {
                diagnostic = "标签不能超过 256 个字符。";
                return false;
            }
            diagnostic = string.Empty;
            return true;
        }

        private bool HasSetupBehaviorGroupEditorChanges(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            if (viewModel == null)
                return false;
            return !string.Equals(
                    _setupBehaviorGroupEditorDisplayName?.Trim(),
                    viewModel.SetupBehaviorGroupEditorDisplayName?.Trim(),
                    StringComparison.Ordinal)
                || !string.Equals(
                    _setupBehaviorGroupEditorDescription?.Trim(),
                    viewModel.SetupBehaviorGroupEditorDescription?.Trim(),
                    StringComparison.Ordinal)
                || !string.Equals(
                    NormalizeSetupBehaviorGroupTags(_setupBehaviorGroupEditorTagsText),
                    NormalizeSetupBehaviorGroupTags(viewModel.SetupBehaviorGroupEditorTagsText),
                    StringComparison.Ordinal);
        }

        private static string NormalizeSetupBehaviorGroupTags(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string[] parts = value.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
            var tags = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string tag = parts[i].Trim();
                if (tag.Length > 0 && !tags.Contains(tag))
                    tags.Add(tag);
            }
            return string.Join("\n", tags);
        }

    }
}
