using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestCharacterActionComponent
    {
        public CharacterTestCharacterActionComponent(
            CharacterActionId activeActionId,
            CharacterTestActionStateId state,
            CharacterTestActionPhaseId phase,
            int localFrame,
            CharacterTestActionRejection lastRejectReason,
            string lastAnimationActionKey,
            string animationActionKeyThisFrame,
            float animationTransitionSeconds,
            float animationHoldSeconds,
            float jumpImpulseY,
            CharacterMovementMode movementMode = CharacterMovementMode.Idle,
            bool movementLocked = false,
            float movementControlScale = 1f,
            float rotationControlScale = 1f)
            : this(
                activeActionId,
                state,
                phase,
                localFrame,
                lastRejectReason,
                lastAnimationActionKey,
                animationActionKeyThisFrame,
                CharacterTestDeterministicUnits.SecondsToFrames(animationTransitionSeconds),
                CharacterTestDeterministicUnits.SecondsToFrames(animationHoldSeconds),
                CharacterTestDeterministicUnits.ScaleFactorScale,
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(jumpImpulseY),
                movementMode,
                movementLocked,
                CharacterTestDeterministicUnits.ScaleToMilli(movementControlScale),
                CharacterTestDeterministicUnits.ScaleToMilli(rotationControlScale),
                ComputeStableAudioBindingId(activeActionId),
                deterministic: true)
        {
        }

        private CharacterTestCharacterActionComponent(
            CharacterActionId activeActionId,
            CharacterTestActionStateId state,
            CharacterTestActionPhaseId phase,
            int localFrame,
            CharacterTestActionRejection lastRejectReason,
            string lastAnimationActionKey,
            string animationActionKeyThisFrame,
            int animationTransitionFrames,
            int animationHoldFrames,
            int animationPlaybackSpeedMilli,
            int jumpImpulseYMillimetersPerSecond,
            CharacterMovementMode movementMode,
            bool movementLocked,
            int movementControlScaleMilli,
            int rotationControlScaleMilli,
            int activeActionAudioBindingId,
            bool deterministic,
            int lastAnimationBindingId = 0)
        {
            if ((uint)state > (uint)CharacterTestActionStateId.Active)
                throw new System.ArgumentOutOfRangeException(nameof(state));
            if (!((uint)phase <= (uint)CharacterTestActionPhaseId.Exit
                || (phase >= CharacterTestActionPhaseId.Custom && phase <= CharacterTestActionPhaseId.Reaction)))
                throw new System.ArgumentOutOfRangeException(nameof(phase));
            ActiveActionId = activeActionId;
            ActiveActionAudioBindingId = activeActionAudioBindingId < 0 ? 0 : activeActionAudioBindingId;
            State = state;
            Phase = phase;
            LocalFrame = localFrame;
            LastRejection = lastRejectReason;
            LastAnimationActionKey = lastAnimationActionKey ?? string.Empty;
            LastAnimationBindingId = lastAnimationBindingId;
            AnimationActionKeyThisFrame = animationActionKeyThisFrame ?? string.Empty;
            AnimationTransitionFrames = animationTransitionFrames < 0 ? 0 : animationTransitionFrames;
            AnimationHoldFrames = animationHoldFrames < 0 ? 0 : animationHoldFrames;
            AnimationPlaybackSpeedMilli = animationPlaybackSpeedMilli <= 0
                ? CharacterTestDeterministicUnits.ScaleFactorScale
                : animationPlaybackSpeedMilli;
            JumpImpulseYMillimetersPerSecond = jumpImpulseYMillimetersPerSecond;
            MovementMode = movementMode;
            MovementLocked = movementLocked;
            MovementControlScaleMilli = CharacterTestDeterministicMath.ClampScaleMilli(movementControlScaleMilli);
            RotationControlScaleMilli = CharacterTestDeterministicMath.ClampScaleMilli(rotationControlScaleMilli);
        }

        public CharacterActionId ActiveActionId { get; }
        /// <summary>Stable numeric projection consumed by Physical Audio without string lookup in Contact Tick.</summary>
        /// <remarks>NoAlloc to read. Recomputed only at the action start/restore seam, then preserved by per-frame copies.</remarks>
        internal int ActiveActionAudioBindingId { get; }
        public CharacterTestActionStateId State { get; }
        public CharacterTestActionPhaseId Phase { get; }
        public int LocalFrame { get; }
        public CharacterTestActionRejection LastRejection { get; }
        public int LastRejectReasonCode => LastRejection.Code;
        public int LastRejectDetailId => LastRejection.DetailId;
        public string LastRejectReason => LastRejection.ToString();
        public string LastAnimationActionKey { get; }
        public int LastAnimationBindingId { get; }
        public string AnimationActionKeyThisFrame { get; }
        public int AnimationTransitionFrames { get; }
        public int AnimationHoldFrames { get; }
        public int AnimationPlaybackSpeedMilli { get; }
        public int JumpImpulseYMillimetersPerSecond { get; }
        public CharacterMovementMode MovementMode { get; }
        public bool MovementLocked { get; }
        public int MovementControlScaleMilli { get; }
        public int RotationControlScaleMilli { get; }
        public float AnimationTransitionSeconds =>
            CharacterTestDeterministicUnits.FramesToSeconds(AnimationTransitionFrames);
        public float AnimationHoldSeconds =>
            CharacterTestDeterministicUnits.FramesToSeconds(AnimationHoldFrames);
        public float AnimationPlaybackSpeed =>
            CharacterTestDeterministicUnits.MilliToScale(AnimationPlaybackSpeedMilli);
        public float JumpImpulseY =>
            CharacterTestDeterministicUnits.MillimetersPerSecondToMetersPerSecond(JumpImpulseYMillimetersPerSecond);
        public float MovementControlScale =>
            CharacterTestDeterministicUnits.MilliToScale(MovementControlScaleMilli);
        public float RotationControlScale =>
            CharacterTestDeterministicUnits.MilliToScale(RotationControlScaleMilli);

        public static CharacterTestCharacterActionComponent None =>
            FromDeterministic(
                CharacterActionId.None,
                CharacterTestActionStateId.None,
                CharacterTestActionPhaseId.None,
                0,
                default,
                string.Empty,
                string.Empty,
                0,
                0,
                0);

        public static CharacterTestCharacterActionComponent FromDeterministic(
            CharacterActionId activeActionId,
            CharacterTestActionStateId state,
            CharacterTestActionPhaseId phase,
            int localFrame,
            CharacterTestActionRejection lastRejectReason,
            string lastAnimationActionKey,
            string animationActionKeyThisFrame,
            int animationTransitionFrames,
            int animationHoldFrames,
            int jumpImpulseYMillimetersPerSecond,
            CharacterMovementMode movementMode = CharacterMovementMode.Idle,
            bool movementLocked = false,
            int movementControlScaleMilli = CharacterTestDeterministicUnits.ScaleFactorScale,
            int rotationControlScaleMilli = CharacterTestDeterministicUnits.ScaleFactorScale,
            int animationPlaybackSpeedMilli = CharacterTestDeterministicUnits.ScaleFactorScale,
            int lastAnimationBindingId = 0)
        {
            return new CharacterTestCharacterActionComponent(
                activeActionId,
                state,
                phase,
                localFrame,
                lastRejectReason,
                lastAnimationActionKey,
                animationActionKeyThisFrame,
                animationTransitionFrames,
                animationHoldFrames,
                animationPlaybackSpeedMilli,
                jumpImpulseYMillimetersPerSecond,
                movementMode,
                movementLocked,
                movementControlScaleMilli,
                rotationControlScaleMilli,
                ComputeStableAudioBindingId(activeActionId),
                deterministic: true, lastAnimationBindingId: lastAnimationBindingId);
        }

        private CharacterTestCharacterActionComponent WithDeterministicState(
            CharacterActionId activeActionId,
            CharacterTestActionStateId state,
            CharacterTestActionPhaseId phase,
            int localFrame,
            CharacterTestActionRejection lastRejectReason,
            string lastAnimationActionKey,
            string animationActionKeyThisFrame,
            int animationTransitionFrames,
            int animationHoldFrames,
            int animationPlaybackSpeedMilli,
            int jumpImpulseYMillimetersPerSecond,
            CharacterMovementMode movementMode,
            bool movementLocked,
            int movementControlScaleMilli,
            int rotationControlScaleMilli,
            int activeActionAudioBindingId,
            int? lastAnimationBindingId = null)
        {
            return new CharacterTestCharacterActionComponent(
                activeActionId,
                state,
                phase,
                localFrame,
                lastRejectReason,
                lastAnimationActionKey,
                animationActionKeyThisFrame,
                animationTransitionFrames,
                animationHoldFrames,
                animationPlaybackSpeedMilli,
                jumpImpulseYMillimetersPerSecond,
                movementMode,
                movementLocked,
                movementControlScaleMilli,
                rotationControlScaleMilli,
                activeActionAudioBindingId,
                deterministic: true, lastAnimationBindingId: lastAnimationBindingId ?? LastAnimationBindingId);
        }

        public CharacterTestCharacterActionComponent ClearFrameRequests()
        {
            return WithDeterministicState(
                ActiveActionId,
                State,
                Phase,
                LocalFrame,
                LastRejection,
                LastAnimationActionKey,
                string.Empty,
                0,
                0,
                CharacterTestDeterministicUnits.ScaleFactorScale,
                0,
                MovementMode,
                MovementLocked,
                MovementControlScaleMilli,
                RotationControlScaleMilli,
                ActiveActionAudioBindingId);
        }

        public CharacterTestCharacterActionComponent WithDebug(CharacterActionDebugSnapshot snapshot)
        {
            if (snapshot == null)
                return this;

            return WithDebugValues(
                snapshot.ActionId,
                (CharacterTestActionStateId)snapshot.State,
                (CharacterTestActionPhaseId)snapshot.CurrentPhase,
                snapshot.LocalFrame,
                snapshot.LastRejectReasonCode);
        }

        public CharacterTestCharacterActionComponent WithDebugValues(
            CharacterActionId actionId,
            CharacterTestActionStateId state,
            CharacterTestActionPhaseId phase,
            int localFrame,
            CharacterTestActionRejection lastRejectReason)
        {
            return WithDeterministicState(
                actionId,
                state,
                phase,
                localFrame,
                lastRejectReason,
                LastAnimationActionKey,
                AnimationActionKeyThisFrame,
                AnimationTransitionFrames,
                AnimationHoldFrames,
                AnimationPlaybackSpeedMilli,
                JumpImpulseYMillimetersPerSecond,
                MovementMode,
                MovementLocked,
                MovementControlScaleMilli,
                RotationControlScaleMilli,
                ActiveActionAudioBindingId);
        }

        /// <summary>Updates the active-action projection at the low-frequency action start/restore seam.</summary>
        /// <remarks>NoAlloc. Callers must pass the precomputed stable binding and preserve it during per-frame component copies.</remarks>
        internal CharacterTestCharacterActionComponent WithActiveActionAudioBinding(
            CharacterActionId activeActionId,
            int activeActionAudioBindingId)
        {
            return WithDeterministicState(
                activeActionId,
                State,
                Phase,
                LocalFrame,
                LastRejection,
                LastAnimationActionKey,
                AnimationActionKeyThisFrame,
                AnimationTransitionFrames,
                AnimationHoldFrames,
                AnimationPlaybackSpeedMilli,
                JumpImpulseYMillimetersPerSecond,
                MovementMode,
                MovementLocked,
                MovementControlScaleMilli,
                RotationControlScaleMilli,
                activeActionAudioBindingId);
        }

        internal static string ResolveStateText(CharacterTestActionStateId state)
            => state == CharacterTestActionStateId.Completed ? "Completed"
                : state == CharacterTestActionStateId.Active ? "Active" : ResolveStateText((CharacterActionInstanceState)state);

        internal static string ResolvePhaseText(CharacterTestActionPhaseId phase)
            => phase == CharacterTestActionPhaseId.Death ? "Death"
                : phase == CharacterTestActionPhaseId.Action ? "Action"
                : phase == CharacterTestActionPhaseId.Reaction ? "Reaction" : ResolvePhaseText((CharacterActionPhaseKind)phase);

        internal static string ResolveStateText(CharacterActionInstanceState state)
        {
            switch (state)
            {
                case CharacterActionInstanceState.None:
                    return "None";
                case CharacterActionInstanceState.Running:
                    return "Running";
                case CharacterActionInstanceState.Finished:
                    return "Finished";
                case CharacterActionInstanceState.Cancelled:
                    return "Cancelled";
                case CharacterActionInstanceState.Interrupted:
                    return "Interrupted";
                default:
                    return string.Empty;
            }
        }

        /// <remarks>NoAlloc. Deterministic FNV-1a projection; zero remains reserved for no active action.</remarks>
        internal static int ComputeStableAudioBindingId(CharacterActionId actionId)
        {
            return actionId.IsValid ? actionId.Value : 0;
        }

        internal static string ResolvePhaseText(CharacterActionPhaseKind phase)
        {
            switch (phase)
            {
                case CharacterActionPhaseKind.None:
                    return "None";
                case CharacterActionPhaseKind.Startup:
                    return "Startup";
                case CharacterActionPhaseKind.Active:
                    return "Active";
                case CharacterActionPhaseKind.Recovery:
                    return "Recovery";
                case CharacterActionPhaseKind.Loop:
                    return "Loop";
                case CharacterActionPhaseKind.Airborne:
                    return "Airborne";
                case CharacterActionPhaseKind.Landing:
                    return "Landing";
                case CharacterActionPhaseKind.Channel:
                    return "Channel";
                case CharacterActionPhaseKind.Hold:
                    return "Hold";
                case CharacterActionPhaseKind.Exit:
                    return "Exit";
                case CharacterActionPhaseKind.Custom:
                    return "Custom";
                default:
                    return string.Empty;
            }
        }

        public CharacterTestCharacterActionComponent WithReject(CharacterTestActionRejection reason)
        {
            return WithDeterministicState(
                ActiveActionId,
                State,
                Phase,
                LocalFrame,
                reason,
                LastAnimationActionKey,
                AnimationActionKeyThisFrame,
                AnimationTransitionFrames,
                AnimationHoldFrames,
                AnimationPlaybackSpeedMilli,
                JumpImpulseYMillimetersPerSecond,
                MovementMode,
                MovementLocked,
                MovementControlScaleMilli,
                RotationControlScaleMilli,
                ActiveActionAudioBindingId);
        }

        public CharacterTestCharacterActionComponent WithAnimationRequest(
            string actionKey,
            float transitionSeconds,
            float holdSeconds,
            float playbackSpeed = 1f,
            int bindingId = 0)
        {
            actionKey = actionKey ?? string.Empty;
            return WithDeterministicState(
                ActiveActionId,
                State,
                Phase,
                LocalFrame,
                LastRejection,
                string.IsNullOrEmpty(actionKey) ? LastAnimationActionKey : actionKey,
                actionKey,
                CharacterTestDeterministicUnits.SecondsToFrames(transitionSeconds),
                CharacterTestDeterministicUnits.SecondsToFrames(holdSeconds),
                CharacterTestDeterministicUnits.ScaleToMilli(playbackSpeed),
                JumpImpulseYMillimetersPerSecond,
                MovementMode,
                MovementLocked,
                MovementControlScaleMilli,
                RotationControlScaleMilli,
                ActiveActionAudioBindingId, bindingId > 0 ? bindingId : LastAnimationBindingId);
        }

        public CharacterTestCharacterActionComponent WithJumpImpulse(float impulseY)
        {
            return WithJumpImpulseMillimetersPerSecond(
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(impulseY));
        }

        public CharacterTestCharacterActionComponent WithJumpImpulseMillimetersPerSecond(int impulseYMillimetersPerSecond)
        {
            return WithDeterministicState(
                ActiveActionId,
                State,
                Phase,
                LocalFrame,
                LastRejection,
                LastAnimationActionKey,
                AnimationActionKeyThisFrame,
                AnimationTransitionFrames,
                AnimationHoldFrames,
                AnimationPlaybackSpeedMilli,
                impulseYMillimetersPerSecond,
                MovementMode,
                MovementLocked,
                MovementControlScaleMilli,
                RotationControlScaleMilli,
                ActiveActionAudioBindingId);
        }

        public CharacterTestCharacterActionComponent WithMovementRequest(
            CharacterMovementMode movementMode,
            bool movementLocked)
        {
            return WithDeterministicState(
                ActiveActionId,
                State,
                Phase,
                LocalFrame,
                LastRejection,
                LastAnimationActionKey,
                AnimationActionKeyThisFrame,
                AnimationTransitionFrames,
                AnimationHoldFrames,
                AnimationPlaybackSpeedMilli,
                JumpImpulseYMillimetersPerSecond,
                movementMode,
                movementLocked || MovementLocked,
                MovementControlScaleMilli,
                RotationControlScaleMilli,
                ActiveActionAudioBindingId);
        }

        public CharacterTestCharacterActionComponent WithControlScale(
            CharacterMovementMode movementMode,
            float movementControlScale,
            float rotationControlScale,
            bool movementLocked)
        {
            return WithControlScaleMilli(
                movementMode,
                CharacterTestDeterministicUnits.ScaleToMilli(movementControlScale),
                CharacterTestDeterministicUnits.ScaleToMilli(rotationControlScale),
                movementLocked);
        }

        public CharacterTestCharacterActionComponent WithControlScaleMilli(
            CharacterMovementMode movementMode,
            int movementControlScaleMilli,
            int rotationControlScaleMilli,
            bool movementLocked)
        {
            return WithDeterministicState(
                ActiveActionId,
                State,
                Phase,
                LocalFrame,
                LastRejection,
                LastAnimationActionKey,
                AnimationActionKeyThisFrame,
                AnimationTransitionFrames,
                AnimationHoldFrames,
                AnimationPlaybackSpeedMilli,
                JumpImpulseYMillimetersPerSecond,
                movementMode,
                movementLocked,
                movementControlScaleMilli,
                rotationControlScaleMilli,
                ActiveActionAudioBindingId);
        }
    }
}
