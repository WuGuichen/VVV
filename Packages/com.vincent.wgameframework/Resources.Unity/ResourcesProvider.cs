using System.Threading;
using UnityEngine;

namespace MxFramework.Resources.Unity
{
    public sealed class ResourcesProvider : IResourceProvider
    {
        public const string Id = "resources";
        public const string SubAssetNameProviderDataKey = "subAssetName";

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
            if (TryGetProviderData(context.Entry, SubAssetNameProviderDataKey, out string subAssetName))
                return LoadSubAsset(context, assetType, subAssetName);

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
            if (TryGetProviderData(context.Entry, SubAssetNameProviderDataKey, out _))
                return new ImmediateResourceOperation<object>(Load(context));

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

        private ResourceLoadResult<object> LoadSubAsset(ResourceLoadContext context, System.Type assetType, string subAssetName)
        {
            UnityEngine.Object[] assets = UnityEngine.Resources.LoadAll(context.Entry.Address, assetType);
            for (int i = 0; i < assets.Length; i++)
            {
                UnityEngine.Object asset = assets[i];
                if (asset != null && string.Equals(asset.name, subAssetName, System.StringComparison.Ordinal))
                    return ResourceLoadResult<object>.Loaded(asset);
            }

            return ResourceLoadResult<object>.Failed(new ResourceError(
                ResourceErrorCode.NotFound,
                context.Key,
                ProviderId,
                "Unity Resources sub-asset was not found.",
                context.Entry.Address + "#" + subAssetName));
        }

        private static bool TryGetProviderData(ResourceCatalogEntry entry, string key, out string value)
        {
            value = string.Empty;
            if (entry == null
                || entry.ProviderData == null
                || string.IsNullOrWhiteSpace(key)
                || !entry.ProviderData.TryGetValue(key, out value))
            {
                value = string.Empty;
                return false;
            }

            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
