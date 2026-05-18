using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterMotionResolverTests
    {
        private static readonly CombatEntityId CharacterEntity = new CombatEntityId(1);
        private static readonly CombatBodyId CharacterBody = new CombatBodyId(1);
        private static readonly CombatPhysicsLayerMask BlockingMask = CombatPhysicsLayerMask.FromLayer(1);

        [Test]
        public void ReplaySameCommands_ProducesSameMotionAndWorldSync()
        {
            CharacterMotionSnapshot first = Replay();
            CharacterMotionSnapshot second = Replay();

            Assert.AreEqual(first.Position, second.Position);
            Assert.AreEqual(first.Velocity, second.Velocity);
            Assert.AreEqual(first.Grounded, second.Grounded);
            Assert.AreEqual(first.CollisionFlags, second.CollisionFlags);
            Assert.AreEqual(first.WorldRevision, second.WorldRevision);
        }

        [Test]
        public void ReactionAndDisabledLocks_StopMoveAndJump()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            machine.BeginReaction(RuntimeFrame.Zero);
            var resolver = new CharacterMotionResolver(new CombatKinematicMotor(CreateConfig()));
            CharacterCommand command = CreateCommand(frame: 1, move: UnitX, jumpPressed: true);
            CombatMotionState state = CreateState(0, GroundedStart, FixVector3.Zero, grounded: true);

            CombatMotionInput input = resolver.CreateMotionInput(command, machine);

            Assert.AreEqual(FixVector3.Zero, input.MoveDirection);
            Assert.IsFalse(input.JumpPressed);

            machine.Disable(new RuntimeFrame(2));
            input = resolver.CreateMotionInput(command, machine);

            Assert.AreEqual(FixVector3.Zero, input.MoveDirection);
            Assert.IsFalse(input.JumpPressed);
        }

        [Test]
        public void ActionState_UsesActionSpeedScaleUnlessMoveLocked()
        {
            var settings = new CharacterMotionSettings(
                Fix64.One,
                sprintMoveSpeedScale: Fix64.FromInt(2),
                actionMoveSpeedScale: Fix64.Half,
                reactionMoveSpeedScale: Fix64.Zero,
                disabledMoveSpeedScale: Fix64.Zero,
                tractionMoveSpeedScale: Fix64.One);
            var resolver = new CharacterMotionResolver(new CombatKinematicMotor(CreateConfig()), settings);
            var machine = new CharacterControlStateMachine(CreateEntity());
            machine.BeginAction(RuntimeFrame.Zero, CharacterControlLockMask.Jump);
            CharacterCommand command = CreateCommand(frame: 1, move: UnitX, jumpPressed: true);

            CombatMotionInput input = resolver.CreateMotionInput(command, machine);

            Assert.AreEqual(UnitX, input.MoveDirection);
            Assert.AreEqual(Fix64.Half, input.MoveSpeedScale);
            Assert.IsFalse(input.JumpPressed);

            machine.SetControlLockMask(CharacterControlLockMask.Move | CharacterControlLockMask.Jump, new RuntimeFrame(2));
            input = resolver.CreateMotionInput(command, machine);

            Assert.AreEqual(FixVector3.Zero, input.MoveDirection);
            Assert.AreEqual(Fix64.Half, input.MoveSpeedScale);
        }

        [Test]
        public void Step_SyncsCombatPhysicsWorldBodyPosition()
        {
            CombatPhysicsWorld world = CreateWorld(GroundedStart);
            var resolver = new CharacterMotionResolver(new CombatKinematicMotor(CreateConfig()));
            var machine = new CharacterControlStateMachine(CreateEntity());
            CombatMotionState state = CreateState(0, GroundedStart, FixVector3.Zero, grounded: true);
            CharacterCommand command = CreateCommand(frame: 0, move: UnitX, jumpPressed: false);

            CharacterMotionResult result = resolver.Step(command, machine, state, world);

            Assert.IsTrue(result.WorldSynced);
            Assert.IsTrue(world.TryGetBody(CharacterBody, out CombatPhysicsBody body));
            Assert.AreEqual(result.Position, body.Position);
            Assert.AreEqual(result.Position, result.StepResult.State.Position);
            Assert.Greater(result.WorldRevision, 1);
        }

        private static CharacterMotionSnapshot Replay()
        {
            CombatPhysicsWorld world = CreateWorld(GroundedStart);
            var resolver = new CharacterMotionResolver(new CombatKinematicMotor(CreateConfig()));
            var machine = new CharacterControlStateMachine(CreateEntity());
            CombatMotionState state = CreateState(0, GroundedStart, FixVector3.Zero, grounded: true);
            CharacterMotionResult result = default;
            for (int i = 0; i < 24; i++)
            {
                CharacterCommand command = CreateCommand(i, i < 12 ? UnitX : -UnitX, jumpPressed: i == 2);
                result = resolver.Step(command, machine, state, world);
                state = result.StepResult.State;
            }

            return new CharacterMotionSnapshot(state.Position, state.Velocity, state.Grounded, state.CollisionFlags, result.WorldRevision);
        }

        private static CharacterCommand CreateCommand(int frame, FixVector3 move, bool jumpPressed)
        {
            return new CharacterCommand(
                new RuntimeFrame(frame),
                sourceId: 10,
                CreateEntity(),
                move,
                CharacterFacingBasis.Identity,
                jumpPressed,
                sprintHeld: false,
                CharacterActionButtons.None,
                default(CharacterActionRequest),
                traceId: "move-" + frame);
        }

        private static CharacterControlEntityRef CreateEntity()
        {
            return CharacterControlEntityRef.FromGameplayAndCombat(
                new GameplayEntityId(1, 1),
                CharacterEntity,
                CharacterBody,
                stableId: 1);
        }

        private static CombatMotionConfig CreateConfig()
        {
            return new CombatMotionConfig(
                CombatStepConfig.Default,
                characterHalfExtents: new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
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

        private static CombatMotionState CreateState(int frame, FixVector3 position, FixVector3 velocity, bool grounded)
        {
            return new CombatMotionState(
                new CombatFrame(frame),
                position,
                velocity,
                grounded,
                FixVector3.Zero,
                grounded ? CombatMotionCollisionFlags.Grounded : CombatMotionCollisionFlags.None);
        }

        private static CombatPhysicsWorld CreateWorld(FixVector3 startPosition)
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(CharacterEntity, CharacterBody, startPosition));
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), new CombatBodyId(2), FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(2),
                new CombatColliderId(1),
                layer: 1,
                localMin: new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                localMax: new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20))));
            return world;
        }

        private static FixVector3 GroundedStart => new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);

        private static FixVector3 UnitX => new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);

        private readonly struct CharacterMotionSnapshot
        {
            public CharacterMotionSnapshot(
                FixVector3 position,
                FixVector3 velocity,
                bool grounded,
                CombatMotionCollisionFlags collisionFlags,
                int worldRevision)
            {
                Position = position;
                Velocity = velocity;
                Grounded = grounded;
                CollisionFlags = collisionFlags;
                WorldRevision = worldRevision;
            }

            public FixVector3 Position { get; }

            public FixVector3 Velocity { get; }

            public bool Grounded { get; }

            public CombatMotionCollisionFlags CollisionFlags { get; }

            public int WorldRevision { get; }
        }
    }
}
