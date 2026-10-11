using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using MxFramework.Input;
using MxFramework.Runtime;
using Unity.Profiling;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestLocalInputCommandModule : RuntimeModule
    {
        private static readonly ProfilerMarker EnsureInputContextMarker =
            new ProfilerMarker("CharacterTest.LocalInput.EnsureContext");
        private static readonly ProfilerMarker ReadInputSnapshotMarker =
            new ProfilerMarker("CharacterTest.LocalInput.ReadSnapshot");
        private static readonly ProfilerMarker ReadHotbarInputMarker =
            new ProfilerMarker("CharacterTest.LocalInput.ReadHotbar");
        private static readonly ProfilerMarker SubmitControlIntentMarker =
            new ProfilerMarker("CharacterTest.LocalInput.SubmitControlIntent");
        private static readonly ProfilerMarker SubmitControlIntentPreLogMarker =
            new ProfilerMarker("CharacterTest.LocalInput.SubmitControlIntent.PreLog");
        private static readonly ProfilerMarker SubmitControlIntentDelegateMarker =
            new ProfilerMarker("CharacterTest.LocalInput.SubmitControlIntent.Delegate");
        private static readonly ProfilerMarker SubmitControlIntentResultLogMarker =
            new ProfilerMarker("CharacterTest.LocalInput.SubmitControlIntent.ResultLog");
        private static readonly ProfilerMarker SubmitAbilitySlotMarker =
            new ProfilerMarker("CharacterTest.LocalInput.SubmitAbilitySlot");

        private const int AbilitySlotTraceKind = 1014;
        private const int InputCommandInitialCapacity = 16;

        private readonly CharacterTestCharacterRuntimeModule _characterRuntimeModule;
        private readonly Func<RuntimeCommand, RuntimeCommandValidationResult> _submitCommand;
        private readonly Action _clearCommandWarmupState;
        private readonly IRuntimeLogger _logger;
        private readonly List<InputCommand> _inputCommands = new List<InputCommand>(InputCommandInitialCapacity);
        private readonly RuntimeLogBuffer _inputFlowBuffer = new RuntimeLogBuffer(256);
        private IInputProvider _inputProvider;
        private CharacterTestAbilityHotbarRuntime _abilityHotbarRuntime;
        private ICharacterTestAbilityHotbarInputSource _abilityHotbarInputSource;
        private IDisposable _gameplayInputScope;
        private string _lastSubmitFailureMessage = string.Empty;
        private long _lastInputFlowLogFrame = long.MinValue;
        private long _lastInputServiceSnapshotOneShotFrame = long.MinValue;
        private bool _lastLoggedActiveInput;
        private bool _suppressed;
        private bool _submitCommandPathWarmed;

        public CharacterTestLocalInputCommandModule(
            CharacterTestCharacterRuntimeModule characterRuntimeModule,
            Func<RuntimeCommand, RuntimeCommandValidationResult> submitCommand,
            IRuntimeLogger logger,
            IInputProvider inputProvider = null,
            CharacterTestAbilityHotbarRuntime abilityHotbarRuntime = null,
            ICharacterTestAbilityHotbarInputSource abilityHotbarInputSource = null,
            Action clearCommandWarmupState = null)
            : base("CharacterTest.LocalInputCommands", RuntimeTickStage.PreSimulation, priority: 0)
        {
            _characterRuntimeModule = characterRuntimeModule;
            _submitCommand = submitCommand ?? throw new ArgumentNullException(nameof(submitCommand));
            _clearCommandWarmupState = clearCommandWarmupState;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _inputProvider = inputProvider;
            _abilityHotbarRuntime = abilityHotbarRuntime;
            _abilityHotbarInputSource = abilityHotbarInputSource ?? CharacterTestKeyboardAbilityHotbarInputSource.Shared;
        }

        public void SetInputProvider(IInputProvider provider)
        {
            _gameplayInputScope?.Dispose();
            _gameplayInputScope = null;
            _inputProvider = provider;
            _lastInputServiceSnapshotOneShotFrame = long.MinValue;
            EnsureGameplayInputContext();
        }

        public void SetAbilityHotbarRuntime(CharacterTestAbilityHotbarRuntime abilityHotbarRuntime)
        {
            _abilityHotbarRuntime = abilityHotbarRuntime;
        }

        public void SetAbilityHotbarInputSource(ICharacterTestAbilityHotbarInputSource inputSource)
        {
            _abilityHotbarInputSource = inputSource ?? CharacterTestKeyboardAbilityHotbarInputSource.Shared;
        }

        public void SetSuppressed(bool suppressed)
        {
            _suppressed = suppressed;
        }

        public override void Start(RuntimeHostContext context)
        {
            if (_inputProvider == null)
                _inputProvider = InputProviderResolver.ResolveOrCreateDefault(null);

            EnsureGameplayInputContext();
            WarmUpSubmitCommandPath();
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            if (_suppressed)
                return;

            using (EnsureInputContextMarker.Auto())
            {
                EnsureGameplayInputContext();
            }

            IReadOnlyList<CharacterTestCharacterEntity> entities = _characterRuntimeModule != null
                ? _characterRuntimeModule.Entities
                : null;
            if (entities == null || entities.Count == 0)
                return;

            InputSnapshot snapshot;
            bool providerAvailable;
            bool gameplayContextEnabled;
            string providerType = string.Empty;
            using (ReadInputSnapshotMarker.Auto())
            {
                snapshot = ReadSnapshot(
                    out providerAvailable,
                    out gameplayContextEnabled);
            }
            if (CharacterTestInputFlowLog.Enabled)
                providerType = ResolveInputProviderTypeName(_inputProvider);
            bool canReadGameplayInput = providerAvailable
                && gameplayContextEnabled
                && !IsGameplayInputBlocked(_inputProvider);
            DrainQueuedInputCommands(canReadGameplayInput, context.FrameIndex);
            bool applySnapshotOneShots = ShouldApplySnapshotOneShots(
                canReadGameplayInput,
                context.FrameIndex);
            bool jumpPressed = applySnapshotOneShots && snapshot.JumpPressed;
            bool jumpReleased = applySnapshotOneShots && snapshot.JumpReleased;
            bool attackPrimaryPressed = applySnapshotOneShots && snapshot.AttackPrimaryPressed;
            bool attackSecondaryPressed = applySnapshotOneShots && snapshot.AttackSecondaryPressed;
            bool dodgePressed = applySnapshotOneShots && snapshot.DodgePressed;
            bool debugPrimaryPressed = applySnapshotOneShots && snapshot.DebugPrimaryPressed;
            bool debugSecondaryPressed = applySnapshotOneShots && snapshot.DebugSecondaryPressed;
            int skillSlotPressedMask = applySnapshotOneShots ? snapshot.SkillSlotPressedMask : 0;
            ApplyQueuedInputCommands(
                ref jumpPressed,
                ref jumpReleased,
                ref attackPrimaryPressed,
                ref attackSecondaryPressed,
                ref dodgePressed,
                ref debugPrimaryPressed,
                ref debugSecondaryPressed,
                ref skillSlotPressedMask);
            int abilitySlotPressed;
            using (ReadHotbarInputMarker.Auto())
            {
                abilitySlotPressed = applySnapshotOneShots
                    ? _abilityHotbarInputSource.ReadPressedSlot()
                    : 0;
            }
            var frame = new RuntimeFrame(context.FrameIndex);
            LogInputSampleIfNeeded(frame, snapshot, providerAvailable, gameplayContextEnabled, providerType);
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Control.Owner != CharacterTestCharacterControlOwner.LocalPlayer)
                    continue;

                Vector2 worldMove = _characterRuntimeModule != null
                    ? _characterRuntimeModule.ResolveControlWorldMove(entity, snapshot.Move)
                    : Vector2.ClampMagnitude(snapshot.Move, 1f);
                RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetControlIntent(
                    frame,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    entity.Id.Value,
                    snapshot.Move,
                    worldMove,
                    jumpPressed,
                    snapshot.JumpHeld,
                    jumpReleased,
                    attackPrimaryPressed,
                    attackSecondaryPressed,
                    debugSecondaryPressed: debugSecondaryPressed,
                    debugPrimaryPressed: debugPrimaryPressed,
                    skillSlotPressedMask: skillSlotPressedMask,
                    dodgePressed: dodgePressed,
                    traceId: new RuntimeTraceIdentity(CharacterTestRuntimeInputTraceKinds.LocalControlIntent, entity.Id.Value, frame.Value));
                bool logCommand = CharacterTestInputFlowLog.Enabled
                    && (CharacterTestInputFlowLog.HasActiveCommand(command)
                        || frame.Value == _lastInputFlowLogFrame);
                RuntimeCommandValidationResult result;
                using (SubmitControlIntentMarker.Auto())
                {
                    using (SubmitControlIntentPreLogMarker.Auto())
                    {
                        LogCommandSubmit("submit", command, force: logCommand);
                    }

                    using (SubmitControlIntentDelegateMarker.Auto())
                    {
                        result = _submitCommand(command);
                    }

                    if (!result.Success)
                    {
                        using (SubmitControlIntentResultLogMarker.Auto())
                        {
                            LogCommandSubmit("submit-rejected", command, result.Error.Message, logCommand);
                        }

                        LogSubmitFailureOnce(result.Error.Message);
                    }
                    else
                    {
                        using (SubmitControlIntentResultLogMarker.Auto())
                        {
                            LogCommandSubmit("submit-accepted", command, force: logCommand);
                        }

                        _lastSubmitFailureMessage = string.Empty;
                    }
                }

                using (SubmitAbilitySlotMarker.Auto())
                {
                    SubmitAbilitySlotCastIfNeeded(frame, entity, abilitySlotPressed);
                }
            }
        }

        public override void Dispose()
        {
            _gameplayInputScope?.Dispose();
            _gameplayInputScope = null;
        }

        private InputSnapshot ReadSnapshot(
            out bool providerAvailable,
            out bool gameplayContextEnabled)
        {
            providerAvailable = _inputProvider != null;
            gameplayContextEnabled = providerAvailable && _inputProvider.IsContextEnabled(InputContext.Gameplay);
            return providerAvailable && gameplayContextEnabled
                ? _inputProvider.Snapshot
                : InputSnapshot.Empty;
        }

        private static string ResolveInputProviderTypeName(IInputProvider inputProvider)
        {
            return inputProvider != null ? inputProvider.GetType().Name : "null";
        }

        private void EnsureGameplayInputContext()
        {
            if (_inputProvider == null)
            {
                _logger.Warning("CharacterTest.LocalInputCommands", "Input provider is not available.");
                return;
            }

            if (!_inputProvider.IsContextEnabled(InputContext.Gameplay))
            {
                if (IsGameplayInputBlocked(_inputProvider))
                {
                    return;
                }

                _gameplayInputScope?.Dispose();
                _gameplayInputScope = _inputProvider.PushContext(
                    InputContext.Gameplay,
                    InputContextPolicy.Overlay);
            }
        }

        private static bool IsGameplayInputBlocked(IInputProvider inputProvider)
        {
            return inputProvider.ContainsContext(InputContext.UI)
                || inputProvider.ContainsContext(InputContext.Rebinding)
                || inputProvider.ContainsContext(InputContext.Cutscene)
                || inputProvider.ContainsContext(InputContext.Vehicle)
                || inputProvider.ContainsContext(InputContext.PhotoMode);
        }

        private void DrainQueuedInputCommands(bool canReadGameplayInput, long runtimeFrame)
        {
            _inputCommands.Clear();
            InputCommandQueue commands = _inputProvider != null ? _inputProvider.Commands : null;
            if (commands == null)
                return;

            long inputFrame = ResolveInputCommandFrame(_inputProvider, runtimeFrame);
            if (inputFrame < commands.CurrentFrame)
                return;

            commands.DrainForFrame(inputFrame, _inputCommands);
            if (!canReadGameplayInput)
                _inputCommands.Clear();
        }

        private static long ResolveInputCommandFrame(IInputProvider inputProvider, long fallbackFrame)
        {
            if (inputProvider is InputService inputService)
                return inputService.LastCommandFrame;

            InputCommandQueue commands = inputProvider != null ? inputProvider.Commands : null;
            long currentFrame = commands != null ? commands.CurrentFrame : 0L;
            return Math.Max(currentFrame, fallbackFrame);
        }

        private bool ShouldApplySnapshotOneShots(bool canReadGameplayInput, long runtimeFrame)
        {
            if (!canReadGameplayInput)
                return false;

            if (!(_inputProvider is InputService))
                return true;

            long inputFrame = ResolveInputCommandFrame(_inputProvider, runtimeFrame);
            if (inputFrame == _lastInputServiceSnapshotOneShotFrame)
                return false;

            _lastInputServiceSnapshotOneShotFrame = inputFrame;
            return true;
        }

        private void ApplyQueuedInputCommands(
            ref bool jumpPressed,
            ref bool jumpReleased,
            ref bool attackPrimaryPressed,
            ref bool attackSecondaryPressed,
            ref bool dodgePressed,
            ref bool debugPrimaryPressed,
            ref bool debugSecondaryPressed,
            ref int skillSlotPressedMask)
        {
            for (int i = 0; i < _inputCommands.Count; i++)
            {
                InputCommand command = _inputCommands[i];
                if (IsPressedCommand(command.Phase))
                {
                    switch (command.Intent)
                    {
                        case InputIntent.Jump:
                            jumpPressed = true;
                            break;
                        case InputIntent.AttackPrimary:
                            attackPrimaryPressed = true;
                            break;
                        case InputIntent.AttackSecondary:
                            attackSecondaryPressed = true;
                            break;
                        case InputIntent.Dodge:
                            dodgePressed = true;
                            break;
                        case InputIntent.DebugPrimary:
                            debugPrimaryPressed = true;
                            break;
                        case InputIntent.DebugSecondary:
                            debugSecondaryPressed = true;
                            break;
                        default:
                            skillSlotPressedMask |= ToSkillSlotMask(command.Intent);
                            break;
                    }
                }
                else if (command.Intent == InputIntent.Jump && IsReleasedCommand(command.Phase))
                {
                    jumpReleased = true;
                }
            }

            skillSlotPressedMask &= CharacterTestControlIntent.SkillSlotMaskBits;
        }

        private static bool IsPressedCommand(InputCommandPhase phase)
        {
            return phase == InputCommandPhase.Pressed || phase == InputCommandPhase.Performed;
        }

        private static bool IsReleasedCommand(InputCommandPhase phase)
        {
            return phase == InputCommandPhase.Released || phase == InputCommandPhase.Canceled;
        }

        private static int ToSkillSlotMask(InputIntent intent)
        {
            switch (intent)
            {
                case InputIntent.SkillSlot1:
                    return 1 << 0;
                case InputIntent.SkillSlot2:
                    return 1 << 1;
                case InputIntent.SkillSlot3:
                    return 1 << 2;
                case InputIntent.SkillSlot4:
                    return 1 << 3;
                case InputIntent.SkillSlot5:
                    return 1 << 4;
                case InputIntent.SkillSlot6:
                    return 1 << 5;
                case InputIntent.SkillSlot7:
                    return 1 << 6;
                case InputIntent.SkillSlot8:
                    return 1 << 7;
                case InputIntent.SkillSlot9:
                    return 1 << 8;
                default:
                    return 0;
            }
        }

        private void LogSubmitFailureOnce(string message)
        {
            string safeMessage = string.IsNullOrWhiteSpace(message)
                ? "unknown error"
                : message;
            if (string.Equals(_lastSubmitFailureMessage, safeMessage, StringComparison.Ordinal))
                return;

            _lastSubmitFailureMessage = safeMessage;
            _logger.Warning(
                "CharacterTest.LocalInputCommands",
                "Control intent command rejected: " + safeMessage);
        }

        private void WarmUpSubmitCommandPath()
        {
            if (_submitCommandPathWarmed)
                return;

            _submitCommandPathWarmed = true;
            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetControlIntent(
                RuntimeFrame.Zero,
                CharacterTestRuntimeCommandSources.PlayerInput,
                targetEntityId: 1,
                move: Vector2.zero,
                worldMove: Vector2.zero,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: false,
                attackSecondaryPressed: false,
                debugSecondaryPressed: false,
                debugPrimaryPressed: false,
                skillSlotPressedMask: 0,
                dodgePressed: false,
                traceId: RuntimeTraceIdentity.None);
            _submitCommand(command);
            _clearCommandWarmupState?.Invoke();
            _lastSubmitFailureMessage = string.Empty;
        }

        private void SubmitAbilitySlotCastIfNeeded(
            RuntimeFrame frame,
            CharacterTestCharacterEntity entity,
            int abilitySlotPressed)
        {
            if (!CharacterTestAbilityHotbarRuntime.IsValidSlot(abilitySlotPressed)
                || _abilityHotbarRuntime == null
                || entity == null)
            {
                return;
            }

            if (!_abilityHotbarRuntime.TryGetAbilityForSlot(
                abilitySlotPressed,
                out MxFramework.Gameplay.AbilityDefinitionConfig ability))
            {
                return;
            }

            RuntimeCommand command = CharacterTestRuntimeCommandFactory.CastAbilitySlot(
                frame,
                CharacterTestRuntimeCommandSources.PlayerInput,
                entity.Id.Value,
                abilitySlotPressed,
                ability.AbilityId,
                new RuntimeTraceIdentity(AbilitySlotTraceKind, CharacterTestRuntimeCommandSources.PlayerInput, frame.Value, entity.Id.Value));
            LogCommandSubmit("submit", command, force: true);
            RuntimeCommandValidationResult result = _submitCommand(command);
            if (!result.Success)
            {
                LogCommandSubmit("submit-rejected", command, result.Error.Message, force: true);
                LogSubmitFailureOnce(result.Error.Message);
                return;
            }

            LogCommandSubmit("submit-accepted", command, force: true);
            _lastSubmitFailureMessage = string.Empty;
        }

        private void LogInputSampleIfNeeded(
            RuntimeFrame frame,
            InputSnapshot snapshot,
            bool providerAvailable,
            bool gameplayContextEnabled,
            string providerType)
        {
            if (!CharacterTestInputFlowLog.Enabled)
                return;

            bool active = CharacterTestInputFlowLog.HasActiveSnapshot(snapshot);
            int repeatFrames = active
                ? CharacterTestInputFlowLog.ActiveRepeatFrames
                : CharacterTestInputFlowLog.IdleRepeatFrames;
            if (_lastInputFlowLogFrame != long.MinValue
                && _lastLoggedActiveInput == active
                && frame.Value - _lastInputFlowLogFrame < repeatFrames)
            {
                return;
            }

            _lastInputFlowLogFrame = frame.Value;
            _lastLoggedActiveInput = active;
            _inputFlowBuffer.Clear()
                .Append("sample frame=").Append(frame.Value)
                .Append(" provider=").Append(providerType)
                .Append(" providerAvailable=").Append(providerAvailable)
                .Append(" gameplayContext=").Append(gameplayContextEnabled);
            CharacterTestInputFlowLog.AppendEditorFocus(_inputFlowBuffer);
            CharacterTestInputFlowLog.AppendSnapshot(_inputFlowBuffer, snapshot);
            _logger.Info(CharacterTestInputFlowLog.Category, _inputFlowBuffer);
        }

        private void LogCommandSubmit(
            string phase,
            RuntimeCommand command,
            string reason = "",
            bool force = false)
        {
            if (!CharacterTestInputFlowLog.Enabled)
                return;

            if (!force && !CharacterTestInputFlowLog.HasActiveCommand(command))
                return;

            _inputFlowBuffer.Clear()
                .Append(phase).Append(' ');
            CharacterTestInputFlowLog.AppendCommand(_inputFlowBuffer, command);
            if (!string.IsNullOrWhiteSpace(reason))
                _inputFlowBuffer.Append(" reason=").Append(reason);
            _logger.Info(CharacterTestInputFlowLog.Category, _inputFlowBuffer);
        }
    }

    internal static class CharacterTestInputFlowLog
    {
        public static bool Enabled => false;
        public const string Category = "CharacterTest.InputFlow";
        public const int ActiveRepeatFrames = 30;
        public const int IdleRepeatFrames = 120;

        private const float ActiveMoveEpsilon = 0.001f;
        private const float ActiveMotionEpsilon = 0.001f;

        public static bool HasActiveSnapshot(InputSnapshot snapshot)
        {
            return snapshot.Move.sqrMagnitude > ActiveMoveEpsilon
                || snapshot.JumpPressed
                || snapshot.JumpHeld
                || snapshot.JumpReleased
                || snapshot.AttackPrimaryPressed
                || snapshot.AttackSecondaryPressed
                || snapshot.DodgePressed
                || snapshot.DebugPrimaryPressed
                || snapshot.DebugSecondaryPressed
                || snapshot.SkillSlotPressedMask != 0;
        }

        public static bool HasActiveIntent(CharacterTestControlIntent intent)
        {
            return intent.Move.sqrMagnitude > ActiveMoveEpsilon
                || intent.WorldMove.sqrMagnitude > ActiveMoveEpsilon
                || intent.JumpPressed
                || intent.JumpHeld
                || intent.JumpReleased
                || intent.AttackPrimaryPressed
                || intent.AttackSecondaryPressed
                || intent.DodgePressed
                || intent.DebugPrimaryPressed
                || intent.DebugSecondaryPressed
                || intent.SkillSlotPressedMask != 0;
        }

        public static bool HasActiveInput(CharacterTestCharacterInputComponent input)
        {
            return input.HasActiveMove
                || input.JumpPressed
                || input.JumpHeld
                || input.JumpReleased
                || input.AttackPrimaryPressed
                || input.AttackSecondaryPressed
                || input.DodgePressed
                || input.DebugPrimaryPressed
                || input.DebugSecondaryPressed
                || input.SkillSlotPressedMask != 0;
        }

        public static bool HasActiveCommand(RuntimeCommand command)
        {
            if (command.CommandId != CharacterTestRuntimeCommandIds.SetControlIntent)
                return true;

            CharacterTestControlIntent intent = CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
            return HasActiveIntent(intent);
        }

        public static bool HasMotionEffect(CharacterTestCharacterEntity entity)
        {
            if (entity == null)
                return false;

            return entity.HorizontalVelocity.sqrMagnitude > ActiveMotionEpsilon
                || entity.DesiredVelocity.sqrMagnitude > ActiveMotionEpsilon
                || entity.LastMoveInput.sqrMagnitude > ActiveMoveEpsilon
                || Mathf.Abs(entity.VerticalVelocity) > ActiveMotionEpsilon;
        }

        public static void AppendSnapshot(RuntimeLogBuffer buffer, InputSnapshot snapshot)
        {
            buffer.Append(" move=");
            AppendVector2(buffer, snapshot.Move);
            buffer.Append(" jumpP=").Append(snapshot.JumpPressed);
            buffer.Append(" jumpH=").Append(snapshot.JumpHeld);
            buffer.Append(" jumpR=").Append(snapshot.JumpReleased);
            buffer.Append(" atkP=").Append(snapshot.AttackPrimaryPressed);
            buffer.Append(" atkS=").Append(snapshot.AttackSecondaryPressed);
            buffer.Append(" dodge=").Append(snapshot.DodgePressed);
            buffer.Append(" dbgP=").Append(snapshot.DebugPrimaryPressed);
            buffer.Append(" dbgS=").Append(snapshot.DebugSecondaryPressed);
            buffer.Append(" skillMask=").Append(snapshot.SkillSlotPressedMask);
        }

        public static void AppendIntent(RuntimeLogBuffer buffer, CharacterTestControlIntent intent)
        {
            buffer.Append(" entity=").Append(intent.TargetEntityId);
            buffer.Append(" move=");
            AppendVector2(buffer, intent.Move);
            buffer.Append(" world=");
            AppendVector2(buffer, intent.WorldMove);
            buffer.Append(" jumpP=").Append(intent.JumpPressed);
            buffer.Append(" jumpH=").Append(intent.JumpHeld);
            buffer.Append(" jumpR=").Append(intent.JumpReleased);
            buffer.Append(" atkP=").Append(intent.AttackPrimaryPressed);
            buffer.Append(" atkS=").Append(intent.AttackSecondaryPressed);
            buffer.Append(" dodge=").Append(intent.DodgePressed);
            buffer.Append(" dbgP=").Append(intent.DebugPrimaryPressed);
            buffer.Append(" dbgS=").Append(intent.DebugSecondaryPressed);
            buffer.Append(" skillMask=").Append(intent.SkillSlotPressedMask);
        }

        public static void AppendCommand(RuntimeLogBuffer buffer, RuntimeCommand command)
        {
            buffer.Append(" command=").Append(command.CommandId);
            buffer.Append(" frame=").Append(command.Frame.Value);
            buffer.Append(" target=").Append(command.TargetId);
            buffer.Append(" p0=").Append(command.Payload0);
            buffer.Append(" p1=").Append(command.Payload1);
            buffer.Append(" p2=").Append(command.Payload2);
        }

        public static void AppendPostMotion(RuntimeLogBuffer buffer, CharacterTestCharacterEntity entity)
        {
            buffer.Append(" entity=").Append(entity.Id.Value);
            buffer.Append(" pos=");
            AppendVector3(buffer, entity.Transform.X, entity.Transform.Y, entity.Transform.Z);
            buffer.Append(" input=");
            AppendVector2(buffer, entity.Input.Move);
            buffer.Append(" desired=");
            AppendVector2(buffer, entity.DesiredVelocity);
            buffer.Append(" horizontal=");
            AppendVector2(buffer, entity.HorizontalVelocity);
            buffer.Append(" vertical=");
            AppendFloat(buffer, entity.VerticalVelocity);
            buffer.Append(" grounded=").Append(entity.IsGrounded);
            buffer.Append(" mode=").Append(entity.MovementMode.ToString());
            buffer.Append(" locked=").Append(entity.MovementLocked);
        }

        public static void AppendEditorFocus(RuntimeLogBuffer buffer)
        {
            buffer.Append(" appFocused=").Append(Application.isFocused);
        }

        public static void AppendVector2(RuntimeLogBuffer buffer, Vector2 value)
        {
            buffer.Append('(');
            AppendFloat(buffer, value.x);
            buffer.Append(',');
            AppendFloat(buffer, value.y);
            buffer.Append(')');
        }

        private static void AppendVector3(RuntimeLogBuffer buffer, float x, float y, float z)
        {
            buffer.Append('(');
            AppendFloat(buffer, x);
            buffer.Append(',');
            AppendFloat(buffer, y);
            buffer.Append(',');
            AppendFloat(buffer, z);
            buffer.Append(')');
        }

        private static void AppendFloat(RuntimeLogBuffer buffer, float value)
        {
            buffer.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }

    internal interface ICharacterTestAbilityHotbarInputSource
    {
        int ReadPressedSlot();
    }

    internal sealed class CharacterTestKeyboardAbilityHotbarInputSource :
        ICharacterTestAbilityHotbarInputSource
    {
        private static readonly string[] TopRowKeyPropertyNames =
        {
            "digit1Key",
            "digit2Key",
            "digit3Key",
            "digit4Key",
            "digit5Key",
            "digit6Key",
            "digit7Key",
            "digit8Key",
            "digit9Key"
        };

        private static readonly string[] NumpadKeyPropertyNames =
        {
            "numpad1Key",
            "numpad2Key",
            "numpad3Key",
            "numpad4Key",
            "numpad5Key",
            "numpad6Key",
            "numpad7Key",
            "numpad8Key",
            "numpad9Key"
        };

        public static readonly CharacterTestKeyboardAbilityHotbarInputSource Shared =
            new CharacterTestKeyboardAbilityHotbarInputSource();

        private readonly Func<object> _readCurrentKeyboard;
        private readonly Func<object, object>[] _topRowKeyReaders =
            new Func<object, object>[CharacterTestAbilityHotbarRuntime.SlotCount];
        private readonly Func<object, object>[] _numpadKeyReaders =
            new Func<object, object>[CharacterTestAbilityHotbarRuntime.SlotCount];
        private readonly Func<object, bool> _wasPressedReader;

        public CharacterTestKeyboardAbilityHotbarInputSource()
        {
            Type keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            PropertyInfo currentProperty = keyboardType?.GetProperty(
                "current",
                BindingFlags.Public | BindingFlags.Static);
            _readCurrentKeyboard = CreateStaticReferenceGetter(currentProperty);
            _wasPressedReader = CreateKeyReaders(keyboardType);
        }

        public int ReadPressedSlot()
        {
            if (_readCurrentKeyboard == null || _wasPressedReader == null)
                return 0;

            object keyboard = _readCurrentKeyboard();
            if (keyboard == null)
                return 0;

            for (int i = 0; i < CharacterTestAbilityHotbarRuntime.SlotCount; i++)
            {
                if (WasPressed(keyboard, _topRowKeyReaders[i])
                    || WasPressed(keyboard, _numpadKeyReaders[i]))
                {
                    return i + CharacterTestAbilityHotbarRuntime.FirstSlotNumber;
                }
            }

            return 0;
        }

        private Func<object, bool> CreateKeyReaders(Type keyboardType)
        {
            if (keyboardType == null)
                return null;

            Type keyControlType = null;
            for (int i = 0; i < CharacterTestAbilityHotbarRuntime.SlotCount; i++)
            {
                _topRowKeyReaders[i] = CreateKeyReader(
                    keyboardType,
                    TopRowKeyPropertyNames[i],
                    ref keyControlType);
                _numpadKeyReaders[i] = CreateKeyReader(
                    keyboardType,
                    NumpadKeyPropertyNames[i],
                    ref keyControlType);
            }

            PropertyInfo wasPressedProperty = keyControlType?.GetProperty(
                "wasPressedThisFrame",
                BindingFlags.Instance | BindingFlags.Public);
            return CreateInstanceBoolGetter(wasPressedProperty);
        }

        private bool WasPressed(object keyboard, Func<object, object> keyReader)
        {
            if (keyReader == null)
                return false;

            return _wasPressedReader(keyReader(keyboard));
        }

        private static Func<object, object> CreateKeyReader(
            Type keyboardType,
            string propertyName,
            ref Type keyControlType)
        {
            PropertyInfo property = keyboardType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
                return null;

            keyControlType ??= property.PropertyType;
            return CreateInstanceReferenceGetter(property);
        }

        private static Func<object> CreateStaticReferenceGetter(PropertyInfo property)
        {
            if (property == null || property.GetGetMethod() == null)
                return null;

            try
            {
                MethodInfo method = typeof(CharacterTestKeyboardAbilityHotbarInputSource)
                    .GetMethod(
                        nameof(CreateStaticReferenceGetterCore),
                        BindingFlags.NonPublic | BindingFlags.Static)
                    ?.MakeGenericMethod(property.PropertyType);
                return method != null
                    ? (Func<object>)method.Invoke(null, new object[] { property })
                    : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static Func<object, object> CreateInstanceReferenceGetter(PropertyInfo property)
        {
            if (property == null || property.GetGetMethod() == null)
                return null;

            try
            {
                MethodInfo method = typeof(CharacterTestKeyboardAbilityHotbarInputSource)
                    .GetMethod(
                        nameof(CreateInstanceReferenceGetterCore),
                        BindingFlags.NonPublic | BindingFlags.Static)
                    ?.MakeGenericMethod(property.DeclaringType, property.PropertyType);
                return method != null
                    ? (Func<object, object>)method.Invoke(null, new object[] { property })
                    : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static Func<object, bool> CreateInstanceBoolGetter(PropertyInfo property)
        {
            if (property == null
                || property.PropertyType != typeof(bool)
                || property.GetGetMethod() == null)
            {
                return null;
            }

            try
            {
                MethodInfo method = typeof(CharacterTestKeyboardAbilityHotbarInputSource)
                    .GetMethod(
                        nameof(CreateInstanceBoolGetterCore),
                        BindingFlags.NonPublic | BindingFlags.Static)
                    ?.MakeGenericMethod(property.DeclaringType);
                return method != null
                    ? (Func<object, bool>)method.Invoke(null, new object[] { property })
                    : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static Func<object> CreateStaticReferenceGetterCore<TValue>(
            PropertyInfo property)
            where TValue : class
        {
            var getter = (Func<TValue>)Delegate.CreateDelegate(
                typeof(Func<TValue>),
                property.GetGetMethod());
            return () => getter();
        }

        private static Func<object, object> CreateInstanceReferenceGetterCore<TDeclaring, TValue>(
            PropertyInfo property)
            where TDeclaring : class
            where TValue : class
        {
            var getter = (Func<TDeclaring, TValue>)Delegate.CreateDelegate(
                typeof(Func<TDeclaring, TValue>),
                property.GetGetMethod());
            return target => target != null ? getter((TDeclaring)target) : null;
        }

        private static Func<object, bool> CreateInstanceBoolGetterCore<TDeclaring>(
            PropertyInfo property)
            where TDeclaring : class
        {
            var getter = (Func<TDeclaring, bool>)Delegate.CreateDelegate(
                typeof(Func<TDeclaring, bool>),
                property.GetGetMethod());
            return target => target != null && getter((TDeclaring)target);
        }
    }
}
