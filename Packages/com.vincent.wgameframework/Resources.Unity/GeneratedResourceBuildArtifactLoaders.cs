using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEngine;

namespace MxFramework.Resources.Unity
{
#pragma warning disable 0649
    public sealed class GeneratedResourcePreloadGroupCatalog
    {
        private readonly List<ResourcePreloadPlan> _plans;

        public GeneratedResourcePreloadGroupCatalog(
            string profileId,
            string catalogId,
            IEnumerable<ResourcePreloadPlan> plans,
            string mappingInputFingerprint = "",
            string buildPlanFingerprint = "")
        {
            ProfileId = profileId ?? string.Empty;
            CatalogId = catalogId ?? string.Empty;
            _plans = plans != null
                ? new List<ResourcePreloadPlan>(plans)
                : new List<ResourcePreloadPlan>();
            MappingInputFingerprint = mappingInputFingerprint ?? string.Empty;
            BuildPlanFingerprint = buildPlanFingerprint ?? string.Empty;
        }

        public int SchemaVersion { get; } = 1;
        public string ProfileId { get; }
        public string CatalogId { get; }
        public string MappingInputFingerprint { get; }
        public string BuildPlanFingerprint { get; }
        public IReadOnlyList<ResourcePreloadPlan> Plans => _plans;

        public bool TryGetPlan(string groupId, out ResourcePreloadPlan plan)
        {
            for (int i = 0; i < _plans.Count; i++)
            {
                ResourcePreloadPlan candidate = _plans[i];
                if (candidate != null && string.Equals(candidate.GroupId, groupId ?? string.Empty, StringComparison.Ordinal))
                {
                    plan = candidate;
                    return true;
                }
            }

            plan = null;
            return false;
        }
    }

    public static class GeneratedResourcePreloadGroupLoader
    {
        public static GeneratedResourcePreloadGroupCatalog LoadFromStreamingAssets(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ResourceCatalogException("Generated preload group path is missing.");

            return LoadFromFile(Path.Combine(Application.streamingAssetsPath, relativePath));
        }

        public static GeneratedResourcePreloadGroupCatalog LoadFromFile(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ResourceCatalogException("Generated preload group file path is missing.");
            if (!File.Exists(fullPath))
                throw new ResourceCatalogException("Generated preload group file was not found: " + fullPath + ".");

            return LoadFromJson(File.ReadAllText(fullPath));
        }

        public static GeneratedResourcePreloadGroupCatalog LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ResourceCatalogException("Generated preload group json is empty.");

            PreloadGroupCatalogDto dto;
            try
            {
                dto = JsonConvert.DeserializeObject<PreloadGroupCatalogDto>(json);
            }
            catch (JsonException ex)
            {
                throw new ResourceCatalogException("Generated preload group json could not be parsed: " + ex.Message + ".");
            }

            if (dto == null)
                throw new ResourceCatalogException("Generated preload group json could not be parsed.");
            if (dto.schemaVersion != 1)
                throw new ResourceCatalogException("Unsupported generated preload group schema version: " + dto.schemaVersion + ".");

            var plans = new List<ResourcePreloadPlan>();
            PreloadGroupDto[] groups = dto.groups ?? Array.Empty<PreloadGroupDto>();
            for (int i = 0; i < groups.Length; i++)
                plans.Add(CreatePlan(groups[i]));

            return new GeneratedResourcePreloadGroupCatalog(dto.profileId, dto.catalogId, plans, dto.mappingInputFingerprint, dto.buildPlanFingerprint);
        }

        private static ResourcePreloadPlan CreatePlan(PreloadGroupDto dto)
        {
            if (dto == null)
                throw new ResourceCatalogException("Generated preload group entry is missing.");

            var keys = new List<ResourceKey>();
            ResourceKeyDto[] explicitKeys = dto.explicitKeys ?? Array.Empty<ResourceKeyDto>();
            for (int i = 0; i < explicitKeys.Length; i++)
            {
                ResourceKeyDto key = explicitKeys[i];
                keys.Add(new ResourceKey(key.id, key.type, key.variant, key.packageId));
            }

            return new ResourcePreloadPlan(
                dto.id,
                keys,
                dto.labels,
                dto.failFast,
                dto.maxConcurrentLoads);
        }

