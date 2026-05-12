using System.IO;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Resources
{
    public class AssetBundleProviderTests
    {
        private const string MainBundle = "m3-main";
        private const string SharedBundle = "m3-shared";
        private const string MainAssetPath = "Assets/TestAssets/MxFramework/ResourcesDemo/resource_demo_text.txt";
        private const string SharedAssetPath = "Assets/TestAssets/MxFramework/ResourcesDemo/resource_shared_text.txt";
        private string _bundleRoot;

        [SetUp]
        public void SetUp()
        {
            _bundleRoot = Path.Combine("Temp", "MxFrameworkResourceBundleTests");
            if (Directory.Exists(_bundleRoot))
                Directory.Delete(_bundleRoot, true);
            Directory.CreateDirectory(_bundleRoot);

            BuildPipeline.BuildAssetBundles(
                _bundleRoot,
                new[]
                {
                    new AssetBundleBuild
                    {
                        assetBundleName = MainBundle,
                        assetNames = new[] { MainAssetPath }
                    },
                    new AssetBundleBuild
                    {
                        assetBundleName = SharedBundle,
                        assetNames = new[] { SharedAssetPath }
                    }
                },
                BuildAssetBundleOptions.UncompressedAssetBundle,
                EditorUserBuildSettings.activeBuildTarget);
        }

        [TearDown]
        public void TearDown()
        {
            AssetBundle.UnloadAllAssetBundles(true);
            if (Directory.Exists(_bundleRoot))
                Directory.Delete(_bundleRoot, true);
        }

        [Test]
        public void Load_TextAssetFromBundle_ReturnsHandle()
        {
            var provider = new AssetBundleProvider(_bundleRoot);
            var manager = CreateManager(provider, MainAssetPath);

            ResourceLoadResult<ResourceHandle<TextAsset>> result = manager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset));

            Assert.IsTrue(result.Success, result.Error.Message);
            Assert.AreEqual("MxFramework resources demo text", result.Value.Value.text.Trim());
            Assert.AreEqual(1, provider.LoadedBundleCount);
            Assert.AreEqual(1, provider.GetBundleRefCount(MainBundle));

            manager.Release(result.Value);

            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [Test]
        public void Load_WhenAssetMissing_ReturnsNotFoundAndUnloadsBundle()
        {
            var provider = new AssetBundleProvider(_bundleRoot);
            var manager = CreateManager(provider, "Assets/TestAssets/MxFramework/ResourcesDemo/missing.txt");

            ResourceLoadResult<ResourceHandle<TextAsset>> result = manager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResourceErrorCode.NotFound, result.Error.Code);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [Test]
        public void Load_WhenBundleMissing_ReturnsNotFound()
        {
            var provider = new AssetBundleProvider(_bundleRoot);
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog(
                "bundle.demo",
                string.Empty,
                new[]
                {
                    new ResourceCatalogEntry(
                        "demo.text.bundle_main",
                        ResourceTypeIds.TextAsset,
                        AssetBundleProvider.Id,
                        "missing-bundle|" + MainAssetPath)
                }));

            ResourceLoadResult<ResourceHandle<TextAsset>> result = manager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResourceErrorCode.NotFound, result.Error.Code);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [Test]
        public void Load_WithDependency_KeepsDependencyBundleUntilOwnerReleased()
        {
            var dependencies = new DictionaryAssetBundleDependencyProvider()
                .Register(MainBundle, SharedBundle);
            var provider = new AssetBundleProvider(_bundleRoot, dependencies);
            var manager = CreateManager(provider, MainAssetPath);

            ResourceHandle<TextAsset> handle = manager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset)).Value;

            Assert.AreEqual(2, provider.LoadedBundleCount);
            Assert.AreEqual(1, provider.GetBundleRefCount(MainBundle));
            Assert.AreEqual(1, provider.GetBundleRefCount(SharedBundle));

            manager.Release(handle);

            Assert.AreEqual(0, provider.LoadedBundleCount);
            Assert.AreEqual(0, provider.GetBundleRefCount(MainBundle));
            Assert.AreEqual(0, provider.GetBundleRefCount(SharedBundle));
        }

        [Test]
        public void Load_WhenDependencyBundleMissing_ReturnsDependencyInvalidAndUnloadsPreviousBundles()
        {
            var dependencies = new DictionaryAssetBundleDependencyProvider()
                .Register(MainBundle, SharedBundle, "missing-shared");
            var provider = new AssetBundleProvider(_bundleRoot, dependencies);
            var manager = CreateManager(provider, MainAssetPath);

            ResourceLoadResult<ResourceHandle<TextAsset>> result = manager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResourceErrorCode.DependencyInvalid, result.Error.Code);
            Assert.AreEqual(0, provider.LoadedBundleCount);
            Assert.AreEqual(0, provider.GetBundleRefCount(SharedBundle));
        }

        [Test]
        public void Load_SameKeyTwice_KeepsBundleUntilLastHandleReleased()
        {
            var provider = new AssetBundleProvider(_bundleRoot);
            var manager = CreateManager(provider, MainAssetPath);

            ResourceHandle<TextAsset> first = manager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset)).Value;
            ResourceHandle<TextAsset> second = manager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset)).Value;

            Assert.AreEqual(1, provider.LoadedBundleCount);
            Assert.AreEqual(1, provider.GetBundleRefCount(MainBundle));
            Assert.AreEqual(2, manager.CreateDebugSnapshot().TotalRefCount);

            manager.Release(first);

            Assert.AreEqual(1, provider.LoadedBundleCount);
            Assert.AreEqual(1, provider.GetBundleRefCount(MainBundle));

            manager.Release(second);

            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [Test]
        public void Load_WhenAssetMissingAfterDependencyLoad_UnloadsDependencyBundle()
        {
            var dependencies = new DictionaryAssetBundleDependencyProvider()
                .Register(MainBundle, SharedBundle);
            var provider = new AssetBundleProvider(_bundleRoot, dependencies);
            var manager = CreateManager(provider, "Assets/TestAssets/MxFramework/ResourcesDemo/missing.txt");

            ResourceLoadResult<ResourceHandle<TextAsset>> result = manager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResourceErrorCode.NotFound, result.Error.Code);
            Assert.AreEqual(0, provider.LoadedBundleCount);
            Assert.AreEqual(0, provider.GetBundleRefCount(SharedBundle));
        }

        [Test]
        public void TryParseAddress_WhenInvalid_ReturnsFalse()
        {
            Assert.IsFalse(AssetBundleProvider.TryParseAddress(string.Empty, out _, out _));
            Assert.IsFalse(AssetBundleProvider.TryParseAddress("bundle-only", out _, out _));
            Assert.IsFalse(AssetBundleProvider.TryParseAddress("../bundle|asset", out _, out _));
            Assert.IsFalse(AssetBundleProvider.TryParseAddress("bundle|../asset", out _, out _));
        }

        private static ResourceManager CreateManager(AssetBundleProvider provider, string assetName)
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog(
                "bundle.demo",
                string.Empty,
                new[]
                {
                    new ResourceCatalogEntry(
                        "demo.text.bundle_main",
                        ResourceTypeIds.TextAsset,
                        AssetBundleProvider.Id,
                        MainBundle + "|" + assetName)
                }));
            return manager;
        }
    }
}
