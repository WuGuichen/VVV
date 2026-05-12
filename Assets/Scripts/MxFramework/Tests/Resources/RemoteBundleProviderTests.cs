using System;
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
    public class RemoteBundleProviderTests
    {
        private const string MainBundle = "m6c-main";
        private const string MainAssetPath = "Assets/TestAssets/MxFramework/ResourcesDemo/resource_demo_text.txt";
        private string _sourceRoot;
        private string _cacheRoot;

        [SetUp]
        public void SetUp()
        {
            _sourceRoot = Path.Combine("Temp", "MxFrameworkRemoteBundleSource");
            _cacheRoot = Path.Combine("Temp", "MxFrameworkRemoteBundleCache");
            DeleteDirectory(_sourceRoot);
            DeleteDirectory(_cacheRoot);
            Directory.CreateDirectory(_sourceRoot);
            Directory.CreateDirectory(_cacheRoot);

            BuildPipeline.BuildAssetBundles(
                _sourceRoot,
                new[]
                {
                    new AssetBundleBuild
                    {
                        assetBundleName = MainBundle,
                        assetNames = new[] { MainAssetPath }
                    }
                },
                BuildAssetBundleOptions.UncompressedAssetBundle,
                EditorUserBuildSettings.activeBuildTarget);
        }

        [TearDown]
        public void TearDown()
        {
            AssetBundle.UnloadAllAssetBundles(true);
            DeleteDirectory(_sourceRoot);
            DeleteDirectory(_cacheRoot);
        }

        [Test]
        public void Load_FromFileUrl_CachesBundleAndReturnsAsset()
        {
            var provider = new RemoteBundleProvider(_cacheRoot);
            ResourceManager manager = CreateManager(provider, SourceBundleUrl(), ComputeSha256(SourceBundlePath()));

            ResourceLoadResult<ResourceHandle<TextAsset>> result = manager.Load<TextAsset>(new ResourceKey("demo.text.remote_main", ResourceTypeIds.TextAsset));

            Assert.IsTrue(result.Success, result.Error.Message);
            Assert.AreEqual("MxFramework resources demo text", result.Value.Value.text.Trim());
            Assert.AreEqual(1, provider.LoadedBundleCount);
            Assert.AreEqual(1, provider.GetBundleRefCount(MainBundle));
            Assert.AreEqual(1, provider.FetchCount);

            manager.Release(result.Value);

            Assert.AreEqual(0, provider.LoadedBundleCount);
            Assert.IsTrue(File.Exists(Path.Combine(_cacheRoot, "m6c.main.v1")));
        }

        [Test]
        public void Load_WhenCacheExists_DoesNotFetchAgain()
        {
            var provider = new RemoteBundleProvider(_cacheRoot);
            ResourceManager manager = CreateManager(provider, SourceBundleUrl(), ComputeSha256(SourceBundlePath()));

            ResourceHandle<TextAsset> first = manager.Load<TextAsset>(new ResourceKey("demo.text.remote_main", ResourceTypeIds.TextAsset)).Value;
            manager.Release(first);
            File.Delete(SourceBundlePath());

            ResourceLoadResult<ResourceHandle<TextAsset>> second = manager.Load<TextAsset>(new ResourceKey("demo.text.remote_main", ResourceTypeIds.TextAsset));

            Assert.IsTrue(second.Success, second.Error.Message);
            Assert.AreEqual("MxFramework resources demo text", second.Value.Value.text.Trim());
            Assert.AreEqual(1, provider.FetchCount);

            manager.Release(second.Value);
        }

        [Test]
        public void Load_WhenHashMismatch_ReturnsProviderFailedAndClearsCache()
        {
            var provider = new RemoteBundleProvider(_cacheRoot);
            ResourceManager manager = CreateManager(provider, SourceBundleUrl(), "sha256:0000");

            ResourceLoadResult<ResourceHandle<TextAsset>> result = manager.Load<TextAsset>(new ResourceKey("demo.text.remote_main", ResourceTypeIds.TextAsset));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResourceErrorCode.ProviderFailed, result.Error.Code);
            Assert.AreEqual(0, provider.LoadedBundleCount);
            Assert.IsFalse(File.Exists(Path.Combine(_cacheRoot, "m6c.main.v1")));
        }

        [Test]
        public void Load_WhenSourceMissing_ReturnsNotFound()
        {
            var provider = new RemoteBundleProvider(_cacheRoot);
            ResourceManager manager = CreateManager(provider, SourceBundleUrl(), string.Empty);
            File.Delete(SourceBundlePath());

            ResourceLoadResult<ResourceHandle<TextAsset>> result = manager.Load<TextAsset>(new ResourceKey("demo.text.remote_main", ResourceTypeIds.TextAsset));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResourceErrorCode.NotFound, result.Error.Code);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        private ResourceManager CreateManager(RemoteBundleProvider provider, string url, string hash)
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog(
                "remote.bundle.demo",
                string.Empty,
                new[]
                {
                    new ResourceCatalogEntry(
                        "demo.text.remote_main",
                        ResourceTypeIds.TextAsset,
                        RemoteBundleProvider.Id,
                        MainBundle + "|" + MainAssetPath,
                        hash: hash,
                        providerData: new Dictionary<string, string>
                        {
                            { "url", url },
                            { "bundleName", MainBundle },
                            { "cacheKey", "m6c.main.v1" }
                        })
                }));
            return manager;
        }

        private string SourceBundlePath()
        {
            return Path.Combine(_sourceRoot, MainBundle);
        }

        private string SourceBundleUrl()
        {
            return new Uri(Path.GetFullPath(SourceBundlePath())).AbsoluteUri;
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return "sha256:" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }
}
