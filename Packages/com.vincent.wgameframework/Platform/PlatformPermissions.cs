using System;

namespace MxFramework.Platform
{
    /// <summary>
    /// Identifies a stable platform permission. Zero and negative values are invalid.
    /// </summary>
    /// <remarks>NoAlloc when copied, compared, or inspected.</remarks>
    public readonly struct PlatformPermissionId : IEquatable<PlatformPermissionId>
    {
        /// <summary>
        /// Creates a permission identifier. Use <see cref="IsValid"/> before requesting it.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public PlatformPermissionId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;

        /// <summary>
        /// Gets the built-in microphone permission identifier.
        /// </summary>
        public static PlatformPermissionId Microphone => new PlatformPermissionId(1);

        public bool Equals(PlatformPermissionId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is PlatformPermissionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(PlatformPermissionId left, PlatformPermissionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlatformPermissionId left, PlatformPermissionId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Identifies the current host permission fact.
    /// </summary>
    public enum PlatformPermissionStatus
    {
        Unknown = 0,
        Unsupported = 1,
        NotRequired = 2,
        NotDetermined = 3,
        Granted = 4,
        Denied = 5,
        Restricted = 6
    }

    /// <summary>
    /// Identifies the lifecycle state of one permission request operation.
    /// </summary>
    public enum PlatformPermissionOperationStatus
    {
        Unknown = 0,
        Pending = 1,
        Succeeded = 2,
        Failed = 3,
        Cancelled = 4
    }

    /// <summary>
    /// Contains either a trusted permission fact or a structured query failure.
    /// </summary>
    /// <remarks>NoAlloc when copied or inspected.</remarks>
    public readonly struct PlatformPermissionQueryResult
    {
        private PlatformPermissionQueryResult(
            bool success,
            PlatformPermissionStatus status,
            PlatformError error)
        {
            Success = success;
            Status = status;
            Error = error;
        }

        public bool Success { get; }
        public PlatformPermissionStatus Status { get; }
        public PlatformError Error { get; }

        /// <summary>
        /// Creates a successful trusted permission fact.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public static PlatformPermissionQueryResult Ok(PlatformPermissionStatus status)
        {
            if (status < PlatformPermissionStatus.Unsupported
                || status > PlatformPermissionStatus.Restricted)
            {
                return Fail(new PlatformError(
                    PlatformErrorCode.AdapterFailure,
                    "A successful permission query cannot return Unknown."));
            }

            return new PlatformPermissionQueryResult(true, status, PlatformError.None);
        }

        /// <summary>
        /// Creates a failed permission query whose status is Unknown.
        /// </summary>
        /// <remarks>NoAlloc when the supplied error is already constructed.</remarks>
        public static PlatformPermissionQueryResult Fail(PlatformError error)
        {
            if (error.IsNone)
            {
                error = new PlatformError(PlatformErrorCode.AdapterFailure, "Permission query failed.");
            }

            return new PlatformPermissionQueryResult(false, PlatformPermissionStatus.Unknown, error);
        }
    }

    /// <summary>
    /// Exposes one independently owned, pollable permission request.
    /// </summary>
    public interface IPlatformPermissionOperation
    {
        /// <remarks>NoAlloc after operation creation.</remarks>
        PlatformPermissionOperationStatus OperationStatus { get; }

        /// <remarks>NoAlloc after operation creation.</remarks>
        bool IsDone { get; }

        /// <remarks>NoAlloc after operation creation.</remarks>
        bool CanCancel { get; }

        /// <remarks>NoAlloc after operation creation.</remarks>
        PlatformPermissionStatus PermissionStatus { get; }

        /// <remarks>NoAlloc after operation creation.</remarks>
        PlatformError Error { get; }

        /// <summary>
        /// Requests cooperative cancellation. Only provider-confirmed cancellation may produce Cancelled.
        /// </summary>
        /// <remarks>NoAlloc after operation creation.</remarks>
        void RequestCancel();
    }

    /// <summary>
    /// Queries and requests platform permissions.
    /// </summary>
    public interface IPlatformPermissionService
    {
        /// <remarks>LowFreqAlloc at the platform boundary.</remarks>
        PlatformPermissionQueryResult Query(PlatformPermissionId permissionId);

        /// <summary>
        /// Starts an independently owned permission request and never returns null.
        /// </summary>
        /// <remarks>LowFreqAlloc when creating an operation or starting a host request.</remarks>
        IPlatformPermissionOperation Request(PlatformPermissionId permissionId);
    }

    /// <summary>
    /// Provides an immutable completed permission operation for immediate outcomes.
    /// </summary>
    /// <remarks>LowFreqAlloc at construction; property polling and cancellation are NoAlloc.</remarks>
    public sealed class CompletedPlatformPermissionOperation : IPlatformPermissionOperation
    {
        private CompletedPlatformPermissionOperation(
            PlatformPermissionOperationStatus operationStatus,
            PlatformPermissionStatus permissionStatus,
            PlatformError error)
        {
            OperationStatus = operationStatus;
            PermissionStatus = permissionStatus;
            Error = error;
        }

        public PlatformPermissionOperationStatus OperationStatus { get; }
        public bool IsDone => true;
        public bool CanCancel => false;
        public PlatformPermissionStatus PermissionStatus { get; }
        public PlatformError Error { get; }

        /// <summary>
        /// Creates a successful operation for Granted or NotRequired.
        /// </summary>
        /// <remarks>LowFreqAlloc for the operation object.</remarks>
        public static CompletedPlatformPermissionOperation Succeeded(PlatformPermissionStatus status)
        {
            if (status != PlatformPermissionStatus.Granted
                && status != PlatformPermissionStatus.NotRequired)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    "Only Granted or NotRequired may complete successfully.");
            }

            return new CompletedPlatformPermissionOperation(
                PlatformPermissionOperationStatus.Succeeded,
                status,
                PlatformError.None);
        }

        /// <summary>
        /// Creates a failed operation while retaining the final permission fact when available.
        /// </summary>
        /// <remarks>LowFreqAlloc for the operation object.</remarks>
        public static CompletedPlatformPermissionOperation Failed(
            PlatformPermissionStatus status,
            PlatformError error)
        {
            if (status < PlatformPermissionStatus.Unknown
                || status > PlatformPermissionStatus.Restricted
                || status == PlatformPermissionStatus.Granted
                || status == PlatformPermissionStatus.NotRequired)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    "Granted and NotRequired must use a successful operation.");
            }

            if (error.IsNone)
                throw new ArgumentException("A failed operation requires an error.", nameof(error));

            return new CompletedPlatformPermissionOperation(
                PlatformPermissionOperationStatus.Failed,
                status,
                error);
        }

        /// <summary>
        /// Creates a provider-confirmed cancelled operation with no new permission fact.
        /// </summary>
        /// <remarks>LowFreqAlloc for the operation object.</remarks>
        public static CompletedPlatformPermissionOperation Cancelled()
        {
            return new CompletedPlatformPermissionOperation(
                PlatformPermissionOperationStatus.Cancelled,
                PlatformPermissionStatus.Unknown,
                PlatformError.None);
        }

        /// <summary>
        /// Does nothing because this operation is already terminal.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public void RequestCancel()
        {
        }
    }
}
