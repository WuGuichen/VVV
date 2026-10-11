using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Cached read-only adapter from the actual execution table to the original Recipe editor.</summary>
    internal sealed class CharacterTestNativeExecutionView
    {
        private static readonly ConditionalWeakTable<CharacterTestRuntimeAiLabCompiledLogicProgram, CharacterTestNativeExecutionView> Cache = new();
        private static readonly ConditionalWeakTable<CharacterTestRuntimeAiLabCompiledLogicProgram, CharacterTestNativeExecutionView>.CreateValueCallback Factory = p => new CharacterTestNativeExecutionView(p);
        internal static CharacterTestNativeExecutionView Get(CharacterTestRuntimeAiLabCompiledLogicProgram program) =>
            program?.Execution == null || program.ExecutionDebugMap == null ? null : Cache.GetValue(program, Factory);
        internal CharacterTestRuntimeAiLabCompiledLogicProgram Program { get; }
        internal CharacterTestRuntimeAiLabBehaviorCardLogicPayload Payload { get; }
        private readonly Dictionary<string, int> _events = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CharacterTestBehaviorExecutionPredicate> _specialPredicates = new(StringComparer.Ordinal);

        private CharacterTestNativeExecutionView(CharacterTestRuntimeAiLabCompiledLogicProgram program)
        {
            Program = program;
            var table = program.Execution; var map = program.ExecutionDebugMap;
            Payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload { BehaviorRecipeVersion = 11,
                EntryTimelineId = map.Timeline(table.EntryTimeline), ActivationConditionRoot = Condition(table.ActivationCondition) };
            for (int i = 0; i < table.Timelines.Length; i++)
            {
                var row = table.Timelines[i]; string id = map.Timeline(i);
                var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline { TimelineId = id,
                    Title = "Timeline " + i + (i == table.EntryTimeline ? " · 入口" : string.Empty),
                    Order = i, EntryConditionRoot = Condition(row.EntryCondition) };
                for (int j = 0; j < row.EventCount; j++)
                {
                    int index = table.TimelineEvents[row.EventStart + j]; var ev = table.Events[index];
                    var operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation { Kind = ev.Kind };
                    if (ev.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                        operation.CharacterActionId = new CharacterActionId(table.Actions[ev.Parameter].ActionId);
                    if (ev.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline)
                        operation.TargetTimelineId = map.Timeline(table.Flows[ev.Parameter].TargetTimeline);
                    if (ev.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion)
                        operation.MaxLoopRepeats = table.Flows[ev.Parameter].MaxLoopRepeats;
                    if (ev.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget)
                    {
                        var move = table.Movements[ev.Parameter];
                        operation.MovementStrategy = move.Strategy; operation.MovementTargetKind = move.Target;
                        operation.MoveTargetEntityId = move.EntityId; operation.MoveTargetPositionX = move.X; operation.MoveTargetPositionZ = move.Z;
                        operation.MoveMinDistance = move.Minimum; operation.MoveMaxDistance = move.Maximum; operation.MoveSpeedMultiplier = move.Speed;
                    }
                    string key = map.Event(index); _events.Add(key, index);
                    timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = key, TimelineId = id,
                        Title = OperationName(ev.Kind) + " #" + index, StartFrame = ev.StartFrame, DurationFrames = ev.DurationFrames,
                        TimingKind = ev.Timing, ClockOrigin = ev.Clock, Priority = ev.Priority, MaxExecutionsPerPass = ev.MaxExecutionsPerPass,
                        CooldownFrames = ev.CooldownFrames, ConditionRoot = Condition(ev.Condition), Operation = operation });
                }
                Payload.Timelines.Add(timeline);
            }
        }

        private CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Condition(int index)
        {
            if (index < 0) return null;
            var table = Program.Execution; var map = Program.ExecutionDebugMap; var value = table.Conditions[index];
            var node = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = map.Condition(index), Kind = value.Kind,
                Subject = value.Subject, SourceEventId = value.SourceEvent >= 0 ? map.Event(value.SourceEvent) : string.Empty, Signal = value.Signal };
            if (value.Predicate >= 0)
            {
                var predicate = table.Predicates[value.Predicate];
                node.Predicate = new CharacterTestRuntimeAiLabCardCondition { Fact = predicate.Fact, Operator = predicate.Operator,
                    BoolValue = predicate.BoolValue, Value = predicate.Value, MaxValue = predicate.Maximum,
                    AbilityId = predicate.AbilityId, CharacterActionId = new CharacterActionId(predicate.ActionId),
                    AllowedTargetPhases = predicate.Phases, RequiredTargetFlags = predicate.RequiredFlags, ForbiddenTargetFlags = predicate.ForbiddenFlags };
                if (predicate.Kind != CharacterTestBehaviorExecutionPredicateKind.Fact)
                {
                    _specialPredicates[node.NodeId] = predicate;
                    // The original persisted authoring schema has no representation for these predicates.
                    // Keep them explicitly opaque there; Inspector rows below display the actual compiled fields.
                    node.Predicate.Fact = (CharacterTestRuntimeAiLabCardConditionFact)(-1);
                }
            }
            for (int i = 0; i < value.ChildCount; i++) node.Children.Add(Condition(table.Children[value.ChildStart + i]));
            return node;
        }

        internal void ApplyInspector(BehaviorRecipeTriggerEventInspectorViewModel model,
            CharacterTestRuntimeAiPlannerAgentSnapshot? observedAgent = null, int observedRuntimeFrame = -1,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState = null)
        {
            if (model == null) return;
            model.CanEdit = model.CanEditDisplayTitle = model.CanEditResponsibilities = false;
            model.RuntimeStateText = "编译数据 · 只读";
            model.CharacterActionConditionRepairRequired = false;
            model.ValidationText = model.CharacterActionEvent && !model.CharacterActionReferenceValid
                ? "当前显示目录缺少此动作，执行表中的动作引用仍保留。" : string.Empty;
            if (_events.TryGetValue(model.EventId ?? string.Empty, out int index))
            {
                var ev = Program.Execution.Events[index];
                if (ev.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction
                    && runtimeState?.ExecutionState?.ProgramIdentity == Program.Execution.Identity
                    && runtimeState.ExecutionState.Results.Length > index
                    && runtimeState.ExecutionState.Results[index] == (int)CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected)
                    model.RuntimeStateText = "起手条件未通过 · 本次未出招，不会补播";
                model.EffectiveTriggerSummary = "执行事件 #" + index + " · " + OperationName(ev.Kind)
                    + " · " + ev.StartFrame + "f / " + ev.DurationFrames + "f · 优先级 " + ev.Priority;
                if (ev.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget)
                {
                    var m = Program.Execution.Movements[ev.Parameter];
                    model.EffectiveTriggerSummary += "\n距离口径：" + m.Metric + (m.IsOptimalRange ? " · 最佳距离 " + m.Optimal + " · 停步容差 " + m.StopTolerance : string.Empty);
                }
                if (ev.Kind == CharacterTestBehaviorExecutionOperations.TurnToTarget)
                {
                    var t = Program.Execution.Turns[ev.Parameter];
                    model.EffectiveTriggerSummary += "\n目标：" + t.Target + " · 转向速度 " + t.Speed;
                }
            }
            ApplyConditions(model, observedAgent, observedRuntimeFrame);
        }

        internal void ApplyConditions(BehaviorRecipeTriggerEventInspectorViewModel model,
            CharacterTestRuntimeAiPlannerAgentSnapshot? observedAgent, int observedRuntimeFrame)
        {
            foreach (var row in model.Conditions)
            {
                if (!_specialPredicates.TryGetValue(row.NodeId, out var p)) continue;
                row.ValidationText = string.Empty; row.ObservationState = BehaviorRecipeConditionObservationState.NotObserved;
                row.ObservedValueText = "编译条件"; row.ObservationReasonText = "读取执行表；未在查看器中重新执行条件。";
                row.ValueKind = BehaviorRecipeTriggerConditionValueKind.Number; row.Value = p.Value;
                row.FactText = p.Kind == CharacterTestBehaviorExecutionPredicateKind.BoundActionOpportunity ? "动作命中机会"
                    : p.Kind == CharacterTestBehaviorExecutionPredicateKind.TargetFacing ? "目标相对朝向" : "根节点平面距离";
                row.OperatorText = p.Kind == CharacterTestBehaviorExecutionPredicateKind.TargetFacing ? "允许偏差 " + p.Maximum
                    : p.Kind == CharacterTestBehaviorExecutionPredicateKind.RootDistanceRange ? "上限 " + p.Maximum : "动作 " + p.ActionId;
                row.ValueText = p.Value.ToString();
                if (p.Kind == CharacterTestBehaviorExecutionPredicateKind.BoundActionOpportunity)
                {
                    row.ValueKind = BehaviorRecipeTriggerConditionValueKind.CharacterAction;
                    row.CharacterActionId = new CharacterActionId(p.ActionId);
                    row.ValueText = BehaviorRecipeTriggerEventInspectorProjection.FindCharacterActionOption(model.CharacterActionOptions, row.CharacterActionId)?.DisplayName ?? p.ActionId.ToString();
                    row.OperatorText = "满足";
                }
                else if (p.Kind == CharacterTestBehaviorExecutionPredicateKind.RootDistanceRange)
                {
                    row.Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance;
                    row.ValueKind = BehaviorRecipeTriggerConditionValueKind.Range;
                    row.MaxValue = p.Maximum; row.OperatorText = "范围";
                }
                else row.OperatorText = "±" + p.Maximum + "°";
                row.EffectiveSummary = row.FactText + " · " + row.OperatorText + " · " + row.ValueText;
                ApplySpatialObservation(row, p, observedAgent, observedRuntimeFrame);
            }
        }

        private static void ApplySpatialObservation(BehaviorRecipeTriggerConditionViewModel row,
            CharacterTestBehaviorExecutionPredicate predicate,
            CharacterTestRuntimeAiPlannerAgentSnapshot? observedAgent, int frame)
        {
            if (predicate.Kind != CharacterTestBehaviorExecutionPredicateKind.RootDistanceRange
                && predicate.Kind != CharacterTestBehaviorExecutionPredicateKind.TargetFacing) return;
            // Observe the selected frame; never cache live facts on the immutable program adapter.
            row.ObservationRuntimeFrame = observedAgent.HasValue ? frame : -1;
            row.ObservedValueText = "暂无数据";
            row.ObservationReasonText = string.Empty;
            if (!observedAgent.HasValue) return;
            var agent = observedAgent.Value;
            if (!agent.TargetExists)
            {
                row.ObservationState = BehaviorRecipeConditionObservationState.SubjectUnavailable;
                row.ObservedValueText = "无目标";
                return;
            }
            if (!agent.HasScenePreviewGeometry) return;
            bool distance = predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.RootDistanceRange;
            float value = distance ? agent.TargetRootDistance : agent.ScenePreviewFacingDeltaMilliDegrees / 1000f;
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            if (!distance && agent.TargetRootDistance <= 0.000001f)
            {
                row.ObservedValueText = "目标重合，朝向不可用";
                return;
            }
            bool satisfied = distance ? value >= predicate.Value && value <= predicate.Maximum
                : Math.Abs(CharacterTestDeterministicMath.ShortestAngleDelta(
                    (int)Math.Round(predicate.Value * 1000d), agent.ScenePreviewFacingDeltaMilliDegrees))
                    <= (int)Math.Round(predicate.Maximum * 1000d);
            row.ObservationState = satisfied ? BehaviorRecipeConditionObservationState.Satisfied
                : BehaviorRecipeConditionObservationState.Unsatisfied;
            row.ObservedValueText = (distance ? "当前距离：" : "当前朝向：")
                + value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + (distance ? " 米" : "°");
            row.ObservationReasonText = satisfied ? "范围内" : "超出范围";
        }

        private static string OperationName(CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind kind) => kind switch {
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction => "播放角色动作",
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget => "移动",
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition => "等待条件",
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline => "进入时间轴",
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner => "显式退出行为卡",
            CharacterTestBehaviorExecutionOperations.TurnToTarget => "转向目标", _ => kind.ToString() };
    }
}
