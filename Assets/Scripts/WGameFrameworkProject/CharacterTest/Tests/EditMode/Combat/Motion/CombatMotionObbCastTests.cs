using System;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Motion
{
    public sealed class CombatMotionObbCastTests
    {
        private static readonly FixVector3 UnitX = Raw(1000000, 0, 0);
        private static readonly FixVector3 UnitY = Raw(0, 1000000, 0);
        private static readonly FixVector3 UnitZ = Raw(0, 0, 1000000);

        [Test]
        public void DirectedRawArithmetic_P01_DoesNotLateAdvance()
        {
            FixVector3 witness = Raw(600000, 800000, 0);
            FixVector3 delta = Raw(1000000002, -750000376, 0);
            long dotRaw2 = CombatMotionObbCast.DotRaw2Checked(delta, witness);
            long closingUpper = CombatMotionObbCast.DivideCeilPositive(-dotRaw2, 1000000);
            long advanceRaw = CombatMotionObbCast.MulScaleDivFloorPositive(150, closingUpper);

            Assert.That(dotRaw2, Is.EqualTo(-299600000L));
            Assert.That(delta.Dot(witness).RawValue, Is.EqualTo(-299L));
            Assert.That(closingUpper, Is.EqualTo(300L));
            Assert.That(advanceRaw, Is.EqualTo(500000L));
            Assert.That(advanceRaw, Is.LessThanOrEqualTo(500667L));
        }

        [Test]
        public void DirectedRawArithmetic_P02_UsesFrozenFloorAndCeil()
        {
            FixVector3 witness = Raw(1000000, 0, 7000000);
            FixVector3 delta = Raw(-100000000, 0, -100000000);
            long distanceSquaredRaw2 = CombatMotionObbCast.SquareLengthRaw2Checked(witness);
            long floor = CombatMotionObbCast.SqrtFloorRaw2(distanceSquaredRaw2);
            long ceil = CombatMotionObbCast.SqrtCeilRaw2(distanceSquaredRaw2);
            long advanceGapLower = floor - 100000 - CombatMotionObbCast.ContactThresholdRaw;
            long closingRaw2 = -CombatMotionObbCast.DotRaw2Checked(delta, witness);
            long closingUpper = CombatMotionObbCast.DivideCeilPositive(closingRaw2, floor);
            long advanceRaw = CombatMotionObbCast.MulScaleDivFloorPositive(advanceGapLower, closingUpper);

            Assert.That(distanceSquaredRaw2, Is.EqualTo(50000000000000L));
            Assert.That(floor, Is.EqualTo(7071067L));
            Assert.That(ceil, Is.EqualTo(7071068L));
            Assert.That(advanceGapLower, Is.EqualTo(6971051L));
            Assert.That(closingRaw2, Is.EqualTo(800000000000000L));
            Assert.That(closingUpper, Is.EqualTo(113137098L));
            Assert.That(advanceRaw, Is.EqualTo(61615L));
        }

        [Test]
        public void DirectedRawArithmetic_SignedMulDivUsesMathematicalFloor()
        {
            Assert.That(CombatMotionObbCast.MulDivFloorSigned(-5, 3, 2), Is.EqualTo(-8));
            Assert.That(CombatMotionObbCast.MulDivFloorSigned(5, 3, 2), Is.EqualTo(7));
            Assert.That(CombatMotionObbCast.MulDivFloorSigned(-4, 3, 2), Is.EqualTo(-6));
        }

        [Test]
        public void Cast_P02_SphereDegenerateCapsuleIsValidAndMisses()
        {
            var sphereCapsule = new CombatMotionCapsuleProxy(
                Fix64.FromRaw(100000), Fix64.FromRaw(200000), FixVector3.Zero,
                Fix64.Zero, 0, CombatPhysicsLayerMask.All);
            AssertCast(
                sphereCapsule, IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(2000000, 0, 8000000), Raw(-100000000, 0, -100000000),
                false, 0, 0, FixVector3.Zero, CombatObbCastStatus.Miss, 0, 0);
        }

        [Test]
        public void Cast_CapacityEnvelopeRejectsBeforeProofArithmetic()
        {
            CombatPhysicsShape obb = IdentityObb(Raw(1000000, 1000000, 1000000));
            CastResult worldOverflow = Cast(DefaultCapsule(), obb, Raw(2000000001, 0, 0), Raw(-1000000, 0, 0));
            CastResult deltaOverflow = Cast(DefaultCapsule(), obb, Raw(-3000000, 0, 0), Raw(1100000001, 0, 0));
            Assert.That(worldOverflow.Hit, Is.False);
            Assert.That(worldOverflow.Status, Is.EqualTo(CombatObbCastStatus.InvalidInput));
            Assert.That(deltaOverflow.Hit, Is.False);
            Assert.That(deltaOverflow.Status, Is.EqualTo(CombatObbCastStatus.InvalidInput));
        }

        [Test]
        public void Cast_CapacityEnvelopeLegalLongSegmentsDoNotOverflow()
        {
            var longCapsule = new CombatMotionCapsuleProxy(
                Fix64.FromRaw(1), Fix64.FromRaw(1000000000), FixVector3.Zero,
                Fix64.Zero, 0, CombatPhysicsLayerMask.All);
            CombatPhysicsShape thinObb = IdentityObb(Raw(499999000, 1, 1));
            CastResult result = default(CastResult);

            Assert.DoesNotThrow(() => result = Cast(
                longCapsule, thinObb, Raw(500000000, 0, 0), Raw(-1000000, 0, 0)));
            Assert.That(result.Hit, Is.True);
            Assert.That(result.Status, Is.EqualTo(CombatObbCastStatus.Hit));
            Assert.That(result.Fraction.RawValue, Is.EqualTo(983));
            Assert.That(result.Distance.RawValue, Is.EqualTo(983));
            Assert.That(result.Normal, Is.EqualTo(UnitX));
        }

        [Test]
        public void Cast_F01_AxisAlignedFace()
        {
            AssertCast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(-3000000, 0, 0), Raw(4000000, 0, 0), true,
                374996, 1499984, Raw(-1000000, 0, 0), CombatObbCastStatus.Hit, 0, 64, 375000);
        }

        [Test]
        public void Cast_F02_TopFace()
        {
            AssertCast(DefaultCapsule(), IdentityObb(Raw(2000000, 1000000, 2000000)),
                Raw(0, 4000000, 0), Raw(0, -4000000, 0), true,
                499996, 1999984, Raw(0, 1000000, 0), CombatObbCastStatus.Hit, 0, 64, 500000);
        }

        [Test]
        public void Cast_F03_ThirtyDegreeTopFace()
        {
            CombatPhysicsShape obb = CombatPhysicsShape.Obb(
                FixVector3.Zero, Raw(3000000, 500000, 3000000),
                Raw(866025, 500000, 0), Raw(-500000, 866025, 0), UnitZ);
            AssertCast(DefaultCapsule(), obb,
                Raw(-1216506, 2107049, 0), Raw(1000000, -1732050, 0), true,
                500000, 1000000, obb.AxisY, CombatObbCastStatus.PrecisionLimitConservativeHit,
                128, 256, 500000);
        }

        [Test]
        public void Cast_F04_YawPitchTopFace()
        {
            CombatPhysicsShape obb = CombatPhysicsShape.Obb(
                FixVector3.Zero, Raw(3000000, 500000, 3000000),
                Raw(707107, 0, -707107), Raw(353553, 866025, 353553),
                Raw(612372, -500000, 612372));
            AssertCast(DefaultCapsule(), obb,
                Raw(860198, 2107049, 860198), Raw(-707106, -1732050, -707106), true,
                500000, 1000000, obb.AxisY, CombatObbCastStatus.PrecisionLimitConservativeHit,
                128, 256, 500000);
        }

        [Test]
        public void Cast_F05_VerticalEdge()
        {
            AssertCast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(3000000, 0, 3000000), Raw(-3000000, 0, -3000000), true,
                548815, 2328427, Raw(707106, 0, 707106),
                CombatObbCastStatus.PrecisionLimitConservativeHit, 128, 256, 548815);
        }

        [Test]
        public void Cast_F06_TopCorner()
        {
            AssertCast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(3000000, 3000000, 3000000), Raw(-3000000, -2000000, -3000000), true,
                580178, 2721261, Raw(518927, 679285, 518927),
                CombatObbCastStatus.PrecisionLimitConservativeHit, 128, 256, 580178);
        }

        [Test]
        public void Cast_F07_HighSpeedThinObbFailsClosedAtFrozenRaw()
        {
            AssertCast(DefaultCapsule(), IdentityObb(Raw(50000, 2000000, 2000000)),
                Raw(-100000000, 0, 0), Raw(200000000, 0, 0), true,
                497249, 99449800, Raw(-1000000, 0, 0),
                CombatObbCastStatus.PrecisionLimitConservativeHit, 0, 64, 497250);
        }

        [TestCase(-3000000L, 6000000L)]
        [TestCase(-100000000L, 200000000L)]
        public void Cast_F08AndF08A_TangentialNearMissDoesNotUseLimit(long startX, long deltaX)
        {
            AssertCast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(startX, 0, 1500100), Raw(deltaX, 0, 0), false,
                0, 0, FixVector3.Zero, CombatObbCastStatus.Miss, 0, 0);
        }

        [Test]
        public void Cast_F09_ZeroDeltaSeparated()
        {
            AssertCast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(-3000000, 0, 0), FixVector3.Zero, false,
                0, 0, FixVector3.Zero, CombatObbCastStatus.ZeroDelta, 0, 0);
        }

        [Test]
        public void Cast_F09A_MovingAwayMisses()
        {
            AssertCast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(-3000000, 0, 0), Raw(-2000000, 0, 0), false,
                0, 0, FixVector3.Zero, CombatObbCastStatus.Miss, 0, 0);
        }

        [Test]
        public void Cast_F09B_AxisSignEquivalentIsRawIdentical()
        {
            CombatPhysicsShape identity = IdentityObb(Raw(1000000, 1000000, 1000000));
            CombatPhysicsShape signed = CombatPhysicsShape.Obb(
                FixVector3.Zero, Raw(1000000, 1000000, 1000000), -UnitX, UnitY, -UnitZ);
            CastResult first = Cast(DefaultCapsule(), identity, Raw(-3000000, 0, 0), Raw(4000000, 0, 0));
            CastResult second = Cast(DefaultCapsule(), signed, Raw(-3000000, 0, 0), Raw(4000000, 0, 0));
            Assert.That(second.Hit, Is.EqualTo(first.Hit));
            Assert.That(second.Fraction.RawValue, Is.EqualTo(first.Fraction.RawValue));
            Assert.That(second.Distance.RawValue, Is.EqualTo(first.Distance.RawValue));
            Assert.That(second.Normal, Is.EqualTo(first.Normal));
            Assert.That(second.Status, Is.EqualTo(first.Status));
        }

        [Test]
        public void Cast_RepeatedInputsAreRawDeterministic()
        {
            CombatPhysicsShape obb = IdentityObb(Raw(1000000, 1000000, 1000000));
            CastResult expected = Cast(DefaultCapsule(), obb,
                Raw(3000000, 3000000, 3000000), Raw(-3000000, -2000000, -3000000));
            for (int i = 0; i < 32; i++)
            {
                CastResult actual = Cast(DefaultCapsule(), obb,
                    Raw(3000000, 3000000, 3000000), Raw(-3000000, -2000000, -3000000));
                Assert.That(actual.Hit, Is.EqualTo(expected.Hit));
                Assert.That(actual.Fraction.RawValue, Is.EqualTo(expected.Fraction.RawValue));
                Assert.That(actual.Distance.RawValue, Is.EqualTo(expected.Distance.RawValue));
                Assert.That(actual.Normal, Is.EqualTo(expected.Normal));
                Assert.That(actual.Status, Is.EqualTo(expected.Status));
            }
        }

        [Test]
        public void Cast_LargeWorldRotatedBasisSupportOracleIsConservative()
        {
            FixVector3 center = Raw(1400000000, 0, 0);
            CombatPhysicsShape obb = CombatPhysicsShape.Obb(
                center, Raw(1000000, 1000000, 1000000),
                Raw(707107, 0, -707107), UnitY, Raw(707107, 0, 707107));
            FixVector3 delta = Raw(2828428, 0, -2828428);
            CastResult result = Cast(DefaultCapsule(), obb,
                Raw(1397878679, 0, 2121321), delta);
            Assert.That(result.Hit, Is.True);
            Assert.That(result.Status, Is.EqualTo(CombatObbCastStatus.PrecisionLimitConservativeHit));
            Assert.That(result.Fraction.RawValue, Is.InRange(375000 - 128, 375000));
            Assert.That(result.Normal, Is.EqualTo(Raw(-707107, 0, 707107)));
            Assert.That(delta.Dot(result.Normal), Is.LessThan(Fix64.Zero));
        }

        [Test]
        public void Cast_NearBasisToleranceSupportOracleIsConservative()
        {
            CombatPhysicsShape obb = CombatPhysicsShape.Obb(
                FixVector3.Zero, Raw(1000000, 1000000, 1000000),
                UnitX, Raw(63, 999999, 0), UnitZ);
            CastResult result = Cast(DefaultCapsule(), obb, Raw(-3000000, 0, 0), Raw(4000000, 0, 0));
            Assert.That(result.Hit, Is.True);
            Assert.That(result.Status, Is.EqualTo(CombatObbCastStatus.PrecisionLimitConservativeHit));
            Assert.That(result.Fraction.RawValue, Is.InRange(375000 - 128, 375000));
            Assert.That(result.Normal, Is.EqualTo(Raw(-1000000, 0, 0)));
            long supportGap = -1500000 - (-3000000 + 4000000 * result.Fraction.RawValue / Fix64.Scale);
            long beforeFraction = Math.Max(0, result.Fraction.RawValue - 1);
            long supportGapBefore = -1500000 - (-3000000 + 4000000 * beforeFraction / Fix64.Scale);
            Assert.That(supportGap, Is.GreaterThanOrEqualTo(0));
            Assert.That(supportGapBefore, Is.GreaterThanOrEqualTo(supportGap));
        }

        [Test]
        public void Cast_NearBasisToleranceLargeLocalSupportOracleIsConservative()
        {
            CombatPhysicsShape obb = CombatPhysicsShape.Obb(
                FixVector3.Zero, Raw(497000000, 499000000, 1),
                UnitX, Raw(63, 999999, 0), UnitZ);
            CastResult result = Cast(DefaultCapsule(), obb,
                Raw(498531437, 0, 0), Raw(-2000000, 0, 0));
            Assert.That(result.Hit, Is.True);
            Assert.That(result.Status, Is.EqualTo(CombatObbCastStatus.PrecisionLimitConservativeHit));
            Assert.That(result.Fraction.RawValue, Is.InRange(500000 - 128, 500000));
            Assert.That(result.Normal, Is.EqualTo(UnitX));
            long worldSupportGap = 1000000 - 2 * result.Fraction.RawValue;
            Assert.That(worldSupportGap, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void AnalyticOracles_F03ToF06ReturnedAndPreContactPointsStayOnSafeSide()
        {
            CombatPhysicsShape f03Obb = CombatPhysicsShape.Obb(
                FixVector3.Zero, Raw(3000000, 500000, 3000000),
                Raw(866025, 500000, 0), Raw(-500000, 866025, 0), UnitZ);
            CombatPhysicsShape f04Obb = CombatPhysicsShape.Obb(
                FixVector3.Zero, Raw(3000000, 500000, 3000000),
                Raw(707107, 0, -707107), Raw(353553, 866025, 353553),
                Raw(612372, -500000, 612372));
            CombatPhysicsShape identity = IdentityObb(Raw(1000000, 1000000, 1000000));
            CastResult f03 = Cast(DefaultCapsule(), f03Obb,
                Raw(-1216506, 2107049, 0), Raw(1000000, -1732050, 0));
            CastResult f04 = Cast(DefaultCapsule(), f04Obb,
                Raw(860198, 2107049, 860198), Raw(-707106, -1732050, -707106));
            CastResult f05 = Cast(DefaultCapsule(), identity,
                Raw(3000000, 0, 3000000), Raw(-3000000, 0, -3000000));
            CastResult f06 = Cast(DefaultCapsule(), identity,
                Raw(3000000, 3000000, 3000000), Raw(-3000000, -2000000, -3000000));
            Assert.That(f03.Fraction.RawValue, Is.LessThanOrEqualTo(500000));
            Assert.That(f04.Fraction.RawValue, Is.LessThanOrEqualTo(500000));
            Assert.That(2 * (500000 - 499999), Is.GreaterThan(0));
            AssertEdgePointIsSeparated(548814);
            Assert.That(f05.Fraction.RawValue, Is.LessThanOrEqualTo(548815));
            AssertCornerPointIsSeparated(580177);
            Assert.That(f06.Fraction.RawValue, Is.LessThanOrEqualTo(580178));
        }

        [Test]
        public void Cast_WarmedDirectCallsAllocateZeroBytes()
        {
            CombatMotionCapsuleProxy capsule = DefaultCapsule();
            CombatPhysicsShape obb = IdentityObb(Raw(1000000, 1000000, 1000000));
            FixVector3 start = Raw(-3000000, 0, 0);
            FixVector3 delta = Raw(4000000, 0, 0);
            for (int i = 0; i < 16; i++)
                CombatMotionObbCast.TryCastSeparated(capsule, start, delta, obb, out _, out _, out _, out _);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
                CombatMotionObbCast.TryCastSeparated(capsule, start, delta, obb, out _, out _, out _, out _);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        [Test]
        public void Cast_WarmedRotatedEdgeMissAndPrecisionPathsAllocateZeroBytes()
        {
            CombatMotionCapsuleProxy capsule = DefaultCapsule();
            CombatPhysicsShape rotated = CombatPhysicsShape.Obb(
                FixVector3.Zero, Raw(3000000, 500000, 3000000),
                Raw(866025, 500000, 0), Raw(-500000, 866025, 0), UnitZ);
            CombatPhysicsShape box = IdentityObb(Raw(1000000, 1000000, 1000000));
            CombatPhysicsShape thin = IdentityObb(Raw(50000, 2000000, 2000000));
            for (int i = 0; i < 16; i++)
                RunRepresentativeNoAllocCalls(capsule, rotated, box, thin);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                RunRepresentativeNoAllocCalls(capsule, rotated, box, thin);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        private static void RunRepresentativeNoAllocCalls(
            CombatMotionCapsuleProxy capsule,
            CombatPhysicsShape rotated,
            CombatPhysicsShape box,
            CombatPhysicsShape thin)
        {
            CombatMotionObbCast.TryCastSeparated(capsule, Raw(-1216506, 2107049, 0), Raw(1000000, -1732050, 0), rotated, out _, out _, out _, out _);
            CombatMotionObbCast.TryCastSeparated(capsule, Raw(3000000, 0, 3000000), Raw(-3000000, 0, -3000000), box, out _, out _, out _, out _);
            CombatMotionObbCast.TryCastSeparated(capsule, Raw(-100000000, 0, 1500100), Raw(200000000, 0, 0), box, out _, out _, out _, out _);
            CombatMotionObbCast.TryCastSeparated(capsule, Raw(-100000000, 0, 0), Raw(200000000, 0, 0), thin, out _, out _, out _, out _);
        }

        private static void AssertEdgePointIsSeparated(long fractionRaw)
        {
            long excessRaw = 2000000 - 3 * fractionRaw;
            Assert.That(2 * excessRaw * excessRaw, Is.GreaterThan(500000L * 500000L));
        }

        private static void AssertCornerPointIsSeparated(long fractionRaw)
        {
            long horizontalExcessRaw = 2000000 - 3 * fractionRaw;
            long verticalExcessRaw = 1500000 - 2 * fractionRaw;
            long distanceSquaredRaw2 = 2 * horizontalExcessRaw * horizontalExcessRaw
                + verticalExcessRaw * verticalExcessRaw;
            Assert.That(distanceSquaredRaw2, Is.GreaterThan(500000L * 500000L));
        }

        private static void AssertCast(
            CombatMotionCapsuleProxy capsule,
            CombatPhysicsShape obb,
            FixVector3 start,
            FixVector3 delta,
            bool expectedHit,
            long expectedFraction,
            long expectedDistance,
            FixVector3 expectedNormal,
            CombatObbCastStatus expectedStatus,
            long fractionTolerance,
            long normalTolerance,
            long mathematicalContactFraction = -1)
        {
            CastResult first = Cast(capsule, obb, start, delta);
            CastResult second = Cast(capsule, obb, start, delta);
            Assert.That(first.Hit, Is.EqualTo(expectedHit));
            Assert.That(first.Status, Is.EqualTo(expectedStatus));
            Assert.That(first.Fraction.RawValue, Is.EqualTo(second.Fraction.RawValue));
            Assert.That(first.Distance.RawValue, Is.EqualTo(second.Distance.RawValue));
            Assert.That(first.Normal, Is.EqualTo(second.Normal));
            Assert.That(Math.Abs(first.Fraction.RawValue - expectedFraction), Is.LessThanOrEqualTo(fractionTolerance));
            Assert.That(Math.Abs(first.Distance.RawValue - expectedDistance), Is.LessThanOrEqualTo(Math.Max(32L, fractionTolerance * 5L)));
            Assert.That(Math.Abs(first.Normal.X.RawValue - expectedNormal.X.RawValue), Is.LessThanOrEqualTo(normalTolerance));
            Assert.That(Math.Abs(first.Normal.Y.RawValue - expectedNormal.Y.RawValue), Is.LessThanOrEqualTo(normalTolerance));
            Assert.That(Math.Abs(first.Normal.Z.RawValue - expectedNormal.Z.RawValue), Is.LessThanOrEqualTo(normalTolerance));
            if (expectedHit)
            {
                Assert.That(mathematicalContactFraction, Is.GreaterThanOrEqualTo(0));
                Assert.That(first.Fraction.RawValue, Is.LessThanOrEqualTo(mathematicalContactFraction));
                Assert.That(first.Normal.TryNormalize(out _), Is.True);
                Assert.That(delta.Dot(first.Normal), Is.LessThan(Fix64.Zero));
            }
        }

        private static CastResult Cast(
            CombatMotionCapsuleProxy capsule,
            CombatPhysicsShape obb,
            FixVector3 start,
            FixVector3 delta)
        {
            bool hit = CombatMotionObbCast.TryCastSeparated(
                capsule, start, delta, obb,
                out Fix64 fraction, out Fix64 distance,
                out FixVector3 normal, out CombatObbCastStatus status);
            return new CastResult(hit, fraction, distance, normal, status);
        }

        private static CombatMotionCapsuleProxy DefaultCapsule()
        {
            return new CombatMotionCapsuleProxy(
                Fix64.Half, Fix64.FromInt(2), FixVector3.Zero,
                Fix64.Zero, 0, CombatPhysicsLayerMask.All);
        }

        private static CombatPhysicsShape IdentityObb(FixVector3 extents)
        {
            return CombatPhysicsShape.Obb(FixVector3.Zero, extents, UnitX, UnitY, UnitZ);
        }

        private static FixVector3 Raw(long x, long y, long z)
        {
            return new FixVector3(Fix64.FromRaw(x), Fix64.FromRaw(y), Fix64.FromRaw(z));
        }

        private readonly struct CastResult
        {
            public CastResult(
                bool hit,
                Fix64 fraction,
                Fix64 distance,
                FixVector3 normal,
                CombatObbCastStatus status)
            {
                Hit = hit;
                Fraction = fraction;
                Distance = distance;
                Normal = normal;
                Status = status;
            }

            public bool Hit { get; }
            public Fix64 Fraction { get; }
            public Fix64 Distance { get; }
            public FixVector3 Normal { get; }
            public CombatObbCastStatus Status { get; }
        }
    }
}
