using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Resources;

namespace MxFramework.Animation
{
    public enum MxAnimationLocomotionFoot
    {
        Left,
        Right
    }

    public enum MxAnimationFootSlipGrade
    {
        Ok,
        Warning,
        Bad
    }

    public static class MxAnimationLocomotionCalibrationIssueCodes
    {
        public const string BlendUnreachablePoint = "LOCO_CAL_BLEND_UNREACHABLE_POINT";
        public const string ClipMetadataMissing = "LOCO_CAL_CLIP_METADATA_MISSING";
        public const string DraftEmpty = "LOCO_CAL_DRAFT_EMPTY";
        public const string ApplyFieldUnsupported = "LOCO_CAL_APPLY_FIELD_UNSUPPORTED";
        public const string ApplyTargetMissing = "LOCO_CAL_APPLY_TARGET_MISSING";
        public const string ApplyRangeInvalid = "LOCO_CAL_APPLY_RANGE_INVALID";
        public const string ApplyConflict = "LOCO_CAL_APPLY_CONFLICT";
        public const string ApplyCompileFailed = "LOCO_CAL_APPLY_COMPILE_FAILED";
        public const string FootBoneMissing = "LOCO_CAL_FOOT_BONE_MISSING";
        public const string ClipTimeMissing = "LOCO_CAL_CLIP_TIME_MISSING";
        public const string FootMetadataMissing = "LOCO_CAL_FOOT_METADATA_MISSING";
    }

