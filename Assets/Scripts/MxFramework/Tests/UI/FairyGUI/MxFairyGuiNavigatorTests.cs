using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MxFramework.Input;
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

        [Test]
        public void Open_UsesLayerHostAndTracksModalOwnership()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            var layerHost = new FakeLayerHost();
            var descriptor = new MxUiViewDescriptor(ViewId, "ui.demo", "Main", MxUiLayer.Modal)
            {
                Modal = true
            };
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new DemoBinder(), descriptor: descriptor, layerHost: layerHost);

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("modal"));

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(1, layerHost.ShowCount);
            Assert.AreEqual(0, layerHost.HideCount);
            Assert.AreEqual(MxUiLayer.Modal, layerHost.LastShownLayer);
            Assert.AreEqual(1, layerHost.ModalCount);
            Assert.AreEqual(1, host.LastHandle.ShowCount);

            Assert.IsTrue(navigator.Close(ViewId));
            Assert.AreEqual(1, layerHost.HideCount);
            Assert.AreEqual(0, layerHost.ModalCount);
        }

        [Test]
        public void Open_WhenDescriptorIsModal_PushesUiInputContextAndCloseReleasesScope()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            var contexts = new InputContextStack();
            contexts.Set(InputContext.Gameplay);
            var descriptor = new MxUiViewDescriptor(ViewId, "ui.demo", "Main", MxUiLayer.Modal)
            {
                Modal = true
            };
            MxFairyGuiNavigator navigator = CreateNavigator(
                bridge,
                host,
                new DemoBinder(),
                descriptor: descriptor,
                inputBridge: new MxFairyGuiInputContextBridge(_ => contexts.Push(InputContext.UI)));

            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("modal"));

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(InputContext.UI, contexts.ActiveContext);
            Assert.IsFalse(contexts.IsContextEnabled(InputContext.Gameplay));

            Assert.IsTrue(navigator.Close(ViewId));
            Assert.AreEqual(InputContext.Gameplay, contexts.ActiveContext);
            Assert.IsTrue(contexts.IsContextEnabled(InputContext.Gameplay));
        }

        [Test]
        public void Close_WhenModalIsKeepAlive_ReleasesInputScopeUntilReopened()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            var contexts = new InputContextStack();
            contexts.Set(InputContext.Gameplay);
            var descriptor = new MxUiViewDescriptor(ViewId, "ui.demo", "Main", MxUiLayer.Modal)
            {
                Modal = true,
                KeepAlive = true
            };
            MxFairyGuiNavigator navigator = CreateNavigator(
                bridge,
                host,
                new DemoBinder(),
                descriptor: descriptor,
                inputBridge: new MxFairyGuiInputContextBridge(_ => contexts.Push(InputContext.UI)));

            MxUiOpenResult first = navigator.Open(ViewId, new DemoViewModel("first"));
            Assert.IsTrue(navigator.Close(ViewId));

            Assert.IsTrue(first.Success, first.Message);
            Assert.AreEqual(InputContext.Gameplay, contexts.ActiveContext);
            Assert.IsTrue(navigator.IsCached(ViewId));

            MxUiOpenResult second = navigator.Open(ViewId, new DemoViewModel("second"));

            Assert.IsTrue(second.Success, second.Message);
            Assert.AreEqual(InputContext.UI, contexts.ActiveContext);
            Assert.IsTrue(navigator.Close(ViewId));
            Assert.AreEqual(InputContext.Gameplay, contexts.ActiveContext);
        }

        [Test]
        public void ModalCommandGate_BlocksLowerLayerCommandsWhenModalIsActive()
        {
            var layerHost = new FakeLayerHost();
            var inner = new RecordingCommandSink();
            var gate = new MxFairyGuiModalCommandGate(inner, layerHost);
            var modalId = new MxUiViewId("ui.modal");
            var hudId = new MxUiViewId("ui.hud");
            layerHost.Show(new MxUiViewDescriptor(modalId, "ui.demo", "Modal", MxUiLayer.Modal) { Modal = true }, new FakeComponentHandle("ui.demo", "Modal"));

            gate.Enqueue(new MxUiCommand(hudId, "hud.strike", null));
            gate.Enqueue(new MxUiCommand(modalId, "modal.confirm", null));

            Assert.AreEqual(1, gate.BlockedCount);
            Assert.AreEqual(1, gate.ForwardedCount);
            Assert.AreEqual(1, inner.Count);
            Assert.AreEqual("modal.confirm", inner.Last.CommandId);
        }

        [Test]
        public void ModalCommandGate_ForwardsCommandsWhenNoModalIsActiveAndBlocksInvalidSourcesWhenModalIsActive()
        {
            var layerHost = new FakeLayerHost();
            var inner = new RecordingCommandSink();
            var gate = new MxFairyGuiModalCommandGate(inner, layerHost);
            var modalId = new MxUiViewId("ui.modal");

            gate.Enqueue(new MxUiCommand(new MxUiViewId("ui.hud"), "hud.strike", null));
            layerHost.Show(new MxUiViewDescriptor(modalId, "ui.demo", "Modal", MxUiLayer.Modal) { Modal = true }, new FakeComponentHandle("ui.demo", "Modal"));
            gate.Enqueue(new MxUiCommand(default, "invalid.source", null));

            Assert.AreEqual(1, gate.ForwardedCount);
            Assert.AreEqual(1, gate.BlockedCount);
            Assert.AreEqual(1, inner.Count);
            Assert.AreEqual("hud.strike", inner.Last.CommandId);
        }

        [Test]
        public void CancelCommandBridge_EnqueuesCancelForTopModalOnly()
        {
            var layerHost = new FakeLayerHost();
            var inner = new RecordingCommandSink();
            var bridge = new MxFairyGuiCancelCommandBridge(layerHost, inner);
            var modalId = new MxUiViewId("ui.modal");

            var cancel = new InputCommand(10, 1, InputIntent.Cancel, InputCommandPhase.Pressed);
            bool withoutModal = bridge.ProcessCancel(cancel);
            layerHost.Show(new MxUiViewDescriptor(modalId, "ui.demo", "Modal", MxUiLayer.Modal) { Modal = true }, new FakeComponentHandle("ui.demo", "Modal"));
            bool withModal = bridge.ProcessCancel(cancel);

            Assert.IsFalse(withoutModal);
            Assert.IsTrue(withModal);
            Assert.AreEqual(1, inner.Count);
            Assert.AreEqual(modalId, inner.Last.SourceViewId);
            Assert.AreEqual(MxFairyGuiCommandIds.Cancel, inner.Last.CommandId);
            Assert.IsInstanceOf<InputCommand>(inner.Last.Payload);
        }

        [Test]
        public void Dispose_VisibleModal_ReleasesInputScopeOnce()
        {
            var layerHost = new FakeLayerHost();
            var handle = new FakeComponentHandle("ui.demo", "Modal");
            var descriptor = new MxUiViewDescriptor(ViewId, "ui.demo", "Modal", MxUiLayer.Modal)
            {
                Modal = true
            };
            var scope = new RecordingScope();
            var view = new MxFairyGuiView<DemoViewModel>(
                descriptor,
                handle,
                null,
                new FakeFairyGuiHost(),
                layerHost,
                new MxFairyGuiInputContextBridge(_ => scope));

            view.Show();
            view.Dispose();
            view.Dispose();

            Assert.AreEqual(1, scope.DisposeCount);
            Assert.AreEqual(1, handle.DisposeCount);
            Assert.AreEqual(0, layerHost.ModalCount);
        }

        [Test]
        public void Show_WhenLayerHostThrows_ReleasesInputScope()
        {
            var layerHost = new FakeLayerHost { ThrowOnShow = true };
            var handle = new FakeComponentHandle("ui.demo", "Modal");
            var descriptor = new MxUiViewDescriptor(ViewId, "ui.demo", "Modal", MxUiLayer.Modal)
            {
                Modal = true
            };
            var scope = new RecordingScope();
            var view = new MxFairyGuiView<DemoViewModel>(
                descriptor,
                handle,
                null,
                new FakeFairyGuiHost(),
                layerHost,
                new MxFairyGuiInputContextBridge(_ => scope));

            Assert.Throws<InvalidOperationException>(() => view.Show());
            Assert.AreEqual(1, scope.DisposeCount);
        }

        [Test]
        public void Hide_WhenLayerHostThrows_ReleasesInputScope()
        {
            var layerHost = new FakeLayerHost();
            var handle = new FakeComponentHandle("ui.demo", "Modal");
            var descriptor = new MxUiViewDescriptor(ViewId, "ui.demo", "Modal", MxUiLayer.Modal)
            {
                Modal = true
            };
            var scope = new RecordingScope();
            var view = new MxFairyGuiView<DemoViewModel>(
                descriptor,
                handle,
                null,
                new FakeFairyGuiHost(),
                layerHost,
                new MxFairyGuiInputContextBridge(_ => scope));
            view.Show();
            layerHost.ThrowOnHide = true;

            Assert.Throws<InvalidOperationException>(() => view.Hide());
            Assert.AreEqual(1, scope.DisposeCount);
        }

        [Test]
        public void Dispose_WhenLayerHostHideThrows_ReleasesInputScopeComponentAndPackage()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            MxFairyGuiPackageLoadResult load = bridge.LoadPackage(new MxFairyGuiPackageDescriptor("ui.demo", PackageKey));
            Assert.IsTrue(load.Success, load.Failure.Message);

            var layerHost = new FakeLayerHost();
            var host = new FakeFairyGuiHost();
            var handle = new FakeComponentHandle("ui.demo", "Modal");
            var descriptor = new MxUiViewDescriptor(ViewId, "ui.demo", "Modal", MxUiLayer.Modal)
            {
                Modal = true
            };
            var scope = new RecordingScope();
            var view = new MxFairyGuiView<DemoViewModel>(
                descriptor,
                handle,
                load.Scope,
                host,
                layerHost,
                new MxFairyGuiInputContextBridge(_ => scope));
            view.Show();
            layerHost.ThrowOnHide = true;

            Assert.Throws<InvalidOperationException>(() => view.Dispose());

            Assert.AreEqual(1, scope.DisposeCount);
            Assert.AreEqual(1, handle.DisposeCount);
            Assert.AreEqual(1, bridge.ReleasedScopeCount);
            Assert.AreEqual(1, host.ReleasePackageCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void Open_LayerHostRecordsStableLayerOrderIndependentOfOpenOrder()
        {
            var layerHost = new MxFairyGuiLayerHost();
            try
            {
                layerHost.EnsureLayerRoots();

                CollectionAssert.AreEqual(
                    new[] { MxUiLayer.Background, MxUiLayer.Hud, MxUiLayer.Panel, MxUiLayer.Popup, MxUiLayer.Modal, MxUiLayer.Toast, MxUiLayer.Debug },
                    layerHost.LayerOrder);
                AssertRootExists(layerHost, MxUiLayer.Hud);
                AssertRootExists(layerHost, MxUiLayer.Panel);
                AssertRootExists(layerHost, MxUiLayer.Popup);
                AssertRootExists(layerHost, MxUiLayer.Modal);
                AssertRootExists(layerHost, MxUiLayer.Toast);
                AssertRootExists(layerHost, MxUiLayer.Debug);
            }
            finally
            {
                layerHost.Dispose();
            }
        }

        [Test]
        public void Close_WhenDescriptorIsKeepAlive_HidesAndCachesWithoutReleasingPackage()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            var binder = new DemoBinder();
            var descriptor = new MxUiViewDescriptor(ViewId, "ui.demo", "Main", MxUiLayer.Panel)
            {
                KeepAlive = true
            };
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, binder, descriptor: descriptor);
            MxUiOpenResult first = navigator.Open(ViewId, new DemoViewModel("first"));

            bool closed = navigator.Close(ViewId);

            Assert.IsTrue(first.Success, first.Message);
            Assert.IsTrue(closed);
            Assert.IsFalse(navigator.IsOpen(ViewId));
            Assert.IsTrue(navigator.IsCached(ViewId));
            Assert.AreEqual(1, host.LastHandle.HideCount);
            Assert.AreEqual(0, host.LastHandle.DisposeCount);
            Assert.AreEqual(0, bridge.ReleasedScopeCount);
            Assert.AreEqual(0, host.ReleasePackageCount);
            Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);

            MxUiOpenResult second = navigator.Open(ViewId, new DemoViewModel("second"));

            Assert.IsTrue(second.Success, second.Message);
            Assert.AreSame(first.View, second.View);
            Assert.IsTrue(navigator.IsOpen(ViewId));
            Assert.IsFalse(navigator.IsCached(ViewId));
            Assert.AreEqual(1, host.EnsurePackageCount);
            Assert.AreEqual(1, host.CreateComponentCount);
            Assert.AreEqual(2, binder.BindCount);
            Assert.AreEqual("second", binder.Last.Title);
        }

        [Test]
        public void CloseSceneViews_DisposesOpenAndCachedSceneOwnedViews()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            var descriptor = new MxUiViewDescriptor(ViewId, "ui.demo", "Main", MxUiLayer.Panel)
            {
                KeepAlive = true,
                CloseOnSceneChange = true
            };
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new DemoBinder(), descriptor: descriptor);
            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("scene"));
            Assert.IsTrue(result.Success, result.Message);
            Assert.IsTrue(navigator.Close(ViewId));
            Assert.IsTrue(navigator.IsCached(ViewId));

            int closed = navigator.CloseSceneViews();

            Assert.AreEqual(1, closed);
            Assert.IsFalse(navigator.IsOpen(ViewId));
            Assert.IsFalse(navigator.IsCached(ViewId));
            Assert.AreEqual(1, host.LastHandle.DisposeCount);
            Assert.AreEqual(1, bridge.ReleasedScopeCount);
            Assert.AreEqual(1, host.ReleasePackageCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void CloseSceneViews_DisposesOpenSceneOwnedViews()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            var descriptor = new MxUiViewDescriptor(ViewId, "ui.demo", "Main", MxUiLayer.Panel)
            {
                CloseOnSceneChange = true
            };
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new DemoBinder(), descriptor: descriptor);
            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("scene"));

            int closed = navigator.CloseSceneViews();

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(1, closed);
            Assert.IsFalse(navigator.IsOpen(ViewId));
            Assert.IsFalse(navigator.IsCached(ViewId));
            Assert.AreEqual(1, host.LastHandle.DisposeCount);
            Assert.AreEqual(1, bridge.ReleasedScopeCount);
            Assert.AreEqual(1, host.ReleasePackageCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void CloseSceneViews_IgnoresViewsWhenCloseOnSceneChangeIsFalse()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var bridge = new MxFairyGuiResourceBridge(manager);
            var host = new FakeFairyGuiHost();
            var descriptor = new MxUiViewDescriptor(ViewId, "ui.demo", "Main", MxUiLayer.Panel)
            {
                KeepAlive = true,
                CloseOnSceneChange = false
            };
            MxFairyGuiNavigator navigator = CreateNavigator(bridge, host, new DemoBinder(), descriptor: descriptor);
            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("scene"));
            Assert.IsTrue(navigator.Close(ViewId));

            int closed = navigator.CloseSceneViews();

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(0, closed);
            Assert.IsFalse(navigator.IsOpen(ViewId));
            Assert.IsTrue(navigator.IsCached(ViewId));
            Assert.AreEqual(0, host.LastHandle.DisposeCount);
            Assert.AreEqual(0, bridge.ReleasedScopeCount);
            Assert.AreEqual(0, host.ReleasePackageCount);
            Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void RuntimeCatalog_CreateNavigator_RegistersSelectedViewsWithoutGlobalManager()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var host = new FakeFairyGuiHost();
            var binder = new DemoBinder();
            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(CreateDemoRegistration(binder));

            MxFairyGuiNavigator navigator = MxFairyGuiRuntimeShellComposition.CreateNavigator(manager, catalog, host);
            MxUiOpenResult result = navigator.Open(ViewId, new DemoViewModel("catalog"));

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsTrue(navigator.IsOpen(ViewId));
            Assert.AreEqual(1, catalog.ViewCount);
            Assert.AreEqual(1, catalog.PackageCount);
            Assert.AreEqual(1, host.EnsurePackageCount);
            Assert.AreEqual(1, host.CreateComponentCount);
            Assert.AreEqual(1, binder.BindCount);
            Assert.AreEqual("catalog", binder.Last.Title);
        }

        [Test]
        public void RuntimeCatalog_DiagnosticsReportMissingPackageBytes()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider());
            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(CreateDemoRegistration(new DemoBinder()));

            MxFairyGuiRuntimeCatalogDiagnostics diagnostics = catalog.CreateDiagnostics(manager);

            Assert.IsFalse(diagnostics.Success);
            Assert.AreEqual(1, diagnostics.ViewCount);
            Assert.AreEqual(1, diagnostics.PackageCount);
            Assert.AreEqual(1, diagnostics.ResourceKeyCount);
            Assert.AreEqual(1, diagnostics.MissingPackageCount);
            Assert.AreEqual("MissingPackageBytes", diagnostics.Issues[0].Code);
        }

        [Test]
        public void RuntimePreloadSurface_PreloadsCatalogKeysAndReleasesGroup()
        {
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("ui/demo.bytes", new byte[] { 1, 2, 3 }), PackageEntry);
            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(CreateDemoRegistration(new DemoBinder()));
            using (var preload = new MxFairyGuiRuntimePreloadSurface(new ResourcePreloadService(manager)))
            {
                IResourceOperation<ResourcePreloadResult> operation = preload.Preload(catalog, "ui.demo.preload");

                Assert.IsTrue(operation.IsDone);
                MxFairyGuiRuntimePreloadDiagnostics diagnostics = preload.CreateDiagnostics();
                Assert.IsTrue(diagnostics.Success);
                Assert.AreEqual("ui.demo.preload", diagnostics.GroupId);
                Assert.AreEqual(1, diagnostics.RequestedCount);
                Assert.AreEqual(1, diagnostics.LoadedCount);
                Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);

                preload.Release();
                Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            }
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
            string viewModelType = null,
            MxUiViewDescriptor descriptor = null,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null)
        {
            var contracts = new MxUiViewContractRegistry();
            if (registerContract)
            {
                var contract = new MxUiViewContract(descriptor ?? new MxUiViewDescriptor(ViewId, "ui.demo", "Main", MxUiLayer.Panel))
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

            return new MxFairyGuiNavigator(contracts, packages, bridge, host, bindings, layerHost, inputBridge);
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

        private static IMxFairyGuiRuntimeViewRegistration CreateDemoRegistration(IMxFairyGuiViewBinder<DemoViewModel> binder)
        {
            var contract = new MxUiViewContract(new MxUiViewDescriptor(ViewId, "ui.demo", "Main", MxUiLayer.Panel))
            {
                ViewModelType = typeof(DemoViewModel).FullName
            };

            return new MxFairyGuiRuntimeViewRegistration<DemoViewModel>(
                contract,
                new MxFairyGuiPackageDescriptor("ui.demo", PackageKey),
                binder);
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

        private static void AssertRootExists(MxFairyGuiLayerHost layerHost, MxUiLayer layer)
        {
            Fgui.GComponent root;
            Assert.IsTrue(layerHost.TryGetLayerRoot(layer, out root));
            Assert.IsNotNull(root);
            Assert.AreEqual("MxFairyGuiLayer_" + layer, root.name);
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

        private sealed class FakeLayerHost : IMxFairyGuiLayerHost
        {
            private readonly List<MxUiLayer> _layerOrder = new List<MxUiLayer>();
            private readonly List<MxUiViewId> _modalStack = new List<MxUiViewId>();

            public bool ThrowOnShow { get; set; }
            public bool ThrowOnHide { get; set; }
            public int ShowCount { get; private set; }
            public int HideCount { get; private set; }
            public MxUiLayer LastShownLayer { get; private set; }
            public int ModalCount => _modalStack.Count;
            public IReadOnlyList<MxUiLayer> LayerOrder => _layerOrder;

            public void Show(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component)
            {
                if (ThrowOnShow)
                    throw new InvalidOperationException("layer host failed");

                ShowCount++;
                LastShownLayer = descriptor.Layer;
                if (!_layerOrder.Contains(descriptor.Layer))
                {
                    _layerOrder.Add(descriptor.Layer);
                    _layerOrder.Sort((left, right) => ((int)left).CompareTo((int)right));
                }

                if (descriptor.Modal && !_modalStack.Contains(descriptor.Id))
                    _modalStack.Add(descriptor.Id);

                component.Show();
            }

            public void Hide(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component)
            {
                if (ThrowOnHide)
                    throw new InvalidOperationException("layer host hide failed");

                HideCount++;
                if (descriptor.Modal)
                    _modalStack.Remove(descriptor.Id);

                component.Hide();
            }

            public bool TryGetTopModal(out MxUiViewId viewId)
            {
                if (_modalStack.Count == 0)
                {
                    viewId = default;
                    return false;
                }

                viewId = _modalStack[_modalStack.Count - 1];
                return true;
            }
        }

        private sealed class RecordingCommandSink : IMxUiCommandSink
        {
            public int Count { get; private set; }
            public MxUiCommand Last { get; private set; }

            public void Enqueue(MxUiCommand command)
            {
                Count++;
                Last = command;
            }
        }

        private sealed class RecordingScope : IDisposable
        {
            public int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
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
