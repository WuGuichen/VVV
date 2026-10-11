using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    /// <summary>
    /// Locates the frontmost clickable runtime UI below the pointer while the Editor is in Play Mode.
    /// </summary>
    /// <remarks>LowFreqAlloc. This editor-only diagnostic runs only when explicitly invoked.</remarks>
    [InitializeOnLoad]
    internal static class PlayModeUiHierarchyLocator
    {
        internal const string ShortcutId = "MxFramework/Play Mode/Locate Clickable UI Under Pointer";
        internal const string MenuPath = "MxFramework/Editor/Locate Clickable UI Under Pointer";

        private static object _eventSystem;
        private static bool _shortcutWasPressed;
        private static double _lastInvocationTime = double.NegativeInfinity;
        private static object _pointerEventData;
        private static IList _raycastResults;

        static PlayModeUiHierarchyLocator()
        {
            EditorApplication.update += PollPlayModeShortcut;
            InputSystem.onAfterUpdate += OnInputSystemAfterUpdate;
        }

        [Shortcut(ShortcutId, KeyCode.G, ShortcutModifiers.Control)]
        private static void LocateFromShortcutManager()
        {
            InvokeLocator();
        }

        [MenuItem(MenuPath, priority = 20)]
        private static void LocateFromMenu()
        {
            InvokeLocator();
        }

        private static void PollPlayModeShortcut()
        {
            if (!EditorApplication.isPlaying)
            {
                _shortcutWasPressed = false;
                return;
            }

            bool pressed = IsCtrlGPressed();
            if (pressed && !_shortcutWasPressed)
                InvokeLocator();

            _shortcutWasPressed = pressed;
        }

        private static void OnInputSystemAfterUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null
                && keyboard.gKey.wasPressedThisFrame
                && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed))
                InvokeLocator();
        }

        private static bool IsCtrlGPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return keyboard.gKey.isPressed
                    && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
            }

            try
            {
                return UnityEngine.Input.GetKey(KeyCode.G)
                    && (UnityEngine.Input.GetKey(KeyCode.LeftControl)
                        || UnityEngine.Input.GetKey(KeyCode.RightControl));
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static void InvokeLocator()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - _lastInvocationTime < 0.1d)
                return;

            _lastInvocationTime = currentTime;
            LocateClickableUiUnderPointer();
        }

        private static void LocateClickableUiUnderPointer()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("MxFramework UI locator is available in Play Mode only.");
                return;
            }

            if (FairyGuiBridge.TryFindClickableTarget(out GameObject fairyGuiTarget, out string fairyGuiName))
            {
                SelectInHierarchy(fairyGuiTarget, fairyGuiName);
                return;
            }

            if (!TryReadPointerPosition(out Vector2 pointerPosition))
            {
                Debug.LogWarning("MxFramework UI locator could not read the current pointer position.");
                return;
            }

            if (!UGuiBridge.IsAvailable)
            {
                Debug.Log(
                    "MxFramework UI locator found no clickable FairyGUI object. "
                    + "uGUI and EventSystem pointer lookup require the optional com.unity.ugui package.");
                return;
            }

            object eventSystem = UGuiBridge.GetCurrentEventSystem();
            if (eventSystem == null)
            {
                Debug.Log("MxFramework UI locator found no active EventSystem or clickable FairyGUI object.");
                return;
            }

            if (_pointerEventData == null || _eventSystem != eventSystem)
            {
                _eventSystem = eventSystem;
                _pointerEventData = UGuiBridge.CreatePointerEventData(eventSystem);
            }

            _raycastResults ??= UGuiBridge.CreateRaycastResultList();
            try
            {
                UGuiBridge.PreparePointerEventData(_pointerEventData, pointerPosition);
                _raycastResults.Clear();
                UGuiBridge.RaycastAll(eventSystem, _pointerEventData, _raycastResults);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"MxFramework UI locator could not query the active EventSystem: "
                    + exception.GetBaseException().Message);
                return;
            }

            GameObject target = FindFirstClickableTarget(_raycastResults);
            if (target == null)
            {
                Debug.Log("MxFramework UI locator found no clickable UI below the pointer.");
                return;
            }

            SelectInHierarchy(target, target.name);
        }

        internal static GameObject FindFirstClickableTarget(IList results)
        {
            if (results == null)
                return null;

            for (int i = 0; i < results.Count; i++)
            {
                if (!UGuiBridge.TryReadRaycastResult(
                        results[i],
                        out GameObject hit,
                        out bool isUiToolkitPanelHit))
                    continue;

                GameObject clickHandler = UGuiBridge.GetPointerClickHandler(hit);
                if (clickHandler != null && clickHandler.activeInHierarchy)
                    return clickHandler;

                if (isUiToolkitPanelHit
                    || hit.GetComponent<UIDocument>() != null
                    || hit.GetComponentInParent<UIDocument>() != null)
                    return hit;
            }

            return null;
        }

        private static bool TryReadPointerPosition(out Vector2 position)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            try
            {
                position = UnityEngine.Input.mousePosition;
                return true;
            }
            catch (InvalidOperationException)
            {
                position = default;
                return false;
            }
        }

        private static void SelectInHierarchy(GameObject target, string displayName)
        {
            if (target == null)
                return;

            GameObject hierarchyTarget = ResolveVisibleHierarchyTarget(target);
            Selection.activeGameObject = hierarchyTarget;
            EditorGUIUtility.PingObject(hierarchyTarget);
            Debug.Log($"MxFramework UI locator selected '{displayName}'.", hierarchyTarget);
        }

        private static GameObject ResolveVisibleHierarchyTarget(GameObject target)
        {
            Transform current = target.transform;
            while (current != null)
            {
                if ((current.gameObject.hideFlags & HideFlags.HideInHierarchy) == 0)
                    return current.gameObject;

                current = current.parent;
            }

            return target;
        }

        private static class UGuiBridge
        {
            private const string AssemblyName = "UnityEngine.UI";

            private static readonly Type EventSystemType;
            private static readonly Type PointerEventDataType;
            private static readonly Type RaycastResultType;
            private static readonly Type RaycastResultListType;
            private static readonly Type PanelRaycasterType;
            private static readonly PropertyInfo EventSystemCurrentProperty;
            private static readonly ConstructorInfo PointerEventDataConstructor;
            private static readonly MethodInfo PointerEventDataResetMethod;
            private static readonly PropertyInfo PointerEventDataPositionProperty;
            private static readonly MethodInfo EventSystemRaycastAllMethod;
            private static readonly PropertyInfo RaycastResultGameObjectProperty;
            private static readonly FieldInfo RaycastResultModuleField;
            private static readonly MethodInfo GetEventHandlerMethod;

            static UGuiBridge()
            {
                EventSystemType = Type.GetType(
                    $"UnityEngine.EventSystems.EventSystem, {AssemblyName}");
                PointerEventDataType = Type.GetType(
                    $"UnityEngine.EventSystems.PointerEventData, {AssemblyName}");
                RaycastResultType = Type.GetType(
                    $"UnityEngine.EventSystems.RaycastResult, {AssemblyName}");
                Type executeEventsType = Type.GetType(
                    $"UnityEngine.EventSystems.ExecuteEvents, {AssemblyName}");
                Type pointerClickHandlerType = Type.GetType(
                    $"UnityEngine.EventSystems.IPointerClickHandler, {AssemblyName}");
                PanelRaycasterType = Type.GetType(
                    $"UnityEngine.UIElements.PanelRaycaster, {AssemblyName}");

                EventSystemCurrentProperty = EventSystemType?.GetProperty(
                    "current",
                    BindingFlags.Public | BindingFlags.Static);
                PointerEventDataPositionProperty = PointerEventDataType?.GetProperty(
                    "position",
                    BindingFlags.Public | BindingFlags.Instance);
                PointerEventDataResetMethod = PointerEventDataType?.GetMethod(
                    "Reset",
                    BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);
                RaycastResultGameObjectProperty = RaycastResultType?.GetProperty(
                    "gameObject",
                    BindingFlags.Public | BindingFlags.Instance);
                RaycastResultModuleField = RaycastResultType?.GetField(
                    "module",
                    BindingFlags.Public | BindingFlags.Instance);

                if (EventSystemType != null && PointerEventDataType != null)
                {
                    PointerEventDataConstructor = PointerEventDataType.GetConstructor(
                        new[] { EventSystemType });
                }

                if (RaycastResultType != null)
                    RaycastResultListType = typeof(List<>).MakeGenericType(RaycastResultType);

                if (EventSystemType != null
                    && PointerEventDataType != null
                    && RaycastResultListType != null)
                {
                    EventSystemRaycastAllMethod = EventSystemType.GetMethod(
                        "RaycastAll",
                        BindingFlags.Public | BindingFlags.Instance,
                        binder: null,
                        types: new[] { PointerEventDataType, RaycastResultListType },
                        modifiers: null);
                }

                if (executeEventsType == null || pointerClickHandlerType == null)
                    return;

                MethodInfo[] methods = executeEventsType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name != "GetEventHandler" || !method.IsGenericMethodDefinition)
                        continue;

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 1 || parameters[0].ParameterType != typeof(GameObject))
                        continue;

                    GetEventHandlerMethod = method.MakeGenericMethod(pointerClickHandlerType);
                    break;
                }
            }

            internal static bool IsAvailable => EventSystemCurrentProperty != null
                && PointerEventDataConstructor != null
                && PointerEventDataResetMethod != null
                && PointerEventDataPositionProperty != null
                && EventSystemRaycastAllMethod != null
                && RaycastResultListType != null
                && RaycastResultGameObjectProperty != null
                && RaycastResultModuleField != null
                && GetEventHandlerMethod != null;

            internal static object GetCurrentEventSystem()
            {
                return EventSystemCurrentProperty?.GetValue(null);
            }

            internal static object CreatePointerEventData(object eventSystem)
            {
                return PointerEventDataConstructor.Invoke(new[] { eventSystem });
            }

            internal static IList CreateRaycastResultList()
            {
                return (IList)Activator.CreateInstance(RaycastResultListType);
            }

            internal static void PreparePointerEventData(object eventData, Vector2 position)
            {
                PointerEventDataResetMethod.Invoke(eventData, null);
                PointerEventDataPositionProperty.SetValue(eventData, position);
            }

            internal static void RaycastAll(object eventSystem, object eventData, IList results)
            {
                EventSystemRaycastAllMethod.Invoke(eventSystem, new object[] { eventData, results });
            }

            internal static bool TryReadRaycastResult(
                object result,
                out GameObject hit,
                out bool isUiToolkitPanelHit)
            {
                hit = null;
                isUiToolkitPanelHit = false;
                if (result == null || !RaycastResultType.IsInstanceOfType(result))
                    return false;

                hit = RaycastResultGameObjectProperty.GetValue(result) as GameObject;
                object module = RaycastResultModuleField.GetValue(result);
                isUiToolkitPanelHit = module != null
                    && PanelRaycasterType != null
                    && PanelRaycasterType.IsInstanceOfType(module);
                return hit != null;
            }

            internal static GameObject GetPointerClickHandler(GameObject hit)
            {
                return GetEventHandlerMethod.Invoke(null, new object[] { hit }) as GameObject;
            }
        }

        private static class FairyGuiBridge
        {
            private static readonly PropertyInfo StageInstanceProperty;
            private static readonly PropertyInfo StageTouchPositionProperty;
            private static readonly MethodInfo StageHitTestMethod;
            private static readonly FieldInfo DisplayOwnerField;
            private static readonly PropertyInfo ObjectParentProperty;
            private static readonly PropertyInfo ObjectTouchableProperty;
            private static readonly PropertyInfo ObjectEnabledProperty;
            private static readonly PropertyInfo ObjectVisibleProperty;
            private static readonly PropertyInfo ObjectOnClickProperty;
            private static readonly PropertyInfo EventListenerIsEmptyProperty;
            private static readonly PropertyInfo ObjectDisplayObjectProperty;
            private static readonly PropertyInfo DisplayGameObjectProperty;
            private static readonly Type ButtonType;

            static FairyGuiBridge()
            {
                Type stageType = Type.GetType("FairyGUI.Stage, FairyGUI");
                Type displayType = Type.GetType("FairyGUI.DisplayObject, FairyGUI");
                Type objectType = Type.GetType("FairyGUI.GObject, FairyGUI");
                Type listenerType = Type.GetType("FairyGUI.EventListener, FairyGUI");
                ButtonType = Type.GetType("FairyGUI.GButton, FairyGUI");

                StageInstanceProperty = stageType?.GetProperty("inst", BindingFlags.Public | BindingFlags.Static);
                StageTouchPositionProperty = stageType?.GetProperty(
                    "touchPosition",
                    BindingFlags.Public | BindingFlags.Instance);
                StageHitTestMethod = stageType?.GetMethod(
                    "HitTest",
                    BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    types: new[] { typeof(Vector2), typeof(bool) },
                    modifiers: null);
                DisplayOwnerField = displayType?.GetField("gOwner", BindingFlags.Public | BindingFlags.Instance);
                DisplayGameObjectProperty = displayType?.GetProperty(
                    "gameObject",
                    BindingFlags.Public | BindingFlags.Instance);
                ObjectParentProperty = objectType?.GetProperty("parent", BindingFlags.Public | BindingFlags.Instance);
                ObjectTouchableProperty = objectType?.GetProperty(
                    "touchable",
                    BindingFlags.Public | BindingFlags.Instance);
                ObjectEnabledProperty = objectType?.GetProperty(
                    "enabled",
                    BindingFlags.Public | BindingFlags.Instance);
                ObjectVisibleProperty = objectType?.GetProperty(
                    "visible",
                    BindingFlags.Public | BindingFlags.Instance);
                ObjectOnClickProperty = objectType?.GetProperty(
                    "onClick",
                    BindingFlags.Public | BindingFlags.Instance);
                ObjectDisplayObjectProperty = objectType?.GetProperty(
                    "displayObject",
                    BindingFlags.Public | BindingFlags.Instance);
                EventListenerIsEmptyProperty = listenerType?.GetProperty(
                    "isEmpty",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            internal static bool TryFindClickableTarget(out GameObject target, out string displayName)
            {
                target = null;
                displayName = null;
                object stage = StageInstanceProperty?.GetValue(null);
                if (stage == null
                    || StageTouchPositionProperty?.GetValue(stage) is not Vector2 stagePoint
                    || StageHitTestMethod == null)
                    return false;

                object display = StageHitTestMethod.Invoke(stage, new object[] { stagePoint, true });
                object current = display != null ? DisplayOwnerField?.GetValue(display) : null;
                object firstTouchable = current;
                while (current != null)
                {
                    if (IsClickable(current))
                        return TryResolveGameObject(current, out target, out displayName);

                    current = ObjectParentProperty?.GetValue(current);
                }

                return firstTouchable != null
                    && TryResolveGameObject(firstTouchable, out target, out displayName);
            }

            private static bool IsClickable(object value)
            {
                if (ObjectTouchableProperty?.GetValue(value) is false
                    || ObjectEnabledProperty?.GetValue(value) is false
                    || ObjectVisibleProperty?.GetValue(value) is false)
                    return false;

                if (ButtonType != null && ButtonType.IsInstanceOfType(value))
                    return true;

                object listener = ObjectOnClickProperty?.GetValue(value);
                return listener != null
                    && EventListenerIsEmptyProperty?.GetValue(listener) is false;
            }

            private static bool TryResolveGameObject(
                object value,
                out GameObject target,
                out string displayName)
            {
                object display = ObjectDisplayObjectProperty?.GetValue(value);
                target = display != null
                    ? DisplayGameObjectProperty?.GetValue(display) as GameObject
                    : null;
                displayName = target != null ? target.name : value.ToString();
                return target != null;
            }
        }
    }
}
