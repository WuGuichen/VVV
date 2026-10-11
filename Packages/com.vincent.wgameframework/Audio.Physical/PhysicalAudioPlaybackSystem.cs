using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Audio.Physical
{
    internal enum PhysicalAudioCooldownAdmission
    {
        Available = 0,
        Cooldown = 1,
        CapacityFull = 2,
    }

    /// <summary>Fixed-capacity cooldown lookup that only reuses free or expired storage.</summary>
    /// <remarks>NoAlloc after construction. Active cooldown entries are never evicted.</remarks>
    internal sealed class PhysicalAudioFixedCooldownCache<TKey>
        where TKey : struct, IEquatable<TKey>
    {
        private readonly Dictionary<TKey, int> _slotByKey;
        private readonly TKey[] _keys;
        private readonly long[] _expiresAt;
        private readonly bool[] _occupied;

        public PhysicalAudioFixedCooldownCache(int capacity)
        {
            _slotByKey = new Dictionary<TKey, int>(capacity);
            _keys = capacity == 0 ? Array.Empty<TKey>() : new TKey[capacity];
            _expiresAt = capacity == 0 ? Array.Empty<long>() : new long[capacity];
            _occupied = capacity == 0 ? Array.Empty<bool>() : new bool[capacity];
        }

        public bool TryGetValue(TKey key, out long frame)
        {
            if (_slotByKey.TryGetValue(key, out int slot))
            {
                frame = _expiresAt[slot];
                return true;
            }

            frame = 0L;
            return false;
        }

        public PhysicalAudioCooldownAdmission Evaluate(TKey key, long frame, int cooldownFrames)
        {
            if (_slotByKey.TryGetValue(key, out int existingSlot))
            {
                return frame < _expiresAt[existingSlot]
                    ? PhysicalAudioCooldownAdmission.Cooldown
                    : PhysicalAudioCooldownAdmission.Available;
            }

            return FindAvailableOrExpiredSlot(frame, cooldownFrames) >= 0
                ? PhysicalAudioCooldownAdmission.Available
                : PhysicalAudioCooldownAdmission.CapacityFull;
        }

        public bool Record(TKey key, long frame, int cooldownFrames)
        {
            if (_keys.Length == 0)
                return false;
            if (_slotByKey.TryGetValue(key, out int existingSlot))
            {
                _expiresAt[existingSlot] = SaturatingAdd(frame, cooldownFrames);
                return true;
            }

            int targetSlot = FindAvailableOrExpiredSlot(frame, cooldownFrames);
            if (targetSlot < 0)
                return false;
            if (_occupied[targetSlot])
                _slotByKey.Remove(_keys[targetSlot]);
            _keys[targetSlot] = key;
            _expiresAt[targetSlot] = SaturatingAdd(frame, cooldownFrames);
            _occupied[targetSlot] = true;
            _slotByKey.Add(key, targetSlot);
            return true;
        }

        public void Clear()
        {
            _slotByKey.Clear();
            Array.Clear(_occupied, 0, _occupied.Length);
        }

        private int FindAvailableOrExpiredSlot(long frame, int cooldownFrames)
        {
            int oldestSlot = -1;
            long oldestExpiry = long.MaxValue;
            for (int i = 0; i < _occupied.Length; i++)
            {
                if (!_occupied[i])
                    return i;
                if (frame >= _expiresAt[i] && _expiresAt[i] < oldestExpiry)
                {
                    oldestExpiry = _expiresAt[i];
                    oldestSlot = i;
                }
            }

            return oldestSlot;
        }

        private static long SaturatingAdd(long frame, int cooldownFrames)
        {
            long duration = cooldownFrames > 0 ? cooldownFrames : 0L;
            return frame > long.MaxValue - duration ? long.MaxValue : frame + duration;
        }
    }

    /// <summary>
    /// Defines optional audio parameter ids used to map physical contact facts into one-shot playback controls.
    /// </summary>
    /// <remarks>NoAlloc. This value type only carries stable parameter ids and deterministic normalization ranges.</remarks>
    public readonly struct PhysicalAudioParameterMapping
    {
        public static readonly PhysicalAudioParameterMapping None = default;

        public PhysicalAudioParameterMapping(
            int volumeParameterId,
            int pitchParameterId,
            int lowPassParameterId,
            int brightnessParameterId,
            int scrapeParameterId,
            int heavySpeedMillimetersPerSecond = 3200,
            int heavyPressure = 256000)
        {
            if (volumeParameterId < 0)
                throw new ArgumentOutOfRangeException(nameof(volumeParameterId), "Physical audio volume parameter id cannot be negative.");
            if (pitchParameterId < 0)
                throw new ArgumentOutOfRangeException(nameof(pitchParameterId), "Physical audio pitch parameter id cannot be negative.");
            if (lowPassParameterId < 0)
                throw new ArgumentOutOfRangeException(nameof(lowPassParameterId), "Physical audio low-pass parameter id cannot be negative.");
            if (brightnessParameterId < 0)
                throw new ArgumentOutOfRangeException(nameof(brightnessParameterId), "Physical audio brightness parameter id cannot be negative.");
            if (scrapeParameterId < 0)
                throw new ArgumentOutOfRangeException(nameof(scrapeParameterId), "Physical audio scrape parameter id cannot be negative.");
            if (heavySpeedMillimetersPerSecond <= 0)
                throw new ArgumentOutOfRangeException(nameof(heavySpeedMillimetersPerSecond), "Physical audio heavy speed must be positive.");
            if (heavyPressure <= 0)
                throw new ArgumentOutOfRangeException(nameof(heavyPressure), "Physical audio heavy pressure must be positive.");

            VolumeParameterId = volumeParameterId;
            PitchParameterId = pitchParameterId;
            LowPassParameterId = lowPassParameterId;
            BrightnessParameterId = brightnessParameterId;
            ScrapeParameterId = scrapeParameterId;
            HeavySpeedMillimetersPerSecond = heavySpeedMillimetersPerSecond;
            HeavyPressure = heavyPressure;
            ParameterCount = CountParameters(
                volumeParameterId,
                pitchParameterId,
                lowPassParameterId,
                brightnessParameterId,
                scrapeParameterId);
        }

        public int VolumeParameterId { get; }

        public int PitchParameterId { get; }

        public int LowPassParameterId { get; }

        public int BrightnessParameterId { get; }

        public int ScrapeParameterId { get; }

        public int HeavySpeedMillimetersPerSecond { get; }

        public int HeavyPressure { get; }

        public int ParameterCount { get; }

        public bool HasParameters => ParameterCount > 0;

        private static int CountParameters(
            int volumeParameterId,
            int pitchParameterId,
            int lowPassParameterId,
            int brightnessParameterId,
            int scrapeParameterId)
        {
            int count = 0;
            if (volumeParameterId > 0)
                count++;
            if (pitchParameterId > 0)
                count++;
            if (lowPassParameterId > 0)
                count++;
            if (brightnessParameterId > 0)
                count++;
            if (scrapeParameterId > 0)
                count++;
            return count;
        }
    }

    /// <summary>
    /// Converts Combat physical-contact events into 3D one-shot audio requests.
    /// </summary>
    /// <remarks>NoAlloc after construction when cooldown key capacity is pre-sized for active contacts.</remarks>
    public sealed class PhysicalAudioPlaybackSystem
    {
        private static readonly AudioTraceIdentity BodyPartContactTraceId = new AudioTraceIdentity(AudioTraceKindIds.ContactBodyPart);
        private static readonly AudioTraceIdentity LandingTraceId = new AudioTraceIdentity(AudioTraceKindIds.ContactLanding);
        private static readonly AudioTraceIdentity ObjectImpactTraceId = new AudioTraceIdentity(AudioTraceKindIds.ContactObjectImpact);

        private readonly IAudioService _audioService;
        private readonly PhysicalAudioCueRuleTable _ruleTable;
        private readonly PhysicalAudioFixedCooldownCache<CooldownKey> _lastPlayedFrames;
        private readonly PhysicalAudioParameterMapping _parameterMapping;
        private readonly AudioParameterValue[] _parameterValues;

        public PhysicalAudioPlaybackSystem(
            IAudioService audioService,
            PhysicalAudioCueRuleTable ruleTable,
            int cooldownCapacity = 64,
            PhysicalAudioParameterMapping parameterMapping = default)
        {
            if (cooldownCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownCapacity), "Physical audio cooldown capacity cannot be negative.");
            }

            _audioService = audioService;
            _ruleTable = ruleTable ?? PhysicalAudioCueRuleTable.Empty;
            _lastPlayedFrames = new PhysicalAudioFixedCooldownCache<CooldownKey>(cooldownCapacity);
            _parameterMapping = parameterMapping;
            _parameterValues = parameterMapping.HasParameters
                ? new AudioParameterValue[parameterMapping.ParameterCount]
                : AudioPlayRequest.EmptyParameters;
        }

        public PhysicalAudioPlaybackSummary LastSummary { get; private set; } =
            PhysicalAudioPlaybackSummary.Empty;

        public void ResetCooldowns()
        {
            _lastPlayedFrames.Clear();
        }

        /// <summary>
        /// Converts one frame of Combat contact events into audio one-shot requests.
        /// </summary>
        /// <remarks>NoAlloc after construction when cooldown key capacity is pre-sized for active contacts.</remarks>
        public PhysicalAudioPlaybackSummary Tick(CombatPhysicalContactEventBuffer contactEvents, long frame)
        {
            var summary = new PhysicalAudioPlaybackFrameState(
                frame,
                contactEvents == null ? 0 : contactEvents.Count,
                contactEvents == null ? 0 : contactEvents.DroppedCount);
            if (contactEvents == null || contactEvents.Count == 0)
            {
                LastSummary = summary.ToSummary();
                return LastSummary;
            }

            for (int i = 0; i < contactEvents.Count; i++)
            {
                PlayEvent(contactEvents[i], frame, ref summary);
            }

            LastSummary = summary.ToSummary();
            return LastSummary;
        }

        private void PlayEvent(
            in CombatPhysicalContactEvent contactEvent,
            long frame,
            ref PhysicalAudioPlaybackFrameState summary)
        {
            if (contactEvent.Phase != CombatPhysicalContactPhase.Started)
            {
                summary.IgnoredPhaseCount++;
                return;
            }

            if (!_ruleTable.TryResolve(contactEvent, out PhysicalAudioCueRule rule))
            {
                summary.MissingRuleSkipCount++;
                return;
            }

            var cooldownKey = new CooldownKey(
                contactEvent.Kind,
                contactEvent.ContactPairKey,
                contactEvent.SourceSlotId,
                contactEvent.ContactPointId,
                contactEvent.BodyPartId,
                rule.EventId);
            PhysicalAudioCooldownAdmission admission = _lastPlayedFrames.Evaluate(
                cooldownKey,
                frame,
                rule.CooldownFrames);
            if (admission == PhysicalAudioCooldownAdmission.Cooldown)
            {
                summary.CooldownSkipCount++;
                return;
            }
            if (admission == PhysicalAudioCooldownAdmission.CapacityFull)
            {
                summary.CooldownCapacitySkipCount++;
                return;
            }
            if (_audioService == null)
            {
                summary.MissingAudioServiceSkipCount++;
                return;
            }

            AudioParameterValue[] parameters = BuildParameters(contactEvent);
            AudioPlayRequest request = AudioPlayRequest.Create3D(
                rule.EventId,
                CreateTransform(contactEvent.Point),
                emitterId: ResolveEmitterId(contactEvent),
                parameters: parameters,
                priority: rule.Priority,
                frame: frame,
                traceId: ResolveTraceId(contactEvent.Kind));
            AudioPlayResult result = _audioService.PlayOneShot(request);
            if (!result.Success)
            {
                summary.PlaybackFailedSkipCount++;
                return;
            }

            _lastPlayedFrames.Record(cooldownKey, frame, rule.CooldownFrames);
            summary.PlayedCount++;
            summary.LastEventId = rule.EventId;
            summary.LastEmitterId = request.EmitterId;
            switch (contactEvent.Kind)
            {
                case CombatPhysicalContactKind.BodyPartContact:
                    summary.BodyPartContactPlayedCount++;
                    break;
                case CombatPhysicalContactKind.Landing:
                    summary.LandingPlayedCount++;
                    break;
                case CombatPhysicalContactKind.ObjectImpact:
                    summary.ObjectImpactPlayedCount++;
                    break;
            }
        }

        private AudioParameterValue[] BuildParameters(in CombatPhysicalContactEvent contactEvent)
        {
            if (_parameterValues.Length == 0)
            {
                return AudioPlayRequest.EmptyParameters;
            }

            float relativeSpeed = ClampToUnit(
                contactEvent.RelativeSpeedMillimetersPerSecond,
                _parameterMapping.HeavySpeedMillimetersPerSecond);
            float normalSpeed = ClampToUnit(
                contactEvent.NormalSpeedMillimetersPerSecond,
                _parameterMapping.HeavySpeedMillimetersPerSecond);
            float tangentialSpeed = ClampToUnit(
                contactEvent.TangentialSpeedMillimetersPerSecond,
                _parameterMapping.HeavySpeedMillimetersPerSecond);
            float pressure = ClampToUnit(contactEvent.Pressure, _parameterMapping.HeavyPressure);
            float hardness = ResolveMaterialHardness(contactEvent.SurfaceMaterial.Kind);

            float volume = Clamp01(0.22f + pressure * 0.45f + normalSpeed * 0.25f + tangentialSpeed * 0.08f);
            float pitch = Clamp(0.72f + relativeSpeed * 0.35f + hardness * 0.16f - pressure * 0.1f, 0.55f, 1.55f);
            float lowPass = Clamp01(0.42f + hardness * 0.34f + relativeSpeed * 0.22f - pressure * 0.12f);
            float brightness = Clamp01(0.24f + hardness * 0.48f + relativeSpeed * 0.12f + normalSpeed * 0.14f);
            float scrape = Clamp01(tangentialSpeed * (1f - normalSpeed * 0.7f));

            int count = 0;
            AddParameter(_parameterValues, ref count, _parameterMapping.VolumeParameterId, volume);
            AddParameter(_parameterValues, ref count, _parameterMapping.PitchParameterId, pitch);
            AddParameter(_parameterValues, ref count, _parameterMapping.LowPassParameterId, lowPass);
            AddParameter(_parameterValues, ref count, _parameterMapping.BrightnessParameterId, brightness);
            AddParameter(_parameterValues, ref count, _parameterMapping.ScrapeParameterId, scrape);
            return _parameterValues;
        }

        private static void AddParameter(
            AudioParameterValue[] parameters,
            ref int count,
            int parameterId,
            float value)
        {
            if (parameterId <= 0)
            {
                return;
            }

            parameters[count] = new AudioParameterValue(parameterId, value);
            count++;
        }

        private static float ResolveMaterialHardness(CombatSurfaceMaterialKind materialKind)
        {
            switch (materialKind)
            {
                case CombatSurfaceMaterialKind.Rough:
                    return 0.6f;
                case CombatSurfaceMaterialKind.HardSlick:
                    return 0.9f;
                case CombatSurfaceMaterialKind.Elastic:
                    return 0.42f;
                case CombatSurfaceMaterialKind.BouncePad:
                    return 0.35f;
                default:
                    return 0.55f;
            }
        }

        private static float ClampToUnit(int value, int reference)
        {
            if (value <= 0 || reference <= 0)
            {
                return 0f;
            }

            return value >= reference ? 1f : value / (float)reference;
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value <= min)
            {
                return min;
            }

            return value >= max ? max : value;
        }

        private static AudioTransform CreateTransform(FixVector3 point)
        {
            return new AudioTransform(
                ToMeters(point.X),
                ToMeters(point.Y),
                ToMeters(point.Z),
                forwardX: 0f,
                forwardY: 0f,
                forwardZ: 1f);
        }

        private static int ResolveEmitterId(in CombatPhysicalContactEvent contactEvent)
        {
            if (!contactEvent.SourceEntityId.IsNone)
            {
                if (contactEvent.Kind == CombatPhysicalContactKind.BodyPartContact
                    && (contactEvent.ContactPointId > 0 || contactEvent.BodyPartId > 0))
                {
                    return CreateStableBodyPartEmitterId(
                        contactEvent.SourceEntityId.Value,
                        contactEvent.ContactPointId,
                        contactEvent.BodyPartId);
                }

                return contactEvent.SourceEntityId.Value;
            }

            if (!contactEvent.SourceBodyId.IsNone)
            {
                return contactEvent.SourceBodyId.Value;
            }

            return contactEvent.TargetBodyId.Value;
        }

        private static int CreateStableBodyPartEmitterId(
            int sourceEntityId,
            int contactPointId,
            int bodyPartId)
        {
            unchecked
            {
                int hash = 37;
                hash = (hash * 397) ^ sourceEntityId;
                hash = (hash * 397) ^ contactPointId;
                hash = (hash * 397) ^ bodyPartId;
                hash &= int.MaxValue;
                return hash == 0 ? sourceEntityId : hash;
            }
        }

        private static AudioTraceIdentity ResolveTraceId(CombatPhysicalContactKind kind)
        {
            switch (kind)
            {
                case CombatPhysicalContactKind.BodyPartContact:
                    return BodyPartContactTraceId;
                case CombatPhysicalContactKind.Landing:
                    return LandingTraceId;
                case CombatPhysicalContactKind.ObjectImpact:
                    return ObjectImpactTraceId;
                default:
                    return AudioTraceIdentity.None;
            }
        }

        private static float ToMeters(Fix64 meters)
        {
            return (float)((double)meters.RawValue / Fix64.Scale);
        }

        private readonly struct CooldownKey : IEquatable<CooldownKey>
        {
            public CooldownKey(
                CombatPhysicalContactKind kind,
                int contactPairKey,
                int sourceSlotId,
                int contactPointId,
                int bodyPartId,
                int eventId)
            {
                Kind = kind;
                ContactPairKey = contactPairKey;
                SourceSlotId = sourceSlotId;
                ContactPointId = contactPointId;
                BodyPartId = bodyPartId;
                EventId = eventId;
            }

            public CombatPhysicalContactKind Kind { get; }

            public int ContactPairKey { get; }

            public int SourceSlotId { get; }

            public int ContactPointId { get; }

            public int BodyPartId { get; }

            public int EventId { get; }

            public bool Equals(CooldownKey other)
            {
                return Kind == other.Kind
                    && ContactPairKey == other.ContactPairKey
                    && SourceSlotId == other.SourceSlotId
                    && ContactPointId == other.ContactPointId
                    && BodyPartId == other.BodyPartId
                    && EventId == other.EventId;
            }

            public override bool Equals(object obj)
            {
                return obj is CooldownKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)Kind;
                    hash = (hash * 397) ^ ContactPairKey;
                    hash = (hash * 397) ^ SourceSlotId;
                    hash = (hash * 397) ^ ContactPointId;
                    hash = (hash * 397) ^ BodyPartId;
                    hash = (hash * 397) ^ EventId;
                    return hash;
                }
            }
        }

        private struct PhysicalAudioPlaybackFrameState
        {
            public PhysicalAudioPlaybackFrameState(long frame, int contactEventCount, int droppedContactEventCount)
            {
                Frame = frame < 0 ? 0 : frame;
                ContactEventCount = contactEventCount < 0 ? 0 : contactEventCount;
                DroppedContactEventCount = droppedContactEventCount < 0 ? 0 : droppedContactEventCount;
                PlayedCount = 0;
                BodyPartContactPlayedCount = 0;
                LandingPlayedCount = 0;
                ObjectImpactPlayedCount = 0;
                IgnoredPhaseCount = 0;
                CooldownSkipCount = 0;
                CooldownCapacitySkipCount = 0;
                MissingRuleSkipCount = 0;
                MissingAudioServiceSkipCount = 0;
                PlaybackFailedSkipCount = 0;
                LastEventId = 0;
                LastEmitterId = 0;
            }

            public long Frame;
            public int ContactEventCount;
            public int DroppedContactEventCount;
            public int PlayedCount;
            public int BodyPartContactPlayedCount;
            public int LandingPlayedCount;
            public int ObjectImpactPlayedCount;
            public int IgnoredPhaseCount;
            public int CooldownSkipCount;
            public int CooldownCapacitySkipCount;
            public int MissingRuleSkipCount;
            public int MissingAudioServiceSkipCount;
            public int PlaybackFailedSkipCount;
            public int LastEventId;
            public int LastEmitterId;

            public PhysicalAudioPlaybackSummary ToSummary()
            {
                return new PhysicalAudioPlaybackSummary(
                    Frame,
                    ContactEventCount,
                    DroppedContactEventCount,
                    PlayedCount,
                    BodyPartContactPlayedCount,
                    LandingPlayedCount,
                    ObjectImpactPlayedCount,
                    IgnoredPhaseCount,
                    CooldownSkipCount,
                    MissingRuleSkipCount,
                    MissingAudioServiceSkipCount,
                    PlaybackFailedSkipCount,
                    LastEventId,
                    LastEmitterId,
                    CooldownCapacitySkipCount);
            }
        }
    }

    public readonly struct PhysicalAudioPlaybackSummary
    {
        public static readonly PhysicalAudioPlaybackSummary Empty =
            new PhysicalAudioPlaybackSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        public PhysicalAudioPlaybackSummary(
            long frame,
            int contactEventCount,
            int droppedContactEventCount,
            int playedCount,
            int bodyPartContactPlayedCount,
            int landingPlayedCount,
            int objectImpactPlayedCount,
            int ignoredPhaseCount,
            int cooldownSkipCount,
            int missingRuleSkipCount,
            int missingAudioServiceSkipCount,
            int playbackFailedSkipCount,
            int lastEventId,
            int lastEmitterId,
            int cooldownCapacitySkipCount = 0)
        {
            Frame = frame < 0 ? 0 : frame;
            ContactEventCount = contactEventCount < 0 ? 0 : contactEventCount;
            DroppedContactEventCount = droppedContactEventCount < 0 ? 0 : droppedContactEventCount;
            PlayedCount = playedCount < 0 ? 0 : playedCount;
            BodyPartContactPlayedCount = bodyPartContactPlayedCount < 0 ? 0 : bodyPartContactPlayedCount;
            LandingPlayedCount = landingPlayedCount < 0 ? 0 : landingPlayedCount;
            ObjectImpactPlayedCount = objectImpactPlayedCount < 0 ? 0 : objectImpactPlayedCount;
            IgnoredPhaseCount = ignoredPhaseCount < 0 ? 0 : ignoredPhaseCount;
            CooldownSkipCount = cooldownSkipCount < 0 ? 0 : cooldownSkipCount;
            CooldownCapacitySkipCount = cooldownCapacitySkipCount < 0 ? 0 : cooldownCapacitySkipCount;
            MissingRuleSkipCount = missingRuleSkipCount < 0 ? 0 : missingRuleSkipCount;
            MissingAudioServiceSkipCount = missingAudioServiceSkipCount < 0 ? 0 : missingAudioServiceSkipCount;
            PlaybackFailedSkipCount = playbackFailedSkipCount < 0 ? 0 : playbackFailedSkipCount;
            LastEventId = lastEventId;
            LastEmitterId = lastEmitterId;
        }

        public long Frame { get; }

        public int ContactEventCount { get; }

        public int DroppedContactEventCount { get; }

        public int PlayedCount { get; }

        public int BodyPartContactPlayedCount { get; }

        public int LandingPlayedCount { get; }

        public int ObjectImpactPlayedCount { get; }

        public int IgnoredPhaseCount { get; }

        public int CooldownSkipCount { get; }

        public int CooldownCapacitySkipCount { get; }

        public int MissingRuleSkipCount { get; }

        public int MissingAudioServiceSkipCount { get; }

        public int PlaybackFailedSkipCount { get; }

        public int SkippedCount =>
            CooldownSkipCount + CooldownCapacitySkipCount + MissingRuleSkipCount + MissingAudioServiceSkipCount
            + PlaybackFailedSkipCount;

        public int LastEventId { get; }

        public int LastEmitterId { get; }
    }
}