        [Serializable]
        private sealed class PreloadGroupCatalogDto
        {
            public int schemaVersion = 1;
            public string profileId;
            public string catalogId;
            public string mappingInputFingerprint;
            public string buildPlanFingerprint;
            public PreloadGroupDto[] groups;
        }

        [Serializable]
        private sealed class PreloadGroupDto
        {
            public string id;
            public ResourceKeyDto[] explicitKeys;
            public string[] labels;
            public bool failFast;
            public int maxConcurrentLoads;
        }
    }

    public sealed class GeneratedAssetBundleDependencyManifest
    {
        private readonly Dictionary<string, string[]> _dependencies;

        public GeneratedAssetBundleDependencyManifest(
            string profileId,
            string buildTarget,
            IReadOnlyDictionary<string, string[]> dependencies,
            string mappingInputFingerprint = "",
            string buildPlanFingerprint = "")
        {
            ProfileId = profileId ?? string.Empty;
            BuildTarget = buildTarget ?? string.Empty;
            _dependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);
            MappingInputFingerprint = mappingInputFingerprint ?? string.Empty;
            BuildPlanFingerprint = buildPlanFingerprint ?? string.Empty;
            if (dependencies != null)
            {
                foreach (KeyValuePair<string, string[]> pair in dependencies)
                    _dependencies[pair.Key] = pair.Value ?? Array.Empty<string>();
            }
        }

        public int SchemaVersion { get; } = 1;
        public string ProfileId { get; }
        public string BuildTarget { get; }
        public string MappingInputFingerprint { get; }
        public string BuildPlanFingerprint { get; }
        public IReadOnlyDictionary<string, string[]> Dependencies => _dependencies;

        public DictionaryAssetBundleDependencyProvider CreateDependencyProvider()
        {
            var provider = new DictionaryAssetBundleDependencyProvider();
            foreach (KeyValuePair<string, string[]> pair in _dependencies)
                provider.Register(pair.Key, pair.Value);
            return provider;
        }
    }

    public static class GeneratedAssetBundleDependencyManifestLoader
    {
        public static GeneratedAssetBundleDependencyManifest LoadFromStreamingAssets(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ResourceCatalogException("Generated AssetBundle dependency manifest path is missing.");

            return LoadFromFile(Path.Combine(Application.streamingAssetsPath, relativePath));
        }

        public static GeneratedAssetBundleDependencyManifest LoadFromFile(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ResourceCatalogException("Generated AssetBundle dependency manifest file path is missing.");
            if (!File.Exists(fullPath))
                throw new ResourceCatalogException("Generated AssetBundle dependency manifest file was not found: " + fullPath + ".");

            return LoadFromJson(File.ReadAllText(fullPath));
        }

        public static GeneratedAssetBundleDependencyManifest LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ResourceCatalogException("Generated AssetBundle dependency manifest json is empty.");

            AssetBundleDependencyManifestDto dto;
            try
            {
                dto = JsonConvert.DeserializeObject<AssetBundleDependencyManifestDto>(json);
            }
            catch (JsonException ex)
            {
                throw new ResourceCatalogException("Generated AssetBundle dependency manifest json could not be parsed: " + ex.Message + ".");
            }

            if (dto == null)
                throw new ResourceCatalogException("Generated AssetBundle dependency manifest json could not be parsed.");
            if (dto.schemaVersion != 1)
                throw new ResourceCatalogException("Unsupported generated AssetBundle dependency manifest schema version: " + dto.schemaVersion + ".");

            var dependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);
            AssetBundleDependencyDto[] bundles = dto.bundles ?? Array.Empty<AssetBundleDependencyDto>();
            for (int i = 0; i < bundles.Length; i++)
            {
                AssetBundleDependencyDto bundle = bundles[i];
                if (bundle == null || string.IsNullOrWhiteSpace(bundle.bundleName))
                    continue;

                dependencies[bundle.bundleName] = bundle.dependencies ?? Array.Empty<string>();
            }

            return new GeneratedAssetBundleDependencyManifest(dto.profileId, dto.buildTarget, dependencies, dto.mappingInputFingerprint, dto.buildPlanFingerprint);
        }

        [Serializable]
        private sealed class AssetBundleDependencyManifestDto
        {
            public int schemaVersion = 1;
            public string profileId;
            public string buildTarget;
            public string mappingInputFingerprint;
            public string buildPlanFingerprint;
            public AssetBundleDependencyDto[] bundles;
        }

        [Serializable]
        private sealed class AssetBundleDependencyDto
        {
            public string bundleName;
            public string[] dependencies;
        }
    }

    public sealed class GeneratedResourceArtifactSet
    {
        private readonly Dictionary<string, string> _pathsByKind;

        internal GeneratedResourceArtifactSet(
            string profileId,
            string catalogId,
            string buildTarget,
            string mappingInputFingerprint,
            string buildPlanFingerprint,
            string sourceInputFingerprint,
            Dictionary<string, string> pathsByKind)
        {
            ProfileId = profileId ?? string.Empty;
            CatalogId = catalogId ?? string.Empty;
            BuildTarget = buildTarget ?? string.Empty;
            MappingInputFingerprint = mappingInputFingerprint ?? string.Empty;
            BuildPlanFingerprint = buildPlanFingerprint ?? string.Empty;
            SourceInputFingerprint = sourceInputFingerprint ?? string.Empty;
            _pathsByKind = pathsByKind ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public int SchemaVersion { get; } = 1;
        public string ProfileId { get; }
        public string CatalogId { get; }
        public string BuildTarget { get; }
        public string MappingInputFingerprint { get; }
        public string BuildPlanFingerprint { get; }
        public string SourceInputFingerprint { get; }

        public string GetRequiredRelativePath(string kind)
        {
            if (!_pathsByKind.TryGetValue(kind ?? string.Empty, out string path))
                throw new ResourceCatalogException("Generated resource artifact set is missing required kind: " + kind + ".");
            return path;
        }
    }

    public static class GeneratedResourceArtifactSetLoader
    {
        public const string CatalogKind = "catalog";
        public const string CatalogMountsKind = "catalogMounts";
        public const string PreloadGroupsKind = "preloadGroups";
        public const string BundleDependenciesKind = "bundleDependencies";

        /// <remarks>AllocByDesign. Validates the complete committed artifact generation during bootstrap.</remarks>
        public static GeneratedResourceArtifactSet LoadFromStreamingAssets(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ResourceCatalogException("Generated resource artifact-set path is missing.");
            string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            return LoadFromFile(fullPath, Application.streamingAssetsPath);
        }

        /// <remarks>AllocByDesign. Test and build-status validation helper.</remarks>
        public static GeneratedResourceArtifactSet LoadFromFile(string manifestPath, string artifactRoot)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                throw new ResourceCatalogException("Generated resource artifact-set manifest was not found: " + manifestPath + ".");
            ArtifactSetDto dto;
            try { dto = JsonConvert.DeserializeObject<ArtifactSetDto>(File.ReadAllText(manifestPath)); }
            catch (JsonException ex) { throw new ResourceCatalogException("Generated resource artifact-set manifest could not be parsed: " + ex.Message + "."); }
            if (dto == null || dto.schemaVersion != 1)
                throw new ResourceCatalogException("Unsupported generated resource artifact-set schema.");
            Require(dto.profileId, "profileId"); Require(dto.catalogId, "catalogId"); Require(dto.buildTarget, "buildTarget");
            RequireFingerprint(dto.mappingInputFingerprint, "mappingInputFingerprint");
            RequireFingerprint(dto.buildPlanFingerprint, "buildPlanFingerprint");
            RequireFingerprint(dto.sourceInputFingerprint, "sourceInputFingerprint");

            string root = Path.GetFullPath(artifactRoot ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var pathsByKind = new Dictionary<string, string>(StringComparer.Ordinal);
            var relativePaths = new HashSet<string>(StringComparer.Ordinal);
            ArtifactDto[] artifacts = dto.artifacts ?? Array.Empty<ArtifactDto>();
            for (int i = 0; i < artifacts.Length; i++)
            {
                ArtifactDto artifact = artifacts[i] ?? throw new ResourceCatalogException("Generated resource artifact entry is missing.");
                string relative = (artifact.relativePath ?? string.Empty).Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(artifact.kind) || string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.StartsWith("../", StringComparison.Ordinal) || relative.Contains("/../"))
                    throw new ResourceCatalogException("Generated resource artifact path is invalid.");
                RequireFingerprint(artifact.sha256, "artifact sha256");
                if (!relativePaths.Add(relative))
                    throw new ResourceCatalogException("Generated resource artifact path is duplicated: " + relative + ".");
                string fullPath = Path.GetFullPath(Path.Combine(root, relative));
                if (!fullPath.StartsWith(root, StringComparison.Ordinal) || !File.Exists(fullPath))
                    throw new ResourceCatalogException("Generated resource artifact is missing or escapes its root: " + relative + ".");
                if (!string.Equals(artifact.sha256, ComputeFileSha256(fullPath), StringComparison.OrdinalIgnoreCase))
                    throw new ResourceCatalogException("Generated resource artifact hash mismatch: " + relative + ".");
                if (!string.Equals(artifact.kind, "bundle", StringComparison.Ordinal) && !pathsByKind.TryAdd(artifact.kind, relative))
                    throw new ResourceCatalogException("Generated resource artifact kind is duplicated: " + artifact.kind + ".");
            }

            RequireKind(pathsByKind, CatalogKind); RequireKind(pathsByKind, CatalogMountsKind);
            RequireKind(pathsByKind, PreloadGroupsKind); RequireKind(pathsByKind, BundleDependenciesKind);
            return new GeneratedResourceArtifactSet(dto.profileId, dto.catalogId, dto.buildTarget, dto.mappingInputFingerprint,
                dto.buildPlanFingerprint, dto.sourceInputFingerprint, pathsByKind);
        }

        private static void RequireKind(Dictionary<string, string> paths, string kind)
        {
            if (!paths.ContainsKey(kind)) throw new ResourceCatalogException("Generated resource artifact set is missing required kind: " + kind + ".");
        }

        private static void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ResourceCatalogException("Generated resource artifact-set " + name + " is missing.");
        }

        private static void RequireFingerprint(string value, string name)
        {
            const string prefix = "sha256:";
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || value.Length != prefix.Length + 64)
                throw new ResourceCatalogException("Generated resource artifact-set " + name + " is invalid.");
            for (int i = prefix.Length; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    throw new ResourceCatalogException("Generated resource artifact-set " + name + " is invalid.");
            }
        }

        private static string ComputeFileSha256(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return "sha256:" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        [Serializable]
        private sealed class ArtifactSetDto
        {
            public int schemaVersion;
            public string profileId;
            public string catalogId;
            public string buildTarget;
            public string mappingInputFingerprint;
            public string buildPlanFingerprint;
            public string sourceInputFingerprint;
            public ArtifactDto[] artifacts;
        }

        [Serializable]
        private sealed class ArtifactDto
        {
            public string kind;
            public string relativePath;
            public string sha256;
        }
    }

    [Serializable]
    internal sealed class ResourceKeyDto
    {
        public string id;
        public string type;
        public string variant;
        public string packageId;
    }
#pragma warning restore 0649
}
