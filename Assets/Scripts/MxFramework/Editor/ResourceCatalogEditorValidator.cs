using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class ResourceCatalogEditorValidator
    {
        public static ResourceCatalogValidationReport ValidateCatalog(
            ResourceCatalog catalog,
            IEnumerable<string> registeredProviderIds = null)
        {
            ResourceCatalogValidationReport report = ResourceCatalogValidator.Validate(catalog, registeredProviderIds);
            if (catalog == null)
                return report;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                ResourceKey key = entry.CreateKey(catalog.PackageId);
                if (string.Equals(entry.ProviderId, "resources", StringComparison.Ordinal))
                {
                    ValidateResourcesEntry(entry, key, report);
                }
                else if (string.Equals(entry.ProviderId, "assetBundle", StringComparison.Ordinal))
                {
                    ValidateAssetBundleEntry(entry, key, report);
                }
                else if (string.Equals(entry.ProviderId, "streamingFile", StringComparison.Ordinal))
                {
                    ValidateStreamingFileEntry(entry, key, report);
                }
            }

            return report;
        }

        public static string CreateReportText(ResourceCatalog catalog, ResourceCatalogValidationReport report)
        {
            var builder = new System.Text.StringBuilder();
            builder.Append("MxFramework Resource Catalog Validation Report\n");
            builder.Append("catalog: ").Append(catalog != null ? catalog.CatalogId : string.Empty).Append('\n');
            builder.Append("package: ").Append(catalog != null ? catalog.PackageId : string.Empty).Append('\n');
            builder.Append("entries: ").Append(catalog != null ? catalog.Entries.Count : 0).Append('\n');
            builder.Append("errors: ").Append(report != null ? report.ErrorCount : 0).Append('\n');
            builder.Append("warnings: ").Append(report != null ? report.WarningCount : 0).Append('\n');
            builder.Append("issues:\n");

            if (report == null || report.Issues.Count == 0)
            {
                builder.Append("- none\n");
                return builder.ToString();
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

            return builder.ToString();
        }

        private static void ValidateResourcesEntry(
            ResourceCatalogEntry entry,
            ResourceKey key,
            ResourceCatalogValidationReport report)
        {
            string assetPath = FindResourcesAssetPath(entry.Address);
            if (string.IsNullOrEmpty(assetPath))
            {
                report.AddError("AssetMissing", key, "Resources asset was not found for address: " + entry.Address + ".");
                return;
            }

            ValidateMainAssetType(assetPath, entry.TypeId, key, report);
        }

        private static void ValidateAssetBundleEntry(
            ResourceCatalogEntry entry,
            ResourceKey key,
            ResourceCatalogValidationReport report)
        {
            string[] parts = entry.Address.Split('|');
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                report.AddError("AssetBundleAddressInvalid", key, "AssetBundle address must use bundleName|assetPath.");
                return;
            }

            string assetPath = parts[1];
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.GetMainAssetTypeAtPath(assetPath) == null)
            {
                report.AddError("AssetMissing", key, "AssetBundle asset path was not found: " + assetPath + ".");
                return;
            }

            ValidateMainAssetType(assetPath, entry.TypeId, key, report);
        }

        private static void ValidateStreamingFileEntry(
            ResourceCatalogEntry entry,
            ResourceKey key,
            ResourceCatalogValidationReport report)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, entry.Address));
            string streamingRoot = Path.GetFullPath(Application.streamingAssetsPath);
            if (!streamingRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                streamingRoot += Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(streamingRoot, StringComparison.Ordinal) || !File.Exists(fullPath))
                report.AddError("FileMissing", key, "StreamingAssets file was not found: " + entry.Address + ".");
        }

        private static void ValidateMainAssetType(
            string assetPath,
            string typeId,
            ResourceKey key,
            ResourceCatalogValidationReport report)
        {
            Type expectedType = UnityResourceTypeResolver.Resolve(typeId);
            Type actualType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (actualType == null)
            {
                report.AddError("AssetMissing", key, "Asset was not found: " + assetPath + ".");
                return;
            }

            if (expectedType == typeof(UnityEngine.Object))
                return;

            if (!expectedType.IsAssignableFrom(actualType))
            {
                report.AddError(
                    "TypeMismatch",
                    key,
                    "Asset type '" + actualType.Name + "' does not match catalog type '" + typeId + "' at " + assetPath + ".");
            }
        }

        private static string FindResourcesAssetPath(string resourcesAddress)
        {
            string normalizedAddress = NormalizeAddress(resourcesAddress);
            string[] paths = AssetDatabase.GetAllAssetPaths();
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                    continue;

                int resourcesIndex = path.IndexOf("/Resources/", StringComparison.Ordinal);
                if (resourcesIndex < 0)
                    continue;

                string relative = path.Substring(resourcesIndex + "/Resources/".Length);
                string withoutExtension = NormalizeAddress(Path.ChangeExtension(relative, null));
                if (string.Equals(withoutExtension, normalizedAddress, StringComparison.Ordinal))
                    return path;
            }

            return string.Empty;
        }

        private static string NormalizeAddress(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').Trim('/');
        }
    }
}
