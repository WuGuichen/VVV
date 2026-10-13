using System;
using MxFramework.CharacterAction;

namespace MxFramework.CharacterControl
{
    public enum CharacterMotionRequestKind
    {
        None = 0,
        Impulse = 1,
        RootMotionDelta = 2,
        VelocityOverride = 3,
        LockMovement = 4,
        ExternalForce = 5,
        ControlScale = 6,
        MovementMode = 7,
        HorizontalVelocityOverride = 8,
        VerticalVelocityOverride = 9,
        IgnoreGravity = 10,
    }

    public enum CharacterMotionRequestSpace
    {
        World = 0,
        Local = 1,
    }

    public enum CharacterMotionObstaclePolicy
    {
        Default = 0,
        StaticOnly = 1,
    }

    public interface ICharacterMotionLocalSpaceResolver
    {
        /// <remarks>NoAlloc after initialization.</remarks>
        void ResolveLocalToWorld(
            int yawMilliDegrees,
            int localX,
            int localY,
            int localZ,
            out int worldX,
            out int worldY,
            out int worldZ);
    }

    public readonly struct CharacterMotionRequestSource
    {
        public CharacterMotionRequestSource(CharacterActionId sourceActionId, int stableEventId)
        {
            SourceActionId = sourceActionId;
            StableEventId = stableEventId;
        }

        public CharacterActionId SourceActionId { get; }
        public int StableEventId { get; }
        public bool IsEmpty =>
            !SourceActionId.IsValid
            && (StableEventId <= 0);
    }

    public readonly struct CharacterMotionRequest
    {
        public const int DefaultControlScaleMilli = 1000;
        public const int MaximumControlScaleMilli = 10000;

        public CharacterMotionRequest(
            CharacterMotionRequestKind kind,
            int xRaw,
            int yRaw,
            int zRaw,
            CharacterMotionRequestSpace space,
            int movementModeId,
            bool lockMovement,
            int movementControlScaleMilli,
            int rotationControlScaleMilli,
            bool hasMovementControlScale,
            bool hasRotationControlScale,
            CharacterActionId sourceActionId,
            int stableEventId,
            bool bypassGroundDamping = false,
            CharacterMotionObstaclePolicy obstaclePolicy = CharacterMotionObstaclePolicy.Default)
        {
            Kind = kind;
            XRaw = xRaw;
            YRaw = yRaw;
            ZRaw = zRaw;
            Space = space;
            MovementModeId = movementModeId;
            LocksMovement = lockMovement;
            MovementControlScaleMilli = ClampScaleMilli(movementControlScaleMilli);
            RotationControlScaleMilli = ClampScaleMilli(rotationControlScaleMilli);
            HasMovementControlScale = hasMovementControlScale;
            HasRotationControlScale = hasRotationControlScale;
            Source = new CharacterMotionRequestSource(sourceActionId, stableEventId);
            BypassesGroundDamping = bypassGroundDamping;
            ObstaclePolicy = obstaclePolicy;
        }

        public CharacterMotionRequestKind Kind { get; }
        public int XRaw { get; }
        public int YRaw { get; }
        public int ZRaw { get; }
        public CharacterMotionRequestSpace Space { get; }
        public int MovementModeId { get; }
        public bool LocksMovement { get; }
        public int MovementControlScaleMilli { get; }
        public int RotationControlScaleMilli { get; }
        public bool HasMovementControlScale { get; }
        public bool HasRotationControlScale { get; }
        public CharacterMotionRequestSource Source { get; }
        public bool BypassesGroundDamping { get; }
        public CharacterMotionObstaclePolicy ObstaclePolicy { get; }
        public bool HasValue => Kind != CharacterMotionRequestKind.None;

        public static int ClampScaleMilli(int value)
        {
            if (value < 0)
                return 0;

            return value > MaximumControlScaleMilli ? MaximumControlScaleMilli : value;
        }
    }

