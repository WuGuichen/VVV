using System;
using Fgui = global::FairyGUI;
using MxFramework.Resources;
using MxFramework.UI;

namespace MxFramework.UI.FairyGui
{
    public sealed class MxFairyGuiNavigator : IMxUiNavigator, IDisposable
    {
        private readonly MxUiViewContractRegistry _contracts;
        private readonly MxFairyGuiPackageCatalog _packages;
        private readonly MxFairyGuiResourceBridge _resources;
        private readonly IMxFairyGuiHost _host;
        private readonly IMxFairyGuiLayerHost _layerHost;
        private readonly IMxFairyGuiInputContextBridge _inputBridge;
        private readonly IMxFairyGuiViewTransitionController _transitionController;
        private readonly IMxFairyGuiPopupService _popupService;
        private readonly MxFairyGuiViewBindingRegistry _bindings;
        private readonly System.Collections.Generic.Dictionary<MxUiViewId, IMxUiView> _openViews =
            new System.Collections.Generic.Dictionary<MxUiViewId, IMxUiView>();
        private readonly System.Collections.Generic.Dictionary<MxUiViewId, IMxUiView> _cachedViews =
            new System.Collections.Generic.Dictionary<MxUiViewId, IMxUiView>();
        private readonly System.Collections.Generic.Dictionary<MxUiViewId, MxUiOpenOperation> _pendingOpens =
            new System.Collections.Generic.Dictionary<MxUiViewId, MxUiOpenOperation>();

        public MxFairyGuiNavigator(
            MxUiViewContractRegistry contracts,
            MxFairyGuiPackageCatalog packages,
            MxFairyGuiResourceBridge resources,
            IMxFairyGuiHost host,
            MxFairyGuiViewBindingRegistry bindings = null,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiViewTransitionController transitionController = null,
            IMxFairyGuiPopupService popupService = null)
        {
            _contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _layerHost = layerHost ?? new MxFairyGuiLayerHost();
            _inputBridge = inputBridge ?? MxFairyGuiNullInputContextBridge.Instance;
            _transitionController = transitionController ?? MxFairyGuiNullViewTransitionController.Instance;
            _popupService = popupService;
            _bindings = bindings ?? new MxFairyGuiViewBindingRegistry();
        }

        public void UpdateResourceManager(IResourceManager resourceManager)
        {
            _resources.UpdateResourceManager(resourceManager);
        }

        public MxUiOpenResult Open<TArgs>(MxUiViewId id, TArgs args)
        {
            if (!id.IsValid)
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.InvalidViewId, "UI view id is required.");

            if (_pendingOpens.ContainsKey(id))
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.ResourcesPending, "UI view open is already pending: " + id + ".");

            MxFairyGuiStageLayout.EnsureDefaultContentScaleFactor();

