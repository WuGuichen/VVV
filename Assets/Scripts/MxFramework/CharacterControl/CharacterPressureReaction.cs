using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterControl
{
    public enum CharacterPressureReactionKind
    {
        None = 0,
        PressureBandChanged = 1,
        PostureBreak = 2,
        GuardBreak = 3,
        ArmorBreak = 4
    }

    public enum CharacterPressureReactionEventType
    {
        Recorded = 0,
        ReactionStarted = 1,
        ReactionFinished = 2,
        Rejected = 3
    }

    public enum CharacterPressureReactionRejectedReason
    {
        None = 0,
        MissingGameplayEntityMapping = 1,
        EntityMismatch = 2,
        TransitionRejected = 3
    }

    public sealed class CharacterPressureReactionPolicy
    {
        public int PostureBreakReactionFrames { get; set; } = 12;

        public int GuardBreakReactionFrames { get; set; } = 8;

        public int ArmorBreakReactionFrames { get; set; }

        public CharacterControlLockMask PostureBreakLockMask { get; set; } =
            CharacterControlLockMask.Move | CharacterControlLockMask.Jump | CharacterControlLockMask.Action;

        public CharacterControlLockMask GuardBreakLockMask { get; set; } =
            CharacterControlLockMask.Move | CharacterControlLockMask.Jump | CharacterControlLockMask.Action;

        public CharacterControlLockMask ArmorBreakLockMask { get; set; } =
            CharacterControlLockMask.Action;

        public bool PostureBreakStartsReaction { get; set; } = true;

        public bool GuardBreakStartsReaction { get; set; } = true;

        public bool ArmorBreakStartsReaction { get; set; }

        public bool PostureBreakCancelsAction { get; set; } = true;

        public bool GuardBreakCancelsAction { get; set; } = true;

        public bool ArmorBreakCancelsAction { get; set; }

        public bool BrokenBandChangeStartsReaction { get; set; }
    }

    public readonly struct CharacterPressureReactionResult
    {
        private CharacterPressureReactionResult(
            CharacterPressureReactionKind kind,
            CharacterPressureReactionRejectedReason rejectedReason,
            CharacterControlTransitionResult transitionResult,
            CharacterActionResult actionCancelResult,
            RuntimeFrame frame,
            RuntimeFrame reactionEndFrame,
            CharacterControlLockMask lockMask,
            bool recorded,
            bool reactionStarted,
            bool reactionFinished,
            bool actionCancelRequested,
            string message)
        {
            Kind = kind;
            RejectedReason = rejectedReason;
            TransitionResult = transitionResult;
            ActionCancelResult = actionCancelResult;
            Frame = frame;
            ReactionEndFrame = reactionEndFrame;
            LockMask = lockMask;
            Recorded = recorded;
            ReactionStarted = reactionStarted;
            ReactionFinished = reactionFinished;
            ActionCancelRequested = actionCancelRequested;
            Message = message ?? string.Empty;
        }

        public CharacterPressureReactionKind Kind { get; }

        public CharacterPressureReactionRejectedReason RejectedReason { get; }

        public CharacterControlTransitionResult TransitionResult { get; }

        public CharacterActionResult ActionCancelResult { get; }

        public RuntimeFrame Frame { get; }

        public RuntimeFrame ReactionEndFrame { get; }

        public CharacterControlLockMask LockMask { get; }

        public bool Recorded { get; }

        public bool ReactionStarted { get; }

        public bool ReactionFinished { get; }

        public bool ActionCancelRequested { get; }

        public bool ActionCancelSucceeded => ActionCancelRequested && ActionCancelResult.Success;

        public bool Success => RejectedReason == CharacterPressureReactionRejectedReason.None;

        public string Message { get; }

        public static CharacterPressureReactionResult RecordedOnly(
            CharacterPressureReactionKind kind,
            RuntimeFrame frame,
            string message)
        {
            return new CharacterPressureReactionResult(
                kind,
                CharacterPressureReactionRejectedReason.None,
                default,
                default,
                frame,
                frame,
                CharacterControlLockMask.None,
                recorded: true,
                reactionStarted: false,
                reactionFinished: false,
                actionCancelRequested: false,
                message: message);
        }

        public static CharacterPressureReactionResult Started(
            CharacterPressureReactionKind kind,
            CharacterControlTransitionResult transitionResult,
            CharacterActionResult actionCancelResult,
            RuntimeFrame frame,
            RuntimeFrame reactionEndFrame,
            CharacterControlLockMask lockMask,
            bool actionCancelRequested,
            string message)
        {
            return new CharacterPressureReactionResult(
                kind,
                CharacterPressureReactionRejectedReason.None,
                transitionResult,
                actionCancelResult,
                frame,
                reactionEndFrame,
                lockMask,
                recorded: true,
                reactionStarted: transitionResult.Success,
                reactionFinished: false,
                actionCancelRequested: actionCancelRequested,
                message: message);
        }

        public static CharacterPressureReactionResult Finished(
            CharacterPressureReactionKind kind,
            CharacterControlTransitionResult transitionResult,
            RuntimeFrame frame,
            string message)
        {
            return new CharacterPressureReactionResult(
                kind,
                CharacterPressureReactionRejectedReason.None,
                transitionResult,
                default,
                frame,
                frame,
                CharacterControlLockMask.None,
                recorded: true,
                reactionStarted: false,
                reactionFinished: transitionResult.Success,
                actionCancelRequested: false,
                message: message);
        }

        public static CharacterPressureReactionResult Rejected(
            CharacterPressureReactionKind kind,
            CharacterPressureReactionRejectedReason reason,
            RuntimeFrame frame,
            string message)
        {
            return new CharacterPressureReactionResult(
                kind,
                reason,
                default,
                default,
                frame,
                frame,
                CharacterControlLockMask.None,
                recorded: false,
                reactionStarted: false,
                reactionFinished: false,
                actionCancelRequested: false,
                message: message);
        }
    }

    public readonly struct CharacterPressureReactionEvent
    {
        public CharacterPressureReactionEvent(
            CharacterPressureReactionEventType type,
            CharacterPressureReactionKind kind,
            CharacterControlEntityRef entity,
            GameplayEntityId gameplayEntityId,
            RuntimeFrame frame,
            RuntimeFrame reactionEndFrame,
            PressureBand previousBand,
            PressureBand newBand,
            CharacterPressureReactionRejectedReason rejectedReason,
            CharacterControlLockMask lockMask,
            string message)
        {
            Type = type;
            Kind = kind;
            Entity = entity;
            GameplayEntityId = gameplayEntityId;
            Frame = frame;
            ReactionEndFrame = reactionEndFrame;
            PreviousBand = previousBand;
            NewBand = newBand;
            RejectedReason = rejectedReason;
            LockMask = lockMask;
            Message = message ?? string.Empty;
        }

        public CharacterPressureReactionEventType Type { get; }

        public CharacterPressureReactionKind Kind { get; }

        public CharacterControlEntityRef Entity { get; }

        public GameplayEntityId GameplayEntityId { get; }

        public RuntimeFrame Frame { get; }

        public RuntimeFrame ReactionEndFrame { get; }

        public PressureBand PreviousBand { get; }

        public PressureBand NewBand { get; }

        public CharacterPressureReactionRejectedReason RejectedReason { get; }

        public CharacterControlLockMask LockMask { get; }

        public string Message { get; }
    }

    public sealed class CharacterPressureReactionController
    {
        private readonly CharacterControlStateMachine _stateMachine;
        private readonly CharacterActionController _actionController;
        private readonly CharacterPressureReactionPolicy _policy;
        private RuntimeFrame _activeReactionEndFrame;
        private CharacterPressureReactionKind _activeReactionKind;
        private int _activeReactionVersion;
        private bool _hasActiveReaction;

        public CharacterPressureReactionController(
            CharacterControlStateMachine stateMachine,
            CharacterActionController actionController = null,
            CharacterPressureReactionPolicy policy = null)
        {
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _actionController = actionController;
            _policy = policy ?? new CharacterPressureReactionPolicy();
            ValidatePolicy(_policy);
        }

        public event Action<CharacterPressureReactionEvent> ReactionEvent;

        public bool HasActiveReaction => _hasActiveReaction;

        public RuntimeFrame ActiveReactionEndFrame => _activeReactionEndFrame;

        public CharacterPressureReactionKind ActiveReactionKind => _activeReactionKind;

        public CharacterPressureReactionResult Apply(PostureBreakEvent evt)
        {
            return ApplyReaction(
                CharacterPressureReactionKind.PostureBreak,
                evt.EntityId,
                evt.Frame,
                evt.PreviousBand,
                PressureBand.Broken,
                CharacterControlTransitionReason.PressureBreak,
                _policy.PostureBreakStartsReaction,
                _policy.PostureBreakCancelsAction,
                _policy.PostureBreakReactionFrames,
                _policy.PostureBreakLockMask,
                BuildMessage(CharacterPressureReactionKind.PostureBreak, evt.Reason, evt.TraceId));
        }

        public CharacterPressureReactionResult Apply(GuardBreakEvent evt)
        {
            return ApplyReaction(
                CharacterPressureReactionKind.GuardBreak,
                evt.EntityId,
                evt.Frame,
                evt.PreviousBand,
                PressureBand.Broken,
                CharacterControlTransitionReason.GuardBreak,
                _policy.GuardBreakStartsReaction,
                _policy.GuardBreakCancelsAction,
                _policy.GuardBreakReactionFrames,
                _policy.GuardBreakLockMask,
                BuildMessage(CharacterPressureReactionKind.GuardBreak, evt.Reason, evt.TraceId));
        }

        public CharacterPressureReactionResult Apply(ArmorBreakEvent evt)
        {
            return ApplyReaction(
                CharacterPressureReactionKind.ArmorBreak,
                evt.EntityId,
                evt.Frame,
                PressureBand.Stable,
                PressureBand.Broken,
                CharacterControlTransitionReason.ArmorBreak,
                _policy.ArmorBreakStartsReaction,
                _policy.ArmorBreakCancelsAction,
                _policy.ArmorBreakReactionFrames,
                _policy.ArmorBreakLockMask,
                BuildMessage(CharacterPressureReactionKind.ArmorBreak, string.Empty, evt.TraceId));
        }

        public CharacterPressureReactionResult Apply(PressureBandChangedEvent evt)
        {
            bool startsReaction = _policy.BrokenBandChangeStartsReaction
                && IsEscalatingBandChange(evt)
                && evt.NewBand == PressureBand.Broken;
            return ApplyReaction(
                CharacterPressureReactionKind.PressureBandChanged,
                evt.EntityId,
                evt.Frame,
                evt.PreviousBand,
                evt.NewBand,
                CharacterControlTransitionReason.PressureBreak,
                startsReaction,
                startsReaction && _policy.PostureBreakCancelsAction,
                _policy.PostureBreakReactionFrames,
                _policy.PostureBreakLockMask,
                BuildMessage(CharacterPressureReactionKind.PressureBandChanged, evt.Reason, evt.TraceId));
        }

        public CharacterPressureReactionResult FinishActiveReaction(RuntimeFrame frame, string message = "")
        {
            if (!_hasActiveReaction)
            {
                return CharacterPressureReactionResult.RecordedOnly(
                    CharacterPressureReactionKind.None,
                    frame,
                    string.IsNullOrEmpty(message) ? "No active pressure reaction." : message);
            }

            CharacterPressureReactionKind kind = _activeReactionKind;
            bool ownsReaction = OwnsCurrentReaction();
            ClearActiveReaction();

            if (!ownsReaction)
            {
                string recordedMessage = _stateMachine.CurrentState == CharacterControlState.Reaction
                    ? "Pressure reaction ownership changed before finish."
                    : "Pressure reaction already left Reaction state.";
                CharacterPressureReactionResult recorded = CharacterPressureReactionResult.RecordedOnly(
                    kind,
                    frame,
                    string.IsNullOrEmpty(message) ? recordedMessage : message);
                Emit(CharacterPressureReactionEventType.Recorded, recorded, default, PressureBand.Stable, PressureBand.Stable);
                return recorded;
            }

            string finishMessage = string.IsNullOrEmpty(message)
                ? "Pressure reaction finished."
                : message;
            CharacterControlTransitionResult transition = _stateMachine.FinishReaction(frame, finishMessage);
            CharacterPressureReactionResult result = CharacterPressureReactionResult.Finished(
                kind,
                transition,
                frame,
                finishMessage);
            Emit(CharacterPressureReactionEventType.ReactionFinished, result, default, PressureBand.Stable, PressureBand.Stable);
            return result;
        }

        public bool TryFinishExpiredReaction(RuntimeFrame frame, out CharacterPressureReactionResult result)
        {
            result = default;
            if (!_hasActiveReaction)
            {
                return false;
            }

            if (frame < _activeReactionEndFrame)
            {
                return false;
            }

            CharacterPressureReactionKind kind = _activeReactionKind;
            bool ownsReaction = OwnsCurrentReaction();
            ClearActiveReaction();

            if (!ownsReaction)
            {
                string recordedMessage = _stateMachine.CurrentState == CharacterControlState.Reaction
                    ? "Pressure reaction ownership changed before finish."
                    : "Pressure reaction already left Reaction state.";
                result = CharacterPressureReactionResult.RecordedOnly(kind, frame, recordedMessage);
                Emit(CharacterPressureReactionEventType.Recorded, result, default, PressureBand.Stable, PressureBand.Stable);
                return true;
            }

            CharacterControlTransitionResult transition = _stateMachine.FinishReaction(frame, "Pressure reaction window finished.");
            result = CharacterPressureReactionResult.Finished(kind, transition, frame, "Pressure reaction window finished.");
            Emit(CharacterPressureReactionEventType.ReactionFinished, result, default, PressureBand.Stable, PressureBand.Stable);
            return true;
        }

        private CharacterPressureReactionResult ApplyReaction(
            CharacterPressureReactionKind kind,
            GameplayEntityId gameplayEntityId,
            RuntimeFrame frame,
            PressureBand previousBand,
            PressureBand newBand,
            CharacterControlTransitionReason transitionReason,
            bool startsReaction,
            bool cancelsAction,
            int durationFrames,
            CharacterControlLockMask lockMask,
            string message)
        {
            if (!TryValidateEntity(kind, gameplayEntityId, frame, out CharacterPressureReactionResult rejected))
            {
                Emit(CharacterPressureReactionEventType.Rejected, rejected, gameplayEntityId, previousBand, newBand);
                return rejected;
            }

            if (!startsReaction)
            {
                CharacterPressureReactionResult recorded = CharacterPressureReactionResult.RecordedOnly(kind, frame, message);
                Emit(CharacterPressureReactionEventType.Recorded, recorded, gameplayEntityId, previousBand, newBand);
                return recorded;
            }

            bool ownsCurrentReaction = OwnsCurrentReaction();
            bool currentReactionIsForeign = _stateMachine.CurrentState == CharacterControlState.Reaction && !ownsCurrentReaction;
            if (_hasActiveReaction && !ownsCurrentReaction)
            {
                ClearActiveReaction();
            }

            if (currentReactionIsForeign)
            {
                CharacterPressureReactionResult recorded = CharacterPressureReactionResult.RecordedOnly(
                    kind,
                    frame,
                    "Reaction state is already owned by another source.");
                Emit(CharacterPressureReactionEventType.Recorded, recorded, gameplayEntityId, previousBand, newBand);
                return recorded;
            }

            bool shouldCancel = cancelsAction && _stateMachine.CurrentState == CharacterControlState.Action;
            CharacterActionResult cancelResult = default;
            if (shouldCancel && _actionController != null)
            {
                _actionController.ClearQueuedRequest();
                cancelResult = _actionController.Submit(CharacterActionRequest.Cancel(
                    frame,
                    _stateMachine.Entity,
                    traceId: BuildCancelTrace(kind, frame)));
            }

            RuntimeFrame reactionEndFrame = AddFrames(frame, durationFrames);
            CharacterControlTransitionResult transition = _stateMachine.BeginReaction(
                frame,
                transitionReason,
                lockMask,
                message);
            if (!transition.Success)
            {
                CharacterPressureReactionResult transitionRejected = CharacterPressureReactionResult.Rejected(
                    kind,
                    CharacterPressureReactionRejectedReason.TransitionRejected,
                    frame,
                    transition.Message);
                Emit(CharacterPressureReactionEventType.Rejected, transitionRejected, gameplayEntityId, previousBand, newBand);
                return transitionRejected;
            }

            _hasActiveReaction = true;
            _activeReactionKind = kind;
            _activeReactionEndFrame = reactionEndFrame;
            _activeReactionVersion = transition.Version;

            CharacterPressureReactionResult result = CharacterPressureReactionResult.Started(
                kind,
                transition,
                cancelResult,
                frame,
                reactionEndFrame,
                lockMask,
                shouldCancel && _actionController != null,
                message);
            Emit(CharacterPressureReactionEventType.ReactionStarted, result, gameplayEntityId, previousBand, newBand);
            return result;
        }

        private bool OwnsCurrentReaction()
        {
            return _hasActiveReaction
                && _stateMachine.CurrentState == CharacterControlState.Reaction
                && _stateMachine.Version == _activeReactionVersion;
        }

        private void ClearActiveReaction()
        {
            _hasActiveReaction = false;
            _activeReactionKind = CharacterPressureReactionKind.None;
            _activeReactionEndFrame = default;
            _activeReactionVersion = 0;
        }

        private bool TryValidateEntity(
            CharacterPressureReactionKind kind,
            GameplayEntityId gameplayEntityId,
            RuntimeFrame frame,
            out CharacterPressureReactionResult rejected)
        {
            if (!_stateMachine.Entity.HasGameplayEntity)
            {
                rejected = CharacterPressureReactionResult.Rejected(
                    kind,
                    CharacterPressureReactionRejectedReason.MissingGameplayEntityMapping,
                    frame,
                    "Character control entity has no GameplayEntityId mapping.");
                return false;
            }

            if (!_stateMachine.Entity.GameplayEntityId.Equals(gameplayEntityId))
            {
                rejected = CharacterPressureReactionResult.Rejected(
                    kind,
                    CharacterPressureReactionRejectedReason.EntityMismatch,
                    frame,
                    "Pressure event GameplayEntityId does not match the character control entity.");
                return false;
            }

            rejected = default;
            return true;
        }

        private void Emit(
            CharacterPressureReactionEventType type,
            CharacterPressureReactionResult result,
            GameplayEntityId gameplayEntityId,
            PressureBand previousBand,
            PressureBand newBand)
        {
            ReactionEvent?.Invoke(new CharacterPressureReactionEvent(
                type,
                result.Kind,
                _stateMachine.Entity,
                gameplayEntityId,
                result.Frame,
                result.ReactionEndFrame,
                previousBand,
                newBand,
                result.RejectedReason,
                result.LockMask,
                result.Message));
        }

        private static RuntimeFrame AddFrames(RuntimeFrame frame, int frames)
        {
            if (frames <= 0)
            {
                return frame;
            }

            return new RuntimeFrame(checked(frame.Value + frames));
        }

        private static bool IsEscalatingBandChange(PressureBandChangedEvent evt)
        {
            return evt.Delta > 0 && (int)evt.NewBand > (int)evt.PreviousBand;
        }

        private static string BuildCancelTrace(CharacterPressureReactionKind kind, RuntimeFrame frame)
        {
            return "character-pressure-reaction:" + frame.Value + ":" + kind;
        }

        private static string BuildMessage(CharacterPressureReactionKind kind, string reason, string traceId)
        {
            string text = "Character pressure reaction: " + kind;
            if (!string.IsNullOrEmpty(reason))
            {
                text += " reason=" + reason;
            }

            if (!string.IsNullOrEmpty(traceId))
            {
                text += " trace=" + traceId;
            }

            return text;
        }

        private static void ValidatePolicy(CharacterPressureReactionPolicy policy)
        {
            if (policy.PostureBreakReactionFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(policy.PostureBreakReactionFrames));
            if (policy.GuardBreakReactionFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(policy.GuardBreakReactionFrames));
            if (policy.ArmorBreakReactionFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(policy.ArmorBreakReactionFrames));
        }
    }
}
