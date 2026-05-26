using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using NUnit.Framework;
using Fgui = global::FairyGUI;

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
            Assert.AreEqual(1, binder.BindCount);
            Assert.AreEqual("ready", binder.Last.Title);
            Assert.AreSame(host.LastHandle, binder.LastComponent);
            Assert.IsTrue(host.LastHandle.Visible);
            Assert.AreEqual(MxUiLifecycleState.Visible, result.View.Lifecycle.State);
        }

        [Test]
        public void Open_WithRealMxFguiSmokePackage_BindsClosesAndReleases()
        {
            RemoveRealSmokePackageIfLoaded();

            byte[] packageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxFguiSmoke/MxFguiSmoke_fui.bytes"));
            Assert.Greater(packageBytes.Length, 4);
            Assert.AreEqual((byte)'F', packageBytes[0]);
            Assert.AreEqual((byte)'G', packageBytes[1]);
            Assert.AreEqual((byte)'U', packageBytes[2]);
            Assert.AreEqual((byte)'I', packageBytes[3]);

            ResourceManager manager = CreateManager(
                new MemoryResourceProvider().Register("fgui/MxFguiSmoke_fui.bytes", packageBytes),
                RealSmokePackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new MxFairyGuiHost();
            var binder = new RealSmokeBinder();
            MxFairyGuiNavigator navigator = CreateRealSmokeNavigator(bridge, host, binder);

            try
            {
                MxUiOpenResult result = navigator.Open(RealSmokeViewId, new RealSmokeViewModel("Navigator smoke bound"));

                Assert.IsTrue(result.Success, result.Message);
                Assert.IsTrue(navigator.IsOpen(RealSmokeViewId));
                Assert.AreEqual(1, bridge.LoadedScopeCount);
                Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(1, binder.BindCount);
                Assert.AreEqual("Navigator smoke bound", binder.BoundTitle);
                Assert.IsFalse(binder.BoundComponent.IsDisposed);
                Assert.AreEqual(MxUiLifecycleState.Visible, result.View.Lifecycle.State);

                Assert.IsTrue(navigator.Close(RealSmokeViewId));
                Assert.IsFalse(navigator.Close(RealSmokeViewId));
                Assert.IsFalse(navigator.IsOpen(RealSmokeViewId));
                Assert.IsTrue(binder.BoundComponent.IsDisposed);
                Assert.AreEqual(1, bridge.ReleasedScopeCount);
                Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
                Assert.IsNull(Fgui.UIPackage.GetByName(RealSmokePackageId));
            }
            finally
            {
                if (navigator.IsOpen(RealSmokeViewId))
                    navigator.Close(RealSmokeViewId);

                RemoveRealSmokePackageIfLoaded();
            }
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
        public void Open_WhenContractIsMissing_ReturnsStructuredFailure()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new DemoBinder(), registerContract: false);

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("missing"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewNotFound, result.ErrorCode);
            StringAssert.Contains("contract is not registered", result.Message);
            Assert.AreEqual(0, host.EnsurePackageCount);
        }

        [Test]
        public void Open_WhenPackageDescriptorIsMissing_ReturnsStructuredFailure()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new DemoBinder(), registerPackage: false);

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("missing"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, result.ErrorCode);
            StringAssert.Contains("package descriptor is not registered", result.Message);
            Assert.AreEqual(0, host.EnsurePackageCount);
        }

        [Test]
        public void Open_WhenHostEnsurePackageFails_ReleasesLoadedPackage()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost { EnsurePackageFailure = "owned package conflict" };
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new DemoBinder());

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("fail"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, result.ErrorCode);
            StringAssert.Contains("owned package conflict", result.Message);
            Assert.AreEqual(1, bridge.ReleasedScopeCount);
            Assert.AreEqual(1, host.ReleasePackageCount);
            Assert.AreEqual(0, host.CreateComponentCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
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
        public void Open_WhenContractViewModelTypeIsMissing_ReturnsStructuredFailure()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new DemoBinder(), viewModelType: string.Empty);

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("missing"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, result.ErrorCode);
            StringAssert.Contains("view model type is required", result.Message);
            Assert.AreEqual(0, host.EnsurePackageCount);
        }

        [Test]
        public void Open_WhenContractViewModelTypeDoesNotMatchArgs_ReturnsStructuredFailure()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new DemoBinder(), viewModelType: typeof(OtherViewModel).FullName);

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("wrong"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, result.ErrorCode);
            StringAssert.Contains("view model type does not match", result.Message);
            Assert.AreEqual(0, host.EnsurePackageCount);
        }

        [Test]
        public void Open_WhenBindingIsMissing_ReturnsStructuredFailureAndReleasesPackage()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, (IMxFairyGuiViewBinder<DemoViewModel>)null);

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("missing"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, result.ErrorCode);
            StringAssert.Contains("view binding is not registered", result.Message);
            Assert.AreEqual(1, bridge.ReleasedScopeCount);
            Assert.AreEqual(1, host.ReleasePackageCount);
            Assert.AreEqual(1, host.LastHandle.DisposeCount);
        }

        [Test]
        public void Open_WhenBindingTypeDoesNotMatch_ReturnsStructuredFailureAndReleasesPackage()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            var bindings = new MxFairyGuiViewBindingRegistry();
            bindings.Register(ViewId, new OtherBinder());
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, bindings);

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("wrong"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, result.ErrorCode);
            StringAssert.Contains("binding type does not match", result.Message);
            Assert.AreEqual(1, bridge.ReleasedScopeCount);
            Assert.AreEqual(1, host.ReleasePackageCount);
            Assert.AreEqual(1, host.LastHandle.DisposeCount);
        }

        [Test]
        public void Open_WhenBinderThrows_ReturnsStructuredFailureAndReleasesPackage()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new ThrowingBinder());

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("throw"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, result.ErrorCode);
            StringAssert.Contains("view binding failed", result.Message);
            Assert.AreEqual(1, bridge.ReleasedScopeCount);
            Assert.AreEqual(1, host.ReleasePackageCount);
            Assert.AreEqual(1, host.LastHandle.DisposeCount);
        }

        [Test]
        public void Open_WhenSameViewIsReopened_BindsExistingComponentWithNewArgs()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            var binder = new DemoBinder();
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, binder);

            MxUiOpenResult first = navigator.Open(ViewId, new DemoViewModel("first"));
            MxUiOpenResult second = navigator.Open(ViewId, new DemoViewModel("second"));

            Assert.IsTrue(first.Success, first.Message);
            Assert.IsTrue(second.Success, second.Message);
            Assert.AreSame(first.View, second.View);
            Assert.AreEqual(1, host.EnsurePackageCount);
            Assert.AreEqual(1, host.CreateComponentCount);
            Assert.AreEqual(2, binder.BindCount);
            Assert.AreEqual("second", binder.Last.Title);
            Assert.AreSame(host.LastHandle, binder.LastComponent);
            Assert.AreEqual(1, host.LastHandle.ShowCount);
        }

        [Test]
        public void Open_WhenExistingViewIsReopenedWithWrongArgs_ReturnsStructuredFailure()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            var binder = new DemoBinder();
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, binder);
            MxUiOpenResult first = navigator.Open(ViewId, new DemoViewModel("first"));

            MxUiOpenResult second = navigator.Open(ViewId, new OtherViewModel());

            Assert.IsTrue(first.Success, first.Message);
            Assert.IsFalse(second.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, second.ErrorCode);
            StringAssert.Contains("view model type does not match", second.Message);
            Assert.AreEqual(1, binder.BindCount);
            Assert.AreEqual(1, host.LastHandle.ShowCount);
            Assert.AreEqual(1, host.EnsurePackageCount);
            Assert.AreEqual(1, host.CreateComponentCount);
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
        private static readonly MxUiViewId RealSmokeViewId = new MxUiViewId("ui.fairygui.smoke");
        private static readonly ResourceKey RealSmokePackageKey = new ResourceKey("ui.fairygui.smoke.package", MxFairyGuiResourceTypeIds.PackageBytes, packageId: RealSmokePackageId);
        private static readonly ResourceCatalogEntry RealSmokePackageEntry = Entry(RealSmokePackageKey, "fgui/MxFguiSmoke_fui.bytes");
        private const string RealSmokePackageId = "MxFguiSmoke";

        private static MxFairyGuiNavigator CreateNavigator(
            MxFairyGuiResourceBridge bridge,
            IMxFairyGuiHost host,
            IMxFairyGuiViewBinder<DemoViewModel> binder,
            bool registerContract = true,
            bool registerPackage = true,
            string viewModelType = null)
        {
            var contracts = new MxUiViewContractRegistry();
            if (registerContract)
            {
                var contract = new MxUiViewContract(new MxUiViewDescriptor(ViewId, "ui.demo", "Main", MxUiLayer.Panel))
                {
                    ViewModelType = viewModelType ?? typeof(DemoViewModel).FullName
                };
                contracts.Register(contract);
            }

            var packages = new MxFairyGuiPackageCatalog();
            if (registerPackage)
                packages.Register(new MxFairyGuiPackageDescriptor("ui.demo", PackageKey));

            var bindings = new MxFairyGuiViewBindingRegistry();
            if (binder != null)
                bindings.Register(ViewId, binder);

            return new MxFairyGuiNavigator(contracts, packages, bridge, host, bindings);
        }

        private static MxFairyGuiNavigator CreateNavigator(
            MxFairyGuiResourceBridge bridge,
            IMxFairyGuiHost host,
            MxFairyGuiViewBindingRegistry bindings)
        {
            var contracts = new MxUiViewContractRegistry();
            contracts.Register(new MxUiViewContract(new MxUiViewDescriptor(ViewId, "ui.demo", "Main", MxUiLayer.Panel))
            {
                ViewModelType = typeof(DemoViewModel).FullName
            });

            var packages = new MxFairyGuiPackageCatalog();
            packages.Register(new MxFairyGuiPackageDescriptor("ui.demo", PackageKey));

            return new MxFairyGuiNavigator(contracts, packages, bridge, host, bindings);
        }

        private static MxFairyGuiNavigator CreateRealSmokeNavigator(
            MxFairyGuiResourceBridge bridge,
            IMxFairyGuiHost host,
            IMxFairyGuiViewBinder<RealSmokeViewModel> binder)
        {
            var contracts = new MxUiViewContractRegistry();
            contracts.Register(new MxUiViewContract(new MxUiViewDescriptor(RealSmokeViewId, RealSmokePackageId, "SmokePanel", MxUiLayer.Panel))
            {
                ViewModelType = typeof(RealSmokeViewModel).FullName
            });

            var packages = new MxFairyGuiPackageCatalog();
            packages.Register(new MxFairyGuiPackageDescriptor(RealSmokePackageId, RealSmokePackageKey));

            var bindings = new MxFairyGuiViewBindingRegistry();
            bindings.Register(RealSmokeViewId, binder);

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

        private static void RemoveRealSmokePackageIfLoaded()
        {
            if (Fgui.UIPackage.GetByName(RealSmokePackageId) != null)
                Fgui.UIPackage.RemovePackage(RealSmokePackageId);
        }

        private sealed class DemoViewModel
        {
            public DemoViewModel(string title)
            {
                Title = title;
            }

            public string Title { get; }
        }

        private sealed class RealSmokeViewModel
        {
            public RealSmokeViewModel(string title)
            {
                Title = title;
            }

            public string Title { get; }
        }

        private sealed class DemoBinder : IMxFairyGuiViewBinder<DemoViewModel>
        {
            public DemoViewModel Last { get; private set; }
            public IMxFairyGuiComponentHandle LastComponent { get; private set; }
            public int BindCount { get; private set; }

            public void Bind(IMxFairyGuiComponentHandle component, DemoViewModel viewModel)
            {
                BindCount++;
                LastComponent = component;
                Last = viewModel;
            }
        }

        private sealed class RealSmokeBinder : IMxFairyGuiViewBinder<RealSmokeViewModel>
        {
            public MxFairyGuiComponentHandle BoundComponent { get; private set; }
            public int BindCount { get; private set; }
            public string BoundTitle { get; private set; }

            public void Bind(IMxFairyGuiComponentHandle component, RealSmokeViewModel viewModel)
            {
                BindCount++;
                BoundComponent = component as MxFairyGuiComponentHandle;
                Assert.IsNotNull(BoundComponent);

                var title = BoundComponent.Component.GetChild("txtTitle")?.asTextField;
                Assert.IsNotNull(title);
                title.text = viewModel.Title;
                BoundTitle = title.text;
            }
        }

        private sealed class ThrowingBinder : IMxFairyGuiViewBinder<DemoViewModel>
        {
            public void Bind(IMxFairyGuiComponentHandle component, DemoViewModel viewModel)
            {
                throw new InvalidOperationException("binder failed");
            }
        }

        private sealed class OtherBinder : IMxFairyGuiViewBinder<OtherViewModel>
        {
            public void Bind(IMxFairyGuiComponentHandle component, OtherViewModel viewModel)
            {
            }
        }

        private sealed class OtherViewModel
        {
        }

        private sealed class FakeFairyGuiHost : IMxFairyGuiHost
        {
            public string EnsurePackageFailure { get; set; }
            public string CreateComponentFailure { get; set; }
            public int EnsurePackageCount { get; private set; }
            public int CreateComponentCount { get; private set; }
            public int ReleasePackageCount { get; private set; }
            public FakeComponentHandle LastHandle { get; private set; }

            public bool EnsurePackage(MxFairyGuiPackageLoadScope scope, out string failure)
            {
                EnsurePackageCount++;
                if (!string.IsNullOrEmpty(EnsurePackageFailure))
                {
                    failure = EnsurePackageFailure;
                    return false;
                }

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
            public int ShowCount { get; private set; }
            public int HideCount { get; private set; }
            public int DisposeCount { get; private set; }

            public void Show()
            {
                ShowCount++;
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
