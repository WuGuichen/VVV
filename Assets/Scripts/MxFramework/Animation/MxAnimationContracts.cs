using System;
using System.Collections.Generic;
using MxFramework.Resources;

namespace MxFramework.Animation
{
    public readonly struct MxAnimationLayerId : IEquatable<MxAnimationLayerId>
    {
        private readonly string _value;

        public MxAnimationLayerId(string value)
        {
            _value = string.IsNullOrWhiteSpace(value) ? "base" : value;
        }

        public string Value => string.IsNullOrWhiteSpace(_value) ? "base" : _value;
        public static MxAnimationLayerId Base => new MxAnimationLayerId("base");

        public bool Equals(MxAnimationLayerId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MxAnimationLayerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(MxAnimationLayerId left, MxAnimationLayerId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxAnimationLayerId left, MxAnimationLayerId right)
        {
            return !left.Equals(right);
        }
    }

    public enum MxAnimationAlignmentPolicy
    {
        None,
        StartAtZero,
        PreserveNormalizedTime,
        MatchPresentationTime,
        UseCombatFrameAnchor
    }

    public enum MxAnimationEventTimeDomain
    {
        Seconds,
        NormalizedTime,
        CombatFrame,
        PresentationFrame
    }

    public enum MxAnimationLayerStatus
    {
        None,
        Loading,
        FadingIn,
        Playing,
        FadingOut,
        CrossFading,
        Stopped,
        Failed
    }

    public enum MxAnimationResourceLoadStatus
    {
        None,
        Loading,
        Loaded,
        Failed,
        Released
    }

    public enum MxAnimationRequestKind
    {
        Play,
        Stop,
        CrossFade,
        Release
    }

    public enum MxAnimationBackendResultCode
    {
        Success,
        Queued,
        InvalidRequest,
        LoadFailed,
        FallbackFailed,
        BackendReleased
    }

    public enum MxAnimationOutgoingReleasePolicy
    {
        ReleaseWhenGraphDetached
    }

    public sealed class MxAnimationPresentationEvent
    {
        public MxAnimationPresentationEvent(
            string eventId,
            MxAnimationEventTimeDomain timeDomain,
            float time,
            string eventKind,
            ResourceKey payloadKey,
            string socket = "",
            string tag = "")
        {
            EventId = eventId ?? string.Empty;
            TimeDomain = timeDomain;
            Time = time;
            EventKind = eventKind ?? string.Empty;
            PayloadKey = payloadKey;
            Socket = socket ?? string.Empty;
            Tag = tag ?? string.Empty;
        }

        public string EventId { get; }
        public MxAnimationEventTimeDomain TimeDomain { get; }
        public float Time { get; }
        public string EventKind { get; }
        public ResourceKey PayloadKey { get; }
        public string Socket { get; }
        public string Tag { get; }
    }

    public sealed class MxAnimationActionBinding
    {
        private readonly List<MxAnimationPresentationEvent> _presentationEvents;

        public MxAnimationActionBinding(
            string bindingId,
            string actionKey,
            ResourceKey clip,
            MxAnimationLayerId layer,
            float playbackSpeed = 1f,
            bool loop = false,
            MxAnimationAlignmentPolicy alignmentPolicy = MxAnimationAlignmentPolicy.StartAtZero,
            IEnumerable<MxAnimationPresentationEvent> presentationEvents = null)
        {
            BindingId = bindingId ?? string.Empty;
            ActionKey = actionKey ?? string.Empty;
            Clip = clip;
            Layer = layer;
            PlaybackSpeed = playbackSpeed;
            Loop = loop;
            AlignmentPolicy = alignmentPolicy;
            _presentationEvents = presentationEvents != null
                ? new List<MxAnimationPresentationEvent>(presentationEvents)
                : new List<MxAnimationPresentationEvent>();
        }

        public string BindingId { get; }
        public string ActionKey { get; }
        public ResourceKey Clip { get; }
        public MxAnimationLayerId Layer { get; }
        public float PlaybackSpeed { get; }
        public bool Loop { get; }
        public MxAnimationAlignmentPolicy AlignmentPolicy { get; }
        public IReadOnlyList<MxAnimationPresentationEvent> PresentationEvents => _presentationEvents;
    }

    public sealed class MxAnimationSetDefinition
    {
        private readonly List<MxAnimationActionBinding> _actions;
        private readonly List<MxAnimationPresentationEvent> _events;

        public MxAnimationSetDefinition(
            string setId,
            int version,
            ResourceKey defaultClip,
            ResourceKey fallbackClip,
            IEnumerable<MxAnimationActionBinding> actions = null,
            IEnumerable<MxAnimationPresentationEvent> events = null)
        {
            SetId = setId ?? string.Empty;
            Version = version;
            DefaultClip = defaultClip;
            FallbackClip = fallbackClip;
            _actions = actions != null
                ? new List<MxAnimationActionBinding>(actions)
                : new List<MxAnimationActionBinding>();
            _events = events != null
                ? new List<MxAnimationPresentationEvent>(events)
                : new List<MxAnimationPresentationEvent>();
        }

        public string SetId { get; }
        public int Version { get; }
        public ResourceKey DefaultClip { get; }
        public ResourceKey FallbackClip { get; }
        public IReadOnlyList<MxAnimationActionBinding> Actions => _actions;
        public IReadOnlyList<MxAnimationPresentationEvent> Events => _events;

        public bool TryFindBinding(string bindingId, string actionKey, out MxAnimationActionBinding binding)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                MxAnimationActionBinding candidate = _actions[i];
                bool bindingMatches = !string.IsNullOrWhiteSpace(bindingId)
                    && string.Equals(candidate.BindingId, bindingId, StringComparison.Ordinal);
                bool actionMatches = !string.IsNullOrWhiteSpace(actionKey)
                    && string.Equals(candidate.ActionKey, actionKey, StringComparison.Ordinal);
                if (!bindingMatches && !actionMatches)
                    continue;

                binding = candidate;
                return true;
            }

            binding = null;
            return false;
        }
    }

    public sealed class MxAnimationPlayRequest
    {
        public string TargetActorId { get; set; } = string.Empty;
        public string BindingId { get; set; } = string.Empty;
        public string ActionKey { get; set; } = string.Empty;
        public ResourceKey ClipKey { get; set; }
        public MxAnimationLayerId LayerId { get; set; } = MxAnimationLayerId.Base;
        public float PlaybackSpeed { get; set; } = 1f;
        public float StartOffsetSeconds { get; set; }
        public bool Loop { get; set; }
        public MxAnimationAlignmentPolicy AlignmentPolicy { get; set; } = MxAnimationAlignmentPolicy.StartAtZero;
        public string CorrelationId { get; set; } = string.Empty;
    }

    public sealed class MxAnimationStopRequest
    {
        public string TargetActorId { get; set; } = string.Empty;
        public string BindingId { get; set; } = string.Empty;
        public MxAnimationLayerId LayerId { get; set; } = MxAnimationLayerId.Base;
        public float FadeOutDurationSeconds { get; set; }
        public string StopReason { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
    }

    public sealed class MxAnimationCrossFadeRequest
    {
        public string TargetActorId { get; set; } = string.Empty;
        public string BindingId { get; set; } = string.Empty;
        public string ActionKey { get; set; } = string.Empty;
        public ResourceKey ClipKey { get; set; }
        public MxAnimationLayerId LayerId { get; set; } = MxAnimationLayerId.Base;
        public float FadeDurationSeconds { get; set; } = 0.15f;
        public float TargetStartOffsetSeconds { get; set; }
        public float PlaybackSpeed { get; set; } = 1f;
        public bool Loop { get; set; }
        public MxAnimationAlignmentPolicy AlignmentPolicy { get; set; } = MxAnimationAlignmentPolicy.StartAtZero;
        public MxAnimationOutgoingReleasePolicy OutgoingReleasePolicy { get; set; } = MxAnimationOutgoingReleasePolicy.ReleaseWhenGraphDetached;
        public string CorrelationId { get; set; } = string.Empty;
    }

    public sealed class MxAnimationFadeDiagnostic
    {
        public MxAnimationFadeDiagnostic(
            MxAnimationLayerId layerId,
            ResourceKey currentClipKey,
            ResourceKey nextClipKey,
            float elapsedSeconds,
            float durationSeconds,
            float blendWeight,
            MxAnimationLayerStatus status)
        {
            LayerId = layerId;
            CurrentClipKey = currentClipKey;
            NextClipKey = nextClipKey;
            ElapsedSeconds = elapsedSeconds;
            DurationSeconds = durationSeconds;
            BlendWeight = blendWeight;
            Status = status;
        }

        public MxAnimationLayerId LayerId { get; }
        public ResourceKey CurrentClipKey { get; }
        public ResourceKey NextClipKey { get; }
        public float ElapsedSeconds { get; }
        public float DurationSeconds { get; }
        public float BlendWeight { get; }
        public MxAnimationLayerStatus Status { get; }
    }

    public sealed class MxAnimationLayerDiagnostic
    {
        public MxAnimationLayerDiagnostic(
            MxAnimationLayerId layerId,
            MxAnimationLayerStatus status,
            ResourceKey currentClipKey,
            ResourceKey nextClipKey,
            bool currentClipIsFallback,
            float currentWeight,
            float outgoingWeight,
            int activePlayableCount,
            MxAnimationFadeDiagnostic fade,
            ResourceError lastError)
        {
            LayerId = layerId;
            Status = status;
            CurrentClipKey = currentClipKey;
            NextClipKey = nextClipKey;
            CurrentClipIsFallback = currentClipIsFallback;
            CurrentWeight = currentWeight;
            OutgoingWeight = outgoingWeight;
            ActivePlayableCount = activePlayableCount;
            Fade = fade;
            LastError = lastError;
        }

        public MxAnimationLayerId LayerId { get; }
        public MxAnimationLayerStatus Status { get; }
        public ResourceKey CurrentClipKey { get; }
        public ResourceKey NextClipKey { get; }
        public bool CurrentClipIsFallback { get; }
        public float CurrentWeight { get; }
        public float OutgoingWeight { get; }
        public int ActivePlayableCount { get; }
        public MxAnimationFadeDiagnostic Fade { get; }
        public ResourceError LastError { get; }
    }

    public sealed class MxAnimationResourceDiagnostic
    {
        public MxAnimationResourceDiagnostic(
            string role,
            ResourceKey key,
            MxAnimationResourceLoadStatus status,
            bool resident,
            ResourceError lastError)
        {
            Role = role ?? string.Empty;
            Key = key;
            Status = status;
            Resident = resident;
            LastError = lastError;
        }

        public string Role { get; }
        public ResourceKey Key { get; }
        public MxAnimationResourceLoadStatus Status { get; }
        public bool Resident { get; }
        public ResourceError LastError { get; }
    }

    public sealed class MxAnimationRequestDiagnostic
    {
        public MxAnimationRequestDiagnostic(
            MxAnimationRequestKind kind,
            MxAnimationLayerId layerId,
            ResourceKey requestedClipKey,
            ResourceKey resolvedClipKey,
            bool usedFallback,
            MxAnimationBackendResultCode resultCode,
            string correlationId,
            string message)
        {
            Kind = kind;
            LayerId = layerId;
            RequestedClipKey = requestedClipKey;
            ResolvedClipKey = resolvedClipKey;
            UsedFallback = usedFallback;
            ResultCode = resultCode;
            CorrelationId = correlationId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public MxAnimationRequestKind Kind { get; }
        public MxAnimationLayerId LayerId { get; }
        public ResourceKey RequestedClipKey { get; }
        public ResourceKey ResolvedClipKey { get; }
        public bool UsedFallback { get; }
        public MxAnimationBackendResultCode ResultCode { get; }
        public string CorrelationId { get; }
        public string Message { get; }
    }

    public sealed class MxAnimationDiagnosticSnapshot
    {
        private readonly List<MxAnimationLayerDiagnostic> _layerStates;
        private readonly List<MxAnimationFadeDiagnostic> _activeFades;
        private readonly List<MxAnimationRequestDiagnostic> _recentRequests;
        private readonly List<ResourceError> _recentResourceErrors;

        public MxAnimationDiagnosticSnapshot(
            string backendName,
            string actorId,
            string setId,
            int actorCount,
            bool graphIsValid,
            bool isReleased,
            MxAnimationResourceDiagnostic defaultClip,
            MxAnimationResourceDiagnostic fallbackClip,
            IEnumerable<MxAnimationLayerDiagnostic> layerStates,
            IEnumerable<MxAnimationFadeDiagnostic> activeFades,
            IEnumerable<MxAnimationRequestDiagnostic> recentRequests,
            IEnumerable<ResourceError> recentResourceErrors)
        {
            BackendName = backendName ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            SetId = setId ?? string.Empty;
            ActorCount = actorCount;
            GraphIsValid = graphIsValid;
            IsReleased = isReleased;
            DefaultClip = defaultClip;
            FallbackClip = fallbackClip;
            _layerStates = layerStates != null
                ? new List<MxAnimationLayerDiagnostic>(layerStates)
                : new List<MxAnimationLayerDiagnostic>();
            _activeFades = activeFades != null
                ? new List<MxAnimationFadeDiagnostic>(activeFades)
                : new List<MxAnimationFadeDiagnostic>();
            _recentRequests = recentRequests != null
                ? new List<MxAnimationRequestDiagnostic>(recentRequests)
                : new List<MxAnimationRequestDiagnostic>();
            _recentResourceErrors = recentResourceErrors != null
                ? new List<ResourceError>(recentResourceErrors)
                : new List<ResourceError>();
        }

        public string BackendName { get; }
        public string ActorId { get; }
        public string SetId { get; }
        public int ActorCount { get; }
        public bool GraphIsValid { get; }
        public bool IsReleased { get; }
        public MxAnimationResourceDiagnostic DefaultClip { get; }
        public MxAnimationResourceDiagnostic FallbackClip { get; }
        public IReadOnlyList<MxAnimationLayerDiagnostic> LayerStates => _layerStates;
        public IReadOnlyList<MxAnimationFadeDiagnostic> ActiveFades => _activeFades;
        public IReadOnlyList<MxAnimationRequestDiagnostic> RecentRequests => _recentRequests;
        public IReadOnlyList<ResourceError> RecentResourceErrors => _recentResourceErrors;
    }

    public readonly struct MxAnimationBackendResult
    {
        public MxAnimationBackendResult(
            bool success,
            MxAnimationBackendResultCode code,
            ResourceKey clipKey,
            ResourceError resourceError,
            string message)
        {
            Success = success;
            Code = code;
            ClipKey = clipKey;
            ResourceError = resourceError;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public MxAnimationBackendResultCode Code { get; }
        public ResourceKey ClipKey { get; }
        public ResourceError ResourceError { get; }
        public string Message { get; }

        public static MxAnimationBackendResult Succeeded(ResourceKey clipKey, string message)
        {
            return new MxAnimationBackendResult(true, MxAnimationBackendResultCode.Success, clipKey, ResourceError.None, message);
        }

        public static MxAnimationBackendResult Queued(ResourceKey clipKey, string message)
        {
            return new MxAnimationBackendResult(true, MxAnimationBackendResultCode.Queued, clipKey, ResourceError.None, message);
        }

        public static MxAnimationBackendResult Failed(MxAnimationBackendResultCode code, ResourceKey clipKey, string message)
        {
            return new MxAnimationBackendResult(false, code, clipKey, ResourceError.None, message);
        }

        public static MxAnimationBackendResult Failed(MxAnimationBackendResultCode code, ResourceKey clipKey, ResourceError error, string message)
        {
            return new MxAnimationBackendResult(false, code, clipKey, error, message);
        }
    }

    public interface IMxAnimationBackend : IDisposable
    {
        string BackendName { get; }
        MxAnimationBackendResult Play(MxAnimationPlayRequest request);
        MxAnimationBackendResult Stop(MxAnimationStopRequest request);
        MxAnimationBackendResult CrossFade(MxAnimationCrossFadeRequest request);
        void Tick(float deltaTime);
        MxAnimationDiagnosticSnapshot CreateSnapshot();
        void Release();
    }
}
