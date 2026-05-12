using System.Threading;
using UnityEngine;

namespace MxFramework.Resources.Unity
{
    public sealed class ResourcesProvider : IResourceProvider
    {
        public const string Id = "resources";

        public string ProviderId => Id;
        public int ReleaseCount { get; private set; }

        public bool CanLoad(ResourceCatalogEntry entry)
        {
            return entry != null
                && entry.ProviderId == ProviderId
                && !string.IsNullOrWhiteSpace(entry.Address);
        }

        public ResourceLoadResult<object> Load(ResourceLoadContext context)
        {
            if (context == null || context.Entry == null)
            {
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    ProviderId,
                    "Resources load context is missing."));
            }

            System.Type assetType = UnityResourceTypeResolver.Resolve(context.Entry.TypeId);
            UnityEngine.Object asset = UnityEngine.Resources.Load(context.Entry.Address, assetType);
            if (asset == null)
            {
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.NotFound,
                    context.Key,
                    ProviderId,
                    "Unity Resources asset was not found.",
                    context.Entry.Address));
            }

            return ResourceLoadResult<object>.Loaded(asset);
        }

        public IResourceOperation<object> LoadAsync(ResourceLoadContext context, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ImmediateResourceOperation<object>(ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.Cancelled,
                    context != null ? context.Key : default,
                    ProviderId,
                    "Resources operation was cancelled.")));
            }

            if (context == null || context.Entry == null)
            {
                return new ImmediateResourceOperation<object>(ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    ProviderId,
                    "Resources load context is missing.")));
            }

            System.Type assetType = UnityResourceTypeResolver.Resolve(context.Entry.TypeId);
            ResourceRequest request = UnityEngine.Resources.LoadAsync(context.Entry.Address, assetType);
            return new ResourcesLoadOperation(context.Key, context.Entry, request);
        }

        public void Release(ResourceReleaseContext context)
        {
            ReleaseCount++;
            if (context == null)
                return;

            if (context.Value is UnityEngine.Object asset && asset != null && !(asset is GameObject))
                UnityEngine.Resources.UnloadAsset(asset);
        }
    }
}
