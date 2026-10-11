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
        private void BindInput(
            string name,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field)
        {
            Fgui.GTextInput input = _root.GetChild(name)?.asTextInput;
            if (input == null)
                return;
            input.onTouchBegin.Set(context => context.StopPropagation());
            input.onSubmit.Set(() => CommitInput(input, field));
            input.onFocusOut.Set(() => CommitInput(input, field));
            input.onKeyDown.Set(context => HandleInputKey(context, input, ResolveInputValue(field)));
        }

        private void CommitInput(
            Fgui.GTextInput input,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field)
        {
            if (input == null || !CanCommitInput(_model, field))
                return;
            if (field == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title)
            {
                if (_model.InspectorMode == BehaviorRecipeInspectorMode.Recipe)
                {
                    if (_model.CanEditDisplayTitle)
                    {
                        Enqueue(
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardDisplayName,
                            new CharacterTestRuntimeAiLabProjectCardMetadataEditPayload(
                                _model.EntryIndex,
                                input.text));
                    }
                    return;
                }
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField targetField =
                    _model.InspectorMode == BehaviorRecipeInspectorMode.Timeline
                        ? CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.TimelineTitle
                        : field;
                Submit(targetField, stringValue: input.text);
                return;
            }
            if (!int.TryParse(input.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                SetText("validationText", "请输入有效整数。按 Esc 恢复原值。");
                input.RequestFocus();
                return;
            }
            Submit(field, intValue: value);
        }

        internal static bool CanCommitInput(
            BehaviorRecipeTriggerEventInspectorViewModel model,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field)
        {
            if (model == null)
                return false;
            return field == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title
                && model.InspectorMode == BehaviorRecipeInspectorMode.Recipe
                    ? model.CanEditDisplayTitle
                    : model.CanEdit;
        }

        private void CommitConditionValue(int index, Fgui.GTextInput input)
        {
            if (_model?.CanEdit != true || index < 0 || index >= _model.Conditions.Length || input == null)
                return;
            BehaviorRecipeTriggerConditionViewModel row = _model.Conditions[index];
            string text = input.text?.Trim() ?? string.Empty;
            switch (row.ValueKind)
            {
                case BehaviorRecipeTriggerConditionValueKind.Boolean:
                    if (!TryParseBoolean(text, out bool boolean))
                    {
                        RejectConditionInput(input, "布尔值请输入“是”或“否”。");
                        return;
                    }
                    Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionBoolValue,
                        index, boolValue: boolean);
                    break;
                case BehaviorRecipeTriggerConditionValueKind.Ability:
                    string abilityText = text.StartsWith("Ability ", StringComparison.OrdinalIgnoreCase)
                        ? text.Substring("Ability ".Length) : text;
                    if (!int.TryParse(abilityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int abilityId))
                    {
                        RejectConditionInput(input, "请输入有效 Ability ID。");
                        return;
                    }
                    Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionAbilityId,
                        index, intValue: abilityId);
                    break;
                case BehaviorRecipeTriggerConditionValueKind.CharacterAction:
                    Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionCharacterActionId,
                        index, stringValue: text);
                    break;
                case BehaviorRecipeTriggerConditionValueKind.Range:
                    string[] range = text.Split(',');
                    if (range.Length != 2
                        || !TryParseFloat(range[0], out float minimum)
                        || !TryParseFloat(range[1], out float maximum)
                        || maximum < minimum)
                    {
                        RejectConditionInput(input, "范围请输入“最小值,最大值”。");
                        return;
                    }
                    Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionValue,
                        index, floatValue: minimum);
                    Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionMaxValue,
                        index, floatValue: maximum);
                    break;
                default:
                    string numericText = text.Replace("米", string.Empty).Replace("°", string.Empty).Replace("f", string.Empty).Trim();
                    if (!TryParseFloat(numericText, out float numeric))
                    {
                        RejectConditionInput(input, "请输入有效数值。");
                        return;
                    }
                    Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionValue,
                        index, floatValue: numeric);
                    break;
            }
        }

        private void CommitRangeDomain(
            Fgui.GComponent item,
            BehaviorRecipeTriggerConditionViewModel row,
            RangeEditorDomain domain,
            Fgui.GTextInput minimumInput,
            Fgui.GTextInput maximumInput)
        {
            if (_model?.CanEdit != true
                || row == null
                || domain == null
                || minimumInput == null
                || maximumInput == null)
                return;
            bool minimumValid = TryParseRangeEndpoint(minimumInput.text, out float minimum);
            bool maximumValid = TryParseRangeEndpoint(maximumInput.text, out float maximum);
            if (!minimumValid
                || !maximumValid
                || minimum < 0f
                || maximum <= minimum)
            {
                RejectConditionInput(
                    minimumValid && maximumValid && maximum <= minimum
                        ? maximumInput
                        : minimumInput,
                    "请输入有效滑轨上下界，且上界必须大于下界。");
                return;
            }
            if (minimum > row.Value || maximum < row.MaxValue)
            {
                RejectConditionInput(
                    minimum > row.Value ? minimumInput : maximumInput,
                    "滑轨上下界必须包含当前条件范围。请先移动范围手柄。");
                return;
            }
            domain.Minimum = minimum;
            domain.Maximum = maximum;
            minimumInput.text = FormatRangeEndpoint(domain.Minimum, row.Fact);
            maximumInput.text = FormatRangeEndpoint(domain.Maximum, row.Fact);
            ApplyRangeVisual(
                item?.GetChild("rangeTrack"),
                item?.GetChild("rangeSelection"),
                item?.GetChild("rangeMinimumHandle"),
                item?.GetChild("rangeMaximumHandle"),
                row.Value,
                row.MaxValue,
                domain.Minimum,
                domain.Maximum);
        }

        private void BindRangeEditor(
            Fgui.GComponent item,
            BehaviorRecipeTriggerConditionViewModel row,
            int conditionIndex,
            bool visible,
            Fgui.GTextInput minimumInput,
            Fgui.GTextInput maximumInput,
            Fgui.GObject maximumInputBackground,
            Fgui.GTextField minimumLabel,
            Fgui.GTextField maximumLabel,
            Fgui.GObject track,
            Fgui.GObject selection,
            Fgui.GObject minimumHandle,
            Fgui.GObject maximumHandle,
            Fgui.GTextField currentSummary,
            RangeEditorDomain domain)
        {
            if (maximumInputBackground != null)
            {
                maximumInputBackground.visible = visible;
                maximumInputBackground.x = 338f;
                maximumInputBackground.y = 55f;
                maximumInputBackground.width = 68f;
                maximumInputBackground.height = 26f;
            }
            if (maximumInput != null)
            {
                maximumInput.visible = visible;
                maximumInput.x = 341f;
                maximumInput.y = 55f;
                maximumInput.width = 62f;
                maximumInput.height = 26f;
                maximumInput.enabled = visible && _model.CanEdit;
                maximumInput.editable = maximumInput.enabled;
                maximumInput.touchable = maximumInput.enabled;
                if (visible && !maximumInput.focused)
                    maximumInput.text = FormatRangeEndpoint(domain.Maximum, row.Fact);
                maximumInput.onClick.Set(() =>
                {
                    _selectedConditionNodeId = row.NodeId ?? string.Empty;
                    maximumInput.RequestFocus(true);
                });
                maximumInput.onSubmit.Set(() => CommitRangeDomain(
                    item, row, domain, minimumInput, maximumInput));
                maximumInput.onFocusOut.Set(() => CommitRangeDomain(
                    item, row, domain, minimumInput, maximumInput));
                maximumInput.onKeyDown.Set(context => HandleInputKey(
                    context,
                    maximumInput,
                    FormatRangeEndpoint(domain.Maximum, row.Fact)));
            }
            if (minimumLabel != null)
            {
                minimumLabel.visible = visible;
                minimumLabel.text = "下界";
            }
            if (maximumLabel != null)
            {
                maximumLabel.visible = visible;
                maximumLabel.text = "上界";
            }
            SetRangeObjectVisible(track, visible);
            SetRangeObjectVisible(selection, visible);
            SetRangeObjectVisible(minimumHandle, visible);
            SetRangeObjectVisible(maximumHandle, visible);
            if (currentSummary != null)
            {
                currentSummary.visible = visible;
                currentSummary.text = FormatCurrentRange(row.Value, row.MaxValue, row.Fact);
            }
            if (!visible || item == null || track == null || selection == null
                || minimumHandle == null || maximumHandle == null || domain == null)
                return;

            ApplyRangeVisual(
                track,
                selection,
                minimumHandle,
                maximumHandle,
                row.Value,
                row.MaxValue,
                domain.Minimum,
                domain.Maximum);
            BindRangeHandle(
                item,
                row,
                conditionIndex,
                adjustMinimum: true,
                minimumInput,
                maximumInput,
                track,
                selection,
                minimumHandle,
                maximumHandle,
                currentSummary,
                domain.Minimum,
                domain.Maximum);
            BindRangeHandle(
                item,
                row,
                conditionIndex,
                adjustMinimum: false,
                minimumInput,
                maximumInput,
                track,
                selection,
                minimumHandle,
                maximumHandle,
                currentSummary,
                domain.Minimum,
                domain.Maximum);
        }

        private void BindRangeHandle(
            Fgui.GComponent item,
            BehaviorRecipeTriggerConditionViewModel row,
            int conditionIndex,
            bool adjustMinimum,
            Fgui.GTextInput minimumInput,
            Fgui.GTextInput maximumInput,
            Fgui.GObject track,
            Fgui.GObject selection,
            Fgui.GObject minimumHandle,
            Fgui.GObject maximumHandle,
            Fgui.GTextField currentSummary,
            float domainMinimum,
            float domainMaximum)
        {
            Fgui.GObject handle = adjustMinimum ? minimumHandle : maximumHandle;
            handle.onTouchBegin.Set(context =>
            {
                if (_model?.CanEdit != true)
                    return;
                _rangeGesture = new RangeGesture
                {
                    ConditionIndex = conditionIndex,
                    AdjustMinimum = adjustMinimum,
                    Minimum = row.Value,
                    Maximum = row.MaxValue,
                    OriginalMinimum = row.Value,
                    OriginalMaximum = row.MaxValue,
                    DomainMinimum = domainMinimum,
                    DomainMaximum = domainMaximum,
                    Fact = row.Fact,
                    Item = item,
                    Track = track,
                    Selection = selection,
                    MinimumHandle = minimumHandle,
                    MaximumHandle = maximumHandle,
                    CurrentSummary = currentSummary
                };
                context.CaptureTouch();
                context.StopPropagation();
            });
            handle.onTouchMove.Set(context =>
            {
                if (_rangeGesture == null || !ReferenceEquals(_rangeGesture.Item, item))
                    return;
                Vector2 local = item.GlobalToLocal(Fgui.Stage.inst.touchPosition);
                float normalized = Mathf.Clamp01((local.x - track.x) / Math.Max(1f, track.width));
                float value = SnapRangeValue(
                    _rangeGesture.DomainMinimum
                    + normalized * (_rangeGesture.DomainMaximum - _rangeGesture.DomainMinimum),
                    _rangeGesture.Fact);
                if (_rangeGesture.AdjustMinimum)
                    _rangeGesture.Minimum = Math.Min(value, _rangeGesture.Maximum);
                else
                    _rangeGesture.Maximum = Math.Max(value, _rangeGesture.Minimum);
                ApplyRangeGesturePreview(_rangeGesture);
                context.StopPropagation();
            });
            handle.onTouchEnd.Set(context =>
            {
                if (_rangeGesture == null || !ReferenceEquals(_rangeGesture.Item, item))
                    return;
                RangeGesture gesture = _rangeGesture;
                _rangeGesture = null;
                if (!Mathf.Approximately(gesture.Minimum, gesture.OriginalMinimum)
                    || !Mathf.Approximately(gesture.Maximum, gesture.OriginalMaximum))
                {
                    Submit(
                        CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionRange,
                        gesture.ConditionIndex,
                        floatValue: gesture.Minimum,
                        floatMaxValue: gesture.Maximum);
                }
                context.StopPropagation();
            });
        }

        private static void ApplyRangeGesturePreview(RangeGesture gesture)
        {
            ApplyRangeVisual(
                gesture.Track,
                gesture.Selection,
                gesture.MinimumHandle,
                gesture.MaximumHandle,
                gesture.Minimum,
                gesture.Maximum,
                gesture.DomainMinimum,
                gesture.DomainMaximum);
            if (gesture.CurrentSummary != null)
            {
                gesture.CurrentSummary.text = FormatCurrentRange(
                    gesture.Minimum,
                    gesture.Maximum,
                    gesture.Fact);
            }
        }

        private static void ApplyRangeVisual(
            Fgui.GObject track,
            Fgui.GObject selection,
            Fgui.GObject minimumHandle,
            Fgui.GObject maximumHandle,
            float minimum,
            float maximum,
            float domainMinimum,
            float domainMaximum)
        {
            if (track == null || selection == null || minimumHandle == null || maximumHandle == null)
                return;
            float width = Math.Max(1f, track.width);
            float domainRange = Math.Max(0.0001f, domainMaximum - domainMinimum);
            float minimumX = track.x
                + Mathf.Clamp01((minimum - domainMinimum) / domainRange) * width;
            float maximumX = track.x
                + Mathf.Clamp01((maximum - domainMinimum) / domainRange) * width;
            minimumHandle.x = minimumX - minimumHandle.width * 0.5f;
            maximumHandle.x = maximumX - maximumHandle.width * 0.5f;
            selection.x = minimumX;
            selection.width = Math.Max(1f, maximumX - minimumX);
        }

        private RangeEditorDomain ResolveRangeEditorDomain(
            BehaviorRecipeTriggerConditionViewModel row)
        {
            string key = string.Concat(
                _model?.TimelineId ?? string.Empty,
                "\u001f",
                _model?.EventId ?? string.Empty,
                "\u001f",
                row.NodeId ?? row.LeafIndex.ToString(CultureInfo.InvariantCulture));
            if (!_rangeDomains.TryGetValue(key, out RangeEditorDomain domain)
                || domain.Fact != row.Fact)
            {
                domain = new RangeEditorDomain
                {
                    Fact = row.Fact,
                    Minimum = 0f,
                    Maximum = ResolveDefaultRangeDomainMaximum(row.Fact)
                };
                _rangeDomains[key] = domain;
            }
            domain.Minimum = Math.Min(domain.Minimum, row.Value);
            domain.Maximum = Math.Max(domain.Maximum, row.MaxValue);
            return domain;
        }

        private static float ResolveDefaultRangeDomainMaximum(
            CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            if (IsDistanceFact(fact))
                return 12f;
            if (fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames)
                return 120f;
            return 10f;
        }

        private static float SnapRangeValue(
            float value,
            CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            float step = fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames
                ? 1f
                : 0.1f;
            return Mathf.Max(0f, Mathf.Round(value / step) * step);
        }

        private static bool TryParseRangeEndpoint(string text, out float value)
        {
            string numeric = (text ?? string.Empty)
                .Replace("米", string.Empty)
                .Replace("°", string.Empty)
                .Replace("f", string.Empty)
                .Trim();
            return TryParseFloat(numeric, out value);
        }

        private static string FormatRangeEndpoint(
            float value,
            CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            string formatted = value.ToString("0.###", CultureInfo.InvariantCulture);
            if (fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames)
                return formatted + "f";
            if (IsDistanceFact(fact))
                return formatted + " 米";
            return formatted;
        }

        private static string FormatCurrentRange(
            float minimum,
            float maximum,
            CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            string minimumText = minimum.ToString("0.###", CultureInfo.InvariantCulture);
            string maximumText = maximum.ToString("0.###", CultureInfo.InvariantCulture);
            string unit = fact switch
            {
                CharacterTestRuntimeAiLabCardConditionFact.TargetDistance => " 米",
                CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance => " 米",
                CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames => "f",
                _ => string.Empty
            };
            return "要求范围：" + minimumText + "–" + maximumText + unit;
        }

        private static bool IsDistanceFact(CharacterTestRuntimeAiLabCardConditionFact fact) =>
            fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
            || fact == CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance;

        private static void SetRangeObjectVisible(Fgui.GObject item, bool visible)
        {
            if (item != null)
                item.visible = visible;
        }

    }
}
