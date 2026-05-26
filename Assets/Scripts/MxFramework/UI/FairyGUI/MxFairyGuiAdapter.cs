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

    public interface IMxFairyGuiLayerHost
    {
        void Show(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component);
        void Hide(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component);
        bool TryGetTopModal(out MxUiViewId viewId);
    }

    public interface IMxFairyGuiInputContextBridge
    {
        IDisposable Enter(MxUiViewDescriptor descriptor);
    }

    public sealed class MxFairyGuiNullInputContextBridge : IMxFairyGuiInputContextBridge
    {
        public static readonly MxFairyGuiNullInputContextBridge Instance = new MxFairyGuiNullInputContextBridge();

        private MxFairyGuiNullInputContextBridge()
        {
        }

        public IDisposable Enter(MxUiViewDescriptor descriptor)
        {
            return NullScope.Instance;
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();

            private NullScope()
            {
            }

            public void Dispose()
            {
            }
        }
    }

    public sealed class MxFairyGuiInputContextBridge : IMxFairyGuiInputContextBridge
    {
        private readonly Func<MxUiViewDescriptor, IDisposable> _enterModal;

        public MxFairyGuiInputContextBridge(Func<MxUiViewDescriptor, IDisposable> enterModal)
        {
            _enterModal = enterModal ?? throw new ArgumentNullException(nameof(enterModal));
        }

        public IDisposable Enter(MxUiViewDescriptor descriptor)
        {
            if (descriptor == null || !descriptor.Modal)
                return MxFairyGuiNullInputContextBridge.Instance.Enter(descriptor);

            return _enterModal(descriptor) ?? MxFairyGuiNullInputContextBridge.Instance.Enter(descriptor);
        }
    }

    public static class MxFairyGuiCommandIds
    {
        public const string Cancel = "ui.cancel";
    }

    public sealed class MxFairyGuiModalCommandGate : IMxUiCommandSink
    {
        private readonly IMxUiCommandSink _inner;
        private readonly IMxFairyGuiLayerHost _layerHost;

        public MxFairyGuiModalCommandGate(IMxUiCommandSink inner, IMxFairyGuiLayerHost layerHost)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _layerHost = layerHost ?? throw new ArgumentNullException(nameof(layerHost));
        }

        public int ForwardedCount { get; private set; }
        public int BlockedCount { get; private set; }

        public void Enqueue(MxUiCommand command)
        {
            MxUiViewId topModal;
            if (_layerHost.TryGetTopModal(out topModal)
                && (!command.SourceViewId.IsValid || command.SourceViewId != topModal))
            {
                BlockedCount++;
                return;
            }

            ForwardedCount++;
            _inner.Enqueue(command);
        }
    }

    public sealed class MxFairyGuiCancelCommandBridge
    {
        private readonly IMxFairyGuiLayerHost _layerHost;
        private readonly IMxUiCommandSink _commands;
        private readonly string _commandId;

        public MxFairyGuiCancelCommandBridge(
            IMxFairyGuiLayerHost layerHost,
            IMxUiCommandSink commands,
            string commandId = MxFairyGuiCommandIds.Cancel)
        {
            _layerHost = layerHost ?? throw new ArgumentNullException(nameof(layerHost));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _commandId = string.IsNullOrWhiteSpace(commandId) ? MxFairyGuiCommandIds.Cancel : commandId;
        }

        public bool ProcessCancel(object payload = null)
        {
            MxUiViewId topModal;
            if (!_layerHost.TryGetTopModal(out topModal))
                return false;

            _commands.Enqueue(new MxUiCommand(topModal, _commandId, payload));
            return true;
        }
    }

    public sealed class MxFairyGuiLayerHost : IMxFairyGuiLayerHost, IDisposable
    {
        private static readonly MxUiLayer[] ProductizedLayers =
        {
            MxUiLayer.Background,
            MxUiLayer.Hud,
            MxUiLayer.Panel,
            MxUiLayer.Popup,
            MxUiLayer.Modal,
            MxUiLayer.Toast,
            MxUiLayer.Debug
        };

        private readonly Dictionary<MxUiLayer, Fgui.GComponent> _layerRoots = new Dictionary<MxUiLayer, Fgui.GComponent>();
        private readonly List<MxUiViewId> _modalStack = new List<MxUiViewId>();

        public IReadOnlyList<MxUiViewId> ModalStack => _modalStack;
        public IReadOnlyList<MxUiLayer> LayerOrder => ProductizedLayers;

        public void Show(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component)
        {
            if (descriptor == null || component == null)
                return;

            EnsureLayerRoots();
            var concrete = component as MxFairyGuiComponentHandle;
            if (concrete != null)
            {
                Fgui.GComponent layerRoot = _layerRoots[descriptor.Layer];
                if (concrete.Component.parent != layerRoot)
                    layerRoot.AddChild(concrete.Component);
            }

            if (descriptor.Modal && !_modalStack.Contains(descriptor.Id))
                _modalStack.Add(descriptor.Id);

            component.Show();
        }

        public void Hide(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component)
        {
            if (descriptor == null || component == null)
                return;

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

        public void EnsureLayerRoots()
        {
            for (int i = 0; i < ProductizedLayers.Length; i++)
                EnsureLayerRoot(ProductizedLayers[i]);
        }

        public bool TryGetLayerRoot(MxUiLayer layer, out Fgui.GComponent root)
        {
            return _layerRoots.TryGetValue(layer, out root) && root != null && root.parent == Fgui.GRoot.inst;
        }

        public void Dispose()
        {
            foreach (KeyValuePair<MxUiLayer, Fgui.GComponent> entry in _layerRoots)
            {
                if (entry.Value != null)
                    entry.Value.RemoveFromParent();
            }

            _layerRoots.Clear();
            _modalStack.Clear();
        }

        private void EnsureLayerRoot(MxUiLayer layer)
        {
            Fgui.GComponent root;
            if (_layerRoots.TryGetValue(layer, out root) && root.parent == Fgui.GRoot.inst)
                return;

            string rootName = "MxFairyGuiLayer_" + layer;
            Fgui.GObject existing = Fgui.GRoot.inst.GetChild(rootName);
            root = existing as Fgui.GComponent;
            if (root != null)
            {
                _layerRoots[layer] = root;
                return;
            }

            root = new Fgui.GComponent
            {
                name = rootName
            };
            root.SetSize(Fgui.GRoot.inst.width, Fgui.GRoot.inst.height);
            _layerRoots[layer] = root;

            InsertLayerRoot(root, layer);
        }

        private void InsertLayerRoot(Fgui.GComponent root, MxUiLayer layer)
        {
            int index = 0;
            foreach (KeyValuePair<MxUiLayer, Fgui.GComponent> entry in _layerRoots)
            {
                if (entry.Value == root || entry.Value.parent != Fgui.GRoot.inst)
                    continue;

                if ((int)entry.Key < (int)layer)
                    index++;
            }

            Fgui.GRoot.inst.AddChildAt(root, index);
        }
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
        private readonly MxUiViewDescriptor _descriptor;
        private readonly IMxFairyGuiComponentHandle _component;
        private readonly MxFairyGuiPackageLoadScope _scope;
        private readonly IMxFairyGuiHost _host;
        private readonly IMxFairyGuiLayerHost _layerHost;
        private readonly IMxFairyGuiInputContextBridge _inputBridge;
        private readonly MxUiLifecycle _lifecycle;
        private IDisposable _inputScope;

        public MxFairyGuiView(
            MxUiViewDescriptor descriptor,
            IMxFairyGuiComponentHandle component,
            MxFairyGuiPackageLoadScope scope,
            IMxFairyGuiHost host,
            IMxFairyGuiLayerHost layerHost,
            IMxFairyGuiInputContextBridge inputBridge)
        {
            _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Id = descriptor.Id;
            _component = component ?? throw new ArgumentNullException(nameof(component));
            _scope = scope;
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _layerHost = layerHost ?? throw new ArgumentNullException(nameof(layerHost));
            _inputBridge = inputBridge ?? MxFairyGuiNullInputContextBridge.Instance;
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

            EnterInputScope();
            try
            {
                _layerHost.Show(_descriptor, _component);
            }
            catch
            {
                ExitInputScope();
                throw;
            }
        }

        public void Hide()
        {
            if (!_lifecycle.Hide())
                return;

            try
            {
                _layerHost.Hide(_descriptor, _component);
            }
            finally
            {
                ExitInputScope();
            }
        }

        public void Dispose()
        {
            bool wasVisible = _lifecycle.IsVisible;
            if (!_lifecycle.Dispose())
                return;

            try
            {
                if (wasVisible)
                    _layerHost.Hide(_descriptor, _component);
            }
            finally
            {
                ExitInputScope();
                _component.Dispose();
                if (_scope != null)
                {
                    _scope.Release();
                    _host.ReleasePackage(_scope.PackageId);
                }
            }
        }

        private void EnterInputScope()
        {
            ExitInputScope();
            _inputScope = _inputBridge.Enter(_descriptor);
        }

        private void ExitInputScope()
        {
            if (_inputScope == null)
                return;

            _inputScope.Dispose();
            _inputScope = null;
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
