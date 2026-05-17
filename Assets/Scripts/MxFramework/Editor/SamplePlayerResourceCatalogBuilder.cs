using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class SamplePlayerResourceCatalogBuilder
    {
        public const string CatalogPath = "Assets/StreamingAssets/MxFramework/Samples/mxframework_samples_player_catalog.json";
        public const string StreamingCatalogRelativePath = "MxFramework/Samples/mxframework_samples_player_catalog.json";
        public const string BundleRootPath = "Assets/StreamingAssets/MxFramework/Samples/Bundles";
        public const string StreamingBundleRootRelativePath = "MxFramework/Samples/Bundles";
        public const string StartScreenBundleName = "mxframework.samples.start_screen.assetbundle";
        public const string StartScreenButtonNormalResourceId = "ui.start_screen.button.normal";
        public const string StartScreenButtonNormalAssetPath = "Assets/UI/MxFramework/Samples/StartScreen/Textures/button_normal.png";

        private const string PlayerSmokeLabel = "warmup.demo.player_smoke";
        private const string MenuPath = "MxFramework/Samples/Build Player Resource Catalog";

        [MenuItem(MenuPath, priority = 123)]
        public static void Generate()
        {
            BuildAssetBundles();

            ResourceCatalog catalog = BuildCatalog();
            ResourceCatalogValidationReport report = ValidateGeneratedCatalog(catalog);
            if (report.HasErrors)
            {
                Debug.LogError(ResourceCatalogEditorValidator.CreateReportText(catalog, report));
                return;
            }

            EnsureAssetFolder(Path.GetDirectoryName(CatalogPath)?.Replace('\\', '/'));
            File.WriteAllText(CatalogPath, SampleResourceCatalogBuilder.WriteCatalogJson(catalog));
            AssetDatabase.ImportAsset(CatalogPath);
            AssetDatabase.Refresh();
            Debug.Log("MxFramework sample player resource catalog generated: " + CatalogPath);
        }

        public static ResourceCatalog BuildCatalog()
        {
            string bundlePath = Path.Combine(BundleRootPath, StartScreenBundleName);
            long size = File.Exists(bundlePath) ? new FileInfo(bundlePath).Length : 0;
            string hash = File.Exists(bundlePath) ? "sha256:" + ComputeSha256(bundlePath) : string.Empty;

            var entries = new[]
            {
                new ResourceCatalogEntry(
                    StartScreenButtonNormalResourceId,
                    ResourceTypeIds.Texture2D,
                    AssetBundleProvider.Id,
                    StartScreenBundleName + "|" + StartScreenButtonNormalAssetPath,
                    variant: string.Empty,
                    packageId: SampleResourceCatalogBuilder.PackageId,
                    dependencies: Array.Empty<ResourceKey>(),
                    labels: new[]
                    {
                        "package.mxframework.samples",
                        "domain.ui",
                        "sample.start_screen",
                        "warmup.demo.start_screen",
                        PlayerSmokeLabel
                    },
                    hash: hash,
                    size: size,
                    allowOverride: false,
                    providerData: new Dictionary<string, string>
                    {
                        { "bundleName", StartScreenBundleName },
                        { SampleResourceCatalogBuilder.ProviderDataAssetPathKey, StartScreenButtonNormalAssetPath }
                    })
            };

            return new ResourceCatalog(SampleResourceCatalogBuilder.CatalogId, SampleResourceCatalogBuilder.PackageId, entries);
        }

        public static ResourceCatalogValidationReport ValidateGeneratedCatalog(ResourceCatalog catalog)
        {
            ResourceCatalogValidationReport report = ResourceCatalogEditorValidator.ValidateCatalog(catalog, new[] { AssetBundleProvider.Id });
            report.Merge(ValidateBundleFiles(catalog));
            return report;
        }

        public static void BuildAssetBundles()
        {
            EnsureAssetFolder(BundleRootPath);

            string tempOutput = Path.Combine("Temp", "MxFrameworkSamplePlayerBundles");
            if (Directory.Exists(tempOutput))
                Directory.Delete(tempOutput, true);
            Directory.CreateDirectory(tempOutput);

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                tempOutput,
                new[]
                {
                    new AssetBundleBuild
                    {
                        assetBundleName = StartScreenBundleName,
                        assetNames = new[] { StartScreenButtonNormalAssetPath }
                    }
                },
                BuildAssetBundleOptions.UncompressedAssetBundle,
                EditorUserBuildSettings.activeBuildTarget);

            if (manifest == null)
                throw new InvalidOperationException("Unity failed to build sample player resource AssetBundle.");

            string source = Path.Combine(tempOutput, StartScreenBundleName);
            string destination = Path.Combine(BundleRootPath, StartScreenBundleName);
            if (!File.Exists(source))
                throw new FileNotFoundException("Built AssetBundle was not found.", source);

            File.Copy(source, destination, true);
            AssetDatabase.ImportAsset(destination);
        }

        private static ResourceCatalogValidationReport ValidateBundleFiles(ResourceCatalog catalog)
        {
            var report = new ResourceCatalogValidationReport();
            if (catalog == null)
                return report;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null || !string.Equals(entry.ProviderId, AssetBundleProvider.Id, StringComparison.Ordinal))
                    continue;

                ResourceKey key = entry.CreateKey(catalog.PackageId);
                if (!AssetBundleProvider.TryParseAddress(entry.Address, out string bundleName, out _))
                    continue;

                string bundlePath = Path.Combine(BundleRootPath, bundleName);
                if (!File.Exists(bundlePath))
                    report.AddError("BundleMissing", key, "Sample player AssetBundle was not found: " + bundlePath + ".");
            }

            return report;
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string ComputeSha256(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                char[] chars = new char[hash.Length * 2];
                for (int i = 0; i < hash.Length; i++)
                {
                    byte value = hash[i];
                    chars[i * 2] = GetHex(value >> 4);
                    chars[(i * 2) + 1] = GetHex(value & 0xF);
                }

                return new string(chars);
            }
        }

        private static char GetHex(int value)
        {
            return (char)(value < 10 ? '0' + value : 'a' + value - 10);
        }
    }
}
