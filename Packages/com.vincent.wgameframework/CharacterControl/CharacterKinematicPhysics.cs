using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.CharacterControl
{
    public readonly struct CharacterKinematicPhysicsStepInput
    {
        public CharacterKinematicPhysicsStepInput(
            CombatEntityId entityId,
            CombatBodyId bodyId,
            CombatFrame frame,
            int positionXMillimeters,
            int positionYMillimeters,
            int positionZMillimeters,
            int previousXMillimeters,
            int previousYMillimeters,
            int previousZMillimeters,
            Fix64 centerOffsetY,
            int horizontalVelocityX,
            int horizontalVelocityY,
            int verticalVelocity,
            int impactVerticalVelocity,
            bool wasGrounded,
            bool applyGroundSurfaceFriction,
            bool hasRootMotion,
            int persistentHorizontalVelocityX,
            int persistentHorizontalVelocityY,
            int persistentVerticalVelocity,
            int rootMotionHorizontalVelocityX,
            int rootMotionHorizontalVelocityY,
            int rootMotionVerticalVelocity,
            int surfaceBounceSpeedCapY,
            int maxStepHeightMillimeters,
            int groundContactProbeVelocityMillimetersPerSecond,
            int moveFacingEpsilonMilli,
            int inputAxisScale)
        {
            EntityId = entityId;
            BodyId = bodyId;
            Frame = frame;
            PositionXMillimeters = positionXMillimeters;
            PositionYMillimeters = positionYMillimeters;
            PositionZMillimeters = positionZMillimeters;
            PreviousXMillimeters = previousXMillimeters;
            PreviousYMillimeters = previousYMillimeters;
            PreviousZMillimeters = previousZMillimeters;
            CenterOffsetY = centerOffsetY;
            HorizontalVelocityX = horizontalVelocityX;
            HorizontalVelocityY = horizontalVelocityY;
            VerticalVelocity = verticalVelocity;
            ImpactVerticalVelocity = impactVerticalVelocity;
            WasGrounded = wasGrounded;
            ApplyGroundSurfaceFriction = applyGroundSurfaceFriction;
            HasRootMotion = hasRootMotion;
            PersistentHorizontalVelocityX = persistentHorizontalVelocityX;
            PersistentHorizontalVelocityY = persistentHorizontalVelocityY;
            PersistentVerticalVelocity = persistentVerticalVelocity;
            RootMotionHorizontalVelocityX = rootMotionHorizontalVelocityX;
            RootMotionHorizontalVelocityY = rootMotionHorizontalVelocityY;
            RootMotionVerticalVelocity = rootMotionVerticalVelocity;
            SurfaceBounceSpeedCapY = surfaceBounceSpeedCapY;
            MaxStepHeightMillimeters = maxStepHeightMillimeters;
            GroundContactProbeVelocityMillimetersPerSecond = groundContactProbeVelocityMillimetersPerSecond;
            MoveFacingEpsilonMilli = moveFacingEpsilonMilli;
            InputAxisScale = inputAxisScale;
            WallSlideMinimumHorizontalSpeedMillimetersPerSecond = 0;
        }

        public CharacterKinematicPhysicsStepInput(
            CombatEntityId entityId,
            CombatBodyId bodyId,
            CombatFrame frame,
            int positionXMillimeters,
            int positionYMillimeters,
            int positionZMillimeters,
            int previousXMillimeters,
            int previousYMillimeters,
            int previousZMillimeters,
            Fix64 centerOffsetY,
            int horizontalVelocityX,
            int horizontalVelocityY,
            int verticalVelocity,
            int impactVerticalVelocity,
            bool wasGrounded,
            bool applyGroundSurfaceFriction,
            bool hasRootMotion,
            int persistentHorizontalVelocityX,
            int persistentHorizontalVelocityY,
            int persistentVerticalVelocity,
            int rootMotionHorizontalVelocityX,
            int rootMotionHorizontalVelocityY,
            int rootMotionVerticalVelocity,
            int surfaceBounceSpeedCapY,
            int maxStepHeightMillimeters,
            int groundContactProbeVelocityMillimetersPerSecond,
            int moveFacingEpsilonMilli,
            int inputAxisScale,
            int wallSlideMinimumHorizontalSpeedMillimetersPerSecond)
            : this(
                entityId,
                bodyId,
                frame,
                positionXMillimeters,
                positionYMillimeters,
                positionZMillimeters,
                previousXMillimeters,
                previousYMillimeters,
                previousZMillimeters,
                centerOffsetY,
                horizontalVelocityX,
                horizontalVelocityY,
                verticalVelocity,
                impactVerticalVelocity,
                wasGrounded,
                applyGroundSurfaceFriction,
                hasRootMotion,
                persistentHorizontalVelocityX,
                persistentHorizontalVelocityY,
                persistentVerticalVelocity,
                rootMotionHorizontalVelocityX,
                rootMotionHorizontalVelocityY,
                rootMotionVerticalVelocity,
                surfaceBounceSpeedCapY,
                maxStepHeightMillimeters,
                groundContactProbeVelocityMillimetersPerSecond,
                moveFacingEpsilonMilli,
                inputAxisScale)
        {
            if (wallSlideMinimumHorizontalSpeedMillimetersPerSecond < 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(wallSlideMinimumHorizontalSpeedMillimetersPerSecond),
                    "Wall-slide minimum horizontal speed cannot be negative.");
            }

            WallSlideMinimumHorizontalSpeedMillimetersPerSecond =
                wallSlideMinimumHorizontalSpeedMillimetersPerSecond;
        }

        public CombatEntityId EntityId { get; }
        public CombatBodyId BodyId { get; }
        public CombatFrame Frame { get; }
        public int PositionXMillimeters { get; }
        public int PositionYMillimeters { get; }
        public int PositionZMillimeters { get; }
        public int PreviousXMillimeters { get; }
        public int PreviousYMillimeters { get; }
        public int PreviousZMillimeters { get; }
        public Fix64 CenterOffsetY { get; }
        public int HorizontalVelocityX { get; }
        public int HorizontalVelocityY { get; }
        public int VerticalVelocity { get; }
        public int ImpactVerticalVelocity { get; }
        public bool WasGrounded { get; }
        public bool ApplyGroundSurfaceFriction { get; }
        public bool HasRootMotion { get; }
        public int PersistentHorizontalVelocityX { get; }
        public int PersistentHorizontalVelocityY { get; }
        public int PersistentVerticalVelocity { get; }
        public int RootMotionHorizontalVelocityX { get; }
        public int RootMotionHorizontalVelocityY { get; }
        public int RootMotionVerticalVelocity { get; }
        public int SurfaceBounceSpeedCapY { get; }
        public int MaxStepHeightMillimeters { get; }
        public int GroundContactProbeVelocityMillimetersPerSecond { get; }
        public int MoveFacingEpsilonMilli { get; }
        public int InputAxisScale { get; }
        public int WallSlideMinimumHorizontalSpeedMillimetersPerSecond { get; }
    }

    public readonly struct CharacterKinematicPhysicsStepResult
    {
        public CharacterKinematicPhysicsStepResult(
            CombatMotionStepResult stepResult,
            CombatMotionState motionState,
            CombatMotionInput motionInput,
            FixVector3 impactVelocity,
            int positionXMillimeters,
            int positionYMillimeters,
            int positionZMillimeters,
            int horizontalVelocityX,
            int horizontalVelocityY,
            int verticalVelocity,
            bool grounded,
            CombatMotionCollisionFlags collisionFlags,
            int surfaceFrictionMilli,
            CombatSurfaceMaterial surfaceMaterial,
            int surfaceBounceSpeedCapY)
        {
            StepResult = stepResult;
            MotionState = motionState;
            MotionInput = motionInput;
            ImpactVelocity = impactVelocity;
            PositionXMillimeters = positionXMillimeters;
            PositionYMillimeters = positionYMillimeters;
            PositionZMillimeters = positionZMillimeters;
            HorizontalVelocityX = horizontalVelocityX;
            HorizontalVelocityY = horizontalVelocityY;
            VerticalVelocity = verticalVelocity;
            Grounded = grounded;
            CollisionFlags = collisionFlags;
            SurfaceFrictionMilli = surfaceFrictionMilli;
            SurfaceMaterial = surfaceMaterial;
            SurfaceBounceSpeedCapY = surfaceBounceSpeedCapY;
        }

        public CombatMotionStepResult StepResult { get; }
        public CombatMotionState MotionState { get; }
        public CombatMotionInput MotionInput { get; }
        public FixVector3 ImpactVelocity { get; }
        public int PositionXMillimeters { get; }
        public int PositionYMillimeters { get; }
        public int PositionZMillimeters { get; }
        public int HorizontalVelocityX { get; }
        public int HorizontalVelocityY { get; }
        public int VerticalVelocity { get; }
        public bool Grounded { get; }
        public CombatMotionCollisionFlags CollisionFlags { get; }
        public int SurfaceFrictionMilli { get; }
        public CombatSurfaceMaterial SurfaceMaterial { get; }
        public int SurfaceBounceSpeedCapY { get; }
    }

    public sealed class CharacterKinematicPhysicsAdapter
    {
        public static readonly CharacterKinematicPhysicsAdapter Default = new CharacterKinematicPhysicsAdapter();

        /// <summary>Predicts a step without modifying the physics world's body position.</summary>
        /// <remarks>NoAlloc after initialization. Uses caller-owned scratch and does not emit contacts.</remarks>
        public CharacterKinematicPhysicsStepResult Query(CombatKinematicMotor motor, CombatPhysicsWorld world,
            CombatMotionStepBuffer stepBuffer, in CharacterKinematicPhysicsStepInput input,
            CombatKinematicMotorCandidateCache candidateCache = null)
            => StepCore(motor, world, stepBuffer, in input, candidateCache, false);

        /// <remarks>NoAlloc after initialization; invalid null motor/world paths may allocate exception diagnostics.</remarks>
        public CharacterKinematicPhysicsStepResult Step(
            CombatKinematicMotor motor,
            CombatPhysicsWorld world,
            CombatMotionStepBuffer stepBuffer,
            in CharacterKinematicPhysicsStepInput input)
        {
            return StepCore(
                motor,
                world,
                stepBuffer,
                in input,
                candidateCache: null);
        }

        /// <remarks>NoAlloc after initialization; invalid null motor/world paths may allocate exception diagnostics.</remarks>
        public CharacterKinematicPhysicsStepResult Step(
            CombatKinematicMotor motor,
            CombatPhysicsWorld world,
            CombatMotionStepBuffer stepBuffer,
            in CharacterKinematicPhysicsStepInput input,
            CombatKinematicMotorCandidateCache candidateCache)
        {
            return StepCore(
                motor,
                world,
                stepBuffer,
                in input,
                candidateCache);
        }

        private static CharacterKinematicPhysicsStepResult StepCore(
            CombatKinematicMotor motor,
            CombatPhysicsWorld world,
            CombatMotionStepBuffer stepBuffer,
            in CharacterKinematicPhysicsStepInput input,
            CombatKinematicMotorCandidateCache candidateCache,
            bool syncBodyPosition = true)
        {
            if (motor == null)
                throw new System.ArgumentNullException(nameof(motor));
            if (world == null)
                throw new System.ArgumentNullException(nameof(world));

            FixVector3 centerPosition = new FixVector3(
                MillimetersToFix64Meters(input.PositionXMillimeters),
                MillimetersToFix64Meters(input.PositionYMillimeters) + input.CenterOffsetY,
                MillimetersToFix64Meters(input.PositionZMillimeters));
            if (syncBodyPosition) world.UpsertBody(new CombatPhysicsBody(input.EntityId, input.BodyId, centerPosition));

            bool hasTypedUpwardIntent = input.VerticalVelocity > 0
                || input.PersistentVerticalVelocity > 0
                || (input.HasRootMotion && input.RootMotionVerticalVelocity > 0);
            bool snapEnabled = motor.Config.GroundSnapDistance > Fix64.Zero;
            int physicsVerticalVelocity = input.VerticalVelocity;
            if (input.WasGrounded && input.VerticalVelocity <= 0)
            {
                physicsVerticalVelocity = snapEnabled
                    ? (hasTypedUpwardIntent ? input.VerticalVelocity : 0)
                    : input.GroundContactProbeVelocityMillimetersPerSecond;
            }
            FixVector3 velocity = new FixVector3(
                MillimetersToFix64Meters(input.HorizontalVelocityX),
                MillimetersToFix64Meters(physicsVerticalVelocity),
                MillimetersToFix64Meters(input.HorizontalVelocityY));
            CombatMotionState state = new CombatMotionState(
                input.Frame,
                centerPosition,
                velocity,
                input.WasGrounded,
                input.WasGrounded ? new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero) : FixVector3.Zero,
                input.WasGrounded ? CombatMotionCollisionFlags.Grounded : CombatMotionCollisionFlags.None);
            CombatMotionInput motionInput = CreatePhysicsMotionInput(
                input.HorizontalVelocityX,
                input.HorizontalVelocityY,
                input.ApplyGroundSurfaceFriction,
                input.MoveFacingEpsilonMilli,
                input.InputAxisScale);
            FixVector3 impactVelocity = new FixVector3(
                MillimetersToFix64Meters(input.HorizontalVelocityX),
                MillimetersToFix64Meters(input.ImpactVerticalVelocity),
                MillimetersToFix64Meters(input.HorizontalVelocityY));
            CombatMotionStepResult step = motor.Step(
                world,
                input.BodyId,
                state,
                motionInput,
                impactVelocity,
                stepBuffer,
                candidateCache,
                new CombatMotionStepOptions(
                    hasTypedUpwardIntent,
                    MillimetersToFix64Meters(
                        input.WallSlideMinimumHorizontalSpeedMillimetersPerSecond)), syncBodyPosition);
            return BuildResult(in input, step, state, motionInput, impactVelocity);
        }

        private static CharacterKinematicPhysicsStepResult BuildResult(
            in CharacterKinematicPhysicsStepInput input,
            CombatMotionStepResult step,
            CombatMotionState state,
            CombatMotionInput motionInput,
            FixVector3 impactVelocity)
        {
            FixVector3 physicsPosition = step.State.Position;
            FixVector3 physicsVelocity = step.State.Velocity;
            int x = Fix64MetersToMillimeters(physicsPosition.X);
            int y = Fix64MetersToMillimeters(physicsPosition.Y - input.CenterOffsetY);
            int z = Fix64MetersToMillimeters(physicsPosition.Z);
            int resolvedHorizontalVelocityX = Fix64MetersToMillimeters(physicsVelocity.X);
            int resolvedHorizontalVelocityY = Fix64MetersToMillimeters(physicsVelocity.Z);
            int resolvedVerticalVelocity = Fix64MetersToMillimeters(physicsVelocity.Y);

            int horizontalVelocityX;
            int horizontalVelocityY;
            int verticalVelocity;
            if (input.HasRootMotion)
            {
                CharacterMotionRequestAccumulator.ResolvePersistentHorizontalVelocityAfterRootMotion(
                    input.PersistentHorizontalVelocityX,
                    input.PersistentHorizontalVelocityY,
                    resolvedHorizontalVelocityX,
                    resolvedHorizontalVelocityY,
                    input.RootMotionHorizontalVelocityX,
                    input.RootMotionHorizontalVelocityY,
                    input.MoveFacingEpsilonMilli,
                    out horizontalVelocityX,
                    out horizontalVelocityY);
                verticalVelocity = CharacterMotionRequestAccumulator.ResolvePersistentVerticalVelocityAfterRootMotion(
                    input.PersistentVerticalVelocity,
                    resolvedVerticalVelocity,
                    input.RootMotionVerticalVelocity,
                    input.MoveFacingEpsilonMilli);
            }
            else
            {
                horizontalVelocityX = resolvedHorizontalVelocityX;
                horizontalVelocityY = resolvedHorizontalVelocityY;
                verticalVelocity = resolvedVerticalVelocity;
            }

            CombatMotionCollisionFlags collisionFlags = step.CollisionFlags;
            bool grounded = step.State.Grounded
                || (collisionFlags & CombatMotionCollisionFlags.Grounded) == CombatMotionCollisionFlags.Grounded;
            int verticalLiftMillimeters = y - input.PreviousYMillimeters;
            if (input.MaxStepHeightMillimeters > 0
                && verticalLiftMillimeters > 0
                && verticalLiftMillimeters <= input.MaxStepHeightMillimeters
                && (collisionFlags & CombatMotionCollisionFlags.Grounded) == CombatMotionCollisionFlags.Grounded
                && (x != input.PreviousXMillimeters || z != input.PreviousZMillimeters))
            {
                grounded = true;
                collisionFlags |= CombatMotionCollisionFlags.Grounded | CombatMotionCollisionFlags.StepUp;
            }

            int surfaceBounceSpeedCapY = ResolveSurfaceBounceSpeedCapY(
                input.SurfaceBounceSpeedCapY,
                step,
                verticalVelocity);
            return new CharacterKinematicPhysicsStepResult(
                step,
                state,
                motionInput,
                impactVelocity,
                x,
                y,
                z,
                horizontalVelocityX,
                horizontalVelocityY,
                verticalVelocity,
                grounded,
                collisionFlags,
                ScaleToMilli(step.SurfaceFriction),
                step.SurfaceMaterial,
                surfaceBounceSpeedCapY);
        }

        private static CombatMotionInput CreatePhysicsMotionInput(
            int horizontalVelocityX,
            int horizontalVelocityY,
            bool applyGroundSurfaceFriction,
            int moveFacingEpsilonMilli,
            int inputAxisScale)
        {
            int speed = Magnitude(horizontalVelocityX, horizontalVelocityY);
            if (speed <= moveFacingEpsilonMilli)
                return CombatMotionInput.None;

            NormalizeToScale(
                horizontalVelocityX,
                horizontalVelocityY,
                inputAxisScale,
                out int directionX,
                out int directionY);
            return new CombatMotionInput(
                new FixVector3(
                    Fix64.FromRaw((long)directionX * Fix64.Scale / inputAxisScale),
                    Fix64.Zero,
                    Fix64.FromRaw((long)directionY * Fix64.Scale / inputAxisScale)),
                jumpPressed: false,
                MillimetersToFix64Meters(speed),
                applyGroundSurfaceFriction);
        }

        private static int ResolveSurfaceBounceSpeedCapY(
            int currentCap,
            CombatMotionStepResult physicsStep,
            int resolvedVerticalVelocity)
        {
            if (resolvedVerticalVelocity > 0
                && (physicsStep.SurfaceElasticity > Fix64.Zero
                    || physicsStep.SurfaceFixedBounceHeight > Fix64.Zero))
            {
                return resolvedVerticalVelocity;
            }

            if (physicsStep.CollisionCount > 0 || physicsStep.State.Grounded)
                return 0;

            return currentCap > 0 ? currentCap : 0;
        }

        private static Fix64 MillimetersToFix64Meters(int millimeters)
        {
            return Fix64.FromRaw(((long)millimeters * Fix64.Scale) / 1000);
        }

        private static int Fix64MetersToMillimeters(Fix64 meters)
        {
            return DivideRoundToInt(meters.RawValue * 1000, Fix64.Scale);
        }

        private static int ScaleToMilli(Fix64 scale)
        {
            if (scale <= Fix64.Zero)
                return 0;

            return DivideRoundToInt(scale.RawValue * 1000, Fix64.Scale);
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

        private static int Magnitude(int x, int y)
        {
            return IntegerSqrt((long)x * x + (long)y * y);
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
}
