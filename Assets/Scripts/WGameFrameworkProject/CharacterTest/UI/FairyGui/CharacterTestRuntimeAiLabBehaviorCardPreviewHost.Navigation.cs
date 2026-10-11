using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardPreviewHost
    {
        private Fgui.GLoader _navigationLoader;
        private bool _navigationWasTouchable;
        private bool _navigationDragging;
        private bool _navigationRotate;
        private int _navigationTouchId;
        private Vector2 _navigationPosition;
        private Vector3 _navigationPivot;

        private void AttachNavigation(Fgui.GLoader loader)
        {
            if (ReferenceEquals(_navigationLoader, loader)) return;
            DetachNavigation();
            _navigationLoader = loader;
            if (loader == null) return;
            _navigationWasTouchable = loader.touchable;
            loader.touchable = true;
            loader.displayObject.onMouseWheel.Add(OnNavigationWheel);
            loader.onTouchBegin.Add(OnNavigationBegin);
            loader.onTouchMove.Add(OnNavigationMove);
            loader.onTouchEnd.Add(OnNavigationEnd);
        }

        private void DetachNavigation()
        {
            EndNavigation();
            if (_navigationLoader != null && !_navigationLoader.isDisposed)
            {
                _navigationLoader.displayObject.onMouseWheel.Remove(OnNavigationWheel);
                _navigationLoader.onTouchBegin.Remove(OnNavigationBegin);
                _navigationLoader.onTouchMove.Remove(OnNavigationMove);
                _navigationLoader.onTouchEnd.Remove(OnNavigationEnd);
                _navigationLoader.touchable = _navigationWasTouchable;
            }
            _navigationLoader = null;
        }

        private bool CanNavigate => _active && _result.Ready && _previewCamera != null
            && IsLoaderAlive(_navigationLoader) && !CharacterTestRuntimeAiLabInput.IsKeyboardBlockedByUi();

        private void OnNavigationWheel(Fgui.EventContext context)
        {
            if (!CanNavigate || context.inputEvent == null) return;
            float wheel = context.inputEvent.mouseWheelDelta;
            if (Mathf.Abs(wheel) < 0.0001f) return;
            ZoomCamera(_previewCamera, _navigationPivot, wheel);
            SetCameraMode(CharacterTestPreviewCameraMode.Free);
            _fitRequested = false;
            context.StopPropagation();
        }

        private void OnNavigationBegin(Fgui.EventContext context)
        {
            var input = context.inputEvent;
            if (!CanNavigate || input == null || (input.button != 1 && input.button != 2)) return;
            _navigationDragging = true;
            _navigationRotate = input.button == 1 && input.shift;
            _navigationTouchId = input.touchId;
            _navigationPosition = _navigationLoader.GlobalToLocal(input.position);
            context.CaptureTouch();
            context.StopPropagation();
        }

        private void OnNavigationMove(Fgui.EventContext context)
        {
            var input = context.inputEvent;
            if (!_navigationDragging || input == null || input.touchId != _navigationTouchId) return;
            if (!CanNavigate) { EndNavigation(); return; }
            Vector2 position = _navigationLoader.GlobalToLocal(input.position);
            Vector2 delta = position - _navigationPosition;
            _navigationPosition = position;
            DragCamera(_previewCamera, ref _navigationPivot, delta, _navigationRotate);
            SetCameraMode(CharacterTestPreviewCameraMode.Free);
            _fitRequested = false;
            context.StopPropagation();
        }

        private void OnNavigationEnd(Fgui.EventContext context)
        {
            if (!_navigationDragging) return;
            EndNavigation();
            context.StopPropagation();
        }

        private void EndNavigation()
        {
            if (_navigationDragging && Application.isPlaying && _navigationLoader != null)
                Fgui.Stage.inst.RemoveTouchMonitor(_navigationLoader);
            _navigationDragging = false;
        }

        internal static void ZoomCamera(UnityEngine.Camera camera, Vector3 pivot, float wheel)
        {
            if (camera == null || float.IsNaN(wheel) || float.IsInfinity(wheel)) return;
            float factor = Mathf.Exp(Mathf.Clamp(wheel, -20f, 20f) * 0.08f);
            if (camera.orthographic)
                camera.orthographicSize = Mathf.Clamp(camera.orthographicSize * factor, 0.5f, 100f);
            else
            {
                Vector3 offset = camera.transform.position - pivot;
                float distance = offset.magnitude;
                if (distance > 0.001f)
                    camera.transform.position = pivot + offset / distance * Mathf.Clamp(distance * factor, 1f, 150f);
            }
        }

        internal static void DragCamera(UnityEngine.Camera camera, ref Vector3 pivot, Vector2 delta, bool rotate)
        {
            if (camera == null || float.IsNaN(delta.x) || float.IsNaN(delta.y)
                || float.IsInfinity(delta.x) || float.IsInfinity(delta.y)) return;
            Vector3 offset = camera.transform.position - pivot;
            float distance = Mathf.Max(1f, offset.magnitude);
            if (rotate)
            {
                float yaw = Mathf.Atan2(offset.x, offset.z) + delta.x * 0.35f * Mathf.Deg2Rad;
                float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(offset.y / distance, -1f, 1f))
                    + delta.y * 0.35f * Mathf.Deg2Rad, -80f * Mathf.Deg2Rad, 80f * Mathf.Deg2Rad);
                camera.transform.position = pivot + new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch),
                    Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * distance;
                camera.transform.LookAt(pivot);
            }
            else
            {
                Vector3 movement = (-camera.transform.right * delta.x + camera.transform.up * delta.y) * distance * 0.002f;
                pivot += movement;
                camera.transform.position += movement;
            }
        }
    }
}
