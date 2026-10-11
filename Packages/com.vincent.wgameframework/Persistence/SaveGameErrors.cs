using System;

namespace MxFramework.Persistence
{
    public enum SaveGameErrorCode
    {
        None = 0,
        InvalidProfile = 1000,
        InvalidSlot = 1001,
        InvalidOperationId = 1002,
        InvalidSelection = 1003,
        Busy = 1004,
        GenerationConflict = 1005,
        GenerationExhausted = 1006,
        SlotNotFound = 1007,
        SlotDeleted = 1008,
        GenerationNotFound = 1009,
        LimitExceeded = 1010,
        RuntimeCheckpointRejected = 1100,
        InvalidTimestamp = 1200,
        SerializeFailed = 1201,
        RuntimeSerializationFailed = 1202,
        StoreUnavailable = 1300,
        FaultInjected = 1301,
        InvalidLocation = 1302,
        AtomicCommitUnsupported = 1303,
        PermissionDenied = 1304,
        QuotaExceeded = 1305,
        IoFailure = 1306,
        InvalidEnvelope = 1400,
        IntegrityFailed = 1401,
        UnsupportedEnvelopeVersion = 1402,
        UnsupportedEnvelopeFeature = 1403,
        UnsupportedTransform = 1404,
        EnvelopePayloadMismatch = 1405,
        RuntimeCompatibilityFailed = 1500,
        CandidateRestoreFailed = 1600,
        Timeout = 1700
    }

    public enum SaveGameOperationPhase
    {
        None = 0,
        Validation = 1,
        Serialization = 2,
        Integrity = 3,
        Publication = 4,
        Read = 5,
        Listing = 6,
        Deletion = 7,
        Retention = 8
    }

    /// <summary>Describes a structured Persistence Core failure. Messages and exceptions are diagnostic only.</summary>
    public readonly struct SaveGameError : IEquatable<SaveGameError>
    {
        public SaveGameError(SaveGameErrorCode code, SaveGameOperationPhase phase, string path, string message, Exception exception = null)
        {
            Code = code; Phase = phase; Path = path ?? string.Empty; Message = message ?? string.Empty; Exception = exception;
        }
        public SaveGameErrorCode Code { get; }
        public SaveGameOperationPhase Phase { get; }
        public string Path { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public bool IsNone => Code == SaveGameErrorCode.None;
        public static SaveGameError None => default;
        public bool Equals(SaveGameError other) => Code == other.Code && Phase == other.Phase && string.Equals(Path, other.Path, StringComparison.Ordinal) && string.Equals(Message, other.Message, StringComparison.Ordinal) && ReferenceEquals(Exception, other.Exception);
        public override bool Equals(object obj) => obj is SaveGameError other && Equals(other);
        public override int GetHashCode() => ((int)Code * 397) ^ (int)Phase;
        public override string ToString() => IsNone ? "None" : Code + " at " + Path + ": " + Message;
    }

    /// <summary>Identifies a typed non-fatal persistence warning.</summary>
    public enum SaveGameWarningCode
    {
        None = 0,
        RetentionCleanupFailed = 1
    }

    /// <summary>Describes a non-fatal persistence outcome without exposing provider physical paths.</summary>
    public readonly struct SaveGameWarning : IEquatable<SaveGameWarning>
    {
        private readonly string _path;

        private SaveGameWarning(SaveGameWarningCode code, SaveGameOperationPhase phase, string path)
        {
            Code = code;
            Phase = phase;
            _path = path;
        }

        public SaveGameWarningCode Code { get; }
        public SaveGameOperationPhase Phase { get; }
        public string Path => _path ?? string.Empty;
        public bool IsNone => Code == SaveGameWarningCode.None;
        public static SaveGameWarning None => default;

        /// <summary>Gets the warning returned when best-effort retention cleanup fails after publication succeeds.</summary>
        public static SaveGameWarning RetentionCleanupFailed =>
            new SaveGameWarning(SaveGameWarningCode.RetentionCleanupFailed, SaveGameOperationPhase.Retention, "retention");

        public bool Equals(SaveGameWarning other) =>
            Code == other.Code && Phase == other.Phase && string.Equals(Path, other.Path, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is SaveGameWarning other && Equals(other);
        public override int GetHashCode() => ((int)Code * 397) ^ (int)Phase;
        public override string ToString() => IsNone ? "None" : Code + " at " + Path;
    }
}
