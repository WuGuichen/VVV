using System;
using MxFramework.Runtime;

namespace MxFramework.CharacterControl
{
    public enum CharacterControlState
    {
        Locomotion = 0,
        Action = 1,
        Reaction = 2,
        Disabled = 3
    }

    [Flags]
    public enum CharacterControlLockMask
    {
        None = 0,
        Move = 1 << 0,
        Jump = 1 << 1,
        Action = 1 << 2,
        Facing = 1 << 3,
        All = Move | Jump | Action | Facing
    }

    public enum CharacterControlTransitionReason
    {
        None = 0,
        ActionStarted = 1,
        ActionFinished = 2,
        ActionCanceled = 3,
        ReactionStarted = 4,
        ReactionFinished = 5,
        PressureBreak = 6,
        Disabled = 7,
        Death = 8,
        Cutscene = 9,
        Restored = 10,
        Manual = 11
    }

    public enum CharacterControlEventType
    {
        StateChanged = 0,
        TransitionRejected = 1,
        LockChanged = 2
    }

    public readonly struct CharacterStateChangedEvent
    {
        public CharacterStateChangedEvent(
            CharacterControlEntityRef entity,
            CharacterControlState previousState,
            CharacterControlState currentState,
            CharacterControlTransitionReason reason,
            RuntimeFrame frame,
            RuntimeFrame lastCommandFrame,
            int version,
            CharacterControlLockMask previousLockMask,
            CharacterControlLockMask currentLockMask,
            string message)
        {
            Entity = entity;
            PreviousState = previousState;
            CurrentState = currentState;
            Reason = reason;
            Frame = frame;
            LastCommandFrame = lastCommandFrame;
            Version = version;
            PreviousLockMask = previousLockMask;
            CurrentLockMask = currentLockMask;
            Message = message ?? string.Empty;
        }

        public CharacterControlEntityRef Entity { get; }

        public CharacterControlState PreviousState { get; }

        public CharacterControlState CurrentState { get; }

        public CharacterControlTransitionReason Reason { get; }

        public RuntimeFrame Frame { get; }

        public RuntimeFrame LastCommandFrame { get; }

        public int Version { get; }

        public CharacterControlLockMask PreviousLockMask { get; }

        public CharacterControlLockMask CurrentLockMask { get; }

        public string Message { get; }
    }

    public readonly struct CharacterControlEvent
    {
        public CharacterControlEvent(
            CharacterControlEntityRef entity,
            CharacterControlEventType type,
            CharacterControlState state,
            CharacterControlState requestedState,
            CharacterControlTransitionReason reason,
            RuntimeFrame frame,
            int version,
            CharacterControlLockMask lockMask,
            string message)
        {
            Entity = entity;
            Type = type;
            State = state;
            RequestedState = requestedState;
            Reason = reason;
            Frame = frame;
            Version = version;
            LockMask = lockMask;
            Message = message ?? string.Empty;
        }

        public CharacterControlEntityRef Entity { get; }

        public CharacterControlEventType Type { get; }

        public CharacterControlState State { get; }

        public CharacterControlState RequestedState { get; }

        public CharacterControlTransitionReason Reason { get; }

        public RuntimeFrame Frame { get; }

        public int Version { get; }

        public CharacterControlLockMask LockMask { get; }

        public string Message { get; }
    }

    public readonly struct CharacterControlTransitionResult
    {
        private CharacterControlTransitionResult(
            bool success,
            bool changed,
            CharacterControlState previousState,
            CharacterControlState currentState,
            CharacterControlTransitionReason reason,
            int version,
            string message)
        {
            Success = success;
            Changed = changed;
            PreviousState = previousState;
            CurrentState = currentState;
            Reason = reason;
            Version = version;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }

        public bool Changed { get; }

        public CharacterControlState PreviousState { get; }

        public CharacterControlState CurrentState { get; }

        public CharacterControlTransitionReason Reason { get; }

        public int Version { get; }

        public string Message { get; }

        public static CharacterControlTransitionResult ChangedState(
            CharacterControlState previousState,
            CharacterControlState currentState,
            CharacterControlTransitionReason reason,
            int version,
            string message = "")
        {
            return new CharacterControlTransitionResult(true, true, previousState, currentState, reason, version, message);
        }

        public static CharacterControlTransitionResult Unchanged(
            CharacterControlState state,
            CharacterControlTransitionReason reason,
            int version,
            string message = "")
        {
            return new CharacterControlTransitionResult(true, false, state, state, reason, version, message);
        }

        public static CharacterControlTransitionResult Rejected(
            CharacterControlState currentState,
            CharacterControlState requestedState,
            CharacterControlTransitionReason reason,
            int version,
            string message)
        {
            return new CharacterControlTransitionResult(false, false, currentState, requestedState, reason, version, message);
        }
    }

    public sealed class CharacterControlStateMachine
    {
        private readonly RuntimeStateMachine<CharacterControlState> _machine;

        public CharacterControlStateMachine()
            : this(default)
        {
        }

        public CharacterControlStateMachine(CharacterControlEntityRef entity)
        {
            Entity = entity;
            _machine = new RuntimeStateMachine<CharacterControlState>(
                CharacterControlState.Locomotion,
                CanTransition);
            ControlLockMask = CharacterControlLockMask.None;
            LastCommandFrame = RuntimeFrame.Zero;
        }

        public event Action<CharacterStateChangedEvent> StateChanged;

        public event Action<CharacterControlEvent> ControlEvent;

        public CharacterControlEntityRef Entity { get; }

        public CharacterControlState CurrentState => _machine.Current;

        public CharacterControlLockMask ControlLockMask { get; private set; }

        public int Version => _machine.Version;

        public RuntimeFrame LastCommandFrame { get; private set; }

