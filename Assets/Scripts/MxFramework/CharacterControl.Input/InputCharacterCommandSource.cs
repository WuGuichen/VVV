using System;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using UnityEngine;
using MxInput = MxFramework.Input;

namespace MxFramework.CharacterControl.Input
{
    public readonly struct CharacterInputActionBinding
    {
        private CharacterInputActionBinding(
            MxInput.InputIntent intent,
            CharacterActionKind kind,
            int combatActionId,
            int gameplayAbilityId,
            bool forceStart,
            bool queueIfBusy)
        {
            Intent = intent;
            Kind = kind;
            CombatActionId = combatActionId;
            GameplayAbilityId = gameplayAbilityId;
            ForceStart = forceStart;
            QueueIfBusy = queueIfBusy;
        }

        public MxInput.InputIntent Intent { get; }

        public CharacterActionKind Kind { get; }

        public int CombatActionId { get; }

        public int GameplayAbilityId { get; }

        public bool ForceStart { get; }

        public bool QueueIfBusy { get; }

        public bool IsValid => Intent != MxInput.InputIntent.Unknown && Kind != CharacterActionKind.None;

        public static CharacterInputActionBinding CombatAction(
            MxInput.InputIntent intent,
            CharacterActionKind kind,
            int combatActionId,
            bool queueIfBusy = false,
            bool forceStart = false)
        {
            return new CharacterInputActionBinding(intent, kind, combatActionId, 0, forceStart, queueIfBusy);
        }

        public static CharacterInputActionBinding GameplayAbility(
            MxInput.InputIntent intent,
            int gameplayAbilityId,
            bool queueIfBusy = false)
        {
            return new CharacterInputActionBinding(intent, CharacterActionKind.GameplayAbility, 0, gameplayAbilityId, false, queueIfBusy);
        }

        public static CharacterInputActionBinding Cancel(MxInput.InputIntent intent)
        {
            return new CharacterInputActionBinding(intent, CharacterActionKind.Cancel, 0, 0, false, false);
        }
    }

    public sealed class InputCharacterCommandSourceOptions
    {
        public int SourceId { get; set; }

        public CharacterFacingBasis FacingBasis { get; set; } = CharacterFacingBasis.Identity;

        public Func<RuntimeFrame, CharacterFacingBasis> FacingBasisProvider { get; set; }

        public bool UseLookAsFacing { get; set; }

        public Func<int, GameplayEntityId> TargetResolver { get; set; }

        public Func<MxInput.IInputProvider, bool> CanReadGameplayInput { get; set; }

        public CharacterInputActionBinding[] ActionBindings { get; set; } =
            Array.Empty<CharacterInputActionBinding>();

        public Fix64 MoveSpeedScale { get; set; } = Fix64.One;

        public string TracePrefix { get; set; } = "input";
    }

    public sealed class InputCharacterCommandSource : ICharacterCommandSource
    {
        private readonly MxInput.IInputProvider _inputProvider;
        private readonly InputCharacterCommandSourceOptions _options;
        private readonly List<MxInput.InputCommand> _commands = new List<MxInput.InputCommand>(8);

        public InputCharacterCommandSource(
            MxInput.IInputProvider inputProvider,
            InputCharacterCommandSourceOptions options = null)
        {
            _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
            _options = options ?? new InputCharacterCommandSourceOptions();
        }

