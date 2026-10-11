using System;
using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestEngagementCandidateStatus
    {
        None = 0,
        Success = 1,
        SnapshotUnavailable = 2,
        StaleSnapshot = 3,
        AnchorDisabled = 4,
        PhaseMismatch = 5,
        InvalidZone = 6,
        ZoneNotApproachable = 7,
        NavigationUnavailable = 8,
        NoGround = 9,
        Blocked = 10,
        Occupied = 11,
        AttackUnavailable = 12,
        Unreachable = 13,
        PathBudgetExceeded = 14,
        PathStale = 15,
        CapacityFull = 16
    }

    /// <summary>
    /// Immutable witness for one target-root-local engagement anchor projection.
    /// CandidatePose is derived state and is intentionally excluded from snapshots.
    /// </summary>
    internal readonly struct CharacterTestEngagementCandidatePose
    {
        internal CharacterTestEngagementCandidatePose(
            RuntimeFrame completedFrame,
            CombatEntityId sourceEntityId,
            CombatEntityId targetEntityId,
            int resolvedActionRevision,
            int zoneId,
            int anchorId,
            FixVector3 worldPosition,
            int worldYawMilliDegrees,
            int sourcePoseRevision,
            int sourceFacingRevision,
            int targetPoseRevision,
            int targetFacingRevision,
            int targetXMillimeters,
            int targetYMillimeters,
            int targetZMillimeters,
            int targetYawMilliDegrees,
            int zoneBindingRevision,
            int zoneRevision,
            int anchorRevision,
            RuntimePreparedDigest anchorContentHash,
            int phaseId,
            int phaseRevision,
            int combatPhysicsRevision,
            int graphRevision,
            long graphContentHash,
            int standingNodeId)
        {
            CompletedFrame = completedFrame;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            ResolvedActionRevision = resolvedActionRevision;
            ZoneId = zoneId;
            AnchorId = anchorId;
            WorldPosition = worldPosition;
            WorldYawMilliDegrees = NormalizeYaw(worldYawMilliDegrees);
            SourcePoseRevision = sourcePoseRevision;
            SourceFacingRevision = sourceFacingRevision;
            TargetPoseRevision = targetPoseRevision;
            TargetFacingRevision = targetFacingRevision;
            TargetXMillimeters = targetXMillimeters;
            TargetYMillimeters = targetYMillimeters;
            TargetZMillimeters = targetZMillimeters;
            TargetYawMilliDegrees = NormalizeYaw(targetYawMilliDegrees);
            ZoneBindingRevision = zoneBindingRevision;
            ZoneRevision = zoneRevision;
            AnchorRevision = anchorRevision;
            AnchorContentHash = anchorContentHash;
            PhaseId = phaseId;
            PhaseRevision = phaseRevision;
            CombatPhysicsRevision = combatPhysicsRevision;
            GraphRevision = graphRevision;
            GraphContentHash = graphContentHash;
            StandingNodeId = standingNodeId;
        }

        public RuntimeFrame CompletedFrame { get; }
        public CombatEntityId SourceEntityId { get; }
        public CombatEntityId TargetEntityId { get; }
        public int ResolvedActionRevision { get; }
        public int ZoneId { get; }
        public int AnchorId { get; }
        public FixVector3 WorldPosition { get; }
        public int WorldYawMilliDegrees { get; }
        public int SourcePoseRevision { get; }
        public int SourceFacingRevision { get; }
        public int TargetPoseRevision { get; }
        public int TargetFacingRevision { get; }
        public int TargetXMillimeters { get; }
        public int TargetYMillimeters { get; }
        public int TargetZMillimeters { get; }
        public int TargetYawMilliDegrees { get; }
        public int ZoneBindingRevision { get; }
        public int ZoneRevision { get; }
        public int AnchorRevision { get; }
        public RuntimePreparedDigest AnchorContentHash { get; }
        public int PhaseId { get; }
        public int PhaseRevision { get; }
        public int CombatPhysicsRevision { get; }
        public int GraphRevision { get; }
        public long GraphContentHash { get; }
        public int StandingNodeId { get; }
        public bool IsValid => SourceEntityId.Value > 0
            && TargetEntityId.Value > 0
            && ResolvedActionRevision > 0
            && !(ZoneId <= 0)
            && !(AnchorId <= 0)
            && AnchorRevision > 0
            && CombatPhysicsRevision > 0
            && GraphRevision > 0
            && StandingNodeId > 0;

        private static int NormalizeYaw(int value)
        {
            const int fullTurn = 360000;
            int normalized = value % fullTurn;
            return normalized < 0 ? normalized + fullTurn : normalized;
        }
    }

    internal readonly struct CharacterTestEngagementCandidateQuery
    {
        public CharacterTestEngagementCandidateQuery(
            RuntimeFrame completedFrame,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            int resolvedActionRevision,
            CharacterEngagementAnchorRuntimeBinding anchor,
            int anchorRevision,
            RuntimePreparedDigest anchorContentHash,
            CharacterTestDamageZoneSnapshot zones,
            CharacterTestCharacterPhysicsRuntime physics,
            CharacterTestNavigationSession navigation,
            int phaseId,
            int phaseRevision)
        {
            CompletedFrame = completedFrame;
            Source = source;
            Target = target;
            ResolvedActionRevision = resolvedActionRevision;
            Anchor = anchor;
            AnchorRevision = anchorRevision;
            AnchorContentHash = anchorContentHash;
            Zones = zones;
            Physics = physics;
            Navigation = navigation;
            PhaseId = phaseId;
            PhaseRevision = phaseRevision;
        }

        public RuntimeFrame CompletedFrame { get; }
        public CharacterTestCharacterEntity Source { get; }
        public CharacterTestCharacterEntity Target { get; }
        public int ResolvedActionRevision { get; }
        public CharacterEngagementAnchorRuntimeBinding Anchor { get; }
        public int AnchorRevision { get; }
        public RuntimePreparedDigest AnchorContentHash { get; }
        public CharacterTestDamageZoneSnapshot Zones { get; }
        public CharacterTestCharacterPhysicsRuntime Physics { get; }
        public CharacterTestNavigationSession Navigation { get; }
        public int PhaseId { get; }
        public int PhaseRevision { get; }
    }

    internal readonly struct CharacterTestEngagementCandidateResult
    {
        public CharacterTestEngagementCandidateResult(
            CharacterTestEngagementCandidateStatus status,
            CharacterTestEngagementCandidatePose pose,
            CharacterTestAttackOpportunityResult opportunity = default,
            CharacterTestPathfindingSample path = default,
            int authoredPriority = 0,
            int capacity = 0,
            int reservationCount = 0,
            bool isLockedZone = false)
        {
            Status = status;
            Pose = pose;
            Opportunity = opportunity;
            Path = path;
            AuthoredPriority = authoredPriority;
            Capacity = capacity;
            ReservationCount = reservationCount;
            IsLockedZone = isLockedZone;
        }

        public CharacterTestEngagementCandidateStatus Status { get; }
        public CharacterTestEngagementCandidatePose Pose { get; }
        public CharacterTestAttackOpportunityResult Opportunity { get; }
        public CharacterTestPathfindingSample Path { get; }
        public int AuthoredPriority { get; }
        public int Capacity { get; }
        public int ReservationCount { get; }
        public bool IsLockedZone { get; }
        public bool IsSelectable => Status == CharacterTestEngagementCandidateStatus.Success
            && Pose.IsValid
            && Opportunity.Status == CharacterTestAttackOpportunityStatus.Valid
            && Opportunity.TraceCanResolveTargetHit
            && Path.Status == CharacterTestPathStatus.Success
            && ReservationCount < Capacity;
    }

    /// <summary>
    /// Side-effect-free CandidatePose projection, standing validation, path cost and stable
    /// comparison. It reuses the session graph/pathfinder/path buffer owned by PF-M4.
    /// </summary>
    internal sealed class CharacterTestEngagementPlanner
    {
        private const int StandingQueryId = 930100;
        private const int StandingSkinMillimeters = 10;
        private readonly List<CombatQueryResult> _standingHits = new List<CombatQueryResult>(32);

        /// <remarks>NoAlloc after construction/warm-up.</remarks>
        public bool TryProjectAndValidate(
            in CharacterTestEngagementCandidateQuery query,
            out CharacterTestEngagementCandidatePose pose,
            out CharacterTestEngagementCandidateStatus status)
        {
            pose = default;
            status = ValidateAuthority(query);
            if (status != CharacterTestEngagementCandidateStatus.Success)
                return false;

            CharacterEngagementAnchorRuntimeBinding anchor = query.Anchor;
            if (!anchor.Enabled)
                return Fail(CharacterTestEngagementCandidateStatus.AnchorDisabled, out status);
            if (!(anchor.PhaseId <= 0)
                && !(anchor.PhaseId == query.PhaseId))
            {
                return Fail(CharacterTestEngagementCandidateStatus.PhaseMismatch, out status);
            }
            if (!query.Zones.TryGetAvailableZone(
                    anchor.ZoneId,
                    out CharacterTestDamageZoneAggregate zone))
            {
                return Fail(CharacterTestEngagementCandidateStatus.InvalidZone, out status);
            }
            if (!zone.Approachable)
                return Fail(CharacterTestEngagementCandidateStatus.ZoneNotApproachable, out status);

            CharacterTestCharacterTransformComponent targetTransform = query.Target.Transform;
            var targetPose = new CharacterTestDirectHitTraceSourcePose(
                0,
                Position(
                    targetTransform.XMillimeters,
                    targetTransform.YMillimeters,
                    targetTransform.ZMillimeters),
                targetTransform.YawMilliDegrees);
            CharacterRuntimeIntVector3 local = anchor.LocalPositionMillimeters;
            FixVector3 worldPosition = targetPose.TransformLocal(Position(local.X, local.Y, local.Z));
            int worldYaw = NormalizeYaw(targetTransform.YawMilliDegrees + anchor.YawMilliDegrees);

            CharacterTestColliderPostureComponent posture = query.Source.ColliderPosture;
            CharacterTestNavigationGraph graph = query.Navigation.Graph;
            if (!graph.TryFindNearestNode(
                    worldPosition,
                    posture.RadiusMillimeters,
                    posture.HeightMillimeters,
                    out int nodeIndex,
                    out Fix64 nodeDistanceSquared))
            {
                return Fail(CharacterTestEngagementCandidateStatus.NoGround, out status);
            }
            long nodeDistanceMillimeters = CharacterTestNavigationDistance.ToCeilingMillimeters(
                nodeDistanceSquared.Sqrt());
            if (nodeDistanceMillimeters > query.Navigation.Input.MaxEndpointSnapDistanceMillimeters)
                return Fail(CharacterTestEngagementCandidateStatus.NoGround, out status);

            CharacterTestNavigationGraphNode node = graph.GetNode(nodeIndex);
            if (!IsStandingCapsuleClear(query, worldPosition, posture, out status))
                return false;

            pose = new CharacterTestEngagementCandidatePose(
                query.CompletedFrame,
                CombatEntityIdFor(query.Source),
                CombatEntityIdFor(query.Target),
                query.ResolvedActionRevision,
                anchor.ZoneId,
                anchor.AnchorId,
                worldPosition,
                worldYaw,
                query.Source.SourcePoseRevision,
                query.Source.SourceFacingRevision,
                query.Target.SourcePoseRevision,
                query.Target.SourceFacingRevision,
                targetTransform.XMillimeters,
                targetTransform.YMillimeters,
                targetTransform.ZMillimeters,
                targetTransform.YawMilliDegrees,
                query.Zones.BindingRevision,
                query.Zones.ZoneRevision,
                query.AnchorRevision,
                query.AnchorContentHash,
                query.PhaseId,
                query.PhaseRevision,
                query.Physics.World.Revision,
                graph.Revision,
                graph.ContentHash,
                node.NodeId);
            status = CharacterTestEngagementCandidateStatus.Success;
            return true;
        }

        /// <remarks>NoAlloc after construction/warm-up. Uses PF-M4-owned path storage.</remarks>
        public bool TryEvaluatePath(
            in CharacterTestEngagementCandidateQuery query,
            in CharacterTestEngagementCandidatePose pose,
            out CharacterTestPathfindingSample path,
            out CharacterTestEngagementCandidateStatus status)
        {
            path = default;
            if (!IsCurrent(query, pose))
                return Fail(CharacterTestEngagementCandidateStatus.StaleSnapshot, out status);

            CharacterTestNavigationSession session = query.Navigation;
            CharacterTestNavigationSessionBuildInput input = session.Input;
            CharacterTestColliderPostureComponent posture = query.Source.ColliderPosture;
            var pathQuery = new CharacterTestPathQuery(
                session.Graph.Revision,
                session.Graph.ContentHash,
                Position(
                    query.Source.Transform.XMillimeters,
                    query.Source.Transform.YMillimeters,
                    query.Source.Transform.ZMillimeters),
                pose.WorldPosition,
                posture.RadiusMillimeters,
                posture.HeightMillimeters,
                input.MaxEndpointSnapDistanceMillimeters,
                input.MaxNodeExpansions,
                input.MaxPathNodeCount,
                activeBlockedLayerMask: query.Physics.MotionObstacleLayerMask.Value);
            session.Pathfinder.TryFindPath(pathQuery, session.PathBuffer, out path);
            status = MapPathStatus(path.Status);
            return path.Success;
        }

        /// <remarks>NoAlloc. Returns true when left sorts before right.</remarks>
        public static bool IsBetter(
            in CharacterTestEngagementCandidateResult left,
            in CharacterTestEngagementCandidateResult right)
        {
            if (!left.IsSelectable)
                return false;
            if (!right.IsSelectable)
                return true;
            if (left.IsLockedZone != right.IsLockedZone)
                return left.IsLockedZone;
            if (left.AuthoredPriority != right.AuthoredPriority)
                return left.AuthoredPriority > right.AuthoredPriority;
            if (left.Path.TotalCostMillimeters != right.Path.TotalCostMillimeters)
                return left.Path.TotalCostMillimeters < right.Path.TotalCostMillimeters;
            if (left.Opportunity.EarliestCandidateHitTick != right.Opportunity.EarliestCandidateHitTick)
            {
                return left.Opportunity.EarliestCandidateHitTick
                    < right.Opportunity.EarliestCandidateHitTick;
            }
            int comparison = left.Pose.ZoneId.CompareTo(right.Pose.ZoneId);
            if (comparison != 0)
                return comparison < 0;
            comparison = left.Opportunity.SelectedHurtboxId.CompareTo(right.Opportunity.SelectedHurtboxId);
            if (comparison != 0)
                return comparison < 0;
            return left.Pose.AnchorId.CompareTo(right.Pose.AnchorId) < 0;
        }

        /// <remarks>NoAlloc.</remarks>
        public static bool IsCurrent(
            in CharacterTestEngagementCandidateQuery query,
            in CharacterTestEngagementCandidatePose pose)
        {
            if (!pose.IsValid
                || query.Source == null
                || query.Target == null
                || query.Anchor == null
                || query.Zones == null
                || query.Physics?.World == null
                || query.Navigation?.Graph == null)
            {
                return false;
            }
            CharacterTestCharacterTransformComponent targetTransform = query.Target.Transform;
            return pose.CompletedFrame == query.CompletedFrame
                && pose.SourceEntityId.Equals(CombatEntityIdFor(query.Source))
                && pose.TargetEntityId.Equals(CombatEntityIdFor(query.Target))
                && pose.ResolvedActionRevision == query.ResolvedActionRevision
                && (pose.ZoneId == query.Anchor.ZoneId)
                && (pose.AnchorId == query.Anchor.AnchorId)
                && pose.SourcePoseRevision == query.Source.SourcePoseRevision
                && pose.SourceFacingRevision == query.Source.SourceFacingRevision
                && pose.TargetPoseRevision == query.Target.SourcePoseRevision
                && pose.TargetFacingRevision == query.Target.SourceFacingRevision
                && pose.TargetXMillimeters == targetTransform.XMillimeters
                && pose.TargetYMillimeters == targetTransform.YMillimeters
                && pose.TargetZMillimeters == targetTransform.ZMillimeters
                && pose.TargetYawMilliDegrees == NormalizeYaw(targetTransform.YawMilliDegrees)
                && pose.ZoneBindingRevision == query.Zones.BindingRevision
                && pose.ZoneRevision == query.Zones.ZoneRevision
                && pose.AnchorRevision == query.AnchorRevision
                && pose.AnchorContentHash.Equals(query.AnchorContentHash)
                && (pose.PhaseId == query.PhaseId)
                && pose.PhaseRevision == query.PhaseRevision
                && pose.CombatPhysicsRevision == query.Physics.World.Revision
                && pose.GraphRevision == query.Navigation.Graph.Revision
                && pose.GraphContentHash == query.Navigation.Graph.ContentHash;
        }

        private bool IsStandingCapsuleClear(
            in CharacterTestEngagementCandidateQuery query,
            FixVector3 rootPosition,
            CharacterTestColliderPostureComponent posture,
            out CharacterTestEngagementCandidateStatus status)
        {
            int radius = posture.RadiusMillimeters;
            int height = posture.HeightMillimeters;
            Fix64 bottomOffset = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                radius + StandingSkinMillimeters);
            Fix64 topOffset = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                Math.Max(radius, height - radius) + StandingSkinMillimeters);
            var header = new CombatQueryHeader(
                StandingQueryId,
                CombatQueryKind.Capsule,
                CombatEntityIdFor(query.Source),
                StandingQueryId,
                query.ResolvedActionRevision,
                sourceOrder: 0,
                query.Physics.MotionObstacleLayerMask);
            var capsule = new CombatCapsuleQuery(
                header,
                new FixVector3(rootPosition.X, rootPosition.Y + bottomOffset, rootPosition.Z),
                new FixVector3(rootPosition.X, rootPosition.Y + topOffset, rootPosition.Z),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(radius));
            _standingHits.Clear();
            query.Physics.World.QueryCapsule(capsule, _standingHits, includeSourceEntity: false);
            CombatEntityId targetEntityId = CombatEntityIdFor(query.Target);
            for (int i = 0; i < _standingHits.Count; i++)
            {
                CombatQueryResult hit = _standingHits[i];
                if (hit.TargetEntityId.Equals(targetEntityId))
                    continue;
                status = hit.TargetEntityId.Value
                    >= CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart
                    ? CharacterTestEngagementCandidateStatus.Occupied
                    : CharacterTestEngagementCandidateStatus.Blocked;
                return false;
            }
            status = CharacterTestEngagementCandidateStatus.Success;
            return true;
        }

        private static CharacterTestEngagementCandidateStatus ValidateAuthority(
            in CharacterTestEngagementCandidateQuery query)
        {
            if (query.Source == null
                || query.Target == null
                || query.Anchor == null
                || query.Zones == null
                || query.Physics?.World == null
                || query.Navigation == null
                || !query.Navigation.IsAvailable
                || query.ResolvedActionRevision <= 0
                || query.AnchorRevision <= 0)
            {
                return query.Navigation == null || !query.Navigation.IsAvailable
                    ? CharacterTestEngagementCandidateStatus.NavigationUnavailable
                    : CharacterTestEngagementCandidateStatus.SnapshotUnavailable;
            }
            if (!query.Zones.IsValid
                || query.Zones.Frame != query.CompletedFrame
                || query.Zones.WorldRevision != query.Physics.World.Revision
                || query.Zones.EntityId != query.Target.Id.Value)
            {
                return CharacterTestEngagementCandidateStatus.StaleSnapshot;
            }
            if (query.Anchor.ReferenceFrame != CharacterEngagementAnchorReferenceFrame.ModelRoot)
                return CharacterTestEngagementCandidateStatus.SnapshotUnavailable;
            return CharacterTestEngagementCandidateStatus.Success;
        }

        private static CharacterTestEngagementCandidateStatus MapPathStatus(
            CharacterTestPathStatus status)
        {
            switch (status)
            {
                case CharacterTestPathStatus.Success:
                    return CharacterTestEngagementCandidateStatus.Success;
                case CharacterTestPathStatus.BudgetExceeded:
                case CharacterTestPathStatus.PathBufferTooSmall:
                case CharacterTestPathStatus.CostOverflow:
                    return CharacterTestEngagementCandidateStatus.PathBudgetExceeded;
                case CharacterTestPathStatus.StaleGraph:
                    return CharacterTestEngagementCandidateStatus.PathStale;
                case CharacterTestPathStatus.GraphUnavailable:
                case CharacterTestPathStatus.InvalidQuery:
                    return CharacterTestEngagementCandidateStatus.NavigationUnavailable;
                default:
                    return CharacterTestEngagementCandidateStatus.Unreachable;
            }
        }

        private static CombatEntityId CombatEntityIdFor(CharacterTestCharacterEntity entity)
        {
            return new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value);
        }

        private static FixVector3 Position(int x, int y, int z)
        {
            return new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(x),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(y),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(z));
        }

        private static int NormalizeYaw(int value)
        {
            const int fullTurn = 360000;
            int normalized = value % fullTurn;
            return normalized < 0 ? normalized + fullTurn : normalized;
        }

        private static bool Fail(
            CharacterTestEngagementCandidateStatus failure,
            out CharacterTestEngagementCandidateStatus status)
        {
            status = failure;
            return false;
        }
    }
}
