using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Motion
{
    public sealed class CombatKinematicMotorWallSlideTests
    {
        private static readonly CombatEntityId PlayerEntityId = new CombatEntityId(1);
        private static readonly CombatBodyId PlayerBodyId = new CombatBodyId(1);
        private static readonly CombatPhysicsLayerMask BlockingMask =
            CombatPhysicsLayerMask.FromLayer(1);
        private static readonly FixVector3 Start =
            new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
        private static readonly FixVector3 Diagonal =
            new FixVector3(Fix64.FromRaw(707107), Fix64.Zero, Fix64.FromRaw(707107));

        private const int Ticks = 120;
        // The X/Z wall face is at 1m and the capsule radius is 0.5m. The
        // implementation may conservatively stop anywhere below this physical
        // contact plane; exact skin offset depends on the cast witness metric.
        private const long PhysicalContactCenterRaw = 500000L;
        private const long ObservedDiagonalSkinCenterRaw = 494970L;
        private const long FullSpeedPerTickRaw = 100000L;

        [Test]
        public void AabbWall_ActiveLocomotionMaintainsSixMetersPerSecondAndNeverSticks()
        {
            CombatPhysicsWorld world = CreateGroundWorld(Start);
            RegisterAabbWallX(world, bodyValue: 10);
            var motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = GroundedState(Start);
            CombatMotionStepOptions options = ActiveLocomotionOptions();
            var buffer = new CombatMotionStepBuffer(CombatMotionConfig.DefaultMaxCollisionIterations);

            bool contactedWall = false;
            long previousZ = state.Position.Z.RawValue;
            for (int i = 0; i < Ticks; i++)
            {
                CombatMotionStepResult result = Step(
                    motor,
                    world,
                    state,
                    Diagonal,
                    options,
                    buffer);
                state = result.State;
                AssertNoIterationLimit(result, "AABB active wall slide tick " + i);
                Assert.LessOrEqual(
                    state.Position.X.RawValue,
                    PhysicalContactCenterRaw + 3L,
                    "AABB wall penetration tick " + i);
                Assert.Greater(
                    state.Position.Z.RawValue,
                    previousZ,
                    "AABB wall slide must make forward progress tick " + i);
                previousZ = state.Position.Z.RawValue;

                if (!HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.Wall))
                    continue;
                if (contactedWall)
                {
                    Assert.That(
                        result.AppliedDelta.Z.RawValue,
                        Is.InRange(FullSpeedPerTickRaw - 16L, FullSpeedPerTickRaw + 16L),
                        "AABB active steady wall tangent delta tick " + i);
                }
                contactedWall = true;
                Assert.That(
                    state.Velocity.Z.RawValue,
                    Is.InRange(5998500L, 6001500L),
                    "AABB active wall tangent speed tick " + i);
            }

            Assert.IsTrue(contactedWall, "The active locomotion fixture never reached the AABB wall.");
            Assert.That(state.Velocity.Z.RawValue, Is.InRange(5998500L, 6001500L));
        }

        [Test]
        public void AabbWall_DefaultOptionKeepsOrthogonalProjectionWithoutLosingContactTickTangent()
        {
            FixVector3 start = new FixVector3(Fix64.FromRaw(450000), Fix64.One, Fix64.Zero);
            CombatPhysicsWorld world = CreateGroundWorld(start);
            RegisterAabbWallX(world, bodyValue: 20);
            var motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = GroundedState(start);
            var buffer = new CombatMotionStepBuffer(CombatMotionConfig.DefaultMaxCollisionIterations);

            CombatMotionStepResult contact = Step(
                motor,
                world,
                state,
                Diagonal,
                default,
                buffer);
            AssertNoIterationLimit(contact, "default contact tick");
            Assert.IsTrue(HasFlag(contact.CollisionFlags, CombatMotionCollisionFlags.Wall));
            Assert.That(
                contact.AppliedDelta.Z.RawValue,
                Is.InRange(70700L, 70720L),
                "The contact tick must retain both its pre-contact and post-contact tangent time.");

            state = contact.State;
            for (int i = 1; i < Ticks; i++)
            {
                CombatMotionStepResult result = Step(
                    motor,
                    world,
                    state,
                    Diagonal,
                    default,
                    buffer);
                state = result.State;
                AssertNoIterationLimit(result, "default projection tick " + i);
            }

            Assert.That(
                state.Velocity.Z.RawValue,
                Is.InRange(4242500L, 4242800L),
                "Default options must retain physical orthogonal projection instead of the locomotion speed floor.");
            Assert.Less(state.Velocity.Z.RawValue, 5000000L);
        }

        [Test]
        public void RotatedObbWall_ActiveLocomotionIsMonotonicRawNonEnteringAndFullSpeed()
        {
            CombatPhysicsWorld world = CreateGroundWorld(Start);
            FixVector3 normal = R(866025, 0, 500000);
            FixVector3 tangent = R(-500000, 0, 866025);
            RegisterRotatedObbWall(world, bodyValue: 30, normal, tangent);
            var motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = GroundedState(Start);
            Assert.IsTrue((normal + tangent).TryNormalize(out FixVector3 direction));
            var buffer = new CombatMotionStepBuffer(CombatMotionConfig.DefaultMaxCollisionIterations);
            CombatMotionStepOptions options = ActiveLocomotionOptions();

            bool contactedWall = false;
            Fix64 previousTangent = state.Position.Dot(tangent);
            for (int i = 0; i < Ticks; i++)
            {
                CombatMotionStepResult result = Step(
                    motor,
                    world,
                    state,
                    direction,
                    options,
                    buffer);
                state = result.State;
                AssertNoIterationLimit(result, "rotated OBB wall slide tick " + i);
                Fix64 tangentPosition = state.Position.Dot(tangent);
                Assert.Greater(
                    tangentPosition.RawValue,
                    previousTangent.RawValue,
                    "Rotated OBB wall slide must remain monotonic tick " + i);
                previousTangent = tangentPosition;

                for (int collisionIndex = 0; collisionIndex < result.CollisionCount; collisionIndex++)
                {
                    CombatMotionCollision collision = result.Collisions[collisionIndex];
                    if (!collision.TargetBodyId.Equals(new CombatBodyId(30)))
                        continue;
                    bool steadyContact = contactedWall;
                    contactedWall = true;
                    if (!steadyContact)
                        continue;
                    Assert.IsTrue(CombatKinematicMotor.TryRawDotNumerator(
                        result.AppliedDelta,
                        collision.Normal,
                        out long appliedDotNormalRaw2));
                    Assert.GreaterOrEqual(
                        appliedDotNormalRaw2,
                        0L,
                        "Rotated OBB applied delta re-entered the wall tick " + i);
                }
            }

            Assert.IsTrue(contactedWall, "The active locomotion fixture never reached the rotated OBB wall.");
            Fix64 tangentSpeed = state.Velocity.Dot(tangent);
            Assert.That(tangentSpeed.RawValue, Is.InRange(5998500L, 6001500L));
            Assert.LessOrEqual(
                state.Position.Dot(normal).RawValue,
                PhysicalContactCenterRaw + 500L,
                "Rotated OBB wall slide penetrated the skin plane.");
        }

        [Test]
        public void TwoOrthogonalWalls_ActiveLocomotionStopsAtRealCornerWithoutPenetration()
        {
            CombatPhysicsWorld world = CreateGroundWorld(Start);
            RegisterAabbWallX(world, bodyValue: 40);
            RegisterAabbWallZ(world, bodyValue: 41);
            var motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = GroundedState(Start);
            var buffer = new CombatMotionStepBuffer(CombatMotionConfig.DefaultMaxCollisionIterations);
            CombatMotionStepOptions options = ActiveLocomotionOptions();

            long settledX = 0L;
            long settledZ = 0L;
            for (int i = 0; i < Ticks; i++)
            {
                CombatMotionStepResult result = Step(
                    motor,
                    world,
                    state,
                    Diagonal,
                    options,
                    buffer);
                state = result.State;
                AssertNoIterationLimit(result, "real corner tick " + i);
                Assert.LessOrEqual(state.Position.X.RawValue, PhysicalContactCenterRaw + 3L);
                Assert.LessOrEqual(state.Position.Z.RawValue, PhysicalContactCenterRaw + 3L);
                if (i == 60)
                {
                    settledX = state.Position.X.RawValue;
                    settledZ = state.Position.Z.RawValue;
                }
            }

            Assert.That(state.Position.X.RawValue, Is.InRange(settledX - 2L, settledX + 2L));
            Assert.That(state.Position.Z.RawValue, Is.InRange(settledZ - 2L, settledZ + 2L));
            Assert.That(state.Velocity.X.RawValue, Is.InRange(-2L, 2L));
            Assert.That(state.Velocity.Z.RawValue, Is.InRange(-2L, 2L));
            Assert.IsTrue(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.BlockedX));
            Assert.IsTrue(HasFlag(state.CollisionFlags, CombatMotionCollisionFlags.BlockedZ));
        }

        [Test]
        public void OpposingObbGap_ActiveLocomotionEscapesAlongCommonTangent()
        {
            CombatPhysicsWorld world = CreateGroundWorld(Start);
            RegisterOpposingObbGap(world);
            var motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = GroundedState(Start);
            var buffer = new CombatMotionStepBuffer(CombatMotionConfig.DefaultMaxCollisionIterations);
            CombatMotionStepOptions options = ActiveLocomotionOptions();
            FixVector3 tangent = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);

            for (int i = 0; i < 40; i++)
            {
                long previousX = state.Position.X.RawValue;
                CombatMotionStepResult result = Step(
                    motor,
                    world,
                    state,
                    tangent,
                    options,
                    buffer);
                state = result.State;
                AssertNoIterationLimit(result, "opposing OBB gap escape tick " + i);
                Assert.Greater(
                    state.Position.X.RawValue,
                    previousX,
                    "Opposing OBB walls must not freeze their common tangent tick " + i);
            }

            Assert.Greater(state.Position.X.RawValue, 3000000L);
        }

        [Test]
        public void OpposingObbGap_LateralInputDoesNotBypassEitherWall()
        {
            CombatPhysicsWorld world = CreateGroundWorld(Start);
            RegisterOpposingObbGap(world);
            var motor = new CombatKinematicMotor(CreateConfig());
            var buffer = new CombatMotionStepBuffer(CombatMotionConfig.DefaultMaxCollisionIterations);

            CombatMotionStepResult result = Step(
                motor,
                world,
                GroundedState(Start),
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                ActiveLocomotionOptions(),
                buffer);

            Assert.That(result.State.Position.Z.RawValue, Is.InRange(-2L, 2L));
            Assert.LessOrEqual(result.AppliedDelta.Z.RawValue, 2L);
        }

        [Test]
        public void OpposingObbGap_RecoveryHotPathDoesNotAllocateAfterWarmup()
        {
            CombatPhysicsWorld world = CreateGroundWorld(Start);
            RegisterOpposingObbGap(world);
            var motor = new CombatKinematicMotor(CreateConfig());
            var buffer = new CombatMotionStepBuffer(CombatMotionConfig.DefaultMaxCollisionIterations);
            CombatMotionState state = GroundedState(Start);
            FixVector3 tangent = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
            CombatMotionStepOptions options = ActiveLocomotionOptions();

            for (int i = 0; i < 8; i++)
                _ = Step(motor, world, state, tangent, options, buffer);

            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            CombatMotionStepResult result = default;
            for (int i = 0; i < 600; i++)
                result = Step(motor, world, state, tangent, options, buffer);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            AssertNoIterationLimit(result, "opposing OBB gap NoAlloc");
            Assert.Greater(result.AppliedDelta.X.RawValue, 0L);
        }

        [Test]
        public void AabbWall_StartingAtSkinContactNeverDropsAFrameOfActiveTangentMotion()
        {
            FixVector3 start = new FixVector3(
                Fix64.FromRaw(ObservedDiagonalSkinCenterRaw),
                Fix64.One,
                Fix64.Zero);
            CombatPhysicsWorld world = CreateGroundWorld(start);
            RegisterAabbWallX(world, bodyValue: 50);
            var motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = GroundedState(start);
            var buffer = new CombatMotionStepBuffer(CombatMotionConfig.DefaultMaxCollisionIterations);
            CombatMotionStepOptions options = ActiveLocomotionOptions();

            for (int i = 0; i < Ticks; i++)
            {
                long previousZ = state.Position.Z.RawValue;
                CombatMotionStepResult result = Step(
                    motor,
                    world,
                    state,
                    Diagonal,
                    options,
                    buffer);
                state = result.State;
                AssertNoIterationLimit(result, "steady skin wall slide tick " + i);
                Assert.IsTrue(
                    HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.Wall),
                    "Steady skin contact lost the wall tick " + i);
                Assert.That(
                    state.Position.Z.RawValue - previousZ,
                    Is.InRange(FullSpeedPerTickRaw - 16L, FullSpeedPerTickRaw + 16L),
                    "Steady skin contact dropped tangent time tick " + i);
                Assert.That(
                    state.Position.X.RawValue,
                    Is.LessThanOrEqualTo(PhysicalContactCenterRaw + 3L));
            }
        }

        [Test]
        public void AabbWall_ActiveLocomotionHotPathDoesNotAllocateAfterWarmup()
        {
            FixVector3 start = new FixVector3(
                Fix64.FromRaw(ObservedDiagonalSkinCenterRaw),
                Fix64.One,
                Fix64.Zero);
            CombatPhysicsWorld world = CreateGroundWorld(start);
            RegisterAabbWallX(world, bodyValue: 60);
            var motor = new CombatKinematicMotor(CreateConfig());
            CombatMotionState state = GroundedState(start);
            var buffer = new CombatMotionStepBuffer(CombatMotionConfig.DefaultMaxCollisionIterations);
            CombatMotionStepOptions options = ActiveLocomotionOptions();

            for (int i = 0; i < 8; i++)
                _ = Step(motor, world, state, Diagonal, options, buffer);

            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            CombatMotionStepResult result = default;
            for (int i = 0; i < 600; i++)
                result = Step(motor, world, state, Diagonal, options, buffer);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.Wall));
            AssertNoIterationLimit(result, "active wall-slide NoAlloc");
        }

        [Test]
        public void ProjectWallRemaining_NonCollinearRawWitnessSatisfiesBothConstraints()
        {
            FixVector3 classificationNormal = R(1000000, 0, 0);
            FixVector3 nonEnteringWitness = R(800000, 0, -600000);
            FixVector3 value = R(-500000, 0, 1000000);

            FixVector3 result = CombatKinematicMotor.ProjectWallRemaining(
                value,
                classificationNormal,
                nonEnteringWitness,
                Fix64.FromInt(2));

            Assert.IsFalse(result.IsZero);
            Assert.IsTrue(CombatKinematicMotor.TryRawDotNumerator(
                result,
                classificationNormal,
                out long classificationDotRaw2));
            Assert.IsTrue(CombatKinematicMotor.TryRawDotNumerator(
                result,
                nonEnteringWitness,
                out long witnessDotRaw2));
            Assert.GreaterOrEqual(classificationDotRaw2, 0L);
            Assert.GreaterOrEqual(witnessDotRaw2, 0L);
            Assert.That(
                new FixVector3(result.X, Fix64.Zero, result.Z).LengthSquared().Sqrt().RawValue,
                Is.InRange(1999990L, 2000010L));
        }

        private static CombatMotionStepResult Step(
            CombatKinematicMotor motor,
            CombatPhysicsWorld world,
            CombatMotionState state,
            FixVector3 direction,
            CombatMotionStepOptions options,
            CombatMotionStepBuffer buffer)
        {
            return motor.Step(
                world,
                PlayerBodyId,
                state,
                new CombatMotionInput(
                    direction,
                    jumpPressed: false,
                    moveSpeedScale: Fix64.One,
                    applyGroundSurfaceFriction: false),
                impactVelocityOverride: null,
                buffer: buffer,
                candidateCache: null,
                options: options);
        }

        private static CombatMotionStepOptions ActiveLocomotionOptions()
        {
            return new CombatMotionStepOptions(
                suppressGroundSnap: false,
                wallSlideMinimumHorizontalSpeed: Fix64.FromInt(6));
        }

        private static CombatMotionConfig CreateConfig()
        {
            return new CombatMotionConfig(
                ticksPerSecond: 60,
                moveSpeed: Fix64.FromInt(6),
                gravityPerSecond: -Fix64.FromInt(30),
                jumpSpeed: Fix64.FromInt(12),
                maxFallSpeed: -Fix64.FromInt(40),
                skinWidth: Fix64.FromRatio(1, 100),
                groundMinNormalY: Fix64.Half,
                ceilingMinNormalY: Fix64.Half,
                maxCollisionIterations: CombatMotionConfig.DefaultMaxCollisionIterations,
                characterHalfExtents: new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                obstacleLayerMask: BlockingMask,
                maxStepHeight: Fix64.Zero,
                minElasticImpactSpeed: Fix64.One,
                fixedBounceGravityPerSecond: Fix64.FromInt(30),
                groundSnapDistance: Fix64.FromRatio(1, 10));
        }

        private static CombatMotionState GroundedState(FixVector3 position)
        {
            return new CombatMotionState(
                CombatFrame.Zero,
                position,
                FixVector3.Zero,
                grounded: true,
                lastCollisionNormal: new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                collisionFlags: CombatMotionCollisionFlags.Grounded);
        }

        private static CombatPhysicsWorld CreateGroundWorld(FixVector3 playerPosition)
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, playerPosition));
            RegisterAabb(
                world,
                entityValue: 2,
                bodyValue: 2,
                min: R(-20000000, -1000000, -20000000),
                max: R(20000000, 0, 20000000));
            return world;
        }

        private static void RegisterAabbWallX(CombatPhysicsWorld world, int bodyValue)
        {
            RegisterAabb(
                world,
                entityValue: bodyValue,
                bodyValue: bodyValue,
                min: R(1000000, -1000000, -20000000),
                max: R(2000000, 4000000, 20000000));
        }

        private static void RegisterAabbWallZ(CombatPhysicsWorld world, int bodyValue)
        {
            RegisterAabb(
                world,
                entityValue: bodyValue,
                bodyValue: bodyValue,
                min: R(-20000000, -1000000, 1000000),
                max: R(20000000, 4000000, 2000000));
        }

        private static void RegisterAabb(
            CombatPhysicsWorld world,
            int entityValue,
            int bodyValue,
            FixVector3 min,
            FixVector3 max)
        {
            var bodyId = new CombatBodyId(bodyValue);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(entityValue),
                bodyId,
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                layer: 1,
                localMin: min,
                localMax: max));
        }

        private static void RegisterRotatedObbWall(
            CombatPhysicsWorld world,
            int bodyValue,
            FixVector3 normal,
            FixVector3 tangent)
        {
            var bodyId = new CombatBodyId(bodyValue);
            FixVector3 center = normal * Fix64.FromInt(2)
                + new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(bodyValue),
                bodyId,
                center));
            world.UpsertObbCollider(new CombatPhysicsObbCollider(
                bodyId,
                new CombatColliderId(1),
                layer: 1,
                localCenter: FixVector3.Zero,
                halfExtents: new FixVector3(Fix64.One, Fix64.FromInt(2), Fix64.FromInt(20)),
                axisX: normal,
                axisY: new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                axisZ: tangent));
        }

        private static void RegisterOpposingObbGap(CombatPhysicsWorld world)
        {
            RegisterGapWall(world, bodyValue: 70, centerZRaw: -1400000L);
            RegisterGapWall(world, bodyValue: 71, centerZRaw: 1400000L);
        }

        private static void RegisterGapWall(
            CombatPhysicsWorld world,
            int bodyValue,
            long centerZRaw)
        {
            var bodyId = new CombatBodyId(bodyValue);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(bodyValue),
                bodyId,
                R(0L, 1000000L, centerZRaw)));
            world.UpsertObbCollider(new CombatPhysicsObbCollider(
                bodyId,
                new CombatColliderId(1),
                layer: 1,
                localCenter: FixVector3.Zero,
                halfExtents: R(1000000L, 2000000L, 2000000L),
                axisX: new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                axisY: new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                axisZ: new FixVector3(-Fix64.One, Fix64.Zero, Fix64.Zero)));
        }

        private static void AssertNoIterationLimit(CombatMotionStepResult result, string context)
        {
            Assert.IsFalse(
                HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.IterationLimit),
                context + " aggregate IterationLimit");
            for (int i = 0; i < result.CollisionCount; i++)
            {
                Assert.IsFalse(
                    HasFlag(result.Collisions[i].Flags, CombatMotionCollisionFlags.IterationLimit),
                    context + " collision IterationLimit " + i);
            }
        }

        private static bool HasFlag(
            CombatMotionCollisionFlags value,
            CombatMotionCollisionFlags flag)
        {
            return (value & flag) == flag;
        }

        private static FixVector3 R(long x, long y, long z)
        {
            return new FixVector3(Fix64.FromRaw(x), Fix64.FromRaw(y), Fix64.FromRaw(z));
        }
    }
}
