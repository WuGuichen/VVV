using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Resources;

namespace MxFramework.Animation
{
    public enum MxAnimationBakeCoordinateSpace
    {
        Local = 0,
        NormalizedRig = 1
    }

    public enum MxAnimationBakeRoundingPolicy
    {
        RoundNearest = 0,
        Floor = 1,
        Ceiling = 2
    }

    public enum MxAnimationBakeEventKind
    {
        Marker = 0,
        Footstep = 1,
        PresentationEvent = 2
    }

    public enum MxAnimationBakeIssueSeverity
    {
        Error = 0,
        Warning = 1
    }

    public readonly struct MxAnimationBakedVector3 : IEquatable<MxAnimationBakedVector3>
    {
        public static readonly MxAnimationBakedVector3 Zero = new MxAnimationBakedVector3(0, 0, 0);

        public MxAnimationBakedVector3(long x, long y, long z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public long X { get; }
        public long Y { get; }
        public long Z { get; }

        public bool Equals(MxAnimationBakedVector3 other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is MxAnimationBakedVector3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return X.ToString(CultureInfo.InvariantCulture)
                + ","
                + Y.ToString(CultureInfo.InvariantCulture)
                + ","
                + Z.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class MxAnimationBakeProfile
    {
        public MxAnimationBakeProfile(
            string profileId,
            ResourceKey sourceClipKey,
            string sourceClipHash,
            string skeletonProfileId,
            string skeletonProfileHash,
            int sampleTickRate,
            int quantizationScale,
            MxAnimationBakeCoordinateSpace coordinateSpace,
            MxAnimationBakeRoundingPolicy roundingPolicy,
            string importSettingsFingerprint,
            string profileHash = "")
        {
            ProfileId = profileId ?? string.Empty;
            SourceClipKey = sourceClipKey;
            SourceClipHash = sourceClipHash ?? string.Empty;
            SkeletonProfileId = skeletonProfileId ?? string.Empty;
            SkeletonProfileHash = skeletonProfileHash ?? string.Empty;
            SampleTickRate = sampleTickRate;
            QuantizationScale = quantizationScale;
            CoordinateSpace = coordinateSpace;
            RoundingPolicy = roundingPolicy;
            ImportSettingsFingerprint = importSettingsFingerprint ?? string.Empty;
            ProfileHash = string.IsNullOrWhiteSpace(profileHash)
                ? MxAnimationBakeHasher.ComputeProfileHash(this)
                : profileHash;
        }

        public string ProfileId { get; }
        public ResourceKey SourceClipKey { get; }
        public string SourceClipHash { get; }
        public string SkeletonProfileId { get; }
        public string SkeletonProfileHash { get; }
        public int SampleTickRate { get; }
        public int QuantizationScale { get; }
        public MxAnimationBakeCoordinateSpace CoordinateSpace { get; }
        public MxAnimationBakeRoundingPolicy RoundingPolicy { get; }
        public string ImportSettingsFingerprint { get; }
        public string ProfileHash { get; }
    }

    public sealed class MxAnimationBakedWeaponTraceFrame
    {
        public MxAnimationBakedWeaponTraceFrame(
            int localFrame,
            int traceId,
            string socketId,
            MxAnimationBakedVector3 rootPrev,
            MxAnimationBakedVector3 tipPrev,
            MxAnimationBakedVector3 rootNow,
            MxAnimationBakedVector3 tipNow)
        {
            if (localFrame < 0)
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");
            if (traceId < 0)
                throw new ArgumentOutOfRangeException(nameof(traceId), "Trace id cannot be negative.");

            LocalFrame = localFrame;
            TraceId = traceId;
            SocketId = socketId ?? string.Empty;
            RootPrev = rootPrev;
            TipPrev = tipPrev;
            RootNow = rootNow;
            TipNow = tipNow;
        }

        public int LocalFrame { get; }
        public int TraceId { get; }
        public string SocketId { get; }
        public MxAnimationBakedVector3 RootPrev { get; }
        public MxAnimationBakedVector3 TipPrev { get; }
        public MxAnimationBakedVector3 RootNow { get; }
        public MxAnimationBakedVector3 TipNow { get; }
    }

    public sealed class MxAnimationBakedRootMotionFrame
    {
        public MxAnimationBakedRootMotionFrame(
            int localFrame,
            MxAnimationBakedVector3 rootPosition,
            MxAnimationBakedVector3 deltaPosition)
        {
            if (localFrame < 0)
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");

            LocalFrame = localFrame;
            RootPosition = rootPosition;
            DeltaPosition = deltaPosition;
        }

        public int LocalFrame { get; }
        public MxAnimationBakedVector3 RootPosition { get; }
        public MxAnimationBakedVector3 DeltaPosition { get; }
    }

    public sealed class MxAnimationBakedEventMarker
    {
        public MxAnimationBakedEventMarker(
            int localFrame,
            string eventId,
            MxAnimationBakeEventKind kind,
            ResourceKey payloadKey = default,
            int sourceOrder = 0)
        {
            if (localFrame < 0)
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");
            if (sourceOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceOrder), "Source order cannot be negative.");

            LocalFrame = localFrame;
            EventId = eventId ?? string.Empty;
            Kind = kind;
            PayloadKey = payloadKey;
            SourceOrder = sourceOrder;
        }

        public int LocalFrame { get; }
        public string EventId { get; }
        public MxAnimationBakeEventKind Kind { get; }
        public ResourceKey PayloadKey { get; }
        public int SourceOrder { get; }
    }

    public sealed class MxAnimationBakeArtifact
    {
        private readonly List<MxAnimationBakedWeaponTraceFrame> _weaponTraceFrames;
        private readonly List<MxAnimationBakedRootMotionFrame> _rootMotionFrames;
        private readonly List<MxAnimationBakedEventMarker> _eventMarkers;

        public MxAnimationBakeArtifact(
            MxAnimationBakeProfile profile,
            IEnumerable<MxAnimationBakedWeaponTraceFrame> weaponTraceFrames = null,
            IEnumerable<MxAnimationBakedRootMotionFrame> rootMotionFrames = null,
            IEnumerable<MxAnimationBakedEventMarker> eventMarkers = null,
            string artifactHash = "")
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _weaponTraceFrames = CopyAndSort(weaponTraceFrames, CompareWeaponTraceFrame);
            _rootMotionFrames = CopyAndSort(rootMotionFrames, CompareRootMotionFrame);
            _eventMarkers = CopyAndSort(eventMarkers, CompareEventMarker);
            ArtifactHash = string.IsNullOrWhiteSpace(artifactHash)
                ? MxAnimationBakeHasher.ComputeArtifactHash(this)
                : artifactHash;
        }

        public MxAnimationBakeProfile Profile { get; }
        public IReadOnlyList<MxAnimationBakedWeaponTraceFrame> WeaponTraceFrames => _weaponTraceFrames;
        public IReadOnlyList<MxAnimationBakedRootMotionFrame> RootMotionFrames => _rootMotionFrames;
        public IReadOnlyList<MxAnimationBakedEventMarker> EventMarkers => _eventMarkers;
        public string ArtifactHash { get; }

        private static List<T> CopyAndSort<T>(IEnumerable<T> source, Comparison<T> comparison)
            where T : class
        {
            var list = new List<T>();
            if (source != null)
            {
                foreach (T item in source)
                {
                    if (item != null)
                        list.Add(item);
                }
            }

            list.Sort(comparison);
            return list;
        }

        private static int CompareWeaponTraceFrame(MxAnimationBakedWeaponTraceFrame left, MxAnimationBakedWeaponTraceFrame right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int result = left.LocalFrame.CompareTo(right.LocalFrame);
            if (result != 0)
                return result;
            result = left.TraceId.CompareTo(right.TraceId);
            if (result != 0)
                return result;
            return string.CompareOrdinal(left.SocketId, right.SocketId);
        }

        private static int CompareRootMotionFrame(MxAnimationBakedRootMotionFrame left, MxAnimationBakedRootMotionFrame right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            return left.LocalFrame.CompareTo(right.LocalFrame);
        }

        private static int CompareEventMarker(MxAnimationBakedEventMarker left, MxAnimationBakedEventMarker right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int result = left.LocalFrame.CompareTo(right.LocalFrame);
            if (result != 0)
                return result;
            result = left.SourceOrder.CompareTo(right.SourceOrder);
            if (result != 0)
                return result;
            return string.CompareOrdinal(left.EventId, right.EventId);
        }
    }

    public sealed class MxAnimationBakeExpectation
    {
        public MxAnimationBakeExpectation(
            string sourceClipHash = "",
            string profileHash = "",
            string skeletonProfileHash = "",
            string artifactHash = "")
        {
            SourceClipHash = sourceClipHash ?? string.Empty;
            ProfileHash = profileHash ?? string.Empty;
            SkeletonProfileHash = skeletonProfileHash ?? string.Empty;
            ArtifactHash = artifactHash ?? string.Empty;
        }

        public string SourceClipHash { get; }
        public string ProfileHash { get; }
        public string SkeletonProfileHash { get; }
        public string ArtifactHash { get; }
    }

    public sealed class MxAnimationBakeIssue
    {
        public MxAnimationBakeIssue(
            MxAnimationBakeIssueSeverity severity,
            string code,
            string field,
            string expected,
            string actual,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Field = field ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public MxAnimationBakeIssueSeverity Severity { get; }
        public string Code { get; }
        public string Field { get; }
        public string Expected { get; }
        public string Actual { get; }
        public string Message { get; }
    }

    public sealed class MxAnimationBakeValidationReport
    {
        private readonly List<MxAnimationBakeIssue> _issues = new List<MxAnimationBakeIssue>();

        public IReadOnlyList<MxAnimationBakeIssue> Issues => _issues;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public bool HasErrors => ErrorCount > 0;

        public void AddError(string code, string field, string expected, string actual, string message)
        {
            Add(MxAnimationBakeIssueSeverity.Error, code, field, expected, actual, message);
        }

        public void AddWarning(string code, string field, string expected, string actual, string message)
        {
            Add(MxAnimationBakeIssueSeverity.Warning, code, field, expected, actual, message);
        }

        private void Add(MxAnimationBakeIssueSeverity severity, string code, string field, string expected, string actual, string message)
        {
            _issues.Add(new MxAnimationBakeIssue(severity, code, field, expected, actual, message));
            if (severity == MxAnimationBakeIssueSeverity.Error)
                ErrorCount++;
            else
                WarningCount++;
        }
    }

    public static class MxAnimationBakeArtifactValidator
    {
        public static MxAnimationBakeValidationReport Validate(
            MxAnimationBakeArtifact artifact,
            MxAnimationBakeExpectation expectation = null)
        {
            var report = new MxAnimationBakeValidationReport();
            if (artifact == null)
            {
                report.AddError("BakeArtifactMissing", "artifact", "non-null", "null", "Bake artifact is missing.");
                return report;
            }

            ValidateProfile(artifact.Profile, report);

            string actualProfileHash = MxAnimationBakeHasher.ComputeProfileHash(artifact.Profile);
            if (!string.Equals(artifact.Profile.ProfileHash, actualProfileHash, StringComparison.Ordinal))
            {
                report.AddError("BakeProfileHashMismatch", "profileHash", artifact.Profile.ProfileHash, actualProfileHash, "Bake profile hash does not match profile contents.");
            }

            string actualArtifactHash = MxAnimationBakeHasher.ComputeArtifactHash(artifact);
            if (!string.Equals(artifact.ArtifactHash, actualArtifactHash, StringComparison.Ordinal))
            {
                report.AddError("BakeArtifactHashMismatch", "artifactHash", artifact.ArtifactHash, actualArtifactHash, "Bake artifact hash does not match artifact contents.");
            }

            ValidateDuplicateTraceFrames(artifact, report);

            if (expectation != null)
                ValidateExpectation(artifact, expectation, report);

            return report;
        }

        private static void ValidateProfile(MxAnimationBakeProfile profile, MxAnimationBakeValidationReport report)
        {
            if (profile == null)
            {
                report.AddError("BakeProfileMissing", "profile", "non-null", "null", "Bake profile is missing.");
                return;
            }

            if (!profile.SourceClipKey.IsValid)
                report.AddError("BakeSourceClipKeyMissing", "sourceClipKey", "valid ResourceKey", profile.SourceClipKey.ToString(), "Bake source clip key is missing or invalid.");
            if (string.IsNullOrWhiteSpace(profile.SourceClipHash))
                report.AddError("BakeSourceClipHashMissing", "sourceClipHash", "non-empty", profile.SourceClipHash, "Bake source clip hash is required.");
            if (string.IsNullOrWhiteSpace(profile.ProfileId))
                report.AddError("BakeProfileIdMissing", "profileId", "non-empty", profile.ProfileId, "Bake profile id is required.");
            if (string.IsNullOrWhiteSpace(profile.ProfileHash))
                report.AddError("BakeProfileHashMissing", "profileHash", "non-empty", profile.ProfileHash, "Bake profile hash is required.");
            if (string.IsNullOrWhiteSpace(profile.SkeletonProfileHash))
                report.AddWarning("BakeSkeletonProfileHashMissing", "skeletonProfileHash", "non-empty", profile.SkeletonProfileHash, "Skeleton profile hash is recommended for stale-artifact diagnostics.");
            if (profile.SampleTickRate <= 0)
                report.AddError("BakeSampleTickRateInvalid", "sampleTickRate", "> 0", profile.SampleTickRate.ToString(CultureInfo.InvariantCulture), "Sample tick rate must be positive.");
            if (profile.QuantizationScale <= 0)
                report.AddError("BakeQuantizationScaleInvalid", "quantizationScale", "> 0", profile.QuantizationScale.ToString(CultureInfo.InvariantCulture), "Quantization scale must be positive.");
        }

        private static void ValidateDuplicateTraceFrames(MxAnimationBakeArtifact artifact, MxAnimationBakeValidationReport report)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < artifact.WeaponTraceFrames.Count; i++)
            {
                MxAnimationBakedWeaponTraceFrame frame = artifact.WeaponTraceFrames[i];
                string key = frame.LocalFrame.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + frame.TraceId.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + frame.SocketId;
                if (!keys.Add(key))
                    report.AddError("BakeDuplicateWeaponTraceFrame", "weaponTraceFrames", "unique localFrame/traceId/socketId", key, "Duplicate baked weapon trace frame.");
            }
        }

        private static void ValidateExpectation(
            MxAnimationBakeArtifact artifact,
            MxAnimationBakeExpectation expectation,
            MxAnimationBakeValidationReport report)
        {
            CompareExpected("BakeSourceClipHashMismatch", "sourceClipHash", expectation.SourceClipHash, artifact.Profile.SourceClipHash, report);
            CompareExpected("BakeProfileHashExpectedMismatch", "profileHash", expectation.ProfileHash, artifact.Profile.ProfileHash, report);
            CompareExpected("BakeSkeletonProfileHashMismatch", "skeletonProfileHash", expectation.SkeletonProfileHash, artifact.Profile.SkeletonProfileHash, report);
            CompareExpected("BakeArtifactHashExpectedMismatch", "artifactHash", expectation.ArtifactHash, artifact.ArtifactHash, report);
        }

        private static void CompareExpected(
            string code,
            string field,
            string expected,
            string actual,
            MxAnimationBakeValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(expected))
                return;
            if (string.Equals(expected, actual, StringComparison.Ordinal))
                return;

            report.AddError(code, field, expected, actual, "Bake artifact expectation mismatch.");
        }
    }

    public static class MxAnimationBakeQuantizer
    {
        public static long Quantize(double value, int scale, MxAnimationBakeRoundingPolicy policy)
        {
            if (scale <= 0)
                throw new ArgumentOutOfRangeException(nameof(scale), "Quantization scale must be positive.");

            double scaled = value * scale;
            switch (policy)
            {
                case MxAnimationBakeRoundingPolicy.Floor:
                    return checked((long)Math.Floor(scaled));
                case MxAnimationBakeRoundingPolicy.Ceiling:
                    return checked((long)Math.Ceiling(scaled));
                default:
                    return checked((long)Math.Round(scaled, MidpointRounding.AwayFromZero));
            }
        }

        public static MxAnimationBakedVector3 QuantizeVector3(
            double x,
            double y,
            double z,
            int scale,
            MxAnimationBakeRoundingPolicy policy)
        {
            return new MxAnimationBakedVector3(
                Quantize(x, scale, policy),
                Quantize(y, scale, policy),
                Quantize(z, scale, policy));
        }
    }

    public static class MxAnimationBakeHasher
    {
        public const string HashPrefix = "sha256:";

        public static string ComputeProfileHash(MxAnimationBakeProfile profile)
        {
            if (profile == null)
                return HashPrefix + Sha256Hex(string.Empty);

            var builder = new StringBuilder();
            builder.Append("mxanimation.bake.profile.v1\n");
            Append(builder, "profileId", profile.ProfileId);
            AppendResourceKey(builder, "sourceClip", profile.SourceClipKey);
            Append(builder, "skeletonProfileId", profile.SkeletonProfileId);
            Append(builder, "skeletonProfileHash", profile.SkeletonProfileHash);
            Append(builder, "sampleTickRate", profile.SampleTickRate.ToString(CultureInfo.InvariantCulture));
            Append(builder, "quantizationScale", profile.QuantizationScale.ToString(CultureInfo.InvariantCulture));
            Append(builder, "coordinateSpace", profile.CoordinateSpace.ToString());
            Append(builder, "roundingPolicy", profile.RoundingPolicy.ToString());
            Append(builder, "importSettingsFingerprint", profile.ImportSettingsFingerprint);
            return HashPrefix + Sha256Hex(builder.ToString());
        }

        public static string ComputeArtifactHash(MxAnimationBakeArtifact artifact)
        {
            if (artifact == null)
                return HashPrefix + Sha256Hex(string.Empty);

            var builder = new StringBuilder();
            builder.Append("mxanimation.bake.artifact.v1\n");
            Append(builder, "profileHash", artifact.Profile.ProfileHash);
            Append(builder, "sourceClipHash", artifact.Profile.SourceClipHash);

            for (int i = 0; i < artifact.WeaponTraceFrames.Count; i++)
            {
                MxAnimationBakedWeaponTraceFrame frame = artifact.WeaponTraceFrames[i];
                builder.Append("weapon[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]\n");
                Append(builder, "localFrame", frame.LocalFrame.ToString(CultureInfo.InvariantCulture));
                Append(builder, "traceId", frame.TraceId.ToString(CultureInfo.InvariantCulture));
                Append(builder, "socketId", frame.SocketId);
                Append(builder, "rootPrev", frame.RootPrev.ToString());
                Append(builder, "tipPrev", frame.TipPrev.ToString());
                Append(builder, "rootNow", frame.RootNow.ToString());
                Append(builder, "tipNow", frame.TipNow.ToString());
            }

            for (int i = 0; i < artifact.RootMotionFrames.Count; i++)
            {
                MxAnimationBakedRootMotionFrame frame = artifact.RootMotionFrames[i];
                builder.Append("root[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]\n");
                Append(builder, "localFrame", frame.LocalFrame.ToString(CultureInfo.InvariantCulture));
                Append(builder, "rootPosition", frame.RootPosition.ToString());
                Append(builder, "deltaPosition", frame.DeltaPosition.ToString());
            }

            for (int i = 0; i < artifact.EventMarkers.Count; i++)
            {
                MxAnimationBakedEventMarker marker = artifact.EventMarkers[i];
                builder.Append("event[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]\n");
                Append(builder, "localFrame", marker.LocalFrame.ToString(CultureInfo.InvariantCulture));
                Append(builder, "sourceOrder", marker.SourceOrder.ToString(CultureInfo.InvariantCulture));
                Append(builder, "eventId", marker.EventId);
                Append(builder, "kind", marker.Kind.ToString());
                AppendResourceKey(builder, "payload", marker.PayloadKey);
            }

            return HashPrefix + Sha256Hex(builder.ToString());
        }

        private static void Append(StringBuilder builder, string name, string value)
        {
            builder.Append(name).Append('=').Append(value ?? string.Empty).Append('\n');
        }

        private static void AppendResourceKey(StringBuilder builder, string name, ResourceKey key)
        {
            builder.Append(name).Append(".id=").Append(key.Id ?? string.Empty).Append('\n');
            builder.Append(name).Append(".type=").Append(key.TypeId ?? string.Empty).Append('\n');
            builder.Append(name).Append(".variant=").Append(key.Variant ?? string.Empty).Append('\n');
            builder.Append(name).Append(".package=").Append(key.PackageId ?? string.Empty).Append('\n');
        }

        private static string Sha256Hex(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }
}
