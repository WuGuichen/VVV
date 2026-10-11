using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiPlannerDevPreviewSeverity
    {
        None = 0,
        Neutral = 1,
        Good = 2,
        Warning = 3,
        Danger = 4
    }

    internal readonly struct CharacterTestRuntimeAiPlannerDevPreviewItem
    {
        public CharacterTestRuntimeAiPlannerDevPreviewItem(
            bool available,
            CharacterTestRuntimeAiPlannerDevPreviewSeverity severity,
            int frame,
            int agentEntityId,
            int targetEntityId,
            bool targetExists,
            bool hasGeometry,
            bool hasTargetLine,
            bool hasAttackRange,
            bool hasMoveArrow,
            bool targetVisible,
            bool targetBlocked,
            bool targetInAttackRange,
            bool pathAvailable,
            bool directPathClear,
            bool nearHazard,
            Vector3 sourceCenter,
            Vector3 targetCenter,
            Vector3 moveEnd,
            float attackRange,
            Color targetLineColor,
            Color attackRangeColor,
            Color moveArrowColor,
            string headline,
            string detail,
            string statusLabel,
            bool targetDead = false,
            float targetDistance = 0f,
            bool hasTargetDistance = true,
            bool hasDistanceWitness = false,
            Vector3 distanceSourcePoint = default,
            Vector3 distanceTargetPoint = default,
            float targetRootDistance = 0f,
            long targetDistanceGeometryFrame = 0L,
            int targetLogicalHurtboxId = 0)
        {
            Available = available;
            Severity = severity;
            Frame = frame;
            AgentEntityId = agentEntityId;
            TargetEntityId = targetEntityId;
            TargetExists = targetExists;
            HasGeometry = hasGeometry;
            HasTargetLine = hasTargetLine;
            HasAttackRange = hasAttackRange;
            HasMoveArrow = hasMoveArrow;
            TargetVisible = targetVisible;
            TargetDead = targetDead;
            TargetBlocked = targetBlocked;
            TargetInAttackRange = targetInAttackRange;
            PathAvailable = pathAvailable;
            DirectPathClear = directPathClear;
            NearHazard = nearHazard;
            SourceCenter = sourceCenter;
            TargetCenter = targetCenter;
            MoveEnd = moveEnd;
            AttackRange = attackRange;
            TargetLineColor = targetLineColor;
            AttackRangeColor = attackRangeColor;
            MoveArrowColor = moveArrowColor;
            Headline = headline ?? string.Empty;
            Detail = detail ?? string.Empty;
            StatusLabel = statusLabel ?? string.Empty;
            TargetDistance = targetDistance;
            HasTargetDistance = hasTargetDistance && HasGeometry && TargetExists;
            HasDistanceWitness = hasDistanceWitness && HasTargetDistance;
            DistanceSourcePoint = HasDistanceWitness ? distanceSourcePoint : sourceCenter;
            DistanceTargetPoint = HasDistanceWitness ? distanceTargetPoint : targetCenter;
            TargetRootDistance = targetRootDistance > 0f ? targetRootDistance : 0f;
            TargetDistanceGeometryFrame = targetDistanceGeometryFrame;
            TargetLogicalHurtboxId = targetLogicalHurtboxId;
        }

        public bool Available { get; }
        public CharacterTestRuntimeAiPlannerDevPreviewSeverity Severity { get; }
        public int Frame { get; }
        public int AgentEntityId { get; }
        public int TargetEntityId { get; }
        public bool TargetExists { get; }
        public bool HasGeometry { get; }
        public bool HasTargetLine { get; }
        public bool HasAttackRange { get; }
        public bool HasMoveArrow { get; }
        public bool TargetVisible { get; }
        public bool TargetDead { get; }
        public bool TargetBlocked { get; }
        public bool TargetInAttackRange { get; }
        public bool PathAvailable { get; }
        public bool DirectPathClear { get; }
        public bool NearHazard { get; }
        public Vector3 SourceCenter { get; }
        public Vector3 TargetCenter { get; }
        public Vector3 MoveEnd { get; }
        public float AttackRange { get; }
        public bool HasTargetDistance { get; }
        public float TargetDistance { get; }
        public bool HasDistanceWitness { get; }
        public Vector3 DistanceSourcePoint { get; }
        public Vector3 DistanceTargetPoint { get; }
        public float TargetRootDistance { get; }
        public long TargetDistanceGeometryFrame { get; }
        public int TargetLogicalHurtboxId { get; }
        public string FormattedDistance =>
            HasTargetDistance
                ? ("Hurtbox gap "
                    + TargetDistance.ToString("0.#", CultureInfo.InvariantCulture)
                    + "m")
                : string.Empty;
        public Color TargetLineColor { get; }
        public Color AttackRangeColor { get; }
        public Color MoveArrowColor { get; }
        public string Headline { get; }
        public string Detail { get; }
        public string StatusLabel { get; }

        public static CharacterTestRuntimeAiPlannerDevPreviewItem Unavailable(string reason)
        {
            return new CharacterTestRuntimeAiPlannerDevPreviewItem(
                available: false,
                CharacterTestRuntimeAiPlannerDevPreviewSeverity.None,
                frame: 0,
                agentEntityId: 0,
                targetEntityId: 0,
                targetExists: false,
                hasGeometry: false,
                hasTargetLine: false,
                hasAttackRange: false,
                hasMoveArrow: false,
                targetVisible: false,
                targetBlocked: false,
                targetInAttackRange: false,
                pathAvailable: false,
                directPathClear: false,
                nearHazard: false,
                sourceCenter: Vector3.zero,
                targetCenter: Vector3.zero,
                moveEnd: Vector3.zero,
                attackRange: 0f,
                targetLineColor: CharacterTestRuntimeAiPlannerDevPreviewColors.Neutral,
                attackRangeColor: CharacterTestRuntimeAiPlannerDevPreviewColors.Neutral,
                moveArrowColor: CharacterTestRuntimeAiPlannerDevPreviewColors.Neutral,
                headline: reason,
                detail: reason,
                statusLabel: reason,
                targetDead: false,
                targetDistance: 0f);
        }
    }

    internal static class CharacterTestRuntimeAiPlannerDevPreviewBuilder
    {
        private const float MoveArrowLength = 1.6f;
        private const float MoveArrowMinSqrMagnitude = 0.0001f;

        public static CharacterTestRuntimeAiPlannerDevPreviewItem Build(
            CharacterTestRuntimeAiPlannerDevFrameSnapshot frame,
            int selectedAgentIndex,
            bool overlayEnabled)
        {
            if (!overlayEnabled)
                return CharacterTestRuntimeAiPlannerDevPreviewItem.Unavailable("Scene overlay is off.");

            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents = frame != null
                ? frame.Agents
                : null;
            if (agents == null || agents.Count == 0)
                return CharacterTestRuntimeAiPlannerDevPreviewItem.Unavailable("No Runtime AI Planner agent.");

            int agentIndex = ClampIndex(selectedAgentIndex, agents.Count);
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = agents[agentIndex];
            return Build(frame.Frame, agent);
        }

        public static CharacterTestRuntimeAiPlannerDevPreviewItem Build(
            int frame,
            CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (!agent.HasScenePreviewGeometry)
            {
                return new CharacterTestRuntimeAiPlannerDevPreviewItem(
                    available: true,
                    CharacterTestRuntimeAiPlannerDevPreviewSeverity.Neutral,
                    frame,
                    agent.EntityId,
                    agent.TargetEntityId,
                    agent.TargetExists,
                    hasGeometry: false,
                    hasTargetLine: false,
                    hasAttackRange: false,
                    hasMoveArrow: false,
                    agent.TargetVisible,
                    agent.TargetLineOfSightBlocked,
                    agent.TargetInAttackRange,
                    agent.MovePathAvailable,
                    agent.MoveDirectPathClear,
                    agent.EnvironmentNearHazard,
                    sourceCenter: Vector3.zero,
                    targetCenter: Vector3.zero,
                    moveEnd: Vector3.zero,
                    attackRange: 0f,
                    targetLineColor: CharacterTestRuntimeAiPlannerDevPreviewColors.Neutral,
                    attackRangeColor: CharacterTestRuntimeAiPlannerDevPreviewColors.Neutral,
                    moveArrowColor: CharacterTestRuntimeAiPlannerDevPreviewColors.Neutral,
                    headline: "Agent " + agent.EntityId.ToString(CultureInfo.InvariantCulture) + " has no scene geometry.",
                    detail: "Start, pause, then step a Runtime AI Lab frame to capture geometry.",
                    statusLabel: "No scene preview geometry",
                    targetDead: agent.TargetDead,
                    targetDistance: agent.TargetDistance,
                    hasTargetDistance: agent.TargetDistanceAvailable);
            }

            Vector3 source = new Vector3(
                agent.ScenePreviewSourceCenterX,
                agent.ScenePreviewSourceCenterY,
                agent.ScenePreviewSourceCenterZ);
            Vector3 target = new Vector3(
                agent.ScenePreviewTargetCenterX,
                agent.ScenePreviewTargetCenterY,
                agent.ScenePreviewTargetCenterZ);
            Vector3 move = new Vector3(agent.ScenePreviewDecisionMoveX, 0f, agent.ScenePreviewDecisionMoveZ);
            bool hasMove = move.sqrMagnitude > MoveArrowMinSqrMagnitude;
            Vector3 moveEnd = hasMove ? source + move.normalized * MoveArrowLength : source;
            CharacterTestRuntimeAiPlannerDevPreviewSeverity severity = ResolveSeverity(agent);
            Color lineColor = ResolveTargetLineColor(agent, severity);
            Color rangeColor = agent.TargetInAttackRange
                ? CharacterTestRuntimeAiPlannerDevPreviewColors.Good
                : CharacterTestRuntimeAiPlannerDevPreviewColors.Warning;
            Color moveColor = agent.MovePathAvailable
                ? CharacterTestRuntimeAiPlannerDevPreviewColors.Move
                : CharacterTestRuntimeAiPlannerDevPreviewColors.Danger;

            bool hasTargetLine = agent.TargetExists;
            bool hasAttackRange = agent.ScenePreviewAttackRange > 0.001f;
            string headline = CharacterTestRuntimeAiPlannerDevPresentationText.BuildShortHeadline(agent);
            string detail =
                "frame "
                + frame.ToString(CultureInfo.InvariantCulture)
                + " | state "
                + NonEmpty(agent.ActiveStatePath, "none")
                + " | visible="
                + ToShort(agent.TargetVisible)
                + " dead="
                + ToShort(agent.TargetDead)
                + " blocked="
                + ToShort(agent.TargetLineOfSightBlocked)
                + " path="
                + ToShort(agent.MovePathAvailable)
                + " direct="
                + ToShort(agent.MoveDirectPathClear)
                + " range="
                + ToShort(agent.TargetInAttackRange)
                + " | score/cost "
                + agent.SelectedUtilityScore.ToString("0.##", CultureInfo.InvariantCulture)
                + "/"
                + agent.SelectedActionCost.ToString("0.##", CultureInfo.InvariantCulture);
            string status = CharacterTestRuntimeAiPlannerDevPresentationText.BuildShortStatus(agent);
            float targetDistance = agent.TargetDistance;
            Vector3 distanceSourcePoint = new Vector3(
                agent.ScenePreviewDistanceSourceX,
                agent.ScenePreviewDistanceSourceY,
                agent.ScenePreviewDistanceSourceZ);
            Vector3 distanceTargetPoint = new Vector3(
                agent.ScenePreviewDistanceTargetX,
                agent.ScenePreviewDistanceTargetY,
                agent.ScenePreviewDistanceTargetZ);

            return new CharacterTestRuntimeAiPlannerDevPreviewItem(
                available: true,
                severity,
                frame,
                agent.EntityId,
                agent.TargetEntityId,
                agent.TargetExists,
                hasGeometry: true,
                hasTargetLine,
                hasAttackRange,
                hasMoveArrow: hasMove,
                agent.TargetVisible,
                agent.TargetLineOfSightBlocked,
                agent.TargetInAttackRange,
                agent.MovePathAvailable,
                agent.MoveDirectPathClear,
                agent.EnvironmentNearHazard,
                source,
                target,
                moveEnd,
                agent.ScenePreviewAttackRange,
                lineColor,
                rangeColor,
                moveColor,
                headline,
                detail,
                status,
                targetDead: agent.TargetDead,
                targetDistance: targetDistance,
                hasTargetDistance: agent.TargetDistanceAvailable,
                hasDistanceWitness: agent.HasScenePreviewDistanceWitness,
                distanceSourcePoint: distanceSourcePoint,
                distanceTargetPoint: distanceTargetPoint,
                targetRootDistance: agent.TargetRootDistance,
                targetDistanceGeometryFrame: agent.ScenePreviewDistanceGeometryFrame,
                targetLogicalHurtboxId: agent.TargetLogicalHurtboxId);
        }

        private static CharacterTestRuntimeAiPlannerDevPreviewSeverity ResolveSeverity(
            CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (!agent.TargetExists)
                return CharacterTestRuntimeAiPlannerDevPreviewSeverity.Neutral;
            if (agent.TargetDead)
                return CharacterTestRuntimeAiPlannerDevPreviewSeverity.Danger;

            bool rejected = !string.IsNullOrEmpty(agent.RejectReason)
                || !string.IsNullOrEmpty(agent.SuppressedReason);
            if (rejected
                || agent.EnvironmentNearHazard
                || (!agent.MovePathAvailable && !agent.TargetInAttackRange))
            {
                return CharacterTestRuntimeAiPlannerDevPreviewSeverity.Danger;
            }

            if (!agent.TargetVisible
                || agent.TargetLineOfSightBlocked
                || !agent.MoveDirectPathClear)
            {
                return CharacterTestRuntimeAiPlannerDevPreviewSeverity.Warning;
            }

            return CharacterTestRuntimeAiPlannerDevPreviewSeverity.Good;
        }

        private static Color ResolveTargetLineColor(
            CharacterTestRuntimeAiPlannerAgentSnapshot agent,
            CharacterTestRuntimeAiPlannerDevPreviewSeverity severity)
        {
            if (!agent.TargetExists)
                return CharacterTestRuntimeAiPlannerDevPreviewColors.Neutral;
            if (agent.TargetDead)
                return CharacterTestRuntimeAiPlannerDevPreviewColors.Danger;
            bool rejected = !string.IsNullOrEmpty(agent.RejectReason)
                || !string.IsNullOrEmpty(agent.SuppressedReason);
            if (!agent.TargetVisible
                || agent.TargetLineOfSightBlocked
                || rejected
                || (!agent.MovePathAvailable && !agent.TargetInAttackRange)
                || agent.EnvironmentNearHazard)
            {
                return ResolveLineColor(severity);
            }
            if (!agent.MoveDirectPathClear)
                return CharacterTestRuntimeAiPlannerDevPreviewColors.Warning;

            return CharacterTestRuntimeAiPlannerDevPreviewColors.Good;
        }

        private static Color ResolveLineColor(CharacterTestRuntimeAiPlannerDevPreviewSeverity severity)
        {
            switch (severity)
            {
                case CharacterTestRuntimeAiPlannerDevPreviewSeverity.Good:
                    return CharacterTestRuntimeAiPlannerDevPreviewColors.Good;
                case CharacterTestRuntimeAiPlannerDevPreviewSeverity.Warning:
                    return CharacterTestRuntimeAiPlannerDevPreviewColors.Warning;
                case CharacterTestRuntimeAiPlannerDevPreviewSeverity.Danger:
                    return CharacterTestRuntimeAiPlannerDevPreviewColors.Danger;
                default:
                    return CharacterTestRuntimeAiPlannerDevPreviewColors.Neutral;
            }
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0 || index < 0)
                return 0;

            return index < count ? index : count - 1;
        }

        private static string NonEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string ToShort(bool value)
        {
            return value ? "Y" : "N";
        }
    }

    internal static class CharacterTestRuntimeAiPlannerDevPreviewColors
    {
        public static readonly Color Good = new Color(0.2f, 0.9f, 0.38f, 1f);
        public static readonly Color Warning = new Color(1f, 0.78f, 0.22f, 1f);
        public static readonly Color Danger = new Color(1f, 0.23f, 0.2f, 1f);
        public static readonly Color Neutral = new Color(0.6f, 0.66f, 0.72f, 1f);
        public static readonly Color Move = new Color(0.25f, 0.62f, 1f, 1f);
    }
}
