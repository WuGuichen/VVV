using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestCompletedFrameGeometryCache
    {
        private readonly CharacterTestBodyPartDamageZoneSelectionStateAdapter
            _damageZoneStateAdapter = new CharacterTestBodyPartDamageZoneSelectionStateAdapter();

        internal bool TryAcquireNearestHurtbox(
            CharacterTestCharacterEntity sourceEntity,
            CharacterTestCharacterEntity targetEntity,
            out CharacterTestCombatTargetHandle handle,
            out CharacterTestLockedHurtboxDistanceSample sample)
        {
            handle = default;
            sample = default;
            if (_active == null
                || sourceEntity == null
                || targetEntity == null
                || _runtimeModule == null)
            {
                return false;
            }

            _runtimeModule.TryGetDamageZoneSnapshot(
                targetEntity.Id.Value,
                out CharacterTestDamageZoneSnapshot liveZones);

            return _active.TryAcquireNearestHurtbox(
                _evaluationFrame,
                sourceEntity,
                targetEntity,
                liveZones,
                _damageZoneStateAdapter,
                out handle,
                out sample);
        }

        internal bool TryGetLockedHurtboxDistance(
            in CharacterTestCombatTargetHandle handle,
            out CharacterTestLockedHurtboxDistanceSample sample)
        {
            sample = default;
            return _active != null
                && _active.TryGetLockedHurtboxDistance(
                    _evaluationFrame,
                    handle,
                    out sample);
        }

        private sealed partial class FrameBuffer
        {
            private const int HurtboxDistanceQueryId = 920500;
            private readonly Dictionary<int, FrozenDamageGeometry> _damageGeometryByEntity =
                new Dictionary<int, FrozenDamageGeometry>(InitialEntityCapacity);
            private readonly Dictionary<ulong, AcquisitionMemo> _acquisitionMemo =
                new Dictionary<ulong, AcquisitionMemo>(InitialPairCapacity * 2);
            private readonly Dictionary<int, LockedDistanceMemo> _lockedDistanceMemo =
                new Dictionary<int, LockedDistanceMemo>(InitialEntityCapacity);

            private int _zoneLowerBoundTestCount;
            private int _leafLowerBoundTestCount;
            private int _provisionalAcquisitionCacheHitCount;

            internal int ZoneLowerBoundTestCount => _zoneLowerBoundTestCount;
            internal int LeafLowerBoundTestCount => _leafLowerBoundTestCount;
            internal int ProvisionalAcquisitionCacheHitCount =>
                _provisionalAcquisitionCacheHitCount;

            internal bool TryAcquireNearestHurtbox(
                RuntimeFrame evaluationFrame,
                CharacterTestCharacterEntity sourceEntity,
                CharacterTestCharacterEntity targetEntity,
                CharacterTestDamageZoneSnapshot liveZones,
                CharacterTestBodyPartDamageZoneSelectionStateAdapter dynamicStateAdapter,
                out CharacterTestCombatTargetHandle handle,
                out CharacterTestLockedHurtboxDistanceSample sample)
            {
                handle = default;
                sample = default;
                if (sourceEntity == null || targetEntity == null)
                    return false;

                int sourceEntityId = sourceEntity.Id.Value;
                int targetEntityId = targetEntity.Id.Value;
                ulong key = DirectedKey(sourceEntityId, targetEntityId);
                if (_acquisitionMemo.TryGetValue(key, out AcquisitionMemo memo))
                {
                    _provisionalAcquisitionCacheHitCount++;
                    handle = memo.Handle;
                    sample = WithEvaluationFrame(memo.Sample, evaluationFrame);
                    return memo.Success;
                }

                InitialHurtboxScanCount++;
                bool success = TryBuildNearestHurtbox(
                    evaluationFrame,
                    sourceEntity,
                    targetEntity,
                    liveZones,
                    dynamicStateAdapter,
                    out handle,
                    out sample);
                _acquisitionMemo.Add(key, new AcquisitionMemo(success, handle, sample));
                if (success)
                {
                    ActorTargetChangeCount++;
                    SubtargetSelectionCount++;
                }
                return success;
            }

            internal bool TryGetLockedHurtboxDistance(
                RuntimeFrame evaluationFrame,
                in CharacterTestCombatTargetHandle handle,
                out CharacterTestLockedHurtboxDistanceSample sample)
            {
                sample = default;
                if (!handle.HasTarget)
                    return false;

                if (_lockedDistanceMemo.TryGetValue(
                        handle.SourceEntityId,
                        out LockedDistanceMemo memo)
                    && memo.Handle.SameSubtarget(handle)
                    && memo.Handle.DamageZoneId == handle.DamageZoneId)
                {
                    LockedHurtboxDistanceCacheHitCount++;
                    sample = WithEvaluationFrame(memo.Sample, evaluationFrame);
                    return sample.HasDistance;
                }

                bool success = TryBuildLockedDistance(evaluationFrame, handle, out sample);
                _lockedDistanceMemo[handle.SourceEntityId] =
                    new LockedDistanceMemo(handle, sample);
                return success;
            }

            private bool TryBuildNearestHurtbox(
                RuntimeFrame evaluationFrame,
                CharacterTestCharacterEntity sourceEntity,
                CharacterTestCharacterEntity targetEntity,
                CharacterTestDamageZoneSnapshot liveZones,
                CharacterTestBodyPartDamageZoneSelectionStateAdapter dynamicStateAdapter,
                out CharacterTestCombatTargetHandle handle,
                out CharacterTestLockedHurtboxDistanceSample sample)
            {
                handle = default;
                sample = default;
                if (!TryGetEntity(sourceEntity.Id.Value, out CharacterTestCompletedFrameEntityGeometry source)
                    || !TryGetEntity(targetEntity.Id.Value, out CharacterTestCompletedFrameEntityGeometry target)
                    || !_damageGeometryByEntity.TryGetValue(
                        targetEntity.Id.Value,
                        out FrozenDamageGeometry damage)
                    || !damage.IsValid)
                {
                    return false;
                }

                CombatCapsuleQuery query = source.CreateCapsuleQuery(
                    HurtboxDistanceQueryId + source.EntityId,
                    sourceOrder: 0);
                GetCapsuleBounds(query, out FixVector3 sourceMin, out FixVector3 sourceMax);
                bool hasBest = false;
                Fix64 bestGap = Fix64.Zero;
                FrozenDamageZone bestZone = default;
                FrozenDamageZoneMember bestMember = default;
                FrozenHurtbox bestHurtbox = default;
                CombatShapeDistanceResult bestDistance = default;

                bool hasDamageZones = liveZones != null
                    && liveZones.IsValid
                    && damage.ZoneCount > 0;
                for (int zoneIndex = 0; hasDamageZones && zoneIndex < damage.ZoneCount; zoneIndex++)
                {
                    FrozenDamageZone zone = damage.Zones[zoneIndex];
                    if (!zone.IsAvailable || !zone.Hittable)
                        continue;
                    if (!liveZones.TryGetAvailableZone(
                            zone.ZoneId,
                            out CharacterTestDamageZoneAggregate liveZone)
                        || !dynamicStateAdapter.TryProject(
                            targetEntity,
                            liveZones,
                            liveZone,
                            out CharacterTestBodyPartDamageZoneSelectionState dynamicState)
                        || !dynamicState.Hittable)
                    {
                        continue;
                    }

                    _zoneLowerBoundTestCount++;
                    if (hasBest
                        && AabbDistanceSquared(
                            sourceMin,
                            sourceMax,
                            zone.WorldMin,
                            zone.WorldMax) > bestGap * bestGap)
                    {
                        continue;
                    }

                    int memberEnd = zone.MemberStart + zone.MemberCount;
                    for (int memberIndex = zone.MemberStart;
                         memberIndex < memberEnd;
                         memberIndex++)
                    {
                        FrozenDamageZoneMember member = damage.Members[memberIndex];
                        if ((uint)member.HurtboxOrdinal >= (uint)damage.HurtboxCount)
                            continue;
                        FrozenHurtbox hurtbox = damage.Hurtboxes[member.HurtboxOrdinal];
                        _leafLowerBoundTestCount++;
                        if (hasBest
                            && AabbDistanceSquared(
                                sourceMin,
                                sourceMax,
                                hurtbox.WorldMin,
                                hurtbox.WorldMax) > bestGap * bestGap)
                        {
                            continue;
                        }

                        CombatShapeDistanceResult distance =
                            CombatPhysicsWorld.CalculateCapsuleDistance(
                                query,
                                target.CombatEntityId,
                                new CombatBodyId(
                                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxBodyIdStart
                                    + target.EntityId),
                                hurtbox.CombatColliderId,
                                hurtbox.WorldShape,
                                targetIsCompoundLeaf: true,
                                hurtbox.Priority);
                        ExactShapeDistanceBuildCount++;
                        if (!distance.HasDistance)
                            continue;

                        if (!hasBest
                            || distance.SurfaceGap < bestGap
                            || (distance.SurfaceGap == bestGap
                                && CompareStable(
                                    zone,
                                    member,
                                    hurtbox,
                                    bestZone,
                                    bestMember,
                                    bestHurtbox) < 0))
                        {
                            hasBest = true;
                            bestGap = distance.SurfaceGap;
                            bestZone = zone;
                            bestMember = member;
                            bestHurtbox = hurtbox;
                            bestDistance = distance;
                        }
                    }
                }

                // DamageZone is optional metadata. Characters that only author
                // Hurtboxes still acquire the nearest exact leaf directly.
                if (!hasDamageZones)
                {
                    for (int hurtboxIndex = 0;
                         hurtboxIndex < damage.HurtboxCount;
                         hurtboxIndex++)
                    {
                        FrozenHurtbox hurtbox = damage.Hurtboxes[hurtboxIndex];
                        _leafLowerBoundTestCount++;
                        if (hasBest
                            && AabbDistanceSquared(
                                sourceMin,
                                sourceMax,
                                hurtbox.WorldMin,
                                hurtbox.WorldMax) > bestGap * bestGap)
                        {
                            continue;
                        }

                        CombatShapeDistanceResult distance =
                            CombatPhysicsWorld.CalculateCapsuleDistance(
                                query,
                                target.CombatEntityId,
                                new CombatBodyId(
                                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxBodyIdStart
                                    + target.EntityId),
                                hurtbox.CombatColliderId,
                                hurtbox.WorldShape,
                                targetIsCompoundLeaf: true,
                                hurtbox.Priority);
                        ExactShapeDistanceBuildCount++;
                        if (!distance.HasDistance)
                            continue;

                        if (!hasBest
                            || distance.SurfaceGap < bestGap
                            || (distance.SurfaceGap == bestGap
                                && CompareStable(hurtbox, bestHurtbox) < 0))
                        {
                            hasBest = true;
                            bestGap = distance.SurfaceGap;
                            bestHurtbox = hurtbox;
                            bestDistance = distance;
                        }
                    }
                }

                if (!hasBest)
                    return false;

                handle = new CharacterTestCombatTargetHandle(
                    source.EntityId,
                    target.EntityId,
                    target.GameplayEntityId,
                    target.CombatEntityId,
                    hasDamageZones ? bestZone.ZoneId : 0,
                    hasDamageZones ? bestMember.HitZoneId : bestHurtbox.HitZoneId,
                    bestHurtbox.LogicalHurtboxId,
                    damage.BindingRevision,
                    Frame,
                    hasDamageZones ? bestZone.ZoneLabel : string.Empty,
                    hasDamageZones ? bestMember.HitZoneLabel : bestHurtbox.HitZoneLabel,
                    bestHurtbox.LogicalHurtboxLabel);
                sample = CreateAvailableSample(
                    evaluationFrame,
                    damage,
                    bestHurtbox,
                    bestDistance);
                return true;
            }

            private bool TryBuildLockedDistance(
                RuntimeFrame evaluationFrame,
                in CharacterTestCombatTargetHandle handle,
                out CharacterTestLockedHurtboxDistanceSample sample)
            {
                sample = default;
                if (!TryGetEntity(
                        handle.SourceEntityId,
                        out CharacterTestCompletedFrameEntityGeometry source))
                {
                    sample = UnavailableSample(
                        CharacterTestLockedHurtboxAvailabilityStatus.SourceUnavailable,
                        evaluationFrame);
                    LockedHurtboxUnavailableCount++;
                    return false;
                }
                if (!TryGetEntity(
                        handle.TargetEntityId,
                        out CharacterTestCompletedFrameEntityGeometry target)
                    || !target.GameplayEntityId.Equals(handle.TargetGameplayEntityId)
                    || !target.CombatEntityId.Equals(handle.TargetCombatEntityId))
                {
                    sample = UnavailableSample(
                        CharacterTestLockedHurtboxAvailabilityStatus.TargetUnavailable,
                        evaluationFrame);
                    LockedHurtboxUnavailableCount++;
                    return false;
                }
                if (!_damageGeometryByEntity.TryGetValue(
                        handle.TargetEntityId,
                        out FrozenDamageGeometry damage)
                    || !damage.IsValid
                    || !damage.TryGetHurtbox(
                        handle.LogicalHurtboxId,
                        out FrozenHurtbox hurtbox)
                    || (handle.DamageZoneId > 0
                        ? !damage.ContainsMember(
                            handle.DamageZoneId,
                            handle.HitZoneId,
                            handle.LogicalHurtboxId)
                        : (handle.HitZoneId > 0
                            && handle.HitZoneId != hurtbox.HitZoneId)))
                {
                    sample = UnavailableSample(
                        CharacterTestLockedHurtboxAvailabilityStatus.HurtboxUnavailable,
                        evaluationFrame,
                        damage?.HurtboxRevision ?? 0);
                    LockedHurtboxUnavailableCount++;
                    return false;
                }

                CombatShapeDistanceResult distance =
                    CombatPhysicsWorld.CalculateCapsuleDistance(
                        source.CreateCapsuleQuery(
                            HurtboxDistanceQueryId + source.EntityId,
                            sourceOrder: 0),
                        target.CombatEntityId,
                        new CombatBodyId(
                            CharacterTestCharacterPhysicsRuntime.CharacterHurtboxBodyIdStart
                            + target.EntityId),
                        hurtbox.CombatColliderId,
                        hurtbox.WorldShape,
                        targetIsCompoundLeaf: true,
                        hurtbox.Priority);
                ExactShapeDistanceBuildCount++;
                LockedHurtboxDistanceBuildCount++;
                if (!distance.HasDistance)
                {
                    sample = UnavailableSample(
                        CharacterTestLockedHurtboxAvailabilityStatus.InvalidShape,
                        evaluationFrame,
                        damage.HurtboxRevision);
                    LockedHurtboxUnavailableCount++;
                    return false;
                }

                sample = CreateAvailableSample(
                    evaluationFrame,
                    damage,
                    hurtbox,
                    distance);
                return true;
            }

            private CharacterTestLockedHurtboxDistanceSample CreateAvailableSample(
                RuntimeFrame evaluationFrame,
                FrozenDamageGeometry damage,
                FrozenHurtbox hurtbox,
                CombatShapeDistanceResult distance)
            {
                long millimeters = CharacterTestNavigationDistance.ToCeilingMillimeters(
                    distance.SurfaceGap);
                return new CharacterTestLockedHurtboxDistanceSample(
                    CharacterTestLockedHurtboxAvailabilityStatus.Available,
                    evaluationFrame,
                    Frame,
                    Generation,
                    damage.HurtboxRevision,
                    millimeters,
                    distance.SurfaceGap,
                    distance.SourceWitnessPoint,
                    distance.TargetWitnessPoint,
                    hurtbox.AimPoint,
                    hurtbox.CombatColliderId,
                    hurtbox.WorldShape.Kind);
            }

            private CharacterTestLockedHurtboxDistanceSample UnavailableSample(
                CharacterTestLockedHurtboxAvailabilityStatus status,
                RuntimeFrame evaluationFrame,
                int hurtboxRevision = 0)
            {
                return new CharacterTestLockedHurtboxDistanceSample(
                    status,
                    evaluationFrame,
                    Frame,
                    Generation,
                    hurtboxRevision,
                    0L,
                    Fix64.Zero,
                    FixVector3.Zero,
                    FixVector3.Zero,
                    FixVector3.Zero,
                    CombatColliderId.None,
                    default);
            }

            private static CharacterTestLockedHurtboxDistanceSample WithEvaluationFrame(
                CharacterTestLockedHurtboxDistanceSample sample,
                RuntimeFrame evaluationFrame)
            {
                return new CharacterTestLockedHurtboxDistanceSample(
                    sample.Status,
                    evaluationFrame,
                    sample.GeometryFrame,
                    sample.GeometryGeneration,
                    sample.TargetHurtboxRevision,
                    sample.SurfaceGapMillimeters,
                    sample.SurfaceGap,
                    sample.SourceWitnessPoint,
                    sample.TargetWitnessPoint,
                    sample.AimPoint,
                    sample.TargetCombatColliderId,
                    sample.TargetShapeKind);
            }

            private void CaptureDamageGeometry(
                CharacterTestCharacterEntity entity,
                CharacterTestCharacterRuntimeModule runtimeModule,
                CharacterTestCharacterPhysicsRuntime physicsRuntime)
            {
                if (entity == null || entity.Id.Value <= 0)
                    return;
                if (!_damageGeometryByEntity.TryGetValue(
                        entity.Id.Value,
                        out FrozenDamageGeometry frozen))
                {
                    frozen = new FrozenDamageGeometry();
                    _damageGeometryByEntity.Add(entity.Id.Value, frozen);
                }

                if (runtimeModule == null
                    || !runtimeModule.TryGetDamageGeometrySnapshot(
                        entity.Id.Value,
                        out CharacterTestDamageGeometrySnapshot geometry))
                {
                    frozen.Invalidate();
                    return;
                }

                runtimeModule.TryGetDamageZoneSnapshot(
                    entity.Id.Value,
                    out CharacterTestDamageZoneSnapshot zones);

                FixVector3 bodyPosition = new FixVector3(
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                        entity.Transform.XMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                        entity.Transform.YMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                        entity.Transform.ZMillimeters));
                if (physicsRuntime?.World != null
                    && physicsRuntime.World.TryGetBody(
                        physicsRuntime.GetHurtboxBodyId(entity),
                        out CombatPhysicsBody body))
                {
                    bodyPosition = body.Position;
                }

                frozen.Capture(geometry, zones, bodyPosition);
                CapturedHurtboxCount += frozen.HurtboxCount;
                CapturedDamageZoneCount += frozen.ZoneCount;
            }

            private bool DamageGeometryMatches(
                int entityId,
                CharacterTestCharacterRuntimeModule runtimeModule)
            {
                if (!_damageGeometryByEntity.TryGetValue(
                        entityId,
                        out FrozenDamageGeometry frozen))
                {
                    return runtimeModule == null
                        || !runtimeModule.TryGetDamageGeometrySnapshot(entityId, out _);
                }
                if (runtimeModule == null
                    || !runtimeModule.TryGetDamageGeometrySnapshot(
                        entityId,
                        out CharacterTestDamageGeometrySnapshot geometry))
                {
                    return !frozen.IsValid;
                }
                runtimeModule.TryGetDamageZoneSnapshot(
                    entityId,
                    out CharacterTestDamageZoneSnapshot zones);
                return frozen.Matches(geometry, zones);
            }

            private void ResetDamageGeometryMemo()
            {
                _acquisitionMemo.Clear();
                _lockedDistanceMemo.Clear();
                CapturedHurtboxCount = 0;
                CapturedDamageZoneCount = 0;
                InitialHurtboxScanCount = 0;
                ExactShapeDistanceBuildCount = 0;
                LockedHurtboxDistanceBuildCount = 0;
                LockedHurtboxDistanceCacheHitCount = 0;
                LockedHurtboxUnavailableCount = 0;
                ActorTargetChangeCount = 0;
                SubtargetSelectionCount = 0;
                _zoneLowerBoundTestCount = 0;
                _leafLowerBoundTestCount = 0;
                _provisionalAcquisitionCacheHitCount = 0;
            }

            private void ClearDamageDistanceMemo()
            {
                _acquisitionMemo.Clear();
                _lockedDistanceMemo.Clear();
                InitialHurtboxScanCount = 0;
                ExactShapeDistanceBuildCount = 0;
                LockedHurtboxDistanceBuildCount = 0;
                LockedHurtboxDistanceCacheHitCount = 0;
                LockedHurtboxUnavailableCount = 0;
                ActorTargetChangeCount = 0;
                SubtargetSelectionCount = 0;
                _zoneLowerBoundTestCount = 0;
                _leafLowerBoundTestCount = 0;
                _provisionalAcquisitionCacheHitCount = 0;
            }

            private void ResetDamageGeometryStorage()
            {
                _damageGeometryByEntity.Clear();
                ResetDamageGeometryMemo();
            }

            private static Fix64 AabbDistanceSquared(
                FixVector3 firstMin,
                FixVector3 firstMax,
                FixVector3 secondMin,
                FixVector3 secondMax)
            {
                Fix64 dx = AxisGap(firstMin.X, firstMax.X, secondMin.X, secondMax.X);
                Fix64 dy = AxisGap(firstMin.Y, firstMax.Y, secondMin.Y, secondMax.Y);
                Fix64 dz = AxisGap(firstMin.Z, firstMax.Z, secondMin.Z, secondMax.Z);
                return dx * dx + dy * dy + dz * dz;
            }

            private static Fix64 AxisGap(
                Fix64 firstMin,
                Fix64 firstMax,
                Fix64 secondMin,
                Fix64 secondMax)
            {
                if (firstMax < secondMin)
                    return secondMin - firstMax;
                return secondMax < firstMin ? firstMin - secondMax : Fix64.Zero;
            }

            private static void GetCapsuleBounds(
                in CombatCapsuleQuery query,
                out FixVector3 min,
                out FixVector3 max)
            {
                FixVector3 radius = new FixVector3(
                    query.Radius,
                    query.Radius,
                    query.Radius);
                min = new FixVector3(
                    Fix64.Min(query.PointA.X, query.PointB.X),
                    Fix64.Min(query.PointA.Y, query.PointB.Y),
                    Fix64.Min(query.PointA.Z, query.PointB.Z)) - radius;
                max = new FixVector3(
                    Fix64.Max(query.PointA.X, query.PointB.X),
                    Fix64.Max(query.PointA.Y, query.PointB.Y),
                    Fix64.Max(query.PointA.Z, query.PointB.Z)) + radius;
            }

            private static int CompareStable(
                in FrozenDamageZone leftZone,
                in FrozenDamageZoneMember leftMember,
                in FrozenHurtbox leftHurtbox,
                in FrozenDamageZone rightZone,
                in FrozenDamageZoneMember rightMember,
                in FrozenHurtbox rightHurtbox)
            {
                int result = CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, leftZone.ZoneId)
                    .CompareTo(CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, rightZone.ZoneId));
                if (result != 0)
                    return result;
                result = CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, leftMember.HitZoneId)
                    .CompareTo(CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, rightMember.HitZoneId));
                if (result == 0)
                    result = leftMember.HitZoneId.CompareTo(rightMember.HitZoneId);
                return result != 0
                    ? result
                    : CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, leftHurtbox.LogicalHurtboxId)
                        .CompareTo(CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, rightHurtbox.LogicalHurtboxId));
            }

            private static int CompareStable(
                in FrozenHurtbox left,
                in FrozenHurtbox right)
            {
                int result = CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, left.HitZoneId)
                    .CompareTo(CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, right.HitZoneId));
                if (result == 0)
                    result = left.HitZoneId.CompareTo(right.HitZoneId);
                return result != 0
                    ? result
                    : CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, left.LogicalHurtboxId)
                        .CompareTo(CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, right.LogicalHurtboxId));
            }

            private readonly struct AcquisitionMemo
            {
                internal AcquisitionMemo(
                    bool success,
                    CharacterTestCombatTargetHandle handle,
                    CharacterTestLockedHurtboxDistanceSample sample)
                {
                    Success = success;
                    Handle = handle;
                    Sample = sample;
                }

                internal bool Success { get; }
                internal CharacterTestCombatTargetHandle Handle { get; }
                internal CharacterTestLockedHurtboxDistanceSample Sample { get; }
            }

            private readonly struct LockedDistanceMemo
            {
                internal LockedDistanceMemo(
                    CharacterTestCombatTargetHandle handle,
                    CharacterTestLockedHurtboxDistanceSample sample)
                {
                    Handle = handle;
                    Sample = sample;
                }

                internal CharacterTestCombatTargetHandle Handle { get; }
                internal CharacterTestLockedHurtboxDistanceSample Sample { get; }
            }

            private readonly struct FrozenHurtbox
            {
                internal FrozenHurtbox(
                    CharacterTestDamageGeometryColliderMetadata metadata,
                    CombatPhysicsShape worldShape)
                {
                    LogicalHurtboxId = metadata.LogicalColliderId;
                    LogicalHurtboxLabel = metadata.LogicalColliderLabel;
                    CombatColliderId = metadata.CombatColliderId;
                    PartId = metadata.PartId;
                    PartLabel = metadata.PartLabel;
                    HitZoneId = metadata.HitZoneId;
                    HitZoneLabel = metadata.HitZoneLabel;
                    Priority = metadata.Priority;
                    WorldShape = worldShape;
                    WorldMin = metadata.WorldMin;
                    WorldMax = metadata.WorldMax;
                    AimPoint = ResolveAimPoint(worldShape);
                }

                internal int LogicalHurtboxId { get; }
                internal string LogicalHurtboxLabel { get; }
                internal CombatColliderId CombatColliderId { get; }
                internal int PartId { get; }
                internal string PartLabel { get; }
                internal int HitZoneId { get; }
                internal string HitZoneLabel { get; }
                internal int Priority { get; }
                internal CombatPhysicsShape WorldShape { get; }
                internal FixVector3 WorldMin { get; }
                internal FixVector3 WorldMax { get; }
                internal FixVector3 AimPoint { get; }

                private static FixVector3 ResolveAimPoint(CombatPhysicsShape shape)
                {
                    switch (shape.Kind)
                    {
                        case CombatPhysicsShapeKind.Aabb:
                            return (shape.Min + shape.Max) * Fix64.Half;
                        case CombatPhysicsShapeKind.Sphere:
                        case CombatPhysicsShapeKind.Obb:
                            return shape.Center;
                        case CombatPhysicsShapeKind.Capsule:
                            return (shape.PointA + shape.PointB) * Fix64.Half;
                        default:
                            return FixVector3.Zero;
                    }
                }
            }

            private readonly struct FrozenDamageZone
            {
                internal FrozenDamageZone(
                    CharacterTestDamageZoneAggregate zone,
                    int memberStart,
                    int memberCount)
                {
                    ZoneId = zone.ZoneId;
                    Hittable = zone.Hittable;
                    Aimable = zone.Aimable;
                    Approachable = zone.Approachable;
                    ZoneLabel = zone.DisplayName;
                    AuthoredPriority = zone.AuthoredPriority;
                    MemberStart = memberStart;
                    MemberCount = memberCount;
                    IsAvailable = zone.IsAvailable && memberCount > 0;
                    WorldMin = zone.WorldMin;
                    WorldMax = zone.WorldMax;
                }

                internal int ZoneId { get; }
                internal string ZoneLabel { get; }
                internal bool Hittable { get; }
                internal bool Aimable { get; }
                internal bool Approachable { get; }
                internal int AuthoredPriority { get; }
                internal int MemberStart { get; }
                internal int MemberCount { get; }
                internal bool IsAvailable { get; }
                internal FixVector3 WorldMin { get; }
                internal FixVector3 WorldMax { get; }
            }

            private readonly struct FrozenDamageZoneMember
            {
                internal FrozenDamageZoneMember(
                    int zoneId,
                    CharacterTestDamageZoneMember member,
                    FrozenHurtbox hurtbox,
                    int hurtboxOrdinal)
                {
                    ZoneId = zoneId;
                    HitZoneId = member.HitZoneId;
                    HitZoneLabel = hurtbox.HitZoneLabel;
                    LogicalHurtboxId = member.LogicalHurtboxId;
                    LogicalHurtboxLabel = hurtbox.LogicalHurtboxLabel;
                    HurtboxOrdinal = hurtboxOrdinal;
                }

                internal int ZoneId { get; }
                internal int HitZoneId { get; }
                internal string HitZoneLabel { get; }
                internal int LogicalHurtboxId { get; }
                internal string LogicalHurtboxLabel { get; }
                internal int HurtboxOrdinal { get; }
            }

            private sealed class FrozenDamageGeometry
            {
                private FrozenHurtbox[] _hurtboxes = Array.Empty<FrozenHurtbox>();
                private FrozenDamageZone[] _zones = Array.Empty<FrozenDamageZone>();
                private FrozenDamageZoneMember[] _members =
                    Array.Empty<FrozenDamageZoneMember>();

                internal bool IsValid { get; private set; }
                internal RuntimeFrame Frame { get; private set; }
                internal int WorldRevision { get; private set; }
                internal int HurtboxRevision { get; private set; }
                internal int TopologyRevision { get; private set; }
                internal int BindingRevision { get; private set; }
                internal int ZoneRevision { get; private set; }
                internal int HurtboxCount { get; private set; }
                internal int ZoneCount { get; private set; }
                internal int MemberCount { get; private set; }
                internal FrozenHurtbox[] Hurtboxes => _hurtboxes;
                internal FrozenDamageZone[] Zones => _zones;
                internal FrozenDamageZoneMember[] Members => _members;

                internal void Capture(
                    CharacterTestDamageGeometrySnapshot geometry,
                    CharacterTestDamageZoneSnapshot zones,
                    FixVector3 bodyPosition)
                {
                    EnsureCapacity(ref _hurtboxes, geometry.ColliderCount);
                    HurtboxCount = geometry.ColliderCount;
                    for (int i = 0; i < HurtboxCount; i++)
                    {
                        CharacterTestDamageGeometryColliderMetadata metadata =
                            geometry.GetCollider(i);
                        _hurtboxes[i] = new FrozenHurtbox(
                            metadata,
                            TranslateShape(metadata.LocalShape, bodyPosition));
                    }

                    bool hasZones = zones != null && zones.IsValid;
                    int zoneCount = hasZones ? zones.ZoneCount : 0;
                    int memberCapacity = hasZones ? zones.MemberCount : 0;
                    EnsureCapacity(ref _zones, zoneCount);
                    EnsureCapacity(ref _members, memberCapacity);
                    ZoneCount = zoneCount;
                    MemberCount = 0;
                    for (int zoneIndex = 0; zoneIndex < ZoneCount; zoneIndex++)
                    {
                        CharacterTestDamageZoneAggregate zone = zones.GetZone(zoneIndex);
                        int memberStart = MemberCount;
                        int memberEnd = zone.MemberStart + zone.MemberCount;
                        for (int memberIndex = zone.MemberStart;
                             memberIndex < memberEnd;
                             memberIndex++)
                        {
                            CharacterTestDamageZoneMember member = zones.GetMember(memberIndex);
                            int ordinal = FindHurtboxOrdinal(member.LogicalHurtboxId);
                            if (ordinal < 0)
                                continue;
                            _members[MemberCount++] = new FrozenDamageZoneMember(
                                zone.ZoneId,
                                member,
                                _hurtboxes[ordinal],
                                ordinal);
                        }
                        _zones[zoneIndex] = new FrozenDamageZone(
                            zone,
                            memberStart,
                            MemberCount - memberStart);
                    }

                    Frame = geometry.Frame;
                    WorldRevision = geometry.WorldRevision;
                    HurtboxRevision = geometry.HurtboxRevision;
                    TopologyRevision = geometry.TopologyRevision;
                    BindingRevision = hasZones ? zones.BindingRevision : 0;
                    ZoneRevision = hasZones ? zones.ZoneRevision : 0;
                    IsValid = geometry.IsValid && HurtboxCount > 0;
                }

                internal bool Matches(
                    CharacterTestDamageGeometrySnapshot geometry,
                    CharacterTestDamageZoneSnapshot zones)
                {
                    bool hasZones = zones != null && zones.IsValid;
                    int zoneCount = hasZones ? zones.ZoneCount : 0;
                    int memberCount = hasZones ? zones.MemberCount : 0;
                    return IsValid == (geometry.IsValid && geometry.ColliderCount > 0)
                        && Frame.Value == geometry.Frame.Value
                        && WorldRevision == geometry.WorldRevision
                        && HurtboxRevision == geometry.HurtboxRevision
                        && TopologyRevision == geometry.TopologyRevision
                        && BindingRevision == (hasZones ? zones.BindingRevision : 0)
                        && ZoneRevision == (hasZones ? zones.ZoneRevision : 0)
                        && HurtboxCount == geometry.ColliderCount
                        && ZoneCount == zoneCount
                        && MemberCount == memberCount;
                }

                internal bool TryGetHurtbox(
                    int logicalHurtboxId,
                    out FrozenHurtbox hurtbox)
                {
                    int ordinal = FindHurtboxOrdinal(logicalHurtboxId);
                    if (ordinal >= 0)
                    {
                        hurtbox = _hurtboxes[ordinal];
                        return true;
                    }
                    hurtbox = default;
                    return false;
                }

                internal bool ContainsMember(
                    int zoneId,
                    int hitZoneId,
                    int logicalHurtboxId)
                {
                    for (int i = 0; i < MemberCount; i++)
                    {
                        FrozenDamageZoneMember member = _members[i];
                        if (member.ZoneId == zoneId
                            && member.LogicalHurtboxId == logicalHurtboxId
                            && (hitZoneId <= 0 || member.HitZoneId == hitZoneId))
                        {
                            return true;
                        }
                    }
                    return false;
                }

                internal void Invalidate()
                {
                    IsValid = false;
                    Frame = default;
                    WorldRevision = 0;
                    HurtboxRevision = 0;
                    TopologyRevision = 0;
                    BindingRevision = 0;
                    ZoneRevision = 0;
                    HurtboxCount = 0;
                    ZoneCount = 0;
                    MemberCount = 0;
                }

                private int FindHurtboxOrdinal(int logicalHurtboxId)
                {
                    int low = 0;
                    int high = HurtboxCount - 1;
                    while (low <= high)
                    {
                        int middle = low + ((high - low) >> 1);
                        int compare = CompareLogicalHurtboxId(
                            _hurtboxes[middle].LogicalHurtboxId,
                            logicalHurtboxId);
                        if (compare == 0)
                            return middle;
                        if (compare < 0)
                            low = middle + 1;
                        else
                            high = middle - 1;
                    }
                    return -1;
                }

                private static int CompareLogicalHurtboxId(int left, int right)
                {
                    int result = CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, left)
                        .CompareTo(CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, right));
                    return result != 0 ? result : left.CompareTo(right);
                }

                private static CombatPhysicsShape TranslateShape(
                    CombatPhysicsShape local,
                    FixVector3 bodyPosition)
                {
                    switch (local.Kind)
                    {
                        case CombatPhysicsShapeKind.Aabb:
                            return CombatPhysicsShape.Aabb(
                                local.Min + bodyPosition,
                                local.Max + bodyPosition);
                        case CombatPhysicsShapeKind.Sphere:
                            return CombatPhysicsShape.Sphere(
                                local.Center + bodyPosition,
                                local.Radius);
                        case CombatPhysicsShapeKind.Capsule:
                            return CombatPhysicsShape.Capsule(
                                local.PointA + bodyPosition,
                                local.PointB + bodyPosition,
                                local.Radius);
                        case CombatPhysicsShapeKind.Obb:
                            return CombatPhysicsShape.Obb(
                                local.Center + bodyPosition,
                                local.HalfExtents,
                                local.AxisX,
                                local.AxisY,
                                local.AxisZ);
                        default:
                            return local;
                    }
                }

                private static void EnsureCapacity<T>(ref T[] values, int required)
                {
                    if (values.Length >= required)
                        return;
                    int capacity = values.Length == 0 ? 1 : values.Length;
                    while (capacity < required)
                        capacity = Math.Max(required, capacity * 2);
                    Array.Resize(ref values, capacity);
                }
            }
        }
    }
}
