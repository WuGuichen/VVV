using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.Resources;

namespace MxFramework.CharacterRuntimeSpawn
{
    public sealed class CharacterWeaponTraceBakeCatalog
    {
        private readonly Dictionary<CharacterWeaponTraceBakeKey, MxAnimationBakeArtifact> _artifacts =
            new Dictionary<CharacterWeaponTraceBakeKey, MxAnimationBakeArtifact>();
        private readonly Dictionary<CharacterWeaponTraceBakeKey, FrameIndex> _frameIndexes =
            new Dictionary<CharacterWeaponTraceBakeKey, FrameIndex>();

        public IEnumerable<KeyValuePair<CharacterWeaponTraceBakeKey, MxAnimationBakeArtifact>> EnumerateArtifacts() => _artifacts;

        private sealed class FrameIndex
        {
            internal readonly int First;
            internal readonly MxAnimationBakedWeaponTraceFrame[] Dense;
            internal readonly Dictionary<int, MxAnimationBakedWeaponTraceFrame> Sparse;

            internal FrameIndex(IReadOnlyList<MxAnimationBakedWeaponTraceFrame> frames)
            {
                if (frames.Count == 0) { Dense = Array.Empty<MxAnimationBakedWeaponTraceFrame>(); return; }
                First = frames[0].LocalFrame;
                long length = (long)frames[frames.Count - 1].LocalFrame - First + 1;
                if (length > 0 && length <= Math.Max(32L, frames.Count * 8L))
                {
                    Dense = new MxAnimationBakedWeaponTraceFrame[(int)length];
                    for (int i = 0; i < frames.Count; i++)
                        if (Dense[frames[i].LocalFrame - First] == null) Dense[frames[i].LocalFrame - First] = frames[i];
                }
                else
                {
                    Sparse = new Dictionary<int, MxAnimationBakedWeaponTraceFrame>(frames.Count);
                    for (int i = 0; i < frames.Count; i++)
                        if (!Sparse.ContainsKey(frames[i].LocalFrame)) Sparse.Add(frames[i].LocalFrame, frames[i]);
                }
            }

            internal bool TryGet(int frame, out MxAnimationBakedWeaponTraceFrame value)
            {
                if (Sparse != null) return Sparse.TryGetValue(frame, out value);
                long index = (long)frame - First;
                value = index >= 0 && index < Dense.Length ? Dense[(int)index] : null;
                return value != null;
            }
        }

        public int Count => _artifacts.Count;

        public void RegisterArtifact(string clipKey, int traceId, MxAnimationBakeArtifact artifact)
        {
            if (artifact == null)
                throw new ArgumentNullException(nameof(artifact));
            if (string.IsNullOrWhiteSpace(clipKey))
                throw new ArgumentException("Clip key is required.", nameof(clipKey));
            if ((traceId <= 0))
                throw new ArgumentException("Trace id is required.", nameof(traceId));

            _artifacts[new CharacterWeaponTraceBakeKey(clipKey, traceId)] = artifact;
            _frameIndexes[new CharacterWeaponTraceBakeKey(clipKey, traceId)] = new FrameIndex(artifact.WeaponTraceFrames);
        }

        public bool TryGetArtifact(string clipKey, int traceId, out MxAnimationBakeArtifact artifact)
        {
            return _artifacts.TryGetValue(new CharacterWeaponTraceBakeKey(clipKey, traceId), out artifact);
        }

        public bool TryGetFrame(
            string clipKey,
            int traceId,
            int localFrame,
            out MxAnimationBakedWeaponTraceFrame frame)
        {
            frame = null;
            if (localFrame < 0 || !_frameIndexes.TryGetValue(new CharacterWeaponTraceBakeKey(clipKey, traceId), out FrameIndex index))
                return false;
            return index.TryGet(localFrame, out frame);
        }

        public bool TryGetSocketFrame(
            string clipKey,
            int traceId,
            int socketId,
            int localFrame,
            out MxAnimationBakedSocketFrame frame)
        {
            frame = null;
            if (localFrame < 0
                || socketId <= 0
                || !TryGetArtifact(clipKey, traceId, out MxAnimationBakeArtifact artifact))
            {
                return false;
            }

            IReadOnlyList<MxAnimationBakedSocketFrame> frames = artifact.SocketFrames;
            for (int i = 0; i < frames.Count; i++)
            {
                MxAnimationBakedSocketFrame candidate = frames[i];
                if (candidate.LocalFrame == localFrame
                    && candidate.SocketId == socketId)
                {
                    frame = candidate;
                    return true;
                }
            }

            return false;
        }
    }

