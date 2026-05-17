using System.IO;
using MxFramework.Editor;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Resources
{
    public class SamplePlayerResourceCatalogBuilderTests
    {
        [TearDown]
        public void TearDown()
        {
            AssetBundle.UnloadAllAssetBundles(true);
        }

        [Test]
        public void Generate_BuildsStreamingAssetBundleCatalogAndLoadsSmokeTexture()
        {
            SamplePlayerResourceCatalogBuilder.Generate();

            string catalogPath = Path.Combine(Application.dataPath, "../", SamplePlayerResourceCatalogBuilder.CatalogPath);
            string bundlePath = Path.Combine(Application.dataPath, "../", SamplePlayerResourceCatalogBuilder.BundleRootPath, SamplePlayerResourceCatalogBuilder.StartScreenBundleName);
            Assert.IsTrue(File.Exists(catalogPath), "Missing player catalog: " + catalogPath);
            Assert.IsTrue(File.Exists(bundlePath), "Missing player AssetBundle: " + bundlePath);

            ResourceCatalog catalog = StreamingResourceCatalogLoader.LoadFromFile(catalogPath);
            ResourceCatalogValidationReport validation = SamplePlayerResourceCatalogBuilder.ValidateGeneratedCatalog(catalog);
            Assert.False(validation.HasErrors, ResourceCatalogEditorValidator.CreateReportText(catalog, validation));
            Assert.AreEqual(1, catalog.Entries.Count);
            Assert.AreEqual(AssetBundleProvider.Id, catalog.Entries[0].ProviderId);

            string bundleRoot = Path.Combine(Application.streamingAssetsPath, SamplePlayerResourceCatalogBuilder.StreamingBundleRootRelativePath);
            var provider = new AssetBundleProvider(bundleRoot);
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(catalog);
            manager.ValidateCatalogs();

            var preloadService = new ResourcePreloadService(manager);
            ResourceLoadResult<ResourcePreloadResult> preload = preloadService.PreloadAsync(new ResourcePreloadPlan(
                "samples.player_smoke",
                labels: new[] { "warmup.demo.start_screen" })).Result;

            Assert.IsTrue(preload.Success, preload.Error.Message);
            Assert.IsTrue(preload.Value.Success, string.Join("\n", preload.Value.Errors));
            Assert.AreEqual(1, preload.Value.RequestedCount);
            Assert.AreEqual(1, preload.Value.LoadedCount);
            Assert.AreEqual(1, provider.LoadedBundleCount);

            ResourceLoadResult<ResourceHandle<Texture2D>> direct = manager.Load<Texture2D>(new ResourceKey(
                SamplePlayerResourceCatalogBuilder.StartScreenButtonNormalResourceId,
                ResourceTypeIds.Texture2D));
            Assert.IsTrue(direct.Success, direct.Error.Message);
            Assert.NotNull(direct.Value.Value);

            ResourceDebugSnapshot afterDirectLoad = manager.CreateDebugSnapshot();
            Assert.AreEqual(1, afterDirectLoad.LoadedCount);
            Assert.AreEqual(2, afterDirectLoad.TotalRefCount);

            manager.Release(direct.Value);
            preloadService.ReleaseGroup(preload.Value.Handle);

            ResourceDebugSnapshot afterRelease = manager.CreateDebugSnapshot();
            Assert.AreEqual(0, afterRelease.LoadedCount);
            Assert.AreEqual(0, afterRelease.TotalRefCount);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }
    }
}
