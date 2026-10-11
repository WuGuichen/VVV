using System;
using MxFramework.UI.Toolkit;
using UnityEngine.UIElements;

namespace MxFramework.Demo.GameplayTriggers
{
    /// <summary>Cached UI Toolkit binding for the Gameplay Trigger validation surface.</summary>
    internal sealed class GameplayTriggerPlayableHud : IDisposable
    {
        private readonly UIDocument _document;
        private readonly VisualTreeAsset _visualTree;
        private readonly StyleSheet _styleSheet;
        private readonly Action<int, int, bool> _moveHeldChanged;
        private readonly Action<int, int> _movePulseRequested;
        private readonly Action _captureRequested;
        private readonly Action _restoreRequested;
        private readonly Action _resetRequested;

        private VisualElement _root;
        private Label _frameLabel;
        private Label _positionLabel;
        private Label _checkpointLabel;
        private Label _doorLabel;
        private Label _trapLabel;
        private Label _eventLabel;
        private Label _hashLabel;
        private Label _saveLabel;
        private Button _leftButton;
        private Button _rightButton;
        private Button _upButton;
        private Button _downButton;
        private Button _captureButton;
        private Button _restoreButton;
        private Button _resetButton;
        private bool _bound;

        internal GameplayTriggerPlayableHud(
            UIDocument document,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Action<int, int, bool> moveHeldChanged,
            Action<int, int> movePulseRequested,
            Action captureRequested,
            Action restoreRequested,
            Action resetRequested)
        {
            _document = document;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
            _moveHeldChanged = moveHeldChanged;
            _movePulseRequested = movePulseRequested;
            _captureRequested = captureRequested;
            _restoreRequested = restoreRequested;
            _resetRequested = resetRequested;
        }

        internal bool TryBind(out string error)
        {
            error = string.Empty;
            if (_document == null || _document.visualTreeAsset != _visualTree ||
                _styleSheet == null)
            {
                error = "Gameplay Trigger HUD serialized asset bindings are invalid.";
                return false;
            }

            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
            {
                error = "Gameplay Trigger HUD UIDocument has no root visual element.";
                return false;
            }
            if (!documentRoot.styleSheets.Contains(_styleSheet))
                documentRoot.styleSheets.Add(_styleSheet);

            _root = documentRoot.Q<VisualElement>("gameplay-trigger-root");
            _frameLabel = documentRoot.Q<Label>("frame-label");
            _positionLabel = documentRoot.Q<Label>("position-label");
            _checkpointLabel = documentRoot.Q<Label>("checkpoint-label");
            _doorLabel = documentRoot.Q<Label>("door-label");
            _trapLabel = documentRoot.Q<Label>("trap-label");
            _eventLabel = documentRoot.Q<Label>("event-label");
            _hashLabel = documentRoot.Q<Label>("hash-label");
            _saveLabel = documentRoot.Q<Label>("save-label");
            _leftButton = documentRoot.Q<Button>("move-left-button");
            _rightButton = documentRoot.Q<Button>("move-right-button");
            _upButton = documentRoot.Q<Button>("move-up-button");
            _downButton = documentRoot.Q<Button>("move-down-button");
            _captureButton = documentRoot.Q<Button>("capture-button");
            _restoreButton = documentRoot.Q<Button>("restore-button");
            _resetButton = documentRoot.Q<Button>("reset-button");
            if (_root == null || _frameLabel == null || _positionLabel == null ||
                _checkpointLabel == null || _doorLabel == null || _trapLabel == null ||
                _eventLabel == null || _hashLabel == null || _saveLabel == null ||
                _leftButton == null || _rightButton == null || _upButton == null ||
                _downButton == null || _captureButton == null || _restoreButton == null ||
                _resetButton == null)
            {
                error = "Gameplay Trigger HUD UXML is missing a required named element.";
                return false;
            }

            BindMoveButton(_leftButton, OnMoveLeftPulse);
            BindMoveButton(_rightButton, OnMoveRightPulse);
            BindMoveButton(_upButton, OnMoveUpPulse);
            BindMoveButton(_downButton, OnMoveDownPulse);
            _captureButton.clicked += OnCapture;
            _restoreButton.clicked += OnRestore;
            _resetButton.clicked += OnReset;
            _bound = true;
            return true;
        }

