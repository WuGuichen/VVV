using System;
using MxFramework.Runtime;

namespace MxFramework.CharacterAction
{
    public readonly struct CharacterActionPresentationAdapterContext : IEquatable<CharacterActionPresentationAdapterContext>
    {
        public CharacterActionPresentationAdapterContext(
            RuntimeFrame frame,
            string targetActorId,
            int sourceId = 0,
            RuntimeTraceIdentity traceId = default,
            string animationLayerId = "base")
        {
            Frame = frame;
            TargetActorId = targetActorId ?? string.Empty;
            SourceId = sourceId;
            TraceId = traceId;
            AnimationLayerId = string.IsNullOrWhiteSpace(animationLayerId) ? "base" : animationLayerId;
        }

        public RuntimeFrame Frame { get; }
        public string TargetActorId { get; }
        public int SourceId { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public string AnimationLayerId { get; }
        public bool HasTargetActor => !string.IsNullOrEmpty(TargetActorId);

        public bool Equals(CharacterActionPresentationAdapterContext other)
        {
            return Frame.Equals(other.Frame)
                && string.Equals(TargetActorId, other.TargetActorId, StringComparison.Ordinal)
                && SourceId == other.SourceId
                && TraceId.Equals(other.TraceId)
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
                hash = (hash * 397) ^ TraceId.GetHashCode();
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
            float transitionSeconds,
            string clipResourceKey = "",
            float playbackSpeed = 1f,
            int startOffsetFrames = 0,
            int endOffsetFrames = 0,
            int endFrame = -1)
        {
            EnsureAnimationKind(eventKind);
            Metadata = metadata;
            TargetActorId = targetActorId ?? string.Empty;
            EventKind = eventKind;
            ActionKey = actionKey ?? string.Empty;
            ClipResourceKey = clipResourceKey ?? string.Empty;
            PlaybackSpeed = playbackSpeed <= 0.0001f ? 1f : playbackSpeed;
            StartOffsetFrames = Math.Max(0, startOffsetFrames);
            EndOffsetFrames = Math.Max(0, endOffsetFrames);
            EndFrame = endFrame < 0 ? metadata.LocalFrame : Math.Max(metadata.LocalFrame, endFrame);
            LayerId = string.IsNullOrWhiteSpace(layerId) ? "base" : layerId;
            TransitionSeconds = transitionSeconds;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public string TargetActorId { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public string ActionKey { get; }
        public string ClipResourceKey { get; }
        public string EffectiveAnimationKey => CharacterActionAnimationKeyUtility.ResolveEffectiveKey(
            ClipResourceKey,
            ActionKey);
        public float PlaybackSpeed { get; }
        public int StartOffsetFrames { get; }
        public int EndOffsetFrames { get; }
        public int EndFrame { get; }
        public int DurationFrames => Math.Max(1, EndFrame - Metadata.LocalFrame + 1);
        public float StartOffsetSeconds => CharacterActionAnimationKeyUtility.FramesToSeconds(StartOffsetFrames);
        public float DurationSeconds => CharacterActionAnimationKeyUtility.FramesToSeconds(DurationFrames);
        public string LayerId { get; }
        public float TransitionSeconds { get; }

        public bool Equals(CharacterActionAnimationRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && string.Equals(TargetActorId, other.TargetActorId, StringComparison.Ordinal)
                && EventKind == other.EventKind
                && string.Equals(ActionKey, other.ActionKey, StringComparison.Ordinal)
                && string.Equals(ClipResourceKey, other.ClipResourceKey, StringComparison.Ordinal)
                && PlaybackSpeed.Equals(other.PlaybackSpeed)
                && StartOffsetFrames == other.StartOffsetFrames
                && EndOffsetFrames == other.EndOffsetFrames
                && EndFrame == other.EndFrame
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
                hash = (hash * 397) ^ (ClipResourceKey == null ? 0 : StringComparer.Ordinal.GetHashCode(ClipResourceKey));
                hash = (hash * 397) ^ PlaybackSpeed.GetHashCode();
                hash = (hash * 397) ^ StartOffsetFrames;
                hash = (hash * 397) ^ EndOffsetFrames;
                hash = (hash * 397) ^ EndFrame;
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
            int cueId)
        {
            Metadata = metadata;
            TargetActorId = targetActorId ?? string.Empty;
            CueId = cueId;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public string TargetActorId { get; }
        public int CueId { get; }

        public bool Equals(CharacterActionAudioCueRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && string.Equals(TargetActorId, other.TargetActorId, StringComparison.Ordinal)
                && CueId == other.CueId;
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
                hash = (hash * 397) ^ CueId.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct CharacterActionVfxRequest : IEquatable<CharacterActionVfxRequest>
    {
        public CharacterActionVfxRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            string targetActorId,
            string resourceKey,
            int endFrame = -1,
            CharacterActionVfxEventConfig vfxConfig = default)
        {
            Metadata = metadata;
            TargetActorId = targetActorId ?? string.Empty;
            ResourceKey = resourceKey ?? string.Empty;
            EndFrame = endFrame < 0 ? metadata.LocalFrame : Math.Max(metadata.LocalFrame, endFrame);
            VfxConfig = !vfxConfig.Attachment.Transform.IsScaleValid()
                ? new CharacterActionVfxEventConfig()
                : vfxConfig;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public string TargetActorId { get; }
        public string ResourceKey { get; }
        public int EndFrame { get; }
        public int DurationFrames => Math.Max(1, EndFrame - Metadata.LocalFrame + 1);
        public CharacterActionVfxEventConfig VfxConfig { get; }
        public int VfxDefinitionId => VfxConfig.VfxDefinitionId;
        public int NumericSocketId => VfxConfig.NumericSocketId;
        public int EffectiveLifetimeFrames => VfxConfig.PlaybackMode == CharacterActionVfxPlaybackMode.PlayToCompletion
            ? 0
            : DurationFrames;

        public bool Equals(CharacterActionVfxRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && string.Equals(TargetActorId, other.TargetActorId, StringComparison.Ordinal)
                && string.Equals(ResourceKey, other.ResourceKey, StringComparison.Ordinal)
                && EndFrame == other.EndFrame
                && VfxConfig.Equals(other.VfxConfig);
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
                hash = (hash * 397) ^ EndFrame;
                hash = (hash * 397) ^ VfxConfig.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct CharacterActionCameraRequest : IEquatable<CharacterActionCameraRequest>
    {
        public CharacterActionCameraRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            string targetActorId,
            int requestId,
            string payloadKey)
        {
            Metadata = metadata;
            TargetActorId = targetActorId ?? string.Empty;
            RequestId = requestId;
            PayloadKey = payloadKey ?? string.Empty;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public string TargetActorId { get; }
        public int RequestId { get; }
        public string PayloadKey { get; }

        public bool Equals(CharacterActionCameraRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && string.Equals(TargetActorId, other.TargetActorId, StringComparison.Ordinal)
                && RequestId == other.RequestId
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
                hash = (hash * 397) ^ RequestId.GetHashCode();
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
            int feedbackId,
            string payloadKey)
        {
            Metadata = metadata;
            TargetActorId = targetActorId ?? string.Empty;
            FeedbackId = feedbackId;
            PayloadKey = payloadKey ?? string.Empty;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public string TargetActorId { get; }
        public int FeedbackId { get; }
        public string PayloadKey { get; }

        public bool Equals(CharacterActionUiFeedbackRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && string.Equals(TargetActorId, other.TargetActorId, StringComparison.Ordinal)
                && FeedbackId == other.FeedbackId
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
                hash = (hash * 397) ^ FeedbackId.GetHashCode();
                hash = (hash * 397) ^ (PayloadKey == null ? 0 : StringComparer.Ordinal.GetHashCode(PayloadKey));
                return hash;
            }
        }
    }
}
