using System;
using System.Collections.Generic;
using MxFramework.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.Tetris
{
    [AddComponentMenu("MxFramework/Demo/Tetris Playable Demo")]
    public sealed class TetrisPlayableDemo : MonoBehaviour
    {
        private const int DefaultCellSizePixels = 24;
        private const int MinCellSizePixels = 18;
        private const int MaxCellSizePixels = 56;
        private const int BorderTotalPixels = 2;
        private const int BoardPaddingPixels = 10;
        private const int RootHorizontalPaddingPixels = 28;
        private const int RootVerticalPaddingPixels = 24;
        private const int HeaderHeightPixels = 42;
        private const int HeaderMarginBottomPixels = 18;
        private const int SidePanelWidthPixels = 320;
        private const int SidePanelMinWidthPixels = 280;
        private const int SidePanelMaxWidthPixels = 360;
        private const int SidePanelMarginPixels = 24;
        private const int CompactSidePanelMarginPixels = 12;

        private static readonly string[] CellToneClasses =
        {
            "cell-empty",
            "cell-i",
            "cell-o",
            "cell-t",
            "cell-s",
            "cell-z",
            "cell-j",
            "cell-l"
        };

        [SerializeField] private UIDocument _document = null;
        [SerializeField] private VisualTreeAsset _visualTree = null;
        [SerializeField] private StyleSheet _styleSheet = null;
        [SerializeField] [Min(1)] private int _gravityIntervalFrames = 60;
        [SerializeField] [Min(1f)] private float _simulationFramesPerSecond = 60f;
        [SerializeField] [Min(1)] private int _maxSimulationFramesPerUpdate = 4;
        [SerializeField] [Min(1)] private int _targetFrameRate = 60;
        [SerializeField] private bool _startPaused = false;

        private readonly List<VisualElement> _cells = new List<VisualElement>(200);
        private TetrisRuntimeValidationRunner _runner;
        private IInputProvider _input;
        private long _frame;
        private float _simulationFrameAccumulator;
        private bool _isPaused;
        private Vector2 _previousMoveInput;
        private int _previousTargetFrameRate;
        private bool _hasPreviousTargetFrameRate;
        private int _boardWidth;
        private int _boardHeight;
        private int _cellSizePixels = DefaultCellSizePixels;
        private int _appliedCellSizePixels;
        private string _lastBoardCode = string.Empty;
        private Font _runtimeFont;

        private VisualElement _root;
        private VisualElement _shell;
        private VisualElement _boardWrap;
        private VisualElement _board;
        private VisualElement _sidePanel;
        private Label _frameLabel;
        private Label _scoreLabel;
        private Label _linesLabel;
        private Label _lockedLabel;
        private Label _hashLabel;
        private Label _replayLabel;
        private Label _gravityLabel;
        private Label _stateLabel;
        private Button _pauseButton;
        private Button _resetButton;
        private Button _hardDropButton;

        private void OnEnable()
        {
            ResolveInput();
            ApplyTargetFrameRate();
            ResetGame();
            EnsureDocument();
            RefreshUi(force: true);
        }

        private void OnDisable()
        {
            UnregisterButtonCallbacks();
            DisposeRunner();
            RestoreTargetFrameRate();
            _cells.Clear();
            _root = null;
            _board = null;
        }

        private void Update()
        {
            if (_runner == null)
                ResetGame();

            EnsureDocument();
            ResolveInput();
            InputSnapshot input = _input != null ? _input.Snapshot : InputSnapshot.Empty;

            if (input.RestartPressed)
            {
                ResetGame();
                RefreshUi(force: true);
                return;
            }

            if (input.PausePressed || input.DebugSecondaryPressed)
                TogglePause();

            EnqueueInputCommands(input);
            if (!_isPaused && !_runner.Game.IsGameOver)
                TickSimulation();

            RefreshUi(force: false);
        }

        private void EnsureDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_visualTree != null && _document.visualTreeAsset != _visualTree)
                _document.visualTreeAsset = _visualTree;

            if (_document.panelSettings == null)
                _document.panelSettings = CreateFallbackPanelSettings();

            VisualElement root = _document.rootVisualElement;
            if (root == null)
                return;

            if (_styleSheet != null && !root.styleSheets.Contains(_styleSheet))
                root.styleSheets.Add(_styleSheet);

            ApplyDocumentLayout(root);
            VisualElement nextRoot = root.Q<VisualElement>("tetris-root");
            if (_root == nextRoot && _board != null)
            {
                ApplyRuntimeLayout();
                return;
            }

            UnregisterButtonCallbacks();
            CacheElements(root);
            ApplyRuntimeTextStyles();
            RegisterButtonCallbacks();
            BuildBoardIfNeeded(force: true);
            ApplyRuntimeLayout();
        }

        private void CacheElements(VisualElement root)
        {
            _root = root.Q<VisualElement>("tetris-root");
            _shell = root.Q<VisualElement>("tetris-shell");
            _boardWrap = root.Q<VisualElement>("board-wrap");
            _board = root.Q<VisualElement>("tetris-board");
            _sidePanel = root.Q<VisualElement>("side-panel");
            _frameLabel = root.Q<Label>("frame-label");
            _scoreLabel = root.Q<Label>("score-label");
            _linesLabel = root.Q<Label>("lines-label");
            _lockedLabel = root.Q<Label>("locked-label");
            _hashLabel = root.Q<Label>("hash-label");
            _replayLabel = root.Q<Label>("replay-label");
            _gravityLabel = root.Q<Label>("gravity-label");
            _stateLabel = root.Q<Label>("state-label");
            _pauseButton = root.Q<Button>("pause-button");
            _resetButton = root.Q<Button>("reset-button");
            _hardDropButton = root.Q<Button>("hard-drop-button");
        }

        private void ApplyRuntimeTextStyles()
        {
            EnsureRuntimeFont();

            ApplyLabelStyle(rootLabel: _root?.Q<Label>(className: "tetris-title"), 26, new Color(0.96f, 0.97f, 0.98f), FontStyle.Bold, TextAnchor.MiddleLeft);
            ApplyLabelStyle(_stateLabel, 14, new Color(0.88f, 1f, 0.94f), FontStyle.Bold, TextAnchor.MiddleCenter);
            ApplyLabelStyle(_frameLabel, 15, new Color(0.93f, 0.95f, 0.97f), FontStyle.Bold, TextAnchor.MiddleRight);
            ApplyLabelStyle(_scoreLabel, 15, new Color(0.93f, 0.95f, 0.97f), FontStyle.Bold, TextAnchor.MiddleRight);
            ApplyLabelStyle(_linesLabel, 15, new Color(0.93f, 0.95f, 0.97f), FontStyle.Bold, TextAnchor.MiddleRight);
            ApplyLabelStyle(_lockedLabel, 15, new Color(0.93f, 0.95f, 0.97f), FontStyle.Bold, TextAnchor.MiddleRight);
            ApplyLabelStyle(_replayLabel, 15, new Color(0.93f, 0.95f, 0.97f), FontStyle.Bold, TextAnchor.MiddleRight);
            ApplyLabelStyle(_gravityLabel, 15, new Color(0.93f, 0.95f, 0.97f), FontStyle.Bold, TextAnchor.MiddleRight);
            ApplyLabelStyle(_hashLabel, 12, new Color(0.92f, 0.93f, 0.95f), FontStyle.Normal, TextAnchor.UpperLeft);

            ApplyLabelsByClass(_root, "stat-name", 13, new Color(0.70f, 0.75f, 0.82f), FontStyle.Normal, TextAnchor.MiddleLeft);
            ApplyLabelsByClass(_root, "hash-title", 12, new Color(0.70f, 0.75f, 0.82f), FontStyle.Normal, TextAnchor.MiddleLeft);
            ApplyButtonStyle(_pauseButton);
            ApplyButtonStyle(_resetButton);
            ApplyButtonStyle(_hardDropButton);
        }

        private void ApplyLabelsByClass(VisualElement root, string className, int fontSize, Color color, FontStyle fontStyle, TextAnchor alignment)
        {
            if (root == null)
                return;

            var labels = root.Query<Label>(className: className).ToList();
            for (int i = 0; i < labels.Count; i++)
                ApplyLabelStyle(labels[i], fontSize, color, fontStyle, alignment);
        }

        private void ApplyLabelStyle(Label rootLabel, int fontSize, Color color, FontStyle fontStyle, TextAnchor alignment)
        {
            if (rootLabel == null)
                return;

            if (_runtimeFont != null)
                rootLabel.style.unityFont = new StyleFont(_runtimeFont);

            rootLabel.style.fontSize = fontSize;
            rootLabel.style.color = color;
            rootLabel.style.unityFontStyleAndWeight = fontStyle;
            rootLabel.style.unityTextAlign = alignment;
            rootLabel.style.whiteSpace = WhiteSpace.Normal;
            rootLabel.style.minHeight = Mathf.Max(18, fontSize + 8);
        }

        private void ApplyButtonStyle(Button button)
        {
            if (button == null)
                return;

            button.focusable = false;
            if (_runtimeFont != null)
                button.style.unityFont = new StyleFont(_runtimeFont);

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
                return;

            _runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_runtimeFont == null)
                _runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Helvetica", "Arial" }, 14);
        }

        private void RegisterButtonCallbacks()
        {
            _pauseButton?.RegisterCallback<ClickEvent>(OnPauseClicked);
            _resetButton?.RegisterCallback<ClickEvent>(OnResetClicked);
            _hardDropButton?.RegisterCallback<ClickEvent>(OnHardDropClicked);
        }

        private void UnregisterButtonCallbacks()
        {
            _pauseButton?.UnregisterCallback<ClickEvent>(OnPauseClicked);
            _resetButton?.UnregisterCallback<ClickEvent>(OnResetClicked);
            _hardDropButton?.UnregisterCallback<ClickEvent>(OnHardDropClicked);
        }

        private void OnPauseClicked(ClickEvent evt)
        {
            TogglePause();
            RefreshUi(force: true);
        }

        private void OnResetClicked(ClickEvent evt)
        {
            ResetGame();
            RefreshUi(force: true);
        }

        private void OnHardDropClicked(ClickEvent evt)
        {
            QueueCommand(TetrisCommand.HardDrop);
            if (_isPaused && !_runner.Game.IsGameOver)
            {
                _runner.TickFrame(_frame);
                _frame++;
            }

            RefreshUi(force: true);
        }

        private void EnqueueInputCommands(InputSnapshot input)
        {
            if (_runner == null || _runner.Game.IsGameOver)
                return;

            Vector2 move = input.Move;
            if (PressedNegative(move.x, _previousMoveInput.x))
                QueueCommand(TetrisCommand.MoveLeft);

            if (PressedPositive(move.x, _previousMoveInput.x))
                QueueCommand(TetrisCommand.MoveRight);

            if (PressedPositive(move.y, _previousMoveInput.y) || input.DebugCyclePressed)
                QueueCommand(TetrisCommand.RotateClockwise);

            if (PressedNegative(move.y, _previousMoveInput.y))
                QueueCommand(TetrisCommand.SoftDrop);

            if (input.JumpPressed)
                QueueCommand(TetrisCommand.HardDrop);

            _previousMoveInput = move;
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

        private static bool PressedNegative(float current, float previous)
        {
            return current < -0.5f && previous >= -0.5f;
        }

        private void QueueCommand(TetrisCommand command)
        {
            _runner?.EnqueueCommand(_frame, command);
        }

        private void TickSimulation()
        {
            float simulationFramesPerSecond = Mathf.Max(1f, _simulationFramesPerSecond);
            _simulationFrameAccumulator += Time.deltaTime * simulationFramesPerSecond;

            int steps = 0;
            int maxSteps = Math.Max(1, _maxSimulationFramesPerUpdate);
            while (_simulationFrameAccumulator >= 1f && steps < maxSteps)
            {
                _runner.TickFrame(_frame);
                _frame++;
                _simulationFrameAccumulator -= 1f;
                steps++;
            }

            if (steps == maxSteps && _simulationFrameAccumulator >= 1f)
                _simulationFrameAccumulator = 0f;
        }

        private void ResetGame()
        {
            DisposeRunner();
            _runner = new TetrisRuntimeValidationRunner(new TetrisGameOptions(gravityIntervalFrames: Math.Max(1, _gravityIntervalFrames)));
            _frame = 0;
            _simulationFrameAccumulator = 0f;
            _isPaused = _startPaused;
            _lastBoardCode = string.Empty;
        }

        private void TogglePause()
        {
            _isPaused = !_isPaused;
        }

        private void RefreshUi(bool force)
        {
            if (_runner == null)
                return;

            TetrisGameSnapshot snapshot = _runner.Game.CaptureSnapshot();
            BuildBoardIfNeeded(force || snapshot.Width != _boardWidth || snapshot.Height != _boardHeight);
            ApplyRuntimeLayout();

            string boardCode = snapshot.BoardWithActiveCode.Replace("/", string.Empty);
            if (force || !string.Equals(boardCode, _lastBoardCode, StringComparison.Ordinal))
            {
                RefreshBoardCells(boardCode);
                _lastBoardCode = boardCode;
            }

            SetText(_frameLabel, "Frame " + _frame);
            SetText(_scoreLabel, snapshot.Score.ToString());
            SetText(_linesLabel, snapshot.LinesCleared.ToString());
            SetText(_lockedLabel, snapshot.LockedPieces.ToString());
            SetText(_hashLabel, _runner.LastResultHash.ToString());
            SetText(_replayLabel, _runner.Module.ReplayFrameCount.ToString());
            SetText(_gravityLabel, SecondsPerGravityStep().ToString("0.00") + "s / row");

            string state = snapshot.IsGameOver ? "Game Over" : (_isPaused ? "Paused" : "Running");
            SetText(_stateLabel, state);
            SetText(_pauseButton, _isPaused ? "Resume" : "Pause");

            _root?.EnableInClassList("is-paused", _isPaused);
            _root?.EnableInClassList("is-game-over", snapshot.IsGameOver);
        }

        private void BuildBoardIfNeeded(bool force)
        {
            if (_runner == null || _board == null)
                return;

            TetrisGameSnapshot snapshot = _runner.Game.CaptureSnapshot();
            if (!force && _cells.Count == snapshot.Width * snapshot.Height)
                return;

            _boardWidth = snapshot.Width;
            _boardHeight = snapshot.Height;
            _cells.Clear();
            _board.Clear();
            _appliedCellSizePixels = 0;
            _board.style.width = snapshot.Width * _cellSizePixels;
            _board.style.height = snapshot.Height * _cellSizePixels;

            int count = snapshot.Width * snapshot.Height;
            for (int i = 0; i < count; i++)
            {
                var cell = new VisualElement();
                cell.AddToClassList("tetris-cell");
                cell.AddToClassList(CellToneClasses[0]);
                _cells.Add(cell);
                _board.Add(cell);
            }

            ApplyCellSizesIfNeeded();
        }

        private void RefreshBoardCells(string boardCode)
        {
            int count = Math.Min(_cells.Count, boardCode.Length);
            for (int i = 0; i < count; i++)
                SetCellTone(_cells[i], boardCode[i]);
        }

        private static void SetCellTone(VisualElement cell, char value)
        {
            int index = value >= '1' && value <= '7' ? value - '0' : 0;
            for (int i = 0; i < CellToneClasses.Length; i++)
                cell.RemoveFromClassList(CellToneClasses[i]);

            cell.AddToClassList(CellToneClasses[index]);
        }

        private void DisposeRunner()
        {
            if (_runner != null)
            {
                _runner.Dispose();
                _runner = null;
            }
        }

        private void ApplyTargetFrameRate()
        {
            if (!_hasPreviousTargetFrameRate)
            {
                _previousTargetFrameRate = Application.targetFrameRate;
                _hasPreviousTargetFrameRate = true;
            }

            Application.targetFrameRate = Math.Max(1, _targetFrameRate);
        }

        private void RestoreTargetFrameRate()
        {
            if (!_hasPreviousTargetFrameRate)
                return;

            Application.targetFrameRate = _previousTargetFrameRate;
            _hasPreviousTargetFrameRate = false;
        }

        private float SecondsPerGravityStep()
        {
            return Math.Max(1, _gravityIntervalFrames) / Mathf.Max(1f, _simulationFramesPerSecond);
        }

        private static void ApplyDocumentLayout(VisualElement documentRoot)
        {
            documentRoot.style.width = Mathf.Max(1, Screen.width);
            documentRoot.style.height = Mathf.Max(1, Screen.height);
            documentRoot.style.flexGrow = 1;
        }

        private void ApplyRuntimeLayout()
        {
            if (_root == null)
                return;

            float width = Mathf.Max(1, Screen.width);
            float height = Mathf.Max(1, Screen.height);
            _cellSizePixels = ComputeCellSizePixels(width, height);

            float boardWidthPixels = _boardWidth * _cellSizePixels;
            float boardHeightPixels = _boardHeight * _cellSizePixels;
            float boardOuterWidthPixels = boardWidthPixels + BorderTotalPixels;
            float boardOuterHeightPixels = boardHeightPixels + BorderTotalPixels;
            float contentHeight = Mathf.Max(
                BoardPaddingPixels * 2 + boardOuterHeightPixels + BorderTotalPixels,
                height - RootVerticalPaddingPixels * 2 - HeaderHeightPixels - HeaderMarginBottomPixels);
            float boardPanelWidth = BoardPaddingPixels * 2 + boardOuterWidthPixels + BorderTotalPixels;
            float boardPanelHeight = BoardPaddingPixels * 2 + boardOuterHeightPixels + BorderTotalPixels;

            _root.style.width = width;
            _root.style.height = height;
            if (_shell != null)
                _shell.style.height = contentHeight;
            if (_boardWrap != null)
            {
                _boardWrap.style.width = boardPanelWidth;
                _boardWrap.style.height = boardPanelHeight;
            }

            if (_board != null)
            {
                _board.style.position = Position.Relative;
                _board.style.overflow = Overflow.Hidden;
                _board.style.width = boardOuterWidthPixels;
                _board.style.height = boardOuterHeightPixels;
            }

            ApplyCellSizesIfNeeded();

            if (_sidePanel == null)
                return;

            float sidePanelWidth = ComputeSidePanelWidth(width, boardPanelWidth);
            float sidePanelMargin = width < boardPanelWidth + sidePanelWidth + SidePanelMarginPixels + RootHorizontalPaddingPixels * 2
                ? CompactSidePanelMarginPixels
                : SidePanelMarginPixels;

            _sidePanel.style.width = sidePanelWidth;
            _sidePanel.style.height = boardPanelHeight;
            _sidePanel.style.marginLeft = sidePanelMargin;
        }

        private int ComputeCellSizePixels(float screenWidth, float screenHeight)
        {
            int boardWidth = Math.Max(1, _boardWidth);
            int boardHeight = Math.Max(1, _boardHeight);

            float availableHeight = Mathf.Max(
                DefaultCellSizePixels * boardHeight,
                screenHeight - RootVerticalPaddingPixels * 2 - HeaderHeightPixels - HeaderMarginBottomPixels - BoardPaddingPixels * 2 - BorderTotalPixels * 2);
            float availableWidth = Mathf.Max(
                DefaultCellSizePixels * boardWidth,
                screenWidth - RootHorizontalPaddingPixels * 2 - SidePanelMinWidthPixels - SidePanelMarginPixels - BoardPaddingPixels * 2 - BorderTotalPixels * 2);

            int maxByHeight = Mathf.FloorToInt(availableHeight / boardHeight);
            int maxByWidth = Mathf.FloorToInt(availableWidth / boardWidth);
            int fittingSize = Math.Min(maxByHeight, maxByWidth);
            return Mathf.Clamp(fittingSize, MinCellSizePixels, MaxCellSizePixels);
        }

        private float ComputeSidePanelWidth(float screenWidth, float boardPanelWidth)
        {
            float availableWidth = Mathf.Max(1, screenWidth - RootHorizontalPaddingPixels * 2);
            float maxAvailableWidth = availableWidth - boardPanelWidth - CompactSidePanelMarginPixels;
            float preferredWidth = Mathf.Clamp(screenWidth * 0.25f, SidePanelWidthPixels, SidePanelMaxWidthPixels);
            return Mathf.Clamp(Mathf.Min(preferredWidth, maxAvailableWidth), SidePanelMinWidthPixels, SidePanelMaxWidthPixels);
        }

        private void ApplyCellSizesIfNeeded()
        {
            if (_board == null || _appliedCellSizePixels == _cellSizePixels)
                return;

            int boardWidth = Math.Max(1, _boardWidth);
            for (int i = 0; i < _cells.Count; i++)
            {
                VisualElement cell = _cells[i];
                int x = i % boardWidth;
                int y = i / boardWidth;
                cell.style.position = Position.Absolute;
                cell.style.left = x * _cellSizePixels;
                cell.style.top = y * _cellSizePixels;
                cell.style.width = _cellSizePixels;
                cell.style.height = _cellSizePixels;
            }

            _appliedCellSizePixels = _cellSizePixels;
        }

        private static void SetText(Label label, string text)
        {
            if (label != null && !string.Equals(label.text, text, StringComparison.Ordinal))
                label.text = text;
        }

        private static void SetText(Button button, string text)
        {
            if (button != null && !string.Equals(button.text, text, StringComparison.Ordinal))
                button.text = text;
        }

        private static PanelSettings CreateFallbackPanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "TetrisRuntimePanelSettingsInstance";
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 100f;
            return settings;
        }
    }
}
