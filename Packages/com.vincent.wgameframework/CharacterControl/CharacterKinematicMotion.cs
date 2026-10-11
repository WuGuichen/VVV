using System;
using MxFramework.CharacterAction;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.CharacterControl
{
    public readonly struct CharacterKinematicMotionProfile
    {
        public CharacterKinematicMotionProfile(
            int defaultMovementModeId,
            int walkMovementModeId,
            int walkSpeedMillimetersPerSecond,
            int runSpeedMillimetersPerSecond,
            int accelerationMillimetersPerSecondSquared,
            int decelerationMillimetersPerSecondSquared,
            int turnSpeedMilliDegreesPerSecond,
            int groundFrictionMillimetersPerSecondSquared,
            int airControlMilli,
            int gravityMillimetersPerSecondSquared,
            int maxFallSpeedMillimetersPerSecond,
            int jumpReleaseVelocityMultiplierMilli,
            int apexGravityMultiplierMilli,
            int fallGravityMultiplierMilli,
            int jumpBufferFrames,
            int coyoteTimeFrames,
            int upwardMotionGravityScaleMilli = 0)
        {
            DefaultMovementModeId = defaultMovementModeId;
            WalkMovementModeId = walkMovementModeId;
            WalkSpeedMillimetersPerSecond = walkSpeedMillimetersPerSecond;
            RunSpeedMillimetersPerSecond = runSpeedMillimetersPerSecond;
            AccelerationMillimetersPerSecondSquared = accelerationMillimetersPerSecondSquared;
            DecelerationMillimetersPerSecondSquared = decelerationMillimetersPerSecondSquared;
            TurnSpeedMilliDegreesPerSecond = turnSpeedMilliDegreesPerSecond;
            GroundFrictionMillimetersPerSecondSquared = groundFrictionMillimetersPerSecondSquared;
            AirControlMilli = airControlMilli;
            GravityMillimetersPerSecondSquared = gravityMillimetersPerSecondSquared;
            MaxFallSpeedMillimetersPerSecond = maxFallSpeedMillimetersPerSecond;
            JumpReleaseVelocityMultiplierMilli = jumpReleaseVelocityMultiplierMilli;
            ApexGravityMultiplierMilli = apexGravityMultiplierMilli;
            FallGravityMultiplierMilli = fallGravityMultiplierMilli;
            JumpBufferFrames = jumpBufferFrames;
            CoyoteTimeFrames = coyoteTimeFrames;
            UpwardMotionGravityScaleMilli = upwardMotionGravityScaleMilli > 0
                ? Math.Max(CharacterActionVerticalRootMotionGravity.MinimumUpwardScaleMilli,
                    Math.Min(CharacterActionVerticalRootMotionGravity.ScaleMilli, upwardMotionGravityScaleMilli))
                : CharacterActionVerticalRootMotionGravity.ResolveUpwardScaleMilli(
                    gravityMillimetersPerSecondSquared);
        }

        public int DefaultMovementModeId { get; }
        public int WalkMovementModeId { get; }
        public int WalkSpeedMillimetersPerSecond { get; }
        public int RunSpeedMillimetersPerSecond { get; }
        public int AccelerationMillimetersPerSecondSquared { get; }
        public int DecelerationMillimetersPerSecondSquared { get; }
        public int TurnSpeedMilliDegreesPerSecond { get; }
        public int GroundFrictionMillimetersPerSecondSquared { get; }
        public int AirControlMilli { get; }
        public int GravityMillimetersPerSecondSquared { get; }
        public int MaxFallSpeedMillimetersPerSecond { get; }
        public int JumpReleaseVelocityMultiplierMilli { get; }
        public int ApexGravityMultiplierMilli { get; }
        public int FallGravityMultiplierMilli { get; }
        public int JumpBufferFrames { get; }
        public int CoyoteTimeFrames { get; }
        public int UpwardMotionGravityScaleMilli { get; }
    }

    public readonly struct CharacterMotionFacingInput
    {
        public CharacterMotionFacingInput(
            bool isCommandDriven,
            bool hasLockedTarget,
            int currentXMillimeters,
            int currentZMillimeters,
            int currentYawMilliDegrees,
            int lockedTargetXMillimeters,
            int lockedTargetZMillimeters,
            int desiredMoveX,
            int desiredMoveY,
            int horizontalVelocityX,
            int horizontalVelocityY,
            int moveFacingEpsilonMilli)
        {
            IsCommandDriven = isCommandDriven;
            HasLockedTarget = hasLockedTarget;
            CurrentXMillimeters = currentXMillimeters;
            CurrentZMillimeters = currentZMillimeters;
            CurrentYawMilliDegrees = currentYawMilliDegrees;
            LockedTargetXMillimeters = lockedTargetXMillimeters;
            LockedTargetZMillimeters = lockedTargetZMillimeters;
            DesiredMoveX = desiredMoveX;
            DesiredMoveY = desiredMoveY;
            HorizontalVelocityX = horizontalVelocityX;
            HorizontalVelocityY = horizontalVelocityY;
            MoveFacingEpsilonMilli = moveFacingEpsilonMilli;
        }

        public bool IsCommandDriven { get; }
        public bool HasLockedTarget { get; }
        public int CurrentXMillimeters { get; }
        public int CurrentZMillimeters { get; }
        public int CurrentYawMilliDegrees { get; }
        public int LockedTargetXMillimeters { get; }
        public int LockedTargetZMillimeters { get; }
        public int DesiredMoveX { get; }
        public int DesiredMoveY { get; }
        public int HorizontalVelocityX { get; }
        public int HorizontalVelocityY { get; }
        public int MoveFacingEpsilonMilli { get; }
    }

    public interface ICharacterMotionFacingResolver
    {
        /// <remarks>NoAlloc after initialization.</remarks>
        bool TryResolveTargetYaw(in CharacterMotionFacingInput input, out int targetYawMilliDegrees);
    }

    public readonly struct CharacterKinematicMotionInput
    {
        public CharacterKinematicMotionInput(
            CharacterKinematicMotionProfile profile,
            CharacterMotionRequestFrame requestFrame,
            CharacterMotionFacingInput facing,
            int tickRate,
            int desiredMoveX,
            int desiredMoveY,
            int moveInputMagnitude,
            int currentHorizontalVelocityX,
            int currentHorizontalVelocityY,
            int currentVerticalVelocity,
            bool wasGrounded,
            bool jumpPressed,
            bool jumpReleased,
            int jumpBufferFramesRemaining,
            int coyoteTimeFramesRemaining,
            bool actionMovementLocked,
            int actionMovementModeId,
            int actionJumpImpulseYMillimetersPerSecond,
            int actionMovementControlScaleMilli,
            int actionRotationControlScaleMilli,
            int surfaceBounceSpeedCapYMillimetersPerSecond,
            bool hasPhysicsRuntime,
            int contactVelocityX,
            int contactVelocityY,
            CombatSurfaceMaterial currentGroundSurfaceMaterial,
            int movementModeIdleId,
            int movementModeAirborneId,
            int movementModeControlLockedId,
            int moveFacingEpsilonMilli,
            int contactVelocityEpsilonMillimetersPerSecond,
            int inputAxisScale,
            int scaleFactorScale,
            int defaultTickRate,
            int apexVelocityEpsilonMillimetersPerSecond)
        {
            Profile = profile;
            RequestFrame = requestFrame;
            Facing = facing;
            TickRate = tickRate;
            DesiredMoveX = desiredMoveX;
            DesiredMoveY = desiredMoveY;
            MoveInputMagnitude = moveInputMagnitude;
            CurrentHorizontalVelocityX = currentHorizontalVelocityX;
            CurrentHorizontalVelocityY = currentHorizontalVelocityY;
            CurrentVerticalVelocity = currentVerticalVelocity;
            WasGrounded = wasGrounded;
            JumpPressed = jumpPressed;
            JumpReleased = jumpReleased;
            JumpBufferFramesRemaining = jumpBufferFramesRemaining;
            CoyoteTimeFramesRemaining = coyoteTimeFramesRemaining;
            ActionMovementLocked = actionMovementLocked;
            ActionMovementModeId = actionMovementModeId;
            ActionJumpImpulseYMillimetersPerSecond = actionJumpImpulseYMillimetersPerSecond;
            ActionMovementControlScaleMilli = actionMovementControlScaleMilli;
            ActionRotationControlScaleMilli = actionRotationControlScaleMilli;
            SurfaceBounceSpeedCapYMillimetersPerSecond = surfaceBounceSpeedCapYMillimetersPerSecond;
            HasPhysicsRuntime = hasPhysicsRuntime;
            ContactVelocityX = contactVelocityX;
            ContactVelocityY = contactVelocityY;
            CurrentGroundSurfaceMaterial = currentGroundSurfaceMaterial;
            MovementModeIdleId = movementModeIdleId;
            MovementModeAirborneId = movementModeAirborneId;
            MovementModeControlLockedId = movementModeControlLockedId;
            MoveFacingEpsilonMilli = moveFacingEpsilonMilli;
            ContactVelocityEpsilonMillimetersPerSecond = contactVelocityEpsilonMillimetersPerSecond;
            InputAxisScale = inputAxisScale;
            ScaleFactorScale = scaleFactorScale;
            DefaultTickRate = defaultTickRate;
            ApexVelocityEpsilonMillimetersPerSecond = apexVelocityEpsilonMillimetersPerSecond;
        }

        public CharacterKinematicMotionProfile Profile { get; }
        public CharacterMotionRequestFrame RequestFrame { get; }
        public CharacterMotionFacingInput Facing { get; }
        public int TickRate { get; }
        public int DesiredMoveX { get; }
        public int DesiredMoveY { get; }
        public int MoveInputMagnitude { get; }
        public int CurrentHorizontalVelocityX { get; }
        public int CurrentHorizontalVelocityY { get; }
        public int CurrentVerticalVelocity { get; }
        public bool WasGrounded { get; }
        public bool JumpPressed { get; }
        public bool JumpReleased { get; }
        public int JumpBufferFramesRemaining { get; }
        public int CoyoteTimeFramesRemaining { get; }
        public bool ActionMovementLocked { get; }
        public int ActionMovementModeId { get; }
        public int ActionJumpImpulseYMillimetersPerSecond { get; }
        public int ActionMovementControlScaleMilli { get; }
        public int ActionRotationControlScaleMilli { get; }
        public int SurfaceBounceSpeedCapYMillimetersPerSecond { get; }
        public bool HasPhysicsRuntime { get; }
        public int ContactVelocityX { get; }
        public int ContactVelocityY { get; }
        public CombatSurfaceMaterial CurrentGroundSurfaceMaterial { get; }
        public int MovementModeIdleId { get; }
        public int MovementModeAirborneId { get; }
        public int MovementModeControlLockedId { get; }
        public int MoveFacingEpsilonMilli { get; }
        public int ContactVelocityEpsilonMillimetersPerSecond { get; }
        public int InputAxisScale { get; }
        public int ScaleFactorScale { get; }
        public int DefaultTickRate { get; }
        public int ApexVelocityEpsilonMillimetersPerSecond { get; }
    }

    public readonly struct CharacterKinematicMotionPrePhysicsResult
    {
        public CharacterKinematicMotionPrePhysicsResult(
            int horizontalVelocityX,
            int horizontalVelocityY,
            int verticalVelocity,
            int impactVerticalVelocity,
            int simulatedHorizontalVelocityX,
            int simulatedHorizontalVelocityY,
            int simulatedVerticalVelocity,
            int simulatedImpactVerticalVelocity,
            int rootMotionHorizontalVelocityX,
            int rootMotionHorizontalVelocityY,
            int rootMotionVerticalVelocity,
            int targetVelocityX,
            int targetVelocityY,
            int desiredStateVelocityX,
            int desiredStateVelocityY,
            int jumpBufferFrames,
            int coyoteTimeFrames,
            int movementControlScaleMilli,
            int rotationControlScaleMilli,
            int movementModeId,
            bool movementLocked,
            bool hasActiveMoveInput,
            bool preserveExternalHorizontalVelocity,
            bool characterSurfaceTraction,
            bool applyGroundSurfaceFriction,
            bool wasGroundedForPhysics,
            int surfaceBounceSpeedCapY,
            bool jumpImpulseApplied,
            bool jumpImpulseBlocked)
        {
            HorizontalVelocityX = horizontalVelocityX;
            HorizontalVelocityY = horizontalVelocityY;
            VerticalVelocity = verticalVelocity;
            ImpactVerticalVelocity = impactVerticalVelocity;
            SimulatedHorizontalVelocityX = simulatedHorizontalVelocityX;
            SimulatedHorizontalVelocityY = simulatedHorizontalVelocityY;
            SimulatedVerticalVelocity = simulatedVerticalVelocity;
            SimulatedImpactVerticalVelocity = simulatedImpactVerticalVelocity;
            RootMotionHorizontalVelocityX = rootMotionHorizontalVelocityX;
            RootMotionHorizontalVelocityY = rootMotionHorizontalVelocityY;
            RootMotionVerticalVelocity = rootMotionVerticalVelocity;
            TargetVelocityX = targetVelocityX;
            TargetVelocityY = targetVelocityY;
            DesiredStateVelocityX = desiredStateVelocityX;
            DesiredStateVelocityY = desiredStateVelocityY;
            JumpBufferFrames = jumpBufferFrames;
            CoyoteTimeFrames = coyoteTimeFrames;
            MovementControlScaleMilli = movementControlScaleMilli;
            RotationControlScaleMilli = rotationControlScaleMilli;
            MovementModeId = movementModeId;
            MovementLocked = movementLocked;
            HasActiveMoveInput = hasActiveMoveInput;
            PreserveExternalHorizontalVelocity = preserveExternalHorizontalVelocity;
            CharacterSurfaceTraction = characterSurfaceTraction;
            ApplyGroundSurfaceFriction = applyGroundSurfaceFriction;
            WasGroundedForPhysics = wasGroundedForPhysics;
            SurfaceBounceSpeedCapY = surfaceBounceSpeedCapY;
            JumpImpulseApplied = jumpImpulseApplied;
            JumpImpulseBlocked = jumpImpulseBlocked;
        }

        public int HorizontalVelocityX { get; }
        public int HorizontalVelocityY { get; }
        public int VerticalVelocity { get; }
        public int ImpactVerticalVelocity { get; }
        public int SimulatedHorizontalVelocityX { get; }
        public int SimulatedHorizontalVelocityY { get; }
        public int SimulatedVerticalVelocity { get; }
        public int SimulatedImpactVerticalVelocity { get; }
        public int RootMotionHorizontalVelocityX { get; }
        public int RootMotionHorizontalVelocityY { get; }
        public int RootMotionVerticalVelocity { get; }
        public int TargetVelocityX { get; }
        public int TargetVelocityY { get; }
        public int DesiredStateVelocityX { get; }
        public int DesiredStateVelocityY { get; }
        public int JumpBufferFrames { get; }
        public int CoyoteTimeFrames { get; }
        public int MovementControlScaleMilli { get; }
        public int RotationControlScaleMilli { get; }
        public int MovementModeId { get; }
        public bool MovementLocked { get; }
        public bool HasActiveMoveInput { get; }
        public bool PreserveExternalHorizontalVelocity { get; }
        public bool CharacterSurfaceTraction { get; }
        public bool ApplyGroundSurfaceFriction { get; }
        public bool WasGroundedForPhysics { get; }
        public int SurfaceBounceSpeedCapY { get; }
        public bool JumpImpulseApplied { get; }
        public bool JumpImpulseBlocked { get; }
    }

    public readonly struct CharacterKinematicMotionFinalResult
    {
        public CharacterKinematicMotionFinalResult(
            int horizontalVelocityX,
            int horizontalVelocityY,
            int verticalVelocity,
            int yawMilliDegrees,
            short blendX,
            short blendY,
            int jumpBufferFrames,
            int coyoteTimeFrames,
            int movementModeId,
            int surfaceBounceSpeedCapY)
        {
            HorizontalVelocityX = horizontalVelocityX;
            HorizontalVelocityY = horizontalVelocityY;
            VerticalVelocity = verticalVelocity;
            YawMilliDegrees = yawMilliDegrees;
            BlendX = blendX;
            BlendY = blendY;
            JumpBufferFrames = jumpBufferFrames;
            CoyoteTimeFrames = coyoteTimeFrames;
            MovementModeId = movementModeId;
            SurfaceBounceSpeedCapY = surfaceBounceSpeedCapY;
        }

        public int HorizontalVelocityX { get; }
        public int HorizontalVelocityY { get; }
        public int VerticalVelocity { get; }
        public int YawMilliDegrees { get; }
        public short BlendX { get; }
        public short BlendY { get; }
        public int JumpBufferFrames { get; }
        public int CoyoteTimeFrames { get; }
        public int MovementModeId { get; }
        public int SurfaceBounceSpeedCapY { get; }
    }

    public sealed class CharacterKinematicMotionSolver
    {
        public static readonly CharacterKinematicMotionSolver Default = new CharacterKinematicMotionSolver();

        /// <remarks>NoAlloc after initialization.</remarks>
        public CharacterKinematicMotionPrePhysicsResult SolvePrePhysics(in CharacterKinematicMotionInput input)
        {
            CharacterMotionRequestFrame requestFrame = input.RequestFrame;
            int movementControlScaleMilli = CharacterMotionRequestAccumulator.ResolveControlScaleMilli(
                requestFrame,
                input.ActionMovementControlScaleMilli);
            int rotationControlScaleMilli = CharacterMotionRequestAccumulator.ResolveRotationScaleMilli(
                requestFrame,
                input.ActionRotationControlScaleMilli);
            int movementModeId = ResolveMovementMode(input);
            bool movementLocked = requestFrame.LockMovement
                || input.ActionMovementLocked
                || movementModeId == input.MovementModeControlLockedId;
            if (movementLocked)
                movementControlScaleMilli = 0;

            bool hasActiveMoveInput = movementControlScaleMilli > 0
                && SqrMagnitude(input.DesiredMoveX, input.DesiredMoveY)
                    > input.MoveFacingEpsilonMilli * input.MoveFacingEpsilonMilli;
            bool preserveExternalHorizontalVelocity =
                requestFrame.ExternalAccelerationBypassesGroundDamping && !hasActiveMoveInput;
            bool characterSurfaceTraction = UsesCharacterSurfaceTraction(
                input.CurrentGroundSurfaceMaterial,
                input.WasGrounded);

            int jumpBufferFrames = input.JumpPressed
                ? input.Profile.JumpBufferFrames
                : System.Math.Max(0, input.JumpBufferFramesRemaining - 1);
            int coyoteFrames = input.WasGrounded
                ? input.Profile.CoyoteTimeFrames
                : System.Math.Max(0, input.CoyoteTimeFramesRemaining - 1);

            ResolveTargetVelocity(
                input.DesiredMoveX,
                input.DesiredMoveY,
                input.MoveInputMagnitude,
                input.CurrentHorizontalVelocityX,
                input.CurrentHorizontalVelocityY,
                input.WasGrounded,
                movementControlScaleMilli,
                input.Profile,
                input.MoveFacingEpsilonMilli,
                input.InputAxisScale,
                input.ScaleFactorScale,
                out int targetVelocityX,
                out int targetVelocityY);
            int desiredStateVelocityX = preserveExternalHorizontalVelocity ? 0 : targetVelocityX;
            int desiredStateVelocityY = preserveExternalHorizontalVelocity ? 0 : targetVelocityY;
            if (preserveExternalHorizontalVelocity)
            {
                targetVelocityX = input.CurrentHorizontalVelocityX;
                targetVelocityY = input.CurrentHorizontalVelocityY;
            }

            MoveHorizontalVelocity(
                input.CurrentHorizontalVelocityX,
                input.CurrentHorizontalVelocityY,
                targetVelocityX,
                targetVelocityY,
                input.WasGrounded,
                input.TickRate,
                input.Profile,
                input.CurrentGroundSurfaceMaterial,
                input.MoveFacingEpsilonMilli,
                input.InputAxisScale,
                input.ScaleFactorScale,
                input.DefaultTickRate,
                out int horizontalVelocityX,
                out int horizontalVelocityY);

            int verticalVelocity = input.WasGrounded && input.CurrentVerticalVelocity < 0
                ? 0
                : input.CurrentVerticalVelocity;
            int surfaceBounceSpeedCapY = ResolveStartingSurfaceBounceSpeedCapY(
                input.SurfaceBounceSpeedCapYMillimetersPerSecond,
                requestFrame);

            if (requestFrame.HasVelocityOverride)
            {
                horizontalVelocityX = requestFrame.VelocityOverrideX;
                horizontalVelocityY = requestFrame.VelocityOverrideZ;
                verticalVelocity = requestFrame.VelocityOverrideY;
            }
            if (requestFrame.HasVerticalVelocityOverride)
                verticalVelocity = requestFrame.VerticalVelocityOverrideY;

            if (requestFrame.HasExternalAcceleration)
            {
                horizontalVelocityX += DividePerTick(requestFrame.ExternalAccelerationX, input.TickRate, input.DefaultTickRate);
                horizontalVelocityY += DividePerTick(requestFrame.ExternalAccelerationZ, input.TickRate, input.DefaultTickRate);
                verticalVelocity += DividePerTick(requestFrame.ExternalAccelerationY, input.TickRate, input.DefaultTickRate);
            }

            if (input.HasPhysicsRuntime
                && SqrMagnitude(input.ContactVelocityX, input.ContactVelocityY)
                    > input.ContactVelocityEpsilonMillimetersPerSecond * input.ContactVelocityEpsilonMillimetersPerSecond)
            {
                horizontalVelocityX += input.ContactVelocityX;
                horizontalVelocityY += input.ContactVelocityY;
            }

            if (requestFrame.HasHorizontalVelocityOverride)
            {
                horizontalVelocityX = requestFrame.HorizontalVelocityOverrideX;
                horizontalVelocityY = requestFrame.HorizontalVelocityOverrideZ;
            }

            int impulseX = requestFrame.HasImpulse ? requestFrame.ImpulseVelocityX : 0;
            int impulseY = requestFrame.HasImpulse
                ? requestFrame.ImpulseVelocityY
                : input.ActionJumpImpulseYMillimetersPerSecond;
            int impulseZ = requestFrame.HasImpulse ? requestFrame.ImpulseVelocityZ : 0;
            if (requestFrame.HasImpulse)
            {
                horizontalVelocityX += impulseX;
                horizontalVelocityY += impulseZ;
                if (impulseY < 0)
                    verticalVelocity += impulseY;
            }

            bool wasGroundedForPhysics = input.WasGrounded;
            bool jumpImpulseApplied = false;
            bool jumpImpulseBlocked = false;
            bool canApplyJumpImpulse = input.WasGrounded || input.CoyoteTimeFramesRemaining > 0;
            bool suppressJumpForSurfaceBounce = surfaceBounceSpeedCapY > 0;
            if (canApplyJumpImpulse && impulseY > 0 && !suppressJumpForSurfaceBounce)
            {
                verticalVelocity = impulseY;
                wasGroundedForPhysics = false;
                coyoteFrames = 0;
                jumpBufferFrames = 0;
                movementModeId = input.MovementModeAirborneId;
                jumpImpulseApplied = true;
            }
            else if (impulseY > 0)
            {
                jumpImpulseBlocked = true;
            }
            else if (!wasGroundedForPhysics
                && input.JumpReleased
                && verticalVelocity > 0
                && !suppressJumpForSurfaceBounce)
            {
                verticalVelocity = MulDivRoundToInt(
                    verticalVelocity,
                    input.Profile.JumpReleaseVelocityMultiplierMilli,
                    input.ScaleFactorScale);
            }

            bool hasUpwardRootMotion = requestFrame.HasRootMotion
                && requestFrame.RootMotionDeltaY > 0;
            int impactVerticalVelocity = hasUpwardRootMotion ? 0 : verticalVelocity;
            if (hasUpwardRootMotion)
            {
                // The authored upward sample owns vertical displacement on this tick.
                // Gravity only reduces its positive multiplier; it cannot reverse the sample.
                verticalVelocity = 0;
            }
            else if (!wasGroundedForPhysics && !requestFrame.IgnoreGravity)
            {
                int gravityMultiplierMilli = ResolveGravityMultiplierMilli(input, verticalVelocity);
                int gravityDelta = MulDivRoundToInt(
                    input.Profile.GravityMillimetersPerSecondSquared,
                    gravityMultiplierMilli,
                    input.ScaleFactorScale * SafeTickRate(input.TickRate, input.DefaultTickRate));
                verticalVelocity = System.Math.Max(
                    input.Profile.MaxFallSpeedMillimetersPerSecond,
                    verticalVelocity + gravityDelta);
            }

            bool applyHorizontalRootMotion = requestFrame.HasRootMotion
                && !requestFrame.HasHorizontalVelocityOverride;
            int rootMotionHorizontalVelocityX = applyHorizontalRootMotion
                ? requestFrame.RootMotionDeltaX * SafeTickRate(input.TickRate, input.DefaultTickRate)
                : 0;
            int rootMotionHorizontalVelocityY = applyHorizontalRootMotion
                ? requestFrame.RootMotionDeltaZ * SafeTickRate(input.TickRate, input.DefaultTickRate)
                : 0;
            int rootMotionVerticalVelocity = requestFrame.HasRootMotion
                ? requestFrame.RootMotionDeltaY * SafeTickRate(input.TickRate, input.DefaultTickRate)
                : 0;
            if (hasUpwardRootMotion)
            {
                int scaleMilli = requestFrame.IgnoreGravity
                    ? CharacterActionVerticalRootMotionGravity.ScaleMilli
                    : input.Profile.UpwardMotionGravityScaleMilli;
                rootMotionVerticalVelocity = MulDivRoundToInt(
                    rootMotionVerticalVelocity, scaleMilli,
                    CharacterActionVerticalRootMotionGravity.ScaleMilli);
            }
            int simulatedImpactVerticalVelocity = impactVerticalVelocity + rootMotionVerticalVelocity;
            if (surfaceBounceSpeedCapY > 0 && simulatedImpactVerticalVelocity < -surfaceBounceSpeedCapY)
                simulatedImpactVerticalVelocity = -surfaceBounceSpeedCapY;

            return new CharacterKinematicMotionPrePhysicsResult(
                horizontalVelocityX,
                horizontalVelocityY,
                verticalVelocity,
                impactVerticalVelocity,
                horizontalVelocityX + rootMotionHorizontalVelocityX,
                horizontalVelocityY + rootMotionHorizontalVelocityY,
                verticalVelocity + rootMotionVerticalVelocity,
                simulatedImpactVerticalVelocity,
                rootMotionHorizontalVelocityX,
                rootMotionHorizontalVelocityY,
                rootMotionVerticalVelocity,
                targetVelocityX,
                targetVelocityY,
                desiredStateVelocityX,
                desiredStateVelocityY,
                jumpBufferFrames,
                coyoteFrames,
                movementControlScaleMilli,
                rotationControlScaleMilli,
                movementModeId,
                movementLocked,
                hasActiveMoveInput,
                preserveExternalHorizontalVelocity,
                characterSurfaceTraction,
                !hasActiveMoveInput && !characterSurfaceTraction && !preserveExternalHorizontalVelocity,
                wasGroundedForPhysics,
                surfaceBounceSpeedCapY,
                jumpImpulseApplied,
                jumpImpulseBlocked);
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public CharacterKinematicMotionFinalResult CompleteAfterPhysics(
            in CharacterKinematicMotionInput input,
            in CharacterKinematicMotionPrePhysicsResult prePhysics,
            bool grounded,
            int horizontalVelocityX,
            int horizontalVelocityY,
            int verticalVelocity,
            int surfaceBounceSpeedCapY,
            ICharacterMotionFacingResolver facingResolver)
        {
            int coyoteFrames = prePhysics.CoyoteTimeFrames;
            int movementModeId = prePhysics.MovementModeId;
            if (grounded)
            {
                surfaceBounceSpeedCapY = 0;
                if (verticalVelocity < 0)
                    verticalVelocity = 0;
                coyoteFrames = input.Profile.CoyoteTimeFrames;
                if (movementModeId == input.MovementModeAirborneId)
                    movementModeId = input.Profile.DefaultMovementModeId;
            }

            int yawMilliDegrees = ResolveYaw(
                input,
                prePhysics,
                facingResolver);
            int runSpeed = System.Math.Max(1, input.Profile.RunSpeedMillimetersPerSecond);
            short blendX = (short)Clamp(
                DivideRoundToInt((long)horizontalVelocityX * input.InputAxisScale, runSpeed),
                -input.InputAxisScale,
                input.InputAxisScale);
            short blendY = (short)Clamp(
                DivideRoundToInt((long)horizontalVelocityY * input.InputAxisScale, runSpeed),
                -input.InputAxisScale,
                input.InputAxisScale);

            return new CharacterKinematicMotionFinalResult(
                horizontalVelocityX,
                horizontalVelocityY,
                verticalVelocity,
                yawMilliDegrees,
                blendX,
                blendY,
                prePhysics.JumpBufferFrames,
                coyoteFrames,
                movementModeId,
                surfaceBounceSpeedCapY);
        }

        private static int ResolveMovementMode(in CharacterKinematicMotionInput input)
        {
            if (input.RequestFrame.LockMovement)
                return input.MovementModeControlLockedId;

            if (input.RequestFrame.MovementModeId != input.MovementModeIdleId)
                return input.RequestFrame.MovementModeId;

            if (input.ActionMovementLocked)
                return input.MovementModeControlLockedId;

            if (input.ActionMovementModeId != input.MovementModeIdleId)
                return input.ActionMovementModeId;

            return input.WasGrounded ? input.Profile.DefaultMovementModeId : input.MovementModeAirborneId;
        }

        private static void ResolveTargetVelocity(
            int desiredMoveX,
            int desiredMoveY,
            int inputMagnitude,
            int currentVelocityX,
            int currentVelocityY,
            bool grounded,
            int movementControlScaleMilli,
            CharacterKinematicMotionProfile profile,
            int moveFacingEpsilonMilli,
            int inputAxisScale,
            int scaleFactorScale,
            out int targetVelocityX,
            out int targetVelocityY)
        {
            movementControlScaleMilli = CharacterMotionRequest.ClampScaleMilli(movementControlScaleMilli);
            if (movementControlScaleMilli <= 0)
            {
                targetVelocityX = 0;
                targetVelocityY = 0;
                return;
            }

            int desiredSqr = SqrMagnitude(desiredMoveX, desiredMoveY);
            if (desiredSqr <= moveFacingEpsilonMilli * moveFacingEpsilonMilli)
            {
                if (grounded)
                {
                    targetVelocityX = 0;
                    targetVelocityY = 0;
                }
                else
                {
                    targetVelocityX = currentVelocityX;
                    targetVelocityY = currentVelocityY;
                }

                return;
            }

            int speed = ResolveMoveSpeed(inputMagnitude, profile, inputAxisScale);
            if (!grounded)
            {
                int currentSpeed = Magnitude(currentVelocityX, currentVelocityY);
                if (speed < currentSpeed)
                    speed = currentSpeed;
            }

            NormalizeToScale(
                desiredMoveX,
                desiredMoveY,
                inputAxisScale,
                out int directionX,
                out int directionY);
            int scaledSpeed = MulDivRoundToInt(
                speed,
                movementControlScaleMilli,
                scaleFactorScale);
            targetVelocityX = MulDivRoundToInt(directionX, scaledSpeed, inputAxisScale);
            targetVelocityY = MulDivRoundToInt(directionY, scaledSpeed, inputAxisScale);
        }

        private static int ResolveMoveSpeed(
            int inputMagnitude,
            CharacterKinematicMotionProfile profile,
            int inputAxisScale)
        {
            int walkSpeed = System.Math.Max(0, profile.WalkSpeedMillimetersPerSecond);
            int runSpeed = System.Math.Max(walkSpeed, profile.RunSpeedMillimetersPerSecond);
            int magnitude = Clamp(inputMagnitude, 0, inputAxisScale);
            if (profile.DefaultMovementModeId == profile.WalkMovementModeId)
                return MulDivRoundToInt(walkSpeed, magnitude, inputAxisScale);

            return walkSpeed + MulDivRoundToInt(runSpeed - walkSpeed, magnitude, inputAxisScale);
        }

        private static void MoveHorizontalVelocity(
            int currentVelocityX,
            int currentVelocityY,
            int targetVelocityX,
            int targetVelocityY,
            bool grounded,
            int tickRate,
            CharacterKinematicMotionProfile profile,
            CombatSurfaceMaterial groundSurfaceMaterial,
            int moveFacingEpsilonMilli,
            int inputAxisScale,
            int scaleFactorScale,
            int defaultTickRate,
            out int resultVelocityX,
            out int resultVelocityY)
        {
            long currentSqr = (long)currentVelocityX * currentVelocityX + (long)currentVelocityY * currentVelocityY;
            long targetSqr = (long)targetVelocityX * targetVelocityX + (long)targetVelocityY * targetVelocityY;
            int acceleration = targetSqr > currentSqr
                ? System.Math.Max(0, profile.AccelerationMillimetersPerSecondSquared)
                : System.Math.Max(0, profile.DecelerationMillimetersPerSecondSquared);
            if (!grounded)
                acceleration = MulDivRoundToInt(acceleration, profile.AirControlMilli, scaleFactorScale);

            if (grounded && targetSqr <= moveFacingEpsilonMilli * moveFacingEpsilonMilli)
                acceleration += System.Math.Max(0, profile.GroundFrictionMillimetersPerSecondSquared);
            acceleration = ApplyGroundSurfaceTraction(acceleration, grounded, groundSurfaceMaterial);

            if (acceleration <= 0)
            {
                resultVelocityX = targetVelocityX;
                resultVelocityY = targetVelocityY;
                return;
            }

            int maxDelta = System.Math.Max(1, DividePerTick(acceleration, tickRate, defaultTickRate));
            MoveTowards2D(
                currentVelocityX,
                currentVelocityY,
                targetVelocityX,
                targetVelocityY,
                maxDelta,
                out resultVelocityX,
                out resultVelocityY);
        }

        private static int ResolveStartingSurfaceBounceSpeedCapY(
            int currentCap,
            CharacterMotionRequestFrame requestFrame)
        {
            if (currentCap <= 0)
                return 0;

            return requestFrame.HasVelocityOverride
                || requestFrame.HasExternalAcceleration
                || requestFrame.HasImpulse
                ? 0
                : currentCap;
        }

        private static int ResolveGravityMultiplierMilli(
            in CharacterKinematicMotionInput input,
            int verticalVelocity)
        {
            if (verticalVelocity < 0)
                return input.Profile.FallGravityMultiplierMilli;

            return verticalVelocity <= input.ApexVelocityEpsilonMillimetersPerSecond
                ? input.Profile.ApexGravityMultiplierMilli
                : input.ScaleFactorScale;
        }

        private static bool UsesCharacterSurfaceTraction(
            CombatSurfaceMaterial groundSurfaceMaterial,
            bool grounded)
        {
            return grounded && groundSurfaceMaterial.Kind != CombatSurfaceMaterialKind.Default;
        }

        private static int ApplyGroundSurfaceTraction(
            int acceleration,
            bool grounded,
            CombatSurfaceMaterial groundSurfaceMaterial)
        {
            if (!UsesCharacterSurfaceTraction(groundSurfaceMaterial, grounded))
                return acceleration;

            int surfaceAcceleration = Fix64MetersToMillimeters(groundSurfaceMaterial.Friction);
            if (surfaceAcceleration <= 0)
                return 0;

            return System.Math.Min(acceleration, surfaceAcceleration);
        }

        private static int ResolveYaw(
            in CharacterKinematicMotionInput input,
            in CharacterKinematicMotionPrePhysicsResult prePhysics,
            ICharacterMotionFacingResolver facingResolver)
        {
            if (!input.Facing.IsCommandDriven)
                return input.Facing.CurrentYawMilliDegrees;

            int rotationControlScaleMilli = CharacterMotionRequest.ClampScaleMilli(prePhysics.RotationControlScaleMilli);
            if (rotationControlScaleMilli <= 0)
                return input.Facing.CurrentYawMilliDegrees;

            // Facing is an intent/control semantic. The final horizontal velocity
            // also contains contact pressure, impulses and other external motion;
            // feeding it back here would rotate a character merely because it was
            // pushed. Use the pre-external facing snapshot captured by the caller.
            CharacterMotionFacingInput facingInput = input.Facing;
            if (facingResolver == null || !facingResolver.TryResolveTargetYaw(in facingInput, out int targetYaw))
                return input.Facing.CurrentYawMilliDegrees;

            int turnSpeed = MulDivRoundToInt(
                System.Math.Max(0, input.Profile.TurnSpeedMilliDegreesPerSecond),
                rotationControlScaleMilli,
                input.ScaleFactorScale);
            if (turnSpeed <= 0)
                return targetYaw;

            int maxDelta = System.Math.Max(1, DividePerTick(turnSpeed, input.TickRate, input.DefaultTickRate));
            return MoveTowardsAngle(input.Facing.CurrentYawMilliDegrees, targetYaw, maxDelta);
        }

        private static int Fix64MetersToMillimeters(Fix64 meters)
        {
            return DivideRoundToInt(meters.RawValue * 1000, Fix64.Scale);
        }

        private static int SafeTickRate(int tickRate, int defaultTickRate)
        {
            return tickRate <= 0 ? defaultTickRate : tickRate;
        }

        private static int DividePerTick(int value, int tickRate, int defaultTickRate)
        {
            return DivideRoundToInt(value, SafeTickRate(tickRate, defaultTickRate));
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        private static int DivideRoundToInt(long numerator, long denominator)
        {
            if (denominator == 0)
                return 0;

            bool negative = (numerator < 0) ^ (denominator < 0);
            long absNumerator = numerator < 0 ? -numerator : numerator;
            long absDenominator = denominator < 0 ? -denominator : denominator;
            long value = (absNumerator + absDenominator / 2) / absDenominator;
            if (negative)
                value = -value;
            if (value <= int.MinValue)
                return int.MinValue;
            if (value >= int.MaxValue)
                return int.MaxValue;
            return (int)value;
        }

        private static int MulDivRoundToInt(long value, long multiplier, long denominator)
        {
            return DivideRoundToInt(value * multiplier, denominator);
        }

        private static int Magnitude(int x, int y)
        {
            return IntegerSqrt((long)x * x + (long)y * y);
        }

        private static int SqrMagnitude(int x, int y)
        {
            long sqr = (long)x * x + (long)y * y;
            if (sqr >= int.MaxValue)
                return int.MaxValue;
            return (int)sqr;
        }

        private static void NormalizeToScale(int x, int y, int scale, out int normalizedX, out int normalizedY)
        {
            int magnitude = Magnitude(x, y);
            if (magnitude <= 0 || scale <= 0)
            {
                normalizedX = 0;
                normalizedY = 0;
                return;
            }

            normalizedX = DivideRoundToInt((long)x * scale, magnitude);
            normalizedY = DivideRoundToInt((long)y * scale, magnitude);
        }

        private static void MoveTowards2D(
            int currentX,
            int currentY,
            int targetX,
            int targetY,
            int maxDelta,
            out int resultX,
            out int resultY)
        {
            if (maxDelta <= 0)
            {
                resultX = targetX;
                resultY = targetY;
                return;
            }

            int deltaX = targetX - currentX;
            int deltaY = targetY - currentY;
            int distance = Magnitude(deltaX, deltaY);
            if (distance <= maxDelta || distance == 0)
            {
                resultX = targetX;
                resultY = targetY;
                return;
            }

            resultX = currentX + DivideRoundToInt((long)deltaX * maxDelta, distance);
            resultY = currentY + DivideRoundToInt((long)deltaY * maxDelta, distance);
        }

        private static int MoveTowardsAngle(int currentMilliDegrees, int targetMilliDegrees, int maxDeltaMilliDegrees)
        {
            if (maxDeltaMilliDegrees <= 0)
                return NormalizeMilliDegrees(targetMilliDegrees);

            int current = NormalizeMilliDegrees(currentMilliDegrees);
            int delta = ShortestAngleDelta(current, targetMilliDegrees);
            if (delta > maxDeltaMilliDegrees)
                delta = maxDeltaMilliDegrees;
            else if (delta < -maxDeltaMilliDegrees)
                delta = -maxDeltaMilliDegrees;

            return NormalizeMilliDegrees(current + delta);
        }

        private static int ShortestAngleDelta(int currentMilliDegrees, int targetMilliDegrees)
        {
            int current = NormalizeMilliDegrees(currentMilliDegrees);
            int target = NormalizeMilliDegrees(targetMilliDegrees);
            int fullTurn = 360000;
            int delta = (target - current) % fullTurn;
            if (delta > fullTurn / 2)
                delta -= fullTurn;
            else if (delta < -fullTurn / 2)
                delta += fullTurn;
            return delta;
        }

        private static int NormalizeMilliDegrees(int milliDegrees)
        {
            int fullTurn = 360000;
            int normalized = milliDegrees % fullTurn;
            if (normalized < 0)
                normalized += fullTurn;
            return normalized;
        }

        private static int IntegerSqrt(long value)
        {
            if (value <= 0)
                return 0;

            ulong n = (ulong)value;
            ulong result = 0;
            ulong bit = 1UL << 62;
            while (bit > n)
                bit >>= 2;

            while (bit != 0)
            {
                if (n >= result + bit)
                {
                    n -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result >>= 1;
                }

                bit >>= 2;
            }

            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }
    }

    public sealed class CharacterMotionDefaultFacingResolver : ICharacterMotionFacingResolver
    {
        public static readonly CharacterMotionDefaultFacingResolver Instance = new CharacterMotionDefaultFacingResolver();

        public bool TryResolveTargetYaw(in CharacterMotionFacingInput input, out int targetYawMilliDegrees)
        {
            targetYawMilliDegrees = input.CurrentYawMilliDegrees;
            bool hasTargetYaw = false;
            if (input.HasLockedTarget)
            {
                int toTargetX = input.LockedTargetXMillimeters - input.CurrentXMillimeters;
                int toTargetZ = input.LockedTargetZMillimeters - input.CurrentZMillimeters;
                if (CharacterKinematicMotionMath.SqrMagnitude(toTargetX, toTargetZ) > input.MoveFacingEpsilonMilli)
                {
                    targetYawMilliDegrees = CharacterKinematicMotionMath.Atan2MilliDegrees(toTargetX, toTargetZ);
                    hasTargetYaw = true;
                }
            }

            int facingMoveX = CharacterKinematicMotionMath.SqrMagnitude(input.DesiredMoveX, input.DesiredMoveY) > input.MoveFacingEpsilonMilli
                ? input.DesiredMoveX
                : input.HorizontalVelocityX;
            int facingMoveY = CharacterKinematicMotionMath.SqrMagnitude(input.DesiredMoveX, input.DesiredMoveY) > input.MoveFacingEpsilonMilli
                ? input.DesiredMoveY
                : input.HorizontalVelocityY;
            if (!hasTargetYaw && CharacterKinematicMotionMath.SqrMagnitude(facingMoveX, facingMoveY) > input.MoveFacingEpsilonMilli)
            {
                targetYawMilliDegrees = CharacterKinematicMotionMath.Atan2MilliDegrees(facingMoveX, facingMoveY);
                hasTargetYaw = true;
            }

            return hasTargetYaw;
        }
    }

    public static class CharacterKinematicMotionMath
    {
        private const int TrigScale = 1000000;

        public static int SqrMagnitude(int x, int y)
        {
            long sqr = (long)x * x + (long)y * y;
            if (sqr >= int.MaxValue)
                return int.MaxValue;
            return (int)sqr;
        }

        public static int Atan2MilliDegrees(int y, int x)
        {
            if (x == 0 && y == 0)
                return 0;

            long absX = x < 0 ? -(long)x : x;
            long absY = y < 0 ? -(long)y : y;
            int angle;
            if (absX >= absY)
                angle = AtanRatioMilliDegrees(absY, absX);
            else
                angle = 90000 - AtanRatioMilliDegrees(absX, absY);

            if (x < 0)
                angle = 180000 - angle;
            if (y < 0)
                angle = 360000 - angle;
            return NormalizeMilliDegrees(angle);
        }

        private static int AtanRatioMilliDegrees(long numerator, long denominator)
        {
            if (denominator <= 0 || numerator <= 0)
                return 0;

            long ratio = (numerator * TrigScale + denominator / 2) / denominator;
            if (ratio > TrigScale)
                ratio = TrigScale;

            long correction = 15642L * (TrigScale - ratio) / TrigScale;
            return DivideRoundToInt(ratio * (45000 + correction), TrigScale);
        }

        private static int DivideRoundToInt(long numerator, long denominator)
        {
            if (denominator == 0)
                return 0;

            bool negative = (numerator < 0) ^ (denominator < 0);
            long absNumerator = numerator < 0 ? -numerator : numerator;
            long absDenominator = denominator < 0 ? -denominator : denominator;
            long value = (absNumerator + absDenominator / 2) / absDenominator;
            if (negative)
                value = -value;
            if (value <= int.MinValue)
                return int.MinValue;
            if (value >= int.MaxValue)
                return int.MaxValue;
            return (int)value;
        }

        private static int NormalizeMilliDegrees(int milliDegrees)
        {
            int normalized = milliDegrees % 360000;
            if (normalized < 0)
                normalized += 360000;
            return normalized;
        }
    }
}
