using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class BehaviorRecipeTriggerEventInspectorProjection
    {
        internal static string BuildEffectiveSummary(
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger,
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            IReadOnlyList<BehaviorRecipeTriggerConditionViewModel> conditions = null,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = null)
        {
            string source;
            switch (trigger.SourceKind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.Predicate:
                    conditions ??= BuildConditions(trigger);
                    if (conditions.Count == 0)
                        source = "尚未配置触发条件";
                    else if (conditions.Count == 1)
                        source = conditions[0].EffectiveSummary;
                    else
                        source = trigger.ExpectedResult
                            ? "全部条件满足（" + conditions.Count + "项）"
                            : "任一条件不满足（" + conditions.Count + "项）";
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame:
                    int frame = trigger.LocalFrameAnchor
                        == CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd
                            ? ResolvePrimaryActionEnd(timeline)
                            : Math.Max(1, trigger.LocalFrame);
                    source = trigger.LocalFrameAnchor
                        == CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd
                            ? "主行为结束" + (frame > 0 ? "：" + frame + "f" : string.Empty)
                            : frame + "f";
                    break;
                default:
                    source = trigger.ActionSignal switch
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Completed => "动作完成",
                        CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Rejected => "动作被拒绝",
                        _ => "动作失败"
                    };
                    if (trigger.ActionSignalReasonCode != 0)
                        source += "（" + trigger.ActionSignalReasonCode + "）";
                    break;
            }
            string outcome = trigger.OutcomeKind == CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
                ? "显式退出行为卡"
                : "进入 " + ResolveTimelineName(trigger.TargetTimelineId, timeline, payload);
            return source + " → " + outcome;
        }

        private static BehaviorRecipeTriggerConditionViewModel[] BuildConditions(
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger)
        {
            var rows = new BehaviorRecipeTriggerConditionViewModel[trigger.Conditions.Count];
            bool finalOperator = trigger.Conditions.Count == 1;
            for (int i = 0; i < rows.Length; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = trigger.Conditions[i]
                    ?? new CharacterTestRuntimeAiLabCardCondition();
                BehaviorRecipeTriggerConditionValueKind valueKind = ResolveValueKind(condition);
                bool inverted = finalOperator && !trigger.ExpectedResult;
                int effectiveOperator = ResolveEffectiveOperator(condition.Operator, inverted);
                bool effectiveBoolValue = condition.BoolValue;
                if (valueKind == BehaviorRecipeTriggerConditionValueKind.Boolean && inverted)
                {
                    // The author edits the final predicate, never ExpectedResult.  Canonicalize
                    // !(fact == true) to fact == false instead of exposing "!= true".
                    if (condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals)
                    {
                        effectiveOperator = 0;
                        effectiveBoolValue = !condition.BoolValue;
                    }
                    else if (condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.NotEquals)
                        effectiveOperator = 0;
                }
                string fact = ResolveFactText(condition.Fact);
                string op = ResolveOperatorText(effectiveOperator);
                string value = valueKind == BehaviorRecipeTriggerConditionValueKind.Boolean
                    ? (effectiveBoolValue ? "是" : "否")
                    : ResolveValueText(condition, valueKind);
                rows[i] = new BehaviorRecipeTriggerConditionViewModel
                {
                    ConditionId = condition.ConditionId ?? string.Empty,
                    Fact = condition.Fact,
                    FactText = fact,
                    EffectiveOperator = effectiveOperator,
                    OperatorText = op,
                    ValueKind = valueKind,
                    BoolValue = effectiveBoolValue,
                    Value = condition.Value,
                    MaxValue = condition.MaxValue,
                    AbilityId = condition.AbilityId,
                    CharacterActionId = condition.CharacterActionId,
                    ValueText = value,
                    EffectiveSummary = fact + " " + op + " " + value
                };
            }
            return rows;
        }

        private static BehaviorRecipeTriggerTimelineOptionViewModel[] BuildTimelineOptions(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            var options = new List<BehaviorRecipeTriggerTimelineOptionViewModel>(payload.Timelines.Count);
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                if (timeline == null || string.IsNullOrEmpty(timeline.TimelineId))
                    continue;
                options.Add(new BehaviorRecipeTriggerTimelineOptionViewModel
                {
                    TimelineId = timeline.TimelineId,
                    Order = timeline.Order,
                    DisplayText = (timeline.Order + 1).ToString("00", CultureInfo.InvariantCulture)
                        + " " + (string.IsNullOrWhiteSpace(timeline.Title) ? timeline.TimelineId : timeline.Title)
                });
            }
            options.Sort((left, right) =>
            {
                int byOrder = left.Order.CompareTo(right.Order);
                return byOrder != 0 ? byOrder : string.CompareOrdinal(left.TimelineId, right.TimelineId);
            });
            return options.ToArray();
        }

        private static bool TryResolveSelectedIdentity(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source,
            out string timelineId,
            out string eventId)
        {
            timelineId = string.Empty;
            eventId = string.Empty;
            string key = source.Presentation.SelectedRecipeEventStableKey ?? string.Empty;
            const string detectorToken = ":detector:";
            if (key.StartsWith("event:", StringComparison.Ordinal))
            {
                int split = key.IndexOf(detectorToken, StringComparison.Ordinal);
                if (split > "event:".Length)
                {
                    timelineId = key.Substring("event:".Length, split - "event:".Length);
                    string detectorId = key.Substring(split + detectorToken.Length);
                    eventId = NormalizeEventId(detectorId);
                    return eventId.Length > 0;
                }
            }
            int index = source.SelectedElementIndex;
            if (source.SelectedElementKind != CharacterTestRuntimeAiLabLogicElementKind.Detector
                || index < 0 || index >= source.DetectorCount || index >= source.Detectors.Length)
                return false;
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = source.Detectors[index];
            timelineId = detector?.OwningStageId ?? string.Empty;
            eventId = NormalizeEventId(detector?.DetectorId);
            return eventId.Length > 0;
        }

        private static bool TryFind(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId,
            string eventId,
            out CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            out CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger,
            out bool shared)
        {
            timeline = null;
            trigger = null;
            shared = false;
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline candidate = payload.Timelines[i];
                if (candidate == null || (!string.IsNullOrEmpty(timelineId)
                    && !string.Equals(candidate.TimelineId, timelineId, StringComparison.Ordinal)))
                    continue;
                for (int j = 0; j < candidate.TriggerEvents.Count; j++)
                {
                    if (!string.Equals(candidate.TriggerEvents[j]?.EventId, eventId, StringComparison.Ordinal))
                        continue;
                    timeline = candidate;
                    trigger = candidate.TriggerEvents[j];
                    return true;
                }
            }
            for (int i = 0; i < payload.LegacySharedTriggerEvents.Count; i++)
            {
                if (!string.Equals(payload.LegacySharedTriggerEvents[i]?.EventId, eventId, StringComparison.Ordinal))
                    continue;
                trigger = payload.LegacySharedTriggerEvents[i];
                shared = true;
                for (int j = 0; j < payload.Timelines.Count; j++)
                {
                    if (string.IsNullOrEmpty(timelineId)
                        || string.Equals(payload.Timelines[j]?.TimelineId, timelineId, StringComparison.Ordinal))
                    {
                        timeline = payload.Timelines[j];
                        break;
                    }
                }
                return timeline != null;
            }
            return false;
        }

        private static string ResolveRuntimeState(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState)
        {
            if (timeline.Historical)
                return "历史帧只读";
            string detectorId = "event." + trigger.EventId;
            for (int i = 0; i < timeline.DetectorCount && i < timeline.Detectors.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = timeline.Detectors[i];
                if (detector == null || (!string.Equals(detector.DetectorId, detectorId, StringComparison.Ordinal)
                    && !string.Equals(NormalizeEventId(detector.DetectorId), trigger.EventId, StringComparison.Ordinal)))
                    continue;
                if (detector.Triggered || (runtimeState != null && runtimeState.LastTriggeredEventId == i))
                    return "本帧已触发";
                if (detector.Active || detector.Armed)
                    return "正在监听";
                return runtimeState == null ? "未运行" : "未触发";
            }
            return runtimeState == null ? "未运行" : "未触发";
        }

        private static string BuildValidationText(BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            if (model.SourceKind == CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.Predicate
                && model.Conditions.Length == 0)
                return "尚未配置触发条件；保存校验不会通过。";
            if (model.SourceKind == CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame
                && model.LocalFrameAnchor == CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd
                && !model.CanUsePrimaryActionEnd)
                return "当前主行为没有权威结束帧。";
            if (model.SourceKind == CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal)
            {
                if (!model.CanUseActionSignal)
                    return "当前主行为不拥有动作实例。";
                if (model.ActionSignal != CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Completed
                    && string.IsNullOrWhiteSpace(model.ActionSignalReasonCode))
                    return "动作失败或拒绝必须选择具体原因。";
                if (model.CompatibilitySignal)
                    return "兼容事件：正常、可解析的动作结束建议改用“主行为结束帧”。";
            }
            return model.LegacyShared ? "整卡共享监听：所有时间轴镜像共同编辑这一事件。" : string.Empty;
        }

        private static int ResolveEffectiveOperator(
            CharacterTestRuntimeAiLabCardConditionOperator op,
            bool inverted)
        {
            if (!inverted)
                return op switch
                {
                    CharacterTestRuntimeAiLabCardConditionOperator.Equals => 0,
                    CharacterTestRuntimeAiLabCardConditionOperator.NotEquals => 1,
                    CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual => 2,
                    CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan => 3,
                    CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual => 4,
                    CharacterTestRuntimeAiLabCardConditionOperator.Between => 6,
                    _ => 0
                };
            return op switch
            {
                CharacterTestRuntimeAiLabCardConditionOperator.Equals => 1,
                CharacterTestRuntimeAiLabCardConditionOperator.NotEquals => 0,
                CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual => 3,
                CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan => 2,
                CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual => 5,
                CharacterTestRuntimeAiLabCardConditionOperator.Between => 7,
                _ => 1
            };
        }

        private static BehaviorRecipeTriggerConditionValueKind ResolveValueKind(
            CharacterTestRuntimeAiLabCardCondition condition)
        {
            if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable)
                return BehaviorRecipeTriggerConditionValueKind.Ability;
            if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction)
                return BehaviorRecipeTriggerConditionValueKind.CharacterAction;
            if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetState)
                return BehaviorRecipeTriggerConditionValueKind.TargetState;
            if (condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between)
                return BehaviorRecipeTriggerConditionValueKind.Range;
            if (CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                    condition.Fact,
                    out CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor)
                && descriptor.ValueKind == CharacterTestRuntimeAiLabBehaviorConditionValueKind.Number)
                return BehaviorRecipeTriggerConditionValueKind.Number;
            return BehaviorRecipeTriggerConditionValueKind.Boolean;
        }

        private static string ResolveFactText(CharacterTestRuntimeAiLabCardConditionFact fact) =>
            CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                fact,
                out CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor)
                    ? descriptor.DisplayName
                    : fact.ToString();

        private static string ResolveOperatorText(int op) => op switch
        {
            0 => "=", 1 => "≠", 2 => "≤", 3 => ">", 4 => "≥", 5 => "<",
            6 => "在区间", 7 => "不在区间", _ => "="
        };

        private static string ResolveValueText(
            CharacterTestRuntimeAiLabCardCondition condition,
            BehaviorRecipeTriggerConditionValueKind kind)
        {
            return kind switch
            {
                BehaviorRecipeTriggerConditionValueKind.Boolean => condition.BoolValue ? "是" : "否",
                BehaviorRecipeTriggerConditionValueKind.Range =>
                    condition.Value.ToString("0.###", CultureInfo.InvariantCulture) + " – "
                    + condition.MaxValue.ToString("0.###", CultureInfo.InvariantCulture),
                BehaviorRecipeTriggerConditionValueKind.Ability => "Ability " + condition.AbilityId,
                BehaviorRecipeTriggerConditionValueKind.CharacterAction =>
                    !condition.CharacterActionId.IsValid ? "未选择动作" : condition.CharacterActionId.ToString(),
                BehaviorRecipeTriggerConditionValueKind.TargetState => FormatTargetStateValue(condition),
                _ => condition.Value.ToString("0.###", CultureInfo.InvariantCulture)
                    + (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance ? " 米"
                        : condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle ? "°" : "f")
            };
        }

        private static string BuildTimelineTitle(CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline)
        {
            if (timeline == null)
                return "未知时间轴";
            return (timeline.Order + 1).ToString("00", CultureInfo.InvariantCulture) + " "
                + (string.IsNullOrWhiteSpace(timeline.Title) ? timeline.TimelineId : timeline.Title);
        }

        private static string ResolveTimelineName(
            string timelineId,
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline current,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (string.Equals(current?.TimelineId, timelineId, StringComparison.Ordinal))
                return string.IsNullOrWhiteSpace(current.Title) ? current.TimelineId : current.Title;
            for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
                if (string.Equals(payload.Timelines[i]?.TimelineId, timelineId, StringComparison.Ordinal))
                    return string.IsNullOrWhiteSpace(payload.Timelines[i].Title)
                        ? timelineId : payload.Timelines[i].Title;
            return string.IsNullOrWhiteSpace(timelineId) ? "未选择时间轴" : timelineId;
        }

        private static bool HasAuthoritativePrimaryActionEnd(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline) => ResolvePrimaryActionEnd(timeline) > 0;

        private static int ResolvePrimaryActionEnd(CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline)
        {
            CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent action = timeline?.PrimaryActionEvent;
            if (action == null)
                return 0;
            if (action.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait)
                return Math.Max(0, action.WaitFrames);
            return action.Kind is CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                or CharacterTestRuntimeAiLabCardStepKind.UseAbility
                    ? Math.Max(0, action.FallbackDurationFrames)
                    : 0;
        }

        private static bool OwnsActionInstance(CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline) =>
            timeline?.PrimaryActionEvent?.Kind is CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                or CharacterTestRuntimeAiLabCardStepKind.UseAbility;

        private static string NormalizeEventId(string detectorId)
        {
            string value = detectorId ?? string.Empty;
            return value.StartsWith("event.event.", StringComparison.Ordinal)
                    ? value.Substring("event.".Length)
                    : value;
        }
    }
}
