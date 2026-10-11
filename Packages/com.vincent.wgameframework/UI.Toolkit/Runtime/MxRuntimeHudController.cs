using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.UI.Toolkit
{
    public enum MxRuntimeHudManualCommand
    {
        Strike,
        Ignite,
        ApplyBuff,
        Tick,
        ApplyModifier,
        Reset,
        ToggleAuto,
        ToggleLiveTick,
        ToggleMode,
        LoadPatch,
        LoadModPackage,
        RebuildAbility,
        CompareConfig
    }

    public interface IMxRuntimeHudManualControlSink
    {
        void OnRuntimeHudManualCommand(MxRuntimeHudManualCommand command);
    }

    public enum MxRuntimeHudLayoutPreset
    {
        Default,
        RightCompact
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/UI Toolkit/Runtime HUD Controller")]
    public sealed class MxRuntimeHudController : MonoBehaviour
    {
        private static readonly FieldInfo DisableNoThemeWarningField =
            typeof(PanelSettings).GetField("m_DisableNoThemeWarning", BindingFlags.NonPublic | BindingFlags.Instance);

        [SerializeField] private UIDocument _document;
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private StyleSheet _styleSheet;
        [SerializeField] private Font _font;

        public event Action<MxRuntimeHudManualCommand> ManualCommandRequested;
        public event Action StrikeRequested;
        public event Action IgniteRequested;
        public event Action ApplyBuffRequested;
        public event Action TickRequested;
        public event Action ApplyModifierRequested;
        public event Action ResetRequested;
        public event Action AutoToggleRequested;
        public event Action LiveTickToggleRequested;
        public event Action ModeToggleRequested;
        public event Action LoadPatchRequested;
        public event Action LoadModPackageRequested;
        public event Action RebuildAbilityRequested;
        public event Action CompareConfigRequested;

        private Label _title;
        private Button _visibilityToggleButton;
        private Label _mode;
        private Label _abilitySource;
        private Label _configSummary;
        private Label _snapshotSummary;
        private Label _configModeStatus;
        private Label _runtimeConfigChangeSummary;
        private Label _abilityRebuildSummary;
        private Label _configComparisonSummary;
        private Label _feedbackPlayerStatus;
        private Label _feedbackEnemyStatus;
        private Label _feedbackPlayerBuff;
        private Label _feedbackEnemyBuff;
        private Label _feedbackSkillButtons;
        private Label _feedbackRecentAction;
        private Label _diagnosticHeader;
        private Label _diagnosticLastCast;
        private Label _diagnosticConfigSource;
        private Label _diagnosticErrorSummary;
        private Label _playerName;
        private Label _playerStats;
        private Label _playerBuffs;
        private Label _enemyName;
        private Label _enemyStats;
        private Label _enemyBuffs;
        private VisualElement _root;
        private VisualElement _body;
        private ScrollView _bodyScroll;
        private VisualElement _playerHpFill;
        private VisualElement _enemyHpFill;
        private ScrollView _eventScroll;
        private VisualElement _eventList;
        private Button _strikeButton;
        private Button _igniteButton;
        private Button _applyBuffButton;
        private Button _tickButton;
        private Button _applyModifierButton;
        private Button _resetButton;
        private Button _autoToggleButton;
        private Button _liveTickToggleButton;
        private Button _modeToggleButton;
        private Button _loadPatchButton;
        private Button _loadModPackageButton;
        private Button _rebuildAbilityButton;
        private Button _compareConfigButton;
        private Button _diagnosticSummaryButton;
        private Button _diagnosticTechnicalButton;
        private VisualElement _diagnosticSummaryView;
        private VisualElement _diagnosticTechnicalView;
        private VisualElement _diagnosticSummaryEntityList;
        private VisualElement _diagnosticSummaryAbilityEventsList;
        private VisualElement _diagnosticSummaryAttributeEventsList;
        private VisualElement _diagnosticSummaryConfigList;
        private VisualElement _diagnosticSummaryErrorsList;
        private VisualElement _diagnosticTechnicalEntityList;
        private VisualElement _diagnosticAbilityEventsList;
        private VisualElement _diagnosticAttributeEventsList;
        private VisualElement _diagnosticConfigSourceList;
        private VisualElement _diagnosticErrorsList;
        private IMxRuntimeHudManualControlSink _manualControlSink;
        private MxRuntimeDiagnosticViewModel _currentDiagnostic;
        private bool _built;
        private bool _isCompact;
        private bool _hudVisible = true;
        private bool _hudCollapsed;
        private bool _diagnosticTechnicalMode;
        private MxRuntimeHudLayoutPreset _layoutPreset;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            EnsureBuilt();
        }

        public void Refresh(MxRuntimeHudViewModel model)
        {
            EnsureBuilt();
            if (!_built || model == null)
                return;

            SetText(_title, model.Title);
            SetText(_mode, model.ModeName);
            SetText(_abilitySource, model.AbilitySource);
            SetText(_configSummary, model.ConfigSummary);
            SetText(_snapshotSummary, model.SnapshotSummary);
            SetText(_configModeStatus, model.ConfigModeStatus);
            SetText(_runtimeConfigChangeSummary, model.RuntimeConfigChangeSummary);
            SetText(_abilityRebuildSummary, model.AbilityRebuildSummary);
            SetText(_configComparisonSummary, model.ConfigComparisonSummary);
            RefreshMiniGameFeedback(model.MiniGameFeedback);
            RefreshDiagnostic(model.Diagnostic);

            RefreshEntity(model.Player, _playerName, _playerStats, _playerBuffs, _playerHpFill);
            RefreshEntity(model.Enemy, _enemyName, _enemyStats, _enemyBuffs, _enemyHpFill);
            RefreshEvents(model.EventLog);
            ApplyHudVisibilityState();
            RefreshResponsiveLayout();
        }

        public void RegisterManualControlSink(IMxRuntimeHudManualControlSink sink)
        {
            _manualControlSink = sink;
        }

        public void UnregisterManualControlSink(IMxRuntimeHudManualControlSink sink)
        {
            if (ReferenceEquals(_manualControlSink, sink))
                _manualControlSink = null;
        }

        public void SetManualControlState(bool autoSequenceEnabled, bool liveTickEnabled, bool configDrivenMode)
        {
            EnsureBuilt();
            if (!_built)
                return;

            SetButtonText(_autoToggleButton, autoSequenceEnabled ? "Auto: On" : "Auto: Off");
            SetButtonText(_liveTickToggleButton, liveTickEnabled ? "Time: On" : "Time: Off");
            SetButtonText(_modeToggleButton, configDrivenMode ? "Mode: Config" : "Mode: Code");
            SetButtonEnabledState(_autoToggleButton, autoSequenceEnabled);
            SetButtonEnabledState(_liveTickToggleButton, liveTickEnabled);
            SetButtonEnabledState(_modeToggleButton, configDrivenMode);
        }

        public void SetManualButtonLabels(
            string strike,
            string ignite,
            string applyBuff,
            string tick,
            string applyModifier,
            string autoToggle,
            string liveTickToggle,
            string modeToggle,
            string reset)
        {
            EnsureBuilt();
            if (!_built)
                return;

            SetButtonText(_strikeButton, strike);
            SetButtonText(_igniteButton, ignite);
            SetButtonText(_applyBuffButton, applyBuff);
            SetButtonText(_tickButton, tick);
            SetButtonText(_applyModifierButton, applyModifier);
            SetButtonText(_autoToggleButton, autoToggle);
            SetButtonText(_liveTickToggleButton, liveTickToggle);
            SetButtonText(_modeToggleButton, modeToggle);
            SetButtonText(_resetButton, reset);
        }

        public void SetManualControlState(bool autoSequenceEnabled, bool configDrivenMode)
        {
            SetManualControlState(autoSequenceEnabled, true, configDrivenMode);
        }

        public bool HudVisible => _hudVisible;
        public bool HudCollapsed => _hudCollapsed;

        public void SetLayoutPreset(MxRuntimeHudLayoutPreset preset)
        {
            EnsureBuilt();
            _layoutPreset = preset;
            if (!_built)
                return;

            if (_root != null)
            {
                _root.EnableInClassList("hud-right-compact", preset == MxRuntimeHudLayoutPreset.RightCompact);
                _root.EnableInClassList("hud-default-layout", preset == MxRuntimeHudLayoutPreset.Default);
            }

            RefreshResponsiveLayout();
        }

        public void SetHudVisible(bool visible)
        {
            EnsureBuilt();
            if (!_built)
                return;

            _hudVisible = visible;
            if (!visible)
                _hudCollapsed = false;

            ApplyHudVisibilityState();
            RefreshResponsiveLayout();
        }

        public void SetHudCollapsed(bool collapsed)
        {
            EnsureBuilt();
            if (!_built)
                return;

            _hudVisible = true;
            _hudCollapsed = collapsed;
            ApplyHudVisibilityState();
            RefreshResponsiveLayout();
        }

        public void ToggleHudCollapsed()
        {
            SetHudCollapsed(!_hudCollapsed);
        }

        public void ConfigureAssets(PanelSettings panelSettings, VisualTreeAsset visualTree, StyleSheet styleSheet, Font font = null)
        {
            _panelSettings = panelSettings;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
            if (font != null)
                _font = font;

            _built = false;
            EnsureBuilt();
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_visualTree == null)
                return;

            if (_document.panelSettings == null)
                _document.panelSettings = CreateRuntimePanelSettings(_panelSettings);

            if (_visualTree != null)
                _document.visualTreeAsset = _visualTree;

            VisualElement root = _document.rootVisualElement;
            if (root == null)
                return;

            if (_styleSheet != null && !root.styleSheets.Contains(_styleSheet))
                root.styleSheets.Add(_styleSheet);

            CacheElements(root);
            ApplyThemeTokenClasses();
            ApplyRuntimeTextStyles();
            RegisterManualControlCallbacks();
            root.RegisterCallback<GeometryChangedEvent>(_ => RefreshResponsiveLayout());
            _root?.EnableInClassList("hud-right-compact", _layoutPreset == MxRuntimeHudLayoutPreset.RightCompact);
            _root?.EnableInClassList("hud-default-layout", _layoutPreset == MxRuntimeHudLayoutPreset.Default);
            RefreshResponsiveLayout();
            _built = true;
        }

        private static PanelSettings CreateRuntimePanelSettings(PanelSettings template)
        {
            var settings = template != null
                ? Instantiate(template)
                : ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "MxRuntimeHudPanelSettingsInstance";
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.referenceResolution = new Vector2Int(1366, 768);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 100f;
            settings.textSettings = null;

            DisableNoThemeWarningField?.SetValue(settings, true);
            return settings;
        }

        private void CacheElements(VisualElement root)
        {
            _root = root.Q<VisualElement>(className: "showcase-root");
            _body = root.Q<VisualElement>("showcase-body");
            _bodyScroll = root.Q<ScrollView>("showcase-body");
            _title = root.Q<Label>("showcase-title");
            _visibilityToggleButton = root.Q<Button>("hud-visibility-toggle-button");
            _mode = root.Q<Label>("mode-label");
            _abilitySource = root.Q<Label>("ability-source");
            _configSummary = root.Q<Label>("config-summary");
            _snapshotSummary = root.Q<Label>("snapshot-summary");
            _configModeStatus = root.Q<Label>("config-mode-status");
            _runtimeConfigChangeSummary = root.Q<Label>("runtime-config-change-summary");
            _abilityRebuildSummary = root.Q<Label>("ability-rebuild-summary");
            _configComparisonSummary = root.Q<Label>("config-comparison-summary");
            _feedbackPlayerStatus = root.Q<Label>("feedback-player-status");
            _feedbackEnemyStatus = root.Q<Label>("feedback-enemy-status");
            _feedbackPlayerBuff = root.Q<Label>("feedback-player-buff");
            _feedbackEnemyBuff = root.Q<Label>("feedback-enemy-buff");
            _feedbackSkillButtons = root.Q<Label>("feedback-skill-buttons");
            _feedbackRecentAction = root.Q<Label>("feedback-recent-action");
            _diagnosticHeader = root.Q<Label>("diagnostic-header");
            _diagnosticLastCast = root.Q<Label>("diagnostic-last-cast");
            _diagnosticConfigSource = root.Q<Label>("diagnostic-config-source");
            _diagnosticErrorSummary = root.Q<Label>("diagnostic-error-summary");
            _playerName = root.Q<Label>("player-name");
            _playerStats = root.Q<Label>("player-stats");
            _playerBuffs = root.Q<Label>("player-buffs");
            _enemyName = root.Q<Label>("enemy-name");
            _enemyStats = root.Q<Label>("enemy-stats");
            _enemyBuffs = root.Q<Label>("enemy-buffs");
            _playerHpFill = root.Q<VisualElement>("player-hp-fill");
            _enemyHpFill = root.Q<VisualElement>("enemy-hp-fill");
            _eventScroll = root.Q<ScrollView>("event-scroll");
            _eventList = root.Q<VisualElement>("event-list");
            _strikeButton = root.Q<Button>("manual-strike-button");
            _igniteButton = root.Q<Button>("manual-ignite-button");
            _applyBuffButton = root.Q<Button>("manual-buff-button");
            _tickButton = root.Q<Button>("manual-tick-button");
            _applyModifierButton = root.Q<Button>("manual-modifier-button");
            _resetButton = root.Q<Button>("manual-reset-button");
            _autoToggleButton = root.Q<Button>("manual-auto-toggle-button");
            _liveTickToggleButton = root.Q<Button>("manual-live-tick-toggle-button");
            _modeToggleButton = root.Q<Button>("manual-mode-toggle-button");
            _loadPatchButton = root.Q<Button>("config-load-patch-button");
            _loadModPackageButton = root.Q<Button>("config-load-mod-package-button");
            _rebuildAbilityButton = root.Q<Button>("config-rebuild-ability-button");
            _compareConfigButton = root.Q<Button>("config-compare-button");
            _diagnosticSummaryButton = root.Q<Button>("diagnostic-summary-button");
            _diagnosticTechnicalButton = root.Q<Button>("diagnostic-technical-button");
            _diagnosticSummaryView = root.Q<VisualElement>("diagnostic-summary-view");
            _diagnosticTechnicalView = root.Q<VisualElement>("diagnostic-technical-view");
            _diagnosticSummaryEntityList = root.Q<VisualElement>("diagnostic-summary-entities-list");
            _diagnosticSummaryAbilityEventsList = root.Q<VisualElement>("diagnostic-summary-ability-events-list");
            _diagnosticSummaryAttributeEventsList = root.Q<VisualElement>("diagnostic-summary-attribute-events-list");
            _diagnosticSummaryConfigList = root.Q<VisualElement>("diagnostic-summary-config-list");
            _diagnosticSummaryErrorsList = root.Q<VisualElement>("diagnostic-summary-errors-list");
            _diagnosticTechnicalEntityList = root.Q<VisualElement>("diagnostic-technical-entities-list");
            _diagnosticAbilityEventsList = root.Q<VisualElement>("diagnostic-ability-events-list");
            _diagnosticAttributeEventsList = root.Q<VisualElement>("diagnostic-attribute-events-list");
            _diagnosticConfigSourceList = root.Q<VisualElement>("diagnostic-config-source-list");
            _diagnosticErrorsList = root.Q<VisualElement>("diagnostic-errors-list");

            if (_bodyScroll != null)
            {
                _bodyScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
                _bodyScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                _bodyScroll.mouseWheelScrollSize = 64f;
                _bodyScroll.contentContainer.style.flexGrow = 1f;
                _bodyScroll.contentContainer.style.minHeight = 0f;
            }
        }

        private void ApplyThemeTokenClasses()
        {
            AddClass(_feedbackPlayerStatus, MxUiThemeTokens.StatusBadge);
            AddClass(_feedbackEnemyStatus, MxUiThemeTokens.StatusBadge);
            AddClass(_playerHpFill, MxUiThemeTokens.StatBarFill);
            AddClass(_enemyHpFill, MxUiThemeTokens.StatBarFill);
            AddClass(_eventList, MxUiThemeTokens.EventLog);
            AddClass(_diagnosticSummaryButton, MxUiThemeTokens.PanelTab);
            AddClass(_diagnosticTechnicalButton, MxUiThemeTokens.PanelTab);

            VisualElement diagnosticSwitch = _diagnosticSummaryButton?.parent;
            AddClass(diagnosticSwitch, MxUiThemeTokens.PanelTabs);
            AddClass(diagnosticSwitch, MxUiThemeTokens.DiagnosticTabs);

            ApplyCommandClass(_strikeButton);
            ApplyCommandClass(_igniteButton);
            ApplyCommandClass(_applyBuffButton);
            ApplyCommandClass(_tickButton);
            ApplyCommandClass(_applyModifierButton);
            ApplyCommandClass(_resetButton);
            ApplyCommandClass(_autoToggleButton);
            ApplyCommandClass(_liveTickToggleButton);
            ApplyCommandClass(_modeToggleButton);
            ApplyCommandClass(_loadPatchButton);
            ApplyCommandClass(_loadModPackageButton);
            ApplyCommandClass(_rebuildAbilityButton);
            ApplyCommandClass(_compareConfigButton);
            ApplyCommandClass(_diagnosticSummaryButton);
            ApplyCommandClass(_diagnosticTechnicalButton);
            ApplyCommandClass(_visibilityToggleButton);
        }

        private static void AddClass(VisualElement element, string className)
        {
            if (element != null)
                element.AddToClassList(className);
        }

        private static void ApplyCommandClass(Button button)
        {
            AddClass(button, MxUiThemeTokens.CommandButton);
        }

        private void ApplyRuntimeTextStyles()
        {
            ApplyLabelStyle(_title, 20, Color.white, FontStyle.Bold);
            _title.style.whiteSpace = WhiteSpace.NoWrap;
            _title.style.overflow = Overflow.Hidden;
            ApplyLabelStyle(_mode, 13, new Color(1f, 0.96f, 0.66f), FontStyle.Bold);
            _mode.style.whiteSpace = WhiteSpace.NoWrap;
            _mode.style.overflow = Overflow.Hidden;
            ApplyLabelStyle(_playerName, 16, Color.white, FontStyle.Bold);
            ApplyLabelStyle(_enemyName, 16, Color.white, FontStyle.Bold);
            ApplyLabelStyle(_playerStats, 12, new Color(0.93f, 0.96f, 0.99f), FontStyle.Normal);
            ApplyLabelStyle(_enemyStats, 12, new Color(0.93f, 0.96f, 0.99f), FontStyle.Normal);
            ApplyLabelStyle(_playerBuffs, 11, new Color(0.82f, 0.9f, 0.94f), FontStyle.Normal);
            ApplyLabelStyle(_enemyBuffs, 11, new Color(0.82f, 0.9f, 0.94f), FontStyle.Normal);
            ApplyLabelStyle(_abilitySource, 12, Color.white, FontStyle.Normal);
            ApplyLabelStyle(_configSummary, 12, Color.white, FontStyle.Normal);
            ApplyLabelStyle(_snapshotSummary, 12, Color.white, FontStyle.Normal);
            ApplyLabelStyle(_configModeStatus, 12, Color.white, FontStyle.Normal);
            ApplyLabelStyle(_runtimeConfigChangeSummary, 12, Color.white, FontStyle.Normal);
            ApplyLabelStyle(_abilityRebuildSummary, 12, Color.white, FontStyle.Normal);
            ApplyLabelStyle(_configComparisonSummary, 12, Color.white, FontStyle.Normal);
            ApplyLabelStyle(_feedbackPlayerStatus, 12, Color.white, FontStyle.Bold);
            ApplyLabelStyle(_feedbackEnemyStatus, 12, Color.white, FontStyle.Bold);
            ApplyLabelStyle(_feedbackPlayerBuff, 12, new Color(0.82f, 0.9f, 0.94f), FontStyle.Normal);
            ApplyLabelStyle(_feedbackEnemyBuff, 12, new Color(0.82f, 0.9f, 0.94f), FontStyle.Normal);
            ApplyLabelStyle(_feedbackSkillButtons, 12, new Color(0.82f, 0.9f, 0.94f), FontStyle.Normal);
            ApplyLabelStyle(_feedbackRecentAction, 11, new Color(0.96f, 0.92f, 0.69f), FontStyle.Normal);
            ApplyLabelStyle(_diagnosticHeader, 12, Color.white, FontStyle.Bold);
            ApplyLabelStyle(_diagnosticLastCast, 12, Color.white, FontStyle.Normal);
            ApplyLabelStyle(_diagnosticConfigSource, 12, Color.white, FontStyle.Normal);
            ApplyLabelStyle(_diagnosticErrorSummary, 12, Color.white, FontStyle.Normal);
            ApplyButtonStyle(_strikeButton);
            ApplyButtonStyle(_igniteButton);
            ApplyButtonStyle(_applyBuffButton);
            ApplyButtonStyle(_tickButton);
            ApplyButtonStyle(_applyModifierButton);
            ApplyButtonStyle(_resetButton);
            ApplyButtonStyle(_autoToggleButton);
            ApplyButtonStyle(_liveTickToggleButton);
            ApplyButtonStyle(_modeToggleButton);
            ApplyButtonStyle(_loadPatchButton);
            ApplyButtonStyle(_loadModPackageButton);
            ApplyButtonStyle(_rebuildAbilityButton);
            ApplyButtonStyle(_compareConfigButton);
            ApplyButtonStyle(_diagnosticSummaryButton);
            ApplyButtonStyle(_diagnosticTechnicalButton);
            ApplyButtonStyle(_visibilityToggleButton);
        }

        private void ApplyLabelStyle(Label label, int fontSize, Color color, FontStyle fontStyle)
        {
            if (label == null)
                return;

            if (_font != null)
                label.style.unityFont = new StyleFont(_font);

            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = fontStyle;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.minHeight = Mathf.Max(16, fontSize + 4);
        }

        private void ApplyButtonStyle(Button button)
        {
            if (button == null)
                return;

            button.focusable = false;
            if (_font != null)
                button.style.unityFont = new StyleFont(_font);

            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.whiteSpace = WhiteSpace.Normal;
        }

        private void RegisterManualControlCallbacks()
        {
            RegisterButton(_strikeButton, MxRuntimeHudManualCommand.Strike);
            RegisterButton(_igniteButton, MxRuntimeHudManualCommand.Ignite);
            RegisterButton(_applyBuffButton, MxRuntimeHudManualCommand.ApplyBuff);
            RegisterButton(_tickButton, MxRuntimeHudManualCommand.Tick);
            RegisterButton(_applyModifierButton, MxRuntimeHudManualCommand.ApplyModifier);
            RegisterButton(_resetButton, MxRuntimeHudManualCommand.Reset);
            RegisterButton(_autoToggleButton, MxRuntimeHudManualCommand.ToggleAuto);
            RegisterButton(_liveTickToggleButton, MxRuntimeHudManualCommand.ToggleLiveTick);
            RegisterButton(_modeToggleButton, MxRuntimeHudManualCommand.ToggleMode);
            RegisterButton(_loadPatchButton, MxRuntimeHudManualCommand.LoadPatch);
            RegisterButton(_loadModPackageButton, MxRuntimeHudManualCommand.LoadModPackage);
            RegisterButton(_rebuildAbilityButton, MxRuntimeHudManualCommand.RebuildAbility);
            RegisterButton(_compareConfigButton, MxRuntimeHudManualCommand.CompareConfig);
            if (_diagnosticSummaryButton != null)
                _diagnosticSummaryButton.clicked += () => SetDiagnosticMode(false);
            if (_diagnosticTechnicalButton != null)
                _diagnosticTechnicalButton.clicked += () => SetDiagnosticMode(true);
            if (_visibilityToggleButton != null)
                _visibilityToggleButton.clicked += ToggleHudCollapsed;
        }

        private void RegisterButton(Button button, MxRuntimeHudManualCommand command)
        {
            if (button == null)
                return;

            button.clicked += () => RaiseManualCommand(command);
        }

        private void RaiseManualCommand(MxRuntimeHudManualCommand command)
        {
            ManualCommandRequested?.Invoke(command);
            _manualControlSink?.OnRuntimeHudManualCommand(command);

            switch (command)
            {
                case MxRuntimeHudManualCommand.Strike:
                    StrikeRequested?.Invoke();
                    break;
                case MxRuntimeHudManualCommand.Ignite:
                    IgniteRequested?.Invoke();
                    break;
                case MxRuntimeHudManualCommand.ApplyBuff:
                    ApplyBuffRequested?.Invoke();
                    break;
                case MxRuntimeHudManualCommand.Tick:
                    TickRequested?.Invoke();
                    break;
                case MxRuntimeHudManualCommand.ApplyModifier:
                    ApplyModifierRequested?.Invoke();
                    break;
                case MxRuntimeHudManualCommand.Reset:
                    ResetRequested?.Invoke();
                    break;
                case MxRuntimeHudManualCommand.ToggleAuto:
                    AutoToggleRequested?.Invoke();
                    break;
                case MxRuntimeHudManualCommand.ToggleLiveTick:
                    LiveTickToggleRequested?.Invoke();
                    break;
                case MxRuntimeHudManualCommand.ToggleMode:
                    ModeToggleRequested?.Invoke();
                    break;
                case MxRuntimeHudManualCommand.LoadPatch:
                    LoadPatchRequested?.Invoke();
                    break;
                case MxRuntimeHudManualCommand.LoadModPackage:
                    LoadModPackageRequested?.Invoke();
                    break;
                case MxRuntimeHudManualCommand.RebuildAbility:
                    RebuildAbilityRequested?.Invoke();
                    break;
                case MxRuntimeHudManualCommand.CompareConfig:
                    CompareConfigRequested?.Invoke();
                    break;
            }
        }

        private static void SetButtonText(Button button, string value)
        {
            if (button != null)
                button.text = string.IsNullOrEmpty(value) ? "-" : value;
        }

        private static void SetButtonEnabledState(Button button, bool enabled)
        {
            if (button == null)
                return;

            button.EnableInClassList("is-enabled", enabled);
            button.EnableInClassList(MxUiThemeTokens.CommandEnabled, enabled);
        }

        private static void SetButtonFeedbackState(Button button, bool hot, string tooltip)
        {
            if (button == null)
                return;

            button.EnableInClassList("is-feedback-hot", hot);
            button.EnableInClassList("is-feedback-muted", !hot);
            button.EnableInClassList(MxUiThemeTokens.CommandHot, hot);
            button.EnableInClassList(MxUiThemeTokens.CommandMuted, !hot);
            button.tooltip = string.IsNullOrEmpty(tooltip) ? null : tooltip;
        }

        private void RefreshMiniGameFeedback(MxRuntimeMiniGameFeedbackViewModel feedback)
        {
            if (feedback == null)
                feedback = new MxRuntimeMiniGameFeedbackViewModel();

            SetStatusBadge(_feedbackPlayerStatus, feedback.PlayerStatusText, feedback.PlayerStatusTone);
            SetStatusBadge(_feedbackEnemyStatus, feedback.EnemyStatusText, feedback.EnemyStatusTone);
            SetText(_feedbackPlayerBuff, feedback.PlayerBuffText);
            SetText(_feedbackEnemyBuff, feedback.EnemyBuffText);
            SetText(_feedbackRecentAction, feedback.RecentActionText);
            SetText(_feedbackSkillButtons, feedback.SkillFeedbackText);
            SetButtonFeedbackState(_strikeButton, feedback.StrikeButtonHot, feedback.StrikeButtonFeedbackText);
            SetButtonFeedbackState(_igniteButton, feedback.IgniteButtonHot, feedback.IgniteButtonFeedbackText);
            SetButtonFeedbackState(_applyBuffButton, feedback.BuffButtonHot, feedback.BuffButtonFeedbackText);
        }

        private static void SetStatusBadge(Label label, string text, string tone)
        {
            if (label == null)
                return;

            SetText(label, text);
            MxUiTone parsedTone = MxUiThemeTokens.ParseTone(tone);
            label.EnableInClassList("status-positive", parsedTone == MxUiTone.Positive);
            label.EnableInClassList("status-warning", parsedTone == MxUiTone.Warning);
            label.EnableInClassList("status-danger", parsedTone == MxUiTone.Danger);
            label.EnableInClassList("status-neutral", parsedTone == MxUiTone.Neutral);
            MxUiThemeTokens.SetStatusTone(label, parsedTone);
        }

        private void RefreshResponsiveLayout()
        {
            if (_root == null || _document == null || _document.rootVisualElement == null)
                return;

            Rect panel = _document.rootVisualElement.worldBound;
            float panelWidth = ResolvePanelDimension(panel.width, Screen.width, 1366f);
            if (panelWidth <= 0f)
                return;

            float panelHeight = ResolvePanelDimension(panel.height, Screen.height, panelWidth * 0.5625f);
            bool compact = panelWidth < 900f || panelHeight < 620f;
            if (compact != _isCompact)
            {
                _isCompact = compact;
                _root.EnableInClassList("hud-compact", compact);
                _root.EnableInClassList("hud-wide", !compact);
            }

            float margin = compact ? 8f : 28f;
            float availableWidth = Mathf.Max(240f, panelWidth - margin * 2f);
            float availableHeight = Mathf.Max(240f, panelHeight - margin * 2f);
            float width;
            float height;
            if (_layoutPreset == MxRuntimeHudLayoutPreset.RightCompact && !compact)
            {
                width = Mathf.Min(availableWidth, Mathf.Clamp(panelWidth * 0.36f, 430f, 520f));
                height = _hudCollapsed ? 42f : Mathf.Min(availableHeight, Mathf.Clamp(panelHeight * 0.84f, 560f, 760f));
                _root.style.left = panelWidth - margin - width;
                _root.style.top = margin;
            }
            else
            {
                width = compact
                    ? availableWidth
                    : Mathf.Min(availableWidth, Mathf.Clamp(panelWidth * 0.88f, 980f, 1240f));
                height = _hudCollapsed
                    ? 42f
                    : compact
                        ? availableHeight
                        : Mathf.Min(availableHeight, Mathf.Clamp(panelHeight * 0.72f, 520f, 680f));
                _root.style.left = margin;
                _root.style.top = margin;
            }

            _root.style.maxWidth = availableWidth;
            _root.style.maxHeight = availableHeight;
            _root.style.width = width;
            _root.style.height = height;
        }

        private void ApplyHudVisibilityState()
        {
            if (_root == null)
                return;

            _root.style.display = _hudVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _root.EnableInClassList("hud-collapsed", _hudCollapsed);
            if (_body != null)
                _body.style.display = _hudCollapsed ? DisplayStyle.None : DisplayStyle.Flex;

            SetButtonText(_visibilityToggleButton, _hudCollapsed ? "Show" : "Hide");
        }

        private static float ResolvePanelDimension(float panelValue, int screenValue, float fallback)
        {
            if (!float.IsNaN(panelValue) && !float.IsInfinity(panelValue) && panelValue > 0f)
                return panelValue;

            if (screenValue > 0)
                return screenValue;

            return fallback;
        }

        private void SetDiagnosticMode(bool technical)
        {
            _diagnosticTechnicalMode = technical;
            RefreshDiagnostic(_currentDiagnostic);
        }

        private void RefreshDiagnostic(MxRuntimeDiagnosticViewModel diagnostic)
        {
            if (diagnostic == null)
                diagnostic = new MxRuntimeDiagnosticViewModel();
            _currentDiagnostic = diagnostic;

            SetText(_diagnosticHeader, diagnostic.HeaderText);
            SetText(_diagnosticLastCast, diagnostic.LastCastText);
            SetText(_diagnosticConfigSource, diagnostic.ConfigSourceText);
            SetText(_diagnosticErrorSummary, diagnostic.ErrorSummaryText);

            if (_diagnosticSummaryView != null)
                _diagnosticSummaryView.style.display = _diagnosticTechnicalMode ? DisplayStyle.None : DisplayStyle.Flex;
            if (_diagnosticTechnicalView != null)
                _diagnosticTechnicalView.style.display = _diagnosticTechnicalMode ? DisplayStyle.Flex : DisplayStyle.None;
            SetButtonEnabledState(_diagnosticSummaryButton, !_diagnosticTechnicalMode);
            SetButtonEnabledState(_diagnosticTechnicalButton, _diagnosticTechnicalMode);
            _diagnosticSummaryButton?.EnableInClassList(MxUiThemeTokens.PanelTabActive, !_diagnosticTechnicalMode);
            _diagnosticTechnicalButton?.EnableInClassList(MxUiThemeTokens.PanelTabActive, _diagnosticTechnicalMode);

            RefreshDiagnosticList(_diagnosticSummaryEntityList, diagnostic.EntitySummaryLines, diagnostic.EntityEmptyText);
            RefreshDiagnosticList(_diagnosticSummaryAbilityEventsList, diagnostic.AbilityEventSummaryLines, diagnostic.AbilityEventsEmptyText);
            RefreshDiagnosticList(_diagnosticSummaryAttributeEventsList, diagnostic.AttributeEventSummaryLines, diagnostic.AttributeEventsEmptyText);
            RefreshDiagnosticList(_diagnosticSummaryConfigList, diagnostic.ConfigSummaryLines, diagnostic.ConfigSourceEmptyText);
            RefreshDiagnosticList(_diagnosticSummaryErrorsList, diagnostic.ErrorSummaryLines, diagnostic.ErrorsEmptyText);
            RefreshDiagnosticList(_diagnosticTechnicalEntityList, diagnostic.EntityTechnicalLines, diagnostic.EntityEmptyText);
            RefreshDiagnosticList(_diagnosticAbilityEventsList, diagnostic.AbilityEventTechnicalLines, diagnostic.AbilityEventsEmptyText);
            RefreshDiagnosticList(_diagnosticAttributeEventsList, diagnostic.AttributeEventTechnicalLines, diagnostic.AttributeEventsEmptyText);
            RefreshDiagnosticList(_diagnosticConfigSourceList, diagnostic.ConfigTechnicalLines, diagnostic.ConfigSourceEmptyText);
            RefreshDiagnosticList(_diagnosticErrorsList, diagnostic.ErrorTechnicalLines, diagnostic.ErrorsEmptyText);
        }

        private void RefreshDiagnosticList(VisualElement list, IReadOnlyList<string> values, string emptyText)
        {
            if (list == null)
                return;

            list.Clear();
            if (values == null || values.Count == 0)
            {
                var empty = new Label(string.IsNullOrEmpty(emptyText) ? "-" : emptyText);
                empty.AddToClassList("diagnostic-empty");
                ApplyLabelStyle(empty, _isCompact ? 10 : 11, new Color(0.78f, 0.84f, 0.88f), FontStyle.Normal);
                list.Add(empty);
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                var item = new Label(values[i]);
                item.AddToClassList("diagnostic-item");
                ApplyLabelStyle(item, _isCompact ? 10 : 11, new Color(0.95f, 0.97f, 1f), FontStyle.Normal);
                list.Add(item);
            }
        }

        private static void RefreshEntity(
            MxRuntimeEntityViewModel entity,
            Label name,
            Label stats,
            Label buffs,
            VisualElement hpFill)
        {
            if (entity == null)
                return;

            SetText(name, $"{entity.DisplayName}  T{entity.TeamId}  #{entity.EntityId}");
            SetText(stats, $"HP {entity.Hp}/{entity.MaxHp}  ATK {entity.Attack}  DEF {entity.Defense}  {(entity.IsAlive ? "Alive" : "Down")}");
            SetText(buffs, string.IsNullOrEmpty(entity.BuffSummary) ? "Buffs: none" : "Buffs: " + entity.BuffSummary);

            if (hpFill != null)
            {
                float ratio = entity.MaxHp > 0 ? Mathf.Clamp01((float)entity.Hp / entity.MaxHp) : 0f;
                hpFill.style.width = Length.Percent(ratio * 100f);
            }
        }

        private void RefreshEvents(System.Collections.Generic.IReadOnlyList<string> events)
        {
            if (_eventList == null)
                return;

            _eventList.Clear();
            if (events == null || events.Count == 0)
            {
                var empty = new Label("等待运行时事件...");
                empty.AddToClassList(MxUiThemeTokens.EventLogEmpty);
                _eventList.Add(empty);
                return;
            }

            for (int i = events.Count - 1; i >= 0; i--)
            {
                var item = new Label(events[i]);
                item.AddToClassList("event-item");
                item.AddToClassList(MxUiThemeTokens.EventLogRow);
                ApplyLabelStyle(item, _isCompact ? 11 : 12, new Color(0.96f, 0.97f, 1f), FontStyle.Normal);
                _eventList.Add(item);
            }

            if (_eventScroll != null)
            {
                _eventScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
                _eventScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                _eventScroll.mouseWheelScrollSize = 48f;
            }
        }

        private static void SetText(Label label, string value)
        {
            if (label != null)
                label.text = string.IsNullOrEmpty(value) ? "-" : value;
        }
    }
}
