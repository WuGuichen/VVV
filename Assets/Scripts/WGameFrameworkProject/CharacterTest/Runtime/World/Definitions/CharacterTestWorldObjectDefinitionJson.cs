using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestWorldObjectDefinitionJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture,
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        /// <remarks>AllocByDesign: strict authoring JSON parsing and validation.</remarks>
        public static bool TryLoadAuthoring(
            string json,
            out CharacterTestWorldObjectDefinitionAuthoringDocument document,
            out CharacterTestWorldObjectDefinitionValidationReport report)
        {
            document = null;
            report = new CharacterTestWorldObjectDefinitionValidationReport();
            if (string.IsNullOrWhiteSpace(json))
            {
                report.Add("WOD_JSON_EMPTY", "$", "World object definition JSON is empty.");
                return false;
            }

            try
            {
                document = JsonConvert.DeserializeObject<CharacterTestWorldObjectDefinitionAuthoringDocument>(
                    json,
                    Settings);
                report = CharacterTestWorldObjectDefinitionValidator.Validate(document);
                return report.Success;
            }
            catch (JsonException exception)
            {
                report.Add("WOD_JSON_INVALID", "$", exception.Message);
                return false;
            }
        }

        /// <remarks>AllocByDesign: strict runtime JSON parsing, validation and hash verification.</remarks>
        public static bool TryLoadRuntime(
            string json,
            out CharacterTestWorldObjectDefinitionRuntimeDocument document,
            out CharacterTestWorldObjectDefinitionValidationReport report)
        {
            document = null;
            report = new CharacterTestWorldObjectDefinitionValidationReport();
            if (string.IsNullOrWhiteSpace(json))
            {
                report.Add("WOD_JSON_EMPTY", "$", "World object runtime JSON is empty.");
                return false;
            }

            try
            {
                document = JsonConvert.DeserializeObject<CharacterTestWorldObjectDefinitionRuntimeDocument>(
                    json,
                    Settings);
                report = CharacterTestWorldObjectDefinitionValidator.ValidateRuntime(document);
                if (report.Success
                    && !string.Equals(
                        document.ContentHash,
                        ComputeRuntimeContentHash(document),
                        StringComparison.Ordinal))
                {
                    report.Add("WOD_CONTENT_HASH_MISMATCH", "$.contentHash", "Runtime content hash does not match.");
                }

                return report.Success;
            }
            catch (JsonException exception)
            {
                report.Add("WOD_JSON_INVALID", "$", exception.Message);
                return false;
            }
        }

        /// <remarks>AllocByDesign: canonical authoring serialization.</remarks>
        public static string SerializeAuthoring(
            CharacterTestWorldObjectDefinitionAuthoringDocument document,
            bool indented = true)
        {
            return JsonConvert.SerializeObject(
                NormalizeAuthoring(document),
                indented ? Formatting.Indented : Formatting.None,
                Settings);
        }

        /// <remarks>AllocByDesign: canonical runtime serialization.</remarks>
        public static string SerializeRuntime(
            CharacterTestWorldObjectDefinitionRuntimeDocument document,
            bool indented = true)
        {
            return JsonConvert.SerializeObject(
                NormalizeRuntime(document),
                indented ? Formatting.Indented : Formatting.None,
                Settings);
        }

        /// <remarks>AllocByDesign: canonical source hashing during authoring/startup.</remarks>
        public static string ComputeAuthoringHash(
            CharacterTestWorldObjectDefinitionAuthoringDocument document)
        {
            return ComputeSha256(SerializeAuthoring(document, indented: false));
        }

        /// <remarks>AllocByDesign: canonical runtime content hashing during compilation/startup.</remarks>
        public static string ComputeRuntimeContentHash(
            CharacterTestWorldObjectDefinitionRuntimeDocument document)
        {
            CharacterTestWorldObjectDefinitionRuntimeDocument normalized = NormalizeRuntime(document);
            normalized.SourceHash = string.Empty;
            normalized.ContentHash = string.Empty;
            return ComputeSha256(JsonConvert.SerializeObject(normalized, Formatting.None, Settings));
        }

        internal static CharacterTestWorldObjectDefinitionAuthoringDocument NormalizeAuthoring(
            CharacterTestWorldObjectDefinitionAuthoringDocument document)
        {
            var normalized = new CharacterTestWorldObjectDefinitionAuthoringDocument
            {
                Format = document?.Format ?? string.Empty,
                SchemaVersion = document?.SchemaVersion ?? 0,
                Definitions = CloneAndSort(document?.Definitions)
            };
            return normalized;
        }

        internal static CharacterTestWorldObjectDefinitionRuntimeDocument NormalizeRuntime(
            CharacterTestWorldObjectDefinitionRuntimeDocument document)
        {
            return new CharacterTestWorldObjectDefinitionRuntimeDocument
            {
                Format = document?.Format ?? string.Empty,
                SchemaVersion = document?.SchemaVersion ?? 0,
                SourceHash = document?.SourceHash ?? string.Empty,
                ContentHash = document?.ContentHash ?? string.Empty,
                Definitions = CloneAndSort(document?.Definitions)
            };
        }

        internal static CharacterTestWorldObjectDefinitionAuthoringEntry CloneRuntimeEntry(
            CharacterTestWorldObjectDefinitionAuthoringEntry source)
        {
            CharacterTestWorldObjectDefinitionAuthoringEntry result = CloneEntry(source);
            result.Description = string.Empty;
            result.Tags.Clear();
            return result;
        }

        private static List<CharacterTestWorldObjectDefinitionAuthoringEntry> CloneAndSort(
            IReadOnlyList<CharacterTestWorldObjectDefinitionAuthoringEntry> source)
        {
            var result = new List<CharacterTestWorldObjectDefinitionAuthoringEntry>(source?.Count ?? 0);
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                    result.Add(CloneEntry(source[i]));
            }

            result.Sort(CompareEntries);
            return result;
        }

        private static CharacterTestWorldObjectDefinitionAuthoringEntry CloneEntry(
            CharacterTestWorldObjectDefinitionAuthoringEntry source)
        {
            source ??= new CharacterTestWorldObjectDefinitionAuthoringEntry();
            CharacterTestWorldObjectPresentationAuthoring presentation =
                source.Presentation ?? new CharacterTestWorldObjectPresentationAuthoring();
            CharacterTestResourceKeyAuthoring resource =
                presentation.ResourceKey ?? new CharacterTestResourceKeyAuthoring();
            CharacterTestWorldObjectMaterialAuthoring material =
                source.Material ?? new CharacterTestWorldObjectMaterialAuthoring();
            CharacterTestWorldObjectShapeAuthoring shape =
                source.Shape ?? new CharacterTestWorldObjectShapeAuthoring();
            CharacterTestWorldObjectPotencyAuthoring potency =
                source.Potency ?? new CharacterTestWorldObjectPotencyAuthoring();
            CharacterTestMovableNaturalObjectFacetAuthoring movable =
                source.MovableNaturalObject ?? new CharacterTestMovableNaturalObjectFacetAuthoring();
            CharacterTestStaticColliderFacetAuthoring staticCollider =
                source.StaticCollider ?? new CharacterTestStaticColliderFacetAuthoring();
            CharacterTestPointFacetAuthoring point =
                source.Point ?? new CharacterTestPointFacetAuthoring();
            CharacterTestRegionFacetAuthoring region =
                source.Region ?? new CharacterTestRegionFacetAuthoring();
            var tags = source.Tags == null ? new List<string>() : new List<string>(source.Tags);
            tags.Sort(StringComparer.Ordinal);
            return new CharacterTestWorldObjectDefinitionAuthoringEntry
            {
                Enabled = source.Enabled,
                Deprecated = source.Deprecated,
                DefinitionId = source.DefinitionId,
                StableId = source.StableId ?? string.Empty,
                DisplayName = source.DisplayName ?? string.Empty,
                ObjectKind = source.ObjectKind ?? string.Empty,
                Category = source.Category ?? string.Empty,
                Presentation = new CharacterTestWorldObjectPresentationAuthoring
                {
                    Kind = presentation.Kind ?? string.Empty,
                    ResourceKey = new CharacterTestResourceKeyAuthoring
                    {
                        Id = resource.Id ?? string.Empty,
                        TypeId = resource.TypeId ?? string.Empty,
                        Variant = resource.Variant ?? string.Empty,
                        PackageId = resource.PackageId ?? string.Empty
                    },
                    PrimitiveKind = presentation.PrimitiveKind ?? string.Empty
                },
                Material = new CharacterTestWorldObjectMaterialAuthoring
                {
                    Kind = material.Kind ?? string.Empty,
                    FrictionRaw = material.FrictionRaw,
                    ElasticityRaw = material.ElasticityRaw,
                    FixedBounceHeightRaw = material.FixedBounceHeightRaw
                },
                Shape = new CharacterTestWorldObjectShapeAuthoring
                {
                    Kind = shape.Kind ?? string.Empty,
                    HalfExtentXMillimeters = shape.HalfExtentXMillimeters,
                    HalfExtentYMillimeters = shape.HalfExtentYMillimeters,
                    HalfExtentZMillimeters = shape.HalfExtentZMillimeters
                },
                Potency = ClonePotency(potency),
                MovableNaturalObject = new CharacterTestMovableNaturalObjectFacetAuthoring
                {
                    Enabled = movable.Enabled,
                    MassGrams = movable.MassGrams
                },
                StaticCollider = new CharacterTestStaticColliderFacetAuthoring
                {
                    Enabled = staticCollider.Enabled,
                    CombatLayer = staticCollider.CombatLayer,
                    ColliderId = staticCollider.ColliderId
                },
                Point = new CharacterTestPointFacetAuthoring
                {
                    Enabled = point.Enabled,
                    PointKind = point.PointKind ?? string.Empty,
                    CharacterId = point.CharacterId,
                    TeamId = point.TeamId,
                    ControlOwner = point.ControlOwner ?? string.Empty
                },
                Region = new CharacterTestRegionFacetAuthoring
                {
                    Enabled = region.Enabled,
                    RegionKind = region.RegionKind ?? string.Empty
                },
                Description = source.Description ?? string.Empty,
                Tags = tags
            };
        }

        private static CharacterTestWorldObjectPotencyAuthoring ClonePotency(
            CharacterTestWorldObjectPotencyAuthoring source)
        {
            return new CharacterTestWorldObjectPotencyAuthoring
            {
                BaseMotion = source.BaseMotion,
                BaseCohesion = source.BaseCohesion,
                BaseScatter = source.BaseScatter,
                ImmuneMotion = source.ImmuneMotion,
                ImmuneCohesion = source.ImmuneCohesion,
                ImmuneScatter = source.ImmuneScatter,
                BurningEnabled = source.BurningEnabled,
                CrushEnabled = source.CrushEnabled,
                CrackEnabled = source.CrackEnabled,
                CondensedLayerEnabled = source.CondensedLayerEnabled,
                BurningMinMotion = source.BurningMinMotion,
                BurningMinScatter = source.BurningMinScatter,
                BurningMaxCohesion = source.BurningMaxCohesion,
                CondensedLayerMinCohesion = source.CondensedLayerMinCohesion,
                CondensedLayerMaxMotion = source.CondensedLayerMaxMotion
            };
        }

        private static int CompareEntries(
            CharacterTestWorldObjectDefinitionAuthoringEntry left,
            CharacterTestWorldObjectDefinitionAuthoringEntry right)
        {
            int id = (left?.DefinitionId ?? 0).CompareTo(right?.DefinitionId ?? 0);
            return id != 0
                ? id
                : string.CompareOrdinal(left?.StableId, right?.StableId);
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(71);
                builder.Append("sha256:");
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }
}
