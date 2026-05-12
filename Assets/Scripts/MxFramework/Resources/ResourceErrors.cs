namespace MxFramework.Resources
{
    public enum ResourceErrorCode
    {
        None,
        InvalidKey,
        NotFound,
        TypeMismatch,
        ProviderMissing,
        ProviderFailed,
        DependencyInvalid,
        DuplicateKey,
        InvalidCatalog,
        HandleReleased,
        Cancelled
    }

    public readonly struct ResourceError
    {
        public ResourceError(
            ResourceErrorCode code,
            ResourceKey key,
            string providerId,
            string message,
            string address = "")
        {
            Code = code;
            Key = key;
            ProviderId = providerId ?? string.Empty;
            Message = message ?? string.Empty;
            Address = address ?? string.Empty;
        }

        public ResourceErrorCode Code { get; }
        public ResourceKey Key { get; }
        public string ProviderId { get; }
        public string Message { get; }
        public string Address { get; }
        public bool IsNone => Code == ResourceErrorCode.None;

        public static ResourceError None => new ResourceError(ResourceErrorCode.None, default, string.Empty, string.Empty);

        public override string ToString()
        {
            return Code + " " + Key + " " + Message;
        }
    }
}
