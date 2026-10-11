using System;
using System.Collections.Generic;
using MxFramework.Audio.Physical;
using Newtonsoft.Json;

namespace MxFramework.Audio.Physical.Config
{
    /// <summary>Startup-only JSON adapter for physical-audio runtime catalogs.</summary>
    public static class PhysicalAudioRuntimeCatalogJson
    {
        public const string Format = "mx.audioPhysicalRuntimeCatalog.v2";
        public const int SchemaVersion = 2;
        public const string LegacyFormat = "mx.audioPhysicalRuntimeCatalog.v1";
        public const int LegacySchemaVersion = 1;

        /// <summary>Parses one complete physical-audio catalog without merging another source.</summary>
        /// <remarks>LowFreqAlloc. JSON parsing and lookup index construction happen during startup.</remarks>
        public static bool TryLoad(string json, out PhysicalAudioDefinitionCatalog catalog, out string error)
        {
            catalog = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Physical audio runtime catalog JSON is empty.";
                return false;
            }

            try
            {
                PhysicalAudioRuntimeCatalogDto dto = JsonConvert.DeserializeObject<PhysicalAudioRuntimeCatalogDto>(json);
                if (dto == null)
                    throw new JsonSerializationException("Physical audio runtime catalog is invalid.");
                bool legacy = string.Equals(dto.Format, LegacyFormat, StringComparison.Ordinal)
                    && dto.SchemaVersion == LegacySchemaVersion;
                bool current = string.Equals(dto.Format, Format, StringComparison.Ordinal)
                    && dto.SchemaVersion == SchemaVersion;
                if (!legacy && !current)
                    throw new JsonSerializationException("Physical audio runtime catalog must be v2 or the supported v1 migration input.");
                if (string.IsNullOrWhiteSpace(dto.AudioCatalogHash))
                    throw new JsonSerializationException("Physical audio runtime catalog audioCatalogHash is required.");
                if (string.IsNullOrWhiteSpace(dto.PhysicalManifestHash))
                    throw new JsonSerializationException("Physical audio runtime catalog physicalManifestHash is required.");

                catalog = legacy ? BuildLegacyCatalog(dto) : BuildCurrentCatalog(dto);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static PhysicalAudioDefinitionCatalog BuildCurrentCatalog(PhysicalAudioRuntimeCatalogDto dto)
        {
            return new PhysicalAudioDefinitionCatalog(
                BuildMaterials(dto.Materials),
                BuildMaterialPairs(dto.MaterialPairs),
                BuildFamilies(dto.Families),
                BuildCurrentBases(dto.BasisSounds),
                BuildResponses(dto.ResponseProfiles),
                dto.PhysicalManifestHash,
                dto.AudioCatalogHash);
        }

        private static PhysicalAudioDefinitionCatalog BuildLegacyCatalog(PhysicalAudioRuntimeCatalogDto dto)
        {
            PhysicalAudioFamilyDefinition[] families = BuildFamilies(dto.Families);
            var pairByKey = new Dictionary<long, AcousticMaterialPairDefinition>();
            var pairs = new List<AcousticMaterialPairDefinition>();
            var bases = new PhysicalAudioBasisDefinition[dto.BasisSounds?.Count ?? 0];
            for (int i = 0; i < bases.Length; i++)
            {
                BasisDto item = dto.BasisSounds[i] ?? throw new JsonSerializationException("Physical audio Basis entry cannot be null.");
                BodyAnchorDto impactor = item.Impactor ?? throw new JsonSerializationException("Physical audio Basis impactor anchor is required.");
                BodyAnchorDto receiver = item.Receiver ?? throw new JsonSerializationException("Physical audio Basis receiver anchor is required.");
                int materialA = impactor.MaterialRuntimeId <= receiver.MaterialRuntimeId
                    ? impactor.MaterialRuntimeId
                    : receiver.MaterialRuntimeId;
                int materialB = impactor.MaterialRuntimeId <= receiver.MaterialRuntimeId
                    ? receiver.MaterialRuntimeId
                    : impactor.MaterialRuntimeId;
                long key = ((long)materialA << 32) | (uint)materialB;
                if (!pairByKey.TryGetValue(key, out AcousticMaterialPairDefinition pair))
                {
                    pair = new AcousticMaterialPairDefinition(
                        new AcousticMaterialPairId(550001 + pairs.Count),
                        new AcousticMaterialId(materialA),
                        new AcousticMaterialId(materialB));
                    pairByKey.Add(key, pair);
                    pairs.Add(pair);
                }
                PhysicalAudioVariantKind variant = FindLegacyVariant(families, item.FamilyRuntimeId);
                bool impactorIsA = impactor.MaterialRuntimeId <= receiver.MaterialRuntimeId;
                bases[i] = new PhysicalAudioBasisDefinition(
                    new AudioBasisId(item.RuntimeBasisId),
                    pair.Id,
                    variant,
                    item.RuntimeCueId,
                    item.Priority,
                    BuildBodyAnchor(impactorIsA ? impactor : receiver, "sideA"),
                    BuildBodyAnchor(impactorIsA ? receiver : impactor, "sideB"),
                    BuildRange(item.RelativeSpeed, "relativeSpeed"),
                    BuildRange(item.Intensity, "intensity"),
                    BuildRange(item.AngleDegrees, "angleDegrees"));
            }
            return new PhysicalAudioDefinitionCatalog(
                BuildMaterials(dto.Materials),
                pairs.ToArray(),
                families,
                bases,
                BuildResponses(dto.ResponseProfiles),
                dto.PhysicalManifestHash,
                dto.AudioCatalogHash);
        }

        private static PhysicalAudioVariantKind FindLegacyVariant(PhysicalAudioFamilyDefinition[] families, int familyId)
        {
            for (int i = 0; i < families.Length; i++)
            {
                if (families[i].Id.Value == familyId)
                    return families[i].VariantKind;
            }
            throw new JsonSerializationException("Physical audio legacy Basis references missing family " + familyId + ".");
        }

        private static AcousticMaterialProfileDefinition[] BuildMaterials(List<MaterialDto> source)
        {
            source ??= new List<MaterialDto>();
            var result = new AcousticMaterialProfileDefinition[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                MaterialDto item = source[i] ?? throw new JsonSerializationException("Physical audio material entry cannot be null.");
                result[i] = new AcousticMaterialProfileDefinition(
                    new AcousticMaterialId(item.RuntimeMaterialId),
                    item.Hardness,
                    item.Density,
                    item.Roughness,
                    item.Damping,
                    item.Resonance,
                    new AcousticMaterialId(item.FallbackMaterialRuntimeId));
            }
            return result;
        }

        private static PhysicalAudioFamilyDefinition[] BuildFamilies(List<FamilyDto> source)
        {
            source ??= new List<FamilyDto>();
            var result = new PhysicalAudioFamilyDefinition[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                FamilyDto item = source[i] ?? throw new JsonSerializationException("Physical audio family entry cannot be null.");
                WeightDto weights = item.Weights ?? new WeightDto();
                var runtimeWeights = new PhysicalAudioSelectionWeights(
                    weights.Material,
                    weights.Mass,
                    weights.ContactArea,
                    weights.Surface,
                    weights.Construction,
                    weights.Speed,
                    weights.Intensity,
                    weights.Angle);
                result[i] = new PhysicalAudioFamilyDefinition(
                    new PhysicalAudioFamilyId(item.RuntimeFamilyId),
                    ParseInteractionKind(item.InteractionKind),
                    ParseInteractionMode(item.InteractionMode),
                    item.VariantKind > 0
                        ? ParseVariantKind(item.VariantKind)
                        : VariantFromMode(ParseInteractionMode(item.InteractionMode)),
                    new PhysicalAudioFamilyId(item.FallbackFamilyRuntimeId),
                    new AudioBasisId(item.FallbackBasisRuntimeId),
                    new PhysicalAudioResponseProfileId(item.ResponseProfileRuntimeId),
                    item.Priority,
                    item.CooldownFrames,
                    runtimeWeights);
            }
            return result;
        }

        private static AcousticMaterialPairDefinition[] BuildMaterialPairs(List<MaterialPairDto> source)
        {
            source ??= new List<MaterialPairDto>();
            var result = new AcousticMaterialPairDefinition[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                MaterialPairDto item = source[i] ?? throw new JsonSerializationException("Physical audio Pair entry cannot be null.");
                result[i] = new AcousticMaterialPairDefinition(
                    new AcousticMaterialPairId(item.RuntimeMaterialPairId),
                    new AcousticMaterialId(item.MaterialARuntimeId),
                    new AcousticMaterialId(item.MaterialBRuntimeId));
            }
            return result;
        }

        private static PhysicalAudioBasisDefinition[] BuildCurrentBases(List<BasisDto> source)
        {
            source ??= new List<BasisDto>();
            var result = new PhysicalAudioBasisDefinition[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                BasisDto item = source[i] ?? throw new JsonSerializationException("Physical audio Basis entry cannot be null.");
                result[i] = new PhysicalAudioBasisDefinition(
                    new AudioBasisId(item.RuntimeBasisId),
                    new AcousticMaterialPairId(item.MaterialPairRuntimeId),
                    ParseVariantKind(item.VariantKind),
                    item.RuntimeCueId,
                    item.Priority,
                    BuildBodyAnchor(item.SideA, "sideA"),
                    BuildBodyAnchor(item.SideB, "sideB"),
                    BuildRange(item.RelativeSpeed, "relativeSpeed"),
                    BuildRange(item.Intensity, "intensity"),
                    BuildRange(item.AngleDegrees, "angleDegrees"));
            }
            return result;
        }

        private static PhysicalAudioVariantKind ParseVariantKind(int value)
        {
            if (value >= (int)PhysicalAudioVariantKind.Impact
                && value <= (int)PhysicalAudioVariantKind.Compression)
                return (PhysicalAudioVariantKind)value;
            throw new JsonSerializationException("Physical audio variantKind numeric value is invalid: " + value + ".");
        }

        private static PhysicalAudioVariantKind VariantFromMode(PhysicalAudioInteractionMode mode)
        {
            switch (mode)
            {
                case PhysicalAudioInteractionMode.Step: return PhysicalAudioVariantKind.Step;
                case PhysicalAudioInteractionMode.Landing: return PhysicalAudioVariantKind.Landing;
                case PhysicalAudioInteractionMode.Hit: return PhysicalAudioVariantKind.Hit;
                case PhysicalAudioInteractionMode.Block: return PhysicalAudioVariantKind.Block;
                case PhysicalAudioInteractionMode.Sweep: return PhysicalAudioVariantKind.Sweep;
                case PhysicalAudioInteractionMode.Scrape: return PhysicalAudioVariantKind.Scrape;
                case PhysicalAudioInteractionMode.Roll: return PhysicalAudioVariantKind.Roll;
                case PhysicalAudioInteractionMode.Compression: return PhysicalAudioVariantKind.Compression;
                default: return PhysicalAudioVariantKind.Impact;
            }
        }

        private static PhysicalAudioBodyAnchorDefinition BuildBodyAnchor(BodyAnchorDto source, string label)
        {
            if (source == null)
                throw new JsonSerializationException("Physical audio Basis " + label + " anchor is required.");
            return new PhysicalAudioBodyAnchorDefinition(
                new AcousticMaterialId(source.MaterialRuntimeId),
                BuildRange(source.Mass, label + ".mass"),
                BuildRange(source.ContactArea, label + ".contactArea"),
                ParseSurface(source.Surface, label + ".surface"),
                ParseConstruction(source.Construction, label + ".construction"));
        }

        private static PhysicalAudioRange BuildRange(RangeDto source, string label)
        {
            if (source == null)
                throw new JsonSerializationException("Physical audio " + label + " range is required.");
            return new PhysicalAudioRange(source.Min, source.Max, source.Anchor);
        }

        private static PhysicalAudioResponseProfileDefinition[] BuildResponses(List<ResponseProfileDto> source)
        {
            source ??= new List<ResponseProfileDto>();
            var result = new PhysicalAudioResponseProfileDefinition[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                ResponseProfileDto item = source[i] ?? throw new JsonSerializationException("Physical audio response profile entry cannot be null.");
                result[i] = new PhysicalAudioResponseProfileDefinition(
                    new PhysicalAudioResponseProfileId(item.RuntimeResponseProfileId),
                    BuildCurves(item.Curves));
            }
            return result;
        }

        private static PhysicalAudioResponseCurveDefinition[] BuildCurves(List<ResponseCurveDto> source)
        {
            source ??= new List<ResponseCurveDto>();
            var result = new PhysicalAudioResponseCurveDefinition[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                ResponseCurveDto item = source[i] ?? throw new JsonSerializationException("Physical audio response curve entry cannot be null.");
                result[i] = new PhysicalAudioResponseCurveDefinition(
                    item.ParameterId,
                    ParseResponseInput(item.Input),
                    item.OutputMin,
                    item.OutputMax,
                    BuildPoints(item.Points));
            }
            return result;
        }

        private static PhysicalAudioResponsePoint[] BuildPoints(List<ResponsePointDto> source)
        {
            source ??= new List<ResponsePointDto>();
            var result = new PhysicalAudioResponsePoint[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                ResponsePointDto item = source[i] ?? throw new JsonSerializationException("Physical audio response point cannot be null.");
                result[i] = new PhysicalAudioResponsePoint(item.Input, item.Output);
            }
            return result;
        }

        private static PhysicalAudioInteractionKind ParseInteractionKind(int value)
        {
            if (value >= (int)PhysicalAudioInteractionKind.Footstep
                && value <= (int)PhysicalAudioInteractionKind.Compression)
                return (PhysicalAudioInteractionKind)value;
            throw new JsonSerializationException("Physical audio interactionKind numeric value is invalid: " + value + ".");
        }

        private static PhysicalAudioInteractionMode ParseInteractionMode(int value)
        {
            if (value >= (int)PhysicalAudioInteractionMode.Any
                && value <= (int)PhysicalAudioInteractionMode.Compression)
                return (PhysicalAudioInteractionMode)value;
            throw new JsonSerializationException("Physical audio interactionMode numeric value is invalid: " + value + ".");
        }

        private static AcousticSurfaceKind ParseSurface(int value, string field)
        {
            if (value >= (int)AcousticSurfaceKind.Any && value <= (int)AcousticSurfaceKind.Granular)
                return (AcousticSurfaceKind)value;
            throw new JsonSerializationException("Physical audio " + field + " numeric value is invalid: " + value + ".");
        }

        private static AcousticConstructionKind ParseConstruction(int value, string field)
        {
            if (value >= (int)AcousticConstructionKind.Any && value <= (int)AcousticConstructionKind.Gas)
                return (AcousticConstructionKind)value;
            throw new JsonSerializationException("Physical audio " + field + " numeric value is invalid: " + value + ".");
        }

        private static PhysicalAudioResponseInput ParseResponseInput(int value)
        {
            if (value >= (int)PhysicalAudioResponseInput.EffectiveEnergy
                && value <= (int)PhysicalAudioResponseInput.CombinedDamping)
                return (PhysicalAudioResponseInput)value;
            throw new JsonSerializationException("Physical audio response input numeric value is invalid: " + value + ".");
        }

        private sealed class PhysicalAudioRuntimeCatalogDto
        {
            public string Format { get; set; } = string.Empty;
            public int SchemaVersion { get; set; }
            public string PhysicalManifestHash { get; set; } = string.Empty;
            public string AudioCatalogHash { get; set; } = string.Empty;
            public List<MaterialDto> Materials { get; set; } = new List<MaterialDto>();
            public List<MaterialPairDto> MaterialPairs { get; set; } = new List<MaterialPairDto>();
            public List<FamilyDto> Families { get; set; } = new List<FamilyDto>();
            public List<BasisDto> BasisSounds { get; set; } = new List<BasisDto>();
            public List<ResponseProfileDto> ResponseProfiles { get; set; } = new List<ResponseProfileDto>();
        }

        private sealed class MaterialDto
        {
            public int RuntimeMaterialId { get; set; }
            public float Hardness { get; set; }
            public float Density { get; set; }
            public float Roughness { get; set; }
            public float Damping { get; set; }
            public float Resonance { get; set; }
            public int FallbackMaterialRuntimeId { get; set; }
        }

        private sealed class MaterialPairDto
        {
            public int RuntimeMaterialPairId { get; set; }
            public int MaterialARuntimeId { get; set; }
            public int MaterialBRuntimeId { get; set; }
        }

        private sealed class FamilyDto
        {
            public int RuntimeFamilyId { get; set; }
            public int InteractionKind { get; set; }
            public int InteractionMode { get; set; }
            public int VariantKind { get; set; }
            public int FallbackFamilyRuntimeId { get; set; }
            public int FallbackBasisRuntimeId { get; set; }
            public int ResponseProfileRuntimeId { get; set; }
            public int Priority { get; set; }
            public int CooldownFrames { get; set; }
            public WeightDto Weights { get; set; } = new WeightDto();
        }

        private sealed class WeightDto
        {
            public float Material { get; set; }
            public float Mass { get; set; }
            public float ContactArea { get; set; }
            public float Surface { get; set; }
            public float Construction { get; set; }
            public float Speed { get; set; }
            public float Intensity { get; set; }
            public float Angle { get; set; }
        }

        private sealed class BasisDto
        {
            public int RuntimeBasisId { get; set; }
            public int FamilyRuntimeId { get; set; }
            public int MaterialPairRuntimeId { get; set; }
            public int VariantKind { get; set; }
            public int RuntimeCueId { get; set; }
            public int Priority { get; set; }
            public BodyAnchorDto Impactor { get; set; }
            public BodyAnchorDto Receiver { get; set; }
            public BodyAnchorDto SideA { get; set; }
            public BodyAnchorDto SideB { get; set; }
            public RangeDto RelativeSpeed { get; set; }
            public RangeDto Intensity { get; set; }
            public RangeDto AngleDegrees { get; set; }
        }

        private sealed class BodyAnchorDto
        {
            public int MaterialRuntimeId { get; set; }
            public RangeDto Mass { get; set; }
            public RangeDto ContactArea { get; set; }
            public int Surface { get; set; }
            public int Construction { get; set; }
        }

        private sealed class RangeDto
        {
            public float Min { get; set; }
            public float Max { get; set; }
            public float Anchor { get; set; }
        }

        private sealed class ResponseProfileDto
        {
            public int RuntimeResponseProfileId { get; set; }
            public List<ResponseCurveDto> Curves { get; set; } = new List<ResponseCurveDto>();
        }

        private sealed class ResponseCurveDto
        {
            public int ParameterId { get; set; }
            public int Input { get; set; }
            public float OutputMin { get; set; }
            public float OutputMax { get; set; }
            public List<ResponsePointDto> Points { get; set; } = new List<ResponsePointDto>();
        }

        private sealed class ResponsePointDto
        {
            public float Input { get; set; }
            public float Output { get; set; }
        }
    }
}
