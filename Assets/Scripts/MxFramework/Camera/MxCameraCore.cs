using System;
using System.Collections.Generic;

namespace MxFramework.Camera
{
    public static class MxCameraDiagnosticCodes
    {
        public const string ProfileMissing = "CAM_PROFILE_MISSING";
        public const string InvalidProfile = "CAM_INVALID_PROFILE";
        public const string TargetLost = "CAM_TARGET_LOST";
        public const string GroupEmpty = "CAM_GROUP_EMPTY";
        public const string GroupBoundsExceeded = "CAM_GROUP_BOUNDS_EXCEEDED";
        public const string InvalidRequest = "CAM_INVALID_REQUEST";
        public const string RequestConflict = "CAM_REQUEST_CONFLICT";
        public const string InvalidViewport = "CAM_INVALID_VIEWPORT";
        public const string BackendUnavailable = "CAM_BACKEND_UNAVAILABLE";
        public const string BackendApplyFailed = "CAM_BACKEND_APPLY_FAILED";
        public const string BackendMissingCamera = "CAM_BACKEND_MISSING_CAMERA";
        public const string BackendMissingProfileProvider = "CAM_BACKEND_MISSING_PROFILE_PROVIDER";
        public const string BackendMissingTargetBinder = "CAM_BACKEND_MISSING_TARGET_BINDER";
        public const string EventPayloadMissing = "CAM_EVENT_PAYLOAD_MISSING";
        public const string EventInvalidEffect = "CAM_EVENT_INVALID_EFFECT";
    }

    public enum MxCameraMode
    {
        Follow,
        LookAt,
        GroupFollowPerspective,
        GroupFollowOrthographic,
        FixedShot,
        FreeLook,
        PhotoMode
    }

    public enum MxCameraProjectionKind
    {
        Perspective,
        Orthographic
    }

    public enum MxCameraRequestKind
    {
        None,
        SetProfile,
        BindTarget,
        SetTargetGroup,
        Focus,
        Shake,
        Impulse,
        Zoom,
        Override,
        ClearOverride
    }

    public enum MxCameraEvaluationStatus
    {
        Success,
        SuccessWithDiagnostics,
        FallbackUsed,
        Failed
    }

    public enum MxCameraStateSource
    {
        Normal,
        Grace,
        Fallback,
        DebugOverride
    }

    public enum MxCameraBoundsPolicy
    {
        NoClamp,
        ClampPosition,
        ClampTargetCenter
    }

