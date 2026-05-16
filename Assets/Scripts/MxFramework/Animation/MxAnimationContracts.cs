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

    public enum MxAnimationPresentationEventReplayPolicy
    {
        OneShot,
        CatchUpSafe
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
        SetLayerWeight,
        SetBlend1D,
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

    public enum MxAnimationLayerBlendMode
    {
        Override,
        Additive
    }

    public enum MxAnimationLayerMaskStatus
    {
        None,
        NotConfigured,
        Loading,
        Loaded,
        Failed,
        Released
    }

    public enum MxAnimationPresentationSyncStatus
    {
        None,
        Started,
        Running,
        Canceled,
        Finished,
        Interrupted
    }

    public enum MxAnimationPresentationSyncValidationCode
    {
        Success,
        MissingState,
        MissingActorId,
        MissingAnimationSetId,
        AnimationSetIdMismatch,
        AnimationSetVersionMismatch,
        AnimationSetHashMismatch,
        ResourceCatalogHashMismatch,
        ClipRegistryVersionMismatch
    }

    public enum MxAnimationPresentationEventDispatchStatus
    {
        Dispatched,
        DuplicateDropped,
        PayloadUnresolved
    }

    public readonly struct MxAnimationLayerSyncState : IEquatable<MxAnimationLayerSyncState>
    {
        public MxAnimationLayerSyncState(
            MxAnimationLayerId layerId,
            float currentWeight,
            float targetWeight,
            int transitionStartedAtFrame = 0,
            int transitionDurationFrames = 0,
            int transitionRemainingFrames = 0,
            string transitionPolicyId = "",
            string correlationId = "")
        {
            LayerId = layerId;
            CurrentWeight = Clamp01(currentWeight);
            TargetWeight = Clamp01(targetWeight);
            int durationFrames = Math.Max(0, transitionDurationFrames);
            TransitionStartedAtFrame = Math.Max(0, transitionStartedAtFrame);
            TransitionDurationFrames = durationFrames;
            TransitionRemainingFrames = durationFrames == 0
                ? 0
                : Math.Min(durationFrames, Math.Max(0, transitionRemainingFrames));
            TransitionPolicyId = transitionPolicyId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
        }

        public MxAnimationLayerId LayerId { get; }
        public float CurrentWeight { get; }
        public float TargetWeight { get; }
        public int TransitionStartedAtFrame { get; }
        public int TransitionDurationFrames { get; }
        public int TransitionRemainingFrames { get; }
        public string TransitionPolicyId { get; }
        public string CorrelationId { get; }
        public bool IsTransitioning => TransitionDurationFrames > 0
            && TransitionRemainingFrames > 0
            && Math.Abs(CurrentWeight - TargetWeight) > 0.0001f;

        public bool Equals(MxAnimationLayerSyncState other)
        {
            return LayerId.Equals(other.LayerId)
                && CurrentWeight.Equals(other.CurrentWeight)
                && TargetWeight.Equals(other.TargetWeight)
                && TransitionStartedAtFrame == other.TransitionStartedAtFrame
                && TransitionDurationFrames == other.TransitionDurationFrames
                && TransitionRemainingFrames == other.TransitionRemainingFrames
                && string.Equals(TransitionPolicyId, other.TransitionPolicyId, StringComparison.Ordinal)
                && string.Equals(CorrelationId, other.CorrelationId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MxAnimationLayerSyncState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = LayerId.GetHashCode();
                hash = (hash * 397) ^ CurrentWeight.GetHashCode();
                hash = (hash * 397) ^ TargetWeight.GetHashCode();
                hash = (hash * 397) ^ TransitionStartedAtFrame;
                hash = (hash * 397) ^ TransitionDurationFrames;
                hash = (hash * 397) ^ TransitionRemainingFrames;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(TransitionPolicyId ?? string.Empty);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(CorrelationId ?? string.Empty);
                return hash;
            }
        }

        public static bool operator ==(MxAnimationLayerSyncState left, MxAnimationLayerSyncState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxAnimationLayerSyncState left, MxAnimationLayerSyncState right)
        {
            return !left.Equals(right);
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }

    public readonly struct MxAnimationQuantizedParameter : IEquatable<MxAnimationQuantizedParameter>
    {
        public MxAnimationQuantizedParameter(string parameterId, int quantizedValue, int scale = 1000)
        {
            ParameterId = parameterId ?? string.Empty;
            QuantizedValue = quantizedValue;
            Scale = scale <= 0 ? 1 : scale;
        }

        public string ParameterId { get; }
        public int QuantizedValue { get; }
        public int Scale { get; }
        public float Value => QuantizedValue / (float)(Scale <= 0 ? 1 : Scale);

        public bool Equals(MxAnimationQuantizedParameter other)
        {
            return string.Equals(ParameterId, other.ParameterId, StringComparison.Ordinal)
                && QuantizedValue == other.QuantizedValue
                && Scale == other.Scale;
        }

        public override bool Equals(object obj)
        {
            return obj is MxAnimationQuantizedParameter other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(ParameterId ?? string.Empty);
                hash = (hash * 397) ^ QuantizedValue;
                hash = (hash * 397) ^ Scale;
                return hash;
            }
        }

        public static bool operator ==(MxAnimationQuantizedParameter left, MxAnimationQuantizedParameter right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxAnimationQuantizedParameter left, MxAnimationQuantizedParameter right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class MxAnimationBlend1DPoint
    {
        public MxAnimationBlend1DPoint(
            int threshold,
            ResourceKey clipKey,
            float playbackSpeed = 1f,
            bool loop = true)
        {
            Threshold = threshold;
            ClipKey = clipKey;
            PlaybackSpeed = Math.Abs(playbackSpeed) < 0.0001f ? 1f : playbackSpeed;
            Loop = loop;
        }

        public int Threshold { get; }
        public ResourceKey ClipKey { get; }
        public float PlaybackSpeed { get; }
        public bool Loop { get; }
    }

    public sealed class MxAnimationBlend1DDefinition
    {
        private readonly List<MxAnimationBlend1DPoint> _points;

        public MxAnimationBlend1DDefinition(
            string blendId,
            string parameterId,
            MxAnimationLayerId layerId,
            IEnumerable<MxAnimationBlend1DPoint> points,
            int parameterScale = 1000,
            float fadeDurationSeconds = 0.1f)
        {
            BlendId = blendId ?? string.Empty;
            ParameterId = parameterId ?? string.Empty;
            LayerId = layerId;
            ParameterScale = parameterScale <= 0 ? 1 : parameterScale;
            FadeDurationSeconds = fadeDurationSeconds < 0f ? 0f : fadeDurationSeconds;
            _points = points != null
                ? new List<MxAnimationBlend1DPoint>(points)
                : new List<MxAnimationBlend1DPoint>();
            _points.Sort(ComparePoints);
        }

        public string BlendId { get; }
        public string ParameterId { get; }
        public MxAnimationLayerId LayerId { get; }
        public int ParameterScale { get; }
        public float FadeDurationSeconds { get; }
        public IReadOnlyList<MxAnimationBlend1DPoint> Points => _points;

        private static int ComparePoints(MxAnimationBlend1DPoint left, MxAnimationBlend1DPoint right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int result = left.Threshold.CompareTo(right.Threshold);
            if (result != 0)
                return result;

            return string.CompareOrdinal(left.ClipKey.ToString(), right.ClipKey.ToString());
        }
    }

    public readonly struct MxAnimationBlend1DWeight
    {
        public MxAnimationBlend1DWeight(
            ResourceKey clipKey,
            int threshold,
            float weight,
            float playbackSpeed,
            bool loop)
        {
            ClipKey = clipKey;
            Threshold = threshold;
            Weight = Clamp01(weight);
            PlaybackSpeed = Math.Abs(playbackSpeed) < 0.0001f ? 1f : playbackSpeed;
            Loop = loop;
        }

        public ResourceKey ClipKey { get; }
        public int Threshold { get; }
        public float Weight { get; }
        public float PlaybackSpeed { get; }
        public bool Loop { get; }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }

    public sealed class MxAnimationBlend1DWeights
    {
        private readonly List<MxAnimationBlend1DWeight> _weights;

        public MxAnimationBlend1DWeights(
            string blendId,
            MxAnimationQuantizedParameter parameter,
            IEnumerable<MxAnimationBlend1DWeight> weights)
        {
            BlendId = blendId ?? string.Empty;
            Parameter = parameter;
            _weights = weights != null
                ? new List<MxAnimationBlend1DWeight>(weights)
                : new List<MxAnimationBlend1DWeight>();
        }

        public string BlendId { get; }
        public MxAnimationQuantizedParameter Parameter { get; }
        public IReadOnlyList<MxAnimationBlend1DWeight> Weights => _weights;
    }

    public static class MxAnimationBlend1DCalculator
    {
        public static MxAnimationBlend1DWeights Evaluate(
            MxAnimationBlend1DDefinition definition,
            MxAnimationQuantizedParameter parameter)
        {
            if (definition == null || definition.Points.Count == 0)
                return new MxAnimationBlend1DWeights(string.Empty, parameter, null);

            var weights = new List<MxAnimationBlend1DWeight>(definition.Points.Count);
            int value = string.Equals(parameter.ParameterId, definition.ParameterId, StringComparison.Ordinal)
                ? parameter.QuantizedValue
                : 0;

            if (value <= definition.Points[0].Threshold)
            {
                AddWeights(definition.Points, 0, 1f, -1, 0f, weights);
                return new MxAnimationBlend1DWeights(definition.BlendId, parameter, weights);
            }

            int last = definition.Points.Count - 1;
            if (value >= definition.Points[last].Threshold)
            {
                AddWeights(definition.Points, last, 1f, -1, 0f, weights);
                return new MxAnimationBlend1DWeights(definition.BlendId, parameter, weights);
            }

            for (int i = 0; i < last; i++)
            {
                MxAnimationBlend1DPoint lower = definition.Points[i];
                MxAnimationBlend1DPoint upper = definition.Points[i + 1];
                if (value < lower.Threshold || value > upper.Threshold)
                    continue;

                int span = Math.Max(1, upper.Threshold - lower.Threshold);
                float t = (value - lower.Threshold) / (float)span;
                AddWeights(definition.Points, i, 1f - t, i + 1, t, weights);
                return new MxAnimationBlend1DWeights(definition.BlendId, parameter, weights);
            }

            AddWeights(definition.Points, 0, 1f, -1, 0f, weights);
            return new MxAnimationBlend1DWeights(definition.BlendId, parameter, weights);
        }

        private static void AddWeights(
            IReadOnlyList<MxAnimationBlend1DPoint> points,
            int firstIndex,
            float firstWeight,
            int secondIndex,
            float secondWeight,
            List<MxAnimationBlend1DWeight> results)
        {
            for (int i = 0; i < points.Count; i++)
            {
                MxAnimationBlend1DPoint point = points[i];
                float weight = i == firstIndex ? firstWeight : (i == secondIndex ? secondWeight : 0f);
                results.Add(new MxAnimationBlend1DWeight(
                    point.ClipKey,
                    point.Threshold,
                    weight,
                    point.PlaybackSpeed,
                    point.Loop));
            }
        }
    }

    public readonly struct MxAnimationPresentationEventDedupeKey : IEquatable<MxAnimationPresentationEventDedupeKey>
    {
        public MxAnimationPresentationEventDedupeKey(
            string actorId,
            int actionInstanceId,
            int worldFrame,
            int localFrame,
            string eventId,
            int sourceOrder)
        {
            ActorId = actorId ?? string.Empty;
            ActionInstanceId = Math.Max(0, actionInstanceId);
            WorldFrame = Math.Max(0, worldFrame);
            LocalFrame = Math.Max(0, localFrame);
            EventId = eventId ?? string.Empty;
            SourceOrder = sourceOrder;
        }

        public string ActorId { get; }
        public int ActionInstanceId { get; }
        public int WorldFrame { get; }
        public int LocalFrame { get; }
        public string EventId { get; }
        public int SourceOrder { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(ActorId)
            && !string.IsNullOrWhiteSpace(EventId);

        public bool Equals(MxAnimationPresentationEventDedupeKey other)
        {
            return string.Equals(ActorId, other.ActorId, StringComparison.Ordinal)
                && ActionInstanceId == other.ActionInstanceId
                && WorldFrame == other.WorldFrame
                && LocalFrame == other.LocalFrame
                && string.Equals(EventId, other.EventId, StringComparison.Ordinal)
                && SourceOrder == other.SourceOrder;
        }

        public override bool Equals(object obj)
        {
            return obj is MxAnimationPresentationEventDedupeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(ActorId ?? string.Empty);
                hash = (hash * 397) ^ ActionInstanceId;
                hash = (hash * 397) ^ WorldFrame;
                hash = (hash * 397) ^ LocalFrame;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(EventId ?? string.Empty);
                hash = (hash * 397) ^ SourceOrder;
                return hash;
            }
        }

        public static bool operator ==(MxAnimationPresentationEventDedupeKey left, MxAnimationPresentationEventDedupeKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxAnimationPresentationEventDedupeKey left, MxAnimationPresentationEventDedupeKey right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class MxAnimationPresentationEventDispatch
    {
        public MxAnimationPresentationEventDispatch(
            string actorId,
            string actionKey,
            string bindingId,
            int actionInstanceId,
            int worldFrame,
            int localFrame,
            int sourceOrder,
            MxAnimationPresentationEvent presentationEvent,
            string correlationId = "",
            MxAnimationPresentationEventDedupeKey dedupeKey = default)
        {
            ActorId = actorId ?? string.Empty;
            ActionKey = actionKey ?? string.Empty;
            BindingId = bindingId ?? string.Empty;
            ActionInstanceId = Math.Max(0, actionInstanceId);
            WorldFrame = Math.Max(0, worldFrame);
            LocalFrame = Math.Max(0, localFrame);
            SourceOrder = Math.Max(0, sourceOrder);
            PresentationEvent = presentationEvent;
            CorrelationId = correlationId ?? string.Empty;
            DedupeKey = dedupeKey.IsValid
                ? dedupeKey
                : new MxAnimationPresentationEventDedupeKey(
                    ActorId,
                    ActionInstanceId,
                    WorldFrame,
                    LocalFrame,
                    presentationEvent != null ? presentationEvent.EventId : string.Empty,
                    SourceOrder);
        }

        public string ActorId { get; }
        public string ActionKey { get; }
        public string BindingId { get; }
        public int ActionInstanceId { get; }
        public int WorldFrame { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public MxAnimationPresentationEvent PresentationEvent { get; }
        public string CorrelationId { get; }
        public MxAnimationPresentationEventDedupeKey DedupeKey { get; }
    }

    public interface IMxAnimationPresentationEventSink
    {
        void Dispatch(MxAnimationPresentationEventDispatch dispatch);
    }

    public sealed class MxAnimationNullPresentationEventSink : IMxAnimationPresentationEventSink
    {
        public static readonly MxAnimationNullPresentationEventSink Instance = new MxAnimationNullPresentationEventSink();

        private MxAnimationNullPresentationEventSink()
        {
        }

        public void Dispatch(MxAnimationPresentationEventDispatch dispatch)
        {
        }
    }

    public readonly struct MxAnimationPresentationEventDispatchDiagnostic
    {
        public MxAnimationPresentationEventDispatchDiagnostic(
            MxAnimationPresentationEventDispatchStatus status,
            MxAnimationPresentationEventDedupeKey dedupeKey,
            string eventId,
            string correlationId,
            string message)
        {
            Status = status;
            DedupeKey = dedupeKey;
            EventId = eventId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public MxAnimationPresentationEventDispatchStatus Status { get; }
        public MxAnimationPresentationEventDedupeKey DedupeKey { get; }
        public string EventId { get; }
        public string CorrelationId { get; }
        public string Message { get; }
    }

    public sealed class MxAnimationPresentationEventDedupeWindow
    {
        private readonly HashSet<MxAnimationPresentationEventDedupeKey> _seen =
            new HashSet<MxAnimationPresentationEventDedupeKey>();
        private readonly Queue<MxAnimationPresentationEventDedupeKey> _order =
            new Queue<MxAnimationPresentationEventDedupeKey>();

        public MxAnimationPresentationEventDedupeWindow(int capacity = 128)
        {
            Capacity = Math.Max(1, capacity);
        }

        public int Capacity { get; }
        public int Count => _seen.Count;

        public bool TryRecord(MxAnimationPresentationEventDedupeKey key)
        {
            if (!key.IsValid)
                return true;

            if (_seen.Contains(key))
                return false;

            while (_order.Count >= Capacity)
            {
                MxAnimationPresentationEventDedupeKey evicted = _order.Dequeue();
                _seen.Remove(evicted);
            }

            _seen.Add(key);
            _order.Enqueue(key);
            return true;
        }

        public void Clear()
        {
            _seen.Clear();
            _order.Clear();
        }
    }

    public sealed class MxAnimationPresentationEventDispatchSink
    {
        private readonly IMxAnimationPresentationEventSink _sink;
        private readonly MxAnimationPresentationEventDedupeWindow _dedupeWindow;
        private readonly Queue<MxAnimationPresentationEventDispatchDiagnostic> _recentDiagnostics =
            new Queue<MxAnimationPresentationEventDispatchDiagnostic>();

        public MxAnimationPresentationEventDispatchSink(
            IMxAnimationPresentationEventSink sink = null,
            int maxDedupeEntries = 128,
            int maxRecentDiagnostics = 32)
        {
            _sink = sink ?? MxAnimationNullPresentationEventSink.Instance;
            _dedupeWindow = new MxAnimationPresentationEventDedupeWindow(maxDedupeEntries);
            MaxRecentDiagnostics = Math.Max(1, maxRecentDiagnostics);
        }

        public int MaxRecentDiagnostics { get; }
        public IReadOnlyCollection<MxAnimationPresentationEventDispatchDiagnostic> RecentDiagnostics => _recentDiagnostics;

        public bool TryDispatch(
            MxAnimationPresentationEventDispatch dispatch,
            bool payloadResolved,
            out MxAnimationPresentationEventDispatchDiagnostic diagnostic)
        {
            if (dispatch == null)
            {
                diagnostic = new MxAnimationPresentationEventDispatchDiagnostic(
                    MxAnimationPresentationEventDispatchStatus.PayloadUnresolved,
                    default,
                    string.Empty,
                    string.Empty,
                    "Presentation event dispatch payload is null.");
                AddDiagnostic(diagnostic);
                return false;
            }

            string eventId = dispatch.PresentationEvent != null ? dispatch.PresentationEvent.EventId : string.Empty;
            if (!payloadResolved
                && dispatch.PresentationEvent != null
                && dispatch.PresentationEvent.PayloadKey.IsValid)
            {
                diagnostic = new MxAnimationPresentationEventDispatchDiagnostic(
                    MxAnimationPresentationEventDispatchStatus.PayloadUnresolved,
                    dispatch.DedupeKey,
                    eventId,
                    dispatch.CorrelationId,
                    "Presentation event payload could not be resolved.");
                AddDiagnostic(diagnostic);
                return false;
            }

            if (!_dedupeWindow.TryRecord(dispatch.DedupeKey))
            {
                diagnostic = new MxAnimationPresentationEventDispatchDiagnostic(
                    MxAnimationPresentationEventDispatchStatus.DuplicateDropped,
                    dispatch.DedupeKey,
                    eventId,
                    dispatch.CorrelationId,
                    "Duplicate presentation event dispatch dropped.");
                AddDiagnostic(diagnostic);
                return false;
            }

            _sink.Dispatch(dispatch);
            diagnostic = new MxAnimationPresentationEventDispatchDiagnostic(
                MxAnimationPresentationEventDispatchStatus.Dispatched,
                dispatch.DedupeKey,
                eventId,
                dispatch.CorrelationId,
                "Presentation event dispatched.");
            AddDiagnostic(diagnostic);
            return true;
        }

        private void AddDiagnostic(MxAnimationPresentationEventDispatchDiagnostic diagnostic)
        {
            while (_recentDiagnostics.Count >= MaxRecentDiagnostics)
                _recentDiagnostics.Dequeue();

            _recentDiagnostics.Enqueue(diagnostic);
        }
    }

    public sealed class MxAnimationPresentationSyncState
    {
        private readonly List<MxAnimationLayerSyncState> _layerStates;
        private readonly List<MxAnimationQuantizedParameter> _blendParameters;

        public MxAnimationPresentationSyncState(
            string actorId,
            string animationSetId,
            int animationSetVersion,
            string animationSetHash,
            string resourceCatalogHash,
            int clipRegistryVersion,
            int actionId,
            string actionKey,
            int actionInstanceId,
            int startedAtCombatFrame,
            int localFrame,
            MxAnimationPresentationSyncStatus status,
            IEnumerable<MxAnimationLayerSyncState> layerStates = null,
            IEnumerable<MxAnimationQuantizedParameter> blendParameters = null,
            string correlationId = "")
        {
            ActorId = actorId ?? string.Empty;
            AnimationSetId = animationSetId ?? string.Empty;
            AnimationSetVersion = Math.Max(0, animationSetVersion);
            AnimationSetHash = animationSetHash ?? string.Empty;
            ResourceCatalogHash = resourceCatalogHash ?? string.Empty;
            ClipRegistryVersion = Math.Max(0, clipRegistryVersion);
            ActionId = Math.Max(0, actionId);
            ActionKey = actionKey ?? string.Empty;
            ActionInstanceId = Math.Max(0, actionInstanceId);
            StartedAtCombatFrame = Math.Max(0, startedAtCombatFrame);
            LocalFrame = Math.Max(0, localFrame);
            Status = status;
            CorrelationId = correlationId ?? string.Empty;
            _layerStates = layerStates != null
                ? new List<MxAnimationLayerSyncState>(layerStates)
                : new List<MxAnimationLayerSyncState>();
            _blendParameters = blendParameters != null
                ? new List<MxAnimationQuantizedParameter>(blendParameters)
                : new List<MxAnimationQuantizedParameter>();
        }

        public string ActorId { get; }
        public string AnimationSetId { get; }
        public int AnimationSetVersion { get; }
        public string AnimationSetHash { get; }
        public string ResourceCatalogHash { get; }
        public int ClipRegistryVersion { get; }
        public int ActionId { get; }
        public string ActionKey { get; }
        public int ActionInstanceId { get; }
        public int StartedAtCombatFrame { get; }
        public int LocalFrame { get; }
        public MxAnimationPresentationSyncStatus Status { get; }
        public string CorrelationId { get; }
        public IReadOnlyList<MxAnimationLayerSyncState> LayerStates => _layerStates;
        public IReadOnlyList<MxAnimationQuantizedParameter> BlendParameters => _blendParameters;

        public bool TryFindLayerState(MxAnimationLayerId layerId, out MxAnimationLayerSyncState state)
        {
            for (int i = 0; i < _layerStates.Count; i++)
            {
                MxAnimationLayerSyncState candidate = _layerStates[i];
                if (candidate.LayerId != layerId)
                    continue;

                state = candidate;
                return true;
            }

            state = default;
            return false;
        }

        public bool TryFindBlendParameter(string parameterId, out MxAnimationQuantizedParameter parameter)
        {
            for (int i = 0; i < _blendParameters.Count; i++)
            {
                MxAnimationQuantizedParameter candidate = _blendParameters[i];
                if (!string.Equals(candidate.ParameterId, parameterId ?? string.Empty, StringComparison.Ordinal))
                    continue;

                parameter = candidate;
                return true;
            }

            parameter = default;
            return false;
        }
    }

    public readonly struct MxAnimationPresentationSyncVersionExpectation
    {
        public MxAnimationPresentationSyncVersionExpectation(
            string animationSetId,
            int animationSetVersion,
            string animationSetHash,
            string resourceCatalogHash,
            int clipRegistryVersion)
        {
            AnimationSetId = animationSetId ?? string.Empty;
            AnimationSetVersion = animationSetVersion;
            AnimationSetHash = animationSetHash ?? string.Empty;
            ResourceCatalogHash = resourceCatalogHash ?? string.Empty;
            ClipRegistryVersion = clipRegistryVersion;
        }

        public string AnimationSetId { get; }
        public int AnimationSetVersion { get; }
        public string AnimationSetHash { get; }
        public string ResourceCatalogHash { get; }
        public int ClipRegistryVersion { get; }

        public static MxAnimationPresentationSyncVersionExpectation None =>
            new MxAnimationPresentationSyncVersionExpectation(string.Empty, -1, string.Empty, string.Empty, -1);
    }

    public readonly struct MxAnimationPresentationSyncValidationResult
    {
        private MxAnimationPresentationSyncValidationResult(
            bool success,
            MxAnimationPresentationSyncValidationCode code,
            string field,
            string expected,
            string actual,
            string message)
        {
            Success = success;
            Code = code;
            Field = field ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public MxAnimationPresentationSyncValidationCode Code { get; }
        public string Field { get; }
        public string Expected { get; }
        public string Actual { get; }
        public string Message { get; }

        public static MxAnimationPresentationSyncValidationResult Succeeded()
        {
            return new MxAnimationPresentationSyncValidationResult(
                true,
                MxAnimationPresentationSyncValidationCode.Success,
                string.Empty,
                string.Empty,
                string.Empty,
                "Presentation sync state is compatible.");
        }

        public static MxAnimationPresentationSyncValidationResult Failed(
            MxAnimationPresentationSyncValidationCode code,
            string field,
            string expected,
            string actual,
            string message)
        {
            return new MxAnimationPresentationSyncValidationResult(false, code, field, expected, actual, message);
        }
    }

    public static class MxAnimationPresentationSyncValidator
    {
        public static MxAnimationPresentationSyncValidationResult Validate(
            MxAnimationPresentationSyncState state,
            MxAnimationPresentationSyncVersionExpectation expectation)
        {
            if (state == null)
            {
                return MxAnimationPresentationSyncValidationResult.Failed(
                    MxAnimationPresentationSyncValidationCode.MissingState,
                    "state",
                    "non-null",
                    "null",
                    "Presentation sync state is missing.");
            }

            if (string.IsNullOrWhiteSpace(state.ActorId))
            {
                return MxAnimationPresentationSyncValidationResult.Failed(
                    MxAnimationPresentationSyncValidationCode.MissingActorId,
                    "actorId",
                    "non-empty",
                    state.ActorId,
                    "Presentation sync state is missing an actor id.");
            }

            if (string.IsNullOrWhiteSpace(state.AnimationSetId))
            {
                return MxAnimationPresentationSyncValidationResult.Failed(
                    MxAnimationPresentationSyncValidationCode.MissingAnimationSetId,
                    "animationSetId",
                    "non-empty",
                    state.AnimationSetId,
                    "Presentation sync state is missing an animation set id.");
            }

            if (!string.IsNullOrWhiteSpace(expectation.AnimationSetId)
                && !string.Equals(state.AnimationSetId, expectation.AnimationSetId, StringComparison.Ordinal))
            {
                return Mismatch(
                    MxAnimationPresentationSyncValidationCode.AnimationSetIdMismatch,
                    "animationSetId",
                    expectation.AnimationSetId,
                    state.AnimationSetId);
            }

            if (expectation.AnimationSetVersion >= 0
                && state.AnimationSetVersion != expectation.AnimationSetVersion)
            {
                return Mismatch(
                    MxAnimationPresentationSyncValidationCode.AnimationSetVersionMismatch,
                    "animationSetVersion",
                    expectation.AnimationSetVersion.ToString(),
                    state.AnimationSetVersion.ToString());
            }

            if (!string.IsNullOrWhiteSpace(expectation.AnimationSetHash)
                && !string.Equals(state.AnimationSetHash, expectation.AnimationSetHash, StringComparison.Ordinal))
            {
                return Mismatch(
                    MxAnimationPresentationSyncValidationCode.AnimationSetHashMismatch,
                    "animationSetHash",
                    expectation.AnimationSetHash,
                    state.AnimationSetHash);
            }

            if (!string.IsNullOrWhiteSpace(expectation.ResourceCatalogHash)
                && !string.Equals(state.ResourceCatalogHash, expectation.ResourceCatalogHash, StringComparison.Ordinal))
            {
                return Mismatch(
                    MxAnimationPresentationSyncValidationCode.ResourceCatalogHashMismatch,
                    "resourceCatalogHash",
                    expectation.ResourceCatalogHash,
                    state.ResourceCatalogHash);
            }

            if (expectation.ClipRegistryVersion >= 0
                && state.ClipRegistryVersion != expectation.ClipRegistryVersion)
            {
                return Mismatch(
                    MxAnimationPresentationSyncValidationCode.ClipRegistryVersionMismatch,
                    "clipRegistryVersion",
                    expectation.ClipRegistryVersion.ToString(),
                    state.ClipRegistryVersion.ToString());
            }

            return MxAnimationPresentationSyncValidationResult.Succeeded();
        }

        private static MxAnimationPresentationSyncValidationResult Mismatch(
            MxAnimationPresentationSyncValidationCode code,
            string field,
            string expected,
            string actual)
        {
            return MxAnimationPresentationSyncValidationResult.Failed(
                code,
                field,
                expected,
                actual,
                "Presentation sync version mismatch on " + field + ".");
        }
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
            string tag = "",
            MxAnimationPresentationEventReplayPolicy replayPolicy = MxAnimationPresentationEventReplayPolicy.OneShot)
        {
            EventId = eventId ?? string.Empty;
            TimeDomain = timeDomain;
            Time = time;
            EventKind = eventKind ?? string.Empty;
            PayloadKey = payloadKey;
            Socket = socket ?? string.Empty;
            Tag = tag ?? string.Empty;
            ReplayPolicy = replayPolicy;
        }

        public string EventId { get; }
        public MxAnimationEventTimeDomain TimeDomain { get; }
        public float Time { get; }
        public string EventKind { get; }
        public ResourceKey PayloadKey { get; }
        public string Socket { get; }
        public string Tag { get; }
        public MxAnimationPresentationEventReplayPolicy ReplayPolicy { get; }
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
            IEnumerable<MxAnimationPresentationEvent> presentationEvents = null,
            float fadeDurationSeconds = 0.15f)
        {
            BindingId = bindingId ?? string.Empty;
            ActionKey = actionKey ?? string.Empty;
            Clip = clip;
            Layer = layer;
            PlaybackSpeed = playbackSpeed;
            Loop = loop;
            AlignmentPolicy = alignmentPolicy;
            FadeDurationSeconds = fadeDurationSeconds < 0f ? 0f : fadeDurationSeconds;
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
        public float FadeDurationSeconds { get; }
        public IReadOnlyList<MxAnimationPresentationEvent> PresentationEvents => _presentationEvents;
    }

    public sealed class MxAnimationLayerDefinition
    {
        public MxAnimationLayerDefinition(
            MxAnimationLayerId layerId,
            string profileId = "",
            float defaultWeight = 1f,
            MxAnimationLayerBlendMode blendMode = MxAnimationLayerBlendMode.Override,
            ResourceKey avatarMaskKey = default)
        {
            LayerId = layerId;
            ProfileId = profileId ?? string.Empty;
            DefaultWeight = Clamp01(defaultWeight);
            BlendMode = blendMode;
            AvatarMaskKey = avatarMaskKey;
        }

        public MxAnimationLayerId LayerId { get; }
        public string ProfileId { get; }
        public float DefaultWeight { get; }
        public MxAnimationLayerBlendMode BlendMode { get; }
        public ResourceKey AvatarMaskKey { get; }
        public bool HasAvatarMask => AvatarMaskKey.IsValid;

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }

    public sealed class MxAnimationSetDefinition
    {
        private readonly List<MxAnimationActionBinding> _actions;
        private readonly List<MxAnimationPresentationEvent> _events;
        private readonly List<MxAnimationLayerDefinition> _layers;
        private readonly List<MxAnimationBlend1DDefinition> _blend1DDefinitions;

        public MxAnimationSetDefinition(
            string setId,
            int version,
            ResourceKey defaultClip,
            ResourceKey fallbackClip,
            IEnumerable<MxAnimationActionBinding> actions = null,
            IEnumerable<MxAnimationPresentationEvent> events = null,
            string definitionHash = "",
            IEnumerable<MxAnimationLayerDefinition> layers = null,
            MxAnimationWarmupDefinition warmup = null,
            IEnumerable<MxAnimationBlend1DDefinition> blend1DDefinitions = null)
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
            _layers = layers != null
                ? new List<MxAnimationLayerDefinition>(layers)
                : new List<MxAnimationLayerDefinition>();
            _blend1DDefinitions = blend1DDefinitions != null
                ? new List<MxAnimationBlend1DDefinition>(blend1DDefinitions)
                : new List<MxAnimationBlend1DDefinition>();
            Warmup = warmup ?? MxAnimationWarmupDefinition.Default;
            DefinitionHash = string.IsNullOrWhiteSpace(definitionHash)
                ? MxAnimationSetDefinitionHasher.ComputeHash(this)
                : definitionHash;
        }

        public string SetId { get; }
        public int Version { get; }
        public string DefinitionHash { get; }
        public ResourceKey DefaultClip { get; }
        public ResourceKey FallbackClip { get; }
        public IReadOnlyList<MxAnimationActionBinding> Actions => _actions;
        public IReadOnlyList<MxAnimationPresentationEvent> Events => _events;
        public IReadOnlyList<MxAnimationLayerDefinition> Layers => _layers;
        public IReadOnlyList<MxAnimationBlend1DDefinition> Blend1DDefinitions => _blend1DDefinitions;
        public MxAnimationWarmupDefinition Warmup { get; }

        public bool TryFindLayerDefinition(MxAnimationLayerId layerId, out MxAnimationLayerDefinition layer)
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                MxAnimationLayerDefinition candidate = _layers[i];
                if (candidate == null || candidate.LayerId != layerId)
                    continue;

                layer = candidate;
                return true;
            }

            layer = null;
            return false;
        }

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

        public bool TryFindBlend1DDefinition(string blendId, string parameterId, out MxAnimationBlend1DDefinition definition)
        {
            for (int i = 0; i < _blend1DDefinitions.Count; i++)
            {
                MxAnimationBlend1DDefinition candidate = _blend1DDefinitions[i];
                bool blendMatches = !string.IsNullOrWhiteSpace(blendId)
                    && string.Equals(candidate.BlendId, blendId, StringComparison.Ordinal);
                bool parameterMatches = !string.IsNullOrWhiteSpace(parameterId)
                    && string.Equals(candidate.ParameterId, parameterId, StringComparison.Ordinal);
                if (!blendMatches && !parameterMatches)
                    continue;

                definition = candidate;
                return true;
            }

            definition = null;
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

    public sealed class MxAnimationLayerWeightRequest
    {
        public string TargetActorId { get; set; } = string.Empty;
        public MxAnimationLayerId LayerId { get; set; } = MxAnimationLayerId.Base;
        public float Weight { get; set; } = 1f;
        public float FadeDurationSeconds { get; set; }
        public string TransitionPolicyId { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
    }

    public sealed class MxAnimationBlend1DRequest
    {
        public string TargetActorId { get; set; } = string.Empty;
        public string BlendId { get; set; } = string.Empty;
        public MxAnimationQuantizedParameter Parameter { get; set; }
        public float FadeDurationSeconds { get; set; } = -1f;
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
            ResourceError lastError,
            float layerWeight = 1f,
            float targetLayerWeight = 1f,
            MxAnimationLayerMaskStatus maskStatus = MxAnimationLayerMaskStatus.NotConfigured,
            ResourceKey maskKey = default,
            string layerProfileId = "",
            MxAnimationLayerBlendMode blendMode = MxAnimationLayerBlendMode.Override,
            MxAnimationLayerSyncState layerSyncState = default,
            string blend1DId = "",
            MxAnimationQuantizedParameter blendParameter = default,
            IEnumerable<MxAnimationBlend1DWeight> blend1DWeights = null)
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
            LayerWeight = Clamp01(layerWeight);
            TargetLayerWeight = Clamp01(targetLayerWeight);
            MaskStatus = maskStatus;
            MaskKey = maskKey;
            LayerProfileId = layerProfileId ?? string.Empty;
            BlendMode = blendMode;
            LayerSyncState = layerSyncState;
            Blend1DId = blend1DId ?? string.Empty;
            BlendParameter = blendParameter;
            _blend1DWeights = blend1DWeights != null
                ? new List<MxAnimationBlend1DWeight>(blend1DWeights)
                : new List<MxAnimationBlend1DWeight>();
        }

        private readonly List<MxAnimationBlend1DWeight> _blend1DWeights;

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
        public float LayerWeight { get; }
        public float TargetLayerWeight { get; }
        public MxAnimationLayerMaskStatus MaskStatus { get; }
        public ResourceKey MaskKey { get; }
        public string LayerProfileId { get; }
        public MxAnimationLayerBlendMode BlendMode { get; }
        public MxAnimationLayerSyncState LayerSyncState { get; }
        public string Blend1DId { get; }
        public MxAnimationQuantizedParameter BlendParameter { get; }
        public IReadOnlyList<MxAnimationBlend1DWeight> Blend1DWeights => _blend1DWeights;

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
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
        MxAnimationBackendResult SetLayerWeight(MxAnimationLayerWeightRequest request);
        MxAnimationBackendResult SetBlend1D(MxAnimationBlend1DRequest request);
        void Tick(float deltaTime);
        MxAnimationDiagnosticSnapshot CreateSnapshot();
        void Release();
    }
}
