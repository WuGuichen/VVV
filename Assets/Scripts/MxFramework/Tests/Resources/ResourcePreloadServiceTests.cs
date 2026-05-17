using System;
using System.Collections.Generic;
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
        public void PreloadAsync_WhenFailFastWithConcurrentLoads_CancelsInFlightAndDoesNotStartMore()
        {
            ResourceKey first = Key("demo.text.fail_fast_a");
            ResourceKey second = Key("demo.text.fail_fast_b");
            ResourceKey third = Key("demo.text.fail_fast_c");
            var manager = new ManualAsyncResourceManager(first, second, third);
            var service = new ResourcePreloadService(manager);

            IResourceOperation<ResourcePreloadResult> operation = service.PreloadAsync(new ResourcePreloadPlan(
                "fail-fast-concurrent",
                explicitKeys: new[] { first, second, third },
                failFast: true,
                maxConcurrentLoads: 2));

            Assert.IsFalse(operation.IsDone);
            Assert.AreEqual(2, manager.StartedKeys.Count);

            manager.ObjectOperations[0].CompleteFailed();
            Assert.IsTrue(operation.IsDone);

            ResourcePreloadResult result = operation.Result.Value;
            Assert.IsFalse(result.Success);
            Assert.AreEqual(3, result.RequestedCount);
            Assert.AreEqual(0, result.LoadedCount);
            Assert.AreEqual(1, result.FailedCount);
            Assert.AreEqual(2, manager.StartedKeys.Count);
            Assert.IsTrue(manager.ObjectOperations[1].IsCancelled);
        }

        [Test]
        public void PreloadAsync_WithMaxConcurrentLoads_StartsOnlyConcurrencyWindowUntilCompletions()
        {
            ResourceKey first = Key("demo.text.async_a");
            ResourceKey second = Key("demo.text.async_b");
            ResourceKey third = Key("demo.text.async_c");
            ResourceKey fourth = Key("demo.text.async_d");
            var manager = new ManualAsyncResourceManager(first, second, third, fourth);
            var service = new ResourcePreloadService(manager);

            IResourceOperation<ResourcePreloadResult> operation = service.PreloadAsync(new ResourcePreloadPlan(
                "concurrency",
                explicitKeys: new[] { first, second, third, fourth },
                maxConcurrentLoads: 2));

            Assert.IsFalse(operation.IsDone);
            Assert.AreEqual(2, manager.StartedKeys.Count);
            Assert.AreEqual(first, manager.StartedKeys[0]);
            Assert.AreEqual(second, manager.StartedKeys[1]);

            manager.ObjectOperations[0].CompleteLoaded();
            Assert.AreEqual(0.25f, operation.Progress);
            Assert.AreEqual(3, manager.StartedKeys.Count);
            Assert.AreEqual(third, manager.StartedKeys[2]);

            manager.ObjectOperations[1].CompleteLoaded();
            Assert.AreEqual(0.5f, operation.Progress);
            Assert.AreEqual(4, manager.StartedKeys.Count);
            Assert.AreEqual(fourth, manager.StartedKeys[3]);

            manager.ObjectOperations[2].CompleteLoaded();
            manager.ObjectOperations[3].CompleteLoaded();

            Assert.IsTrue(operation.Result.Success, operation.Result.Error.Message);
            Assert.AreEqual(1f, operation.Progress);
            Assert.AreEqual(4, manager.StartedKeys.Count);
        }

        [Test]
        public void PreloadAsync_ProgressAdvancesFromZeroToPartialToComplete()
        {
            ResourceKey first = Key("demo.text.progress_a");
            ResourceKey second = Key("demo.text.progress_b");
            var manager = new ManualAsyncResourceManager(first, second);
            var service = new ResourcePreloadService(manager);

            IResourceOperation<ResourcePreloadResult> operation = service.PreloadAsync(new ResourcePreloadPlan(
                "progress",
                explicitKeys: new[] { first, second },
                maxConcurrentLoads: 1));

            Assert.AreEqual(0f, operation.Progress);

            manager.ObjectOperations[0].CompleteLoaded();
            Assert.AreEqual(0.5f, operation.Progress);

            manager.ObjectOperations[1].CompleteLoaded();
            Assert.IsTrue(operation.Result.Success, operation.Result.Error.Message);
            Assert.AreEqual(1f, operation.Progress);
        }

        [Test]
        public void PreloadAsync_WhenCancelledAfterPartialLoad_ReleasesLoadedHandlesAndReturnsCancelled()
        {
            ResourceKey first = Key("demo.text.cancel_a");
            ResourceKey second = Key("demo.text.cancel_b");
            var manager = new ManualAsyncResourceManager(first, second);
            var service = new ResourcePreloadService(manager);

            IResourceOperation<ResourcePreloadResult> operation = service.PreloadAsync(new ResourcePreloadPlan(
                "cancel",
                explicitKeys: new[] { first, second },
                maxConcurrentLoads: 1));

            manager.ObjectOperations[0].CompleteLoaded();
            Assert.AreEqual(0.5f, operation.Progress);
            Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);

            operation.Cancel();

            Assert.IsTrue(operation.IsDone);
            Assert.IsTrue(operation.IsCancelled);
            Assert.IsFalse(operation.Result.Success);
            Assert.AreEqual(ResourceErrorCode.Cancelled, operation.Result.Error.Code);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(1, manager.ReleaseCount);
            Assert.AreEqual(first, manager.ReleasedKeys[0]);
            Assert.IsTrue(manager.ObjectOperations[1].IsCancelled);
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

        private static ResourceKey Key(string id)
        {
            return new ResourceKey(id, ResourceTypeIds.String);
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

        private sealed class ManualAsyncResourceManager : IResourceManager, IResourceCatalogQuery
        {
            private readonly ResourceManager _inner;

            public ManualAsyncResourceManager(params ResourceKey[] keys)
            {
                var provider = new MemoryResourceProvider();
                var entries = new List<ResourceCatalogEntry>();

                for (int i = 0; i < keys.Length; i++)
                {
                    ResourceKey key = keys[i];
                    string address = key.Id;
                    provider.Register(address, address);
                    entries.Add(new ResourceCatalogEntry(
                        key.Id,
                        key.TypeId,
                        "memory",
                        address,
                        key.Variant,
                        key.PackageId));
                }

                _inner = new ResourceManager();
                _inner.RegisterProvider(provider);
                _inner.AddCatalog(new ResourceCatalog("manual", string.Empty, entries));
            }

            public List<ResourceKey> StartedKeys { get; } = new List<ResourceKey>();
            public List<ResourceKey> ReleasedKeys { get; } = new List<ResourceKey>();
            public List<ManualResourceOperation<ResourceHandle<object>>> ObjectOperations { get; }
                = new List<ManualResourceOperation<ResourceHandle<object>>>();
            public int ReleaseCount { get; private set; }

            public IResourceManager RegisterProvider(IResourceProvider provider)
            {
                _inner.RegisterProvider(provider);
                return this;
            }

            public IResourceManager AddCatalog(ResourceCatalog catalog)
            {
                _inner.AddCatalog(catalog);
                return this;
            }

            public bool Contains(ResourceKey key)
            {
                return _inner.Contains(key);
            }

            public ResourceLoadResult<ResourceHandle<T>> Load<T>(ResourceKey key)
            {
                return _inner.Load<T>(key);
            }

            public IResourceOperation<ResourceHandle<T>> LoadAsync<T>(
                ResourceKey key,
                CancellationToken cancellationToken = default)
            {
                StartedKeys.Add(key);

                if (cancellationToken.IsCancellationRequested)
                {
                    return new ImmediateResourceOperation<ResourceHandle<T>>(
                        ResourceLoadResult<ResourceHandle<T>>.Failed(new ResourceError(
                            ResourceErrorCode.Cancelled,
                            key,
                            string.Empty,
                            "Resource operation was cancelled.")));
                }

                var operation = new ManualResourceOperation<ResourceHandle<T>>(() => _inner.Load<T>(key), key);
                if (typeof(T) == typeof(object))
                    ObjectOperations.Add((ManualResourceOperation<ResourceHandle<object>>)(object)operation);

                return operation;
            }

            public void Release<T>(ResourceHandle<T> handle)
            {
                if (handle != null)
                {
                    ReleasedKeys.Add(handle.Key);
                    ReleaseCount++;
                }

                _inner.Release(handle);
            }

            public ResourceDebugSnapshot CreateDebugSnapshot()
            {
                return _inner.CreateDebugSnapshot();
            }

            public IReadOnlyList<ResourceKey> FindKeysByLabel(string label)
            {
                return Array.Empty<ResourceKey>();
            }
        }

        private sealed class ManualResourceOperation<T> : IResourceOperation<T>
        {
            private readonly Func<ResourceLoadResult<T>> _load;
            private readonly ResourceKey _key;
            private ResourceLoadResult<T> _result;

            public ManualResourceOperation(Func<ResourceLoadResult<T>> load, ResourceKey key)
            {
                _load = load;
                _key = key;
                _result = ResourceLoadResult<T>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    key,
                    string.Empty,
                    "Manual resource operation is pending."));
            }

            public bool IsDone { get; private set; }
            public bool IsCancelled { get; private set; }
            public float Progress => IsDone ? 1f : 0f;
            public ResourceLoadResult<T> Result => _result;

            public void CompleteLoaded()
            {
                Complete(_load());
            }

            public void CompleteFailed(ResourceErrorCode code = ResourceErrorCode.ProviderFailed)
            {
                Complete(ResourceLoadResult<T>.Failed(new ResourceError(
                    code,
                    _key,
                    string.Empty,
                    "Manual resource operation failed.")));
            }

            public void Cancel()
            {
                if (IsDone)
                    return;

                IsCancelled = true;
                Complete(ResourceLoadResult<T>.Failed(new ResourceError(
                    ResourceErrorCode.Cancelled,
                    _key,
                    string.Empty,
                    "Resource operation was cancelled.")));
            }

            private void Complete(ResourceLoadResult<T> result)
            {
                if (IsDone)
                    return;

                _result = result;
                IsDone = true;
            }
        }
    }
}
