namespace MxFramework.Resources
{
    public readonly struct ResourceLoadResult<T>
    {
        private ResourceLoadResult(bool success, T value, ResourceError error)
        {
            Success = success;
            Value = value;
            Error = error;
        }

        public bool Success { get; }
        public T Value { get; }
        public ResourceError Error { get; }

        public static ResourceLoadResult<T> Loaded(T value)
        {
            return new ResourceLoadResult<T>(true, value, ResourceError.None);
        }

        public static ResourceLoadResult<T> Failed(ResourceError error)
        {
            return new ResourceLoadResult<T>(false, default, error);
        }
    }
}
