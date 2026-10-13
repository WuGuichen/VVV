using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using MxFramework.ResourceMapping.Authoring;

namespace MxFramework.Authoring
{
    public static class CharacterAuthoringCompilerFormats
    {
        public const string CompileResult = "mx.characterAuthoringCompileResult.v1";
        public const string ConfigPatchBundle = "mx.characterApplicationConfigPatchBundle.v1";
        public const string GeometryBinding = "mx.characterGeometryBinding.v1";
        public const string GeometryBindingV2 = "mx.characterGeometryBinding.v2";
        public const string GeometryBindingV3 = "mx.characterGeometryBinding.v3";
        public const string GeometryBindingV4 = "mx.characterGeometryBinding.v4";
        public const string GeometryBindingV5 = "mx.characterGeometryBinding.v5";
        public const string GeometryBindingV6 = "mx.characterGeometryBinding.v6";
        public const string GeometryBindingV7 = "mx.characterGeometryBinding.v7";
        public const string GeometryBindingV8 = "mx.characterGeometryBinding.v8";
        public const string ResourceMapping = "mx.characterResourceMapping.v1";
        public const string UnityWritePlan = "mx.characterUnityImportWritePlan.v1";
    }

    public static class CharacterAuthoringCompilerWriteKinds
    {
        public const string GeneratedConfigPatch = "generatedConfigPatch";
        public const string GeometryBinding = "geometryBinding";
        public const string ResourceMapping = "resourceMapping";
        public const string ResourceFile = "resourceFile";
    }

    public static class CharacterAuthoringCompilerOwnerKinds
    {
        public const string ExternalEditor = "ExternalEditor";
        public const string AuthoringCompiler = "AuthoringCompiler";
        public const string UnityImporter = "UnityImporter";
    }

    public static class CharacterApplicationCompilerTableNames
    {
        public const string CharacterConfig = "CharacterConfig";
        public const string CharacterAttributeProfileConfig = "CharacterAttributeProfileConfig";
        public const string CharacterBodyProfileConfig = "CharacterBodyProfileConfig";
        public const string CharacterBodyPartConfig = "CharacterBodyPartConfig";
        public const string CharacterBodyPartContactPointConfig = "CharacterBodyPartContactPointConfig";
        public const string EquipmentSchemaConfig = "EquipmentSchemaConfig";
        public const string EquipmentLoadoutConfig = "EquipmentLoadoutConfig";
        public const string EquipmentStateConfig = "EquipmentStateConfig";
        public const string WeaponConfig = "WeaponConfig";
        public const string AbilityLoadoutConfig = "AbilityLoadoutConfig";
        public const string CombatActionSetConfig = "CombatActionSetConfig";
        public const string CharacterPresentationProfileConfig = "CharacterPresentationProfileConfig";
        public const string SpawnProfileConfig = "SpawnProfileConfig";
    }

    public static class CharacterAuthoringCompilerValidationCodes
    {
        public const string CoordinateTargetMismatch = "CHARPKG_COORDINATE_TARGET_MISMATCH";
        public const string ResourceKeyConflict = "CHARPKG_RESOURCE_KEY_CONFLICT";
        public const string StrictWarningBlocked = "CHARPKG_STRICT_WARNING_BLOCKED";
        public const string EffectiveAnimationProfileMissing = "CHARPKG_EFFECTIVE_ANIMATION_PROFILE_MISSING";
        public const string EffectiveAnimationProfileAmbiguous = "CHARPKG_EFFECTIVE_ANIMATION_PROFILE_AMBIGUOUS";
        public const string EffectiveLocomotionSlotMissing = "CHARPKG_EFFECTIVE_LOCOMOTION_SLOT_MISSING";
        public const string EffectiveLocomotionSlotAmbiguous = "CHARPKG_EFFECTIVE_LOCOMOTION_SLOT_AMBIGUOUS";
    }

    public enum CharacterAuthoringCompilerStatus
    {
        Ready = 0,
        WarningOnly = 1,
        SpawnBlocked = 2,
        ImportBlocked = 3,
        ExportBlocked = 4
    }

    public sealed class CharacterAuthoringCompileOptions
    {
        public bool Strict { get; set; }
        public bool AllowWarnings { get; set; } = true;
        public bool ValidateResourceFiles { get; set; }
        public bool ValidateResourceHashes { get; set; }
        public string TargetOutputFormat { get; set; } = CharacterAuthoringCompilerFormats.ConfigPatchBundle;
        public string TargetUnityPathPolicy { get; set; } = CharacterPackageImportTargetPathPolicies.GeneratedCharacterPackage;
        public string GeneratedRootPath { get; set; } = "Assets/MxFrameworkGenerated/CharacterPackages";
        public CharacterPackageCoordinateConvention TargetCoordinateConvention { get; set; } = new CharacterPackageCoordinateConvention();
    }

    public sealed class CharacterAuthoringCompileRequest
    {
        public CharacterResourcePackage Package { get; set; }
        public string PackageRootPath { get; set; } = string.Empty;
        public ProjectAuthoringManifest ConfigSourceIndex { get; set; }
        public CharacterPackageResourceCatalog ExistingProjectResourceCatalogSummary { get; set; }
        public CharacterContentIdentityCatalogSnapshot IdentityCatalog { get; set; }
        public CharacterAuthoringCompileOptions Options { get; set; } = new CharacterAuthoringCompileOptions();
    }

    public sealed class CharacterAuthoringCompileResult
    {
        public string Format { get; set; } = CharacterAuthoringCompilerFormats.CompileResult;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string PackageStableId { get; set; } = string.Empty;
        public bool IsDeterministicFullCompile { get; set; } = true;
        public CharacterAuthoringCompilerStatus Status { get; set; }
        public CharacterCompilerHashSet Hashes { get; set; } = new CharacterCompilerHashSet();
        public CharacterCompilerGateReport GateReport { get; set; } = new CharacterCompilerGateReport();
        public CharacterAuthoringCompiledConfigPatch GeneratedConfigPatch { get; set; } = new CharacterAuthoringCompiledConfigPatch();
        public CharacterAuthoringGeometryBinding GeometryBinding { get; set; } = new CharacterAuthoringGeometryBinding();
        public CharacterPackageResourceMapping ResourceMapping { get; set; } = new CharacterPackageResourceMapping();
        public ResourceMappingProducerSnapshot ResourceMappingV2Snapshot { get; set; } = new ResourceMappingProducerSnapshot();
        public List<ResourceMappingDiagnostic> ResourceMappingV2Diagnostics { get; set; } = new List<ResourceMappingDiagnostic>();
        public CharacterUnityImportWritePlan UnityImportWritePlan { get; set; } = new CharacterUnityImportWritePlan();
        public CharacterResolverVerificationPlan ResolverVerificationPlan { get; set; } = new CharacterResolverVerificationPlan();
        public CharacterPackageDependencyGraph DependencyGraph { get; set; } = new CharacterPackageDependencyGraph();
        public CharacterPackageResourceHashReport ResourceHashReport { get; set; } = new CharacterPackageResourceHashReport();
        public List<CharacterPackageSourceMapping> SourceMappings { get; set; } = new List<CharacterPackageSourceMapping>();
        public AnimationAuthoringCompileResult AnimationCompileResult { get; set; } = new AnimationAuthoringCompileResult();
        public MxFramework.CharacterApplication.CharacterTacticalDefaults TacticalDefaults { get; set; }
    }

    public sealed class CharacterCompilerHashSet
    {
        public string Algorithm { get; set; } = "sha256";
        public string SourcePackageHash { get; set; } = string.Empty;
        public string GeneratedConfigHash { get; set; } = string.Empty;
        public string GeometryBindingHash { get; set; } = string.Empty;
        public string ResourceMappingHash { get; set; } = string.Empty;
        public string WritePlanHash { get; set; } = string.Empty;
    }

    public sealed class CharacterCompilerGateReport
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public CharacterAuthoringCompilerStatus Status { get; set; }
        public bool ExportBlocked { get; set; }
        public bool ImportBlocked { get; set; }
        public bool SpawnBlocked { get; set; }
        public bool WarningOnly { get; set; }
        public List<CharacterAuthoringValidationIssue> Issues { get; set; } = new List<CharacterAuthoringValidationIssue>();

