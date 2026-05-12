using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Input;
using MxFramework.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.Breakout
{
    [AddComponentMenu("MxFramework/Demo/Breakout Playable Demo")]
    public sealed class BreakoutPlayableDemo : MonoBehaviour
    {
        private const int DefaultBoardWidthPixels = 720;
        private const int DefaultBoardHeightPixels = 520;
        private const int BoardPaddingPixels = 12;
        private const int RootPaddingX = 24;
        private const int RootPaddingY = 20;
        private const int SidePanelWidthPixels = 300;
        private const int SidePanelCompactWidthPixels = 250;
        private const int MaxSimulationFramesPerUpdate = 5;

        [SerializeField] private UIDocument _document = null;
        [SerializeField] private VisualTreeAsset _visualTree = null;
        [SerializeField] private StyleSheet _styleSheet = null;
        [SerializeField] [Min(1f)] private float _simulationFramesPerSecond = 60f;
        [SerializeField] private bool _autoCreateAppFlow = true;

        private readonly List<VisualElement> _brickElements = new List<VisualElement>(80);
        private readonly List<VisualElement> _ballElements = new List<VisualElement>(4);
        private BreakoutRuntimeBridge _runtime;
        private IInputProvider _input;
        private BreakoutViewSnapshot _snapshot = BreakoutViewSnapshot.CreateEmpty();
        private BreakoutUiMode _mode = BreakoutUiMode.Boot;
        private AppFlowSnapshot _appSnapshot;
        private SceneFlowSnapshot _sceneSnapshot;
        private long _frame;
        private float _simulationFrameAccumulator;
        private bool _isPaused;
        private Vector2 _previousMoveInput;
        private string _dependencyStatus = string.Empty;
        private string _lastBrickLayoutSignature = string.Empty;
        private Font _runtimeFont;

        private VisualElement _root;
        private VisualElement _shell;
        private VisualElement _boardWrap;
        private VisualElement _board;
        private VisualElement _brickLayer;
        private VisualElement _paddle;
        private VisualElement _ball;
        private VisualElement _menuPanel;
        private VisualElement _loadingPanel;
        private VisualElement _gameOverPanel;
        private VisualElement _sidePanel;
        private VisualElement _progressFill;
        private Label _appStateLabel;
        private Label _scoreLabel;
        private Label _livesLabel;
        private Label _frameLabel;
        private Label _hashLabel;
        private Label _replayLabel;
        private Label _sceneLabel;
        private Label _progressLabel;
        private Label _statusLabel;
        private Label _gameOverTitle;
        private Label _gameOverStats;
        private Button _startButton;
        private Button _pauseButton;
        private Button _restartButton;
        private Button _launchButton;
        private Button _menuButton;
        private Button _gameOverRestartButton;
        private Button _gameOverMenuButton;

        public event Action StartRequested;
        public event Action RestartRequested;
        public event Action MenuRequested;

        public enum BreakoutUiMode
        {
            Boot = 0,
            Menu = 1,
            Loading = 2,
            Gameplay = 3,
            GameOver = 4
        }

        public bool IsGameOver => _snapshot.IsGameOver || _snapshot.IsVictory;

        private void OnEnable()
        {
            ResolveInput();
            EnsureDocument();
            if (_autoCreateAppFlow && GetComponent<BreakoutAppFlowDemo>() == null)
            {
                gameObject.AddComponent<BreakoutAppFlowDemo>();
            }

            ResetGame();
            RefreshUi(force: true);
        }

        private void OnDisable()
        {
            UnregisterButtonCallbacks();
            DisposeRuntime();
            _brickElements.Clear();
            _ballElements.Clear();
            _root = null;
            _board = null;
        }

        private void Update()
        {
            EnsureDocument();
            ResolveInput();
            InputSnapshot input = _input != null ? _input.Snapshot : InputSnapshot.Empty;
            HandleGlobalInput(input);

            if (_mode == BreakoutUiMode.Gameplay && !_isPaused)
            {
                EnqueueGameplayInput(input);
                TickSimulation();
            }

            _previousMoveInput = input.Move;
            RefreshUi(force: false);
        }

        public void SetMode(BreakoutUiMode mode)
        {
            if (_mode == mode)
            {
                return;
            }

            _mode = mode;
            if (mode == BreakoutUiMode.Gameplay)
            {
                _isPaused = false;
            }

            RefreshUi(force: true);
        }

        public void SetFlowSnapshots(AppFlowSnapshot appSnapshot, SceneFlowSnapshot sceneSnapshot)
        {
            _appSnapshot = appSnapshot;
            _sceneSnapshot = sceneSnapshot;
            RefreshFlowLabels();
        }

        public void SetDependencyStatus(string status)
        {
            _dependencyStatus = status ?? string.Empty;
            SetText(_statusLabel, _dependencyStatus);
        }

        public void ResetGame()
        {
            DisposeRuntime();
            _runtime = new BreakoutRuntimeBridge();
            _runtime.TryCreateRunner(out _dependencyStatus);
            _frame = 0;
            _simulationFrameAccumulator = 0f;
            _isPaused = false;
            _lastBrickLayoutSignature = string.Empty;
            CaptureSnapshot();
            RefreshUi(force: true);
        }

        private void EnsureDocument()
        {
            if (_document == null)
            {
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            }

            if (_visualTree != null && _document.visualTreeAsset != _visualTree)
            {
                _document.visualTreeAsset = _visualTree;
            }

            if (_document.panelSettings == null)
            {
                _document.panelSettings = CreateFallbackPanelSettings();
            }

            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
            {
                return;
            }

            if (_styleSheet != null && !documentRoot.styleSheets.Contains(_styleSheet))
            {
                documentRoot.styleSheets.Add(_styleSheet);
            }

            ApplyDocumentLayout(documentRoot);
            VisualElement nextRoot = documentRoot.Q<VisualElement>("breakout-root");
            if (_root == nextRoot && _board != null)
            {
                ApplyRuntimeLayout();
                return;
            }

            UnregisterButtonCallbacks();
            CacheElements(documentRoot);
            ApplyRuntimeTextStyles();
            RegisterButtonCallbacks();
            ApplyRuntimeLayout();
        }

        private void CacheElements(VisualElement root)
        {
            _root = root.Q<VisualElement>("breakout-root");
            _shell = root.Q<VisualElement>("breakout-shell");
            _boardWrap = root.Q<VisualElement>("board-wrap");
            _board = root.Q<VisualElement>("breakout-board");
            _brickLayer = root.Q<VisualElement>("brick-layer");
            _paddle = root.Q<VisualElement>("paddle");
            _ball = root.Q<VisualElement>("ball");
            _menuPanel = root.Q<VisualElement>("menu-panel");
            _loadingPanel = root.Q<VisualElement>("loading-panel");
            _gameOverPanel = root.Q<VisualElement>("game-over-panel");
            _sidePanel = root.Q<VisualElement>("side-panel");
            _progressFill = root.Q<VisualElement>("progress-fill");
            _appStateLabel = root.Q<Label>("app-state-label");
            _scoreLabel = root.Q<Label>("score-label");
            _livesLabel = root.Q<Label>("lives-label");
            _frameLabel = root.Q<Label>("frame-label");
            _hashLabel = root.Q<Label>("hash-label");
            _replayLabel = root.Q<Label>("replay-label");
            _sceneLabel = root.Q<Label>("scene-label");
            _progressLabel = root.Q<Label>("progress-label");
            _statusLabel = root.Q<Label>("status-label");
            _gameOverTitle = root.Q<Label>("game-over-title");
            _gameOverStats = root.Q<Label>("game-over-stats");
            _startButton = root.Q<Button>("start-button");
            _pauseButton = root.Q<Button>("pause-button");
            _restartButton = root.Q<Button>("restart-button");
            _launchButton = root.Q<Button>("launch-button");
            _menuButton = root.Q<Button>("menu-button");
            _gameOverRestartButton = root.Q<Button>("game-over-restart-button");
            _gameOverMenuButton = root.Q<Button>("game-over-menu-button");
        }

        private void RegisterButtonCallbacks()
        {
            _startButton?.RegisterCallback<ClickEvent>(OnStartClicked);
            _pauseButton?.RegisterCallback<ClickEvent>(OnPauseClicked);
            _restartButton?.RegisterCallback<ClickEvent>(OnRestartClicked);
            _launchButton?.RegisterCallback<ClickEvent>(OnLaunchClicked);
            _menuButton?.RegisterCallback<ClickEvent>(OnMenuClicked);
            _gameOverRestartButton?.RegisterCallback<ClickEvent>(OnRestartClicked);
            _gameOverMenuButton?.RegisterCallback<ClickEvent>(OnMenuClicked);
        }

        private void UnregisterButtonCallbacks()
        {
            _startButton?.UnregisterCallback<ClickEvent>(OnStartClicked);
            _pauseButton?.UnregisterCallback<ClickEvent>(OnPauseClicked);
            _restartButton?.UnregisterCallback<ClickEvent>(OnRestartClicked);
            _launchButton?.UnregisterCallback<ClickEvent>(OnLaunchClicked);
            _menuButton?.UnregisterCallback<ClickEvent>(OnMenuClicked);
            _gameOverRestartButton?.UnregisterCallback<ClickEvent>(OnRestartClicked);
            _gameOverMenuButton?.UnregisterCallback<ClickEvent>(OnMenuClicked);
        }

        private void OnStartClicked(ClickEvent evt)
        {
            StartRequested?.Invoke();
        }

        private void OnPauseClicked(ClickEvent evt)
        {
            _isPaused = !_isPaused;
            QueueCommand("Pause");
            RefreshUi(force: true);
        }

        private void OnRestartClicked(ClickEvent evt)
        {
            RestartRequested?.Invoke();
        }

        private void OnLaunchClicked(ClickEvent evt)
        {
            QueueCommand("Launch");
        }

        private void OnMenuClicked(ClickEvent evt)
        {
            MenuRequested?.Invoke();
        }

        private void HandleGlobalInput(InputSnapshot input)
        {
            if (input.SubmitPressed && (_mode == BreakoutUiMode.Menu || _mode == BreakoutUiMode.Boot))
            {
                StartRequested?.Invoke();
            }

            if (input.RestartPressed)
            {
                RestartRequested?.Invoke();
            }

            if (_mode == BreakoutUiMode.Gameplay && (input.PausePressed || input.DebugSecondaryPressed))
            {
                _isPaused = !_isPaused;
                QueueCommand("Pause");
            }
        }

        private void EnqueueGameplayInput(InputSnapshot input)
        {
            if (_runtime == null || !_runtime.IsReady)
            {
                return;
            }

            if (input.Move.x < -0.5f)
            {
                QueueCommand("MoveLeft");
            }

            if (input.Move.x > 0.5f)
            {
                QueueCommand("MoveRight");
            }

            if (input.JumpPressed || PressedPositive(input.Move.y, _previousMoveInput.y))
            {
                QueueCommand("Launch");
            }
        }

        private void ResolveInput()
        {
            if (_input == null)
                _input = InputProviderResolver.ResolveOrCreateDefault(this);
        }

        private static bool PressedPositive(float current, float previous)
        {
            return current > 0.5f && previous <= 0.5f;
        }

        private void QueueCommand(string commandName)
        {
            if (_runtime == null || !_runtime.IsReady)
            {
                return;
            }

            string error;
            if (!_runtime.EnqueueCommand(_frame, commandName, out error))
            {
                _dependencyStatus = error;
            }
        }

        private void TickSimulation()
        {
            if (_runtime == null || !_runtime.IsReady || IsGameOver)
            {
                return;
            }

            float framesPerSecond = Mathf.Max(1f, _simulationFramesPerSecond);
            _simulationFrameAccumulator += Time.deltaTime * framesPerSecond;

            int steps = 0;
            while (_simulationFrameAccumulator >= 1f && steps < MaxSimulationFramesPerUpdate)
            {
                string error;
                if (!_runtime.TickFrame(_frame, out error))
                {
                    _dependencyStatus = error;
                    break;
                }

                _frame++;
                _simulationFrameAccumulator -= 1f;
                steps++;
            }

            if (steps == MaxSimulationFramesPerUpdate && _simulationFrameAccumulator >= 1f)
            {
                _simulationFrameAccumulator = 0f;
            }

            CaptureSnapshot();
        }

        private void CaptureSnapshot()
        {
            if (_runtime == null || !_runtime.IsReady)
            {
                _snapshot = BreakoutViewSnapshot.CreateEmpty();
                return;
            }

            string error;
            if (!_runtime.TryCaptureSnapshot(out _snapshot, out error))
            {
                _dependencyStatus = error;
                _snapshot = BreakoutViewSnapshot.CreateEmpty();
            }
        }

        private void RefreshUi(bool force)
        {
            EnsureDocument();
            CaptureSnapshot();
            ApplyRuntimeLayout();
            RefreshModeClasses();
            RefreshFlowLabels();
            RefreshStats();
            RefreshGameElements(force);
            SetText(_statusLabel, BuildStatusText());
        }

        private void RefreshModeClasses()
        {
            if (_root == null)
            {
                return;
            }

            _root.EnableInClassList("mode-boot", _mode == BreakoutUiMode.Boot);
            _root.EnableInClassList("mode-menu", _mode == BreakoutUiMode.Menu);
            _root.EnableInClassList("mode-loading", _mode == BreakoutUiMode.Loading);
            _root.EnableInClassList("mode-gameplay", _mode == BreakoutUiMode.Gameplay);
            _root.EnableInClassList("mode-game-over", _mode == BreakoutUiMode.GameOver);
            _root.EnableInClassList("is-paused", _isPaused);
            _root.EnableInClassList("runtime-missing", _runtime == null || !_runtime.IsReady);
        }

        private void RefreshFlowLabels()
        {
            string appState = _appSnapshot != null && !string.IsNullOrEmpty(_appSnapshot.CurrentStateId)
                ? _appSnapshot.CurrentStateId
                : _mode.ToString();
            SetText(_appStateLabel, appState);

            if (_sceneSnapshot != null)
            {
                string scene = _sceneSnapshot.IsBusy
                    ? _sceneSnapshot.CurrentOperationType + " " + _sceneSnapshot.CurrentSceneKey
                    : "Active " + (_sceneSnapshot.ActiveSceneKey ?? string.Empty);
                SetText(_sceneLabel, scene);

                float progress = _sceneSnapshot.IsBusy ? _sceneSnapshot.Progress : 1f;
                SetText(_progressLabel, Mathf.RoundToInt(progress * 100f) + "%");
                if (_progressFill != null)
                {
                    _progressFill.style.width = Length.Percent(progress * 100f);
                }
            }
            else
            {
                SetText(_sceneLabel, "SceneFlow pending");
                SetText(_progressLabel, "0%");
                if (_progressFill != null)
                {
                    _progressFill.style.width = Length.Percent(0f);
                }
            }
        }

        private void RefreshStats()
        {
            SetText(_scoreLabel, _snapshot.Score.ToString());
            SetText(_livesLabel, _snapshot.Lives.ToString());
            SetText(_frameLabel, "Frame " + _frame);
            SetText(_hashLabel, _runtime != null ? _runtime.LastResultHash.ToString() : "0");
            SetText(_replayLabel, _runtime != null ? _runtime.ReplayFrameCount.ToString() : "0");
            SetText(_pauseButton, _isPaused ? "Resume" : "Pause");
            SetText(_gameOverTitle, _snapshot.IsVictory ? "Stage Clear" : "Game Over");
            SetText(_gameOverStats, "Score " + _snapshot.Score + " / Lives " + _snapshot.Lives);
        }

        private void RefreshGameElements(bool force)
        {
            if (_board == null)
            {
                return;
            }

            string signature = _snapshot.GetBrickLayoutSignature();
            if (force || !string.Equals(signature, _lastBrickLayoutSignature, StringComparison.Ordinal))
            {
                RebuildBricks();
                _lastBrickLayoutSignature = signature;
            }

            float boardWidth = GetBoardWidthPixels();
            float boardHeight = GetBoardHeightPixels();
            float scaleX = boardWidth / Mathf.Max(1f, _snapshot.Width);
            float scaleY = boardHeight / Mathf.Max(1f, _snapshot.Height);

            for (int i = 0; i < _brickElements.Count && i < _snapshot.Bricks.Count; i++)
            {
                BreakoutBrickView brick = _snapshot.Bricks[i];
                VisualElement element = _brickElements[i];
                Label buffLabel = element.Q<Label>("buff-label");
                element.style.left = brick.X * scaleX;
                element.style.top = boardHeight - (brick.Y + brick.Height) * scaleY;
                element.style.width = Mathf.Max(2f, brick.Width * scaleX - 2f);
                element.style.height = Mathf.Max(2f, brick.Height * scaleY - 2f);
                element.EnableInClassList("brick-dead", !brick.IsActive);
                element.EnableInClassList("brick-strong", brick.HitPoints > 1);
                UpdateBrickBuffClasses(element, brick.PowerUpType);
                if (buffLabel != null)
                {
                    buffLabel.text = GetPowerUpShortLabel(brick.PowerUpType);
                    buffLabel.style.display = brick.IsActive && IsPowerUpBrick(brick) ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            if (_paddle != null)
            {
                _paddle.style.left = (_snapshot.PaddleX - _snapshot.PaddleWidth * 0.5f) * scaleX;
                _paddle.style.top = boardHeight - (_snapshot.PaddleY + _snapshot.PaddleHeight) * scaleY;
                _paddle.style.width = Mathf.Max(16f, _snapshot.PaddleWidth * scaleX);
                _paddle.style.height = Mathf.Max(8f, _snapshot.PaddleHeight * scaleY);
            }

            EnsureBallElements();
            for (int i = 0; i < _ballElements.Count; i++)
            {
                BreakoutBallView ball = i < _snapshot.Balls.Count
                    ? _snapshot.Balls[i]
                    : new BreakoutBallView { X = _snapshot.BallX, Y = _snapshot.BallY, Radius = _snapshot.BallRadius };
                VisualElement element = _ballElements[i];
                float diameter = Mathf.Max(8f, ball.Radius * 2f * Mathf.Min(scaleX, scaleY));
                element.style.left = ball.X * scaleX - diameter * 0.5f;
                element.style.top = boardHeight - ball.Y * scaleY - diameter * 0.5f;
                element.style.width = diameter;
                element.style.height = diameter;
            }
        }

        private void RebuildBricks()
        {
            if (_brickLayer == null)
            {
                return;
            }

            _brickLayer.Clear();
            _brickElements.Clear();
            for (int i = 0; i < _snapshot.Bricks.Count; i++)
            {
                var brick = new VisualElement();
                brick.AddToClassList("brick");
                brick.AddToClassList("brick-" + _snapshot.Bricks[i].Type.ToString().ToLowerInvariant());
                var buffLabel = new Label(GetPowerUpShortLabel(_snapshot.Bricks[i].PowerUpType));
                buffLabel.name = "buff-label";
                buffLabel.AddToClassList("brick-buff-label");
                buffLabel.style.display = IsPowerUpBrick(_snapshot.Bricks[i]) ? DisplayStyle.Flex : DisplayStyle.None;
                brick.Add(buffLabel);
                _brickElements.Add(brick);
                _brickLayer.Add(brick);
            }
        }

        private static void UpdateBrickBuffClasses(VisualElement element, string powerUpType)
        {
            element.EnableInClassList("brick-buff-wide", string.Equals(powerUpType, "widepaddle", StringComparison.Ordinal));
            element.EnableInClassList("brick-buff-slow", string.Equals(powerUpType, "slowball", StringComparison.Ordinal));
            element.EnableInClassList("brick-buff-multi", string.Equals(powerUpType, "multiball", StringComparison.Ordinal));
            element.EnableInClassList("brick-buff-life", string.Equals(powerUpType, "extralife", StringComparison.Ordinal));
            element.EnableInClassList("brick-buff-laser", string.Equals(powerUpType, "laser", StringComparison.Ordinal));
        }

        private static bool IsPowerUpBrick(BreakoutBrickView brick)
        {
            return string.Equals(brick.Type, "powerup", StringComparison.Ordinal)
                && !string.IsNullOrEmpty(brick.PowerUpType)
                && !string.Equals(brick.PowerUpType, "none", StringComparison.Ordinal);
        }

        private static string GetPowerUpShortLabel(string powerUpType)
        {
            if (string.Equals(powerUpType, "widepaddle", StringComparison.Ordinal))
            {
                return "W";
            }

            if (string.Equals(powerUpType, "slowball", StringComparison.Ordinal))
            {
                return "S";
            }

            if (string.Equals(powerUpType, "multiball", StringComparison.Ordinal))
            {
                return "M";
            }

            if (string.Equals(powerUpType, "extralife", StringComparison.Ordinal))
            {
                return "+";
            }

            if (string.Equals(powerUpType, "laser", StringComparison.Ordinal))
            {
                return "L";
            }

            return string.Empty;
        }

        private void EnsureBallElements()
        {
            if (_board == null)
            {
                return;
            }

            int desired = Mathf.Max(1, _snapshot.Balls.Count);
            if (_ball != null && !_ballElements.Contains(_ball))
            {
                _ballElements.Add(_ball);
            }

            while (_ballElements.Count < desired)
            {
                var ball = new VisualElement();
                ball.AddToClassList("ball");
                _ballElements.Add(ball);
                _board.Add(ball);
            }

            for (int i = 0; i < _ballElements.Count; i++)
            {
                _ballElements[i].style.display = i < desired ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private string BuildStatusText()
        {
            if (!string.IsNullOrEmpty(_dependencyStatus))
            {
                return _dependencyStatus;
            }

            if (_mode == BreakoutUiMode.Gameplay && _isPaused)
            {
                return "Paused";
            }

            if (_mode == BreakoutUiMode.Gameplay)
            {
                return "Running L" + (_snapshot.LevelIndex + 1) + " Balls " + Mathf.Max(1, _snapshot.Balls.Count) + " Event " + _snapshot.LastEvent;
            }

            return "Ready";
        }

        private void DisposeRuntime()
        {
            if (_runtime != null)
            {
                _runtime.Dispose();
                _runtime = null;
            }
        }

        private void ApplyRuntimeLayout()
        {
            if (_root == null)
            {
                return;
            }

            float width = Mathf.Max(1, Screen.width);
            float height = Mathf.Max(1, Screen.height);
            bool compact = width < 980f;
            float sidePanelWidth = compact ? SidePanelCompactWidthPixels : SidePanelWidthPixels;
            float boardMaxWidth = Mathf.Max(320f, width - RootPaddingX * 2f - sidePanelWidth - 22f - BoardPaddingPixels * 2f);
            float boardMaxHeight = Mathf.Max(260f, height - RootPaddingY * 2f - BoardPaddingPixels * 2f);
            float boardWidth = Mathf.Min(DefaultBoardWidthPixels, boardMaxWidth);
            float boardHeight = Mathf.Min(DefaultBoardHeightPixels, boardMaxHeight);

            _root.style.width = width;
            _root.style.height = height;
            if (_shell != null)
            {
                _shell.style.height = height - RootPaddingY * 2f;
            }

            if (_boardWrap != null)
            {
                _boardWrap.style.width = boardWidth + BoardPaddingPixels * 2f;
                _boardWrap.style.height = boardHeight + BoardPaddingPixels * 2f;
            }

            if (_board != null)
            {
                _board.style.width = boardWidth;
                _board.style.height = boardHeight;
            }

            if (_sidePanel != null)
            {
                _sidePanel.style.width = sidePanelWidth;
                _sidePanel.style.height = boardHeight + BoardPaddingPixels * 2f;
            }
        }

        private float GetBoardWidthPixels()
        {
            return _board != null && _board.resolvedStyle.width > 1f
                ? _board.resolvedStyle.width
                : DefaultBoardWidthPixels;
        }

        private float GetBoardHeightPixels()
        {
            return _board != null && _board.resolvedStyle.height > 1f
                ? _board.resolvedStyle.height
                : DefaultBoardHeightPixels;
        }

        private void ApplyRuntimeTextStyles()
        {
            EnsureRuntimeFont();
            ApplyLabelsByClass(_root, "label-soft", 12, new Color(0.70f, 0.76f, 0.82f), FontStyle.Normal, TextAnchor.MiddleLeft);
            ApplyLabelsByClass(_root, "stat-value", 16, new Color(0.95f, 0.97f, 0.98f), FontStyle.Bold, TextAnchor.MiddleRight);
            ApplyLabelsByClass(_root, "panel-title", 20, new Color(0.95f, 0.97f, 0.98f), FontStyle.Bold, TextAnchor.MiddleCenter);
            ApplyLabelStyle(_appStateLabel, 13, new Color(0.88f, 0.96f, 0.92f), FontStyle.Bold, TextAnchor.MiddleCenter);
            ApplyLabelStyle(_statusLabel, 12, new Color(0.86f, 0.88f, 0.91f), FontStyle.Normal, TextAnchor.MiddleLeft);
            ApplyLabelStyle(_hashLabel, 12, new Color(0.89f, 0.92f, 0.95f), FontStyle.Normal, TextAnchor.MiddleRight);
            ApplyButtonStyle(_startButton);
            ApplyButtonStyle(_pauseButton);
            ApplyButtonStyle(_restartButton);
            ApplyButtonStyle(_launchButton);
            ApplyButtonStyle(_menuButton);
            ApplyButtonStyle(_gameOverRestartButton);
            ApplyButtonStyle(_gameOverMenuButton);
        }

        private void ApplyLabelsByClass(VisualElement root, string className, int fontSize, Color color, FontStyle fontStyle, TextAnchor alignment)
        {
            if (root == null)
            {
                return;
            }

            List<Label> labels = root.Query<Label>(className: className).ToList();
            for (int i = 0; i < labels.Count; i++)
            {
                ApplyLabelStyle(labels[i], fontSize, color, fontStyle, alignment);
            }
        }

        private void ApplyLabelStyle(Label label, int fontSize, Color color, FontStyle fontStyle, TextAnchor alignment)
        {
            if (label == null)
            {
                return;
            }

            if (_runtimeFont != null)
            {
                label.style.unityFont = new StyleFont(_runtimeFont);
            }

            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = fontStyle;
            label.style.unityTextAlign = alignment;
            label.style.whiteSpace = WhiteSpace.Normal;
        }

        private void ApplyButtonStyle(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.focusable = false;
            if (_runtimeFont != null)
            {
                button.style.unityFont = new StyleFont(_runtimeFont);
            }

            button.style.fontSize = 14;
            button.style.color = new Color(0.96f, 0.97f, 0.98f);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.whiteSpace = WhiteSpace.NoWrap;
            button.style.minHeight = 34;
        }

        private void EnsureRuntimeFont()
        {
            if (_runtimeFont != null)
            {
                return;
            }

            _runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_runtimeFont == null)
            {
                _runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Helvetica", "Arial" }, 14);
            }
        }

        private static void ApplyDocumentLayout(VisualElement documentRoot)
        {
            documentRoot.style.width = Mathf.Max(1, Screen.width);
            documentRoot.style.height = Mathf.Max(1, Screen.height);
            documentRoot.style.flexGrow = 1;
        }

        private static void SetText(Label label, string text)
        {
            if (label != null && !string.Equals(label.text, text, StringComparison.Ordinal))
            {
                label.text = text;
            }
        }

        private static void SetText(Button button, string text)
        {
            if (button != null && !string.Equals(button.text, text, StringComparison.Ordinal))
            {
                button.text = text;
            }
        }

        private static PanelSettings CreateFallbackPanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "BreakoutRuntimePanelSettingsInstance";
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 100f;
            return settings;
        }

        private sealed class BreakoutRuntimeBridge : IDisposable
        {
            private const string RunnerTypeName = "MxFramework.Demo.Breakout.BreakoutRuntimeValidationRunner";
            private const string OptionsTypeName = "MxFramework.Demo.Breakout.BreakoutGameOptions";
            private const string CommandTypeName = "MxFramework.Demo.Breakout.BreakoutCommand";

            private object _runner;
            private Type _commandType;

            public bool IsReady => _runner != null;

            public string LastResultHash => GetPropertyValue(_runner, "LastResultHash", "0");

            public int ReplayFrameCount
            {
                get
                {
                    object module = GetProperty(_runner, "Module");
                    return ConvertToInt(GetProperty(module, "ReplayFrameCount"), 0);
                }
            }

            public bool TryCreateRunner(out string error)
            {
                error = string.Empty;
                Type runnerType = FindType(RunnerTypeName);
                if (runnerType == null)
                {
                    error = "Waiting for Worker A runtime: missing " + RunnerTypeName;
                    return false;
                }

                _commandType = FindType(CommandTypeName);
                if (_commandType == null || !_commandType.IsEnum)
                {
                    error = "Waiting for Worker A runtime: missing enum " + CommandTypeName;
                    return false;
                }

                try
                {
                    _runner = CreateRunner(runnerType);
                    if (_runner == null)
                    {
                        error = "Worker A runtime bridge could not construct BreakoutRuntimeValidationRunner.";
                        return false;
                    }

                    return true;
                }
                catch (Exception exception)
                {
                    error = "Worker A runtime bridge failed: " + exception.Message;
                    return false;
                }
            }

            public bool EnqueueCommand(long frame, string commandName, out string error)
            {
                error = string.Empty;
                if (_runner == null || _commandType == null)
                {
                    error = "Breakout runtime is not ready.";
                    return false;
                }

                object command;
                try
                {
                    command = Enum.Parse(_commandType, commandName);
                }
                catch (Exception)
                {
                    error = "Worker A runtime enum does not define BreakoutCommand." + commandName;
                    return false;
                }

                MethodInfo method = FindMethod(_runner.GetType(), "EnqueueCommand", typeof(long), _commandType);
                if (method == null)
                {
                    error = "Worker A runtime is missing EnqueueCommand(long, BreakoutCommand).";
                    return false;
                }

                method.Invoke(_runner, new[] { (object)frame, command });
                return true;
            }

            public bool TickFrame(long frame, out string error)
            {
                error = string.Empty;
                if (_runner == null)
                {
                    error = "Breakout runtime is not ready.";
                    return false;
                }

                MethodInfo method = FindMethod(_runner.GetType(), "TickFrame", typeof(long));
                if (method == null)
                {
                    error = "Worker A runtime is missing TickFrame(long).";
                    return false;
                }

                method.Invoke(_runner, new object[] { frame });
                return true;
            }

            public bool TryCaptureSnapshot(out BreakoutViewSnapshot snapshot, out string error)
            {
                snapshot = BreakoutViewSnapshot.CreateEmpty();
                error = string.Empty;
                object game = GetProperty(_runner, "Game");
                if (game == null)
                {
                    error = "Worker A runtime is missing Game property.";
                    return false;
                }

                MethodInfo capture = game.GetType().GetMethod("CaptureSnapshot", BindingFlags.Instance | BindingFlags.Public);
                if (capture == null)
                {
                    error = "Worker A runtime is missing Game.CaptureSnapshot().";
                    return false;
                }

                object rawSnapshot = capture.Invoke(game, null);
                if (rawSnapshot == null)
                {
                    error = "Worker A runtime returned a null Breakout snapshot.";
                    return false;
                }

                snapshot = BreakoutViewSnapshot.FromObject(rawSnapshot);
                return true;
            }

            public void Dispose()
            {
                IDisposable disposable = _runner as IDisposable;
                disposable?.Dispose();
                _runner = null;
            }

            private static object CreateRunner(Type runnerType)
            {
                ConstructorInfo parameterless = runnerType.GetConstructor(Type.EmptyTypes);
                if (parameterless != null)
                {
                    return parameterless.Invoke(null);
                }

                Type optionsType = FindType(OptionsTypeName);
                if (optionsType != null)
                {
                    ConstructorInfo optionsCtor = optionsType.GetConstructor(Type.EmptyTypes);
                    ConstructorInfo runnerCtor = runnerType.GetConstructor(new[] { optionsType });
                    if (optionsCtor != null && runnerCtor != null)
                    {
                        object options = optionsCtor.Invoke(null);
                        return runnerCtor.Invoke(new[] { options });
                    }
                }

                return null;
            }

            private static Type FindType(string fullName)
            {
                Type type = Type.GetType(fullName);
                if (type != null)
                {
                    return type;
                }

                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    type = assemblies[i].GetType(fullName);
                    if (type != null)
                    {
                        return type;
                    }
                }

                return null;
            }

            private static MethodInfo FindMethod(Type type, string name, params Type[] parameterTypes)
            {
                return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null);
            }
        }

        private sealed class BreakoutViewSnapshot
        {
            public float Width;
            public float Height;
            public float PaddleX;
            public float PaddleY;
            public float PaddleWidth;
            public float PaddleHeight;
            public float BallX;
            public float BallY;
            public float BallRadius;
            public int Score;
            public int Lives;
            public bool IsGameOver;
            public bool IsVictory;
            public int LevelIndex;
            public string LastEvent = string.Empty;
            public readonly List<BreakoutBrickView> Bricks = new List<BreakoutBrickView>();
            public readonly List<BreakoutBallView> Balls = new List<BreakoutBallView>();

            public static BreakoutViewSnapshot CreateEmpty()
            {
                var snapshot = new BreakoutViewSnapshot
                {
                    Width = 100f,
                    Height = 70f,
                    PaddleX = 38f,
                    PaddleY = 5f,
                    PaddleWidth = 24f,
                    PaddleHeight = 3f,
                    BallX = 50f,
                    BallY = 12f,
                    BallRadius = 1.6f,
                    Lives = 3
	                };
                snapshot.Balls.Add(new BreakoutBallView
                {
                    X = snapshot.BallX,
                    Y = snapshot.BallY,
                    Radius = snapshot.BallRadius
                });

                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        snapshot.Bricks.Add(new BreakoutBrickView
                        {
                            X = 7f + x * 11f,
                            Y = 48f + y * 5f,
                            Width = 9f,
                            Height = 3.5f,
                            HitPoints = y == 3 ? 2 : 1,
                            IsActive = true
                        });
                    }
                }

                return snapshot;
            }

            public static BreakoutViewSnapshot FromObject(object source)
            {
                var snapshot = CreateEmpty();
                snapshot.Width = ConvertToFloat(GetAnyProperty(source, "Width", "FieldWidth", "PlayfieldWidth"), snapshot.Width);
                snapshot.Height = ConvertToFloat(GetAnyProperty(source, "Height", "FieldHeight", "PlayfieldHeight"), snapshot.Height);
                snapshot.Score = ConvertToInt(GetAnyProperty(source, "Score"), 0);
                snapshot.Lives = ConvertToInt(GetAnyProperty(source, "Lives", "LifeCount"), snapshot.Lives);
                snapshot.IsGameOver = ConvertToBool(GetAnyProperty(source, "IsGameOver", "GameOver"), false);
                snapshot.IsVictory = ConvertToBool(GetAnyProperty(source, "IsVictory", "HasWon", "IsWin"), false);
                snapshot.PaddleX = ConvertToFloat(GetAnyProperty(source, "PaddleX"), snapshot.PaddleX);
                snapshot.PaddleY = ConvertToFloat(GetAnyProperty(source, "PaddleY"), snapshot.PaddleY);
                snapshot.PaddleWidth = ConvertToFloat(GetAnyProperty(source, "PaddleWidth"), snapshot.PaddleWidth);
                snapshot.PaddleHeight = ConvertToFloat(GetAnyProperty(source, "PaddleHeight"), snapshot.PaddleHeight);
                snapshot.BallX = ConvertToFloat(GetAnyProperty(source, "BallX"), snapshot.BallX);
                snapshot.BallY = ConvertToFloat(GetAnyProperty(source, "BallY"), snapshot.BallY);
                snapshot.BallRadius = ConvertToFloat(GetAnyProperty(source, "BallRadius"), snapshot.BallRadius);
                snapshot.LevelIndex = ConvertToInt(GetAnyProperty(source, "LevelIndex"), 0);
                object lastEvent = GetAnyProperty(source, "LastEvent");
                snapshot.LastEvent = lastEvent != null ? lastEvent.ToString() : string.Empty;

                object bricks = GetAnyProperty(source, "Bricks", "BrickSnapshots", "ActiveBricks");
                if (bricks is IEnumerable enumerable)
                {
                    snapshot.Bricks.Clear();
                    foreach (object brick in enumerable)
                    {
                        snapshot.Bricks.Add(BreakoutBrickView.FromObject(brick));
                    }
                }

                object balls = GetAnyProperty(source, "Balls", "BallSnapshots", "ActiveBalls");
                if (balls is IEnumerable ballEnumerable)
                {
                    snapshot.Balls.Clear();
                    foreach (object ball in ballEnumerable)
                    {
                        snapshot.Balls.Add(BreakoutBallView.FromObject(ball));
                    }
                }

                if (snapshot.Balls.Count == 0)
                {
                    snapshot.Balls.Add(new BreakoutBallView
                    {
                        X = snapshot.BallX,
                        Y = snapshot.BallY,
                        Radius = snapshot.BallRadius
                    });
                }

                return snapshot;
            }

            public string GetBrickLayoutSignature()
            {
                return Bricks.Count + ":" + Score + ":" + Lives + ":" + LevelIndex + ":" + Balls.Count + ":" + IsGameOver + ":" + IsVictory;
            }
        }

        private struct BreakoutBallView
        {
            public float X;
            public float Y;
            public float Radius;

            public static BreakoutBallView FromObject(object source)
            {
                return new BreakoutBallView
                {
                    X = ConvertToFloat(GetAnyProperty(source, "X"), 0f),
                    Y = ConvertToFloat(GetAnyProperty(source, "Y"), 0f),
                    Radius = ConvertToFloat(GetAnyProperty(source, "Radius", "BallRadius"), 1f)
                };
            }
        }

        private struct BreakoutBrickView
        {
            public float X;
            public float Y;
            public float Width;
            public float Height;
            public int HitPoints;
            public bool IsActive;
            public string Type;
            public string PowerUpType;

            public static BreakoutBrickView FromObject(object source)
            {
                bool destroyed = ConvertToBool(GetAnyProperty(source, "IsDestroyed", "Destroyed"), false);
                return new BreakoutBrickView
                {
                    X = ConvertToFloat(GetAnyProperty(source, "X"), 0f),
                    Y = ConvertToFloat(GetAnyProperty(source, "Y"), 0f),
                    Width = ConvertToFloat(GetAnyProperty(source, "Width"), 1f),
                    Height = ConvertToFloat(GetAnyProperty(source, "Height"), 1f),
                    HitPoints = ConvertToInt(GetAnyProperty(source, "HitPoints", "Health"), 1),
                    IsActive = ConvertToBool(GetAnyProperty(source, "IsActive", "Active"), !destroyed),
                    Type = GetPropertyValue(source, "Type", "normal").ToLowerInvariant(),
                    PowerUpType = GetPropertyValue(source, "PowerUpType", "none").ToLowerInvariant()
                };
            }
        }

        private static object GetAnyProperty(object source, params string[] names)
        {
            if (source == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                object value = GetProperty(source, names[i]);
                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        private static object GetProperty(object source, string name)
        {
            if (source == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            PropertyInfo property = source.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            return property != null ? property.GetValue(source, null) : null;
        }

        private static string GetPropertyValue(object source, string name, string fallback)
        {
            object value = GetProperty(source, name);
            return value != null ? value.ToString() : fallback;
        }

        private static float ConvertToFloat(object value, float fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToSingle(value);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static int ConvertToInt(object value, int fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static bool ConvertToBool(object value, bool fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToBoolean(value);
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
