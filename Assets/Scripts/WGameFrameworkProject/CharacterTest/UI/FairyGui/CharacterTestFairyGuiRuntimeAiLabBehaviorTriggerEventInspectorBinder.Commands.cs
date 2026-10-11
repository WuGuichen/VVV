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
        private void SelectDefaultTimelineOutcome()
        {
            if (_model?.CanEdit != true || _model.TimelineOptions.Length == 0)
                return;
            string target = string.IsNullOrEmpty(_model.TargetTimelineId)
                ? _model.TimelineOptions[0].TimelineId
                : _model.TargetTimelineId;
            Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.TargetTimelineId,
                stringValue: target);
        }

        private void RequestDelete()
        {
            if (_model?.CanEdit != true)
                return;
            // Deletion is already one-step recoverable through Undo. The former hidden
            // two-click arming state produced no command/log on the first click and looked
            // exactly like a broken button.
            _pendingDeleteEventId = string.Empty;
            Enqueue(CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DeleteTriggerEvent,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventCommand(
                    _model.TimelineId,
                    _model.EventId,
                    default));
        }

        private void Submit(
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field,
            int conditionIndex = -1,
            int intValue = 0,
            float floatValue = 0f,
            float floatMaxValue = 0f,
            bool boolValue = false,
            string stringValue = "",
            int parameterIntValue = 0,
            string parameterStringValue = "",
            uint allowedTargetPhases = 0u,
            uint requiredTargetFlags = 0u,
            uint forbiddenTargetFlags = 0u)
        {
            if (_model?.CanEdit != true)
                return;
            if (_model.TacticalEditor != null || _tacticalConditionLease != null)
            {
                var lease = _tacticalConditionLease;
                var editor = lease?.Editor ?? _model.TacticalEditor;
                _tacticalConditionLease = null;
                var token = lease?.Token ?? _model.TacticalToken;
                var value = new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue, floatValue, boolValue,
                    stringValue, floatMaxValue, parameterIntValue, parameterStringValue, allowedTargetPhases, requiredTargetFlags, forbiddenTargetFlags);
                string timeline = lease?.Timeline ?? _model.TacticalConditionTimeline, eventId = lease?.Event ?? _model.TacticalConditionEvent;
                if (lease?.LeafIndex >= 0 && conditionIndex >= 0) conditionIndex = lease.LeafIndex;
                editor.Commit(token, "修改条件", s => editor.ApplyCondition(s, timeline, eventId, field, conditionIndex, value));
                return;
            }
            Enqueue(CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.EditTriggerEvent,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventCommand(
                    _model.TimelineId,
                    _model.EventId,
                    field,
                    conditionIndex,
                    new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                        intValue,
                        floatValue,
                        boolValue,
                        stringValue,
                        floatMaxValue,
                        parameterIntValue,
                        parameterStringValue,
                        allowedTargetPhases,
                        requiredTargetFlags,
                        forbiddenTargetFlags)));
        }

        private sealed class RangeGesture
        {
            internal int ConditionIndex;
            internal bool AdjustMinimum;
            internal float Minimum;
            internal float Maximum;
            internal float OriginalMinimum;
            internal float OriginalMaximum;
            internal float DomainMinimum;
            internal float DomainMaximum;
            internal CharacterTestRuntimeAiLabCardConditionFact Fact;
            internal Fgui.GComponent Item;
            internal Fgui.GObject Track;
            internal Fgui.GObject Selection;
            internal Fgui.GObject MinimumHandle;
            internal Fgui.GObject MaximumHandle;
            internal Fgui.GTextField CurrentSummary;
        }

        private sealed class RangeEditorDomain
        {
            internal CharacterTestRuntimeAiLabCardConditionFact Fact;
            internal float Minimum;
            internal float Maximum;
        }

        private void Enqueue(string commandId, object payload) =>
            _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, payload));

        private void BindButton(string name, Action action)
        {
            Fgui.GButton button = _root.GetChild(name)?.asButton;
            if (button != null)
                button.onClick.Set(() => action?.Invoke());
        }

        private void HandleInputKey(Fgui.EventContext context, Fgui.GTextInput input, string original)
        {
            if (context?.inputEvent?.keyCode == KeyCode.Escape)
            {
                input.text = original ?? string.Empty;
                _root?.RequestFocus();
                context.PreventDefault();
            }
            context?.StopPropagation();
        }

        private void RejectConditionInput(Fgui.GTextInput input, string message)
        {
            SetText("validationText", message);
            input.RequestFocus();
        }

        private string ResolveInputValue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field) => field switch
        {
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title => _model?.DisplayTitle ?? string.Empty,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.LocalFrame => (_model?.LocalFrame ?? 1).ToString(CultureInfo.InvariantCulture),
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.DurationFrames => (_model?.DurationFrames ?? 1).ToString(CultureInfo.InvariantCulture),
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.MaxExecutionsPerEntry =>
                ((_model?.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion
                    ? _model.MaxLoopRepeats
                    : _model?.MaxExecutionsPerPass) ?? 1).ToString(CultureInfo.InvariantCulture),
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.CooldownFrames => (_model?.CooldownFrames ?? 0).ToString(CultureInfo.InvariantCulture),
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Priority => (_model?.Priority ?? 0).ToString(CultureInfo.InvariantCulture),
            _ => string.Empty
        };

        private static string ResolveOperationText(
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind operation) => operation switch
        {
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget => "接近 / 移动至目标",
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction => "播放角色动作",
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Ability => "已移除的 Ability 事件",
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Hold => "已移除的 Hold 事件",
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition => "等待条件",
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion => "循环区间",
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline => "进入其他时间轴",
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner => "显式退出行为卡",
            CharacterTestBehaviorExecutionOperations.TurnToTarget => "转向目标",
            _ => "未配置操作"
        };

        private string ResolveValidationText(BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            return model.ValidationText;
        }

        private static string ResolveSignalText(
            CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal signal) => signal switch
        {
            CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Completed => "动作完成",
            CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Rejected => "动作被拒绝",
            _ => "动作失败"
        };

        private static string ResolveReasonText(string reason) => reason switch
        {
            "ActionFailed" => "动作执行失败",
            "ConfigurationInvalid" => "配置无效",
            "AbilityUnavailable" => "能力不可用",
            "CooldownActive" => "能力冷却中",
            "TargetUnavailable" => "目标不可用",
            "RequestRejected" => "动作请求被拒绝",
            "OrchestrationRejected" => "编排交接被拒绝",
            _ => reason
        };

        private static string ResolveTargetTimelineText(BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            for (int i = 0; i < model.TimelineOptions.Length; i++)
                if (string.Equals(model.TimelineOptions[i].TimelineId, model.TargetTimelineId, StringComparison.Ordinal))
                    return model.TimelineOptions[i].DisplayText;
            return "选择目标时间轴";
        }

        private static bool TryParseBoolean(string text, out bool value)
        {
            if (string.Equals(text, "是", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
                || text == "1")
            {
                value = true;
                return true;
            }
            if (string.Equals(text, "否", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)
                || text == "0")
            {
                value = false;
                return true;
            }
            value = false;
            return false;
        }

        private static bool TryParseFloat(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);

        private void SetText(string name, string value)
        {
            Fgui.GTextField text = _root.GetChild(name)?.asTextField;
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private void SetInput(string name, string value)
        {
            Fgui.GTextInput input = _root.GetChild(name)?.asTextInput;
            if (input != null && (_selectionChanged || !input.focused)
                && !string.Equals(input.text, value, StringComparison.Ordinal))
                input.text = value ?? string.Empty;
        }

        private void BindRecipeResponsibilities(
            BehaviorRecipeTriggerEventInspectorViewModel model,
            bool visible)
        {
            if (_root == null)
                return;
            Fgui.GTextField label = _root.GetChild("responsibilityLabel")?.asTextField;
            if (label == null && visible)
            {
                label = new Fgui.GTextField
                {
                    name = "responsibilityLabel",
                    touchable = false,
                    singleLine = true,
                    text = "职责"
                };
                label.textFormat.size = 16;
                label.textFormat.bold = true;
                label.color = new Color32(93, 213, 200, 255);
                label.verticalAlign = Fgui.VertAlignType.Middle;
                _root.AddChild(label);
            }
            if (label != null)
                label.visible = visible;

            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities =
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.OrderedResponsibilities;
            for (int i = 0; i < responsibilities.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility = responsibilities[i];
                Fgui.GButton button = _root.GetChild("responsibilityOption" + i)?.asButton;
                if (button == null && visible)
                {
                    button = Fgui.UIPackage.CreateObject(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                        "CompactNeutralButton")?.asButton;
                    if (button == null)
                        continue;
                    button.name = "responsibilityOption" + i;
                    _root.AddChild(button);
                }
                if (button == null)
                    continue;
                bool selected = HasResponsibility(model?.ResponsibilityKinds, responsibility);
                bool enabled = visible && model?.CanEditResponsibilities == true;
                button.visible = visible;
                button.enabled = enabled;
                button.touchable = enabled;
                button.grayed = false;
                button.titleFontSize = 13;
                button.title = (selected ? "✓ " : string.Empty)
                    + CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.GetDisplayName(responsibility);
                button.onClick.Set(context =>
                {
                    context.StopPropagation();
                    SubmitResponsibilityToggle(responsibility);
                });
            }
        }

        private void SetRecipeResponsibilitiesVisible(bool visible)
        {
            SetVisible("responsibilityLabel", visible);
            for (int i = 0; i < 5; i++)
                SetVisible("responsibilityOption" + i, visible);
        }

        private void SubmitResponsibilityToggle(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility)
        {
            if (_model?.CanEditResponsibilities != true || _model.EntryIndex < 0)
                return;
            int responsibilityIndex = (int)responsibility;
            var selected = new bool[5];
            for (int i = 0; _model.ResponsibilityKinds != null && i < _model.ResponsibilityKinds.Length; i++)
            {
                int index = (int)_model.ResponsibilityKinds[i];
                if (index >= 0 && index < selected.Length)
                    selected[index] = true;
            }
            if (responsibility == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other)
            {
                Array.Clear(selected, 0, selected.Length);
                selected[responsibilityIndex] = true;
            }
            else
            {
                selected[responsibilityIndex] = !selected[responsibilityIndex];
                selected[(int)CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other] = false;
                bool anyExplicit = false;
                for (int i = 1; i < selected.Length; i++)
                    anyExplicit |= selected[i];
                if (!anyExplicit)
                    selected[(int)CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other] = true;
            }

            var payload = new StringBuilder();
            payload.Append(_model.EntryIndex).Append('|');
            bool appended = false;
            for (int i = 0; i < selected.Length; i++)
            {
                if (!selected[i])
                    continue;
                if (appended)
                    payload.Append(',');
                payload.Append((CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind)i);
                appended = true;
            }
            Enqueue(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetEntryResponsibilities,
                payload.ToString());
        }

        private static bool HasResponsibility(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] source,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility)
        {
            for (int i = 0; source != null && i < source.Length; i++)
            {
                if (source[i] == responsibility)
                    return true;
            }
            return false;
        }

        private void SetButtonTitle(string name, string title)
        {
            Fgui.GButton button = _root.GetChild(name)?.asButton;
            if (button != null)
                button.title = title ?? string.Empty;
        }

        private void SetSelected(string name, bool selected)
        {
            Fgui.GButton button = _root.GetChild(name)?.asButton;
            if (button != null)
                button.selected = selected;
        }

        private void SetVisible(string name, bool visible)
        {
            Fgui.GObject child = _root.GetChild(name);
            if (child != null)
                child.visible = visible;
        }

        private void SetEnabled(string name, bool enabled)
        {
            Fgui.GObject child = _root.GetChild(name);
            if (child != null)
            {
                child.enabled = enabled;
                child.grayed = !enabled;
            }
        }

        private static bool IsDescendantOf(Fgui.GObject child, Fgui.GComponent ancestor)
        {
            for (Fgui.GObject current = child; current != null; current = current.parent)
                if (ReferenceEquals(current, ancestor))
                    return true;
            return false;
        }
    }
}
