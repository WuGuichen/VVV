using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    internal interface ICharacterTestCharacterColliderSync
    {
        /// <remarks>NoAlloc after initialization.</remarks>
        void SyncAllCharacterCombatColliders(IReadOnlyList<CharacterTestCharacterEntity> entities);
    }

    internal enum CharacterTestBodyPresenceContactBuildStatus
    {
        NotRequested = 0,
        Success = 1,
        SourceUnavailable = 2,
        CapacityExceeded = 3,
        InvalidSeparation = 4,
        UnsupportedSeparation = 5,
        InvalidIdentity = 6,
        ConflictingDuplicate = 7,
        InvalidMotionWitness = 8,
        InvalidPressureScale = 9,
        ArithmeticOverflow = 10,
        DegenerateSurfaceMotion = 11
    }

    internal readonly struct CharacterTestBodyPresenceContactDiagnostics
    {
        internal CharacterTestBodyPresenceContactDiagnostics(
            CharacterTestBodyPresenceContactBuildStatus status,
            int rawCandidateCount,
            int rawCandidateCapacity,
            int pairCount,
            int pairCapacity,
            int constraintCount,
            int constraintCapacity,
            int pressureIterationCount,
            int surfaceClampCount)
        {
            Status = status;
            RawCandidateCount = rawCandidateCount;
            RawCandidateCapacity = rawCandidateCapacity;
            PairCount = pairCount;
            PairCapacity = pairCapacity;
            ConstraintCount = constraintCount;
            ConstraintCapacity = constraintCapacity;
            PressureIterationCount = pressureIterationCount;
            SurfaceClampCount = surfaceClampCount;
        }

        internal CharacterTestBodyPresenceContactBuildStatus Status { get; }
        internal int RawCandidateCount { get; }
        internal int RawCandidateCapacity { get; }
        internal int PairCount { get; }
        internal int PairCapacity { get; }
        internal int ConstraintCount { get; }
        internal int ConstraintCapacity { get; }
        internal int PressureIterationCount { get; }
        internal int SurfaceClampCount { get; }
        internal bool HasFailure => Status != CharacterTestBodyPresenceContactBuildStatus.NotRequested
            && Status != CharacterTestBodyPresenceContactBuildStatus.Success;
    }

    internal readonly struct CharacterTestBodyPresenceConstraintDiagnostic
    {
        internal CharacterTestBodyPresenceConstraintDiagnostic(
            int presenceEntityId,
            int targetEntityId,
            int presenceBodyId,
            int colliderId,
            int leafId,
            int volumeOrdinal,
            CombatShapeSeparationStatus separationStatus,
            long normalXFix64Raw,
            long normalYFix64Raw,
            long normalZFix64Raw,
            long penetrationFix64Raw,
            long contactPointXFix64Raw,
            long contactPointYFix64Raw,
            long contactPointZFix64Raw,
            int horizontalNormalXScaled,
            int horizontalNormalZScaled,
            bool surfaceMotionEnabled,
            int surfaceVelocityXMillimetersPerSecond,
            int surfaceVelocityZMillimetersPerSecond,
            int penetrationBiasMillimetersPerSecond,
            int pressureScaleMilli,
            int maximumComposedSpeedMillimetersPerSecond)
        {
            PresenceEntityId = presenceEntityId;
            TargetEntityId = targetEntityId;
            PresenceBodyId = presenceBodyId;
            ColliderId = colliderId;
            LeafId = leafId;
            VolumeOrdinal = volumeOrdinal;
            SeparationStatus = separationStatus;
            NormalXFix64Raw = normalXFix64Raw;
            NormalYFix64Raw = normalYFix64Raw;
            NormalZFix64Raw = normalZFix64Raw;
            PenetrationFix64Raw = penetrationFix64Raw;
            ContactPointXFix64Raw = contactPointXFix64Raw;
            ContactPointYFix64Raw = contactPointYFix64Raw;
            ContactPointZFix64Raw = contactPointZFix64Raw;
            HorizontalNormalXScaled = horizontalNormalXScaled;
            HorizontalNormalZScaled = horizontalNormalZScaled;
            SurfaceMotionEnabled = surfaceMotionEnabled;
            SurfaceVelocityXMillimetersPerSecond = surfaceVelocityXMillimetersPerSecond;
            SurfaceVelocityZMillimetersPerSecond = surfaceVelocityZMillimetersPerSecond;
            PenetrationBiasMillimetersPerSecond = penetrationBiasMillimetersPerSecond;
            PressureScaleMilli = pressureScaleMilli;
            MaximumComposedSpeedMillimetersPerSecond = maximumComposedSpeedMillimetersPerSecond;
        }

        internal int PresenceEntityId { get; }
        internal int TargetEntityId { get; }
        internal int PresenceBodyId { get; }
        internal int ColliderId { get; }
        internal int LeafId { get; }
        internal int VolumeOrdinal { get; }
        internal CombatShapeSeparationStatus SeparationStatus { get; }
        internal long NormalXFix64Raw { get; }
        internal long NormalYFix64Raw { get; }
        internal long NormalZFix64Raw { get; }
        internal long PenetrationFix64Raw { get; }
        internal long ContactPointXFix64Raw { get; }
        internal long ContactPointYFix64Raw { get; }
        internal long ContactPointZFix64Raw { get; }
        internal int HorizontalNormalXScaled { get; }
        internal int HorizontalNormalZScaled { get; }
        internal bool SurfaceMotionEnabled { get; }
        internal int SurfaceVelocityXMillimetersPerSecond { get; }
        internal int SurfaceVelocityZMillimetersPerSecond { get; }
        internal int PenetrationBiasMillimetersPerSecond { get; }
        internal int PressureScaleMilli { get; }
        internal int MaximumComposedSpeedMillimetersPerSecond { get; }
    }

    internal sealed partial class CharacterTestCharacterContactResolver
    {
        private const int ContactSolverIterations = 2;
        private const int ContactPressureIterations = 3;
        private const int ContactEpsilonMillimeters = 1;
        private const int ContactPressureSlopMillimeters = 50;
        private const int ContactVelocityEpsilonMillimetersPerSecond = 1;
        private const int ContactBufferInitialCapacity = 64;
        private const int ContactGridCellSizeMillimeters = 1000;
        private const int BodyPresenceRawCandidateCapacity = 28;
        private const int BodyPresencePairCapacity = 28;
        private const int BodyPresenceDirectedConstraintCapacity = 224;
        private const long BodyPresencePiFix64Raw = 3141593L;

        private readonly ICharacterTestCharacterColliderSync _colliderSync;
        private readonly Dictionary<int, RawVector2> _contactVelocities =
            new Dictionary<int, RawVector2>(ContactBufferInitialCapacity);
        private readonly Dictionary<int, RawVector2> _contactDisplacements =
            new Dictionary<int, RawVector2>(ContactBufferInitialCapacity);
        private readonly Dictionary<int, int> _contactCounts =
            new Dictionary<int, int>(ContactBufferInitialCapacity);
        private readonly Dictionary<int, RawVector2> _attemptedContactVelocities =
            new Dictionary<int, RawVector2>(ContactBufferInitialCapacity);
        private readonly Dictionary<int, RawVector2> _pressureVelocities =
            new Dictionary<int, RawVector2>(ContactBufferInitialCapacity);
        private readonly Dictionary<int, string> _characterContactStableIds =
            new Dictionary<int, string>(ContactBufferInitialCapacity);
        private readonly Dictionary<int, CharacterTestCharacterEntity> _dynamicCharactersByBodyId =
            new Dictionary<int, CharacterTestCharacterEntity>(ContactBufferInitialCapacity);
        private readonly Dictionary<int, CharacterTestCharacterEntity> _currentContactStateEntitiesById =
            new Dictionary<int, CharacterTestCharacterEntity>(ContactBufferInitialCapacity);
        private readonly List<CharacterTestCharacterEntity> _currentContactStateEntities =
            new List<CharacterTestCharacterEntity>(ContactBufferInitialCapacity);
        private readonly List<CharacterTestCharacterEntity> _appliedContactStateEntities =
            new List<CharacterTestCharacterEntity>(ContactBufferInitialCapacity);
        private readonly List<ContactEntityInfo> _contactEntityInfos =
            new List<ContactEntityInfo>(ContactBufferInitialCapacity);
        private readonly Dictionary<ContactCell, ContactCellEntry> _contactCells =
            new Dictionary<ContactCell, ContactCellEntry>();
        private readonly List<ContactCellEntry> _activeContactCellEntries =
            new List<ContactCellEntry>();
        private readonly List<ContactCellEntry> _contactCellEntryPool =
            new List<ContactCellEntry>();
        private readonly List<int> _contactCandidateIndices =
            new List<int>(ContactBufferInitialCapacity);
        private readonly List<CombatShapeSeparationResult> _bodyPresenceRawCandidates =
            new List<CombatShapeSeparationResult>(BodyPresenceRawCandidateCapacity);
        private readonly CharacterTestCharacterEntity[] _bodyPresenceEntities =
            new CharacterTestCharacterEntity[CharacterTestCharacterPhysicsRuntime.BodyPresenceMaximumActiveCharacters];
        private readonly BodyPresencePairKey[] _bodyPresencePairs =
            new BodyPresencePairKey[BodyPresencePairCapacity];
        private readonly BodyPresenceDirectedConstraint[] _bodyPresenceConstraints =
            new BodyPresenceDirectedConstraint[BodyPresenceDirectedConstraintCapacity];
        private IReadOnlyList<CharacterTestCharacterEntity> _contactEntityInfoSource;
        private bool _dynamicCharacterLookupPrepared;
        private int _contactGridMaxRadiusMillimeters;
        private int _lastPressureCandidatePairCount;
        private int _lastPressureQueryCellCount;
        private int _lastPressureSkippedCellCount;
        private int _lastOverlapCandidatePairCount;
        private int _lastOverlapSkippedCellCount;
        private int _lastContactStateApplyEntityCount;
        private bool _contactStateApplyInitialized;
        private int _bodyPresenceEntityCount;
        private int _bodyPresencePairCount;
        private int _bodyPresenceConstraintCount;
        private int _lastBodyPresenceRawCandidateCount;
        private int _lastBodyPresencePairCount;
        private int _lastBodyPresenceConstraintCount;
        private CharacterTestBodyPresenceContactBuildStatus _lastBodyPresenceContactBuildStatus;
        private int _lastBodyPresenceSurfaceClampCount;

        public CharacterTestCharacterContactResolver(ICharacterTestCharacterColliderSync colliderSync)
        {
            _colliderSync = colliderSync;
        }

        internal int LastPressureCandidatePairCount => _lastPressureCandidatePairCount;

        internal int LastPressureQueryCellCount => _lastPressureQueryCellCount;

        internal int LastPressureSkippedCellCount => _lastPressureSkippedCellCount;

        internal int LastOverlapCandidatePairCount => _lastOverlapCandidatePairCount;

        internal int LastOverlapSkippedCellCount => _lastOverlapSkippedCellCount;

        internal int LastContactStateApplyEntityCount => _lastContactStateApplyEntityCount;

        internal int LastBodyPresenceRawCandidateCount => _lastBodyPresenceRawCandidateCount;

        internal int LastBodyPresencePairCount => _lastBodyPresencePairCount;

        internal int LastBodyPresenceConstraintCount => _lastBodyPresenceConstraintCount;

        internal CharacterTestBodyPresenceContactBuildStatus LastBodyPresenceContactBuildStatus =>
            _lastBodyPresenceContactBuildStatus;

        internal int LastBodyPresenceSurfaceClampCount => _lastBodyPresenceSurfaceClampCount;

        /// <remarks>NoAlloc. Projects counters from the existing fixed Presence buffers.</remarks>
        internal CharacterTestBodyPresenceContactDiagnostics CaptureBodyPresenceContactDiagnostics()
        {
            return new CharacterTestBodyPresenceContactDiagnostics(
                _lastBodyPresenceContactBuildStatus,
                _lastBodyPresenceRawCandidateCount,
                BodyPresenceRawCandidateCapacity,
                _lastBodyPresencePairCount,
                BodyPresencePairCapacity,
                _lastBodyPresenceConstraintCount,
                BodyPresenceDirectedConstraintCapacity,
                ContactPressureIterations,
                _lastBodyPresenceSurfaceClampCount);
        }

        /// <remarks>NoAlloc. Reads the existing stable-sorted constraint buffer without copying it.</remarks>
        internal bool TryGetBodyPresenceConstraintDiagnostic(
            int index,
            out CharacterTestBodyPresenceConstraintDiagnostic diagnostic)
        {
            if ((uint)index >= (uint)_bodyPresenceConstraintCount)
            {
                diagnostic = default;
                return false;
            }

            BodyPresenceDirectedConstraint constraint = _bodyPresenceConstraints[index];
            CombatShapeSeparationResult separation = constraint.Separation;
            BodyPresenceConstraintMotion surfaceMotion = constraint.SurfaceMotion;
            diagnostic = new CharacterTestBodyPresenceConstraintDiagnostic(
                constraint.PresenceOwner.Id.Value,
                constraint.Target.Id.Value,
                constraint.DirectedKey.PresenceBodyId,
                constraint.DirectedKey.ColliderId,
                constraint.DirectedKey.LeafId,
                constraint.DirectedKey.ColliderId
                    - CharacterTestCharacterPhysicsRuntime.BodyPresenceColliderIdStart,
                separation.Status,
                separation.Normal.X.RawValue,
                separation.Normal.Y.RawValue,
                separation.Normal.Z.RawValue,
                separation.Penetration.RawValue,
                separation.ContactPoint.X.RawValue,
                separation.ContactPoint.Y.RawValue,
                separation.ContactPoint.Z.RawValue,
                constraint.HorizontalNormal.X,
                constraint.HorizontalNormal.Y,
                surfaceMotion.Enabled,
                surfaceMotion.SurfaceVelocity.X,
                surfaceMotion.SurfaceVelocity.Y,
                surfaceMotion.PenetrationBiasMillimetersPerSecond,
                surfaceMotion.PressureScaleMilli,
                surfaceMotion.MaximumComposedSpeedMillimetersPerSecond);
            return true;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void Clear()
        {
            _contactVelocities.Clear();
            _contactDisplacements.Clear();
            _contactCounts.Clear();
            _attemptedContactVelocities.Clear();
            _pressureVelocities.Clear();
            _dynamicCharactersByBodyId.Clear();
            _currentContactStateEntitiesById.Clear();
            _currentContactStateEntities.Clear();
            _contactEntityInfos.Clear();
            _contactEntityInfoSource = null;
            ReleaseContactCells();
            _contactCandidateIndices.Clear();
            ResetBodyPresenceContactBuild(CharacterTestBodyPresenceContactBuildStatus.NotRequested);
            ResetAnimatedAppendageSoftContactBuild();
            _dynamicCharacterLookupPrepared = false;
            _lastOverlapCandidatePairCount = 0;
            _lastOverlapSkippedCellCount = 0;
            _lastContactStateApplyEntityCount = 0;
            ResetPressureCandidateStats();
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void PrepareDynamicCharacterLookup(IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            _dynamicCharactersByBodyId.Clear();
            _dynamicCharacterLookupPrepared = true;
            if (entities == null || entities.Count == 0)
                return;

            _dynamicCharactersByBodyId.EnsureCapacity(entities.Count);
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null)
                    continue;

                int bodyId = CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value;
                _dynamicCharactersByBodyId[bodyId] = entity;
            }
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void RecordAttemptedContactVelocity(int entityId, int velocityX, int velocityY)
        {
            if (entityId <= 0)
                return;

            var velocity = new RawVector2(velocityX, velocityY);
            if (velocity.SqrMagnitude <= ContactVelocityEpsilonMillimetersPerSecond * ContactVelocityEpsilonMillimetersPerSecond)
            {
                _attemptedContactVelocities.Remove(entityId);
                return;
            }

            _attemptedContactVelocities[entityId] = velocity;
        }

        /// <remarks>NoAlloc after initialization; unseen movable-contact character ids may allocate one cached stable id.</remarks>
        public void RecordDynamicContactHit(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterRuntimeWorld world,
            CombatMotionStepResult physicsStep,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            int attemptedVelocityX,
            int attemptedVelocityY)
        {
            var attemptedHorizontalVelocity = new RawVector2(attemptedVelocityX, attemptedVelocityY);
            if (source == null
                || world == null
                || physicsRuntime == null
                || physicsStep.CollisionCount == 0
                || attemptedHorizontalVelocity.SqrMagnitude <= ContactVelocityEpsilonMillimetersPerSecond * ContactVelocityEpsilonMillimetersPerSecond)
            {
                return;
            }

            for (int i = 0; i < physicsStep.CollisionCount; i++)
            {
                CombatMotionCollision collision = physicsStep.Collisions[i];
                CharacterTestCharacterEntity target = _dynamicCharacterLookupPrepared
                    ? FindPreparedDynamicCharacterByBodyId(collision.TargetBodyId)
                    : FindDynamicCharacterByBodyId(
                        world.Entities,
                        collision.TargetBodyId);
                if (target != null && target.Id.Value != source.Id.Value)
                {
                    AccumulateContactCount(source);
                    AccumulateContactCount(target);
                    continue;
                }

                if (!physicsRuntime.TryResolveMovableObjectNumericId(
                        collision.TargetBodyId,
                        out int movableNumericId)
                    || !world.TryFindMovableNaturalObject(
                        movableNumericId,
                        out CharacterTestMovableNaturalObjectState movable))
                {
                    continue;
                }

                AccumulateContactCount(source);
                movable.AccumulateContactPush(
                    source.Id.Value,
                    source.Physics.EffectiveMassGrams,
                    source.Physics.EffectivePushScaleMilli,
                    attemptedHorizontalVelocity.X,
                    attemptedHorizontalVelocity.Y);
            }
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void ResolveCharacterContactPressure(IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            ResolveCharacterContactPressure(
                entities,
                physicsRuntime: null,
                bodyPresenceReady: false,
                requestBodyPresence: false,
                requestSurfaceMotion: false,
                bodyPresenceMotionSamples: null,
                bodyPresenceMotionSampleCount: 0,
                bodyPresenceMotionReady: false,
                runSpeedMillimetersPerSecond: 0,
                accelerationMillimetersPerSecondSquared: 0,
                turnSpeedMilliDegreesPerSecond: 0,
                tickRate: 0);
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        internal void ResolveCharacterContactPressure(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            bool bodyPresenceReady)
        {
            ResolveCharacterContactPressure(
                entities,
                physicsRuntime,
                bodyPresenceReady,
                requestBodyPresence: true,
                requestSurfaceMotion: false,
                bodyPresenceMotionSamples: null,
                bodyPresenceMotionSampleCount: 0,
                bodyPresenceMotionReady: false,
                runSpeedMillimetersPerSecond: 0,
                accelerationMillimetersPerSecondSquared: 0,
                turnSpeedMilliDegreesPerSecond: 0,
                tickRate: 0);
        }

        /// <remarks>NoAlloc after package and collection warmup.</remarks>
        internal void ResolveCharacterContactPressure(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            bool bodyPresenceReady,
            CharacterTestBodyPresenceMotionSample[] bodyPresenceMotionSamples,
            int bodyPresenceMotionSampleCount,
            bool bodyPresenceMotionReady,
            int runSpeedMillimetersPerSecond,
            int accelerationMillimetersPerSecondSquared,
            int turnSpeedMilliDegreesPerSecond,
            int tickRate)
        {
            ResolveCharacterContactPressure(
                entities,
                physicsRuntime,
                bodyPresenceReady,
                requestBodyPresence: true,
                requestSurfaceMotion: true,
                bodyPresenceMotionSamples: bodyPresenceMotionSamples,
                bodyPresenceMotionSampleCount: bodyPresenceMotionSampleCount,
                bodyPresenceMotionReady: bodyPresenceMotionReady,
                runSpeedMillimetersPerSecond: runSpeedMillimetersPerSecond,
                accelerationMillimetersPerSecondSquared: accelerationMillimetersPerSecondSquared,
                turnSpeedMilliDegreesPerSecond: turnSpeedMilliDegreesPerSecond,
                tickRate: tickRate);
        }

        private void ResolveCharacterContactPressure(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            bool bodyPresenceReady,
            bool requestBodyPresence,
            bool requestSurfaceMotion,
            CharacterTestBodyPresenceMotionSample[] bodyPresenceMotionSamples,
            int bodyPresenceMotionSampleCount,
            bool bodyPresenceMotionReady,
            int runSpeedMillimetersPerSecond,
            int accelerationMillimetersPerSecondSquared,
            int turnSpeedMilliDegreesPerSecond,
            int tickRate)
        {
            ResetPressureCandidateStats();
            bool hasBodyPresenceConstraints = TryPrepareBodyPresenceConstraints(
                entities,
                physicsRuntime,
                bodyPresenceReady,
                requestBodyPresence,
                requestSurfaceMotion,
                bodyPresenceMotionSamples,
                bodyPresenceMotionSampleCount,
                bodyPresenceMotionReady,
                runSpeedMillimetersPerSecond,
                accelerationMillimetersPerSecondSquared,
                turnSpeedMilliDegreesPerSecond,
                tickRate);

            if (entities == null || entities.Count == 0)
                return;

            _pressureVelocities.Clear();
            foreach (KeyValuePair<int, RawVector2> attempted in _attemptedContactVelocities)
                _pressureVelocities[attempted.Key] = attempted.Value;

            if (hasBodyPresenceConstraints)
                AccumulateBodyPresenceContactObservations();

            if (_pressureVelocities.Count == 0 && !hasBodyPresenceConstraints)
                return;

            EnsureContactEntityInfos(entities);
            BuildContactCandidateGrid();
            bool hasRootContactCandidates = _activeContactCellEntries.Count > 0
                && _contactEntityInfos.Count > 1;
            if (!hasRootContactCandidates && !hasBodyPresenceConstraints)
                return;

            for (int iteration = 0; iteration < ContactPressureIterations; iteration++)
            {
                bool changedAny = false;
                if (hasRootContactCandidates)
                {
                    for (int i = 0; i < _contactEntityInfos.Count; i++)
                    {
                        ContactEntityInfo aInfo = _contactEntityInfos[i];
                        CharacterTestCharacterEntity a = aInfo.Entity;

                        QueryContactCandidateIndices(aInfo, i);
                        for (int candidateIndex = 0; candidateIndex < _contactCandidateIndices.Count; candidateIndex++)
                        {
                            int j = _contactCandidateIndices[candidateIndex];
                            ContactEntityInfo bInfo = _contactEntityInfos[j];
                            if (!VerticalRangesOverlap(aInfo, bInfo))
                                continue;

                            CharacterTestCharacterEntity b = bInfo.Entity;
                            int minDistance = aInfo.RadiusMillimeters + bInfo.RadiusMillimeters;
                            int deltaX = b.Transform.XMillimeters - a.Transform.XMillimeters;
                            int deltaY = b.Transform.ZMillimeters - a.Transform.ZMillimeters;
                            long distanceSq = (long)deltaX * deltaX + (long)deltaY * deltaY;
                            int pressureRange = minDistance + ContactPressureSlopMillimeters;
                            if (distanceSq > (long)pressureRange * pressureRange)
                                continue;

                            RawVector2 normal = ResolveContactNormal(a, b, deltaX, deltaY, distanceSq);
                            AccumulateContactCount(a);
                            AccumulateContactCount(b);
                            if (_pressureVelocities.TryGetValue(a.Id.Value, out RawVector2 pressureVelocityA))
                                changedAny |= ApplyContactPressure(a, b, normal, pressureVelocityA);
                            if (_pressureVelocities.TryGetValue(b.Id.Value, out RawVector2 pressureVelocityB))
                                changedAny |= ApplyContactPressure(b, a, -normal, pressureVelocityB);
                        }
                    }
                }

                if (hasBodyPresenceConstraints)
                    changedAny |= ApplyBodyPresenceContactPressure();

                if (!changedAny)
                    break;
            }
        }

        private bool TryPrepareBodyPresenceConstraints(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            bool bodyPresenceReady,
            bool requestBodyPresence,
            bool requestSurfaceMotion,
            CharacterTestBodyPresenceMotionSample[] bodyPresenceMotionSamples,
            int bodyPresenceMotionSampleCount,
            bool bodyPresenceMotionReady,
            int runSpeedMillimetersPerSecond,
            int accelerationMillimetersPerSecondSquared,
            int turnSpeedMilliDegreesPerSecond,
            int tickRate)
        {
            ResetBodyPresenceContactBuild(
                requestBodyPresence
                    ? CharacterTestBodyPresenceContactBuildStatus.SourceUnavailable
                    : CharacterTestBodyPresenceContactBuildStatus.NotRequested);
            if (!requestBodyPresence || !bodyPresenceReady || physicsRuntime?.World == null)
                return false;

            if (entities == null || entities.Count == 0)
            {
                _lastBodyPresenceContactBuildStatus = CharacterTestBodyPresenceContactBuildStatus.Success;
                return false;
            }

            if (entities.Count > CharacterTestCharacterPhysicsRuntime.BodyPresenceMaximumActiveCharacters)
                return RejectBodyPresenceContactBuild(CharacterTestBodyPresenceContactBuildStatus.CapacityExceeded);

            BodyPresenceMotionLimits motionLimits = default;
            if (requestSurfaceMotion
                && (!bodyPresenceMotionReady
                    || !TryValidateBodyPresenceMotionSamples(
                        entities,
                        bodyPresenceMotionSamples,
                        bodyPresenceMotionSampleCount)
                    || !BodyPresenceMotionLimits.TryCreate(
                        runSpeedMillimetersPerSecond,
                        accelerationMillimetersPerSecondSquared,
                        turnSpeedMilliDegreesPerSecond,
                        tickRate,
                        out motionLimits)))
            {
                return RejectBodyPresenceContactBuild(
                    CharacterTestBodyPresenceContactBuildStatus.InvalidMotionWitness);
            }

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (!IsValidBodyPresenceContactEntity(entity)
                    || ContainsBodyPresenceEntity(entity.Id.Value))
                {
                    continue;
                }

                if (_bodyPresenceEntityCount >= _bodyPresenceEntities.Length)
                    return RejectBodyPresenceContactBuild(CharacterTestBodyPresenceContactBuildStatus.CapacityExceeded);

                int insertIndex = _bodyPresenceEntityCount;
                while (insertIndex > 0
                    && _bodyPresenceEntities[insertIndex - 1].Id.Value > entity.Id.Value)
                {
                    _bodyPresenceEntities[insertIndex] = _bodyPresenceEntities[insertIndex - 1];
                    insertIndex--;
                }

                _bodyPresenceEntities[insertIndex] = entity;
                _bodyPresenceEntityCount++;
            }

            for (int targetIndex = 0; targetIndex < _bodyPresenceEntityCount; targetIndex++)
            {
                CharacterTestCharacterEntity target = _bodyPresenceEntities[targetIndex];
                CombatCapsuleQuery query = CreateBodyPresenceContactQuery(
                    target,
                    physicsRuntime,
                    targetIndex);
                _bodyPresenceRawCandidates.Clear();
                physicsRuntime.World.QueryCapsuleSeparations(
                    query,
                    _bodyPresenceRawCandidates,
                    includeSourceEntity: false);

                if (_bodyPresenceRawCandidates.Count > _lastBodyPresenceRawCandidateCount)
                    _lastBodyPresenceRawCandidateCount = _bodyPresenceRawCandidates.Count;
                if (_bodyPresenceRawCandidates.Count > BodyPresenceRawCandidateCapacity)
                    return RejectBodyPresenceContactBuild(CharacterTestBodyPresenceContactBuildStatus.CapacityExceeded);

                for (int candidateIndex = 0;
                    candidateIndex < _bodyPresenceRawCandidates.Count;
                    candidateIndex++)
                {
                    CombatShapeSeparationResult result = _bodyPresenceRawCandidates[candidateIndex];
                    if (result.Status == CombatShapeSeparationStatus.InvalidShape)
                        return RejectBodyPresenceContactBuild(CharacterTestBodyPresenceContactBuildStatus.InvalidSeparation);
                    if (result.Status == CombatShapeSeparationStatus.UnsupportedShape)
                        return RejectBodyPresenceContactBuild(CharacterTestBodyPresenceContactBuildStatus.UnsupportedSeparation);

                    if (result.TargetEntityId.Equals(physicsRuntime.GetEntityId(target)))
                        continue;

                    CharacterTestCharacterEntity rosterEntity = FindRosterEntity(
                        entities,
                        result.TargetEntityId,
                        physicsRuntime);
                    if (rosterEntity != null && !rosterEntity.BattleState.CountsAsAlive)
                        continue;

                    CharacterTestCharacterEntity presenceOwner = FindBodyPresenceEntity(
                        result.TargetEntityId,
                        physicsRuntime);
                    if (presenceOwner == null
                        || !IsValidBodyPresenceResultIdentity(result, presenceOwner, physicsRuntime))
                    {
                        return RejectBodyPresenceContactBuild(
                            CharacterTestBodyPresenceContactBuildStatus.InvalidIdentity);
                    }

                    if (!result.HasContact)
                        continue;

                    BodyPresencePairKey pairKey = BodyPresencePairKey.Create(
                        presenceOwner.Id.Value,
                        target.Id.Value);
                    if (!TryRegisterBodyPresencePair(pairKey))
                        return RejectBodyPresenceContactBuild(CharacterTestBodyPresenceContactBuildStatus.CapacityExceeded);

                    BodyPresenceDirectedKey directedKey = new BodyPresenceDirectedKey(
                        result.TargetEntityId.Value,
                        result.TargetBodyId.Value,
                        result.TargetColliderId.Value,
                        result.TargetCompoundLeafId.Value,
                        physicsRuntime.GetEntityId(target).Value);
                    int duplicateIndex = FindBodyPresenceConstraint(directedKey);
                    if (duplicateIndex >= 0)
                    {
                        if (_bodyPresenceConstraints[duplicateIndex].Separation.Equals(result))
                            continue;

                        return RejectBodyPresenceContactBuild(
                            CharacterTestBodyPresenceContactBuildStatus.ConflictingDuplicate);
                    }

                    if (_bodyPresenceConstraintCount >= _bodyPresenceConstraints.Length)
                        return RejectBodyPresenceContactBuild(CharacterTestBodyPresenceContactBuildStatus.CapacityExceeded);

                    RawVector2 horizontalNormal = ResolveBodyPresenceHorizontalNormal(result.Normal);
                    if (requestSurfaceMotion
                        && horizontalNormal.SqrMagnitude
                            <= ContactEpsilonMillimeters * ContactEpsilonMillimeters)
                    {
                        return RejectBodyPresenceContactBuild(
                            CharacterTestBodyPresenceContactBuildStatus.DegenerateSurfaceMotion);
                    }

                    BodyPresenceConstraintMotion surfaceMotion = default;
                    if (requestSurfaceMotion
                        && !TryCreateBodyPresenceConstraintMotion(
                            result,
                            presenceOwner,
                            bodyPresenceMotionSamples,
                            bodyPresenceMotionSampleCount,
                            in motionLimits,
                            out surfaceMotion,
                            out CharacterTestBodyPresenceContactBuildStatus failureStatus))
                    {
                        return RejectBodyPresenceContactBuild(failureStatus);
                    }

                    _bodyPresenceConstraints[_bodyPresenceConstraintCount++] =
                        new BodyPresenceDirectedConstraint(
                            pairKey,
                            directedKey,
                            presenceOwner,
                            target,
                            result,
                            horizontalNormal,
                            surfaceMotion);
                }
            }

            SortBodyPresencePairs();
            SortBodyPresenceConstraints();
            _lastBodyPresencePairCount = _bodyPresencePairCount;
            _lastBodyPresenceConstraintCount = _bodyPresenceConstraintCount;
            _lastBodyPresenceContactBuildStatus = CharacterTestBodyPresenceContactBuildStatus.Success;
            return _bodyPresenceConstraintCount > 0;
        }

        private static bool TryValidateBodyPresenceMotionSamples(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestBodyPresenceMotionSample[] samples,
            int sampleCount)
        {
            if (entities == null
                || samples == null
                || sampleCount != entities.Count
                || sampleCount < 0
                || sampleCount > samples.Length
                || sampleCount > CharacterTestCharacterPhysicsRuntime.BodyPresenceMaximumActiveCharacters)
            {
                return false;
            }

            for (int i = 0; i < sampleCount; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null || entity.Id.Value <= 0)
                    return false;

                int matchCount = 0;
                for (int j = 0; j < sampleCount; j++)
                {
                    if (samples[j].IsComplete && samples[j].EntityId == entity.Id.Value)
                        matchCount++;
                }

                if (matchCount != 1)
                    return false;
            }

            return true;
        }

        private bool TryCreateBodyPresenceConstraintMotion(
            CombatShapeSeparationResult result,
            CharacterTestCharacterEntity presenceOwner,
            CharacterTestBodyPresenceMotionSample[] samples,
            int sampleCount,
            in BodyPresenceMotionLimits limits,
            out BodyPresenceConstraintMotion motion,
            out CharacterTestBodyPresenceContactBuildStatus failureStatus)
        {
            motion = default;
            failureStatus = CharacterTestBodyPresenceContactBuildStatus.InvalidMotionWitness;
            if (!TryFindBodyPresenceMotionSample(
                    samples,
                    sampleCount,
                    presenceOwner.Id.Value,
                    out CharacterTestBodyPresenceMotionSample sample))
            {
                return false;
            }

            CharacterImportedGeometryBinding binding = sample.Binding;
            int ordinal = result.TargetColliderId.Value
                - CharacterTestCharacterPhysicsRuntime.BodyPresenceColliderIdStart;
            if (binding == null
                || binding.BodyPresenceMode != CharacterBodyPresenceMode.Authored
                || binding.BodyPresenceRevision <= 0
                || binding.BodyPresenceVolumes == null
                || ordinal < 0
                || ordinal >= binding.BodyPresenceVolumes.Length)
            {
                return false;
            }

            CharacterBodyPresenceRuntimeBinding volume = binding.BodyPresenceVolumes[ordinal];
            if (volume == null
                || !volume.Enabled
                || volume.PressureScaleMilli < 0
                || volume.PressureScaleMilli > 100000)
            {
                failureStatus = CharacterTestBodyPresenceContactBuildStatus.InvalidPressureScale;
                return false;
            }

            try
            {
                bool clamped = false;
                long linearX = checked(
                    ((long)sample.CurrentXMillimeters - sample.PreviousXMillimeters)
                    * limits.TickRate);
                long linearZ = checked(
                    ((long)sample.CurrentZMillimeters - sample.PreviousZMillimeters)
                    * limits.TickRate);
                int surfaceX = ClampLongToIntRange(
                    linearX,
                    -limits.MaximumComposedSpeedMillimetersPerSecond,
                    limits.MaximumComposedSpeedMillimetersPerSecond,
                    ref clamped);
                int surfaceZ = ClampLongToIntRange(
                    linearZ,
                    -limits.MaximumComposedSpeedMillimetersPerSecond,
                    limits.MaximumComposedSpeedMillimetersPerSecond,
                    ref clamped);

                if (sample.RotationWitnessValid && limits.TurnSpeedMilliDegreesPerSecond > 0)
                {
                    int yawDelta = CharacterTestDeterministicMath.ShortestAngleDelta(
                        sample.PreviousYawMilliDegrees,
                        sample.CurrentYawMilliDegrees);
                    long yawSpeed = checked((long)yawDelta * limits.TickRate);
                    if (yawSpeed > limits.TurnSpeedMilliDegreesPerSecond)
                    {
                        yawSpeed = limits.TurnSpeedMilliDegreesPerSecond;
                        clamped = true;
                    }
                    else if (yawSpeed < -limits.TurnSpeedMilliDegreesPerSecond)
                    {
                        yawSpeed = -limits.TurnSpeedMilliDegreesPerSecond;
                        clamped = true;
                    }

                    int contactXMillimeters = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                        result.ContactPoint.X);
                    int contactZMillimeters = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                        result.ContactPoint.Z);
                    long offsetXMillimeters = (long)contactXMillimeters - sample.CurrentXMillimeters;
                    long offsetZMillimeters = (long)contactZMillimeters - sample.CurrentZMillimeters;
                    long omegaFix64Raw = checked(yawSpeed * BodyPresencePiFix64Raw) / 180000L;
                    long tangentialX = checked(omegaFix64Raw * offsetZMillimeters) / Fix64.Scale;
                    long tangentialZ = checked(-omegaFix64Raw * offsetXMillimeters) / Fix64.Scale;
                    surfaceX = ClampLongToIntRange(
                        checked((long)surfaceX + tangentialX),
                        -limits.MaximumComposedSpeedMillimetersPerSecond,
                        limits.MaximumComposedSpeedMillimetersPerSecond,
                        ref clamped);
                    surfaceZ = ClampLongToIntRange(
                        checked((long)surfaceZ + tangentialZ),
                        -limits.MaximumComposedSpeedMillimetersPerSecond,
                        limits.MaximumComposedSpeedMillimetersPerSecond,
                        ref clamped);
                }

                RawVector2 surfaceVelocity = ClampMagnitude(
                    new RawVector2(surfaceX, surfaceZ),
                    limits.MaximumComposedSpeedMillimetersPerSecond,
                    ref clamped);
                int penetrationMillimeters = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                    result.Penetration);
                if (penetrationMillimeters < 0)
                {
                    failureStatus = CharacterTestBodyPresenceContactBuildStatus.InvalidSeparation;
                    return false;
                }

                long requestedBias = checked((long)penetrationMillimeters * limits.TickRate);
                int penetrationBias = ClampLongToIntRange(
                    requestedBias,
                    0,
                    limits.AccelerationStepMillimetersPerSecond,
                    ref clamped);
                if (clamped)
                    _lastBodyPresenceSurfaceClampCount++;
                motion = new BodyPresenceConstraintMotion(
                    surfaceVelocity,
                    penetrationBias,
                    volume.PressureScaleMilli,
                    limits.MaximumComposedSpeedMillimetersPerSecond);
                failureStatus = CharacterTestBodyPresenceContactBuildStatus.Success;
                return true;
            }
            catch (System.OverflowException)
            {
                failureStatus = CharacterTestBodyPresenceContactBuildStatus.ArithmeticOverflow;
                return false;
            }
        }

        private static bool TryFindBodyPresenceMotionSample(
            CharacterTestBodyPresenceMotionSample[] samples,
            int sampleCount,
            int entityId,
            out CharacterTestBodyPresenceMotionSample sample)
        {
            if (samples != null)
            {
                int safeCount = System.Math.Min(sampleCount, samples.Length);
                for (int i = 0; i < safeCount; i++)
                {
                    if (samples[i].IsComplete && samples[i].EntityId == entityId)
                    {
                        sample = samples[i];
                        return true;
                    }
                }
            }

            sample = default;
            return false;
        }

        private static int ClampLongToIntRange(
            long value,
            int minimum,
            int maximum,
            ref bool clamped)
        {
            if (value < minimum)
            {
                clamped = true;
                return minimum;
            }

            if (value > maximum)
            {
                clamped = true;
                return maximum;
            }

            return checked((int)value);
        }

        private static RawVector2 ClampMagnitude(
            RawVector2 value,
            int maximumMagnitude,
            ref bool clamped)
        {
            if (maximumMagnitude <= 0)
            {
                if (value.X != 0 || value.Y != 0)
                    clamped = true;
                return RawVector2.Zero;
            }

            int magnitude = CharacterTestDeterministicMath.Magnitude(value.X, value.Y);
            if (magnitude <= maximumMagnitude)
                return value;

            clamped = true;
            return new RawVector2(
                CharacterTestDeterministicMath.MulDivRoundToInt(
                    value.X,
                    maximumMagnitude,
                    magnitude),
                CharacterTestDeterministicMath.MulDivRoundToInt(
                    value.Y,
                    maximumMagnitude,
                    magnitude));
        }

        private void ResetBodyPresenceContactBuild(CharacterTestBodyPresenceContactBuildStatus status)
        {
            for (int i = 0; i < _bodyPresenceEntityCount; i++)
                _bodyPresenceEntities[i] = null;

            _bodyPresenceRawCandidates.Clear();
            _bodyPresenceEntityCount = 0;
            _bodyPresencePairCount = 0;
            _bodyPresenceConstraintCount = 0;
            _lastBodyPresenceRawCandidateCount = 0;
            _lastBodyPresencePairCount = 0;
            _lastBodyPresenceConstraintCount = 0;
            _lastBodyPresenceSurfaceClampCount = 0;
            _lastBodyPresenceContactBuildStatus = status;
        }

        private bool RejectBodyPresenceContactBuild(CharacterTestBodyPresenceContactBuildStatus status)
        {
            _lastBodyPresencePairCount = _bodyPresencePairCount;
            _lastBodyPresenceConstraintCount = _bodyPresenceConstraintCount;
            _bodyPresencePairCount = 0;
            _bodyPresenceConstraintCount = 0;
            _lastBodyPresenceContactBuildStatus = status;
            return false;
        }

        private bool ContainsBodyPresenceEntity(int entityId)
        {
            for (int i = 0; i < _bodyPresenceEntityCount; i++)
            {
                if (_bodyPresenceEntities[i].Id.Value == entityId)
                    return true;
            }

            return false;
        }

        private CharacterTestCharacterEntity FindBodyPresenceEntity(
            CombatEntityId entityId,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            for (int i = 0; i < _bodyPresenceEntityCount; i++)
            {
                CharacterTestCharacterEntity entity = _bodyPresenceEntities[i];
                if (physicsRuntime.GetEntityId(entity).Equals(entityId))
                    return entity;
            }

            return null;
        }

        private static CharacterTestCharacterEntity FindRosterEntity(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CombatEntityId entityId,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            if (entities == null)
                return null;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null && physicsRuntime.GetEntityId(entity).Equals(entityId))
                    return entity;
            }

            return null;
        }

        private static bool IsValidBodyPresenceContactEntity(CharacterTestCharacterEntity entity)
        {
            if (entity == null
                || !entity.BattleState.CountsAsAlive
                || !entity.ColliderPosture.BlocksCharacters
                || entity.Id.Value <= 0
                || entity.Id.Value > CharacterTestCharacterPhysicsRuntime.BodyPresenceMaximumEntityId)
            {
                return false;
            }

            CharacterTestColliderPostureComponent posture = entity.ColliderPosture;
            return posture.RadiusMillimeters > 0 && posture.HeightMillimeters > 0;
        }

        private static CombatCapsuleQuery CreateBodyPresenceContactQuery(
            CharacterTestCharacterEntity target,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            int sourceOrder)
        {
            CharacterTestColliderPostureComponent posture = target.ColliderPosture;
            int radiusMillimeters = posture.RadiusMillimeters;
            int centerYMillimeters = target.Transform.YMillimeters
                + ResolveEffectiveCenterYMillimeters(posture);
            int halfSegmentMillimeters = System.Math.Max(
                0,
                (posture.HeightMillimeters / 2) - radiusMillimeters);
            Fix64 x = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                target.Transform.XMillimeters);
            Fix64 z = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                target.Transform.ZMillimeters);
            FixVector3 pointA = new FixVector3(
                x,
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                    centerYMillimeters - halfSegmentMillimeters),
                z);
            FixVector3 pointB = new FixVector3(
                x,
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                    centerYMillimeters + halfSegmentMillimeters),
                z);
            CombatQueryHeader header = new CombatQueryHeader(
                target.Id.Value,
                CombatQueryKind.Capsule,
                physicsRuntime.GetEntityId(target),
                traceId: 0,
                actionId: 0,
                sourceOrder,
                physicsRuntime.BodyPresenceLayerMask);
            return new CombatCapsuleQuery(
                header,
                pointA,
                pointB,
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(radiusMillimeters));
        }

        private static bool IsValidBodyPresenceResultIdentity(
            CombatShapeSeparationResult result,
            CharacterTestCharacterEntity presenceOwner,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            if (!physicsRuntime.TryGetBodyPresenceBodyId(presenceOwner, out CombatBodyId expectedBodyId)
                || !result.TargetBodyId.Equals(expectedBodyId)
                || !result.TargetColliderId.Equals(result.TargetCompoundLeafId))
            {
                return false;
            }

            int ordinal = result.TargetColliderId.Value
                - CharacterTestCharacterPhysicsRuntime.BodyPresenceColliderIdStart;
            if (ordinal < 0
                || ordinal >= CharacterTestCharacterPhysicsRuntime.BodyPresenceColliderCapacity)
            {
                return false;
            }

            return result.TargetShapeKind == CombatPhysicsShapeKind.Aabb
                || result.TargetShapeKind == CombatPhysicsShapeKind.Sphere
                || result.TargetShapeKind == CombatPhysicsShapeKind.Capsule
                || result.TargetShapeKind == CombatPhysicsShapeKind.Obb;
        }

        private bool TryRegisterBodyPresencePair(BodyPresencePairKey key)
        {
            for (int i = 0; i < _bodyPresencePairCount; i++)
            {
                if (_bodyPresencePairs[i].Equals(key))
                    return true;
            }

            if (_bodyPresencePairCount >= _bodyPresencePairs.Length)
                return false;

            _bodyPresencePairs[_bodyPresencePairCount++] = key;
            return true;
        }

        private int FindBodyPresenceConstraint(BodyPresenceDirectedKey key)
        {
            for (int i = 0; i < _bodyPresenceConstraintCount; i++)
            {
                if (_bodyPresenceConstraints[i].DirectedKey.Equals(key))
                    return i;
            }

            return -1;
        }

        private void SortBodyPresencePairs()
        {
            for (int i = 1; i < _bodyPresencePairCount; i++)
            {
                BodyPresencePairKey value = _bodyPresencePairs[i];
                int j = i - 1;
                while (j >= 0 && _bodyPresencePairs[j].CompareTo(value) > 0)
                {
                    _bodyPresencePairs[j + 1] = _bodyPresencePairs[j];
                    j--;
                }

                _bodyPresencePairs[j + 1] = value;
            }
        }

        private void SortBodyPresenceConstraints()
        {
            for (int i = 1; i < _bodyPresenceConstraintCount; i++)
            {
                BodyPresenceDirectedConstraint value = _bodyPresenceConstraints[i];
                int j = i - 1;
                while (j >= 0 && BodyPresenceDirectedConstraint.Compare(
                    _bodyPresenceConstraints[j],
                    value) > 0)
                {
                    _bodyPresenceConstraints[j + 1] = _bodyPresenceConstraints[j];
                    j--;
                }

                _bodyPresenceConstraints[j + 1] = value;
            }
        }

        private void AccumulateBodyPresenceContactObservations()
        {
            for (int i = 0; i < _bodyPresenceConstraintCount; i++)
            {
                BodyPresenceDirectedConstraint constraint = _bodyPresenceConstraints[i];
                AccumulateContactCount(constraint.PresenceOwner);
                AccumulateContactCount(constraint.Target);
            }
        }

        private bool ApplyBodyPresenceContactPressure()
        {
            bool changedAny = false;
            for (int i = 0; i < _bodyPresenceConstraintCount; i++)
            {
                BodyPresenceDirectedConstraint constraint = _bodyPresenceConstraints[i];
                if (constraint.SurfaceMotion.Enabled)
                {
                    changedAny |= ApplyBodyPresenceSurfacePressure(constraint);
                    if (constraint.SurfaceMotion.MaximumComposedSpeedMillimetersPerSecond > 0
                        && _pressureVelocities.TryGetValue(
                        constraint.Target.Id.Value,
                        out RawVector2 movingTargetVelocity))
                    {
                        changedAny |= ApplyContactPressure(
                            constraint.Target,
                            constraint.PresenceOwner,
                            -constraint.HorizontalNormal,
                            movingTargetVelocity,
                            constraint.SurfaceMotion.PressureScaleMilli,
                            constraint.SurfaceMotion.MaximumComposedSpeedMillimetersPerSecond);
                    }

                    continue;
                }

                if (_pressureVelocities.TryGetValue(
                    constraint.PresenceOwner.Id.Value,
                    out RawVector2 presenceOwnerVelocity))
                {
                    changedAny |= ApplyContactPressure(
                        constraint.PresenceOwner,
                        constraint.Target,
                        constraint.HorizontalNormal,
                        presenceOwnerVelocity);
                }

                if (_pressureVelocities.TryGetValue(
                    constraint.Target.Id.Value,
                    out RawVector2 targetVelocity))
                {
                    changedAny |= ApplyContactPressure(
                        constraint.Target,
                        constraint.PresenceOwner,
                        -constraint.HorizontalNormal,
                        targetVelocity);
                }
            }

            return changedAny;
        }

        private bool ApplyBodyPresenceSurfacePressure(
            BodyPresenceDirectedConstraint constraint)
        {
            RawVector2 normal = constraint.HorizontalNormal;
            if (constraint.SurfaceMotion.MaximumComposedSpeedMillimetersPerSecond <= 0
                || normal.SqrMagnitude <= ContactEpsilonMillimeters * ContactEpsilonMillimeters)
                return false;

            int sourceAlong = RawVector2.DotScaled(
                constraint.SurfaceMotion.SurfaceVelocity,
                normal);
            long drivingAlong = (long)sourceAlong
                + constraint.SurfaceMotion.PenetrationBiasMillimetersPerSecond;
            _pressureVelocities.TryGetValue(
                constraint.Target.Id.Value,
                out RawVector2 targetVelocity);
            int targetAlong = RawVector2.DotScaled(targetVelocity, normal);
            long closingSpeed = drivingAlong - targetAlong;
            if (closingSpeed <= ContactVelocityEpsilonMillimetersPerSecond
                || !constraint.Target.ColliderPosture.CanBePushed
                || !TryResolveContactTransferScale(
                    constraint.PresenceOwner,
                    constraint.Target,
                    out int massTransferMilli,
                    out int pushScaleMilli))
            {
                return false;
            }

            if (!TryScalePositive(closingSpeed, massTransferMilli, out long deltaAlong)
                || !TryScalePositive(deltaAlong, pushScaleMilli, out deltaAlong)
                || !TryScalePositive(
                    deltaAlong,
                    constraint.SurfaceMotion.PressureScaleMilli,
                    out deltaAlong))
            {
                return false;
            }

            if (deltaAlong > closingSpeed)
                deltaAlong = closingSpeed;
            if (deltaAlong <= ContactVelocityEpsilonMillimetersPerSecond
                || deltaAlong > int.MaxValue)
            {
                return false;
            }

            return ApplyBoundedPressureDelta(
                constraint.Target,
                targetVelocity,
                normal,
                (int)deltaAlong,
                constraint.SurfaceMotion.MaximumComposedSpeedMillimetersPerSecond);
        }

        private static RawVector2 ResolveBodyPresenceHorizontalNormal(FixVector3 normal)
        {
            int normalX = CharacterTestDeterministicMath.DivideRoundToInt(
                normal.X.RawValue * CharacterTestDeterministicMath.TrigScale,
                Fix64.Scale);
            int normalZ = CharacterTestDeterministicMath.DivideRoundToInt(
                normal.Z.RawValue * CharacterTestDeterministicMath.TrigScale,
                Fix64.Scale);
            CharacterTestDeterministicMath.NormalizeToScale(
                normalX,
                normalZ,
                CharacterTestDeterministicMath.TrigScale,
                out normalX,
                out normalZ);
            return new RawVector2(normalX, normalZ);
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void ResolveCharacterOverlaps(IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            _lastOverlapCandidatePairCount = 0;
            _lastOverlapSkippedCellCount = 0;
            if (entities == null || entities.Count <= 1)
                return;

            BuildContactEntityInfos(entities);
            if (_contactEntityInfos.Count <= 1)
                return;

            for (int iteration = 0; iteration < ContactSolverIterations; iteration++)
            {
                BuildContactCandidateGrid();
                bool movedAny = false;
                for (int i = 0; i < _contactEntityInfos.Count; i++)
                {
                    ContactEntityInfo aInfo = _contactEntityInfos[i];
                    CharacterTestCharacterEntity a = aInfo.Entity;

                    QueryContactCandidateIndices(
                        aInfo,
                        i,
                        querySlopMillimeters: 0,
                        ContactCandidateQueryKind.Overlap);
                    for (int candidateIndex = 0; candidateIndex < _contactCandidateIndices.Count; candidateIndex++)
                    {
                        int j = _contactCandidateIndices[candidateIndex];
                        ContactEntityInfo bInfo = _contactEntityInfos[j];
                        if (!VerticalRangesOverlap(aInfo, bInfo))
                            continue;

                        CharacterTestCharacterEntity b = bInfo.Entity;
                        int minDistance = aInfo.RadiusMillimeters + bInfo.RadiusMillimeters;
                        int deltaX = b.Transform.XMillimeters - a.Transform.XMillimeters;
                        int deltaY = b.Transform.ZMillimeters - a.Transform.ZMillimeters;
                        long distanceSq = (long)deltaX * deltaX + (long)deltaY * deltaY;
                        if (distanceSq >= (long)minDistance * minDistance)
                            continue;

                        RawVector2 normal = ResolveContactNormal(a, b, deltaX, deltaY, distanceSq);
                        int distance = CharacterTestDeterministicMath.Magnitude(deltaX, deltaY);
                        int overlap = minDistance - distance;
                        if (overlap <= ContactEpsilonMillimeters)
                            continue;

                        long weightA = aInfo.PushWeight;
                        long weightB = bInfo.PushWeight;
                        long totalWeight = weightA + weightB;
                        if (totalWeight <= 0)
                            continue;

                        int displacementA = CharacterTestDeterministicMath.DivideRoundToInt(overlap * weightA, totalWeight);
                        int displacementB = overlap - displacementA;
                        bool movedA = ApplyContactDisplacement(a, (-normal).MultiplyScaled(displacementA));
                        bool movedB = ApplyContactDisplacement(b, normal.MultiplyScaled(displacementB));
                        movedAny |= movedA || movedB;
                        AccumulateContactCount(a);
                        AccumulateContactCount(b);
                    }
                }

                if (!movedAny)
                    return;

                _colliderSync?.SyncAllCharacterCombatColliders(entities);
            }
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void ApplyCharacterContactState(IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            if (entities == null)
                return;

            _lastContactStateApplyEntityCount = 0;
            if (!_contactStateApplyInitialized)
            {
                ApplyAllCharacterContactState(entities);
                _contactStateApplyInitialized = true;
                StoreAppliedContactStateEntities();
                return;
            }

            for (int i = 0; i < _appliedContactStateEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = _appliedContactStateEntities[i];
                if (entity == null || _currentContactStateEntitiesById.ContainsKey(entity.Id.Value))
                    continue;

                ApplyCharacterContactState(entity, RawVector2.Zero, RawVector2.Zero, contactCount: 0);
            }

            for (int i = 0; i < _currentContactStateEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = _currentContactStateEntities[i];
                if (entity == null)
                    continue;

                ApplyCharacterContactState(
                    entity,
                    ResolveContactVelocity(entity.Id.Value),
                    ResolveContactDisplacement(entity.Id.Value),
                    ResolveContactCount(entity.Id.Value));
            }

            StoreAppliedContactStateEntities();
        }

        private void ApplyAllCharacterContactState(IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null)
                    continue;

                ApplyCharacterContactState(
                    entity,
                    ResolveContactVelocity(entity.Id.Value),
                    ResolveContactDisplacement(entity.Id.Value),
                    ResolveContactCount(entity.Id.Value));
            }
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void ResetCharacterContactState(IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            if (entities == null)
                return;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null
                    && (entity.Physics.ContactCount != 0
                        || entity.Physics.ContactVelocityXMillimetersPerSecond != 0
                        || entity.Physics.ContactVelocityYMillimetersPerSecond != 0
                        || entity.Physics.ContactDisplacementXMillimeters != 0
                        || entity.Physics.ContactDisplacementYMillimeters != 0))
                {
                    entity.SetPhysics(CharacterTestCharacterPhysicsComponent.FromDeterministic(
                        entity.Physics.CapsuleRadiusMillimeters,
                        entity.Physics.CapsuleHeightMillimeters,
                        entity.Physics.CapsuleCenterYMillimeters,
                        entity.Physics.MassGrams,
                        entity.Physics.MassScaleMilli,
                        entity.Physics.RadiusScaleMilli,
                        entity.Physics.PushScaleMilli,
                        entity.Physics.BlocksCharacters,
                        entity.Physics.CanBePushed));
                }
            }

            _appliedContactStateEntities.Clear();
            _contactStateApplyInitialized = false;
        }

        private bool ApplyContactPressure(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            RawVector2 pushDirection,
            RawVector2 sourceVelocity,
            int pressureScaleMilli = CharacterTestDeterministicUnits.ScaleFactorScale,
            int maximumComposedSpeedMillimetersPerSecond = 0)
        {
            if (source == null
                || target == null
                || pushDirection.SqrMagnitude <= ContactEpsilonMillimeters * ContactEpsilonMillimeters)
            {
                return false;
            }

            int sourceAlong = RawVector2.DotScaled(sourceVelocity, pushDirection);
            if (sourceAlong <= ContactVelocityEpsilonMillimetersPerSecond)
                return false;

            _pressureVelocities.TryGetValue(target.Id.Value, out RawVector2 targetVelocity);
            int targetAlong = RawVector2.DotScaled(targetVelocity, pushDirection);
            if (targetAlong >= sourceAlong - ContactVelocityEpsilonMillimetersPerSecond)
                return false;

            AccumulateContactCount(source);
            AccumulateContactCount(target);
            if (!target.ColliderPosture.CanBePushed)
                return false;

            if (!TryResolveContactTransferScale(
                    source,
                    target,
                    out int massTransferMilli,
                    out int pushScaleMilli))
            {
                return false;
            }

            int desiredTargetAlong = CharacterTestDeterministicMath.MulDivRoundToInt(
                sourceAlong,
                (long)massTransferMilli * pushScaleMilli,
                CharacterTestDeterministicUnits.ScaleFactorScale * CharacterTestDeterministicUnits.ScaleFactorScale);
            if (pressureScaleMilli != CharacterTestDeterministicUnits.ScaleFactorScale)
            {
                if (pressureScaleMilli <= 0
                    || !TryScalePositive(
                        desiredTargetAlong,
                        pressureScaleMilli,
                        out long scaledDesired)
                    || scaledDesired > int.MaxValue)
                {
                    return false;
                }

                desiredTargetAlong = (int)scaledDesired;
            }
            if (desiredTargetAlong > sourceAlong)
                desiredTargetAlong = sourceAlong;
            int deltaAlong = desiredTargetAlong - targetAlong;
            if (deltaAlong <= ContactVelocityEpsilonMillimetersPerSecond)
                return false;

            if (maximumComposedSpeedMillimetersPerSecond > 0)
            {
                return ApplyBoundedPressureDelta(
                    target,
                    targetVelocity,
                    pushDirection,
                    deltaAlong,
                    maximumComposedSpeedMillimetersPerSecond);
            }

            RawVector2 pressureVelocity = pushDirection.MultiplyScaled(deltaAlong);
            _pressureVelocities[target.Id.Value] = targetVelocity + pressureVelocity;
            AccumulateContactVelocity(target, pressureVelocity);
            return true;
        }

        private static bool TryResolveContactTransferScale(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            out int massTransferMilli,
            out int pushScaleMilli)
        {
            massTransferMilli = 0;
            pushScaleMilli = 0;
            if (source == null || target == null)
                return false;

            int sourceMass = System.Math.Max(
                CharacterTestCharacterPhysicsComponent.MinimumMassGrams,
                source.Physics.EffectiveMassGrams);
            int targetMass = System.Math.Max(
                CharacterTestCharacterPhysicsComponent.MinimumMassGrams,
                target.Physics.EffectiveMassGrams);
            massTransferMilli = CharacterTestDeterministicMath.DivideRoundToInt(
                (long)sourceMass * CharacterTestDeterministicUnits.ScaleFactorScale,
                (long)sourceMass + targetMass);
            pushScaleMilli = CharacterTestDeterministicMath.MulDivRoundToInt(
                source.Physics.EffectivePushScaleMilli,
                target.Physics.EffectivePushScaleMilli,
                CharacterTestDeterministicUnits.ScaleFactorScale);
            return pushScaleMilli > 0;
        }

        private static bool TryScalePositive(long value, int scaleMilli, out long result)
        {
            result = 0;
            if (value <= 0 || scaleMilli <= 0)
                return value == 0 || scaleMilli == 0;

            try
            {
                long numerator = checked(value * scaleMilli);
                result = checked((numerator
                    + CharacterTestDeterministicUnits.ScaleFactorScale / 2)
                    / CharacterTestDeterministicUnits.ScaleFactorScale);
                return true;
            }
            catch (System.OverflowException)
            {
                return false;
            }
        }

        private bool ApplyBoundedPressureDelta(
            CharacterTestCharacterEntity target,
            RawVector2 targetVelocity,
            RawVector2 pushDirection,
            int deltaAlong,
            int maximumComposedSpeedMillimetersPerSecond)
        {
            try
            {
                RawVector2 requestedDelta = pushDirection.MultiplyScaled(deltaAlong);
                RawVector2 composed = new RawVector2(
                    checked(targetVelocity.X + requestedDelta.X),
                    checked(targetVelocity.Y + requestedDelta.Y));
                bool clamped = false;
                RawVector2 bounded = ClampMagnitude(
                    composed,
                    maximumComposedSpeedMillimetersPerSecond,
                    ref clamped);
                RawVector2 appliedDelta = new RawVector2(
                    checked(bounded.X - targetVelocity.X),
                    checked(bounded.Y - targetVelocity.Y));
                if (appliedDelta.SqrMagnitude
                    <= ContactVelocityEpsilonMillimetersPerSecond
                        * ContactVelocityEpsilonMillimetersPerSecond)
                {
                    return false;
                }

                if (clamped)
                    _lastBodyPresenceSurfaceClampCount++;
                _pressureVelocities[target.Id.Value] = bounded;
                AccumulateContactVelocity(target, appliedDelta);
                return true;
            }
            catch (System.OverflowException)
            {
                return false;
            }
        }

        private static CharacterTestCharacterEntity FindDynamicCharacterByBodyId(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CombatBodyId bodyId)
        {
            if (entities == null || bodyId.Value < CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart)
                return null;

            int entityId = bodyId.Value - CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null && entity.Id.Value == entityId)
                    return entity;
            }

            return null;
        }

        private CharacterTestCharacterEntity FindPreparedDynamicCharacterByBodyId(CombatBodyId bodyId)
        {
            return _dynamicCharactersByBodyId.TryGetValue(bodyId.Value, out CharacterTestCharacterEntity entity)
                ? entity
                : null;
        }

        private void BuildContactEntityInfos(IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            _contactEntityInfos.Clear();
            _contactEntityInfoSource = entities;
            if (entities == null || entities.Count == 0)
                return;

            if (_contactEntityInfos.Capacity < entities.Count)
                _contactEntityInfos.Capacity = entities.Count;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null || !entity.ColliderPosture.BlocksCharacters)
                    continue;

                _contactEntityInfos.Add(new ContactEntityInfo(entity));
            }
        }

        private void EnsureContactEntityInfos(IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            if (!ReferenceEquals(_contactEntityInfoSource, entities))
                BuildContactEntityInfos(entities);
        }

        private void BuildContactCandidateGrid()
        {
            ReleaseContactCells();
            _contactGridMaxRadiusMillimeters = 0;
            if (_contactEntityInfos.Count == 0)
                return;

            if (_contactCandidateIndices.Capacity < _contactEntityInfos.Count)
                _contactCandidateIndices.Capacity = _contactEntityInfos.Count;
            for (int i = 0; i < _contactEntityInfos.Count; i++)
            {
                ContactEntityInfo info = _contactEntityInfos[i];
                CharacterTestCharacterEntity entity = info.Entity;

                if (info.RadiusMillimeters > _contactGridMaxRadiusMillimeters)
                    _contactGridMaxRadiusMillimeters = info.RadiusMillimeters;

                int centerX = entity.Transform.XMillimeters;
                int centerZ = entity.Transform.ZMillimeters;
                int radius = info.RadiusMillimeters;
                ContactCell cell = ToContactCell(centerX, centerZ);
                ContactCellEntry entry = GetContactCellEntry(cell);
                entry.Add(
                    i,
                    centerX - radius,
                    centerX + radius,
                    centerZ - radius,
                    centerZ + radius,
                    info.MinYMillimeters,
                    info.MaxYMillimeters,
                    radius);
            }
        }

        private void QueryContactCandidateIndices(
            ContactEntityInfo source,
            int sourceIndex)
        {
            QueryContactCandidateIndices(
                source,
                sourceIndex,
                ContactPressureSlopMillimeters,
                ContactCandidateQueryKind.Pressure);
        }

        private void QueryContactCandidateIndices(
            ContactEntityInfo source,
            int sourceIndex,
            int querySlopMillimeters,
            ContactCandidateQueryKind queryKind)
        {
            _contactCandidateIndices.Clear();
            int sourceX = source.Entity.Transform.XMillimeters;
            int sourceZ = source.Entity.Transform.ZMillimeters;
            int queryRadius = source.RadiusMillimeters
                + _contactGridMaxRadiusMillimeters
                + querySlopMillimeters;
            ContactCell minCell = ToContactCell(sourceX - queryRadius, sourceZ - queryRadius);
            ContactCell maxCell = ToContactCell(sourceX + queryRadius, sourceZ + queryRadius);
            int sourceQueryRadius = source.RadiusMillimeters + querySlopMillimeters;
            int sourceMinX = sourceX - sourceQueryRadius;
            int sourceMaxX = sourceX + sourceQueryRadius;
            int sourceMinZ = sourceZ - sourceQueryRadius;
            int sourceMaxZ = sourceZ + sourceQueryRadius;
            if (ShouldScanActiveContactCells(minCell, maxCell))
            {
                QueryContactCandidateIndicesFromActiveCells(
                    source,
                    sourceIndex,
                    sourceX,
                    sourceZ,
                    sourceMinX,
                    sourceMaxX,
                    sourceMinZ,
                    sourceMaxZ,
                    querySlopMillimeters,
                    queryKind);
                SortAndCountContactCandidates(queryKind);
                return;
            }

            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int z = minCell.Z; z <= maxCell.Z; z++)
                {
                    CountContactCellQuery(queryKind);
                    ContactCell cell = new ContactCell(x, z);
                    if (!_contactCells.TryGetValue(cell, out ContactCellEntry entry))
                        continue;

                    AppendContactCandidatesFromCell(
                        source,
                        sourceIndex,
                        sourceX,
                        sourceZ,
                        sourceMinX,
                        sourceMaxX,
                        sourceMinZ,
                        sourceMaxZ,
                        entry,
                        querySlopMillimeters,
                        queryKind);
                }
            }

            SortAndCountContactCandidates(queryKind);
        }

        private void QueryContactCandidateIndicesFromActiveCells(
            ContactEntityInfo source,
            int sourceIndex,
            int sourceX,
            int sourceZ,
            int sourceMinX,
            int sourceMaxX,
            int sourceMinZ,
            int sourceMaxZ,
            int querySlopMillimeters,
            ContactCandidateQueryKind queryKind)
        {
            for (int i = 0; i < _activeContactCellEntries.Count; i++)
            {
                CountContactCellQuery(queryKind);
                AppendContactCandidatesFromCell(
                    source,
                    sourceIndex,
                    sourceX,
                    sourceZ,
                    sourceMinX,
                    sourceMaxX,
                    sourceMinZ,
                    sourceMaxZ,
                    _activeContactCellEntries[i],
                    querySlopMillimeters,
                    queryKind);
            }
        }

        private void AppendContactCandidatesFromCell(
            ContactEntityInfo source,
            int sourceIndex,
            int sourceX,
            int sourceZ,
            int sourceMinX,
            int sourceMaxX,
            int sourceMinZ,
            int sourceMaxZ,
            ContactCellEntry entry,
            int querySlopMillimeters,
            ContactCandidateQueryKind queryKind)
        {
            if (!VerticalRangesOverlap(
                    source.MinYMillimeters,
                    source.MaxYMillimeters,
                    entry.MinYMillimeters,
                    entry.MaxYMillimeters)
                || !ContactBoundsOverlap(
                    sourceMinX,
                    sourceMaxX,
                    sourceMinZ,
                    sourceMaxZ,
                    entry.MinXMillimeters,
                    entry.MaxXMillimeters,
                    entry.MinZMillimeters,
                    entry.MaxZMillimeters)
                || !ContactCellMayContainCandidate(
                    source,
                    sourceX,
                    sourceZ,
                    entry.Cell,
                    entry.MaxRadiusMillimeters,
                    querySlopMillimeters))
            {
                CountSkippedContactCell(queryKind);
                return;
            }

            List<int> indices = entry.Indices;
            for (int i = 0; i < indices.Count; i++)
            {
                int candidateIndex = indices[i];
                if (candidateIndex <= sourceIndex)
                    continue;

                ContactEntityInfo candidate = _contactEntityInfos[candidateIndex];
                if (ContactCandidateMayOverlap(
                    source,
                    candidate,
                    querySlopMillimeters,
                    queryKind))
                {
                    _contactCandidateIndices.Add(candidateIndex);
                }
            }
        }

        private void SortAndCountContactCandidates(ContactCandidateQueryKind queryKind)
        {
            SortContactCandidateIndices(_contactCandidateIndices);
            if (queryKind == ContactCandidateQueryKind.Pressure)
                _lastPressureCandidatePairCount += _contactCandidateIndices.Count;
            else
                _lastOverlapCandidatePairCount += _contactCandidateIndices.Count;
        }

        private bool ShouldScanActiveContactCells(ContactCell minCell, ContactCell maxCell)
        {
            if (_activeContactCellEntries.Count == 0)
                return false;

            long cellCountX = (long)maxCell.X - minCell.X + 1;
            long cellCountZ = (long)maxCell.Z - minCell.Z + 1;
            return cellCountX > 0
                && cellCountZ > 0
                && cellCountX * cellCountZ > _activeContactCellEntries.Count;
        }

        private void CountContactCellQuery(ContactCandidateQueryKind queryKind)
        {
            if (queryKind == ContactCandidateQueryKind.Pressure)
                _lastPressureQueryCellCount++;
        }

        private void CountSkippedContactCell(ContactCandidateQueryKind queryKind)
        {
            if (queryKind == ContactCandidateQueryKind.Pressure)
                _lastPressureSkippedCellCount++;
            else
                _lastOverlapSkippedCellCount++;
        }

        private ContactCellEntry GetContactCellEntry(ContactCell cell)
        {
            if (_contactCells.TryGetValue(cell, out ContactCellEntry entry))
                return entry;

            entry = RentContactCellEntry();
            entry.Activate(cell);
            _contactCells.Add(cell, entry);
            _activeContactCellEntries.Add(entry);
            return entry;
        }

        private ContactCellEntry RentContactCellEntry()
        {
            int last = _contactCellEntryPool.Count - 1;
            if (last < 0)
                return new ContactCellEntry();

            ContactCellEntry entry = _contactCellEntryPool[last];
            _contactCellEntryPool.RemoveAt(last);
            return entry;
        }

        private void ReleaseContactCells()
        {
            for (int i = 0; i < _activeContactCellEntries.Count; i++)
            {
                ContactCellEntry entry = _activeContactCellEntries[i];
                entry.Reset();
                _contactCellEntryPool.Add(entry);
            }

            _activeContactCellEntries.Clear();
            _contactCells.Clear();
        }

        private void ResetPressureCandidateStats()
        {
            _lastPressureCandidatePairCount = 0;
            _lastPressureQueryCellCount = 0;
            _lastPressureSkippedCellCount = 0;
        }

        private static void SortContactCandidateIndices(List<int> indices)
        {
            for (int i = 1; i < indices.Count; i++)
            {
                int value = indices[i];
                int j = i - 1;
                while (j >= 0 && indices[j] > value)
                {
                    indices[j + 1] = indices[j];
                    j--;
                }

                indices[j + 1] = value;
            }
        }

        private static ContactCell ToContactCell(int xMillimeters, int zMillimeters)
        {
            return new ContactCell(
                ToContactCellIndex(xMillimeters),
                ToContactCellIndex(zMillimeters));
        }

        private static int ToContactCellIndex(int value)
        {
            if (value >= 0)
                return value / ContactGridCellSizeMillimeters;

            return -(((-value - 1) / ContactGridCellSizeMillimeters) + 1);
        }

        private string GetCharacterContactStableId(int entityId)
        {
            if (entityId <= 0)
                return string.Empty;

            if (_characterContactStableIds.TryGetValue(entityId, out string stableId))
                return stableId;

            stableId = "character:" + entityId.ToString(CultureInfo.InvariantCulture);
            _characterContactStableIds.Add(entityId, stableId);
            return stableId;
        }

        private bool ApplyContactDisplacement(
            CharacterTestCharacterEntity entity,
            RawVector2 displacement)
        {
            if (entity == null
                || displacement.SqrMagnitude <= ContactEpsilonMillimeters * ContactEpsilonMillimeters)
            {
                return false;
            }

            entity.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                entity.Transform.XMillimeters + displacement.X,
                entity.Transform.YMillimeters,
                entity.Transform.ZMillimeters + displacement.Y,
                entity.Transform.YawMilliDegrees));
            AccumulateContactDisplacement(entity, displacement);
            return true;
        }

        private void ApplyCharacterContactState(
            CharacterTestCharacterEntity entity,
            RawVector2 contactVelocity,
            RawVector2 contactDisplacement,
            int contactCount)
        {
            if (entity == null)
                return;

            entity.SetPhysics(CharacterTestCharacterPhysicsComponent.FromDeterministic(
                entity.Physics.CapsuleRadiusMillimeters,
                entity.Physics.CapsuleHeightMillimeters,
                entity.Physics.CapsuleCenterYMillimeters,
                entity.Physics.MassGrams,
                entity.Physics.MassScaleMilli,
                entity.Physics.RadiusScaleMilli,
                entity.Physics.PushScaleMilli,
                entity.Physics.BlocksCharacters,
                entity.Physics.CanBePushed,
                contactVelocity.X,
                contactVelocity.Y,
                contactDisplacement.X,
                contactDisplacement.Y,
                contactCount));
            _lastContactStateApplyEntityCount++;
        }

        private RawVector2 ResolveContactVelocity(int entityId)
        {
            return _contactVelocities.TryGetValue(entityId, out RawVector2 contactVelocity)
                ? contactVelocity
                : RawVector2.Zero;
        }

        private RawVector2 ResolveContactDisplacement(int entityId)
        {
            return _contactDisplacements.TryGetValue(entityId, out RawVector2 contactDisplacement)
                ? contactDisplacement
                : RawVector2.Zero;
        }

        private int ResolveContactCount(int entityId)
        {
            return _contactCounts.TryGetValue(entityId, out int contactCount)
                ? contactCount
                : 0;
        }

        private void StoreAppliedContactStateEntities()
        {
            _appliedContactStateEntities.Clear();
            for (int i = 0; i < _currentContactStateEntities.Count; i++)
                _appliedContactStateEntities.Add(_currentContactStateEntities[i]);
        }

        private void RegisterCurrentContactStateEntity(CharacterTestCharacterEntity entity)
        {
            if (entity == null || entity.Id.Value <= 0)
                return;

            int entityId = entity.Id.Value;
            if (_currentContactStateEntitiesById.ContainsKey(entityId))
                return;

            _currentContactStateEntitiesById.Add(entityId, entity);
            _currentContactStateEntities.Add(entity);
        }

        private void AccumulateContactVelocity(CharacterTestCharacterEntity entity, RawVector2 velocity)
        {
            if (entity == null
                || entity.Id.Value <= 0
                || velocity.SqrMagnitude <= ContactVelocityEpsilonMillimetersPerSecond * ContactVelocityEpsilonMillimetersPerSecond)
            {
                return;
            }

            int entityId = entity.Id.Value;
            RegisterCurrentContactStateEntity(entity);
            _contactVelocities.TryGetValue(entityId, out RawVector2 existing);
            _contactVelocities[entityId] = existing + velocity;
        }

        private void AccumulateContactDisplacement(CharacterTestCharacterEntity entity, RawVector2 displacement)
        {
            if (entity == null
                || entity.Id.Value <= 0
                || displacement.SqrMagnitude <= ContactEpsilonMillimeters * ContactEpsilonMillimeters)
            {
                return;
            }

            int entityId = entity.Id.Value;
            RegisterCurrentContactStateEntity(entity);
            _contactDisplacements.TryGetValue(entityId, out RawVector2 existing);
            _contactDisplacements[entityId] = existing + displacement;
        }

        private void AccumulateContactCount(CharacterTestCharacterEntity entity)
        {
            if (entity == null || entity.Id.Value <= 0)
                return;

            int entityId = entity.Id.Value;
            RegisterCurrentContactStateEntity(entity);
            _contactCounts.TryGetValue(entityId, out int existing);
            _contactCounts[entityId] = existing + 1;
        }

        private static long ResolvePushWeight(CharacterTestCharacterEntity entity)
        {
            if (entity == null || !entity.ColliderPosture.CanBePushed)
                return 0;

            int mass = System.Math.Max(
                CharacterTestCharacterPhysicsComponent.MinimumMassGrams,
                entity.Physics.EffectiveMassGrams);
            return CharacterTestDeterministicMath.DivideRoundToInt(
                (long)entity.Physics.EffectivePushScaleMilli * CharacterTestDeterministicMath.TrigScale,
                mass);
        }

        private static RawVector2 ResolveContactNormal(
            CharacterTestCharacterEntity a,
            CharacterTestCharacterEntity b,
            int deltaX,
            int deltaY,
            long distanceSq)
        {
            if (distanceSq <= ContactEpsilonMillimeters * ContactEpsilonMillimeters)
            {
                return a.Id.Value <= b.Id.Value
                    ? new RawVector2(CharacterTestDeterministicMath.TrigScale, 0)
                    : new RawVector2(-CharacterTestDeterministicMath.TrigScale, 0);
            }

            CharacterTestDeterministicMath.NormalizeToScale(
                deltaX,
                deltaY,
                CharacterTestDeterministicMath.TrigScale,
                out int normalX,
                out int normalY);
            return new RawVector2(normalX, normalY);
        }

        private static bool ContactCandidateMayOverlap(
            ContactEntityInfo source,
            ContactEntityInfo candidate,
            int querySlopMillimeters,
            ContactCandidateQueryKind queryKind)
        {
            if (!VerticalRangesOverlap(source, candidate))
                return false;

            long deltaX = (long)candidate.Entity.Transform.XMillimeters
                - source.Entity.Transform.XMillimeters;
            long deltaY = (long)candidate.Entity.Transform.ZMillimeters
                - source.Entity.Transform.ZMillimeters;
            long queryRange = (long)source.RadiusMillimeters
                + candidate.RadiusMillimeters
                + querySlopMillimeters;
            long distanceSq = deltaX * deltaX + deltaY * deltaY;
            long queryRangeSq = queryRange * queryRange;
            return queryKind == ContactCandidateQueryKind.Overlap
                ? distanceSq < queryRangeSq
                : distanceSq <= queryRangeSq;
        }

        private static bool ContactCellMayContainCandidate(
            ContactEntityInfo source,
            int sourceX,
            int sourceZ,
            ContactCell cell,
            int cellMaxRadiusMillimeters,
            int querySlopMillimeters)
        {
            int minX = cell.X * ContactGridCellSizeMillimeters;
            int minZ = cell.Z * ContactGridCellSizeMillimeters;
            int maxX = minX + ContactGridCellSizeMillimeters;
            int maxZ = minZ + ContactGridCellSizeMillimeters;
            long deltaX = 0;
            if (sourceX < minX)
                deltaX = minX - sourceX;
            else if (sourceX > maxX)
                deltaX = sourceX - maxX;

            long deltaZ = 0;
            if (sourceZ < minZ)
                deltaZ = minZ - sourceZ;
            else if (sourceZ > maxZ)
                deltaZ = sourceZ - maxZ;

            long queryRange = (long)source.RadiusMillimeters
                + cellMaxRadiusMillimeters
                + querySlopMillimeters;
            return (deltaX * deltaX) + (deltaZ * deltaZ) <= queryRange * queryRange;
        }

        private static bool ContactBoundsOverlap(
            int minX,
            int maxX,
            int minZ,
            int maxZ,
            int candidateMinX,
            int candidateMaxX,
            int candidateMinZ,
            int candidateMaxZ)
        {
            return maxX >= candidateMinX
                && candidateMaxX >= minX
                && maxZ >= candidateMinZ
                && candidateMaxZ >= minZ;
        }

        private static bool VerticalRangesOverlap(
            CharacterTestCharacterEntity a,
            CharacterTestCharacterEntity b)
        {
            int halfA = a.ColliderPosture.HeightMillimeters / 2;
            int halfB = b.ColliderPosture.HeightMillimeters / 2;
            int centerA = a.Transform.YMillimeters + ResolveEffectiveCenterYMillimeters(a.ColliderPosture);
            int centerB = b.Transform.YMillimeters + ResolveEffectiveCenterYMillimeters(b.ColliderPosture);
            int minA = centerA - halfA;
            int maxA = centerA + halfA;
            int minB = centerB - halfB;
            int maxB = centerB + halfB;
            return maxA > minB + ContactEpsilonMillimeters
                && maxB > minA + ContactEpsilonMillimeters;
        }

        private static bool VerticalRangesOverlap(ContactEntityInfo a, ContactEntityInfo b)
        {
            return VerticalRangesOverlap(
                a.MinYMillimeters,
                a.MaxYMillimeters,
                b.MinYMillimeters,
                b.MaxYMillimeters);
        }

        private static bool VerticalRangesOverlap(int minA, int maxA, int minB, int maxB)
        {
            return maxA > minB + ContactEpsilonMillimeters
                && maxB > minA + ContactEpsilonMillimeters;
        }

        private static int ResolveEffectiveCenterYMillimeters(CharacterTestCharacterPhysicsComponent physics)
        {
            return physics.CapsuleCenterYMillimeters > 0
                ? physics.CapsuleCenterYMillimeters
                : physics.EffectiveHeightMillimeters / 2;
        }

        private static int ResolveEffectiveCenterYMillimeters(CharacterTestColliderPostureComponent posture)
        {
            return posture.CenterYMillimeters > 0
                ? posture.CenterYMillimeters
                : posture.HeightMillimeters / 2;
        }

        private readonly struct BodyPresencePairKey : System.IEquatable<BodyPresencePairKey>
        {
            private BodyPresencePairKey(int minEntityId, int maxEntityId)
            {
                MinEntityId = minEntityId;
                MaxEntityId = maxEntityId;
            }

            public int MinEntityId { get; }

            public int MaxEntityId { get; }

            public static BodyPresencePairKey Create(int leftEntityId, int rightEntityId)
            {
                return leftEntityId <= rightEntityId
                    ? new BodyPresencePairKey(leftEntityId, rightEntityId)
                    : new BodyPresencePairKey(rightEntityId, leftEntityId);
            }

            public int CompareTo(BodyPresencePairKey other)
            {
                int compare = MinEntityId.CompareTo(other.MinEntityId);
                return compare != 0 ? compare : MaxEntityId.CompareTo(other.MaxEntityId);
            }

            public bool Equals(BodyPresencePairKey other)
            {
                return MinEntityId == other.MinEntityId
                    && MaxEntityId == other.MaxEntityId;
            }
        }

        private readonly struct BodyPresenceDirectedKey : System.IEquatable<BodyPresenceDirectedKey>
        {
            public BodyPresenceDirectedKey(
                int presenceEntityId,
                int presenceBodyId,
                int colliderId,
                int leafId,
                int targetEntityId)
            {
                PresenceEntityId = presenceEntityId;
                PresenceBodyId = presenceBodyId;
                ColliderId = colliderId;
                LeafId = leafId;
                TargetEntityId = targetEntityId;
            }

            public int PresenceEntityId { get; }

            public int PresenceBodyId { get; }

            public int ColliderId { get; }

            public int LeafId { get; }

            public int TargetEntityId { get; }

            public int CompareTo(BodyPresenceDirectedKey other)
            {
                int compare = PresenceEntityId.CompareTo(other.PresenceEntityId);
                if (compare != 0) return compare;
                compare = PresenceBodyId.CompareTo(other.PresenceBodyId);
                if (compare != 0) return compare;
                compare = ColliderId.CompareTo(other.ColliderId);
                if (compare != 0) return compare;
                compare = LeafId.CompareTo(other.LeafId);
                return compare != 0 ? compare : TargetEntityId.CompareTo(other.TargetEntityId);
            }

            public bool Equals(BodyPresenceDirectedKey other)
            {
                return PresenceEntityId == other.PresenceEntityId
                    && PresenceBodyId == other.PresenceBodyId
                    && ColliderId == other.ColliderId
                    && LeafId == other.LeafId
                    && TargetEntityId == other.TargetEntityId;
            }
        }

        private readonly struct BodyPresenceDirectedConstraint
        {
            public BodyPresenceDirectedConstraint(
                BodyPresencePairKey pairKey,
                BodyPresenceDirectedKey directedKey,
                CharacterTestCharacterEntity presenceOwner,
                CharacterTestCharacterEntity target,
                CombatShapeSeparationResult separation,
                RawVector2 horizontalNormal,
                BodyPresenceConstraintMotion surfaceMotion)
            {
                PairKey = pairKey;
                DirectedKey = directedKey;
                PresenceOwner = presenceOwner;
                Target = target;
                Separation = separation;
                HorizontalNormal = horizontalNormal;
                SurfaceMotion = surfaceMotion;
            }

            public BodyPresencePairKey PairKey { get; }

            public BodyPresenceDirectedKey DirectedKey { get; }

            public CharacterTestCharacterEntity PresenceOwner { get; }

            public CharacterTestCharacterEntity Target { get; }

            public CombatShapeSeparationResult Separation { get; }

            public RawVector2 HorizontalNormal { get; }

            public BodyPresenceConstraintMotion SurfaceMotion { get; }

            public static int Compare(
                BodyPresenceDirectedConstraint left,
                BodyPresenceDirectedConstraint right)
            {
                int compare = left.PairKey.CompareTo(right.PairKey);
                if (compare != 0) return compare;
                compare = left.DirectedKey.CompareTo(right.DirectedKey);
                return compare != 0
                    ? compare
                    : CombatShapeSeparationResultComparer.Instance.Compare(
                        left.Separation,
                        right.Separation);
            }
        }

        private readonly struct BodyPresenceConstraintMotion
        {
            public BodyPresenceConstraintMotion(
                RawVector2 surfaceVelocity,
                int penetrationBiasMillimetersPerSecond,
                int pressureScaleMilli,
                int maximumComposedSpeedMillimetersPerSecond)
            {
                SurfaceVelocity = surfaceVelocity;
                PenetrationBiasMillimetersPerSecond = penetrationBiasMillimetersPerSecond;
                PressureScaleMilli = pressureScaleMilli;
                MaximumComposedSpeedMillimetersPerSecond =
                    maximumComposedSpeedMillimetersPerSecond;
                Enabled = true;
            }

            public bool Enabled { get; }

            public RawVector2 SurfaceVelocity { get; }

            public int PenetrationBiasMillimetersPerSecond { get; }

            public int PressureScaleMilli { get; }

            public int MaximumComposedSpeedMillimetersPerSecond { get; }
        }

        private readonly struct BodyPresenceMotionLimits
        {
            private BodyPresenceMotionLimits(
                int tickRate,
                int maximumComposedSpeedMillimetersPerSecond,
                int accelerationStepMillimetersPerSecond,
                int turnSpeedMilliDegreesPerSecond)
            {
                TickRate = tickRate;
                MaximumComposedSpeedMillimetersPerSecond =
                    maximumComposedSpeedMillimetersPerSecond;
                AccelerationStepMillimetersPerSecond = accelerationStepMillimetersPerSecond;
                TurnSpeedMilliDegreesPerSecond = turnSpeedMilliDegreesPerSecond;
            }

            public int TickRate { get; }

            public int MaximumComposedSpeedMillimetersPerSecond { get; }

            public int AccelerationStepMillimetersPerSecond { get; }

            public int TurnSpeedMilliDegreesPerSecond { get; }

            public static bool TryCreate(
                int runSpeedMillimetersPerSecond,
                int accelerationMillimetersPerSecondSquared,
                int turnSpeedMilliDegreesPerSecond,
                int tickRate,
                out BodyPresenceMotionLimits limits)
            {
                limits = default;
                if (runSpeedMillimetersPerSecond < 0
                    || accelerationMillimetersPerSecondSquared < 0
                    || turnSpeedMilliDegreesPerSecond < 0
                    || tickRate <= 0)
                {
                    return false;
                }

                try
                {
                    int accelerationStep = checked((int)(
                        ((long)accelerationMillimetersPerSecondSquared + tickRate - 1)
                        / tickRate));
                    int maximumComposedSpeed = checked(
                        runSpeedMillimetersPerSecond + accelerationStep);
                    limits = new BodyPresenceMotionLimits(
                        tickRate,
                        maximumComposedSpeed,
                        accelerationStep,
                        turnSpeedMilliDegreesPerSecond);
                    return true;
                }
                catch (System.OverflowException)
                {
                    return false;
                }
            }
        }

        private readonly struct ContactEntityInfo
        {
            public ContactEntityInfo(CharacterTestCharacterEntity entity)
            {
                Entity = entity;
                CharacterTestColliderPostureComponent posture = entity.ColliderPosture;
                RadiusMillimeters = posture.RadiusMillimeters;
                int halfHeight = posture.HeightMillimeters / 2;
                int centerY = entity.Transform.YMillimeters + ResolveEffectiveCenterYMillimeters(posture);
                MinYMillimeters = centerY - halfHeight;
                MaxYMillimeters = centerY + halfHeight;
                PushWeight = ResolvePushWeight(entity);
            }

            public CharacterTestCharacterEntity Entity { get; }

            public int RadiusMillimeters { get; }

            public int MinYMillimeters { get; }

            public int MaxYMillimeters { get; }

            public long PushWeight { get; }
        }

        private sealed class ContactCellEntry
        {
            public ContactCellEntry()
            {
                Indices = new List<int>(4);
            }

            public List<int> Indices { get; }

            public ContactCell Cell { get; private set; }

            public int MaxRadiusMillimeters { get; set; }

            public int MinXMillimeters { get; private set; }

            public int MaxXMillimeters { get; private set; }

            public int MinZMillimeters { get; private set; }

            public int MaxZMillimeters { get; private set; }

            public int MinYMillimeters { get; private set; }

            public int MaxYMillimeters { get; private set; }

            public void Activate(ContactCell cell)
            {
                Cell = cell;
            }

            public void Add(
                int index,
                int minX,
                int maxX,
                int minZ,
                int maxZ,
                int minY,
                int maxY,
                int radius)
            {
                if (Indices.Count == 0)
                {
                    MinXMillimeters = minX;
                    MaxXMillimeters = maxX;
                    MinZMillimeters = minZ;
                    MaxZMillimeters = maxZ;
                    MinYMillimeters = minY;
                    MaxYMillimeters = maxY;
                }
                else
                {
                    if (minX < MinXMillimeters)
                        MinXMillimeters = minX;
                    if (maxX > MaxXMillimeters)
                        MaxXMillimeters = maxX;
                    if (minZ < MinZMillimeters)
                        MinZMillimeters = minZ;
                    if (maxZ > MaxZMillimeters)
                        MaxZMillimeters = maxZ;
                    if (minY < MinYMillimeters)
                        MinYMillimeters = minY;
                    if (maxY > MaxYMillimeters)
                        MaxYMillimeters = maxY;
                }

                Indices.Add(index);
                if (radius > MaxRadiusMillimeters)
                    MaxRadiusMillimeters = radius;
            }

            public void Reset()
            {
                Indices.Clear();
                MaxRadiusMillimeters = 0;
                MinXMillimeters = 0;
                MaxXMillimeters = 0;
                MinZMillimeters = 0;
                MaxZMillimeters = 0;
                MinYMillimeters = 0;
                MaxYMillimeters = 0;
                Cell = default;
            }
        }

        private enum ContactCandidateQueryKind
        {
            Overlap,
            Pressure
        }

        private readonly struct RawVector2
        {
            public static readonly RawVector2 Zero = new RawVector2(0, 0);

            public RawVector2(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }
            public int SqrMagnitude => CharacterTestDeterministicMath.SqrMagnitude(X, Y);

            public RawVector2 MultiplyScaled(int magnitude)
            {
                return new RawVector2(
                    CharacterTestDeterministicMath.MulDivRoundToInt(X, magnitude, CharacterTestDeterministicMath.TrigScale),
                    CharacterTestDeterministicMath.MulDivRoundToInt(Y, magnitude, CharacterTestDeterministicMath.TrigScale));
            }

            public static int DotScaled(RawVector2 a, RawVector2 b)
            {
                return CharacterTestDeterministicMath.DivideRoundToInt(
                    (long)a.X * b.X + (long)a.Y * b.Y,
                    CharacterTestDeterministicMath.TrigScale);
            }

            public static RawVector2 operator +(RawVector2 a, RawVector2 b)
            {
                return new RawVector2(a.X + b.X, a.Y + b.Y);
            }

            public static RawVector2 operator -(RawVector2 value)
            {
                return new RawVector2(-value.X, -value.Y);
            }
        }

        private readonly struct ContactCell : System.IEquatable<ContactCell>
        {
            public ContactCell(int x, int z)
            {
                X = x;
                Z = z;
            }

            public int X { get; }

            public int Z { get; }

            public bool Equals(ContactCell other)
            {
                return X == other.X && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is ContactCell other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (X * 397) ^ Z;
                }
            }
        }
    }
}
