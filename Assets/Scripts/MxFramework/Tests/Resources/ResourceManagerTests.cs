using System.Collections.Generic;
using MxFramework.Diagnostics;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Resources
{
    public class ResourceManagerTests
    {
        [Test]
        public void Load_ReturnsRegisteredMemoryResourceAndTracksRefCount()
        {
            var provider = new MemoryResourceProvider().Register("demo/text", "hello");
            ResourceManager manager = CreateManager(provider, Entry("demo.text.hello", ResourceTypeIds.String, "demo/text"));

            ResourceLoadResult<ResourceHandle<string>> first = manager.Load<string>(new ResourceKey("demo.text.hello", ResourceTypeIds.String));
            ResourceLoadResult<ResourceHandle<string>> second = manager.Load<string>(new ResourceKey("demo.text.hello", ResourceTypeIds.String));

            Assert.IsTrue(first.Success, first.Error.Message);
            Assert.IsTrue(second.Success, second.Error.Message);
            Assert.AreEqual("hello", first.Value.Value);
            Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(2, manager.CreateDebugSnapshot().TotalRefCount);
        }

        [Test]
        public void Release_WhenCalledTwice_IsIdempotentAndKeepsRefCountSafe()
        {
            var provider = new MemoryResourceProvider().Register("demo/text", "hello");
            ResourceManager manager = CreateManager(provider, Entry("demo.text.hello", ResourceTypeIds.String, "demo/text"));
            ResourceHandle<string> handle = manager.Load<string>(new ResourceKey("demo.text.hello", ResourceTypeIds.String)).Value;

            manager.Release(handle);
            manager.Release(handle);

            ResourceDebugSnapshot snapshot = manager.CreateDebugSnapshot();
            Assert.AreEqual(0, snapshot.LoadedCount);
            Assert.AreEqual(0, snapshot.TotalRefCount);
            Assert.AreEqual(1, provider.ReleaseCount);
            Assert.AreEqual(ResourceHandleState.Released, handle.State);
            Assert.AreEqual(ResourceErrorCode.HandleReleased, snapshot.RecentErrors[snapshot.RecentErrors.Count - 1].Code);
        }

        [Test]
        public void AddCatalog_WhenDuplicateKeyInCatalog_Throws()
        {
            var provider = new MemoryResourceProvider();
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);

            Assert.Throws<ResourceCatalogException>(() => manager.AddCatalog(new ResourceCatalog(
                "demo",
                string.Empty,
                new[]
                {
                    Entry("demo.text.hello", ResourceTypeIds.String, "one"),
                    Entry("demo.text.hello", ResourceTypeIds.String, "two")
                })));
        }

        [Test]
        public void Load_WhenRequestedTypeDiffersFromCatalog_ReturnsTypeMismatch()
        {
            var provider = new MemoryResourceProvider().Register("demo/text", "hello");
            ResourceManager manager = CreateManager(provider, Entry("demo.text.hello", ResourceTypeIds.String, "demo/text"));

            ResourceLoadResult<ResourceHandle<int>> result = manager.Load<int>(new ResourceKey("demo.text.hello", ResourceTypeIds.String));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResourceErrorCode.TypeMismatch, result.Error.Code);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void Load_WhenDependencyIsMissing_ReturnsDependencyInvalid()
        {
            var provider = new MemoryResourceProvider().Register("demo/prefab", new TestAsset("prefab"));
            ResourceManager manager = CreateManager(
                provider,
                Entry(
                    "demo.prefab.hero",
                    "TestAsset",
                    "demo/prefab",
                    dependencies: new[] { new ResourceKey("demo.texture.missing", ResourceTypeIds.Texture2D) }));

            ResourceLoadResult<ResourceHandle<TestAsset>> result = manager.Load<TestAsset>(new ResourceKey("demo.prefab.hero", "TestAsset"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResourceErrorCode.DependencyInvalid, result.Error.Code);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void Release_WhenResourceHasDependency_ReleasesDependencyAfterOwner()
        {
            var provider = new MemoryResourceProvider()
                .Register("demo/texture", "texture")
                .Register("demo/prefab", new TestAsset("prefab"));
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.texture.icon", ResourceTypeIds.String, "demo/texture"),
                Entry(
                    "demo.prefab.hero",
                    "TestAsset",
                    "demo/prefab",
                    dependencies: new[] { new ResourceKey("demo.texture.icon", ResourceTypeIds.String) }));

            ResourceHandle<TestAsset> handle = manager.Load<TestAsset>(new ResourceKey("demo.prefab.hero", "TestAsset")).Value;
            Assert.AreEqual(2, manager.CreateDebugSnapshot().LoadedCount);

            manager.Release(handle);

            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(2, provider.ReleaseCount);
        }

        [Test]
        public void AddCatalog_WhenOverrideIsExplicit_ReplacesGlobalEntryButKeepsPackageRoute()
        {
            var provider = new MemoryResourceProvider()
                .Register("base/text", "base")
                .Register("mod/text", "mod");
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog("base", "base.package", new[] { Entry("demo.text.title", ResourceTypeIds.String, "base/text") }));
            manager.AddCatalog(new ResourceCatalog("mod", "mod.package", new[] { Entry("demo.text.title", ResourceTypeIds.String, "mod/text", allowOverride: true) }));

            ResourceHandle<string> global = manager.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String)).Value;
            ResourceHandle<string> exactBase = manager.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String, packageId: "base.package")).Value;

            Assert.AreEqual("mod", global.Value);
            Assert.AreEqual("base", exactBase.Value);
        }

        [Test]
        public void Load_WithVariantProfile_UsesActiveVariantBeforeDefault()
        {
            var provider = new MemoryResourceProvider()
                .Register("demo/default", "default")
                .Register("demo/high", "high");
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.title", ResourceTypeIds.String, "demo/default"),
                Entry("demo.text.title", ResourceTypeIds.String, "demo/high", variant: "pc.high"));
            manager.SetVariantProfile(new ResourceVariantProfile("pc.high", new[] { string.Empty }));

            ResourceHandle<string> handle = manager.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String)).Value;

            Assert.AreEqual("high", handle.Value);
            Assert.AreEqual("pc.high", handle.Key.Variant);
        }

        [Test]
        public void Load_WithVariantProfile_FallsBackOnlyWhenProfileDeclaresFallback()
        {
            var provider = new MemoryResourceProvider().Register("demo/default", "default");
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.title", ResourceTypeIds.String, "demo/default"));
            manager.SetVariantProfile(new ResourceVariantProfile("pc.high"));

            ResourceLoadResult<ResourceHandle<string>> missing = manager.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String));
            Assert.IsFalse(missing.Success);
            Assert.AreEqual(ResourceErrorCode.NotFound, missing.Error.Code);

            manager.SetVariantProfile(new ResourceVariantProfile("pc.high", new[] { string.Empty }));
            ResourceLoadResult<ResourceHandle<string>> fallback = manager.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String));

            Assert.IsTrue(fallback.Success, fallback.Error.Message);
            Assert.AreEqual("default", fallback.Value.Value);
            Assert.AreEqual(string.Empty, fallback.Value.Key.Variant);
        }

        [Test]
        public void Load_WithVariantProfile_RespectsPackageRouteBeforeGlobalOverride()
        {
            var provider = new MemoryResourceProvider()
                .Register("base/high", "base-high")
                .Register("mod/high", "mod-high");
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.SetVariantProfile(new ResourceVariantProfile("pc.high"));
            manager.AddCatalog(new ResourceCatalog("base", "base.package", new[] { Entry("demo.text.title", ResourceTypeIds.String, "base/high", variant: "pc.high") }));
            manager.AddCatalog(new ResourceCatalog("mod", "mod.package", new[] { Entry("demo.text.title", ResourceTypeIds.String, "mod/high", variant: "pc.high", allowOverride: true) }));

            ResourceHandle<string> global = manager.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String)).Value;
            ResourceHandle<string> exactBase = manager.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String, packageId: "base.package")).Value;

            Assert.AreEqual("mod-high", global.Value);
            Assert.AreEqual("base-high", exactBase.Value);
        }

        [Test]
        public void Release_WithTimedRetainPolicy_KeepsRecordUntilRetainWindowExpires()
        {
            var provider = new MemoryResourceProvider().Register("demo/text", "hello");
            ResourceManager manager = CreateManager(provider, Entry("demo.text.hello", ResourceTypeIds.String, "demo/text"));
            manager.SetRetainPolicy(ResourceRetainPolicy.Timed(durationSeconds: 1f));

            ResourceHandle<string> first = manager.Load<string>(new ResourceKey("demo.text.hello", ResourceTypeIds.String)).Value;
            manager.Release(first);

            ResourceDebugSnapshot retained = manager.CreateDebugSnapshot();
            Assert.AreEqual(1, retained.LoadedCount);
            Assert.AreEqual(0, retained.TotalRefCount);
            Assert.AreEqual(1, retained.RetainedCount);
            Assert.AreEqual(1, retained.EvictableCount);
            Assert.AreEqual(0, provider.ReleaseCount);

            ResourceHandle<string> second = manager.Load<string>(new ResourceKey("demo.text.hello", ResourceTypeIds.String)).Value;
            Assert.AreEqual("hello", second.Value);
            Assert.AreEqual(1, provider.LoadCount);

            manager.Release(second);
            Assert.AreEqual(0, manager.AdvanceRetainTime(0.5f));
            Assert.AreEqual(1, manager.CreateDebugSnapshot().RetainedCount);
            Assert.AreEqual(1, manager.AdvanceRetainTime(0.5f));
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(1, provider.ReleaseCount);
        }

        [Test]
        public void Release_WithKeepAliveRetainPolicy_KeepsRecordUntilManualEviction()
        {
            var provider = new MemoryResourceProvider().Register("demo/text", "hello");
            ResourceManager manager = CreateManager(provider, Entry("demo.text.hello", ResourceTypeIds.String, "demo/text"));
            manager.SetRetainPolicy(ResourceRetainPolicy.KeepAlive);

            ResourceHandle<string> handle = manager.Load<string>(new ResourceKey("demo.text.hello", ResourceTypeIds.String)).Value;
            manager.Release(handle);

            ResourceDebugSnapshot retained = manager.CreateDebugSnapshot();
            Assert.AreEqual(1, retained.LoadedCount);
            Assert.AreEqual(1, retained.RetainedCount);
            Assert.AreEqual(1, retained.PinnedCount);
            Assert.AreEqual(1, retained.RetainPolicyCount);
            Assert.AreEqual(0, provider.ReleaseCount);

            Assert.AreEqual(1, manager.EvictRetainedResources());
            ResourceDebugSnapshot evicted = manager.CreateDebugSnapshot();
            Assert.AreEqual(0, evicted.LoadedCount);
            Assert.AreEqual(0, evicted.RetainedCount);
            Assert.AreEqual(1, evicted.RecentEvictions.Count);
            Assert.AreEqual(1, provider.ReleaseCount);
        }

        [Test]
        public void ResourceDebugSource_ExportsSnapshotAsFrameworkDebugReport()
        {
            var provider = new MemoryResourceProvider().Register("demo/text", "hello");
            ResourceManager manager = CreateManager(provider, Entry("demo.text.hello", ResourceTypeIds.String, "demo/text"));
            ResourceHandle<string> handle = manager.Load<string>(new ResourceKey("demo.text.hello", ResourceTypeIds.String)).Value;

            var source = new ResourceDebugSource(manager);
            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();
            string text = FrameworkDebugReportExporter.ExportText(snapshot);

            StringAssert.Contains("source: Resources", text);
            StringAssert.Contains("- Summary", text);
            StringAssert.Contains("loaded: 1", text);
            StringAssert.Contains("totalRefCount: 1", text);
            StringAssert.Contains("- Entry Origins", text);
            StringAssert.Contains("demo.text.hello", text);

            manager.Release(handle);
        }

        [Test]
        public void ResourceCatalogValidator_DetectsProviderUnsafeAddressAndMissingDependency()
        {
            var catalog = new ResourceCatalog(
                "bad",
                "pkg.bad",
                new[]
                {
                    new ResourceCatalogEntry(
                        "demo.prefab.hero",
                        "TestAsset",
                        "missingProvider",
                        "../hero",
                        dependencies: new[] { new ResourceKey("demo.texture.missing", ResourceTypeIds.Texture2D) })
                });

            ResourceCatalogValidationReport report = ResourceCatalogValidator.Validate(catalog, new[] { "memory" });

            Assert.IsTrue(report.HasErrors);
            AssertIssue(report, "ProviderMissing");
            AssertIssue(report, "UnsafeAddress");
            AssertIssue(report, "DependencyMissing");
        }

        [Test]
        public void ResourceCatalogValidator_DetectsDependencyCycle()
        {
            var catalog = new ResourceCatalog(
                "cycle",
                "pkg.cycle",
                new[]
                {
                    Entry(
                        "demo.text.a",
                        ResourceTypeIds.String,
                        "a",
                        dependencies: new[] { new ResourceKey("demo.text.b", ResourceTypeIds.String) }),
                    Entry(
                        "demo.text.b",
                        ResourceTypeIds.String,
                        "b",
                        dependencies: new[] { new ResourceKey("demo.text.a", ResourceTypeIds.String) })
                });

            ResourceCatalogValidationReport report = ResourceCatalogValidator.Validate(catalog, new[] { "memory" });

            Assert.IsTrue(report.HasErrors);
            AssertIssue(report, "DependencyCycle");
        }

        private static ResourceManager CreateManager(MemoryResourceProvider provider, params ResourceCatalogEntry[] entries)
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog("demo", string.Empty, entries));
            return manager;
        }

        private static ResourceCatalogEntry Entry(
            string id,
            string typeId,
            string address,
            string variant = "",
            IEnumerable<ResourceKey> dependencies = null,
            bool allowOverride = false)
        {
            return new ResourceCatalogEntry(
                id,
                typeId,
                "memory",
                address,
                variant: variant,
                dependencies: dependencies,
                allowOverride: allowOverride);
        }

        private static void AssertIssue(ResourceCatalogValidationReport report, string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                    return;
            }

            Assert.Fail("Expected resource catalog validation issue: " + code);
        }

        private sealed class TestAsset
        {
            public TestAsset(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }
    }
}
