using System;
using MxFramework.Core.Math;
using MxFramework.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.GameplayTriggers
{
    /// <summary>Single Unity composition root for the Gameplay Trigger validation playable.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Demo/Gameplay Trigger Playable Showcase Root")]
    public sealed class GameplayTriggerPlayableShowcaseRoot : MonoBehaviour
    {
        private const int MaxLogicalTicksPerUpdate = 8;
        private const int MovePulseCapacity = 8;
        private const double HudRefreshIntervalSeconds = 0.2d;

        [SerializeField] private UIDocument _document = null;
        [SerializeField] private PanelSettings _panelSettings = null;
        [SerializeField] private VisualTreeAsset _visualTree = null;
        [SerializeField] private StyleSheet _styleSheet = null;
        [SerializeField] private InputService _inputService = null;
        [SerializeField] private Transform _playerView = null;
        [SerializeField] private Transform _checkpointView = null;
        [SerializeField] private Transform _doorView = null;
        [SerializeField] private Transform _trapView = null;

        private GameplayTriggerPlayableSession _session;
        private GameplayTriggerPlayableHud _hud;
        private IInputProvider _input;
        private IDisposable _debugInputContext;
        private GameplayTriggerPlayableSnapshot _snapshot;
        private GameplayTriggerPlayableSnapshot _lastHudSnapshot;
        private readonly Vector2Int[] _uiMovePulses =
            new Vector2Int[MovePulseCapacity];
        private double _logicalAccumulator;
        private double _hudAccumulator;
        private int _uiMovePulseHead;
        private int _uiMovePulseCount;
        private bool _uiLeftHeld;
        private bool _uiRightHeld;
        private bool _uiUpHeld;
        private bool _uiDownHeld;
        private bool _uiCaptureRequested;
        private bool _uiRestoreRequested;
        private bool _uiResetRequested;
        private bool _initialized;

        /// <remarks>NoAlloc.</remarks>
        public bool IsInitialized => _initialized;

        /// <summary>Gets the latest read-only playable snapshot used by the Unity view.</summary>
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerPlayableSnapshot Snapshot => _snapshot;

        private void Awake()
        {
            if (!ValidateSerializedScene())
            {
                enabled = false;
                return;
            }

            _input = _inputService;
            _session = new GameplayTriggerPlayableSession();
            _snapshot = _session.Snapshot;
            ApplyView(in _snapshot);
        }

        private void Start()
        {
            if (_session == null)
                return;
            if (!TryEnableInputContexts())
                return;

            _hud = new GameplayTriggerPlayableHud(
                _document,
                _visualTree,
                _styleSheet,
                SetUiMoveHeld,
                QueueUiMovePulse,
                QueueCapture,
                QueueRestore,
                QueueReset);
            if (!_hud.TryBind(out string error))
            {
                Debug.LogError(error, this);
                enabled = false;
                return;
            }

            _initialized = true;
            RefreshHud();
        }

        private void Update()
        {
            if (!_initialized || _session == null)
                return;

            InputSnapshot input = _input != null
                ? _input.Snapshot
                : InputSnapshot.Empty;
            bool sampleAccepted = SubmitUnitySample(
                in input,
                out bool movePulseSubmitted);

            double delta = Math.Min(Time.unscaledDeltaTime, 0.25d);
            double maxAccumulated =
                GameplayTriggerPlayableSession.FixedDeltaTime * MaxLogicalTicksPerUpdate;
            _logicalAccumulator = Math.Min(
                _logicalAccumulator + delta,
                maxAccumulated);
            bool advanced = false;
            for (int i = 0;
                 i < MaxLogicalTicksPerUpdate &&
                 _logicalAccumulator >= GameplayTriggerPlayableSession.FixedDeltaTime;
                 i++)
            {
                GameplayTriggerPlayableTickResult tick = _session.Tick();
                if (!tick.Accepted)
                    break;

                _logicalAccumulator -= GameplayTriggerPlayableSession.FixedDeltaTime;
                _snapshot = tick.Snapshot;
                ApplyView(in _snapshot);
                advanced = true;
            }
            if (advanced && sampleAccepted && movePulseSubmitted)
                CommitUiMovePulse();

            _hudAccumulator += delta;
            if (PresentationStateChanged(in _snapshot, in _lastHudSnapshot) ||
                _hudAccumulator >= HudRefreshIntervalSeconds)
            {
                RefreshHud();
            }
        }

        private void OnDestroy()
        {
            _hud?.Dispose();
            _hud = null;
            _debugInputContext?.Dispose();
            _debugInputContext = null;
            _session?.Dispose();
            _session = null;
            _initialized = false;
        }

        private bool ValidateSerializedScene()
        {
            if (_document == null || _panelSettings == null || _visualTree == null ||
                _styleSheet == null || _inputService == null || _playerView == null ||
                _checkpointView == null || _doorView == null || _trapView == null)
            {
                Debug.LogError(
                    "Gameplay Trigger playable requires all serialized UI, input, and view references.",
                    this);
                return false;
            }
            if (_document.panelSettings != _panelSettings ||
                _document.visualTreeAsset != _visualTree)
            {
                Debug.LogError(
                    "Gameplay Trigger playable UIDocument bindings do not match the serialized assets.",
                    this);
                return false;
            }
            return true;
        }

        private bool TryEnableInputContexts()
        {
            _debugInputContext = _inputService.PushContext(
                InputContext.Debug,
                InputContextPolicy.Overlay);
            if (_inputService.IsContextEnabled(InputContext.Gameplay) &&
                _inputService.IsContextEnabled(InputContext.Debug))
            {
                return true;
            }

            Debug.LogError(
                "Gameplay Trigger playable requires Gameplay plus Debug input contexts.",
                this);
            _debugInputContext.Dispose();
            _debugInputContext = null;
            enabled = false;
            return false;
        }

        private bool SubmitUnitySample(
            in InputSnapshot input,
            out bool movePulseSubmitted)
        {
            int moveX = Quantize(input.Move.x);
            int moveZ = Quantize(input.Move.y);
            bool uiXHeld = _uiLeftHeld || _uiRightHeld;
            bool uiZHeld = _uiUpHeld || _uiDownHeld;
            if (uiXHeld)
                moveX = (_uiRightHeld ? 1000 : 0) - (_uiLeftHeld ? 1000 : 0);
            if (uiZHeld)
                moveZ = (_uiUpHeld ? 1000 : 0) - (_uiDownHeld ? 1000 : 0);

            movePulseSubmitted = TryPeekUiMovePulse(
                out int pulseX,
                out int pulseZ);
            if (movePulseSubmitted)
            {
                if (pulseX != 0)
                    moveX = pulseX;
                if (pulseZ != 0)
                    moveZ = pulseZ;
            }

            var sample = new GameplayTriggerPlayableInput(
                moveX,
                moveZ,
                capture: input.InteractPressed || _uiCaptureRequested,
                restore: input.DebugSecondaryPressed || _uiRestoreRequested,
                reset: input.RestartPressed || _uiResetRequested);
            if (!_session.SubmitInput(in sample))
            {
                movePulseSubmitted = false;
                return false;
            }

            _uiCaptureRequested = false;
            _uiRestoreRequested = false;
            _uiResetRequested = false;
            return true;
        }

        private void ApplyView(in GameplayTriggerPlayableSnapshot snapshot)
        {
            float x = (float)snapshot.PlayerXRaw / Fix64.Scale;
            float z = (float)snapshot.PlayerZRaw / Fix64.Scale;
            _playerView.position = new Vector3(x, 0.55f, z);
            _checkpointView.localScale = snapshot.CheckpointAccepted
                ? new Vector3(1.2f, 0.12f, 2.2f)
                : new Vector3(1f, 0.08f, 2f);
            _doorView.gameObject.SetActive(!snapshot.DoorOpen);
            _trapView.localScale = snapshot.SlowFramesRemaining > 0
                ? new Vector3(1.2f, 0.12f, 2.2f)
                : new Vector3(1f, 0.08f, 2f);
        }

        private void RefreshHud()
        {
            if (_hud == null || _session == null)
                return;

            _session.ComputeHash();
            _snapshot = _session.Snapshot;
            _hud.Refresh(in _snapshot);
            _lastHudSnapshot = _snapshot;
            _hudAccumulator = 0d;
        }

        private static bool PresentationStateChanged(
            in GameplayTriggerPlayableSnapshot current,
            in GameplayTriggerPlayableSnapshot previous)
        {
            return current.CheckpointAccepted != previous.CheckpointAccepted ||
                   current.DoorUnlocked != previous.DoorUnlocked ||
                   current.DoorOpen != previous.DoorOpen ||
                   current.TrapActivations != previous.TrapActivations ||
                   current.HasCapture != previous.HasCapture ||
                   current.CapturedFrame != previous.CapturedFrame ||
                   current.LastControlSucceeded != previous.LastControlSucceeded ||
                   current.LastOccurrenceKind != previous.LastOccurrenceKind ||
                   current.LastActionKindId != previous.LastActionKindId ||
                   current.LastResultKind != previous.LastResultKind ||
                   current.LastResultStatus != previous.LastResultStatus;
        }

        private static int Quantize(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * 1000f);
        }

        private void SetUiMoveHeld(int moveX, int moveZ, bool held)
        {
            if (moveX < 0)
                _uiLeftHeld = held;
            else if (moveX > 0)
                _uiRightHeld = held;
            if (moveZ > 0)
                _uiUpHeld = held;
            else if (moveZ < 0)
                _uiDownHeld = held;
        }

        private void QueueUiMovePulse(int moveX, int moveZ)
        {
            if (_uiMovePulseCount == _uiMovePulses.Length)
                return;

            int tail = (_uiMovePulseHead + _uiMovePulseCount) %
                _uiMovePulses.Length;
            _uiMovePulses[tail] = new Vector2Int(moveX, moveZ);
            _uiMovePulseCount++;
        }

        private bool TryPeekUiMovePulse(out int moveX, out int moveZ)
        {
            if (_uiMovePulseCount == 0)
            {
                moveX = 0;
                moveZ = 0;
                return false;
            }

            Vector2Int pulse = _uiMovePulses[_uiMovePulseHead];
            moveX = pulse.x;
            moveZ = pulse.y;
            return true;
        }

        private void CommitUiMovePulse()
        {
            if (_uiMovePulseCount == 0)
                return;

            _uiMovePulses[_uiMovePulseHead] = default;
            _uiMovePulseHead = (_uiMovePulseHead + 1) % _uiMovePulses.Length;
            _uiMovePulseCount--;
        }

        private void QueueCapture()
        {
            _uiCaptureRequested = true;
        }

        private void QueueRestore()
        {
            _uiRestoreRequested = true;
        }

        private void QueueReset()
        {
            _uiResetRequested = true;
        }
    }
}
