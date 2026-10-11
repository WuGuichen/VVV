using System;

namespace MxFramework.Runtime
{
    public enum RuntimeSaveStateErrorCode
    {
        None = 0,
        UnsupportedVersion = 1001,
        MissingMigration = 1002,
        InvalidDocument = 1003,
        MissingConfig = 1101,
        MissingResource = 1102,
        UnknownEntity = 1201,
        UnknownBuff = 1202,
        UnknownModifier = 1203,
        CustomStateMismatch = 1301,
        CustomStateMigrationFailed = 1302,
        PendingWork = 1401,
        CompositionFailed = 1501,
        RootHeaderMismatch = 1502,
        RootHeaderPolluted = 1503,
        RestoreProfileMismatch = 1504,
        FrameChangedDuringMigration = 1505,
        DocumentLimitExceeded = 1506
    }

    public sealed class RuntimeSaveStateError
    {
        /// <summary>Creates a save-state error using the compatibility detail value zero.</summary>
        /// <remarks>
        /// LowFreqAlloc. This six-parameter constructor preserves its original source and binary signature.
        /// </remarks>
        public RuntimeSaveStateError(
            RuntimeSaveStateErrorCode code,
            string path,
            string message,
            int sourceSchemaVersion = -1,
            int targetSchemaVersion = -1,
            Exception exception = null)
            : this(
                code,
                path,
                message,
                sourceSchemaVersion,
                targetSchemaVersion,
                exception,
                0)
        {
        }

        /// <summary>Creates a structured save-state error with an additional numeric detail code.</summary>
        /// <remarks>
        /// LowFreqAlloc. The existing six-parameter constructor remains available with the same signature and
        /// delegates here with <c>detailCode = 0</c> for source and binary compatibility.
        /// </remarks>
        public RuntimeSaveStateError(
            RuntimeSaveStateErrorCode code,
            string path,
            string message,
            int sourceSchemaVersion,
            int targetSchemaVersion,
            Exception exception,
            int detailCode)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
            SourceSchemaVersion = sourceSchemaVersion;
            TargetSchemaVersion = targetSchemaVersion;
            Exception = exception;
            DetailCode = detailCode;
        }

        public RuntimeSaveStateErrorCode Code { get; }
        public string Path { get; }
        public string Message { get; }
        public int SourceSchemaVersion { get; }
        public int TargetSchemaVersion { get; }
        public Exception Exception { get; }

        /// <summary>Gets an optional structured numeric detail, or zero when no detail is present.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int DetailCode { get; }
        public bool IsNone => Code == RuntimeSaveStateErrorCode.None;

        public static RuntimeSaveStateError None => new RuntimeSaveStateError(RuntimeSaveStateErrorCode.None, string.Empty, string.Empty);

        public override string ToString()
        {
            if (IsNone)
            {
                return "None";
            }

            string path = string.IsNullOrEmpty(Path) ? "$" : Path;
            string value = Code + " Path=" + path + " SourceSchema=" + SourceSchemaVersion + " TargetSchema=" + TargetSchemaVersion;
            if (DetailCode != 0)
            {
                value += " Detail=" + DetailCode;
            }

            return value + " " + Message;
        }
    }

    public readonly struct RuntimeSaveStateResult<T>
    {
        private RuntimeSaveStateResult(bool success, T value, RuntimeSaveStateError error)
        {
            Success = success;
            Value = value;
            Error = error ?? RuntimeSaveStateError.None;
        }

        public bool Success { get; }
        public T Value { get; }
        public RuntimeSaveStateError Error { get; }

        public static RuntimeSaveStateResult<T> Succeeded(T value)
        {
            return new RuntimeSaveStateResult<T>(true, value, RuntimeSaveStateError.None);
        }

        public static RuntimeSaveStateResult<T> Failed(RuntimeSaveStateError error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            return new RuntimeSaveStateResult<T>(false, default, error);
        }
    }
}
