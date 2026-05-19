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

    public static class CharacterAuthoringValidationCodes
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
        public const string MissingSocketId = "CHARPKG_MISSING_SOCKET_ID";
        public const string DuplicateSocket = "CHARPKG_DUPLICATE_SOCKET";
        public const string MissingSocketPart = "CHARPKG_MISSING_SOCKET_PART";
        public const string MissingSocketLocator = "CHARPKG_MISSING_SOCKET_LOCATOR";
        public const string MissingMirrorSocket = "CHARPKG_MISSING_MIRROR_SOCKET";
        public const string SelfMirrorSocket = "CHARPKG_SELF_MIRROR_SOCKET";
        public const string MissingAttachmentSocket = "CHARPKG_MISSING_ATTACHMENT_SOCKET";
        public const string MissingAttachmentTraceSocket = "CHARPKG_MISSING_ATTACHMENT_TRACE_SOCKET";
        public const string MissingAttachmentTrace = "CHARPKG_MISSING_ATTACHMENT_TRACE";
        public const string InvalidAttachmentTraceRadius = "CHARPKG_INVALID_ATTACHMENT_TRACE_RADIUS";
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

    public static class CharacterResourcePackageValidator
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
            ValidateGeometry(report, package.Geometry);
            return report;
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
                    "packageId is required.", "Set packageId, for example iron_vanguard.");

            if (string.IsNullOrWhiteSpace(manifest.StableId))
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.MissingStableId, "manifest.json", "manifest", "stableId",
                    "stableId is required.", "Set a long-term stable id, for example charpkg.iron_vanguard.");

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
                    "resource source format is not supported by the v1 character package contract.", "Use glTF/GLB for models and animation, png/jpg/tga for textures/previews, json for config/material/vfx descriptors, or wav/ogg for audio.");
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

        private static void ValidateGeometry(CharacterAuthoringValidationReport report, CharacterAuthoringGeometry geometry)
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

            var traceIds = new HashSet<string>();
            for (int i = 0; i < geometry.Traces.Count; i++)
            {
                WeaponTraceProfile trace = geometry.Traces[i];
                if (trace != null && !string.IsNullOrWhiteSpace(trace.TraceId))
                    traceIds.Add(trace.TraceId);
            }

            var socketIds = new HashSet<string>();
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

                if (!string.IsNullOrWhiteSpace(socket.ParentPartId) && !partIds.Contains(socket.ParentPartId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingSocketPart, "geometry/sockets.json", "geometry/sockets/" + socket.SocketId, "parentPartId",
                        "socket parentPartId does not exist in body parts.", "Use an existing body part id or add the missing body part.");

                if (string.IsNullOrWhiteSpace(socket.ParentPartId) &&
                    string.IsNullOrWhiteSpace(socket.BonePath) &&
                    string.IsNullOrWhiteSpace(socket.LocatorPath))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingSocketLocator, "geometry/sockets.json", objectPath, "bonePath",
                        "socket must bind to a parent body part, bone path or locator path.", "Set parentPartId, bonePath or locatorPath.");
            }

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

                ValidateColliderDimensions(report, collider, objectPath);
            }

            for (int i = 0; i < geometry.WeaponAttachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = geometry.WeaponAttachments[i];
                if (attachment == null)
                    continue;

                string objectPath = "geometry/weaponAttachments/" + (!string.IsNullOrWhiteSpace(attachment.WeaponId) ? attachment.WeaponId : i.ToString());
                if (string.IsNullOrWhiteSpace(attachment.AttachSocketId) || !socketIds.Contains(attachment.AttachSocketId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingAttachmentSocket, "geometry/weapon_attachments.json", objectPath, "attachSocketId",
                        "weapon attachment references a missing socket.", "Use an existing socket id.");

                if (!string.IsNullOrWhiteSpace(attachment.TraceId) && !traceIds.Contains(attachment.TraceId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingAttachmentTrace, "geometry/weapon_attachments.json", objectPath, "traceId",
                        "weapon attachment references a missing trace.", "Create the trace or clear traceId.");

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
