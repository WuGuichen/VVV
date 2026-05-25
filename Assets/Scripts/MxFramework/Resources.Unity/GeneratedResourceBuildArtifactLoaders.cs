using System;
using System.Collections.Generic;
using System.IO;
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
            IEnumerable<ResourcePreloadPlan> plans)
        {
            ProfileId = profileId ?? string.Empty;
            CatalogId = catalogId ?? string.Empty;
            _plans = plans != null
                ? new List<ResourcePreloadPlan>(plans)
                : new List<ResourcePreloadPlan>();
        }

        public int SchemaVersion { get; } = 1;
        public string ProfileId { get; }
        public string CatalogId { get; }
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

            return new GeneratedResourcePreloadGroupCatalog(dto.profileId, dto.catalogId, plans);
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
            IReadOnlyDictionary<string, string[]> dependencies)
        {
            ProfileId = profileId ?? string.Empty;
            BuildTarget = buildTarget ?? string.Empty;
            _dependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (dependencies != null)
            {
                foreach (KeyValuePair<string, string[]> pair in dependencies)
                    _dependencies[pair.Key] = pair.Value ?? Array.Empty<string>();
            }
        }

        public int SchemaVersion { get; } = 1;
        public string ProfileId { get; }
        public string BuildTarget { get; }
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

            return new GeneratedAssetBundleDependencyManifest(dto.profileId, dto.buildTarget, dependencies);
        }

        [Serializable]
        private sealed class AssetBundleDependencyManifestDto
        {
            public int schemaVersion = 1;
            public string profileId;
            public string buildTarget;
            public AssetBundleDependencyDto[] bundles;
        }

        [Serializable]
        private sealed class AssetBundleDependencyDto
        {
            public string bundleName;
            public string[] dependencies;
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
