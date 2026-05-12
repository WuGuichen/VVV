using System.Collections.Generic;
using System.Threading;

namespace MxFramework.Resources
{
    public sealed class MemoryResourceProvider : IResourceProvider
    {
        private readonly Dictionary<string, object> _assets = new Dictionary<string, object>(System.StringComparer.Ordinal);

        public string ProviderId => "memory";
        public int LoadCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public MemoryResourceProvider Register(string address, object value)
        {
            _assets[address ?? string.Empty] = value;
            return this;
        }

        public bool CanLoad(ResourceCatalogEntry entry)
        {
            return entry != null && _assets.ContainsKey(entry.Address);
        }

        public ResourceLoadResult<object> Load(ResourceLoadContext context)
        {
            if (context == null || context.Entry == null)
            {
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    ProviderId,
                    "Memory resource load context is missing."));
            }

            if (!_assets.TryGetValue(context.Entry.Address, out object value))
            {
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.NotFound,
                    context.Key,
                    ProviderId,
                    "Memory resource address is not registered.",
                    context.Entry.Address));
            }

            LoadCount++;
            return ResourceLoadResult<object>.Loaded(value);
        }

        public IResourceOperation<object> LoadAsync(ResourceLoadContext context, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ImmediateResourceOperation<object>(ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.Cancelled,
                    context != null ? context.Key : default,
                    ProviderId,
                    "Memory resource operation was cancelled.")));
            }

            return new ImmediateResourceOperation<object>(Load(context));
        }

        public void Release(ResourceReleaseContext context)
        {
            ReleaseCount++;
        }
    }
}
