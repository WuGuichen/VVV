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

        private static CombatMotionConfig CreateConfig()
        {
            return new CombatMotionConfig(
                CombatStepConfig.Default,
                CharacterHalfExtents,
                moveSpeed: Fix64.FromInt(6),
                gravityPerSecond: -Fix64.FromInt(30),
                jumpSpeed: Fix64.FromInt(12),
                maxFallSpeed: Fix64.FromInt(40),
                skinWidth: Fix64.FromRatio(1, 100),
                groundMinNormalY: Fix64.Half,
                ceilingMinNormalY: Fix64.Half,
                collisionLayerMask: BlockingMask.Value,
                maxSlideIterations: 3);
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

        private static CombatMotionInput CreateInput(FixVector3 moveDirection, bool jumpPressed)
        {
            return new CombatMotionInput(moveDirection, jumpPressed, Fix64.One);
        }

        private static CombatPhysicsWorld CreateWorldWithGround(FixVector3 startPosition)
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, startPosition));
            RegisterStaticAabb(world, entity: 2, body: 2, collider: 1, layer: 1, position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                localMax: new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)));
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
            FixVector3 localMax)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, position));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(collider),
                layer,
                localMin,
                localMax));
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
