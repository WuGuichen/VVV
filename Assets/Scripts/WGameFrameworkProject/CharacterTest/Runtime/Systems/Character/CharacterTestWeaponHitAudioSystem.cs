using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestWeaponHitAudioSystem
    {
        private const int DefaultCooldownFrames = 6;
        private const int CooldownCapacity = 64;
        private const int StandaloneAudioIntentCapacity = 64;
        private static readonly AudioTraceIdentity WeaponHitTraceId = new AudioTraceIdentity(AudioTraceKindIds.WeaponHit);
        private static readonly AudioTraceIdentity ResolvedWeaponHitTraceId = new AudioTraceIdentity(AudioTraceKindIds.ResolvedWeaponHit);

        private static readonly CharacterTestWeaponHitAudioRule[] DefaultRules =
        {
            new CharacterTestWeaponHitAudioRule(
                HitResolveKind.Blocked,
                CombatSurfaceMaterialKind.Default,
                CharacterTestAudioCueCatalog.WeaponHitBlockedCueKey,
                DefaultCooldownFrames,
                priority: 18),
            new CharacterTestWeaponHitAudioRule(
                HitResolveKind.Damage,
                CombatSurfaceMaterialKind.HardSlick,
                CharacterTestAudioCueCatalog.WeaponHitHardSlickCueKey,
                DefaultCooldownFrames,
                priority: 14),
            new CharacterTestWeaponHitAudioRule(
                HitResolveKind.Damage,
                CombatSurfaceMaterialKind.Rough,
                CharacterTestAudioCueCatalog.WeaponHitRoughCueKey,
                DefaultCooldownFrames,
                priority: 12),
            new CharacterTestWeaponHitAudioRule(
                HitResolveKind.Damage,
                CombatSurfaceMaterialKind.Elastic,
                CharacterTestAudioCueCatalog.WeaponHitElasticCueKey,
                DefaultCooldownFrames,
                priority: 12),
            new CharacterTestWeaponHitAudioRule(
                HitResolveKind.Damage,
                CombatSurfaceMaterialKind.BouncePad,
                CharacterTestAudioCueCatalog.WeaponHitElasticCueKey,
                DefaultCooldownFrames,
                priority: 12),
            new CharacterTestWeaponHitAudioRule(
                HitResolveKind.Damage,
                CombatSurfaceMaterialKind.Default,
                CharacterTestAudioCueCatalog.WeaponHitDefaultCueKey,
                DefaultCooldownFrames,
                priority: 10)
        };

        private readonly CharacterTestAudioCuePlaybackSystem _audioCuePlaybackSystem;
        private readonly ICharacterTestAudioCueCatalog _audioCueCatalog;
        private readonly PhysicalAudioResolver _physicalAudioResolver;
        private readonly IAcousticBodyPropertyProvider _propertyProvider;
        private readonly CharacterTestWeaponHitAudioRule[] _rules;
        private readonly CharacterTestWeaponHitAudioRuleBinding[] _ruleBindings;
        private readonly CharacterTestAudioIntentQueue _audioIntentQueue;
        private readonly CharacterTestAudioIntentDrainSystem _standaloneAudioIntentDrainSystem;
        private readonly bool _useStandaloneAudioIntentQueue;
        private readonly CharacterTestFixedCooldownCache<CooldownKey> _lastPlayedFrames =
            new CharacterTestFixedCooldownCache<CooldownKey>(CooldownCapacity);
        private readonly AudioParameterValue[] _parameterValues =
            new AudioParameterValue[CharacterTestAudioIntent.MaxParameterCount];

        public CharacterTestWeaponHitAudioSystem(CharacterTestAudioCuePlaybackSystem audioCuePlaybackSystem)
            : this(audioCuePlaybackSystem, DefaultRules, null, null)
        {
        }

        internal CharacterTestWeaponHitAudioSystem(
            CharacterTestAudioCuePlaybackSystem audioCuePlaybackSystem,
            CharacterTestWeaponHitAudioRule[] rules)
            : this(audioCuePlaybackSystem, rules, null, null)
        {
        }

        internal CharacterTestWeaponHitAudioSystem(
            CharacterTestAudioCuePlaybackSystem audioCuePlaybackSystem,
            CharacterTestAudioIntentQueue audioIntentQueue)
            : this(audioCuePlaybackSystem, DefaultRules, audioIntentQueue, null)
        {
        }

        internal CharacterTestWeaponHitAudioSystem(
            CharacterTestAudioCuePlaybackSystem audioCuePlaybackSystem,
            CharacterTestAudioIntentQueue audioIntentQueue,
            IAcousticBodyPropertyProvider propertyProvider)
            : this(audioCuePlaybackSystem, DefaultRules, audioIntentQueue, propertyProvider)
        {
        }

        internal CharacterTestWeaponHitAudioSystem(
            CharacterTestAudioCuePlaybackSystem audioCuePlaybackSystem,
            CharacterTestWeaponHitAudioRule[] rules,
            CharacterTestAudioIntentQueue audioIntentQueue,
            IAcousticBodyPropertyProvider propertyProvider = null)
        {
            _audioCuePlaybackSystem = audioCuePlaybackSystem;
            _audioCueCatalog = audioCuePlaybackSystem?.Catalog;
            if (_audioCueCatalog?.PhysicalAudioDefinitions != null)
                _physicalAudioResolver = new PhysicalAudioResolver(_audioCueCatalog.PhysicalAudioDefinitions);
            _propertyProvider = propertyProvider;
            _rules = rules == null || rules.Length == 0 ? DefaultRules : rules;
            _ruleBindings = BuildRuleBindings(_audioCueCatalog, _rules);
            _audioIntentQueue = audioIntentQueue ?? new CharacterTestAudioIntentQueue(StandaloneAudioIntentCapacity);
            _useStandaloneAudioIntentQueue = audioIntentQueue == null;
            _standaloneAudioIntentDrainSystem = _useStandaloneAudioIntentQueue
                ? new CharacterTestAudioIntentDrainSystem(audioCuePlaybackSystem)
                : null;
            LastSummary = CharacterTestWeaponHitAudioSummary.Empty;
        }

        public CharacterTestWeaponHitAudioSummary LastSummary { get; private set; }
        public CharacterTestAudioIntentDrainSummary LastIntentDrainSummary =>
            _standaloneAudioIntentDrainSystem == null
                ? CharacterTestAudioIntentDrainSummary.Empty
                : _standaloneAudioIntentDrainSystem.LastSummary;

        /// <remarks>NoAlloc after initialization when the cooldown dictionary has enough capacity.</remarks>
        internal void Tick(
            IReadOnlyList<CharacterTestWeaponHitAudioEvent> events,
            RuntimeFrame frame)
        {
            var state = new FrameState(frame);
            if (_useStandaloneAudioIntentQueue)
                _audioIntentQueue.Clear();
            if (events == null || events.Count == 0)
            {
                if (_useStandaloneAudioIntentQueue)
                    _standaloneAudioIntentDrainSystem.Tick(_audioIntentQueue, frame);
                LastSummary = state.ToSummary();
                return;
            }

            for (int i = 0; i < events.Count; i++)
                Play(events[i], frame, ref state);

            if (_useStandaloneAudioIntentQueue)
                _standaloneAudioIntentDrainSystem.Tick(_audioIntentQueue, frame);
            LastSummary = state.ToSummary();
        }

        private void Play(
            in CharacterTestWeaponHitAudioEvent evt,
            RuntimeFrame frame,
            ref FrameState state)
        {
            state.ProcessedCount++;
            if (evt.ResultKind != HitResolveKind.Damage && evt.ResultKind != HitResolveKind.Blocked)
            {
                state.IgnoredResultCount++;
                return;
            }

            if (_physicalAudioResolver != null)
            {
                PlayResolved(evt, frame, ref state);
                return;
            }

            if (!TryResolveRule(
                    evt,
                    out CharacterTestWeaponHitAudioRule rule,
                    out CharacterTestWeaponHitAudioRuleBinding ruleBinding))
            {
                state.MissingRuleSkipCount++;
                return;
            }

            if (_audioCuePlaybackSystem == null)
            {
                state.MissingAudioServiceSkipCount++;
                return;
            }

            if (!ruleBinding.IsValid)
            {
                state.MissingCueSkipCount++;
                return;
            }

            var cooldownKey = new CooldownKey(
                evt.SourceEntityId,
                evt.TargetEntityId,
                evt.ResultKind,
                ruleBinding.EventId);
            CharacterTestCooldownAdmission admission = _lastPlayedFrames.Evaluate(
                cooldownKey,
                frame.Value,
                rule.CooldownFrames);
            if (admission == CharacterTestCooldownAdmission.Cooldown)
            {
                state.CooldownSkipCount++;
                return;
            }
            if (admission == CharacterTestCooldownAdmission.CapacityFull)
            {
                state.CooldownCapacitySkipCount++;
                return;
            }
            AudioParameterValue[] parameters = BuildParameters(evt);
            int emitterId = CreateEmitterId(evt);
            if (_audioIntentQueue == null
                || !_audioIntentQueue.Enqueue(CharacterTestAudioIntent.CreateEvent(
                    CharacterTestAudioIntentSource.WeaponHit,
                    ruleBinding.EventId,
                    CreateAudioTransform(evt.HitPointWorld, evt.HitNormalWorld),
                    emitterId,
                    frame,
                    WeaponHitTraceId,
                    parameters,
                    rule.Priority,
                    ruleBinding.Is3D)))
            {
                state.PlaybackFailedSkipCount++;
                return;
            }

            _lastPlayedFrames.Record(cooldownKey, frame.Value, rule.CooldownFrames);
            state.PlayedCount++;
            state.LastCueKey = rule.CueKey;
            state.LastEventId = ruleBinding.EventId;
            state.LastEmitterId = emitterId;
            if (evt.ResultKind == HitResolveKind.Blocked)
                state.BlockedPlayedCount++;
            else
                state.DamagePlayedCount++;
        }

        /// <remarks>NoAlloc after initialization while cooldown storage and the intent queue remain within capacity.</remarks>
        private void PlayResolved(
            in CharacterTestWeaponHitAudioEvent evt,
            RuntimeFrame frame,
            ref FrameState state)
        {
            if (!CharacterTestPhysicalAudioFactBuilder.TryCreate(
                    evt,
                    _propertyProvider,
                    out PhysicalAudioInteractionFact fact)
                || !_physicalAudioResolver.TryResolve(
                    fact,
                    _parameterValues.AsSpan(),
                    out PhysicalAudioResolvedPlayback resolved))
            {
                state.MissingRuleSkipCount++;
                return;
            }

            if (_audioCueCatalog == null
                || !_audioCueCatalog.TryGetPhysicalAudioBinding(
                    resolved.CueId,
                    out CharacterTestPhysicalAudioPlaybackBinding playbackBinding))
            {
                state.MissingCueSkipCount++;
                return;
            }

            var cooldownKey = new CooldownKey(
                evt.SourceEntityId,
                evt.TargetEntityId,
                evt.ResultKind,
                playbackBinding.EventId);
            CharacterTestCooldownAdmission admission = _lastPlayedFrames.Evaluate(
                cooldownKey,
                frame.Value,
                resolved.CooldownFrames);
            if (admission == CharacterTestCooldownAdmission.Cooldown)
            {
                state.CooldownSkipCount++;
                return;
            }
            if (admission == CharacterTestCooldownAdmission.CapacityFull)
            {
                state.CooldownCapacitySkipCount++;
                return;
            }
            if (!_audioIntentQueue.Enqueue(CharacterTestAudioIntent.CreateEvent(
                    CharacterTestAudioIntentSource.WeaponHit,
                    playbackBinding.EventId,
                    fact.Transform,
                    fact.EmitterId,
                    frame,
                    ResolvedWeaponHitTraceId,
                    _parameterValues,
                    resolved.Priority,
                    playbackBinding.Is3D,
                    resolved.ParameterCount)))
            {
                state.PlaybackFailedSkipCount++;
                return;
            }

            PhysicalAudioLatestFactCapture.Publish(
                fact,
                resolved,
                _parameterValues.AsSpan(0, resolved.ParameterCount));

            _lastPlayedFrames.Record(cooldownKey, frame.Value, resolved.CooldownFrames);
            state.PlayedCount++;
            state.LastCueKey = playbackBinding.CueName;
            state.LastEventId = playbackBinding.EventId;
            state.LastEmitterId = fact.EmitterId;
            if (evt.ResultKind == HitResolveKind.Blocked)
                state.BlockedPlayedCount++;
            else
                state.DamagePlayedCount++;
        }

        private bool TryResolveRule(
            in CharacterTestWeaponHitAudioEvent evt,
            out CharacterTestWeaponHitAudioRule rule,
            out CharacterTestWeaponHitAudioRuleBinding binding)
        {
            int bestScore = int.MinValue;
            int bestIndex = -1;
            for (int i = 0; i < _rules.Length; i++)
            {
                CharacterTestWeaponHitAudioRule candidate = _rules[i];
                if (!candidate.Matches(evt, out int score))
                    continue;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                rule = default;
                binding = default;
                return false;
            }

            rule = _rules[bestIndex];
            binding = _ruleBindings[bestIndex];
            return true;
        }

        private static CharacterTestWeaponHitAudioRuleBinding[] BuildRuleBindings(
            ICharacterTestAudioCueCatalog catalog,
            CharacterTestWeaponHitAudioRule[] rules)
        {
            var result = new CharacterTestWeaponHitAudioRuleBinding[rules.Length];
            if (catalog == null)
                return result;
            for (int i = 0; i < rules.Length; i++)
            {
                if (catalog.TryGetCue(rules[i].CueKey, out AudioCueDefinition cue)
                    && catalog.TryGetEvent(cue.EventId, out AudioEventDefinition audioEvent))
                {
                    result[i] = new CharacterTestWeaponHitAudioRuleBinding(
                        audioEvent.Id,
                        audioEvent.Is3D);
                }
            }
            return result;
        }

        private AudioParameterValue[] BuildParameters(in CharacterTestWeaponHitAudioEvent evt)
        {
            float impact = ClampToUnit(evt.ImpactPower, 80);
            float effectiveImpact = ClampToUnit(evt.EffectiveImpact, 80f);
            float damage = ClampToUnit(evt.FinalDamage, 40);
            float intensity = ClampToUnit(evt.Intensity, 8000);
            float materialHardness = ResolveMaterialHardness(evt.SurfaceMaterial.Kind);
            float blocked = evt.ResultKind == HitResolveKind.Blocked ? 1f : 0f;

            float volume = Clamp01(0.24f + impact * 0.28f + effectiveImpact * 0.1f + damage * 0.22f + blocked * 0.18f);
            float pitch = Clamp(
                0.78f + materialHardness * 0.2f + impact * 0.14f + effectiveImpact * 0.06f - blocked * 0.08f,
                0.55f,
                1.55f);
            float lowPass = Clamp01(
                0.38f + materialHardness * 0.36f + intensity * 0.12f + effectiveImpact * 0.06f - blocked * 0.08f);
            float brightness = Clamp01(
                0.28f + materialHardness * 0.44f + impact * 0.12f + effectiveImpact * 0.06f + blocked * 0.18f);
            float scrape = Clamp01(blocked * 0.22f + (1f - materialHardness) * 0.12f);

            _parameterValues[0] = new AudioParameterValue(
                CharacterTestAudioCueCatalog.PhysicalAudioVolumeParameterId,
                volume);
            _parameterValues[1] = new AudioParameterValue(
                CharacterTestAudioCueCatalog.PhysicalAudioPitchParameterId,
                pitch);
            _parameterValues[2] = new AudioParameterValue(
                CharacterTestAudioCueCatalog.PhysicalAudioLowPassParameterId,
                lowPass);
            _parameterValues[3] = new AudioParameterValue(
                CharacterTestAudioCueCatalog.PhysicalAudioBrightnessParameterId,
                brightness);
            _parameterValues[4] = new AudioParameterValue(
                CharacterTestAudioCueCatalog.PhysicalAudioScrapeParameterId,
                scrape);
            return _parameterValues;
        }

        private static AudioTransform CreateAudioTransform(FixVector3 point, FixVector3 normal)
        {
            return new AudioTransform(
                ToMeters(point.X),
                ToMeters(point.Y),
                ToMeters(point.Z),
                ToMeters(normal.X),
                ToMeters(normal.Y),
                ToMeters(normal.Z));
        }

        private static int CreateEmitterId(in CharacterTestWeaponHitAudioEvent evt)
        {
            unchecked
            {
                int hash = 53;
                hash = (hash * 397) ^ evt.SourceEntityId;
                hash = (hash * 397) ^ evt.ActionInstanceId;
                hash = (hash * 397) ^ evt.TraceId;
                hash &= int.MaxValue;
                return hash == 0 ? evt.SourceEntityId : hash;
            }
        }

        private static float ResolveMaterialHardness(CombatSurfaceMaterialKind materialKind)
        {
            switch (materialKind)
            {
                case CombatSurfaceMaterialKind.Rough:
                    return 0.55f;
                case CombatSurfaceMaterialKind.HardSlick:
                    return 0.92f;
                case CombatSurfaceMaterialKind.Elastic:
                    return 0.42f;
                case CombatSurfaceMaterialKind.BouncePad:
                    return 0.35f;
                default:
                    return 0.62f;
            }
        }

        private static float ClampToUnit(int value, int reference)
        {
            if (value <= 0 || reference <= 0)
                return 0f;
            return value >= reference ? 1f : value / (float)reference;
        }

        private static float ClampToUnit(float value, float reference)
        {
            if (value <= 0f || reference <= 0f)
                return 0f;
            return value >= reference ? 1f : value / reference;
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value <= min)
                return min;
            return value >= max ? max : value;
        }

        private static float ToMeters(Fix64 value)
        {
            return value.RawValue / (float)Fix64.Scale;
        }

        private struct FrameState
        {
            public FrameState(RuntimeFrame frame)
            {
                Frame = frame;
                ProcessedCount = 0;
                PlayedCount = 0;
                DamagePlayedCount = 0;
                BlockedPlayedCount = 0;
                IgnoredResultCount = 0;
                MissingRuleSkipCount = 0;
                MissingAudioServiceSkipCount = 0;
                MissingCueSkipCount = 0;
                CooldownSkipCount = 0;
                CooldownCapacitySkipCount = 0;
                PlaybackFailedSkipCount = 0;
                LastCueKey = string.Empty;
                LastEventId = 0;
                LastEmitterId = 0;
            }

            public RuntimeFrame Frame;
            public int ProcessedCount;
            public int PlayedCount;
            public int DamagePlayedCount;
            public int BlockedPlayedCount;
            public int IgnoredResultCount;
            public int MissingRuleSkipCount;
            public int MissingAudioServiceSkipCount;
            public int MissingCueSkipCount;
            public int CooldownSkipCount;
            public int CooldownCapacitySkipCount;
            public int PlaybackFailedSkipCount;
            public string LastCueKey;
            public int LastEventId;
            public int LastEmitterId;

            public CharacterTestWeaponHitAudioSummary ToSummary()
            {
                return new CharacterTestWeaponHitAudioSummary(
                    Frame,
                    ProcessedCount,
                    PlayedCount,
                    DamagePlayedCount,
                    BlockedPlayedCount,
                    IgnoredResultCount,
                    MissingRuleSkipCount,
                    MissingAudioServiceSkipCount,
                    MissingCueSkipCount,
                    CooldownSkipCount,
                    PlaybackFailedSkipCount,
                    LastCueKey,
                    LastEventId,
                    LastEmitterId,
                    CooldownCapacitySkipCount);
            }
        }

        private readonly struct CooldownKey : IEquatable<CooldownKey>
        {
            public CooldownKey(
                int sourceEntityId,
                int targetEntityId,
                HitResolveKind resultKind,
                int eventId)
            {
                SourceEntityId = sourceEntityId;
                TargetEntityId = targetEntityId;
                ResultKind = resultKind;
                EventId = eventId;
            }

            public int SourceEntityId { get; }
            public int TargetEntityId { get; }
            public HitResolveKind ResultKind { get; }
            public int EventId { get; }

            public bool Equals(CooldownKey other)
            {
                return SourceEntityId == other.SourceEntityId
                    && TargetEntityId == other.TargetEntityId
                    && ResultKind == other.ResultKind
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
                    int hash = SourceEntityId;
                    hash = (hash * 397) ^ TargetEntityId;
                    hash = (hash * 397) ^ (int)ResultKind;
                    hash = (hash * 397) ^ EventId;
                    return hash;
                }
            }
        }
    }

}
