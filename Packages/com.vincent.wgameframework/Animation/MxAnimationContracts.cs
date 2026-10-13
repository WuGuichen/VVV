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
        SetBlend2D,
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

    public enum MxAnimationBlendKind
    {
        None,
        Blend1D,
        Blend2D
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

    public sealed class MxAnimationBlend2DPoint
    {
        public MxAnimationBlend2DPoint(
            int x,
            int y,
            ResourceKey clipKey,
            float playbackSpeed = 1f,
            bool loop = true)
        {
            X = x;
            Y = y;
            ClipKey = clipKey;
            PlaybackSpeed = Math.Abs(playbackSpeed) < 0.0001f ? 1f : playbackSpeed;
            Loop = loop;
        }

        public int X { get; }
        public int Y { get; }
        public ResourceKey ClipKey { get; }
        public float PlaybackSpeed { get; }
        public bool Loop { get; }
    }

    public sealed class MxAnimationBlend2DDefinition
    {
        private readonly List<MxAnimationBlend2DPoint> _points;

        public MxAnimationBlend2DDefinition(
            string blendId,
            string parameterXId,
            string parameterYId,
            MxAnimationLayerId layerId,
            IEnumerable<MxAnimationBlend2DPoint> points,
            int parameterXScale = 1000,
            int parameterYScale = 1000,
            float fadeDurationSeconds = 0.1f)
        {
            BlendId = blendId ?? string.Empty;
            ParameterXId = parameterXId ?? string.Empty;
            ParameterYId = parameterYId ?? string.Empty;
            LayerId = layerId;
            ParameterXScale = parameterXScale <= 0 ? 1 : parameterXScale;
            ParameterYScale = parameterYScale <= 0 ? 1 : parameterYScale;
            FadeDurationSeconds = fadeDurationSeconds < 0f ? 0f : fadeDurationSeconds;
            _points = points != null
                ? new List<MxAnimationBlend2DPoint>(points)
                : new List<MxAnimationBlend2DPoint>();
            _points.Sort(ComparePoints);
        }

        public string BlendId { get; }
        public string ParameterXId { get; }
        public string ParameterYId { get; }
        public MxAnimationLayerId LayerId { get; }
        public int ParameterXScale { get; }
        public int ParameterYScale { get; }
        public float FadeDurationSeconds { get; }
        public IReadOnlyList<MxAnimationBlend2DPoint> Points => _points;

        private static int ComparePoints(MxAnimationBlend2DPoint left, MxAnimationBlend2DPoint right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int result = left.X.CompareTo(right.X);
            if (result != 0)
                return result;

            result = left.Y.CompareTo(right.Y);
            if (result != 0)
                return result;

            return string.CompareOrdinal(left.ClipKey.ToString(), right.ClipKey.ToString());
        }
    }

    public readonly struct MxAnimationBlend2DWeight
    {
        public MxAnimationBlend2DWeight(
            ResourceKey clipKey,
            int x,
            int y,
            float weight,
            float playbackSpeed,
            bool loop)
        {
            ClipKey = clipKey;
            X = x;
            Y = y;
            Weight = Clamp01(weight);
            PlaybackSpeed = Math.Abs(playbackSpeed) < 0.0001f ? 1f : playbackSpeed;
            Loop = loop;
        }

        public ResourceKey ClipKey { get; }
        public int X { get; }
        public int Y { get; }
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

    public sealed class MxAnimationBlend2DWeights
    {
        private readonly List<MxAnimationBlend2DWeight> _weights;

        public MxAnimationBlend2DWeights(
            string blendId,
            MxAnimationQuantizedParameter parameterX,
            MxAnimationQuantizedParameter parameterY,
            IEnumerable<MxAnimationBlend2DWeight> weights)
        {
            BlendId = blendId ?? string.Empty;
            ParameterX = parameterX;
            ParameterY = parameterY;
            _weights = weights != null
                ? new List<MxAnimationBlend2DWeight>(weights)
                : new List<MxAnimationBlend2DWeight>();
        }

        public string BlendId { get; }
        public MxAnimationQuantizedParameter ParameterX { get; }
        public MxAnimationQuantizedParameter ParameterY { get; }
        public IReadOnlyList<MxAnimationBlend2DWeight> Weights => _weights;
    }

    public sealed class MxAnimationBlend2DEvaluationBuffer
    {
        private readonly List<MxAnimationBlend2DWeight> _weights = new List<MxAnimationBlend2DWeight>();
        private readonly List<int> _projectionIndices = new List<int>();
        private readonly List<int> _uniqueX = new List<int>();
        private readonly List<int> _uniqueY = new List<int>();

        public string BlendId { get; private set; } = string.Empty;
        public MxAnimationQuantizedParameter ParameterX { get; private set; }
        public MxAnimationQuantizedParameter ParameterY { get; private set; }
        public IReadOnlyList<MxAnimationBlend2DWeight> Weights => _weights;

        internal List<MxAnimationBlend2DWeight> MutableWeights => _weights;
        internal List<int> UniqueX => _uniqueX;
        internal List<int> UniqueY => _uniqueY;

        internal void Reset(
            string blendId,
            MxAnimationQuantizedParameter parameterX,
            MxAnimationQuantizedParameter parameterY)
        {
            BlendId = blendId ?? string.Empty;
            ParameterX = parameterX;
            ParameterY = parameterY;
            _weights.Clear();
            _projectionIndices.Clear();
            _uniqueX.Clear();
            _uniqueY.Clear();
        }

        internal List<int> BuildProjectionIndices(IReadOnlyList<MxAnimationBlend2DPoint> points)
        {
            _projectionIndices.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] != null)
                    _projectionIndices.Add(i);
            }

            return _projectionIndices;
        }
    }

    public static class MxAnimationBlend2DCalculator
    {
        private static readonly Comparison<int> IntComparison = CompareInts;
        private static int CompareInts(int left, int right) => left.CompareTo(right);

        private const double Epsilon = 0.000001d;

        public static MxAnimationBlend2DWeights Evaluate(
            MxAnimationBlend2DDefinition definition,
            MxAnimationQuantizedParameter parameterX,
            MxAnimationQuantizedParameter parameterY)
        {
            var buffer = new MxAnimationBlend2DEvaluationBuffer();
            Evaluate(definition, parameterX, parameterY, buffer);
            return new MxAnimationBlend2DWeights(buffer.BlendId, buffer.ParameterX, buffer.ParameterY, buffer.Weights);
        }

        public static void Evaluate(
            MxAnimationBlend2DDefinition definition,
            MxAnimationQuantizedParameter parameterX,
            MxAnimationQuantizedParameter parameterY,
            MxAnimationBlend2DEvaluationBuffer buffer)
        {
            int x = definition != null && string.Equals(parameterX.ParameterId, definition.ParameterXId, StringComparison.Ordinal)
                ? parameterX.QuantizedValue : 0;
            int y = definition != null && string.Equals(parameterY.ParameterId, definition.ParameterYId, StringComparison.Ordinal)
                ? parameterY.QuantizedValue : 0;
            EvaluateCore(definition, parameterX, parameterY, buffer, x, y);
        }

        /// <summary>NoAlloc after buffer warmup. Axes must already be bound to this definition during preparation.</summary>
        public static void EvaluatePrepared(MxAnimationBlend2DDefinition definition, int x, int y, MxAnimationBlend2DEvaluationBuffer buffer)
        {
            var parameterX = new MxAnimationQuantizedParameter(definition?.ParameterXId, x, definition?.ParameterXScale ?? 1000);
            var parameterY = new MxAnimationQuantizedParameter(definition?.ParameterYId, y, definition?.ParameterYScale ?? 1000);
            EvaluateCore(definition, parameterX, parameterY, buffer, x, y);
        }

        private static void EvaluateCore(
            MxAnimationBlend2DDefinition definition,
            MxAnimationQuantizedParameter parameterX,
            MxAnimationQuantizedParameter parameterY,
            MxAnimationBlend2DEvaluationBuffer buffer, int x, int y)
        {
            if (definition == null || definition.Points.Count == 0)
            {
                if (buffer == null)
                    throw new ArgumentNullException(nameof(buffer));

                buffer.Reset(string.Empty, parameterX, parameterY);
                return;
            }

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            buffer.Reset(definition.BlendId, parameterX, parameterY);
            List<MxAnimationBlend2DWeight> weights = buffer.MutableWeights;
            for (int i = 0; i < definition.Points.Count; i++)
                weights.Add(CreateWeight(definition.Points[i], 0f));

            int exactIndex = FindExactPoint(definition.Points, x, y);
            if (exactIndex >= 0)
            {
                SetSingle(weights, definition.Points, exactIndex);
                return;
            }

            if (definition.Points.Count == 1)
            {
                SetSingle(weights, definition.Points, 0);
                return;
            }

            if (definition.Points.Count == 2)
            {
                ApplySegment(definition.Points, 0, 1, x, y, weights);
                return;
            }

            if (AreCollinear(definition.Points))
            {
                ApplyCollinear(definition.Points, x, y, weights, buffer.BuildProjectionIndices(definition.Points));
                return;
            }

            if (TryApplyPolarRings(definition.Points, x, y, weights, buffer.UniqueX, buffer.UniqueY)
                || TryApplyRectangle(definition.Points, x, y, weights, buffer.UniqueX, buffer.UniqueY)
                || TryApplyTriangle(definition.Points, x, y, weights))
            {
                return;
            }

            ApplyNearestSegment(definition.Points, x, y, weights);
        }

        private static bool TryApplyPolarRings(
            IReadOnlyList<MxAnimationBlend2DPoint> points,
            int x,
            int y,
            List<MxAnimationBlend2DWeight> weights,
            List<int> directionAngles,
            List<int> directionCounts)
        {
            directionAngles.Clear();
            directionCounts.Clear();
            int centerIndex = -1;
            for (int i = 0; i < points.Count; i++)
            {
                MxAnimationBlend2DPoint point = points[i];
                if (point == null)
                    continue;
                if (point.X == 0 && point.Y == 0)
                {
                    if (centerIndex >= 0)
                        return false;
                    centerIndex = i;
                    continue;
                }

                int angle = DirectionAngleMilliDegrees(point.X, point.Y);
                int directionIndex = directionAngles.IndexOf(angle);
                if (directionIndex < 0)
                {
                    directionAngles.Add(angle);
                    directionCounts.Add(1);
                }
                else
                {
                    directionCounts[directionIndex]++;
                }
            }

            if (centerIndex < 0 || directionAngles.Count < 3)
                return false;
            for (int i = 0; i < directionCounts.Count; i++)
            {
                if (directionCounts[i] < 2)
                    return false;
            }

            directionAngles.Sort(IntComparison);
            ClearWeights(weights, points);
            int inputAngle = DirectionAngleMilliDegrees(x, y);
            int exactDirection = directionAngles.IndexOf(inputAngle);
            double inputRadius = Math.Sqrt(((double)x * x) + ((double)y * y));
            if (exactDirection >= 0)
            {
                ApplyPolarSpoke(
                    points,
                    weights,
                    centerIndex,
                    inputAngle,
                    inputRadius,
                    1f);
                return true;
            }

            int upperIndex = 0;
            while (upperIndex < directionAngles.Count && directionAngles[upperIndex] < inputAngle)
                upperIndex++;
            int lowerIndex = upperIndex <= 0 ? directionAngles.Count - 1 : upperIndex - 1;
            if (upperIndex >= directionAngles.Count)
                upperIndex = 0;

            int lowerAngle = directionAngles[lowerIndex];
            int upperAngle = directionAngles[upperIndex];
            int totalArc = ClockwiseAngleDistance(lowerAngle, upperAngle);
            int inputArc = ClockwiseAngleDistance(lowerAngle, inputAngle);
            float upperWeight = totalArc <= 0
                ? 0f
                : (float)Clamp01((double)inputArc / totalArc);
            float lowerWeight = 1f - upperWeight;
            ApplyPolarSpoke(
                points,
                weights,
                centerIndex,
                lowerAngle,
                inputRadius,
                lowerWeight);
            ApplyPolarSpoke(
                points,
                weights,
                centerIndex,
                upperAngle,
                inputRadius,
                upperWeight);
            return true;
        }

        private static void ApplyPolarSpoke(
            IReadOnlyList<MxAnimationBlend2DPoint> points,
            List<MxAnimationBlend2DWeight> weights,
            int centerIndex,
            int directionAngle,
            double inputRadius,
            float angularWeight)
        {
            if (angularWeight <= 0f)
                return;

            int lowerIndex = centerIndex;
            double lowerRadius = 0d;
            int upperIndex = -1;
            double upperRadius = double.PositiveInfinity;
            for (int i = 0; i < points.Count; i++)
            {
                MxAnimationBlend2DPoint point = points[i];
                if (point == null
                    || (point.X == 0 && point.Y == 0)
                    || DirectionAngleMilliDegrees(point.X, point.Y) != directionAngle)
                {
                    continue;
                }

                double radius = Math.Sqrt(((double)point.X * point.X) + ((double)point.Y * point.Y));
                if (radius <= inputRadius && radius >= lowerRadius)
                {
                    lowerIndex = i;
                    lowerRadius = radius;
                }
                if (radius >= inputRadius && radius < upperRadius)
                {
                    upperIndex = i;
                    upperRadius = radius;
                }
            }

            if (upperIndex < 0)
            {
                AddPolarWeight(points, weights, lowerIndex, angularWeight);
                return;
            }
            if (upperIndex == lowerIndex || upperRadius - lowerRadius <= Epsilon)
            {
                AddPolarWeight(points, weights, upperIndex, angularWeight);
                return;
            }

            float upperRadialWeight = (float)Clamp01((inputRadius - lowerRadius) / (upperRadius - lowerRadius));
            AddPolarWeight(points, weights, lowerIndex, angularWeight * (1f - upperRadialWeight));
            AddPolarWeight(points, weights, upperIndex, angularWeight * upperRadialWeight);
        }

        private static void AddPolarWeight(
            IReadOnlyList<MxAnimationBlend2DPoint> points,
            List<MxAnimationBlend2DWeight> weights,
            int index,
            float delta)
        {
            if (index < 0 || index >= points.Count || delta <= 0f)
                return;
            MxAnimationBlend2DPoint point = points[index];
            float value = (float)Clamp01(weights[index].Weight + delta);
            weights[index] = CreateWeight(point, value);
        }

        private static int DirectionAngleMilliDegrees(int x, int y)
        {
            double angle = Math.Atan2(x, y) * (180000d / Math.PI);
            int result = (int)Math.Round(angle, MidpointRounding.AwayFromZero);
            result %= 360000;
            return result < 0 ? result + 360000 : result;
        }

        private static int ClockwiseAngleDistance(int from, int to)
        {
            int distance = (to - from) % 360000;
            return distance < 0 ? distance + 360000 : distance;
        }

        private static MxAnimationBlend2DWeight CreateWeight(MxAnimationBlend2DPoint point, float weight)
        {
            return point == null
                ? default
                : new MxAnimationBlend2DWeight(point.ClipKey, point.X, point.Y, weight, point.PlaybackSpeed, point.Loop);
        }

        private static void SetSingle(
            List<MxAnimationBlend2DWeight> weights,
            IReadOnlyList<MxAnimationBlend2DPoint> points,
            int index)
        {
            ClearWeights(weights, points);
            weights[index] = CreateWeight(points[index], 1f);
        }

        private static void ClearWeights(
            List<MxAnimationBlend2DWeight> weights,
            IReadOnlyList<MxAnimationBlend2DPoint> points)
        {
            for (int i = 0; i < weights.Count; i++)
                weights[i] = CreateWeight(points[i], 0f);
        }

        private static int FindExactPoint(IReadOnlyList<MxAnimationBlend2DPoint> points, int x, int y)
        {
            for (int i = 0; i < points.Count; i++)
            {
                MxAnimationBlend2DPoint point = points[i];
                if (point != null && point.X == x && point.Y == y)
                    return i;
            }

            return -1;
        }

        private static bool AreCollinear(IReadOnlyList<MxAnimationBlend2DPoint> points)
        {
            int first = FindFirstValidPoint(points);
            int second = FindNextDistinctPoint(points, first);
            if (first < 0 || second < 0)
                return true;

            MxAnimationBlend2DPoint a = points[first];
            MxAnimationBlend2DPoint b = points[second];
            for (int i = 0; i < points.Count; i++)
            {
                MxAnimationBlend2DPoint c = points[i];
                if (c == null)
                    continue;

                long area = Cross(a, b, c);
                if (area != 0)
                    return false;
            }

            return true;
        }

        private static void ApplyCollinear(
            IReadOnlyList<MxAnimationBlend2DPoint> points,
            int x,
            int y,
            List<MxAnimationBlend2DWeight> weights,
            IReadOnlyList<int> indices)
        {
            if (indices.Count == 0)
                return;

            if (indices.Count == 1)
            {
                SetSingle(weights, points, indices[0]);
                return;
            }

            bool useX = Range(points, indices, true) >= Range(points, indices, false);
            double value = useX ? x : y;
            double first = Coordinate(points[indices[0]], useX);
            double last = Coordinate(points[indices[indices.Count - 1]], useX);
            if (value <= first)
            {
                SetSingle(weights, points, indices[0]);
                return;
            }

            if (value >= last)
            {
                SetSingle(weights, points, indices[indices.Count - 1]);
                return;
            }

            for (int i = 0; i < indices.Count - 1; i++)
            {
                int left = indices[i];
                int right = indices[i + 1];
                double leftValue = Coordinate(points[left], useX);
                double rightValue = Coordinate(points[right], useX);
                if (Math.Abs(rightValue - leftValue) <= Epsilon)
                    continue;
                if (value < leftValue || value > rightValue)
                    continue;

                double t = (value - leftValue) / (rightValue - leftValue);
                ApplyPair(points, left, right, 1d - t, t, weights);
                return;
            }

            ApplyNearestSegment(points, x, y, weights);
        }

        private static bool TryApplyRectangle(
            IReadOnlyList<MxAnimationBlend2DPoint> points,
            int x,
            int y,
            List<MxAnimationBlend2DWeight> weights,
            List<int> xs,
            List<int> ys)
        {
            UniqueCoordinates(points, true, xs);
            UniqueCoordinates(points, false, ys);
            if (xs.Count < 2 || ys.Count < 2)
                return false;

            FindBracket(xs, x, out int x0, out int x1);
            FindBracket(ys, y, out int y0, out int y1);
            if (x0 == x1 || y0 == y1)
                return false;

            int i00 = FindPoint(points, x0, y0);
            int i10 = FindPoint(points, x1, y0);
            int i01 = FindPoint(points, x0, y1);
            int i11 = FindPoint(points, x1, y1);
            if (i00 < 0 || i10 < 0 || i01 < 0 || i11 < 0)
                return false;

            double clampedX = Clamp(x, x0, x1);
            double clampedY = Clamp(y, y0, y1);
            double tx = (clampedX - x0) / (x1 - x0);
            double ty = (clampedY - y0) / (y1 - y0);
            ClearWeights(weights, points);
            weights[i00] = CreateWeight(points[i00], (float)((1d - tx) * (1d - ty)));
            weights[i10] = CreateWeight(points[i10], (float)(tx * (1d - ty)));
            weights[i01] = CreateWeight(points[i01], (float)((1d - tx) * ty));
            weights[i11] = CreateWeight(points[i11], (float)(tx * ty));
            return true;
        }

        private static bool TryApplyTriangle(
            IReadOnlyList<MxAnimationBlend2DPoint> points,
            int x,
            int y,
            List<MxAnimationBlend2DWeight> weights)
        {
            for (int a = 0; a < points.Count - 2; a++)
            {
                if (points[a] == null)
                    continue;

                for (int b = a + 1; b < points.Count - 1; b++)
                {
                    if (points[b] == null)
                        continue;

                    for (int c = b + 1; c < points.Count; c++)
                    {
                        if (points[c] == null)
                            continue;

                        if (!TryBarycentric(points[a], points[b], points[c], x, y, out double wa, out double wb, out double wc))
                            continue;

                        if (wa < -Epsilon || wb < -Epsilon || wc < -Epsilon)
                            continue;

                        wa = Clamp01(wa);
                        wb = Clamp01(wb);
                        wc = Clamp01(wc);
                        Normalize(ref wa, ref wb, ref wc);
                        ClearWeights(weights, points);
                        weights[a] = CreateWeight(points[a], (float)wa);
                        weights[b] = CreateWeight(points[b], (float)wb);
                        weights[c] = CreateWeight(points[c], (float)wc);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryBarycentric(
            MxAnimationBlend2DPoint a,
            MxAnimationBlend2DPoint b,
            MxAnimationBlend2DPoint c,
            int x,
            int y,
            out double wa,
            out double wb,
            out double wc)
        {
            double denominator = ((b.Y - c.Y) * (a.X - c.X)) + ((c.X - b.X) * (a.Y - c.Y));
            if (Math.Abs(denominator) <= Epsilon)
            {
                wa = wb = wc = 0d;
                return false;
            }

            wa = (((b.Y - c.Y) * (x - c.X)) + ((c.X - b.X) * (y - c.Y))) / denominator;
            wb = (((c.Y - a.Y) * (x - c.X)) + ((a.X - c.X) * (y - c.Y))) / denominator;
            wc = 1d - wa - wb;
            return true;
        }

        private static void ApplyNearestSegment(
            IReadOnlyList<MxAnimationBlend2DPoint> points,
            int x,
            int y,
            List<MxAnimationBlend2DWeight> weights)
        {
            double bestDistance = double.MaxValue;
            int bestA = -1;
            int bestB = -1;
            double bestT = 0d;
            for (int a = 0; a < points.Count - 1; a++)
            {
                if (points[a] == null)
                    continue;

                for (int b = a + 1; b < points.Count; b++)
                {
                    if (points[b] == null)
                        continue;

                    double vx = points[b].X - points[a].X;
                    double vy = points[b].Y - points[a].Y;
                    double lengthSquared = (vx * vx) + (vy * vy);
                    if (lengthSquared <= Epsilon)
                        continue;

                    double t = (((x - points[a].X) * vx) + ((y - points[a].Y) * vy)) / lengthSquared;
                    t = Clamp01(t);
                    double projectedX = points[a].X + (vx * t);
                    double projectedY = points[a].Y + (vy * t);
                    double distance = DistanceSquared(projectedX, projectedY, x, y);
                    if (distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    bestA = a;
                    bestB = b;
                    bestT = t;
                }
            }

            if (bestA >= 0 && bestB >= 0)
            {
                ApplyPair(points, bestA, bestB, 1d - bestT, bestT, weights);
                return;
            }

            SetSingle(weights, points, FindNearestPoint(points, x, y));
        }

        private static void ApplySegment(
            IReadOnlyList<MxAnimationBlend2DPoint> points,
            int a,
            int b,
            int x,
            int y,
            List<MxAnimationBlend2DWeight> weights)
        {
            double vx = points[b].X - points[a].X;
            double vy = points[b].Y - points[a].Y;
            double lengthSquared = (vx * vx) + (vy * vy);
            if (lengthSquared <= Epsilon)
            {
                SetSingle(weights, points, a);
                return;
            }

            double t = (((x - points[a].X) * vx) + ((y - points[a].Y) * vy)) / lengthSquared;
            t = Clamp01(t);
            ApplyPair(points, a, b, 1d - t, t, weights);
        }

        private static void ApplyPair(
            IReadOnlyList<MxAnimationBlend2DPoint> points,
            int a,
            int b,
            double wa,
            double wb,
            List<MxAnimationBlend2DWeight> weights)
        {
            ClearWeights(weights, points);
            weights[a] = CreateWeight(points[a], (float)wa);
            weights[b] = CreateWeight(points[b], (float)wb);
        }

        private static int FindFirstValidPoint(IReadOnlyList<MxAnimationBlend2DPoint> points)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] != null)
                    return i;
            }

            return -1;
        }

        private static int FindNextDistinctPoint(IReadOnlyList<MxAnimationBlend2DPoint> points, int first)
        {
            if (first < 0)
                return -1;

            for (int i = first + 1; i < points.Count; i++)
            {
                if (points[i] == null)
                    continue;
                if (points[i].X != points[first].X || points[i].Y != points[first].Y)
                    return i;
            }

            return -1;
        }

        private static int FindNearestPoint(IReadOnlyList<MxAnimationBlend2DPoint> points, int x, int y)
        {
            int best = 0;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                MxAnimationBlend2DPoint point = points[i];
                if (point == null)
                    continue;

                double distance = DistanceSquared(point.X, point.Y, x, y);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = i;
            }

            return best;
        }

        private static void UniqueCoordinates(IReadOnlyList<MxAnimationBlend2DPoint> points, bool useX, List<int> values)
        {
            values.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                MxAnimationBlend2DPoint point = points[i];
                if (point == null)
                    continue;

                int value = useX ? point.X : point.Y;
                if (!values.Contains(value))
                    values.Add(value);
            }

            values.Sort(IntComparison);
        }

        private static void FindBracket(IReadOnlyList<int> values, int sample, out int lower, out int upper)
        {
            if (sample <= values[0])
            {
                lower = values[0];
                upper = values[1];
                return;
            }

            int last = values.Count - 1;
            if (sample >= values[last])
            {
                lower = values[last - 1];
                upper = values[last];
                return;
            }

            for (int i = 0; i < values.Count - 1; i++)
            {
                if (sample < values[i] || sample > values[i + 1])
                    continue;

                lower = values[i];
                upper = values[i + 1];
                return;
            }

            lower = values[0];
            upper = values[1];
        }

        private static int FindPoint(IReadOnlyList<MxAnimationBlend2DPoint> points, int x, int y)
        {
            for (int i = 0; i < points.Count; i++)
            {
                MxAnimationBlend2DPoint point = points[i];
                if (point != null && point.X == x && point.Y == y)
                    return i;
            }

            return -1;
        }

        private static double Coordinate(MxAnimationBlend2DPoint point, bool useX)
        {
            return useX ? point.X : point.Y;
        }

        private static double Range(IReadOnlyList<MxAnimationBlend2DPoint> points, IReadOnlyList<int> indices, bool useX)
        {
            return Coordinate(points[indices[indices.Count - 1]], useX) - Coordinate(points[indices[0]], useX);
        }

        private static long Cross(MxAnimationBlend2DPoint a, MxAnimationBlend2DPoint b, MxAnimationBlend2DPoint c)
        {
            return ((long)b.X - a.X) * (c.Y - a.Y) - ((long)b.Y - a.Y) * (c.X - a.X);
        }

        private static double DistanceSquared(double ax, double ay, double bx, double by)
        {
            double dx = ax - bx;
            double dy = ay - by;
            return (dx * dx) + (dy * dy);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value <= min)
                return min;
            return value >= max ? max : value;
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value) || value <= 0d)
                return 0d;
            return value >= 1d ? 1d : value;
        }

        private static void Normalize(ref double a, ref double b, ref double c)
        {
            double sum = a + b + c;
            if (sum <= Epsilon)
                return;

            a /= sum;
            b /= sum;
            c /= sum;
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
            int bindingId,
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
            BindingId = bindingId;
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
        public int BindingId { get; }
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
            int bindingId,
            string actionKey,
            ResourceKey clip,
            MxAnimationLayerId layer,
            float playbackSpeed = 1f,
            bool loop = false,
            MxAnimationAlignmentPolicy alignmentPolicy = MxAnimationAlignmentPolicy.StartAtZero,
            IEnumerable<MxAnimationPresentationEvent> presentationEvents = null,
            float fadeDurationSeconds = 0.15f,
            string bindingLabel = "",
            int actionId = 0)
        {
            if (bindingId < 0) throw new ArgumentOutOfRangeException(nameof(bindingId));
            BindingId = bindingId;
            BindingLabel = bindingLabel ?? string.Empty;
            ActionId = actionId;
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

        public int BindingId { get; }
        public string BindingLabel { get; }
        public int ActionId { get; }
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
        private readonly System.Collections.ObjectModel.ReadOnlyCollection<MxAnimationActionBinding> _preparedClipBindings;
        private readonly List<MxAnimationPresentationEvent> _events;
        private readonly List<MxAnimationLayerDefinition> _layers;
        private readonly List<MxAnimationBlend1DDefinition> _blend1DDefinitions;
        private readonly List<MxAnimationBlend2DDefinition> _blend2DDefinitions;
        private readonly List<MxAnimationLocomotionClipCalibration> _locomotionClipCalibrations;

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
            IEnumerable<MxAnimationBlend1DDefinition> blend1DDefinitions = null,
            IEnumerable<MxAnimationBlend2DDefinition> blend2DDefinitions = null,
            MxAnimationCompatibilityExpectation compatibilityExpectation = null,
            IEnumerable<MxAnimationLocomotionClipCalibration> locomotionClipCalibrations = null,
            IEnumerable<MxAnimationActionBinding> preparedClipBindings = null)
        {
            SetId = setId ?? string.Empty;
            Version = version;
            DefaultClip = defaultClip;
            FallbackClip = fallbackClip;
            _actions = actions != null
                ? new List<MxAnimationActionBinding>(actions)
                : new List<MxAnimationActionBinding>();
            var directClips = preparedClipBindings != null ? new List<MxAnimationActionBinding>(preparedClipBindings) : new List<MxAnimationActionBinding>();
            var identities = new HashSet<int>();
            foreach (var action in _actions) if (action != null && action.BindingId > 0) identities.Add(action.BindingId);
            foreach (var direct in directClips)
                if (direct == null || direct.BindingId <= 0 || !direct.Clip.IsValid || !identities.Add(direct.BindingId))
                    throw new ArgumentException("Prepared direct clip bindings require unique positive IDs and valid resource keys.", nameof(preparedClipBindings));
            _preparedClipBindings = directClips.AsReadOnly();
            _events = events != null
                ? new List<MxAnimationPresentationEvent>(events)
                : new List<MxAnimationPresentationEvent>();
            _layers = layers != null
                ? new List<MxAnimationLayerDefinition>(layers)
                : new List<MxAnimationLayerDefinition>();
            _blend1DDefinitions = blend1DDefinitions != null
                ? new List<MxAnimationBlend1DDefinition>(blend1DDefinitions)
                : new List<MxAnimationBlend1DDefinition>();
            _blend2DDefinitions = blend2DDefinitions != null
                ? new List<MxAnimationBlend2DDefinition>(blend2DDefinitions)
                : new List<MxAnimationBlend2DDefinition>();
            _locomotionClipCalibrations = locomotionClipCalibrations != null
                ? new List<MxAnimationLocomotionClipCalibration>(locomotionClipCalibrations)
                : new List<MxAnimationLocomotionClipCalibration>();
            Warmup = warmup ?? MxAnimationWarmupDefinition.Default;
            CompatibilityExpectation = compatibilityExpectation ?? new MxAnimationCompatibilityExpectation();
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
        public IReadOnlyList<MxAnimationActionBinding> PreparedClipBindings => _preparedClipBindings;
        public IReadOnlyList<MxAnimationPresentationEvent> Events => _events;
        public IReadOnlyList<MxAnimationLayerDefinition> Layers => _layers;
        public IReadOnlyList<MxAnimationBlend1DDefinition> Blend1DDefinitions => _blend1DDefinitions;
        public IReadOnlyList<MxAnimationBlend2DDefinition> Blend2DDefinitions => _blend2DDefinitions;
        public IReadOnlyList<MxAnimationLocomotionClipCalibration> LocomotionClipCalibrations => _locomotionClipCalibrations;
        public MxAnimationWarmupDefinition Warmup { get; }
        public MxAnimationCompatibilityExpectation CompatibilityExpectation { get; }

        public bool TryFindLocomotionClipCalibration(ResourceKey clipKey, out MxAnimationLocomotionClipCalibration calibration)
        {
            for (int i = 0; i < _locomotionClipCalibrations.Count; i++)
            {
                MxAnimationLocomotionClipCalibration candidate = _locomotionClipCalibrations[i];
                if (candidate == null || candidate.ClipKey != clipKey)
                    continue;

                calibration = candidate;
                return true;
            }

            calibration = null;
            return false;
        }

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

        public bool TryFindBinding(int bindingId, out MxAnimationActionBinding binding)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                MxAnimationActionBinding candidate = _actions[i];
                if (bindingId > 0 && candidate.BindingId == bindingId)
                {
                    binding = candidate;
                    return true;
                }
            }
            return TryFindPreparedClipBinding(bindingId, out binding);
        }

        public bool TryFindPreparedClipBinding(int bindingId, out MxAnimationActionBinding binding)
        {
            for (int i = 0; i < _preparedClipBindings.Count; i++)
                if (bindingId > 0 && _preparedClipBindings[i].BindingId == bindingId) { binding = _preparedClipBindings[i]; return true; }
            binding = null;
            return false;
        }

        public bool TryFindActionBinding(int actionId, out MxAnimationActionBinding binding)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                var candidate = _actions[i];
                if (actionId > 0 && candidate.ActionId == actionId) { binding = candidate; return true; }
            }
            binding = null;
            return false;
        }

        /// <summary>Preparation only: resolves a source action label once; Tick callers retain BindingId.</summary>
        public bool TryResolveBindingForPreparation(string actionKey, out MxAnimationActionBinding binding)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                MxAnimationActionBinding candidate = _actions[i];
                if (!string.IsNullOrWhiteSpace(actionKey) && string.Equals(candidate.ActionKey, actionKey, StringComparison.Ordinal))
                {
                    binding = candidate;
                    return true;
                }
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

        public bool TryFindBlend2DDefinition(
            string blendId,
            string parameterXId,
            string parameterYId,
            out MxAnimationBlend2DDefinition definition)
        {
            for (int i = 0; i < _blend2DDefinitions.Count; i++)
            {
                MxAnimationBlend2DDefinition candidate = _blend2DDefinitions[i];
                if (candidate == null)
                    continue;

                bool blendMatches = !string.IsNullOrWhiteSpace(blendId)
                    && string.Equals(candidate.BlendId, blendId, StringComparison.Ordinal);
                bool parameterMatches = !string.IsNullOrWhiteSpace(parameterXId)
                    && !string.IsNullOrWhiteSpace(parameterYId)
                    && string.Equals(candidate.ParameterXId, parameterXId, StringComparison.Ordinal)
                    && string.Equals(candidate.ParameterYId, parameterYId, StringComparison.Ordinal);
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
        public int BindingId { get; set; }
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
        public int BindingId { get; set; }
        public MxAnimationLayerId LayerId { get; set; } = MxAnimationLayerId.Base;
        public float FadeOutDurationSeconds { get; set; }
        public string StopReason { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
    }

    public sealed class MxAnimationCrossFadeRequest
    {
        public bool AllowFallback { get; set; } = true;
        public string TargetActorId { get; set; } = string.Empty;
        public int BindingId { get; set; }
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

    public sealed class MxAnimationBlendRequest
    {
        private readonly List<MxAnimationQuantizedParameter> _parameters;

        public MxAnimationBlendRequest(
            MxAnimationBlendKind blendKind,
            string targetActorId,
            string blendId,
            IEnumerable<MxAnimationQuantizedParameter> parameters,
            float fadeDurationSeconds = -1f,
            string correlationId = "")
        {
            BlendKind = blendKind;
            TargetActorId = targetActorId ?? string.Empty;
            BlendId = blendId ?? string.Empty;
            FadeDurationSeconds = fadeDurationSeconds;
            CorrelationId = correlationId ?? string.Empty;
            _parameters = parameters != null
                ? new List<MxAnimationQuantizedParameter>(parameters)
                : new List<MxAnimationQuantizedParameter>();
        }

        public MxAnimationBlendKind BlendKind { get; }
        public string TargetActorId { get; }
        public string BlendId { get; }
        public float FadeDurationSeconds { get; }
        public string CorrelationId { get; }
        public IReadOnlyList<MxAnimationQuantizedParameter> Parameters => _parameters;

        public static MxAnimationBlendRequest From1D(MxAnimationBlend1DRequest request)
        {
            if (request == null)
                return null;

            return new MxAnimationBlendRequest(
                MxAnimationBlendKind.Blend1D,
                request.TargetActorId,
                request.BlendId,
                new[] { request.Parameter },
                request.FadeDurationSeconds,
                request.CorrelationId);
        }

        public static MxAnimationBlendRequest From2D(MxAnimationBlend2DRequest request)
        {
            if (request == null)
                return null;

            return new MxAnimationBlendRequest(
                MxAnimationBlendKind.Blend2D,
                request.TargetActorId,
                request.BlendId,
                new[] { request.ParameterX, request.ParameterY },
                request.FadeDurationSeconds,
                request.CorrelationId);
        }
    }

    public sealed class MxAnimationBlend1DRequest
    {
        public string TargetActorId { get; set; } = string.Empty;
        public string BlendId { get; set; } = string.Empty;
        public MxAnimationQuantizedParameter Parameter { get; set; }
        public float FadeDurationSeconds { get; set; } = -1f;
        public string CorrelationId { get; set; } = string.Empty;
    }

    public sealed class MxAnimationBlend2DRequest
    {
        public string TargetActorId { get; set; } = string.Empty;
        public string BlendId { get; set; } = string.Empty;
        public MxAnimationQuantizedParameter ParameterX { get; set; }
        public MxAnimationQuantizedParameter ParameterY { get; set; }
        public float PlaybackSpeedMultiplier { get; set; } = 1f;
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
            IEnumerable<MxAnimationBlend1DWeight> blend1DWeights = null,
            MxAnimationBlendKind blendKind = MxAnimationBlendKind.None,
            string blend2DId = "",
            MxAnimationQuantizedParameter blend2DParameterX = default,
            MxAnimationQuantizedParameter blend2DParameterY = default,
            IEnumerable<MxAnimationBlend2DWeight> blend2DWeights = null,
            IEnumerable<MxAnimationClipPlaybackDiagnostic> activeClipPlaybacks = null)
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
            BlendKind = blendKind;
            Blend2DId = blend2DId ?? string.Empty;
            Blend2DParameterX = blend2DParameterX;
            Blend2DParameterY = blend2DParameterY;
            _blend2DWeights = blend2DWeights != null
                ? new List<MxAnimationBlend2DWeight>(blend2DWeights)
                : new List<MxAnimationBlend2DWeight>();
            _activeClipPlaybacks = activeClipPlaybacks != null
                ? new List<MxAnimationClipPlaybackDiagnostic>(activeClipPlaybacks)
                : new List<MxAnimationClipPlaybackDiagnostic>();
        }

        private readonly List<MxAnimationBlend1DWeight> _blend1DWeights;
        private readonly List<MxAnimationBlend2DWeight> _blend2DWeights;
        private readonly List<MxAnimationClipPlaybackDiagnostic> _activeClipPlaybacks;

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
        public MxAnimationBlendKind BlendKind { get; }
        public string Blend2DId { get; }
        public MxAnimationQuantizedParameter Blend2DParameterX { get; }
        public MxAnimationQuantizedParameter Blend2DParameterY { get; }
        public IReadOnlyList<MxAnimationBlend2DWeight> Blend2DWeights => _blend2DWeights;
        public IReadOnlyList<MxAnimationClipPlaybackDiagnostic> ActiveClipPlaybacks => _activeClipPlaybacks;

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }

    public sealed class MxAnimationClipPlaybackDiagnostic
    {
        public MxAnimationClipPlaybackDiagnostic(
            ResourceKey clipKey,
            float weight,
            float normalizedTime,
            float playbackSpeed,
            bool loop,
            bool blendSlot,
            bool fallback)
        {
            ClipKey = clipKey;
            Weight = Clamp01(weight);
            NormalizedTime = Normalize01(normalizedTime);
            PlaybackSpeed = float.IsNaN(playbackSpeed) || float.IsInfinity(playbackSpeed) ? 1f : playbackSpeed;
            Loop = loop;
            BlendSlot = blendSlot;
            Fallback = fallback;
        }

        public ResourceKey ClipKey { get; }
        public float Weight { get; }
        public float NormalizedTime { get; }
        public float PlaybackSpeed { get; }
        public bool Loop { get; }
        public bool BlendSlot { get; }
        public bool Fallback { get; }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }

        private static float Normalize01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;
            float normalized = value % 1f;
            if (normalized < 0f)
                normalized += 1f;
            return normalized;
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

    public sealed class MxAnimationBackendCacheDiagnostic
    {
        public static readonly MxAnimationBackendCacheDiagnostic Empty =
            new MxAnimationBackendCacheDiagnostic(0, 0, 0, 0, 0, 0, 0);

        public MxAnimationBackendCacheDiagnostic(
            int cacheHitCount,
            int cacheMissCount,
            int residentClipCount,
            int cachedPlayableCount,
            int activePlayableCount,
            int resourceLoadedCount,
            int resourceRefCount)
        {
            CacheHitCount = Math.Max(0, cacheHitCount);
            CacheMissCount = Math.Max(0, cacheMissCount);
            ResidentClipCount = Math.Max(0, residentClipCount);
            CachedPlayableCount = Math.Max(0, cachedPlayableCount);
            ActivePlayableCount = Math.Max(0, activePlayableCount);
            ResourceLoadedCount = Math.Max(0, resourceLoadedCount);
            ResourceRefCount = Math.Max(0, resourceRefCount);
        }

        public int CacheHitCount { get; }
        public int CacheMissCount { get; }
        public int ResidentClipCount { get; }
        public int CachedPlayableCount { get; }
        public int ActivePlayableCount { get; }
        public int ResourceLoadedCount { get; }
        public int ResourceRefCount { get; }
    }

    public readonly struct MxAnimationRequestDiagnostic
    {
        private readonly string _correlationId;
        private readonly string _message;

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
            _correlationId = correlationId ?? string.Empty;
            _message = message ?? string.Empty;
        }

        public MxAnimationRequestKind Kind { get; }
        public MxAnimationLayerId LayerId { get; }
        public ResourceKey RequestedClipKey { get; }
        public ResourceKey ResolvedClipKey { get; }
        public bool UsedFallback { get; }
        public MxAnimationBackendResultCode ResultCode { get; }
        public string CorrelationId => _correlationId ?? string.Empty;
        public string Message => _message ?? string.Empty;
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
            IEnumerable<ResourceError> recentResourceErrors,
            MxAnimationBackendCacheDiagnostic cache = null)
        {
            BackendName = backendName ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            SetId = setId ?? string.Empty;
            ActorCount = actorCount;
            GraphIsValid = graphIsValid;
            IsReleased = isReleased;
            DefaultClip = defaultClip;
            FallbackClip = fallbackClip;
            Cache = cache ?? MxAnimationBackendCacheDiagnostic.Empty;
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
        public MxAnimationBackendCacheDiagnostic Cache { get; }
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

    /// <summary>Identifies the role of a clip playable in a captured animation layer.</summary>
    public enum MxAnimationPlaybackSlotRole
    {
        Current = 0,
        Outgoing = 1,
        Blend = 2
    }

    /// <summary>Immutable low-frequency snapshot of one clip playable and its fade state.</summary>
    public readonly struct MxAnimationPlaybackSlotState
    {
        public MxAnimationPlaybackSlotState(
            MxAnimationPlaybackSlotRole role,
            ResourceKey clipKey,
            double timeSeconds,
            float playbackSpeed,
            bool loop,
            bool fallback,
            float weight,
            float targetWeight,
            float fadeElapsedSeconds,
            float fadeDurationSeconds)
        {
            Role = role;
            ClipKey = clipKey;
            TimeSeconds = IsFinite(timeSeconds) ? Math.Max(0d, timeSeconds) : 0d;
            PlaybackSpeed = IsFinite(playbackSpeed) ? playbackSpeed : 1f;
            Loop = loop;
            Fallback = fallback;
            Weight = Clamp01(weight);
            TargetWeight = Clamp01(targetWeight);
            FadeElapsedSeconds = IsFinite(fadeElapsedSeconds) ? Math.Max(0f, fadeElapsedSeconds) : 0f;
            FadeDurationSeconds = IsFinite(fadeDurationSeconds) ? Math.Max(0f, fadeDurationSeconds) : 0f;
        }

        public MxAnimationPlaybackSlotRole Role { get; }
        public ResourceKey ClipKey { get; }
        public double TimeSeconds { get; }
        public float PlaybackSpeed { get; }
        public bool Loop { get; }
        public bool Fallback { get; }
        public float Weight { get; }
        public float TargetWeight { get; }
        public float FadeElapsedSeconds { get; }
        public float FadeDurationSeconds { get; }

        private static float Clamp01(float value)
        {
            if (!IsFinite(value) || value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    /// <summary>AllocByDesign snapshot of one animation layer, its transitions, blend inputs, and clips.</summary>
    public sealed class MxAnimationLayerPlaybackState
    {
        private readonly MxAnimationPlaybackSlotState[] _slots;
        private readonly MxAnimationBlend1DWeight[] _blend1DWeights;
        private readonly MxAnimationBlend2DWeight[] _blend2DWeights;

        public MxAnimationLayerPlaybackState(
            MxAnimationLayerId layerId,
            MxAnimationLayerStatus status,
            float weight,
            float startWeight,
            float targetWeight,
            float weightTransitionElapsedSeconds,
            float weightTransitionDurationSeconds,
            string weightTransitionPolicyId,
            string weightTransitionCorrelationId,
            MxAnimationBlendKind activeBlendKind,
            string activeBlendId,
            MxAnimationQuantizedParameter blendParameter,
            MxAnimationQuantizedParameter blend2DParameterX,
            MxAnimationQuantizedParameter blend2DParameterY,
            float blendFadeElapsedSeconds,
            float blendFadeDurationSeconds,
            IReadOnlyList<MxAnimationPlaybackSlotState> slots,
            IReadOnlyList<MxAnimationBlend1DWeight> blend1DWeights = null,
            IReadOnlyList<MxAnimationBlend2DWeight> blend2DWeights = null)
        {
            LayerId = layerId;
            Status = status;
            Weight = Clamp01(weight);
            StartWeight = Clamp01(startWeight);
            TargetWeight = Clamp01(targetWeight);
            WeightTransitionElapsedSeconds = ClampNonNegative(weightTransitionElapsedSeconds);
            WeightTransitionDurationSeconds = ClampNonNegative(weightTransitionDurationSeconds);
            WeightTransitionPolicyId = weightTransitionPolicyId ?? string.Empty;
            WeightTransitionCorrelationId = weightTransitionCorrelationId ?? string.Empty;
            ActiveBlendKind = activeBlendKind;
            ActiveBlendId = activeBlendId ?? string.Empty;
            BlendParameter = blendParameter;
            Blend2DParameterX = blend2DParameterX;
            Blend2DParameterY = blend2DParameterY;
            BlendFadeElapsedSeconds = ClampNonNegative(blendFadeElapsedSeconds);
            BlendFadeDurationSeconds = ClampNonNegative(blendFadeDurationSeconds);
            int count = slots != null ? slots.Count : 0;
            _slots = count > 0 ? new MxAnimationPlaybackSlotState[count] : Array.Empty<MxAnimationPlaybackSlotState>();
            for (int i = 0; i < count; i++)
                _slots[i] = slots[i];

            int blend1DCount = blend1DWeights != null ? blend1DWeights.Count : 0;
            _blend1DWeights = blend1DCount > 0
                ? new MxAnimationBlend1DWeight[blend1DCount]
                : Array.Empty<MxAnimationBlend1DWeight>();
            for (int i = 0; i < blend1DCount; i++)
                _blend1DWeights[i] = blend1DWeights[i];

            int blend2DCount = blend2DWeights != null ? blend2DWeights.Count : 0;
            _blend2DWeights = blend2DCount > 0
                ? new MxAnimationBlend2DWeight[blend2DCount]
                : Array.Empty<MxAnimationBlend2DWeight>();
            for (int i = 0; i < blend2DCount; i++)
                _blend2DWeights[i] = blend2DWeights[i];
        }

        /// <summary>Captures the continuation phase used by the next looping Blend synchronization.</summary>
        /// <remarks>
        /// The original constructor remains available and leaves this state unspecified.
        /// A captured phase may exceed one after deferred presentation advancement.
        /// </remarks>
        public MxAnimationLayerPlaybackState(
            MxAnimationLayerId layerId,
            MxAnimationLayerStatus status,
            float weight,
            float startWeight,
            float targetWeight,
            float weightTransitionElapsedSeconds,
            float weightTransitionDurationSeconds,
            string weightTransitionPolicyId,
            string weightTransitionCorrelationId,
            MxAnimationBlendKind activeBlendKind,
            string activeBlendId,
            MxAnimationQuantizedParameter blendParameter,
            MxAnimationQuantizedParameter blend2DParameterX,
            MxAnimationQuantizedParameter blend2DParameterY,
            float blendFadeElapsedSeconds,
            float blendFadeDurationSeconds,
            IReadOnlyList<MxAnimationPlaybackSlotState> slots,
            IReadOnlyList<MxAnimationBlend1DWeight> blend1DWeights,
            IReadOnlyList<MxAnimationBlend2DWeight> blend2DWeights,
            double? blendNormalizedPhase)
            : this(layerId, status, weight, startWeight, targetWeight,
                weightTransitionElapsedSeconds, weightTransitionDurationSeconds,
                weightTransitionPolicyId, weightTransitionCorrelationId,
                activeBlendKind, activeBlendId, blendParameter, blend2DParameterX, blend2DParameterY,
                blendFadeElapsedSeconds, blendFadeDurationSeconds, slots, blend1DWeights, blend2DWeights)
        {
            BlendNormalizedPhase = blendNormalizedPhase;
        }

        /// <summary>
        /// Next synchronization phase, distinct from the currently sampled slot times;
        /// null identifies a legacy snapshot without continuation phase state.
        /// </summary>
        public double? BlendNormalizedPhase { get; }

        public MxAnimationLayerId LayerId { get; }
        public MxAnimationLayerStatus Status { get; }
        public float Weight { get; }
        public float StartWeight { get; }
        public float TargetWeight { get; }
        public float WeightTransitionElapsedSeconds { get; }
        public float WeightTransitionDurationSeconds { get; }
        public string WeightTransitionPolicyId { get; }
        public string WeightTransitionCorrelationId { get; }
        public MxAnimationBlendKind ActiveBlendKind { get; }
        public string ActiveBlendId { get; }
        public MxAnimationQuantizedParameter BlendParameter { get; }
        public MxAnimationQuantizedParameter Blend2DParameterX { get; }
        public MxAnimationQuantizedParameter Blend2DParameterY { get; }
        public float BlendFadeElapsedSeconds { get; }
        public float BlendFadeDurationSeconds { get; }
        public IReadOnlyList<MxAnimationPlaybackSlotState> Slots => _slots;
        public IReadOnlyList<MxAnimationBlend1DWeight> Blend1DWeights => _blend1DWeights;
        public IReadOnlyList<MxAnimationBlend2DWeight> Blend2DWeights => _blend2DWeights;

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }

        private static float ClampNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);
        }
    }

    /// <summary>AllocByDesign backend-owned playback checkpoint used by rollback and developer preview tools.</summary>
    public sealed class MxAnimationPlaybackState
    {
        private readonly MxAnimationLayerPlaybackState[] _layers;

        public MxAnimationPlaybackState(
            string backendName,
            string actorId,
            string animationSetId,
            IReadOnlyList<MxAnimationLayerPlaybackState> layers)
        {
            BackendName = backendName ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            AnimationSetId = animationSetId ?? string.Empty;
            int count = layers != null ? layers.Count : 0;
            _layers = count > 0 ? new MxAnimationLayerPlaybackState[count] : Array.Empty<MxAnimationLayerPlaybackState>();
            for (int i = 0; i < count; i++)
                _layers[i] = layers[i];
        }

        public string BackendName { get; }
        public string ActorId { get; }
        public string AnimationSetId { get; }
        public IReadOnlyList<MxAnimationLayerPlaybackState> Layers => _layers;
    }

    /// <summary>
    /// Optional low-frequency extension for animation backends that can restore an exact presentation pose.
    /// </summary>
    public interface IMxAnimationRewindableBackend
    {
        /// <summary>AllocByDesign: captures owned playback arrays for a checkpoint or rollback record.</summary>
        MxAnimationPlaybackState CapturePlaybackState();

        /// <summary>LowFreqAlloc: rebuilds backend slots and evaluates the restored pose without advancing time.</summary>
        MxAnimationBackendResult RestorePlaybackState(MxAnimationPlaybackState state);

        /// <summary>NoAlloc: evaluates the current graph state without advancing playback time.</summary>
        void EvaluateCurrentPose();
    }

    /// <summary>Optional presentation clock input; does not change gameplay authority.</summary>
    public interface IMxAnimationTimeSynchronizedBackend
    {
        /// <summary>NoAlloc: anchors the matching current clip on the next Tick without restarting its transition.</summary>
        bool TrySynchronizeCurrentClipTime(MxAnimationLayerId layerId, ResourceKey expectedClipKey, double timeSeconds);
    }

    /// <summary>Optional transient presentation sampling, independent of gameplay and playback clocks.</summary>
    public interface IMxAnimationPoseSamplingBackend
    {
        /// <summary>NoAlloc. Evaluate matching current clip at time; restore graph time. Caller must restore transforms.</summary>
        bool TryEvaluateClipPose(MxAnimationLayerId layerId, ResourceKey expectedClipKey, double timeSeconds);
    }

    /// <summary>Optional presentation-only state advancement without evaluating a skeletal pose.</summary>
    public interface IMxAnimationPoseEvaluationBackend : IMxAnimationRewindableBackend
    {
        /// <summary>False for playback that requires every evaluation, including root motion or clip events.</summary>
        bool CanDeferPoseEvaluation { get; }

        /// <summary>NoAlloc: advance loads, transitions and all clip clocks once; retain the published pose.</summary>
        /// <remarks>The caller evaluates the current pose when due. Never accumulate a second catch-up delta.</remarks>
        void AdvancePresentationState(float deltaTime);
    }

    public interface IMxAnimationBackend : IDisposable
    {
        string BackendName { get; }
        MxAnimationBackendResult Play(MxAnimationPlayRequest request);
        MxAnimationBackendResult Stop(MxAnimationStopRequest request);
        MxAnimationBackendResult CrossFade(MxAnimationCrossFadeRequest request);
        MxAnimationBackendResult SetLayerWeight(MxAnimationLayerWeightRequest request);
        MxAnimationBackendResult SetBlend1D(MxAnimationBlend1DRequest request);
        MxAnimationBackendResult SetBlend2D(MxAnimationBlend2DRequest request);
        void Tick(float deltaTime);
        MxAnimationDiagnosticSnapshot CreateSnapshot();
        void Release();
    }
}
