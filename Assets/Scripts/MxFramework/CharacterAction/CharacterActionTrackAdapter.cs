using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterAction
{
    public readonly struct CharacterActionAdapterContext : IEquatable<CharacterActionAdapterContext>
    {
        public CharacterActionAdapterContext(
            RuntimeFrame frame,
            GameplayEntityId gameplayEntityId,
            CombatEntityId combatEntityId,
            CombatBodyId combatBodyId,
            int sourceId = 0,
            string traceId = "")
        {
            Frame = frame;
            GameplayEntityId = gameplayEntityId;
            CombatEntityId = combatEntityId;
            CombatBodyId = combatBodyId;
            SourceId = sourceId;
            TraceId = traceId ?? string.Empty;
        }

        public RuntimeFrame Frame { get; }
        public GameplayEntityId GameplayEntityId { get; }
        public CombatEntityId CombatEntityId { get; }
        public CombatBodyId CombatBodyId { get; }
        public int SourceId { get; }
        public string TraceId { get; }
        public bool HasGameplayEntity => GameplayEntityId.IsValid;
        public bool HasCombatEntity => !CombatEntityId.IsNone;
        public bool HasCombatBody => !CombatBodyId.IsNone;

        public bool Equals(CharacterActionAdapterContext other)
        {
            return Frame.Equals(other.Frame)
                && GameplayEntityId.Equals(other.GameplayEntityId)
                && CombatEntityId.Equals(other.CombatEntityId)
                && CombatBodyId.Equals(other.CombatBodyId)
                && SourceId == other.SourceId
                && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionAdapterContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ GameplayEntityId.GetHashCode();
                hash = (hash * 397) ^ CombatEntityId.GetHashCode();
                hash = (hash * 397) ^ CombatBodyId.GetHashCode();
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ (TraceId == null ? 0 : StringComparer.Ordinal.GetHashCode(TraceId));
                return hash;
            }
        }
    }

    public readonly struct CharacterActionAdapterDispatchMetadata : IEquatable<CharacterActionAdapterDispatchMetadata>
    {
        public CharacterActionAdapterDispatchMetadata(
            RuntimeFrame frame,
            long instanceId,
            long planId,
            string actionId,
            int localFrame,
            string stableEventId,
            int sourceId,
            string traceId)
        {
            if (instanceId < 0L)
                throw new ArgumentOutOfRangeException(nameof(instanceId), "Action instance id cannot be negative.");
            if (planId < 0L)
                throw new ArgumentOutOfRangeException(nameof(planId), "Action plan id cannot be negative.");
            if (localFrame < 0)
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Action local frame cannot be negative.");

            Frame = frame;
            InstanceId = instanceId;
            PlanId = planId;
            ActionId = actionId ?? string.Empty;
            LocalFrame = localFrame;
            StableEventId = stableEventId ?? string.Empty;
            SourceId = sourceId;
            TraceId = traceId ?? string.Empty;
        }

        public RuntimeFrame Frame { get; }
        public long InstanceId { get; }
        public long PlanId { get; }
        public string ActionId { get; }
        public int LocalFrame { get; }
        public string StableEventId { get; }
        public int SourceId { get; }
        public string TraceId { get; }

        public bool Equals(CharacterActionAdapterDispatchMetadata other)
        {
            return Frame.Equals(other.Frame)
                && InstanceId == other.InstanceId
                && PlanId == other.PlanId
                && string.Equals(ActionId, other.ActionId, StringComparison.Ordinal)
                && LocalFrame == other.LocalFrame
                && string.Equals(StableEventId, other.StableEventId, StringComparison.Ordinal)
                && SourceId == other.SourceId
                && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionAdapterDispatchMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ InstanceId.GetHashCode();
                hash = (hash * 397) ^ PlanId.GetHashCode();
                hash = (hash * 397) ^ (ActionId == null ? 0 : StringComparer.Ordinal.GetHashCode(ActionId));
                hash = (hash * 397) ^ LocalFrame;
                hash = (hash * 397) ^ (StableEventId == null ? 0 : StringComparer.Ordinal.GetHashCode(StableEventId));
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ (TraceId == null ? 0 : StringComparer.Ordinal.GetHashCode(TraceId));
                return hash;
            }
        }
    }

    public readonly struct CharacterActionMotionRequest : IEquatable<CharacterActionMotionRequest>
    {
        public CharacterActionMotionRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            GameplayEntityId gameplayEntityId,
            CombatBodyId combatBodyId,
            CharacterActionTrackEventKind eventKind,
            CharacterMovementMode movementMode,
            float x,
            float y,
            float z)
        {
            EnsureMotionKind(eventKind);
            if (!Enum.IsDefined(typeof(CharacterMovementMode), movementMode))
                throw new ArgumentOutOfRangeException(nameof(movementMode), "Movement mode is not defined.");

            Metadata = metadata;
            GameplayEntityId = gameplayEntityId;
            CombatBodyId = combatBodyId;
            EventKind = eventKind;
            MovementMode = movementMode;
            X = x;
            Y = y;
            Z = z;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public GameplayEntityId GameplayEntityId { get; }
        public CombatBodyId CombatBodyId { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public CharacterMovementMode MovementMode { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public bool Equals(CharacterActionMotionRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && GameplayEntityId.Equals(other.GameplayEntityId)
                && CombatBodyId.Equals(other.CombatBodyId)
                && EventKind == other.EventKind
                && MovementMode == other.MovementMode
                && X.Equals(other.X)
                && Y.Equals(other.Y)
                && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionMotionRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Metadata.GetHashCode();
                hash = (hash * 397) ^ GameplayEntityId.GetHashCode();
                hash = (hash * 397) ^ CombatBodyId.GetHashCode();
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ (int)MovementMode;
                hash = (hash * 397) ^ X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                return hash;
            }
        }

        private static void EnsureMotionKind(CharacterActionTrackEventKind eventKind)
        {
            if (eventKind != CharacterActionTrackEventKind.SetMovementMode
                && eventKind != CharacterActionTrackEventKind.ApplyImpulse
                && eventKind != CharacterActionTrackEventKind.LockMovement)
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Event kind is not a MotionTrack request.");
            }
        }
    }

    public readonly struct CharacterActionCombatRequest : IEquatable<CharacterActionCombatRequest>
    {
        public CharacterActionCombatRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            CombatEntityId combatEntityId,
            CharacterActionTrackEventKind eventKind,
            string combatActionId,
            string traceProfileId)
        {
            EnsureCombatKind(eventKind);
            Metadata = metadata;
            CombatEntityId = combatEntityId;
            EventKind = eventKind;
            CombatActionId = combatActionId ?? string.Empty;
            TraceProfileId = traceProfileId ?? string.Empty;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public CombatEntityId CombatEntityId { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public string CombatActionId { get; }
        public string TraceProfileId { get; }

        public bool Equals(CharacterActionCombatRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && CombatEntityId.Equals(other.CombatEntityId)
                && EventKind == other.EventKind
                && string.Equals(CombatActionId, other.CombatActionId, StringComparison.Ordinal)
                && string.Equals(TraceProfileId, other.TraceProfileId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionCombatRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Metadata.GetHashCode();
                hash = (hash * 397) ^ CombatEntityId.GetHashCode();
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ (CombatActionId == null ? 0 : StringComparer.Ordinal.GetHashCode(CombatActionId));
                hash = (hash * 397) ^ (TraceProfileId == null ? 0 : StringComparer.Ordinal.GetHashCode(TraceProfileId));
                return hash;
            }
        }

        private static void EnsureCombatKind(CharacterActionTrackEventKind eventKind)
        {
            if (eventKind != CharacterActionTrackEventKind.StartCombatAction
                && eventKind != CharacterActionTrackEventKind.StartHitTrace
                && eventKind != CharacterActionTrackEventKind.StopHitTrace)
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Event kind is not a CombatTrack request.");
            }
        }
    }

    public readonly struct CharacterActionGameplayRequest : IEquatable<CharacterActionGameplayRequest>
    {
        public CharacterActionGameplayRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            GameplayEntityId gameplayEntityId,
            CharacterActionTrackEventKind eventKind,
            string requestId,
            string abilityStableId)
        {
            EnsureGameplayKind(eventKind);
            Metadata = metadata;
            GameplayEntityId = gameplayEntityId;
            EventKind = eventKind;
            RequestId = requestId ?? string.Empty;
            AbilityStableId = abilityStableId ?? string.Empty;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public GameplayEntityId GameplayEntityId { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public string RequestId { get; }
        public string AbilityStableId { get; }

        public bool Equals(CharacterActionGameplayRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && GameplayEntityId.Equals(other.GameplayEntityId)
                && EventKind == other.EventKind
                && string.Equals(RequestId, other.RequestId, StringComparison.Ordinal)
                && string.Equals(AbilityStableId, other.AbilityStableId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionGameplayRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Metadata.GetHashCode();
                hash = (hash * 397) ^ GameplayEntityId.GetHashCode();
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ (RequestId == null ? 0 : StringComparer.Ordinal.GetHashCode(RequestId));
                hash = (hash * 397) ^ (AbilityStableId == null ? 0 : StringComparer.Ordinal.GetHashCode(AbilityStableId));
                return hash;
            }
        }

        private static void EnsureGameplayKind(CharacterActionTrackEventKind eventKind)
        {
            if (eventKind != CharacterActionTrackEventKind.SendGameplayRequest
                && eventKind != CharacterActionTrackEventKind.CastAbility
                && eventKind != CharacterActionTrackEventKind.ApplyGameplayEffect)
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Event kind is not a GameplayTrack request.");
            }
        }
    }

    public readonly struct CharacterActionPresentationAdapterContext : IEquatable<CharacterActionPresentationAdapterContext>
    {
        public CharacterActionPresentationAdapterContext(
            RuntimeFrame frame,
            string targetActorId,
            int sourceId = 0,
            string traceId = "",
            string animationLayerId = "base")
        {
            Frame = frame;
            TargetActorId = targetActorId ?? string.Empty;
            SourceId = sourceId;
            TraceId = traceId ?? string.Empty;
            AnimationLayerId = string.IsNullOrWhiteSpace(animationLayerId) ? "base" : animationLayerId;
        }

        public RuntimeFrame Frame { get; }
        public string TargetActorId { get; }
        public int SourceId { get; }
        public string TraceId { get; }
        public string AnimationLayerId { get; }
        public bool HasTargetActor => !string.IsNullOrEmpty(TargetActorId);

        public bool Equals(CharacterActionPresentationAdapterContext other)
        {
            return Frame.Equals(other.Frame)
                && string.Equals(TargetActorId, other.TargetActorId, StringComparison.Ordinal)
                && SourceId == other.SourceId
                && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal)
                && string.Equals(AnimationLayerId, other.AnimationLayerId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionPresentationAdapterContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ (TargetActorId == null ? 0 : StringComparer.Ordinal.GetHashCode(TargetActorId));
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ (TraceId == null ? 0 : StringComparer.Ordinal.GetHashCode(TraceId));
                hash = (hash * 397) ^ (AnimationLayerId == null ? 0 : StringComparer.Ordinal.GetHashCode(AnimationLayerId));
                return hash;
            }
        }
    }

    public readonly struct CharacterActionAnimationRequest : IEquatable<CharacterActionAnimationRequest>
    {
        public CharacterActionAnimationRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            string targetActorId,
            CharacterActionTrackEventKind eventKind,
            string actionKey,
            string layerId,
            float transitionSeconds)
        {
            EnsureAnimationKind(eventKind);
            Metadata = metadata;
            TargetActorId = targetActorId ?? string.Empty;
            EventKind = eventKind;
            ActionKey = actionKey ?? string.Empty;
            LayerId = string.IsNullOrWhiteSpace(layerId) ? "base" : layerId;
            TransitionSeconds = transitionSeconds;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public string TargetActorId { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public string ActionKey { get; }
        public string LayerId { get; }
        public float TransitionSeconds { get; }

        public bool Equals(CharacterActionAnimationRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && string.Equals(TargetActorId, other.TargetActorId, StringComparison.Ordinal)
                && EventKind == other.EventKind
                && string.Equals(ActionKey, other.ActionKey, StringComparison.Ordinal)
                && string.Equals(LayerId, other.LayerId, StringComparison.Ordinal)
                && TransitionSeconds.Equals(other.TransitionSeconds);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionAnimationRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Metadata.GetHashCode();
                hash = (hash * 397) ^ (TargetActorId == null ? 0 : StringComparer.Ordinal.GetHashCode(TargetActorId));
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ (ActionKey == null ? 0 : StringComparer.Ordinal.GetHashCode(ActionKey));
                hash = (hash * 397) ^ (LayerId == null ? 0 : StringComparer.Ordinal.GetHashCode(LayerId));
                hash = (hash * 397) ^ TransitionSeconds.GetHashCode();
                return hash;
            }
        }

        private static void EnsureAnimationKind(CharacterActionTrackEventKind eventKind)
        {
            if (eventKind != CharacterActionTrackEventKind.PlayAnimation
                && eventKind != CharacterActionTrackEventKind.CrossFadeAnimation
                && eventKind != CharacterActionTrackEventKind.SetAnimationBlend)
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Event kind is not an AnimationTrack request.");
            }
        }
    }

    public readonly struct CharacterActionAudioCueRequest : IEquatable<CharacterActionAudioCueRequest>
    {
        public CharacterActionAudioCueRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            string targetActorId,
            string cueId)
        {
            Metadata = metadata;
            TargetActorId = targetActorId ?? string.Empty;
            CueId = cueId ?? string.Empty;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public string TargetActorId { get; }
        public string CueId { get; }

        public bool Equals(CharacterActionAudioCueRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && string.Equals(TargetActorId, other.TargetActorId, StringComparison.Ordinal)
                && string.Equals(CueId, other.CueId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionAudioCueRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Metadata.GetHashCode();
                hash = (hash * 397) ^ (TargetActorId == null ? 0 : StringComparer.Ordinal.GetHashCode(TargetActorId));
                hash = (hash * 397) ^ (CueId == null ? 0 : StringComparer.Ordinal.GetHashCode(CueId));
                return hash;
            }
        }
    }

    public readonly struct CharacterActionVfxRequest : IEquatable<CharacterActionVfxRequest>
    {
        public CharacterActionVfxRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            string targetActorId,
            string resourceKey)
        {
            Metadata = metadata;
            TargetActorId = targetActorId ?? string.Empty;
            ResourceKey = resourceKey ?? string.Empty;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public string TargetActorId { get; }
        public string ResourceKey { get; }

        public bool Equals(CharacterActionVfxRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && string.Equals(TargetActorId, other.TargetActorId, StringComparison.Ordinal)
                && string.Equals(ResourceKey, other.ResourceKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionVfxRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Metadata.GetHashCode();
                hash = (hash * 397) ^ (TargetActorId == null ? 0 : StringComparer.Ordinal.GetHashCode(TargetActorId));
                hash = (hash * 397) ^ (ResourceKey == null ? 0 : StringComparer.Ordinal.GetHashCode(ResourceKey));
                return hash;
            }
        }
    }

    public readonly struct CharacterActionCameraRequest : IEquatable<CharacterActionCameraRequest>
    {
        public CharacterActionCameraRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            string targetActorId,
            string requestId,
            string payloadKey)
        {
            Metadata = metadata;
            TargetActorId = targetActorId ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            PayloadKey = payloadKey ?? string.Empty;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public string TargetActorId { get; }
        public string RequestId { get; }
        public string PayloadKey { get; }

        public bool Equals(CharacterActionCameraRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && string.Equals(TargetActorId, other.TargetActorId, StringComparison.Ordinal)
                && string.Equals(RequestId, other.RequestId, StringComparison.Ordinal)
                && string.Equals(PayloadKey, other.PayloadKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionCameraRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Metadata.GetHashCode();
                hash = (hash * 397) ^ (TargetActorId == null ? 0 : StringComparer.Ordinal.GetHashCode(TargetActorId));
                hash = (hash * 397) ^ (RequestId == null ? 0 : StringComparer.Ordinal.GetHashCode(RequestId));
                hash = (hash * 397) ^ (PayloadKey == null ? 0 : StringComparer.Ordinal.GetHashCode(PayloadKey));
                return hash;
            }
        }
    }

    public readonly struct CharacterActionUiFeedbackRequest : IEquatable<CharacterActionUiFeedbackRequest>
    {
        public CharacterActionUiFeedbackRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            string targetActorId,
            string feedbackId,
            string payloadKey)
        {
            Metadata = metadata;
            TargetActorId = targetActorId ?? string.Empty;
            FeedbackId = feedbackId ?? string.Empty;
            PayloadKey = payloadKey ?? string.Empty;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public string TargetActorId { get; }
        public string FeedbackId { get; }
        public string PayloadKey { get; }

        public bool Equals(CharacterActionUiFeedbackRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && string.Equals(TargetActorId, other.TargetActorId, StringComparison.Ordinal)
                && string.Equals(FeedbackId, other.FeedbackId, StringComparison.Ordinal)
                && string.Equals(PayloadKey, other.PayloadKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionUiFeedbackRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Metadata.GetHashCode();
                hash = (hash * 397) ^ (TargetActorId == null ? 0 : StringComparer.Ordinal.GetHashCode(TargetActorId));
                hash = (hash * 397) ^ (FeedbackId == null ? 0 : StringComparer.Ordinal.GetHashCode(FeedbackId));
                hash = (hash * 397) ^ (PayloadKey == null ? 0 : StringComparer.Ordinal.GetHashCode(PayloadKey));
                return hash;
            }
        }
    }

    public readonly struct CharacterActionPressureOnlyReactionRequest : IEquatable<CharacterActionPressureOnlyReactionRequest>
    {
        public CharacterActionPressureOnlyReactionRequest(CharacterReactionContext context, string requestedActionId = "")
        {
            Context = context;
            RequestedActionId = requestedActionId ?? string.Empty;
        }

        public CharacterReactionContext Context { get; }
        public string RequestedActionId { get; }

        public bool Equals(CharacterActionPressureOnlyReactionRequest other)
        {
            return Context.Equals(other.Context)
                && string.Equals(RequestedActionId, other.RequestedActionId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionPressureOnlyReactionRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Context.GetHashCode() * 397)
                    ^ (RequestedActionId == null ? 0 : StringComparer.Ordinal.GetHashCode(RequestedActionId));
            }
        }
    }

    public readonly struct CharacterActionAdapterSinkResult
    {
        private CharacterActionAdapterSinkResult(bool accepted, CharacterActionDiagnostic[] diagnostics)
        {
            Accepted = accepted;
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
        }

        public bool Accepted { get; }
        public CharacterActionDiagnostic[] Diagnostics { get; }

        public static CharacterActionAdapterSinkResult AcceptedResult()
        {
            return new CharacterActionAdapterSinkResult(true, Array.Empty<CharacterActionDiagnostic>());
        }

        public static CharacterActionAdapterSinkResult Rejected(params CharacterActionDiagnostic[] diagnostics)
        {
            return new CharacterActionAdapterSinkResult(false, diagnostics);
        }
    }

    public readonly struct CharacterActionAdapterResult
    {
        public CharacterActionAdapterResult(
            int motionRequestCount,
            int combatRequestCount,
            int gameplayRequestCount,
            int pressureOnlyReactionRequestCount,
            CharacterActionDiagnostic[] diagnostics)
        {
            if (motionRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(motionRequestCount), "Request count cannot be negative.");
            if (combatRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(combatRequestCount), "Request count cannot be negative.");
            if (gameplayRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(gameplayRequestCount), "Request count cannot be negative.");
            if (pressureOnlyReactionRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(pressureOnlyReactionRequestCount), "Request count cannot be negative.");

            MotionRequestCount = motionRequestCount;
            CombatRequestCount = combatRequestCount;
            GameplayRequestCount = gameplayRequestCount;
            PressureOnlyReactionRequestCount = pressureOnlyReactionRequestCount;
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
        }

        public int MotionRequestCount { get; }
        public int CombatRequestCount { get; }
        public int GameplayRequestCount { get; }
        public int PressureOnlyReactionRequestCount { get; }
        public CharacterActionDiagnostic[] Diagnostics { get; }
        public bool Accepted => !HasErrors(Diagnostics);

        private static bool HasErrors(CharacterActionDiagnostic[] diagnostics)
        {
            diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error)
                    return true;
            }

            return false;
        }
    }

    public interface ICharacterActionMotionRequestSink
    {
        CharacterActionAdapterSinkResult SubmitMotionRequest(CharacterActionMotionRequest request);
    }

    public interface ICharacterActionCombatRequestSink
    {
        CharacterActionAdapterSinkResult SubmitCombatRequest(CharacterActionCombatRequest request);
    }

    public interface ICharacterActionGameplayRequestSink
    {
        CharacterActionAdapterSinkResult SubmitGameplayRequest(CharacterActionGameplayRequest request);
    }

    public interface ICharacterActionPressureOnlyReactionRequestSink
    {
        CharacterActionAdapterSinkResult SubmitPressureOnlyReactionRequest(CharacterActionPressureOnlyReactionRequest request);
    }

    public interface ICharacterActionAnimationRequestSink
    {
        CharacterActionAdapterSinkResult SubmitAnimationRequest(CharacterActionAnimationRequest request);
    }

    public interface ICharacterActionAudioCueRequestSink
    {
        CharacterActionAdapterSinkResult SubmitAudioCueRequest(CharacterActionAudioCueRequest request);
    }

    public interface ICharacterActionVfxRequestSink
    {
        CharacterActionAdapterSinkResult SubmitVfxRequest(CharacterActionVfxRequest request);
    }

    public interface ICharacterActionCameraRequestSink
    {
        CharacterActionAdapterSinkResult SubmitCameraRequest(CharacterActionCameraRequest request);
    }

    public interface ICharacterActionUiFeedbackRequestSink
    {
        CharacterActionAdapterSinkResult SubmitUiFeedbackRequest(CharacterActionUiFeedbackRequest request);
    }

    public sealed class CharacterActionAdapterRequestCollector :
        ICharacterActionMotionRequestSink,
        ICharacterActionCombatRequestSink,
        ICharacterActionGameplayRequestSink,
        ICharacterActionPressureOnlyReactionRequestSink
    {
        private readonly List<CharacterActionMotionRequest> _motionRequests = new List<CharacterActionMotionRequest>();
        private readonly List<CharacterActionCombatRequest> _combatRequests = new List<CharacterActionCombatRequest>();
        private readonly List<CharacterActionGameplayRequest> _gameplayRequests = new List<CharacterActionGameplayRequest>();
        private readonly List<CharacterActionPressureOnlyReactionRequest> _pressureOnlyReactionRequests =
            new List<CharacterActionPressureOnlyReactionRequest>();

        public IReadOnlyList<CharacterActionMotionRequest> MotionRequests => _motionRequests;
        public IReadOnlyList<CharacterActionCombatRequest> CombatRequests => _combatRequests;
        public IReadOnlyList<CharacterActionGameplayRequest> GameplayRequests => _gameplayRequests;
        public IReadOnlyList<CharacterActionPressureOnlyReactionRequest> PressureOnlyReactionRequests => _pressureOnlyReactionRequests;

        public void Clear()
        {
            _motionRequests.Clear();
            _combatRequests.Clear();
            _gameplayRequests.Clear();
            _pressureOnlyReactionRequests.Clear();
        }

        public CharacterActionAdapterSinkResult SubmitMotionRequest(CharacterActionMotionRequest request)
        {
            _motionRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitCombatRequest(CharacterActionCombatRequest request)
        {
            _combatRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitGameplayRequest(CharacterActionGameplayRequest request)
        {
            _gameplayRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitPressureOnlyReactionRequest(CharacterActionPressureOnlyReactionRequest request)
        {
            _pressureOnlyReactionRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }
    }

    public sealed class CharacterActionPresentationRequestCollector :
        ICharacterActionAnimationRequestSink,
        ICharacterActionAudioCueRequestSink,
        ICharacterActionVfxRequestSink,
        ICharacterActionCameraRequestSink,
        ICharacterActionUiFeedbackRequestSink
    {
        private readonly List<CharacterActionAnimationRequest> _animationRequests =
            new List<CharacterActionAnimationRequest>();
        private readonly List<CharacterActionAudioCueRequest> _audioCueRequests =
            new List<CharacterActionAudioCueRequest>();
        private readonly List<CharacterActionVfxRequest> _vfxRequests =
            new List<CharacterActionVfxRequest>();
        private readonly List<CharacterActionCameraRequest> _cameraRequests =
            new List<CharacterActionCameraRequest>();
        private readonly List<CharacterActionUiFeedbackRequest> _uiFeedbackRequests =
            new List<CharacterActionUiFeedbackRequest>();

        public IReadOnlyList<CharacterActionAnimationRequest> AnimationRequests => _animationRequests;
        public IReadOnlyList<CharacterActionAudioCueRequest> AudioCueRequests => _audioCueRequests;
        public IReadOnlyList<CharacterActionVfxRequest> VfxRequests => _vfxRequests;
        public IReadOnlyList<CharacterActionCameraRequest> CameraRequests => _cameraRequests;
        public IReadOnlyList<CharacterActionUiFeedbackRequest> UiFeedbackRequests => _uiFeedbackRequests;

        public void Clear()
        {
            _animationRequests.Clear();
            _audioCueRequests.Clear();
            _vfxRequests.Clear();
            _cameraRequests.Clear();
            _uiFeedbackRequests.Clear();
        }

        public CharacterActionAdapterSinkResult SubmitAnimationRequest(CharacterActionAnimationRequest request)
        {
            _animationRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitAudioCueRequest(CharacterActionAudioCueRequest request)
        {
            _audioCueRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitVfxRequest(CharacterActionVfxRequest request)
        {
            _vfxRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitCameraRequest(CharacterActionCameraRequest request)
        {
            _cameraRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public CharacterActionAdapterSinkResult SubmitUiFeedbackRequest(CharacterActionUiFeedbackRequest request)
        {
            _uiFeedbackRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }
    }

    public sealed class CharacterActionTrackAdapter
    {
        private readonly ICharacterActionMotionRequestSink _motionSink;
        private readonly ICharacterActionCombatRequestSink _combatSink;
        private readonly ICharacterActionGameplayRequestSink _gameplaySink;
        private readonly ICharacterActionPressureOnlyReactionRequestSink _pressureOnlyReactionSink;

        public CharacterActionTrackAdapter(
            ICharacterActionMotionRequestSink motionSink = null,
            ICharacterActionCombatRequestSink combatSink = null,
            ICharacterActionGameplayRequestSink gameplaySink = null,
            ICharacterActionPressureOnlyReactionRequestSink pressureOnlyReactionSink = null)
        {
            _motionSink = motionSink;
            _combatSink = combatSink;
            _gameplaySink = gameplaySink;
            _pressureOnlyReactionSink = pressureOnlyReactionSink;
        }

        public CharacterActionAdapterResult Adapt(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionAdapterContext context)
        {
            var diagnostics = new List<CharacterActionDiagnostic>();
            int motionCount = 0;
            int combatCount = 0;
            int gameplayCount = 0;

            if (runnerEvent.Kind != CharacterActionRunnerEventKind.TrackEventFired
                || !runnerEvent.TrackDispatch.HasEvent)
            {
                return new CharacterActionAdapterResult(0, 0, 0, 0, Array.Empty<CharacterActionDiagnostic>());
            }

            CharacterActionTrackDispatchEvent dispatch = runnerEvent.TrackDispatch;
            switch (dispatch.TrackKind)
            {
                case CharacterActionTrackKind.Motion:
                    if (TryAdaptMotion(runnerEvent, dispatch, context, diagnostics))
                        motionCount++;
                    break;
                case CharacterActionTrackKind.Combat:
                    if (TryAdaptCombat(runnerEvent, dispatch, context, diagnostics))
                        combatCount++;
                    break;
                case CharacterActionTrackKind.Gameplay:
                    if (TryAdaptGameplay(runnerEvent, dispatch, context, diagnostics))
                        gameplayCount++;
                    break;
            }

            return new CharacterActionAdapterResult(motionCount, combatCount, gameplayCount, 0, diagnostics.ToArray());
        }

        public CharacterActionAdapterResult AdaptMany(
            CharacterActionRunnerEvent[] runnerEvents,
            CharacterActionAdapterContext context)
        {
            runnerEvents = runnerEvents ?? Array.Empty<CharacterActionRunnerEvent>();
            var diagnostics = new List<CharacterActionDiagnostic>();
            int motionCount = 0;
            int combatCount = 0;
            int gameplayCount = 0;

            for (int i = 0; i < runnerEvents.Length; i++)
            {
                CharacterActionAdapterResult result = Adapt(runnerEvents[i], context);
                motionCount += result.MotionRequestCount;
                combatCount += result.CombatRequestCount;
                gameplayCount += result.GameplayRequestCount;
                diagnostics.AddRange(result.Diagnostics);
            }

            return new CharacterActionAdapterResult(motionCount, combatCount, gameplayCount, 0, diagnostics.ToArray());
        }

        public CharacterActionAdapterResult SubmitPressureOnlyReaction(
            CharacterReactionContext reactionContext,
            string requestedActionId = "")
        {
            var diagnostics = new List<CharacterActionDiagnostic>();
            if (reactionContext.Completeness != CharacterReactionContextCompleteness.PressureOnly)
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.ReactionContextIncomplete,
                    "PressureOnly reaction request requires PressureOnly context."));
                return new CharacterActionAdapterResult(0, 0, 0, 0, diagnostics.ToArray());
            }

            if (!reactionContext.EntityId.IsValid)
            {
                diagnostics.Add(PayloadMissing("PressureOnly reaction request requires a gameplay entity id."));
                return new CharacterActionAdapterResult(0, 0, 0, 0, diagnostics.ToArray());
            }

            if (_pressureOnlyReactionSink == null)
            {
                diagnostics.Add(MissingSink("PressureOnlyReaction"));
                return new CharacterActionAdapterResult(0, 0, 0, 0, diagnostics.ToArray());
            }

            var request = new CharacterActionPressureOnlyReactionRequest(reactionContext, requestedActionId);
            CharacterActionAdapterSinkResult sinkResult = SubmitToSink(
                () => _pressureOnlyReactionSink.SubmitPressureOnlyReactionRequest(request),
                "PressureOnlyReaction");
            diagnostics.AddRange(sinkResult.Diagnostics);
            int count = sinkResult.Accepted ? 1 : 0;
            return new CharacterActionAdapterResult(0, 0, 0, count, diagnostics.ToArray());
        }

        private bool TryAdaptMotion(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionAdapterContext context,
            List<CharacterActionDiagnostic> diagnostics)
        {
            if (_motionSink == null)
            {
                diagnostics.Add(MissingSink("Motion"));
                return false;
            }

            if (!context.HasGameplayEntity && !context.HasCombatBody)
            {
                diagnostics.Add(PayloadMissing("Motion request requires a gameplay entity id or combat body id."));
                return false;
            }

            if (dispatch.EventKind == CharacterActionTrackEventKind.ApplyImpulse
                && dispatch.X.Equals(0f)
                && dispatch.Y.Equals(0f)
                && dispatch.Z.Equals(0f))
            {
                diagnostics.Add(PayloadMissing("Motion ApplyImpulse request requires a non-zero vector."));
                return false;
            }

            var request = new CharacterActionMotionRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.GameplayEntityId,
                context.CombatBodyId,
                dispatch.EventKind,
                dispatch.MovementMode,
                dispatch.X,
                dispatch.Y,
                dispatch.Z);
            CharacterActionAdapterSinkResult sinkResult = SubmitToSink(
                () => _motionSink.SubmitMotionRequest(request),
                "Motion");
            diagnostics.AddRange(sinkResult.Diagnostics);
            return sinkResult.Accepted;
        }

        private bool TryAdaptCombat(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionAdapterContext context,
            List<CharacterActionDiagnostic> diagnostics)
        {
            if (_combatSink == null)
            {
                diagnostics.Add(MissingSink("Combat"));
                return false;
            }

            if (!context.HasCombatEntity)
            {
                diagnostics.Add(PayloadMissing("Combat request requires a combat entity id."));
                return false;
            }

            if (dispatch.EventKind == CharacterActionTrackEventKind.StartCombatAction
                && string.IsNullOrEmpty(dispatch.CombatActionId))
            {
                diagnostics.Add(PayloadMissing("Combat StartCombatAction request requires a combat action id."));
                return false;
            }

            if ((dispatch.EventKind == CharacterActionTrackEventKind.StartHitTrace
                    || dispatch.EventKind == CharacterActionTrackEventKind.StopHitTrace)
                && string.IsNullOrEmpty(dispatch.TraceProfileId))
            {
                diagnostics.Add(PayloadMissing("Combat hit trace request requires a trace profile id."));
                return false;
            }

            var request = new CharacterActionCombatRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.CombatEntityId,
                dispatch.EventKind,
                dispatch.CombatActionId,
                dispatch.TraceProfileId);
            CharacterActionAdapterSinkResult sinkResult = SubmitToSink(
                () => _combatSink.SubmitCombatRequest(request),
                "Combat");
            diagnostics.AddRange(sinkResult.Diagnostics);
            return sinkResult.Accepted;
        }

        private bool TryAdaptGameplay(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionAdapterContext context,
            List<CharacterActionDiagnostic> diagnostics)
        {
            if (_gameplaySink == null)
            {
                diagnostics.Add(MissingSink("Gameplay"));
                return false;
            }

            if (!context.HasGameplayEntity)
            {
                diagnostics.Add(PayloadMissing("Gameplay request requires a gameplay entity id."));
                return false;
            }

            if ((dispatch.EventKind == CharacterActionTrackEventKind.SendGameplayRequest
                    || dispatch.EventKind == CharacterActionTrackEventKind.ApplyGameplayEffect)
                && string.IsNullOrEmpty(dispatch.GameplayRequestId))
            {
                diagnostics.Add(PayloadMissing("Gameplay request track event requires a request id."));
                return false;
            }

            if (dispatch.EventKind == CharacterActionTrackEventKind.CastAbility
                && string.IsNullOrEmpty(dispatch.AbilityStableId))
            {
                diagnostics.Add(PayloadMissing("Gameplay CastAbility request requires an ability stable id."));
                return false;
            }

            var request = new CharacterActionGameplayRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.GameplayEntityId,
                dispatch.EventKind,
                dispatch.GameplayRequestId,
                dispatch.AbilityStableId);
            CharacterActionAdapterSinkResult sinkResult = SubmitToSink(
                () => _gameplaySink.SubmitGameplayRequest(request),
                "Gameplay");
            diagnostics.AddRange(sinkResult.Diagnostics);
            return sinkResult.Accepted;
        }

        private static CharacterActionAdapterDispatchMetadata CreateMetadata(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionAdapterContext context)
        {
            string traceId = string.IsNullOrEmpty(context.TraceId) ? runnerEvent.TraceId : context.TraceId;
            return new CharacterActionAdapterDispatchMetadata(
                context.Frame,
                runnerEvent.InstanceId,
                runnerEvent.PlanId,
                runnerEvent.ActionId,
                runnerEvent.LocalFrame,
                dispatch.StableEventId,
                context.SourceId,
                traceId);
        }

        private static CharacterActionAdapterSinkResult SubmitToSink(
            Func<CharacterActionAdapterSinkResult> submit,
            string sinkName)
        {
            try
            {
                return submit();
            }
            catch (Exception ex)
            {
                return CharacterActionAdapterSinkResult.Rejected(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.AdapterSinkFailure,
                    sinkName + " sink failed: " + ex.Message));
            }
        }

        private static CharacterActionDiagnostic MissingSink(string sinkName)
        {
            return CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCodes.AdapterSinkMissing,
                sinkName + " request sink is missing.");
        }

        private static CharacterActionDiagnostic PayloadMissing(string message)
        {
            return CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCodes.AdapterPayloadMissing,
                message);
        }
    }

    public readonly struct CharacterActionPresentationAdapterResult
    {
        public CharacterActionPresentationAdapterResult(
            int animationRequestCount,
            int audioCueRequestCount,
            int vfxRequestCount,
            int cameraRequestCount,
            int uiFeedbackRequestCount,
            CharacterActionDiagnostic[] diagnostics)
        {
            if (animationRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(animationRequestCount), "Request count cannot be negative.");
            if (audioCueRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(audioCueRequestCount), "Request count cannot be negative.");
            if (vfxRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(vfxRequestCount), "Request count cannot be negative.");
            if (cameraRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(cameraRequestCount), "Request count cannot be negative.");
            if (uiFeedbackRequestCount < 0)
                throw new ArgumentOutOfRangeException(nameof(uiFeedbackRequestCount), "Request count cannot be negative.");

            AnimationRequestCount = animationRequestCount;
            AudioCueRequestCount = audioCueRequestCount;
            VfxRequestCount = vfxRequestCount;
            CameraRequestCount = cameraRequestCount;
            UiFeedbackRequestCount = uiFeedbackRequestCount;
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
        }

        public int AnimationRequestCount { get; }
        public int AudioCueRequestCount { get; }
        public int VfxRequestCount { get; }
        public int CameraRequestCount { get; }
        public int UiFeedbackRequestCount { get; }
        public CharacterActionDiagnostic[] Diagnostics { get; }
        public bool Accepted => !HasErrors(Diagnostics);

        private static bool HasErrors(CharacterActionDiagnostic[] diagnostics)
        {
            diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error)
                    return true;
            }

            return false;
        }
    }

    public sealed class CharacterActionPresentationTrackAdapter
    {
        private readonly ICharacterActionAnimationRequestSink _animationSink;
        private readonly ICharacterActionAudioCueRequestSink _audioCueSink;
        private readonly ICharacterActionVfxRequestSink _vfxSink;
        private readonly ICharacterActionCameraRequestSink _cameraSink;
        private readonly ICharacterActionUiFeedbackRequestSink _uiFeedbackSink;

        public CharacterActionPresentationTrackAdapter(
            ICharacterActionAnimationRequestSink animationSink = null,
            ICharacterActionAudioCueRequestSink audioCueSink = null,
            ICharacterActionVfxRequestSink vfxSink = null,
            ICharacterActionCameraRequestSink cameraSink = null,
            ICharacterActionUiFeedbackRequestSink uiFeedbackSink = null)
        {
            _animationSink = animationSink;
            _audioCueSink = audioCueSink;
            _vfxSink = vfxSink;
            _cameraSink = cameraSink;
            _uiFeedbackSink = uiFeedbackSink;
        }

        public CharacterActionPresentationAdapterResult Adapt(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionPresentationAdapterContext context)
        {
            var diagnostics = new List<CharacterActionDiagnostic>();
            int animationCount = 0;
            int audioCount = 0;
            int vfxCount = 0;
            int cameraCount = 0;
            int uiCount = 0;

            if (runnerEvent.Kind != CharacterActionRunnerEventKind.TrackEventFired
                || !runnerEvent.TrackDispatch.HasEvent)
            {
                return new CharacterActionPresentationAdapterResult(0, 0, 0, 0, 0, Array.Empty<CharacterActionDiagnostic>());
            }

            CharacterActionTrackDispatchEvent dispatch = runnerEvent.TrackDispatch;
            if (dispatch.TrackKind == CharacterActionTrackKind.Animation)
            {
                if (TryAdaptAnimation(runnerEvent, dispatch, context, diagnostics))
                    animationCount++;
            }
            else if (dispatch.TrackKind == CharacterActionTrackKind.Presentation)
            {
                switch (dispatch.EventKind)
                {
                    case CharacterActionTrackEventKind.PlayAudioCue:
                        if (TryAdaptAudioCue(runnerEvent, dispatch, context, diagnostics))
                            audioCount++;
                        break;
                    case CharacterActionTrackEventKind.SpawnVisualCue:
                        if (TryAdaptVfx(runnerEvent, dispatch, context, diagnostics))
                            vfxCount++;
                        break;
                    case CharacterActionTrackEventKind.CameraImpulse:
                        if (TryAdaptCamera(runnerEvent, dispatch, context, diagnostics))
                            cameraCount++;
                        break;
                    case CharacterActionTrackEventKind.UiFeedback:
                        if (TryAdaptUiFeedback(runnerEvent, dispatch, context, diagnostics))
                            uiCount++;
                        break;
                }
            }

            return new CharacterActionPresentationAdapterResult(
                animationCount,
                audioCount,
                vfxCount,
                cameraCount,
                uiCount,
                diagnostics.ToArray());
        }

        public CharacterActionPresentationAdapterResult AdaptMany(
            CharacterActionRunnerEvent[] runnerEvents,
            CharacterActionPresentationAdapterContext context)
        {
            runnerEvents = runnerEvents ?? Array.Empty<CharacterActionRunnerEvent>();
            var diagnostics = new List<CharacterActionDiagnostic>();
            int animationCount = 0;
            int audioCount = 0;
            int vfxCount = 0;
            int cameraCount = 0;
            int uiCount = 0;

            for (int i = 0; i < runnerEvents.Length; i++)
            {
                CharacterActionPresentationAdapterResult result = Adapt(runnerEvents[i], context);
                animationCount += result.AnimationRequestCount;
                audioCount += result.AudioCueRequestCount;
                vfxCount += result.VfxRequestCount;
                cameraCount += result.CameraRequestCount;
                uiCount += result.UiFeedbackRequestCount;
                diagnostics.AddRange(result.Diagnostics);
            }

            return new CharacterActionPresentationAdapterResult(
                animationCount,
                audioCount,
                vfxCount,
                cameraCount,
                uiCount,
                diagnostics.ToArray());
        }

        private bool TryAdaptAnimation(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionPresentationAdapterContext context,
            List<CharacterActionDiagnostic> diagnostics)
        {
            if (_animationSink == null)
            {
                diagnostics.Add(MissingSink("Animation"));
                return false;
            }

            if (!context.HasTargetActor)
            {
                diagnostics.Add(PayloadMissing("Animation request requires a target actor id."));
                return false;
            }

            if (string.IsNullOrEmpty(dispatch.AnimationActionKey))
            {
                diagnostics.Add(PayloadMissing("Animation track event requires an animation action key or blend id."));
                return false;
            }

            var request = new CharacterActionAnimationRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.TargetActorId,
                dispatch.EventKind,
                dispatch.AnimationActionKey,
                context.AnimationLayerId,
                dispatch.TransitionSeconds);
            CharacterActionAdapterSinkResult sinkResult = SubmitToSink(
                () => _animationSink.SubmitAnimationRequest(request),
                "Animation");
            diagnostics.AddRange(sinkResult.Diagnostics);
            return sinkResult.Accepted;
        }

        private bool TryAdaptAudioCue(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionPresentationAdapterContext context,
            List<CharacterActionDiagnostic> diagnostics)
        {
            if (_audioCueSink == null)
            {
                diagnostics.Add(MissingSink("Audio"));
                return false;
            }

            if (string.IsNullOrEmpty(dispatch.PresentationCueId))
            {
                diagnostics.Add(PayloadMissing("Audio presentation event requires a cue id."));
                return false;
            }

            var request = new CharacterActionAudioCueRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.TargetActorId,
                dispatch.PresentationCueId);
            CharacterActionAdapterSinkResult sinkResult = SubmitToSink(
                () => _audioCueSink.SubmitAudioCueRequest(request),
                "Audio");
            diagnostics.AddRange(sinkResult.Diagnostics);
            return sinkResult.Accepted;
        }

        private bool TryAdaptVfx(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionPresentationAdapterContext context,
            List<CharacterActionDiagnostic> diagnostics)
        {
            if (_vfxSink == null)
            {
                diagnostics.Add(MissingSink("VFX"));
                return false;
            }

            if (string.IsNullOrEmpty(dispatch.ResourceKey))
            {
                diagnostics.Add(PayloadMissing("VFX presentation event requires a resource key."));
                return false;
            }

            var request = new CharacterActionVfxRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.TargetActorId,
                dispatch.ResourceKey);
            CharacterActionAdapterSinkResult sinkResult = SubmitToSink(
                () => _vfxSink.SubmitVfxRequest(request),
                "VFX");
            diagnostics.AddRange(sinkResult.Diagnostics);
            return sinkResult.Accepted;
        }

        private bool TryAdaptCamera(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionPresentationAdapterContext context,
            List<CharacterActionDiagnostic> diagnostics)
        {
            if (_cameraSink == null)
            {
                diagnostics.Add(MissingSink("Camera"));
                return false;
            }

            if (string.IsNullOrEmpty(dispatch.PresentationCueId) && string.IsNullOrEmpty(dispatch.ResourceKey))
            {
                diagnostics.Add(PayloadMissing("Camera presentation event requires a request id or payload key."));
                return false;
            }

            var request = new CharacterActionCameraRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.TargetActorId,
                dispatch.PresentationCueId,
                dispatch.ResourceKey);
            CharacterActionAdapterSinkResult sinkResult = SubmitToSink(
                () => _cameraSink.SubmitCameraRequest(request),
                "Camera");
            diagnostics.AddRange(sinkResult.Diagnostics);
            return sinkResult.Accepted;
        }

        private bool TryAdaptUiFeedback(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionPresentationAdapterContext context,
            List<CharacterActionDiagnostic> diagnostics)
        {
            if (_uiFeedbackSink == null)
            {
                diagnostics.Add(MissingSink("UIFeedback"));
                return false;
            }

            if (string.IsNullOrEmpty(dispatch.PresentationCueId))
            {
                diagnostics.Add(PayloadMissing("UI feedback presentation event requires a feedback id."));
                return false;
            }

            var request = new CharacterActionUiFeedbackRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.TargetActorId,
                dispatch.PresentationCueId,
                dispatch.ResourceKey);
            CharacterActionAdapterSinkResult sinkResult = SubmitToSink(
                () => _uiFeedbackSink.SubmitUiFeedbackRequest(request),
                "UIFeedback");
            diagnostics.AddRange(sinkResult.Diagnostics);
            return sinkResult.Accepted;
        }

        private static CharacterActionAdapterDispatchMetadata CreateMetadata(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionPresentationAdapterContext context)
        {
            string traceId = string.IsNullOrEmpty(context.TraceId) ? runnerEvent.TraceId : context.TraceId;
            return new CharacterActionAdapterDispatchMetadata(
                context.Frame,
                runnerEvent.InstanceId,
                runnerEvent.PlanId,
                runnerEvent.ActionId,
                runnerEvent.LocalFrame,
                dispatch.StableEventId,
                context.SourceId,
                traceId);
        }

        private static CharacterActionAdapterSinkResult SubmitToSink(
            Func<CharacterActionAdapterSinkResult> submit,
            string sinkName)
        {
            try
            {
                return submit();
            }
            catch (Exception ex)
            {
                return CharacterActionAdapterSinkResult.Rejected(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.AdapterSinkFailure,
                    sinkName + " sink failed: " + ex.Message));
            }
        }

        private static CharacterActionDiagnostic MissingSink(string sinkName)
        {
            return CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCodes.AdapterSinkMissing,
                sinkName + " request sink is missing.");
        }

        private static CharacterActionDiagnostic PayloadMissing(string message)
        {
            return CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCodes.AdapterPayloadMissing,
                message);
        }
    }
}
