using System;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(94)]
    [AddComponentMenu("MxFramework/Game/Character Test/Runtime AI Lab Setup Camera")]
    internal sealed class CharacterTestRuntimeAiLabSetupCameraController : MonoBehaviour
    {
        private const float DefaultYaw = 45f;
        private const float DefaultPitch = 55f;
        private const float DefaultDistance = 56f;
        private const float MinDistance = 18f;
        private const float MaxDistance = 90f;
        private const float MinPitch = 45f;
        private const float MaxPitch = 65f;
        private const float PanSpeed = 18f;
        private const float MousePanSpeed = 0.08f;
        private const float ZoomSpeed = 5f;
        private const float RotateSpeed = 0.35f;

        private readonly Vector3 _arenaCenter = Vector3.zero;
        private CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private CharacterTestRuntimeAiLabSetupOverlay _setupOverlay;
        private IDisposable _cursorScope;
        private Vector3 _focus;
        private float _yaw = DefaultYaw;
        private float _pitch = DefaultPitch;
        private float _distance = DefaultDistance;
        private Vector3 _lastPanMousePosition;
        private Vector3 _lastRotateMousePosition;

        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public float Distance => _distance;

        public void FitView()
        {
            _focus = _arenaCenter;
            _yaw = DefaultYaw;
            _pitch = DefaultPitch;
            _distance = DefaultDistance;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
            _setupOverlay = GetComponent<CharacterTestRuntimeAiLabSetupOverlay>();
            FitView();
        }

        private void OnDisable()
        {
            ReleaseCursorScope();
        }

        private void Update()
        {
            if (!IsSetupActive())
            {
                ReleaseCursorScope();
                return;
            }

            EnsureCursorReleased();

            bool keyboardBlocked = CharacterTestRuntimeAiLabInput.IsKeyboardBlockedByUi();
            bool draggingSlot = IsSetupDragActive();
            if (!keyboardBlocked && !draggingSlot)
            {
                if (CharacterTestRuntimeAiLabInput.IsKeyDown(CharacterTestRuntimeAiLabKey.F))
                    FitView();
                UpdateKeyboardPan();
            }
            if (keyboardBlocked || CharacterTestRuntimeAiLabInput.IsPointerBlockedByUi() || draggingSlot)
            {
                _lastPanMousePosition = _lastRotateMousePosition = CharacterTestRuntimeAiLabInput.MousePosition;
                return;
            }

            UpdateMousePan();
            UpdateMouseRotate();
            UpdateMouseZoom();
        }

        private void LateUpdate()
        {
            if (!IsSetupActive())
                return;

            if (!CharacterTestRuntimeAiLabCameraResolver.TryGetSceneCamera(out UnityEngine.Camera camera))
            {
                _controller?.SetSetupInteractionStatus("Setup camera unavailable: no active scene camera found.");
                return;
            }

            _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
            _distance = Mathf.Clamp(_distance, MinDistance, MaxDistance);
            _focus = ClampFocus(_focus);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            camera.orthographic = false;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            camera.transform.SetPositionAndRotation(
                _focus - rotation * Vector3.forward * _distance,
                rotation);
        }

        private bool IsSetupActive()
        {
            if (_controller == null)
                _controller = GetComponent<CharacterTestRuntimeAiPlannerDevSceneController>();

            return _controller != null
                && _controller.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup;
        }

        private bool IsSetupDragActive()
        {
            if (_setupOverlay == null)
                _setupOverlay = GetComponent<CharacterTestRuntimeAiLabSetupOverlay>();

            return _setupOverlay != null && _setupOverlay.IsDraggingSlot;
        }

        private void EnsureCursorReleased()
        {
            if (_cursorScope == null && _controller != null)
                _cursorScope = _controller.PushSetupCursorReleased();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ReleaseCursorScope()
        {
            if (_cursorScope == null)
                return;

            _cursorScope.Dispose();
            _cursorScope = null;
        }

        private void UpdateKeyboardPan()
        {
            Vector3 direction = Vector3.zero;
            if (CharacterTestRuntimeAiLabInput.IsKeyPressed(CharacterTestRuntimeAiLabKey.W))
                direction.z += 1f;
            if (CharacterTestRuntimeAiLabInput.IsKeyPressed(CharacterTestRuntimeAiLabKey.S))
                direction.z -= 1f;
            if (CharacterTestRuntimeAiLabInput.IsKeyPressed(CharacterTestRuntimeAiLabKey.A))
                direction.x -= 1f;
            if (CharacterTestRuntimeAiLabInput.IsKeyPressed(CharacterTestRuntimeAiLabKey.D))
                direction.x += 1f;

            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Vector3 forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            _focus += (forward * direction.z + right * direction.x).normalized
                * (PanSpeed * Time.unscaledDeltaTime);
        }

        private void UpdateMousePan()
        {
            Vector3 mousePosition = CharacterTestRuntimeAiLabInput.MousePosition;
            bool shiftPressed = CharacterTestRuntimeAiLabInput.IsKeyPressed(CharacterTestRuntimeAiLabKey.Shift);
            bool panPressed = CharacterTestRuntimeAiLabInput.IsMouseButtonPressed(2)
                || (CharacterTestRuntimeAiLabInput.IsMouseButtonPressed(1) && !shiftPressed);
            bool panDown = CharacterTestRuntimeAiLabInput.IsMouseButtonDown(2)
                || (CharacterTestRuntimeAiLabInput.IsMouseButtonDown(1) && !shiftPressed);
            if (panDown)
                _lastPanMousePosition = mousePosition;

            if (!panPressed)
            {
                _lastPanMousePosition = mousePosition;
                return;
            }

            Vector3 delta = mousePosition - _lastPanMousePosition;
            _lastPanMousePosition = mousePosition;
            Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            Vector3 forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            _focus -= right * (delta.x * MousePanSpeed);
            _focus -= forward * (delta.y * MousePanSpeed);
        }

        private void UpdateMouseRotate()
        {
            Vector3 mousePosition = CharacterTestRuntimeAiLabInput.MousePosition;
            bool rotatePressed = CharacterTestRuntimeAiLabInput.IsMouseButtonPressed(1)
                && CharacterTestRuntimeAiLabInput.IsKeyPressed(CharacterTestRuntimeAiLabKey.Shift);
            if (CharacterTestRuntimeAiLabInput.IsMouseButtonDown(1))
                _lastRotateMousePosition = mousePosition;

            if (!rotatePressed)
            {
                _lastRotateMousePosition = mousePosition;
                return;
            }

            Vector3 delta = mousePosition - _lastRotateMousePosition;
            _lastRotateMousePosition = mousePosition;
            _yaw += delta.x * RotateSpeed;
        }

        private void UpdateMouseZoom()
        {
            float wheel = CharacterTestRuntimeAiLabInput.MouseWheelY;
            if (Mathf.Abs(wheel) <= 0.0001f)
                return;

            _distance -= wheel * ZoomSpeed;
        }

        private static Vector3 ClampFocus(Vector3 focus)
        {
            return new Vector3(
                Mathf.Clamp(
                    focus.x,
                    -CharacterTestRuntimeAiLabSpawnValidator.ArenaHalfWidth,
                    CharacterTestRuntimeAiLabSpawnValidator.ArenaHalfWidth),
                0f,
                Mathf.Clamp(
                    focus.z,
                    -CharacterTestRuntimeAiLabSpawnValidator.ArenaHalfDepth,
                    CharacterTestRuntimeAiLabSpawnValidator.ArenaHalfDepth));
        }
    }
}
