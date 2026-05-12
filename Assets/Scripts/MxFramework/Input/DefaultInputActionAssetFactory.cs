using UnityEngine;
using UnityEngine.InputSystem;

namespace MxFramework.Input
{
    public static class DefaultInputActionAssetFactory
    {
        public static InputActionAsset Create()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.AddActionMap(CreateGameplayMap());
            asset.AddActionMap(CreateUiMap());
            asset.AddActionMap(CreateDebugMap());
            return asset;
        }

        private static InputActionMap CreateGameplayMap()
        {
            var map = new InputActionMap(InputActionNames.GameplayMap);
            InputAction move = map.AddAction(InputActionNames.Move, InputActionType.Value, expectedControlLayout: "Vector2");
            AddMoveBindings(move);
            map.AddAction(InputActionNames.Look, InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2")
                .AddBinding("<Gamepad>/rightStick");
            AddButton(map, InputActionNames.Jump, "<Keyboard>/space", "<Gamepad>/buttonSouth");
            AddButton(map, InputActionNames.AttackPrimary, "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            AddButton(map, InputActionNames.AttackSecondary, "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            AddButton(map, InputActionNames.Interact, "<Keyboard>/e", "<Gamepad>/buttonWest");
            AddButton(map, InputActionNames.Dodge, "<Keyboard>/leftCtrl", "<Gamepad>/buttonEast");
            AddButton(map, InputActionNames.Sprint, "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            AddButton(map, InputActionNames.Pause, "<Keyboard>/escape", "<Keyboard>/p", "<Gamepad>/start");
            map.AddAction(InputActionNames.Point, InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2")
                .AddBinding("<Touchscreen>/primaryTouch/position");
            AddButton(map, InputActionNames.Click, "<Mouse>/leftButton", "<Touchscreen>/primaryTouch/press");
            AddButton(map, InputActionNames.RightClick, "<Mouse>/rightButton");
            map.AddAction(InputActionNames.ScrollWheel, InputActionType.PassThrough, "<Mouse>/scroll", expectedControlLayout: "Vector2");
            return map;
        }

        private static InputActionMap CreateUiMap()
        {
            var map = new InputActionMap(InputActionNames.UIMap);
            InputAction navigate = map.AddAction(InputActionNames.Navigate, InputActionType.Value, expectedControlLayout: "Vector2");
            AddMoveBindings(navigate);
            navigate.AddBinding("<Gamepad>/dpad");
            AddButton(map, InputActionNames.Submit, "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            AddButton(map, InputActionNames.Cancel, "<Keyboard>/escape", "<Gamepad>/buttonEast");
            map.AddAction(InputActionNames.Point, InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2")
                .AddBinding("<Touchscreen>/primaryTouch/position");
            AddButton(map, InputActionNames.Click, "<Mouse>/leftButton", "<Touchscreen>/primaryTouch/press");
            AddButton(map, InputActionNames.RightClick, "<Mouse>/rightButton");
            map.AddAction(InputActionNames.ScrollWheel, InputActionType.PassThrough, "<Mouse>/scroll", expectedControlLayout: "Vector2");
            return map;
        }

        private static InputActionMap CreateDebugMap()
        {
            var map = new InputActionMap(InputActionNames.DebugMap);
            AddButton(map, InputActionNames.ToggleConsole, "<Keyboard>/backquote");
            AddButton(map, InputActionNames.Restart, "<Keyboard>/r");
            AddButton(map, InputActionNames.DebugPrimary, "<Keyboard>/j");
            AddButton(map, InputActionNames.DebugSecondary, "<Keyboard>/p");
            AddButton(map, InputActionNames.DebugCycle, "<Keyboard>/q", "<Keyboard>/x");
            AddButton(map, InputActionNames.DebugStep, "<Keyboard>/t");
            AddButton(map, InputActionNames.ToggleHud, "<Keyboard>/h");
            AddButton(map, InputActionNames.AudioPrimary, "<Keyboard>/1");
            AddButton(map, InputActionNames.AudioSecondary, "<Keyboard>/2");
            return map;
        }

        private static void AddMoveBindings(InputAction action)
        {
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            action.AddBinding("<Gamepad>/leftStick");
        }

        private static InputAction AddButton(InputActionMap map, string name, params string[] bindings)
        {
            InputAction action = map.AddAction(name, InputActionType.Button, expectedControlLayout: "Button");
            for (int i = 0; i < bindings.Length; i++)
            {
                action.AddBinding(bindings[i]);
            }

            return action;
        }
    }
}