    public static class CharacterWeaponTraceBakeArtifactBuilder
    {
        public const int DefaultQuantizationScale = 1000000;

        /// <summary>
        /// LowFreqAlloc: creates a package-local bake artifact whose weapon trace frames store authored root/tip endpoints directly.
        /// </summary>
        /// <remarks>
        /// RootPrev/TipPrev/RootNow/TipNow represent character-local blade endpoints. Runtime code must not reconstruct tip from Length.
        /// </remarks>
        public static MxAnimationBakeArtifact CreateAuthoredEndpointArtifact(
            string clipKey,
            int traceId,
            int numericTraceId,
            CharacterWeaponTraceRuntimeBinding traceBinding,
            CharacterWeaponTraceBakeSampleOptions options)
        {
            if (traceBinding == null)
                throw new ArgumentNullException(nameof(traceBinding));

            CharacterWeaponTraceBakeSampleOptions safeOptions = options ?? CharacterWeaponTraceBakeSampleOptions.Default;
            int maxFrame = Math.Max(1, safeOptions.MaxFrame);
            int quantizationScale = safeOptions.QuantizationScale <= 0
                ? DefaultQuantizationScale
                : safeOptions.QuantizationScale;
            string safeClipKey = clipKey ?? string.Empty;
            int safeTraceId = traceId > 0 ? traceId : traceBinding.TraceId;

            var profile = new MxAnimationBakeProfile(
                string.IsNullOrWhiteSpace(safeOptions.ProfileId)
                    ? "character.weaponTraceBake." + StableSuffix(safeClipKey) + "." + StableSuffix(traceBinding.TraceLabel)
                    : safeOptions.ProfileId,
                new ResourceKey(safeClipKey, "AnimationClip"),
                safeOptions.SourceClipHash,
                safeOptions.SkeletonProfileId,
                safeOptions.SkeletonProfileHash,
                safeOptions.SampleTickRate <= 0 ? 60 : safeOptions.SampleTickRate,
                quantizationScale,
                MxAnimationBakeCoordinateSpace.Local,
                MxAnimationBakeRoundingPolicy.RoundNearest,
                CreateEndpointFingerprint(traceBinding));

            var frames = new List<MxAnimationBakedWeaponTraceFrame>(maxFrame + 1);
            CharacterRuntimeVector3 root = traceBinding.StartPose.Position;
            CharacterRuntimeVector3 tip = traceBinding.EndPose.Position;
            MxAnimationBakedVector3 bakedRoot = Bake(root, quantizationScale);
            MxAnimationBakedVector3 bakedTip = Bake(tip, quantizationScale);
            int socketId = ResolveSocketId(traceBinding);
            for (int frame = 0; frame <= maxFrame; frame++)
            {
                frames.Add(new MxAnimationBakedWeaponTraceFrame(
                    frame,
                    Math.Max(0, numericTraceId),
                    socketId,
                    bakedRoot,
                    bakedTip,
                    bakedRoot,
                    bakedTip));
            }

            return new MxAnimationBakeArtifact(
                profile,
                frames,
                rootMotionFrames: null,
                eventMarkers: null,
                artifactHash: string.Empty);
        }