    public readonly struct MxCameraVector3 : IEquatable<MxCameraVector3>
    {
        public static readonly MxCameraVector3 Zero = new MxCameraVector3(0f, 0f, 0f);
        public static readonly MxCameraVector3 Up = new MxCameraVector3(0f, 1f, 0f);
        public static readonly MxCameraVector3 Forward = new MxCameraVector3(0f, 0f, 1f);
        public static readonly MxCameraVector3 Right = new MxCameraVector3(1f, 0f, 0f);

        public MxCameraVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float SqrMagnitude => X * X + Y * Y + Z * Z;
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        public MxCameraVector3 Normalized
        {
            get
            {
                float length = Magnitude;
                return length > 0.00001f ? this / length : Zero;
            }
        }

        public static MxCameraVector3 Min(MxCameraVector3 a, MxCameraVector3 b)
        {
            return new MxCameraVector3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
        }

        public static MxCameraVector3 Max(MxCameraVector3 a, MxCameraVector3 b)
        {
            return new MxCameraVector3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
        }

        public static float Dot(MxCameraVector3 a, MxCameraVector3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static MxCameraVector3 Cross(MxCameraVector3 a, MxCameraVector3 b)
        {
            return new MxCameraVector3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);
        }

        public static float Distance(MxCameraVector3 a, MxCameraVector3 b)
        {
            return (a - b).Magnitude;
        }

        public static MxCameraVector3 ClampMagnitude(MxCameraVector3 value, float maxLength)
        {
            if (maxLength < 0f)
                maxLength = 0f;

            float sqrMagnitude = value.SqrMagnitude;
            if (sqrMagnitude <= maxLength * maxLength || sqrMagnitude <= 0.0000001f)
                return value;

            return value.Normalized * maxLength;
        }

        public static MxCameraVector3 operator +(MxCameraVector3 a, MxCameraVector3 b)
        {
            return new MxCameraVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static MxCameraVector3 operator -(MxCameraVector3 a, MxCameraVector3 b)
        {
            return new MxCameraVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static MxCameraVector3 operator -(MxCameraVector3 value)
        {
            return new MxCameraVector3(-value.X, -value.Y, -value.Z);
        }

        public static MxCameraVector3 operator *(MxCameraVector3 value, float scalar)
        {
            return new MxCameraVector3(value.X * scalar, value.Y * scalar, value.Z * scalar);
        }

        public static MxCameraVector3 operator /(MxCameraVector3 value, float scalar)
        {
            if (Math.Abs(scalar) <= 0.000001f)
                return Zero;

            return new MxCameraVector3(value.X / scalar, value.Y / scalar, value.Z / scalar);
        }

        public bool Equals(MxCameraVector3 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is MxCameraVector3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return X + "," + Y + "," + Z;
        }
    }

    public readonly struct MxCameraEulerRotation
    {
        public MxCameraEulerRotation(float pitch, float yaw, float roll = 0f)
        {
            Pitch = pitch;
            Yaw = yaw;
            Roll = roll;
        }

        public float Pitch { get; }
        public float Yaw { get; }
        public float Roll { get; }
    }

    public readonly struct MxCameraProfileId : IEquatable<MxCameraProfileId>
    {
        public MxCameraProfileId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(MxCameraProfileId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MxCameraProfileId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct MxCameraRigId : IEquatable<MxCameraRigId>
    {
        public MxCameraRigId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(MxCameraRigId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MxCameraRigId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct MxCameraTargetRef : IEquatable<MxCameraTargetRef>
    {
        public MxCameraTargetRef(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(MxCameraTargetRef other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MxCameraTargetRef other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct MxCameraTargetGroupId : IEquatable<MxCameraTargetGroupId>
    {
        public MxCameraTargetGroupId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(MxCameraTargetGroupId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MxCameraTargetGroupId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
    }

    public sealed class MxCameraProfileDefinition
    {
        public MxCameraProfileId ProfileId { get; set; }
        public MxCameraMode Mode { get; set; }
        public int Priority { get; set; }
        public MxCameraVector3 LocalOffset { get; set; }
        public MxCameraVector3 WorldOffset { get; set; }
        public float Distance { get; set; } = 8f;
        public float MinDistance { get; set; } = 1f;
        public float MaxDistance { get; set; } = 32f;
        public float FieldOfView { get; set; } = 60f;
        public float MinFieldOfView { get; set; } = 15f;
        public float MaxFieldOfView { get; set; } = 90f;
        public float OrthographicSize { get; set; } = 5f;
        public float MinOrthographicSize { get; set; } = 1f;
        public float MaxOrthographicSize { get; set; } = 40f;
        public float PositionSmoothing { get; set; }
        public float RotationSmoothing { get; set; }
        public float ZoomSmoothing { get; set; }
        public float TargetPadding { get; set; } = 0.5f;
        public int TargetLostGraceFrames { get; set; } = 6;
        public MxCameraBoundsPolicy BoundsPolicy { get; set; }
        public float MaxTargetRadius { get; set; } = 64f;
        public float ShakeLimit { get; set; } = 1f;
        public float Pitch { get; set; } = 35f;
        public float Yaw { get; set; }
        public MxCameraVector3 FallbackPosition { get; set; } = new MxCameraVector3(0f, 6f, -8f);
        public MxCameraVector3 FallbackFocus { get; set; } = MxCameraVector3.Zero;
        public string[] DiagnosticTags { get; set; } = new string[0];

        public MxCameraProjectionKind ProjectionKind
        {
            get
            {
                return Mode == MxCameraMode.GroupFollowOrthographic
                    ? MxCameraProjectionKind.Orthographic
                    : MxCameraProjectionKind.Perspective;
            }
        }
    }

    public sealed class MxCameraTargetSnapshot
    {
        public MxCameraTargetSnapshot(
            MxCameraTargetRef targetRef,
            MxCameraVector3 position,
            MxCameraVector3 forward,
            MxCameraVector3 up,
            MxCameraVector3 velocity,
            MxCameraVector3 boundsCenter,
            MxCameraVector3 boundsExtents,
            float weight,
            bool isPrimary,
            bool isValid,
            long timestampFrame)
        {
            TargetRef = targetRef;
            Position = position;
            Forward = forward.SqrMagnitude > 0f ? forward.Normalized : MxCameraVector3.Forward;
            Up = up.SqrMagnitude > 0f ? up.Normalized : MxCameraVector3.Up;
            Velocity = velocity;
            BoundsCenter = boundsCenter;
            BoundsExtents = new MxCameraVector3(
                Math.Max(0f, boundsExtents.X),
                Math.Max(0f, boundsExtents.Y),
                Math.Max(0f, boundsExtents.Z));
            Weight = weight > 0f ? weight : 1f;
            IsPrimary = isPrimary;
            IsValid = isValid && targetRef.IsValid;
            TimestampFrame = timestampFrame;
        }

        public MxCameraTargetRef TargetRef { get; }
        public MxCameraVector3 Position { get; }
        public MxCameraVector3 Forward { get; }
        public MxCameraVector3 Up { get; }
        public MxCameraVector3 Velocity { get; }
        public MxCameraVector3 BoundsCenter { get; }
        public MxCameraVector3 BoundsExtents { get; }
        public float Weight { get; }
        public bool IsPrimary { get; }
        public bool IsValid { get; }
        public long TimestampFrame { get; }
    }

    public sealed class MxCameraTargetGroup
    {
        private readonly List<MxCameraTargetRef> _targets;

        public MxCameraTargetGroup(MxCameraTargetGroupId groupId, IEnumerable<MxCameraTargetRef> targets = null, MxCameraTargetRef primaryTarget = default)
        {
            GroupId = groupId;
            PrimaryTarget = primaryTarget;
            _targets = targets != null ? new List<MxCameraTargetRef>(targets) : new List<MxCameraTargetRef>();
        }

        public MxCameraTargetGroupId GroupId { get; }
        public MxCameraTargetRef PrimaryTarget { get; }
        public IReadOnlyList<MxCameraTargetRef> Targets => _targets;
        public bool HasExplicitTargets => _targets.Count > 0;
    }

    public sealed class MxCameraTargetGroupState
    {
        public static readonly MxCameraTargetGroupState Empty = new MxCameraTargetGroupState(
            default,
            MxCameraVector3.Zero,
            MxCameraVector3.Zero,
            MxCameraVector3.Zero,
            0f,
            default,
            0,
            false);

        public MxCameraTargetGroupState(
            MxCameraTargetGroupId groupId,
            MxCameraVector3 center,
            MxCameraVector3 boundsMin,
            MxCameraVector3 boundsMax,
            float radius,
            MxCameraTargetRef primaryTarget,
            int validTargetCount,
            bool boundsExceeded)
        {
            GroupId = groupId;
            Center = center;
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
            Radius = Math.Max(0f, radius);
            PrimaryTarget = primaryTarget;
            ValidTargetCount = Math.Max(0, validTargetCount);
            BoundsExceeded = boundsExceeded;
        }

        public MxCameraTargetGroupId GroupId { get; }
        public MxCameraVector3 Center { get; }
        public MxCameraVector3 BoundsMin { get; }
        public MxCameraVector3 BoundsMax { get; }
        public float Radius { get; }
        public MxCameraTargetRef PrimaryTarget { get; }
        public int ValidTargetCount { get; }
        public bool BoundsExceeded { get; }
    }

    public readonly struct MxCameraRequest
    {
        public MxCameraRequest(
            ulong requestId,
            long frame,
            long sequence,
            string sourceId,
            MxCameraRequestKind kind,
            int priority = 0,
            MxCameraTargetRef targetRef = default,
            MxCameraTargetGroupId groupId = default,
            MxCameraProfileId profileId = default,
            float floatValue = 0f,
            int durationFrames = 0,
            long expiresFrame = 0,
            string payloadKey = "",
            string traceId = "",
            MxCameraTargetGroup targetGroup = null)
        {
            RequestId = requestId;
            Frame = frame;
            Sequence = sequence;
            SourceId = sourceId ?? string.Empty;
            Kind = kind;
            Priority = priority;
            TargetRef = targetRef;
            GroupId = groupId;
            ProfileId = profileId;
            FloatValue = floatValue;
            DurationFrames = Math.Max(0, durationFrames);
            ExpiresFrame = expiresFrame;
            PayloadKey = payloadKey ?? string.Empty;
            TraceId = traceId ?? string.Empty;
            TargetGroup = targetGroup;
        }

        public ulong RequestId { get; }
        public long Frame { get; }
        public long Sequence { get; }
        public string SourceId { get; }
        public MxCameraRequestKind Kind { get; }
        public int Priority { get; }
        public MxCameraTargetRef TargetRef { get; }
        public MxCameraTargetGroupId GroupId { get; }
        public MxCameraProfileId ProfileId { get; }
        public float FloatValue { get; }
        public int DurationFrames { get; }
        public long ExpiresFrame { get; }
        public string PayloadKey { get; }
        public string TraceId { get; }
        public MxCameraTargetGroup TargetGroup { get; }
        public bool IsExpired(long frame) => ExpiresFrame > 0L && ExpiresFrame < frame;
    }

    public readonly struct MxCameraDiagnostic
    {
        public MxCameraDiagnostic(string code, string message, ulong requestId = 0UL, string field = "")
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            RequestId = requestId;
            Field = field ?? string.Empty;
        }

        public string Code { get; }
        public string Message { get; }
        public ulong RequestId { get; }
        public string Field { get; }
    }

    public readonly struct MxCameraResult
    {
        public MxCameraResult(bool success, string code, string message)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public string Code { get; }
        public string Message { get; }

        public static MxCameraResult Ok(string message = "")
        {
            return new MxCameraResult(true, string.Empty, message);
        }

        public static MxCameraResult Failed(string code, string message)
        {
            return new MxCameraResult(false, code, message);
        }
    }

    public sealed class MxCameraState
    {
        public static readonly MxCameraState Empty = new MxCameraState(
            default,
            default,
            MxCameraVector3.Zero,
            MxCameraVector3.Forward,
            MxCameraVector3.Up,
            new MxCameraEulerRotation(0f, 0f, 0f),
            MxCameraProjectionKind.Perspective,
            60f,
            5f,
            MxCameraVector3.Zero,
            MxCameraVector3.Zero,
            0f,
            0f,
            MxCameraStateSource.Fallback);

        public MxCameraState(
            MxCameraRigId rigId,
            MxCameraProfileId profileId,
            MxCameraVector3 position,
            MxCameraVector3 viewForward,
            MxCameraVector3 viewUp,
            MxCameraEulerRotation rotation,
            MxCameraProjectionKind projectionKind,
            float fieldOfView,
            float orthographicSize,
            MxCameraVector3 focusCenter,
            MxCameraVector3 shakeOffset,
            float groupRadius,
            float framingUtilization,
            MxCameraStateSource source)
        {
            RigId = rigId;
            ProfileId = profileId;
            Position = position;
            ViewForward = viewForward.SqrMagnitude > 0f ? viewForward.Normalized : MxCameraVector3.Forward;
            ViewUp = viewUp.SqrMagnitude > 0f ? viewUp.Normalized : MxCameraVector3.Up;
            Rotation = rotation;
            ProjectionKind = projectionKind;
            FieldOfView = fieldOfView;
            OrthographicSize = orthographicSize;
            FocusCenter = focusCenter;
            ShakeOffset = shakeOffset;
            GroupRadius = groupRadius;
            FramingUtilization = framingUtilization;
            Source = source;
        }

        public MxCameraRigId RigId { get; }
        public MxCameraProfileId ProfileId { get; }
        public MxCameraVector3 Position { get; }
        public MxCameraVector3 ViewForward { get; }
        public MxCameraVector3 ViewUp { get; }
        public MxCameraEulerRotation Rotation { get; }
        public MxCameraProjectionKind ProjectionKind { get; }
        public float FieldOfView { get; }
        public float OrthographicSize { get; }
        public MxCameraVector3 FocusCenter { get; }
        public MxCameraVector3 ShakeOffset { get; }
        public float GroupRadius { get; }
        public float FramingUtilization { get; }
        public MxCameraStateSource Source { get; }
    }

    public sealed class MxCameraEvaluationContext
    {
        private readonly List<MxCameraProfileDefinition> _profiles;
        private readonly List<MxCameraTargetSnapshot> _targetSnapshots;
        private readonly List<MxCameraRequest> _requests;

        public MxCameraEvaluationContext(
            long frame,
            float deltaTime,
            MxCameraRigId rigId,
            float viewportWidth,
            float viewportHeight,
            MxCameraState previousState,
            IEnumerable<MxCameraProfileDefinition> profiles,
            IEnumerable<MxCameraTargetSnapshot> targetSnapshots,
            IEnumerable<MxCameraRequest> requests = null,
            MxCameraTargetGroup targetGroup = null)
        {
            Frame = frame;
            DeltaTime = deltaTime;
            RigId = rigId;
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
            PreviousState = previousState ?? MxCameraState.Empty;
            TargetGroup = targetGroup;
            _profiles = profiles != null ? new List<MxCameraProfileDefinition>(profiles) : new List<MxCameraProfileDefinition>();
            _targetSnapshots = targetSnapshots != null ? new List<MxCameraTargetSnapshot>(targetSnapshots) : new List<MxCameraTargetSnapshot>();
            _requests = requests != null ? new List<MxCameraRequest>(requests) : new List<MxCameraRequest>();
        }

        public long Frame { get; }
        public float DeltaTime { get; }
        public MxCameraRigId RigId { get; }
        public float ViewportWidth { get; }
        public float ViewportHeight { get; }
        public float ViewportAspect => ViewportWidth > 0f && ViewportHeight > 0f ? ViewportWidth / ViewportHeight : 0f;
        public MxCameraState PreviousState { get; }
        public IReadOnlyList<MxCameraProfileDefinition> Profiles => _profiles;
        public IReadOnlyList<MxCameraTargetSnapshot> TargetSnapshots => _targetSnapshots;
        public IReadOnlyList<MxCameraRequest> Requests => _requests;
        public MxCameraTargetGroup TargetGroup { get; }
    }

    public sealed class MxCameraEvaluationResult
    {
        public MxCameraEvaluationResult(
            MxCameraEvaluationStatus status,
            long frame,
            MxCameraRigId rigId,
            MxCameraProfileId activeProfileId,
            MxCameraState state,
            MxCameraTargetGroupState targetGroupState,
            IReadOnlyList<ulong> acceptedRequestIds,
            IReadOnlyList<ulong> rejectedRequestIds,
            IReadOnlyList<MxCameraDiagnostic> diagnostics,
            MxCameraDebugSummary debugSummary)
        {
            Status = status;
            Frame = frame;
            RigId = rigId;
            ActiveProfileId = activeProfileId;
            State = state ?? MxCameraState.Empty;
            TargetGroupState = targetGroupState ?? MxCameraTargetGroupState.Empty;
            AcceptedRequestIds = acceptedRequestIds != null ? new List<ulong>(acceptedRequestIds) : new List<ulong>();
            RejectedRequestIds = rejectedRequestIds != null ? new List<ulong>(rejectedRequestIds) : new List<ulong>();
            Diagnostics = diagnostics != null ? new List<MxCameraDiagnostic>(diagnostics) : new List<MxCameraDiagnostic>();
            DebugSummary = debugSummary;
        }

        public MxCameraEvaluationStatus Status { get; }
        public long Frame { get; }
        public MxCameraRigId RigId { get; }
        public MxCameraProfileId ActiveProfileId { get; }
        public MxCameraState State { get; }
        public MxCameraTargetGroupState TargetGroupState { get; }
        public IReadOnlyList<ulong> AcceptedRequestIds { get; }
        public IReadOnlyList<ulong> RejectedRequestIds { get; }
        public IReadOnlyList<MxCameraDiagnostic> Diagnostics { get; }
        public MxCameraDebugSummary DebugSummary { get; }
    }

    public readonly struct MxCameraDebugSummary
    {
        public MxCameraDebugSummary(int acceptedRequestCount, int rejectedRequestCount, int diagnosticCount, int shakeRequestCount = 0)
        {
            AcceptedRequestCount = acceptedRequestCount;
            RejectedRequestCount = rejectedRequestCount;
            DiagnosticCount = diagnosticCount;
            ShakeRequestCount = Math.Max(0, shakeRequestCount);
        }

        public int AcceptedRequestCount { get; }
        public int RejectedRequestCount { get; }
        public int DiagnosticCount { get; }
        public int ShakeRequestCount { get; }
    }

    public sealed class MxCameraDebugSnapshot
    {
        public MxCameraDebugSnapshot(
            bool isAvailable,
            MxCameraRigId rigId,
            string backendId,
            MxCameraProfileId activeProfileId,
            MxCameraMode mode,
            MxCameraTargetGroupState targetGroupState,
            MxCameraState state,
            IReadOnlyList<MxCameraDiagnostic> recentDiagnostics,
            int shakeRequestCount)
        {
            IsAvailable = isAvailable;
            RigId = rigId;
            BackendId = backendId ?? string.Empty;
            ActiveProfileId = activeProfileId;
            Mode = mode;
            TargetGroupState = targetGroupState ?? MxCameraTargetGroupState.Empty;
            State = state ?? MxCameraState.Empty;
            RecentDiagnostics = recentDiagnostics != null ? new List<MxCameraDiagnostic>(recentDiagnostics) : new List<MxCameraDiagnostic>();
            ShakeRequestCount = Math.Max(0, shakeRequestCount);
        }

        public bool IsAvailable { get; }
        public MxCameraRigId RigId { get; }
        public string BackendId { get; }
        public MxCameraProfileId ActiveProfileId { get; }
        public MxCameraMode Mode { get; }
        public MxCameraTargetGroupState TargetGroupState { get; }
        public MxCameraState State { get; }
        public IReadOnlyList<MxCameraDiagnostic> RecentDiagnostics { get; }
        public int ShakeRequestCount { get; }
    }

    public interface IMxCameraRequestSink
    {
        MxCameraResult EnqueueRequest(in MxCameraRequest request);
    }

    public interface IMxCameraService : IMxCameraRequestSink
    {
        MxCameraResult SetProfile(MxCameraProfileId profileId, string traceId = "");
        MxCameraResult BindTarget(MxCameraTargetRef target, string traceId = "");
        MxCameraResult SetTargetGroup(MxCameraTargetGroup group, string traceId = "");
        MxCameraEvaluationResult Evaluate(MxCameraEvaluationContext context);
        MxCameraDebugSnapshot CaptureSnapshot();
    }

    public interface IMxCameraProfileProvider
    {
        IReadOnlyList<MxCameraProfileDefinition> Profiles { get; }
    }

    public interface IMxCameraBackend
    {
        MxCameraResult Initialize(IMxCameraProfileProvider profiles);
        MxCameraResult Apply(in MxCameraState state);
        MxCameraDebugSnapshot CaptureSnapshot();
        void Dispose();
    }

    public sealed class MxCameraNullBackend : IMxCameraBackend
    {
        public MxCameraResult Initialize(IMxCameraProfileProvider profiles)
        {
            return MxCameraResult.Ok();
        }

        public MxCameraResult Apply(in MxCameraState state)
        {
            return MxCameraResult.Ok();
        }

        public MxCameraDebugSnapshot CaptureSnapshot()
        {
            return new MxCameraDebugSnapshot(false, default, "Null", default, MxCameraMode.Follow, MxCameraTargetGroupState.Empty, MxCameraState.Empty, null, 0);
        }

        public void Dispose()
        {
        }
    }

    public sealed class MxCameraProfileValidator
    {
        public IReadOnlyList<MxCameraDiagnostic> Validate(MxCameraProfileDefinition profile)
        {
            var diagnostics = new List<MxCameraDiagnostic>();
            if (profile == null)
            {
                diagnostics.Add(new MxCameraDiagnostic(MxCameraDiagnosticCodes.InvalidProfile, "Profile is null.", field: "profile"));
                return diagnostics;
            }

            AddIf(!profile.ProfileId.IsValid, "ProfileId", "Profile id is required.", diagnostics);
            AddIf(profile.Distance <= 0f, "Distance", "Distance must be greater than zero.", diagnostics);
            AddIf(profile.MinDistance <= 0f || profile.MaxDistance < profile.MinDistance, "MinDistance/MaxDistance", "Distance range is invalid.", diagnostics);
            AddIf(profile.FieldOfView <= 0f || profile.FieldOfView >= 179f, "FieldOfView", "Field of view must be in (0, 179).", diagnostics);
            AddIf(profile.MinFieldOfView <= 0f || profile.MaxFieldOfView < profile.MinFieldOfView, "MinFieldOfView/MaxFieldOfView", "Field of view range is invalid.", diagnostics);
            AddIf(profile.OrthographicSize <= 0f, "OrthographicSize", "Orthographic size must be greater than zero.", diagnostics);
            AddIf(profile.MinOrthographicSize <= 0f || profile.MaxOrthographicSize < profile.MinOrthographicSize, "MinOrthographicSize/MaxOrthographicSize", "Orthographic size range is invalid.", diagnostics);
            AddIf(profile.PositionSmoothing < 0f || profile.RotationSmoothing < 0f || profile.ZoomSmoothing < 0f, "Smoothing", "Smoothing values cannot be negative.", diagnostics);
            AddIf(profile.TargetLostGraceFrames < 0, "TargetLostGraceFrames", "Target lost grace frames cannot be negative.", diagnostics);
            AddIf(profile.TargetPadding < 0f, "TargetPadding", "Target padding cannot be negative.", diagnostics);
            AddIf(profile.MaxTargetRadius < 0f, "MaxTargetRadius", "Max target radius cannot be negative.", diagnostics);
            AddIf(profile.ShakeLimit < 0f, "ShakeLimit", "Shake limit cannot be negative.", diagnostics);
            return diagnostics;
        }

        private static void AddIf(bool condition, string field, string message, List<MxCameraDiagnostic> diagnostics)
        {
            if (condition)
                diagnostics.Add(new MxCameraDiagnostic(MxCameraDiagnosticCodes.InvalidProfile, message, field: field));
        }
    }

    public sealed class MxCameraService : IMxCameraService
    {
        private readonly List<MxCameraRequest> _pendingRequests = new List<MxCameraRequest>();
        private readonly Queue<MxCameraDiagnostic> _recentDiagnostics = new Queue<MxCameraDiagnostic>();
        private readonly MxCameraProfileValidator _validator = new MxCameraProfileValidator();
        private MxCameraEvaluationResult _lastResult;
        private MxCameraTargetGroupState _lastValidGroup = MxCameraTargetGroupState.Empty;
        private int _targetLostFrames;
        private ulong _nextRequestId = 1UL;

        public MxCameraService(MxCameraRigId rigId)
        {
            RigId = rigId;
        }

        public MxCameraRigId RigId { get; }
        public int MaxRecentDiagnostics { get; set; } = 32;

        public MxCameraResult SetProfile(MxCameraProfileId profileId, string traceId = "")
        {
            return EnqueueRequest(new MxCameraRequest(NextRequestId(), 0L, 0L, "camera-service", MxCameraRequestKind.SetProfile, profileId: profileId, traceId: traceId));
        }

        public MxCameraResult BindTarget(MxCameraTargetRef target, string traceId = "")
        {
            return EnqueueRequest(new MxCameraRequest(NextRequestId(), 0L, 0L, "camera-service", MxCameraRequestKind.BindTarget, targetRef: target, traceId: traceId));
        }

        public MxCameraResult SetTargetGroup(MxCameraTargetGroup group, string traceId = "")
        {
            if (group == null)
                return MxCameraResult.Failed(MxCameraDiagnosticCodes.InvalidRequest, "Target group cannot be null.");

            return EnqueueRequest(new MxCameraRequest(NextRequestId(), 0L, 0L, "camera-service", MxCameraRequestKind.SetTargetGroup, groupId: group.GroupId, traceId: traceId, targetGroup: group));
        }

        public MxCameraResult EnqueueRequest(in MxCameraRequest request)
        {
            if (request.Kind == MxCameraRequestKind.None || request.RequestId == 0UL)
                return MxCameraResult.Failed(MxCameraDiagnosticCodes.InvalidRequest, "Camera request must have a kind and non-zero id.");

            _pendingRequests.Add(request);
            return MxCameraResult.Ok();
        }

        public MxCameraEvaluationResult Evaluate(MxCameraEvaluationContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var diagnostics = new List<MxCameraDiagnostic>();
            var accepted = new List<ulong>();
            var rejected = new List<ulong>();
            var requests = BuildSortedRequests(context, diagnostics, rejected);
            MxCameraProfileId requestedProfileId = context.PreviousState.ProfileId;
            MxCameraTargetRef requestedTarget = default;
            MxCameraTargetGroup requestedGroup = context.TargetGroup;
            float zoomDelta = 0f;
            MxCameraVector3 shake = MxCameraVector3.Zero;
            int shakeCount = 0;

            for (int i = 0; i < requests.Count; i++)
            {
                MxCameraRequest request = requests[i];
                if (!ValidateRequest(request, diagnostics))
                {
                    rejected.Add(request.RequestId);
                    continue;
                }

                switch (request.Kind)
                {
                    case MxCameraRequestKind.SetProfile:
                        if (requestedProfileId.IsValid && !requestedProfileId.Equals(request.ProfileId))
                            diagnostics.Add(new MxCameraDiagnostic(MxCameraDiagnosticCodes.RequestConflict, "SetProfile request replaced a lower-priority profile request.", request.RequestId));
                        requestedProfileId = request.ProfileId;
                        accepted.Add(request.RequestId);
                        break;
                    case MxCameraRequestKind.BindTarget:
                        requestedTarget = request.TargetRef;
                        accepted.Add(request.RequestId);
                        break;
                    case MxCameraRequestKind.SetTargetGroup:
                        requestedGroup = request.TargetGroup;
                        requestedTarget = default;
                        accepted.Add(request.RequestId);
                        break;
                    case MxCameraRequestKind.Shake:
                        shake += MxCameraVector3.Right * Math.Abs(request.FloatValue);
                        shakeCount++;
                        accepted.Add(request.RequestId);
                        break;
                    case MxCameraRequestKind.Impulse:
                        shake += MxCameraVector3.Up * Math.Abs(request.FloatValue);
                        shakeCount++;
                        accepted.Add(request.RequestId);
                        break;
                    case MxCameraRequestKind.Zoom:
                        zoomDelta += request.FloatValue;
                        accepted.Add(request.RequestId);
                        break;
                    case MxCameraRequestKind.Focus:
                    case MxCameraRequestKind.Override:
                    case MxCameraRequestKind.ClearOverride:
                        accepted.Add(request.RequestId);
                        break;
                }
            }

            MxCameraProfileDefinition profile = ResolveProfile(requestedProfileId, context.Profiles);
            if (profile == null && context.Profiles.Count > 0)
                profile = context.Profiles[0];

            if (profile == null)
            {
                diagnostics.Add(new MxCameraDiagnostic(MxCameraDiagnosticCodes.ProfileMissing, "No camera profile is available."));
                MxCameraState fallback = BuildFallbackState(context, null, MxCameraVector3.Zero, MxCameraStateSource.Fallback);
                return StoreResult(context, fallback, MxCameraTargetGroupState.Empty, accepted, rejected, diagnostics, shakeCount, MxCameraEvaluationStatus.Failed);
            }

            IReadOnlyList<MxCameraDiagnostic> profileDiagnostics = _validator.Validate(profile);
            for (int i = 0; i < profileDiagnostics.Count; i++)
                diagnostics.Add(profileDiagnostics[i]);

            float aspect = context.ViewportAspect;
            if (aspect <= 0f)
            {
                aspect = 16f / 9f;
                diagnostics.Add(new MxCameraDiagnostic(MxCameraDiagnosticCodes.InvalidViewport, "Viewport dimensions are invalid; fallback aspect was used."));
            }

            if (profileDiagnostics.Count > 0)
            {
                MxCameraState fallback = BuildFallbackState(context, profile, MxCameraVector3.Zero, MxCameraStateSource.Fallback);
                return StoreResult(context, fallback, MxCameraTargetGroupState.Empty, accepted, rejected, diagnostics, shakeCount, MxCameraEvaluationStatus.Failed);
            }

            MxCameraTargetGroupState groupState = SolveTargetGroup(context, profile, requestedGroup, requestedTarget, diagnostics);
            MxCameraStateSource stateSource = MxCameraStateSource.Normal;
            if (groupState.ValidTargetCount == 0)
            {
                diagnostics.Add(new MxCameraDiagnostic(MxCameraDiagnosticCodes.GroupEmpty, "Camera target group has no valid targets."));
                if (_lastValidGroup.ValidTargetCount > 0 && _targetLostFrames < profile.TargetLostGraceFrames)
                {
                    _targetLostFrames++;
                    groupState = _lastValidGroup;
                    stateSource = MxCameraStateSource.Grace;
                    diagnostics.Add(new MxCameraDiagnostic(MxCameraDiagnosticCodes.TargetLost, "Target was lost; last valid target group is still inside grace frames."));
                }
                else
                {
                    _targetLostFrames++;
                    MxCameraState fallback = BuildFallbackState(context, profile, MxCameraVector3.ClampMagnitude(shake, profile.ShakeLimit), MxCameraStateSource.Fallback);
                    return StoreResult(context, fallback, groupState, accepted, rejected, diagnostics, shakeCount, MxCameraEvaluationStatus.FallbackUsed);
                }
            }
            else
            {
                _targetLostFrames = 0;
                _lastValidGroup = groupState;
            }

            if (groupState.BoundsExceeded)
                diagnostics.Add(new MxCameraDiagnostic(MxCameraDiagnosticCodes.GroupBoundsExceeded, "Camera target group radius exceeds profile limit."));

            MxCameraVector3 shakeOffset = MxCameraVector3.ClampMagnitude(shake, profile.ShakeLimit);
            MxCameraState state = BuildState(context, profile, groupState, aspect, zoomDelta, shakeOffset, stateSource);
            MxCameraEvaluationStatus status = diagnostics.Count > 0 ? MxCameraEvaluationStatus.SuccessWithDiagnostics : MxCameraEvaluationStatus.Success;
            return StoreResult(context, state, groupState, accepted, rejected, diagnostics, shakeCount, status);
        }

        public MxCameraDebugSnapshot CaptureSnapshot()
        {
            if (_lastResult == null)
            {
                return new MxCameraDebugSnapshot(false, RigId, string.Empty, default, MxCameraMode.Follow, MxCameraTargetGroupState.Empty, MxCameraState.Empty, _recentDiagnostics.ToArray(), 0);
            }

            return new MxCameraDebugSnapshot(
                true,
                _lastResult.RigId,
                string.Empty,
                _lastResult.ActiveProfileId,
                ResolveMode(_lastResult.State),
                _lastResult.TargetGroupState,
                _lastResult.State,
                _recentDiagnostics.ToArray(),
                _lastResult.DebugSummary.ShakeRequestCount);
        }

        private ulong NextRequestId()
        {
            return _nextRequestId++;
        }

        private List<MxCameraRequest> BuildSortedRequests(MxCameraEvaluationContext context, List<MxCameraDiagnostic> diagnostics, List<ulong> rejected)
        {
            var requests = new List<MxCameraRequest>();
            for (int i = 0; i < context.Requests.Count; i++)
                requests.Add(context.Requests[i]);

            for (int i = _pendingRequests.Count - 1; i >= 0; i--)
            {
                MxCameraRequest request = _pendingRequests[i];
                requests.Add(request);
                _pendingRequests.RemoveAt(i);
            }

            for (int i = requests.Count - 1; i >= 0; i--)
            {
                MxCameraRequest request = requests[i];
                if (request.Frame > 0L && request.Frame > context.Frame)
                {
                    requests.RemoveAt(i);
                    continue;
                }

                if (request.IsExpired(context.Frame))
                {
                    rejected.Add(request.RequestId);
                    diagnostics.Add(new MxCameraDiagnostic(MxCameraDiagnosticCodes.InvalidRequest, "Camera request is expired.", request.RequestId));
                    requests.RemoveAt(i);
                }
            }

            requests.Sort(CompareRequests);
            return requests;
        }

        private static int CompareRequests(MxCameraRequest left, MxCameraRequest right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            if (priority != 0)
                return priority;

            int sequence = left.Sequence.CompareTo(right.Sequence);
            if (sequence != 0)
                return sequence;

            return left.RequestId.CompareTo(right.RequestId);
        }

        private static bool ValidateRequest(MxCameraRequest request, List<MxCameraDiagnostic> diagnostics)
        {
            if (request.Kind == MxCameraRequestKind.SetProfile && !request.ProfileId.IsValid)
            {
                diagnostics.Add(new MxCameraDiagnostic(MxCameraDiagnosticCodes.InvalidRequest, "SetProfile request has no profile id.", request.RequestId));
                return false;
            }

            if (request.Kind == MxCameraRequestKind.BindTarget && !request.TargetRef.IsValid)
            {
                diagnostics.Add(new MxCameraDiagnostic(MxCameraDiagnosticCodes.InvalidRequest, "BindTarget request has no target ref.", request.RequestId));
                return false;
            }

            if (request.Kind == MxCameraRequestKind.SetTargetGroup && request.TargetGroup == null)
            {
                diagnostics.Add(new MxCameraDiagnostic(MxCameraDiagnosticCodes.InvalidRequest, "SetTargetGroup request has no target group.", request.RequestId));
                return false;
            }

            return true;
        }

        private static MxCameraProfileDefinition ResolveProfile(MxCameraProfileId profileId, IReadOnlyList<MxCameraProfileDefinition> profiles)
        {
            if (profiles == null || profiles.Count == 0)
                return null;

            if (profileId.IsValid)
            {
                for (int i = 0; i < profiles.Count; i++)
                {
                    if (profiles[i] != null && profiles[i].ProfileId.Equals(profileId))
                        return profiles[i];
                }
            }

            return null;
        }

        private static MxCameraTargetGroupState SolveTargetGroup(
            MxCameraEvaluationContext context,
            MxCameraProfileDefinition profile,
            MxCameraTargetGroup requestedGroup,
            MxCameraTargetRef requestedTarget,
            List<MxCameraDiagnostic> diagnostics)
        {
            var snapshots = new List<MxCameraTargetSnapshot>();
            for (int i = 0; i < context.TargetSnapshots.Count; i++)
            {
                MxCameraTargetSnapshot snapshot = context.TargetSnapshots[i];
                if (snapshot == null || !snapshot.IsValid)
                    continue;

                if (requestedTarget.IsValid && !snapshot.TargetRef.Equals(requestedTarget))
                    continue;

                if (requestedGroup != null && requestedGroup.HasExplicitTargets && !ContainsTarget(requestedGroup.Targets, snapshot.TargetRef))
                    continue;

                snapshots.Add(snapshot);
            }

            snapshots.Sort(CompareSnapshots);
            if (snapshots.Count == 0)
                return MxCameraTargetGroupState.Empty;

            float totalWeight = 0f;
            MxCameraVector3 weightedCenter = MxCameraVector3.Zero;
            MxCameraVector3 boundsMin = new MxCameraVector3(float.MaxValue, float.MaxValue, float.MaxValue);
            MxCameraVector3 boundsMax = new MxCameraVector3(float.MinValue, float.MinValue, float.MinValue);
            MxCameraTargetRef primary = default;

            for (int i = 0; i < snapshots.Count; i++)
            {
                MxCameraTargetSnapshot snapshot = snapshots[i];
                float weight = snapshot.Weight > 0f ? snapshot.Weight : 1f;
                totalWeight += weight;
                weightedCenter += snapshot.BoundsCenter * weight;
                MxCameraVector3 padding = new MxCameraVector3(profile.TargetPadding, profile.TargetPadding, profile.TargetPadding);
                boundsMin = MxCameraVector3.Min(boundsMin, snapshot.BoundsCenter - snapshot.BoundsExtents - padding);
                boundsMax = MxCameraVector3.Max(boundsMax, snapshot.BoundsCenter + snapshot.BoundsExtents + padding);
                if (!primary.IsValid || snapshot.IsPrimary || (requestedGroup != null && requestedGroup.PrimaryTarget.Equals(snapshot.TargetRef)))
                    primary = snapshot.TargetRef;
            }

            if (totalWeight <= 0f)
                totalWeight = 1f;

            weightedCenter /= totalWeight;
            float radius = 0f;
            for (int i = 0; i < snapshots.Count; i++)
                radius = Math.Max(radius, MxCameraVector3.Distance(weightedCenter, snapshots[i].BoundsCenter + snapshots[i].BoundsExtents));

            bool boundsExceeded = profile.MaxTargetRadius > 0f && radius > profile.MaxTargetRadius;
            MxCameraTargetGroupId groupId = requestedGroup != null ? requestedGroup.GroupId : default;
            return new MxCameraTargetGroupState(groupId, weightedCenter, boundsMin, boundsMax, radius, primary, snapshots.Count, boundsExceeded);
        }

        private static bool ContainsTarget(IReadOnlyList<MxCameraTargetRef> targets, MxCameraTargetRef target)
        {
            if (targets == null)
                return false;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].Equals(target))
                    return true;
            }

            return false;
        }

        private static int CompareSnapshots(MxCameraTargetSnapshot left, MxCameraTargetSnapshot right)
        {
            return string.CompareOrdinal(left.TargetRef.Value, right.TargetRef.Value);
        }

        private static MxCameraState BuildState(
            MxCameraEvaluationContext context,
            MxCameraProfileDefinition profile,
            MxCameraTargetGroupState groupState,
            float aspect,
            float zoomDelta,
            MxCameraVector3 shakeOffset,
            MxCameraStateSource stateSource)
        {
            float fov = Clamp(profile.FieldOfView + zoomDelta, profile.MinFieldOfView, profile.MaxFieldOfView);
            float distance = Clamp(profile.Distance - zoomDelta, profile.MinDistance, profile.MaxDistance);
            float orthographicSize = Clamp(profile.OrthographicSize + zoomDelta, profile.MinOrthographicSize, profile.MaxOrthographicSize);

            if (profile.Mode == MxCameraMode.GroupFollowPerspective && groupState.Radius > 0f)
            {
                float fovRadians = fov * (float)Math.PI / 180f;
                float requiredDistance = groupState.Radius / Math.Max(0.01f, (float)Math.Tan(fovRadians * 0.5f));
                distance = Clamp(requiredDistance, profile.MinDistance, profile.MaxDistance);
            }
            else if (profile.Mode == MxCameraMode.GroupFollowOrthographic)
            {
                MxCameraVector3 size = groupState.BoundsMax - groupState.BoundsMin;
                float vertical = Math.Max(0.01f, size.Y * 0.5f);
                float horizontal = aspect > 0f ? size.X * 0.5f / aspect : size.X * 0.5f;
                orthographicSize = Clamp(Math.Max(profile.OrthographicSize, Math.Max(vertical, horizontal)), profile.MinOrthographicSize, profile.MaxOrthographicSize);
            }

            MxCameraEulerRotation rotation = new MxCameraEulerRotation(profile.Pitch, profile.Yaw, 0f);
            MxCameraVector3 forward = DirectionFromYawPitch(profile.Yaw, profile.Pitch);
            MxCameraVector3 focus = groupState.Center + profile.WorldOffset;
            MxCameraVector3 position = focus - forward * distance + profile.LocalOffset + shakeOffset;
            float utilization = profile.MaxTargetRadius > 0f ? groupState.Radius / profile.MaxTargetRadius : 0f;
            return new MxCameraState(
                context.RigId.IsValid ? context.RigId : profile.ProfileId.IsValid ? new MxCameraRigId(profile.ProfileId.Value) : default,
                profile.ProfileId,
                position,
                forward,
                MxCameraVector3.Up,
                rotation,
                profile.ProjectionKind,
                fov,
                orthographicSize,
                focus,
                shakeOffset,
                groupState.Radius,
                utilization,
                stateSource);
        }

        private static MxCameraState BuildFallbackState(MxCameraEvaluationContext context, MxCameraProfileDefinition profile, MxCameraVector3 shakeOffset, MxCameraStateSource source)
        {
            MxCameraProfileId profileId = profile != null ? profile.ProfileId : default;
            MxCameraVector3 focus = profile != null ? profile.FallbackFocus : MxCameraVector3.Zero;
            MxCameraVector3 position = profile != null ? profile.FallbackPosition : MxCameraState.Empty.Position;
            float fov = profile != null ? Clamp(profile.FieldOfView, profile.MinFieldOfView, profile.MaxFieldOfView) : 60f;
            float ortho = profile != null ? Clamp(profile.OrthographicSize, profile.MinOrthographicSize, profile.MaxOrthographicSize) : 5f;
            MxCameraProjectionKind projection = profile != null ? profile.ProjectionKind : MxCameraProjectionKind.Perspective;
            MxCameraVector3 forward = (focus - position).Normalized;
            if (forward.SqrMagnitude <= 0f)
                forward = MxCameraVector3.Forward;

            return new MxCameraState(
                context.RigId,
                profileId,
                position + shakeOffset,
                forward,
                MxCameraVector3.Up,
                new MxCameraEulerRotation(profile != null ? profile.Pitch : 0f, profile != null ? profile.Yaw : 0f, 0f),
                projection,
                fov,
                ortho,
                focus,
                shakeOffset,
                0f,
                0f,
                source);
        }

        private static MxCameraVector3 DirectionFromYawPitch(float yawDegrees, float pitchDegrees)
        {
            double yaw = yawDegrees * Math.PI / 180.0;
            double pitch = pitchDegrees * Math.PI / 180.0;
            float x = (float)(Math.Sin(yaw) * Math.Cos(pitch));
            float y = (float)(-Math.Sin(pitch));
            float z = (float)(Math.Cos(yaw) * Math.Cos(pitch));
            return new MxCameraVector3(x, y, z).Normalized;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (max < min)
                max = min;

            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private MxCameraEvaluationResult StoreResult(
            MxCameraEvaluationContext context,
            MxCameraState state,
            MxCameraTargetGroupState groupState,
            List<ulong> accepted,
            List<ulong> rejected,
            List<MxCameraDiagnostic> diagnostics,
            int shakeCount,
            MxCameraEvaluationStatus status)
        {
            for (int i = 0; i < diagnostics.Count; i++)
                AddRecentDiagnostic(diagnostics[i]);

            var result = new MxCameraEvaluationResult(
                status,
                context.Frame,
                context.RigId,
                state.ProfileId,
                state,
                groupState,
                accepted,
                rejected,
                diagnostics,
                new MxCameraDebugSummary(accepted.Count, rejected.Count, diagnostics.Count, shakeCount));
            _lastResult = result;
            return result;
        }

        private void AddRecentDiagnostic(MxCameraDiagnostic diagnostic)
        {
            while (_recentDiagnostics.Count >= Math.Max(1, MaxRecentDiagnostics))
                _recentDiagnostics.Dequeue();

            _recentDiagnostics.Enqueue(diagnostic);
        }

        private static MxCameraMode ResolveMode(MxCameraState state)
        {
            return state.ProjectionKind == MxCameraProjectionKind.Orthographic
                ? MxCameraMode.GroupFollowOrthographic
                : MxCameraMode.GroupFollowPerspective;
        }
    }

    public readonly struct MxCameraFacingBasis
    {
        public MxCameraFacingBasis(MxCameraVector3 forward, MxCameraVector3 right)
        {
            Forward = forward;
            Right = right;
        }

        public MxCameraVector3 Forward { get; }
        public MxCameraVector3 Right { get; }
    }

    public static class MxCameraFacingBasisResolver
    {
        public static MxCameraFacingBasis Resolve(MxCameraState state)
        {
            MxCameraVector3 forward = state != null ? state.ViewForward : MxCameraVector3.Forward;
            forward = new MxCameraVector3(forward.X, 0f, forward.Z).Normalized;
            if (forward.SqrMagnitude <= 0f)
                forward = MxCameraVector3.Forward;

            MxCameraVector3 right = MxCameraVector3.Cross(MxCameraVector3.Up, forward).Normalized;
            if (right.SqrMagnitude <= 0f)
                right = MxCameraVector3.Right;

            return new MxCameraFacingBasis(forward, right);
        }
    }
}
