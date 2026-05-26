using System;
using System.Collections.Generic;
using Fgui = global::FairyGUI;
using MxFramework.UI;

namespace MxFramework.UI.FairyGui
{
    public interface IMxFairyGuiComponentHandle : IDisposable
    {
        string PackageId { get; }
        string ComponentName { get; }
        bool IsDisposed { get; }
        void Show();
        void Hide();
    }

    public interface IMxFairyGuiHost
    {
        bool EnsurePackage(MxFairyGuiPackageLoadScope scope, out string failure);
        bool TryCreateComponent(string packageId, string componentName, out IMxFairyGuiComponentHandle handle, out string failure);
        void ReleasePackage(string packageId);
    }

    public interface IMxFairyGuiViewBinder<in TViewModel>
    {
        void Bind(IMxFairyGuiComponentHandle component, TViewModel viewModel);
    }

    public sealed class MxFairyGuiPackageCatalog
    {
        private readonly Dictionary<string, MxFairyGuiPackageDescriptor> _packages =
            new Dictionary<string, MxFairyGuiPackageDescriptor>(StringComparer.Ordinal);

        public int Count => _packages.Count;

        public void Register(MxFairyGuiPackageDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            if (string.IsNullOrWhiteSpace(descriptor.PackageId))
                throw new ArgumentException("Package id is required.", nameof(descriptor));

            if (_packages.ContainsKey(descriptor.PackageId))
                throw new ArgumentException("FairyGUI package is already registered: " + descriptor.PackageId + ".", nameof(descriptor));

            _packages.Add(descriptor.PackageId, descriptor);
        }

        public bool TryGet(string packageId, out MxFairyGuiPackageDescriptor descriptor)
        {
            return _packages.TryGetValue(packageId ?? string.Empty, out descriptor);
        }
    }

    public sealed class MxFairyGuiViewBindingRegistry
    {
        private readonly Dictionary<MxUiViewId, object> _bindings = new Dictionary<MxUiViewId, object>();

        public int Count => _bindings.Count;

        public void Register<TViewModel>(MxUiViewId viewId, IMxFairyGuiViewBinder<TViewModel> binder)
        {
            if (!viewId.IsValid)
                throw new ArgumentException("View id is required.", nameof(viewId));

            if (binder == null)
                throw new ArgumentNullException(nameof(binder));

            if (_bindings.ContainsKey(viewId))
                throw new ArgumentException("FairyGUI view binding is already registered: " + viewId + ".", nameof(viewId));

            _bindings.Add(viewId, binder);
        }

        public bool TryBind<TViewModel>(
            MxUiViewId viewId,
            IMxFairyGuiComponentHandle component,
            TViewModel viewModel,
            out string failure)
        {
            object value;
            if (!_bindings.TryGetValue(viewId, out value))
            {
                failure = "FairyGUI view binding is not registered: " + viewId + ".";
                return false;
            }

            var binder = value as IMxFairyGuiViewBinder<TViewModel>;
            if (binder == null)
            {
                failure = "FairyGUI view binding type does not match open args for: " + viewId + ".";
                return false;
            }

            binder.Bind(component, viewModel);
            failure = string.Empty;
            return true;
        }
    }

    public sealed class MxFairyGuiView<TViewModel> : IMxUiView<TViewModel>
    {
        private readonly IMxFairyGuiComponentHandle _component;
        private readonly MxFairyGuiPackageLoadScope _scope;
        private readonly IMxFairyGuiHost _host;
        private readonly MxUiLifecycle _lifecycle;

        public MxFairyGuiView(
            MxUiViewId id,
            IMxFairyGuiComponentHandle component,
            MxFairyGuiPackageLoadScope scope,
            IMxFairyGuiHost host)
        {
            Id = id;
            _component = component ?? throw new ArgumentNullException(nameof(component));
            _scope = scope;
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _lifecycle = new MxUiLifecycle();
        }

        public MxUiViewId Id { get; }
        public MxUiLifecycle Lifecycle => _lifecycle;
        public IMxFairyGuiComponentHandle Component => _component;
        public TViewModel ViewModel { get; private set; }

        public void Bind(TViewModel model)
        {
            ViewModel = model;
        }

        public void Show()
        {
            if (!_lifecycle.Show())
                return;

            _component.Show();
        }

        public void Hide()
        {
            if (!_lifecycle.Hide())
                return;

            _component.Hide();
        }

        public void Dispose()
        {
            if (!_lifecycle.Dispose())
                return;

            _component.Dispose();
            if (_scope != null)
            {
                _scope.Release();
                _host.ReleasePackage(_scope.PackageId);
            }
        }
    }

    public sealed class MxFairyGuiHost : IMxFairyGuiHost
    {
        private readonly Dictionary<string, int> _packageRefCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _ownedPackages = new HashSet<string>(StringComparer.Ordinal);

