using MxFramework.Runtime;

namespace MxFramework.Persistence.RuntimeBridge
{
    public enum RuntimeSaveGameCompatibilityStatus
    {
        Unknown = 0,
        Compatible = 1,
        MigrationRequired = 2,
        Incompatible = 3
    }

    /// <summary>Stable compatibility failure categories. Unknown is fail-closed.</summary>
    public enum RuntimeSaveGameCompatibilityErrorCode
    {
        None = 0,
        Unknown = 1,
        RuntimeRejected = 2,
        BuildMismatch = 3,
        MissingMigration = 4
    }

    /// <summary>Immutable compatibility decision.</summary>
    /// <remarks>NoAlloc. Factory calls and property reads do not allocate on their normal paths.</remarks>
    public readonly struct RuntimeSaveGameCompatibilityResult
    {
        private readonly RuntimeSaveGameCompatibilityErrorCode _errorCode;

        private RuntimeSaveGameCompatibilityResult(
            RuntimeSaveGameCompatibilityStatus status,
            RuntimeSaveGameCompatibilityErrorCode errorCode,
            RuntimeSaveStateError error)
        {
            Status = status;
            _errorCode = errorCode;
            Error = error;
        }

        public RuntimeSaveGameCompatibilityStatus Status { get; }
        public RuntimeSaveGameCompatibilityErrorCode ErrorCode =>
            Status == RuntimeSaveGameCompatibilityStatus.Unknown && _errorCode == RuntimeSaveGameCompatibilityErrorCode.None
                ? RuntimeSaveGameCompatibilityErrorCode.Unknown
                : _errorCode;
        public RuntimeSaveStateError Error { get; }
        public bool IsCompatible => Status == RuntimeSaveGameCompatibilityStatus.Compatible ||
                                    Status == RuntimeSaveGameCompatibilityStatus.MigrationRequired;
        public static RuntimeSaveGameCompatibilityResult Compatible => new RuntimeSaveGameCompatibilityResult(
            RuntimeSaveGameCompatibilityStatus.Compatible,
            RuntimeSaveGameCompatibilityErrorCode.None,
            null);
        public static RuntimeSaveGameCompatibilityResult MigrationRequired => new RuntimeSaveGameCompatibilityResult(
            RuntimeSaveGameCompatibilityStatus.MigrationRequired,
            RuntimeSaveGameCompatibilityErrorCode.None,
            null);

        /// <remarks>NoAlloc on the normal failure construction path.</remarks>
        public static RuntimeSaveGameCompatibilityResult Incompatible(
            RuntimeSaveGameCompatibilityErrorCode errorCode,
            RuntimeSaveStateError error = null)
        {
            if (errorCode == RuntimeSaveGameCompatibilityErrorCode.None ||
                errorCode == RuntimeSaveGameCompatibilityErrorCode.Unknown)
            {
                throw new System.ArgumentException("Incompatible results require a stable non-default error code.", nameof(errorCode));
            }

            return new RuntimeSaveGameCompatibilityResult(RuntimeSaveGameCompatibilityStatus.Incompatible, errorCode, error);
        }
    }

    /// <summary>Read-only compatibility policy between persisted build facts and one Runtime restore profile.</summary>
    public interface IRuntimeSaveGameCompatibilityChecker
    {
        /// <remarks>LowFreqAlloc. The checker must not mutate the profile or any live Runtime object.</remarks>
        RuntimeSaveGameCompatibilityResult Check(
            SaveGameBuildDescriptor persistedBuild,
            RuntimeSaveStateRestoreProfile restoreProfile);
    }
}
