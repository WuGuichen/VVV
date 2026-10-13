using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestPerceptionTargetRelationFilter
    {
        AnyOther = 0,
        OpposingTeam = 1
    }

    internal enum CharacterTestPerceptionTargetStateFilter
    {
        Any = 0,
        Alive = 1
    }

    internal enum CharacterTestPerceptionTargetTiePolicy
    {
        First = 0,
        Last = 1
    }

    internal readonly struct CharacterTestPerceptionEntitySample
    {
        public CharacterTestPerceptionEntitySample(
            RuntimeFrame frame,
            int entityId,
            CombatEntityId combatEntityId,
            CombatBodyId bodyId,
            int teamId,
            bool alive,
            FixVector3 position,
            FixVector3 center,
            int yawMilliDegrees,
            CombatSurfaceMaterialKind surfaceKind)
        {
            Frame = frame;
            EntityId = entityId;
            CombatEntityId = combatEntityId;
            BodyId = bodyId;
            TeamId = teamId;
            Alive = alive;
            Position = position;
            Center = center;
            YawMilliDegrees = yawMilliDegrees;
            SurfaceKind = surfaceKind;
            OnHardSlick = surfaceKind == CombatSurfaceMaterialKind.HardSlick;
        }

        public RuntimeFrame Frame { get; }
        public int EntityId { get; }
        public CombatEntityId CombatEntityId { get; }
        public CombatBodyId BodyId { get; }
        public int TeamId { get; }
        public bool Alive { get; }
        public FixVector3 Position { get; }
        public FixVector3 Center { get; }
        public int YawMilliDegrees { get; }
        public CombatSurfaceMaterialKind SurfaceKind { get; }
        public bool OnHardSlick { get; }
    }

    internal readonly struct CharacterTestTargetGeometrySample
    {
        public CharacterTestTargetGeometrySample(
            RuntimeFrame evaluationFrame,
            RuntimeFrame geometryFrame,
            long geometryGeneration,
            CharacterTestPerceptionEntitySample source,
            CharacterTestPerceptionEntitySample target,
            int deltaXMillimeters,
            int deltaZMillimeters,
            long sqrPlanarDistanceMillimeters,
            long sqrSpatialDistanceMillimeters,
            float deltaXMeters,
            float deltaZMeters,
            float sqrDistanceMeters,
            float distanceMeters,
            float directionX,
            float directionZ,
            int directionXScaled,
            int directionZScaled,
            int targetBearingMilliDegrees,
            int facingDeltaMilliDegrees,
            float facingDot)
        {
            Frame = evaluationFrame;
            GeometryFrame = geometryFrame;
            GeometryGeneration = geometryGeneration;
            Source = source;
            Target = target;
            DeltaXMillimeters = deltaXMillimeters;
            DeltaZMillimeters = deltaZMillimeters;
            SqrPlanarDistanceMillimeters = sqrPlanarDistanceMillimeters;
            SqrSpatialDistanceMillimeters = sqrSpatialDistanceMillimeters;
            DeltaXMeters = deltaXMeters;
            DeltaZMeters = deltaZMeters;
            SqrDistanceMeters = sqrDistanceMeters;
            DistanceMeters = distanceMeters;
            DirectionX = directionX;
            DirectionZ = directionZ;
            DirectionXScaled = directionXScaled;
            DirectionZScaled = directionZScaled;
            TargetBearingMilliDegrees = targetBearingMilliDegrees;
            FacingDeltaMilliDegrees = facingDeltaMilliDegrees;
            FacingDot = facingDot;
        }

        public RuntimeFrame Frame { get; }
        public RuntimeFrame EvaluationFrame => Frame;
        public RuntimeFrame GeometryFrame { get; }
        public long GeometryGeneration { get; }
        public CharacterTestPerceptionEntitySample Source { get; }
        public CharacterTestPerceptionEntitySample Target { get; }
        public int SourceEntityId => Source.EntityId;
        public int TargetEntityId => Target.EntityId;
        public int DeltaXMillimeters { get; }
        public int DeltaZMillimeters { get; }
        public long SqrPlanarDistanceMillimeters { get; }
        public long SqrSpatialDistanceMillimeters { get; }
        public float DeltaXMeters { get; }
        public float DeltaZMeters { get; }
        public float SqrDistanceMeters { get; }
        public float DistanceMeters { get; }
        public float DirectionX { get; }
        public float DirectionZ { get; }
        public int DirectionXScaled { get; }
        public int DirectionZScaled { get; }
        public int TargetBearingMilliDegrees { get; }
        public int FacingDeltaMilliDegrees { get; }
        public float FacingDot { get; }
        public FixVector3 SourceCenter => Source.Center;
        public FixVector3 TargetCenter => Target.Center;
        public bool TargetAlive => Target.Alive;
        public bool IsOpposingTeam => Source.TeamId != Target.TeamId;

        public FixVector3 ResolveTargetPoint(Fix64 targetHeightOffset)
        {
            return Target.Center + new FixVector3(Fix64.Zero, targetHeightOffset, Fix64.Zero);
        }
    }

    internal readonly struct CharacterTestVisibilitySample
    {
        public CharacterTestVisibilitySample(
            RuntimeFrame frame,
            int sourceEntityId,
            int targetEntityId,
            bool lineOfSightBlocked,
            bool targetVisible,
            int hitCount,
            CombatEntityId nearestEntityId,
            CombatBodyId nearestBodyId,
            Fix64 nearestDistance)
        {
            Frame = frame;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            LineOfSightBlocked = lineOfSightBlocked;
            TargetVisible = targetVisible;
            HitCount = hitCount;
            NearestEntityId = nearestEntityId;
            NearestBodyId = nearestBodyId;
            NearestDistance = nearestDistance;
        }

        public RuntimeFrame Frame { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public bool LineOfSightBlocked { get; }
        public bool TargetVisible { get; }
        public int HitCount { get; }
        public CombatEntityId NearestEntityId { get; }
        public CombatBodyId NearestBodyId { get; }
        public Fix64 NearestDistance { get; }
    }

    internal enum CharacterTestNavigationResolutionStatus
    {
        None = 0,
        Coincident = 1,
        Direct = 2,
        PathWaypoint = 3,
        LocalCandidate = 4,
        Stopped = 5
    }

    internal enum CharacterTestNavigationReplanReason
    {
        None = 0,
        DirectPathBlocked = 1
    }

    internal enum CharacterTestNavigationBlockedReason
    {
        None = 0,
        GraphUnavailable = 1,
        SceneInputUnavailable = 2,
        SessionBuildFailed = 3,
        InvalidQuery = 4,
        StaleGraph = 5,
        SourceNodeUnavailable = 6,
        TargetNodeUnavailable = 7,
        SourceOutsideGraph = 8,
        TargetOutsideGraph = 9,
        Unreachable = 10,
        BudgetExceeded = 11,
        PathBufferTooSmall = 12,
        CostOverflow = 13,
        PathHasNoWaypoint = 14,
        SegmentBlocked = 15
    }

    internal enum CharacterTestNavigationFallbackReason
    {
        None = 0,
        LocalCandidate = 1,
        Stop = 2
    }

    internal readonly struct CharacterTestNavigationPathDetail
    {
        public CharacterTestNavigationPathDetail(
            int physicsWorldRevision,
            CharacterTestNavigationSessionBuildStatus sessionBuildStatus,
            CharacterTestNavigationSceneInputStatus sceneInputStatus,
            CharacterTestPathStatus pathStatus,
            int graphRevision,
            long graphContentHash,
            bool hasNextWaypoint,
            FixVector3 nextWaypoint,
            long remainingDistanceMillimeters,
            long totalCostMillimeters,
            int expandedNodeCount,
            int pathQueryCount,
            int segmentPathHitCount,
            CombatEntityId segmentObstacleEntityId,
            CombatBodyId segmentObstacleBodyId,
            Fix64 segmentObstacleDistance,
            CharacterTestNavigationResolutionStatus resolutionStatus,
            CharacterTestNavigationReplanReason replanReason,
            CharacterTestNavigationBlockedReason blockedReason,
            CharacterTestNavigationFallbackReason fallbackReason)
        {
            PhysicsWorldRevision = physicsWorldRevision;
            SessionBuildStatus = sessionBuildStatus;
            SceneInputStatus = sceneInputStatus;
            PathStatus = pathStatus;
            GraphRevision = graphRevision;
            GraphContentHash = graphContentHash;
            HasNextWaypoint = hasNextWaypoint;
            NextWaypoint = nextWaypoint;
            RemainingDistanceMillimeters = remainingDistanceMillimeters;
            TotalCostMillimeters = totalCostMillimeters;
            ExpandedNodeCount = expandedNodeCount;
            PathQueryCount = pathQueryCount;
            SegmentPathHitCount = segmentPathHitCount;
            SegmentObstacleEntityId = segmentObstacleEntityId;
            SegmentObstacleBodyId = segmentObstacleBodyId;
            SegmentObstacleDistance = segmentObstacleDistance;
            ResolutionStatus = resolutionStatus;
            ReplanReason = replanReason;
            BlockedReason = blockedReason;
            FallbackReason = fallbackReason;
        }

        public int PhysicsWorldRevision { get; }
        public CharacterTestNavigationSessionBuildStatus SessionBuildStatus { get; }
        public CharacterTestNavigationSceneInputStatus SceneInputStatus { get; }
        public CharacterTestPathStatus PathStatus { get; }
        public int GraphRevision { get; }
        public long GraphContentHash { get; }
        public bool HasNextWaypoint { get; }
        public FixVector3 NextWaypoint { get; }
        public long RemainingDistanceMillimeters { get; }
        public long TotalCostMillimeters { get; }
        public int ExpandedNodeCount { get; }
        public int PathQueryCount { get; }
        public int SegmentPathHitCount { get; }
        public CombatEntityId SegmentObstacleEntityId { get; }
        public CombatBodyId SegmentObstacleBodyId { get; }
        public Fix64 SegmentObstacleDistance { get; }
        public CharacterTestNavigationResolutionStatus ResolutionStatus { get; }
        public CharacterTestNavigationReplanReason ReplanReason { get; }
        public CharacterTestNavigationBlockedReason BlockedReason { get; }
        public CharacterTestNavigationFallbackReason FallbackReason { get; }
    }

    internal readonly struct CharacterTestNavigationSample
    {
        public CharacterTestNavigationSample(
            RuntimeFrame frame,
            int sourceEntityId,
            int targetEntityId,
            CharacterTestAiNavigationResult result,
            CharacterTestNavigationProbeShape queryShape,
            int layerMask,
            int clearanceRadiusMillimeters,
            int clearanceHeightMillimeters,
            int combatQueryCount,
            int directPathHitCount,
            int selectedPathHitCount,
            CombatEntityId nearestObstacleEntityId,
            CombatBodyId nearestObstacleBodyId,
            Fix64 nearestObstacleDistance,
            CharacterTestNavigationPathDetail pathDetail = default)
        {
            Frame = frame;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            Result = result;
            QueryShape = queryShape;
            LayerMask = layerMask;
            ClearanceRadiusMillimeters = clearanceRadiusMillimeters;
            ClearanceHeightMillimeters = clearanceHeightMillimeters;
            CombatQueryCount = combatQueryCount;
            DirectPathHitCount = directPathHitCount;
            SelectedPathHitCount = selectedPathHitCount;
            NearestObstacleEntityId = nearestObstacleEntityId;
            NearestObstacleBodyId = nearestObstacleBodyId;
            NearestObstacleDistance = nearestObstacleDistance;
            PathDetail = pathDetail;
        }

        public RuntimeFrame Frame { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public CharacterTestAiNavigationResult Result { get; }
        public CharacterTestNavigationProbeShape QueryShape { get; }
        public int LayerMask { get; }
        public int ClearanceRadiusMillimeters { get; }
        public int ClearanceHeightMillimeters { get; }
        public int CombatQueryCount { get; }
        public int DirectPathHitCount { get; }
        public int SelectedPathHitCount { get; }
        public CombatEntityId NearestObstacleEntityId { get; }
        public CombatBodyId NearestObstacleBodyId { get; }
        public Fix64 NearestObstacleDistance { get; }
        public CharacterTestNavigationPathDetail PathDetail { get; }
        public int PhysicsWorldRevision => PathDetail.PhysicsWorldRevision;
        public CharacterTestNavigationSessionBuildStatus SessionBuildStatus =>
            PathDetail.SessionBuildStatus;
        public CharacterTestNavigationSceneInputStatus SceneInputStatus =>
            PathDetail.SceneInputStatus;
        public CharacterTestPathStatus PathStatus => PathDetail.PathStatus;
        public int GraphRevision => PathDetail.GraphRevision;
        public long GraphContentHash => PathDetail.GraphContentHash;
        public bool HasNextWaypoint => PathDetail.HasNextWaypoint;
        public FixVector3 NextWaypoint => PathDetail.NextWaypoint;
        public long RemainingDistanceMillimeters => PathDetail.RemainingDistanceMillimeters;
        public long TotalCostMillimeters => PathDetail.TotalCostMillimeters;
        public int ExpandedNodeCount => PathDetail.ExpandedNodeCount;
        public int PathQueryCount => PathDetail.PathQueryCount;
        public int SegmentPathHitCount => PathDetail.SegmentPathHitCount;
        public CombatEntityId SegmentObstacleEntityId => PathDetail.SegmentObstacleEntityId;
        public CombatBodyId SegmentObstacleBodyId => PathDetail.SegmentObstacleBodyId;
        public Fix64 SegmentObstacleDistance => PathDetail.SegmentObstacleDistance;
        public CharacterTestNavigationResolutionStatus ResolutionStatus =>
            PathDetail.ResolutionStatus;
        public CharacterTestNavigationReplanReason ReplanReason => PathDetail.ReplanReason;
        public CharacterTestNavigationBlockedReason BlockedReason => PathDetail.BlockedReason;
        public CharacterTestNavigationFallbackReason FallbackReason => PathDetail.FallbackReason;
    }

    internal readonly struct CharacterTestEnvironmentSample
    {
        public CharacterTestEnvironmentSample(
            RuntimeFrame frame,
            int entityId,
            CombatSurfaceMaterialKind surfaceKind,
            bool nearMovableObject,
            bool nearObstacle,
            bool nearHazard,
            bool nearUsefulObject,
            int obstacleHitCount,
            CombatEntityId nearestObstacleEntityId,
            CombatBodyId nearestObstacleBodyId,
            Fix64 nearestObstacleDistance)
        {
            Frame = frame;
            EntityId = entityId;
            SurfaceKind = surfaceKind;
            OnHardSlick = surfaceKind == CombatSurfaceMaterialKind.HardSlick;
            NearMovableObject = nearMovableObject;
            NearObstacle = nearObstacle;
            NearHazard = nearHazard;
            NearUsefulObject = nearUsefulObject;
            ObstacleHitCount = obstacleHitCount;
            NearestObstacleEntityId = nearestObstacleEntityId;
            NearestObstacleBodyId = nearestObstacleBodyId;
            NearestObstacleDistance = nearestObstacleDistance;
        }

        public RuntimeFrame Frame { get; }
        public int EntityId { get; }
        public CombatSurfaceMaterialKind SurfaceKind { get; }
        public bool OnHardSlick { get; }
        public bool NearMovableObject { get; }
        public bool NearObstacle { get; }
        public bool NearHazard { get; }
        public bool NearUsefulObject { get; }
        public int ObstacleHitCount { get; }
        public CombatEntityId NearestObstacleEntityId { get; }
        public CombatBodyId NearestObstacleBodyId { get; }
        public Fix64 NearestObstacleDistance { get; }
    }

    internal sealed class CharacterTestPerceptionFrameCache
    {
        private const int InitialEntitySampleCapacity = 16;
        private const int InitialTargetGeometryCapacity = 64;
        private const int InitialQuerySampleCapacity = 32;
        private const float DirectionEpsilon = 0.0001f;
        private const int VisibilityQueryId = 910210;
        private const int EnvironmentQueryId = 910200;
        private const int NavigationQueryId = 910100;

        private readonly List<EntitySampleEntry> _entitySamples =
            new List<EntitySampleEntry>(InitialEntitySampleCapacity);
        private readonly List<TargetGeometryEntry> _targetGeometrySamples =
            new List<TargetGeometryEntry>(InitialTargetGeometryCapacity);
        private readonly List<VisibilityEntry> _visibilitySamples =
            new List<VisibilityEntry>(InitialQuerySampleCapacity);
        private readonly List<NavigationEntry> _navigationSamples =
            new List<NavigationEntry>(InitialQuerySampleCapacity);
        private readonly List<EnvironmentEntry> _environmentSamples =
            new List<EnvironmentEntry>(InitialQuerySampleCapacity);
        private readonly List<CombatQueryResult> _queryResults =
            new List<CombatQueryResult>(InitialQuerySampleCapacity);
        private readonly CharacterTestCompletedFrameGeometryCache _completedGeometry =
            new CharacterTestCompletedFrameGeometryCache();

        private CharacterTestCharacterRuntimeModule _runtimeModule;
        private CharacterTestCharacterPhysicsRuntime _physicsRuntime;
        private RuntimeFrame _frame;
        private bool _hasFrame;

        public CharacterTestPerceptionFrameCache(CharacterTestCharacterRuntimeModule runtimeModule)
        {
            _runtimeModule = runtimeModule;
            _physicsRuntime = runtimeModule?.PhysicsRuntime;
        }

        public bool HasActiveFrame => _hasFrame;
        public RuntimeFrame CurrentFrame => _frame;
        internal bool HasCompletedGeometry => _completedGeometry.HasCommittedGeometry;
        internal RuntimeFrame GeometryFrame => _completedGeometry.GeometryFrame;
        internal long GeometryGeneration => _completedGeometry.GeometryGeneration;
        internal CharacterTestCompletedFrameGeometryState GeometryState => _completedGeometry.State;
        internal int CompletedEntityCaptureCount => _completedGeometry.EntityCaptureCount;
        internal int CompletedPairBuildCount => _completedGeometry.PairBuildCount;
        internal int CompletedDirectedProjectionBuildCount =>
            _completedGeometry.DirectedProjectionBuildCount;
        internal int CompletedPairCacheHitCount => _completedGeometry.PairCacheHitCount;
        internal int CompletedPostMotionMutationCount => _completedGeometry.PostMotionMutationCount;
        internal int CompletedCapturedHurtboxCount => _completedGeometry.CapturedHurtboxCount;
        internal int CompletedCapturedDamageZoneCount => _completedGeometry.CapturedDamageZoneCount;
        internal int CompletedInitialHurtboxScanCount => _completedGeometry.InitialHurtboxScanCount;
        internal int CompletedZoneLowerBoundTestCount => _completedGeometry.ZoneLowerBoundTestCount;
        internal int CompletedLeafLowerBoundTestCount => _completedGeometry.LeafLowerBoundTestCount;
        internal int CompletedProvisionalAcquisitionCacheHitCount =>
            _completedGeometry.ProvisionalAcquisitionCacheHitCount;
        internal int CompletedExactShapeDistanceBuildCount =>
            _completedGeometry.ExactShapeDistanceBuildCount;
        internal int CompletedLockedHurtboxDistanceBuildCount =>
            _completedGeometry.LockedHurtboxDistanceBuildCount;
        internal int CompletedLockedHurtboxDistanceCacheHitCount =>
            _completedGeometry.LockedHurtboxDistanceCacheHitCount;
        internal int CompletedLockedHurtboxUnavailableCount =>
            _completedGeometry.LockedHurtboxUnavailableCount;
        internal int CompletedActorTargetChangeCount => _completedGeometry.ActorTargetChangeCount;
        internal int CompletedSubtargetSelectionCount => _completedGeometry.SubtargetSelectionCount;
        internal int CompletedSameActorReselectionAttemptCount =>
            _completedGeometry.SameActorReselectionAttemptCount;
        internal int TargetGeometryBuildCount { get; private set; }
        internal int VisibilityQueryCount { get; private set; }
        internal int NavigationQueryCount { get; private set; }
        internal int PathfindingQueryCount { get; private set; }
        internal int EnvironmentQueryCount { get; private set; }

        public void BeginFrame(
            RuntimeFrame frame,
            CharacterTestCharacterRuntimeModule runtimeModule,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            UpdateOwners(runtimeModule, physicsRuntime);
            _completedGeometry.BeginCompatibilityFrame(
                frame,
                _runtimeModule?.Entities,
                _runtimeModule,
                _physicsRuntime);
            BeginStage(frame);
        }

        internal bool BeginCommittedConsumerPhase(
            RuntimeFrame evaluationFrame,
            CharacterTestCharacterRuntimeModule runtimeModule,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            UpdateOwners(runtimeModule, physicsRuntime);
            bool available = _completedGeometry.BeginCommittedConsumerPhase(
                evaluationFrame,
                _runtimeModule?.Entities,
                _runtimeModule,
                _physicsRuntime);
            BeginStage(evaluationFrame);
            return available;
        }

        internal void BeginPostMotionGeometryFrame(
            RuntimeFrame frame,
            CharacterTestCharacterRuntimeModule runtimeModule,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            UpdateOwners(runtimeModule, physicsRuntime);
            _completedGeometry.BeginPostMotionGeometryFrame(
                frame,
                _runtimeModule?.Entities,
                _runtimeModule,
                _physicsRuntime);
            BeginStage(frame);
        }

        internal void CommitCompletedGeometryFrame(RuntimeFrame frame)
        {
            _completedGeometry.CommitCompletedGeometryFrame(
                frame,
                _runtimeModule?.Entities,
                _runtimeModule,
                _physicsRuntime);
        }

        internal void ResetForAuthorityRestore(RuntimeFrame frame)
        {
            _completedGeometry.ResetForAuthorityRestore(frame);
            _hasFrame = false;
            ClearFrameData();
        }

        internal void ResetCompletedGeometry()
        {
            _completedGeometry.Reset();
            _hasFrame = false;
            ClearFrameData();
        }

        internal bool TryAcquireNearestHurtbox(
            RuntimeFrame evaluationFrame,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            out CharacterTestCombatTargetHandle handle,
            out CharacterTestLockedHurtboxDistanceSample sample)
        {
            EnsureFrame(evaluationFrame);
            return _completedGeometry.TryAcquireNearestHurtbox(
                source,
                target,
                out handle,
                out sample);
        }

        internal bool TryGetLockedHurtboxDistance(
            RuntimeFrame evaluationFrame,
            in CharacterTestCombatTargetHandle handle,
            out CharacterTestLockedHurtboxDistanceSample sample)
        {
            EnsureFrame(evaluationFrame);
            return _completedGeometry.TryGetLockedHurtboxDistance(handle, out sample);
        }

        internal bool TryGetSpatialDistanceSquaredMillimeters(
            RuntimeFrame evaluationFrame,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            out long squaredDistance)
        {
            EnsureFrame(evaluationFrame);
            squaredDistance = 0L;
            if (source == null || target == null)
                return false;
            if (!_completedGeometry.TryGetPairGeometry(
                    source.Id.Value,
                    target.Id.Value,
                    out _,
                    out _,
                    out CharacterTestCompletedFramePairGeometry pair,
                    out _))
            {
                return false;
            }
            squaredDistance = pair.SqrSpatialDistanceMillimeters;
            return true;
        }

        internal bool TryGetCommittedPlanarDistanceSquaredMeters(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            out float squaredDistance)
        {
            squaredDistance = 0f;
            if (source == null
                || target == null
                || !_completedGeometry.TryGetCommittedPairGeometry(
                    source.Id.Value,
                    target.Id.Value,
                    out CharacterTestCompletedFramePairGeometry pair,
                    out _))
            {
                return false;
            }
            squaredDistance = pair.SqrDistanceMeters;
            return true;
        }

        public bool TryGetEntitySample(
            RuntimeFrame frame,
            CharacterTestCharacterEntity entity,
            out CharacterTestPerceptionEntitySample sample)
        {
            EnsureFrame(frame);
            return TryGetEntitySample(entity, out sample);
        }

        public bool TryGetTargetGeometry(
            RuntimeFrame frame,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            out CharacterTestTargetGeometrySample sample)
        {
            EnsureFrame(frame);
            return TryGetTargetGeometry(source, target, out sample);
        }

        public bool TryFindNearestTarget(
            RuntimeFrame frame,
            CharacterTestCharacterEntity source,
            CharacterTestPerceptionTargetRelationFilter relationFilter,
            float maxDistance,
            CharacterTestPerceptionTargetTiePolicy tiePolicy,
            out CharacterTestTargetGeometrySample sample,
            out CharacterTestCharacterEntity target)
        {
            EnsureFrame(frame);
            return TryFindNearestTarget(
                source,
                relationFilter,
                CharacterTestPerceptionTargetStateFilter.Any,
                maxDistance,
                tiePolicy,
                out sample,
                out target);
        }

        public bool TryFindNearestTarget(
            RuntimeFrame frame,
            CharacterTestCharacterEntity source,
            CharacterTestPerceptionTargetRelationFilter relationFilter,
            CharacterTestPerceptionTargetStateFilter stateFilter,
            float maxDistance,
            CharacterTestPerceptionTargetTiePolicy tiePolicy,
            out CharacterTestTargetGeometrySample sample,
            out CharacterTestCharacterEntity target)
        {
            EnsureFrame(frame);
            return TryFindNearestTarget(
                source,
                relationFilter,
                stateFilter,
                maxDistance,
                tiePolicy,
                out sample,
                out target);
        }

        public bool TryGetVisibility(
            RuntimeFrame frame,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings,
            out CharacterTestVisibilitySample sample)
        {
            EnsureFrame(frame);
            if (!TryGetTargetGeometry(source, target, out CharacterTestTargetGeometrySample geometry))
            {
                sample = default;
                return false;
            }

            return TryGetVisibility(source, target, settings, geometry, out sample);
        }

        public bool TryGetVisibility(
            RuntimeFrame frame,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings,
            CharacterTestTargetGeometrySample geometry,
            out CharacterTestVisibilitySample sample)
        {
            EnsureFrame(frame);
            return TryGetVisibility(source, target, settings, geometry, out sample);
        }

        public bool TryGetEnvironment(
            RuntimeFrame frame,
            CharacterTestCharacterEntity entity,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings,
            out CharacterTestEnvironmentSample sample)
        {
            EnsureFrame(frame);
            return TryGetEnvironment(entity, settings, out sample);
        }

        public bool TryGetNavigation(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            float attackRange,
            int navigationProfileKey,
            float probeMinLength,
            float probeMaxLength,
            float[] candidateAngles,
            out CharacterTestNavigationSample sample)
        {
            return TryGetNavigation(
                source,
                target,
                attackRange,
                navigationProfileKey,
                probeMinLength,
                probeMaxLength,
                candidateAngles,
                navigationSession: null,
                out sample);
        }

        internal bool TryGetNavigation(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            float attackRange,
            int navigationProfileKey,
            float probeMinLength,
            float probeMaxLength,
            float[] candidateAngles,
            CharacterTestNavigationSession navigationSession,
            out CharacterTestNavigationSample sample)
        {
            sample = default;
            if (!_hasFrame
                || source == null
                || target == null
                || _physicsRuntime == null
                || !_physicsRuntime.IsReady
                || _physicsRuntime.World == null)
            {
                return false;
            }

            if (!TryGetTargetGeometry(source, target, out CharacterTestTargetGeometrySample geometry))
                return false;

            int sourceId = source.Id.Value;
            int targetId = target.Id.Value;
            int attackRangeMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(attackRange);
            int probeMinMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(probeMinLength);
            int probeMaxMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(probeMaxLength);
            CombatPhysicsLayerMask layerMask = _physicsRuntime.MotionObstacleLayerMask;
            FixVector3 clearanceHalfExtents = _physicsRuntime.GetCharacterHalfExtents(source);
            int clearanceRadiusMillimeters = ToMillimeters(ResolveClearanceRadius(clearanceHalfExtents));
            int clearanceHeightMillimeters = ToMillimeters(
                clearanceHalfExtents.Y + clearanceHalfExtents.Y);
            FixVector3 clearanceCenter = ResolveEntityCenter(source);
            bool clearanceGrounded = source.IsGrounded;
            int candidateCount = candidateAngles != null ? candidateAngles.Length : 0;
            int physicsWorldRevision = _physicsRuntime.World.Revision;
            CharacterTestNavigationGraph graph = navigationSession?.Graph;
            int graphRevision = graph?.Revision ?? 0;
            long graphContentHash = graph?.ContentHash ?? 0L;
            CharacterTestNavigationSessionBuildStatus sessionBuildStatus =
                navigationSession?.BuildResult.Status
                ?? CharacterTestNavigationSessionBuildStatus.InputUnavailable;
            CharacterTestNavigationSceneInputStatus sceneInputStatus =
                navigationSession?.SceneInputResult.Status
                ?? CharacterTestNavigationSceneInputStatus.None;
            int maxEndpointSnapDistanceMillimeters = navigationSession?.IsAvailable == true
                ? navigationSession.Input.MaxEndpointSnapDistanceMillimeters
                : 0;
            int maxNodeExpansions = navigationSession?.IsAvailable == true
                ? navigationSession.Input.MaxNodeExpansions
                : 0;
            int maxPathNodeCount = navigationSession?.IsAvailable == true
                ? navigationSession.Input.MaxPathNodeCount
                : 0;
            for (int i = 0; i < _navigationSamples.Count; i++)
            {
                NavigationEntry entry = _navigationSamples[i];
                if (entry.Frame.Value == _frame.Value
                    && entry.SourceEntityId == sourceId
                    && entry.TargetEntityId == targetId
                    && entry.AttackRangeMillimeters == attackRangeMillimeters
                    && entry.NavigationProfileKey == navigationProfileKey
                    && entry.ProbeMinMillimeters == probeMinMillimeters
                    && entry.ProbeMaxMillimeters == probeMaxMillimeters
                    && entry.QueryShape == CharacterTestNavigationProbeShape.SweptObb
                    && entry.LayerMask == layerMask.Value
                    && entry.ClearanceRadiusMillimeters == clearanceRadiusMillimeters
                    && entry.ClearanceHeightMillimeters == clearanceHeightMillimeters
                    && entry.ClearanceCenter.Equals(clearanceCenter)
                    && entry.ClearanceGrounded == clearanceGrounded
                    && entry.CandidateCount == candidateCount
                    && entry.PhysicsWorldRevision == physicsWorldRevision
                    && entry.GraphRevision == graphRevision
                    && entry.GraphContentHash == graphContentHash
                    && entry.SessionBuildStatus == sessionBuildStatus
                    && entry.SceneInputStatus == sceneInputStatus
                    && entry.MaxEndpointSnapDistanceMillimeters
                        == maxEndpointSnapDistanceMillimeters
                    && entry.MaxNodeExpansions == maxNodeExpansions
                    && entry.MaxPathNodeCount == maxPathNodeCount)
                {
                    sample = entry.Sample;
                    return true;
                }
            }

            sample = BuildNavigationSample(
                source,
                geometry,
                attackRange,
                probeMinLength,
                probeMaxLength,
                candidateAngles,
                layerMask,
                clearanceHalfExtents,
                clearanceRadiusMillimeters,
                clearanceHeightMillimeters,
                navigationSession);
            _navigationSamples.Add(new NavigationEntry(
                _frame,
                sourceId,
                targetId,
                attackRangeMillimeters,
                navigationProfileKey,
                probeMinMillimeters,
                probeMaxMillimeters,
                CharacterTestNavigationProbeShape.SweptObb,
                layerMask.Value,
                clearanceRadiusMillimeters,
                clearanceHeightMillimeters,
                clearanceCenter,
                clearanceGrounded,
                candidateCount,
                physicsWorldRevision,
                graphRevision,
                graphContentHash,
                sessionBuildStatus,
                sceneInputStatus,
                maxEndpointSnapDistanceMillimeters,
                maxNodeExpansions,
                maxPathNodeCount,
                sample));
            return true;
        }

        internal bool TryGetCachedNavigation(
            int sourceEntityId,
            int targetEntityId,
            out CharacterTestNavigationSample sample)
        {
            if (_hasFrame)
            {
                for (int i = _navigationSamples.Count - 1; i >= 0; i--)
                {
                    NavigationEntry entry = _navigationSamples[i];
                    if (entry.Frame.Value == _frame.Value
                        && entry.SourceEntityId == sourceEntityId
                        && entry.TargetEntityId == targetEntityId)
                    {
                        sample = entry.Sample;
                        return true;
                    }
                }
            }

            sample = default;
            return false;
        }

        internal bool TryGetLatestCachedNavigation(
            int sourceEntityId,
            out CharacterTestNavigationSample sample)
        {
            if (_hasFrame)
            {
                for (int i = _navigationSamples.Count - 1; i >= 0; i--)
                {
                    NavigationEntry entry = _navigationSamples[i];
                    if (entry.Frame.Value == _frame.Value
                        && entry.SourceEntityId == sourceEntityId)
                    {
                        sample = entry.Sample;
                        return true;
                    }
                }
            }

            sample = default;
            return false;
        }

        private void UpdateOwners(
            CharacterTestCharacterRuntimeModule runtimeModule,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            if (runtimeModule != null)
                _runtimeModule = runtimeModule;
            _physicsRuntime = physicsRuntime ?? _runtimeModule?.PhysicsRuntime;
        }

        private void BeginStage(RuntimeFrame frame)
        {
            _frame = frame;
            _hasFrame = true;
            ClearFrameData();
        }

        private void ClearFrameData()
        {
            _entitySamples.Clear();
            _targetGeometrySamples.Clear();
            _visibilitySamples.Clear();
            _navigationSamples.Clear();
            _environmentSamples.Clear();
            _queryResults.Clear();
            TargetGeometryBuildCount = 0;
            VisibilityQueryCount = 0;
            NavigationQueryCount = 0;
            PathfindingQueryCount = 0;
            EnvironmentQueryCount = 0;
        }

        private void EnsureFrame(RuntimeFrame frame)
        {
            if (_hasFrame && _frame.Value == frame.Value)
                return;

            BeginFrame(frame, _runtimeModule, _runtimeModule?.PhysicsRuntime ?? _physicsRuntime);
        }

        private bool TryGetEntitySample(
            CharacterTestCharacterEntity entity,
            out CharacterTestPerceptionEntitySample sample)
        {
            sample = default;
            if (entity == null || entity.Id.Value <= 0)
                return false;

            int entityId = entity.Id.Value;
            for (int i = 0; i < _entitySamples.Count; i++)
            {
                EntitySampleEntry entry = _entitySamples[i];
                if (entry.EntityId == entityId)
                {
                    sample = entry.Sample;
                    return true;
                }
            }

            if (!_completedGeometry.TryGetEntityGeometry(
                    entityId,
                    out CharacterTestCompletedFrameEntityGeometry geometry))
            {
                return false;
            }

            sample = BuildEntitySample(entity, geometry);
            _entitySamples.Add(new EntitySampleEntry(entityId, sample));
            return true;
        }

        private CharacterTestPerceptionEntitySample BuildEntitySample(
            CharacterTestCharacterEntity entity,
            CharacterTestCompletedFrameEntityGeometry geometry)
        {
            CombatEntityId combatEntityId = ResolveCombatEntityId(entity);
            CombatBodyId bodyId = ResolveCombatBodyId(entity);
            FixVector3 position = geometry.Position;
            FixVector3 center = ResolveEntityCenter(entity, bodyId, position);
            return new CharacterTestPerceptionEntitySample(
                _frame,
                entity.Id.Value,
                combatEntityId,
                bodyId,
                entity.Team.TeamId,
                entity.BattleState.CountsAsAlive,
                position,
                center,
                geometry.YawMilliDegrees,
                entity.SurfaceMaterialKind);
        }

        private bool TryGetTargetGeometry(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            out CharacterTestTargetGeometrySample sample)
        {
            sample = default;
            if (source == null || target == null)
                return false;

            int sourceId = source.Id.Value;
            int targetId = target.Id.Value;
            for (int i = 0; i < _targetGeometrySamples.Count; i++)
            {
                TargetGeometryEntry entry = _targetGeometrySamples[i];
                if (entry.SourceEntityId == sourceId && entry.TargetEntityId == targetId)
                {
                    sample = entry.Sample;
                    return true;
                }
            }

            if (!_completedGeometry.TryGetPairGeometry(
                    sourceId,
                    targetId,
                    out _,
                    out _,
                    out CharacterTestCompletedFramePairGeometry pair,
                    out CharacterTestCompletedFrameDirectedGeometry directed)
                || !TryGetEntitySample(source, out CharacterTestPerceptionEntitySample sourceSample)
                || !TryGetEntitySample(target, out CharacterTestPerceptionEntitySample targetSample))
            {
                return false;
            }

            sample = new CharacterTestTargetGeometrySample(
                _frame,
                _completedGeometry.GeometryFrame,
                _completedGeometry.GeometryGeneration,
                sourceSample,
                targetSample,
                directed.DeltaXMillimeters,
                directed.DeltaZMillimeters,
                pair.SqrPlanarDistanceMillimeters,
                pair.SqrSpatialDistanceMillimeters,
                directed.DeltaXMeters,
                directed.DeltaZMeters,
                pair.SqrDistanceMeters,
                pair.DistanceMeters,
                directed.DirectionX,
                directed.DirectionZ,
                directed.DirectionXScaled,
                directed.DirectionZScaled,
                directed.TargetBearingMilliDegrees,
                directed.FacingDeltaMilliDegrees,
                directed.FacingDot);
            _targetGeometrySamples.Add(new TargetGeometryEntry(sourceId, targetId, sample));
            TargetGeometryBuildCount++;
            return true;
        }

        private bool TryFindNearestTarget(
            CharacterTestCharacterEntity source,
            CharacterTestPerceptionTargetRelationFilter relationFilter,
            CharacterTestPerceptionTargetStateFilter stateFilter,
            float maxDistance,
            CharacterTestPerceptionTargetTiePolicy tiePolicy,
            out CharacterTestTargetGeometrySample sample,
            out CharacterTestCharacterEntity target)
        {
            sample = default;
            target = null;
            IReadOnlyList<CharacterTestCharacterEntity> entities = _runtimeModule?.Entities;
            if (source == null || entities == null)
                return false;

            float bestSqrDistance = float.IsPositiveInfinity(maxDistance)
                ? float.MaxValue
                : maxDistance * maxDistance;
            bool found = false;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity candidate = entities[i];
                if (!AcceptTargetRelation(source, candidate, relationFilter))
                    continue;

                if (!TryGetTargetGeometry(source, candidate, out CharacterTestTargetGeometrySample candidateSample))
                    continue;
                if (!AcceptTargetState(candidateSample, stateFilter))
                    continue;

                bool select = !found
                    || candidateSample.SqrDistanceMeters < bestSqrDistance
                    || (tiePolicy == CharacterTestPerceptionTargetTiePolicy.Last
                        && candidateSample.SqrDistanceMeters <= bestSqrDistance);
                if (!select)
                    continue;

                if (!float.IsPositiveInfinity(maxDistance)
                    && candidateSample.SqrDistanceMeters > maxDistance * maxDistance)
                    continue;

                found = true;
                bestSqrDistance = candidateSample.SqrDistanceMeters;
                sample = candidateSample;
                target = candidate;
            }

            return found;
        }

        private static bool AcceptTargetState(
            CharacterTestTargetGeometrySample sample,
            CharacterTestPerceptionTargetStateFilter stateFilter)
        {
            switch (stateFilter)
            {
                case CharacterTestPerceptionTargetStateFilter.Alive:
                    return sample.TargetAlive;
                default:
                    return true;
            }
        }

        private bool TryGetVisibility(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings,
            CharacterTestTargetGeometrySample geometry,
            out CharacterTestVisibilitySample sample)
        {
            sample = default;
            if (source == null || target == null)
                return false;

            CharacterTestRuntimeAiPlannerPerceptionSettings safeSettings =
                CharacterTestRuntimeAiPlannerPerceptionSettings.Normalize(settings);
            if (geometry.DistanceMeters <= DirectionEpsilon)
            {
                sample = new CharacterTestVisibilitySample(
                    _frame,
                    source.Id.Value,
                    target.Id.Value,
                    lineOfSightBlocked: false,
                    targetVisible: safeSettings.VisionRange >= 0f,
                    hitCount: 0,
                    CombatEntityId.None,
                    CombatBodyId.None,
                    Fix64.Zero);
                return true;
            }

            CharacterTestCharacterPhysicsRuntime physicsRuntime = _physicsRuntime;
            if (physicsRuntime == null || !physicsRuntime.IsReady || physicsRuntime.World == null)
                return false;

            CombatPhysicsLayerMask layerMask = ResolveLineOfSightLayerMask(physicsRuntime, safeSettings);
            if (layerMask.Equals(CombatPhysicsLayerMask.None))
                return false;

            int sourceId = source.Id.Value;
            int targetId = target.Id.Value;
            int visionRangeMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(
                safeSettings.VisionRange);
            int layerMaskValue = layerMask.Value;
            for (int i = 0; i < _visibilitySamples.Count; i++)
            {
                VisibilityEntry entry = _visibilitySamples[i];
                if (entry.SourceEntityId == sourceId
                    && entry.TargetEntityId == targetId
                    && entry.VisionRangeMillimeters == visionRangeMillimeters
                    && entry.LayerMask == layerMaskValue)
                {
                    sample = entry.Sample;
                    return true;
                }
            }

            FixVector3 rayDirection = new FixVector3(
                ToFix64(geometry.DirectionX),
                Fix64.Zero,
                ToFix64(geometry.DirectionZ));
            var header = new CombatQueryHeader(
                VisibilityQueryId,
                CombatQueryKind.Ray,
                physicsRuntime.GetEntityId(source),
                VisibilityQueryId,
                actionId: 0,
                sourceOrder: 0,
                layerMask);
            _queryResults.Clear();
            physicsRuntime.World.QueryRay(
                new CombatRayQuery(header, geometry.SourceCenter, rayDirection, ToFix64(geometry.DistanceMeters)),
                _queryResults);
            VisibilityQueryCount++;

            int hitCount = _queryResults.Count;
            CombatEntityId nearestEntityId = CombatEntityId.None;
            CombatBodyId nearestBodyId = CombatBodyId.None;
            Fix64 nearestDistance = Fix64.Zero;
            if (hitCount > 0)
            {
                CombatQueryResult nearest = _queryResults[0];
                nearestEntityId = nearest.TargetEntityId;
                nearestBodyId = nearest.TargetBodyId;
                nearestDistance = nearest.Distance;
            }

            bool blocked = hitCount > 0;
            sample = new CharacterTestVisibilitySample(
                _frame,
                sourceId,
                targetId,
                blocked,
                geometry.DistanceMeters <= safeSettings.VisionRange && !blocked,
                hitCount,
                nearestEntityId,
                nearestBodyId,
                nearestDistance);
            _visibilitySamples.Add(new VisibilityEntry(
                sourceId,
                targetId,
                visionRangeMillimeters,
                layerMaskValue,
                sample));
            return true;
        }

        private bool TryGetEnvironment(
            CharacterTestCharacterEntity entity,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings,
            out CharacterTestEnvironmentSample sample)
        {
            sample = default;
            if (entity == null)
                return false;

            CharacterTestRuntimeAiPlannerPerceptionSettings safeSettings =
                CharacterTestRuntimeAiPlannerPerceptionSettings.Normalize(settings);
            float radius = safeSettings.EnvironmentSampleRadius;
            int entityId = entity.Id.Value;
            int radiusMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(radius);
            CombatPhysicsLayerMask layerMask = _physicsRuntime != null
                ? ResolveEnvironmentObstacleLayerMask(_physicsRuntime, safeSettings)
                : CombatPhysicsLayerMask.None;
            for (int i = 0; i < _environmentSamples.Count; i++)
            {
                EnvironmentEntry entry = _environmentSamples[i];
                if (entry.EntityId == entityId
                    && entry.RadiusMillimeters == radiusMillimeters
                    && entry.LayerMask == layerMask.Value)
                {
                    sample = entry.Sample;
                    return true;
                }
            }

            bool nearMovableObject = false;
            bool nearHazard = false;
            bool nearUsefulObject = false;
            bool nearObstacle = false;
            int obstacleHitCount = 0;
            CombatEntityId nearestObstacleEntityId = CombatEntityId.None;
            CombatBodyId nearestObstacleBodyId = CombatBodyId.None;
            Fix64 nearestObstacleDistance = Fix64.Zero;
            if (radius > 0f)
            {
                SampleMovableObjects(entity, radius, out nearMovableObject, out nearHazard, out nearUsefulObject);
                if (_physicsRuntime != null
                    && _physicsRuntime.IsReady
                    && _physicsRuntime.World != null
                    && !layerMask.Equals(CombatPhysicsLayerMask.None))
                {
                    QueryEnvironmentObstacles(
                        entity,
                        radius,
                        layerMask,
                        out nearObstacle,
                        out obstacleHitCount,
                        out nearestObstacleEntityId,
                        out nearestObstacleBodyId,
                        out nearestObstacleDistance);
                }
            }

            sample = new CharacterTestEnvironmentSample(
                _frame,
                entityId,
                entity.SurfaceMaterialKind,
                nearMovableObject,
                nearObstacle,
                nearHazard,
                nearUsefulObject,
                obstacleHitCount,
                nearestObstacleEntityId,
                nearestObstacleBodyId,
                nearestObstacleDistance);
            _environmentSamples.Add(new EnvironmentEntry(
                entityId,
                radiusMillimeters,
                layerMask.Value,
                sample));
            return true;
        }

        private CharacterTestNavigationSample BuildNavigationSample(
            CharacterTestCharacterEntity source,
            CharacterTestTargetGeometrySample geometry,
            float attackRange,
            float probeMinLength,
            float probeMaxLength,
            float[] candidateAngles,
            CombatPhysicsLayerMask layerMask,
            FixVector3 clearanceHalfExtents,
            int clearanceRadiusMillimeters,
            int clearanceHeightMillimeters,
            CharacterTestNavigationSession navigationSession)
        {
            if (geometry.SqrDistanceMeters <= DirectionEpsilon)
            {
                return new CharacterTestNavigationSample(
                    _frame,
                    source.Id.Value,
                    geometry.TargetEntityId,
                    new CharacterTestAiNavigationResult(
                        Vector2.zero,
                        directPathClear: true,
                        targetInAttackRange: true,
                        selectedCandidateIndex: -1),
                    CharacterTestNavigationProbeShape.SweptObb,
                    layerMask.Value,
                    clearanceRadiusMillimeters,
                    clearanceHeightMillimeters,
                    combatQueryCount: 0,
                    directPathHitCount: 0,
                    selectedPathHitCount: 0,
                    CombatEntityId.None,
                    CombatBodyId.None,
                    Fix64.Zero,
                    CreatePathDetail(
                        navigationSession,
                        CharacterTestPathStatus.None,
                        CharacterTestNavigationResolutionStatus.Coincident,
                        CharacterTestNavigationReplanReason.None,
                        CharacterTestNavigationBlockedReason.None,
                        CharacterTestNavigationFallbackReason.None));
            }

            int queryCount = 0;
            var directMove = new Vector2(geometry.DirectionX, geometry.DirectionZ);
            bool directPathClear = IsDirectionClear(
                source,
                directMove,
                geometry.DistanceMeters,
                probeMinLength,
                probeMaxLength,
                layerMask,
                clearanceHalfExtents,
                geometry.Target.CombatEntityId,
                querySlot: 0,
                clampProbeLength: true,
                out int directHitCount,
                out CombatEntityId nearestObstacleEntityId,
                out CombatBodyId nearestObstacleBodyId,
                out Fix64 nearestObstacleDistance);
            queryCount++;
            if (directPathClear)
            {
                return new CharacterTestNavigationSample(
                    _frame,
                    source.Id.Value,
                    geometry.TargetEntityId,
                    new CharacterTestAiNavigationResult(
                        directMove,
                        directPathClear: true,
                        targetInAttackRange: geometry.DistanceMeters <= attackRange,
                        selectedCandidateIndex: -1),
                    CharacterTestNavigationProbeShape.SweptObb,
                    layerMask.Value,
                    clearanceRadiusMillimeters,
                    clearanceHeightMillimeters,
                    queryCount,
                    directHitCount,
                    selectedPathHitCount: 0,
                    CombatEntityId.None,
                    CombatBodyId.None,
                    Fix64.Zero,
                    CreatePathDetail(
                        navigationSession,
                        CharacterTestPathStatus.None,
                        CharacterTestNavigationResolutionStatus.Direct,
                        CharacterTestNavigationReplanReason.None,
                        CharacterTestNavigationBlockedReason.None,
                        CharacterTestNavigationFallbackReason.None));
            }

            CharacterTestPathStatus pathStatus = CharacterTestPathStatus.GraphUnavailable;
            CharacterTestPathfindingSample pathSample = default;
            int pathQueryCount = 0;
            Vector2 steeringMove = directMove;
            float steeringDistance = geometry.DistanceMeters;
            int segmentPathHitCount = 0;
            CombatEntityId segmentObstacleEntityId = CombatEntityId.None;
            CombatBodyId segmentObstacleBodyId = CombatBodyId.None;
            Fix64 segmentObstacleDistance = Fix64.Zero;
            CharacterTestNavigationBlockedReason blockedReason =
                ResolveUnavailableBlockedReason(navigationSession);

            if (navigationSession?.IsAvailable == true)
            {
                var pathQuery = new CharacterTestPathQuery(
                    navigationSession.Graph.Revision,
                    navigationSession.Graph.ContentHash,
                    geometry.Source.Position,
                    geometry.Target.Position,
                    clearanceRadiusMillimeters,
                    clearanceHeightMillimeters,
                    navigationSession.Input.MaxEndpointSnapDistanceMillimeters,
                    navigationSession.Input.MaxNodeExpansions,
                    navigationSession.Input.MaxPathNodeCount);
                bool pathSuccess = navigationSession.Pathfinder.TryFindPath(
                    pathQuery,
                    navigationSession.PathBuffer,
                    out pathSample);
                PathfindingQueryCount++;
                pathQueryCount = 1;
                pathStatus = pathSample.Status;
                blockedReason = pathSuccess
                    ? CharacterTestNavigationBlockedReason.None
                    : ResolvePathBlockedReason(pathStatus);

                if (pathSuccess && pathSample.HasNextWaypoint)
                {
                    Vector2 waypointDelta = new Vector2(
                        ToFloat(pathSample.NextWaypoint.X - geometry.Source.Position.X),
                        ToFloat(pathSample.NextWaypoint.Z - geometry.Source.Position.Z));
                    float waypointSqrDistance = waypointDelta.sqrMagnitude;
                    if (waypointSqrDistance > DirectionEpsilon)
                    {
                        steeringDistance = Mathf.Sqrt(waypointSqrDistance);
                        steeringMove = waypointDelta / steeringDistance;
                        bool segmentClear = IsDirectionClear(
                            source,
                            steeringMove,
                            steeringDistance,
                            probeMinLength,
                            probeMaxLength,
                            layerMask,
                            clearanceHalfExtents,
                            geometry.Target.CombatEntityId,
                            querySlot: 1,
                            clampProbeLength: false,
                            out segmentPathHitCount,
                            out segmentObstacleEntityId,
                            out segmentObstacleBodyId,
                            out segmentObstacleDistance);
                        queryCount++;
                        if (segmentClear)
                        {
                            return new CharacterTestNavigationSample(
                                _frame,
                                source.Id.Value,
                                geometry.TargetEntityId,
                                new CharacterTestAiNavigationResult(
                                    steeringMove,
                                    directPathClear: false,
                                    targetInAttackRange: false,
                                    selectedCandidateIndex: -1),
                                CharacterTestNavigationProbeShape.SweptObb,
                                layerMask.Value,
                                clearanceRadiusMillimeters,
                                clearanceHeightMillimeters,
                                queryCount,
                                directHitCount,
                                selectedPathHitCount: 0,
                                nearestObstacleEntityId,
                                nearestObstacleBodyId,
                                nearestObstacleDistance,
                                CreatePathDetail(
                                    navigationSession,
                                    pathStatus,
                                    CharacterTestNavigationResolutionStatus.PathWaypoint,
                                    CharacterTestNavigationReplanReason.DirectPathBlocked,
                                    CharacterTestNavigationBlockedReason.None,
                                    CharacterTestNavigationFallbackReason.None,
                                    pathSample,
                                    pathQueryCount));
                        }

                        blockedReason = CharacterTestNavigationBlockedReason.SegmentBlocked;
                    }
                    else
                    {
                        blockedReason = CharacterTestNavigationBlockedReason.PathHasNoWaypoint;
                    }
                }
                else if (pathSuccess)
                {
                    blockedReason = CharacterTestNavigationBlockedReason.PathHasNoWaypoint;
                }
            }

            int candidateCount = candidateAngles != null ? candidateAngles.Length : 0;
            for (int i = 0; i < candidateCount; i++)
            {
                Vector2 candidateMove = Rotate(steeringMove, candidateAngles[i]);
                bool clear = IsDirectionClear(
                    source,
                    candidateMove,
                    steeringDistance,
                    probeMinLength,
                    probeMaxLength,
                    layerMask,
                    clearanceHalfExtents,
                    geometry.Target.CombatEntityId,
                    querySlot: i + 2,
                    clampProbeLength: true,
                    out int candidateHitCount,
                    out CombatEntityId _,
                    out CombatBodyId _,
                    out Fix64 _);
                queryCount++;
                if (!clear)
                    continue;

                return new CharacterTestNavigationSample(
                    _frame,
                    source.Id.Value,
                    geometry.TargetEntityId,
                    new CharacterTestAiNavigationResult(
                        candidateMove,
                        directPathClear: false,
                        targetInAttackRange: false,
                        selectedCandidateIndex: i),
                    CharacterTestNavigationProbeShape.SweptObb,
                    layerMask.Value,
                    clearanceRadiusMillimeters,
                    clearanceHeightMillimeters,
                    queryCount,
                    directHitCount,
                    candidateHitCount,
                    nearestObstacleEntityId,
                    nearestObstacleBodyId,
                    nearestObstacleDistance,
                    CreatePathDetail(
                        navigationSession,
                        pathStatus,
                        CharacterTestNavigationResolutionStatus.LocalCandidate,
                        CharacterTestNavigationReplanReason.DirectPathBlocked,
                        blockedReason,
                        CharacterTestNavigationFallbackReason.LocalCandidate,
                        pathSample,
                        pathQueryCount,
                        segmentPathHitCount,
                        segmentObstacleEntityId,
                        segmentObstacleBodyId,
                        segmentObstacleDistance));
            }

            return new CharacterTestNavigationSample(
                _frame,
                source.Id.Value,
                geometry.TargetEntityId,
                new CharacterTestAiNavigationResult(
                    Vector2.zero,
                    directPathClear: false,
                    targetInAttackRange: false,
                    selectedCandidateIndex: candidateCount),
                CharacterTestNavigationProbeShape.SweptObb,
                layerMask.Value,
                clearanceRadiusMillimeters,
                clearanceHeightMillimeters,
                queryCount,
                directHitCount,
                selectedPathHitCount: directHitCount,
                nearestObstacleEntityId,
                nearestObstacleBodyId,
                nearestObstacleDistance,
                CreatePathDetail(
                    navigationSession,
                    pathStatus,
                    CharacterTestNavigationResolutionStatus.Stopped,
                    CharacterTestNavigationReplanReason.DirectPathBlocked,
                    blockedReason,
                    CharacterTestNavigationFallbackReason.Stop,
                    pathSample,
                    pathQueryCount,
                    segmentPathHitCount,
                    segmentObstacleEntityId,
                    segmentObstacleBodyId,
                    segmentObstacleDistance));
        }

        private CharacterTestNavigationPathDetail CreatePathDetail(
            CharacterTestNavigationSession navigationSession,
            CharacterTestPathStatus pathStatus,
            CharacterTestNavigationResolutionStatus resolutionStatus,
            CharacterTestNavigationReplanReason replanReason,
            CharacterTestNavigationBlockedReason blockedReason,
            CharacterTestNavigationFallbackReason fallbackReason,
            CharacterTestPathfindingSample pathSample = default,
            int pathQueryCount = 0,
            int segmentPathHitCount = 0,
            CombatEntityId segmentObstacleEntityId = default,
            CombatBodyId segmentObstacleBodyId = default,
            Fix64 segmentObstacleDistance = default)
        {
            CharacterTestNavigationGraph graph = navigationSession?.Graph;
            int graphRevision = pathSample.GraphRevision != 0
                ? pathSample.GraphRevision
                : graph?.Revision ?? 0;
            long graphContentHash = pathSample.GraphContentHash != 0
                ? pathSample.GraphContentHash
                : graph?.ContentHash ?? 0L;
            return new CharacterTestNavigationPathDetail(
                _physicsRuntime?.World?.Revision ?? 0,
                navigationSession?.BuildResult.Status
                    ?? CharacterTestNavigationSessionBuildStatus.InputUnavailable,
                navigationSession?.SceneInputResult.Status
                    ?? CharacterTestNavigationSceneInputStatus.None,
                pathStatus,
                graphRevision,
                graphContentHash,
                pathSample.HasNextWaypoint,
                pathSample.NextWaypoint,
                pathSample.RemainingDistanceMillimeters,
                pathSample.TotalCostMillimeters,
                pathSample.ExpandedNodeCount,
                pathQueryCount,
                segmentPathHitCount,
                segmentObstacleEntityId,
                segmentObstacleBodyId,
                segmentObstacleDistance,
                resolutionStatus,
                replanReason,
                blockedReason,
                fallbackReason);
        }

        private static CharacterTestNavigationBlockedReason ResolveUnavailableBlockedReason(
            CharacterTestNavigationSession navigationSession)
        {
            if (navigationSession == null)
                return CharacterTestNavigationBlockedReason.GraphUnavailable;
            if (navigationSession.SceneInputResult.Status != CharacterTestNavigationSceneInputStatus.None
                && navigationSession.SceneInputResult.Status != CharacterTestNavigationSceneInputStatus.Success)
            {
                return CharacterTestNavigationBlockedReason.SceneInputUnavailable;
            }

            return navigationSession.BuildResult.Status
                == CharacterTestNavigationSessionBuildStatus.InputUnavailable
                ? CharacterTestNavigationBlockedReason.GraphUnavailable
                : CharacterTestNavigationBlockedReason.SessionBuildFailed;
        }

        private static CharacterTestNavigationBlockedReason ResolvePathBlockedReason(
            CharacterTestPathStatus status)
        {
            switch (status)
            {
                case CharacterTestPathStatus.GraphUnavailable:
                    return CharacterTestNavigationBlockedReason.GraphUnavailable;
                case CharacterTestPathStatus.InvalidQuery:
                    return CharacterTestNavigationBlockedReason.InvalidQuery;
                case CharacterTestPathStatus.StaleGraph:
                    return CharacterTestNavigationBlockedReason.StaleGraph;
                case CharacterTestPathStatus.SourceNodeUnavailable:
                    return CharacterTestNavigationBlockedReason.SourceNodeUnavailable;
                case CharacterTestPathStatus.TargetNodeUnavailable:
                    return CharacterTestNavigationBlockedReason.TargetNodeUnavailable;
                case CharacterTestPathStatus.SourceOutsideGraph:
                    return CharacterTestNavigationBlockedReason.SourceOutsideGraph;
                case CharacterTestPathStatus.TargetOutsideGraph:
                    return CharacterTestNavigationBlockedReason.TargetOutsideGraph;
                case CharacterTestPathStatus.Unreachable:
                    return CharacterTestNavigationBlockedReason.Unreachable;
                case CharacterTestPathStatus.BudgetExceeded:
                    return CharacterTestNavigationBlockedReason.BudgetExceeded;
                case CharacterTestPathStatus.PathBufferTooSmall:
                    return CharacterTestNavigationBlockedReason.PathBufferTooSmall;
                case CharacterTestPathStatus.CostOverflow:
                    return CharacterTestNavigationBlockedReason.CostOverflow;
                default:
                    return CharacterTestNavigationBlockedReason.InvalidQuery;
            }
        }

        private bool IsDirectionClear(
            CharacterTestCharacterEntity source,
            Vector2 direction,
            float distance,
            float probeMinLength,
            float probeMaxLength,
            CombatPhysicsLayerMask layerMask,
            FixVector3 clearanceHalfExtents,
            CombatEntityId ignoredTargetEntityId,
            int querySlot,
            bool clampProbeLength,
            out int hitCount,
            out CombatEntityId nearestEntityId,
            out CombatBodyId nearestBodyId,
            out Fix64 nearestDistance)
        {
            hitCount = 0;
            nearestEntityId = CombatEntityId.None;
            nearestBodyId = CombatBodyId.None;
            nearestDistance = Fix64.Zero;
            if (direction.sqrMagnitude <= DirectionEpsilon)
                return true;

            float resolvedProbeLength = clampProbeLength
                ? Mathf.Clamp(distance, probeMinLength, probeMaxLength)
                : Mathf.Max(0f, distance);
            Fix64 probeLength = ToFix64(resolvedProbeLength);
            FixVector3 origin = ResolveEntityCenter(source);
            FixVector3 probeDirection = new FixVector3(
                ToFix64(direction.x),
                Fix64.Zero,
                ToFix64(direction.y));
            var header = new CombatQueryHeader(
                queryId: NavigationQueryId + querySlot,
                kind: CombatQueryKind.Obb,
                sourceEntityId: _physicsRuntime.GetEntityId(source),
                traceId: NavigationQueryId,
                actionId: 0,
                sourceOrder: querySlot,
                layerMask);

            _queryResults.Clear();
            _physicsRuntime.World.Query(
                CharacterTestNavigationClearanceProbe.CreateQuery(
                    header, _physicsRuntime, source, origin, probeDirection, probeLength,
                    clearanceHalfExtents),
                _queryResults);
            NavigationQueryCount++;
            for (int i = 0; i < _queryResults.Count; i++)
            {
                CombatQueryResult candidate = _queryResults[i];
                if (!ignoredTargetEntityId.IsNone
                    && candidate.TargetEntityId.Equals(ignoredTargetEntityId))
                {
                    continue;
                }

                if (hitCount == 0)
                {
                    nearestEntityId = candidate.TargetEntityId;
                    nearestBodyId = candidate.TargetBodyId;
                    nearestDistance = candidate.Distance;
                }

                hitCount++;
            }

            return hitCount == 0;
        }

        private void SampleMovableObjects(
            CharacterTestCharacterEntity entity,
            float radius,
            out bool nearMovableObject,
            out bool nearHazard,
            out bool nearUsefulObject)
        {
            nearMovableObject = false;
            nearHazard = false;
            nearUsefulObject = false;
            IReadOnlyList<CharacterTestMovableNaturalObjectState> objects =
                _runtimeModule?.MovableNaturalObjects;
            if (objects == null || objects.Count == 0)
                return;

            float radiusSquared = radius * radius;
            float selfX = entity.Transform.X;
            float selfZ = entity.Transform.Z;
            for (int i = 0; i < objects.Count; i++)
            {
                CharacterTestMovableNaturalObjectState obj = objects[i];
                if (obj == null)
                    continue;

                float dx = obj.X - selfX;
                float dz = obj.Z - selfZ;
                if (dx * dx + dz * dz > radiusSquared)
                    continue;

                nearMovableObject = true;
                nearUsefulObject = true;
                nearHazard = HasHazardPotency(obj);
                if (nearHazard)
                    return;
            }
        }

        private void QueryEnvironmentObstacles(
            CharacterTestCharacterEntity entity,
            float radius,
            CombatPhysicsLayerMask layerMask,
            out bool nearObstacle,
            out int hitCount,
            out CombatEntityId nearestEntityId,
            out CombatBodyId nearestBodyId,
            out Fix64 nearestDistance)
        {
            var header = new CombatQueryHeader(
                EnvironmentQueryId,
                CombatQueryKind.Sphere,
                _physicsRuntime.GetEntityId(entity),
                EnvironmentQueryId,
                actionId: 0,
                sourceOrder: 0,
                layerMask);
            _queryResults.Clear();
            _physicsRuntime.World.QuerySphere(
                new CombatSphereQuery(header, ResolveEntityCenter(entity), ToFix64(radius)),
                _queryResults);
            EnvironmentQueryCount++;

            hitCount = _queryResults.Count;
            nearObstacle = hitCount > 0;
            nearestEntityId = CombatEntityId.None;
            nearestBodyId = CombatBodyId.None;
            nearestDistance = Fix64.Zero;
            if (hitCount <= 0)
                return;

            CombatQueryResult nearest = _queryResults[0];
            nearestEntityId = nearest.TargetEntityId;
            nearestBodyId = nearest.TargetBodyId;
            nearestDistance = nearest.Distance;
        }

        private FixVector3 ResolveEntityCenter(CharacterTestCharacterEntity entity)
        {
            if (entity == null)
                return FixVector3.Zero;

            return ResolveEntityCenter(
                entity,
                ResolveCombatBodyId(entity),
                new FixVector3(
                    ToFix64(entity.Transform.X),
                    ToFix64(entity.Transform.Y),
                    ToFix64(entity.Transform.Z)));
        }

        private FixVector3 ResolveEntityCenter(
            CharacterTestCharacterEntity entity,
            CombatBodyId bodyId,
            FixVector3 fallbackPosition)
        {
            if (_physicsRuntime != null
                && _physicsRuntime.World != null
                && _physicsRuntime.World.TryGetBody(bodyId, out CombatPhysicsBody body))
            {
                return body.Position;
            }

            Fix64 centerOffsetY = _physicsRuntime != null
                ? _physicsRuntime.GetCharacterCenterOffsetY(entity)
                : Fix64.Zero;
            return fallbackPosition + new FixVector3(Fix64.Zero, centerOffsetY, Fix64.Zero);
        }

        private static Fix64 ResolveClearanceRadius(FixVector3 clearanceHalfExtents)
        {
            Fix64 radius = clearanceHalfExtents.X > clearanceHalfExtents.Z
                ? clearanceHalfExtents.X
                : clearanceHalfExtents.Z;
            return radius < Fix64.Zero ? Fix64.Zero : radius;
        }

        private CombatEntityId ResolveCombatEntityId(CharacterTestCharacterEntity entity)
        {
            if (entity == null || entity.Id.Value <= 0)
                return CombatEntityId.None;

            return _physicsRuntime != null
                ? _physicsRuntime.GetEntityId(entity)
                : new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value);
        }

        private CombatBodyId ResolveCombatBodyId(CharacterTestCharacterEntity entity)
        {
            if (entity == null || entity.Id.Value <= 0)
                return CombatBodyId.None;

            return _physicsRuntime != null
                ? _physicsRuntime.GetBodyId(entity)
                : new CombatBodyId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value);
        }

        private static bool AcceptTargetRelation(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterTestPerceptionTargetRelationFilter relationFilter)
        {
            if (source == null || target == null || source.Id.Value == target.Id.Value)
                return false;

            switch (relationFilter)
            {
                case CharacterTestPerceptionTargetRelationFilter.OpposingTeam:
                    return source.Team.TeamId != target.Team.TeamId;
                default:
                    return true;
            }
        }

        private static CombatPhysicsLayerMask ResolveEnvironmentObstacleLayerMask(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings)
        {
            if (settings != null && settings.LineOfSightObstacleLayerMask > 0)
                return new CombatPhysicsLayerMask(settings.LineOfSightObstacleLayerMask);

            return physicsRuntime != null
                ? physicsRuntime.StaticObstacleLayerMask
                : CombatPhysicsLayerMask.None;
        }

        private static CombatPhysicsLayerMask ResolveLineOfSightLayerMask(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings)
        {
            if (settings != null && settings.LineOfSightObstacleLayerMask > 0)
                return new CombatPhysicsLayerMask(settings.LineOfSightObstacleLayerMask);

            return physicsRuntime != null
                ? physicsRuntime.ObstacleLayerMask
                : CombatPhysicsLayerMask.None;
        }

        private static bool HasHazardPotency(CharacterTestMovableNaturalObjectState obj)
        {
            CharacterTestGenesisPotencyTriplet potency = obj.CurrentPotency;
            return potency.Motion != 0 || potency.Cohesion != 0 || potency.Scatter != 0;
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos).normalized;
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }

        private static int ToMillimeters(Fix64 value)
        {
            return CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                value < Fix64.Zero ? Fix64.Zero : value);
        }

        private readonly struct EntitySampleEntry
        {
            public EntitySampleEntry(int entityId, CharacterTestPerceptionEntitySample sample)
            {
                EntityId = entityId;
                Sample = sample;
            }

            public int EntityId { get; }
            public CharacterTestPerceptionEntitySample Sample { get; }
        }

        private readonly struct TargetGeometryEntry
        {
            public TargetGeometryEntry(int sourceEntityId, int targetEntityId, CharacterTestTargetGeometrySample sample)
            {
                SourceEntityId = sourceEntityId;
                TargetEntityId = targetEntityId;
                Sample = sample;
            }

            public int SourceEntityId { get; }
            public int TargetEntityId { get; }
            public CharacterTestTargetGeometrySample Sample { get; }
        }

        private readonly struct VisibilityEntry
        {
            public VisibilityEntry(
                int sourceEntityId,
                int targetEntityId,
                int visionRangeMillimeters,
                int layerMask,
                CharacterTestVisibilitySample sample)
            {
                SourceEntityId = sourceEntityId;
                TargetEntityId = targetEntityId;
                VisionRangeMillimeters = visionRangeMillimeters;
                LayerMask = layerMask;
                Sample = sample;
            }

            public int SourceEntityId { get; }
            public int TargetEntityId { get; }
            public int VisionRangeMillimeters { get; }
            public int LayerMask { get; }
            public CharacterTestVisibilitySample Sample { get; }
        }

        private readonly struct NavigationEntry
        {
            public NavigationEntry(
                RuntimeFrame frame,
                int sourceEntityId,
                int targetEntityId,
                int attackRangeMillimeters,
                int navigationProfileKey,
                int probeMinMillimeters,
                int probeMaxMillimeters,
                CharacterTestNavigationProbeShape queryShape,
                int layerMask,
                int clearanceRadiusMillimeters,
                int clearanceHeightMillimeters,
                FixVector3 clearanceCenter,
                bool clearanceGrounded,
                int candidateCount,
                int physicsWorldRevision,
                int graphRevision,
                long graphContentHash,
                CharacterTestNavigationSessionBuildStatus sessionBuildStatus,
                CharacterTestNavigationSceneInputStatus sceneInputStatus,
                int maxEndpointSnapDistanceMillimeters,
                int maxNodeExpansions,
                int maxPathNodeCount,
                CharacterTestNavigationSample sample)
            {
                Frame = frame;
                SourceEntityId = sourceEntityId;
                TargetEntityId = targetEntityId;
                AttackRangeMillimeters = attackRangeMillimeters;
                NavigationProfileKey = navigationProfileKey;
                ProbeMinMillimeters = probeMinMillimeters;
                ProbeMaxMillimeters = probeMaxMillimeters;
                QueryShape = queryShape;
                LayerMask = layerMask;
                ClearanceRadiusMillimeters = clearanceRadiusMillimeters;
                ClearanceHeightMillimeters = clearanceHeightMillimeters;
                ClearanceCenter = clearanceCenter;
                ClearanceGrounded = clearanceGrounded;
                CandidateCount = candidateCount;
                PhysicsWorldRevision = physicsWorldRevision;
                GraphRevision = graphRevision;
                GraphContentHash = graphContentHash;
                SessionBuildStatus = sessionBuildStatus;
                SceneInputStatus = sceneInputStatus;
                MaxEndpointSnapDistanceMillimeters = maxEndpointSnapDistanceMillimeters;
                MaxNodeExpansions = maxNodeExpansions;
                MaxPathNodeCount = maxPathNodeCount;
                Sample = sample;
            }

            public RuntimeFrame Frame { get; }
            public int SourceEntityId { get; }
            public int TargetEntityId { get; }
            public int AttackRangeMillimeters { get; }
            public int NavigationProfileKey { get; }
            public int ProbeMinMillimeters { get; }
            public int ProbeMaxMillimeters { get; }
            public CharacterTestNavigationProbeShape QueryShape { get; }
            public int LayerMask { get; }
            public int ClearanceRadiusMillimeters { get; }
            public int ClearanceHeightMillimeters { get; }
            public FixVector3 ClearanceCenter { get; }
            public bool ClearanceGrounded { get; }
            public int CandidateCount { get; }
            public int PhysicsWorldRevision { get; }
            public int GraphRevision { get; }
            public long GraphContentHash { get; }
            public CharacterTestNavigationSessionBuildStatus SessionBuildStatus { get; }
            public CharacterTestNavigationSceneInputStatus SceneInputStatus { get; }
            public int MaxEndpointSnapDistanceMillimeters { get; }
            public int MaxNodeExpansions { get; }
            public int MaxPathNodeCount { get; }
            public CharacterTestNavigationSample Sample { get; }
        }

        private readonly struct EnvironmentEntry
        {
            public EnvironmentEntry(
                int entityId,
                int radiusMillimeters,
                int layerMask,
                CharacterTestEnvironmentSample sample)
            {
                EntityId = entityId;
                RadiusMillimeters = radiusMillimeters;
                LayerMask = layerMask;
                Sample = sample;
            }

            public int EntityId { get; }
            public int RadiusMillimeters { get; }
            public int LayerMask { get; }
            public CharacterTestEnvironmentSample Sample { get; }
        }
    }
}
