using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestRuntimeAiPlannerBehaviorCardEditorModel
    {
        public const int MaxStepCount = 5;
        public const int MinStepCount = 1;
        private const int UnknownDurationFrames = 24;

        public static List<CharacterTestRuntimeAiPlannerBehaviorCardDraft> CreateBuiltInDrafts()
        {
            return new List<CharacterTestRuntimeAiPlannerBehaviorCardDraft>
            {
                CreateDashStrikePursuitDraft()
            };
        }

        public static CharacterTestRuntimeAiPlannerBehaviorCardDraft CreateDashStrikePursuitDraft()
        {
            var draft = new CharacterTestRuntimeAiPlannerBehaviorCardDraft(
                "draft.pursuit.dash_strike",
                "Pursuit.DashStrike",
                "突进追击")
            {
                Category = "Pursuit / 追击",
                CapabilityText = "Pursuit + Damage | counters Kite/Defense",
                IntentText = "目标在中距离且可见时突进，近身后接轻攻击链。",
                TriggerSummary = "目标存在且可见；目标距离 3-7m；自身空闲；DashStrike 可用。",
                OutputSummary = "DashStrikeAbilityId -> second_dash -> light_attack -> light_attack_2",
                RiskText = "依赖 DashStrike Ability 和 CharacterAction 切换窗口。",
                SourceText = "内置示例草稿 / 未发布"
            };

            draft.Steps.Add(new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                "dash",
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility)
            {
                Title = "3-7m 看见目标时突进",
                ConditionSummary = "target.exists && target.visible && 3m <= target.distance <= 7m && self idle",
                AbilityId = CharacterTestAbilityIds.DashStrikeAbilityId,
                OnSuccess = "second_dash",
                OnFailure = "End",
                OnConditionMet = "light_attack",
                EstimatedDurationFrames = 54,
                ConditionWindowStartFrame = 0,
                ConditionWindowEndFrame = 54,
                ConditionWindowSummary = "突进动作完成后进入近身判断",
                Note = "请求 DashStrike；Ability 是否真正释放由 Ability runtime 判断。"
            });
            draft.Steps.Add(new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                "second_dash",
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility)
            {
                Title = "第一次突进后仍未进 1m 时二段突进",
                ConditionSummary = "target.exists && target.visible && target.distance > 1m && target.distance <= 7m && self idle",
                AbilityId = CharacterTestAbilityIds.DashStrikeAbilityId,
                OnSuccess = "End",
                OnFailure = "End",
                OnConditionMet = "light_attack",
                EstimatedDurationFrames = 54,
                ConditionWindowStartFrame = 0,
                ConditionWindowEndFrame = 54,
                ConditionWindowSummary = "二段突进后如果 target.distance <= 1m，则进入轻攻击。",
                Note = "第一段突进没能贴近时的追击补偿分支。"
            });
            draft.Steps.Add(new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                "light_attack",
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction)
            {
                Title = "突进后 1m 内接轻攻击",
                ConditionSummary = "target.distance <= 1m && transition window allows",
                CharacterActionId = new CharacterActionId(301),
                OnSuccess = "light_attack_2",
                OnFailure = "End",
                OnConditionMet = "light_attack_2",
                EstimatedDurationFrames = 76,
                ConditionWindowStartFrame = 8,
                ConditionWindowEndFrame = 18,
                ConditionWindowSummary = "目标仍在 1m 内且 CharacterAction 切换窗口允许",
                Note = "第一段轻攻击；不能绕过 CharacterAction 切换窗口。"
            });
            draft.Steps.Add(new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                "light_attack_2",
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction)
            {
                Title = "连段窗口允许时接第二段",
                ConditionSummary = "target.distance <= 1m && light_attack transition window allows",
                CharacterActionId = new CharacterActionId(324),
                OnSuccess = "End",
                OnFailure = "End",
                OnConditionMet = "End",
                EstimatedDurationFrames = 76,
                ConditionWindowStartFrame = 8,
                ConditionWindowEndFrame = 18,
                ConditionWindowSummary = "第二段结束后显式 End，交回行为组重新决策",
                EndReason = "连段完成，交回行为组重新决策",
                Note = "第二段轻攻击只在动作切换窗口允许时请求。"
            });

            EnsureStructuredBranches(draft);
            ConfigureDashStrikePursuitBranches(draft);
            return draft;
        }

        private static void ConfigureDashStrikePursuitBranches(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft)
        {
            if (draft == null)
                return;

            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft dash =
                FindDraftStep(draft, "dash");
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft secondDash =
                FindDraftStep(draft, "second_dash");

            ConfigureBranch(
                dash,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition,
                "已进 1m",
                "突进后 target.distance <= 1m，进入轻攻击。",
                "light_attack",
                10);
            ConfigureBranch(
                dash,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess,
                "未进 1m",
                "突进完成但仍未贴近，进入二段突进。",
                "second_dash",
                100);
            ConfigureBranch(
                dash,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure,
                "失败/中断",
                "Ability 不可用、目标丢失或突进中断时结束。",
                "End",
                0);

            ConfigureBranch(
                secondDash,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition,
                "二段后进 1m",
                "二段突进后 target.distance <= 1m，进入轻攻击。",
                "light_attack",
                10);
            ConfigureBranch(
                secondDash,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess,
                "仍未进 1m",
                "二段突进后仍未贴近，结束本行为并交回行为组重新决策。",
                "End",
                100);
            ConfigureBranch(
                secondDash,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure,
                "失败/中断",
                "二段突进不可用、目标丢失或突进中断时结束。",
                "End",
                0);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardStepDraft FindDraftStep(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            string stepId)
        {
            if (draft == null || string.IsNullOrWhiteSpace(stepId))
                return null;

            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step != null && string.Equals(step.StepId, stepId, StringComparison.Ordinal))
                    return step;
            }

            return null;
        }

        private static void ConfigureBranch(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType triggerType,
            string label,
            string conditionSummary,
            string targetStepId,
            int priority)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch =
                FindFirstBranch(step, triggerType);
            if (branch == null)
                return;

            branch.Label = label ?? branch.Label;
            branch.ConditionSummary = conditionSummary ?? branch.ConditionSummary;
            branch.TargetStepId = string.IsNullOrWhiteSpace(targetStepId)
                ? "End"
                : targetStepId;
            branch.Priority = priority;
        }

        public static List<CharacterTestRuntimeAiPlannerBehaviorCardDraft> FilterDrafts(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCardDraft> drafts,
            string searchText,
            string capabilityFilter)
        {
            var results = new List<CharacterTestRuntimeAiPlannerBehaviorCardDraft>();
            if (drafts == null)
                return results;

            string search = searchText ?? string.Empty;
            string filter = capabilityFilter ?? string.Empty;
            for (int i = 0; i < drafts.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardDraft draft = drafts[i];
                if (draft == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(filter)
                    && !string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase)
                    && draft.CapabilityText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && draft.Category.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(search)
                    && draft.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                    && draft.StableId.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                    && draft.IntentText.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                    && draft.OutputSummary.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                results.Add(draft);
            }

            return results;
        }

        public static int CountExecutableSteps(CharacterTestRuntimeAiPlannerBehaviorCardDraft draft)
        {
            if (draft == null)
                return 0;

            int count = 0;
            for (int i = 0; i < draft.Steps.Count; i++)
            {
                if (draft.Steps[i].Kind != CharacterTestRuntimeAiPlannerBehaviorCardStepKind.End)
                    count++;
            }

            return count;
        }

    }
}
