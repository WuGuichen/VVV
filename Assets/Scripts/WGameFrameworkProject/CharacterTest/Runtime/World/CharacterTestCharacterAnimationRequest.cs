using System;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestCharacterAnimationRequestStatus
    {
        None = 0,
        Pending = 1,
        Emitted = 2,
        Expired = 3
    }

    internal readonly struct CharacterTestCharacterAnimationRequestComponent
    {
        public const int DefaultLifetimeFrames = 2;

        public static CharacterTestCharacterAnimationRequestComponent None =>
            new CharacterTestCharacterAnimationRequestComponent(
                CharacterTestCharacterAnimationRequestStatus.None,
                createdFrame: -1L,
                expiresFrame: -1L,
                emittedFrame: -1L,
                actionId: CharacterActionId.None,
                localFrame: -1,
                animationKey: string.Empty,
                transitionFrames: 0,
                holdFrames: 0,
                playbackSpeedMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                stableEventId: 0);

        private CharacterTestCharacterAnimationRequestComponent(
            CharacterTestCharacterAnimationRequestStatus status,
            long createdFrame,
            long expiresFrame,
            long emittedFrame,
            CharacterActionId actionId,
            int localFrame,
            string animationKey,
            int transitionFrames,
            int holdFrames,
            int playbackSpeedMilli,
            int stableEventId,
            int bindingId = 0,
            string clipResourceKey = "",
            int authorityBindingId = 0)
        {
            bool isValid = status != CharacterTestCharacterAnimationRequestStatus.None
                && createdFrame >= 0L
                && bindingId > 0;
            Status = isValid ? status : CharacterTestCharacterAnimationRequestStatus.None;
            CreatedFrame = isValid ? createdFrame : -1L;
            ExpiresFrame = isValid ? Math.Max(createdFrame, expiresFrame) : -1L;
            EmittedFrame = isValid ? emittedFrame : -1L;
            ActionId = actionId;
            LocalFrame = isValid ? localFrame : -1;
            AnimationKey = isValid ? animationKey : string.Empty;
            TransitionFrames = transitionFrames < 0 ? 0 : transitionFrames;
            HoldFrames = holdFrames < 0 ? 0 : holdFrames;
            PlaybackSpeedMilli = playbackSpeedMilli <= 0
                ? CharacterTestDeterministicUnits.ScaleFactorScale
                : playbackSpeedMilli;
            StableEventId = stableEventId;
            BindingId = isValid ? bindingId : 0;
            AuthorityBindingId = isValid ? authorityBindingId : 0;
            ClipResourceKey = clipResourceKey ?? string.Empty;
        }

        public CharacterTestCharacterAnimationRequestStatus Status { get; }
        public bool IsValid => Status != CharacterTestCharacterAnimationRequestStatus.None
            && CreatedFrame >= 0L
            && BindingId > 0;
        public bool IsPending => Status == CharacterTestCharacterAnimationRequestStatus.Pending && IsValid;
        public bool IsEmitted => Status == CharacterTestCharacterAnimationRequestStatus.Emitted && IsValid;
        public bool IsExpired => Status == CharacterTestCharacterAnimationRequestStatus.Expired && IsValid;
        public long CreatedFrame { get; }
        public long Frame => CreatedFrame;
        public long ExpiresFrame { get; }
        public long EmittedFrame { get; }
        public CharacterActionId ActionId { get; }
        public int LocalFrame { get; }
        public string AnimationKey { get; }
        public int TransitionFrames { get; }
        public int HoldFrames { get; }
        public int PlaybackSpeedMilli { get; }
        public int StableEventId { get; }
        public int BindingId { get; }
        public int AuthorityBindingId { get; }
        public string ClipResourceKey { get; }
        public float TransitionSeconds =>
            CharacterTestDeterministicUnits.FramesToSeconds(TransitionFrames);
        public float HoldSeconds =>
            CharacterTestDeterministicUnits.FramesToSeconds(HoldFrames);
        public float PlaybackSpeed =>
            CharacterTestDeterministicUnits.MilliToScale(PlaybackSpeedMilli);

        public static CharacterTestCharacterAnimationRequestComponent FromDeterministic(
            long frame,
            CharacterActionId actionId,
            int localFrame,
            string animationKey,
            int transitionFrames,
            int holdFrames,
            int playbackSpeedMilli,
            int stableEventId,
            int lifetimeFrames = DefaultLifetimeFrames,
            int bindingId = 0,
            string clipResourceKey = "",
            int authorityBindingId = 0)
        {
            int safeLifetimeFrames = lifetimeFrames < 0 ? 0 : lifetimeFrames;
            return new CharacterTestCharacterAnimationRequestComponent(
                CharacterTestCharacterAnimationRequestStatus.Pending,
                createdFrame: frame,
                expiresFrame: frame + safeLifetimeFrames,
                emittedFrame: -1L,
                actionId: actionId,
                localFrame: localFrame,
                animationKey: animationKey,
                transitionFrames: transitionFrames,
                holdFrames: holdFrames,
                playbackSpeedMilli: playbackSpeedMilli,
                stableEventId: stableEventId, bindingId: bindingId, clipResourceKey: clipResourceKey, authorityBindingId: authorityBindingId);
        }

        public bool CanEmit(long frame, CharacterActionId activeActionId)
        {
            return IsPending
                && frame >= CreatedFrame
                && frame <= ExpiresFrame
                && ActionId == activeActionId;
        }

        public bool IsPastExpiry(long frame)
        {
            return IsPending && frame > ExpiresFrame;
        }

        public CharacterTestCharacterAnimationRequestComponent MarkEmitted(long frame)
        {
            if (!IsPending)
                return this;

            return new CharacterTestCharacterAnimationRequestComponent(
                CharacterTestCharacterAnimationRequestStatus.Emitted,
                CreatedFrame,
                ExpiresFrame,
                frame,
                ActionId,
                LocalFrame,
                AnimationKey,
                TransitionFrames,
                HoldFrames,
                PlaybackSpeedMilli,
                StableEventId, BindingId, ClipResourceKey, AuthorityBindingId);
        }

        public CharacterTestCharacterAnimationRequestComponent MarkExpired(long frame)
        {
            if (!IsPending)
                return this;

            return new CharacterTestCharacterAnimationRequestComponent(
                CharacterTestCharacterAnimationRequestStatus.Expired,
                CreatedFrame,
                ExpiresFrame,
                frame,
                ActionId,
                LocalFrame,
                AnimationKey,
                TransitionFrames,
                HoldFrames,
                PlaybackSpeedMilli,
                StableEventId, BindingId, ClipResourceKey, AuthorityBindingId);
        }

        internal static CharacterTestCharacterAnimationRequestComponent Restore(
            CharacterTestCharacterAnimationRequestStatus status, long createdFrame, long expiresFrame, long emittedFrame,
            CharacterActionId actionId, int localFrame, int bindingId, string animationKey, string clipResourceKey,
            int transitionFrames, int holdFrames, int playbackSpeedMilli, int stableEventId, int authorityBindingId)
        {
            return new CharacterTestCharacterAnimationRequestComponent(status, createdFrame, expiresFrame, emittedFrame,
                actionId, localFrame, animationKey, transitionFrames, holdFrames, playbackSpeedMilli, stableEventId, bindingId, clipResourceKey, authorityBindingId);
        }

        public long TerminalFrame =>
            EmittedFrame >= 0L ? EmittedFrame : ExpiresFrame;
    }
}
