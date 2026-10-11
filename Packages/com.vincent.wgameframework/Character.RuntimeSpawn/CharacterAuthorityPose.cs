using System;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.CharacterRuntimeSpawn
{
    public static class CharacterAuthorityPoseRegionIds
    {
        public const int Broadcast = 0;
        public const int Default = 1;
        public const int Torso = 10;
        public const int Head = 3;
        public const int LeftWingMembrane = 32;
        public const int LeftWingRoot = 31;
        public const int Neck = 30;
        public const int Jaw = 29;

        public static int NormalizeRequestRegion(int regionId)
        {
            if (regionId < 0) throw new ArgumentOutOfRangeException(nameof(regionId));
            return regionId;
        }

        public static int NormalizeBindingRegion(int regionId)
        {
            if (regionId <= 0) throw new ArgumentOutOfRangeException(nameof(regionId));
            return regionId;
        }
    }

    public enum CharacterAuthorityPoseMatchSource
    {
        None = 0, BodyPart = 1, HitZone = 2, Default = 3, PreviewCommand = 4
    }

    public enum CharacterAuthorityPoseIssue
    {
        None = 0, MissingBodyPartOrHitZone = 1, UnmappedHitZone = 2,
        NoEffect = 3, NoBindings = 4, DefaultBindingUsed = 5, BindingNotFound = 6,
        TargetEntityNotFound = 7, StateUnavailable = 8, LockedTargetNotFound = 9, TargetAndLocalPlayerUnavailable = 10
    }

    public sealed class CharacterAuthorityPoseRigDefinition
    {
        public static readonly CharacterAuthorityPoseRigDefinition Empty =
            new CharacterAuthorityPoseRigDefinition(
                Array.Empty<CharacterAuthoritySecondaryBinding>(),
                Array.Empty<CharacterAuthorityTwoBoneIkBinding>());

        private readonly CharacterAuthoritySecondaryBinding[] _secondaryBindings;
        private readonly CharacterAuthorityTwoBoneIkBinding[] _twoBoneIkBindings;
        private readonly CharacterAuthorityPoseChannelDefinition[] _channels;
        private readonly CharacterAuthorityGroundProbeDefinition[] _groundProbes;
        private readonly CharacterAuthorityPoseAttachmentDefinition[] _attachments;
        private readonly int _knownSecondaryRegionCount;

        public CharacterAuthorityPoseRigDefinition(
            IEnumerable<CharacterAuthoritySecondaryBinding> secondaryBindings = null,
            IEnumerable<CharacterAuthorityTwoBoneIkBinding> twoBoneIkBindings = null,
            IEnumerable<CharacterAuthorityPoseChannelDefinition> channels = null,
            IEnumerable<CharacterAuthorityGroundProbeDefinition> groundProbes = null,
            IEnumerable<CharacterAuthorityPoseAttachmentDefinition> attachments = null,
            int sourceRevision = 0,
            string contentHash = "")
        {
            _secondaryBindings = CopyAndSort(
                secondaryBindings,
                binding => binding.BindingId);
            _twoBoneIkBindings = CopyAndSort(
                twoBoneIkBindings,
                binding => binding.BindingId);
            _channels = CopyAndSort(channels, channel => channel.ChannelId);
            _groundProbes = CopyAndSort(groundProbes, probe => probe.ProbeId);
            _attachments = CopyAndSort(attachments, attachment => attachment.Ordinal);
            _knownSecondaryRegionCount = CountDistinctRegions(_secondaryBindings);
            SourceRevision = sourceRevision > 0 ? sourceRevision : 0;
            ContentHash = contentHash ?? string.Empty;
        }

        public IReadOnlyList<CharacterAuthoritySecondaryBinding> SecondaryBindings => _secondaryBindings;
        public IReadOnlyList<CharacterAuthorityTwoBoneIkBinding> TwoBoneIkBindings => _twoBoneIkBindings;
        public IReadOnlyList<CharacterAuthorityPoseChannelDefinition> Channels => _channels;
        public IReadOnlyList<CharacterAuthorityGroundProbeDefinition> GroundProbes => _groundProbes;
        public IReadOnlyList<CharacterAuthorityPoseAttachmentDefinition> Attachments => _attachments;
        public int KnownSecondaryRegionCount => _knownSecondaryRegionCount;
        public int SourceRevision { get; }
        public string ContentHash { get; }

        public static CharacterAuthorityPoseRigDefinition FromRuntimeBinding(
            CharacterAuthorityPoseRigRuntimeBinding binding,
            IEnumerable<CharacterAuthoritySecondaryBinding> secondaryBindings = null)
        {
            if (binding == null || !binding.IsAuthored)
                return new CharacterAuthorityPoseRigDefinition(secondaryBindings);
            var channels = new CharacterAuthorityPoseChannelDefinition[binding.Channels.Length];
            for (int i = 0; i < channels.Length; i++)
            {
                CharacterAuthorityPoseChannelRuntimeBinding source = binding.Channels[i];
                channels[i] = new CharacterAuthorityPoseChannelDefinition(
                    source.Ordinal,
                    source.ChannelId,
                    source.BonePath,
                    source.PartId);
            }
            var ik = new CharacterAuthorityTwoBoneIkBinding[binding.Limbs.Length];
            var probes = new CharacterAuthorityGroundProbeDefinition[binding.Limbs.Length];
            for (int i = 0; i < binding.Limbs.Length; i++)
            {
                CharacterAuthorityPoseLimbRuntimeBinding source = binding.Limbs[i];
                FixVector3 pole = FromSnorm(source.PoleDirectionSnorm, binding.NormalQuantizationScale);
                ik[i] = new CharacterAuthorityTwoBoneIkBinding(
                    source.IkBindingId,
                    source.RootChannelId,
                    source.MidChannelId,
                    source.TipChannelId,
                    FromMillimeters(source.RootToMidLengthMillimeters),
                    FromMillimeters(source.MidToTipLengthMillimeters),
                    pole,
                    FromMillimeters(source.MaximumReachMillimeters),
                    FromMillimeters(source.MinimumReachMillimeters),
                    source.PartStateId,
                    source.Handedness,
                    source.MirrorPeerProbeId);
                probes[i] = new CharacterAuthorityGroundProbeDefinition(source, binding.NormalQuantizationScale);
            }
            var attachments = new CharacterAuthorityPoseAttachmentDefinition[binding.Attachments.Length];
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterAuthorityPoseAttachmentRuntimeBinding source = binding.Attachments[i];
                attachments[i] = new CharacterAuthorityPoseAttachmentDefinition(
                    source.Ordinal,
                    source.Kind,
                    source.StableId,
                    source.ChannelId);
            }
            return new CharacterAuthorityPoseRigDefinition(
                secondaryBindings,
                ik,
                channels,
                probes,
                attachments,
                binding.Revision,
                binding.ContentHash);
        }

        internal static Fix64 FromMillimeters(int value) => Fix64.FromRatio(value, 1000);

        internal static FixVector3 FromSnorm(CharacterAuthorityPoseQuantizedVector3 value, int scale)
        {
            int safeScale = scale > 0 ? scale : 32767;
            var vector = new FixVector3(
                Fix64.FromRatio(value.X, safeScale),
                Fix64.FromRatio(value.Y, safeScale),
                Fix64.FromRatio(value.Z, safeScale));
            return vector.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
        }

        private static T[] CopyAndSort<T>(IEnumerable<T> source, Func<T, int> keySelector)
            where T : class
        {
            if (source == null)
                return Array.Empty<T>();

            var values = new List<T>();
            foreach (T value in source)
            {
                if (value != null)
                    values.Add(value);
            }

            values.Sort((left, right) => keySelector(left).CompareTo(keySelector(right)));
            return values.ToArray();
        }

        private static int CountDistinctRegions(CharacterAuthoritySecondaryBinding[] bindings)
        {
            if (bindings == null || bindings.Length == 0)
                return 0;

            int count = 0;
            for (int i = 0; i < bindings.Length; i++)
            {
                int regionId = bindings[i].RegionId;
                bool known = false;
                for (int j = 0; j < i; j++)
                {
                    if (bindings[j].RegionId == regionId)
                    {
                        known = true;
                        break;
                    }
                }

                if (!known)
                    count++;
            }

            return count;
        }
    }

    public sealed class CharacterAuthoritySecondaryBinding
    {
        public CharacterAuthoritySecondaryBinding(
            int bindingId,
            int regionId,
            int channelId,
            int socketId,
            Fix64 stiffness,
            Fix64 dampingPerFrame,
            Fix64 maxOffset,
            Fix64 strengthScale)
        {
            if (bindingId <= 0) throw new ArgumentOutOfRangeException(nameof(bindingId));
            BindingId = bindingId;
            RegionId = CharacterAuthorityPoseRegionIds.NormalizeBindingRegion(regionId);
            ChannelId = channelId;
            SocketId = socketId;
            Stiffness = Fix64.Clamp(stiffness, Fix64.Zero, Fix64.One);
            DampingPerFrame = Fix64.Clamp(dampingPerFrame, Fix64.Zero, Fix64.One);
            MaxOffset = maxOffset > Fix64.Zero ? maxOffset : Fix64.FromRatio(4, 100);
            StrengthScale = strengthScale > Fix64.Zero ? strengthScale : Fix64.One;
        }

        public int BindingId { get; }
        public int RegionId { get; }
        public int ChannelId { get; }
        public int SocketId { get; }
        public Fix64 Stiffness { get; }
        public Fix64 DampingPerFrame { get; }
        public Fix64 MaxOffset { get; }
        public Fix64 StrengthScale { get; }
    }

    public sealed class CharacterAuthorityTwoBoneIkBinding
    {
        public CharacterAuthorityTwoBoneIkBinding(
            int bindingId,
            int rootChannelId,
            int midChannelId,
            int tipChannelId,
            Fix64 rootToMidLength,
            Fix64 midToTipLength,
            FixVector3 poleDirection,
            Fix64 reachClamp,
            Fix64 minimumReach = default,
            int partStateId = 0,
            CharacterAuthorityPoseRigHandedness handedness = CharacterAuthorityPoseRigHandedness.None,
            int mirrorPeerProbeId = 0)
        {
            if (bindingId <= 0) throw new ArgumentOutOfRangeException(nameof(bindingId));
            BindingId = bindingId;
            RootChannelId = rootChannelId;
            MidChannelId = midChannelId;
            TipChannelId = tipChannelId;
            RootToMidLength = rootToMidLength > Fix64.Zero ? rootToMidLength : Fix64.Zero;
            MidToTipLength = midToTipLength > Fix64.Zero ? midToTipLength : Fix64.Zero;
            PoleDirection = poleDirection.TryNormalize(out FixVector3 normalizedPole)
                ? normalizedPole
                : new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
            ReachClamp = reachClamp > Fix64.Zero ? reachClamp : RootToMidLength + MidToTipLength;
            MinimumReach = minimumReach > Fix64.Zero ? minimumReach : Fix64.FromRatio(1, 1000);
            PartStateId = partStateId;
            Handedness = handedness;
            MirrorPeerProbeId = mirrorPeerProbeId;
        }

        public int BindingId { get; }
        public int RootChannelId { get; }
        public int MidChannelId { get; }
        public int TipChannelId { get; }
        public Fix64 RootToMidLength { get; }
        public Fix64 MidToTipLength { get; }
        public FixVector3 PoleDirection { get; }
        public Fix64 ReachClamp { get; }
        public Fix64 MinimumReach { get; }
        public int PartStateId { get; }
        public CharacterAuthorityPoseRigHandedness Handedness { get; }
        public int MirrorPeerProbeId { get; }
    }

    public sealed class CharacterAuthorityPoseChannelDefinition
    {
        public CharacterAuthorityPoseChannelDefinition(int ordinal, int channelId, string bonePath, int partId)
        {
            Ordinal = ordinal;
            ChannelId = channelId;
            BonePath = bonePath ?? string.Empty;
            PartId = partId;
        }
        public int Ordinal { get; }
        public int ChannelId { get; }
        public string BonePath { get; }
        public int PartId { get; }
    }

    public sealed class CharacterAuthorityGroundProbeDefinition
    {
        internal CharacterAuthorityGroundProbeDefinition(CharacterAuthorityPoseLimbRuntimeBinding source, int normalScale)
        {
            Ordinal = source.Ordinal;
            ProbeId = source.ProbeId;
            IkBindingId = source.IkBindingId;
            PartStateId = source.PartStateId;
            Handedness = source.Handedness;
            MirrorPeerProbeId = source.MirrorPeerProbeId;
            TipChannelId = source.TipChannelId;
            TipLocalOrigin = new FixVector3(
                CharacterAuthorityPoseRigDefinition.FromMillimeters(source.TipProbeOriginMillimeters.X),
                CharacterAuthorityPoseRigDefinition.FromMillimeters(source.TipProbeOriginMillimeters.Y),
                CharacterAuthorityPoseRigDefinition.FromMillimeters(source.TipProbeOriginMillimeters.Z));
            TipLocalAxis = CharacterAuthorityPoseRigDefinition.FromSnorm(source.TipProbeAxisSnorm, normalScale);
            MaxUpDistance = CharacterAuthorityPoseRigDefinition.FromMillimeters(source.MaxUpDistanceMillimeters);
            MaxDownDistance = CharacterAuthorityPoseRigDefinition.FromMillimeters(source.MaxDownDistanceMillimeters);
            Radius = CharacterAuthorityPoseRigDefinition.FromMillimeters(source.RadiusMillimeters);
            Clearance = CharacterAuthorityPoseRigDefinition.FromMillimeters(source.ClearanceMillimeters);
            StaticLayerMask = source.StaticLayerMask;
            MinimumSlopeCosine = Fix64.FromRatio(source.MinimumSlopeCosineMilli, 1000);
            WorldNormalTargetOffset = CharacterAuthorityPoseRigDefinition.FromMillimeters(source.WorldNormalTargetOffsetMillimeters);
            AllowedMotionIds = source.AllowedMotionIds;
            AllowedPostureIds = source.AllowedPostureIds;
            DisabledActionTags = source.DisabledActionTags;
            EnterFrames = source.EnterFrames;
            ExitFrames = source.ExitFrames;
            LostFrames = source.LostFrames;
        }
        public int Ordinal { get; }
        public int ProbeId { get; }
        public int IkBindingId { get; }
        public int PartStateId { get; }
        public CharacterAuthorityPoseRigHandedness Handedness { get; }
        public int MirrorPeerProbeId { get; }
        public int TipChannelId { get; }
        public FixVector3 TipLocalOrigin { get; }
        public FixVector3 TipLocalAxis { get; }
        public Fix64 MaxUpDistance { get; }
        public Fix64 MaxDownDistance { get; }
        public Fix64 Radius { get; }
        public Fix64 Clearance { get; }
        public int StaticLayerMask { get; }
        public Fix64 MinimumSlopeCosine { get; }
        public Fix64 WorldNormalTargetOffset { get; }
        public string[] AllowedMotionIds { get; }
        public int[] AllowedPostureIds { get; }
        public string[] DisabledActionTags { get; }
        public int EnterFrames { get; }
        public int ExitFrames { get; }
        public int LostFrames { get; }
    }

    public sealed class CharacterAuthorityPoseAttachmentDefinition
    {
        public CharacterAuthorityPoseAttachmentDefinition(
            int ordinal,
            CharacterAuthorityPoseRigAttachmentKind kind,
            int stableId,
            int channelId)
        {
            Ordinal = ordinal;
            Kind = kind;
            StableId = stableId;
            ChannelId = channelId;
            StableKey = ((long)Kind << 32) | (uint)StableId;
        }
        public int Ordinal { get; }
        public CharacterAuthorityPoseRigAttachmentKind Kind { get; }
        public int StableId { get; }
        public int ChannelId { get; }
        public long StableKey { get; }
    }

    public readonly struct CharacterAuthorityPoseImpulseRequest
    {
        public CharacterAuthorityPoseImpulseRequest(
            int regionId,
            FixVector3 direction,
            Fix64 strength,
            int durationFrames,
            RuntimeTraceIdentity correlationId = default,
            CharacterAuthorityPoseMatchSource matchSource = CharacterAuthorityPoseMatchSource.None,
            CharacterAuthorityPoseIssue fallbackIssue = CharacterAuthorityPoseIssue.None,
            int fallbackIssueArgumentId = 0)
        {
            RegionId = CharacterAuthorityPoseRegionIds.NormalizeRequestRegion(regionId);
            Direction = direction.TryNormalize(out FixVector3 normalized)
                ? normalized
                : FixVector3.Zero;
            Strength = strength > Fix64.Zero ? strength : Fix64.Zero;
            DurationFrames = durationFrames > 0 ? durationFrames : 0;
            CorrelationId = correlationId;
            MatchSource = matchSource;
            FallbackIssue = fallbackIssue;
            FallbackIssueArgumentId = fallbackIssueArgumentId;
        }

        public int RegionId { get; }
        public FixVector3 Direction { get; }
        public Fix64 Strength { get; }
        public int DurationFrames { get; }
        public RuntimeTraceIdentity CorrelationId { get; }
        public CharacterAuthorityPoseMatchSource MatchSource { get; }
        public CharacterAuthorityPoseIssue FallbackIssue { get; }
        public int FallbackIssueArgumentId { get; }
        public bool HasEffect => DurationFrames > 0 && Strength > Fix64.Zero && !Direction.IsZero;
    }

    public sealed class CharacterAuthorityPoseStateComponent
    {
        private static readonly Fix64 SleepOffsetThreshold = Fix64.FromRatio(1, 100000);
        private CharacterAuthorityPoseRigDefinition _definition;
        private CharacterAuthoritySecondaryState[] _secondaryStates = Array.Empty<CharacterAuthoritySecondaryState>();
        private int _lastResolvedRegionId;
        private CharacterAuthorityPoseMatchSource _lastMatchSource;
        private CharacterAuthorityPoseIssue _lastIssue;
        private int _lastIssueArgumentId;

        public CharacterAuthorityPoseStateComponent(CharacterAuthorityPoseRigDefinition definition = null)
        {
            ConfigureDefinition(definition ?? CharacterAuthorityPoseRigDefinition.Empty);
        }

        public CharacterAuthorityPoseRigDefinition Definition => _definition;
        public int LastResolvedRegionId => _lastResolvedRegionId;
        public CharacterAuthorityPoseMatchSource LastMatchSource => _lastMatchSource;
        public CharacterAuthorityPoseIssue LastIssue => _lastIssue;

        public bool HasActiveWork
        {
            get
            {
                for (int i = 0; i < _secondaryStates.Length; i++)
                {
                    if (_secondaryStates[i].HasActiveWork)
                        return true;
                }

                return false;
            }
        }

        public void ConfigureDefinition(CharacterAuthorityPoseRigDefinition definition)
        {
            _definition = definition ?? CharacterAuthorityPoseRigDefinition.Empty;
            IReadOnlyList<CharacterAuthoritySecondaryBinding> bindings = _definition.SecondaryBindings;
            _secondaryStates = new CharacterAuthoritySecondaryState[bindings.Count];
            for (int i = 0; i < bindings.Count; i++)
                _secondaryStates[i] = new CharacterAuthoritySecondaryState(bindings[i]);
            _lastResolvedRegionId = 0;
            _lastMatchSource = CharacterAuthorityPoseMatchSource.None;
            _lastIssue = CharacterAuthorityPoseIssue.None;
            _lastIssueArgumentId = 0;
        }

        public bool ApplySecondaryImpulse(
            CharacterAuthorityPoseImpulseRequest request,
            RuntimeFrame startFrame,
            out string issue)
        {
            issue = string.Empty;
            _lastResolvedRegionId = request.RegionId;
            _lastMatchSource = request.MatchSource;
            _lastIssue = request.FallbackIssue;
            _lastIssueArgumentId = request.FallbackIssueArgumentId;

            if (!request.HasEffect)
            {
                issue = "authority pose request has no effect";
                _lastIssue = CharacterAuthorityPoseIssue.NoEffect;
                return false;
            }

            if (_secondaryStates.Length == 0)
            {
                issue = "authority pose has no secondary bindings";
                _lastIssue = CharacterAuthorityPoseIssue.NoBindings;
                return false;
            }

            int requestRegion = CharacterAuthorityPoseRegionIds.NormalizeRequestRegion(request.RegionId);
            int matched = ApplySecondaryImpulseToMatchingRegion(request, startFrame, requestRegion);
            if (matched == 0
                && requestRegion != CharacterAuthorityPoseRegionIds.Default)
            {
                matched = ApplySecondaryImpulseToMatchingRegion(
                    request,
                    startFrame,
                    CharacterAuthorityPoseRegionIds.Default);
                if (matched > 0)
                {
                    issue = "authority pose region missing; default binding used: " + requestRegion;
                    _lastIssue = CharacterAuthorityPoseIssue.DefaultBindingUsed;
                    _lastIssueArgumentId = requestRegion;
                }
            }

            if (matched == 0)
            {
                issue = "authority pose region binding was not found: "
                    + (requestRegion == 0 ? "broadcast" : requestRegion);
                _lastIssue = CharacterAuthorityPoseIssue.BindingNotFound;
                _lastIssueArgumentId = requestRegion;
                return false;
            }

            return true;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(RuntimeFrame frame)
        {
            for (int i = 0; i < _secondaryStates.Length; i++)
                _secondaryStates[i].Tick(frame);
        }

        public bool TryGetChannelOffset(int channelId, out FixVector3 offset)
        {
            return TryGetOffset(channelId, matchSocket: false, out offset);
        }

        public bool TryGetSocketOffset(int socketId, out FixVector3 offset)
        {
            return TryGetOffset(socketId, matchSocket: true, out offset);
        }

        /// <summary>Contributes the current authority pose without allocating a recovery snapshot.</summary>
        /// <remarks>NoAlloc. The field sequence is identical to AddToHash(CreateSnapshot()).</remarks>
        public void AddStateHash(RuntimeHashAccumulator accumulator, int key)
        {
            if (accumulator == null) return;
            accumulator.BeginObject(key, CharacterAuthorityPoseHashFields.SchemaId, CharacterAuthorityPoseHashFields.SchemaVersion, true);
            int active = 0; for (int i = 0; i < _secondaryStates.Length; i++) if (_secondaryStates[i].HasActiveWork) active++;
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F1, CharacterAuthorityPoseSnapshot.CurrentSchemaVersion);
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F2, active);
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F3, _definition?.KnownSecondaryRegionCount ?? 0);
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F4, _lastResolvedRegionId);
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F5, (int)_lastMatchSource);
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F6, (int)_lastIssue);
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F24, _lastIssueArgumentId);
            accumulator.BeginArray(CharacterAuthorityPoseHashFields.F7, _secondaryStates.Length);
            for (int i = 0; i < _secondaryStates.Length; i++)
            {
                accumulator.BeginElement(i);
                _secondaryStates[i].AddStateHash(accumulator);
                accumulator.EndElement();
            }
            accumulator.EndArray();
            accumulator.EndObject();
        }

        public CharacterAuthorityPoseSnapshot CreateSnapshot()
        {
            var entries = new CharacterAuthoritySecondarySnapshot[_secondaryStates.Length];
            int activeCount = 0;
            for (int i = 0; i < _secondaryStates.Length; i++)
            {
                entries[i] = _secondaryStates[i].CreateSnapshot();
                if (entries[i].Active)
                    activeCount++;
            }

            return new CharacterAuthorityPoseSnapshot
            {
                SchemaVersion = CharacterAuthorityPoseSnapshot.CurrentSchemaVersion,
                ActiveSecondaryCount = activeCount,
                KnownRegionCount = _definition != null ? _definition.KnownSecondaryRegionCount : 0,
                LastResolvedRegionId = _lastResolvedRegionId,
                LastMatchSource = _lastMatchSource,
                LastIssue = _lastIssue,
                LastIssueArgumentId = _lastIssueArgumentId,
                SecondaryBindings = entries
            };
        }

        public void CopySecondaryPoseSamplesTo(List<CharacterAuthoritySecondaryPoseSample> samples)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));

            samples.Clear();
            if (samples.Capacity < _secondaryStates.Length)
                samples.Capacity = _secondaryStates.Length;

            for (int i = 0; i < _secondaryStates.Length; i++)
            {
                CharacterAuthoritySecondaryPoseSample sample =
                    _secondaryStates[i].CreatePoseSample();
                if (sample.HasOffset)
                    samples.Add(sample);
            }
        }

        public void RestoreSnapshot(CharacterAuthorityPoseSnapshot snapshot)
        {
            if (snapshot == null)
            {
                ClearRuntimeState();
                return;
            }

            if (snapshot.SchemaVersion != CharacterAuthorityPoseSnapshot.CurrentSchemaVersion)
                throw new ArgumentException("Authority pose snapshot schema mismatch.", nameof(snapshot));
            CharacterAuthoritySecondarySnapshot[] verified = snapshot.SecondaryBindings;
            if (verified == null || verified.Length != _secondaryStates.Length
                || snapshot.LastResolvedRegionId < 0 || snapshot.LastIssueArgumentId < 0
                || (uint)snapshot.LastMatchSource > (uint)CharacterAuthorityPoseMatchSource.PreviewCommand
                || (uint)snapshot.LastIssue > (uint)CharacterAuthorityPoseIssue.TargetAndLocalPlayerUnavailable)
                throw new ArgumentException("Authority pose snapshot numeric identity is invalid.", nameof(snapshot));
            var seen = new HashSet<int>();
            for (int i = 0; i < verified.Length; i++)
            {
                var entry = verified[i];
                if (entry == null || entry.BindingId <= 0 || !seen.Add(entry.BindingId))
                    throw new ArgumentException("Authority pose snapshot binding is missing or duplicated.", nameof(snapshot));
                bool found = false;
                for (int j = 0; j < _secondaryStates.Length; j++)
                {
                    var binding = _secondaryStates[j].Binding;
                    if (binding.BindingId != entry.BindingId) continue;
                    found = binding.RegionId == entry.RegionId && binding.ChannelId == entry.ChannelId
                        && binding.SocketId == entry.SocketId;
                    break;
                }
                if (!found) throw new ArgumentException("Authority pose snapshot references an unknown binding.", nameof(snapshot));
            }
            _lastResolvedRegionId = snapshot.LastResolvedRegionId;
            _lastMatchSource = snapshot.LastMatchSource;
            _lastIssue = snapshot.LastIssue;
            _lastIssueArgumentId = snapshot.LastIssueArgumentId;
            CharacterAuthoritySecondarySnapshot[] entries =
                snapshot.SecondaryBindings ?? Array.Empty<CharacterAuthoritySecondarySnapshot>();
            for (int i = 0; i < _secondaryStates.Length; i++)
            {
                CharacterAuthoritySecondaryState state = _secondaryStates[i];
                bool restored = false;
                for (int j = 0; j < entries.Length; j++)
                {
                    if (state.Binding.BindingId == entries[j].BindingId)
                    {
                        state.RestoreSnapshot(entries[j]);
                        restored = true;
                        break;
                    }
                }

                if (!restored)
                    state.Clear();
            }
        }

        public void ClearRuntimeState()
        {
            for (int i = 0; i < _secondaryStates.Length; i++)
                _secondaryStates[i].Clear();
            _lastResolvedRegionId = 0;
            _lastMatchSource = CharacterAuthorityPoseMatchSource.None;
            _lastIssue = CharacterAuthorityPoseIssue.None;
            _lastIssueArgumentId = 0;
        }

        private int ApplySecondaryImpulseToMatchingRegion(
            CharacterAuthorityPoseImpulseRequest request,
            RuntimeFrame startFrame,
            int regionId)
        {
            bool broadcast = regionId == 0;
            int matched = 0;
            for (int i = 0; i < _secondaryStates.Length; i++)
            {
                CharacterAuthoritySecondaryState state = _secondaryStates[i];
                if (!broadcast
                    && state.Binding.RegionId != regionId)
                {
                    continue;
                }

                state.ApplyImpulse(request, startFrame);
                matched++;
            }

            return matched;
        }

        private bool TryGetOffset(int id, bool matchSocket, out FixVector3 offset)
        {
            offset = FixVector3.Zero;
            if (id <= 0)
                return false;

            int normalizedId = id;
            bool found = false;
            for (int i = 0; i < _secondaryStates.Length; i++)
            {
                CharacterAuthoritySecondaryState state = _secondaryStates[i];
                int bindingId = matchSocket ? state.Binding.SocketId : state.Binding.ChannelId;
                if (bindingId != normalizedId) continue;

                FixVector3 current = state.Offset;
                if (current.IsZero)
                    continue;

                offset += current;
                found = true;
            }

            return found;
        }

        private sealed class CharacterAuthoritySecondaryState
        {
            public CharacterAuthoritySecondaryState(CharacterAuthoritySecondaryBinding binding)
            {
                Binding = binding ?? throw new ArgumentNullException(nameof(binding));
            }

            public CharacterAuthoritySecondaryBinding Binding { get; }
            public FixVector3 Offset { get; private set; }
            private FixVector3 Velocity { get; set; }
            private FixVector3 PendingForce { get; set; }
            private int RemainingFrames { get; set; }
            private long StartFrame { get; set; }

            public bool HasActiveWork => RemainingFrames > 0
                || !Offset.IsZero
                || !Velocity.IsZero
                || !PendingForce.IsZero;

            public void ApplyImpulse(CharacterAuthorityPoseImpulseRequest request, RuntimeFrame startFrame)
            {
                Fix64 strength = request.Strength * Binding.StrengthScale;
                PendingForce += request.Direction * strength;
                RemainingFrames = Math.Max(RemainingFrames, request.DurationFrames);
                StartFrame = startFrame.Value;
            }

            /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
            public void Tick(RuntimeFrame frame)
            {
                if (frame.Value < StartFrame && RemainingFrames > 0)
                    return;

                if (RemainingFrames > 0)
                {
                    Velocity += PendingForce - (Offset * Binding.Stiffness);
                    Velocity *= Binding.DampingPerFrame;
                    Offset += Velocity;
                    Offset = ClampMagnitude(Offset, Binding.MaxOffset);
                    RemainingFrames--;
                    if (RemainingFrames == 0)
                        PendingForce = FixVector3.Zero;
                    return;
                }

                if (Offset.IsZero && Velocity.IsZero)
                    return;

                Velocity -= Offset * Binding.Stiffness;
                Velocity *= Binding.DampingPerFrame;
                Offset += Velocity;
                Offset = ClampMagnitude(Offset, Binding.MaxOffset);
                if (IsSleeping(Offset) && IsSleeping(Velocity))
                    Clear();
            }

            public void Clear()
            {
                Offset = FixVector3.Zero;
                Velocity = FixVector3.Zero;
                PendingForce = FixVector3.Zero;
                RemainingFrames = 0;
                StartFrame = 0L;
            }

            internal void AddStateHash(RuntimeHashAccumulator accumulator)
            {
                accumulator.AddInt(CharacterAuthorityPoseHashFields.F8, Binding.BindingId);
                accumulator.AddInt(CharacterAuthorityPoseHashFields.F9, Binding.RegionId);
                accumulator.AddInt(CharacterAuthorityPoseHashFields.F10, Binding.ChannelId);
                accumulator.AddInt(CharacterAuthorityPoseHashFields.F11, Binding.SocketId);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F12, Offset.X.RawValue);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F13, Offset.Y.RawValue);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F14, Offset.Z.RawValue);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F15, Velocity.X.RawValue);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F16, Velocity.Y.RawValue);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F17, Velocity.Z.RawValue);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F18, PendingForce.X.RawValue);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F19, PendingForce.Y.RawValue);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F20, PendingForce.Z.RawValue);
                accumulator.AddInt(CharacterAuthorityPoseHashFields.F21, RemainingFrames);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F22, StartFrame);
                accumulator.AddInt(CharacterAuthorityPoseHashFields.F23, HasActiveWork ? 1 : 0);
            }

            public CharacterAuthoritySecondarySnapshot CreateSnapshot()
            {
                return new CharacterAuthoritySecondarySnapshot
                {
                    BindingId = Binding.BindingId,
                    RegionId = Binding.RegionId,
                    ChannelId = Binding.ChannelId,
                    SocketId = Binding.SocketId,
                    OffsetXRaw = Offset.X.RawValue,
                    OffsetYRaw = Offset.Y.RawValue,
                    OffsetZRaw = Offset.Z.RawValue,
                    VelocityXRaw = Velocity.X.RawValue,
                    VelocityYRaw = Velocity.Y.RawValue,
                    VelocityZRaw = Velocity.Z.RawValue,
                    PendingForceXRaw = PendingForce.X.RawValue,
                    PendingForceYRaw = PendingForce.Y.RawValue,
                    PendingForceZRaw = PendingForce.Z.RawValue,
                    RemainingFrames = RemainingFrames,
                    StartFrame = StartFrame,
                    Active = HasActiveWork
                };
            }

            public CharacterAuthoritySecondaryPoseSample CreatePoseSample()
            {
                return new CharacterAuthoritySecondaryPoseSample(
                    Binding.ChannelId,
                    Binding.SocketId,
                    Offset.X.RawValue,
                    Offset.Y.RawValue,
                    Offset.Z.RawValue);
            }

            public void RestoreSnapshot(CharacterAuthoritySecondarySnapshot snapshot)
            {
                if (snapshot == null)
                {
                    Clear();
                    return;
                }

                Offset = FromRawVector(snapshot.OffsetXRaw, snapshot.OffsetYRaw, snapshot.OffsetZRaw);
                Velocity = FromRawVector(snapshot.VelocityXRaw, snapshot.VelocityYRaw, snapshot.VelocityZRaw);
                PendingForce = FromRawVector(
                    snapshot.PendingForceXRaw,
                    snapshot.PendingForceYRaw,
                    snapshot.PendingForceZRaw);
                RemainingFrames = snapshot.RemainingFrames > 0 ? snapshot.RemainingFrames : 0;
                StartFrame = snapshot.StartFrame;
            }

            private static FixVector3 ClampMagnitude(FixVector3 value, Fix64 maxMagnitude)
            {
                if (value.IsZero || maxMagnitude <= Fix64.Zero)
                    return FixVector3.Zero;

                Fix64 maxSquared = maxMagnitude * maxMagnitude;
                if (value.LengthSquared() <= maxSquared)
                    return value;

                return value.TryNormalize(out FixVector3 normalized)
                    ? normalized * maxMagnitude
                    : FixVector3.Zero;
            }

            private static bool IsSleeping(FixVector3 value)
            {
                if (value.IsZero)
                    return true;

                Fix64 thresholdSquared = SleepOffsetThreshold * SleepOffsetThreshold;
                return value.LengthSquared() <= thresholdSquared;
            }

            private static FixVector3 FromRawVector(long x, long y, long z)
            {
                return new FixVector3(
                    Fix64.FromRaw(x),
                    Fix64.FromRaw(y),
                    Fix64.FromRaw(z));
            }
        }
    }

    public static class CharacterAuthorityTwoBoneIkSolver
    {
        public static bool TrySolve(
            CharacterAuthorityTwoBoneIkBinding binding,
            FixVector3 rootPosition,
            FixVector3 targetPosition,
            out CharacterAuthorityTwoBoneIkSolveResult result)
        {
            result = CharacterAuthorityTwoBoneIkSolveResult.Failed("binding is missing");
            if (binding == null
                || binding.RootToMidLength <= Fix64.Zero
                || binding.MidToTipLength <= Fix64.Zero)
            {
                return false;
            }

            FixVector3 rootToTarget = targetPosition - rootPosition;
            if (!rootToTarget.TryNormalize(out FixVector3 aim))
            {
                result = CharacterAuthorityTwoBoneIkSolveResult.Failed("target overlaps root");
                return false;
            }

            Fix64 requestedDistance = rootToTarget.LengthSquared().Sqrt();
            Fix64 maxReach = Fix64.Min(
                binding.RootToMidLength + binding.MidToTipLength,
                binding.ReachClamp);
            Fix64 safeMinimumReach = Fix64.Min(binding.MinimumReach, maxReach);
            Fix64 safeDistance = Fix64.Clamp(requestedDistance, safeMinimumReach, maxReach);
            FixVector3 solvedTip = rootPosition + aim * safeDistance;
            Fix64 a = binding.RootToMidLength;
            Fix64 b = binding.MidToTipLength;
            Fix64 x = ((a * a) - (b * b) + (safeDistance * safeDistance))
                / (safeDistance * Fix64.FromInt(2));
            Fix64 heightSquared = (a * a) - (x * x);
            Fix64 height = heightSquared > Fix64.Zero ? heightSquared.Sqrt() : Fix64.Zero;
            if (!TryResolveBendAxis(aim, binding.PoleDirection, out FixVector3 bendAxis))
            {
                result = CharacterAuthorityTwoBoneIkSolveResult.Failed("pole is degenerate");
                return false;
            }
            FixVector3 solvedMid = rootPosition + (aim * x) + (bendAxis * height);
            result = new CharacterAuthorityTwoBoneIkSolveResult(
                true,
                solvedMid,
                solvedTip,
                requestedDistance > maxReach || requestedDistance < safeMinimumReach,
                string.Empty);
            return true;
        }

        private static bool TryResolveBendAxis(FixVector3 aim, FixVector3 pole, out FixVector3 bendAxis)
        {
            Fix64 projection = pole.Dot(aim);
            FixVector3 planarPole = pole - (aim * projection);
            if (planarPole.TryNormalize(out bendAxis))
                return true;

            FixVector3 fallback = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
            projection = fallback.Dot(aim);
            planarPole = fallback - (aim * projection);
            if (planarPole.TryNormalize(out bendAxis))
                return true;

            fallback = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
            projection = fallback.Dot(aim);
            planarPole = fallback - (aim * projection);
            return planarPole.TryNormalize(out bendAxis);
        }
    }

    public readonly struct CharacterAuthorityTwoBoneIkSolveResult
    {
        public CharacterAuthorityTwoBoneIkSolveResult(
            bool solved,
            FixVector3 midPosition,
            FixVector3 tipPosition,
            bool targetClamped,
            string issue)
        {
            Solved = solved;
            MidPosition = midPosition;
            TipPosition = tipPosition;
            TargetClamped = targetClamped;
            Issue = issue ?? string.Empty;
        }

        public bool Solved { get; }
        public FixVector3 MidPosition { get; }
        public FixVector3 TipPosition { get; }
        public bool TargetClamped { get; }
        public string Issue { get; }

        public static CharacterAuthorityTwoBoneIkSolveResult Failed(string issue)
        {
            return new CharacterAuthorityTwoBoneIkSolveResult(
                false,
                FixVector3.Zero,
                FixVector3.Zero,
                false,
                issue);
        }
    }

    public sealed class CharacterAuthorityPoseSnapshot
    {
        public const int CurrentSchemaVersion = 2;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public int ActiveSecondaryCount { get; set; }
        public int KnownRegionCount { get; set; }
        public int LastResolvedRegionId { get; set; }
        public CharacterAuthorityPoseMatchSource LastMatchSource { get; set; }
        public CharacterAuthorityPoseIssue LastIssue { get; set; }
        public int LastIssueArgumentId { get; set; }
        public CharacterAuthoritySecondarySnapshot[] SecondaryBindings { get; set; } =
            Array.Empty<CharacterAuthoritySecondarySnapshot>();

        public static CharacterAuthorityPoseSnapshot Empty(CharacterAuthorityPoseIssue issue = CharacterAuthorityPoseIssue.None)
        {
            return new CharacterAuthorityPoseSnapshot
            {
                SchemaVersion = CurrentSchemaVersion,
                ActiveSecondaryCount = 0,
                KnownRegionCount = 0,
                LastIssue = issue,
                SecondaryBindings = Array.Empty<CharacterAuthoritySecondarySnapshot>()
            };
        }
    }

    public sealed class CharacterAuthoritySecondarySnapshot
    {
        public int BindingId { get; set; }
        public int RegionId { get; set; }
        public int ChannelId { get; set; }
        public int SocketId { get; set; }
        public long OffsetXRaw { get; set; }
        public long OffsetYRaw { get; set; }
        public long OffsetZRaw { get; set; }
        public long VelocityXRaw { get; set; }
        public long VelocityYRaw { get; set; }
        public long VelocityZRaw { get; set; }
        public long PendingForceXRaw { get; set; }
        public long PendingForceYRaw { get; set; }
        public long PendingForceZRaw { get; set; }
        public int RemainingFrames { get; set; }
        public long StartFrame { get; set; }
        public bool Active { get; set; }
    }

    public readonly struct CharacterAuthoritySecondaryPoseSample
    {
        public CharacterAuthoritySecondaryPoseSample(
            int channelId,
            int socketId,
            long offsetXRaw,
            long offsetYRaw,
            long offsetZRaw)
        {
            ChannelId = channelId;
            SocketId = socketId;
            OffsetXRaw = offsetXRaw;
            OffsetYRaw = offsetYRaw;
            OffsetZRaw = offsetZRaw;
        }

        public int ChannelId { get; }
        public int SocketId { get; }
        public long OffsetXRaw { get; }
        public long OffsetYRaw { get; }
        public long OffsetZRaw { get; }
        public bool HasOffset => OffsetXRaw != 0L || OffsetYRaw != 0L || OffsetZRaw != 0L;
    }

    public static class CharacterAuthorityPoseHashUtility
    {
        public static void AddToHash(
            RuntimeHashAccumulator accumulator,
            int key,
            CharacterAuthorityPoseSnapshot snapshot)
        {
            if (accumulator == null)
                return;

            accumulator.BeginObject(key, CharacterAuthorityPoseHashFields.SchemaId, CharacterAuthorityPoseHashFields.SchemaVersion, snapshot != null);
            if (snapshot == null) { accumulator.EndObject(); return; }
            CharacterAuthorityPoseSnapshot safeSnapshot = snapshot;
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F1, safeSnapshot.SchemaVersion);
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F2, safeSnapshot.ActiveSecondaryCount);
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F3, safeSnapshot.KnownRegionCount);
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F4, safeSnapshot.LastResolvedRegionId);
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F5, (int)safeSnapshot.LastMatchSource);
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F6, (int)safeSnapshot.LastIssue);
            accumulator.AddInt(CharacterAuthorityPoseHashFields.F24, safeSnapshot.LastIssueArgumentId);
            CharacterAuthoritySecondarySnapshot[] entries =
                safeSnapshot.SecondaryBindings ?? Array.Empty<CharacterAuthoritySecondarySnapshot>();
            accumulator.BeginArray(CharacterAuthorityPoseHashFields.F7, entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                CharacterAuthoritySecondarySnapshot entry = entries[i];
                accumulator.BeginElement(i);
                accumulator.AddInt(CharacterAuthorityPoseHashFields.F8, entry.BindingId);
                accumulator.AddInt(CharacterAuthorityPoseHashFields.F9, entry.RegionId);
                accumulator.AddInt(CharacterAuthorityPoseHashFields.F10, entry.ChannelId);
                accumulator.AddInt(CharacterAuthorityPoseHashFields.F11, entry.SocketId);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F12, entry.OffsetXRaw);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F13, entry.OffsetYRaw);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F14, entry.OffsetZRaw);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F15, entry.VelocityXRaw);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F16, entry.VelocityYRaw);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F17, entry.VelocityZRaw);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F18, entry.PendingForceXRaw);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F19, entry.PendingForceYRaw);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F20, entry.PendingForceZRaw);
                accumulator.AddInt(CharacterAuthorityPoseHashFields.F21, entry.RemainingFrames);
                accumulator.AddLong(CharacterAuthorityPoseHashFields.F22, entry.StartFrame);
                accumulator.AddInt(CharacterAuthorityPoseHashFields.F23, entry.Active ? 1 : 0);
                accumulator.EndElement();
            }
            accumulator.EndArray();
            accumulator.EndObject();
        }
    }
}

