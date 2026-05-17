using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Animation;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.Animation
{
    public enum MxAnimationPackageProviderSampleKind
    {
        Memory = 0,
        LocalAssetBundle = 1,
        RemoteBundle = 2
    }

    public sealed class MxAnimationPackageBuilderOptions
    {
        private readonly List<string> _acceptedProviderIds;

        public MxAnimationPackageBuilderOptions(
            string packageId = "",
            int packageVersion = 0,
            string catalogId = "",
            MxAnimationPackageProviderSampleKind providerSampleKind = MxAnimationPackageProviderSampleKind.LocalAssetBundle,
            string bundleName = "",
            string remoteBundleUrl = "",
            string remoteCacheKey = "",
            IEnumerable<string> acceptedProviderIds = null)
        {
            PackageId = packageId ?? string.Empty;
            PackageVersion = packageVersion;
            CatalogId = catalogId ?? string.Empty;
            ProviderSampleKind = providerSampleKind;
            BundleName = bundleName ?? string.Empty;
            RemoteBundleUrl = remoteBundleUrl ?? string.Empty;
            RemoteCacheKey = remoteCacheKey ?? string.Empty;
            _acceptedProviderIds = acceptedProviderIds != null
                ? new List<string>(acceptedProviderIds)
                : new List<string>();
        }

        public string PackageId { get; }
        public int PackageVersion { get; }
        public string CatalogId { get; }
        public MxAnimationPackageProviderSampleKind ProviderSampleKind { get; }
        public string BundleName { get; }
        public string RemoteBundleUrl { get; }
        public string RemoteCacheKey { get; }
        public IReadOnlyList<string> AcceptedProviderIds => _acceptedProviderIds;
    }

    public sealed class MxAnimationPackageBuildResult
    {
        public MxAnimationPackageBuildResult(
            MxAnimationClipRegistryExportResult exportResult,
            MxAnimationPackageExpectation expectation,
            ResourceCatalog catalogSnapshot,
            MxAnimationPackageCatalog packageCatalog,
            ResourceCatalogValidationReport catalogValidation,
            MxAnimationPackageValidationReport packageValidation,
            string reportText)
        {
            ExportResult = exportResult;
            Expectation = expectation;
            CatalogSnapshot = catalogSnapshot;
            PackageCatalog = packageCatalog;
            CatalogValidation = catalogValidation ?? new ResourceCatalogValidationReport();
            PackageValidation = packageValidation ?? new MxAnimationPackageValidationReport();
            ReportText = reportText ?? string.Empty;
        }

        public MxAnimationClipRegistryExportResult ExportResult { get; }
        public MxAnimationPackageExpectation Expectation { get; }
        public ResourceCatalog CatalogSnapshot { get; }
        public MxAnimationPackageCatalog PackageCatalog { get; }
        public ResourceCatalogValidationReport CatalogValidation { get; }
        public MxAnimationPackageValidationReport PackageValidation { get; }
        public string ReportText { get; }
        public bool Success =>
            ExportResult != null
            && ExportResult.Success
            && !CatalogValidation.HasErrors
            && PackageValidation.Success
            && Expectation != null
            && CatalogSnapshot != null;
    }

    internal static class MxAnimationPackageBuilder
    {
        private const string DefaultBundleSuffix = ".animation.package";

        public static MxAnimationPackageBuildResult Build(
            MxAnimationClipRegistryAsset registry,
            MxAnimationPackageBuilderOptions options = null,
            MxAnimationBatchBakeReport batchBakeReport = null,
            MxAnimationCompatibilityWorkstationReport compatibilityReport = null)
        {
            options = options ?? new MxAnimationPackageBuilderOptions();
            MxAnimationClipRegistryExportResult export = MxAnimationClipRegistryExporter.ExportStructureOnly(registry);
            string packageId = ResolvePackageId(registry, options);
            int packageVersion = options.PackageVersion > 0
                ? options.PackageVersion
                : registry != null ? Math.Max(1, registry.Version) : 1;
            string catalogId = !string.IsNullOrWhiteSpace(options.CatalogId)
                ? options.CatalogId
                : packageId + ".catalog";
            string providerId = ResolveProviderId(options.ProviderSampleKind);
            string bundleName = ResolveBundleName(packageId, options);

            var entries = new List<ResourceCatalogEntry>();
            var resources = new List<MxAnimationPackageResourceExpectation>();
            var seen = new HashSet<ResourceKey>();
            AddDefinitionResources(registry, export != null ? export.Definition : null, providerId, bundleName, options, entries, resources, seen);
            AddBakeResources(batchBakeReport, providerId, bundleName, options, entries, resources, seen);
            AddCompatibilityResource(registry, compatibilityReport, providerId, bundleName, options, entries, resources, seen);

            string catalogHash = ComputeCatalogHash(catalogId, packageId, entries);
            var catalog = new ResourceCatalog(catalogId, packageId, entries);
            var expectation = new MxAnimationPackageExpectation(
                packageId,
                packageVersion,
                catalogId,
                catalogHash,
                ResolveAcceptedProviderIds(options),
                resources);
            var packageCatalog = new MxAnimationPackageCatalog(catalog, packageVersion, catalogHash, packageId, catalogId);
            ResourceCatalogValidationReport catalogValidation = ResourceCatalogValidator.Validate(
                catalog,
                ResolveAcceptedProviderIds(options));
            MxAnimationPackageValidationReport packageValidation =
                MxAnimationPackageCatalogValidator.Validate(packageCatalog, expectation);
            string reportText = CreateReportText(
                export,
                expectation,
                catalog,
                packageCatalog,
                catalogValidation,
                packageValidation,
                options.ProviderSampleKind,
                bundleName,
                options);

            return new MxAnimationPackageBuildResult(
                export,
                expectation,
                catalog,
                packageCatalog,
                catalogValidation,
                packageValidation,
                reportText);
        }

        public static string CreateReportText(MxAnimationPackageBuildResult result)
        {
            if (result == null)
                return "MxAnimation Package Build Report\nsuccess: false\n";

            return CreateReportText(
                result.ExportResult,
                result.Expectation,
                result.CatalogSnapshot,
                result.PackageCatalog,
                result.CatalogValidation,
                result.PackageValidation,
                ResolveSampleKind(result.CatalogSnapshot),
                ResolveBundleName(result.CatalogSnapshot),
                null);
        }

        private static void AddDefinitionResources(
            MxAnimationClipRegistryAsset registry,
            MxAnimationSetDefinition definition,
            string providerId,
            string bundleName,
            MxAnimationPackageBuilderOptions options,
            List<ResourceCatalogEntry> entries,
            List<MxAnimationPackageResourceExpectation> resources,
            HashSet<ResourceKey> seen)
        {
            if (definition == null)
                return;

            AddResource(definition.DefaultClip, MxAnimationPackageResourceKind.AnimationClip, ResolveAssetPath(registry, definition.DefaultClip), providerId, bundleName, options, entries, resources, seen);
            AddResource(definition.FallbackClip, MxAnimationPackageResourceKind.AnimationClip, ResolveAssetPath(registry, definition.FallbackClip), providerId, bundleName, options, entries, resources, seen);

            for (int i = 0; i < definition.Actions.Count; i++)
            {
                MxAnimationActionBinding action = definition.Actions[i];
                if (action != null)
                    AddResource(action.Clip, MxAnimationPackageResourceKind.AnimationClip, ResolveAssetPath(registry, action.Clip), providerId, bundleName, options, entries, resources, seen);
            }

            for (int i = 0; i < definition.Blend1DDefinitions.Count; i++)
            {
                MxAnimationBlend1DDefinition blend = definition.Blend1DDefinitions[i];
                if (blend == null)
                    continue;

                for (int j = 0; j < blend.Points.Count; j++)
                    AddResource(blend.Points[j].ClipKey, MxAnimationPackageResourceKind.AnimationClip, ResolveAssetPath(registry, blend.Points[j].ClipKey), providerId, bundleName, options, entries, resources, seen);
            }

            for (int i = 0; i < definition.Blend2DDefinitions.Count; i++)
            {
                MxAnimationBlend2DDefinition blend = definition.Blend2DDefinitions[i];
                if (blend == null)
                    continue;

                for (int j = 0; j < blend.Points.Count; j++)
                    AddResource(blend.Points[j].ClipKey, MxAnimationPackageResourceKind.AnimationClip, ResolveAssetPath(registry, blend.Points[j].ClipKey), providerId, bundleName, options, entries, resources, seen);
            }

            for (int i = 0; i < definition.Layers.Count; i++)
            {
                MxAnimationLayerDefinition layer = definition.Layers[i];
                if (layer != null && layer.AvatarMaskKey.IsValid)
                    AddResource(layer.AvatarMaskKey, MxAnimationPackageResourceKind.AvatarMask, ResolveAssetPath(registry, layer.AvatarMaskKey), providerId, bundleName, options, entries, resources, seen);
            }
        }

        private static void AddBakeResources(
            MxAnimationBatchBakeReport batchBakeReport,
            string providerId,
            string bundleName,
            MxAnimationPackageBuilderOptions options,
            List<ResourceCatalogEntry> entries,
            List<MxAnimationPackageResourceExpectation> resources,
            HashSet<ResourceKey> seen)
        {
            if (batchBakeReport == null)
                return;

            for (int i = 0; i < batchBakeReport.Results.Count; i++)
            {
                MxAnimationBatchBakeClipResult result = batchBakeReport.Results[i];
                MxAnimationBakeArtifact artifact = result != null && result.BakeResult != null
                    ? result.BakeResult.Artifact
                    : null;
                if (artifact == null || !artifact.Profile.SourceClipKey.IsValid)
                    continue;

                ResourceKey bakeKey = CreateDerivedKey(
                    artifact.Profile.SourceClipKey,
                    MxAnimationResourceTypeIds.BakeArtifact,
                    ".bake");
                string assetPath = !string.IsNullOrWhiteSpace(result.BakeResult.OutputPath)
                    ? result.BakeResult.OutputPath
                    : "Assets/MxAnimationPackages/" + SanitizePathPart(bakeKey.PackageId) + "/Bake/" + SanitizePathPart(bakeKey.Id) + ".mxbake.txt";
                AddResource(
                    bakeKey,
                    MxAnimationPackageResourceKind.BakeArtifact,
                    assetPath,
                    providerId,
                    bundleName,
                    options,
                    entries,
                    resources,
                    seen,
                    artifact.ArtifactHash);
            }
        }

        private static void AddCompatibilityResource(
            MxAnimationClipRegistryAsset registry,
            MxAnimationCompatibilityWorkstationReport compatibilityReport,
            string providerId,
            string bundleName,
            MxAnimationPackageBuilderOptions options,
            List<ResourceCatalogEntry> entries,
            List<MxAnimationPackageResourceExpectation> resources,
            HashSet<ResourceKey> seen)
        {
            MxAnimationCompatibilityProfile profile = compatibilityReport != null ? compatibilityReport.Profile : null;
            if (profile == null || profile.SkeletonProfile == null)
                return;

            string packageId = ResolvePackageId(registry, options);
            string setId = registry != null && !string.IsNullOrWhiteSpace(registry.AnimationSetId)
                ? registry.AnimationSetId
                : packageId;
            string profileId = string.IsNullOrWhiteSpace(profile.SkeletonProfile.ProfileId)
                ? "skeleton"
                : profile.SkeletonProfile.ProfileId;
            var key = new ResourceKey(
                NormalizeResourceId(setId) + ".compatibility." + NormalizeResourceId(profileId),
                MxAnimationResourceTypeIds.CompatibilityProfile,
                string.Empty,
                packageId);
            string assetPath = "Assets/MxAnimationPackages/" + SanitizePathPart(packageId) + "/Compatibility/" + SanitizePathPart(profileId) + ".mxcompat.txt";
            AddResource(
                key,
                MxAnimationPackageResourceKind.CompatibilityProfile,
                assetPath,
                providerId,
                bundleName,
                options,
                entries,
                resources,
                seen,
                ComputeCompatibilityProfileHash(profile));
        }

        private static void AddResource(
            ResourceKey key,
            MxAnimationPackageResourceKind kind,
            string assetPath,
            string providerId,
            string bundleName,
            MxAnimationPackageBuilderOptions options,
            List<ResourceCatalogEntry> entries,
            List<MxAnimationPackageResourceExpectation> resources,
            HashSet<ResourceKey> seen,
            string hashOverride = "")
        {
            if (!key.IsValid || !seen.Add(key))
                return;

            string address = CreateAddress(key, assetPath, providerId, bundleName);
            string hash = !string.IsNullOrWhiteSpace(hashOverride)
                ? hashOverride
                : ComputeEntryHash(key, providerId, address, assetPath);
            Dictionary<string, string> providerData = CreateProviderData(providerId, bundleName, options, hash);
            var entry = new ResourceCatalogEntry(
                key.Id,
                key.TypeId,
                providerId,
                address,
                key.Variant,
                key.PackageId,
                labels: new[] { "mxanimation", "animation-package", KindLabel(kind) },
                hash: hash,
                providerData: providerData);
            entries.Add(entry);
            resources.Add(new MxAnimationPackageResourceExpectation(key, hash, kind: kind));
        }

        private static string ResolveAssetPath(MxAnimationClipRegistryAsset registry, ResourceKey key)
        {
            if (registry == null || !key.IsValid)
                return CreateSyntheticAssetPath(key);

            MxAnimationClipRegistryClipEntry[] clips = registry.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].CreateResourceKey(registry.PackageId) == key)
                {
                    string path = AssetDatabase.GetAssetPath(clips[i].Clip);
                    return string.IsNullOrWhiteSpace(path) ? CreateSyntheticAssetPath(key) : path;
                }
            }

            MxAnimationClipRegistryLayerEntry[] layers = registry.Layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].CreateAvatarMaskKey(registry.PackageId) == key)
                {
                    string path = AssetDatabase.GetAssetPath(layers[i].AvatarMask);
                    return string.IsNullOrWhiteSpace(path) ? CreateSyntheticAssetPath(key) : path;
                }
            }

            return CreateSyntheticAssetPath(key);
        }

        private static string CreateSyntheticAssetPath(ResourceKey key)
        {
            string extension = string.Equals(key.TypeId, ResourceTypeIds.AvatarMask, StringComparison.Ordinal)
                ? ".mask"
                : string.Equals(key.TypeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal) ? ".anim" : ".asset";
            string package = string.IsNullOrWhiteSpace(key.PackageId) ? "default" : key.PackageId;
            return "Assets/MxAnimationPackages/" + SanitizePathPart(package) + "/" + SanitizePathPart(key.Id) + extension;
        }

        private static string CreateAddress(ResourceKey key, string assetPath, string providerId, string bundleName)
        {
            if (string.Equals(providerId, AssetBundleProvider.Id, StringComparison.Ordinal)
                || string.Equals(providerId, RemoteBundleProvider.Id, StringComparison.Ordinal))
            {
                return bundleName + "|" + NormalizeProjectPath(assetPath);
            }

            return "mxanimation/" + SanitizePathPart(key.PackageId) + "/" + SanitizePathPart(key.Id);
        }

        private static Dictionary<string, string> CreateProviderData(
            string providerId,
            string bundleName,
            MxAnimationPackageBuilderOptions options,
            string hash)
        {
            if (!string.Equals(providerId, RemoteBundleProvider.Id, StringComparison.Ordinal))
                return null;

            string cacheKey = !string.IsNullOrWhiteSpace(options.RemoteCacheKey)
                ? options.RemoteCacheKey
                : bundleName + "." + ShortHash(hash);
            string url = !string.IsNullOrWhiteSpace(options.RemoteBundleUrl)
                ? options.RemoteBundleUrl
                : "file:///{bundle-root}/" + bundleName;
            return new Dictionary<string, string>
            {
                { "url", url },
                { "bundleName", bundleName },
                { "cacheKey", cacheKey }
            };
        }

        private static IReadOnlyList<string> ResolveAcceptedProviderIds(MxAnimationPackageBuilderOptions options)
        {
            if (options != null && options.AcceptedProviderIds.Count > 0)
                return options.AcceptedProviderIds;

            return new[] { "memory", AssetBundleProvider.Id, RemoteBundleProvider.Id };
        }

        private static string ResolveProviderId(MxAnimationPackageProviderSampleKind sampleKind)
        {
            switch (sampleKind)
            {
                case MxAnimationPackageProviderSampleKind.Memory:
                    return "memory";
                case MxAnimationPackageProviderSampleKind.RemoteBundle:
                    return RemoteBundleProvider.Id;
                default:
                    return AssetBundleProvider.Id;
            }
        }

        private static MxAnimationPackageProviderSampleKind ResolveSampleKind(ResourceCatalog catalog)
        {
            if (catalog == null || catalog.Entries.Count == 0)
                return MxAnimationPackageProviderSampleKind.LocalAssetBundle;

            string provider = catalog.Entries[0].ProviderId;
            if (string.Equals(provider, RemoteBundleProvider.Id, StringComparison.Ordinal))
                return MxAnimationPackageProviderSampleKind.RemoteBundle;
            if (string.Equals(provider, "memory", StringComparison.Ordinal))
                return MxAnimationPackageProviderSampleKind.Memory;
            return MxAnimationPackageProviderSampleKind.LocalAssetBundle;
        }

        private static string ResolveBundleName(ResourceCatalog catalog)
        {
            if (catalog == null || catalog.Entries.Count == 0)
                return string.Empty;

            string address = catalog.Entries[0].Address;
            int separator = address.IndexOf('|');
            return separator > 0 ? address.Substring(0, separator) : string.Empty;
        }

        private static string ResolveBundleName(string packageId, MxAnimationPackageBuilderOptions options)
        {
            if (options != null && !string.IsNullOrWhiteSpace(options.BundleName))
                return options.BundleName;

            return SanitizePathPart(packageId) + DefaultBundleSuffix;
        }

        private static string ResolvePackageId(MxAnimationClipRegistryAsset registry, MxAnimationPackageBuilderOptions options)
        {
            if (options != null && !string.IsNullOrWhiteSpace(options.PackageId))
                return options.PackageId;
            if (registry != null && !string.IsNullOrWhiteSpace(registry.PackageId))
                return registry.PackageId;
            if (registry != null && !string.IsNullOrWhiteSpace(registry.AnimationSetId))
                return NormalizeResourceId(registry.AnimationSetId) + ".package";
            return "mxanimation.package";
        }

        private static ResourceKey CreateDerivedKey(ResourceKey source, string typeId, string suffix)
        {
            return new ResourceKey(source.Id + suffix, typeId, source.Variant, source.PackageId);
        }

        private static string ComputeCatalogHash(string catalogId, string packageId, IReadOnlyList<ResourceCatalogEntry> entries)
        {
            var builder = new StringBuilder();
            builder.Append(catalogId).Append('|').Append(packageId).Append('\n');
            for (int i = 0; i < entries.Count; i++)
            {
                ResourceCatalogEntry entry = entries[i];
                builder.Append(entry.Id).Append('|')
                    .Append(entry.TypeId).Append('|')
                    .Append(entry.Variant).Append('|')
                    .Append(entry.PackageId).Append('|')
                    .Append(entry.ProviderId).Append('|')
                    .Append(entry.Address).Append('|')
                    .Append(entry.Hash).Append('|');
                AppendProviderData(builder, entry.ProviderData);
                builder.Append('\n');
            }

            return ComputeSha256(builder.ToString());
        }

        private static string ComputeCompatibilityProfileHash(MxAnimationCompatibilityProfile profile)
        {
            if (profile == null || profile.SkeletonProfile == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.Append("skeleton|")
                .Append(profile.SkeletonProfile.ProfileId)
                .Append('|')
                .Append(profile.SkeletonProfile.ProfileHash)
                .Append('\n');
            for (int i = 0; i < profile.SkeletonProfile.BonePaths.Count; i++)
                builder.Append("bone|").Append(profile.SkeletonProfile.BonePaths[i]).Append('\n');
            for (int i = 0; i < profile.SkeletonProfile.SocketPaths.Count; i++)
                builder.Append("socket|").Append(profile.SkeletonProfile.SocketPaths[i]).Append('\n');

            for (int i = 0; i < profile.ClipProfiles.Count; i++)
            {
                MxAnimationClipCompatibilityProfile clip = profile.ClipProfiles[i];
                builder.Append("clip|")
                    .Append(clip.ClipKey)
                    .Append('|')
                    .Append(clip.SkeletonProfileId)
                    .Append('|')
                    .Append(clip.SkeletonProfileHash)
                    .Append('\n');
                for (int pathIndex = 0; pathIndex < clip.BindingPaths.Count; pathIndex++)
                    builder.Append("clip.path|").Append(clip.ClipKey).Append('|').Append(clip.BindingPaths[pathIndex]).Append('\n');
            }

            for (int i = 0; i < profile.AvatarMaskProfiles.Count; i++)
            {
                MxAnimationAvatarMaskCompatibilityProfile mask = profile.AvatarMaskProfiles[i];
                builder.Append("mask|")
                    .Append(mask.AvatarMaskKey)
                    .Append('|')
                    .Append(mask.SkeletonProfileId)
                    .Append('|')
                    .Append(mask.SkeletonProfileHash)
                    .Append('\n');
                for (int pathIndex = 0; pathIndex < mask.ActivePaths.Count; pathIndex++)
                    builder.Append("mask.path|").Append(mask.AvatarMaskKey).Append('|').Append(mask.ActivePaths[pathIndex]).Append('\n');
            }

            for (int i = 0; i < profile.BakeArtifacts.Count; i++)
            {
                MxAnimationBakeArtifact artifact = profile.BakeArtifacts[i];
                builder.Append("bake|")
                    .Append(artifact.Profile != null ? artifact.Profile.SourceClipKey.ToString() : string.Empty)
                    .Append('|')
                    .Append(artifact.ArtifactHash)
                    .Append('\n');
            }

            return ComputeSha256(builder.ToString());
        }

        private static void AppendProviderData(StringBuilder builder, IReadOnlyDictionary<string, string> providerData)
        {
            if (providerData == null || providerData.Count == 0)
                return;

            var keys = new List<string>(providerData.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                builder.Append(key).Append('=').Append(providerData[key]).Append(';');
            }
        }

        private static string ComputeEntryHash(ResourceKey key, string providerId, string address, string assetPath)
        {
            string assetDependencyHash = string.Empty;
            if (!string.IsNullOrWhiteSpace(assetPath))
                assetDependencyHash = AssetDatabase.GetAssetDependencyHash(assetPath).ToString();

            return ComputeSha256(key + "|" + providerId + "|" + address + "|" + assetDependencyHash);
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder("sha256:", 71);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static string ShortHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return "nohash";
            string value = hash.StartsWith("sha256:", StringComparison.Ordinal) ? hash.Substring(7) : hash;
            return value.Length <= 12 ? value : value.Substring(0, 12);
        }

        private static string NormalizeProjectPath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return "Assets/MxAnimationPackages/missing.asset";
            if (normalized.StartsWith(Application.dataPath.Replace('\\', '/'), StringComparison.Ordinal))
                normalized = "Assets" + normalized.Substring(Application.dataPath.Length);
            return normalized;
        }

        private static string NormalizeResourceId(string value)
        {
            string input = (value ?? string.Empty).Trim().ToLowerInvariant();
            var builder = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')
                    builder.Append(c);
                else if (char.IsWhiteSpace(c) || c == '/' || c == '\\')
                    builder.Append('.');
            }

            string result = builder.ToString().Trim('.');
            return string.IsNullOrWhiteSpace(result) ? "mxanimation" : result;
        }

        private static string SanitizePathPart(string value)
        {
            string normalized = NormalizeResourceId(value).Replace('.', '_');
            return string.IsNullOrWhiteSpace(normalized) ? "mxanimation" : normalized;
        }

        private static string KindLabel(MxAnimationPackageResourceKind kind)
        {
            switch (kind)
            {
                case MxAnimationPackageResourceKind.AnimationClip:
                    return "animation-clip";
                case MxAnimationPackageResourceKind.AvatarMask:
                    return "avatar-mask";
                case MxAnimationPackageResourceKind.BakeArtifact:
                    return "bake-artifact";
                case MxAnimationPackageResourceKind.CompatibilityProfile:
                    return "compatibility-profile";
                default:
                    return "resource";
            }
        }

        private static string CreateReportText(
            MxAnimationClipRegistryExportResult export,
            MxAnimationPackageExpectation expectation,
            ResourceCatalog catalog,
            MxAnimationPackageCatalog packageCatalog,
            ResourceCatalogValidationReport catalogValidation,
            MxAnimationPackageValidationReport packageValidation,
            MxAnimationPackageProviderSampleKind sampleKind,
            string bundleName,
            MxAnimationPackageBuilderOptions options)
        {
            var builder = new StringBuilder();
            builder.Append("MxAnimation Package Build Report\n");
            builder.Append("success: ")
                .Append(export != null
                    && export.Success
                    && catalogValidation != null
                    && !catalogValidation.HasErrors
                    && packageValidation != null
                    && packageValidation.Success ? "true" : "false")
                .Append('\n');
            builder.Append("setId: ").Append(export != null && export.Definition != null ? export.Definition.SetId : string.Empty).Append('\n');
            builder.Append("definitionHash: ").Append(export != null && export.Definition != null ? export.Definition.DefinitionHash : string.Empty).Append('\n');
            builder.Append("packageId: ").Append(expectation != null ? expectation.PackageId : string.Empty).Append('\n');
            builder.Append("packageVersion: ").Append(expectation != null ? expectation.Version : 0).Append('\n');
            builder.Append("catalogId: ").Append(catalog != null ? catalog.CatalogId : string.Empty).Append('\n');
            builder.Append("catalogHash: ").Append(packageCatalog != null ? packageCatalog.CatalogHash : string.Empty).Append('\n');
            builder.Append("sampleProvider: ").Append(ResolveProviderId(sampleKind)).Append('\n');
            builder.Append("sampleBundle: ").Append(bundleName ?? string.Empty).Append('\n');
            if (options != null && !string.IsNullOrWhiteSpace(options.RemoteBundleUrl))
                builder.Append("remoteUrl: ").Append(options.RemoteBundleUrl).Append('\n');
            builder.Append("acceptedProviders: ")
                .Append(expectation != null ? string.Join(",", expectation.AcceptedProviderIds) : string.Empty)
                .Append('\n');
            builder.Append("resources: ").Append(expectation != null ? expectation.Resources.Count : 0).Append('\n');
            AppendResources(builder, expectation, catalog);
            AppendCatalogIssues(builder, "exportIssues", export != null ? export.ValidationReport : null);
            AppendCatalogIssues(builder, "catalogIssues", catalogValidation);
            AppendPackageIssues(builder, packageValidation);
            builder.Append("warmup: pass Expectation + PackageCatalog to MxAnimationWarmupRequest; RequiredForWarmup resources are added to the ResourcePreloadPlan.\n");
            return builder.ToString();
        }

        private static void AppendResources(StringBuilder builder, MxAnimationPackageExpectation expectation, ResourceCatalog catalog)
        {
            builder.Append("resourceEntries:\n");
            if (expectation == null || expectation.Resources.Count == 0)
            {
                builder.Append("- none\n");
                return;
            }

            for (int i = 0; i < expectation.Resources.Count; i++)
            {
                MxAnimationPackageResourceExpectation resource = expectation.Resources[i];
                ResourceCatalogEntry entry = FindEntry(catalog, resource.Key);
                builder.Append("- ")
                    .Append(resource.Kind)
                    .Append(" key=")
                    .Append(resource.Key)
                    .Append(" hash=")
                    .Append(resource.CatalogEntryHash)
                    .Append(" provider=")
                    .Append(entry != null ? entry.ProviderId : string.Empty)
                    .Append(" address=")
                    .Append(entry != null ? entry.Address : string.Empty)
                    .Append('\n');
            }
        }

        private static ResourceCatalogEntry FindEntry(ResourceCatalog catalog, ResourceKey key)
        {
            if (catalog == null)
                return null;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry != null && entry.CreateKey(catalog.PackageId) == key)
                    return entry;
            }

            return null;
        }

        private static void AppendCatalogIssues(StringBuilder builder, string title, ResourceCatalogValidationReport report)
        {
            builder.Append(title).Append(":\n");
            if (report == null || report.Issues.Count == 0)
            {
                builder.Append("- none\n");
                return;
            }

            for (int i = 0; i < report.Issues.Count; i++)
            {
                ResourceCatalogValidationIssue issue = report.Issues[i];
                builder.Append("- ")
                    .Append(issue.Severity)
                    .Append(' ')
                    .Append(issue.Code)
                    .Append(" key=")
                    .Append(issue.Key)
                    .Append(" message=")
                    .Append(issue.Message)
                    .Append('\n');
            }
        }

        private static void AppendPackageIssues(StringBuilder builder, MxAnimationPackageValidationReport report)
        {
            builder.Append("packageIssues:\n");
            if (report == null || report.Issues.Count == 0)
            {
                builder.Append("- none\n");
                return;
            }

            for (int i = 0; i < report.Issues.Count; i++)
            {
                MxAnimationPackageValidationIssue issue = report.Issues[i];
                builder.Append("- ")
                    .Append(issue.Severity)
                    .Append(' ')
                    .Append(issue.Code)
                    .Append(" key=")
                    .Append(issue.Key)
                    .Append(" field=")
                    .Append(issue.Field)
                    .Append(" expected=")
                    .Append(issue.Expected)
                    .Append(" actual=")
                    .Append(issue.Actual)
                    .Append(" message=")
                    .Append(issue.Message)
                    .Append('\n');
            }
        }
    }
}
