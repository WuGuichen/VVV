using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Hit;

namespace MxFramework.CharacterAction
{
    public enum CharacterActionInstanceState
    {
        None = 0,
        Running = 1,
        Finished = 2,
        Cancelled = 3,
        Interrupted = 4,
    }

    public enum CharacterActionRunnerEventKind
    {
        None = 0,
        ActionStarted = 1,
        PhaseChanged = 2,
        TrackEventFired = 3,
        ActionFinished = 4,
        ActionCancelled = 5,
        ActionInterrupted = 6,
        CancelRejected = 7,
        InterruptRejected = 8,
        TrackLifecycle = 9,
    }

    public enum CharacterActionTrackLifecyclePhase
    {
        None = 0,
        Enter = 1,
        Active = 2,
        Exit = 3,
    }

    public enum CharacterActionTrackLifecycleBoundary
    {
        BeforeFrame = 0,
        AfterFrame = 1,
    }

    public enum CharacterActionTrackExitReason
    {
        None = 0,
        NaturalEnd = 1,
        ActionFinished = 2,
        ActionCancelled = 3,
        ActionInterrupted = 4,
        ActionReplaced = 5,
        Death = 6,
        ReviveReset = 7,
        EntityDespawn = 8,
        ConfigurationReload = 9,
        RuntimeShutdown = 10,
        PreviewSeek = 11,
        ReplayRebuild = 12,
    }

