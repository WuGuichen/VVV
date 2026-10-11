using System;

namespace MxFramework.Platform
{
    /// <summary>
    /// Identifies a recoverable platform-boundary failure.
    /// </summary>
    public enum PlatformErrorCode
    {
        None = 0,
        Unsupported = 1,
        Unavailable = 2,
        InvalidArgument = 3,
        InvalidLocation = 4,
        PermissionDenied = 5,
        PermissionRestricted = 6,
        Busy = 7,
        RequestFailed = 8,
        Cancelled = 9,
        AdapterFailure = 10
    }

    /// <summary>
    /// Describes a structured platform error and optional local diagnostic exception.
    /// </summary>
    /// <remarks>
    /// NoAlloc when copied or inspected. Constructing failure diagnostics is LowFreqAlloc.
    /// The exception is local diagnostic context and must not cross SaveState or network boundaries.
    /// </remarks>
    public readonly struct PlatformError : IEquatable<PlatformError>
    {
        private readonly string _message;

        /// <summary>
        /// Creates a platform error value.
        /// </summary>
        /// <remarks>LowFreqAlloc when the caller constructs a diagnostic message or exception.</remarks>
        public PlatformError(PlatformErrorCode code, string message = "", Exception exception = null)
        {
            Code = code;
            _message = message ?? string.Empty;
            Exception = exception;
        }

        /// <summary>
        /// Gets the stable error code.
        /// </summary>
        public PlatformErrorCode Code { get; }

        /// <summary>
        /// Gets optional human-readable diagnostic context.
        /// </summary>
        public string Message => _message ?? string.Empty;

        /// <summary>
        /// Gets the optional adapter exception for local diagnostics.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets whether this value represents no error.
        /// </summary>
        public bool IsNone => Code == PlatformErrorCode.None;

        /// <summary>
        /// Gets the canonical success value.
        /// </summary>
        public static PlatformError None => default;

        public bool Equals(PlatformError other)
        {
            return Code == other.Code
                && string.Equals(Message, other.Message, StringComparison.Ordinal)
                && ReferenceEquals(Exception, other.Exception);
        }

        public override bool Equals(object obj)
        {
            return obj is PlatformError other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)Code;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Message);
                hashCode = (hashCode * 397) ^ (Exception == null ? 0 : Exception.GetHashCode());
                return hashCode;
            }
        }

        public static bool operator ==(PlatformError left, PlatformError right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlatformError left, PlatformError right)
        {
            return !left.Equals(right);
        }
    }
}
