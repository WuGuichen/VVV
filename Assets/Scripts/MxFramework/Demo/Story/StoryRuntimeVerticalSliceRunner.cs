using System.Reflection;
using MxFramework.Story.Runtime;
using MxFramework.Story.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.Story
{
    [AddComponentMenu("MxFramework/Demo/Story Runtime Vertical Slice")]
    public sealed class StoryRuntimeVerticalSliceRunner : MonoBehaviour
    {
        private static readonly FieldInfo DisableNoThemeWarningField =
            typeof(PanelSettings).GetField("m_DisableNoThemeWarning", BindingFlags.NonPublic | BindingFlags.Instance);

        [SerializeField] private UIDocument _document;
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private StyleSheet _styleSheet;

        private StoryRuntimeVerticalSliceDemo _demo;
        private StoryUnityManualFrameProvider _frameProvider;
        private StoryTriggerZoneAdapter _triggerAdapter;
        private StoryPresentationCompletionAdapter _presentationAdapter;
        private VisualElement _root;
        private Label _phaseLabel;
        private Label _dialogueLabel;
        private Label _storyLabel;
        private Label _gameplayLabel;
        private Label _hashLabel;
        private Label _saveLabel;
        private Label _replayLabel;
        private Label _aiLabel;
        private Label _resourceLabel;
        private Label _eventLogLabel;
        private Button _triggerButton;
        private Button _continueButton;
        private Button _choiceButton;
        private Button _saveButton;
        private Button _restoreButton;
        private Button _replayButton;
        private Button _resetButton;

        private void OnEnable()
        {
            _demo = new StoryRuntimeVerticalSliceDemo();
            _frameProvider = new StoryUnityManualFrameProvider(_demo.CurrentCommandFrame);
            EnsureUnityAdapters();
            EnsureDocument();
            RefreshUi();
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
            if (_demo != null)
            {
                _demo.Dispose();
                _demo = null;
            }
        }

        private void Update()
        {
            EnsureDocument();
            RefreshUi();
        }

        public void ConfigureAssets(
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet)
        {
            _document = document;
            _panelSettings = panelSettings;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
        }

        private void EnsureUnityAdapters()
        {
            if (_demo == null)
                return;

            if (_triggerAdapter == null)
                _triggerAdapter = GetComponent<StoryTriggerZoneAdapter>() ?? gameObject.AddComponent<StoryTriggerZoneAdapter>();
            if (_presentationAdapter == null)
                _presentationAdapter = GetComponent<StoryPresentationCompletionAdapter>() ?? gameObject.AddComponent<StoryPresentationCompletionAdapter>();

            _triggerAdapter.TriggerId = StoryRuntimeVerticalSliceDemo.TriggerId;
            _triggerAdapter.TraceId = "story.demo.unity.trigger";
            _triggerAdapter.SourceId = StoryRuntimeCommandSources.UnityAdapter;
            _triggerAdapter.RaiseOnTriggerEnter = false;
            _triggerAdapter.Bind(_demo.StoryModule, _frameProvider);

            _presentationAdapter.TraceId = "story.demo.unity.presentation";
            _presentationAdapter.SourceId = StoryRuntimeCommandSources.PresentationAdapter;
            _presentationAdapter.Bind(_demo.StoryModule, _frameProvider);
        }

        private void EnsureDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.panelSettings == null)
                _document.panelSettings = CreateRuntimePanelSettings(_panelSettings);

            if (_visualTree != null && _document.visualTreeAsset != _visualTree)
                _document.visualTreeAsset = _visualTree;

            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
                return;

            if (_styleSheet != null && !documentRoot.styleSheets.Contains(_styleSheet))
                documentRoot.styleSheets.Add(_styleSheet);

            VisualElement nextRoot = documentRoot.Q<VisualElement>("story-runtime-root");
            if (nextRoot == null)
                nextRoot = BuildFallbackTree(documentRoot);

            if (_root == nextRoot)
                return;

            UnregisterCallbacks();
            _root = nextRoot;
            CacheElements(_root);
            ApplyRuntimeStyles();
            RegisterCallbacks();
        }

        private VisualElement BuildFallbackTree(VisualElement documentRoot)
        {
            documentRoot.Clear();
            var root = new VisualElement { name = "story-runtime-root" };
            root.AddToClassList("story-runtime-root");
            documentRoot.Add(root);

            root.Add(new Label("Story Runtime") { name = "title-label" });
            root.Add(new Label("Ready") { name = "phase-label" });
            root.Add(new Label(string.Empty) { name = "dialogue-label" });
            root.Add(CreateMetric("Story", "story-label"));
            root.Add(CreateMetric("Gameplay", "gameplay-label"));
            root.Add(CreateMetric("Hash", "hash-label"));
            root.Add(CreateMetric("Save", "save-label"));
            root.Add(CreateMetric("Replay", "replay-label"));
            root.Add(CreateMetric("Runtime AI Planner", "ai-label"));
            root.Add(CreateMetric("Resources", "resource-label"));

            var row = new VisualElement { name = "button-row" };
            row.AddToClassList("button-row");
            root.Add(row);
            row.Add(new Button { name = "trigger-button", text = "Trigger" });
            row.Add(new Button { name = "continue-button", text = "Continue" });
            row.Add(new Button { name = "choice-button", text = "Stabilize signal" });
            row.Add(new Button { name = "save-button", text = "Save" });
            row.Add(new Button { name = "restore-button", text = "Restore" });
            row.Add(new Button { name = "replay-button", text = "Replay" });
            row.Add(new Button { name = "reset-button", text = "Reset" });

            root.Add(new Label(string.Empty) { name = "event-log-label" });
            return root;
        }

        private static VisualElement CreateMetric(string title, string valueName)
        {
            var row = new VisualElement();
            row.AddToClassList("metric-row");
            row.Add(new Label(title));
            row.Add(new Label("-") { name = valueName });
            return row;
        }

        private void CacheElements(VisualElement root)
        {
            _phaseLabel = root.Q<Label>("phase-label");
            _dialogueLabel = root.Q<Label>("dialogue-label");
            _storyLabel = root.Q<Label>("story-label");
            _gameplayLabel = root.Q<Label>("gameplay-label");
            _hashLabel = root.Q<Label>("hash-label");
            _saveLabel = root.Q<Label>("save-label");
            _replayLabel = root.Q<Label>("replay-label");
            _aiLabel = root.Q<Label>("ai-label");
            _resourceLabel = root.Q<Label>("resource-label");
            _eventLogLabel = root.Q<Label>("event-log-label");
            _triggerButton = root.Q<Button>("trigger-button");
            _continueButton = root.Q<Button>("continue-button");
            _choiceButton = root.Q<Button>("choice-button");
            _saveButton = root.Q<Button>("save-button");
            _restoreButton = root.Q<Button>("restore-button");
            _replayButton = root.Q<Button>("replay-button");
            _resetButton = root.Q<Button>("reset-button");
        }

        private void RegisterCallbacks()
        {
            if (_triggerButton != null) _triggerButton.clicked += OnTriggerClicked;
            if (_continueButton != null) _continueButton.clicked += OnContinueClicked;
            if (_choiceButton != null) _choiceButton.clicked += OnChoiceClicked;
            if (_saveButton != null) _saveButton.clicked += OnSaveClicked;
            if (_restoreButton != null) _restoreButton.clicked += OnRestoreClicked;
            if (_replayButton != null) _replayButton.clicked += OnReplayClicked;
            if (_resetButton != null) _resetButton.clicked += OnResetClicked;
        }

        private void UnregisterCallbacks()
        {
            if (_triggerButton != null) _triggerButton.clicked -= OnTriggerClicked;
            if (_continueButton != null) _continueButton.clicked -= OnContinueClicked;
            if (_choiceButton != null) _choiceButton.clicked -= OnChoiceClicked;
            if (_saveButton != null) _saveButton.clicked -= OnSaveClicked;
            if (_restoreButton != null) _restoreButton.clicked -= OnRestoreClicked;
            if (_replayButton != null) _replayButton.clicked -= OnReplayClicked;
            if (_resetButton != null) _resetButton.clicked -= OnResetClicked;
        }

        private void ApplyRuntimeStyles()
        {
            if (_root == null)
                return;

            _root.style.flexGrow = 1f;
            ApplyLabel(_root.Q<Label>("title-label"), 24, new Color(0.96f, 0.98f, 0.98f), FontStyle.Bold);
            ApplyLabel(_phaseLabel, 14, new Color(0.68f, 0.91f, 1f), FontStyle.Bold);
            ApplyLabel(_dialogueLabel, 18, new Color(1f, 0.96f, 0.82f), FontStyle.Bold);
            ApplyLabel(_storyLabel, 13, Color.white, FontStyle.Normal);
            ApplyLabel(_gameplayLabel, 13, new Color(0.77f, 1f, 0.83f), FontStyle.Bold);
            ApplyLabel(_hashLabel, 12, new Color(0.84f, 0.88f, 0.93f), FontStyle.Normal);
            ApplyLabel(_saveLabel, 12, new Color(0.86f, 0.91f, 1f), FontStyle.Normal);
            ApplyLabel(_replayLabel, 12, new Color(0.95f, 0.87f, 1f), FontStyle.Normal);
            ApplyLabel(_aiLabel, 12, new Color(0.86f, 0.95f, 1f), FontStyle.Normal);
            ApplyLabel(_resourceLabel, 12, new Color(0.92f, 0.95f, 0.82f), FontStyle.Normal);
            ApplyLabel(_eventLogLabel, 12, new Color(0.86f, 0.90f, 0.94f), FontStyle.Normal);
            ApplyButton(_triggerButton);
            ApplyButton(_continueButton);
            ApplyButton(_choiceButton);
            ApplyButton(_saveButton);
            ApplyButton(_restoreButton);
            ApplyButton(_replayButton);
            ApplyButton(_resetButton);
        }

        private void RefreshUi()
        {
            if (_demo == null || _root == null)
                return;

            StoryRuntimeVerticalSliceSnapshot snapshot = _demo.CreateSnapshot();
            Set(_phaseLabel, ResolvePhase(snapshot));
            Set(_dialogueLabel, snapshot.DialogueText);
            Set(_storyLabel, "Graph " + snapshot.GraphStatus + " / frame " + snapshot.Frame.Value);
            Set(_gameplayLabel, "Signal " + snapshot.SignalValue + " / commands " + snapshot.GameplayCommandCount);
            Set(_hashLabel, snapshot.Hash.ToString());
            Set(_saveLabel, snapshot.SaveStatus);
            Set(_replayLabel, snapshot.ReplayStatus);
            Set(_aiLabel, snapshot.AiFacts);
            Set(_resourceLabel, snapshot.PreloadGroupId);
            Set(_eventLogLabel, string.Join("\n", snapshot.EventLog));

            SetEnabled(_triggerButton, snapshot.GraphStatus == MxFramework.Story.StoryGraphRuntimeStatus.Loaded);
            SetEnabled(_continueButton, snapshot.IsWaitingForPresentation);
            SetEnabled(_choiceButton, snapshot.HasChoice);
            SetEnabled(_restoreButton, _demo.HasSavedState);
        }

        private static string ResolvePhase(StoryRuntimeVerticalSliceSnapshot snapshot)
        {
            if (snapshot.GraphStatus == MxFramework.Story.StoryGraphRuntimeStatus.Completed)
                return "Playable loop completed";
            if (snapshot.HasChoice)
                return "Choice available";
            if (snapshot.IsWaitingForPresentation)
                return "Presentation waiting";
            return "Ready";
        }

        private void OnTriggerClicked()
        {
            if (_demo == null || _triggerAdapter == null)
                return;

            _frameProvider.SetFrame(_demo.CurrentCommandFrame);
            _triggerAdapter.RaiseTrigger();
            _demo.Tick();
            RefreshUi();
        }

        private void OnContinueClicked()
        {
            if (_demo == null || _presentationAdapter == null)
                return;

            StoryRuntimeVerticalSliceSnapshot snapshot = _demo.CreateSnapshot();
            if (!snapshot.IsWaitingForPresentation)
                return;

            _frameProvider.SetFrame(_demo.CurrentCommandFrame);
            _presentationAdapter.CompletePresentation(
                snapshot.WaitingBeatInstanceId,
                snapshot.WaitingStepId,
                StoryRuntimeVerticalSliceDemo.GraphId);
            _demo.Tick();
            RefreshUi();
        }

        private void OnChoiceClicked()
        {
            _demo?.SelectFirstChoiceAndTick();
            RefreshUi();
        }

        private void OnSaveClicked()
        {
            _demo?.Save();
            RefreshUi();
        }

        private void OnRestoreClicked()
        {
            _demo?.Restore();
            EnsureUnityAdapters();
            RefreshUi();
        }

        private void OnReplayClicked()
        {
            _demo?.RunReplaySmoke();
            RefreshUi();
        }

        private void OnResetClicked()
        {
            _demo?.Reset();
            EnsureUnityAdapters();
            RefreshUi();
        }

        private static PanelSettings CreateRuntimePanelSettings(PanelSettings fallback)
        {
            if (fallback != null)
                return fallback;

            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 100f;
            DisableNoThemeWarningField?.SetValue(settings, true);
            return settings;
        }

        private static void Set(Label label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }

        private static void SetEnabled(Button button, bool enabled)
        {
            if (button != null)
                button.SetEnabled(enabled);
        }

        private static void ApplyLabel(Label label, int fontSize, Color color, FontStyle fontStyle)
        {
            if (label == null)
                return;

            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = fontStyle;
        }

        private static void ApplyButton(Button button)
        {
            if (button == null)
                return;

            button.style.height = 34;
            button.style.minWidth = 92;
            button.style.marginRight = 8;
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
    }
}