namespace MxFramework.CharacterRuntimeSpawn
{
    // Frozen v3 protocol numbers. Retired entries remain reserved.
    internal static class CharacterAuthorityPoseHashFields
    {
        internal const int SchemaId = RuntimeHashSchemaIds.CharacterAuthorityPose;
        internal const int SchemaVersion = 1;
        internal const int F1 = 1; // .schema
        internal const int F2 = 2; // .activeSecondary
        internal const int F3 = 3; // .knownRegions
        internal const int F4 = 4; // .lastRegion
        internal const int F5 = 5; // .lastMatch
        internal const int F6 = 6; // .lastIssue
        internal const int F7 = 7; // .secondary.count
        internal const int F8 = 8; // .binding
        internal const int F9 = 9; // .region
        internal const int F10 = 10; // .channel
        internal const int F11 = 11; // .socket
        internal const int F12 = 12; // .offset.x.raw
        internal const int F13 = 13; // .offset.y.raw
        internal const int F14 = 14; // .offset.z.raw
        internal const int F15 = 15; // .velocity.x.raw
        internal const int F16 = 16; // .velocity.y.raw
        internal const int F17 = 17; // .velocity.z.raw
        internal const int F18 = 18; // .force.x.raw
        internal const int F19 = 19; // .force.y.raw
        internal const int F20 = 20; // .force.z.raw
        internal const int F21 = 21; // .remaining
        internal const int F22 = 22; // .start
        internal const int F23 = 23; // .active
        internal const int F24 = 24; // .lastIssueArgument
    }
}
