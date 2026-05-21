using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Authoring
{
    public sealed class UnityProjectAssetAuthoringResourceProvider : IAuthoringResourceProvider
    {
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

            List<string> paths = DiscoverAnimationAssetPaths(assetsRoot);
            for (int i = 0; i < paths.Count; i++)
                collection.Items.Add(CreateAnimationItem(context, paths[i]));

            return collection;
        }

        private static string GetAssetsRoot(string projectRootPath)
        {
            if (string.IsNullOrWhiteSpace(projectRootPath))
                return string.Empty;
            return Path.Combine(projectRootPath, "Assets");
        }

        private static List<string> DiscoverAnimationAssetPaths(string assetsRoot)
        {
            var result = new List<string>();
            string[] files = Directory.GetFiles(assetsRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                if (IsIgnoredAssetPath(path))
                    continue;

                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".anim" || IsAnimationContainerPath(path, extension))
                    result.Add(ToProjectAssetPath(path));
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
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

        private static AuthoringResourceItem CreateAnimationItem(AuthoringResourceProviderContext context, string assetPath)
        {
            string extension = Path.GetExtension(assetPath).TrimStart('.').ToLowerInvariant();
            bool directClip = string.Equals(extension, "anim", StringComparison.OrdinalIgnoreCase);
            string usage = directClip ? "animationClip" : CharacterPackageResourceUsageIds.AnimationClipGroup;
            string stableId = "unity.project." + AuthoringResourceProviderUtilities.SanitizeStableSegment(assetPath);
            string providerKey = assetPath;
            string displayName = AuthoringResourceProviderUtilities.GetFileDisplayName(assetPath, stableId);
            string fullPath = AuthoringResourceProviderUtilities.ResolveProjectPath(context != null ? context.ProjectRootPath : string.Empty, assetPath);
            string hash = File.Exists(fullPath) ? CharacterPackageHashUtility.ComputeFileSha256(fullPath) : string.Empty;
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
    }
}