            MxUiViewContract contract;
            if (!_contracts.TryGet(id, out contract))
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewNotFound, "UI view contract is not registered: " + id + ".");

            string failure;
            if (!ValidateViewModelType<TArgs>(contract, out failure))
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewCreateFailed, failure);

            IMxUiView existing;
            if (_cachedViews.TryGetValue(id, out existing))
            {
                MxUiOpenResult cachedResult = RebindExistingView(id, args, existing);
                if (cachedResult.Success)
                {
                    _cachedViews.Remove(id);
                    _openViews.Add(id, existing);
                }

                return cachedResult;
            }

            if (_openViews.TryGetValue(id, out existing))
                return RebindExistingView(id, args, existing);

            MxFairyGuiPackageDescriptor package;
            if (!_packages.TryGet(contract.Descriptor.PackageKey, out package))
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewCreateFailed, "FairyGUI package descriptor is not registered: " + contract.Descriptor.PackageKey + ".");

            MxFairyGuiPackageLoadResult loadResult = _resources.LoadPackage(package);
            if (!loadResult.Success)
                return MxFairyGuiResourceBridgeUiMapping.ToOpenFailure(loadResult);

            return OpenLoaded(id, args, contract, loadResult);
        }

        public MxUiOpenOperation OpenAsync<TArgs>(MxUiViewId id, TArgs args)
        {
            if (!id.IsValid)
            {
                return MxUiOpenOperation.CompletedWith(
                    MxUiOpenResult.Fail(MxUiOpenErrorCode.InvalidViewId, "UI view id is required."));
            }

            MxFairyGuiStageLayout.EnsureDefaultContentScaleFactor();

            if (!_contracts.TryGet(id, out MxUiViewContract contract))
            {
                return MxUiOpenOperation.CompletedWith(MxUiOpenResult.Fail(
                    MxUiOpenErrorCode.ViewNotFound,
                    "UI view contract is not registered: " + id + "."));
            }

            if (!ValidateViewModelType<TArgs>(contract, out string failure))
            {
                return MxUiOpenOperation.CompletedWith(
                    MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewCreateFailed, failure));
            }

            if (_pendingOpens.TryGetValue(id, out MxUiOpenOperation pending))
                return pending;

            if (_cachedViews.ContainsKey(id) || _openViews.ContainsKey(id))
                return MxUiOpenOperation.CompletedWith(Open(id, args));

            if (!_packages.TryGet(contract.Descriptor.PackageKey, out MxFairyGuiPackageDescriptor package))
            {
                return MxUiOpenOperation.CompletedWith(MxUiOpenResult.Fail(
                    MxUiOpenErrorCode.ViewCreateFailed,
                    "FairyGUI package descriptor is not registered: " + contract.Descriptor.PackageKey + "."));
            }

            IResourceOperation<MxFairyGuiPackageLoadResult> packageLoad =
                _resources.LoadPackageAsync(package);
            var driver = new AsyncOpenDriver<TArgs>(this, id, args, contract, packageLoad);
            var operation = new MxUiOpenOperation(driver);
            _pendingOpens.Add(id, operation);
            return operation;
        }

        private MxUiOpenResult OpenLoaded<TArgs>(
            MxUiViewId id,
            TArgs args,
            MxUiViewContract contract,
            MxFairyGuiPackageLoadResult loadResult)
        {
            MxFairyGuiPackageLoadScope scope = loadResult.Scope;
            string failure;

            if (!_host.EnsurePackage(scope, out failure))
                return FailAndReleaseScope(scope, MxUiOpenErrorCode.ViewCreateFailed, failure);

            IMxFairyGuiComponentHandle component;
            if (!_host.TryCreateComponent(scope.PackageId, contract.Descriptor.ComponentName, out component, out failure))
                return FailAndReleasePackage(scope, MxUiOpenErrorCode.ViewCreateFailed, failure);

            try
            {
                if (!_bindings.TryBind(id, component, args, out failure))
                {
                    component.Dispose();
                    return FailAndReleasePackage(scope, MxUiOpenErrorCode.ViewCreateFailed, failure);
                }

                var view = new MxFairyGuiView<TArgs>(
                    contract.Descriptor,
                    component,
                    scope,
                    _host,
                    _layerHost,
                    _inputBridge,
                    _transitionController);
                view.Bind(args);
                view.Show();
                _openViews.Add(id, view);
                return MxUiOpenResult.Opened(view);
            }
            catch (Exception ex)
            {
                component.Dispose();
                return FailAndReleasePackage(scope, MxUiOpenErrorCode.ViewCreateFailed, FormatBindingException(ex));
            }
        }

        public bool Close(MxUiViewId id)
        {
            _popupService?.HideOwner(id);
            if (_pendingOpens.TryGetValue(id, out MxUiOpenOperation pending))
                return pending.Cancel("UI view open was cancelled by Close().");

            IMxUiView view;
            if (!_openViews.TryGetValue(id, out view))
                return false;

            _openViews.Remove(id);
            MxUiViewContract contract;
            if (_contracts.TryGet(id, out contract) && contract.Descriptor.KeepAlive)
            {
                view.Hide();
                _cachedViews[id] = view;
                return true;
            }

            view.Dispose();
            return true;
        }

        public bool IsOpen(MxUiViewId id)
        {
            return _openViews.ContainsKey(id);
        }

        /// <summary>Disposes one open or cached view regardless of KeepAlive, or cancels its pending open.</summary>
        /// <remarks>LowFreqAlloc. Use when an owner is released or reconfigured; unrelated views are retained.</remarks>
        public bool ReleaseView(MxUiViewId id)
        {
            _popupService?.HideOwner(id);
            if (_pendingOpens.TryGetValue(id, out MxUiOpenOperation pending))
                return pending.Cancel("UI view open was cancelled by ReleaseView().");

            if (_openViews.TryGetValue(id, out IMxUiView view))
                _openViews.Remove(id);
            else if (_cachedViews.TryGetValue(id, out view))
                _cachedViews.Remove(id);
            else
                return false;

            view.Dispose();
            return true;
        }

        public bool IsCached(MxUiViewId id)
        {
            return _cachedViews.ContainsKey(id);
        }

        public bool TryGetOpenView(MxUiViewId id, out IMxUiView view)
        {
            return _openViews.TryGetValue(id, out view);
        }

        public int CloseSceneViews()
        {
            int closed = 0;
            closed += CloseSceneViews(_openViews);
            closed += CloseSceneViews(_cachedViews);
            return closed;
        }

        /// <remarks>LowFreqAlloc; intended for runtime-shell teardown.</remarks>
        public void Dispose()
        {
            _popupService?.HideAll(MxFairyGuiPopupCloseReason.OwnerClosed);
            while (_pendingOpens.Count > 0)
            {
                MxUiViewId pendingId = default;
                MxUiOpenOperation pending = null;
                foreach (System.Collections.Generic.KeyValuePair<MxUiViewId, MxUiOpenOperation> entry in _pendingOpens)
                {
                    pendingId = entry.Key;
                    pending = entry.Value;
                    break;
                }

                if (pending == null || !pending.Cancel("FairyGUI navigator was disposed."))
                    _pendingOpens.Remove(pendingId);
            }

            DisposeViews(_openViews);
            DisposeViews(_cachedViews);
        }

        private MxUiOpenResult RebindExistingView<TArgs>(MxUiViewId id, TArgs args, IMxUiView existing)
        {
            var typedExisting = existing as MxFairyGuiView<TArgs>;
            if (typedExisting == null)
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewCreateFailed, "FairyGUI open args do not match existing view model type for: " + id + ".");

            string failure;
            try
            {
                if (!_bindings.TryBind(id, typedExisting.Component, args, out failure))
                    return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewCreateFailed, failure);

                typedExisting.Bind(args);
            }
            catch (Exception ex)
            {
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewCreateFailed, FormatBindingException(ex));
            }

            existing.Show();
            return MxUiOpenResult.Opened(existing);
        }

        private int CloseSceneViews(System.Collections.Generic.Dictionary<MxUiViewId, IMxUiView> views)
        {
            if (views.Count == 0)
                return 0;

            var ids = new System.Collections.Generic.List<MxUiViewId>(views.Keys);
            int closed = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                MxUiViewId id = ids[i];
                MxUiViewContract contract;
                if (!_contracts.TryGet(id, out contract) || !contract.Descriptor.CloseOnSceneChange)
                    continue;

                IMxUiView view;
                if (!views.TryGetValue(id, out view))
                    continue;

                views.Remove(id);
                _popupService?.HideOwner(id);
                view.Dispose();
                closed++;
            }

            return closed;
        }

        private static void DisposeViews(
            System.Collections.Generic.Dictionary<MxUiViewId, IMxUiView> views)
        {
            if (views.Count == 0)
                return;

            foreach (System.Collections.Generic.KeyValuePair<MxUiViewId, IMxUiView> entry in views)
                entry.Value?.Dispose();

            views.Clear();
        }

        private void RemovePending(MxUiViewId id)
        {
            _pendingOpens.Remove(id);
        }

        private sealed class AsyncOpenDriver<TArgs> : IMxUiOpenOperationDriver
        {
            private readonly MxFairyGuiNavigator _owner;
            private readonly MxUiViewId _id;
            private readonly TArgs _args;
            private readonly MxUiViewContract _contract;
            private readonly IResourceOperation<MxFairyGuiPackageLoadResult> _packageLoad;

            public AsyncOpenDriver(
                MxFairyGuiNavigator owner,
                MxUiViewId id,
                TArgs args,
                MxUiViewContract contract,
                IResourceOperation<MxFairyGuiPackageLoadResult> packageLoad)
            {
                _owner = owner;
                _id = id;
                _args = args;
                _contract = contract;
                _packageLoad = packageLoad;
            }

            public void Pump(MxUiOpenOperation operation)
            {
                if (!_packageLoad.IsDone)
                    return;

                _owner.RemovePending(_id);
                if (_packageLoad.IsCancelled)
                {
                    operation.Cancel("FairyGUI package load was cancelled.");
                    return;
                }

                ResourceLoadResult<MxFairyGuiPackageLoadResult> resourceResult = _packageLoad.Result;
                if (!resourceResult.Success)
                {
                    operation.Complete(MxUiOpenResult.Fail(
                        resourceResult.Error.Code == ResourceErrorCode.Cancelled
                            ? MxUiOpenErrorCode.OperationCancelled
                            : MxUiOpenErrorCode.ViewCreateFailed,
                        resourceResult.Error.Message));
                    return;
                }

                MxFairyGuiPackageLoadResult packageResult = resourceResult.Value;
                operation.Complete(packageResult.Success
                    ? _owner.OpenLoaded(_id, _args, _contract, packageResult)
                    : MxFairyGuiResourceBridgeUiMapping.ToOpenFailure(packageResult));
            }

            public void Cancel()
            {
                if (!_packageLoad.IsDone)
                    _packageLoad.Cancel();

                _owner.RemovePending(_id);
            }
        }

        private static MxUiOpenResult FailAndReleaseScope(
            MxFairyGuiPackageLoadScope scope,
            MxUiOpenErrorCode code,
            string message)
        {
            if (scope != null)
                scope.Release();

            return MxUiOpenResult.Fail(code, message);
        }

        private MxUiOpenResult FailAndReleasePackage(
            MxFairyGuiPackageLoadScope scope,
            MxUiOpenErrorCode code,
            string message)
        {
            if (scope != null)
                _host.ReleasePackage(scope.PackageId);

            return MxUiOpenResult.Fail(code, message);
        }

        private static bool ValidateViewModelType<TArgs>(MxUiViewContract contract, out string failure)
        {
            string contractType = contract.ViewModelType;
            if (string.IsNullOrWhiteSpace(contractType))
            {
                failure = "FairyGUI view model type is required for: " + contract.Descriptor.Id + ".";
                return false;
            }

            Type argsType = typeof(TArgs);
            if (string.Equals(contractType, argsType.FullName, StringComparison.Ordinal)
                || string.Equals(contractType, argsType.AssemblyQualifiedName, StringComparison.Ordinal))
            {
                failure = string.Empty;
                return true;
            }

            failure = "FairyGUI view model type does not match open args for: " + contract.Descriptor.Id + ". Expected "
                + contractType + ", got " + argsType.FullName + ".";
            return false;
        }

        private static string FormatBindingException(Exception ex)
        {
            if (ex == null)
                return "FairyGUI view binding failed.";

            string message = "FairyGUI view binding failed: "
                + ex.GetType().Name
                + ": "
                + ex.Message;

            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                string stackTrace = ex.StackTrace.Replace("\r\n", "\n");
                int newline = stackTrace.IndexOf('\n');
                string firstFrame = newline >= 0 ? stackTrace.Substring(0, newline) : stackTrace;
                message += " at " + firstFrame.Trim();
            }

            return message;
        }
    }

    public sealed class MxFairyGuiFocusInputBridge
    {
        private readonly MxFairyGuiNavigator _navigator;
        private readonly IMxFairyGuiLayerHost _layerHost;
        private readonly MxFairyGuiCancelCommandBridge _cancelBridge;
        private readonly IMxFairyGuiPopupService _popupService;

        public MxFairyGuiFocusInputBridge(
            MxFairyGuiNavigator navigator,
            IMxFairyGuiLayerHost layerHost,
            MxFairyGuiCancelCommandBridge cancelBridge = null,
            IMxFairyGuiPopupService popupService = null)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            _layerHost = layerHost ?? throw new ArgumentNullException(nameof(layerHost));
            _cancelBridge = cancelBridge;
            _popupService = popupService;
        }

        public MxFairyGuiFocusInputBridge(
            IMxFairyGuiLayerHost layerHost,
            MxFairyGuiCancelCommandBridge cancelBridge = null,
            IMxFairyGuiPopupService popupService = null)
        {
            _layerHost = layerHost ?? throw new ArgumentNullException(nameof(layerHost));
            _cancelBridge = cancelBridge;
            _popupService = popupService;
        }

        public bool Process(MxUiViewId viewId, MxFairyGuiFocusNavigationIntent intent, object payload = null)
        {
            if (intent == MxFairyGuiFocusNavigationIntent.Cancel)
            {
                if (_popupService != null
                    && _popupService.HideTop(MxFairyGuiPopupCloseReason.Cancel))
                {
                    return true;
                }

                return _cancelBridge != null && _cancelBridge.ProcessCancel(payload);
            }

            Fgui.GComponent popupRoot = _popupService?.TopPopup?.asCom;
            if (popupRoot != null)
            {
                switch (intent)
                {
                    case MxFairyGuiFocusNavigationIntent.Next:
                        return MxFairyGuiFocusNavigation.MoveNext(popupRoot);
                    case MxFairyGuiFocusNavigationIntent.Previous:
                        return MxFairyGuiFocusNavigation.MovePrevious(popupRoot);
                    case MxFairyGuiFocusNavigationIntent.Submit:
                        return MxFairyGuiFocusNavigation.Submit(popupRoot);
                }
            }

            MxUiViewId topModal;
            if (_layerHost.TryGetTopModal(out topModal) && viewId != topModal)
                return false;

            Fgui.GComponent root = TryGetRoot(viewId);
            if (root == null)
                return false;

            switch (intent)
            {
                case MxFairyGuiFocusNavigationIntent.Next:
                    return MxFairyGuiFocusNavigation.MoveNext(root);
                case MxFairyGuiFocusNavigationIntent.Previous:
                    return MxFairyGuiFocusNavigation.MovePrevious(root);
                case MxFairyGuiFocusNavigationIntent.Submit:
                    return MxFairyGuiFocusNavigation.Submit(root);
                default:
                    return false;
            }
        }

        private Fgui.GComponent TryGetRoot(MxUiViewId viewId)
        {
            IMxFairyGuiComponentHandle component;
            if (_layerHost.TryGetVisibleComponent(viewId, out component))
                return TryGetRoot(component);

            if (_navigator == null)
                return null;

            IMxUiView view;
            if (!_navigator.TryGetOpenView(viewId, out view))
                return null;

            var fairyGuiView = view as IMxFairyGuiView;
            return fairyGuiView != null ? TryGetRoot(fairyGuiView.Component) : null;
        }

        private static Fgui.GComponent TryGetRoot(IMxFairyGuiComponentHandle component)
        {
            var handle = component as MxFairyGuiComponentHandle;
            return handle != null ? handle.Component : null;
        }
    }
}
