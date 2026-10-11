using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    /// <summary>
    /// Global authoring source for reusable single-weapon configurations.
    /// AllocByDesign: loaded and edited only by low-frequency authoring workflows.
    /// </summary>
    public sealed class GlobalWeaponLibrary
    {
        public string SchemaVersion { get; set; } = GlobalWeaponLibrarySchema.CurrentSchemaVersion;
        public string LibraryId { get; set; } = GlobalWeaponLibrarySchema.DefaultLibraryId;
        public List<MxFramework.CharacterApplication.WeaponTypeDefinition> WeaponTypes { get; set; } =
            new List<MxFramework.CharacterApplication.WeaponTypeDefinition>();
        public List<SingleWeaponConfiguration> SingleWeaponConfigurations { get; set; } =
            new List<SingleWeaponConfiguration>();
        public List<GlobalAnimationConfiguration> AnimationConfigurations { get; set; } =
            new List<GlobalAnimationConfiguration>();
        public List<GlobalAbilityConfiguration> AbilityConfigurations { get; set; } =
            new List<GlobalAbilityConfiguration>();
        public List<GlobalReactionConfiguration> ReactionConfigurations { get; set; } =
            new List<GlobalReactionConfiguration>();
        public List<CombinedWeaponConfigurationAuthoringSummary> CombinedWeaponConfigurations { get; set; } =
            new List<CombinedWeaponConfigurationAuthoringSummary>();
    }

    public sealed class SingleWeaponConfiguration
    {
        public string SingleWeaponConfigurationId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string WeaponTypeId { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> AllowedSlots { get; set; } = new List<string>();
        public List<string> OccupiesSlots { get; set; } = new List<string>();
        public AuthoringResourceSelectionRef WeaponRef { get; set; }
        public CharacterAuthoringLocalPose ModelPose { get; set; } = new CharacterAuthoringLocalPose
        {
            ParentKind = CharacterPoseParentKind.ModelRoot
        };
        public SingleWeaponPresentationConfiguration PresentationConfiguration { get; set; } =
            new SingleWeaponPresentationConfiguration();
        public SingleWeaponCombatConfiguration CombatConfiguration { get; set; } =
            new SingleWeaponCombatConfiguration();
        public SingleWeaponResourcePolicy ResourcePolicy { get; set; } =
            new SingleWeaponResourcePolicy();
        public List<SingleWeaponTraceProfile> TraceProfiles { get; set; } =
            new List<SingleWeaponTraceProfile>();
        public List<SingleWeaponModifierConfiguration> BaseModifiers { get; set; } =
            new List<SingleWeaponModifierConfiguration>();
    }

    public sealed class SingleWeaponPresentationConfiguration
    {
        public string ProfileId { get; set; } = string.Empty;
        public string DisplayMode { get; set; } = "Equipped";
        public float ModelScale { get; set; } = 1f;
        /// <summary>Formal VFX resource key. Empty means this weapon has no default blade trail.</summary>
        public string BladeTrailResourceKey { get; set; } = string.Empty;
        public int BladeTrailResidueMilliseconds { get; set; } = 100;
        /// <summary>0 uses the VFX resource default; 1 thin, 2 ribbon, 3 double, 4 energy.</summary>
        public int BladeTrailStyleId { get; set; }
    }

    public sealed class SingleWeaponCombatConfiguration
    {
        public string ProfileId { get; set; } = string.Empty;
        public float ReachMeters { get; set; }
        public float ImpactScale { get; set; } = 1f;
        public List<string> CombatTags { get; set; } = new List<string>();
    }

    public sealed class SingleWeaponResourcePolicy
    {
        public string ProfileId { get; set; } = string.Empty;
        public string PreloadPolicy { get; set; } = "EquipmentInitial";
        public bool RequiredForSpawn { get; set; } = true;
    }

    public sealed class SingleWeaponTraceDefaults
    {
        public float Radius { get; set; } = 0.05f;
        public string SampleRule { get; set; } = "CapsuleSweep";
        public float LengthMeters { get; set; } = 1f;
    }

    /// <summary>
    /// Weapon-local hit trace owned by a reusable single-weapon configuration.
    /// AllocByDesign: authoring DTO containing editable lists and poses.
    /// </summary>
    public sealed class SingleWeaponTraceProfile
    {
        public string TraceId { get; set; } = string.Empty;
        public string StartLocatorPath { get; set; } = string.Empty;
        public string EndLocatorPath { get; set; } = string.Empty;
        public CharacterAuthoringLocalPose StartPose { get; set; } = new CharacterAuthoringLocalPose
        {
            ParentKind = CharacterPoseParentKind.ModelRoot
        };
        public CharacterAuthoringLocalPose EndPose { get; set; } = new CharacterAuthoringLocalPose
        {
            ParentKind = CharacterPoseParentKind.ModelRoot
        };
        public float Radius { get; set; } = 0.05f;
        public float ProximalExtension { get; set; }
        public float ProximalRadius { get; set; }
        public WeaponTraceSampleRule SampleRule { get; set; } = WeaponTraceSampleRule.CapsuleSweep;
        public int FixedSampleCount { get; set; } = 4;
        public List<string> ActionKeys { get; set; } = new List<string>();
    }

    public sealed class SingleWeaponModifierConfiguration
    {
        public string ModifierStableId { get; set; } = string.Empty;
        public float Value { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public sealed class GlobalAnimationConfiguration
    {
        public string AnimationConfigurationId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RigType { get; set; } = GlobalAnimationRigTypes.Unspecified;
        public string SkeletonProfileId { get; set; } = string.Empty;
        public string AvatarProfileId { get; set; } = string.Empty;
        public List<AnimationAuthoringSet> Sets { get; set; } = new List<AnimationAuthoringSet>();
        public List<AnimationAuthoringProfile> Profiles { get; set; } = new List<AnimationAuthoringProfile>();
    }

    public static class GlobalAnimationRigTypes
    {
        public const string Unspecified = "";
        public const string Humanoid = "Humanoid";
        public const string Generic = "Generic";

        public static string Normalize(string value)
        {
            if (string.Equals(value, Humanoid, System.StringComparison.OrdinalIgnoreCase))
                return Humanoid;
            if (string.Equals(value, Generic, System.StringComparison.OrdinalIgnoreCase))
                return Generic;
            return Unspecified;
        }
    }

    public sealed class GlobalAbilityConfiguration
    {
        public string AbilityConfigurationId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<GlobalAbilityEntry> Abilities { get; set; } = new List<GlobalAbilityEntry>();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<int> AbilityIds { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<int> RemoveAbilityIds { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<GlobalAbilitySlotBinding> SlotBindings { get; set; }
    }

    /// <summary>Migration-only schema 3.0 input. Schema 4.0 and later write GlobalAbilityEntry.</summary>
    public sealed class GlobalAbilitySlotBinding
    {
        public string SlotId { get; set; } = string.Empty;
        public int AbilityId { get; set; }
        public string InputIntentId { get; set; } = string.Empty;
        public string ActionRole { get; set; } = string.Empty;
    }

    public sealed class GlobalAbilityEntry
    {
        public int AbilityId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Tier { get; set; } = GlobalAbilityTiers.Unspecified;
        public List<string> Tags { get; set; } = new List<string>();
        public string ExecutionMode { get; set; } = GlobalAbilityExecutionModes.External;
        public string TriggerMode { get; set; } = GlobalAbilityTriggerModes.External;
        public string SlotId { get; set; } = string.Empty;
        public string InputIntentId { get; set; } = string.Empty;
        public string ActionBindingMode { get; set; } = GlobalAbilityActionBindingModes.None;
        public string ActionRole { get; set; } = string.Empty;
    }

    public static class GlobalAbilityTiers
    {
        public const string Unspecified = "Unspecified";
        public const string Basic = "Basic";
        public const string Special = "Special";
        public const string Passive = "Passive";
    }

    public static class GlobalAbilityExecutionModes
    {
        public const string ActionOnly = "ActionOnly";
        public const string GameplayRuntime = "GameplayRuntime";
        public const string External = "External";
    }

    public static class GlobalAbilityTriggerModes
    {
        public const string Input = "Input";
        public const string Event = "Event";
        public const string Passive = "Passive";
        public const string Automatic = "Automatic";
        public const string External = "External";
    }

    public static class GlobalAbilityActionBindingModes
    {
        public const string Required = "Required";
        public const string Optional = "Optional";
        public const string None = "None";
    }

    public sealed class GlobalReactionConfiguration
    {
        public string ReactionConfigurationId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DefaultSlotId { get; set; } = string.Empty;
        public List<GlobalReactionSlotConfiguration> Slots { get; set; } =
            new List<GlobalReactionSlotConfiguration>();
    }

    public sealed class GlobalReactionSlotConfiguration
    {
        public string SlotId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Trigger { get; set; } = string.Empty;
        public string PressureBand { get; set; } = string.Empty;
        public string ReactionGroupId { get; set; } = string.Empty;
        public int? MinImpactForce { get; set; }
        public int? MaxImpactForce { get; set; }
        public string ActionRole { get; set; } = string.Empty;
        public int DurationFrames { get; set; }
        public int Priority { get; set; }
        public string ResourceKey { get; set; } = string.Empty;
        public string PreloadPolicy { get; set; } = AuthoringResourcePreloadPolicies.AnimationWarmup;
        public bool Required { get; set; }
        public AuthoringResourceSelectionRef ResourceSelection { get; set; } =
            new AuthoringResourceSelectionRef();
    }

    public static class GlobalWeaponLibrarySchema
    {
        public const string CurrentSchemaVersion = "6.0";
        public const string DefaultLibraryId = "mx.global.single-weapons";
        public const string UnarmedConfigurationId = "weapon.unarmed";

        /// <summary>AllocByDesign: creates the initial editable global library.</summary>
        public static GlobalWeaponLibrary CreateDefault()
        {
            var library = new GlobalWeaponLibrary();
            library.SingleWeaponConfigurations.Add(CreateUnarmed());
            return library;
        }

        /// <summary>AllocByDesign: creates the immutable built-in unarmed definition.</summary>
        public static SingleWeaponConfiguration CreateUnarmed()
        {
            return new SingleWeaponConfiguration
            {
                SingleWeaponConfigurationId = UnarmedConfigurationId,
                DisplayName = "空手",
                Category = "Unarmed",
                Tags = new List<string> { "unarmed" },
                AllowedSlots = new List<string>
                {
                    CharacterWeaponConfigurationValidator.MainHandSlotId,
                    CharacterWeaponConfigurationValidator.OffHandSlotId
                },
                OccupiesSlots = new List<string>(),
                WeaponRef = null
            };
        }

        public static bool IsUnarmed(string singleWeaponConfigurationId)
        {
            return string.Equals(
                singleWeaponConfigurationId,
                UnarmedConfigurationId,
                System.StringComparison.Ordinal);
        }
    }
}
