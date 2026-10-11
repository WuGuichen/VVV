using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MxFramework.Authoring
{
    public enum CharacterAuthoringValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public enum CharacterAuthoringValidationGate
    {
        Unknown = 0,
        ExportBlocked = 10,
        ImportBlocked = 20,
        SpawnBlocked = 30,
        WarningOnly = 40,
        Reserved1000 = 1000,
        Reserved1001 = 1001,
        Reserved1002 = 1002
    }

    public static partial class CharacterAuthoringValidationCodes
    {
        public const string MissingManifest = "CHARPKG_MISSING_MANIFEST";
        public const string MissingPackageId = "CHARPKG_MISSING_PACKAGE_ID";
        public const string MissingStableId = "CHARPKG_MISSING_STABLE_ID";
        public const string InvalidPackageKind = "CHARPKG_INVALID_PACKAGE_KIND";
        public const string InvalidUnitScale = "CHARPKG_INVALID_UNIT_SCALE";
        public const string RotationMustBeQuaternion = "CHARPKG_ROTATION_MUST_BE_QUATERNION";
        public const string MissingResourceCatalog = "CHARPKG_MISSING_RESOURCE_CATALOG";
        public const string MissingResourceKey = "CHARPKG_MISSING_RESOURCE_KEY";
        public const string InvalidResourceKey = "CHARPKG_INVALID_RESOURCE_KEY";
        public const string DuplicateResourceKey = "CHARPKG_DUPLICATE_RESOURCE_KEY";
        public const string MissingResourceLocalId = "CHARPKG_MISSING_RESOURCE_LOCAL_ID";
        public const string MissingResourceStableId = "CHARPKG_MISSING_RESOURCE_STABLE_ID";
        public const string DuplicateResourceStableId = "CHARPKG_DUPLICATE_RESOURCE_STABLE_ID";
        public const string MissingResourcePath = "CHARPKG_MISSING_RESOURCE_PATH";
        public const string InvalidResourcePath = "CHARPKG_INVALID_RESOURCE_PATH";
        public const string MissingResourceFile = "CHARPKG_MISSING_RESOURCE_FILE";
        public const string MissingResourceHash = "CHARPKG_MISSING_RESOURCE_HASH";
        public const string ResourceHashMismatch = "CHARPKG_RESOURCE_HASH_MISMATCH";
        public const string UnsupportedResourceFormat = "CHARPKG_UNSUPPORTED_RESOURCE_FORMAT";
        public const string FutureResourceFormat = "CHARPKG_FUTURE_RESOURCE_FORMAT";
        public const string InvalidImportTargetPath = "CHARPKG_INVALID_IMPORT_TARGET_PATH";
        public const string MissingResourceDependency = "CHARPKG_MISSING_RESOURCE_DEPENDENCY";
        public const string DuplicateResourceDependency = "CHARPKG_DUPLICATE_RESOURCE_DEPENDENCY";
        public const string SelfResourceDependency = "CHARPKG_SELF_RESOURCE_DEPENDENCY";
        public const string MissingPreviewResource = "CHARPKG_MISSING_PREVIEW_RESOURCE";
        public const string MissingGeometry = "CHARPKG_MISSING_GEOMETRY";
        public const string MissingBodyProfile = "CHARPKG_MISSING_BODY_PROFILE";
        public const string MissingBodyPart = "CHARPKG_MISSING_BODY_PART";
        public const string DuplicateBodyPart = "CHARPKG_DUPLICATE_BODY_PART";
        public const string MissingBodyPartLocator = "CHARPKG_MISSING_BODY_PART_LOCATOR";
        public const string MissingColliderPart = "CHARPKG_MISSING_COLLIDER_PART";
        public const string DuplicateCollider = "CHARPKG_DUPLICATE_COLLIDER";
        public const string MissingColliderHitZone = "CHARPKG_MISSING_COLLIDER_HIT_ZONE";
        public const string UnsupportedColliderShape = "CHARPKG_UNSUPPORTED_COLLIDER_SHAPE";
        public const string InvalidColliderDimensions = "CHARPKG_INVALID_COLLIDER_DIMENSIONS";
        public const string DamageZoneInvalidFormat = "CHARDZ001_INVALID_FORMAT";
        public const string DamageZoneUnsupportedSchema = "CHARDZ002_UNSUPPORTED_SCHEMA";
        public const string DamageZoneDuplicateZoneId = "CHARDZ003_DUPLICATE_ZONE_ID";
        public const string DamageZoneEmptyZoneId = "CHARDZ004_EMPTY_ZONE_ID";
        public const string DamageZoneEmptyMembers = "CHARDZ005_EMPTY_MEMBERS";
        public const string DamageZoneDuplicateHitZoneMembership = "CHARDZ006_DUPLICATE_HIT_ZONE_MEMBERSHIP";
        public const string DamageZoneUnknownHitZone = "CHARDZ007_UNKNOWN_HIT_ZONE";
        public const string DamageZoneDisabledBodyPart = "CHARDZ008_DISABLED_BODY_PART";
        public const string DamageZoneInvalidPriority = "CHARDZ009_INVALID_PRIORITY";
        public const string DamageZoneInvalidAttributes = "CHARDZ010_INVALID_ATTRIBUTES";
        public const string DamageZoneReadOnlyDocument = "CHARDZ011_READ_ONLY_DOCUMENT";
        public const string EngagementAnchorInvalidFormat = "CHARANCH001_INVALID_FORMAT";
        public const string EngagementAnchorUnsupportedSchema = "CHARANCH002_UNSUPPORTED_SCHEMA";
        public const string EngagementAnchorReadOnlyDocument = "CHARANCH003_READ_ONLY_DOCUMENT";
        public const string EngagementAnchorInvalidRevision = "CHARANCH004_INVALID_REVISION";
        public const string EngagementAnchorEmptyId = "CHARANCH005_EMPTY_ID";
        public const string EngagementAnchorDuplicateId = "CHARANCH006_DUPLICATE_ID";
        public const string EngagementAnchorUnknownZone = "CHARANCH007_UNKNOWN_ZONE";
        public const string EngagementAnchorZoneNotApproachable = "CHARANCH008_ZONE_NOT_APPROACHABLE";
        public const string EngagementAnchorInvalidReferenceFrame = "CHARANCH009_INVALID_REFERENCE_FRAME";
        public const string EngagementAnchorInvalidPosition = "CHARANCH010_INVALID_POSITION";
        public const string EngagementAnchorInvalidYaw = "CHARANCH011_INVALID_YAW";
        public const string EngagementAnchorInvalidCapacity = "CHARANCH012_INVALID_CAPACITY";
        public const string EngagementAnchorInvalidPriority = "CHARANCH013_INVALID_PRIORITY";
        public const string EngagementAnchorInvalidPhaseId = "CHARANCH014_INVALID_PHASE_ID";
        public const string MissingBodyPartContactPointStableId = "CHARPKG_MISSING_BODY_PART_CONTACT_POINT_STABLE_ID";
        public const string DuplicateBodyPartContactPointStableId = "CHARPKG_DUPLICATE_BODY_PART_CONTACT_POINT_STABLE_ID";
        public const string InvalidBodyPartContactPointId = "CHARPKG_INVALID_BODY_PART_CONTACT_POINT_ID";
        public const string DuplicateBodyPartContactPointId = "CHARPKG_DUPLICATE_BODY_PART_CONTACT_POINT_ID";
        public const string MissingBodyPartContactPointPart = "CHARPKG_MISSING_BODY_PART_CONTACT_POINT_PART";
        public const string InvalidBodyPartContactPointRadius = "CHARPKG_INVALID_BODY_PART_CONTACT_POINT_RADIUS";
        public const string InvalidBodyPartContactActivationMode = "CHARPKG_INVALID_BODY_PART_CONTACT_ACTIVATION_MODE";
        public const string InvalidBodyPartContactMotionMode = "CHARPKG_INVALID_BODY_PART_CONTACT_MOTION_MODE";
        public const string MissingBodyPartContactMotionSocket = "CHARPKG_MISSING_BODY_PART_CONTACT_MOTION_SOCKET";
        public const string MissingSocketId = "CHARPKG_MISSING_SOCKET_ID";
        public const string DuplicateSocket = "CHARPKG_DUPLICATE_SOCKET";
        public const string InvalidAbstractBoneId = "CHARPKG_ABSTRACT_BONE_ID_INVALID";
        public const string DuplicateAbstractBoneId = "CHARPKG_ABSTRACT_BONE_DUPLICATE";
        public const string MissingRequiredAbstractBone = "CHARPKG_REQUIRED_ABSTRACT_BONE_MISSING";
        public const string CameraProfileInvalid = "CHARACTER_CAMERA_PROFILE_INVALID";
        public const string CameraFocusMissing = "CHARACTER_CAMERA_FOCUS_MISSING";
        public const string CameraBoundsInvalid = "CHARACTER_CAMERA_BOUNDS_INVALID";
        public const string CameraAnchorUnresolved = "CHARACTER_CAMERA_ANCHOR_UNRESOLVED";
        public const string MissingSocketPart = "CHARPKG_MISSING_SOCKET_PART";
        public const string MissingSocketLocator = "CHARPKG_MISSING_SOCKET_LOCATOR";
        public const string MissingMirrorSocket = "CHARPKG_MISSING_MIRROR_SOCKET";
        public const string SelfMirrorSocket = "CHARPKG_SELF_MIRROR_SOCKET";
        public const string MissingAttachmentSocket = "CHARPKG_MISSING_ATTACHMENT_SOCKET";
        public const string MissingAttachmentTraceSocket = "CHARPKG_MISSING_ATTACHMENT_TRACE_SOCKET";
        public const string MissingAttachmentTrace = "CHARPKG_MISSING_ATTACHMENT_TRACE";
        public const string MissingWeaponTraceBinding = "CHARPKG_MISSING_WEAPON_TRACE_BINDING";
        public const string AttachmentTraceMismatch = "CHARPKG_ATTACHMENT_TRACE_MISMATCH";
        public const string AmbiguousWeaponTraceBinding = "CHARPKG_AMBIGUOUS_WEAPON_TRACE_BINDING";
        public const string LegacyTraceOverride = "CHARPKG_LEGACY_TRACE_OVERRIDE";
        public const string LegacyTraceOverrideConflict = "CHARPKG_LEGACY_TRACE_OVERRIDE_CONFLICT";
        public const string InvalidAttachmentTraceRadius = "CHARPKG_INVALID_ATTACHMENT_TRACE_RADIUS";
        public const string InvalidWeaponTraceProximalRange = "CHARPKG_INVALID_WEAPON_TRACE_PROXIMAL_RANGE";
        public const string WeaponConfigurationUpgradeRequired = "CHARPKG_WEAPON_CONFIGURATION_UPGRADE_REQUIRED";
        public const string MissingWeaponConfiguration = "CHARPKG_MISSING_WEAPON_CONFIGURATION";
        public const string MissingDefaultWeaponConfiguration = "CHARPKG_MISSING_DEFAULT_WEAPON_CONFIGURATION";
        public const string DuplicateWeaponDefinitionId = "CHARPKG_DUPLICATE_WEAPON_DEFINITION_ID";
        public const string DuplicateWeaponConfigurationId = "CHARPKG_DUPLICATE_WEAPON_CONFIGURATION_ID";
        public const string InvalidWeaponConfigurationSlot = "CHARPKG_INVALID_WEAPON_CONFIGURATION_SLOT";
        public const string MissingWeaponDefinitionReference = "CHARPKG_MISSING_WEAPON_DEFINITION_REFERENCE";
        public const string MissingWeaponAttachmentReference = "CHARPKG_MISSING_WEAPON_ATTACHMENT_REFERENCE";
        public const string DuplicateWeaponAttachmentId = "CHARPKG_DUPLICATE_WEAPON_ATTACHMENT_ID";
        public const string WeaponSlotNotAllowed = "CHARPKG_WEAPON_SLOT_NOT_ALLOWED";
        public const string TwoHandWeaponSlotConflict = "CHARPKG_TWO_HAND_WEAPON_SLOT_CONFLICT";
        public const string MissingAnimationProfileReference = "CHARPKG_MISSING_ANIMATION_PROFILE_REFERENCE";
        public const string MissingActionSetReference = "CHARPKG_MISSING_ACTION_SET_REFERENCE";
        public const string MissingAbilityLoadoutReference = "CHARPKG_MISSING_ABILITY_LOADOUT_REFERENCE";
        public const string GlobalWeaponLibraryMissing = "GLOBAL_WEAPON_LIBRARY_MISSING";
        public const string GlobalWeaponLibrarySchemaUnsupported = "GLOBAL_WEAPON_LIBRARY_SCHEMA_UNSUPPORTED";
        public const string GlobalAnimationConfigurationIdDuplicate = "GLOBAL_ANIMATION_CONFIGURATION_ID_DUPLICATE";
        public const string GlobalAbilityConfigurationIdDuplicate = "GLOBAL_ABILITY_CONFIGURATION_ID_DUPLICATE";
        public const string GlobalReactionConfigurationIdDuplicate = "GLOBAL_REACTION_CONFIGURATION_ID_DUPLICATE";
        public const string GlobalReactionDefaultSlotMissing = "GLOBAL_REACTION_DEFAULT_SLOT_MISSING";
        public const string GlobalReactionDefaultSlotNotFound = "GLOBAL_REACTION_DEFAULT_SLOT_NOT_FOUND";
        public const string GlobalAnimationConfigurationNotFound = "GLOBAL_ANIMATION_CONFIGURATION_NOT_FOUND";
        public const string GlobalAnimationConfigurationIncompatible = "GLOBAL_ANIMATION_CONFIGURATION_INCOMPATIBLE";
        public const string GlobalAbilityConfigurationNotFound = "GLOBAL_ABILITY_CONFIGURATION_NOT_FOUND";
        public const string GlobalReactionConfigurationNotFound = "GLOBAL_REACTION_CONFIGURATION_NOT_FOUND";
        public const string GlobalAnimationLocomotionMissing = "GLOBAL_ANIMATION_LOCOMOTION_MISSING";
        public const string GlobalAbilityConfigurationEmpty = "GLOBAL_ABILITY_CONFIGURATION_EMPTY";
        public const string GlobalAbilityEntryInvalid = "GLOBAL_ABILITY_ENTRY_INVALID";
        public const string GlobalAbilityInputBindingInvalid = "GLOBAL_ABILITY_INPUT_BINDING_INVALID";
        public const string GlobalAbilityActionRoleInvalid = "GLOBAL_ABILITY_ACTION_ROLE_INVALID";
        public const string CombinedWeaponActionRoleMissing = "COMBINED_WEAPON_ACTION_ROLE_MISSING";
        public const string CombinedWeaponActionRoleDuplicate = "COMBINED_WEAPON_ACTION_ROLE_DUPLICATE";
        public const string CombinedWeaponActionIdShared = "COMBINED_WEAPON_ACTION_ID_SHARED";
        public const string CharacterActionAnimationIncompatible = "CHARACTER_ACTION_ANIMATION_INCOMPATIBLE";
        public const string CombinedWeaponCommandIntentConflict = "COMBINED_WEAPON_COMMAND_INTENT_CONFLICT";
        public const string SingleWeaponConfigurationIdDuplicate = "SINGLE_WEAPON_CONFIGURATION_ID_DUPLICATE";
        public const string SingleWeaponConfigurationNotFound = "SINGLE_WEAPON_CONFIGURATION_NOT_FOUND";
        public const string SingleWeaponConfigurationInUse = "SINGLE_WEAPON_CONFIGURATION_IN_USE";
        public const string GlobalWeaponConfigurationInUse = "GLOBAL_WEAPON_CONFIGURATION_IN_USE";
        public const string UnarmedConfigurationInvalid = "UNARMED_CONFIGURATION_INVALID";
        public const string CombinedWeaponConfigurationMissing = "COMBINED_WEAPON_CONFIGURATION_MISSING";
        public const string CombinedWeaponConfigurationIdDuplicate = "COMBINED_WEAPON_CONFIGURATION_ID_DUPLICATE";
        public const string GlobalCombinedWeaponConfigurationIdDuplicate = "GLOBAL_COMBINED_WEAPON_CONFIGURATION_ID_DUPLICATE";
        public const string GlobalCombinedWeaponConfigurationNotFound = "GLOBAL_COMBINED_WEAPON_CONFIGURATION_NOT_FOUND";
        public const string CombinedWeaponConfigurationDefaultInvalid = "COMBINED_WEAPON_CONFIGURATION_DEFAULT_INVALID";
        public const string CombinedWeaponSlotMissing = "COMBINED_WEAPON_SLOT_MISSING";
        public const string CombinedWeaponSlotDuplicate = "COMBINED_WEAPON_SLOT_DUPLICATE";
        public const string CombinedWeaponAttachmentRequired = "COMBINED_WEAPON_ATTACHMENT_REQUIRED";
        public const string CharacterWeaponSlotDuplicate = "CHARACTER_WEAPON_SLOT_DUPLICATE";
        public const string CharacterWeaponSlotSocketMissing = "CHARACTER_WEAPON_SLOT_SOCKET_MISSING";
        public const string SingleWeaponModelPoseMissing = "SINGLE_WEAPON_MODEL_POSE_MISSING";
        public const string SingleWeaponTraceIdDuplicate = "SINGLE_WEAPON_TRACE_ID_DUPLICATE";
        public const string SingleWeaponTraceSpaceInvalid = "SINGLE_WEAPON_TRACE_SPACE_INVALID";
        public const string CombinedWeaponAttachmentForbidden = "COMBINED_WEAPON_ATTACHMENT_FORBIDDEN";
        public const string CombinedWeaponAttachmentMismatch = "COMBINED_WEAPON_ATTACHMENT_MISMATCH";
        public const string CombinedWeaponAnimationProfileMissing = "COMBINED_WEAPON_ANIMATION_PROFILE_MISSING";
        public const string CombinedWeaponActionSetMissing = "COMBINED_WEAPON_ACTION_SET_MISSING";
        public const string CombinedWeaponAbilityLoadoutMissing = "COMBINED_WEAPON_ABILITY_LOADOUT_MISSING";
        public const string WeaponSchemaMigrationGlobalConflict = "WEAPON_SCHEMA_MIGRATION_GLOBAL_CONFLICT";
        public const string WeaponSlotMigrationConflict = "WEAPON_SLOT_MIGRATION_CONFLICT";
        public const string SingleWeaponTraceMigrationConflict = "SINGLE_WEAPON_TRACE_MIGRATION_CONFLICT";
        public const string WeaponSchemaMigrationAbilityRebuildRequired = "WEAPON_SCHEMA_MIGRATION_ABILITY_REBUILD_REQUIRED";
        public const string GeneratedEquipmentSlotMissing = "CHARPKG_GENERATED_EQUIPMENT_SLOT_MISSING";
    }

    public sealed class CharacterAuthoringValidationIssue
    {
        public CharacterAuthoringValidationSeverity Severity { get; set; }
        public CharacterAuthoringValidationGate Gate { get; set; } = CharacterAuthoringValidationGate.WarningOnly;
        public string Code { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string SourceObjectPath { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SuggestedFix { get; set; } = string.Empty;
    }

    public sealed class CharacterAuthoringValidationReport
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public List<CharacterAuthoringValidationIssue> Issues { get; set; } = new List<CharacterAuthoringValidationIssue>();

        public bool HasBlockingIssues
        {
            get
            {
                for (int i = 0; i < Issues.Count; i++)
                {
                    CharacterAuthoringValidationIssue issue = Issues[i];
                    if (issue.Severity == CharacterAuthoringValidationSeverity.Error)
                        return true;
                    if (issue.Gate == CharacterAuthoringValidationGate.ExportBlocked ||
                        issue.Gate == CharacterAuthoringValidationGate.ImportBlocked ||
                        issue.Gate == CharacterAuthoringValidationGate.SpawnBlocked)
                        return true;
                }

                return false;
            }
        }

        public string ToText()
        {
            var builder = new StringBuilder();
            builder.Append("MxFramework Character Authoring Validation Report\n");
            builder.Append("package=").Append(PackageId).Append('\n');
            builder.Append("status=").Append(HasBlockingIssues ? "blocked" : "ready").Append('\n');
            for (int i = 0; i < Issues.Count; i++)
            {
                CharacterAuthoringValidationIssue issue = Issues[i];
                builder.Append(issue.Severity)
                    .Append(" gate=").Append(issue.Gate)
                    .Append(" code=").Append(issue.Code)
                    .Append(" sourcePath=").Append(issue.SourcePath)
                    .Append(" object=").Append(issue.SourceObjectPath)
                    .Append(" field=").Append(issue.Field)
                    .Append(" message=").Append(issue.Message)
                    .Append('\n');
            }

            return builder.ToString();
        }
    }

    public static partial class CharacterResourcePackageValidator
    {
        public static CharacterAuthoringValidationReport Validate(CharacterResourcePackage package)
        {
            return Validate(package, new CharacterResourcePackageValidationOptions());
        }

        public static CharacterAuthoringValidationReport Validate(CharacterResourcePackage package, CharacterResourcePackageValidationOptions options)
        {
            var report = new CharacterAuthoringValidationReport();
            if (options == null)
                options = new CharacterResourcePackageValidationOptions();

            if (package == null)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.MissingManifest, "manifest.json", "", "",
                    "CharacterResourcePackage is missing.", "Create a package directory with manifest.json.");
                return report;
            }

            CharacterPackageManifest manifest = package.Manifest;
            report.PackageId = manifest != null ? manifest.PackageId : string.Empty;
            ValidateManifest(report, manifest);
            ValidateResourceCatalog(report, package.ResourceCatalog, options);
            ValidateGeometry(report, package.Geometry, package.Manifest?.PackageId);
            ValidateGameplayCamera(report, package);
            ValidatePresentationImages(report, package);
            ValidateBaseAttributes(report, package.BaseAttributes);
            if (package.TacticalDefaults != null)
            {
                if (!package.TacticalDefaults.TryValidate(out string issue)
                    || package.TacticalDefaults.CharacterStableId != package.ApplicationConfig?.CharacterStableId)
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        "CHAR_TACTICAL_DEFAULTS_INVALID", MxFramework.CharacterApplication.CharacterTacticalDefaults.RelativePath,
                        "tacticalDefaults", "", string.IsNullOrEmpty(issue) ? "Character identity does not match the package." : issue,
                        "Repair the tactical references and character identity.");
            }
            CharacterWeaponConfigurationValidator.Validate(report, package);
            return report;
        }

        private static void ValidateBaseAttributes(CharacterAuthoringValidationReport report, CharacterBaseAttributesAuthoring attributes)
        {
            if (attributes == null) return;
            const string path = CharacterBaseAttributesAuthoring.RelativePath;
            if (attributes.SchemaVersion != CharacterBaseAttributesAuthoring.CurrentSchemaVersion)
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    "CHARATTR_UNSUPPORTED_SCHEMA", path, "baseAttributes", "schemaVersion",
                    "Unsupported character attribute schema.", "Use character attribute schema 1.0.");
            var fields = new[] { "hp", "attack", "defense", "baseCohesion", "scatterRecoveryRate", "scatterRecoveryDelayFrames", "cohesionBreakRecoveryDelayFrames" };
            var values = new[] { attributes.Hp, attributes.Attack, attributes.Defense,
                attributes.BaseCohesion, attributes.ScatterRecoveryRate, attributes.ScatterRecoveryDelayFrames, attributes.CohesionBreakRecoveryDelayFrames };
            for (int i = 0; i < fields.Length; i++)
            {
                int minimum = i == 0 || i == 3 ? 1 : 0;
                if (values[i] >= minimum && (i >= 3 || values[i] <= CharacterBaseAttributesAuthoring.MaximumScalarValue)) continue;
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    "CHARATTR_INVALID_VALUE", path, "baseAttributes", fields[i],
                    fields[i] + " is outside the supported integer range.", "Use non-negative integers, positive HP/base cohesion, and HP/Attack/Defense no greater than 16777215.");
            }
        }

        private static void ValidateGameplayCamera(
            CharacterAuthoringValidationReport report,
            CharacterResourcePackage package)
        {
            CharacterGameplayCameraProfileAuthoring camera = package?.ApplicationConfig?.GameplayCameraProfile;
            if (camera == null)
                return;

            const string SourcePath = "config/character_application.json";
            const string ObjectPath = "gameplayCameraProfile";
            if (string.IsNullOrWhiteSpace(camera.ProfileId))
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                    CharacterAuthoringValidationCodes.CameraProfileInvalid, SourcePath, ObjectPath, "profileId",
                    "Explicit gameplay camera profile requires a stable profileId.", "Set profileId or remove gameplayCameraProfile to use the legacy default.");

            if (string.IsNullOrWhiteSpace(camera.FocusAbstractBoneId))
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                    CharacterAuthoringValidationCodes.CameraFocusMissing, SourcePath, ObjectPath, "focusAbstractBoneId",
                    "Explicit gameplay camera profile requires a focus abstract bone.", "Choose one abstract bone declared by geometry/sockets.json.");
            }
            else
            {
                int matches = 0;
                List<CharacterSocketProfile> sockets = package?.Geometry?.Sockets;
                for (int i = 0; sockets != null && i < sockets.Count; i++)
                {
                    if (string.Equals(sockets[i]?.AbstractBoneId, camera.FocusAbstractBoneId, System.StringComparison.Ordinal))
                        matches++;
                }
                if (matches != 1)
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.CameraAnchorUnresolved, SourcePath, ObjectPath, "focusAbstractBoneId",
                        "Gameplay camera focus abstract bone must resolve to exactly one socket. abstractBoneId=" + camera.FocusAbstractBoneId + " matches=" + matches,
                        "Assign the semantic to one valid socket in geometry/sockets.json.");
            }

            CharacterAuthoringVector3 extents = camera.FramingBoundsExtents;
            if (extents == null || !IsFinitePositive(extents.X) || !IsFinitePositive(extents.Y) || !IsFinitePositive(extents.Z))
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                    CharacterAuthoringValidationCodes.CameraBoundsInvalid, SourcePath, ObjectPath, "framingBoundsExtents",
                    "Gameplay camera framing extents must be finite and greater than zero on every axis.", "Measure the model in CharacterStudio or enter explicit positive extents.");

            CharacterGameplayCameraNormalAuthoring normal = camera.Normal;
            CharacterGameplayCameraLockedAuthoring locked = camera.Locked;
            CharacterGameplayCameraObstructionAuthoring obstruction = camera.Obstruction;
            bool normalInvalid = normal == null
                || !AreFinite(normal.Distance, normal.MinDistance, normal.MaxDistance, normal.FieldOfView, normal.Pitch, normal.YawOffset, normal.TargetPadding)
                || normal.MinDistance <= 0f || normal.Distance < normal.MinDistance || normal.Distance > normal.MaxDistance
                || normal.FieldOfView < 20f || normal.FieldOfView > 80f || normal.Pitch < -80f || normal.Pitch > 80f
                || normal.TargetPadding < 0f;
            bool lockedInvalid = locked == null
                || !AreFinite(locked.MinDistance, locked.MaxDistance, locked.TargetScreenY, locked.TargetPadding, locked.FramingScale)
                || locked.MinDistance <= 0f || locked.MaxDistance < locked.MinDistance
                || locked.TargetScreenY < 0.55f || locked.TargetScreenY > 0.85f || locked.TargetPadding < 0f
                || locked.FramingScale < 0.25f || locked.FramingScale > 1f;
            bool obstructionInvalid = obstruction == null
                || !AreFinite(obstruction.Radius, obstruction.Padding, obstruction.MinDistance, obstruction.FocusClearance)
                || obstruction.Radius < 0f || obstruction.Padding < 0f || obstruction.MinDistance < 0f || obstruction.FocusClearance < 0f;
            if (normalInvalid || lockedInvalid || obstructionInvalid)
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                    CharacterAuthoringValidationCodes.CameraProfileInvalid, SourcePath, ObjectPath, "normal/locked/obstruction",
                    "Gameplay camera distance, lens, screen placement, or obstruction settings are outside the supported finite ranges.",
                    "Use ordered positive distances, FOV 20-80, pitch -80..80, screenY 0.55..0.85, framingScale 0.25..1, and non-negative padding values.");
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool AreFinite(params float[] values)
        {
            for (int i = 0; values != null && i < values.Length; i++)
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                    return false;
            return values != null;
        }

        public static bool IsSupportedV1Shape(CharacterColliderShape shape)
        {
            return shape == CharacterColliderShape.Capsule ||
                   shape == CharacterColliderShape.Box ||
                   shape == CharacterColliderShape.Sphere;
        }

        private static void ValidateManifest(CharacterAuthoringValidationReport report, CharacterPackageManifest manifest)
        {
            if (manifest == null)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.MissingManifest, "manifest.json", "", "",
                    "manifest.json is missing.", "Create manifest.json at package root.");
                return;
            }

            if (string.IsNullOrWhiteSpace(manifest.PackageId))
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.MissingPackageId, "manifest.json", "manifest", "packageId",
                    "packageId is required.", "Set packageId, for example codex_flow_test.");

            if (string.IsNullOrWhiteSpace(manifest.StableId))
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.MissingStableId, "manifest.json", "manifest", "stableId",
                    "stableId is required.", "Set a long-term stable id, for example charpkg.codex_flow_test.");

            if (manifest.Kind != CharacterResourcePackageKind.Character)
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.InvalidPackageKind, "manifest.json", "manifest", "kind",
                    "v1 only supports Character packages.", "Set kind to Character.");

            CharacterPackageCoordinateConvention coordinate = manifest.CoordinateConvention;
            if (coordinate == null)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.InvalidUnitScale, "manifest.json", "manifest.coordinateConvention", "coordinateConvention",
                    "coordinateConvention is required.", "Declare upAxis, forwardAxis, handedness, unitScaleMeters and rotationStorage.");
                return;
            }

            if (coordinate.UnitScaleMeters <= 0f)
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.InvalidUnitScale, "manifest.json", "manifest.coordinateConvention", "unitScaleMeters",
                    "unitScaleMeters must be greater than zero.", "Use 1.0 for 1 unit = 1 meter.");

            if (coordinate.RotationStorage != CharacterRotationStorage.Quaternion)
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.RotationMustBeQuaternion, "manifest.json", "manifest.coordinateConvention", "rotationStorage",
                    "Quaternion is the only authoritative v1 rotation storage.", "Store quaternion values; keep Euler only as UI display hints.");
        }

        private static void ValidateResourceCatalog(CharacterAuthoringValidationReport report, CharacterPackageResourceCatalog catalog, CharacterResourcePackageValidationOptions options)
        {
            if (catalog == null)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.MissingResourceCatalog, "resource_catalog.json", "", "",
                    "resource_catalog.json is missing.", "Create a package-local resource catalog.");
                return;
            }

            var keys = new HashSet<string>();
            var stableIds = new HashSet<string>();
            bool hasPreview = false;
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                string objectPath = "resourceCatalog/entries/" + i;
                if (entry == null)
                    continue;

                if (string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Preview, System.StringComparison.OrdinalIgnoreCase))
                    hasPreview = true;

                if (string.IsNullOrWhiteSpace(entry.ResourceKey))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.MissingResourceKey, "resource_catalog.json", objectPath, "resourceKey",
                        "resourceKey is required.", "Use a package-local stable ResourceKey.");
                    continue;
                }

                if (!CharacterPackageResourceKeyGenerator.IsValidResourceKey(entry.ResourceKey))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.InvalidResourceKey, "resource_catalog.json", objectPath, "resourceKey",
                        "resourceKey contains unsupported characters.", "Use lowercase letters, digits, '.', '_' and '-' only.");

                if (!keys.Add(entry.ResourceKey))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.DuplicateResourceKey, "resource_catalog.json", objectPath, "resourceKey",
                        "resourceKey must be unique within one character package.", "Rename or remove the duplicate resource entry.");

                if (string.IsNullOrWhiteSpace(entry.LocalId))
                    Add(report, CharacterAuthoringValidationSeverity.Warning, CharacterAuthoringValidationGate.WarningOnly,
                        CharacterAuthoringValidationCodes.MissingResourceLocalId, "resource_catalog.json", objectPath, "localId",
                        "localId is empty; ResourceKey generation cannot be reproduced deterministically.", "Set a package-local localId such as model.body.");

                if (string.IsNullOrWhiteSpace(entry.StableId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.MissingResourceStableId, "resource_catalog.json", objectPath, "stableId",
                        "stableId is required for cross-version import, diagnostics and conflict handling.", "Set a long-term stable id for this resource.");
                else if (!stableIds.Add(entry.StableId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.DuplicateResourceStableId, "resource_catalog.json", objectPath, "stableId",
                        "stableId must be unique within one character package.", "Rename or remove the duplicate resource entry.");

                if (string.IsNullOrWhiteSpace(entry.RelativePath))
                    Add(report, CharacterAuthoringValidationSeverity.Warning, CharacterAuthoringValidationGate.WarningOnly,
                        CharacterAuthoringValidationCodes.MissingResourcePath, "resource_catalog.json", objectPath, "relativePath",
                        "relativePath is empty; #223 will define stricter resource file validation.", "Set a package-relative path when the resource exists in this package.");
                else if (!CharacterPackageResourcePipeline.IsSafePackageRelativePath(entry.RelativePath))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ImportBlocked,
                        CharacterAuthoringValidationCodes.InvalidResourcePath, "resource_catalog.json", objectPath, "relativePath",
                        "relativePath must stay inside the character package.", "Use a package-relative path without absolute roots or '..'.");

                if (entry.ImportHints != null && !string.IsNullOrWhiteSpace(entry.ImportHints.TargetRelativePath) &&
                    !CharacterPackageResourcePipeline.IsSafePackageRelativePath(entry.ImportHints.TargetRelativePath))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ImportBlocked,
                        CharacterAuthoringValidationCodes.InvalidImportTargetPath, "resource_catalog.json", objectPath, "importHints.targetRelativePath",
                        "Unity target path must be project-relative and must not escape the generated character package root.", "Use a generated package relative path.");

                ValidateResourceFormat(report, entry, objectPath);
                ValidateResourceFile(report, entry, objectPath, options);
            }

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;
                ValidateResourceDependencies(report, entry, "resourceCatalog/entries/" + i, keys);
            }

            if (options.ValidatePreviewResources && !hasPreview)
                Add(report, CharacterAuthoringValidationSeverity.Warning, CharacterAuthoringValidationGate.WarningOnly,
                    CharacterAuthoringValidationCodes.MissingPreviewResource, "resource_catalog.json", "resourceCatalog", "entries",
                    "package has no preview resource entry.", "Add a preview thumbnail or preview mesh resource so external tools can show the package without loading full runtime data.");
        }

        private static void ValidateResourceFormat(CharacterAuthoringValidationReport report, CharacterPackageResourceEntry entry, string objectPath)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.RelativePath))
                return;

            if (CharacterPackageResourcePipeline.IsFutureFormat(entry))
            {
                Add(report, CharacterAuthoringValidationSeverity.Warning, CharacterAuthoringValidationGate.WarningOnly,
                    CharacterAuthoringValidationCodes.FutureResourceFormat, "resource_catalog.json", objectPath, "sourceFormat",
                    "FBX is recorded as future/optional in the v1 resource package pipeline.", "Prefer glTF/GLB for v1, or add an importer/conversion step before Unity import.");
                return;
            }

            if (!CharacterPackageResourcePipeline.IsSupportedV1Format(entry))
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ImportBlocked,
                    CharacterAuthoringValidationCodes.UnsupportedResourceFormat, "resource_catalog.json", objectPath, "sourceFormat",
                    "resource source format is not supported by the v1 character package contract.", "Use glTF/GLB for models, glTF/GLB/anim/json for animation, png/jpg/tga for textures/previews, json for config/material/vfx descriptors, or wav/ogg for audio.");
        }

        private static void ValidateResourceFile(CharacterAuthoringValidationReport report, CharacterPackageResourceEntry entry, string objectPath, CharacterResourcePackageValidationOptions options)
        {
            if (entry == null || options == null || !options.ValidateResourceFiles)
                return;

            if (string.IsNullOrWhiteSpace(entry.RelativePath))
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ImportBlocked,
                    CharacterAuthoringValidationCodes.MissingResourceFile, "resource_catalog.json", objectPath, "relativePath",
                    "resource file validation is enabled but relativePath is empty.", "Set relativePath or remove this resource entry.");
                return;
            }

            string path = CharacterPackageResourcePipeline.ResolvePackagePath(options.PackageRootPath, entry.RelativePath);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ImportBlocked,
                    CharacterAuthoringValidationCodes.MissingResourceFile, entry.RelativePath, objectPath, "relativePath",
                    "resource file is missing from the character package.", "Place the resource at the declared package-relative path or update resource_catalog.json.");
                return;
            }

            if (!options.ValidateResourceHashes)
                return;

            string declaredHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(entry);
            if (string.IsNullOrWhiteSpace(declaredHash))
            {
                Add(report, CharacterAuthoringValidationSeverity.Warning, CharacterAuthoringValidationGate.WarningOnly,
                    CharacterAuthoringValidationCodes.MissingResourceHash, "resource_catalog.json", objectPath, "hashes.contentHash",
                    "resource file exists but no content hash is declared.", "Record a sha256 content hash before publishing or importing this package.");
                return;
            }

            string actualHash = CharacterPackageHashUtility.ComputeFileSha256(path);
            if (!string.Equals(declaredHash, actualHash, System.StringComparison.OrdinalIgnoreCase))
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ImportBlocked,
                    CharacterAuthoringValidationCodes.ResourceHashMismatch, entry.RelativePath, objectPath, "hashes.contentHash",
                    "declared content hash does not match the package file.", "Recompute hashes after changing source assets.");
        }

        private static void ValidateResourceDependencies(CharacterAuthoringValidationReport report, CharacterPackageResourceEntry entry, string objectPath, HashSet<string> keys)
        {
            if (entry == null || entry.Dependencies == null)
                return;

            var dependencies = new HashSet<string>();
            for (int i = 0; i < entry.Dependencies.Count; i++)
            {
                CharacterPackageResourceDependency dependency = entry.Dependencies[i];
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.ResourceKey))
                    continue;

                string dependencyPath = objectPath + "/dependencies/" + i;
                if (string.Equals(entry.ResourceKey, dependency.ResourceKey, System.StringComparison.Ordinal))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ImportBlocked,
                        CharacterAuthoringValidationCodes.SelfResourceDependency, "resource_catalog.json", dependencyPath, "resourceKey",
                        "resource dependency cannot point to itself.", "Remove the self dependency.");

                if (!dependencies.Add(dependency.ResourceKey))
                    Add(report, CharacterAuthoringValidationSeverity.Warning, CharacterAuthoringValidationGate.WarningOnly,
                        CharacterAuthoringValidationCodes.DuplicateResourceDependency, "resource_catalog.json", dependencyPath, "resourceKey",
                        "resource dependency is duplicated on the same resource entry.", "Keep one dependency edge per target ResourceKey.");

                if (!keys.Contains(dependency.ResourceKey))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ImportBlocked,
                        CharacterAuthoringValidationCodes.MissingResourceDependency, "resource_catalog.json", dependencyPath, "resourceKey",
                        "resource dependency references a missing package-local ResourceKey.", "Add the dependency resource to resource_catalog.json or remove this edge.");
            }
        }

        private static void ValidateGeometry(CharacterAuthoringValidationReport report, CharacterAuthoringGeometry geometry, string packageId)
        {
            if (geometry == null)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.MissingGeometry, "geometry/", "", "",
                    "geometry authoring data is missing.", "Create geometry/body_geometry.json and related geometry files.");
                return;
            }

            if (geometry.BodyProfile == null || string.IsNullOrWhiteSpace(geometry.BodyProfile.ProfileId))
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.MissingBodyProfile, "geometry/body_geometry.json", "geometry/bodyProfile", "profileId",
                    "Body geometry profile id is required.", "Set bodyProfile.profileId.");

            var partIds = new HashSet<string>();
            var parts = new List<CharacterBodyPartAuthoring>();
            for (int i = 0; i < geometry.BodyParts.Count; i++)
            {
                CharacterBodyPartAuthoring part = geometry.BodyParts[i];
                if (part == null)
                    continue;
                parts.Add(part);
                if (string.IsNullOrWhiteSpace(part.PartId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.MissingBodyPart, "geometry/body_parts.json", "geometry/bodyParts/" + i, "partId",
                        "partId is required.", "Set a stable part id such as head, torso, core or tail.");
                    continue;
                }

                if (!partIds.Add(part.PartId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.DuplicateBodyPart, "geometry/body_parts.json", "geometry/bodyParts/" + part.PartId, "partId",
                        "partId must be unique in one geometry profile.", "Rename or merge duplicate body parts.");
            }

            for (int i = 0; i < parts.Count; i++)
            {
                CharacterBodyPartAuthoring part = parts[i];
                string objectPath = "geometry/bodyParts/" + (!string.IsNullOrWhiteSpace(part.PartId) ? part.PartId : i.ToString());
                if (!string.IsNullOrWhiteSpace(part.ParentPartId) && !partIds.Contains(part.ParentPartId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingBodyPart, "geometry/body_parts.json", objectPath, "parentPartId",
                        "body part parentPartId does not exist in the same part set.", "Use an existing body part id or clear parentPartId.");

                if (part.PartKind == CharacterAuthoringBodyPartKind.Bone &&
                    string.IsNullOrWhiteSpace(part.BonePath) &&
                    string.IsNullOrWhiteSpace(part.LocatorId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingBodyPartLocator, "geometry/body_parts.json", objectPath, "bonePath",
                        "bone body part must resolve to a representative bone path or locator id.", "Set bonePath or locatorId, for example bone.Head.");
            }

            var declaredSocketIds = new HashSet<string>();
            for (int i = 0; i < geometry.Sockets.Count; i++)
            {
                CharacterSocketProfile socket = geometry.Sockets[i];
                if (socket != null && !string.IsNullOrWhiteSpace(socket.SocketId))
                    declaredSocketIds.Add(socket.SocketId);
            }

            ValidateBodyPartContactPoints(report, geometry, partIds, declaredSocketIds);

            var traceIds = new HashSet<string>();
            for (int i = 0; i < geometry.Traces.Count; i++)
            {
                WeaponTraceProfile trace = geometry.Traces[i];
                if (trace == null)
                    continue;

                string objectPath = TraceObjectPath(trace);
                if (!string.IsNullOrWhiteSpace(trace.TraceId))
                    traceIds.Add(trace.TraceId);

                ValidateWeaponTraceProximalRange(report, trace, objectPath);
            }

            var socketIds = new HashSet<string>();
            var abstractBoneIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < geometry.Sockets.Count; i++)
            {
                CharacterSocketProfile socket = geometry.Sockets[i];
                if (socket == null)
                    continue;
                string objectPath = "geometry/sockets/" + (!string.IsNullOrWhiteSpace(socket.SocketId) ? socket.SocketId : i.ToString());
                if (string.IsNullOrWhiteSpace(socket.SocketId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingSocketId, "geometry/sockets.json", objectPath, "socketId",
                        "socketId is required for socket references.", "Set a stable socket id such as mainHand.");
                else if (!socketIds.Add(socket.SocketId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.DuplicateSocket, "geometry/sockets.json", objectPath, "socketId",
                        "socketId must be unique in one geometry profile.", "Rename or merge duplicate sockets.");

                if (!string.IsNullOrWhiteSpace(socket.AbstractBoneId))
                {
                    if (!CharacterAbstractBoneIds.IsValid(packageId, socket.AbstractBoneId))
                        Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                            CharacterAuthoringValidationCodes.InvalidAbstractBoneId, "geometry/sockets.json", objectPath, "abstractBoneId",
                            "abstractBoneId must be a core id or ext.<packageId>.<name>.", "Choose a core semantic or namespace the id to this package.");
                    else if (!abstractBoneIds.Add(socket.AbstractBoneId))
                        Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                            CharacterAuthoringValidationCodes.DuplicateAbstractBoneId, "geometry/sockets.json", objectPath, "abstractBoneId",
                            "abstractBoneId must map to exactly one socket in a character package.", "Keep one socket for this abstract bone semantic.");
                }

                if (!string.IsNullOrWhiteSpace(socket.ParentPartId) && !partIds.Contains(socket.ParentPartId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingSocketPart, "geometry/sockets.json", "geometry/sockets/" + socket.SocketId, "parentPartId",
                        "socket parentPartId does not exist in body parts.", "Use an existing body part id or add the missing body part.");

                if (!HasDeclaredSocketBinding(socket))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingSocketLocator, "geometry/sockets.json", objectPath, "bonePath",
                        "socket binding does not match its localPose.parentKind.",
                        "Use ModelRoot for a stable root-local socket, or set the matching bonePath, locatorPath or parentPartId.");
            }

            if (!abstractBoneIds.Contains(CharacterAbstractBoneIds.Head))
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                    CharacterAuthoringValidationCodes.MissingRequiredAbstractBone, "geometry/sockets.json", "geometry/sockets", "abstractBoneId",
                    "Every spawnable character requires a logical head abstract bone socket.", "Assign abstractBoneId=head to the character's logical head or top socket.");

            for (int i = 0; i < geometry.Sockets.Count; i++)
            {
                CharacterSocketProfile socket = geometry.Sockets[i];
                if (socket == null || string.IsNullOrWhiteSpace(socket.MirrorPairSocketId))
                    continue;

                string objectPath = "geometry/sockets/" + (!string.IsNullOrWhiteSpace(socket.SocketId) ? socket.SocketId : i.ToString());
                if (string.Equals(socket.SocketId, socket.MirrorPairSocketId, System.StringComparison.Ordinal))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.SelfMirrorSocket, "geometry/sockets.json", objectPath, "mirrorPairSocketId",
                        "socket mirrorPairSocketId cannot point to itself.", "Use the opposite socket id or clear mirrorPairSocketId.");
                else if (!socketIds.Contains(socket.MirrorPairSocketId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingMirrorSocket, "geometry/sockets.json", objectPath, "mirrorPairSocketId",
                        "socket mirrorPairSocketId references a missing socket.", "Use an existing socket id or clear mirrorPairSocketId.");
            }

            var colliderIds = new HashSet<string>();
            var hitZoneIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < geometry.Colliders.Count; i++)
            {
                CharacterBodyColliderProfile collider = geometry.Colliders[i];
                if (collider == null)
                    continue;

                string objectPath = "geometry/colliders/" + (!string.IsNullOrWhiteSpace(collider.ColliderId) ? collider.ColliderId : i.ToString());
                if (!string.IsNullOrWhiteSpace(collider.ColliderId) && !colliderIds.Add(collider.ColliderId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.DuplicateCollider, "geometry/body_colliders.json", objectPath, "colliderId",
                        "colliderId must be unique in one geometry profile.", "Rename or merge duplicate colliders.");

                if (!IsSupportedV1Shape(collider.Shape))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.UnsupportedColliderShape, "geometry/body_colliders.json", objectPath, "shape",
                        "v1 only supports capsule, box and sphere collider shapes.", "Replace this collider with capsule, box or sphere; convex/custom mesh is future work.");

                if (string.IsNullOrWhiteSpace(collider.PartId) || !partIds.Contains(collider.PartId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingColliderPart, "geometry/body_colliders.json", objectPath, "partId",
                        "collider partId does not exist in body parts.", "Use an existing body part id or add the missing body part.");

                if (string.IsNullOrWhiteSpace(collider.HitZoneId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingColliderHitZone, "geometry/body_colliders.json", objectPath, "hitZoneId",
                        "collider hitZoneId is required for BodyPartHitZoneResolver mapping.", "Set a stable hit zone id, for example hit.head.");
                else
                    hitZoneIds.Add(collider.HitZoneId);

                ValidateColliderDimensions(report, collider, objectPath);
            }

            ValidateDamageZones(report, geometry, hitZoneIds);
            ValidateEngagementAnchors(report, geometry);
            ValidateBodyPresence(report, geometry);
            ValidateBodyPartDamage(report, geometry);
            ValidateAuthorityPoseRig(report, geometry);
            ValidateRootCapsuleCenterContract(report, geometry);

            var weaponSlotIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < geometry.WeaponSlots.Count; i++)
            {
                CharacterWeaponSlotProfile slot = geometry.WeaponSlots[i];
                string objectPath = "geometry/weaponSlots/" + (slot?.SlotId ?? i.ToString());
                if (slot == null || string.IsNullOrWhiteSpace(slot.SlotId) || !weaponSlotIds.Add(slot.SlotId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.CharacterWeaponSlotDuplicate, "geometry/weapon_slots.json", objectPath, "slotId",
                        "character weapon slot ids must be non-empty and unique.", "Keep one binding for each semantic weapon slot.");
                else if (string.IsNullOrWhiteSpace(slot.AttachSocketId) || !socketIds.Contains(slot.AttachSocketId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.CharacterWeaponSlotSocketMissing, "geometry/weapon_slots.json", objectPath, "attachSocketId",
                        "character weapon slot references a missing socket.", "Bind the slot to an existing character socket.");
            }

            for (int i = 0; i < geometry.WeaponAttachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = geometry.WeaponAttachments[i];
                if (attachment == null)
                    continue;

                string objectPath = "geometry/weaponAttachments/" + (!string.IsNullOrWhiteSpace(attachment.AttachmentId) ? attachment.AttachmentId : i.ToString());
                if (string.IsNullOrWhiteSpace(attachment.AttachSocketId) || !socketIds.Contains(attachment.AttachSocketId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingAttachmentSocket, "geometry/weapon_attachments.json", objectPath, "attachSocketId",
                        "weapon attachment references a missing socket.", "Use an existing socket id.");

                if (!string.IsNullOrWhiteSpace(attachment.TraceId) && !traceIds.Contains(attachment.TraceId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingAttachmentTrace, "geometry/weapon_attachments.json", objectPath, "traceId",
                        "weapon attachment references a missing trace.", "Create the trace or clear traceId.");

                WeaponTraceProfile selectedTrace = FindTraceById(geometry, attachment.TraceId);
                if (selectedTrace != null)
                {
                    ValidateAttachmentTraceMatch(report, attachment, selectedTrace);
                    ValidateLegacyTraceOverrideConflicts(report, attachment, selectedTrace, objectPath);
                }

                if (RequiresWeaponTraceBinding(attachment) && string.IsNullOrWhiteSpace(attachment.TraceId))
                {
                    int matchingTraceCount = CountMatchingTracesForAttachment(geometry, attachment);
                    if (matchingTraceCount == 0)
                        Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                            CharacterAuthoringValidationCodes.MissingWeaponTraceBinding, "geometry/weapon_attachments.json", objectPath, "traceId",
                            "attack-capable weapon attachment has no weapon trace binding for slot-based hit traces.", "Set traceId or add a geometry trace with the same weaponId.");
                    else if (matchingTraceCount > 1)
                        Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                            CharacterAuthoringValidationCodes.AmbiguousWeaponTraceBinding, "geometry/weapon_attachments.json", objectPath, "traceId",
                            "attack-capable weapon attachment matches multiple weapon traces.", "Set traceId to the exact trace geometry, for example trace.iron_sword.blade.");
                }

                if (!string.IsNullOrWhiteSpace(attachment.TraceStartSocketId) && !socketIds.Contains(attachment.TraceStartSocketId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingAttachmentTraceSocket, "geometry/weapon_attachments.json", objectPath, "traceStartSocketId",
                        "weapon attachment traceStartSocketId references a missing socket.", "Use an existing socket id or clear traceStartSocketId.");

                if (!string.IsNullOrWhiteSpace(attachment.TraceEndSocketId) && !socketIds.Contains(attachment.TraceEndSocketId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingAttachmentTraceSocket, "geometry/weapon_attachments.json", objectPath, "traceEndSocketId",
                        "weapon attachment traceEndSocketId references a missing socket.", "Use an existing socket id or clear traceEndSocketId.");

                if (attachment.TraceRadius < 0f)
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.InvalidAttachmentTraceRadius, "geometry/weapon_attachments.json", objectPath, "traceRadius",
                        "weapon attachment traceRadius must be zero or positive.", "Use 0 to inherit the trace profile radius, or set a positive override.");
            }
        }

        private static void ValidateDamageZones(
            CharacterAuthoringValidationReport report,
            CharacterAuthoringGeometry geometry,
            HashSet<string> knownHitZoneIds)
        {
            const string sourcePath = "geometry/damage_zones.json";
            if (geometry.DamageZonesDocumentState == CharacterDamageZonesDocumentState.Missing)
                return;

            CharacterDamageZonesDocument document = geometry.DamageZones;
            if (geometry.DamageZonesDocumentState == CharacterDamageZonesDocumentState.UnsupportedSchema)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.DamageZoneUnsupportedSchema, sourcePath, "damageZones", "schemaVersion",
                    "DamageZone format or schemaVersion is not supported and the original payload is read-only.",
                    "Use mx.characterDamageZones.v1 schema 1.0 or upgrade with a supported migrator.");
                return;
            }

            if (geometry.DamageZonesDocumentState == CharacterDamageZonesDocumentState.Invalid || document == null)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.DamageZoneReadOnlyDocument, sourcePath, "damageZones", "",
                    "DamageZone document could not be read without data loss and is write-ineligible.",
                    "Repair the JSON document before saving or compiling the package.");
                return;
            }

            if (!string.Equals(document.Format, CharacterDamageZonesDocument.CurrentFormat, System.StringComparison.Ordinal))
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.DamageZoneInvalidFormat, sourcePath, "damageZones", "format",
                    "DamageZone format must be mx.characterDamageZones.v1.", "Set the current format without changing the authored zones.");
            if (!string.Equals(document.SchemaVersion, CharacterDamageZonesDocument.CurrentSchemaVersion, System.StringComparison.Ordinal))
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.DamageZoneUnsupportedSchema, sourcePath, "damageZones", "schemaVersion",
                    "DamageZone schemaVersion is unsupported.", "Use schemaVersion 1.0.");

            var zoneIds = new HashSet<string>(System.StringComparer.Ordinal);
            var ownersByHitZone = new Dictionary<string, string>(System.StringComparer.Ordinal);
            List<CharacterDamageZoneAuthoring> zones = document.Zones ?? new List<CharacterDamageZoneAuthoring>();
            for (int i = 0; i < zones.Count; i++)
            {
                CharacterDamageZoneAuthoring zone = zones[i];
                string zoneId = zone?.ZoneId ?? string.Empty;
                string objectPath = "damageZones/zones/" + (!string.IsNullOrWhiteSpace(zoneId) ? zoneId : i.ToString());
                if (string.IsNullOrWhiteSpace(zoneId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.DamageZoneEmptyZoneId, sourcePath, objectPath, "zoneId",
                        "DamageZone zoneId is required.", "Assign a stable package-local Zone id.");
                else if (!zoneIds.Add(zoneId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.DamageZoneDuplicateZoneId, sourcePath, objectPath, "zoneId",
                        "DamageZone zoneId must be unique.", "Rename or merge the duplicate Zone.");

                if (zone == null || zone.HitZoneIds == null || zone.HitZoneIds.Count == 0)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.DamageZoneEmptyMembers, sourcePath, objectPath, "hitZoneIds",
                        "DamageZone must own at least one HitZone.", "Add a valid HitZone id or remove the Zone.");
                    continue;
                }

                if (zone.AuthoredPriority < -100000 || zone.AuthoredPriority > 100000)
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.DamageZoneInvalidPriority, sourcePath, objectPath, "authoredPriority",
                        "DamageZone authoredPriority must be between -100000 and 100000.", "Use an in-range deterministic priority.");

                var localMembers = new HashSet<string>(System.StringComparer.Ordinal);
                for (int memberIndex = 0; memberIndex < zone.HitZoneIds.Count; memberIndex++)
                {
                    string hitZoneId = zone.HitZoneIds[memberIndex] ?? string.Empty;
                    string memberPath = objectPath + "/hitZoneIds/" + memberIndex;
                    if (string.IsNullOrWhiteSpace(hitZoneId) || !knownHitZoneIds.Contains(hitZoneId))
                        Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                            CharacterAuthoringValidationCodes.DamageZoneUnknownHitZone, sourcePath, memberPath, "hitZoneId",
                            "DamageZone references an unknown HitZone.", "Use a hitZoneId declared by body_colliders.json.");
                    bool duplicateInZone = !localMembers.Add(hitZoneId);
                    bool alreadyOwned = ownersByHitZone.TryGetValue(hitZoneId, out string previousOwner);
                    if (duplicateInZone || alreadyOwned)
                        Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                            CharacterAuthoringValidationCodes.DamageZoneDuplicateHitZoneMembership, sourcePath, memberPath, "hitZoneId",
                            "A HitZone can belong to at most one DamageZone; previous owner is '" + (previousOwner ?? zoneId) + "'.",
                            "Keep the HitZone in exactly one Zone.");
                    else
                        ownersByHitZone[hitZoneId] = zoneId;
                }
            }
        }

        private static void ValidateEngagementAnchors(
            CharacterAuthoringValidationReport report,
            CharacterAuthoringGeometry geometry)
        {
            const string sourcePath = "geometry/engagement_anchors.json";
            if (geometry.EngagementAnchorsDocumentState
                == CharacterEngagementAnchorsDocumentState.Missing)
            {
                return;
            }

            CharacterEngagementAnchorsDocument document = geometry.EngagementAnchors;
            if (geometry.EngagementAnchorsDocumentState
                == CharacterEngagementAnchorsDocumentState.UnsupportedSchema)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error,
                    CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.EngagementAnchorUnsupportedSchema,
                    sourcePath, "engagementAnchors", "schemaVersion",
                    "EngagementAnchor format or schemaVersion is unsupported and the original payload is read-only.",
                    "Use mx.characterEngagementAnchors.v1 schema 1.0 or a supported explicit migrator.");
                return;
            }
            if (geometry.EngagementAnchorsDocumentState
                    == CharacterEngagementAnchorsDocumentState.Invalid
                || document == null)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error,
                    CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.EngagementAnchorReadOnlyDocument,
                    sourcePath, "engagementAnchors", string.Empty,
                    "EngagementAnchor document could not be read without data loss and is write-ineligible.",
                    "Repair the JSON document before saving or compiling the package.");
                return;
            }

            if (!string.Equals(
                    document.Format,
                    CharacterEngagementAnchorsDocument.CurrentFormat,
                    System.StringComparison.Ordinal))
            {
                Add(report, CharacterAuthoringValidationSeverity.Error,
                    CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.EngagementAnchorInvalidFormat,
                    sourcePath, "engagementAnchors", "format",
                    "EngagementAnchor format must be mx.characterEngagementAnchors.v1.",
                    "Set the current format without changing authored anchors.");
            }
            if (!string.Equals(
                    document.SchemaVersion,
                    CharacterEngagementAnchorsDocument.CurrentSchemaVersion,
                    System.StringComparison.Ordinal))
            {
                Add(report, CharacterAuthoringValidationSeverity.Error,
                    CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.EngagementAnchorUnsupportedSchema,
                    sourcePath, "engagementAnchors", "schemaVersion",
                    "EngagementAnchor schemaVersion is unsupported.", "Use schemaVersion 1.0.");
            }
            if (document.Revision <= 0)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error,
                    CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.EngagementAnchorInvalidRevision,
                    sourcePath, "engagementAnchors", "revision",
                    "EngagementAnchor revision must be positive.", "Use revision 1 or a later positive revision.");
            }

            var zones = new Dictionary<string, CharacterDamageZoneAuthoring>(System.StringComparer.Ordinal);
            if (geometry.DamageZonesDocumentState == CharacterDamageZonesDocumentState.Current
                && geometry.DamageZones?.Zones != null)
            {
                for (int i = 0; i < geometry.DamageZones.Zones.Count; i++)
                {
                    CharacterDamageZoneAuthoring zone = geometry.DamageZones.Zones[i];
                    if (zone != null && !string.IsNullOrWhiteSpace(zone.ZoneId)
                        && !zones.ContainsKey(zone.ZoneId))
                    {
                        zones.Add(zone.ZoneId, zone);
                    }
                }
            }

            var anchorIds = new HashSet<string>(System.StringComparer.Ordinal);
            List<CharacterEngagementAnchorAuthoring> anchors =
                document.Anchors ?? new List<CharacterEngagementAnchorAuthoring>();
            for (int i = 0; i < anchors.Count; i++)
            {
                CharacterEngagementAnchorAuthoring anchor = anchors[i];
                string anchorId = anchor?.AnchorId ?? string.Empty;
                string objectPath = "engagementAnchors/anchors/"
                    + (!string.IsNullOrWhiteSpace(anchorId) ? anchorId : i.ToString());
                if (!IsStableAnchorId(anchorId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error,
                        CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.EngagementAnchorEmptyId,
                        sourcePath, objectPath, "anchorId",
                        "EngagementAnchor anchorId must be a non-empty stable package-local id.",
                        "Use lowercase letters, digits, '.', '_' or '-'.");
                }
                else if (!anchorIds.Add(anchorId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error,
                        CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.EngagementAnchorDuplicateId,
                        sourcePath, objectPath, "anchorId",
                        "EngagementAnchor anchorId must be unique.", "Rename or merge the duplicate Anchor.");
                }

                string zoneId = anchor?.ZoneId ?? string.Empty;
                if (!zones.TryGetValue(zoneId, out CharacterDamageZoneAuthoring zone))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error,
                        CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.EngagementAnchorUnknownZone,
                        sourcePath, objectPath, "zoneId",
                        "EngagementAnchor references an unknown DamageZone.",
                        "Use a Zone declared by geometry/damage_zones.json.");
                }
                else if (!zone.Approachable)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error,
                        CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.EngagementAnchorZoneNotApproachable,
                        sourcePath, objectPath, "zoneId",
                        "EngagementAnchor may only reference a DamageZone with approachable=true.",
                        "Enable approach for the Zone or choose another Zone.");
                }

                if (anchor == null
                    || anchor.ReferenceFrame != CharacterEngagementAnchorReferenceFrame.ModelRoot)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error,
                        CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.EngagementAnchorInvalidReferenceFrame,
                        sourcePath, objectPath, "referenceFrame",
                        "EngagementAnchor v1 only supports ModelRoot-local coordinates.",
                        "Use ModelRoot; World, bone and Hurtbox-following anchors are rejected.");
                }

                CharacterAuthoringIntVector3 position = anchor?.LocalPositionMillimeters;
                const int maximumCoordinate = 10000000;
                if (position == null
                    || position.X < -maximumCoordinate || position.X > maximumCoordinate
                    || position.Y < -maximumCoordinate || position.Y > maximumCoordinate
                    || position.Z < -maximumCoordinate || position.Z > maximumCoordinate)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error,
                        CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.EngagementAnchorInvalidPosition,
                        sourcePath, objectPath, "localPositionMillimeters",
                        "EngagementAnchor local position must be integral millimeters inside the supported range.",
                        "Use ModelRoot-local coordinates between -10000000 and 10000000 millimeters per axis.");
                }
                if (anchor == null || anchor.YawMilliDegrees < 0 || anchor.YawMilliDegrees >= 360000)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error,
                        CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.EngagementAnchorInvalidYaw,
                        sourcePath, objectPath, "yawMilliDegrees",
                        "EngagementAnchor yaw must use canonical [0,359999] millidegrees.",
                        "Normalize the ModelRoot-local yaw before saving.");
                }
                if (anchor == null || anchor.Capacity < 1 || anchor.Capacity > 64)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error,
                        CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.EngagementAnchorInvalidCapacity,
                        sourcePath, objectPath, "capacity",
                        "EngagementAnchor capacity must be between 1 and 64.",
                        "Use a bounded positive capacity.");
                }
                if (anchor == null
                    || anchor.AuthoredPriority < -100000 || anchor.AuthoredPriority > 100000)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error,
                        CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.EngagementAnchorInvalidPriority,
                        sourcePath, objectPath, "authoredPriority",
                        "EngagementAnchor authoredPriority must be between -100000 and 100000.",
                        "Use an in-range deterministic priority.");
                }
                if (anchor != null && !string.IsNullOrEmpty(anchor.PhaseId)
                    && !IsStableAnchorId(anchor.PhaseId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error,
                        CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.EngagementAnchorInvalidPhaseId,
                        sourcePath, objectPath, "phaseId",
                        "EngagementAnchor phaseId must be empty or a stable id.",
                        "Use lowercase letters, digits, '.', '_' or '-'.");
                }
            }
        }

        private static bool IsStableAnchorId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 96)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLower(c) && !char.IsDigit(c) && c != '.' && c != '_' && c != '-')
                    return false;
            }
            return true;
        }

        private static bool HasDeclaredSocketBinding(CharacterSocketProfile socket)
        {
            if (socket == null)
                return false;

            CharacterPoseParentKind parentKind = socket.LocalPose?.ParentKind ?? CharacterPoseParentKind.Unknown;
            switch (parentKind)
            {
                case CharacterPoseParentKind.ModelRoot:
                    return true;
                case CharacterPoseParentKind.Bone:
                    return !string.IsNullOrWhiteSpace(socket.BonePath);
                case CharacterPoseParentKind.Locator:
                    return !string.IsNullOrWhiteSpace(socket.LocatorPath);
                case CharacterPoseParentKind.BodyPart:
                    return !string.IsNullOrWhiteSpace(socket.ParentPartId);
                default:
                    return !string.IsNullOrWhiteSpace(socket.ParentPartId)
                        || !string.IsNullOrWhiteSpace(socket.BonePath)
                        || !string.IsNullOrWhiteSpace(socket.LocatorPath);
            }
        }

        private static bool RequiresWeaponTraceBinding(WeaponAttachmentProfile attachment)
        {
            if (attachment == null)
                return false;

            return string.Equals(attachment.EquipSlot, "mainHand", System.StringComparison.Ordinal) ||
                   string.Equals(attachment.EquipSlot, "twoHand", System.StringComparison.Ordinal) ||
                   string.Equals(attachment.EquipSlot, "naturalWeapon", System.StringComparison.Ordinal);
        }

        private static WeaponTraceProfile FindTraceById(CharacterAuthoringGeometry geometry, string traceId)
        {
            if (geometry == null || string.IsNullOrWhiteSpace(traceId))
                return null;

            for (int i = 0; i < geometry.Traces.Count; i++)
            {
                WeaponTraceProfile trace = geometry.Traces[i];
                if (trace != null && string.Equals(trace.TraceId, traceId, System.StringComparison.Ordinal))
                    return trace;
            }

            return null;
        }

        private static int CountMatchingTracesForAttachment(CharacterAuthoringGeometry geometry, WeaponAttachmentProfile attachment)
        {
            if (geometry == null || attachment == null)
                return 0;

            int count = 0;
            for (int i = 0; i < geometry.Traces.Count; i++)
            {
                WeaponTraceProfile trace = geometry.Traces[i];
                if (trace == null || string.IsNullOrWhiteSpace(trace.TraceId))
                    continue;
                if (!string.IsNullOrWhiteSpace(attachment.SingleWeaponConfigurationId) &&
                    !string.Equals(trace.WeaponId, attachment.SingleWeaponConfigurationId, System.StringComparison.Ordinal))
                    continue;
                if (!string.IsNullOrWhiteSpace(attachment.EquipSlot) &&
                    !string.IsNullOrWhiteSpace(trace.EquipSlot) &&
                    !string.Equals(trace.EquipSlot, attachment.EquipSlot, System.StringComparison.Ordinal))
                    continue;

                count++;
            }

            return count;
        }

        private static void ValidateAttachmentTraceMatch(
            CharacterAuthoringValidationReport report,
            WeaponAttachmentProfile attachment,
            WeaponTraceProfile trace)
        {
            if (attachment == null || trace == null)
                return;

            if (!string.IsNullOrWhiteSpace(attachment.SingleWeaponConfigurationId) &&
                !string.IsNullOrWhiteSpace(trace.WeaponId) &&
                !string.Equals(attachment.SingleWeaponConfigurationId, trace.WeaponId, System.StringComparison.Ordinal))
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                    CharacterAuthoringValidationCodes.AttachmentTraceMismatch, "geometry/traces.json", TraceObjectPath(trace), "weaponId",
                    "weapon attachment traceId points at trace geometry for a different weaponId.", "Set weapon_attachments.traceId to a trace owned by this weapon, or fix geometry/traces weaponId.");

            if (!string.IsNullOrWhiteSpace(attachment.EquipSlot) &&
                !string.IsNullOrWhiteSpace(trace.EquipSlot) &&
                !string.Equals(attachment.EquipSlot, trace.EquipSlot, System.StringComparison.Ordinal))
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                    CharacterAuthoringValidationCodes.AttachmentTraceMismatch, "geometry/traces.json", TraceObjectPath(trace), "equipSlot",
                    "weapon attachment traceId points at trace geometry for a different equipSlot.", "Set weapon_attachments.traceId to a trace for this slot, or fix geometry/traces equipSlot.");
        }

        private static void ValidateLegacyTraceOverrideConflicts(
            CharacterAuthoringValidationReport report,
            WeaponAttachmentProfile attachment,
            WeaponTraceProfile trace,
            string attachmentObjectPath)
        {
            if (attachment == null || trace == null)
                return;

            if (attachment.TraceRadius > 0f && trace.Radius > 0f && System.Math.Abs(attachment.TraceRadius - trace.Radius) > 0.0001f)
                Add(report, CharacterAuthoringValidationSeverity.Warning, CharacterAuthoringValidationGate.WarningOnly,
                    CharacterAuthoringValidationCodes.LegacyTraceOverrideConflict, "geometry/weapon_attachments.json", attachmentObjectPath, "traceRadius",
                    "legacy attachment traceRadius differs from geometry trace radius.", "Prefer geometry/traces radius as the authoring source and clear the attachment override when migration is complete.");

            if (attachment.TraceSampleRule != WeaponTraceSampleRule.Unknown &&
                trace.SampleRule != WeaponTraceSampleRule.Unknown &&
                attachment.TraceSampleRule != trace.SampleRule)
                Add(report, CharacterAuthoringValidationSeverity.Warning, CharacterAuthoringValidationGate.WarningOnly,
                    CharacterAuthoringValidationCodes.LegacyTraceOverrideConflict, "geometry/weapon_attachments.json", attachmentObjectPath, "traceSampleRule",
                    "legacy attachment traceSampleRule differs from geometry trace sampleRule.", "Prefer geometry/traces sampleRule as the authoring source and clear the attachment override when migration is complete.");

            if (!string.IsNullOrWhiteSpace(attachment.TraceStartSocketId) &&
                trace.StartPose != null &&
                trace.StartPose.ParentKind == CharacterPoseParentKind.Socket &&
                !string.IsNullOrWhiteSpace(trace.StartPose.ParentPath) &&
                !string.Equals(attachment.TraceStartSocketId, trace.StartPose.ParentPath, System.StringComparison.Ordinal))
                Add(report, CharacterAuthoringValidationSeverity.Warning, CharacterAuthoringValidationGate.WarningOnly,
                    CharacterAuthoringValidationCodes.LegacyTraceOverrideConflict, "geometry/weapon_attachments.json", attachmentObjectPath, "traceStartSocketId",
                    "legacy attachment traceStartSocketId differs from geometry trace startPose socket.", "Prefer geometry/traces startPose as the authoring source and clear the attachment override when migration is complete.");

            if (!string.IsNullOrWhiteSpace(attachment.TraceEndSocketId) &&
                trace.EndPose != null &&
                trace.EndPose.ParentKind == CharacterPoseParentKind.Socket &&
                !string.IsNullOrWhiteSpace(trace.EndPose.ParentPath) &&
                !string.Equals(attachment.TraceEndSocketId, trace.EndPose.ParentPath, System.StringComparison.Ordinal))
                Add(report, CharacterAuthoringValidationSeverity.Warning, CharacterAuthoringValidationGate.WarningOnly,
                    CharacterAuthoringValidationCodes.LegacyTraceOverrideConflict, "geometry/weapon_attachments.json", attachmentObjectPath, "traceEndSocketId",
                    "legacy attachment traceEndSocketId differs from geometry trace endPose socket.", "Prefer geometry/traces endPose as the authoring source and clear the attachment override when migration is complete.");
        }

        private static string TraceObjectPath(WeaponTraceProfile trace)
        {
            return "geometry/traces/" + (!string.IsNullOrWhiteSpace(trace != null ? trace.TraceId : string.Empty) ? trace.TraceId : "unknown");
        }

        private static void ValidateBodyPartContactPoints(
            CharacterAuthoringValidationReport report,
            CharacterAuthoringGeometry geometry,
            HashSet<string> partIds,
            HashSet<string> socketIds)
        {
            if (geometry == null || geometry.BodyPartContactPoints == null)
                return;

            var stableIds = new HashSet<string>();
            var contactPointIds = new HashSet<int>();
            for (int i = 0; i < geometry.BodyPartContactPoints.Count; i++)
            {
                CharacterBodyPartContactPointProfile contactPoint = geometry.BodyPartContactPoints[i];
                if (contactPoint == null)
                    continue;

                string objectPath = BodyPartContactPointObjectPath(contactPoint, i);
                if (string.IsNullOrWhiteSpace(contactPoint.StableId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.MissingBodyPartContactPointStableId, "geometry/body_part_contact_points.json", objectPath, "stableId",
                        "body part contact point stableId is required.", "Set a long-term stable id such as contact.left_foot.sole.");
                }
                else if (!stableIds.Add(contactPoint.StableId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.DuplicateBodyPartContactPointStableId, "geometry/body_part_contact_points.json", objectPath, "stableId",
                        "body part contact point stableId must be unique.", "Rename or merge duplicate body part contact points.");
                }

                if (contactPoint.ContactPointId <= 0)
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.InvalidBodyPartContactPointId, "geometry/body_part_contact_points.json", objectPath, "contactPointId",
                        "contactPointId must be a positive stable numeric id.", "Assign a stable positive id that does not change between package versions.");
                else if (!contactPointIds.Add(contactPoint.ContactPointId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.DuplicateBodyPartContactPointId, "geometry/body_part_contact_points.json", objectPath, "contactPointId",
                        "contactPointId must be unique within one geometry profile.", "Rename or merge duplicate body part contact points.");

                if (contactPoint.BodyPartId <= 0)
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.InvalidBodyPartContactPointId, "geometry/body_part_contact_points.json", objectPath, "bodyPartId",
                        "bodyPartId must be a positive stable numeric id.", "Assign the body part id used by presentation audio rules.");

                if (string.IsNullOrWhiteSpace(contactPoint.PartId) || partIds == null || !partIds.Contains(contactPoint.PartId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingBodyPartContactPointPart, "geometry/body_part_contact_points.json", objectPath, "partId",
                        "body part contact point partId does not exist in body parts.", "Use an existing body part id or add the missing body part.");

                if (contactPoint.RadiusMeters <= 0f)
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.InvalidBodyPartContactPointRadius, "geometry/body_part_contact_points.json", objectPath, "radiusMeters",
                        "body part contact point radiusMeters must be positive.", "Use a positive sphere radius, for example 0.09.");

                if (!System.Enum.IsDefined(typeof(CharacterBodyPartContactActivationMode), contactPoint.ActivationMode))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.InvalidBodyPartContactActivationMode, "geometry/body_part_contact_points.json", objectPath, "activationMode",
                        "activationMode is not a supported body part contact activation mode.", "Use Disabled, Always, ActionWindow, ExplicitState or MotionState.");

                if (!System.Enum.IsDefined(typeof(CharacterBodyPartContactMotionMode), contactPoint.MotionMode))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.InvalidBodyPartContactMotionMode, "geometry/body_part_contact_points.json", objectPath, "motionMode",
                        "motionMode is not a supported body part contact motion mode.", "Use Static or BakedSocket.");

                if (contactPoint.MotionMode == CharacterBodyPartContactMotionMode.BakedSocket
                    && string.IsNullOrWhiteSpace(contactPoint.MotionSocketId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingBodyPartContactMotionSocket, "geometry/body_part_contact_points.json", objectPath, "motionSocketId",
                        "BakedSocket contact points require a socket id.", "Bind the contact point to an exported socket such as leftFootContact or use Static mode.");
                }
                else if (contactPoint.MotionMode == CharacterBodyPartContactMotionMode.BakedSocket
                    && socketIds != null
                    && !socketIds.Contains(contactPoint.MotionSocketId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingBodyPartContactMotionSocket, "geometry/body_part_contact_points.json", objectPath, "motionSocketId",
                        "BakedSocket contact point references a missing socket id.", "Use an existing socket id from geometry/sockets.json or switch to Static mode.");
                }
            }
        }

        private static string BodyPartContactPointObjectPath(CharacterBodyPartContactPointProfile contactPoint, int index)
        {
            if (contactPoint != null && !string.IsNullOrWhiteSpace(contactPoint.StableId))
                return "geometry/bodyPartContactPoints/" + contactPoint.StableId;

            return "geometry/bodyPartContactPoints/" + index.ToString();
        }

        private static void ValidateWeaponTraceProximalRange(
            CharacterAuthoringValidationReport report,
            WeaponTraceProfile trace,
            string objectPath)
        {
            if (trace == null)
                return;

            if (trace.ProximalExtension < 0f)
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                    CharacterAuthoringValidationCodes.InvalidWeaponTraceProximalRange, "geometry/traces.json", objectPath, "proximalExtension",
                    "weapon trace proximalExtension must be zero or positive.", "Use 0 to disable proximal trace extension, or set a positive meter length.");

            if (trace.ProximalRadius < 0f)
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                    CharacterAuthoringValidationCodes.InvalidWeaponTraceProximalRange, "geometry/traces.json", objectPath, "proximalRadius",
                    "weapon trace proximalRadius must be zero or positive.", "Use 0 to disable proximal trace extension, or set a positive meter radius.");

            bool hasExtension = trace.ProximalExtension > 0f;
            bool hasRadius = trace.ProximalRadius > 0f;
            if (hasExtension == hasRadius)
                return;

            string field = hasExtension ? "proximalRadius" : "proximalExtension";
            Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                CharacterAuthoringValidationCodes.InvalidWeaponTraceProximalRange, "geometry/traces.json", objectPath, field,
                "weapon trace proximalExtension and proximalRadius must be enabled together.", "Set both values positive, or set both to 0 to disable proximal trace extension.");
        }

        private static void ValidateColliderDimensions(CharacterAuthoringValidationReport report, CharacterBodyColliderProfile collider, string objectPath)
        {
            if (collider == null)
                return;

            bool invalid = false;
            string field = "radius";
            if (collider.Shape == CharacterColliderShape.Sphere)
            {
                invalid = collider.Radius <= 0f;
            }
            else if (collider.Shape == CharacterColliderShape.Capsule)
            {
                invalid = collider.Radius <= 0f || collider.Height <= 0f;
                field = collider.Radius <= 0f ? "radius" : "height";
            }
            else if (collider.Shape == CharacterColliderShape.Box)
            {
                CharacterAuthoringVector3 size = collider.Size;
                invalid = size == null || size.X <= 0f || size.Y <= 0f || size.Z <= 0f;
                field = "size";
            }

            if (invalid)
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                    CharacterAuthoringValidationCodes.InvalidColliderDimensions, "geometry/body_colliders.json", objectPath, field,
                    "collider dimensions must be positive for the selected shape.", "Set radius/height/size to positive meter values.");
        }

        private static void Add(
            CharacterAuthoringValidationReport report,
            CharacterAuthoringValidationSeverity severity,
            CharacterAuthoringValidationGate gate,
            string code,
            string sourcePath,
            string sourceObjectPath,
            string field,
            string message,
            string suggestedFix)
        {
            report.Issues.Add(new CharacterAuthoringValidationIssue
            {
                Severity = severity,
                Gate = gate,
                Code = code ?? string.Empty,
                SourcePath = sourcePath ?? string.Empty,
                SourceObjectPath = sourceObjectPath ?? string.Empty,
                Field = field ?? string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            });
        }
    }
}