        public bool EnsurePackage(MxFairyGuiPackageLoadScope scope, out string failure)
        {
            if (scope == null || scope.PackageHandle == null)
            {
                failure = "FairyGUI package load scope is required.";
                return false;
            }

            string packageId = scope.PackageId;
            if (string.IsNullOrWhiteSpace(packageId))
            {
                failure = "FairyGUI package id is required.";
                return false;
            }

            int refCount;
            if (_packageRefCounts.TryGetValue(packageId, out refCount))
            {
                _packageRefCounts[packageId] = refCount + 1;
                failure = string.Empty;
                return true;
            }

            byte[] bytes = scope.PackageHandle.Value as byte[];
            if (bytes == null)
            {
                failure = "FairyGUI package bytes are missing.";
                return false;
            }

            try
            {
                Fgui.UIPackage package = Fgui.UIPackage.GetByName(packageId);
                bool ownsPackage = package == null;
                if (ownsPackage)
                {
                    package = Fgui.UIPackage.AddPackage(bytes, packageId, (string name, string extension, Type type, out Fgui.DestroyMethod destroyMethod) =>
                        LoadPackageResource(scope, name, extension, type, out destroyMethod));
                }

                if (package == null)
                {
                    failure = "FairyGUI package registration returned null: " + packageId + ".";
                    return false;
                }

                if (ownsPackage)
                    _ownedPackages.Add(packageId);

                _packageRefCounts.Add(packageId, 1);
                failure = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                failure = "FairyGUI package registration failed: " + ex.Message;
                return false;
            }
        }

        public bool TryCreateComponent(string packageId, string componentName, out IMxFairyGuiComponentHandle handle, out string failure)
        {
            handle = null;
            if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(componentName))
            {
                failure = "FairyGUI package id and component name are required.";
                return false;
            }

            try
            {
                Fgui.GObject component = Fgui.UIPackage.CreateObject(packageId, componentName);
                var asComponent = component as Fgui.GComponent;
                if (asComponent == null)
                {
                    component?.Dispose();
                    failure = "FairyGUI component was not found or was not a Fgui.GComponent: " + packageId + "/" + componentName + ".";
                    return false;
                }

                handle = new MxFairyGuiComponentHandle(packageId, componentName, asComponent);
                failure = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                failure = "FairyGUI component creation failed: " + ex.Message;
                return false;
            }
        }

        public void ReleasePackage(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return;

            int refCount;
            if (!_packageRefCounts.TryGetValue(packageId, out refCount))
                return;

            if (refCount > 1)
            {
                _packageRefCounts[packageId] = refCount - 1;
                return;
            }

            _packageRefCounts.Remove(packageId);
            if (_ownedPackages.Remove(packageId))
                Fgui.UIPackage.RemovePackage(packageId);
        }

        private static object LoadPackageResource(MxFairyGuiPackageLoadScope scope, string name, string extension, Type type, out Fgui.DestroyMethod destroyMethod)
        {
            destroyMethod = Fgui.DestroyMethod.None;
            if (scope == null)
                return null;

            string requested = name ?? string.Empty;
            string requestedWithExtension = requested + (extension ?? string.Empty);
            for (int i = 0; i < scope.ResourceHandles.Count; i++)
            {
                object value = scope.ResourceHandles[i].Value;
                if (value == null || (type != null && !type.IsInstanceOfType(value)))
                    continue;

                if (MatchesResource(scope.ResourceHandles[i], requested, requestedWithExtension))
                    return value;
            }

            return null;
        }

        private static bool MatchesResource(MxFramework.Resources.ResourceHandle<object> handle, string requested, string requestedWithExtension)
        {
            if (handle == null)
                return false;

            if (MatchesName(handle.Key.Id, requested, requestedWithExtension))
                return true;

            if (handle.Entry == null)
                return false;

            return MatchesName(handle.Entry.Id, requested, requestedWithExtension)
                || MatchesName(handle.Entry.Address, requested, requestedWithExtension);
        }

        private static bool MatchesName(string candidate, string requested, string requestedWithExtension)
        {
            if (string.IsNullOrEmpty(candidate))
                return false;

            return string.Equals(candidate, requested, StringComparison.Ordinal)
                || string.Equals(candidate, requestedWithExtension, StringComparison.Ordinal)
                || candidate.EndsWith("/" + requested, StringComparison.Ordinal)
                || candidate.EndsWith("/" + requestedWithExtension, StringComparison.Ordinal);
        }
    }

    public sealed class MxFairyGuiComponentHandle : IMxFairyGuiComponentHandle
    {
        private readonly Fgui.GComponent _component;

        public MxFairyGuiComponentHandle(string packageId, string componentName, Fgui.GComponent component)
        {
            PackageId = packageId ?? string.Empty;
            ComponentName = componentName ?? string.Empty;
            _component = component ?? throw new ArgumentNullException(nameof(component));
        }

        public string PackageId { get; }
        public string ComponentName { get; }
        public Fgui.GComponent Component => _component;
        public bool IsDisposed { get; private set; }

        public void Show()
        {
            if (IsDisposed)
                return;

            if (_component.parent == null)
                Fgui.GRoot.inst.AddChild(_component);

            _component.visible = true;
        }

        public void Hide()
        {
            if (IsDisposed)
                return;

            _component.visible = false;
            _component.RemoveFromParent();
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            _component.RemoveFromParent();
            _component.Dispose();
        }
    }
}
