using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Resources;

namespace MxFramework.CharacterRuntimeSpawn
{
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
        public const string DeferredBinding = "CHAR_RUNTIME_DEFERRED_BINDING";
        public const string MissingCompiledAnimationArtifact = "CHAR_RUNTIME_MISSING_COMPILED_ANIMATION_ARTIFACT";
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
            CharacterCompiledAnimationArtifacts compiledAnimationArtifacts = null)
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
        public CharacterAnimationGroup(string groupId, string displayName, string usage, CharacterAnimationClipReference[] clips)
        {
            GroupId = groupId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Usage = usage ?? string.Empty;
            Clips = clips ?? Array.Empty<CharacterAnimationClipReference>();
        }

        public string GroupId { get; }
        public string DisplayName { get; }
        public string Usage { get; }
        public CharacterAnimationClipReference[] Clips { get; }
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
        public CharacterAnimationActionBinding(string bindingId, string actionId, string groupId, string clipId, string blendId, string timelineId, bool required)
        {
            BindingId = bindingId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            GroupId = groupId ?? string.Empty;
            ClipId = clipId ?? string.Empty;
            BlendId = blendId ?? string.Empty;
            TimelineId = timelineId ?? string.Empty;
            Required = required;
        }

        public string BindingId { get; }
        public string ActionId { get; }
        public string GroupId { get; }
        public string ClipId { get; }
        public string BlendId { get; }
        public string TimelineId { get; }
        public bool Required { get; }
    }

    public sealed class CharacterAnimationProfile
    {
        public CharacterAnimationProfile(string profileId, string displayName, string defaultSetId, string defaultGroupId)
        {
            ProfileId = profileId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DefaultSetId = defaultSetId ?? string.Empty;
            DefaultGroupId = defaultGroupId ?? string.Empty;
        }

        public string ProfileId { get; }
        public string DisplayName { get; }
        public string DefaultSetId { get; }
        public string DefaultGroupId { get; }
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
            EquipmentSchemaConfig[] equipmentSchemas,
            EquipmentLoadoutConfig[] loadouts,
            EquipmentStateConfig[] equipmentStates,
            WeaponConfig[] weapons,
            AbilityLoadoutConfig[] abilityLoadouts,
            CombatActionSetConfig[] combatActionSets,
            CharacterPresentationProfileConfig[] presentationProfiles,
            SpawnProfileConfig[] spawnProfiles)
        {
            Characters = characters ?? Array.Empty<CharacterConfig>();
            AttributeProfiles = attributeProfiles ?? Array.Empty<CharacterAttributeProfileConfig>();
            BodyProfiles = bodyProfiles ?? Array.Empty<CharacterBodyProfileConfig>();
            BodyParts = bodyParts ?? Array.Empty<CharacterBodyPartConfig>();
            EquipmentSchemas = equipmentSchemas ?? Array.Empty<EquipmentSchemaConfig>();
            Loadouts = loadouts ?? Array.Empty<EquipmentLoadoutConfig>();
            EquipmentStates = equipmentStates ?? Array.Empty<EquipmentStateConfig>();
            Weapons = weapons ?? Array.Empty<WeaponConfig>();
            AbilityLoadouts = abilityLoadouts ?? Array.Empty<AbilityLoadoutConfig>();
            CombatActionSets = combatActionSets ?? Array.Empty<CombatActionSetConfig>();
            PresentationProfiles = presentationProfiles ?? Array.Empty<CharacterPresentationProfileConfig>();
            SpawnProfiles = spawnProfiles ?? Array.Empty<SpawnProfileConfig>();
        }

        public static CharacterImportedConfigSet Empty { get; } = new CharacterImportedConfigSet(
            Array.Empty<CharacterConfig>(),
            Array.Empty<CharacterAttributeProfileConfig>(),
            Array.Empty<CharacterBodyProfileConfig>(),
            Array.Empty<CharacterBodyPartConfig>(),
            Array.Empty<EquipmentSchemaConfig>(),
            Array.Empty<EquipmentLoadoutConfig>(),
            Array.Empty<EquipmentStateConfig>(),
            Array.Empty<WeaponConfig>(),
            Array.Empty<AbilityLoadoutConfig>(),
            Array.Empty<CombatActionSetConfig>(),
            Array.Empty<CharacterPresentationProfileConfig>(),
            Array.Empty<SpawnProfileConfig>());

        public CharacterConfig[] Characters { get; }
        public CharacterAttributeProfileConfig[] AttributeProfiles { get; }
        public CharacterBodyProfileConfig[] BodyProfiles { get; }
        public CharacterBodyPartConfig[] BodyParts { get; }
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

    public sealed class CharacterImportedGeometryBinding
    {
        public CharacterImportedGeometryBinding(
            string packageId,
            CharacterBodyGeometryRuntimeProfile bodyProfile,
            CharacterBodyColliderRuntimeBinding[] bodyColliders,
            CharacterHitZoneRuntimeBinding[] hitZoneBindings,
            CharacterSocketRuntimeBinding[] sockets,
            CharacterWeaponAttachmentRuntimeBinding[] weaponAttachments,
            CharacterWeaponTraceRuntimeBinding[] weaponTraces)
        {
            PackageId = packageId ?? string.Empty;
            BodyProfile = bodyProfile;
            BodyColliders = bodyColliders ?? Array.Empty<CharacterBodyColliderRuntimeBinding>();
            HitZoneBindings = hitZoneBindings ?? Array.Empty<CharacterHitZoneRuntimeBinding>();
            Sockets = sockets ?? Array.Empty<CharacterSocketRuntimeBinding>();
            WeaponAttachments = weaponAttachments ?? Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();
            WeaponTraces = weaponTraces ?? Array.Empty<CharacterWeaponTraceRuntimeBinding>();
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
        public CharacterBodyGeometryRuntimeProfile BodyProfile { get; }
        public CharacterBodyColliderRuntimeBinding[] BodyColliders { get; }
        public CharacterHitZoneRuntimeBinding[] HitZoneBindings { get; }
        public CharacterSocketRuntimeBinding[] Sockets { get; }
        public CharacterWeaponAttachmentRuntimeBinding[] WeaponAttachments { get; }
        public CharacterWeaponTraceRuntimeBinding[] WeaponTraces { get; }
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
            string colliderId,
            string partId,
            string hitZoneId,
            string shape,
            CharacterRuntimePose localPose,
            CharacterRuntimeVector3 size,
            float radius,
            float height,
            int priority,
            bool isWeakPoint,
            string physicsLayer)
        {
            ColliderId = colliderId ?? string.Empty;
            PartId = partId ?? string.Empty;
            HitZoneId = hitZoneId ?? string.Empty;
            Shape = shape ?? string.Empty;
            LocalPose = localPose;
            Size = size;
            Radius = radius;
            Height = height;
            Priority = priority;
            IsWeakPoint = isWeakPoint;
            PhysicsLayer = physicsLayer ?? string.Empty;
        }

        public string ColliderId { get; }
        public string PartId { get; }
        public string HitZoneId { get; }
        public string Shape { get; }
        public CharacterRuntimePose LocalPose { get; }
        public CharacterRuntimeVector3 Size { get; }
        public float Radius { get; }
        public float Height { get; }
        public int Priority { get; }
        public bool IsWeakPoint { get; }
        public string PhysicsLayer { get; }
    }

    public sealed class CharacterHitZoneRuntimeBinding
    {
        public CharacterHitZoneRuntimeBinding(string hitZoneId, string partId, int priority, bool isWeakPoint)
        {
            HitZoneId = hitZoneId ?? string.Empty;
            PartId = partId ?? string.Empty;
            Priority = priority;
            IsWeakPoint = isWeakPoint;
        }

        public string HitZoneId { get; }
        public string PartId { get; }
        public int Priority { get; }
        public bool IsWeakPoint { get; }
    }

    public sealed class CharacterSocketRuntimeBinding
    {
        public CharacterSocketRuntimeBinding(string socketId, string parentPartId, string bonePath, string locatorPath, CharacterRuntimePose localPose, string usage)
        {
            SocketId = socketId ?? string.Empty;
            ParentPartId = parentPartId ?? string.Empty;
            BonePath = bonePath ?? string.Empty;
            LocatorPath = locatorPath ?? string.Empty;
            LocalPose = localPose;
            Usage = usage ?? string.Empty;
        }

        public string SocketId { get; }
        public string ParentPartId { get; }
        public string BonePath { get; }
        public string LocatorPath { get; }
        public CharacterRuntimePose LocalPose { get; }
        public string Usage { get; }
    }

    public sealed class CharacterWeaponAttachmentRuntimeBinding
    {
        public CharacterWeaponAttachmentRuntimeBinding(string weaponId, string equipSlot, string attachSocketId, CharacterRuntimePose localGripPose, string previewResourceKey, string traceId)
        {
            WeaponId = weaponId ?? string.Empty;
            EquipSlot = equipSlot ?? string.Empty;
            AttachSocketId = attachSocketId ?? string.Empty;
            LocalGripPose = localGripPose;
            PreviewResourceKey = previewResourceKey ?? string.Empty;
            TraceId = traceId ?? string.Empty;
        }

        public string WeaponId { get; }
        public string EquipSlot { get; }
        public string AttachSocketId { get; }
        public CharacterRuntimePose LocalGripPose { get; }
        public string PreviewResourceKey { get; }
        public string TraceId { get; }
    }

    public sealed class CharacterWeaponTraceRuntimeBinding
    {
        public CharacterWeaponTraceRuntimeBinding(string traceId, string weaponId, string equipSlot, string startLocatorPath, string endLocatorPath, float radius, string sampleRule, int fixedSampleCount, string[] actionKeys)
        {
            TraceId = traceId ?? string.Empty;
            WeaponId = weaponId ?? string.Empty;
            EquipSlot = equipSlot ?? string.Empty;
            StartLocatorPath = startLocatorPath ?? string.Empty;
            EndLocatorPath = endLocatorPath ?? string.Empty;
            Radius = radius;
            SampleRule = sampleRule ?? string.Empty;
            FixedSampleCount = fixedSampleCount;
            ActionKeys = actionKeys ?? Array.Empty<string>();
        }

        public string TraceId { get; }
        public string WeaponId { get; }
        public string EquipSlot { get; }
        public string StartLocatorPath { get; }
        public string EndLocatorPath { get; }
        public float Radius { get; }
        public string SampleRule { get; }
        public int FixedSampleCount { get; }
        public string[] ActionKeys { get; }
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
            string debugSummary)
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
        public CharacterCombatColliderBinding(CombatColliderId colliderId, string authoringColliderId, string partId, string hitZoneId, string shape, int priority, bool isWeakPoint)
        {
            ColliderId = colliderId;
            AuthoringColliderId = authoringColliderId ?? string.Empty;
            PartId = partId ?? string.Empty;
            HitZoneId = hitZoneId ?? string.Empty;
            Shape = shape ?? string.Empty;
            Priority = priority;
            IsWeakPoint = isWeakPoint;
        }

        public CombatColliderId ColliderId { get; }
        public string AuthoringColliderId { get; }
        public string PartId { get; }
        public string HitZoneId { get; }
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