        public bool HasGate(CharacterAuthoringValidationGate gate)
        {
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i] != null && Issues[i].Gate == gate)
                    return true;
            }

            return false;
        }
    }

    public sealed class CharacterAuthoringCompiledConfigPatch
    {
        public string Format { get; set; } = CharacterAuthoringCompilerFormats.ConfigPatchBundle;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string Layer { get; set; } = "Generated";
        public PatchDocument Patch { get; set; } = new PatchDocument();
        public List<CharacterGeneratedConfigReference> GeneratedReferences { get; set; } = new List<CharacterGeneratedConfigReference>();
        public int RuntimeCharacterId { get; set; }
    }

    public sealed class CharacterGeneratedConfigReference
    {
        public string TableName { get; set; } = string.Empty;
        public int GeneratedId { get; set; }
        public string StableId { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string SourceObjectPath { get; set; } = string.Empty;
    }

    public sealed partial class CharacterAuthoringGeometryBinding
    {
        public string Format { get; set; } = CharacterAuthoringCompilerFormats.GeometryBinding;
        public string SchemaVersion { get; set; } = "1.0";
        public int Features { get; set; }
        public string PackageId { get; set; } = string.Empty;
        public string BodyProfileStableId { get; set; } = string.Empty;
        public CharacterCoordinateConversionPlan CoordinateConversion { get; set; } = new CharacterCoordinateConversionPlan();
        public CharacterBodyGeometryProfile BodyProfile { get; set; } = new CharacterBodyGeometryProfile();
        public List<CharacterBodyPartBinding> BodyParts { get; set; } = new List<CharacterBodyPartBinding>();
        public List<CharacterBodyPartContactPointBinding> BodyPartContactPoints { get; set; } =
            new List<CharacterBodyPartContactPointBinding>();
        public List<CharacterBodyColliderBinding> BodyColliders { get; set; } = new List<CharacterBodyColliderBinding>();
        public List<CharacterHitZoneBinding> HitZoneBindings { get; set; } = new List<CharacterHitZoneBinding>();
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<CharacterDamageZoneBinding> DamageZones { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public int EngagementAnchorRevision { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string EngagementAnchorContentHash { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<CharacterEngagementAnchorBinding> EngagementAnchors { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public CharacterBodyPresenceBindingMode BodyPresenceMode { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public int BodyPresenceRevision { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string BodyPresenceContentHash { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<CharacterBodyPresenceVolumeBinding> BodyPresenceVolumes { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<CharacterBodyPresenceAnimatedHurtboxSourceBinding> AnimatedHurtboxSources { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public CharacterBodyPartDamageBindingMode BodyPartDamageMode { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string BodyPartDamageFormat { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string BodyPartDamageSchemaVersion { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public int BodyPartDamageContentEncodingVersion { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public int BodyPartDamageRevision { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string BodyPartDamageContentHash { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<CharacterBodyPartDamageBinding> BodyPartDamageDefinitions { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<CharacterBodyPartDamageLookupBinding> BodyPartDamagePartLookup { get; set; }
        public List<CharacterSocketBinding> Sockets { get; set; } = new List<CharacterSocketBinding>();
        public List<CharacterWeaponAttachmentBinding> WeaponAttachments { get; set; } = new List<CharacterWeaponAttachmentBinding>();
        public List<CharacterWeaponTraceBinding> WeaponTraces { get; set; } = new List<CharacterWeaponTraceBinding>();

        [System.Text.Json.Serialization.JsonIgnore]
        public float AuthorCapsuleCenterX => BodyProfile?.DefaultCapsule?.Center?.X ?? 0f;
        [System.Text.Json.Serialization.JsonIgnore]
        public float AuthorCapsuleCenterZ => BodyProfile?.DefaultCapsule?.Center?.Z ?? 0f;
        [System.Text.Json.Serialization.JsonIgnore]
        public float RuntimeConsumedCapsuleCenterX => 0f;
        [System.Text.Json.Serialization.JsonIgnore]
        public float RuntimeConsumedCapsuleCenterZ => 0f;
        [System.Text.Json.Serialization.JsonIgnore]
        public bool RuntimeConsumesAuthorCapsuleCenterXZ => false;
    }

    public enum CharacterBodyPresenceBindingMode
    {
        CompatibilityHardRoot = 0,
        Authored = 1
    }

    public enum CharacterBodyPartDamageBindingMode
    {
        Missing = 0,
        Authored = 1
    }

    public sealed class CharacterCoordinateConversionPlan
    {
        public CharacterPackageCoordinateConvention SourceConvention { get; set; } = new CharacterPackageCoordinateConvention();
        public CharacterPackageCoordinateConvention TargetConvention { get; set; } = new CharacterPackageCoordinateConvention();
        public bool RequiresConversion { get; set; }
        public float PositionScale { get; set; } = 1f;
        public string AxisConversion { get; set; } = "identity";
        public string RotationConversion { get; set; } = "identity";
    }

    public sealed class CharacterBodyPartBinding
    {
        public int PartId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public CharacterAuthoringBodyPartKind PartKind { get; set; }
        public int ParentPartId { get; set; }
        public string BonePath { get; set; } = string.Empty;
        public string LocatorId { get; set; } = string.Empty;
        public int DefaultHitZoneId { get; set; }
        public int ReactionGroupId { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public string SourcePath { get; set; } = "geometry/body_parts.json";
        public string SourceObjectPath { get; set; } = string.Empty;
        public string PartLabel { get; set; } = string.Empty;
        public string ParentPartLabel { get; set; } = string.Empty;
        public string DefaultHitZoneLabel { get; set; } = string.Empty;
        public string ReactionGroupLabel { get; set; } = string.Empty;
    }

    public sealed class CharacterBodyPartContactPointBinding
    {
        public string StableId { get; set; } = string.Empty;
        public int ContactPointId { get; set; }
        public int BodyPartId { get; set; }
        public int PartId { get; set; }
        public CharacterAuthoringVector3 LocalOffsetMeters { get; set; } = new CharacterAuthoringVector3();
        public float RadiusMeters { get; set; }
        public string LayerMaskProfileId { get; set; } = string.Empty;
        public string Usage { get; set; } = "PresentationAudio";
        public string Authority { get; set; } = "PresentationOnly";
        public CharacterBodyPartContactActivationMode ActivationMode { get; set; } =
            CharacterBodyPartContactActivationMode.Always;
        public CharacterBodyPartContactMotionMode MotionMode { get; set; } =
            CharacterBodyPartContactMotionMode.Static;
        public int MotionSocketId { get; set; }
        public int SourceOrder { get; set; }
        public string SourcePath { get; set; } = "geometry/body_part_contact_points.json";
        public string SourceObjectPath { get; set; } = string.Empty;
        public string PartLabel { get; set; } = string.Empty;
        public string MotionSocketLabel { get; set; } = string.Empty;
    }

    public sealed class CharacterBodyColliderBinding
    {
        public int ColliderId { get; set; }
        public int PartId { get; set; }
        public int HitZoneId { get; set; }
        public CharacterColliderShape Shape { get; set; }
        public CharacterAuthoringLocalPose LocalPose { get; set; } = new CharacterAuthoringLocalPose();
        public CharacterAuthoringVector3 Size { get; set; } = new CharacterAuthoringVector3();
        public float Radius { get; set; }
        public float Height { get; set; }
        public int Priority { get; set; }
        public bool IsWeakPoint { get; set; }
        public float DamageMultiplierOverride { get; set; }
        public float ImpactPowerScaleOverride { get; set; }
        public string PhysicsLayer { get; set; } = string.Empty;
        public string MaterialStableId { get; set; } = string.Empty;
        public string SourcePath { get; set; } = "geometry/body_colliders.json";
        public string SourceObjectPath { get; set; } = string.Empty;
        public string ColliderLabel { get; set; } = string.Empty;
        public string PartLabel { get; set; } = string.Empty;
        public string HitZoneLabel { get; set; } = string.Empty;
    }

    public sealed class CharacterHitZoneBinding
    {
        public int HitZoneId { get; set; }
        public int PartId { get; set; }
        public int Priority { get; set; }
        public bool IsWeakPoint { get; set; }
        public float DamageMultiplierOverride { get; set; }
        public float ImpactPowerScaleOverride { get; set; }
        public string SourcePath { get; set; } = "geometry/body_colliders.json";
        public string SourceObjectPath { get; set; } = string.Empty;
        public string HitZoneLabel { get; set; } = string.Empty;
        public string PartLabel { get; set; } = string.Empty;
    }

    public sealed class CharacterDamageZoneBinding
    {
        public int ZoneId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public List<int> HitZoneIds { get; set; } = new List<int>();
        public bool Hittable { get; set; }
        public bool Aimable { get; set; }
        public bool Approachable { get; set; }
        public int AuthoredPriority { get; set; }
        public string SourcePath { get; set; } = "geometry/damage_zones.json";
        public string SourceObjectPath { get; set; } = string.Empty;
        public string ZoneLabel { get; set; } = string.Empty;
        public List<string> HitZoneLabels { get; set; } = new List<string>();
    }

    public sealed class CharacterEngagementAnchorBinding
    {
        public int AnchorId { get; set; }
        public int ZoneId { get; set; }
        public CharacterEngagementAnchorReferenceFrame ReferenceFrame { get; set; } =
            CharacterEngagementAnchorReferenceFrame.ModelRoot;
        public CharacterAuthoringIntVector3 LocalPositionMillimeters { get; set; } =
            new CharacterAuthoringIntVector3();
        public int YawMilliDegrees { get; set; }
        public int Capacity { get; set; } = 1;
        public int AuthoredPriority { get; set; }
        public bool Enabled { get; set; } = true;
        public int PhaseId { get; set; }
        public string SourcePath { get; set; } = "geometry/engagement_anchors.json";
        public string SourceObjectPath { get; set; } = string.Empty;
        public string AnchorLabel { get; set; } = string.Empty;
        public string ZoneLabel { get; set; } = string.Empty;
        public string PhaseLabel { get; set; } = string.Empty;
    }

    public sealed class CharacterBodyPresenceVolumeBinding
    {
        public string VolumeId { get; set; } = string.Empty;
        public CharacterBodyPresenceReferenceFrame ReferenceFrame { get; set; } =
            CharacterBodyPresenceReferenceFrame.ModelRoot;
        public CharacterBodyPresenceShape Shape { get; set; }
        public CharacterBodyPresenceLocalPose LocalPose { get; set; } =
            new CharacterBodyPresenceLocalPose();
        public float Radius { get; set; }
        public float Height { get; set; }
        public CharacterAuthoringVector3 Size { get; set; } = new CharacterAuthoringVector3();
        public int SkinMillimeters { get; set; }
        public int PressureScaleMilli { get; set; }
        public bool Enabled { get; set; }
        public string SourcePath { get; set; } = "geometry/body_presence.json";
        public string SourceObjectPath { get; set; } = string.Empty;
    }

    public sealed class CharacterBodyPresenceAnimatedHurtboxSourceBinding
    {
        public string SourceId { get; set; } = string.Empty;
        public int ColliderId { get; set; }
        public int SkinMillimeters { get; set; }
        public int PressureScaleMilli { get; set; }
        public bool Enabled { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string SourceObjectPath { get; set; }
        public string ColliderLabel { get; set; } = string.Empty;
    }

    public sealed class CharacterBodyPartDamageBinding
    {
        public int DefinitionIndex { get; set; }
        public int PartStateId { get; set; }
        public List<int> MemberPartIds { get; set; } = new List<int>();
        public int DamageZoneId { get; set; }
        public int WoundCapacity { get; set; }
        public int OpenDamageScaleMilli { get; set; }
        public string FocusConsumeTag { get; set; } = string.Empty;
        public int MaxIntegrity { get; set; }
        public int IntegrityDamageScaleMilli { get; set; }
        public string BreakReactionId { get; set; } = string.Empty;
        public int AuthoredPriorityDeltaOpen { get; set; }
        public int AuthoredPriorityDeltaBroken { get; set; }
        public bool Severable { get; set; }
        public string SeverRequiredTag { get; set; } = string.Empty;
        public string PresentationEventId { get; set; } = string.Empty;
        public string SourcePath { get; set; } = "geometry/body_part_damage.json";
        public string SourceObjectPath { get; set; } = string.Empty;
        public string PartStateLabel { get; set; } = string.Empty;
        public List<string> MemberPartLabels { get; set; } = new List<string>();
        public string DamageZoneLabel { get; set; } = string.Empty;
    }

    public sealed class CharacterBodyPartDamageLookupBinding
    {
        public int PartId { get; set; }
        public int DefinitionIndex { get; set; }
        public string SourcePath { get; set; } = "geometry/body_part_damage.json";
        public string SourceObjectPath { get; set; } = string.Empty;
        public string PartLabel { get; set; } = string.Empty;
    }

    public sealed class CharacterSocketBinding
    {
        public int SocketId { get; set; }
        public string AbstractBoneId { get; set; } = string.Empty;
        public int ParentPartId { get; set; }
        public string BonePath { get; set; } = string.Empty;
        public string LocatorPath { get; set; } = string.Empty;
        public CharacterAuthoringLocalPose LocalPose { get; set; } = new CharacterAuthoringLocalPose();
        public CharacterSocketUsage Usage { get; set; }
        public int MirrorPairSocketId { get; set; }
        public CharacterSocketHandedness Handedness { get; set; } = CharacterSocketHandedness.None;
        public CharacterSocketSideTag SideTag { get; set; } = CharacterSocketSideTag.Center;
        public List<string> Tags { get; set; } = new List<string>();
        public string SourcePath { get; set; } = "geometry/sockets.json";
        public string SourceObjectPath { get; set; } = string.Empty;
        public string SocketLabel { get; set; } = string.Empty;
        public string ParentPartLabel { get; set; } = string.Empty;
        public string MirrorPairSocketLabel { get; set; } = string.Empty;
    }

    public sealed class CharacterWeaponAttachmentBinding
    {
        public string AttachmentId { get; set; } = string.Empty;
        public int WeaponId { get; set; }
        public int EquipSlot { get; set; }
        public int AttachSocketId { get; set; }
        public CharacterAuthoringLocalPose LocalGripPose { get; set; } = new CharacterAuthoringLocalPose();
        public CharacterAuthoringLocalPose SlotPose { get; set; } = new CharacterAuthoringLocalPose();
        public CharacterAuthoringLocalPose WeaponModelPose { get; set; } = new CharacterAuthoringLocalPose();
        public AuthoringResourceSelectionRef WeaponRef { get; set; } = new AuthoringResourceSelectionRef();
        public string PreviewResourceKey { get; set; } = string.Empty;
        public int TraceId { get; set; }
        public int TraceStartSocketId { get; set; }
        public int TraceEndSocketId { get; set; }
        public float TraceRadius { get; set; }
        public WeaponTraceSampleRule TraceSampleRule { get; set; }
        public string SourcePath { get; set; } = "config/character_application.json";
        public string SourceObjectPath { get; set; } = string.Empty;
        public string WeaponLabel { get; set; } = string.Empty;
        public string EquipSlotLabel { get; set; } = string.Empty;
        public string AttachSocketLabel { get; set; } = string.Empty;
        public string TraceLabel { get; set; } = string.Empty;
        public string TraceStartSocketLabel { get; set; } = string.Empty;
        public string TraceEndSocketLabel { get; set; } = string.Empty;
    }

    public sealed class CharacterWeaponTraceBinding
    {
        public int TraceId { get; set; }
        public int WeaponId { get; set; }
        public int EquipSlot { get; set; }
        public string StartLocatorPath { get; set; } = string.Empty;
        public string EndLocatorPath { get; set; } = string.Empty;
        public CharacterAuthoringLocalPose StartPose { get; set; } = new CharacterAuthoringLocalPose();
        public CharacterAuthoringLocalPose EndPose { get; set; } = new CharacterAuthoringLocalPose();
        public float Radius { get; set; }
        public float ProximalExtension { get; set; }
        public float ProximalRadius { get; set; }
        public WeaponTraceSampleRule SampleRule { get; set; }
        public int FixedSampleCount { get; set; }
        public List<string> ActionKeys { get; set; } = new List<string>();
        public int SocketId { get; set; }
        public string SourcePath { get; set; } = "WeaponSource/global_weapon_library.json";
        public string SourceObjectPath { get; set; } = string.Empty;
        public string TraceLabel { get; set; } = string.Empty;
        public string WeaponLabel { get; set; } = string.Empty;
        public string EquipSlotLabel { get; set; } = string.Empty;
        public string SocketLabel { get; set; } = string.Empty;
    }

    public sealed class CharacterPackageResourceMapping
    {
        public string Format { get; set; } = CharacterAuthoringCompilerFormats.ResourceMapping;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public List<CharacterPackageResourceMappingEntry> Entries { get; set; } = new List<CharacterPackageResourceMappingEntry>();
    }

    public sealed class CharacterPackageResourceMappingEntry
    {
        public string PackageResourceKey { get; set; } = string.Empty;
        public string ProjectResourceKey { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string TypeId { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
        public string SourceFormat { get; set; } = string.Empty;
        public string SourceRelativePath { get; set; } = string.Empty;
        public string DeclaredContentHash { get; set; } = string.Empty;
        public string ImportHash { get; set; } = string.Empty;
        public string DependencyHash { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string TargetPathPolicy { get; set; } = string.Empty;
        public string ImportTargetPath { get; set; } = string.Empty;
        public CharacterAuthoringLocalPose ModelWrapperPose { get; set; } = new CharacterAuthoringLocalPose();
        public string ConflictAction { get; set; } = string.Empty;
    }

    public sealed class CharacterUnityImportWritePlan
    {
        public string Format { get; set; } = CharacterAuthoringCompilerFormats.UnityWritePlan;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string TargetRootPath { get; set; } = string.Empty;
        public string TargetPathPolicy { get; set; } = string.Empty;
        public bool CanWriteToUnityProject { get; set; }
        public bool CanSpawnAfterImport { get; set; }
        public List<CharacterUnityImportWriteEntry> Writes { get; set; } = new List<CharacterUnityImportWriteEntry>();
    }

    public sealed class CharacterUnityImportWriteEntry
    {
        public string Kind { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string WritePolicy { get; set; } = "Recreate";
        public string ContentHash { get; set; } = string.Empty;
    }

    public sealed class CharacterResolverVerificationPlan
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string ExpectedResolverEntrypoint { get; set; } = "CharacterPackageResolver.Resolve";
        public string Status { get; set; } = "NotReady";
        public string CharacterStableId { get; set; } = string.Empty;
        public string DefaultLoadoutStableId { get; set; } = string.Empty;
        public string ExpectedActiveEquipmentStateStableId { get; set; } = string.Empty;
        public string ExpectedCombatActionSetStableId { get; set; } = string.Empty;
        public string ExpectedAnimationProfileId { get; set; } = string.Empty;
        public List<CharacterResolverTableRequirement> RequiredTables { get; set; } = new List<CharacterResolverTableRequirement>();
        public List<int> KnownAbilityIds { get; set; } = new List<int>();
        public List<int> GameplayRuntimeAbilityIds { get; set; } = new List<int>();
        public List<string> RequiredResourceKeys { get; set; } = new List<string>();
        public List<CharacterAuthoringValidationIssue> Diagnostics { get; set; } = new List<CharacterAuthoringValidationIssue>();
    }

    public sealed class CharacterResolverTableRequirement
    {
        public string TableName { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public List<int> GeneratedIds { get; set; } = new List<int>();
        public List<string> StableIds { get; set; } = new List<string>();
    }

    public sealed class CharacterPackageSourceMapping
    {
        public string SourcePath { get; set; } = string.Empty;
        public string SourceObjectPath { get; set; } = string.Empty;
        public string TargetKind { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string TargetField { get; set; } = string.Empty;
    }

    public static partial class CharacterAuthoringCompiler
    {
        public static CharacterAuthoringCompileResult Compile(CharacterAuthoringCompileRequest request)
        {
            if (request == null)
                request = new CharacterAuthoringCompileRequest();

            CharacterAuthoringCompileOptions options = request.Options ?? new CharacterAuthoringCompileOptions();
            CharacterResourcePackage package = request.Package;
            var result = new CharacterAuthoringCompileResult();
            result.PackageId = package != null && package.Manifest != null ? package.Manifest.PackageId : string.Empty;
            result.PackageStableId = package != null && package.Manifest != null ? package.Manifest.StableId : string.Empty;
            if (package?.TacticalDefaults != null)
                result.TacticalDefaults = JsonSerializer.Deserialize<MxFramework.CharacterApplication.CharacterTacticalDefaults>(
                    JsonSerializer.Serialize(package.TacticalDefaults));

            CharacterAuthoringValidationReport validation = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
            {
                PackageRootPath = request.PackageRootPath,
                ValidateResourceFiles = options.ValidateResourceFiles || options.ValidateResourceHashes,
                ValidateResourceHashes = options.ValidateResourceHashes
            });

            var issues = new List<CharacterAuthoringValidationIssue>();
            if (validation != null && validation.Issues != null)
                issues.AddRange(validation.Issues);

            AddCoordinateDiagnostics(package, options, issues);
            AddResourceConflictDiagnostics(package, request.ExistingProjectResourceCatalogSummary, issues);
            AddPaletteUsageDiagnostics(package, request.PackageRootPath, issues);
            AddStrictDiagnostics(options, issues);

            CharacterContentIdentityCatalogSnapshot catalog = request.IdentityCatalog;
            if (catalog == null)
            {
                var searchDirs = new List<string>();
                if (!string.IsNullOrWhiteSpace(request.PackageRootPath)) searchDirs.Add(request.PackageRootPath);
                searchDirs.Add(System.IO.Directory.GetCurrentDirectory());
                searchDirs.Add(System.AppDomain.CurrentDomain.BaseDirectory);

                for (int i = 0; i < searchDirs.Count; i++)
                {
                    string start = searchDirs[i];
                    if (string.IsNullOrWhiteSpace(start)) continue;
                    string dir = System.IO.Path.GetFullPath(start);
                    while (!string.IsNullOrEmpty(dir))
                    {
                        string candidate = System.IO.Path.Combine(dir, "Tools", "RuntimeHash", "character_definition_ids.json");
                        if (System.IO.File.Exists(candidate))
                        {
                            try
                            {
                                catalog = CharacterContentIdentityCatalogSnapshot.LoadFromFile(candidate);
                                if (catalog != null) break;
                            }
                            catch { }
                        }
                        string parent = System.IO.Path.GetDirectoryName(dir);
                        if (string.Equals(parent, dir, StringComparison.Ordinal)) break;
                        dir = parent;
                    }
                    if (catalog != null) break;
                }
            }
            if (catalog == null && CharacterContentIdentityCatalogSnapshot.DefaultAllowAutoRegisterForTests)
            {
                catalog = new CharacterContentIdentityCatalogSnapshot { AllowAutoRegisterForTests = true };
            }
            if (catalog == null)
            {
                issues.Add(new CharacterAuthoringValidationIssue
                {
                    Severity = CharacterAuthoringValidationSeverity.Error,
                    Gate = CharacterAuthoringValidationGate.ExportBlocked,
                    Code = CharacterContentIdentityCodes.CatalogInvalid,
                    SourcePath = "character_definition_ids.json",
                    SourceObjectPath = "catalog",
                    Field = "catalog",
                    Message = "Digital identity catalog snapshot is missing or could not be loaded.",
                    SuggestedFix = "Provide a valid CharacterContentIdentityCatalogSnapshot or ensure Tools/RuntimeHash/character_definition_ids.json exists."
                });
            }
            var identityResolver = new CharacterContentIdentityResolver(catalog);

            result.DependencyGraph = CharacterPackageResourcePipeline.BuildDependencyGraph(package != null ? package.ResourceCatalog : null);
            result.ResourceHashReport = options.ValidateResourceFiles || options.ValidateResourceHashes
                ? CharacterPackageResourcePipeline.BuildHashReport(package, request.PackageRootPath)
                : new CharacterPackageResourceHashReport { PackageId = result.PackageId };
            result.GeometryBinding = BuildGeometryBinding(package, options, validation, identityResolver, issues);
            result.ResourceMapping = BuildResourceMapping(package, options, issues);
            CharacterResourceMappingV2Materialization v2Materialization = CharacterResourceMappingV2Producer.Materialize(package, result.ResourceMapping);
            result.ResourceMappingV2Snapshot = v2Materialization.Snapshot;
            result.ResourceMappingV2Diagnostics = v2Materialization.Diagnostics;
            foreach (ResourceMappingDiagnostic diagnostic in v2Materialization.Diagnostics)
            {
                if (diagnostic.Code != CharacterResourceMappingV2ProducerContracts.MaterialTargetCollision)
                    continue;
                issues.Add(new CharacterAuthoringValidationIssue
                {
                    Severity = CharacterAuthoringValidationSeverity.Error,
                    Gate = CharacterAuthoringValidationGate.ImportBlocked,
                    Code = diagnostic.Code,
                    SourcePath = "resource_catalog.json",
                    SourceObjectPath = diagnostic.Path,
                    Field = "importHints.targetRelativePath",
                    Message = diagnostic.Message,
                    SuggestedFix = "Use distinct material descriptor file names and import targets after filename sanitization and case-insensitive comparison."
                });
            }
            result.GeneratedConfigPatch = BuildConfigPatch(package, result.GeometryBinding, result.ResourceMapping, identityResolver, issues);
            AddGeneratedConfigDiagnostics(result.GeneratedConfigPatch, issues);
            AnimationAuthoringPackage effectiveAnimationPackage =
                GlobalWeaponConfigurationResolver.BuildEffectiveAnimationPackage(package);
            result.AnimationCompileResult = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest
            {
                Package = effectiveAnimationPackage,
                PackageRootPath = request.PackageRootPath,
                ResourceCatalog = package != null ? package.ResourceCatalog : null,
                IdentityCatalog = catalog
            });
            AddAnimationCompilerDiagnostics(result.AnimationCompileResult, issues);
            AddEffectiveAnimationProfileDiagnostics(result.GeneratedConfigPatch, result.AnimationCompileResult, issues);
            result.GateReport = BuildGateReport(result.PackageId, issues);
            result.Status = result.GateReport.Status;
            result.UnityImportWritePlan = BuildWritePlan(package, options, result.GateReport, result.ResourceMapping, result.GeneratedConfigPatch);
            result.ResolverVerificationPlan = BuildResolverVerificationPlan(package.ApplicationConfig, package.GlobalWeaponLibrary, result.GeneratedConfigPatch, result.GateReport, result.ResourceMapping);
            result.SourceMappings = BuildSourceMappings(result.GeneratedConfigPatch, result.GeometryBinding, result.ResourceMapping);
            result.Hashes = BuildHashes(package, result.GeneratedConfigPatch, result.GeometryBinding, result.ResourceMapping, result.UnityImportWritePlan, catalog);
            result.UnityImportWritePlan.Writes.Insert(0, new CharacterUnityImportWriteEntry
            {
                Kind = CharacterAuthoringCompilerWriteKinds.GeneratedConfigPatch,
                Owner = CharacterAuthoringCompilerOwnerKinds.AuthoringCompiler,
                SourcePath = "compiler/generated_config_patch.json",
                TargetPath = CombineProjectPath(result.UnityImportWritePlan.TargetRootPath, "config/character_config_patch.json"),
                WritePolicy = "Recreate",
                ContentHash = result.Hashes.GeneratedConfigHash
            });
            result.UnityImportWritePlan.Writes.Insert(1, new CharacterUnityImportWriteEntry
            {
                Kind = CharacterAuthoringCompilerWriteKinds.GeometryBinding,
                Owner = CharacterAuthoringCompilerOwnerKinds.AuthoringCompiler,
                SourcePath = "compiler/geometry_binding.json",
                TargetPath = CombineProjectPath(result.UnityImportWritePlan.TargetRootPath, "config/geometry_binding.json"),
                WritePolicy = "Recreate",
                ContentHash = result.Hashes.GeometryBindingHash
            });
            result.UnityImportWritePlan.Writes.Insert(2, new CharacterUnityImportWriteEntry
            {
                Kind = CharacterAuthoringCompilerWriteKinds.ResourceMapping,
                Owner = CharacterAuthoringCompilerOwnerKinds.AuthoringCompiler,
                SourcePath = "compiler/resource_mapping.json",
                TargetPath = CombineProjectPath(result.UnityImportWritePlan.TargetRootPath, "config/resource_catalog_mapping.json"),
                WritePolicy = "Recreate",
                ContentHash = result.Hashes.ResourceMappingHash
            });

            // 三项正式配置目标必须在最终计划形成之后再计算 WritePlanHash，
            // 否则持久化的 WritePlanHash 不覆盖这三项，改变正式目标路径也不会改变该哈希。
            result.Hashes.WritePlanHash = CharacterPackageHashUtility.ComputeTextSha256(
                BuildWritePlanCanonicalText(result.UnityImportWritePlan));

            return result;
        }

        private static void AddAnimationCompilerDiagnostics(
            AnimationAuthoringCompileResult animationCompileResult,
            List<CharacterAuthoringValidationIssue> issues)
        {
            List<CharacterAuthoringValidationIssue> animationIssues =
                animationCompileResult?.AnimationValidationReport?.Issues;
            if (animationIssues != null)
                issues.AddRange(animationIssues);
        }

        private static void AddEffectiveAnimationProfileDiagnostics(
            CharacterAuthoringCompiledConfigPatch configPatch,
            AnimationAuthoringCompileResult animationCompileResult,
            List<CharacterAuthoringValidationIssue> issues)
        {
            List<AnimationSetDefinitionProfile> profiles =
                animationCompileResult?.AnimationSetDefinition?.Profiles;
            List<PatchEntry> entries = configPatch?.Patch?.Entries;
            var validatedProfileIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                PatchEntry entry = entries[i];
                if (entry == null ||
                    !string.Equals(entry.Source, CharacterApplicationCompilerTableNames.EquipmentStateConfig, StringComparison.Ordinal))
                    continue;

                string profileId = entry.Fields.GetScalar("AnimationProfileId");
                if (!validatedProfileIds.Add(profileId ?? string.Empty))
                    continue;

                string sourceObjectPath = "EquipmentStateConfig/" +
                    FirstNonEmpty(entry.Fields.GetScalar("StableId"), entry.Id);
                if (string.IsNullOrWhiteSpace(profileId))
                {
                    AddEffectiveAnimationIssue(
                        issues,
                        CharacterAuthoringCompilerValidationCodes.EffectiveAnimationProfileMissing,
                        sourceObjectPath,
                        "The effective equipment state does not resolve an AnimationProfileId.",
                        "Select an animation configuration whose exact profileId exists and owns the required locomotion slot.");
                    continue;
                }

                AnimationSetDefinitionProfile profile = null;
                int profileCount = 0;
                for (int profileIndex = 0; profiles != null && profileIndex < profiles.Count; profileIndex++)
                {
                    if (string.Equals(profiles[profileIndex]?.ProfileId, profileId, StringComparison.Ordinal))
                    {
                        profile = profiles[profileIndex];
                        profileCount++;
                    }
                }

                if (profileCount != 1)
                {
                    AddEffectiveAnimationIssue(
                        issues,
                        profileCount == 0
                            ? CharacterAuthoringCompilerValidationCodes.EffectiveAnimationProfileMissing
                            : CharacterAuthoringCompilerValidationCodes.EffectiveAnimationProfileAmbiguous,
                        sourceObjectPath,
                        profileCount == 0
                            ? "The effective AnimationProfileId does not exist in compiled animation profiles: " + profileId
                            : "The effective AnimationProfileId is not unique in compiled animation profiles: " + profileId,
                        "Create exactly one case-sensitive profileId=\"" + profileId + "\" in AnimationEditor.");
                    continue;
                }

                int locomotionCount = 0;
                for (int slotIndex = 0; profile.Slots != null && slotIndex < profile.Slots.Count; slotIndex++)
                {
                    if (string.Equals(profile.Slots[slotIndex]?.SlotId, "locomotion", StringComparison.Ordinal))
                        locomotionCount++;
                }

                if (locomotionCount != 1)
                {
                    AddEffectiveAnimationIssue(
                        issues,
                        locomotionCount == 0
                            ? CharacterAuthoringCompilerValidationCodes.EffectiveLocomotionSlotMissing
                            : CharacterAuthoringCompilerValidationCodes.EffectiveLocomotionSlotAmbiguous,
                        sourceObjectPath,
                        locomotionCount == 0
                            ? "The effective animation profile has no case-sensitive slotId=\"locomotion\": " + profileId
                            : "The effective animation profile has more than one slotId=\"locomotion\": " + profileId,
                        "Keep exactly one required locomotion slot with complete setId, groupId and defaultBlendId references.");
                }
            }
        }

        private static void AddEffectiveAnimationIssue(
            List<CharacterAuthoringValidationIssue> issues,
            string code,
            string sourceObjectPath,
            string message,
            string suggestedFix)
        {
            issues.Add(new CharacterAuthoringValidationIssue
            {
                Severity = CharacterAuthoringValidationSeverity.Error,
                Gate = CharacterAuthoringValidationGate.ExportBlocked,
                Code = code ?? string.Empty,
                SourcePath = "compiler/generated_config_patch.json",
                SourceObjectPath = sourceObjectPath ?? string.Empty,
                Field = "AnimationProfileId",
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            });
        }

        private static CharacterCompilerGateReport BuildGateReport(string packageId, List<CharacterAuthoringValidationIssue> issues)
        {
            var report = new CharacterCompilerGateReport { PackageId = packageId ?? string.Empty };
            if (issues != null)
                report.Issues.AddRange(issues);

            for (int i = 0; i < report.Issues.Count; i++)
            {
                CharacterAuthoringValidationIssue issue = report.Issues[i];
                if (issue == null)
                    continue;

                if (issue.Gate == CharacterAuthoringValidationGate.ExportBlocked)
                    report.ExportBlocked = true;
                else if (issue.Gate == CharacterAuthoringValidationGate.ImportBlocked)
                    report.ImportBlocked = true;
                else if (issue.Gate == CharacterAuthoringValidationGate.SpawnBlocked)
                    report.SpawnBlocked = true;
                else if (issue.Gate == CharacterAuthoringValidationGate.WarningOnly || issue.Severity == CharacterAuthoringValidationSeverity.Warning)
                    report.WarningOnly = true;
            }

            if (report.ExportBlocked)
                report.Status = CharacterAuthoringCompilerStatus.ExportBlocked;
            else if (report.ImportBlocked)
                report.Status = CharacterAuthoringCompilerStatus.ImportBlocked;
            else if (report.SpawnBlocked)
                report.Status = CharacterAuthoringCompilerStatus.SpawnBlocked;
            else if (report.WarningOnly)
                report.Status = CharacterAuthoringCompilerStatus.WarningOnly;
            else
                report.Status = CharacterAuthoringCompilerStatus.Ready;

            return report;
        }

        private static void AddGeneratedConfigDiagnostics(
            CharacterAuthoringCompiledConfigPatch compiled,
            List<CharacterAuthoringValidationIssue> issues)
        {
            if (compiled?.Patch?.Entries == null || issues == null)
                return;

            var declaredSlots = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < compiled.Patch.Entries.Count; i++)
            {
                PatchEntry entry = compiled.Patch.Entries[i];
                if (entry == null || entry.Source != CharacterApplicationCompilerTableNames.EquipmentSchemaConfig)
                    continue;
                if (!entry.Fields.TryGetValue("Slots", out FieldValue slots) || slots?.List == null)
                    continue;
                for (int slotIndex = 0; slotIndex < slots.List.Count; slotIndex++)
                {
                    FieldValue slot = slots.List[slotIndex];
                    if (slot?.Map != null && slot.Map.TryGetValue("SlotId", out FieldValue slotId) &&
                        !string.IsNullOrWhiteSpace(slotId?.Scalar))
                        declaredSlots.Add(slotId.Scalar);
                }
            }

            var reported = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < compiled.Patch.Entries.Count; i++)
            {
                PatchEntry entry = compiled.Patch.Entries[i];
                if (entry == null || entry.Source != CharacterApplicationCompilerTableNames.EquipmentLoadoutConfig)
                    continue;
                if (!entry.Fields.TryGetValue("Slots", out FieldValue slots) || slots?.List == null)
                    continue;
                for (int slotIndex = 0; slotIndex < slots.List.Count; slotIndex++)
                {
                    FieldValue slot = slots.List[slotIndex];
                    if (slot?.Map == null || !slot.Map.TryGetValue("SlotId", out FieldValue slotId) ||
                        string.IsNullOrWhiteSpace(slotId?.Scalar) || declaredSlots.Contains(slotId.Scalar) ||
                        !reported.Add(slotId.Scalar))
                        continue;
                    issues.Add(new CharacterAuthoringValidationIssue
                    {
                        Severity = CharacterAuthoringValidationSeverity.Error,
                        Gate = CharacterAuthoringValidationGate.ExportBlocked,
                        Code = CharacterAuthoringValidationCodes.GeneratedEquipmentSlotMissing,
                        SourcePath = "compiler/generated_config_patch.json",
                        SourceObjectPath = "EquipmentLoadoutConfig/" + entry.Id,
                        Field = "Slots/" + slotIndex.ToString(CultureInfo.InvariantCulture) + "/SlotId",
                        Message = "Generated loadout slot '" + slotId.Scalar + "' is not declared by the generated equipment schema.",
                        SuggestedFix = "Ensure geometry/weapon_slots.json declares the slot and regenerate the Character Application patch."
                    });
                }
            }
        }

        private static CharacterAuthoringGeometryBinding BuildGeometryBinding(
            CharacterResourcePackage package,
            CharacterAuthoringCompileOptions options,
            CharacterAuthoringValidationReport validation,
            CharacterContentIdentityResolver identityResolver,
            List<CharacterAuthoringValidationIssue> issues)
        {
            var binding = new CharacterAuthoringGeometryBinding();
            if (package == null)
                return binding;

            binding.PackageId = package.Manifest != null ? package.Manifest.PackageId : string.Empty;
            binding.BodyProfileStableId = GetBodyProfileStableId(package);
            binding.CoordinateConversion = BuildCoordinatePlan(package.Manifest != null ? package.Manifest.CoordinateConvention : null, options.TargetCoordinateConvention);
            CharacterAuthoringGeometry geometry = package.Geometry ?? new CharacterAuthoringGeometry();
            bool hasCurrentDamageZones =
                geometry.DamageZonesDocumentState == CharacterDamageZonesDocumentState.Current
                && geometry.DamageZones != null;
            bool hasCurrentEngagementAnchors =
                geometry.EngagementAnchorsDocumentState == CharacterEngagementAnchorsDocumentState.Current
                && geometry.EngagementAnchors != null;
            bool hasCurrentBodyPresence =
                geometry.BodyPresenceDocumentState == CharacterBodyPresenceDocumentState.Current
                && geometry.BodyPresence != null
                && !HasBlockingBodyPresenceIssue(validation);
            bool hasCurrentBodyPresenceV2 = hasCurrentBodyPresence && geometry.BodyPresence.IsV2;
            bool hasCurrentBodyPartDamage =
                geometry.BodyPartDamageDocumentState == CharacterBodyPartDamageDocumentState.Current
                && geometry.BodyPartDamage != null
                && !HasBlockingBodyPartDamageIssue(validation);
            bool hasCurrentAuthorityPoseRig =
                geometry.AuthorityPoseRigDocumentState == CharacterAuthorityPoseRigDocumentState.Current
                && geometry.AuthorityPoseRig != null
                && !HasBlockingAuthorityPoseRigIssue(validation);

            if (hasCurrentBodyPresenceV2)
            {
                binding.Format = CharacterAuthoringCompilerFormats.GeometryBindingV7;
                binding.SchemaVersion = "7.0";
            }
            else if (hasCurrentAuthorityPoseRig)
            {
                binding.Format = CharacterAuthoringCompilerFormats.GeometryBindingV6;
                binding.SchemaVersion = "6.0";
            }
            else if (hasCurrentBodyPartDamage)
            {
                binding.Format = CharacterAuthoringCompilerFormats.GeometryBindingV5;
                binding.SchemaVersion = "5.0";
            }
            else if (hasCurrentBodyPresence)
            {
                binding.Format = CharacterAuthoringCompilerFormats.GeometryBindingV4;
                binding.SchemaVersion = "4.0";
            }
            else if (hasCurrentEngagementAnchors)
            {
                binding.Format = CharacterAuthoringCompilerFormats.GeometryBindingV3;
                binding.SchemaVersion = "3.0";
            }
            else if (hasCurrentDamageZones)
            {
                binding.Format = CharacterAuthoringCompilerFormats.GeometryBindingV2;
                binding.SchemaVersion = "2.0";
            }

            // Runtime-facing geometry always carries frozen numeric identities in v8.
            // Feature flags below still describe which optional authored sections exist.
            binding.Format = CharacterAuthoringCompilerFormats.GeometryBindingV8;
            binding.SchemaVersion = "8.0";

            binding.Features = (hasCurrentEngagementAnchors ? 2 : 0)
                | (hasCurrentBodyPresence ? 4 : 0)
                | (hasCurrentBodyPresenceV2 ? 8 : 0)
                | (hasCurrentBodyPartDamage ? 16 : 0)
                | (hasCurrentAuthorityPoseRig ? 32 : 0);

            if (hasCurrentDamageZones)
                binding.DamageZones = new List<CharacterDamageZoneBinding>();
            if (hasCurrentEngagementAnchors)
            {
                binding.DamageZones ??= new List<CharacterDamageZoneBinding>();
                binding.EngagementAnchors = new List<CharacterEngagementAnchorBinding>();
                binding.EngagementAnchorRevision = geometry.EngagementAnchors.Revision;
            }
            if (hasCurrentBodyPresence)
            {
                binding.BodyPresenceMode = CharacterBodyPresenceBindingMode.Authored;
                binding.BodyPresenceRevision = geometry.BodyPresence.Revision;
                binding.BodyPresenceContentHash =
                    CharacterBodyPresenceDocumentCodec.ComputeContentHash(geometry.BodyPresence);
                binding.BodyPresenceVolumes = new List<CharacterBodyPresenceVolumeBinding>();
                if (hasCurrentBodyPresenceV2)
                    binding.AnimatedHurtboxSources = new List<CharacterBodyPresenceAnimatedHurtboxSourceBinding>();
            }
            if (hasCurrentBodyPartDamage)
            {
                binding.BodyPartDamageMode = CharacterBodyPartDamageBindingMode.Authored;
                binding.BodyPartDamageFormat = "mx.characterBodyPartDamage.v3";
                binding.BodyPartDamageSchemaVersion = "3.0";
                // The numeric runtime envelope retains the canonical authoring content witness.
                binding.BodyPartDamageContentEncodingVersion = 1;
                binding.BodyPartDamageRevision = geometry.BodyPartDamage.Revision;
                binding.BodyPartDamageContentHash =
                    CharacterBodyPartDamageDocumentCodec.ComputeContentHash(geometry.BodyPartDamage);
                binding.BodyPartDamageDefinitions = new List<CharacterBodyPartDamageBinding>();
                binding.BodyPartDamagePartLookup = new List<CharacterBodyPartDamageLookupBinding>();
            }
            if (hasCurrentAuthorityPoseRig)
                ApplyAuthorityPoseRigBinding(binding, geometry.AuthorityPoseRig, identityResolver, issues);
            var weaponDefinitions = new Dictionary<string, SingleWeaponConfiguration>(StringComparer.Ordinal);
            List<SingleWeaponConfiguration> sourceWeaponDefinitions = package.GlobalWeaponLibrary?.SingleWeaponConfigurations;
            for (int i = 0; sourceWeaponDefinitions != null && i < sourceWeaponDefinitions.Count; i++)
            {
                SingleWeaponConfiguration definition = sourceWeaponDefinitions[i];
                if (definition != null && !string.IsNullOrWhiteSpace(definition.SingleWeaponConfigurationId) &&
                    !weaponDefinitions.ContainsKey(definition.SingleWeaponConfigurationId))
                    weaponDefinitions.Add(definition.SingleWeaponConfigurationId, definition);
            }

            List<SingleWeaponConfiguration> globalDefinitions = GetSortedReferencedSingleWeapons(package);
            var weaponIdsByConfigId = new Dictionary<string, int>(StringComparer.Ordinal);
            var ids = new GeneratedIds();
            for (int i = 0; i < globalDefinitions.Count; i++)
            {
                if (globalDefinitions[i] != null && !string.IsNullOrWhiteSpace(globalDefinitions[i].SingleWeaponConfigurationId))
                {
                    weaponIdsByConfigId[globalDefinitions[i].SingleWeaponConfigurationId] = ids.WeaponStartId + i;
                }
            }

            binding.BodyProfile = geometry.BodyProfile ?? new CharacterBodyGeometryProfile();
            if (hasCurrentBodyPresence)
            {
                CharacterBodyPresenceDocument canonical =
                    CharacterBodyPresenceDocumentCodec.CreateCanonicalDocument(geometry.BodyPresence);
                for (int i = 0; i < canonical.Volumes.Count; i++)
                {
                    CharacterBodyPresenceVolumeAuthoring volume = canonical.Volumes[i];
                    if (volume == null)
                        continue;
                    CharacterBodyPresenceLocalPose pose =
                        volume.LocalPose ?? new CharacterBodyPresenceLocalPose();
                    binding.BodyPresenceVolumes.Add(new CharacterBodyPresenceVolumeBinding
                    {
                        VolumeId = volume.VolumeId ?? string.Empty,
                        ReferenceFrame = volume.ReferenceFrame,
                        Shape = volume.Shape,
                        LocalPose = new CharacterBodyPresenceLocalPose
                        {
                            Position = CloneVector(pose.Position),
                            Rotation = CloneQuaternion(pose.Rotation),
                            Scale = CloneScale(pose.Scale)
                        },
                        Radius = volume.Radius,
                        Height = volume.Height,
                        Size = CloneVector(volume.Size),
                        SkinMillimeters = volume.SkinMillimeters,
                        PressureScaleMilli = volume.PressureScaleMilli,
                        Enabled = volume.Enabled,
                        SourceObjectPath = "bodyPresence/volumes/" + volume.VolumeId
                    });
                }
                for (int i = 0; i < (canonical.AnimatedHurtboxSources?.Count ?? 0); i++)
                {
                    CharacterBodyPresenceAnimatedHurtboxSourceAuthoring source = canonical.AnimatedHurtboxSources[i];
                    int colliderId = identityResolver.ResolveId(
                        CharacterContentIdentityDomains.Pose,
                        source.ColliderId,
                        "geometry/body_presence.json",
                        "bodyPresence/animatedHurtboxSources/" + source.SourceId,
                        "colliderId",
                        issues,
                        required: true);
                    binding.AnimatedHurtboxSources.Add(new CharacterBodyPresenceAnimatedHurtboxSourceBinding
                    {
                        SourceId = source.SourceId,
                        ColliderId = colliderId,
                        ColliderLabel = source.ColliderId ?? string.Empty,
                        SkinMillimeters = source.SkinMillimeters,
                        PressureScaleMilli = source.PressureScaleMilli,
                        Enabled = source.Enabled,
                        SourceObjectPath = "bodyPresence/animatedHurtboxSources/" + source.SourceId
                    });
                }
            }
            var bodyPartsById = new Dictionary<string, CharacterBodyPartAuthoring>(StringComparer.Ordinal);
            for (int i = 0; i < geometry.BodyParts.Count; i++)
            {
                CharacterBodyPartAuthoring part = geometry.BodyParts[i];
                if (part == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(part.PartId) && !bodyPartsById.ContainsKey(part.PartId))
                    bodyPartsById.Add(part.PartId, part);

                int partId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    part.PartId,
                    "geometry/body_parts.json",
                    "geometry/bodyParts/" + part.PartId,
                    "partId",
                    issues,
                    required: true);
                int parentPartId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    part.ParentPartId,
                    "geometry/body_parts.json",
                    "geometry/bodyParts/" + part.PartId,
                    "parentPartId",
                    issues,
                    required: false);
                int defaultHitZoneId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    part.DefaultHitZoneId,
                    "geometry/body_parts.json",
                    "geometry/bodyParts/" + part.PartId,
                    "defaultHitZoneId",
                    issues,
                    required: false);
                int reactionGroupId = identityResolver.ResolveReactionGroupId(part.ReactionGroupId);

                binding.BodyParts.Add(new CharacterBodyPartBinding
                {
                    PartId = partId,
                    PartLabel = part.PartId ?? string.Empty,
                    DisplayName = part.DisplayName,
                    PartKind = part.PartKind,
                    ParentPartId = parentPartId,
                    ParentPartLabel = part.ParentPartId ?? string.Empty,
                    BonePath = part.BonePath,
                    LocatorId = part.LocatorId,
                    DefaultHitZoneId = defaultHitZoneId,
                    DefaultHitZoneLabel = part.DefaultHitZoneId ?? string.Empty,
                    ReactionGroupId = reactionGroupId,
                    ReactionGroupLabel = part.ReactionGroupId ?? string.Empty,
                    Tags = part.Tags != null ? new List<string>(part.Tags) : new List<string>(),
                    SourceObjectPath = "geometry/bodyParts/" + (!string.IsNullOrWhiteSpace(part.PartId) ? part.PartId : i.ToString(CultureInfo.InvariantCulture))
                });
            }

            if (hasCurrentBodyPartDamage)
            {
                CharacterBodyPartDamageDocument canonical =
                    CharacterBodyPartDamageDocumentCodec.CreateCanonicalDocument(
                        geometry.BodyPartDamage);
                for (int i = 0; i < canonical.PartStates.Count; i++)
                {
                    CharacterBodyPartDamageAuthoring definition = canonical.PartStates[i];
                    if (definition == null)
                        continue;
                    var memberPartLabels = new List<string>(
                        definition.MemberPartIds ?? new List<string>());
                    var memberPartIds = new List<int>();
                    for (int m = 0; m < memberPartLabels.Count; m++)
                    {
                        int memId = identityResolver.ResolveId(
                            CharacterContentIdentityDomains.Pose,
                            memberPartLabels[m],
                            "geometry/body_part_damage.json",
                            "bodyPartDamage/partStates/" + definition.PartStateId + "/memberPartIds/" + memberPartLabels[m],
                            "memberPartId",
                            issues,
                            required: true);
                        memberPartIds.Add(memId);
                    }

                    int partStateId = identityResolver.ResolveId(
                        CharacterContentIdentityDomains.Pose,
                        definition.PartStateId,
                        "geometry/body_part_damage.json",
                        "bodyPartDamage/partStates/" + definition.PartStateId,
                        "partStateId",
                        issues,
                        required: true);
                    int damageZoneId = identityResolver.ResolveId(
                        CharacterContentIdentityDomains.Pose,
                        definition.DamageZoneId,
                        "geometry/body_part_damage.json",
                        "bodyPartDamage/partStates/" + definition.PartStateId,
                        "damageZoneId",
                        issues,
                        required: false);

                    binding.BodyPartDamageDefinitions.Add(new CharacterBodyPartDamageBinding
                    {
                        DefinitionIndex = i,
                        PartStateId = partStateId,
                        PartStateLabel = definition.PartStateId ?? string.Empty,
                        MemberPartIds = memberPartIds,
                        MemberPartLabels = memberPartLabels,
                        DamageZoneId = damageZoneId,
                        DamageZoneLabel = definition.DamageZoneId ?? string.Empty,
                        WoundCapacity = definition.WoundCapacity,
                        OpenDamageScaleMilli = definition.OpenDamageScaleMilli,
                        FocusConsumeTag = definition.FocusConsumeTag ?? string.Empty,
                        MaxIntegrity = definition.MaxIntegrity,
                        IntegrityDamageScaleMilli = definition.IntegrityDamageScaleMilli,
                        BreakReactionId = definition.BreakReactionId ?? string.Empty,
                        AuthoredPriorityDeltaOpen = definition.AuthoredPriorityDeltaOpen,
                        AuthoredPriorityDeltaBroken = definition.AuthoredPriorityDeltaBroken,
                        Severable = definition.Severable,
                        SeverRequiredTag = definition.SeverRequiredTag ?? string.Empty,
                        PresentationEventId = definition.PresentationEventId ?? string.Empty,
                        SourceObjectPath = "bodyPartDamage/partStates/" + definition.PartStateId
                    });
                    for (int memberIndex = 0; memberIndex < memberPartLabels.Count; memberIndex++)
                    {
                        string memberPartLabel = memberPartLabels[memberIndex] ?? string.Empty;
                        int memberPartId = memberPartIds.Count > memberIndex ? memberPartIds[memberIndex] : 0;
                        binding.BodyPartDamagePartLookup.Add(new CharacterBodyPartDamageLookupBinding
                        {
                            PartId = memberPartId,
                            PartLabel = memberPartLabel,
                            DefinitionIndex = i,
                            SourceObjectPath = "bodyPartDamage/partStates/"
                                + definition.PartStateId + "/memberPartIds/" + memberPartLabel
                        });
                    }
                }
                binding.BodyPartDamagePartLookup.Sort((left, right) =>
                    left.PartId.CompareTo(right.PartId));
            }

            List<CharacterBodyPartContactPointProfile> contactPoints = GetSortedBodyPartContactPoints(geometry);
            for (int i = 0; i < contactPoints.Count; i++)
            {
                CharacterBodyPartContactPointProfile contactPoint = contactPoints[i];
                if (contactPoint == null)
                    continue;

                string objectPath = BodyPartContactPointObjectPath(contactPoint, i);
                int partId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    !string.IsNullOrWhiteSpace(contactPoint.PartId) ? contactPoint.PartId : contactPoint.BodyPartId.ToString(CultureInfo.InvariantCulture),
                    "geometry/body_part_contact_points.json",
                    objectPath,
                    "partId",
                    issues,
                    required: true);
                int motionSocketId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    contactPoint.MotionSocketId,
                    "geometry/body_part_contact_points.json",
                    objectPath,
                    "motionSocketId",
                    issues,
                    required: false);

                binding.BodyPartContactPoints.Add(new CharacterBodyPartContactPointBinding
                {
                    StableId = contactPoint.StableId,
                    ContactPointId = contactPoint.ContactPointId,
                    BodyPartId = partId,
                    PartId = partId,
                    PartLabel = !string.IsNullOrWhiteSpace(contactPoint.PartId) ? contactPoint.PartId : string.Empty,
                    LocalOffsetMeters = contactPoint.LocalOffsetMeters ?? new CharacterAuthoringVector3(),
                    RadiusMeters = contactPoint.RadiusMeters,
                    LayerMaskProfileId = contactPoint.LayerMaskProfileId,
                    Usage = string.IsNullOrWhiteSpace(contactPoint.Usage)
                        ? "PresentationAudio"
                        : contactPoint.Usage,
                    Authority = string.IsNullOrWhiteSpace(contactPoint.Authority)
                        ? "PresentationOnly"
                        : contactPoint.Authority,
                    ActivationMode = contactPoint.ActivationMode,
                    MotionMode = contactPoint.MotionMode,
                    MotionSocketId = motionSocketId,
                    MotionSocketLabel = contactPoint.MotionSocketId ?? string.Empty,
                    SourceOrder = contactPoint.SourceOrder,
                    SourceObjectPath = objectPath
                });
            }

            for (int i = 0; i < geometry.Colliders.Count; i++)
            {
                CharacterBodyColliderProfile collider = geometry.Colliders[i];
                if (collider == null)
                    continue;

                string objectPath = "geometry/colliders/" + (!string.IsNullOrWhiteSpace(collider.ColliderId) ? collider.ColliderId : i.ToString(CultureInfo.InvariantCulture));
                int colliderId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    collider.ColliderId,
                    "geometry/body_colliders.json",
                    objectPath,
                    "colliderId",
                    issues,
                    required: true);
                int partId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    collider.PartId,
                    "geometry/body_colliders.json",
                    objectPath,
                    "partId",
                    issues,
                    required: true);
                int hitZoneId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    collider.HitZoneId,
                    "geometry/body_colliders.json",
                    objectPath,
                    "hitZoneId",
                    issues,
                    required: true);

                binding.BodyColliders.Add(new CharacterBodyColliderBinding
                {
                    ColliderId = colliderId,
                    ColliderLabel = collider.ColliderId ?? string.Empty,
                    PartId = partId,
                    PartLabel = collider.PartId ?? string.Empty,
                    HitZoneId = hitZoneId,
                    HitZoneLabel = collider.HitZoneId ?? string.Empty,
                    Shape = collider.Shape,
                    LocalPose = NormalizeColliderPose(collider, bodyPartsById),
                    Size = collider.Size ?? new CharacterAuthoringVector3(),
                    Radius = collider.Radius,
                    Height = collider.Height,
                    Priority = collider.Priority,
                    IsWeakPoint = collider.IsWeakPoint,
                    DamageMultiplierOverride = collider.DamageMultiplierOverride,
                    ImpactPowerScaleOverride = collider.ImpactPowerScaleOverride,
                    PhysicsLayer = collider.PhysicsLayer,
                    MaterialStableId = collider.MaterialStableId,
                    SourceObjectPath = objectPath
                });
                binding.HitZoneBindings.Add(new CharacterHitZoneBinding
                {
                    HitZoneId = hitZoneId,
                    HitZoneLabel = collider.HitZoneId ?? string.Empty,
                    PartId = partId,
                    PartLabel = collider.PartId ?? string.Empty,
                    Priority = collider.Priority,
                    IsWeakPoint = collider.IsWeakPoint,
                    DamageMultiplierOverride = collider.DamageMultiplierOverride,
                    ImpactPowerScaleOverride = collider.ImpactPowerScaleOverride,
                    SourceObjectPath = objectPath
                });
            }

            if (hasCurrentDamageZones)
            {
                var zones = new List<CharacterDamageZoneAuthoring>(
                    geometry.DamageZones.Zones ?? new List<CharacterDamageZoneAuthoring>());
                zones.Sort((left, right) => string.CompareOrdinal(left?.ZoneId, right?.ZoneId));
                for (int i = 0; i < zones.Count; i++)
                {
                    CharacterDamageZoneAuthoring zone = zones[i];
                    if (zone == null)
                        continue;
                    int zoneId = identityResolver.ResolveId(
                        CharacterContentIdentityDomains.Pose,
                        zone.ZoneId,
                        "geometry/damage_zones.json",
                        "damageZones/zones/" + zone.ZoneId,
                        "zoneId",
                        issues,
                        required: true);
                    var hitZoneLabels = new List<string>(zone.HitZoneIds ?? new List<string>());
                    var hitZoneIds = new List<int>();
                    for (int h = 0; h < hitZoneLabels.Count; h++)
                    {
                        int hid = identityResolver.ResolveId(
                            CharacterContentIdentityDomains.Pose,
                            hitZoneLabels[h],
                            "geometry/damage_zones.json",
                            "damageZones/zones/" + zone.ZoneId + "/hitZoneIds/" + hitZoneLabels[h],
                            "hitZoneId",
                            issues,
                            required: true);
                        hitZoneIds.Add(hid);
                    }
                    hitZoneIds.Sort();
                    hitZoneLabels.Sort(StringComparer.Ordinal);

                    binding.DamageZones.Add(new CharacterDamageZoneBinding
                    {
                        ZoneId = zoneId,
                        ZoneLabel = zone.ZoneId ?? string.Empty,
                        DisplayName = string.IsNullOrWhiteSpace(zone.DisplayName) ? zone.ZoneId : zone.DisplayName,
                        HitZoneIds = hitZoneIds,
                        HitZoneLabels = hitZoneLabels,
                        Hittable = zone.Hittable,
                        Aimable = zone.Aimable,
                        Approachable = zone.Approachable,
                        AuthoredPriority = zone.AuthoredPriority,
                        SourceObjectPath = "damageZones/zones/" + zone.ZoneId
                    });
                }
            }

            if (hasCurrentEngagementAnchors)
            {
                var anchors = new List<CharacterEngagementAnchorAuthoring>(
                    geometry.EngagementAnchors.Anchors ?? new List<CharacterEngagementAnchorAuthoring>());
                anchors.Sort((left, right) => string.CompareOrdinal(left?.AnchorId, right?.AnchorId));
                for (int i = 0; i < anchors.Count; i++)
                {
                    CharacterEngagementAnchorAuthoring anchor = anchors[i];
                    if (anchor == null)
                        continue;
                    int anchorId = identityResolver.ResolveId(
                        CharacterContentIdentityDomains.Pose,
                        anchor.AnchorId,
                        "geometry/engagement_anchors.json",
                        "engagementAnchors/anchors/" + anchor.AnchorId,
                        "anchorId",
                        issues,
                        required: true);
                    int zoneId = identityResolver.ResolveId(
                        CharacterContentIdentityDomains.Pose,
                        anchor.ZoneId,
                        "geometry/engagement_anchors.json",
                        "engagementAnchors/anchors/" + anchor.AnchorId,
                        "zoneId",
                        issues,
                        required: true);
                    int phaseId = identityResolver.ResolveId(
                        CharacterContentIdentityDomains.Pose,
                        anchor.PhaseId,
                        "geometry/engagement_anchors.json",
                        "engagementAnchors/anchors/" + anchor.AnchorId,
                        "phaseId",
                        issues,
                        required: false);

                    CharacterAuthoringIntVector3 position =
                        anchor.LocalPositionMillimeters ?? new CharacterAuthoringIntVector3();
                    binding.EngagementAnchors.Add(new CharacterEngagementAnchorBinding
                    {
                        AnchorId = anchorId,
                        AnchorLabel = anchor.AnchorId ?? string.Empty,
                        ZoneId = zoneId,
                        ZoneLabel = anchor.ZoneId ?? string.Empty,
                        ReferenceFrame = anchor.ReferenceFrame,
                        LocalPositionMillimeters = new CharacterAuthoringIntVector3(
                            position.X,
                            position.Y,
                            position.Z),
                        YawMilliDegrees = anchor.YawMilliDegrees,
                        Capacity = anchor.Capacity,
                        AuthoredPriority = anchor.AuthoredPriority,
                        Enabled = anchor.Enabled,
                        PhaseId = phaseId,
                        PhaseLabel = anchor.PhaseId ?? string.Empty,
                        SourceObjectPath = "engagementAnchors/anchors/" + anchor.AnchorId
                    });
                }
                binding.EngagementAnchorContentHash = CharacterPackageHashUtility.ComputeTextSha256(
                    BuildEngagementAnchorsCanonicalText(
                        binding.EngagementAnchorRevision,
                        binding.EngagementAnchors));
            }

            for (int i = 0; i < geometry.Sockets.Count; i++)
            {
                CharacterSocketProfile socket = geometry.Sockets[i];
                if (socket == null)
                    continue;

                int socketId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    socket.SocketId,
                    "geometry/sockets.json",
                    "geometry/sockets/" + socket.SocketId,
                    "socketId",
                    issues,
                    required: true);
                int parentPartId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    socket.ParentPartId,
                    "geometry/sockets.json",
                    "geometry/sockets/" + socket.SocketId,
                    "parentPartId",
                    issues,
                    required: false);
                int mirrorPairSocketId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    socket.MirrorPairSocketId,
                    "geometry/sockets.json",
                    "geometry/sockets/" + socket.SocketId,
                    "mirrorPairSocketId",
                    issues,
                    required: false);

                binding.Sockets.Add(new CharacterSocketBinding
                {
                    SocketId = socketId,
                    SocketLabel = socket.SocketId ?? string.Empty,
                    AbstractBoneId = socket.AbstractBoneId,
                    ParentPartId = parentPartId,
                    ParentPartLabel = socket.ParentPartId ?? string.Empty,
                    BonePath = socket.BonePath,
                    LocatorPath = socket.LocatorPath,
                    LocalPose = NormalizeSocketPose(socket),
                    Usage = socket.Usage,
                    MirrorPairSocketId = mirrorPairSocketId,
                    MirrorPairSocketLabel = socket.MirrorPairSocketId ?? string.Empty,
                    Handedness = socket.Handedness,
                    SideTag = socket.SideTag,
                    Tags = socket.Tags != null ? new List<string>(socket.Tags) : new List<string>(),
                    SourceObjectPath = "geometry/sockets/" + (!string.IsNullOrWhiteSpace(socket.SocketId) ? socket.SocketId : i.ToString(CultureInfo.InvariantCulture))
                });
            }

            List<WeaponAttachmentProfile> canonicalAttachments = BuildSyntheticAttachments(package);
            for (int i = 0; i < canonicalAttachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = canonicalAttachments[i];
                if (attachment == null)
                    continue;
                weaponDefinitions.TryGetValue(attachment.SingleWeaponConfigurationId ?? string.Empty, out SingleWeaponConfiguration weaponDefinition);
                weaponIdsByConfigId.TryGetValue(attachment.SingleWeaponConfigurationId ?? string.Empty, out int weaponNumericId);

                int equipSlotId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Slot,
                    attachment.EquipSlot,
                    "config/character_application.json",
                    "combinedWeaponConfigurationBindings/derivedAttachments/" + attachment.AttachmentId,
                    "equipSlot",
                    issues,
                    required: true);
                int attachSocketId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    attachment.AttachSocketId,
                    "config/character_application.json",
                    "combinedWeaponConfigurationBindings/derivedAttachments/" + attachment.AttachmentId,
                    "attachSocketId",
                    issues,
                    required: true);
                int traceId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Trace,
                    attachment.TraceId,
                    "config/character_application.json",
                    "combinedWeaponConfigurationBindings/derivedAttachments/" + attachment.AttachmentId,
                    "traceId",
                    issues,
                    required: false);
                int traceStartSocketId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    attachment.TraceStartSocketId,
                    "config/character_application.json",
                    "combinedWeaponConfigurationBindings/derivedAttachments/" + attachment.AttachmentId,
                    "traceStartSocketId",
                    issues,
                    required: false);
                int traceEndSocketId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Pose,
                    attachment.TraceEndSocketId,
                    "config/character_application.json",
                    "combinedWeaponConfigurationBindings/derivedAttachments/" + attachment.AttachmentId,
                    "traceEndSocketId",
                    issues,
                    required: false);

                binding.WeaponAttachments.Add(new CharacterWeaponAttachmentBinding
                {
                    AttachmentId = attachment.AttachmentId,
                    WeaponId = weaponNumericId,
                    WeaponLabel = attachment.SingleWeaponConfigurationId ?? string.Empty,
                    EquipSlot = equipSlotId,
                    EquipSlotLabel = attachment.EquipSlot ?? string.Empty,
                    AttachSocketId = attachSocketId,
                    AttachSocketLabel = attachment.AttachSocketId ?? string.Empty,
                    LocalGripPose = NormalizeWeaponGripPose(attachment),
                    SlotPose = ClonePose(attachment.LocalGripPose),
                    WeaponModelPose = ClonePose(weaponDefinition?.ModelPose),
                    WeaponRef = CloneSelectionRef(weaponDefinition?.WeaponRef),
                    PreviewResourceKey = GetWeaponRuntimeResourceKey(weaponDefinition),
                    TraceId = traceId,
                    TraceLabel = attachment.TraceId ?? string.Empty,
                    TraceStartSocketId = traceStartSocketId,
                    TraceStartSocketLabel = attachment.TraceStartSocketId ?? string.Empty,
                    TraceEndSocketId = traceEndSocketId,
                    TraceEndSocketLabel = attachment.TraceEndSocketId ?? string.Empty,
                    TraceRadius = attachment.TraceRadius,
                    TraceSampleRule = attachment.TraceSampleRule,
                    SourceObjectPath = "combinedWeaponConfigurationBindings/derivedAttachments/" + (!string.IsNullOrWhiteSpace(attachment.AttachmentId) ? attachment.AttachmentId : i.ToString(CultureInfo.InvariantCulture))
                });
            }

            for (int definitionIndex = 0; definitionIndex < globalDefinitions.Count; definitionIndex++)
            {
                SingleWeaponConfiguration definition = globalDefinitions[definitionIndex];
                weaponIdsByConfigId.TryGetValue(definition?.SingleWeaponConfigurationId ?? string.Empty, out int weaponNumericId);
                for (int i = 0; definition?.TraceProfiles != null && i < definition.TraceProfiles.Count; i++)
                {
                    SingleWeaponTraceProfile trace = definition.TraceProfiles[i];
                    if (trace == null) continue;
                    int traceId = identityResolver.ResolveId(
                        CharacterContentIdentityDomains.Trace,
                        trace.TraceId,
                        "WeaponSource/global_weapon_library.json",
                        "singleWeaponConfigurations/" + definition.SingleWeaponConfigurationId + "/traceProfiles/" + trace.TraceId,
                        "traceId",
                        issues,
                        required: true);

                    binding.WeaponTraces.Add(new CharacterWeaponTraceBinding
                    {
                        TraceId = traceId,
                        TraceLabel = trace.TraceId ?? string.Empty,
                        WeaponId = weaponNumericId,
                        WeaponLabel = definition.SingleWeaponConfigurationId ?? string.Empty,
                        EquipSlot = 0,
                        EquipSlotLabel = string.Empty,
                        StartLocatorPath = trace.StartLocatorPath,
                        EndLocatorPath = trace.EndLocatorPath,
                        StartPose = ClonePose(trace.StartPose),
                        EndPose = ClonePose(trace.EndPose),
                        Radius = trace.Radius,
                        ProximalExtension = trace.ProximalExtension,
                        ProximalRadius = trace.ProximalRadius,
                        SampleRule = trace.SampleRule,
                        FixedSampleCount = trace.FixedSampleCount,
                        ActionKeys = trace.ActionKeys != null ? new List<string>(trace.ActionKeys) : new List<string>(),
                        SourcePath = "WeaponSource/global_weapon_library.json",
                        SourceObjectPath = "singleWeaponConfigurations/" + definition.SingleWeaponConfigurationId + "/traceProfiles/" + trace.TraceId
                    });
                }
            }

            return binding;
        }

        private static bool HasBlockingBodyPresenceIssue(CharacterAuthoringValidationReport validation)
        {
            List<CharacterAuthoringValidationIssue> issues = validation?.Issues;
            for (int i = 0; issues != null && i < issues.Count; i++)
            {
                CharacterAuthoringValidationIssue issue = issues[i];
                if (issue != null
                    && issue.Severity == CharacterAuthoringValidationSeverity.Error
                    && string.Equals(
                        issue.SourcePath,
                        "geometry/body_presence.json",
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool HasBlockingBodyPartDamageIssue(
            CharacterAuthoringValidationReport validation)
        {
            List<CharacterAuthoringValidationIssue> issues = validation?.Issues;
            for (int i = 0; issues != null && i < issues.Count; i++)
            {
                CharacterAuthoringValidationIssue issue = issues[i];
                if (issue != null
                    && issue.Severity == CharacterAuthoringValidationSeverity.Error
                    && string.Equals(
                        issue.SourcePath,
                        "geometry/body_part_damage.json",
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static CharacterAuthoringLocalPose NormalizeColliderPose(CharacterBodyColliderProfile collider, Dictionary<string, CharacterBodyPartAuthoring> bodyPartsById)
        {
            CharacterAuthoringLocalPose pose = ClonePose(collider != null ? collider.LocalPose : null);
            if (collider == null || HasExplicitPoseParent(pose))
                return pose;

            string parentPath = collider.PartId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(parentPath) && bodyPartsById != null && bodyPartsById.TryGetValue(parentPath, out CharacterBodyPartAuthoring part))
            {
                pose.ParentKind = CharacterPoseParentKind.BodyPart;
                pose.ParentPath = part.PartId;
            }

            return pose;
        }

        private static CharacterAuthoringLocalPose NormalizeSocketPose(CharacterSocketProfile socket)
        {
            CharacterAuthoringLocalPose pose = ClonePose(socket != null ? socket.LocalPose : null);
            if (socket == null || HasExplicitPoseParent(pose))
                return pose;

            if (!string.IsNullOrWhiteSpace(socket.BonePath))
            {
                pose.ParentKind = CharacterPoseParentKind.Bone;
                pose.ParentPath = socket.BonePath;
            }
            else if (!string.IsNullOrWhiteSpace(socket.LocatorPath))
            {
                pose.ParentKind = CharacterPoseParentKind.Locator;
                pose.ParentPath = socket.LocatorPath;
            }
            else if (!string.IsNullOrWhiteSpace(socket.ParentPartId))
            {
                pose.ParentKind = CharacterPoseParentKind.BodyPart;
                pose.ParentPath = socket.ParentPartId;
            }

            return pose;
        }

        private static CharacterAuthoringLocalPose NormalizeWeaponGripPose(WeaponAttachmentProfile attachment)
        {
            CharacterAuthoringLocalPose pose = ClonePose(attachment != null ? attachment.LocalGripPose : null);
            if (attachment == null || HasExplicitPoseParent(pose))
                return pose;

            if (!string.IsNullOrWhiteSpace(attachment.AttachSocketId))
            {
                pose.ParentKind = CharacterPoseParentKind.Socket;
                pose.ParentPath = attachment.AttachSocketId;
            }

            return pose;
        }

        private static bool HasExplicitPoseParent(CharacterAuthoringLocalPose pose)
        {
            return pose != null && pose.ParentKind != CharacterPoseParentKind.Unknown && !string.IsNullOrWhiteSpace(pose.ParentPath);
        }

        private static CharacterAuthoringLocalPose ClonePose(CharacterAuthoringLocalPose pose)
        {
            if (pose == null)
                return new CharacterAuthoringLocalPose();

            return new CharacterAuthoringLocalPose
            {
                ParentKind = pose.ParentKind,
                ParentPath = pose.ParentPath ?? string.Empty,
                Position = CloneVector(pose.Position),
                Rotation = CloneQuaternion(pose.Rotation),
                Scale = CloneScale(pose.Scale),
                EulerHint = CloneVector(pose.EulerHint)
            };
        }

        private static CharacterAuthoringVector3 CloneVector(CharacterAuthoringVector3 value)
        {
            return value == null ? new CharacterAuthoringVector3() : new CharacterAuthoringVector3(value.X, value.Y, value.Z);
        }

        private static CharacterAuthoringVector3 CloneScale(CharacterAuthoringVector3 value)
        {
            return value == null ? new CharacterAuthoringVector3(1f, 1f, 1f) : new CharacterAuthoringVector3(value.X, value.Y, value.Z);
        }

        private static CharacterAuthoringQuaternion CloneQuaternion(CharacterAuthoringQuaternion value)
        {
            return value == null ? new CharacterAuthoringQuaternion() : new CharacterAuthoringQuaternion(value.X, value.Y, value.Z, value.W);
        }

        /// <summary>收集被非预览条目声明的物理相对路径，用于用途冲突保护。</summary>
        private static HashSet<string> CollectFormallyClaimedPaths(CharacterPackageResourceCatalog catalog)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (catalog == null)
                return paths;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null || CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb(entry))
                    continue;

                string path = NormalizeCatalogRelativePath(entry.RelativePath);
                if (path.Length > 0)
                    paths.Add(path);
            }

            return paths;
        }

        private static string NormalizeCatalogRelativePath(string relativePath)
        {
            return (relativePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
        }

        /// <summary>
        /// 用途冲突诊断：被标为专用预览的 GLB 同时被非预览条目声明。
        /// </summary>
        /// <remarks>
        /// 不静默删引用、不自动换模型、也不自动重标成正式用途——由资源所有者显式分类。
        /// </remarks>
        private static void AddPreviewGlbRuntimeReferenceIssue(
            List<CharacterAuthoringValidationIssue> issues,
            CharacterPackageResourceEntry entry,
            string relativePath)
        {
            if (issues == null)
                return;

            issues.Add(new CharacterAuthoringValidationIssue
            {
                Severity = CharacterAuthoringValidationSeverity.Error,
                Gate = CharacterAuthoringValidationGate.ExportBlocked,
                Code = CharacterPackageResourceExportPolicy.RuntimeReferenceDiagnosticCode,
                SourcePath = "resource_catalog.json",
                SourceObjectPath = "entries/" + (entry.ResourceKey ?? string.Empty),
                Field = "usage",
                Message = "The authoring-only preview GLB '" + relativePath + "' is also claimed by a non-preview entry; the file was preserved and its export classification must be resolved by the resource owner.",
                SuggestedFix = "Give the formal use its own model file, or reclassify this entry so the preview and the runtime use no longer share one physical file."
            });
        }

        /// <summary>
        /// 专用预览 GLB 的外部依赖预检（§4.4）：不假定 GLB 内嵌全部纹理与 buffer。
        /// </summary>
        /// <remarks>
        /// 只判定与报告，不修改文件、不递归删除同目录贴图/buffer/共享材质。
        /// 依赖缺失或 URI 越界/绝对/带外部 scheme 时报告错误，避免在停止生成副本后
        /// 才发现作者源预览本身不可读。
        /// </remarks>
        private static void AddPreviewGlbDependencyIssues(
            List<CharacterAuthoringValidationIssue> issues,
            CharacterResourcePackage package,
            CharacterPackageResourceEntry entry)
        {
            if (issues == null || package == null || entry == null)
                return;
            if (string.IsNullOrWhiteSpace(package.PackagePath) || string.IsNullOrWhiteSpace(entry.RelativePath))
                return;

            string glbPath = System.IO.Path.Combine(
                package.PackagePath,
                entry.RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            CharacterGlbDependencyReport report = CharacterGlbDependencyInspector.Inspect(glbPath, package.PackagePath);

            if (!report.Inspected)
            {
                issues.Add(new CharacterAuthoringValidationIssue
                {
                    Severity = CharacterAuthoringValidationSeverity.Error,
                    Gate = CharacterAuthoringValidationGate.ExportBlocked,
                    Code = CharacterPackageResourceExportPolicy.DependencyDiagnosticCode,
                    SourcePath = "resource_catalog.json",
                    SourceObjectPath = "entries/" + (entry.ResourceKey ?? string.Empty),
                    Field = "relativePath",
                    Message = "Authoring-only preview GLB could not be inspected: " + report.FailureReason,
                    SuggestedFix = "Restore the authoring source GLB before the generated copy is retired."
                });
                return;
            }

            for (int i = 0; i < report.Violations.Count; i++)
            {
                issues.Add(new CharacterAuthoringValidationIssue
                {
                    Severity = CharacterAuthoringValidationSeverity.Error,
                    Gate = CharacterAuthoringValidationGate.ExportBlocked,
                    Code = CharacterPackageResourceExportPolicy.DependencyDiagnosticCode,
                    SourcePath = "resource_catalog.json",
                    SourceObjectPath = "entries/" + (entry.ResourceKey ?? string.Empty),
                    Field = "relativePath",
                    Message = "Authoring-only preview GLB dependency check failed: " + report.Violations[i],
                    SuggestedFix = "Keep external dependencies inside the package root using relative URIs, or embed them in the GLB."
                });
            }
        }

        private static CharacterPackageResourceMapping BuildResourceMapping(
            CharacterResourcePackage package,
            CharacterAuthoringCompileOptions options,
            List<CharacterAuthoringValidationIssue> issues)
        {
            var mapping = new CharacterPackageResourceMapping();
            if (package == null)
                return mapping;

            mapping.PackageId = package.Manifest != null ? package.Manifest.PackageId : string.Empty;
            CharacterPackageResourceCatalog catalog = package.ResourceCatalog ?? new CharacterPackageResourceCatalog();

            // 同一物理文件被非预览条目声明时，不得因"预览别名被排除"而丢掉正式资源。
            HashSet<string> formallyClaimedPaths = CollectFormallyClaimedPaths(catalog);

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                // 专用预览 GLB 只服务制作侧：不进入 Unity / runtime 导入投影。
                // 源 catalog 不被修改，制作 API、资源浏览与诊断继续看到完整条目。
                // 该映射是 write plan 与表现资源键的共同上游，过滤在此处一次完成。
                if (CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb(entry))
                {
                    string entryPath = NormalizeCatalogRelativePath(entry.RelativePath);
                    if (entryPath.Length > 0 && formallyClaimedPaths.Contains(entryPath))
                    {
                        // 用途冲突：同一物理文件同时被正式条目声明。保留该条目使文件继续导出
                        // （从而不被孤立清理），并以稳定诊断要求资源所有者显式分类。
                        AddPreviewGlbRuntimeReferenceIssue(issues, entry, entryPath);
                    }
                    else
                    {
                        // 停止生成副本之前先预检源 GLB 的外部依赖：不假定它内嵌了全部纹理与 buffer。
                        AddPreviewGlbDependencyIssues(issues, package, entry);
                        continue;
                    }

                    AddPreviewGlbDependencyIssues(issues, package, entry);
                }

                string targetRelativePath = GetImportTargetRelativePath(entry);
                mapping.Entries.Add(new CharacterPackageResourceMappingEntry
                {
                    PackageResourceKey = entry.ResourceKey,
                    ProjectResourceKey = entry.ResourceKey,
                    StableId = entry.StableId,
                    TypeId = entry.TypeId,
                    Usage = entry.Usage,
                    SourceFormat = CharacterPackageResourcePipeline.GetEffectiveSourceFormat(entry),
                    SourceRelativePath = entry.RelativePath,
                    DeclaredContentHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(entry),
                    ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(entry),
                    DependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(entry, catalog),
                    ProviderId = entry.ImportHints != null ? entry.ImportHints.ProviderId : string.Empty,
                    TargetPathPolicy = GetTargetPathPolicy(entry, options),
                    ImportTargetPath = CombineProjectPath(GetTargetRootPath(package, options), targetRelativePath),
                    ModelWrapperPose = entry.ImportHints != null ? entry.ImportHints.ModelWrapperPose ?? new CharacterAuthoringLocalPose() : new CharacterAuthoringLocalPose(),
                    ConflictAction = entry.ConflictPolicy != null ? entry.ConflictPolicy.HashChangedAction : string.Empty
                });
            }

            return mapping;
        }

        private static CharacterAuthoringCompiledConfigPatch BuildConfigPatch(
            CharacterResourcePackage package,
            CharacterAuthoringGeometryBinding geometryBinding,
            CharacterPackageResourceMapping resourceMapping,
            CharacterContentIdentityResolver identityResolver,
            List<CharacterAuthoringValidationIssue> issues)
        {
            var compiled = new CharacterAuthoringCompiledConfigPatch();
            if (package == null)
                return compiled;

            compiled.PackageId = package.Manifest != null ? package.Manifest.PackageId : string.Empty;
            compiled.RuntimeCharacterId = identityResolver.ResolveId(
                CharacterContentIdentityDomains.RuntimeCharacter,
                compiled.PackageId,
                "Tools/RuntimeHash/character_definition_ids.json",
                "domains/413/" + compiled.PackageId,
                "runtimeCharacterId",
                issues,
                required: true);
            compiled.Patch = new PatchDocument
            {
                SchemaVersion = "1.0",
                Source = CharacterAuthoringCompilerFormats.ConfigPatchBundle
            };

            var ids = new GeneratedIds();
            string packageId = package.Manifest != null ? package.Manifest.PackageId : "character";
            string characterStableId = GetCharacterStableId(package);
            string attributeStableId = GetAttributeProfileStableId(package);
            string bodyStableId = GetBodyProfileStableId(package);
            string schemaStableId = GetEquipmentSchemaStableId(package);
            string presentationStableId = "mx.presentation." + NormalizeSegment(packageId);
            string spawnStableId = "mx.spawn." + NormalizeSegment(packageId) + ".default";
            string partSetId = NormalizeSegment(packageId) + "_parts";

            AddReference(compiled, CharacterApplicationCompilerTableNames.CharacterConfig, ids.CharacterId, characterStableId, "config/character_application.json", "characterStableId");
            AddReference(compiled, CharacterApplicationCompilerTableNames.CharacterAttributeProfileConfig, ids.AttributeProfileId, attributeStableId, "config/character_application.json", "attributeProfileStableId");
            AddReference(compiled, CharacterApplicationCompilerTableNames.CharacterBodyProfileConfig, ids.BodyProfileId, bodyStableId, "config/character_application.json", "bodyProfileStableId");
            AddReference(compiled, CharacterApplicationCompilerTableNames.EquipmentSchemaConfig, ids.EquipmentSchemaId, schemaStableId, "config/character_application.json", "equipmentSchemaStableId");
            AddReference(compiled, CharacterApplicationCompilerTableNames.CharacterPresentationProfileConfig, ids.PresentationProfileId, presentationStableId, "config/character_application.json", "presentationProfile");
            AddReference(compiled, CharacterApplicationCompilerTableNames.SpawnProfileConfig, ids.SpawnProfileId, spawnStableId, "config/character_application.json", "spawnProfile");

            CharacterAuthoringGeometry geometry = package.Geometry ?? new CharacterAuthoringGeometry();
            AddCharacterAndCoreEntries(compiled, package, ids, characterStableId, attributeStableId, bodyStableId, schemaStableId, presentationStableId, spawnStableId, partSetId, resourceMapping, geometryBinding, identityResolver, issues);
            AddBodyPartEntries(compiled, geometryBinding, ids, partSetId);
            AddBodyPartContactPointEntries(compiled, geometryBinding, ids, partSetId);

            List<WeaponAttachmentProfile> attachments = BuildSyntheticAttachments(package);
            List<SingleWeaponConfiguration> weaponDefinitions = GetSortedReferencedSingleWeapons(package);
            List<CombinedWeaponConfigurationAuthoringSummary> weaponConfigurations = GetSortedCombinedWeaponConfigurations(package.ApplicationConfig);
            Dictionary<string, int> weaponIds = AddWeaponEntries(compiled, geometry, weaponDefinitions, attachments, resourceMapping, ids, identityResolver, issues);
            LoadoutPlan loadouts = AddLoadoutsAndStates(compiled, package, weaponConfigurations, attachments, weaponIds, ids, identityResolver, issues);
            AddAbilityLoadouts(compiled, package, weaponConfigurations, loadouts, ids, identityResolver, issues);
            AddCombatActionSets(compiled, geometry, loadouts, ids);

            SetCharacterDefaultLoadout(compiled, ids.CharacterId, loadouts.DefaultLoadoutId, loadouts.DefaultLoadoutStableId);
            return compiled;
        }

        private static void AddCharacterAndCoreEntries(
            CharacterAuthoringCompiledConfigPatch compiled,
            CharacterResourcePackage package,
            GeneratedIds ids,
            string characterStableId,
            string attributeStableId,
            string bodyStableId,
            string schemaStableId,
            string presentationStableId,
            string spawnStableId,
            string partSetId,
            CharacterPackageResourceMapping resourceMapping,
            CharacterAuthoringGeometryBinding geometryBinding,
            CharacterContentIdentityResolver identityResolver,
            List<CharacterAuthoringValidationIssue> issues)
        {
            CharacterAuthoringGeometry geometry = package.Geometry ?? new CharacterAuthoringGeometry();
            CharacterBodyGeometryProfile body = geometry.BodyProfile ?? new CharacterBodyGeometryProfile();
            List<WeaponAttachmentProfile> attachments = BuildSyntheticAttachments(package);
            string packageSegment = NormalizeSegment(package.Manifest != null ? package.Manifest.PackageId : "character");

            AddEntry(compiled, CharacterApplicationCompilerTableNames.CharacterConfig, ids.CharacterId, new Dictionary<string, FieldValue>
            {
                ["CharacterId"] = Int(ids.CharacterId),
                ["StableId"] = Str(characterStableId),
                ["NameText"] = Str("character." + packageSegment + ".name"),
                ["DescriptionText"] = Str("character." + packageSegment + ".desc"),
                ["AttributeProfileId"] = Int(ids.AttributeProfileId),
                ["BodyProfileId"] = Int(ids.BodyProfileId),
                ["EquipmentSchemaId"] = Int(ids.EquipmentSchemaId),
                ["DefaultLoadoutId"] = Int(ids.DefaultLoadoutId),
                ["BaseAbilityLoadoutId"] = Int(0),
                ["PresentationProfileId"] = Int(ids.PresentationProfileId),
                ["DefaultControllerKind"] = Str("HumanInput"),
                ["DefaultControllerProfileId"] = Str("controller.human.default"),
                ["DefaultSpawnTags"] = List("sample", packageSegment)
            });

            CharacterBaseAttributesAuthoring attributes = package.BaseAttributes ?? new CharacterBaseAttributesAuthoring();
            AddEntry(compiled, CharacterApplicationCompilerTableNames.CharacterAttributeProfileConfig, ids.AttributeProfileId, new Dictionary<string, FieldValue>
            {
                ["AttributeProfileId"] = Int(ids.AttributeProfileId),
                ["StableId"] = Str(attributeStableId),
                ["BaseCohesion"] = Int(attributes.BaseCohesion),
                ["ScatterRecoveryRate"] = Int(attributes.ScatterRecoveryRate),
                ["ScatterRecoveryDelayFrames"] = Int(attributes.ScatterRecoveryDelayFrames),
                ["CohesionBreakRecoveryDelayFrames"] = Int(attributes.CohesionBreakRecoveryDelayFrames),
                ["Attributes"] = FieldValue.FromList(new[]
                {
                    Map(("AttributeId", Int(ids.HpAttributeId)), ("StableId", Str("attr.hp")), ("Group", Str("Vital")), ("BaseValue", Int(attributes.Hp)), ("InitialValue", Int(attributes.Hp)), ("MinValue", Int(0)), ("MaxValue", Int(attributes.Hp))),
                    Map(("AttributeId", Int(ids.AttackAttributeId)), ("StableId", Str("attr.attack")), ("Group", Str("Custom")), ("BaseValue", Int(attributes.Attack)), ("InitialValue", Int(attributes.Attack)), ("MinValue", Int(0)), ("MaxValue", Int(int.MaxValue))),
                    Map(("AttributeId", Int(ids.DefenseAttributeId)), ("StableId", Str("attr.defense")), ("Group", Str("Custom")), ("BaseValue", Int(attributes.Defense)), ("InitialValue", Int(attributes.Defense)), ("MinValue", Int(0)), ("MaxValue", Int(int.MaxValue))),
                    Map(("AttributeId", Int(ids.StaminaAttributeId)), ("StableId", Str("attr.stamina")), ("Group", Str("Resource")), ("BaseValue", Int(100)), ("InitialValue", Int(100)), ("MinValue", Int(0)), ("MaxValue", Int(100)))
                })
            });

            AddEntry(compiled, CharacterApplicationCompilerTableNames.CharacterBodyProfileConfig, ids.BodyProfileId, new Dictionary<string, FieldValue>
            {
                ["BodyProfileId"] = Int(ids.BodyProfileId),
                ["StableId"] = Str(bodyStableId),
                ["BodyKind"] = Str(MapBodyKind(package, body.BodyKind)),
                ["PartSetId"] = Str(partSetId),
                ["DefaultMotionProfileId"] = Str("motion." + packageSegment + ".default"),
                ["DefaultPhysicsProfileId"] = Str(string.IsNullOrWhiteSpace(body.DefaultPhysicsProfileId) ? "physics." + packageSegment + ".default" : body.DefaultPhysicsProfileId),
                ["Sockets"] = BuildSocketField(geometryBinding),
                ["HitZoneBindings"] = BuildHitZoneBindingField(geometryBinding)
            });

            AddEntry(compiled, CharacterApplicationCompilerTableNames.EquipmentSchemaConfig, ids.EquipmentSchemaId, new Dictionary<string, FieldValue>
            {
                ["EquipmentSchemaId"] = Int(ids.EquipmentSchemaId),
                ["StableId"] = Str(schemaStableId),
                ["Slots"] = BuildEquipmentSlotsField(geometry, identityResolver, issues),
                ["ExclusiveGroups"] = FieldValue.FromList(new[] { Map(("GroupId", Str("hands")), ("SlotIds", BuildSlotIdList(geometry, identityResolver, issues)), ("MaxFilledCount", Int(Math.Max(1, CountDistinctSlots(geometry)))), ("SlotLabels", List(GetDistinctSlots(geometry).ToArray()))) }),
                ["AllowedStateIds"] = BuildAllowedStateIdsField(package, attachments, ids)
            });

            var presentationFields = new Dictionary<string, FieldValue>
            {
                ["PresentationProfileId"] = Int(ids.PresentationProfileId),
                ["StableId"] = Str(presentationStableId),
                ["DefaultAnimationProfileId"] = Str("anim." + packageSegment + ".default"),
                ["ResourceKeys"] = BuildPresentationResourceKeys(package.ApplicationConfig, resourceMapping),
                ["PresentationTags"] = List(MapBodyKind(package, body.BodyKind).ToLowerInvariant(), "package:" + packageSegment)
            };
            if (package.ApplicationConfig?.GameplayCameraProfile != null)
                presentationFields["GameplayCameraProfile"] = BuildGameplayCameraProfileField(package.ApplicationConfig.GameplayCameraProfile);
            if (package.ApplicationConfig?.PresentationImages != null)
                presentationFields["PresentationImages"] = BuildPresentationImagesField(package.ApplicationConfig.PresentationImages);
            AddEntry(compiled, CharacterApplicationCompilerTableNames.CharacterPresentationProfileConfig, ids.PresentationProfileId, presentationFields);

            AddEntry(compiled, CharacterApplicationCompilerTableNames.SpawnProfileConfig, ids.SpawnProfileId, new Dictionary<string, FieldValue>
            {
                ["SpawnProfileId"] = Int(ids.SpawnProfileId),
                ["StableId"] = Str(spawnStableId),
                ["CharacterId"] = Int(ids.CharacterId),
                ["TeamId"] = Str("team.neutral"),
                ["ControllerKind"] = Str("HumanInput"),
                ["EquipmentLoadoutId"] = Int(ids.DefaultLoadoutId),
                ["SpawnPose"] = Map(("AnchorId", Str("spawn.default")), ("X", Float(0f)), ("Y", Float(0f)), ("Z", Float(0f)), ("YawDegrees", Float(0f))),
                ["RuntimeAiPlannerProfileId"] = Str(string.Empty),
                ["DebugName"] = Str(package.Manifest != null && !string.IsNullOrWhiteSpace(package.Manifest.DisplayName) ? package.Manifest.DisplayName : packageSegment)
            });
        }

        private static void AddBodyPartEntries(CharacterAuthoringCompiledConfigPatch compiled, CharacterAuthoringGeometryBinding geometryBinding, GeneratedIds ids, string partSetId)
        {
            if (geometryBinding == null)
                return;

            for (int i = 0; i < geometryBinding.BodyParts.Count; i++)
            {
                CharacterBodyPartBinding part = geometryBinding.BodyParts[i];
                if (part == null)
                    continue;

                int generatedId = ids.BodyPartStartId + i;
                string stableId = "mx.body_part." + NormalizeSegment(partSetId) + "." + NormalizeSegment(part.PartLabel);
                AddReference(compiled, CharacterApplicationCompilerTableNames.CharacterBodyPartConfig, generatedId, stableId, part.SourcePath, part.SourceObjectPath);
                AddEntry(compiled, CharacterApplicationCompilerTableNames.CharacterBodyPartConfig, generatedId, new Dictionary<string, FieldValue>
                {
                    ["BodyPartConfigId"] = Int(generatedId),
                    ["StableId"] = Str(stableId),
                    ["PartSetId"] = Str(partSetId),
                    ["PartId"] = NumericIdentity(part.PartId),
                    ["ParentPartId"] = NumericIdentity(part.ParentPartId),
                    ["PartKind"] = Str(part.PartKind.ToString()),
                    ["BonePath"] = Str(part.BonePath),
                    ["LocatorId"] = Str(!string.IsNullOrWhiteSpace(part.LocatorId) ? part.LocatorId : "loc." + NormalizeSegment(part.PartLabel)),
                    ["HitZoneId"] = NumericIdentity(part.DefaultHitZoneId),
                    ["ReactionGroupId"] = NumericIdentity(part.ReactionGroupId),
                    ["DamageMultiplier"] = Float(IsHead(part.PartLabel) ? 1.25f : 1f),
                    ["ImpulseScale"] = Float(1f),
                    ["StaggerScale"] = Float(1f),
                    ["ImpactPowerScale"] = Float(IsHead(part.PartLabel) ? 1.4f : 1f),
                    ["IsCritical"] = Bool(IsHead(part.PartLabel) || ContainsTag(part.Tags, "critical")),
                    ["Tags"] = List(part.Tags != null ? part.Tags.ToArray() : Array.Empty<string>()),
                    ["PartLabel"] = Str(part.PartLabel),
                    ["ParentPartLabel"] = Str(part.ParentPartLabel),
                    ["HitZoneLabel"] = Str(part.DefaultHitZoneLabel),
                    ["ReactionGroupLabel"] = Str(part.ReactionGroupLabel)
                });
            }
        }

        private static void AddBodyPartContactPointEntries(
            CharacterAuthoringCompiledConfigPatch compiled,
            CharacterAuthoringGeometryBinding geometryBinding,
            GeneratedIds ids,
            string partSetId)
        {
            if (geometryBinding == null)
                return;

            List<CharacterBodyPartContactPointBinding> contactPoints = geometryBinding.BodyPartContactPoints;
            for (int i = 0; i < contactPoints.Count; i++)
            {
                CharacterBodyPartContactPointBinding contactPoint = contactPoints[i];
                if (contactPoint == null)
                    continue;

                int generatedId = ids.BodyPartContactPointStartId + i;
                string stableId = !string.IsNullOrWhiteSpace(contactPoint.StableId)
                    ? contactPoint.StableId
                    : "mx.body_part_contact_point." + NormalizeSegment(partSetId) + "." + contactPoint.ContactPointId.ToString(CultureInfo.InvariantCulture);
                CharacterAuthoringVector3 offset = contactPoint.LocalOffsetMeters ?? new CharacterAuthoringVector3();
                AddReference(
                    compiled,
                    CharacterApplicationCompilerTableNames.CharacterBodyPartContactPointConfig,
                    generatedId,
                    stableId,
                    "geometry/body_part_contact_points.json",
                    contactPoint.SourceObjectPath);
                AddEntry(compiled, CharacterApplicationCompilerTableNames.CharacterBodyPartContactPointConfig, generatedId, new Dictionary<string, FieldValue>
                {
                    ["ContactPointConfigId"] = Int(generatedId),
                    ["StableId"] = Str(stableId),
                    ["PartSetId"] = Str(partSetId),
                    ["PartId"] = NumericIdentity(contactPoint.PartId),
                    ["ContactPointId"] = Int(contactPoint.ContactPointId),
                    ["BodyPartId"] = Int(contactPoint.BodyPartId),
                    ["LocalOffsetXMillimeters"] = Int(QuantizeMetersToMillimeters(offset.X)),
                    ["LocalOffsetYMillimeters"] = Int(QuantizeMetersToMillimeters(offset.Y)),
                    ["LocalOffsetZMillimeters"] = Int(QuantizeMetersToMillimeters(offset.Z)),
                    ["RadiusMillimeters"] = Int(Math.Max(0, QuantizeMetersToMillimeters(contactPoint.RadiusMeters))),
                    ["LayerMaskProfileId"] = Str(contactPoint.LayerMaskProfileId),
                    ["ActivationMode"] = Str(contactPoint.ActivationMode.ToString()),
                    ["MotionMode"] = Str(contactPoint.MotionMode.ToString()),
                    ["MotionSocketId"] = NumericIdentity(contactPoint.MotionSocketId),
                    ["Usage"] = Str(string.IsNullOrWhiteSpace(contactPoint.Usage)
                        ? "PresentationAudio"
                        : contactPoint.Usage),
                    ["Authority"] = Str(string.IsNullOrWhiteSpace(contactPoint.Authority)
                        ? "PresentationOnly"
                        : contactPoint.Authority),
                    ["SourceOrder"] = Int(contactPoint.SourceOrder),
                    ["PartLabel"] = Str(contactPoint.PartLabel),
                    ["MotionSocketLabel"] = Str(contactPoint.MotionSocketLabel)
                });
            }
        }

        private static Dictionary<string, int> AddWeaponEntries(
            CharacterAuthoringCompiledConfigPatch compiled,
            CharacterAuthoringGeometry geometry,
            List<SingleWeaponConfiguration> definitions,
            List<WeaponAttachmentProfile> attachments,
            CharacterPackageResourceMapping resourceMapping,
            GeneratedIds ids,
            CharacterContentIdentityResolver identityResolver,
            List<CharacterAuthoringValidationIssue> issues)
        {
            var weaponIds = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < definitions.Count; i++)
            {
                SingleWeaponConfiguration definition = definitions[i];
                int weaponId = ids.WeaponStartId + i;
                weaponIds[definition.SingleWeaponConfigurationId] = weaponId;
                string weaponSegment = NormalizeSegment(definition.SingleWeaponConfigurationId);
                string stableId = "mx.weapon." + weaponSegment;
                AddReference(compiled, CharacterApplicationCompilerTableNames.WeaponConfig, weaponId, stableId,
                    "WeaponSource/global_weapon_library.json", "singleWeaponConfigurations/" + definition.SingleWeaponConfigurationId);
                AddEntry(compiled, CharacterApplicationCompilerTableNames.WeaponConfig, weaponId, new Dictionary<string, FieldValue>
                {
                    ["WeaponId"] = Int(weaponId),
                    ["StableId"] = Str(stableId),
                    ["Category"] = Str(definition.Category),
                    ["WeaponTypeId"] = Str(definition.WeaponTypeId),
                    ["Tags"] = List(definition.Tags != null ? definition.Tags.ToArray() : Array.Empty<string>()),
                    ["OccupiesSlots"] = List(definition.OccupiesSlots != null ? definition.OccupiesSlots.ToArray() : Array.Empty<string>()),
                    ["GrantedAbilityLoadoutId"] = Int(0),
                    ["BladeTrailResourceKey"] = Str(definition.PresentationConfiguration?.BladeTrailResourceKey ?? string.Empty),
                    ["BladeTrailResidueMilliseconds"] = Int(definition.PresentationConfiguration?.BladeTrailResidueMilliseconds ?? 100),
                    ["BladeTrailStyleId"] = Int(definition.PresentationConfiguration?.BladeTrailStyleId ?? 0),
                    ["WeaponPresentationProfileId"] = Str(!string.IsNullOrWhiteSpace(definition.PresentationConfiguration?.ProfileId)
                        ? definition.PresentationConfiguration.ProfileId : "presentation." + weaponSegment),
                    ["WeaponCombatProfileId"] = Str(!string.IsNullOrWhiteSpace(definition.CombatConfiguration?.ProfileId)
                        ? definition.CombatConfiguration.ProfileId : "combat." + weaponSegment),
                    ["WeaponResourceProfileId"] = Str(!string.IsNullOrWhiteSpace(definition.ResourcePolicy?.ProfileId)
                        ? definition.ResourcePolicy.ProfileId : "resource." + weaponSegment),
                    ["ResourceKeys"] = BuildWeaponResourceKeys(definition, resourceMapping),
                    ["TraceBindings"] = BuildWeaponTraceBindings(geometry, definition, attachments, identityResolver, issues),
                    ["BaseModifiers"] = BuildSingleWeaponBaseModifiers(definition)
                });
            }

            return weaponIds;
        }

        private static LoadoutPlan AddLoadoutsAndStates(
            CharacterAuthoringCompiledConfigPatch compiled,
            CharacterResourcePackage package,
            List<CombinedWeaponConfigurationAuthoringSummary> configurations,
            List<WeaponAttachmentProfile> attachments,
            Dictionary<string, int> weaponIds,
            GeneratedIds ids,
            CharacterContentIdentityResolver identityResolver,
            List<CharacterAuthoringValidationIssue> issues)
        {
            var plan = new LoadoutPlan();
            Dictionary<string, WeaponAttachmentProfile> attachmentsById = IndexAttachmentsById(attachments);
            string packageId = package?.Manifest?.PackageId ?? "character";

            for (int i = 0; i < configurations.Count; i++)
            {
                CombinedWeaponConfigurationAuthoringSummary configuration = configurations[i];
                string stableId = ToLoadoutStableId(packageId, configuration.CombinedWeaponConfigurationId);
                int loadoutId = ids.LoadoutStartId + i;
                List<WeaponAttachmentProfile> loadoutAttachments = ResolveConfigurationAttachments(
                    packageId,
                    configuration,
                    attachmentsById);
                int stateId = ids.StateStartId + i;
                if (string.Equals(configuration.CombinedWeaponConfigurationId, package.ApplicationConfig.DefaultCombinedWeaponConfigurationId, StringComparison.Ordinal))
                {
                    plan.DefaultLoadoutId = loadoutId;
                    plan.DefaultLoadoutStableId = stableId;
                    ids.DefaultLoadoutId = loadoutId;
                }

                AddReference(compiled, CharacterApplicationCompilerTableNames.EquipmentLoadoutConfig, loadoutId, stableId,
                    "config/character_application.json", "combinedWeaponConfigurationBindings/" + configuration.CombinedWeaponConfigurationId);
                AddEntry(compiled, CharacterApplicationCompilerTableNames.EquipmentLoadoutConfig, loadoutId, new Dictionary<string, FieldValue>
                {
                    ["LoadoutId"] = Int(loadoutId),
                    ["StableId"] = Str(stableId),
                    ["EquipmentSchemaId"] = Int(ids.EquipmentSchemaId),
                    ["EquipmentStateId"] = Int(stateId),
                    ["Slots"] = BuildLoadoutSlots(packageId, configuration, weaponIds, identityResolver, issues)
                });

                int actionSetId = ids.ActionSetStartId + i;
                int stateAbilityLoadoutId = string.IsNullOrWhiteSpace(configuration.AbilityConfigurationId)
                    ? 0
                    : ids.StateAbilityLoadoutStartId + i;
                string stateStableId = ToStateStableId(stableId);
                plan.StateIdByLoadoutStableId[stableId] = stateId;
                plan.StateStableIdByLoadoutStableId[stableId] = stateStableId;
                plan.ActionSetIdByLoadoutStableId[stableId] = actionSetId;
                plan.ActionSetStableIdByLoadoutStableId[stableId] = !string.IsNullOrWhiteSpace(configuration.ActionConfiguration?.ActionSetId)
                    ? configuration.ActionConfiguration.ActionSetId
                    : (configuration.CombinedWeaponConfigurationId ?? string.Empty);
                plan.ConfigurationByLoadoutStableId[stableId] = configuration;
                plan.LoadoutAttachmentsByStableId[stableId] = loadoutAttachments;
                plan.OrderedLoadoutStableIds.Add(stableId);

                AddReference(compiled, CharacterApplicationCompilerTableNames.EquipmentStateConfig, stateId, stateStableId,
                    "config/character_application.json", "combinedWeaponConfigurationBindings/" + configuration.CombinedWeaponConfigurationId);
                AddEntry(compiled, CharacterApplicationCompilerTableNames.EquipmentStateConfig, stateId, new Dictionary<string, FieldValue>
                {
                    ["StateId"] = Int(stateId),
                    ["StableId"] = Str(stateStableId),
                    ["EquipmentSchemaId"] = Int(ids.EquipmentSchemaId),
                    ["Priority"] = Int(0),
                    ["MatchRules"] = FieldValue.FromList(Array.Empty<FieldValue>()),
                    ["GrantedAbilityLoadoutId"] = Int(stateAbilityLoadoutId),
                    ["CombatActionSetId"] = Int(actionSetId),
                    ["AnimationProfileId"] = Str(GetAnimationProfileId(package.GlobalWeaponLibrary, configuration.AnimationConfigurationId)),
                    ["Tags"] = BuildLoadoutTags(stableId, loadoutAttachments, package.GlobalWeaponLibrary)
                });
            }

            if (plan.DefaultLoadoutId == 0 && configurations.Count > 0)
            {
                ids.DefaultLoadoutId = ids.LoadoutStartId;
                plan.DefaultLoadoutId = ids.DefaultLoadoutId;
                plan.DefaultLoadoutStableId = ToLoadoutStableId(packageId, configurations[0].CombinedWeaponConfigurationId);
            }

            return plan;
        }

        private static void AddAbilityLoadouts(
            CharacterAuthoringCompiledConfigPatch compiled,
            CharacterResourcePackage package,
            List<CombinedWeaponConfigurationAuthoringSummary> configurations,
            LoadoutPlan loadouts,
            GeneratedIds ids,
            CharacterContentIdentityResolver identityResolver,
            List<CharacterAuthoringValidationIssue> issues)
        {
            for (int index = 0; index < configurations.Count; index++)
            {
                CombinedWeaponConfigurationAuthoringSummary configuration = configurations[index];
                int loadoutId = ids.StateAbilityLoadoutStartId + index;
                GlobalAbilityConfiguration source = GlobalWeaponConfigurationResolver.FindAbility(
                    package?.GlobalWeaponLibrary, configuration.AbilityConfigurationId);
                if (source == null)
                    continue;
                string stableId = source.AbilityConfigurationId;
                AddReference(compiled, CharacterApplicationCompilerTableNames.AbilityLoadoutConfig, loadoutId, stableId,
                    "WeaponSource/global_weapon_library.json", "abilityConfigurations/" + stableId);
                AddEntry(compiled, CharacterApplicationCompilerTableNames.AbilityLoadoutConfig, loadoutId, new Dictionary<string, FieldValue>
                {
                    ["LoadoutId"] = Int(loadoutId),
                    ["StableId"] = Str(stableId),
                    ["AbilityIds"] = BuildGlobalAbilityIds(source.Abilities),
                    ["RemoveAbilityIds"] = FieldValue.FromList(Array.Empty<FieldValue>()),
                    ["SlotBindings"] = BuildGlobalAbilitySlotBindings(source.Abilities, identityResolver, issues)
                });
            }
        }

        private static FieldValue BuildGlobalAbilitySlotBindings(
            List<GlobalAbilityEntry> source,
            CharacterContentIdentityResolver identityResolver,
            List<CharacterAuthoringValidationIssue> issues)
        {
            var values = new List<FieldValue>();
            var ordered = source != null
                ? new List<GlobalAbilityEntry>(source)
                : new List<GlobalAbilityEntry>();
            ordered.Sort((left, right) => string.CompareOrdinal(left?.SlotId, right?.SlotId));
            for (int i = 0; i < ordered.Count; i++)
            {
                GlobalAbilityEntry binding = ordered[i] ?? new GlobalAbilityEntry();
                if (!string.Equals(binding.TriggerMode, GlobalAbilityTriggerModes.Input, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(binding.SlotId))
                    continue;
                int slotId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Slot,
                    binding.SlotId,
                    "WeaponSource/global_weapon_library.json",
                    "abilityConfigurations/slotBindings/" + binding.SlotId,
                    "slotId",
                    issues,
                    required: true);
                values.Add(Map(
                    ("SlotId", NumericIdentity(slotId)),
                    ("AbilityId", Int(binding.AbilityId)),
                    ("InputIntentId", Str(binding.InputIntentId)),
                    ("SlotLabel", Str(binding.SlotId))));
            }
            return FieldValue.FromList(values);
        }

        private static FieldValue BuildGlobalAbilityIds(List<GlobalAbilityEntry> source)
        {
            var ids = new List<int>();
            for (int i = 0; source != null && i < source.Count; i++)
                if (source[i] != null && source[i].AbilityId > 0)
                    ids.Add(source[i].AbilityId);
            return BuildSortedAbilityIdList(ids);
        }

        private static FieldValue BuildSortedAbilityIdList(List<int> source)
        {
            var ordered = source != null ? new List<int>(source) : new List<int>();
            ordered.Sort();
            var values = new List<FieldValue>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
                values.Add(Int(ordered[i]));
            return FieldValue.FromList(values);
        }

        private static string GetAnimationProfileId(GlobalWeaponLibrary library, string configurationId)
        {
            GlobalAnimationConfiguration configuration = GlobalWeaponConfigurationResolver.FindAnimation(library, configurationId);
            if (configuration?.Profiles != null && configuration.Profiles.Count > 0)
            {
                string selected = string.Empty;
                for (int i = 0; i < configuration.Profiles.Count; i++)
                {
                    string candidate = configuration.Profiles[i]?.ProfileId ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(candidate) &&
                        (string.IsNullOrWhiteSpace(selected) || string.CompareOrdinal(candidate, selected) < 0))
                        selected = candidate;
                }
                return selected;
            }
            return string.Empty;
        }

        private static FieldValue BuildSingleWeaponBaseModifiers(SingleWeaponConfiguration definition)
        {
            var values = new List<FieldValue>();
            List<SingleWeaponModifierConfiguration> modifiers = definition?.BaseModifiers;
            for (int i = 0; modifiers != null && i < modifiers.Count; i++)
            {
                SingleWeaponModifierConfiguration modifier = modifiers[i] ?? new SingleWeaponModifierConfiguration();
                values.Add(Map(
                    ("ModifierStableId", Str(modifier.ModifierStableId)),
                    ("Value", Float(modifier.Value)),
                    ("Source", Str(modifier.Source))));
            }
            return FieldValue.FromList(values);
        }

        private static void AddAbilityLoadout(
            CharacterAuthoringCompiledConfigPatch compiled,
            int loadoutId,
            string generatedStableId,
            string fallbackSourcePath,
            string fallbackSourceObjectPath,
            AbilityLoadoutAuthoringMatch authoring,
            int[] fallbackAbilityIds,
            int[] fallbackRemoveAbilityIds,
            AbilitySlotBindingSpec[] fallbackSlotBindings)
        {
            CharacterAbilityLoadoutAuthoringSummary source = authoring?.Summary;
            string stableId = source != null && !string.IsNullOrWhiteSpace(source.StableId)
                ? source.StableId.Trim()
                : generatedStableId;
            string sourcePath = source != null ? "config/character_application.json" : fallbackSourcePath;
            string sourceObjectPath = source != null
                ? "abilityLoadouts/" + authoring.Index.ToString(CultureInfo.InvariantCulture)
                : fallbackSourceObjectPath;

            AddReference(compiled, CharacterApplicationCompilerTableNames.AbilityLoadoutConfig, loadoutId, stableId, sourcePath, sourceObjectPath);
            AddEntry(compiled, CharacterApplicationCompilerTableNames.AbilityLoadoutConfig, loadoutId, new Dictionary<string, FieldValue>
            {
                ["LoadoutId"] = Int(loadoutId),
                ["StableId"] = Str(stableId),
                ["AbilityIds"] = BuildAbilityIdList(source != null ? source.AbilityIds : null, fallbackAbilityIds),
                ["RemoveAbilityIds"] = BuildAbilityIdList(source != null ? source.RemoveAbilityIds : null, fallbackRemoveAbilityIds),
                ["SlotBindings"] = BuildAbilitySlotBindings(source != null ? source.SlotBindings : null, fallbackSlotBindings)
            });
        }

        private static FieldValue BuildAbilityIdList(List<int> source, int[] fallback)
        {
            var values = new List<FieldValue>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                    values.Add(Int(source[i]));
                return FieldValue.FromList(values);
            }

            if (fallback != null)
            {
                for (int i = 0; i < fallback.Length; i++)
                    values.Add(Int(fallback[i]));
            }

            return FieldValue.FromList(values);
        }

        private static FieldValue BuildAbilitySlotBindings(
            List<CharacterAbilitySlotBindingAuthoringSummary> source,
            AbilitySlotBindingSpec[] fallback)
        {
            var values = new List<FieldValue>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    CharacterAbilitySlotBindingAuthoringSummary binding = source[i] ?? new CharacterAbilitySlotBindingAuthoringSummary();
                    values.Add(Map(
                        ("SlotId", Str(binding.SlotId)),
                        ("AbilityId", Int(binding.AbilityId)),
                        ("InputIntentId", Str(binding.InputIntentId))));
                }

                return FieldValue.FromList(values);
            }

            if (fallback != null)
            {
                for (int i = 0; i < fallback.Length; i++)
                {
                    AbilitySlotBindingSpec binding = fallback[i];
                    values.Add(Map(
                        ("SlotId", Str(binding.SlotId)),
                        ("AbilityId", Int(binding.AbilityId)),
                        ("InputIntentId", Str(binding.InputIntentId))));
                }
            }

            return FieldValue.FromList(values);
        }

        private static void AddCombatActionSets(CharacterAuthoringCompiledConfigPatch compiled, CharacterAuthoringGeometry geometry, LoadoutPlan loadouts, GeneratedIds ids)
        {
            for (int index = 0; index < loadouts.OrderedLoadoutStableIds.Count; index++)
            {
                string loadoutStableId = loadouts.OrderedLoadoutStableIds[index];
                List<WeaponAttachmentProfile> attachments = loadouts.LoadoutAttachmentsByStableId[loadoutStableId];
                int actionSetId = ids.ActionSetStartId + index;
                string stableId = loadouts.ActionSetStableIdByLoadoutStableId[loadoutStableId];
                CombinedWeaponConfigurationAuthoringSummary configuration = loadouts.ConfigurationByLoadoutStableId[loadoutStableId];
                AddReference(compiled, CharacterApplicationCompilerTableNames.CombatActionSetConfig, actionSetId, stableId, "config/character_application.json", loadoutStableId);
                AddEntry(compiled, CharacterApplicationCompilerTableNames.CombatActionSetConfig, actionSetId, new Dictionary<string, FieldValue>
                {
                    ["ActionSetId"] = Int(actionSetId),
                    ["StableId"] = Str(stableId),
                    ["Actions"] = BuildCombatActions(geometry, attachments, configuration, index)
                });
            }
        }

        private static CharacterUnityImportWritePlan BuildWritePlan(
            CharacterResourcePackage package,
            CharacterAuthoringCompileOptions options,
            CharacterCompilerGateReport gate,
            CharacterPackageResourceMapping resourceMapping,
            CharacterAuthoringCompiledConfigPatch configPatch)
        {
            var plan = new CharacterUnityImportWritePlan();
            if (package == null)
                return plan;

            plan.PackageId = package.Manifest != null ? package.Manifest.PackageId : string.Empty;
            plan.TargetRootPath = GetTargetRootPath(package, options);
            plan.TargetPathPolicy = options != null ? options.TargetUnityPathPolicy : string.Empty;
            plan.CanWriteToUnityProject = gate != null && !gate.ExportBlocked && !gate.ImportBlocked;
            plan.CanSpawnAfterImport = plan.CanWriteToUnityProject && !gate.SpawnBlocked;

            if (resourceMapping != null)
            {
                for (int i = 0; i < resourceMapping.Entries.Count; i++)
                {
                    CharacterPackageResourceMappingEntry entry = resourceMapping.Entries[i];
                    plan.Writes.Add(new CharacterUnityImportWriteEntry
                    {
                        Kind = CharacterAuthoringCompilerWriteKinds.ResourceFile,
                        Owner = CharacterAuthoringCompilerOwnerKinds.UnityImporter,
                        SourcePath = entry.SourceRelativePath,
                        TargetPath = entry.ImportTargetPath,
                        WritePolicy = "CopyIfHashChanged",
                        ContentHash = entry.DeclaredContentHash
                    });
                }
            }

            if (package.ApplicationConfig != null)
            {
                plan.Writes.Add(new CharacterUnityImportWriteEntry
                {
                    Kind = CharacterAuthoringCompilerWriteKinds.ResourceFile,
                    Owner = CharacterAuthoringCompilerOwnerKinds.UnityImporter,
                    SourcePath = "config/character_application.json",
                    TargetPath = CombineProjectPath(plan.TargetRootPath, "package_cache/character_application.json"),
                    WritePolicy = "Recreate",
                    ContentHash = string.Empty
                });
            }

            return plan;
        }

        private static CharacterResolverVerificationPlan BuildResolverVerificationPlan(
            CharacterApplicationAuthoringSummary applicationConfig,
            GlobalWeaponLibrary globalWeaponLibrary,
            CharacterAuthoringCompiledConfigPatch configPatch,
            CharacterCompilerGateReport gate,
            CharacterPackageResourceMapping resourceMapping)
        {
            var plan = new CharacterResolverVerificationPlan();
            plan.Status = gate != null && !gate.ExportBlocked && !gate.ImportBlocked && !gate.SpawnBlocked ? "Ready" : "Blocked";
            if (gate != null && gate.Issues != null)
                plan.Diagnostics.AddRange(gate.Issues);

            var tableMap = new Dictionary<string, CharacterResolverTableRequirement>(StringComparer.Ordinal);
            if (configPatch != null && configPatch.GeneratedReferences != null)
            {
                for (int i = 0; i < configPatch.GeneratedReferences.Count; i++)
                {
                    CharacterGeneratedConfigReference reference = configPatch.GeneratedReferences[i];
                    CharacterResolverTableRequirement requirement;
                    if (!tableMap.TryGetValue(reference.TableName, out requirement))
                    {
                        requirement = new CharacterResolverTableRequirement { TableName = reference.TableName };
                        tableMap[reference.TableName] = requirement;
                        plan.RequiredTables.Add(requirement);
                    }

                    requirement.RowCount++;
                    requirement.GeneratedIds.Add(reference.GeneratedId);
                    requirement.StableIds.Add(reference.StableId);

                    if (reference.TableName == CharacterApplicationCompilerTableNames.CharacterConfig)
                        plan.CharacterStableId = reference.StableId;
                }
            }

            plan.RequiredTables.Sort((a, b) => string.CompareOrdinal(a.TableName, b.TableName));

            if (configPatch != null && configPatch.Patch != null)
            {
                for (int i = 0; i < configPatch.Patch.Entries.Count; i++)
                {
                    PatchEntry entry = configPatch.Patch.Entries[i];
                    if (entry == null)
                        continue;

                    if (entry.Source == CharacterApplicationCompilerTableNames.CharacterConfig)
                    {
                        plan.DefaultLoadoutStableId = FindStableId(configPatch, CharacterApplicationCompilerTableNames.EquipmentLoadoutConfig, entry.Fields.GetScalar("DefaultLoadoutId"));
                    }
                    else if (entry.Source == CharacterApplicationCompilerTableNames.AbilityLoadoutConfig)
                    {
                        FieldValue abilities;
                        if (entry.Fields.TryGetValue("AbilityIds", out abilities) && abilities != null && abilities.List != null)
                        {
                            for (int j = 0; j < abilities.List.Count; j++)
                            {
                                int abilityId;
                                if (int.TryParse(abilities.List[j].Scalar, NumberStyles.Integer, CultureInfo.InvariantCulture, out abilityId) && abilityId > 0 && !plan.KnownAbilityIds.Contains(abilityId))
                                    plan.KnownAbilityIds.Add(abilityId);
                            }
                        }
                    }
                }

                string expectedStateStableId = ToStateStableId(plan.DefaultLoadoutStableId);
                for (int i = 0; i < configPatch.Patch.Entries.Count; i++)
                {
                    PatchEntry entry = configPatch.Patch.Entries[i];
                    if (entry == null || entry.Source != CharacterApplicationCompilerTableNames.EquipmentStateConfig)
                        continue;
                    if (!string.Equals(entry.Fields.GetScalar("StableId"), expectedStateStableId, StringComparison.Ordinal))
                        continue;

                    plan.ExpectedActiveEquipmentStateStableId = expectedStateStableId;
                    plan.ExpectedCombatActionSetStableId = FindStableId(configPatch, CharacterApplicationCompilerTableNames.CombatActionSetConfig, entry.Fields.GetScalar("CombatActionSetId"));
                    plan.ExpectedAnimationProfileId = entry.Fields.GetScalar("AnimationProfileId");
                    break;
                }
            }

            if (resourceMapping != null)
            {
                List<CharacterPackageResourceMappingEntry> referencedEntries = GetReferencedResourceMappingEntries(applicationConfig, resourceMapping);
                for (int i = 0; i < referencedEntries.Count; i++)
                {
                    string key = referencedEntries[i].ProjectResourceKey;
                    if (!string.IsNullOrWhiteSpace(key))
                        plan.RequiredResourceKeys.Add(key);
                }
            }

            plan.KnownAbilityIds.Sort();
            List<GlobalAbilityConfiguration> abilityConfigurations = globalWeaponLibrary?.AbilityConfigurations;
            for (int i = 0; abilityConfigurations != null && i < abilityConfigurations.Count; i++)
            {
                List<GlobalAbilityEntry> entries = abilityConfigurations[i]?.Abilities;
                for (int j = 0; entries != null && j < entries.Count; j++)
                {
                    GlobalAbilityEntry entry = entries[j];
                    if (entry != null && entry.AbilityId > 0 &&
                        string.Equals(entry.ExecutionMode, GlobalAbilityExecutionModes.GameplayRuntime, StringComparison.Ordinal) &&
                        plan.KnownAbilityIds.Contains(entry.AbilityId) && !plan.GameplayRuntimeAbilityIds.Contains(entry.AbilityId))
                        plan.GameplayRuntimeAbilityIds.Add(entry.AbilityId);
                }
            }
            plan.GameplayRuntimeAbilityIds.Sort();
            plan.RequiredResourceKeys.Sort(StringComparer.Ordinal);
            return plan;
        }

        private static List<CharacterPackageSourceMapping> BuildSourceMappings(
            CharacterAuthoringCompiledConfigPatch configPatch,
            CharacterAuthoringGeometryBinding geometryBinding,
            CharacterPackageResourceMapping resourceMapping)
        {
            var mappings = new List<CharacterPackageSourceMapping>();
            if (configPatch != null && configPatch.GeneratedReferences != null)
            {
                for (int i = 0; i < configPatch.GeneratedReferences.Count; i++)
                {
                    CharacterGeneratedConfigReference reference = configPatch.GeneratedReferences[i];
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = reference.SourcePath,
                        SourceObjectPath = reference.SourceObjectPath,
                        TargetKind = "CharacterApplicationConfig",
                        TargetPath = reference.TableName + "/" + reference.GeneratedId.ToString(CultureInfo.InvariantCulture),
                        TargetField = "StableId"
                    });
                }
            }

            if (geometryBinding != null)
            {
                for (int i = 0; i < geometryBinding.BodyParts.Count; i++)
                {
                    CharacterBodyPartBinding part = geometryBinding.BodyParts[i];
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = part.SourcePath,
                        SourceObjectPath = part.SourceObjectPath,
                        TargetKind = "GeometryBinding",
                        TargetPath = "bodyParts/" + part.PartId,
                        TargetField = "part"
                    });
                }

                for (int i = 0; i < geometryBinding.BodyColliders.Count; i++)
                {
                    CharacterBodyColliderBinding collider = geometryBinding.BodyColliders[i];
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = collider.SourcePath,
                        SourceObjectPath = collider.SourceObjectPath,
                        TargetKind = "GeometryBinding",
                        TargetPath = "bodyColliders/" + collider.ColliderId,
                        TargetField = "collider"
                    });
                }

                for (int i = 0; i < (geometryBinding.DamageZones?.Count ?? 0); i++)
                {
                    CharacterDamageZoneBinding zone = geometryBinding.DamageZones[i];
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = zone.SourcePath,
                        SourceObjectPath = zone.SourceObjectPath,
                        TargetKind = "GeometryBinding",
                        TargetPath = "damageZones/" + (!string.IsNullOrEmpty(zone.ZoneLabel) ? zone.ZoneLabel : zone.ZoneId.ToString()),
                        TargetField = "zone"
                    });
                }

                for (int i = 0; i < (geometryBinding.EngagementAnchors?.Count ?? 0); i++)
                {
                    CharacterEngagementAnchorBinding anchor = geometryBinding.EngagementAnchors[i];
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = anchor.SourcePath,
                        SourceObjectPath = anchor.SourceObjectPath,
                        TargetKind = "GeometryBinding",
                        TargetPath = "engagementAnchors/" + (!string.IsNullOrEmpty(anchor.AnchorLabel) ? anchor.AnchorLabel : anchor.AnchorId.ToString()),
                        TargetField = "anchor"
                    });
                }

                if (geometryBinding.BodyPresenceMode == CharacterBodyPresenceBindingMode.Authored)
                {
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = "geometry/body_presence.json",
                        SourceObjectPath = "bodyPresence",
                        TargetKind = "GeometryBinding",
                        TargetPath = "bodyPresence",
                        TargetField = "set"
                    });
                    for (int i = 0; i < (geometryBinding.BodyPresenceVolumes?.Count ?? 0); i++)
                    {
                        CharacterBodyPresenceVolumeBinding volume =
                            geometryBinding.BodyPresenceVolumes[i];
                        mappings.Add(new CharacterPackageSourceMapping
                        {
                            SourcePath = volume.SourcePath,
                            SourceObjectPath = volume.SourceObjectPath,
                            TargetKind = "GeometryBinding",
                            TargetPath = "bodyPresenceVolumes/" + volume.VolumeId,
                            TargetField = "volume"
                        });
                    }
                    for (int i = 0; i < (geometryBinding.AnimatedHurtboxSources?.Count ?? 0); i++)
                    {
                        CharacterBodyPresenceAnimatedHurtboxSourceBinding source =
                            geometryBinding.AnimatedHurtboxSources[i];
                        mappings.Add(new CharacterPackageSourceMapping
                        {
                            SourcePath = "geometry/body_presence.json",
                            SourceObjectPath = source.SourceObjectPath,
                            TargetKind = "GeometryBinding",
                            TargetPath = "animatedHurtboxSources/" + source.SourceId,
                            TargetField = "source"
                        });
                    }
                }

                if (geometryBinding.BodyPartDamageMode == CharacterBodyPartDamageBindingMode.Authored)
                {
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = "geometry/body_part_damage.json",
                        SourceObjectPath = "bodyPartDamage",
                        TargetKind = "GeometryBinding",
                        TargetPath = "bodyPartDamage",
                        TargetField = "set"
                    });
                    for (int i = 0; i < (geometryBinding.BodyPartDamageDefinitions?.Count ?? 0); i++)
                    {
                        CharacterBodyPartDamageBinding definition =
                            geometryBinding.BodyPartDamageDefinitions[i];
                        mappings.Add(new CharacterPackageSourceMapping
                        {
                            SourcePath = definition.SourcePath,
                            SourceObjectPath = definition.SourceObjectPath,
                            TargetKind = "GeometryBinding",
                            TargetPath = "bodyPartDamageDefinitions/"
                                + definition.DefinitionIndex.ToString(CultureInfo.InvariantCulture),
                            TargetField = "definition"
                        });
                    }
                    for (int i = 0; i < (geometryBinding.BodyPartDamagePartLookup?.Count ?? 0); i++)
                    {
                        CharacterBodyPartDamageLookupBinding lookup =
                            geometryBinding.BodyPartDamagePartLookup[i];
                        mappings.Add(new CharacterPackageSourceMapping
                        {
                            SourcePath = lookup.SourcePath,
                            SourceObjectPath = lookup.SourceObjectPath,
                            TargetKind = "GeometryBinding",
                            TargetPath = "bodyPartDamagePartLookup/" + (!string.IsNullOrEmpty(lookup.PartLabel) ? lookup.PartLabel : lookup.PartId.ToString()),
                            TargetField = "definitionIndex"
                        });
                    }
                }

                AddAuthorityPoseRigSourceMappings(mappings, geometryBinding.AuthorityPoseRig);

                for (int i = 0; i < geometryBinding.Sockets.Count; i++)
                {
                    CharacterSocketBinding socket = geometryBinding.Sockets[i];
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = socket.SourcePath,
                        SourceObjectPath = socket.SourceObjectPath,
                        TargetKind = "GeometryBinding",
                        TargetPath = "sockets/" + (!string.IsNullOrEmpty(socket.SocketLabel) ? socket.SocketLabel : socket.SocketId.ToString()),
                        TargetField = "socket"
                    });
                }

                for (int i = 0; i < geometryBinding.WeaponAttachments.Count; i++)
                {
                    CharacterWeaponAttachmentBinding attachment = geometryBinding.WeaponAttachments[i];
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = attachment.SourcePath,
                        SourceObjectPath = attachment.SourceObjectPath,
                        TargetKind = "GeometryBinding",
                        TargetPath = "weaponAttachments/" + attachment.AttachmentId,
                        TargetField = "attachment"
                    });
                }

                for (int i = 0; i < geometryBinding.WeaponTraces.Count; i++)
                {
                    CharacterWeaponTraceBinding trace = geometryBinding.WeaponTraces[i];
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = trace.SourcePath,
                        SourceObjectPath = trace.SourceObjectPath,
                        TargetKind = "GeometryBinding",
                        TargetPath = "weaponTraces/" + (!string.IsNullOrEmpty(trace.TraceLabel) ? trace.TraceLabel : trace.TraceId.ToString()),
                        TargetField = "trace"
                    });
                }
            }

            if (resourceMapping != null)
            {
                for (int i = 0; i < resourceMapping.Entries.Count; i++)
                {
                    CharacterPackageResourceMappingEntry entry = resourceMapping.Entries[i];
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = entry.SourceRelativePath,
                        SourceObjectPath = entry.PackageResourceKey,
                        TargetKind = "UnityImportTarget",
                        TargetPath = entry.ImportTargetPath,
                        TargetField = "ResourceKey"
                    });
                }
            }

            return mappings;
        }

        private static CharacterCompilerHashSet BuildHashes(
            CharacterResourcePackage package,
            CharacterAuthoringCompiledConfigPatch configPatch,
            CharacterAuthoringGeometryBinding geometryBinding,
            CharacterPackageResourceMapping resourceMapping,
            CharacterUnityImportWritePlan writePlan,
            CharacterContentIdentityCatalogSnapshot catalog)
        {
            return new CharacterCompilerHashSet
            {
                SourcePackageHash = CharacterPackageHashUtility.ComputeTextSha256(BuildSourcePackageCanonicalText(package, catalog)),
                GeneratedConfigHash = CharacterPackageHashUtility.ComputeTextSha256(BuildConfigPatchCanonicalText(configPatch)),
                GeometryBindingHash = CharacterPackageHashUtility.ComputeTextSha256(BuildGeometryBindingCanonicalText(geometryBinding)),
                ResourceMappingHash = CharacterPackageHashUtility.ComputeTextSha256(BuildResourceMappingCanonicalText(resourceMapping)),
                WritePlanHash = CharacterPackageHashUtility.ComputeTextSha256(BuildWritePlanCanonicalText(writePlan))
            };
        }

        private static void AddCoordinateDiagnostics(CharacterResourcePackage package, CharacterAuthoringCompileOptions options, List<CharacterAuthoringValidationIssue> issues)
        {
            if (package == null || package.Manifest == null)
                return;

            CharacterCoordinateConversionPlan plan = BuildCoordinatePlan(package.Manifest.CoordinateConvention, options.TargetCoordinateConvention);
            if (!plan.RequiresConversion)
                return;

            issues.Add(new CharacterAuthoringValidationIssue
            {
                Severity = CharacterAuthoringValidationSeverity.Warning,
                Gate = CharacterAuthoringValidationGate.WarningOnly,
                Code = CharacterAuthoringCompilerValidationCodes.CoordinateTargetMismatch,
                SourcePath = "manifest.json",
                SourceObjectPath = "manifest.coordinateConvention",
                Field = "coordinateConvention",
                Message = "package coordinate convention differs from Unity target convention; compiler emitted a deterministic conversion plan.",
                SuggestedFix = "Review axis, handedness and unit scale before import."
            });
        }

        private static void AddResourceConflictDiagnostics(CharacterResourcePackage package, CharacterPackageResourceCatalog projectCatalog, List<CharacterAuthoringValidationIssue> issues)
        {
            if (package == null || package.ResourceCatalog == null || projectCatalog == null)
                return;

            for (int i = 0; i < package.ResourceCatalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = package.ResourceCatalog.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ResourceKey))
                    continue;

                CharacterPackageResourceEntry existing = CharacterPackageResourcePipeline.FindByKey(projectCatalog, entry.ResourceKey);
                if (existing == null)
                    continue;

                string incomingHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(entry);
                string existingHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(existing);
                if (string.IsNullOrWhiteSpace(incomingHash) || string.Equals(incomingHash, existingHash, StringComparison.OrdinalIgnoreCase))
                    continue;

                issues.Add(new CharacterAuthoringValidationIssue
                {
                    Severity = CharacterAuthoringValidationSeverity.Error,
                    Gate = CharacterAuthoringValidationGate.ImportBlocked,
                    Code = CharacterAuthoringCompilerValidationCodes.ResourceKeyConflict,
                    SourcePath = "resource_catalog.json",
                    SourceObjectPath = "resourceCatalog/entries/" + i.ToString(CultureInfo.InvariantCulture),
                    Field = "resourceKey",
                    Message = "package ResourceKey already exists in the target project catalog with a different content hash.",
                    SuggestedFix = "Rename the package resource key, choose a variant, or explicitly migrate the existing project resource."
                });
            }
        }

        /// <summary>
        /// Surface Palette usage 的模型版本一致性门禁。
        ///
        /// 判定规则集中在 <see cref="CharacterPaletteUsageAuthority"/>，编译期只负责把它接到 GateReport 上。
        /// 首轮以 Warning 级别输出（WarningOnly），不设置 ImportBlocked：仓库中已存在需要人工重新扫描的
        /// 历史数据（character-000007），在数据修复完成前直接阻断会挡住所有人的导入流程。
        /// 注意本方法必须早于 AddStrictDiagnostics 调用，否则 --strict / --no-warnings 无法把它升级为阻断。
        /// </summary>
        private static void AddPaletteUsageDiagnostics(
            CharacterResourcePackage package,
            string packageRootPath,
            List<CharacterAuthoringValidationIssue> issues)
        {
            if (issues == null || package == null || string.IsNullOrWhiteSpace(packageRootPath))
                return;

            List<CharacterPaletteUsageGateIssue> paletteIssues = CharacterPaletteUsageAuthority.EvaluatePaletteUsage(package, packageRootPath);
            for (int i = 0; i < paletteIssues.Count; i++)
            {
                CharacterPaletteUsageGateIssue issue = paletteIssues[i];
                if (issue == null)
                    continue;

                issues.Add(new CharacterAuthoringValidationIssue
                {
                    Severity = CharacterAuthoringValidationSeverity.Warning,
                    Gate = CharacterAuthoringValidationGate.WarningOnly,
                    Code = issue.Code ?? string.Empty,
                    SourcePath = string.IsNullOrWhiteSpace(issue.SourcePath)
                        ? CharacterPaletteUsageAuthority.UsageRelativePath
                        : issue.SourcePath,
                    SourceObjectPath = "modelHash",
                    Field = string.IsNullOrWhiteSpace(issue.Field) ? "modelHash" : issue.Field,
                    Message = issue.Message ?? string.Empty,
                    SuggestedFix = "Reopen the body model preview in CharacterStudio and save the palette usage again so that "
                        + "tokenUsages and modelHash come from the same model version. The authoring service cannot recompute face counts."
                });
            }
        }

        private static void AddStrictDiagnostics(CharacterAuthoringCompileOptions options, List<CharacterAuthoringValidationIssue> issues)
        {
            if (options == null || (!options.Strict && options.AllowWarnings))
                return;

            bool hasWarning = false;
            for (int i = 0; i < issues.Count; i++)
            {
                CharacterAuthoringValidationIssue issue = issues[i];
                if (issue != null && (issue.Severity == CharacterAuthoringValidationSeverity.Warning || issue.Gate == CharacterAuthoringValidationGate.WarningOnly))
                {
                    hasWarning = true;
                    break;
                }
            }

            if (!hasWarning)
                return;

            issues.Add(new CharacterAuthoringValidationIssue
            {
                Severity = CharacterAuthoringValidationSeverity.Error,
                Gate = CharacterAuthoringValidationGate.ImportBlocked,
                Code = CharacterAuthoringCompilerValidationCodes.StrictWarningBlocked,
                SourcePath = "compiler/options",
                SourceObjectPath = "options",
                Field = options.Strict ? "strict" : "allowWarnings",
                Message = "compiler options do not allow warning-only packages to continue to Unity import.",
                SuggestedFix = "Fix warnings or compile with warnings allowed."
            });
        }

        private static CharacterCoordinateConversionPlan BuildCoordinatePlan(CharacterPackageCoordinateConvention source, CharacterPackageCoordinateConvention target)
        {
            if (source == null)
                source = new CharacterPackageCoordinateConvention();
            if (target == null)
                target = new CharacterPackageCoordinateConvention();

            bool requiresConversion =
                source.UpAxis != target.UpAxis ||
                source.ForwardAxis != target.ForwardAxis ||
                source.Handedness != target.Handedness ||
                Math.Abs(source.UnitScaleMeters - target.UnitScaleMeters) > 0.000001f;

            return new CharacterCoordinateConversionPlan
            {
                SourceConvention = source,
                TargetConvention = target,
                RequiresConversion = requiresConversion,
                PositionScale = target.UnitScaleMeters <= 0f ? 1f : source.UnitScaleMeters / target.UnitScaleMeters,
                AxisConversion = requiresConversion ? source.UpAxis + "/" + source.ForwardAxis + "/" + source.Handedness + " -> " + target.UpAxis + "/" + target.ForwardAxis + "/" + target.Handedness : "identity",
                RotationConversion = requiresConversion ? "quaternion-axis-remap" : "identity"
            };
        }

        private static void AddEntry(CharacterAuthoringCompiledConfigPatch compiled, string tableName, int id, Dictionary<string, FieldValue> fields)
        {
            compiled.Patch.Entries.Add(new PatchEntry
            {
                Operation = PatchOperation.Upsert,
                Source = tableName,
                Id = id.ToString(CultureInfo.InvariantCulture),
                Layer = "Generated",
                Fields = fields ?? new Dictionary<string, FieldValue>()
            });
        }

        private static void AddReference(CharacterAuthoringCompiledConfigPatch compiled, string tableName, int id, string stableId, string sourcePath, string sourceObjectPath)
        {
            compiled.GeneratedReferences.Add(new CharacterGeneratedConfigReference
            {
                TableName = tableName,
                GeneratedId = id,
                StableId = stableId ?? string.Empty,
                SourcePath = sourcePath ?? string.Empty,
                SourceObjectPath = sourceObjectPath ?? string.Empty
            });
        }

        private static FieldValue BuildSocketField(CharacterAuthoringGeometryBinding geometryBinding)
        {
            var values = new List<FieldValue>();
            if (geometryBinding != null)
            {
                for (int i = 0; i < geometryBinding.Sockets.Count; i++)
                {
                    CharacterSocketBinding socket = geometryBinding.Sockets[i];
                    if (socket == null)
                        continue;

                    values.Add(Map(
                        ("SocketId", NumericIdentity(socket.SocketId)),
                        ("ParentPartId", NumericIdentity(socket.ParentPartId)),
                        ("BonePath", Str(socket.BonePath)),
                        ("LocatorId", Str(!string.IsNullOrWhiteSpace(socket.LocatorPath) ? socket.LocatorPath : socket.SocketLabel)),
                        ("Usage", Str(socket.Usage.ToString())),
                        ("MirrorPairSocketId", NumericIdentity(socket.MirrorPairSocketId)),
                        ("Handedness", Str(socket.Handedness.ToString())),
                        ("SideTag", Str(socket.SideTag.ToString())),
                        ("Tags", List(socket.Tags != null ? socket.Tags.ToArray() : Array.Empty<string>())),
                        ("SocketLabel", Str(socket.SocketLabel)),
                        ("ParentPartLabel", Str(socket.ParentPartLabel)),
                        ("MirrorPairSocketLabel", Str(socket.MirrorPairSocketLabel))));
                }
            }

            return FieldValue.FromList(values);
        }

        private static FieldValue BuildHitZoneBindingField(CharacterAuthoringGeometryBinding geometryBinding)
        {
            var values = new List<FieldValue>();
            if (geometryBinding != null)
            {
                for (int i = 0; i < geometryBinding.HitZoneBindings.Count; i++)
                {
                    CharacterHitZoneBinding hitZone = geometryBinding.HitZoneBindings[i];
                    if (hitZone == null || hitZone.HitZoneId <= 0)
                        continue;

                    values.Add(Map(
                        ("HitZoneId", NumericIdentity(hitZone.HitZoneId)),
                        ("PartId", NumericIdentity(hitZone.PartId)),
                        ("Priority", Int(hitZone.Priority)),
                        ("IsWeakPoint", Bool(hitZone.IsWeakPoint)),
                        ("DamageMultiplierOverride", Float(hitZone.DamageMultiplierOverride)),
                        ("ImpactPowerScaleOverride", Float(hitZone.ImpactPowerScaleOverride)),
                        ("HitZoneLabel", Str(hitZone.HitZoneLabel)),
                        ("PartLabel", Str(hitZone.PartLabel))));
                }
            }

            return FieldValue.FromList(values);
        }

        private static FieldValue BuildEquipmentSlotsField(
            CharacterAuthoringGeometry geometry,
            CharacterContentIdentityResolver identityResolver,
            List<CharacterAuthoringValidationIssue> issues)
        {
            var values = new List<FieldValue>();
            List<string> slots = GetDistinctSlots(geometry);
            for (int i = 0; i < slots.Count; i++)
            {
                string slot = slots[i];
                int slotId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Slot,
                    slot,
                    "config/character_application.json",
                    "equipmentSchema/slots/" + slot,
                    "slotId",
                    issues,
                    required: true);
                values.Add(Map(
                    ("SlotId", NumericIdentity(slotId)),
                    ("Kind", Str(MapSlotKind(slot))),
                    ("DisplayName", Str(ToDisplayName(slot))),
                    ("AllowedWeaponCategories", SlotAllowedCategories(slot)),
                    ("RequiredTags", FieldValue.FromList(Array.Empty<FieldValue>())),
                    ("SlotLabel", Str(slot))));
            }

            return FieldValue.FromList(values);
        }

        private static FieldValue BuildSlotIdList(
            CharacterAuthoringGeometry geometry,
            CharacterContentIdentityResolver identityResolver,
            List<CharacterAuthoringValidationIssue> issues)
        {
            List<string> slots = GetDistinctSlots(geometry);
            var values = new List<FieldValue>();
            for (int i = 0; i < slots.Count; i++)
            {
                values.Add(NumericIdentity(identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Slot,
                    slots[i],
                    "config/character_application.json",
                    "equipmentSchema/exclusiveGroups/hands",
                    "slotIds",
                    issues,
                    required: true)));
            }
            return FieldValue.FromList(values);
        }

        private static FieldValue BuildGameplayCameraProfileField(CharacterGameplayCameraProfileAuthoring profile)
        {
            CharacterAuthoringVector3 focusOffset = profile.FocusOffset ?? new CharacterAuthoringVector3();
            CharacterAuthoringVector3 boundsCenter = profile.FramingBoundsCenter ?? new CharacterAuthoringVector3();
            CharacterAuthoringVector3 boundsExtents = profile.FramingBoundsExtents ?? new CharacterAuthoringVector3();
            CharacterGameplayCameraNormalAuthoring normal = profile.Normal ?? new CharacterGameplayCameraNormalAuthoring();
            CharacterGameplayCameraLockedAuthoring locked = profile.Locked ?? new CharacterGameplayCameraLockedAuthoring();
            CharacterGameplayCameraObstructionAuthoring obstruction = profile.Obstruction ?? new CharacterGameplayCameraObstructionAuthoring();
            return Map(
                ("ProfileId", Str(profile.ProfileId)),
                ("FocusAbstractBoneId", Str(profile.FocusAbstractBoneId)),
                ("FocusOffset", BuildGameplayCameraVectorField(focusOffset)),
                ("FramingBoundsCenter", BuildGameplayCameraVectorField(boundsCenter)),
                ("FramingBoundsExtents", BuildGameplayCameraVectorField(boundsExtents)),
                ("Normal", Map(
                    ("Distance", Float(normal.Distance)),
                    ("MinDistance", Float(normal.MinDistance)),
                    ("MaxDistance", Float(normal.MaxDistance)),
                    ("FieldOfView", Float(normal.FieldOfView)),
                    ("Pitch", Float(normal.Pitch)),
                    ("YawOffset", Float(normal.YawOffset)),
                    ("TargetPadding", Float(normal.TargetPadding)))),
                ("Locked", Map(
                    ("MinDistance", Float(locked.MinDistance)),
                    ("MaxDistance", Float(locked.MaxDistance)),
                    ("TargetScreenY", Float(locked.TargetScreenY)),
                    ("TargetPadding", Float(locked.TargetPadding)),
                    ("FramingScale", Float(locked.FramingScale)))),
                ("Obstruction", Map(
                    ("Radius", Float(obstruction.Radius)),
                    ("Padding", Float(obstruction.Padding)),
                    ("MinDistance", Float(obstruction.MinDistance)),
                    ("FocusClearance", Float(obstruction.FocusClearance)))));
        }

        private static FieldValue BuildGameplayCameraVectorField(CharacterAuthoringVector3 value)
        {
            return Map(("X", Float(value.X)), ("Y", Float(value.Y)), ("Z", Float(value.Z)));
        }

        private static FieldValue BuildPresentationImagesField(MxFramework.CharacterApplication.CharacterPresentationImages images)
        {
            var variants = new List<FieldValue>();
            for (int i = 0; images.Variants != null && i < images.Variants.Count; i++)
            {
                var variant = images.Variants[i];
                if (variant != null) variants.Add(Map(("VariantId", Str(variant.VariantId)),
                    ("PortraitResourceKey", Str(variant.PortraitResourceKey)), ("StandingResourceKey", Str(variant.StandingResourceKey))));
            }
            return Map(("SchemaVersion", Str(images.SchemaVersion)), ("PortraitResourceKey", Str(images.PortraitResourceKey)),
                ("StandingResourceKey", Str(images.StandingResourceKey)), ("Variants", FieldValue.FromList(variants)));
        }

        private static FieldValue BuildPresentationResourceKeys(CharacterApplicationAuthoringSummary applicationConfig, CharacterPackageResourceMapping mapping)
        {
            var values = new List<FieldValue>();
            if (mapping != null)
            {
                List<CharacterPackageResourceMappingEntry> referencedEntries = GetReferencedResourceMappingEntries(applicationConfig, mapping);
                for (int i = 0; i < referencedEntries.Count; i++)
                {
                    CharacterPackageResourceMappingEntry entry = referencedEntries[i];
                    values.Add(Map(
                        ("Id", Str(entry.ProjectResourceKey)),
                        ("TypeId", Str(MapResourceType(entry))),
                        ("UsageKind", Str(MapResourceUsageKind(entry))),
                        ("Variant", Str(string.Empty)),
                        ("PackageId", Str(mapping.PackageId)),
                        ("PreloadGroupId", Str("character." + NormalizeSegment(mapping.PackageId)))));
                }
            }

            return FieldValue.FromList(values);
        }

        private static List<CharacterPackageResourceMappingEntry> GetReferencedResourceMappingEntries(
            CharacterApplicationAuthoringSummary applicationConfig,
            CharacterPackageResourceMapping mapping)
        {
            var result = new List<CharacterPackageResourceMappingEntry>();
            if (mapping == null || mapping.Entries == null)
                return result;

            List<string> resourceKeys = applicationConfig?.ResourceKeys == null ? null : new List<string>(applicationConfig.ResourceKeys);
            // Explicit image slots participate even when the model resource list is restricted.
            if (resourceKeys != null && resourceKeys.Count > 0 && applicationConfig.PresentationImages != null)
            {
                var images = applicationConfig.PresentationImages;
                resourceKeys.Add(images.PortraitResourceKey);
                resourceKeys.Add(images.StandingResourceKey);
                for (int i = 0; images.Variants != null && i < images.Variants.Count; i++)
                {
                    var variant = images.Variants[i];
                    if (variant != null) { resourceKeys.Add(variant.PortraitResourceKey); resourceKeys.Add(variant.StandingResourceKey); }
                }
                resourceKeys.RemoveAll(string.IsNullOrWhiteSpace);
            }
            if (resourceKeys == null || resourceKeys.Count == 0)
            {
                for (int i = 0; i < mapping.Entries.Count; i++)
                {
                    if (mapping.Entries[i] != null)
                        result.Add(mapping.Entries[i]);
                }
                return result;
            }

            var byPackageKey = new Dictionary<string, CharacterPackageResourceMappingEntry>(StringComparer.Ordinal);
            for (int i = 0; i < mapping.Entries.Count; i++)
            {
                CharacterPackageResourceMappingEntry entry = mapping.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.PackageResourceKey))
                    continue;
                if (!byPackageKey.ContainsKey(entry.PackageResourceKey))
                    byPackageKey.Add(entry.PackageResourceKey, entry);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < resourceKeys.Count; i++)
            {
                string key = resourceKeys[i];
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (!byPackageKey.TryGetValue(key, out CharacterPackageResourceMappingEntry entry))
                    continue;

                string identity = string.IsNullOrWhiteSpace(entry.ProjectResourceKey) ? entry.PackageResourceKey : entry.ProjectResourceKey;
                if (seen.Add(identity))
                    result.Add(entry);
            }

            return result;
        }

        private static FieldValue BuildWeaponResourceKeys(SingleWeaponConfiguration definition, CharacterPackageResourceMapping resourceMapping)
        {
            var values = new List<FieldValue>();
            string key = GetWeaponRuntimeResourceKey(definition);
            if (!string.IsNullOrWhiteSpace(key))
            {
                string packageId = GetWeaponResourcePackageId(definition, resourceMapping);
                values.Add(Map(
                    ("Id", Str(key)),
                    ("TypeId", Str("GameObject")),
                    ("UsageKind", Str("WeaponModel")),
                    ("Variant", Str(string.Empty)),
                    ("PackageId", Str(packageId)),
                    ("PreloadGroupId", Str("character." + NormalizeSegment(resourceMapping != null ? resourceMapping.PackageId : string.Empty)))));
            }

            return FieldValue.FromList(values);
        }

        private static string GetWeaponResourcePackageId(SingleWeaponConfiguration definition, CharacterPackageResourceMapping resourceMapping)
        {
            if (definition == null)
                return string.Empty;

            if (definition.WeaponRef != null
                && !string.IsNullOrWhiteSpace(definition.WeaponRef.RuntimeResourceKey)
                && string.IsNullOrWhiteSpace(definition.WeaponRef.PackageResourceKey))
            {
                return string.Empty;
            }

            return resourceMapping != null ? resourceMapping.PackageId : string.Empty;
        }

        private static FieldValue BuildWeaponTraceBindings(
            CharacterAuthoringGeometry geometry,
            SingleWeaponConfiguration definition,
            List<WeaponAttachmentProfile> attachments,
            CharacterContentIdentityResolver identityResolver,
            List<CharacterAuthoringValidationIssue> issues)
        {
            var values = new List<FieldValue>();
            if (definition == null)
                return FieldValue.FromList(values);

            for (int attachmentIndex = 0; attachments != null && attachmentIndex < attachments.Count; attachmentIndex++)
            {
                WeaponAttachmentProfile attachment = attachments[attachmentIndex];
                if (attachment == null || !string.Equals(
                    attachment.SingleWeaponConfigurationId,
                    definition.SingleWeaponConfigurationId,
                    StringComparison.Ordinal))
                    continue;
                for (int i = 0; definition.TraceProfiles != null && i < definition.TraceProfiles.Count; i++)
                {
                    SingleWeaponTraceProfile trace = definition.TraceProfiles[i];
                    if (trace == null)
                        continue;
                    string startSocketId = attachment.AttachSocketId;
                    float radius = trace.Radius;
                    int slotId = identityResolver.ResolveId(
                        CharacterContentIdentityDomains.Slot,
                        attachment.SlotId,
                        "config/character_application.json",
                        "combinedWeaponConfigurationBindings/derivedAttachments/" + attachment.AttachmentId,
                        "slotId",
                        issues,
                        required: true);
                    int socketId = identityResolver.ResolveId(
                        CharacterContentIdentityDomains.Pose,
                        startSocketId,
                        "config/character_application.json",
                        "combinedWeaponConfigurationBindings/derivedAttachments/" + attachment.AttachmentId,
                        "socketId",
                        issues,
                        required: true);
                    values.Add(Map(
                        ("TraceProfileId", Str(trace.TraceId)),
                        ("SlotId", NumericIdentity(slotId)),
                        ("SocketId", NumericIdentity(socketId)),
                        ("EndSocketId", NumericIdentity(0)),
                        ("Length", Float(ComputeTraceLength(trace))),
                        ("Radius", Float(radius)),
                        ("ProximalExtension", Float(trace.ProximalExtension)),
                        ("ProximalRadius", Float(trace.ProximalRadius)),
                        ("SampleRule", Str(trace.SampleRule.ToString())),
                        ("SlotLabel", Str(attachment.SlotId)),
                        ("SocketLabel", Str(startSocketId)),
                        ("EndSocketLabel", Str(string.Empty))));
                }
            }

            return FieldValue.FromList(values);
        }

        private static FieldValue BuildLoadoutSlots(
            string packageId,
            CombinedWeaponConfigurationAuthoringSummary configuration,
            Dictionary<string, int> weaponIds,
            CharacterContentIdentityResolver identityResolver,
            List<CharacterAuthoringValidationIssue> issues)
        {
            var values = new List<FieldValue>();
            List<CombinedWeaponConfigurationSlotAuthoringSummary> slots = configuration?.Slots ??
                new List<CombinedWeaponConfigurationSlotAuthoringSummary>();
            var sorted = new List<CombinedWeaponConfigurationSlotAuthoringSummary>(slots);
            sorted.Sort((left, right) => string.CompareOrdinal(left?.SlotId, right?.SlotId));
            for (int i = 0; i < sorted.Count; i++)
            {
                CombinedWeaponConfigurationSlotAuthoringSummary slot = sorted[i];
                if (slot == null || GlobalWeaponLibrarySchema.IsUnarmed(slot.SingleWeaponConfigurationId))
                    continue;
                int weaponId;
                weaponIds.TryGetValue(slot.SingleWeaponConfigurationId, out weaponId);
                int slotId = identityResolver.ResolveId(
                    CharacterContentIdentityDomains.Slot,
                    slot.SlotId,
                    "config/character_application.json",
                    "combinedWeaponConfigurationBindings/" + configuration.CombinedWeaponConfigurationId + "/slots/" + slot.SlotId,
                    "slotId",
                    issues,
                    required: true);
                values.Add(Map(
                    ("SlotId", NumericIdentity(slotId)),
                    ("WeaponId", Int(weaponId)),
                    ("AttachmentId", Str(BuildAttachmentStableId(packageId, configuration.CombinedWeaponConfigurationId, slot.SlotId, slot.SingleWeaponConfigurationId))),
                    ("WeaponInstanceStableId", Str("weapon.instance." +
                        NormalizeSegment(ToCharacterCombinationIdentity(packageId, configuration.CombinedWeaponConfigurationId)) + "." +
                        NormalizeSegment(slot.SlotId) + "." +
                        NormalizeSegment(slot.SingleWeaponConfigurationId))),
                    ("SlotLabel", Str(slot.SlotId))));
            }

            return FieldValue.FromList(values);
        }

        private static FieldValue BuildMatchRule(List<WeaponAttachmentProfile> loadoutAttachments, List<WeaponAttachmentProfile> allAttachments)
        {
            var filled = new List<FieldValue>();
            var empty = new List<FieldValue>();
            var categories = new List<FieldValue>();
            var tags = new List<FieldValue>();
            var filledSlots = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < loadoutAttachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = loadoutAttachments[i];
                filled.Add(Str(attachment.EquipSlot));
                filledSlots.Add(attachment.EquipSlot);
                categories.Add(Str(attachment.EquipSlot + ":" + MapWeaponCategory(attachment)));
                tags.Add(Str(attachment.EquipSlot + ":" + GetPrimaryWeaponTag(attachment)));
            }

            for (int i = 0; i < allAttachments.Count; i++)
            {
                string slot = allAttachments[i].EquipSlot;
                if (!filledSlots.Contains(slot))
                    empty.Add(Str(slot));
            }

            return Map(
                ("RequiredFilledSlots", FieldValue.FromList(filled)),
                ("RequiredEmptySlots", FieldValue.FromList(empty)),
                ("RequiredWeaponCategoriesBySlot", FieldValue.FromList(categories)),
                ("RequiredWeaponTagsBySlot", FieldValue.FromList(tags)),
                ("ForbiddenWeaponTagsBySlot", FieldValue.FromList(Array.Empty<FieldValue>())));
        }

        private static FieldValue BuildLoadoutTags(string stableId, List<WeaponAttachmentProfile> attachments, GlobalWeaponLibrary library)
        {
            var tags = new List<FieldValue> { Str(NormalizeSegment(stableId)) };
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < attachments.Count; i++)
            {
                var definition = library?.SingleWeaponConfigurations?.Find(d => d != null
                    && d.SingleWeaponConfigurationId == attachments[i].SingleWeaponConfigurationId);
                // State tags reflect declared source facts. Numeric IDs and hand slots cannot imply a blade.
                string tag = definition?.Tags?.Find(t => t == "blade" || t == "shield" || t == "natural" || t == "ranged");
                if (!string.IsNullOrEmpty(tag) && emitted.Add(tag)) tags.Add(Str(tag));
            }
            return FieldValue.FromList(tags);
        }

        private static FieldValue BuildWeaponTags(WeaponAttachmentProfile attachment)
        {
            string tag = GetPrimaryWeaponTag(attachment);
            string handed = string.Equals(attachment != null ? attachment.EquipSlot : string.Empty, "twoHand", StringComparison.OrdinalIgnoreCase) ? "two_hand" : "one_hand";
            return List(tag, handed);
        }

        private static FieldValue BuildCombatActions(
            CharacterAuthoringGeometry geometry,
            List<WeaponAttachmentProfile> attachments,
            CombinedWeaponConfigurationAuthoringSummary configuration,
            int index)
        {
            var actions = new List<FieldValue>();
            List<CombinedWeaponActionEntry> authored = configuration?.ActionConfiguration?.Actions;
            if (authored != null && authored.Count > 0)
            {
                var sorted = new List<CombinedWeaponActionEntry>(authored);
                sorted.Sort((left, right) => string.CompareOrdinal(left?.ActionRole, right?.ActionRole));
                for (int i = 0; i < sorted.Count; i++)
                {
                    CombinedWeaponActionEntry action = sorted[i] ?? new CombinedWeaponActionEntry();
                    actions.Add(Map(
                        ("ActionKey", Str(action.ActionRole)),
                        ("CombatActionId", Int(910001 + index * 100 + i)),
                        ("TraceProfileIdOverride", Str(string.Empty)),
                        ("AnimationActionKey", Str(!string.IsNullOrWhiteSpace(action.AnimationClipId)
                            ? action.AnimationClipId : action.ActionRole))));
                }
                return FieldValue.FromList(actions);
            }
            if (attachments.Count == 0)
            {
                actions.Add(Map(("ActionKey", Str("primary")), ("CombatActionId", Int(910001 + index * 10)), ("TraceProfileIdOverride", Str(string.Empty)), ("AnimationActionKey", Str("punch"))));
                return FieldValue.FromList(actions);
            }

            for (int i = 0; i < attachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = attachments[i];
                bool shield = string.Equals(MapWeaponCategory(attachment), "Shield", StringComparison.Ordinal);
                string actionKey = shield ? "guard" : "primary";
                string animation = shield ? "shield_guard" : "slash";
                actions.Add(Map(
                    ("ActionKey", Str(actionKey)),
                    ("CombatActionId", Int(910001 + index * 10 + i)),
                    ("TraceProfileIdOverride", Str(string.Empty)),
                    ("AnimationActionKey", Str(animation))));
            }

            return FieldValue.FromList(actions);
        }

        private static void SetCharacterDefaultLoadout(CharacterAuthoringCompiledConfigPatch compiled, int characterId, int loadoutId, string stableId)
        {
            for (int i = 0; i < compiled.Patch.Entries.Count; i++)
            {
                PatchEntry entry = compiled.Patch.Entries[i];
                if (entry.Source == CharacterApplicationCompilerTableNames.CharacterConfig && entry.Id == characterId.ToString(CultureInfo.InvariantCulture))
                    entry.Fields["DefaultLoadoutId"] = Int(loadoutId);
                else if (entry.Source == CharacterApplicationCompilerTableNames.SpawnProfileConfig)
                    entry.Fields["EquipmentLoadoutId"] = Int(loadoutId);
            }
        }

        private static string FindStableId(CharacterAuthoringCompiledConfigPatch configPatch, string tableName, string id)
        {
            if (string.IsNullOrWhiteSpace(id) || configPatch == null)
                return string.Empty;

            int parsed;
            if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return string.Empty;

            for (int i = 0; i < configPatch.GeneratedReferences.Count; i++)
            {
                CharacterGeneratedConfigReference reference = configPatch.GeneratedReferences[i];
                if (reference.TableName == tableName && reference.GeneratedId == parsed)
                    return reference.StableId;
            }

            return string.Empty;
        }

        private static List<WeaponAttachmentProfile> GetSortedAttachments(CharacterAuthoringGeometry geometry)
        {
            var attachments = new List<WeaponAttachmentProfile>();
            if (geometry != null && geometry.WeaponAttachments != null)
                attachments.AddRange(geometry.WeaponAttachments);
            attachments.Sort((a, b) => string.CompareOrdinal(a != null ? a.AttachmentId : string.Empty, b != null ? b.AttachmentId : string.Empty));
            return attachments;
        }

        private static List<WeaponAttachmentProfile> BuildSyntheticAttachments(CharacterResourcePackage package)
        {
            var result = new List<WeaponAttachmentProfile>();
            CharacterAuthoringGeometry geometry = package?.Geometry;
            var slots = new Dictionary<string, CharacterWeaponSlotProfile>(StringComparer.Ordinal);
            for (int i = 0; geometry?.WeaponSlots != null && i < geometry.WeaponSlots.Count; i++)
            {
                CharacterWeaponSlotProfile slot = geometry.WeaponSlots[i];
                if (slot != null && !string.IsNullOrWhiteSpace(slot.SlotId) && !slots.ContainsKey(slot.SlotId))
                    slots.Add(slot.SlotId, slot);
            }
            var definitions = new Dictionary<string, SingleWeaponConfiguration>(StringComparer.Ordinal);
            for (int i = 0; package?.GlobalWeaponLibrary?.SingleWeaponConfigurations != null && i < package.GlobalWeaponLibrary.SingleWeaponConfigurations.Count; i++)
            {
                SingleWeaponConfiguration definition = package.GlobalWeaponLibrary.SingleWeaponConfigurations[i];
                if (definition != null && !string.IsNullOrWhiteSpace(definition.SingleWeaponConfigurationId))
                    definitions[definition.SingleWeaponConfigurationId] = definition;
            }
            var identities = new HashSet<string>(StringComparer.Ordinal);
            string packageId = package?.Manifest?.PackageId ?? "character";
            List<CombinedWeaponConfigurationAuthoringSummary> configurations = package?.ApplicationConfig?.CombinedWeaponConfigurations;
            for (int configurationIndex = 0; configurations != null && configurationIndex < configurations.Count; configurationIndex++)
            {
                List<CombinedWeaponConfigurationSlotAuthoringSummary> configuredSlots = configurations[configurationIndex]?.Slots;
                for (int slotIndex = 0; configuredSlots != null && slotIndex < configuredSlots.Count; slotIndex++)
                {
                    CombinedWeaponConfigurationSlotAuthoringSummary configured = configuredSlots[slotIndex];
                    if (configured == null || GlobalWeaponLibrarySchema.IsUnarmed(configured.SingleWeaponConfigurationId)) continue;
                    string identity = BuildAttachmentStableId(
                        packageId,
                        configurations[configurationIndex].CombinedWeaponConfigurationId,
                        configured.SlotId,
                        configured.SingleWeaponConfigurationId);
                    if (!identities.Add(identity) || !slots.TryGetValue(configured.SlotId, out CharacterWeaponSlotProfile characterSlot)) continue;
                    definitions.TryGetValue(configured.SingleWeaponConfigurationId, out SingleWeaponConfiguration definition);
                    string traceId = definition?.TraceProfiles != null && definition.TraceProfiles.Count > 0
                        ? definition.TraceProfiles[0]?.TraceId ?? string.Empty
                        : string.Empty;
                    result.Add(new WeaponAttachmentProfile
                    {
                        AttachmentId = identity,
                        SingleWeaponConfigurationId = configured.SingleWeaponConfigurationId,
                        SlotId = configured.SlotId,
                        AttachSocketId = characterSlot.AttachSocketId,
                        LocalGripPose = ClonePose(characterSlot.SlotPose),
                        TraceId = traceId
                    });
                }
            }
            result.Sort((left, right) => string.CompareOrdinal(left?.AttachmentId, right?.AttachmentId));
            return result;
        }

        private static string BuildAttachmentStableId(
            string packageId,
            string configurationId,
            string slotId,
            string weaponId)
        {
            return "weapon.attachment." + NormalizeSegment(ToCharacterCombinationIdentity(packageId, configurationId)) + "." +
                NormalizeSegment(slotId) + "." + NormalizeSegment(weaponId);
        }

        private static List<SingleWeaponConfiguration> GetSortedReferencedSingleWeapons(CharacterResourcePackage package)
        {
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            List<CombinedWeaponConfigurationAuthoringSummary> configurations =
                package?.ApplicationConfig?.CombinedWeaponConfigurations;
            for (int i = 0; configurations != null && i < configurations.Count; i++)
            {
                List<CombinedWeaponConfigurationSlotAuthoringSummary> slots = configurations[i]?.Slots;
                for (int slotIndex = 0; slots != null && slotIndex < slots.Count; slotIndex++)
                {
                    string id = slots[slotIndex]?.SingleWeaponConfigurationId ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(id) && !GlobalWeaponLibrarySchema.IsUnarmed(id))
                        referenced.Add(id);
                }
            }

            var definitions = new List<SingleWeaponConfiguration>();
            List<SingleWeaponConfiguration> library = package?.GlobalWeaponLibrary?.SingleWeaponConfigurations;
            for (int i = 0; library != null && i < library.Count; i++)
            {
                SingleWeaponConfiguration item = library[i];
                if (item != null && referenced.Contains(item.SingleWeaponConfigurationId))
                    definitions.Add(item);
            }
            definitions.Sort((left, right) => string.CompareOrdinal(
                left?.SingleWeaponConfigurationId,
                right?.SingleWeaponConfigurationId));
            return definitions;
        }

        private static List<CombinedWeaponConfigurationAuthoringSummary> GetSortedCombinedWeaponConfigurations(
            CharacterApplicationAuthoringSummary application)
        {
            var configurations = new List<CombinedWeaponConfigurationAuthoringSummary>();
            if (application?.CombinedWeaponConfigurations != null)
                configurations.AddRange(application.CombinedWeaponConfigurations);
            configurations.Sort((left, right) => string.CompareOrdinal(
                left?.CombinedWeaponConfigurationId,
                right?.CombinedWeaponConfigurationId));
            return configurations;
        }

        private static Dictionary<string, WeaponAttachmentProfile> IndexAttachmentsById(List<WeaponAttachmentProfile> attachments)
        {
            var result = new Dictionary<string, WeaponAttachmentProfile>(StringComparer.Ordinal);
            for (int i = 0; attachments != null && i < attachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = attachments[i];
                if (attachment != null && !string.IsNullOrWhiteSpace(attachment.AttachmentId) && !result.ContainsKey(attachment.AttachmentId))
                    result.Add(attachment.AttachmentId, attachment);
            }
            return result;
        }

        private static List<WeaponAttachmentProfile> ResolveConfigurationAttachments(
            string packageId,
            CombinedWeaponConfigurationAuthoringSummary configuration,
            Dictionary<string, WeaponAttachmentProfile> attachmentsById)
        {
            var result = new List<WeaponAttachmentProfile>();
            List<CombinedWeaponConfigurationSlotAuthoringSummary> slots = configuration?.Slots;
            for (int i = 0; slots != null && i < slots.Count; i++)
            {
                CombinedWeaponConfigurationSlotAuthoringSummary slot = slots[i];
                if (slot != null && attachmentsById.TryGetValue(BuildAttachmentStableId(
                    packageId,
                    configuration.CombinedWeaponConfigurationId,
                    slot.SlotId,
                    slot.SingleWeaponConfigurationId), out WeaponAttachmentProfile attachment))
                    result.Add(attachment);
            }
            result.Sort((left, right) => string.CompareOrdinal(left?.SlotId, right?.SlotId));
            return result;
        }

        private static string ToLoadoutStableId(string packageId, string configurationId)
        {
            return "equip_loadout." + NormalizeSegment(packageId) + "." +
                NormalizeSegment(ToCharacterCombinationIdentity(packageId, configurationId));
        }

        private static string ToCharacterCombinationIdentity(string packageId, string configurationId)
        {
            return GlobalWeaponConfigurationResolver.GetCharacterScopedCombinationIdentity(
                packageId,
                configurationId);
        }

        private static List<CharacterBodyPartContactPointProfile> GetSortedBodyPartContactPoints(
            CharacterAuthoringGeometry geometry)
        {
            var contactPoints = new List<CharacterBodyPartContactPointProfile>();
            if (geometry != null && geometry.BodyPartContactPoints != null)
                contactPoints.AddRange(geometry.BodyPartContactPoints);

            contactPoints.Sort(CompareBodyPartContactPoint);
            return contactPoints;
        }

        private static int CompareBodyPartContactPoint(
            CharacterBodyPartContactPointProfile left,
            CharacterBodyPartContactPointProfile right)
        {
            int result = (left != null ? left.SourceOrder : 0).CompareTo(right != null ? right.SourceOrder : 0);
            if (result != 0)
                return result;

            result = (left != null ? left.ContactPointId : 0).CompareTo(right != null ? right.ContactPointId : 0);
            if (result != 0)
                return result;

            result = (left != null ? left.BodyPartId : 0).CompareTo(right != null ? right.BodyPartId : 0);
            if (result != 0)
                return result;

            return string.CompareOrdinal(left != null ? left.StableId : string.Empty, right != null ? right.StableId : string.Empty);
        }

        private static string BodyPartContactPointObjectPath(CharacterBodyPartContactPointProfile contactPoint, int index)
        {
            if (contactPoint != null && !string.IsNullOrWhiteSpace(contactPoint.StableId))
                return "geometry/bodyPartContactPoints/" + contactPoint.StableId;

            return "geometry/bodyPartContactPoints/" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static int QuantizeMetersToMillimeters(float meters)
        {
            return (int)Math.Round(meters * 1000f, MidpointRounding.AwayFromZero);
        }

        private static FieldValue BuildAllowedStateIdsField(
            CharacterResourcePackage package,
            List<WeaponAttachmentProfile> attachments,
            GeneratedIds ids)
        {
            int count = package?.ApplicationConfig?.CombinedWeaponConfigurations?.Count ?? 0;
            var allowedStateIds = new FieldValue[count];
            for (int i = 0; i < count; i++)
                allowedStateIds[i] = Int(ids.StateStartId + i);
            return FieldValue.FromList(allowedStateIds);
        }

        private static List<string> GetDistinctSlots(CharacterAuthoringGeometry geometry)
        {
            var slots = new List<string>();
            if (geometry != null)
            {
                for (int i = 0; i < geometry.WeaponSlots.Count; i++)
                {
                    CharacterWeaponSlotProfile slot = geometry.WeaponSlots[i];
                    if (slot != null && !string.IsNullOrWhiteSpace(slot.SlotId) && !slots.Contains(slot.SlotId))
                        slots.Add(slot.SlotId);
                }
            }

            slots.Sort(StringComparer.Ordinal);
            return slots;
        }

        private static int CountDistinctSlots(CharacterAuthoringGeometry geometry)
        {
            return GetDistinctSlots(geometry).Count;
        }

        private static string GetCharacterStableId(CharacterResourcePackage package)
        {
            if (package != null && package.ApplicationConfig != null && !string.IsNullOrWhiteSpace(package.ApplicationConfig.CharacterStableId))
                return package.ApplicationConfig.CharacterStableId;
            string packageId = package != null && package.Manifest != null ? package.Manifest.PackageId : "character";
            return "mx.character." + NormalizeSegment(packageId);
        }

        private static string GetBodyProfileStableId(CharacterResourcePackage package)
        {
            if (package != null && package.ApplicationConfig != null && !string.IsNullOrWhiteSpace(package.ApplicationConfig.BodyProfileStableId))
                return package.ApplicationConfig.BodyProfileStableId;
            string packageId = package != null && package.Manifest != null ? package.Manifest.PackageId : "character";
            return "mx.body." + NormalizeSegment(packageId);
        }

        private static string GetAttributeProfileStableId(CharacterResourcePackage package)
        {
            if (package != null && package.ApplicationConfig != null && !string.IsNullOrWhiteSpace(package.ApplicationConfig.AttributeProfileStableId))
                return package.ApplicationConfig.AttributeProfileStableId;
            string packageId = package != null && package.Manifest != null ? package.Manifest.PackageId : "character";
            return "mx.character_attr." + NormalizeSegment(packageId);
        }

        private static string GetEquipmentSchemaStableId(CharacterResourcePackage package)
        {
            if (package != null && package.ApplicationConfig != null && !string.IsNullOrWhiteSpace(package.ApplicationConfig.EquipmentSchemaStableId))
                return package.ApplicationConfig.EquipmentSchemaStableId;
            string packageId = package != null && package.Manifest != null ? package.Manifest.PackageId : "character";
            return "mx.equipment_schema." + NormalizeSegment(packageId);
        }

        private static string GetTargetRootPath(CharacterResourcePackage package, CharacterAuthoringCompileOptions options)
        {
            string root = options != null && !string.IsNullOrWhiteSpace(options.GeneratedRootPath)
                ? options.GeneratedRootPath
                : "Assets/MxFrameworkGenerated/CharacterPackages";
            string packageSegment = package != null && package.Manifest != null ? NormalizeSegment(package.Manifest.PackageId) : "character";
            return CombineProjectPath(root, packageSegment);
        }

        private static string GetImportTargetRelativePath(CharacterPackageResourceEntry entry)
        {
            if (entry != null && entry.ImportHints != null && !string.IsNullOrWhiteSpace(entry.ImportHints.TargetRelativePath))
                return entry.ImportHints.TargetRelativePath.Replace('\\', '/');
            return entry != null ? (entry.RelativePath ?? string.Empty).Replace('\\', '/') : string.Empty;
        }

        private static string GetTargetPathPolicy(CharacterPackageResourceEntry entry, CharacterAuthoringCompileOptions options)
        {
            if (entry != null && entry.ImportHints != null && !string.IsNullOrWhiteSpace(entry.ImportHints.TargetPathPolicy))
                return entry.ImportHints.TargetPathPolicy;
            return options != null ? options.TargetUnityPathPolicy : string.Empty;
        }

        private static string CombineProjectPath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left))
                return (right ?? string.Empty).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(right))
                return left.Replace('\\', '/');
            return left.TrimEnd('/', '\\').Replace('\\', '/') + "/" + right.TrimStart('/', '\\').Replace('\\', '/');
        }

        private static string ToStateStableId(string loadoutStableId)
        {
            if (loadoutStableId != null && loadoutStableId.Contains("equip_loadout."))
                return loadoutStableId.Replace("equip_loadout.", "equip_state.");
            return "equip_state." + NormalizeSegment(loadoutStableId);
        }

        private static string ToActionSetStableId(string loadoutStableId)
        {
            if (loadoutStableId != null && loadoutStableId.Contains("equip_loadout."))
                return loadoutStableId.Replace("equip_loadout.", "action_set.");
            return "action_set." + NormalizeSegment(loadoutStableId);
        }

        private static string ToAnimationProfileId(string loadoutStableId)
        {
            string value = NormalizeSegment(loadoutStableId);
            if (value.StartsWith("equip_loadout.", StringComparison.Ordinal))
                value = value.Substring("equip_loadout.".Length);
            return "anim." + value;
        }

        private static string MapBodyKind(CharacterResourcePackage package, CharacterAuthoringBodyKind kind)
        {
            if (kind == CharacterAuthoringBodyKind.Primitive)
                return "Slime";
            if (kind == CharacterAuthoringBodyKind.Compound)
                return "SimpleShape";
            if (kind == CharacterAuthoringBodyKind.Skeletal
                && string.Equals(
                    package?.AnimationAuthoring?.RigType,
                    GlobalAnimationRigTypes.Generic,
                    StringComparison.Ordinal))
            {
                return "Custom";
            }
            return "Humanoid";
        }

        private static string MapBodyPartKind(CharacterBodyPartAuthoring part)
        {
            string id = NormalizeSegment(part != null ? part.PartId : string.Empty);
            if (id.Contains("root"))
                return "Root";
            if (id.Contains("head"))
                return "Head";
            if (id.Contains("torso") || id.Contains("body"))
                return "Torso";
            if (id.Contains("hand"))
                return "Hand";
            if (id.Contains("leg"))
                return "Leg";
            if (id.Contains("foot"))
                return "Foot";
            if (id.Contains("tail"))
                return "Tail";
            if (id.Contains("core"))
                return "Core";
            if (part != null && part.PartKind == CharacterAuthoringBodyPartKind.Primitive)
                return "Core";
            return "Custom";
        }

        private static string MapSlotKind(string slot)
        {
            if (string.Equals(slot, "mainHand", StringComparison.OrdinalIgnoreCase))
                return "MainHand";
            if (string.Equals(slot, "offHand", StringComparison.OrdinalIgnoreCase))
                return "OffHand";
            if (string.Equals(slot, "twoHand", StringComparison.OrdinalIgnoreCase))
                return "TwoHand";
            if (string.Equals(slot, "naturalWeapon", StringComparison.OrdinalIgnoreCase))
                return "NaturalWeapon";
            return "Custom";
        }

        private static string MapWeaponCategory(WeaponAttachmentProfile attachment)
        {
            string id = NormalizeSegment(attachment != null ? attachment.SingleWeaponConfigurationId : string.Empty);
            string slot = attachment != null ? attachment.EquipSlot : string.Empty;
            if (id.Contains("shield") || string.Equals(slot, "offHand", StringComparison.OrdinalIgnoreCase))
                return "Shield";
            if (id.Contains("bow") || id.Contains("gun"))
                return "Ranged";
            if (id.Contains("staff") || id.Contains("wand"))
                return "Catalyst";
            if (id.Contains("claw") || id.Contains("bite"))
                return "Natural";
            return "OneHandMelee";
        }

        private static string GetPrimaryWeaponTag(WeaponAttachmentProfile attachment)
        {
            string category = MapWeaponCategory(attachment);
            if (category == "Shield")
                return "shield";
            if (category == "Natural")
                return "natural";
            if (category == "Ranged")
                return "ranged";
            return "blade";
        }

        private static string GetAbilitySlot(WeaponAttachmentProfile attachment)
        {
            return MapWeaponCategory(attachment) == "Shield" ? "guard" : "primary";
        }

        private static string GetInputIntent(WeaponAttachmentProfile attachment)
        {
            return MapWeaponCategory(attachment) == "Shield" ? "intent.guard" : "intent.primary";
        }

        private static FieldValue SlotAllowedCategories(string slot)
        {
            if (string.Equals(slot, "naturalWeapon", StringComparison.OrdinalIgnoreCase))
                return List("Natural");
            return List("OneHandMelee", "Shield", "Tool", "Catalyst", "Ranged");
        }

        private static string MapResourceType(CharacterPackageResourceMappingEntry entry)
        {
            if (entry == null)
                return string.Empty;
            if (entry.TypeId == CharacterPackageResourceTypeIds.Model)
                return "GameObject";
            if (entry.TypeId == CharacterPackageResourceTypeIds.Animation)
                return "AnimationClipGroup";
            if (entry.TypeId == CharacterPackageResourceTypeIds.Preview || entry.TypeId == CharacterPackageResourceTypeIds.Texture)
                return entry.Usage == "characterPortrait" || entry.Usage == "characterStanding" ? "Texture2D" : "Sprite";
            return entry.TypeId;
        }

        private static string MapResourceUsageKind(CharacterPackageResourceMappingEntry entry)
        {
            if (entry == null)
                return "Custom";
            if (entry.Usage == CharacterPackageResourceUsageIds.CharacterModel)
                return "Model";
            if (entry.Usage == CharacterPackageResourceUsageIds.WeaponModel)
                return "WeaponModel";
            if (entry.Usage == CharacterPackageResourceUsageIds.AnimationClipGroup)
                return "AnimationProfile";
            if (entry.Usage == CharacterPackageResourceUsageIds.PreviewThumbnail)
                return "Ui";
            if (entry.Usage == "characterPortrait" || entry.Usage == "characterStanding")
                return "Ui";
            return "Custom";
        }

        private static float ComputeTraceLength(WeaponTraceProfile trace)
        {
            if (trace == null || trace.StartPose == null || trace.EndPose == null || trace.StartPose.Position == null || trace.EndPose.Position == null)
                return 0f;
            float dx = trace.EndPose.Position.X - trace.StartPose.Position.X;
            float dy = trace.EndPose.Position.Y - trace.StartPose.Position.Y;
            float dz = trace.EndPose.Position.Z - trace.StartPose.Position.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static float ComputeTraceLength(SingleWeaponTraceProfile trace)
        {
            if (trace == null || trace.StartPose?.Position == null || trace.EndPose?.Position == null)
                return 0f;
            float dx = trace.EndPose.Position.X - trace.StartPose.Position.X;
            float dy = trace.EndPose.Position.Y - trace.StartPose.Position.Y;
            float dz = trace.EndPose.Position.Z - trace.StartPose.Position.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static string ToDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var builder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c))
                    builder.Append(' ');
                builder.Append(i == 0 ? char.ToUpperInvariant(c) : c);
            }

            return builder.ToString();
        }

        private static bool IsHead(string partId)
        {
            return NormalizeSegment(partId).Contains("head");
        }

        private static bool ContainsTag(List<string> tags, string tag)
        {
            if (tags == null)
                return false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static FieldValue Str(string value)
        {
            return FieldValue.FromScalar(value ?? string.Empty);
        }

        private static FieldValue Int(int value)
        {
            return FieldValue.FromScalar(value.ToString(CultureInfo.InvariantCulture));
        }

        private static FieldValue NumericIdentity(int value)
        {
            return FieldValue.FromNumber(value.ToString(CultureInfo.InvariantCulture));
        }

        private static FieldValue Float(float value)
        {
            return FieldValue.FromScalar(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static FieldValue Bool(bool value)
        {
            return FieldValue.FromScalar(value ? "true" : "false");
        }

        private static FieldValue List(params string[] values)
        {
            var items = new List<FieldValue>();
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                    items.Add(Str(values[i]));
            }

            return FieldValue.FromList(items);
        }

        private static FieldValue Map(params (string Key, FieldValue Value)[] values)
        {
            var map = new Dictionary<string, FieldValue>(StringComparer.Ordinal);
            for (int i = 0; i < values.Length; i++)
                map[values[i].Key] = values[i].Value;
            return FieldValue.FromMap(map);
        }

        private static string NormalizeSegment(string value)
        {
            return CharacterPackageResourceKeyGenerator.NormalizeSegment(value);
        }

        private static string BuildSourcePackageCanonicalText(CharacterResourcePackage package, CharacterContentIdentityCatalogSnapshot identityCatalog = null)
        {
            if (package == null)
                return string.Empty;

            var builder = new StringBuilder();
            if (package.BaseAttributes != null)
                builder.Append("baseAttributes|").Append(JsonSerializer.Serialize(package.BaseAttributes)).Append('\n');
            if (package.TacticalDefaults != null)
                builder.Append("tacticalDefaults|").Append(JsonSerializer.Serialize(package.TacticalDefaults)).Append('\n');
            if (package.Manifest != null)
            {
                builder.Append("manifest|").Append(package.Manifest.PackageId).Append('|').Append(package.Manifest.StableId).Append('|').Append(package.Manifest.Version).Append('\n');
                if (package.Manifest.CoordinateConvention != null)
                    builder.Append("coord|").Append(package.Manifest.CoordinateConvention.UpAxis).Append('|').Append(package.Manifest.CoordinateConvention.ForwardAxis).Append('|').Append(package.Manifest.CoordinateConvention.Handedness).Append('|').Append(package.Manifest.CoordinateConvention.UnitScaleMeters.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }

            if (package.PackageConfigFileContents != null)
            {
                foreach (KeyValuePair<string, string> configFile
                    in package.PackageConfigFileContents.OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    builder.Append("packageConfig|")
                        .Append(configFile.Key).Append('|')
                        .Append(CharacterPackageHashUtility.ComputeTextSha256(configFile.Value ?? string.Empty))
                        .Append('\n');
                }
            }

            CharacterPackageResourceCatalog catalog = package.ResourceCatalog ?? new CharacterPackageResourceCatalog();
            var resourceLines = new List<string>();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry != null)
                    resourceLines.Add(entry.ResourceKey + "|" + entry.StableId + "|" + entry.RelativePath + "|" + CharacterPackageResourcePipeline.GetDeclaredContentHash(entry) + "|" + CharacterPackageResourcePipeline.ComputeImportHash(entry) + "|" + CharacterPackageResourcePipeline.ComputeDependencyHash(entry, catalog));
            }

            resourceLines.Sort(StringComparer.Ordinal);
            for (int i = 0; i < resourceLines.Count; i++)
                builder.Append("resource|").Append(resourceLines[i]).Append('\n');

            GlobalWeaponLibrary library = package.GlobalWeaponLibrary;
            builder.Append("weaponLibrary|")
                .Append(library?.SchemaVersion ?? string.Empty).Append('|')
                .Append(library?.LibraryId ?? string.Empty).Append('\n');
            if (library?.WeaponTypes != null)
                foreach (var type in library.WeaponTypes.OrderBy(t => t.TypeId, StringComparer.Ordinal))
                    builder.Append("weaponType|").Append(type.TypeId).Append('|').Append(type.DisplayName).Append('|')
                        .Append(type.EquipmentCategory).Append('|').Append(type.IconResourceKey).Append('\n');
            List<SingleWeaponConfiguration> referencedSingles = GetSortedReferencedSingleWeapons(package);
            for (int i = 0; i < referencedSingles.Count; i++)
            {
                SingleWeaponConfiguration single = referencedSingles[i];
                builder.Append("singleWeapon|")
                    .Append(single.SingleWeaponConfigurationId).Append('|')
                    .Append(single.DisplayName).Append('|')
                    .Append(single.Category).Append('|').Append(single.WeaponTypeId).Append('|');
                AppendStringList(builder, single.Tags);
                AppendStringList(builder, single.AllowedSlots);
                AppendStringList(builder, single.OccupiesSlots);
                AppendSelectionRef(builder, single.WeaponRef);
                builder.Append("singleProfiles|")
                    .Append(single.PresentationConfiguration?.ProfileId).Append('|')
                    .Append(single.PresentationConfiguration?.BladeTrailResourceKey ?? string.Empty).Append('|')
                    .Append(single.PresentationConfiguration?.BladeTrailResidueMilliseconds ?? 100).Append('|')
                    .Append(single.PresentationConfiguration?.BladeTrailStyleId ?? 0).Append('|')
                    .Append(single.CombatConfiguration?.ProfileId).Append('|')
                    .Append(single.ResourcePolicy?.ProfileId).Append('|');
                AppendPose(builder, single.ModelPose);
                for (int traceIndex = 0; single.TraceProfiles != null && traceIndex < single.TraceProfiles.Count; traceIndex++)
                    builder.Append("singleTrace|").Append(JsonSerializer.Serialize(single.TraceProfiles[traceIndex])).Append('\n');
            }

            AppendReferencedGlobalConfigurationHashes(builder, package);

            AppendCharacterActionSourceLibrary(builder, "commonActionLibrary", package.CommonCharacterActionLibrary);
            AppendCharacterActionSourceLibrary(builder, "characterActionLibrary", package.CharacterActionLibrary);

            List<CombinedWeaponConfigurationAuthoringSummary> combined =
                GetSortedCombinedWeaponConfigurations(package.ApplicationConfig);
            builder.Append("defaultCombinedWeapon|")
                .Append(package.ApplicationConfig?.DefaultCombinedWeaponConfigurationId ?? string.Empty)
                .Append('\n');
            var additionalActionLibraries = package.ApplicationConfig?.AdditionalActionLibraryIds
                ?? new List<string>();
            var sortedAdditionalActionLibraries = new List<string>(additionalActionLibraries);
            sortedAdditionalActionLibraries.Sort(StringComparer.Ordinal);
            for (int libraryIndex = 0; libraryIndex < sortedAdditionalActionLibraries.Count; libraryIndex++)
                builder.Append("additionalActionLibrary|")
                    .Append(sortedAdditionalActionLibraries[libraryIndex]).Append('\n');
            if (package.ApplicationConfig?.GameplayCameraProfile != null)
            {
                builder.Append("gameplayCamera|")
                    .Append(JsonSerializer.Serialize(package.ApplicationConfig.GameplayCameraProfile))
                    .Append('\n');
            }
            if (package.ApplicationConfig?.PresentationImages != null)
                builder.Append("presentationImages|").Append(JsonSerializer.Serialize(package.ApplicationConfig.PresentationImages)).Append('\n');
            for (int i = 0; i < combined.Count; i++)
            {
                CombinedWeaponConfigurationAuthoringSummary configuration = combined[i];
                builder.Append("combinedWeapon|")
                    .Append(configuration.CombinedWeaponConfigurationId).Append('|')
                    .Append(configuration.DisplayName).Append('|')
                    .Append(configuration.AnimationConfigurationId).Append('|')
                    .Append(configuration.AbilityConfigurationId).Append('|')
                    .Append(configuration.ReactionConfigurationId).Append('|')
                    .Append(configuration.ActionConfiguration?.ActionSetId).Append('|')
                    .Append(JsonSerializer.Serialize(configuration.ActionConfiguration?.ActionSet)).Append('\n');
                List<CombinedWeaponActionEntry> actions = configuration.ActionConfiguration?.Actions;
                var sortedActions = actions != null
                    ? new List<CombinedWeaponActionEntry>(actions)
                    : new List<CombinedWeaponActionEntry>();
                sortedActions.Sort((left, right) => string.CompareOrdinal(left?.ActionRole, right?.ActionRole));
                for (int actionIndex = 0; actionIndex < sortedActions.Count; actionIndex++)
                {
                    CombinedWeaponActionEntry action = sortedActions[actionIndex];
                    builder.Append("combinedAction|").Append(action?.ActionRole).Append('|')
                        .Append(action?.ActionId).Append('|').Append(action?.TimelineId).Append('|')
                        .Append(action?.AnimationClipId).Append('|')
                        .Append(JsonSerializer.Serialize(action?.Action)).Append('|')
                        .Append(JsonSerializer.Serialize(action?.Timeline)).Append('\n');
                }
                List<CombinedWeaponCommandBinding> commandBindings = configuration.ActionConfiguration?.CommandBindings;
                var sortedCommandBindings = commandBindings != null
                    ? new List<CombinedWeaponCommandBinding>(commandBindings)
                    : new List<CombinedWeaponCommandBinding>();
                sortedCommandBindings.Sort((left, right) => string.CompareOrdinal(left?.IntentId, right?.IntentId));
                for (int bindingIndex = 0; bindingIndex < sortedCommandBindings.Count; bindingIndex++)
                {
                    CombinedWeaponCommandBinding binding = sortedCommandBindings[bindingIndex];
                    builder.Append("combinedCommandBinding|").Append(binding?.IntentId).Append('|')
                        .Append(binding?.ActionRole).Append('|').Append(binding?.ActionId).Append('|')
                        .Append(binding?.Priority).Append('|').Append(binding?.AllowQueue).Append('|')
                        .Append(binding?.QueueWindowFrames).Append('\n');
                }
                List<CombinedWeaponAbilityActionBinding> abilityBindings = configuration.ActionConfiguration?.AbilityActionBindings;
                var sortedAbilityBindings = abilityBindings != null
                    ? new List<CombinedWeaponAbilityActionBinding>(abilityBindings)
                    : new List<CombinedWeaponAbilityActionBinding>();
                sortedAbilityBindings.Sort((left, right) => left == null || right == null
                    ? (left == null ? -1 : 1)
                    : left.AbilityId != right.AbilityId
                        ? left.AbilityId.CompareTo(right.AbilityId)
                        : string.CompareOrdinal(left.ActionRole, right.ActionRole));
                for (int bindingIndex = 0; bindingIndex < sortedAbilityBindings.Count; bindingIndex++)
                {
                    CombinedWeaponAbilityActionBinding binding = sortedAbilityBindings[bindingIndex];
                    builder.Append("combinedAbilityAction|").Append(binding?.AbilityId).Append('|')
                        .Append(binding?.ActionRole).Append('|').Append(binding?.ActionId).Append('\n');
                }
                List<CombinedWeaponReactionActionBinding> reactionBindings = configuration.ActionConfiguration?.ReactionActionBindings;
                var sortedReactionBindings = reactionBindings != null
                    ? new List<CombinedWeaponReactionActionBinding>(reactionBindings)
                    : new List<CombinedWeaponReactionActionBinding>();
                sortedReactionBindings.Sort((left, right) => string.CompareOrdinal(left?.ReactionSlotId, right?.ReactionSlotId));
                for (int bindingIndex = 0; bindingIndex < sortedReactionBindings.Count; bindingIndex++)
                {
                    CombinedWeaponReactionActionBinding binding = sortedReactionBindings[bindingIndex];
                    builder.Append("combinedReactionAction|").Append(binding?.ReactionSlotId).Append('|')
                        .Append(binding?.ActionRole).Append('|').Append(binding?.ActionId).Append('\n');
                }
                List<CombinedWeaponCommandSequence> commandSequences = configuration.ActionConfiguration?.CommandSequences;
                var sortedCommandSequences = commandSequences != null
                    ? new List<CombinedWeaponCommandSequence>(commandSequences)
                    : new List<CombinedWeaponCommandSequence>();
                sortedCommandSequences.Sort((left, right) => string.CompareOrdinal(left?.IntentId, right?.IntentId));
                for (int sequenceIndex = 0; sequenceIndex < sortedCommandSequences.Count; sequenceIndex++)
                {
                    CombinedWeaponCommandSequence sequence = sortedCommandSequences[sequenceIndex];
                    builder.Append("combinedCommandSequence|").Append(sequence?.IntentId).Append('|')
                        .Append(sequence?.WrapAfterLast).Append('|').Append(sequence?.Priority).Append('|')
                        .Append(sequence?.AllowQueue).Append('|').Append(sequence?.QueueWindowFrames).Append('|');
                    AppendStringList(builder, sequence?.ActionRoles);
                }
                var slots = new List<CombinedWeaponConfigurationSlotAuthoringSummary>(configuration.Slots ??
                    new List<CombinedWeaponConfigurationSlotAuthoringSummary>());
                slots.Sort((left, right) => string.CompareOrdinal(left?.SlotId, right?.SlotId));
                for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
                {
                    CombinedWeaponConfigurationSlotAuthoringSummary slot = slots[slotIndex];
                    if (slot != null)
                        builder.Append("combinedSlot|").Append(slot.SlotId).Append('|')
                            .Append(slot.SingleWeaponConfigurationId).Append('\n');
                }
            }

            CharacterAuthoringGeometry geometry = package.Geometry ?? new CharacterAuthoringGeometry();
            for (int i = 0; i < geometry.BodyParts.Count; i++)
            {
                CharacterBodyPartAuthoring part = geometry.BodyParts[i];
                if (part == null)
                    continue;
                builder.Append("part|").Append(part.PartId).Append('|').Append(part.DisplayName).Append('|').Append(part.PartKind).Append('|').Append(part.ParentPartId).Append('|').Append(part.BonePath).Append('|').Append(part.LocatorId).Append('|').Append(part.DefaultHitZoneId).Append('|').Append(part.ReactionGroupId).Append('|');
                AppendStringList(builder, part.Tags);
            }
            for (int i = 0; i < geometry.Colliders.Count; i++)
            {
                CharacterBodyColliderProfile collider = geometry.Colliders[i];
                if (collider == null)
                    continue;
                builder.Append("collider|").Append(collider.ColliderId).Append('|').Append(collider.PartId).Append('|').Append(collider.HitZoneId).Append('|').Append(collider.Shape).Append('|').Append(collider.Radius.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(collider.Height.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(collider.Priority).Append('|').Append(collider.IsWeakPoint).Append('|').Append(collider.DamageMultiplierOverride.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(collider.ImpactPowerScaleOverride.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(collider.PhysicsLayer).Append('|').Append(collider.MaterialStableId).Append('|');
                AppendVector(builder, collider.Size);
                AppendPose(builder, collider.LocalPose);
            }
            if (geometry.DamageZonesDocumentState == CharacterDamageZonesDocumentState.Current
                && geometry.DamageZones != null)
            {
                builder.Append("damageZones|").Append(geometry.DamageZones.Format).Append('|')
                    .Append(geometry.DamageZones.SchemaVersion).Append('\n');
                var zones = new List<CharacterDamageZoneAuthoring>(
                    geometry.DamageZones.Zones ?? new List<CharacterDamageZoneAuthoring>());
                zones.Sort((left, right) => string.CompareOrdinal(left?.ZoneId, right?.ZoneId));
                for (int i = 0; i < zones.Count; i++)
                {
                    CharacterDamageZoneAuthoring zone = zones[i];
                    if (zone == null)
                        continue;
                    builder.Append("damageZone|").Append(zone.ZoneId).Append('|')
                        .Append(string.IsNullOrWhiteSpace(zone.DisplayName) ? zone.ZoneId : zone.DisplayName).Append('|')
                        .Append(zone.Hittable).Append('|').Append(zone.Aimable).Append('|')
                        .Append(zone.Approachable).Append('|').Append(zone.AuthoredPriority).Append('|');
                    var hitZoneIds = new List<string>(zone.HitZoneIds ?? new List<string>());
                    hitZoneIds.Sort(StringComparer.Ordinal);
                    AppendStringList(builder, hitZoneIds);
                }
            }
            if (geometry.EngagementAnchorsDocumentState
                    == CharacterEngagementAnchorsDocumentState.Current
                && geometry.EngagementAnchors != null)
            {
                var anchors = new List<CharacterEngagementAnchorAuthoring>(
                    geometry.EngagementAnchors.Anchors ?? new List<CharacterEngagementAnchorAuthoring>());
                anchors.Sort((left, right) => string.CompareOrdinal(left?.AnchorId, right?.AnchorId));
                builder.Append("engagementAnchors|")
                    .Append(geometry.EngagementAnchors.Format).Append('|')
                    .Append(geometry.EngagementAnchors.SchemaVersion).Append('|')
                    .Append(geometry.EngagementAnchors.Revision).Append('\n');
                for (int i = 0; i < anchors.Count; i++)
                {
                    CharacterEngagementAnchorAuthoring anchor = anchors[i];
                    if (anchor == null)
                        continue;
                    CharacterAuthoringIntVector3 position =
                        anchor.LocalPositionMillimeters ?? new CharacterAuthoringIntVector3();
                    builder.Append("engagementAnchor|").Append(anchor.AnchorId).Append('|')
                        .Append(anchor.ZoneId).Append('|').Append(anchor.ReferenceFrame).Append('|')
                        .Append(position.X).Append('|').Append(position.Y).Append('|')
                        .Append(position.Z).Append('|').Append(anchor.YawMilliDegrees).Append('|')
                        .Append(anchor.Capacity).Append('|').Append(anchor.AuthoredPriority).Append('|')
                        .Append(anchor.Enabled).Append('|').Append(anchor.PhaseId ?? string.Empty)
                        .Append('\n');
                }
            }
            if (geometry.BodyPresenceDocumentState == CharacterBodyPresenceDocumentState.Current
                && geometry.BodyPresence != null)
            {
                builder.Append("bodyPresence|")
                    .Append(CharacterBodyPresenceDocumentCodec.SerializeCanonical(geometry.BodyPresence))
                    .Append('\n');
            }
            if (geometry.BodyPartDamageDocumentState == CharacterBodyPartDamageDocumentState.Current
                && geometry.BodyPartDamage != null)
            {
                builder.Append("bodyPartDamage|")
                    .Append(CharacterBodyPartDamageDocumentCodec.BuildCanonicalHashText(
                        geometry.BodyPartDamage));
            }
            if (geometry.AuthorityPoseRigDocumentState == CharacterAuthorityPoseRigDocumentState.Current
                && geometry.AuthorityPoseRig != null)
            {
                builder.Append("authorityPoseRig|")
                    .Append(CharacterAuthorityPoseRigDocumentCodec.SerializeCanonical(geometry.AuthorityPoseRig))
                    .Append('\n');
            }
            for (int i = 0; i < geometry.Sockets.Count; i++)
            {
                CharacterSocketProfile socket = geometry.Sockets[i];
                if (socket == null)
                    continue;
                builder.Append("socket|").Append(socket.SocketId).Append('|').Append(socket.ParentPartId).Append('|').Append(socket.BonePath).Append('|').Append(socket.LocatorPath).Append('|').Append(socket.Usage).Append('|').Append(socket.MirrorPairSocketId).Append('|').Append(socket.Handedness).Append('|').Append(socket.SideTag).Append('|');
                AppendStringList(builder, socket.Tags);
                AppendPose(builder, socket.LocalPose);
            }
            List<WeaponAttachmentProfile> sourceAttachments = GetSortedAttachments(geometry);
            for (int i = 0; i < sourceAttachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = sourceAttachments[i];
                if (attachment == null)
                    continue;
                builder.Append("attachment|").Append(attachment.AttachmentId).Append('|').Append(attachment.SingleWeaponConfigurationId).Append('|').Append(attachment.EquipSlot).Append('|').Append(attachment.AttachSocketId).Append('|').Append(attachment.TraceId).Append('|').Append(attachment.TraceStartSocketId).Append('|').Append(attachment.TraceEndSocketId).Append('|').Append(attachment.TraceRadius.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(attachment.TraceSampleRule).Append('|');
                AppendPose(builder, attachment.LocalGripPose);
            }
            for (int i = 0; i < geometry.Traces.Count; i++)
            {
                WeaponTraceProfile trace = geometry.Traces[i];
                if (trace == null)
                    continue;
                builder.Append("trace|").Append(trace.TraceId).Append('|').Append(trace.WeaponId).Append('|').Append(trace.EquipSlot).Append('|').Append(trace.StartLocatorPath).Append('|').Append(trace.EndLocatorPath).Append('|').Append(trace.Radius.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(trace.ProximalExtension.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(trace.ProximalRadius.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(trace.SampleRule).Append('|').Append(trace.FixedSampleCount).Append('|');
                AppendStringList(builder, trace.ActionKeys);
                AppendPose(builder, trace.StartPose);
                AppendPose(builder, trace.EndPose);
            }
            if (identityCatalog != null)
            {
                builder.Append("identityCatalogRevision|").Append(identityCatalog.Revision).Append('\n');
                builder.Append("identityCatalogHash|").Append(identityCatalog.CatalogHash ?? string.Empty).Append('\n');
            }
            return builder.ToString();
        }

        private static void AppendCharacterActionSourceLibrary(
            StringBuilder builder,
            string prefix,
            CharacterActionSourceLibrary library)
        {
            builder.Append(prefix).Append('|').Append(library?.SchemaVersion ?? string.Empty).Append('\n');
            var animationSources = new List<CharacterActionAnimationSource>(
                library?.AnimationSources ?? new List<CharacterActionAnimationSource>());
            animationSources.Sort((left, right) =>
                string.CompareOrdinal(left?.ResourceKeyPrefix, right?.ResourceKeyPrefix));
            for (int i = 0; i < animationSources.Count; i++)
            {
                CharacterActionAnimationSource source = animationSources[i];
                builder.Append(prefix).Append("AnimationSource|")
                    .Append(source?.ResourceKeyPrefix).Append('|')
                    .Append(source?.RigType).Append('|')
                    .Append(source?.SkeletonProfileId).Append('|')
                    .Append(source?.AvatarProfileId).Append('|')
                    .Append(source?.AllowRetargeting).Append('\n');
            }
            var actions = new List<(int ActionId, string Json)>();
            for (int i = 0; library?.Actions != null && i < library.Actions.Count; i++)
            {
                JsonElement action = library.Actions[i];
                actions.Add((CharacterActionSourceLibrary.GetActionId(action), action.GetRawText()));
            }
            actions.Sort((left, right) => left.ActionId.CompareTo(right.ActionId));
            for (int i = 0; i < actions.Count; i++)
                builder.Append(prefix).Append("Action|").Append(actions[i].ActionId).Append('|')
                    .Append(actions[i].Json).Append('\n');
        }

        private static string BuildConfigPatchCanonicalText(CharacterAuthoringCompiledConfigPatch configPatch)
        {
            var builder = new StringBuilder();
            if (configPatch == null || configPatch.Patch == null)
                return builder.ToString();

            for (int i = 0; i < configPatch.Patch.Entries.Count; i++)
            {
                PatchEntry entry = configPatch.Patch.Entries[i];
                builder.Append(entry.Source).Append('|').Append(entry.Id).Append('|').Append(entry.Operation).Append('\n');
                var keys = new List<string>(entry.Fields.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (int j = 0; j < keys.Count; j++)
                    builder.Append(keys[j]).Append('=').Append(CanonicalFieldValue(entry.Fields[keys[j]])).Append('\n');
            }

            builder.Append("runtimeCharacterId|").Append(configPatch.RuntimeCharacterId).Append('\n');
            return builder.ToString();
        }

        private static string BuildGeometryBindingCanonicalText(CharacterAuthoringGeometryBinding binding)
        {
            var builder = new StringBuilder();
            if (binding == null)
                return builder.ToString();

            builder.Append(binding.PackageId).Append('|').Append(binding.BodyProfileStableId).Append('|').Append(binding.CoordinateConversion.RequiresConversion).Append('\n');
            if (string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV2, StringComparison.Ordinal)
                || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV3, StringComparison.Ordinal)
                || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV4, StringComparison.Ordinal)
                || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV5, StringComparison.Ordinal)
                || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV6, StringComparison.Ordinal)
                || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV7, StringComparison.Ordinal)
                || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV8, StringComparison.Ordinal))
                builder.Append("format|").Append(binding.Format).Append('|').Append(binding.SchemaVersion).Append('\n');
            for (int i = 0; i < binding.BodyParts.Count; i++)
            {
                CharacterBodyPartBinding part = binding.BodyParts[i];
                if (part == null)
                    continue;
                builder.Append("part|").Append(part.PartId).Append('|').Append(part.DisplayName).Append('|').Append(part.PartKind).Append('|').Append(part.ParentPartId).Append('|').Append(part.BonePath).Append('|').Append(part.LocatorId).Append('|').Append(part.DefaultHitZoneId).Append('|').Append(part.ReactionGroupId).Append('|');
                AppendStringList(builder, part.Tags);
            }
            for (int i = 0; i < binding.BodyPartContactPoints.Count; i++)
            {
                CharacterBodyPartContactPointBinding contactPoint = binding.BodyPartContactPoints[i];
                if (contactPoint == null)
                    continue;
                builder.Append("contactPoint|").Append(contactPoint.StableId).Append('|').Append(contactPoint.ContactPointId).Append('|').Append(contactPoint.BodyPartId).Append('|').Append(contactPoint.PartId).Append('|').Append(contactPoint.RadiusMeters.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(contactPoint.LayerMaskProfileId).Append('|').Append(contactPoint.Usage).Append('|').Append(contactPoint.Authority).Append('|').Append(contactPoint.ActivationMode).Append('|').Append(contactPoint.MotionMode).Append('|').Append(contactPoint.MotionSocketId).Append('|').Append(contactPoint.SourceOrder).Append('|');
                AppendVector(builder, contactPoint.LocalOffsetMeters);
            }
            for (int i = 0; i < binding.BodyColliders.Count; i++)
            {
                CharacterBodyColliderBinding collider = binding.BodyColliders[i];
                if (collider == null)
                    continue;
                builder.Append("collider|").Append(collider.ColliderId).Append('|').Append(collider.PartId).Append('|').Append(collider.HitZoneId).Append('|').Append(collider.Shape).Append('|').Append(collider.Radius.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(collider.Height.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(collider.Priority).Append('|').Append(collider.IsWeakPoint).Append('|').Append(collider.DamageMultiplierOverride.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(collider.ImpactPowerScaleOverride.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(collider.PhysicsLayer).Append('|').Append(collider.MaterialStableId).Append('|');
                AppendVector(builder, collider.Size);
                AppendPose(builder, collider.LocalPose);
            }
            for (int i = 0; i < (binding.DamageZones?.Count ?? 0); i++)
            {
                CharacterDamageZoneBinding zone = binding.DamageZones[i];
                if (zone == null)
                    continue;
                builder.Append("damageZone|").Append(zone.ZoneId).Append('|').Append(zone.DisplayName).Append('|')
                    .Append(zone.Hittable).Append('|').Append(zone.Aimable).Append('|')
                    .Append(zone.Approachable).Append('|').Append(zone.AuthoredPriority).Append('|');
                AppendIntList(builder, zone.HitZoneIds);
            }
            if ((string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV3, StringComparison.Ordinal)
                    || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV4, StringComparison.Ordinal)
                    || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV5, StringComparison.Ordinal)
                    || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV6, StringComparison.Ordinal)
                    || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV7, StringComparison.Ordinal)
                    || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV8, StringComparison.Ordinal))
                && binding.EngagementAnchors != null)
            {
                builder.Append("engagementAnchorSet|")
                    .Append(binding.EngagementAnchorRevision).Append('|')
                    .Append(binding.EngagementAnchorContentHash ?? string.Empty).Append('\n');
                builder.Append(BuildEngagementAnchorsCanonicalText(
                    binding.EngagementAnchorRevision,
                    binding.EngagementAnchors));
            }
            if ((string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV4, StringComparison.Ordinal)
                    || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV5, StringComparison.Ordinal)
                    || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV6, StringComparison.Ordinal)
                    || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV7, StringComparison.Ordinal)
                    || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV8, StringComparison.Ordinal))
                && binding.BodyPresenceMode == CharacterBodyPresenceBindingMode.Authored)
            {
                builder.Append("bodyPresenceSet|")
                    .Append(binding.BodyPresenceMode).Append('|')
                    .Append(binding.BodyPresenceRevision).Append('|')
                    .Append(binding.BodyPresenceContentHash ?? string.Empty).Append('\n');
                for (int i = 0; i < (binding.BodyPresenceVolumes?.Count ?? 0); i++)
                {
                    CharacterBodyPresenceVolumeBinding volume = binding.BodyPresenceVolumes[i];
                    if (volume == null)
                        continue;
                    CharacterBodyPresenceLocalPose pose =
                        volume.LocalPose ?? new CharacterBodyPresenceLocalPose();
                    builder.Append("bodyPresenceVolume|").Append(volume.VolumeId).Append('|')
                        .Append(volume.ReferenceFrame).Append('|').Append(volume.Shape).Append('|');
                    AppendVector(builder, pose.Position);
                    AppendQuaternion(builder, pose.Rotation);
                    AppendVector(builder, pose.Scale);
                    builder.Append(volume.Radius.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(volume.Height.ToString("R", CultureInfo.InvariantCulture)).Append('|');
                    AppendVector(builder, volume.Size);
                    builder.Append(volume.SkinMillimeters).Append('|')
                        .Append(volume.PressureScaleMilli).Append('|')
                        .Append(volume.Enabled).Append('\n');
                }
                if (string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV7, StringComparison.Ordinal)
                    || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV8, StringComparison.Ordinal))
                {
                    for (int i = 0; i < (binding.AnimatedHurtboxSources?.Count ?? 0); i++)
                    {
                        CharacterBodyPresenceAnimatedHurtboxSourceBinding source = binding.AnimatedHurtboxSources[i];
                        builder.Append("animatedHurtboxSource|").Append(source.SourceId).Append('|')
                            .Append(source.ColliderId).Append('|').Append(source.SkinMillimeters).Append('|')
                            .Append(source.PressureScaleMilli).Append('|').Append(source.Enabled).Append('\n');
                    }
                }
            }
            if ((string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV5, StringComparison.Ordinal)
                    || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV6, StringComparison.Ordinal)
                    || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV7, StringComparison.Ordinal)
                    || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV8, StringComparison.Ordinal))
                && binding.BodyPartDamageMode == CharacterBodyPartDamageBindingMode.Authored)
            {
                builder.Append("bodyPartDamageSet|")
                    .Append(binding.BodyPartDamageFormat).Append('|')
                    .Append(binding.BodyPartDamageSchemaVersion).Append('|')
                    .Append(binding.BodyPartDamageContentEncodingVersion).Append('|')
                    .Append(binding.BodyPartDamageRevision).Append('|')
                    .Append(binding.BodyPartDamageContentHash).Append('\n');
                for (int i = 0; i < (binding.BodyPartDamageDefinitions?.Count ?? 0); i++)
                {
                    CharacterBodyPartDamageBinding definition =
                        binding.BodyPartDamageDefinitions[i];
                    builder.Append("bodyPartDamageDefinition|")
                        .Append(definition.DefinitionIndex).Append('|')
                        .Append(definition.PartStateId).Append('|')
                        .Append(definition.DamageZoneId).Append('|')
                        .Append(definition.WoundCapacity).Append('|')
                        .Append(definition.OpenDamageScaleMilli).Append('|')
                        .Append(definition.FocusConsumeTag).Append('|')
                        .Append(definition.MaxIntegrity).Append('|')
                        .Append(definition.IntegrityDamageScaleMilli).Append('|')
                        .Append(definition.BreakReactionId).Append('|')
                        .Append(definition.AuthoredPriorityDeltaOpen).Append('|')
                        .Append(definition.AuthoredPriorityDeltaBroken).Append('|')
                        .Append(definition.Severable).Append('|')
                        .Append(definition.SeverRequiredTag).Append('|')
                        .Append(definition.PresentationEventId).Append('|');
                    AppendIntList(builder, definition.MemberPartIds);
                }
                for (int i = 0; i < (binding.BodyPartDamagePartLookup?.Count ?? 0); i++)
                {
                    CharacterBodyPartDamageLookupBinding lookup =
                        binding.BodyPartDamagePartLookup[i];
                    builder.Append("bodyPartDamageLookup|").Append(lookup.PartId).Append('|')
                        .Append(lookup.DefinitionIndex).Append('\n');
                }
            }
            if (string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV6, StringComparison.Ordinal)
                || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV7, StringComparison.Ordinal)
                || string.Equals(binding.Format, CharacterAuthoringCompilerFormats.GeometryBindingV8, StringComparison.Ordinal))
                AppendAuthorityPoseRigCanonical(builder, binding.AuthorityPoseRig);
            for (int i = 0; i < binding.Sockets.Count; i++)
            {
                CharacterSocketBinding socket = binding.Sockets[i];
                if (socket == null)
                    continue;
                builder.Append("socket|").Append(socket.SocketId).Append('|').Append(socket.ParentPartId).Append('|').Append(socket.BonePath).Append('|').Append(socket.LocatorPath).Append('|').Append(socket.Usage).Append('|').Append(socket.MirrorPairSocketId).Append('|').Append(socket.Handedness).Append('|').Append(socket.SideTag).Append('|');
                AppendStringList(builder, socket.Tags);
                AppendPose(builder, socket.LocalPose);
            }
            for (int i = 0; i < binding.WeaponAttachments.Count; i++)
            {
                CharacterWeaponAttachmentBinding attachment = binding.WeaponAttachments[i];
                if (attachment == null)
                    continue;
                builder.Append("attachment|").Append(attachment.WeaponId).Append('|').Append(attachment.EquipSlot).Append('|').Append(attachment.AttachSocketId).Append('|').Append(attachment.TraceId).Append('|').Append(attachment.TraceStartSocketId).Append('|').Append(attachment.TraceEndSocketId).Append('|').Append(attachment.TraceRadius.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(attachment.TraceSampleRule).Append('|');
                AppendSelectionRef(builder, attachment.WeaponRef);
                AppendPose(builder, attachment.LocalGripPose);
            }
            for (int i = 0; i < binding.WeaponTraces.Count; i++)
            {
                CharacterWeaponTraceBinding trace = binding.WeaponTraces[i];
                if (trace == null)
                    continue;
                builder.Append("trace|").Append(trace.TraceId).Append('|').Append(trace.WeaponId).Append('|').Append(trace.EquipSlot).Append('|').Append(trace.StartLocatorPath).Append('|').Append(trace.EndLocatorPath).Append('|').Append(trace.Radius.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(trace.ProximalExtension.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(trace.ProximalRadius.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(trace.SampleRule).Append('|').Append(trace.FixedSampleCount).Append('|');
                AppendStringList(builder, trace.ActionKeys);
                AppendPose(builder, trace.StartPose);
                AppendPose(builder, trace.EndPose);
            }
            return builder.ToString();
        }

        private static string BuildEngagementAnchorsCanonicalText(
            int revision,
            List<CharacterEngagementAnchorBinding> source)
        {
            var builder = new StringBuilder();
            builder.Append("revision|").Append(revision).Append('\n');
            var anchors = new List<CharacterEngagementAnchorBinding>(
                source ?? new List<CharacterEngagementAnchorBinding>());
            anchors.Sort((left, right) => string.CompareOrdinal(left?.AnchorLabel, right?.AnchorLabel));
            for (int i = 0; i < anchors.Count; i++)
            {
                CharacterEngagementAnchorBinding anchor = anchors[i];
                if (anchor == null)
                    continue;
                CharacterAuthoringIntVector3 position =
                    anchor.LocalPositionMillimeters ?? new CharacterAuthoringIntVector3();
                builder.Append("engagementAnchor|").Append(anchor.AnchorId).Append('|')
                    .Append(anchor.ZoneId).Append('|').Append(anchor.ReferenceFrame).Append('|')
                    .Append(position.X).Append('|').Append(position.Y).Append('|')
                    .Append(position.Z).Append('|').Append(anchor.YawMilliDegrees).Append('|')
                    .Append(anchor.Capacity).Append('|').Append(anchor.AuthoredPriority).Append('|')
                    .Append(anchor.Enabled).Append('|').Append(anchor.PhaseId)
                    .Append('\n');
            }
            return builder.ToString();
        }

        private static string BuildResourceMappingCanonicalText(CharacterPackageResourceMapping mapping)
        {
            var builder = new StringBuilder();
            if (mapping == null)
                return builder.ToString();

            for (int i = 0; i < mapping.Entries.Count; i++)
            {
                CharacterPackageResourceMappingEntry entry = mapping.Entries[i];
                builder.Append(entry.PackageResourceKey).Append('|').Append(entry.ProjectResourceKey).Append('|').Append(entry.ImportTargetPath).Append('|').Append(entry.DeclaredContentHash).Append('|').Append(entry.ImportHash).Append('|').Append(entry.DependencyHash).Append('|');
                AppendPose(builder, entry.ModelWrapperPose);
            }

            return builder.ToString();
        }

        public static string BuildWritePlanCanonicalText(CharacterUnityImportWritePlan plan)
        {
            var builder = new StringBuilder();
            if (plan == null)
                return builder.ToString();

            builder.Append(plan.TargetRootPath).Append('|').Append(plan.CanWriteToUnityProject).Append('|').Append(plan.CanSpawnAfterImport).Append('\n');
            for (int i = 0; i < plan.Writes.Count; i++)
                builder.Append(plan.Writes[i].Kind).Append('|').Append(plan.Writes[i].SourcePath).Append('|').Append(plan.Writes[i].TargetPath).Append('|').Append(plan.Writes[i].ContentHash).Append('\n');
            return builder.ToString();
        }

        private static void AppendPose(StringBuilder builder, CharacterAuthoringLocalPose pose)
        {
            if (pose == null)
            {
                builder.Append("pose|null\n");
                return;
            }

            builder.Append("pose|")
                .Append(pose.ParentKind).Append('|')
                .Append(pose.ParentPath).Append('|');
            AppendVector(builder, pose.Position);
            AppendQuaternion(builder, pose.Rotation);
            AppendVector(builder, pose.Scale);
            AppendVector(builder, pose.EulerHint);
            builder.Append('\n');
        }

        private static AuthoringResourceSelectionRef CloneSelectionRef(AuthoringResourceSelectionRef selection)
        {
            if (selection == null)
                return new AuthoringResourceSelectionRef();

            return new AuthoringResourceSelectionRef
            {
                ResourceStableId = selection.ResourceStableId ?? string.Empty,
                SourceProviderId = selection.SourceProviderId ?? string.Empty,
                BindingKind = selection.BindingKind,
                ExpectedKind = selection.ExpectedKind ?? string.Empty,
                ExpectedUsage = selection.ExpectedUsage ?? string.Empty,
                ExpectedHash = selection.ExpectedHash ?? string.Empty,
                UnityGuid = selection.UnityGuid ?? string.Empty,
                UnityAssetPath = selection.UnityAssetPath ?? string.Empty,
                RuntimeResourceKey = selection.RuntimeResourceKey ?? string.Empty,
                ProviderResourceKey = selection.ProviderResourceKey ?? string.Empty,
                PackageResourceKey = selection.PackageResourceKey ?? string.Empty,
                AudioCueId = selection.AudioCueId ?? string.Empty,
                AudioEventDefinitionId = selection.AudioEventDefinitionId ?? string.Empty
            };
        }

        private static string GetWeaponRuntimeResourceKey(WeaponAttachmentProfile attachment)
        {
            if (attachment == null)
                return string.Empty;

            return FirstNonEmpty(
                attachment.WeaponRef != null ? attachment.WeaponRef.RuntimeResourceKey : string.Empty,
                attachment.WeaponRef != null ? attachment.WeaponRef.PackageResourceKey : string.Empty,
                attachment.WeaponRef != null ? attachment.WeaponRef.ProviderResourceKey : string.Empty);
        }

        private static string GetWeaponRuntimeResourceKey(SingleWeaponConfiguration definition)
        {
            if (definition == null)
                return string.Empty;

            return FirstNonEmpty(
                definition.WeaponRef != null ? definition.WeaponRef.RuntimeResourceKey : string.Empty,
                definition.WeaponRef != null ? definition.WeaponRef.PackageResourceKey : string.Empty,
                definition.WeaponRef != null ? definition.WeaponRef.ProviderResourceKey : string.Empty);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }

        private static void AppendReferencedGlobalConfigurationHashes(
            StringBuilder builder,
            CharacterResourcePackage package)
        {
            var animationIds = new HashSet<string>(StringComparer.Ordinal);
            var abilityIds = new HashSet<string>(StringComparer.Ordinal);
            var reactionIds = new HashSet<string>(StringComparer.Ordinal);
            List<CombinedWeaponConfigurationAuthoringSummary> combinations =
                package?.ApplicationConfig?.CombinedWeaponConfigurations;
            for (int i = 0; combinations != null && i < combinations.Count; i++)
            {
                CombinedWeaponConfigurationAuthoringSummary item = combinations[i];
                if (!string.IsNullOrWhiteSpace(item?.AnimationConfigurationId))
                    animationIds.Add(item.AnimationConfigurationId);
                if (!string.IsNullOrWhiteSpace(item?.AbilityConfigurationId))
                    abilityIds.Add(item.AbilityConfigurationId);
                if (!string.IsNullOrWhiteSpace(item?.ReactionConfigurationId))
                    reactionIds.Add(item.ReactionConfigurationId);
            }
            AppendGlobalHashes(builder, "animationConfiguration", animationIds,
                id => GlobalWeaponConfigurationResolver.FindAnimation(package?.GlobalWeaponLibrary, id));
            AppendGlobalHashes(builder, "abilityConfiguration", abilityIds,
                id => GlobalWeaponConfigurationResolver.FindAbility(package?.GlobalWeaponLibrary, id));
            AppendGlobalHashes(builder, "reactionConfiguration", reactionIds,
                id => GlobalWeaponConfigurationResolver.FindReaction(package?.GlobalWeaponLibrary, id));
        }

        private static void AppendGlobalHashes<T>(
            StringBuilder builder,
            string kind,
            HashSet<string> ids,
            Func<string, T> resolver) where T : class
        {
            var sorted = new List<string>(ids);
            sorted.Sort(StringComparer.Ordinal);
            for (int i = 0; i < sorted.Count; i++)
            {
                T value = resolver(sorted[i]);
                builder.Append(kind).Append('|').Append(sorted[i]).Append('|')
                    .Append(CharacterPackageHashUtility.ComputeTextSha256(
                        value != null ? SerializeGlobalConfigurationForHash(value) : string.Empty))
                    .Append('\n');
            }
        }

        private static string SerializeGlobalConfigurationForHash<T>(T value) where T : class
        {
            if (value is GlobalAbilityConfiguration ability)
            {
                GlobalAbilityConfiguration copy = JsonSerializer.Deserialize<GlobalAbilityConfiguration>(
                    JsonSerializer.Serialize(ability));
                copy.Abilities ??= new List<GlobalAbilityEntry>();
                copy.Abilities.Sort((left, right) => (left?.AbilityId ?? 0).CompareTo(right?.AbilityId ?? 0));
                for (int i = 0; i < copy.Abilities.Count; i++)
                {
                    copy.Abilities[i].Tags ??= new List<string>();
                    copy.Abilities[i].Tags.Sort(StringComparer.Ordinal);
                }
                copy.AbilityIds = null;
                copy.RemoveAbilityIds = null;
                copy.SlotBindings = null;
                return JsonSerializer.Serialize(copy);
            }
            if (value is GlobalReactionConfiguration reaction)
            {
                GlobalReactionConfiguration copy = JsonSerializer.Deserialize<GlobalReactionConfiguration>(
                    JsonSerializer.Serialize(reaction));
                copy.Slots ??= new List<GlobalReactionSlotConfiguration>();
                copy.Slots.Sort((left, right) => string.CompareOrdinal(left?.SlotId, right?.SlotId));
                return JsonSerializer.Serialize(copy);
            }
            if (value is GlobalAnimationConfiguration animation)
            {
                GlobalAnimationConfiguration copy = JsonSerializer.Deserialize<GlobalAnimationConfiguration>(
                    JsonSerializer.Serialize(animation));
                copy.Sets ??= new List<AnimationAuthoringSet>();
                copy.Profiles ??= new List<AnimationAuthoringProfile>();
                copy.Sets.Sort((left, right) => string.CompareOrdinal(left?.SetId, right?.SetId));
                copy.Profiles.Sort((left, right) => string.CompareOrdinal(left?.ProfileId, right?.ProfileId));
                return JsonSerializer.Serialize(copy);
            }
            return JsonSerializer.Serialize(value);
        }

        private static void AppendSelectionRef(StringBuilder builder, AuthoringResourceSelectionRef selection)
        {
            if (selection == null)
            {
                builder.Append("selection|null\n");
                return;
            }

            builder.Append("selection|")
                .Append(selection.ResourceStableId).Append('|')
                .Append(selection.SourceProviderId).Append('|')
                .Append(selection.BindingKind).Append('|')
                .Append(selection.ExpectedKind).Append('|')
                .Append(selection.ExpectedUsage).Append('|')
                .Append(selection.ExpectedHash).Append('|')
                .Append(selection.UnityGuid).Append('|')
                .Append(selection.UnityAssetPath).Append('|')
                .Append(selection.RuntimeResourceKey).Append('|')
                .Append(selection.ProviderResourceKey).Append('|')
                .Append(selection.PackageResourceKey).Append('|')
                .Append(selection.AudioCueId).Append('|')
                .Append(selection.AudioEventDefinitionId).Append('\n');
        }

        private static void AppendStringList(StringBuilder builder, List<string> values)
        {
            if (values == null || values.Count == 0)
            {
                builder.Append("[]\n");
                return;
            }

            var copy = new List<string>(values);
            copy.Sort(StringComparer.Ordinal);
            builder.Append('[');
            for (int i = 0; i < copy.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(copy[i]);
            }
            builder.Append("]\n");
        }

        private static void AppendIntList(StringBuilder builder, List<int> values)
        {
            if (values == null || values.Count == 0)
            {
                builder.Append("[]\n");
                return;
            }

            var copy = new List<int>(values);
            copy.Sort();
            builder.Append('[');
            for (int i = 0; i < copy.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(copy[i]);
            }
            builder.Append("]\n");
        }

        private static void AppendVector(StringBuilder builder, CharacterAuthoringVector3 value)
        {
            if (value == null)
            {
                builder.Append("null|null|null|");
                return;
            }

            builder.Append(value.X.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(value.Y.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(value.Z.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        }

        private static void AppendQuaternion(StringBuilder builder, CharacterAuthoringQuaternion value)
        {
            if (value == null)
            {
                builder.Append("null|null|null|null|");
                return;
            }

            builder.Append(value.X.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(value.Y.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(value.Z.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(value.W.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        }

        private static string CanonicalFieldValue(FieldValue value)
        {
            if (value == null)
                return string.Empty;
            if (value.Kind == FieldValueKind.Scalar)
                return value.Scalar ?? string.Empty;
            if (value.Kind == FieldValueKind.List)
            {
                var builder = new StringBuilder("[");
                if (value.List != null)
                {
                    for (int i = 0; i < value.List.Count; i++)
                    {
                        if (i > 0)
                            builder.Append(',');
                        builder.Append(CanonicalFieldValue(value.List[i]));
                    }
                }

                builder.Append(']');
                return builder.ToString();
            }

            var mapBuilder = new StringBuilder("{");
            if (value.Map != null)
            {
                var keys = new List<string>(value.Map.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (i > 0)
                        mapBuilder.Append(',');
                    mapBuilder.Append(keys[i]).Append(':').Append(CanonicalFieldValue(value.Map[keys[i]]));
                }
            }

            mapBuilder.Append('}');
            return mapBuilder.ToString();
        }

        private sealed class GeneratedIds
        {
            public int CharacterId { get; } = 710001;
            public int AttributeProfileId { get; } = 720001;
            public int BodyProfileId { get; } = 730001;
            public int BodyPartStartId { get; } = 740001;
            public int BodyPartContactPointStartId { get; } = 830001;
            public int EquipmentSchemaId { get; } = 750001;
            public int LoadoutStartId { get; } = 760001;
            public int StateStartId { get; } = 770001;
            public int WeaponStartId { get; } = 780001;
            public int BaseAbilityLoadoutId { get; } = 790001;
            public int WeaponAbilityLoadoutStartId { get; } = 790002;
            public int StateAbilityLoadoutStartId { get; } = 790100;
            public int ActionSetStartId { get; } = 800001;
            public int PresentationProfileId { get; } = 810001;
            public int SpawnProfileId { get; } = 820001;
            public int BaseAbilityId { get; } = 900001;
            public int WeaponAbilityStartId { get; } = 900002;
            public int StateAbilityStartId { get; } = 900100;
            public int HpAttributeId { get; } = 1;
            public int AttackAttributeId { get; } = 2;
            public int DefenseAttributeId { get; } = 3;
            public int StaminaAttributeId { get; } = 920002;
            public int DefaultLoadoutId { get; set; } = 760001;
            public int UnarmedStateId { get { return StateStartId; } }
            public int SingleWeaponStateId { get { return StateStartId + 1; } }
            public int AllWeaponsStateId { get { return StateStartId + 2; } }
        }

        private sealed class AbilitySlotBindingSpec
        {
            public AbilitySlotBindingSpec(string slotId, int abilityId, string inputIntentId)
            {
                SlotId = slotId ?? string.Empty;
                AbilityId = abilityId;
                InputIntentId = inputIntentId ?? string.Empty;
            }

            public string SlotId { get; }
            public int AbilityId { get; }
            public string InputIntentId { get; }
        }

        private sealed class AbilityLoadoutAuthoringMatch
        {
            public CharacterAbilityLoadoutAuthoringSummary Summary { get; set; }
            public int Index { get; set; }
        }

        private sealed class AbilityLoadoutAuthoringLookup
        {
            private readonly List<CharacterAbilityLoadoutAuthoringSummary> _items;

            private AbilityLoadoutAuthoringLookup(List<CharacterAbilityLoadoutAuthoringSummary> items)
            {
                _items = items ?? new List<CharacterAbilityLoadoutAuthoringSummary>();
            }

            public static AbilityLoadoutAuthoringLookup Create(List<CharacterAbilityLoadoutAuthoringSummary> items)
            {
                return new AbilityLoadoutAuthoringLookup(items);
            }

            public AbilityLoadoutAuthoringMatch Find(string sourceKind, string sourceStableId, string stableId, string loadoutId)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    CharacterAbilityLoadoutAuthoringSummary item = _items[i];
                    if (item == null)
                        continue;
                    if (!Matches(item, sourceKind, sourceStableId, stableId, loadoutId))
                        continue;
                    return new AbilityLoadoutAuthoringMatch { Summary = item, Index = i };
                }

                return null;
            }

            private static bool Matches(
                CharacterAbilityLoadoutAuthoringSummary item,
                string sourceKind,
                string sourceStableId,
                string stableId,
                string loadoutId)
            {
                if (EqualsOrdinal(item.StableId, stableId) || EqualsOrdinal(item.LoadoutId, loadoutId) || EqualsOrdinal(item.LoadoutId, stableId))
                    return true;

                if (!EqualsIgnoreCase(item.SourceKind, sourceKind))
                    return false;

                if (string.IsNullOrWhiteSpace(sourceStableId) || string.IsNullOrWhiteSpace(item.SourceStableId))
                    return true;

                return EqualsOrdinal(item.SourceStableId, sourceStableId);
            }

            private static bool EqualsOrdinal(string left, string right)
            {
                return !string.IsNullOrWhiteSpace(left)
                    && !string.IsNullOrWhiteSpace(right)
                    && string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);
            }

            private static bool EqualsIgnoreCase(string left, string right)
            {
                return !string.IsNullOrWhiteSpace(left)
                    && !string.IsNullOrWhiteSpace(right)
                    && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        private sealed class LoadoutPlan
        {
            public int DefaultLoadoutId { get; set; }
            public string DefaultLoadoutStableId { get; set; } = string.Empty;
            public Dictionary<string, int> StateIdByLoadoutStableId { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
            public Dictionary<string, string> StateStableIdByLoadoutStableId { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
            public Dictionary<string, int> ActionSetIdByLoadoutStableId { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
            public Dictionary<string, string> ActionSetStableIdByLoadoutStableId { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
            public Dictionary<string, CombinedWeaponConfigurationAuthoringSummary> ConfigurationByLoadoutStableId { get; } =
                new Dictionary<string, CombinedWeaponConfigurationAuthoringSummary>(StringComparer.Ordinal);
            public Dictionary<string, List<WeaponAttachmentProfile>> LoadoutAttachmentsByStableId { get; } = new Dictionary<string, List<WeaponAttachmentProfile>>(StringComparer.Ordinal);
            public List<string> OrderedLoadoutStableIds { get; } = new List<string>();
        }
    }
}
