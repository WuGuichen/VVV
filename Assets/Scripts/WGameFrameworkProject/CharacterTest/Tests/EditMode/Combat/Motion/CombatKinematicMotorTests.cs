using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Motion
{
    public class CombatKinematicMotorTests
    {
        private static readonly CombatEntityId PlayerEntityId = new CombatEntityId(1);
        private static readonly CombatBodyId PlayerBodyId = new CombatBodyId(1);
        private static readonly CombatEntityId TargetEntityId = new CombatEntityId(20);
        private static readonly CombatBodyId TargetBodyId = new CombatBodyId(20);
        private static readonly CombatPhysicsLayerMask BlockingMask = CombatPhysicsLayerMask.FromLayer(1);
        private static readonly CombatPhysicsLayerMask TargetMask = CombatPhysicsLayerMask.FromLayer(2);
        private static readonly FixVector3 CharacterHalfExtents = new FixVector3(Fix64.Half, Fix64.One, Fix64.Half);

        [Test]
        public void FixedStepDeterminism_ReplayingSameInputsProducesSameFinalState()
        {
            CombatMotionSnapshot first = ReplayDeterminismSequence();
            CombatMotionSnapshot second = ReplayDeterminismSequence();

            Assert.AreEqual(first.Position, second.Position);
            Assert.AreEqual(first.Velocity, second.Velocity);
            Assert.AreEqual(first.Grounded, second.Grounded);
            Assert.AreEqual(first.CollisionFlags, second.CollisionFlags);
            Assert.AreEqual(first.WorldPosition, second.WorldPosition);
            Assert.AreEqual(first.WorldRevision, second.WorldRevision);
        }

        [Test]
        public void GravityLanding_FallsOntoGroundAndClampsVerticalVelocity()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: new FixVector3(Fix64.Zero, Fix64.FromInt(6), Fix64.Zero));
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(
                frame: 0,
                position: new FixVector3(Fix64.Zero, Fix64.FromInt(6), Fix64.Zero),
                velocity: FixVector3.Zero,
                grounded: false);

            CombatMotionStepResult result = default;
            for (int i = 0; i < 180; i++)
            {
                result = motor.Step(world, PlayerBodyId, state, CreateInput(FixVector3.Zero, jumpPressed: false));
                state = result.State;
                if (state.Grounded)
                {
                    break;
                }
            }

            Assert.IsTrue(state.Grounded, "The character should become grounded after falling onto the ground.");
            Assert.IsTrue(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.Grounded));
            Assert.IsTrue(state.Velocity.Y <= Fix64.Zero);
            Assert.AreEqual(Fix64.Zero, state.Velocity.Y, "Landing should clear or clamp downward vertical velocity to zero for v0.");
            Assert.AreEqual(state.CollisionFlags, result.CollisionFlags);
            Assert.IsTrue(world.TryGetBody(PlayerBodyId, out CombatPhysicsBody body));
            Assert.AreEqual(state.Position, body.Position);
        }

        [Test]
        public void GroundedContact_StaysGroundedAndDoesNotAccumulateFallVelocity()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            for (int i = 0; i < 12; i++)
            {
                CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(FixVector3.Zero, jumpPressed: false));
                state = result.State;

                Assert.IsTrue(state.Grounded, "A character already resting on the ground should remain grounded.");
                Assert.IsTrue(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.Grounded));
                Assert.AreEqual(Fix64.Zero, state.Velocity.Y);
                Assert.AreEqual(GroundedStart.Y, state.Position.Y);
            }
        }

        [Test]
        public void GroundedContact_SurfaceFrictionZeroPreservesGroundedHorizontalVelocity()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(
                startPosition: GroundedStart,
                groundSurfaceFriction: Fix64.Zero);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));

            Assert.AreEqual(Fix64.Zero, result.SurfaceFriction);
            Assert.AreEqual(Fix64.FromInt(6), result.State.Velocity.X);
            Assert.AreEqual(Fix64.Zero, result.State.Velocity.Z);
            Assert.IsTrue(result.State.Grounded);
        }

        [Test]
        public void GroundedContact_SurfaceFrictionReducesGroundedHorizontalVelocity()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(
                startPosition: GroundedStart,
                groundSurfaceFriction: Fix64.FromInt(60));
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));

            Assert.AreEqual(Fix64.FromInt(60), result.SurfaceFriction);
            Assert.IsTrue((result.State.Velocity.X - Fix64.FromInt(5)).Abs() <= Fix64.FromRatio(1, 1000));
            Assert.AreEqual(Fix64.Zero, result.State.Velocity.Z);
            Assert.IsTrue(result.State.Grounded);
        }

        [Test]
        public void GroundedContact_ControlledDriveCanBypassGroundSurfaceFriction()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(
                startPosition: GroundedStart,
                groundSurfaceFriction: Fix64.FromInt(60));
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            CombatMotionInput input = CreateInput(UnitX, jumpPressed: false, applyGroundSurfaceFriction: false);
            CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, input);

            Assert.AreEqual(Fix64.FromInt(60), result.SurfaceFriction);
            Assert.AreEqual(Fix64.FromInt(6), result.State.Velocity.X);
            Assert.AreEqual(Fix64.Zero, result.State.Velocity.Z);
            Assert.IsTrue(result.State.Grounded);
        }

        [Test]
        public void GroundedContact_SurfaceMaterialPropagatesToCollisionAndStepResult()
        {
            CombatSurfaceMaterial rough = CombatSurfaceMaterial.Rough;
            CombatPhysicsWorld world = CreateWorldWithGround(
                startPosition: GroundedStart,
                groundSurfaceMaterial: rough);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));

            Assert.AreEqual(rough, result.SurfaceMaterial);
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, result.SurfaceMaterialKind);
            Assert.AreEqual("rough", result.SurfaceMaterialStableId);
            Assert.AreEqual(rough.Friction, result.SurfaceFriction);
            Assert.AreEqual(rough.Elasticity, result.SurfaceElasticity);
            Assert.Greater(result.CollisionCount, 0);
            Assert.AreEqual(rough, result.Collisions[0].SurfaceMaterial);
        }

        [Test]
        public void GroundedContact_OverlappingDefaultGroundPrefersNonDefaultSurfaceMaterial()
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, GroundedStart));
            RegisterStaticAabb(world, entity: 2, body: 2, collider: 1, layer: 1, position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromInt(20), -Fix64.FromRatio(5, 100), -Fix64.FromInt(20)),
                localMax: new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)),
                surfaceMaterial: CombatSurfaceMaterial.FromFriction(Fix64.FromInt(10)));
            RegisterStaticAabb(world, entity: 3, body: 3, collider: 1, layer: 1, position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromInt(2), -Fix64.FromRatio(4, 100), -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(2), Fix64.FromRatio(4, 100), Fix64.FromInt(2)),
                surfaceMaterial: CombatSurfaceMaterial.HardSlick);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));

            Assert.AreEqual(CombatSurfaceMaterialKind.HardSlick, result.SurfaceMaterialKind);
            Assert.AreEqual(CombatSurfaceMaterial.HardSlick, result.SurfaceMaterial);
            Assert.Greater(result.CollisionCount, 0);
            Assert.AreEqual(CombatSurfaceMaterialKind.HardSlick, result.Collisions[0].SurfaceMaterialKind);
        }

        [Test]
        public void Step_QueryBroadphaseDoesNotAllocateAfterScratchWarmup()
        {
            var world = new CombatPhysicsWorld();
            FixVector3 start = new FixVector3(Fix64.Zero, Fix64.FromInt(5), Fix64.Zero);
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, start));
            for (int i = 0; i < 48; i++)
            {
                Fix64 offset = Fix64.FromRatio(92 + i % 4, 100);
                Fix64 minY = Fix64.FromRatio(450 + i, 100);
                RegisterStaticAabb(
                    world,
                    entity: 100 + i,
                    body: 100 + i,
                    collider: 1,
                    layer: 1,
                    position: FixVector3.Zero,
                    localMin: new FixVector3(offset, minY, offset),
                    localMax: new FixVector3(
                        offset + Fix64.FromRatio(3, 100),
                        minY + Fix64.FromRatio(5, 100),
                        offset + Fix64.FromRatio(3, 100)));
            }

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(
                frame: 0,
                position: start,
                velocity: FixVector3.Zero,
                grounded: false);
            CombatMotionInput input = CreateInput(FixVector3.Zero, jumpPressed: false);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4; i++)
            {
                CombatMotionStepResult warmup = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false);
                Assert.AreEqual(0, warmup.CollisionCount);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                CombatMotionStepResult result = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false);
                Assert.AreEqual(0, result.CollisionCount);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void Step_BufferedCollisionResultDoesNotAllocateAfterWarmup()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            var buffer = new CombatMotionStepBuffer();
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);
            CombatMotionInput input = CreateInput(UnitX, jumpPressed: false);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4; i++)
            {
                CombatMotionStepResult warmup = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false,
                    impactVelocityOverride: null,
                    buffer: buffer);
                Assert.Greater(warmup.CollisionCount, 0);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                CombatMotionStepResult result = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false,
                    impactVelocityOverride: null,
                    buffer: buffer);
                Assert.Greater(result.CollisionCount, 0);
                Assert.AreEqual(CombatSurfaceMaterialKind.Default, result.Collisions[0].SurfaceMaterialKind);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void Step_BufferedCollisionResultDoesNotAllocateOnFirstCollisionAfterQueryWarmup()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            CombatMotionConfig config = CreateConfig();
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);
            CombatMotionInput input = CreateInput(UnitX, jumpPressed: false);

            var warmupMotor = new CombatKinematicMotor(config);
            _ = warmupMotor.Step(
                state,
                input,
                world,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                impactVelocityOverride: null,
                buffer: new CombatMotionStepBuffer(CombatMotionConfig.DefaultMaxCollisionIterations));

            var measuredMotor = new CombatKinematicMotor(config);
            var measuredBuffer = new CombatMotionStepBuffer(CombatMotionConfig.DefaultMaxCollisionIterations);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            CombatMotionStepResult result = measuredMotor.Step(
                state,
                input,
                world,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                impactVelocityOverride: null,
                buffer: measuredBuffer);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Greater(result.CollisionCount, 0);
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void Step_BufferedDirectCandidateCollisionDoesNotAllocateAfterWarmup()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            for (int i = 0; i < 64; i++)
            {
                Fix64 x = Fix64.FromRatio(-80 + (i % 16) * 10, 100);
                Fix64 y = Fix64.FromRatio(230 + (i / 16) * 5, 100);
                RegisterStaticAabb(
                    world,
                    entity: 300 + i,
                    body: 300 + i,
                    collider: 1,
                    layer: 1,
                    position: FixVector3.Zero,
                    localMin: new FixVector3(x, y, -Fix64.FromRatio(1, 10)),
                    localMax: new FixVector3(
                        x + Fix64.FromRatio(3, 100),
                        y + Fix64.FromRatio(3, 100),
                        Fix64.FromRatio(1, 10)));
            }

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            var buffer = new CombatMotionStepBuffer();
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);
            CombatMotionInput input = CreateInput(UnitX, jumpPressed: false);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 8; i++)
            {
                CombatMotionStepResult warmup = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false,
                    impactVelocityOverride: null,
                    buffer: buffer);
                Assert.Greater(warmup.CollisionCount, 0);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                CombatMotionStepResult result = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false,
                    impactVelocityOverride: null,
                    buffer: buffer);
                if (result.CollisionCount == 0)
                {
                    Assert.Fail("Buffered direct-candidate motion step should keep reporting ground contact.");
                }
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void Step_StepUpCandidateCacheDoesNotAllocateAfterWarmup()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            RegisterStaticAabb(
                world,
                entity: 360,
                body: 360,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.One, Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(4), Fix64.FromRatio(1, 4), Fix64.FromInt(2)));
            for (int i = 0; i < 96; i++)
            {
                Fix64 x = Fix64.FromInt(8 + (i % 8));
                Fix64 z = Fix64.FromInt(-8 + (i / 8));
                RegisterStaticAabb(
                    world,
                    entity: 400 + i,
                    body: 400 + i,
                    collider: 1,
                    layer: 1,
                    position: FixVector3.Zero,
                    localMin: new FixVector3(x, Fix64.Zero, z),
                    localMax: new FixVector3(x + Fix64.Half, Fix64.FromInt(2), z + Fix64.Half));
            }

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.FromRatio(35, 100)));
            var buffer = new CombatMotionStepBuffer();
            CombatMotionState state = CreateState(
                frame: 0,
                position: new FixVector3(Fix64.FromRatio(49, 100), Fix64.One, Fix64.Zero),
                velocity: FixVector3.Zero,
                grounded: true);
            CombatMotionInput input = CreateInput(UnitX, jumpPressed: false);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 8; i++)
            {
                CombatMotionStepResult warmup = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false,
                    impactVelocityOverride: null,
                    buffer: buffer);
                Assert.IsTrue(HasFlag(warmup.CollisionFlags, CombatMotionCollisionFlags.StepUp));
                Assert.Greater(motor.LastQueryStats.StepUpProbeCount, 0);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                CombatMotionStepResult result = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false,
                    impactVelocityOverride: null,
                    buffer: buffer);
                Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.StepUp));
                Assert.Greater(motor.LastQueryStats.StepUpProbeCount, 0);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void Step_StepCachePrefiltersSourceAndOffLayerCandidates()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            RegisterStaticAabb(
                world,
                entity: PlayerEntityId.Value,
                body: PlayerBodyId.Value,
                collider: 2,
                layer: 1,
                position: GroundedStart,
                localMin: new FixVector3(-Fix64.One, -Fix64.One, -Fix64.One),
                localMax: new FixVector3(Fix64.One, Fix64.One, Fix64.One));
            for (int i = 0; i < 24; i++)
            {
                Fix64 x = Fix64.FromRatio(-12 + i, 20);
                RegisterStaticAabb(
                    world,
                    entity: 520 + i,
                    body: 520 + i,
                    collider: 1,
                    layer: 5,
                    position: FixVector3.Zero,
                    localMin: new FixVector3(x, -Fix64.Half, -Fix64.Half),
                    localMax: new FixVector3(x + Fix64.FromRatio(1, 20), Fix64.Half, Fix64.Half));
            }

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(
                frame: 0,
                position: GroundedStart,
                velocity: FixVector3.Zero,
                grounded: true);

            CombatMotionStepResult result = motor.Step(
                state,
                CreateInput(FixVector3.Zero, jumpPressed: false),
                world,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                buffer: new CombatMotionStepBuffer());

            CombatKinematicMotorQueryStats stats = motor.LastQueryStats;
            Assert.IsTrue(result.State.Grounded);
            Assert.AreEqual(1, stats.BroadphaseCandidateCount);
            Assert.AreEqual(1, stats.NarrowphaseCandidateCount);
        }

        [Test]
        public void Step_StepCacheUsesCapsuleBoundsWithoutDoubleRadiusPadding()
        {
            var world = new CombatPhysicsWorld(new CombatPhysicsBroadphaseConfig(Fix64.One));
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, GroundedStart));
            RegisterStaticAabb(
                world,
                entity: 610,
                body: 610,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromInt(4), -Fix64.FromRatio(5, 100), -Fix64.FromInt(4)),
                localMax: new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.FromInt(4)));

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            CombatMotionStepResult result = motor.Step(
                state,
                CreateInput(FixVector3.Zero, jumpPressed: false),
                world,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                buffer: new CombatMotionStepBuffer());

            CombatKinematicMotorQueryStats stats = motor.LastQueryStats;
            Assert.IsTrue(result.State.Grounded);
            Assert.AreEqual(1, stats.QueryCount);
            Assert.AreEqual(
                16,
                stats.BroadphaseCellCount,
                "The Step cache query should use capsule bounds plus skin only. "
                + "Adding capsule radius again expands this 1m-cell query to 36 cells.");
            Assert.AreEqual(1, stats.BroadphaseCandidateCount);
            Assert.AreEqual(1, stats.NarrowphaseCandidateCount);
        }

        [Test]
        public void Step_StepCacheSkipsStepUpPaddingWithoutHorizontalDelta()
        {
            var world = new CombatPhysicsWorld(new CombatPhysicsBroadphaseConfig(Fix64.One));
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, GroundedStart));
            RegisterStaticAabb(
                world,
                entity: 620,
                body: 620,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromInt(4), -Fix64.FromRatio(5, 100), -Fix64.FromInt(4)),
                localMax: new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.FromInt(4)));

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.FromRatio(3, 2)));
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            CombatMotionStepResult result = motor.Step(
                state,
                CreateInput(FixVector3.Zero, jumpPressed: false),
                world,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                buffer: new CombatMotionStepBuffer());

            CombatKinematicMotorQueryStats stats = motor.LastQueryStats;
            Assert.IsTrue(result.State.Grounded);
            Assert.AreEqual(1, stats.QueryCount);
            Assert.AreEqual(
                16,
                stats.BroadphaseCellCount,
                "A grounded idle probe cannot attempt step-up because horizontal delta is zero, "
                + "so the Step cache envelope should not add MaxStepHeight vertical padding.");
            Assert.AreEqual(0, stats.StepUpProbeCount);
            Assert.AreEqual(1, stats.BroadphaseCandidateCount);
            Assert.AreEqual(1, stats.NarrowphaseCandidateCount);
        }

        [Test]
        public void Step_StepCacheDefersStepUpEnvelopeUntilWallCandidate()
        {
            var world = new CombatPhysicsWorld(new CombatPhysicsBroadphaseConfig(Fix64.One));
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, GroundedStart));
            RegisterStaticAabb(
                world,
                entity: 625,
                body: 625,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromRatio(3, 5), Fix64.Zero, -Fix64.Half),
                localMax: new FixVector3(-Fix64.Half, Fix64.FromInt(2), Fix64.Half));

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.FromRatio(3, 2)));
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            CombatMotionStepResult result = motor.Step(
                state,
                CreateInput(UnitX, jumpPressed: false),
                world,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                buffer: new CombatMotionStepBuffer());

            CombatKinematicMotorQueryStats stats = motor.LastQueryStats;
            Assert.Greater(result.State.Position.X.RawValue, GroundedStart.X.RawValue);
            Assert.AreEqual(0, result.CollisionCount);
            Assert.AreEqual(1, stats.QueryCount, "QueryCount");
            Assert.AreEqual(
                16,
                stats.BroadphaseCellCount,
                "Horizontal movement with step-up enabled should still start from the base sweep envelope; "
                + "the wider step-up envelope is needed only after a wall candidate is found.");
            Assert.AreEqual(0, stats.StepUpProbeCount);
            Assert.AreEqual(1, stats.BroadphaseCandidateCount, "BroadphaseCandidateCount");
            Assert.AreEqual(1, stats.NarrowphaseCandidateCount, "NarrowphaseCandidateCount");
        }

        [Test]
        public void Step_StepCacheSkipsCollisionProbeWhenNoEligibleCandidates()
        {
            var world = new CombatPhysicsWorld(new CombatPhysicsBroadphaseConfig(Fix64.One));
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, GroundedStart));
            RegisterStaticAabb(
                world,
                entity: 630,
                body: 630,
                collider: 1,
                layer: 9,
                position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromRatio(1, 20), Fix64.Zero, -Fix64.Half),
                localMax: new FixVector3(Fix64.FromRatio(3, 20), Fix64.FromInt(2), Fix64.Half));

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateExternalVelocityConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: false);

            CombatMotionStepResult result = motor.Step(
                state,
                CreateInput(UnitX, jumpPressed: false),
                world,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                buffer: new CombatMotionStepBuffer());

            CombatKinematicMotorQueryStats stats = motor.LastQueryStats;
            Assert.Greater(result.State.Position.X.RawValue, GroundedStart.X.RawValue);
            Assert.AreEqual(0, result.CollisionCount);
            Assert.AreEqual(0, stats.QueryCount);
            Assert.Greater(stats.BroadphaseCellCount, 0);
            Assert.AreEqual(0, stats.BroadphaseCandidateCount);
            Assert.AreEqual(0, stats.NarrowphaseCandidateCount);
        }

        [Test]
        public void Step_ObstacleLayerMaskNoneSkipsBroadphaseAndCollisionProbes()
        {
            var world = new CombatPhysicsWorld(new CombatPhysicsBroadphaseConfig(Fix64.One));
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, GroundedStart));
            RegisterStaticAabb(
                world,
                entity: 635,
                body: 635,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromRatio(1, 20), Fix64.Zero, -Fix64.Half),
                localMax: new FixVector3(Fix64.FromRatio(3, 20), Fix64.FromInt(2), Fix64.Half));

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateExternalVelocityConfig(
                collisionLayerMask: CombatPhysicsLayerMask.None.Value));
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: false);

            CombatMotionStepResult result = motor.Step(
                state,
                CreateInput(UnitX, jumpPressed: false),
                world,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                buffer: new CombatMotionStepBuffer());

            CombatKinematicMotorQueryStats stats = motor.LastQueryStats;
            Assert.Greater(result.State.Position.X.RawValue, GroundedStart.X.RawValue);
            Assert.AreEqual(0, result.CollisionCount);
            Assert.AreEqual(0, stats.QueryCount);
            Assert.AreEqual(0, stats.BroadphaseCellCount);
            Assert.AreEqual(0, stats.BroadphaseCandidateCount);
            Assert.AreEqual(0, stats.NarrowphaseCandidateCount);
        }

        [Test]
        public void Step_StepCacheSkipsBaseProbeWhenOnlyStepPaddingCandidatesExist()
        {
            var world = new CombatPhysicsWorld(new CombatPhysicsBroadphaseConfig(Fix64.One));
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, GroundedStart));
            for (int i = 0; i < 32; i++)
            {
                Fix64 x = Fix64.FromRatio(-40 + i % 8 * 10, 100);
                Fix64 z = Fix64.FromRatio(-40 + i / 8 * 10, 100);
                RegisterStaticAabb(
                    world,
                    entity: 640 + i,
                    body: 640 + i,
                    collider: 1,
                    layer: 1,
                    position: FixVector3.Zero,
                    localMin: new FixVector3(x, Fix64.FromRatio(5, 2), z),
                    localMax: new FixVector3(
                        x + Fix64.FromRatio(3, 100),
                        Fix64.FromRatio(27, 10),
                        z + Fix64.FromRatio(3, 100)));
            }

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.FromInt(1)));
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            CombatMotionStepResult result = motor.Step(
                state,
                CreateInput(UnitX, jumpPressed: false),
                world,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                buffer: new CombatMotionStepBuffer());

            CombatKinematicMotorQueryStats stats = motor.LastQueryStats;
            Assert.Greater(result.State.Position.X.RawValue, GroundedStart.X.RawValue);
            Assert.AreEqual(0, result.CollisionCount);
            Assert.AreEqual(0, stats.QueryCount);
            Assert.Greater(stats.BroadphaseCellCount, 0);
            Assert.AreEqual(0, stats.BroadphaseCandidateCount);
            Assert.AreEqual(0, stats.NarrowphaseCandidateCount);
        }

        [Test]
        public void Step_BestFractionPrefixSkipsFarCandidates()
        {
            var world = new CombatPhysicsWorld(new CombatPhysicsBroadphaseConfig(Fix64.FromInt(4)));
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, GroundedStart));
            RegisterStaticAabb(
                world,
                entity: 300,
                body: 300,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromRatio(3, 4), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromRatio(17, 20), Fix64.FromInt(3), Fix64.FromInt(2)));
            for (int i = 0; i < 12; i++)
            {
                Fix64 x = Fix64.FromRatio(18 + i, 10);
                RegisterStaticAabb(
                    world,
                    entity: 310 + i,
                    body: 310 + i,
                    collider: 1,
                    layer: 1,
                    position: FixVector3.Zero,
                    localMin: new FixVector3(x, Fix64.Zero, -Fix64.Half),
                    localMax: new FixVector3(x + Fix64.FromRatio(1, 20), Fix64.FromInt(3), Fix64.Half));
            }

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(moveSpeed: Fix64.FromInt(120)));
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: false);

            CombatMotionStepResult result = motor.Step(
                state,
                CreateInput(UnitX, jumpPressed: false),
                world,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                buffer: new CombatMotionStepBuffer());
            CombatKinematicMotorQueryStats stats = motor.LastQueryStats;

            Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.Wall));
            Assert.Greater(stats.BroadphaseCandidateCount, stats.NarrowphaseCandidateCount);
            Assert.Greater(stats.EarlyCandidateSkipCount, 0);
        }

        [Test]
        public void Step_StepCachePrioritizesReverseSweepCandidateBeforePrefixSkip()
        {
            var world = new CombatPhysicsWorld(new CombatPhysicsBroadphaseConfig(Fix64.FromInt(4)));
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, GroundedStart));
            for (int i = 0; i < 12; i++)
            {
                Fix64 x = -Fix64.FromRatio(24 - i, 10);
                RegisterStaticAabb(
                    world,
                    entity: 720 + i,
                    body: 720 + i,
                    collider: 1,
                    layer: 1,
                    position: FixVector3.Zero,
                    localMin: new FixVector3(x, Fix64.Zero, -Fix64.Half),
                    localMax: new FixVector3(x + Fix64.FromRatio(1, 20), Fix64.FromInt(3), Fix64.Half));
            }

            RegisterStaticAabb(
                world,
                entity: 710,
                body: 710,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromRatio(17, 20), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(-Fix64.FromRatio(4, 5), Fix64.FromInt(3), Fix64.FromInt(2)));

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(moveSpeed: Fix64.FromInt(120)));
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: false);

            CombatMotionStepResult result = motor.Step(
                state,
                CreateInput(-UnitX, jumpPressed: false),
                world,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                buffer: new CombatMotionStepBuffer());
            CombatKinematicMotorQueryStats stats = motor.LastQueryStats;

            Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.Wall));
            Assert.Greater(result.CollisionCount, 0);
            Assert.AreEqual(new CombatBodyId(710), result.Collisions[0].TargetBodyId);
            Assert.GreaterOrEqual(stats.BroadphaseCandidateCount, 10);
            Assert.LessOrEqual(stats.NarrowphaseCandidateCount, 3);
            Assert.Greater(stats.EarlyCandidateSkipCount, 0);
        }

        [Test]
        public void GroundedContact_ElasticProbeBelowThresholdDoesNotRebound()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(
                startPosition: GroundedStart,
                groundSurfaceMaterial: CombatSurfaceMaterial.Elastic);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateExternalVelocityConfig());
            CombatMotionState state = CreateState(
                frame: 0,
                position: GroundedStart,
                velocity: new FixVector3(Fix64.Zero, -Fix64.Half, Fix64.Zero),
                grounded: true);

            CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(FixVector3.Zero, jumpPressed: false));

            Assert.IsTrue(result.State.Grounded);
            Assert.AreEqual(Fix64.Zero, result.State.Velocity.Y);
            Assert.AreEqual(CombatSurfaceMaterialKind.Elastic, result.SurfaceMaterialKind);
        }

        [Test]
        public void ElasticGround_FallingImpactReboundsWhenImpactSpeedExceedsThreshold()
        {
            CombatSurfaceMaterial elastic = new CombatSurfaceMaterial(
                CombatSurfaceMaterialKind.Elastic,
                Fix64.Zero,
                Fix64.Half);
            FixVector3 start = new FixVector3(Fix64.Zero, Fix64.FromRatio(51, 50), Fix64.Zero);
            CombatPhysicsWorld world = CreateWorldWithGround(
                startPosition: start,
                groundSurfaceMaterial: elastic);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(minElasticImpactSpeed: Fix64.One));
            CombatMotionState state = CreateState(
                frame: 0,
                position: start,
                velocity: new FixVector3(Fix64.Zero, -Fix64.FromInt(20), Fix64.Zero),
                grounded: false);

            CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(FixVector3.Zero, jumpPressed: false));

            Assert.IsFalse(result.State.Grounded);
            Assert.IsFalse(HasFlag(result.State.CollisionFlags, CombatMotionCollisionFlags.Grounded));
            Assert.AreEqual(CombatSurfaceMaterialKind.Elastic, result.SurfaceMaterialKind);
            Assert.AreEqual(Fix64.Half, result.SurfaceElasticity);
            Assert.AreEqual(Fix64.FromInt(10), result.State.Velocity.Y);
        }

        [Test]
        public void ElasticGround_FullElasticityDoesNotExceedIncomingImpactSpeed()
        {
            FixVector3 start = new FixVector3(Fix64.Zero, Fix64.FromRatio(51, 50), Fix64.Zero);
            CombatPhysicsWorld world = CreateWorldWithGround(
                startPosition: start,
                groundSurfaceMaterial: CombatSurfaceMaterial.Elastic);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(minElasticImpactSpeed: Fix64.One));
            CombatMotionState state = CreateState(
                frame: 0,
                position: start,
                velocity: new FixVector3(Fix64.Zero, -Fix64.FromInt(20), Fix64.Zero),
                grounded: false);

            CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(FixVector3.Zero, jumpPressed: false));

            Assert.IsFalse(result.State.Grounded);
            Assert.IsFalse(HasFlag(result.State.CollisionFlags, CombatMotionCollisionFlags.Grounded));
            Assert.AreEqual(CombatSurfaceMaterialKind.Elastic, result.SurfaceMaterialKind);
            Assert.LessOrEqual(result.State.Velocity.Y.RawValue, Fix64.FromInt(20).RawValue);
            Assert.AreEqual(Fix64.FromInt(20), result.State.Velocity.Y);
        }

        [Test]
        public void ElasticGround_ExternalImpactVelocityDoesNotUsePostGravityDisplacementVelocity()
        {
            FixVector3 start = new FixVector3(Fix64.Zero, Fix64.FromRatio(51, 50), Fix64.Zero);
            CombatPhysicsWorld world = CreateWorldWithGround(
                startPosition: start,
                groundSurfaceMaterial: CombatSurfaceMaterial.Elastic);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateExternalVelocityConfig());
            CombatMotionState state = CreateState(
                frame: 0,
                position: start,
                velocity: new FixVector3(Fix64.Zero, -Fix64.FromRatio(41, 2), Fix64.Zero),
                grounded: false);
            FixVector3 impactVelocity = new FixVector3(Fix64.Zero, -Fix64.FromInt(20), Fix64.Zero);

            CombatMotionStepResult result = motor.Step(
                world,
                PlayerBodyId,
                state,
                CreateInput(FixVector3.Zero, jumpPressed: false),
                impactVelocity);

            Assert.IsFalse(result.State.Grounded);
            Assert.IsFalse(HasFlag(result.State.CollisionFlags, CombatMotionCollisionFlags.Grounded));
            Assert.AreEqual(CombatSurfaceMaterialKind.Elastic, result.SurfaceMaterialKind);
            Assert.AreEqual(Fix64.FromInt(20), result.State.Velocity.Y);
        }

        [Test]
        public void ElasticGround_ReboundNextFrameUsesAirborneGravity()
        {
            FixVector3 start = new FixVector3(Fix64.Zero, Fix64.FromRatio(51, 50), Fix64.Zero);
            CombatPhysicsWorld world = CreateWorldWithGround(
                startPosition: start,
                groundSurfaceMaterial: CombatSurfaceMaterial.Elastic);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateExternalVelocityConfig());
            CombatMotionState state = CreateState(
                frame: 0,
                position: start,
                velocity: new FixVector3(Fix64.Zero, -Fix64.FromRatio(41, 2), Fix64.Zero),
                grounded: false);
            FixVector3 impactVelocity = new FixVector3(Fix64.Zero, -Fix64.FromInt(20), Fix64.Zero);

            CombatMotionStepResult rebound = motor.Step(
                world,
                PlayerBodyId,
                state,
                CreateInput(FixVector3.Zero, jumpPressed: false),
                impactVelocity);
            FixVector3 nextFrameVelocity = rebound.State.Grounded
                ? rebound.State.Velocity
                : rebound.State.Velocity + new FixVector3(Fix64.Zero, -Fix64.Half, Fix64.Zero);
            CombatMotionState nextFrameState = new CombatMotionState(
                rebound.State.Frame,
                rebound.State.Position,
                nextFrameVelocity,
                rebound.State.Grounded,
                rebound.State.LastCollisionNormal,
                rebound.State.CollisionFlags);

            CombatMotionStepResult next = motor.Step(
                world,
                PlayerBodyId,
                nextFrameState,
                CreateInput(FixVector3.Zero, jumpPressed: false),
                rebound.State.Velocity);

            Assert.IsFalse(rebound.State.Grounded);
            Assert.AreEqual(Fix64.FromRatio(39, 2), next.State.Velocity.Y);
        }

        [Test]
        public void BouncePad_FallingImpactSetsFixedBounceHeightSpeed()
        {
            Fix64 bounceHeight = Fix64.FromInt(4);
            var bouncePad = new CombatSurfaceMaterial(
                CombatSurfaceMaterialKind.BouncePad,
                Fix64.Zero,
                Fix64.Zero,
                bounceHeight);
            FixVector3 start = new FixVector3(Fix64.Zero, Fix64.FromRatio(51, 50), Fix64.Zero);
            CombatPhysicsWorld world = CreateWorldWithGround(
                startPosition: start,
                groundSurfaceMaterial: bouncePad);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(minElasticImpactSpeed: Fix64.One));
            CombatMotionState state = CreateState(
                frame: 0,
                position: start,
                velocity: new FixVector3(Fix64.Zero, -Fix64.FromInt(3), Fix64.Zero),
                grounded: false);

            CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(FixVector3.Zero, jumpPressed: false));

            Assert.IsFalse(result.State.Grounded);
            Assert.IsFalse(HasFlag(result.State.CollisionFlags, CombatMotionCollisionFlags.Grounded));
            Assert.AreEqual(CombatSurfaceMaterialKind.BouncePad, result.SurfaceMaterialKind);
            Assert.AreEqual(bounceHeight, result.SurfaceFixedBounceHeight);
            Assert.AreEqual(CalculateFixedBounceSpeed(bounceHeight), result.State.Velocity.Y);
        }

        [Test]
        public void BouncePad_GroundedExternalVelocityContactSetsFixedBounceHeightSpeed()
        {
            Fix64 bounceHeight = Fix64.FromInt(4);
            CombatPhysicsWorld world = CreateWorldWithGround(
                startPosition: GroundedStart,
                groundSurfaceMaterial: new CombatSurfaceMaterial(
                    CombatSurfaceMaterialKind.BouncePad,
                    Fix64.Zero,
                    Fix64.Zero,
                    bounceHeight));
            CombatKinematicMotor motor = new CombatKinematicMotor(
                CreateExternalVelocityConfig(fixedBounceGravityPerSecond: Fix64.FromInt(30)));
            CombatMotionState state = CreateState(
                frame: 0,
                position: GroundedStart,
                velocity: new FixVector3(Fix64.Zero, -Fix64.Half, Fix64.Zero),
                grounded: true);

            CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(FixVector3.Zero, jumpPressed: false));

            Assert.IsFalse(result.State.Grounded);
            Assert.IsFalse(HasFlag(result.State.CollisionFlags, CombatMotionCollisionFlags.Grounded));
            Assert.AreEqual(CombatSurfaceMaterialKind.BouncePad, result.SurfaceMaterialKind);
            Assert.AreEqual(bounceHeight, result.SurfaceFixedBounceHeight);
            Assert.AreEqual(CalculateFixedBounceSpeed(bounceHeight), result.State.Velocity.Y);
        }

        [Test]
        public void BouncePad_RepeatedImpactsStayWithinFixedHeightCap()
        {
            Fix64 bounceHeight = Fix64.FromInt(4);
            CombatPhysicsWorld world = CreateWorldWithGround(
                startPosition: new FixVector3(Fix64.Zero, Fix64.FromInt(3), Fix64.Zero),
                groundSurfaceMaterial: new CombatSurfaceMaterial(
                    CombatSurfaceMaterialKind.BouncePad,
                    Fix64.Zero,
                    Fix64.Zero,
                    bounceHeight));
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(minElasticImpactSpeed: Fix64.One));
            CombatMotionState state = CreateState(
                frame: 0,
                position: new FixVector3(Fix64.Zero, Fix64.FromInt(3), Fix64.Zero),
                velocity: FixVector3.Zero,
                grounded: false);
            Fix64 maxY = state.Position.Y;

            for (int i = 0; i < 360; i++)
            {
                CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(FixVector3.Zero, jumpPressed: false));
                state = result.State;
                if (state.Position.Y > maxY)
                    maxY = state.Position.Y;
            }

            Assert.LessOrEqual(maxY.RawValue, (GroundedStart.Y + bounceHeight + Fix64.Half).RawValue);
        }

        [Test]
        public void GroundedContact_ZeroGravityExternalProbeKeepsGroundedWhileMoving()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateExternalVelocityConfig());
            CombatMotionState state = CreateState(
                frame: 0,
                position: GroundedStart,
                velocity: new FixVector3(Fix64.Zero, -Fix64.Half, Fix64.Zero),
                grounded: true);

            for (int i = 0; i < 12; i++)
            {
                state = new CombatMotionState(
                    state.Frame,
                    state.Position,
                    new FixVector3(Fix64.Zero, -Fix64.Half, Fix64.Zero),
                    state.Grounded,
                    state.LastCollisionNormal,
                    state.CollisionFlags);
                CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));
                state = result.State;

                Assert.IsTrue(state.Grounded, "External-velocity callers must be able to use a small downward probe to preserve contact. frame=" + i);
                Assert.IsTrue(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.Grounded));
                Assert.AreEqual(Fix64.Zero, state.Velocity.Y);
            }

            Assert.IsTrue(state.Position.X > GroundedStart.X);
        }

        [Test]
        public void Jump_GroundedJumpIsNotBlockedByInitialGroundContactAndAirJumpDoesNotRestart()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            CombatMotionStepResult first = motor.Step(world, PlayerBodyId, state, CreateInput(FixVector3.Zero, jumpPressed: true));
            Assert.IsTrue(first.JumpStarted, "JumpPressed from a grounded state should start a jump even when the character AABB is initially touching the ground.");
            Assert.IsFalse(first.State.Grounded, "The first jump frame should leave grounded state.");
            Assert.IsFalse(HasFlag(first.State.CollisionFlags, CombatMotionCollisionFlags.Grounded), "Initial ground contact must not be reported as a blocking collision while moving upward.");
            Assert.IsTrue(first.State.Velocity.Y > Fix64.Zero, "Jump should preserve upward velocity after gravity for the frame.");
            Assert.IsTrue(first.State.Position.Y > GroundedStart.Y);

            Fix64 firstJumpVelocity = first.State.Velocity.Y;
            state = first.State;
            for (int i = 0; i < 12; i++)
            {
                CombatMotionStepResult airStep = motor.Step(world, PlayerBodyId, state, CreateInput(FixVector3.Zero, jumpPressed: true));
                Assert.IsFalse(airStep.JumpStarted, "Holding or repeating JumpPressed while airborne must not start another jump in v0.");
                Assert.IsTrue(airStep.State.Velocity.Y <= firstJumpVelocity);
                state = airStep.State;
            }
        }

        [Test]
        public void WallCollision_BlocksHorizontalMovementAndSetsWallFlags()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            RegisterStaticAabb(world, entity: 3, body: 3, collider: 1, layer: 1, position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromInt(2), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(3), Fix64.FromInt(4), Fix64.FromInt(2)));
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            CombatMotionStepResult result = default;
            for (int i = 0; i < 40; i++)
            {
                result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));
                state = result.State;
            }

            Assert.IsTrue(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.Wall), "Moving into a vertical wall should set Wall.");
            Assert.IsTrue(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.BlockedX), "Moving into a +X wall should block X velocity.");
            Assert.AreEqual(Fix64.Zero, state.Velocity.X);
            Assert.AreEqual(Fix64.FromInt(2) - motor.Config.CapsuleProxy.Radius - motor.Config.CapsuleProxy.SkinWidth, state.Position.X);
            Assert.AreEqual(result.CollisionFlags, state.CollisionFlags);
        }

        [Test]
        public void StepUp_TallAabbWallDoesNotRunStepUpProbes()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            RegisterStaticAabb(world, entity: 9, body: 9, collider: 1, layer: 1, position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromInt(2), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(3), Fix64.FromInt(4), Fix64.FromInt(2)));
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.FromRatio(35, 100)));
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            for (int i = 0; i < 40; i++)
            {
                CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));
                state = result.State;
                if (!HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.Wall))
                {
                    continue;
                }

                Assert.AreEqual(0, motor.LastQueryStats.StepUpProbeCount);
                Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.BlockedX));
                return;
            }

            Assert.Fail("Expected a wall collision against the tall AABB.");
        }

        [Test]
        public void StepUp_LowAabbStepIsClimbedWhenConfigured()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            RegisterStaticAabb(world, entity: 6, body: 6, collider: 1, layer: 1, position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromInt(1), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(4), Fix64.FromRatio(1, 4), Fix64.FromInt(2)));
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.FromRatio(35, 100)));
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);
            bool steppedUp = false;

            for (int i = 0; i < 25; i++)
            {
                CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));
                state = result.State;
                steppedUp |= HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.StepUp);
            }

            Assert.IsTrue(steppedUp, "Low AABB steps should be resolved by the optional step-up path.");
            Assert.IsTrue(state.Position.X > Fix64.FromInt(1));
            Assert.IsTrue(state.Position.Y > GroundedStart.Y);
            Assert.IsFalse(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.BlockedX));
        }

        [Test]
        public void StepUp_PreservesSurfaceMaterialOnStepCollision()
        {
            CombatSurfaceMaterial rough = CombatSurfaceMaterial.Rough;
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            RegisterStaticAabb(world, entity: 8, body: 8, collider: 1, layer: 1, position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromInt(1), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(4), Fix64.FromRatio(1, 4), Fix64.FromInt(2)),
                surfaceMaterial: rough);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.FromRatio(35, 100)));
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            for (int i = 0; i < 25; i++)
            {
                CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));
                state = result.State;
                for (int collisionIndex = 0; collisionIndex < result.CollisionCount; collisionIndex++)
                {
                    CombatMotionCollision collision = result.Collisions[collisionIndex];
                    if (HasFlag(collision.Flags, CombatMotionCollisionFlags.StepUp))
                    {
                        Assert.AreEqual(rough, collision.SurfaceMaterial);
                        Assert.AreEqual(CombatSurfaceMaterialKind.Rough, collision.SurfaceMaterialKind);
                        return;
                    }
                }
            }

            Assert.Fail("Expected a step-up collision while climbing the low AABB.");
        }

        [Test]
        public void StepUp_DisabledKeepsLowStepAsWall()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            RegisterStaticAabb(world, entity: 7, body: 7, collider: 1, layer: 1, position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromInt(1), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(4), Fix64.FromRatio(1, 4), Fix64.FromInt(2)));
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.Zero));
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            for (int i = 0; i < 25; i++)
            {
                CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));
                state = result.State;
            }

            Assert.IsFalse(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.StepUp));
            Assert.IsTrue(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.Wall));
            Assert.IsTrue(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.BlockedX));
            Assert.AreEqual(Fix64.FromInt(1) - motor.Config.CapsuleProxy.Radius - motor.Config.CapsuleProxy.SkinWidth, state.Position.X);
        }

        [Test]
        public void StepUp_ContinuousStairsKeepHorizontalMotionWhenTreadsAreWideEnough()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            Fix64 stepDepth = Fix64.FromRatio(7, 5);
            Fix64 stepHeight = Fix64.FromRatio(1, 4);
            Fix64 startX = Fix64.FromInt(1);
            for (int i = 0; i < 8; i++)
            {
                Fix64 minX = startX + (stepDepth * Fix64.FromInt(i));
                Fix64 maxX = minX + stepDepth;
                Fix64 maxY = stepHeight * Fix64.FromInt(i + 1);
                RegisterStaticAabb(world, entity: 30 + i, body: 30 + i, collider: 1, layer: 1, position: FixVector3.Zero,
                    localMin: new FixVector3(minX, Fix64.Zero, -Fix64.FromInt(2)),
                    localMax: new FixVector3(maxX, maxY, Fix64.FromInt(2)));
            }
            RegisterStaticAabb(world, entity: 38, body: 38, collider: 1, layer: 1, position: FixVector3.Zero,
                localMin: new FixVector3(startX + (stepDepth * Fix64.FromInt(8)), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(18), stepHeight * Fix64.FromInt(8), Fix64.FromInt(2)));

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.FromRatio(35, 100)));
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);
            int stepUpCount = 0;

            for (int i = 0; i < 160; i++)
            {
                CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));
                state = result.State;
                if (HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.StepUp))
                    stepUpCount++;
            }

            Assert.GreaterOrEqual(stepUpCount, 6, "The character should keep stepping through the staircase instead of sticking on adjacent risers.");
            string finalState = " finalPosition=" + state.Position
                + " finalVelocity=" + state.Velocity
                + " finalFlags=" + state.CollisionFlags
                + " stepUpCount=" + stepUpCount;
            Assert.IsTrue(state.Grounded, finalState);
            Assert.IsFalse(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.BlockedX), finalState);
            Assert.IsTrue(state.Position.X > Fix64.FromInt(9), finalState);
            Assert.IsTrue(state.Position.Y > GroundedStart.Y + Fix64.FromInt(1), finalState);
        }

        [Test]
        public void StepUp_ContinuousStairsPreserveDrivenHorizontalVelocity()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            Fix64 stepDepth = Fix64.FromRatio(7, 5);
            Fix64 stepHeight = Fix64.FromRatio(1, 4);
            Fix64 startX = Fix64.FromInt(1);
            for (int i = 0; i < 8; i++)
            {
                Fix64 minX = startX + (stepDepth * Fix64.FromInt(i));
                Fix64 maxX = minX + stepDepth;
                Fix64 maxY = stepHeight * Fix64.FromInt(i + 1);
                RegisterStaticAabb(world, entity: 40 + i, body: 40 + i, collider: 1, layer: 1, position: FixVector3.Zero,
                    localMin: new FixVector3(minX, Fix64.Zero, -Fix64.FromInt(2)),
                    localMax: new FixVector3(maxX, maxY, Fix64.FromInt(2)));
            }

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.FromRatio(35, 100)));
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);
            Fix64 minimumSpeed = Fix64.FromInt(6);
            int lowSpeedFrame = -1;
            CombatMotionCollisionFlags lowSpeedFlags = CombatMotionCollisionFlags.None;

            for (int i = 0; i < 120; i++)
            {
                CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));
                state = result.State;
                if (state.Velocity.X < minimumSpeed)
                {
                    minimumSpeed = state.Velocity.X;
                    lowSpeedFrame = i;
                    lowSpeedFlags = state.CollisionFlags;
                }
            }

            string finalState = " minSpeed=" + minimumSpeed
                + " lowSpeedFrame=" + lowSpeedFrame
                + " lowSpeedFlags=" + lowSpeedFlags
                + " finalPosition=" + state.Position
                + " finalVelocity=" + state.Velocity
                + " finalFlags=" + state.CollisionFlags;
            Assert.IsTrue(minimumSpeed >= Fix64.FromRatio(59, 10), finalState);
        }

        [Test]
        public void CapsuleProxyContract_DerivesStableRadiusHeightAndCollisionMask()
        {
            CombatMotionConfig config = CreateConfig();

            Assert.AreEqual(Fix64.Half, config.CapsuleProxy.Radius);
            Assert.AreEqual(Fix64.FromInt(2), config.CapsuleProxy.Height);
            Assert.AreEqual(FixVector3.Zero, config.CapsuleProxy.Center);
            Assert.AreEqual(config.SkinWidth, config.CapsuleProxy.SkinWidth);
            Assert.AreEqual(BlockingMask, config.CapsuleProxy.CollisionMask);
        }

        [Test]
        public void CapsuleNoPenetration_WallKeepsSkinWidthGap()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            RegisterStaticAabb(world, entity: 5, body: 5, collider: 1, layer: 1, position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromInt(2), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(3), Fix64.FromInt(4), Fix64.FromInt(2)));
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            for (int i = 0; i < 40; i++)
            {
                CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));
                state = result.State;
            }

            Fix64 capsuleRight = state.Position.X + motor.Config.CapsuleProxy.Radius;
            Fix64 gap = Fix64.FromInt(2) - capsuleRight;
            Assert.AreEqual(motor.Config.CapsuleProxy.SkinWidth, gap);
            Assert.IsTrue(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.Wall));
        }

        [TestCase(CombatPhysicsShapeKind.Sphere)]
        [TestCase(CombatPhysicsShapeKind.Capsule)]
        public void ShapeTargetCollision_BlocksAndReportsCollision(CombatPhysicsShapeKind targetShape)
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            FixVector3 targetPosition = new FixVector3(Fix64.FromRatio(23, 20), Fix64.Half, Fix64.Zero);
            if (targetShape == CombatPhysicsShapeKind.Sphere)
            {
                RegisterStaticSphere(
                    world,
                    entity: 50,
                    body: 50,
                    collider: 1,
                    layer: 1,
                    position: targetPosition,
                    radius: Fix64.Half);
            }
            else
            {
                RegisterStaticCapsule(
                    world,
                    entity: 51,
                    body: 51,
                    collider: 1,
                    layer: 1,
                    position: targetPosition,
                    localPointA: new FixVector3(Fix64.Zero, -Fix64.Half, Fix64.Zero),
                    localPointB: new FixVector3(Fix64.Zero, Fix64.Half, Fix64.Zero),
                    radius: Fix64.Half);
            }

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.Half));
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);
            CombatMotionStepResult result = default;

            for (int i = 0; i < 12; i++)
            {
                result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));
                state = result.State;
            }

            Assert.Greater(result.CollisionCount, 0, targetShape + " target should produce a motion collision.");
            Assert.AreEqual(targetShape == CombatPhysicsShapeKind.Sphere ? new CombatBodyId(50) : new CombatBodyId(51),
                result.Collisions[result.CollisionCount - 1].TargetBodyId);
            Assert.IsTrue(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.Wall), targetShape.ToString());
            Assert.IsTrue(state.Position.X < targetPosition.X, targetShape + " target should block horizontal motion.");
            Assert.AreEqual(0, motor.LastQueryStats.StepUpProbeCount, targetShape + " target should not run AABB step-up probes.");
        }

        [TestCase(CombatPhysicsShapeKind.Sphere)]
        [TestCase(CombatPhysicsShapeKind.Capsule)]
        public void ShapeTargetCollision_DynamicShapeSweepDoesNotAllocateAfterWarmup(
            CombatPhysicsShapeKind targetShape)
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            RegisterShapeTarget(world, targetShape, entity: 60, body: 60, collider: 1,
                position: new FixVector3(Fix64.FromRatio(23, 20), Fix64.Half, Fix64.Zero),
                radius: Fix64.Half);
            for (int i = 0; i < 64; i++)
            {
                RegisterShapeTarget(
                    world,
                    targetShape,
                    entity: 600 + i,
                    body: 600 + i,
                    collider: 1,
                    position: new FixVector3(
                        Fix64.FromRatio(9 + i % 8, 10),
                        Fix64.FromRatio(23 + i / 8, 10),
                        Fix64.FromRatio(-20 + i % 5, 100)),
                    radius: Fix64.FromRatio(12, 100));
            }

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig(
                moveSpeed: Fix64.FromInt(120),
                maxStepHeight: Fix64.Half));
            var buffer = new CombatMotionStepBuffer();
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);
            CombatMotionInput input = CreateInput(UnitX, jumpPressed: false);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 8; i++)
            {
                CombatMotionStepResult warmup = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false,
                    buffer: buffer);
                Assert.Greater(warmup.CollisionCount, 0);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                CombatMotionStepResult result = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false,
                    buffer: buffer);
                Assert.Greater(result.CollisionCount, 0);
                Assert.Greater(motor.LastQueryStats.NarrowphaseCandidateCount, 0);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        [TestCase(CombatPhysicsShapeKind.Sphere)]
        [TestCase(CombatPhysicsShapeKind.Capsule)]
        public void ShapeTargetInitialOverlap_MovingAwayDoesNotBlock(CombatPhysicsShapeKind targetShape)
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            FixVector3 targetPosition = new FixVector3(-Fix64.FromRatio(19, 20), Fix64.Half, Fix64.Zero);
            if (targetShape == CombatPhysicsShapeKind.Sphere)
            {
                RegisterStaticSphere(
                    world,
                    entity: 52,
                    body: 52,
                    collider: 1,
                    layer: 1,
                    position: targetPosition,
                    radius: Fix64.Half);
            }
            else
            {
                RegisterStaticCapsule(
                    world,
                    entity: 53,
                    body: 53,
                    collider: 1,
                    layer: 1,
                    position: targetPosition,
                    localPointA: new FixVector3(Fix64.Zero, -Fix64.Half, Fix64.Zero),
                    localPointB: new FixVector3(Fix64.Zero, Fix64.Half, Fix64.Zero),
                    radius: Fix64.Half);
            }

            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));

            Assert.Greater(result.State.Position.X.RawValue, GroundedStart.X.RawValue, targetShape + " overlap should allow outward movement.");
            Assert.IsFalse(HasFlag(result.State.CollisionFlags, CombatMotionCollisionFlags.Wall), targetShape + " outward overlap should not report a wall.");
        }

        [Test]
        public void CeilingCollision_ClearsUpwardVelocityAndKeepsAirborne()
        {
            FixVector3 start = new FixVector3(Fix64.Zero, Fix64.FromRatio(9, 5), Fix64.Zero);
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: start);
            RegisterStaticAabb(world, entity: 4, body: 4, collider: 1, layer: 1, position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromInt(2), Fix64.FromInt(3), -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(2), Fix64.FromInt(4), Fix64.FromInt(2)));
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(
                frame: 0,
                position: start,
                velocity: new FixVector3(Fix64.Zero, Fix64.FromInt(24), Fix64.Zero),
                grounded: false);

            CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(FixVector3.Zero, jumpPressed: false));

            Assert.IsTrue(HasFlag(result.State.CollisionFlags, CombatMotionCollisionFlags.Ceiling), "Upward movement into a ceiling should set Ceiling.");
            Assert.IsTrue(HasFlag(result.State.CollisionFlags, CombatMotionCollisionFlags.BlockedY));
            Assert.IsFalse(result.State.Grounded);
            Assert.AreEqual(Fix64.Zero, result.State.Velocity.Y, "Ceiling collision should clear upward velocity.");
            Assert.IsTrue(result.State.Position.Y <= Fix64.FromInt(3) - CharacterHalfExtents.Y);
        }

        [Test]
        public void WorldSync_MovedBodyPositionIsUsedBySubsequentPhysicsQuery()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            RegisterStaticAabb(world, TargetEntityId.Value, TargetBodyId.Value, collider: 1, layer: 2, position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromInt(4), Fix64.Zero, -Fix64.Half),
                localMax: new FixVector3(Fix64.FromRatio(9, 2), Fix64.FromInt(2), Fix64.Half));
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            for (int i = 0; i < 30; i++)
            {
                CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(UnitX, jumpPressed: false));
                state = result.State;
            }

            Assert.IsTrue(world.TryGetBody(PlayerBodyId, out CombatPhysicsBody body));
            Assert.AreEqual(state.Position, body.Position);
            Assert.Greater(world.Revision, 1);

            var oldPositionResults = new List<CombatQueryResult>();
            var newPositionResults = new List<CombatQueryResult>();
            world.Query(CombatPhysicsQuery.From(CreateProbeRay(GroundedStart, Fix64.FromRatio(3, 2))), oldPositionResults);
            world.Query(CombatPhysicsQuery.From(CreateProbeRay(body.Position, Fix64.FromRatio(3, 2))), newPositionResults);

            Assert.AreEqual(0, oldPositionResults.Count, "The old body position should not be close enough to hit the target with this short probe.");
            Assert.AreEqual(1, newPositionResults.Count, "The probe built from the synced world body position should hit the target.");
            Assert.AreEqual(TargetEntityId, newPositionResults[0].TargetEntityId);
        }

        [Test]
        public void Step_CandidateCacheMatchesWorldPathAcrossStaticAndDynamicCandidates()
        {
            CombatPhysicsWorld expectedWorld = CreateMixedCandidateWorld();
            CombatPhysicsWorld cachedWorld = CreateMixedCandidateWorld();
            CombatMotionConfig config = CreateConfig(
                maxStepHeight: Fix64.FromRatio(35, 100),
                collisionLayerMask: BlockingMask.Value | TargetMask.Value);
            var expectedMotor = new CombatKinematicMotor(config);
            var cachedMotor = new CombatKinematicMotor(config);
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(cachedWorld, BlockingMask, TargetMask);
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);
            CombatMotionInput input = CreateInput(UnitX, jumpPressed: false);

            CombatMotionStepResult expected = expectedMotor.Step(
                state,
                input,
                expectedWorld,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                impactVelocityOverride: null,
                buffer: new CombatMotionStepBuffer());
            CombatMotionStepResult actual = cachedMotor.Step(
                state,
                input,
                cachedWorld,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                impactVelocityOverride: null,
                buffer: new CombatMotionStepBuffer(),
                candidateCache: cache);

            Assert.AreEqual(expected, actual);
            Assert.Greater(cachedMotor.LastQueryStats.CandidateCacheHitCount, 0);
            Assert.AreEqual(0, cachedMotor.LastQueryStats.CandidateCacheFallbackCount);
            Assert.AreEqual(2, cache.LastStats.DynamicColliderCount);
        }

        [Test]
        public void Step_CandidateCachePreservesStableOrderingForEqualFractionCandidates()
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, GroundedStart));
            RegisterStaticAabb(
                world,
                entity: 30,
                body: 30,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromInt(2), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(3), Fix64.FromInt(3), Fix64.FromInt(2)));
            RegisterStaticAabb(
                world,
                entity: 10,
                body: 10,
                collider: 1,
                layer: 2,
                position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromInt(2), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(3), Fix64.FromInt(3), Fix64.FromInt(2)));
            CombatMotionConfig config = CreateConfig(
                moveSpeed: Fix64.FromInt(120),
                collisionLayerMask: BlockingMask.Value | TargetMask.Value);
            var motor = new CombatKinematicMotor(config);
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(world, BlockingMask, TargetMask);
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: false);

            CombatMotionStepResult result = motor.Step(
                state,
                CreateInput(UnitX, jumpPressed: false),
                world,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                impactVelocityOverride: null,
                buffer: new CombatMotionStepBuffer(),
                candidateCache: cache);

            Assert.Greater(result.CollisionCount, 0);
            Assert.AreEqual(new CombatEntityId(10), result.Collisions[0].TargetEntityId);
            Assert.AreEqual(new CombatBodyId(10), result.Collisions[0].TargetBodyId);
            Assert.Greater(motor.LastQueryStats.CandidateCacheHitCount, 0);
            Assert.AreEqual(0, motor.LastQueryStats.CandidateCacheFallbackCount);
        }

        [Test]
        public void Step_CandidateCacheFallsBackWhenPreparedMasksDoNotCoverObstacleMask()
        {
            CombatPhysicsWorld expectedWorld = CreateMixedCandidateWorld();
            CombatPhysicsWorld cachedWorld = CreateMixedCandidateWorld();
            CombatMotionConfig config = CreateConfig(
                collisionLayerMask: BlockingMask.Value | TargetMask.Value);
            var expectedMotor = new CombatKinematicMotor(config);
            var cachedMotor = new CombatKinematicMotor(config);
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(cachedWorld, BlockingMask, CombatPhysicsLayerMask.None);
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);
            CombatMotionInput input = CreateInput(UnitX, jumpPressed: false);

            CombatMotionStepResult expected = expectedMotor.Step(
                state,
                input,
                expectedWorld,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                impactVelocityOverride: null,
                buffer: new CombatMotionStepBuffer());
            CombatMotionStepResult actual = cachedMotor.Step(
                state,
                input,
                cachedWorld,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                impactVelocityOverride: null,
                buffer: new CombatMotionStepBuffer(),
                candidateCache: cache);

            Assert.AreEqual(expected, actual);
            Assert.AreEqual(0, cachedMotor.LastQueryStats.CandidateCacheHitCount);
            Assert.Greater(cachedMotor.LastQueryStats.CandidateCacheFallbackCount, 0);
        }

        [Test]
        public void Step_CandidateCacheUpdatesDynamicBodyAfterWorldSync()
        {
            CombatPhysicsWorld expectedWorld = CreateDynamicUpdateWorld();
            CombatPhysicsWorld cachedWorld = CreateDynamicUpdateWorld();
            CombatMotionConfig config = CreateExternalVelocityConfig(collisionLayerMask: TargetMask.Value);
            var expectedMotor = new CombatKinematicMotor(config);
            var cachedMotor = new CombatKinematicMotor(config);
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(cachedWorld, CombatPhysicsLayerMask.None, TargetMask);
            CombatMotionState firstState = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: false);
            CombatMotionInput firstInput = CreateInput(UnitX, jumpPressed: false);
            var expectedBuffer = new CombatMotionStepBuffer();
            var cachedBuffer = new CombatMotionStepBuffer();

            CombatMotionStepResult expectedFirst = expectedMotor.Step(
                firstState,
                firstInput,
                expectedWorld,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: true,
                impactVelocityOverride: null,
                buffer: expectedBuffer);
            CombatMotionStepResult cachedFirst = cachedMotor.Step(
                firstState,
                firstInput,
                cachedWorld,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: true,
                impactVelocityOverride: null,
                buffer: cachedBuffer,
                candidateCache: cache);

            Assert.AreEqual(expectedFirst, cachedFirst);
            Assert.Greater(cache.LastStats.DynamicBodyUpdateCount, 0);

            var secondEntity = new CombatEntityId(2);
            var secondBody = new CombatBodyId(2);
            CombatMotionState secondState = CreateState(
                frame: 0,
                position: new FixVector3(Fix64.FromRatio(112, 100), Fix64.One, Fix64.Zero),
                velocity: FixVector3.Zero,
                grounded: false);
            CombatMotionInput secondInput = CreateInput(-UnitX, jumpPressed: false);
            CombatMotionStepResult expectedSecond = expectedMotor.Step(
                secondState,
                secondInput,
                expectedWorld,
                secondEntity,
                secondBody,
                syncBodyPosition: true,
                impactVelocityOverride: null,
                buffer: expectedBuffer);
            CombatMotionStepResult cachedSecond = cachedMotor.Step(
                secondState,
                secondInput,
                cachedWorld,
                secondEntity,
                secondBody,
                syncBodyPosition: true,
                impactVelocityOverride: null,
                buffer: cachedBuffer,
                candidateCache: cache);

            Assert.AreEqual(expectedSecond, cachedSecond);
            Assert.Greater(cachedSecond.CollisionCount, 0);
            Assert.AreEqual(PlayerBodyId, cachedSecond.Collisions[0].TargetBodyId);
            Assert.Greater(cachedMotor.LastQueryStats.CandidateCacheHitCount, 0);
            Assert.AreEqual(0, cachedMotor.LastQueryStats.CandidateCacheFallbackCount);
        }

        [Test]
        public void Step_CandidateCachePrepareUsesIncrementalUpdateWhenOnlyBodyMoves()
        {
            CombatPhysicsWorld expectedWorld = CreateIncrementalDynamicCandidateWorld();
            CombatPhysicsWorld cachedWorld = CreateIncrementalDynamicCandidateWorld();
            CombatMotionConfig config = CreateConfig(
                collisionLayerMask: BlockingMask.Value | TargetMask.Value);
            var expectedMotor = new CombatKinematicMotor(config);
            var cachedMotor = new CombatKinematicMotor(config);
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(cachedWorld, BlockingMask, TargetMask);
            CombatKinematicMotorCandidateCacheStats initialStats = cache.LastStats;
            FixVector3 movedTargetPosition = GetIncrementalTargetPosition(0);
            expectedWorld.SetBodyPosition(new CombatBodyId(90), movedTargetPosition);
            cachedWorld.SetBodyPosition(new CombatBodyId(90), movedTargetPosition);
            cache.Prepare(cachedWorld, BlockingMask, TargetMask);
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);
            CombatMotionInput input = CreateInput(UnitX, jumpPressed: false);

            CombatMotionStepResult expected = expectedMotor.Step(
                state,
                input,
                expectedWorld,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                impactVelocityOverride: null,
                buffer: new CombatMotionStepBuffer());
            CombatMotionStepResult actual = cachedMotor.Step(
                state,
                input,
                cachedWorld,
                PlayerEntityId,
                PlayerBodyId,
                syncBodyPosition: false,
                impactVelocityOverride: null,
                buffer: new CombatMotionStepBuffer(),
                candidateCache: cache);

            Assert.AreEqual(expected, actual);
            Assert.IsTrue(ContainsCollisionWithBody(actual, new CombatBodyId(90)));
            Assert.AreEqual(initialStats.DynamicRebuildCount, cache.LastStats.DynamicRebuildCount);
            Assert.AreEqual(1, cache.LastStats.DynamicIncrementalUpdateCount);
            Assert.AreEqual(1, cache.LastStats.DynamicMovedBodyCount);
            Assert.Greater(cachedMotor.LastQueryStats.CandidateCacheHitCount, 0);
            Assert.AreEqual(0, cachedMotor.LastQueryStats.CandidateCacheFallbackCount);
        }

        [Test]
        public void Step_CandidateCachePrepareUsesSnapshotDiffWhenMovedBodySnapshotWasConsumed()
        {
            CombatPhysicsWorld world = CreateIncrementalDynamicCandidateWorld();
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(world, BlockingMask, TargetMask);
            CombatKinematicMotorCandidateCacheStats initialStats = cache.LastStats;
            world.SetBodyPosition(new CombatBodyId(90), GetIncrementalTargetPosition(0));
            var queryResults = new List<CombatQueryResult>();
            world.Query(CombatPhysicsQuery.From(CreateProbeRay(GroundedStart, Fix64.FromInt(2))), queryResults);

            cache.Prepare(world, BlockingMask, TargetMask);

            Assert.AreEqual(initialStats.DynamicRebuildCount, cache.LastStats.DynamicRebuildCount);
            Assert.AreEqual(initialStats.DynamicIncrementalUpdateCount + 1, cache.LastStats.DynamicIncrementalUpdateCount);
            Assert.AreEqual(1, cache.LastStats.DynamicMovedBodyCount);
        }

        [Test]
        public void Step_CandidateCachePrepareRebuildsWhenDynamicTopologyChanges()
        {
            CombatPhysicsWorld world = CreateIncrementalDynamicCandidateWorld();
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(world, BlockingMask, TargetMask);
            CombatKinematicMotorCandidateCacheStats initialStats = cache.LastStats;
            RegisterStaticSphere(
                world,
                entity: 91,
                body: 91,
                collider: 1,
                layer: 2,
                position: new FixVector3(Fix64.FromInt(3), Fix64.One, Fix64.Zero),
                radius: Fix64.FromRatio(2, 10));

            cache.Prepare(world, BlockingMask, TargetMask);

            Assert.AreEqual(initialStats.DynamicRebuildCount + 1, cache.LastStats.DynamicRebuildCount);
            Assert.AreEqual(initialStats.DynamicIncrementalUpdateCount, cache.LastStats.DynamicIncrementalUpdateCount);
        }

        [Test]
        public void Step_CachedIncrementalPrepareDoesNotAllocateAfterWarmup()
        {
            CombatPhysicsWorld world = CreateIncrementalDynamicCandidateWorld();
            CombatMotionConfig config = CreateConfig(
                collisionLayerMask: BlockingMask.Value | TargetMask.Value);
            var motor = new CombatKinematicMotor(config);
            var buffer = new CombatMotionStepBuffer();
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(world, BlockingMask, TargetMask);
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);
            CombatMotionInput input = CreateInput(UnitX, jumpPressed: false);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 16; i++)
            {
                world.SetBodyPosition(new CombatBodyId(90), GetIncrementalTargetPosition(i));
                cache.Prepare(world, BlockingMask, TargetMask);
                _ = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false,
                    impactVelocityOverride: null,
                    buffer: buffer,
                    candidateCache: cache);
            }

            int collisionCount = 0;
            int cacheHitCount = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                world.SetBodyPosition(new CombatBodyId(90), GetIncrementalTargetPosition(i));
                cache.Prepare(world, BlockingMask, TargetMask);
                CombatMotionStepResult result = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false,
                    impactVelocityOverride: null,
                    buffer: buffer,
                    candidateCache: cache);
                collisionCount += result.CollisionCount;
                cacheHitCount += motor.LastQueryStats.CandidateCacheHitCount;
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.Greater(collisionCount, 0);
            Assert.Greater(cacheHitCount, 0);
            Assert.Greater(cache.LastStats.DynamicIncrementalUpdateCount, 0);
            Assert.AreEqual(1, cache.LastStats.DynamicRebuildCount);
        }

        [Test]
        public void Step_CachedCandidatePathDoesNotAllocateAfterWarmup()
        {
            CombatPhysicsWorld world = CreateMixedCandidateWorld();
            for (int i = 0; i < 96; i++)
            {
                Fix64 x = Fix64.FromRatio(80 + (i % 12) * 7, 100);
                Fix64 z = Fix64.FromRatio(-60 + (i / 12) * 10, 100);
                RegisterStaticAabb(
                    world,
                    entity: 2000 + i,
                    body: 2000 + i,
                    collider: 1,
                    layer: i % 2 == 0 ? 1 : 2,
                    position: FixVector3.Zero,
                    localMin: new FixVector3(x, Fix64.Zero, z),
                    localMax: new FixVector3(
                        x + Fix64.FromRatio(3, 100),
                        Fix64.FromRatio(21, 10),
                        z + Fix64.FromRatio(3, 100)));
            }

            CombatMotionConfig config = CreateConfig(
                moveSpeed: Fix64.FromInt(60),
                collisionLayerMask: BlockingMask.Value | TargetMask.Value);
            var motor = new CombatKinematicMotor(config);
            var buffer = new CombatMotionStepBuffer();
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(world, BlockingMask, TargetMask);
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);
            CombatMotionInput input = CreateInput(UnitX, jumpPressed: false);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 8; i++)
            {
                CombatMotionStepResult warmup = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false,
                    impactVelocityOverride: null,
                    buffer: buffer,
                    candidateCache: cache);
                Assert.Greater(warmup.CollisionCount, 0);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                CombatMotionStepResult result = motor.Step(
                    state,
                    input,
                    world,
                    PlayerEntityId,
                    PlayerBodyId,
                    syncBodyPosition: false,
                    impactVelocityOverride: null,
                    buffer: buffer,
                    candidateCache: cache);
                Assert.Greater(result.CollisionCount, 0);
                Assert.Greater(motor.LastQueryStats.CandidateCacheHitCount, 0);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        private static CombatMotionSnapshot ReplayDeterminismSequence()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            CombatKinematicMotor motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = CreateState(frame: 0, position: GroundedStart, velocity: FixVector3.Zero, grounded: true);

            for (int i = 0; i < 140; i++)
            {
                FixVector3 move = i < 70 ? UnitX : -UnitX;
                bool jumpPressed = i == 5 || i == 16 || i == 90;
                CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, CreateInput(move, jumpPressed));
                state = result.State;
            }

            Assert.IsTrue(world.TryGetBody(PlayerBodyId, out CombatPhysicsBody body));
            return new CombatMotionSnapshot(state.Position, state.Velocity, state.Grounded, state.CollisionFlags, body.Position, world.Revision);
        }

        private static CombatMotionConfig CreateConfig(
            Fix64 maxStepHeight = default(Fix64),
            Fix64 groundMinNormalY = default(Fix64),
            Fix64 minElasticImpactSpeed = default(Fix64),
            Fix64 moveSpeed = default(Fix64),
            int? collisionLayerMask = null)
        {
            Fix64 groundNormal = groundMinNormalY.IsZero ? Fix64.Half : groundMinNormalY;
            return new CombatMotionConfig(
                CombatStepConfig.Default,
                CharacterHalfExtents,
                moveSpeed: moveSpeed.IsZero ? Fix64.FromInt(6) : moveSpeed,
                gravityPerSecond: -Fix64.FromInt(30),
                jumpSpeed: Fix64.FromInt(12),
                maxFallSpeed: Fix64.FromInt(40),
                skinWidth: Fix64.FromRatio(1, 100),
                groundMinNormalY: groundNormal,
                ceilingMinNormalY: Fix64.Half,
                collisionLayerMask: collisionLayerMask ?? BlockingMask.Value,
                maxSlideIterations: 3,
                maxStepHeight: maxStepHeight,
                minElasticImpactSpeed: minElasticImpactSpeed);
        }

        private static CombatMotionConfig CreateExternalVelocityConfig(
            Fix64 fixedBounceGravityPerSecond = default(Fix64),
            int? collisionLayerMask = null)
        {
            return new CombatMotionConfig(
                CombatStepConfig.Default,
                CharacterHalfExtents,
                moveSpeed: Fix64.FromInt(6),
                gravityPerSecond: Fix64.Zero,
                jumpSpeed: Fix64.Zero,
                maxFallSpeed: Fix64.FromInt(40),
                skinWidth: Fix64.FromRatio(1, 100),
                groundMinNormalY: Fix64.Half,
                ceilingMinNormalY: Fix64.Half,
                collisionLayerMask: collisionLayerMask ?? BlockingMask.Value,
                maxSlideIterations: 3,
                fixedBounceGravityPerSecond: fixedBounceGravityPerSecond);
        }

        private static CombatMotionState CreateState(
            int frame,
            FixVector3 position,
            FixVector3 velocity,
            bool grounded)
        {
            return new CombatMotionState(
                new CombatFrame(frame),
                position,
                velocity,
                grounded,
                FixVector3.Zero,
                grounded ? CombatMotionCollisionFlags.Grounded : CombatMotionCollisionFlags.None);
        }

        private static CombatMotionInput CreateInput(
            FixVector3 moveDirection,
            bool jumpPressed,
            bool applyGroundSurfaceFriction = true)
        {
            return new CombatMotionInput(
                moveDirection,
                jumpPressed,
                Fix64.One,
                applyGroundSurfaceFriction);
        }

        private static Fix64 CalculateFixedBounceSpeed(Fix64 bounceHeight)
        {
            return (Fix64.FromInt(2) * Fix64.FromInt(30) * bounceHeight).Sqrt();
        }

        private static CombatPhysicsWorld CreateWorldWithGround(
            FixVector3 startPosition,
            Fix64 groundSurfaceFriction = default(Fix64))
        {
            return CreateWorldWithGround(
                startPosition,
                CombatSurfaceMaterial.FromFriction(groundSurfaceFriction));
        }

        private static CombatPhysicsWorld CreateWorldWithGround(
            FixVector3 startPosition,
            CombatSurfaceMaterial groundSurfaceMaterial)
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, startPosition));
            RegisterStaticAabb(world, entity: 2, body: 2, collider: 1, layer: 1, position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                localMax: new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)),
                surfaceMaterial: groundSurfaceMaterial);
            return world;
        }

        private static CombatPhysicsWorld CreateMixedCandidateWorld()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            RegisterStaticAabb(
                world,
                entity: PlayerEntityId.Value,
                body: PlayerBodyId.Value,
                collider: 2,
                layer: 1,
                position: GroundedStart,
                localMin: new FixVector3(-Fix64.Half, -Fix64.One, -Fix64.Half),
                localMax: new FixVector3(Fix64.Half, Fix64.One, Fix64.Half));
            RegisterStaticAabb(
                world,
                entity: 70,
                body: 70,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.One, Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(4), Fix64.FromRatio(1, 4), Fix64.FromInt(2)),
                surfaceMaterial: CombatSurfaceMaterial.Rough);
            RegisterStaticSphere(
                world,
                entity: 80,
                body: 80,
                collider: 1,
                layer: 2,
                position: new FixVector3(Fix64.FromRatio(13, 10), Fix64.Half, Fix64.FromRatio(7, 10)),
                radius: Fix64.FromRatio(3, 10));
            RegisterStaticCapsule(
                world,
                entity: 81,
                body: 81,
                collider: 1,
                layer: 2,
                position: new FixVector3(Fix64.FromRatio(7, 5), Fix64.Half, -Fix64.FromRatio(7, 10)),
                localPointA: new FixVector3(Fix64.Zero, -Fix64.Half, Fix64.Zero),
                localPointB: new FixVector3(Fix64.Zero, Fix64.Half, Fix64.Zero),
                radius: Fix64.FromRatio(3, 10));
            return world;
        }

        private static CombatPhysicsWorld CreateDynamicUpdateWorld()
        {
            var world = new CombatPhysicsWorld();
            RegisterStaticAabb(
                world,
                entity: PlayerEntityId.Value,
                body: PlayerBodyId.Value,
                collider: 1,
                layer: 2,
                position: GroundedStart,
                localMin: new FixVector3(-Fix64.Half, -Fix64.One, -Fix64.Half),
                localMax: new FixVector3(Fix64.Half, Fix64.One, Fix64.Half));
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(2),
                new CombatBodyId(2),
                new FixVector3(Fix64.FromRatio(112, 100), Fix64.One, Fix64.Zero)));
            return world;
        }

        private static CombatPhysicsWorld CreateIncrementalDynamicCandidateWorld()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(startPosition: GroundedStart);
            RegisterStaticAabb(
                world,
                entity: 90,
                body: 90,
                collider: 1,
                layer: 2,
                position: new FixVector3(Fix64.FromInt(4), Fix64.One, Fix64.Zero),
                localMin: new FixVector3(-Fix64.FromRatio(1, 10), -Fix64.One, -Fix64.Half),
                localMax: new FixVector3(Fix64.FromRatio(1, 10), Fix64.One, Fix64.Half),
                surfaceMaterial: CombatSurfaceMaterial.Rough);
            return world;
        }

        private static FixVector3 GetIncrementalTargetPosition(int index)
        {
            Fix64 x = (index & 1) == 0
                ? Fix64.FromRatio(7, 10)
                : Fix64.FromRatio(72, 100);
            return new FixVector3(x, Fix64.One, Fix64.Zero);
        }

        private static bool ContainsCollisionWithBody(CombatMotionStepResult result, CombatBodyId bodyId)
        {
            for (int i = 0; i < result.CollisionCount; i++)
            {
                if (result.Collisions[i].TargetBodyId.Equals(bodyId))
                    return true;
            }

            return false;
        }

        private static void RegisterStaticAabb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            FixVector3 position,
            FixVector3 localMin,
            FixVector3 localMax,
            Fix64 surfaceFriction = default(Fix64))
        {
            RegisterStaticAabb(
                world,
                entity,
                body,
                collider,
                layer,
                position,
                localMin,
                localMax,
                CombatSurfaceMaterial.FromFriction(surfaceFriction));
        }

        private static void RegisterStaticAabb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            FixVector3 position,
            FixVector3 localMin,
            FixVector3 localMax,
            CombatSurfaceMaterial surfaceMaterial)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, position));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(collider),
                layer,
                localMin,
                localMax,
                surfaceMaterial));
        }

        private static void RegisterStaticSphere(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            FixVector3 position,
            Fix64 radius)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, position));
            world.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                bodyId,
                new CombatColliderId(collider),
                layer,
                FixVector3.Zero,
                radius,
                CombatSurfaceMaterial.Default));
        }

        private static void RegisterShapeTarget(
            CombatPhysicsWorld world,
            CombatPhysicsShapeKind shapeKind,
            int entity,
            int body,
            int collider,
            FixVector3 position,
            Fix64 radius)
        {
            if (shapeKind == CombatPhysicsShapeKind.Sphere)
            {
                RegisterStaticSphere(
                    world,
                    entity,
                    body,
                    collider,
                    layer: 1,
                    position,
                    radius);
                return;
            }

            RegisterStaticCapsule(
                world,
                entity,
                body,
                collider,
                layer: 1,
                position,
                localPointA: new FixVector3(Fix64.Zero, -radius, Fix64.Zero),
                localPointB: new FixVector3(Fix64.Zero, radius, Fix64.Zero),
                radius);
        }

        private static void RegisterStaticCapsule(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            FixVector3 position,
            FixVector3 localPointA,
            FixVector3 localPointB,
            Fix64 radius)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, position));
            world.UpsertCapsuleCollider(new CombatPhysicsCapsuleCollider(
                bodyId,
                new CombatColliderId(collider),
                layer,
                localPointA,
                localPointB,
                radius,
                CombatSurfaceMaterial.Default));
        }

        private static CombatRayQuery CreateProbeRay(FixVector3 origin, Fix64 length)
        {
            return new CombatRayQuery(
                new CombatQueryHeader(
                    queryId: 100,
                    CombatQueryKind.Ray,
                    PlayerEntityId,
                    traceId: 200,
                    actionId: 300,
                    sourceOrder: 0,
                    TargetMask),
                origin,
                UnitX,
                length);
        }

        private static bool HasFlag(CombatMotionCollisionFlags value, CombatMotionCollisionFlags flag)
        {
            return (value & flag) == flag;
        }

        private static FixVector3 GroundedStart => new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);

        private static FixVector3 UnitX => new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);

        private static FixVector3 UnitZ => new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

        private readonly struct CombatMotionSnapshot
        {
            public CombatMotionSnapshot(
                FixVector3 position,
                FixVector3 velocity,
                bool grounded,
                CombatMotionCollisionFlags collisionFlags,
                FixVector3 worldPosition,
                int worldRevision)
            {
                Position = position;
                Velocity = velocity;
                Grounded = grounded;
                CollisionFlags = collisionFlags;
                WorldPosition = worldPosition;
                WorldRevision = worldRevision;
            }

            public FixVector3 Position { get; }

            public FixVector3 Velocity { get; }

            public bool Grounded { get; }

            public CombatMotionCollisionFlags CollisionFlags { get; }

            public FixVector3 WorldPosition { get; }

            public int WorldRevision { get; }
        }
    }
}
