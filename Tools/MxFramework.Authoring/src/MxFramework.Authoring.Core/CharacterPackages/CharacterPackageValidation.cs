using System.Collections.Generic;
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
        public const string DuplicateResourceKey = "CHARPKG_DUPLICATE_RESOURCE_KEY";
        public const string MissingResourcePath = "CHARPKG_MISSING_RESOURCE_PATH";
        public const string MissingGeometry = "CHARPKG_MISSING_GEOMETRY";
        public const string MissingBodyProfile = "CHARPKG_MISSING_BODY_PROFILE";
        public const string MissingBodyPart = "CHARPKG_MISSING_BODY_PART";
        public const string DuplicateBodyPart = "CHARPKG_DUPLICATE_BODY_PART";
        public const string MissingColliderPart = "CHARPKG_MISSING_COLLIDER_PART";
        public const string MissingColliderHitZone = "CHARPKG_MISSING_COLLIDER_HIT_ZONE";
        public const string UnsupportedColliderShape = "CHARPKG_UNSUPPORTED_COLLIDER_SHAPE";
        public const string MissingSocketPart = "CHARPKG_MISSING_SOCKET_PART";
        public const string MissingAttachmentSocket = "CHARPKG_MISSING_ATTACHMENT_SOCKET";
        public const string MissingAttachmentTrace = "CHARPKG_MISSING_ATTACHMENT_TRACE";
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
            var report = new CharacterAuthoringValidationReport();
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
            ValidateResourceCatalog(report, package.ResourceCatalog);
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

        private static void ValidateResourceCatalog(CharacterAuthoringValidationReport report, CharacterPackageResourceCatalog catalog)
        {
            if (catalog == null)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.MissingResourceCatalog, "resource_catalog.json", "", "",
                    "resource_catalog.json is missing.", "Create a package-local resource catalog.");
                return;
            }

            var keys = new HashSet<string>();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                string objectPath = "resourceCatalog/entries/" + i;
                if (entry == null)
                    continue;

                if (string.IsNullOrWhiteSpace(entry.ResourceKey))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.MissingResourceKey, "resource_catalog.json", objectPath, "resourceKey",
                        "resourceKey is required.", "Use a package-local stable ResourceKey.");
                    continue;
                }

                if (!keys.Add(entry.ResourceKey))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.DuplicateResourceKey, "resource_catalog.json", objectPath, "resourceKey",
                        "resourceKey must be unique within one character package.", "Rename or remove the duplicate resource entry.");

                if (string.IsNullOrWhiteSpace(entry.RelativePath))
                    Add(report, CharacterAuthoringValidationSeverity.Warning, CharacterAuthoringValidationGate.WarningOnly,
                        CharacterAuthoringValidationCodes.MissingResourcePath, "resource_catalog.json", objectPath, "relativePath",
                        "relativePath is empty; #223 will define stricter resource file validation.", "Set a package-relative path when the resource exists in this package.");
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
            for (int i = 0; i < geometry.BodyParts.Count; i++)
            {
                CharacterBodyPartAuthoring part = geometry.BodyParts[i];
                if (part == null)
                    continue;
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
                if (!string.IsNullOrWhiteSpace(socket.SocketId))
                    socketIds.Add(socket.SocketId);
                if (!string.IsNullOrWhiteSpace(socket.ParentPartId) && !partIds.Contains(socket.ParentPartId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.SpawnBlocked,
                        CharacterAuthoringValidationCodes.MissingSocketPart, "geometry/sockets.json", "geometry/sockets/" + socket.SocketId, "parentPartId",
                        "socket parentPartId does not exist in body parts.", "Use an existing body part id or add the missing body part.");
            }

            for (int i = 0; i < geometry.Colliders.Count; i++)
            {
                CharacterBodyColliderProfile collider = geometry.Colliders[i];
                if (collider == null)
                    continue;

                string objectPath = "geometry/colliders/" + (!string.IsNullOrWhiteSpace(collider.ColliderId) ? collider.ColliderId : i.ToString());
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
            }
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
