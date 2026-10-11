using System;
using System.Globalization;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField
    {
        Title,
        TimelineTitle,
        SetEntryTimeline,
        SourceKind,
        PredicateGroupMode,
        AddCondition,
        AddSubjectCondition,
        ReplaceSubjectCondition,
        RemoveCondition,
        ConditionFact,
        ConditionOperator,
        ConditionBoolValue,
        ConditionValue,
        ConditionMaxValue,
        ConditionRange,
        ConditionAbilityId,
        ConditionCharacterActionId,
        ConditionTargetStateMasks,
        LocalFrameAnchor,
        LocalFrame,
        ActionSignal,
        ActionSignalReason,
        ActivationStartFrame,
        WindowEndMode,
        ActivationDurationFrames,
        OutcomeKind,
        TargetTimelineId,
        Priority,
        OperationKind,
        OperationAbilityId,
        OperationCharacterActionId,
        ConvertCharacterActionEvent,
        OperationMoveMinDistance,
        OperationMoveMaxDistance,
        OperationMoveSpeedMultiplier,
        TimingKind,
        DurationMode,
        DurationFrames,
        MaxExecutionsPerEntry,
        MaxExecutionsPerPass,
        MaxLoopRepeats,
        ConfirmDuration,
        CooldownFrames,
        AddAllGroup,
        AddAnyGroup,
        AddNotGroup,
        AddEventSignal,
        SetConditionGroupKind,
        ConditionSignalSourceEventId,
        ConditionSignalKind,
        RemoveConditionNode,
        MoveConditionNode
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipePredicateGroupMode
    {
        AllConditionsMatch,
        AnyConditionDoesNotMatch
    }

    internal readonly struct CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue
    {
        internal CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
            int intValue = 0,
            float floatValue = 0f,
            bool boolValue = false,
            string stringValue = "",
            float floatMaxValue = 0f,
            int parameterIntValue = 0,
            string parameterStringValue = "",
            uint allowedTargetPhases = 0u,
            uint requiredTargetFlags = 0u,
            uint forbiddenTargetFlags = 0u)
        {
            IntValue = intValue;
            FloatValue = floatValue;
            FloatMaxValue = floatMaxValue;
            BoolValue = boolValue;
            StringValue = stringValue ?? string.Empty;
            ParameterIntValue = parameterIntValue;
            ParameterStringValue = parameterStringValue ?? string.Empty;
            AllowedTargetPhases = allowedTargetPhases;
            RequiredTargetFlags = requiredTargetFlags;
            ForbiddenTargetFlags = forbiddenTargetFlags;
        }

        internal int IntValue { get; }
        internal float FloatValue { get; }
        internal float FloatMaxValue { get; }
        internal bool BoolValue { get; }
        internal string StringValue { get; }
        internal int ParameterIntValue { get; }
        internal string ParameterStringValue { get; }
        internal uint AllowedTargetPhases { get; }
        internal uint RequiredTargetFlags { get; }
        internal uint ForbiddenTargetFlags { get; }
    }

    /// <summary>
    /// Direct v6 Event and condition-owner mutation. Legacy v5 editing remains isolated below as a load-time
    /// compatibility path and never becomes the v6 editing authority.
    /// </summary>
    internal static class CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation
    {
        internal const string ActivationConditionOwnerId = "$activation";
        internal const string TimelineEntryConditionOwnerId = "$timeline-entry";
        internal static bool TryApply(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId,
            string eventId,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field,
            int conditionIndex,
            in CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue value,
            out bool changed,
            out string diagnostic)
        {
            changed = false;
            diagnostic = string.Empty;
            if (CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
            {
                return TryApplyV6(payload, timelineId, eventId, field, conditionIndex, value, out changed, out diagnostic);
            }
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(payload);
            if (!TryFind(payload, timelineId, NormalizeEventId(eventId), out var timeline, out var trigger))
            {
                diagnostic = "所选分支事件已不存在，请重新选择。";
                return false;
            }

            switch (field)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title:
                    changed = Set(trigger.Title, value.StringValue.Trim(), item => trigger.Title = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.SourceKind:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind), value.IntValue))
                    {
                        diagnostic = "触发来源无效。";
                        return false;
                    }
                    CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind source =
                        (CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind)value.IntValue;
                    if (trigger.SourceKind != source)
                    {
                        trigger.SourceKind = source;
                        NormalizeSource(trigger, timeline);
                        changed = true;
                    }
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.PredicateGroupMode:
                    bool expected = value.IntValue
                        != (int)CharacterTestRuntimeAiLabBehaviorRecipePredicateGroupMode.AnyConditionDoesNotMatch;
                    changed = Set(trigger.ExpectedResult, expected, item => trigger.ExpectedResult = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddCondition:
                    trigger.Conditions.Add(CreateDefaultCondition(trigger));
                    changed = true;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.RemoveCondition:
                    if (!TryGetCondition(trigger, conditionIndex, out _, out diagnostic))
                        return false;
                    trigger.Conditions.RemoveAt(conditionIndex);
                    changed = true;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionFact:
                    if (!TryGetCondition(trigger, conditionIndex, out var factCondition, out diagnostic))
                        return false;
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardConditionFact), value.IntValue))
                    {
                        diagnostic = "条件事实无效。";
                        return false;
                    }
                    CharacterTestRuntimeAiLabCardConditionFact fact =
                        (CharacterTestRuntimeAiLabCardConditionFact)value.IntValue;
                    if (factCondition.Fact != fact)
                    {
                        factCondition.Fact = fact;
                        NormalizeConditionForFact(factCondition);
                        changed = true;
                    }
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionOperator:
                    if (!TryGetCondition(trigger, conditionIndex, out var operatorCondition, out diagnostic))
                        return false;
                    if (!TryApplyEffectiveOperator(trigger, operatorCondition, value.IntValue, out changed, out diagnostic))
                        return false;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionBoolValue:
                    if (!TryGetCondition(trigger, conditionIndex, out var boolCondition, out diagnostic))
                        return false;
                    bool rawBoolValue = value.BoolValue;
                    if (trigger.Conditions.Count == 1
                        && !trigger.ExpectedResult
                        && boolCondition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals)
                    {
                        rawBoolValue = !rawBoolValue;
                    }
                    changed = Set(boolCondition.BoolValue, rawBoolValue, item => boolCondition.BoolValue = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionValue:
                    if (!TryGetCondition(trigger, conditionIndex, out var numericCondition, out diagnostic))
                        return false;
                    if (!IsFinite(value.FloatValue))
                    {
                        diagnostic = "条件数值必须是有限数字。";
                        return false;
                    }
                    changed = Set(numericCondition.Value, value.FloatValue, item => numericCondition.Value = item);
                    if (numericCondition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between
                        && numericCondition.MaxValue < numericCondition.Value)
                    {
                        numericCondition.MaxValue = numericCondition.Value;
                        changed = true;
                    }
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionMaxValue:
                    if (!TryGetCondition(trigger, conditionIndex, out var maximumCondition, out diagnostic))
                        return false;
                    if (!IsFinite(value.FloatValue) || value.FloatValue < maximumCondition.Value)
                    {
                        diagnostic = "范围最大值必须大于或等于最小值。";
                        return false;
                    }
                    changed = Set(maximumCondition.MaxValue, value.FloatValue, item => maximumCondition.MaxValue = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionRange:
                    if (!TryGetCondition(trigger, conditionIndex, out var rangeCondition, out diagnostic))
                        return false;
                    if (!IsFinite(value.FloatValue)
                        || !IsFinite(value.FloatMaxValue)
                        || value.FloatMaxValue < value.FloatValue)
                    {
                        diagnostic = "范围必须由两个有效数值组成，且最大值不能小于最小值。";
                        return false;
                    }
                    changed = Set(rangeCondition.Value, value.FloatValue, item => rangeCondition.Value = item);
                    changed |= Set(rangeCondition.MaxValue, value.FloatMaxValue, item => rangeCondition.MaxValue = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionAbilityId:
                    if (!TryGetCondition(trigger, conditionIndex, out var abilityCondition, out diagnostic))
                        return false;
                    if (value.IntValue < 0)
                    {
                        diagnostic = "Ability ID 不能小于 0。";
                        return false;
                    }
                    changed = Set(abilityCondition.AbilityId, value.IntValue, item => abilityCondition.AbilityId = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionCharacterActionId:
                    if (!TryGetCondition(trigger, conditionIndex, out var actionCondition, out diagnostic))
                        return false;
                    CharacterActionId actionId = ParseCharacterActionId(value.StringValue);
                    changed = Set(actionCondition.CharacterActionId, actionId, item => actionCondition.CharacterActionId = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.LocalFrameAnchor:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor), value.IntValue))
                    {
                        diagnostic = "时间来源无效。";
                        return false;
                    }
                    CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor anchor =
                        (CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor)value.IntValue;
                    if (anchor == CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd
                        && !HasAuthoritativePrimaryActionEnd(timeline))
                    {
                        diagnostic = "当前主行为没有可解析的结束帧，不能选择“主行为结束帧”。";
                        return false;
                    }
                    changed = Set(trigger.LocalFrameAnchor, anchor, item => trigger.LocalFrameAnchor = item);
                    if (anchor == CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame
                        && trigger.LocalFrame < 1)
                    {
                        trigger.LocalFrame = 1;
                        changed = true;
                    }
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.LocalFrame:
                    int frame = Math.Max(1, Math.Min(36000, value.IntValue));
                    changed = Set(trigger.LocalFrame, frame, item => trigger.LocalFrame = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActionSignal:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal), value.IntValue))
                    {
                        diagnostic = "动作信号无效。";
                        return false;
                    }
                    if (!OwnsActionInstance(timeline))
                    {
                        diagnostic = "当前时间轴的主行为不拥有动作实例，不能监听动作信号。";
                        return false;
                    }
                    CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal signal =
                        (CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal)value.IntValue;
                    changed = Set(trigger.ActionSignal, signal, item => trigger.ActionSignal = item);
                    if (signal == CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Completed)
                        changed |= Set(trigger.ActionSignalReasonCode, 0, item => trigger.ActionSignalReasonCode = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActionSignalReason:
                    changed = Set(trigger.ActionSignalReasonCode, value.IntValue, item => trigger.ActionSignalReasonCode = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActivationStartFrame:
                    changed = Set(trigger.ActivationStartFrame, Math.Max(0, Math.Min(36000, value.IntValue)), item => trigger.ActivationStartFrame = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.WindowEndMode:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicDetectorWindowEndMode), value.IntValue)
                        || (CharacterTestRuntimeAiLabLogicDetectorWindowEndMode)value.IntValue
                            == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.UntilEvent)
                    {
                        diagnostic = "监听窗口模式无效。";
                        return false;
                    }
                    CharacterTestRuntimeAiLabLogicDetectorWindowEndMode mode =
                        (CharacterTestRuntimeAiLabLogicDetectorWindowEndMode)value.IntValue;
                    changed = Set(trigger.WindowEndMode, mode, item => trigger.WindowEndMode = item);
                    if (mode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                        && trigger.ActivationDurationFrames < 1)
                    {
                        trigger.ActivationDurationFrames = 1;
                        changed = true;
                    }
                    else if (mode != CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed)
                    {
                        changed |= Set(trigger.ActivationDurationFrames, 0, item => trigger.ActivationDurationFrames = item);
                    }
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActivationDurationFrames:
                    if (trigger.SourceKind == CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame)
                    {
                        diagnostic = "时间点事件没有可调整的监听宽度。";
                        return false;
                    }
                    changed = Set(
                        trigger.WindowEndMode,
                        CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed,
                        item => trigger.WindowEndMode = item);
                    changed |= Set(trigger.ActivationDurationFrames, Math.Max(1, Math.Min(36000, value.IntValue)), item => trigger.ActivationDurationFrames = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OutcomeKind:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind), value.IntValue))
                    {
                        diagnostic = "触发结果无效。";
                        return false;
                    }
                    CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind outcome =
                        (CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind)value.IntValue;
                    changed = Set(trigger.OutcomeKind, outcome, item => trigger.OutcomeKind = item);
                    if (outcome == CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner)
                        changed |= Set(trigger.TargetTimelineId, string.Empty, item => trigger.TargetTimelineId = item);
                    else if (string.IsNullOrEmpty(trigger.TargetTimelineId))
                        changed |= Set(trigger.TargetTimelineId, timeline.TimelineId, item => trigger.TargetTimelineId = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.TargetTimelineId:
                    if (!ContainsTimeline(payload, value.StringValue))
                    {
                        diagnostic = "目标时间轴不存在。";
                        return false;
                    }
                    changed = Set(trigger.TargetTimelineId, value.StringValue, item => trigger.TargetTimelineId = item);
                    changed |= Set(trigger.OutcomeKind,
                        CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline,
                        item => trigger.OutcomeKind = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Priority:
                    changed = Set(trigger.Priority, value.IntValue, item => trigger.Priority = item);
                    break;
                default:
                    diagnostic = "不支持的分支事件字段。";
                    return false;
            }

            if (!changed)
                return true;
            NormalizeEvent(trigger, timeline);
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildLegacyProjection(payload);
            return true;
        }

        internal static bool TryDelete(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId,
            string eventId,
            out bool changed,
            out string diagnostic)
        {
            changed = false;
            diagnostic = string.Empty;
            if (CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
            {
                string normalizedV6 = NormalizeEventId(eventId);
                for (int timelineIndex = 0; payload != null && timelineIndex < payload.Timelines.Count; timelineIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeTimeline v6Timeline = payload.Timelines[timelineIndex];
                    if (v6Timeline == null || (!string.IsNullOrEmpty(timelineId)
                        && !string.Equals(v6Timeline.TimelineId, timelineId, StringComparison.Ordinal)))
                        continue;
                    for (int eventIndex = 0; eventIndex < v6Timeline.Events.Count; eventIndex++)
                    {
                        if (!string.Equals(v6Timeline.Events[eventIndex]?.EventId, normalizedV6, StringComparison.Ordinal))
                            continue;
                        v6Timeline.Events.RemoveAt(eventIndex);
                        CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.SynchronizeForWrite(payload);
                        changed = true;
                        return true;
                    }
                }
                for (int eventIndex = 0; payload != null && eventIndex < payload.LegacySharedEvents.Count; eventIndex++)
                {
                    if (!string.Equals(payload.LegacySharedEvents[eventIndex]?.EventId, normalizedV6, StringComparison.Ordinal))
                        continue;
                    payload.LegacySharedEvents.RemoveAt(eventIndex);
                    CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.SynchronizeForWrite(payload);
                    changed = true;
                    return true;
                }
                diagnostic = "所选事件已不存在。";
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(payload);
            string normalized = NormalizeEventId(eventId);
            for (int timelineIndex = 0; payload != null && timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[timelineIndex];
                if (timeline == null || (!string.IsNullOrEmpty(timelineId)
                    && !string.Equals(timeline.TimelineId, timelineId, StringComparison.Ordinal)))
                    continue;
                for (int eventIndex = 0; eventIndex < timeline.TriggerEvents.Count; eventIndex++)
                {
                    if (!string.Equals(timeline.TriggerEvents[eventIndex]?.EventId, normalized, StringComparison.Ordinal))
                        continue;
                    timeline.TriggerEvents.RemoveAt(eventIndex);
                    changed = true;
                    CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildLegacyProjection(payload);
                    return true;
                }
            }
            for (int eventIndex = 0; payload != null && eventIndex < payload.LegacySharedTriggerEvents.Count; eventIndex++)
            {
                if (!string.Equals(payload.LegacySharedTriggerEvents[eventIndex]?.EventId, normalized, StringComparison.Ordinal))
                    continue;
                payload.LegacySharedTriggerEvents.RemoveAt(eventIndex);
                changed = true;
                CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildLegacyProjection(payload);
                return true;
            }
            diagnostic = "所选分支事件已不存在。";
            return false;
        }

        private static bool TryApplyV6(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId,
            string eventId,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field,
            int conditionIndex,
            in CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue value,
            out bool changed,
            out string diagnostic)
        {
            changed = false;
            diagnostic = string.Empty;
            string normalizedEventId = NormalizeEventId(eventId);
            bool conditionOwner = TryCreateV6ConditionOwnerProxy(
                payload,
                timelineId,
                normalizedEventId,
                out var timeline,
                out var evt);
            if (!conditionOwner
                && !TryFindV6(payload, timelineId, normalizedEventId, out timeline, out evt))
            {
                diagnostic = "所选事件已不存在，请重新选择。";
                return false;
            }
            var leaves = new System.Collections.Generic.List<CharacterTestRuntimeAiLabBehaviorRecipeConditionNode>();
            CollectV6Leaves(evt.ConditionRoot, leaves);
            CharacterTestRuntimeAiLabCardCondition condition = conditionIndex >= 0 && conditionIndex < leaves.Count
                ? leaves[conditionIndex].Predicate
                : null;
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode conditionNode =
                conditionIndex >= 0 && conditionIndex < leaves.Count ? leaves[conditionIndex] : null;
            switch (field)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.TimelineTitle:
                    if (!conditionOwner
                        || timeline == null
                        || !string.Equals(
                            normalizedEventId,
                            TimelineEntryConditionOwnerId,
                            StringComparison.Ordinal))
                    {
                        diagnostic = "只有时间轴属性可以修改时间轴名称。";
                        return false;
                    }
                    string normalizedTitle = value.StringValue.Trim();
                    if (string.IsNullOrEmpty(normalizedTitle))
                    {
                        diagnostic = "时间轴名称不能为空。";
                        return false;
                    }
                    changed = Set(timeline.Title, normalizedTitle, item => timeline.Title = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.SetEntryTimeline:
                    if (!conditionOwner
                        || timeline == null
                        || !string.Equals(
                            normalizedEventId,
                            TimelineEntryConditionOwnerId,
                            StringComparison.Ordinal))
                    {
                        diagnostic = "只有时间轴属性可以设置行为入口。";
                        return false;
                    }
                    changed = Set(
                        payload.EntryTimelineId,
                        timeline.TimelineId,
                        item => payload.EntryTimelineId = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title:
                    if (conditionOwner) { diagnostic = "条件属性没有事件名称。"; return false; }
                    changed = Set(evt.Title, value.StringValue.Trim(), item => evt.Title = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddCondition:
                    AddV6Condition(evt, value.StringValue);
                    changed = true;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddSubjectCondition:
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ReplaceSubjectCondition:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardConditionFact), value.IntValue))
                    {
                        diagnostic = "条件事实无效。";
                        return false;
                    }
                    CharacterTestRuntimeAiLabCardConditionFact addedFact =
                        (CharacterTestRuntimeAiLabCardConditionFact)value.IntValue;
                    if (!CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                            addedFact,
                            out CharacterTestRuntimeAiLabBehaviorConditionDescriptor addedDescriptor))
                    {
                        diagnostic = "条件事实尚未登记对象与属性信息。";
                        return false;
                    }
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNode addedNode =
                        CreateV10FactCondition(evt, addedFact, value);
                    if (addedFact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                        && !addedNode.Predicate.CharacterActionId.IsValid)
                    {
                        diagnostic = "请选择角色动作后再添加条件。";
                        return false;
                    }
                    if (addedFact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable
                        && addedNode.Predicate.AbilityId <= 0)
                    {
                        diagnostic = "请选择能力后再添加条件。";
                        return false;
                    }
                    if (!addedDescriptor.IsSubjectAvailability)
                        addedNode = WrapInSubjectScope(addedNode, addedDescriptor.Subject);
                    if (field == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ReplaceSubjectCondition)
                    {
                        if (conditionNode == null)
                        {
                            diagnostic = "所选条件已不存在。";
                            return false;
                        }
                        CharacterTestRuntimeAiLabBehaviorRecipeConditionNode replacementLeaf =
                            addedNode.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                                ? addedNode.Children[0]
                                : addedNode;
                        replacementLeaf.NodeId = conditionNode.NodeId;
                        replacementLeaf.Predicate.ConditionId = conditionNode.Predicate?.ConditionId
                            ?? replacementLeaf.Predicate.ConditionId;
                        evt.ConditionRoot = ReplaceV10ConditionNode(
                            evt.ConditionRoot,
                            conditionNode.NodeId,
                            addedNode);
                    }
                    else
                    {
                        AddSubjectAwareNode(evt, addedNode, value.StringValue);
                    }
                    changed = true;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.RemoveCondition:
                    if (conditionIndex < 0 || conditionIndex >= leaves.Count)
                    {
                        diagnostic = "所选条件已不存在。";
                        return false;
                    }
                    evt.ConditionRoot = RemoveV6Node(evt.ConditionRoot, leaves[conditionIndex].NodeId);
                    changed = true;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.RemoveConditionNode:
                    if (string.IsNullOrWhiteSpace(value.StringValue)
                        || FindV6Node(evt.ConditionRoot, value.StringValue) == null)
                    {
                        diagnostic = "所选条件节点已不存在。";
                        return false;
                    }
                    evt.ConditionRoot = RemoveV6Node(evt.ConditionRoot, value.StringValue);
                    changed = true;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.MoveConditionNode:
                    if (string.IsNullOrWhiteSpace(value.StringValue)
                        || value.IntValue == 0
                        || !TryMoveV6Node(evt.ConditionRoot, value.StringValue, value.IntValue < 0 ? -1 : 1))
                    {
                        diagnostic = "该条件节点在当前条件组中不能继续移动。";
                        return false;
                    }
                    changed = true;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.PredicateGroupMode:
                    if (evt.ConditionRoot == null)
                    {
                        diagnostic = "请先添加条件。";
                        return false;
                    }
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind groupKind = value.IntValue
                        == (int)CharacterTestRuntimeAiLabBehaviorRecipePredicateGroupMode.AnyConditionDoesNotMatch
                            ? CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any
                            : CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All;
                    changed = Set(evt.ConditionRoot.Kind, groupKind, item => evt.ConditionRoot.Kind = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddAllGroup:
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddAnyGroup:
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddNotGroup:
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind addedGroupKind =
                        field == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddAllGroup
                            ? CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                            : field == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddAnyGroup
                                ? CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any
                                : CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not;
                    if (string.IsNullOrWhiteSpace(value.StringValue))
                    {
                        evt.ConditionRoot = WrapV6Condition(
                            evt,
                            evt.ConditionRoot,
                            addedGroupKind);
                    }
                    else
                    {
                        AddV6Node(evt, CreateV6ConditionGroup(evt, addedGroupKind), value.StringValue);
                    }
                    changed = true;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddEventSignal:
                    if (conditionOwner) { diagnostic = "行为启用条件和时间轴进入条件只能引用事实。"; return false; }
                    string sourceEventId = FirstOtherEventId(timeline, evt.EventId);
                    if (string.IsNullOrEmpty(sourceEventId))
                    {
                        diagnostic = "当前时间轴没有可引用的其他事件。";
                        return false;
                    }
                    AddV6Node(evt, new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                    {
                        NodeId = "condition.signal." + Guid.NewGuid().ToString("N"),
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal,
                        SourceEventId = sourceEventId,
                        Signal = CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Completed
                    }, value.StringValue);
                    changed = true;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.SetConditionGroupKind:
                    if (!Enum.IsDefined(
                            typeof(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind),
                            value.IntValue))
                    {
                        diagnostic = "条件组组合方式无效。";
                        return false;
                    }
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNode groupNode =
                        FindV6Node(evt.ConditionRoot, value.StringValue);
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind targetGroupKind =
                        (CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind)value.IntValue;
                    if (groupNode == null
                        || groupNode.Kind is not (CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                            or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any
                            or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not)
                        || targetGroupKind is not (CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                            or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any
                            or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not))
                    {
                        diagnostic = "所选条件组已不存在。";
                        return false;
                    }
                    if (targetGroupKind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not
                        && groupNode.Children.Count != 1)
                    {
                        diagnostic = "“非”组只能包含一个条件或条件组。";
                        return false;
                    }
                    changed = Set(groupNode.Kind, targetGroupKind, item => groupNode.Kind = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionSignalSourceEventId:
                    if (conditionOwner) { diagnostic = "行为启用条件和时间轴进入条件不能引用 Event 结果。"; return false; }
                    if (conditionNode?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal
                        || !ContainsOtherTimelineEvent(timeline, evt.EventId, value.StringValue))
                    {
                        diagnostic = "事件结果条件必须引用当前时间轴中的另一个事件。";
                        return false;
                    }
                    changed = Set(conditionNode.SourceEventId, value.StringValue,
                        item => conditionNode.SourceEventId = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionSignalKind:
                    if (conditionNode?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal
                        || !Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeEventSignal), value.IntValue))
                    {
                        diagnostic = "事件运行结果无效。";
                        return false;
                    }
                    changed = Set(conditionNode.Signal,
                        (CharacterTestRuntimeAiLabBehaviorRecipeEventSignal)value.IntValue,
                        item => conditionNode.Signal = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionFact:
                    if (condition == null || !Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardConditionFact), value.IntValue))
                    {
                        diagnostic = "条件事实无效。";
                        return false;
                    }
                    CharacterTestRuntimeAiLabCardConditionFact fact = (CharacterTestRuntimeAiLabCardConditionFact)value.IntValue;
                    if (condition.Fact != fact)
                    {
                        condition.Fact = fact;
                        NormalizeConditionForFact(condition);
                        changed = true;
                    }
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionOperator:
                    if (condition == null || !TryApplyV6Operator(condition, value.IntValue, out changed, out diagnostic))
                        return false;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionBoolValue:
                    if (condition == null) { diagnostic = "所选条件已不存在。"; return false; }
                    changed = Set(condition.BoolValue, value.BoolValue, item => condition.BoolValue = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionValue:
                    if (condition == null || !IsFinite(value.FloatValue)) { diagnostic = "条件数值无效。"; return false; }
                    changed = Set(condition.Value, value.FloatValue, item => condition.Value = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionMaxValue:
                    if (condition == null || !IsFinite(value.FloatValue) || value.FloatValue < condition.Value) { diagnostic = "范围最大值无效。"; return false; }
                    changed = Set(condition.MaxValue, value.FloatValue, item => condition.MaxValue = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionRange:
                    if (condition == null
                        || !IsFinite(value.FloatValue)
                        || !IsFinite(value.FloatMaxValue)
                        || value.FloatMaxValue < value.FloatValue)
                    {
                        diagnostic = "范围必须由两个有效数值组成，且最大值不能小于最小值。";
                        return false;
                    }
                    changed = Set(condition.Value, value.FloatValue, item => condition.Value = item);
                    changed |= Set(condition.MaxValue, value.FloatMaxValue, item => condition.MaxValue = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionAbilityId:
                    if (condition == null || value.IntValue < 0) { diagnostic = "Ability ID 无效。"; return false; }
                    changed = Set(condition.AbilityId, value.IntValue, item => condition.AbilityId = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionCharacterActionId:
                    if (condition == null) { diagnostic = "所选条件已不存在。"; return false; }
                    CharacterActionId conditionActionId = ParseCharacterActionId(value.StringValue);
                    if (!conditionActionId.IsValid)
                    {
                        diagnostic = "请选择角色动作。";
                        return false;
                    }
                    changed = Set(condition.CharacterActionId, conditionActionId, item => condition.CharacterActionId = item);
                    changed |= Set(
                        condition.Fact,
                        CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction,
                        item => condition.Fact = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionTargetStateMasks:
                    if (condition == null
                        || condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetState)
                    {
                        diagnostic = "所选条件不是目标状态。";
                        return false;
                    }
                    if (!TryValidateTargetStateMasks(
                            value.AllowedTargetPhases,
                            value.RequiredTargetFlags,
                            value.ForbiddenTargetFlags,
                            out diagnostic))
                    {
                        return false;
                    }
                    bool includesNoTarget = (value.AllowedTargetPhases
                        & (uint)CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget) != 0u;
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNode enclosingScope =
                        FindEnclosingSubjectScope(evt.ConditionRoot, conditionNode.NodeId);
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNode normalizedRoot = evt.ConditionRoot;
                    if (includesNoTarget && enclosingScope != null)
                    {
                        if (enclosingScope.Subject
                            != CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget)
                        {
                            diagnostic = "目标状态不属于当前对象作用域。";
                            return false;
                        }
                        if (!TrySplitTargetScope(
                                enclosingScope.Children.Count == 1 ? enclosingScope.Children[0] : null,
                                conditionNode.NodeId,
                                enclosingScope.NodeId,
                                out CharacterTestRuntimeAiLabBehaviorRecipeConditionNode split,
                                out diagnostic))
                        {
                            return false;
                        }
                        normalizedRoot = ReplaceV10ConditionNode(
                            evt.ConditionRoot,
                            enclosingScope.NodeId,
                            split);
                    }
                    else if (!includesNoTarget && enclosingScope == null)
                    {
                        if (!TryRestoreSplitTargetScope(
                                evt.ConditionRoot,
                                conditionNode.NodeId,
                                out normalizedRoot))
                        {
                            normalizedRoot = ReplaceV10ConditionNode(
                                evt.ConditionRoot,
                                conditionNode.NodeId,
                                WrapInSubjectScope(
                                    conditionNode,
                                    CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget));
                        }
                    }
                    changed = Set(
                        condition.AllowedTargetPhases,
                        value.AllowedTargetPhases,
                        item => condition.AllowedTargetPhases = item);
                    changed |= Set(
                        condition.RequiredTargetFlags,
                        value.RequiredTargetFlags,
                        item => condition.RequiredTargetFlags = item);
                    changed |= Set(
                        condition.ForbiddenTargetFlags,
                        value.ForbiddenTargetFlags,
                        item => condition.ForbiddenTargetFlags = item);
                    if (!ReferenceEquals(normalizedRoot, evt.ConditionRoot))
                    {
                        evt.ConditionRoot = normalizedRoot;
                        changed = true;
                    }
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.LocalFrame:
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActivationStartFrame:
                    if (conditionOwner) { diagnostic = "条件属性没有时间区间。"; return false; }
                    changed = Set(evt.StartFrame, Math.Max(0, Math.Min(36000, value.IntValue)), item => evt.StartFrame = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.DurationMode:
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.WindowEndMode:
                    diagnostic = "v8 Event 使用显式时间类别，请选择单帧事件或持续事件。";
                    return false;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.TimingKind:
                    if (conditionOwner) { diagnostic = "条件属性没有时间类别。"; return false; }
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind), value.IntValue))
                    {
                        diagnostic = "事件时间类别无效。";
                        return false;
                    }
                    CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind timingKind =
                        (CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind)value.IntValue;
                    if (evt.TimingKind == timingKind)
                        break;
                    evt.Operation ??= new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation();
                    bool clearIncompatibleOperation =
                        !CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.IsOperationCompatible(
                            timingKind,
                            evt.Operation.Kind);
                    evt.TimingKind = timingKind;
                    evt.DurationFrames = timingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                        ? 1
                        : 30;
                    if (timingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant)
                    {
                        evt.MaxExecutionsPerPass = 1;
                        evt.CooldownFrames = 0;
                    }
                    if (clearIncompatibleOperation)
                        evt.Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation();
                    changed = true;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.DurationFrames:
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActivationDurationFrames:
                    if (conditionOwner) { diagnostic = "条件属性没有持续时间。"; return false; }
                    if (evt.TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant)
                    {
                        diagnostic = "单帧事件没有可调整的时长。";
                        return false;
                    }
                    int minimumDuration = 1;
                    int duration = evt.Operation?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition
                        ? 1
                        : Math.Max(minimumDuration, Math.Min(36000, value.IntValue));
                    changed = Set(evt.DurationFrames, duration, item => evt.DurationFrames = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.MaxExecutionsPerEntry:
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.MaxExecutionsPerPass:
                    if (conditionOwner) { diagnostic = "条件属性没有执行次数。"; return false; }
                    if (evt.TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant)
                    {
                        diagnostic = "单帧事件每轮只采样一次。";
                        return false;
                    }
                    if (evt.Operation?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion)
                        changed = Set(evt.Operation.MaxLoopRepeats, Math.Max(0, value.IntValue), item => evt.Operation.MaxLoopRepeats = item);
                    else
                        changed = Set(evt.MaxExecutionsPerPass, Math.Max(0, value.IntValue), item => evt.MaxExecutionsPerPass = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.MaxLoopRepeats:
                    if (conditionOwner) { diagnostic = "条件属性没有循环次数。"; return false; }
                    evt.Operation ??= new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation();
                    changed = Set(evt.Operation.MaxLoopRepeats, Math.Max(0, value.IntValue), item => evt.Operation.MaxLoopRepeats = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConfirmDuration:
                    if (conditionOwner) { diagnostic = "条件属性没有事件区间。"; return false; }
                    changed = Set(evt.DurationReviewRequired, false, item => evt.DurationReviewRequired = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.CooldownFrames:
                    if (conditionOwner) { diagnostic = "条件属性没有执行冷却。"; return false; }
                    if (evt.TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant)
                    {
                        diagnostic = "单帧事件不使用冷却。";
                        return false;
                    }
                    changed = Set(evt.CooldownFrames, Math.Max(0, value.IntValue), item => evt.CooldownFrames = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Priority:
                    if (conditionOwner) { diagnostic = "条件属性没有事件优先级。"; return false; }
                    changed = Set(evt.Priority, value.IntValue, item => evt.Priority = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationKind:
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OutcomeKind:
                    if (conditionOwner) { diagnostic = "条件属性不执行 Event 操作。"; return false; }
                    CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind operationKind = field == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OutcomeKind
                        ? value.IntValue == (int)CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline
                            ? CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
                            : CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner
                        : (CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind)value.IntValue;
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind), operationKind)) { diagnostic = "事件操作无效。"; return false; }
                    if (operationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                    {
                        diagnostic = "角色动作只能通过“添加角色动作事件”创建。";
                        return false;
                    }
                    if (operationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Ability)
                    {
                        diagnostic = "行为配方已不支持 Ability 事件；请添加其绑定的角色动作事件。";
                        return false;
                    }
                    if (operationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Hold)
                    {
                        diagnostic = "行为配方已不支持 Hold 事件。";
                        return false;
                    }
                    if (!CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.IsOperationCompatible(evt.TimingKind, operationKind))
                    {
                        diagnostic = "该操作不支持当前时间类别。";
                        return false;
                    }
                    evt.Operation ??= new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation();
                    changed = Set(evt.Operation.Kind, operationKind, item => evt.Operation.Kind = item);
                    if (operationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition)
                        changed |= Set(evt.DurationFrames, 1, item => evt.DurationFrames = item);
                    else if (operationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion
                        && evt.DurationFrames < 2)
                    {
                        evt.DurationFrames = 2;
                        changed = true;
                    }
                    if (operationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
                        && string.IsNullOrEmpty(evt.Operation.TargetTimelineId))
                    {
                        evt.Operation.TargetTimelineId = timeline.TimelineId;
                        changed = true;
                    }
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationAbilityId:
                    diagnostic = "行为配方已不支持 Ability 事件；请添加其绑定的角色动作事件。";
                    return false;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationCharacterActionId:
                    if (conditionOwner) { diagnostic = "条件属性不执行 Event 操作。"; return false; }
                    if (evt.Operation?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                    {
                        diagnostic = "普通事件不能转换为角色动作事件；请使用专用创建入口。";
                        return false;
                    }
                    CharacterActionId operationActionId = ParseCharacterActionId(value.StringValue);
                    if (!operationActionId.IsValid)
                    {
                        diagnostic = "请选择角色动作。";
                        return false;
                    }
                    changed = Set(evt.Operation.CharacterActionId, operationActionId, item => evt.Operation.CharacterActionId = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConvertCharacterActionEvent:
                    if (conditionOwner) { diagnostic = "条件属性不是角色动作事件。"; return false; }
                    if (evt.Operation?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                    {
                        diagnostic = "当前事件不是角色动作事件。";
                        return false;
                    }
                    if (evt.ConditionRoot != null)
                    {
                        evt.ConditionRoot = null;
                        changed = true;
                    }
                    changed |= Set(evt.TimingKind, CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration, item => evt.TimingKind = item);
                    changed |= Set(evt.MaxExecutionsPerPass, 1, item => evt.MaxExecutionsPerPass = item);
                    changed |= Set(evt.CooldownFrames, 0, item => evt.CooldownFrames = item);
                    changed |= Set(evt.DurationReviewRequired, false, item => evt.DurationReviewRequired = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationMoveMinDistance:
                    if (conditionOwner) { diagnostic = "条件属性不执行 Event 操作。"; return false; }
                    if (!IsFinite(value.FloatValue) || value.FloatValue < 0f) { diagnostic = "最小距离无效。"; return false; }
                    evt.Operation ??= new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation();
                    changed = Set(evt.Operation.MoveMinDistance, value.FloatValue, item => evt.Operation.MoveMinDistance = item);
                    changed |= Set(evt.Operation.Kind, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget, item => evt.Operation.Kind = item);
                    if (evt.Operation.MoveMaxDistance < evt.Operation.MoveMinDistance)
                    {
                        evt.Operation.MoveMaxDistance = evt.Operation.MoveMinDistance;
                        changed = true;
                    }
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationMoveMaxDistance:
                    if (conditionOwner) { diagnostic = "条件属性不执行 Event 操作。"; return false; }
                    evt.Operation ??= new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation();
                    if (!IsFinite(value.FloatValue) || value.FloatValue < evt.Operation.MoveMinDistance) { diagnostic = "最大距离无效。"; return false; }
                    changed = Set(evt.Operation.MoveMaxDistance, value.FloatValue, item => evt.Operation.MoveMaxDistance = item);
                    changed |= Set(evt.Operation.Kind, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget, item => evt.Operation.Kind = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationMoveSpeedMultiplier:
                    if (conditionOwner) { diagnostic = "条件属性不执行 Event 操作。"; return false; }
                    if (!IsFinite(value.FloatValue)
                        || value.FloatValue < CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.MinimumMoveSpeedMultiplier
                        || value.FloatValue > CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.MaximumMoveSpeedMultiplier)
                    {
                        diagnostic = "速度倍率必须在 0.1 到 1.0 之间。";
                        return false;
                    }
                    evt.Operation ??= new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation();
                    changed = Set(evt.Operation.MoveSpeedMultiplier, value.FloatValue, item => evt.Operation.MoveSpeedMultiplier = item);
                    changed |= Set(evt.Operation.Kind, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget, item => evt.Operation.Kind = item);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.TargetTimelineId:
                    if (conditionOwner) { diagnostic = "条件属性不执行 Event 操作。"; return false; }
                    if (!ContainsTimeline(payload, value.StringValue)) { diagnostic = "目标时间轴不存在。"; return false; }
                    evt.Operation ??= new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation();
                    changed = Set(evt.Operation.TargetTimelineId, value.StringValue, item => evt.Operation.TargetTimelineId = item);
                    changed |= Set(evt.Operation.Kind, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline, item => evt.Operation.Kind = item);
                    break;
                default:
                    diagnostic = "该字段尚未接入统一 Event。";
                    return false;
            }
            if (changed && conditionOwner)
            {
                if (string.Equals(normalizedEventId, ActivationConditionOwnerId, StringComparison.Ordinal))
                    payload.ActivationConditionRoot = evt.ConditionRoot;
                else
                    timeline.EntryConditionRoot = evt.ConditionRoot;
            }
            if (changed)
                CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(payload);
            return true;
        }

        private static bool TryCreateV6ConditionOwnerProxy(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId,
            string ownerId,
            out CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            out CharacterTestRuntimeAiLabBehaviorRecipeEvent proxy)
        {
            timeline = null;
            proxy = null;
            if (string.Equals(ownerId, ActivationConditionOwnerId, StringComparison.Ordinal))
            {
                proxy = new CharacterTestRuntimeAiLabBehaviorRecipeEvent
                {
                    EventId = ActivationConditionOwnerId,
                    ConditionRoot = payload?.ActivationConditionRoot
                };
                return payload != null;
            }
            if (!string.Equals(ownerId, TimelineEntryConditionOwnerId, StringComparison.Ordinal))
                return false;
            for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
            {
                if (!string.Equals(payload.Timelines[i]?.TimelineId, timelineId, StringComparison.Ordinal))
                    continue;
                timeline = payload.Timelines[i];
                proxy = new CharacterTestRuntimeAiLabBehaviorRecipeEvent
                {
                    EventId = TimelineEntryConditionOwnerId,
                    ConditionRoot = timeline.EntryConditionRoot
                };
                return true;
            }
            return false;
        }

        private static bool TryFindV6(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload, string timelineId, string eventId, out CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline, out CharacterTestRuntimeAiLabBehaviorRecipeEvent evt)
        {
            timeline = null; evt = null;
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline selectedTimeline = null;
            for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline candidate = payload.Timelines[i];
                if (candidate == null || (!string.IsNullOrEmpty(timelineId) && !string.Equals(candidate.TimelineId, timelineId, StringComparison.Ordinal))) continue;
                selectedTimeline = candidate;
                for (int j = 0; j < candidate.Events.Count; j++)
                {
                    if (!string.Equals(candidate.Events[j]?.EventId, eventId, StringComparison.Ordinal)) continue;
                    timeline = candidate; evt = candidate.Events[j]; return true;
                }
            }
            for (int i = 0; selectedTimeline != null && payload != null && i < payload.LegacySharedEvents.Count; i++)
            {
                if (!string.Equals(payload.LegacySharedEvents[i]?.EventId, eventId, StringComparison.Ordinal))
                    continue;
                timeline = selectedTimeline;
                evt = payload.LegacySharedEvents[i];
                return true;
            }
            return false;
        }

        private static void CollectV6Leaves(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node, System.Collections.Generic.List<CharacterTestRuntimeAiLabBehaviorRecipeConditionNode> leaves)
        {
            if (node == null) return;
            if (node.Kind is CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate
                or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal) { leaves.Add(node); return; }
            for (int i = 0; i < node.Children.Count; i++) CollectV6Leaves(node.Children[i], leaves);
        }

        private static void AddV6Condition(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            string relativeToNodeId = "")
        {
            AddV6Node(evt, CreateV6FactCondition(evt), relativeToNodeId);
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode CreateV6FactCondition(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt)
        {
            return new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition." + evt.EventId + "." + Guid.NewGuid().ToString("N"),
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = CharacterTestRuntimeAiLabBehaviorConditionCatalog.CreateDefault(
                    CharacterTestRuntimeAiLabCardConditionFact.TargetState,
                    "condition." + Guid.NewGuid().ToString("N"))
            };
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode CreateV10FactCondition(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            CharacterTestRuntimeAiLabCardConditionFact fact,
            in CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue value)
        {
            string conditionId = "condition." + Guid.NewGuid().ToString("N");
            CharacterTestRuntimeAiLabCardCondition condition =
                CharacterTestRuntimeAiLabBehaviorConditionCatalog.CreateDefault(fact, conditionId);
            condition.AbilityId = value.ParameterIntValue;
            if (fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction)
                condition.CharacterActionId = ParseCharacterActionId(value.ParameterStringValue);
            return new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition." + evt.EventId + "." + Guid.NewGuid().ToString("N"),
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = condition
            };
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode WrapInSubjectScope(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode child,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject)
        {
            var scope = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition.scope." + Guid.NewGuid().ToString("N"),
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                Subject = subject
            };
            scope.Children.Add(child);
            return scope;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode FindEnclosingSubjectScope(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            string targetNodeId)
        {
            if (node == null)
                return null;
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                && node.Children.Count == 1
                && ContainsV6Node(node.Children[0], targetNodeId))
            {
                return node;
            }
            for (int i = 0; i < node.Children.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode found =
                    FindEnclosingSubjectScope(node.Children[i], targetNodeId);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static bool TrySplitTargetScope(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            string targetNodeId,
            string sourceScopeId,
            out CharacterTestRuntimeAiLabBehaviorRecipeConditionNode result,
            out string diagnostic)
        {
            result = null;
            diagnostic = string.Empty;
            if (node == null || !ContainsV6Node(node, targetNodeId))
            {
                diagnostic = "目标状态所在作用域已经变化，请重新打开编辑器。";
                return false;
            }
            if (string.Equals(node.NodeId, targetNodeId, StringComparison.Ordinal))
            {
                result = node;
                return true;
            }
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not)
            {
                diagnostic = "目标状态跨越 Not 时不能自动移出作用域，请先调整逻辑树。";
                return false;
            }
            if (node.Kind is not CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                and not CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any)
            {
                diagnostic = "目标状态只能沿 All/Any 逻辑组自动移出作用域。";
                return false;
            }
            int pathIndex = -1;
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (!ContainsV6Node(node.Children[i], targetNodeId))
                    continue;
                pathIndex = i;
                break;
            }
            if (pathIndex < 0
                || !TrySplitTargetScope(
                    node.Children[pathIndex],
                    targetNodeId,
                    sourceScopeId,
                    out CharacterTestRuntimeAiLabBehaviorRecipeConditionNode path,
                    out diagnostic))
            {
                return false;
            }
            node.Children[pathIndex] = path;
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (i == pathIndex)
                    continue;
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode sibling = node.Children[i];
                var wrapper = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                {
                    NodeId = (sourceScopeId ?? "condition.scope") + ".split."
                        + (sibling?.NodeId ?? i.ToString(CultureInfo.InvariantCulture)),
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                    Subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget
                };
                if (sibling != null)
                    wrapper.Children.Add(sibling);
                node.Children[i] = wrapper;
            }
            result = node;
            return true;
        }

        private static bool TryRestoreSplitTargetScope(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            string targetNodeId,
            out CharacterTestRuntimeAiLabBehaviorRecipeConditionNode result)
        {
            result = node;
            if (node == null || !ContainsV6Node(node, targetNodeId))
                return false;
            if (node.Kind is CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any)
            {
                string sourceScopeId = FindSplitSourceScopeId(node, targetNodeId);
                if (!string.IsNullOrEmpty(sourceScopeId))
                {
                    result = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                    {
                        NodeId = sourceScopeId,
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                        Subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget
                    };
                    result.Children.Add(UnwrapSplitScopes(node, sourceScopeId));
                    return true;
                }
            }
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (!ContainsV6Node(node.Children[i], targetNodeId)
                    || !TryRestoreSplitTargetScope(node.Children[i], targetNodeId, out var restored))
                {
                    continue;
                }
                node.Children[i] = restored;
                result = node;
                return true;
            }
            return false;
        }

        private static string FindSplitSourceScopeId(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode group,
            string targetNodeId)
        {
            string sourceScopeId = string.Empty;
            bool foundSibling = false;
            for (int i = 0; i < group.Children.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode child = group.Children[i];
                if (ContainsV6Node(child, targetNodeId))
                    continue;
                if (child?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                    || child.Subject
                        != CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget
                    || child.Children.Count != 1)
                {
                    return string.Empty;
                }
                int marker = (child.NodeId ?? string.Empty).IndexOf(".split.", StringComparison.Ordinal);
                if (marker <= 0)
                    return string.Empty;
                string candidate = child.NodeId.Substring(0, marker);
                if (foundSibling && !string.Equals(sourceScopeId, candidate, StringComparison.Ordinal))
                    return string.Empty;
                sourceScopeId = candidate;
                foundSibling = true;
            }
            return foundSibling ? sourceScopeId : string.Empty;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode UnwrapSplitScopes(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            string sourceScopeId)
        {
            string prefix = sourceScopeId + ".split.";
            if (node?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                && node.Subject
                    == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget
                && node.Children.Count == 1
                && (node.NodeId ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal))
            {
                return UnwrapSplitScopes(node.Children[0], sourceScopeId);
            }
            for (int i = 0; node != null && i < node.Children.Count; i++)
                node.Children[i] = UnwrapSplitScopes(node.Children[i], sourceScopeId);
            return node;
        }

        private static void AddV6Node(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            string relativeToNodeId = "")
        {
            if (evt.ConditionRoot == null) { evt.ConditionRoot = node; return; }
            if (!string.IsNullOrWhiteSpace(relativeToNodeId))
            {
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode relative =
                    FindV6Node(evt.ConditionRoot, relativeToNodeId);
                if (relative?.Kind is CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                    or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any)
                {
                    relative.Children.Add(node);
                    return;
                }
                if (string.Equals(evt.ConditionRoot.NodeId, relativeToNodeId, StringComparison.Ordinal))
                {
                    evt.ConditionRoot = CreateV6SiblingGroup(evt.ConditionRoot, node);
                    return;
                }
                if (TryInsertV6Sibling(evt.ConditionRoot, relativeToNodeId, node))
                    return;
            }
            if (evt.ConditionRoot.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All) { evt.ConditionRoot.Children.Add(node); return; }
            evt.ConditionRoot = CreateV6SiblingGroup(evt.ConditionRoot, node);
        }

        private static void AddSubjectAwareNode(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            string relativeToNodeId)
        {
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode relative =
                FindV6Node(evt?.ConditionRoot, relativeToNodeId);
            if (relative?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                && node?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                && relative.Subject == node.Subject
                && relative.Children.Count == 1
                && node.Children.Count == 1)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode current = relative.Children[0];
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode added = node.Children[0];
                if (current.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All)
                    current.Children.Add(added);
                else
                    relative.Children[0] = CreateV6SiblingGroup(current, added);
                return;
            }
            AddV6Node(evt, node, relativeToNodeId);
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode ReplaceV10ConditionNode(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            string targetNodeId,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode replacement)
        {
            if (node == null)
                return null;
            if (string.Equals(node.NodeId, targetNodeId, StringComparison.Ordinal))
                return replacement;
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                && node.Children.Count == 1
                && ContainsV6Node(node.Children[0], targetNodeId))
            {
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind replacementSubject =
                    replacement.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                        ? replacement.Subject
                        : CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Unknown;
                if (replacementSubject == node.Subject)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNode replacementLeaf =
                        replacement.Children[0];
                    node.Children[0] = ReplaceV10ConditionNode(
                        node.Children[0], targetNodeId, replacementLeaf);
                    return node;
                }

                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode originalChild = node.Children[0];
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode remaining =
                    RemoveV6Node(originalChild, targetNodeId);
                if (remaining == null)
                    return replacement;
                node.Children[0] = remaining;
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind joinKind =
                    originalChild.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any
                        ? CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any
                        : CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All;
                var joined = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                {
                    NodeId = "condition.group." + Guid.NewGuid().ToString("N"),
                    Kind = joinKind
                };
                joined.Children.Add(node);
                joined.Children.Add(replacement);
                return joined;
            }
            for (int i = 0; i < node.Children.Count; i++)
                node.Children[i] = ReplaceV10ConditionNode(node.Children[i], targetNodeId, replacement);
            return node;
        }

        private static bool ContainsV6Node(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            string nodeId)
        {
            if (node == null)
                return false;
            if (string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
                return true;
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (ContainsV6Node(node.Children[i], nodeId))
                    return true;
            }
            return false;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode CreateV6SiblingGroup(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode first,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode second)
        {
            var group = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition.group." + Guid.NewGuid().ToString("N"),
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
            };
            group.Children.Add(first);
            group.Children.Add(second);
            return group;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode CreateV6ConditionGroup(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind kind)
        {
            var group = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition.group." + Guid.NewGuid().ToString("N"),
                Kind = kind
            };
            group.Children.Add(CreateV6FactCondition(evt));
            if (kind is CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any)
            {
                group.Children.Add(CreateV6FactCondition(evt));
            }
            return group;
        }

        private static bool TryInsertV6Sibling(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode parent,
            string nodeId,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode sibling)
        {
            if (parent == null)
                return false;
            for (int i = 0; i < parent.Children.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode child = parent.Children[i];
                if (string.Equals(child?.NodeId, nodeId, StringComparison.Ordinal))
                {
                    if (parent.Kind is not (CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                        or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any))
                        return false;
                    parent.Children.Insert(i + 1, sibling);
                    return true;
                }
                if (TryInsertV6Sibling(child, nodeId, sibling))
                    return true;
            }
            return false;
        }

        private static string FirstOtherEventId(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            string owningEventId)
        {
            for (int i = 0; timeline != null && i < timeline.Events.Count; i++)
            {
                string candidate = timeline.Events[i]?.EventId ?? string.Empty;
                if (candidate.Length > 0 && !string.Equals(candidate, owningEventId, StringComparison.Ordinal))
                    return candidate;
            }
            return string.Empty;
        }

        private static bool ContainsOtherTimelineEvent(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            string owningEventId,
            string candidateEventId)
        {
            if (string.IsNullOrWhiteSpace(candidateEventId)
                || string.Equals(owningEventId, candidateEventId, StringComparison.Ordinal))
                return false;
            for (int i = 0; timeline != null && i < timeline.Events.Count; i++)
            {
                if (string.Equals(timeline.Events[i]?.EventId, candidateEventId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode WrapV6Condition(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode current,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind kind)
        {
            if (current == null)
            {
                AddV6Condition(evt);
                current = evt.ConditionRoot;
            }
            var group = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition.group." + Guid.NewGuid().ToString("N"),
                Kind = kind
            };
            group.Children.Add(current);
            if (kind is CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any)
            {
                var temporary = new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = evt.EventId };
                AddV6Condition(temporary);
                group.Children.Add(temporary.ConditionRoot);
            }
            return group;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode RemoveV6Node(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node, string nodeId)
        {
            if (node == null || string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)) return null;
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode updated = RemoveV6Node(node.Children[i], nodeId);
                if (updated == null) node.Children.RemoveAt(i); else node.Children[i] = updated;
            }
            if ((node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All || node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any) && node.Children.Count == 1) return node.Children[0];
            return node.Children.Count == 0 && node.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate && node.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal ? null : node;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode FindV6Node(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            string nodeId)
        {
            if (node == null)
                return null;
            if (string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
                return node;
            for (int i = 0; i < node.Children.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode found =
                    FindV6Node(node.Children[i], nodeId);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static bool TryMoveV6Node(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode parent,
            string nodeId,
            int direction)
        {
            if (parent == null)
                return false;
            for (int i = 0; i < parent.Children.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode child = parent.Children[i];
                if (string.Equals(child?.NodeId, nodeId, StringComparison.Ordinal))
                {
                    int target = i + direction;
                    if (target < 0 || target >= parent.Children.Count)
                        return false;
                    parent.Children[i] = parent.Children[target];
                    parent.Children[target] = child;
                    return true;
                }
                if (TryMoveV6Node(child, nodeId, direction))
                    return true;
            }
            return false;
        }

        private static bool TryApplyV6Operator(CharacterTestRuntimeAiLabCardCondition condition, int effectiveOperator, out bool changed, out string diagnostic)
        {
            diagnostic = string.Empty; changed = false;
            CharacterTestRuntimeAiLabCardConditionOperator value;
            switch (effectiveOperator)
            {
                case 0: value = CharacterTestRuntimeAiLabCardConditionOperator.Equals; break;
                case 1: value = CharacterTestRuntimeAiLabCardConditionOperator.NotEquals; break;
                case 2: value = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual; break;
                case 3: value = CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan; break;
                case 4: value = CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual; break;
                case 6: value = CharacterTestRuntimeAiLabCardConditionOperator.Between; break;
                default: diagnostic = "比较方式无效。"; return false;
            }
            changed = Set(condition.Operator, value, item => condition.Operator = item);
            return true;
        }

        private static void NormalizeSource(
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger,
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline)
        {
            trigger.Conditions.Clear();
            trigger.ActionSignalReasonCode = 0;
            trigger.LegacyUnfilteredSignal = false;
            switch (trigger.SourceKind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.Predicate:
                    trigger.Conditions.Add(CreateDefaultCondition(trigger));
                    trigger.ExpectedResult = true;
                    trigger.LocalFrame = 0;
                    trigger.LocalFrameAnchor = CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame:
                    trigger.LocalFrameAnchor = CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame;
                    trigger.LocalFrame = Math.Max(1, trigger.LocalFrame > 0
                        ? trigger.LocalFrame
                        : trigger.ActivationStartFrame);
                    trigger.ActivationStartFrame = trigger.LocalFrame;
                    trigger.ActivationDurationFrames = 0;
                    trigger.WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite;
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal:
                    trigger.ActionSignal = CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Failed;
                    trigger.ActionSignalReasonCode = OwnsActionInstance(timeline)
                        ? CharacterTestAiTraceReason.RUNTIME_ACTION_FAILED
                        : 0;
                    trigger.LocalFrame = 0;
                    break;
            }
        }

        private static void NormalizeEvent(
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger,
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline)
        {
            trigger.TimelineId = trigger.ClockOrigin == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe
                ? trigger.TimelineId
                : timeline?.TimelineId ?? trigger.TimelineId;
            if (trigger.SourceKind == CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame)
            {
                trigger.LocalFrame = Math.Max(1, trigger.LocalFrame);
                trigger.ActivationStartFrame = trigger.LocalFrame;
                trigger.ActivationDurationFrames = 0;
                trigger.WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite;
            }
            else if (trigger.WindowEndMode != CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed)
            {
                trigger.ActivationDurationFrames = 0;
            }
        }

        private static CharacterTestRuntimeAiLabCardCondition CreateDefaultCondition(
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger)
        {
            int suffix = trigger.Conditions.Count + 1;
            string conditionId;
            do
            {
                conditionId = "condition." + trigger.EventId + "."
                    + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }
            while (ContainsConditionId(trigger, conditionId));
            return new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = conditionId,
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true
            };
        }

        private static bool ContainsConditionId(
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger,
            string conditionId)
        {
            for (int i = 0; i < trigger.Conditions.Count; i++)
                if (string.Equals(trigger.Conditions[i]?.ConditionId, conditionId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool TryApplyEffectiveOperator(
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger,
            CharacterTestRuntimeAiLabCardCondition condition,
            int effectiveOperator,
            out bool changed,
            out string diagnostic)
        {
            changed = false;
            diagnostic = string.Empty;
            // 0 =, 1 !=, 2 <=, 3 >, 4 >=, 5 <, 6 range, 7 outside range.
            bool preserveBooleanValue = trigger.Conditions.Count == 1
                && IsBooleanFact(condition.Fact);
            bool effectiveBooleanValue = condition.BoolValue;
            if (preserveBooleanValue && !trigger.ExpectedResult
                && condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals)
            {
                effectiveBooleanValue = !effectiveBooleanValue;
            }

            CharacterTestRuntimeAiLabCardConditionOperator raw;
            bool expected = true;
            switch (effectiveOperator)
            {
                case 0: raw = CharacterTestRuntimeAiLabCardConditionOperator.Equals; break;
                case 1: raw = CharacterTestRuntimeAiLabCardConditionOperator.NotEquals; break;
                case 2: raw = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual; break;
                case 3: raw = CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan; break;
                case 4: raw = CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual; break;
                case 5:
                    raw = CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual;
                    expected = false;
                    break;
                case 6: raw = CharacterTestRuntimeAiLabCardConditionOperator.Between; break;
                case 7:
                    raw = CharacterTestRuntimeAiLabCardConditionOperator.Between;
                    expected = false;
                    break;
                default:
                    diagnostic = "比较方式无效。";
                    return false;
            }
            if (trigger.Conditions.Count > 1 && expected != trigger.ExpectedResult)
            {
                diagnostic = "多条件事件使用统一组规则，不能只反转其中一个条件。";
                return false;
            }
            changed = Set(condition.Operator, raw, item => condition.Operator = item);
            if (trigger.Conditions.Count == 1)
                changed |= Set(trigger.ExpectedResult, expected, item => trigger.ExpectedResult = item);
            if (preserveBooleanValue)
                changed |= Set(condition.BoolValue, effectiveBooleanValue, item => condition.BoolValue = item);
            return true;
        }

        private static bool IsBooleanFact(CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            return fact != CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                && fact != CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance
                && fact != CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames
                && fact != CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable
                && fact != CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction;
        }

        private static bool TryGetCondition(
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger,
            int index,
            out CharacterTestRuntimeAiLabCardCondition condition,
            out string diagnostic)
        {
            condition = index >= 0 && index < trigger.Conditions.Count ? trigger.Conditions[index] : null;
            diagnostic = condition == null ? "所选条件已不存在。" : string.Empty;
            return condition != null;
        }

        private static void NormalizeConditionForFact(CharacterTestRuntimeAiLabCardCondition condition)
        {
            condition.Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals;
            condition.BoolValue = true;
            condition.Value = 0f;
            condition.MaxValue = 0f;
            condition.AbilityId = 0;
            condition.CharacterActionId = default;
            condition.AllowedTargetPhases = condition.Fact
                    == CharacterTestRuntimeAiLabCardConditionFact.TargetState
                ? (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive
                : 0u;
            condition.RequiredTargetFlags = 0u;
            condition.ForbiddenTargetFlags = 0u;
            if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                || condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance)
            {
                condition.Operator = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual;
                condition.Value = 1.5f;
            }
            else if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames)
            {
                condition.Operator = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual;
                condition.Value = 10f;
            }
        }

        private static bool TryValidateTargetStateMasks(
            uint phases,
            uint required,
            uint forbidden,
            out string diagnostic)
        {
            uint phaseMask = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.PhaseMask;
            uint flagMask = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.FlagMask;
            if (phases == 0u || (phases & ~phaseMask) != 0u)
            {
                diagnostic = "目标状态必须选择至少一个有效阶段。";
                return false;
            }
            if ((required & ~flagMask) != 0u || (forbidden & ~flagMask) != 0u)
            {
                diagnostic = "目标状态包含未知状态位。";
                return false;
            }
            if ((required & forbidden) != 0u)
            {
                diagnostic = "同一目标状态不能同时设为必须和排除。";
                return false;
            }
            diagnostic = string.Empty;
            return true;
        }

        private static bool HasAuthoritativePrimaryActionEnd(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline)
        {
            CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent action = timeline?.PrimaryActionEvent;
            if (action == null)
                return false;
            return action.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait
                ? action.WaitFrames > 0
                : (action.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                    || action.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility)
                    && action.FallbackDurationFrames > 0;
        }

        private static bool OwnsActionInstance(CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline)
        {
            CharacterTestRuntimeAiLabCardStepKind kind = timeline?.PrimaryActionEvent?.Kind
                ?? CharacterTestRuntimeAiLabCardStepKind.Logic;
            return kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                || kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility;
        }

        private static bool TryFind(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId,
            string eventId,
            out CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            out CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger)
        {
            timeline = null;
            trigger = null;
            for (int timelineIndex = 0; payload != null && timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline candidate = payload.Timelines[timelineIndex];
                if (candidate == null || (!string.IsNullOrEmpty(timelineId)
                    && !string.Equals(candidate.TimelineId, timelineId, StringComparison.Ordinal)))
                    continue;
                for (int eventIndex = 0; eventIndex < candidate.TriggerEvents.Count; eventIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent candidateEvent =
                        candidate.TriggerEvents[eventIndex];
                    if (!string.Equals(candidateEvent?.EventId, eventId, StringComparison.Ordinal))
                        continue;
                    timeline = candidate;
                    trigger = candidateEvent;
                    return true;
                }
            }
            for (int eventIndex = 0; payload != null && eventIndex < payload.LegacySharedTriggerEvents.Count; eventIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent shared = payload.LegacySharedTriggerEvents[eventIndex];
                if (!string.Equals(shared?.EventId, eventId, StringComparison.Ordinal))
                    continue;
                trigger = shared;
                for (int timelineIndex = 0; timelineIndex < payload.Timelines.Count; timelineIndex++)
                {
                    if (string.IsNullOrEmpty(timelineId)
                        || string.Equals(payload.Timelines[timelineIndex]?.TimelineId, timelineId, StringComparison.Ordinal))
                    {
                        timeline = payload.Timelines[timelineIndex];
                        break;
                    }
                }
                return timeline != null;
            }
            return false;
        }

        private static string NormalizeEventId(string eventId)
        {
            string value = eventId ?? string.Empty;
            return value.StartsWith("event.event.", StringComparison.Ordinal)
                    ? value.Substring("event.".Length)
                    : value;
        }

        private static bool ContainsTimeline(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId)
        {
            for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
                if (string.Equals(payload.Timelines[i]?.TimelineId, timelineId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static CharacterActionId ParseCharacterActionId(string value)
        {
            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed)
                && parsed > 0
                ? new CharacterActionId(parsed)
                : CharacterActionId.None;
        }

        private static bool Set<T>(T current, T value, Action<T> assign)
        {
            if (Equals(current, value))
                return false;
            assign(value);
            return true;
        }
    }
}
