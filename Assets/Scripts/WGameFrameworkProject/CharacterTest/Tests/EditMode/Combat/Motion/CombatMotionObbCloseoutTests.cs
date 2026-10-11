using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Motion
{
    public sealed class CombatMotionObbCloseoutTests
    {
        private static readonly CombatEntityId SourceEntityId = new CombatEntityId(1);
        private static readonly CombatBodyId SourceBodyId = new CombatBodyId(1);
        private static readonly CombatPhysicsLayerMask BlockingMask = CombatPhysicsLayerMask.FromLayer(1);
        private static readonly FixVector3 UnitX = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
        private static readonly FixVector3 UnitY = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
        private static readonly FixVector3 UnitZ = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
        private static readonly FixVector3 CharacterHalfExtents = new FixVector3(Fix64.Half, Fix64.One, Fix64.Half);

        [TestCase(965926L, 258819L)]
        [TestCase(866025L, 500000L)]
        [TestCase(707107L, 707107L)]
        [TestCase(642788L, 766044L)]
        public void RotatedTopFaceMatrix_FifteenThirtyFortyFiveAndFiftyDegreesIsConservative(
            long cosineRaw,
            long sineRaw)
        {
            FixVector3 axisX = Raw(cosineRaw, sineRaw, 0);
            FixVector3 axisY = Raw(-sineRaw, cosineRaw, 0);
            CombatPhysicsShape obb = CombatPhysicsShape.Obb(
                FixVector3.Zero,
                Raw(3000000, 500000, 3000000),
                axisX,
                axisY,
                UnitZ);
            CombatMotionCapsuleProxy sphereProxy = new CombatMotionCapsuleProxy(
                Fix64.Half,
                Fix64.One,
                FixVector3.Zero,
                Fix64.Zero,
                0,
                CombatPhysicsLayerMask.All);

            bool hit = CombatMotionObbCast.TryCast(
                sphereProxy,
                axisY * Fix64.FromInt(3),
                -axisY * Fix64.FromInt(4),
                obb,
                out Fix64 fraction,
                out Fix64 distance,
                out FixVector3 normal,
                out CombatObbCastStatus status);

            Assert.That(hit, Is.True);
            Assert.That(status, Is.EqualTo(CombatObbCastStatus.Hit)
                .Or.EqualTo(CombatObbCastStatus.PrecisionLimitConservativeHit));
            Assert.That(fraction.RawValue, Is.LessThanOrEqualTo(500000));
            Assert.That(fraction.RawValue, Is.GreaterThanOrEqualTo(499800));
            Assert.That(distance.RawValue, Is.LessThanOrEqualTo(2000000));
            AssertVectorNear(axisY, normal, 8);
        }

        [Test]
        public void YawPitchTopFace_RemainsConservativeAndReturnsWorldNormal()
        {
            FixVector3 axisX = Raw(707107, 0, -707107);
            FixVector3 axisY = Raw(353553, 866025, 353553);
            FixVector3 axisZ = Raw(612372, -500000, 612372);
            CombatPhysicsShape obb = CombatPhysicsShape.Obb(
                FixVector3.Zero,
                Raw(3000000, 500000, 3000000),
                axisX,
                axisY,
                axisZ);
            CombatMotionCapsuleProxy sphereProxy = new CombatMotionCapsuleProxy(
                Fix64.Half,
                Fix64.One,
                FixVector3.Zero,
                Fix64.Zero,
                0,
                CombatPhysicsLayerMask.All);

            bool hit = CombatMotionObbCast.TryCast(
                sphereProxy,
                axisY * Fix64.FromInt(3),
                -axisY * Fix64.FromInt(4),
                obb,
                out Fix64 fraction,
                out _,
                out FixVector3 normal,
                out CombatObbCastStatus status);

            Assert.That(hit, Is.True);
            Assert.That(status, Is.EqualTo(CombatObbCastStatus.Hit)
                .Or.EqualTo(CombatObbCastStatus.PrecisionLimitConservativeHit));
            Assert.That(fraction.RawValue, Is.LessThanOrEqualTo(500000));
            Assert.That(fraction.RawValue, Is.GreaterThanOrEqualTo(499800));
            AssertVectorNear(axisY, normal, 8);
        }

        [Test]
        public void MixedAabbObbLongSequence_IsRawDeterministicAcrossRunsAndPositiveCompetitorInsertionOrder()
        {
            SequenceOracle forwardFirst = RunLongSequence(reverseInsertion: false);
            SequenceOracle forwardSecond = RunLongSequence(reverseInsertion: false);
            SequenceOracle reversed = RunLongSequence(reverseInsertion: true);

            Assert.That(forwardSecond, Is.EqualTo(forwardFirst));
            Assert.That(reversed, Is.EqualTo(forwardFirst));
            Assert.That(forwardFirst.TickCount, Is.EqualTo(256));
            Assert.That(forwardFirst.CollisionCount, Is.GreaterThan(0));
            TestContext.WriteLine(
                "ticks=" + forwardFirst.TickCount
                + ", collisions=" + forwardFirst.CollisionCount
                + ", positionRaw=(" + forwardFirst.State.Position.X.RawValue + ","
                + forwardFirst.State.Position.Y.RawValue + "," + forwardFirst.State.Position.Z.RawValue + ")"
                + ", velocityRaw=(" + forwardFirst.State.Velocity.X.RawValue + ","
                + forwardFirst.State.Velocity.Y.RawValue + "," + forwardFirst.State.Velocity.Z.RawValue + ")"
                + ", flags=" + forwardFirst.State.CollisionFlags
                + ", lastNormalRaw=(" + forwardFirst.State.LastCollisionNormal.X.RawValue + ","
                + forwardFirst.State.LastCollisionNormal.Y.RawValue + ","
                + forwardFirst.State.LastCollisionNormal.Z.RawValue + ")"
                + ", authorityStateHash=" + forwardFirst.Hash);
        }

        [Test]
        public void MotorObbHotPath_HitMissRotatedPrecisionAndMixedCandidatesAllocateZeroBytesAfterWarmup()
        {
            HotPathCase faceHit = CreateFaceHitCase();
            HotPathCase miss = CreateMissCase();
            HotPathCase rotated = CreateRotatedHitCase();
            HotPathCase precision = CreatePrecisionHitCase();
            HotPathCase mixed = CreateMixedCandidateCase();

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 16; i++)
                RunHotPathSet(faceHit, miss, rotated, precision, mixed);

            long before = GC.GetAllocatedBytesForCurrentThread();
            int collisionCount = 0;
            for (int i = 0; i < 128; i++)
                collisionCount += RunHotPathSet(faceHit, miss, rotated, precision, mixed);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            CombatMotionStepResult missResult = RunHotPathResult(miss);

            Assert.That(allocated, Is.Zero);
            Assert.That(collisionCount, Is.GreaterThanOrEqualTo(4 * 128));
            Assert.That(faceHit.Motor.LastQueryStats.CandidateCacheHitCount, Is.GreaterThan(0));
            Assert.That(miss.Motor.LastQueryStats.CandidateCacheHitCount, Is.GreaterThan(0));
            Assert.That(rotated.Motor.LastQueryStats.CandidateCacheHitCount, Is.GreaterThan(0));
            Assert.That(precision.Motor.LastQueryStats.CandidateCacheHitCount, Is.GreaterThan(0));
            Assert.That(mixed.Motor.LastQueryStats.CandidateCacheHitCount, Is.GreaterThan(0));
            Assert.That(missResult.CollisionCount, Is.Zero);
            Assert.That(miss.Motor.LastQueryStats.BroadphaseCandidateCount, Is.GreaterThan(0));
            Assert.That(miss.Motor.LastQueryStats.NarrowphaseCandidateCount, Is.GreaterThan(0));
            TestContext.WriteLine(
                "warmupSets=16, measuredSets=128, stepsPerSet=5, allocatedBytes=" + allocated
                + ", broadphaseCandidates=" + CandidateStats(faceHit, miss, rotated, precision, mixed, broadphase: true)
                + ", narrowphaseCandidates=" + CandidateStats(faceHit, miss, rotated, precision, mixed, broadphase: false));
        }

        [Test]
        public void PrecisionAndInvalidInputStatusesRemainExplicitAtCloseout()
        {
            CombatMotionCapsuleProxy capsule = DefaultCapsule();
            CombatPhysicsShape thin = IdentityObb(Raw(50000, 2000000, 2000000));
            bool precisionHit = CombatMotionObbCast.TryCast(
                capsule,
                Raw(-100000000, 0, 0),
                Raw(200000000, 0, 0),
                thin,
                out _,
                out _,
                out _,
                out CombatObbCastStatus precisionStatus);
            bool invalidHit = CombatMotionObbCast.TryCast(
                capsule,
                Raw(2001000000, 0, 0),
                UnitX,
                IdentityObb(Raw(1000000, 1000000, 1000000)),
                out _,
                out _,
                out _,
                out CombatObbCastStatus invalidStatus);

            Assert.That(precisionHit, Is.True);
            Assert.That(precisionStatus, Is.EqualTo(CombatObbCastStatus.PrecisionLimitConservativeHit));
            Assert.That(invalidHit, Is.False);
            Assert.That(invalidStatus, Is.EqualTo(CombatObbCastStatus.InvalidInput));
            Assert.That(CombatMotionObbCast.MaxCastIterations, Is.EqualTo(40));
            FixVector3 iterationDelta = Raw(-3000000, 0, -3000000);
            bool iterationLimitHit = CombatMotionObbCast.TryCastSeparatedForTesting(
                capsule,
                Raw(3000000, 0, 3000000),
                iterationDelta,
                IdentityObb(Raw(1000000, 1000000, 1000000)),
                maxIterations: 1,
                out Fix64 iterationFraction,
                out Fix64 iterationDistance,
                out FixVector3 iterationNormal,
                out CombatObbCastStatus iterationStatus);

            Assert.That(iterationLimitHit, Is.True);
            Assert.That(iterationStatus, Is.EqualTo(CombatObbCastStatus.IterationLimitConservativeHit));
            Assert.That(iterationFraction, Is.GreaterThan(Fix64.Zero));
            Assert.That(iterationFraction.RawValue, Is.LessThanOrEqualTo(548815));
            AssertVectorNear(Raw(707106, 0, 707106), iterationNormal, 256);
            Assert.That(iterationDelta.Dot(iterationNormal), Is.LessThan(Fix64.Zero));
            Assert.That(iterationDistance,
                Is.EqualTo(iterationDelta.LengthSquared().Sqrt() * iterationFraction));
            Assert.That(CombatKinematicMotor.RouteObbCastResult(
                iterationLimitHit,
                iterationStatus,
                new CombatBodyId(40),
                new CombatColliderId(50)), Is.True,
                "The real fixed-iteration fallback must remain a fail-closed blocking route rather than a miss.");
            Assert.That(CombatKinematicMotor.RouteObbCastResult(
                true,
                CombatObbCastStatus.PrecisionLimitConservativeHit,
                new CombatBodyId(40),
                new CombatColliderId(50)), Is.True,
                "The precision fallback must remain a fail-closed blocking route rather than a miss.");
        }

        private static int RunHotPathSet(
            HotPathCase faceHit,
            HotPathCase miss,
            HotPathCase rotated,
            HotPathCase precision,
            HotPathCase mixed)
        {
            return RunHotPathCase(faceHit)
                + RunHotPathCase(miss)
                + RunHotPathCase(rotated)
                + RunHotPathCase(precision)
                + RunHotPathCase(mixed);
        }

        private static int RunHotPathCase(HotPathCase testCase)
        {
            return RunHotPathResult(testCase).CollisionCount;
        }

        private static CombatMotionStepResult RunHotPathResult(HotPathCase testCase)
        {
            return testCase.Motor.Step(
                testCase.State,
                testCase.Input,
                testCase.World,
                SourceEntityId,
                SourceBodyId,
                syncBodyPosition: false,
                impactVelocityOverride: null,
                buffer: testCase.Buffer,
                candidateCache: testCase.Cache);
        }

        private static string CandidateStats(
            HotPathCase faceHit,
            HotPathCase miss,
            HotPathCase rotated,
            HotPathCase precision,
            HotPathCase mixed,
            bool broadphase)
        {
            return GetCandidateCount(faceHit, broadphase) + "/"
                + GetCandidateCount(miss, broadphase) + "/"
                + GetCandidateCount(rotated, broadphase) + "/"
                + GetCandidateCount(precision, broadphase) + "/"
                + GetCandidateCount(mixed, broadphase);
        }

        private static int GetCandidateCount(HotPathCase testCase, bool broadphase)
        {
            return broadphase
                ? testCase.Motor.LastQueryStats.BroadphaseCandidateCount
                : testCase.Motor.LastQueryStats.NarrowphaseCandidateCount;
        }

        private static HotPathCase CreateFaceHitCase()
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            RegisterFaceObb(world, entity: 10, body: 10, faceX: Fix64.One);
            return CreateHotPathCase(world, FixVector3.Zero, UnitX, Fix64.FromInt(4));
        }

        private static HotPathCase CreateMissCase()
        {
            FixVector3 start = Raw(-3000000, 0, 1400000);
            CombatPhysicsWorld world = CreateWorld(start);
            RegisterObb(world, entity: 11, body: 11, center: FixVector3.Zero,
                halfExtents: Raw(1000000, 1000000, 1000000), UnitX, UnitY, UnitZ);
            return CreateHotPathCase(
                world,
                start,
                Raw(707107, 0, 707107),
                Fix64.FromRaw(2262742));
        }

        private static HotPathCase CreateRotatedHitCase()
        {
            FixVector3 axisX = Raw(866025, 0, -500000);
            FixVector3 axisZ = Raw(500000, 0, 866025);
            FixVector3 start = axisX * Fix64.FromInt(3);
            CombatPhysicsWorld world = CreateWorld(start);
            RegisterObb(world, entity: 12, body: 12, center: FixVector3.Zero,
                halfExtents: Raw(1000000, 1000000, 1000000), axisX, UnitY, axisZ);
            return CreateHotPathCase(world, start, -axisX, Fix64.FromInt(4));
        }

        private static HotPathCase CreatePrecisionHitCase()
        {
            FixVector3 start = Raw(-100000000, 0, 0);
            CombatPhysicsWorld world = CreateWorld(start);
            RegisterObb(world, entity: 13, body: 13, center: FixVector3.Zero,
                halfExtents: Raw(10000, 1000000, 1000000), UnitX, UnitY, UnitZ);
            return CreateHotPathCase(world, start, UnitX, Fix64.FromInt(200));
        }

        private static HotPathCase CreateMixedCandidateCase()
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            RegisterFaceObb(world, entity: 14, body: 14, faceX: Fix64.One);
            RegisterFaceAabb(world, entity: 15, body: 15, faceX: Fix64.FromInt(2));
            RegisterSphere(world, entity: 16, body: 16, center: Raw(3000000, 0, 0));
            RegisterCapsule(world, entity: 17, body: 17, center: Raw(3500000, 0, 0));
            return CreateHotPathCase(world, FixVector3.Zero, UnitX, Fix64.FromInt(4));
        }

        private static HotPathCase CreateHotPathCase(
            CombatPhysicsWorld world,
            FixVector3 start,
            FixVector3 direction,
            Fix64 moveSpeed)
        {
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(world, BlockingMask, CombatPhysicsLayerMask.None);
            return new HotPathCase(
                world,
                new CombatKinematicMotor(CreateConfig(moveSpeed)),
                new CombatMotionStepBuffer(3),
                cache,
                CreateState(start),
                new CombatMotionInput(direction, false, Fix64.One));
        }

        private static SequenceOracle RunLongSequence(bool reverseInsertion)
        {
            CombatPhysicsWorld world = CreateWorld(FixVector3.Zero);
            Action registerAabb = () => RegisterFaceAabb(world, entity: 20, body: 20, faceX: Fix64.FromInt(2));
            Action registerObb = () => RegisterFaceObb(world, entity: 21, body: 21, faceX: Fix64.One);
            if (reverseInsertion)
            {
                registerObb();
                registerAabb();
            }
            else
            {
                registerAabb();
                registerObb();
            }

            RegisterObb(world, entity: 22, body: 22, center: Raw(-2000000, 0, 0),
                halfExtents: Raw(1000000, 1000000, 1000000), UnitX, UnitY, UnitZ);
            var cache = new CombatKinematicMotorCandidateCache();
            cache.Prepare(world, BlockingMask, CombatPhysicsLayerMask.None);
            var motor = new CombatKinematicMotor(CreateConfig(Fix64.FromRatio(1, 4)));
            var buffer = new CombatMotionStepBuffer(3);
            CombatMotionState state = CreateState(FixVector3.Zero);
            ulong hash = 1469598103934665603UL;
            int collisionCount = 0;

            for (int tick = 0; tick < 256; tick++)
            {
                FixVector3 direction = tick % 64 < 32 ? UnitX : -UnitX;
                CombatMotionStepResult result = motor.Step(
                    state,
                    new CombatMotionInput(direction, false, Fix64.One),
                    world,
                    SourceEntityId,
                    SourceBodyId,
                    syncBodyPosition: false,
                    impactVelocityOverride: null,
                    buffer: buffer,
                    candidateCache: cache);
                state = result.State;
                collisionCount += result.CollisionCount;
                hash = Mix(hash, state.Frame.Value);
                hash = Mix(hash, state.Position.X.RawValue);
                hash = Mix(hash, state.Position.Y.RawValue);
                hash = Mix(hash, state.Position.Z.RawValue);
                hash = Mix(hash, state.Velocity.X.RawValue);
                hash = Mix(hash, state.Velocity.Y.RawValue);
                hash = Mix(hash, state.Velocity.Z.RawValue);
                hash = Mix(hash, state.Grounded ? 1L : 0L);
                hash = Mix(hash, (long)state.CollisionFlags);
                hash = Mix(hash, state.LastCollisionNormal.X.RawValue);
                hash = Mix(hash, state.LastCollisionNormal.Y.RawValue);
                hash = Mix(hash, state.LastCollisionNormal.Z.RawValue);
            }

            return new SequenceOracle(256, collisionCount, state, hash);
        }

        private static ulong Mix(ulong hash, long value)
        {
            unchecked
            {
                hash ^= (ulong)value;
                return hash * 1099511628211UL;
            }
        }

        private static CombatMotionState CreateState(FixVector3 position)
        {
            return new CombatMotionState(
                new CombatFrame(0),
                position,
                FixVector3.Zero,
                false,
                FixVector3.Zero,
                CombatMotionCollisionFlags.None);
        }

        private static CombatMotionConfig CreateConfig(Fix64 moveSpeed)
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
                maxCollisionIterations: 3,
                CharacterHalfExtents,
                BlockingMask,
                maxStepHeight: Fix64.Zero);
        }

        private static CombatPhysicsWorld CreateWorld(FixVector3 sourcePosition)
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(SourceEntityId, SourceBodyId, sourcePosition));
            return world;
        }

        private static void RegisterFaceObb(CombatPhysicsWorld world, int entity, int body, Fix64 faceX)
        {
            RegisterObb(world, entity, body, new FixVector3(faceX + Fix64.One, Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.One, Fix64.One, Fix64.One), UnitX, UnitY, UnitZ);
        }

        private static void RegisterObb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            FixVector3 center,
            FixVector3 halfExtents,
            FixVector3 axisX,
            FixVector3 axisY,
            FixVector3 axisZ)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, center));
            world.UpsertObbCollider(new CombatPhysicsObbCollider(
                bodyId,
                new CombatColliderId(1),
                1,
                FixVector3.Zero,
                halfExtents,
                axisX,
                axisY,
                axisZ));
        }

        private static void RegisterFaceAabb(CombatPhysicsWorld world, int entity, int body, Fix64 faceX)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                1,
                new FixVector3(faceX, -Fix64.One, -Fix64.One),
                new FixVector3(faceX + Fix64.FromInt(2), Fix64.One, Fix64.One)));
        }

        private static void RegisterSphere(CombatPhysicsWorld world, int entity, int body, FixVector3 center)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, center));
            world.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                bodyId,
                new CombatColliderId(1),
                1,
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

        private static CombatMotionCapsuleProxy DefaultCapsule()
        {
            return new CombatMotionCapsuleProxy(
                Fix64.Half,
                Fix64.FromInt(2),
                FixVector3.Zero,
                Fix64.Zero,
                0,
                CombatPhysicsLayerMask.All);
        }

        private static CombatPhysicsShape IdentityObb(FixVector3 halfExtents)
        {
            return CombatPhysicsShape.Obb(FixVector3.Zero, halfExtents, UnitX, UnitY, UnitZ);
        }

        private static FixVector3 Raw(long x, long y, long z)
        {
            return new FixVector3(Fix64.FromRaw(x), Fix64.FromRaw(y), Fix64.FromRaw(z));
        }

        private static void AssertVectorNear(FixVector3 expected, FixVector3 actual, long toleranceRaw)
        {
            Assert.That(Math.Abs(expected.X.RawValue - actual.X.RawValue), Is.LessThanOrEqualTo(toleranceRaw));
            Assert.That(Math.Abs(expected.Y.RawValue - actual.Y.RawValue), Is.LessThanOrEqualTo(toleranceRaw));
            Assert.That(Math.Abs(expected.Z.RawValue - actual.Z.RawValue), Is.LessThanOrEqualTo(toleranceRaw));
        }

        private sealed class HotPathCase
        {
            public HotPathCase(
                CombatPhysicsWorld world,
                CombatKinematicMotor motor,
                CombatMotionStepBuffer buffer,
                CombatKinematicMotorCandidateCache cache,
                CombatMotionState state,
                CombatMotionInput input)
            {
                World = world;
                Motor = motor;
                Buffer = buffer;
                Cache = cache;
                State = state;
                Input = input;
            }

            public CombatPhysicsWorld World { get; }
            public CombatKinematicMotor Motor { get; }
            public CombatMotionStepBuffer Buffer { get; }
            public CombatKinematicMotorCandidateCache Cache { get; }
            public CombatMotionState State { get; }
            public CombatMotionInput Input { get; }
        }

        private readonly struct SequenceOracle : IEquatable<SequenceOracle>
        {
            public SequenceOracle(int tickCount, int collisionCount, CombatMotionState state, ulong hash)
            {
                TickCount = tickCount;
                CollisionCount = collisionCount;
                State = state;
                Hash = hash;
            }

            public int TickCount { get; }
            public int CollisionCount { get; }
            public CombatMotionState State { get; }
            public ulong Hash { get; }

            public bool Equals(SequenceOracle other)
            {
                return TickCount == other.TickCount
                    && CollisionCount == other.CollisionCount
                    && State.Equals(other.State)
                    && Hash == other.Hash;
            }

            public override bool Equals(object obj) => obj is SequenceOracle other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = TickCount;
                    hashCode = (hashCode * 397) ^ CollisionCount;
                    hashCode = (hashCode * 397) ^ State.GetHashCode();
                    hashCode = (hashCode * 397) ^ Hash.GetHashCode();
                    return hashCode;
                }
            }
        }
    }
}
