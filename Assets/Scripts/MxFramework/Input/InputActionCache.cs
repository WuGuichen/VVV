using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MxFramework.Input
{
    internal sealed class InputActionCache
    {
        private readonly List<InputContext> _enabledContexts = new List<InputContext>(4);
        private InputActionAsset _asset;

        private InputActionMap _gameplay;
        private InputActionMap _ui;
        private InputActionMap _vehicle;
        private InputActionMap _photoMode;
        private InputActionMap _cutscene;
        private InputActionMap _debug;

        private InputAction _move;
        private InputAction _look;
        private InputAction _jump;
        private InputAction _attackPrimary;
        private InputAction _attackSecondary;
        private InputAction _interact;
        private InputAction _dodge;
        private InputAction _sprint;
        private InputAction _pause;
        private InputAction _gameplayPoint;
        private InputAction _gameplayClick;
        private InputAction _gameplayRightClick;
        private InputAction _gameplayScrollWheel;

        private InputAction _navigate;
        private InputAction _submit;
        private InputAction _cancel;
        private InputAction _point;
        private InputAction _click;
        private InputAction _rightClick;
        private InputAction _scrollWheel;

        private InputAction _steer;
        private InputAction _throttle;
        private InputAction _brake;
        private InputAction _exitVehicle;

        private InputAction _orbit;
        private InputAction _zoom;
        private InputAction _takeShot;
        private InputAction _exitPhotoMode;

        private InputAction _skip;
        private InputAction _continue;

        private InputAction _toggleConsole;
        private InputAction _restart;
        private InputAction _debugPrimary;
        private InputAction _debugSecondary;
        private InputAction _debugCycle;
        private InputAction _debugStep;
        private InputAction _toggleHud;
        private InputAction _audioPrimary;
        private InputAction _audioSecondary;

        public InputActionAsset Asset => _asset;

        public void Bind(InputActionAsset asset)
        {
            _asset = asset;
            if (_asset == null)
            {
                Clear();
                return;
            }

            _gameplay = FindMap(InputActionNames.GameplayMap);
            _ui = FindMap(InputActionNames.UIMap);
            _vehicle = FindMap(InputActionNames.VehicleMap);
            _photoMode = FindMap(InputActionNames.PhotoModeMap);
            _cutscene = FindMap(InputActionNames.CutsceneMap);
            _debug = FindMap(InputActionNames.DebugMap);

            _move = FindAction(_gameplay, InputActionNames.Move);
            _look = FindAction(_gameplay, InputActionNames.Look);
            _jump = FindAction(_gameplay, InputActionNames.Jump);
            _attackPrimary = FindAction(_gameplay, InputActionNames.AttackPrimary);
            _attackSecondary = FindAction(_gameplay, InputActionNames.AttackSecondary);
            _interact = FindAction(_gameplay, InputActionNames.Interact);
            _dodge = FindAction(_gameplay, InputActionNames.Dodge);
            _sprint = FindAction(_gameplay, InputActionNames.Sprint);
            _pause = FindAction(_gameplay, InputActionNames.Pause);
            _gameplayPoint = FindAction(_gameplay, InputActionNames.Point);
            _gameplayClick = FindAction(_gameplay, InputActionNames.Click);
            _gameplayRightClick = FindAction(_gameplay, InputActionNames.RightClick);
            _gameplayScrollWheel = FindAction(_gameplay, InputActionNames.ScrollWheel);

            _navigate = FindAction(_ui, InputActionNames.Navigate);
            _submit = FindAction(_ui, InputActionNames.Submit);
            _cancel = FindAction(_ui, InputActionNames.Cancel);
            _point = FindAction(_ui, InputActionNames.Point);
            _click = FindAction(_ui, InputActionNames.Click);
            _rightClick = FindAction(_ui, InputActionNames.RightClick);
            _scrollWheel = FindAction(_ui, InputActionNames.ScrollWheel);

            _steer = FindAction(_vehicle, InputActionNames.Steer);
            _throttle = FindAction(_vehicle, InputActionNames.Throttle);
            _brake = FindAction(_vehicle, InputActionNames.Brake);
            _exitVehicle = FindAction(_vehicle, InputActionNames.ExitVehicle);

            _orbit = FindAction(_photoMode, InputActionNames.Orbit);
            _zoom = FindAction(_photoMode, InputActionNames.Zoom);
            _takeShot = FindAction(_photoMode, InputActionNames.TakeShot);
            _exitPhotoMode = FindAction(_photoMode, InputActionNames.Exit);

            _skip = FindAction(_cutscene, InputActionNames.Skip);
            _continue = FindAction(_cutscene, InputActionNames.Continue);

            _toggleConsole = FindAction(_debug, InputActionNames.ToggleConsole);
            _restart = FindAction(_debug, InputActionNames.Restart);
            _debugPrimary = FindAction(_debug, InputActionNames.DebugPrimary);
            _debugSecondary = FindAction(_debug, InputActionNames.DebugSecondary);
            _debugCycle = FindAction(_debug, InputActionNames.DebugCycle);
            _debugStep = FindAction(_debug, InputActionNames.DebugStep);
            _toggleHud = FindAction(_debug, InputActionNames.ToggleHud);
            _audioPrimary = FindAction(_debug, InputActionNames.AudioPrimary);
            _audioSecondary = FindAction(_debug, InputActionNames.AudioSecondary);
        }

        public void ApplyContexts(InputContextStack stack)
        {
            DisableAllMaps();
            if (_asset == null || stack == null)
            {
                return;
            }

            stack.FillEnabledContexts(_enabledContexts);
            for (int i = 0; i < _enabledContexts.Count; i++)
            {
                InputActionMap map = GetMap(_enabledContexts[i]);
                if (map != null)
                {
                    map.Enable();
                }
            }
        }

        public void DisableAllMaps()
        {
            DisableMap(_gameplay);
            DisableMap(_ui);
            DisableMap(_vehicle);
            DisableMap(_photoMode);
            DisableMap(_cutscene);
            DisableMap(_debug);
        }

        public InputSnapshot ReadSnapshot()
        {
            Vector2 move = ReadVector2(_move);
            Vector2 look = ReadVector2(_look);
            Vector2 navigate = ReadVector2(_navigate);
            Vector2 point = ReadVector2Any(_point, _gameplayPoint);
            Vector2 scroll = ReadVector2Any(_scrollWheel, _gameplayScrollWheel);
            float throttle = ReadFloat(_throttle) - ReadFloat(_brake);

            return new InputSnapshot(
                move,
                look,
                navigate,
                point,
                scroll,
                throttle,
                WasPressed(_jump),
                IsPressed(_jump),
                WasReleased(_jump),
                WasPressed(_attackPrimary),
                IsPressed(_attackPrimary),
                WasPressed(_attackSecondary),
                WasPressed(_interact),
                WasPressed(_dodge),
                IsPressed(_sprint),
                WasPressed(_submit),
                WasPressed(_cancel),
                WasPressed(_pause),
                WasPressed(_toggleConsole),
                WasPressedAny(_click, _gameplayClick),
                IsPressedAny(_click, _gameplayClick),
                WasReleasedAny(_click, _gameplayClick),
                WasPressedAny(_rightClick, _gameplayRightClick),
                IsPressedAny(_rightClick, _gameplayRightClick),
                WasReleasedAny(_rightClick, _gameplayRightClick),
                WasPressed(_restart),
                WasPressed(_debugPrimary),
                WasPressed(_debugSecondary),
                WasPressed(_debugCycle),
                WasPressed(_debugStep),
                WasPressed(_toggleHud),
                WasPressed(_audioPrimary),
                WasPressed(_audioSecondary));
        }

        public void EnqueuePressedCommands(InputCommandQueue commands, long frame, int sourceId)
        {
            if (commands == null)
            {
                return;
            }

            EnqueueIfPressed(commands, frame, sourceId, _jump, InputIntent.Jump);
            EnqueueIfReleased(commands, frame, sourceId, _jump, InputIntent.Jump);
            EnqueueIfPressed(commands, frame, sourceId, _attackPrimary, InputIntent.AttackPrimary);
            EnqueueIfPressed(commands, frame, sourceId, _attackSecondary, InputIntent.AttackSecondary);
            EnqueueIfPressed(commands, frame, sourceId, _interact, InputIntent.Interact);
            EnqueueIfPressed(commands, frame, sourceId, _dodge, InputIntent.Dodge);
            EnqueueIfPressed(commands, frame, sourceId, _pause, InputIntent.Pause);
            EnqueueIfPressed(commands, frame, sourceId, _submit, InputIntent.Submit);
            EnqueueIfPressed(commands, frame, sourceId, _cancel, InputIntent.Cancel);
            EnqueueIfPressedAny(commands, frame, sourceId, _click, _gameplayClick, InputIntent.Click, ReadVector2Any(_point, _gameplayPoint));
            EnqueueIfPressedAny(commands, frame, sourceId, _rightClick, _gameplayRightClick, InputIntent.RightClick, ReadVector2Any(_point, _gameplayPoint));
            EnqueueIfPressed(commands, frame, sourceId, _exitVehicle, InputIntent.ExitVehicle);
            EnqueueIfPressed(commands, frame, sourceId, _takeShot, InputIntent.TakeShot);
            EnqueueIfPressed(commands, frame, sourceId, _exitPhotoMode, InputIntent.ExitPhotoMode);
            EnqueueIfPressed(commands, frame, sourceId, _skip, InputIntent.SkipCutscene);
            EnqueueIfPressed(commands, frame, sourceId, _continue, InputIntent.ContinueCutscene);
            EnqueueIfPressed(commands, frame, sourceId, _toggleConsole, InputIntent.ToggleConsole);
            EnqueueIfPressed(commands, frame, sourceId, _restart, InputIntent.Restart);
            EnqueueIfPressed(commands, frame, sourceId, _debugPrimary, InputIntent.DebugPrimary);
            EnqueueIfPressed(commands, frame, sourceId, _debugSecondary, InputIntent.DebugSecondary);
            EnqueueIfPressed(commands, frame, sourceId, _debugCycle, InputIntent.DebugCycle);
            EnqueueIfPressed(commands, frame, sourceId, _debugStep, InputIntent.DebugStep);
            EnqueueIfPressed(commands, frame, sourceId, _toggleHud, InputIntent.ToggleHud);
            EnqueueIfPressed(commands, frame, sourceId, _audioPrimary, InputIntent.AudioPrimary);
            EnqueueIfPressed(commands, frame, sourceId, _audioSecondary, InputIntent.AudioSecondary);
        }

        private void Clear()
        {
            _gameplay = null;
            _ui = null;
            _vehicle = null;
            _photoMode = null;
            _cutscene = null;
            _debug = null;
        }

        private InputActionMap FindMap(string name)
        {
            return _asset != null ? _asset.FindActionMap(name, false) : null;
        }

        private static InputAction FindAction(InputActionMap map, string name)
        {
            return map != null ? map.FindAction(name, false) : null;
        }

        private InputActionMap GetMap(InputContext context)
        {
            switch (context)
            {
                case InputContext.Gameplay:
                    return _gameplay;
                case InputContext.UI:
                case InputContext.Rebinding:
                    return _ui;
                case InputContext.Vehicle:
                    return _vehicle;
                case InputContext.PhotoMode:
                    return _photoMode;
                case InputContext.Cutscene:
                    return _cutscene;
                case InputContext.Debug:
                    return _debug;
                default:
                    return null;
            }
        }

        private static void DisableMap(InputActionMap map)
        {
            if (map != null)
            {
                map.Disable();
            }
        }

        private static Vector2 ReadVector2(InputAction action)
        {
            return action != null && action.enabled ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        private static Vector2 ReadVector2Any(InputAction first, InputAction second)
        {
            if (first != null && first.enabled)
            {
                return first.ReadValue<Vector2>();
            }

            return second != null && second.enabled ? second.ReadValue<Vector2>() : Vector2.zero;
        }

        private static float ReadFloat(InputAction action)
        {
            return action != null && action.enabled ? action.ReadValue<float>() : 0f;
        }

        private static bool WasPressed(InputAction action)
        {
            return action != null && action.enabled && action.WasPressedThisFrame();
        }

        private static bool WasPressedAny(InputAction first, InputAction second)
        {
            return WasPressed(first) || WasPressed(second);
        }

        private static bool WasReleased(InputAction action)
        {
            return action != null && action.enabled && action.WasReleasedThisFrame();
        }

        private static bool WasReleasedAny(InputAction first, InputAction second)
        {
            return WasReleased(first) || WasReleased(second);
        }

        private static bool IsPressed(InputAction action)
        {
            return action != null && action.enabled && action.IsPressed();
        }

        private static bool IsPressedAny(InputAction first, InputAction second)
        {
            return IsPressed(first) || IsPressed(second);
        }

        private static void EnqueueIfPressed(
            InputCommandQueue commands,
            long frame,
            int sourceId,
            InputAction action,
            InputIntent intent)
        {
            EnqueueIfPressed(commands, frame, sourceId, action, intent, Vector2.zero);
        }

        private static void EnqueueIfPressed(
            InputCommandQueue commands,
            long frame,
            int sourceId,
            InputAction action,
            InputIntent intent,
            Vector2 value)
        {
            if (WasPressed(action))
            {
                commands.TryEnqueue(new InputCommand(frame, sourceId, intent, InputCommandPhase.Pressed, value), out _);
            }
        }

        private static void EnqueueIfPressedAny(
            InputCommandQueue commands,
            long frame,
            int sourceId,
            InputAction first,
            InputAction second,
            InputIntent intent,
            Vector2 value)
        {
            if (WasPressedAny(first, second))
            {
                commands.TryEnqueue(new InputCommand(frame, sourceId, intent, InputCommandPhase.Pressed, value), out _);
            }
        }

        private static void EnqueueIfReleased(
            InputCommandQueue commands,
            long frame,
            int sourceId,
            InputAction action,
            InputIntent intent)
        {
            if (WasReleased(action))
            {
                commands.TryEnqueue(new InputCommand(frame, sourceId, intent, InputCommandPhase.Released), out _);
            }
        }
    }
}
