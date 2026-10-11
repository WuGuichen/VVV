using System;
using MxFramework.DebugUI;
using MxFramework.DebugUI.Input;
using MxFramework.Input;
using MxFramework.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    [AddComponentMenu("MxFramework/Game/Character Test/Developer Debug Panel")]
    public sealed class CharacterTestDeveloperDebugPanel : MonoBehaviour, IDebugUiInputTarget, IDebugUiTabNavigationTarget
    {
        private const string DefaultStoryPanelSettingsPath =
            "Assets/UI/MxFramework/Story/StoryRuntimeVerticalSlicePanelSettings.asset";
        private const string DefaultGameplayPanelSettingsPath =
            "Assets/UI/MxFramework/GameplayComponentRuntime/GameplayComponentRuntimePanelSettings.asset";
        private const string DefaultCombatAnimationPanelSettingsPath =
            "Assets/UI/MxFramework/CombatAnimationPanelSettings.asset";

        [SerializeField] private UIDocument _document;
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private DebugUiVisibility _visibility = DebugUiVisibility.Collapsed;
        [SerializeField] private bool _showOnStart = true;
        [SerializeField] private int _initialTabIndex;
        [SerializeField] private float _refreshIntervalSeconds = 0.2f;
        [SerializeField] private CharacterTestDeveloperDebugPanelAnchor _anchor =
            CharacterTestDeveloperDebugPanelAnchor.TopRight;

        private readonly CharacterTestDeveloperDebugSnapshotBuilder _snapshotBuilder =
            new CharacterTestDeveloperDebugSnapshotBuilder();
        private readonly CharacterTestDeveloperDebugView _view = new CharacterTestDeveloperDebugView();
        private readonly DebugUiInputAdapter _inputAdapter = new DebugUiInputAdapter();
        private readonly CharacterTestGizmoPreferences _gizmoPreferences =
            new CharacterTestGizmoPreferences();

        private CharacterTestSliceServices _services;
        private Func<bool> _isPausedProvider;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private IInputProvider _inputProvider;
        private CharacterTestThirdPersonCameraRigBridge _cameraBridge;
        private int _activeTab;
        private float _nextRefreshTime;
        private bool _refreshPaused;
        private bool _viewEventsSubscribed;
        private DebugUiInputAdapterResult _lastInputResult;
        private Func<CharacterTestGuidanceSelectionSnapshot> _guidanceSelectionProvider;
        private Action<string> _guidanceSelectionSink;

        public DebugUiVisibility Visibility => _visibility;
        public int ActiveTab => _activeTab;
        public CharacterTestDeveloperDebugPanelAnchor Anchor => _anchor;
        public bool RefreshPaused => _refreshPaused;
        public DebugUiInputAdapterResult LastInputResult => _lastInputResult;

        public void Configure(
            GameSlice slice,
            Func<bool> isPausedProvider,
            PanelSettings panelSettings = null,
            DebugUiVisibility? initialVisibility = null,
            bool? showOnStart = null,
            int? initialTabIndex = null,
            IRuntimeLogger logger = null,
            IInputProvider inputProvider = null)
        {
            Configure(
                slice?.Services,
                isPausedProvider,
                panelSettings,
                initialVisibility,
                showOnStart,
                initialTabIndex,
                logger,
                inputProvider);
        }

        internal void Configure(
            CharacterTestSliceServices services,
            Func<bool> isPausedProvider,
            PanelSettings panelSettings = null,
            DebugUiVisibility? initialVisibility = null,
            bool? showOnStart = null,
            int? initialTabIndex = null,
            IRuntimeLogger logger = null,
            IInputProvider inputProvider = null)
        {
            _services = services;
            _isPausedProvider = isPausedProvider;
            _logger = logger ?? _logger ?? NullRuntimeLogger.Instance;
            if (panelSettings != null)
                _panelSettings = panelSettings;
            if (initialVisibility.HasValue)
                _visibility = initialVisibility.Value;
            if (initialTabIndex.HasValue)
                _initialTabIndex = initialTabIndex.Value;

            _activeTab = CharacterTestDeveloperDebugSnapshot.ClampTabIndex(_initialTabIndex);
            _inputProvider = inputProvider ?? InputProviderResolver.ResolveOrCreateDefault(this);
            _inputAdapter.Configure(_inputProvider);
            _inputAdapter.SetEnabled(_services != null && _inputProvider != null);
            _cameraBridge = GetComponent<CharacterTestThirdPersonCameraRigBridge>();
            CharacterTestGizmoRuntimeDisplay.Apply(_gizmoPreferences.Load());
            enabled = _services != null;

            EnsureDocument();
            _view.SetAnchor(_anchor);
            bool reveal = showOnStart ?? _showOnStart;
            SetVisibility(reveal ? _visibility : DebugUiVisibility.Hidden);
            RefreshNow();
        }

        public void Release()
        {
            _services = null;
            _isPausedProvider = null;
            _inputAdapter.SetEnabled(false);
            _inputAdapter.Configure(null);
            SetVisibility(DebugUiVisibility.Hidden);
            enabled = false;
        }

        public void SetVisibility(DebugUiVisibility visibility)
        {
            _visibility = visibility;
            _view.SetVisibility(visibility);
        }

        public void RefreshNow()
        {
            EnsureDocument();
            CharacterTestDeveloperDebugSnapshot snapshot = CreateSnapshot();
            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, _refreshIntervalSeconds);
            _view.Bind(snapshot, _activeTab, _visibility);
            _view.BindGizmoSettings(CharacterTestGizmoRuntimeDisplay.Current);
            bool guidanceAvailable = _guidanceSelectionProvider != null && _guidanceSelectionSink != null;
            _view.BindGuidanceSelection(
                guidanceAvailable,
                guidanceAvailable ? _guidanceSelectionProvider() : default);
        }

        internal void ConfigureGuidanceSelection(
            Func<CharacterTestGuidanceSelectionSnapshot> provider,
            Action<string> sink)
        {
            _guidanceSelectionProvider = provider;
            _guidanceSelectionSink = sink;
            RefreshNow();
        }

        public void RequestDebugStep()
        {
            _nextRefreshTime = 0f;
        }

        public void RequestNextTab()
        {
            int count = CharacterTestDeveloperDebugSnapshot.TabNames.Length;
            _activeTab = count > 0 ? (_activeTab + 1) % count : 0;
            RefreshNow();
        }

        private void Awake()
        {
            EnsureDocument();
        }

        private void OnEnable()
        {
            EnsureDocument();
            if (_services == null)
                return;

            _inputAdapter.SetEnabled(GetInputProvider() != null);
            RefreshNow();
        }

        private void OnDisable()
        {
            _inputAdapter.SetEnabled(false);
            UnsubscribeViewEvents();
        }

        private void OnDestroy()
        {
            _inputAdapter.Dispose();
            UnsubscribeViewEvents();
        }

        private void Update()
        {
            EnsureDocument();
            ProcessDebugInput();
            if (_visibility == DebugUiVisibility.Hidden || _refreshPaused)
                return;

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            RefreshNow();
        }

        private void EnsureDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.panelSettings == null)
                _document.panelSettings = ResolvePanelSettings();

            VisualElement root = _document.rootVisualElement;
            if (root == null)
                return;

            if (_view.IsBuiltFor(root))
            {
                SubscribeViewEvents();
                return;
            }

            _view.Build(root);
            _view.SetAnchor(_anchor);
            SubscribeViewEvents();
            _view.SetVisibility(_visibility);
        }

        private void SubscribeViewEvents()
        {
            if (_viewEventsSubscribed)
                return;

            _view.TabSelected += OnTabSelected;
            _view.RefreshRequested += RefreshNow;
            _view.ActionReloadRequested += ReloadActions;
            _view.SurfaceProbeRequested += EnqueueSurfaceProbe;
            _view.MovableNaturalObjectResetRequested += EnqueueMovableNaturalObjectReset;
            _view.MovableNaturalObjectPotencyInfluenceRequested +=
                EnqueueMovableNaturalObjectPotencyInfluence;
            _view.RefreshPauseToggled += ToggleRefreshPaused;
            _view.AnchorCycleRequested += CycleAnchor;
            _view.CursorToggleRequested += ToggleCursorCapture;
            _view.GizmoOptionChanged += SetGizmoOption;
            _view.GizmoDefaultsRequested += ResetGizmoDefaults;
            _view.GuidanceTestSelected += SelectGuidanceTest;
            _view.CollapseRequested += Collapse;
            _view.HideRequested += Hide;
            _view.ExpandRequested += Expand;
            _viewEventsSubscribed = true;
        }

        private void UnsubscribeViewEvents()
        {
            if (!_viewEventsSubscribed)
                return;

            _view.TabSelected -= OnTabSelected;
            _view.RefreshRequested -= RefreshNow;
            _view.ActionReloadRequested -= ReloadActions;
            _view.SurfaceProbeRequested -= EnqueueSurfaceProbe;
            _view.MovableNaturalObjectResetRequested -= EnqueueMovableNaturalObjectReset;
            _view.MovableNaturalObjectPotencyInfluenceRequested -=
                EnqueueMovableNaturalObjectPotencyInfluence;
            _view.RefreshPauseToggled -= ToggleRefreshPaused;
            _view.AnchorCycleRequested -= CycleAnchor;
            _view.CursorToggleRequested -= ToggleCursorCapture;
            _view.GizmoOptionChanged -= SetGizmoOption;
            _view.GizmoDefaultsRequested -= ResetGizmoDefaults;
            _view.GuidanceTestSelected -= SelectGuidanceTest;
            _view.CollapseRequested -= Collapse;
            _view.HideRequested -= Hide;
            _view.ExpandRequested -= Expand;
            _viewEventsSubscribed = false;
        }

        private CharacterTestDeveloperDebugSnapshot CreateSnapshot()
        {
            return _snapshotBuilder.Build(
                _services,
                _isPausedProvider,
                GetInputProvider(),
                GetCameraBridge(),
                _refreshPaused,
                _logger);
        }

        private IInputProvider GetInputProvider()
        {
            if (_inputProvider == null)
                _inputProvider = InputProviderResolver.ResolveOrCreateDefault(this);

            return _inputProvider;
        }

        private CharacterTestThirdPersonCameraRigBridge GetCameraBridge()
        {
            if (_cameraBridge == null)
                _cameraBridge = GetComponent<CharacterTestThirdPersonCameraRigBridge>();

            return _cameraBridge;
        }

        private void ProcessDebugInput()
        {
            IInputProvider input = GetInputProvider();
            if (input == null)
                return;

            _inputAdapter.Configure(input);
            _inputAdapter.SetEnabled(_services != null);
            _lastInputResult = _inputAdapter.ProcessFrame(ResolveInputCommandFrame(input), this);
        }

        private static long ResolveInputCommandFrame(IInputProvider input)
        {
            if (input is InputService inputService)
                return inputService.LastCommandFrame;

            return input.Commands != null ? input.Commands.CurrentFrame : 0L;
        }

        private void OnTabSelected(int index)
        {
            _activeTab = CharacterTestDeveloperDebugSnapshot.ClampTabIndex(index);
            RefreshNow();
        }

        private void ToggleRefreshPaused()
        {
            _refreshPaused = !_refreshPaused;
            RefreshNow();
        }

        private void ReloadActions()
        {
            TryReloadCharacterActions(out _);
        }

        /// <summary>
        /// Reloads CharacterAction JSON through the configured CharacterTest runtime session.
        /// </summary>
        /// <remarks>
        /// AllocByDesign: this explicit developer operation reparses authoring JSON and refreshes debug UI state.
        /// </remarks>
        public bool TryReloadCharacterActions(out CharacterTestCharacterActionReloadResult result)
        {
            if (_services == null)
            {
                result = default;
                return false;
            }

            result = _services.Commands.ReloadCharacterActionConfig();
            RefreshNow();
            return true;
        }

        private void EnqueueSurfaceProbe(CharacterTestSurfaceProbeOperation operation)
        {
            if (_services == null)
                return;

            _services.Commands.EnqueueSurfaceProbe(
                operation,
                sourceId: CharacterTestRuntimeCommandSources.DebugUi);
            RefreshNow();
        }

        private void EnqueueMovableNaturalObjectReset()
        {
            if (_services == null)
                return;

            _services.EnqueueMovableNaturalObjectReset(
                0,
                sourceId: CharacterTestRuntimeCommandSources.DebugUi);
            RefreshNow();
        }

        private void EnqueueMovableNaturalObjectPotencyInfluence(
            int targetNumericId,
            CharacterTestGenesisPotencyInfluenceKind influenceKind)
        {
            if (_services == null)
                return;

            _services.EnqueueMovableNaturalObjectPotencyInfluence(
                influenceKind,
                Mathf.Max(0, targetNumericId),
                sourceId: CharacterTestRuntimeCommandSources.DebugUi);
            RefreshNow();
        }

        private void CycleAnchor()
        {
            _anchor = NextAnchor(_anchor);
            _view.SetAnchor(_anchor);
            RefreshNow();
        }

        private void ToggleCursorCapture()
        {
            CharacterTestThirdPersonCameraRigBridge cameraBridge = GetCameraBridge();
            if (cameraBridge == null)
                return;

            if (cameraBridge.CursorCaptured)
                cameraBridge.RequestCursorReleasedByDebugUi();
            else
                cameraBridge.RequestCursorCapturedByDebugUi();

            RefreshNow();
        }

        private void SetGizmoOption(CharacterTestGizmoDisplayOption option, bool value)
        {
            CharacterTestGizmoDisplaySettings settings =
                CharacterTestGizmoRuntimeDisplay.Current.With(option, value);
            CharacterTestGizmoRuntimeDisplay.Apply(settings);
            _gizmoPreferences.Save(settings);
            RefreshNow();
        }

        private void ResetGizmoDefaults()
        {
            CharacterTestGizmoDisplaySettings settings = CharacterTestGizmoDisplaySettings.Defaults;
            CharacterTestGizmoRuntimeDisplay.Apply(settings);
            _gizmoPreferences.Save(settings);
            RefreshNow();
        }

        private void SelectGuidanceTest(string stableId)
        {
            _guidanceSelectionSink?.Invoke(stableId);
            RefreshNow();
        }

        private void Collapse()
        {
            SetVisibility(DebugUiVisibility.Collapsed);
        }

        private void Hide()
        {
            SetVisibility(DebugUiVisibility.Hidden);
        }

        private void Expand()
        {
            SetVisibility(DebugUiVisibility.Expanded);
        }

        private PanelSettings ResolvePanelSettings()
        {
            if (_panelSettings != null)
                return _panelSettings;

            PanelSettings defaultPanelSettings = LoadDefaultPanelSettings();
            if (defaultPanelSettings != null)
                return defaultPanelSettings;

            _logger?.Warning(
                "CharacterTest.Debug",
                "Developer debug panel has no PanelSettings. Falling back to a temporary PanelSettings.");
            return ScriptableObject.CreateInstance<PanelSettings>();
        }

        private static CharacterTestDeveloperDebugPanelAnchor NextAnchor(
            CharacterTestDeveloperDebugPanelAnchor anchor)
        {
            switch (anchor)
            {
                case CharacterTestDeveloperDebugPanelAnchor.TopRight:
                    return CharacterTestDeveloperDebugPanelAnchor.TopLeft;
                case CharacterTestDeveloperDebugPanelAnchor.TopLeft:
                    return CharacterTestDeveloperDebugPanelAnchor.BottomRight;
                default:
                    return CharacterTestDeveloperDebugPanelAnchor.TopRight;
            }
        }

        private static PanelSettings LoadDefaultPanelSettings()
        {
#if UNITY_EDITOR
            PanelSettings panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                DefaultStoryPanelSettingsPath);
            if (panelSettings != null)
                return panelSettings;

            panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                DefaultGameplayPanelSettingsPath);
            if (panelSettings != null)
                return panelSettings;

            return UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                DefaultCombatAnimationPanelSettingsPath);
#else
            return null;
#endif
        }
    }
}
