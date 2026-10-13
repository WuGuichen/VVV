using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
        private const string IdentityBundle = "identity-common";
        private const string IdentityAsset = "identity-text";
        private readonly List<ResourceManager> _managers = new List<ResourceManager>();
        private readonly List<AssetBundle> _nativeBundles = new List<AssetBundle>();
        private readonly List<UnityEngine.Object> _detachedAssets = new List<UnityEngine.Object>();
        private string _bundleRoot;

        [SetUp]
        public void SetUp()
        {
            _managers.Clear();
            _nativeBundles.Clear();
            _detachedAssets.Clear();
            _bundleRoot = Path.Combine("Temp", "MxFrameworkResourceBundleTests");
            if (Directory.Exists(_bundleRoot))
                Directory.Delete(_bundleRoot, true);
            Directory.CreateDirectory(_bundleRoot);

            BuildFixtureBundles(_bundleRoot, new[]
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
                });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ResourceManager manager in _managers)
                manager.ReleaseAllLoaded();
            foreach (AssetBundle bundle in _nativeBundles)
            {
                if (bundle != null)
                    bundle.Unload(true);
            }
            foreach (UnityEngine.Object asset in _detachedAssets)
            {
                if (asset != null)
                    UnityEngine.Resources.UnloadAsset(asset);
            }
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
        public void Load_WhenBundleAlreadyLoadedByUnity_RejectsUnknownSourceWithoutUnloading()
        {
            AssetBundle loadedBundle = AssetBundle.LoadFromFile(Path.Combine(_bundleRoot, MainBundle));
            Assert.NotNull(loadedBundle);
            _nativeBundles.Add(loadedBundle);
            var provider = new AssetBundleProvider(_bundleRoot);
            var manager = CreateManager(provider, MainAssetPath);

            ResourceLoadResult<ResourceHandle<TextAsset>> result =
                manager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResourceErrorCode.ProviderFailed, result.Error.Code);
            StringAssert.Contains("unknown or unregistered", result.Error.Message);
            Assert.AreEqual(0, provider.LoadedBundleCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().TotalRefCount);
            Assert.IsNotNull(loadedBundle.LoadAsset<TextAsset>(MainAssetPath));
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

        [TestCase(false)]
        [TestCase(true)]
        public void Load_DifferentRootsWithSameBundleName_RejectsOtherSource(bool externalFirst)
        {
            BuildIdentityBundles(out string baseRoot, out string externalRoot);
            var local = new AssetBundleProvider(baseRoot);
            var external = new AssetBundleProvider(externalRoot, AssetBundleProvider.ExternalId);
            ResourceManager manager = CreateIdentityManager(local, external);
            ResourceKey firstKey = IdentityKey(externalFirst ? "external" : "base");
            ResourceKey secondKey = IdentityKey(externalFirst ? "base" : "external");
            AssetBundleProvider rejected = externalFirst ? local : external;
            var first = manager.Load<TextAsset>(firstKey);
            Assert.IsTrue(first.Success, first.Error.Message);
            Assert.AreEqual(externalFirst ? SharedText : MainText, first.Value.Value.text.Trim());

            var second = manager.Load<TextAsset>(secondKey);

            Assert.IsFalse(second.Success, "A different source must never return the first bundle's asset.");
            Assert.AreEqual(ResourceErrorCode.ProviderFailed, second.Error.Code);
            StringAssert.Contains("source identity", second.Error.Message);
            StringAssert.Contains(Path.GetFullPath(externalFirst ? baseRoot : externalRoot), second.Error.Address);
            Assert.AreEqual(0, rejected.LoadedBundleCount);
            Assert.AreEqual(0, rejected.GetBundleRefCount(IdentityBundle));
            Assert.AreEqual(1, manager.CreateDebugSnapshot().TotalRefCount);
            Assert.AreEqual(externalFirst ? SharedText : MainText, first.Value.Value.text.Trim());
            manager.Release(first.Value);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().TotalRefCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Load_SameCanonicalSourceAcrossProviders_KeepsBundleUntilLastRelease(bool releaseLoaderFirst)
        {
            var local = new AssetBundleProvider(_bundleRoot);
            var external = new AssetBundleProvider(Path.Combine(Path.GetFullPath(_bundleRoot), "."), AssetBundleProvider.ExternalId);
            ResourceManager manager = CreateIdentityManager(local, external, MainBundle, MainAssetPath);
            var first = manager.Load<TextAsset>(IdentityKey("base"));
            var second = manager.Load<TextAsset>(IdentityKey("external"));
            Assert.IsTrue(first.Success, first.Error.Message);
            Assert.IsTrue(second.Success, second.Error.Message);
            Assert.AreSame(first.Value.Value, second.Value.Value);
            TextAsset remaining = releaseLoaderFirst ? second.Value.Value : first.Value.Value;

            manager.Release(releaseLoaderFirst ? first.Value : second.Value);

            Assert.IsTrue(remaining != null, "Releasing one provider must preserve the other provider's lease.");
            Assert.AreEqual(MainText, remaining.text.Trim());
            manager.Release(releaseLoaderFirst ? second.Value : first.Value);
            Assert.AreEqual(0, local.LoadedBundleCount);
            Assert.AreEqual(0, external.LoadedBundleCount);
            Assert.IsTrue(remaining == null, "The last lease must unload provider-owned assets.");
        }

        [Test]
        public void Load_DifferentRootsWithUniqueNames_LoadsRequestedContents()
        {
            string baseRoot = Path.Combine(_bundleRoot, "base");
            string externalRoot = Path.Combine(_bundleRoot, "external");
            const string externalBundle = "identity-external";
            BuildIdentityBundle(baseRoot, MainAssetPath);
            BuildIdentityBundle(externalRoot, SharedAssetPath, externalBundle);
            var local = new AssetBundleProvider(baseRoot);
            var external = new AssetBundleProvider(externalRoot, AssetBundleProvider.ExternalId);
            ResourceManager manager = CreateIdentityManager(local, external, externalBundleName: externalBundle);

            var first = manager.Load<TextAsset>(IdentityKey("base"));
            var second = manager.Load<TextAsset>(IdentityKey("external"));

            Assert.IsTrue(first.Success, first.Error.Message);
            Assert.IsTrue(second.Success, second.Error.Message);
            Assert.AreEqual(MainText, first.Value.Value.text.Trim());
            Assert.AreEqual(SharedText, second.Value.Value.text.Trim());
            Assert.AreNotSame(first.Value.Value, second.Value.Value);
            manager.Release(first.Value);
            manager.Release(second.Value);
            Assert.AreEqual(0, local.LoadedBundleCount);
            Assert.AreEqual(0, external.LoadedBundleCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Load_WhenSourceBytesChanged_RejectsCachedGeneration(bool useSecondProvider)
        {
            var firstProvider = new AssetBundleProvider(_bundleRoot);
            ResourceManager firstManager = CreateManager(firstProvider, MainAssetPath);
            var first = firstManager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset));
            Assert.IsTrue(first.Success, first.Error.Message);
            File.AppendAllText(Path.Combine(_bundleRoot, MainBundle), "changed-generation");
            var nextProvider = useSecondProvider ? new AssetBundleProvider(_bundleRoot) : firstProvider;
            ResourceManager nextManager = CreateManager(nextProvider, MainAssetPath);

            var next = nextManager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset));

            Assert.IsFalse(next.Success);
            Assert.AreEqual(ResourceErrorCode.ProviderFailed, next.Error.Code);
            Assert.AreEqual(0, nextManager.CreateDebugSnapshot().TotalRefCount);
            Assert.AreEqual(1, firstProvider.GetBundleRefCount(MainBundle));
            Assert.AreEqual(MainText, first.Value.Value.text.Trim());
            firstManager.Release(first.Value);
        }

        [Test]
        public void Load_WhenConflictAfterDependencyLoad_RollsBackOnlyNewReferences()
        {
            BuildIdentityBundles(out string baseRoot, out string externalRoot);
            File.Copy(Path.Combine(_bundleRoot, SharedBundle), Path.Combine(externalRoot, SharedBundle));
            var local = new AssetBundleProvider(baseRoot);
            var dependencies = new DictionaryAssetBundleDependencyProvider().Register(IdentityBundle, SharedBundle);
            var external = new AssetBundleProvider(externalRoot, AssetBundleProvider.ExternalId, dependencies);
            ResourceManager manager = CreateIdentityManager(local, external);
            var first = manager.Load<TextAsset>(IdentityKey("base"));
            Assert.IsTrue(first.Success, first.Error.Message);

            var next = manager.Load<TextAsset>(IdentityKey("external"));

            Assert.IsFalse(next.Success);
            Assert.AreEqual(ResourceErrorCode.ProviderFailed, next.Error.Code);
            Assert.AreEqual(0, external.LoadedBundleCount);
            Assert.AreEqual(0, external.GetBundleRefCount(SharedBundle));
            Assert.AreEqual(1, local.GetBundleRefCount(IdentityBundle));
            Assert.AreEqual(1, manager.CreateDebugSnapshot().TotalRefCount);
            Assert.AreEqual(MainText, first.Value.Value.text.Trim());
            manager.Release(first.Value);
        }

        [Test]
        public void Load_WhenNativeBundleInvalidated_RejectsOldRecordAndPreservesNewLoad()
        {
            var oldProvider = new AssetBundleProvider(_bundleRoot);
            ResourceManager oldManager = CreateManager(oldProvider, MainAssetPath);
            var oldHandle = oldManager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset));
            Assert.IsTrue(oldHandle.Success, oldHandle.Error.Message);
            _detachedAssets.Add(oldHandle.Value.Value);
            // Simulate an external owner unloading this exact native bundle, never a global unload.
            foreach (AssetBundle bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (bundle.name == MainBundle)
                {
                    bundle.Unload(false);
                    break;
                }
            }
            ResourceManager staleManager = CreateManager(oldProvider, MainAssetPath);
            var stale = staleManager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset));
            Assert.IsFalse(stale.Success);
            Assert.AreEqual(ResourceErrorCode.ProviderFailed, stale.Error.Code);
            Assert.AreEqual(1, oldProvider.GetBundleRefCount(MainBundle));
            var newProvider = new AssetBundleProvider(_bundleRoot);
            ResourceManager newManager = CreateManager(newProvider, MainAssetPath);
            var fresh = newManager.Load<TextAsset>(new ResourceKey("demo.text.bundle_main", ResourceTypeIds.TextAsset));
            Assert.IsTrue(fresh.Success, fresh.Error.Message);

            oldManager.Release(oldHandle.Value);

            Assert.IsTrue(fresh.Value.Value != null);
            Assert.AreEqual(MainText, fresh.Value.Value.text.Trim());
            newManager.Release(fresh.Value);
            Assert.AreEqual(0, newProvider.LoadedBundleCount);
        }

        [Test]
        public void Load_WhenBorrowedAssetMissing_RollsBackBorrowerAndPreservesLoader()
        {
            var local = new AssetBundleProvider(_bundleRoot);
            var external = new AssetBundleProvider(_bundleRoot, AssetBundleProvider.ExternalId);
            ResourceManager manager = CreateIdentityManager(local, external, MainBundle, MainAssetPath);
            manager.AddCatalog(new ResourceCatalog("identity.missing", "external", new[]
            {
                new ResourceCatalogEntry("identity.missing.text", ResourceTypeIds.TextAsset,
                    external.ProviderId, MainBundle + "|missing-asset")
            }));
            var first = manager.Load<TextAsset>(IdentityKey("base"));
            Assert.IsTrue(first.Success, first.Error.Message);

            var missing = manager.Load<TextAsset>(new ResourceKey("identity.missing.text", ResourceTypeIds.TextAsset, packageId: "external"));

            Assert.IsFalse(missing.Success);
            Assert.AreEqual(ResourceErrorCode.NotFound, missing.Error.Code);
            Assert.AreEqual(0, external.LoadedBundleCount);
            Assert.AreEqual(1, local.GetBundleRefCount(MainBundle));
            Assert.AreEqual(1, manager.CreateDebugSnapshot().TotalRefCount);
            Assert.AreEqual(MainText, first.Value.Value.text.Trim());
            manager.Release(first.Value);
        }

        private const string MainText = "MxFramework resources demo text";
        private const string SharedText = "MxFramework shared resources demo text";

        private void BuildIdentityBundles(out string baseRoot, out string externalRoot)
        {
            baseRoot = Path.Combine(_bundleRoot, "base");
            externalRoot = Path.Combine(_bundleRoot, "external");
            BuildIdentityBundle(baseRoot, MainAssetPath);
            BuildIdentityBundle(externalRoot, SharedAssetPath);
            using (SHA256 hash = SHA256.Create())
            {
                byte[] first;
                using (var stream = File.OpenRead(Path.Combine(baseRoot, IdentityBundle)))
                    first = hash.ComputeHash(stream);
                using (var stream = File.OpenRead(Path.Combine(externalRoot, IdentityBundle)))
                    CollectionAssert.AreNotEqual(first, hash.ComputeHash(stream), "The native bundles must contain different bytes.");
            }
        }

        private static void BuildIdentityBundle(string root, string assetPath, string bundleName = IdentityBundle)
        {
            Directory.CreateDirectory(root);
            BuildFixtureBundles(root, new[]
            {
                new AssetBundleBuild
                {
                    assetBundleName = bundleName,
                    assetNames = new[] { assetPath },
                    addressableNames = new[] { IdentityAsset }
                }
            });
        }

        private static void BuildFixtureBundles(string root, AssetBundleBuild[] definitions)
        {
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(root, definitions,
                BuildAssetBundleOptions.UncompressedAssetBundle, EditorUserBuildSettings.activeBuildTarget);
            Assert.NotNull(manifest, "Native fixture bundle build failed.");
        }

        private ResourceManager CreateIdentityManager(AssetBundleProvider local, AssetBundleProvider external,
            string bundleName = IdentityBundle, string assetName = IdentityAsset, string externalBundleName = null)
        {
            var manager = new ResourceManager();
            _managers.Add(manager);
            manager.RegisterProvider(local).RegisterProvider(external);
            manager.AddCatalog(new ResourceCatalog("identity.base", "base", new[]
            {
                new ResourceCatalogEntry("identity.base.text", ResourceTypeIds.TextAsset, local.ProviderId, bundleName + "|" + assetName)
            }));
            manager.AddCatalog(new ResourceCatalog("identity.external", "external", new[]
            {
                new ResourceCatalogEntry("identity.external.text", ResourceTypeIds.TextAsset, external.ProviderId,
                    (externalBundleName ?? bundleName) + "|" + assetName)
            }));
            return manager;
        }

        private static ResourceKey IdentityKey(string packageId)
        {
            return new ResourceKey("identity." + packageId + ".text", ResourceTypeIds.TextAsset, packageId: packageId);
        }

        [Test]
        public void TryParseAddress_WhenInvalid_ReturnsFalse()
        {
            Assert.IsFalse(AssetBundleProvider.TryParseAddress(string.Empty, out _, out _));
            Assert.IsFalse(AssetBundleProvider.TryParseAddress("bundle-only", out _, out _));
            Assert.IsFalse(AssetBundleProvider.TryParseAddress("../bundle|asset", out _, out _));
            Assert.IsFalse(AssetBundleProvider.TryParseAddress("bundle|../asset", out _, out _));
        }

        [Test]
        public void SceneAssetBundleProvider_RejectsNormalResourceLoad()
        {
            var provider = new SceneAssetBundleProvider(_bundleRoot);
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog(
                "scene.demo",
                string.Empty,
                new[]
                {
                    new ResourceCatalogEntry(
                        "scene.character_test",
                        ResourceTypeIds.Scene,
                        SceneAssetBundleProvider.Id,
                        "global.scene.character_test.assetbundle|Assets/Scenes/CharacterTestScene.unity")
                }));

            ResourceLoadResult<ResourceHandle<object>> result = manager.Load<object>(new ResourceKey("scene.character_test", ResourceTypeIds.Scene));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResourceErrorCode.ProviderFailed, result.Error.Code);
            Assert.AreEqual(SceneAssetBundleProvider.Id, result.Error.ProviderId);
        }

        [Test]
        public void SceneAssetBundleProvider_TryParseAddress_RequiresUnityScenePath()
        {
            Assert.IsTrue(SceneAssetBundleProvider.TryParseAddress(
                "global.scene.character_test.assetbundle|Assets/Scenes/CharacterTestScene.unity",
                out string bundleName,
                out string scenePath));
            Assert.AreEqual("global.scene.character_test.assetbundle", bundleName);
            Assert.AreEqual("Assets/Scenes/CharacterTestScene.unity", scenePath);
            Assert.IsFalse(SceneAssetBundleProvider.TryParseAddress("bundle|Assets/Scenes/not_scene.prefab", out _, out _));
            Assert.IsFalse(SceneAssetBundleProvider.TryParseAddress("bundle|../Scenes/CharacterTestScene.unity", out _, out _));
        }

        private ResourceManager CreateManager(AssetBundleProvider provider, string assetName)
        {
            var manager = new ResourceManager();
            _managers.Add(manager);
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
