using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private void BindUnifiedOperationParameter(BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            string label = string.Empty;
            string value = string.Empty;
            bool characterAction = false;
            bool speedMultiplier = false;
            switch (model.OperationKind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget:
                    // Approach events only produce movement. Authors express stop/exit behavior
                    // with explicit conditions so the recipe has a single control authority.
                    speedMultiplier = true;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction:
                    // CharacterAction uses the dedicated action-event inspector path.
                    break;
            }
            bool visible = label.Length > 0;
            SetVisible("operationParameterLabel", visible);
            SetVisible("inputOperationParameter", visible && !characterAction);
            SetVisible("btnCharacterActionParameter", visible && characterAction);
            SetVisible("btnViewCharacterActionTimeline", false);
            SetVisible("operationSecondaryParameterLabel", speedMultiplier);
            SetVisible("inputOperationSecondaryParameter", speedMultiplier);
            SetVisible("btnMoveSpeedMinus", speedMultiplier);
            SetVisible("btnMoveSpeedPlus", speedMultiplier);
            SetVisible("moveSpeedHint", speedMultiplier);
            SetText("operationParameterLabel", label);
            SetText("operationSecondaryParameterLabel", "速度倍率(×)");
            SetText("moveSpeedHint", "范围 0.1–1.0");
            SetButtonTitle("btnMoveSpeedMinus", "-");
            SetButtonTitle("btnMoveSpeedPlus", "+");
            if (characterAction)
            {
                SetButtonTitle("btnCharacterActionParameter", value);
                SetEnabled("btnCharacterActionParameter", model.CanEdit);
            }
            else
            {
                SetInput("inputOperationParameter", value);
            }
            if (speedMultiplier)
            {
                SetInput("inputOperationSecondaryParameter",
                    model.OperationMoveSpeedMultiplier.ToString("0.##", CultureInfo.InvariantCulture));
            }
        }

        private void ShowCharacterActionPicker(
            Fgui.GObject anchor,
            CharacterActionId selectedActionId,
            Action<CharacterActionId> select)
        {
            if (anchor == null || _model?.CanEdit != true)
                return;
            EnsureCharacterActionPicker();
            if (_characterActionPicker == null)
                return;
            bool open = _popupService?.IsOpen(_characterActionPicker) == true
                || _characterActionPicker.parent != null;
            if (open)
            {
                bool sameAnchor = ReferenceEquals(_characterActionPickerAnchor, anchor);
                CloseCharacterActionPicker();
                if (sameAnchor)
                    return;
            }
            _characterActionPickerAnchor = anchor;
            _characterActionSelection = select;
            _characterActionPicker.data = selectedActionId;
            Fgui.GTextInput query = _characterActionPicker.GetChild("query")?.asTextInput;
            if (query != null)
                query.text = string.Empty;
            RebuildCharacterActionPicker(string.Empty);
            if (_popupService != null)
            {
                _characterActionPickerUsesPopupService = _popupService.Show(
                    _viewId,
                    _characterActionPicker,
                    anchor,
                    new MxFairyGuiPopupOptions(
                        MxFairyGuiPopupPlacement.Auto,
                        safeMargin: 12f,
                        anchorGap: 4f));
            }
            if (!_characterActionPickerUsesPopupService)
            {
                Fgui.GRoot.inst.TogglePopup(
                    _characterActionPicker,
                    anchor,
                    Fgui.PopupDirection.Auto,
                    false);
            }
        }

        internal void ShowCharacterActionPickerForCreation(
            Fgui.GObject anchor,
            Action<string> select)
        {
            ShowCharacterActionPicker(
                anchor,
                CharacterActionId.None,
                actionId => select?.Invoke(actionId.Value.ToString(CultureInfo.InvariantCulture)));
        }

        private void EnsureCharacterActionPicker()
        {
            if (_characterActionPicker != null)
                return;
            _characterActionPicker = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "RuntimeAiLabCharacterActionPicker")?.asCom;
            if (_characterActionPicker == null)
                return;
            Fgui.GTextInput query = _characterActionPicker.GetChild("query")?.asTextInput;
            if (query != null)
            {
                query.promptText = "搜索动作名称、ID、分类或标签";
                query.onTouchBegin.Set(context => context.StopPropagation());
                query.onChanged.Set(() => RebuildCharacterActionPicker(query.text));
                query.onKeyDown.Set(context =>
                {
                    if (context.inputEvent?.keyCode == KeyCode.Escape)
                    {
                        context.StopPropagation();
                        CloseCharacterActionPicker();
                    }
                });
            }
            Fgui.GButton close = _characterActionPicker.GetChild("btnClose")?.asButton;
            if (close != null)
                close.onClick.Set(CloseCharacterActionPicker);
            Fgui.GList list = _characterActionPicker.GetChild("actionList")?.asList;
            if (list == null)
                return;
            list.layout = Fgui.ListLayoutType.SingleColumn;
            list.selectionMode = Fgui.ListSelectionMode.Single;
            list.lineGap = 4;
            list.itemRenderer = BindCharacterActionPickerRow;
        }

        private void RebuildCharacterActionPicker(string query)
        {
            if (_characterActionPicker == null)
                return;
            BehaviorRecipeCharacterActionOptionViewModel[] options =
                _model?.CharacterActionOptions ?? Array.Empty<BehaviorRecipeCharacterActionOptionViewModel>();
            if (_filteredCharacterActionIndices.Length < options.Length)
                _filteredCharacterActionIndices = new int[options.Length];
            string normalized = (query ?? string.Empty).Trim().ToLowerInvariant();
            _filteredCharacterActionCount = 0;
            for (int i = 0; i < options.Length; i++)
            {
                BehaviorRecipeCharacterActionOptionViewModel option = options[i];
                if (option == null)
                    continue;
                if (normalized.Length > 0
                    && (option.SearchText?.IndexOf(normalized, StringComparison.Ordinal) ?? -1) < 0)
                    continue;
                _filteredCharacterActionIndices[_filteredCharacterActionCount++] = i;
            }
            Fgui.GList list = _characterActionPicker.GetChild("actionList")?.asList;
            if (list != null)
                list.numItems = _filteredCharacterActionCount;
            SetPickerText(
                "emptyText",
                options.Length == 0 ? "动作目录不可用" : "没有匹配的角色动作");
            Fgui.GObject empty = _characterActionPicker.GetChild("emptyText");
            if (empty != null)
                empty.visible = _filteredCharacterActionCount == 0;
            SetPickerText(
                "resultCount",
                _filteredCharacterActionCount.ToString(CultureInfo.InvariantCulture) + " 个动作");
        }

        private void BindCharacterActionPickerRow(int index, Fgui.GObject itemObject)
        {
            Fgui.GComponent item = itemObject?.asCom;
            BehaviorRecipeCharacterActionOptionViewModel[] options =
                _model?.CharacterActionOptions ?? Array.Empty<BehaviorRecipeCharacterActionOptionViewModel>();
            if (item == null || index < 0 || index >= _filteredCharacterActionCount)
                return;
            int optionIndex = _filteredCharacterActionIndices[index];
            if (optionIndex < 0 || optionIndex >= options.Length)
                return;
            BehaviorRecipeCharacterActionOptionViewModel option = options[optionIndex];
            CharacterActionId selectedId = _characterActionPicker?.data is CharacterActionId value
                ? value
                : CharacterActionId.None;
            bool selected = option.ActionId == selectedId;
            SetPickerRowText(item, "title", option.DisplayName);
            SetPickerRowText(item, "stableId", option.ActionId.ToString());
            SetPickerRowText(
                item,
                "meta",
                option.Valid
                    ? option.CategoryText + " · "
                        + option.DurationFrames.ToString(CultureInfo.InvariantCulture) + "f"
                    : option.CategoryText + " · 动作缺少权威时长");
            Fgui.GObject selectedMark = item.GetChild("selected");
            if (selectedMark != null)
                selectedMark.visible = selected;
            item.grayed = !option.Valid;
            item.touchable = option.Valid;
            item.onClick.Set(context =>
            {
                context.StopPropagation();
                if (!option.Valid)
                    return;
                Action<CharacterActionId> selection = _characterActionSelection;
                CloseCharacterActionPicker();
                selection?.Invoke(option.ActionId);
            });
        }

        private void CloseCharacterActionPicker()
        {
            if (_characterActionPicker == null)
                return;
            _characterActionPickerAnchor = null;
            _characterActionSelection = null;
            if (_characterActionPickerUsesPopupService && _popupService != null)
                _popupService.Hide(_characterActionPicker, MxFairyGuiPopupCloseReason.Toggle);
            else if (_characterActionPicker.parent != null)
                Fgui.GRoot.inst.HidePopup(_characterActionPicker);
            _characterActionPickerUsesPopupService = false;
        }

        private void SetPickerText(string childName, string value)
        {
            Fgui.GTextField text = _characterActionPicker?.GetChild(childName)?.asTextField;
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private static void SetPickerRowText(
            Fgui.GComponent row,
            string childName,
            string value)
        {
            Fgui.GTextField text = row?.GetChild(childName)?.asTextField;
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private void BindOperationParameterInput()
        {
            Fgui.GTextInput input = _root.GetChild("inputOperationParameter")?.asTextInput;
            if (input == null)
                return;
            input.onTouchBegin.Set(context => context.StopPropagation());
            input.onSubmit.Set(() => CommitOperationParameter(input));
            input.onFocusOut.Set(() => CommitOperationParameter(input));
            input.onKeyDown.Set(context => HandleInputKey(context, input, input.text));
        }

        private void BindOperationSecondaryParameterInput()
        {
            Fgui.GTextInput input = _root.GetChild("inputOperationSecondaryParameter")?.asTextInput;
            if (input == null)
                return;
            input.onTouchBegin.Set(context => context.StopPropagation());
            input.onSubmit.Set(() => CommitOperationSecondaryParameter(input));
            input.onFocusOut.Set(() => CommitOperationSecondaryParameter(input));
            input.onKeyDown.Set(context => HandleInputKey(context, input, input.text));
        }

        private void CommitOperationSecondaryParameter(Fgui.GTextInput input)
        {
            if (_model?.UnifiedV6 != true || !_model.CanEdit || input == null
                || _model.OperationKind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget)
            {
                return;
            }
            string text = input.text?.Trim() ?? string.Empty;
            if (!TryParseFloat(text, out float multiplier)
                || multiplier < CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.MinimumMoveSpeedMultiplier
                || multiplier > CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.MaximumMoveSpeedMultiplier)
            {
                RejectConditionInput(input, "速度倍率必须在 0.1 到 1.0 之间。");
                return;
            }
            Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationMoveSpeedMultiplier,
                floatValue: multiplier);
        }

        private void CommitOperationParameter(Fgui.GTextInput input)
        {
            if (_model?.UnifiedV6 != true || !_model.CanEdit || input == null)
                return;
            string text = input.text?.Trim() ?? string.Empty;
            if (_model.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
            {
                Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationCharacterActionId,
                    stringValue: text);
                return;
            }
            if (_model.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget)
            {
                if (!TryParseFloat(text, out float minimum) || minimum < 0f)
                {
                    RejectConditionInput(input, "停止距离必须是大于或等于 0 的有效数字。");
                    return;
                }
                Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationMoveMinDistance,
                    floatValue: minimum);
            }
        }

    }
}
