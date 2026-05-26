using System;
using MxFramework.UI;

namespace MxFramework.UI.FairyGui
{
    public sealed class MxFairyGuiNavigator : IMxUiNavigator
    {
        private readonly MxUiViewContractRegistry _contracts;
        private readonly MxFairyGuiPackageCatalog _packages;
        private readonly MxFairyGuiResourceBridge _resources;
        private readonly IMxFairyGuiHost _host;
        private readonly IMxFairyGuiLayerHost _layerHost;
        private readonly IMxFairyGuiInputContextBridge _inputBridge;
        private readonly MxFairyGuiViewBindingRegistry _bindings;
        private readonly System.Collections.Generic.Dictionary<MxUiViewId, IMxUiView> _openViews =
            new System.Collections.Generic.Dictionary<MxUiViewId, IMxUiView>();
        private readonly System.Collections.Generic.Dictionary<MxUiViewId, IMxUiView> _cachedViews =
            new System.Collections.Generic.Dictionary<MxUiViewId, IMxUiView>();

        public MxFairyGuiNavigator(
            MxUiViewContractRegistry contracts,
            MxFairyGuiPackageCatalog packages,
            MxFairyGuiResourceBridge resources,
            IMxFairyGuiHost host,
            MxFairyGuiViewBindingRegistry bindings = null,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null)
        {
            _contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _layerHost = layerHost ?? new MxFairyGuiLayerHost();
            _inputBridge = inputBridge ?? MxFairyGuiNullInputContextBridge.Instance;
            _bindings = bindings ?? new MxFairyGuiViewBindingRegistry();
        }

        public MxUiOpenResult Open<TArgs>(MxUiViewId id, TArgs args)
        {
            if (!id.IsValid)
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.InvalidViewId, "UI view id is required.");

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

            MxFairyGuiPackageLoadScope scope = loadResult.Scope;
            if (!_host.EnsurePackage(scope, out failure))
                return FailAndRelease(scope, MxUiOpenErrorCode.ViewCreateFailed, failure);

            IMxFairyGuiComponentHandle component;
            if (!_host.TryCreateComponent(package.PackageId, contract.Descriptor.ComponentName, out component, out failure))
                return FailAndRelease(scope, MxUiOpenErrorCode.ViewCreateFailed, failure);

            try
            {
                if (!_bindings.TryBind(id, component, args, out failure))
                {
                    component.Dispose();
                    return FailAndRelease(scope, MxUiOpenErrorCode.ViewCreateFailed, failure);
                }

                var view = new MxFairyGuiView<TArgs>(contract.Descriptor, component, scope, _host, _layerHost, _inputBridge);
                view.Bind(args);
                view.Show();
                _openViews.Add(id, view);
                return MxUiOpenResult.Opened(view);
            }
            catch (Exception ex)
            {
                component.Dispose();
                return FailAndRelease(scope, MxUiOpenErrorCode.ViewCreateFailed, "FairyGUI view binding failed: " + ex.Message);
            }
        }

        public MxUiOpenOperation OpenAsync<TArgs>(MxUiViewId id, TArgs args)
        {
            return MxUiOpenOperation.CompletedWith(Open(id, args));
        }

        public bool Close(MxUiViewId id)
        {
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

        public bool IsCached(MxUiViewId id)
        {
            return _cachedViews.ContainsKey(id);
        }

        public int CloseSceneViews()
        {
            int closed = 0;
            closed += CloseSceneViews(_openViews);
            closed += CloseSceneViews(_cachedViews);
            return closed;
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
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewCreateFailed, "FairyGUI view binding failed: " + ex.Message);
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
                view.Dispose();
                closed++;
            }

            return closed;
        }

        private MxUiOpenResult FailAndRelease(MxFairyGuiPackageLoadScope scope, MxUiOpenErrorCode code, string message)
        {
            if (scope != null)
            {
                scope.Release();
                _host.ReleasePackage(scope.PackageId);
            }

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
    }
}
