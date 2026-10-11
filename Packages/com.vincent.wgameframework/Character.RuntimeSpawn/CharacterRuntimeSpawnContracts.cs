using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;

namespace MxFramework.CharacterRuntimeSpawn
{
    public static class CharacterAbstractBoneIds
    {
        public const string Root = "root";
        public const string BodyCenter = "body_center";
        public const string Head = "head";
        public const string Mouth = "mouth";
        public const string LeftHand = "left_hand";
        public const string RightHand = "right_hand";
        public const string LeftFoot = "left_foot";
        public const string RightFoot = "right_foot";
        public const string TailBase = "tail_base";
        public const string TailTip = "tail_tip";
        public const string LeftWing = "left_wing";
        public const string RightWing = "right_wing";
    }

    public enum CharacterRuntimeSpawnStatus
    {
        Success = 0,
        MissingImportedPackage = 1,
        ImportBlocked = 2,
        SpawnBlocked = 3,
        ResolveFailed = 4
    }

    public enum CharacterRuntimeSpawnIssueSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public static class CharacterRuntimeSpawnIssueCodes
    {
        public const string MissingImportedPackage = "CHAR_RUNTIME_MISSING_IMPORTED_PACKAGE";
        public const string ImportBlocked = "CHAR_RUNTIME_IMPORT_BLOCKED";
        public const string SpawnBlocked = "CHAR_RUNTIME_SPAWN_BLOCKED";
        public const string MissingConfig = "CHAR_RUNTIME_MISSING_CONFIG";
        public const string ResolverFailed = "CHAR_RUNTIME_RESOLVER_FAILED";
        public const string MissingResourceMapping = "CHAR_RUNTIME_MISSING_RESOURCE_MAPPING";
        public const string MissingWeaponAttachment = "CHAR_RUNTIME_MISSING_WEAPON_ATTACHMENT";
        public const string DeferredBinding = "CHAR_RUNTIME_DEFERRED_BINDING";
        public const string MissingCompiledAnimationArtifact = "CHAR_RUNTIME_MISSING_COMPILED_ANIMATION_ARTIFACT";
        public const string MissingAnimationProfile = "CHAR_RUNTIME_MISSING_ANIMATION_PROFILE";
        public const string AmbiguousAnimationProfile = "CHAR_RUNTIME_AMBIGUOUS_ANIMATION_PROFILE";
        public const string MissingLocomotionSlot = "CHAR_RUNTIME_MISSING_LOCOMOTION_SLOT";
        public const string InvalidLocomotionSlot = "CHAR_RUNTIME_INVALID_LOCOMOTION_SLOT";
        public const string MissingLocomotionSet = "CHAR_RUNTIME_MISSING_LOCOMOTION_SET";
        public const string MissingLocomotionGroup = "CHAR_RUNTIME_MISSING_LOCOMOTION_GROUP";
        public const string MissingLocomotionBlend = "CHAR_RUNTIME_MISSING_LOCOMOTION_BLEND";
        public const string AmbiguousLocomotionBlend = "CHAR_RUNTIME_AMBIGUOUS_LOCOMOTION_BLEND";
        public const string MissingLocomotionClip = "CHAR_RUNTIME_MISSING_LOCOMOTION_CLIP";
        public const string MissingAnimationResource = "CHAR_RUNTIME_MISSING_ANIMATION_RESOURCE";
    }

    public enum CharacterAnimationBlendKind
    {
        None = 0,
        Blend1D = 1,
        Blend2D = 2
    }

