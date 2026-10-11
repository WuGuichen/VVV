using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestCharacterMotionRequestKind
    {
        None = 0,
        Impulse = 1,
        RootMotionDelta = 2,
        VelocityOverride = 3,
        LockMovement = 4,
        ExternalForce = 5,
        ControlScale = 6,
        HorizontalVelocityOverride = 7,
        VerticalVelocityOverride = 8,
        IgnoreGravity = 9,
    }

    public enum CharacterTestCharacterMotionRequestSpace
    {
        World = 0,
        Local = 1,
    }

    public readonly struct CharacterTestCharacterMotionRequest
    {
        public CharacterTestCharacterMotionRequest(
            CharacterTestCharacterMotionRequestKind kind,
            Vector3 value,
            CharacterTestCharacterMotionRequestSpace space,
            CharacterMovementMode movementMode,
            bool lockMovement,
            float movementControlScale,
            float rotationControlScale,
            bool hasMovementControlScale,
            bool hasRotationControlScale,
            CharacterActionId sourceActionId,
            int stableEventId,
            bool bypassGroundDamping = false,
            CharacterMotionObstaclePolicy obstaclePolicy = CharacterMotionObstaclePolicy.Default)
            : this(
                kind,
                QuantizeRaw(kind, value.x),
                QuantizeRaw(kind, value.y),
                QuantizeRaw(kind, value.z),
                space,
                movementMode,
                lockMovement,
                CharacterTestDeterministicUnits.ScaleToMilli(movementControlScale),
                CharacterTestDeterministicUnits.ScaleToMilli(rotationControlScale),
                hasMovementControlScale,
                hasRotationControlScale,
                sourceActionId,
                stableEventId,
                deterministic: true,
                bypassGroundDamping: bypassGroundDamping,
                obstaclePolicy: obstaclePolicy)
        {
        }

        private CharacterTestCharacterMotionRequest(
            CharacterTestCharacterMotionRequestKind kind,
            int xRaw,
            int yRaw,
            int zRaw,
            CharacterTestCharacterMotionRequestSpace space,
            CharacterMovementMode movementMode,
            bool lockMovement,
            int movementControlScaleMilli,
            int rotationControlScaleMilli,
            bool hasMovementControlScale,
            bool hasRotationControlScale,
            CharacterActionId sourceActionId,
            int stableEventId,
            bool deterministic,
            bool bypassGroundDamping = false,
            CharacterMotionObstaclePolicy obstaclePolicy = CharacterMotionObstaclePolicy.Default)
        {
            Kind = kind;
            XRaw = xRaw;
            YRaw = yRaw;
            ZRaw = zRaw;
            Space = space;
            MovementMode = movementMode;
            LocksMovement = lockMovement;
            MovementControlScaleMilli = CharacterTestDeterministicMath.ClampScaleMilli(movementControlScaleMilli);
            RotationControlScaleMilli = CharacterTestDeterministicMath.ClampScaleMilli(rotationControlScaleMilli);
            HasMovementControlScale = hasMovementControlScale;
            HasRotationControlScale = hasRotationControlScale;
            SourceActionId = sourceActionId;
            StableEventId = stableEventId;
            BypassesGroundDamping = bypassGroundDamping;
            ObstaclePolicy = obstaclePolicy;
        }

        public CharacterTestCharacterMotionRequestKind Kind { get; }
        public int XRaw { get; }
        public int YRaw { get; }
        public int ZRaw { get; }
        public Vector3 Value => new Vector3(
            CharacterTestDeterministicUnits.MillimetersToMeters(XRaw),
            CharacterTestDeterministicUnits.MillimetersToMeters(YRaw),
            CharacterTestDeterministicUnits.MillimetersToMeters(ZRaw));
        public CharacterTestCharacterMotionRequestSpace Space { get; }
        public CharacterMovementMode MovementMode { get; }
        public bool LocksMovement { get; }
        public int MovementControlScaleMilli { get; }
        public int RotationControlScaleMilli { get; }
        public float MovementControlScale =>
            CharacterTestDeterministicUnits.MilliToScale(MovementControlScaleMilli);
        public float RotationControlScale =>
            CharacterTestDeterministicUnits.MilliToScale(RotationControlScaleMilli);
        public bool HasMovementControlScale { get; }
        public bool HasRotationControlScale { get; }
        public CharacterActionId SourceActionId { get; }
        public int StableEventId { get; }
        public bool BypassesGroundDamping { get; }
        public CharacterMotionObstaclePolicy ObstaclePolicy { get; }
        public bool HasValue => Kind != CharacterTestCharacterMotionRequestKind.None;

        /// <remarks>NoAlloc after initialization.</remarks>
        public CharacterMotionRequest ToCharacterMotionRequest()
        {
            return new CharacterMotionRequest(
                ToCharacterMotionRequestKind(Kind),
                XRaw,
                YRaw,
                ZRaw,
                Space == CharacterTestCharacterMotionRequestSpace.Local
                    ? CharacterMotionRequestSpace.Local
                    : CharacterMotionRequestSpace.World,
                (int)MovementMode,
                LocksMovement,
                MovementControlScaleMilli,
                RotationControlScaleMilli,
                HasMovementControlScale,
                HasRotationControlScale,
                SourceActionId,
                StableEventId,
                BypassesGroundDamping,
                ObstaclePolicy);
        }

        public static CharacterTestCharacterMotionRequest Impulse(
            Vector3 velocity,
            CharacterMovementMode movementMode,
            CharacterActionId sourceActionId,
            int stableEventId,
            CharacterTestCharacterMotionRequestSpace space = CharacterTestCharacterMotionRequestSpace.World)
        {
            return ImpulseRaw(
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(velocity.x),
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(velocity.y),
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(velocity.z),
                movementMode,
                sourceActionId,
                stableEventId,
                space);
        }

        public static CharacterTestCharacterMotionRequest ImpulseRaw(
            int velocityXMillimetersPerSecond,
            int velocityYMillimetersPerSecond,
            int velocityZMillimetersPerSecond,
            CharacterMovementMode movementMode,
            CharacterActionId sourceActionId,
            int stableEventId,
            CharacterTestCharacterMotionRequestSpace space = CharacterTestCharacterMotionRequestSpace.World)
        {
            return new CharacterTestCharacterMotionRequest(
                CharacterTestCharacterMotionRequestKind.Impulse,
                velocityXMillimetersPerSecond,
                velocityYMillimetersPerSecond,
                velocityZMillimetersPerSecond,
                space,
                movementMode,
                lockMovement: false,
                movementControlScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                rotationControlScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                hasMovementControlScale: false,
                hasRotationControlScale: false,
                sourceActionId: sourceActionId,
                stableEventId: stableEventId,
                deterministic: true);
        }

        public static CharacterTestCharacterMotionRequest RootMotionDelta(
            Vector3 delta,
            CharacterTestCharacterMotionRequestSpace space,
            CharacterActionId sourceActionId,
            int stableEventId,
            CharacterMotionObstaclePolicy obstaclePolicy = CharacterMotionObstaclePolicy.Default)
        {
            return RootMotionDeltaRaw(
                CharacterTestDeterministicUnits.MetersToMillimeters(delta.x),
                CharacterTestDeterministicUnits.MetersToMillimeters(delta.y),
                CharacterTestDeterministicUnits.MetersToMillimeters(delta.z),
                space,
                sourceActionId,
                stableEventId,
                obstaclePolicy);
        }

        public static CharacterTestCharacterMotionRequest RootMotionDeltaRaw(
            int deltaXMillimeters,
            int deltaYMillimeters,
            int deltaZMillimeters,
            CharacterTestCharacterMotionRequestSpace space,
            CharacterActionId sourceActionId,
            int stableEventId,
            CharacterMotionObstaclePolicy obstaclePolicy = CharacterMotionObstaclePolicy.Default)
        {
            return new CharacterTestCharacterMotionRequest(
                CharacterTestCharacterMotionRequestKind.RootMotionDelta,
                deltaXMillimeters,
                deltaYMillimeters,
                deltaZMillimeters,
                space,
                CharacterMovementMode.RootMotionDriven,
                lockMovement: false,
                movementControlScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                rotationControlScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                hasMovementControlScale: false,
                hasRotationControlScale: false,
                sourceActionId: sourceActionId,
                stableEventId: stableEventId,
                deterministic: true,
                obstaclePolicy: obstaclePolicy);
        }

        public static CharacterTestCharacterMotionRequest LockMovement(
            CharacterMovementMode movementMode,
            CharacterActionId sourceActionId,
            int stableEventId)
        {
            return new CharacterTestCharacterMotionRequest(
                CharacterTestCharacterMotionRequestKind.LockMovement,
                0,
                0,
                0,
                CharacterTestCharacterMotionRequestSpace.World,
                movementMode == CharacterMovementMode.Idle ? CharacterMovementMode.ControlLocked : movementMode,
                lockMovement: true,
                movementControlScaleMilli: 0,
                rotationControlScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                hasMovementControlScale: true,
                hasRotationControlScale: true,
                sourceActionId: sourceActionId,
                stableEventId: stableEventId,
                deterministic: true);
        }

        public static CharacterTestCharacterMotionRequest ControlScale(
            float movementControlScale,
            float rotationControlScale,
            CharacterMovementMode movementMode,
            CharacterActionId sourceActionId,
            int stableEventId,
            bool hasMovementControlScale = true,
            bool hasRotationControlScale = true)
        {
            return ControlScaleRaw(
                CharacterTestDeterministicUnits.ScaleToMilli(movementControlScale),
                CharacterTestDeterministicUnits.ScaleToMilli(rotationControlScale),
                movementMode,
                sourceActionId,
                stableEventId,
                hasMovementControlScale,
                hasRotationControlScale);
        }

        public static CharacterTestCharacterMotionRequest ControlScaleRaw(
            int movementControlScaleMilli,
            int rotationControlScaleMilli,
            CharacterMovementMode movementMode,
            CharacterActionId sourceActionId,
            int stableEventId,
            bool hasMovementControlScale = true,
            bool hasRotationControlScale = true)
        {
            return new CharacterTestCharacterMotionRequest(
                CharacterTestCharacterMotionRequestKind.ControlScale,
                0,
                0,
                0,
                CharacterTestCharacterMotionRequestSpace.World,
                movementMode,
                lockMovement: false,
                movementControlScaleMilli: movementControlScaleMilli,
                rotationControlScaleMilli: rotationControlScaleMilli,
                hasMovementControlScale: hasMovementControlScale,
                hasRotationControlScale: hasRotationControlScale,
                sourceActionId: sourceActionId,
                stableEventId: stableEventId,
                deterministic: true);
        }

        public static CharacterTestCharacterMotionRequest VelocityOverride(
            Vector3 velocity,
            CharacterTestCharacterMotionRequestSpace space,
            CharacterMovementMode movementMode,
            CharacterActionId sourceActionId,
            int stableEventId)
        {
            return VelocityOverrideRaw(
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(velocity.x),
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(velocity.y),
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(velocity.z),
                space,
                movementMode,
                sourceActionId,
                stableEventId);
        }

        public static CharacterTestCharacterMotionRequest VelocityOverrideRaw(
            int velocityXMillimetersPerSecond,
            int velocityYMillimetersPerSecond,
            int velocityZMillimetersPerSecond,
            CharacterTestCharacterMotionRequestSpace space,
            CharacterMovementMode movementMode,
            CharacterActionId sourceActionId,
            int stableEventId)
        {
            return new CharacterTestCharacterMotionRequest(
                CharacterTestCharacterMotionRequestKind.VelocityOverride,
                velocityXMillimetersPerSecond,
                velocityYMillimetersPerSecond,
                velocityZMillimetersPerSecond,
                space,
                movementMode,
                lockMovement: false,
                movementControlScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                rotationControlScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                hasMovementControlScale: false,
                hasRotationControlScale: false,
                sourceActionId: sourceActionId,
                stableEventId: stableEventId,
                deterministic: true);
        }

        public static CharacterTestCharacterMotionRequest HorizontalVelocityOverride(
            Vector3 velocity,
            CharacterMovementMode movementMode,
            CharacterActionId sourceActionId,
            int stableEventId,
            CharacterTestCharacterMotionRequestSpace space = CharacterTestCharacterMotionRequestSpace.World)
        {
            return HorizontalVelocityOverrideRaw(
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(velocity.x),
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(velocity.z),
                movementMode,
                sourceActionId,
                stableEventId,
                space);
        }

        public static CharacterTestCharacterMotionRequest VerticalVelocityOverride(
            float velocityY,
            CharacterActionId sourceActionId,
            int stableEventId)
        {
            return new CharacterTestCharacterMotionRequest(
                CharacterTestCharacterMotionRequestKind.VerticalVelocityOverride,
                new Vector3(0f, velocityY, 0f),
                CharacterTestCharacterMotionRequestSpace.World,
                CharacterMovementMode.Idle,
                false, 1f, 1f, false, false,
                sourceActionId, stableEventId);
        }

        public static CharacterTestCharacterMotionRequest IgnoreGravity(
            CharacterActionId sourceActionId,
            int stableEventId)
        {
            return new CharacterTestCharacterMotionRequest(
                CharacterTestCharacterMotionRequestKind.IgnoreGravity,
                Vector3.zero,
                CharacterTestCharacterMotionRequestSpace.World,
                CharacterMovementMode.Idle,
                false, 1f, 1f, false, false,
                sourceActionId, stableEventId);
        }

        public static CharacterTestCharacterMotionRequest HorizontalVelocityOverrideRaw(
            int velocityXMillimetersPerSecond,
            int velocityZMillimetersPerSecond,
            CharacterMovementMode movementMode,
            CharacterActionId sourceActionId,
            int stableEventId,
            CharacterTestCharacterMotionRequestSpace space = CharacterTestCharacterMotionRequestSpace.World)
        {
            return new CharacterTestCharacterMotionRequest(
                CharacterTestCharacterMotionRequestKind.HorizontalVelocityOverride,
                velocityXMillimetersPerSecond,
                0,
                velocityZMillimetersPerSecond,
                space,
                movementMode,
                lockMovement: false,
                movementControlScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                rotationControlScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                hasMovementControlScale: false,
                hasRotationControlScale: false,
                sourceActionId: sourceActionId,
                stableEventId: stableEventId,
                deterministic: true);
        }

        public static CharacterTestCharacterMotionRequest ExternalForce(
            Vector3 acceleration,
            CharacterTestCharacterMotionRequestSpace space,
            CharacterActionId sourceActionId,
            int stableEventId,
            bool bypassGroundDamping = false)
        {
            return ExternalForceRaw(
                CharacterTestDeterministicUnits.AccelerationToMillimetersPerSecondSquared(acceleration.x),
                CharacterTestDeterministicUnits.AccelerationToMillimetersPerSecondSquared(acceleration.y),
                CharacterTestDeterministicUnits.AccelerationToMillimetersPerSecondSquared(acceleration.z),
                space,
                sourceActionId,
                stableEventId,
                bypassGroundDamping);
        }

        public static CharacterTestCharacterMotionRequest ExternalForceRaw(
            int accelerationXMillimetersPerSecondSquared,
            int accelerationYMillimetersPerSecondSquared,
            int accelerationZMillimetersPerSecondSquared,
            CharacterTestCharacterMotionRequestSpace space,
            CharacterActionId sourceActionId,
            int stableEventId,
            bool bypassGroundDamping = false)
        {
            return new CharacterTestCharacterMotionRequest(
                CharacterTestCharacterMotionRequestKind.ExternalForce,
                accelerationXMillimetersPerSecondSquared,
                accelerationYMillimetersPerSecondSquared,
                accelerationZMillimetersPerSecondSquared,
                space,
                CharacterMovementMode.Idle,
                lockMovement: false,
                movementControlScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                rotationControlScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                hasMovementControlScale: false,
                hasRotationControlScale: false,
                sourceActionId: sourceActionId,
                stableEventId: stableEventId,
                deterministic: true,
                bypassGroundDamping: bypassGroundDamping);
        }

        private static int QuantizeRaw(CharacterTestCharacterMotionRequestKind kind, float value)
        {
            switch (kind)
            {
                case CharacterTestCharacterMotionRequestKind.ExternalForce:
                    return CharacterTestDeterministicUnits.AccelerationToMillimetersPerSecondSquared(value);
                case CharacterTestCharacterMotionRequestKind.Impulse:
                case CharacterTestCharacterMotionRequestKind.VelocityOverride:
                case CharacterTestCharacterMotionRequestKind.HorizontalVelocityOverride:
                case CharacterTestCharacterMotionRequestKind.VerticalVelocityOverride:
                    return CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(value);
                default:
                    return CharacterTestDeterministicUnits.MetersToMillimeters(value);
            }
        }

        private static CharacterMotionRequestKind ToCharacterMotionRequestKind(
            CharacterTestCharacterMotionRequestKind kind)
        {
            switch (kind)
            {
                case CharacterTestCharacterMotionRequestKind.Impulse:
                    return CharacterMotionRequestKind.Impulse;
                case CharacterTestCharacterMotionRequestKind.RootMotionDelta:
                    return CharacterMotionRequestKind.RootMotionDelta;
                case CharacterTestCharacterMotionRequestKind.VelocityOverride:
                    return CharacterMotionRequestKind.VelocityOverride;
                case CharacterTestCharacterMotionRequestKind.HorizontalVelocityOverride:
                    return CharacterMotionRequestKind.HorizontalVelocityOverride;
                case CharacterTestCharacterMotionRequestKind.VerticalVelocityOverride:
                    return CharacterMotionRequestKind.VerticalVelocityOverride;
                case CharacterTestCharacterMotionRequestKind.IgnoreGravity:
                    return CharacterMotionRequestKind.IgnoreGravity;
                case CharacterTestCharacterMotionRequestKind.LockMovement:
                    return CharacterMotionRequestKind.LockMovement;
                case CharacterTestCharacterMotionRequestKind.ExternalForce:
                    return CharacterMotionRequestKind.ExternalForce;
                case CharacterTestCharacterMotionRequestKind.ControlScale:
                    return CharacterMotionRequestKind.ControlScale;
                default:
                    return CharacterMotionRequestKind.None;
            }
        }
    }
}
