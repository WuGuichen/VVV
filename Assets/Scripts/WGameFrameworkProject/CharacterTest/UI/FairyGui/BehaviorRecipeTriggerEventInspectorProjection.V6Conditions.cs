using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class BehaviorRecipeTriggerEventInspectorProjection
    {
        private static void BuildV6Conditions(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            int depth,
            List<BehaviorRecipeTriggerConditionViewModel> rows,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            ref int leafIndex)
        {
            if (node == null)
                return;
            if (node.MovementQuery != null)
            {
                string label = node.MovementQuery == "Arrived" ? "移动到位" : "移动目标失效";
                rows.Add(new BehaviorRecipeTriggerConditionViewModel { NodeId = node.NodeId, LeafIndex = leafIndex++, Depth = depth,
                    NodeKind = node.Kind, FactText = label, ValueText = node.SourceEventId, EffectiveSummary = label + " · " + node.SourceEventId });
                return;
            }
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate)
            {
                CharacterTestRuntimeAiLabCardCondition condition = node.Predicate
                    ?? new CharacterTestRuntimeAiLabCardCondition();
                BehaviorRecipeTriggerConditionValueKind valueKind = ResolveValueKind(condition);
                int effectiveOperator = ResolveEffectiveOperator(condition.Operator, false);
                string fact = ResolveFactText(condition.Fact);
                string op = valueKind == BehaviorRecipeTriggerConditionValueKind.TargetState
                    ? "符合"
                    : ResolveOperatorText(effectiveOperator);
                string value = valueKind == BehaviorRecipeTriggerConditionValueKind.Boolean
                    ? (condition.BoolValue ? "是" : "否")
                    : ResolveValueText(condition, valueKind);
                rows.Add(new BehaviorRecipeTriggerConditionViewModel
                {
                    NodeId = node.NodeId ?? string.Empty,
                    ConditionId = condition.ConditionId ?? string.Empty,
                    Depth = depth,
                    LeafIndex = leafIndex++,
                    NodeKind = node.Kind,
                    Fact = condition.Fact,
                    FactText = fact,
                    EffectiveOperator = effectiveOperator,
                    OperatorText = op,
                    ValueKind = valueKind,
                    BoolValue = condition.BoolValue,
                    Value = condition.Value,
                    MaxValue = condition.MaxValue,
                    AbilityId = condition.AbilityId,
                    CharacterActionId = condition.CharacterActionId,
                    AllowedTargetPhases = condition.AllowedTargetPhases,
                    RequiredTargetFlags = condition.RequiredTargetFlags,
                    ForbiddenTargetFlags = condition.ForbiddenTargetFlags,
                    ValueText = value,
                    EffectiveSummary = new string('　', Math.Max(0, depth)) + fact + " " + op + " " + value
                });
                return;
            }
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal)
            {
                rows.Add(new BehaviorRecipeTriggerConditionViewModel
                {
                    NodeId = node.NodeId ?? string.Empty,
                    ConditionId = node.NodeId ?? string.Empty,
                    Depth = depth,
                    LeafIndex = leafIndex++,
                    NodeKind = node.Kind,
                    SourceEventId = node.SourceEventId ?? string.Empty,
                    EventSignal = node.Signal,
                    ValueKind = BehaviorRecipeTriggerConditionValueKind.EventSignal,
                    FactText = "事件 " + ResolveV6EventTitle(timeline, node.SourceEventId),
                    OperatorText = "为",
                    ValueText = ResolveV6Signal(node.Signal),
                    EffectiveSummary = new string('　', Math.Max(0, depth))
                        + ResolveV6EventTitle(timeline, node.SourceEventId) + " " + ResolveV6Signal(node.Signal)
                });
                return;
            }
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope)
            {
                rows.Add(new BehaviorRecipeTriggerConditionViewModel
                {
                    NodeId = node.NodeId ?? string.Empty,
                    ConditionId = node.NodeId ?? string.Empty,
                    Depth = depth,
                    ChildCount = node.Children.Count,
                    LeafIndex = -1,
                    NodeKind = node.Kind,
                    Subject = node.Subject,
                    SubjectScope = true,
                    ValueKind = BehaviorRecipeTriggerConditionValueKind.EventSignal,
                    FactText = CharacterTestRuntimeAiLabBehaviorConditionCatalog.SubjectText(node.Subject),
                    EffectiveSummary = CharacterTestRuntimeAiLabBehaviorConditionCatalog.SubjectText(node.Subject)
                });
                for (int i = 0; i < node.Children.Count; i++)
                    BuildV6Conditions(node.Children[i], depth + 1, rows, payload, timeline, ref leafIndex);
                return;
            }
            rows.Add(new BehaviorRecipeTriggerConditionViewModel
            {
                NodeId = node.NodeId ?? string.Empty,
                ConditionId = node.NodeId ?? string.Empty,
                Depth = depth,
                ChildCount = node.Children.Count,
                LeafIndex = -1,
                NodeKind = node.Kind,
                ValueKind = BehaviorRecipeTriggerConditionValueKind.EventSignal,
                FactText = node.Kind switch
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All => "全部满足",
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any => "任一满足",
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not => "非",
                    _ => "条件组"
                },
                EffectiveSummary = BehaviorRecipeTimelineProjection.BuildV6ConditionSummary(node)
            });
            for (int i = 0; i < node.Children.Count; i++)
                BuildV6Conditions(node.Children[i], depth + 1, rows, payload, timeline, ref leafIndex);
        }

        internal static bool TryResolveConditionLeaf(
            IReadOnlyList<BehaviorRecipeTriggerConditionViewModel> rows,
            int leafIndex,
            out BehaviorRecipeTriggerConditionViewModel row)
        {
            row = null;
            if (rows == null || leafIndex < 0)
                return false;
            for (int i = 0; i < rows.Count; i++)
            {
                BehaviorRecipeTriggerConditionViewModel candidate = rows[i];
                if (candidate != null && candidate.LeafIndex == leafIndex)
                {
                    row = candidate;
                    return true;
                }
            }
            return false;
        }

        internal static string FormatTargetStateValue(CharacterTestRuntimeAiLabCardCondition condition)
        {
            if (condition == null)
                return "未选择阶段";
            string phases = FormatTargetMask(
                condition.AllowedTargetPhases,
                new[]
                {
                    ((uint)CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget, "无目标"),
                    ((uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive, "存活"),
                    ((uint)CharacterTestRuntimeAiPlannerTargetStateMask.Dying, "濒死"),
                    ((uint)CharacterTestRuntimeAiPlannerTargetStateMask.Dead, "死亡"),
                    ((uint)CharacterTestRuntimeAiPlannerTargetStateMask.Reviving, "复活中")
                });
            string required = FormatTargetMask(
                condition.RequiredTargetFlags,
                TargetFlagLabels);
            string forbidden = FormatTargetMask(
                condition.ForbiddenTargetFlags,
                TargetFlagLabels);
            return "阶段：" + (phases.Length > 0 ? phases : "未选择")
                + (required.Length > 0 ? " · 必须：" + required : string.Empty)
                + (forbidden.Length > 0 ? " · 排除：" + forbidden : string.Empty);
        }

        private static readonly (uint Mask, string Label)[] TargetFlagLabels =
        {
            ((uint)CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable, "可感知"),
            ((uint)CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked, "视线受阻"),
            ((uint)CharacterTestRuntimeAiPlannerTargetStateMask.InAttackRange, "攻击范围内"),
            ((uint)CharacterTestRuntimeAiPlannerTargetStateMask.MovePathAvailable, "移动建议可用"),
            ((uint)CharacterTestRuntimeAiPlannerTargetStateMask.AttackThreatToSelf, "攻击威胁朝向自身"),
            ((uint)CharacterTestRuntimeAiPlannerTargetStateMask.AttackInstanceResponded, "攻击实例已响应")
        };

        private static string FormatTargetMask(
            uint mask,
            (uint Mask, string Label)[] labels)
        {
            var values = new List<string>();
            for (int i = 0; i < labels.Length; i++)
                if ((mask & labels[i].Mask) != 0u)
                    values.Add(labels[i].Label);
            return string.Join("、", values);
        }

        private static string BuildV6EventSummary(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            BehaviorRecipeCharacterActionOptionViewModel[] actionOptions)
        {
            string condition = BehaviorRecipeTimelineProjection.BuildV6ConditionSummary(evt.ConditionRoot);
            string operation = BehaviorRecipeTimelineProjection.BuildV6OperationSummary(
                evt.Operation,
                payload,
                actionOptions);
            return string.IsNullOrEmpty(condition) ? operation : condition + " → " + operation;
        }

        internal static BehaviorRecipeCharacterActionOptionViewModel[] BuildCharacterActionOptions(
            IReadOnlyList<CharacterActionConfig> actions)
        {
            if (actions == null || actions.Count == 0)
                return Array.Empty<BehaviorRecipeCharacterActionOptionViewModel>();
            var options = new List<BehaviorRecipeCharacterActionOptionViewModel>(actions.Count);
            var seen = new HashSet<CharacterActionId>();
            for (int i = 0; i < actions.Count; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action == null
                    || !action.ActionId.IsValid
                    || !seen.Add(action.ActionId))
                    continue;
                string displayName = string.IsNullOrWhiteSpace(action.DisplayName)
                    ? "动作 " + action.ActionId
                    : action.DisplayName.Trim();
                string tags = action.Tags == null ? string.Empty : string.Join(" ", action.Tags);
                int duration = action.DurationFrames.GetValueOrDefault();
                string category = action.Category.ToString();
                options.Add(new BehaviorRecipeCharacterActionOptionViewModel
                {
                    ActionId = action.ActionId,
                    DisplayName = displayName,
                    CategoryText = category,
                    DurationFrames = Math.Max(0, duration),
                    TagsText = tags,
                    SearchText = (displayName + " " + action.ActionId + " " + category + " " + tags).ToLowerInvariant(),
                    DisplayText = displayName + "（" + action.ActionId + "）",
                    Valid = duration > 0,
                    TimelinePreview = CharacterActionTimelinePreviewProjection.Build(action),
                });
            }
            options.Sort((left, right) =>
            {
                int display = string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCultureIgnoreCase);
                return display != 0 ? display : left.ActionId.CompareTo(right.ActionId);
            });
            return options.ToArray();
        }

        internal static BehaviorRecipeCharacterActionOptionViewModel FindCharacterActionOption(
            BehaviorRecipeCharacterActionOptionViewModel[] options,
            CharacterActionId actionId)
        {
            if (!actionId.IsValid)
                return null;
            for (int i = 0; options != null && i < options.Length; i++)
            {
                if (options[i]?.ActionId == actionId)
                    return options[i];
            }
            return null;
        }

        private static void ApplyCharacterActionPresentation(
            List<BehaviorRecipeTriggerConditionViewModel> rows,
            BehaviorRecipeCharacterActionOptionViewModel[] options)
        {
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                BehaviorRecipeTriggerConditionViewModel row = rows[i];
                if (row?.ValueKind != BehaviorRecipeTriggerConditionValueKind.CharacterAction)
                    continue;
                BehaviorRecipeCharacterActionOptionViewModel option =
                    FindCharacterActionOption(options, row.CharacterActionId);
                row.ValueText = option?.DisplayName
                    ?? (!row.CharacterActionId.IsValid ? "选择角色动作…" : row.CharacterActionId.ToString());
                if (row.CharacterActionId.IsValid && option == null)
                    row.ValidationText = "动作引用不存在：" + row.CharacterActionId;
            }
        }

        private static string ResolveV6RuntimeState(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState)
        {
            if (timeline.Historical)
                return "历史帧只读";
            CharacterTestRuntimeAiLabCompiledRecipeV6 recipe = timeline.CompiledRecipeV6;
            BehaviorRecipeRuntimeCursor cursor = timeline.RuntimeCursor;
            if (recipe == null
                || runtimeState == null
                || cursor?.Available != true
                || !cursor.AuthorityMatched)
                return "未运行";
            CharacterTestRuntimeAiLabCompiledRecipeV6Event compiledEvent = null;
            for (int i = 0; i < recipe.Events.Count; i++)
            {
                if (string.Equals(recipe.Events[i].EventId, evt.EventId, StringComparison.Ordinal))
                {
                    compiledEvent = recipe.Events[i];
                    break;
                }
            }
            if (compiledEvent == null)
                return "未运行";
            int eventIndex = compiledEvent.Index;
            if (runtimeState.PendingEventId == eventIndex
                || runtimeState.ActorOwnerEventIndex == eventIndex)
                return runtimeState.WaitingForInstantOwnerAtTimelineEnd
                    && evt.TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                        ? "时间轴末端 · 等待操作完成"
                        : "执行中";
            int result = runtimeState.EventResults != null && eventIndex < runtimeState.EventResults.Length
                ? runtimeState.EventResults[eventIndex]
                : 0;
            return ((CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult)result) switch
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed => "已完成",
                CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Failed => "失败",
                CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected => "被拒绝",
                CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.CommitFailed => "本次提交失败（未出招）",
                CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled => "被取消",
                CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Running => "执行中",
                _ => ResolveV6PendingRuntimeState(cursor, compiledEvent)
            };
        }

        private static string ResolveV6PendingRuntimeState(
            BehaviorRecipeRuntimeCursor cursor,
            CharacterTestRuntimeAiLabCompiledRecipeV6Event evt)
        {
            if (cursor == null || evt == null || evt.TimelineIndex != cursor.TimelineIndex)
                return "未运行";

            int frame = cursor.LocalFrame;
            if (frame < evt.StartFrame)
                return "等待开始";
            bool withinSamplingRange = evt.IsInstant
                ? frame == evt.StartFrame
                : frame < evt.EndFrame;
            if (!withinSamplingRange)
                return "未触发";
            if (evt.IsWaitControl || evt.Condition != null)
                return "等待条件";
            return "等待触发";
        }

        private static string ResolveV6EventTitle(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            string eventId)
        {
            for (int i = 0; timeline != null && i < timeline.Events.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[i];
                if (string.Equals(evt?.EventId, eventId, StringComparison.Ordinal))
                    return string.IsNullOrWhiteSpace(evt.Title) ? evt.EventId : evt.Title.Trim();
            }
            return eventId ?? string.Empty;
        }

        private static BehaviorRecipeTriggerEventSignalOptionViewModel[] BuildV6SignalEventOptions(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            string owningEventId)
        {
            var result = new List<BehaviorRecipeTriggerEventSignalOptionViewModel>();
            for (int i = 0; timeline != null && i < timeline.Events.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[i];
                if (evt == null || string.Equals(evt.EventId, owningEventId, StringComparison.Ordinal))
                    continue;
                result.Add(new BehaviorRecipeTriggerEventSignalOptionViewModel
                {
                    EventId = evt.EventId ?? string.Empty,
                    DisplayText = string.IsNullOrWhiteSpace(evt.Title) ? evt.EventId : evt.Title.Trim()
                });
            }
            return result.ToArray();
        }

        private static string ResolveV6Signal(CharacterTestRuntimeAiLabBehaviorRecipeEventSignal signal) => signal switch
        {
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Completed => "已完成",
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Failed => "失败",
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Rejected => "被拒绝",
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Cancelled => "被取消",
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.CommitFailed => "本次提交失败（未出招）",
            _ => signal.ToString()
        };

    }
}