    public readonly struct CharacterRuntimeSpawnIssue
    {
        public CharacterRuntimeSpawnIssue(
            CharacterRuntimeSpawnIssueSeverity severity,
            string code,
            string sourcePath,
            string field,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            Field = field ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CharacterRuntimeSpawnIssueSeverity Severity { get; }
        public string Code { get; }
        public string SourcePath { get; }
        public string Field { get; }
        public string Message { get; }
        public bool IsError => Severity == CharacterRuntimeSpawnIssueSeverity.Error;
    }

    public sealed class CharacterUnityImportRuntimeReport
    {
        public CharacterUnityImportRuntimeReport(
            string packageId,
            string packageStableId,
            string status,
            bool canWriteToUnityProject,
            bool canSpawnAfterImport,
            string targetRootPath,
            string reportPath,
            string sourcePackageHash,
            string generatedConfigHash,
            string geometryBindingHash,
            string resourceMappingHash,
            string writePlanHash,
            int conflictCount,
            int errorCount)
        {
            PackageId = packageId ?? string.Empty;
            PackageStableId = packageStableId ?? string.Empty;
            Status = status ?? string.Empty;
            CanWriteToUnityProject = canWriteToUnityProject;
            CanSpawnAfterImport = canSpawnAfterImport;
            TargetRootPath = targetRootPath ?? string.Empty;
            ReportPath = reportPath ?? string.Empty;
            SourcePackageHash = sourcePackageHash ?? string.Empty;
            GeneratedConfigHash = generatedConfigHash ?? string.Empty;
            GeometryBindingHash = geometryBindingHash ?? string.Empty;
            ResourceMappingHash = resourceMappingHash ?? string.Empty;
            WritePlanHash = writePlanHash ?? string.Empty;
            ConflictCount = conflictCount;
            ErrorCount = errorCount;
        }

        public string PackageId { get; }
        public string PackageStableId { get; }
        public string Status { get; }
        public bool CanWriteToUnityProject { get; }
        public bool CanSpawnAfterImport { get; }
        public string TargetRootPath { get; }
        public string ReportPath { get; }
        public string SourcePackageHash { get; }
        public string GeneratedConfigHash { get; }
        public string GeometryBindingHash { get; }
        public string ResourceMappingHash { get; }
        public string WritePlanHash { get; }
        public int ConflictCount { get; }
        public int ErrorCount { get; }
    }

    public sealed class CharacterImportedPackage
    {
        public CharacterImportedPackage(
            string rootPath,
            string packageId,
            CharacterImportedConfigSet configs,
            CharacterImportedGeometryBinding geometry,
            CharacterImportedResourceMapping resourceMapping,
            ResourceCatalog unityResourceCatalog,
            CharacterUnityImportRuntimeReport importReport,
            ResourceCatalog runtimeResourceCatalog = null,
            CharacterResourcePlan runtimeResourcePlan = null,
            CharacterAudioCueManifest audioCueManifest = null,
            CharacterCompiledAnimationArtifacts compiledAnimationArtifacts = null,
            CharacterTacticalDefaults tacticalDefaults = null)
        {
            RootPath = rootPath ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            Configs = configs ?? CharacterImportedConfigSet.Empty;
            Geometry = geometry ?? CharacterImportedGeometryBinding.Empty;
            ResourceMapping = resourceMapping ?? CharacterImportedResourceMapping.Empty;
            UnityResourceCatalog = unityResourceCatalog;
            ImportReport = importReport;
            RuntimeResourceCatalog = runtimeResourceCatalog;
            RuntimeResourcePlan = runtimeResourcePlan;
            AudioCueManifest = audioCueManifest ?? CharacterAudioCueManifest.Empty;
            CompiledAnimationArtifacts = compiledAnimationArtifacts ?? CharacterCompiledAnimationArtifacts.Empty;
            TacticalDefaults = tacticalDefaults;
        }

        public string RootPath { get; }
        public string PackageId { get; }
        public CharacterImportedConfigSet Configs { get; }
        public CharacterImportedGeometryBinding Geometry { get; }
        public CharacterImportedResourceMapping ResourceMapping { get; }
        public ResourceCatalog UnityResourceCatalog { get; }
        public ResourceCatalog RuntimeResourceCatalog { get; }
        public CharacterResourcePlan RuntimeResourcePlan { get; }
        public CharacterAudioCueManifest AudioCueManifest { get; }
        public CharacterCompiledAnimationArtifacts CompiledAnimationArtifacts { get; }
        public CharacterUnityImportRuntimeReport ImportReport { get; }
        public CharacterTacticalDefaults TacticalDefaults { get; }
    }

    public sealed class CharacterCompiledAnimationArtifacts
    {
        public CharacterCompiledAnimationArtifacts(
            CharacterAnimationSetDefinition animationSetDefinition,
            CharacterAnimationClipRegistry animationClipRegistry,
            CharacterAnimationResourcePlan animationResourcePlan,
            CharacterRuntimeSpawnIssue[] diagnostics,
            IMxAnimationMappingProvider runtimeMappingProvider = null,
            MxAnimationClipRegistry runtimeClipRegistry = null)
        {
            AnimationSetDefinition = animationSetDefinition;
            AnimationClipRegistry = animationClipRegistry;
            AnimationResourcePlan = animationResourcePlan;
            Diagnostics = diagnostics ?? Array.Empty<CharacterRuntimeSpawnIssue>();
            RuntimeMappingProvider = runtimeMappingProvider ?? new MxAnimationStaticMappingProvider(null);
            RuntimeClipRegistry = runtimeClipRegistry ?? new MxAnimationClipRegistry(0, string.Empty, string.Empty, null);
        }

        public static CharacterCompiledAnimationArtifacts Empty { get; } = new CharacterCompiledAnimationArtifacts(null, null, null, Array.Empty<CharacterRuntimeSpawnIssue>());

        public CharacterAnimationSetDefinition AnimationSetDefinition { get; }
        public CharacterAnimationClipRegistry AnimationClipRegistry { get; }
        public CharacterAnimationResourcePlan AnimationResourcePlan { get; }
        public CharacterRuntimeSpawnIssue[] Diagnostics { get; }
        public IMxAnimationMappingProvider RuntimeMappingProvider { get; }
        public MxAnimationClipRegistry RuntimeClipRegistry { get; }
        public bool HasRequiredArtifacts => AnimationSetDefinition != null && AnimationClipRegistry != null && AnimationResourcePlan != null;
        public bool HasRuntimeAnimationContracts => RuntimeMappingProvider.Definitions.Count > 0 && RuntimeClipRegistry.Entries.Count > 0;
    }

    public sealed class CharacterAnimationSetDefinition
    {
        public CharacterAnimationSetDefinition(
            string format,
            string schemaVersion,
            string packageId,
            string stableId,
            string displayName,
            string skeletonProfileId,
            string avatarProfileId,
            CharacterAnimationSet[] sets,
            CharacterAnimationProfile[] profiles)
        {
            Format = format ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            StableId = stableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            SkeletonProfileId = skeletonProfileId ?? string.Empty;
            AvatarProfileId = avatarProfileId ?? string.Empty;
            Sets = sets ?? Array.Empty<CharacterAnimationSet>();
            Profiles = profiles ?? Array.Empty<CharacterAnimationProfile>();
        }

        public string Format { get; }
        public string SchemaVersion { get; }
        public string PackageId { get; }
        public string StableId { get; }
        public string DisplayName { get; }
        public string SkeletonProfileId { get; }
        public string AvatarProfileId { get; }
        public CharacterAnimationSet[] Sets { get; }
        public CharacterAnimationProfile[] Profiles { get; }
    }

    public sealed class CharacterAnimationSet
    {
        public CharacterAnimationSet(string setId, string displayName, string version, string defaultClipId, string fallbackClipId, CharacterAnimationGroup[] groups, CharacterAnimationActionBinding[] actionBindings)
        {
            SetId = setId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Version = version ?? string.Empty;
            DefaultClipId = defaultClipId ?? string.Empty;
            FallbackClipId = fallbackClipId ?? string.Empty;
            Groups = groups ?? Array.Empty<CharacterAnimationGroup>();
            ActionBindings = actionBindings ?? Array.Empty<CharacterAnimationActionBinding>();
        }

        public string SetId { get; }
        public string DisplayName { get; }
        public string Version { get; }
        public string DefaultClipId { get; }
        public string FallbackClipId { get; }
        public CharacterAnimationGroup[] Groups { get; }
        public CharacterAnimationActionBinding[] ActionBindings { get; }
    }

    public sealed class CharacterAnimationGroup
    {
        public CharacterAnimationGroup(
            string groupId,
            string displayName,
            string usage,
            CharacterAnimationClipReference[] clips,
            CharacterAnimationBlendReference[] blends = null)
        {
            GroupId = groupId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Usage = usage ?? string.Empty;
            Clips = clips ?? Array.Empty<CharacterAnimationClipReference>();
            Blends = blends ?? Array.Empty<CharacterAnimationBlendReference>();
        }

        public string GroupId { get; }
        public string DisplayName { get; }
        public string Usage { get; }
        public CharacterAnimationClipReference[] Clips { get; }
        public CharacterAnimationBlendReference[] Blends { get; }
    }

    public sealed class CharacterAnimationBlendReference
    {
        public CharacterAnimationBlendReference(
            string blendId,
            CharacterAnimationBlendKind blendKind,
            string defaultClipId,
            string[] clipIds)
        {
            BlendId = blendId ?? string.Empty;
            BlendKind = blendKind;
            DefaultClipId = defaultClipId ?? string.Empty;
            ClipIds = clipIds ?? Array.Empty<string>();
        }

        public string BlendId { get; }
        public CharacterAnimationBlendKind BlendKind { get; }
        public string DefaultClipId { get; }
        public string[] ClipIds { get; }
    }

    public sealed class CharacterAnimationClipReference
    {
        public CharacterAnimationClipReference(string clipId, string displayName, string runtimeResourceKey, string sourceClipName, string sourceSubClipId, bool loop, float speed, string rootMotionPolicy)
        {
            ClipId = clipId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            RuntimeResourceKey = runtimeResourceKey ?? string.Empty;
            SourceClipName = sourceClipName ?? string.Empty;
            SourceSubClipId = sourceSubClipId ?? string.Empty;
            Loop = loop;
            Speed = speed;
            RootMotionPolicy = rootMotionPolicy ?? string.Empty;
        }

        public string ClipId { get; }
        public string DisplayName { get; }
        public string RuntimeResourceKey { get; }
        public string SourceClipName { get; }
        public string SourceSubClipId { get; }
        public bool Loop { get; }
        public float Speed { get; }
        public string RootMotionPolicy { get; }
    }

    public sealed class CharacterAnimationActionBinding
    {
        public CharacterAnimationActionBinding(int bindingId, string actionId, string groupId, string clipId, string blendId, string timelineId, bool required)
        {
            BindingId = bindingId;
            ActionId = actionId ?? string.Empty;
            GroupId = groupId ?? string.Empty;
            ClipId = clipId ?? string.Empty;
            BlendId = blendId ?? string.Empty;
            TimelineId = timelineId ?? string.Empty;
            Required = required;
        }

        public int BindingId { get; }
        public string ActionId { get; }
        public string GroupId { get; }
        public string ClipId { get; }
        public string BlendId { get; }
        public string TimelineId { get; }
        public bool Required { get; }
    }

    public sealed class CharacterAnimationProfile
    {
        public CharacterAnimationProfile(
            string profileId,
            string displayName,
            string defaultSetId,
            string defaultGroupId,
            CharacterAnimationProfileSlot[] slots = null)
        {
            ProfileId = profileId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DefaultSetId = defaultSetId ?? string.Empty;
            DefaultGroupId = defaultGroupId ?? string.Empty;
            Slots = slots ?? Array.Empty<CharacterAnimationProfileSlot>();
        }

        public string ProfileId { get; }
        public string DisplayName { get; }
        public string DefaultSetId { get; }
        public string DefaultGroupId { get; }
        public CharacterAnimationProfileSlot[] Slots { get; }
    }

    public sealed class CharacterAnimationProfileSlot
    {
        public CharacterAnimationProfileSlot(
            string slotId,
            string displayName,
            string purpose,
            string setId,
            string groupId,
            string defaultClipId,
            string defaultBlendId,
            string preloadPolicy,
            bool required)
        {
            SlotId = slotId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Purpose = purpose ?? string.Empty;
            SetId = setId ?? string.Empty;
            GroupId = groupId ?? string.Empty;
            DefaultClipId = defaultClipId ?? string.Empty;
            DefaultBlendId = defaultBlendId ?? string.Empty;
            PreloadPolicy = preloadPolicy ?? string.Empty;
            Required = required;
        }

        public string SlotId { get; }
        public string DisplayName { get; }
        public string Purpose { get; }
        public string SetId { get; }
        public string GroupId { get; }
        public string DefaultClipId { get; }
        public string DefaultBlendId { get; }
        public string PreloadPolicy { get; }
        public bool Required { get; }
    }

    public readonly struct CharacterRuntimeAnimationProfileBinding
    {
        public CharacterRuntimeAnimationProfileBinding(
            string profileId,
            string slotId,
            string setId,
            string groupId,
            string blendId,
            CharacterAnimationBlendKind blendKind,
            string resourcePackageId)
        {
            ProfileId = profileId ?? string.Empty;
            SlotId = slotId ?? string.Empty;
            SetId = setId ?? string.Empty;
            GroupId = groupId ?? string.Empty;
            BlendId = blendId ?? string.Empty;
            BlendKind = blendKind;
            ResourcePackageId = resourcePackageId ?? string.Empty;
        }

        public string ProfileId { get; }
        public string SlotId { get; }
        public string SetId { get; }
        public string GroupId { get; }
        public string BlendId { get; }
        public CharacterAnimationBlendKind BlendKind { get; }
        public string ResourcePackageId { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(ProfileId)
            && string.Equals(SlotId, CharacterRuntimeAnimationProfileResolver.LocomotionSlotId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(SetId)
            && !string.IsNullOrWhiteSpace(GroupId)
            && !string.IsNullOrWhiteSpace(BlendId)
            && BlendKind != CharacterAnimationBlendKind.None
            && !string.IsNullOrWhiteSpace(ResourcePackageId);
    }

    public sealed class CharacterRuntimeAnimationProfileResolveResult
    {
        public CharacterRuntimeAnimationProfileResolveResult(
            CharacterRuntimeAnimationProfileBinding binding,
            string issueCode,
            string field,
            string message)
        {
            Binding = binding;
            IssueCode = issueCode ?? string.Empty;
            Field = field ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CharacterRuntimeAnimationProfileBinding Binding { get; }
        public string IssueCode { get; }
        public string Field { get; }
        public string Message { get; }
        public bool IsSuccess => Binding.IsValid && string.IsNullOrEmpty(IssueCode);
    }

    public sealed class CharacterAnimationClipRegistry
    {
        public CharacterAnimationClipRegistry(string format, string schemaVersion, string packageId, CharacterAnimationClipRegistryEntry[] clips)
        {
            Format = format ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            Clips = clips ?? Array.Empty<CharacterAnimationClipRegistryEntry>();
        }

        public string Format { get; }
        public string SchemaVersion { get; }
        public string PackageId { get; }
        public CharacterAnimationClipRegistryEntry[] Clips { get; }
    }

    public sealed class CharacterAnimationClipRegistryEntry
    {
        public CharacterAnimationClipRegistryEntry(string setId, string groupId, string clipId, string displayName, string sourceClipName, string sourceSubClipId, string runtimeResourceKey)
        {
            SetId = setId ?? string.Empty;
            GroupId = groupId ?? string.Empty;
            ClipId = clipId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            SourceClipName = sourceClipName ?? string.Empty;
            SourceSubClipId = sourceSubClipId ?? string.Empty;
            RuntimeResourceKey = runtimeResourceKey ?? string.Empty;
        }

        public string SetId { get; }
        public string GroupId { get; }
        public string ClipId { get; }
        public string DisplayName { get; }
        public string SourceClipName { get; }
        public string SourceSubClipId { get; }
        public string RuntimeResourceKey { get; }
    }

    public sealed class CharacterAnimationResourcePlan
    {
        public CharacterAnimationResourcePlan(string format, string schemaVersion, string packageId, string stableId, string planHash)
        {
            Format = format ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            StableId = stableId ?? string.Empty;
            PlanHash = planHash ?? string.Empty;
        }

        public string Format { get; }
        public string SchemaVersion { get; }
        public string PackageId { get; }
        public string StableId { get; }
        public string PlanHash { get; }
    }

    public sealed class CharacterImportedConfigSet
    {
        public CharacterImportedConfigSet(
            CharacterConfig[] characters,
            CharacterAttributeProfileConfig[] attributeProfiles,
            CharacterBodyProfileConfig[] bodyProfiles,
            CharacterBodyPartConfig[] bodyParts,
            CharacterBodyPartContactPointConfig[] bodyPartContactPoints,
            EquipmentSchemaConfig[] equipmentSchemas,
            EquipmentLoadoutConfig[] loadouts,
            EquipmentStateConfig[] equipmentStates,
            WeaponConfig[] weapons,
            AbilityLoadoutConfig[] abilityLoadouts,
            CombatActionSetConfig[] combatActionSets,
            CharacterPresentationProfileConfig[] presentationProfiles,
            SpawnProfileConfig[] spawnProfiles,
            int runtimeCharacterId = 0)
        {
            Characters = characters ?? Array.Empty<CharacterConfig>();
            AttributeProfiles = attributeProfiles ?? Array.Empty<CharacterAttributeProfileConfig>();
            BodyProfiles = bodyProfiles ?? Array.Empty<CharacterBodyProfileConfig>();
            BodyParts = bodyParts ?? Array.Empty<CharacterBodyPartConfig>();
            BodyPartContactPoints = bodyPartContactPoints ?? Array.Empty<CharacterBodyPartContactPointConfig>();
            EquipmentSchemas = equipmentSchemas ?? Array.Empty<EquipmentSchemaConfig>();
            Loadouts = loadouts ?? Array.Empty<EquipmentLoadoutConfig>();
            EquipmentStates = equipmentStates ?? Array.Empty<EquipmentStateConfig>();
            Weapons = weapons ?? Array.Empty<WeaponConfig>();
            AbilityLoadouts = abilityLoadouts ?? Array.Empty<AbilityLoadoutConfig>();
            CombatActionSets = combatActionSets ?? Array.Empty<CombatActionSetConfig>();
            PresentationProfiles = presentationProfiles ?? Array.Empty<CharacterPresentationProfileConfig>();
            SpawnProfiles = spawnProfiles ?? Array.Empty<SpawnProfileConfig>();
            RuntimeCharacterId = runtimeCharacterId;
        }

        public static CharacterImportedConfigSet Empty { get; } = new CharacterImportedConfigSet(
            Array.Empty<CharacterConfig>(),
            Array.Empty<CharacterAttributeProfileConfig>(),
            Array.Empty<CharacterBodyProfileConfig>(),
            Array.Empty<CharacterBodyPartConfig>(),
            Array.Empty<CharacterBodyPartContactPointConfig>(),
            Array.Empty<EquipmentSchemaConfig>(),
            Array.Empty<EquipmentLoadoutConfig>(),
            Array.Empty<EquipmentStateConfig>(),
            Array.Empty<WeaponConfig>(),
            Array.Empty<AbilityLoadoutConfig>(),
            Array.Empty<CombatActionSetConfig>(),
            Array.Empty<CharacterPresentationProfileConfig>(),
            Array.Empty<SpawnProfileConfig>());

        public int RuntimeCharacterId { get; }
        public CharacterConfig[] Characters { get; }
        public CharacterAttributeProfileConfig[] AttributeProfiles { get; }
        public CharacterBodyProfileConfig[] BodyProfiles { get; }
        public CharacterBodyPartConfig[] BodyParts { get; }
        public CharacterBodyPartContactPointConfig[] BodyPartContactPoints { get; }
        public EquipmentSchemaConfig[] EquipmentSchemas { get; }
        public EquipmentLoadoutConfig[] Loadouts { get; }
        public EquipmentStateConfig[] EquipmentStates { get; }
        public WeaponConfig[] Weapons { get; }
        public AbilityLoadoutConfig[] AbilityLoadouts { get; }
        public CombatActionSetConfig[] CombatActionSets { get; }
        public CharacterPresentationProfileConfig[] PresentationProfiles { get; }
        public SpawnProfileConfig[] SpawnProfiles { get; }

        public SpawnProfileConfig FindSpawnProfile(SpawnProfileId id)
        {
            for (int i = 0; i < SpawnProfiles.Length; i++)
            {
                if (SpawnProfiles[i].SpawnProfileId.Equals(id))
                    return SpawnProfiles[i];
            }

            return null;
        }

        public SpawnProfileConfig GetDefaultSpawnProfile()
        {
            return SpawnProfiles.Length == 0 ? null : SpawnProfiles[0];
        }

        public CharacterConfig FindCharacter(CharacterConfigId id)
        {
            for (int i = 0; i < Characters.Length; i++)
            {
                if (Characters[i].CharacterId.Equals(id))
                    return Characters[i];
            }

            return null;
        }

        public CharacterAttributeProfileConfig FindAttributeProfile(CharacterAttributeProfileId id)
        {
            for (int i = 0; i < AttributeProfiles.Length; i++)
            {
                if (AttributeProfiles[i].AttributeProfileId.Equals(id))
                    return AttributeProfiles[i];
            }

            return null;
        }

        public CharacterBodyProfileConfig FindBodyProfile(CharacterBodyProfileId id)
        {
            for (int i = 0; i < BodyProfiles.Length; i++)
            {
                if (BodyProfiles[i].BodyProfileId.Equals(id))
                    return BodyProfiles[i];
            }

            return null;
        }

        public EquipmentSchemaConfig FindEquipmentSchema(EquipmentSchemaId id)
        {
            for (int i = 0; i < EquipmentSchemas.Length; i++)
            {
                if (EquipmentSchemas[i].EquipmentSchemaId.Equals(id))
                    return EquipmentSchemas[i];
            }

            return null;
        }

        public EquipmentLoadoutConfig FindLoadout(EquipmentLoadoutId id)
        {
            for (int i = 0; i < Loadouts.Length; i++)
            {
                if (Loadouts[i].LoadoutId.Equals(id))
                    return Loadouts[i];
            }

            return null;
        }

        public CharacterPresentationProfileConfig FindPresentationProfile(CharacterPresentationProfileId id)
        {
            for (int i = 0; i < PresentationProfiles.Length; i++)
            {
                if (PresentationProfiles[i].PresentationProfileId.Equals(id))
                    return PresentationProfiles[i];
            }

            return null;
        }
    }

    public readonly struct CharacterRuntimeVector3
    {
        public CharacterRuntimeVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
    }

    public readonly struct CharacterRuntimeIntVector3
    {
        public CharacterRuntimeIntVector3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; }
        public int Y { get; }
        public int Z { get; }
    }

    public readonly struct CharacterRuntimeQuaternion
    {
        public CharacterRuntimeQuaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float W { get; }
    }

    public readonly struct CharacterRuntimePose
    {
        /// <summary>NoAlloc: identity pose in model-root space.</summary>
        public static CharacterRuntimePose Identity { get; } = new CharacterRuntimePose(
            "ModelRoot", string.Empty,
            new CharacterRuntimeVector3(0f, 0f, 0f),
            new CharacterRuntimeQuaternion(0f, 0f, 0f, 1f),
            new CharacterRuntimeVector3(1f, 1f, 1f));

        public CharacterRuntimePose(
            string parentKind,
            string parentPath,
            CharacterRuntimeVector3 position,
            CharacterRuntimeQuaternion rotation,
            CharacterRuntimeVector3 scale)
        {
            ParentKind = parentKind ?? string.Empty;
            ParentPath = parentPath ?? string.Empty;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public string ParentKind { get; }
        public string ParentPath { get; }
        public CharacterRuntimeVector3 Position { get; }
        public CharacterRuntimeQuaternion Rotation { get; }
        public CharacterRuntimeVector3 Scale { get; }
    }

    public sealed partial class CharacterImportedGeometryBinding
    {
        public CharacterImportedGeometryBinding(
            string packageId,
            CharacterBodyGeometryRuntimeProfile bodyProfile,
            CharacterBodyColliderRuntimeBinding[] bodyColliders,
            CharacterHitZoneRuntimeBinding[] hitZoneBindings,
            CharacterSocketRuntimeBinding[] sockets,
            CharacterWeaponAttachmentRuntimeBinding[] weaponAttachments,
            CharacterWeaponTraceRuntimeBinding[] weaponTraces)
            : this(
                "mx.characterGeometryBinding.v1",
                "1.0",
                packageId,
                bodyProfile,
                bodyColliders,
                hitZoneBindings,
                Array.Empty<CharacterDamageZoneRuntimeBinding>(),
                sockets,
                weaponAttachments,
                weaponTraces)
        {
        }

        public CharacterImportedGeometryBinding(
            string format,
            string schemaVersion,
            string packageId,
            CharacterBodyGeometryRuntimeProfile bodyProfile,
            CharacterBodyColliderRuntimeBinding[] bodyColliders,
            CharacterHitZoneRuntimeBinding[] hitZoneBindings,
            CharacterDamageZoneRuntimeBinding[] damageZones,
            CharacterSocketRuntimeBinding[] sockets,
            CharacterWeaponAttachmentRuntimeBinding[] weaponAttachments,
            CharacterWeaponTraceRuntimeBinding[] weaponTraces,
            CharacterEngagementAnchorRuntimeBinding[] engagementAnchors = null,
            int engagementAnchorRevision = 0,
            string engagementAnchorContentHash = "",
            CharacterEngagementAnchorMode engagementAnchorMode =
                CharacterEngagementAnchorMode.CompatibilityRootApproach,
            CharacterBodyPresenceRuntimeBinding[] bodyPresenceVolumes = null,
            int bodyPresenceRevision = 0,
            string bodyPresenceContentHash = "",
            CharacterBodyPresenceMode bodyPresenceMode =
                CharacterBodyPresenceMode.CompatibilityHardRoot,
            CharacterBodyPartDamageRuntimeBinding[] bodyPartDamageDefinitions = null,
            CharacterBodyPartDamageLookupRuntimeBinding[] bodyPartDamagePartLookup = null,
            string bodyPartDamageFormat = "",
            string bodyPartDamageSchemaVersion = "",
            int bodyPartDamageRevision = 0,
            string bodyPartDamageContentHash = "",
            CharacterBodyPartDamageMode bodyPartDamageMode = CharacterBodyPartDamageMode.Missing,
            CharacterAuthorityPoseRigRuntimeBinding authorityPoseRig = null,
            CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding[] animatedHurtboxSources = null)
        {
            Format = format ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            BodyProfile = bodyProfile;
            BodyColliders = bodyColliders ?? Array.Empty<CharacterBodyColliderRuntimeBinding>();
            HitZoneBindings = hitZoneBindings ?? Array.Empty<CharacterHitZoneRuntimeBinding>();
            DamageZones = damageZones ?? Array.Empty<CharacterDamageZoneRuntimeBinding>();
            Sockets = sockets ?? Array.Empty<CharacterSocketRuntimeBinding>();
            WeaponAttachments = weaponAttachments ?? Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();
            WeaponTraces = weaponTraces ?? Array.Empty<CharacterWeaponTraceRuntimeBinding>();
            EngagementAnchors = engagementAnchors ?? Array.Empty<CharacterEngagementAnchorRuntimeBinding>();
            EngagementAnchorRevision = engagementAnchorRevision;
            EngagementAnchorContentHash = engagementAnchorContentHash ?? string.Empty;
            EngagementAnchorContentDigest = RuntimePreparedDigest.PrepareExisting(EngagementAnchorContentHash);
            EngagementAnchorMode = engagementAnchorMode;
            BodyPresenceVolumes = bodyPresenceVolumes ?? Array.Empty<CharacterBodyPresenceRuntimeBinding>();
            BodyPresenceRevision = bodyPresenceRevision;
            BodyPresenceContentHash = bodyPresenceContentHash ?? string.Empty;
            BodyPresenceContentDigest = RuntimePreparedDigest.PrepareExisting(bodyPresenceContentHash);
            BodyPresenceMode = bodyPresenceMode;
            AnimatedHurtboxSources = animatedHurtboxSources
                ?? Array.Empty<CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding>();
            BodyPartDamageDefinitions = bodyPartDamageDefinitions
                ?? Array.Empty<CharacterBodyPartDamageRuntimeBinding>();
            BodyPartDamagePartLookup = bodyPartDamagePartLookup
                ?? Array.Empty<CharacterBodyPartDamageLookupRuntimeBinding>();
            BodyPartDamageFormat = bodyPartDamageFormat ?? string.Empty;
            BodyPartDamageSchemaVersion = bodyPartDamageSchemaVersion ?? string.Empty;
            BodyPartDamageRevision = bodyPartDamageRevision;
            BodyPartDamageContentHash = bodyPartDamageContentHash ?? string.Empty;
            BodyPartDamageMode = bodyPartDamageMode;
            AuthorityPoseRig = authorityPoseRig ?? CharacterAuthorityPoseRigRuntimeBinding.Missing;
        }

        public static CharacterImportedGeometryBinding Empty { get; } = new CharacterImportedGeometryBinding(
            string.Empty,
            null,
            Array.Empty<CharacterBodyColliderRuntimeBinding>(),
            Array.Empty<CharacterHitZoneRuntimeBinding>(),
            Array.Empty<CharacterSocketRuntimeBinding>(),
            Array.Empty<CharacterWeaponAttachmentRuntimeBinding>(),
            Array.Empty<CharacterWeaponTraceRuntimeBinding>());

        public string PackageId { get; }
        public string Format { get; }
        public string SchemaVersion { get; }
        public CharacterBodyGeometryRuntimeProfile BodyProfile { get; }
        public CharacterBodyColliderRuntimeBinding[] BodyColliders { get; }
        public CharacterHitZoneRuntimeBinding[] HitZoneBindings { get; }
        public CharacterDamageZoneRuntimeBinding[] DamageZones { get; }
        public CharacterEngagementAnchorRuntimeBinding[] EngagementAnchors { get; }
        public int EngagementAnchorRevision { get; }
        public string EngagementAnchorContentHash { get; }
        public RuntimePreparedDigest EngagementAnchorContentDigest { get; }
        public CharacterEngagementAnchorMode EngagementAnchorMode { get; }
        public CharacterBodyPresenceRuntimeBinding[] BodyPresenceVolumes { get; }
        public int BodyPresenceRevision { get; }
        public string BodyPresenceContentHash { get; }
        public RuntimePreparedDigest BodyPresenceContentDigest { get; }
        public CharacterBodyPresenceMode BodyPresenceMode { get; }
        public CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding[] AnimatedHurtboxSources { get; }
        public CharacterBodyPartDamageRuntimeBinding[] BodyPartDamageDefinitions { get; }
        public CharacterBodyPartDamageLookupRuntimeBinding[] BodyPartDamagePartLookup { get; }
        public string BodyPartDamageFormat { get; }
        public string BodyPartDamageSchemaVersion { get; }
        public int BodyPartDamageRevision { get; }
        public string BodyPartDamageContentHash { get; }
        public CharacterBodyPartDamageMode BodyPartDamageMode { get; }
        public CharacterAuthorityPoseRigRuntimeBinding AuthorityPoseRig { get; }
        public CharacterSocketRuntimeBinding[] Sockets { get; }
        public CharacterWeaponAttachmentRuntimeBinding[] WeaponAttachments { get; }
        public CharacterWeaponTraceRuntimeBinding[] WeaponTraces { get; }

        public float AuthorCapsuleCenterX => BodyProfile?.CapsuleCenter.X ?? 0f;
        public float AuthorCapsuleCenterZ => BodyProfile?.CapsuleCenter.Z ?? 0f;
        public float RuntimeConsumedCapsuleCenterX => 0f;
        public float RuntimeConsumedCapsuleCenterZ => 0f;
        public bool RuntimeConsumesAuthorCapsuleCenterXZ => false;

    }

    public sealed class CharacterBodyGeometryRuntimeProfile
    {
        public CharacterBodyGeometryRuntimeProfile(
            string profileId,
            string bodyKind,
            float bodyScale,
            float heightMeters,
            float radiusMeters,
            float massKg,
            float capsuleHeight,
            float capsuleRadius,
            CharacterRuntimeVector3 capsuleCenter)
        {
            ProfileId = profileId ?? string.Empty;
            BodyKind = bodyKind ?? string.Empty;
            BodyScale = bodyScale;
            HeightMeters = heightMeters;
            RadiusMeters = radiusMeters;
            MassKg = massKg;
            CapsuleHeight = capsuleHeight;
            CapsuleRadius = capsuleRadius;
            CapsuleCenter = capsuleCenter;
        }

        public string ProfileId { get; }
        public string BodyKind { get; }
        public float BodyScale { get; }
        public float HeightMeters { get; }
        public float RadiusMeters { get; }
        public float MassKg { get; }
        public float CapsuleHeight { get; }
        public float CapsuleRadius { get; }
        public CharacterRuntimeVector3 CapsuleCenter { get; }
    }

    public sealed class CharacterBodyColliderRuntimeBinding
    {
        public CharacterBodyColliderRuntimeBinding(
            int colliderId,
            int partId,
            int hitZoneId,
            string shape,
            CharacterRuntimePose localPose,
            CharacterRuntimeVector3 size,
            float radius,
            float height,
            int priority,
            bool isWeakPoint,
            string physicsLayer,
            float damageMultiplierOverride = 0f,
            float impactPowerScaleOverride = 0f,
            string materialStableId = "",
            string partLabel = "",
            string hitZoneLabel = "",
            string colliderLabel = "")
        {
            ColliderId = colliderId;
            ColliderLabel = colliderLabel ?? string.Empty;
            PartId = partId;
            PartLabel = partLabel ?? string.Empty;
            HitZoneId = hitZoneId;
            HitZoneLabel = hitZoneLabel ?? string.Empty;
            Shape = shape ?? string.Empty;
            LocalPose = localPose;
            Size = size;
            Radius = radius;
            Height = height;
            Priority = priority;
            IsWeakPoint = isWeakPoint;
            PhysicsLayer = physicsLayer ?? string.Empty;
            DamageMultiplierOverride = damageMultiplierOverride;
            ImpactPowerScaleOverride = impactPowerScaleOverride;
            MaterialStableId = materialStableId ?? string.Empty;
        }

        public int ColliderId { get; }
        public string ColliderLabel { get; }
        public int PartId { get; }
        public string PartLabel { get; }
        public int HitZoneId { get; }
        public string HitZoneLabel { get; }
        public string Shape { get; }
        public CharacterRuntimePose LocalPose { get; }
        public CharacterRuntimeVector3 Size { get; }
        public float Radius { get; }
        public float Height { get; }
        public int Priority { get; }
        public bool IsWeakPoint { get; }
        public string PhysicsLayer { get; }
        public float DamageMultiplierOverride { get; }
        public float ImpactPowerScaleOverride { get; }
        public string MaterialStableId { get; }
    }

    public sealed class CharacterHitZoneRuntimeBinding
    {
        public CharacterHitZoneRuntimeBinding(
            int hitZoneId,
            int partId,
            int priority,
            bool isWeakPoint,
            float damageMultiplierOverride = 0f,
            float impactPowerScaleOverride = 0f,
            string hitZoneLabel = "",
            string partLabel = "")
        {
            HitZoneId = hitZoneId;
            HitZoneLabel = hitZoneLabel ?? string.Empty;
            PartId = partId;
            PartLabel = partLabel ?? string.Empty;
            Priority = priority;
            IsWeakPoint = isWeakPoint;
            DamageMultiplierOverride = damageMultiplierOverride;
            ImpactPowerScaleOverride = impactPowerScaleOverride;
        }

        public int HitZoneId { get; }
        public string HitZoneLabel { get; }
        public int PartId { get; }
        public string PartLabel { get; }
        public int Priority { get; }
        public bool IsWeakPoint { get; }
        public float DamageMultiplierOverride { get; }
        public float ImpactPowerScaleOverride { get; }
    }

    public sealed class CharacterDamageZoneRuntimeBinding
    {
        public CharacterDamageZoneRuntimeBinding(
            int zoneId,
            string displayName,
            int[] hitZoneIds,
            bool hittable,
            bool aimable,
            bool approachable,
            int authoredPriority,
            string zoneLabel = "")
        {
            ZoneId = zoneId;
            ZoneLabel = zoneLabel ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            HitZoneIds = hitZoneIds ?? Array.Empty<int>();
            Hittable = hittable;
            Aimable = aimable;
            Approachable = approachable;
            AuthoredPriority = authoredPriority;
        }

        public int ZoneId { get; }
        public string ZoneLabel { get; }
        public string DisplayName { get; }
        public int[] HitZoneIds { get; }
        public bool Hittable { get; }
        public bool Aimable { get; }
        public bool Approachable { get; }
        public int AuthoredPriority { get; }
    }

    public enum CharacterEngagementAnchorMode
    {
        CompatibilityRootApproach = 0,
        AuthoredAnchors = 1
    }

    public enum CharacterEngagementAnchorReferenceFrame
    {
        ModelRoot = 1
    }

    public sealed class CharacterEngagementAnchorRuntimeBinding
    {
        public const int DefaultPhaseId = 33;
        public CharacterEngagementAnchorRuntimeBinding(
            int anchorId,
            int zoneId,
            CharacterEngagementAnchorReferenceFrame referenceFrame,
            CharacterRuntimeIntVector3 localPositionMillimeters,
            int yawMilliDegrees,
            int capacity,
            int authoredPriority,
            bool enabled,
            int phaseId,
            string anchorLabel = "",
            string zoneLabel = "",
            string phaseLabel = "")
        {
            AnchorId = anchorId;
            AnchorLabel = anchorLabel ?? string.Empty;
            ZoneId = zoneId;
            ZoneLabel = zoneLabel ?? string.Empty;
            ReferenceFrame = referenceFrame;
            LocalPositionMillimeters = localPositionMillimeters;
            YawMilliDegrees = yawMilliDegrees;
            Capacity = capacity;
            AuthoredPriority = authoredPriority;
            Enabled = enabled;
            PhaseId = phaseId;
            PhaseLabel = phaseLabel ?? string.Empty;
        }

        public int AnchorId { get; }
        public string AnchorLabel { get; }
        public int ZoneId { get; }
        public string ZoneLabel { get; }
        public CharacterEngagementAnchorReferenceFrame ReferenceFrame { get; }
        public CharacterRuntimeIntVector3 LocalPositionMillimeters { get; }
        public int YawMilliDegrees { get; }
        public int Capacity { get; }
        public int AuthoredPriority { get; }
        public bool Enabled { get; }
        public int PhaseId { get; }
        public string PhaseLabel { get; }
    }

    public enum CharacterBodyPresenceMode
    {
        CompatibilityHardRoot = 0,
        Authored = 1
    }

    public enum CharacterBodyPresenceReferenceFrame
    {
        ModelRoot = 1
    }

    public enum CharacterBodyPresenceShape
    {
        Sphere = 1,
        Capsule = 2,
        Box = 3
    }

    public sealed class CharacterBodyPresenceRuntimeBinding
    {
        public CharacterBodyPresenceRuntimeBinding(
            string volumeId,
            CharacterBodyPresenceReferenceFrame referenceFrame,
            CharacterBodyPresenceShape shape,
            CharacterRuntimePose localPose,
            float radius,
            float height,
            CharacterRuntimeVector3 size,
            int skinMillimeters,
            int pressureScaleMilli,
            bool enabled)
        {
            VolumeId = volumeId ?? string.Empty;
            ReferenceFrame = referenceFrame;
            Shape = shape;
            LocalPose = localPose;
            Radius = radius;
            Height = height;
            Size = size;
            SkinMillimeters = skinMillimeters;
            PressureScaleMilli = pressureScaleMilli;
            Enabled = enabled;
        }

        public string VolumeId { get; }
        public CharacterBodyPresenceReferenceFrame ReferenceFrame { get; }
        public CharacterBodyPresenceShape Shape { get; }
        public CharacterRuntimePose LocalPose { get; }
        public float Radius { get; }
        public float Height { get; }
        public CharacterRuntimeVector3 Size { get; }
        public int SkinMillimeters { get; }
        public int PressureScaleMilli { get; }
        public bool Enabled { get; }
    }

    public sealed class CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding
    {
        public CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding(
            string sourceId,
            int colliderId,
            int skinMillimeters,
            int pressureScaleMilli,
            bool enabled, string colliderLabel = "")
        {
            SourceId = sourceId ?? string.Empty;
            ColliderId = colliderId;
            ColliderLabel = colliderLabel ?? string.Empty;
            SkinMillimeters = skinMillimeters;
            PressureScaleMilli = pressureScaleMilli;
            Enabled = enabled;
        }

        public string SourceId { get; }
        public int ColliderId { get; }
        public string ColliderLabel { get; }
        public int SkinMillimeters { get; }
        public int PressureScaleMilli { get; }
        public bool Enabled { get; }
    }

    public enum CharacterBodyPartDamageMode
    {
        Missing = 0,
        Authored = 1
    }

    public sealed class CharacterBodyPartDamageRuntimeBinding
    {
        public CharacterBodyPartDamageRuntimeBinding(
            int definitionIndex,
            int partStateId,
            int[] memberPartIds,
            int damageZoneId,
            int woundCapacity,
            int openDamageScaleMilli,
            string focusConsumeTag,
            int maxIntegrity,
            int integrityDamageScaleMilli,
            string breakReactionId,
            int authoredPriorityDeltaOpen,
            int authoredPriorityDeltaBroken,
            bool severable = false,
            string severRequiredTag = "",
            string presentationEventId = "",
            string partStateLabel = "",
            string damageZoneLabel = "",
            string[] memberPartLabels = null)
        {
            DefinitionIndex = definitionIndex;
            PartStateId = partStateId;
            PartStateLabel = partStateLabel ?? string.Empty;
            MemberPartIds = memberPartIds ?? Array.Empty<int>();
            MemberPartLabels = memberPartLabels ?? Array.Empty<string>();
            DamageZoneId = damageZoneId;
            DamageZoneLabel = damageZoneLabel ?? string.Empty;
            WoundCapacity = woundCapacity;
            OpenDamageScaleMilli = openDamageScaleMilli;
            FocusConsumeTag = focusConsumeTag ?? string.Empty;
            MaxIntegrity = maxIntegrity;
            IntegrityDamageScaleMilli = integrityDamageScaleMilli;
            BreakReactionId = breakReactionId ?? string.Empty;
            AuthoredPriorityDeltaOpen = authoredPriorityDeltaOpen;
            AuthoredPriorityDeltaBroken = authoredPriorityDeltaBroken;
            Severable = severable;
            SeverRequiredTag = severRequiredTag ?? string.Empty;
            PresentationEventId = presentationEventId ?? string.Empty;
        }

        public int DefinitionIndex { get; }
        public int PartStateId { get; }
        public string PartStateLabel { get; }
        public int[] MemberPartIds { get; }
        public string[] MemberPartLabels { get; }
        public int DamageZoneId { get; }
        public string DamageZoneLabel { get; }
        public int WoundCapacity { get; }
        public int OpenDamageScaleMilli { get; }
        public string FocusConsumeTag { get; }
        public int MaxIntegrity { get; }
        public int IntegrityDamageScaleMilli { get; }
        public string BreakReactionId { get; }
        public int AuthoredPriorityDeltaOpen { get; }
        public int AuthoredPriorityDeltaBroken { get; }
        public bool Severable { get; }
        public string SeverRequiredTag { get; }
        public string PresentationEventId { get; }
    }

    public sealed class CharacterBodyPartDamageLookupRuntimeBinding
    {
        public CharacterBodyPartDamageLookupRuntimeBinding(int partId, int definitionIndex,
            string partLabel = "")
        {
            PartId = partId;
            PartLabel = partLabel ?? string.Empty;
            DefinitionIndex = definitionIndex;
        }

        public int PartId { get; }
        public string PartLabel { get; }
        public int DefinitionIndex { get; }
    }

    public sealed class CharacterSocketRuntimeBinding
    {
        public CharacterSocketRuntimeBinding(int socketId, string parentPartId, string bonePath, string locatorPath, CharacterRuntimePose localPose, string usage, string socketLabel = "")
            : this(socketId, string.Empty, parentPartId, bonePath, locatorPath, localPose, usage, socketLabel)
        {
        }

        public CharacterSocketRuntimeBinding(int socketId, string abstractBoneId, string parentPartId, string bonePath, string locatorPath, CharacterRuntimePose localPose, string usage, string socketLabel = "")
        {
            SocketId = socketId;
            SocketLabel = socketLabel ?? string.Empty;
            AbstractBoneId = abstractBoneId ?? string.Empty;
            ParentPartId = parentPartId;
            BonePath = bonePath ?? string.Empty;
            LocatorPath = locatorPath ?? string.Empty;
            LocalPose = localPose;
            Usage = usage ?? string.Empty;
        }

        public int SocketId { get; }
        public string SocketLabel { get; }
        public string AbstractBoneId { get; }
        public string ParentPartId { get; }
        public string BonePath { get; }
        public string LocatorPath { get; }
        public CharacterRuntimePose LocalPose { get; }
        public string Usage { get; }
    }

    public sealed class CharacterWeaponAttachmentRuntimeBinding
    {
        public CharacterWeaponAttachmentRuntimeBinding(int weaponId, int equipSlot, int attachSocketId, CharacterRuntimePose localGripPose, string previewResourceKey, int traceId)
            : this(weaponId, equipSlot, attachSocketId, localGripPose, previewResourceKey, traceId, string.Empty, string.Empty, string.Empty, 0, 0, 0f, string.Empty)
        {
        }

        public CharacterWeaponAttachmentRuntimeBinding(
            int weaponId,
            int equipSlot,
            int attachSocketId,
            CharacterRuntimePose localGripPose,
            string previewResourceKey,
            int traceId,
            string runtimeResourceKey,
            string resourcePackageId,
            string resourceSourceProviderId)
            : this(weaponId, equipSlot, attachSocketId, localGripPose, previewResourceKey, traceId, runtimeResourceKey, resourcePackageId, resourceSourceProviderId, 0, 0, 0f, string.Empty)
        {
        }

        public CharacterWeaponAttachmentRuntimeBinding(
            int weaponId,
            int equipSlot,
            int attachSocketId,
            CharacterRuntimePose localGripPose,
            string previewResourceKey,
            int traceId,
            string runtimeResourceKey,
            string resourcePackageId,
            string resourceSourceProviderId,
            int traceStartSocketId,
            int traceEndSocketId,
            float traceRadius,
            string traceSampleRule)
            : this(
                weaponId,
                equipSlot,
                attachSocketId,
                localGripPose,
                previewResourceKey,
                traceId,
                runtimeResourceKey,
                resourcePackageId,
                resourceSourceProviderId,
                traceStartSocketId,
                traceEndSocketId,
                traceRadius,
                traceSampleRule,
                string.Empty)
        {
        }

        /// <summary>AllocByDesign: constructs one imported attachment binding object with an exact schema 3.0 attachment identity.</summary>
        public CharacterWeaponAttachmentRuntimeBinding(
            int weaponId,
            int equipSlot,
            int attachSocketId,
            CharacterRuntimePose localGripPose,
            string previewResourceKey,
            int traceId,
            string runtimeResourceKey,
            string resourcePackageId,
            string resourceSourceProviderId,
            int traceStartSocketId,
            int traceEndSocketId,
            float traceRadius,
            string traceSampleRule,
            string attachmentId)
            : this(weaponId, equipSlot, attachSocketId, localGripPose, localGripPose,
                CharacterRuntimePose.Identity, previewResourceKey, traceId, runtimeResourceKey,
                resourcePackageId, resourceSourceProviderId, traceStartSocketId,
                traceEndSocketId, traceRadius, traceSampleRule, attachmentId)
        {
        }

        /// <summary>AllocByDesign: constructs one schema 5.0 binding with explicit character-slot and weapon-local transforms.</summary>
        public CharacterWeaponAttachmentRuntimeBinding(
            int weaponId,
            int equipSlot,
            int attachSocketId,
            CharacterRuntimePose localGripPose,
            CharacterRuntimePose slotPose,
            CharacterRuntimePose weaponModelPose,
            string previewResourceKey,
            int traceId,
            string runtimeResourceKey,
            string resourcePackageId,
            string resourceSourceProviderId,
            int traceStartSocketId,
            int traceEndSocketId,
            float traceRadius,
            string traceSampleRule,
            string attachmentId,
            string attachSocketLabel = "",
            string weaponLabel = "", string equipmentSlotLabel = "", string traceLabel = "")
        {
            AttachmentId = attachmentId ?? string.Empty;
            WeaponId = weaponId;
            WeaponLabel = weaponLabel ?? string.Empty;
            EquipmentSlotLabel = equipmentSlotLabel ?? string.Empty;
            TraceLabel = traceLabel ?? string.Empty;
            EquipSlot = equipSlot;
            AttachSocketId = attachSocketId;
            AttachSocketLabel = attachSocketLabel ?? string.Empty;
            LocalGripPose = localGripPose;
            SlotPose = IsUnsetPose(slotPose) ? localGripPose : slotPose;
            WeaponModelPose = IsUnsetPose(weaponModelPose) ? CharacterRuntimePose.Identity : weaponModelPose;
            PreviewResourceKey = previewResourceKey ?? string.Empty;
            TraceId = traceId;
            RuntimeResourceKey = runtimeResourceKey ?? string.Empty;
            ResourcePackageId = resourcePackageId ?? string.Empty;
            ResourceSourceProviderId = resourceSourceProviderId ?? string.Empty;
            TraceStartSocketId = traceStartSocketId;
            TraceEndSocketId = traceEndSocketId;
            TraceRadius = traceRadius < 0f ? 0f : traceRadius;
            TraceSampleRule = traceSampleRule ?? string.Empty;
        }

        public string AttachmentId { get; }
        public int WeaponId { get; }
        public string WeaponLabel { get; }
        public string EquipmentSlotLabel { get; }
        public string TraceLabel { get; }
        public int EquipSlot { get; }
        public int AttachSocketId { get; }
        public string AttachSocketLabel { get; }
        public CharacterRuntimePose LocalGripPose { get; }
        public CharacterRuntimePose SlotPose { get; }
        public CharacterRuntimePose WeaponModelPose { get; }
        public string PreviewResourceKey { get; }
        public int TraceId { get; }
        public string RuntimeResourceKey { get; }
        public string ResourcePackageId { get; }
        public string ResourceSourceProviderId { get; }
        public int TraceStartSocketId { get; }
        public int TraceEndSocketId { get; }
        public float TraceRadius { get; }
        public string TraceSampleRule { get; }

        private static bool IsUnsetPose(CharacterRuntimePose pose)
        {
            return pose.Scale.X == 0f && pose.Scale.Y == 0f && pose.Scale.Z == 0f;
        }
    }

    public sealed class CharacterWeaponTraceRuntimeBinding
    {
        public CharacterWeaponTraceRuntimeBinding(int traceId, int weaponId, int equipSlot, string startLocatorPath, string endLocatorPath, float radius, string sampleRule, int fixedSampleCount, string[] actionKeys)
            : this(
                traceId,
                weaponId,
                equipSlot,
                startLocatorPath,
                endLocatorPath,
                radius,
                sampleRule,
                fixedSampleCount,
                actionKeys,
                default,
                default,
                0f,
                0f)
        {
        }

        public CharacterWeaponTraceRuntimeBinding(
            int traceId,
            int weaponId,
            int equipSlot,
            string startLocatorPath,
            string endLocatorPath,
            float radius,
            string sampleRule,
            int fixedSampleCount,
            string[] actionKeys,
            CharacterRuntimePose startPose,
            CharacterRuntimePose endPose,
            float proximalExtension = 0f,
            float proximalRadius = 0f,
            int socketId = 0,
            string socketLabel = "",
            string weaponLabel = "", string equipmentSlotLabel = "", string traceLabel = "")
        {
            TraceId = traceId;
            WeaponId = weaponId;
            WeaponLabel = weaponLabel ?? string.Empty;
            EquipmentSlotLabel = equipmentSlotLabel ?? string.Empty;
            TraceLabel = traceLabel ?? string.Empty;
            EquipSlot = equipSlot;
            StartLocatorPath = startLocatorPath ?? string.Empty;
            EndLocatorPath = endLocatorPath ?? string.Empty;
            Radius = radius;
            SampleRule = sampleRule ?? string.Empty;
            FixedSampleCount = fixedSampleCount;
            ActionKeys = actionKeys ?? Array.Empty<string>();
            StartPose = startPose;
            EndPose = endPose;
            ProximalExtension = proximalExtension < 0f ? 0f : proximalExtension;
            ProximalRadius = proximalRadius < 0f ? 0f : proximalRadius;
            SocketId = socketId;
            SocketLabel = socketLabel ?? string.Empty;
        }

        public int TraceId { get; }
        public int WeaponId { get; }
        public string WeaponLabel { get; }
        public string EquipmentSlotLabel { get; }
        public string TraceLabel { get; }
        public int EquipSlot { get; }
        public string StartLocatorPath { get; }
        public string EndLocatorPath { get; }
        public float Radius { get; }
        public string SampleRule { get; }
        public int FixedSampleCount { get; }
        public string[] ActionKeys { get; }
        public CharacterRuntimePose StartPose { get; }
        public CharacterRuntimePose EndPose { get; }
        public float ProximalExtension { get; }
        public float ProximalRadius { get; }
        public int SocketId { get; }
        public string SocketLabel { get; }
    }

    public sealed class CharacterImportedResourceMapping
    {
        public CharacterImportedResourceMapping(string packageId, CharacterImportedResourceMappingEntry[] entries)
        {
            PackageId = packageId ?? string.Empty;
            Entries = entries ?? Array.Empty<CharacterImportedResourceMappingEntry>();
        }

        public static CharacterImportedResourceMapping Empty { get; } = new CharacterImportedResourceMapping(string.Empty, Array.Empty<CharacterImportedResourceMappingEntry>());

        public string PackageId { get; }
        public CharacterImportedResourceMappingEntry[] Entries { get; }

        public CharacterImportedResourceMappingEntry FindByPackageResourceKey(string packageResourceKey)
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                if (string.Equals(Entries[i].PackageResourceKey, packageResourceKey, StringComparison.Ordinal))
                    return Entries[i];
            }

            return null;
        }
    }

    public sealed class CharacterImportedResourceMappingEntry
    {
        public CharacterImportedResourceMappingEntry(
            string packageResourceKey,
            string projectResourceKey,
            string stableId,
            string typeId,
            string usage,
            string importTargetPath,
            string importHash,
            string dependencyHash)
        {
            PackageResourceKey = packageResourceKey ?? string.Empty;
            ProjectResourceKey = projectResourceKey ?? string.Empty;
            StableId = stableId ?? string.Empty;
            TypeId = typeId ?? string.Empty;
            Usage = usage ?? string.Empty;
            ImportTargetPath = importTargetPath ?? string.Empty;
            ImportHash = importHash ?? string.Empty;
            DependencyHash = dependencyHash ?? string.Empty;
        }

        public string PackageResourceKey { get; }
        public string ProjectResourceKey { get; }
        public string StableId { get; }
        public string TypeId { get; }
        public string Usage { get; }
        public string ImportTargetPath { get; }
        public string ImportHash { get; }
        public string DependencyHash { get; }
    }

    public sealed class CharacterRuntimeSpawnResult
    {
        public CharacterRuntimeSpawnResult(
            CharacterRuntimeSpawnStatus status,
            CharacterRuntimeBinding binding,
            CharacterPackageResolveResult packageResolveResult,
            CharacterRuntimeSpawnIssue[] issues)
        {
            Status = status;
            Binding = binding;
            PackageResolveResult = packageResolveResult;
            Issues = issues ?? Array.Empty<CharacterRuntimeSpawnIssue>();
        }

        public CharacterRuntimeSpawnStatus Status { get; }
        public bool IsSuccess => Status == CharacterRuntimeSpawnStatus.Success && Binding != null;
        public CharacterRuntimeBinding Binding { get; }
        public CharacterPackageResolveResult PackageResolveResult { get; }
        public CharacterRuntimeSpawnIssue[] Issues { get; }
    }

    public sealed class CharacterRuntimeBinding
    {
        public CharacterRuntimeBinding(
            CharacterControlEntityRef entityRef,
            CharacterSpawnPlan spawnPlan,
            CharacterResolvedProfile resolvedProfile,
            string sourcePackageHash,
            string generatedConfigHash,
            string geometryBindingHash,
            string resourceMappingHash,
            CharacterGameplayRegistrationPlan gameplayRegistrationPlan,
            CharacterCombatBodyBindingPlan combatBodyBindingPlan,
            CharacterWeaponAttachmentBindingPlan weaponAttachmentBindingPlan,
            CharacterResourcePreloadBindingPlan resourcePreloadPlan,
            string debugSummary,
            CharacterRuntimeAnimationProfileBinding animationProfileBinding = default)
        {
            EntityRef = entityRef;
            SpawnPlan = spawnPlan;
            ResolvedProfile = resolvedProfile;
            SourcePackageHash = sourcePackageHash ?? string.Empty;
            GeneratedConfigHash = generatedConfigHash ?? string.Empty;
            GeometryBindingHash = geometryBindingHash ?? string.Empty;
            ResourceMappingHash = resourceMappingHash ?? string.Empty;
            GameplayRegistrationPlan = gameplayRegistrationPlan;
            CombatBodyBindingPlan = combatBodyBindingPlan;
            WeaponAttachmentBindingPlan = weaponAttachmentBindingPlan;
            ResourcePreloadPlan = resourcePreloadPlan;
            DebugSummary = debugSummary ?? string.Empty;
            AnimationProfileBinding = animationProfileBinding;
        }

        public CharacterControlEntityRef EntityRef { get; }
        public CharacterSpawnPlan SpawnPlan { get; }
        public CharacterResolvedProfile ResolvedProfile { get; }
        public string SourcePackageHash { get; }
        public string GeneratedConfigHash { get; }
        public string GeometryBindingHash { get; }
        public string ResourceMappingHash { get; }
        public CharacterGameplayRegistrationPlan GameplayRegistrationPlan { get; }
        public CharacterCombatBodyBindingPlan CombatBodyBindingPlan { get; }
        public CharacterWeaponAttachmentBindingPlan WeaponAttachmentBindingPlan { get; }
        public CharacterResourcePreloadBindingPlan ResourcePreloadPlan { get; }
        public string DebugSummary { get; }
        public CharacterRuntimeAnimationProfileBinding AnimationProfileBinding { get; }
    }

    public sealed class CharacterGameplayRegistrationPlan
    {
        public CharacterGameplayRegistrationPlan(GameplayEntityId entityId, CharacterConfigId characterId, string teamId, bool willCreateEntity, string[] diagnostics)
        {
            EntityId = entityId;
            CharacterId = characterId;
            TeamId = teamId ?? string.Empty;
            WillCreateEntity = willCreateEntity;
            Diagnostics = diagnostics ?? Array.Empty<string>();
        }

        public GameplayEntityId EntityId { get; }
        public CharacterConfigId CharacterId { get; }
        public string TeamId { get; }
        public bool WillCreateEntity { get; }
        public string[] Diagnostics { get; }
    }

    public sealed class CharacterCombatBodyBindingPlan
    {
        public CharacterCombatBodyBindingPlan(CombatEntityId entityId, CombatBodyId bodyId, CharacterCombatColliderBinding[] colliders)
        {
            EntityId = entityId;
            BodyId = bodyId;
            Colliders = colliders ?? Array.Empty<CharacterCombatColliderBinding>();
        }

        public CombatEntityId EntityId { get; }
        public CombatBodyId BodyId { get; }
        public CharacterCombatColliderBinding[] Colliders { get; }
    }

    public sealed class CharacterCombatColliderBinding
    {
        public CharacterCombatColliderBinding(CombatColliderId colliderId, int authoringColliderId, int partId, int hitZoneId, string shape, int priority, bool isWeakPoint,
            string partLabel = "",
            string hitZoneLabel = "")
        {
            ColliderId = colliderId;
            AuthoringColliderId = authoringColliderId;
            PartId = partId;
            PartLabel = partLabel ?? string.Empty;
            HitZoneId = hitZoneId;
            HitZoneLabel = hitZoneLabel ?? string.Empty;
            Shape = shape ?? string.Empty;
            Priority = priority;
            IsWeakPoint = isWeakPoint;
        }

        public CombatColliderId ColliderId { get; }
        public int AuthoringColliderId { get; }
        public int PartId { get; }
        public string PartLabel { get; }
        public int HitZoneId { get; }
        public string HitZoneLabel { get; }
        public string Shape { get; }
        public int Priority { get; }
        public bool IsWeakPoint { get; }
    }

    public sealed class CharacterWeaponAttachmentBindingPlan
    {
        public CharacterWeaponAttachmentBindingPlan(CharacterWeaponAttachmentRuntimeBinding[] attachments, CharacterWeaponTraceRuntimeBinding[] traces)
        {
            Attachments = attachments ?? Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();
            Traces = traces ?? Array.Empty<CharacterWeaponTraceRuntimeBinding>();
        }

        public CharacterWeaponAttachmentRuntimeBinding[] Attachments { get; }
        public CharacterWeaponTraceRuntimeBinding[] Traces { get; }
    }

    public sealed class CharacterResourcePreloadBindingPlan
    {
        public CharacterResourcePreloadBindingPlan(ResourceKey[] requiredResources, CharacterResolvedResourceBinding[] resolvedResources, string[] missingResourceKeys)
        {
            RequiredResources = requiredResources ?? Array.Empty<ResourceKey>();
            ResolvedResources = resolvedResources ?? Array.Empty<CharacterResolvedResourceBinding>();
            MissingResourceKeys = missingResourceKeys ?? Array.Empty<string>();
        }

        public ResourceKey[] RequiredResources { get; }
        public CharacterResolvedResourceBinding[] ResolvedResources { get; }
        public string[] MissingResourceKeys { get; }
    }

    public sealed class CharacterResolvedResourceBinding
    {
        public CharacterResolvedResourceBinding(ResourceKey requestedKey, ResourceKey projectKey, string providerId, string address, string assetPath)
        {
            RequestedKey = requestedKey;
            ProjectKey = projectKey;
            ProviderId = providerId ?? string.Empty;
            Address = address ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
        }

        public ResourceKey RequestedKey { get; }
        public ResourceKey ProjectKey { get; }
        public string ProviderId { get; }
        public string Address { get; }
        public string AssetPath { get; }
    }

    internal static class CharacterRuntimeSpawnIssueListExtensions
    {
        public static void AddIssue(
            this List<CharacterRuntimeSpawnIssue> issues,
            CharacterRuntimeSpawnIssueSeverity severity,
            string code,
            string sourcePath,
            string field,
            string message)
        {
            issues.Add(new CharacterRuntimeSpawnIssue(severity, code, sourcePath, field, message));
        }
    }
}
