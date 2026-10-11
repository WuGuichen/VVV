namespace MxFramework.Audio
{
    public enum AudioErrorCode
    {
        None = 0,
        NotInitialized = 1001,
        InvalidEvent = 1101,
        InvalidBus = 1102,
        InvalidParameter = 1103,
        InvalidHandle = 1201,
        BackendUnavailable = 1301,
        BackendFailed = 1302,
        RequestRejected = 1401
    }

    public readonly struct AudioResult
    {
        private AudioResult(bool success, AudioErrorCode errorCode, string message)
        {
            Success = success;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public AudioErrorCode ErrorCode { get; }
        public string Message { get; }
        public bool Failed => !Success;

        public static AudioResult Ok()
        {
            return new AudioResult(true, AudioErrorCode.None, string.Empty);
        }

        public static AudioResult Fail(AudioErrorCode errorCode, string message)
        {
            if (errorCode == AudioErrorCode.None)
            {
                errorCode = AudioErrorCode.BackendFailed;
            }

            return new AudioResult(false, errorCode, message);
        }

        public override string ToString()
        {
            return Success ? "Ok" : ErrorCode + ": " + Message;
        }
    }

    public readonly struct AudioPlayResult
    {
        private AudioPlayResult(AudioResult result, AudioHandle handle)
        {
            Result = result;
            Handle = handle;
        }

        public AudioResult Result { get; }
        public AudioHandle Handle { get; }
        public bool Success => Result.Success;
        public AudioErrorCode ErrorCode => Result.ErrorCode;
        public string Message => Result.Message;

        public static AudioPlayResult Ok(AudioHandle handle)
        {
            return new AudioPlayResult(AudioResult.Ok(), handle);
        }

        public static AudioPlayResult Fail(AudioErrorCode errorCode, string message)
        {
            return new AudioPlayResult(AudioResult.Fail(errorCode, message), AudioHandle.Invalid);
        }
    }
}
