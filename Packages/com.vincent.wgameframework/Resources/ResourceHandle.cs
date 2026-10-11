using System;

namespace MxFramework.Resources
{
    public enum ResourceHandleState
    {
        None,
        Loading,
        Loaded,
        Failed,
        Released
    }

    public sealed class ResourceHandle<T>
    {
        internal ResourceHandle(ResourceKey key, ResourceCatalogEntry entry, string providerId, T value)
        {
            Key = key;
            Entry = entry;
            ProviderId = providerId ?? string.Empty;
            Value = value;
            State = ResourceHandleState.Loaded;
        }

        public ResourceKey Key { get; }
        public ResourceCatalogEntry Entry { get; }
        public string ProviderId { get; }
        public T Value { get; }
        public ResourceHandleState State { get; private set; }
        public bool IsReleased => State == ResourceHandleState.Released;

        internal bool TryMarkReleased()
        {
            if (State == ResourceHandleState.Released)
                return false;

            State = ResourceHandleState.Released;
            return true;
        }

        internal void MarkFailed()
        {
            if (State != ResourceHandleState.Released)
                State = ResourceHandleState.Failed;
        }

        public override string ToString()
        {
            return Key + " " + State;
        }
    }

    public interface IResourceOperation<T>
    {
        bool IsDone { get; }
        bool IsCancelled { get; }
        float Progress { get; }
        ResourceLoadResult<T> Result { get; }
        void Cancel();
    }

    public sealed class ImmediateResourceOperation<T> : IResourceOperation<T>
    {
        private ResourceLoadResult<T> _result;

        public ImmediateResourceOperation(ResourceLoadResult<T> result)
        {
            _result = result;
        }

        public bool IsDone => true;
        public bool IsCancelled { get; private set; }
        public float Progress => 1f;
        public ResourceLoadResult<T> Result => _result;

        public void Cancel()
        {
            if (IsCancelled)
                return;

            IsCancelled = true;
            _result = ResourceLoadResult<T>.Failed(new ResourceError(ResourceErrorCode.Cancelled, default, string.Empty, "Resource operation was cancelled."));
        }
    }
}
