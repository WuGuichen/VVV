using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabKey
    {
        F,
        W,
        A,
        S,
        D,
        Shift,
        F9
    }

    internal static class CharacterTestRuntimeAiLabInput
    {
        private static readonly InputSystemMouseAdapter MouseAdapter = new();
        private static readonly List<RaycastResult> UiRaycastResults = new(8);
        private static PointerEventData _pointerEventData;

        public static Vector3 MousePosition
        {
            get
            {
                if (MouseAdapter.TryReadPosition(out Vector2 position))
                    return new Vector3(position.x, position.y, 0f);

                try
                {
                    return UnityEngine.Input.mousePosition;
                }
                catch (InvalidOperationException)
                {
                    return Vector3.zero;
                }
            }
        }

        public static float MouseWheelY
        {
            get
            {
                if (MouseAdapter.TryReadScroll(out Vector2 scroll))
                {
                    float value = scroll.y;
                    return Mathf.Abs(value) > 1f ? value / 120f : value;
                }

                try
                {
                    return UnityEngine.Input.mouseScrollDelta.y;
                }
                catch (InvalidOperationException)
                {
                    return 0f;
                }
            }
        }

        public static bool IsPointerBlockedByUi()
        {
            if (Fgui.Stage.isTouchOnUI)
                return true;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            _pointerEventData ??= new PointerEventData(eventSystem);
            _pointerEventData.Reset();
            _pointerEventData.position = MousePosition;
            UiRaycastResults.Clear();
            eventSystem.RaycastAll(_pointerEventData, UiRaycastResults);
            return UiRaycastResults.Count > 0;
        }

        /// <summary>View-camera shortcuts yield to text entry, popup navigation and selected Unity UI.</summary>
        public static bool IsKeyboardBlockedByUi()
        {
            if (Fgui.Stage.inst.focus is Fgui.InputTextField || Fgui.GRoot.inst.hasModalWindow || Fgui.GRoot.inst.hasAnyPopup)
                return true;
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            return selected != null && selected.activeInHierarchy;
        }

        public static bool IsKeyDown(CharacterTestRuntimeAiLabKey key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            if (key == CharacterTestRuntimeAiLabKey.Shift)
                return keyboard.leftShiftKey.wasPressedThisFrame
                    || keyboard.rightShiftKey.wasPressedThisFrame;

            KeyControl keyControl = ResolveKeyControl(keyboard, key);
            return keyControl != null && keyControl.wasPressedThisFrame;
        }

        public static bool IsKeyPressed(CharacterTestRuntimeAiLabKey key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            if (key == CharacterTestRuntimeAiLabKey.Shift)
                return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

            KeyControl keyControl = ResolveKeyControl(keyboard, key);
            return keyControl != null && keyControl.isPressed;
        }

        public static bool IsMouseButtonDown(int button)
        {
            if (MouseAdapter.TryReadButton(button, "wasPressedThisFrame", out bool pressed))
                return pressed;

            try
            {
                return UnityEngine.Input.GetMouseButtonDown(button);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public static bool IsMouseButtonPressed(int button)
        {
            if (MouseAdapter.TryReadButton(button, "isPressed", out bool pressed))
                return pressed;

            try
            {
                return UnityEngine.Input.GetMouseButton(button);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public static bool IsMouseButtonUp(int button)
        {
            if (MouseAdapter.TryReadButton(button, "wasReleasedThisFrame", out bool released))
                return released;

            try
            {
                return UnityEngine.Input.GetMouseButtonUp(button);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static KeyControl ResolveKeyControl(
            Keyboard keyboard,
            CharacterTestRuntimeAiLabKey key)
        {
            return key switch
            {
                CharacterTestRuntimeAiLabKey.F => keyboard.fKey,
                CharacterTestRuntimeAiLabKey.W => keyboard.wKey,
                CharacterTestRuntimeAiLabKey.A => keyboard.aKey,
                CharacterTestRuntimeAiLabKey.S => keyboard.sKey,
                CharacterTestRuntimeAiLabKey.D => keyboard.dKey,
                CharacterTestRuntimeAiLabKey.F9 => keyboard.f9Key,
                _ => null
            };
        }

        private sealed class InputSystemMouseAdapter
        {
            private readonly PropertyInfo _current;
            private readonly PropertyInfo _position;
            private readonly PropertyInfo _scroll;
            private readonly PropertyInfo _leftButton;
            private readonly PropertyInfo _rightButton;
            private readonly PropertyInfo _middleButton;

            public InputSystemMouseAdapter()
            {
                Type mouseType = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
                _current = mouseType?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                if (mouseType == null)
                    return;

                _position = mouseType.GetProperty("position");
                _scroll = mouseType.GetProperty("scroll");
                _leftButton = mouseType.GetProperty("leftButton");
                _rightButton = mouseType.GetProperty("rightButton");
                _middleButton = mouseType.GetProperty("middleButton");
            }

            public bool TryReadPosition(out Vector2 value)
            {
                return TryReadVector2(_position, out value);
            }

            public bool TryReadScroll(out Vector2 value)
            {
                return TryReadVector2(_scroll, out value);
            }

            public bool TryReadButton(int button, string propertyName, out bool value)
            {
                value = false;
                object mouse = _current?.GetValue(null);
                if (mouse == null)
                    return false;

                PropertyInfo buttonProperty = button switch
                {
                    0 => _leftButton,
                    1 => _rightButton,
                    2 => _middleButton,
                    _ => null
                };
                object control = buttonProperty?.GetValue(mouse);
                if (control == null)
                    return false;

                PropertyInfo property = control.GetType().GetProperty(propertyName);
                if (property == null || property.PropertyType != typeof(bool))
                    return false;

                value = (bool)property.GetValue(control);
                return true;
            }

            private bool TryReadVector2(PropertyInfo property, out Vector2 value)
            {
                value = default;
                object mouse = _current?.GetValue(null);
                object control = mouse != null ? property?.GetValue(mouse) : null;
                if (control == null)
                    return false;

                MethodInfo readValue = control.GetType().GetMethod("ReadValue", Type.EmptyTypes);
                if (readValue == null)
                    return false;

                object result = readValue.Invoke(control, Array.Empty<object>());
                if (result is not Vector2 vector)
                    return false;

                value = vector;
                return true;
            }
        }
    }

    internal static class CharacterTestRuntimeAiLabCameraResolver
    {
        private static UnityEngine.Camera _cachedCamera;
        private static long _cameraGeneration;
        private static bool _lookupAttempted;

        public static bool TryGetSceneCamera(out UnityEngine.Camera camera)
        {
            return TryGetSceneCamera(out camera, out _);
        }

        internal static bool TryGetSceneCamera(
            out UnityEngine.Camera camera,
            out long generation)
        {
            if (IsCachedAuthorityUsable(_cachedCamera))
            {
                camera = _cachedCamera;
                generation = _cameraGeneration;
                return true;
            }

            if (!ReferenceEquals(_cachedCamera, null))
            {
                SetAuthoritativeCamera(null);
                _lookupAttempted = false;
            }

            if (_lookupAttempted)
            {
                camera = null;
                generation = _cameraGeneration;
                return false;
            }

            _lookupAttempted = true;

            camera = UnityEngine.Camera.main;
            if (IsSceneCamera(camera))
            {
                SetAuthoritativeCamera(camera);
                generation = _cameraGeneration;
                return true;
            }

            GameObject tagged = GameObject.FindGameObjectWithTag("MainCamera");
            camera = tagged != null ? tagged.GetComponent<UnityEngine.Camera>() : null;
            if (IsSceneCamera(camera))
            {
                SetAuthoritativeCamera(camera);
                generation = _cameraGeneration;
                return true;
            }

            camera = FindFallbackSceneCamera();
            if (IsSceneCamera(camera))
            {
                SetAuthoritativeCamera(camera);
                generation = _cameraGeneration;
                return true;
            }

            camera = null;
            generation = _cameraGeneration;
            return false;
        }

        internal static void NotifySceneCameraChanged(UnityEngine.Camera camera)
        {
            if (IsSceneCamera(camera))
            {
                SetAuthoritativeCamera(camera);
                _lookupAttempted = true;
                return;
            }

            SetAuthoritativeCamera(null);
            _lookupAttempted = false;
        }

        internal static void RequestSceneCameraRefresh()
        {
            _lookupAttempted = false;
        }

        private static void SetAuthoritativeCamera(UnityEngine.Camera camera)
        {
            if (ReferenceEquals(_cachedCamera, camera))
                return;
            _cachedCamera = camera;
            _cameraGeneration++;
            if (_cameraGeneration <= 0)
                _cameraGeneration = 1;
        }

        private static UnityEngine.Camera FindFallbackSceneCamera()
        {
            UnityEngine.Camera[] cameras =
                UnityEngine.Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsInactive.Exclude);
            UnityEngine.Camera firstUsable = null;
            UnityEngine.Camera firstMainCamera = null;
            for (int i = 0; i < cameras.Length; i++)
            {
                UnityEngine.Camera candidate = cameras[i];
                if (!IsSceneCamera(candidate))
                    continue;

                if (firstUsable == null)
                    firstUsable = candidate;

                string name = candidate.gameObject.name;
                if (name.IndexOf("Runtime AI Lab", StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;

                if (name.IndexOf("Main Camera", StringComparison.OrdinalIgnoreCase) >= 0)
                    firstMainCamera ??= candidate;
            }

            return firstMainCamera ?? firstUsable;
        }

        private static bool IsUsable(UnityEngine.Camera camera)
        {
            return camera != null
                && camera.isActiveAndEnabled
                && camera.gameObject.activeInHierarchy;
        }

        private static bool IsCachedAuthorityUsable(UnityEngine.Camera camera)
        {
            return IsUsable(camera);
        }

        private static bool IsSceneCamera(UnityEngine.Camera camera)
        {
            if (!IsUsable(camera)
                || IsLikelyUiCamera(camera)
                || camera.GetComponent<CharacterTestRuntimeAiLabPreviewCameraMarker>() != null)
                return false;

            int uiLayer = LayerMask.NameToLayer("UI");
            int uiMask = uiLayer >= 0 ? 1 << uiLayer : 0;
            int nonUiMask = uiMask != 0 ? camera.cullingMask & ~uiMask : camera.cullingMask;
            return nonUiMask != 0;
        }

        private static bool IsLikelyUiCamera(UnityEngine.Camera camera)
        {
            string name = camera.gameObject.name;
            return name.IndexOf("Stage", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Fairy", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("FGUI", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