    /// <summary>Describes one deterministic lifecycle transition for an action-owned stateful track range.</summary>
    /// <remarks>Inclusive frame range and NoAlloc value contract.</remarks>
    public readonly struct CharacterActionTrackLifecycleEvent : IEquatable<CharacterActionTrackLifecycleEvent>
    {
        public static readonly CharacterActionTrackLifecycleEvent None = new CharacterActionTrackLifecycleEvent();

        public CharacterActionTrackLifecycleEvent(
            long actionInstanceId,
            CharacterActionId actionId,
            int localFrame,
            CharacterActionTrackLifecyclePhase phase,
            CharacterActionTrackLifecycleBoundary boundary,
            CharacterActionTrackExitReason exitReason,
            CharacterActionTrackDispatchEvent dispatch)
        {
            if (phase < CharacterActionTrackLifecyclePhase.Enter || phase > CharacterActionTrackLifecyclePhase.Exit)
                throw new ArgumentOutOfRangeException(nameof(phase));
            if (boundary < CharacterActionTrackLifecycleBoundary.BeforeFrame || boundary > CharacterActionTrackLifecycleBoundary.AfterFrame)
                throw new ArgumentOutOfRangeException(nameof(boundary));
            if (exitReason < CharacterActionTrackExitReason.None || exitReason > CharacterActionTrackExitReason.ReplayRebuild)
                throw new ArgumentOutOfRangeException(nameof(exitReason));
            if (!dispatch.HasEvent)
                throw new ArgumentException("Lifecycle event requires a track dispatch payload.", nameof(dispatch));
            if ((dispatch.StableEventId <= 0))
                throw new ArgumentException("Lifecycle event requires a stable event id.", nameof(dispatch));
            if (phase == CharacterActionTrackLifecyclePhase.Exit && exitReason == CharacterActionTrackExitReason.None)
                throw new ArgumentException("Exit lifecycle event requires an exit reason.", nameof(exitReason));
            if (phase != CharacterActionTrackLifecyclePhase.Exit && exitReason != CharacterActionTrackExitReason.None)
                throw new ArgumentException("Only exit lifecycle events may carry an exit reason.", nameof(exitReason));

            ActionInstanceId = actionInstanceId;
            ActionId = actionId;
            LocalFrame = localFrame;
            Phase = phase;
            Boundary = boundary;
            ExitReason = exitReason;
            Dispatch = dispatch;
        }

        public long ActionInstanceId { get; }
        public CharacterActionId ActionId { get; }
        public CharacterActionTrackKind TrackKind => Dispatch.TrackKind;
        public CharacterActionTrackEventKind EventKind => Dispatch.EventKind;
        public int StableEventId => Dispatch.StableEventId;
        public int StartFrame => Dispatch.Frame;
        public int EndFrame => Dispatch.EndFrame;
        public int LocalFrame { get; }
        public CharacterActionTrackLifecyclePhase Phase { get; }
        public CharacterActionTrackLifecycleBoundary Boundary { get; }
        public CharacterActionTrackExitReason ExitReason { get; }
        public CharacterActionTrackDispatchEvent Dispatch { get; }
        public bool HasEvent => Phase != CharacterActionTrackLifecyclePhase.None;

        public bool Equals(CharacterActionTrackLifecycleEvent other)
        {
            return ActionInstanceId == other.ActionInstanceId
                && ActionId == other.ActionId
                && LocalFrame == other.LocalFrame
                && Phase == other.Phase
                && Boundary == other.Boundary
                && ExitReason == other.ExitReason
                && Dispatch.Equals(other.Dispatch);
        }

        public override bool Equals(object obj) => obj is CharacterActionTrackLifecycleEvent other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ActionInstanceId.GetHashCode();
                hash = (hash * 397) ^ ActionId.GetHashCode();
                hash = (hash * 397) ^ LocalFrame;
                hash = (hash * 397) ^ (int)Phase;
                hash = (hash * 397) ^ (int)Boundary;
                hash = (hash * 397) ^ (int)ExitReason;
                hash = (hash * 397) ^ Dispatch.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct CharacterActionTrackDispatchEvent : IEquatable<CharacterActionTrackDispatchEvent>
    {
        public static readonly CharacterActionTrackDispatchEvent None = new CharacterActionTrackDispatchEvent();

        public CharacterActionTrackDispatchEvent(
            CharacterActionTrackKind trackKind,
            CharacterActionTrackEventKind eventKind,
            int frame,
            int stableEventId = 0,
            CharacterMovementMode movementMode = CharacterMovementMode.Idle,
            float x = 0f,
            float y = 0f,
            float z = 0f,
            int combatActionId = 0,
            int traceProfileId = 0,
            int gameplayRequestId = 0,
            int abilityStableId = 0,
            string animationActionKey = "",
            float transitionSeconds = 0f,
            int presentationCueId = 0,
            string resourceKey = "",
            string debugMarkerId = "",
            string animationClipResourceKey = "",
            int animationStartOffsetFrames = 0,
            int animationEndOffsetFrames = 0,
            float animationPlaybackSpeed = 1f,
            int endFrame = -1,
            float movementControlScale = 1f,
            float rotationControlScale = 1f,
            CharacterActionMotionVectorSpace motionVectorSpace = CharacterActionMotionVectorSpace.World,
            bool hasMovementControlScale = true,
            bool hasRotationControlScale = true,
            int collisionProfileId = 0,
            CharacterActionCollisionProfileRestorePolicy collisionRestorePolicy =
                CharacterActionCollisionProfileRestorePolicy.OnActionEnd,
            int collisionTransitionFrames = 0,
            int equipmentSlotId = 0,
            int targetPolicyId = 0,
            int effectTimelineId = 0,
            CharacterActionDamagePayload damagePayload = default,
            CombatHitEventConfig hitEvent = default,
            CharacterActionHitVolumeConfig hitVolume = default,
            CharacterActionVfxEventConfig vfxConfig = default,
            HitTargetStateFlags targetStateFlags = HitTargetStateFlags.None,
            int stableEventOrder = 0)
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, eventKind, trackKind);
            if (!Enum.IsDefined(typeof(CharacterActionMotionVectorSpace), motionVectorSpace))
                throw new ArgumentOutOfRangeException(nameof(motionVectorSpace), "Motion vector space is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionCollisionProfileRestorePolicy), collisionRestorePolicy))
                throw new ArgumentOutOfRangeException(nameof(collisionRestorePolicy), "Collision profile restore policy is not defined.");

            TrackKind = trackKind;
            EventKind = eventKind;
            Frame = frame;
            StableEventId = stableEventId;
            StableEventOrder = stableEventOrder;
            MovementMode = movementMode;
            X = x;
            Y = y;
            Z = z;
            CombatActionId = combatActionId;
            TraceProfileId = traceProfileId;
            EquipmentSlotId = equipmentSlotId;
            TargetPolicyId = targetPolicyId;
            EffectTimelineId = effectTimelineId;
            DamagePayload = damagePayload;
            HitEvent = hitEvent.WithDefaults(StableEventId);
            HitVolume = hitVolume;
            TargetStateFlags = targetStateFlags;
            GameplayRequestId = gameplayRequestId;
            AbilityStableId = abilityStableId;
            AnimationActionKey = animationActionKey ?? string.Empty;
            AnimationClipResourceKey = animationClipResourceKey ?? string.Empty;
            AnimationStartOffsetFrames = Math.Max(0, animationStartOffsetFrames);
            AnimationEndOffsetFrames = Math.Max(0, animationEndOffsetFrames);
            AnimationPlaybackSpeed = animationPlaybackSpeed <= 0.0001f ? 1f : animationPlaybackSpeed;
            EndFrame = endFrame < 0 ? frame : Math.Max(frame, endFrame);
            TransitionSeconds = transitionSeconds;
            PresentationCueId = presentationCueId;
            ResourceKey = resourceKey ?? string.Empty;
            DebugMarkerId = debugMarkerId ?? string.Empty;
            MovementControlScale = Clamp01(movementControlScale);
            RotationControlScale = Clamp01(rotationControlScale);
            MotionVectorSpace = motionVectorSpace;
            HasMovementControlScale = hasMovementControlScale;
            HasRotationControlScale = hasRotationControlScale;
            CollisionProfileId = collisionProfileId;
            CollisionRestorePolicy = collisionRestorePolicy;
            CollisionTransitionFrames = collisionTransitionFrames < 0 ? 0 : collisionTransitionFrames;
            VfxConfig = !vfxConfig.Attachment.Transform.IsScaleValid()
                ? new CharacterActionVfxEventConfig()
                : vfxConfig;
        }

        public CharacterActionTrackKind TrackKind { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public int Frame { get; }
        public int StableEventId { get; }
        public int StableEventOrder { get; }
        public CharacterMovementMode MovementMode { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public int CombatActionId { get; }
        public int TraceProfileId { get; }
        public int EquipmentSlotId { get; }
        public int TargetPolicyId { get; }
        public int EffectTimelineId { get; }
        public CharacterActionDamagePayload DamagePayload { get; }
        public CombatHitEventConfig HitEvent { get; }
        public CharacterActionHitVolumeConfig HitVolume { get; }
        public HitTargetStateFlags TargetStateFlags { get; }
        public int GameplayRequestId { get; }
        public int AbilityStableId { get; }
        public string AnimationActionKey { get; }
        public string AnimationClipResourceKey { get; }
        public string EffectiveAnimationKey => CharacterActionAnimationKeyUtility.ResolveEffectiveKey(
            AnimationClipResourceKey,
            AnimationActionKey);
        public int AnimationStartOffsetFrames { get; }
        public int AnimationEndOffsetFrames { get; }
        public float AnimationPlaybackSpeed { get; }
        public int EndFrame { get; }
        public float TransitionSeconds { get; }
        public int PresentationCueId { get; }
        public string ResourceKey { get; }
        public string DebugMarkerId { get; }
        public float MovementControlScale { get; }
        public float RotationControlScale { get; }
        public CharacterActionMotionVectorSpace MotionVectorSpace { get; }
        public bool HasMovementControlScale { get; }
        public bool HasRotationControlScale { get; }
        public int CollisionProfileId { get; }
        public CharacterActionCollisionProfileRestorePolicy CollisionRestorePolicy { get; }
        public int CollisionTransitionFrames { get; }
        public CharacterActionVfxEventConfig VfxConfig { get; }
        public bool HasEvent => EventKind != CharacterActionTrackEventKind.None;

        public bool Equals(CharacterActionTrackDispatchEvent other)
        {
            return TrackKind == other.TrackKind
                && EventKind == other.EventKind
                && Frame == other.Frame
                && (StableEventId == other.StableEventId)
                && StableEventOrder == other.StableEventOrder
                && MovementMode == other.MovementMode
                && X.Equals(other.X)
                && Y.Equals(other.Y)
                && Z.Equals(other.Z)
                && (CombatActionId == other.CombatActionId)
                && (TraceProfileId == other.TraceProfileId)
                && (EquipmentSlotId == other.EquipmentSlotId)
                && (TargetPolicyId == other.TargetPolicyId)
                && EffectTimelineId == other.EffectTimelineId
                && DamagePayload.Equals(other.DamagePayload)
                && HitEvent.Equals(other.HitEvent)
                && HitVolume.Equals(other.HitVolume)
                && TargetStateFlags == other.TargetStateFlags
                && (GameplayRequestId == other.GameplayRequestId)
                && (AbilityStableId == other.AbilityStableId)
                && string.Equals(AnimationActionKey, other.AnimationActionKey, StringComparison.Ordinal)
                && string.Equals(AnimationClipResourceKey, other.AnimationClipResourceKey, StringComparison.Ordinal)
                && AnimationStartOffsetFrames == other.AnimationStartOffsetFrames
                && AnimationEndOffsetFrames == other.AnimationEndOffsetFrames
                && AnimationPlaybackSpeed.Equals(other.AnimationPlaybackSpeed)
                && EndFrame == other.EndFrame
                && TransitionSeconds.Equals(other.TransitionSeconds)
                && PresentationCueId == other.PresentationCueId
                && string.Equals(ResourceKey, other.ResourceKey, StringComparison.Ordinal)
                && string.Equals(DebugMarkerId, other.DebugMarkerId, StringComparison.Ordinal)
                && MovementControlScale.Equals(other.MovementControlScale)
                && RotationControlScale.Equals(other.RotationControlScale)
                && MotionVectorSpace == other.MotionVectorSpace
                && HasMovementControlScale == other.HasMovementControlScale
                && HasRotationControlScale == other.HasRotationControlScale
                && (CollisionProfileId == other.CollisionProfileId)
                && CollisionRestorePolicy == other.CollisionRestorePolicy
                && CollisionTransitionFrames == other.CollisionTransitionFrames
                && VfxConfig.Equals(other.VfxConfig);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionTrackDispatchEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)TrackKind;
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ Frame;
                hash = (hash * 397) ^ StableEventId;
                hash = (hash * 397) ^ StableEventOrder;
                hash = (hash * 397) ^ (int)MovementMode;
                hash = (hash * 397) ^ X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                hash = (hash * 397) ^ CombatActionId;
                hash = (hash * 397) ^ TraceProfileId;
                hash = (hash * 397) ^ EquipmentSlotId;
                hash = (hash * 397) ^ TargetPolicyId;
                hash = (hash * 397) ^ EffectTimelineId.GetHashCode();
                hash = (hash * 397) ^ DamagePayload.GetHashCode();
                hash = (hash * 397) ^ HitEvent.GetHashCode();
                hash = (hash * 397) ^ HitVolume.GetHashCode();
                hash = (hash * 397) ^ (int)TargetStateFlags;
                hash = (hash * 397) ^ GameplayRequestId;
                hash = (hash * 397) ^ AbilityStableId;
                hash = (hash * 397) ^ (AnimationActionKey == null ? 0 : AnimationActionKey.GetHashCode());
                hash = (hash * 397) ^ (AnimationClipResourceKey == null ? 0 : AnimationClipResourceKey.GetHashCode());
                hash = (hash * 397) ^ AnimationStartOffsetFrames;
                hash = (hash * 397) ^ AnimationEndOffsetFrames;
                hash = (hash * 397) ^ AnimationPlaybackSpeed.GetHashCode();
                hash = (hash * 397) ^ EndFrame;
                hash = (hash * 397) ^ TransitionSeconds.GetHashCode();
                hash = (hash * 397) ^ PresentationCueId.GetHashCode();
                hash = (hash * 397) ^ (ResourceKey == null ? 0 : ResourceKey.GetHashCode());
                hash = (hash * 397) ^ (DebugMarkerId == null ? 0 : DebugMarkerId.GetHashCode());
                hash = (hash * 397) ^ MovementControlScale.GetHashCode();
                hash = (hash * 397) ^ RotationControlScale.GetHashCode();
                hash = (hash * 397) ^ (int)MotionVectorSpace;
                hash = (hash * 397) ^ HasMovementControlScale.GetHashCode();
                hash = (hash * 397) ^ HasRotationControlScale.GetHashCode();
                hash = (hash * 397) ^ CollisionProfileId;
                hash = (hash * 397) ^ (int)CollisionRestorePolicy;
                hash = (hash * 397) ^ CollisionTransitionFrames;
                hash = (hash * 397) ^ VfxConfig.GetHashCode();
                return hash;
            }
        }

        internal static CharacterActionTrackDispatchEvent FromMotion(MotionTrackEvent trackEvent)
        {
            return new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Motion,
                trackEvent.Kind,
                trackEvent.Frame,
                trackEvent.StableEventId,
                trackEvent.MovementMode,
                trackEvent.X,
                trackEvent.Y,
                trackEvent.Z,
                movementControlScale: trackEvent.MovementControlScale,
                rotationControlScale: trackEvent.RotationControlScale,
                motionVectorSpace: trackEvent.Space,
                hasMovementControlScale: trackEvent.HasMovementControlScale,
                hasRotationControlScale: trackEvent.HasRotationControlScale,
                collisionProfileId: trackEvent.CollisionProfileId,
                collisionRestorePolicy: trackEvent.CollisionRestorePolicy,
                collisionTransitionFrames: trackEvent.CollisionTransitionFrames,
                endFrame: trackEvent.EndFrame, stableEventOrder: trackEvent.StableEventOrder);
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }

