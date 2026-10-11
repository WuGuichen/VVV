using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestSlopeMotionSourcesDiagnosticsTests
    {
        [Test]
        public void CollisionSelectionUsesFrozenPriorityAndStableFirstTie()
        {
            CombatMotionCollision wall = Collision(10, CombatMotionCollisionFlags.Wall);
            CombatMotionCollision steep = Collision(11, CombatMotionCollisionFlags.SteepSlope);
            CombatMotionCollision groundedFirst = Collision(12, CombatMotionCollisionFlags.Grounded);
            CombatMotionCollision groundedSecond = Collision(13, CombatMotionCollisionFlags.Grounded);
            CombatMotionCollision snap = Collision(
                14,
                CombatMotionCollisionFlags.Grounded | CombatMotionCollisionFlags.GroundSnap);

            AssertSelected(Step(wall, steep, groundedFirst, snap), 14);
            AssertSelected(Step(wall, steep, groundedFirst), 12);
            AssertSelected(Step(groundedFirst, groundedSecond), 12);
            AssertSelected(Step(wall, steep), 11);
            AssertSelected(Step(wall), 10);
            Assert.IsFalse(CharacterTestCharacterMotionSystem.TrySelectDiagnosticCollision(
                Step(),
                out _));

            Assert.AreEqual(
                CharacterTestMotionSurfaceClassification.Walkable,
                CharacterTestCharacterMotionSystem.ResolveSurfaceClassification(
                    CombatMotionCollisionFlags.Grounded));
            Assert.AreEqual(
                CharacterTestMotionSurfaceClassification.Steep,
                CharacterTestCharacterMotionSystem.ResolveSurfaceClassification(
                    CombatMotionCollisionFlags.SteepSlope));
            Assert.AreEqual(
                CharacterTestMotionSurfaceClassification.Ceiling,
                CharacterTestCharacterMotionSystem.ResolveSurfaceClassification(
                    CombatMotionCollisionFlags.Ceiling));
            Assert.AreEqual(
                CharacterTestMotionSurfaceClassification.Wall,
                CharacterTestCharacterMotionSystem.ResolveSurfaceClassification(
                    CombatMotionCollisionFlags.Wall));
            Assert.AreEqual(
                CharacterTestMotionSurfaceClassification.None,
                CharacterTestCharacterMotionSystem.ResolveSurfaceClassification(
                    CombatMotionCollisionFlags.None));
        }

        [Test]
        public void ActiveLocomotionWallSlideFloorUsesLowerOfSimulatedAndTargetSpeed()
        {
            Assert.AreEqual(
                5000,
                ResolveWallSlideFloor(
                    simulatedX: 6000,
                    simulatedY: 0,
                    targetX: 3000,
                    targetY: 4000));
            Assert.AreEqual(
                3000,
                ResolveWallSlideFloor(
                    simulatedX: 1800,
                    simulatedY: 2400,
                    targetX: 6000,
                    targetY: 0));
        }

        [Test]
        public void SpecialOrExternalMotionDisablesActiveLocomotionWallSlideFloor()
        {
            Assert.AreEqual(0, ResolveWallSlideFloor(hasActiveMoveInput: false));
            Assert.AreEqual(0, ResolveWallSlideFloor(preserveExternalHorizontalVelocity: true));
            Assert.AreEqual(0, ResolveWallSlideFloor(hasRootMotion: true));
            Assert.AreEqual(0, ResolveWallSlideFloor(hasVelocityOverride: true));
            Assert.AreEqual(0, ResolveWallSlideFloor(hasHorizontalVelocityOverride: true));
            Assert.AreEqual(0, ResolveWallSlideFloor(hasExternalAcceleration: true));
            Assert.AreEqual(0, ResolveWallSlideFloor(hasImpulse: true));
            Assert.AreEqual(0, ResolveWallSlideFloor(contactVelocityX: 1000));
            Assert.AreEqual(6000, ResolveWallSlideFloor());
        }

        [Test]
        public void RealObbGroundSupportProducesExactTypedSameFrameDiagnostic()
        {
            CharacterTestCharacterMotionDiagnostic diagnostic = RunGroundDiagnostic(17);

            Assert.AreEqual(new RuntimeFrame(17), diagnostic.Frame);
            Assert.AreEqual(1, diagnostic.EntityId);
            Assert.IsTrue(diagnostic.HasPhysics);
            Assert.IsTrue(diagnostic.HasCollision);
            Assert.AreEqual(new CombatBodyId(2), diagnostic.TargetBodyId);
            Assert.AreEqual(new CombatColliderId(1), diagnostic.TargetColliderId);
            Assert.AreEqual(CombatPhysicsShapeKind.Obb, diagnostic.TargetShapeKind);
            Assert.IsTrue(diagnostic.Normal.Y > Fix64.Zero);
            Assert.AreEqual(CharacterTestMotionSurfaceClassification.Walkable, diagnostic.SurfaceClassification);
            Assert.IsTrue(HasFlag(diagnostic.CollisionFlags, CombatMotionCollisionFlags.Grounded));
            Assert.IsTrue(HasFlag(diagnostic.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, diagnostic.SurfaceMaterial.Kind);
            Assert.AreEqual(Fix64.FromRaw(707128), diagnostic.GroundMinNormalY);
            Assert.AreEqual(Fix64.Zero, diagnostic.GroundSnapAppliedDistance);
            Assert.IsTrue(diagnostic.Grounded);
        }

        [Test]
        public void PhysicsWithoutCollisionIsReportedAndReplayResetClearsTransientSample()
        {
            CreateCharacterWorld(
                yMeters: 10f,
                grounded: false,
                out CharacterTestCharacterRuntimeWorld characterWorld,
                out CharacterTestCharacterEntity entity);
            var physicsWorld = new CombatPhysicsWorld();
            var body = new CombatBodyId(2);
            physicsWorld.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(2),
                body,
                new FixVector3(Fix64.FromInt(100), Fix64.Zero, Fix64.Zero)));
            physicsWorld.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                body,
                new CombatColliderId(1),
                1,
                new FixVector3(-Fix64.One, -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.One, Fix64.One, Fix64.One)));
            var runtime = CreatePhysicsRuntime(physicsWorld, staticColliderCount: 1);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetPhysicsRuntime(runtime);

            motion.Tick(characterWorld, 60, new RuntimeFrame(23));

            Assert.IsTrue(motion.TryGetCharacterMotionDiagnostic(entity.Id.Value, out var diagnostic));
            Assert.IsTrue(diagnostic.HasPhysics);
            Assert.IsFalse(diagnostic.HasCollision);
            Assert.AreEqual((CombatPhysicsShapeKind)0, diagnostic.TargetShapeKind);
            Assert.AreEqual(CharacterTestMotionSurfaceClassification.None, diagnostic.SurfaceClassification);

            motion.ResetRuntimeAiLabReplayTransientState();
            Assert.IsFalse(motion.TryGetCharacterMotionDiagnostic(entity.Id.Value, out _));
        }

        [Test]
        public void IdenticalWorldAndInputProduceEqualTypedDiagnostics()
        {
            CharacterTestCharacterMotionDiagnostic first = RunGroundDiagnostic(31);
            CharacterTestCharacterMotionDiagnostic second = RunGroundDiagnostic(31);

            Assert.AreEqual(first, second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        private static CharacterTestCharacterMotionDiagnostic RunGroundDiagnostic(long frame)
        {
            CreateCharacterWorld(
                yMeters: 0f,
                grounded: true,
                out CharacterTestCharacterRuntimeWorld characterWorld,
                out CharacterTestCharacterEntity entity);
            var physicsWorld = new CombatPhysicsWorld();
            var body = new CombatBodyId(2);
            physicsWorld.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(2),
                body,
                FixVector3.Zero));
            physicsWorld.UpsertObbCollider(new CombatPhysicsObbCollider(
                body,
                new CombatColliderId(1),
                1,
                new FixVector3(Fix64.Zero, Fix64.FromRaw(-250000), Fix64.Zero),
                new FixVector3(Fix64.FromInt(10), Fix64.FromRaw(250000), Fix64.FromInt(10)),
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                CombatSurfaceMaterial.Rough));
            var runtime = CreatePhysicsRuntime(physicsWorld, staticColliderCount: 1);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetPhysicsRuntime(runtime);

            motion.Tick(characterWorld, 60, new RuntimeFrame(frame));

            Assert.IsTrue(motion.TryGetCharacterMotionDiagnostic(entity.Id.Value, out var diagnostic));
            return diagnostic;
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePhysicsRuntime(
            CombatPhysicsWorld world,
            int staticColliderCount)
        {
            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.FromRaw(450000), Fix64.FromRaw(900000), Fix64.FromRaw(450000)),
                Fix64.FromRaw(10000),
                Fix64.FromRaw(350000),
                staticColliderCount,
                "typed diagnostic fixture; snap=not-authority",
                Fix64.FromRaw(100000));
        }

        private static void CreateCharacterWorld(
            float yMeters,
            bool grounded,
            out CharacterTestCharacterRuntimeWorld world,
            out CharacterTestCharacterEntity entity)
        {
            var definition = new CharacterTestCharacterDefinition(
                900001,
                "Slope diagnostic fixture",
                default(ResourceKey),
                1,
                default(CharacterRuntimeAnimationBinding));
            world = new CharacterTestCharacterRuntimeWorld();
            entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    yMeters,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            entity.SetMotionState(0f, Vector2.zero, grounded);
            entity.SetInput(CharacterTestCharacterInputComponent.None);
        }

        private static CombatMotionStepResult Step(params CombatMotionCollision[] collisions)
        {
            return new CombatMotionStepResult(
                new CombatMotionState(
                    new CombatFrame(1),
                    FixVector3.Zero,
                    FixVector3.Zero,
                    false,
                    FixVector3.Zero,
                    CombatMotionCollisionFlags.None),
                FixVector3.Zero,
                FixVector3.Zero,
                false,
                CombatMotionCollisionFlags.None,
                collisions,
                CombatSurfaceMaterial.Default);
        }

        private static CombatMotionCollision Collision(
            int bodyId,
            CombatMotionCollisionFlags flags)
        {
            return new CombatMotionCollision(
                new CombatEntityId(bodyId),
                new CombatBodyId(bodyId),
                new CombatColliderId(1),
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                Fix64.FromRaw(50000),
                Fix64.Half,
                flags,
                CombatSurfaceMaterial.Rough);
        }

        private static void AssertSelected(CombatMotionStepResult step, int expectedBodyId)
        {
            Assert.IsTrue(CharacterTestCharacterMotionSystem.TrySelectDiagnosticCollision(
                step,
                out CombatMotionCollision selected));
            Assert.AreEqual(expectedBodyId, selected.TargetBodyId.Value);
        }

        private static int ResolveWallSlideFloor(
            bool hasActiveMoveInput = true,
            bool preserveExternalHorizontalVelocity = false,
            int simulatedX = 4243,
            int simulatedY = 4243,
            int targetX = 4243,
            int targetY = 4243,
            bool hasRootMotion = false,
            bool hasVelocityOverride = false,
            bool hasHorizontalVelocityOverride = false,
            bool hasExternalAcceleration = false,
            bool hasImpulse = false,
            int contactVelocityX = 0,
            int contactVelocityY = 0)
        {
            return CharacterTestCharacterMotionSystem.ResolveWallSlideMinimumHorizontalSpeed(
                hasActiveMoveInput,
                preserveExternalHorizontalVelocity,
                simulatedX,
                simulatedY,
                targetX,
                targetY,
                hasRootMotion,
                hasVelocityOverride,
                hasHorizontalVelocityOverride,
                hasExternalAcceleration,
                hasImpulse,
                contactVelocityX,
                contactVelocityY);
        }

        private static bool HasFlag(
            CombatMotionCollisionFlags value,
            CombatMotionCollisionFlags expected)
        {
            return (value & expected) == expected;
        }
    }
}
