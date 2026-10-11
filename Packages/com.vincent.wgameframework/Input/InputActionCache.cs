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
        private InputAction _targetLock;
        private InputAction _cameraModeNext;
        private InputAction _guidancePrevious;
        private InputAction _guidanceNext;
        private InputAction _gameplayPoint;
        private InputAction _gameplayClick;
        private InputAction _gameplayRightClick;
        private InputAction _gameplayMiddleClick;
        private InputAction _gameplayScrollWheel;
        private readonly InputAction[] _skillSlots = new InputAction[InputActionNames.SkillSlots.Length];

        private InputAction _navigate;
        private InputAction _submit;
        private InputAction _cancel;
        private InputAction _point;
        private InputAction _click;
        private InputAction _rightClick;
        private InputAction _middleClick;
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
        internal int MapStateChangeCountForTests { get; private set; }

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
            _targetLock = FindAction(_gameplay, InputActionNames.TargetLock);
            _cameraModeNext = FindAction(_gameplay, InputActionNames.CameraModeNext);
            _guidancePrevious = FindAction(_gameplay, InputActionNames.GuidancePrevious);
            _guidanceNext = FindAction(_gameplay, InputActionNames.GuidanceNext);
            _gameplayPoint = FindAction(_gameplay, InputActionNames.Point);
            _gameplayClick = FindAction(_gameplay, InputActionNames.Click);
            _gameplayRightClick = FindAction(_gameplay, InputActionNames.RightClick);
            _gameplayMiddleClick = FindAction(_gameplay, InputActionNames.MiddleClick);
            _gameplayScrollWheel = FindAction(_gameplay, InputActionNames.ScrollWheel);
            for (int i = 0; i < _skillSlots.Length; i++)
                _skillSlots[i] = FindAction(_gameplay, InputActionNames.SkillSlots[i]);

            _navigate = FindAction(_ui, InputActionNames.Navigate);
            _submit = FindAction(_ui, InputActionNames.Submit);
            _cancel = FindAction(_ui, InputActionNames.Cancel);
            _point = FindAction(_ui, InputActionNames.Point);
            _click = FindAction(_ui, InputActionNames.Click);
            _rightClick = FindAction(_ui, InputActionNames.RightClick);
            _middleClick = FindAction(_ui, InputActionNames.MiddleClick);
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
            if (_asset == null || stack == null)
            {
                DisableAllMaps();
                return;
            }

            stack.FillEnabledContexts(_enabledContexts);
            bool gameplayEnabled = false;
            bool uiEnabled = false;
            bool vehicleEnabled = false;
            bool photoModeEnabled = false;
            bool cutsceneEnabled = false;
            bool debugEnabled = false;

            for (int i = 0; i < _enabledContexts.Count; i++)
            {
                switch (_enabledContexts[i])
                {
                    case InputContext.Gameplay:
                        gameplayEnabled = true;
                        break;
                    case InputContext.UI:
                    case InputContext.Rebinding:
                        uiEnabled = true;
                        break;
                    case InputContext.Vehicle:
                        vehicleEnabled = true;
                        break;
                    case InputContext.PhotoMode:
                        photoModeEnabled = true;
                        break;
                    case InputContext.Cutscene:
                        cutsceneEnabled = true;
                        break;
                    case InputContext.Debug:
                        debugEnabled = true;
                        break;
                }
            }

            ApplyMapState(_gameplay, gameplayEnabled);
            ApplyMapState(_ui, uiEnabled);
            ApplyMapState(_vehicle, vehicleEnabled);
            ApplyMapState(_photoMode, photoModeEnabled);
            ApplyMapState(_cutscene, cutsceneEnabled);
            ApplyMapState(_debug, debugEnabled);
        }

        public void DisableAllMaps()
        {
            ApplyMapState(_gameplay, false);
            ApplyMapState(_ui, false);
            ApplyMapState(_vehicle, false);
            ApplyMapState(_photoMode, false);
            ApplyMapState(_cutscene, false);
            ApplyMapState(_debug, false);
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
                WasPressedAny(_middleClick, _gameplayMiddleClick),
                IsPressedAny(_middleClick, _gameplayMiddleClick),
                WasReleasedAny(_middleClick, _gameplayMiddleClick),
                WasPressed(_restart),
                WasPressed(_debugPrimary),
                WasPressed(_debugSecondary),
                WasPressed(_debugCycle),
                WasPressed(_debugStep),
                WasPressed(_toggleHud),
                WasPressed(_audioPrimary),
                WasPressed(_audioSecondary),
                ReadSkillSlotPressedMask(),
                WasPressed(_cameraModeNext),
                WasPressed(_targetLock));
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
            EnqueueIfPressed(commands, frame, sourceId, _guidancePrevious, InputIntent.GuidancePrevious);
            EnqueueIfPressed(commands, frame, sourceId, _guidanceNext, InputIntent.GuidanceNext);
            for (int i = 0; i < _skillSlots.Length; i++)
                EnqueueIfPressed(commands, frame, sourceId, _skillSlots[i], ToSkillSlotIntent(i));
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
            for (int i = 0; i < _skillSlots.Length; i++)
                _skillSlots[i] = null;
        }

        private InputActionMap FindMap(string name)
        {
            return _asset != null ? _asset.FindActionMap(name, false) : null;
        }

        private static InputAction FindAction(InputActionMap map, string name)
        {
            return map != null ? map.FindAction(name, false) : null;
        }

        private void ApplyMapState(InputActionMap map, bool enabled)
        {
            if (map == null || map.enabled == enabled)
                return;

            if (enabled)
            {
                map.Enable();
            }
            else
            {
                map.Disable();
            }

            MapStateChangeCountForTests++;
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

        private int ReadSkillSlotPressedMask()
        {
            int mask = 0;
            for (int i = 0; i < _skillSlots.Length; i++)
            {
                if (WasPressed(_skillSlots[i]))
                    mask |= 1 << i;
            }

            return mask;
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

        private static InputIntent ToSkillSlotIntent(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0:
                    return InputIntent.SkillSlot1;
                case 1:
                    return InputIntent.SkillSlot2;
                case 2:
                    return InputIntent.SkillSlot3;
                case 3:
                    return InputIntent.SkillSlot4;
                case 4:
                    return InputIntent.SkillSlot5;
                case 5:
                    return InputIntent.SkillSlot6;
                case 6:
                    return InputIntent.SkillSlot7;
                case 7:
                    return InputIntent.SkillSlot8;
                case 8:
                    return InputIntent.SkillSlot9;
                default:
                    return InputIntent.Unknown;
            }
        }
    }
}
