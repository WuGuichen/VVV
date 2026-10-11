using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Audio.Physical;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestAudioCatalogMode
    {
        Invalid = 0,
        GeneratedLinked = 1,
        LegacyFallback = 2
    }

    /// <summary>AllocByDesign startup diagnostics for the CharacterTest audio catalog pair.</summary>
    internal readonly struct CharacterTestAudioCatalogStatus
    {
        public CharacterTestAudioCatalogStatus(
            CharacterTestAudioCatalogMode mode,
            string audioManifestHash,
            string physicalManifestHash,
            string linkedAudioCatalogHash,
            string message)
        {
            Mode = mode;
            AudioManifestHash = audioManifestHash ?? string.Empty;
            PhysicalManifestHash = physicalManifestHash ?? string.Empty;
            LinkedAudioCatalogHash = linkedAudioCatalogHash ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CharacterTestAudioCatalogMode Mode { get; }
        public string AudioManifestHash { get; }
        public string PhysicalManifestHash { get; }
        public string LinkedAudioCatalogHash { get; }
        public string Message { get; }

        public static CharacterTestAudioCatalogStatus From(ICharacterTestAudioCueCatalog catalog)
        {
            if (catalog == null)
            {
                return new CharacterTestAudioCatalogStatus(
                    CharacterTestAudioCatalogMode.Invalid,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "Audio playback is disabled or no catalog was composed.");
            }

            if (catalog.PhysicalAudioDefinitions == null)
            {
                return new CharacterTestAudioCatalogStatus(
                    CharacterTestAudioCatalogMode.LegacyFallback,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "The complete legacy CharacterTest audio fallback is active.");
            }

            string audioManifestHash = catalog is CharacterTestAudioCueCatalog concrete
                ? concrete.RuntimeManifestHash
                : string.Empty;
            return new CharacterTestAudioCatalogStatus(
                CharacterTestAudioCatalogMode.GeneratedLinked,
                audioManifestHash,
                catalog.PhysicalAudioDefinitions.PhysicalManifestHash,
                catalog.PhysicalAudioDefinitions.AudioCatalogHash,
                "Generated Global and Physical audio catalogs are linked.");
        }
    }

    /// <summary>Startup-linked numeric playback binding for one generated physical-audio Cue.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct CharacterTestPhysicalAudioPlaybackBinding
    {
        public CharacterTestPhysicalAudioPlaybackBinding(int cueId, int eventId, bool is3D, string cueName)
        {
            CueId = cueId;
            EventId = eventId;
            Is3D = is3D;
            CueName = cueName ?? string.Empty;
        }

        public int CueId { get; }
        public int EventId { get; }
        public bool Is3D { get; }
        public string CueName { get; }
    }

    public interface ICharacterTestAudioCueCatalog : IAudioDefinitionProvider, IAudioCueDefinitionProvider, IAudioCueKeyDefinitionProvider
    {
        /// <summary>Generated physical-audio definitions, or null when the complete legacy fallback is active.</summary>
        /// <remarks>NoAlloc.</remarks>
        IPhysicalAudioDefinitionProvider PhysicalAudioDefinitions { get; }

        /// <remarks>NoAlloc. Parameter-name lookup tables are initialized with the catalog.</remarks>
        bool TryGetParameter(int eventId, string parameterName, out AudioParameterDefinition definition);

        /// <summary>Resolves a generated physical Cue through the startup-linked numeric binding table.</summary>
        /// <remarks>NoAlloc. Uses a sorted immutable array and performs no Cue/Event dictionary lookup.</remarks>
        bool TryGetPhysicalAudioBinding(int cueId, out CharacterTestPhysicalAudioPlaybackBinding binding);
    }

    public sealed partial class CharacterTestAudioCueCatalog : ICharacterTestAudioCueCatalog
    {
        public const string LightAttackCueKey = "sfx.light_attack";
        public const string LightAttackAirDisplacementCueKey = "sfx.air_displacement.light_attack";
        public const string FireballExplosionCueKey = "sfx.fireball_explosion";
        public const int DefaultBusId = 500001;
        public const int LightAttackCueId = 500101;
        public const int LightAttackEventId = 500101;
        public const string LightAttackEventPath = "event:/MxFramework/Demo/OneShot3D";
        public const string LightAttackEventGuid = "{5b50605b-36ec-4999-96c8-ec6cc5fcaa28}";
        public const int LightAttackAirDisplacementCueId = 500304;
        public const int LightAttackAirDisplacementEventId = 500314;
        public const string LightAttackAirDisplacementEventPath =
            "event:/MxFramework/Generated/Physical/audio.family.air_displacement/audio.basis.air_displacement.light_attack";
        public const string LightAttackAirDisplacementEventGuid = "{b6d3e343-5b60-495d-b3da-508847136970}";
        public const int FireballExplosionCueId = 500102;
        public const int FireballExplosionEventId = 500102;
        public const string FireballExplosionEventPath = LightAttackEventPath;
        public const string FireballExplosionEventGuid = LightAttackEventGuid;
        public const string SoleContactDefaultCueKey = "sfx.body_contact.sole.default";
        public const string SoleContactRoughCueKey = "sfx.body_contact.sole.rough";
        public const string SoleContactHardSlickCueKey = "sfx.body_contact.sole.hard_slick";
        public const string SoleContactElasticCueKey = "sfx.body_contact.sole.elastic";
        public const int SoleContactDefaultCueId = 500201;
        public const int SoleContactDefaultEventId = 500201;
        public const int SoleContactRoughCueId = 500202;
        public const int SoleContactRoughEventId = 500202;
        public const int SoleContactHardSlickCueId = 500203;
        public const int SoleContactHardSlickEventId = 500203;
        public const int SoleContactElasticCueId = 500204;
        public const int SoleContactElasticEventId = 500204;
        public const string SoleContactDefaultEventPath =
            "event:/MxFramework/CharacterTest/PhysicalAudio/SoleContactDefault";
        public const string SoleContactRoughEventPath =
            "event:/MxFramework/CharacterTest/PhysicalAudio/SoleContactRough";
        public const string SoleContactHardSlickEventPath =
            "event:/MxFramework/CharacterTest/PhysicalAudio/SoleContactHardSlick";
        public const string SoleContactElasticEventPath =
            "event:/MxFramework/CharacterTest/PhysicalAudio/SoleContactElastic";
        public const string PhysicalAudioEventGuid = "";
        public const int PhysicalAudioVolumeParameterId = 500251;
        public const int PhysicalAudioPitchParameterId = 500252;
        public const int PhysicalAudioLowPassParameterId = 500253;
        public const int PhysicalAudioBrightnessParameterId = 500254;
        public const int PhysicalAudioScrapeParameterId = 500255;
        public const string PhysicalAudioVolumeParameterName = "MxPhysicalVolume";
        public const string PhysicalAudioPitchParameterName = "MxPhysicalPitch";
        public const string PhysicalAudioLowPassParameterName = "MxPhysicalLowPass";
        public const string PhysicalAudioBrightnessParameterName = "MxPhysicalBrightness";
        public const string PhysicalAudioScrapeParameterName = "MxPhysicalScrape";
        public const string LandingDefaultCueKey = "sfx.landing.default";
        public const string LandingRoughCueKey = "sfx.landing.rough";
        public const string LandingHardSlickCueKey = "sfx.landing.hard_slick";
        public const string LandingElasticCueKey = "sfx.landing.elastic";
        public const int LandingDefaultCueId = 500205;
        public const int LandingDefaultEventId = 500205;
        public const int LandingRoughCueId = 500206;
        public const int LandingRoughEventId = 500206;
        public const int LandingHardSlickCueId = 500207;
        public const int LandingHardSlickEventId = 500207;
        public const int LandingElasticCueId = 500208;
        public const int LandingElasticEventId = 500208;
        public const string LandingDefaultEventPath =
            "event:/MxFramework/CharacterTest/PhysicalAudio/ObjectCollisionDefault";
        public const string LandingRoughEventPath =
            "event:/MxFramework/CharacterTest/PhysicalAudio/ObjectCollisionRough";
        public const string LandingHardSlickEventPath =
            "event:/MxFramework/CharacterTest/PhysicalAudio/ObjectCollisionHardSlick";
        public const string LandingElasticEventPath =
            "event:/MxFramework/CharacterTest/PhysicalAudio/ObjectCollisionElastic";
        public const string ObjectCollisionDefaultCueKey = "sfx.object_collision.default";
        public const string ObjectCollisionRoughCueKey = "sfx.object_collision.rough";
        public const string ObjectCollisionHardSlickCueKey = "sfx.object_collision.hard_slick";
        public const string ObjectCollisionElasticCueKey = "sfx.object_collision.elastic";
        public const int ObjectCollisionDefaultCueId = 500211;
        public const int ObjectCollisionDefaultEventId = 500211;
        public const int ObjectCollisionRoughCueId = 500212;
        public const int ObjectCollisionRoughEventId = 500212;
        public const int ObjectCollisionHardSlickCueId = 500213;
        public const int ObjectCollisionHardSlickEventId = 500213;
        public const int ObjectCollisionElasticCueId = 500214;
        public const int ObjectCollisionElasticEventId = 500214;
        public const string ObjectCollisionDefaultEventPath =
            "event:/MxFramework/CharacterTest/PhysicalAudio/ObjectCollisionDefault";
        public const string ObjectCollisionRoughEventPath =
            "event:/MxFramework/CharacterTest/PhysicalAudio/ObjectCollisionRough";
        public const string ObjectCollisionHardSlickEventPath =
            "event:/MxFramework/CharacterTest/PhysicalAudio/ObjectCollisionHardSlick";
        public const string ObjectCollisionElasticEventPath =
            "event:/MxFramework/CharacterTest/PhysicalAudio/ObjectCollisionElastic";
        public const string WeaponHitDefaultCueKey = "sfx.weapon_hit.default";
        public const string WeaponHitBlockedCueKey = "sfx.weapon_hit.blocked";
        public const string WeaponHitRoughCueKey = "sfx.weapon_hit.rough";
        public const string WeaponHitHardSlickCueKey = "sfx.weapon_hit.hard_slick";
        public const string WeaponHitElasticCueKey = "sfx.weapon_hit.elastic";
        public const int WeaponHitDefaultCueId = 500221;
        public const int WeaponHitDefaultEventId = 500221;
        public const int WeaponHitBlockedCueId = 500222;
        public const int WeaponHitBlockedEventId = 500222;
        public const int WeaponHitRoughCueId = 500223;
        public const int WeaponHitRoughEventId = 500223;
        public const int WeaponHitHardSlickCueId = 500224;
        public const int WeaponHitHardSlickEventId = 500224;
        public const int WeaponHitElasticCueId = 500225;
        public const int WeaponHitElasticEventId = 500225;
        public const string WeaponHitDefaultEventPath = ObjectCollisionDefaultEventPath;
        public const string WeaponHitBlockedEventPath = ObjectCollisionHardSlickEventPath;
        public const string WeaponHitRoughEventPath = ObjectCollisionRoughEventPath;
        public const string WeaponHitHardSlickEventPath = ObjectCollisionHardSlickEventPath;
        public const string WeaponHitElasticEventPath = ObjectCollisionElasticEventPath;

        private readonly Dictionary<string, AudioCueDefinition> _cuesByKey =
            new Dictionary<string, AudioCueDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<int, AudioCueDefinition> _cuesById =
            new Dictionary<int, AudioCueDefinition>();
        private readonly Dictionary<int, AudioEventDefinition> _events =
            new Dictionary<int, AudioEventDefinition>();
        private readonly Dictionary<int, AudioBusDefinition> _buses =
            new Dictionary<int, AudioBusDefinition>();
        private readonly AudioDefinitionCatalog _runtimeCatalog;
        private readonly IPhysicalAudioDefinitionProvider _physicalAudioDefinitions;
        private readonly CharacterTestPhysicalAudioPlaybackBinding[] _physicalAudioBindings =
            Array.Empty<CharacterTestPhysicalAudioPlaybackBinding>();
        private static readonly AudioParameterDefinition[] PhysicalAudioParameters =
        {
            new AudioParameterDefinition(
                PhysicalAudioVolumeParameterId,
                PhysicalAudioVolumeParameterName,
                AudioParameterKind.Continuous,
                defaultValue: 0.5f,
                minValue: 0f,
                maxValue: 1f),
            new AudioParameterDefinition(
                PhysicalAudioPitchParameterId,
                PhysicalAudioPitchParameterName,
                AudioParameterKind.Continuous,
                defaultValue: 1f,
                minValue: 0.5f,
                maxValue: 1.6f),
            new AudioParameterDefinition(
                PhysicalAudioLowPassParameterId,
                PhysicalAudioLowPassParameterName,
                AudioParameterKind.Continuous,
                defaultValue: 0.75f,
                minValue: 0f,
                maxValue: 1f),
            new AudioParameterDefinition(
                PhysicalAudioBrightnessParameterId,
                PhysicalAudioBrightnessParameterName,
                AudioParameterKind.Continuous,
                defaultValue: 0.5f,
                minValue: 0f,
                maxValue: 1f),
            new AudioParameterDefinition(
                PhysicalAudioScrapeParameterId,
                PhysicalAudioScrapeParameterName,
                AudioParameterKind.Continuous,
                defaultValue: 0f,
                minValue: 0f,
                maxValue: 1f)
        };

        private CharacterTestAudioCueCatalog(
            AudioDefinitionCatalog runtimeCatalog,
            IPhysicalAudioDefinitionProvider physicalAudioDefinitions,
            CharacterTestPhysicalAudioPlaybackBinding[] physicalAudioBindings)
        {
            _runtimeCatalog = runtimeCatalog ?? throw new ArgumentNullException(nameof(runtimeCatalog));
            _physicalAudioDefinitions = physicalAudioDefinitions
                ?? throw new ArgumentNullException(nameof(physicalAudioDefinitions));
            _physicalAudioBindings = physicalAudioBindings
                ?? throw new ArgumentNullException(nameof(physicalAudioBindings));
        }

        public CharacterTestAudioCueCatalog()
        {
        }

        /// <summary>Generated physical definitions; null means this catalog is the complete legacy fallback.</summary>
        /// <remarks>NoAlloc.</remarks>
        public IPhysicalAudioDefinitionProvider PhysicalAudioDefinitions => _physicalAudioDefinitions;

        internal string RuntimeManifestHash => _runtimeCatalog?.ManifestHash ?? string.Empty;

        /// <inheritdoc />
        public bool TryGetPhysicalAudioBinding(int cueId, out CharacterTestPhysicalAudioPlaybackBinding binding)
        {
            int low = 0;
            int high = _physicalAudioBindings.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                CharacterTestPhysicalAudioPlaybackBinding candidate = _physicalAudioBindings[middle];
                if (candidate.CueId == cueId)
                {
                    binding = candidate;
                    return true;
                }
                if (candidate.CueId < cueId)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            binding = default;
            return false;
        }
        public void AddCue(string cueKey, AudioCueDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(cueKey))
                throw new ArgumentException("Audio cue key cannot be empty.", nameof(cueKey));
            if (definition.Id <= 0)
                throw new ArgumentOutOfRangeException(nameof(definition), "Audio cue id must be positive.");

            _cuesByKey[cueKey] = definition;
            _cuesById[definition.Id] = definition;
        }

        public void AddEvent(AudioEventDefinition definition)
        {
            if (definition.Id <= 0)
                throw new ArgumentOutOfRangeException(nameof(definition), "Audio event id must be positive.");

            _events[definition.Id] = definition;
        }

        public void AddBus(AudioBusDefinition definition)
        {
            if (definition.Id <= 0)
                throw new ArgumentOutOfRangeException(nameof(definition), "Audio bus id must be positive.");

            _buses[definition.Id] = definition;
        }

        public bool TryGetCue(string cueKey, out AudioCueDefinition definition)
        {
            if (_runtimeCatalog != null)
                return _runtimeCatalog.TryGetCue(cueKey, out definition);
            if (string.IsNullOrEmpty(cueKey))
            {
                definition = default;
                return false;
            }

            return _cuesByKey.TryGetValue(cueKey, out definition);
        }

        public bool TryGetCue(int cueId, out AudioCueDefinition definition)
        {
            if (_runtimeCatalog != null)
                return _runtimeCatalog.TryGetCue(cueId, out definition);
            return _cuesById.TryGetValue(cueId, out definition);
        }

        public bool TryGetEvent(int eventId, out AudioEventDefinition definition)
        {
            if (_runtimeCatalog != null)
                return _runtimeCatalog.TryGetEvent(eventId, out definition);
            return _events.TryGetValue(eventId, out definition);
        }

        public bool TryGetBus(int busId, out AudioBusDefinition definition)
        {
            if (_runtimeCatalog != null)
                return _runtimeCatalog.TryGetBus(busId, out definition);
            return _buses.TryGetValue(busId, out definition);
        }

        public bool TryGetParameter(int eventId, int parameterId, out AudioParameterDefinition definition)
        {
            if (_runtimeCatalog != null)
                return _runtimeCatalog.TryGetParameter(eventId, parameterId, out definition);
            if (_events.TryGetValue(eventId, out AudioEventDefinition audioEvent))
            {
                IReadOnlyList<AudioParameterDefinition> parameters = audioEvent.Parameters ?? AudioEventDefinition.EmptyParameters;
                for (int i = 0; i < parameters.Count; i++)
                {
                    if (parameters[i].Id == parameterId)
                    {
                        definition = parameters[i];
                        return true;
                    }
                }
            }

            definition = default;
            return false;
        }

        /// <remarks>NoAlloc. Parameter name maps are built when the generated catalog is loaded.</remarks>
        public bool TryGetParameter(int eventId, string parameterName, out AudioParameterDefinition definition)
        {
            if (_runtimeCatalog != null)
                return _runtimeCatalog.TryGetParameter(eventId, parameterName, out definition);

            if (_events.TryGetValue(eventId, out AudioEventDefinition audioEvent))
            {
                IReadOnlyList<AudioParameterDefinition> parameters = audioEvent.Parameters ?? AudioEventDefinition.EmptyParameters;
                for (int i = 0; i < parameters.Count; i++)
                {
                    if (string.Equals(parameters[i].Name, parameterName, StringComparison.Ordinal))
                    {
                        definition = parameters[i];
                        return true;
                    }
                }
            }
            definition = default;
            return false;
        }
    }
}
