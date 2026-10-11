using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        private static void FillSelectedAgent(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents,
            int selectedAgentIndex,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame)
        {
            if (agents == null || agents.Count == 0)
            {
                viewModel.SelectedAgentText = "尚未选择智能体。";
                viewModel.FactsText = "事实数据不可用。";
                viewModel.PathText = "路径数据不可用。";
                viewModel.UtilityText = "效用数据不可用。";
                viewModel.CommandText = "命令数据不可用。";
                viewModel.DiagnosticsText = "运行时智能规划快照中没有智能体。";
                return;
            }

            CharacterTestRuntimeAiPlannerAgentSnapshot agent = agents[selectedAgentIndex];
            viewModel.SelectedAgentText =
                "帧 "
                + (selectedFrame != null ? selectedFrame.Frame.ToString(CultureInfo.InvariantCulture) : "-")
                + " | 智能体 "
                + agent.EntityId.ToString(CultureInfo.InvariantCulture)
                + " | 状态 "
                + NonEmpty(agent.ActiveStatePath, "无");
            viewModel.FactsText =
                "目标存在="
                + FormatBool(agent.TargetExists)
                + " id="
                + agent.TargetEntityId.ToString(CultureInfo.InvariantCulture)
                + " 可见="
                + FormatBool(agent.TargetVisible)
                + " 死亡="
                + FormatBool(agent.TargetDead)
                + " 受阻="
                + FormatBool(agent.TargetLineOfSightBlocked)
                + " 距离="
                + agent.TargetDistance.ToString("0.000", CultureInfo.InvariantCulture)
                + "\n攻击威胁="
                + (!agent.TargetAttackActionId.IsValid ? "无" : agent.TargetAttackActionId.ToString())
                + " 预计命中="
                + (agent.TargetAttackImpactFrames >= 0
                    ? agent.TargetAttackImpactFrames.ToString(CultureInfo.InvariantCulture) + "帧"
                    : "-")
                + " 会命中="
                + FormatBool(agent.TargetAttackWillHitSelf)
                + " 已响应="
                + FormatBool(agent.TargetAttackInstanceResponded)
                + " 冷却就绪="
                + FormatBool(agent.SelfDodgeCooldownReady);
            viewModel.PathText = BuildNavigationPathText(agent);
            viewModel.UtilityText =
                "动作 "
                + CharacterTestRuntimeAiPlannerDevPresentationText.BuildActionLabel(agent)
                + " | 分数="
                + agent.SelectedUtilityScore.ToString("0.###", CultureInfo.InvariantCulture)
                + " 代价="
                + agent.SelectedActionCost.ToString("0.###", CultureInfo.InvariantCulture)
                + " | 槽位="
                + agent.RuntimeAiPlannerProfileSlot.ToString(CultureInfo.InvariantCulture)
                + " loadout="
                + agent.RuntimeAiBehaviorLoadoutId.ToString(CultureInfo.InvariantCulture)
                + "/"
                + agent.RuntimeAiBehaviorVariantId.ToString(CultureInfo.InvariantCulture);
            viewModel.CommandText =
                "命令："
                + NonEmpty(agent.LastCommand, "无")
                + " | 拒绝="
                + NonEmpty(agent.RejectReason, "无")
                + " | 抑制="
                + NonEmpty(agent.SuppressedReason, "无");
            viewModel.DiagnosticsText =
                "状态转换 "
                + NonEmpty(agent.TransitionFromStateId, "无")
                + " -> "
                + NonEmpty(agent.TransitionToStateId, "无")
                + " | 报告 "
                + ResolveReportStatus(selectedFrame);
        }

        internal static string BuildNavigationPathText(
            CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            return "路径可用="
                + FormatBool(agent.MovePathAvailable)
                + " 直达="
                + FormatBool(agent.MoveDirectPathClear)
                + " 候选="
                + agent.MoveCandidateIndex.ToString(CultureInfo.InvariantCulture)
                + " 危险="
                + FormatBool(agent.EnvironmentNearHazard)
                + " 障碍="
                + FormatBool(agent.EnvironmentNearObstacle)
                + CharacterTestRuntimeAiPlannerNavigationDiagnosticText.BuildCompact(agent);
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0)
                return 0;
            if (index < 0)
                return 0;
            return index < count ? index : count - 1;
        }

        private static string ResolveReportStatus(CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame)
        {
            string report = selectedFrame?.RuntimeAiPlannerSnapshot?.ValidationReport;
            return string.IsNullOrWhiteSpace(report) ? "无" : "可用";
        }

        private static string NonEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string NonEmpty(int value, string fallback)
        {
            return value <= 0 ? fallback : value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatBool(bool value)
        {
            return value ? "是" : "否";
        }

        private static string L(IMxUiTextProvider textProvider, string key, string fallback)
        {
            return CharacterTestUiText.Resolve(textProvider, key, fallback);
        }
    }
}
