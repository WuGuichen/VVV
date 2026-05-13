using System.Collections.Generic;
using System.IO;
using MxFramework.Editor;
using MxFramework.Resources;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Demo
{
    public static class RuntimeVerticalSliceResourceCatalogGenerator
    {
        public const string CatalogPath = "Assets/Config/MxFramework/Demo/runtime_vertical_slice_resource_catalog.json";
        private const string MenuPath = "MxFramework/Runtime Showcase/Generate Resource Catalog";

        private static readonly string[] Roots =
        {
            "Assets/Config/MxFramework/Demo",
            "Assets/UI/MxFramework/Showcase",
            "Assets/Art/MxFramework/Showcase"
        };

        [MenuItem(MenuPath, priority = 121)]
        public static void Generate()
        {
            EnsureFolder(Path.GetDirectoryName(CatalogPath)?.Replace('\\', '/'));

            IReadOnlyList<RuntimeVerticalSliceResourceAsset> assets = CollectCatalogAssets();
            ResourceCatalog catalog = RuntimeVerticalSliceResourceCatalog.CreateCatalog(assets);
            ResourceCatalogValidationReport report = ResourceCatalogEditorValidator.ValidateCatalog(
                catalog,
                new[] { RuntimeVerticalSliceResourceCatalog.MemoryProviderId });

            if (report.HasErrors)
            {
                Debug.LogError(ResourceCatalogEditorValidator.CreateReportText(catalog, report));
                return;
            }

            File.WriteAllText(CatalogPath, WriteCatalogJson(catalog));
            AssetDatabase.ImportAsset(CatalogPath);
            Debug.Log("Runtime Vertical Slice resource catalog generated: " + CatalogPath);
        }

        public static IReadOnlyList<RuntimeVerticalSliceResourceAsset> CollectCatalogAssets()
        {
            var assets = new List<RuntimeVerticalSliceResourceAsset>();
            for (int i = 0; i < Roots.Length; i++)
            {
                string root = Roots[i];
                if (!AssetDatabase.IsValidFolder(root))
                    continue;

                string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { root });
                for (int j = 0; j < guids.Length; j++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[j]);
                    if (string.IsNullOrWhiteSpace(path) || path.EndsWith(".meta", System.StringComparison.Ordinal))
                        continue;
                    if (string.Equals(path, CatalogPath, System.StringComparison.Ordinal))
                        continue;

                    Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset == null)
                        continue;

                    string typeId = RuntimeVerticalSliceResourceCatalog.GetTypeId(asset);
                    if (string.Equals(typeId, ResourceTypeIds.Object, System.StringComparison.Ordinal))
                        continue;

                    string category = GetCategoryLabel(path);
                    assets.Add(new RuntimeVerticalSliceResourceAsset(
                        CreateId(path),
                        typeId,
                        RuntimeVerticalSliceResourceCatalog.CreateAddress(CreateAddressSuffix(path)),
                        asset,
                        path,
                        new[] { RuntimeVerticalSliceResourceCatalog.WarmupLabel, category }));
                }
            }

            assets.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            return assets;
        }

        private static string WriteCatalogJson(ResourceCatalog catalog)
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
                    dependencies = new object[0],
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

        private static string CreateId(string assetPath)
        {
            string withoutExtension = Path.ChangeExtension(assetPath, null)
                .Replace('\\', '/')
                .ToLowerInvariant();
            if (withoutExtension.StartsWith("assets/", System.StringComparison.Ordinal))
                withoutExtension = withoutExtension.Substring("assets/".Length);

            return "mxframework." + withoutExtension
                .Replace('/', '.')
                .Replace(' ', '_');
        }

        private static string CreateAddressSuffix(string assetPath)
        {
            string withoutExtension = Path.ChangeExtension(assetPath, null).Replace('\\', '/');
            if (withoutExtension.StartsWith("Assets/", System.StringComparison.Ordinal))
                withoutExtension = withoutExtension.Substring("Assets/".Length);
            return withoutExtension.ToLowerInvariant().Replace(' ', '_');
        }

        private static string GetCategoryLabel(string assetPath)
        {
            if (assetPath.StartsWith("Assets/UI/", System.StringComparison.Ordinal))
                return RuntimeVerticalSliceResourceCatalog.UiLabel;
            if (assetPath.StartsWith("Assets/Art/", System.StringComparison.Ordinal))
                return RuntimeVerticalSliceResourceCatalog.ArtLabel;
            // Catalog roots are limited to Config/UI/Art; the fallback covers Config assets.
            return RuntimeVerticalSliceResourceCatalog.ConfigLabel;
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
            public object[] dependencies;
            public string hash;
            public long size;
            public bool allowOverride;
            public Dictionary<string, string> providerData;
        }
    }
}