        internal static CharacterActionTrackDispatchEvent FromCombat(CombatTrackEvent trackEvent, int fallbackCombatActionId)
        {
            int combatActionId = (trackEvent.CombatActionId <= 0)
                ? fallbackCombatActionId
                : trackEvent.CombatActionId;
            return new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Combat,
                trackEvent.Kind,
                trackEvent.Frame,
                trackEvent.StableEventId,
                combatActionId: combatActionId,
                traceProfileId: trackEvent.TraceProfileId,
                equipmentSlotId: trackEvent.EquipmentSlotId,
                damagePayload: trackEvent.DamagePayload,
                hitEvent: trackEvent.HitEvent,
                hitVolume: trackEvent.HitVolume,
                endFrame: trackEvent.EndFrame,
                targetStateFlags: trackEvent.TargetStateFlags, stableEventOrder: trackEvent.StableEventOrder);
        }

        internal static CharacterActionTrackDispatchEvent FromGameplay(GameplayTrackEvent trackEvent)
        {
            return new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Gameplay,
                trackEvent.Kind,
                trackEvent.Frame,
                trackEvent.StableEventId,
                gameplayRequestId: trackEvent.RequestId,
                abilityStableId: trackEvent.AbilityStableId,
                targetPolicyId: trackEvent.TargetPolicyId,
                effectTimelineId: trackEvent.EffectTimelineId,
                damagePayload: trackEvent.DamagePayload,
                hitEvent: trackEvent.HitEvent, stableEventOrder: trackEvent.StableEventOrder);
        }

        internal static CharacterActionTrackDispatchEvent FromAnimation(AnimationTrackEvent trackEvent)
        {
            return new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Animation,
                trackEvent.Kind,
                trackEvent.Frame,
                trackEvent.StableEventId,
                animationActionKey: trackEvent.AnimationActionKey,
                animationClipResourceKey: trackEvent.AnimationClipResourceKey,
                animationStartOffsetFrames: trackEvent.AnimationStartOffsetFrames,
                animationEndOffsetFrames: trackEvent.AnimationEndOffsetFrames,
                animationPlaybackSpeed: trackEvent.PlaybackSpeed,
                endFrame: trackEvent.EndFrame,
                transitionSeconds: trackEvent.TransitionSeconds, stableEventOrder: trackEvent.StableEventOrder);
        }

        internal static CharacterActionTrackDispatchEvent FromPresentation(PresentationTrackEvent trackEvent)
        {
            return new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Presentation,
                trackEvent.Kind,
                trackEvent.Frame,
                trackEvent.StableEventId,
                presentationCueId: trackEvent.CueId,
                resourceKey: trackEvent.ResourceKey,
                endFrame: trackEvent.EndFrame,
                vfxConfig: trackEvent.VfxConfig, stableEventOrder: trackEvent.StableEventOrder);
        }

        internal static CharacterActionTrackDispatchEvent FromDebug(DebugTrackEvent trackEvent)
        {
            return new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Debug,
                trackEvent.Kind,
                trackEvent.Frame,
                trackEvent.StableEventId,
                debugMarkerId: trackEvent.MarkerId, stableEventOrder: trackEvent.StableEventOrder);
        }
    }

    internal static class CharacterActionTrackLifecycleUtility
    {
        public static bool RequiresStatefulLifecycle(in CharacterActionTrackDispatchEvent dispatch)
        {
            if (!dispatch.HasEvent)
                return false;

            switch (dispatch.EventKind)
            {
                case CharacterActionTrackEventKind.LockMovement:
                case CharacterActionTrackEventKind.SetControlScale:
                case CharacterActionTrackEventKind.SetCollisionProfile:
                case CharacterActionTrackEventKind.SetVerticalVelocity:
                case CharacterActionTrackEventKind.IgnoreGravity:
                case CharacterActionTrackEventKind.SampleRootMotionY:
                case CharacterActionTrackEventKind.HitTraceWindow:
                case CharacterActionTrackEventKind.SetHitTargetState:
                    return true;

                case CharacterActionTrackEventKind.SpawnVisualCue:
                    return dispatch.VfxConfig.PlaybackMode == CharacterActionVfxPlaybackMode.StopAtEventEnd
                        || dispatch.VfxConfig.PlaybackMode == CharacterActionVfxPlaybackMode.LoopUntilEventEnd;

                default:
                    return false;
            }
        }

        public static bool IsStatefulRange(in CharacterActionTrackDispatchEvent dispatch)
        {
            if (!RequiresStatefulLifecycle(in dispatch)
                || dispatch.EndFrame < dispatch.Frame
                || (dispatch.StableEventId <= 0))
                return false;
            return true;
        }
    }

}
