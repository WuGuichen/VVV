using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Motion
{
    public sealed class CombatKinematicMotorSlopeSurfaceTests
    {
        private static readonly FixVector3 HalfExtents =
            new FixVector3(Fix64.Half, Fix64.One, Fix64.Half);

        [TestCase(0, 1000000, 10)]
        [TestCase(258819, 965926, 10)]
        [TestCase(500000, 866025, 10)]
        [TestCase(707107, 707107, 10)]
        [TestCase(766044, 642788, 291)]
        public void Classification_UsesFrozenSlopeAngleMatrix(int normalX, int normalY, int expectedFlags)
        {
            CombatKinematicMotor motor = CreateMotor(Fix64.FromRaw(707107), Fix64.Half, Fix64.Zero);

            CombatMotionCollisionFlags flags = motor.ClassifyCollision(
                R(normalX, normalY, 0));

            Assert.AreEqual(expectedFlags, (int)flags);
        }

        [Test]
        public void Classification_UsesInclusiveThresholdAndBothSides()
        {
            CombatKinematicMotor motor = CreateMotor(Fix64.FromRaw(750000), Fix64.Half, Fix64.Zero);

            Assert.AreEqual(10, (int)motor.ClassifyCollision(R(661438, 750000, 0)));
            Assert.AreEqual(291, (int)motor.ClassifyCollision(R(661439, 749999, 0)));
        }

        [Test]
        public void Classification_ZeroThresholdStillTreatsZeroYAsWall()
        {
            CombatKinematicMotor zeroGround = CreateMotor(Fix64.Zero, Fix64.Half, Fix64.Zero);
            CombatKinematicMotor zeroCeiling = CreateMotor(Fix64.Half, Fix64.Zero, Fix64.Zero);
            FixVector3 wallNormal = R(1000000, 0, 0);

            Assert.AreEqual(33, (int)zeroGround.ClassifyCollision(wallNormal));
            Assert.AreEqual(33, (int)zeroCeiling.ClassifyCollision(wallNormal));
        }

        [Test]
        public void Classification_CeilingAndWallKeepFrozenYFirstFlags()
        {
            CombatKinematicMotor motor = CreateMotor(Fix64.Half, Fix64.Half, Fix64.Zero);

            Assert.AreEqual(18, (int)motor.ClassifyCollision(R(600000, -800000, 0)));
            Assert.AreEqual(35, (int)motor.ClassifyCollision(R(1000000, -1, 0)));
        }

        [Test]
        public void WalkableProjection_MatchesCanonicalAndRawResidualOracles()
        {
            FixVector3 normal = R(600000, 800000, 0);

            FixVector3 canonical = CombatKinematicMotor.ProjectWalkableRemaining(
                R(-1000000, 0, 500000),
                normal);
            FixVector3 residual = CombatKinematicMotor.ProjectWalkableRemaining(
                R(-999999, 0, 0),
                normal);

            Assert.AreEqual(R(-640000, 480000, 500000), canonical);
            Assert.AreEqual(R(-639999, 480000, 0), residual);
            AssertRawNonEntering(canonical, normal);
            AssertRawNonEntering(residual, normal);
        }

        [Test]
        public void WalkableProjection_DualConstraintPreservesClassificationAndRawWitnessPostconditions()
        {
            FixVector3 witness = R(-648434, 729988, 0);
            FixVector3 classificationNormal = R(-664110, 747635, 0);
            FixVector3 value = R(152348, 968945, 0);

            FixVector3 resolved = CombatKinematicMotor.ProjectWalkableRemaining(
                value,
                classificationNormal,
                witness);

            Assert.AreNotEqual(FixVector3.Zero, resolved);
            AssertRawNonEntering(resolved, classificationNormal);
            AssertRawNonEntering(resolved, witness);
        }

        [Test]
        public void SteepProjection_MatchesCanonicalAndRawResidualOracles()
        {
            FixVector3 normal = R(600000, 800000, 0);

            FixVector3 canonical = CombatKinematicMotor.ProjectSteepRemaining(
                R(-1000000, 0, 500000),
                normal);
            FixVector3 residual = CombatKinematicMotor.ProjectSteepRemaining(
                R(-876543, 0, -765431),
                normal);

            Assert.AreEqual(R(0, 0, 500000), canonical);
            Assert.AreEqual(R(0, 0, -765431), residual);
            Assert.IsTrue(canonical.Y <= Fix64.Zero);
            Assert.IsTrue(residual.Y <= Fix64.Zero);
            AssertRawNonEntering(canonical, normal);
            AssertRawNonEntering(residual, normal);
        }

        [Test]
        public void SteepProjection_DualConstraintPreservesBothDotsAndNonPositiveY()
        {
            FixVector3 witness = R(-648434, 729988, 0);
            FixVector3 classificationNormal = R(-664110, 747635, 0);
            FixVector3 value = R(152348, 968945, 0);

            FixVector3 resolved = CombatKinematicMotor.ProjectSteepRemaining(
                value,
                classificationNormal,
                witness);

            Assert.IsTrue(resolved.Y <= Fix64.Zero);
            AssertRawNonEntering(resolved, classificationNormal);
            AssertRawNonEntering(resolved, witness);
        }

        [Test]
        public void SteepProjection_ThreeDimensionalDualConstraintPreservesLateralTangent()
        {
            FixVector3 witness = R(341007, 558526, -843066);
            FixVector3 classificationNormal = R(319522, 523337, -789951);
            FixVector3 value = R(531756, 456836, 1052497);

            FixVector3 resolved = CombatKinematicMotor.ProjectSteepRemaining(
                value,
                classificationNormal,
                witness);

            Assert.AreEqual(R(822849, 0, 332828), resolved);
            AssertRawNonEntering(resolved, classificationNormal);
            AssertRawNonEntering(resolved, witness);
            Assert.AreEqual(Fix64.Zero, resolved.Y);
        }

        [Test]
        public void SteepProjection_WithoutLateralTangentFailsClosedToZero()
        {
            FixVector3 result = CombatKinematicMotor.ProjectSteepRemaining(
                R(-1000000, 0, 0),
                R(600000, 800000, 0));

            Assert.AreEqual(FixVector3.Zero, result);
        }

        [Test]
        public void DirectedCorrection_ExpansionAndBinarySearchChooseSmallestSafeRaw()
        {
            FixVector3 normal = R(600000, 800000, 0);
            FixVector3 ordinary = R(-640000, 479999, 0);

            FixVector3 corrected = CombatKinematicMotor.CorrectRawNonEnteringForTests(
                ordinary,
                normal,
                normal,
                requireNonPositiveY: false,
                out long correctionRaw);

            Assert.AreEqual(2L, correctionRaw, "One raw does not change either normalized component; two is the minimum safe correction.");
            Assert.AreEqual(R(-639999, 480000, 0), corrected);
            AssertRawNonEntering(corrected, normal);
            FixVector3 oneRaw = ordinary + normal * Fix64.FromRaw(1);
            Assert.IsTrue(CombatKinematicMotor.TryRawDotNumerator(oneRaw, normal, out long oneRawDot));
            Assert.Less(oneRawDot, 0L);
        }

        [Test]
        public void DirectedCorrection_ArithmeticOverflowFailsClosedToZero()
        {
            FixVector3 overflow = R(long.MaxValue, long.MaxValue, long.MaxValue);

            Assert.AreEqual(
                FixVector3.Zero,
                CombatKinematicMotor.ProjectWalkableRemaining(overflow, overflow));
            Assert.AreEqual(
                FixVector3.Zero,
                CombatKinematicMotor.ProjectSteepRemaining(overflow, overflow));
        }

        [TestCase(258819, 965926)]
        [TestCase(500000, 866025)]
        public void RotatedObbMotorStep_ContinuousUphillHasPositiveMonotonicY(
            int sinRaw,
            int cosRaw)
        {
            CreateSlopeFixture(
                sinRaw,
                cosRaw,
                out CombatPhysicsWorld world,
                out CombatKinematicMotor motor,
                out CombatMotionState state,
                out FixVector3 normal);
            Fix64 previousY = state.Position.Y;

            for (int i = 0; i < 8; i++)
            {
                CombatMotionStepResult result = motor.Step(
                    world,
                    new CombatBodyId(1),
                    state,
                    new CombatMotionInput(R(1000000, 0, 0), false));

                Assert.IsTrue(result.AppliedDelta.Y > Fix64.Zero, "uphill Y at step " + i);
                Assert.IsTrue(result.State.Position.Y > previousY, "monotonic position Y at step " + i);
                Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.Grounded));
                Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.IterationLimit));
                AssertRawNonEntering(result.AppliedDelta, normal);
                previousY = result.State.Position.Y;
                state = result.State;
            }
        }

        [Test]
        public void RotatedObbMotorStep_WalkableTraverseHasNoUnrelatedY()
        {
            CreateSlopeFixture(
                500000,
                866025,
                out CombatPhysicsWorld world,
                out CombatKinematicMotor motor,
                out CombatMotionState state,
                out _);

            CombatMotionStepResult result = motor.Step(
                world,
                new CombatBodyId(1),
                state,
                new CombatMotionInput(R(0, 0, 1000000), false));

            Assert.AreEqual(Fix64.Zero, result.AppliedDelta.Y);
            Assert.AreEqual(state.Position.Y, result.State.Position.Y);
            Assert.IsTrue(result.AppliedDelta.Z > Fix64.Zero);
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.IterationLimit));
        }

        [Test]
        public void RotatedObbMotorStep_SteepFiftyDegreeNeverClimbsOrReenters()
        {
            CreateSlopeFixture(
                766044,
                642788,
                out CombatPhysicsWorld world,
                out CombatKinematicMotor motor,
                out CombatMotionState state,
                out FixVector3 normal);

            CombatMotionStepResult result = motor.Step(
                world,
                new CombatBodyId(1),
                state,
                new CombatMotionInput(R(1000000, 0, 0), false));

            Assert.IsTrue(result.AppliedDelta.Y <= Fix64.Zero);
            Assert.IsTrue(result.State.Position.Y <= state.Position.Y);
            Assert.IsTrue(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.SteepSlope));
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.Grounded));
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.IterationLimit));
            AssertRawNonEntering(result.AppliedDelta, normal);
        }

        [Test]
        public void RotatedObbMotorStep_InitialContactUsesFractionZeroWithoutIterationLimit()
        {
            CreateSlopeFixture(
                500000,
                866025,
                out CombatPhysicsWorld world,
                out CombatKinematicMotor motor,
                out CombatMotionState state,
                out FixVector3 normal);

            CombatMotionStepResult result = motor.Step(
                world,
                new CombatBodyId(1),
                state,
                new CombatMotionInput(R(1000000, 0, 0), false));

            Assert.Greater(result.CollisionCount, 0);
            Assert.AreEqual(Fix64.Zero, result.Collisions[0].Fraction);
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.IterationLimit));
            Assert.IsTrue(result.AppliedDelta.Y > Fix64.Zero);
            AssertRawNonEntering(result.AppliedDelta, normal);
        }

        [TestCase(258819, 965926)]
        [TestCase(500000, 866025)]
        public void FormalCapsuleSkinSmallInput_FractionZeroProjectionRechecksAsNonBlocking(
            int sinRaw,
            int cosRaw)
        {
            FixVector3 axisX = R(0, 0, -1000000);
            FixVector3 axisY = R(sinRaw, cosRaw, 0);
            FixVector3 axisZ = R(cosRaw, -sinRaw, 0);
            var collider = new CombatPhysicsObbCollider(
                new CombatBodyId(2),
                new CombatColliderId(1),
                1,
                FixVector3.Zero,
                R(1250000, 250000, 3000000),
                axisX,
                axisY,
                axisZ,
                CombatSurfaceMaterial.Rough);
            CombatPhysicsShape shape = CombatPhysicsShape.Obb(
                FixVector3.Zero,
                collider.HalfExtents,
                collider.AxisX,
                collider.AxisY,
                collider.AxisZ);
            var capsule = new CombatMotionCapsuleProxy(
                Fix64.FromRaw(450000),
                Fix64.FromRaw(1800000),
                FixVector3.Zero,
                Fix64.FromRaw(10000),
                0,
                CombatPhysicsLayerMask.All);
            Fix64 support = capsule.Radius
                + capsule.SegmentHalfLength * collider.AxisY.Y.Abs();
            FixVector3 topPoint = collider.AxisY * collider.HalfExtents.Y
                + collider.AxisZ * Fix64.FromRaw(1800000);
            FixVector3 start = topPoint + collider.AxisY * support;
            FixVector3 desired = R(-12500, 0, 0);

            bool hit = CombatMotionObbCast.TryCast(
                capsule,
                start,
                desired,
                shape,
                out Fix64 fraction,
                out _,
                out FixVector3 normal,
                out FixVector3 nonEnteringDirection,
                out CombatObbCastStatus status);

            Assert.IsTrue(hit);
            Assert.AreEqual(Fix64.Zero, fraction);
            Assert.IsTrue(
                status == CombatObbCastStatus.InitialTouching
                || status == CombatObbCastStatus.InitialOverlap);
            Assert.IsFalse(nonEnteringDirection.IsZero);
            FixVector3 resolved = CombatKinematicMotor.ProjectWalkableRemaining(
                desired,
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
                shape,
                out Fix64 recheckFraction,
                out _,
                out _,
                out _,
                out CombatObbCastStatus recheckStatus);
            Assert.IsFalse(reblocked);
            Assert.AreEqual(Fix64.Zero, recheckFraction);
            Assert.IsTrue(
                recheckStatus == CombatObbCastStatus.InitialTouching
                || recheckStatus == CombatObbCastStatus.InitialOverlap);

            FixVector3 roundedStart = new FixVector3(
                Fix64.FromRaw(RoundToMillimeterRaw(start.X.RawValue)),
                Fix64.FromRaw(
                    RoundToMillimeterRaw(start.Y.RawValue - 900000L)
                    + 900000L),
                Fix64.FromRaw(RoundToMillimeterRaw(start.Z.RawValue)));
            bool roundedHit = CombatMotionObbCast.TryCast(
                capsule,
                roundedStart,
                desired,
                shape,
                out Fix64 roundedFraction,
                out _,
                out FixVector3 roundedNormal,
                out FixVector3 roundedNonEnteringDirection,
                out CombatObbCastStatus roundedStatus);
            Assert.IsTrue(roundedHit);
            Assert.IsFalse(roundedNonEnteringDirection.IsZero);
            if (sinRaw == 500000)
            {
                Assert.IsTrue(
                    roundedStatus == CombatObbCastStatus.Hit
                    || roundedStatus == CombatObbCastStatus.PrecisionLimitConservativeHit
                    || roundedStatus == CombatObbCastStatus.IterationLimitConservativeHit,
                    "30-degree millimeter authority start must exercise the separated hit path.");
            }

            FixVector3 roundedHitPosition = roundedStart + desired * roundedFraction;
            FixVector3 roundedAfterHit = desired * (Fix64.One - roundedFraction);
            FixVector3 roundedResolved = CombatKinematicMotor.ProjectWalkableRemaining(
                roundedAfterHit,
                roundedNormal,
                roundedNonEnteringDirection);
            Assert.IsTrue(CombatKinematicMotor.TryRawDotNumerator(
                roundedResolved,
                roundedNonEnteringDirection,
                out long roundedRawDot));
            Assert.GreaterOrEqual(roundedRawDot, 0L);
            Assert.IsTrue(CombatMotionObbCast.TryInspectSeparatedStartForTesting(
                capsule,
                roundedHitPosition,
                roundedResolved,
                shape,
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
            if (sinRaw == 500000)
            {
                string normalizedProbe =
                    " normalizedDirection=" + probeNormalizedDirection
                    + " normalizedLengthCeil=" + probeNormalizedLengthCeil
                    + " normalizedSupportGapLowerRaw2=" + probeNormalizedSupportGapLowerRaw2
                    + " normalizedThresholdDirectionUpperRaw2=" + probeNormalizedThresholdDirectionUpperRaw2
                    + " normalizedCurrentExpandedGapLowerRaw2=" + probeNormalizedCurrentExpandedGapLowerRaw2
                    + " deltaDotNormalizedRaw2=" + probeDeltaDotNormalizedRaw2
                    + " shapeAxisX=" + shape.AxisX
                    + " shapeAxisY=" + shape.AxisY
                    + " shapeAxisZ=" + shape.AxisZ
                    + " corrected=" + roundedResolved;
                Assert.LessOrEqual(
                    probeNormalizedCurrentExpandedGapLowerRaw2,
                    0L,
                    "The formal 30-degree millimeter seam must exercise the OBB-axis proof, not the normalized witness proof." + normalizedProbe);
                Assert.GreaterOrEqual(probeDeltaDotNormalizedRaw2, 0L, normalizedProbe);
                FixVector3[] shapeDirections =
                {
                    shape.AxisX,
                    -shape.AxisX,
                    shape.AxisY,
                    -shape.AxisY,
                    shape.AxisZ,
                    -shape.AxisZ,
                };
                for (int directionIndex = 0;
                     directionIndex < shapeDirections.Length;
                     directionIndex++)
                {
                    Assert.IsTrue(CombatMotionObbCast.TryInspectExpandedDirectionForTesting(
                        capsule,
                        roundedHitPosition,
                        roundedResolved,
                        shape,
                        shapeDirections[directionIndex],
                        out long shapeLengthCeil,
                        out long shapeSupportGapLowerRaw2,
                        out long shapeThresholdDirectionUpperRaw2,
                        out long shapeExpandedGapLowerRaw2,
                        out long shapeDeltaDotRaw2));
                    TestContext.WriteLine(
                        "formal30 shapeDirection[" + directionIndex + "]="
                        + shapeDirections[directionIndex]
                        + " lengthCeil=" + shapeLengthCeil
                        + " supportGapLowerRaw2=" + shapeSupportGapLowerRaw2
                        + " thresholdDirectionUpperRaw2=" + shapeThresholdDirectionUpperRaw2
                        + " expandedGapLowerRaw2=" + shapeExpandedGapLowerRaw2
                        + " deltaDotRaw2=" + shapeDeltaDotRaw2);
                    if (directionIndex == 2)
                    {
                        Assert.Greater(shapeExpandedGapLowerRaw2, 0L);
                        Assert.GreaterOrEqual(shapeDeltaDotRaw2, 0L);
                    }
                }
            }
            bool roundedReblocked = CombatMotionObbCast.TryCast(
                capsule,
                roundedHitPosition,
                roundedResolved,
                shape,
                out Fix64 roundedRecheckFraction,
                out Fix64 roundedRecheckDistance,
                out FixVector3 roundedRecheckNormal,
                out FixVector3 roundedRecheckDirection,
                out CombatObbCastStatus roundedRecheckStatus);
            Assert.IsFalse(
                roundedReblocked,
                "body=2 collider=1 status=" + roundedRecheckStatus
                + " fractionRaw=" + roundedRecheckFraction.RawValue
                + " distanceRaw=" + roundedRecheckDistance.RawValue
                + " normal=" + roundedRecheckNormal
                + " direction=" + roundedRecheckDirection
                + " witness=" + probeWitness
                + " distanceSquaredRaw2=" + probeDistanceSquaredRaw2
                + " radiusRaw=" + capsule.Radius.RawValue
                + " thresholdRaw=" + CombatMotionObbCast.ContactThresholdRaw
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
                + " deltaDotNormalizedRaw2=" + probeDeltaDotNormalizedRaw2
                + " axisX=" + collider.AxisX
                + " axisY=" + collider.AxisY
                + " axisZ=" + collider.AxisZ
                + " half=" + collider.HalfExtents
                + " corrected=" + roundedResolved);
            Assert.AreEqual(Fix64.Zero, roundedRecheckFraction);
        }

        [Test]
        public void SupportOnly_PreGravityBranchSuppressesOnlyEligibleMainDelta()
        {
            CombatKinematicMotor snapMotor = CreateMotor(Fix64.Half, Fix64.Half, Fix64.FromRatio(1, 10));
            CombatMotionState grounded = State(Fix64.Zero, grounded: true);

            CombatMotionStepResult supportOnly = Step(snapMotor, grounded, CombatMotionInput.None, null, default);
            CombatMotionStepResult suppressed = Step(
                snapMotor,
                grounded,
                CombatMotionInput.None,
                null,
                new CombatMotionStepOptions(suppressGroundSnap: true));
            CombatMotionStepResult positiveImpact = Step(
                snapMotor,
                grounded,
                CombatMotionInput.None,
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                default);
            CombatMotionStepResult horizontal = Step(
                snapMotor,
                grounded,
                new CombatMotionInput(new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), false),
                null,
                default);
            CombatMotionStepResult positiveState = Step(
                snapMotor,
                State(Fix64.One, grounded: true),
                CombatMotionInput.None,
                null,
                default);
            CombatMotionStepResult snapDisabled = Step(
                CreateMotor(Fix64.Half, Fix64.Half, Fix64.Zero),
                grounded,
                CombatMotionInput.None,
                null,
                default);

            Assert.AreEqual(FixVector3.Zero, supportOnly.DesiredDelta);
            Assert.AreEqual(FixVector3.Zero, supportOnly.AppliedDelta);
            Assert.AreEqual(CombatMotionCollisionFlags.None, supportOnly.CollisionFlags);
            Assert.IsFalse(supportOnly.State.Grounded);
            Assert.IsFalse(HasFlag(supportOnly.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
            Assert.IsTrue(suppressed.DesiredDelta.Y < Fix64.Zero);
            Assert.IsTrue(positiveImpact.DesiredDelta.Y < Fix64.Zero);
            Assert.AreNotEqual(Fix64.Zero, horizontal.DesiredDelta.X);
            Assert.IsTrue(positiveState.DesiredDelta.Y > Fix64.Zero);
            Assert.IsTrue(snapDisabled.DesiredDelta.Y < Fix64.Zero);
        }

        [Test]
        public void MultiContactFlags_OrWalkableAndSteepWithoutForgingSnap()
        {
            CombatKinematicMotor motor = CreateMotor(Fix64.FromRaw(750000), Fix64.Half, Fix64.Zero);
            CombatMotionCollisionFlags walkable = motor.ClassifyCollision(R(600000, 800000, 0));
            CombatMotionCollisionFlags steep = motor.ClassifyCollision(R(800000, 600000, 0));
            CombatMotionCollisionFlags combined = walkable | steep;

            Assert.IsTrue(HasFlag(combined, CombatMotionCollisionFlags.Grounded));
            Assert.IsTrue(HasFlag(combined, CombatMotionCollisionFlags.SteepSlope));
            Assert.IsTrue(HasFlag(combined, CombatMotionCollisionFlags.Wall));
            Assert.IsFalse(HasFlag(combined, CombatMotionCollisionFlags.GroundSnap));
        }

        [Test]
        public void FlatAabbMaterialAndLastNormalRegression_RemainsGrounded()
        {
            var world = new CombatPhysicsWorld();
            var characterBody = new CombatBodyId(1);
            var groundBody = new CombatBodyId(2);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(1),
                characterBody,
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero)));
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), groundBody, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                groundBody,
                new CombatColliderId(1),
                1,
                new FixVector3(-Fix64.FromInt(10), -Fix64.One, -Fix64.FromInt(10)),
                new FixVector3(Fix64.FromInt(10), Fix64.Zero, Fix64.FromInt(10)),
                CombatSurfaceMaterial.Rough));
            CombatKinematicMotor motor = CreateMotor(Fix64.Half, Fix64.Half, Fix64.Zero);
            CombatMotionState state = new CombatMotionState(
                CombatFrame.Zero,
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                FixVector3.Zero,
                grounded: false,
                FixVector3.Zero,
                CombatMotionCollisionFlags.None);

            CombatMotionStepResult result = motor.Step(
                world,
                characterBody,
                state,
                CombatMotionInput.None);

            Assert.IsTrue(result.State.Grounded);
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, result.SurfaceMaterialKind);
            Assert.AreEqual(R(0, 1000000, 0), result.State.LastCollisionNormal);
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.StepUp));
            Assert.IsFalse(HasFlag(result.CollisionFlags, CombatMotionCollisionFlags.GroundSnap));
        }

        private static CombatMotionStepResult Step(
            CombatKinematicMotor motor,
            CombatMotionState state,
            CombatMotionInput input,
            FixVector3? impactVelocity,
            CombatMotionStepOptions options)
        {
            return motor.Step(
                null,
                CombatBodyId.None,
                state,
                input,
                impactVelocity,
                null,
                null,
                options);
        }

        private static void CreateSlopeFixture(
            long sinRaw,
            long cosRaw,
            out CombatPhysicsWorld world,
            out CombatKinematicMotor motor,
            out CombatMotionState state,
            out FixVector3 normal)
        {
            Fix64 sine = Fix64.FromRaw(sinRaw);
            Fix64 cosine = Fix64.FromRaw(cosRaw);
            FixVector3 tangent = new FixVector3(cosine, sine, Fix64.Zero);
            normal = new FixVector3(-sine, cosine, Fix64.Zero);
            FixVector3 axisZ = R(0, 0, 1000000);
            var slopeBody = new CombatBodyId(2);
            var slopeCollider = new CombatPhysicsObbCollider(
                slopeBody,
                new CombatColliderId(1),
                1,
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(10), Fix64.Half, Fix64.FromInt(5)),
                tangent,
                normal,
                axisZ,
                CombatSurfaceMaterial.Rough,
                priority: 0);
            tangent = slopeCollider.AxisX;
            normal = slopeCollider.AxisY;
            sine = tangent.Y;
            cosine = normal.Y;
            Fix64 x = Fix64.FromInt(-2);
            Fix64 capsuleSupport = Fix64.Half + Fix64.Half * cosine;
            Fix64 planeDistance = Fix64.Half + capsuleSupport;
            Fix64 y = (planeDistance + sine * x) / cosine;
            FixVector3 start = new FixVector3(x, y, Fix64.Zero);

            world = new CombatPhysicsWorld();
            var characterBody = new CombatBodyId(1);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(1), characterBody, start));
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), slopeBody, FixVector3.Zero));
            world.UpsertObbCollider(slopeCollider);
            motor = new CombatKinematicMotor(new CombatMotionConfig(
                CombatMotionConfig.DefaultTicksPerSecond,
                Fix64.FromInt(5),
                Fix64.Zero,
                Fix64.FromInt(10),
                Fix64.Zero,
                Fix64.FromRatio(1, 1000),
                Fix64.FromRaw(707107),
                Fix64.Half,
                CombatMotionConfig.DefaultMaxCollisionIterations,
                HalfExtents,
                CombatPhysicsLayerMask.All,
                Fix64.Zero,
                CombatMotionConfig.DefaultMinElasticImpactSpeed,
                Fix64.Zero,
                Fix64.Zero));
            state = new CombatMotionState(
                CombatFrame.Zero,
                start,
                FixVector3.Zero,
                grounded: true,
                normal,
                CombatMotionCollisionFlags.Grounded);
        }

        private static CombatMotionState State(Fix64 verticalVelocity, bool grounded)
        {
            return new CombatMotionState(
                CombatFrame.Zero,
                FixVector3.Zero,
                new FixVector3(Fix64.Zero, verticalVelocity, Fix64.Zero),
                grounded,
                grounded ? R(0, 1000000, 0) : FixVector3.Zero,
                grounded ? CombatMotionCollisionFlags.Grounded : CombatMotionCollisionFlags.None);
        }

        private static CombatKinematicMotor CreateMotor(
            Fix64 groundThreshold,
            Fix64 ceilingThreshold,
            Fix64 snapDistance)
        {
            return new CombatKinematicMotor(new CombatMotionConfig(
                CombatMotionConfig.DefaultTicksPerSecond,
                Fix64.FromInt(5),
                Fix64.FromInt(-30),
                Fix64.FromInt(10),
                Fix64.FromInt(-50),
                Fix64.FromRatio(1, 1000),
                groundThreshold,
                ceilingThreshold,
                CombatMotionConfig.DefaultMaxCollisionIterations,
                HalfExtents,
                CombatPhysicsLayerMask.All,
                Fix64.Zero,
                CombatMotionConfig.DefaultMinElasticImpactSpeed,
                Fix64.FromInt(30),
                snapDistance));
        }

        private static FixVector3 R(long x, long y, long z)
        {
            return new FixVector3(Fix64.FromRaw(x), Fix64.FromRaw(y), Fix64.FromRaw(z));
        }

        private static long RoundToMillimeterRaw(long raw)
        {
            return raw >= 0L
                ? ((raw + 500L) / 1000L) * 1000L
                : ((raw - 500L) / 1000L) * 1000L;
        }

        private static void AssertRawNonEntering(FixVector3 value, FixVector3 normal)
        {
            Assert.IsTrue(CombatKinematicMotor.TryRawDotNumerator(value, normal, out long numerator));
            Assert.GreaterOrEqual(numerator, 0L);
        }

        private static bool HasFlag(CombatMotionCollisionFlags value, CombatMotionCollisionFlags flag)
        {
            return (value & flag) == flag;
        }
    }
}
