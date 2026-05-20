using System;
using System.Collections.Generic;
using MxFramework.Resources;

namespace MxFramework.CharacterRuntimeSpawn
{
    public enum CharacterResourcePlanGroupKind
    {
        Custom = 0,
        SpawnCritical = 1,
        PresentationCritical = 2,
        EquipmentInitial = 3,
        AnimationWarmup = 4,
        VfxWarmup = 5,
        UiDeferred = 6
    }

    public enum CharacterResourceFailurePolicy
    {
        Continue = 0,
        FailSpawn = 1,
        UseFallbackVisual = 2,
        UseFallbackEquipment = 3,
        UseFallbackPose = 4,
        SkipEffect = 5,
        ShowPlaceholder = 6,
        MuteMissingCue = 7
    }

    public static class CharacterResourceDiagnostics
    {
        public const string MissingPlan = "CHAR_RESOURCE_PLAN_MISSING";
        public const string PreloadFailed = "CHAR_RESOURCE_PRELOAD_FAILED";
        public const string AcquireWithoutPreload = "CHAR_RESOURCE_ACQUIRE_WITHOUT_PRELOAD";
        public const string AudioWarmupDeferred = "CHAR_RESOURCE_AUDIO_WARMUP_DEFERRED";
        public const string SessionAlreadyReleased = "CHAR_RESOURCE_SESSION_ALREADY_RELEASED";
        public const string EquipmentPreloadFailed = "CHAR_RESOURCE_EQUIPMENT_PRELOAD_FAILED";
    }

    public sealed class CharacterResourcePlan
    {
        private readonly List<CharacterResourcePlanGroup> _groups;

        public CharacterResourcePlan(
            string characterStableId,
            string planHash,
            IEnumerable<CharacterResourcePlanGroup> groups,
            CharacterAudioResourcePlan audio)
        {
            CharacterStableId = characterStableId ?? string.Empty;
            PlanHash = planHash ?? string.Empty;
            _groups = groups != null
                ? new List<CharacterResourcePlanGroup>(groups)
                : new List<CharacterResourcePlanGroup>();
            Audio = audio ?? CharacterAudioResourcePlan.Empty;
        }

        public string CharacterStableId { get; }
        public string PlanHash { get; }
        public IReadOnlyList<CharacterResourcePlanGroup> Groups => _groups;
        public CharacterAudioResourcePlan Audio { get; }

