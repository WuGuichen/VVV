using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterKinematicPhysicsAdapterTests
    {
        private const int TickRate = 60;
        private const int InputAxisScale = 1000;
        private const int GroundContactProbeVelocity = -500;
        private const int MoveFacingEpsilon = 1;
        private static readonly CombatEntityId CharacterEntity = new CombatEntityId(1);
        private static readonly CombatBodyId CharacterBody = new CombatBodyId(1);
        private static readonly CombatPhysicsLayerMask BlockingMask = CombatPhysicsLayerMask.FromLayer(1);
        private static readonly CharacterKinematicPhysicsAdapter Adapter = CharacterKinematicPhysicsAdapter.Default;

        [Test]
        public void Query_DoesNotMoveWorldBodyAndMatchesFollowingRealStep()
        {
            var world = new CombatPhysicsWorld(); var motor = new CombatKinematicMotor(CreateConfig());
            var buffer = new CombatMotionStepBuffer();
            var baseline = CreateInput(horizontalVelocityX: 3000, wasGrounded: false, applyGroundSurfaceFriction: false);
            Adapter.Step(motor, world, buffer, in baseline);
            Assert.IsTrue(world.TryGetBody(CharacterBody, out var before));
            var combined = CreateInput(horizontalVelocityX: 9000, wasGrounded: false, applyGroundSurfaceFriction: false,
                hasRootMotion: true, persistentHorizontalVelocityX: 3000, rootMotionHorizontalVelocityX: 6000);
            var predicted = Adapter.Query(motor, world, buffer, in combined);
            Assert.IsTrue(world.TryGetBody(CharacterBody, out var queried));
            Assert.AreEqual(before.Position, queried.Position, "A base-only contribution query must not mutate the authoritative body.");
            var actual = Adapter.Step(motor, world, buffer, in combined);
            Assert.AreEqual(predicted.PositionXMillimeters, actual.PositionXMillimeters);
            Assert.AreEqual(predicted.PositionYMillimeters, actual.PositionYMillimeters);
            Assert.AreEqual(predicted.PositionZMillimeters, actual.PositionZMillimeters);
            Assert.AreEqual(3000, actual.HorizontalVelocityX, "Transient displacement must not replace persistent velocity.");
            Assert.IsTrue(world.TryGetBody(CharacterBody, out var moved));
            Assert.AreNotEqual(before.Position, moved.Position);
        }

        [Test]
        public void Step_ConstructsCombatMotionStateInputAndImpactVelocity()
        {
            var world = new CombatPhysicsWorld();
            var motor = new CombatKinematicMotor(CreateConfig());

            CharacterKinematicPhysicsStepResult result = Adapter.Step(
                motor,
                world,
                new CombatMotionStepBuffer(),
                CreateInput(
                    x: 1200,
                    y: 300,
                    z: -400,
                    centerOffsetY: Fix64.Half,
                    horizontalVelocityX: 3000,
                    horizontalVelocityY: 4000,
                    verticalVelocity: 700,
                    impactVerticalVelocity: 900,
                    wasGrounded: false,
                    applyGroundSurfaceFriction: false));

            Assert.AreEqual(new FixVector3(
                Fix64.FromRatio(3, 5),
                Fix64.Zero,
                Fix64.FromRatio(4, 5)), result.MotionInput.MoveDirection);
            Assert.AreEqual(Fix64.FromInt(5), result.MotionInput.MoveSpeedScale);
            Assert.IsFalse(result.MotionInput.ApplyGroundSurfaceFriction);
            Assert.AreEqual(Fix64.FromRatio(12, 10), result.MotionState.Position.X);
            Assert.AreEqual(Fix64.FromRatio(8, 10), result.MotionState.Position.Y);
            Assert.AreEqual(-Fix64.FromRatio(4, 10), result.MotionState.Position.Z);
            Assert.AreEqual(Fix64.FromRatio(9, 10), result.ImpactVelocity.Y);
            Assert.IsTrue(world.TryGetBody(CharacterBody, out CombatPhysicsBody body));
            Assert.AreEqual(CharacterEntity, body.EntityId);
        }

        [Test]
        public void Step_GroundedDownwardVelocityUsesGroundContactProbe()
        {
            var world = new CombatPhysicsWorld();
            var motor = new CombatKinematicMotor(CreateConfig());

            CharacterKinematicPhysicsStepResult result = Adapter.Step(
                motor,
                world,
                null,
                CreateInput(
                    verticalVelocity: -100,
                    impactVerticalVelocity: -100,
                    wasGrounded: true));

            Assert.AreEqual(Fix64.FromRatio(-1, 2), result.MotionState.Velocity.Y);
            Assert.IsTrue(result.MotionState.Grounded);
            Assert.IsTrue((result.MotionState.CollisionFlags & CombatMotionCollisionFlags.Grounded) == CombatMotionCollisionFlags.Grounded);
        }

        [Test]
        public void Step_SnapEnabledGroundedVelocityUsesZeroSupportInsteadOfLegacyProbe()
        {
            var world = new CombatPhysicsWorld();
            var motor = new CombatKinematicMotor(CreateConfig(groundSnapDistance: Fix64.FromRatio(1, 10)));

            CharacterKinematicPhysicsStepResult result = Adapter.Step(
                motor,
                world,
                null,
                CreateInput(verticalVelocity: -100, wasGrounded: true));

            Assert.AreEqual(Fix64.Zero, result.MotionState.Velocity.Y);
            Assert.AreNotEqual(Fix64.FromRatio(-1, 2), result.MotionState.Velocity.Y);
        }

        [Test]
        public void Step_SnapEnabledFlatGroundStaysGroundedWithMaterial()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(CombatSurfaceMaterial.Rough);
            var motor = new CombatKinematicMotor(CreateConfig(groundSnapDistance: Fix64.FromRatio(1, 10)));

            CharacterKinematicPhysicsStepResult result = Adapter.Step(
                motor,
                world,
                new CombatMotionStepBuffer(),
                CreateInput(y: 1000, wasGrounded: true));

            Assert.IsTrue(result.Grounded);
            Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
            Assert.AreEqual(CombatSurfaceMaterial.Rough, result.SurfaceMaterial);
            Assert.AreEqual(0, result.VerticalVelocity);
        }

        [TestCase(258819, 965926)]
        [TestCase(500000, 866025)]
        public void Step_SnapEnabledSlopeIdleSixHundredTicksHasZeroXZDrift(
            int sinRaw,
            int cosRaw)
        {
            CombatPhysicsWorld world = CreateSlopeWorld(sinRaw, cosRaw, out int startY);
            var motor = new CombatKinematicMotor(CreateConfig(groundSnapDistance: Fix64.FromRatio(1, 10)));
            int x = 0;
            int y = startY;
            int z = 0;
            bool grounded = true;
            for (int i = 0; i < 600; i++)
            {
                CharacterKinematicPhysicsStepResult result = Adapter.Step(
                    motor,
                    world,
                    new CombatMotionStepBuffer(),
                    CreateInput(
                        x: x,
                        y: y,
                        z: z,
                        previousX: x,
                        previousY: y,
                        previousZ: z,
                        wasGrounded: grounded));
                x = result.PositionXMillimeters;
                y = result.PositionYMillimeters;
                z = result.PositionZMillimeters;
                grounded = result.Grounded;
                Assert.AreEqual(0, x, "X drift at " + i);
                Assert.AreEqual(0, z, "Z drift at " + i);
                Assert.IsTrue(grounded, "grounded at " + i);
            }
        }

        [TestCase(258819, 965926)]
        [TestCase(500000, 866025)]
        public void Step_SnapEnabledSlopeDownhillContinuouslyFollowsSupport(
            int sinRaw,
            int cosRaw)
        {
            CombatPhysicsWorld world = CreateSlopeWorld(sinRaw, cosRaw, out int startY);
            var motor = new CombatKinematicMotor(CreateConfig(groundSnapDistance: Fix64.FromRatio(1, 10)));
            int x = 0;
            int y = startY;
            int initialY = startY;
            bool grounded = true;
            bool decreased = false;
            bool snapped = false;
            for (int i = 0; i < 12; i++)
            {
                int previousY = y;
                CharacterKinematicPhysicsStepResult result = Adapter.Step(
                    motor,
                    world,
                    new CombatMotionStepBuffer(),
                    CreateInput(
                        x: x,
                        y: y,
                        previousX: x,
                        previousY: y,
                        horizontalVelocityX: -1000,
                        wasGrounded: grounded));
                x = result.PositionXMillimeters;
                y = result.PositionYMillimeters;
                grounded = result.Grounded;
                Assert.LessOrEqual(y, previousY, "non-increasing downhill Y at " + i);
                decreased |= y < previousY;
                snapped |= HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.GroundSnap);
                Assert.IsTrue(grounded, "grounded at " + i);
            }

            Assert.IsTrue(decreased);
            Assert.IsTrue(snapped, "At least one tick must use the explicit Ground Snap support phase.");
            Assert.Less(y, initialY);
        }

        [TestCase(258819, 965926)]
        [TestCase(500000, 866025)]
        public void Step_FormalSlopeSmallInputMillimeterRoundTripMovesWithoutIterationLimit(
            int sinRaw,
            int cosRaw)
        {
            CombatPhysicsWorld world = CreateFormalSlopeWorld(
                sinRaw,
                cosRaw,
                out int x,
                out int y,
                out int z);
            var motor = new CombatKinematicMotor(CreateFormalSlopeConfig());
            var buffer = new CombatMotionStepBuffer();
            int initialX = x;
            int initialY = y;
            int slopeContacts = 0;
            bool grounded = true;
            int verticalVelocity = 0;

            for (int i = 0; i < 180; i++)
            {
                CharacterKinematicPhysicsStepInput input = CreateInput(
                    x: x,
                    y: y,
                    z: z,
                    previousX: x,
                    previousY: y,
                    previousZ: z,
                    centerOffsetY: Fix64.FromRaw(900000),
                    horizontalVelocityX: -750,
                    verticalVelocity: verticalVelocity,
                    wasGrounded: grounded);
                CharacterKinematicPhysicsStepResult result = Adapter.Step(
                    motor,
                    world,
                    buffer,
                    in input);
                x = result.PositionXMillimeters;
                y = result.PositionYMillimeters;
                z = result.PositionZMillimeters;
                verticalVelocity = result.VerticalVelocity;
                grounded = result.Grounded;

                Assert.IsTrue(grounded, "grounded at " + i);
                Assert.IsFalse(
                    HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.IterationLimit),
                    "aggregate IterationLimit at " + i);
                bool slopeContact = false;
                for (int collisionIndex = 0;
                     collisionIndex < result.StepResult.CollisionCount;
                     collisionIndex++)
                {
                    CombatMotionCollision collision = result.StepResult.Collisions[collisionIndex];
                    Assert.IsFalse(
                        HasFlag(collision.Flags, CombatMotionCollisionFlags.IterationLimit),
                        "collision IterationLimit at " + i + "/" + collisionIndex);
                    slopeContact |= collision.TargetBodyId.Equals(new CombatBodyId(2));
                }

                Assert.IsTrue(slopeContact, "formal slope contact at " + i);
                slopeContacts++;
            }

            Assert.AreEqual(180, slopeContacts);
            Assert.Greater(y - initialY, 200, "formal uphill authority Y");
            Assert.Less(x, initialX, "formal uphill authority X");
        }

        [Test]
        public void Step_PositiveTypedIntentsAndImpactEachSuppressGroundSnap()
        {
            AssertTypedIntentSuppresses(CreateInput(y: 1050, verticalVelocity: 100, wasGrounded: true));
            AssertTypedIntentSuppresses(CreateInput(
                y: 1050, verticalVelocity: -100, persistentVerticalVelocity: 100, wasGrounded: true));
            AssertTypedIntentSuppresses(CreateInput(
                y: 1050, verticalVelocity: -100, hasRootMotion: true,
                rootMotionVerticalVelocity: 100, wasGrounded: true));
            AssertTypedIntentSuppresses(CreateInput(
                y: 1050, impactVerticalVelocity: 100, wasGrounded: true));
        }

        [Test]
        public void Step_MixedSignRootAndPersistentIntentsPreserveNetStateButSuppressSnap()
        {
            CombatPhysicsWorld world = CreateWorldWithGround();
            var motor = new CombatKinematicMotor(CreateConfig(groundSnapDistance: Fix64.FromRatio(1, 10)));
            CharacterKinematicPhysicsStepInput input = CreateInput(
                y: 1050,
                verticalVelocity: -200,
                persistentVerticalVelocity: 100,
                hasRootMotion: true,
                rootMotionVerticalVelocity: 50,
                wasGrounded: true);

            CharacterKinematicPhysicsStepResult result = Adapter.Step(motor, world, null, in input);

            Assert.AreEqual(Fix64.FromRatio(-1, 5), result.MotionState.Velocity.Y);
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
            Assert.AreEqual(Fix64.Zero, result.ImpactVelocity.Y);
        }

        [Test]
        public void Step_SnapEnabledCandidateCacheMatchesUncachedResult()
        {
            CombatPhysicsWorld expectedWorld = CreateWorldWithGround(CombatSurfaceMaterial.Rough);
            CombatPhysicsWorld cachedWorld = CreateWorldWithGround(CombatSurfaceMaterial.Rough);
            var expectedMotor = new CombatKinematicMotor(CreateConfig(groundSnapDistance: Fix64.FromRatio(1, 10)));
            var cachedMotor = new CombatKinematicMotor(CreateConfig(groundSnapDistance: Fix64.FromRatio(1, 10)));
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(cachedWorld, BlockingMask, CombatPhysicsLayerMask.None);
            CharacterKinematicPhysicsStepInput input = CreateInput(y: 1050, wasGrounded: true);

            CharacterKinematicPhysicsStepResult expected = Adapter.Step(
                expectedMotor, expectedWorld, new CombatMotionStepBuffer(), in input);
            CharacterKinematicPhysicsStepResult actual = Adapter.Step(
                cachedMotor, cachedWorld, new CombatMotionStepBuffer(), in input, cache);

            AssertEquivalentPhysicsResult(expected, actual);
            Assert.Greater(cachedMotor.LastQueryStats.CandidateCacheHitCount, 0);
        }

        [Test]
        public void Step_RootMotionOnlyVelocityDoesNotBecomePersistent()
        {
            var world = new CombatPhysicsWorld();
            var motor = new CombatKinematicMotor(CreateConfig());

            CharacterKinematicPhysicsStepResult result = Adapter.Step(
                motor,
                world,
                null,
                CreateInput(
                    horizontalVelocityX: 600,
                    hasRootMotion: true,
                    rootMotionHorizontalVelocityX: 600,
                    persistentHorizontalVelocityX: 0,
                    wasGrounded: false));

            Assert.AreEqual(0, result.HorizontalVelocityX);
            Assert.AreEqual(0, result.HorizontalVelocityY);
            Assert.AreEqual(10, result.PositionXMillimeters);
        }

        [Test]
        public void Step_LowAabbStepSetsStepUpAndGrounded()
        {
            CombatPhysicsWorld world = CreateWorldWithGround();
            RegisterStaticAabb(
                world,
                entity: 6,
                body: 6,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromInt(1), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(4), Fix64.FromRatio(1, 4), Fix64.FromInt(2)),
                surfaceMaterial: CombatSurfaceMaterial.Rough);
            var motor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.FromRatio(35, 100)));
            int x = 0;
            int y = 1000;
            int z = 0;
            bool grounded = true;
            bool steppedUp = false;
            CombatSurfaceMaterial surfaceMaterial = CombatSurfaceMaterial.Default;

            for (int i = 0; i < 25; i++)
            {
                int previousX = x;
                int previousY = y;
                int previousZ = z;
                CharacterKinematicPhysicsStepResult result = Adapter.Step(
                    motor,
                    world,
                    new CombatMotionStepBuffer(),
                    CreateInput(
                        x: x,
                        y: y,
                        z: z,
                        previousX: previousX,
                        previousY: previousY,
                        previousZ: previousZ,
                        horizontalVelocityX: 6000,
                        wasGrounded: grounded,
                        maxStepHeightMillimeters: 350));

                x = result.PositionXMillimeters;
                y = result.PositionYMillimeters;
                z = result.PositionZMillimeters;
                grounded = result.Grounded;
                surfaceMaterial = result.SurfaceMaterial;
                steppedUp |= (result.CollisionFlags & CombatMotionCollisionFlags.StepUp) == CombatMotionCollisionFlags.StepUp;
            }

            Assert.IsTrue(steppedUp);
            Assert.IsTrue(grounded);
            Assert.AreEqual(CombatSurfaceMaterial.Rough, surfaceMaterial);
            Assert.Greater(x, 1000);
            Assert.Greater(y, 1000);
        }

        [Test]
        public void Step_CandidateCacheOverloadMatchesDefaultStep()
        {
            CombatPhysicsWorld expectedWorld = CreateLowStepWorld();
            CombatPhysicsWorld cachedWorld = CreateLowStepWorld();
            var expectedMotor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.FromRatio(35, 100)));
            var cachedMotor = new CombatKinematicMotor(CreateConfig(maxStepHeight: Fix64.FromRatio(35, 100)));
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(cachedWorld, BlockingMask, CombatPhysicsLayerMask.None);
            CharacterKinematicPhysicsStepInput input = CreateInput(
                x: 0,
                y: 1000,
                z: 0,
                horizontalVelocityX: 6000,
                wasGrounded: true,
                maxStepHeightMillimeters: 350);

            CharacterKinematicPhysicsStepResult expected = Adapter.Step(
                expectedMotor,
                expectedWorld,
                new CombatMotionStepBuffer(),
                in input);
            CharacterKinematicPhysicsStepResult actual = Adapter.Step(
                cachedMotor,
                cachedWorld,
                new CombatMotionStepBuffer(),
                in input,
                cache);

            AssertEquivalentPhysicsResult(expected, actual);
            Assert.Greater(cachedMotor.LastQueryStats.CandidateCacheHitCount, 0);
            Assert.AreEqual(0, cachedMotor.LastQueryStats.CandidateCacheFallbackCount);
        }

        [Test]
        public void Step_SurfaceMaterialAndGroundContactClearBounceCap()
        {
            CombatPhysicsWorld world = CreateWorldWithGround(CombatSurfaceMaterial.Rough);
            var motor = new CombatKinematicMotor(CreateConfig());

            CharacterKinematicPhysicsStepResult result = Adapter.Step(
                motor,
                world,
                new CombatMotionStepBuffer(),
                CreateInput(
                    y: 1000,
                    wasGrounded: true,
                    surfaceBounceSpeedCapY: 4000));

            Assert.AreEqual(CombatSurfaceMaterial.Rough, result.SurfaceMaterial);
            Assert.AreEqual(60000, result.SurfaceFrictionMilli);
            Assert.AreEqual(0, result.SurfaceBounceSpeedCapY);
            Assert.IsTrue(result.Grounded);
        }

        private static void AssertEquivalentPhysicsResult(
            CharacterKinematicPhysicsStepResult expected,
            CharacterKinematicPhysicsStepResult actual)
        {
            Assert.AreEqual(expected.StepResult, actual.StepResult);
            Assert.AreEqual(expected.MotionState, actual.MotionState);
            Assert.AreEqual(expected.MotionInput, actual.MotionInput);
            Assert.AreEqual(expected.ImpactVelocity, actual.ImpactVelocity);
            Assert.AreEqual(expected.PositionXMillimeters, actual.PositionXMillimeters);
            Assert.AreEqual(expected.PositionYMillimeters, actual.PositionYMillimeters);
            Assert.AreEqual(expected.PositionZMillimeters, actual.PositionZMillimeters);
            Assert.AreEqual(expected.HorizontalVelocityX, actual.HorizontalVelocityX);
            Assert.AreEqual(expected.HorizontalVelocityY, actual.HorizontalVelocityY);
            Assert.AreEqual(expected.VerticalVelocity, actual.VerticalVelocity);
            Assert.AreEqual(expected.Grounded, actual.Grounded);
            Assert.AreEqual(expected.CollisionFlags, actual.CollisionFlags);
            Assert.AreEqual(expected.SurfaceFrictionMilli, actual.SurfaceFrictionMilli);
            Assert.AreEqual(expected.SurfaceMaterial, actual.SurfaceMaterial);
            Assert.AreEqual(expected.SurfaceBounceSpeedCapY, actual.SurfaceBounceSpeedCapY);
        }

        [Test]
        public void Step_ActiveLocomotionWallSlideMinimumSpeedSurvivesMillimeterRoundTrip()
        {
            CombatPhysicsWorld world = CreateWorldWithGround();
            RegisterStaticAabb(
                world,
                entity: 3,
                body: 3,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(
                    Fix64.FromRatio(51, 100),
                    Fix64.Zero,
                    -Fix64.FromInt(20)),
                localMax: new FixVector3(
                    Fix64.FromInt(2),
                    Fix64.FromInt(4),
                    Fix64.FromInt(20)),
                surfaceMaterial: CombatSurfaceMaterial.Default);
            var motor = new CombatKinematicMotor(CreateConfig(
                groundSnapDistance: Fix64.FromRatio(1, 10)));
            var buffer = new CombatMotionStepBuffer();
            int x = 0;
            int y = 0;
            int z = 0;
            int previousX = x;
            int previousY = y;
            int previousZ = z;
            CharacterKinematicPhysicsStepResult result = default;

            for (int i = 0; i < 120; i++)
            {
                CharacterKinematicPhysicsStepInput input = CreateInput(
                    x: x,
                    y: y,
                    z: z,
                    previousX: previousX,
                    previousY: previousY,
                    previousZ: previousZ,
                    centerOffsetY: Fix64.One,
                    horizontalVelocityX: 4243,
                    horizontalVelocityY: 4243,
                    wasGrounded: true,
                    applyGroundSurfaceFriction: false,
                    wallSlideMinimumHorizontalSpeedMillimetersPerSecond: 6000);
                result = Adapter.Step(motor, world, buffer, in input);

                Assert.IsFalse(
                    HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.IterationLimit),
                    "wall slide must not exhaust the collision loop at tick " + i);
                Assert.IsTrue(
                    HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.Wall),
                    "wall contact must remain typed at tick " + i);
                Assert.That(
                    result.PositionZMillimeters - z,
                    Is.InRange(99, 101),
                    "active locomotion must retain approximately 6m/s along the wall at tick " + i);

                previousX = x;
                previousY = y;
                previousZ = z;
                x = result.PositionXMillimeters;
                y = result.PositionYMillimeters;
                z = result.PositionZMillimeters;
            }

            Assert.That(z, Is.InRange(11990, 12010));
            Assert.That(result.HorizontalVelocityY, Is.InRange(5990, 6010));
            Assert.That(result.HorizontalVelocityX, Is.InRange(-10, 10));
        }

        private static CharacterKinematicPhysicsStepInput CreateInput(
            int x = 0,
            int y = 0,
            int z = 0,
            int previousX = 0,
            int previousY = 0,
            int previousZ = 0,
            Fix64 centerOffsetY = default,
            int horizontalVelocityX = 0,
            int horizontalVelocityY = 0,
            int verticalVelocity = 0,
            int impactVerticalVelocity = 0,
            bool wasGrounded = false,
            bool applyGroundSurfaceFriction = true,
            bool hasRootMotion = false,
            int persistentHorizontalVelocityX = 0,
            int persistentHorizontalVelocityY = 0,
            int persistentVerticalVelocity = 0,
            int rootMotionHorizontalVelocityX = 0,
            int rootMotionHorizontalVelocityY = 0,
            int rootMotionVerticalVelocity = 0,
            int surfaceBounceSpeedCapY = 0,
            int maxStepHeightMillimeters = 0,
            int wallSlideMinimumHorizontalSpeedMillimetersPerSecond = 0)
        {
            return new CharacterKinematicPhysicsStepInput(
                CharacterEntity,
                CharacterBody,
                CombatFrame.Zero,
                x,
                y,
                z,
                previousX,
                previousY,
                previousZ,
                centerOffsetY,
                horizontalVelocityX,
                horizontalVelocityY,
                verticalVelocity,
                impactVerticalVelocity,
                wasGrounded,
                applyGroundSurfaceFriction,
                hasRootMotion,
                persistentHorizontalVelocityX,
                persistentHorizontalVelocityY,
                persistentVerticalVelocity,
                rootMotionHorizontalVelocityX,
                rootMotionHorizontalVelocityY,
                rootMotionVerticalVelocity,
                surfaceBounceSpeedCapY,
                maxStepHeightMillimeters,
                GroundContactProbeVelocity,
                MoveFacingEpsilon,
                InputAxisScale,
                wallSlideMinimumHorizontalSpeedMillimetersPerSecond);
        }

        private static CombatMotionConfig CreateConfig(
            Fix64 maxStepHeight = default,
            Fix64 groundSnapDistance = default)
        {
            return new CombatMotionConfig(
                CombatStepConfig.Default,
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                moveSpeed: Fix64.One,
                gravityPerSecond: -Fix64.FromInt(30),
                jumpSpeed: Fix64.Zero,
                maxFallSpeed: Fix64.FromInt(50),
                skinWidth: Fix64.FromRatio(1, 100),
                groundMinNormalY: Fix64.Half,
                ceilingMinNormalY: Fix64.Half,
                collisionLayerMask: BlockingMask.Value,
                maxSlideIterations: 3,
                maxStepHeight: maxStepHeight,
                minElasticImpactSpeed: CombatMotionConfig.DefaultMinElasticImpactSpeed,
                fixedBounceGravityPerSecond: Fix64.FromInt(30),
                groundSnapDistance: groundSnapDistance);
        }

        private static void AssertTypedIntentSuppresses(CharacterKinematicPhysicsStepInput input)
        {
            CombatPhysicsWorld world = CreateWorldWithGround();
            var motor = new CombatKinematicMotor(CreateConfig(groundSnapDistance: Fix64.FromRatio(1, 10)));

            CharacterKinematicPhysicsStepResult result = Adapter.Step(motor, world, null, in input);

            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
        }

        private static CombatPhysicsWorld CreateSlopeWorld(int sinRaw, int cosRaw, out int startYMillimeters)
        {
            FixVector3 tangent = new FixVector3(Fix64.FromRaw(cosRaw), Fix64.FromRaw(sinRaw), Fix64.Zero);
            FixVector3 normal = new FixVector3(-Fix64.FromRaw(sinRaw), Fix64.FromRaw(cosRaw), Fix64.Zero);
            var slopeBody = new CombatBodyId(2);
            var slope = new CombatPhysicsObbCollider(
                slopeBody,
                new CombatColliderId(1),
                1,
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(10), Fix64.Half, Fix64.FromInt(5)),
                tangent,
                normal,
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                CombatSurfaceMaterial.Rough);
            normal = slope.AxisY;
            Fix64 touchingY = (Fix64.One + Fix64.Half * normal.Y) / normal.Y;
            startYMillimeters = ToMillimeters(touchingY + Fix64.FromRatio(2, 100));
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), slopeBody, FixVector3.Zero));
            world.UpsertObbCollider(slope);
            return world;
        }

        private static CombatPhysicsWorld CreateFormalSlopeWorld(
            int sinRaw,
            int cosRaw,
            out int startXMillimeters,
            out int startYMillimeters,
            out int startZMillimeters)
        {
            FixVector3 axisX = new FixVector3(Fix64.Zero, Fix64.Zero, -Fix64.One);
            FixVector3 axisY = new FixVector3(Fix64.FromRaw(sinRaw), Fix64.FromRaw(cosRaw), Fix64.Zero);
            FixVector3 axisZ = new FixVector3(Fix64.FromRaw(cosRaw), -Fix64.FromRaw(sinRaw), Fix64.Zero);
            var slopeBody = new CombatBodyId(2);
            FixVector3 slopeCenter = new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero);
            var slope = new CombatPhysicsObbCollider(
                slopeBody,
                new CombatColliderId(1),
                1,
                FixVector3.Zero,
                new FixVector3(
                    Fix64.FromRaw(1250000),
                    Fix64.FromRaw(250000),
                    Fix64.FromInt(3)),
                axisX,
                axisY,
                axisZ,
                CombatSurfaceMaterial.Rough);
            axisY = slope.AxisY;
            axisZ = slope.AxisZ;
            Fix64 radius = Fix64.FromRaw(450000);
            Fix64 segmentHalf = Fix64.FromRaw(450000);
            Fix64 support = radius + segmentHalf * axisY.Y.Abs();
            FixVector3 topPoint = slopeCenter
                + axisY * slope.HalfExtents.Y
                + axisZ * Fix64.FromRaw(1800000);
            FixVector3 characterCenter = topPoint + axisY * support;
            startXMillimeters = ToMillimeters(characterCenter.X);
            startYMillimeters = ToMillimeters(characterCenter.Y - Fix64.FromRaw(900000));
            startZMillimeters = ToMillimeters(characterCenter.Z);

            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(2),
                slopeBody,
                slopeCenter));
            world.UpsertObbCollider(slope);
            return world;
        }

        private static CombatMotionConfig CreateFormalSlopeConfig()
        {
            return new CombatMotionConfig(
                60,
                Fix64.One,
                Fix64.Zero,
                Fix64.Zero,
                -Fix64.FromInt(200),
                Fix64.FromRaw(10000),
                Fix64.FromRaw(707128),
                Fix64.Half,
                CombatMotionConfig.DefaultMaxCollisionIterations,
                new FixVector3(
                    Fix64.FromRaw(450000),
                    Fix64.FromRaw(900000),
                    Fix64.FromRaw(450000)),
                BlockingMask,
                Fix64.FromRaw(350000),
                CombatMotionConfig.DefaultMinElasticImpactSpeed,
                Fix64.Zero,
                Fix64.FromRaw(100000));
        }

        private static int ToMillimeters(Fix64 value)
        {
            return (int)((value.RawValue * 1000L + Fix64.Scale / 2L) / Fix64.Scale);
        }

        private static bool HasFlag(CombatMotionCollisionFlags value, CombatMotionCollisionFlags flag)
        {
            return (value & flag) == flag;
        }

        private static CombatPhysicsWorld CreateWorldWithGround(CombatSurfaceMaterial surfaceMaterial = default)
        {
            CombatSurfaceMaterial material = surfaceMaterial.Kind == CombatSurfaceMaterialKind.Default
                ? CombatSurfaceMaterial.Default
                : surfaceMaterial;
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(CharacterEntity, CharacterBody, new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero)));
            RegisterStaticAabb(
                world,
                entity: 2,
                body: 2,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                localMax: new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)),
                surfaceMaterial: material);
            return world;
        }

        private static CombatPhysicsWorld CreateLowStepWorld()
        {
            CombatPhysicsWorld world = CreateWorldWithGround();
            RegisterStaticAabb(
                world,
                entity: 6,
                body: 6,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.FromInt(1), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(4), Fix64.FromRatio(1, 4), Fix64.FromInt(2)),
                surfaceMaterial: CombatSurfaceMaterial.Rough);
            return world;
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
    }
}
