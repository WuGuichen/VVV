using System;
using System.Collections.Generic;
using System.Threading;
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
        public void AddCatalog_WhenLaterEntryConflicts_DoesNotPartiallyRegisterEarlierEntries()
        {
            var provider = new MemoryResourceProvider()
                .Register("demo/existing", "existing")
                .Register("demo/new", "new")
                .Register("demo/conflict", "conflict");
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.existing", ResourceTypeIds.String, "demo/existing"));

            Assert.Throws<ResourceCatalogException>(() => manager.AddCatalog(new ResourceCatalog(
                "conflicting",
                string.Empty,
                new[]
                {
                    Entry("demo.text.new", ResourceTypeIds.String, "demo/new"),
                    Entry("demo.text.existing", ResourceTypeIds.String, "demo/conflict")
                })));

            Assert.IsFalse(manager.Contains(new ResourceKey("demo.text.new", ResourceTypeIds.String)));
            Assert.IsTrue(manager.Contains(new ResourceKey("demo.text.existing", ResourceTypeIds.String)));
            Assert.AreEqual(1, manager.CreateDebugSnapshot().EntryCount);
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

        [TestCase(ResourceRetainMode.None, false)]
        [TestCase(ResourceRetainMode.None, true)]
        [TestCase(ResourceRetainMode.Timed, false)]
        [TestCase(ResourceRetainMode.Timed, true)]
        [TestCase(ResourceRetainMode.KeepAlive, false)]
        [TestCase(ResourceRetainMode.KeepAlive, true)]
        [TestCase(ResourceRetainMode.Budgeted, false)]
        [TestCase(ResourceRetainMode.Budgeted, true)]
        public void Load_ProviderTypeMismatch_RollsBackWithoutRetaining(ResourceRetainMode mode, bool nullValue)
        {
            var dependency = new ResourceKey("demo.dep", ResourceTypeIds.String);
            var key = new ResourceKey("demo.bad", ResourceTypeIds.String);
            var provider = new MemoryResourceProvider()
                .Register("dep", "dependency").Register("bad", nullValue ? null : (object)123);
            ResourceManager manager = CreateManager(provider,
                Entry("demo.dep", ResourceTypeIds.String, "dep"),
                Entry("demo.bad", ResourceTypeIds.String, "bad", dependencies: new[] { dependency }));
            manager.SetRetainPolicy(new ResourceRetainPolicy(mode, 10f, 10, 1024));
            for (int i = 0; i < 2; i++)
            {
                ResourceLoadResult<ResourceHandle<string>> result = manager.Load<string>(key);
                Assert.IsFalse(result.Success);
                Assert.AreEqual(ResourceErrorCode.TypeMismatch, result.Error.Code);
                Assert.IsNull(result.Value);
                ResourceDebugSnapshot snapshot = manager.CreateDebugSnapshot();
                Assert.AreEqual(0, snapshot.LoadedCount);
                Assert.AreEqual(0, snapshot.TotalRefCount);
                Assert.AreEqual(0, snapshot.RetainedCount);
            }
            Assert.AreEqual(4, provider.ReleaseCount, "Each failure releases its resource and dependency.");
            provider.Register("bad", "repaired");
            ResourceLoadResult<ResourceHandle<string>> repaired = manager.Load<string>(key);
            Assert.IsTrue(repaired.Success, repaired.Error.Message);
            Assert.AreEqual("repaired", repaired.Value.Value);
            manager.Release(repaired.Value);
            manager.ReleaseAllLoaded();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Load_ProviderTypeMismatch_PreservesExistingOwner(bool preloadOwner)
        {
            var dependency = new ResourceKey("demo.dep", ResourceTypeIds.String);
            var key = new ResourceKey("demo.bad", ResourceTypeIds.String);
            var provider = new MemoryResourceProvider().Register("dep", "dependency").Register("bad", 123);
            ResourceManager manager = CreateManager(provider,
                Entry("demo.dep", ResourceTypeIds.String, "dep"),
                Entry("demo.bad", ResourceTypeIds.String, "bad", dependencies: new[] { dependency }));
            var existing = manager.LoadAsync<object>(preloadOwner ? key : dependency).Result;
            Assert.IsTrue(existing.Success, existing.Error.Message);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(ResourceErrorCode.TypeMismatch, manager.Load<string>(key).Error.Code);
                ResourceDebugSnapshot snapshot = manager.CreateDebugSnapshot();
                Assert.AreEqual(preloadOwner ? 2 : 1, snapshot.LoadedCount);
                Assert.AreEqual(preloadOwner ? 2 : 1, snapshot.TotalRefCount);
                Assert.AreEqual(preloadOwner ? (object)123 : "dependency", existing.Value.Value);
            }
            manager.Release(existing.Value);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().TotalRefCount);
        }

        [Test]
        public void LoadAsync_CompletedCancelPreservesHandleAndOwnership(
            [Values(ResourceRetainMode.None, ResourceRetainMode.KeepAlive)] ResourceRetainMode mode,
            [Values(false, true)] bool readBeforeCancel,
            [Values(false, true)] bool anotherOwner)
        {
            var dependency = new ResourceKey("demo.dep", ResourceTypeIds.String);
            var key = new ResourceKey("demo.asset", ResourceTypeIds.String);
            var provider = new MemoryResourceProvider().Register("dep", "dependency").Register("asset", "valid");
            ResourceManager manager = CreateManager(provider,
                Entry("demo.dep", ResourceTypeIds.String, "dep"),
                Entry("demo.asset", ResourceTypeIds.String, "asset", dependencies: new[] { dependency }));
            manager.SetRetainPolicy(new ResourceRetainPolicy(mode));
            ResourceHandle<string> existing = anotherOwner ? manager.Load<string>(key).Value : null;
            IResourceOperation<ResourceHandle<string>> operation = manager.LoadAsync<string>(key);
            Assert.IsTrue(operation.IsDone);
            Assert.IsFalse(operation.IsCancelled);
            ResourceHandle<string> original = readBeforeCancel ? operation.Result.Value : null;

            operation.Cancel();
            operation.Cancel();

            Assert.IsTrue(operation.IsDone);
            Assert.IsFalse(operation.IsCancelled);
            Assert.AreEqual(1f, operation.Progress);
            Assert.IsTrue(operation.Result.Success, operation.Result.Error.Message);
            ResourceHandle<string> handle = operation.Result.Value;
            Assert.IsNotNull(handle);
            if (readBeforeCancel) Assert.AreSame(original, handle);
            Assert.AreEqual("valid", handle.Value);
            Assert.IsFalse(handle.IsReleased);
            Assert.AreEqual(anotherOwner ? 3 : 2, manager.CreateDebugSnapshot().TotalRefCount);
            Assert.AreEqual(0, provider.ReleaseCount, "Terminal Cancel must not release a consumer's handle.");
            if (existing != null)
            {
                Assert.AreEqual("valid", existing.Value);
                Assert.IsFalse(existing.IsReleased);
                manager.Release(existing);
                Assert.AreEqual(2, manager.CreateDebugSnapshot().TotalRefCount);
                Assert.AreEqual(0, provider.ReleaseCount);
            }
            manager.Release(handle);
            Assert.IsTrue(handle.IsReleased);
            operation.Cancel();
            Assert.AreSame(handle, operation.Result.Value);
            if (mode == ResourceRetainMode.KeepAlive)
            {
                Assert.AreEqual(1, manager.CreateDebugSnapshot().RetainedCount);
                Assert.AreEqual(1, manager.EvictRetainedResources());
            }
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().TotalRefCount);
            Assert.AreEqual(2, provider.ReleaseCount);
        }

        [TestCase(ResourceErrorCode.NotFound)]
        [TestCase(ResourceErrorCode.TypeMismatch)]
        [TestCase(ResourceErrorCode.Cancelled)]
        public void ImmediateOperation_TerminalFailureRemainsStableAfterCancel(ResourceErrorCode code)
        {
            var key = new ResourceKey("demo.missing", ResourceTypeIds.String);
            var error = new ResourceError(code, key, "memory", "original failure", "original address");
            var operation = new ImmediateResourceOperation<object>(ResourceLoadResult<object>.Failed(error));
            bool cancelled = code == ResourceErrorCode.Cancelled;
            Assert.AreEqual(cancelled, operation.IsCancelled);
            operation.Cancel();
            operation.Cancel();
            Assert.IsTrue(operation.IsDone);
            Assert.AreEqual(1f, operation.Progress);
            Assert.IsFalse(operation.Result.Success);
            Assert.IsNull(operation.Result.Value);
            Assert.AreEqual(cancelled, operation.IsCancelled);
            Assert.AreEqual(error, operation.Result.Error);
        }

        [Test]
        public void LoadAsync_PreCancelledTokenPreservesCancellationWithoutAcquiringReferences()
        {
            var provider = new MemoryResourceProvider().Register("asset", "valid");
            var manager = CreateManager(provider, Entry("demo.asset", ResourceTypeIds.String, "asset"));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var operation = manager.LoadAsync<string>(new ResourceKey("demo.asset", ResourceTypeIds.String), cancellation.Token);
            Assert.IsTrue(operation.IsDone);
            Assert.IsTrue(operation.IsCancelled);
            ResourceError original = operation.Result.Error;
            Assert.AreEqual(ResourceErrorCode.Cancelled, original.Code);
            operation.Cancel();
            operation.Cancel();
            Assert.AreEqual(original, operation.Result.Error);
            Assert.IsNull(operation.Result.Value);
            Assert.AreEqual(0, provider.LoadCount);
            Assert.AreEqual(0, provider.ReleaseCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().TotalRefCount);
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
        public void ReleaseAllLoaded_ReleasesOutstandingResourcesAndDependencies()
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

            manager.Load<TestAsset>(new ResourceKey("demo.prefab.hero", "TestAsset"));

            Assert.AreEqual(2, manager.CreateDebugSnapshot().LoadedCount);

            int released = manager.ReleaseAllLoaded("test");

            Assert.AreEqual(2, released);
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
        public void Release_WithBudgetedRetainPolicy_EvictsByPriorityThenRetainAge()
        {
            var provider = new MemoryResourceProvider()
                .Register("demo/low", "low")
                .Register("demo/old", "old")
                .Register("demo/new", "new")
                .Register("demo/newer", "newer");
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.low", ResourceTypeIds.String, "demo/low", size: 40, providerData: RetainPriority(0)),
                Entry("demo.text.old", ResourceTypeIds.String, "demo/old", size: 40, providerData: RetainPriority(5)),
                Entry("demo.text.new", ResourceTypeIds.String, "demo/new", size: 40, providerData: RetainPriority(5)),
                Entry("demo.text.newer", ResourceTypeIds.String, "demo/newer", size: 40, providerData: RetainPriority(5)));
            manager.SetRetainPolicy(ResourceRetainPolicy.Budgeted(80));

            ResourceHandle<string> low = manager.Load<string>(new ResourceKey("demo.text.low", ResourceTypeIds.String)).Value;
            ResourceHandle<string> old = manager.Load<string>(new ResourceKey("demo.text.old", ResourceTypeIds.String)).Value;
            ResourceHandle<string> newer = manager.Load<string>(new ResourceKey("demo.text.newer", ResourceTypeIds.String)).Value;
            ResourceHandle<string> @new = manager.Load<string>(new ResourceKey("demo.text.new", ResourceTypeIds.String)).Value;

            manager.Release(low);
            manager.Release(old);
            manager.Release(@new);
            manager.Release(newer);

            ResourceDebugSnapshot snapshot = manager.CreateDebugSnapshot();
            Assert.AreEqual(2, provider.ReleaseCount);
            Assert.AreEqual(2, snapshot.LoadedCount);
            Assert.AreEqual(2, snapshot.RetainedCount);
            Assert.AreEqual(80, snapshot.RetainedBytes);
            Assert.AreEqual(80, snapshot.RetainBudgetBytes);
            Assert.AreEqual(0, snapshot.RetainBudgetOverageBytes);
            Assert.IsFalse(snapshot.RetainBudgetExceeded);
            Assert.AreEqual(2, snapshot.RecentEvictions.Count);
            Assert.AreEqual(new ResourceKey("demo.text.low", ResourceTypeIds.String), snapshot.RecentEvictions[0].Key);
            Assert.AreEqual("budget", snapshot.RecentEvictions[0].Reason);
            Assert.AreEqual(new ResourceKey("demo.text.old", ResourceTypeIds.String), snapshot.RecentEvictions[1].Key);
            Assert.AreEqual("budget", snapshot.RecentEvictions[1].Reason);
        }

        [Test]
        public void Release_WithBudgetedRetainPolicy_ReportsBudgetDiagnosticsAndEvictionReason()
        {
            var provider = new MemoryResourceProvider()
                .Register("demo/pinned", "pinned")
                .Register("demo/transient", "transient");
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.pinned", ResourceTypeIds.String, "demo/pinned", size: 90),
                Entry("demo.text.transient", ResourceTypeIds.String, "demo/transient", size: 25));
            manager.SetRetainPolicy(ResourceRetainPolicy.KeepAlive);

            ResourceHandle<string> pinned = manager.Load<string>(new ResourceKey("demo.text.pinned", ResourceTypeIds.String)).Value;
            manager.Release(pinned);
            manager.SetRetainPolicy(ResourceRetainPolicy.Budgeted(50));
            ResourceHandle<string> transient = manager.Load<string>(new ResourceKey("demo.text.transient", ResourceTypeIds.String)).Value;
            manager.Release(transient);

            ResourceDebugSnapshot snapshot = manager.CreateDebugSnapshot();
            Assert.AreEqual(1, snapshot.RetainedCount);
            Assert.AreEqual(0, snapshot.EvictableCount);
            Assert.AreEqual(1, snapshot.PinnedCount);
            Assert.AreEqual(90, snapshot.RetainedBytes);
            Assert.AreEqual(50, snapshot.RetainBudgetBytes);
            Assert.AreEqual(40, snapshot.RetainBudgetOverageBytes);
            Assert.IsTrue(snapshot.RetainBudgetExceeded);
            Assert.AreEqual(1, provider.ReleaseCount);
            Assert.AreEqual(1, snapshot.RecentEvictions.Count);
            Assert.AreEqual(new ResourceKey("demo.text.transient", ResourceTypeIds.String), snapshot.RecentEvictions[0].Key);
            Assert.AreEqual("budget", snapshot.RecentEvictions[0].Reason);
        }

        [Test]
        public void Release_WithBudgetedRetainPolicy_CountsSharedDependencyClosureOnce()
        {
            var provider = new MemoryResourceProvider()
                .Register("demo/shared", "shared")
                .Register("demo/first", "first")
                .Register("demo/second", "second")
                .Register("demo/transient", "transient");
            var sharedDependency = new ResourceKey("demo.text.shared", ResourceTypeIds.String);
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.shared", ResourceTypeIds.String, "demo/shared", size: 90),
                Entry(
                    "demo.text.first",
                    ResourceTypeIds.String,
                    "demo/first",
                    dependencies: new[] { sharedDependency },
                    size: 10,
                    providerData: RetainPriority(10)),
                Entry(
                    "demo.text.second",
                    ResourceTypeIds.String,
                    "demo/second",
                    dependencies: new[] { sharedDependency },
                    size: 10,
                    providerData: RetainPriority(10)),
                Entry("demo.text.transient", ResourceTypeIds.String, "demo/transient", size: 1));
            manager.SetRetainPolicy(ResourceRetainPolicy.Budgeted(110));

            ResourceHandle<string> first = manager.Load<string>(new ResourceKey("demo.text.first", ResourceTypeIds.String)).Value;
            ResourceHandle<string> second = manager.Load<string>(new ResourceKey("demo.text.second", ResourceTypeIds.String)).Value;
            ResourceHandle<string> transient = manager.Load<string>(new ResourceKey("demo.text.transient", ResourceTypeIds.String)).Value;

            manager.Release(first);
            manager.Release(second);

            ResourceDebugSnapshot shared = manager.CreateDebugSnapshot();
            Assert.AreEqual(4, shared.LoadedCount);
            Assert.AreEqual(2, shared.RetainedCount);
            Assert.AreEqual(110, shared.RetainedBytes);
            Assert.AreEqual(0, provider.ReleaseCount);

            manager.Release(transient);

            ResourceDebugSnapshot snapshot = manager.CreateDebugSnapshot();
            Assert.AreEqual(3, snapshot.LoadedCount);
            Assert.AreEqual(2, snapshot.RetainedCount);
            Assert.AreEqual(110, snapshot.RetainedBytes);
            Assert.AreEqual(110, snapshot.RetainBudgetBytes);
            Assert.AreEqual(0, snapshot.RetainBudgetOverageBytes);
            Assert.IsFalse(snapshot.RetainBudgetExceeded);
            Assert.AreEqual(1, provider.ReleaseCount);
            Assert.AreEqual(1, snapshot.RecentEvictions.Count);
            Assert.AreEqual(new ResourceKey("demo.text.transient", ResourceTypeIds.String), snapshot.RecentEvictions[0].Key);
            Assert.AreEqual("budget", snapshot.RecentEvictions[0].Reason);
        }

        [Test]
        public void Release_WithBudgetedRetainPolicy_ReusesRetainedRecordWhenUnderBudget()
        {
            var provider = new MemoryResourceProvider().Register("demo/reused", "reused");
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.reused", ResourceTypeIds.String, "demo/reused", size: 25));
            manager.SetRetainPolicy(ResourceRetainPolicy.Budgeted(50));

            ResourceHandle<string> first = manager.Load<string>(new ResourceKey("demo.text.reused", ResourceTypeIds.String)).Value;
            manager.Release(first);
            ResourceHandle<string> second = manager.Load<string>(new ResourceKey("demo.text.reused", ResourceTypeIds.String)).Value;

            Assert.AreEqual("reused", second.Value);
            Assert.AreEqual(1, provider.LoadCount);
            Assert.AreEqual(0, provider.ReleaseCount);
            Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);

            manager.Release(second);
        }

        [Test]
        public void Release_WithBudgetedRetainPolicy_DoesNotEstimateBytesWhenCatalogSizeIsMissing()
        {
            var provider = new MemoryResourceProvider()
                .Register("demo/a", "A")
                .Register("demo/b", "B");
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.a", ResourceTypeIds.String, "demo/a"),
                Entry("demo.text.b", ResourceTypeIds.String, "demo/b"));
            manager.SetRetainPolicy(ResourceRetainPolicy.Budgeted(0));

            ResourceHandle<string> first = manager.Load<string>(new ResourceKey("demo.text.a", ResourceTypeIds.String)).Value;
            ResourceHandle<string> second = manager.Load<string>(new ResourceKey("demo.text.b", ResourceTypeIds.String)).Value;
            manager.Release(first);
            manager.Release(second);

            ResourceDebugSnapshot snapshot = manager.CreateDebugSnapshot();
            Assert.AreEqual(2, snapshot.LoadedCount);
            Assert.AreEqual(2, snapshot.RetainedCount);
            Assert.AreEqual(0, snapshot.RetainedBytes);
            Assert.AreEqual(0, snapshot.RetainBudgetBytes);
            Assert.AreEqual(0, snapshot.RetainBudgetOverageBytes);
            Assert.IsFalse(snapshot.RetainBudgetExceeded);
            Assert.AreEqual(0, provider.ReleaseCount);
        }

        [Test]
        public void SetRetainPolicy_WhenSwitchingKeepAliveToBudgeted_DoesNotEvictPinnedRecords()
        {
            var provider = new MemoryResourceProvider().Register("demo/pinned", "pinned");
            ResourceManager manager = CreateManager(provider, Entry("demo.text.pinned", ResourceTypeIds.String, "demo/pinned", size: 75));
            manager.SetRetainPolicy(ResourceRetainPolicy.KeepAlive);

            ResourceHandle<string> pinned = manager.Load<string>(new ResourceKey("demo.text.pinned", ResourceTypeIds.String)).Value;
            manager.Release(pinned);
            manager.SetRetainPolicy(ResourceRetainPolicy.Budgeted(0));

            ResourceDebugSnapshot budgeted = manager.CreateDebugSnapshot();
            Assert.AreEqual(1, budgeted.LoadedCount);
            Assert.AreEqual(1, budgeted.RetainedCount);
            Assert.AreEqual(1, budgeted.PinnedCount);
            Assert.AreEqual(0, budgeted.EvictableCount);
            Assert.AreEqual(75, budgeted.RetainedBytes);
            Assert.AreEqual(0, budgeted.RetainBudgetBytes);
            Assert.AreEqual(75, budgeted.RetainBudgetOverageBytes);
            Assert.IsTrue(budgeted.RetainBudgetExceeded);
            Assert.AreEqual(0, provider.ReleaseCount);

            Assert.AreEqual(1, manager.EvictRetainedResources());
            ResourceDebugSnapshot evicted = manager.CreateDebugSnapshot();
            Assert.AreEqual(0, evicted.LoadedCount);
            Assert.AreEqual(0, evicted.RetainedCount);
            Assert.AreEqual(1, provider.ReleaseCount);
            Assert.AreEqual(1, evicted.RecentEvictions.Count);
            Assert.AreEqual(new ResourceKey("demo.text.pinned", ResourceTypeIds.String), evicted.RecentEvictions[0].Key);
            Assert.AreEqual("manual", evicted.RecentEvictions[0].Reason);
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
            StringAssert.Contains("retainedBytes: 0", text);
            StringAssert.Contains("retainBudgetExceeded: false", text);
            StringAssert.Contains("- Recent Evictions", text);

            manager.Release(handle);
        }

        [Test]
        public void ResourceDebugSource_ExportsBudgetAndRecentEvictionDiagnostics()
        {
            var provider = new MemoryResourceProvider()
                .Register("demo/a", "A")
                .Register("demo/b", "B");
            ResourceManager manager = CreateManager(
                provider,
                Entry("demo.text.a", ResourceTypeIds.String, "demo/a", size: 40),
                Entry("demo.text.b", ResourceTypeIds.String, "demo/b", size: 40));
            manager.SetRetainPolicy(ResourceRetainPolicy.Budgeted(40));

            ResourceHandle<string> first = manager.Load<string>(new ResourceKey("demo.text.a", ResourceTypeIds.String)).Value;
            ResourceHandle<string> second = manager.Load<string>(new ResourceKey("demo.text.b", ResourceTypeIds.String)).Value;
            manager.Release(first);
            manager.Release(second);

            var source = new ResourceDebugSource(manager);
            string text = FrameworkDebugReportExporter.ExportText(source.CreateSnapshot());

            StringAssert.Contains("retainedBytes: 40", text);
            StringAssert.Contains("retainBudgetBytes: 40", text);
            StringAssert.Contains("retainBudgetExceeded: false", text);
            StringAssert.Contains("- Recent Evictions", text);
            StringAssert.Contains("budget key=demo.text.a:String provider=memory", text);
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

        [Test]
        public void TryGetEntry_ReturnsResolvedCatalogEntryWithoutLoading()
        {
            var provider = new MemoryResourceProvider().Register("demo/text", "hello");
            ResourceManager manager = CreateManager(provider, Entry("demo.text.hello", ResourceTypeIds.String, "demo/text"));

            bool found = manager.TryGetEntry(new ResourceKey("demo.text.hello", ResourceTypeIds.String), out ResourceCatalogEntry entry);

            Assert.IsTrue(found);
            Assert.AreEqual("demo.text.hello", entry.Id);
            Assert.AreEqual("demo/text", entry.Address);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void SealRuntimeConfiguration_FreezesTopologyAndReturnsStableOpaqueIdentity()
        {
            var provider = new MemoryResourceProvider().Register("demo/text", "hello");
            ResourceManager manager = CreateManager(provider, Entry("demo.text.hello", ResourceTypeIds.String, "demo/text"));
            manager.SetVariantProfile(new ResourceVariantProfile("pc.high", new[] { string.Empty }));

            ResourceRuntimeConfigurationIdentity first = manager.SealRuntimeConfiguration();
            ResourceRuntimeConfigurationIdentity second = manager.SealRuntimeConfiguration();

            Assert.AreSame(first, second);
            Assert.IsTrue(manager.IsRuntimeConfigurationSealed);
            Assert.IsTrue(manager.TryGetRuntimeConfigurationIdentity(out ResourceRuntimeConfigurationIdentity read));
            Assert.AreSame(first, read);
            Assert.IsNotEmpty(first.DiagnosticFingerprint);
            Assert.AreEqual(1, first.ProviderCount);
            Assert.AreEqual(1, first.CatalogCount);
            Assert.AreEqual(1, first.ResourceCount);
            Assert.AreEqual("pc.high", first.ActiveVariant);

            Assert.Throws<ResourceRuntimeConfigurationSealedException>(() => manager.RegisterProvider(new MemoryResourceProvider()));
            Assert.Throws<ResourceRuntimeConfigurationSealedException>(() => manager.AddCatalog(new ResourceCatalog("other", string.Empty, Array.Empty<ResourceCatalogEntry>())));
            Assert.Throws<ResourceRuntimeConfigurationSealedException>(() => manager.SetVariantProfile(ResourceVariantProfile.Empty));

            Assert.AreEqual(1, manager.CreateDebugSnapshot().ProviderCount);
            Assert.AreEqual(1, manager.CreateDebugSnapshot().CatalogCount);
        }

        [Test]
        public void RuntimeConfigurationIdentity_PublicReadsAllocateZeroAfterSeal()
        {
            var provider = new MemoryResourceProvider().Register("demo/text", "hello");
            ResourceManager manager = CreateManager(provider, Entry("demo.text.hello", ResourceTypeIds.String, "demo/text"));
            ResourceRuntimeConfigurationIdentity identity = manager.SealRuntimeConfiguration();

            _ = identity.DiagnosticFingerprint;
            _ = manager.TryGetRuntimeConfigurationIdentity(out _);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                _ = identity.DiagnosticFingerprint;
                _ = identity.ProviderCount;
                _ = identity.CatalogCount;
                _ = identity.ResourceCount;
                _ = identity.ActiveVariant;
                _ = manager.IsRuntimeConfigurationSealed;
                _ = manager.TryGetRuntimeConfigurationIdentity(out _);
            }

            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Test]
        public void RuntimeConfigurationTopology_CollectionsCannotDriftAfterSeal()
        {
            var provider = new MemoryResourceProvider().Register("demo/text", "hello");
            var entry = new ResourceCatalogEntry(
                "demo.text.hello",
                ResourceTypeIds.String,
                provider.ProviderId,
                "demo/text",
                dependencies: new[] { new ResourceKey("demo.text.dependency", ResourceTypeIds.String) },
                labels: new[] { "warmup.demo" },
                providerData: new Dictionary<string, string> { { "channel", "stable" } });
            var catalog = new ResourceCatalog("demo", string.Empty, new[] { entry });
            var profile = new ResourceVariantProfile("pc.high", new[] { string.Empty });
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.SetVariantProfile(profile);
            manager.AddCatalog(catalog);
            ResourceRuntimeConfigurationIdentity identity = manager.SealRuntimeConfiguration();

            Assert.Throws<NotSupportedException>(() => ((IList<ResourceCatalogEntry>)catalog.Entries).Add(entry));
            Assert.Throws<NotSupportedException>(() => ((IList<ResourceKey>)entry.Dependencies).Add(default));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)entry.Labels).Add("drift"));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)entry.ProviderData)["drift"] = "true");
            Assert.Throws<NotSupportedException>(() => ((IList<string>)profile.FallbackVariants).Add("drift"));
            Assert.AreSame(identity, manager.SealRuntimeConfiguration());
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
            bool allowOverride = false,
            long size = 0,
            IReadOnlyDictionary<string, string> providerData = null)
        {
            return new ResourceCatalogEntry(
                id,
                typeId,
                "memory",
                address,
                variant: variant,
                dependencies: dependencies,
                size: size,
                allowOverride: allowOverride,
                providerData: providerData);
        }

        private static IReadOnlyDictionary<string, string> RetainPriority(int priority)
        {
            return new Dictionary<string, string>
            {
                { "retainPriority", priority.ToString() }
            };
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
