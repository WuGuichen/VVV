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
        CustomStateMigrationFailed = 1302
    }

    public sealed class RuntimeSaveStateError
    {
        public RuntimeSaveStateError(
            RuntimeSaveStateErrorCode code,
            string path,
            string message,
            int sourceSchemaVersion = -1,
            int targetSchemaVersion = -1,
            Exception exception = null)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
            SourceSchemaVersion = sourceSchemaVersion;
            TargetSchemaVersion = targetSchemaVersion;
            Exception = exception;
        }

        public RuntimeSaveStateErrorCode Code { get; }
        public string Path { get; }
        public string Message { get; }
        public int SourceSchemaVersion { get; }
        public int TargetSchemaVersion { get; }
        public Exception Exception { get; }
        public bool IsNone => Code == RuntimeSaveStateErrorCode.None;

        public static RuntimeSaveStateError None => new RuntimeSaveStateError(RuntimeSaveStateErrorCode.None, string.Empty, string.Empty);

        public override string ToString()
        {
            if (IsNone)
            {
                return "None";
            }

            string path = string.IsNullOrEmpty(Path) ? "$" : Path;
            return Code + " Path=" + path + " SourceSchema=" + SourceSchemaVersion + " TargetSchema=" + TargetSchemaVersion + " " + Message;
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