        /// <remarks>LowFreqAlloc. Called at 5 Hz or on presentation state transitions.</remarks>
        internal void Refresh(in GameplayTriggerPlayableSnapshot snapshot)
        {
            if (!_bound)
                return;

            _frameLabel.text = "Frame " + snapshot.Frame;
            _positionLabel.text =
                "X " + FormatRaw(snapshot.PlayerXRaw) + "  Z " + FormatRaw(snapshot.PlayerZRaw);
            _checkpointLabel.text = snapshot.CheckpointAccepted
                ? "ACQUIRED"
                : "PENDING";
            MxUiThemeTokens.SetStatusTone(
                _checkpointLabel,
                snapshot.CheckpointAccepted ? MxUiTone.Positive : MxUiTone.Neutral);
            _doorLabel.text = snapshot.DoorOpen
                ? "OPEN"
                : snapshot.DoorUnlocked ? "UNLOCKED" : "LOCKED";
            MxUiThemeTokens.SetStatusTone(
                _doorLabel,
                snapshot.DoorOpen
                    ? MxUiTone.Positive
                    : snapshot.DoorUnlocked ? MxUiTone.Warning : MxUiTone.Neutral);
            _trapLabel.text =
                "Activations " + snapshot.TrapActivations +
                "  Slow " + snapshot.SlowFramesRemaining + "f";
            MxUiThemeTokens.SetStatusTone(
                _trapLabel,
                snapshot.SlowFramesRemaining > 0 ? MxUiTone.Danger : MxUiTone.Neutral);
            _eventLabel.text =
                "Occurrence " + snapshot.LastOccurrenceKind +
                " / Action " + snapshot.LastActionKindId +
                " / Result " + snapshot.LastResultKind + ":" + snapshot.LastResultStatus;
            _hashLabel.text = "Authority 0x" + snapshot.AuthorityHash.ToString("X16");
            _saveLabel.text = snapshot.HasCapture
                ? "Captured end-F " + snapshot.CapturedFrame +
                  (snapshot.LastControlSucceeded ? " / ready" : " / last control rejected")
                : snapshot.LastControlSucceeded ? "No capture" : "Capture required / rejected";
        }

        public void Dispose()
        {
            if (!_bound)
                return;

            UnbindMoveButton(_leftButton, OnMoveLeftPulse);
            UnbindMoveButton(_rightButton, OnMoveRightPulse);
            UnbindMoveButton(_upButton, OnMoveUpPulse);
            UnbindMoveButton(_downButton, OnMoveDownPulse);
            _captureButton.clicked -= OnCapture;
            _restoreButton.clicked -= OnRestore;
            _resetButton.clicked -= OnReset;
            _bound = false;
        }

        private static string FormatRaw(long raw)
        {
            bool negative = raw < 0L;
            long absolute = Math.Abs(raw);
            long whole = absolute / 1000000L;
            long fraction = absolute % 1000000L / 1000L;
            return (negative ? "-" : string.Empty) +
                   whole + "." + fraction.ToString("D3");
        }

        private void BindMoveButton(Button button, Action pulse)
        {
            button.RegisterCallback<PointerDownEvent>(
                OnMovePointerDown,
                TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(OnMovePointerUp);
            button.RegisterCallback<PointerCaptureOutEvent>(OnMovePointerCaptureOut);
            button.clicked += pulse;
        }

        private void UnbindMoveButton(Button button, Action pulse)
        {
            button.UnregisterCallback<PointerDownEvent>(
                OnMovePointerDown,
                TrickleDown.TrickleDown);
            button.UnregisterCallback<PointerUpEvent>(OnMovePointerUp);
            button.UnregisterCallback<PointerCaptureOutEvent>(OnMovePointerCaptureOut);
            button.clicked -= pulse;
        }

        private void OnMovePointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 ||
                !TryGetMoveDirection(evt.currentTarget, out int moveX, out int moveZ))
            {
                return;
            }

            _moveHeldChanged(moveX, moveZ, true);
        }

        private void OnMovePointerUp(PointerUpEvent evt)
        {
            if (evt.button != 0 ||
                !TryGetMoveDirection(evt.currentTarget, out int moveX, out int moveZ))
            {
                return;
            }

            _moveHeldChanged(moveX, moveZ, false);
        }

        private void OnMovePointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (TryGetMoveDirection(evt.currentTarget, out int moveX, out int moveZ))
                _moveHeldChanged(moveX, moveZ, false);
        }

        private bool TryGetMoveDirection(
            object target,
            out int moveX,
            out int moveZ)
        {
            moveX = 0;
            moveZ = 0;
            if (ReferenceEquals(target, _leftButton))
                moveX = -1000;
            else if (ReferenceEquals(target, _rightButton))
                moveX = 1000;
            else if (ReferenceEquals(target, _upButton))
                moveZ = 1000;
            else if (ReferenceEquals(target, _downButton))
                moveZ = -1000;
            else
                return false;
            return true;
        }

        private void OnMoveLeftPulse() => _movePulseRequested(-1000, 0);
        private void OnMoveRightPulse() => _movePulseRequested(1000, 0);
        private void OnMoveUpPulse() => _movePulseRequested(0, 1000);
        private void OnMoveDownPulse() => _movePulseRequested(0, -1000);
        private void OnCapture() => _captureRequested();
        private void OnRestore() => _restoreRequested();
        private void OnReset() => _resetRequested();
    }
}
