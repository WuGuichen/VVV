using System;
using System.Globalization;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal enum CharacterTestRuntimeAiPlannerPlayMonitorSeverity
    {
        Normal = 0,
        Warning = 1,
        Error = 2
    }

    internal readonly struct CharacterTestRuntimeAiPlannerPlayMonitorSnapshotEntry
    {
        public CharacterTestRuntimeAiPlannerPlayMonitorSnapshotEntry(
            string targetName,
            CharacterTestRuntimeAiPlannerSnapshot snapshot)
        {
            TargetName = string.IsNullOrWhiteSpace(targetName)
                ? CharacterTestRuntimeAiPlannerDebugSource.DefaultSourceName
                : targetName;
            Snapshot = snapshot;
        }

        public string TargetName { get; }
        public CharacterTestRuntimeAiPlannerSnapshot Snapshot { get; }

        public int AgentCount => Snapshot != null && Snapshot.Agents != null ? Snapshot.Agents.Count : 0;
    }

    internal readonly struct CharacterTestRuntimeAiPlannerPlayMonitorAgentEntry
    {
        public CharacterTestRuntimeAiPlannerPlayMonitorAgentEntry(
            string targetName,
            CharacterTestRuntimeAiPlannerAgentSnapshot snapshot,
            CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel viewModel)
        {
            TargetName = string.IsNullOrWhiteSpace(targetName)
                ? CharacterTestRuntimeAiPlannerDebugSource.DefaultSourceName
                : targetName;
            Snapshot = snapshot;
            ViewModel = viewModel;
        }

        public string TargetName { get; }
        public CharacterTestRuntimeAiPlannerAgentSnapshot Snapshot { get; }
        public CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel ViewModel { get; }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel
    {
        private const string SuppressedNone = "None";

        private CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel(
            string sourceName,
            string agentText,
            string stateText,
            string targetText,
            string actionText,
            string distanceText,
            string visibilityText,
            string pathText,
            string scoreCostText,
            string commandText,
            string decisionHeadline,
            string conditionSummary,
            string targetDetailText,
            string movementDetailText,
            string utilityDetailText,
            string commandDetailText,
            string behaviorLoadoutText,
            string playerStatusText,
            string actionChainText,
            string rawFactsText,
            string issueText,
            string transitionText,
            CharacterTestRuntimeAiPlannerPlayMonitorSeverity severity)
        {
            SourceName = sourceName;
            AgentText = agentText;
            StateText = stateText;
            TargetText = targetText;
            ActionText = actionText;
            DistanceText = distanceText;
            VisibilityText = visibilityText;
            PathText = pathText;
            ScoreCostText = scoreCostText;
            CommandText = commandText;
            DecisionHeadline = decisionHeadline;
            ConditionSummary = conditionSummary;
            TargetDetailText = targetDetailText;
            MovementDetailText = movementDetailText;
            UtilityDetailText = utilityDetailText;
            CommandDetailText = commandDetailText;
            BehaviorLoadoutText = behaviorLoadoutText;
            PlayerStatusText = playerStatusText;
            ActionChainText = actionChainText;
            RawFactsText = rawFactsText;
            IssueText = issueText;
            TransitionText = transitionText;
            Severity = severity;
        }

        public string SourceName { get; }
        public string AgentText { get; }
        public string StateText { get; }
        public string TargetText { get; }
        public string ActionText { get; }
        public string DistanceText { get; }
        public string VisibilityText { get; }
        public string PathText { get; }
        public string ScoreCostText { get; }
        public string CommandText { get; }
        public string DecisionHeadline { get; }
        public string ConditionSummary { get; }
        public string TargetDetailText { get; }
        public string MovementDetailText { get; }
        public string UtilityDetailText { get; }
        public string CommandDetailText { get; }
        public string BehaviorLoadoutText { get; }
        public string PlayerStatusText { get; }
        public string ActionChainText { get; }
        public string RawFactsText { get; }
        public string IssueText { get; }
        public string TransitionText { get; }
        public CharacterTestRuntimeAiPlannerPlayMonitorSeverity Severity { get; }

        public bool HasIssue => Severity != CharacterTestRuntimeAiPlannerPlayMonitorSeverity.Normal;

        public static CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel Create(
            string sourceName,
            CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            string safeSource = EmptyAsNone(sourceName);
            string state = EmptyAsNone(agent.ActiveStatePath);
            string target = agent.TargetExists ? "#" + agent.TargetEntityId.ToString(CultureInfo.InvariantCulture) : "无目标";
            string action = agent.SelectedActionId > 0
                ? EmptyAsNone(agent.SelectedActionName) + " #" + agent.SelectedActionId.ToString(CultureInfo.InvariantCulture)
                : "无动作";
            string distance = agent.TargetExists
                ? agent.TargetDistance.ToString("0.##", CultureInfo.InvariantCulture)
                : "-";
            string visibility = agent.TargetExists
                ? (agent.TargetVisible ? "可见" : "不可见")
                    + " / "
                    + (agent.TargetLineOfSightBlocked ? "遮挡" : "通畅")
                : "-";
            string path = agent.MovePathAvailable
                ? agent.MoveDirectPathClear
                    ? "路径可用 / 直线"
                    : "路径可用 / 候选 " + agent.MoveCandidateIndex.ToString(CultureInfo.InvariantCulture)
                : "路径不可用";
            path += CharacterTestRuntimeAiPlannerNavigationDiagnosticText.BuildCompact(agent);
            string scoreCost = agent.SelectedUtilityScore.ToString("0.###", CultureInfo.InvariantCulture)
                + " / "
                + agent.SelectedActionCost.ToString("0.###", CultureInfo.InvariantCulture);
            string command = EmptyAsNone(agent.LastCommand);
            string transition = agent.TransitionToStateId <= 0
                ? "none"
                : (agent.TransitionFromStateId > 0 ? agent.TransitionFromStateId.ToString(CultureInfo.InvariantCulture) : "none")
                    + " -> " + agent.TransitionToStateId.ToString(CultureInfo.InvariantCulture);
            string decision = BuildDecisionHeadline(agent, transition);
            string condition = BuildConditionSummary(agent);
            CharacterTestRuntimeAiPlannerPlayMonitorSeverity severity = DetermineSeverity(agent);
            string issue = BuildIssueText(agent, severity);

            return new CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel(
                safeSource,
                "Agent " + agent.EntityId.ToString(CultureInfo.InvariantCulture),
                state,
                target,
                action,
                distance,
                visibility,
                path,
                scoreCost,
                command,
                decision,
                condition,
                BuildTargetDetail(agent),
                BuildMovementDetail(agent),
                BuildUtilityDetail(agent),
                BuildCommandDetail(agent, transition),
                BuildBehaviorLoadoutText(agent),
                CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationCatalog.BuildCombatStatusLabel(agent),
                CharacterTestRuntimeAiPlannerPlayMonitorActionChainCache.BuildSelectedActionChainText(agent),
                BuildRawFacts(agent),
                issue,
                transition,
                severity);
        }

        private static CharacterTestRuntimeAiPlannerPlayMonitorSeverity DetermineSeverity(
            CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (!string.IsNullOrWhiteSpace(agent.RejectReason))
                return CharacterTestRuntimeAiPlannerPlayMonitorSeverity.Error;
            if (HasSuppressedReason(agent.SuppressedReason)
                || agent.SelectedActionId <= 0
                || !agent.TargetExists
                || !agent.MovePathAvailable)
            {
                return CharacterTestRuntimeAiPlannerPlayMonitorSeverity.Warning;
            }

            if (agent.TargetLineOfSightBlocked || agent.EnvironmentNearHazard)
                return CharacterTestRuntimeAiPlannerPlayMonitorSeverity.Warning;

            return CharacterTestRuntimeAiPlannerPlayMonitorSeverity.Normal;
        }

        private static string BuildIssueText(
            CharacterTestRuntimeAiPlannerAgentSnapshot agent,
            CharacterTestRuntimeAiPlannerPlayMonitorSeverity severity)
        {
            if (!string.IsNullOrWhiteSpace(agent.RejectReason))
                return "命令被拒绝: " + agent.RejectReason;
            if (HasSuppressedReason(agent.SuppressedReason))
                return "命令未提交: " + agent.SuppressedReason;
            if (agent.SelectedActionId <= 0)
                return "未选中可执行动作";
            if (!agent.TargetExists)
                return "当前无目标";
            if (!agent.MovePathAvailable)
                return "移动路径不可用";
            if (agent.TargetLineOfSightBlocked)
                return "目标视线被遮挡";
            if (agent.EnvironmentNearHazard)
                return "靠近危险区域";

            return severity == CharacterTestRuntimeAiPlannerPlayMonitorSeverity.Normal ? "正常" : "需要关注";
        }

        private static string BuildDecisionHeadline(
            CharacterTestRuntimeAiPlannerAgentSnapshot agent,
            string transition)
        {
            if (!string.IsNullOrWhiteSpace(agent.RejectReason))
                return "命令被拒绝: " + agent.RejectReason;

            if (HasSuppressedReason(agent.SuppressedReason))
                return "未提交命令: " + agent.SuppressedReason;

            if (agent.SelectedActionId <= 0)
                return "没有选中可执行动作。";

            string transitionText = string.Equals(transition, "none", StringComparison.Ordinal)
                ? "无状态切换"
                : "状态切换 " + transition;
            return transitionText
                + "；Utility 选择 "
                + EmptyAsNone(agent.SelectedActionName)
                + "，score="
                + agent.SelectedUtilityScore.ToString("0.###", CultureInfo.InvariantCulture)
                + "，cost="
                + agent.SelectedActionCost.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string BuildConditionSummary(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            return "目标"
                + (agent.TargetExists ? "存在" : "缺失")
                + "，"
                + (agent.TargetVisible ? "可见" : "不可见")
                + "，"
                + (agent.TargetLineOfSightBlocked ? "视线遮挡" : "视线通畅")
                + "，"
                + (agent.TargetInAttackRange ? "在攻击范围" : "不在攻击范围")
                + "；路径"
                + (agent.MovePathAvailable ? "可用" : "不可用")
                + "，危险="
                + FormatBool(agent.EnvironmentNearHazard)
                + "，可利用物="
                + FormatBool(agent.EnvironmentNearUsefulObject);
        }

        private static string BuildTargetDetail(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            return "目标: "
                + (agent.TargetExists ? "#" + agent.TargetEntityId.ToString(CultureInfo.InvariantCulture) : "none")
                + "\n距离: "
                + (agent.TargetExists ? agent.TargetDistance.ToString("0.###", CultureInfo.InvariantCulture) : "-")
                + "\n可见/遮挡: "
                + FormatBool(agent.TargetVisible)
                + " / "
                + FormatBool(agent.TargetLineOfSightBlocked)
                + "\n攻击范围: "
                + FormatBool(agent.TargetInAttackRange)
                + "\nlastSeen: "
                + agent.TargetLastSeenAgeFrames.ToString(CultureInfo.InvariantCulture)
                + "\nthreat: "
                + agent.TargetThreatScore.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string BuildMovementDetail(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            return "路径可用: "
                + FormatBool(agent.MovePathAvailable)
                + "\n直线路径: "
                + FormatBool(agent.MoveDirectPathClear)
                + "\n候选索引: "
                + agent.MoveCandidateIndex.ToString(CultureInfo.InvariantCulture)
                + "\n移动风格: speed="
                + agent.MovementSpeedScale.ToString("0.###", CultureInfo.InvariantCulture)
                + ", side="
                + agent.MovementStrafeSide.ToString(CultureInfo.InvariantCulture)
                + ", hold="
                + agent.MovementDirectionHoldFrames.ToString(CultureInfo.InvariantCulture)
                + ", reaction="
                + agent.MovementReactionDelayFrames.ToString(CultureInfo.InvariantCulture)
                + "\n环境: obstacle="
                + FormatBool(agent.EnvironmentNearObstacle)
                + ", hazard="
                + FormatBool(agent.EnvironmentNearHazard)
                + ", useful="
                + FormatBool(agent.EnvironmentNearUsefulObject)
                + CharacterTestRuntimeAiPlannerNavigationDiagnosticText.BuildMultiline(agent);
        }

        private static string BuildUtilityDetail(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            return "动作: "
                + EmptyAsNone(agent.SelectedActionName)
                + " #"
                + agent.SelectedActionId.ToString(CultureInfo.InvariantCulture)
                + "\n类型: "
                + agent.SelectedActionKind
                + "\nscore/cost: "
                + agent.SelectedUtilityScore.ToString("0.###", CultureInfo.InvariantCulture)
                + " / "
                + agent.SelectedActionCost.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string BuildCommandDetail(
            CharacterTestRuntimeAiPlannerAgentSnapshot agent,
            string transition)
        {
            return "last="
                + EmptyAsNone(agent.LastCommand)
                + "\nreject="
                + EmptyAsNone(agent.RejectReason)
                + "\nsuppress="
                + EmptyAsNone(agent.SuppressedReason)
                + "\ntransition="
                + transition;
        }

        private static string BuildBehaviorLoadoutText(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (agent.RuntimeAiBehaviorLoadoutId <= 0 || agent.RuntimeAiBehaviorVariantId <= 0)
            {
                return "legacy profileSlot="
                    + agent.RuntimeAiPlannerProfileSlot.ToString(CultureInfo.InvariantCulture);
            }

            return "loadout="
                + agent.RuntimeAiBehaviorLoadoutId.ToString(CultureInfo.InvariantCulture)
                + " variant="
                + agent.RuntimeAiBehaviorVariantId.ToString(CultureInfo.InvariantCulture)
                + " slot="
                + agent.RuntimeAiBehaviorProfileSlot.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildRawFacts(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            return "target.exists="
                + FormatBool(agent.TargetExists)
                + "\ntarget.visible="
                + FormatBool(agent.TargetVisible)
                + "\ntarget.lineOfSightBlocked="
                + FormatBool(agent.TargetLineOfSightBlocked)
                + "\ntarget.inAttackRange="
                + FormatBool(agent.TargetInAttackRange)
                + "\ntarget.lastSeenAgeFrames="
                + agent.TargetLastSeenAgeFrames.ToString(CultureInfo.InvariantCulture)
                + "\ntarget.threatScore="
                + agent.TargetThreatScore.ToString("0.###", CultureInfo.InvariantCulture)
                + "\nmove.pathAvailable="
                + FormatBool(agent.MovePathAvailable)
                + "\nmove.directPathClear="
                + FormatBool(agent.MoveDirectPathClear)
                + "\nmove.candidateIndex="
                + agent.MoveCandidateIndex.ToString(CultureInfo.InvariantCulture)
                + "\nself.surfaceKind="
                + agent.SelfSurfaceKind.ToString(CultureInfo.InvariantCulture)
                + "\nself.onHardSlick="
                + FormatBool(agent.SelfOnHardSlick)
                + "\nself.nearMovableObject="
                + FormatBool(agent.SelfNearMovableObject)
                + "\nenvironment.nearObstacle="
                + FormatBool(agent.EnvironmentNearObstacle)
                + "\nenvironment.nearHazard="
                + FormatBool(agent.EnvironmentNearHazard)
                + "\nenvironment.nearUsefulObject="
                + FormatBool(agent.EnvironmentNearUsefulObject)
                + "\nruntimeAi.profileSlot="
                + agent.RuntimeAiPlannerProfileSlot.ToString(CultureInfo.InvariantCulture)
                + "\nruntimeAi.behaviorLoadoutId="
                + agent.RuntimeAiBehaviorLoadoutId.ToString(CultureInfo.InvariantCulture)
                + "\nruntimeAi.behaviorVariantId="
                + agent.RuntimeAiBehaviorVariantId.ToString(CultureInfo.InvariantCulture)
                + "\nruntimeAi.behaviorCatalogHash="
                + agent.RuntimeAiBehaviorCatalogHash.ToString(CultureInfo.InvariantCulture)
                + "\nruntimeAi.behaviorProfileSlot="
                + agent.RuntimeAiBehaviorProfileSlot.ToString(CultureInfo.InvariantCulture)
                + "\nruntimeAi.selectedAbilitySlot="
                + agent.SelectedAbilitySlot.ToString(CultureInfo.InvariantCulture);
        }

        private static bool HasSuppressedReason(string reason)
        {
            return !string.IsNullOrWhiteSpace(reason)
                && !string.Equals(reason, SuppressedNone, StringComparison.Ordinal);
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }

        private static string FormatBool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
