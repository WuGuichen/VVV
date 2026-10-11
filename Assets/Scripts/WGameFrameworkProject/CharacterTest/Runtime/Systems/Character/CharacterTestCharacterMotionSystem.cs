using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using UnityEngine;
using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestLandingEvent
    {
        public CharacterTestLandingEvent(
            CharacterTestCharacterEntity entity,
            int downwardSpeedMillimetersPerSecond)
        {
            Entity = entity;
            DownwardSpeedMillimetersPerSecond = downwardSpeedMillimetersPerSecond;
        }

        public CharacterTestCharacterEntity Entity { get; }
        public int DownwardSpeedMillimetersPerSecond { get; }
    }

    internal readonly struct CharacterTestBodyPresenceMotionSample
    {
        public CharacterTestBodyPresenceMotionSample(
            int entityId,
            int previousXMillimeters,
            int previousYMillimeters,
            int previousZMillimeters,
            int previousYawMilliDegrees,
            int currentXMillimeters,
            int currentYMillimeters,
            int currentZMillimeters,
            int currentYawMilliDegrees,
            CharacterImportedGeometryBinding binding,
            bool rotationWitnessValid)
        {
            EntityId = entityId;
            PreviousXMillimeters = previousXMillimeters;
            PreviousYMillimeters = previousYMillimeters;
            PreviousZMillimeters = previousZMillimeters;
            PreviousYawMilliDegrees = previousYawMilliDegrees;
            CurrentXMillimeters = currentXMillimeters;
            CurrentYMillimeters = currentYMillimeters;
            CurrentZMillimeters = currentZMillimeters;
            CurrentYawMilliDegrees = currentYawMilliDegrees;
            Binding = binding;
            RotationWitnessValid = rotationWitnessValid;
        }

        public int EntityId { get; }
        public int PreviousXMillimeters { get; }
        public int PreviousYMillimeters { get; }
        public int PreviousZMillimeters { get; }
        public int PreviousYawMilliDegrees { get; }
        public int CurrentXMillimeters { get; }
        public int CurrentYMillimeters { get; }
        public int CurrentZMillimeters { get; }
        public int CurrentYawMilliDegrees { get; }
        public CharacterImportedGeometryBinding Binding { get; }
        public bool RotationWitnessValid { get; }
        public bool IsComplete => EntityId > 0;
    }

    public sealed partial class CharacterTestCharacterMotionSystem : ICharacterTestCharacterColliderSync
    {
        private const int GroundYMillimeters = 0;
        private const int GroundEpsilonMillimeters = 1;
        private const int MoveFacingEpsilonMilli = 1;
        private const int ApexVelocityEpsilonMillimetersPerSecond = 1250;
        private const int GroundContactProbeVelocityMillimetersPerSecond = -500;
        private const int CharacterContactVelocityEpsilonMillimetersPerSecond = 1;
        private const int DefaultLaunchLandingRecoveryFrames = 12;
        private const int BodyPresenceMotionScratchCapacity =
            CharacterTestCharacterPhysicsRuntime.BodyPresenceMaximumActiveCharacters;
        private static readonly ProfilerMarker CandidateCachePrepareMarker =
            new ProfilerMarker("CharacterTest.Motion.CandidateCachePrepare");
        private static readonly ProfilerMarker CandidateCacheDynamicIncrementalMarker =
            new ProfilerMarker("CharacterTest.Motion.CandidateCacheDynamicIncremental");
        private static readonly ProfilerMarker CandidateCacheDynamicRebuildMarker =
            new ProfilerMarker("CharacterTest.Motion.CandidateCacheDynamicRebuild");
        private static readonly ProfilerMarker KinematicStepMarker =
            new ProfilerMarker("CharacterTest.Motion.KinematicStep");
        private static readonly ProfilerMarker CandidateCacheFallbackMarker =
            new ProfilerMarker("CharacterTest.Motion.CandidateCacheFallback");
        private static readonly ICharacterMotionLocalSpaceResolver MotionLocalSpaceResolver =
            new CharacterTestMotionLocalSpaceResolver();
        private static readonly ICharacterMotionFacingResolver MotionFacingResolver =
            CharacterMotionDefaultFacingResolver.Instance;
        private readonly CharacterKinematicMotionSolver _kinematicMotionSolver =
            CharacterKinematicMotionSolver.Default;
        private readonly CharacterKinematicPhysicsAdapter _kinematicPhysicsAdapter =
            CharacterKinematicPhysicsAdapter.Default;

        private ICharacterTestCharacterMovementBasisProvider _movementBasisProvider;
        private CharacterTestCharacterMovementProfileRuntime _movementProfile =
            CharacterTestCharacterMovementProfileSource.Load(null, null);
        private CharacterTestCharacterPhysicsRuntime _physicsRuntime;
        private readonly Dictionary<CombatMotionConfig, CombatKinematicMotor> _physicsMotorCache =
            new Dictionary<CombatMotionConfig, CombatKinematicMotor>(32);
        private readonly CombatKinematicMotorCandidateCache _physicsCandidateCache =
            new CombatKinematicMotorCandidateCache();
        private readonly CombatMotionStepBuffer _physicsStepBuffer =
            new CombatMotionStepBuffer(CombatMotionConfig.DefaultMaxCollisionIterations);
        private readonly CharacterTestCharacterContactResolver _contactResolver;
        private readonly CharacterTestCharacterHurtboxSystem _hurtboxSystem = new CharacterTestCharacterHurtboxSystem();
        private readonly CharacterTestCharacterBodyPresenceSystem _bodyPresenceSystem =
            new CharacterTestCharacterBodyPresenceSystem();
        private readonly CharacterTestMovableNaturalObjectContactScratch _movableNaturalObjectContactScratch =
            new CharacterTestMovableNaturalObjectContactScratch();
        private readonly BodyPresenceMotionCapture[] _bodyPresenceMotionCaptures =
            new BodyPresenceMotionCapture[BodyPresenceMotionScratchCapacity];
        private readonly CharacterTestBodyPresenceMotionSample[] _bodyPresenceMotionSamples =
            new CharacterTestBodyPresenceMotionSample[BodyPresenceMotionScratchCapacity];
        private int _bodyPresenceMotionSampleCount;
        private CharacterTestTraceRecorder _traceRecorder;
        private RuntimeFrame _traceFrame;
        private CharacterTestLandingEvent[] _landingEvents = new CharacterTestLandingEvent[8];
        private int _landingEventCount;
        private CharacterTestCharacterMotionDiagnostic[] _motionDiagnostics =
            new CharacterTestCharacterMotionDiagnostic[8];
        private int _motionDiagnosticCount;
        private bool _suppressLandingEdges;

        public CharacterTestCharacterMotionSystem()
        {
            _contactResolver = new CharacterTestCharacterContactResolver(this);
        }

        public CharacterTestCharacterMovementProfileRuntime MovementProfile => _movementProfile;
        public CharacterTestCharacterPhysicsRuntime PhysicsRuntime => _physicsRuntime;
        public int LandingEventCount => _landingEventCount;

        /// <remarks>NoAlloc.</remarks>
        internal bool TryGetCharacterMotionDiagnostic(
            int entityId,
            out CharacterTestCharacterMotionDiagnostic diagnostic)
        {
            for (int i = 0; i < _motionDiagnosticCount; i++)
            {
                CharacterTestCharacterMotionDiagnostic candidate = _motionDiagnostics[i];
                if (candidate.EntityId != entityId)
                    continue;

                diagnostic = candidate;
                return true;
            }

            diagnostic = default;
            return false;
        }

        internal bool TryGetDamageGeometrySnapshot(
            int entityId,
            out CharacterTestDamageGeometrySnapshot snapshot)
        {
            return _hurtboxSystem.TryGetDamageGeometrySnapshot(entityId, out snapshot);
        }

        internal void ConfigureReferenceDummy(int entityId, MxFramework.CharacterAction.CharacterActionReferenceDummy dummy)
        {
            _hurtboxSystem.ConfigureReferenceDummy(entityId, dummy);
        }

        internal void SetActionPoseRegistry(MxFramework.CharacterAction.CharacterActionConfigRegistry registry)
            => _hurtboxSystem.SetActionPoseRegistry(registry);

        internal void PrepareHurtboxSource(CharacterTestCharacterEntity entity) => _hurtboxSystem.PrepareEntity(entity);

        internal int GetHurtboxRevision(int entityId)
        {
            return _hurtboxSystem.GetHurtboxRevision(entityId);
        }

        internal bool TryGetDamageZoneSnapshot(
            int entityId,
            out CharacterTestDamageZoneSnapshot snapshot)
        {
            return _hurtboxSystem.TryGetDamageZoneSnapshot(entityId, out snapshot);
        }

        internal bool TryGetRuntimeGeometryBinding(
            int entityId,
            out CharacterImportedGeometryBinding binding)
        {
            return _hurtboxSystem.TryGetRuntimeGeometryBinding(entityId, out binding);
        }

        /// <remarks>NoAlloc. Read-only projection of the existing Presence contact buffers.</remarks>
        internal CharacterTestBodyPresenceContactDiagnostics CaptureBodyPresenceContactDiagnostics()
        {
            return _contactResolver.CaptureBodyPresenceContactDiagnostics();
        }

        /// <remarks>NoAlloc. Reads the existing stable-sorted Presence constraint buffer.</remarks>
        internal bool TryGetBodyPresenceConstraintDiagnostic(
            int index,
            out CharacterTestBodyPresenceConstraintDiagnostic diagnostic)
        {
            return _contactResolver.TryGetBodyPresenceConstraintDiagnostic(index, out diagnostic);
        }

        /// <remarks>NoAlloc. Read-only projection of the existing Presence sync buffers.</remarks>
        internal CharacterTestBodyPresenceSyncDiagnostics CaptureBodyPresenceSyncDiagnostics()
        {
            return _bodyPresenceSystem.CaptureDiagnostics();
        }

        internal void RemoveHurtbox(CharacterTestCharacterEntityId entityId)
        {
            _hurtboxSystem.RemoveEntity(entityId.Value, _physicsRuntime);
            _bodyPresenceSystem.RemoveEntity(entityId.Value, _physicsRuntime);
        }

        internal void ClearHurtboxes()
        {
            _hurtboxSystem.Clear(_physicsRuntime);
            _bodyPresenceSystem.Clear(_physicsRuntime);
        }

        /// <remarks>NoAlloc.</remarks>
        public CharacterTestLandingEvent GetLandingEvent(int index)
        {
            if ((uint)index >= (uint)_landingEventCount)
                throw new System.ArgumentOutOfRangeException(nameof(index));
            return _landingEvents[index];
        }

        public void SetTraceRecorder(CharacterTestTraceRecorder traceRecorder)
        {
            _traceRecorder = traceRecorder;
        }

        public void SetMovementProfile(CharacterTestCharacterMovementProfileRuntime profile)
        {
            _movementProfile = profile.Profile != null
                ? profile
                : CharacterTestCharacterMovementProfileSource.Load(null, null);
            _physicsMotorCache.Clear();
        }

        public void SetPhysicsRuntime(CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            CharacterTestCharacterPhysicsRuntime previousPhysicsRuntime = _physicsRuntime;
            if (previousPhysicsRuntime != null)
            {
                _hurtboxSystem.Clear(previousPhysicsRuntime);
                _bodyPresenceSystem.Clear(previousPhysicsRuntime);
            }
            else
            {
                _hurtboxSystem.Clear();
                _bodyPresenceSystem.Clear(null);
            }

            _physicsRuntime = physicsRuntime != null && physicsRuntime.IsReady
                ? physicsRuntime
                : null;
            _physicsMotorCache.Clear();
            _physicsCandidateCache.Clear();
            _contactResolver.Clear();
            _movableNaturalObjectContactScratch.Clear();
            ClearCharacterMotionDiagnostics();
            ClearBodyPresenceMotionScratch();
            ResetAnimatedAppendageSoftContact();
        }

        internal void ResetRuntimeAiLabReplayTransientState()
        {
            _physicsCandidateCache.Invalidate();
            _contactResolver.Clear();
            _movableNaturalObjectContactScratch.Clear();
            ClearCharacterMotionDiagnostics();
            _bodyPresenceSystem.Clear(_physicsRuntime);
            ClearBodyPresenceMotionScratch();
            ResetAnimatedAppendageSoftContact();
            SuppressLandingEdgesForNextTick();
        }

        internal void ClearCharacterMotionDiagnostics()
        {
            _motionDiagnosticCount = 0;
        }

        internal void SuppressLandingEdgesForNextTick()
        {
            _landingEventCount = 0;
            _suppressLandingEdges = true;
            _contactResolver.SuppressAnimatedAppendagePresentationPhases();
        }

        internal void SyncBodyPartDerivedTopology(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            RuntimeFrame frame)
        {
            if (_physicsRuntime != null)
                _hurtboxSystem.SyncAll(entities, _physicsRuntime, frame);
        }

        public void SetMovementBasisProvider(ICharacterTestCharacterMovementBasisProvider provider)
        {
            _movementBasisProvider = provider;
        }

        public Vector2 ResolveWorldMove(CharacterTestCharacterEntity entity, Vector2 moveInput)
        {
            short moveX = CharacterTestDeterministicUnits.AxisToMilli(moveInput.x);
            short moveY = CharacterTestDeterministicUnits.AxisToMilli(moveInput.y);
            ClampMoveMilli(moveX, moveY, out moveX, out moveY);
            return ResolveMovementBasis(entity).ToWorldMove(new Vector2(
                CharacterTestDeterministicUnits.MilliToAxis(moveX),
                CharacterTestDeterministicUnits.MilliToAxis(moveY)));
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(CharacterTestCharacterRuntimeWorld world, float deltaTime, RuntimeFrame frame = default)
        {
            int steps = CharacterTestDeterministicUnits.SecondsToFrames(
                deltaTime,
                CharacterTestDeterministicUnits.DefaultTickRate);
            for (int i = 0; i < steps; i++)
                Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, frame);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(CharacterTestCharacterRuntimeWorld world, int tickRate, RuntimeFrame frame = default)
        {
            if (tickRate <= 0)
                tickRate = CharacterTestDeterministicUnits.DefaultTickRate;

            _traceFrame = frame;
            _landingEventCount = 0;
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            _recallWorld = world;
            ProcessPositionRecalls(entities, frame.Value);
            EnsureLandingEventCapacity(entities.Count);
            _motionDiagnosticCount = 0;
            EnsureMotionDiagnosticCapacity(entities.Count);
            _contactResolver.Clear();
            ClearBodyPresenceMotionScratch();
            bool animatedAppendagePreviousReady =
                TryCaptureAnimatedAppendagePreviousWitnesses(entities, frame);
            bool bodyPresenceMotionReady = false;
            if (_physicsRuntime != null)
            {
                bodyPresenceMotionReady = TryCaptureBodyPresenceMotionBeforeMotor(entities);
                CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
                CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                    world.MovableNaturalObjects,
                    _physicsRuntime);
                SyncAllCharacterCombatColliders(entities);
                _physicsMotorCache.EnsureCapacity(Mathf.Max(32, entities.Count * 2));
                _contactResolver.PrepareDynamicCharacterLookup(entities);
                using (CandidateCachePrepareMarker.Auto())
                {
                    bool canPrepareIncrementally = _physicsCandidateCache.CanServe(
                        _physicsRuntime.World,
                        _physicsRuntime.MotionObstacleLayerMask);
                    ProfilerMarker prepareKindMarker = canPrepareIncrementally
                        ? CandidateCacheDynamicIncrementalMarker
                        : CandidateCacheDynamicRebuildMarker;
                    using (prepareKindMarker.Auto())
                    {
                        _physicsCandidateCache.Prepare(
                            _physicsRuntime.World,
                            _physicsRuntime.StaticObstacleLayerMask,
                            CreateDynamicMotionObstacleMask(_physicsRuntime));
                    }
                }
            }
            for (int i = 0; i < entities.Count; i++)
                TickEntity(entities[i], world, tickRate);
            if (_physicsRuntime != null)
            {
                CharacterTestMovableNaturalObjectSystem.Tick(
                    world,
                    _physicsRuntime,
                    tickRate,
                    frame,
                    _movableNaturalObjectContactScratch);
                _contactResolver.ResolveCharacterOverlaps(entities);
                bool bodyPresenceReady = _bodyPresenceSystem.SyncAll(entities, _physicsRuntime);
                bodyPresenceMotionReady = bodyPresenceMotionReady
                    && TryCompleteBodyPresenceMotionAfterSync(entities, tickRate);
                _contactResolver.ResolveCharacterContactPressure(
                    entities,
                    _physicsRuntime,
                    bodyPresenceReady,
                    _bodyPresenceMotionSamples,
                    _bodyPresenceMotionSampleCount,
                    bodyPresenceMotionReady,
                    _movementProfile.RunSpeedMillimetersPerSecond,
                    _movementProfile.AccelerationMillimetersPerSecondSquared,
                    _movementProfile.TurnSpeedMilliDegreesPerSecond,
                    tickRate);
                _contactResolver.ApplyCharacterContactState(entities);
                _hurtboxSystem.SyncAll(entities, _physicsRuntime, frame);
            }
            else
            {
                _contactResolver.ResetCharacterContactState(entities);
            }
            ClearBodyPresenceMotionScratch();
            _animatedAppendagePreviousReady = animatedAppendagePreviousReady;
            _suppressLandingEdges = false;
        }

        private bool TryCaptureBodyPresenceMotionBeforeMotor(
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            if (_physicsRuntime == null
                || entities == null
                || entities.Count > _bodyPresenceMotionCaptures.Length)
            {
                return false;
            }

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null
                    || entity.Id.Value <= 0
                    || entity.Id.Value > CharacterTestCharacterPhysicsRuntime.BodyPresenceMaximumEntityId
                    || ContainsBodyPresenceMotionEntity(i, entity.Id.Value))
                {
                    return false;
                }

                CharacterImportedGeometryBinding binding =
                    CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity)?.Geometry;
                CharacterTestCharacterTransformComponent root = entity.Transform;
                bool previousWitnessValid = false;
                if (IsAuthoredBodyPresenceBinding(binding)
                    && _physicsRuntime.TryGetBodyPresenceSyncWitness(
                        entity.Id.Value,
                        out int revision,
                        out string contentHash,
                        out int activeLeafCount)
                    && revision == binding.BodyPresenceRevision
                    && string.Equals(
                        contentHash,
                        binding.BodyPresenceContentHash,
                        System.StringComparison.Ordinal))
                {
                    previousWitnessValid = _physicsRuntime.IsBodyPresenceColliderSynced(
                        entity.Id.Value,
                        root.XMillimeters,
                        root.YMillimeters,
                        root.ZMillimeters,
                        root.YawMilliDegrees,
                        revision,
                        contentHash,
                        activeLeafCount);
                }

                _bodyPresenceMotionCaptures[i] = new BodyPresenceMotionCapture(
                    entity,
                    root.XMillimeters,
                    root.YMillimeters,
                    root.ZMillimeters,
                    root.YawMilliDegrees,
                    binding,
                    previousWitnessValid);
            }

            _bodyPresenceMotionSampleCount = entities.Count;
            return true;
        }

        private bool TryCompleteBodyPresenceMotionAfterSync(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int tickRate)
        {
            if (_physicsRuntime == null
                || entities == null
                || entities.Count != _bodyPresenceMotionSampleCount
                || !TryResolveBodyPresenceLegalDelta(tickRate, out int legalDeltaMillimeters))
            {
                return false;
            }

            for (int i = 0; i < _bodyPresenceMotionSampleCount; i++)
            {
                BodyPresenceMotionCapture capture = _bodyPresenceMotionCaptures[i];
                CharacterTestCharacterEntity entity = capture.Entity;
                if (entity == null || !ReferenceEquals(entity, entities[i]))
                    return false;

                CharacterImportedGeometryBinding currentBinding =
                    CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity)?.Geometry;
                CharacterTestCharacterTransformComponent root = entity.Transform;
                bool currentWitnessValid = false;
                if (IsAuthoredBodyPresenceBinding(currentBinding)
                    && _physicsRuntime.TryGetBodyPresenceSyncWitness(
                        entity.Id.Value,
                        out int revision,
                        out string contentHash,
                        out int activeLeafCount)
                    && revision == currentBinding.BodyPresenceRevision
                    && string.Equals(
                        contentHash,
                        currentBinding.BodyPresenceContentHash,
                        System.StringComparison.Ordinal))
                {
                    currentWitnessValid = _physicsRuntime.IsBodyPresenceColliderSynced(
                        entity.Id.Value,
                        root.XMillimeters,
                        root.YMillimeters,
                        root.ZMillimeters,
                        root.YawMilliDegrees,
                        revision,
                        contentHash,
                        activeLeafCount);
                }

                bool sameBinding = IsAuthoredBodyPresenceBinding(capture.Binding)
                    && IsAuthoredBodyPresenceBinding(currentBinding)
                    && capture.Binding.BodyPresenceRevision == currentBinding.BodyPresenceRevision
                    && string.Equals(
                        capture.Binding.BodyPresenceContentHash,
                        currentBinding.BodyPresenceContentHash,
                        System.StringComparison.Ordinal);
                bool teleported = ExceedsLegalBodyPresenceDelta(
                    capture.PreviousXMillimeters,
                    capture.PreviousYMillimeters,
                    capture.PreviousZMillimeters,
                    root.XMillimeters,
                    root.YMillimeters,
                    root.ZMillimeters,
                    legalDeltaMillimeters);
                bool rotationWitnessValid = !_suppressLandingEdges
                    && !teleported
                    && capture.PreviousWitnessValid
                    && currentWitnessValid
                    && sameBinding;
                _bodyPresenceMotionSamples[i] = new CharacterTestBodyPresenceMotionSample(
                    entity.Id.Value,
                    capture.PreviousXMillimeters,
                    capture.PreviousYMillimeters,
                    capture.PreviousZMillimeters,
                    capture.PreviousYawMilliDegrees,
                    root.XMillimeters,
                    root.YMillimeters,
                    root.ZMillimeters,
                    root.YawMilliDegrees,
                    currentBinding,
                    rotationWitnessValid);
            }

            return true;
        }

        private bool ContainsBodyPresenceMotionEntity(int count, int entityId)
        {
            for (int i = 0; i < count; i++)
            {
                if (_bodyPresenceMotionCaptures[i].Entity?.Id.Value == entityId)
                    return true;
            }

            return false;
        }

        private bool TryResolveBodyPresenceLegalDelta(
            int tickRate,
            out int legalDeltaMillimeters)
        {
            legalDeltaMillimeters = 0;
            int runSpeed = _movementProfile.RunSpeedMillimetersPerSecond;
            int acceleration = _movementProfile.AccelerationMillimetersPerSecondSquared;
            if (runSpeed < 0 || acceleration < 0 || tickRate <= 0)
                return false;

            try
            {
                int accelerationStep = checked((int)(((long)acceleration +
                    tickRate - 1) /
                    tickRate));
                int maximumSpeed = checked(runSpeed + accelerationStep);
                legalDeltaMillimeters = checked((int)(((long)maximumSpeed +
                    tickRate - 1) /
                    tickRate));
                return true;
            }
            catch (System.OverflowException)
            {
                return false;
            }
        }

        private static bool ExceedsLegalBodyPresenceDelta(
            int previousX,
            int previousY,
            int previousZ,
            int currentX,
            int currentY,
            int currentZ,
            int legalDelta)
        {
            return System.Math.Abs((long)currentX - previousX) > legalDelta
                || System.Math.Abs((long)currentZ - previousZ) > legalDelta;
        }

        private static bool IsAuthoredBodyPresenceBinding(
            CharacterImportedGeometryBinding binding)
        {
            return binding != null
                && binding.BodyPresenceMode == CharacterBodyPresenceMode.Authored
                && binding.BodyPresenceRevision > 0
                && binding.BodyPresenceVolumes != null;
        }

        private void ClearBodyPresenceMotionScratch()
        {
            for (int i = 0; i < _bodyPresenceMotionCaptures.Length; i++)
            {
                _bodyPresenceMotionCaptures[i] = default;
                _bodyPresenceMotionSamples[i] = default;
            }

            _bodyPresenceMotionSampleCount = 0;
        }

        private void TickEntity(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterRuntimeWorld world,
            int tickRate)
        {
            // Freeze only the successful return frame; later ticks resume ordinary input and new forces normally.
            if (entity.PositionRecall.Result == CharacterTestPositionRecallResult.Returned && entity.PositionRecall.CompletedTick == _traceFrame.Value)
            { entity.ClearMotionRequests(); return; }
            short moveX = 0;
            short moveY = 0;
            short desiredMoveX = 0;
            short desiredMoveY = 0;
            int moveInputMagnitude = 0;
            if (entity.BattleState.CanMoveByInput)
            {
                ClampMoveMilli(entity.Input.MoveXMilli, entity.Input.MoveYMilli, out moveX, out moveY);
                ResolveWorldMoveMilli(entity, moveX, moveY, out desiredMoveX, out desiredMoveY);
                moveInputMagnitude = CharacterTestDeterministicMath.Magnitude(moveX, moveY);
            }

            if (entity.Input.TurnInPlace)
            {
                moveX = 0;
                moveY = 0;
                desiredMoveX = 0;
                desiredMoveY = 0;
                moveInputMagnitude = 0;
            }

            bool wasGrounded = entity.IsGrounded
                || (_physicsRuntime == null && entity.Transform.YMillimeters <= GroundYMillimeters + GroundEpsilonMillimeters);
            CharacterMotionRequestFrame requestFrame = BuildMotionRequestFrame(entity);
            if (entity.Input.TurnInPlace)
            {
                // A turn command is a zero-translation control semantic. Do
                // not let an already queued root-motion request turn it into
                // a hidden world move on this tick.
                requestFrame.HasRootMotion = false;
                requestFrame.RootMotionDeltaX = 0;
                requestFrame.RootMotionDeltaY = 0;
                requestFrame.RootMotionDeltaZ = 0;
                requestFrame.RootMotionSource = default;
                requestFrame.RootMotionObstaclePolicy = CharacterMotionObstaclePolicy.Default;
            }
            var baseRequestFrame = requestFrame;
            var traction = entity.GroundPointTraction;
            int tractionX = 0, tractionZ = 0;
            if (traction.IsActive)
            {
                if (traction.Actor != entity.GameplayEntityId || !entity.CombatState.IsAlive)
                    traction.Finish(CharacterTestGroundPointTractionEnd.ActorUnavailable, _traceFrame.Value);
                else if (!entity.IsGrounded)
                    traction.Finish(CharacterTestGroundPointTractionEnd.GroundLost, _traceFrame.Value);
                else if (!entity.BattleState.CanMoveByInput || entity.Action.State == CharacterTestActionStateId.Running || entity.Input.TurnInPlace)
                    traction.Finish(CharacterTestGroundPointTractionEnd.ActorNotFree, _traceFrame.Value);
                else if (traction.TryStep(entity.Transform.XMillimeters, entity.Transform.ZMillimeters,
                    tickRate, _traceFrame.Value, out tractionX, out tractionZ))
                {
                    // The existing motor's transient displacement math preserves shared persistent velocity.
                    // This is procedural mechanism motion, never an action or walking command.
                    requestFrame.HasRootMotion = true;
                    requestFrame.RootMotionDeltaX += tractionX; requestFrame.RootMotionDeltaZ += tractionZ;
                    requestFrame.RootMotionObstaclePolicy = CharacterMotionObstaclePolicy.Default;
                }
                entity.SetGroundPointTraction(traction);
            }
            CharacterKinematicMotionInput kinematicInput = BuildKinematicMotionInput(
                entity,
                requestFrame,
                tickRate,
                desiredMoveX,
                desiredMoveY,
                moveInputMagnitude,
                wasGrounded);
            CharacterKinematicMotionPrePhysicsResult prePhysics =
                _kinematicMotionSolver.SolvePrePhysics(in kinematicInput);
            if (requestFrame.HasMovementControlScale || requestFrame.HasRotationControlScale || requestFrame.LockMovement)
            {
                _traceRecorder?.LocomotionControlScale(
                    _traceFrame,
                    entity.Id.Value,
                    new CharacterTestMotionControlScaleTraceEvent(
                        prePhysics.MovementControlScaleMilli,
                        prePhysics.RotationControlScaleMilli,
                        requestFrame.ControlScaleSource));
            }

            int impulseY = requestFrame.HasImpulse
                ? requestFrame.ImpulseVelocityY
                : entity.Action.JumpImpulseYMillimetersPerSecond;
            if (prePhysics.JumpImpulseApplied)
            {
                _traceRecorder?.JumpMotionImpulse(
                    _traceFrame,
                    entity.Id.Value,
                    new CharacterTestJumpMotionTraceEvent(
                        false,
                        impulseY,
                        requestFrame.ImpulseSource));
            }
            else if (prePhysics.JumpImpulseBlocked)
            {
                _traceRecorder?.JumpMotionImpulse(
                    _traceFrame,
                    entity.Id.Value,
                    new CharacterTestJumpMotionTraceEvent(
                        true,
                        impulseY,
                        requestFrame.ImpulseSource,
                        wasGrounded,
                        entity.CoyoteTimeFramesRemaining));
            }

            int rootMotionDeltaX = requestFrame.RootMotionDeltaX;
            int rootMotionDeltaY = requestFrame.RootMotionDeltaY;
            int rootMotionDeltaZ = requestFrame.RootMotionDeltaZ;
            if (requestFrame.HasRootMotion)
            {
                _traceRecorder?.LocomotionRootMotionDelta(
                    _traceFrame,
                    entity.Id.Value,
                    new CharacterTestRootMotionDeltaTraceEvent(
                        rootMotionDeltaX,
                        rootMotionDeltaY,
                        rootMotionDeltaZ,
                        requestFrame.RootMotionSource));
            }

            CharacterKinematicMotionPrePhysicsResult basePrePhysics = prePhysics;
            if (tractionX != 0 || tractionZ != 0)
            {
                var baseInput = BuildKinematicMotionInput(entity, baseRequestFrame, tickRate,
                    desiredMoveX, desiredMoveY, moveInputMagnitude, wasGrounded);
                basePrePhysics = _kinematicMotionSolver.SolvePrePhysics(in baseInput);
            }
            int x;
            int y;
            int z;
            int previousX = entity.Transform.XMillimeters;
            int previousY = entity.Transform.YMillimeters;
            int previousZ = entity.Transform.ZMillimeters;
            int tractionBaseX = previousX, tractionBaseZ = previousZ;
            bool grounded = false;
            CombatMotionCollisionFlags collisionFlags = CombatMotionCollisionFlags.None;
            int surfaceFrictionMilli = 0;
            CombatSurfaceMaterial surfaceMaterial = CombatSurfaceMaterial.Default;
            int horizontalVelocityX = prePhysics.HorizontalVelocityX;
            int horizontalVelocityY = prePhysics.HorizontalVelocityY;
            int verticalVelocity = prePhysics.VerticalVelocity;
            int landingDownwardSpeed = verticalVelocity < 0 ? -verticalVelocity : 0;
            int surfaceBounceSpeedCapY = prePhysics.SurfaceBounceSpeedCapY;

            if (_physicsRuntime != null)
            {
                _contactResolver.RecordAttemptedContactVelocity(
                    entity.Id.Value,
                    prePhysics.SimulatedHorizontalVelocityX,
                    prePhysics.SimulatedHorizontalVelocityY);
                CombatPhysicsLayerMask activeObstacleLayerMask = ResolveMotionObstacleLayerMask(requestFrame);
                CombatKinematicMotor physicsMotor =
                    EnsurePhysicsMotor(tickRate, _movementProfile.Profile, entity.ColliderPosture, activeObstacleLayerMask);
                CharacterKinematicPhysicsStepInput physicsInput = BuildKinematicPhysicsStepInput(
                    entity,
                    prePhysics,
                    requestFrame.HasRootMotion,
                    horizontalVelocityX,
                    horizontalVelocityY,
                    verticalVelocity,
                    previousX,
                    previousY,
                    previousZ,
                    ResolveWallSlideMinimumHorizontalSpeed(
                        entity,
                        prePhysics,
                        requestFrame));
                bool canUseCandidateCache = _physicsCandidateCache.CanServe(
                    _physicsRuntime.World,
                    activeObstacleLayerMask);
                CombatKinematicMotorCandidateCache activeCandidateCache =
                    canUseCandidateCache ? _physicsCandidateCache : null;
                CharacterKinematicPhysicsStepResult basePhysicsResult = default;
                bool hasTractionStep = tractionX != 0 || tractionZ != 0;
                if (hasTractionStep)
                {
                    var basePhysicsInput = BuildKinematicPhysicsStepInput(entity, basePrePhysics, baseRequestFrame.HasRootMotion,
                        basePrePhysics.HorizontalVelocityX, basePrePhysics.HorizontalVelocityY, basePrePhysics.VerticalVelocity,
                        previousX, previousY, previousZ, ResolveWallSlideMinimumHorizontalSpeed(entity, basePrePhysics, baseRequestFrame));
                    // Query the same start pose without traction. This keeps blocked transient displacement
                    // out of shared velocity and does not publish predicted contacts or move the world body.
                    basePhysicsResult = _kinematicPhysicsAdapter.Query(physicsMotor, _physicsRuntime.World,
                        _physicsStepBuffer, in basePhysicsInput, activeCandidateCache);
                    tractionBaseX = basePhysicsResult.PositionXMillimeters; tractionBaseZ = basePhysicsResult.PositionZMillimeters;
                }
                CharacterKinematicPhysicsStepResult physicsResult;
                using (KinematicStepMarker.Auto())
                {
                    if (canUseCandidateCache)
                    {
                        physicsResult = _kinematicPhysicsAdapter.Step(
                            physicsMotor,
                            _physicsRuntime.World,
                            _physicsStepBuffer,
                            in physicsInput,
                            activeCandidateCache);
                    }
                    else
                    {
                        using (CandidateCacheFallbackMarker.Auto())
                        {
                            physicsResult = _kinematicPhysicsAdapter.Step(
                                physicsMotor,
                                _physicsRuntime.World,
                                _physicsStepBuffer,
                                in physicsInput);
                        }
                    }
                }

                CombatMotionStepResult physicsStep = physicsResult.StepResult;
                x = physicsResult.PositionXMillimeters;
                y = physicsResult.PositionYMillimeters;
                z = physicsResult.PositionZMillimeters;
                horizontalVelocityX = hasTractionStep ? basePhysicsResult.HorizontalVelocityX : physicsResult.HorizontalVelocityX;
                horizontalVelocityY = hasTractionStep ? basePhysicsResult.HorizontalVelocityY : physicsResult.HorizontalVelocityY;
                verticalVelocity = hasTractionStep ? basePhysicsResult.VerticalVelocity : physicsResult.VerticalVelocity;
                collisionFlags = physicsResult.CollisionFlags;
                grounded = physicsResult.Grounded;
                surfaceFrictionMilli = physicsResult.SurfaceFrictionMilli;
                surfaceMaterial = physicsResult.SurfaceMaterial;
                surfaceBounceSpeedCapY = physicsResult.SurfaceBounceSpeedCapY;
                RecordCharacterMotionDiagnostic(
                    entity.Id.Value,
                    physicsMotor,
                    in physicsResult);
                _contactResolver.RecordDynamicContactHit(
                    entity,
                    world,
                    physicsStep,
                    _physicsRuntime,
                    prePhysics.SimulatedHorizontalVelocityX,
                    prePhysics.SimulatedHorizontalVelocityY);
            }
            else
            {
                x = entity.Transform.XMillimeters
                    + DividePerTick(horizontalVelocityX, tickRate)
                    + rootMotionDeltaX;
                z = entity.Transform.ZMillimeters
                    + DividePerTick(horizontalVelocityY, tickRate)
                    + rootMotionDeltaZ;
                y = entity.Transform.YMillimeters
                    + DividePerTick(verticalVelocity, tickRate)
                    + rootMotionDeltaY;
            }

            if (_physicsRuntime == null && y <= GroundYMillimeters)
            {
                y = GroundYMillimeters;
                verticalVelocity = 0;
                grounded = true;
            }

            CharacterKinematicMotionFinalResult finalMotion =
                _kinematicMotionSolver.CompleteAfterPhysics(
                    in kinematicInput,
                    in prePhysics,
                    grounded,
                    horizontalVelocityX,
                    horizontalVelocityY,
                    verticalVelocity,
                    surfaceBounceSpeedCapY,
                    MotionFacingResolver);
            horizontalVelocityX = finalMotion.HorizontalVelocityX;
            horizontalVelocityY = finalMotion.HorizontalVelocityY;
            verticalVelocity = finalMotion.VerticalVelocity;

            int yawMilliDegrees = finalMotion.YawMilliDegrees;
            short blendX = finalMotion.BlendX;
            short blendY = finalMotion.BlendY;
            int jumpBufferFrames = finalMotion.JumpBufferFrames;
            int coyoteFrames = finalMotion.CoyoteTimeFrames;
            CharacterMovementMode movementMode = (CharacterMovementMode)finalMotion.MovementModeId;
            surfaceBounceSpeedCapY = finalMotion.SurfaceBounceSpeedCapY;

            // TurnInPlace is an explicit control semantic. It must not be
            // represented by a tiny world move (which would activate
            // locomotion and can leak translation through retained velocity).
            // Motion owns the facing write so the resulting pose/revision is
            // authoritative for the next completed-frame snapshot.
            if (entity.Input.TurnInPlace)
            {
                // The command carries the quantized yaw *delta* requested by
                // the planner (the same contract as
                // AttackOpportunityResult.RequiredYawDeltaMilliDegrees).
                // Apply it to the completed authority facing instead of
                // treating the delta as an absolute world yaw.
                yawMilliDegrees = CharacterTestDeterministicUnits.NormalizeMilliDegrees(
                    entity.Transform.YawMilliDegrees
                    + entity.Input.TurnInPlaceYawMilliDegrees);
                movementMode = CharacterMovementMode.TurnInPlace;
                horizontalVelocityX = 0;
                horizontalVelocityY = 0;
                blendX = 0;
                blendY = 0;
            }

            entity.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                x,
                y,
                z,
                yawMilliDegrees));
            entity.SetMotionStateDeterministic(
                verticalVelocity,
                blendX,
                blendY,
                grounded,
                horizontalVelocityX,
                horizontalVelocityY,
                entity.Input.TurnInPlace ? 0 : prePhysics.DesiredStateVelocityX,
                entity.Input.TurnInPlace ? 0 : prePhysics.DesiredStateVelocityY,
                jumpBufferFrames,
                coyoteFrames,
                movementMode,
                prePhysics.MovementLocked,
                prePhysics.MovementControlScaleMilli,
                prePhysics.RotationControlScaleMilli,
                _movementProfile.Id,
                _movementProfile.UsedFallback,
                _physicsRuntime != null,
                collisionFlags,
                surfaceFrictionMilli,
                surfaceMaterial,
                surfaceBounceSpeedCapY);
            if (!_suppressLandingEdges && !wasGrounded && grounded)
                AddLandingEvent(entity, landingDownwardSpeed);
            UpdateSpecialHitStateAfterMotion(entity, grounded, _traceFrame.Value);
            SyncCharacterCombatCollider(entity);
            if (tractionX != 0 || tractionZ != 0)
            {
                traction.CompleteStep(tractionBaseX, tractionBaseZ, x, z, tractionX, tractionZ, grounded, _traceFrame.Value);
                entity.SetGroundPointTraction(traction);
            }
            entity.ClearMotionRequests();
            if (entity.Input.TurnInPlace)
            {
                // TurnInPlace is an edge command. Consume it only after the
                // resulting facing has been written, so a retained input or
                // replayed snapshot cannot rotate the entity twice.
                entity.SetInput(entity.Input.WithoutTurnInPlace());
            }
        }

        private readonly struct BodyPresenceMotionCapture
        {
            public BodyPresenceMotionCapture(
                CharacterTestCharacterEntity entity,
                int previousXMillimeters,
                int previousYMillimeters,
                int previousZMillimeters,
                int previousYawMilliDegrees,
                CharacterImportedGeometryBinding binding,
                bool previousWitnessValid)
            {
                Entity = entity;
                PreviousXMillimeters = previousXMillimeters;
                PreviousYMillimeters = previousYMillimeters;
                PreviousZMillimeters = previousZMillimeters;
                PreviousYawMilliDegrees = previousYawMilliDegrees;
                Binding = binding;
                PreviousWitnessValid = previousWitnessValid;
            }

            public CharacterTestCharacterEntity Entity { get; }
            public int PreviousXMillimeters { get; }
            public int PreviousYMillimeters { get; }
            public int PreviousZMillimeters { get; }
            public int PreviousYawMilliDegrees { get; }
            public CharacterImportedGeometryBinding Binding { get; }
            public bool PreviousWitnessValid { get; }
        }

        private void AddLandingEvent(
            CharacterTestCharacterEntity entity,
            int downwardSpeedMillimetersPerSecond)
        {
            _landingEvents[_landingEventCount++] = new CharacterTestLandingEvent(
                entity,
                downwardSpeedMillimetersPerSecond);
        }

        private void EnsureLandingEventCapacity(int entityCount)
        {
            if (entityCount <= _landingEvents.Length)
                return;

            int capacity = _landingEvents.Length;
            while (capacity < entityCount)
                capacity *= 2;
            System.Array.Resize(ref _landingEvents, capacity);
        }

        private void EnsureMotionDiagnosticCapacity(int entityCount)
        {
            if (entityCount <= _motionDiagnostics.Length)
                return;

            int capacity = _motionDiagnostics.Length;
            while (capacity < entityCount)
                capacity *= 2;
            System.Array.Resize(ref _motionDiagnostics, capacity);
        }

        private void RecordCharacterMotionDiagnostic(
            int entityId,
            CombatKinematicMotor motor,
            in CharacterKinematicPhysicsStepResult physicsResult)
        {
            CombatMotionStepResult step = physicsResult.StepResult;
            bool hasCollision = TrySelectDiagnosticCollision(
                step,
                out CombatMotionCollision collision);
            CombatMotionCollisionFlags selectedFlags = hasCollision
                ? collision.Flags
                : CombatMotionCollisionFlags.None;
            Fix64 snapAppliedDistance = hasCollision
                && HasFlag(selectedFlags, CombatMotionCollisionFlags.GroundSnap)
                    ? Fix64.Max(collision.Distance - motor.Config.SkinWidth, Fix64.Zero)
                    : Fix64.Zero;
            CombatPhysicsShapeKind targetShapeKind = hasCollision
                ? ResolveTargetShapeKind(
                    _physicsRuntime.World,
                    collision.TargetBodyId,
                    collision.TargetColliderId)
                : default;

            _motionDiagnostics[_motionDiagnosticCount++] =
                new CharacterTestCharacterMotionDiagnostic(
                    _traceFrame,
                    entityId,
                    true,
                    hasCollision,
                    hasCollision ? collision.TargetEntityId : CombatEntityId.None,
                    hasCollision ? collision.TargetBodyId : CombatBodyId.None,
                    hasCollision ? collision.TargetColliderId : CombatColliderId.None,
                    targetShapeKind,
                    hasCollision ? collision.Normal : FixVector3.Zero,
                    selectedFlags,
                    hasCollision ? collision.Distance : Fix64.Zero,
                    hasCollision ? collision.Fraction : Fix64.Zero,
                    hasCollision ? collision.SurfaceMaterial : step.SurfaceMaterial,
                    motor.Config.GroundMinNormalY,
                    ResolveSurfaceClassification(selectedFlags),
                    step.DesiredDelta,
                    step.AppliedDelta,
                    step.State.Velocity,
                    physicsResult.Grounded,
                    snapAppliedDistance);
        }

        internal static bool TrySelectDiagnosticCollision(
            CombatMotionStepResult step,
            out CombatMotionCollision collision)
        {
            if (step.CollisionCount <= 0)
            {
                collision = default;
                return false;
            }

            collision = step.Collisions[0];
            int bestPriority = GetDiagnosticCollisionPriority(collision.Flags);
            for (int i = 1; i < step.CollisionCount; i++)
            {
                CombatMotionCollision candidate = step.Collisions[i];
                int candidatePriority = GetDiagnosticCollisionPriority(candidate.Flags);
                if (candidatePriority <= bestPriority)
                    continue;

                collision = candidate;
                bestPriority = candidatePriority;
                if (bestPriority == 4)
                    break;
            }

            return true;
        }

        internal static CharacterTestMotionSurfaceClassification ResolveSurfaceClassification(
            CombatMotionCollisionFlags flags)
        {
            if (HasFlag(flags, CombatMotionCollisionFlags.Grounded))
                return CharacterTestMotionSurfaceClassification.Walkable;
            if (HasFlag(flags, CombatMotionCollisionFlags.SteepSlope))
                return CharacterTestMotionSurfaceClassification.Steep;
            if (HasFlag(flags, CombatMotionCollisionFlags.Ceiling))
                return CharacterTestMotionSurfaceClassification.Ceiling;
            if (HasFlag(flags, CombatMotionCollisionFlags.Wall))
                return CharacterTestMotionSurfaceClassification.Wall;
            return CharacterTestMotionSurfaceClassification.None;
        }

        private static int GetDiagnosticCollisionPriority(CombatMotionCollisionFlags flags)
        {
            if (HasFlag(flags, CombatMotionCollisionFlags.GroundSnap))
                return 4;
            if (HasFlag(flags, CombatMotionCollisionFlags.Grounded))
                return 3;
            if (HasFlag(flags, CombatMotionCollisionFlags.SteepSlope))
                return 2;
            return 1;
        }

        private static CombatPhysicsShapeKind ResolveTargetShapeKind(
            CombatPhysicsWorld world,
            CombatBodyId bodyId,
            CombatColliderId colliderId)
        {
            if (world.TryGetAabbCollider(bodyId, colliderId, out _))
                return CombatPhysicsShapeKind.Aabb;
            if (world.TryGetSphereCollider(bodyId, colliderId, out _))
                return CombatPhysicsShapeKind.Sphere;
            if (world.TryGetCapsuleCollider(bodyId, colliderId, out _))
                return CombatPhysicsShapeKind.Capsule;
            if (world.TryGetObbCollider(bodyId, colliderId, out _))
                return CombatPhysicsShapeKind.Obb;
            return default;
        }

        private static bool HasFlag(
            CombatMotionCollisionFlags value,
            CombatMotionCollisionFlags flag)
        {
            return (value & flag) == flag;
        }

        private static void UpdateSpecialHitStateAfterMotion(
            CharacterTestCharacterEntity entity,
            bool grounded,
            long frame)
        {
            CharacterTestSpecialHitStateComponent state = entity.SpecialHitState;
            if (!state.IsLaunched)
                return;

            if (!grounded)
            {
                if (state.Phase != CharacterTestSpecialHitPhase.Airborne)
                    entity.SetSpecialHitState(state.WithPhase(CharacterTestSpecialHitPhase.Airborne, frame));
                return;
            }

            if (state.Phase == CharacterTestSpecialHitPhase.Airborne)
            {
                entity.SetSpecialHitState(state.WithPhase(CharacterTestSpecialHitPhase.LandingRecovery, frame));
                return;
            }

            if (state.Phase == CharacterTestSpecialHitPhase.Enter)
            {
                entity.SetSpecialHitState(state.WithPhase(CharacterTestSpecialHitPhase.LandingRecovery, frame));
                return;
            }

            if (state.Phase == CharacterTestSpecialHitPhase.LandingRecovery
                && frame - state.SinceFrame >= DefaultLaunchLandingRecoveryFrames)
            {
                entity.SetSpecialHitState(CharacterTestSpecialHitStateComponent.None);
            }
        }

        private CharacterTestCharacterMovementBasis ResolveMovementBasis(CharacterTestCharacterEntity entity)
        {
            return entity != null
                && entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer
                ? _movementBasisProvider?.CurrentMovementBasis ?? CharacterTestCharacterMovementBasis.Identity
                : CharacterTestCharacterMovementBasis.Identity;
        }

        private void ResolveWorldMoveMilli(
            CharacterTestCharacterEntity entity,
            short moveX,
            short moveY,
            out short desiredMoveX,
            out short desiredMoveY)
        {
            if (entity.Input.HasWorldMove)
            {
                ClampMoveMilli(entity.Input.WorldMoveXMilli, entity.Input.WorldMoveYMilli, out desiredMoveX, out desiredMoveY);
                return;
            }

            desiredMoveX = moveX;
            desiredMoveY = moveY;
        }

        private CharacterKinematicMotionInput BuildKinematicMotionInput(
            CharacterTestCharacterEntity entity,
            CharacterMotionRequestFrame requestFrame,
            int tickRate,
            short desiredMoveX,
            short desiredMoveY,
            int moveInputMagnitude,
            bool wasGrounded)
        {
            return new CharacterKinematicMotionInput(
                BuildKinematicMotionProfile(tickRate, entity.Input.MovementSpeedScaleMilli, entity.Input.BehaviorControl),
                requestFrame,
                BuildMotionFacingInput(entity, desiredMoveX, desiredMoveY),
                tickRate,
                desiredMoveX,
                desiredMoveY,
                moveInputMagnitude,
                entity.Input.TurnInPlace
                    ? 0
                    : BoundApproachVelocity(entity.HorizontalVelocityXMillimetersPerSecond, entity.HorizontalVelocityYMillimetersPerSecond, entity.Input.BehaviorControl, tickRate),
                entity.Input.TurnInPlace
                    ? 0
                    : BoundApproachVelocity(entity.HorizontalVelocityYMillimetersPerSecond, entity.HorizontalVelocityXMillimetersPerSecond, entity.Input.BehaviorControl, tickRate),
                entity.VerticalVelocityMillimetersPerSecond,
                wasGrounded,
                false,
                entity.Input.JumpReleased,
                0,
                entity.CoyoteTimeFramesRemaining,
                entity.Action.MovementLocked,
                (int)entity.Action.MovementMode,
                entity.Action.JumpImpulseYMillimetersPerSecond,
                entity.Action.MovementControlScaleMilli,
                entity.Action.RotationControlScaleMilli,
                entity.SurfaceBounceSpeedCapYMillimetersPerSecond,
                _physicsRuntime != null,
                entity.Physics.ContactVelocityXMillimetersPerSecond,
                entity.Physics.ContactVelocityYMillimetersPerSecond,
                ResolveCurrentGroundSurfaceMaterial(entity, wasGrounded),
                (int)CharacterMovementMode.Idle,
                (int)CharacterMovementMode.Airborne,
                (int)CharacterMovementMode.ControlLocked,
                MoveFacingEpsilonMilli,
                CharacterContactVelocityEpsilonMillimetersPerSecond,
                CharacterTestDeterministicUnits.InputAxisScale,
                CharacterTestDeterministicUnits.ScaleFactorScale,
                CharacterTestDeterministicUnits.DefaultTickRate,
                ApexVelocityEpsilonMillimetersPerSecond);
        }

        private static int BoundApproachSpeed(int speed, CharacterTestBehaviorControlInput control, int tickRate)
        {
            if (!control.IsPresent || control.MaxMoveStepMillimeters < 0) return speed;
            return (int)System.Math.Min(speed, System.Math.Min(int.MaxValue, (long)control.MaxMoveStepMillimeters * tickRate));
        }

        private static int BoundApproachVelocity(int component, int otherComponent, CharacterTestBehaviorControlInput control, int tickRate)
        {
            if (!control.IsPresent || control.MaxMoveStepMillimeters < 0) return component;
            long limit = System.Math.Min(int.MaxValue, (long)control.MaxMoveStepMillimeters * tickRate);
            long magnitudeBound = System.Math.Abs((long)component) + System.Math.Abs((long)otherComponent);
            return magnitudeBound > limit ? (int)((long)component * limit / magnitudeBound) : component;
        }

        private CharacterKinematicMotionProfile BuildKinematicMotionProfile(
            int tickRate,
            int movementSpeedScaleMilli,
            CharacterTestBehaviorControlInput behaviorControl = default)
        {
            CharacterMovementProfileConfig profile = _movementProfile.Profile;
            int speedScaleMilli = movementSpeedScaleMilli <= 0
                ? CharacterTestControlIntent.DefaultMovementSpeedScaleMilli
                : CharacterTestControlIntent.NormalizeMovementSpeedScaleMilli(movementSpeedScaleMilli);
            return new CharacterKinematicMotionProfile(
                (int)profile.DefaultMode,
                (int)CharacterMovementMode.Walk,
                BoundApproachSpeed(CharacterTestDeterministicMath.MulDivRoundToInt(
                    _movementProfile.WalkSpeedMillimetersPerSecond,
                    speedScaleMilli,
                    CharacterTestControlIntent.DefaultMovementSpeedScaleMilli), behaviorControl, tickRate),
                BoundApproachSpeed(CharacterTestDeterministicMath.MulDivRoundToInt(
                    _movementProfile.RunSpeedMillimetersPerSecond,
                    speedScaleMilli,
                    CharacterTestControlIntent.DefaultMovementSpeedScaleMilli), behaviorControl, tickRate),
                _movementProfile.AccelerationMillimetersPerSecondSquared,
                _movementProfile.DecelerationMillimetersPerSecondSquared,
                behaviorControl.IsPresent ? CharacterTestDeterministicMath.MulDivRoundToInt(
                    _movementProfile.TurnSpeedMilliDegreesPerSecond, behaviorControl.TurnSpeedScaleMilli, 1000)
                    : _movementProfile.TurnSpeedMilliDegreesPerSecond,
                _movementProfile.GroundFrictionMillimetersPerSecondSquared,
                _movementProfile.AirControlMilli,
                _movementProfile.GravityMillimetersPerSecondSquared,
                _movementProfile.MaxFallSpeedMillimetersPerSecond,
                _movementProfile.JumpReleaseVelocityMultiplierMilli,
                _movementProfile.ApexGravityMultiplierMilli,
                _movementProfile.FallGravityMultiplierMilli,
                _movementProfile.GetJumpBufferFrames(tickRate),
                _movementProfile.GetCoyoteTimeFrames(tickRate),
                _movementProfile.UpwardMotionGravityScaleMilli);
        }

        private CharacterMotionFacingInput BuildMotionFacingInput(
            CharacterTestCharacterEntity entity,
            short desiredMoveX,
            short desiredMoveY)
        {
            CharacterTestBehaviorControlInput control = entity.Input.BehaviorControl;
            if (control.IsPresent && control.FacingMode != 0)
                return new CharacterMotionFacingInput(true, control.FacingMode == 2,
                    entity.Transform.XMillimeters, entity.Transform.ZMillimeters, entity.Transform.YawMilliDegrees,
                    control.TargetXMillimeters, control.TargetZMillimeters, 0, 0, 0, 0, MoveFacingEpsilonMilli);
            CharacterTestCharacterMovementBasis basis = ResolveMovementBasis(entity);
            int targetX = 0;
            int targetZ = 0;
            if (basis.HasLockedTarget)
            {
                targetX = CharacterTestDeterministicUnits.MetersToMillimeters(basis.LockedTargetPosition.x);
                targetZ = CharacterTestDeterministicUnits.MetersToMillimeters(basis.LockedTargetPosition.y);
            }

            return new CharacterMotionFacingInput(
                IsControlIntentFacingDriven(entity.Control.Owner),
                basis.HasLockedTarget,
                entity.Transform.XMillimeters,
                entity.Transform.ZMillimeters,
                entity.Transform.YawMilliDegrees,
                targetX,
                targetZ,
                desiredMoveX,
                desiredMoveY,
                entity.DesiredVelocityXMillimetersPerSecond,
                entity.DesiredVelocityYMillimetersPerSecond,
                MoveFacingEpsilonMilli);
        }

        private CharacterKinematicPhysicsStepInput BuildKinematicPhysicsStepInput(
            CharacterTestCharacterEntity entity,
            CharacterKinematicMotionPrePhysicsResult prePhysics,
            bool hasRootMotion,
            int persistentHorizontalVelocityX,
            int persistentHorizontalVelocityY,
            int persistentVerticalVelocity,
            int previousX,
            int previousY,
            int previousZ,
            int wallSlideMinimumHorizontalSpeedMillimetersPerSecond)
        {
            int maxStepHeightMillimeters = _physicsRuntime.MaxStepHeight <= Fix64.Zero
                ? 0
                : CharacterTestDeterministicUnits.Fix64MetersToMillimeters(_physicsRuntime.MaxStepHeight);
            return new CharacterKinematicPhysicsStepInput(
                _physicsRuntime.GetEntityId(entity),
                _physicsRuntime.GetBodyId(entity),
                CombatFrame.Zero,
                entity.Transform.XMillimeters,
                entity.Transform.YMillimeters,
                entity.Transform.ZMillimeters,
                previousX,
                previousY,
                previousZ,
                _physicsRuntime.GetCharacterCenterOffsetY(entity),
                prePhysics.SimulatedHorizontalVelocityX,
                prePhysics.SimulatedHorizontalVelocityY,
                prePhysics.SimulatedVerticalVelocity,
                prePhysics.SimulatedImpactVerticalVelocity,
                prePhysics.WasGroundedForPhysics,
                prePhysics.ApplyGroundSurfaceFriction,
                hasRootMotion,
                persistentHorizontalVelocityX,
                persistentHorizontalVelocityY,
                persistentVerticalVelocity,
                prePhysics.RootMotionHorizontalVelocityX,
                prePhysics.RootMotionHorizontalVelocityY,
                prePhysics.RootMotionVerticalVelocity,
                prePhysics.SurfaceBounceSpeedCapY,
                maxStepHeightMillimeters,
                GroundContactProbeVelocityMillimetersPerSecond,
                MoveFacingEpsilonMilli,
                CharacterTestDeterministicUnits.InputAxisScale,
                wallSlideMinimumHorizontalSpeedMillimetersPerSecond);
        }

        private static int ResolveWallSlideMinimumHorizontalSpeed(
            CharacterTestCharacterEntity entity,
            CharacterKinematicMotionPrePhysicsResult prePhysics,
            CharacterMotionRequestFrame requestFrame)
        {
            return ResolveWallSlideMinimumHorizontalSpeed(
                prePhysics.HasActiveMoveInput,
                prePhysics.PreserveExternalHorizontalVelocity,
                prePhysics.SimulatedHorizontalVelocityX,
                prePhysics.SimulatedHorizontalVelocityY,
                prePhysics.TargetVelocityX,
                prePhysics.TargetVelocityY,
                requestFrame.HasRootMotion,
                requestFrame.HasVelocityOverride,
                requestFrame.HasHorizontalVelocityOverride,
                requestFrame.HasExternalAcceleration,
                requestFrame.HasImpulse,
                entity?.Physics.ContactVelocityXMillimetersPerSecond ?? 0,
                entity?.Physics.ContactVelocityYMillimetersPerSecond ?? 0);
        }

        internal static int ResolveWallSlideMinimumHorizontalSpeed(
            bool hasActiveMoveInput,
            bool preserveExternalHorizontalVelocity,
            int simulatedHorizontalVelocityX,
            int simulatedHorizontalVelocityY,
            int targetVelocityX,
            int targetVelocityY,
            bool hasRootMotion,
            bool hasVelocityOverride,
            bool hasHorizontalVelocityOverride,
            bool hasExternalAcceleration,
            bool hasImpulse,
            int contactVelocityX,
            int contactVelocityY)
        {
            if (!hasActiveMoveInput
                || preserveExternalHorizontalVelocity
                || hasRootMotion
                || hasVelocityOverride
                || hasHorizontalVelocityOverride
                || hasExternalAcceleration
                || hasImpulse
                || CharacterTestDeterministicMath.SqrMagnitude(
                    contactVelocityX,
                    contactVelocityY)
                    > CharacterContactVelocityEpsilonMillimetersPerSecond
                    * CharacterContactVelocityEpsilonMillimetersPerSecond)
            {
                return 0;
            }

            int simulatedSpeed = CharacterTestDeterministicMath.Magnitude(
                simulatedHorizontalVelocityX,
                simulatedHorizontalVelocityY);
            int targetSpeed = CharacterTestDeterministicMath.Magnitude(
                targetVelocityX,
                targetVelocityY);
            return System.Math.Min(simulatedSpeed, targetSpeed);
        }

        private void SyncCharacterCombatCollider(CharacterTestCharacterEntity entity)
        {
            if (_physicsRuntime == null || _physicsRuntime.World == null || entity == null)
                return;

            CombatBodyId bodyId = _physicsRuntime.GetBodyId(entity);
            CombatEntityId entityId = _physicsRuntime.GetEntityId(entity);
            CharacterTestColliderPostureComponent posture = entity.ColliderPosture;
            FixVector3 halfExtents = _physicsRuntime.GetCharacterHalfExtents(posture);
            FixVector3 centerPosition = new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.YMillimeters)
                    + _physicsRuntime.GetCharacterCenterOffsetY(posture),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.ZMillimeters));
            var colliderId = new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicCharacterColliderId);
            bool blocksCharacters = posture.BlocksCharacters;
            if (_physicsRuntime.IsCharacterColliderSynced(
                    entity.Id.Value,
                    blocksCharacters,
                    centerPosition,
                    halfExtents)
                && HasCurrentCharacterPhysicsRecord(bodyId, colliderId, blocksCharacters))
            {
                return;
            }

            if (_physicsRuntime.IsCharacterColliderShapeSynced(
                    entity.Id.Value,
                    blocksCharacters,
                    halfExtents)
                && HasCurrentCharacterPhysicsRecord(bodyId, colliderId, blocksCharacters))
            {
                _physicsRuntime.World.SetBodyPosition(bodyId, centerPosition);
                _physicsRuntime.MarkCharacterColliderSynced(
                    entity.Id.Value,
                    blocksCharacters,
                    centerPosition,
                    halfExtents);
                return;
            }

            _physicsRuntime.World.UpsertBody(new CombatPhysicsBody(entityId, bodyId, centerPosition));
            if (!blocksCharacters)
            {
                _physicsRuntime.World.RemoveCollider(bodyId, colliderId);
                _physicsRuntime.MarkCharacterColliderSynced(
                    entity.Id.Value,
                    blocksCharacters,
                    centerPosition,
                    halfExtents);
                return;
            }

            _physicsRuntime.World.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                colliderId,
                CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer,
                new FixVector3(
                    -halfExtents.X,
                    -halfExtents.Y,
                    -halfExtents.Z),
                halfExtents));
            _physicsRuntime.MarkCharacterColliderSynced(
                entity.Id.Value,
                blocksCharacters,
                centerPosition,
                halfExtents);

            CombatBodyId hurtboxBodyId = _physicsRuntime.GetHurtboxBodyId(entity);
            FixVector3 rootPosition = new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.YMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.ZMillimeters));
            if (_physicsRuntime.World.TryGetBody(hurtboxBodyId, out _))
            {
                _physicsRuntime.World.SetBodyPosition(hurtboxBodyId, rootPosition);
            }
        }

        private bool HasCurrentCharacterPhysicsRecord(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            bool blocksCharacters)
        {
            if (!_physicsRuntime.World.TryGetBody(bodyId, out _))
                return false;

            bool hasCollider = _physicsRuntime.World.TryGetAabbCollider(bodyId, colliderId, out _);
            return blocksCharacters ? hasCollider : !hasCollider;
        }

        private void SyncAllCharacterCombatColliders(IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            if (entities == null)
                return;

            for (int i = 0; i < entities.Count; i++)
                SyncCharacterCombatCollider(entities[i]);
        }

        void ICharacterTestCharacterColliderSync.SyncAllCharacterCombatColliders(
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            SyncAllCharacterCombatColliders(entities);
            _bodyPresenceSystem.RebuildAll(entities, _physicsRuntime);
        }

        private CombatKinematicMotor EnsurePhysicsMotor(
            int ticksPerSecond,
            CharacterMovementProfileConfig profile,
            CharacterTestColliderPostureComponent posture,
            CombatPhysicsLayerMask obstacleLayerMask)
        {
            ticksPerSecond = ticksPerSecond <= 0
                ? CombatMotionConfig.DefaultTicksPerSecond
                : ticksPerSecond;
            Fix64 groundMinNormalY = ResolveGroundMinNormalY(profile);
            Fix64 fixedBounceGravityPerSecond = ResolveFixedBounceGravityPerSecond(_movementProfile);
            CombatMotionConfig config = _physicsRuntime.CreateMotionConfig(
                ticksPerSecond,
                groundMinNormalY,
                posture,
                fixedBounceGravityPerSecond,
                obstacleLayerMask);
            if (_physicsMotorCache.TryGetValue(config, out CombatKinematicMotor motor))
                return motor;

            motor = new CombatKinematicMotor(config);
            _physicsMotorCache.Add(config, motor);
            return motor;
        }

        private static CombatPhysicsLayerMask CreateDynamicMotionObstacleMask(
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            return new CombatPhysicsLayerMask(
                physicsRuntime.DynamicCharacterObstacleLayerMask.Value
                | physicsRuntime.DynamicMovableObjectObstacleLayerMask.Value);
        }

        private CombatPhysicsLayerMask ResolveMotionObstacleLayerMask(
            CharacterMotionRequestFrame requestFrame)
        {
            if (_physicsRuntime == null)
                return CombatPhysicsLayerMask.None;

            return requestFrame.HasRootMotion
                && requestFrame.RootMotionObstaclePolicy == CharacterMotionObstaclePolicy.StaticOnly
                ? _physicsRuntime.StaticObstacleLayerMask
                : _physicsRuntime.MotionObstacleLayerMask;
        }

        private static Fix64 ResolveGroundMinNormalY(CharacterMovementProfileConfig profile)
        {
            int slopeLimit = profile != null
                ? CharacterTestDeterministicUnits.DegreesToMilliDegrees(profile.SlopeLimitDegrees)
                : 60000;
            slopeLimit = CharacterTestDeterministicMath.Clamp(slopeLimit, 0, 89900);
            return CharacterTestDeterministicMath.CosFix64FromMilliDegrees(slopeLimit);
        }

        private static Fix64 ResolveFixedBounceGravityPerSecond(
            CharacterTestCharacterMovementProfileRuntime movementProfile)
        {
            int gravity = movementProfile.GravityMillimetersPerSecondSquared;
            if (gravity == int.MinValue)
                gravity = int.MaxValue;
            else if (gravity < 0)
                gravity = -gravity;

            return CharacterTestDeterministicUnits.MillimetersToFix64Meters(gravity);
        }

        private static CombatSurfaceMaterial ResolveCurrentGroundSurfaceMaterial(
            CharacterTestCharacterEntity entity,
            bool grounded)
        {
            return grounded && entity != null
                ? entity.SurfaceMaterial
                : CombatSurfaceMaterial.Default;
        }

        private static CharacterMotionRequestFrame BuildMotionRequestFrame(CharacterTestCharacterEntity entity)
        {
            var accumulator = new CharacterMotionRequestAccumulator();
            accumulator.Reset();
            IReadOnlyList<CharacterTestCharacterMotionRequest> requests = entity.MotionRequests;
            for (int i = 0; i < requests.Count; i++)
            {
                CharacterMotionRequest request = requests[i].ToCharacterMotionRequest();
                accumulator.Accumulate(
                    in request,
                    entity.Transform.YawMilliDegrees,
                    MotionLocalSpaceResolver);
            }

            return accumulator.ToFrame();
        }

        private static void LocalToWorld(
            int yawMilliDegrees,
            int localX,
            int localY,
            int localZ,
            out int worldX,
            out int worldY,
            out int worldZ)
        {
            CharacterTestDeterministicMath.SinCosMilliDegrees(yawMilliDegrees, out int sin, out int cos);
            worldX = CharacterTestDeterministicMath.DivideRoundToInt(
                (long)localX * cos + (long)localZ * sin,
                CharacterTestDeterministicMath.TrigScale);
            worldY = localY;
            worldZ = CharacterTestDeterministicMath.DivideRoundToInt(
                -(long)localX * sin + (long)localZ * cos,
                CharacterTestDeterministicMath.TrigScale);
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrEmpty(value) ? "none" : value;
        }

        private static string EmptyAsNone(CharacterMotionRequestSource source)
        {
            if (source.IsEmpty)
                return "none";

            if (!source.SourceActionId.IsValid)
                return source.StableEventId.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (source.StableEventId == 0)
                return source.SourceActionId.ToString();

            return source.SourceActionId + ":" + source.StableEventId;
        }

        private static bool IsControlIntentFacingDriven(CharacterTestCharacterControlOwner owner)
        {
            return owner == CharacterTestCharacterControlOwner.LocalPlayer
                || owner == CharacterTestCharacterControlOwner.RemotePlayer
                || owner == CharacterTestCharacterControlOwner.AiControlled;
        }

        private static int DividePerTick(int value, int tickRate)
        {
            return CharacterTestDeterministicMath.DivideRoundToInt(value, tickRate <= 0 ? CharacterTestDeterministicUnits.DefaultTickRate : tickRate);
        }

        private static void ClampMoveMilli(
            short inputX,
            short inputY,
            out short outputX,
            out short outputY)
        {
            int magnitude = CharacterTestDeterministicMath.Magnitude(inputX, inputY);
            if (magnitude <= CharacterTestDeterministicUnits.InputAxisScale)
            {
                outputX = inputX;
                outputY = inputY;
                return;
            }

            outputX = (short)CharacterTestDeterministicMath.DivideRoundToInt(
                (long)inputX * CharacterTestDeterministicUnits.InputAxisScale,
                magnitude);
            outputY = (short)CharacterTestDeterministicMath.DivideRoundToInt(
                (long)inputY * CharacterTestDeterministicUnits.InputAxisScale,
                magnitude);
        }

        private sealed class CharacterTestMotionLocalSpaceResolver : ICharacterMotionLocalSpaceResolver
        {
            public void ResolveLocalToWorld(
                int yawMilliDegrees,
                int localX,
                int localY,
                int localZ,
                out int worldX,
                out int worldY,
                out int worldZ)
            {
                LocalToWorld(
                    yawMilliDegrees,
                    localX,
                    localY,
                    localZ,
                    out worldX,
                    out worldY,
                    out worldZ);
            }
        }

    }
}
