using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

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

        [TestCase(false)]
        [TestCase(true)]
        public void Load_KnownCache_ReloadsOfflineAfterNativeRelease(bool declaredHash)
        {
            var provider = new RemoteBundleProvider(_cacheRoot);
            var manager = IdentityManager(provider, IdentityEntry("identity.owner", SourceBundleUrl(),
                declaredHash ? ComputeSha256(SourceBundlePath()) : string.Empty, "identity.known"));
            var first = manager.Load<TextAsset>(IdentityKey("identity.owner"));
            Assert.IsTrue(first.Success, first.Error.Message);
            manager.Release(first.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
            string cachedPath = Path.Combine(_cacheRoot, "identity.known");
            string cachedHash = ComputeSha256(cachedPath);
            File.Delete(SourceBundlePath());

            for (int i = 0; i < 3; i++)
            {
                var next = manager.Load<TextAsset>(IdentityKey("identity.owner"));
                Assert.IsTrue(next.Success, next.Error.Message);
                Assert.AreEqual("MxFramework resources demo text", next.Value.Value.text.Trim());
                Assert.AreEqual(1, provider.FetchCount);
                Assert.AreEqual(cachedHash, ComputeSha256(cachedPath));
                manager.Release(next.Value);
                Assert.AreEqual(0, provider.LoadedBundleCount);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Load_UnknownDiskCache_UsesDigestOrRequiresSourceProof(bool declaredHash)
        {
            string hash = ComputeSha256(SourceBundlePath());
            File.Copy(SourceBundlePath(), Path.Combine(_cacheRoot, "identity.unknown"), true);
            var provider = new RemoteBundleProvider(_cacheRoot);
            var manager = IdentityManager(provider, IdentityEntry("identity.request", SourceBundleUrl(),
                declaredHash ? hash : string.Empty, "identity.unknown"));
            File.Delete(SourceBundlePath());
            var rejected = manager.Load<TextAsset>(IdentityKey("identity.request"));
            if (declaredHash)
            {
                Assert.IsTrue(rejected.Success, rejected.Error.Message);
                Assert.AreEqual("MxFramework resources demo text", rejected.Value.Value.text.Trim());
                Assert.AreEqual(0, provider.FetchCount);
                manager.Release(rejected.Value);
                Assert.AreEqual(0, provider.LoadedBundleCount);
                return;
            }
            AssertIdentityFailure(rejected, "identity.request", ResourceErrorCode.NotFound, "source file");
            Assert.AreEqual(1, provider.FetchCount);
            Assert.AreEqual(0, provider.LoadedBundleCount);
            Assert.AreEqual(hash, ComputeSha256(Path.Combine(_cacheRoot, "identity.unknown")));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Load_RecreatedProvider_MatchingDigestUsesOfflineCache(bool sourceOnline)
        {
            string hash = ComputeSha256(SourceBundlePath());
            var entry = IdentityEntry("identity.owner", SourceBundleUrl(), hash, "identity.recreated");
            var original = new RemoteBundleProvider(_cacheRoot);
            var oldManager = IdentityManager(original, entry);
            var first = oldManager.Load<TextAsset>(IdentityKey("identity.owner"));
            Assert.IsTrue(first.Success, first.Error.Message);
            oldManager.Release(first.Value);
            if (!sourceOnline)
                File.Delete(SourceBundlePath());
            var recreated = new RemoteBundleProvider(_cacheRoot);
            var manager = IdentityManager(recreated, entry);
            var next = manager.Load<TextAsset>(IdentityKey("identity.owner"));
            Assert.IsTrue(next.Success, next.Error.Message);
            Assert.AreEqual("MxFramework resources demo text", next.Value.Value.text.Trim());
            Assert.AreEqual(0, recreated.FetchCount);
            manager.Release(next.Value);
            Assert.AreEqual(0, recreated.LoadedBundleCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Load_EvictedIdentity_MatchingDigestStillUsesCache(bool sourceOnline)
        {
            string hash = ComputeSha256(SourceBundlePath());
            var provider = new RemoteBundleProvider(_cacheRoot);
            var entries = new List<ResourceCatalogEntry>
            {
                IdentityEntry("identity.owner", SourceBundleUrl(), hash, "identity.evicted")
            };
            for (int i = 0; i < 256; i++)
                entries.Add(IdentityEntry("identity.fill." + i, SourceBundleUrl(), hash, "identity.fill." + i));
            var manager = IdentityManager(provider, entries.ToArray());
            var first = manager.Load<TextAsset>(IdentityKey("identity.owner"));
            Assert.IsTrue(first.Success, first.Error.Message);
            manager.Release(first.Value);
            for (int i = 0; i < 256; i++)
            {
                var fill = manager.Load<TextAsset>(IdentityKey("identity.fill." + i));
                Assert.IsTrue(fill.Success, fill.Error.Message);
                manager.Release(fill.Value);
            }
            Assert.AreEqual(257, provider.FetchCount);
            if (!sourceOnline)
                File.Delete(SourceBundlePath());
            var next = manager.Load<TextAsset>(IdentityKey("identity.owner"));
            Assert.IsTrue(next.Success, next.Error.Message);
            Assert.AreEqual("MxFramework resources demo text", next.Value.Value.text.Trim());
            Assert.AreEqual(257, provider.FetchCount);
            manager.Release(next.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [Test]
        public void Load_CacheUpdateMatchingNewDigest_OverridesOldAuxiliaryIdentity()
        {
            string alternate = CreateAlternateSource(false);
            string oldHash = ComputeSha256(SourceBundlePath());
            string newHash = ComputeSha256(alternate);
            var provider = new RemoteBundleProvider(_cacheRoot);
            var manager = IdentityManager(provider,
                IdentityEntry("identity.owner", SourceBundleUrl(), oldHash, "identity.digest-update"),
                IdentityEntry("identity.request", SourceBundleUrl(), newHash, "identity.digest-update"));
            var first = manager.Load<TextAsset>(IdentityKey("identity.owner"));
            Assert.IsTrue(first.Success, first.Error.Message);
            manager.Release(first.Value);
            File.Copy(alternate, Path.Combine(_cacheRoot, "identity.digest-update"), true);
            File.Delete(SourceBundlePath());
            var next = manager.Load<TextAsset>(IdentityKey("identity.request"));
            Assert.IsTrue(next.Success, next.Error.Message);
            Assert.AreEqual("MxFramework shared resources demo text", next.Value.Value.text.Trim());
            Assert.AreEqual(1, provider.FetchCount);
            manager.Release(next.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void Load_CacheBytesReplacedAfterRelease_RepairsOrRefuses(bool sourceOffline, bool declaredHash)
        {
            string alternate = CreateAlternateSource(false);
            string hash = ComputeSha256(SourceBundlePath());
            var provider = new RemoteBundleProvider(_cacheRoot);
            var manager = IdentityManager(provider, IdentityEntry("identity.owner", SourceBundleUrl(),
                declaredHash ? hash : string.Empty, "identity.changed"));
            var first = manager.Load<TextAsset>(IdentityKey("identity.owner"));
            Assert.IsTrue(first.Success, first.Error.Message);
            manager.Release(first.Value);
            string cachedPath = Path.Combine(_cacheRoot, "identity.changed");
            File.Delete(cachedPath);
            File.Copy(alternate, cachedPath, true);
            Assert.AreNotEqual(hash, ComputeSha256(cachedPath));
            if (sourceOffline)
                File.Delete(SourceBundlePath());

            var next = manager.Load<TextAsset>(IdentityKey("identity.owner"));
            if (sourceOffline)
            {
                AssertIdentityFailure(next, "identity.owner", ResourceErrorCode.NotFound, "source file");
                Assert.AreEqual(0, provider.LoadedBundleCount);
            }
            else
            {
                Assert.IsTrue(next.Success, next.Error.Message);
                Assert.AreEqual("MxFramework resources demo text", next.Value.Value.text.Trim());
                Assert.AreEqual(hash, ComputeSha256(cachedPath));
                manager.Release(next.Value);
                Assert.AreEqual(0, provider.LoadedBundleCount);
            }
            Assert.AreEqual(2, provider.FetchCount);
        }

        [Test]
        public void Load_NewSourceOffline_RefusesAndPreservesKnownOrigin()
        {
            string alternate = CreateAlternateSource(true);
            var provider = new RemoteBundleProvider(_cacheRoot);
            var manager = IdentityManager(provider,
                IdentityEntry("identity.a", SourceBundleUrl(), string.Empty, "identity.shared"),
                IdentityEntry("identity.b", new Uri(Path.GetFullPath(alternate)).AbsoluteUri, string.Empty, "identity.shared"));
            var first = manager.Load<TextAsset>(IdentityKey("identity.a"));
            Assert.IsTrue(first.Success, first.Error.Message);
            manager.Release(first.Value);
            File.Delete(alternate);
            var rejected = manager.Load<TextAsset>(IdentityKey("identity.b"));
            AssertIdentityFailure(rejected, "identity.b", ResourceErrorCode.NotFound, "source file");
            Assert.AreEqual(2, provider.FetchCount);
            File.Delete(SourceBundlePath());
            var old = manager.Load<TextAsset>(IdentityKey("identity.a"));
            Assert.IsTrue(old.Success, old.Error.Message);
            Assert.AreEqual("MxFramework resources demo text", old.Value.Value.text.Trim());
            Assert.AreEqual(2, provider.FetchCount);
            manager.Release(old.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [Test]
        public void Load_EqualBytesAtNewSource_EstablishesNewOriginBeforeOfflineReuse()
        {
            string alternate = CreateAlternateSource(true);
            var provider = new RemoteBundleProvider(_cacheRoot);
            var manager = IdentityManager(provider,
                IdentityEntry("identity.a", SourceBundleUrl(), string.Empty, "identity.shared"),
                IdentityEntry("identity.b", new Uri(Path.GetFullPath(alternate)).AbsoluteUri, string.Empty, "identity.shared"));
            var first = manager.Load<TextAsset>(IdentityKey("identity.a"));
            Assert.IsTrue(first.Success, first.Error.Message);
            manager.Release(first.Value);
            var next = manager.Load<TextAsset>(IdentityKey("identity.b"));
            Assert.IsTrue(next.Success, next.Error.Message);
            Assert.AreEqual(2, provider.FetchCount);
            manager.Release(next.Value);
            File.Delete(alternate);
            var offline = manager.Load<TextAsset>(IdentityKey("identity.b"));
            Assert.IsTrue(offline.Success, offline.Error.Message);
            Assert.AreEqual("MxFramework resources demo text", offline.Value.Value.text.Trim());
            Assert.AreEqual(2, provider.FetchCount);
            manager.Release(offline.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Load_WrongDigest_ColdAndWarmReject(bool warm)
        {
            var provider = new RemoteBundleProvider(_cacheRoot);
            var manager = IdentityManager(provider,
                IdentityEntry("identity.owner", SourceBundleUrl(), ComputeSha256(SourceBundlePath()), "identity.owner"),
                IdentityEntry("identity.request", SourceBundleUrl(), "sha256:" + new string('0', 64), "identity.bad"));
            ResourceHandle<TextAsset> owner = warm ? manager.Load<TextAsset>(IdentityKey("identity.owner")).Value : null;
            if (warm)
                Assert.IsNotNull(owner);
            var rejected = manager.Load<TextAsset>(IdentityKey("identity.request"));

            AssertIdentityFailure(rejected, "identity.request", ResourceErrorCode.ProviderFailed, "hash mismatch");
            Assert.AreEqual(warm ? 1 : 0, provider.GetBundleRefCount(MainBundle));
            Assert.IsFalse(File.Exists(Path.Combine(_cacheRoot, "identity.bad")));
            if (warm)
            {
                Assert.AreEqual("MxFramework resources demo text", owner.Value.text.Trim());
                Assert.AreEqual(1, provider.FetchCount);
                manager.Release(owner);
            }
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void Load_SameNameDifferentSources_RejectsBothOrders(bool reverse, bool sameBytes)
        {
            string alternate = CreateAlternateSource(sameBytes);
            var provider = new RemoteBundleProvider(_cacheRoot);
            var manager = IdentityManager(provider,
                IdentityEntry("identity.a", SourceBundleUrl(), ComputeSha256(SourceBundlePath()), "identity.a"),
                IdentityEntry("identity.b", new Uri(Path.GetFullPath(alternate)).AbsoluteUri, ComputeSha256(alternate), "identity.b"));
            string ownerKey = reverse ? "identity.b" : "identity.a";
            string nextKey = reverse ? "identity.a" : "identity.b";
            var owner = manager.Load<TextAsset>(IdentityKey(ownerKey));
            Assert.IsTrue(owner.Success, owner.Error.Message);
            var rejected = manager.Load<TextAsset>(IdentityKey(nextKey));

            AssertIdentityFailure(rejected, nextKey, ResourceErrorCode.ProviderFailed, "source identity");
            Assert.AreEqual(1, provider.GetBundleRefCount(MainBundle));
            Assert.AreEqual(1, provider.FetchCount);
            Assert.AreEqual(reverse && !sameBytes ? "MxFramework shared resources demo text" : "MxFramework resources demo text", owner.Value.Value.text.Trim());
            Assert.IsFalse(File.Exists(Path.Combine(_cacheRoot, nextKey)));
            manager.Release(owner.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Load_SameSourceMultipleKeys_SharesUntilLastRelease(bool reverseRelease)
        {
            var provider = new RemoteBundleProvider(_cacheRoot);
            string hash = ComputeSha256(SourceBundlePath());
            var manager = IdentityManager(provider,
                IdentityEntry("identity.a", SourceBundleUrl(), hash, "identity.a"),
                IdentityEntry("identity.b", SourceBundleUrl(), hash.Substring(7).ToUpperInvariant(), "identity.b"));
            var a = manager.Load<TextAsset>(IdentityKey("identity.a"));
            var b = manager.Load<TextAsset>(IdentityKey("identity.b"));

            Assert.IsTrue(a.Success, a.Error.Message);
            Assert.IsTrue(b.Success, b.Error.Message);
            Assert.AreSame(a.Value.Value, b.Value.Value);
            Assert.AreEqual(2, provider.GetBundleRefCount(MainBundle));
            Assert.AreEqual(1, provider.FetchCount);
            manager.Release(reverseRelease ? b.Value : a.Value);
            Assert.AreEqual(1, provider.GetBundleRefCount(MainBundle));
            Assert.AreEqual("MxFramework resources demo text", (reverseRelease ? a.Value : b.Value).Value.text.Trim());
            manager.Release(reverseRelease ? a.Value : b.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [Test]
        public void Load_ValidDigestChangeSameUrl_RejectsUntilOwnerReleased()
        {
            string alternate = CreateAlternateSource(false);
            string oldHash = ComputeSha256(SourceBundlePath());
            string newHash = ComputeSha256(alternate);
            Assert.AreNotEqual(oldHash, newHash);
            var provider = new RemoteBundleProvider(_cacheRoot);
            var manager = IdentityManager(provider,
                IdentityEntry("identity.owner", SourceBundleUrl(), oldHash, "identity.owner"),
                IdentityEntry("identity.request", SourceBundleUrl(), newHash, "identity.request"));
            var owner = manager.Load<TextAsset>(IdentityKey("identity.owner"));
            Assert.IsTrue(owner.Success, owner.Error.Message);
            File.Copy(alternate, SourceBundlePath(), true);
            var rejected = manager.Load<TextAsset>(IdentityKey("identity.request"));

            AssertIdentityFailure(rejected, "identity.request", ResourceErrorCode.ProviderFailed, "hash mismatch");
            Assert.AreEqual(1, provider.GetBundleRefCount(MainBundle));
            Assert.AreEqual("MxFramework resources demo text", owner.Value.Value.text.Trim());
            manager.Release(owner.Value);
            var next = manager.Load<TextAsset>(IdentityKey("identity.request"));
            Assert.IsTrue(next.Success, next.Error.Message);
            Assert.AreEqual("MxFramework shared resources demo text", next.Value.Value.text.Trim());
            manager.Release(next.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Load_DependencyConflict_RollsBackNewDependency(bool wrongDigest)
        {
            string stable = CreateStableDependency();
            string alternate = CreateAlternateSource(true);
            var dependencies = new DictionaryAssetBundleDependencyProvider().Register("identity-target", "identity-stable", MainBundle);
            var provider = new RemoteBundleProvider(_cacheRoot, dependencies);
            var data = new Dictionary<string, string>
            {
                { "url", SourceBundleUrl() }, { "cacheKey", "identity.target" },
                { "url.identity-stable", new Uri(Path.GetFullPath(stable)).AbsoluteUri },
                { "hash.identity-stable", ComputeSha256(stable) }, { "cacheKey.identity-stable", "identity.stable" },
                { "dependency." + MainBundle + ".url", wrongDigest ? SourceBundleUrl() : new Uri(Path.GetFullPath(alternate)).AbsoluteUri },
                { "hash." + MainBundle, wrongDigest ? "sha256:" + new string('0', 64) : ComputeSha256(alternate) },
                { "cacheKey." + MainBundle, "identity.owner" }
            };
            var request = new ResourceCatalogEntry("identity.request", ResourceTypeIds.TextAsset, RemoteBundleProvider.Id,
                "identity-target|" + MainAssetPath, hash: ComputeSha256(SourceBundlePath()), providerData: data);
            var manager = IdentityManager(provider,
                IdentityEntry("identity.owner", SourceBundleUrl(), ComputeSha256(SourceBundlePath()), "identity.owner"), request);
            var owner = manager.Load<TextAsset>(IdentityKey("identity.owner"));
            Assert.IsTrue(owner.Success, owner.Error.Message);
            var rejected = manager.Load<TextAsset>(IdentityKey("identity.request"));

            AssertIdentityFailure(rejected, "identity.request", ResourceErrorCode.DependencyInvalid, wrongDigest ? "hash mismatch" : "source identity");
            Assert.AreEqual(1, provider.GetBundleRefCount(MainBundle));
            Assert.AreEqual(0, provider.GetBundleRefCount("identity-stable"));
            Assert.AreEqual(0, provider.GetBundleRefCount("identity-target"));
            Assert.AreEqual(1, provider.LoadedBundleCount);
            Assert.AreEqual("MxFramework resources demo text", owner.Value.Value.text.Trim());
            manager.Release(owner.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [Test]
        public void Load_MainHashConflict_RollsBackWarmDependency()
        {
            string stable = CreateStableDependency();
            var dependencies = new DictionaryAssetBundleDependencyProvider().Register(MainBundle, "identity-stable");
            var provider = new RemoteBundleProvider(_cacheRoot, dependencies);
            var ownerData = new Dictionary<string, string>
            {
                { "url", SourceBundleUrl() }, { "cacheKey", "identity.owner" },
                { "url.identity-stable", new Uri(Path.GetFullPath(stable)).AbsoluteUri },
                { "hash.identity-stable", ComputeSha256(stable) }, { "cacheKey.identity-stable", "identity.stable" }
            };
            var requestData = new Dictionary<string, string>(ownerData) { ["cacheKey"] = "identity.request" };
            var manager = IdentityManager(provider,
                new ResourceCatalogEntry("identity.owner", ResourceTypeIds.TextAsset, RemoteBundleProvider.Id, MainBundle + "|" + MainAssetPath,
                    hash: ComputeSha256(SourceBundlePath()), providerData: ownerData),
                new ResourceCatalogEntry("identity.request", ResourceTypeIds.TextAsset, RemoteBundleProvider.Id, MainBundle + "|" + MainAssetPath,
                    hash: "sha256:" + new string('0', 64), providerData: requestData));
            var owner = manager.Load<TextAsset>(IdentityKey("identity.owner"));
            Assert.IsTrue(owner.Success, owner.Error.Message);
            var rejected = manager.Load<TextAsset>(IdentityKey("identity.request"));

            AssertIdentityFailure(rejected, "identity.request", ResourceErrorCode.ProviderFailed, "hash mismatch");
            Assert.AreEqual(1, provider.GetBundleRefCount(MainBundle));
            Assert.AreEqual(1, provider.GetBundleRefCount("identity-stable"));
            Assert.AreEqual(2, provider.FetchCount);
            Assert.AreEqual("MxFramework resources demo text", owner.Value.Value.text.Trim());
            manager.Release(owner.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [Test]
        public void Load_NativeBundleInvalidated_RejectsSecondKey()
        {
            var provider = new RemoteBundleProvider(_cacheRoot);
            var manager = IdentityManager(provider,
                IdentityEntry("identity.owner", SourceBundleUrl(), ComputeSha256(SourceBundlePath()), "identity.owner"),
                IdentityEntry("identity.request", SourceBundleUrl(), ComputeSha256(SourceBundlePath()), "identity.owner"));
            var owner = manager.Load<TextAsset>(IdentityKey("identity.owner"));
            Assert.IsTrue(owner.Success, owner.Error.Message);
            AssetBundle.UnloadAllAssetBundles(true);
            var rejected = manager.Load<TextAsset>(IdentityKey("identity.request"));

            AssertIdentityFailure(rejected, "identity.request", ResourceErrorCode.ProviderFailed, "invalidated native");
            Assert.AreEqual(1, provider.GetBundleRefCount(MainBundle));
            manager.Release(owner.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [Test]
        public void Load_MissingAsset_DoesNotReleaseExistingOwner()
        {
            var provider = new RemoteBundleProvider(_cacheRoot);
            var manager = IdentityManager(provider,
                IdentityEntry("identity.owner", SourceBundleUrl(), ComputeSha256(SourceBundlePath()), "identity.owner"),
                IdentityEntry("identity.request", SourceBundleUrl(), ComputeSha256(SourceBundlePath()), "identity.owner", "missing-asset"));
            var owner = manager.Load<TextAsset>(IdentityKey("identity.owner"));
            Assert.IsTrue(owner.Success, owner.Error.Message);
            var rejected = manager.Load<TextAsset>(IdentityKey("identity.request"));
            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(ResourceErrorCode.NotFound, rejected.Error.Code);
            Assert.AreEqual(1, provider.GetBundleRefCount(MainBundle));
            Assert.AreEqual("MxFramework resources demo text", owner.Value.Value.text.Trim());
            manager.Release(owner.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [Test]
        public void Load_UnhashedDiskAlias_RefetchesRequestedSource()
        {
            string alternate = CreateAlternateSource(false);
            var provider = new RemoteBundleProvider(_cacheRoot);
            var manager = IdentityManager(provider,
                IdentityEntry("identity.a", SourceBundleUrl(), string.Empty, "identity.shared"),
                IdentityEntry("identity.b", new Uri(Path.GetFullPath(alternate)).AbsoluteUri, string.Empty, "identity.shared"));
            var a = manager.Load<TextAsset>(IdentityKey("identity.a"));
            Assert.IsTrue(a.Success, a.Error.Message);
            manager.Release(a.Value);
            var b = manager.Load<TextAsset>(IdentityKey("identity.b"));
            Assert.IsTrue(b.Success, b.Error.Message);
            Assert.AreEqual("MxFramework shared resources demo text", b.Value.Value.text.Trim());
            Assert.AreEqual(2, provider.FetchCount);
            manager.Release(b.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [Test]
        public void Load_DifferentBundleNames_ReturnIndependentContents()
        {
            string stable = CreateStableDependency();
            var provider = new RemoteBundleProvider(_cacheRoot);
            var other = new ResourceCatalogEntry("identity.b", ResourceTypeIds.TextAsset, RemoteBundleProvider.Id,
                "identity-stable|Assets/TestAssets/MxFramework/ResourcesDemo/resource_shared_text.txt", hash: ComputeSha256(stable),
                providerData: new Dictionary<string, string> { { "url", new Uri(Path.GetFullPath(stable)).AbsoluteUri }, { "cacheKey", "identity.b" } });
            var manager = IdentityManager(provider,
                IdentityEntry("identity.a", SourceBundleUrl(), ComputeSha256(SourceBundlePath()), "identity.a"), other);
            var a = manager.Load<TextAsset>(IdentityKey("identity.a"));
            var b = manager.Load<TextAsset>(IdentityKey("identity.b"));
            Assert.IsTrue(a.Success, a.Error.Message);
            Assert.IsTrue(b.Success, b.Error.Message);
            Assert.AreEqual("MxFramework resources demo text", a.Value.Value.text.Trim());
            Assert.AreEqual("MxFramework shared resources demo text", b.Value.Value.text.Trim());
            Assert.AreEqual(2, provider.LoadedBundleCount);
            Assert.AreEqual(2, provider.FetchCount);
            manager.Release(a.Value);
            Assert.AreEqual("MxFramework shared resources demo text", b.Value.Value.text.Trim());
            manager.Release(b.Value);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Load_LocalAndRemoteSameNativeBundle_PreservesFirstOwner(bool remoteFirst)
        {
            const string cacheKey = "identity.cross-cache";
            string localRoot = Path.Combine(_sourceRoot, "identity-local");
            Directory.CreateDirectory(localRoot);
            string localPath = Path.Combine(localRoot, MainBundle);
            string cachedPath = Path.Combine(_cacheRoot, cacheKey);
            File.Copy(SourceBundlePath(), localPath);
            File.Copy(SourceBundlePath(), cachedPath);
            string hash = ComputeSha256(SourceBundlePath());
            Assert.AreEqual(hash, ComputeSha256(localPath));
            Assert.AreEqual(hash, ComputeSha256(cachedPath));
            Assert.IsEmpty(AssetBundle.GetAllLoadedAssetBundles());

            var remote = new RemoteBundleProvider(_cacheRoot);
            var local = new AssetBundleProvider(localRoot, AssetBundleProvider.ExternalId);
            var manager = new ResourceManager();
            manager.RegisterProvider(remote);
            manager.RegisterProvider(local);
            ResourceKey remoteKey = IdentityKey("identity.cross.remote");
            ResourceKey localKey = IdentityKey("identity.cross.local");
            manager.AddCatalog(new ResourceCatalog("remote.local.identity.tests", string.Empty, new[]
            {
                IdentityEntry(remoteKey.Id, SourceBundleUrl(), hash, cacheKey),
                new ResourceCatalogEntry(localKey.Id, ResourceTypeIds.TextAsset, AssetBundleProvider.ExternalId,
                    MainBundle + "|" + MainAssetPath)
            }));
            ResourceKey firstKey = remoteFirst ? remoteKey : localKey;
            ResourceKey secondKey = remoteFirst ? localKey : remoteKey;
            ResourceHandle<TextAsset> firstOwner = null;
            ResourceHandle<TextAsset> nextOwner = null;
            try
            {
                var first = manager.Load<TextAsset>(firstKey);
                Assert.IsTrue(first.Success, first.Error.Message);
                firstOwner = first.Value;
                TextAsset firstAsset = firstOwner.Value;
                Assert.AreEqual("MxFramework resources demo text", firstAsset.text.Trim());

                if (!remoteFirst)
                {
                    // Only this duplicate-native-load diagnostic is expected; all other logs remain checked.
                    LogAssert.Expect(LogType.Error, new Regex(
                        "^The AssetBundle '[^'\\r\\n]*identity\\.cross-cache' can't be loaded because another AssetBundle with the same files is already loaded\\.$"));
                }
                var rejected = manager.Load<TextAsset>(secondKey);
                Assert.IsFalse(rejected.Success);
                Assert.AreEqual(ResourceErrorCode.ProviderFailed, rejected.Error.Code);
                Assert.AreEqual(secondKey, rejected.Error.Key);
                Assert.AreEqual(remoteFirst ? AssetBundleProvider.ExternalId : RemoteBundleProvider.Id,
                    rejected.Error.ProviderId);
                StringAssert.Contains(remoteFirst ? "unknown or unregistered" : "failed to load cached",
                    rejected.Error.Message);
                Assert.IsFalse(string.IsNullOrWhiteSpace(rejected.Error.Address));
                Assert.AreEqual(1, manager.CreateDebugSnapshot().TotalRefCount);
                Assert.AreEqual(remoteFirst ? 1 : 0, remote.LoadedBundleCount);
                Assert.AreEqual(remoteFirst ? 1 : 0, remote.GetBundleRefCount(MainBundle));
                Assert.AreEqual(remoteFirst ? 0 : 1, local.LoadedBundleCount);
                Assert.AreEqual(remoteFirst ? 0 : 1, local.GetBundleRefCount(MainBundle));
                Assert.AreEqual(0, remote.FetchCount);
                Assert.AreSame(firstAsset, firstOwner.Value);
                Assert.AreEqual("MxFramework resources demo text", firstAsset.text.Trim());
                Assert.AreEqual(1, new List<AssetBundle>(AssetBundle.GetAllLoadedAssetBundles()).Count);

                manager.Release(firstOwner);
                Assert.AreEqual(0, manager.CreateDebugSnapshot().TotalRefCount);
                Assert.AreEqual(0, remote.LoadedBundleCount);
                Assert.AreEqual(0, local.LoadedBundleCount);
                Assert.IsEmpty(AssetBundle.GetAllLoadedAssetBundles());
                var reloaded = manager.Load<TextAsset>(secondKey);
                Assert.IsTrue(reloaded.Success, reloaded.Error.Message);
                nextOwner = reloaded.Value;
                Assert.AreEqual("MxFramework resources demo text", nextOwner.Value.text.Trim());
                Assert.AreEqual(1, manager.CreateDebugSnapshot().TotalRefCount);
                Assert.AreEqual(0, remote.FetchCount);
                manager.Release(nextOwner);
                Assert.AreEqual(0, manager.CreateDebugSnapshot().TotalRefCount);
                Assert.AreEqual(0, remote.LoadedBundleCount);
                Assert.AreEqual(0, remote.GetBundleRefCount(MainBundle));
                Assert.AreEqual(0, local.LoadedBundleCount);
                Assert.AreEqual(0, local.GetBundleRefCount(MainBundle));
                Assert.IsEmpty(AssetBundle.GetAllLoadedAssetBundles());
                TestContext.WriteLine("Combined ownership cleanup: managerRefs=0, remoteRecords=0, remoteRefs=0, localRecords=0, localRefs=0, nativeBundles=0.");
            }
            finally
            {
                if (nextOwner != null)
                    manager.Release(nextOwner);
                if (firstOwner != null)
                    manager.Release(firstOwner);
            }
        }

        private static ResourceKey IdentityKey(string id)
        {
            return new ResourceKey(id, ResourceTypeIds.TextAsset);
        }

        private static ResourceCatalogEntry IdentityEntry(string id, string url, string hash, string cacheKey, string asset = MainAssetPath)
        {
            return new ResourceCatalogEntry(id, ResourceTypeIds.TextAsset, RemoteBundleProvider.Id, MainBundle + "|" + asset,
                hash: hash, providerData: new Dictionary<string, string> { { "url", url }, { "cacheKey", cacheKey } });
        }

        private static ResourceManager IdentityManager(RemoteBundleProvider provider, params ResourceCatalogEntry[] entries)
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog("remote.identity.tests", string.Empty, entries));
            return manager;
        }

        private static void AssertIdentityFailure(ResourceLoadResult<ResourceHandle<TextAsset>> result, string id, ResourceErrorCode code, string message)
        {
            Assert.IsFalse(result.Success);
            Assert.AreEqual(code, result.Error.Code);
            Assert.AreEqual(IdentityKey(id), result.Error.Key);
            Assert.AreEqual(RemoteBundleProvider.Id, result.Error.ProviderId);
            StringAssert.Contains(message, result.Error.Message);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Error.Address));
        }

        private string CreateAlternateSource(bool sameBytes)
        {
            string directory = Path.Combine(_sourceRoot, "identity-alternate");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, MainBundle);
            if (sameBytes)
                File.Copy(SourceBundlePath(), path, true);
            else
                BuildPipeline.BuildAssetBundles(directory, new[] { new AssetBundleBuild
                    { assetBundleName = MainBundle, assetNames = new[] { "Assets/TestAssets/MxFramework/ResourcesDemo/resource_shared_text.txt" },
                        addressableNames = new[] { MainAssetPath } } }, BuildAssetBundleOptions.UncompressedAssetBundle, EditorUserBuildSettings.activeBuildTarget);
            Assert.IsTrue(File.Exists(path));
            return path;
        }

        private string CreateStableDependency()
        {
            string directory = Path.Combine(_sourceRoot, "identity-dependencies");
            Directory.CreateDirectory(directory);
            BuildPipeline.BuildAssetBundles(directory, new[] { new AssetBundleBuild
                { assetBundleName = "identity-stable", assetNames = new[] { "Assets/TestAssets/MxFramework/ResourcesDemo/resource_shared_text.txt" } } },
                BuildAssetBundleOptions.UncompressedAssetBundle, EditorUserBuildSettings.activeBuildTarget);
            string path = Path.Combine(directory, "identity-stable");
            Assert.IsTrue(File.Exists(path));
            return path;
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