        public bool TryGetCommand(RuntimeFrame frame, CharacterControlEntityRef entity, out CharacterCommand command)
        {
            command = default;
            if (!CanReadGameplayInput())
            {
                _commands.Clear();
                _inputProvider.Commands.DrainForFrame(frame.Value, _commands);
                return false;
            }

            MxInput.InputSnapshot snapshot = _inputProvider.Snapshot;
            _commands.Clear();
            _inputProvider.Commands.DrainForFrame(frame.Value, _commands);

            CharacterActionButtons buttons = GetSnapshotButtons(snapshot);
            bool jumpPressed = snapshot.JumpPressed;
            bool sprintHeld = snapshot.SprintHeld;
            CharacterActionRequest actionRequest = default;
            string traceId = BuildTraceId(frame, MxInput.InputIntent.Unknown, string.Empty);

            for (int i = 0; i < _commands.Count; i++)
            {
                MxInput.InputCommand inputCommand = _commands[i];
                if (!IsPressed(inputCommand.Phase))
                {
                    continue;
                }

                buttons |= GetIntentButton(inputCommand.Intent);
                if (inputCommand.Intent == MxInput.InputIntent.Jump)
                {
                    jumpPressed = true;
                }
                else if (inputCommand.Intent == MxInput.InputIntent.Sprint)
                {
                    sprintHeld = true;
                }

                if (actionRequest.Kind == CharacterActionKind.None
                    && TryCreateActionRequest(frame, entity, inputCommand, out CharacterActionRequest request))
                {
                    actionRequest = request;
                    traceId = request.TraceId;
                }
            }

            if (actionRequest.Kind == CharacterActionKind.None)
            {
                actionRequest = CreateSnapshotActionRequest(frame, entity, snapshot, ref traceId);
            }

            command = new CharacterCommand(
                frame,
                _options.SourceId,
                entity,
                ToMoveVector(snapshot.Move),
                GetFacingBasis(frame, snapshot),
                jumpPressed,
                sprintHeld,
                buttons,
                actionRequest,
                _options.MoveSpeedScale,
                traceId);
            return true;
        }

        private bool CanReadGameplayInput()
        {
            if (_options.CanReadGameplayInput != null)
            {
                return _options.CanReadGameplayInput(_inputProvider);
            }

            return _inputProvider.CurrentContext == MxInput.InputContext.Gameplay;
        }

        private CharacterFacingBasis GetFacingBasis(RuntimeFrame frame, MxInput.InputSnapshot snapshot)
        {
            if (_options.UseLookAsFacing && snapshot.Look.sqrMagnitude > 0f)
            {
                return CharacterFacingBasis.FromForward(ToMoveVector(snapshot.Look));
            }

            return _options.FacingBasisProvider != null
                ? _options.FacingBasisProvider(frame)
                : _options.FacingBasis;
        }

        private CharacterActionRequest CreateSnapshotActionRequest(
            RuntimeFrame frame,
            CharacterControlEntityRef entity,
            MxInput.InputSnapshot snapshot,
            ref string traceId)
        {
            if (snapshot.AttackPrimaryPressed)
            {
                return CreateBoundRequest(frame, entity, MxInput.InputIntent.AttackPrimary, 0, string.Empty, ref traceId);
            }

            if (snapshot.AttackSecondaryPressed)
            {
                return CreateBoundRequest(frame, entity, MxInput.InputIntent.AttackSecondary, 0, string.Empty, ref traceId);
            }

            if (snapshot.InteractPressed)
            {
                return CreateBoundRequest(frame, entity, MxInput.InputIntent.Interact, 0, string.Empty, ref traceId);
            }

            if (snapshot.DodgePressed)
            {
                return CreateBoundRequest(frame, entity, MxInput.InputIntent.Dodge, 0, string.Empty, ref traceId);
            }

            if (snapshot.CancelPressed)
            {
                return CreateBoundRequest(frame, entity, MxInput.InputIntent.Cancel, 0, string.Empty, ref traceId);
            }

            return default;
        }

        private bool TryCreateActionRequest(
            RuntimeFrame frame,
            CharacterControlEntityRef entity,
            MxInput.InputCommand inputCommand,
            out CharacterActionRequest request)
        {
            string traceId = inputCommand.TraceId;
            request = CreateBoundRequest(
                frame,
                entity,
                inputCommand.Intent,
                inputCommand.TargetId,
                inputCommand.TraceId,
                ref traceId);
            return request.Kind != CharacterActionKind.None;
        }

