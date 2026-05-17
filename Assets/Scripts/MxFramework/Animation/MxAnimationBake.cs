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

    public enum MxAnimationBakeDataPurpose
    {
        CombatReferenceInput = 0,
        AuthoringPreview = 1,
        TimelineAlignment = 2,
        Diagnostics = 3
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

    public sealed class MxAnimationBakedSocketFrame
    {
        public MxAnimationBakedSocketFrame(
            int localFrame,
            string socketId,
            string socketPath,
            MxAnimationBakedVector3 position,
            MxAnimationBakedVector3 deltaPosition)
        {
            if (localFrame < 0)
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");

            LocalFrame = localFrame;
            SocketId = socketId ?? string.Empty;
            SocketPath = socketPath ?? string.Empty;
            Position = position;
            DeltaPosition = deltaPosition;
        }

        public int LocalFrame { get; }
        public string SocketId { get; }
        public string SocketPath { get; }
        public MxAnimationBakedVector3 Position { get; }
        public MxAnimationBakedVector3 DeltaPosition { get; }
    }

    public sealed class MxAnimationBakedEventMarker
    {
        public MxAnimationBakedEventMarker(
            int localFrame,
            string eventId,
            MxAnimationBakeEventKind kind,
            ResourceKey payloadKey,
            int sourceOrder)
            : this(localFrame, eventId, kind, payloadKey, sourceOrder, -1, -1)
        {
        }

        public MxAnimationBakedEventMarker(
            int localFrame,
            string eventId,
            MxAnimationBakeEventKind kind,
            ResourceKey payloadKey = default,
            int sourceOrder = 0,
            int presentationFrame = -1,
            int combatFrame = -1)
        {
            if (localFrame < 0)
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");
            if (sourceOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceOrder), "Source order cannot be negative.");
            if (presentationFrame < -1)
                throw new ArgumentOutOfRangeException(nameof(presentationFrame), "Presentation frame cannot be less than -1.");
            if (combatFrame < -1)
                throw new ArgumentOutOfRangeException(nameof(combatFrame), "Combat frame cannot be less than -1.");

            LocalFrame = localFrame;
            EventId = eventId ?? string.Empty;
            Kind = kind;
            PayloadKey = payloadKey;
            SourceOrder = sourceOrder;
            PresentationFrame = presentationFrame;
            CombatFrame = combatFrame;
        }

        public int LocalFrame { get; }
        public string EventId { get; }
        public MxAnimationBakeEventKind Kind { get; }
        public ResourceKey PayloadKey { get; }
        public int SourceOrder { get; }
        public int PresentationFrame { get; }
        public int CombatFrame { get; }
    }

    public sealed class MxAnimationBakeArtifact
    {
        private readonly List<MxAnimationBakedWeaponTraceFrame> _weaponTraceFrames;
        private readonly List<MxAnimationBakedRootMotionFrame> _rootMotionFrames;
        private readonly List<MxAnimationBakedSocketFrame> _socketFrames;
        private readonly List<MxAnimationBakedEventMarker> _eventMarkers;

        public MxAnimationBakeArtifact(
            MxAnimationBakeProfile profile,
            IEnumerable<MxAnimationBakedWeaponTraceFrame> weaponTraceFrames,
            IEnumerable<MxAnimationBakedRootMotionFrame> rootMotionFrames,
            IEnumerable<MxAnimationBakedEventMarker> eventMarkers,
            string artifactHash)
            : this(profile, weaponTraceFrames, rootMotionFrames, eventMarkers, artifactHash, null)
        {
        }

        public MxAnimationBakeArtifact(
            MxAnimationBakeProfile profile,
            IEnumerable<MxAnimationBakedWeaponTraceFrame> weaponTraceFrames = null,
            IEnumerable<MxAnimationBakedRootMotionFrame> rootMotionFrames = null,
            IEnumerable<MxAnimationBakedEventMarker> eventMarkers = null,
            string artifactHash = "",
            IEnumerable<MxAnimationBakedSocketFrame> socketFrames = null)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _weaponTraceFrames = CopyAndSort(weaponTraceFrames, CompareWeaponTraceFrame);
            _rootMotionFrames = CopyAndSort(rootMotionFrames, CompareRootMotionFrame);
            _socketFrames = CopyAndSort(socketFrames, CompareSocketFrame);
            _eventMarkers = CopyAndSort(eventMarkers, CompareEventMarker);
            ArtifactHash = string.IsNullOrWhiteSpace(artifactHash)
                ? MxAnimationBakeHasher.ComputeArtifactHash(this)
                : artifactHash;
        }

        public MxAnimationBakeProfile Profile { get; }
        public IReadOnlyList<MxAnimationBakedWeaponTraceFrame> WeaponTraceFrames => _weaponTraceFrames;
        public IReadOnlyList<MxAnimationBakedRootMotionFrame> RootMotionFrames => _rootMotionFrames;
        public IReadOnlyList<MxAnimationBakedSocketFrame> SocketFrames => _socketFrames;
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

        private static int CompareSocketFrame(MxAnimationBakedSocketFrame left, MxAnimationBakedSocketFrame right)
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
            result = string.CompareOrdinal(left.SocketId, right.SocketId);
            if (result != 0)
                return result;
            return string.CompareOrdinal(left.SocketPath, right.SocketPath);
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
            string artifactHash = "",
            MxAnimationCompatibilityExpectation compatibilityExpectation = null)
        {
            SourceClipHash = sourceClipHash ?? string.Empty;
            ProfileHash = profileHash ?? string.Empty;
            SkeletonProfileHash = skeletonProfileHash ?? string.Empty;
            ArtifactHash = artifactHash ?? string.Empty;
            CompatibilityExpectation = compatibilityExpectation;
        }

        public string SourceClipHash { get; }
        public string ProfileHash { get; }
        public string SkeletonProfileHash { get; }
        public string ArtifactHash { get; }
        public MxAnimationCompatibilityExpectation CompatibilityExpectation { get; }
    }

    public enum MxAnimationCompatibilityIssueSeverity
    {
        Error = 0,
        Warning = 1
    }

    public static class MxAnimationCompatibilityIssueCodes
    {
        public const string CompatibilityProfileMissing = "CompatibilityProfileMissing";
        public const string SkeletonProfileMissing = "SkeletonProfileMissing";
        public const string SkeletonProfileIdMismatch = "SkeletonProfileIdMismatch";
        public const string SkeletonProfileHashMismatch = "SkeletonProfileHashMismatch";
        public const string BonePathMissing = "BonePathMissing";
        public const string SocketPathMissing = "SocketPathMissing";
        public const string ClipProfileMissing = "ClipProfileMissing";
        public const string ClipKeyInvalid = "ClipKeyInvalid";
        public const string ClipSkeletonProfileIdMismatch = "ClipSkeletonProfileIdMismatch";
        public const string ClipSkeletonProfileHashMismatch = "ClipSkeletonProfileHashMismatch";
        public const string ClipBindingPathMissing = "ClipBindingPathMissing";
        public const string AvatarMaskProfileMissing = "AvatarMaskProfileMissing";
        public const string AvatarMaskKeyInvalid = "AvatarMaskKeyInvalid";
        public const string AvatarMaskSkeletonProfileIdMismatch = "AvatarMaskSkeletonProfileIdMismatch";
        public const string AvatarMaskSkeletonProfileHashMismatch = "AvatarMaskSkeletonProfileHashMismatch";
        public const string AvatarMaskPathMissing = "AvatarMaskPathMissing";
        public const string BakeArtifactMissing = "BakeArtifactMissing";
        public const string BakeArtifactSkeletonProfileIdMismatch = "BakeArtifactSkeletonProfileIdMismatch";
        public const string BakeArtifactSkeletonProfileHashMismatch = "BakeArtifactSkeletonProfileHashMismatch";
    }

    public sealed class MxAnimationSkeletonCompatibilityProfile
    {
        private readonly List<string> _bonePaths;
        private readonly List<string> _socketPaths;

        public MxAnimationSkeletonCompatibilityProfile(
            string profileId,
            string profileHash = "",
            IEnumerable<string> bonePaths = null,
            IEnumerable<string> socketPaths = null)
        {
            ProfileId = profileId ?? string.Empty;
            _bonePaths = MxAnimationCompatibilityPathUtility.CopyUniqueSortedPaths(bonePaths);
            _socketPaths = MxAnimationCompatibilityPathUtility.CopyUniqueSortedPaths(socketPaths);
            ProfileHash = string.IsNullOrWhiteSpace(profileHash)
                ? MxAnimationCompatibilityHasher.ComputeSkeletonProfileHash(this)
                : profileHash;
        }

        public string ProfileId { get; }
        public string ProfileHash { get; }
        public IReadOnlyList<string> BonePaths => _bonePaths;
        public IReadOnlyList<string> SocketPaths => _socketPaths;

        public bool ContainsBonePath(string path)
        {
            return MxAnimationCompatibilityPathUtility.ContainsPath(_bonePaths, path);
        }

        public bool ContainsSocketPath(string path)
        {
            return MxAnimationCompatibilityPathUtility.ContainsPath(_socketPaths, path);
        }
    }

    public sealed class MxAnimationClipCompatibilityProfile
    {
        private readonly List<string> _bindingPaths;

        public MxAnimationClipCompatibilityProfile(
            ResourceKey clipKey,
            string skeletonProfileId = "",
            string skeletonProfileHash = "",
            IEnumerable<string> bindingPaths = null)
        {
            ClipKey = clipKey;
            SkeletonProfileId = skeletonProfileId ?? string.Empty;
            SkeletonProfileHash = skeletonProfileHash ?? string.Empty;
            _bindingPaths = MxAnimationCompatibilityPathUtility.CopyUniqueSortedPaths(bindingPaths);
        }

        public ResourceKey ClipKey { get; }
        public string SkeletonProfileId { get; }
        public string SkeletonProfileHash { get; }
        public IReadOnlyList<string> BindingPaths => _bindingPaths;

        public bool ContainsBindingPath(string path)
        {
            return MxAnimationCompatibilityPathUtility.ContainsPath(_bindingPaths, path);
        }
    }

    public sealed class MxAnimationAvatarMaskCompatibilityProfile
    {
        private readonly List<string> _activePaths;

        public MxAnimationAvatarMaskCompatibilityProfile(
            ResourceKey avatarMaskKey,
            string skeletonProfileId = "",
            string skeletonProfileHash = "",
            IEnumerable<string> activePaths = null)
        {
            AvatarMaskKey = avatarMaskKey;
            SkeletonProfileId = skeletonProfileId ?? string.Empty;
            SkeletonProfileHash = skeletonProfileHash ?? string.Empty;
            _activePaths = MxAnimationCompatibilityPathUtility.CopyUniqueSortedPaths(activePaths);
        }

        public ResourceKey AvatarMaskKey { get; }
        public string SkeletonProfileId { get; }
        public string SkeletonProfileHash { get; }
        public IReadOnlyList<string> ActivePaths => _activePaths;

        public bool ContainsActivePath(string path)
        {
            return MxAnimationCompatibilityPathUtility.ContainsPath(_activePaths, path);
        }
    }

    public sealed class MxAnimationClipCompatibilityExpectation
    {
        private readonly List<string> _requiredBindingPaths;

        public MxAnimationClipCompatibilityExpectation(
            ResourceKey clipKey,
            IEnumerable<string> requiredBindingPaths = null,
            string skeletonProfileId = "",
            string skeletonProfileHash = "")
        {
            ClipKey = clipKey;
            SkeletonProfileId = skeletonProfileId ?? string.Empty;
            SkeletonProfileHash = skeletonProfileHash ?? string.Empty;
            _requiredBindingPaths = MxAnimationCompatibilityPathUtility.CopyUniqueSortedPaths(requiredBindingPaths);
        }

        public ResourceKey ClipKey { get; }
        public string SkeletonProfileId { get; }
        public string SkeletonProfileHash { get; }
        public IReadOnlyList<string> RequiredBindingPaths => _requiredBindingPaths;
    }

    public sealed class MxAnimationAvatarMaskCompatibilityExpectation
    {
        private readonly List<string> _requiredActivePaths;

        public MxAnimationAvatarMaskCompatibilityExpectation(
            ResourceKey avatarMaskKey,
            IEnumerable<string> requiredActivePaths = null,
            string skeletonProfileId = "",
            string skeletonProfileHash = "")
        {
            AvatarMaskKey = avatarMaskKey;
            SkeletonProfileId = skeletonProfileId ?? string.Empty;
            SkeletonProfileHash = skeletonProfileHash ?? string.Empty;
            _requiredActivePaths = MxAnimationCompatibilityPathUtility.CopyUniqueSortedPaths(requiredActivePaths);
        }

        public ResourceKey AvatarMaskKey { get; }
        public string SkeletonProfileId { get; }
        public string SkeletonProfileHash { get; }
        public IReadOnlyList<string> RequiredActivePaths => _requiredActivePaths;
    }

    public sealed class MxAnimationCompatibilityExpectation
    {
        private readonly List<string> _requiredBonePaths;
        private readonly List<string> _requiredSocketPaths;
        private readonly List<MxAnimationClipCompatibilityExpectation> _clipExpectations;
        private readonly List<MxAnimationAvatarMaskCompatibilityExpectation> _avatarMaskExpectations;

        public MxAnimationCompatibilityExpectation(
            string skeletonProfileId = "",
            string skeletonProfileHash = "",
            IEnumerable<string> requiredBonePaths = null,
            IEnumerable<string> requiredSocketPaths = null,
            IEnumerable<MxAnimationClipCompatibilityExpectation> clipExpectations = null,
            IEnumerable<MxAnimationAvatarMaskCompatibilityExpectation> avatarMaskExpectations = null)
        {
            SkeletonProfileId = skeletonProfileId ?? string.Empty;
            SkeletonProfileHash = skeletonProfileHash ?? string.Empty;
            _requiredBonePaths = MxAnimationCompatibilityPathUtility.CopyUniqueSortedPaths(requiredBonePaths);
            _requiredSocketPaths = MxAnimationCompatibilityPathUtility.CopyUniqueSortedPaths(requiredSocketPaths);
            _clipExpectations = CopyAndSort(clipExpectations, CompareClipExpectation);
            _avatarMaskExpectations = CopyAndSort(avatarMaskExpectations, CompareAvatarMaskExpectation);
        }

        public string SkeletonProfileId { get; }
        public string SkeletonProfileHash { get; }
        public IReadOnlyList<string> RequiredBonePaths => _requiredBonePaths;
        public IReadOnlyList<string> RequiredSocketPaths => _requiredSocketPaths;
        public IReadOnlyList<MxAnimationClipCompatibilityExpectation> ClipExpectations => _clipExpectations;
        public IReadOnlyList<MxAnimationAvatarMaskCompatibilityExpectation> AvatarMaskExpectations => _avatarMaskExpectations;

        public bool IsDefault =>
            string.IsNullOrWhiteSpace(SkeletonProfileId)
            && string.IsNullOrWhiteSpace(SkeletonProfileHash)
            && _requiredBonePaths.Count == 0
            && _requiredSocketPaths.Count == 0
            && _clipExpectations.Count == 0
            && _avatarMaskExpectations.Count == 0;

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

        private static int CompareClipExpectation(
            MxAnimationClipCompatibilityExpectation left,
            MxAnimationClipCompatibilityExpectation right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return string.CompareOrdinal(left.ClipKey.ToString(), right.ClipKey.ToString());
        }

        private static int CompareAvatarMaskExpectation(
            MxAnimationAvatarMaskCompatibilityExpectation left,
            MxAnimationAvatarMaskCompatibilityExpectation right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return string.CompareOrdinal(left.AvatarMaskKey.ToString(), right.AvatarMaskKey.ToString());
        }
    }

    public sealed class MxAnimationCompatibilityProfile
    {
        private readonly List<MxAnimationClipCompatibilityProfile> _clipProfiles;
        private readonly List<MxAnimationAvatarMaskCompatibilityProfile> _avatarMaskProfiles;
        private readonly List<MxAnimationBakeArtifact> _bakeArtifacts;

        public MxAnimationCompatibilityProfile(
            MxAnimationSkeletonCompatibilityProfile skeletonProfile,
            IEnumerable<MxAnimationClipCompatibilityProfile> clipProfiles = null,
            IEnumerable<MxAnimationAvatarMaskCompatibilityProfile> avatarMaskProfiles = null,
            IEnumerable<MxAnimationBakeArtifact> bakeArtifacts = null)
        {
            SkeletonProfile = skeletonProfile;
            _clipProfiles = CopyAndSort(clipProfiles, CompareClipProfile);
            _avatarMaskProfiles = CopyAndSort(avatarMaskProfiles, CompareAvatarMaskProfile);
            _bakeArtifacts = CopyAndSort(bakeArtifacts, CompareBakeArtifact);
        }

        public MxAnimationSkeletonCompatibilityProfile SkeletonProfile { get; }
        public IReadOnlyList<MxAnimationClipCompatibilityProfile> ClipProfiles => _clipProfiles;
        public IReadOnlyList<MxAnimationAvatarMaskCompatibilityProfile> AvatarMaskProfiles => _avatarMaskProfiles;
        public IReadOnlyList<MxAnimationBakeArtifact> BakeArtifacts => _bakeArtifacts;

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

        private static int CompareClipProfile(
            MxAnimationClipCompatibilityProfile left,
            MxAnimationClipCompatibilityProfile right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return string.CompareOrdinal(left.ClipKey.ToString(), right.ClipKey.ToString());
        }

        private static int CompareAvatarMaskProfile(
            MxAnimationAvatarMaskCompatibilityProfile left,
            MxAnimationAvatarMaskCompatibilityProfile right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return string.CompareOrdinal(left.AvatarMaskKey.ToString(), right.AvatarMaskKey.ToString());
        }

        private static int CompareBakeArtifact(MxAnimationBakeArtifact left, MxAnimationBakeArtifact right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return string.CompareOrdinal(left.Profile.SourceClipKey.ToString(), right.Profile.SourceClipKey.ToString());
        }
    }

    public sealed class MxAnimationCompatibilityIssue
    {
        public MxAnimationCompatibilityIssue(
            MxAnimationCompatibilityIssueSeverity severity,
            string code,
            ResourceKey key,
            string field,
            string expected,
            string actual,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Key = key;
            Field = field ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public MxAnimationCompatibilityIssueSeverity Severity { get; }
        public string Code { get; }
        public ResourceKey Key { get; }
        public string Field { get; }
        public string Expected { get; }
        public string Actual { get; }
        public string Message { get; }
    }

    public sealed class MxAnimationCompatibilityValidationReport
    {
        private readonly List<MxAnimationCompatibilityIssue> _issues = new List<MxAnimationCompatibilityIssue>();

        public IReadOnlyList<MxAnimationCompatibilityIssue> Issues => _issues;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public bool HasErrors => ErrorCount > 0;

        public void AddError(string code, ResourceKey key, string field, string expected, string actual, string message)
        {
            Add(MxAnimationCompatibilityIssueSeverity.Error, code, key, field, expected, actual, message);
        }

        public void AddWarning(string code, ResourceKey key, string field, string expected, string actual, string message)
        {
            Add(MxAnimationCompatibilityIssueSeverity.Warning, code, key, field, expected, actual, message);
        }

        private void Add(
            MxAnimationCompatibilityIssueSeverity severity,
            string code,
            ResourceKey key,
            string field,
            string expected,
            string actual,
            string message)
        {
            _issues.Add(new MxAnimationCompatibilityIssue(severity, code, key, field, expected, actual, message));
            if (severity == MxAnimationCompatibilityIssueSeverity.Error)
                ErrorCount++;
            else
                WarningCount++;
        }
    }

    public static class MxAnimationCompatibilityValidator
    {
        public static MxAnimationCompatibilityValidationReport ValidateExpectation(
            MxAnimationCompatibilityExpectation expectation)
        {
            var report = new MxAnimationCompatibilityValidationReport();
            if (expectation == null || expectation.IsDefault)
                return report;

            for (int i = 0; i < expectation.ClipExpectations.Count; i++)
            {
                MxAnimationClipCompatibilityExpectation clip = expectation.ClipExpectations[i];
                if (!clip.ClipKey.IsValid || !string.Equals(clip.ClipKey.TypeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal))
                {
                    report.AddError(
                        MxAnimationCompatibilityIssueCodes.ClipKeyInvalid,
                        clip.ClipKey,
                        "clipKey",
                        ResourceTypeIds.AnimationClip,
                        clip.ClipKey.TypeId,
                        "Clip compatibility expectation must use an animation clip ResourceKey.");
                }
            }

            for (int i = 0; i < expectation.AvatarMaskExpectations.Count; i++)
            {
                MxAnimationAvatarMaskCompatibilityExpectation mask = expectation.AvatarMaskExpectations[i];
                if (!mask.AvatarMaskKey.IsValid || !string.Equals(mask.AvatarMaskKey.TypeId, ResourceTypeIds.AvatarMask, StringComparison.Ordinal))
                {
                    report.AddError(
                        MxAnimationCompatibilityIssueCodes.AvatarMaskKeyInvalid,
                        mask.AvatarMaskKey,
                        "avatarMaskKey",
                        ResourceTypeIds.AvatarMask,
                        mask.AvatarMaskKey.TypeId,
                        "AvatarMask compatibility expectation must use an AvatarMask ResourceKey.");
                }
            }

            return report;
        }

        public static MxAnimationCompatibilityValidationReport Validate(
            MxAnimationCompatibilityProfile profile,
            MxAnimationCompatibilityExpectation expectation)
        {
            var report = ValidateExpectation(expectation);
            if (expectation == null || expectation.IsDefault)
                return report;

            if (profile == null)
            {
                report.AddError(
                    MxAnimationCompatibilityIssueCodes.CompatibilityProfileMissing,
                    default,
                    "compatibilityProfile",
                    "present",
                    "missing",
                    "Animation compatibility profile is required.");
                return report;
            }

            ValidateSkeleton(profile.SkeletonProfile, expectation, report);
            ValidateClips(profile, expectation, report);
            ValidateAvatarMasks(profile, expectation, report);
            ValidateBakeArtifacts(profile, expectation, report);
            return report;
        }

        public static MxAnimationCompatibilityValidationReport ValidateBakeArtifact(
            MxAnimationBakeArtifact artifact,
            MxAnimationCompatibilityExpectation expectation)
        {
            var report = new MxAnimationCompatibilityValidationReport();
            ValidateBakeArtifactInternal(artifact, expectation, report);
            return report;
        }

        private static void ValidateSkeleton(
            MxAnimationSkeletonCompatibilityProfile skeleton,
            MxAnimationCompatibilityExpectation expectation,
            MxAnimationCompatibilityValidationReport report)
        {
            if (skeleton == null)
            {
                report.AddError(
                    MxAnimationCompatibilityIssueCodes.SkeletonProfileMissing,
                    default,
                    "skeletonProfile",
                    "present",
                    "missing",
                    "Skeleton compatibility profile is required.");
                return;
            }

            CompareExpected(
                report,
                MxAnimationCompatibilityIssueCodes.SkeletonProfileIdMismatch,
                default,
                "skeletonProfileId",
                expectation.SkeletonProfileId,
                skeleton.ProfileId,
                "Skeleton profile id does not match compatibility expectation.");
            CompareExpected(
                report,
                MxAnimationCompatibilityIssueCodes.SkeletonProfileHashMismatch,
                default,
                "skeletonProfileHash",
                expectation.SkeletonProfileHash,
                skeleton.ProfileHash,
                "Skeleton profile hash does not match compatibility expectation.");

            for (int i = 0; i < expectation.RequiredBonePaths.Count; i++)
            {
                string path = expectation.RequiredBonePaths[i];
                if (!skeleton.ContainsBonePath(path))
                {
                    report.AddError(
                        MxAnimationCompatibilityIssueCodes.BonePathMissing,
                        default,
                        "bonePath",
                        path,
                        "missing",
                        "Required skeleton bone path is missing: " + path + ".");
                }
            }

            for (int i = 0; i < expectation.RequiredSocketPaths.Count; i++)
            {
                string path = expectation.RequiredSocketPaths[i];
                if (!skeleton.ContainsSocketPath(path))
                {
                    report.AddError(
                        MxAnimationCompatibilityIssueCodes.SocketPathMissing,
                        default,
                        "socketPath",
                        path,
                        "missing",
                        "Required skeleton socket path is missing: " + path + ".");
                }
            }
        }

        private static void ValidateClips(
            MxAnimationCompatibilityProfile profile,
            MxAnimationCompatibilityExpectation expectation,
            MxAnimationCompatibilityValidationReport report)
        {
            for (int i = 0; i < expectation.ClipExpectations.Count; i++)
            {
                MxAnimationClipCompatibilityExpectation clipExpectation = expectation.ClipExpectations[i];
                if (!TryFindClipProfile(profile, clipExpectation.ClipKey, out MxAnimationClipCompatibilityProfile clipProfile))
                {
                    report.AddError(
                        MxAnimationCompatibilityIssueCodes.ClipProfileMissing,
                        clipExpectation.ClipKey,
                        "clipProfile",
                        "present",
                        "missing",
                        "Clip compatibility profile is missing.");
                    continue;
                }

                string expectedProfileId = ResolveExpected(clipExpectation.SkeletonProfileId, expectation.SkeletonProfileId);
                string expectedProfileHash = ResolveExpected(clipExpectation.SkeletonProfileHash, expectation.SkeletonProfileHash);
                CompareExpected(
                    report,
                    MxAnimationCompatibilityIssueCodes.ClipSkeletonProfileIdMismatch,
                    clipExpectation.ClipKey,
                    "clipSkeletonProfileId",
                    expectedProfileId,
                    clipProfile.SkeletonProfileId,
                    "Clip skeleton profile id does not match compatibility expectation.");
                CompareExpected(
                    report,
                    MxAnimationCompatibilityIssueCodes.ClipSkeletonProfileHashMismatch,
                    clipExpectation.ClipKey,
                    "clipSkeletonProfileHash",
                    expectedProfileHash,
                    clipProfile.SkeletonProfileHash,
                    "Clip skeleton profile hash does not match compatibility expectation.");

                for (int pathIndex = 0; pathIndex < clipExpectation.RequiredBindingPaths.Count; pathIndex++)
                {
                    string path = clipExpectation.RequiredBindingPaths[pathIndex];
                    if (!clipProfile.ContainsBindingPath(path))
                    {
                        report.AddError(
                            MxAnimationCompatibilityIssueCodes.ClipBindingPathMissing,
                            clipExpectation.ClipKey,
                            "clipBindingPath",
                            path,
                            "missing",
                            "Required clip binding path is missing: " + path + ".");
                    }
                }
            }
        }

        private static void ValidateAvatarMasks(
            MxAnimationCompatibilityProfile profile,
            MxAnimationCompatibilityExpectation expectation,
            MxAnimationCompatibilityValidationReport report)
        {
            for (int i = 0; i < expectation.AvatarMaskExpectations.Count; i++)
            {
                MxAnimationAvatarMaskCompatibilityExpectation maskExpectation = expectation.AvatarMaskExpectations[i];
                if (!TryFindAvatarMaskProfile(profile, maskExpectation.AvatarMaskKey, out MxAnimationAvatarMaskCompatibilityProfile maskProfile))
                {
                    report.AddError(
                        MxAnimationCompatibilityIssueCodes.AvatarMaskProfileMissing,
                        maskExpectation.AvatarMaskKey,
                        "avatarMaskProfile",
                        "present",
                        "missing",
                        "AvatarMask compatibility profile is missing.");
                    continue;
                }

                string expectedProfileId = ResolveExpected(maskExpectation.SkeletonProfileId, expectation.SkeletonProfileId);
                string expectedProfileHash = ResolveExpected(maskExpectation.SkeletonProfileHash, expectation.SkeletonProfileHash);
                CompareExpected(
                    report,
                    MxAnimationCompatibilityIssueCodes.AvatarMaskSkeletonProfileIdMismatch,
                    maskExpectation.AvatarMaskKey,
                    "avatarMaskSkeletonProfileId",
                    expectedProfileId,
                    maskProfile.SkeletonProfileId,
                    "AvatarMask skeleton profile id does not match compatibility expectation.");
                CompareExpected(
                    report,
                    MxAnimationCompatibilityIssueCodes.AvatarMaskSkeletonProfileHashMismatch,
                    maskExpectation.AvatarMaskKey,
                    "avatarMaskSkeletonProfileHash",
                    expectedProfileHash,
                    maskProfile.SkeletonProfileHash,
                    "AvatarMask skeleton profile hash does not match compatibility expectation.");

                for (int pathIndex = 0; pathIndex < maskExpectation.RequiredActivePaths.Count; pathIndex++)
                {
                    string path = maskExpectation.RequiredActivePaths[pathIndex];
                    if (!maskProfile.ContainsActivePath(path))
                    {
                        report.AddError(
                            MxAnimationCompatibilityIssueCodes.AvatarMaskPathMissing,
                            maskExpectation.AvatarMaskKey,
                            "avatarMaskPath",
                            path,
                            "missing",
                            "Required AvatarMask path is missing or inactive: " + path + ".");
                    }
                }
            }
        }

        private static void ValidateBakeArtifacts(
            MxAnimationCompatibilityProfile profile,
            MxAnimationCompatibilityExpectation expectation,
            MxAnimationCompatibilityValidationReport report)
        {
            for (int i = 0; i < profile.BakeArtifacts.Count; i++)
                ValidateBakeArtifactInternal(profile.BakeArtifacts[i], expectation, report);
        }

        private static void ValidateBakeArtifactInternal(
            MxAnimationBakeArtifact artifact,
            MxAnimationCompatibilityExpectation expectation,
            MxAnimationCompatibilityValidationReport report)
        {
            if (artifact == null)
            {
                report.AddError(
                    MxAnimationCompatibilityIssueCodes.BakeArtifactMissing,
                    default,
                    "bakeArtifact",
                    "present",
                    "missing",
                    "Bake artifact is missing.");
                return;
            }

            ResourceKey key = artifact.Profile != null ? artifact.Profile.SourceClipKey : default;
            string actualProfileId = artifact.Profile != null ? artifact.Profile.SkeletonProfileId : string.Empty;
            string actualProfileHash = artifact.Profile != null ? artifact.Profile.SkeletonProfileHash : string.Empty;
            CompareExpected(
                report,
                MxAnimationCompatibilityIssueCodes.BakeArtifactSkeletonProfileIdMismatch,
                key,
                "bakeSkeletonProfileId",
                expectation != null ? expectation.SkeletonProfileId : string.Empty,
                actualProfileId,
                "Bake artifact skeleton profile id does not match compatibility expectation.");
            CompareExpected(
                report,
                MxAnimationCompatibilityIssueCodes.BakeArtifactSkeletonProfileHashMismatch,
                key,
                "bakeSkeletonProfileHash",
                expectation != null ? expectation.SkeletonProfileHash : string.Empty,
                actualProfileHash,
                "Bake artifact skeleton profile hash does not match compatibility expectation.");
        }

        private static bool TryFindClipProfile(
            MxAnimationCompatibilityProfile profile,
            ResourceKey key,
            out MxAnimationClipCompatibilityProfile clipProfile)
        {
            for (int i = 0; i < profile.ClipProfiles.Count; i++)
            {
                MxAnimationClipCompatibilityProfile candidate = profile.ClipProfiles[i];
                if (candidate != null && MatchesKey(candidate.ClipKey, key))
                {
                    clipProfile = candidate;
                    return true;
                }
            }

            clipProfile = null;
            return false;
        }

        private static bool TryFindAvatarMaskProfile(
            MxAnimationCompatibilityProfile profile,
            ResourceKey key,
            out MxAnimationAvatarMaskCompatibilityProfile maskProfile)
        {
            for (int i = 0; i < profile.AvatarMaskProfiles.Count; i++)
            {
                MxAnimationAvatarMaskCompatibilityProfile candidate = profile.AvatarMaskProfiles[i];
                if (candidate != null && MatchesKey(candidate.AvatarMaskKey, key))
                {
                    maskProfile = candidate;
                    return true;
                }
            }

            maskProfile = null;
            return false;
        }

        private static bool MatchesKey(ResourceKey left, ResourceKey right)
        {
            if (!string.Equals(left.Id, right.Id, StringComparison.Ordinal)
                || !string.Equals(left.TypeId, right.TypeId, StringComparison.Ordinal)
                || !string.Equals(left.Variant, right.Variant, StringComparison.Ordinal))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(right.PackageId)
                || string.Equals(left.PackageId, right.PackageId, StringComparison.Ordinal);
        }

        private static string ResolveExpected(string overrideValue, string fallback)
        {
            return string.IsNullOrWhiteSpace(overrideValue) ? fallback ?? string.Empty : overrideValue;
        }

        private static void CompareExpected(
            MxAnimationCompatibilityValidationReport report,
            string code,
            ResourceKey key,
            string field,
            string expected,
            string actual,
            string message)
        {
            if (string.IsNullOrWhiteSpace(expected))
                return;
            if (string.Equals(expected, actual, StringComparison.Ordinal))
                return;

            report.AddError(code, key, field, expected, actual, message);
        }
    }

    public static class MxAnimationCompatibilityHasher
    {
        public const string HashPrefix = "sha256:";

        public static string ComputeSkeletonProfileHash(MxAnimationSkeletonCompatibilityProfile profile)
        {
            if (profile == null)
                return HashPrefix + Sha256Hex(string.Empty);

            var builder = new StringBuilder();
            builder.Append("mxanimation.compatibility.skeleton.v1\n");
            builder.Append("profileId=").Append(profile.ProfileId ?? string.Empty).Append('\n');
            for (int i = 0; i < profile.BonePaths.Count; i++)
                builder.Append("bone[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]=").Append(profile.BonePaths[i]).Append('\n');
            for (int i = 0; i < profile.SocketPaths.Count; i++)
                builder.Append("socket[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]=").Append(profile.SocketPaths[i]).Append('\n');
            return HashPrefix + Sha256Hex(builder.ToString());
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

    internal static class MxAnimationCompatibilityPathUtility
    {
        public static List<string> CopyUniqueSortedPaths(IEnumerable<string> source)
        {
            var paths = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            if (source != null)
            {
                foreach (string path in source)
                {
                    string normalized = NormalizePath(path);
                    if (string.IsNullOrWhiteSpace(normalized) || !unique.Add(normalized))
                        continue;

                    paths.Add(normalized);
                }
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        public static bool ContainsPath(IReadOnlyList<string> paths, string path)
        {
            string normalized = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], normalized, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
        }
    }

    public readonly struct MxAnimationBakeIssueLocation : IEquatable<MxAnimationBakeIssueLocation>
    {
        public MxAnimationBakeIssueLocation(
            ResourceKey sourceClipKey,
            string profileId = "",
            string skeletonProfileId = "",
            string artifactHash = "")
        {
            SourceClipKey = sourceClipKey;
            ProfileId = profileId ?? string.Empty;
            SkeletonProfileId = skeletonProfileId ?? string.Empty;
            ArtifactHash = artifactHash ?? string.Empty;
        }

        public ResourceKey SourceClipKey { get; }
        public string ProfileId { get; }
        public string SkeletonProfileId { get; }
        public string ArtifactHash { get; }

        public bool HasValue =>
            SourceClipKey.IsValid
            || !string.IsNullOrWhiteSpace(ProfileId)
            || !string.IsNullOrWhiteSpace(SkeletonProfileId)
            || !string.IsNullOrWhiteSpace(ArtifactHash);

        public bool Equals(MxAnimationBakeIssueLocation other)
        {
            return SourceClipKey.Equals(other.SourceClipKey)
                && string.Equals(ProfileId, other.ProfileId, StringComparison.Ordinal)
                && string.Equals(SkeletonProfileId, other.SkeletonProfileId, StringComparison.Ordinal)
                && string.Equals(ArtifactHash, other.ArtifactHash, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MxAnimationBakeIssueLocation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceClipKey.GetHashCode();
                hash = (hash * 397) ^ (ProfileId != null ? ProfileId.GetHashCode() : 0);
                hash = (hash * 397) ^ (SkeletonProfileId != null ? SkeletonProfileId.GetHashCode() : 0);
                hash = (hash * 397) ^ (ArtifactHash != null ? ArtifactHash.GetHashCode() : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            if (!HasValue)
                return string.Empty;

            return "sourceClip=" + SourceClipKey
                + " profile=" + ProfileId
                + " skeleton=" + SkeletonProfileId
                + " artifact=" + ArtifactHash;
        }
    }

    public sealed class MxAnimationBakeIssue
    {
        public MxAnimationBakeIssue(
            MxAnimationBakeIssueSeverity severity,
            string code,
            string field,
            string expected,
            string actual,
            string message,
            MxAnimationBakeIssueLocation location = default)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Field = field ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
            Message = message ?? string.Empty;
            Location = location;
        }

        public MxAnimationBakeIssueSeverity Severity { get; }
        public string Code { get; }
        public string Field { get; }
        public string Expected { get; }
        public string Actual { get; }
        public string Message { get; }
        public MxAnimationBakeIssueLocation Location { get; }
    }

    public sealed class MxAnimationBakeValidationReport
    {
        private readonly List<MxAnimationBakeIssue> _issues = new List<MxAnimationBakeIssue>();

        public IReadOnlyList<MxAnimationBakeIssue> Issues => _issues;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public bool HasErrors => ErrorCount > 0;

        public void AddError(
            string code,
            string field,
            string expected,
            string actual,
            string message,
            MxAnimationBakeIssueLocation location = default)
        {
            Add(MxAnimationBakeIssueSeverity.Error, code, field, expected, actual, message, location);
        }

        public void AddWarning(
            string code,
            string field,
            string expected,
            string actual,
            string message,
            MxAnimationBakeIssueLocation location = default)
        {
            Add(MxAnimationBakeIssueSeverity.Warning, code, field, expected, actual, message, location);
        }

        private void Add(
            MxAnimationBakeIssueSeverity severity,
            string code,
            string field,
            string expected,
            string actual,
            string message,
            MxAnimationBakeIssueLocation location)
        {
            _issues.Add(new MxAnimationBakeIssue(severity, code, field, expected, actual, message, location));
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

            MxAnimationBakeIssueLocation location = CreateLocation(artifact);
            ValidateProfile(artifact.Profile, report, location);

            string actualProfileHash = MxAnimationBakeHasher.ComputeProfileHash(artifact.Profile);
            if (!string.Equals(artifact.Profile.ProfileHash, actualProfileHash, StringComparison.Ordinal))
            {
                report.AddError("BakeProfileHashMismatch", "profileHash", artifact.Profile.ProfileHash, actualProfileHash, "Bake profile hash does not match profile contents.", location);
            }

            string actualArtifactHash = MxAnimationBakeHasher.ComputeArtifactHash(artifact);
            if (!string.Equals(artifact.ArtifactHash, actualArtifactHash, StringComparison.Ordinal))
            {
                report.AddError("BakeArtifactHashMismatch", "artifactHash", artifact.ArtifactHash, actualArtifactHash, "Bake artifact hash does not match artifact contents.", location);
            }

            ValidateDuplicateTraceFrames(artifact, report, location);
            ValidateDuplicateSocketFrames(artifact, report, location);

            if (expectation != null)
                ValidateExpectation(artifact, expectation, report);

            return report;
        }

        private static void ValidateProfile(
            MxAnimationBakeProfile profile,
            MxAnimationBakeValidationReport report,
            MxAnimationBakeIssueLocation location)
        {
            if (profile == null)
            {
                report.AddError("BakeProfileMissing", "profile", "non-null", "null", "Bake profile is missing.", location);
                return;
            }

            if (!profile.SourceClipKey.IsValid)
                report.AddError("BakeSourceClipKeyMissing", "sourceClipKey", "valid ResourceKey", profile.SourceClipKey.ToString(), "Bake source clip key is missing or invalid.", location);
            if (string.IsNullOrWhiteSpace(profile.SourceClipHash))
                report.AddError("BakeSourceClipHashMissing", "sourceClipHash", "non-empty", profile.SourceClipHash, "Bake source clip hash is required.", location);
            if (string.IsNullOrWhiteSpace(profile.ProfileId))
                report.AddError("BakeProfileIdMissing", "profileId", "non-empty", profile.ProfileId, "Bake profile id is required.", location);
            if (string.IsNullOrWhiteSpace(profile.ProfileHash))
                report.AddError("BakeProfileHashMissing", "profileHash", "non-empty", profile.ProfileHash, "Bake profile hash is required.", location);
            if (string.IsNullOrWhiteSpace(profile.SkeletonProfileHash))
                report.AddWarning("BakeSkeletonProfileHashMissing", "skeletonProfileHash", "non-empty", profile.SkeletonProfileHash, "Skeleton profile hash is recommended for stale-artifact diagnostics.", location);
            if (profile.SampleTickRate <= 0)
                report.AddError("BakeSampleTickRateInvalid", "sampleTickRate", "> 0", profile.SampleTickRate.ToString(CultureInfo.InvariantCulture), "Sample tick rate must be positive.", location);
            if (profile.QuantizationScale <= 0)
                report.AddError("BakeQuantizationScaleInvalid", "quantizationScale", "> 0", profile.QuantizationScale.ToString(CultureInfo.InvariantCulture), "Quantization scale must be positive.", location);
        }

        private static void ValidateDuplicateTraceFrames(
            MxAnimationBakeArtifact artifact,
            MxAnimationBakeValidationReport report,
            MxAnimationBakeIssueLocation location)
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
                    report.AddError("BakeDuplicateWeaponTraceFrame", "weaponTraceFrames", "unique localFrame/traceId/socketId", key, "Duplicate baked weapon trace frame.", location);
            }
        }

        private static void ValidateDuplicateSocketFrames(
            MxAnimationBakeArtifact artifact,
            MxAnimationBakeValidationReport report,
            MxAnimationBakeIssueLocation location)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < artifact.SocketFrames.Count; i++)
            {
                MxAnimationBakedSocketFrame frame = artifact.SocketFrames[i];
                string key = frame.LocalFrame.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + frame.SocketId
                    + ":"
                    + frame.SocketPath;
                if (!keys.Add(key))
                    report.AddError("BakeDuplicateSocketFrame", "socketFrames", "unique localFrame/socketId/socketPath", key, "Duplicate baked socket trajectory frame.", location);
            }
        }

        private static void ValidateExpectation(
            MxAnimationBakeArtifact artifact,
            MxAnimationBakeExpectation expectation,
            MxAnimationBakeValidationReport report)
        {
            MxAnimationBakeIssueLocation location = CreateLocation(artifact);
            CompareExpected("BakeSourceClipHashMismatch", "sourceClipHash", expectation.SourceClipHash, artifact.Profile.SourceClipHash, report, location);
            CompareExpected("BakeProfileHashExpectedMismatch", "profileHash", expectation.ProfileHash, artifact.Profile.ProfileHash, report, location);
            CompareExpected("BakeSkeletonProfileHashMismatch", "skeletonProfileHash", expectation.SkeletonProfileHash, artifact.Profile.SkeletonProfileHash, report, location);
            CompareExpected("BakeArtifactHashExpectedMismatch", "artifactHash", expectation.ArtifactHash, artifact.ArtifactHash, report, location);

            if (expectation.CompatibilityExpectation != null && !expectation.CompatibilityExpectation.IsDefault)
            {
                MxAnimationCompatibilityValidationReport compatibilityReport =
                    MxAnimationCompatibilityValidator.ValidateBakeArtifact(artifact, expectation.CompatibilityExpectation);
                for (int i = 0; i < compatibilityReport.Issues.Count; i++)
                {
                    MxAnimationCompatibilityIssue issue = compatibilityReport.Issues[i];
                    if (issue.Severity == MxAnimationCompatibilityIssueSeverity.Error)
                        report.AddError(issue.Code, issue.Field, issue.Expected, issue.Actual, issue.Message, location);
                    else
                        report.AddWarning(issue.Code, issue.Field, issue.Expected, issue.Actual, issue.Message, location);
                }
            }
        }

        private static MxAnimationBakeIssueLocation CreateLocation(MxAnimationBakeArtifact artifact)
        {
            if (artifact == null)
                return default;

            return CreateLocation(artifact.Profile, artifact.ArtifactHash);
        }

        private static MxAnimationBakeIssueLocation CreateLocation(
            MxAnimationBakeProfile profile,
            string artifactHash = "")
        {
            if (profile == null)
                return default;

            return new MxAnimationBakeIssueLocation(
                profile.SourceClipKey,
                profile.ProfileId,
                profile.SkeletonProfileId,
                artifactHash);
        }

        private static void CompareExpected(
            string code,
            string field,
            string expected,
            string actual,
            MxAnimationBakeValidationReport report,
            MxAnimationBakeIssueLocation location)
        {
            if (string.IsNullOrWhiteSpace(expected))
                return;
            if (string.Equals(expected, actual, StringComparison.Ordinal))
                return;

            report.AddError(code, field, expected, actual, "Bake artifact expectation mismatch.", location);
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
            builder.Append("mxanimation.bake.artifact.v2\n");
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

            for (int i = 0; i < artifact.SocketFrames.Count; i++)
            {
                MxAnimationBakedSocketFrame frame = artifact.SocketFrames[i];
                builder.Append("socket[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]\n");
                Append(builder, "localFrame", frame.LocalFrame.ToString(CultureInfo.InvariantCulture));
                Append(builder, "socketId", frame.SocketId);
                Append(builder, "socketPath", frame.SocketPath);
                Append(builder, "position", frame.Position.ToString());
                Append(builder, "deltaPosition", frame.DeltaPosition.ToString());
            }

            for (int i = 0; i < artifact.EventMarkers.Count; i++)
            {
                MxAnimationBakedEventMarker marker = artifact.EventMarkers[i];
                builder.Append("event[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]\n");
                Append(builder, "localFrame", marker.LocalFrame.ToString(CultureInfo.InvariantCulture));
                Append(builder, "presentationFrame", marker.PresentationFrame.ToString(CultureInfo.InvariantCulture));
                Append(builder, "combatFrame", marker.CombatFrame.ToString(CultureInfo.InvariantCulture));
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
