using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.AI;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiPlannerNavigationDiagnostics
    {
        public CharacterTestRuntimeAiPlannerNavigationDiagnostics(
            CharacterTestNavigationSample sample)
        {
            HasSample = true;
            Sample = sample;
        }

        public bool HasSample { get; }
        public CharacterTestNavigationSample Sample { get; }
    }

    public readonly struct CharacterTestRuntimeAiPlannerAgentSnapshot
    {
        public CharacterTestRuntimeAiPlannerAgentSnapshot(
            int entityId,
            int targetEntityId,
            string activeStatePath,
            bool targetExists,
            bool targetInAttackRange,
            float targetDistance,
            bool movePathAvailable,
            float selectedUtilityScore,
            float selectedActionCost,
            int selectedActionId,
            string selectedActionName,
            CharacterTestRuntimeAiPlannerActionKind selectedActionKind,
            string lastCommand,
            string suppressedReason,
            string rejectReason,
            int transitionFromStateId,
            int transitionToStateId,
            bool targetVisible = false,
            bool targetLineOfSightBlocked = false,
            int targetLastSeenAgeFrames = 0,
            float targetThreatScore = 0f,
            int selfSurfaceKind = 0,
            bool selfOnHardSlick = false,
            bool selfNearMovableObject = false,
            bool moveDirectPathClear = false,
            int moveCandidateIndex = -1,
            bool environmentNearObstacle = false,
            bool environmentNearHazard = false,
            bool environmentNearUsefulObject = false,
            float movementSpeedScale = 0f,
            int movementStrafeSide = 0,
            int movementDirectionHoldFrames = 0,
            int movementReactionDelayFrames = 0,
            bool hasScenePreviewGeometry = false,
            float scenePreviewSourceCenterX = 0f,
            float scenePreviewSourceCenterY = 0f,
            float scenePreviewSourceCenterZ = 0f,
            float scenePreviewTargetCenterX = 0f,
            float scenePreviewTargetCenterY = 0f,
            float scenePreviewTargetCenterZ = 0f,
            float scenePreviewTargetDirectionX = 0f,
            float scenePreviewTargetDirectionZ = 0f,
            int scenePreviewTargetBearingMilliDegrees = 0,
            int scenePreviewFacingDeltaMilliDegrees = 0,
            float scenePreviewFacingDot = 0f,
            float scenePreviewDecisionMoveX = 0f,
            float scenePreviewDecisionMoveZ = 0f,
            float scenePreviewAttackRange = 0f,
            int runtimeAiPlannerProfileSlot = 0,
            int runtimeAiBehaviorLoadoutId = 0,
            int runtimeAiBehaviorVariantId = 0,
            long runtimeAiBehaviorCatalogHash = 0L,
            int runtimeAiBehaviorProfileSlot = 0,
            int selectedAbilitySlot = 0,
            CharacterActionId targetAttackActionId = default,
            int targetAttackSequence = 0,
            int targetAttackImpactFrames = -1,
            bool targetAttackWillHitSelf = false,
            bool targetAttackInstanceResponded = false,
            bool selfDodgeCooldownReady = true,
            int nextDodgeAllowedFrame = 0,
            bool targetDead = false,
            CharacterTestRuntimeAiPlannerTargetLifeState targetLifeState =
                CharacterTestRuntimeAiPlannerTargetLifeState.None,
            bool targetDistanceAvailable = true,
            float targetRootDistance = -1f,
            bool hasScenePreviewDistanceWitness = false,
            float scenePreviewDistanceSourceX = 0f,
            float scenePreviewDistanceSourceY = 0f,
            float scenePreviewDistanceSourceZ = 0f,
            float scenePreviewDistanceTargetX = 0f,
            float scenePreviewDistanceTargetY = 0f,
            float scenePreviewDistanceTargetZ = 0f,
            long scenePreviewDistanceGeometryFrame = 0L,
            int targetLogicalHurtboxId = 0,
            bool selfActionInterruptible = true,
            int activeStateId = 0,
            RuntimeHashDigest activeStatePathDigest = default)
        {
            EntityId = entityId;
            TargetEntityId = targetEntityId;
            ActiveStatePath = activeStatePath ?? string.Empty;
            ActiveStateId = activeStateId;
            ActiveStatePathDigest = activeStatePathDigest;
            TargetExists = targetExists;
            TargetInAttackRange = targetInAttackRange;
            TargetDistance = targetDistance;
            TargetDistanceAvailable = targetDistanceAvailable;
            TargetRootDistance = targetRootDistance >= 0f
                ? targetRootDistance
                : targetDistance;
            HasScenePreviewDistanceWitness = hasScenePreviewDistanceWitness;
            ScenePreviewDistanceSourceX = scenePreviewDistanceSourceX;
            ScenePreviewDistanceSourceY = scenePreviewDistanceSourceY;
            ScenePreviewDistanceSourceZ = scenePreviewDistanceSourceZ;
            ScenePreviewDistanceTargetX = scenePreviewDistanceTargetX;
            ScenePreviewDistanceTargetY = scenePreviewDistanceTargetY;
            ScenePreviewDistanceTargetZ = scenePreviewDistanceTargetZ;
            ScenePreviewDistanceGeometryFrame = scenePreviewDistanceGeometryFrame;
            TargetLogicalHurtboxId = targetLogicalHurtboxId;
            SelfActionInterruptible = selfActionInterruptible;
            MovePathAvailable = movePathAvailable;
            TargetVisible = targetVisible;
            TargetDead = targetDead;
            TargetLifeState = targetLifeState;
            TargetLineOfSightBlocked = targetLineOfSightBlocked;
            TargetLastSeenAgeFrames = targetLastSeenAgeFrames < 0 ? 0 : targetLastSeenAgeFrames;
            TargetThreatScore = targetThreatScore > 0f ? targetThreatScore : 0f;
            SelfSurfaceKind = selfSurfaceKind;
            SelfOnHardSlick = selfOnHardSlick;
            SelfNearMovableObject = selfNearMovableObject;
            MoveDirectPathClear = moveDirectPathClear;
            MoveCandidateIndex = moveCandidateIndex;
            EnvironmentNearObstacle = environmentNearObstacle;
            EnvironmentNearHazard = environmentNearHazard;
            EnvironmentNearUsefulObject = environmentNearUsefulObject;
            MovementSpeedScale = movementSpeedScale > 0f ? movementSpeedScale : 0f;
            MovementStrafeSide = movementStrafeSide;
            MovementDirectionHoldFrames = movementDirectionHoldFrames > 0 ? movementDirectionHoldFrames : 0;
            MovementReactionDelayFrames = movementReactionDelayFrames > 0 ? movementReactionDelayFrames : 0;
            HasScenePreviewGeometry = hasScenePreviewGeometry;
            ScenePreviewSourceCenterX = scenePreviewSourceCenterX;
            ScenePreviewSourceCenterY = scenePreviewSourceCenterY;
            ScenePreviewSourceCenterZ = scenePreviewSourceCenterZ;
            ScenePreviewTargetCenterX = scenePreviewTargetCenterX;
            ScenePreviewTargetCenterY = scenePreviewTargetCenterY;
            ScenePreviewTargetCenterZ = scenePreviewTargetCenterZ;
            ScenePreviewTargetDirectionX = scenePreviewTargetDirectionX;
            ScenePreviewTargetDirectionZ = scenePreviewTargetDirectionZ;
            ScenePreviewTargetBearingMilliDegrees = scenePreviewTargetBearingMilliDegrees;
            ScenePreviewFacingDeltaMilliDegrees = scenePreviewFacingDeltaMilliDegrees;
            ScenePreviewFacingDot = scenePreviewFacingDot;
            ScenePreviewDecisionMoveX = scenePreviewDecisionMoveX;
            ScenePreviewDecisionMoveZ = scenePreviewDecisionMoveZ;
            ScenePreviewAttackRange = scenePreviewAttackRange > 0f ? scenePreviewAttackRange : 0f;
            RuntimeAiPlannerProfileSlot = runtimeAiPlannerProfileSlot > 0 ? runtimeAiPlannerProfileSlot : 0;
            RuntimeAiBehaviorLoadoutId = runtimeAiBehaviorLoadoutId > 0 ? runtimeAiBehaviorLoadoutId : 0;
            RuntimeAiBehaviorVariantId = runtimeAiBehaviorVariantId > 0 ? runtimeAiBehaviorVariantId : 0;
            RuntimeAiBehaviorCatalogHash = runtimeAiBehaviorCatalogHash;
            RuntimeAiBehaviorProfileSlot = runtimeAiBehaviorProfileSlot > 0 ? runtimeAiBehaviorProfileSlot : 0;
            SelectedAbilitySlot = selectedAbilitySlot > 0 ? selectedAbilitySlot : 0;
            TargetAttackActionId = targetAttackActionId;
            TargetAttackSequence = targetAttackSequence > 0 ? targetAttackSequence : 0;
            TargetAttackImpactFrames = targetAttackImpactFrames >= 0 ? targetAttackImpactFrames : -1;
            TargetAttackWillHitSelf = targetAttackWillHitSelf;
            TargetAttackInstanceResponded = targetAttackInstanceResponded;
            SelfDodgeCooldownReady = selfDodgeCooldownReady;
            NextDodgeAllowedFrame = nextDodgeAllowedFrame > 0 ? nextDodgeAllowedFrame : 0;
            SelectedUtilityScore = selectedUtilityScore;
            SelectedActionCost = selectedActionCost;
            SelectedActionId = selectedActionId;
            SelectedActionName = selectedActionName ?? string.Empty;
            SelectedActionKind = selectedActionKind;
            LastCommand = lastCommand ?? string.Empty;
            SuppressedReason = suppressedReason ?? string.Empty;
            RejectReason = rejectReason ?? string.Empty;
            TransitionFromStateId = transitionFromStateId;
            TransitionToStateId = transitionToStateId;
            _navigationDiagnostics = default;
        }

        private CharacterTestRuntimeAiPlannerAgentSnapshot(
            CharacterTestRuntimeAiPlannerAgentSnapshot source,
            CharacterTestRuntimeAiPlannerNavigationDiagnostics navigationDiagnostics)
        {
            this = source;
            _navigationDiagnostics = navigationDiagnostics;
        }

        public CharacterTestRuntimeAiPlannerAgentSnapshot WithRuntimeAiBehaviorSelection(
            int runtimeAiPlannerProfileSlot,
            CharacterTestRuntimeAiBehaviorSelection selection)
        {
            CharacterTestRuntimeAiPlannerAgentSnapshot snapshot =
                new CharacterTestRuntimeAiPlannerAgentSnapshot(
                EntityId,
                TargetEntityId,
                ActiveStatePath,
                TargetExists,
                TargetInAttackRange,
                TargetDistance,
                MovePathAvailable,
                SelectedUtilityScore,
                SelectedActionCost,
                SelectedActionId,
                SelectedActionName,
                SelectedActionKind,
                LastCommand,
                SuppressedReason,
                RejectReason,
                TransitionFromStateId,
                TransitionToStateId,
                TargetVisible,
                TargetLineOfSightBlocked,
                TargetLastSeenAgeFrames,
                TargetThreatScore,
                SelfSurfaceKind,
                SelfOnHardSlick,
                SelfNearMovableObject,
                MoveDirectPathClear,
                MoveCandidateIndex,
                EnvironmentNearObstacle,
                EnvironmentNearHazard,
                EnvironmentNearUsefulObject,
                MovementSpeedScale,
                MovementStrafeSide,
                MovementDirectionHoldFrames,
                MovementReactionDelayFrames,
                HasScenePreviewGeometry,
                ScenePreviewSourceCenterX,
                ScenePreviewSourceCenterY,
                ScenePreviewSourceCenterZ,
                ScenePreviewTargetCenterX,
                ScenePreviewTargetCenterY,
                ScenePreviewTargetCenterZ,
                ScenePreviewTargetDirectionX,
                ScenePreviewTargetDirectionZ,
                ScenePreviewTargetBearingMilliDegrees,
                ScenePreviewFacingDeltaMilliDegrees,
                ScenePreviewFacingDot,
                ScenePreviewDecisionMoveX,
                ScenePreviewDecisionMoveZ,
                ScenePreviewAttackRange,
                runtimeAiPlannerProfileSlot,
                selection.LoadoutId,
                selection.VariantId,
                selection.CatalogHash,
                selection.ProfileSlot,
                SelectedAbilitySlot,
                TargetAttackActionId,
                TargetAttackSequence,
                TargetAttackImpactFrames,
                TargetAttackWillHitSelf,
                TargetAttackInstanceResponded,
                SelfDodgeCooldownReady,
                NextDodgeAllowedFrame,
                TargetDead,
                TargetLifeState,
                TargetDistanceAvailable,
                TargetRootDistance,
                HasScenePreviewDistanceWitness,
                ScenePreviewDistanceSourceX,
                ScenePreviewDistanceSourceY,
                ScenePreviewDistanceSourceZ,
                ScenePreviewDistanceTargetX,
                ScenePreviewDistanceTargetY,
                ScenePreviewDistanceTargetZ,
                ScenePreviewDistanceGeometryFrame,
                TargetLogicalHurtboxId,
                SelfActionInterruptible,
                ActiveStateId,
                ActiveStatePathDigest);
            return new CharacterTestRuntimeAiPlannerAgentSnapshot(
                snapshot,
                _navigationDiagnostics);
        }

        internal CharacterTestRuntimeAiPlannerAgentSnapshot WithNavigationDiagnostics(
            CharacterTestNavigationSample sample)
        {
            return new CharacterTestRuntimeAiPlannerAgentSnapshot(
                this,
                new CharacterTestRuntimeAiPlannerNavigationDiagnostics(sample));
        }

        internal CharacterTestRuntimeAiPlannerNavigationDiagnostics NavigationDiagnostics =>
            _navigationDiagnostics;

        public int EntityId { get; }
        public int TargetEntityId { get; }
        public string ActiveStatePath { get; }
        public int ActiveStateId { get; }
        public RuntimeHashDigest ActiveStatePathDigest { get; }
        public bool TargetExists { get; }
        public bool TargetInAttackRange { get; }
        public float TargetDistance { get; }
        public bool TargetDistanceAvailable { get; }
        public float TargetRootDistance { get; }
        public bool HasScenePreviewDistanceWitness { get; }
        public float ScenePreviewDistanceSourceX { get; }
        public float ScenePreviewDistanceSourceY { get; }
        public float ScenePreviewDistanceSourceZ { get; }
        public float ScenePreviewDistanceTargetX { get; }
        public float ScenePreviewDistanceTargetY { get; }
        public float ScenePreviewDistanceTargetZ { get; }
        public long ScenePreviewDistanceGeometryFrame { get; }
        public int TargetLogicalHurtboxId { get; }
        public bool SelfActionInterruptible { get; }
        public bool MovePathAvailable { get; }
        public bool TargetVisible { get; }
        public bool TargetDead { get; }
        public CharacterTestRuntimeAiPlannerTargetLifeState TargetLifeState { get; }
        public bool TargetLineOfSightBlocked { get; }
        public int TargetLastSeenAgeFrames { get; }
        public float TargetThreatScore { get; }
        public int SelfSurfaceKind { get; }
        public bool SelfOnHardSlick { get; }
        public bool SelfNearMovableObject { get; }
        public bool MoveDirectPathClear { get; }
        public int MoveCandidateIndex { get; }
        public bool EnvironmentNearObstacle { get; }
        public bool EnvironmentNearHazard { get; }
        public bool EnvironmentNearUsefulObject { get; }
        public float MovementSpeedScale { get; }
        public int MovementStrafeSide { get; }
        public int MovementDirectionHoldFrames { get; }
        public int MovementReactionDelayFrames { get; }
        public bool HasScenePreviewGeometry { get; }
        public float ScenePreviewSourceCenterX { get; }
        public float ScenePreviewSourceCenterY { get; }
        public float ScenePreviewSourceCenterZ { get; }
        public float ScenePreviewTargetCenterX { get; }
        public float ScenePreviewTargetCenterY { get; }
        public float ScenePreviewTargetCenterZ { get; }
        public float ScenePreviewTargetDirectionX { get; }
        public float ScenePreviewTargetDirectionZ { get; }
        public int ScenePreviewTargetBearingMilliDegrees { get; }
        public int ScenePreviewFacingDeltaMilliDegrees { get; }
        public float ScenePreviewFacingDot { get; }
        public float ScenePreviewDecisionMoveX { get; }
        public float ScenePreviewDecisionMoveZ { get; }
        public float ScenePreviewAttackRange { get; }
        public int RuntimeAiPlannerProfileSlot { get; }
        public int RuntimeAiBehaviorLoadoutId { get; }
        public int RuntimeAiBehaviorVariantId { get; }
        public long RuntimeAiBehaviorCatalogHash { get; }
        public int RuntimeAiBehaviorProfileSlot { get; }
        public int SelectedAbilitySlot { get; }
        public CharacterActionId TargetAttackActionId { get; }
        public int TargetAttackSequence { get; }
        public int TargetAttackImpactFrames { get; }
        public bool TargetAttackWillHitSelf { get; }
        public bool TargetAttackInstanceResponded { get; }
        public bool SelfDodgeCooldownReady { get; }
        public int NextDodgeAllowedFrame { get; }
        public float SelectedUtilityScore { get; }
        public float SelectedActionCost { get; }
        public int SelectedActionId { get; }
        public string SelectedActionName { get; }
        public CharacterTestRuntimeAiPlannerActionKind SelectedActionKind { get; }
        public string LastCommand { get; }
        public string SuppressedReason { get; }
        public string RejectReason { get; }
        public int TransitionFromStateId { get; }
        public int TransitionToStateId { get; }

        private readonly CharacterTestRuntimeAiPlannerNavigationDiagnostics _navigationDiagnostics;
    }

    internal static class CharacterTestRuntimeAiPlannerNavigationDiagnosticText
    {
        public static string BuildFactsCardPrimary(
            CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            CharacterTestRuntimeAiPlannerNavigationDiagnostics diagnostics =
                agent.NavigationDiagnostics;
            if (!diagnostics.HasSample)
                return string.Empty;

            CharacterTestNavigationSample sample = diagnostics.Sample;
            var builder = new StringBuilder(96);
            builder.Append(sample.ResolutionStatus)
                .Append('/')
                .Append(sample.PathStatus)
                .Append(" · G r")
                .Append(sample.GraphRevision.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(sample.GraphContentHash.ToString("X16", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        public static string BuildFactsCardDetail(
            CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            CharacterTestRuntimeAiPlannerNavigationDiagnostics diagnostics =
                agent.NavigationDiagnostics;
            if (!diagnostics.HasSample)
                return string.Empty;

            CharacterTestNavigationSample sample = diagnostics.Sample;
            var builder = new StringBuilder(192);
            builder.Append("WP ");
            AppendWaypointValue(builder, sample);
            builder.Append(" · C ")
                .Append(sample.TotalCostMillimeters.ToString(CultureInfo.InvariantCulture))
                .Append('/')
                .Append(sample.RemainingDistanceMillimeters.ToString(CultureInfo.InvariantCulture))
                .Append("mm · X ")
                .Append(sample.ExpandedNodeCount.ToString(CultureInfo.InvariantCulture))
                .Append(" · Q ")
                .Append(sample.PathQueryCount.ToString(CultureInfo.InvariantCulture))
                .Append('/')
                .Append(sample.CombatQueryCount.ToString(CultureInfo.InvariantCulture))
                .Append(" · S ");
            AppendSegmentBlocker(builder, sample);
            builder.Append(" · R ")
                .Append(sample.ReplanReason)
                .Append(" · F ")
                .Append(sample.FallbackReason)
                .Append('/')
                .Append(sample.BlockedReason);
            return builder.ToString();
        }

        public static string BuildCompact(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            CharacterTestRuntimeAiPlannerNavigationDiagnostics diagnostics =
                agent.NavigationDiagnostics;
            if (!diagnostics.HasSample)
                return string.Empty;

            CharacterTestNavigationSample sample = diagnostics.Sample;
            var builder = new StringBuilder(256);
            builder.Append(" | nav=")
                .Append(sample.ResolutionStatus)
                .Append(" path=")
                .Append(sample.PathStatus)
                .Append(" graph=r")
                .Append(sample.GraphRevision.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(sample.GraphContentHash.ToString("X16", CultureInfo.InvariantCulture));
            AppendWaypoint(builder, sample);
            builder.Append(" cost=")
                .Append(sample.TotalCostMillimeters.ToString(CultureInfo.InvariantCulture))
                .Append('/')
                .Append(sample.RemainingDistanceMillimeters.ToString(CultureInfo.InvariantCulture))
                .Append("mm expanded=")
                .Append(sample.ExpandedNodeCount.ToString(CultureInfo.InvariantCulture))
                .Append(" query=")
                .Append(sample.PathQueryCount.ToString(CultureInfo.InvariantCulture))
                .Append('/')
                .Append(sample.CombatQueryCount.ToString(CultureInfo.InvariantCulture))
                .Append(" segment=");
            AppendSegmentBlocker(builder, sample);
            builder.Append(" fallback=")
                .Append(sample.FallbackReason)
                .Append(" blocked=")
                .Append(sample.BlockedReason);
            return builder.ToString();
        }

        public static string BuildMultiline(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            CharacterTestRuntimeAiPlannerNavigationDiagnostics diagnostics =
                agent.NavigationDiagnostics;
            if (!diagnostics.HasSample)
                return string.Empty;

            CharacterTestNavigationSample sample = diagnostics.Sample;
            var builder = new StringBuilder(384);
            builder.Append("\n解析/路径状态: ")
                .Append(sample.ResolutionStatus)
                .Append(" / ")
                .Append(sample.PathStatus)
                .Append("\n图身份: revision=")
                .Append(sample.GraphRevision.ToString(CultureInfo.InvariantCulture))
                .Append(", hash=")
                .Append(sample.GraphContentHash.ToString("X16", CultureInfo.InvariantCulture))
                .Append("\n会话/场景: ")
                .Append(sample.SessionBuildStatus)
                .Append(" / ")
                .Append(sample.SceneInputStatus)
                .Append("\n下一路径点: ");
            AppendWaypointValue(builder, sample);
            builder.Append("\n代价 total/remaining(mm): ")
                .Append(sample.TotalCostMillimeters.ToString(CultureInfo.InvariantCulture))
                .Append(" / ")
                .Append(sample.RemainingDistanceMillimeters.ToString(CultureInfo.InvariantCulture))
                .Append("\n展开/查询 path/combat: ")
                .Append(sample.ExpandedNodeCount.ToString(CultureInfo.InvariantCulture))
                .Append(" / ")
                .Append(sample.PathQueryCount.ToString(CultureInfo.InvariantCulture))
                .Append(" / ")
                .Append(sample.CombatQueryCount.ToString(CultureInfo.InvariantCulture))
                .Append("\n路径段阻挡: ");
            AppendSegmentBlocker(builder, sample);
            builder.Append(" hits=")
                .Append(sample.SegmentPathHitCount.ToString(CultureInfo.InvariantCulture))
                .Append("\n重规划/阻断/fallback: ")
                .Append(sample.ReplanReason)
                .Append(" / ")
                .Append(sample.BlockedReason)
                .Append(" / ")
                .Append(sample.FallbackReason)
                .Append("\n物理 revision=")
                .Append(sample.PhysicsWorldRevision.ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static void AppendWaypoint(StringBuilder builder, CharacterTestNavigationSample sample)
        {
            builder.Append(" waypoint=");
            AppendWaypointValue(builder, sample);
        }

        private static void AppendWaypointValue(
            StringBuilder builder,
            CharacterTestNavigationSample sample)
        {
            if (!sample.HasNextWaypoint)
            {
                builder.Append("none");
                return;
            }

            builder.Append('(')
                .Append(ToMeters(sample.NextWaypoint.X).ToString("0.###", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(ToMeters(sample.NextWaypoint.Y).ToString("0.###", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(ToMeters(sample.NextWaypoint.Z).ToString("0.###", CultureInfo.InvariantCulture))
                .Append(')');
        }

        private static void AppendSegmentBlocker(
            StringBuilder builder,
            CharacterTestNavigationSample sample)
        {
            if (sample.SegmentObstacleEntityId.IsNone && sample.SegmentObstacleBodyId.IsNone)
            {
                builder.Append("none");
                return;
            }

            builder.Append("entity#")
                .Append(sample.SegmentObstacleEntityId.Value.ToString(CultureInfo.InvariantCulture))
                .Append("/body#")
                .Append(sample.SegmentObstacleBodyId.Value.ToString(CultureInfo.InvariantCulture))
                .Append('@')
                .Append(ToMeters(sample.SegmentObstacleDistance).ToString("0.###", CultureInfo.InvariantCulture))
                .Append('m');
        }

        private static double ToMeters(Fix64 value)
        {
            return value.RawValue / (double)Fix64.Scale;
        }
    }

    public sealed class CharacterTestRuntimeAiPlannerSnapshot
    {
        public CharacterTestRuntimeAiPlannerSnapshot(
            bool available,
            string profileId,
            CharacterTestRuntimeAiPlannerAgentSnapshot[] agents,
            string validationReport)
        {
            Available = available;
            ProfileId = profileId ?? string.Empty;
            Agents = agents ?? Array.Empty<CharacterTestRuntimeAiPlannerAgentSnapshot>();
            ValidationReport = validationReport ?? string.Empty;
        }

        public bool Available { get; }
        public string ProfileId { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> Agents { get; }
        public string ValidationReport { get; }
    }
}
