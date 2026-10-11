using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    public sealed class AbilityAuthoringDocument
    {
        public string SchemaVersion { get; set; } = "1";
        public string PackageId { get; set; } = string.Empty;
        [JsonIgnore]
        public List<AbilityAuthoringDiagnostic> SourceDiagnostics { get; set; } = new List<AbilityAuthoringDiagnostic>();
        public List<AbilityDefinitionConfig> Abilities { get; set; } = new List<AbilityDefinitionConfig>();
        public List<VfxDefinitionConfig> VfxDefinitions { get; set; } = new List<VfxDefinitionConfig>();
        public List<VfxPresentationProfileConfig> VfxPresentationProfiles { get; set; } = new List<VfxPresentationProfileConfig>();
        public List<VfxTriggerMapConfig> VfxTriggerMaps { get; set; } = new List<VfxTriggerMapConfig>();
        public List<ProjectileDefinitionConfig> ProjectileDefinitions { get; set; } = new List<ProjectileDefinitionConfig>();
        public List<ProjectileSpawnProfileConfig> ProjectileSpawnProfiles { get; set; } = new List<ProjectileSpawnProfileConfig>();
        public List<ProjectilePresentationBindingConfig> ProjectilePresentationBindings { get; set; } = new List<ProjectilePresentationBindingConfig>();
        public List<AbilityEffectTimelineConfig> EffectTimelines { get; set; } = new List<AbilityEffectTimelineConfig>();
        public List<CharacterTestMechanicTuningConfig> CharacterTestMechanicTunings { get; set; } = new List<CharacterTestMechanicTuningConfig>();
        public List<string> KnownSocketIds { get; set; } = new List<string>();
        public List<AbilityResourceReferenceConfig> ResourceRefs { get; set; } = new List<AbilityResourceReferenceConfig>();
    }

    public sealed class ProjectileRequestAuthoringDocument
    {
        public string SchemaVersion { get; set; } = "1";
        public string StableId { get; set; } = string.Empty;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ProjectileDefinitionConfig Definition { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ProjectileSpawnProfileConfig SpawnProfile { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ProjectilePresentationBindingConfig PresentationBinding { get; set; }
    }

    public sealed class AbilityDefinitionConfig
    {
        public int AbilityId { get; set; }
        public string AbilityStableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public AbilityInputBindingConfig Input { get; set; } = new AbilityInputBindingConfig();
        public AbilityCooldownConfig Cooldown { get; set; } = new AbilityCooldownConfig();
        public List<AbilityCostConfig> Costs { get; set; } = new List<AbilityCostConfig>();
        public int TargetPolicyId { get; set; }
        public string TargetPolicyLabel { get; set; } = string.Empty;
        public AbilityActionBindingConfig ActionBinding { get; set; } = new AbilityActionBindingConfig();
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CombatHitDamageConfig Damage { get; set; }
        public int PresentationProfileId { get; set; }
        public string PresentationProfileLabel { get; set; } = string.Empty;
        public int EffectTimelineId { get; set; }
        public string EffectTimelineLabel { get; set; } = string.Empty;
    }

    public sealed class AbilityInputBindingConfig
    {
        public int InputIntentId { get; set; }
        public string InputIntentLabel { get; set; } = string.Empty;
        public int SlotId { get; set; }
        public string SlotLabel { get; set; } = string.Empty;
    }

    public sealed class AbilityCostConfig
    {
        public int AttributeId { get; set; }
        public int Amount { get; set; }
    }

    public sealed class AbilityCooldownConfig
    {
        public long Frames { get; set; }
        public int GroupId { get; set; }
        public string GroupLabel { get; set; } = string.Empty;
    }

    public sealed class AbilityActionBindingConfig
    {
        public int ActionId { get; set; }
        public int ActionIntentId { get; set; }
        public string ActionIntentLabel { get; set; } = string.Empty;
    }

    public sealed class VfxDefinitionConfig
    {
        public int DefinitionId { get; set; }
        public string StableId { get; set; } = string.Empty;
        public AbilityResourceKeyConfig PrefabResource { get; set; } = new AbilityResourceKeyConfig();
        public int DefaultLifetimeFrames { get; set; }
        public string DefinitionHash { get; set; } = string.Empty;
    }

    public sealed class VfxPresentationProfileConfig
    {
        public int PresentationProfileId { get; set; }
        public string PresentationProfileLabel { get; set; } = string.Empty;
        public int DefinitionId { get; set; }
        public string AnchorStrategy { get; set; } = "HitPoint";
        public int LifetimeFramesOverride { get; set; }
    }

    public sealed class VfxTriggerMapConfig
    {
        public string SourceDomain { get; set; } = string.Empty;
        public int SourceDomainId { get; set; }
        public int TriggerId { get; set; }
        public int PresentationProfileId { get; set; }
        public string PresentationProfileLabel { get; set; } = string.Empty;
        public int DefinitionId { get; set; }
        public int LifetimeFramesOverride { get; set; }
    }

    public sealed class AbilityResourceReferenceConfig
    {
        public AbilityResourceKeyConfig ResourceKey { get; set; } = new AbilityResourceKeyConfig();
        public string Address { get; set; } = string.Empty;
        public string CatalogId { get; set; } = string.Empty;
    }

    public sealed class CharacterTestMechanicTuningConfig
    {
        public string StableId { get; set; } = string.Empty;
        public string AbilityStableId { get; set; } = string.Empty;
        public string SourceStableId { get; set; } = string.Empty;
        public string InterruptPolicy { get; set; } = "CancelOnDamageOrActionExit";
        public int TelegraphFrames { get; set; }
        public int ZoneDurationFrames { get; set; }
        public int ZoneRadiusCentimeters { get; set; }
        public int FadeInFrames { get; set; }
        public int FadeOutFrames { get; set; }
        public int ReleaseFrames { get; set; }
        public int TelegraphCancelFrames { get; set; }
        public int TelegraphVfxDefinitionId { get; set; }
        public int TelegraphCancelVfxDefinitionId { get; set; }
        public int ZoneVfxDefinitionId { get; set; }
        public int CharacterVfxDefinitionId { get; set; }
        public int CharacterReleaseVfxDefinitionId { get; set; }
        public int DeployRangeMeters { get; set; }
        public int FallbackForwardMeters { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ArmDelayFrames { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int FuseDelayFrames { get; set; }
        public int DurationFrames { get; set; }
        public int TriggerRadiusCentimeters { get; set; }
        public int BlastRadiusCentimeters { get; set; }
        public int MaxBlastTargets { get; set; }
        public int DeployVfxDefinitionId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int FuseVfxDefinitionId { get; set; }
        public int TriggerVfxDefinitionId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int WarmupFrames { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ScanRadiusCentimeters { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ScanIntervalFrames { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int FireIntervalFrames { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MaxShots { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MuzzleHeightCentimeters { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int IdleVfxDefinitionId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int FireVfxDefinitionId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ExpireVfxDefinitionId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MaxStamina { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int RegenerationDelayFrames { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int RegenerationPerSecond { get; set; }
    }

    public sealed class ProjectileDefinitionConfig
    {
        public int DefinitionId { get; set; }
        public string StableId { get; set; } = string.Empty;
        public bool PhysicalEventsEnabled { get; set; }
        public ProjectileMotionConfig Motion { get; set; } = new ProjectileMotionConfig();
        public ProjectileCollisionConfig Collision { get; set; } = new ProjectileCollisionConfig();
        public ProjectileImpactConfig Impact { get; set; } = new ProjectileImpactConfig();
    }

    public sealed class ProjectileMotionConfig
    {
        public string Kind { get; set; } = "Linear";
        public float SpeedPerFrame { get; set; }
        public int LifetimeFrames { get; set; }
        public float ArcHeight { get; set; }
        public float GravityPerFrameSquared { get; set; }
        public float MaxLaunchAngleDegrees { get; set; }
        public float MaxLaunchAngleTangent { get; set; }
        public float LateralAmplitude { get; set; }
        public int LateralCycles { get; set; }
        public ProjectileVector3Config BezierControlOffsetA { get; set; } = new ProjectileVector3Config();
        public ProjectileVector3Config BezierControlOffsetB { get; set; } = new ProjectileVector3Config();
        public ProjectileSpeedProfileConfig SpeedProfile { get; set; } = new ProjectileSpeedProfileConfig();
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ProjectileHomingConfig Homing { get; set; }
    }

    public sealed class ProjectileSpeedProfileConfig
    {
        public string Kind { get; set; } = "Constant";
    }

    public sealed class ProjectileHomingConfig
    {
        public float TurnRatePerFrame { get; set; } = 1f;
    }

    public sealed class ProjectileCollisionConfig
    {
        public float Radius { get; set; }
        public int LayerMask { get; set; }
        public bool StopOnFirstAcceptedImpact { get; set; } = true;
        public bool StopOnBlockedImpact { get; set; }
        public int MaxAcceptedImpacts { get; set; } = 1;
        public string HitSameTargetPolicy { get; set; } = "AllowRepeat";
    }

    public sealed class ProjectileImpactConfig
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int PresentationProfileId { get; set; }
        public string PresentationProfileLabel { get; set; } = string.Empty;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int StableEventId { get; set; }
        public string StableEventLabel { get; set; } = string.Empty;
        public string PolicyKind { get; set; } = "DirectHit";
        public float AreaRadius { get; set; }
        public int AreaLayerMask { get; set; }
        public bool TriggerOnExpire { get; set; }
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int EffectTimelineId { get; set; }
        public string EffectTimelineLabel { get; set; } = string.Empty;
        public CombatHitEventConfig HitEvent { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CombatHitEventConfig AreaHitEvent { get; set; }
    }

    public sealed class CombatHitEventConfig
    {
        public List<string> TargetTypes { get; set; } = new List<string>();
        public string EnvironmentHitPolicy { get; set; } = "None";
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int StableEventId { get; set; }
        public string StableEventLabel { get; set; } = string.Empty;
        public int MaxAcceptedHits { get; set; } = 1;
        public string HitSameTargetPolicy { get; set; } = "AllowRepeat";
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CombatHitDamageConfig Damage { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<CombatHitBuffApplicationConfig> OnHitBuffs { get; set; }
    }

    public sealed class CombatHitBuffApplicationConfig
    {
        public int BuffId { get; set; }
    }

    public sealed class CombatHitDamageConfig
    {
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int DamageTypeId { get; set; }
        public string DamageTypeLabel { get; set; } = string.Empty;
        public int BaseDamage { get; set; }
        public List<CombatDamageAttributeScaleConfig> SourceAttributeScales { get; set; } = new List<CombatDamageAttributeScaleConfig>();
        public int ImpactPower { get; set; }
        public int LaunchPower { get; set; }
        [JsonConverter(typeof(FlexibleIntListJsonConverter))]
        public List<int> Tags { get; set; } = new List<int>();
        public List<string> TagLabels { get; set; } = new List<string>();
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int AnalysisCategory { get; set; }
        public string AnalysisCategoryLabel { get; set; } = string.Empty;
    }

    public sealed class CombatDamageAttributeScaleConfig
    {
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int AttributeId { get; set; }
        public float Scale { get; set; }
    }

    public sealed class ProjectileSpawnProfileConfig
    {
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int ProfileId { get; set; }
        public string StableId { get; set; } = string.Empty;
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int ProjectileDefinitionId { get; set; }
        public string ProjectileStableId { get; set; } = string.Empty;
        public ProjectileSpawnOriginConfig Origin { get; set; } = new ProjectileSpawnOriginConfig();
        public ProjectileAimConfig Aim { get; set; } = new ProjectileAimConfig();
    }

    public sealed class ProjectileSpawnOriginConfig
    {
        public string Kind { get; set; } = "Socket";
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int SocketId { get; set; }
        public string SocketLabel { get; set; } = string.Empty;
        public ProjectileVector3Config LocalOffset { get; set; } = new ProjectileVector3Config();
        public ProjectileVector3Config FallbackRootOffset { get; set; } = new ProjectileVector3Config
        {
            Y = 1.15f,
            Z = 0.65f
        };
    }

    public sealed class ProjectileAimConfig
    {
        public string Kind { get; set; } = "CasterForwardRange";
        public float TargetHeightOffset { get; set; }
        public string FallbackKind { get; set; } = "CasterForwardRange";
        public float MaxRange { get; set; }
    }

    public sealed class ProjectileVector3Config
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public sealed class ProjectilePresentationBindingConfig
    {
        public int ProjectileDefinitionId { get; set; }
        [JsonIgnore]
        public AbilityResourceKeyConfig PrefabResource { get; set; }
        [JsonPropertyName("prefabResource")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AbilityResourceKeyConfig LegacyPrefabResource
        {
            get => null;
            set => PrefabResource = value;
        }
        public string ViewNamePrefix { get; set; } = "ProjectileVFX";
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ProjectilePresentationSlotSetConfig Slots { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ProjectilePresentationExitPolicyConfig ExitPolicy { get; set; }
    }

    /// <summary>Fixed-order projectile lifecycle presentation slots.</summary>
    /// <remarks>LowFreqAlloc: created and normalized during authoring compile/import.</remarks>
    public sealed class ProjectilePresentationSlotSetConfig
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ProjectilePresentationSlotConfig Spawn { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ProjectilePresentationSlotConfig FlightBody { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ProjectilePresentationSlotConfig Trail { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ProjectilePresentationSlotConfig DirectImpact { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ProjectilePresentationSlotConfig AreaImpact { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ProjectilePresentationSlotConfig BlockedImpact { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ProjectilePresentationSlotConfig Expire { get; set; }
    }

    /// <summary>One configured projectile lifecycle presentation slot.</summary>
    /// <remarks>LowFreqAlloc: authoring-time DTO; runtime imports an immutable projection.</remarks>
    public sealed class ProjectilePresentationSlotConfig
    {
        public AbilityResourceKeyConfig Resource { get; set; } = new AbilityResourceKeyConfig();
        public string Orientation { get; set; } = string.Empty;
        public ProjectileVector3Config LocalOffset { get; set; } = new ProjectileVector3Config();
        public int LifetimeFramesOverride { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int VfxDefinitionId { get; set; }
    }

    /// <summary>Flight and trail exit policy for projectile presentation.</summary>
    /// <remarks>LowFreqAlloc: normalized during authoring compile/import.</remarks>
    public sealed class ProjectilePresentationExitPolicyConfig
    {
        public string AcceptedImpact { get; set; } = "Complete";
        public string BlockedImpact { get; set; } = "Dissipate";
        public string Expiration { get; set; } = "Complete";
        public string ExternalRemoval { get; set; } = "Dissipate";
        public string RuntimeReset { get; set; } = "Immediate";
    }

    public sealed class AbilityEffectTimelineConfig
    {
        public int TimelineId { get; set; }
        public string TimelineLabel { get; set; } = string.Empty;
        public int DurationFrames { get; set; }
        public List<AbilityEffectTimelineEventConfig> Events { get; set; } = new List<AbilityEffectTimelineEventConfig>();
    }

    public sealed class AbilityEffectTimelineEventConfig
    {
        public int EventId { get; set; }
        public string EventLabel { get; set; } = string.Empty;
        public string TrackKind { get; set; } = "Gameplay";
        public string EventKind { get; set; } = "EmitGameplayEvent";
        public int Frame { get; set; }
        public int EndFrame { get; set; } = -1;
        public string ShapeKind { get; set; } = "None";
        public string AnchorMode { get; set; } = "CapturedAnchor";
        public ProjectileVector3Config LocalOffset { get; set; } = new ProjectileVector3Config();
        public ProjectileVector3Config LocalPointB { get; set; } = new ProjectileVector3Config();
        public float Radius { get; set; }
        public int LayerMask { get; set; }
        public int TargetTimelineId { get; set; }
        public string TargetTimelineLabel { get; set; } = string.Empty;
        public int RequestId { get; set; }
        public string RequestLabel { get; set; } = string.Empty;
        public int EffectId { get; set; }
        public int PresentationProfileId { get; set; }
        public string PresentationProfileLabel { get; set; } = string.Empty;
        public bool RequiresOwner { get; set; }
        public bool RequiresSource { get; set; }
    }

    public sealed class AbilityResourceKeyConfig
    {
        public string Id { get; set; } = string.Empty;
        public string TypeId { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
    }

    public sealed class AbilityAuthoringCompileResult
    {
        public string PackageId { get; set; } = string.Empty;
        public AbilityAuthoringValidationReport ValidationReport { get; set; } = new AbilityAuthoringValidationReport();
        public AbilityRuntimeCatalogDocument AbilityCatalog { get; set; } = new AbilityRuntimeCatalogDocument();
        public AbilityVfxDefinitionCatalogDocument VfxDefinitionCatalog { get; set; } = new AbilityVfxDefinitionCatalogDocument();
        public AbilityVfxTriggerMapDocument VfxTriggerMap { get; set; } = new AbilityVfxTriggerMapDocument();
        public AbilityActionReferenceCatalogDocument ActionReferenceCatalog { get; set; } = new AbilityActionReferenceCatalogDocument();
        public AbilityProjectileDefinitionCatalogDocument ProjectileDefinitionCatalog { get; set; } = new AbilityProjectileDefinitionCatalogDocument();
        public AbilityProjectileSpawnProfileCatalogDocument ProjectileSpawnProfileCatalog { get; set; } = new AbilityProjectileSpawnProfileCatalogDocument();
        public AbilityProjectilePresentationBindingDocument ProjectilePresentationBindings { get; set; } = new AbilityProjectilePresentationBindingDocument();
        public AbilityEffectTimelineCatalogDocument EffectTimelineCatalog { get; set; } = new AbilityEffectTimelineCatalogDocument();
        public CharacterTestMechanicTuningCatalogDocument CharacterTestMechanicTuningCatalog { get; set; } = new CharacterTestMechanicTuningCatalogDocument();
    }

    public sealed class AbilityAuthoringValidationReport
    {
        public string PackageId { get; set; } = string.Empty;
        public string Status { get; set; } = "Ready";
        public List<AbilityAuthoringDiagnostic> Diagnostics { get; set; } = new List<AbilityAuthoringDiagnostic>();
        public bool HasBlockingIssues { get; set; }

        public string ToText()
        {
            var lines = new List<string>
            {
                "packageId=" + (PackageId ?? string.Empty),
                "status=" + (Status ?? string.Empty),
                "diagnosticCount=" + Diagnostics.Count
            };
            for (int i = 0; i < Diagnostics.Count; i++)
            {
                AbilityAuthoringDiagnostic diagnostic = Diagnostics[i];
                lines.Add(
                    diagnostic.Severity + " "
                    + diagnostic.Code + " "
                    + diagnostic.Path + " - "
                    + diagnostic.Message);
            }

            return string.Join(System.Environment.NewLine, lines) + System.Environment.NewLine;
        }
    }

    public sealed class AbilityAuthoringDiagnostic
    {
        public string Severity { get; set; } = "Info";
        public string Code { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class AbilityRuntimeCatalogDocument
    {
        public string SchemaVersion { get; set; } = "2";
        public string PackageId { get; set; } = string.Empty;
        public List<AbilityDefinitionConfig> Abilities { get; set; } = new List<AbilityDefinitionConfig>();
    }

    public sealed class AbilityVfxDefinitionCatalogDocument
    {
        public string SchemaVersion { get; set; } = "1";
        public string PackageId { get; set; } = string.Empty;
        public List<VfxDefinitionConfig> Definitions { get; set; } = new List<VfxDefinitionConfig>();
    }

    public sealed class AbilityVfxTriggerMapDocument
    {
        public string SchemaVersion { get; set; } = "2";
        public string PackageId { get; set; } = string.Empty;
        public List<VfxPresentationProfileConfig> PresentationProfiles { get; set; } = new List<VfxPresentationProfileConfig>();
        public List<VfxTriggerMapConfig> Mappings { get; set; } = new List<VfxTriggerMapConfig>();
    }

    public sealed class AbilityActionReferenceCatalogDocument
    {
        public string SchemaVersion { get; set; } = "1";
        public string PackageId { get; set; } = string.Empty;
        public List<AbilityActionReferenceConfig> ActionReferences { get; set; } = new List<AbilityActionReferenceConfig>();
    }

    public sealed class CharacterTestMechanicTuningCatalogDocument
    {
        public string SchemaVersion { get; set; } = "1";
        public string PackageId { get; set; } = string.Empty;
        public List<CharacterTestMechanicTuningConfig> Tunings { get; set; } = new List<CharacterTestMechanicTuningConfig>();
    }

    public sealed class AbilityProjectileDefinitionCatalogDocument
    {
        public string SchemaVersion { get; set; } = "4";
        public string PackageId { get; set; } = string.Empty;
        public List<ProjectileDefinitionConfig> Definitions { get; set; } = new List<ProjectileDefinitionConfig>();
    }

    public sealed class AbilityProjectileSpawnProfileCatalogDocument
    {
        public string SchemaVersion { get; set; } = "2";
        public string PackageId { get; set; } = string.Empty;
        public List<ProjectileSpawnProfileConfig> Profiles { get; set; } = new List<ProjectileSpawnProfileConfig>();
    }

    public sealed class AbilityProjectilePresentationBindingDocument
    {
        public string SchemaVersion { get; set; } = "2";
        public string PackageId { get; set; } = string.Empty;
        public List<ProjectilePresentationBindingConfig> Bindings { get; set; } = new List<ProjectilePresentationBindingConfig>();
    }

    public sealed class AbilityEffectTimelineCatalogDocument
    {
        public string SchemaVersion { get; set; } = "2";
        public string PackageId { get; set; } = string.Empty;
        public List<AbilityEffectTimelineConfig> Timelines { get; set; } = new List<AbilityEffectTimelineConfig>();
    }

    public sealed class AbilityActionReferenceConfig
    {
        public string AbilityStableId { get; set; } = string.Empty;
        public int ActionId { get; set; }
        public int ActionIntentId { get; set; }
        public string ActionIntentLabel { get; set; } = string.Empty;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CombatHitDamageConfig Damage { get; set; }
        public int TargetPolicyId { get; set; }
        public string TargetPolicyLabel { get; set; } = string.Empty;
        public int PresentationProfileId { get; set; }
        public string PresentationProfileLabel { get; set; } = string.Empty;
        public int EffectTimelineId { get; set; }
        public string EffectTimelineLabel { get; set; } = string.Empty;
    }

    public sealed class FlexibleIntJsonConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetInt32();
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                string s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s)) return 0;
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)) return result;
                return 0;
            }
            if (reader.TokenType == JsonTokenType.Null)
            {
                return 0;
            }
            return 0;
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
        }
    }

    public sealed class FlexibleIntListJsonConverter : JsonConverter<List<int>>
    {
        public override List<int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return new List<int>();

            if (reader.TokenType != JsonTokenType.StartArray)
                return new List<int>();

            var list = new List<int>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (reader.TokenType == JsonTokenType.Number)
                {
                    list.Add(reader.GetInt32());
                }
                else if (reader.TokenType == JsonTokenType.String)
                {
                    string s = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                    {
                        list.Add(result);
                    }
                    else
                    {
                        list.Add(0);
                    }
                }
            }
            return list;
        }

        public override void Write(Utf8JsonWriter writer, List<int> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            if (value != null)
            {
                foreach (int item in value)
                {
                    writer.WriteNumberValue(item);
                }
            }
            writer.WriteEndArray();
        }
    }
}
