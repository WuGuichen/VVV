using System;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterControl
{
    public enum CharacterActionEventType
    {
        Accepted = 0,
        Rejected = 1,
        Queued = 2,
        Started = 3,
        GameplayCommandEnqueued = 4,
        Finished = 5,
        Canceled = 6
    }

    public enum CharacterActionRejectedReason
    {
        None = 0,
        InvalidRequest = 1,
        EntityMismatch = 2,
        ControlStateBlocked = 3,
        ActionLocked = 4,
        DuplicateSameFrame = 5,
        Busy = 6,
        ConstraintRejected = 7,
        MissingCombatActionRunner = 8,
        MissingCombatEntity = 9,
        MissingGameplayCommandBuffer = 10,
        MissingGameplayEntity = 11,
        MissingAbilityRequestStore = 12,
        RuntimeCommandRejected = 13,
        CombatActionRejected = 14,
        CancelRejected = 15
    }

    public readonly struct CharacterActionConstraintResult
    {
        private CharacterActionConstraintResult(bool success, CharacterActionRejectedReason reason, string message)
        {
            Success = success;
            Reason = reason;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }

        public CharacterActionRejectedReason Reason { get; }

        public string Message { get; }

        public static CharacterActionConstraintResult Allowed()
        {
            return new CharacterActionConstraintResult(true, CharacterActionRejectedReason.None, string.Empty);
        }

        public static CharacterActionConstraintResult Rejected(CharacterActionRejectedReason reason, string message)
        {
            return new CharacterActionConstraintResult(false, reason, message);
        }
    }

    public readonly struct CharacterActionContext
    {
        public CharacterActionContext(CharacterActionRequest request, CharacterControlState state, CharacterControlLockMask lockMask)
        {
            Request = request;
            State = state;
            LockMask = lockMask;
        }

        public CharacterActionRequest Request { get; }

        public CharacterControlState State { get; }

        public CharacterControlLockMask LockMask { get; }
    }

    public interface ICharacterActionConstraint
    {
        CharacterActionConstraintResult Evaluate(CharacterActionContext context);
    }

    public readonly struct CharacterActionResult
    {
        private CharacterActionResult(
            bool success,
            bool queued,
            CharacterActionRejectedReason rejectedReason,
            int actionInstanceId,
            RuntimeCommand runtimeCommand,
            string message)
        {
            Success = success;
            Queued = queued;
            RejectedReason = rejectedReason;
            ActionInstanceId = actionInstanceId;
            RuntimeCommand = runtimeCommand;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }

        public bool Queued { get; }

        public CharacterActionRejectedReason RejectedReason { get; }

        public int ActionInstanceId { get; }

        public RuntimeCommand RuntimeCommand { get; }

        public bool HasRuntimeCommand => RuntimeCommand.CommandId != 0;

        public string Message { get; }

        public static CharacterActionResult Accepted(int actionInstanceId = 0)
        {
            return new CharacterActionResult(true, false, CharacterActionRejectedReason.None, actionInstanceId, default, string.Empty);
        }

        public static CharacterActionResult CommandAccepted(RuntimeCommand command)
        {
            return new CharacterActionResult(true, false, CharacterActionRejectedReason.None, 0, command, string.Empty);
        }

        public static CharacterActionResult QueuedResult()
        {
            return new CharacterActionResult(true, true, CharacterActionRejectedReason.None, 0, default, string.Empty);
        }

        public static CharacterActionResult Rejected(CharacterActionRejectedReason reason, string message)
        {
            return new CharacterActionResult(false, false, reason, 0, default, message);
        }
    }

    public readonly struct CharacterActionEvent
    {
        public CharacterActionEvent(
            CharacterActionEventType type,
            CharacterActionRequest request,
            CharacterActionRejectedReason rejectedReason,
            int actionInstanceId,
            RuntimeCommand runtimeCommand,
            string message)
        {
            Type = type;
            Request = request;
            RejectedReason = rejectedReason;
            ActionInstanceId = actionInstanceId;
            RuntimeCommand = runtimeCommand;
            Message = message ?? string.Empty;
        }

        public CharacterActionEventType Type { get; }

        public CharacterActionRequest Request { get; }

        public CharacterActionRejectedReason RejectedReason { get; }

        public int ActionInstanceId { get; }

        public RuntimeCommand RuntimeCommand { get; }

        public string Message { get; }
    }

    public sealed class CharacterActionController : IDisposable
    {
        private readonly CharacterControlStateMachine _stateMachine;
        private readonly CombatActionRunner _combatActionRunner;
        private readonly RuntimeCommandBuffer _gameplayCommandBuffer;
        private readonly GameplayComponentAbilityRequestStore _abilityRequestStore;
        private readonly ICharacterActionConstraint[] _constraints;
        private CharacterActionRequest _lastSubmittedRequest;
        private bool _hasLastSubmittedRequest;
        private CharacterActionRequest _pendingCombatActionRequest;
        private bool _hasPendingCombatActionRequest;
        private CharacterActionRequest _activeCombatActionRequest;
        private bool _hasActiveCombatActionRequest;
        private CharacterActionRequest _queuedRequest;
        private bool _hasQueuedRequest;
        private bool _disposed;

        public CharacterActionController(
            CharacterControlStateMachine stateMachine,
            CombatActionRunner combatActionRunner = null,
            RuntimeCommandBuffer gameplayCommandBuffer = null,
            GameplayComponentAbilityRequestStore abilityRequestStore = null,
            ICharacterActionConstraint[] constraints = null)
        {
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _combatActionRunner = combatActionRunner;
            _gameplayCommandBuffer = gameplayCommandBuffer;
            _abilityRequestStore = abilityRequestStore;
            _constraints = constraints ?? Array.Empty<ICharacterActionConstraint>();

            if (_combatActionRunner != null)
            {
                _combatActionRunner.ActionStarted += OnCombatActionStarted;
                _combatActionRunner.ActionFinished += OnCombatActionFinished;
                _combatActionRunner.ActionCanceled += OnCombatActionCanceled;
            }
        }

        public event Action<CharacterActionEvent> ActionEvent;

        public bool HasQueuedRequest => _hasQueuedRequest;

        public CharacterActionResult Submit(CharacterActionRequest request)
        {
            ThrowIfDisposed();
            return SubmitInternal(request, fromQueue: false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_combatActionRunner != null)
            {
                _combatActionRunner.ActionStarted -= OnCombatActionStarted;
                _combatActionRunner.ActionFinished -= OnCombatActionFinished;
                _combatActionRunner.ActionCanceled -= OnCombatActionCanceled;
            }

            _disposed = true;
        }

        private CharacterActionResult SubmitInternal(CharacterActionRequest request, bool fromQueue)
        {
            CharacterActionResult validation = ValidateRequest(request, fromQueue);
            if (!validation.Success)
            {
                Emit(CharacterActionEventType.Rejected, request, validation.RejectedReason, 0, default, validation.Message);
                return validation;
            }

            _lastSubmittedRequest = request;
            _hasLastSubmittedRequest = true;

            if (request.Kind == CharacterActionKind.Cancel)
            {
                return ExecuteCancel(request);
            }

            if (_stateMachine.CurrentState == CharacterControlState.Action
                && request.QueueIfBusy
                && !request.ForceStart)
            {
                _queuedRequest = request;
                _hasQueuedRequest = true;
                Emit(CharacterActionEventType.Queued, request, CharacterActionRejectedReason.None, 0, default, string.Empty);
                return CharacterActionResult.QueuedResult();
            }

            if (request.HasCombatAction)
            {
                return ExecuteCombatAction(request);
            }

            if (request.HasGameplayAbility)
            {
                return ExecuteGameplayAbility(request);
            }

            CharacterActionResult rejected = CharacterActionResult.Rejected(CharacterActionRejectedReason.InvalidRequest, "Action request has no executable target.");
            Emit(CharacterActionEventType.Rejected, request, rejected.RejectedReason, 0, default, rejected.Message);
            return rejected;
        }

        private CharacterActionResult ValidateRequest(CharacterActionRequest request, bool fromQueue)
        {
            if (request.Kind == CharacterActionKind.None)
            {
                return CharacterActionResult.Rejected(CharacterActionRejectedReason.InvalidRequest, "Action request kind is None.");
            }

            if (_stateMachine.Entity.IsValid && request.Entity.IsValid && !_stateMachine.Entity.Equals(request.Entity))
            {
                return CharacterActionResult.Rejected(CharacterActionRejectedReason.EntityMismatch, "Action request entity does not match the controller entity.");
            }

            if (!fromQueue && _hasLastSubmittedRequest && IsDuplicateSameFrame(_lastSubmittedRequest, request))
            {
                return CharacterActionResult.Rejected(CharacterActionRejectedReason.DuplicateSameFrame, "Duplicate same-frame action request.");
            }

            if (request.Kind != CharacterActionKind.Cancel)
            {
                if (_stateMachine.CurrentState == CharacterControlState.Disabled
                    || _stateMachine.CurrentState == CharacterControlState.Reaction)
                {
                    return CharacterActionResult.Rejected(CharacterActionRejectedReason.ControlStateBlocked, "Current control state blocks action requests.");
                }

                if (_stateMachine.IsLocked(CharacterControlLockMask.Action))
                {
                    return CharacterActionResult.Rejected(CharacterActionRejectedReason.ActionLocked, "Action control is locked.");
                }

                var context = new CharacterActionContext(request, _stateMachine.CurrentState, _stateMachine.ControlLockMask);
                for (int i = 0; i < _constraints.Length; i++)
                {
                    CharacterActionConstraintResult result = _constraints[i].Evaluate(context);
                    if (!result.Success)
                    {
                        return CharacterActionResult.Rejected(
                            result.Reason == CharacterActionRejectedReason.None
                                ? CharacterActionRejectedReason.ConstraintRejected
                                : result.Reason,
                            result.Message);
                    }
                }
            }

            return CharacterActionResult.Accepted();
        }

        private CharacterActionResult ExecuteCancel(CharacterActionRequest request)
        {
            if (_combatActionRunner == null)
            {
                CharacterActionResult rejected = CharacterActionResult.Rejected(CharacterActionRejectedReason.MissingCombatActionRunner, "CombatActionRunner is required to cancel combat actions.");
                Emit(CharacterActionEventType.Rejected, request, rejected.RejectedReason, 0, default, rejected.Message);
                return rejected;
            }

            CharacterControlEntityRef entity = ResolveEntity(request);
            if (!entity.HasCombatEntity)
            {
                CharacterActionResult rejected = CharacterActionResult.Rejected(CharacterActionRejectedReason.MissingCombatEntity, "Combat entity id is required to cancel combat actions.");
                Emit(CharacterActionEventType.Rejected, request, rejected.RejectedReason, 0, default, rejected.Message);
                return rejected;
            }

            Emit(CharacterActionEventType.Accepted, request, CharacterActionRejectedReason.None, 0, default, string.Empty);
            bool canceled = _combatActionRunner.ForceCancel(entity.CombatEntityId);
            if (!canceled)
            {
                CharacterActionResult rejected = CharacterActionResult.Rejected(CharacterActionRejectedReason.CancelRejected, "Combat action cancel was rejected because no action is running.");
                Emit(CharacterActionEventType.Rejected, request, rejected.RejectedReason, 0, default, rejected.Message);
                return rejected;
            }

            return CharacterActionResult.Accepted();
        }

        private CharacterActionResult ExecuteCombatAction(CharacterActionRequest request)
        {
            if (_combatActionRunner == null)
            {
                CharacterActionResult rejected = CharacterActionResult.Rejected(CharacterActionRejectedReason.MissingCombatActionRunner, "CombatActionRunner is required to start combat actions.");
                Emit(CharacterActionEventType.Rejected, request, rejected.RejectedReason, 0, default, rejected.Message);
                return rejected;
            }

            CharacterControlEntityRef entity = ResolveEntity(request);
            if (!entity.HasCombatEntity)
            {
                CharacterActionResult rejected = CharacterActionResult.Rejected(CharacterActionRejectedReason.MissingCombatEntity, "Combat entity id is required to start combat actions.");
                Emit(CharacterActionEventType.Rejected, request, rejected.RejectedReason, 0, default, rejected.Message);
                return rejected;
            }

            Emit(CharacterActionEventType.Accepted, request, CharacterActionRejectedReason.None, 0, default, string.Empty);
            CombatFrame combatFrame = ToCombatFrame(request.Frame);
            _pendingCombatActionRequest = request;
            _hasPendingCombatActionRequest = true;
            ActionResult result = request.ForceStart
                ? _combatActionRunner.ForceStartAction(entity.CombatEntityId, request.CombatActionId, combatFrame)
                : _combatActionRunner.StartAction(entity.CombatEntityId, request.CombatActionId, combatFrame);
            if (!result.Success)
            {
                ClearPendingCombatActionRequest(request);

                if (request.QueueIfBusy)
                {
                    _queuedRequest = request;
                    _hasQueuedRequest = true;
                    Emit(CharacterActionEventType.Queued, request, CharacterActionRejectedReason.None, 0, default, result.Reason);
                    return CharacterActionResult.QueuedResult();
                }

                CharacterActionResult rejected = CharacterActionResult.Rejected(CharacterActionRejectedReason.CombatActionRejected, result.Reason);
                Emit(CharacterActionEventType.Rejected, request, rejected.RejectedReason, 0, default, rejected.Message);
                return rejected;
            }

            return CharacterActionResult.Accepted(result.ActionInstanceId);
        }

        private CharacterActionResult ExecuteGameplayAbility(CharacterActionRequest request)
        {
            if (_gameplayCommandBuffer == null)
            {
                CharacterActionResult rejected = CharacterActionResult.Rejected(CharacterActionRejectedReason.MissingGameplayCommandBuffer, "RuntimeCommandBuffer is required to enqueue gameplay ability commands.");
                Emit(CharacterActionEventType.Rejected, request, rejected.RejectedReason, 0, default, rejected.Message);
                return rejected;
            }

            CharacterControlEntityRef entity = ResolveEntity(request);
            if (!entity.HasGameplayEntity)
            {
                CharacterActionResult rejected = CharacterActionResult.Rejected(CharacterActionRejectedReason.MissingGameplayEntity, "Gameplay entity id is required to cast gameplay abilities.");
                Emit(CharacterActionEventType.Rejected, request, rejected.RejectedReason, 0, default, rejected.Message);
                return rejected;
            }

            RuntimeCommand command;
            if (request.HasTarget)
            {
                if (_abilityRequestStore == null)
                {
                    CharacterActionResult rejected = CharacterActionResult.Rejected(CharacterActionRejectedReason.MissingAbilityRequestStore, "GameplayComponentAbilityRequestStore is required for explicit target ability requests.");
                    Emit(CharacterActionEventType.Rejected, request, rejected.RejectedReason, 0, default, rejected.Message);
                    return rejected;
                }

                var abilityRequest = new GameplayComponentAbilityRequest(
                    entity.GameplayEntityId,
                    request.GameplayAbilityId,
                    new[] { request.TargetGameplayEntityId },
                    targetQuery: null);
                GameplayComponentAbilityRequestHandle handle = _abilityRequestStore.Add(abilityRequest);
                command = GameplayRuntimeCommandFactory.CastComponentAbilityRequest(
                    request.Frame,
                    handle,
                    request.GameplayAbilityId,
                    request.SourceId,
                    request.TraceId);
            }
            else
            {
                command = GameplayRuntimeCommandFactory.CastComponentAbility(
                    request.Frame,
                    entity.GameplayEntityId,
                    request.GameplayAbilityId,
                    request.SourceId,
                    request.TraceId);
            }

            Emit(CharacterActionEventType.Accepted, request, CharacterActionRejectedReason.None, 0, default, string.Empty);
            RuntimeCommandValidationResult enqueue = _gameplayCommandBuffer.Enqueue(command);
            if (!enqueue.Success)
            {
                CharacterActionResult rejected = CharacterActionResult.Rejected(CharacterActionRejectedReason.RuntimeCommandRejected, enqueue.Error.Message);
                Emit(CharacterActionEventType.Rejected, request, rejected.RejectedReason, 0, enqueue.Error.Command, rejected.Message);
                return rejected;
            }

            Emit(CharacterActionEventType.GameplayCommandEnqueued, request, CharacterActionRejectedReason.None, 0, enqueue.Command, string.Empty);
            return CharacterActionResult.CommandAccepted(enqueue.Command);
        }

        private void OnCombatActionStarted(ActionStartedEvent evt)
        {
            if (!IsControllerCombatEntity(evt.EntityId))
            {
                return;
            }

            CharacterActionRequest request = _hasPendingCombatActionRequest ? _pendingCombatActionRequest : default;
            _activeCombatActionRequest = request;
            _hasActiveCombatActionRequest = _hasPendingCombatActionRequest;
            _pendingCombatActionRequest = default;
            _hasPendingCombatActionRequest = false;

            _stateMachine.BeginAction(new RuntimeFrame(evt.Frame.Value));
            Emit(CharacterActionEventType.Started, request, CharacterActionRejectedReason.None, evt.ActionInstanceId, default, string.Empty);
        }

        private void OnCombatActionFinished(ActionFinishedEvent evt)
        {
            if (!IsControllerCombatEntity(evt.EntityId))
            {
                return;
            }

            CharacterActionRequest request = GetActiveCombatActionRequest();
            _stateMachine.FinishAction(RuntimeFrameFromActiveCombatState(request));
            Emit(CharacterActionEventType.Finished, request, CharacterActionRejectedReason.None, evt.ActionInstanceId, default, string.Empty);
            ClearActiveCombatActionRequest();
            TrySubmitQueuedRequest();
        }

        private void OnCombatActionCanceled(ActionCanceledEvent evt)
        {
            if (!IsControllerCombatEntity(evt.EntityId))
            {
                return;
            }

            CharacterActionRequest request = GetActiveCombatActionRequest();
            _stateMachine.CancelAction(RuntimeFrameFromActiveCombatState(request), evt.Reason);
            Emit(CharacterActionEventType.Canceled, request, CharacterActionRejectedReason.None, evt.ActionInstanceId, default, evt.Reason);
            bool hasPendingReplacement = _hasPendingCombatActionRequest;
            ClearActiveCombatActionRequest();
            if (!hasPendingReplacement)
            {
                TrySubmitQueuedRequest();
            }
        }

        private void TrySubmitQueuedRequest()
        {
            if (!_hasQueuedRequest)
            {
                return;
            }

            CharacterActionRequest request = _queuedRequest;
            _queuedRequest = default;
            _hasQueuedRequest = false;
            SubmitInternal(request, fromQueue: true);
        }

        private CharacterControlEntityRef ResolveEntity(CharacterActionRequest request)
        {
            return request.Entity.IsValid ? request.Entity : _stateMachine.Entity;
        }

        private bool IsControllerCombatEntity(CombatEntityId entityId)
        {
            return !_stateMachine.Entity.HasCombatEntity || _stateMachine.Entity.CombatEntityId.Equals(entityId);
        }

        private static bool IsDuplicateSameFrame(CharacterActionRequest left, CharacterActionRequest right)
        {
            return left.Frame == right.Frame
                && left.SourceId == right.SourceId
                && left.Entity.Equals(right.Entity)
                && left.Kind == right.Kind
                && left.CombatActionId == right.CombatActionId
                && left.GameplayAbilityId == right.GameplayAbilityId
                && left.TargetGameplayEntityId.Equals(right.TargetGameplayEntityId)
                && string.Equals(left.TraceId, right.TraceId, StringComparison.Ordinal);
        }

        private static CombatFrame ToCombatFrame(RuntimeFrame frame)
        {
            if (frame.Value > int.MaxValue)
                throw new InvalidOperationException("Runtime frame is too large to convert to CombatFrame.");

            return new CombatFrame((int)frame.Value);
        }

        private CharacterActionRequest GetActiveCombatActionRequest()
        {
            if (_hasActiveCombatActionRequest)
            {
                return _activeCombatActionRequest;
            }

            return _hasLastSubmittedRequest ? _lastSubmittedRequest : default;
        }

        private RuntimeFrame RuntimeFrameFromActiveCombatState(CharacterActionRequest request)
        {
            return request.Kind == CharacterActionKind.None ? RuntimeFrame.Zero : request.Frame;
        }

        private void ClearPendingCombatActionRequest(CharacterActionRequest request)
        {
            if (_hasPendingCombatActionRequest && IsDuplicateSameFrame(_pendingCombatActionRequest, request))
            {
                _pendingCombatActionRequest = default;
                _hasPendingCombatActionRequest = false;
            }
        }

        private void ClearActiveCombatActionRequest()
        {
            _activeCombatActionRequest = default;
            _hasActiveCombatActionRequest = false;
        }

        private void Emit(
            CharacterActionEventType type,
            CharacterActionRequest request,
            CharacterActionRejectedReason rejectedReason,
            int actionInstanceId,
            RuntimeCommand command,
            string message)
        {
            ActionEvent?.Invoke(new CharacterActionEvent(type, request, rejectedReason, actionInstanceId, command, message));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterActionController));
        }
    }
}
