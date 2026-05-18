using System;
using MxFramework.DebugUI;
using MxFramework.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.DebugUI.Toolkit
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Debug UI/Debug UI Overlay Controller")]
    public sealed class DebugUiOverlayController : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private DebugUiVisibility _visibility = DebugUiVisibility.Collapsed;
        [SerializeField] private float _refreshIntervalSeconds = 0.25f;

        private readonly DebugUiSnapshotAggregator _aggregator = new DebugUiSnapshotAggregator();
        private readonly DebugUiOverlayViewModelBinder _binder = new DebugUiOverlayViewModelBinder();
        private FrameworkDebugSourceRegistry _registry;
        private DebugUiDashboardViewModel _lastModel = DebugUiDashboardViewModel.Empty;
        private VisualElement _boundRoot;
        private float _nextRefreshTime;

        public DebugUiVisibility Visibility => _visibility;
        public bool RefreshPaused { get; private set; }
        public DebugUiDashboardViewModel LastModel => _lastModel;
        public FrameworkDebugSourceRegistry Registry => _registry;

        private void Awake()
        {
            EnsureRegistry();
            EnsureDocument();
        }

        private void OnEnable()
        {
            EnsureRegistry();
            EnsureDocument();
            RegisterBinderCallbacks();
            RefreshNow();
        }

        private void OnDisable()
        {
            UnregisterBinderCallbacks();
        }

        private void Update()
        {
            EnsureDocument();
            if (RefreshPaused || _visibility == DebugUiVisibility.Hidden)
                return;

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            RefreshNow();
        }

        public void Configure(FrameworkDebugSourceRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            RefreshNow();
        }

        public bool RegisterSource(IFrameworkDebugSource source)
        {
            EnsureRegistry();
            bool registered = _registry.Register(source);
            if (registered)
                RefreshNow();

            return registered;
        }

        public bool UnregisterSource(string name)
        {
            EnsureRegistry();
            bool removed = _registry.Unregister(name);
            if (removed)
                RefreshNow();

            return removed;
        }

        public void SetVisibility(DebugUiVisibility visibility)
        {
            _visibility = visibility;
            Bind();
        }

        public void ToggleExpanded()
        {
            SetVisibility(_visibility == DebugUiVisibility.Expanded ? DebugUiVisibility.Collapsed : DebugUiVisibility.Expanded);
        }

        public void SetRefreshPaused(bool paused)
        {
            RefreshPaused = paused;
            Bind();
        }

        public void RefreshNow()
        {
            EnsureRegistry();
            _lastModel = _aggregator.Refresh(_registry);
            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, _refreshIntervalSeconds);
            Bind();
        }

        private void EnsureRegistry()
        {
            if (_registry == null)
                _registry = new FrameworkDebugSourceRegistry();
        }

        private void EnsureDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.panelSettings == null)
                _document.panelSettings = _panelSettings != null ? _panelSettings : ScriptableObject.CreateInstance<PanelSettings>();

            VisualElement root = _document.rootVisualElement;
            if (root == null || ReferenceEquals(root, _boundRoot))
                return;

            _boundRoot = root;
            _binder.Build(root);
            ApplyInlineLayout(root);
            Bind();
        }

        private void Bind()
        {
            if (_binder.Root == null)
                return;

            _binder.Bind(_lastModel, _visibility, RefreshPaused);
        }

        private void RegisterBinderCallbacks()
        {
            _binder.RefreshRequested += RefreshNow;
            _binder.PauseToggled += TogglePause;
            _binder.CloseRequested += Hide;
            _binder.CollapseRequested += Collapse;
            _binder.ExpandRequested += Expand;
        }

        private void UnregisterBinderCallbacks()
        {
            _binder.RefreshRequested -= RefreshNow;
            _binder.PauseToggled -= TogglePause;
            _binder.CloseRequested -= Hide;
            _binder.CollapseRequested -= Collapse;
            _binder.ExpandRequested -= Expand;
        }

        private void TogglePause()
        {
            SetRefreshPaused(!RefreshPaused);
        }

        private void Hide()
        {
            SetVisibility(DebugUiVisibility.Hidden);
        }

        private void Collapse()
        {
            SetVisibility(DebugUiVisibility.Collapsed);
        }

        private void Expand()
        {
            SetVisibility(DebugUiVisibility.Expanded);
        }

        private static void ApplyInlineLayout(VisualElement root)
        {
            VisualElement panel = root.Q<VisualElement>(DebugUiToolkitThemeTokens.RootName);
            if (panel == null)
                return;

            panel.style.position = Position.Absolute;
            panel.style.right = 12;
            panel.style.top = 12;
            panel.style.maxWidth = 520;
            panel.style.maxHeight = 720;
            panel.style.paddingLeft = 8;
            panel.style.paddingRight = 8;
            panel.style.paddingTop = 8;
            panel.style.paddingBottom = 8;
        }
    }
}
