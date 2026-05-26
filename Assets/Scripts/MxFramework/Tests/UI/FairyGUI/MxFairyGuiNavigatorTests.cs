using System.Collections.Generic;
using System.Threading;
using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using NUnit.Framework;

namespace MxFramework.Tests.UI.FairyGui
{
    public sealed class MxFairyGuiNavigatorTests
    {
        [Test]
        public void Open_WithRegisteredPackageComponentAndViewModel_BindsAndShowsView()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            var binder = new DemoBinder();
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, binder);

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("ready"));

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsTrue(navigator.IsOpen(ViewId));
            Assert.AreEqual(1, host.EnsurePackageCount);
            Assert.AreEqual(1, host.CreateComponentCount);
            Assert.AreEqual("ready", binder.Last.Title);
            Assert.AreSame(host.LastHandle, binder.LastComponent);
            Assert.IsTrue(host.LastHandle.Visible);
            Assert.AreEqual(MxUiLifecycleState.Visible, result.View.Lifecycle.State);
        }

        [Test]
        public void Open_WhenPackageBytesAreMissing_MapsBridgeFailureToOpenFailure()
        {
            var bridge = new MxFairyGuiResourceBridge(CreateManager(new MemoryResourceProvider()));
            var host = new FakeFairyGuiHost();
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new DemoBinder());

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("missing"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, result.ErrorCode);
            Assert.AreEqual(0, host.EnsurePackageCount);
            Assert.AreEqual(0, host.CreateComponentCount);
        }

        [Test]
        public void Open_WhenComponentCreationFails_ReleasesLoadedPackage()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost { CreateComponentFailure = "component missing" };
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new DemoBinder());

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("fail"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, result.ErrorCode);
            StringAssert.Contains("component missing", result.Message);
            Assert.AreEqual(1, bridge.ReleasedScopeCount);
            Assert.AreEqual(1, host.ReleasePackageCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void Open_WhenResourceIsPending_MapsToResourcesPending()
        {
            var manager = new PendingResourceManager(PackageKey);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new DemoBinder());

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("pending"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ResourcesPending, result.ErrorCode);
            Assert.AreEqual(1, manager.CancelCount);
            Assert.AreEqual(0, host.EnsurePackageCount);
        }

        [Test]
        public void CloseHideAndDispose_AreIdempotentAndReleasePackageOnce()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new DemoBinder());
            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("close"));

            result.View.Hide();
            result.View.Hide();
            bool firstClose = navigator.Close(ViewId);
            bool secondClose = navigator.Close(ViewId);
            result.View.Dispose();

            Assert.IsTrue(firstClose);
            Assert.IsFalse(secondClose);
            Assert.IsFalse(navigator.IsOpen(ViewId));
            Assert.AreEqual(1, host.LastHandle.HideCount);
            Assert.AreEqual(1, host.LastHandle.DisposeCount);
            Assert.AreEqual(1, bridge.ReleasedScopeCount);
            Assert.AreEqual(1, host.ReleasePackageCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        private static readonly MxUiViewId ViewId = new MxUiViewId("ui.demo.main");
        private static readonly ResourceKey PackageKey = new ResourceKey("ui.demo.package", MxFairyGuiResourceTypeIds.PackageBytes);
        private static readonly ResourceCatalogEntry PackageEntry = Entry(PackageKey, "ui/demo.bytes");

        private static MxFairyGuiNavigator CreateNavigator(
            MxFairyGuiResourceBridge bridge,
            IMxFairyGuiHost host,
            DemoBinder binder)
        {
            var contracts = new MxUiViewContractRegistry();
            var contract = new MxUiViewContract(new MxUiViewDescriptor(ViewId, "ui.demo", "Main", MxUiLayer.Panel))
            {
                ViewModelType = typeof(DemoViewModel).FullName
            };
            contracts.Register(contract);

            var packages = new MxFairyGuiPackageCatalog();
            packages.Register(new MxFairyGuiPackageDescriptor("ui.demo", PackageKey));

            var bindings = new MxFairyGuiViewBindingRegistry();
            bindings.Register(ViewId, binder);

            return new MxFairyGuiNavigator(contracts, packages, bridge, host, bindings);
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

        private sealed class DemoViewModel
        {
            public DemoViewModel(string title)
            {
                Title = title;
            }

            public string Title { get; }
        }

        private sealed class DemoBinder : IMxFairyGuiViewBinder<DemoViewModel>
        {
            public DemoViewModel Last { get; private set; }
            public IMxFairyGuiComponentHandle LastComponent { get; private set; }

            public void Bind(IMxFairyGuiComponentHandle component, DemoViewModel viewModel)
            {
                LastComponent = component;
                Last = viewModel;
            }
        }

        private sealed class FakeFairyGuiHost : IMxFairyGuiHost
        {
            public string CreateComponentFailure { get; set; }
            public int EnsurePackageCount { get; private set; }
            public int CreateComponentCount { get; private set; }
            public int ReleasePackageCount { get; private set; }
            public FakeComponentHandle LastHandle { get; private set; }

            public bool EnsurePackage(MxFairyGuiPackageLoadScope scope, out string failure)
            {
                EnsurePackageCount++;
                failure = string.Empty;
                return true;
            }

            public bool TryCreateComponent(string packageId, string componentName, out IMxFairyGuiComponentHandle handle, out string failure)
            {
                CreateComponentCount++;
                if (!string.IsNullOrEmpty(CreateComponentFailure))
                {
                    handle = null;
                    failure = CreateComponentFailure;
                    return false;
                }

                LastHandle = new FakeComponentHandle(packageId, componentName);
                handle = LastHandle;
                failure = string.Empty;
                return true;
            }

            public void ReleasePackage(string packageId)
            {
                ReleasePackageCount++;
            }
        }

        private sealed class FakeComponentHandle : IMxFairyGuiComponentHandle
        {
            public FakeComponentHandle(string packageId, string componentName)
            {
                PackageId = packageId;
                ComponentName = componentName;
            }

            public string PackageId { get; }
            public string ComponentName { get; }
            public bool IsDisposed { get; private set; }
            public bool Visible { get; private set; }
            public int HideCount { get; private set; }
            public int DisposeCount { get; private set; }

            public void Show()
            {
                Visible = true;
            }

            public void Hide()
            {
                HideCount++;
                Visible = false;
            }

            public void Dispose()
            {
                if (IsDisposed)
                    return;

                IsDisposed = true;
                DisposeCount++;
            }
        }

        private sealed class PendingResourceManager : IResourceManager
        {
            private readonly ResourceKey _pendingKey;

            public PendingResourceManager(ResourceKey pendingKey)
            {
                _pendingKey = pendingKey;
            }

            public int CancelCount { get; private set; }

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
                return new PendingOperation<T>(key, () => CancelCount++);
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
            private readonly System.Action _onCancel;

            public PendingOperation(ResourceKey key, System.Action onCancel)
            {
                _onCancel = onCancel;
                Result = ResourceLoadResult<ResourceHandle<T>>.Failed(new ResourceError(ResourceErrorCode.ProviderFailed, key, string.Empty, "Pending."));
            }

            public bool IsDone => false;
            public bool IsCancelled { get; private set; }
            public float Progress => 0.25f;
            public ResourceLoadResult<ResourceHandle<T>> Result { get; }

            public void Cancel()
            {
                if (IsCancelled)
                    return;

                IsCancelled = true;
                _onCancel?.Invoke();
            }
        }
    }
}
