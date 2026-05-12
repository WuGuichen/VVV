using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace MxFramework.Resources.Unity
{
#pragma warning disable 0649
    public static class StreamingResourceCatalogLoader
    {
        public static ResourceCatalog LoadFromStreamingAssets(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ResourceCatalogException("Streaming resource catalog path is missing.");

            string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            return LoadFromFile(fullPath);
        }

        public static ResourceCatalog LoadFromFile(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ResourceCatalogException("Resource catalog file path is missing.");
            if (!File.Exists(fullPath))
                throw new ResourceCatalogException("Resource catalog file was not found: " + fullPath + ".");

            return LoadFromJson(File.ReadAllText(fullPath));
        }

        public static ResourceCatalog LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ResourceCatalogException("Resource catalog json is empty.");

            ResourceCatalogDto dto;
            try
            {
                dto = JsonConvert.DeserializeObject<ResourceCatalogDto>(json);
            }
            catch (JsonException ex)
            {
                throw new ResourceCatalogException("Resource catalog json could not be parsed: " + ex.Message + ".");
            }

            if (dto == null)
                throw new ResourceCatalogException("Resource catalog json could not be parsed.");
            if (dto.schemaVersion != 1)
                throw new ResourceCatalogException("Unsupported resource catalog schema version: " + dto.schemaVersion + ".");

            var entries = new List<ResourceCatalogEntry>();
            ResourceCatalogEntryDto[] dtoEntries = dto.entries ?? Array.Empty<ResourceCatalogEntryDto>();
            for (int i = 0; i < dtoEntries.Length; i++)
                entries.Add(CreateEntry(dto.packageId, dtoEntries[i]));

            return new ResourceCatalog(dto.catalogId, dto.packageId, entries);
        }

        private static ResourceCatalogEntry CreateEntry(string catalogPackageId, ResourceCatalogEntryDto dto)
        {
            if (dto == null)
                throw new ResourceCatalogException("Resource catalog entry is missing.");

            var dependencies = new List<ResourceKey>();
            ResourceKeyDto[] dtoDependencies = dto.dependencies ?? Array.Empty<ResourceKeyDto>();
            for (int i = 0; i < dtoDependencies.Length; i++)
            {
                ResourceKeyDto dependency = dtoDependencies[i];
                dependencies.Add(new ResourceKey(
                    dependency.id,
                    dependency.type,
                    dependency.variant,
                    dependency.packageId));
            }

            return new ResourceCatalogEntry(
                dto.id,
                dto.type,
                dto.provider,
                dto.address,
                dto.variant,
                string.IsNullOrWhiteSpace(dto.packageId) ? catalogPackageId : dto.packageId,
                dependencies,
                dto.labels,
                dto.hash,
                dto.size,
                dto.allowOverride,
                dto.providerData);
        }

        [Serializable]
        private sealed class ResourceCatalogDto
        {
            public int schemaVersion = 1;
            public string catalogId;
            public string packageId;
            public ResourceCatalogEntryDto[] entries;
        }

        [Serializable]
        private sealed class ResourceCatalogEntryDto
        {
            public string id;
            public string type;
            public string variant;
            public string packageId;
            public string provider;
            public string address;
            public string[] labels;
            public ResourceKeyDto[] dependencies;
            public string hash;
            public long size;
            public bool allowOverride;
            public Dictionary<string, string> providerData;
        }

        [Serializable]
        private sealed class ResourceKeyDto
        {
            public string id;
            public string type;
            public string variant;
            public string packageId;
        }
    }
#pragma warning restore 0649
}
