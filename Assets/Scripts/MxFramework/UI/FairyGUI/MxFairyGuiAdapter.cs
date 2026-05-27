using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Fgui = global::FairyGUI;
using MxFramework.Resources;
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

    public enum MxFairyGuiFocusNavigationIntent
    {
        Next,
        Previous,
        Submit,
        Cancel
    }

    public enum MxFairyGuiTransitionCloseReason
    {
        Hide,
        Dispose,
        ShowFailed
    }

    public interface IMxFairyGuiViewTransitionController
    {
        void PlayShow(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component);
        void PlayHide(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component);
        void Cancel(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component, MxFairyGuiTransitionCloseReason reason);
    }

    public sealed class MxFairyGuiNullViewTransitionController : IMxFairyGuiViewTransitionController
    {
        public static readonly MxFairyGuiNullViewTransitionController Instance = new MxFairyGuiNullViewTransitionController();

        private MxFairyGuiNullViewTransitionController()
        {
        }

        public void PlayShow(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component)
        {
        }

        public void PlayHide(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component)
        {
        }

        public void Cancel(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component, MxFairyGuiTransitionCloseReason reason)
        {
        }
    }

    public sealed class MxFairyGuiViewTransitionController : IMxFairyGuiViewTransitionController
    {
        public const string DefaultShowTransitionName = "show";
        public const string DefaultHideTransitionName = "hide";

        private readonly string _showTransitionName;
        private readonly string _hideTransitionName;
        private readonly Dictionary<IMxFairyGuiComponentHandle, Fgui.Transition> _activeTransitions =
            new Dictionary<IMxFairyGuiComponentHandle, Fgui.Transition>();

        public MxFairyGuiViewTransitionController(
            string showTransitionName = DefaultShowTransitionName,
            string hideTransitionName = DefaultHideTransitionName)
        {
            _showTransitionName = string.IsNullOrWhiteSpace(showTransitionName) ? DefaultShowTransitionName : showTransitionName;
            _hideTransitionName = string.IsNullOrWhiteSpace(hideTransitionName) ? DefaultHideTransitionName : hideTransitionName;
        }

        public void PlayShow(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component)
        {
            Play(component, _showTransitionName);
        }

        public void PlayHide(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component)
        {
            Play(component, _hideTransitionName);
        }

        public void Cancel(MxUiViewDescriptor descriptor, IMxFairyGuiComponentHandle component, MxFairyGuiTransitionCloseReason reason)
        {
            Fgui.Transition active;
            if (component == null || !_activeTransitions.TryGetValue(component, out active))
                return;

            _activeTransitions.Remove(component);
            if (active != null && active.playing)
                active.Stop(false, false);
        }

        private void Play(IMxFairyGuiComponentHandle component, string transitionName)
        {
            if (component == null || component.IsDisposed || string.IsNullOrWhiteSpace(transitionName))
                return;

            Cancel(null, component, MxFairyGuiTransitionCloseReason.Hide);

            var concrete = component as MxFairyGuiComponentHandle;
            if (concrete == null)
                return;

            Fgui.Transition transition = concrete.Component.GetTransition(transitionName);
            if (transition == null)
                return;

            _activeTransitions[component] = transition;
            transition.Play(() => _activeTransitions.Remove(component));
        }
    }

    public sealed class MxFairyGuiFocusNavigationMetadata
    {
        private readonly List<string> _orderedChildNames;

        public MxFairyGuiFocusNavigationMetadata(
            MxUiViewId viewId,
            string defaultChildName,
            IEnumerable<string> orderedChildNames,
            bool wrap = true)
        {
            if (!viewId.IsValid)
                throw new ArgumentException("Focus navigation view id is required.", nameof(viewId));

            ViewId = viewId;
            DefaultChildName = defaultChildName ?? string.Empty;
            Wrap = wrap;
            _orderedChildNames = new List<string>();
            if (orderedChildNames != null)
            {
                foreach (string childName in orderedChildNames)
                {
                    if (!string.IsNullOrWhiteSpace(childName) && !_orderedChildNames.Contains(childName))
                        _orderedChildNames.Add(childName);
                }
            }
        }

        public MxUiViewId ViewId { get; }
        public string DefaultChildName { get; }
        public bool Wrap { get; }
        public IReadOnlyList<string> OrderedChildNames => _orderedChildNames;
    }

    public static class MxFairyGuiFocusNavigation
    {
        private static readonly Dictionary<Fgui.GComponent, MxFairyGuiFocusNavigationMetadata> MetadataByRoot =
            new Dictionary<Fgui.GComponent, MxFairyGuiFocusNavigationMetadata>();

        public static void Configure(Fgui.GComponent root, MxFairyGuiFocusNavigationMetadata metadata)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            MetadataByRoot[root] = metadata;
            for (int i = 0; i < metadata.OrderedChildNames.Count; i++)
            {
                Fgui.GObject child = root.GetChild(metadata.OrderedChildNames[i]);
                if (child == null)
                    continue;

                child.focusable = true;
                child.tabStop = true;
            }
        }

        public static void Clear(Fgui.GComponent root)
        {
            if (root != null)
                MetadataByRoot.Remove(root);
        }

        public static bool RequestDefaultFocus(Fgui.GComponent root)
        {
            MxFairyGuiFocusNavigationMetadata metadata;
            if (!TryGetMetadata(root, out metadata))
                return false;

            Fgui.GObject target = FindFocusable(root, metadata.DefaultChildName);
            if (target == null)
                target = FindFocusable(root, metadata.OrderedChildNames);

            if (target == null)
                return false;

            target.RequestFocus(true);
            return true;
        }

        public static bool MoveNext(Fgui.GComponent root)
        {
            return Move(root, 1);
        }

        public static bool MovePrevious(Fgui.GComponent root)
        {
            return Move(root, -1);
        }

        public static bool Submit(Fgui.GComponent root)
        {
            if (root == null)
                return false;

            Fgui.GObject focused = FindFocused(root);
            Fgui.GButton button = focused != null ? focused.asButton : null;
            if (button == null || !button.enabled)
                return false;

            button.onClick.Call();
            return true;
        }

        private static bool Move(Fgui.GComponent root, int direction)
        {
            MxFairyGuiFocusNavigationMetadata metadata;
            if (!TryGetMetadata(root, out metadata))
                return false;

            if (metadata.OrderedChildNames.Count == 0)
                return RequestDefaultFocus(root);

            int currentIndex = IndexOf(root, metadata, FindFocused(root));
            if (currentIndex < 0)
                currentIndex = direction < 0 ? metadata.OrderedChildNames.Count : -1;

            for (int step = 1; step <= metadata.OrderedChildNames.Count; step++)
            {
                int index = currentIndex + direction * step;
                if (metadata.Wrap)
                    index = WrapIndex(index, metadata.OrderedChildNames.Count);
                else if (index < 0 || index >= metadata.OrderedChildNames.Count)
                    return false;

                Fgui.GObject target = FindFocusable(root, metadata.OrderedChildNames[index]);
                if (target == null)
                    continue;

                target.RequestFocus(true);
                return true;
            }

            return false;
        }

        private static bool TryGetMetadata(Fgui.GComponent root, out MxFairyGuiFocusNavigationMetadata metadata)
        {
            if (root == null)
            {
                metadata = null;
                return false;
            }

            return MetadataByRoot.TryGetValue(root, out metadata);
        }

        private static int IndexOf(
            Fgui.GComponent root,
            MxFairyGuiFocusNavigationMetadata metadata,
            Fgui.GObject focused)
        {
            if (focused == null)
                return -1;

            for (int i = 0; i < metadata.OrderedChildNames.Count; i++)
            {
                Fgui.GObject child = root.GetChild(metadata.OrderedChildNames[i]);
                if (child == focused)
                    return i;
            }

            return -1;
        }

        private static Fgui.GObject FindFocusable(Fgui.GComponent root, IReadOnlyList<string> names)
        {
            for (int i = 0; i < names.Count; i++)
            {
                Fgui.GObject target = FindFocusable(root, names[i]);
                if (target != null)
                    return target;
            }

            return null;
        }

        private static Fgui.GObject FindFocusable(Fgui.GComponent root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
                return null;

            Fgui.GObject child = root.GetChild(childName);
            if (child == null || !child.visible || !child.enabled || !child.focusable)
                return null;

            return child;
        }

        private static Fgui.GObject FindFocused(Fgui.GComponent root)
        {
            if (root == null)
                return null;

            for (int i = 0; i < root.numChildren; i++)
            {
                Fgui.GObject child = root.GetChildAt(i);
                if (child != null && child.focused)
                    return child;
            }

            return null;
        }

        private static int WrapIndex(int index, int count)
        {
            if (count <= 0)
                return 0;

            while (index < 0)
                index += count;

            while (index >= count)
                index -= count;

            return index;
        }
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

    public interface IMxFairyGuiRuntimeViewRegistration
    {
        MxUiViewId ViewId { get; }
        MxUiViewContract Contract { get; }
        MxFairyGuiPackageDescriptor Package { get; }
        void ApplyTo(
            MxUiViewContractRegistry contracts,
            MxFairyGuiPackageCatalog packages,
            MxFairyGuiViewBindingRegistry bindings);
    }

    public sealed class MxFairyGuiRuntimeViewRegistration<TViewModel> : IMxFairyGuiRuntimeViewRegistration
    {
        private readonly IMxFairyGuiViewBinder<TViewModel> _binder;

        public MxFairyGuiRuntimeViewRegistration(
            MxUiViewContract contract,
            MxFairyGuiPackageDescriptor package,
            IMxFairyGuiViewBinder<TViewModel> binder)
        {
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            Package = package ?? throw new ArgumentNullException(nameof(package));
            _binder = binder ?? throw new ArgumentNullException(nameof(binder));
        }

        public MxUiViewId ViewId => Contract.Descriptor.Id;
        public MxUiViewContract Contract { get; }
        public MxFairyGuiPackageDescriptor Package { get; }

        public void ApplyTo(
            MxUiViewContractRegistry contracts,
            MxFairyGuiPackageCatalog packages,
            MxFairyGuiViewBindingRegistry bindings)
        {
            if (contracts == null)
                throw new ArgumentNullException(nameof(contracts));

            if (packages == null)
                throw new ArgumentNullException(nameof(packages));

            if (bindings == null)
                throw new ArgumentNullException(nameof(bindings));

            contracts.Register(Contract);
            if (!packages.TryGet(Package.PackageId, out _))
                packages.Register(Package);

            bindings.Register(ViewId, _binder);
        }
    }

    public sealed class MxFairyGuiRuntimeCatalog
    {
        private readonly List<IMxFairyGuiRuntimeViewRegistration> _registrations =
            new List<IMxFairyGuiRuntimeViewRegistration>();
        private readonly Dictionary<MxUiViewId, IMxFairyGuiRuntimeViewRegistration> _byViewId =
            new Dictionary<MxUiViewId, IMxFairyGuiRuntimeViewRegistration>();
        private readonly Dictionary<string, MxFairyGuiPackageDescriptor> _packages =
            new Dictionary<string, MxFairyGuiPackageDescriptor>(StringComparer.Ordinal);

        public int ViewCount => _registrations.Count;
        public int PackageCount => _packages.Count;
        public IReadOnlyList<IMxFairyGuiRuntimeViewRegistration> Registrations => _registrations;

        public void Register(IMxFairyGuiRuntimeViewRegistration registration)
        {
            if (registration == null)
                throw new ArgumentNullException(nameof(registration));

            if (!registration.ViewId.IsValid)
                throw new ArgumentException("FairyGUI runtime view id is required.", nameof(registration));

            if (_byViewId.ContainsKey(registration.ViewId))
                throw new ArgumentException("FairyGUI runtime view is already registered: " + registration.ViewId + ".", nameof(registration));

            if (registration.Package == null || string.IsNullOrWhiteSpace(registration.Package.PackageId))
                throw new ArgumentException("FairyGUI runtime package descriptor is required.", nameof(registration));

            string descriptorPackageId = registration.Contract.Descriptor.PackageKey;
            if (!string.Equals(descriptorPackageId, registration.Package.PackageId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "FairyGUI runtime view package does not match descriptor package key for: " + registration.ViewId + ".",
                    nameof(registration));
            }

            MxFairyGuiPackageDescriptor existingPackage;
            if (_packages.TryGetValue(registration.Package.PackageId, out existingPackage)
                && !existingPackage.PackageBytesKey.Equals(registration.Package.PackageBytesKey))
            {
                throw new ArgumentException(
                    "FairyGUI runtime package bytes key conflicts for: " + registration.Package.PackageId + ".",
                    nameof(registration));
            }

            _registrations.Add(registration);
            _byViewId.Add(registration.ViewId, registration);
            if (existingPackage == null)
                _packages.Add(registration.Package.PackageId, registration.Package);
        }

        public bool TryGet(MxUiViewId viewId, out IMxFairyGuiRuntimeViewRegistration registration)
        {
            return _byViewId.TryGetValue(viewId, out registration);
        }

        public IReadOnlyList<MxFairyGuiPackageDescriptor> ListPackages()
        {
            if (_packages.Count == 0)
                return Array.Empty<MxFairyGuiPackageDescriptor>();

            var packages = new List<MxFairyGuiPackageDescriptor>(_packages.Values);
            packages.Sort((left, right) => string.Compare(left.PackageId, right.PackageId, StringComparison.Ordinal));
            return new ReadOnlyCollection<MxFairyGuiPackageDescriptor>(packages);
        }

        public IReadOnlyList<ResourceKey> CollectPreloadKeys()
        {
            var keys = new List<ResourceKey>();
            var unique = new HashSet<ResourceKey>();
            IReadOnlyList<MxFairyGuiPackageDescriptor> packages = ListPackages();
            for (int i = 0; i < packages.Count; i++)
            {
                AddKey(packages[i].PackageBytesKey, keys, unique);
                for (int j = 0; j < packages[i].Resources.Count; j++)
                    AddKey(packages[i].Resources[j].Key, keys, unique);
            }

            return new ReadOnlyCollection<ResourceKey>(keys);
        }

        public ResourcePreloadPlan CreatePreloadPlan(
            string groupId = "mx.fairygui.runtime",
            bool failFast = true,
            int maxConcurrentLoads = 1)
        {
            return new ResourcePreloadPlan(groupId, CollectPreloadKeys(), null, failFast, maxConcurrentLoads);
        }

        public MxFairyGuiRuntimeCatalogDiagnostics CreateDiagnostics(IResourceManager resourceManager = null)
        {
            var issues = new List<MxFairyGuiRuntimeCatalogIssue>();
            IReadOnlyList<MxFairyGuiPackageDescriptor> packages = ListPackages();
            int resourceKeyCount = 0;
            int missingPackageCount = 0;
            int missingResourceCount = 0;

            for (int i = 0; i < packages.Count; i++)
            {
                MxFairyGuiPackageDescriptor package = packages[i];
                resourceKeyCount++;
                if (!package.PackageBytesKey.IsValid)
                {
                    issues.Add(MxFairyGuiRuntimeCatalogIssue.Error(
                        "InvalidPackageBytesKey",
                        default,
                        "Package bytes key is invalid for package: " + package.PackageId + "."));
                }
                else if (resourceManager != null && !resourceManager.Contains(package.PackageBytesKey))
                {
                    missingPackageCount++;
                    issues.Add(MxFairyGuiRuntimeCatalogIssue.Error(
                        "MissingPackageBytes",
                        package.PackageBytesKey,
                        "Package bytes are not registered for package: " + package.PackageId + "."));
                }

                for (int j = 0; j < package.Resources.Count; j++)
                {
                    MxFairyGuiPackageResourceDescriptor resource = package.Resources[j];
                    resourceKeyCount++;
                    if (!resource.Key.IsValid)
                    {
                        issues.Add(MxFairyGuiRuntimeCatalogIssue.Error(
                            "InvalidPackageResourceKey",
                            default,
                            "Package resource key is invalid for package: " + package.PackageId + "."));
                        continue;
                    }

                    if (resource.Required && resourceManager != null && !resourceManager.Contains(resource.Key))
                    {
                        missingResourceCount++;
                        issues.Add(MxFairyGuiRuntimeCatalogIssue.Error(
                            "MissingPackageResource",
                            resource.Key,
                            "Required package resource is not registered for package: " + package.PackageId + "."));
                    }
                }
            }

            ResourceDebugSnapshot resourceSnapshot = resourceManager != null
                ? resourceManager.CreateDebugSnapshot()
                : null;

            return new MxFairyGuiRuntimeCatalogDiagnostics(
                ViewCount,
                PackageCount,
                resourceKeyCount,
                missingPackageCount,
                missingResourceCount,
                issues,
                resourceSnapshot);
        }

        private static void AddKey(ResourceKey key, List<ResourceKey> keys, HashSet<ResourceKey> unique)
        {
            if (!key.IsValid || !unique.Add(key))
                return;

            keys.Add(key);
        }
    }

    public readonly struct MxFairyGuiRuntimeCatalogIssue
    {
        public MxFairyGuiRuntimeCatalogIssue(string code, ResourceKey key, string message, bool isError)
        {
            Code = code ?? string.Empty;
            Key = key;
            Message = message ?? string.Empty;
            IsError = isError;
        }

        public string Code { get; }
        public ResourceKey Key { get; }
        public string Message { get; }
        public bool IsError { get; }

        public static MxFairyGuiRuntimeCatalogIssue Error(string code, ResourceKey key, string message)
        {
            return new MxFairyGuiRuntimeCatalogIssue(code, key, message, true);
        }
    }

    public sealed class MxFairyGuiRuntimeCatalogDiagnostics
    {
        private readonly IReadOnlyList<MxFairyGuiRuntimeCatalogIssue> _issues;

        public MxFairyGuiRuntimeCatalogDiagnostics(
            int viewCount,
            int packageCount,
            int resourceKeyCount,
            int missingPackageCount,
            int missingResourceCount,
            IReadOnlyList<MxFairyGuiRuntimeCatalogIssue> issues,
            ResourceDebugSnapshot resourceSnapshot)
        {
            ViewCount = viewCount < 0 ? 0 : viewCount;
            PackageCount = packageCount < 0 ? 0 : packageCount;
            ResourceKeyCount = resourceKeyCount < 0 ? 0 : resourceKeyCount;
            MissingPackageCount = missingPackageCount < 0 ? 0 : missingPackageCount;
            MissingResourceCount = missingResourceCount < 0 ? 0 : missingResourceCount;
            _issues = issues ?? Array.Empty<MxFairyGuiRuntimeCatalogIssue>();
            ResourceSnapshot = resourceSnapshot;
        }

        public int ViewCount { get; }
        public int PackageCount { get; }
        public int ResourceKeyCount { get; }
        public int MissingPackageCount { get; }
        public int MissingResourceCount { get; }
        public IReadOnlyList<MxFairyGuiRuntimeCatalogIssue> Issues => _issues;
        public ResourceDebugSnapshot ResourceSnapshot { get; }
        public bool Success => MissingPackageCount == 0 && MissingResourceCount == 0 && Issues.Count == 0;
    }

    public sealed class MxFairyGuiRuntimePreloadSurface : IDisposable
    {
        private readonly IResourcePreloadService _preloadService;
        private IResourceOperation<ResourcePreloadResult> _operation;
        private ResourcePreloadResult _lastResult;

        public MxFairyGuiRuntimePreloadSurface(IResourcePreloadService preloadService)
        {
            _preloadService = preloadService ?? throw new ArgumentNullException(nameof(preloadService));
        }

        public IResourceOperation<ResourcePreloadResult> Operation => _operation;
        public ResourcePreloadResult LastResult => _lastResult;
        public bool IsPreloading => _operation != null && !_operation.IsDone;
        public bool HasResult => _lastResult != null;

        public IResourceOperation<ResourcePreloadResult> Preload(
            MxFairyGuiRuntimeCatalog catalog,
            string groupId = "mx.fairygui.runtime",
            bool failFast = true,
            int maxConcurrentLoads = 1,
            CancellationToken cancellationToken = default)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            Release();
            _operation = _preloadService.PreloadAsync(catalog.CreatePreloadPlan(groupId, failFast, maxConcurrentLoads), cancellationToken);
            CaptureResultIfDone();
            return _operation;
        }

        public MxFairyGuiRuntimePreloadDiagnostics CreateDiagnostics()
        {
            CaptureResultIfDone();
            return new MxFairyGuiRuntimePreloadDiagnostics(_operation, _lastResult);
        }

        public void Release()
        {
            if (_operation != null && !_operation.IsDone)
                _operation.Cancel();

            CaptureResultIfDone();
            if (_lastResult != null && _lastResult.Handle != null)
                _preloadService.ReleaseGroup(_lastResult.Handle);

            _operation = null;
            _lastResult = null;
        }

        public void Dispose()
        {
            Release();
        }

        private void CaptureResultIfDone()
        {
            if (_operation == null || !_operation.IsDone)
                return;

            ResourceLoadResult<ResourcePreloadResult> result = _operation.Result;
            if (result.Success)
                _lastResult = result.Value;
        }
    }

    public sealed class MxFairyGuiRuntimePreloadDiagnostics
    {
        public MxFairyGuiRuntimePreloadDiagnostics(
            IResourceOperation<ResourcePreloadResult> operation,
            ResourcePreloadResult result)
        {
            IsRunning = operation != null && !operation.IsDone;
            IsCancelled = operation != null && operation.IsCancelled;
            Progress = operation != null ? operation.Progress : 0f;
            GroupId = result != null ? result.GroupId : string.Empty;
            RequestedCount = result != null ? result.RequestedCount : 0;
            LoadedCount = result != null ? result.LoadedCount : 0;
            FailedCount = result != null ? result.FailedCount : 0;
            Success = result != null && result.Success;
        }

        public bool IsRunning { get; }
        public bool IsCancelled { get; }
        public float Progress { get; }
        public string GroupId { get; }
        public int RequestedCount { get; }
        public int LoadedCount { get; }
        public int FailedCount { get; }
        public bool Success { get; }
    }

    public static class MxFairyGuiRuntimeShellComposition
    {
        public static MxFairyGuiNavigator CreateNavigator(
            IResourceManager resourceManager,
            MxFairyGuiRuntimeCatalog catalog,
            IMxFairyGuiHost host = null,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiViewTransitionController transitionController = null)
        {
            if (resourceManager == null)
                throw new ArgumentNullException(nameof(resourceManager));

            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            var contracts = new MxUiViewContractRegistry();
            var packages = new MxFairyGuiPackageCatalog();
            var bindings = new MxFairyGuiViewBindingRegistry();
            for (int i = 0; i < catalog.Registrations.Count; i++)
                catalog.Registrations[i].ApplyTo(contracts, packages, bindings);

            return new MxFairyGuiNavigator(
                contracts,
                packages,
                new MxFairyGuiResourceBridge(resourceManager),
                host ?? new MxFairyGuiHost(),
                bindings,
                layerHost,
                inputBridge,
                transitionController);
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
        private readonly IMxFairyGuiViewTransitionController _transitionController;
        private readonly MxUiLifecycle _lifecycle;
        private IDisposable _inputScope;

        public MxFairyGuiView(
            MxUiViewDescriptor descriptor,
            IMxFairyGuiComponentHandle component,
            MxFairyGuiPackageLoadScope scope,
            IMxFairyGuiHost host,
            IMxFairyGuiLayerHost layerHost,
            IMxFairyGuiInputContextBridge inputBridge,
            IMxFairyGuiViewTransitionController transitionController = null)
        {
            _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Id = descriptor.Id;
            _component = component ?? throw new ArgumentNullException(nameof(component));
            _scope = scope;
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _layerHost = layerHost ?? throw new ArgumentNullException(nameof(layerHost));
            _inputBridge = inputBridge ?? MxFairyGuiNullInputContextBridge.Instance;
            _transitionController = transitionController ?? MxFairyGuiNullViewTransitionController.Instance;
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
                _transitionController.PlayShow(_descriptor, _component);
            }
            catch
            {
                _transitionController.Cancel(_descriptor, _component, MxFairyGuiTransitionCloseReason.ShowFailed);
                try
                {
                    _layerHost.Hide(_descriptor, _component);
                }
                catch
                {
                }

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
                _transitionController.Cancel(_descriptor, _component, MxFairyGuiTransitionCloseReason.Hide);
                _transitionController.PlayHide(_descriptor, _component);
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
                _transitionController.Cancel(_descriptor, _component, MxFairyGuiTransitionCloseReason.Dispose);
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
            MxFairyGuiFocusNavigation.Clear(_component);
            _component.RemoveFromParent();
            _component.Dispose();
        }
    }
}
