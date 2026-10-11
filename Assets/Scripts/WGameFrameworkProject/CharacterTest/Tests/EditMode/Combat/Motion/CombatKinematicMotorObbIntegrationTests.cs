using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Motion
{
    public sealed class CombatKinematicMotorObbIntegrationTests
    {
        private static readonly CombatEntityId SourceEntityId = new CombatEntityId(1);
        private static readonly CombatBodyId SourceBodyId = new CombatBodyId(1);
        private static readonly CombatPhysicsLayerMask BlockingMask = CombatPhysicsLayerMask.FromLayer(1);
        private static readonly FixVector3 UnitX = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
        private static readonly FixVector3 UnitY = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
        private static readonly FixVector3 UnitZ = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
        private static readonly FixVector3 CharacterHalfExtents = new FixVector3(Fix64.Half, Fix64.One, Fix64.Half);

        [Test]
        public void AxisAlignedFace_PreservesConservativePrimitiveHitIdentityMaterialAndAppliesSkinOnce()
        {
            CombatSurfaceMaterial material = CombatSurfaceMaterial.Rough;
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            RegisterObb(world, entity: 10, body: 20, collider: 30, layer: 1,
                center: new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero),
                halfExtents: new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                UnitX, UnitY, UnitZ, material);

            CombatMotionStepResult result = Step(world, FixVector3.Zero, UnitX, moveSpeed: Fix64.FromInt(4));
            CombatMotionCollision collision = FindCollision(result, new CombatBodyId(20));

            Assert.AreEqual(new CombatEntityId(10), collision.TargetEntityId);
            Assert.AreEqual(new CombatColliderId(30), collision.TargetColliderId);
            Assert.AreEqual(material, collision.SurfaceMaterial);
            Assert.AreEqual(499984L, collision.Distance.RawValue);
            Assert.AreEqual(124996L, collision.Fraction.RawValue);
            Assert.AreEqual(-UnitX, collision.Normal);
            Assert.AreEqual(489984L, result.State.Position.X.RawValue, "Motor must subtract the 0.01 m skin exactly once from the conservative primitive contact distance.");
        }

        [Test]
        public void RotatedThirtyDegreeFace_PreservesWorldNormal()
        {
            Fix64 cosine = Fix64.FromRaw(866025);
            Fix64 sine = Fix64.Half;
            var axisX = new FixVector3(cosine, Fix64.Zero, -sine);
            var axisZ = new FixVector3(sine, Fix64.Zero, cosine);
            CombatPhysicsWorld world = CreateWorld(axisX * Fix64.FromInt(3));
            RegisterObb(world, entity: 11, body: 21, collider: 31, layer: 1,
                center: FixVector3.Zero,
                halfExtents: new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                axisX, UnitY, axisZ, CombatSurfaceMaterial.HardSlick);

            CombatMotionStepResult result = Step(
                world,
                axisX * Fix64.FromInt(3),
                -axisX,
                moveSpeed: Fix64.FromInt(4));
            CombatMotionCollision collision = FindCollision(result, new CombatBodyId(21));

            AssertVectorNear(axisX, collision.Normal, 4);
            Assert.AreEqual(CombatSurfaceMaterial.HardSlick, collision.SurfaceMaterial);
            Assert.AreEqual(new CombatBodyId(21), collision.TargetBodyId);
        }

        [Test]
        public void HighSpeedThinObb_BlocksWithoutMappingPrimitiveLimitToMotorIterationLimit()
        {
            CombatPhysicsWorld world = CreateWorld(new FixVector3(-Fix64.FromInt(100), Fix64.Zero, Fix64.Zero));
            RegisterObb(world, entity: 12, body: 22, collider: 32, layer: 1,
                center: FixVector3.Zero,
                halfExtents: new FixVector3(Fix64.FromRatio(1, 100), Fix64.One, Fix64.One),
                UnitX, UnitY, UnitZ, CombatSurfaceMaterial.Default);

            CombatMotionStepResult result = Step(
                world,
                new FixVector3(-Fix64.FromInt(100), Fix64.Zero, Fix64.Zero),
                UnitX,
                moveSpeed: Fix64.FromInt(200));

            Assert.Greater(result.CollisionCount, 0);
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.IterationLimit));
            Assert.Less(result.State.Position.X.RawValue, -Fix64.Half.RawValue);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void InitialTouching_OnlyInwardMotionBlocks(bool inward)
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            RegisterObb(world, entity: 13, body: 23, collider: 33, layer: 1,
                center: new FixVector3(Fix64.FromRatio(3, 2), Fix64.Zero, Fix64.Zero),
                halfExtents: new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                UnitX, UnitY, UnitZ, CombatSurfaceMaterial.Default);

            FixVector3 direction = inward ? UnitX : -UnitX;
            CombatMotionStepResult result = Step(world, FixVector3.Zero, direction, moveSpeed: Fix64.One);

            Assert.AreEqual(inward, result.CollisionCount > 0);
            Assert.AreEqual(inward ? 0L : -Fix64.Scale, result.State.Position.X.RawValue);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void InitialOverlap_OnlyInwardMotionBlocks(bool inward)
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            RegisterObb(world, entity: 14, body: 24, collider: 34, layer: 1,
                center: new FixVector3(Fix64.FromRatio(7, 5), Fix64.Zero, Fix64.Zero),
                halfExtents: new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                UnitX, UnitY, UnitZ, CombatSurfaceMaterial.Default);

            FixVector3 direction = inward ? UnitX : -UnitX;
            CombatMotionStepResult result = Step(world, FixVector3.Zero, direction, moveSpeed: Fix64.One);

            Assert.AreEqual(inward, result.CollisionCount > 0);
            Assert.AreEqual(inward ? 0L : -Fix64.Scale, result.State.Position.X.RawValue);
        }

        [Test]
        public void FullyInside_UsesStableInitialNormalAndFractionZero()
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            RegisterObb(world, entity: 15, body: 25, collider: 35, layer: 1,
                center: FixVector3.Zero,
                halfExtents: new FixVector3(Fix64.FromInt(2), Fix64.FromRatio(3, 2), Fix64.FromInt(2)),
                UnitX, UnitY, UnitZ, CombatSurfaceMaterial.Default);

            CombatMotionStepResult result = Step(
                world,
                FixVector3.Zero,
                FixVector3.Zero,
                moveSpeed: Fix64.Zero,
                initialVerticalVelocity: -Fix64.One);
            CombatMotionCollision collision = FindCollision(result, new CombatBodyId(25));

            Assert.AreEqual(Fix64.Zero, collision.Fraction);
            Assert.AreEqual(Fix64.Zero, collision.Distance);
            Assert.AreEqual(UnitY, collision.Normal);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MixedAabbAndObb_SelectsEarliestConservativeContactRegardlessOfKind(bool obbFirst)
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            if (obbFirst)
            {
                RegisterFaceObb(world, entity: 20, body: 20, faceX: Fix64.FromInt(1));
                RegisterFaceAabb(world, entity: 30, body: 30, faceX: Fix64.FromInt(2));
            }
            else
            {
                RegisterFaceAabb(world, entity: 20, body: 20, faceX: Fix64.One);
                RegisterFaceObb(world, entity: 30, body: 30, faceX: Fix64.FromInt(2));
            }

            CombatMotionStepResult result = Step(world, FixVector3.Zero, UnitX, moveSpeed: Fix64.FromInt(4));

            Assert.AreEqual(new CombatBodyId(20), result.Collisions[0].TargetBodyId);
        }

        [Test]
        public void SphereCapsuleAndObbMixedWorld_SelectsObbEarliest()
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            RegisterSphere(world, entity: 40, body: 40, center: new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero));
            RegisterCapsule(world, entity: 41, body: 41, center: new FixVector3(Fix64.FromRatio(5, 2), Fix64.Zero, Fix64.Zero));
            RegisterFaceObb(world, entity: 42, body: 42, faceX: Fix64.One);

            CombatMotionStepResult result = Step(world, FixVector3.Zero, UnitX, moveSpeed: Fix64.FromInt(4));

            Assert.AreEqual(new CombatBodyId(42), result.Collisions[0].TargetBodyId);
        }

        [Test]
        public void SameFractionSameNormal_MergesHigherPrioritySurfaceMaterial()
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            RegisterFaceObb(world, entity: 50, body: 50, faceX: Fix64.One, material: CombatSurfaceMaterial.Default);
            RegisterFaceObb(world, entity: 51, body: 51, faceX: Fix64.One, material: CombatSurfaceMaterial.Rough);

            CombatMotionStepResult result = Step(world, FixVector3.Zero, UnitX, moveSpeed: Fix64.FromInt(4));

            Assert.AreEqual(new CombatBodyId(50), result.Collisions[0].TargetBodyId);
            Assert.AreEqual(CombatSurfaceMaterial.Rough, result.Collisions[0].SurfaceMaterial);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SameFractionDifferentNormal_UsesStableIdentityTieBreak(bool reverseInsertion)
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            Action addX = () => RegisterFaceObb(world, entity: 60, body: 60, faceX: Fix64.One);
            Action addZ = () => RegisterZFaceObb(world, entity: 61, body: 61, faceZ: Fix64.One);
            if (reverseInsertion) { addZ(); addX(); } else { addX(); addZ(); }

            FixVector3 diagonal = new FixVector3(Fix64.One, Fix64.Zero, Fix64.One);
            CombatMotionStepResult result = Step(world, FixVector3.Zero, diagonal, moveSpeed: Fix64.FromInt(4));

            Assert.AreEqual(new CombatBodyId(60), result.Collisions[0].TargetBodyId);
            Assert.AreEqual(-UnitX, result.Collisions[0].Normal);
        }

        [Test]
        public void LayerAndSourceEntityFiltersSkipIneligibleObbCandidates()
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            RegisterFaceObb(world, entity: SourceEntityId.Value, body: 70, faceX: Fix64.One);
            RegisterObb(world, entity: 71, body: 71, collider: 1, layer: 2,
                center: new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero),
                halfExtents: new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                UnitX, UnitY, UnitZ, CombatSurfaceMaterial.Default);

            CombatMotionStepResult result = Step(world, FixVector3.Zero, UnitX, moveSpeed: Fix64.FromInt(4));

            Assert.AreEqual(0, result.CollisionCount);
            Assert.AreEqual(Fix64.FromInt(4), result.State.Position.X);
        }

        [Test]
        public void CandidateCache_ColdWarmAndTopologyRebuildMatchWorldPath()
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            RegisterFaceObb(world, entity: 80, body: 80, faceX: Fix64.One);
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(world, BlockingMask, CombatPhysicsLayerMask.None);
            CombatMotionStepResult uncached = Step(world, FixVector3.Zero, UnitX, Fix64.FromInt(4));
            CombatMotionStepResult warm = Step(world, FixVector3.Zero, UnitX, Fix64.FromInt(4), cache);
            int rebuilds = cache.LastStats.StaticRebuildCount;

            RegisterFaceObb(world, entity: 81, body: 81, faceX: Fix64.FromRatio(3, 4));
            cache.Prepare(world, BlockingMask, CombatPhysicsLayerMask.None);
            CombatMotionStepResult uncachedAfterTopologyChange = Step(
                world,
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(4));
            CombatMotionStepResult rebuilt = Step(world, FixVector3.Zero, UnitX, Fix64.FromInt(4), cache);

            Assert.AreEqual(uncached, warm);
            Assert.AreEqual(new CombatBodyId(80), uncached.Collisions[0].TargetBodyId);
            Assert.AreEqual(new CombatBodyId(81), rebuilt.Collisions[0].TargetBodyId);
            Assert.Less(rebuilt.Collisions[0].Fraction.RawValue, uncached.Collisions[0].Fraction.RawValue);
            Assert.AreNotEqual(uncached, rebuilt);
            Assert.AreEqual(uncachedAfterTopologyChange, rebuilt,
                "A rebuilt static candidate cache must reproduce the complete uncached result for the same topology.");
            Assert.AreEqual(rebuilds + 1, cache.LastStats.StaticRebuildCount);
        }

        [Test]
        public void CandidateCache_DynamicCandidateUpdateDoesNotChangeStaticObbSelection()
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            RegisterFaceObb(world, entity: 82, body: 82, faceX: Fix64.One);
            RegisterSphere(world, entity: 83, body: 83,
                center: new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero), layer: 2);
            var cache = new CombatKinematicMotorCandidateCache();
            CombatPhysicsLayerMask dynamicMask = CombatPhysicsLayerMask.FromLayer(2);
            cache.Prepare(world, BlockingMask, dynamicMask);
            int updateCount = cache.LastStats.DynamicBodyUpdateCount;
            int collisionMask = BlockingMask.Value | dynamicMask.Value;
            CombatMotionStepResult before = Step(
                world,
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(4),
                cache,
                collisionLayerMask: collisionMask);

            world.SetBodyPosition(new CombatBodyId(83), new FixVector3(Fix64.FromRatio(3, 4), Fix64.Zero, Fix64.Zero));
            cache.UpdateDynamicBody(world, new CombatBodyId(83));
            CombatMotionStepResult uncached = Step(
                world,
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(4),
                collisionLayerMask: collisionMask);
            CombatMotionStepResult cached = Step(
                world,
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(4),
                cache,
                collisionLayerMask: collisionMask);

            Assert.AreEqual(new CombatBodyId(82), before.Collisions[0].TargetBodyId);
            Assert.AreEqual(new CombatBodyId(83), uncached.Collisions[0].TargetBodyId);
            Assert.AreEqual(uncached, cached);
            Assert.AreNotEqual(before, cached);
            Assert.Less(cached.Collisions[0].Fraction.RawValue, before.Collisions[0].Fraction.RawValue);
            Assert.AreEqual(updateCount + 1, cache.LastStats.DynamicBodyUpdateCount);
        }

        [Test]
        public void FractionZeroMultiContact_TerminatesAndStepUpRemainsAabbOnly()
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            RegisterFaceObb(
                world,
                entity: 90,
                body: 90,
                faceX: Fix64.Half,
                material: CombatSurfaceMaterial.Default);
            RegisterZFaceObb(
                world,
                entity: 91,
                body: 91,
                faceZ: Fix64.Half,
                material: CombatSurfaceMaterial.Rough);

            CombatMotionStepResult result = Step(
                world,
                FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.One),
                Fix64.One,
                cache: null,
                maxStepHeight: Fix64.One);

            Assert.AreEqual(2, result.CollisionCount);
            Assert.AreEqual(new CombatBodyId(90), result.Collisions[0].TargetBodyId);
            Assert.AreEqual(new CombatColliderId(1), result.Collisions[0].TargetColliderId);
            Assert.AreEqual(new CombatBodyId(91), result.Collisions[1].TargetBodyId);
            Assert.AreEqual(new CombatColliderId(1), result.Collisions[1].TargetColliderId);
            Assert.AreEqual(-UnitX, result.Collisions[0].Normal);
            Assert.AreEqual(-UnitZ, result.Collisions[1].Normal);
            Assert.AreEqual(CombatSurfaceMaterial.Rough, result.Collisions[1].SurfaceMaterial);
            Assert.AreEqual(CombatSurfaceMaterial.Rough, result.SurfaceMaterial);
            Assert.AreEqual(Fix64.Zero, result.State.Position.X);
            Assert.AreEqual(Fix64.Zero, result.State.Position.Z);
            Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.BlockedX));
            Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.BlockedZ));
            Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.Wall));
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.IterationLimit));
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.StepUp));
            Assert.AreEqual(-UnitZ, result.State.LastCollisionNormal);
        }

        [Test]
        public void ObbCollisionLoop_UsesExistingMotorIterationLimit()
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            RegisterFaceObb(world, entity: 92, body: 92, faceX: Fix64.One);

            CombatMotionStepResult result = Step(
                world,
                FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.One),
                Fix64.FromInt(4),
                maxCollisionIterations: 1);

            Assert.Greater(result.CollisionCount, 0);
            Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.IterationLimit));
        }

        [Test]
        public void InvalidObbCandidate_FailsFastWithBodyAndColliderIdentity()
        {
            FixVector3 start = new FixVector3(Fix64.FromInt(2001), Fix64.Zero, Fix64.Zero);
            CombatPhysicsWorld world = CreateWorld(start);
            RegisterObb(world, entity: 100, body: 123, collider: 456, layer: 1,
                center: new FixVector3(Fix64.FromInt(2003), Fix64.Zero, Fix64.Zero),
                halfExtents: new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                UnitX, UnitY, UnitZ, CombatSurfaceMaterial.Default);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Step(world, start, UnitX, Fix64.FromInt(4)));

            StringAssert.Contains("123", exception.Message);
            StringAssert.Contains("456", exception.Message);
        }

        [Test]
        public void ObbCastRoute_AcceptsEveryLegalBoolStatusCombination()
        {
            var bodyId = new CombatBodyId(123);
            var colliderId = new CombatColliderId(456);
            CombatObbCastStatus[] blockingStatuses =
            {
                CombatObbCastStatus.Hit,
                CombatObbCastStatus.IterationLimitConservativeHit,
                CombatObbCastStatus.PrecisionLimitConservativeHit,
                CombatObbCastStatus.InitialTouching,
                CombatObbCastStatus.InitialOverlap,
            };
            CombatObbCastStatus[] nonBlockingStatuses =
            {
                CombatObbCastStatus.Miss,
                CombatObbCastStatus.ZeroDelta,
                CombatObbCastStatus.InitialTouching,
                CombatObbCastStatus.InitialOverlap,
            };

            for (int i = 0; i < blockingStatuses.Length; i++)
            {
                Assert.IsTrue(CombatKinematicMotor.RouteObbCastResult(
                    true, blockingStatuses[i], bodyId, colliderId));
            }

            for (int i = 0; i < nonBlockingStatuses.Length; i++)
            {
                Assert.IsFalse(CombatKinematicMotor.RouteObbCastResult(
                    false, nonBlockingStatuses[i], bodyId, colliderId));
            }
        }

        [Test]
        public void ObbCastRoute_FailsFastForEveryImpossibleBoolStatusCombination()
        {
            var bodyId = new CombatBodyId(123);
            var colliderId = new CombatColliderId(456);
            CombatObbCastStatus[] invalidTrueStatuses =
            {
                CombatObbCastStatus.Miss,
                CombatObbCastStatus.ZeroDelta,
                CombatObbCastStatus.InvalidInput,
                (CombatObbCastStatus)99,
            };
            CombatObbCastStatus[] invalidFalseStatuses =
            {
                CombatObbCastStatus.Hit,
                CombatObbCastStatus.IterationLimitConservativeHit,
                CombatObbCastStatus.PrecisionLimitConservativeHit,
                CombatObbCastStatus.InvalidInput,
                (CombatObbCastStatus)99,
            };

            for (int i = 0; i < invalidTrueStatuses.Length; i++)
                AssertInvalidObbRoute(true, invalidTrueStatuses[i], bodyId, colliderId);
            for (int i = 0; i < invalidFalseStatuses.Length; i++)
                AssertInvalidObbRoute(false, invalidFalseStatuses[i], bodyId, colliderId);
        }

        private static CombatMotionStepResult Step(
            CombatPhysicsWorld world,
            FixVector3 start,
            FixVector3 direction,
            Fix64 moveSpeed,
            CombatKinematicMotorCandidateCache cache = null,
            Fix64 maxStepHeight = default,
            Fix64 initialVerticalVelocity = default,
            int maxCollisionIterations = 3,
            int? collisionLayerMask = null)
        {
            var motor = new CombatKinematicMotor(CreateConfig(
                moveSpeed,
                maxStepHeight,
                maxCollisionIterations,
                collisionLayerMask));
            var state = new CombatMotionState(
                new CombatFrame(0),
                start,
                new FixVector3(Fix64.Zero, initialVerticalVelocity, Fix64.Zero),
                false,
                FixVector3.Zero,
                CombatMotionCollisionFlags.None);
            return motor.Step(
                state,
                new CombatMotionInput(direction, false, Fix64.One),
                world,
                SourceEntityId,
                SourceBodyId,
                syncBodyPosition: false,
                impactVelocityOverride: null,
                buffer: new CombatMotionStepBuffer(3),
                candidateCache: cache);
        }

        private static CombatMotionConfig CreateConfig(
            Fix64 moveSpeed,
            Fix64 maxStepHeight,
            int maxCollisionIterations,
            int? collisionLayerMask)
        {
            return new CombatMotionConfig(
                ticksPerSecond: 1,
                moveSpeed,
                gravityPerSecond: Fix64.Zero,
                jumpSpeed: Fix64.Zero,
                maxFallSpeed: -Fix64.FromInt(100),
                skinWidth: Fix64.FromRatio(1, 100),
                groundMinNormalY: Fix64.Half,
                ceilingMinNormalY: Fix64.Half,
                maxCollisionIterations,
                CharacterHalfExtents,
                new CombatPhysicsLayerMask(collisionLayerMask ?? BlockingMask.Value),
                maxStepHeight);
        }

        private static CombatPhysicsWorld CreateWorld(FixVector3 sourcePosition)
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(SourceEntityId, SourceBodyId, sourcePosition));
            return world;
        }

        private static void RegisterFaceObb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            Fix64 faceX,
            CombatSurfaceMaterial material = default)
        {
            RegisterObb(world, entity, body, collider: 1, layer: 1,
                center: new FixVector3(faceX + Fix64.One, Fix64.Zero, Fix64.Zero),
                halfExtents: new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                UnitX, UnitY, UnitZ, material);
        }

        private static void RegisterZFaceObb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            Fix64 faceZ,
            CombatSurfaceMaterial material = default)
        {
            RegisterObb(world, entity, body, collider: 1, layer: 1,
                center: new FixVector3(Fix64.Zero, Fix64.Zero, faceZ + Fix64.One),
                halfExtents: new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                UnitX, UnitY, UnitZ, material);
        }

        private static void RegisterObb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            FixVector3 center,
            FixVector3 halfExtents,
            FixVector3 axisX,
            FixVector3 axisY,
            FixVector3 axisZ,
            CombatSurfaceMaterial material)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, center));
            world.UpsertObbCollider(new CombatPhysicsObbCollider(
                bodyId,
                new CombatColliderId(collider),
                layer,
                FixVector3.Zero,
                halfExtents,
                axisX,
                axisY,
                axisZ,
                material));
        }

        private static void RegisterFaceAabb(CombatPhysicsWorld world, int entity, int body, Fix64 faceX)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                layer: 1,
                localMin: new FixVector3(faceX, -Fix64.One, -Fix64.One),
                localMax: new FixVector3(faceX + Fix64.FromInt(2), Fix64.One, Fix64.One)));
        }

        private static void RegisterSphere(
            CombatPhysicsWorld world,
            int entity,
            int body,
            FixVector3 center,
            int layer = 1)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, center));
            world.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                bodyId,
                new CombatColliderId(1),
                layer,
                FixVector3.Zero,
                Fix64.Half));
        }

        private static void RegisterCapsule(CombatPhysicsWorld world, int entity, int body, FixVector3 center)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, center));
            world.UpsertCapsuleCollider(new CombatPhysicsCapsuleCollider(
                bodyId,
                new CombatColliderId(1),
                1,
                -UnitY * Fix64.Half,
                UnitY * Fix64.Half,
                Fix64.Half));
        }

        private static CombatMotionCollision FindCollision(CombatMotionStepResult result, CombatBodyId bodyId)
        {
            for (int i = 0; i < result.CollisionCount; i++)
            {
                if (result.Collisions[i].TargetBodyId.Equals(bodyId))
                    return result.Collisions[i];
            }

            Assert.Fail("Expected collision with body " + bodyId.Value + ".");
            return default;
        }

        private static void AssertVectorNear(FixVector3 expected, FixVector3 actual, long toleranceRaw)
        {
            Assert.LessOrEqual(Math.Abs(expected.X.RawValue - actual.X.RawValue), toleranceRaw, "X");
            Assert.LessOrEqual(Math.Abs(expected.Y.RawValue - actual.Y.RawValue), toleranceRaw, "Y");
            Assert.LessOrEqual(Math.Abs(expected.Z.RawValue - actual.Z.RawValue), toleranceRaw, "Z");
        }

        private static void AssertInvalidObbRoute(
            bool castResult,
            CombatObbCastStatus status,
            CombatBodyId bodyId,
            CombatColliderId colliderId)
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                CombatKinematicMotor.RouteObbCastResult(castResult, status, bodyId, colliderId));
            StringAssert.Contains(bodyId.Value.ToString(), exception.Message);
            StringAssert.Contains(colliderId.Value.ToString(), exception.Message);
            StringAssert.Contains(castResult.ToString(), exception.Message);
            StringAssert.Contains(status.ToString(), exception.Message);
        }

        private static bool HasFlag(CombatMotionCollisionFlags value, CombatMotionCollisionFlags flag)
        {
            return (value & flag) == flag;
        }
    }
}