    public readonly struct MxAnimationVelocity2D : IEquatable<MxAnimationVelocity2D>
    {
        public MxAnimationVelocity2D(float x, float y)
        {
            X = Sanitize(x);
            Y = Sanitize(y);
        }

        public float X { get; }
        public float Y { get; }
        public float Magnitude => (float)Math.Sqrt((X * X) + (Y * Y));

        public bool Equals(MxAnimationVelocity2D other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is MxAnimationVelocity2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public override string ToString()
        {
            return X.ToString("0.###", CultureInfo.InvariantCulture)
                + ","
                + Y.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public static bool operator ==(MxAnimationVelocity2D left, MxAnimationVelocity2D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxAnimationVelocity2D left, MxAnimationVelocity2D right)
        {
            return !left.Equals(right);
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }

    public readonly struct MxAnimationFootContactWindow : IEquatable<MxAnimationFootContactWindow>
    {
        public MxAnimationFootContactWindow(float startNormalized, float endNormalized, float confidence = 1f)
        {
            StartNormalized = Normalize01(startNormalized);
            EndNormalized = Normalize01(endNormalized);
            Confidence = Clamp01(confidence);
        }

        public float StartNormalized { get; }
        public float EndNormalized { get; }
        public float Confidence { get; }

        public bool Contains(float normalizedTime)
        {
            float time = Normalize01(normalizedTime);
            if (Math.Abs(StartNormalized - EndNormalized) <= 0.0001f)
                return true;

            if (StartNormalized < EndNormalized)
                return time >= StartNormalized && time <= EndNormalized;

            return time >= StartNormalized || time <= EndNormalized;
        }

        public bool Equals(MxAnimationFootContactWindow other)
        {
            return StartNormalized.Equals(other.StartNormalized)
                && EndNormalized.Equals(other.EndNormalized)
                && Confidence.Equals(other.Confidence);
        }

        public override bool Equals(object obj)
        {
            return obj is MxAnimationFootContactWindow other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StartNormalized.GetHashCode();
                hash = (hash * 397) ^ EndNormalized.GetHashCode();
                hash = (hash * 397) ^ Confidence.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(MxAnimationFootContactWindow left, MxAnimationFootContactWindow right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxAnimationFootContactWindow left, MxAnimationFootContactWindow right)
        {
            return !left.Equals(right);
        }

        internal static float Normalize01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;

            float normalized = value % 1f;
            if (normalized < 0f)
                normalized += 1f;
            return normalized;
        }

        internal static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }

    public sealed class MxAnimationLocomotionClipCalibration
    {
        private readonly List<MxAnimationFootContactWindow> _leftFootContacts;
        private readonly List<MxAnimationFootContactWindow> _rightFootContacts;

        public MxAnimationLocomotionClipCalibration(
            string clipId,
            ResourceKey clipKey,
            float nativeVelocityX,
            float nativeVelocityY,
            float playbackSpeed = 1f,
            float cycleDurationSeconds = 0f,
            IEnumerable<MxAnimationFootContactWindow> leftFootContacts = null,
            IEnumerable<MxAnimationFootContactWindow> rightFootContacts = null)
        {
            ClipId = clipId ?? string.Empty;
            ClipKey = clipKey;
            NativeVelocityX = Sanitize(nativeVelocityX);
            NativeVelocityY = Sanitize(nativeVelocityY);
            PlaybackSpeed = Math.Abs(playbackSpeed) < 0.0001f ? 1f : playbackSpeed;
            CycleDurationSeconds = cycleDurationSeconds <= 0f || float.IsNaN(cycleDurationSeconds)
                ? 0f
                : cycleDurationSeconds;
            _leftFootContacts = leftFootContacts != null
                ? new List<MxAnimationFootContactWindow>(leftFootContacts)
                : new List<MxAnimationFootContactWindow>();
            _rightFootContacts = rightFootContacts != null
                ? new List<MxAnimationFootContactWindow>(rightFootContacts)
                : new List<MxAnimationFootContactWindow>();
        }

        public string ClipId { get; }
        public ResourceKey ClipKey { get; }
        public float NativeVelocityX { get; }
        public float NativeVelocityY { get; }
        public float PlaybackSpeed { get; }
        public float CycleDurationSeconds { get; }
        public IReadOnlyList<MxAnimationFootContactWindow> LeftFootContacts => _leftFootContacts;
        public IReadOnlyList<MxAnimationFootContactWindow> RightFootContacts => _rightFootContacts;

        public MxAnimationVelocity2D NativeVelocity => new MxAnimationVelocity2D(NativeVelocityX, NativeVelocityY);

        public float GetContactConfidence(MxAnimationLocomotionFoot foot, float normalizedTime)
        {
            IReadOnlyList<MxAnimationFootContactWindow> windows = foot == MxAnimationLocomotionFoot.Left
                ? _leftFootContacts
                : _rightFootContacts;
            float confidence = 0f;
            for (int i = 0; i < windows.Count; i++)
            {
                MxAnimationFootContactWindow window = windows[i];
                if (window.Contains(normalizedTime) && window.Confidence > confidence)
                    confidence = window.Confidence;
            }

            return confidence;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }

    public readonly struct MxAnimationFootSlipThresholds
    {
        public MxAnimationFootSlipThresholds(
            float okAverageSlipCmPerSecond,
            float okMaxSlipDistanceCm,
            float warningAverageSlipCmPerSecond,
            float warningMaxSlipDistanceCm)
        {
            OkAverageSlipCmPerSecond = Math.Max(0f, okAverageSlipCmPerSecond);
            OkMaxSlipDistanceCm = Math.Max(0f, okMaxSlipDistanceCm);
            WarningAverageSlipCmPerSecond = Math.Max(OkAverageSlipCmPerSecond, warningAverageSlipCmPerSecond);
            WarningMaxSlipDistanceCm = Math.Max(OkMaxSlipDistanceCm, warningMaxSlipDistanceCm);
        }

        public float OkAverageSlipCmPerSecond { get; }
        public float OkMaxSlipDistanceCm { get; }
        public float WarningAverageSlipCmPerSecond { get; }
        public float WarningMaxSlipDistanceCm { get; }

        public static MxAnimationFootSlipThresholds Default =>
            new MxAnimationFootSlipThresholds(3f, 3f, 8f, 8f);
    }

    public readonly struct MxAnimationBlend2DControllerDomain
    {
        public MxAnimationBlend2DControllerDomain(int minX, int maxX, int minY, int maxY)
        {
            MinX = Math.Min(minX, maxX);
            MaxX = Math.Max(minX, maxX);
            MinY = Math.Min(minY, maxY);
            MaxY = Math.Max(minY, maxY);
        }

        public int MinX { get; }
        public int MaxX { get; }
        public int MinY { get; }
        public int MaxY { get; }

        public bool Contains(int x, int y)
        {
            return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
        }
    }

    public sealed class MxAnimationBlendReachabilityPoint
    {
        public MxAnimationBlendReachabilityPoint(ResourceKey clipKey, int x, int y)
        {
            ClipKey = clipKey;
            X = x;
            Y = y;
        }

        public ResourceKey ClipKey { get; }
        public int X { get; }
        public int Y { get; }
    }

    public sealed class MxAnimationBlendReachabilityIssue
    {
        public MxAnimationBlendReachabilityIssue(
            string code,
            ResourceKey clipKey,
            int x,
            int y,
            string message)
        {
            Code = code ?? string.Empty;
            ClipKey = clipKey;
            X = x;
            Y = y;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public ResourceKey ClipKey { get; }
        public int X { get; }
        public int Y { get; }
        public string Message { get; }
    }

    public sealed class MxAnimationBlendReachabilityReport
    {
        private readonly List<MxAnimationBlendReachabilityPoint> _reachablePoints;
        private readonly List<MxAnimationBlendReachabilityPoint> _unreachablePoints;
        private readonly List<MxAnimationBlendReachabilityIssue> _issues;

        public MxAnimationBlendReachabilityReport(
            string blendId,
            MxAnimationBlend2DControllerDomain domain,
            IEnumerable<MxAnimationBlendReachabilityPoint> reachablePoints,
            IEnumerable<MxAnimationBlendReachabilityPoint> unreachablePoints,
            IEnumerable<MxAnimationBlendReachabilityIssue> issues)
        {
            BlendId = blendId ?? string.Empty;
            Domain = domain;
            _reachablePoints = reachablePoints != null
                ? new List<MxAnimationBlendReachabilityPoint>(reachablePoints)
                : new List<MxAnimationBlendReachabilityPoint>();
            _unreachablePoints = unreachablePoints != null
                ? new List<MxAnimationBlendReachabilityPoint>(unreachablePoints)
                : new List<MxAnimationBlendReachabilityPoint>();
            _issues = issues != null
                ? new List<MxAnimationBlendReachabilityIssue>(issues)
                : new List<MxAnimationBlendReachabilityIssue>();
        }

        public string BlendId { get; }
        public MxAnimationBlend2DControllerDomain Domain { get; }
        public IReadOnlyList<MxAnimationBlendReachabilityPoint> ReachablePoints => _reachablePoints;
        public IReadOnlyList<MxAnimationBlendReachabilityPoint> UnreachablePoints => _unreachablePoints;
        public IReadOnlyList<MxAnimationBlendReachabilityIssue> Issues => _issues;
        public bool HasUnreachablePoints => _unreachablePoints.Count > 0;
    }

    public sealed class MxAnimationLocomotionBlendProbeSnapshot
    {
        private readonly List<MxAnimationBlend2DWeight> _weights;

        public MxAnimationLocomotionBlendProbeSnapshot(
            string blendId,
            MxAnimationBlend2DControllerDomain domain,
            int sampleX,
            int sampleY,
            MxAnimationBlendReachabilityReport reachabilityReport,
            IEnumerable<MxAnimationBlend2DWeight> weights,
            bool weightsFromBackend)
        {
            BlendId = blendId ?? string.Empty;
            Domain = domain;
            SampleX = sampleX;
            SampleY = sampleY;
            ReachabilityReport = reachabilityReport;
            WeightsFromBackend = weightsFromBackend;
            _weights = weights != null
                ? new List<MxAnimationBlend2DWeight>(weights)
                : new List<MxAnimationBlend2DWeight>();
            DominantClipKey = FindDominantClipKey(_weights, out float dominantWeight);
            DominantWeight = dominantWeight;
        }

        public string BlendId { get; }
        public MxAnimationBlend2DControllerDomain Domain { get; }
        public int SampleX { get; }
        public int SampleY { get; }
        public MxAnimationBlendReachabilityReport ReachabilityReport { get; }
        public IReadOnlyList<MxAnimationBlend2DWeight> Weights => _weights;
        public bool WeightsFromBackend { get; }
        public ResourceKey DominantClipKey { get; }
        public float DominantWeight { get; }
        public bool HasDominantClip => DominantWeight > 0f && DominantClipKey.IsValid;

        private static ResourceKey FindDominantClipKey(
            IReadOnlyList<MxAnimationBlend2DWeight> weights,
            out float dominantWeight)
        {
            ResourceKey dominant = default;
            dominantWeight = 0f;
            if (weights == null)
                return dominant;

            for (int i = 0; i < weights.Count; i++)
            {
                MxAnimationBlend2DWeight weight = weights[i];
                if (weight.Weight <= dominantWeight)
                    continue;

                dominantWeight = weight.Weight;
                dominant = weight.ClipKey;
            }

            return dominant;
        }
    }

    public sealed class MxAnimationLocomotionCalibrationFrame
    {
        public MxAnimationLocomotionCalibrationFrame(
            long frame,
            float deltaTime,
            float targetLocalVelocityX,
            float targetLocalVelocityY,
            float actualLocalVelocityX,
            float actualLocalVelocityY,
            float blendedNativeVelocityX,
            float blendedNativeVelocityY,
            float velocityErrorRatio,
            float directionErrorDegrees,
            string dominantClipId,
            float leftFootContactConfidence,
            float rightFootContactConfidence,
            float leftFootSlipCmPerSecond,
            float rightFootSlipCmPerSecond,
            float maxSlipDistanceCm)
        {
            Frame = Math.Max(0, frame);
            DeltaTime = deltaTime <= 0f || float.IsNaN(deltaTime) ? 0f : deltaTime;
            TargetLocalVelocityX = Sanitize(targetLocalVelocityX);
            TargetLocalVelocityY = Sanitize(targetLocalVelocityY);
            ActualLocalVelocityX = Sanitize(actualLocalVelocityX);
            ActualLocalVelocityY = Sanitize(actualLocalVelocityY);
            BlendedNativeVelocityX = Sanitize(blendedNativeVelocityX);
            BlendedNativeVelocityY = Sanitize(blendedNativeVelocityY);
            VelocityErrorRatio = velocityErrorRatio <= 0f || float.IsNaN(velocityErrorRatio)
                ? 0f
                : velocityErrorRatio;
            DirectionErrorDegrees = Sanitize(directionErrorDegrees);
            DominantClipId = dominantClipId ?? string.Empty;
            LeftFootContactConfidence = MxAnimationFootContactWindow.Clamp01(leftFootContactConfidence);
            RightFootContactConfidence = MxAnimationFootContactWindow.Clamp01(rightFootContactConfidence);
            LeftFootSlipCmPerSecond = Math.Max(0f, Sanitize(leftFootSlipCmPerSecond));
            RightFootSlipCmPerSecond = Math.Max(0f, Sanitize(rightFootSlipCmPerSecond));
            MaxSlipDistanceCm = Math.Max(0f, Sanitize(maxSlipDistanceCm));
        }

        public long Frame { get; }
        public float DeltaTime { get; }
        public float TargetLocalVelocityX { get; }
        public float TargetLocalVelocityY { get; }
        public float ActualLocalVelocityX { get; }
        public float ActualLocalVelocityY { get; }
        public float BlendedNativeVelocityX { get; }
        public float BlendedNativeVelocityY { get; }
        public float VelocityErrorRatio { get; }
        public float DirectionErrorDegrees { get; }
        public string DominantClipId { get; }
        public float LeftFootContactConfidence { get; }
        public float RightFootContactConfidence { get; }
        public float LeftFootSlipCmPerSecond { get; }
        public float RightFootSlipCmPerSecond { get; }
        public float MaxSlipDistanceCm { get; }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }

    public sealed class MxAnimationLocomotionCalibrationChange
    {
        public MxAnimationLocomotionCalibrationChange(
            string targetKind,
            string targetId,
            string field,
            string oldValue,
            string newValue,
            string reason)
        {
            TargetKind = targetKind ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Field = field ?? string.Empty;
            OldValue = oldValue ?? string.Empty;
            NewValue = newValue ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string TargetKind { get; }
        public string TargetId { get; }
        public string Field { get; }
        public string OldValue { get; }
        public string NewValue { get; }
        public string Reason { get; }
    }

    public sealed class MxAnimationLocomotionCalibrationDraft
    {
        private readonly List<MxAnimationLocomotionCalibrationChange> _changes;
        private readonly List<string> _diagnostics;

        public MxAnimationLocomotionCalibrationDraft(
            string packageId,
            string animationSetId,
            string blendId,
            IEnumerable<MxAnimationLocomotionCalibrationChange> changes,
            IEnumerable<string> diagnostics = null,
            bool requiresCompile = true)
        {
            PackageId = packageId ?? string.Empty;
            AnimationSetId = animationSetId ?? string.Empty;
            BlendId = blendId ?? string.Empty;
            _changes = changes != null
                ? new List<MxAnimationLocomotionCalibrationChange>(changes)
                : new List<MxAnimationLocomotionCalibrationChange>();
            _diagnostics = diagnostics != null
                ? new List<string>(diagnostics)
                : new List<string>();
            RequiresCompile = requiresCompile;
        }

        public string PackageId { get; }
        public string AnimationSetId { get; }
        public string BlendId { get; }
        public IReadOnlyList<MxAnimationLocomotionCalibrationChange> Changes => _changes;
        public IReadOnlyList<string> Diagnostics => _diagnostics;
        public bool RequiresCompile { get; }
        public bool IsEmpty => _changes.Count == 0;
    }

    public static class MxAnimationLocomotionCalibrationCalculator
    {
        private const float Epsilon = 0.0001f;

        public static MxAnimationVelocity2D BlendNativeVelocity(
            IEnumerable<MxAnimationBlend2DWeight> weights,
            IEnumerable<MxAnimationLocomotionClipCalibration> calibrations)
        {
            if (weights == null || calibrations == null)
                return default;

            float x = 0f;
            float y = 0f;
            foreach (MxAnimationBlend2DWeight weight in weights)
            {
                if (weight.Weight <= 0f)
                    continue;

                MxAnimationLocomotionClipCalibration calibration =
                    FindCalibration(calibrations, weight.ClipKey);
                if (calibration == null)
                    continue;

                float playbackSpeed = Math.Abs(weight.PlaybackSpeed) < Epsilon
                    ? calibration.PlaybackSpeed
                    : weight.PlaybackSpeed;
                x += calibration.NativeVelocityX * playbackSpeed * weight.Weight;
                y += calibration.NativeVelocityY * playbackSpeed * weight.Weight;
            }

            return new MxAnimationVelocity2D(x, y);
        }

        public static MxAnimationFootSlipGrade ClassifySlip(
            float averageSlipCmPerSecond,
            float maxSlipDistanceCm,
            MxAnimationFootSlipThresholds thresholds)
        {
            float average = Math.Max(0f, averageSlipCmPerSecond);
            float max = Math.Max(0f, maxSlipDistanceCm);
            if (average <= thresholds.OkAverageSlipCmPerSecond && max <= thresholds.OkMaxSlipDistanceCm)
                return MxAnimationFootSlipGrade.Ok;
            if (average <= thresholds.WarningAverageSlipCmPerSecond && max <= thresholds.WarningMaxSlipDistanceCm)
                return MxAnimationFootSlipGrade.Warning;
            return MxAnimationFootSlipGrade.Bad;
        }

        public static float CalculateVelocityErrorRatio(
            MxAnimationVelocity2D actualLocalVelocity,
            MxAnimationVelocity2D blendedNativeVelocity)
        {
            float dx = actualLocalVelocity.X - blendedNativeVelocity.X;
            float dy = actualLocalVelocity.Y - blendedNativeVelocity.Y;
            float error = (float)Math.Sqrt((dx * dx) + (dy * dy));
            float denominator = Math.Max(actualLocalVelocity.Magnitude, Epsilon);
            return error / denominator;
        }

        public static float CalculateWeightedFootContactConfidence(
            MxAnimationLocomotionFoot foot,
            IEnumerable<MxAnimationBlend2DWeight> weights,
            IEnumerable<MxAnimationClipPlaybackDiagnostic> playbacks,
            IEnumerable<MxAnimationLocomotionClipCalibration> calibrations)
        {
            if (weights == null || playbacks == null || calibrations == null)
                return 0f;

            float confidence = 0f;
            foreach (MxAnimationBlend2DWeight weight in weights)
            {
                if (weight.Weight <= 0f)
                    continue;

                MxAnimationLocomotionClipCalibration calibration = FindCalibration(calibrations, weight.ClipKey);
                if (calibration == null)
                    continue;

                MxAnimationClipPlaybackDiagnostic playback = FindPlayback(playbacks, weight.ClipKey);
                if (playback == null)
                    continue;

                confidence += calibration.GetContactConfidence(foot, playback.NormalizedTime) * weight.Weight;
            }

            return confidence <= 0f ? 0f : confidence >= 1f ? 1f : confidence;
        }

        public static float CalculateSlipCmPerSecond(
            float previousX,
            float previousY,
            float currentX,
            float currentY,
            float deltaTime)
        {
            if (deltaTime <= Epsilon || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                return 0f;

            float dx = currentX - previousX;
            float dy = currentY - previousY;
            float meters = (float)Math.Sqrt((dx * dx) + (dy * dy));
            return (meters * 100f) / deltaTime;
        }

        public static float CalculateSlipDistanceCm(
            float anchorX,
            float anchorY,
            float currentX,
            float currentY)
        {
            float dx = currentX - anchorX;
            float dy = currentY - anchorY;
            return (float)Math.Sqrt((dx * dx) + (dy * dy)) * 100f;
        }

        private static MxAnimationLocomotionClipCalibration FindCalibration(
            IEnumerable<MxAnimationLocomotionClipCalibration> calibrations,
            ResourceKey clipKey)
        {
            foreach (MxAnimationLocomotionClipCalibration calibration in calibrations)
            {
                if (calibration != null && calibration.ClipKey == clipKey)
                    return calibration;
            }

            return null;
        }

        private static MxAnimationClipPlaybackDiagnostic FindPlayback(
            IEnumerable<MxAnimationClipPlaybackDiagnostic> playbacks,
            ResourceKey clipKey)
        {
            foreach (MxAnimationClipPlaybackDiagnostic playback in playbacks)
            {
                if (playback != null && playback.ClipKey == clipKey)
                    return playback;
            }

            return null;
        }
    }

    public static class MxAnimationBlendReachabilityAnalyzer
    {
        public static MxAnimationBlendReachabilityReport Analyze(
            MxAnimationBlend2DDefinition definition,
            MxAnimationBlend2DControllerDomain domain)
        {
            var reachable = new List<MxAnimationBlendReachabilityPoint>();
            var unreachable = new List<MxAnimationBlendReachabilityPoint>();
            var issues = new List<MxAnimationBlendReachabilityIssue>();
            if (definition == null)
                return new MxAnimationBlendReachabilityReport(string.Empty, domain, reachable, unreachable, issues);

            for (int i = 0; i < definition.Points.Count; i++)
            {
                MxAnimationBlend2DPoint point = definition.Points[i];
                if (point == null)
                    continue;

                var reportPoint = new MxAnimationBlendReachabilityPoint(point.ClipKey, point.X, point.Y);
                if (domain.Contains(point.X, point.Y))
                {
                    reachable.Add(reportPoint);
                    continue;
                }

                unreachable.Add(reportPoint);
                issues.Add(new MxAnimationBlendReachabilityIssue(
                    MxAnimationLocomotionCalibrationIssueCodes.BlendUnreachablePoint,
                    point.ClipKey,
                    point.X,
                    point.Y,
                    "Blend point is outside the controller output domain."));
            }

            return new MxAnimationBlendReachabilityReport(
                definition.BlendId,
                domain,
                reachable,
                unreachable,
                issues);
        }
    }

    public static class MxAnimationLocomotionCalibrationReportFormatter
    {
        public static string CreateSummary(
            MxAnimationBlendReachabilityReport reachabilityReport,
            MxAnimationLocomotionCalibrationDraft draft = null)
        {
            var builder = new StringBuilder();
            if (reachabilityReport != null)
            {
                builder.Append("blend=").Append(reachabilityReport.BlendId).Append('\n');
                builder.Append("reachable=").Append(reachabilityReport.ReachablePoints.Count)
                    .Append(" unreachable=").Append(reachabilityReport.UnreachablePoints.Count).Append('\n');
                for (int i = 0; i < reachabilityReport.Issues.Count; i++)
                {
                    MxAnimationBlendReachabilityIssue issue = reachabilityReport.Issues[i];
                    builder.Append(issue.Code)
                        .Append(" clip=").Append(issue.ClipKey.Id)
                        .Append(" point=(").Append(issue.X).Append(',').Append(issue.Y).Append(')')
                        .Append(" message=").Append(issue.Message)
                        .Append('\n');
                }
            }

            if (draft != null)
            {
                builder.Append("draftChanges=").Append(draft.Changes.Count)
                    .Append(" requiresCompile=").Append(draft.RequiresCompile ? "true" : "false")
                    .Append('\n');
                for (int i = 0; i < draft.Changes.Count; i++)
                {
                    MxAnimationLocomotionCalibrationChange change = draft.Changes[i];
                    builder.Append(change.TargetKind).Append(':').Append(change.TargetId)
                        .Append(' ').Append(change.Field)
                        .Append(' ').Append(change.OldValue)
                        .Append(" -> ").Append(change.NewValue)
                        .Append(" reason=").Append(change.Reason)
                        .Append('\n');
                }
            }

            return builder.ToString().TrimEnd();
        }
    }
}