        private CharacterActionRequest CreateBoundRequest(
            RuntimeFrame frame,
            CharacterControlEntityRef entity,
            MxInput.InputIntent intent,
            int targetId,
            string inputTraceId,
            ref string traceId)
        {
            if (!TryGetBinding(intent, out CharacterInputActionBinding binding))
            {
                return default;
            }

            traceId = BuildTraceId(frame, intent, inputTraceId);
            if (binding.Kind == CharacterActionKind.Cancel)
            {
                return CharacterActionRequest.Cancel(frame, entity, _options.SourceId, traceId);
            }

            if (binding.CombatActionId != 0)
            {
                return CharacterActionRequest.CombatAction(
                    frame,
                    entity,
                    binding.Kind,
                    binding.CombatActionId,
                    sourceId: _options.SourceId,
                    traceId: traceId,
                    forceStart: binding.ForceStart,
                    queueIfBusy: binding.QueueIfBusy);
            }

            if (binding.GameplayAbilityId != 0)
            {
                GameplayEntityId target = ResolveTarget(targetId);
                return target.IsValid
                    ? CharacterActionRequest.GameplayAbility(frame, entity, binding.GameplayAbilityId, target, _options.SourceId, traceId)
                    : CharacterActionRequest.GameplayAbility(
                        frame,
                        entity,
                        binding.GameplayAbilityId,
                        sourceId: _options.SourceId,
                        traceId: traceId);
            }

            return default;
        }

        private bool TryGetBinding(MxInput.InputIntent intent, out CharacterInputActionBinding binding)
        {
            CharacterInputActionBinding[] bindings = _options.ActionBindings ?? Array.Empty<CharacterInputActionBinding>();
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].Intent == intent && bindings[i].IsValid)
                {
                    binding = bindings[i];
                    return true;
                }
            }

            binding = default;
            return false;
        }

        private GameplayEntityId ResolveTarget(int targetId)
        {
            return targetId != 0 && _options.TargetResolver != null
                ? _options.TargetResolver(targetId)
                : default;
        }

        private string BuildTraceId(RuntimeFrame frame, MxInput.InputIntent intent, string inputTraceId)
        {
            if (!string.IsNullOrEmpty(inputTraceId))
            {
                return inputTraceId;
            }

            string prefix = _options.TracePrefix ?? string.Empty;
            return intent == MxInput.InputIntent.Unknown
                ? $"{prefix}:{frame.Value}"
                : $"{prefix}:{frame.Value}:{intent}";
        }

        private static bool IsPressed(MxInput.InputCommandPhase phase)
        {
            return phase == MxInput.InputCommandPhase.Pressed
                || phase == MxInput.InputCommandPhase.Performed;
        }

        private static CharacterActionButtons GetSnapshotButtons(MxInput.InputSnapshot snapshot)
        {
            CharacterActionButtons buttons = CharacterActionButtons.None;
            if (snapshot.AttackPrimaryPressed || snapshot.AttackPrimaryHeld)
            {
                buttons |= CharacterActionButtons.Primary;
            }

            if (snapshot.AttackSecondaryPressed)
            {
                buttons |= CharacterActionButtons.Secondary;
            }

            if (snapshot.InteractPressed)
            {
                buttons |= CharacterActionButtons.Interact;
            }

            if (snapshot.DodgePressed)
            {
                buttons |= CharacterActionButtons.Dodge;
            }

            if (snapshot.CancelPressed)
            {
                buttons |= CharacterActionButtons.Cancel;
            }

            return buttons;
        }

        private static CharacterActionButtons GetIntentButton(MxInput.InputIntent intent)
        {
            switch (intent)
            {
                case MxInput.InputIntent.AttackPrimary:
                    return CharacterActionButtons.Primary;
                case MxInput.InputIntent.AttackSecondary:
                    return CharacterActionButtons.Secondary;
                case MxInput.InputIntent.Interact:
                    return CharacterActionButtons.Interact;
                case MxInput.InputIntent.Dodge:
                    return CharacterActionButtons.Dodge;
                case MxInput.InputIntent.Cancel:
                    return CharacterActionButtons.Cancel;
                default:
                    return CharacterActionButtons.None;
            }
        }

        private static FixVector3 ToMoveVector(Vector2 move)
        {
            return new FixVector3(
                ToFix(move.x),
                Fix64.Zero,
                ToFix(move.y));
        }

        private static Fix64 ToFix(float value)
        {
            float clamped = Mathf.Clamp(value, -1f, 1f);
            return Fix64.FromRaw((long)Math.Round(clamped * Fix64.Scale));
        }
    }
}
