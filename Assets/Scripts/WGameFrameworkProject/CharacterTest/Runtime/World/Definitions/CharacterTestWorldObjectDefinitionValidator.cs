using System;
using System.Collections.Generic;
using MxFramework.Combat.Physics;
using MxFramework.Config.Levels;
using MxFramework.Resources;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestWorldObjectDefinitionValidationIssue
    {
        public CharacterTestWorldObjectDefinitionValidationIssue(string code, string path, string message)
        {
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string Path { get; }
        public string Message { get; }
    }

    public sealed class CharacterTestWorldObjectDefinitionValidationReport
    {
        private readonly List<CharacterTestWorldObjectDefinitionValidationIssue> _issues =
            new List<CharacterTestWorldObjectDefinitionValidationIssue>();

        public IReadOnlyList<CharacterTestWorldObjectDefinitionValidationIssue> Issues => _issues;
        public bool Success => _issues.Count == 0;

        internal void Add(string code, string path, string message)
        {
            _issues.Add(new CharacterTestWorldObjectDefinitionValidationIssue(code, path, message));
        }
    }

    public static class CharacterTestWorldObjectDefinitionValidator
    {
        /// <remarks>AllocByDesign: authoring/startup validation builds structured diagnostics.</remarks>
        public static CharacterTestWorldObjectDefinitionValidationReport Validate(
            CharacterTestWorldObjectDefinitionAuthoringDocument document)
        {
            var report = new CharacterTestWorldObjectDefinitionValidationReport();
            if (document == null)
            {
                report.Add("WOD_DOCUMENT_NULL", "$", "World object definition document is null.");
                return report;
            }

            if (!string.Equals(
                    document.Format,
                    CharacterTestWorldObjectDefinitionFormats.AuthoringV1,
                    StringComparison.Ordinal))
            {
                report.Add("WOD_FORMAT_INVALID", "$.format", "Authoring format is not supported.");
            }

            ValidateSchemaVersion(document.SchemaVersion, report);
            ValidateDefinitions(document.Definitions, report);
            return report;
        }

        internal static CharacterTestWorldObjectDefinitionValidationReport ValidateRuntime(
            CharacterTestWorldObjectDefinitionRuntimeDocument document)
        {
            var report = new CharacterTestWorldObjectDefinitionValidationReport();
            if (document == null)
            {
                report.Add("WOD_DOCUMENT_NULL", "$", "World object runtime document is null.");
                return report;
            }

            if (!string.Equals(
                    document.Format,
                    CharacterTestWorldObjectDefinitionFormats.RuntimeV1,
                    StringComparison.Ordinal))
            {
                report.Add("WOD_FORMAT_INVALID", "$.format", "Runtime format is not supported.");
            }

            ValidateSchemaVersion(document.SchemaVersion, report);
            ValidateDefinitions(document.Definitions, report);
            if (string.IsNullOrWhiteSpace(document.SourceHash))
                report.Add("WOD_SOURCE_HASH_MISSING", "$.sourceHash", "Source hash is required.");
            if (string.IsNullOrWhiteSpace(document.ContentHash))
                report.Add("WOD_CONTENT_HASH_MISSING", "$.contentHash", "Content hash is required.");
            return report;
        }

        private static void ValidateSchemaVersion(
            int schemaVersion,
            CharacterTestWorldObjectDefinitionValidationReport report)
        {
            if (schemaVersion != CharacterTestWorldObjectDefinitionFormats.SchemaVersion)
                report.Add("WOD_SCHEMA_VERSION_INVALID", "$.schemaVersion", "Schema version must be 1.");
        }

        private static void ValidateDefinitions(
            IReadOnlyList<CharacterTestWorldObjectDefinitionAuthoringEntry> definitions,
            CharacterTestWorldObjectDefinitionValidationReport report)
        {
            if (definitions == null)
            {
                report.Add("WOD_DEFINITIONS_NULL", "$.definitions", "Definitions cannot be null.");
                return;
            }

            var ids = new HashSet<int>();
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definitions.Count; i++)
            {
                string path = "$.definitions[" + i + "]";
                CharacterTestWorldObjectDefinitionAuthoringEntry entry = definitions[i];
                if (entry == null)
                {
                    report.Add("WOD_DEFINITION_NULL", path, "Definition cannot be null.");
                    continue;
                }

                if (entry.DefinitionId <= 0)
                    report.Add("WOD_ID_INVALID", path + ".definitionId", "Definition id must be positive.");
                else if (!ids.Add(entry.DefinitionId))
                    report.Add("WOD_ID_DUPLICATE", path + ".definitionId", "Definition id is duplicated.");

                if (string.IsNullOrWhiteSpace(entry.StableId))
                    report.Add("WOD_STABLE_ID_INVALID", path + ".stableId", "Stable id is required.");
                else if (!ResourceKey.IsValidId(entry.StableId))
                    report.Add("WOD_STABLE_ID_INVALID", path + ".stableId", "Stable id must use ResourceKey id syntax.");
                else if (!stableIds.Add(entry.StableId))
                    report.Add("WOD_STABLE_ID_DUPLICATE", path + ".stableId", "Stable id is duplicated.");

                if (string.IsNullOrWhiteSpace(entry.DisplayName))
                    report.Add("WOD_DISPLAY_NAME_MISSING", path + ".displayName", "Display name is required.");
                if (string.IsNullOrWhiteSpace(entry.Category))
                    report.Add("WOD_CATEGORY_MISSING", path + ".category", "Category is required.");

                if (!TryParse(entry.ObjectKind, out CharacterTestWorldObjectKind objectKind)
                    || objectKind == CharacterTestWorldObjectKind.Unknown)
                {
                    report.Add("WOD_OBJECT_KIND_INVALID", path + ".objectKind", "Object kind is invalid.");
                }

                ValidatePresentation(entry.Presentation, path + ".presentation", report);
                ValidateMaterial(entry.Material, path + ".material", report);
                ValidateShape(entry.Shape, path + ".shape", report);
                ValidatePotency(entry.Potency, path + ".potency", report);
                ValidateFacets(entry, objectKind, path, report);
                ValidateTags(entry.Tags, path + ".tags", report);
            }
        }

        private static void ValidatePresentation(
            CharacterTestWorldObjectPresentationAuthoring presentation,
            string path,
            CharacterTestWorldObjectDefinitionValidationReport report)
        {
            if (presentation == null)
            {
                report.Add("WOD_PRESENTATION_MISSING", path, "Presentation is required.");
                return;
            }

            if (!TryParse(presentation.Kind, out CharacterTestWorldObjectPresentationKind kind)
                || kind == CharacterTestWorldObjectPresentationKind.Unknown)
            {
                report.Add("WOD_PRESENTATION_KIND_INVALID", path + ".kind", "Presentation kind is invalid.");
                return;
            }

            if (kind == CharacterTestWorldObjectPresentationKind.PrefabResource)
            {
                ResourceKey key = presentation.ResourceKey?.ToResourceKey() ?? default;
                if (!key.IsValid || !string.Equals(key.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal))
                {
                    report.Add(
                        "WOD_RESOURCE_KEY_INVALID",
                        path + ".resourceKey",
                        "Prefab presentation requires a valid GameObject ResourceKey.");
                }

                return;
            }

            if (!TryParse(presentation.PrimitiveKind, out CharacterTestWorldObjectPrimitiveKind primitive)
                || primitive == CharacterTestWorldObjectPrimitiveKind.Unknown)
            {
                report.Add(
                    "WOD_PRIMITIVE_KIND_INVALID",
                    path + ".primitiveKind",
                    "Primitive presentation requires Cube, Sphere or Capsule.");
            }
        }

        private static void ValidateMaterial(
            CharacterTestWorldObjectMaterialAuthoring material,
            string path,
            CharacterTestWorldObjectDefinitionValidationReport report)
        {
            if (material == null)
            {
                report.Add("WOD_MATERIAL_MISSING", path, "Material facet is required.");
                return;
            }

            if (!TryParse(material.Kind, out CombatSurfaceMaterialKind _))
                report.Add("WOD_MATERIAL_KIND_INVALID", path + ".kind", "Material kind is invalid.");
            if (material.FrictionRaw < 0 || material.ElasticityRaw < 0 || material.FixedBounceHeightRaw < 0)
                report.Add("WOD_MATERIAL_VALUE_INVALID", path, "Material raw values cannot be negative.");
        }

        private static void ValidateShape(
            CharacterTestWorldObjectShapeAuthoring shape,
            string path,
            CharacterTestWorldObjectDefinitionValidationReport report)
        {
            if (shape == null)
            {
                report.Add("WOD_SHAPE_MISSING", path, "Shape facet is required.");
                return;
            }

            if (!TryParse(shape.Kind, out CharacterTestMovableNaturalObjectShapeKind _))
            {
                report.Add("WOD_SHAPE_KIND_INVALID", path + ".kind", "Shape kind is invalid.");
            }

            if (shape.HalfExtentXMillimeters <= 0
                || shape.HalfExtentYMillimeters <= 0
                || shape.HalfExtentZMillimeters <= 0)
            {
                report.Add("WOD_SHAPE_EXTENTS_INVALID", path, "Shape half extents must be positive.");
            }
        }

        private static void ValidatePotency(
            CharacterTestWorldObjectPotencyAuthoring potency,
            string path,
            CharacterTestWorldObjectDefinitionValidationReport report)
        {
            if (potency == null)
            {
                report.Add("WOD_POTENCY_MISSING", path, "Potency facet is required.");
                return;
            }

            int[] values =
            {
                potency.BaseMotion,
                potency.BaseCohesion,
                potency.BaseScatter,
                potency.BurningMinMotion,
                potency.BurningMinScatter,
                potency.BurningMaxCohesion,
                potency.CondensedLayerMinCohesion,
                potency.CondensedLayerMaxMotion
            };
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] < CharacterTestGenesisPotencyRuntime.MinStateValue
                    || values[i] > CharacterTestGenesisPotencyRuntime.MaxStateValue)
                {
                    report.Add("WOD_POTENCY_VALUE_INVALID", path, "Potency values must be between 0 and 1000.");
                    return;
                }
            }
        }

        private static void ValidateFacets(
            CharacterTestWorldObjectDefinitionAuthoringEntry entry,
            CharacterTestWorldObjectKind objectKind,
            string path,
            CharacterTestWorldObjectDefinitionValidationReport report)
        {
            bool movable = entry.MovableNaturalObject?.Enabled == true;
            bool staticCollider = entry.StaticCollider?.Enabled == true;
            bool point = entry.Point?.Enabled == true;
            bool region = entry.Region?.Enabled == true;
            int enabledCount = (movable ? 1 : 0) + (staticCollider ? 1 : 0) + (point ? 1 : 0) + (region ? 1 : 0);
            bool expected = objectKind == CharacterTestWorldObjectKind.MovableNaturalObject && movable
                || objectKind == CharacterTestWorldObjectKind.StaticBoundary && staticCollider
                || objectKind == CharacterTestWorldObjectKind.LevelPoint && point
                || objectKind == CharacterTestWorldObjectKind.LevelRegion && region;
            if (enabledCount != 1 || !expected)
                report.Add("WOD_FACET_MISMATCH", path, "Exactly one typed facet must match objectKind.");

            if (movable && entry.MovableNaturalObject.MassGrams <= 0)
                report.Add("WOD_MOVABLE_MASS_INVALID", path + ".movableNaturalObject.massGrams", "Mass must be positive.");
            if (staticCollider
                && (entry.StaticCollider.CombatLayer < 0
                    || entry.StaticCollider.CombatLayer > 31
                    || entry.StaticCollider.ColliderId <= 0))
            {
                report.Add("WOD_STATIC_COLLIDER_INVALID", path + ".staticCollider", "Static collider values are invalid.");
            }
            if (point)
            {
                bool kindParsed = TryParse(entry.Point.PointKind, out LevelWorldObjectPointKind pointKind);
                bool ownerParsed = TryParse(
                    entry.Point.ControlOwner,
                    out CharacterTestCharacterControlOwner controlOwner);
                bool isSpawn = pointKind == LevelWorldObjectPointKind.PlayerSpawn
                    || pointKind == LevelWorldObjectPointKind.AiSpawn;
                bool identityValid = isSpawn
                    ? entry.Point.CharacterId > 0 && entry.Point.TeamId > 0
                    : entry.Point.CharacterId == 0
                        && entry.Point.TeamId == 0
                        && controlOwner == CharacterTestCharacterControlOwner.None;
                if (!kindParsed
                    || pointKind == LevelWorldObjectPointKind.Unknown
                    || !ownerParsed
                    || !identityValid)
                {
                    report.Add(
                        "WOD_POINT_INVALID",
                        path + ".point",
                        "Spawn points require character/team identity; spatial points must not carry spawn identity.");
                }
            }
            if (region
                && (!TryParse(entry.Region.RegionKind, out LevelWorldObjectRegionKind regionKind)
                    || regionKind == LevelWorldObjectRegionKind.Unknown))
            {
                report.Add("WOD_REGION_INVALID", path + ".region", "Region facet values are invalid.");
            }
        }

        private static void ValidateTags(
            IReadOnlyList<string> tags,
            string path,
            CharacterTestWorldObjectDefinitionValidationReport report)
        {
            if (tags == null)
            {
                report.Add("WOD_TAGS_NULL", path, "Tags cannot be null.");
                return;
            }

            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(tags[i]) || !unique.Add(tags[i]))
                    report.Add("WOD_TAG_INVALID", path + "[" + i + "]", "Tags must be non-empty and unique.");
            }
        }

        private static bool TryParse<T>(string value, out T result) where T : struct
        {
            return Enum.TryParse(value, ignoreCase: false, out result)
                && Enum.IsDefined(typeof(T), result);
        }
    }
}
