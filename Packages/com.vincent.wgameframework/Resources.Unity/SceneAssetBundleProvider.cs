using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace MxFramework.Resources.Unity
{
    public sealed class SceneAssetBundleProvider : IResourceProvider
    {
        public const string Id = "sceneAssetBundle";

        private readonly string _bundleRootPath;
        private readonly IAssetBundleDependencyProvider _dependencyProvider;
        private readonly Dictionary<string, BundleRecord> _bundles = new Dictionary<string, BundleRecord>(StringComparer.Ordinal);

        public SceneAssetBundleProvider(string bundleRootPath, IAssetBundleDependencyProvider dependencyProvider = null)
        {
            _bundleRootPath = bundleRootPath ?? string.Empty;
            _dependencyProvider = dependencyProvider ?? EmptyAssetBundleDependencyProvider.Instance;
        }

        public string ProviderId => Id;
        public int LoadedBundleCount => _bundles.Count;

        public bool CanLoad(ResourceCatalogEntry entry)
        {
            return entry != null
                && string.Equals(entry.ProviderId, Id, StringComparison.Ordinal)
                && TryParseAddress(entry.Address, out _, out _);
        }

        public ResourceLoadResult<object> Load(ResourceLoadContext context)
        {
            return ResourceLoadResult<object>.Failed(new ResourceError(
                ResourceErrorCode.ProviderFailed,
                context != null ? context.Key : default,
                Id,
                "Scene resources must be loaded through ResourceSceneFlowDriver.",
                context != null && context.Entry != null ? context.Entry.Address : string.Empty));
        }

        public IResourceOperation<object> LoadAsync(ResourceLoadContext context, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ImmediateResourceOperation<object>(ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.Cancelled,
                    context != null ? context.Key : default,
                    Id,
                    "Scene resource operation was cancelled.")));
            }

            return new ImmediateResourceOperation<object>(Load(context));
        }

        public void Release(ResourceReleaseContext context)
        {
            if (context != null && context.Value is SceneAssetBundleHandle handle)
                Release(handle);
        }

        public ResourceLoadResult<SceneAssetBundleHandle> LoadSceneBundle(ResourceKey key, ResourceCatalogEntry entry)
        {
            if (entry == null)
            {
                return ResourceLoadResult<SceneAssetBundleHandle>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    key,
                    Id,
                    "Scene AssetBundle catalog entry is missing."));
            }

            if (!TryParseAddress(entry.Address, out string bundleName, out string scenePath))
            {
                return ResourceLoadResult<SceneAssetBundleHandle>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    key,
                    Id,
                    "Scene AssetBundle address must use 'bundleName|scenePath'.",
                    entry.Address));
            }

            var loadedBundles = new List<string>();
            IReadOnlyList<string> dependencies = _dependencyProvider.GetDependencies(bundleName);
            for (int i = 0; i < dependencies.Count; i++)
            {
                ResourceError dependencyError = LoadBundle(dependencies[i], loadedBundles);
                if (!dependencyError.IsNone)
                {
                    ReleaseBundles(loadedBundles);
                    return ResourceLoadResult<SceneAssetBundleHandle>.Failed(new ResourceError(
                        ResourceErrorCode.DependencyInvalid,
                        key,
                        Id,
                        "Scene AssetBundle dependency failed: " + dependencyError.Message,
                        entry.Address));
                }
            }

            ResourceError bundleError = LoadBundle(bundleName, loadedBundles);
            if (!bundleError.IsNone)
            {
                ReleaseBundles(loadedBundles);
                return ResourceLoadResult<SceneAssetBundleHandle>.Failed(bundleError);
            }

            AssetBundle bundle = _bundles[bundleName].Bundle;
            if (!bundle.isStreamedSceneAssetBundle)
            {
                ReleaseBundles(loadedBundles);
                return ResourceLoadResult<SceneAssetBundleHandle>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    key,
                    Id,
                    "AssetBundle is not a streamed Scene AssetBundle.",
                    entry.Address));
            }

            if (!ContainsScenePath(bundle, scenePath))
            {
                ReleaseBundles(loadedBundles);
                return ResourceLoadResult<SceneAssetBundleHandle>.Failed(new ResourceError(
                    ResourceErrorCode.NotFound,
                    key,
                    Id,
                    "Scene path was not found in the Scene AssetBundle.",
                    entry.Address));
            }

            return ResourceLoadResult<SceneAssetBundleHandle>.Loaded(new SceneAssetBundleHandle(
                key,
                entry,
                bundleName,
                scenePath,
                loadedBundles));
        }

        public void Release(SceneAssetBundleHandle handle)
        {
            if (handle == null)
                return;

            IReadOnlyList<string> bundleNames = handle.LoadedBundleNames;
            for (int i = bundleNames.Count - 1; i >= 0; i--)
                ReleaseBundle(bundleNames[i]);
        }

        public static bool TryParseAddress(string address, out string bundleName, out string scenePath)
        {
            bundleName = string.Empty;
            scenePath = string.Empty;
            if (string.IsNullOrWhiteSpace(address))
                return false;

            int separator = address.IndexOf('|');
            if (separator <= 0 || separator >= address.Length - 1)
                return false;

            bundleName = address.Substring(0, separator);
            scenePath = address.Substring(separator + 1).Replace('\\', '/');
            return !string.IsNullOrWhiteSpace(bundleName)
                && !string.IsNullOrWhiteSpace(scenePath)
                && scenePath.StartsWith("Assets/", StringComparison.Ordinal)
                && scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                && bundleName.IndexOf("..", StringComparison.Ordinal) < 0
                && scenePath.IndexOf("..", StringComparison.Ordinal) < 0;
        }

        private static bool ContainsScenePath(AssetBundle bundle, string scenePath)
        {
            string[] scenePaths = bundle.GetAllScenePaths();
            for (int i = 0; i < scenePaths.Length; i++)
            {
                if (string.Equals(scenePaths[i], scenePath, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private ResourceError LoadBundle(string bundleName, List<string> loadedBundles)
        {
            if (string.IsNullOrWhiteSpace(bundleName))
            {
                return new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    Id,
                    "Scene AssetBundle name is missing.");
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
                    Id,
                    "Scene AssetBundle file was not found.",
                    fullPath);
            }

            AssetBundle bundle = AssetBundle.LoadFromFile(fullPath);
            if (bundle == null)
            {
                return new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    Id,
                    "Scene AssetBundle failed to load.",
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
            UnloadBundleIfAlive(record.Bundle);
        }

        private static void UnloadBundleIfAlive(AssetBundle bundle)
        {
            if (bundle == null)
                return;

            try
            {
                bundle.Unload(true);
            }
            catch (NullReferenceException)
            {
                // Unity may invalidate AssetBundle native state during Player shutdown before managed release runs.
            }
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

    public sealed class SceneAssetBundleHandle
    {
        private readonly List<string> _loadedBundleNames;

        public SceneAssetBundleHandle(
            ResourceKey key,
            ResourceCatalogEntry entry,
            string bundleName,
            string scenePath,
            IEnumerable<string> loadedBundleNames)
        {
            Key = key;
            Entry = entry;
            BundleName = bundleName ?? string.Empty;
            ScenePath = scenePath ?? string.Empty;
            _loadedBundleNames = loadedBundleNames != null
                ? new List<string>(loadedBundleNames)
                : new List<string>();
        }

        public ResourceKey Key { get; }
        public ResourceCatalogEntry Entry { get; }
        public string BundleName { get; }
        public string ScenePath { get; }
        public IReadOnlyList<string> LoadedBundleNames => _loadedBundleNames;
    }
}
