using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestInteractionPhysicsSystem : ICharacterTestInteractionConsumer
    {
        public const string ConsumerIdValue = "interaction.physics";
        public static readonly CharacterActionId ExplosionImpulseSourceActionId = CharacterActionId.None;

        private const int QueryActionId = 0;
        private const int DirectionFallbackScale = CharacterTestDeterministicMath.TrigScale;
        private const int InitialDynamicFieldQueryBufferCapacity = 128;
        private const long DirectionalImpulseApplicationHistoryFrames = 180L;
        private static readonly ProfilerMarker TornadoIndexRebuildMarker =
            new ProfilerMarker("CharacterTest.InteractionPhysics.TornadoIndexRebuild");
        private static readonly ProfilerMarker TornadoFieldQueryMarker =
            new ProfilerMarker("CharacterTest.InteractionPhysics.TornadoFieldQuery");
        private static readonly ProfilerMarker FallbackFieldQueryMarker =
            new ProfilerMarker("CharacterTest.InteractionPhysics.FallbackFieldQuery");

        private readonly List<CombatQueryResult> _queryResults =
            new List<CombatQueryResult>(InitialDynamicFieldQueryBufferCapacity);
        private readonly HashSet<int> _processedBodyIds =
            new HashSet<int>(InitialDynamicFieldQueryBufferCapacity);
        private readonly CharacterTestDynamicFieldTargetIndex _dynamicFieldTargetIndex =
            new CharacterTestDynamicFieldTargetIndex();
        private readonly CharacterTestTeamRelationProvider _teamRelationProvider =
            new CharacterTestTeamRelationProvider();
        private readonly Dictionary<DirectionalImpulseApplicationKey, long> _directionalImpulseApplications =
            new Dictionary<DirectionalImpulseApplicationKey, long>();
        private readonly List<DirectionalImpulseApplicationKey> _directionalImpulseApplicationRemovals =
            new List<DirectionalImpulseApplicationKey>();
        private CharacterTestCharacterRuntimeWorld _indexedWorld;
        private CharacterTestCharacterPhysicsRuntime _indexedPhysicsRuntime;
        private long _indexedFrame = -1L;
        private int _indexedLayerMaskValue;
        private bool _hasDynamicFieldTargetIndex;
        private long _directionalImpulsePruneFrame = -1L;

        public CharacterTestInteractionPhysicsSummary LastSummary { get; private set; } =
            CharacterTestInteractionPhysicsSummary.Empty;

        public string ConsumerId => ConsumerIdValue;
        public int ConsumerOrder => 100;

        public void Consume(CharacterTestInteractionConsumerContext context)
        {
            Tick(
                context.World,
                context.PhysicsRuntime,
                context.Interactions,
                context.Frame,
                context.TickRate);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            IReadOnlyList<CharacterTestInteractionEvent> interactions,
            RuntimeFrame frame,
            int tickRate = CharacterTestDeterministicUnits.DefaultTickRate)
        {
            var state = new FieldProcessingState(frame);
            if (tickRate <= 0)
                tickRate = CharacterTestDeterministicUnits.DefaultTickRate;

            if (world == null
                || physicsRuntime == null
                || !physicsRuntime.IsReady
                || physicsRuntime.World == null
                || interactions == null
                || interactions.Count == 0)
            {
                LastSummary = state.ToSummary();
                return;
            }

            _teamRelationProvider.Configure(world, physicsRuntime);
            for (int i = 0; i < interactions.Count; i++)
            {
                CharacterTestInteractionEvent interaction = interactions[i];
                if (interaction.Kind != CharacterTestInteractionKind.PhysicalField)
                    continue;

                state.ProcessedFieldCount++;
                if (interaction.Radius <= Fix64.Zero
                    || !TryCreateLayerMask(interaction.PhysicalField, out CombatPhysicsLayerMask layerMask))
                {
                    state.SkippedTargetCount++;
                    continue;
                }

                QueryField(world, physicsRuntime, interaction, layerMask, frame);
                state.QueryCount++;
                state.QueryResultCount += _queryResults.Count;
                ProcessFieldResults(world, physicsRuntime, interaction, tickRate, ref state);
            }

            LastSummary = state.ToSummary();
        }

        private void QueryField(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestInteractionEvent interaction,
            CombatPhysicsLayerMask layerMask,
            RuntimeFrame frame)
        {
            _queryResults.Clear();
            _processedBodyIds.Clear();

            CharacterTestInteractionPhysicalField field = interaction.PhysicalField;
            var header = new CombatQueryHeader(
                interaction.TraceId,
                IsCapsuleFieldShape(field.Shape)
                    ? CombatQueryKind.Capsule
                    : CombatQueryKind.Sphere,
                ToCombatEntityId(interaction.SourceEntityId),
                interaction.TraceId,
                    QueryActionId,
                    interaction.SourceOrder,
                    layerMask);
            bool includeSource = !field.Targets(CharacterTestInteractionTargetFilter.ExcludeSource);

            if (IsCapsuleFieldShape(field.Shape))
            {
                ResolveCapsulePoints(interaction, field, out FixVector3 pointA, out FixVector3 pointB);
                if (CanUseTornadoDynamicFieldQuery(interaction, field))
                {
                    EnsureDynamicFieldTargetIndex(world, physicsRuntime, layerMask, frame);
                    using (TornadoFieldQueryMarker.Auto())
                    {
                        _dynamicFieldTargetIndex.QueryVerticalCapsule(
                            header,
                            pointA,
                            pointB,
                            interaction.Radius,
                            _queryResults,
                            includeSource);
                    }

                    return;
                }

                using (FallbackFieldQueryMarker.Auto())
                {
                    physicsRuntime.World.QueryCapsule(
                        new CombatCapsuleQuery(header, pointA, pointB, interaction.Radius),
                        _queryResults,
                        includeSource);
                }

                return;
            }

            using (FallbackFieldQueryMarker.Auto())
            {
                physicsRuntime.World.QuerySphere(
                    new CombatSphereQuery(header, interaction.AnchorPosition, interaction.Radius),
                    _queryResults,
                    includeSource);
            }
        }

        private void EnsureDynamicFieldTargetIndex(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CombatPhysicsLayerMask layerMask,
            RuntimeFrame frame)
        {
            if (_hasDynamicFieldTargetIndex
                && _indexedFrame == frame.Value
                && _indexedLayerMaskValue == layerMask.Value
                && ReferenceEquals(_indexedWorld, world)
                && ReferenceEquals(_indexedPhysicsRuntime, physicsRuntime))
            {
                EnsureDynamicFieldQueryBufferCapacity();
                return;
            }

            using (TornadoIndexRebuildMarker.Auto())
            {
                _dynamicFieldTargetIndex.Rebuild(world, physicsRuntime, layerMask);
            }

            _indexedWorld = world;
            _indexedPhysicsRuntime = physicsRuntime;
            _indexedFrame = frame.Value;
            _indexedLayerMaskValue = layerMask.Value;
            _hasDynamicFieldTargetIndex = true;
            EnsureDynamicFieldQueryBufferCapacity();
        }

        private void EnsureDynamicFieldQueryBufferCapacity()
        {
            int targetCount = _dynamicFieldTargetIndex.TargetCount;
            if (targetCount <= 0)
                return;

            _dynamicFieldTargetIndex.EnsureResultCapacity(_queryResults);
            _processedBodyIds.EnsureCapacity(targetCount);
        }

        private static bool CanUseTornadoDynamicFieldQuery(
            CharacterTestInteractionEvent interaction,
            CharacterTestInteractionPhysicalField field)
        {
            return field.Shape == CharacterTestInteractionFieldShape.Capsule
                && field.ForceMode == CharacterTestInteractionForceMode.AttractionAcceleration
                && (interaction.StableId == CharacterTestCharacterActionConfigSource.TornadoAttractionStableEventId)
                && interaction.SourceDomain == CharacterTestInteractionSourceDomains.CombatProjectileActiveField
                && (field.Targets(CharacterTestInteractionTargetFilter.DynamicCharacters)
                    || field.Targets(CharacterTestInteractionTargetFilter.DynamicMovableObjects));
        }

        private void ProcessFieldResults(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestInteractionEvent interaction,
            int tickRate,
            ref FieldProcessingState state)
        {
            if (_queryResults.Count == 0)
                return;

            int radiusMillimeters =
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(interaction.Radius);
            if (radiusMillimeters <= 0)
                return;

            for (int i = 0; i < _queryResults.Count; i++)
            {
                CombatQueryResult query = _queryResults[i];
                if (!_processedBodyIds.Add(query.TargetBodyId.Value))
                {
                    state.DuplicateBodySkipCount++;
                    state.SkippedTargetCount++;
                    continue;
                }

                if (!physicsRuntime.World.TryGetBody(query.TargetBodyId, out CombatPhysicsBody targetBody))
                {
                    state.SkippedTargetCount++;
                    continue;
                }

                if (TryProcessCharacterTarget(world, interaction, query, targetBody, radiusMillimeters, ref state))
                    continue;
                if (TryProcessMovableTarget(world, physicsRuntime, interaction, query, targetBody, radiusMillimeters, tickRate, ref state))
                    continue;

                state.SkippedTargetCount++;
            }
        }

        private bool TryProcessCharacterTarget(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestInteractionEvent interaction,
            CombatQueryResult query,
            CombatPhysicsBody targetBody,
            int radiusMillimeters,
            ref FieldProcessingState state)
        {
            CharacterTestInteractionPhysicalField field = interaction.PhysicalField;
            if (!field.Targets(CharacterTestInteractionTargetFilter.DynamicCharacters)
                || !TryResolveCharacterEntityId(query.TargetBodyId, out int targetEntityId))
            {
                return false;
            }

            CharacterTestCharacterEntity target = FindEntity(world, targetEntityId);
            if (target == null
                || !target.BattleState.CanBeTargeted
                || (field.Targets(CharacterTestInteractionTargetFilter.ExcludeSource)
                    && target.Id.Value == interaction.SourceEntityId)
                || (field.Targets(CharacterTestInteractionTargetFilter.RequireHostile)
                    && !_teamRelationProvider.AreHostile(ToCombatEntityId(interaction.SourceEntityId), query.TargetEntityId)))
            {
                state.SkippedTargetCount++;
                return true;
            }

            switch (field.ForceMode)
            {
                case CharacterTestInteractionForceMode.RadialImpulse:
                    return ApplyRadialImpulseToCharacter(interaction, target, targetBody, radiusMillimeters, ref state);
                case CharacterTestInteractionForceMode.AttractionAcceleration:
                    return ApplyAttractionAccelerationToCharacter(interaction, target, targetBody, radiusMillimeters, ref state);
                default:
                    state.SkippedTargetCount++;
                    return true;
            }
        }

        private bool ApplyRadialImpulseToCharacter(
            CharacterTestInteractionEvent interaction,
            CharacterTestCharacterEntity target,
            CombatPhysicsBody targetBody,
            int radiusMillimeters,
            ref FieldProcessingState state)
        {
            if (!target.ColliderPosture.CanBePushed)
            {
                state.SkippedTargetCount++;
                return true;
            }

            if (!ResolveHorizontalDirection(
                    targetBody.Position - interaction.AnchorPosition,
                    interaction.Direction,
                    out int directionX,
                    out int directionZ,
                    out int horizontalDistance)
                || horizontalDistance >= radiusMillimeters)
            {
                state.SkippedTargetCount++;
                return true;
            }

            CharacterTestInteractionPhysicalField field = interaction.PhysicalField;
            int speed = ResolveRadialImpulseSpeed(field, horizontalDistance, radiusMillimeters);
            if (field.UsePushScale)
            {
                speed = CharacterTestDeterministicMath.MulDivRoundToInt(
                    speed,
                    target.Physics.EffectivePushScaleMilli,
                    CharacterTestDeterministicUnits.ScaleFactorScale);
            }

            speed = ClampSpeed(speed, field.MaxSpeedMillimetersPerSecond);
            if (speed <= 0)
            {
                state.SkippedTargetCount++;
                return true;
            }

            int impulseX = CharacterTestDeterministicMath.MulDivRoundToInt(
                directionX,
                speed,
                DirectionFallbackScale);
            int impulseZ = CharacterTestDeterministicMath.MulDivRoundToInt(
                directionZ,
                speed,
                DirectionFallbackScale);
            target.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ImpulseRaw(
                impulseX,
                0,
                impulseZ,
                CharacterMovementMode.Idle,
                ResolveMotionSourceActionId(interaction),
                interaction.StableId));

            state.CharacterTargetCount++;
            state.AppliedForceCount++;
            state.LastTargetEntityId = target.Id.Value;
            state.LastSpeedMillimetersPerSecond = speed;
            state.LastStableId = interaction.StableId;
            state.LastForceMode = field.ForceMode;
            state.LastFieldShape = field.Shape;
            return true;
        }

        private bool ApplyAttractionAccelerationToCharacter(
            CharacterTestInteractionEvent interaction,
            CharacterTestCharacterEntity target,
            CombatPhysicsBody targetBody,
            int radiusMillimeters,
            ref FieldProcessingState state)
        {
            if (!ResolveHorizontalDirection(
                    interaction.AnchorPosition - targetBody.Position,
                    interaction.Direction,
                    out int directionX,
                    out int directionZ,
                    out int horizontalDistance)
                || horizontalDistance > radiusMillimeters)
            {
                state.SkippedTargetCount++;
                return true;
            }

            CharacterTestInteractionPhysicalField field = interaction.PhysicalField;
            int massScaleMilli = ResolveMassScaleMilli(field, target.Physics.EffectiveMassGrams);
            int distanceScaleMilli = ResolveLinearFalloffScale(field, horizontalDistance, radiusMillimeters);
            int radialVelocityMillimetersPerSecond =
                ResolveRadialVelocityMillimetersPerSecond(
                    target.HorizontalVelocityXMillimetersPerSecond,
                    target.HorizontalVelocityYMillimetersPerSecond,
                    directionX,
                    directionZ);
            int terminalScaleMilli = ResolveTerminalScaleMilli(field, radialVelocityMillimetersPerSecond);
            int accelerationMagnitude = ResolveAccelerationMagnitude(
                field.AccelerationMillimetersPerSecondSquared,
                massScaleMilli,
                distanceScaleMilli,
                terminalScaleMilli);
            int accelerationX = CharacterTestDeterministicMath.MulDivRoundToInt(
                directionX,
                accelerationMagnitude,
                DirectionFallbackScale);
            int accelerationZ = CharacterTestDeterministicMath.MulDivRoundToInt(
                directionZ,
                accelerationMagnitude,
                DirectionFallbackScale);
            if (accelerationX == 0 && accelerationZ == 0)
            {
                state.SkippedTargetCount++;
                return true;
            }

            target.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ExternalForceRaw(
                accelerationX,
                0,
                accelerationZ,
                CharacterTestCharacterMotionRequestSpace.World,
                ResolveMotionSourceActionId(interaction),
                interaction.StableId,
                bypassGroundDamping: true));

            state.CharacterTargetCount++;
            state.AppliedForceCount++;
            state.LastTargetEntityId = target.Id.Value;
            state.LastAccelerationMillimetersPerSecondSquared = accelerationMagnitude;
            state.LastStableId = interaction.StableId;
            state.LastForceMode = field.ForceMode;
            state.LastFieldShape = field.Shape;
            return true;
        }

        private bool TryProcessMovableTarget(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestInteractionEvent interaction,
            CombatQueryResult query,
            CombatPhysicsBody targetBody,
            int radiusMillimeters,
            int tickRate,
            ref FieldProcessingState state)
        {
            CharacterTestInteractionPhysicalField field = interaction.PhysicalField;
            if (!field.Targets(CharacterTestInteractionTargetFilter.DynamicMovableObjects)
                || !physicsRuntime.TryResolveMovableObjectNumericId(query.TargetBodyId, out int movableNumericId)
                || !world.TryFindMovableNaturalObject(
                    movableNumericId,
                    out CharacterTestMovableNaturalObjectState movable))
            {
                return false;
            }

            switch (field.ForceMode)
            {
                case CharacterTestInteractionForceMode.RadialImpulse:
                    return ApplyRadialImpulseToMovable(interaction, movable, targetBody, radiusMillimeters, ref state);
                case CharacterTestInteractionForceMode.AttractionAcceleration:
                    return ApplyAttractionAccelerationToMovable(interaction, movable, targetBody, radiusMillimeters, tickRate, ref state);
                case CharacterTestInteractionForceMode.DirectionalImpulse:
                    if (!TryReserveDirectionalImpulseApplication(interaction, movable.NumericId, state.Frame.Value))
                    {
                        state.SkippedTargetCount++;
                        return true;
                    }

                    return ApplyDirectionalImpulseToMovable(interaction, movable, ref state);
                default:
                    state.SkippedTargetCount++;
                    return true;
            }
        }

        private bool ApplyRadialImpulseToMovable(
            CharacterTestInteractionEvent interaction,
            CharacterTestMovableNaturalObjectState movable,
            CombatPhysicsBody targetBody,
            int radiusMillimeters,
            ref FieldProcessingState state)
        {
            if (!ResolveHorizontalDirection(
                    targetBody.Position - interaction.AnchorPosition,
                    interaction.Direction,
                    out int directionX,
                    out int directionZ,
                    out int horizontalDistance)
                || horizontalDistance >= radiusMillimeters)
            {
                state.SkippedTargetCount++;
                return true;
            }

            CharacterTestInteractionPhysicalField field = interaction.PhysicalField;
            int speed = ResolveRadialImpulseSpeed(field, horizontalDistance, radiusMillimeters);
            speed = ApplyMovableScales(field, movable, speed);
            speed = ClampSpeed(speed, field.MaxSpeedMillimetersPerSecond);
            if (speed <= 0)
            {
                state.SkippedTargetCount++;
                return true;
            }

            int velocityX = CharacterTestDeterministicMath.MulDivRoundToInt(
                directionX,
                speed,
                DirectionFallbackScale);
            int velocityZ = CharacterTestDeterministicMath.MulDivRoundToInt(
                directionZ,
                speed,
                DirectionFallbackScale);
            movable.AccumulateInteractionFieldVelocity(
                interaction.StableId,
                velocityX,
                velocityZ,
                ResolvePressure(speed, movable.MassGrams));

            state.MovableTargetCount++;
            state.AppliedForceCount++;
            state.LastMovableNumericId = movable.NumericId;
            state.LastSpeedMillimetersPerSecond = speed;
            state.LastStableId = interaction.StableId;
            state.LastForceMode = field.ForceMode;
            state.LastFieldShape = field.Shape;
            return true;
        }

        private bool ApplyAttractionAccelerationToMovable(
            CharacterTestInteractionEvent interaction,
            CharacterTestMovableNaturalObjectState movable,
            CombatPhysicsBody targetBody,
            int radiusMillimeters,
            int tickRate,
            ref FieldProcessingState state)
        {
            if (!ResolveHorizontalDirection(
                    interaction.AnchorPosition - targetBody.Position,
                    interaction.Direction,
                    out int directionX,
                    out int directionZ,
                    out int horizontalDistance)
                || horizontalDistance > radiusMillimeters)
            {
                state.SkippedTargetCount++;
                return true;
            }

            CharacterTestInteractionPhysicalField field = interaction.PhysicalField;
            int massScaleMilli = ResolveMassScaleMilli(field, movable.MassGrams);
            int distanceScaleMilli = ResolveLinearFalloffScale(field, horizontalDistance, radiusMillimeters);
            int radialVelocityMillimetersPerSecond =
                ResolveRadialVelocityMillimetersPerSecond(
                    movable.VelocityXMillimetersPerSecond,
                    movable.VelocityZMillimetersPerSecond,
                    directionX,
                    directionZ);
            int terminalScaleMilli = ResolveTerminalScaleMilli(field, radialVelocityMillimetersPerSecond);
            int accelerationMagnitude = ResolveAccelerationMagnitude(
                field.AccelerationMillimetersPerSecondSquared,
                massScaleMilli,
                distanceScaleMilli,
                terminalScaleMilli);
            int accelerationX = CharacterTestDeterministicMath.MulDivRoundToInt(
                directionX,
                accelerationMagnitude,
                DirectionFallbackScale);
            int accelerationZ = CharacterTestDeterministicMath.MulDivRoundToInt(
                directionZ,
                accelerationMagnitude,
                DirectionFallbackScale);
            int velocityX = CharacterTestDeterministicMath.DivideRoundToInt(accelerationX, tickRate);
            int velocityZ = CharacterTestDeterministicMath.DivideRoundToInt(accelerationZ, tickRate);
            if (velocityX == 0 && velocityZ == 0)
            {
                state.SkippedTargetCount++;
                return true;
            }

            movable.AccumulateInteractionFieldVelocity(
                interaction.StableId,
                velocityX,
                velocityZ,
                ResolvePressure(CharacterTestDeterministicMath.Magnitude(velocityX, velocityZ), movable.MassGrams));

            state.MovableTargetCount++;
            state.AppliedForceCount++;
            state.LastMovableNumericId = movable.NumericId;
            state.LastAccelerationMillimetersPerSecondSquared = accelerationMagnitude;
            state.LastStableId = interaction.StableId;
            state.LastForceMode = field.ForceMode;
            state.LastFieldShape = field.Shape;
            return true;
        }

        private bool ApplyDirectionalImpulseToMovable(
            CharacterTestInteractionEvent interaction,
            CharacterTestMovableNaturalObjectState movable,
            ref FieldProcessingState state)
        {
            if (!ResolveHorizontalDirection(
                    interaction.Direction,
                    interaction.Direction,
                    out int directionX,
                    out int directionZ,
                    out _))
            {
                state.SkippedTargetCount++;
                return true;
            }

            CharacterTestInteractionPhysicalField field = interaction.PhysicalField;
            int speed = ApplyMovableScales(field, movable, field.ImpulseSpeedMillimetersPerSecond);
            speed = ClampSpeed(speed, field.MaxSpeedMillimetersPerSecond);
            if (speed <= 0)
            {
                state.SkippedTargetCount++;
                return true;
            }

            int velocityX = CharacterTestDeterministicMath.MulDivRoundToInt(
                directionX,
                speed,
                DirectionFallbackScale);
            int velocityZ = CharacterTestDeterministicMath.MulDivRoundToInt(
                directionZ,
                speed,
                DirectionFallbackScale);
            if (velocityX == 0 && velocityZ == 0)
            {
                state.SkippedTargetCount++;
                return true;
            }

            movable.AccumulateInteractionFieldVelocity(
                interaction.StableId,
                velocityX,
                velocityZ,
                ResolveDirectionalImpulsePressure(velocityX, velocityZ, movable.MassGrams));

            state.MovableTargetCount++;
            state.AppliedForceCount++;
            state.LastMovableNumericId = movable.NumericId;
            state.LastSpeedMillimetersPerSecond = speed;
            state.LastStableId = interaction.StableId;
            state.LastForceMode = field.ForceMode;
            state.LastFieldShape = field.Shape;
            return true;
        }

        private static bool TryCreateLayerMask(
            CharacterTestInteractionPhysicalField field,
            out CombatPhysicsLayerMask layerMask)
        {
            layerMask = CombatPhysicsLayerMask.None;
            if (field.Targets(CharacterTestInteractionTargetFilter.DynamicCharacters))
                layerMask = layerMask.AddLayer(CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer);
            if (field.Targets(CharacterTestInteractionTargetFilter.DynamicMovableObjects))
                layerMask = layerMask.AddLayer(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer);
            return !layerMask.Equals(CombatPhysicsLayerMask.None);
        }

        private static bool IsCapsuleFieldShape(CharacterTestInteractionFieldShape shape)
        {
            return shape == CharacterTestInteractionFieldShape.Capsule
                || shape == CharacterTestInteractionFieldShape.OrientedCapsule;
        }

        private static void ResolveCapsulePoints(
            CharacterTestInteractionEvent interaction,
            CharacterTestInteractionPhysicalField field,
            out FixVector3 pointA,
            out FixVector3 pointB)
        {
            if (field.Shape == CharacterTestInteractionFieldShape.OrientedCapsule)
            {
                FixVector3 axis = interaction.Direction.TryNormalize(out FixVector3 normalized)
                    ? normalized
                    : new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
                pointA = interaction.AnchorPosition + axis * field.CapsuleBottomOffset;
                pointB = interaction.AnchorPosition + axis * field.CapsuleTopOffset;
                return;
            }

            pointA = interaction.AnchorPosition
                + new FixVector3(Fix64.Zero, field.CapsuleBottomOffset, Fix64.Zero);
            pointB = interaction.AnchorPosition
                + new FixVector3(Fix64.Zero, field.CapsuleTopOffset, Fix64.Zero);
        }

        private bool TryReserveDirectionalImpulseApplication(
            CharacterTestInteractionEvent interaction,
            int movableNumericId,
            long frame)
        {
            PruneDirectionalImpulseApplications(frame);
            var key = new DirectionalImpulseApplicationKey(
                interaction.TraceId,
                interaction.SourceOrder,
                interaction.SourceEntityId,
                movableNumericId);
            if (_directionalImpulseApplications.ContainsKey(key))
                return false;

            _directionalImpulseApplications.Add(key, frame);
            return true;
        }

        private void PruneDirectionalImpulseApplications(long frame)
        {
            if (_directionalImpulsePruneFrame == frame)
                return;

            _directionalImpulsePruneFrame = frame;
            long oldestFrame = frame - DirectionalImpulseApplicationHistoryFrames;
            _directionalImpulseApplicationRemovals.Clear();
            foreach (KeyValuePair<DirectionalImpulseApplicationKey, long> entry in _directionalImpulseApplications)
            {
                if (entry.Value < oldestFrame)
                    _directionalImpulseApplicationRemovals.Add(entry.Key);
            }

            for (int i = 0; i < _directionalImpulseApplicationRemovals.Count; i++)
                _directionalImpulseApplications.Remove(_directionalImpulseApplicationRemovals[i]);
            _directionalImpulseApplicationRemovals.Clear();
        }

        private static int ResolveRadialImpulseSpeed(
            CharacterTestInteractionPhysicalField field,
            int horizontalDistance,
            int radiusMillimeters)
        {
            int falloffScaleMilli = ResolveLinearFalloffScale(field, horizontalDistance, radiusMillimeters);
            return CharacterTestDeterministicMath.MulDivRoundToInt(
                field.ImpulseSpeedMillimetersPerSecond,
                falloffScaleMilli,
                CharacterTestDeterministicUnits.ScaleFactorScale);
        }

        private static int ResolveLinearFalloffScale(
            CharacterTestInteractionPhysicalField field,
            int horizontalDistance,
            int radiusMillimeters)
        {
            if (radiusMillimeters <= 0)
                return 0;

            int minimumScale = CharacterTestDeterministicMath.Clamp(
                field.LinearFalloffMinimumScaleMilli,
                0,
                CharacterTestDeterministicUnits.ScaleFactorScale);
            int clampedDistance = CharacterTestDeterministicMath.Clamp(horizontalDistance, 0, radiusMillimeters);
            int range = CharacterTestDeterministicUnits.ScaleFactorScale - minimumScale;
            int falloff = CharacterTestDeterministicMath.DivideRoundToInt(
                (long)(radiusMillimeters - clampedDistance) * range,
                radiusMillimeters);
            return minimumScale + falloff;
        }

        private static int ResolveMassScaleMilli(
            CharacterTestInteractionPhysicalField field,
            int targetMassGrams)
        {
            if (!field.UseMassScale)
                return CharacterTestDeterministicUnits.ScaleFactorScale;

            int referenceMass = Math.Max(
                CharacterTestCharacterPhysicsComponent.MinimumMassGrams,
                field.ReferenceMassGrams > 0
                    ? field.ReferenceMassGrams
                    : CharacterTestCharacterPhysicsComponent.DefaultMassGrams);
            int safeMass = Math.Max(CharacterTestCharacterPhysicsComponent.MinimumMassGrams, targetMassGrams);
            int scale = CharacterTestDeterministicMath.DivideRoundToInt(
                (long)referenceMass * CharacterTestDeterministicUnits.ScaleFactorScale,
                safeMass);
            int minScale = field.MinimumMassScaleMilli > 0
                ? field.MinimumMassScaleMilli
                : CharacterTestDeterministicUnits.ScaleFactorScale;
            int maxScale = field.MaximumMassScaleMilli > 0
                ? field.MaximumMassScaleMilli
                : CharacterTestDeterministicUnits.ScaleFactorScale;
            return CharacterTestDeterministicMath.Clamp(scale, minScale, Math.Max(minScale, maxScale));
        }

        private static int ResolveTerminalScaleMilli(
            CharacterTestInteractionPhysicalField field,
            int radialVelocityMillimetersPerSecond)
        {
            int terminalSpeed = Math.Max(1, field.MaxSpeedMillimetersPerSecond);
            if (radialVelocityMillimetersPerSecond <= 0)
                return CharacterTestDeterministicUnits.ScaleFactorScale;

            int remainingSpeed = Math.Max(0, terminalSpeed - radialVelocityMillimetersPerSecond);
            return CharacterTestDeterministicMath.DivideRoundToInt(
                (long)remainingSpeed * CharacterTestDeterministicUnits.ScaleFactorScale,
                terminalSpeed);
        }

        private static int ResolveAccelerationMagnitude(
            int baseAccelerationMillimetersPerSecondSquared,
            int massScaleMilli,
            int distanceScaleMilli,
            int terminalScaleMilli)
        {
            return CharacterTestDeterministicMath.DivideRoundToInt(
                (long)Math.Max(0, baseAccelerationMillimetersPerSecondSquared)
                * massScaleMilli
                * distanceScaleMilli
                * terminalScaleMilli,
                (long)CharacterTestDeterministicUnits.ScaleFactorScale
                * CharacterTestDeterministicUnits.ScaleFactorScale
                * CharacterTestDeterministicUnits.ScaleFactorScale);
        }

        private static int ApplyMovableScales(
            CharacterTestInteractionPhysicalField field,
            CharacterTestMovableNaturalObjectState movable,
            int speed)
        {
            if (field.UseMassScale)
            {
                speed = CharacterTestDeterministicMath.MulDivRoundToInt(
                    speed,
                    ResolveMassScaleMilli(field, movable.MassGrams),
                    CharacterTestDeterministicUnits.ScaleFactorScale);
            }

            if (field.UseMaterialPushScale)
            {
                speed = CharacterTestDeterministicMath.MulDivRoundToInt(
                    speed,
                    CharacterTestMovableNaturalObjectState.ResolveMaterialPushScaleMilli(movable.Material),
                    CharacterTestDeterministicUnits.ScaleFactorScale);
            }

            return speed;
        }

        private static int ClampSpeed(int speed, int maxSpeed)
        {
            if (maxSpeed <= 0)
                return Math.Max(0, speed);

            return CharacterTestDeterministicMath.Clamp(speed, 0, maxSpeed);
        }

        private static int ResolveRadialVelocityMillimetersPerSecond(
            int velocityXMillimetersPerSecond,
            int velocityZMillimetersPerSecond,
            int directionX,
            int directionZ)
        {
            return CharacterTestDeterministicMath.DivideRoundToInt(
                (long)velocityXMillimetersPerSecond * directionX
                + (long)velocityZMillimetersPerSecond * directionZ,
                DirectionFallbackScale);
        }

        private static bool ResolveHorizontalDirection(
            FixVector3 horizontalDelta,
            FixVector3 fallbackDirection,
            out int directionX,
            out int directionZ,
            out int horizontalDistance)
        {
            int deltaX = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(horizontalDelta.X);
            int deltaZ = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(horizontalDelta.Z);
            horizontalDistance = CharacterTestDeterministicMath.Magnitude(deltaX, deltaZ);
            CharacterTestDeterministicMath.NormalizeToScale(
                deltaX,
                deltaZ,
                DirectionFallbackScale,
                out directionX,
                out directionZ);
            if (directionX != 0 || directionZ != 0)
                return true;

            int eventX = FixDirectionToScaled(fallbackDirection.X);
            int eventZ = FixDirectionToScaled(fallbackDirection.Z);
            CharacterTestDeterministicMath.NormalizeToScale(
                eventX,
                eventZ,
                DirectionFallbackScale,
                out directionX,
                out directionZ);
            if (directionX != 0 || directionZ != 0)
                return true;

            directionX = DirectionFallbackScale;
            directionZ = 0;
            return true;
        }

        private static int ResolvePressure(int speedMillimetersPerSecond, int massGrams)
        {
            return CharacterTestDeterministicMath.DivideRoundToInt(
                (long)Math.Max(0, speedMillimetersPerSecond)
                * Math.Max(CharacterTestCharacterPhysicsComponent.MinimumMassGrams, massGrams),
                CharacterTestDeterministicUnits.PositionScale);
        }

        private static int ResolveDirectionalImpulsePressure(
            int velocityXMillimetersPerSecond,
            int velocityZMillimetersPerSecond,
            int massGrams)
        {
            int pressure = ResolvePressure(
                CharacterTestDeterministicMath.Magnitude(
                    velocityXMillimetersPerSecond,
                    velocityZMillimetersPerSecond),
                massGrams);
            return pressure > 0 ? pressure : 1;
        }

        private static int FixDirectionToScaled(Fix64 value)
        {
            return CharacterTestDeterministicMath.DivideRoundToInt(
                value.RawValue * DirectionFallbackScale,
                Fix64.Scale);
        }

        private static CharacterActionId ResolveMotionSourceActionId(CharacterTestInteractionEvent interaction)
        {
            if (interaction.PhysicalField.ForceMode == CharacterTestInteractionForceMode.RadialImpulse)
                return CharacterActionId.None;
            if (interaction.PhysicalField.ForceMode == CharacterTestInteractionForceMode.DirectionalImpulse
                && (interaction.StableId == CharacterTestCharacterActionConfigSource.SwordWaveProjectileImpactStableEventId))
            {
                return CharacterTestCharacterActionConfigSource.SwordWaveActionId;
            }

            if ((interaction.StableId == CharacterTestCharacterActionConfigSource.TornadoAttractionStableEventId))
            {
                return CharacterTestCharacterActionConfigSource.TornadoActionId;
            }

            return CharacterActionId.None;
        }

        private static CombatEntityId ToCombatEntityId(int characterEntityId)
        {
            return characterEntityId <= 0
                ? CombatEntityId.None
                : new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + characterEntityId);
        }

        private static bool TryResolveCharacterEntityId(CombatBodyId bodyId, out int entityId)
        {
            entityId = 0;
            if (bodyId.Value <= CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart
                || bodyId.Value >= CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectBodyIdStart)
            {
                return false;
            }

            entityId = bodyId.Value - CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart;
            return entityId > 0;
        }

        private static CharacterTestCharacterEntity FindEntity(
            CharacterTestCharacterRuntimeWorld world,
            int entityId)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id.Value == entityId)
                    return entities[i];
            }

            return null;
        }

        private readonly struct DirectionalImpulseApplicationKey :
            IEquatable<DirectionalImpulseApplicationKey>
        {
            public DirectionalImpulseApplicationKey(
                int traceId,
                int sourceOrder,
                int sourceEntityId,
                int movableNumericId)
            {
                TraceId = traceId < 0 ? 0 : traceId;
                SourceOrder = sourceOrder < 0 ? 0 : sourceOrder;
                SourceEntityId = sourceEntityId < 0 ? 0 : sourceEntityId;
                MovableNumericId = movableNumericId < 0 ? 0 : movableNumericId;
            }

            public int TraceId { get; }
            public int SourceOrder { get; }
            public int SourceEntityId { get; }
            public int MovableNumericId { get; }

            public bool Equals(DirectionalImpulseApplicationKey other)
            {
                return TraceId == other.TraceId
                    && SourceOrder == other.SourceOrder
                    && SourceEntityId == other.SourceEntityId
                    && MovableNumericId == other.MovableNumericId;
            }

            public override bool Equals(object obj)
            {
                return obj is DirectionalImpulseApplicationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = TraceId;
                    hash = (hash * 397) ^ SourceOrder;
                    hash = (hash * 397) ^ SourceEntityId;
                    hash = (hash * 397) ^ MovableNumericId;
                    return hash;
                }
            }
        }

        private struct FieldProcessingState
        {
            public FieldProcessingState(RuntimeFrame frame)
            {
                Frame = frame;
                ProcessedFieldCount = 0;
                AppliedForceCount = 0;
                CharacterTargetCount = 0;
                MovableTargetCount = 0;
                SkippedTargetCount = 0;
                LastTargetEntityId = 0;
                LastMovableNumericId = 0;
                LastSpeedMillimetersPerSecond = 0;
                LastAccelerationMillimetersPerSecondSquared = 0;
                LastStableId = 0;
                LastForceMode = 0;
                LastFieldShape = 0;
                QueryCount = 0;
                QueryResultCount = 0;
                DuplicateBodySkipCount = 0;
            }

            public RuntimeFrame Frame { get; }
            public int ProcessedFieldCount;
            public int AppliedForceCount;
            public int CharacterTargetCount;
            public int MovableTargetCount;
            public int SkippedTargetCount;
            public int LastTargetEntityId;
            public int LastMovableNumericId;
            public int LastSpeedMillimetersPerSecond;
            public int LastAccelerationMillimetersPerSecondSquared;
            public int LastStableId;
            public CharacterTestInteractionForceMode LastForceMode;
            public CharacterTestInteractionFieldShape LastFieldShape;
            public int QueryCount;
            public int QueryResultCount;
            public int DuplicateBodySkipCount;

            public CharacterTestInteractionPhysicsSummary ToSummary()
            {
                return new CharacterTestInteractionPhysicsSummary(
                    Frame,
                    ProcessedFieldCount,
                    AppliedForceCount,
                    CharacterTargetCount,
                    MovableTargetCount,
                    SkippedTargetCount,
                    LastTargetEntityId,
                    LastMovableNumericId,
                    LastSpeedMillimetersPerSecond,
                    LastAccelerationMillimetersPerSecondSquared,
                    LastStableId,
                    LastForceMode,
                    LastFieldShape,
                    QueryCount,
                    QueryResultCount,
                    DuplicateBodySkipCount);
            }
        }
    }

    public readonly struct CharacterTestInteractionPhysicsSummary
    {
        public static readonly CharacterTestInteractionPhysicsSummary Empty =
            new CharacterTestInteractionPhysicsSummary(
                RuntimeFrame.Zero,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0);

        public CharacterTestInteractionPhysicsSummary(
            RuntimeFrame frame,
            int processedFieldCount,
            int appliedForceCount,
            int characterTargetCount,
            int movableTargetCount,
            int skippedTargetCount,
            int lastTargetEntityId,
            int lastMovableNumericId,
            int lastSpeedMillimetersPerSecond,
            int lastAccelerationMillimetersPerSecondSquared,
            int lastStableId,
            CharacterTestInteractionForceMode lastForceMode,
            CharacterTestInteractionFieldShape lastFieldShape,
            int queryCount = 0,
            int queryResultCount = 0,
            int duplicateBodySkipCount = 0)
        {
            Frame = frame;
            ProcessedFieldCount = processedFieldCount < 0 ? 0 : processedFieldCount;
            AppliedForceCount = appliedForceCount < 0 ? 0 : appliedForceCount;
            CharacterTargetCount = characterTargetCount < 0 ? 0 : characterTargetCount;
            MovableTargetCount = movableTargetCount < 0 ? 0 : movableTargetCount;
            SkippedTargetCount = skippedTargetCount < 0 ? 0 : skippedTargetCount;
            LastTargetEntityId = lastTargetEntityId < 0 ? 0 : lastTargetEntityId;
            LastMovableNumericId = lastMovableNumericId < 0 ? 0 : lastMovableNumericId;
            LastSpeedMillimetersPerSecond =
                lastSpeedMillimetersPerSecond < 0 ? 0 : lastSpeedMillimetersPerSecond;
            LastAccelerationMillimetersPerSecondSquared =
                lastAccelerationMillimetersPerSecondSquared < 0
                    ? 0
                    : lastAccelerationMillimetersPerSecondSquared;
            LastStableId = lastStableId;
            LastForceMode = lastForceMode;
            LastFieldShape = lastFieldShape;
            QueryCount = queryCount < 0 ? 0 : queryCount;
            QueryResultCount = queryResultCount < 0 ? 0 : queryResultCount;
            DuplicateBodySkipCount = duplicateBodySkipCount < 0 ? 0 : duplicateBodySkipCount;
        }

        public RuntimeFrame Frame { get; }
        public int ProcessedFieldCount { get; }
        public int ProcessedEventCount => ProcessedFieldCount;
        public int AppliedForceCount { get; }
        public int AppliedImpulseCount => AppliedForceCount;
        public int CharacterTargetCount { get; }
        public int MovableTargetCount { get; }
        public int SkippedTargetCount { get; }
        public int LastTargetEntityId { get; }
        public int LastMovableNumericId { get; }
        public int LastSpeedMillimetersPerSecond { get; }
        public int LastAccelerationMillimetersPerSecondSquared { get; }
        public int LastStableId { get; }
        public CharacterTestInteractionForceMode LastForceMode { get; }
        public CharacterTestInteractionFieldShape LastFieldShape { get; }
        public int QueryCount { get; }
        public int QueryResultCount { get; }
        public int DuplicateBodySkipCount { get; }

        public string ToSummaryLine()
        {
            return "frame=" + Frame.Value.ToString(CultureInfo.InvariantCulture)
                + " fields=" + ProcessedFieldCount.ToString(CultureInfo.InvariantCulture)
                + " queries=" + QueryCount.ToString(CultureInfo.InvariantCulture)
                + " queryResults=" + QueryResultCount.ToString(CultureInfo.InvariantCulture)
                + " duplicateBodySkips=" + DuplicateBodySkipCount.ToString(CultureInfo.InvariantCulture)
                + " applied=" + AppliedForceCount.ToString(CultureInfo.InvariantCulture)
                + " characters=" + CharacterTargetCount.ToString(CultureInfo.InvariantCulture)
                + " movables=" + MovableTargetCount.ToString(CultureInfo.InvariantCulture)
                + " skipped=" + SkippedTargetCount.ToString(CultureInfo.InvariantCulture)
                + " lastTarget=" + (LastTargetEntityId > 0
                    ? LastTargetEntityId.ToString(CultureInfo.InvariantCulture)
                    : "none")
                + " lastMovable=" + (LastMovableNumericId > 0
                    ? LastMovableNumericId.ToString(CultureInfo.InvariantCulture)
                    : "none")
                + " lastMode=" + ((int)LastForceMode == 0 ? "none" : LastForceMode.ToString())
                + " lastShape=" + ((int)LastFieldShape == 0 ? "none" : LastFieldShape.ToString())
                + " lastSpeed="
                + CharacterTestDeterministicUnits.MillimetersPerSecondToMetersPerSecond(
                    LastSpeedMillimetersPerSecond).ToString("0.###", CultureInfo.InvariantCulture)
                + "m/s"
                + " lastAccel="
                + CharacterTestDeterministicUnits.MillimetersPerSecondToMetersPerSecond(
                    LastAccelerationMillimetersPerSecondSquared).ToString("0.###", CultureInfo.InvariantCulture)
                + "m/s2"
                + " stable=" + (LastStableId <= 0 ? "none" : LastStableId.ToString(CultureInfo.InvariantCulture));
        }
    }
}


