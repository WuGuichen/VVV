using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;

namespace MxFramework.Resources.Unity
{
    /// <remarks>LowFreqAlloc. File hashing and native bundle access require the Unity main thread.</remarks>
    public sealed class AssetBundleProvider : IResourceProvider
    {
        public const string Id = "assetBundle";
        public const string ExternalId = "externalAssetBundle";

        // Only successful loads performed here establish provenance. Unity's bundle name is not source evidence.
        private static readonly Dictionary<AssetBundle, SharedBundleRecord> SharedBundles = new Dictionary<AssetBundle, SharedBundleRecord>();
        private static readonly StringComparer SourcePathComparer = Path.DirectorySeparatorChar == '\\'
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        private readonly string _bundleRootPath;
        private readonly string _providerId;
        private readonly IAssetBundleDependencyProvider _dependencyProvider;
        private readonly Dictionary<string, BundleRecord> _bundles = new Dictionary<string, BundleRecord>(StringComparer.Ordinal);

        public AssetBundleProvider(string bundleRootPath, IAssetBundleDependencyProvider dependencyProvider = null)
            : this(bundleRootPath, Id, dependencyProvider)
        {
        }

        public AssetBundleProvider(string bundleRootPath, string providerId, IAssetBundleDependencyProvider dependencyProvider = null)
        {
            _bundleRootPath = Path.GetFullPath(string.IsNullOrEmpty(bundleRootPath) ? "." : bundleRootPath);
            _providerId = string.IsNullOrWhiteSpace(providerId) ? Id : providerId;
            _dependencyProvider = dependencyProvider ?? EmptyAssetBundleDependencyProvider.Instance;
        }

        public string ProviderId => _providerId;
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
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    bundleError.Code, context.Key, ProviderId, bundleError.Message, bundleError.Address));
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

            string fullPath = Path.Combine(_bundleRootPath, bundleName);
            try
            {
                fullPath = Path.GetFullPath(fullPath);
                if (!File.Exists(fullPath))
                {
                    return new ResourceError(ResourceErrorCode.NotFound, default, ProviderId,
                        "AssetBundle file was not found.", fullPath);
                }

                // Keep the source open without write sharing through the native load and provenance registration.
                using (var source = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    string sourceHash = ComputeSourceHash(source);
                    if (_bundles.TryGetValue(bundleName, out BundleRecord existing))
                    {
                        if (!MatchesSource(existing.Shared, fullPath, sourceHash) || !IsBundleLoaded(existing.Bundle))
                            return SourceIdentityError(fullPath, sourceHash, existing.Shared);

                        existing.RefCount++;
                        loadedBundles.Add(bundleName);
                        return ResourceError.None;
                    }

                    ResourceError reuseError = FindLoadedBundle(bundleName, fullPath, sourceHash, out SharedBundleRecord shared);
                    if (!reuseError.IsNone)
                        return reuseError;

                    if (shared == null)
                    {
                        AssetBundle bundle = AssetBundle.LoadFromFile(fullPath);
                        if (bundle == null)
                        {
                            return new ResourceError(ResourceErrorCode.ProviderFailed, default, ProviderId,
                                "AssetBundle failed to load from the requested source.", fullPath);
                        }

                        try
                        {
                            source.Position = 0;
                            if (!string.Equals(sourceHash, ComputeSourceHash(source), StringComparison.Ordinal))
                            {
                                UnloadBundleIfAlive(bundle);
                                return SourceIdentityError(fullPath, sourceHash, null);
                            }
                        }
                        catch
                        {
                            UnloadBundleIfAlive(bundle);
                            throw;
                        }

                        shared = new SharedBundleRecord(bundle, fullPath, sourceHash);
                        SharedBundles[bundle] = shared;
                    }

                    shared.LeaseCount++;
                    _bundles.Add(bundleName, new BundleRecord(shared));
                    loadedBundles.Add(bundleName);
                    return ResourceError.None;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException)
            {
                return new ResourceError(ResourceErrorCode.ProviderFailed, default, ProviderId,
                    "AssetBundle source identity could not be verified: " + exception.Message, fullPath);
            }
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
            SharedBundleRecord shared = record.Shared;
            shared.LeaseCount--;
            if (shared.LeaseCount > 0)
                return;

            if (SharedBundles.TryGetValue(shared.Bundle, out SharedBundleRecord registered)
                && ReferenceEquals(registered, shared))
                SharedBundles.Remove(shared.Bundle);
            if (IsBundleLoaded(shared.Bundle))
                UnloadBundleIfAlive(shared.Bundle);
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

        private ResourceError FindLoadedBundle(string bundleName, string fullPath, string sourceHash, out SharedBundleRecord shared)
        {
            string normalizedBundleName = NormalizeBundleName(bundleName);
            string fileName = Path.GetFileName(bundleName);
            ResourceError conflict = ResourceError.None;
            foreach (AssetBundle loaded in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (loaded == null)
                    continue;

                SharedBundles.TryGetValue(loaded, out SharedBundleRecord known);
                if (known != null && known.Bundle != loaded)
                    known = null;
                if (known != null && MatchesSource(known, fullPath, sourceHash))
                {
                    shared = known;
                    return ResourceError.None;
                }

                string loadedName = NormalizeBundleName(loaded.name);
                if (string.Equals(loadedName, normalizedBundleName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(loaded.name, fileName, StringComparison.OrdinalIgnoreCase)
                    || (known != null && (SourcePathComparer.Equals(known.SourcePath, fullPath)
                        || string.Equals(Path.GetFileName(known.SourcePath), fileName, StringComparison.OrdinalIgnoreCase))))
                {
                    conflict = SourceIdentityError(fullPath, sourceHash, known);
                }
            }

            shared = null;
            return conflict;
        }

        private ResourceError SourceIdentityError(string fullPath, string sourceHash, SharedBundleRecord loaded)
        {
            string loadedSource = loaded == null ? "unknown or unregistered"
                : "'" + loaded.SourcePath + "' sha256:" + loaded.SourceHash;
            return new ResourceError(ResourceErrorCode.ProviderFailed, default, ProviderId,
                "AssetBundle source identity conflict or invalidated native bundle. Requested '" + fullPath
                + "' sha256:" + sourceHash + "; loaded source: " + loadedSource + ".", fullPath);
        }

        private static bool MatchesSource(SharedBundleRecord record, string fullPath, string sourceHash)
        {
            return SourcePathComparer.Equals(record.SourcePath, fullPath)
                && string.Equals(record.SourceHash, sourceHash, StringComparison.Ordinal);
        }

        private static string ComputeSourceHash(Stream stream)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static bool IsBundleLoaded(AssetBundle bundle)
        {
            if (bundle == null)
                return false;
            foreach (AssetBundle loaded in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (loaded == bundle)
                    return true;
            }
            return false;
        }

        private static string NormalizeBundleName(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/');
        }

        private sealed class BundleRecord
        {
            public BundleRecord(SharedBundleRecord shared)
            {
                Shared = shared;
                RefCount = 1;
            }

            public AssetBundle Bundle => Shared.Bundle;
            public SharedBundleRecord Shared { get; }
            public int RefCount { get; set; }
        }

        private sealed class SharedBundleRecord
        {
            public SharedBundleRecord(AssetBundle bundle, string sourcePath, string sourceHash)
            {
                Bundle = bundle;
                SourcePath = sourcePath;
                SourceHash = sourceHash;
            }

            public AssetBundle Bundle { get; }
            public string SourcePath { get; }
            public string SourceHash { get; }
            public int LeaseCount { get; set; }
        }
    }
}