        public CharacterResourcePlanGroup GetGroup(CharacterResourcePlanGroupKind kind)
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Kind == kind)
                    return _groups[i];
            }

            return CharacterResourcePlanGroup.Empty(kind);
        }

        public ResourceKey[] GetResourceKeys(params CharacterResourcePlanGroupKind[] kinds)
        {
            if (kinds == null || kinds.Length == 0)
                return GetResourceKeysForAllGroups();

            var keys = new List<ResourceKey>();
            var unique = new HashSet<ResourceKey>();
            for (int i = 0; i < kinds.Length; i++)
                AddGroupKeys(GetGroup(kinds[i]), keys, unique);

            return keys.ToArray();
        }

        private ResourceKey[] GetResourceKeysForAllGroups()
        {
            var keys = new List<ResourceKey>();
            var unique = new HashSet<ResourceKey>();
            for (int i = 0; i < _groups.Count; i++)
                AddGroupKeys(_groups[i], keys, unique);

            return keys.ToArray();
        }

        private static void AddGroupKeys(CharacterResourcePlanGroup group, List<ResourceKey> keys, HashSet<ResourceKey> unique)
        {
            if (group == null)
                return;

            for (int i = 0; i < group.Resources.Count; i++)
            {
                ResourceKey key = group.Resources[i];
                if (unique.Add(key))
                    keys.Add(key);
            }
        }
    }

    public sealed class CharacterResourcePlanGroup
    {
        private readonly List<ResourceKey> _resources;

        public CharacterResourcePlanGroup(
            CharacterResourcePlanGroupKind kind,
            string groupId,
            IEnumerable<ResourceKey> resources,
            bool required,
            CharacterResourceFailurePolicy failurePolicy)
        {
            Kind = kind;
            GroupId = groupId ?? kind.ToString();
            _resources = resources != null
                ? new List<ResourceKey>(resources)
                : new List<ResourceKey>();
            Required = required;
            FailurePolicy = failurePolicy;
        }

        public CharacterResourcePlanGroupKind Kind { get; }
        public string GroupId { get; }
        public IReadOnlyList<ResourceKey> Resources => _resources;
        public bool Required { get; }
        public CharacterResourceFailurePolicy FailurePolicy { get; }

        public static CharacterResourcePlanGroup Empty(CharacterResourcePlanGroupKind kind)
        {
            return new CharacterResourcePlanGroup(kind, kind.ToString(), Array.Empty<ResourceKey>(), false, CharacterResourceFailurePolicy.Continue);
        }
    }

    public sealed class CharacterAudioResourcePlan
    {
        private readonly List<string> _requiredBanks;
        private readonly List<int> _requiredCueIds;
        private readonly List<string> _requiredCueKeys;
        private readonly List<string> _requiredEventDefinitionIds;

        public CharacterAudioResourcePlan(
            IEnumerable<string> requiredBanks,
            IEnumerable<int> requiredCueIds,
            IEnumerable<string> requiredEventDefinitionIds,
            CharacterResourceFailurePolicy failurePolicy = CharacterResourceFailurePolicy.MuteMissingCue,
            IEnumerable<string> requiredCueKeys = null)
        {
            _requiredBanks = requiredBanks != null
                ? new List<string>(requiredBanks)
                : new List<string>();
            _requiredCueIds = requiredCueIds != null
                ? new List<int>(requiredCueIds)
                : new List<int>();
            _requiredCueKeys = requiredCueKeys != null
                ? new List<string>(requiredCueKeys)
                : new List<string>();
            _requiredEventDefinitionIds = requiredEventDefinitionIds != null
                ? new List<string>(requiredEventDefinitionIds)
                : new List<string>();
            FailurePolicy = failurePolicy;
        }

        public static CharacterAudioResourcePlan Empty { get; } = new CharacterAudioResourcePlan(
            Array.Empty<string>(),
            Array.Empty<int>(),
            Array.Empty<string>());

        public IReadOnlyList<string> RequiredBanks => _requiredBanks;
        public IReadOnlyList<int> RequiredCueIds => _requiredCueIds;
        public IReadOnlyList<string> RequiredCueKeys => _requiredCueKeys;
        public IReadOnlyList<string> RequiredEventDefinitionIds => _requiredEventDefinitionIds;
        public CharacterResourceFailurePolicy FailurePolicy { get; }
        public bool HasAudioRequirements => _requiredBanks.Count > 0 || _requiredCueIds.Count > 0 || _requiredCueKeys.Count > 0 || _requiredEventDefinitionIds.Count > 0;
    }

    public sealed class CharacterAudioCueManifest
    {
        private readonly List<string> _banks;
        private readonly List<CharacterAudioCueManifestEntry> _cues;

        public CharacterAudioCueManifest(
            string packageId,
            string characterStableId,
            IEnumerable<string> banks,
            IEnumerable<CharacterAudioCueManifestEntry> cues)
        {
            PackageId = packageId ?? string.Empty;
            CharacterStableId = characterStableId ?? string.Empty;
            _banks = banks != null ? new List<string>(banks) : new List<string>();
            _cues = cues != null ? new List<CharacterAudioCueManifestEntry>(cues) : new List<CharacterAudioCueManifestEntry>();
        }

        public static CharacterAudioCueManifest Empty { get; } = new CharacterAudioCueManifest(
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<CharacterAudioCueManifestEntry>());

        public string PackageId { get; }
        public string CharacterStableId { get; }
        public IReadOnlyList<string> Banks => _banks;
        public IReadOnlyList<CharacterAudioCueManifestEntry> Cues => _cues;
    }

    public sealed class CharacterAudioCueManifestEntry
    {
        public CharacterAudioCueManifestEntry(
            string cueId,
            string stableId,
            string resourceKey,
            string eventPath,
            string bank,
            CharacterResourceFailurePolicy fallbackPolicy,
            IReadOnlyDictionary<string, string> providerData)
        {
            CueId = cueId ?? string.Empty;
            StableId = stableId ?? string.Empty;
            ResourceKey = resourceKey ?? string.Empty;
            EventPath = eventPath ?? string.Empty;
            Bank = bank ?? string.Empty;
            FallbackPolicy = fallbackPolicy;
            ProviderData = providerData != null
                ? new Dictionary<string, string>(providerData, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public string CueId { get; }
        public string StableId { get; }
        public string ResourceKey { get; }
        public string EventPath { get; }
        public string Bank { get; }
        public CharacterResourceFailurePolicy FallbackPolicy { get; }
        public IReadOnlyDictionary<string, string> ProviderData { get; }
    }
}
