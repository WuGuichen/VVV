using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestCompletedFrameGeometryState
    {
        Unavailable = 0,
        Building = 1,
        Committed = 2
    }

    internal readonly struct CharacterTestCompletedFrameEntityGeometry
    {
        internal CharacterTestCompletedFrameEntityGeometry(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            if (entity == null)
            {
                EntityId = 0;
                GameplayEntityId = default;
                CombatEntityId = default;
                XMillimeters = 0;
                YMillimeters = 0;
                ZMillimeters = 0;
                YawMilliDegrees = 0;
                PoseRevision = 0;
                FacingRevision = 0;
                CapsulePointA = FixVector3.Zero;
                CapsulePointB = FixVector3.Zero;
                CapsuleRadius = Fix64.Zero;
                return;
            }

            EntityId = entity.Id.Value;
            GameplayEntityId = entity.GameplayEntityId;
            CombatEntityId = physicsRuntime?.GetEntityId(entity)
                ?? new CombatEntityId(
                    CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value);
            XMillimeters = entity.Transform.XMillimeters;
            YMillimeters = entity.Transform.YMillimeters;
            ZMillimeters = entity.Transform.ZMillimeters;
            YawMilliDegrees = entity.Transform.YawMilliDegrees;
            PoseRevision = entity.SourcePoseRevision;
            FacingRevision = entity.SourceFacingRevision;
            BuildWorldCapsule(
                entity,
                physicsRuntime,
                out FixVector3 pointA,
                out FixVector3 pointB,
                out Fix64 radius);
            CapsulePointA = pointA;
            CapsulePointB = pointB;
            CapsuleRadius = radius;
        }

        internal int EntityId { get; }
        internal GameplayEntityId GameplayEntityId { get; }
        internal CombatEntityId CombatEntityId { get; }
        internal int XMillimeters { get; }
        internal int YMillimeters { get; }
        internal int ZMillimeters { get; }
        internal int YawMilliDegrees { get; }
        internal int PoseRevision { get; }
        internal int FacingRevision { get; }
        internal FixVector3 CapsulePointA { get; }
        internal FixVector3 CapsulePointB { get; }
        internal Fix64 CapsuleRadius { get; }

        internal bool Matches(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            if (entity == null
                || EntityId != entity.Id.Value
                || !GameplayEntityId.Equals(entity.GameplayEntityId)
                || XMillimeters != entity.Transform.XMillimeters
                || YMillimeters != entity.Transform.YMillimeters
                || ZMillimeters != entity.Transform.ZMillimeters
                || YawMilliDegrees != entity.Transform.YawMilliDegrees)
            {
                return false;
            }

            BuildWorldCapsule(
                entity,
                physicsRuntime,
                out FixVector3 pointA,
                out FixVector3 pointB,
                out Fix64 radius);
            return CapsulePointA.Equals(pointA)
                && CapsulePointB.Equals(pointB)
                && CapsuleRadius.Equals(radius);
        }

        internal CombatCapsuleQuery CreateCapsuleQuery(int queryId, int sourceOrder)
        {
            var header = new CombatQueryHeader(
                queryId,
                CombatQueryKind.Capsule,
                CombatEntityId,
                traceId: queryId,
                actionId: 0,
                sourceOrder,
                CombatPhysicsLayerMask.FromLayer(
                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer));
            return new CombatCapsuleQuery(
                header,
                CapsulePointA,
                CapsulePointB,
                CapsuleRadius);
        }

        private static void BuildWorldCapsule(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            out FixVector3 pointA,
            out FixVector3 pointB,
            out Fix64 radius)
        {
            FixVector3 halfExtents = physicsRuntime != null
                ? physicsRuntime.GetCharacterHalfExtents(entity)
                : new FixVector3(Fix64.Half, Fix64.One, Fix64.Half);
            radius = Fix64.Max(halfExtents.X, halfExtents.Z);
            Fix64 centerOffsetY = physicsRuntime != null
                ? physicsRuntime.GetCharacterCenterOffsetY(entity)
                : halfExtents.Y;
            Fix64 halfSegment = Fix64.Max(Fix64.Zero, halfExtents.Y - radius);
            Fix64 x = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                entity.Transform.XMillimeters);
            Fix64 centerY = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                entity.Transform.YMillimeters) + centerOffsetY;
            Fix64 z = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                entity.Transform.ZMillimeters);
            pointA = new FixVector3(x, centerY - halfSegment, z);
            pointB = new FixVector3(x, centerY + halfSegment, z);
        }

        internal FixVector3 Position => new FixVector3(
            ToFix64(CharacterTestDeterministicUnits.MillimetersToMeters(XMillimeters)),
            ToFix64(CharacterTestDeterministicUnits.MillimetersToMeters(YMillimeters)),
            ToFix64(CharacterTestDeterministicUnits.MillimetersToMeters(ZMillimeters)));

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }
    }

    internal readonly struct CharacterTestCompletedFramePairGeometry
    {
        internal CharacterTestCompletedFramePairGeometry(
            int lowEntityId,
            int highEntityId,
            int canonicalDeltaXMillimeters,
            int canonicalDeltaYMillimeters,
            int canonicalDeltaZMillimeters,
            long sqrPlanarDistanceMillimeters,
            long sqrSpatialDistanceMillimeters,
            float canonicalDeltaXMeters,
            float canonicalDeltaZMeters,
            float sqrDistanceMeters,
            float distanceMeters)
        {
            LowEntityId = lowEntityId;
            HighEntityId = highEntityId;
            CanonicalDeltaXMillimeters = canonicalDeltaXMillimeters;
            CanonicalDeltaYMillimeters = canonicalDeltaYMillimeters;
            CanonicalDeltaZMillimeters = canonicalDeltaZMillimeters;
            SqrPlanarDistanceMillimeters = sqrPlanarDistanceMillimeters;
            SqrSpatialDistanceMillimeters = sqrSpatialDistanceMillimeters;
            CanonicalDeltaXMeters = canonicalDeltaXMeters;
            CanonicalDeltaZMeters = canonicalDeltaZMeters;
            SqrDistanceMeters = sqrDistanceMeters;
            DistanceMeters = distanceMeters;
        }

        internal int LowEntityId { get; }
        internal int HighEntityId { get; }
        internal int CanonicalDeltaXMillimeters { get; }
        internal int CanonicalDeltaYMillimeters { get; }
        internal int CanonicalDeltaZMillimeters { get; }
        internal long SqrPlanarDistanceMillimeters { get; }
        internal long SqrSpatialDistanceMillimeters { get; }
        internal float CanonicalDeltaXMeters { get; }
        internal float CanonicalDeltaZMeters { get; }
        internal float SqrDistanceMeters { get; }
        internal float DistanceMeters { get; }
    }

    internal readonly struct CharacterTestCompletedFrameDirectedGeometry
    {
        internal CharacterTestCompletedFrameDirectedGeometry(
            int deltaXMillimeters,
            int deltaZMillimeters,
            float deltaXMeters,
            float deltaZMeters,
            float directionX,
            float directionZ,
            int directionXScaled,
            int directionZScaled,
            int targetBearingMilliDegrees,
            int facingDeltaMilliDegrees,
            float facingDot)
        {
            DeltaXMillimeters = deltaXMillimeters;
            DeltaZMillimeters = deltaZMillimeters;
            DeltaXMeters = deltaXMeters;
            DeltaZMeters = deltaZMeters;
            DirectionX = directionX;
            DirectionZ = directionZ;
            DirectionXScaled = directionXScaled;
            DirectionZScaled = directionZScaled;
            TargetBearingMilliDegrees = targetBearingMilliDegrees;
            FacingDeltaMilliDegrees = facingDeltaMilliDegrees;
            FacingDot = facingDot;
        }

        internal int DeltaXMillimeters { get; }
        internal int DeltaZMillimeters { get; }
        internal float DeltaXMeters { get; }
        internal float DeltaZMeters { get; }
        internal float DirectionX { get; }
        internal float DirectionZ { get; }
        internal int DirectionXScaled { get; }
        internal int DirectionZScaled { get; }
        internal int TargetBearingMilliDegrees { get; }
        internal int FacingDeltaMilliDegrees { get; }
        internal float FacingDot { get; }
    }

    /// <summary>
    /// CharacterTest-owned double buffer for immutable completed-frame root,
    /// posture-capsule and Hurtbox geometry. Root pair and locked-Hurtbox metrics
    /// are memoized lazily and excluded from save/network/hash authority.
    /// </summary>
    internal sealed partial class CharacterTestCompletedFrameGeometryCache
    {
        private const int InitialEntityCapacity = 16;
        private const int InitialPairCapacity = 64;
        private const int DirectionScale = CharacterTestDeterministicUnits.InputAxisScale;
        private const float DirectionEpsilon = 0.0001f;

        private readonly FrameBuffer _bufferA = new FrameBuffer();
        private readonly FrameBuffer _bufferB = new FrameBuffer();
        private FrameBuffer _committed;
        private FrameBuffer _building;
        private FrameBuffer _active;
        private RuntimeFrame _evaluationFrame;
        private RuntimeFrame _pendingRestoredFrame;
        private bool _hasPendingRestoredFrame;
        private long _nextGeneration;
        private long _sessionGeneration = 1L;
        private CharacterTestCharacterRuntimeModule _runtimeModule;
        private CharacterTestCharacterPhysicsRuntime _physicsRuntime;

        internal bool HasActiveGeometry => _active != null;
        internal bool HasCommittedGeometry => _committed != null;
        internal RuntimeFrame EvaluationFrame => _evaluationFrame;
        internal RuntimeFrame GeometryFrame => _active?.Frame ?? default;
        internal long GeometryGeneration => _active?.Generation ?? 0L;
        internal long SessionGeneration => _sessionGeneration;
        internal CharacterTestCompletedFrameGeometryState State =>
            _active?.State ?? CharacterTestCompletedFrameGeometryState.Unavailable;
        internal int EntityCaptureCount => _active?.EntityCaptureCount ?? 0;
        internal int PairBuildCount => _active?.PairBuildCount ?? 0;
        internal int DirectedProjectionBuildCount => _active?.DirectedProjectionBuildCount ?? 0;
        internal int PairCacheHitCount => _active?.PairCacheHitCount ?? 0;
        internal int PostMotionMutationCount => _active?.PostMotionMutationCount ?? 0;
        internal int CapturedHurtboxCount => _active?.CapturedHurtboxCount ?? 0;
        internal int CapturedDamageZoneCount => _active?.CapturedDamageZoneCount ?? 0;
        internal int InitialHurtboxScanCount => _active?.InitialHurtboxScanCount ?? 0;
        internal int ZoneLowerBoundTestCount => _active?.ZoneLowerBoundTestCount ?? 0;
        internal int LeafLowerBoundTestCount => _active?.LeafLowerBoundTestCount ?? 0;
        internal int ProvisionalAcquisitionCacheHitCount =>
            _active?.ProvisionalAcquisitionCacheHitCount ?? 0;
        internal int ExactShapeDistanceBuildCount => _active?.ExactShapeDistanceBuildCount ?? 0;
        internal int LockedHurtboxDistanceBuildCount => _active?.LockedHurtboxDistanceBuildCount ?? 0;
        internal int LockedHurtboxDistanceCacheHitCount => _active?.LockedHurtboxDistanceCacheHitCount ?? 0;
        internal int LockedHurtboxUnavailableCount => _active?.LockedHurtboxUnavailableCount ?? 0;
        internal int ActorTargetChangeCount => _active?.ActorTargetChangeCount ?? 0;
        internal int SubtargetSelectionCount => _active?.SubtargetSelectionCount ?? 0;
        internal int SameActorReselectionAttemptCount => 0;

        internal bool BeginCommittedConsumerPhase(
            RuntimeFrame evaluationFrame,
            IReadOnlyList<CharacterTestCharacterEntity> currentEntities,
            CharacterTestCharacterRuntimeModule runtimeModule,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            _runtimeModule = runtimeModule;
            _physicsRuntime = physicsRuntime;
            if (_committed == null && _hasPendingRestoredFrame)
            {
                InstallRestoredCommittedFrame(_pendingRestoredFrame, currentEntities);
                _hasPendingRestoredFrame = false;
            }

            _evaluationFrame = evaluationFrame;
            _active = _committed;
            return _active != null;
        }

        internal bool BeginCommittedConsumerPhase(
            RuntimeFrame evaluationFrame,
            IReadOnlyList<CharacterTestCharacterEntity> currentEntities)
        {
            return BeginCommittedConsumerPhase(
                evaluationFrame,
                currentEntities,
                _runtimeModule,
                _physicsRuntime);
        }

        internal void BeginPostMotionGeometryFrame(
            RuntimeFrame frame,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterRuntimeModule runtimeModule,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            _runtimeModule = runtimeModule;
            _physicsRuntime = physicsRuntime;
            _evaluationFrame = frame;
            _building = ReferenceEquals(_committed, _bufferA) ? _bufferB : _bufferA;
            _building.Capture(
                frame,
                ++_nextGeneration,
                _sessionGeneration,
                entities,
                runtimeModule,
                physicsRuntime,
                CharacterTestCompletedFrameGeometryState.Building);
            _active = _building;
        }

        internal void BeginPostMotionGeometryFrame(
            RuntimeFrame frame,
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            BeginPostMotionGeometryFrame(
                frame,
                entities,
                _runtimeModule,
                _physicsRuntime);
        }

        internal void CommitCompletedGeometryFrame(
            RuntimeFrame frame,
            IReadOnlyList<CharacterTestCharacterEntity> finalEntities,
            CharacterTestCharacterRuntimeModule runtimeModule,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            if (_building == null || _building.Frame.Value != frame.Value)
                return;

            _runtimeModule = runtimeModule;
            _physicsRuntime = physicsRuntime;
            _building.Commit(finalEntities, runtimeModule, physicsRuntime);
            _committed = _building;
            _building = null;
            _active = _committed;
        }

        internal void CommitCompletedGeometryFrame(
            RuntimeFrame frame,
            IReadOnlyList<CharacterTestCharacterEntity> finalEntities)
        {
            CommitCompletedGeometryFrame(
                frame,
                finalEntities,
                _runtimeModule,
                _physicsRuntime);
        }

        internal void BeginCompatibilityFrame(
            RuntimeFrame frame,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterRuntimeModule runtimeModule = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null)
        {
            BeginPostMotionGeometryFrame(frame, entities, runtimeModule, physicsRuntime);
            CommitCompletedGeometryFrame(frame, entities, runtimeModule, physicsRuntime);
            _evaluationFrame = frame;
        }

        internal void ResetForAuthorityRestore(RuntimeFrame restoredFrame)
        {
            ResetBuffers();
            _pendingRestoredFrame = restoredFrame;
            _hasPendingRestoredFrame = true;
            _sessionGeneration++;
        }

        internal void Reset()
        {
            ResetBuffers();
            _hasPendingRestoredFrame = false;
            _sessionGeneration++;
        }

        internal bool TryGetEntityGeometry(
            int entityId,
            out CharacterTestCompletedFrameEntityGeometry geometry)
        {
            geometry = default;
            return _active != null && _active.TryGetEntity(entityId, out geometry);
        }

        internal bool TryGetPairGeometry(
            int sourceEntityId,
            int targetEntityId,
            out CharacterTestCompletedFrameEntityGeometry source,
            out CharacterTestCompletedFrameEntityGeometry target,
            out CharacterTestCompletedFramePairGeometry pair,
            out CharacterTestCompletedFrameDirectedGeometry directed)
        {
            source = default;
            target = default;
            pair = default;
            directed = default;
            return _active != null
                && _active.TryGetPair(
                    sourceEntityId,
                    targetEntityId,
                    out source,
                    out target,
                    out pair,
                    out directed);
        }

        internal bool TryGetCommittedPairGeometry(
            int sourceEntityId,
            int targetEntityId,
            out CharacterTestCompletedFramePairGeometry pair,
            out CharacterTestCompletedFrameDirectedGeometry directed)
        {
            pair = default;
            directed = default;
            return _committed != null
                && _committed.TryGetPair(
                    sourceEntityId,
                    targetEntityId,
                    out _,
                    out _,
                    out pair,
                    out directed);
        }

        private void InstallRestoredCommittedFrame(
            RuntimeFrame frame,
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            _building = ReferenceEquals(_committed, _bufferA) ? _bufferB : _bufferA;
            _building.Capture(
                frame,
                ++_nextGeneration,
                _sessionGeneration,
                entities,
                _runtimeModule,
                _physicsRuntime,
                CharacterTestCompletedFrameGeometryState.Building);
            _building.Commit(entities, _runtimeModule, _physicsRuntime);
            _committed = _building;
            _building = null;
        }

        private void ResetBuffers()
        {
            _bufferA.Reset();
            _bufferB.Reset();
            _committed = null;
            _building = null;
            _active = null;
        }

        private sealed partial class FrameBuffer
        {
            private readonly Dictionary<int, CharacterTestCompletedFrameEntityGeometry> _entities =
                new Dictionary<int, CharacterTestCompletedFrameEntityGeometry>(InitialEntityCapacity);
            private readonly HashSet<int> _activeEntityIds = new HashSet<int>();
            private readonly Dictionary<ulong, CharacterTestCompletedFramePairGeometry> _pairs =
                new Dictionary<ulong, CharacterTestCompletedFramePairGeometry>(InitialPairCapacity);
            private readonly Dictionary<ulong, CharacterTestCompletedFrameDirectedGeometry> _directed =
                new Dictionary<ulong, CharacterTestCompletedFrameDirectedGeometry>(InitialPairCapacity * 2);

            internal RuntimeFrame Frame { get; private set; }
            internal long Generation { get; private set; }
            internal long SessionGeneration { get; private set; }
            internal CharacterTestCompletedFrameGeometryState State { get; private set; }
            internal int EntityCaptureCount { get; private set; }
            internal int PairBuildCount { get; private set; }
            internal int DirectedProjectionBuildCount { get; private set; }
            internal int PairCacheHitCount { get; private set; }
            internal int PostMotionMutationCount { get; private set; }
            internal int CapturedHurtboxCount { get; private set; }
            internal int CapturedDamageZoneCount { get; private set; }
            internal int InitialHurtboxScanCount { get; private set; }
            internal int ExactShapeDistanceBuildCount { get; private set; }
            internal int LockedHurtboxDistanceBuildCount { get; private set; }
            internal int LockedHurtboxDistanceCacheHitCount { get; private set; }
            internal int LockedHurtboxUnavailableCount { get; private set; }
            internal int ActorTargetChangeCount { get; private set; }
            internal int SubtargetSelectionCount { get; private set; }

            internal void Capture(
                RuntimeFrame frame,
                long generation,
                long sessionGeneration,
                IReadOnlyList<CharacterTestCharacterEntity> entities,
                CharacterTestCharacterRuntimeModule runtimeModule,
                CharacterTestCharacterPhysicsRuntime physicsRuntime,
                CharacterTestCompletedFrameGeometryState state)
            {
                Frame = frame;
                Generation = generation;
                SessionGeneration = sessionGeneration;
                State = state;
                _entities.Clear();
                _activeEntityIds.Clear();
                _pairs.Clear();
                _directed.Clear();
                EntityCaptureCount = 0;
                PairBuildCount = 0;
                DirectedProjectionBuildCount = 0;
                PairCacheHitCount = 0;
                PostMotionMutationCount = 0;
                ResetDamageGeometryMemo();

                int count = entities?.Count ?? 0;
                for (int i = 0; i < count; i++)
                    CaptureEntity(entities[i], runtimeModule, physicsRuntime);
            }

            internal void Commit(
                IReadOnlyList<CharacterTestCharacterEntity> finalEntities,
                CharacterTestCharacterRuntimeModule runtimeModule,
                CharacterTestCharacterPhysicsRuntime physicsRuntime)
            {
                bool geometryChanged = false;
                _activeEntityIds.Clear();
                int count = finalEntities?.Count ?? 0;
                for (int i = 0; i < count; i++)
                {
                    CharacterTestCharacterEntity entity = finalEntities[i];
                    if (entity == null || entity.Id.Value <= 0)
                        continue;

                    int entityId = entity.Id.Value;
                    _activeEntityIds.Add(entityId);
                    if (_entities.TryGetValue(entityId, out CharacterTestCompletedFrameEntityGeometry captured)
                        && captured.Matches(entity, physicsRuntime)
                        && DamageGeometryMatches(entityId, runtimeModule))
                    {
                        continue;
                    }

                    _entities[entityId] = new CharacterTestCompletedFrameEntityGeometry(
                        entity,
                        physicsRuntime);
                    CaptureDamageGeometry(entity, runtimeModule, physicsRuntime);
                    EntityCaptureCount++;
                    PostMotionMutationCount++;
                    geometryChanged = true;
                }

                if (geometryChanged)
                {
                    _pairs.Clear();
                    _directed.Clear();
                    PairBuildCount = 0;
                    DirectedProjectionBuildCount = 0;
                    PairCacheHitCount = 0;
                    ClearDamageDistanceMemo();
                }

                State = CharacterTestCompletedFrameGeometryState.Committed;
            }

            internal bool TryGetEntity(
                int entityId,
                out CharacterTestCompletedFrameEntityGeometry geometry)
            {
                if (!_activeEntityIds.Contains(entityId))
                {
                    geometry = default;
                    return false;
                }
                return _entities.TryGetValue(entityId, out geometry);
            }

            internal bool TryGetPair(
                int sourceEntityId,
                int targetEntityId,
                out CharacterTestCompletedFrameEntityGeometry source,
                out CharacterTestCompletedFrameEntityGeometry target,
                out CharacterTestCompletedFramePairGeometry pair,
                out CharacterTestCompletedFrameDirectedGeometry directed)
            {
                source = default;
                target = default;
                pair = default;
                directed = default;
                if (!TryGetEntity(sourceEntityId, out source)
                    || !TryGetEntity(targetEntityId, out target))
                {
                    return false;
                }

                if (sourceEntityId == targetEntityId)
                {
                    pair = new CharacterTestCompletedFramePairGeometry(
                        sourceEntityId,
                        targetEntityId,
                        0,
                        0,
                        0,
                        0L,
                        0L,
                        0f,
                        0f,
                        0f,
                        0f);
                    directed = new CharacterTestCompletedFrameDirectedGeometry(
                        0,
                        0,
                        0f,
                        0f,
                        0f,
                        0f,
                        0,
                        0,
                        0,
                        0,
                        0f);
                    return true;
                }

                ulong pairKey = PairKey(sourceEntityId, targetEntityId);
                if (!_pairs.TryGetValue(pairKey, out pair))
                {
                    int lowId = sourceEntityId < targetEntityId ? sourceEntityId : targetEntityId;
                    int highId = sourceEntityId < targetEntityId ? targetEntityId : sourceEntityId;
                    CharacterTestCompletedFrameEntityGeometry low =
                        sourceEntityId == lowId ? source : target;
                    CharacterTestCompletedFrameEntityGeometry high =
                        sourceEntityId == lowId ? target : source;
                    pair = BuildPair(low, high);
                    _pairs.Add(pairKey, pair);
                    PairBuildCount++;
                }
                else
                {
                    PairCacheHitCount++;
                }

                ulong directedKey = DirectedKey(sourceEntityId, targetEntityId);
                if (!_directed.TryGetValue(directedKey, out directed))
                {
                    directed = BuildDirected(source, pair);
                    _directed.Add(directedKey, directed);
                    DirectedProjectionBuildCount++;
                }
                return true;
            }

            internal void Reset()
            {
                State = CharacterTestCompletedFrameGeometryState.Unavailable;
                _entities.Clear();
                _activeEntityIds.Clear();
                _pairs.Clear();
                _directed.Clear();
                EntityCaptureCount = 0;
                PairBuildCount = 0;
                DirectedProjectionBuildCount = 0;
                PairCacheHitCount = 0;
                PostMotionMutationCount = 0;
                ResetDamageGeometryStorage();
            }

            private void CaptureEntity(
                CharacterTestCharacterEntity entity,
                CharacterTestCharacterRuntimeModule runtimeModule,
                CharacterTestCharacterPhysicsRuntime physicsRuntime)
            {
                if (entity == null || entity.Id.Value <= 0)
                    return;
                var geometry = new CharacterTestCompletedFrameEntityGeometry(entity, physicsRuntime);
                _entities[geometry.EntityId] = geometry;
                _activeEntityIds.Add(geometry.EntityId);
                EntityCaptureCount++;
                CaptureDamageGeometry(entity, runtimeModule, physicsRuntime);
            }

            private static CharacterTestCompletedFramePairGeometry BuildPair(
                CharacterTestCompletedFrameEntityGeometry low,
                CharacterTestCompletedFrameEntityGeometry high)
            {
                int dx = high.XMillimeters - low.XMillimeters;
                int dy = high.YMillimeters - low.YMillimeters;
                int dz = high.ZMillimeters - low.ZMillimeters;
                float dxMeters = CharacterTestDeterministicUnits.MillimetersToMeters(dx);
                float dzMeters = CharacterTestDeterministicUnits.MillimetersToMeters(dz);
                float sqrDistanceMeters = dxMeters * dxMeters + dzMeters * dzMeters;
                float distanceMeters = sqrDistanceMeters > 0f ? Mathf.Sqrt(sqrDistanceMeters) : 0f;
                return new CharacterTestCompletedFramePairGeometry(
                    low.EntityId,
                    high.EntityId,
                    dx,
                    dy,
                    dz,
                    SaturatingSquaredDistance(dx, 0, dz),
                    SaturatingSquaredDistance(dx, dy, dz),
                    dxMeters,
                    dzMeters,
                    sqrDistanceMeters,
                    distanceMeters);
            }

            private static CharacterTestCompletedFrameDirectedGeometry BuildDirected(
                CharacterTestCompletedFrameEntityGeometry source,
                CharacterTestCompletedFramePairGeometry pair)
            {
                bool canonical = source.EntityId == pair.LowEntityId;
                int dx = canonical
                    ? pair.CanonicalDeltaXMillimeters
                    : -pair.CanonicalDeltaXMillimeters;
                int dz = canonical
                    ? pair.CanonicalDeltaZMillimeters
                    : -pair.CanonicalDeltaZMillimeters;
                float dxMeters = canonical
                    ? pair.CanonicalDeltaXMeters
                    : -pair.CanonicalDeltaXMeters;
                float dzMeters = canonical
                    ? pair.CanonicalDeltaZMeters
                    : -pair.CanonicalDeltaZMeters;
                float distance = pair.DistanceMeters;
                float directionX = distance > DirectionEpsilon ? dxMeters / distance : 0f;
                float directionZ = distance > DirectionEpsilon ? dzMeters / distance : 0f;
                CharacterTestDeterministicMath.NormalizeToScale(
                    dx,
                    dz,
                    DirectionScale,
                    out int directionXScaled,
                    out int directionZScaled);
                int targetBearing = CharacterTestDeterministicMath.Atan2MilliDegrees(dx, dz);
                int facingDelta = CharacterTestDeterministicMath.ShortestAngleDelta(
                    source.YawMilliDegrees,
                    targetBearing);
                CharacterTestDeterministicMath.SinCosMilliDegrees(
                    source.YawMilliDegrees,
                    out int forwardXScaled,
                    out int forwardZScaled);
                long facingDotRaw =
                    ((long)forwardXScaled * directionXScaled)
                    + ((long)forwardZScaled * directionZScaled);
                float facingDot = (float)(facingDotRaw
                    / (double)(CharacterTestDeterministicMath.TrigScale * DirectionScale));
                return new CharacterTestCompletedFrameDirectedGeometry(
                    dx,
                    dz,
                    dxMeters,
                    dzMeters,
                    directionX,
                    directionZ,
                    directionXScaled,
                    directionZScaled,
                    targetBearing,
                    facingDelta,
                    facingDot);
            }

            private static ulong PairKey(int first, int second)
            {
                uint low = (uint)(first < second ? first : second);
                uint high = (uint)(first < second ? second : first);
                return ((ulong)low << 32) | high;
            }

            private static ulong DirectedKey(int source, int target)
            {
                return ((ulong)(uint)source << 32) | (uint)target;
            }

            private static long SaturatingSquaredDistance(long x, long y, long z)
            {
                ulong ax = AbsAsUnsigned(x);
                ulong ay = AbsAsUnsigned(y);
                ulong az = AbsAsUnsigned(z);
                const ulong squareRootLongMax = 3037000499UL;
                if (ax > squareRootLongMax || ay > squareRootLongMax || az > squareRootLongMax)
                    return long.MaxValue;
                ulong total = ax * ax;
                ulong ySquared = ay * ay;
                ulong zSquared = az * az;
                if (total > (ulong)long.MaxValue - ySquared)
                    return long.MaxValue;
                total += ySquared;
                if (total > (ulong)long.MaxValue - zSquared)
                    return long.MaxValue;
                return (long)(total + zSquared);
            }

            private static ulong AbsAsUnsigned(long value)
            {
                return value >= 0L ? (ulong)value : (ulong)(-(value + 1L)) + 1UL;
            }
        }
    }
}
