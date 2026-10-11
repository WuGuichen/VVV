using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal enum CharacterTestRuntimeAiPlannerScenePreviewMode
    {
        SelectedAgent = 0,
        AllAgents = 1
    }

    internal enum CharacterTestRuntimeAiPlannerScenePreviewLabelMode
    {
        Off = 0,
        Issues = 1,
        Selected = 2,
        All = 3
    }

    internal enum CharacterTestRuntimeAiPlannerScenePreviewSeverity
    {
        Normal = 0,
        Warning = 1,
        Error = 2,
        Inactive = 3
    }

    internal readonly struct CharacterTestRuntimeAiPlannerScenePreviewItem
    {
        public CharacterTestRuntimeAiPlannerScenePreviewItem(
            string sourceName,
            int agentEntityId,
            int targetEntityId,
            string stateText,
            string actionText,
            bool targetExists,
            bool targetInAttackRange,
            bool targetVisible,
            bool targetBlocked,
            bool movePathAvailable,
            bool moveDirectPathClear,
            int moveCandidateIndex,
            bool environmentNearHazard,
            string rejectReason,
            float targetDistance,
            float utilityScore,
            float actionCost,
            bool hasGeometry,
            Vector3 sourceCenter,
            Vector3 targetCenter,
            Vector2 targetDirection,
            Vector2 decisionMove,
            float attackRange,
            string decisionHeadline,
            string conditionSummary,
            string issueText,
            string visibilityText,
            string pathText,
            string rangeText,
            string moveText,
            CharacterTestRuntimeAiPlannerScenePreviewSeverity severity,
            bool hasTargetDistance = true,
            bool hasDistanceWitness = false,
            Vector3 distanceSourcePoint = default,
            Vector3 distanceTargetPoint = default,
            float targetRootDistance = 0f,
            long targetDistanceGeometryFrame = 0L,
            string targetLogicalHurtboxId = "")
        {
            SourceName = sourceName ?? string.Empty;
            AgentEntityId = agentEntityId;
            TargetEntityId = targetEntityId;
            StateText = stateText ?? string.Empty;
            ActionText = actionText ?? string.Empty;
            TargetExists = targetExists;
            TargetInAttackRange = targetInAttackRange;
            TargetVisible = targetVisible;
            TargetBlocked = targetBlocked;
            MovePathAvailable = movePathAvailable;
            MoveDirectPathClear = moveDirectPathClear;
            MoveCandidateIndex = moveCandidateIndex;
            EnvironmentNearHazard = environmentNearHazard;
            RejectReason = rejectReason ?? string.Empty;
            TargetDistance = targetDistance;
            HasTargetDistance = hasTargetDistance && hasGeometry && targetExists;
            HasDistanceWitness = hasDistanceWitness && HasTargetDistance;
            DistanceSourcePoint = HasDistanceWitness ? distanceSourcePoint : sourceCenter;
            DistanceTargetPoint = HasDistanceWitness ? distanceTargetPoint : targetCenter;
            TargetRootDistance = targetRootDistance > 0f ? targetRootDistance : 0f;
            TargetDistanceGeometryFrame = targetDistanceGeometryFrame;
            TargetLogicalHurtboxId = targetLogicalHurtboxId ?? string.Empty;
            UtilityScore = utilityScore;
            ActionCost = actionCost;
            HasGeometry = hasGeometry;
            SourceCenter = sourceCenter;
            TargetCenter = targetCenter;
            TargetDirection = targetDirection;
            DecisionMove = decisionMove;
            AttackRange = attackRange;
            DecisionHeadline = decisionHeadline ?? string.Empty;
            ConditionSummary = conditionSummary ?? string.Empty;
            IssueText = issueText ?? string.Empty;
            VisibilityText = visibilityText ?? string.Empty;
            PathText = pathText ?? string.Empty;
            RangeText = rangeText ?? string.Empty;
            MoveText = moveText ?? string.Empty;
            Severity = severity;
        }

        public string SourceName { get; }
        public int AgentEntityId { get; }
        public int TargetEntityId { get; }
        public string StateText { get; }
        public string ActionText { get; }
        public bool TargetExists { get; }
        public bool TargetInAttackRange { get; }
        public bool TargetVisible { get; }
        public bool TargetBlocked { get; }
        public bool MovePathAvailable { get; }
        public bool MoveDirectPathClear { get; }
        public int MoveCandidateIndex { get; }
        public bool EnvironmentNearHazard { get; }
        public string RejectReason { get; }
        public bool HasTargetDistance { get; }
        public float TargetDistance { get; }
        public bool HasDistanceWitness { get; }
        public Vector3 DistanceSourcePoint { get; }
        public Vector3 DistanceTargetPoint { get; }
        public float TargetRootDistance { get; }
        public long TargetDistanceGeometryFrame { get; }
        public string TargetLogicalHurtboxId { get; }
        public float UtilityScore { get; }
        public float ActionCost { get; }
        public bool HasGeometry { get; }
        public Vector3 SourceCenter { get; }
        public Vector3 TargetCenter { get; }
        public Vector2 TargetDirection { get; }
        public Vector2 DecisionMove { get; }
        public float AttackRange { get; }
        public string DecisionHeadline { get; }
        public string ConditionSummary { get; }
        public string IssueText { get; }
        public string VisibilityText { get; }
        public string PathText { get; }
        public string RangeText { get; }
        public string MoveText { get; }
        public CharacterTestRuntimeAiPlannerScenePreviewSeverity Severity { get; }
    }
}
