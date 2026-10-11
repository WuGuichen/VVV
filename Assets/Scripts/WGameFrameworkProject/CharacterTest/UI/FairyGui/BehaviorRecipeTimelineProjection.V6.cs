using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class BehaviorRecipeTimelineProjection
    {
        private void RebuildV6(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            var timelines = new List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(payload.Timelines);
            timelines.Sort((left, right) =>
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return 1;
                if (right == null) return -1;
                int order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : string.CompareOrdinal(left.TimelineId, right.TimelineId);
            });
            string currentTimelineId = source.RuntimeCursor.Available
                ? source.RuntimeCursor.TimelineId
                : string.Empty;
            LocalPlayheadFrame = source.RuntimeCursor.Available
                ? source.RuntimeCursor.LocalFrame
                : -1;

            var groupBuckets = new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>[4];
            for (int g = 0; g < 4; g++)
                groupBuckets[g] = new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>(8);

            for (int timelineIndex = 0; timelineIndex < timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline data = timelines[timelineIndex];
                if (data == null)
                    continue;
                BehaviorRecipeTimelineViewModel timeline = GetOrCreateTimeline(_timelineCount++);
                timeline.ClearTracks();
                timeline.StageId = data.TimelineId ?? string.Empty;
                timeline.Title = string.IsNullOrWhiteSpace(data.Title)
                    ? "时间轴 " + (timelineIndex + 1)
                    : data.Title.Trim();
                timeline.StageIndex = timelineIndex;
                timeline.Order = data.Order;
                timeline.Entry = string.Equals(payload.EntryTimelineId, data.TimelineId, StringComparison.Ordinal);
                timeline.Current = string.Equals(currentTimelineId, data.TimelineId, StringComparison.Ordinal);
                timeline.Selected = false;
                timeline.EntryConditionSummary = BuildV6ConditionSummary(data.EntryConditionRoot);
                timeline.DurationFrames = 0;
                if (timeline.Current)
                    CurrentTimelineIndex = _timelineCount - 1;

                for (int g = 0; g < 4; g++)
                    groupBuckets[g].Clear();

                for (int i = 0; i < data.Events.Count; i++)
                {
                    var ev = data.Events[i];
                    if (ev == null) continue;
                    var group = ResolveEventGroupKind(ev);
                    groupBuckets[(int)group].Add(ev);
                }
                for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
                {
                    var ev = payload.LegacySharedEvents[i];
                    if (ev == null) continue;
                    var group = ResolveEventGroupKind(ev);
                    groupBuckets[(int)group].Add(ev);
                }

                for (int g = 0; g < 4; g++)
                {
                    groupBuckets[g].Sort((left, right) =>
                    {
                        if (ReferenceEquals(left, right)) return 0;
                        if (left == null) return 1;
                        if (right == null) return -1;
                        int start = left.StartFrame.CompareTo(right.StartFrame);
                        if (start != 0) return start;
                        int priority = right.Priority.CompareTo(left.Priority);
                        return priority != 0 ? priority : string.CompareOrdinal(left.EventId, right.EventId);
                    });
                }

                int currentBaseLane = 0;
                for (int g = 0; g < 4; g++)
                {
                    List<CharacterTestRuntimeAiLabBehaviorRecipeEvent> groupEvents = groupBuckets[g];
                    if (groupEvents.Count == 0)
                        continue;

                    BehaviorRecipeEventGroupKind groupKind = (BehaviorRecipeEventGroupKind)g;
                    int groupLaneCount = 0;

                    for (int evIdx = 0; evIdx < groupEvents.Count; evIdx++)
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeEvent ev = groupEvents[evIdx];
                        int startFrame = Math.Max(0, ev.StartFrame);
                        bool characterActionEvent = ev.Operation?.Kind
                            == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction;
                        BehaviorRecipeCharacterActionOptionViewModel characterAction = characterActionEvent
                            ? BehaviorRecipeTriggerEventInspectorProjection.FindCharacterActionOption(
                                source.CharacterActionOptions,
                                ev.Operation?.CharacterActionId ?? CharacterActionId.None)
                            : null;
                        int durationFrames = source.TacticalPreview?.TimingPreview == null && characterAction?.Valid == true
                            ? Math.Max(1, characterAction.DurationFrames)
                            : Math.Max(1, ev.DurationFrames);
                        int effectiveEndFrame = ev.TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                            ? startFrame + Math.Max(InstantVisualSafeSpanFrames, 1)
                            : startFrame + durationFrames;

                        int assignedLane = -1;
                        for (int l = 0; l < groupLaneCount; l++)
                        {
                            int laneCandidate = currentBaseLane + l;
                            if (_laneEndFrames[laneCandidate] <= startFrame)
                            {
                                assignedLane = laneCandidate;
                                break;
                            }
                        }

                        bool ownsBackground = false;
                        if (assignedLane < 0)
                        {
                            assignedLane = currentBaseLane + groupLaneCount;
                            groupLaneCount++;
                            EnsureLaneCapacity(assignedLane + 1);
                            ownsBackground = true;
                        }

                        _laneEndFrames[assignedLane] = effectiveEndFrame;
                        AddV6EventLane(
                            source,
                            timeline,
                            ev,
                            assignedLane,
                            groupKind,
                            durationFrames,
                            ownsBackground);
                    }

                    currentBaseLane += groupLaneCount;
                }
                timeline.LaneCount = Math.Max(1, currentBaseLane);
            }
            StructureKey = CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV8(payload);
        }

        private void AddV6EventLane(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source,
            BehaviorRecipeTimelineViewModel timeline,
            CharacterTestRuntimeAiLabBehaviorRecipeEvent data,
            int laneIndex,
            BehaviorRecipeEventGroupKind groupKind,
            int durationFrames,
            bool ownsBackground)
        {
            if (data == null)
                return;
            int startFrame = Math.Max(0, data.StartFrame);
            bool characterActionEvent = data.Operation?.Kind
                == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction;
            BehaviorRecipeCharacterActionOptionViewModel characterAction = characterActionEvent
                ? BehaviorRecipeTriggerEventInspectorProjection.FindCharacterActionOption(
                    source.CharacterActionOptions,
                    data.Operation?.CharacterActionId ?? CharacterActionId.None)
                : null;
            BehaviorRecipeTrackViewModel lane = GetOrCreateTrack();
            lane.Kind = BehaviorRecipeTrackKind.EventLane;
            lane.GroupKind = groupKind;
            lane.StageIndex = timeline.StageIndex;
            lane.VerticalOrder = laneIndex;
            lane.OwnsLaneBackground = ownsBackground;
            lane.DisplayName = FormatEventGroupDisplayName(groupKind);
            lane.StableKey = "lane:" + timeline.StageId + ":" + data.EventId;

            BehaviorRecipeEventViewModel recipeEvent = GetOrCreateEvent();
            ResetEvent(recipeEvent);
            recipeEvent.GroupKind = groupKind;
            recipeEvent.GroupDisplayName = FormatEventGroupDisplayName(groupKind);
            recipeEvent.StableKey = BuildV6EventStableKey(timeline.StageId, data.EventId);
            recipeEvent.Kind = BehaviorRecipeEventKind.Event;
            recipeEvent.SelectionKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            recipeEvent.SelectionIndex = timeline.StageIndex;
            recipeEvent.StageIndex = timeline.StageIndex;
            recipeEvent.StageId = timeline.StageId;
            recipeEvent.EventId = data.EventId ?? string.Empty;
            recipeEvent.CompiledEventIndex = ResolveCompiledEventIndex(
                source.CompiledRecipeV6,
                timeline.StageId,
                recipeEvent.EventId);
            recipeEvent.StartFrame = startFrame;
            recipeEvent.DurationFrames = durationFrames;
            recipeEvent.TimingKind = data.TimingKind;
            recipeEvent.Priority = data.Priority;
            recipeEvent.OperationKind = data.Operation?.Kind
                ?? CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured;
            recipeEvent.CharacterActionEvent = characterActionEvent;
            recipeEvent.CharacterActionId = data.Operation?.CharacterActionId ?? default;
            recipeEvent.CharacterActionDisplayName = characterAction?.DisplayName ?? string.Empty;
            bool unconfigured = recipeEvent.OperationKind
                == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured;
            recipeEvent.Primitive = data.TimingKind
                    == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                    ? BehaviorRecipeEventPrimitive.Marker
                    : BehaviorRecipeEventPrimitive.Span;
            recipeEvent.CanMove = data.ClockOrigin == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline;
            recipeEvent.CanResize = recipeEvent.CanMove
                && data.TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration
                && !characterActionEvent;
            recipeEvent.ConditionLabel = BuildV6ConditionSummary(data.ConditionRoot);
            recipeEvent.ResultLabel = BuildV6OperationSummary(
                data.Operation,
                source.RecipePayload,
                source.CharacterActionOptions);
            recipeEvent.Title = unconfigured
                ? "新事件 · 未配置操作"
                : characterActionEvent
                    ? "▶ " + (string.IsNullOrWhiteSpace(recipeEvent.CharacterActionDisplayName)
                        ? recipeEvent.CharacterActionId
                        : recipeEvent.CharacterActionDisplayName)
                        + "   " + durationFrames + "f"
                : string.IsNullOrEmpty(recipeEvent.ConditionLabel)
                    ? recipeEvent.ResultLabel
                    : recipeEvent.ConditionLabel + " → " + recipeEvent.ResultLabel;
            recipeEvent.Detail = unconfigured
                ? "请在右侧 Inspector 中选择要执行的操作。"
                : characterActionEvent
                    ? "角色动作事件 · " + recipeEvent.StartFrame + "f · 权威长度 "
                        + durationFrames + "f"
                : data.TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                    ? "单帧事件 · " + recipeEvent.StartFrame + "f"
                : data.MaxExecutionsPerPass == 0
                    ? "无限次 · 冷却 " + data.CooldownFrames + "f"
                    : "最多 " + data.MaxExecutionsPerPass + " 次 · 冷却 " + data.CooldownFrames + "f";
            if (recipeEvent.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion)
                recipeEvent.Detail = data.Operation.MaxLoopRepeats == 0
                    ? "无限循环"
                    : "最多重复 " + data.Operation.MaxLoopRepeats + " 次";
            if (data.ClockOrigin == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe)
                recipeEvent.Detail = "整卡共享 · " + recipeEvent.Detail;
            recipeEvent.Invalid = data.DurationReviewRequired
                || recipeEvent.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured
                || characterActionEvent && (characterAction?.Valid != true
                    || data.ConditionRoot != null
                    || data.TimingKind != CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration
                    || data.MaxExecutionsPerPass != 1
                    || data.CooldownFrames != 0)
                || !CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.IsOperationCompatible(
                    data.TimingKind,
                    recipeEvent.OperationKind);
            recipeEvent.DurationReviewRequired = data.DurationReviewRequired;
            recipeEvent.Selected = string.Equals(
                source.Presentation.SelectedRecipeEventStableKey,
                recipeEvent.StableKey,
                StringComparison.Ordinal);
            ResolveV6Runtime(source, recipeEvent);
            MaximumLocalFrame = Math.Max(MaximumLocalFrame, recipeEvent.StartFrame + recipeEvent.DurationFrames);
            timeline.DurationFrames = Math.Max(
                timeline.DurationFrames,
                recipeEvent.StartFrame + recipeEvent.DurationFrames);
            recipeEvent.TacticalOperationKind = null;
            recipeEvent.TacticalSourceEventId = null;
            recipeEvent.TacticalSourceNodeId = null;
            recipeEvent.SourceHighlighted = false;
            if (!source.InspectCompiledExecution && source.TacticalPreview?.WholeCard is CharacterTestTacticalWholeCardTimeline whole)
            {
                recipeEvent.Title = data.Title;
                recipeEvent.Detail = whole.Describe(data.EventId, data.TimelineId);
                recipeEvent.TacticalOperationKind = whole.OperationKind(data.EventId);
                recipeEvent.TacticalSourceEventId = whole.Location(data.EventId)?.EventId;
                recipeEvent.TacticalSourceNodeId = whole.Location(data.EventId)?.NodeId;
                recipeEvent.SourceHighlighted = whole.Highlighted(data.EventId);
                recipeEvent.Selected = whole.Selected(data.EventId, source.Presentation.SelectedRecipeEventId);
                recipeEvent.CanMove = recipeEvent.CanResize = false;
                recipeEvent.Invalid = whole.Program == null;
                if (!characterActionEvent && (data.DurationFrames <= 1 || data.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition))
                    recipeEvent.Primitive = BehaviorRecipeEventPrimitive.Marker;
            }
            else if (!source.InspectCompiledExecution && source.TacticalPreview != null)
            {
                var authoring = source.TacticalPreview.FindEvent(timeline.StageId, recipeEvent.EventId);
                var block = source.TacticalPreview.FindBlock(recipeEvent.EventId);
                recipeEvent.TacticalOperationKind = authoring?.Operation?.Kind;
                string operationName = authoring?.Operation?.Kind switch {
                    "CharacterAction" => "动作起手", "ApproachOptimalRange" => "接近目标", "TurnToTarget" => "持续转向",
                    "WaitForCondition" => "等待条件", "ReturnToPlanner" => "退出行为卡", _ => "事件" };
                // Default source identifiers remain in the Inspector path and tooltip.
                // A deliberately authored display name always takes precedence.
                recipeEvent.Title = block != null ? block.Title : string.IsNullOrWhiteSpace(authoring?.Title) || authoring.Title == authoring.EventId
                    ? operationName : authoring.Title;
                if (source.TacticalPreview.FocusBlockId != null && authoring != null) recipeEvent.Title = CharacterTestTacticalAuthoringPreview.LowLevelTitle(authoring);
                if (!source.TacticalPreview.HasFramePositions) recipeEvent.Primitive = BehaviorRecipeEventPrimitive.Marker;
                recipeEvent.Detail = source.TacticalPreview.DescribeEvent(timeline.StageId, recipeEvent.EventId);
                recipeEvent.Invalid = source.TacticalPreview.EventTimeInvalid(recipeEvent.EventId);
                recipeEvent.CanMove = block == null && source.TacticalPreview.HasFramePositions && source.TacticalPreview.Editor?.CanEdit == true;
                if (block != null) recipeEvent.TacticalOperationKind = "EventBlock";
                recipeEvent.CanResize = block != null
                    ? block.Timing != null && source.TacticalPreview.HasFramePositions
                        && source.TacticalPreview.Editor?.CanEdit == true && source.TacticalPreview.Editor.Composition?.CanEdit == true
                    : recipeEvent.CanMove && authoring?.TimingKind == "Duration" && authoring.Operation.Kind != "CharacterAction";
                if (source.TacticalPreview.Editor?.Selection.Count > 0)
                    recipeEvent.Selected = source.TacticalPreview.Editor.Selection.Contains(recipeEvent.EventId);
            }
            lane.Event = recipeEvent;
            if (source.InspectCompiledExecution)
            {
                recipeEvent.CanMove = recipeEvent.CanResize = false;
                recipeEvent.Invalid = characterActionEvent && characterAction?.Valid != true;
                if (!characterActionEvent) recipeEvent.Title = data.Title;
            }
            timeline.AddTrack(lane);
        }

        private void EnsureLaneCapacity(int required)
        {
            if (_laneEndFrames.Length < required)
                Array.Resize(ref _laneEndFrames, Math.Max(required, _laneEndFrames.Length * 2 + 4));
        }

        internal static string BuildV6EventStableKey(string timelineId, string eventId) =>
            "event:" + (timelineId ?? string.Empty) + ":v6:" + (eventId ?? string.Empty);

        internal void UpdateRuntimeOverlay(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source)
        {
            CurrentTimelineIndex = -1;
            LocalPlayheadFrame = -1;
            for (int i = 0; i < _timelineCount; i++)
                _timelinePool[i].Current = false;
            for (int i = 0; i < _eventCount; i++)
            {
                _eventPool[i].Running = false;
                _eventPool[i].ProgressFrames = -1;
            }

            BehaviorRecipeRuntimeCursor cursor = source?.RuntimeCursor;
            if (cursor?.Available != true)
                return;

            for (int i = 0; i < _timelineCount; i++)
            {
                BehaviorRecipeTimelineViewModel timeline = _timelinePool[i];
                if (!string.Equals(timeline.StageId, cursor.TimelineId, StringComparison.Ordinal))
                    continue;
                timeline.Current = true;
                CurrentTimelineIndex = i;
                break;
            }
            if (CurrentTimelineIndex < 0)
                return;

            LocalPlayheadFrame = cursor.LocalFrame;
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot state = source.RuntimeState;
            int ownerEventIndex = state?.ActorOwnerEventIndex ?? -1;
            for (int i = 0; i < _eventCount; i++)
            {
                BehaviorRecipeEventViewModel recipeEvent = _eventPool[i];
                if (recipeEvent.CompiledEventIndex != ownerEventIndex
                    || !string.Equals(recipeEvent.StageId, cursor.TimelineId, StringComparison.Ordinal))
                {
                    continue;
                }
                recipeEvent.Running = true;
                recipeEvent.ProgressFrames = Math.Max(0, cursor.LocalFrame - recipeEvent.StartFrame);
            }
        }

        private static void ResolveV6Runtime(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source,
            BehaviorRecipeEventViewModel destination)
        {
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot state = source.RuntimeState;
            if (source.RuntimeCursor.Available != true || state == null)
                return;
            destination.Running = destination.CompiledEventIndex >= 0
                && state.ActorOwnerEventIndex == destination.CompiledEventIndex
                && string.Equals(
                    destination.StageId,
                    source.RuntimeCursor.TimelineId,
                    StringComparison.Ordinal);
            destination.ProgressFrames = destination.Running
                ? Math.Max(0, source.RuntimeCursor.LocalFrame - destination.StartFrame)
                : -1;
        }

        private static int ResolveCompiledEventIndex(
            CharacterTestRuntimeAiLabCompiledRecipeV6 recipe,
            string timelineId,
            string eventId)
        {
            if (recipe == null || string.IsNullOrEmpty(eventId))
                return -1;
            for (int i = 0; i < recipe.Events.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledRecipeV6Event candidate = recipe.Events[i];
                if (!string.Equals(candidate.EventId, eventId, StringComparison.Ordinal))
                    continue;
                if (candidate.TimelineIndex < 0
                    || (candidate.TimelineIndex < recipe.Timelines.Count
                        && string.Equals(
                            recipe.Timelines[candidate.TimelineIndex].TimelineId,
                            timelineId,
                            StringComparison.Ordinal)))
                {
                    return candidate.Index;
                }
            }
            return -1;
        }

        internal static string BuildV6ConditionSummary(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
        {
            if (node == null)
                return string.Empty;
            switch (node.Kind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate:
                    return BuildFactSummary(node.Predicate);
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal:
                    return "事件 " + node.SourceEventId + " " + SignalText(node.Signal);
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope:
                    return node.Children.Count == 1
                        ? CharacterTestRuntimeAiLabBehaviorConditionCatalog.SubjectText(node.Subject)
                            + "：" + BuildV6ConditionSummary(node.Children[0])
                        : "无效对象作用域";
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not:
                    if (node.Children.Count != 1)
                        return "无效条件";
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNode negated = node.Children[0];
                    while (negated != null
                        && (negated.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                            || negated.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any)
                        && negated.Children.Count == 1)
                    {
                        negated = negated.Children[0];
                    }
                    if (negated?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate)
                        return BuildNegatedFactSummary(negated.Predicate);
                    if (negated?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not
                        && negated.Children.Count == 1)
                        return BuildV6ConditionSummary(negated.Children[0]);
                    return "非（" + BuildV6ConditionSummary(negated) + "）";
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any:
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All:
                    var parts = new List<string>(node.Children.Count);
                    for (int i = 0; i < node.Children.Count; i++)
                        parts.Add(BuildV6ConditionSummary(node.Children[i]));
                    return string.Join(node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All ? " 且 " : " 或 ", parts);
                default:
                    return string.Empty;
            }
        }

        private static string BuildFactSummary(CharacterTestRuntimeAiLabCardCondition condition)
        {
            if (condition == null)
                return "未配置条件";
            string fact = CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                condition.Fact,
                out CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor)
                    ? descriptor.DisplayName
                    : condition.Fact.ToString();
            string op = condition.Operator switch
            {
                CharacterTestRuntimeAiLabCardConditionOperator.Equals => "=",
                CharacterTestRuntimeAiLabCardConditionOperator.NotEquals => "≠",
                CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual => "≤",
                CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan => ">",
                CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual => "≥",
                CharacterTestRuntimeAiLabCardConditionOperator.Between => "区间",
                _ => condition.Operator.ToString()
            };
            string value = descriptor.ValueKind switch
            {
                CharacterTestRuntimeAiLabBehaviorConditionValueKind.Boolean =>
                    condition.BoolValue ? "是" : "否",
                CharacterTestRuntimeAiLabBehaviorConditionValueKind.TargetStateMask =>
                    BehaviorRecipeTriggerEventInspectorProjection.FormatTargetStateValue(condition),
                _ => condition.Value.ToString("0.##", CultureInfo.InvariantCulture)
                    + (descriptor.Unit.Length > 0 ? " " + descriptor.Unit : string.Empty)
            };
            if (descriptor.ValueKind == CharacterTestRuntimeAiLabBehaviorConditionValueKind.TargetStateMask)
                return fact + " · " + value;
            return fact + " " + op + " " + value;
        }

        private static string BuildNegatedFactSummary(
            CharacterTestRuntimeAiLabCardCondition condition)
        {
            if (condition == null)
                return "未配置条件";
            string fact = CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                condition.Fact,
                out CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor)
                    ? descriptor.DisplayName
                    : condition.Fact.ToString();
            bool booleanFact = descriptor.ValueKind == CharacterTestRuntimeAiLabBehaviorConditionValueKind.Boolean;
            if (descriptor.ValueKind == CharacterTestRuntimeAiLabBehaviorConditionValueKind.TargetStateMask)
                return "非（" + BehaviorRecipeTriggerEventInspectorProjection.FormatTargetStateValue(condition) + "）";
            if (booleanFact && condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals)
                return fact + " = " + (condition.BoolValue ? "否" : "是");
            if (booleanFact && condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.NotEquals)
                return fact + " = " + (condition.BoolValue ? "是" : "否");
            string value = condition.Value.ToString("0.##", CultureInfo.InvariantCulture)
                + (descriptor.Unit.Length > 0 ? " " + descriptor.Unit : string.Empty);
            string op = condition.Operator switch
            {
                CharacterTestRuntimeAiLabCardConditionOperator.Equals => "≠",
                CharacterTestRuntimeAiLabCardConditionOperator.NotEquals => "=",
                CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual => ">",
                CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan => "≤",
                CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual => "<",
                CharacterTestRuntimeAiLabCardConditionOperator.Between => "不在区间",
                _ => "不满足"
            };
            return fact + " " + op + " " + value;
        }

        internal static string BuildV6OperationSummary(
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperation operation,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            BehaviorRecipeCharacterActionOptionViewModel[] actionOptions = null)
        {
            if (operation == null || operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured)
                return "未配置操作";
            switch (operation.Kind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget: return "接近目标";
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction:
                    BehaviorRecipeCharacterActionOptionViewModel action =
                        BehaviorRecipeTriggerEventInspectorProjection.FindCharacterActionOption(
                            actionOptions,
                            operation.CharacterActionId);
                    return "播放 " + (action?.DisplayName
                        ?? (!operation.CharacterActionId.IsValid
                            ? "未选择角色动作"
                            : operation.CharacterActionId.ToString()));
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Ability: return "已移除的 Ability 事件";
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Hold: return "已移除的 Hold 事件";
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition: return "等待条件";
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion: return "循环区间";
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner: return "显式退出行为卡";
                case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline:
                    for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
                    {
                        if (string.Equals(payload.Timelines[i]?.TimelineId, operation.TargetTimelineId, StringComparison.Ordinal))
                            return "进入 " + (string.IsNullOrWhiteSpace(payload.Timelines[i].Title) ? operation.TargetTimelineId : payload.Timelines[i].Title);
                    }
                    return "进入 " + operation.TargetTimelineId;
                default: return operation.Kind.ToString();
            }
        }

        private static string SignalText(CharacterTestRuntimeAiLabBehaviorRecipeEventSignal signal) => signal switch
        {
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Completed => "已完成",
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Failed => "执行失败",
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Rejected => "执行被拒绝",
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.CommitFailed => "本次提交失败（未出招）",
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Cancelled => "被取消",
            _ => signal.ToString()
        };

    }
}
