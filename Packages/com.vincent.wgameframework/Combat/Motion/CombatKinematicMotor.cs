using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public sealed partial class CombatKinematicMotor
    {
        private static readonly FixVector3 AxisX = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
        private static readonly FixVector3 AxisY = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
        private static readonly FixVector3 AxisZ = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
        private static readonly Fix64 LargeNegative = Fix64.FromRaw(long.MinValue / 4);
        private static readonly Fix64 LargePositive = Fix64.FromRaw(long.MaxValue / 4);

        private readonly List<CombatMotionCollision> _collisions;
        private readonly List<CombatPhysicsBroadphaseCandidate> _stepCachedBroadphaseCandidates;
        private readonly List<int> _stepBaseEligibleBroadphaseCandidateIndices;
        private readonly List<int> _stepEligibleBroadphaseCandidateIndices;
        private IReadOnlyList<CombatPhysicsBroadphaseCandidate> _stepBroadphaseCandidateView =
            Array.Empty<CombatPhysicsBroadphaseCandidate>();
        private FixVector3 _stepBaseQueryMin;
        private FixVector3 _stepBaseQueryMax;
        private FixVector3 _stepUpQueryMin;
        private FixVector3 _stepUpQueryMax;
        private FixVector3 _stepStartMin;
        private FixVector3 _stepStartMax;
        private FixVector3 _stepDesiredDelta;
        private bool _stepBroadphaseCacheActive;
        private bool _stepBroadphaseCacheIncludesStepUpEnvelope;
        private bool _stepHasStepUpEnvelope;
        private int _lastQueryCount;
        private int _lastBroadphaseCandidateCount;
        private int _lastBroadphaseCellCount;
        private int _lastNarrowphaseCandidateCount;
        private int _lastEarlyCandidateSkipCount;
        private int _lastStepUpProbeCount;
        private int _lastCandidateCacheHitCount;
        private int _lastCandidateCacheFallbackCount;

        public CombatKinematicMotor()
            : this(CombatMotionConfig.Default)
        {
        }

        public CombatKinematicMotor(CombatMotionConfig config)
        {
            Config = config;
            int scratchCapacity = Math.Max(0, config.MaxCollisionIterations);
            _collisions = new List<CombatMotionCollision>(scratchCapacity);
            _stepCachedBroadphaseCandidates = new List<CombatPhysicsBroadphaseCandidate>(scratchCapacity);
            _stepBaseEligibleBroadphaseCandidateIndices = new List<int>(scratchCapacity);
            _stepEligibleBroadphaseCandidateIndices = new List<int>(scratchCapacity);
        }

        public CombatMotionConfig Config { get; }

        public CombatKinematicMotorQueryStats LastQueryStats => new CombatKinematicMotorQueryStats(
            _lastQueryCount,
            _lastBroadphaseCandidateCount,
            _lastBroadphaseCellCount,
            _lastNarrowphaseCandidateCount,
            _lastEarlyCandidateSkipCount,
            _lastStepUpProbeCount,
            _lastCandidateCacheHitCount,
            _lastCandidateCacheFallbackCount);

        public CombatMotionStepResult Step(CombatMotionState state, CombatMotionInput input)
        {
            return Step(state, input, null, CombatEntityId.None, CombatBodyId.None, false);
        }

        /// <summary>
        /// Advances one kinematic step using a caller-prepared candidate cache and syncs the result body position.
        /// </summary>
        /// <remarks>NoAlloc after initialization when the motor, buffer, world, and candidate cache are warmed.</remarks>
        public CombatMotionStepResult Step(
            CombatPhysicsWorld physicsWorld,
            CombatBodyId syncBodyId,
            CombatMotionState state,
            CombatMotionInput input)
        {
            return Step(physicsWorld, syncBodyId, state, input, null);
        }

        public CombatMotionStepResult Step(
            CombatPhysicsWorld physicsWorld,
            CombatBodyId syncBodyId,
            CombatMotionState state,
            CombatMotionInput input,
            FixVector3? impactVelocityOverride)
        {
            return Step(physicsWorld, syncBodyId, state, input, impactVelocityOverride, null);
        }

        public CombatMotionStepResult Step(
            CombatPhysicsWorld physicsWorld,
            CombatBodyId syncBodyId,
            CombatMotionState state,
            CombatMotionInput input,
            FixVector3? impactVelocityOverride,
            CombatMotionStepBuffer buffer)
        {
            CombatEntityId sourceEntityId = CombatEntityId.None;
            if (physicsWorld != null && physicsWorld.TryGetBody(syncBodyId, out CombatPhysicsBody body))
            {
                sourceEntityId = body.EntityId;
            }

            return Step(
                state,
                input,
                physicsWorld,
                sourceEntityId,
                syncBodyId,
                impactVelocityOverride: impactVelocityOverride,
                buffer: buffer);
        }

        public CombatMotionStepResult Step(
            CombatPhysicsWorld physicsWorld,
            CombatBodyId syncBodyId,
            CombatMotionState state,
            CombatMotionInput input,
            FixVector3? impactVelocityOverride,
            CombatMotionStepBuffer buffer,
            CombatKinematicMotorCandidateCache candidateCache)
        {
            return Step(
                physicsWorld,
                syncBodyId,
                state,
                input,
                impactVelocityOverride,
                buffer,
                candidateCache,
                default);
        }

        public CombatMotionStepResult Step(
            CombatPhysicsWorld physicsWorld,
            CombatBodyId syncBodyId,
            CombatMotionState state,
            CombatMotionInput input,
            FixVector3? impactVelocityOverride,
            CombatMotionStepBuffer buffer,
            CombatKinematicMotorCandidateCache candidateCache,
            CombatMotionStepOptions options)
            => Step(physicsWorld, syncBodyId, state, input, impactVelocityOverride, buffer, candidateCache, options, true);

        /// <summary>Queries a configured step without body synchronization when syncBodyPosition is false.</summary>
        /// <remarks>NoAlloc after initialization. The caller still owns motor and buffer scratch.</remarks>
        public CombatMotionStepResult Step(CombatPhysicsWorld physicsWorld, CombatBodyId syncBodyId,
            CombatMotionState state, CombatMotionInput input, FixVector3? impactVelocityOverride,
            CombatMotionStepBuffer buffer, CombatKinematicMotorCandidateCache candidateCache,
            CombatMotionStepOptions options, bool syncBodyPosition)
        {
            CombatEntityId sourceEntityId = CombatEntityId.None;
            if (physicsWorld != null && physicsWorld.TryGetBody(syncBodyId, out CombatPhysicsBody body))
            {
                sourceEntityId = body.EntityId;
            }

            return StepCore(
                state,
                input,
                physicsWorld,
                sourceEntityId,
                syncBodyId,
                syncBodyPosition: syncBodyPosition,
                impactVelocityOverride: impactVelocityOverride,
                buffer: buffer,
                candidateCache: candidateCache,
                options: options);
        }

        /// <summary>
        /// Advances one kinematic step against a physics world and optionally syncs the result body position.
        /// </summary>
        /// <remarks>NoAlloc after initialization when the motor, buffer, and world are warmed.</remarks>
        public CombatMotionStepResult Step(
            CombatMotionState state,
            CombatMotionInput input,
            CombatPhysicsWorld physicsWorld,
            CombatEntityId sourceEntityId,
            CombatBodyId syncBodyId,
            bool syncBodyPosition = true,
            FixVector3? impactVelocityOverride = null,
            CombatMotionStepBuffer buffer = null)
        {
            return StepCore(
                state,
                input,
                physicsWorld,
                sourceEntityId,
                syncBodyId,
                syncBodyPosition,
                impactVelocityOverride,
                buffer,
                candidateCache: null,
                options: default);
        }

        /// <summary>
        /// Advances one kinematic step using a caller-prepared candidate cache when it covers the obstacle mask.
        /// </summary>
        /// <remarks>NoAlloc after initialization when the motor, buffer, world, and candidate cache are warmed.</remarks>
        public CombatMotionStepResult Step(
            CombatMotionState state,
            CombatMotionInput input,
            CombatPhysicsWorld physicsWorld,
            CombatEntityId sourceEntityId,
            CombatBodyId syncBodyId,
            bool syncBodyPosition,
            FixVector3? impactVelocityOverride,
            CombatMotionStepBuffer buffer,
            CombatKinematicMotorCandidateCache candidateCache)
        {
            return StepCore(
                state,
                input,
                physicsWorld,
                sourceEntityId,
                syncBodyId,
                syncBodyPosition,
                impactVelocityOverride,
                buffer,
                candidateCache,
                options: default);
        }

        private CombatMotionStepResult StepCore(
            CombatMotionState state,
            CombatMotionInput input,
            CombatPhysicsWorld physicsWorld,
            CombatEntityId sourceEntityId,
            CombatBodyId syncBodyId,
            bool syncBodyPosition,
            FixVector3? impactVelocityOverride,
            CombatMotionStepBuffer buffer,
            CombatKinematicMotorCandidateCache candidateCache,
            CombatMotionStepOptions options)
        {
            _collisions.Clear();
            ResetQueryStats();

            bool jumpStarted = input.JumpPressed && state.Grounded;
            FixVector3 horizontalDirection = ClampHorizontalDirection(input.MoveDirection);
            Fix64 moveScale = input.MoveSpeedScale;
            Fix64 tick = Fix64.FromInt(Config.TicksPerSecond);
            FixVector3 horizontalVelocity = horizontalDirection * (Config.MoveSpeed * moveScale);

            Fix64 verticalVelocity = state.Velocity.Y;
            if (jumpStarted)
            {
                verticalVelocity = Config.JumpSpeed;
            }
            else if (state.Grounded && verticalVelocity < Fix64.Zero && Config.GravityPerSecond < Fix64.Zero)
            {
                verticalVelocity = Fix64.Zero;
            }

            Fix64 preGravityVerticalVelocity = verticalVelocity;
            bool hasHorizontalIntent = !horizontalDirection.X.IsZero || !horizontalDirection.Z.IsZero;
            FixVector3 horizontalDelta = new FixVector3(
                horizontalVelocity.X,
                Fix64.Zero,
                horizontalVelocity.Z) / tick;
            bool hasExplicitUpwardIntent = jumpStarted
                || state.Velocity.Y > Fix64.Zero
                || (impactVelocityOverride.HasValue && impactVelocityOverride.Value.Y > Fix64.Zero)
                || options.SuppressGroundSnap;
            bool supportOnlyDownward = Config.GroundSnapDistance > Fix64.Zero
                && state.Grounded
                && !hasHorizontalIntent
                && !hasExplicitUpwardIntent
                && preGravityVerticalVelocity <= Fix64.Zero;

            FixVector3 impactVelocity = impactVelocityOverride
                ?? new FixVector3(horizontalVelocity.X, verticalVelocity, horizontalVelocity.Z);
            verticalVelocity += Config.GravityPerStep;
            if (verticalVelocity < Config.MaxFallSpeed)
            {
                verticalVelocity = Config.MaxFallSpeed;
            }

            FixVector3 desiredVelocity = new FixVector3(horizontalVelocity.X, verticalVelocity, horizontalVelocity.Z);
            FixVector3 desiredDelta = supportOnlyDownward
                ? horizontalDelta
                : desiredVelocity / tick;
            Fix64 wallSlideMinimumHorizontalDelta =
                options.WallSlideMinimumHorizontalSpeed > Fix64.Zero
                    ? options.WallSlideMinimumHorizontalSpeed / tick
                    : Fix64.Zero;
            FixVector3 startPosition = state.Position;
            FixVector3 resolvedPosition = ResolveCollisions(
                state.Frame,
                startPosition,
                desiredDelta,
                wallSlideMinimumHorizontalDelta,
                state.Grounded,
                physicsWorld,
                sourceEntityId,
                candidateCache,
                out CombatMotionCollisionFlags collisionFlags);
            if ((collisionFlags & CombatMotionCollisionFlags.IterationLimit) != 0
                && Config.GroundSnapDistance > Fix64.Zero
                && state.Grounded
                && hasHorizontalIntent
                && !hasExplicitUpwardIntent
                && preGravityVerticalVelocity <= Fix64.Zero
                && desiredDelta.Y < Fix64.Zero)
            {
                _collisions.Clear();
                resolvedPosition = ResolveCollisions(
                    state.Frame,
                    startPosition,
                    horizontalDelta,
                    wallSlideMinimumHorizontalDelta,
                    state.Grounded,
                    physicsWorld,
                    sourceEntityId,
                    candidateCache,
                    out collisionFlags);
            }
            if (IsGroundSnapEligible(
                    state,
                    jumpStarted,
                    desiredDelta,
                    impactVelocityOverride,
                    options,
                    collisionFlags)
                && TryApplyGroundSupport(
                    resolvedPosition,
                    physicsWorld,
                    sourceEntityId,
                    candidateCache,
                    out FixVector3 supportedPosition,
                    out CombatMotionCollision supportCollision))
            {
                resolvedPosition = supportedPosition;
                collisionFlags |= supportCollision.Flags;
                _collisions.Add(supportCollision);
            }
            FixVector3 appliedDelta = resolvedPosition - startPosition;
            bool grounded = (collisionFlags & CombatMotionCollisionFlags.Grounded) != 0;
            CombatSurfaceMaterial surfaceMaterial = ResolveContactSurfaceMaterial();
            FixVector3 resolvedVelocity = ResolveVelocityFromAppliedDelta(
                desiredVelocity,
                appliedDelta,
                collisionFlags,
                tick);
            resolvedVelocity = ApplySurfaceBounce(
                resolvedVelocity,
                impactVelocity,
                Config.FixedBounceGravityPerSecond,
                Config.MinElasticImpactSpeed,
                out FixVector3 reboundNormal);
            if (!reboundNormal.IsZero && resolvedVelocity.Dot(reboundNormal) > Fix64.Zero)
            {
                collisionFlags = ResolveCollisionFlagsAfterBounce(collisionFlags, reboundNormal);
                grounded = (collisionFlags & CombatMotionCollisionFlags.Grounded) != 0;
            }

            resolvedVelocity = ApplySurfaceFriction(
                resolvedVelocity,
                collisionFlags,
                surfaceMaterial.Friction,
                tick,
                input.ApplyGroundSurfaceFriction);
            FixVector3 lastNormal = _collisions.Count == 0 ? FixVector3.Zero : _collisions[_collisions.Count - 1].Normal;
            CombatMotionState nextState = new CombatMotionState(
                state.Frame.Next(),
                resolvedPosition,
                resolvedVelocity,
                grounded,
                lastNormal,
                collisionFlags);

            if (syncBodyPosition && physicsWorld != null && !syncBodyId.IsNone)
            {
                if (physicsWorld.SetBodyPosition(syncBodyId, resolvedPosition))
                {
                    candidateCache?.UpdateDynamicBody(physicsWorld, syncBodyId);
                }
            }

            return new CombatMotionStepResult(
                nextState,
                desiredDelta,
                appliedDelta,
                jumpStarted,
                collisionFlags,
                CreateCollisionResult(buffer),
                surfaceMaterial);
        }

        private void ResetQueryStats()
        {
            _lastQueryCount = 0;
            _lastBroadphaseCandidateCount = 0;
            _lastBroadphaseCellCount = 0;
            _lastNarrowphaseCandidateCount = 0;
            _lastEarlyCandidateSkipCount = 0;
            _lastStepUpProbeCount = 0;
            _lastCandidateCacheHitCount = 0;
            _lastCandidateCacheFallbackCount = 0;
        }

        private IReadOnlyList<CombatMotionCollision> CreateCollisionResult(CombatMotionStepBuffer buffer)
        {
            if (_collisions.Count == 0)
                return Array.Empty<CombatMotionCollision>();

            return buffer == null
                ? _collisions.ToArray()
                : buffer.CopyCollisionsFrom(_collisions);
        }

        public void GetCharacterAabb(FixVector3 position, out FixVector3 min, out FixVector3 max)
        {
            Config.CapsuleProxy.GetBounds(position, out min, out max);
        }

    }

    public readonly struct CombatKinematicMotorQueryStats
    {
        public CombatKinematicMotorQueryStats(
            int queryCount,
            int broadphaseCandidateCount,
            int broadphaseCellCount,
            int narrowphaseCandidateCount,
            int earlyCandidateSkipCount,
            int stepUpProbeCount)
            : this(
                queryCount,
                broadphaseCandidateCount,
                broadphaseCellCount,
                narrowphaseCandidateCount,
                earlyCandidateSkipCount,
                stepUpProbeCount,
                0,
                0)
        {
        }

        public CombatKinematicMotorQueryStats(
            int queryCount,
            int broadphaseCandidateCount,
            int broadphaseCellCount,
            int narrowphaseCandidateCount,
            int earlyCandidateSkipCount,
            int stepUpProbeCount,
            int candidateCacheHitCount,
            int candidateCacheFallbackCount)
        {
            QueryCount = queryCount;
            BroadphaseCandidateCount = broadphaseCandidateCount;
            BroadphaseCellCount = broadphaseCellCount;
            NarrowphaseCandidateCount = narrowphaseCandidateCount;
            EarlyCandidateSkipCount = earlyCandidateSkipCount;
            StepUpProbeCount = stepUpProbeCount;
            CandidateCacheHitCount = candidateCacheHitCount;
            CandidateCacheFallbackCount = candidateCacheFallbackCount;
        }

        public int QueryCount { get; }

        public int BroadphaseCandidateCount { get; }

        public int BroadphaseCellCount { get; }

        public int NarrowphaseCandidateCount { get; }

        public int EarlyCandidateSkipCount { get; }

        public int StepUpProbeCount { get; }

        /// <summary>
        /// Gets how many step broadphase queries were served by a supplied candidate cache.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public int CandidateCacheHitCount { get; }

        /// <summary>
        /// Gets how many step broadphase queries fell back to the world broadphase.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public int CandidateCacheFallbackCount { get; }
    }
}
