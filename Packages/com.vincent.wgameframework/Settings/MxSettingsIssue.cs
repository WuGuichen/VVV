using System;

namespace MxFramework.Settings
{
    public enum MxSettingsIssueCode
    {
        None = 0,
        MissingFile = 1001,
        InvalidPath = 1002,
        InvalidJson = 1003,
        UnsupportedFormat = 1004,
        InvalidProfile = 1005,
        IoError = 1006,
        InvalidValue = 1007,
        AdapterUnavailable = 2001,
        InvalidTarget = 2002,
        ApplyFailed = 2003,
        OperationFailed = 2004,
        UnsupportedSetting = 2005
    }

    public readonly struct MxSettingsIssue
    {
        public MxSettingsIssue(
            MxSettingsIssueCode code,
            string path,
            string message,
            Exception exception = null)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public MxSettingsIssueCode Code { get; }
        public string Path { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public bool IsNone => Code == MxSettingsIssueCode.None;

        public static MxSettingsIssue None => new MxSettingsIssue(
            MxSettingsIssueCode.None,
            string.Empty,
            string.Empty);

        public override string ToString()
        {
            return IsNone ? "None" : Code + " " + Path + ": " + Message;
        }
    }
}
