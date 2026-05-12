namespace MxFramework.Config
{
    public readonly struct ConfigResult<T> where T : IConfigData
    {
        private ConfigResult(bool success, T value, ConfigError error, string message)
        {
            Success = success;
            Value = value;
            Error = error;
            Message = message;
        }

        public bool Success { get; }
        public T Value { get; }
        public ConfigError Error { get; }
        public string Message { get; }

        public static ConfigResult<T> Found(T value)
        {
            return new ConfigResult<T>(true, value, ConfigError.None, string.Empty);
        }

        public static ConfigResult<T> Failed(ConfigError error, string message)
        {
            return new ConfigResult<T>(false, default, error, message);
        }
    }

    public enum ConfigError
    {
        None,
        InvalidId,
        NotFound,
        DuplicateId,
        TypeNotRegistered,
        TypeMismatch
    }
}
