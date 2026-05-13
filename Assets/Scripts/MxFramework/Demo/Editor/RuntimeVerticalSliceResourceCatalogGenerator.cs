using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Editor;
using MxFramework.Resources;
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
                new[] { "memory" });

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
            var builder = new StringBuilder();
            builder.Append("{\n");
            builder.Append("  \"schemaVersion\": 1,\n");
            builder.Append("  \"catalogId\": \"").Append(Escape(catalog.CatalogId)).Append("\",\n");
            builder.Append("  \"packageId\": \"").Append(Escape(catalog.PackageId)).Append("\",\n");
            builder.Append("  \"entries\": [\n");

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                builder.Append("    {\n");
                builder.Append("      \"id\": \"").Append(Escape(entry.Id)).Append("\",\n");
                builder.Append("      \"type\": \"").Append(Escape(entry.TypeId)).Append("\",\n");
                builder.Append("      \"variant\": \"").Append(Escape(entry.Variant)).Append("\",\n");
                builder.Append("      \"packageId\": \"").Append(Escape(entry.PackageId)).Append("\",\n");
                builder.Append("      \"provider\": \"").Append(Escape(entry.ProviderId)).Append("\",\n");
                builder.Append("      \"address\": \"").Append(Escape(entry.Address)).Append("\",\n");
                builder.Append("      \"labels\": [");
                for (int j = 0; j < entry.Labels.Count; j++)
                {
                    if (j > 0)
                        builder.Append(", ");
                    builder.Append("\"").Append(Escape(entry.Labels[j])).Append("\"");
                }
                builder.Append("],\n");
                builder.Append("      \"dependencies\": [],\n");
                builder.Append("      \"hash\": \"\",\n");
                builder.Append("      \"size\": ").Append(entry.Size).Append(",\n");
                builder.Append("      \"allowOverride\": false,\n");
                builder.Append("      \"providerData\": {");
                if (entry.ProviderData.TryGetValue(RuntimeVerticalSliceResourceCatalog.ProviderDataAssetPathKey, out string assetPath))
                {
                    builder.Append("\"")
                        .Append(RuntimeVerticalSliceResourceCatalog.ProviderDataAssetPathKey)
                        .Append("\": \"")
                        .Append(Escape(assetPath))
                        .Append("\"");
                }
                builder.Append("}\n");
                builder.Append("    }");
                if (i + 1 < catalog.Entries.Count)
                    builder.Append(",");
                builder.Append("\n");
            }

            builder.Append("  ]\n");
            builder.Append("}\n");
            return builder.ToString();
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

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
