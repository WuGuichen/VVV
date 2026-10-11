using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.CombatBridge;
using MxFramework.Gameplay.Triggers.Config;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Scene-owned derived input for CharacterTest level-authored Gameplay Triggers.
    /// The Level Editor is the only writer; runtime treats the cache as immutable startup data.
    /// </summary>
    [Serializable]
    public sealed class CharacterTestGameplayTriggerSceneCache
    {
        internal const int LegacyRegionOnlyCacheVersion = 2;
        internal const int CurrentCacheVersion = 3;

        [SerializeField] private bool _enabled = true;
        [SerializeField] private string _levelFormat = string.Empty;
        [SerializeField] private int _levelSchemaVersion;
        [SerializeField] private int _moduleSchemaVersion;
        [SerializeField] private int _cacheVersion = CurrentCacheVersion;
        [SerializeField] private int _triggerEntryCount;
        [SerializeField] private string _authoringContentHash = string.Empty;
        [SerializeField] private string _triggerSourceHash = string.Empty;
        [SerializeField, TextArea(2, 8)]
        private string _canonicalTriggerEnvelopeJson = string.Empty;
        [SerializeField] private CharacterTestGameplayTriggerSensorCacheRow[] _sensorRows =
            Array.Empty<CharacterTestGameplayTriggerSensorCacheRow>();
        [SerializeField]
        private CharacterTestGameplayTriggerAttributeThresholdCacheRow[]
            _attributeThresholdRows =
                Array.Empty<
                    CharacterTestGameplayTriggerAttributeThresholdCacheRow>();
        [SerializeField] private long _triggerCatalogHash;
        [SerializeField] private long _sensorCatalogHash;
        [SerializeField] private long _attributeThresholdCatalogHash;
        [SerializeField] private long _combinedCatalogHash;
        [SerializeField] private string _lastAppliedUtc = string.Empty;

        /// <remarks>NoAlloc.</remarks>
        public bool Enabled => _enabled;

        /// <remarks>NoAlloc.</remarks>
        public string LevelFormat => _levelFormat ?? string.Empty;

        /// <remarks>NoAlloc.</remarks>
        public int LevelSchemaVersion => _levelSchemaVersion;

        /// <remarks>NoAlloc.</remarks>
        public int ModuleSchemaVersion => _moduleSchemaVersion;

        /// <remarks>NoAlloc.</remarks>
        public int CacheVersion => _cacheVersion;

        /// <remarks>NoAlloc.</remarks>
        public int TriggerEntryCount => _triggerEntryCount;

        /// <remarks>NoAlloc.</remarks>
        public string AuthoringContentHash => _authoringContentHash ?? string.Empty;

        /// <remarks>NoAlloc.</remarks>
        public string TriggerSourceHash => _triggerSourceHash ?? string.Empty;

        /// <remarks>NoAlloc.</remarks>
        public string CanonicalTriggerEnvelopeJson =>
            _canonicalTriggerEnvelopeJson ?? string.Empty;

        /// <remarks>NoAlloc.</remarks>
        public IReadOnlyList<CharacterTestGameplayTriggerSensorCacheRow> SensorRows =>
            _sensorRows ?? Array.Empty<CharacterTestGameplayTriggerSensorCacheRow>();

        /// <remarks>NoAlloc.</remarks>
        public int SensorRowCount => _sensorRows == null ? 0 : _sensorRows.Length;

        /// <remarks>NoAlloc.</remarks>
        public IReadOnlyList<
                CharacterTestGameplayTriggerAttributeThresholdCacheRow>
            AttributeThresholdRows =>
                _attributeThresholdRows
                ?? Array.Empty<
                    CharacterTestGameplayTriggerAttributeThresholdCacheRow>();

        /// <remarks>NoAlloc.</remarks>
        public int AttributeThresholdRowCount =>
            _attributeThresholdRows == null
                ? 0
                : _attributeThresholdRows.Length;

        /// <remarks>NoAlloc.</remarks>
        public long TriggerCatalogHash => _triggerCatalogHash;

        /// <remarks>NoAlloc.</remarks>
        public long SensorCatalogHash => _sensorCatalogHash;

        /// <remarks>NoAlloc.</remarks>
        public long AttributeThresholdCatalogHash =>
            _attributeThresholdCatalogHash;

        /// <remarks>NoAlloc.</remarks>
        public long CombinedCatalogHash => _combinedCatalogHash;

        /// <remarks>NoAlloc.</remarks>
        public string LastAppliedUtc => _lastAppliedUtc ?? string.Empty;

        /// <remarks>NoAlloc.</remarks>
        public bool HasRuntimeContent =>
            _enabled
            && _triggerEntryCount > 0
            && (SensorRowCount > 0 || AttributeThresholdRowCount > 0);

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetSensorRow(
            int sourceBindingId,
            out CharacterTestGameplayTriggerSensorCacheRow row)
        {
            if (_sensorRows != null)
            {
                for (int i = 0; i < _sensorRows.Length; i++)
                {
                    CharacterTestGameplayTriggerSensorCacheRow candidate =
                        _sensorRows[i];
                    if (candidate != null
                        && candidate.SourceBindingId == sourceBindingId)
                    {
                        row = candidate;
                        return true;
                    }
                }
            }

            row = null;
            return false;
        }

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetAttributeThresholdRow(
            int sourceBindingId,
            out CharacterTestGameplayTriggerAttributeThresholdCacheRow row)
        {
            if (_attributeThresholdRows != null)
            {
                for (int i = 0; i < _attributeThresholdRows.Length; i++)
                {
                    CharacterTestGameplayTriggerAttributeThresholdCacheRow
                        candidate = _attributeThresholdRows[i];
                    if (candidate != null
                        && candidate.SourceBindingId == sourceBindingId)
                    {
                        row = candidate;
                        return true;
                    }
                }
            }
            row = null;
            return false;
        }

        internal void Configure(
            CharacterTestGameplayTriggerSceneCacheData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            _enabled = data.Enabled;
            _levelFormat = data.LevelFormat ?? string.Empty;
            _levelSchemaVersion = data.LevelSchemaVersion;
            _moduleSchemaVersion = data.ModuleSchemaVersion;
            _cacheVersion = CurrentCacheVersion;
            _triggerEntryCount = data.TriggerEntryCount;
            _authoringContentHash = data.AuthoringContentHash ?? string.Empty;
            _triggerSourceHash = data.TriggerSourceHash ?? string.Empty;
            _canonicalTriggerEnvelopeJson =
                data.CanonicalTriggerEnvelopeJson ?? string.Empty;
            _sensorRows = data.SensorRows == null
                ? Array.Empty<CharacterTestGameplayTriggerSensorCacheRow>()
                : (CharacterTestGameplayTriggerSensorCacheRow[])
                    data.SensorRows.Clone();
            _attributeThresholdRows =
                data.AttributeThresholdRows == null
                    ? Array.Empty<
                        CharacterTestGameplayTriggerAttributeThresholdCacheRow>()
                    : (CharacterTestGameplayTriggerAttributeThresholdCacheRow[])
                        data.AttributeThresholdRows.Clone();
            _triggerCatalogHash = data.TriggerCatalogHash;
            _sensorCatalogHash = data.SensorCatalogHash;
            _attributeThresholdCatalogHash =
                data.AttributeThresholdCatalogHash;
            _combinedCatalogHash = data.CombinedCatalogHash;
            _lastAppliedUtc = data.LastAppliedUtc ?? string.Empty;
        }
    }

    /// <summary>
    /// Serializable trusted binding for the CharacterTest current-player HP
    /// Threshold source. Threshold values remain in the Trigger catalog.
    /// </summary>
    [Serializable]
    public sealed class
        CharacterTestGameplayTriggerAttributeThresholdCacheRow
    {
        [SerializeField] private int _sourceBindingId;
        [SerializeField] private int _providerId;
        [SerializeField] private int _attributeId;

        internal CharacterTestGameplayTriggerAttributeThresholdCacheRow(
            int sourceBindingId,
            int providerId,
            int attributeId)
        {
            _sourceBindingId = sourceBindingId;
            _providerId = providerId;
            _attributeId = attributeId;
        }

        /// <remarks>NoAlloc.</remarks>
        public int SourceBindingId => _sourceBindingId;

        /// <remarks>NoAlloc.</remarks>
        public int ProviderId => _providerId;

        /// <remarks>NoAlloc.</remarks>
        public int AttributeId => _attributeId;

        /// <remarks>NoAlloc.</remarks>
        internal CharacterTestGameplayTriggerAttributeThresholdDescriptor
            ToDescriptor()
        {
            return new
                CharacterTestGameplayTriggerAttributeThresholdDescriptor(
                    new GameplayTriggerSourceBindingId(_sourceBindingId),
                    new GameplayTriggerSourceProviderId(_providerId),
                    _attributeId);
        }
    }

    /// <summary>
    /// Serializable fixed Combat AABB query row generated from one TriggerRegion.
    /// </summary>
    [Serializable]
    public sealed class CharacterTestGameplayTriggerSensorCacheRow
    {
        [SerializeField] private int _sourceBindingId;
        [SerializeField] private string _regionStableId = string.Empty;
        [SerializeField] private int _providerId;
        [SerializeField] private int _queryId;
        [SerializeField] private int _layerMask;
        [SerializeField] private int _maxQueryHits;
        [SerializeField] private int _maxMappedSubjects;
        [SerializeField] private GameplayTargetIdentityTypes
            _acceptedIdentityTypes;
        [SerializeField] private GameplayTargetCapabilities
            _requiredCapabilities;
        [SerializeField] private bool _requireAlive;
        [SerializeField] private GameplayTargetRelationFilter _relationFilter;
        [SerializeField] private int _referenceTeamId;
        [SerializeField] private long _minXRaw;
        [SerializeField] private long _minYRaw;
        [SerializeField] private long _minZRaw;
        [SerializeField] private long _maxXRaw;
        [SerializeField] private long _maxYRaw;
        [SerializeField] private long _maxZRaw;

        internal CharacterTestGameplayTriggerSensorCacheRow(
            int sourceBindingId,
            string regionStableId,
            int providerId,
            int queryId,
            int layerMask,
            int maxQueryHits,
            int maxMappedSubjects,
            in FixVector3 min,
            in FixVector3 max)
            : this(
                sourceBindingId,
                regionStableId,
                providerId,
                queryId,
                layerMask,
                maxQueryHits,
                maxMappedSubjects,
                GameplayComponentTargetFilter.Unrestricted,
                in min,
                in max)
        {
        }

        internal CharacterTestGameplayTriggerSensorCacheRow(
            int sourceBindingId,
            string regionStableId,
            int providerId,
            int queryId,
            int layerMask,
            int maxQueryHits,
            int maxMappedSubjects,
            GameplayComponentTargetFilter subjectFilter,
            in FixVector3 min,
            in FixVector3 max)
        {
            _sourceBindingId = sourceBindingId;
            _regionStableId = regionStableId ?? string.Empty;
            _providerId = providerId;
            _queryId = queryId;
            _layerMask = layerMask;
            _maxQueryHits = maxQueryHits;
            _maxMappedSubjects = maxMappedSubjects;
            _acceptedIdentityTypes =
                subjectFilter.AcceptedIdentityTypes;
            _requiredCapabilities =
                subjectFilter.RequiredCapabilities;
            _requireAlive = subjectFilter.RequireAlive;
            _relationFilter = subjectFilter.RelationFilter;
            _referenceTeamId = subjectFilter.ReferenceTeamId;
            _minXRaw = min.X.RawValue;
            _minYRaw = min.Y.RawValue;
            _minZRaw = min.Z.RawValue;
            _maxXRaw = max.X.RawValue;
            _maxYRaw = max.Y.RawValue;
            _maxZRaw = max.Z.RawValue;
        }

        /// <remarks>NoAlloc.</remarks>
        public int SourceBindingId => _sourceBindingId;

        /// <remarks>NoAlloc.</remarks>
        public string RegionStableId => _regionStableId ?? string.Empty;

        /// <remarks>NoAlloc.</remarks>
        public int ProviderId => _providerId;

        /// <remarks>NoAlloc.</remarks>
        public int QueryId => _queryId;

        /// <remarks>NoAlloc.</remarks>
        public int LayerMask => _layerMask;

        /// <remarks>NoAlloc.</remarks>
        public int MaxQueryHits => _maxQueryHits;

        /// <remarks>NoAlloc.</remarks>
        public int MaxMappedSubjects => _maxMappedSubjects;

        /// <remarks>NoAlloc.</remarks>
        public GameplayComponentTargetFilter SubjectFilter =>
            new GameplayComponentTargetFilter(
                _acceptedIdentityTypes,
                _requiredCapabilities,
                _requireAlive,
                _relationFilter,
                referenceTeamId: _referenceTeamId);

        /// <remarks>NoAlloc.</remarks>
        public FixVector3 Min => new FixVector3(
            Fix64.FromRaw(_minXRaw),
            Fix64.FromRaw(_minYRaw),
            Fix64.FromRaw(_minZRaw));

        /// <remarks>NoAlloc.</remarks>
        public FixVector3 Max => new FixVector3(
            Fix64.FromRaw(_maxXRaw),
            Fix64.FromRaw(_maxYRaw),
            Fix64.FromRaw(_maxZRaw));

        /// <remarks>NoAlloc.</remarks>
        public FixVector3 Center => (Min + Max) / Fix64.FromInt(2);

        /// <remarks>NoAlloc.</remarks>
        public FixVector3 HalfExtents => (Max - Min) / Fix64.FromInt(2);

        /// <remarks>NoAlloc.</remarks>
        internal GameplayTriggerCombatSensorDescriptor ToDescriptor()
        {
            var header = new CombatQueryHeader(
                _queryId,
                CombatQueryKind.Aabb,
                CombatEntityId.None,
                _queryId,
                _queryId,
                _sourceBindingId,
                new CombatPhysicsLayerMask(_layerMask));
            return new GameplayTriggerCombatSensorDescriptor(
                new GameplayTriggerSourceBindingId(_sourceBindingId),
                new GameplayTriggerSourceProviderId(_providerId),
                CombatPhysicsQuery.From(new CombatAabbQuery(header, Min, Max)),
                _maxQueryHits,
                _maxMappedSubjects,
                SubjectFilter);
        }
    }

    internal sealed class CharacterTestGameplayTriggerSceneCacheData
    {
        internal bool Enabled { get; set; } = true;
        internal string LevelFormat { get; set; } = string.Empty;
        internal int LevelSchemaVersion { get; set; }
        internal int ModuleSchemaVersion { get; set; }
        internal int TriggerEntryCount { get; set; }
        internal string AuthoringContentHash { get; set; } = string.Empty;
        internal string TriggerSourceHash { get; set; } = string.Empty;
        internal string CanonicalTriggerEnvelopeJson { get; set; } = string.Empty;
        internal CharacterTestGameplayTriggerSensorCacheRow[] SensorRows { get; set; } =
            Array.Empty<CharacterTestGameplayTriggerSensorCacheRow>();
        internal CharacterTestGameplayTriggerAttributeThresholdCacheRow[]
            AttributeThresholdRows { get; set; } =
                Array.Empty<
                    CharacterTestGameplayTriggerAttributeThresholdCacheRow>();
        internal long TriggerCatalogHash { get; set; }
        internal long SensorCatalogHash { get; set; }
        internal long AttributeThresholdCatalogHash { get; set; }
        internal long CombinedCatalogHash { get; set; }
        internal string LastAppliedUtc { get; set; } = string.Empty;
    }
}
