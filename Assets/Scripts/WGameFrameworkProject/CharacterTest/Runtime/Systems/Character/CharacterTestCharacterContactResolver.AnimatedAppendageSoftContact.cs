using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestAnimatedAppendageSoftContactBuildStatus
    {
        NotRequested = 0,
        Success = 1,
        SourceUnavailable = 2,
        CapacityExceeded = 3,
        InvalidIdentity = 4,
        InvalidSeparation = 5,
        UnsupportedSeparation = 6,
        ConflictingDuplicate = 7,
        InvalidMotionWitness = 8,
        InvalidPolicy = 9,
        ArithmeticOverflow = 10
    }

    internal enum CharacterTestAnimatedAppendageSoftContactPhase
    {
        Started = 1,
        Stayed = 2,
        Ended = 3
    }

    internal readonly struct CharacterTestAnimatedAppendageSoftContactPhaseFact
    {
        internal CharacterTestAnimatedAppendageSoftContactPhaseFact(
            CharacterTestAnimatedAppendageSoftContactPhase phase,
            int sourceEntityId,
            int targetEntityId,
            int logicalColliderId,
            string logicalColliderLabel,
            int combatColliderId,
            CombatPhysicsShapeKind shapeKind)
        {
            Phase = phase;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            LogicalColliderId = logicalColliderId;
            LogicalColliderLabel = logicalColliderLabel ?? string.Empty;
            CombatColliderId = combatColliderId;
            ShapeKind = shapeKind;
        }

        internal CharacterTestAnimatedAppendageSoftContactPhase Phase { get; }
        internal int SourceEntityId { get; }
        internal int TargetEntityId { get; }
        internal int LogicalColliderId { get; }
        internal string LogicalColliderLabel { get; }
        internal int CombatColliderId { get; }
        internal CombatPhysicsShapeKind ShapeKind { get; }
    }

    internal readonly struct CharacterTestAnimatedAppendageSoftContactDiagnostics
    {
        internal CharacterTestAnimatedAppendageSoftContactDiagnostics(
            CharacterTestAnimatedAppendageSoftContactBuildStatus status,
            int previousWitnessCount,
            int currentWitnessCount,
            int constraintCount,
            int phaseFactCount,
            int pressureIterationCount,
            int clampCount)
        {
            Status = status;
            PreviousWitnessCount = previousWitnessCount;
            CurrentWitnessCount = currentWitnessCount;
            ConstraintCount = constraintCount;
            PhaseFactCount = phaseFactCount;
            PressureIterationCount = pressureIterationCount;
            ClampCount = clampCount;
        }

        internal CharacterTestAnimatedAppendageSoftContactBuildStatus Status { get; }
        internal int PreviousWitnessCount { get; }
        internal int CurrentWitnessCount { get; }
        internal int ConstraintCount { get; }
        internal int PhaseFactCount { get; }
        internal int PressureIterationCount { get; }
        internal int ClampCount { get; }
    }

    internal sealed partial class CharacterTestCharacterContactResolver
    {
        private const int AnimatedAppendageMaximumRoster = 9;
        private const int AnimatedAppendageMaximumLeafWitnesses = 32;
        private const int AnimatedAppendageDirectedConstraintCapacity = 256;
        private const int AnimatedAppendagePhaseFactCapacity = 512;

        private readonly CharacterTestCharacterEntity[] _animatedAppendageRoster =
            new CharacterTestCharacterEntity[AnimatedAppendageMaximumRoster];
        private readonly AnimatedAppendageConstraint[] _animatedAppendageConstraints =
            new AnimatedAppendageConstraint[AnimatedAppendageDirectedConstraintCapacity];
        private readonly AnimatedAppendageContactKey[] _animatedAppendagePreviousPhaseKeys =
            new AnimatedAppendageContactKey[AnimatedAppendageDirectedConstraintCapacity];
        private readonly CharacterTestAnimatedAppendageSoftContactPhaseFact[] _animatedAppendagePhaseFacts =
            new CharacterTestAnimatedAppendageSoftContactPhaseFact[AnimatedAppendagePhaseFactCapacity];
        private int _animatedAppendageRosterCount;
        private int _animatedAppendageConstraintCount;
        private int _animatedAppendagePreviousPhaseKeyCount;
        private int _animatedAppendagePhaseFactCount;
        private int _animatedAppendagePreviousWitnessCount;
        private int _animatedAppendageCurrentWitnessCount;
        private int _animatedAppendageClampCount;
        private bool _animatedAppendagePhaseBaselineInitialized;
        private bool _suppressAnimatedAppendagePresentationPhases;
        private CharacterTestAnimatedAppendageSoftContactBuildStatus _animatedAppendageBuildStatus;

        internal CharacterTestAnimatedAppendageSoftContactDiagnostics
            CaptureAnimatedAppendageSoftContactDiagnostics()
        {
            return new CharacterTestAnimatedAppendageSoftContactDiagnostics(
                _animatedAppendageBuildStatus,
                _animatedAppendagePreviousWitnessCount,
                _animatedAppendageCurrentWitnessCount,
                _animatedAppendageConstraintCount,
                _animatedAppendagePhaseFactCount,
                ContactPressureIterations,
                _animatedAppendageClampCount);
        }

        internal bool TryGetAnimatedAppendageSoftContactPhase(
            int index,
            out CharacterTestAnimatedAppendageSoftContactPhaseFact fact)
        {
            if ((uint)index >= (uint)_animatedAppendagePhaseFactCount)
            {
                fact = default;
                return false;
            }
            fact = _animatedAppendagePhaseFacts[index];
            return true;
        }

        internal void SuppressAnimatedAppendagePresentationPhases()
        {
            _animatedAppendagePreviousPhaseKeyCount = 0;
            _animatedAppendagePhaseFactCount = 0;
            _animatedAppendagePhaseBaselineInitialized = false;
            _suppressAnimatedAppendagePresentationPhases = true;
        }

        internal void ResolveAnimatedAppendageSoftContacts(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestAnimatedAppendageLeafWitness[] previousWitnesses,
            int previousWitnessCount,
            bool previousReady,
            CharacterTestAnimatedAppendageLeafWitness[] currentWitnesses,
            int currentWitnessCount,
            bool currentReady,
            int runSpeedMillimetersPerSecond,
            int accelerationMillimetersPerSecondSquared,
            int tickRate)
        {
            ResetAnimatedAppendageSoftContactBuild();
            _animatedAppendagePreviousWitnessCount = previousWitnessCount;
            _animatedAppendageCurrentWitnessCount = currentWitnessCount;
            _animatedAppendageBuildStatus = CharacterTestAnimatedAppendageSoftContactBuildStatus.SourceUnavailable;
            if (!currentReady
                || physicsRuntime?.World == null
                || entities == null
                || previousWitnessCount < 0
                || currentWitnessCount < 0
                || previousWitnesses == null
                || currentWitnesses == null
                || previousWitnessCount > previousWitnesses.Length
                || currentWitnessCount > currentWitnesses.Length)
            {
                return;
            }

            if (previousWitnessCount > AnimatedAppendageMaximumLeafWitnesses
                || currentWitnessCount > AnimatedAppendageMaximumLeafWitnesses
                || entities.Count > AnimatedAppendageMaximumRoster)
            {
                _animatedAppendageBuildStatus =
                    CharacterTestAnimatedAppendageSoftContactBuildStatus.CapacityExceeded;
                return;
            }

            if (!BodyPresenceMotionLimits.TryCreate(
                    runSpeedMillimetersPerSecond,
                    accelerationMillimetersPerSecondSquared,
                    turnSpeedMilliDegreesPerSecond: 0,
                    tickRate,
                    out BodyPresenceMotionLimits limits))
            {
                _animatedAppendageBuildStatus =
                    CharacterTestAnimatedAppendageSoftContactBuildStatus.InvalidMotionWitness;
                return;
            }

            if (!TryPrepareAnimatedAppendageRoster(entities)
                || !ValidateAnimatedAppendageWitnesses(currentWitnesses, currentWitnessCount))
            {
                _animatedAppendageBuildStatus =
                    CharacterTestAnimatedAppendageSoftContactBuildStatus.InvalidIdentity;
                return;
            }

            bool sourceReset = !previousReady;
            for (int witnessIndex = 0; witnessIndex < currentWitnessCount; witnessIndex++)
            {
                CharacterTestAnimatedAppendageLeafWitness current = currentWitnesses[witnessIndex];
                CharacterTestCharacterEntity owner = FindAnimatedAppendageRosterEntity(current.OwnerEntityId);
                if (!IsValidAnimatedAppendageEntity(owner))
                    continue;

                if (current.PressureScaleMilli < 0
                    || current.PressureScaleMilli > 100000
                    || current.SkinMillimeters < 0
                    || current.SkinMillimeters > 10000)
                {
                    _animatedAppendageBuildStatus =
                        CharacterTestAnimatedAppendageSoftContactBuildStatus.InvalidPolicy;
                    RejectAnimatedAppendageBuild();
                    return;
                }

                CombatPhysicsShape worldShape;
                try
                {
                    worldShape = TranslateAnimatedAppendageShape(
                        current.Shape,
                        current.OwnerRootWorld);
                }
                catch (ArithmeticException)
                {
                    _animatedAppendageBuildStatus =
                        CharacterTestAnimatedAppendageSoftContactBuildStatus.ArithmeticOverflow;
                    RejectAnimatedAppendageBuild();
                    return;
                }

                CharacterTestAnimatedAppendageLeafWitness previous = default;
                bool hasPrevious = previousReady
                    && TryFindPreviousAnimatedAppendageWitness(
                        previousWitnesses,
                        previousWitnessCount,
                        in current,
                        out previous);
                if (hasPrevious && !IsCompatibleAnimatedAppendageMotionWitness(in previous, in current))
                    hasPrevious = false;
                if (!hasPrevious)
                    sourceReset = true;

                for (int targetIndex = 0; targetIndex < _animatedAppendageRosterCount; targetIndex++)
                {
                    CharacterTestCharacterEntity target = _animatedAppendageRoster[targetIndex];
                    if (target.Id.Value == current.OwnerEntityId
                        || !IsValidAnimatedAppendageEntity(target))
                    {
                        continue;
                    }

                    if (_animatedAppendageConstraintCount
                        >= AnimatedAppendageDirectedConstraintCapacity)
                    {
                        _animatedAppendageBuildStatus =
                            CharacterTestAnimatedAppendageSoftContactBuildStatus.CapacityExceeded;
                        RejectAnimatedAppendageBuild();
                        return;
                    }

                    CombatCapsuleQuery query = CreateAnimatedAppendageQuery(
                        target,
                        physicsRuntime,
                        targetIndex,
                        current.SkinMillimeters);
                    CombatShapeSeparationResult separation =
                        CombatPhysicsWorld.CalculateCapsuleSeparation(
                            query,
                            physicsRuntime.GetEntityId(owner),
                            physicsRuntime.GetHurtboxBodyId(owner),
                            new CombatColliderId(current.CombatColliderId),
                            worldShape,
                            targetIsCompoundLeaf: true);
                    if (separation.Status == CombatShapeSeparationStatus.InvalidShape)
                    {
                        _animatedAppendageBuildStatus =
                            CharacterTestAnimatedAppendageSoftContactBuildStatus.InvalidSeparation;
                        RejectAnimatedAppendageBuild();
                        return;
                    }
                    if (separation.Status == CombatShapeSeparationStatus.UnsupportedShape)
                    {
                        _animatedAppendageBuildStatus =
                            CharacterTestAnimatedAppendageSoftContactBuildStatus.UnsupportedSeparation;
                        RejectAnimatedAppendageBuild();
                        return;
                    }
                    if (!separation.HasContact)
                        continue;

                    var key = new AnimatedAppendageContactKey(
                        target.Id.Value,
                        owner.Id.Value,
                        current.LogicalColliderId,
                        current.LogicalColliderLabel,
                        current.CombatColliderId,
                        current.Shape.Kind);
                    int duplicateIndex = FindAnimatedAppendageConstraint(key);
                    if (duplicateIndex >= 0)
                    {
                        if (_animatedAppendageConstraints[duplicateIndex].Separation.Equals(separation))
                            continue;
                        _animatedAppendageBuildStatus =
                            CharacterTestAnimatedAppendageSoftContactBuildStatus.ConflictingDuplicate;
                        RejectAnimatedAppendageBuild();
                        return;
                    }

                    RawVector2 normal = ResolveBodyPresenceHorizontalNormal(separation.Normal);
                    if (normal.SqrMagnitude
                        <= ContactEpsilonMillimeters * ContactEpsilonMillimeters)
                    {
                        _animatedAppendageBuildStatus =
                            CharacterTestAnimatedAppendageSoftContactBuildStatus.InvalidSeparation;
                        RejectAnimatedAppendageBuild();
                        return;
                    }

                    if (!TryCreateAnimatedAppendageMotion(
                            in current,
                            hasPrevious,
                            in previous,
                            in separation,
                            in limits,
                            out AnimatedAppendageConstraintMotion motion))
                    {
                        _animatedAppendageBuildStatus =
                            CharacterTestAnimatedAppendageSoftContactBuildStatus.ArithmeticOverflow;
                        RejectAnimatedAppendageBuild();
                        return;
                    }

                    _animatedAppendageConstraints[_animatedAppendageConstraintCount++] =
                        new AnimatedAppendageConstraint(
                            key,
                            owner,
                            target,
                            separation,
                            normal,
                            motion);
                }
            }

            SortAnimatedAppendageConstraints();

            // Mutation starts only after every identity, capacity, separation and arithmetic
            // check above has succeeded.
            foreach (KeyValuePair<int, RawVector2> attempted in _attemptedContactVelocities)
            {
                if (!_pressureVelocities.ContainsKey(attempted.Key))
                    _pressureVelocities[attempted.Key] = attempted.Value;
            }
            for (int i = 0; i < _animatedAppendageConstraintCount; i++)
            {
                AnimatedAppendageConstraint constraint = _animatedAppendageConstraints[i];
                AccumulateContactCount(constraint.Owner);
                AccumulateContactCount(constraint.Target);
            }
            for (int iteration = 0; iteration < ContactPressureIterations; iteration++)
            {
                bool changed = false;
                for (int i = 0; i < _animatedAppendageConstraintCount; i++)
                    changed |= ApplyAnimatedAppendagePressure(_animatedAppendageConstraints[i]);
                if (!changed)
                    break;
            }

            DeriveAnimatedAppendagePresentationPhases(sourceReset);
            _animatedAppendageBuildStatus =
                CharacterTestAnimatedAppendageSoftContactBuildStatus.Success;
        }

        private bool TryPrepareAnimatedAppendageRoster(
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null || entity.Id.Value <= 0)
                    return false;
                for (int j = 0; j < _animatedAppendageRosterCount; j++)
                {
                    if (_animatedAppendageRoster[j].Id.Value == entity.Id.Value)
                        return false;
                }
                int insert = _animatedAppendageRosterCount;
                while (insert > 0
                    && _animatedAppendageRoster[insert - 1].Id.Value > entity.Id.Value)
                {
                    _animatedAppendageRoster[insert] = _animatedAppendageRoster[insert - 1];
                    insert--;
                }
                _animatedAppendageRoster[insert] = entity;
                _animatedAppendageRosterCount++;
            }
            return true;
        }

        private static bool ValidateAnimatedAppendageWitnesses(
            CharacterTestAnimatedAppendageLeafWitness[] witnesses,
            int count)
        {
            for (int i = 0; i < count; i++)
            {
                CharacterTestAnimatedAppendageLeafWitness value = witnesses[i];
                if (value.OwnerEntityId <= 0
                    || value.CombatColliderId < CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart
                    || value.LogicalColliderId <= 0
                    || value.PartId <= 0
                    || value.BindingRevision <= 0
                    || string.IsNullOrEmpty(value.BindingContentHash)
                    || (value.Shape.Kind != CombatPhysicsShapeKind.Sphere
                        && value.Shape.Kind != CombatPhysicsShapeKind.Capsule
                        && value.Shape.Kind != CombatPhysicsShapeKind.Obb))
                {
                    return false;
                }
                for (int j = 0; j < i; j++)
                {
                    CharacterTestAnimatedAppendageLeafWitness previous = witnesses[j];
                    if (previous.OwnerEntityId == value.OwnerEntityId
                        && previous.CombatColliderId == value.CombatColliderId
                        && previous.Shape.Kind == value.Shape.Kind
                        && previous.LogicalColliderId == value.LogicalColliderId)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IsValidAnimatedAppendageEntity(CharacterTestCharacterEntity entity)
        {
            return entity != null
                && entity.Id.Value > 0
                && entity.BattleState.CountsAsAlive
                && entity.ColliderPosture.BlocksCharacters
                && entity.ColliderPosture.RadiusMillimeters > 0
                && entity.ColliderPosture.HeightMillimeters > 0;
        }

        private CharacterTestCharacterEntity FindAnimatedAppendageRosterEntity(int entityId)
        {
            for (int i = 0; i < _animatedAppendageRosterCount; i++)
            {
                if (_animatedAppendageRoster[i].Id.Value == entityId)
                    return _animatedAppendageRoster[i];
            }
            return null;
        }

        private static CombatCapsuleQuery CreateAnimatedAppendageQuery(
            CharacterTestCharacterEntity target,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            int sourceOrder,
            int skinMillimeters)
        {
            CharacterTestColliderPostureComponent posture = target.ColliderPosture;
            int centerY = target.Transform.YMillimeters
                + ResolveEffectiveCenterYMillimeters(posture);
            int halfSegment = Math.Max(0, posture.HeightMillimeters / 2 - posture.RadiusMillimeters);
            Fix64 x = CharacterTestDeterministicUnits.MillimetersToFix64Meters(target.Transform.XMillimeters);
            Fix64 z = CharacterTestDeterministicUnits.MillimetersToFix64Meters(target.Transform.ZMillimeters);
            var header = new CombatQueryHeader(
                target.Id.Value,
                CombatQueryKind.Capsule,
                physicsRuntime.GetEntityId(target),
                traceId: 0,
                actionId: 0,
                sourceOrder,
                CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer));
            return new CombatCapsuleQuery(
                header,
                new FixVector3(
                    x,
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(centerY - halfSegment),
                    z),
                new FixVector3(
                    x,
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(centerY + halfSegment),
                    z),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                    checked(posture.RadiusMillimeters + skinMillimeters)));
        }

        private static CombatPhysicsShape TranslateAnimatedAppendageShape(
            CombatPhysicsShape shape,
            FixVector3 root)
        {
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Sphere:
                    return CombatPhysicsShape.Sphere(root + shape.Center, shape.Radius);
                case CombatPhysicsShapeKind.Capsule:
                    return CombatPhysicsShape.Capsule(root + shape.PointA, root + shape.PointB, shape.Radius);
                case CombatPhysicsShapeKind.Obb:
                    return CombatPhysicsShape.Obb(
                        root + shape.Center,
                        shape.HalfExtents,
                        shape.AxisX,
                        shape.AxisY,
                        shape.AxisZ);
                default:
                    throw new InvalidOperationException("Animated appendage shape is unsupported.");
            }
        }

        private static FixVector3 ResolveAnimatedAppendageShapeCenter(CombatPhysicsShape shape)
        {
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Sphere:
                case CombatPhysicsShapeKind.Obb:
                    return shape.Center;
                case CombatPhysicsShapeKind.Capsule:
                    return (shape.PointA + shape.PointB) * Fix64.Half;
                default:
                    return FixVector3.Zero;
            }
        }

        private bool TryCreateAnimatedAppendageMotion(
            in CharacterTestAnimatedAppendageLeafWitness current,
            bool hasPrevious,
            in CharacterTestAnimatedAppendageLeafWitness previous,
            in CombatShapeSeparationResult separation,
            in BodyPresenceMotionLimits limits,
            out AnimatedAppendageConstraintMotion motion)
        {
            motion = default;
            try
            {
                bool clamped = false;
                RawVector2 surfaceVelocity = RawVector2.Zero;
                bool surfaceMotionEnabled = false;
                if (hasPrevious
                    && previous.Frame.Value + 1L == current.Frame.Value
                    && previous.BindingRevision == current.BindingRevision
                    && string.Equals(previous.BindingContentHash, current.BindingContentHash, StringComparison.Ordinal)
                    && previous.CombatColliderId == current.CombatColliderId
                    && previous.Shape.Kind == current.Shape.Kind
                    && previous.LogicalColliderId == current.LogicalColliderId
                    && previous.PartId == current.PartId)
                {
                    int legalRootDelta = checked((limits.MaximumComposedSpeedMillimetersPerSecond
                        + limits.TickRate - 1) / limits.TickRate);
                    int rootDeltaX = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                        current.OwnerRootWorld.X - previous.OwnerRootWorld.X);
                    int rootDeltaZ = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                        current.OwnerRootWorld.Z - previous.OwnerRootWorld.Z);
                    if (Math.Abs((long)rootDeltaX) <= legalRootDelta
                        && Math.Abs((long)rootDeltaZ) <= legalRootDelta)
                    {
                        FixVector3 previousCenter = previous.OwnerRootWorld
                            + ResolveAnimatedAppendageShapeCenter(previous.Shape);
                        FixVector3 currentCenter = current.OwnerRootWorld
                            + ResolveAnimatedAppendageShapeCenter(current.Shape);
                        long velocityX = checked((long)CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                            currentCenter.X - previousCenter.X) * limits.TickRate);
                        long velocityZ = checked((long)CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                            currentCenter.Z - previousCenter.Z) * limits.TickRate);
                        int surfaceX = ClampLongToIntRange(
                            velocityX,
                            -limits.MaximumComposedSpeedMillimetersPerSecond,
                            limits.MaximumComposedSpeedMillimetersPerSecond,
                            ref clamped);
                        int surfaceZ = ClampLongToIntRange(
                            velocityZ,
                            -limits.MaximumComposedSpeedMillimetersPerSecond,
                            limits.MaximumComposedSpeedMillimetersPerSecond,
                            ref clamped);
                        surfaceVelocity = ClampMagnitude(
                            new RawVector2(surfaceX, surfaceZ),
                            limits.MaximumComposedSpeedMillimetersPerSecond,
                            ref clamped);
                        surfaceMotionEnabled = true;
                    }
                }

                int penetrationMillimeters = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                    separation.Penetration);
                if (penetrationMillimeters < 0)
                    return false;
                int bias = ClampLongToIntRange(
                    checked((long)penetrationMillimeters * limits.TickRate),
                    0,
                    limits.MaximumComposedSpeedMillimetersPerSecond,
                    ref clamped);
                if (clamped)
                    _animatedAppendageClampCount++;
                motion = new AnimatedAppendageConstraintMotion(
                    surfaceVelocity,
                    surfaceMotionEnabled,
                    bias,
                    current.PressureScaleMilli,
                    limits.MaximumComposedSpeedMillimetersPerSecond);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private bool ApplyAnimatedAppendagePressure(AnimatedAppendageConstraint constraint)
        {
            if (constraint.Motion.MaximumComposedSpeedMillimetersPerSecond <= 0
                || !constraint.Target.ColliderPosture.CanBePushed)
            {
                return false;
            }

            int sourceAlong = constraint.Motion.SurfaceMotionEnabled
                ? RawVector2.DotScaled(constraint.Motion.SurfaceVelocity, constraint.HorizontalNormal)
                : 0;
            long drivingAlong = (long)sourceAlong
                + constraint.Motion.PenetrationBiasMillimetersPerSecond;
            _pressureVelocities.TryGetValue(constraint.Target.Id.Value, out RawVector2 targetVelocity);
            int targetAlong = RawVector2.DotScaled(targetVelocity, constraint.HorizontalNormal);
            long closingSpeed = drivingAlong - targetAlong;
            if (closingSpeed <= ContactVelocityEpsilonMillimetersPerSecond
                || !TryResolveContactTransferScale(
                    constraint.Owner,
                    constraint.Target,
                    out int massTransferMilli,
                    out int pushScaleMilli)
                || !TryScalePositive(closingSpeed, massTransferMilli, out long deltaAlong)
                || !TryScalePositive(deltaAlong, pushScaleMilli, out deltaAlong)
                || !TryScalePositive(deltaAlong, constraint.Motion.PressureScaleMilli, out deltaAlong))
            {
                return false;
            }
            if (deltaAlong > closingSpeed)
                deltaAlong = closingSpeed;
            if (deltaAlong <= ContactVelocityEpsilonMillimetersPerSecond || deltaAlong > int.MaxValue)
                return false;
            return ApplyBoundedPressureDelta(
                constraint.Target,
                targetVelocity,
                constraint.HorizontalNormal,
                (int)deltaAlong,
                constraint.Motion.MaximumComposedSpeedMillimetersPerSecond);
        }

        private static bool TryFindPreviousAnimatedAppendageWitness(
            CharacterTestAnimatedAppendageLeafWitness[] previousWitnesses,
            int previousWitnessCount,
            in CharacterTestAnimatedAppendageLeafWitness current,
            out CharacterTestAnimatedAppendageLeafWitness previous)
        {
            for (int i = 0; i < previousWitnessCount; i++)
            {
                CharacterTestAnimatedAppendageLeafWitness candidate = previousWitnesses[i];
                if (candidate.OwnerEntityId == current.OwnerEntityId
                    && candidate.CombatColliderId == current.CombatColliderId
                    && candidate.Shape.Kind == current.Shape.Kind
                    && candidate.LogicalColliderId == current.LogicalColliderId)
                {
                    previous = candidate;
                    return true;
                }
            }
            previous = default;
            return false;
        }

        private static bool IsCompatibleAnimatedAppendageMotionWitness(
            in CharacterTestAnimatedAppendageLeafWitness previous,
            in CharacterTestAnimatedAppendageLeafWitness current)
        {
            return previous.Frame.Value + 1L == current.Frame.Value
                && previous.BindingRevision == current.BindingRevision
                && string.Equals(previous.BindingContentHash, current.BindingContentHash, StringComparison.Ordinal)
                && previous.CombatColliderId == current.CombatColliderId
                && previous.Shape.Kind == current.Shape.Kind
                && previous.LogicalColliderId == current.LogicalColliderId
                && previous.PartId == current.PartId;
        }

        private int FindAnimatedAppendageConstraint(AnimatedAppendageContactKey key)
        {
            for (int i = 0; i < _animatedAppendageConstraintCount; i++)
            {
                if (_animatedAppendageConstraints[i].Key.Equals(key))
                    return i;
            }
            return -1;
        }

        private void SortAnimatedAppendageConstraints()
        {
            for (int i = 1; i < _animatedAppendageConstraintCount; i++)
            {
                AnimatedAppendageConstraint value = _animatedAppendageConstraints[i];
                int j = i - 1;
                while (j >= 0 && _animatedAppendageConstraints[j].Key.CompareTo(value.Key) > 0)
                {
                    _animatedAppendageConstraints[j + 1] = _animatedAppendageConstraints[j];
                    j--;
                }
                _animatedAppendageConstraints[j + 1] = value;
            }
        }

        private void DeriveAnimatedAppendagePresentationPhases(bool sourceReset)
        {
            _animatedAppendagePhaseFactCount = 0;
            if (_suppressAnimatedAppendagePresentationPhases
                || sourceReset
                || !_animatedAppendagePhaseBaselineInitialized)
            {
                CopyAnimatedAppendagePhaseBaseline();
                _animatedAppendagePhaseBaselineInitialized = true;
                _suppressAnimatedAppendagePresentationPhases = false;
                return;
            }

            int previousIndex = 0;
            int currentIndex = 0;
            while (previousIndex < _animatedAppendagePreviousPhaseKeyCount
                || currentIndex < _animatedAppendageConstraintCount)
            {
                if (previousIndex >= _animatedAppendagePreviousPhaseKeyCount)
                {
                    AddAnimatedAppendagePhase(
                        CharacterTestAnimatedAppendageSoftContactPhase.Started,
                        _animatedAppendageConstraints[currentIndex++].Key);
                    continue;
                }
                if (currentIndex >= _animatedAppendageConstraintCount)
                {
                    AddAnimatedAppendagePhase(
                        CharacterTestAnimatedAppendageSoftContactPhase.Ended,
                        _animatedAppendagePreviousPhaseKeys[previousIndex++]);
                    continue;
                }

                AnimatedAppendageContactKey previous = _animatedAppendagePreviousPhaseKeys[previousIndex];
                AnimatedAppendageContactKey current = _animatedAppendageConstraints[currentIndex].Key;
                int comparison = previous.CompareTo(current);
                if (comparison == 0)
                {
                    AddAnimatedAppendagePhase(
                        CharacterTestAnimatedAppendageSoftContactPhase.Stayed,
                        current);
                    previousIndex++;
                    currentIndex++;
                }
                else if (comparison < 0)
                {
                    AddAnimatedAppendagePhase(
                        CharacterTestAnimatedAppendageSoftContactPhase.Ended,
                        previous);
                    previousIndex++;
                }
                else
                {
                    AddAnimatedAppendagePhase(
                        CharacterTestAnimatedAppendageSoftContactPhase.Started,
                        current);
                    currentIndex++;
                }
            }
            CopyAnimatedAppendagePhaseBaseline();
        }

        private void AddAnimatedAppendagePhase(
            CharacterTestAnimatedAppendageSoftContactPhase phase,
            AnimatedAppendageContactKey key)
        {
            if (_animatedAppendagePhaseFactCount >= _animatedAppendagePhaseFacts.Length)
                return;
            _animatedAppendagePhaseFacts[_animatedAppendagePhaseFactCount++] =
                new CharacterTestAnimatedAppendageSoftContactPhaseFact(
                    phase,
                    key.SourceEntityId,
                    key.TargetEntityId,
                    key.LogicalColliderId,
                    key.LogicalColliderLabel,
                    key.CombatColliderId,
                    key.ShapeKind);
        }

        private void CopyAnimatedAppendagePhaseBaseline()
        {
            _animatedAppendagePreviousPhaseKeyCount = _animatedAppendageConstraintCount;
            for (int i = 0; i < _animatedAppendageConstraintCount; i++)
                _animatedAppendagePreviousPhaseKeys[i] = _animatedAppendageConstraints[i].Key;
            for (int i = _animatedAppendageConstraintCount;
                i < _animatedAppendagePreviousPhaseKeys.Length;
                i++)
            {
                _animatedAppendagePreviousPhaseKeys[i] = default;
            }
        }

        private void ResetAnimatedAppendageSoftContactBuild()
        {
            for (int i = 0; i < _animatedAppendageRosterCount; i++)
                _animatedAppendageRoster[i] = null;
            Array.Clear(_animatedAppendageConstraints, 0, _animatedAppendageConstraints.Length);
            Array.Clear(_animatedAppendagePhaseFacts, 0, _animatedAppendagePhaseFacts.Length);
            _animatedAppendageRosterCount = 0;
            _animatedAppendageConstraintCount = 0;
            _animatedAppendagePhaseFactCount = 0;
            _animatedAppendagePreviousWitnessCount = 0;
            _animatedAppendageCurrentWitnessCount = 0;
            _animatedAppendageClampCount = 0;
            _animatedAppendageBuildStatus =
                CharacterTestAnimatedAppendageSoftContactBuildStatus.NotRequested;
        }

        private void RejectAnimatedAppendageBuild()
        {
            Array.Clear(_animatedAppendageConstraints, 0, _animatedAppendageConstraintCount);
            _animatedAppendageConstraintCount = 0;
            _animatedAppendagePhaseFactCount = 0;
        }

        private readonly struct AnimatedAppendageConstraint
        {
            internal AnimatedAppendageConstraint(
                AnimatedAppendageContactKey key,
                CharacterTestCharacterEntity owner,
                CharacterTestCharacterEntity target,
                CombatShapeSeparationResult separation,
                RawVector2 horizontalNormal,
                AnimatedAppendageConstraintMotion motion)
            {
                Key = key;
                Owner = owner;
                Target = target;
                Separation = separation;
                HorizontalNormal = horizontalNormal;
                Motion = motion;
            }

            internal AnimatedAppendageContactKey Key { get; }
            internal CharacterTestCharacterEntity Owner { get; }
            internal CharacterTestCharacterEntity Target { get; }
            internal CombatShapeSeparationResult Separation { get; }
            internal RawVector2 HorizontalNormal { get; }
            internal AnimatedAppendageConstraintMotion Motion { get; }
        }

        private readonly struct AnimatedAppendageConstraintMotion
        {
            internal AnimatedAppendageConstraintMotion(
                RawVector2 surfaceVelocity,
                bool surfaceMotionEnabled,
                int penetrationBiasMillimetersPerSecond,
                int pressureScaleMilli,
                int maximumComposedSpeedMillimetersPerSecond)
            {
                SurfaceVelocity = surfaceVelocity;
                SurfaceMotionEnabled = surfaceMotionEnabled;
                PenetrationBiasMillimetersPerSecond = penetrationBiasMillimetersPerSecond;
                PressureScaleMilli = pressureScaleMilli;
                MaximumComposedSpeedMillimetersPerSecond = maximumComposedSpeedMillimetersPerSecond;
            }

            internal RawVector2 SurfaceVelocity { get; }
            internal bool SurfaceMotionEnabled { get; }
            internal int PenetrationBiasMillimetersPerSecond { get; }
            internal int PressureScaleMilli { get; }
            internal int MaximumComposedSpeedMillimetersPerSecond { get; }
        }

        private readonly struct AnimatedAppendageContactKey : IEquatable<AnimatedAppendageContactKey>
        {
            internal AnimatedAppendageContactKey(
                int sourceEntityId,
                int targetEntityId,
                int logicalColliderId,
                string logicalColliderLabel,
                int combatColliderId,
                CombatPhysicsShapeKind shapeKind)
            {
                SourceEntityId = sourceEntityId;
                TargetEntityId = targetEntityId;
                LogicalColliderId = logicalColliderId;
                LogicalColliderLabel = logicalColliderLabel ?? string.Empty;
                CombatColliderId = combatColliderId;
                ShapeKind = shapeKind;
            }

            internal int SourceEntityId { get; }
            internal int TargetEntityId { get; }
            internal int LogicalColliderId { get; }
            internal string LogicalColliderLabel { get; }
            internal int CombatColliderId { get; }
            internal CombatPhysicsShapeKind ShapeKind { get; }

            internal int CompareTo(AnimatedAppendageContactKey other)
            {
                int result = SourceEntityId.CompareTo(other.SourceEntityId);
                if (result != 0) return result;
                result = TargetEntityId.CompareTo(other.TargetEntityId);
                if (result != 0) return result;
                result = LogicalColliderId.CompareTo(other.LogicalColliderId);
                if (result != 0) return result;
                result = CombatColliderId.CompareTo(other.CombatColliderId);
                if (result != 0) return result;
                return ShapeKind.CompareTo(other.ShapeKind);
            }

            public bool Equals(AnimatedAppendageContactKey other)
            {
                return SourceEntityId == other.SourceEntityId
                    && TargetEntityId == other.TargetEntityId
                    && CombatColliderId == other.CombatColliderId
                    && ShapeKind == other.ShapeKind
                    && LogicalColliderId == other.LogicalColliderId;
            }
        }
    }
}
