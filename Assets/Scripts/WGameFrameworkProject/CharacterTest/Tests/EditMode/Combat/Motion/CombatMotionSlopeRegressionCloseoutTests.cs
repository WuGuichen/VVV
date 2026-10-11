using System;
using System.Collections.Generic;
using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Motion
{
    public sealed class CombatMotionSlopeRegressionCloseoutTests
    {
        private static readonly CombatBodyId CharacterBody = new CombatBodyId(1);
        private static readonly CombatEntityId CharacterEntity = new CombatEntityId(1);
        private static readonly CombatPhysicsLayerMask BlockingMask = CombatPhysicsLayerMask.FromLayer(1);
        private static readonly FixVector3 HalfExtents = new FixVector3(Fix64.Half, Fix64.One, Fix64.Half);

        [Test]
        public void FullSlopeSequence_RepeatedRunsProduceIdenticalCompleteResults()
        {
            List<CombatMotionStepResult> first = RunDeterministicSequence(reverseFarCandidateOrder: false);
            List<CombatMotionStepResult> second = RunDeterministicSequence(reverseFarCandidateOrder: false);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
                Assert.AreEqual(first[i], second[i], "complete step result at " + i);
        }

        [Test]
        public void FullSlopeSequence_UnrelatedFarTopologyInsertionOrderDoesNotChangeResults()
        {
            List<CombatMotionStepResult> ordered = RunDeterministicSequence(reverseFarCandidateOrder: false);
            List<CombatMotionStepResult> reversed = RunDeterministicSequence(reverseFarCandidateOrder: true);

            Assert.AreEqual(ordered.Count, reversed.Count);
            for (int i = 0; i < ordered.Count; i++)
                Assert.AreEqual(ordered[i], reversed[i], "candidate permutation result at " + i);
        }

        [Test]
        public void WarmedMotorSlopeSupportAndNearObstacleMatrixIsNoAlloc()
        {
            MotorCase uphill = CreateSlopeCase(500000, 866025, uphill: true, snap: Fix64.Zero);
            MotorCase downhill = CreateSlopeCase(500000, 866025, uphill: false, snap: Fix64.FromRaw(100000));
            MotorCase traverse = CreateSlopeCase(500000, 866025, uphill: true, snap: Fix64.Zero, traverse: true);
            MotorCase supportHit = CreateFlatSupportCase(withGround: true);
            MotorCase supportMiss = CreateFlatSupportCase(withGround: false);
            MotorCase nearSteep = CreateNearSteepCase();
            MotorCase[] cases = { uphill, downhill, traverse, supportHit, supportMiss, nearSteep };

            for (int i = 0; i < cases.Length; i++)
            {
                cases[i].Cache.Prepare(cases[i].World, BlockingMask, CombatPhysicsLayerMask.None);
                for (int warm = 0; warm < 16; warm++)
                    RunResetStep(cases[i]);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int collisionCount = 0;
            for (int iteration = 0; iteration < 64; iteration++)
            {
                for (int i = 0; i < cases.Length; i++)
                    collisionCount += RunResetStep(cases[i]);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.Greater(collisionCount, 0);
            Assert.Greater(supportHit.Motor.LastQueryStats.CandidateCacheHitCount, 0);
            Assert.Greater(nearSteep.Motor.LastQueryStats.CandidateCacheHitCount, 0);

            CombatMotionStepResult uphillResult = RunResetStepResult(uphill);
            CombatMotionStepResult downhillResult = RunResetStepResult(downhill);
            CombatMotionStepResult traverseResult = RunResetStepResult(traverse);
            CombatMotionStepResult supportHitResult = RunResetStepResult(supportHit);
            CombatMotionStepResult supportMissResult = RunResetStepResult(supportMiss);
            CombatMotionStepResult nearSteepResult = RunResetStepResult(nearSteep);
            Assert.IsTrue(uphillResult.AppliedDelta.Y > Fix64.Zero);
            Assert.IsTrue(HasFlag(uphillResult.CollisionFlags, CombatMotionCollisionFlags.Grounded));
            Assert.IsTrue(HasFlag(downhillResult.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
            Assert.AreEqual(Fix64.Zero, traverseResult.AppliedDelta.Y);
            Assert.IsTrue(HasFlag(supportHitResult.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
            Assert.IsFalse(HasFlag(supportMissResult.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
            Assert.IsTrue(nearSteepResult.AppliedDelta.Y <= Fix64.Zero);
            Assert.IsTrue(HasFlag(nearSteepResult.CollisionFlags, CombatMotionCollisionFlags.SteepSlope));
        }

        [Test]
        public void WarmedCharacterControlSnapAdapterRepeatedSnapHotPathIsNoAlloc()
        {
            CombatPhysicsWorld world = CreateFlatWorld(Fix64.FromRaw(50000));
            CombatKinematicMotor motor = CreateMotor(
                gravity: Fix64.FromInt(-30),
                snap: Fix64.FromRaw(100000));
            var cache = new CombatKinematicMotorCandidateCache();
            var buffer = new CombatMotionStepBuffer();
            cache.Prepare(world, BlockingMask, CombatPhysicsLayerMask.None);
            CharacterKinematicPhysicsStepInput input = CreateAdapterInput(yMillimeters: 1050);
            for (int i = 0; i < 16; i++)
            {
                world.SetBodyPosition(CharacterBody, R(0, 1050000, 0));
                _ = CharacterKinematicPhysicsAdapter.Default.Step(motor, world, buffer, in input, cache);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int groundedCount = 0;
            for (int i = 0; i < 128; i++)
            {
                world.SetBodyPosition(CharacterBody, R(0, 1050000, 0));
                CharacterKinematicPhysicsStepResult result =
                    CharacterKinematicPhysicsAdapter.Default.Step(motor, world, buffer, in input, cache);
                if (result.Grounded)
                    groundedCount++;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(128, groundedCount);
        }

        private static List<CombatMotionStepResult> RunDeterministicSequence(bool reverseFarCandidateOrder)
        {
            MotorCase item = CreateSlopeCase(
                500000,
                866025,
                uphill: false,
                snap: Fix64.FromRaw(100000));
            AddFarCandidates(item.World, reverseFarCandidateOrder);
            item.Cache.Prepare(item.World, BlockingMask, CombatPhysicsLayerMask.None);
            var results = new List<CombatMotionStepResult>(96);
            CombatMotionState state = item.InitialState;
            for (int i = 0; i < 32; i++)
            {
                CombatMotionStepResult result = item.Motor.Step(
                    item.World,
                    CharacterBody,
                    state,
                    i < 16
                        ? new CombatMotionInput(R(-1000000, 0, 0), false)
                        : new CombatMotionInput(R(0, 0, 1000000), false),
                    null,
                    null,
                    item.Cache,
                    default);
                results.Add(result);
                state = result.State;
            }

            return results;
        }

        private static int RunResetStep(MotorCase item)
        {
            return RunResetStepResult(item).CollisionCount;
        }

        private static CombatMotionStepResult RunResetStepResult(MotorCase item)
        {
            item.World.SetBodyPosition(CharacterBody, item.InitialState.Position);
            return item.Motor.Step(
                item.World,
                CharacterBody,
                item.InitialState,
                item.Input,
                null,
                item.Buffer,
                item.Cache,
                default);
        }

        private static MotorCase CreateSlopeCase(
            long sinRaw,
            long cosRaw,
            bool uphill,
            Fix64 snap,
            bool traverse = false)
        {
            FixVector3 tangent = R(cosRaw, sinRaw, 0);
            FixVector3 normal = R(-sinRaw, cosRaw, 0);
            var slopeBody = new CombatBodyId(2);
            var slope = new CombatPhysicsObbCollider(
                slopeBody,
                new CombatColliderId(2),
                1,
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(10), Fix64.Half, Fix64.FromInt(5)),
                tangent,
                normal,
                R(0, 0, 1000000),
                CombatSurfaceMaterial.Rough);
            tangent = slope.AxisX;
            normal = slope.AxisY;
            Fix64 x = uphill ? Fix64.FromInt(-2) : Fix64.FromInt(2);
            Fix64 planeDistance = Fix64.One + Fix64.Half * normal.Y;
            Fix64 y = (planeDistance - normal.X * x) / normal.Y;
            FixVector3 start = new FixVector3(x, y, Fix64.Zero);
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(CharacterEntity, CharacterBody, start));
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), slopeBody, FixVector3.Zero));
            world.UpsertObbCollider(slope);
            FixVector3 direction = traverse
                ? R(0, 0, 1000000)
                : uphill ? R(1000000, 0, 0) : R(-1000000, 0, 0);
            return new MotorCase(
                world,
                CreateMotor(Fix64.Zero, snap),
                GroundedState(start, normal),
                new CombatMotionInput(direction, false));
        }

        private static MotorCase CreateFlatSupportCase(bool withGround)
        {
            FixVector3 start = R(0, 1050000, 0);
            CombatPhysicsWorld world = withGround
                ? CreateFlatWorld(Fix64.FromRaw(50000))
                : new CombatPhysicsWorld();
            if (!withGround)
                world.UpsertBody(new CombatPhysicsBody(CharacterEntity, CharacterBody, start));
            return new MotorCase(
                world,
                CreateMotor(Fix64.FromInt(-30), Fix64.FromRaw(100000)),
                GroundedState(start, R(0, 1000000, 0)),
                CombatMotionInput.None);
        }

        private static MotorCase CreateNearSteepCase()
        {
            const long sinRaw = 766044;
            const long cosRaw = 642788;
            FixVector3 tangent = R(cosRaw, sinRaw, 0);
            FixVector3 normal = R(-sinRaw, cosRaw, 0);
            var steepBody = new CombatBodyId(2);
            var steep = new CombatPhysicsObbCollider(
                steepBody, new CombatColliderId(2), 1, FixVector3.Zero,
                new FixVector3(Fix64.FromInt(5), Fix64.Half, Fix64.FromInt(5)),
                tangent, normal, R(0, 0, 1000000), CombatSurfaceMaterial.Rough);
            normal = steep.AxisY;
            Fix64 startY = (Fix64.One + Fix64.Half * normal.Y) / normal.Y;
            FixVector3 start = new FixVector3(Fix64.Zero, startY, Fix64.Zero);
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(CharacterEntity, CharacterBody, start));
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), steepBody, FixVector3.Zero));
            world.UpsertObbCollider(steep);
            return new MotorCase(
                world,
                CreateMotor(Fix64.Zero, Fix64.FromRaw(100000)),
                GroundedState(start, R(0, 1000000, 0)),
                new CombatMotionInput(R(1000000, 0, 0), false));
        }

        private static CombatPhysicsWorld CreateFlatWorld(Fix64 gap)
        {
            FixVector3 start = new FixVector3(Fix64.Zero, Fix64.One + gap, Fix64.Zero);
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(CharacterEntity, CharacterBody, start));
            var groundBody = new CombatBodyId(2);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), groundBody, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                groundBody, new CombatColliderId(2), 1,
                R(-10000000, -1000000, -10000000), R(10000000, 0, 10000000),
                CombatSurfaceMaterial.Rough));
            return world;
        }

        private static void AddFarCandidates(CombatPhysicsWorld world, bool reverse)
        {
            if (reverse)
            {
                AddFar(world, 11, Fix64.FromInt(80));
                AddFar(world, 10, Fix64.FromInt(60));
            }
            else
            {
                AddFar(world, 10, Fix64.FromInt(60));
                AddFar(world, 11, Fix64.FromInt(80));
            }
        }

        private static void AddFar(CombatPhysicsWorld world, int id, Fix64 x)
        {
            var body = new CombatBodyId(id);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(id), body, new FixVector3(x, Fix64.Zero, Fix64.Zero)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                body, new CombatColliderId(1), 1,
                R(-1000000, -1000000, -1000000), R(1000000, 1000000, 1000000)));
        }

        private static CombatKinematicMotor CreateMotor(Fix64 gravity, Fix64 snap)
        {
            return new CombatKinematicMotor(new CombatMotionConfig(
                60,
                Fix64.One,
                gravity,
                Fix64.FromInt(10),
                Fix64.FromInt(-50),
                Fix64.FromRaw(10000),
                Fix64.FromRaw(707107),
                Fix64.Half,
                3,
                HalfExtents,
                BlockingMask,
                Fix64.Zero,
                Fix64.One,
                gravity.Abs(),
                snap));
        }

        private static CombatMotionState GroundedState(FixVector3 position, FixVector3 normal)
        {
            return new CombatMotionState(
                CombatFrame.Zero,
                position,
                FixVector3.Zero,
                true,
                normal,
                CombatMotionCollisionFlags.Grounded);
        }

        private static CharacterKinematicPhysicsStepInput CreateAdapterInput(int yMillimeters)
        {
            return new CharacterKinematicPhysicsStepInput(
                CharacterEntity,
                CharacterBody,
                CombatFrame.Zero,
                0,
                yMillimeters,
                0,
                0,
                yMillimeters,
                0,
                Fix64.Zero,
                0,
                0,
                0,
                0,
                true,
                true,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                -500,
                1,
                1000);
        }

        private static FixVector3 R(long x, long y, long z) =>
            new FixVector3(Fix64.FromRaw(x), Fix64.FromRaw(y), Fix64.FromRaw(z));

        private static bool HasFlag(CombatMotionCollisionFlags value, CombatMotionCollisionFlags flag) =>
            (value & flag) == flag;

        private sealed class MotorCase
        {
            public MotorCase(
                CombatPhysicsWorld world,
                CombatKinematicMotor motor,
                CombatMotionState initialState,
                CombatMotionInput input)
            {
                World = world;
                Motor = motor;
                InitialState = initialState;
                Input = input;
                Cache = new CombatKinematicMotorCandidateCache();
                Buffer = new CombatMotionStepBuffer();
            }

            public CombatPhysicsWorld World { get; }
            public CombatKinematicMotor Motor { get; }
            public CombatMotionState InitialState { get; }
            public CombatMotionInput Input { get; }
            public CombatKinematicMotorCandidateCache Cache { get; }
            public CombatMotionStepBuffer Buffer { get; }
        }
    }
}
