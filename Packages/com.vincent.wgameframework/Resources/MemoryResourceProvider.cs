using System.Collections.Generic;
using System.Threading;

namespace MxFramework.Resources
{
    public sealed class MemoryResourceProvider : IResourceProvider, System.IDisposable
    {
        public const string Id = "memory";

        private readonly Dictionary<string, object> _assets = new Dictionary<string, object>(System.StringComparer.Ordinal);
        private readonly HashSet<object> _ownedAssets = new HashSet<object>();
        private readonly System.Action<object> _releaseOwnedAsset;
        private bool _disposed;

        /// <remarks>LowFreqAlloc. Only RegisterOwned assets are released on disposal.</remarks>
        public MemoryResourceProvider() : this(null) { }

        /// <remarks>LowFreqAlloc. The callback releases private assets registered through RegisterOwned.</remarks>
        public MemoryResourceProvider(System.Action<object> releaseOwnedAsset)
        {
            _releaseOwnedAsset = releaseOwnedAsset;
        }

        /// <summary>Registers a private template whose lifetime belongs to this provider.</summary>
        /// <remarks>LowFreqAlloc. Imported/shared assets must use Register instead.</remarks>
        public MemoryResourceProvider RegisterOwned(string address, object value)
        {
            if (_releaseOwnedAsset == null)
                throw new System.InvalidOperationException("Owned memory assets require a release callback.");
            Register(address, value);
            if (value != null) _ownedAssets.Add(value);
            return this;
        }

        public string ProviderId => Id;
        public int LoadCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public MemoryResourceProvider Register(string address, object value)
        {
            if (_disposed) throw new System.ObjectDisposedException(nameof(MemoryResourceProvider));
            _assets[address ?? string.Empty] = value;
            return this;
        }

        /// <remarks>LowFreqAlloc. Releases each private asset once, even after address replacement.</remarks>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var errors = new List<System.Exception>();
            foreach (object value in _ownedAssets)
            {
                try { _releaseOwnedAsset(value); }
                catch (System.Exception exception) { errors.Add(exception); }
            }
            _ownedAssets.Clear();
            _assets.Clear();
            if (errors.Count > 0) throw new System.AggregateException("Owned memory asset cleanup failed.", errors);
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
