using System;
using System.Globalization;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeAiPlannerDevPresentationText
    {
        public static string BuildActionLabel(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (!LooksRawActionName(agent.SelectedActionName))
                return agent.SelectedActionName;

            switch (agent.SelectedActionKind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                    return "接近";
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                    return "侧移";
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    return "撤退";
                case CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack:
                    return "攻击";
                case CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot:
                    return "施放";
                case CharacterTestRuntimeAiPlannerActionKind.Dodge:
                    return "闪避";
                case CharacterTestRuntimeAiPlannerActionKind.Wait:
                    return "等待";
                default:
                    return agent.SelectedActionKind.ToString();
            }
        }

        public static string BuildShortHeadline(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            string targetPart = agent.TargetExists
                ? " | 距离 " + agent.TargetDistance.ToString("0.00", CultureInfo.InvariantCulture)
                : " | 无目标";
            return "智能体 "
                + agent.EntityId.ToString(CultureInfo.InvariantCulture)
                + " | "
                + BuildActionLabel(agent)
                + targetPart;
        }

        public static string BuildShortStatus(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (!agent.TargetExists)
                return "无目标";

            return BuildLosLabel(agent)
                + " | "
                + BuildPathLabel(agent)
                + " | "
                + (agent.TargetInAttackRange ? "范围内" : "范围外");
        }

        public static string BuildLosLabel(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (!agent.TargetVisible)
                return "视线未知";
            return agent.TargetLineOfSightBlocked ? "视线受阻" : "视线畅通";
        }

        public static string BuildPathLabel(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (!agent.MovePathAvailable)
                return "无路径";
            return agent.MoveDirectPathClear ? "路径直达" : "路径需转向";
        }

        /// <summary>Returns a Chinese-first stage label while preserving authored Chinese names.</summary>
        /// <remarks>LowFreqAlloc: presentation-only normalization during UI refresh.</remarks>
        public static string BuildBehaviorStageLabel(
            string title,
            CharacterTestRuntimeAiLabCardStepKind kind)
        {
            string value = title?.Trim() ?? string.Empty;
            if (string.Equals(value, "Targeting.NearestEnemy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "NearestEnemy", StringComparison.OrdinalIgnoreCase))
            {
                return "锁定最近敌人";
            }
            if (string.Equals(
                    value,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "持续靠近";
            }
            if (ContainsChinese(value))
                return value;
            switch (kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.Logic:
                    if (string.Equals(value, "Observe", StringComparison.OrdinalIgnoreCase))
                        return "观察";
                    if (string.Equals(value, "Wait", StringComparison.OrdinalIgnoreCase))
                        return "等待";
                    return "逻辑判断";
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                    return "使用能力";
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    return "执行动作";
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                    return "移动至目标";
                case CharacterTestRuntimeAiLabCardStepKind.Wait:
                    return "等待";
                case CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly:
                    return "执行兼容动作";
                default:
                    return value.Length == 0 ? "逻辑阶段" : value;
            }
        }

        /// <summary>Returns the player-facing gameplay verb shown on the plan action track.</summary>
        /// <remarks>LowFreqAlloc: called only while rebuilding the gameplay-plan projection.</remarks>
        public static string BuildBehaviorGameplayActionLabel(
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            if (stage == null)
                return "没有可执行动作";
            string title = stage.Title?.Trim() ?? string.Empty;
            if (string.Equals(title, "Targeting.NearestEnemy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "NearestEnemy", StringComparison.OrdinalIgnoreCase))
            {
                return "选择最近敌人";
            }
            switch (stage.Kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                    return stage.AbilityId > 0
                        ? "使用能力 " + stage.AbilityId.ToString(CultureInfo.InvariantCulture)
                        : "使用能力";
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    return BuildCharacterActionLabel(
                        stage.CharacterActionId,
                        stage.CharacterActionDisplayName);
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                    return "移动至目标";
                case CharacterTestRuntimeAiLabCardStepKind.Wait:
                    return stage.WaitFrames > 0
                        ? "等待 " + stage.WaitFrames.ToString(CultureInfo.InvariantCulture) + " 帧"
                        : "等待下一次决策";
                case CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly:
                    return BuildLegacyGameplayActionLabel(stage.LegacyActionKind);
                case CharacterTestRuntimeAiLabCardStepKind.Logic:
                    if (string.Equals(title, "Observe", StringComparison.OrdinalIgnoreCase))
                        return "观察目标";
                    if (string.Equals(title, "Wait", StringComparison.OrdinalIgnoreCase))
                        return "等待条件";
                    return ContainsChinese(title)
                        ? title
                        : "逻辑判断";
                default:
                    return "执行当前阶段";
            }
        }

        /// <summary>Returns a compact Chinese-first label for an authored CharacterAction.</summary>
        /// <remarks>LowFreqAlloc only for an unknown authored display name.</remarks>
        internal static string BuildCharacterActionLabel(CharacterActionId actionId, string displayName)
        {
            string value = displayName?.Trim() ?? string.Empty;
            if (!actionId.IsValid)
                return value.Length > 0 ? value + "（未配置）" : "角色动作（未配置）";
            return value.Length > 0
                ? value + "（" + actionId + "）"
                : "动作 " + actionId;
        }

        /// <summary>Returns a Chinese-first detector label for the plan timeline.</summary>
        /// <remarks>NoAlloc for known labels; LowFreqAlloc only when trimming authored text.</remarks>
        public static string BuildBehaviorDetectorLabel(string title, bool cardScope)
        {
            string value = title?.Trim() ?? string.Empty;
            if (string.Equals(value, "Complete", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return "完成条件";
            }
            if (string.Equals(value, "Failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Failure", StringComparison.OrdinalIgnoreCase))
            {
                return "失败条件";
            }
            if (string.Equals(value, "Target visible", StringComparison.OrdinalIgnoreCase))
                return "目标可见";
            if (ContainsChinese(value))
                return value;
            return value.Length == 0
                ? cardScope ? "卡级条件" : "阶段条件"
                : value;
        }

        /// <summary>Returns a Chinese-first transition event label for the plan timeline.</summary>
        /// <remarks>LowFreqAlloc: presentation-only normalization during geometry rebuild.</remarks>
        public static string BuildBehaviorTransitionLabel(string title)
        {
            string value = title?.Trim() ?? string.Empty;
            bool backEdge = value.StartsWith("↩ ", StringComparison.Ordinal);
            string core = backEdge ? value.Substring(2).TrimStart() : value;
            string localized;
            if (string.Equals(core, "Complete", StringComparison.OrdinalIgnoreCase)
                || string.Equals(core, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                localized = "阶段完成";
            }
            else if (string.Equals(core, "Failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(core, "Failure", StringComparison.OrdinalIgnoreCase))
            {
                localized = "阶段失败";
            }
            else if (string.Equals(core, "Timeout", StringComparison.OrdinalIgnoreCase))
            {
                localized = "等待超时";
            }
            else
            {
                localized = core.Length == 0 ? "阶段切换" : core;
            }
            return backEdge ? "回跳：" + localized : localized;
        }

        private static bool LooksRawActionName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;
            if (value.Length > 18)
                return true;
            return value.IndexOf('.') >= 0 || value.IndexOf('/') >= 0 || value.IndexOf('\\') >= 0;
        }

        private static string BuildLegacyGameplayActionLabel(
            CharacterTestRuntimeAiPlannerActionKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                    return "接近目标";
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                    return "围绕目标侧移";
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    return "向后撤离";
                case CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack:
                    return "执行普通攻击";
                case CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot:
                    return "施放已选择能力";
                case CharacterTestRuntimeAiPlannerActionKind.Dodge:
                    return "执行闪避";
                default:
                    return "等待下一次决策";
            }
        }

        private static bool ContainsChinese(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character >= '\u3400' && character <= '\u9fff')
                    return true;
            }
            return false;
        }
    }
}
