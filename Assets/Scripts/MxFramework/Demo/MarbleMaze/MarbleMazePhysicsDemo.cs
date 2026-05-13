using MxFramework.Input;
using MxFramework.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.MarbleMaze
{
    [AddComponentMenu("MxFramework/Demo/Marble Maze Physics Demo")]
    public sealed class MarbleMazePhysicsDemo : MonoBehaviour
    {
        [SerializeField] private UIDocument _document = null;
        [SerializeField] private VisualTreeAsset _visualTree = null;
        [SerializeField] private StyleSheet _styleSheet = null;
        [SerializeField] private Transform _ball = null;
        [SerializeField] private Transform _board = null;
        [SerializeField] [Min(1)] private int _checkpointCount = 2;
        [SerializeField] [Min(0.1f)] private float _targetTimeSeconds = 45f;
        [SerializeField] [Range(1f, 30f)] private float _maxTiltDegrees = 7f;
        [SerializeField] [Min(0.1f)] private float _tiltResponse = 4.5f;
        [SerializeField] [Min(0f)] private float _tiltAcceleration = 7.5f;
        [SerializeField] [Min(1f)] private float _simulationFramesPerSecond = 60f;
        [SerializeField] [Min(1)] private int _maxRuntimeFramesPerUpdate = 4;
        [SerializeField] [Min(1)] private int _targetFrameRate = 60;
        [SerializeField] private Vector3 _ballSpawnPosition = new Vector3(0f, 0.65f, -4f);
        [SerializeField] private Vector3 _checkpoint0Position = new Vector3(-2.7f, 0.55f, -1.5f);
        [SerializeField] private Vector3 _checkpoint1Position = new Vector3(2.4f, 0.55f, 0.7f);
        [SerializeField] private Vector3 _exitPosition = new Vector3(0f, 0.55f, 3.15f);

        private MarbleMazeRuntimeRunner _runner;
        private MarbleMazeFrameworkPhysicsWorld _physicsWorld;
        private IInputProvider _input;
        private long _frame;
        private float _runtimeAccumulator;
        private Vector2 _previousMove;
        private int _previousTargetFrameRate;
        private bool _hasPreviousTargetFrameRate;
        private string _lastSaveJson = string.Empty;
        private Font _runtimeFont;
        private Quaternion _targetBoardRotation = Quaternion.identity;

        private VisualElement _root;
        private Label _stateLabel;
        private Label _timeLabel;
        private Label _checkpointLabel;
        private Label _hashLabel;
        private Label _eventLabel;
        private Label _saveLabel;
        private Button _pauseButton;
        private Button _resetButton;
        private Button _saveButton;
        private Button _loadButton;

        public MarbleMazeSnapshot Snapshot => _runner != null
            ? _runner.Game.CaptureSnapshot()
            : new MarbleMazeGame(new MarbleMazeOptions(_checkpointCount, _targetTimeSeconds)).CaptureSnapshot();

        public event System.Action ResetRequested;

        private void OnEnable()
        {
            ApplyTargetFrameRate();
            ResolveInput();
            EnsureViewObjects();
            ResetRuntime(resetBall: true);
            EnsureDocument();
            RefreshUi();
        }

        private void OnDisable()
        {
            UnregisterButtonCallbacks();
            if (_runner != null)
            {
                _runner.Dispose();
                _runner = null;
            }

            RestoreTargetFrameRate();
        }

        private void Update()
        {
            ResolveInput();
            EnsureDocument();

            InputSnapshot input = _input != null ? _input.Snapshot : InputSnapshot.Empty;
            if (input.RestartPressed)
                EnqueueReset();

            if (input.PausePressed || input.DebugSecondaryPressed)
            {
                MarbleMazeSnapshot snapshot = Snapshot;
                EnqueuePause(!snapshot.IsPaused);
            }

            EnqueueTiltFromInput(input.Move);
            AdvanceRuntime();
            UpdateBoardTiltTarget(Snapshot);
            ApplyBoardTilt(Time.deltaTime);
            _previousMove = input.Move;
            RefreshUi();
        }

        private void ResetRuntime(bool resetBall)
        {
            if (_runner != null)
                _runner.Dispose();

            _runner = new MarbleMazeRuntimeRunner(new MarbleMazeOptions(_checkpointCount, _targetTimeSeconds));
            _physicsWorld = CreatePhysicsWorld();
            _frame = 0;
            _runtimeAccumulator = 0f;
            if (resetBall)
                ResetBall();
        }

        private MarbleMazeFrameworkPhysicsWorld CreatePhysicsWorld()
        {
            var checkpoints = new[]
            {
                ToRuntimeVector(_checkpoint0Position),
                ToRuntimeVector(_checkpoint1Position)
            };
            return new MarbleMazeFrameworkPhysicsWorld(
                checkpoints,
                ToRuntimeVector(_exitPosition),
                acceleration: _tiltAcceleration);
        }

        private void AdvanceRuntime()
        {
            if (_runner == null || _physicsWorld == null)
                ResetRuntime(resetBall: true);

            float step = 1f / Mathf.Max(1f, _simulationFramesPerSecond);
            _runtimeAccumulator += Time.deltaTime;
            int steps = 0;
            while (_runtimeAccumulator >= step && steps < _maxRuntimeFramesPerUpdate)
            {
                long currentFrame = _frame;
                _runner.TickFrame(currentFrame, step);
                _frame++;
                StepFrameworkPhysics(step);
                _runtimeAccumulator -= step;
                steps++;
            }
        }

        private void StepFrameworkPhysics(float step)
        {
            MarbleMazeSnapshot snapshot = Snapshot;
            if (snapshot.IsPaused || snapshot.IsFinished)
                return;

            MarbleMazePhysicsStepResult result = _physicsWorld.Step(
                step,
                snapshot.TiltX,
                snapshot.TiltZ,
                snapshot.NextCheckpointIndex);

            EnqueuePhysicsSample(result.Position, result.Velocity);
            if (result.CheckpointHit >= 0)
                _runner.Module.EnqueueCommand(new RuntimeFrame(_frame), MarbleMazeCommand.Checkpoint, targetId: result.CheckpointHit, sourceId: 3);
            if (result.ExitHit)
                _runner.Module.EnqueueCommand(new RuntimeFrame(_frame), MarbleMazeCommand.Finish, sourceId: 3);

            SyncBallView(result.Position);
        }

        private void EnqueuePhysicsSample(MarbleMazeVector3 position, MarbleMazeVector3 velocity)
        {
            _runner.Module.EnqueueCommand(
                new RuntimeFrame(_frame),
                MarbleMazeCommand.PhysicsSample,
                payload0: MarbleMazeGame.Encode(position.X),
                payload1: MarbleMazeGame.Encode(position.Y),
                payload2: MarbleMazeGame.Encode(position.Z),
                sourceId: 2);
            _runner.Game.ApplyVelocitySample(velocity.X, velocity.Y, velocity.Z);
        }

        private void EnqueueTiltFromInput(Vector2 move)
        {
            if (_runner == null)
                return;

            if ((move - _previousMove).sqrMagnitude < 0.0001f)
                return;

            _runner.Module.EnqueueCommand(
                new RuntimeFrame(_frame),
                MarbleMazeCommand.Tilt,
                payload0: MarbleMazeGame.Encode(Mathf.Clamp(move.x, -1f, 1f)),
                payload1: MarbleMazeGame.Encode(Mathf.Clamp(move.y, -1f, 1f)));
        }

        private void EnqueueReset()
        {
            if (_runner == null)
                return;

            _runner.Module.EnqueueCommand(new RuntimeFrame(_frame), MarbleMazeCommand.Reset);
            ResetBall();
            ResetRequested?.Invoke();
        }

        private void EnqueuePause(bool paused)
        {
            if (_runner == null)
                return;

            _runner.Module.EnqueueCommand(new RuntimeFrame(_frame), MarbleMazeCommand.Pause, payload0: paused ? 1 : 0);
        }

        private void UpdateBoardTiltTarget(MarbleMazeSnapshot snapshot)
        {
            if (_board == null)
                return;

            _targetBoardRotation = Quaternion.Euler(
                (float)(snapshot.TiltZ * _maxTiltDegrees),
                0f,
                (float)(-snapshot.TiltX * _maxTiltDegrees));
        }

        private void ApplyBoardTilt(float deltaTime)
        {
            if (_board == null)
                return;

            float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, _tiltResponse) * deltaTime);
            _board.localRotation = Quaternion.Slerp(_board.localRotation, _targetBoardRotation, t);
        }

        private void ResetBall()
        {
            if (_physicsWorld == null)
                _physicsWorld = CreatePhysicsWorld();

            MarbleMazeVector3 spawn = ToRuntimeVector(_ballSpawnPosition);
            _physicsWorld.Reset(spawn);
            SyncBallView(spawn);
        }

        private void SyncBallView(MarbleMazeVector3 position)
        {
            if (_ball == null)
                return;

            _ball.localPosition = new Vector3((float)position.X, (float)position.Y, (float)position.Z);
        }

        private void EnsureViewObjects()
        {
            if (_board == null)
            {
                GameObject boardObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boardObject.name = "MarbleMazeRuntimeBoard";
                boardObject.transform.SetParent(transform, false);
                boardObject.transform.localScale = new Vector3(9f, 0.25f, 9f);
                boardObject.transform.localPosition = Vector3.zero;
                DestroyColliders(boardObject);
                _board = boardObject.transform;
            }

            if (_ball == null)
            {
                GameObject ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ballObject.name = "MarbleMazeRuntimeBall";
                ballObject.transform.SetParent(transform, false);
                ballObject.transform.localScale = Vector3.one * 0.65f;
                DestroyColliders(ballObject);
                _ball = ballObject.transform;
            }
        }

        private void EnsureDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_visualTree != null && _document.visualTreeAsset != _visualTree)
                _document.visualTreeAsset = _visualTree;

            if (_document.panelSettings == null)
                _document.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
                return;

            if (_styleSheet != null && !documentRoot.styleSheets.Contains(_styleSheet))
                documentRoot.styleSheets.Add(_styleSheet);

            VisualElement nextRoot = documentRoot.Q<VisualElement>("marble-root");
            if (_root == nextRoot)
                return;

            UnregisterButtonCallbacks();
            _root = nextRoot;
            _stateLabel = documentRoot.Q<Label>("state-label");
            _timeLabel = documentRoot.Q<Label>("time-label");
            _checkpointLabel = documentRoot.Q<Label>("checkpoint-label");
            _hashLabel = documentRoot.Q<Label>("hash-label");
            _eventLabel = documentRoot.Q<Label>("event-label");
            _saveLabel = documentRoot.Q<Label>("save-label");
            _pauseButton = documentRoot.Q<Button>("pause-button");
            _resetButton = documentRoot.Q<Button>("reset-button");
            _saveButton = documentRoot.Q<Button>("save-button");
            _loadButton = documentRoot.Q<Button>("load-button");
            ApplyRuntimeTextStyles(documentRoot);
            RegisterButtonCallbacks();
        }

        private void RegisterButtonCallbacks()
        {
            _pauseButton?.RegisterCallback<ClickEvent>(OnPauseClicked);
            _resetButton?.RegisterCallback<ClickEvent>(OnResetClicked);
            _saveButton?.RegisterCallback<ClickEvent>(OnSaveClicked);
            _loadButton?.RegisterCallback<ClickEvent>(OnLoadClicked);
        }

        private void UnregisterButtonCallbacks()
        {
            _pauseButton?.UnregisterCallback<ClickEvent>(OnPauseClicked);
            _resetButton?.UnregisterCallback<ClickEvent>(OnResetClicked);
            _saveButton?.UnregisterCallback<ClickEvent>(OnSaveClicked);
            _loadButton?.UnregisterCallback<ClickEvent>(OnLoadClicked);
        }

        private void OnPauseClicked(ClickEvent evt)
        {
            EnqueuePause(!Snapshot.IsPaused);
        }

        private void OnResetClicked(ClickEvent evt)
        {
            EnqueueReset();
        }

        private void OnSaveClicked(ClickEvent evt)
        {
            if (_runner == null)
                return;

            RuntimeSaveStateResult<RuntimeSaveState> save = _runner.CaptureSaveState();
            if (save.Success)
            {
                _lastSaveJson = RuntimeSaveStateJson.SaveToJson(save.Value);
                SetText(_saveLabel, "Saved JSON bytes: " + _lastSaveJson.Length);
            }
            else
            {
                SetText(_saveLabel, save.Error.ToString());
            }
        }

        private void OnLoadClicked(ClickEvent evt)
        {
            if (_runner == null || string.IsNullOrEmpty(_lastSaveJson))
                return;

            RuntimeSaveStateResult<RuntimeSaveState> load = RuntimeSaveStateJson.LoadFromJson(_lastSaveJson);
            if (load.Success)
            {
                RuntimeSaveStateResult<bool> restore = _runner.RestoreSaveState(load.Value);
                if (restore.Success)
                {
                    MarbleMazeSnapshot snapshot = Snapshot;
                    _physicsWorld.Reset(snapshot.BallPosition, snapshot.BallVelocity);
                    SyncBallView(snapshot.BallPosition);
                }

                SetText(_saveLabel, restore.Success ? "Loaded saved run" : restore.Error.ToString());
            }
            else
            {
                SetText(_saveLabel, load.Error.ToString());
            }
        }

        private void RefreshUi()
        {
            if (_runner == null)
                return;

            MarbleMazeSnapshot snapshot = _runner.Game.CaptureSnapshot();
            SetText(_stateLabel, snapshot.IsFinished ? "Finished" : snapshot.IsPaused ? "Paused" : "Running");
            SetText(_timeLabel, snapshot.ElapsedSeconds.ToString("0.00") + "s");
            SetText(_checkpointLabel, snapshot.CheckpointsCleared + " / " + snapshot.CheckpointCount);
            SetText(_hashLabel, _runner.LastResultHash.ToString());
            SetText(_eventLabel, snapshot.LastEvent);
            if (_pauseButton != null)
                _pauseButton.text = snapshot.IsPaused ? "Resume" : "Pause";
        }

        private void ResolveInput()
        {
            if (_input != null)
                return;

            _input = InputProviderResolver.ResolveOrCreateDefault(this, GetComponent<InputService>());
        }

        private void ApplyRuntimeTextStyles(VisualElement root)
        {
            if (_runtimeFont == null)
                _runtimeFont = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var labels = root.Query<Label>().ToList();
            for (int i = 0; i < labels.Count; i++)
            {
                labels[i].style.color = new Color(0.93f, 0.95f, 0.97f);
                labels[i].style.fontSize = labels[i].ClassListContains("title") ? 24 : 14;
                labels[i].style.whiteSpace = WhiteSpace.Normal;
                if (_runtimeFont != null)
                    labels[i].style.unityFont = new StyleFont(_runtimeFont);
            }
        }

        private static MarbleMazeVector3 ToRuntimeVector(Vector3 value)
        {
            return new MarbleMazeVector3(value.x, value.y, value.z);
        }

        private static void DestroyColliders(GameObject gameObject)
        {
            Collider[] colliders = gameObject.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
                Destroy(colliders[i]);
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
                label.text = text ?? string.Empty;
        }

        private void ApplyTargetFrameRate()
        {
            if (!_hasPreviousTargetFrameRate)
            {
                _previousTargetFrameRate = Application.targetFrameRate;
                _hasPreviousTargetFrameRate = true;
            }

            Application.targetFrameRate = Mathf.Max(1, _targetFrameRate);
        }

        private void RestoreTargetFrameRate()
        {
            if (_hasPreviousTargetFrameRate)
            {
                Application.targetFrameRate = _previousTargetFrameRate;
                _hasPreviousTargetFrameRate = false;
            }
        }
    }
}