    public struct CharacterMotionRequestFrame
    {
        public bool HasImpulse;
        public int ImpulseVelocityX;
        public int ImpulseVelocityY;
        public int ImpulseVelocityZ;
        public CharacterMotionRequestSource ImpulseSource;
        public bool HasRootMotion;
        public int RootMotionDeltaX;
        public int RootMotionDeltaY;
        public int RootMotionDeltaZ;
        public CharacterMotionRequestSource RootMotionSource;
        public CharacterMotionObstaclePolicy RootMotionObstaclePolicy;
        public bool HasVelocityOverride;
        public int VelocityOverrideX;
        public int VelocityOverrideY;
        public int VelocityOverrideZ;
        public bool HasHorizontalVelocityOverride;
        public int HorizontalVelocityOverrideX;
        public int HorizontalVelocityOverrideZ;
        public bool HasVerticalVelocityOverride;
        public int VerticalVelocityOverrideY;
        public bool IgnoreGravity;
        public bool LockMovement;
        public bool HasMovementControlScale;
        public bool HasRotationControlScale;
        public int MovementControlScaleMilli;
        public int RotationControlScaleMilli;
        public CharacterMotionRequestSource ControlScaleSource;
        public bool HasExternalAcceleration;
        public int ExternalAccelerationX;
        public int ExternalAccelerationY;
        public int ExternalAccelerationZ;
        public bool ExternalAccelerationBypassesGroundDamping;
        public int MovementModeId;
    }

    public struct CharacterMotionRequestAccumulator
    {
        private const int RootMotionDrivenMovementModeId = (int)CharacterMovementMode.RootMotionDriven;
        private CharacterMotionRequestFrame _frame;

        /// <remarks>NoAlloc after initialization.</remarks>
        public void Reset()
        {
            _frame = default;
            _frame.MovementControlScaleMilli = CharacterMotionRequest.DefaultControlScaleMilli;
            _frame.RotationControlScaleMilli = CharacterMotionRequest.DefaultControlScaleMilli;
        }

        /// <remarks>NoAlloc after initialization; invalid local-space resolver paths may allocate exception diagnostics.</remarks>
        public void Accumulate(
            in CharacterMotionRequest request,
            int yawMilliDegrees,
            ICharacterMotionLocalSpaceResolver localSpaceResolver)
        {
            ResolveWorldRawVector(
                request,
                yawMilliDegrees,
                localSpaceResolver,
                out int valueX,
                out int valueY,
                out int valueZ);

            switch (request.Kind)
            {
                case CharacterMotionRequestKind.Impulse:
                    _frame.HasImpulse = true;
                    _frame.ImpulseVelocityX += valueX;
                    _frame.ImpulseVelocityY += valueY;
                    _frame.ImpulseVelocityZ += valueZ;
                    CaptureFirstSource(ref _frame.ImpulseSource, request.Source);
                    ApplyRequestedMovementMode(request.MovementModeId);
                    break;

                case CharacterMotionRequestKind.RootMotionDelta:
                    _frame.HasRootMotion = true;
                    _frame.RootMotionDeltaX += valueX;
                    _frame.RootMotionDeltaY += valueY;
                    _frame.RootMotionDeltaZ += valueZ;
                    CaptureFirstSource(ref _frame.RootMotionSource, request.Source);
                    if (_frame.RootMotionObstaclePolicy == CharacterMotionObstaclePolicy.Default
                        || request.ObstaclePolicy != CharacterMotionObstaclePolicy.Default)
                    {
                        _frame.RootMotionObstaclePolicy = request.ObstaclePolicy;
                    }

                    ApplyRequestedMovementMode(request.MovementModeId);
                    break;

                case CharacterMotionRequestKind.VelocityOverride:
                    _frame.HasVelocityOverride = true;
                    _frame.VelocityOverrideX = valueX;
                    _frame.VelocityOverrideY = valueY;
                    _frame.VelocityOverrideZ = valueZ;
                    ApplyRequestedMovementMode(request.MovementModeId);
                    break;

                case CharacterMotionRequestKind.HorizontalVelocityOverride:
                    _frame.HasHorizontalVelocityOverride = true;
                    _frame.HorizontalVelocityOverrideX = valueX;
                    _frame.HorizontalVelocityOverrideZ = valueZ;
                    ApplyRequestedMovementMode(request.MovementModeId);
                    break;

                case CharacterMotionRequestKind.VerticalVelocityOverride:
                    _frame.HasVerticalVelocityOverride = true;
                    _frame.VerticalVelocityOverrideY = valueY;
                    break;

                case CharacterMotionRequestKind.IgnoreGravity:
                    _frame.IgnoreGravity = true;
                    break;

                case CharacterMotionRequestKind.LockMovement:
                    _frame.LockMovement = true;
                    _frame.HasMovementControlScale = true;
                    _frame.HasRotationControlScale = true;
                    _frame.MovementControlScaleMilli = 0;
                    _frame.RotationControlScaleMilli = CharacterMotionRequest.DefaultControlScaleMilli;
                    CaptureFirstSource(ref _frame.ControlScaleSource, request.Source);
                    ApplyRequestedMovementMode(request.MovementModeId);
                    break;

                case CharacterMotionRequestKind.ExternalForce:
                    _frame.HasExternalAcceleration = true;
                    _frame.ExternalAccelerationX += valueX;
                    _frame.ExternalAccelerationY += valueY;
                    _frame.ExternalAccelerationZ += valueZ;
                    _frame.ExternalAccelerationBypassesGroundDamping |= request.BypassesGroundDamping;
                    ApplyRequestedMovementMode(request.MovementModeId);
                    break;

                case CharacterMotionRequestKind.ControlScale:
                    if (request.HasMovementControlScale)
                    {
                        _frame.HasMovementControlScale = true;
                        _frame.MovementControlScaleMilli = request.MovementControlScaleMilli;
                    }

                    if (request.HasRotationControlScale)
                    {
                        _frame.HasRotationControlScale = true;
                        _frame.RotationControlScaleMilli = request.RotationControlScaleMilli;
                    }

                    CaptureFirstSource(ref _frame.ControlScaleSource, request.Source);
                    break;

                case CharacterMotionRequestKind.MovementMode:
                    ApplyRequestedMovementMode(request.MovementModeId);
                    break;
            }

            if (request.LocksMovement)
                _frame.LockMovement = true;
        }