        public static MxAnimationBakeArtifact CreateAuthoredLocatorSweepArtifact(
            string clipKey,
            int traceId,
            int numericTraceId,
            CharacterWeaponTraceRuntimeBinding traceBinding,
            CharacterWeaponTraceBakeSampleOptions options)
        {
            if (traceBinding == null)
                throw new ArgumentNullException(nameof(traceBinding));

            CharacterWeaponTraceBakeSampleOptions safeOptions = options ?? CharacterWeaponTraceBakeSampleOptions.Default;
            int maxFrame = Math.Max(1, safeOptions.MaxFrame);
            int activeStartFrame = Math.Max(0, safeOptions.ActiveStartFrame);
            int activeEndFrame = Math.Max(activeStartFrame + 1, safeOptions.ActiveEndFrame);
            int quantizationScale = safeOptions.QuantizationScale <= 0
                ? DefaultQuantizationScale
                : safeOptions.QuantizationScale;
            string safeClipKey = clipKey ?? string.Empty;
            int safeTraceId = traceId > 0 ? traceId : traceBinding.TraceId;

            var profile = new MxAnimationBakeProfile(
                string.IsNullOrWhiteSpace(safeOptions.ProfileId)
                    ? "character.weaponTraceBake." + StableSuffix(safeClipKey) + "." + StableSuffix(traceBinding.TraceLabel)
                    : safeOptions.ProfileId,
                new ResourceKey(safeClipKey, "AnimationClip"),
                safeOptions.SourceClipHash,
                safeOptions.SkeletonProfileId,
                safeOptions.SkeletonProfileHash,
                safeOptions.SampleTickRate <= 0 ? 60 : safeOptions.SampleTickRate,
                quantizationScale,
                MxAnimationBakeCoordinateSpace.Local,
                MxAnimationBakeRoundingPolicy.RoundNearest,
                CreateImportFingerprint(traceBinding, safeOptions));

            var frames = new List<MxAnimationBakedWeaponTraceFrame>(maxFrame + 1);
            CharacterRuntimeVector3 previousRoot = RootAt(traceBinding, safeOptions, 0);
            CharacterRuntimeVector3 previousTip = TipAt(traceBinding, safeOptions, 0, activeStartFrame, activeEndFrame);
            int socketId = ResolveSocketId(traceBinding);
            for (int frame = 0; frame <= maxFrame; frame++)
            {
                CharacterRuntimeVector3 root = RootAt(traceBinding, safeOptions, frame);
                CharacterRuntimeVector3 tip = TipAt(traceBinding, safeOptions, frame, activeStartFrame, activeEndFrame);
                frames.Add(new MxAnimationBakedWeaponTraceFrame(
                    frame,
                    Math.Max(0, numericTraceId),
                    socketId,
                    Bake(previousRoot, quantizationScale),
                    Bake(previousTip, quantizationScale),
                    Bake(root, quantizationScale),
                    Bake(tip, quantizationScale)));
                previousRoot = root;
                previousTip = tip;
            }

            return new MxAnimationBakeArtifact(
                profile,
                frames,
                rootMotionFrames: null,
                eventMarkers: null,
                artifactHash: string.Empty);
        }

        private static CharacterRuntimeVector3 RootAt(
            CharacterWeaponTraceRuntimeBinding traceBinding,
            CharacterWeaponTraceBakeSampleOptions options,
            int frame)
        {
            CharacterRuntimeVector3 start = traceBinding.StartPose.Position;
            return new CharacterRuntimeVector3(
                options.SocketAnchorLocal.X + start.X,
                options.SocketAnchorLocal.Y + start.Y,
                options.SocketAnchorLocal.Z + start.Z);
        }

        private static CharacterRuntimeVector3 TipAt(
            CharacterWeaponTraceRuntimeBinding traceBinding,
            CharacterWeaponTraceBakeSampleOptions options,
            int frame,
            int activeStartFrame,
            int activeEndFrame)
        {
            CharacterRuntimeVector3 root = RootAt(traceBinding, options, frame);
            float bladeLength = Math.Max(options.MinimumBladeLength, Distance(traceBinding.StartPose.Position, traceBinding.EndPose.Position));
            float t = Clamp01((frame - activeStartFrame) / (float)Math.Max(1, activeEndFrame - activeStartFrame));
            float smoothed = t * t * (3f - 2f * t);
            double angle = Lerp(options.SweepStartDegrees, options.SweepEndDegrees, smoothed) * Math.PI / 180.0;
            return new CharacterRuntimeVector3(
                root.X + (float)Math.Sin(angle) * bladeLength,
                root.Y + options.TipHeightOffset,
                root.Z + (float)Math.Cos(angle) * bladeLength);
        }

        private static MxAnimationBakedVector3 Bake(CharacterRuntimeVector3 value, int quantizationScale)
        {
            return new MxAnimationBakedVector3(
                (long)Math.Round(value.X * quantizationScale),
                (long)Math.Round(value.Y * quantizationScale),
                (long)Math.Round(value.Z * quantizationScale));
        }

