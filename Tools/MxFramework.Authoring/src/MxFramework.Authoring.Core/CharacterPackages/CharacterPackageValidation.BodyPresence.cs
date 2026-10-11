using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public static partial class CharacterAuthoringValidationCodes
    {
        public const string BodyPresenceUnsupportedSchema = "CHARPRES001_UNSUPPORTED_SCHEMA";
        public const string BodyPresenceReadOnlyDocument = "CHARPRES002_READ_ONLY_DOCUMENT";
        public const string BodyPresenceInvalidRevision = "CHARPRES003_INVALID_REVISION";
        public const string BodyPresenceTooManyVolumes = "CHARPRES004_TOO_MANY_VOLUMES";
        public const string BodyPresenceInvalidVolumeId = "CHARPRES005_INVALID_VOLUME_ID";
        public const string BodyPresenceDuplicateVolumeId = "CHARPRES006_DUPLICATE_VOLUME_ID";
        public const string BodyPresenceInvalidReferenceFrame = "CHARPRES007_INVALID_REFERENCE_FRAME";
        public const string BodyPresenceInvalidShape = "CHARPRES008_INVALID_SHAPE";
        public const string BodyPresenceInvalidPose = "CHARPRES009_INVALID_POSE";
        public const string BodyPresenceInvalidDimensions = "CHARPRES010_INVALID_DIMENSIONS";
        public const string BodyPresenceInvalidSkin = "CHARPRES011_INVALID_SKIN";
        public const string BodyPresenceInvalidPressureScale = "CHARPRES012_INVALID_PRESSURE_SCALE";
        public const string RootCapsuleCenterXZNotConsumed = "CHARPRES013_ROOT_CAPSULE_CENTER_XZ_NOT_CONSUMED";
        public const string BodyPresenceTooManyAnimatedSources = "CHARPRES014_TOO_MANY_ANIMATED_SOURCES";
        public const string BodyPresenceInvalidAnimatedSourceId = "CHARPRES015_INVALID_ANIMATED_SOURCE_ID";
        public const string BodyPresenceDuplicateAnimatedSourceId = "CHARPRES016_DUPLICATE_ANIMATED_SOURCE_ID";
        public const string BodyPresenceDuplicateAnimatedCollider = "CHARPRES017_DUPLICATE_ANIMATED_COLLIDER";
        public const string BodyPresenceMissingAnimatedCollider = "CHARPRES018_MISSING_ANIMATED_COLLIDER";
        public const string BodyPresenceUnsupportedAnimatedCollider = "CHARPRES019_UNSUPPORTED_ANIMATED_COLLIDER";
        public const string BodyPresenceInvalidAnimatedSourcePolicy = "CHARPRES020_INVALID_ANIMATED_SOURCE_POLICY";
    }

    public static partial class CharacterResourcePackageValidator
    {
        private const string BodyPresenceSourcePath = "geometry/body_presence.json";

        private static void ValidateBodyPresence(
            CharacterAuthoringValidationReport report,
            CharacterAuthoringGeometry geometry)
        {
            if (geometry.BodyPresenceDocumentState == CharacterBodyPresenceDocumentState.Missing)
                return;

            if (geometry.BodyPresenceDocumentState == CharacterBodyPresenceDocumentState.UnsupportedSchema)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.BodyPresenceUnsupportedSchema, BodyPresenceSourcePath,
                    "bodyPresence", "schemaVersion",
                    "Body Presence format or schemaVersion is unsupported and the original payload is read-only.",
                    "Use mx.characterBodyPresence.v1 schema 1.0, mx.characterBodyPresence.v2 schema 2.0, or an explicit supported migrator.");
                return;
            }

            CharacterBodyPresenceDocument document = geometry.BodyPresence;
            if (geometry.BodyPresenceDocumentState == CharacterBodyPresenceDocumentState.Invalid || document == null)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.BodyPresenceReadOnlyDocument, BodyPresenceSourcePath,
                    "bodyPresence", string.Empty,
                    "Body Presence could not be read without data loss and is write-ineligible.",
                    "Repair the JSON document before saving or compiling the package.");
                return;
            }

            if (!CharacterBodyPresenceDocumentCodec.IsSupportedSchema(document.Format, document.SchemaVersion))
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.BodyPresenceUnsupportedSchema, BodyPresenceSourcePath,
                    "bodyPresence", "format/schemaVersion",
                    "Body Presence requires a supported v1/1.0 or v2/2.0 pair.",
                    "Use the current format without changing authored volume identities.");
            }

            if (document.Revision < 1)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.BodyPresenceInvalidRevision, BodyPresenceSourcePath,
                    "bodyPresence", "revision", "Body Presence revision must be at least 1.",
                    "Use revision 1 or a later positive revision.");
            }

            List<CharacterBodyPresenceVolumeAuthoring> volumes =
                document.Volumes ?? new List<CharacterBodyPresenceVolumeAuthoring>();
            if (volumes.Count > CharacterBodyPresenceLimits.MaximumVolumeCount)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.BodyPresenceTooManyVolumes, BodyPresenceSourcePath,
                    "bodyPresence/volumes", "volumes",
                    "Body Presence supports at most 4 authored volumes; disabled entries still count.",
                    "Remove or merge volumes until the total count is 4 or fewer.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < volumes.Count; i++)
            {
                CharacterBodyPresenceVolumeAuthoring volume = volumes[i];
                string volumeId = volume?.VolumeId ?? string.Empty;
                string objectPath = "bodyPresence/volumes/"
                    + (!string.IsNullOrWhiteSpace(volumeId) ? volumeId : i.ToString(CultureInfo.InvariantCulture));
                if (!IsStableBodyPresenceId(volumeId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceInvalidVolumeId, BodyPresenceSourcePath,
                        objectPath, "volumeId",
                        "Body Presence volumeId must be a stable package-local id.",
                        "Use lowercase letters, digits, '.', '_' or '-'.");
                }
                else if (!ids.Add(volumeId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceDuplicateVolumeId, BodyPresenceSourcePath,
                        objectPath, "volumeId", "Body Presence volumeId must be unique.",
                        "Rename or merge the duplicate volume.");
                }

                if (volume == null)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceInvalidPose, BodyPresenceSourcePath,
                        objectPath, "volume", "Body Presence volume cannot be null.",
                        "Remove the null entry or author a complete volume.");
                    continue;
                }

                if (volume.ReferenceFrame != CharacterBodyPresenceReferenceFrame.ModelRoot)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceInvalidReferenceFrame, BodyPresenceSourcePath,
                        objectPath, "referenceFrame", "Body Presence v1 only supports ModelRoot.",
                        "Use ModelRoot; animated bones and world space are not valid Presence frames.");
                }

                bool supportedShape = volume.Shape == CharacterBodyPresenceShape.Sphere
                    || volume.Shape == CharacterBodyPresenceShape.Capsule
                    || volume.Shape == CharacterBodyPresenceShape.Box;
                if (!supportedShape)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceInvalidShape, BodyPresenceSourcePath,
                        objectPath, "shape", "Body Presence v1 only supports Sphere, Capsule and Box.",
                        "Choose one of the supported shapes.");
                }

                if (!IsValidPresencePose(volume.LocalPose))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceInvalidPose, BodyPresenceSourcePath,
                        objectPath, "localPose",
                        "Body Presence local pose must be finite, within the supported local range, use a unit quaternion, and have unit scale.",
                        "Use a ModelRoot-local finite pose with scale (1,1,1).");
                }

                if (!AreValidPresenceDimensions(volume))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceInvalidDimensions, BodyPresenceSourcePath,
                        objectPath, "radius/height/size",
                        "Body Presence dimensions must be finite, bounded, positive for the selected shape, and capsule height must include both caps.",
                        "Set dimensions in meters and keep capsule height at least twice its radius.");
                }

                if (volume.SkinMillimeters < 0
                    || volume.SkinMillimeters > CharacterBodyPresenceLimits.MaximumSkinMillimeters)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceInvalidSkin, BodyPresenceSourcePath,
                        objectPath, "skinMillimeters", "Body Presence skinMillimeters is outside [0,10000].",
                        "Use a non-negative skin no larger than 10000 millimeters.");
                }

                if (volume.PressureScaleMilli < 0
                    || volume.PressureScaleMilli > CharacterBodyPresenceLimits.MaximumPressureScaleMilli)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceInvalidPressureScale, BodyPresenceSourcePath,
                        objectPath, "pressureScaleMilli",
                        "Body Presence pressureScaleMilli is outside [0,100000].",
                        "Use 0 to disable pressure or a bounded fixed-point scale.");
                }
            }

            ValidateAnimatedHurtboxSources(report, geometry, document);
        }

        private static void ValidateAnimatedHurtboxSources(
            CharacterAuthoringValidationReport report,
            CharacterAuthoringGeometry geometry,
            CharacterBodyPresenceDocument document)
        {
            if (!document.IsV2)
                return;
            List<CharacterBodyPresenceAnimatedHurtboxSourceAuthoring> sources =
                document.AnimatedHurtboxSources ?? new List<CharacterBodyPresenceAnimatedHurtboxSourceAuthoring>();
            if (sources.Count > CharacterBodyPresenceLimits.MaximumAnimatedHurtboxSourceCount)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.BodyPresenceTooManyAnimatedSources, BodyPresenceSourcePath,
                    "bodyPresence/animatedHurtboxSources", "animatedHurtboxSources",
                    "Body Presence v2 supports at most 4 animated Hurtbox sources; disabled entries still count.",
                    "Remove sources until the total count is 4 or fewer.");
            }
            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            var colliderIds = new HashSet<string>(StringComparer.Ordinal);
            var colliders = new Dictionary<string, CharacterBodyColliderProfile>(StringComparer.Ordinal);
            for (int i = 0; i < (geometry.Colliders?.Count ?? 0); i++)
            {
                CharacterBodyColliderProfile collider = geometry.Colliders[i];
                if (collider != null && !string.IsNullOrWhiteSpace(collider.ColliderId) && !colliders.ContainsKey(collider.ColliderId))
                    colliders.Add(collider.ColliderId, collider);
            }
            for (int i = 0; i < sources.Count; i++)
            {
                CharacterBodyPresenceAnimatedHurtboxSourceAuthoring source = sources[i];
                string sourceId = source?.SourceId ?? string.Empty;
                string colliderId = source?.ColliderId ?? string.Empty;
                string objectPath = "bodyPresence/animatedHurtboxSources/" +
                    (!string.IsNullOrWhiteSpace(sourceId) ? sourceId : i.ToString(CultureInfo.InvariantCulture));
                if (!IsStableBodyPresenceId(sourceId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceInvalidAnimatedSourceId, BodyPresenceSourcePath,
                        objectPath, "sourceId", "Animated Hurtbox sourceId must be a stable package-local id.",
                        "Use lowercase letters, digits, '.', '_' or '-'.");
                else if (!sourceIds.Add(sourceId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceDuplicateAnimatedSourceId, BodyPresenceSourcePath,
                        objectPath, "sourceId", "Animated Hurtbox sourceId must be unique.", "Rename the duplicate source.");
                if (string.IsNullOrWhiteSpace(colliderId) || !colliderIds.Add(colliderId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceDuplicateAnimatedCollider, BodyPresenceSourcePath,
                        objectPath, "colliderId", "Animated Hurtbox colliderId must be non-empty and unique.",
                        "Select one stable CharacterHurtbox collider per source.");
                if (!colliders.TryGetValue(colliderId, out CharacterBodyColliderProfile collider))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceMissingAnimatedCollider, BodyPresenceSourcePath,
                        objectPath, "colliderId", "Animated Hurtbox source references a missing collider.",
                        "Select a collider from geometry/body_colliders.json.");
                else if (!string.Equals(collider.PhysicsLayer, "CharacterHurtbox", StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(collider.PartId)
                    || (collider.Shape != CharacterColliderShape.Sphere
                        && collider.Shape != CharacterColliderShape.Capsule
                        && collider.Shape != CharacterColliderShape.Box))
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceUnsupportedAnimatedCollider, BodyPresenceSourcePath,
                        objectPath, "colliderId", "Animated source must resolve to a sphere, capsule, or OBB CharacterHurtbox with a stable PartId.",
                        "Choose a supported baked CharacterHurtbox collider.");
                if (source == null || source.SkinMillimeters < 0
                    || source.SkinMillimeters > CharacterBodyPresenceLimits.MaximumSkinMillimeters
                    || source.PressureScaleMilli < 0
                    || source.PressureScaleMilli > CharacterBodyPresenceLimits.MaximumPressureScaleMilli)
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPresenceInvalidAnimatedSourcePolicy, BodyPresenceSourcePath,
                        objectPath, "skinMillimeters/pressureScaleMilli", "Animated source policy is outside the frozen bounds.",
                        "Use skin 0..10000 mm and pressure scale 0..100000 milli.");
            }
        }

        private static void ValidateRootCapsuleCenterContract(
            CharacterAuthoringValidationReport report,
            CharacterAuthoringGeometry geometry)
        {
            CharacterAuthoringVector3 center = geometry?.BodyProfile?.DefaultCapsule?.Center;
            if (center == null || (center.X == 0f && center.Z == 0f))
                return;
            Add(report, CharacterAuthoringValidationSeverity.Warning, CharacterAuthoringValidationGate.WarningOnly,
                CharacterAuthoringValidationCodes.RootCapsuleCenterXZNotConsumed,
                "geometry/body_geometry.json", "geometry/bodyProfile/defaultCapsule", "center.x/center.z",
                "defaultCapsule.center source X=" + center.X.ToString("R", CultureInfo.InvariantCulture)
                    + ", Z=" + center.Z.ToString("R", CultureInfo.InvariantCulture)
                    + "; current Runtime motor consumes center.y only and does not consume source X/Z.",
                "Keep the authored values for diagnosis; M0 does not change the root motor contract.");
        }

        private static bool IsStableBodyPresenceId(string value)
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

        private static bool IsValidPresencePose(CharacterBodyPresenceLocalPose pose)
        {
            if (pose?.Position == null || pose.Rotation == null || pose.Scale == null)
                return false;
            CharacterAuthoringVector3 position = pose.Position;
            if (!IsFinite(position.X) || !IsFinite(position.Y) || !IsFinite(position.Z)
                || Math.Abs(position.X) > CharacterBodyPresenceLimits.MaximumLocalCoordinateMeters
                || Math.Abs(position.Y) > CharacterBodyPresenceLimits.MaximumLocalCoordinateMeters
                || Math.Abs(position.Z) > CharacterBodyPresenceLimits.MaximumLocalCoordinateMeters)
                return false;
            CharacterAuthoringQuaternion rotation = pose.Rotation;
            if (!IsFinite(rotation.X) || !IsFinite(rotation.Y)
                || !IsFinite(rotation.Z) || !IsFinite(rotation.W))
                return false;
            double normSquared = (double)rotation.X * rotation.X + (double)rotation.Y * rotation.Y
                + (double)rotation.Z * rotation.Z + (double)rotation.W * rotation.W;
            if (Math.Abs(normSquared - 1d) > 0.001d)
                return false;
            CharacterAuthoringVector3 scale = pose.Scale;
            return IsFinite(scale.X) && IsFinite(scale.Y) && IsFinite(scale.Z)
                && Math.Abs(scale.X - 1f) <= 0.0001f
                && Math.Abs(scale.Y - 1f) <= 0.0001f
                && Math.Abs(scale.Z - 1f) <= 0.0001f;
        }

        private static bool AreValidPresenceDimensions(CharacterBodyPresenceVolumeAuthoring volume)
        {
            CharacterAuthoringVector3 size = volume.Size;
            if (!IsBoundedNonNegative(volume.Radius) || !IsBoundedNonNegative(volume.Height)
                || size == null || !IsBoundedNonNegative(size.X)
                || !IsBoundedNonNegative(size.Y) || !IsBoundedNonNegative(size.Z))
                return false;
            return volume.Shape switch
            {
                CharacterBodyPresenceShape.Sphere => volume.Radius > 0f,
                CharacterBodyPresenceShape.Capsule => volume.Radius > 0f
                    && volume.Height >= 2f * volume.Radius,
                CharacterBodyPresenceShape.Box => size.X > 0f && size.Y > 0f && size.Z > 0f,
                _ => false
            };
        }

        private static bool IsBoundedNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f
                && value <= CharacterBodyPresenceLimits.MaximumDimensionMeters;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
