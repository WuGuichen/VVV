using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private void RefreshConditionObservations(BehaviorRecipeTriggerConditionViewModel[] rows)
        {
            Fgui.GList list = _root?.GetChild("conditionList")?.asList;
            if (list == null || rows == null) return;
            for (int i = 0; i < list.numChildren; i++)
            {
                Fgui.GComponent item = list.GetChildAt(i)?.asCom;
                if (item?.data is not int index || index < 0 || index >= rows.Length) continue;
                var row = rows[index];
                if (row == null || _model?.Conditions == null || index >= _model.Conditions.Length
                    || !string.Equals(row.NodeId, _model.Conditions[index]?.NodeId, StringComparison.Ordinal)) continue;
                Fgui.GTextField observation = item.GetChild("observation")?.asTextField;
                if (row.SubjectScope && item.GetChild("subjectScopeTitle") is Fgui.GTextField subject)
                {
                    string title = row.FactText + (string.IsNullOrEmpty(row.SubjectIdentityText) ? string.Empty : " · " + row.SubjectIdentityText);
                    if (!string.Equals(subject.text, title, StringComparison.Ordinal)) subject.text = title;
                }
                if (observation == null) continue;
                string text = FormatConditionObservation(row);
                if (!string.Equals(observation.text, text, StringComparison.Ordinal)) observation.text = text;
                observation.color = ResolveConditionObservationColor(row);
                observation.visible = !string.IsNullOrEmpty(text);
                BindCharacterActionTooltip(observation, row.ObservedValueText
                    + (string.IsNullOrEmpty(row.ObservationReasonText) ? string.Empty : "\n" + row.ObservationReasonText));
            }
        }

        private void BindConditionList(BehaviorRecipeTriggerConditionViewModel[] rows)
        {
            Fgui.GList list = _root.GetChild("conditionList")?.asList;
            if (list == null)
                return;
            if (list.scrollPane != null)
                _conditionScrollY = list.scrollPane.posY;
            list.layout = Fgui.ListLayoutType.SingleColumn;
            list.selectionMode = Fgui.ListSelectionMode.Single;
            list.lineGap = 2;
            list.itemRenderer = (index, itemObject) =>
            {
                Fgui.GComponent item = itemObject?.asCom;
                BehaviorRecipeTriggerConditionViewModel row =
                    index >= 0 && index < rows.Length ? rows[index] : null;
                if (item == null || row == null)
                    return;
                itemObject.data = index;
                item.onClick.Set(() => _selectedConditionNodeId = row.NodeId ?? string.Empty);
                // The row used to own the whole pointer gesture for condition reordering.
                // That makes the embedded numeric inputs appear editable while the row
                // actually captures the press before either input can receive focus.
                // Reordering needs an explicit drag handle; the editable row itself must
                // never compete with its fields or the range handles for pointer capture.
                item.draggable = false;
                item.onDragEnd.Set(() =>
                {
                    if (!_model.CanEdit || !_model.UnifiedV6 || string.IsNullOrEmpty(row.NodeId))
                        return;
                    Vector2 point = list.GlobalToLocal(Fgui.Stage.inst.touchPosition);
                    float scrollY = list.scrollPane?.posY ?? 0f;
                    int targetRow = Mathf.Clamp(
                        Mathf.FloorToInt((point.y + scrollY) / Math.Max(1f, item.height + 2f)),
                        0,
                        rows.Length - 1);
                    int direction = targetRow < index ? -1 : targetRow > index ? 1 : 0;
                    if (direction != 0)
                    {
                        Submit(
                            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.MoveConditionNode,
                            stringValue: row.NodeId,
                            intValue: direction);
                    }
                });
                bool group = row.NodeKind is CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                    or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any
                    or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not
                    or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope;
                bool subjectScope = row.NodeKind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope;
                bool eventSignal = row.NodeKind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal;
                int editableConditionIndex = _model.UnifiedV6 ? row.LeafIndex : index;
                Fgui.GButton fact = item.GetChild("btnFact")?.asButton;
                Fgui.GButton op = item.GetChild("btnOperator")?.asButton;
                Fgui.GTextInput input = item.GetChild("inputValue")?.asTextInput;
                Fgui.GObject inputBackground = item.GetChild("inputValueBackground");
                Fgui.GTextInput maximumInput = item.GetChild("inputMaxValue")?.asTextInput;
                Fgui.GObject maximumInputBackground = item.GetChild("inputMaxValueBackground");
                Fgui.GTextField rangeMinimumLabel = item.GetChild("rangeMinimumLabel")?.asTextField;
                Fgui.GTextField rangeMaximumLabel = item.GetChild("rangeMaximumLabel")?.asTextField;
                Fgui.GObject rangeTrack = item.GetChild("rangeTrack");
                Fgui.GObject rangeSelection = item.GetChild("rangeSelection");
                Fgui.GObject rangeMinimumHandle = item.GetChild("rangeMinimumHandle");
                Fgui.GObject rangeMaximumHandle = item.GetChild("rangeMaximumHandle");
                Fgui.GTextField rangeCurrentSummary = item.GetChild("rangeCurrentSummary")?.asTextField;
                Fgui.GButton valueDropdown = item.GetChild("btnValueDropdown")?.asButton;
                Fgui.GButton delete = item.GetChild("btnDelete")?.asButton;
                Fgui.GTextField observation = item.GetChild("observation")?.asTextField;
                Fgui.GTextField subjectScopeTitle = item.GetChild("subjectScopeTitle")?.asTextField;
                if (subjectScopeTitle != null)
                {
                    subjectScopeTitle.visible = subjectScope;
                    subjectScopeTitle.SetXY(8f + Mathf.Min(48f, row.Depth * 16f), 5f);
                    subjectScopeTitle.SetSize(
                        398f - Mathf.Min(48f, row.Depth * 16f),
                        40f);
                    subjectScopeTitle.text = subjectScope
                        ? row.FactText
                            + (string.IsNullOrEmpty(row.SubjectIdentityText)
                                ? string.Empty
                                : " · " + row.SubjectIdentityText)
                        : string.Empty;
                    BindCharacterActionTooltip(subjectScopeTitle, row.EffectiveSummary);
                }
                if (fact != null)
                {
                    fact.visible = !subjectScope;
                    fact.x = 4f + Mathf.Min(48f, row.Depth * 16f);
                    fact.title = row.FactText;
                    fact.width = eventSignal ? Mathf.Max(160f, item.width - fact.x - 48f)
                        : group ? 360f - Mathf.Min(48f, row.Depth * 16f) : 142f;
                    SetButtonFontSize(fact, 15);
                    fact.enabled = _model.CanEdit;
                    BindCharacterActionTooltip(
                        fact,
                        "左键更改当前对象的属性，并可继续更换判断对象；右键可直接打开完整对象选择。\n"
                            + row.EffectiveSummary);
                    fact.onClick.Set(() =>
                    {
                        _selectedConditionNodeId = row.NodeId ?? string.Empty;
                        if (subjectScope)
                            return;
                        if (group)
                            ShowConditionGroupKindDropdown(row, fact);
                        else if (eventSignal)
                            ShowEventSignalSourceDropdown(editableConditionIndex, row.SourceEventId, fact);
                        else
                            TryShowFormalConditionPicker(
                                fact,
                                editableConditionIndex,
                                restrictToCurrentSubject: true);
                    });
                    fact.onRightClick.Set(context =>
                    {
                        if (!_model.CanEdit || !_model.UnifiedV6 || group || eventSignal)
                            return;
                        context.StopPropagation();
                        _selectedConditionNodeId = row.NodeId ?? string.Empty;
                        TryShowFormalConditionPicker(fact, editableConditionIndex);
                    });
                }
                bool targetStateValue = row.ValueKind
                    == BehaviorRecipeTriggerConditionValueKind.TargetState;
                if (op != null)
                {
                    op.visible = !group && !targetStateValue;
                    op.title = eventSignal ? row.ValueText : row.OperatorText;
                    float resultX = 4f + Mathf.Min(48f, row.Depth * 16f);
                    op.SetXY(eventSignal ? resultX : 200f, eventSignal ? 44f : 6f);
                    op.SetSize(eventSignal ? Mathf.Max(180f, item.width - resultX - 48f) : 60f, 36f);
                    SetButtonFontSize(op, 15);
                    op.enabled = _model.CanEdit;
                    op.onClick.Set(() =>
                    {
                        _selectedConditionNodeId = row.NodeId ?? string.Empty;
                        if (eventSignal)
                            ShowEventSignalKindDropdown(editableConditionIndex, row.EventSignal, op);
                        else
                            ShowOperatorDropdown(editableConditionIndex, op);
                    });
                }
                bool booleanValue = row.ValueKind == BehaviorRecipeTriggerConditionValueKind.Boolean;
                bool characterActionValue = row.ValueKind
                    == BehaviorRecipeTriggerConditionValueKind.CharacterAction;
                bool abilityValue = row.ValueKind == BehaviorRecipeTriggerConditionValueKind.Ability;
                bool rangeValue = row.ValueKind == BehaviorRecipeTriggerConditionValueKind.Range;
                RangeEditorDomain rangeDomain = rangeValue ? ResolveRangeEditorDomain(row) : null;
                bool hasObservation = !string.IsNullOrEmpty(row.ObservedValueText)
                    || !string.IsNullOrEmpty(row.ObservationReasonText);
                item.height = eventSignal ? (hasObservation ? 118f : 88f)
                    : rangeValue ? (hasObservation ? 122f : 98f) : (hasObservation ? 80f : 52f);
                Fgui.GObject rowBackground = item.GetChild("rowBackground");
                if (rowBackground != null)
                    rowBackground.height = item.height - 2f;
                if (inputBackground != null)
                {
                    inputBackground.visible = !group && !eventSignal
                        && !booleanValue && !characterActionValue && !abilityValue && !targetStateValue;
                    inputBackground.x = rangeValue ? 4f : 266f;
                    inputBackground.y = rangeValue ? 55f : 6f;
                    inputBackground.width = rangeValue ? 68f : 108f;
                    inputBackground.height = rangeValue ? 26f : 36f;
                }
                if (input != null)
                {
                    input.visible = !group && !eventSignal
                        && !booleanValue && !characterActionValue && !abilityValue && !targetStateValue;
                    input.x = rangeValue ? 7f : 272f;
                    input.y = rangeValue ? 55f : 6f;
                    input.width = rangeValue ? 62f : 96f;
                    input.height = rangeValue ? 26f : 36f;
                    if (input.focused == false)
                        input.text = rangeValue
                            ? FormatRangeEndpoint(rangeDomain.Minimum, row.Fact)
                            : row.ValueText;
                    input.enabled = _model.CanEdit
                        && row.NodeKind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal;
                    input.editable = input.enabled;
                    input.touchable = input.enabled;
                    input.onClick.Set(() =>
                    {
                        _selectedConditionNodeId = row.NodeId ?? string.Empty;
                        input.RequestFocus(true);
                    });
                    input.onSubmit.Set(() =>
                    {
                        if (rangeValue)
                            CommitRangeDomain(item, row, rangeDomain, input, maximumInput);
                        else
                            CommitConditionValue(editableConditionIndex, input);
                    });
                    input.onFocusOut.Set(() =>
                    {
                        if (rangeValue)
                            CommitRangeDomain(item, row, rangeDomain, input, maximumInput);
                        else
                            CommitConditionValue(editableConditionIndex, input);
                    });
                    input.onKeyDown.Set(context => HandleInputKey(
                        context,
                        input,
                        rangeValue
                            ? FormatRangeEndpoint(rangeDomain.Minimum, row.Fact)
                            : row.ValueText));
                }
                BindRangeEditor(
                    item,
                    row,
                    editableConditionIndex,
                    rangeValue && !group && !eventSignal,
                    input,
                    maximumInput,
                    maximumInputBackground,
                    rangeMinimumLabel,
                    rangeMaximumLabel,
                    rangeTrack,
                    rangeSelection,
                    rangeMinimumHandle,
                    rangeMaximumHandle,
                    rangeCurrentSummary,
                    rangeDomain);
                if (valueDropdown != null)
                {
                    valueDropdown.visible = !group && !eventSignal
                        && (booleanValue || characterActionValue || abilityValue || targetStateValue);
                    valueDropdown.x = targetStateValue ? 154f : 266f;
                    valueDropdown.width = targetStateValue ? 220f : 108f;
                    valueDropdown.title = targetStateValue
                        ? FormatTargetStateRequirement(row.ValueText)
                        : row.ValueText;
                    SetButtonFontSize(valueDropdown, 15);
                    BindCharacterActionTooltip(valueDropdown, valueDropdown.title);
                    valueDropdown.enabled = _model.CanEdit
                        && row.NodeKind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal;
                    valueDropdown.onClick.Set(() =>
                    {
                        _selectedConditionNodeId = row.NodeId ?? string.Empty;
                        if (targetStateValue)
                        {
                            TryShowTargetStatePicker(valueDropdown, row);
                        }
                        else if (characterActionValue)
                        {
                            ShowCharacterActionPicker(
                                valueDropdown,
                                row.CharacterActionId,
                                actionId => Submit(
                                    CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionCharacterActionId,
                                    conditionIndex: editableConditionIndex,
                                    stringValue: actionId.Value.ToString(CultureInfo.InvariantCulture)));
                        }
                        else if (abilityValue)
                        {
                            ShowAbilityPicker(valueDropdown, row.AbilityId, abilityId => Submit(
                                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionAbilityId,
                                conditionIndex: editableConditionIndex,
                                intValue: abilityId));
                        }
                        else
                        {
                            ShowBooleanDropdown(editableConditionIndex, valueDropdown);
                        }
                    });
                }
                if (delete != null)
                {
                    delete.visible = !subjectScope;
                    delete.enabled = _model.CanEdit && !subjectScope;
                    delete.onClick.Set(() =>
                    {
                        _selectedConditionNodeId = row.NodeId ?? string.Empty;
                        Submit(
                            _model.UnifiedV6
                                ? CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.RemoveConditionNode
                                : CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.RemoveCondition,
                            conditionIndex: editableConditionIndex,
                            stringValue: row.NodeId);
                    });
                }
                Fgui.GTextField validation = item.GetChild("validation")?.asTextField;
                if (validation != null)
                {
                    validation.x = rangeValue ? 4f : 266f;
                    validation.y = rangeValue ? 84f : 42f;
                    validation.width = rangeValue ? 400f : 140f;
                    validation.text = row.ValidationText;
                    validation.visible = !string.IsNullOrEmpty(validation.text);
                }
                if (observation != null)
                {
                    observation.SetXY(8f, eventSignal ? 86f : rangeValue ? 96f : 50f);
                    observation.SetSize(396f, 24f);
                    observation.text = FormatConditionObservation(row);
                    observation.color = ResolveConditionObservationColor(row);
                    observation.visible = !string.IsNullOrEmpty(observation.text);
                    Fgui.TextFormat observationFormat = observation.textFormat;
                    observationFormat.size = 13;
                    observationFormat.bold = true;
                    observation.textFormat = observationFormat;
                    BindCharacterActionTooltip(
                        observation,
                        row.ObservedValueText
                            + (string.IsNullOrEmpty(row.ObservationReasonText)
                                ? string.Empty
                                : "\n" + row.ObservationReasonText));
                }
                ArrangeOverviewCondition(item, row);
            };
            list.numItems = rows.Length;
            if (list.scrollPane != null && rows.Length > 0)
                list.scrollPane.SetPosY(Mathf.Min(_conditionScrollY, list.scrollPane.contentHeight), false);
        }

        internal static string FormatConditionObservation(BehaviorRecipeTriggerConditionViewModel row)
        {
            if (row == null || string.IsNullOrEmpty(row.ObservedValueText))
                return row?.ObservationReasonText ?? string.Empty;
            if (row.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetState)
            {
                string targetStateSummary = row.ObservationState switch
                {
                    BehaviorRecipeConditionObservationState.Satisfied => "✓ 当前状态满足要求",
                    BehaviorRecipeConditionObservationState.Unsatisfied => "✕ 当前状态不满足要求",
                    BehaviorRecipeConditionObservationState.SubjectUnavailable => "— 目标不可用",
                    _ => "· 目标状态未采样"
                };
                return row.ObservationState == BehaviorRecipeConditionObservationState.Unsatisfied
                    && !string.IsNullOrEmpty(row.ObservationReasonText)
                    ? targetStateSummary + " · " + row.ObservationReasonText
                    : targetStateSummary;
            }
            string prefix = row.ObservationState switch
            {
                BehaviorRecipeConditionObservationState.Satisfied => "✓ ",
                BehaviorRecipeConditionObservationState.Unsatisfied => "✕ ",
                BehaviorRecipeConditionObservationState.SubjectUnavailable => "— ",
                _ => "· "
            };
            return prefix + row.ObservedValueText
                + (string.IsNullOrEmpty(row.ObservationReasonText)
                    ? string.Empty
                    : " · " + row.ObservationReasonText);
        }

        internal static string FormatTargetStateRequirement(string valueText)
        {
            string value = valueText?.Trim() ?? string.Empty;
            const string phasePrefix = "阶段：";
            if (value.StartsWith(phasePrefix, StringComparison.Ordinal))
                return "要求：" + value.Substring(phasePrefix.Length);
            return value.Length > 0 ? "要求：" + value : "要求：未配置";
        }

        private static Color ResolveConditionObservationColor(BehaviorRecipeTriggerConditionViewModel row) =>
            row?.ObservationState switch
            {
                BehaviorRecipeConditionObservationState.Satisfied => new Color(0.42f, 0.86f, 0.64f, 1f),
                BehaviorRecipeConditionObservationState.Unsatisfied => new Color(1f, 0.58f, 0.48f, 1f),
                BehaviorRecipeConditionObservationState.SubjectUnavailable => new Color(0.58f, 0.68f, 0.72f, 1f),
                _ => new Color(0.62f, 0.72f, 0.76f, 1f)
            };

        private void ConfigureTypography()
        {
            if (_root == null)
                return;
            string[] buttons =
            {
                "btnGroupAll", "btnGroupAnyNot", "btnAddCondition", "btnAddConditionGroup",
                "btnFrameAbsolute", "btnFrameActionEnd", "btnSignal", "btnSignalReason",
                "btnOutcomeReturn", "btnTargetTimeline",
                "btnCharacterActionParameter", "btnViewCharacterActionTimeline",
                "btnPriorityMinus", "btnPriorityPlus", "btnLocateTarget", "btnCloseInspector"
            };
            for (int i = 0; i < buttons.Length; i++)
                SetButtonFontSize(_root.GetChild(buttons[i])?.asButton, 15);
            SetButtonFontSize(_root.GetChild("btnAddCondition")?.asButton, 14);
            SetButtonFontSize(_root.GetChild("btnAddConditionGroup")?.asButton, 14);
            SetButtonFontSize(_root.GetChild("btnCloseInspector")?.asButton, 16);
            SetButtonTitle("btnCloseInspector", "✕");
            Fgui.GTextInput inputTitle = _root.GetChild("inputTitle")?.asTextInput;
            if (inputTitle != null)
            {
                Fgui.TextFormat format = inputTitle.textFormat;
                format.size = 16;
                format.bold = true;
                inputTitle.textFormat = format;
                inputTitle.color = new Color32(243, 247, 247, 255);
            }
        }

        private void UpdateAccentColor(BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            Fgui.GGraph accent = _root?.GetChild("accent")?.asGraph;
            if (accent == null)
                return;
            Color32 color = model?.CharacterActionEvent == true
                ? new Color32(56, 168, 248, 255)
                : model?.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
                    ? new Color32(52, 211, 198, 255)
                    : new Color32(168, 102, 236, 255);
            accent.DrawRect(4f, 46f, 0, Color.clear, color);
        }

        private void ApplyCompactLayout(
            bool predicate,
            bool frame,
            bool signal,
            bool multi,
            bool enterTimeline,
            bool hasValidation)
        {
            const float labelX = 30f;
            const float labelWidth = 86f;
            const float contentX = 72f;
            const float contentWidth = 414f;
            float y = 96f;

            Place("ruleWhenLabel", labelX, y, 34f, 52f);
            if (predicate)
            {
                if (multi)
                {
                    Place("btnGroupAll", contentX, y, 201f, 32f);
                    Place("btnGroupAnyNot", 279f, y, 207f, 32f);
                    y += 38f;
                }
                float listHeight = Mathf.Min(Mathf.Max(_model?.Conditions.Length ?? 1, 1), 2) * 54f;
                Place("conditionList", contentX, y, contentWidth, listHeight);
                y += listHeight + 6f;
                Place("btnAddCondition", contentX, y, 200f, 30f);
                Place("btnAddConditionGroup", contentX + 208f, y, 206f, 30f);
                y += 42f;
            }
            else if (frame)
            {
                Place("btnFrameAbsolute", contentX, y, 134f, 36f);
                Place("btnFrameActionEnd", 212f, y, 176f, 36f);
                Place("inputLocalFrame", 394f, y, 92f, 36f);
                y += 46f;
            }
            else if (signal)
            {
                bool showReason = _model != null
                    && _model.ActionSignal != CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Completed;
                Place("btnSignal", contentX, y, showReason ? 180f : contentWidth, 36f);
                if (showReason)
                    Place("btnSignalReason", 258f, y, 228f, 36f);
                y += 46f;
            }

            Place("ruleDivider", labelX, y, 456f, 1f);
            y += 12f;
            Place("ruleThenLabel", labelX, y, 34f, 38f);
            Place("btnOutcomeReturn", contentX, y, contentWidth, 38f);
            y += 46f;
            if (enterTimeline)
            {
                Place("btnTargetTimeline", contentX, y, 324f, 34f);
                Place("btnLocateTarget", 402f, y, 84f, 34f);
                y += 42f;
            }

            Place("advancedDivider", labelX, y, 456f, 1f);
            y += 12f;
            Place("priorityLabel", labelX, y, labelWidth, 34f);
            PlaceStepperButton("btnPriorityMinus", 120f, y, "-10");
            Place("inputPriority", 162f, y, 68f, 34f);
            PlaceStepperButton("btnPriorityPlus", 234f, y, "+10");
            Place("deleteHint", 300f, y, 140f, 36f);
            Place("btnDeleteEvent", 448f, y, 36f, 36f);
            y += 44f;
            if (hasValidation)
            {
                Place("validationText", labelX, y, 456f, 36f);
                y += 42f;
            }
            Place("bodyBackground", 16f, 78f, 488f, Mathf.Clamp(y - 60f, 240f, 540f));
        }

        private void ConfigureDeleteAction(bool confirming)
        {
            SetVisible("deleteHint", confirming);
            SetText("deleteHint", confirming ? "再次点击删除" : string.Empty);
        }

        private void Place(string name, float x, float y, float width, float height)
        {
            Fgui.GObject child = _root?.GetChild(name);
            if (child == null)
                return;
            child.relations.ClearAll();
            child.SetXY(x, y);
            child.SetSize(width, height);
        }

        private void PlaceStepperButton(string name, float x, float y, string title)
        {
            Place(name, x, y, 38f, 32f);
            Fgui.GButton button = _root?.GetChild(name)?.asButton;
            if (button == null)
                return;
            button.title = title;
            Fgui.GTextField text = button.GetTextField();
            if (text == null)
                return;
            text.SetXY(0f, 0f);
            text.SetSize(38f, 32f);
            Fgui.TextFormat format = text.textFormat;
            format.size = title.Length > 2 ? 14 : 18;
            format.bold = true;
            text.textFormat = format;
        }

        private static void SetButtonFontSize(Fgui.GButton button, int fontSize)
        {
            Fgui.GTextField title = button?.GetTextField();
            if (title != null)
            {
                Fgui.TextFormat format = title.textFormat;
                format.size = fontSize;
                title.textFormat = format;
            }
        }

    }
}