        private static string CreateImportFingerprint(
            CharacterWeaponTraceRuntimeBinding traceBinding,
            CharacterWeaponTraceBakeSampleOptions options)
        {
            return "trace="
                + (traceBinding.TraceId)
                + "|weapon="
                + (traceBinding.WeaponId)
                + "|startLocator="
                + (traceBinding.StartLocatorPath ?? string.Empty)
                + "|endLocator="
                + (traceBinding.EndLocatorPath ?? string.Empty)
                + "|sampleRule="
                + (traceBinding.SampleRule ?? string.Empty)
                + "|fixedSamples="
                + traceBinding.FixedSampleCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|proximalExtension="
                + traceBinding.ProximalExtension.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|proximalRadius="
                + traceBinding.ProximalRadius.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|sweep="
                + options.SweepStartDegrees.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ".."
                + options.SweepEndDegrees.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string CreateEndpointFingerprint(CharacterWeaponTraceRuntimeBinding traceBinding)
        {
            return "trace="
                + (traceBinding.TraceId)
                + "|weapon="
                + (traceBinding.WeaponId)
                + "|slot="
                + (traceBinding.EquipSlot)
                + "|startLocator="
                + (traceBinding.StartLocatorPath ?? string.Empty)
                + "|endLocator="
                + (traceBinding.EndLocatorPath ?? string.Empty)
                + "|startPose="
                + FormatVector(traceBinding.StartPose.Position)
                + "|endPose="
                + FormatVector(traceBinding.EndPose.Position)
                + "|sampleRule="
                + (traceBinding.SampleRule ?? string.Empty)
                + "|fixedSamples="
                + traceBinding.FixedSampleCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|proximalExtension="
                + traceBinding.ProximalExtension.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|proximalRadius="
                + traceBinding.ProximalRadius.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|source=authoredEndpoints";
        }

        private static int ResolveSocketId(CharacterWeaponTraceRuntimeBinding traceBinding)
        {
            if (traceBinding.SocketId > 0)
                return traceBinding.SocketId;
            if (traceBinding.EquipSlot > 0)
                return traceBinding.EquipSlot;
            return 1;
        }

        private static string FormatVector(CharacterRuntimeVector3 value)
        {
            return value.X.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ","
                + value.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ","
                + value.Z.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static float Distance(CharacterRuntimeVector3 left, CharacterRuntimeVector3 right)
        {
            float x = right.X - left.X;
            float y = right.Y - left.Y;
            float z = right.Z - left.Z;
            return (float)Math.Sqrt((x * x) + (y * y) + (z * z));
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }

        private static float Lerp(float from, float to, float t)
        {
            return from + ((to - from) * t);
        }

        private static string StableSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "none";

            var chars = new char[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                chars[i] = char.IsLetterOrDigit(c) ? c : '_';
            }

            return new string(chars);
        }
    }

    public sealed class CharacterWeaponTraceBakeSampleOptions
    {
        public static readonly CharacterWeaponTraceBakeSampleOptions Default =
            new CharacterWeaponTraceBakeSampleOptions();

        public string ProfileId { get; set; } = string.Empty;
        public string SourceClipHash { get; set; } = "generated";
        public string SkeletonProfileId { get; set; } = "character";
        public string SkeletonProfileHash { get; set; } = "generated";
        public int SampleTickRate { get; set; } = 60;
        public int QuantizationScale { get; set; } = CharacterWeaponTraceBakeArtifactBuilder.DefaultQuantizationScale;
        public int MaxFrame { get; set; } = 90;
        public int ActiveStartFrame { get; set; } = 18;
        public int ActiveEndFrame { get; set; } = 35;
        public float SweepStartDegrees { get; set; } = -55f;
        public float SweepEndDegrees { get; set; } = 55f;
        public float MinimumBladeLength { get; set; } = 0.01f;
        public float TipHeightOffset { get; set; } = 0.03f;
        public CharacterRuntimeVector3 SocketAnchorLocal { get; set; } =
            new CharacterRuntimeVector3(0.18f, 0.83f, 0.35f);
    }

    public readonly struct CharacterWeaponTraceBakeKey : IEquatable<CharacterWeaponTraceBakeKey>
    {
        public CharacterWeaponTraceBakeKey(string clipKey, int traceId)
        {
            ClipKey = clipKey ?? string.Empty;
            TraceId = traceId;
        }

        public string ClipKey { get; }
        public int TraceId { get; }

        public bool Equals(CharacterWeaponTraceBakeKey other)
        {
            return string.Equals(ClipKey, other.ClipKey, StringComparison.Ordinal)
                && (TraceId == other.TraceId);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterWeaponTraceBakeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(ClipKey) * 397)
                    ^ TraceId.GetHashCode();
            }
        }
    }
}
