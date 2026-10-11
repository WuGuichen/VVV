using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    public sealed class CharacterApplicationCompileBoundary
    {
        public List<string> AuthoringOnly { get; set; } = new List<string>();
        public List<string> RuntimeConfigCandidates { get; set; } = new List<string>();
    }

    public sealed class CharacterApplicationAuthoringSummary
    {
        public string SchemaVersion { get; set; } = "8.0";
        public string AuthoringNote { get; set; } = string.Empty;
        public string CharacterStableId { get; set; } = string.Empty;
        public string BodyProfileStableId { get; set; } = string.Empty;
        public string AttributeProfileStableId { get; set; } = string.Empty;
        public string EquipmentSchemaStableId { get; set; } = string.Empty;
        public List<string> AdditionalActionLibraryIds { get; set; } = new List<string>();
        public string DefaultCombinedWeaponConfigurationId { get; set; } = string.Empty;
        public List<CharacterCombinedWeaponConfigurationBinding> CombinedWeaponConfigurationBindings { get; set; } =
            new List<CharacterCombinedWeaponConfigurationBinding>();

        /// <summary>
        /// Effective global defaults plus character overrides. This is hydrated after the package and
        /// global weapon library are loaded and is never serialized into the character package.
        /// </summary>
        [JsonIgnore]
        public List<CombinedWeaponConfigurationAuthoringSummary> CombinedWeaponConfigurations { get; set; } =
            new List<CombinedWeaponConfigurationAuthoringSummary>();
        public List<string> ResourceKeys { get; set; } = new List<string>();
        public CharacterGameplayCameraProfileAuthoring GameplayCameraProfile { get; set; }
        public MxFramework.CharacterApplication.CharacterPresentationImages PresentationImages { get; set; }
        [JsonIgnore]
        public List<CharacterAbilityLoadoutAuthoringSummary> AbilityLoadouts { get; set; } = new List<CharacterAbilityLoadoutAuthoringSummary>();
        [JsonIgnore]
        public List<CharacterAnimationGroupAuthoringSummary> AnimationGroups { get; set; } = new List<CharacterAnimationGroupAuthoringSummary>();
        [JsonIgnore]
        public List<CharacterAnimationProfileAuthoringSummary> AnimationProfiles { get; set; } = new List<CharacterAnimationProfileAuthoringSummary>();
        [JsonIgnore]
        public List<CharacterReactionProfileAuthoringSummary> ReactionProfiles { get; set; } = new List<CharacterReactionProfileAuthoringSummary>();
        public CharacterApplicationCompileBoundary CompileBoundary { get; set; } = new CharacterApplicationCompileBoundary();

        [JsonIgnore]
        public string LegacyDefaultWeaponConfigurationId { get; set; } = string.Empty;

        [JsonIgnore]
        public List<CharacterWeaponDefinitionAuthoringSummary> LegacyWeaponDefinitions { get; set; } =
            new List<CharacterWeaponDefinitionAuthoringSummary>();

        [JsonIgnore]
        public List<CharacterWeaponConfigurationAuthoringSummary> LegacyWeaponConfigurations { get; set; } =
            new List<CharacterWeaponConfigurationAuthoringSummary>();

        [JsonPropertyName("combinedWeaponConfigurations")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<CombinedWeaponConfigurationAuthoringSummary> LegacyCombinedWeaponConfigurationsJson
        {
            get { return null; }
            set { CombinedWeaponConfigurations = value ?? new List<CombinedWeaponConfigurationAuthoringSummary>(); }
        }

        [JsonPropertyName("abilityLoadouts")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<CharacterAbilityLoadoutAuthoringSummary> LegacyAbilityLoadoutsJson
        {
            get { return null; }
            set { AbilityLoadouts = value ?? new List<CharacterAbilityLoadoutAuthoringSummary>(); }
        }

        [JsonPropertyName("animationGroups")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<CharacterAnimationGroupAuthoringSummary> LegacyAnimationGroupsJson
        {
            get { return null; }
            set { AnimationGroups = value ?? new List<CharacterAnimationGroupAuthoringSummary>(); }
        }

        [JsonPropertyName("animationProfiles")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<CharacterAnimationProfileAuthoringSummary> LegacyAnimationProfilesJson
        {
            get { return null; }
            set { AnimationProfiles = value ?? new List<CharacterAnimationProfileAuthoringSummary>(); }
        }

        [JsonPropertyName("reactionProfiles")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<CharacterReactionProfileAuthoringSummary> LegacyReactionProfilesJson
        {
            get { return null; }
            set { ReactionProfiles = value ?? new List<CharacterReactionProfileAuthoringSummary>(); }
        }

        [JsonPropertyName("defaultWeaponConfigurationId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyDefaultWeaponConfigurationIdJson
        {
            get { return null; }
            set { LegacyDefaultWeaponConfigurationId = value ?? string.Empty; }
        }

        [JsonPropertyName("weaponDefinitions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<CharacterWeaponDefinitionAuthoringSummary> LegacyWeaponDefinitionsJson
        {
            get { return null; }
            set { LegacyWeaponDefinitions = value ?? new List<CharacterWeaponDefinitionAuthoringSummary>(); }
        }

        [JsonPropertyName("weaponConfigurations")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<CharacterWeaponConfigurationAuthoringSummary> LegacyWeaponConfigurationsJson
        {
            get { return null; }
            set { LegacyWeaponConfigurations = value ?? new List<CharacterWeaponConfigurationAuthoringSummary>(); }
        }
    }

    public sealed class CharacterWeaponDefinitionAuthoringSummary
    {
        public string WeaponId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> AllowedSlots { get; set; } = new List<string>();
        public List<string> OccupiesSlots { get; set; } = new List<string>();
        public AuthoringResourceSelectionRef WeaponRef { get; set; } = new AuthoringResourceSelectionRef();
        public string AbilityLoadoutId { get; set; } = string.Empty;
    }

    public sealed class CharacterWeaponConfigurationAuthoringSummary
    {
        public string ConfigurationId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<CharacterWeaponConfigurationSlotAuthoringSummary> Slots { get; set; } = new List<CharacterWeaponConfigurationSlotAuthoringSummary>();
        public string AnimationProfileId { get; set; } = string.Empty;
        public string ActionSetId { get; set; } = string.Empty;
        public string AbilityLoadoutId { get; set; } = string.Empty;
    }

    public sealed class CharacterWeaponConfigurationSlotAuthoringSummary
    {
        public string SlotId { get; set; } = string.Empty;
        public string WeaponId { get; set; } = string.Empty;
        public string AttachmentId { get; set; } = string.Empty;
    }

    public sealed class CombinedWeaponConfigurationAuthoringSummary
    {
        public string CombinedWeaponConfigurationId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<CombinedWeaponConfigurationSlotAuthoringSummary> Slots { get; set; } =
            new List<CombinedWeaponConfigurationSlotAuthoringSummary>();
        public string AnimationConfigurationId { get; set; } = string.Empty;
        public string AbilityConfigurationId { get; set; } = string.Empty;
        public string ReactionConfigurationId { get; set; } = string.Empty;
        public CombinedWeaponActionConfiguration ActionConfiguration { get; set; } =
            new CombinedWeaponActionConfiguration();

        [JsonIgnore]
        public string AnimationProfileId { get; set; } = string.Empty;
        [JsonIgnore]
        public string ActionSetId { get; set; } = string.Empty;
        [JsonIgnore]
        public string AbilityLoadoutId { get; set; } = string.Empty;

        [JsonPropertyName("animationProfileId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyAnimationProfileIdJson
        {
            get { return null; }
            set { AnimationProfileId = value ?? string.Empty; }
        }

        [JsonPropertyName("actionSetId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyActionSetIdJson
        {
            get { return null; }
            set { ActionSetId = value ?? string.Empty; }
        }

        [JsonPropertyName("abilityLoadoutId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyAbilityLoadoutIdJson
        {
            get { return null; }
            set { AbilityLoadoutId = value ?? string.Empty; }
        }
    }

    /// <summary>
    /// Character-local selection of one project-global combined weapon configuration.
    /// AllocByDesign: low-frequency authoring DTO.
    /// </summary>
    public sealed class CharacterCombinedWeaponConfigurationBinding
    {
        public string CombinedWeaponConfigurationId { get; set; } = string.Empty;
        public CharacterCombinedWeaponConfigurationOverrides Overrides { get; set; } =
            new CharacterCombinedWeaponConfigurationOverrides();
    }

    /// <summary>
    /// Optional character-specific behavior replacement. Empty fields inherit the public defaults;
    /// weapon slots are intentionally not overrideable.
    /// </summary>
    public sealed class CharacterCombinedWeaponConfigurationOverrides
    {
        public string AnimationConfigurationId { get; set; } = string.Empty;
        public string AbilityConfigurationId { get; set; } = string.Empty;
        public string ReactionConfigurationId { get; set; } = string.Empty;
        public CombinedWeaponActionConfiguration ActionConfiguration { get; set; }
    }

    public sealed class CombinedWeaponActionConfiguration
    {
        [JsonIgnore]
        public string ActionSetId { get; set; } = string.Empty;

        [JsonIgnore]
        public JsonElement? ActionSet { get; set; }

        [JsonPropertyName("actionSetId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyActionSetIdJson
        {
            get { return null; }
            set { ActionSetId = value ?? string.Empty; }
        }

        [JsonPropertyName("actionSet")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? LegacyActionSetJson
        {
            get { return null; }
            set { ActionSet = value; }
        }

        public List<CombinedWeaponActionEntry> Actions { get; set; } =
            new List<CombinedWeaponActionEntry>();
        public List<CombinedWeaponCommandBinding> CommandBindings { get; set; } =
            new List<CombinedWeaponCommandBinding>();
        public List<CombinedWeaponCommandSequence> CommandSequences { get; set; } =
            new List<CombinedWeaponCommandSequence>();
        public List<CombinedWeaponAbilityActionBinding> AbilityActionBindings { get; set; } =
            new List<CombinedWeaponAbilityActionBinding>();
        public List<CombinedWeaponReactionActionBinding> ReactionActionBindings { get; set; } =
            new List<CombinedWeaponReactionActionBinding>();
    }

    public sealed class CombinedWeaponCommandBinding
    {
        public string IntentId { get; set; } = string.Empty;
        public string ActionRole { get; set; } = string.Empty;
        public int ActionId { get; set; }
        public int Priority { get; set; }
        public bool AllowQueue { get; set; }
        public int QueueWindowFrames { get; set; }
    }

    public sealed class CombinedWeaponActionEntry
    {
        public string ActionRole { get; set; } = string.Empty;
        public int ActionId { get; set; }

        [JsonIgnore]
        public string TimelineId { get; set; } = string.Empty;
        [JsonIgnore]
        public string AnimationClipId { get; set; } = string.Empty;
        [JsonIgnore]
        public JsonElement? Action { get; set; }
        [JsonIgnore]
        public JsonElement? Timeline { get; set; }

        [JsonPropertyName("timelineId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyTimelineIdJson
        {
            get { return null; }
            set { TimelineId = value ?? string.Empty; }
        }

        [JsonPropertyName("animationClipId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyAnimationClipIdJson
        {
            get { return null; }
            set { AnimationClipId = value ?? string.Empty; }
        }

        [JsonPropertyName("action")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? LegacyActionJson
        {
            get { return null; }
            set { Action = value; }
        }

        [JsonPropertyName("timeline")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? LegacyTimelineJson
        {
            get { return null; }
            set { Timeline = value; }
        }
    }

    public sealed class CombinedWeaponCommandSequence
    {
        public string IntentId { get; set; } = string.Empty;
        public List<string> ActionRoles { get; set; } = new List<string>();
        public bool WrapAfterLast { get; set; }
        public int Priority { get; set; }
        public bool AllowQueue { get; set; }
        public int QueueWindowFrames { get; set; }
    }

    public sealed class CombinedWeaponAbilityActionBinding
    {
        public int AbilityId { get; set; }
        public string ActionRole { get; set; } = string.Empty;
        public int ActionId { get; set; }
    }

    public sealed class CombinedWeaponReactionActionBinding
    {
        public string ReactionSlotId { get; set; } = string.Empty;
        public string ActionRole { get; set; } = string.Empty;
        public int ActionId { get; set; }
    }

    public sealed class CombinedWeaponConfigurationSlotAuthoringSummary
    {
        public string SlotId { get; set; } = string.Empty;
        public string SingleWeaponConfigurationId { get; set; } = string.Empty;
    }

    public sealed class CharacterAbilityLoadoutAuthoringSummary
    {
        public string LoadoutId { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SourceKind { get; set; } = string.Empty;
        public string SourceStableId { get; set; } = string.Empty;
        public List<int> AbilityIds { get; set; } = new List<int>();
        public List<int> RemoveAbilityIds { get; set; } = new List<int>();
        public List<CharacterAbilitySlotBindingAuthoringSummary> SlotBindings { get; set; } = new List<CharacterAbilitySlotBindingAuthoringSummary>();
    }

    public sealed class CharacterAbilitySlotBindingAuthoringSummary
    {
        public string SlotId { get; set; } = string.Empty;
        public int AbilityId { get; set; }
        public string InputIntentId { get; set; } = string.Empty;
    }

    public sealed class CharacterAnimationGroupAuthoringSummary
    {
        public string GroupId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SourceResourceKey { get; set; } = string.Empty;
        public string AnimationPolicy { get; set; } = "splitByClipNames";
        public string DefaultBlendSpaceId { get; set; } = string.Empty;
        public List<CharacterAnimationClipAuthoringSummary> Clips { get; set; } = new List<CharacterAnimationClipAuthoringSummary>();
        public List<CharacterAnimationBlendSpaceAuthoringSummary> BlendSpaces { get; set; } = new List<CharacterAnimationBlendSpaceAuthoringSummary>();
    }

    public sealed class CharacterAnimationClipAuthoringSummary
    {
        public string ClipId { get; set; } = string.Empty;
        public string SourceClipName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Loop { get; set; }
        public string RootMotionPolicy { get; set; } = "Ignore";
        public float Speed { get; set; } = 1f;
    }

    public sealed class CharacterAnimationBlendSpaceAuthoringSummary
    {
        public string BlendSpaceId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string XParameter { get; set; } = "moveX";
        public string YParameter { get; set; } = "moveY";
        public string DefaultClipId { get; set; } = string.Empty;
        public List<CharacterAnimationBlendSpaceClipAuthoringSummary> Clips { get; set; } = new List<CharacterAnimationBlendSpaceClipAuthoringSummary>();
    }

    public sealed class CharacterAnimationBlendSpaceClipAuthoringSummary
    {
        public string ClipId { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
    }

    public sealed class CharacterAnimationProfileAuthoringSummary
    {
        public string ProfileId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<CharacterAnimationSlotAuthoringSummary> Slots { get; set; } = new List<CharacterAnimationSlotAuthoringSummary>();
    }

    public sealed class CharacterAnimationSlotAuthoringSummary
    {
        public string SlotId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string AnimationGroupId { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
        public string PreloadPolicy { get; set; } = AuthoringResourcePreloadPolicies.AnimationWarmup;
        public bool Required { get; set; }
        public AuthoringResourceSelectionRef ResourceSelection { get; set; } = new AuthoringResourceSelectionRef();
    }

    public sealed class CharacterReactionProfileAuthoringSummary
    {
        public string ProfileId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DefaultSlotId { get; set; } = string.Empty;
        public List<CharacterReactionSlotAuthoringSummary> Slots { get; set; } = new List<CharacterReactionSlotAuthoringSummary>();
    }

    public sealed class CharacterReactionSlotAuthoringSummary
    {
        public string SlotId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Trigger { get; set; } = string.Empty;
        public string PressureBand { get; set; } = string.Empty;
        public string ReactionGroupId { get; set; } = string.Empty;
        public int? MinImpactForce { get; set; }
        public int? MaxImpactForce { get; set; }
        public int ActionId { get; set; }
        public int DurationFrames { get; set; }
        public int Priority { get; set; }
        public string ResourceKey { get; set; } = string.Empty;
        public string PreloadPolicy { get; set; } = AuthoringResourcePreloadPolicies.AnimationWarmup;
        public bool Required { get; set; }
        public AuthoringResourceSelectionRef ResourceSelection { get; set; } = new AuthoringResourceSelectionRef();
    }

    public sealed class CharacterGameplayCameraProfileAuthoring
    {
        public string ProfileId { get; set; } = string.Empty;
        public string FocusAbstractBoneId { get; set; } = string.Empty;
        public CharacterAuthoringVector3 FocusOffset { get; set; } = new CharacterAuthoringVector3();
        public CharacterAuthoringVector3 FramingBoundsCenter { get; set; } = new CharacterAuthoringVector3();
        public CharacterAuthoringVector3 FramingBoundsExtents { get; set; } = new CharacterAuthoringVector3();
        public CharacterGameplayCameraNormalAuthoring Normal { get; set; } = new CharacterGameplayCameraNormalAuthoring();
        public CharacterGameplayCameraLockedAuthoring Locked { get; set; } = new CharacterGameplayCameraLockedAuthoring();
        public CharacterGameplayCameraObstructionAuthoring Obstruction { get; set; } = new CharacterGameplayCameraObstructionAuthoring();
    }

    public sealed class CharacterGameplayCameraNormalAuthoring
    {
        public float Distance { get; set; } = 2f;
        public float MinDistance { get; set; } = 1.5f;
        public float MaxDistance { get; set; } = 10f;
        public float FieldOfView { get; set; } = 60f;
        public float Pitch { get; set; } = 28f;
        public float YawOffset { get; set; }
        public float TargetPadding { get; set; } = 0.35f;
    }

    public sealed class CharacterGameplayCameraLockedAuthoring
    {
        public float MinDistance { get; set; } = 1.5f;
        public float MaxDistance { get; set; } = 10f;
        public float TargetScreenY { get; set; } = 0.66f;
        public float TargetPadding { get; set; } = 0.35f;
        public float FramingScale { get; set; } = 1f;
    }

    public sealed class CharacterGameplayCameraObstructionAuthoring
    {
        public float Radius { get; set; } = 0.22f;
        public float Padding { get; set; } = 0.12f;
        public float MinDistance { get; set; } = 0.75f;
        public float FocusClearance { get; set; } = 0.25f;
    }
}
