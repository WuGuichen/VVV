using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using MxFramework.Resources;
using UnityEngine;
using UnityEngine.Networking;

namespace MxFramework.Resources.Unity
{
    public sealed class RemoteBundleProvider : IResourceProvider
    {
        public const string Id = "remoteBundle";

        private readonly string _cacheRootPath;
        private readonly IAssetBundleDependencyProvider _dependencyProvider;
        private readonly Dictionary<string, BundleRecord> _bundles = new Dictionary<string, BundleRecord>(StringComparer.Ordinal);

        public RemoteBundleProvider(string cacheRootPath, IAssetBundleDependencyProvider dependencyProvider = null)
        {
            _cacheRootPath = cacheRootPath ?? string.Empty;
            _dependencyProvider = dependencyProvider ?? EmptyAssetBundleDependencyProvider.Instance;
        }

        public string ProviderId => Id;
        public int LoadedBundleCount => _bundles.Count;
        public int FetchCount { get; private set; }

        public bool CanLoad(ResourceCatalogEntry entry)
        {
            return entry != null
                && entry.ProviderId == ProviderId
                && AssetBundleProvider.TryParseAddress(entry.Address, out _, out _)
                && TryGetBundleUrl(entry, GetDeclaredBundleName(entry), out _);
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
                    "RemoteBundle load context is missing."));
            }

            if (!AssetBundleProvider.TryParseAddress(context.Entry.Address, out string bundleName, out string assetName))
            {
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    context.Key,
                    ProviderId,
                    "RemoteBundle address must use 'bundleName|assetName'.",
                    context.Entry.Address));
            }

            var loadedBundles = new List<string>();
            IReadOnlyList<string> dependencies = _dependencyProvider.GetDependencies(bundleName);
            for (int i = 0; i < dependencies.Count; i++)
            {
                ResourceError dependencyError = LoadBundle(dependencies[i], context.Entry, loadedBundles);
                if (!dependencyError.IsNone)
                {
                    ReleaseBundles(loadedBundles);
                    return ResourceLoadResult<object>.Failed(new ResourceError(
                        ResourceErrorCode.DependencyInvalid,
                        context.Key,
                        ProviderId,
                        "RemoteBundle dependency failed: " + dependencyError.Message,
                        context.Entry.Address));
                }
            }

            ResourceError bundleError = LoadBundle(bundleName, context.Entry, loadedBundles);
            if (!bundleError.IsNone)
            {
                ReleaseBundles(loadedBundles);
                return ResourceLoadResult<object>.Failed(bundleError);
            }

            Type assetType = UnityResourceTypeResolver.Resolve(context.Entry.TypeId);
            UnityEngine.Object asset = _bundles[bundleName].Bundle.LoadAsset(assetName, assetType);
            if (asset == null)
            {
                ReleaseBundles(loadedBundles);
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.NotFound,
                    context.Key,
                    ProviderId,
                    "RemoteBundle asset was not found.",
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
                    "RemoteBundle operation was cancelled.")));
            }

            return new ImmediateResourceOperation<object>(Load(context));
        }

        public void Release(ResourceReleaseContext context)
        {
            if (context == null || context.Entry == null)
                return;

            if (!AssetBundleProvider.TryParseAddress(context.Entry.Address, out string bundleName, out _))
                return;

            ReleaseBundle(bundleName);
            IReadOnlyList<string> dependencies = _dependencyProvider.GetDependencies(bundleName);
            for (int i = dependencies.Count - 1; i >= 0; i--)
                ReleaseBundle(dependencies[i]);
        }

        private ResourceError LoadBundle(string bundleName, ResourceCatalogEntry entry, List<string> loadedBundles)
        {
            if (string.IsNullOrWhiteSpace(bundleName))
            {
                return new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    ProviderId,
                    "RemoteBundle name is missing.");
            }

            if (_bundles.TryGetValue(bundleName, out BundleRecord existing))
            {
                existing.RefCount++;
                loadedBundles.Add(bundleName);
                return ResourceError.None;
            }

            ResourceError cacheError = EnsureCachedBundle(bundleName, entry, out string cachedPath);
            if (!cacheError.IsNone)
                return cacheError;

            AssetBundle bundle = AssetBundle.LoadFromFile(cachedPath);
            if (bundle == null)
            {
                return new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    ProviderId,
                    "RemoteBundle failed to load cached AssetBundle.",
                    cachedPath);
            }

            _bundles.Add(bundleName, new BundleRecord(bundle));
            loadedBundles.Add(bundleName);
            return ResourceError.None;
        }

        private ResourceError EnsureCachedBundle(string bundleName, ResourceCatalogEntry entry, out string cachedPath)
        {
            cachedPath = GetCachePath(bundleName, entry);
            string expectedHash = GetExpectedHash(bundleName, entry);
            if (File.Exists(cachedPath) && HashMatches(cachedPath, expectedHash))
                return ResourceError.None;

            if (File.Exists(cachedPath))
                File.Delete(cachedPath);

            if (!TryGetBundleUrl(entry, bundleName, out string url))
            {
                return new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    ProviderId,
                    "RemoteBundle url is missing.",
                    bundleName);
            }

            string cacheDirectory = Path.GetDirectoryName(cachedPath);
            if (!string.IsNullOrWhiteSpace(cacheDirectory))
                Directory.CreateDirectory(cacheDirectory);
            ResourceError fetchError = FetchBundle(url, cachedPath);
            if (!fetchError.IsNone)
                return fetchError;

            if (!HashMatches(cachedPath, expectedHash))
            {
                File.Delete(cachedPath);
                return new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    ProviderId,
                    "RemoteBundle hash mismatch.",
                    url);
            }

            return ResourceError.None;
        }

        private ResourceError FetchBundle(string url, string cachedPath)
        {
            FetchCount++;

            if (TryGetLocalPath(url, out string localPath))
            {
                if (!File.Exists(localPath))
                {
                    return new ResourceError(
                        ResourceErrorCode.NotFound,
                        default,
                        ProviderId,
                        "RemoteBundle source file was not found.",
                        localPath);
                }

                File.Copy(localPath, cachedPath, true);
                return ResourceError.None;
            }

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    return new ResourceError(
                        ResourceErrorCode.ProviderFailed,
                        default,
                        ProviderId,
                        "RemoteBundle download failed: " + request.error,
                        url);
                }

                File.WriteAllBytes(cachedPath, request.downloadHandler.data);
                return ResourceError.None;
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
            record.Bundle.Unload(true);
        }

        private string GetCachePath(string bundleName, ResourceCatalogEntry entry)
        {
            string cacheKey = GetProviderData(entry, "cacheKey");
            if (!IsDeclaredBundle(entry, bundleName))
                cacheKey = GetProviderData(entry, "cacheKey." + bundleName);

            if (string.IsNullOrWhiteSpace(cacheKey))
                cacheKey = bundleName;

            return Path.Combine(_cacheRootPath, SanitizeCacheKey(cacheKey));
        }

        private static string GetExpectedHash(string bundleName, ResourceCatalogEntry entry)
        {
            string hash = GetProviderData(entry, "hash." + bundleName);
            if (string.IsNullOrWhiteSpace(hash) && IsDeclaredBundle(entry, bundleName))
                hash = entry.Hash;

            return NormalizeSha256(hash);
        }

        private static bool TryGetBundleUrl(ResourceCatalogEntry entry, string bundleName, out string url)
        {
            url = GetProviderData(entry, "url." + bundleName);
            if (string.IsNullOrWhiteSpace(url))
                url = GetProviderData(entry, "dependency." + bundleName + ".url");
            if (string.IsNullOrWhiteSpace(url) && IsDeclaredBundle(entry, bundleName))
                url = GetProviderData(entry, "url");
            if (string.IsNullOrWhiteSpace(url))
            {
                string baseUrl = GetProviderData(entry, "baseUrl");
                if (!string.IsNullOrWhiteSpace(baseUrl))
                    url = CombineUrl(baseUrl, bundleName);
            }

            return !string.IsNullOrWhiteSpace(url);
        }

        private static string GetDeclaredBundleName(ResourceCatalogEntry entry)
        {
            string bundleName = GetProviderData(entry, "bundleName");
            if (!string.IsNullOrWhiteSpace(bundleName))
                return bundleName;

            return AssetBundleProvider.TryParseAddress(entry != null ? entry.Address : string.Empty, out bundleName, out _)
                ? bundleName
                : string.Empty;
        }

        private static bool IsDeclaredBundle(ResourceCatalogEntry entry, string bundleName)
        {
            return string.Equals(GetDeclaredBundleName(entry), bundleName ?? string.Empty, StringComparison.Ordinal);
        }

        private static string GetProviderData(ResourceCatalogEntry entry, string key)
        {
            if (entry == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            return entry.ProviderData.TryGetValue(key, out string value)
                ? value
                : string.Empty;
        }

        private static bool TryGetLocalPath(string url, out string localPath)
        {
            localPath = string.Empty;
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && uri.IsFile)
            {
                localPath = uri.LocalPath;
                return true;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out _) && File.Exists(url))
            {
                localPath = url;
                return true;
            }

            return false;
        }

        private static bool HashMatches(string path, string expectedSha256)
        {
            if (string.IsNullOrWhiteSpace(expectedSha256))
                return true;
            if (!File.Exists(path))
                return false;

            return string.Equals(ComputeSha256(path), expectedSha256, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string NormalizeSha256(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return string.Empty;

            const string Prefix = "sha256:";
            return hash.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                ? hash.Substring(Prefix.Length)
                : hash;
        }

        private static string SanitizeCacheKey(string cacheKey)
        {
            var chars = cacheKey.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool valid = (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '.'
                    || c == '_'
                    || c == '-';
                if (!valid)
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static string CombineUrl(string baseUrl, string bundleName)
        {
            if (baseUrl.EndsWith("/", StringComparison.Ordinal))
                return baseUrl + bundleName;

            return baseUrl + "/" + bundleName;
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