        public void RecordCommandFrame(RuntimeFrame frame)
        {
            LastCommandFrame = frame;
        }

        public bool IsLocked(CharacterControlLockMask mask)
        {
            return (ControlLockMask & mask) != 0;
        }

        public CharacterControlTransitionResult BeginAction(
            RuntimeFrame frame,
            CharacterControlLockMask lockMask = CharacterControlLockMask.Jump,
            CharacterControlTransitionReason reason = CharacterControlTransitionReason.ActionStarted,
            string message = "")
        {
            return Transition(CharacterControlState.Action, frame, reason, lockMask, message);
        }

        public CharacterControlTransitionResult FinishAction(RuntimeFrame frame, string message = "")
        {
            return Transition(CharacterControlState.Locomotion, frame, CharacterControlTransitionReason.ActionFinished, CharacterControlLockMask.None, message);
        }

        public CharacterControlTransitionResult CancelAction(RuntimeFrame frame, string message = "")
        {
            return Transition(CharacterControlState.Locomotion, frame, CharacterControlTransitionReason.ActionCanceled, CharacterControlLockMask.None, message);
        }

        public CharacterControlTransitionResult BeginReaction(
            RuntimeFrame frame,
            CharacterControlTransitionReason reason = CharacterControlTransitionReason.ReactionStarted,
            string message = "")
        {
            return Transition(
                CharacterControlState.Reaction,
                frame,
                reason,
                CharacterControlLockMask.Move | CharacterControlLockMask.Jump | CharacterControlLockMask.Action,
                message);
        }

        public CharacterControlTransitionResult ApplyPressureBreak(RuntimeFrame frame, string message = "")
        {
            return BeginReaction(frame, CharacterControlTransitionReason.PressureBreak, message);
        }

        public CharacterControlTransitionResult FinishReaction(RuntimeFrame frame, string message = "")
        {
            return Transition(CharacterControlState.Locomotion, frame, CharacterControlTransitionReason.ReactionFinished, CharacterControlLockMask.None, message);
        }

        public CharacterControlTransitionResult Disable(
            RuntimeFrame frame,
            CharacterControlTransitionReason reason = CharacterControlTransitionReason.Disabled,
            CharacterControlLockMask lockMask = CharacterControlLockMask.All,
            string message = "")
        {
            return Transition(CharacterControlState.Disabled, frame, reason, lockMask, message);
        }

        public CharacterControlTransitionResult RestoreLocomotion(RuntimeFrame frame, string message = "")
        {
            return Transition(CharacterControlState.Locomotion, frame, CharacterControlTransitionReason.Restored, CharacterControlLockMask.None, message);
        }

        public bool SetControlLockMask(CharacterControlLockMask lockMask, RuntimeFrame frame, string message = "")
        {
            if (ControlLockMask == lockMask)
            {
                return false;
            }

            ControlLockMask = lockMask;
            ControlEvent?.Invoke(new CharacterControlEvent(
                Entity,
                CharacterControlEventType.LockChanged,
                CurrentState,
                CurrentState,
                CharacterControlTransitionReason.Manual,
                frame,
                Version,
                ControlLockMask,
                message));
            return true;
        }

        private CharacterControlTransitionResult Transition(
            CharacterControlState nextState,
            RuntimeFrame frame,
            CharacterControlTransitionReason reason,
            CharacterControlLockMask lockMask,
            string message)
        {
            CharacterControlState previousState = CurrentState;
            CharacterControlLockMask previousLock = ControlLockMask;
            if (previousState == nextState)
            {
                if (previousLock == lockMask)
                {
                    return CharacterControlTransitionResult.Unchanged(previousState, reason, Version, message);
                }

                SetControlLockMask(lockMask, frame, message);
                return CharacterControlTransitionResult.Unchanged(previousState, reason, Version, message);
            }

            string normalizedReason = reason.ToString();
            if (!_machine.TryTransition(nextState, normalizedReason))
            {
                string rejectMessage = string.IsNullOrEmpty(message)
                    ? "Character control transition is not allowed."
                    : message;
                ControlEvent?.Invoke(new CharacterControlEvent(
                    Entity,
                    CharacterControlEventType.TransitionRejected,
                    previousState,
                    nextState,
                    reason,
                    frame,
                    Version,
                    ControlLockMask,
                    rejectMessage));
                return CharacterControlTransitionResult.Rejected(previousState, nextState, reason, Version, rejectMessage);
            }

            ControlLockMask = lockMask;
            var evt = new CharacterStateChangedEvent(
                Entity,
                previousState,
                nextState,
                reason,
                frame,
                LastCommandFrame,
                Version,
                previousLock,
                ControlLockMask,
                message);
            StateChanged?.Invoke(evt);
            ControlEvent?.Invoke(new CharacterControlEvent(
                Entity,
                CharacterControlEventType.StateChanged,
                nextState,
                nextState,
                reason,
                frame,
                Version,
                ControlLockMask,
                message));
            return CharacterControlTransitionResult.ChangedState(previousState, nextState, reason, Version, message);
        }

        private static bool CanTransition(CharacterControlState current, CharacterControlState next, string reason)
        {
            if (next == CharacterControlState.Disabled)
            {
                return true;
            }

            switch (current)
            {
                case CharacterControlState.Locomotion:
                    return next == CharacterControlState.Action || next == CharacterControlState.Reaction;
                case CharacterControlState.Action:
                    return next == CharacterControlState.Locomotion || next == CharacterControlState.Reaction;
                case CharacterControlState.Reaction:
                    return next == CharacterControlState.Locomotion;
                case CharacterControlState.Disabled:
                    return next == CharacterControlState.Locomotion;
                default:
                    return false;
            }
        }
    }
}
