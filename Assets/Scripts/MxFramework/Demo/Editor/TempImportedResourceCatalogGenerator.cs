using System.Collections.Generic;
using System.IO;
using MxFramework.Editor;
using MxFramework.Resources;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Demo
{
    public static class TempImportedResourceCatalogGenerator
    {
        public const string CatalogPath = "Assets/Config/MxFramework/ResourceCatalogs/mxframework_samples_resource_catalog.json";
        private const string MenuPath = "MxFramework/Samples/Generate Resource Catalog";

        [MenuItem(MenuPath, priority = 122)]
        public static void Generate()
        {
            ResourceCatalog catalog = TempImportedResourceCatalog.CreateCatalog();
            ResourceCatalogValidationReport report = ResourceCatalogEditorValidator.ValidateCatalog(
                catalog,
                new[] { TempImportedResourceCatalog.MemoryProviderId });
            report.Merge(TempImportedResourceCatalog.ValidateCatalogBootstrap(catalog));

            if (report.HasErrors)
            {
                Debug.LogError(ResourceCatalogEditorValidator.CreateReportText(catalog, report));
                return;
            }

            EnsureFolder(Path.GetDirectoryName(CatalogPath)?.Replace('\\', '/'));
            File.WriteAllText(CatalogPath, WriteCatalogJson(catalog));
            AssetDatabase.ImportAsset(CatalogPath);
            Debug.Log("MxFramework sample resource catalog generated: " + CatalogPath);
        }

        public static string WriteCatalogJson(ResourceCatalog catalog)
        {
            var entries = new List<ResourceCatalogEntryDto>();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                entries.Add(new ResourceCatalogEntryDto
                {
                    id = entry.Id,
                    type = entry.TypeId,
                    variant = entry.Variant,
                    packageId = entry.PackageId,
                    provider = entry.ProviderId,
                    address = entry.Address,
                    labels = Copy(entry.Labels),
                    dependencies = Copy(entry.Dependencies),
                    hash = entry.Hash,
                    size = entry.Size,
                    allowOverride = entry.AllowOverride,
                    providerData = new Dictionary<string, string>(entry.ProviderData)
                });
            }

            return JsonConvert.SerializeObject(new ResourceCatalogDto
            {
                schemaVersion = 1,
                catalogId = catalog.CatalogId,
                packageId = catalog.PackageId,
                entries = entries.ToArray()
            }, Formatting.Indented) + "\n";
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return new string[0];

            var copy = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }

        private static ResourceKeyDto[] Copy(IReadOnlyList<ResourceKey> values)
        {
            if (values == null || values.Count == 0)
                return new ResourceKeyDto[0];

            var copy = new ResourceKeyDto[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                ResourceKey key = values[i];
                copy[i] = new ResourceKeyDto
                {
                    id = key.Id,
                    type = key.TypeId,
                    variant = key.Variant,
                    packageId = key.PackageId
                };
            }

            return copy;
        }

        private sealed class ResourceCatalogDto
        {
            public int schemaVersion;
            public string catalogId;
            public string packageId;
            public ResourceCatalogEntryDto[] entries;
        }

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

        private sealed class ResourceKeyDto
        {
            public string id;
            public string type;
            public string variant;
            public string packageId;
        }
    }
}
