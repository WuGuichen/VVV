using NUnit.Framework;
using MxFramework.CharacterControl;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalBlackHoleRulesTests
    {
        // Review-proposed trial values, not a published configuration or global physics defaults.
        private static TacticalBlackHolePullParameters Parameters => new TacticalBlackHolePullParameters(3000, 250, 3000, 80000);
        private static TacticalBlackHoleAcceleration Sample(long x = 1000, long y = 0, long z = 0,
            int mass = 80000, int pushScale = 1000, bool canPush = true)
        {
            Assert.IsTrue(TacticalBlackHoleRules.TrySample(Parameters, x, y, z, mass, pushScale, canPush, 6000, out var value));
            return value;
        }

        [Test]
        public void LifetimeBeginsOnTheNextSimulationStepAndIncludesTheFollowingTurnLastDamageFrame()
        {
            Assert.IsTrue(TacticalBlackHoleLifetime.TryCreate(7, 2, 100, out var lifetime));
            Assert.IsFalse(lifetime.IsActive(7, 2, 100));
            Assert.IsTrue(lifetime.IsActive(7, 2, 101));
            Assert.IsTrue(lifetime.IsActive(7, 3, 102));
            Assert.IsTrue(lifetime.IsActive(7, 3, 9000));
            Assert.IsFalse(lifetime.IsActive(7, 4, 9001));
            Assert.IsFalse(lifetime.IsActive(8, 2, 101));
            Assert.IsFalse(lifetime.IsActive(7, 1, 101));
            Assert.IsFalse(default(TacticalBlackHoleLifetime).IsActive(7, 1, 101));
        }

        [Test]
        public void LifetimeCannotOverflowTurnOrFrameIdentity()
        {
            Assert.IsFalse(TacticalBlackHoleLifetime.TryCreate(0, 2, 100, out _));
            Assert.IsFalse(TacticalBlackHoleLifetime.TryCreate(7, 0, 100, out _));
            Assert.IsFalse(TacticalBlackHoleLifetime.TryCreate(7, ulong.MaxValue, 100, out _));
            Assert.IsFalse(TacticalBlackHoleLifetime.TryCreate(7, 2, long.MaxValue, out _));
            Assert.IsFalse(TacticalBlackHoleLifetime.TryCreate(7, 2, -1, out _));
        }

        [TestCase(80000, 1000, 3000)] [TestCase(160000, 1000, 1500)]
        [TestCase(40000, 1000, 6000)] [TestCase(80000, 500, 1500)] [TestCase(1, int.MaxValue, 6000)]
        public void PullUsesEffectiveMassAndPushScaleWithABoundedRequest(int mass, int pushScale, int expected)
        { var force = Sample(mass: mass, pushScale: pushScale); Assert.AreEqual(expected, force.X); Assert.AreEqual(0, force.Z); }

        [Test]
        public void ImmunityAndZeroScaleProduceNoMotionAndDoNotCreateDamageOrControlRequests()
        { Assert.AreEqual(0, Sample(canPush: false).X); Assert.AreEqual(0, Sample(pushScale: 0).X); }

        [TestCase(0, 0, 0)] [TestCase(250, 0, 0)] [TestCase(3001, 0, 0)]
        [TestCase(3000, 1, 0)] [TestCase(0, 3001, 1000)] [TestCase(long.MinValue, 0, long.MaxValue)]
        public void CenterOutsideAndUnrepresentableDisplacementsNeverProduceAnInvalidDirection(long x, long y, long z)
        { var force = Sample(x, y, z); Assert.AreEqual(0, force.X); Assert.AreEqual(0, force.Z); }

        [Test]
        public void PullPointsTowardTheFixedCenterAndLeavesVerticalMotionToTheExistingMotor()
        {
            var force = Sample(-1000, 1000, 1000);
            Assert.Less(force.X, 0); Assert.Greater(force.Z, 0); Assert.AreEqual(-force.X, force.Z);
            Assert.LessOrEqual((long)force.X * force.X + (long)force.Z * force.Z, 3000L * 3000);
            Assert.AreEqual(3000, Sample(3000).X, "Range boundary is inclusive.");
        }

        [TestCase(0, 6000)] [TestCase(-4000, 6000)] [TestCase(1950, 3000)]
        [TestCase(1999, 60)] [TestCase(2000, 0)] [TestCase(4000, 0)]
        public void CombinedFieldsCannotPushPastTheRadialCapInOneStep(int radialVelocity, int expected)
        {
            Assert.IsTrue(TacticalBlackHoleRules.TryLimitCombined(12000, 0, radialVelocity, 7000, 6000, 2000, 60, out var force));
            Assert.AreEqual(expected, force.X); Assert.AreEqual(0, force.Z, "Tangential velocity is not clamped by the field.");
            if (radialVelocity < 2000) Assert.LessOrEqual(radialVelocity + force.X / 60, 2000);
        }

        [Test]
        public void OpposingFieldsCancelBeforeCappingAndDiagonalFieldsShareOneMagnitudeBudget()
        {
            var east = Sample(); var west = Sample(-1000);
            Assert.IsTrue(TacticalBlackHoleRules.TryLimitCombined((long)east.X + west.X, (long)east.Z + west.Z,
                1000, 1000, 6000, 2000, 60, out var cancelled));
            Assert.AreEqual(0, cancelled.X); Assert.AreEqual(0, cancelled.Z);
            Assert.IsTrue(TacticalBlackHoleRules.TryLimitCombined(12000, 12000, 0, 0, 6000, 2000, 60, out var diagonal));
            Assert.AreEqual(diagonal.X, diagonal.Z);
            Assert.LessOrEqual((long)diagonal.X * diagonal.X + (long)diagonal.Z * diagonal.Z, 6000L * 6000);
        }

        [Test]
        public void ExtremeSummedForcesCannotOverflowBeforeLimiting()
        {
            Assert.IsTrue(TacticalBlackHoleRules.TryLimitCombined(int.MinValue, int.MinValue, 0, 0, 6000, 2000, 60, out var force));
            Assert.Less(force.X, 0); Assert.AreEqual(force.X, force.Z);
            Assert.IsFalse(TacticalBlackHoleRules.TryLimitCombined(long.MaxValue, 0, 0, 0, 6000, 2000, 60, out _));
            Assert.IsFalse(TacticalBlackHoleRules.TryLimitCombined(10, 0, 0, 0, 6000, 2000, 0, out _));
            Assert.IsFalse(TacticalBlackHoleRules.TrySample(Parameters, 1000, 0, 0, 0, 1000, true, 6000, out _));
        }

        [TestCase(12000, 12)] [TestCase(-12000, 12)] [TestCase(12000, -12)] [TestCase(-12000, -12)]
        [TestCase(12, 12000)] [TestCase(-12, 12000)] [TestCase(12, -12000)] [TestCase(-12, -12000)]
        public void NearAxisFinalIntegerVectorRemainsInsideTheSquaredAccelerationBound(int x, int z)
        {
            Assert.IsTrue(TacticalBlackHoleRules.TryLimitCombined(x, z, 0, 0, 6000, 2000, 60, out var force));
            Assert.LessOrEqual((long)force.X * force.X + (long)force.Z * force.Z, 36000000);
            Assert.AreEqual(System.Math.Sign(x), System.Math.Sign(force.X));
            Assert.AreEqual(System.Math.Sign(z), System.Math.Sign(force.Z));
        }

        [TestCase(1, 1)] [TestCase(-1, 1)] [TestCase(1, -1)] [TestCase(-1, -1)]
        public void RadialLimitUsesRealMotorRoundingIncludingNegativeHalfTies(int signX, int signZ)
        {
            Assert.IsTrue(TacticalBlackHoleRules.TryLimitCombined(6000 * signX, 8000 * signZ,
                5 * signX, 2496 * signZ, 6000, 2000, 60, out var force));
            var result = SolveMotion(force, 5 * signX, 2496 * signZ);
            Assert.LessOrEqual(result.HorizontalVelocityX * signX * 6L + result.HorizontalVelocityY * signZ * 8L, 20000);
            var unsafeResult = SolveMotion(new TacticalBlackHoleAcceleration(36 * signX, 48 * signZ), 5 * signX, 2496 * signZ);
            Assert.Greater(unsafeResult.HorizontalVelocityX * signX * 6L + unsafeResult.HorizontalVelocityY * signZ * 8L, 20000,
                "The real solver must reproduce the reviewed counterexample before accepting the fix.");
        }

        internal static CharacterKinematicMotionPrePhysicsResult SolveMotion(TacticalBlackHoleAcceleration force, int vx, int vz)
        {
            var frame = new CharacterMotionRequestFrame { HasExternalAcceleration = true, ExternalAccelerationX = force.X,
                ExternalAccelerationZ = force.Z, ExternalAccelerationBypassesGroundDamping = true };
            var input = new CharacterKinematicMotionInput(default, frame, default, tickRate: 60,
                desiredMoveX: 0, desiredMoveY: 0, moveInputMagnitude: 0,
                currentHorizontalVelocityX: vx, currentHorizontalVelocityY: vz, currentVerticalVelocity: 0,
                wasGrounded: true, jumpPressed: false, jumpReleased: false, jumpBufferFramesRemaining: 0, coyoteTimeFramesRemaining: 0,
                actionMovementLocked: false, actionMovementModeId: 0, actionJumpImpulseYMillimetersPerSecond: 0,
                actionMovementControlScaleMilli: 1000, actionRotationControlScaleMilli: 1000, surfaceBounceSpeedCapYMillimetersPerSecond: 0,
                hasPhysicsRuntime: false, contactVelocityX: 0, contactVelocityY: 0, currentGroundSurfaceMaterial: default,
                movementModeIdleId: 0, movementModeAirborneId: 1, movementModeControlLockedId: 2, moveFacingEpsilonMilli: 0,
                contactVelocityEpsilonMillimetersPerSecond: 0, inputAxisScale: 1000, scaleFactorScale: 1000, defaultTickRate: 60,
                apexVelocityEpsilonMillimetersPerSecond: 0);
            return CharacterKinematicMotionSolver.Default.SolvePrePhysics(input);
        }
    }
}
