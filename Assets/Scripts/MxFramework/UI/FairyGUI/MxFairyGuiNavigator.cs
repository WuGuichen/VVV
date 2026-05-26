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
        private readonly MxFairyGuiViewBindingRegistry _bindings;
        private readonly System.Collections.Generic.Dictionary<MxUiViewId, IMxUiView> _openViews =
            new System.Collections.Generic.Dictionary<MxUiViewId, IMxUiView>();

        public MxFairyGuiNavigator(
            MxUiViewContractRegistry contracts,
            MxFairyGuiPackageCatalog packages,
            MxFairyGuiResourceBridge resources,
            IMxFairyGuiHost host,
            MxFairyGuiViewBindingRegistry bindings = null)
        {
            _contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _bindings = bindings ?? new MxFairyGuiViewBindingRegistry();
        }

        public MxUiOpenResult Open<TArgs>(MxUiViewId id, TArgs args)
        {
            if (!id.IsValid)
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.InvalidViewId, "UI view id is required.");

            IMxUiView existing;
            if (_openViews.TryGetValue(id, out existing))
            {
                var typedExisting = existing as IMxUiView<TArgs>;
                if (typedExisting != null)
                    typedExisting.Bind(args);

                existing.Show();
                return MxUiOpenResult.Opened(existing);
            }

            MxUiViewContract contract;
            if (!_contracts.TryGet(id, out contract))
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewNotFound, "UI view contract is not registered: " + id + ".");

            MxFairyGuiPackageDescriptor package;
            if (!_packages.TryGet(contract.Descriptor.PackageKey, out package))
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewCreateFailed, "FairyGUI package descriptor is not registered: " + contract.Descriptor.PackageKey + ".");

            MxFairyGuiPackageLoadResult loadResult = _resources.LoadPackage(package);
            if (!loadResult.Success)
                return MxFairyGuiResourceBridgeUiMapping.ToOpenFailure(loadResult);

            MxFairyGuiPackageLoadScope scope = loadResult.Scope;
            string failure;
            if (!_host.EnsurePackage(scope, out failure))
                return FailAndRelease(scope, MxUiOpenErrorCode.ViewCreateFailed, failure);

            IMxFairyGuiComponentHandle component;
            if (!_host.TryCreateComponent(package.PackageId, contract.Descriptor.ComponentName, out component, out failure))
                return FailAndRelease(scope, MxUiOpenErrorCode.ViewCreateFailed, failure);

            try
            {
                if (!string.IsNullOrWhiteSpace(contract.ViewModelType)
                    && !_bindings.TryBind(id, component, args, out failure))
                {
                    component.Dispose();
                    return FailAndRelease(scope, MxUiOpenErrorCode.ViewCreateFailed, failure);
                }

                var view = new MxFairyGuiView<TArgs>(id, component, scope, _host);
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
            view.Dispose();
            return true;
        }

        public bool IsOpen(MxUiViewId id)
        {
            return _openViews.ContainsKey(id);
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
    }
}
