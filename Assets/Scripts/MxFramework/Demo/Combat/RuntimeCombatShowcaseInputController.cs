using MxFramework.Input;
using MxFramework.UI.Toolkit;
using UnityEngine;

namespace MxFramework.Demo
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimeCombatShowcaseRunner))]
    [AddComponentMenu("MxFramework/Demo/Runtime Combat Showcase Input Controller")]
    public sealed class RuntimeCombatShowcaseInputController : MonoBehaviour
    {
        private const float ClickDragThreshold = 6f;
        private const float FixedStepEpsilon = 0.0001f;

        [SerializeField] private Camera _camera;
        [SerializeField] private float _orbitYaw = 0f;
        [SerializeField] private float _orbitPitch = 35f;
        [SerializeField] private float _orbitDistance = 8f;
        [SerializeField] private float _orbitSensitivity = 0.25f;
        [SerializeField] private float _zoomSensitivity = 3f;
        [SerializeField] private float _minOrbitDistance = 3f;
        [SerializeField] private float _maxOrbitDistance = 16f;
        [SerializeField] private float _groundY = 0f;
        [SerializeField] private float _rightHudExclusionWidth = 380f;
        [SerializeField] private float _characterControlStepRate = 60f;
        [SerializeField] private int _maxCharacterControlStepsPerUpdate = 4;

        private RuntimeCombatShowcaseRunner _runner;
        private MxRuntimeHudController _hud;
        private IInputProvider _input;
        private FakeInputProvider _characterControlInput;
        private InputSnapshot _latestInput;
        private Transform _selectedDragMarker;
        private bool _selectedMarkerOnMouseDown;
        private bool _isDraggingSelected;
        private bool _isOrbiting;
        private Vector3 _lastMousePosition;
        private Vector3 _mouseDownPosition;
        private Vector3 _dragGroundOffset;
        private float _characterControlStepAccumulator;

        private void Awake()
        {
            _runner = GetComponent<RuntimeCombatShowcaseRunner>();
            _hud = GetComponent<MxRuntimeHudController>();
            _characterControlInput = new FakeInputProvider();
            ResolveInput();
            ResolveCamera();
            ApplyCameraOrbit();
        }

        private void Update()
        {
            if (_runner == null || !_runner.IsInitialized)
                return;

            ResolveCamera();
            ResolveInput();
            _latestInput = _input != null ? _input.Snapshot : InputSnapshot.Empty;
            HandleSelectionAndDrag(_latestInput);
            HandleOrbit(_latestInput);
            HandleMotionInput(_latestInput);
            HandleKeyboardCommands(_latestInput);
        }

        private void LateUpdate()
        {
            if (_isOrbiting || (!IsPointerOverHud(_latestInput.Point) && _latestInput.Scroll.y != 0f))
                ApplyCameraOrbit();
        }

        private void HandleSelectionAndDrag(InputSnapshot input)
        {
            Vector3 pointer = input.Point;
            if (input.ClickPressed)
            {
                _mouseDownPosition = pointer;
                _selectedMarkerOnMouseDown = !IsPointerOverHud(pointer) && TrySelectMarkerUnderCursor(pointer, out _selectedDragMarker);
                if (_selectedMarkerOnMouseDown && _selectedDragMarker != null && TryProjectMouseToGround(pointer, out Vector3 groundPoint))
                    _dragGroundOffset = _selectedDragMarker.position - groundPoint;
                else
                    _dragGroundOffset = Vector3.zero;

                _isDraggingSelected = false;
            }

            if (_selectedMarkerOnMouseDown && input.ClickHeld && !IsPointerOverHud(pointer))
            {
                if (!_isDraggingSelected && !MouseMovedLessThanClickThreshold(pointer))
                    _isDraggingSelected = true;

                if (_isDraggingSelected && TryProjectMouseToGround(pointer, out Vector3 groundPoint))
                    _runner.MoveSelectedTo(groundPoint + _dragGroundOffset);
            }

            if (input.ClickReleased)
            {
                _selectedDragMarker = null;
                _selectedMarkerOnMouseDown = false;
                _isDraggingSelected = false;
                _dragGroundOffset = Vector3.zero;
            }
        }

        private void HandleOrbit(InputSnapshot input)
        {
            Vector3 pointer = input.Point;
            if (input.RightClickPressed)
            {
                _lastMousePosition = pointer;
                _mouseDownPosition = pointer;
                _isOrbiting = false;
                if (IsPointerOverHud(pointer))
                    return;
            }

            if (input.RightClickHeld && !IsPointerOverHud(pointer))
            {
                Vector3 delta = pointer - _lastMousePosition;
                if (_isOrbiting || delta.sqrMagnitude > ClickDragThreshold * ClickDragThreshold)
                {
                    _isOrbiting = true;
                    _orbitYaw += delta.x * _orbitSensitivity;
                    _orbitPitch = Mathf.Clamp(_orbitPitch - delta.y * _orbitSensitivity, 15f, 75f);
                }

                _lastMousePosition = pointer;
            }

            if (input.RightClickReleased)
                _isOrbiting = false;

            float scroll = Mathf.Clamp(input.Scroll.y, -1f, 1f);
            if (scroll != 0f && !IsPointerOverHud(pointer))
                _orbitDistance = Mathf.Clamp(_orbitDistance - scroll * _zoomSensitivity, _minOrbitDistance, _maxOrbitDistance);
        }

        private void HandleKeyboardCommands(InputSnapshot input)
        {
            if (input.DebugSecondaryPressed)
                _runner.ProbeFromSelected();

            if (input.DebugCyclePressed)
                _runner.CycleQueryShape();

            if (input.DebugStepPressed)
                _runner.RunRuntimeAiPlannerCommand();

            if (input.RestartPressed)
                _runner.ResetShowcase();

            if (input.ToggleHudPressed)
            {
                _hud = _hud != null ? _hud : GetComponent<MxRuntimeHudController>();
                if (_hud != null)
                    _hud.ToggleHudCollapsed();
            }
        }

        private void HandleMotionInput(InputSnapshot input)
        {
            SyncCharacterControlInput(input);
            EnqueueCharacterControlPressedCommands(input);
            StepCharacterControlFixed();
        }

        private void StepCharacterControlFixed()
        {
            if (_characterControlInput == null)
                return;

            float stepRate = Mathf.Max(1f, _characterControlStepRate);
            float fixedDelta = 1f / stepRate;
            _characterControlStepAccumulator += Mathf.Max(0f, Time.deltaTime);

            int maxSteps = Mathf.Max(1, _maxCharacterControlStepsPerUpdate);
            int steps = 0;
            while (_characterControlStepAccumulator + FixedStepEpsilon >= fixedDelta && steps < maxSteps)
            {
                _runner.StepCharacterControlFromInput(_characterControlInput);
                _characterControlStepAccumulator -= fixedDelta;
                steps++;
            }

            if (steps == maxSteps && _characterControlStepAccumulator >= fixedDelta)
                _characterControlStepAccumulator = 0f;
        }

        private void SyncCharacterControlInput(InputSnapshot input)
        {
            if (_characterControlInput == null)
                _characterControlInput = new FakeInputProvider();

            long nextFrame = _runner.CurrentFrame.Value + 1L;
            if (_characterControlInput.Commands.CurrentFrame > nextFrame)
                _characterControlInput.Commands.Reset(nextFrame);

            bool gameplayEnabled = _input == null || _input.IsContextEnabled(InputContext.Gameplay);
            _characterControlInput.SetContext(gameplayEnabled ? InputContext.Gameplay : _input.CurrentContext);
            _characterControlInput.SetSnapshot(CreateCharacterControlSnapshot(input));
        }

        private void EnqueueCharacterControlPressedCommands(InputSnapshot input)
        {
            if (_characterControlInput == null || (_input != null && !_input.IsContextEnabled(InputContext.Gameplay)))
                return;

            long frame = _runner.CurrentFrame.Value + 1L;
            if (frame < _characterControlInput.Commands.CurrentFrame)
                frame = _characterControlInput.Commands.CurrentFrame;

            EnqueueCharacterControlPressedCommand(frame, input.JumpPressed, InputIntent.Jump, "combat-showcase-keyboard:Jump");
            EnqueueCharacterControlPressedCommand(frame, input.DebugPrimaryPressed, InputIntent.DebugPrimary, "combat-showcase-keyboard:DebugPrimary");
            EnqueueCharacterControlPressedCommand(frame, input.AttackPrimaryPressed, InputIntent.AttackPrimary, "combat-showcase-keyboard:AttackPrimary");
            EnqueueCharacterControlPressedCommand(frame, input.AttackSecondaryPressed, InputIntent.AttackSecondary, "combat-showcase-keyboard:AttackSecondary");
        }

        private void EnqueueCharacterControlPressedCommand(long frame, bool pressed, InputIntent intent, string traceId)
        {
            if (!pressed)
                return;

            _characterControlInput.Commands.TryEnqueue(new InputCommand(
                frame,
                sourceId: 0,
                intent,
                traceId: traceId),
                out _);
        }

        private static InputSnapshot CreateCharacterControlSnapshot(InputSnapshot input)
        {
            return new InputSnapshot(
                input.Move,
                input.Look,
                input.Navigate,
                input.Point,
                input.Scroll,
                input.Throttle,
                jumpPressed: false,
                jumpHeld: input.JumpHeld,
                jumpReleased: false,
                attackPrimaryPressed: false,
                attackPrimaryHeld: input.AttackPrimaryHeld,
                attackSecondaryPressed: false,
                interactPressed: false,
                dodgePressed: false,
                sprintHeld: input.SprintHeld,
                submitPressed: false,
                cancelPressed: false,
                pausePressed: false,
                debugTogglePressed: false);
        }

        private bool TrySelectMarkerUnderCursor(Vector3 pointer, out Transform marker)
        {
            marker = null;
            if (_camera == null)
                return false;

            Ray ray = _camera.ScreenPointToRay(pointer);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
                return false;

            marker = FindMarkerRoot(hit.transform);
            return marker != null && _runner.SelectMarker(marker);
        }

        private Transform FindMarkerRoot(Transform candidate)
        {
            while (candidate != null)
            {
                if (_runner.PlayerMarker != null && candidate == _runner.PlayerMarker)
                    return candidate;

                if (_runner.EnemyMarker != null && candidate == _runner.EnemyMarker)
                    return candidate;

                candidate = candidate.parent;
            }

            return null;
        }

        private bool TryProjectMouseToGround(Vector3 pointer, out Vector3 point)
        {
            point = default;
            if (_camera == null)
                return false;

            Ray ray = _camera.ScreenPointToRay(pointer);
            var plane = new Plane(Vector3.up, new Vector3(0f, _groundY, 0f));
            if (!plane.Raycast(ray, out float distance))
                return false;

            point = ray.GetPoint(distance);
            return true;
        }

        private void ResolveCamera()
        {
            if (_camera == null)
                _camera = Camera.main;
        }

        private void ApplyCameraOrbit()
        {
            if (_camera == null)
                return;

            Vector3 pivot = GetCameraPivot();
            Quaternion rotation = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
            _camera.transform.position = pivot + rotation * new Vector3(0f, 0f, -_orbitDistance);
            _camera.transform.rotation = rotation;
        }

        private Vector3 GetCameraPivot()
        {
            Transform player = _runner.PlayerMarker;
            Transform enemy = _runner.EnemyMarker;
            if (player != null && enemy != null)
                return Vector3.Lerp(player.position, enemy.position, 0.5f);

            if (player != null)
                return player.position;

            if (enemy != null)
                return enemy.position;

            return Vector3.zero;
        }

        private void ResolveInput()
        {
            if (_input == null)
                _input = InputProviderResolver.ResolveOrCreateDefault(this);
        }

        private bool MouseMovedLessThanClickThreshold(Vector3 pointer)
        {
            return (pointer - _mouseDownPosition).sqrMagnitude <= ClickDragThreshold * ClickDragThreshold;
        }

        private bool IsPointerOverHud(Vector3 pointer)
        {
            return pointer.x >= Screen.width - _rightHudExclusionWidth;
        }
    }
}
