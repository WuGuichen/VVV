using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestRuntimeAiPlannerBehaviorCardEditorModel
    {
        public static List<CharacterTestRuntimeAiPlannerBehaviorCardForecastRow> BuildForecast(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft)
        {
            var rows = new List<CharacterTestRuntimeAiPlannerBehaviorCardForecastRow>();
            if (draft == null)
                return rows;

            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview =
                BuildFlowTimePreview(draft);
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview dashPreview =
                FindStepPreview(preview, "dash");
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview secondDashPreview =
                FindStepPreview(preview, "second_dash");
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview attackPreview =
                FindStepPreview(preview, "light_attack");
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview attack2Preview =
                FindStepPreview(preview, "light_attack_2");
            rows.Add(new CharacterTestRuntimeAiPlannerBehaviorCardForecastRow(
                "开局追击",
                dashPreview.StepId.Length > 0 ? dashPreview.Title : "?",
                dashPreview.StepId.Length > 0 ? "UseAbility(DashStrikeAbilityId)" : "无输出",
                dashPreview.StepId.Length > 0
                    ? FormatForecastPath(dashPreview, secondDashPreview, attackPreview, attack2Preview)
                    : "缺少突进步骤。"));
            rows.Add(new CharacterTestRuntimeAiPlannerBehaviorCardForecastRow(
                "第一突进未贴近",
                secondDashPreview.StepId.Length > 0 ? secondDashPreview.Title : "End",
                secondDashPreview.StepId.Length > 0 ? "UseAbility(DashStrikeAbilityId)" : "End",
                secondDashPreview.StepId.Length > 0
                    ? FormatStepWindow(dashPreview)
                        + " default -> "
                        + FormatStepWindow(secondDashPreview)
                        + " condition -> "
                        + FormatTargetOrEnd(FindConditionBranch(preview, secondDashPreview.StepId))
                    : "缺少 second_dash 步骤。"));
            rows.Add(new CharacterTestRuntimeAiPlannerBehaviorCardForecastRow(
                "突进后近身",
                attackPreview.StepId.Length > 0 ? attackPreview.Title : "?",
                attackPreview.StepId.Length > 0 ? "UseAction(light_attack)" : "无输出",
                attackPreview.StepId.Length > 0
                    ? FormatStepWindow(attackPreview)
                        + " -> "
                        + FormatTargetOrEnd(FindConditionBranch(preview, attackPreview.StepId))
                    : "缺少轻攻击步骤。"));
            rows.Add(new CharacterTestRuntimeAiPlannerBehaviorCardForecastRow(
                "连段窗口",
                attack2Preview.StepId.Length > 0 ? attack2Preview.Title : "End",
                attack2Preview.StepId.Length > 0 ? "UseAction(light_attack_2)" : "End",
                attack2Preview.StepId.Length > 0
                    ? FormatStepWindow(attack2Preview) + " -> End"
                    : "未配置第二段。"));
            rows.Add(new CharacterTestRuntimeAiPlannerBehaviorCardForecastRow(
                "突进失败",
                "End",
                "交回行为组",
                dashPreview.StepId.Length > 0
                    ? FormatStepWindow(dashPreview)
                        + " failure -> End | Ability 不可用、目标丢失或距离不满足时结束。"
                    : "Ability 不可用、目标丢失或距离不满足时结束。"));
            rows.Add(new CharacterTestRuntimeAiPlannerBehaviorCardForecastRow(
                "Window missed",
                "End",
                "Return to behavior group",
                attackPreview.StepId.Length > 0
                    ? FormatStepWindow(attackPreview)
                        + " condition missed -> "
                        + FormatTargetOrEnd(FindFailureBranch(preview, attackPreview.StepId))
                    : "light_attack step missing."));
            rows.Add(new CharacterTestRuntimeAiPlannerBehaviorCardForecastRow(
                "Target lost",
                "End",
                "Return to behavior group",
                dashPreview.StepId.Length > 0
                    ? FormatStepWindow(dashPreview)
                        + " failure -> "
                        + FormatTargetOrEnd(FindFailureBranch(preview, dashPreview.StepId))
                    : "dash step missing."));
            if (preview != null)
                return rows;

            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft dash = FindStep(draft, "dash");
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft attack = FindStep(draft, "light_attack");
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft attack2 = FindStep(draft, "light_attack_2");
            rows.Add(new CharacterTestRuntimeAiPlannerBehaviorCardForecastRow(
                "开局追击",
                dash != null ? dash.DisplayTitle : "?",
                dash != null ? "UseAbility(DashStrikeAbilityId)" : "无输出",
                dash != null ? "3-7m 看见目标时突进。" : "缺少突进步骤。"));
            rows.Add(new CharacterTestRuntimeAiPlannerBehaviorCardForecastRow(
                "突进后近身",
                attack != null ? attack.DisplayTitle : "?",
                attack != null ? "UseAction(light_attack)" : "无输出",
                attack != null ? "1m 内请求第一段轻攻击。" : "缺少轻攻击步骤。"));
            rows.Add(new CharacterTestRuntimeAiPlannerBehaviorCardForecastRow(
                "连段窗口",
                attack2 != null ? attack2.DisplayTitle : "结束",
                attack2 != null ? "UseAction(light_attack_2)" : "End",
                attack2 != null ? "切换窗口允许时接第二段。" : "未配置第二段。"));
            rows.Add(new CharacterTestRuntimeAiPlannerBehaviorCardForecastRow(
                "突进失败",
                "End",
                "交回行为组",
                "Ability 不可用、目标丢失或距离不满足时结束。"));
            return rows;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview FindConditionBranch(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId)
        {
            if (preview == null)
                return default;

            for (int i = 0; i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.ConditionMet
                    && string.Equals(branch.FromStepId, stepId, StringComparison.Ordinal))
                {
                    return branch;
                }
            }

            return default;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview FindFailureBranch(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId)
        {
            if (preview == null)
                return default;

            for (int i = 0; i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure
                    && string.Equals(branch.FromStepId, stepId, StringComparison.Ordinal))
                {
                    return branch;
                }
            }

            return default;
        }

        private static string FormatForecastPath(
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview dash,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview secondDash,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview attack,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview attack2)
        {
            var parts = new List<string>();
            if (dash.StepId.Length > 0)
                parts.Add(FormatStepWindow(dash) + " condition light_attack / default second_dash");
            if (secondDash.StepId.Length > 0)
                parts.Add(FormatStepWindow(secondDash) + " condition light_attack");
            if (attack.StepId.Length > 0)
                parts.Add(FormatStepWindow(attack) + " condition window");
            if (attack2.StepId.Length > 0)
                parts.Add(FormatStepWindow(attack2));
            parts.Add("End");
            return string.Join(" -> ", parts.ToArray());
        }

        private static string FormatStepWindow(
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview step)
        {
            if (step.StepId.Length == 0)
                return "missing";

            string duration = step.DurationKnown
                ? step.StartFrame.ToString(CultureInfo.InvariantCulture)
                    + "-"
                    + step.EndFrame.ToString(CultureInfo.InvariantCulture)
                : "?";
            if (!step.HasConditionWindow)
                return step.StepId + "[" + duration + "]";

            return step.StepId
                + "["
                + duration
                + "] window["
                + step.ConditionWindowStartFrame.ToString(CultureInfo.InvariantCulture)
                + "-"
                + step.ConditionWindowEndFrame.ToString(CultureInfo.InvariantCulture)
                + "]";
        }

        private static string FormatTargetOrEnd(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            if (branch.TargetStepId == null || branch.TargetStepId.Length == 0)
                return "End";

            return branch.TargetIsEnd ? "End" : branch.TargetStepId;
        }
    }
}
