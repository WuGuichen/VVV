namespace MxFramework.UI
{
    public enum MxUiOpenErrorCode
    {
        None = 0,
        InvalidViewId = 1001,
        ViewNotFound = 1002,
        ViewCreateFailed = 1003,
        ResourcesPending = 1004,
        OperationCancelled = 1005
    }

    public readonly struct MxUiOpenResult
    {
        private MxUiOpenResult(bool success, MxUiOpenErrorCode errorCode, string message, IMxUiView view)
        {
            Success = success;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            View = view;
        }

        public bool Success { get; }
        public bool Failed => !Success;
        public MxUiOpenErrorCode ErrorCode { get; }
        public string Message { get; }
        public IMxUiView View { get; }

        public static MxUiOpenResult Opened(IMxUiView view)
        {
            if (view == null)
            {
                return Fail(MxUiOpenErrorCode.ViewCreateFailed, "UI view is required for a successful open result.");
            }

            return new MxUiOpenResult(true, MxUiOpenErrorCode.None, string.Empty, view);
        }

        public static MxUiOpenResult Fail(MxUiOpenErrorCode errorCode, string message)
        {
            if (errorCode == MxUiOpenErrorCode.None)
            {
                errorCode = MxUiOpenErrorCode.ViewCreateFailed;
            }

            return new MxUiOpenResult(false, errorCode, message, null);
        }

        public override string ToString()
        {
            return Success ? "Opened" : ErrorCode + ": " + Message;
        }
    }
}
