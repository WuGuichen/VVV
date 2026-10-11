using System;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Motion
{
    [TestFixture]
    public sealed class CombatMotionObbCastOverlapSkinTests
    {
        private static readonly FixVector3 UnitX = Raw(Fix64.Scale, 0, 0);
        private static readonly FixVector3 UnitY = Raw(0, Fix64.Scale, 0);
        private static readonly FixVector3 UnitZ = Raw(0, 0, Fix64.Scale);

        [TestCase(-1000000L, false)]
        [TestCase(1000000L, true)]
        [TestCase(0L, false)]
        public void F10ToF12_FaceTouchingResolvesDirection(long deltaX, bool expectedHit)
        {
            CastResult result = Cast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(-1500000, 0, 0), Raw(deltaX, 0, deltaX == 0 ? 1000000 : 0));

            Assert.That(result.Hit, Is.EqualTo(expectedHit));
            Assert.That(result.Status, Is.EqualTo(CombatObbCastStatus.InitialTouching));
            Assert.That(result.Fraction.RawValue, Is.Zero);
            Assert.That(result.Distance.RawValue, Is.Zero);
            Assert.That(result.Normal, Is.EqualTo(-UnitX));
        }

        [Test]
        public void Touching_ZeroDeltaDoesNotInventMovement()
        {
            CastResult result = Cast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(-1500000, 0, 0), FixVector3.Zero);

            Assert.That(result.Hit, Is.False);
            Assert.That(result.Status, Is.EqualTo(CombatObbCastStatus.InitialTouching));
            Assert.That(result.Normal, Is.EqualTo(-UnitX));
        }

        [TestCase(1353553L, 0L)]
        [TestCase(1288675L, 288675L)]
        public void Touching_EdgeAndCornerResolveIntoAndAway(long horizontalRaw, long verticalOffsetRaw)
        {
            FixVector3 start = verticalOffsetRaw == 0
                ? Raw(horizontalRaw, 0, horizontalRaw)
                : Raw(horizontalRaw, 1500000 + verticalOffsetRaw, horizontalRaw);
            FixVector3 into = verticalOffsetRaw == 0
                ? Raw(-1000000, 0, -1000000)
                : Raw(-1000000, -1000000, -1000000);

            CastResult inward = Cast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)), start, into);
            CastResult away = Cast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)), start, -into);

            Assert.That(inward.Hit, Is.True);
            Assert.That(away.Hit, Is.False);
            Assert.That(inward.Status, Is.EqualTo(CombatObbCastStatus.InitialTouching));
            Assert.That(away.Status, Is.EqualTo(CombatObbCastStatus.InitialTouching));
            Assert.That(inward.Normal, Is.EqualTo(away.Normal));
            Assert.That(CombatMotionObbCast.DotRaw2Checked(into, inward.Normal), Is.LessThan(0));
        }

        [TestCase(699999L, true)]
        [TestCase(700000L, false)]
        [TestCase(700001L, false)]
        public void TouchingCorner_UsesOriginalWitnessForNearZeroDirectionSign(long deltaXRaw, bool expectedHit)
        {
            var sphere = new CombatMotionCapsuleProxy(
                Fix64.FromRaw(707106), Fix64.FromRaw(1414212), FixVector3.Zero,
                Fix64.Zero, 0, CombatPhysicsLayerMask.All);
            FixVector3 delta = Raw(deltaXRaw, -100000, 0);
            FixVector3 originalWitness = Raw(100000, 700000, 0);

            CastResult result = Cast(sphere, IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(1100000, 1700000, 0), delta);

            Assert.That(result.Hit, Is.EqualTo(expectedHit));
            Assert.That(result.Status, Is.EqualTo(CombatObbCastStatus.InitialTouching));
            Assert.That(result.Fraction.RawValue, Is.Zero);
            Assert.That(result.Distance.RawValue, Is.Zero);
            Assert.That(result.Normal, Is.EqualTo(Raw(141421, 989950, 0)));
            Assert.That(result.NonEnteringDirection, Is.EqualTo(originalWitness));
            Assert.That(
                CombatMotionObbCast.DotRaw2Checked(delta, originalWitness),
                Is.EqualTo(checked(100000L * (deltaXRaw - 700000L))));
            if (deltaXRaw == 700000)
            {
                Assert.That(CombatMotionObbCast.DotRaw2Checked(delta, originalWitness), Is.Zero);
                Assert.That(CombatMotionObbCast.DotRaw2Checked(delta, result.Normal), Is.LessThan(0));
                Assert.That(result.Hit, Is.False, "normalized output must not decide tangent direction");
            }
        }

        [Test]
        public void InitialContactRawWitnessClosesProjectionAndLegacySignatureRemainsEquivalent()
        {
            var capsule = new CombatMotionCapsuleProxy(
                Fix64.FromRaw(707106),
                Fix64.FromRaw(1414212),
                FixVector3.Zero,
                Fix64.FromRaw(10000),
                0,
                CombatPhysicsLayerMask.All);
            CombatPhysicsShape obb = IdentityObb(Raw(1000000, 1000000, 1000000));
            FixVector3 start = Raw(1100000, 1700000, 0);
            FixVector3 inward = Raw(699999, -100000, 0);

            bool expandedHit = CombatMotionObbCast.TryCast(
                capsule,
                start,
                inward,
                obb,
                out Fix64 fraction,
                out Fix64 distance,
                out FixVector3 normal,
                out FixVector3 nonEnteringDirection,
                out CombatObbCastStatus status);
            bool legacyHit = CombatMotionObbCast.TryCast(
                capsule,
                start,
                inward,
                obb,
                out Fix64 legacyFraction,
                out Fix64 legacyDistance,
                out FixVector3 legacyNormal,
                out CombatObbCastStatus legacyStatus);

            Assert.IsTrue(expandedHit);
            Assert.AreEqual(Raw(100000, 700000, 0), nonEnteringDirection);
            Assert.AreEqual(expandedHit, legacyHit);
            Assert.AreEqual(fraction, legacyFraction);
            Assert.AreEqual(distance, legacyDistance);
            Assert.AreEqual(normal, legacyNormal);
            Assert.AreEqual(status, legacyStatus);

            FixVector3 resolved = CombatKinematicMotor.ProjectWalkableRemaining(
                inward,
                normal,
                nonEnteringDirection);
            Assert.IsTrue(CombatKinematicMotor.TryRawDotNumerator(
                resolved,
                nonEnteringDirection,
                out long rawDot));
            Assert.GreaterOrEqual(rawDot, 0L);
            bool reblocked = CombatMotionObbCast.TryCast(
                capsule,
                start,
                resolved,
                obb,
                out Fix64 recheckFraction,
                out _,
                out _,
                out FixVector3 recheckDirection,
                out CombatObbCastStatus recheckStatus);
            Assert.IsFalse(reblocked);
            Assert.AreEqual(Fix64.Zero, recheckFraction);
            Assert.AreEqual(CombatObbCastStatus.InitialTouching, recheckStatus);
            Assert.AreEqual(nonEnteringDirection, recheckDirection);
        }

        [Test]
        public void SeparatedNearThresholdFaceEdgeCornerTangentAndAwayMissWhileIntoBlocks()
        {
            var sphere = new CombatMotionCapsuleProxy(
                Fix64.Half,
                Fix64.One,
                FixVector3.Zero,
                Fix64.FromRaw(10000),
                0,
                CombatPhysicsLayerMask.All);
            CombatPhysicsShape obb = IdentityObb(Raw(1000000, 1000000, 1000000));
            FixVector3[] starts =
            {
                Raw(-1501000, 0, 0),
                Raw(-1354260, -1354260, 0),
                Raw(-1289252, -1289252, -1289252),
            };
            FixVector3[] tangents =
            {
                Raw(0, 0, 100000),
                Raw(0, 0, 100000),
                Raw(100000, -100000, 0),
            };
            FixVector3[] away =
            {
                Raw(-100000, 0, 0),
                Raw(-100000, -100000, 0),
                Raw(-100000, -100000, -100000),
            };
            FixVector3[] into =
            {
                -away[0],
                -away[1],
                -away[2],
            };

            for (int i = 0; i < starts.Length; i++)
            {
                CastResult tangent = Cast(sphere, obb, starts[i], tangents[i]);
                CastResult separating = Cast(sphere, obb, starts[i], away[i]);
                CastResult closing = Cast(sphere, obb, starts[i], into[i]);

                Assert.IsFalse(tangent.Hit, "tangent feature " + i);
                Assert.AreEqual(CombatObbCastStatus.Miss, tangent.Status, "tangent status " + i);
                Assert.AreEqual(FixVector3.Zero, tangent.NonEnteringDirection);
                Assert.IsFalse(separating.Hit, "away feature " + i);
                Assert.AreEqual(CombatObbCastStatus.Miss, separating.Status, "away status " + i);
                Assert.AreEqual(FixVector3.Zero, separating.NonEnteringDirection);
                Assert.IsTrue(closing.Hit, "into feature " + i);
                Assert.IsTrue(
                    closing.Status == CombatObbCastStatus.Hit
                    || closing.Status == CombatObbCastStatus.PrecisionLimitConservativeHit
                    || closing.Status == CombatObbCastStatus.IterationLimitConservativeHit,
                    "into status " + i);
                Assert.IsFalse(closing.NonEnteringDirection.IsZero);
            }
        }

        [Test]
        public void SeparatedNonClosingWithoutExpandedGapProofRemainsPrecisionConservativeHit()
        {
            var capsule = new CombatMotionCapsuleProxy(
                Fix64.FromRaw(499900),
                Fix64.FromRaw(999800),
                FixVector3.Zero,
                Fix64.Zero,
                0,
                CombatPhysicsLayerMask.All);
            CombatPhysicsShape obb = CombatPhysicsShape.Obb(
                FixVector3.Zero,
                Raw(1000000, 500000000, 1000000),
                UnitX,
                Raw(63, 1000001, 0),
                UnitZ);
            FixVector3 start = Raw(1500000, 0, 0);
            FixVector3 nonClosing = Raw(0, 100000, 0);

            bool hit = CombatMotionObbCast.TryCastSeparated(
                capsule,
                start,
                nonClosing,
                obb,
                out Fix64 fraction,
                out Fix64 distance,
                out FixVector3 normal,
                out FixVector3 nonEnteringDirection,
                out CombatObbCastStatus status);
            bool legacyHit = CombatMotionObbCast.TryCastSeparated(
                capsule,
                start,
                nonClosing,
                obb,
                out Fix64 legacyFraction,
                out Fix64 legacyDistance,
                out FixVector3 legacyNormal,
                out CombatObbCastStatus legacyStatus);

            Assert.IsTrue(hit);
            Assert.AreEqual(CombatObbCastStatus.PrecisionLimitConservativeHit, status);
            Assert.AreEqual(Fix64.Zero, fraction);
            Assert.AreEqual(Fix64.Zero, distance);
            Assert.IsFalse(nonEnteringDirection.IsZero);
            Assert.AreEqual(hit, legacyHit);
            Assert.AreEqual(fraction, legacyFraction);
            Assert.AreEqual(distance, legacyDistance);
            Assert.AreEqual(normal, legacyNormal);
            Assert.AreEqual(status, legacyStatus);
            Assert.IsTrue(CombatMotionObbCast.TryInspectSeparatedStartForTesting(
                capsule,
                start,
                nonClosing,
                obb,
                out FixVector3 probeWitness,
                out long probeDistanceSquaredRaw2,
                out long probeLengthFloor,
                out long probeLengthCeil,
                out long probeSupportGapLowerRaw2,
                out long probeThresholdDirectionUpperRaw2,
                out long probeCurrentExpandedGapLowerRaw2,
                out long probeDeltaDotWitnessRaw2,
                out FixVector3 probeNormalizedDirection,
                out long probeNormalizedLengthCeil,
                out long probeNormalizedSupportGapLowerRaw2,
                out long probeNormalizedThresholdDirectionUpperRaw2,
                out long probeNormalizedCurrentExpandedGapLowerRaw2,
                out long probeDeltaDotNormalizedRaw2));
            Assert.LessOrEqual(probeCurrentExpandedGapLowerRaw2, 0L);
            Assert.LessOrEqual(probeNormalizedCurrentExpandedGapLowerRaw2, 0L);
            FixVector3[] shapeDirections =
            {
                obb.AxisX,
                -obb.AxisX,
                obb.AxisY,
                -obb.AxisY,
                obb.AxisZ,
                -obb.AxisZ,
            };
            for (int directionIndex = 0;
                 directionIndex < shapeDirections.Length;
                 directionIndex++)
            {
                Assert.IsTrue(CombatMotionObbCast.TryInspectExpandedDirectionForTesting(
                    capsule,
                    start,
                    nonClosing,
                    obb,
                    shapeDirections[directionIndex],
                    out long shapeLengthCeil,
                    out long shapeSupportGapLowerRaw2,
                    out long shapeThresholdDirectionUpperRaw2,
                    out long shapeExpandedGapLowerRaw2,
                    out long shapeDeltaDotRaw2));
                Assert.IsFalse(
                    shapeExpandedGapLowerRaw2 > 0L && shapeDeltaDotRaw2 >= 0L,
                    "dot63 shape direction unexpectedly proved separation: " + directionIndex);
                TestContext.WriteLine(
                    "dot63 shapeDirection[" + directionIndex + "]="
                    + shapeDirections[directionIndex]
                    + " lengthCeil=" + shapeLengthCeil
                    + " supportGapLowerRaw2=" + shapeSupportGapLowerRaw2
                    + " thresholdDirectionUpperRaw2=" + shapeThresholdDirectionUpperRaw2
                    + " expandedGapLowerRaw2=" + shapeExpandedGapLowerRaw2
                    + " deltaDotRaw2=" + shapeDeltaDotRaw2);
            }
            TestContext.WriteLine(
                "dot63 witness=" + probeWitness
                + " distanceSquaredRaw2=" + probeDistanceSquaredRaw2
                + " lengthFloor=" + probeLengthFloor
                + " lengthCeil=" + probeLengthCeil
                + " supportGapLowerRaw2=" + probeSupportGapLowerRaw2
                + " thresholdDirectionUpperRaw2=" + probeThresholdDirectionUpperRaw2
                + " currentExpandedGapLowerRaw2=" + probeCurrentExpandedGapLowerRaw2
                + " deltaDotWitnessRaw2=" + probeDeltaDotWitnessRaw2
                + " normalizedDirection=" + probeNormalizedDirection
                + " normalizedLengthCeil=" + probeNormalizedLengthCeil
                + " normalizedSupportGapLowerRaw2=" + probeNormalizedSupportGapLowerRaw2
                + " normalizedThresholdDirectionUpperRaw2=" + probeNormalizedThresholdDirectionUpperRaw2
                + " normalizedCurrentExpandedGapLowerRaw2=" + probeNormalizedCurrentExpandedGapLowerRaw2
                + " deltaDotNormalizedRaw2=" + probeDeltaDotNormalizedRaw2);
        }

        [TestCase(1000000L, 0L, true)]
        [TestCase(-1000000L, 0L, false)]
        [TestCase(0L, 1000000L, false)]
        public void F13_ShallowFaceOverlapResolvesDirection(long deltaX, long deltaZ, bool expectedHit)
        {
            CastResult result = Cast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(-1490000, 0, 0), Raw(deltaX, 0, deltaZ));

            Assert.That(result.Hit, Is.EqualTo(expectedHit));
            Assert.That(result.Status, Is.EqualTo(CombatObbCastStatus.InitialOverlap));
            Assert.That(result.Fraction.RawValue, Is.Zero);
            Assert.That(result.Normal, Is.EqualTo(-UnitX));
        }

        [TestCase(1300000L, 0L)]
        [TestCase(1250000L, 1750000L)]
        public void Overlap_EdgeAndCornerReturnStableFeatureNormal(long horizontalRaw, long centerYRaw)
        {
            FixVector3 start = Raw(horizontalRaw, centerYRaw, horizontalRaw);
            FixVector3 into = centerYRaw == 0
                ? Raw(-1000000, 0, -1000000)
                : Raw(-1000000, -1000000, -1000000);
            CastResult inward = Cast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)), start, into);
            CastResult away = Cast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)), start, -into);

            Assert.That(inward.Hit, Is.True);
            Assert.That(away.Hit, Is.False);
            Assert.That(inward.Status, Is.EqualTo(CombatObbCastStatus.InitialOverlap));
            Assert.That(inward.Normal, Is.EqualTo(away.Normal));
            Assert.That(CombatMotionObbCast.DotRaw2Checked(into, inward.Normal), Is.LessThan(0));
        }

        [Test]
        public void F14_FullyInsideUsesCompleteSeparationDepthAndYFirstTieBreak()
        {
            CombatPhysicsShape obb = IdentityObb(Raw(2000000, 1500000, 2000000));
            CastResult result = Cast(DefaultCapsule(), obb, FixVector3.Zero, Raw(0, -1000000, 0));

            Assert.That(result.Hit, Is.True);
            Assert.That(result.Status, Is.EqualTo(CombatObbCastStatus.InitialOverlap));
            Assert.That(result.Fraction.RawValue, Is.Zero);
            Assert.That(result.Normal, Is.EqualTo(UnitY));
        }

        [Test]
        public void FullyInside_NonCenteredUsesNearestCompleteSeparationFace()
        {
            CombatPhysicsShape obb = IdentityObb(Raw(2000000, 1500000, 2000000));
            CastResult inward = Cast(DefaultCapsule(), obb, Raw(-250000, 0, 0), Raw(1000000, 0, 0));
            CastResult away = Cast(DefaultCapsule(), obb, Raw(-250000, 0, 0), Raw(-1000000, 0, 0));

            Assert.That(inward.Hit, Is.True);
            Assert.That(away.Hit, Is.False);
            Assert.That(inward.Normal, Is.EqualTo(-UnitX));
            Assert.That(inward.Status, Is.EqualTo(CombatObbCastStatus.InitialOverlap));
        }

        [Test]
        public void FullyInside_EquivalentAxisSignsAreRawInvariant()
        {
            CombatPhysicsShape identity = IdentityObb(Raw(2000000, 1500000, 2000000));
            CombatPhysicsShape signed = CombatPhysicsShape.Obb(
                FixVector3.Zero, Raw(2000000, 1500000, 2000000), -UnitX, UnitY, -UnitZ);

            CastResult first = Cast(DefaultCapsule(), identity, FixVector3.Zero, Raw(0, -1000000, 0));
            CastResult second = Cast(DefaultCapsule(), signed, FixVector3.Zero, Raw(0, -1000000, 0));
            AssertSame(first, second);
        }

        [Test]
        public void FullyInside_RotatedEquivalentAxisSignsUseCanonicalTieBreak()
        {
            FixVector3 axisX = Raw(707107, 0, -707107);
            FixVector3 axisY = UnitY;
            FixVector3 axisZ = Raw(707107, 0, 707107);
            CombatPhysicsShape firstObb = CombatPhysicsShape.Obb(
                FixVector3.Zero, Raw(2000000, 1500000, 2000000), axisX, axisY, axisZ);
            CombatPhysicsShape secondObb = CombatPhysicsShape.Obb(
                FixVector3.Zero, Raw(2000000, 1500000, 2000000), -axisX, axisY, -axisZ);

            CastResult first = Cast(DefaultCapsule(), firstObb, FixVector3.Zero, Raw(0, -1000000, 0));
            CastResult second = Cast(DefaultCapsule(), secondObb, FixVector3.Zero, Raw(0, -1000000, 0));
            AssertSame(first, second);
            Assert.That(first.Normal, Is.EqualTo(UnitY));
        }

        [TestCase(0L, 374996L)]
        [TestCase(10000L, 372496L)]
        [TestCase(100000L, 349996L)]
        public void F15_SkinIsAppliedExactlyOnce(long skinRaw, long expectedSafeFractionRaw)
        {
            CombatMotionCapsuleProxy capsule = CapsuleWithSkin(skinRaw);
            FixVector3 delta = Raw(4000000, 0, 0);
            CastResult result = Cast(capsule, IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(-3000000, 0, 0), delta);
            Fix64 safe = CombatMotionObbCast.CalculateSafeFraction(
                result.Fraction, result.Distance, delta, capsule);

            Assert.That(result.Hit, Is.True);
            Assert.That(result.Fraction.RawValue, Is.EqualTo(374996));
            Assert.That(result.Distance.RawValue, Is.EqualTo(1499984));
            Assert.That(safe.RawValue, Is.EqualTo(expectedSafeFractionRaw));
        }

        [TestCase(-1600000L, 0L)]
        [TestCase(-1600001L, 0L)]
        public void F15_SkinBoundaryIsConservativeWithoutInflatingGeometry(long startRaw, long expectedObbSafeRaw)
        {
            CombatMotionCapsuleProxy capsule = CapsuleWithSkin(100000);
            FixVector3 delta = UnitX;
            CastResult result = Cast(capsule, IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(startRaw, 0, 0), delta);
            Fix64 safe = CombatMotionObbCast.CalculateSafeFraction(
                result.Fraction, result.Distance, delta, capsule);

            Assert.That(result.Hit, Is.True);
            Assert.That(safe.RawValue, Is.EqualTo(expectedObbSafeRaw));
            Assert.That(result.Status, Is.Not.EqualTo(CombatObbCastStatus.InitialTouching));
        }

        [Test]
        public void F15_HighSpeedPrecisionHitRemainsBlockingAndSkinUsesPhysicalDistance()
        {
            CombatMotionCapsuleProxy capsule = CapsuleWithSkin(100000);
            FixVector3 delta = Raw(200000000, 0, 0);
            CastResult result = Cast(capsule, IdentityObb(Raw(50000, 2000000, 2000000)),
                Raw(-100000000, 0, 0), delta);

            Assert.That(result.Hit, Is.True);
            Assert.That(result.Status, Is.EqualTo(CombatObbCastStatus.PrecisionLimitConservativeHit));
            Assert.That(result.Fraction.RawValue, Is.EqualTo(497249));
            Assert.That(CombatMotionObbCast.CalculateSafeFraction(
                result.Fraction, result.Distance, delta, capsule).RawValue, Is.EqualTo(496749));
        }

        [TestCase(-3000000L, 6000000L)]
        [TestCase(-100000000L, 200000000L)]
        public void F15_NearMissWithSkinStaysProvenMiss(long startX, long deltaX)
        {
            CastResult result = Cast(CapsuleWithSkin(100000), IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(startX, 0, 1500100), Raw(deltaX, 0, 0));

            Assert.That(result.Hit, Is.False);
            Assert.That(result.Status, Is.EqualTo(CombatObbCastStatus.Miss));
        }

        [Test]
        public void F16_AxisAlignedObbMatchesSixDirectedAabbOracles()
        {
            FixVector3[] starts =
            {
                Raw(-3000000, 0, 0), Raw(3000000, 0, 0),
                Raw(0, -3000000, 0), Raw(0, 3000000, 0),
                Raw(0, 0, -3000000), Raw(0, 0, 3000000),
            };
            FixVector3[] deltas =
            {
                Raw(4000000, 0, 0), Raw(-4000000, 0, 0),
                Raw(0, 4000000, 0), Raw(0, -4000000, 0),
                Raw(0, 0, 4000000), Raw(0, 0, -4000000),
            };
            FixVector3[] normals = { -UnitX, UnitX, -UnitY, UnitY, -UnitZ, UnitZ };
            long[] obbFractions = { 374996, 374996, 249996, 249996, 374996, 374996 };
            long[] aabbFractions = { 375000, 375000, 250000, 250000, 375000, 375000 };

            for (int i = 0; i < starts.Length; i++)
            {
                CastResult result = Cast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)), starts[i], deltas[i]);
                Assert.That(result.Hit, Is.True, "direction " + i);
                Assert.That(result.Fraction.RawValue, Is.EqualTo(obbFractions[i]), "direction " + i);
                Assert.That(aabbFractions[i] - result.Fraction.RawValue, Is.EqualTo(4), "AABB raw delta " + i);
                Assert.That(result.Normal, Is.EqualTo(normals[i]), "direction " + i);
            }
        }

        [Test]
        public void AxisAlignedAabbEquivalenceMatrixLocksAllSixDirectionsAndBoundaryKinds()
        {
            FixVector3[] normals = { -UnitX, UnitX, -UnitY, UnitY, -UnitZ, UnitZ };
            FixVector3[] touchingStarts =
            {
                Raw(-1500000, 0, 0), Raw(1500000, 0, 0),
                Raw(0, -2000000, 0), Raw(0, 2000000, 0),
                Raw(0, 0, -1500000), Raw(0, 0, 1500000),
            };
            FixVector3[] insideStarts =
            {
                Raw(-1000000, 0, 0), Raw(1000000, 0, 0),
                Raw(0, -750000, 0), Raw(0, 750000, 0),
                Raw(0, 0, -1000000), Raw(0, 0, 1000000),
            };
            FixVector3[] separatedStarts =
            {
                Raw(-3000000, 0, 0), Raw(3000000, 0, 0),
                Raw(0, -3000000, 0), Raw(0, 3000000, 0),
                Raw(0, 0, -3000000), Raw(0, 0, 3000000),
            };
            long[] obbFractions = { 374996, 374996, 249996, 249996, 374996, 374996 };
            long[] obbDistances = { 1499984, 1499984, 999984, 999984, 1499984, 1499984 };
            long[] aabbFractions = { 375000, 375000, 250000, 250000, 375000, 375000 };
            long[] aabbDistances = { 1500000, 1500000, 1000000, 1000000, 1500000, 1500000 };
            long[] skinValues = { 0, 10000, 100000 };
            CombatPhysicsShape contactBox = IdentityObb(Raw(1000000, 1000000, 1000000));
            CombatPhysicsShape insideBox = IdentityObb(Raw(2000000, 2000000, 2000000));

            for (int direction = 0; direction < normals.Length; direction++)
            {
                FixVector3 into = -normals[direction];
                FixVector3 away = normals[direction];
                CastResult touchingInto = Cast(DefaultCapsule(), contactBox, touchingStarts[direction], into);
                CastResult touchingAway = Cast(DefaultCapsule(), contactBox, touchingStarts[direction], away);
                AssertFields(touchingInto, true, CombatObbCastStatus.InitialTouching, 0, 0, normals[direction]);
                AssertFields(touchingAway, false, CombatObbCastStatus.InitialTouching, 0, 0, normals[direction]);
                Assert.That(CombatMotionObbCast.CalculateSafeFraction(
                    touchingInto.Fraction, touchingInto.Distance, into, DefaultCapsule()).RawValue, Is.Zero);
                Assert.That(CombatMotionObbCast.CalculateSafeFraction(
                    touchingAway.Fraction, touchingAway.Distance, away, DefaultCapsule()).RawValue, Is.Zero);

                FixVector3 overlapStart = touchingStarts[direction] + into * Fix64.FromRaw(10000);
                CastResult overlapInto = Cast(DefaultCapsule(), contactBox, overlapStart, into);
                CastResult overlapAway = Cast(DefaultCapsule(), contactBox, overlapStart, away);
                AssertFields(overlapInto, true, CombatObbCastStatus.InitialOverlap, 0, 0, normals[direction]);
                AssertFields(overlapAway, false, CombatObbCastStatus.InitialOverlap, 0, 0, normals[direction]);
                Assert.That(CombatMotionObbCast.CalculateSafeFraction(
                    overlapInto.Fraction, overlapInto.Distance, into, DefaultCapsule()).RawValue, Is.Zero);
                Assert.That(CombatMotionObbCast.CalculateSafeFraction(
                    overlapAway.Fraction, overlapAway.Distance, away, DefaultCapsule()).RawValue, Is.Zero);

                CastResult inside = Cast(DefaultCapsule(), insideBox, insideStarts[direction], into);
                AssertFields(inside, true, CombatObbCastStatus.InitialOverlap, 0, 0, normals[direction]);
                Assert.That(CombatMotionObbCast.CalculateSafeFraction(
                    inside.Fraction, inside.Distance, into, DefaultCapsule()).RawValue, Is.Zero);

                FixVector3 separatedDelta = into * Fix64.FromInt(4);
                for (int skinIndex = 0; skinIndex < skinValues.Length; skinIndex++)
                {
                    long skinRaw = skinValues[skinIndex];
                    CombatMotionCapsuleProxy capsule = CapsuleWithSkin(skinRaw);
                    CastResult separated = Cast(capsule, contactBox, separatedStarts[direction], separatedDelta);
                    long expectedObbSafe = obbFractions[direction] - skinRaw / 4;
                    long expectedAabbSafe = aabbFractions[direction] - skinRaw / 4;
                    AssertFields(
                        separated,
                        true,
                        CombatObbCastStatus.Hit,
                        obbFractions[direction],
                        obbDistances[direction],
                        normals[direction]);
                    Fix64 safe = CombatMotionObbCast.CalculateSafeFraction(
                        separated.Fraction, separated.Distance, separatedDelta, capsule);
                    Assert.That(safe.RawValue, Is.EqualTo(expectedObbSafe),
                        "OBB safe direction " + direction + " skin " + skinRaw);
                    Assert.That(aabbFractions[direction] - separated.Fraction.RawValue, Is.EqualTo(4),
                        "AABB fraction delta direction " + direction);
                    Assert.That(aabbDistances[direction] - separated.Distance.RawValue, Is.EqualTo(16),
                        "AABB distance delta direction " + direction);
                    Assert.That(expectedAabbSafe - safe.RawValue, Is.EqualTo(4),
                        "AABB safe delta direction " + direction + " skin " + skinRaw);
                }
            }
        }

        [Test]
        public void AxisAlignedTouchOverlapAndInsideMatchAabbObservableRules()
        {
            CombatPhysicsShape box = IdentityObb(Raw(1000000, 1000000, 1000000));
            Assert.That(Cast(DefaultCapsule(), box, Raw(-1500000, 0, 0), -UnitX).Hit, Is.False);
            Assert.That(Cast(DefaultCapsule(), box, Raw(-1500000, 0, 0), UnitX).Normal, Is.EqualTo(-UnitX));
            Assert.That(Cast(DefaultCapsule(), box, Raw(-1490000, 0, 0), -UnitX).Hit, Is.False);
            Assert.That(Cast(DefaultCapsule(), box, Raw(-1490000, 0, 0), UnitX).Normal, Is.EqualTo(-UnitX));
            Assert.That(Cast(DefaultCapsule(), IdentityObb(Raw(2000000, 1500000, 2000000)),
                FixVector3.Zero, -UnitY).Normal, Is.EqualTo(UnitY));
        }

        [Test]
        public void ContactThresholdIsOneSidedAndSeparationBySeventeenRawUsesSeparatedCast()
        {
            CastResult touching = Cast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(-1500016, 0, 0), UnitX);
            CastResult separated = Cast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(-1500017, 0, 0), UnitX);

            Assert.That(touching.Hit, Is.True);
            Assert.That(touching.Status, Is.EqualTo(CombatObbCastStatus.InitialTouching));
            Assert.That(separated.Hit, Is.True);
            Assert.That(separated.Status, Is.Not.EqualTo(CombatObbCastStatus.InitialTouching));
            Assert.That(separated.Status, Is.Not.EqualTo(CombatObbCastStatus.InitialOverlap));
        }

        [TestCase(-1499984L, 1000000L, 3, true)]
        [TestCase(-1499984L, -1000000L, 3, false)]
        [TestCase(-1499983L, 1000000L, 4, true)]
        [TestCase(-1499983L, -1000000L, 4, false)]
        public void NegativeContactThresholdBoundaryDistinguishesMinusSixteenAndMinusSeventeen(
            long startX,
            long deltaX,
            int expectedStatusValue,
            bool expectedHit)
        {
            CastResult result = Cast(DefaultCapsule(), IdentityObb(Raw(1000000, 1000000, 1000000)),
                Raw(startX, 0, 0), Raw(deltaX, 0, 0));

            AssertFields(result, expectedHit, (CombatObbCastStatus)expectedStatusValue, 0, 0, -UnitX);
        }

        [Test]
        public void RepeatedBoundaryInputsAreRawDeterministic()
        {
            CombatPhysicsShape obb = IdentityObb(Raw(2000000, 1500000, 2000000));
            CastResult expected = Cast(DefaultCapsule(), obb, FixVector3.Zero, Raw(0, -1000000, 0));
            for (int i = 0; i < 64; i++)
                AssertSame(expected, Cast(DefaultCapsule(), obb, FixVector3.Zero, Raw(0, -1000000, 0)));
        }

        [Test]
        public void WarmedFullPrimitiveAndSafeFractionAllocateZeroBytes()
        {
            CombatMotionCapsuleProxy capsule = CapsuleWithSkin(100000);
            CombatPhysicsShape box = IdentityObb(Raw(1000000, 1000000, 1000000));
            CombatPhysicsShape insideBox = IdentityObb(Raw(2000000, 1500000, 2000000));
            for (int i = 0; i < 16; i++)
                RunNoAllocSet(capsule, box, insideBox);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
                RunNoAllocSet(capsule, box, insideBox);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        private static void RunNoAllocSet(
            CombatMotionCapsuleProxy capsule,
            CombatPhysicsShape box,
            CombatPhysicsShape insideBox)
        {
            CombatMotionObbCast.TryCast(capsule, Raw(-1500000, 0, 0), UnitX, box, out _, out _, out _, out _);
            CombatMotionObbCast.TryCast(capsule, Raw(-1490000, 0, 0), UnitX, box, out _, out _, out _, out _);
            CombatMotionObbCast.TryCast(capsule, FixVector3.Zero, -UnitY, insideBox, out _, out _, out _, out _);
            CombatMotionObbCast.TryCast(capsule, Raw(-3000000, 0, 0), Raw(4000000, 0, 0), box,
                out Fix64 fraction, out Fix64 distance, out _, out _);
            CombatMotionObbCast.CalculateSafeFraction(fraction, distance, Raw(4000000, 0, 0), capsule);
        }

        private static CastResult Cast(
            CombatMotionCapsuleProxy capsule,
            CombatPhysicsShape obb,
            FixVector3 start,
            FixVector3 delta)
        {
            bool hit = CombatMotionObbCast.TryCast(
                capsule, start, delta, obb,
                out Fix64 fraction, out Fix64 distance,
                out FixVector3 normal,
                out FixVector3 nonEnteringDirection,
                out CombatObbCastStatus status);
            return new CastResult(
                hit,
                fraction,
                distance,
                normal,
                nonEnteringDirection,
                status);
        }

        private static void AssertSame(CastResult left, CastResult right)
        {
            Assert.That(right.Hit, Is.EqualTo(left.Hit));
            Assert.That(right.Fraction.RawValue, Is.EqualTo(left.Fraction.RawValue));
            Assert.That(right.Distance.RawValue, Is.EqualTo(left.Distance.RawValue));
            Assert.That(right.Normal, Is.EqualTo(left.Normal));
            Assert.That(right.NonEnteringDirection, Is.EqualTo(left.NonEnteringDirection));
            Assert.That(right.Status, Is.EqualTo(left.Status));
        }

        private static void AssertFields(
            CastResult result,
            bool expectedHit,
            CombatObbCastStatus expectedStatus,
            long expectedFractionRaw,
            long expectedDistanceRaw,
            FixVector3 expectedNormal)
        {
            Assert.That(result.Hit, Is.EqualTo(expectedHit));
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.Fraction.RawValue, Is.EqualTo(expectedFractionRaw));
            Assert.That(result.Distance.RawValue, Is.EqualTo(expectedDistanceRaw));
            Assert.That(result.Normal, Is.EqualTo(expectedNormal));
        }

        private static CombatMotionCapsuleProxy DefaultCapsule() => CapsuleWithSkin(0);

        private static CombatMotionCapsuleProxy CapsuleWithSkin(long skinRaw)
        {
            return new CombatMotionCapsuleProxy(
                Fix64.Half, Fix64.FromInt(2), FixVector3.Zero,
                Fix64.FromRaw(skinRaw), 0, CombatPhysicsLayerMask.All);
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
                FixVector3 nonEnteringDirection,
                CombatObbCastStatus status)
            {
                Hit = hit;
                Fraction = fraction;
                Distance = distance;
                Normal = normal;
                NonEnteringDirection = nonEnteringDirection;
                Status = status;
            }

            public bool Hit { get; }
            public Fix64 Fraction { get; }
            public Fix64 Distance { get; }
            public FixVector3 Normal { get; }
            public FixVector3 NonEnteringDirection { get; }
            public CombatObbCastStatus Status { get; }
        }
    }
}
