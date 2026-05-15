using System.Threading;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Resources
{
    public class ResourcePreloadServiceTests
    {
        [Test]
        public void PreloadAsync_WithExplicitKeys_LoadsAndReleaseGroupIsIdempotent()
        {
            var provider = new MemoryResourceProvider()
                .Register("demo/a", "A")
                .Register("demo/b", "B");
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.a", "demo/a"),
                Entry("demo.text.b", "demo/b"));
            var service = new ResourcePreloadService(manager);

            IResourceOperation<ResourcePreloadResult> operation = service.PreloadAsync(new ResourcePreloadPlan(
                "explicit",
                explicitKeys: new[]
                {
                    new ResourceKey("demo.text.a", ResourceTypeIds.String),
                    new ResourceKey("demo.text.b", ResourceTypeIds.String)
                }));

            Assert.IsTrue(operation.Result.Success, operation.Result.Error.Message);
            ResourcePreloadResult result = operation.Result.Value;
            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.RequestedCount);
            Assert.AreEqual(2, result.LoadedCount);
            Assert.AreEqual(2, manager.CreateDebugSnapshot().LoadedCount);

            service.ReleaseGroup(result.Handle);
            service.ReleaseGroup(result.Handle);

            Assert.IsTrue(result.Handle.IsReleased);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(2, provider.ReleaseCount);
        }

        [Test]
        public void PreloadAsync_WithLabels_LoadsMatchingCatalogEntries()
        {
            var provider = new MemoryResourceProvider()
                .Register("demo/a", "A")
                .Register("demo/b", "B")
                .Register("demo/c", "C");
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.a", "demo/a", labels: new[] { "warmup.combat" }),
                Entry("demo.text.b", "demo/b", labels: new[] { "warmup.combat", "ui" }),
                Entry("demo.text.c", "demo/c", labels: new[] { "other" }));
            var service = new ResourcePreloadService(manager);

            ResourcePreloadResult result = service.PreloadAsync(new ResourcePreloadPlan(
                "combat",
                explicitKeys: new[] { new ResourceKey("demo.text.a", ResourceTypeIds.String) },
                labels: new[] { "warmup.combat" })).Result.Value;

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.RequestedCount);
            Assert.AreEqual(2, result.LoadedCount);
            Assert.AreEqual(2, manager.CreateDebugSnapshot().LoadedCount);

            service.ReleaseGroup(result.Handle);
        }

        [Test]
        public void PreloadAsync_WithLabelVariants_UsesVariantProfileAndRetainsReleasedGroup()
        {
            var provider = new MemoryResourceProvider()
                .Register("demo/title/default", "Default Title")
                .Register("demo/title/high", "High Title");
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.title", "demo/title/default", labels: new[] { "warmup.demo.unit_test" }),
                Entry("demo.text.title", "demo/title/high", "pc.high", labels: new[] { "warmup.demo.unit_test" }));
            manager
                .SetVariantProfile(new ResourceVariantProfile("pc.high", new[] { string.Empty }))
                .SetRetainPolicy(ResourceRetainPolicy.Timed(frameCount: 2));
            var service = new ResourcePreloadService(manager);

            ResourcePreloadResult result = service.PreloadAsync(new ResourcePreloadPlan(
                "unit_test",
                labels: new[] { "warmup.demo.unit_test" })).Result.Value;

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.RequestedCount);
            Assert.AreEqual(1, result.LoadedCount);
            Assert.AreEqual("High Title", result.Handle.Handles[0].Value);
            Assert.AreEqual("pc.high", result.Handle.Handles[0].Key.Variant);
            Assert.AreEqual(1, provider.LoadCount);

            service.ReleaseGroup(result.Handle);
            ResourceDebugSnapshot retained = manager.CreateDebugSnapshot();
            Assert.AreEqual(1, retained.LoadedCount);
            Assert.AreEqual(1, retained.RetainedCount);
            Assert.AreEqual(1, retained.EvictableCount);
            Assert.AreEqual(0, retained.TotalRefCount);
            Assert.AreEqual(0, provider.ReleaseCount);

            ResourcePreloadResult reused = service.PreloadAsync(new ResourcePreloadPlan(
                "unit_test",
                labels: new[] { "warmup.demo.unit_test" })).Result.Value;

            Assert.IsTrue(reused.Success);
            Assert.AreEqual(1, reused.LoadedCount);
            Assert.AreEqual(1, provider.LoadCount);

            service.ReleaseGroup(reused.Handle);
            Assert.AreEqual(0, manager.AdvanceRetainFrames(1));
            Assert.AreEqual(1, manager.CreateDebugSnapshot().RetainedCount);
            Assert.AreEqual(1, manager.AdvanceRetainFrames(1));
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(1, provider.ReleaseCount);
        }

        [Test]
        public void PreloadAsync_WhenResourceMissing_CollectsErrorAndKeepsLoadedHandles()
        {
            var provider = new MemoryResourceProvider().Register("demo/a", "A");
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.a", "demo/a"),
                Entry("demo.text.missing", "demo/missing"));
            var service = new ResourcePreloadService(manager);

            ResourcePreloadResult result = service.PreloadAsync(new ResourcePreloadPlan(
                "mixed",
                explicitKeys: new[]
                {
                    new ResourceKey("demo.text.a", ResourceTypeIds.String),
                    new ResourceKey("demo.text.missing", ResourceTypeIds.String)
                })).Result.Value;

            Assert.IsFalse(result.Success);
            Assert.AreEqual(2, result.RequestedCount);
            Assert.AreEqual(1, result.LoadedCount);
            Assert.AreEqual(1, result.FailedCount);
            Assert.AreEqual(ResourceErrorCode.NotFound, result.Errors[0].Code);
            Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);

            service.ReleaseGroup(result.Handle);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void PreloadAsync_WhenFailFast_StopsAfterFirstFailure()
        {
            var provider = new MemoryResourceProvider().Register("demo/after", "After");
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.missing", "demo/missing"),
                Entry("demo.text.after", "demo/after"));
            var service = new ResourcePreloadService(manager);

            ResourcePreloadResult result = service.PreloadAsync(new ResourcePreloadPlan(
                "fail-fast",
                explicitKeys: new[]
                {
                    new ResourceKey("demo.text.missing", ResourceTypeIds.String),
                    new ResourceKey("demo.text.after", ResourceTypeIds.String)
                },
                failFast: true)).Result.Value;

            Assert.IsFalse(result.Success);
            Assert.AreEqual(2, result.RequestedCount);
            Assert.AreEqual(0, result.LoadedCount);
            Assert.AreEqual(1, result.FailedCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void PreloadAsync_WhenCancelledBeforeStart_ReturnsCancelledAndDoesNotLoad()
        {
            var provider = new MemoryResourceProvider().Register("demo/a", "A");
            ResourceManager manager = CreateManager(provider, Entry("demo.text.a", "demo/a"));
            var service = new ResourcePreloadService(manager);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            IResourceOperation<ResourcePreloadResult> operation = service.PreloadAsync(new ResourcePreloadPlan(
                "cancelled",
                explicitKeys: new[] { new ResourceKey("demo.text.a", ResourceTypeIds.String) }),
                cts.Token);

            Assert.IsFalse(operation.Result.Success);
            Assert.AreEqual(ResourceErrorCode.Cancelled, operation.Result.Error.Code);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(0, provider.ReleaseCount);
        }

        private static ResourceManager CreateManager(MemoryResourceProvider provider, params ResourceCatalogEntry[] entries)
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog("demo", string.Empty, entries));
            return manager;
        }

        private static ResourceCatalogEntry Entry(string id, string address, string variant = "", string[] labels = null)
        {
            return new ResourceCatalogEntry(
                id,
                ResourceTypeIds.String,
                "memory",
                address,
                variant: variant,
                labels: labels);
        }
    }
}