        public CharacterMotionRequestFrame ToFrame()
        {
            return _frame;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public static int ResolveControlScaleMilli(
            CharacterMotionRequestFrame requestFrame,
            int fallbackScaleMilli)
        {
            return CharacterMotionRequest.ClampScaleMilli(
                requestFrame.HasMovementControlScale
                    ? requestFrame.MovementControlScaleMilli
                    : fallbackScaleMilli);
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public static int ResolveRotationScaleMilli(
            CharacterMotionRequestFrame requestFrame,
            int fallbackScaleMilli)
        {
            return CharacterMotionRequest.ClampScaleMilli(
                requestFrame.HasRotationControlScale
                    ? requestFrame.RotationControlScaleMilli
                    : fallbackScaleMilli);
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public static void ResolvePersistentHorizontalVelocityAfterRootMotion(
            int baseVelocityX,
            int baseVelocityY,
            int resolvedVelocityX,
            int resolvedVelocityY,
            int rootMotionVelocityX,
            int rootMotionVelocityY,
            int epsilon,
            out int persistentVelocityX,
            out int persistentVelocityY)
        {
            if (SqrMagnitude(rootMotionVelocityX, rootMotionVelocityY) <= epsilon)
            {
                persistentVelocityX = resolvedVelocityX;
                persistentVelocityY = resolvedVelocityY;
                return;
            }

            if (SqrMagnitude(baseVelocityX, baseVelocityY) <= epsilon)
            {
                persistentVelocityX = 0;
                persistentVelocityY = 0;
                return;
            }

            persistentVelocityX = resolvedVelocityX - rootMotionVelocityX;
            persistentVelocityY = resolvedVelocityY - rootMotionVelocityY;
            long dot = (long)persistentVelocityX * baseVelocityX + (long)persistentVelocityY * baseVelocityY;
            if (dot < 0)
            {
                persistentVelocityX = 0;
                persistentVelocityY = 0;
            }
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public static int ResolvePersistentVerticalVelocityAfterRootMotion(
            int baseVelocity,
            int resolvedVelocity,
            int rootMotionVelocity,
            int epsilon)
        {
            if (Math.Abs(rootMotionVelocity) <= epsilon)
                return resolvedVelocity;

            if (Math.Abs(baseVelocity) <= epsilon)
                return 0;

            int persistentVelocity = resolvedVelocity - rootMotionVelocity;
            return SameSign(persistentVelocity, baseVelocity) ? persistentVelocity : 0;
        }

        private static void ResolveWorldRawVector(
            in CharacterMotionRequest request,
            int yawMilliDegrees,
            ICharacterMotionLocalSpaceResolver localSpaceResolver,
            out int valueX,
            out int valueY,
            out int valueZ)
        {
            if (request.Space != CharacterMotionRequestSpace.Local)
            {
                valueX = request.XRaw;
                valueY = request.YRaw;
                valueZ = request.ZRaw;
                return;
            }

            if (localSpaceResolver == null)
                throw new InvalidOperationException("A local-space resolver is required for local character motion requests.");

            localSpaceResolver.ResolveLocalToWorld(
                yawMilliDegrees,
                request.XRaw,
                request.YRaw,
                request.ZRaw,
                out valueX,
                out valueY,
                out valueZ);
        }

        private void ApplyRequestedMovementMode(int movementModeId)
        {
            if (movementModeId == 0)
                return;

            if (_frame.MovementModeId == RootMotionDrivenMovementModeId)
                return;

            _frame.MovementModeId = movementModeId;
        }

        private static void CaptureFirstSource(
            ref CharacterMotionRequestSource current,
            CharacterMotionRequestSource source)
        {
            if (!current.IsEmpty)
                return;

            current = source;
        }

        private static long SqrMagnitude(int x, int y)
        {
            return (long)x * x + (long)y * y;
        }

        private static bool SameSign(int a, int b)
        {
            return (a < 0 && b < 0) || (a > 0 && b > 0) || a == b;
        }
    }

    /// <summary>
    /// Bridges CharacterAction motion-track requests into a CharacterControl motion request accumulator.
    /// </summary>
    /// <remarks>
    /// NoAlloc after initialization on accepted submissions. Rejected diagnostic paths allocate the returned diagnostic array.
    /// </remarks>
    public sealed class CharacterActionMotionRequestAccumulatorSink :
        ICharacterActionMotionRequestSink,
        ICharacterActionRootMotionRequestSink
    {
        private const int MillimetersPerMeter = 1000;
        private CharacterMotionRequestAccumulator _accumulator;
        private int _yawMilliDegrees;
        private ICharacterMotionLocalSpaceResolver _localSpaceResolver;

        public CharacterActionMotionRequestAccumulatorSink(
            int yawMilliDegrees = 0,
            ICharacterMotionLocalSpaceResolver localSpaceResolver = null)
        {
            _yawMilliDegrees = yawMilliDegrees;
            _localSpaceResolver = localSpaceResolver;
            _accumulator.Reset();
        }

        public int YawMilliDegrees
        {
            get => _yawMilliDegrees;
            set => _yawMilliDegrees = value;
        }

        public ICharacterMotionLocalSpaceResolver LocalSpaceResolver
        {
            get => _localSpaceResolver;
            set => _localSpaceResolver = value;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void ConfigureLocalSpace(
            int yawMilliDegrees,
            ICharacterMotionLocalSpaceResolver localSpaceResolver)
        {
            _yawMilliDegrees = yawMilliDegrees;
            _localSpaceResolver = localSpaceResolver;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void Reset()
        {
            _accumulator.Reset();
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public CharacterMotionRequestFrame ToFrame()
        {
            return _accumulator.ToFrame();
        }

        /// <remarks>NoAlloc after initialization on accepted submissions.</remarks>
        public CharacterActionAdapterSinkResult SubmitMotionRequest(CharacterActionMotionRequest request)
        {
            if (!TryCreateMotionRequest(in request, out CharacterMotionRequest motionRequest, out CharacterActionAdapterSinkResult rejected))
                return rejected;

            _accumulator.Accumulate(in motionRequest, _yawMilliDegrees, _localSpaceResolver);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        /// <remarks>NoAlloc after initialization on accepted submissions.</remarks>
        public CharacterActionAdapterSinkResult SubmitRootMotionRequest(CharacterActionRootMotionRequest request)
        {
            if (request.X.Equals(0f) && request.Y.Equals(0f) && request.Z.Equals(0f))
                return CharacterActionAdapterSinkResult.AcceptedResult();

            if (request.Space == CharacterActionRootMotionSpace.Local
                && _localSpaceResolver == null)
            {
                return CharacterActionAdapterSinkResult.Rejected(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.AdapterPayloadMissing,
                    "Local root motion requests require a CharacterControl local-space resolver."));
            }

            CharacterMotionRequest motionRequest = CreateRootMotionRequest(in request);
            _accumulator.Accumulate(in motionRequest, _yawMilliDegrees, _localSpaceResolver);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        private bool TryCreateMotionRequest(
            in CharacterActionMotionRequest request,
            out CharacterMotionRequest motionRequest,
            out CharacterActionAdapterSinkResult rejected)
        {
            motionRequest = default;
            rejected = default;

            switch (request.EventKind)
            {
                case CharacterActionTrackEventKind.SetMovementMode:
                    motionRequest = CreateRequest(
                        CharacterMotionRequestKind.MovementMode,
                        0,
                        0,
                        0,
                        CharacterMotionRequestSpace.World,
                        (int)request.MovementMode,
                        lockMovement: false,
                        movementControlScaleMilli: CharacterMotionRequest.DefaultControlScaleMilli,
                        rotationControlScaleMilli: CharacterMotionRequest.DefaultControlScaleMilli,
                        hasMovementControlScale: false,
                        hasRotationControlScale: false,
                        request);
                    return true;

                case CharacterActionTrackEventKind.ApplyImpulse:
                    if (request.MotionVectorSpace == CharacterActionMotionVectorSpace.Local
                        && _localSpaceResolver == null)
                    {
                        rejected = CharacterActionAdapterSinkResult.Rejected(CharacterActionDiagnostic.Error(
                            CharacterActionDiagnosticCode.AdapterPayloadMissing,
                            "Local motion requests require a CharacterControl local-space resolver."));
                        return false;
                    }

                    motionRequest = CreateRequest(
                        CharacterMotionRequestKind.Impulse,
                        MetersToMillimeters(request.X),
                        MetersToMillimeters(request.Y),
                        MetersToMillimeters(request.Z),
                        ToMotionRequestSpace(request.MotionVectorSpace),
                        (int)request.MovementMode,
                        lockMovement: false,
                        movementControlScaleMilli: CharacterMotionRequest.DefaultControlScaleMilli,
                        rotationControlScaleMilli: CharacterMotionRequest.DefaultControlScaleMilli,
                        hasMovementControlScale: false,
                        hasRotationControlScale: false,
                        request);
                    return true;

                case CharacterActionTrackEventKind.LockMovement:
                    motionRequest = CreateRequest(
                        CharacterMotionRequestKind.LockMovement,
                        0,
                        0,
                        0,
                        CharacterMotionRequestSpace.World,
                        ToLockMovementModeId(request.MovementMode),
                        lockMovement: true,
                        movementControlScaleMilli: 0,
                        rotationControlScaleMilli: CharacterMotionRequest.DefaultControlScaleMilli,
                        hasMovementControlScale: true,
                        hasRotationControlScale: true,
                        request);
                    return true;

                case CharacterActionTrackEventKind.SetControlScale:
                    motionRequest = CreateRequest(
                        CharacterMotionRequestKind.ControlScale,
                        0,
                        0,
                        0,
                        CharacterMotionRequestSpace.World,
                        0,
                        lockMovement: false,
                        movementControlScaleMilli: ScaleToMilli(request.MovementControlScale),
                        rotationControlScaleMilli: ScaleToMilli(request.RotationControlScale),
                        hasMovementControlScale: request.HasMovementControlScale,
                        hasRotationControlScale: request.HasRotationControlScale,
                        request);
                    return true;

                case CharacterActionTrackEventKind.SetVerticalVelocity:
                    motionRequest = CreateRequest(
                        CharacterMotionRequestKind.VerticalVelocityOverride,
                        0,
                        MetersToMillimeters(request.Y),
                        0,
                        CharacterMotionRequestSpace.World,
                        0,
                        lockMovement: false,
                        movementControlScaleMilli: CharacterMotionRequest.DefaultControlScaleMilli,
                        rotationControlScaleMilli: CharacterMotionRequest.DefaultControlScaleMilli,
                        hasMovementControlScale: false,
                        hasRotationControlScale: false,
                        request);
                    return true;

                case CharacterActionTrackEventKind.IgnoreGravity:
                    motionRequest = CreateRequest(
                        CharacterMotionRequestKind.IgnoreGravity,
                        0,
                        0,
                        0,
                        CharacterMotionRequestSpace.World,
                        0,
                        lockMovement: false,
                        movementControlScaleMilli: CharacterMotionRequest.DefaultControlScaleMilli,
                        rotationControlScaleMilli: CharacterMotionRequest.DefaultControlScaleMilli,
                        hasMovementControlScale: false,
                        hasRotationControlScale: false,
                        request);
                    return true;

                default:
                    rejected = CharacterActionAdapterSinkResult.Rejected(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.AdapterPayloadMissing,
                        "Unsupported CharacterAction motion request kind."));
                    return false;
            }
        }

        private static CharacterMotionRequest CreateRequest(
            CharacterMotionRequestKind kind,
            int xRaw,
            int yRaw,
            int zRaw,
            CharacterMotionRequestSpace space,
            int movementModeId,
            bool lockMovement,
            int movementControlScaleMilli,
            int rotationControlScaleMilli,
            bool hasMovementControlScale,
            bool hasRotationControlScale,
            CharacterActionMotionRequest source)
        {
            return new CharacterMotionRequest(
                kind,
                xRaw,
                yRaw,
                zRaw,
                space,
                movementModeId,
                lockMovement,
                movementControlScaleMilli,
                rotationControlScaleMilli,
                hasMovementControlScale,
                hasRotationControlScale,
                source.Metadata.ActionId,
                source.Metadata.StableEventId);
        }

        private static CharacterMotionRequest CreateRootMotionRequest(in CharacterActionRootMotionRequest source)
        {
            return new CharacterMotionRequest(
                CharacterMotionRequestKind.RootMotionDelta,
                MetersToMillimeters(source.X),
                MetersToMillimeters(source.Y),
                MetersToMillimeters(source.Z),
                ToMotionRequestSpace(source.Space),
                (int)CharacterMovementMode.RootMotionDriven,
                lockMovement: false,
                movementControlScaleMilli: CharacterMotionRequest.DefaultControlScaleMilli,
                rotationControlScaleMilli: CharacterMotionRequest.DefaultControlScaleMilli,
                hasMovementControlScale: false,
                hasRotationControlScale: false,
                source.Metadata.ActionId,
                source.Metadata.StableEventId,
                bypassGroundDamping: false,
                ToMotionObstaclePolicy(source.ObstaclePolicy));
        }

        private static CharacterMotionRequestSpace ToMotionRequestSpace(CharacterActionMotionVectorSpace space)
        {
            return space == CharacterActionMotionVectorSpace.Local
                ? CharacterMotionRequestSpace.Local
                : CharacterMotionRequestSpace.World;
        }

        private static CharacterMotionRequestSpace ToMotionRequestSpace(CharacterActionRootMotionSpace space)
        {
            return space == CharacterActionRootMotionSpace.Local
                ? CharacterMotionRequestSpace.Local
                : CharacterMotionRequestSpace.World;
        }

        private static CharacterMotionObstaclePolicy ToMotionObstaclePolicy(CharacterActionRootMotionObstaclePolicy policy)
        {
            return policy == CharacterActionRootMotionObstaclePolicy.StaticOnly
                ? CharacterMotionObstaclePolicy.StaticOnly
                : CharacterMotionObstaclePolicy.Default;
        }

        private static int ToLockMovementModeId(CharacterMovementMode movementMode)
        {
            return movementMode == CharacterMovementMode.Idle
                ? (int)CharacterMovementMode.ControlLocked
                : (int)movementMode;
        }

        private static int ScaleToMilli(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
                return 0;
            if (float.IsPositiveInfinity(value) || value >= 10f)
                return CharacterMotionRequest.MaximumControlScaleMilli;

            return (int)Math.Round(
                value * CharacterMotionRequest.DefaultControlScaleMilli,
                MidpointRounding.AwayFromZero);
        }

        private static int MetersToMillimeters(float value)
        {
            if (float.IsNaN(value))
                return 0;
            if (float.IsPositiveInfinity(value) || value >= int.MaxValue / (float)MillimetersPerMeter)
                return int.MaxValue;
            if (float.IsNegativeInfinity(value) || value <= int.MinValue / (float)MillimetersPerMeter)
                return int.MinValue;

            return (int)Math.Round(value * MillimetersPerMeter, MidpointRounding.AwayFromZero);
        }
    }
}
