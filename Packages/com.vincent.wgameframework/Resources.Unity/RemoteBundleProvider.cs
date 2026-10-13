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
    /// <remarks>LowFreqAlloc. Source identity and native bundle access require the Unity main thread.</remarks>
    public sealed class RemoteBundleProvider : IResourceProvider
    {
        public const string Id = "remoteBundle";

        private static readonly StringComparer SourcePathComparer = Path.DirectorySeparatorChar == '\\'
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        private readonly string _cacheRootPath;
        private const int VerifiedCacheIdentityCapacity = 256;
        // Managed metadata only: independent of native leases, bounded per provider, never shared globally.
        private readonly Dictionary<string, CachedBundleIdentity> _verifiedCacheIdentities
            = new Dictionary<string, CachedBundleIdentity>(SourcePathComparer);
        private readonly LinkedList<string> _verifiedCacheUsage = new LinkedList<string>();

        private readonly IAssetBundleDependencyProvider _dependencyProvider;
        private readonly Dictionary<string, BundleRecord> _bundles = new Dictionary<string, BundleRecord>(StringComparer.Ordinal);

        public RemoteBundleProvider(string cacheRootPath, IAssetBundleDependencyProvider dependencyProvider = null)
        {
            _cacheRootPath = Path.GetFullPath(string.IsNullOrEmpty(cacheRootPath) ? "." : cacheRootPath);
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
                        dependencyError.Address));
                }
            }

            ResourceError bundleError = LoadBundle(bundleName, context.Entry, loadedBundles);
            if (!bundleError.IsNone)
            {
                ReleaseBundles(loadedBundles);
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    bundleError.Code, context.Key, ProviderId, bundleError.Message, bundleError.Address));
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

            string cachedPath = string.Empty;
            try
            {
                cachedPath = Path.GetFullPath(GetCachePath(bundleName, entry));
                if (!TryGetBundleUrl(entry, bundleName, out string url))
                {
                    return new ResourceError(ResourceErrorCode.ProviderFailed, default, ProviderId,
                        "RemoteBundle url is missing.", cachedPath);
                }

                string sourceIdentity = GetSourceIdentity(url, out bool isFileSource);
                string expectedHash = GetExpectedHash(bundleName, entry);
                if (_bundles.TryGetValue(bundleName, out BundleRecord existing))
                {
                    bool sameSource = existing.IsFileSource == isFileSource
                        && (isFileSource ? SourcePathComparer.Equals(existing.SourceIdentity, sourceIdentity)
                            : string.Equals(existing.SourceIdentity, sourceIdentity, StringComparison.Ordinal));
                    if (!sameSource || !IsBundleLoaded(existing.Bundle))
                    {
                        return new ResourceError(ResourceErrorCode.ProviderFailed, default, ProviderId,
                            "RemoteBundle source identity conflict or invalidated native bundle. Requested source '"
                            + sourceIdentity + "'; loaded source '" + existing.SourceIdentity + "' sha256:"
                            + existing.ContentHash + ".", cachedPath);
                    }

                    if (!string.IsNullOrWhiteSpace(expectedHash)
                        && !string.Equals(existing.ContentHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        return new ResourceError(ResourceErrorCode.ProviderFailed, default, ProviderId,
                            "RemoteBundle hash mismatch. Requested sha256:" + expectedHash
                            + "; loaded sha256:" + existing.ContentHash + ".", cachedPath);
                    }

                    existing.RefCount++;
                    loadedBundles.Add(bundleName);
                    return ResourceError.None;
                }

                ResourceError cacheError = EnsureCachedBundle(cachedPath, url, sourceIdentity, isFileSource,
                    expectedHash, out string verifiedContentHash);
                if (!cacheError.IsNone)
                    return cacheError;

                // Pin the bytes used for this native load; cacheKey is a storage hint, not provenance.
                using (FileStream source = File.Open(cachedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    string contentHash = ComputeSha256(source);
                    if (!string.Equals(contentHash, verifiedContentHash, StringComparison.Ordinal))
                    {
                        ForgetVerifiedCacheIdentity(cachedPath);
                        return new ResourceError(ResourceErrorCode.ProviderFailed, default, ProviderId,
                            "RemoteBundle cached content changed before native load.", cachedPath);
                    }
                    if (!string.IsNullOrWhiteSpace(expectedHash)
                        && !string.Equals(contentHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        return new ResourceError(ResourceErrorCode.ProviderFailed, default, ProviderId,
                            "RemoteBundle hash mismatch before native load.", cachedPath);
                    }

                    AssetBundle bundle = AssetBundle.LoadFromFile(cachedPath);
                    if (bundle == null)
                    {
                        return new ResourceError(ResourceErrorCode.ProviderFailed, default, ProviderId,
                            "RemoteBundle failed to load cached AssetBundle.", cachedPath);
                    }

                    try
                    {
                        source.Position = 0;
                        if (!string.Equals(contentHash, ComputeSha256(source), StringComparison.Ordinal))
                        {
                            UnloadBundleIfAlive(bundle);
                            return new ResourceError(ResourceErrorCode.ProviderFailed, default, ProviderId,
                                "RemoteBundle cached content changed during native load.", cachedPath);
                        }
                    }
                    catch
                    {
                        UnloadBundleIfAlive(bundle);
                        throw;
                    }

                    RememberVerifiedCacheIdentity(cachedPath, sourceIdentity, isFileSource, contentHash);
                    _bundles.Add(bundleName, new BundleRecord(bundle, sourceIdentity, isFileSource, contentHash));
                    loadedBundles.Add(bundleName);
                    return ResourceError.None;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException)
            {
                return new ResourceError(ResourceErrorCode.ProviderFailed, default, ProviderId,
                    "RemoteBundle source identity could not be verified: " + exception.Message, cachedPath);
            }
        }

        private static string GetSourceIdentity(string url, out bool isFileSource)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                isFileSource = uri.IsFile;
                return uri.IsFile ? Path.GetFullPath(uri.LocalPath) : url;
            }

            isFileSource = true;
            return Path.GetFullPath(url);
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

        private ResourceError EnsureCachedBundle(string cachedPath, string url, string sourceIdentity,
            bool isFileSource, string expectedHash, out string verifiedContentHash)
        {
            verifiedContentHash = string.Empty;
            if (File.Exists(cachedPath))
            {
                string cachedHash = ComputeSha256(cachedPath);
                // A matching declared SHA-256 proves requested content, even without retained origin metadata.
                if (!string.IsNullOrWhiteSpace(expectedHash)
                    && string.Equals(cachedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    verifiedContentHash = cachedHash;
                    return ResourceError.None;
                }

                if (_verifiedCacheIdentities.TryGetValue(cachedPath, out CachedBundleIdentity known))
                {
                    if (!string.Equals(known.ContentHash, cachedHash, StringComparison.Ordinal))
                    {
                        // The file no longer has the bytes this provider established.
                        ForgetVerifiedCacheIdentity(cachedPath);
                    }
                    else
                    {
                        bool sameSource = known.IsFileSource == isFileSource
                            && (isFileSource ? SourcePathComparer.Equals(known.SourceIdentity, sourceIdentity)
                                : string.Equals(known.SourceIdentity, sourceIdentity, StringComparison.Ordinal));
                        if (sameSource && string.IsNullOrWhiteSpace(expectedHash))
                        {
                            verifiedContentHash = cachedHash;
                            _verifiedCacheUsage.Remove(known.UsageNode);
                            _verifiedCacheUsage.AddLast(known.UsageNode);
                            return ResourceError.None;
                        }
                        // A failed request for another source/digest must not erase valid old provenance.
                    }
                }
            }
            else
            {
                ForgetVerifiedCacheIdentity(cachedPath);
            }

            // Unknown, changed, or mismatched disk content is never accepted by cacheKey alone.
            string cacheDirectory = Path.GetDirectoryName(cachedPath);
            if (!string.IsNullOrWhiteSpace(cacheDirectory))
                Directory.CreateDirectory(cacheDirectory);
            // Read the requested source before replacing a cache file that may alias that source.
            string downloadPath = cachedPath + "." + Guid.NewGuid().ToString("N") + ".download";
            try
            {
                ResourceError fetchError = FetchBundle(url, downloadPath);
                if (!fetchError.IsNone)
                    return fetchError;

                string downloadHash = ComputeSha256(downloadPath);
                if (!string.IsNullOrWhiteSpace(expectedHash)
                    && !string.Equals(downloadHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    return new ResourceError(ResourceErrorCode.ProviderFailed, default, ProviderId,
                        "RemoteBundle hash mismatch. Requested sha256:" + expectedHash + ".", url);
                }

                File.Copy(downloadPath, cachedPath, true);
                verifiedContentHash = downloadHash;
                return ResourceError.None;
            }
            finally
            {
                if (File.Exists(downloadPath))
                    File.Delete(downloadPath);
            }
        }

        private void RememberVerifiedCacheIdentity(string cachedPath, string sourceIdentity, bool isFileSource, string contentHash)
        {
            ForgetVerifiedCacheIdentity(cachedPath);
            if (_verifiedCacheIdentities.Count >= VerifiedCacheIdentityCapacity)
                ForgetVerifiedCacheIdentity(_verifiedCacheUsage.First.Value);

            LinkedListNode<string> usageNode = _verifiedCacheUsage.AddLast(cachedPath);
            _verifiedCacheIdentities.Add(cachedPath, new CachedBundleIdentity(sourceIdentity, isFileSource, contentHash, usageNode));
        }

        private void ForgetVerifiedCacheIdentity(string cachedPath)
        {
            if (!_verifiedCacheIdentities.TryGetValue(cachedPath, out CachedBundleIdentity known))
                return;

            _verifiedCacheIdentities.Remove(cachedPath);
            _verifiedCacheUsage.Remove(known.UsageNode);
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
            if (IsBundleLoaded(record.Bundle))
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

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
                return ComputeSha256(stream);
        }

        private static string ComputeSha256(Stream stream)
        {
            using (SHA256 sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
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

        private sealed class CachedBundleIdentity
        {
            public CachedBundleIdentity(string sourceIdentity, bool isFileSource, string contentHash, LinkedListNode<string> usageNode)
            {
                SourceIdentity = sourceIdentity;
                IsFileSource = isFileSource;
                ContentHash = contentHash;
                UsageNode = usageNode;
            }

            public string SourceIdentity { get; }
            public bool IsFileSource { get; }
            public string ContentHash { get; }
            public LinkedListNode<string> UsageNode { get; }
        }

        private sealed class BundleRecord
        {
            public BundleRecord(AssetBundle bundle, string sourceIdentity, bool isFileSource, string contentHash)
            {
                Bundle = bundle;
                SourceIdentity = sourceIdentity;
                IsFileSource = isFileSource;
                ContentHash = contentHash;
                RefCount = 1;
            }

            public AssetBundle Bundle { get; }
            public string SourceIdentity { get; }
            public bool IsFileSource { get; }
            public string ContentHash { get; }
            public int RefCount { get; set; }
        }
    }
}
