using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Authoring
{
    public sealed class UnityProjectAssetAuthoringResourceProvider : IAuthoringResourceProvider
    {
        private static readonly object CacheGate = new object();
        private static readonly Dictionary<string, CachedAnimationAssetIndex> CachedIndexes = new Dictionary<string, CachedAnimationAssetIndex>(StringComparer.OrdinalIgnoreCase);

        public string ProviderId
        {
            get { return AuthoringResourceProviderIds.UnityProjectAssets; }
        }

        public AuthoringResourceProviderDescriptor Describe(AuthoringResourceProviderContext context)
        {
            bool available = context != null && Directory.Exists(GetAssetsRoot(context.ProjectRootPath));
            return new AuthoringResourceProviderDescriptor
            {
                ProviderId = ProviderId,
                DisplayName = "Unity Project Assets",
                SourceKind = AuthoringResourceSourceKind.UnityAsset,
                Available = available,
                Status = available ? "Ready" : "Unavailable",
                DiagnosticCode = available ? string.Empty : AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                Message = available ? string.Empty : "Unity project Assets folder is not available for resource discovery."
            };
        }

        public AuthoringResourceCollection BuildResourceCollection(AuthoringResourceProviderContext context)
        {
            var collection = new AuthoringResourceCollection
            {
                ScopeId = context != null && !string.IsNullOrWhiteSpace(context.ScopeId)
                    ? context.ScopeId
                    : "unityProjectAssets"
            };
            collection.Providers.Add(Describe(context));

            if (context != null)
            {
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "packageId", context.PackageId);
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "packagePath", context.PackagePath);
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "projectRootPath", context.ProjectRootPath);
            }

            string assetsRoot = context != null ? GetAssetsRoot(context.ProjectRootPath) : string.Empty;
            if (string.IsNullOrWhiteSpace(assetsRoot) || !Directory.Exists(assetsRoot))
            {
                collection.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Warning,
                    Code = AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                    ProviderId = ProviderId,
                    Message = "Unity project Assets folder is not available for resource discovery.",
                    SuggestedFix = "Start the Authoring server with --root pointing at the Unity project root."
                });
                return collection;
            }

            List<CachedAnimationAsset> assets = DiscoverAnimationAssets(assetsRoot);
            for (int i = 0; i < assets.Count; i++)
                collection.Items.Add(CreateAnimationItem(context, assets[i]));

            return collection;
        }

        private static string GetAssetsRoot(string projectRootPath)
        {
            if (string.IsNullOrWhiteSpace(projectRootPath))
                return string.Empty;
            return Path.Combine(projectRootPath, "Assets");
        }

        private static List<CachedAnimationAsset> DiscoverAnimationAssets(string assetsRoot)
        {
            string cacheKey = NormalizeCacheKey(assetsRoot);
            lock (CacheGate)
            {
                if (CachedIndexes.TryGetValue(cacheKey, out CachedAnimationAssetIndex cached) && !cached.Invalidated)
                    return new List<CachedAnimationAsset>(cached.Assets);

                if (cached != null)
                    cached.Dispose();

                CachedAnimationAssetIndex rebuilt = BuildAnimationAssetIndex(cacheKey);
                CachedIndexes[cacheKey] = rebuilt;
                return new List<CachedAnimationAsset>(rebuilt.Assets);
            }
        }

        private static CachedAnimationAssetIndex BuildAnimationAssetIndex(string assetsRoot)
        {
            var result = new List<CachedAnimationAsset>();
            string[] files = Directory.GetFiles(assetsRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                if (IsIgnoredAssetPath(path))
                    continue;

                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".anim" || IsAnimationContainerPath(path, extension))
                {
                    string assetPath = ToProjectAssetPath(path);
                    string hash = File.Exists(path) ? CharacterPackageHashUtility.ComputeFileSha256(path) : string.Empty;
                    result.Add(new CachedAnimationAsset(assetPath, hash));
                }
            }

            result.Sort((left, right) => string.Compare(left.AssetPath, right.AssetPath, StringComparison.OrdinalIgnoreCase));
            return new CachedAnimationAssetIndex(assetsRoot, result, CreateWatcher(assetsRoot));
        }

        private static string NormalizeCacheKey(string assetsRoot)
        {
            return Path.GetFullPath(assetsRoot);
        }

        private static FileSystemWatcher CreateWatcher(string assetsRoot)
        {
            try
            {
                var watcher = new FileSystemWatcher(assetsRoot)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
                };
                FileSystemEventHandler invalidate = (sender, args) => InvalidateCachedIndex(assetsRoot);
                RenamedEventHandler invalidateRename = (sender, args) => InvalidateCachedIndex(assetsRoot);
                watcher.Created += invalidate;
                watcher.Changed += invalidate;
                watcher.Deleted += invalidate;
                watcher.Renamed += invalidateRename;
                watcher.EnableRaisingEvents = true;
                return watcher;
            }
            catch
            {
                return null;
            }
        }

        private static void InvalidateCachedIndex(string assetsRoot)
        {
            string cacheKey = NormalizeCacheKey(assetsRoot);
            lock (CacheGate)
            {
                if (CachedIndexes.TryGetValue(cacheKey, out CachedAnimationAssetIndex cached))
                    cached.Invalidated = true;
            }
        }

        private static bool IsIgnoredAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return true;
            if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                return true;
            if (path.IndexOf("/Assets/MxFrameworkGenerated/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        private static bool IsAnimationContainerPath(string path, string extension)
        {
            if (extension != ".glb" && extension != ".gltf")
                return false;

            string normalized = path.Replace('\\', '/').ToLowerInvariant();
            return normalized.Contains("/animation/")
                || normalized.Contains("/animations/")
                || normalized.Contains("/animationclips/")
                || normalized.Contains("_animation")
                || normalized.Contains(".animation");
        }

        private static string ToProjectAssetPath(string fullPath)
        {
            string normalized = fullPath.Replace('\\', '/');
            int index = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
                return normalized.Substring(index + 1);
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return normalized;
            return normalized;
        }

        private static AuthoringResourceItem CreateAnimationItem(AuthoringResourceProviderContext context, CachedAnimationAsset asset)
        {
            string assetPath = asset.AssetPath;
            string extension = Path.GetExtension(assetPath).TrimStart('.').ToLowerInvariant();
            bool directClip = string.Equals(extension, "anim", StringComparison.OrdinalIgnoreCase);
            string usage = directClip ? "animationClip" : CharacterPackageResourceUsageIds.AnimationClipGroup;
            string stableId = AuthoringResourceNaming.BuildUnityProjectAssetStableId(assetPath);
            string providerKey = assetPath;
            string displayName = AuthoringResourceNaming.GetFileDisplayName(assetPath, stableId);
            string hash = asset.Hash;
            string assetType = directClip ? "AnimationClip" : "AnimationClipGroup";

            var item = new AuthoringResourceItem
            {
                ResourceId = AuthoringResourceProviderUtilities.BuildResourceId(AuthoringResourceProviderIds.UnityProjectAssets, stableId, providerKey),
                StableId = stableId,
                DisplayName = displayName,
                Kind = CharacterPackageResourceTypeIds.Animation,
                Usage = usage,
                SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                SourceKind = AuthoringResourceSourceKind.UnityAsset,
                BindingKind = AuthoringResourceBindingKind.UnityEditorOnlyAsset,
                ImportStatus = AuthoringResourceImportStatus.Clean,
                RuntimeAvailability = AuthoringResourceRuntimeAvailability.EditorOnly,
                Tags = new List<string> { "unity-project", "animation", usage }
            };

            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityAssetPath", assetPath);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "sourceFormat", extension);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "assetType", assetType);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "clipName", displayName);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "subClipName", directClip ? displayName : string.Empty);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "subClipId", directClip ? displayName : string.Empty);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "preloadPolicy", AuthoringResourcePreloadPolicies.AnimationWarmup);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "hash", hash);
            item.Metadata["bindingKind"] = item.BindingKind.ToString();
            item.Metadata["runtimeAvailability"] = item.RuntimeAvailability.ToString();
            item.Metadata["sourceLoadability"] = item.RuntimeAvailability.ToString();
            if (context != null)
                AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "projectRootPath", context.ProjectRootPath);

            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                BindingKind = AuthoringResourceBindingKind.UnityEditorOnlyAsset,
                BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityAssetPath,
                DisplayValue = assetPath,
                IsPrimary = true,
                ProviderResourceKey = assetPath,
                UnityAssetPath = assetPath,
                AssetType = assetType,
                Hash = hash,
                ProviderData = new Dictionary<string, string>
                {
                    { "sourceFormat", extension },
                    { "usage", usage },
                    { "clipName", displayName },
                    { "subClipName", directClip ? displayName : string.Empty },
                    { "subClipId", directClip ? displayName : string.Empty },
                    { "bindingKind", AuthoringResourceBindingKind.UnityEditorOnlyAsset.ToString() },
                    { "runtimeAvailability", AuthoringResourceRuntimeAvailability.EditorOnly.ToString() },
                    { "sourceLoadability", AuthoringResourceRuntimeAvailability.EditorOnly.ToString() },
                    { "preloadPolicy", AuthoringResourcePreloadPolicies.AnimationWarmup }
                }
            });

            return item;
        }

        private sealed class CachedAnimationAsset
        {
            public CachedAnimationAsset(string assetPath, string hash)
            {
                AssetPath = assetPath ?? string.Empty;
                Hash = hash ?? string.Empty;
            }

            public string AssetPath { get; }

            public string Hash { get; }
        }

        private sealed class CachedAnimationAssetIndex : IDisposable
        {
            public CachedAnimationAssetIndex(string assetsRoot, List<CachedAnimationAsset> assets, FileSystemWatcher watcher)
            {
                AssetsRoot = assetsRoot ?? string.Empty;
                Assets = assets ?? new List<CachedAnimationAsset>();
                Watcher = watcher;
            }

            public string AssetsRoot { get; }

            public List<CachedAnimationAsset> Assets { get; }

            public FileSystemWatcher Watcher { get; }

            public bool Invalidated { get; set; }

            public void Dispose()
            {
                if (Watcher != null)
                    Watcher.Dispose();
            }
        }
    }
}
