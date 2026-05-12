using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace MxFramework.Resources.Unity
{
    public sealed class AssetBundleProvider : IResourceProvider
    {
        public const string Id = "assetBundle";

        private readonly string _bundleRootPath;
        private readonly IAssetBundleDependencyProvider _dependencyProvider;
        private readonly Dictionary<string, BundleRecord> _bundles = new Dictionary<string, BundleRecord>(StringComparer.Ordinal);

        public AssetBundleProvider(string bundleRootPath, IAssetBundleDependencyProvider dependencyProvider = null)
        {
            _bundleRootPath = bundleRootPath ?? string.Empty;
            _dependencyProvider = dependencyProvider ?? EmptyAssetBundleDependencyProvider.Instance;
        }

        public string ProviderId => Id;
        public int LoadedBundleCount => _bundles.Count;

        public bool CanLoad(ResourceCatalogEntry entry)
        {
            return entry != null
                && entry.ProviderId == ProviderId
                && TryParseAddress(entry.Address, out _, out _);
        }

        public int GetBundleRefCount(string bundleName)
        {
            return _bundles.TryGetValue(bundleName ?? string.Empty, out BundleRecord record)
                ? record.RefCount
                : 0;
        }

        public ResourceLoadResult<object> Load(ResourceLoadContext context)
        {
            if (context == null || context.Entry == null)
            {
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    ProviderId,
                    "AssetBundle load context is missing."));
            }

            if (!TryParseAddress(context.Entry.Address, out string bundleName, out string assetName))
            {
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    context.Key,
                    ProviderId,
                    "AssetBundle address must use 'bundleName|assetName'.",
                    context.Entry.Address));
            }

            var loadedBundles = new List<string>();
            IReadOnlyList<string> dependencies = _dependencyProvider.GetDependencies(bundleName);
            for (int i = 0; i < dependencies.Count; i++)
            {
                ResourceError dependencyError = LoadBundle(dependencies[i], loadedBundles);
                if (!dependencyError.IsNone)
                {
                    ReleaseBundles(loadedBundles);
                    return ResourceLoadResult<object>.Failed(new ResourceError(
                        ResourceErrorCode.DependencyInvalid,
                        context.Key,
                        ProviderId,
                        "AssetBundle dependency failed: " + dependencyError.Message,
                        context.Entry.Address));
                }
            }

            ResourceError bundleError = LoadBundle(bundleName, loadedBundles);
            if (!bundleError.IsNone)
            {
                ReleaseBundles(loadedBundles);
                return ResourceLoadResult<object>.Failed(bundleError);
            }

            System.Type assetType = UnityResourceTypeResolver.Resolve(context.Entry.TypeId);
            UnityEngine.Object asset = _bundles[bundleName].Bundle.LoadAsset(assetName, assetType);
            if (asset == null)
            {
                ReleaseBundles(loadedBundles);
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.NotFound,
                    context.Key,
                    ProviderId,
                    "AssetBundle asset was not found.",
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
                    "AssetBundle operation was cancelled.")));
            }

            return new ImmediateResourceOperation<object>(Load(context));
        }

        public void Release(ResourceReleaseContext context)
        {
            if (context == null || context.Entry == null)
                return;

            if (!TryParseAddress(context.Entry.Address, out string bundleName, out _))
                return;

            ReleaseBundle(bundleName);
            IReadOnlyList<string> dependencies = _dependencyProvider.GetDependencies(bundleName);
            for (int i = dependencies.Count - 1; i >= 0; i--)
                ReleaseBundle(dependencies[i]);
        }

        public static bool TryParseAddress(string address, out string bundleName, out string assetName)
        {
            bundleName = string.Empty;
            assetName = string.Empty;
            if (string.IsNullOrWhiteSpace(address))
                return false;

            int separator = address.IndexOf('|');
            if (separator <= 0 || separator >= address.Length - 1)
                return false;

            bundleName = address.Substring(0, separator);
            assetName = address.Substring(separator + 1);
            return !string.IsNullOrWhiteSpace(bundleName)
                && !string.IsNullOrWhiteSpace(assetName)
                && bundleName.IndexOf("..", StringComparison.Ordinal) < 0
                && assetName.IndexOf("..", StringComparison.Ordinal) < 0;
        }

        private ResourceError LoadBundle(string bundleName, List<string> loadedBundles)
        {
            if (string.IsNullOrWhiteSpace(bundleName))
            {
                return new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    ProviderId,
                    "AssetBundle name is missing.");
            }

            if (_bundles.TryGetValue(bundleName, out BundleRecord existing))
            {
                existing.RefCount++;
                loadedBundles.Add(bundleName);
                return ResourceError.None;
            }

            string fullPath = Path.Combine(_bundleRootPath, bundleName);
            if (!File.Exists(fullPath))
            {
                return new ResourceError(
                    ResourceErrorCode.NotFound,
                    default,
                    ProviderId,
                    "AssetBundle file was not found.",
                    fullPath);
            }

            AssetBundle bundle = AssetBundle.LoadFromFile(fullPath);
            if (bundle == null)
            {
                return new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    ProviderId,
                    "AssetBundle failed to load.",
                    fullPath);
            }

            _bundles.Add(bundleName, new BundleRecord(bundle));
            loadedBundles.Add(bundleName);
            return ResourceError.None;
        }

        private void ReleaseBundles(List<string> bundleNames)
        {
            for (int i = bundleNames.Count - 1; i >= 0; i--)
                ReleaseBundle(bundleNames[i]);
        }

        private void ReleaseBundle(string bundleName)
        {
            if (!_bundles.TryGetValue(bundleName ?? string.Empty, out BundleRecord record))
                return;

            record.RefCount--;
            if (record.RefCount > 0)
                return;

            _bundles.Remove(bundleName);
            record.Bundle.Unload(true);
        }

        private sealed class BundleRecord
        {
            public BundleRecord(AssetBundle bundle)
            {
                Bundle = bundle;
                RefCount = 1;
            }

            public AssetBundle Bundle { get; }
            public int RefCount { get; set; }
        }
    }
}
