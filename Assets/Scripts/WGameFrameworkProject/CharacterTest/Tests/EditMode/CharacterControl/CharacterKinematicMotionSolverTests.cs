using MxFramework.CharacterControl;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterKinematicMotionSolverTests
    {
        private const int IdleMode = 0;
        private const int WalkMode = 1;
        private const int RunMode = 2;
        private const int AirborneMode = 9;
        private const int ControlLockedMode = 10;
        private const int TickRate = 60;
        private const int InputScale = 1000;
        private const int Scale = 1000;
        private static readonly CharacterKinematicMotionSolver Solver = CharacterKinematicMotionSolver.Default;

        [Test]
        public void SolvePrePhysics_GroundedInputAcceleratesTowardRunTarget()
        {
            CharacterKinematicMotionPrePhysicsResult result = Solver.SolvePrePhysics(CreateInput(
                desiredMoveX: InputScale,
                moveInputMagnitude: InputScale,
                wasGrounded: true));

            Assert.AreEqual(4000, result.TargetVelocityX);
            Assert.AreEqual(0, result.TargetVelocityY);
            Assert.AreEqual(400, result.HorizontalVelocityX);
            Assert.AreEqual(0, result.HorizontalVelocityY);
            Assert.AreEqual(4000, result.DesiredStateVelocityX);
        }

        [Test]
        public void SolvePrePhysics_AirControlScalesAcceleration()
        {
            CharacterKinematicMotionPrePhysicsResult result = Solver.SolvePrePhysics(CreateInput(
                desiredMoveX: InputScale,
                moveInputMagnitude: InputScale,
                wasGrounded: false));

            Assert.AreEqual(140, result.HorizontalVelocityX);
            Assert.AreEqual(AirborneMode, result.MovementModeId);
        }

        [Test]
        public void SolvePrePhysics_ControlScaleZeroBlocksActiveMoveButNotRotation()
        {
            CharacterMotionRequestFrame frame = default;
            frame.HasMovementControlScale = true;
            frame.HasRotationControlScale = true;
            frame.MovementControlScaleMilli = 0;
            frame.RotationControlScaleMilli = 250;

            CharacterKinematicMotionPrePhysicsResult result = Solver.SolvePrePhysics(CreateInput(
                requestFrame: frame,
                desiredMoveX: InputScale,
                moveInputMagnitude: InputScale,
                wasGrounded: true));

            Assert.AreEqual(0, result.TargetVelocityX);
            Assert.AreEqual(0, result.HorizontalVelocityX);
            Assert.AreEqual(0, result.MovementControlScaleMilli);
            Assert.AreEqual(250, result.RotationControlScaleMilli);
            Assert.IsFalse(result.MovementLocked);
        }

        [Test]
        public void SolvePrePhysics_JumpImpulseAppliesBeforeApexGravity()
        {
            CharacterKinematicMotionPrePhysicsResult result = Solver.SolvePrePhysics(CreateInput(
                wasGrounded: true,
                actionJumpImpulseYMillimetersPerSecond: 8000));

            Assert.IsTrue(result.JumpImpulseApplied);
            Assert.AreEqual(8000, result.ImpactVerticalVelocity);
            Assert.AreEqual(7500, result.VerticalVelocity);
            Assert.AreEqual(0, result.JumpBufferFrames);
            Assert.AreEqual(0, result.CoyoteTimeFrames);
            Assert.AreEqual(AirborneMode, result.MovementModeId);
            Assert.IsFalse(result.WasGroundedForPhysics);
        }

        [Test]
        public void SolvePrePhysics_JumpReleaseCutsUpwardVelocityBeforeGravity()
        {
            CharacterKinematicMotionPrePhysicsResult result = Solver.SolvePrePhysics(CreateInput(
                currentVerticalVelocity: 8000,
                wasGrounded: false,
                jumpReleased: true));

            Assert.AreEqual(3600, result.ImpactVerticalVelocity);
            Assert.AreEqual(3100, result.VerticalVelocity);
        }

        [Test]
        public void SolvePrePhysics_VerticalVelocityAndIgnoreGravityCompose()
        {
            CharacterMotionRequestFrame frame = default;
            frame.HasVerticalVelocityOverride = true;
            frame.VerticalVelocityOverrideY = 2500;
            frame.IgnoreGravity = true;

            CharacterKinematicMotionPrePhysicsResult held = Solver.SolvePrePhysics(CreateInput(
                requestFrame: frame,
                currentVerticalVelocity: -4000,
                wasGrounded: false));
            Assert.AreEqual(2500, held.VerticalVelocity);

            frame.IgnoreGravity = false;
            CharacterKinematicMotionPrePhysicsResult released = Solver.SolvePrePhysics(CreateInput(
                requestFrame: frame,
                currentVerticalVelocity: -4000,
                wasGrounded: false));
            Assert.AreEqual(2000, released.VerticalVelocity);
        }

        [Test]
        public void SolvePrePhysics_SurfaceTractionCapsGroundAcceleration()
        {
            var slick = new CombatSurfaceMaterial(
                CombatSurfaceMaterialKind.HardSlick,
                Fix64.FromInt(3),
                Fix64.Zero);
            CharacterKinematicMotionPrePhysicsResult result = Solver.SolvePrePhysics(CreateInput(
                desiredMoveX: InputScale,
                moveInputMagnitude: InputScale,
                wasGrounded: true,
                groundSurfaceMaterial: slick));

            Assert.AreEqual(50, result.HorizontalVelocityX);
            Assert.IsTrue(result.CharacterSurfaceTraction);
            Assert.IsFalse(result.ApplyGroundSurfaceFriction);
        }

        [Test]
        public void SolvePrePhysics_RootMotionVelocityComposesWithSimulatedVelocity()
        {
            CharacterMotionRequestFrame frame = default;
            frame.HasRootMotion = true;
            frame.RootMotionDeltaX = 12;
            frame.RootMotionDeltaY = 3;
            frame.RootMotionDeltaZ = -6;

            CharacterKinematicMotionPrePhysicsResult result = Solver.SolvePrePhysics(CreateInput(
                requestFrame: frame,
                currentHorizontalVelocityX: 100,
                currentHorizontalVelocityY: -200,
                currentVerticalVelocity: 0,
                wasGrounded: true));

            Assert.AreEqual(720, result.RootMotionHorizontalVelocityX);
            Assert.AreEqual(-360, result.RootMotionHorizontalVelocityY);
            Assert.AreEqual(135, result.RootMotionVerticalVelocity);
            Assert.AreEqual(result.HorizontalVelocityX + 720, result.SimulatedHorizontalVelocityX);
            Assert.AreEqual(result.HorizontalVelocityY - 360, result.SimulatedHorizontalVelocityY);
            Assert.AreEqual(result.VerticalVelocity + 135, result.SimulatedVerticalVelocity);
        }

        [Test]
        public void SolvePrePhysics_HorizontalVelocityOverrideReplacesOldVelocityAndHorizontalRootMotion()
        {
            CharacterMotionRequestFrame frame = default;
            frame.HasHorizontalVelocityOverride = true;
            frame.HorizontalVelocityOverrideX = -1200;
            frame.HorizontalVelocityOverrideZ = 300;
            frame.HasRootMotion = true;
            frame.RootMotionDeltaX = 12;
            frame.RootMotionDeltaY = 3;
            frame.RootMotionDeltaZ = -6;

            CharacterKinematicMotionPrePhysicsResult result = Solver.SolvePrePhysics(CreateInput(
                requestFrame: frame,
                currentHorizontalVelocityX: 9000,
                currentHorizontalVelocityY: -4000,
                currentVerticalVelocity: 0,
                wasGrounded: true));

            Assert.AreEqual(-1200, result.HorizontalVelocityX);
            Assert.AreEqual(300, result.HorizontalVelocityY);
            Assert.AreEqual(-1200, result.SimulatedHorizontalVelocityX);
            Assert.AreEqual(300, result.SimulatedHorizontalVelocityY);
            Assert.AreEqual(0, result.RootMotionHorizontalVelocityX);
            Assert.AreEqual(0, result.RootMotionHorizontalVelocityY);
            Assert.AreEqual(135, result.RootMotionVerticalVelocity);
            Assert.AreEqual(result.VerticalVelocity + 135, result.SimulatedVerticalVelocity);
        }

        [Test]
        public void SolvePrePhysics_UpwardRootSampleRemainsPositiveAtExtremeGravity()
        {
            CharacterMotionRequestFrame frame = default;
            frame.HasRootMotion = true;
            frame.RootMotionDeltaY = 200;

            CharacterKinematicMotionPrePhysicsResult noGravity = Solver.SolvePrePhysics(CreateInput(
                requestFrame: frame,
                currentVerticalVelocity: -10000,
                wasGrounded: false,
                gravityMillimetersPerSecondSquared: 0));
            Assert.AreEqual(12000, noGravity.SimulatedVerticalVelocity);

            CharacterKinematicMotionPrePhysicsResult extremeGravity = Solver.SolvePrePhysics(CreateInput(
                requestFrame: frame,
                currentVerticalVelocity: -10000,
                wasGrounded: false,
                gravityMillimetersPerSecondSquared: -9000000));
            Assert.AreEqual(120, extremeGravity.SimulatedVerticalVelocity);
            Assert.Greater(extremeGravity.SimulatedVerticalVelocity, 0);

            frame.IgnoreGravity = true;
            CharacterKinematicMotionPrePhysicsResult ignored = Solver.SolvePrePhysics(CreateInput(
                requestFrame: frame,
                wasGrounded: false,
                gravityMillimetersPerSecondSquared: -9000000));
            Assert.AreEqual(12000, ignored.SimulatedVerticalVelocity);
        }

        [Test]
        public void CompleteAfterPhysics_ResolvesYawAndBlendFromFinalVelocity()
        {
            CharacterKinematicMotionInput input = CreateInput(
                desiredMoveX: InputScale,
                moveInputMagnitude: InputScale,
                wasGrounded: true,
                isCommandDriven: true);
            CharacterKinematicMotionPrePhysicsResult prePhysics = Solver.SolvePrePhysics(in input);

            CharacterKinematicMotionFinalResult result = Solver.CompleteAfterPhysics(
                in input,
                in prePhysics,
                grounded: true,
                horizontalVelocityX: 4000,
                horizontalVelocityY: 0,
                verticalVelocity: 0,
                surfaceBounceSpeedCapY: 0,
                facingResolver: CharacterMotionDefaultFacingResolver.Instance);

            Assert.AreEqual(12000, result.YawMilliDegrees);
            Assert.AreEqual(1000, result.BlendX);
            Assert.AreEqual(0, result.BlendY);
            Assert.AreEqual(RunMode, result.MovementModeId);
        }

        [Test]
        public void CompleteAfterPhysics_ContactVelocityMovesWithoutChangingIntentFacing()
        {
            CharacterKinematicMotionInput input = CreateInput(
                wasGrounded: true,
                hasPhysicsRuntime: true,
                contactVelocityX: 4000,
                isCommandDriven: true);
            CharacterKinematicMotionPrePhysicsResult prePhysics = Solver.SolvePrePhysics(in input);
            Assert.AreEqual(4000, prePhysics.HorizontalVelocityX);
            Assert.AreEqual(0, prePhysics.HorizontalVelocityY);

            CharacterKinematicMotionFinalResult result = Solver.CompleteAfterPhysics(
                in input,
                in prePhysics,
                grounded: true,
                horizontalVelocityX: prePhysics.HorizontalVelocityX,
                horizontalVelocityY: prePhysics.HorizontalVelocityY,
                verticalVelocity: 0,
                surfaceBounceSpeedCapY: 0,
                facingResolver: CharacterMotionDefaultFacingResolver.Instance);

            Assert.AreEqual(4000, result.HorizontalVelocityX);
            Assert.AreEqual(0, result.HorizontalVelocityY);
            Assert.AreEqual(0, result.YawMilliDegrees);
        }

        private static CharacterKinematicMotionInput CreateInput(
            CharacterMotionRequestFrame requestFrame = default,
            int desiredMoveX = 0,
            int desiredMoveY = 0,
            int moveInputMagnitude = 0,
            int currentHorizontalVelocityX = 0,
            int currentHorizontalVelocityY = 0,
            int currentVerticalVelocity = 0,
            bool wasGrounded = true,
            bool jumpPressed = false,
            bool jumpReleased = false,
            int jumpBufferFramesRemaining = 0,
            int coyoteTimeFramesRemaining = 0,
            bool actionMovementLocked = false,
            int actionMovementModeId = IdleMode,
            int actionJumpImpulseYMillimetersPerSecond = 0,
            int actionMovementControlScaleMilli = Scale,
            int actionRotationControlScaleMilli = Scale,
            int surfaceBounceSpeedCapYMillimetersPerSecond = 0,
            bool hasPhysicsRuntime = false,
            int contactVelocityX = 0,
            int contactVelocityY = 0,
            CombatSurfaceMaterial groundSurfaceMaterial = default,
            bool isCommandDriven = false,
            int gravityMillimetersPerSecondSquared = -30000)
        {
            return new CharacterKinematicMotionInput(
                CreateProfile(gravityMillimetersPerSecondSquared),
                requestFrame,
                new CharacterMotionFacingInput(
                    isCommandDriven,
                    hasLockedTarget: false,
                    currentXMillimeters: 0,
                    currentZMillimeters: 0,
                    currentYawMilliDegrees: 0,
                    lockedTargetXMillimeters: 0,
                    lockedTargetZMillimeters: 0,
                    desiredMoveX,
                    desiredMoveY,
                    currentHorizontalVelocityX,
                    currentHorizontalVelocityY,
                    moveFacingEpsilonMilli: 1),
                TickRate,
                desiredMoveX,
                desiredMoveY,
                moveInputMagnitude,
                currentHorizontalVelocityX,
                currentHorizontalVelocityY,
                currentVerticalVelocity,
                wasGrounded,
                jumpPressed,
                jumpReleased,
                jumpBufferFramesRemaining,
                coyoteTimeFramesRemaining,
                actionMovementLocked,
                actionMovementModeId,
                actionJumpImpulseYMillimetersPerSecond,
                actionMovementControlScaleMilli,
                actionRotationControlScaleMilli,
                surfaceBounceSpeedCapYMillimetersPerSecond,
                hasPhysicsRuntime,
                contactVelocityX,
                contactVelocityY,
                groundSurfaceMaterial.Kind == CombatSurfaceMaterialKind.Default
                    ? CombatSurfaceMaterial.Default
                    : groundSurfaceMaterial,
                IdleMode,
                AirborneMode,
                ControlLockedMode,
                moveFacingEpsilonMilli: 1,
                contactVelocityEpsilonMillimetersPerSecond: 1,
                InputScale,
                Scale,
                TickRate,
                apexVelocityEpsilonMillimetersPerSecond: 1250);
        }

        private static CharacterKinematicMotionProfile CreateProfile(
            int gravityMillimetersPerSecondSquared = -30000)
        {
            return new CharacterKinematicMotionProfile(
                RunMode,
                WalkMode,
                walkSpeedMillimetersPerSecond: 2500,
                runSpeedMillimetersPerSecond: 4000,
                accelerationMillimetersPerSecondSquared: 24000,
                decelerationMillimetersPerSecondSquared: 28000,
                turnSpeedMilliDegreesPerSecond: 720000,
                groundFrictionMillimetersPerSecondSquared: 10000,
                airControlMilli: 350,
                gravityMillimetersPerSecondSquared: gravityMillimetersPerSecondSquared,
                maxFallSpeedMillimetersPerSecond: -50000,
                jumpReleaseVelocityMultiplierMilli: 450,
                apexGravityMultiplierMilli: 750,
                fallGravityMultiplierMilli: 1350,
                jumpBufferFrames: 7,
                coyoteTimeFrames: 6);
        }
    }
}
