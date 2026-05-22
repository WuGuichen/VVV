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

    public sealed class MxAnimationLocomotionPresetReport
    {
        private readonly List<string> _diagnostics;
        private readonly List<string> _suggestedFields;

        public MxAnimationLocomotionPresetReport(
            string presetId,
            string displayName,
            float durationSeconds,
            int sampleCount,
            float averageVelocityErrorRatio,
            float maxSlipDistanceCm,
            float maxFootSlipCmPerSecond,
            MxAnimationFootSlipGrade grade,
            string dominantClipId,
            int unreachablePointCount,
            int resourceErrorCount,
            int backendErrorCount,
            float suggestedPlaybackSpeed,
            IEnumerable<string> diagnostics = null,
            IEnumerable<string> suggestedFields = null)
        {
            PresetId = presetId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DurationSeconds = durationSeconds <= 0f || float.IsNaN(durationSeconds) ? 0f : durationSeconds;
            SampleCount = Math.Max(0, sampleCount);
            AverageVelocityErrorRatio = Math.Max(0f, Sanitize(averageVelocityErrorRatio));
            MaxSlipDistanceCm = Math.Max(0f, Sanitize(maxSlipDistanceCm));
            MaxFootSlipCmPerSecond = Math.Max(0f, Sanitize(maxFootSlipCmPerSecond));
            Grade = grade;
            DominantClipId = dominantClipId ?? string.Empty;
            UnreachablePointCount = Math.Max(0, unreachablePointCount);
            ResourceErrorCount = Math.Max(0, resourceErrorCount);
            BackendErrorCount = Math.Max(0, backendErrorCount);
            SuggestedPlaybackSpeed = Math.Max(0f, Sanitize(suggestedPlaybackSpeed));
            _diagnostics = diagnostics != null ? new List<string>(diagnostics) : new List<string>();
            _suggestedFields = suggestedFields != null ? new List<string>(suggestedFields) : new List<string>();
        }

        public string PresetId { get; }
        public string DisplayName { get; }
        public float DurationSeconds { get; }
        public int SampleCount { get; }
        public float AverageVelocityErrorRatio { get; }
        public float MaxSlipDistanceCm { get; }
        public float MaxFootSlipCmPerSecond { get; }
        public MxAnimationFootSlipGrade Grade { get; }
        public string DominantClipId { get; }
        public int UnreachablePointCount { get; }
        public int ResourceErrorCount { get; }
        public int BackendErrorCount { get; }
        public float SuggestedPlaybackSpeed { get; }
        public IReadOnlyList<string> Diagnostics => _diagnostics;
        public IReadOnlyList<string> SuggestedFields => _suggestedFields;
        public bool HasBlockingIssue => Grade == MxAnimationFootSlipGrade.Bad
            || UnreachablePointCount > 0
            || ResourceErrorCount > 0
            || BackendErrorCount > 0;

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }

    public sealed class MxAnimationLocomotionPresetSequenceReport
    {
        private readonly List<MxAnimationLocomotionPresetReport> _presets;
        private readonly List<string> _diagnostics;

        public MxAnimationLocomotionPresetSequenceReport(
            string packageId,
            string characterResourceId,
            string animationSetId,
            string blendId,
            string generatedAtUtc,
            IEnumerable<MxAnimationLocomotionPresetReport> presets,
            IEnumerable<string> diagnostics = null)
        {
            PackageId = packageId ?? string.Empty;
            CharacterResourceId = characterResourceId ?? string.Empty;
            AnimationSetId = animationSetId ?? string.Empty;
            BlendId = blendId ?? string.Empty;
            GeneratedAtUtc = generatedAtUtc ?? string.Empty;
            _presets = presets != null
                ? new List<MxAnimationLocomotionPresetReport>(presets)
                : new List<MxAnimationLocomotionPresetReport>();
            _diagnostics = diagnostics != null ? new List<string>(diagnostics) : new List<string>();
            OverallGrade = CalculateOverallGrade(_presets);
        }

        public string PackageId { get; }
        public string CharacterResourceId { get; }
        public string AnimationSetId { get; }
        public string BlendId { get; }
        public string GeneratedAtUtc { get; }
        public IReadOnlyList<MxAnimationLocomotionPresetReport> Presets => _presets;
        public IReadOnlyList<string> Diagnostics => _diagnostics;
        public MxAnimationFootSlipGrade OverallGrade { get; }
        public bool HasBlockingIssue => OverallGrade == MxAnimationFootSlipGrade.Bad
            || _diagnostics.Count > 0
            || HasPresetBlockingIssue(_presets);

        private static MxAnimationFootSlipGrade CalculateOverallGrade(IReadOnlyList<MxAnimationLocomotionPresetReport> presets)
        {
            var grade = MxAnimationFootSlipGrade.Ok;
            if (presets == null)
                return grade;

            for (int i = 0; i < presets.Count; i++)
            {
                MxAnimationLocomotionPresetReport preset = presets[i];
                if (preset == null)
                    continue;
                if (preset.Grade == MxAnimationFootSlipGrade.Bad || preset.HasBlockingIssue)
                    return MxAnimationFootSlipGrade.Bad;
                if (preset.Grade == MxAnimationFootSlipGrade.Warning)
                    grade = MxAnimationFootSlipGrade.Warning;
            }

            return grade;
        }

        private static bool HasPresetBlockingIssue(IReadOnlyList<MxAnimationLocomotionPresetReport> presets)
        {
            if (presets == null)
                return false;

            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i] != null && presets[i].HasBlockingIssue)
                    return true;
            }

            return false;
        }
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
        public static string CreateSummary(MxAnimationLocomotionPresetSequenceReport report)
        {
            if (report == null)
                return "Locomotion Calibration Preset Report\nNo report.";

            var builder = new StringBuilder();
            builder.Append("Locomotion Calibration Preset Report").Append('\n');
            builder.Append("packageId: ").Append(report.PackageId).Append('\n');
            builder.Append("characterResourceId: ").Append(report.CharacterResourceId).Append('\n');
            builder.Append("animationSetId: ").Append(report.AnimationSetId).Append('\n');
            builder.Append("blendId: ").Append(report.BlendId).Append('\n');
            builder.Append("generatedAtUtc: ").Append(report.GeneratedAtUtc).Append('\n');
            builder.Append("overallGrade: ").Append(report.OverallGrade).Append('\n');
            builder.Append("presetCount: ").Append(report.Presets.Count).Append('\n');

            for (int i = 0; i < report.Presets.Count; i++)
            {
                MxAnimationLocomotionPresetReport preset = report.Presets[i];
                if (preset == null)
                    continue;

                builder.Append('\n')
                    .Append("- ").Append(preset.DisplayName)
                    .Append(" [").Append(preset.Grade).Append(']')
                    .Append(" samples=").Append(preset.SampleCount)
                    .Append(" avgVelocityError=").Append(FormatFloat(preset.AverageVelocityErrorRatio))
                    .Append(" maxSlipCm=").Append(FormatFloat(preset.MaxSlipDistanceCm))
                    .Append(" maxSlipCmPerSecond=").Append(FormatFloat(preset.MaxFootSlipCmPerSecond))
                    .Append(" dominant=").Append(string.IsNullOrWhiteSpace(preset.DominantClipId) ? "-" : preset.DominantClipId)
                    .Append(" suggestedPlaybackSpeed=").Append(FormatFloat(preset.SuggestedPlaybackSpeed));

                if (preset.UnreachablePointCount > 0 || preset.ResourceErrorCount > 0 || preset.BackendErrorCount > 0)
                {
                    builder.Append('\n')
                        .Append("  issues: unreachable=").Append(preset.UnreachablePointCount)
                        .Append(" resourceErrors=").Append(preset.ResourceErrorCount)
                        .Append(" backendErrors=").Append(preset.BackendErrorCount);
                }

                if (preset.SuggestedFields.Count > 0)
                {
                    builder.Append('\n').Append("  suggestedFields:");
                    for (int j = 0; j < preset.SuggestedFields.Count; j++)
                        builder.Append(' ').Append(preset.SuggestedFields[j]);
                }

                if (preset.Diagnostics.Count > 0)
                {
                    builder.Append('\n').Append("  diagnostics:");
                    for (int j = 0; j < preset.Diagnostics.Count; j++)
                        builder.Append('\n').Append("    - ").Append(preset.Diagnostics[j]);
                }
            }

            if (report.Diagnostics.Count > 0)
            {
                builder.Append('\n').Append('\n').Append("Report diagnostics:");
                for (int i = 0; i < report.Diagnostics.Count; i++)
                    builder.Append('\n').Append("- ").Append(report.Diagnostics[i]);
            }

            return builder.ToString().TrimEnd();
        }

        public static string CreateJson(MxAnimationLocomotionPresetSequenceReport report)
        {
            if (report == null)
                return "{}";

            var builder = new StringBuilder();
            builder.Append('{');
            AppendJsonProperty(builder, "format", "mx.locomotionCalibrationPresetReport.v1", trailingComma: true);
            AppendJsonProperty(builder, "packageId", report.PackageId, trailingComma: true);
            AppendJsonProperty(builder, "characterResourceId", report.CharacterResourceId, trailingComma: true);
            AppendJsonProperty(builder, "animationSetId", report.AnimationSetId, trailingComma: true);
            AppendJsonProperty(builder, "blendId", report.BlendId, trailingComma: true);
            AppendJsonProperty(builder, "generatedAtUtc", report.GeneratedAtUtc, trailingComma: true);
            AppendJsonProperty(builder, "overallGrade", report.OverallGrade.ToString(), trailingComma: true);
            builder.Append("\"presets\":[");
            for (int i = 0; i < report.Presets.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                AppendPresetJson(builder, report.Presets[i]);
            }
            builder.Append("],");
            builder.Append("\"diagnostics\":");
            AppendStringArray(builder, report.Diagnostics);
            builder.Append('}');
            return builder.ToString();
        }

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

        private static void AppendPresetJson(StringBuilder builder, MxAnimationLocomotionPresetReport preset)
        {
            if (preset == null)
            {
                builder.Append("{}");
                return;
            }

            builder.Append('{');
            AppendJsonProperty(builder, "presetId", preset.PresetId, trailingComma: true);
            AppendJsonProperty(builder, "displayName", preset.DisplayName, trailingComma: true);
            AppendJsonNumber(builder, "durationSeconds", preset.DurationSeconds, trailingComma: true);
            AppendJsonNumber(builder, "sampleCount", preset.SampleCount, trailingComma: true);
            AppendJsonNumber(builder, "averageVelocityErrorRatio", preset.AverageVelocityErrorRatio, trailingComma: true);
            AppendJsonNumber(builder, "maxSlipDistanceCm", preset.MaxSlipDistanceCm, trailingComma: true);
            AppendJsonNumber(builder, "maxFootSlipCmPerSecond", preset.MaxFootSlipCmPerSecond, trailingComma: true);
            AppendJsonProperty(builder, "grade", preset.Grade.ToString(), trailingComma: true);
            AppendJsonProperty(builder, "dominantClipId", preset.DominantClipId, trailingComma: true);
            AppendJsonNumber(builder, "unreachablePointCount", preset.UnreachablePointCount, trailingComma: true);
            AppendJsonNumber(builder, "resourceErrorCount", preset.ResourceErrorCount, trailingComma: true);
            AppendJsonNumber(builder, "backendErrorCount", preset.BackendErrorCount, trailingComma: true);
            AppendJsonNumber(builder, "suggestedPlaybackSpeed", preset.SuggestedPlaybackSpeed, trailingComma: true);
            builder.Append("\"diagnostics\":");
            AppendStringArray(builder, preset.Diagnostics);
            builder.Append(',');
            builder.Append("\"suggestedFields\":");
            AppendStringArray(builder, preset.SuggestedFields);
            builder.Append('}');
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, string value, bool trailingComma)
        {
            builder.Append('"').Append(EscapeJson(name)).Append("\":\"")
                .Append(EscapeJson(value)).Append('"');
            if (trailingComma)
                builder.Append(',');
        }

        private static void AppendJsonNumber(StringBuilder builder, string name, float value, bool trailingComma)
        {
            builder.Append('"').Append(EscapeJson(name)).Append("\":")
                .Append(FormatFloat(value));
            if (trailingComma)
                builder.Append(',');
        }

        private static void AppendJsonNumber(StringBuilder builder, string name, int value, bool trailingComma)
        {
            builder.Append('"').Append(EscapeJson(name)).Append("\":")
                .Append(value.ToString(CultureInfo.InvariantCulture));
            if (trailingComma)
                builder.Append(',');
        }

        private static void AppendStringArray(StringBuilder builder, IReadOnlyList<string> values)
        {
            builder.Append('[');
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    if (i > 0)
                        builder.Append(',');
                    builder.Append('"').Append(EscapeJson(values[i])).Append('"');
                }
            }
            builder.Append(']');
        }

        private static string FormatFloat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = 0f;
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
