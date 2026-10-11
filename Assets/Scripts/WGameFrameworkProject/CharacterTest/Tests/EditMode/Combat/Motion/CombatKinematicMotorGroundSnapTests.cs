using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Motion
{
    public sealed class CombatKinematicMotorGroundSnapTests
    {
        private static readonly CombatBodyId CharacterBody = new CombatBodyId(1);
        private static readonly CombatPhysicsLayerMask BlockingMask = CombatPhysicsLayerMask.FromLayer(1);
        private static readonly FixVector3 HalfExtents = new FixVector3(Fix64.Half, Fix64.One, Fix64.Half);

        [Test]
        public void SafeDistanceHelper_IsInclusiveAndRejectsOneRawBeyond()
        {
            Assert.IsTrue(CombatKinematicMotor.TryCalculateGroundSnapSafeDistance(
                Fix64.FromRaw(100000), Fix64.FromRaw(100000), Fix64.FromRaw(10000), out Fix64 safe));
            Assert.AreEqual(Fix64.FromRaw(90000), safe);
            Assert.IsFalse(CombatKinematicMotor.TryCalculateGroundSnapSafeDistance(
                Fix64.FromRaw(100001), Fix64.FromRaw(100000), Fix64.FromRaw(10000), out _));
        }

        [Test]
        public void RealWorld_ExactSnapDistanceHitsAndAppliesSkinOnce()
        {
            GroundCase item = CreateFlatCase(Fix64.FromRaw(100000), CombatSurfaceMaterial.Rough);

            CombatMotionStepResult result = Step(item);

            Assert.AreEqual(Fix64.FromRaw(-90000), result.AppliedDelta.Y);
            Assert.AreEqual(522, (int)result.CollisionFlags);
            Assert.AreEqual(Fix64.FromRaw(100000), result.LastCollision.Distance);
            Assert.AreEqual(new CombatEntityId(2), result.LastCollision.TargetEntityId);
            Assert.AreEqual(new CombatBodyId(2), result.LastCollision.TargetBodyId);
            Assert.AreEqual(new CombatColliderId(7), result.LastCollision.TargetColliderId);
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, result.SurfaceMaterialKind);
            Assert.AreEqual(R(0, 1000000, 0), result.State.LastCollisionNormal);
            Assert.AreEqual(Fix64.Zero, result.State.Velocity.Y);
        }

        [Test]
        public void RealWorld_OneRawBeyondSnapDistanceMissesWithoutForgedResult()
        {
            GroundCase item = CreateFlatCase(Fix64.FromRaw(100001), CombatSurfaceMaterial.Rough);

            CombatMotionStepResult result = Step(item);

            Assert.AreEqual(FixVector3.Zero, result.AppliedDelta);
            Assert.AreEqual(0, result.CollisionCount);
            Assert.AreEqual(CombatMotionCollisionFlags.None, result.CollisionFlags);
            Assert.AreEqual(CombatSurfaceMaterialKind.Default, result.SurfaceMaterialKind);
            Assert.AreEqual(FixVector3.Zero, result.State.LastCollisionNormal);
        }

        [Test]
        public void Eligibility_OptionsJumpPositiveDesiredAndImpactAllRejectSnap()
        {
            GroundCase options = CreateFlatCase(Fix64.FromRaw(50000), CombatSurfaceMaterial.Rough);
            GroundCase jump = CreateFlatCase(Fix64.FromRaw(50000), CombatSurfaceMaterial.Rough);
            GroundCase upward = CreateFlatCase(
                Fix64.FromRaw(50000), CombatSurfaceMaterial.Rough, Fix64.FromRaw(100000));
            GroundCase impact = CreateFlatCase(Fix64.FromRaw(50000), CombatSurfaceMaterial.Rough);

            CombatMotionStepResult optionsResult = Step(options, CombatMotionInput.None, null,
                new CombatMotionStepOptions(suppressGroundSnap: true));
            CombatMotionStepResult jumpResult = Step(jump,
                new CombatMotionInput(FixVector3.Zero, jumpPressed: true), null, default);
            CombatMotionStepResult upwardResult = Step(upward);
            CombatMotionStepResult impactResult = Step(impact, CombatMotionInput.None,
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero), default);

            AssertNoGroundSnap(optionsResult);
            AssertNoGroundSnap(jumpResult);
            AssertNoGroundSnap(upwardResult);
            AssertNoGroundSnap(impactResult);
        }

        [Test]
        public void MainMovementGroundedContactDoesNotAddGroundSnap()
        {
            GroundCase item = CreateFlatCase(Fix64.Zero, CombatSurfaceMaterial.Rough);

            CombatMotionStepResult result = Step(item,
                new CombatMotionInput(R(1000000, 0, 0), false), null, default);

            Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.Grounded));
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
            Assert.IsTrue(result.AppliedDelta.X > Fix64.Zero);
        }

        [Test]
        public void EdgeWithoutSupportDoesNotSnap()
        {
            GroundCase item = CreateEmptyCase();

            CombatMotionStepResult result = Step(item);

            AssertNoGroundSnap(result);
            Assert.AreEqual(0, result.CollisionCount);
        }

        [Test]
        public void EarlierSteepContactRejectsFartherWalkableSupport()
        {
            GroundCase item = CreateSteepBeforeFlatCase();

            CombatMotionStepResult result = Step(item);

            AssertNoGroundSnap(result);
            Assert.AreEqual(0, result.CollisionCount);
            Assert.AreEqual(FixVector3.Zero, result.AppliedDelta);
        }

        [Test]
        public void NonWalkableSteepWallAndCeilingNormalsAreRejectedBySupportGate()
        {
            CombatKinematicMotor motor = CreateMotor(
                Fix64.FromRaw(100000), Fix64.FromRaw(10000), Fix64.FromInt(-30));
            FixVector3[] normals =
            {
                R(766044, 642788, 0),
                R(1000000, 0, 0),
                R(0, -1000000, 0)
            };
            for (int i = 0; i < normals.Length; i++)
            {
                CombatMotionCollisionFlags flags = motor.ClassifyCollision(normals[i]);
                Assert.IsFalse(HasFlag(flags, CombatMotionCollisionFlags.Grounded), "normal " + i);
            }
        }

        [Test]
        public void SameFractionSupportUsesStableIdentityRegardlessOfInsertionOrder()
        {
            GroundCase first = CreateSameFractionGroundCase(reverse: false);
            GroundCase reversed = CreateSameFractionGroundCase(reverse: true);

            CombatMotionStepResult a = Step(first);
            CombatMotionStepResult b = Step(reversed);

            Assert.AreEqual(a, b);
            Assert.AreEqual(new CombatEntityId(2), a.LastCollision.TargetEntityId);
            Assert.AreEqual(new CombatBodyId(2), a.LastCollision.TargetBodyId);
            Assert.AreEqual(new CombatColliderId(7), a.LastCollision.TargetColliderId);
        }

        [TestCase(258819, 965926)]
        [TestCase(500000, 866025)]
        public void RotatedObb_DownhillContinuouslyUsesPureYGroundSnap(int sinRaw, int cosRaw)
        {
            GroundCase item = CreateSlopeCase(sinRaw, cosRaw);
            Fix64 initialY = item.State.Position.Y;
            Fix64 previousY = item.State.Position.Y;
            bool decreased = false;
            for (int i = 0; i < 8; i++)
            {
                CombatMotionStepResult result = Step(item,
                    new CombatMotionInput(R(-1000000, 0, 0), false), null, default);
                Assert.IsTrue(result.State.Position.Y <= previousY, "non-increasing downhill Y at " + i);
                decreased |= result.State.Position.Y < previousY;
                Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
                Assert.IsTrue(result.State.Grounded);
                Assert.AreEqual(Fix64.Zero, result.State.Velocity.Y);
                Assert.AreEqual(Fix64.Zero, result.AppliedDelta.Z);
                previousY = result.State.Position.Y;
                item.State = result.State;
            }

            Assert.IsTrue(decreased, "Ground Snap must eventually move down after the accumulated gap exceeds skin.");
            Assert.IsTrue(item.State.Position.Y < initialY);
        }

        [Test]
        public void FixedBounceSupportClearsGroundedAndGroundSnap()
        {
            GroundCase item = CreateFlatCase(Fix64.FromRaw(50000), CombatSurfaceMaterial.BouncePad);

            CombatMotionStepResult result = Step(item);

            Assert.IsTrue(result.State.Velocity.Y > Fix64.Zero);
            Assert.IsFalse(result.State.Grounded);
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.Grounded));
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
            Assert.AreEqual(R(0, 1000000, 0), result.State.LastCollisionNormal);
        }

        [Test]
        public void ElasticSupportClearsGroundedAndGroundSnap()
        {
            GroundCase item = CreateFlatCase(Fix64.FromRaw(50000), CombatSurfaceMaterial.Elastic);

            CombatMotionStepResult result = Step(item, CombatMotionInput.None,
                new FixVector3(Fix64.Zero, Fix64.FromInt(-2), Fix64.Zero), default);

            Assert.IsTrue(result.State.Velocity.Y > Fix64.Zero);
            Assert.IsFalse(result.State.Grounded);
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
        }

        [Test]
        public void CandidateCache_ColdWarmAndRebuildMatchUncachedResult()
        {
            GroundCase expected = CreateFlatCase(Fix64.FromRaw(50000), CombatSurfaceMaterial.Rough);
            GroundCase cached = CreateFlatCase(Fix64.FromRaw(50000), CombatSurfaceMaterial.Rough);
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(cached.World, BlockingMask, CombatPhysicsLayerMask.None);

            CombatMotionStepResult uncached = Step(expected);
            CombatMotionStepResult cold = Step(cached, CombatMotionInput.None, null, default, cache);
            ResetCase(cached);
            CombatMotionStepResult warm = Step(cached, CombatMotionInput.None, null, default, cache);
            cached.World.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(99), new CombatBodyId(99), R(100000000, 0, 0)));
            cache.Prepare(cached.World, BlockingMask, CombatPhysicsLayerMask.None);
            ResetCase(cached);
            CombatMotionStepResult rebuilt = Step(cached, CombatMotionInput.None, null, default, cache);

            Assert.AreEqual(uncached, cold);
            Assert.AreEqual(uncached, warm);
            Assert.AreEqual(uncached, rebuilt);
            Assert.Greater(cached.Motor.LastQueryStats.CandidateCacheHitCount, 0);
        }

        [Test]
        public void WarmedCachedSupportHitIsNoAlloc()
        {
            GroundCase item = CreateFlatCase(Fix64.FromRaw(50000), CombatSurfaceMaterial.Rough);
            var cache = new CombatKinematicMotorCandidateCache();
            var buffer = new CombatMotionStepBuffer();
            cache.Prepare(item.World, BlockingMask, CombatPhysicsLayerMask.None);
            for (int i = 0; i < 16; i++)
            {
                ResetCase(item);
                _ = Step(item, CombatMotionInput.None, null, default, cache, buffer);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                ResetCase(item);
                _ = Step(item, CombatMotionInput.None, null, default, cache, buffer);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
        }

        private static GroundCase CreateFlatCase(
            Fix64 gap,
            CombatSurfaceMaterial material,
            Fix64 verticalVelocity = default)
        {
            var item = CreateEmptyCase(verticalVelocity);
            var groundBody = new CombatBodyId(2);
            item.World.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), groundBody, FixVector3.Zero));
            item.World.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                groundBody, new CombatColliderId(7), 1,
                R(-10000000, -1000000, -10000000), R(10000000, 0, 10000000), material));
            FixVector3 start = new FixVector3(Fix64.Zero, Fix64.One + gap, Fix64.Zero);
            item.InitialPosition = start;
            item.State = State(start, verticalVelocity);
            item.World.SetBodyPosition(CharacterBody, start);
            return item;
        }

        private static GroundCase CreateEmptyCase(Fix64 verticalVelocity = default)
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(1), CharacterBody, FixVector3.Zero));
            return new GroundCase
            {
                World = world,
                Motor = CreateMotor(Fix64.FromRaw(100000), Fix64.FromRaw(10000), Fix64.FromInt(-30)),
                State = State(FixVector3.Zero, verticalVelocity),
                InitialPosition = FixVector3.Zero,
                InitialVelocity = verticalVelocity
            };
        }

        private static GroundCase CreateSlopeCase(long sinRaw, long cosRaw)
        {
            FixVector3 tangent = R(cosRaw, sinRaw, 0);
            FixVector3 normal = R(-sinRaw, cosRaw, 0);
            var slopeBody = new CombatBodyId(2);
            var collider = new CombatPhysicsObbCollider(
                slopeBody, new CombatColliderId(7), 1, FixVector3.Zero,
                new FixVector3(Fix64.FromInt(10), Fix64.Half, Fix64.FromInt(5)),
                tangent, normal, R(0, 0, 1000000), CombatSurfaceMaterial.Rough);
            tangent = collider.AxisX;
            normal = collider.AxisY;
            Fix64 x = Fix64.FromInt(2);
            Fix64 planeDistance = Fix64.One + Fix64.Half * normal.Y;
            Fix64 y = (planeDistance - normal.X * x) / normal.Y;
            FixVector3 start = new FixVector3(x, y, Fix64.Zero);
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(1), CharacterBody, start));
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), slopeBody, FixVector3.Zero));
            world.UpsertObbCollider(collider);
            return new GroundCase
            {
                World = world,
                Motor = CreateMotor(Fix64.FromRaw(100000), Fix64.FromRaw(10000), Fix64.Zero),
                State = State(start, Fix64.Zero),
                InitialPosition = start,
                InitialVelocity = Fix64.Zero
            };
        }

        private static GroundCase CreateSteepBeforeFlatCase()
        {
            const long sinRaw = 766044;
            const long cosRaw = 642788;
            FixVector3 tangent = R(cosRaw, sinRaw, 0);
            FixVector3 normal = R(-sinRaw, cosRaw, 0);
            var steepBody = new CombatBodyId(2);
            var steep = new CombatPhysicsObbCollider(
                steepBody, new CombatColliderId(7), 1, FixVector3.Zero,
                new FixVector3(Fix64.FromInt(5), Fix64.Half, Fix64.FromInt(5)),
                tangent, normal, R(0, 0, 1000000), CombatSurfaceMaterial.Rough);
            normal = steep.AxisY;
            Fix64 verticalGap = Fix64.FromRaw(50000);
            Fix64 planeDistance = Fix64.One + Fix64.Half * normal.Y;
            Fix64 startY = (planeDistance / normal.Y) + verticalGap;
            FixVector3 start = new FixVector3(Fix64.Zero, startY, Fix64.Zero);
            Fix64 flatTop = startY - Fix64.One - Fix64.FromRaw(80000);
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(1), CharacterBody, start));
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), steepBody, FixVector3.Zero));
            world.UpsertObbCollider(steep);
            var flatBody = new CombatBodyId(3);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(3), flatBody, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                flatBody, new CombatColliderId(8), 1,
                new FixVector3(-Fix64.FromInt(10), flatTop - Fix64.One, -Fix64.FromInt(10)),
                new FixVector3(Fix64.FromInt(10), flatTop, Fix64.FromInt(10))));
            return new GroundCase
            {
                World = world,
                Motor = CreateMotor(Fix64.FromRaw(100000), Fix64.FromRaw(10000), Fix64.FromInt(-30)),
                State = State(start, Fix64.Zero),
                InitialPosition = start,
                InitialVelocity = Fix64.Zero
            };
        }

        private static GroundCase CreateSameFractionGroundCase(bool reverse)
        {
            GroundCase item = CreateEmptyCase();
            void AddGround(int id, int colliderId)
            {
                var bodyId = new CombatBodyId(id);
                item.World.UpsertBody(new CombatPhysicsBody(new CombatEntityId(id), bodyId, FixVector3.Zero));
                item.World.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                    bodyId, new CombatColliderId(colliderId), 1,
                    R(-10000000, -1000000, -10000000), R(10000000, 0, 10000000),
                    CombatSurfaceMaterial.Rough));
            }

            if (reverse)
            {
                AddGround(3, 8);
                AddGround(2, 7);
            }
            else
            {
                AddGround(2, 7);
                AddGround(3, 8);
            }

            FixVector3 start = new FixVector3(Fix64.Zero, Fix64.One + Fix64.FromRaw(50000), Fix64.Zero);
            item.InitialPosition = start;
            item.State = State(start, Fix64.Zero);
            item.World.SetBodyPosition(CharacterBody, start);
            return item;
        }

        private static CombatKinematicMotor CreateMotor(Fix64 snap, Fix64 skin, Fix64 gravity)
        {
            return new CombatKinematicMotor(new CombatMotionConfig(
                60, Fix64.One, gravity, Fix64.FromInt(10), Fix64.FromInt(-50), skin,
                Fix64.FromRaw(707107), Fix64.Half, 3, HalfExtents, BlockingMask,
                Fix64.Zero, Fix64.One, gravity.Abs(), snap));
        }

        private static CombatMotionState State(FixVector3 position, Fix64 verticalVelocity)
        {
            return new CombatMotionState(
                CombatFrame.Zero, position, new FixVector3(Fix64.Zero, verticalVelocity, Fix64.Zero),
                grounded: true, R(0, 1000000, 0), CombatMotionCollisionFlags.Grounded);
        }

        private static CombatMotionStepResult Step(
            GroundCase item,
            CombatMotionInput input = default,
            FixVector3? impact = null,
            CombatMotionStepOptions options = default,
            CombatKinematicMotorCandidateCache cache = null,
            CombatMotionStepBuffer buffer = null)
        {
            return item.Motor.Step(item.World, CharacterBody, item.State,
                input.Equals(default(CombatMotionInput)) ? CombatMotionInput.None : input,
                impact, buffer, cache, options);
        }

        private static void ResetCase(GroundCase item)
        {
            item.State = State(item.InitialPosition, item.InitialVelocity);
            item.World.SetBodyPosition(CharacterBody, item.InitialPosition);
        }

        private static void AssertNoGroundSnap(CombatMotionStepResult result)
        {
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
        }

        private static bool HasFlag(CombatMotionCollisionFlags value, CombatMotionCollisionFlags flag) =>
            (value & flag) == flag;

        private static FixVector3 R(long x, long y, long z) =>
            new FixVector3(Fix64.FromRaw(x), Fix64.FromRaw(y), Fix64.FromRaw(z));

        private sealed class GroundCase
        {
            public CombatPhysicsWorld World;
            public CombatKinematicMotor Motor;
            public CombatMotionState State;
            public FixVector3 InitialPosition;
            public Fix64 InitialVelocity;
        }
    }
}
