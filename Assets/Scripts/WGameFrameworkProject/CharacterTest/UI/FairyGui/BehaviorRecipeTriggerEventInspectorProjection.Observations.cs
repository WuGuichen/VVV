using System;
using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class BehaviorRecipeTriggerEventInspectorProjection
    {
        private static void ApplyV10ConditionObservations(
            BehaviorRecipeTriggerEventInspectorViewModel model,
            CharacterTestRuntimeAiPlannerAgentSnapshot? observedAgent,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState,
            int runtimeFrame,
            CharacterTestRuntimeAiLabCompiledRecipeV6 recipe)
        {
            if (model?.Conditions == null)
                return;
            for (int i = 0; i < model.Conditions.Length; i++)
            {
                BehaviorRecipeTriggerConditionViewModel row = model.Conditions[i];
                if (row == null)
                    continue;
                row.ObservationRuntimeFrame = observedAgent.HasValue ? runtimeFrame : -1;
                if (row.SubjectScope)
                {
                    ApplySubjectObservation(row, observedAgent);
                    continue;
                }
                if (row.NodeKind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate)
                {
                    if (row.NodeKind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal)
                        ApplyEventSignalObservation(row, runtimeState, recipe);
                    else
                        row.ObservationState = BehaviorRecipeConditionObservationState.NotObserved;
                    continue;
                }
                ApplyFactObservation(row, observedAgent, runtimeState);
            }
        }

        private static void ApplyEventSignalObservation(
            BehaviorRecipeTriggerConditionViewModel row,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState,
            CharacterTestRuntimeAiLabCompiledRecipeV6 recipe)
        {
            if (runtimeState?.PreviousEventResults == null || recipe == null)
            {
                MarkNotObserved(row, "当前 RuntimeFrame 没有可用的上一帧事件结果。" );
                return;
            }
            int sourceIndex = -1;
            for (int i = 0; i < recipe.Events.Count; i++)
            {
                if (string.Equals(recipe.Events[i]?.EventId, row.SourceEventId, StringComparison.Ordinal))
                {
                    sourceIndex = i;
                    break;
                }
            }
            if (sourceIndex < 0 || sourceIndex >= runtimeState.PreviousEventResults.Length)
            {
                MarkNotObserved(row, "引用事件在当前编译配方中不可用。" );
                return;
            }
            CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult result =
                (CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult)
                    runtimeState.PreviousEventResults[sourceIndex];
            bool satisfied = row.EventSignal switch
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Completed =>
                    result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed,
                CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Failed =>
                    result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Failed,
                CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Rejected =>
                    result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected,
                CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.CommitFailed =>
                    result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.CommitFailed,
                CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Cancelled =>
                    result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled,
                _ => false
            };
            row.ObservationState = satisfied
                ? BehaviorRecipeConditionObservationState.Satisfied
                : BehaviorRecipeConditionObservationState.Unsatisfied;
            row.ObservedValueText = result switch
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed => "已完成",
                CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Failed => "执行失败",
                CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected => "执行被拒绝",
                CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.CommitFailed => "本次提交失败（未出招）",
                CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled => "被取消",
                CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Running => "运行中",
                _ => "无结果"
            };
            row.ObservationReasonText = "同一时间轴上一 RuntimeFrame 的稳定结果。";
        }

        private static void ApplySubjectObservation(
            BehaviorRecipeTriggerConditionViewModel row,
            CharacterTestRuntimeAiPlannerAgentSnapshot? observedAgent)
        {
            if (!observedAgent.HasValue)
            {
                row.ObservationState = BehaviorRecipeConditionObservationState.NotObserved;
                row.ObservedValueText = "未采样";
                row.ObservationReasonText = "当前没有可用的选中 Agent/RuntimeFrame 采样。";
                return;
            }
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = observedAgent.Value;
            if (row.Subject == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget)
            {
                if (!agent.TargetExists)
                {
                    row.ObservationState = BehaviorRecipeConditionObservationState.SubjectUnavailable;
                    row.SubjectIdentityText = string.Empty;
                    row.ObservedValueText = "当前：无目标";
                    row.ObservationReasonText = string.Empty;
                    return;
                }
                row.ObservationState = BehaviorRecipeConditionObservationState.Satisfied;
                row.SubjectIdentityText = "Entity "
                    + agent.TargetEntityId.ToString(CultureInfo.InvariantCulture);
                row.ObservedValueText = "当前："
                    + FormatObservedTargetState(BuildObservedTargetState(agent));
                row.ObservationReasonText = string.Empty;
                return;
            }
            row.ObservationState = BehaviorRecipeConditionObservationState.Satisfied;
            row.SubjectIdentityText = row.Subject == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self
                ? "Entity " + agent.EntityId.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            row.ObservedValueText = row.Subject
                    == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self
                ? "当前：可用"
                : "当前：环境采样可用";
            row.ObservationReasonText = string.Empty;
        }

        private static void ApplyFactObservation(
            BehaviorRecipeTriggerConditionViewModel row,
            CharacterTestRuntimeAiPlannerAgentSnapshot? observedAgent,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState)
        {
            if (!observedAgent.HasValue)
            {
                MarkNotObserved(row, "当前没有可用的选中 Agent/RuntimeFrame 采样。");
                return;
            }
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = observedAgent.Value;
            if (CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                    row.Fact,
                    out CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor)
                && descriptor.Subject == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget
                && row.Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetState
                && !agent.TargetExists)
            {
                row.ObservationState = BehaviorRecipeConditionObservationState.SubjectUnavailable;
                row.ObservedValueText = "—";
                row.ObservationReasonText = "当前没有战斗目标，目标属性不可用。";
                return;
            }

            bool boolValue;
            float numberValue;
            switch (row.Fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.TargetState:
                    ApplyTargetStateObservation(row, agent);
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetExists:
                    ApplyBoolean(row, agent.TargetExists);
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetVisible:
                    ApplyBoolean(row, agent.TargetVisible);
                    row.ObservationReasonText = "由视觉感知距离与直线视线共同推导。";
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDead:
                    ApplyBoolean(row, agent.TargetDead);
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistance:
                    if (!agent.TargetDistanceAvailable)
                    {
                        row.ObservationState = BehaviorRecipeConditionObservationState.NotObserved;
                        row.ObservedValueText = "—";
                        row.ObservationReasonText = "当前 RuntimeFrame 没有可用的锁定目标 Hurtbox 距离。";
                        return;
                    }
                    ApplyNumber(row, agent.TargetDistance, "米");
                    row.ObservationReasonText = "测量口径：自身姿态胶囊到锁定目标 Hurtbox 的三维表面净空。";
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance:
                    ApplyNumber(row, agent.TargetRootDistance, "米");
                    row.ObservationReasonText = "测量口径：自身根节点到目标根节点的 XZ 平面距离。";
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning:
                    boolValue = runtimeState != null
                        && runtimeState.PrimaryActionState == (int)CharacterTestRuntimeAiLabLogicActionState.Running;
                    ApplyBoolean(row, boolValue);
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible:
                    ApplyBoolean(row, agent.SelfActionInterruptible);
                    row.ObservationReasonText = agent.SelfActionInterruptible
                        ? "当前没有运行中动作，或动作已进入任一正式打断窗口。"
                        : "当前运行中动作尚未进入打断窗口。";
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable:
                    ApplyBoolean(row, agent.MovePathAvailable);
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf:
                    ApplyBoolean(row, agent.TargetAttackWillHitSelf);
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames:
                    numberValue = agent.TargetAttackImpactFrames;
                    ApplyNumber(row, numberValue, "帧");
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                    ApplyBoolean(row, agent.TargetAttackInstanceResponded);
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady:
                    ApplyBoolean(row, agent.SelfDodgeCooldownReady);
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange:
                    ApplyBoolean(row, agent.TargetInAttackRange);
                    row.ObservationReasonText = "口径：Runtime AI Planner Profile 基础攻击范围。";
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked:
                    ApplyBoolean(row, agent.TargetLineOfSightBlocked);
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard:
                    ApplyBoolean(row, agent.EnvironmentNearHazard);
                    return;
                case CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable:
                case CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction:
                    MarkNotObserved(row, "参数化条件只显示该 RuntimeFrame 的已记录 Fact trace；当前没有匹配记录。");
                    return;
                default:
                    MarkNotObserved(row, "当前 Fact 没有观察投影。" );
                    return;
            }
        }

        private static void ApplyTargetStateObservation(
            BehaviorRecipeTriggerConditionViewModel row,
            CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            uint state = BuildObservedTargetState(agent);
            bool satisfied = (state & row.AllowedTargetPhases) != 0u
                && (state & row.RequiredTargetFlags) == row.RequiredTargetFlags
                && (state & row.ForbiddenTargetFlags) == 0u;
            row.ObservationState = satisfied
                ? BehaviorRecipeConditionObservationState.Satisfied
                : BehaviorRecipeConditionObservationState.Unsatisfied;
            row.ObservedValueText = FormatObservedTargetState(state);
            if (satisfied)
            {
                row.ObservationReasonText = "阶段、必须状态和排除状态均匹配。";
                return;
            }
            uint phase = state & (uint)CharacterTestRuntimeAiPlannerTargetStateMask.PhaseMask;
            uint missing = row.RequiredTargetFlags & ~state;
            uint forbidden = row.ForbiddenTargetFlags & state;
            if ((phase & row.AllowedTargetPhases) == 0u)
                row.ObservationReasonText = "当前阶段不在允许阶段中。";
            else if (missing != 0u)
                row.ObservationReasonText = "缺少必须状态：" + FormatObservedTargetFlags(missing) + "。";
            else
                row.ObservationReasonText = "命中排除状态：" + FormatObservedTargetFlags(forbidden) + "。";
        }

        private static uint BuildObservedTargetState(
            CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            uint state = (uint)(!agent.TargetExists
                ? CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget
                : agent.TargetLifeState switch
                {
                    CharacterTestRuntimeAiPlannerTargetLifeState.Dying =>
                        CharacterTestRuntimeAiPlannerTargetStateMask.Dying,
                    CharacterTestRuntimeAiPlannerTargetLifeState.Dead =>
                        CharacterTestRuntimeAiPlannerTargetStateMask.Dead,
                    CharacterTestRuntimeAiPlannerTargetLifeState.Reviving =>
                        CharacterTestRuntimeAiPlannerTargetStateMask.Reviving,
                    _ => CharacterTestRuntimeAiPlannerTargetStateMask.Alive
                });
            if (agent.TargetVisible)
                state |= (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable;
            if (agent.TargetLineOfSightBlocked)
                state |= (uint)CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked;
            if (agent.TargetInAttackRange)
                state |= (uint)CharacterTestRuntimeAiPlannerTargetStateMask.InAttackRange;
            if (agent.MovePathAvailable)
                state |= (uint)CharacterTestRuntimeAiPlannerTargetStateMask.MovePathAvailable;
            if (agent.TargetAttackWillHitSelf)
                state |= (uint)CharacterTestRuntimeAiPlannerTargetStateMask.AttackThreatToSelf;
            if (agent.TargetAttackInstanceResponded)
                state |= (uint)CharacterTestRuntimeAiPlannerTargetStateMask.AttackInstanceResponded;
            return state;
        }

        private static string FormatObservedTargetState(uint state)
        {
            uint phase = state & (uint)CharacterTestRuntimeAiPlannerTargetStateMask.PhaseMask;
            string phaseText = phase switch
            {
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget => "无目标",
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive => "存活",
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Dying => "濒死",
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Dead => "死亡",
                (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Reviving => "复活中",
                _ => "阶段未知"
            };
            uint flags = state & (uint)CharacterTestRuntimeAiPlannerTargetStateMask.FlagMask;
            return flags == 0u ? phaseText : phaseText + " · " + FormatObservedTargetFlags(flags);
        }

        private static string FormatObservedTargetFlags(uint flags)
        {
            var values = new System.Collections.Generic.List<string>();
            AddObservedTargetFlag(values, flags, CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable, "可感知");
            AddObservedTargetFlag(values, flags, CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked, "视线受阻");
            AddObservedTargetFlag(values, flags, CharacterTestRuntimeAiPlannerTargetStateMask.InAttackRange, "攻击范围内");
            AddObservedTargetFlag(values, flags, CharacterTestRuntimeAiPlannerTargetStateMask.MovePathAvailable, "移动建议可用");
            AddObservedTargetFlag(values, flags, CharacterTestRuntimeAiPlannerTargetStateMask.AttackThreatToSelf, "攻击威胁朝向自身");
            AddObservedTargetFlag(values, flags, CharacterTestRuntimeAiPlannerTargetStateMask.AttackInstanceResponded, "攻击实例已响应");
            return string.Join("、", values);
        }

        private static void AddObservedTargetFlag(
            System.Collections.Generic.List<string> values,
            uint flags,
            CharacterTestRuntimeAiPlannerTargetStateMask flag,
            string label)
        {
            if ((flags & (uint)flag) != 0u)
                values.Add(label);
        }

        private static void ApplyBoolean(BehaviorRecipeTriggerConditionViewModel row, bool observed)
        {
            bool satisfied = row.EffectiveOperator == 1
                ? observed != row.BoolValue
                : observed == row.BoolValue;
            row.ObservationState = satisfied
                ? BehaviorRecipeConditionObservationState.Satisfied
                : BehaviorRecipeConditionObservationState.Unsatisfied;
            row.ObservedValueText = observed ? "是" : "否";
        }

        private static void ApplyNumber(
            BehaviorRecipeTriggerConditionViewModel row,
            float observed,
            string unit)
        {
            bool satisfied = row.EffectiveOperator switch
            {
                0 => Math.Abs(observed - row.Value) <= 0.0001f,
                1 => Math.Abs(observed - row.Value) > 0.0001f,
                2 => observed <= row.Value,
                3 => observed > row.Value,
                4 => observed >= row.Value,
                5 => observed < row.Value,
                6 => observed >= row.Value && observed <= row.MaxValue,
                7 => observed < row.Value || observed > row.MaxValue,
                _ => false
            };
            row.ObservationState = satisfied
                ? BehaviorRecipeConditionObservationState.Satisfied
                : BehaviorRecipeConditionObservationState.Unsatisfied;
            row.ObservedValueText = observed.ToString("0.###", CultureInfo.InvariantCulture)
                + (string.IsNullOrEmpty(unit) ? string.Empty : " " + unit);
        }

        private static void MarkNotObserved(
            BehaviorRecipeTriggerConditionViewModel row,
            string reason)
        {
            row.ObservationState = BehaviorRecipeConditionObservationState.NotObserved;
            row.ObservedValueText = "未采样";
            row.ObservationReasonText = reason ?? string.Empty;
        }
    }
}
