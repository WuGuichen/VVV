using System.Collections.Generic;
using System.Threading;
using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using NUnit.Framework;

namespace MxFramework.Tests.UI.FairyGui
{
    public sealed class MxFairyGuiResourceBridgeTests
    {
        [Test]
        public void LoadPackage_WhenPackageBytesAreMissing_ReturnsStructuredMissingPackage()
        {
            var bridge = new MxFairyGuiResourceBridge(CreateManager(new MemoryResourceProvider()));

            MxFairyGuiPackageLoadResult result = bridge.LoadPackage(CreateDescriptor());

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxFairyGuiResourceBridgeStatus.MissingPackage, result.Status);
            Assert.AreEqual(PackageKey, result.Failure.Key);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, MxFairyGuiResourceBridgeUiMapping.ToOpenFailure(result).ErrorCode);
        }

        [Test]
        public void LoadPackage_WhenRequiredAtlasIsMissing_ReturnsStructuredMissingResource()
        {
            var provider = new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 });
            ResourceManager manager = CreateManager(provider, Entry(PackageKey, "ui/demo.bytes"));
            var bridge = new MxFairyGuiResourceBridge(manager);

            MxFairyGuiPackageLoadResult result = bridge.LoadPackage(CreateDescriptor(Atlas("ui.demo.atlas.missing")));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxFairyGuiResourceBridgeStatus.MissingResource, result.Status);
            Assert.AreEqual(new ResourceKey("ui.demo.atlas.missing", ResourceTypeIds.Texture2D), result.Failure.Key);
            Assert.AreEqual(0, provider.ReleaseCount);
        }

        [Test]
        public void LoadPackage_WhenProviderFails_ReturnsLoadFailedAndResourceError()
        {
            var provider = new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 });
            ResourceManager manager = CreateManager(
                provider,
                Entry(PackageKey, "ui/demo.bytes"),
                Entry(new ResourceKey("ui.demo.atlas.main", ResourceTypeIds.Texture2D), "ui/missing-atlas"));
            var bridge = new MxFairyGuiResourceBridge(manager);

            MxFairyGuiPackageLoadResult result = bridge.LoadPackage(CreateDescriptor(Atlas("ui.demo.atlas.main")));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxFairyGuiResourceBridgeStatus.LoadFailed, result.Status);
            Assert.AreEqual(ResourceErrorCode.NotFound, result.Failure.ResourceError.Code);
            Assert.AreEqual(1, provider.ReleaseCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void LoadPackage_WhenOperationIsPending_ReturnsPendingOpenMapping()
        {
            var manager = new PendingResourceManager(PackageKey);
            var bridge = new MxFairyGuiResourceBridge(manager);

            MxFairyGuiPackageLoadResult result = bridge.LoadPackage(CreateDescriptor());

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxFairyGuiResourceBridgeStatus.LoadPending, result.Status);
            Assert.AreEqual(MxUiOpenErrorCode.ResourcesPending, MxFairyGuiResourceBridgeUiMapping.ToOpenFailure(result).ErrorCode);
        }

        [Test]
        public void LoadPackage_WithMemoryResources_ReturnsScopeAndTracksHandles()
        {
            var provider = new MemoryResourceProvider()
                .Register("ui/demo.bytes", new byte[] { 1, 2, 3 })
                .Register("ui/demo-atlas", new FakeResource("atlas"))
                .Register("ui/demo-audio", new FakeResource("audio"))
                .Register("ui/demo-font", new FakeResource("font"));
            ResourceManager manager = CreateManager(
                provider,
                Entry(PackageKey, "ui/demo.bytes"),
                Entry(AtlasKey, "ui/demo-atlas"),
                Entry(AudioKey, "ui/demo-audio"),
                Entry(FontKey, "ui/demo-font"));
            var bridge = new MxFairyGuiResourceBridge(manager);

            MxFairyGuiPackageLoadResult result = bridge.LoadPackage(CreateDescriptor(Atlas(AtlasKey.Id), Audio(AudioKey.Id), Font(FontKey.Id)));

            Assert.IsTrue(result.Success, result.Failure.Message);
            Assert.IsNotNull(result.Scope);
            Assert.AreEqual("ui.demo", result.Scope.PackageId);
            Assert.AreEqual(PackageKey, result.Scope.PackageHandle.Key);
            Assert.AreEqual(3, result.Scope.ResourceHandles.Count);
            Assert.AreEqual(1, bridge.LoadedScopeCount);
            Assert.AreEqual(4, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(4, manager.CreateDebugSnapshot().TotalRefCount);
        }

        [Test]
        public void Release_WhenScopeIsReleased_ReleasesPackageResourcesAndIsIdempotent()
        {
            var provider = new MemoryResourceProvider()
                .Register("ui/demo.bytes", new byte[] { 1, 2, 3 })
                .Register("ui/demo-atlas", new FakeResource("atlas"));
            ResourceManager manager = CreateManager(
                provider,
                Entry(PackageKey, "ui/demo.bytes"),
                Entry(AtlasKey, "ui/demo-atlas"));
            var bridge = new MxFairyGuiResourceBridge(manager);
            MxFairyGuiPackageLoadScope scope = bridge.LoadPackage(CreateDescriptor(Atlas(AtlasKey.Id))).Scope;

            scope.Release();
            scope.Release();
            MxFairyGuiPackageLoadResult released = bridge.ToReleasedResult(scope);

            Assert.IsTrue(scope.IsReleased);
            Assert.AreEqual(MxFairyGuiResourceBridgeStatus.Released, released.Status);
            Assert.AreEqual(MxUiOpenErrorCode.OperationCancelled, MxFairyGuiResourceBridgeUiMapping.ToOpenFailure(released).ErrorCode);
            Assert.AreEqual(1, bridge.ReleasedScopeCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(2, provider.ReleaseCount);
        }

        [Test]
        public void LoadPackage_WhenDescriptorIsInvalid_ReturnsInvalidDescriptor()
        {
            var bridge = new MxFairyGuiResourceBridge(CreateManager(new MemoryResourceProvider()));
            var descriptor = new MxFairyGuiPackageDescriptor("ui.demo", new ResourceKey("Bad Key", MxFairyGuiResourceTypeIds.PackageBytes));

            MxFairyGuiPackageLoadResult result = bridge.LoadPackage(descriptor);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxFairyGuiResourceBridgeStatus.InvalidDescriptor, result.Status);
            StringAssert.Contains("invalid", result.Failure.Message);
        }

        private static readonly ResourceKey PackageKey = new ResourceKey("ui.demo.package", MxFairyGuiResourceTypeIds.PackageBytes);
        private static readonly ResourceKey AtlasKey = new ResourceKey("ui.demo.atlas.main", ResourceTypeIds.Texture2D);
        private static readonly ResourceKey AudioKey = new ResourceKey("ui.demo.audio.click", ResourceTypeIds.AudioClip);
        private static readonly ResourceKey FontKey = new ResourceKey("ui.demo.font.main", ResourceTypeIds.Font);

        private static MxFairyGuiPackageDescriptor CreateDescriptor(params MxFairyGuiPackageResourceDescriptor[] resources)
        {
            return new MxFairyGuiPackageDescriptor("ui.demo", PackageKey, resources);
        }

        private static MxFairyGuiPackageResourceDescriptor Atlas(string id)
        {
            return new MxFairyGuiPackageResourceDescriptor(new ResourceKey(id, ResourceTypeIds.Texture2D), MxFairyGuiPackageResourceKind.Atlas);
        }

        private static MxFairyGuiPackageResourceDescriptor Audio(string id)
        {
            return new MxFairyGuiPackageResourceDescriptor(new ResourceKey(id, ResourceTypeIds.AudioClip), MxFairyGuiPackageResourceKind.Audio);
        }

        private static MxFairyGuiPackageResourceDescriptor Font(string id)
        {
            return new MxFairyGuiPackageResourceDescriptor(new ResourceKey(id, ResourceTypeIds.Font), MxFairyGuiPackageResourceKind.Font);
        }

        private static ResourceManager CreateManager(MemoryResourceProvider provider, params ResourceCatalogEntry[] entries)
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog("ui.demo.catalog", string.Empty, entries));
            return manager;
        }

        private static ResourceCatalogEntry Entry(ResourceKey key, string address)
        {
            return new ResourceCatalogEntry(key.Id, key.TypeId, "memory", address, key.Variant, key.PackageId);
        }

        private sealed class FakeResource
        {
            public FakeResource(string id)
            {
                Id = id;
            }

            public string Id { get; }
        }

        private sealed class PendingResourceManager : IResourceManager
        {
            private readonly ResourceKey _pendingKey;

            public PendingResourceManager(ResourceKey pendingKey)
            {
                _pendingKey = pendingKey;
            }

            public IResourceManager RegisterProvider(IResourceProvider provider)
            {
                return this;
            }

            public IResourceManager AddCatalog(ResourceCatalog catalog)
            {
                return this;
            }

            public bool Contains(ResourceKey key)
            {
                return key == _pendingKey;
            }

            public ResourceLoadResult<ResourceHandle<T>> Load<T>(ResourceKey key)
            {
                return ResourceLoadResult<ResourceHandle<T>>.Failed(new ResourceError(ResourceErrorCode.ProviderFailed, key, string.Empty, "Use async path."));
            }

            public IResourceOperation<ResourceHandle<T>> LoadAsync<T>(ResourceKey key, CancellationToken cancellationToken = default)
            {
                return new PendingOperation<T>(key);
            }

            public void Release<T>(ResourceHandle<T> handle)
            {
            }

            public ResourceDebugSnapshot CreateDebugSnapshot()
            {
                return default;
            }
        }

        private sealed class PendingOperation<T> : IResourceOperation<ResourceHandle<T>>
        {
            public PendingOperation(ResourceKey key)
            {
                Result = ResourceLoadResult<ResourceHandle<T>>.Failed(new ResourceError(ResourceErrorCode.ProviderFailed, key, string.Empty, "Pending."));
            }

            public bool IsDone => false;
            public bool IsCancelled => false;
            public float Progress => 0.25f;
            public ResourceLoadResult<ResourceHandle<T>> Result { get; }

            public void Cancel()
            {
            }
        }
    }
}
