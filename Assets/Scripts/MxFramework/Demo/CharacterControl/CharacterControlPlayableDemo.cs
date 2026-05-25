using System;
using MxFramework.Core.Math;
using MxFramework.Input;
using MxFramework.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.CharacterControl
{
    [AddComponentMenu("MxFramework/Demo/Character Control Playable Demo")]
    public sealed class CharacterControlPlayableDemo : MonoBehaviour
    {
        private const int MaxSimulationFramesPerUpdate = 4;
        private const float BoardWidth = 420f;
        private const float BoardHeight = 260f;
        private const float BoardScale = 22f;

        [SerializeField] private UIDocument _document = null;
        [SerializeField] private PanelSettings _panelSettings = null;
        [SerializeField] private VisualTreeAsset _visualTree = null;
        [SerializeField] private StyleSheet _styleSheet = null;
        [SerializeField] private Transform _playerMarker = null;
        [SerializeField] private Transform _enemyMarker = null;
        [SerializeField] [Min(1f)] private float _simulationFramesPerSecond = 60f;
        [SerializeField] private bool _startPaused = false;

        private CharacterControlPlayableSlice _slice;
        private IInputProvider _input;
        private CharacterControlPlayableSnapshot _snapshot;
        private float _simulationFrameAccumulator;
        private bool _paused;
        private Font _runtimeFont;

        private VisualElement _root;
        private VisualElement _avatar;
        private VisualElement _enemy;
        private Label _stateLabel;
        private Label _frameLabel;
        private Label _sourceLabel;
        private Label _positionLabel;
        private Label _actionLabel;
        private Label _pressureLabel;
        private Label _animationLabel;
        private Label _hashLabel;
        private Label _replayLabel;
        private Label _debugLabel;
        private Button _moveButton;
        private Button _jumpButton;
        private Button _attackButton;
        private Button _pressureButton;
        private Button _aiButton;
        private Button _pauseButton;
        private Button _resetButton;

        private void OnEnable()
        {
            ResolveInput();
            _slice = new CharacterControlPlayableSlice();
            _snapshot = _slice.CurrentSnapshot;
            _paused = _startPaused;
            _simulationFrameAccumulator = 0f;
            EnsureDocument();
            EnsureWorldMarkers();
            RefreshUi(force: true);
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
            _slice?.Dispose();
            _slice = null;
            _root = null;
            _avatar = null;
            _enemy = null;
        }

        public void ConfigureAssets(
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Transform playerMarker,
            Transform enemyMarker)
        {
            _document = document;
            _panelSettings = panelSettings;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
            _playerMarker = playerMarker;
            _enemyMarker = enemyMarker;
        }

        private void Update()
        {
            if (_slice == null)
                _slice = new CharacterControlPlayableSlice();

            EnsureDocument();
            EnsureWorldMarkers();
            ResolveInput();
            HandleInput(_input != null ? _input.Snapshot : InputSnapshot.Empty);

            if (!_paused)
                TickSimulation();

            RefreshUi(force: false);
        }

        private void HandleInput(InputSnapshot input)
        {
            if (input.RestartPressed)
            {
                ResetSlice();
                return;
            }

            if (input.PausePressed || input.DebugSecondaryPressed)
                TogglePaused();

            Vector2 move = input.Move;
            if (Mathf.Abs(move.x) > 0f || Mathf.Abs(move.y) > 0f)
            {
                if (move.sqrMagnitude > 1f)
                    move.Normalize();
                _slice.EnqueueMove(move.x, move.y, input.SprintHeld);
            }

            if (input.JumpPressed)
                _slice.EnqueueJump();
            if (input.AttackPrimaryPressed || input.DebugPrimaryPressed)
                _slice.EnqueueAttack();
            if (input.AttackSecondaryPressed || input.ToggleHudPressed)
                _slice.EnqueuePressureBreak();
            if (input.DebugStepPressed || input.DebugCyclePressed)
                _slice.EnqueueRuntimeAiStep();
        }

        private void TickSimulation()
        {
            _simulationFrameAccumulator += Time.unscaledDeltaTime * Mathf.Max(1f, _simulationFramesPerSecond);
            int frames = 0;
            while (_simulationFrameAccumulator >= 1f && frames < MaxSimulationFramesPerUpdate)
            {
                _snapshot = _slice.Tick();
                _simulationFrameAccumulator -= 1f;
                frames++;
            }
        }

        private void EnsureDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_panelSettings != null && _document.panelSettings != _panelSettings)
                _document.panelSettings = _panelSettings;
            if (_visualTree != null && _document.visualTreeAsset != _visualTree)
                _document.visualTreeAsset = _visualTree;
            if (_document.panelSettings == null)
                _document.panelSettings = CreateFallbackPanelSettings();

            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
                return;

            if (_styleSheet != null && !documentRoot.styleSheets.Contains(_styleSheet))
                documentRoot.styleSheets.Add(_styleSheet);

            VisualElement nextRoot = documentRoot.Q<VisualElement>("character-control-root");
            if (_root == nextRoot && _avatar != null)
                return;

            UnregisterCallbacks();
            CacheElements(documentRoot);
            ApplyRuntimeTextStyles();
            RegisterCallbacks();
        }

        private void CacheElements(VisualElement root)
        {
            _root = root.Q<VisualElement>("character-control-root");
            _avatar = root.Q<VisualElement>("avatar");
            _enemy = root.Q<VisualElement>("enemy");
            _stateLabel = root.Q<Label>("state-label");
            _frameLabel = root.Q<Label>("frame-label");
            _sourceLabel = root.Q<Label>("source-label");
            _positionLabel = root.Q<Label>("position-label");
            _actionLabel = root.Q<Label>("action-label");
            _pressureLabel = root.Q<Label>("pressure-label");
            _animationLabel = root.Q<Label>("animation-label");
            _hashLabel = root.Q<Label>("hash-label");
            _replayLabel = root.Q<Label>("replay-label");
            _debugLabel = root.Q<Label>("debug-label");
            _moveButton = root.Q<Button>("move-button");
            _jumpButton = root.Q<Button>("jump-button");
            _attackButton = root.Q<Button>("attack-button");
            _pressureButton = root.Q<Button>("pressure-button");
            _aiButton = root.Q<Button>("ai-button");
            _pauseButton = root.Q<Button>("pause-button");
            _resetButton = root.Q<Button>("reset-button");
        }

        private void RegisterCallbacks()
        {
            _moveButton?.RegisterCallback<ClickEvent>(OnMoveClicked);
            _jumpButton?.RegisterCallback<ClickEvent>(OnJumpClicked);
            _attackButton?.RegisterCallback<ClickEvent>(OnAttackClicked);
            _pressureButton?.RegisterCallback<ClickEvent>(OnPressureClicked);
            _aiButton?.RegisterCallback<ClickEvent>(OnAiClicked);
            _pauseButton?.RegisterCallback<ClickEvent>(OnPauseClicked);
            _resetButton?.RegisterCallback<ClickEvent>(OnResetClicked);
        }

        private void UnregisterCallbacks()
        {
            _moveButton?.UnregisterCallback<ClickEvent>(OnMoveClicked);
            _jumpButton?.UnregisterCallback<ClickEvent>(OnJumpClicked);
            _attackButton?.UnregisterCallback<ClickEvent>(OnAttackClicked);
            _pressureButton?.UnregisterCallback<ClickEvent>(OnPressureClicked);
            _aiButton?.UnregisterCallback<ClickEvent>(OnAiClicked);
            _pauseButton?.UnregisterCallback<ClickEvent>(OnPauseClicked);
            _resetButton?.UnregisterCallback<ClickEvent>(OnResetClicked);
        }

        private void RefreshUi(bool force)
        {
            if (_slice == null)
                return;

            if (force)
                _snapshot = _slice.CurrentSnapshot;

            SetText(_stateLabel, _snapshot.State.ToString());
            SetText(_frameLabel, _snapshot.Frame.Value.ToString());
            SetText(_sourceLabel, _snapshot.LastCommandSource);
            SetText(_positionLabel, FormatPosition(_snapshot.Position));
            SetText(_actionLabel, FormatAction(_snapshot));
            SetText(_pressureLabel, FormatPressure(_snapshot));
            SetText(_animationLabel, _snapshot.LastAnimation.EventKind.ToString());
            SetText(_hashLabel, _snapshot.RuntimeHash.ToString("X16"));
            SetText(_replayLabel, _snapshot.ReplayFrameCount.ToString());
            SetText(_debugLabel, SummarizeDebug(_snapshot));
            if (_pauseButton != null)
                _pauseButton.text = _paused ? "Resume" : "Pause";

            SyncUiBoard(_snapshot.Position);
            SyncWorldMarkers(_snapshot);
        }

        private void SyncUiBoard(FixVector3 position)
        {
            if (_avatar != null)
            {
                _avatar.style.left = BoardWidth * 0.5f + ToFloat(position.X) * BoardScale - 9f;
                _avatar.style.top = BoardHeight * 0.5f - ToFloat(position.Z) * BoardScale - 9f;
            }

            if (_enemy != null)
            {
                _enemy.style.left = BoardWidth * 0.5f + 3f * BoardScale - 8f;
                _enemy.style.top = BoardHeight * 0.5f - 2f * BoardScale - 8f;
            }
        }

        private void EnsureWorldMarkers()
        {
            if (_playerMarker == null)
            {
                GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "CharacterControlPlayer";
                _playerMarker = player.transform;
            }

            if (_enemyMarker == null)
            {
                GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                enemy.name = "CharacterControlTarget";
                enemy.transform.position = new Vector3(3f, 0.5f, 2f);
                _enemyMarker = enemy.transform;
            }
        }

        private void SyncWorldMarkers(CharacterControlPlayableSnapshot snapshot)
        {
            if (_playerMarker != null)
            {
                _playerMarker.position = new Vector3(
                    ToFloat(snapshot.Position.X),
                    ToFloat(snapshot.Position.Y),
                    ToFloat(snapshot.Position.Z));
            }

            if (_enemyMarker != null)
                _enemyMarker.position = new Vector3(3f, 0.5f, 2f);
        }

        private void ApplyRuntimeTextStyles()
        {
            EnsureRuntimeFont();
            ApplyLabels(_root);
            ApplyButtons(_root);
        }

        private void ApplyLabels(VisualElement root)
        {
            if (root == null)
                return;

            var labels = root.Query<Label>().ToList();
            for (int i = 0; i < labels.Count; i++)
            {
                if (_runtimeFont != null)
                    labels[i].style.unityFont = new StyleFont(_runtimeFont);
                labels[i].style.whiteSpace = WhiteSpace.Normal;
            }
        }

        private void ApplyButtons(VisualElement root)
        {
            if (root == null)
                return;

            var buttons = root.Query<Button>().ToList();
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].focusable = false;
                if (_runtimeFont != null)
                    buttons[i].style.unityFont = new StyleFont(_runtimeFont);
            }
        }

        private void EnsureRuntimeFont()
        {
            if (_runtimeFont != null)
                return;

            _runtimeFont = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_runtimeFont == null)
                _runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Helvetica", "Arial" }, 14);
        }

        private void ResetSlice()
        {
            _slice.ResetAll();
            _snapshot = _slice.CurrentSnapshot;
            _simulationFrameAccumulator = 0f;
            RefreshUi(force: true);
        }

        private void TogglePaused()
        {
            _paused = !_paused;
        }

        private void OnMoveClicked(ClickEvent evt)
        {
            _slice.EnqueueMove(1f, 0.5f, sprintHeld: false);
        }

        private void OnJumpClicked(ClickEvent evt)
        {
            _slice.EnqueueJump();
        }

        private void OnAttackClicked(ClickEvent evt)
        {
            _slice.EnqueueAttack();
        }

        private void OnPressureClicked(ClickEvent evt)
        {
            _slice.EnqueuePressureBreak();
        }

        private void OnAiClicked(ClickEvent evt)
        {
            _slice.EnqueueRuntimeAiStep();
        }

        private void OnPauseClicked(ClickEvent evt)
        {
            TogglePaused();
        }

        private void OnResetClicked(ClickEvent evt)
        {
            ResetSlice();
        }

        private void ResolveInput()
        {
            if (_input == null)
                _input = InputProviderResolver.ResolveOrCreateDefault(this);
        }

        private static PanelSettings CreateFallbackPanelSettings()
        {
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 100f;
            return settings;
        }

        private static string FormatPosition(FixVector3 position)
        {
            return ToFloat(position.X).ToString("0.00")
                + ", "
                + ToFloat(position.Y).ToString("0.00")
                + ", "
                + ToFloat(position.Z).ToString("0.00");
        }

        private static string FormatAction(CharacterControlPlayableSnapshot snapshot)
        {
            if (snapshot.LastActionEvent.Type != 0)
                return snapshot.LastActionEvent.Type + " " + snapshot.LastActionEvent.Request.Kind;
            return snapshot.LastCommand.ActionRequest.Kind.ToString();
        }

        private static string FormatPressure(CharacterControlPlayableSnapshot snapshot)
        {
            return snapshot.LastPressureResult.Kind == 0
                ? "None"
                : snapshot.LastPressureResult.Kind + " " + snapshot.LastPressureResult.Message;
        }

        private static string SummarizeDebug(CharacterControlPlayableSnapshot snapshot)
        {
            return snapshot.DebugDashboard == null
                ? "unavailable"
                : snapshot.DebugDashboard.SourceCount + " source / " + snapshot.DebugDashboard.ErrorCount + " errors";
        }

        private static void SetText(Label label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }

        private static float ToFloat(Fix64 value)
        {
            return value.RawValue / (float)Fix64.Scale;
        }
    }
}
