using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public static partial class PhysicalAudioAuthoringManifest
    {
        public static PhysicalAudioRuntimeCatalogDocument CompileRuntimeCatalog(PhysicalAudioAuthoringManifestDocument manifest, AudioSemanticManifestDocument semanticManifest, string physicalManifestHash, string audioCatalogHash)
        {
            manifest = Upgrade(manifest);
            var result = new PhysicalAudioRuntimeCatalogDocument { PhysicalManifestHash = physicalManifestHash ?? string.Empty, AudioCatalogHash = audioCatalogHash ?? string.Empty };
            for (int i = 0; i < manifest.MaterialProfiles.Count; i++)
            {
                PhysicalAudioMaterialProfileAuthoring item = manifest.MaterialProfiles[i];
                if (item == null || !IsActive(item.Lifecycle)) continue;
                result.Materials.Add(new PhysicalAudioRuntimeMaterial { RuntimeMaterialId = item.RuntimeMaterialId, Hardness = item.Hardness, Density = item.Density, Roughness = item.Roughness, Damping = item.Damping, Resonance = item.Resonance, FallbackMaterialRuntimeId = FindActiveMaterial(manifest, item.FallbackMaterialId)?.RuntimeMaterialId ?? 0 });
            }
            for (int i = 0; i < manifest.MaterialPairs.Count; i++)
            {
                PhysicalAudioMaterialPairAuthoring item = manifest.MaterialPairs[i];
                if (item == null || !IsActive(item.Lifecycle)) continue;
                PhysicalAudioMaterialProfileAuthoring materialA = FindActiveMaterial(manifest, item.MaterialAId);
                PhysicalAudioMaterialProfileAuthoring materialB = FindActiveMaterial(manifest, item.MaterialBId);
                if (materialA == null || materialB == null) continue;
                result.MaterialPairs.Add(new PhysicalAudioRuntimeMaterialPair
                {
                    RuntimeMaterialPairId = item.RuntimeMaterialPairId,
                    MaterialARuntimeId = materialA.RuntimeMaterialId,
                    MaterialBRuntimeId = materialB.RuntimeMaterialId
                });
            }
            for (int i = 0; i < manifest.ResponseProfiles.Count; i++)
            {
                PhysicalAudioResponseProfileAuthoring item = manifest.ResponseProfiles[i];
                if (item == null || !IsActive(item.Lifecycle)) continue;
                result.ResponseProfiles.Add(new PhysicalAudioRuntimeResponseProfile { RuntimeResponseProfileId = item.RuntimeResponseProfileId, Curves = BuildRuntimeCurves(item.Curves) });
            }
            for (int i = 0; i < manifest.Families.Count; i++)
            {
                PhysicalAudioFamilyAuthoring item = manifest.Families[i];
                if (item == null || !IsActive(item.Lifecycle)) continue;
                PhysicalAudioFamilyAuthoring fallbackFamily = FindActiveFamily(manifest, item.FallbackFamilyId);
                PhysicalAudioBasisSoundAuthoring fallbackBasis = FindActiveBasis(manifest, item.FallbackBasisId);
                PhysicalAudioResponseProfileAuthoring activeResponse = FindActiveResponse(manifest, item.ResponseProfileId);
                result.Families.Add(new PhysicalAudioRuntimeFamily
                {
                    RuntimeFamilyId = item.RuntimeFamilyId,
                    InteractionKind = RuntimeInteractionKind(item.InteractionKind),
                    InteractionMode = RuntimeInteractionMode(item.InteractionMode),
                    VariantKind = RuntimeVariantKind(item.VariantKind),
                    FallbackFamilyRuntimeId = fallbackFamily?.RuntimeFamilyId ?? 0,
                    FallbackBasisRuntimeId = fallbackBasis?.RuntimeBasisId ?? 0,
                    ResponseProfileRuntimeId = activeResponse?.RuntimeResponseProfileId ?? 0,
                    Priority = item.Priority,
                    CooldownFrames = item.CooldownFrames,
                    Weights = CloneWeights(item.Weights)
                });
            }
            for (int i = 0; i < manifest.BasisSounds.Count; i++)
            {
                PhysicalAudioBasisSoundAuthoring item = manifest.BasisSounds[i];
                if (item == null || !IsActive(item.Lifecycle)) continue;
                PhysicalAudioFamilyAuthoring family = FindFamilyForBasis(manifest, item);
                PhysicalAudioMaterialPairAuthoring pair = FindActiveMaterialPair(manifest, item.MaterialPairId);
                AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(semanticManifest, item.AudioCueId);
                if (pair == null || cue == null || !string.Equals(cue.Lifecycle, AudioCueLifecycles.Active, StringComparison.Ordinal)) continue;
                result.BasisSounds.Add(new PhysicalAudioRuntimeBasisSound
                {
                    RuntimeBasisId = item.RuntimeBasisId,
                    FamilyRuntimeId = family?.RuntimeFamilyId ?? 0,
                    MaterialPairRuntimeId = pair.RuntimeMaterialPairId,
                    VariantKind = RuntimeVariantKind(item.VariantKind),
                    RuntimeCueId = cue.RuntimeCueId,
                    Priority = item.Priority,
                    Impactor = BuildRuntimeBody(manifest, item.Impactor),
                    Receiver = BuildRuntimeBody(manifest, item.Receiver),
                    SideA = BuildRuntimeBody(manifest, item.SideA),
                    SideB = BuildRuntimeBody(manifest, item.SideB),
                    RelativeSpeed = CloneRange(item.RelativeSpeed), Intensity = CloneRange(item.Intensity), AngleDegrees = CloneRange(item.AngleDegrees)
                });
            }
            result.Materials.Sort((a, b) => a.RuntimeMaterialId.CompareTo(b.RuntimeMaterialId));
            result.MaterialPairs.Sort((a, b) => a.RuntimeMaterialPairId.CompareTo(b.RuntimeMaterialPairId));
            result.Families.Sort((a, b) => a.RuntimeFamilyId.CompareTo(b.RuntimeFamilyId));
            result.BasisSounds.Sort((a, b) => a.RuntimeBasisId.CompareTo(b.RuntimeBasisId));
            result.ResponseProfiles.Sort((a, b) => a.RuntimeResponseProfileId.CompareTo(b.RuntimeResponseProfileId));
            PhysicalAudioValidationReport runtimeValidation = ValidateRuntimeCatalog(result);
            if (runtimeValidation.HasErrors)
                throw new InvalidOperationException("Compiled physical audio runtime catalog failed strict validation: " + runtimeValidation.Diagnostics[0].Code + ".");
            return result;
        }

        /// <summary>Validates the compiled DTO with the same structural invariants enforced by the startup runtime loader.</summary>
        /// <remarks>LowFreqAlloc. Intended for publication and compile/load symmetry tests.</remarks>
        public static PhysicalAudioValidationReport ValidateRuntimeCatalog(PhysicalAudioRuntimeCatalogDocument catalog)
        {
            var report = new PhysicalAudioValidationReport();
            if (catalog == null)
            {
                Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_CATALOG_MISSING", string.Empty, "Runtime physical audio catalog 缺失。 ");
                report.HasErrors = true;
                return report;
            }
            if (!string.Equals(catalog.Format, PhysicalAudioAuthoringFormats.Runtime, StringComparison.Ordinal) || catalog.SchemaVersion != 2)
                Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_FORMAT_INVALID", string.Empty, "Runtime catalog format/schemaVersion 无效。 ");
            if (string.IsNullOrWhiteSpace(catalog.PhysicalManifestHash) || string.IsNullOrWhiteSpace(catalog.AudioCatalogHash))
                Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_HASH_MISSING", string.Empty, "Runtime catalog 必须携带 Physical Manifest 与 Audio Catalog Hash。 ");
            catalog.Materials ??= new List<PhysicalAudioRuntimeMaterial>();
            catalog.MaterialPairs ??= new List<PhysicalAudioRuntimeMaterialPair>();
            catalog.Families ??= new List<PhysicalAudioRuntimeFamily>();
            catalog.BasisSounds ??= new List<PhysicalAudioRuntimeBasisSound>();
            catalog.ResponseProfiles ??= new List<PhysicalAudioRuntimeResponseProfile>();

            var materialIds = new HashSet<int>();
            var familyIds = new HashSet<int>();
            var materialPairIds = new HashSet<int>();
            var basisIds = new HashSet<int>();
            var responseIds = new HashSet<int>();
            var basisVariantById = new Dictionary<int, int>();
            for (int i = 0; i < catalog.Materials.Count; i++)
            {
                PhysicalAudioRuntimeMaterial material = catalog.Materials[i];
                if (material == null || material.RuntimeMaterialId <= 0 || !materialIds.Add(material.RuntimeMaterialId))
                {
                    Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_MATERIAL_ID_INVALID", material?.RuntimeMaterialId.ToString(CultureInfo.InvariantCulture), "Runtime material ID 必须是唯一正整数。 ");
                    continue;
                }
                Validate01(report, material.RuntimeMaterialId.ToString(CultureInfo.InvariantCulture), "runtime acoustic features", material.Hardness, material.Density, material.Roughness, material.Damping, material.Resonance);
            }
            for (int i = 0; i < catalog.Materials.Count; i++)
            {
                PhysicalAudioRuntimeMaterial material = catalog.Materials[i];
                if (material != null && material.FallbackMaterialRuntimeId > 0 && !materialIds.Contains(material.FallbackMaterialRuntimeId))
                    Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_MATERIAL_FALLBACK_MISSING", material.RuntimeMaterialId.ToString(CultureInfo.InvariantCulture), "Runtime material fallback 引用了不存在的 material。 ");
            }
            for (int i = 0; i < catalog.MaterialPairs.Count; i++)
            {
                PhysicalAudioRuntimeMaterialPair pair = catalog.MaterialPairs[i];
                if (pair == null || pair.RuntimeMaterialPairId <= 0 || !materialPairIds.Add(pair.RuntimeMaterialPairId))
                {
                    Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_PAIR_ID_INVALID", pair?.RuntimeMaterialPairId.ToString(CultureInfo.InvariantCulture), "Runtime Pair ID 必须是唯一正整数。 ");
                    continue;
                }
                if (!materialIds.Contains(pair.MaterialARuntimeId) || !materialIds.Contains(pair.MaterialBRuntimeId) || pair.MaterialARuntimeId > pair.MaterialBRuntimeId)
                    Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_PAIR_MATERIAL_INVALID", pair.RuntimeMaterialPairId.ToString(CultureInfo.InvariantCulture), "Runtime Pair 必须引用两个按稳定 ID 规范化的 material。 ");
            }
            for (int i = 0; i < catalog.ResponseProfiles.Count; i++)
            {
                PhysicalAudioRuntimeResponseProfile response = catalog.ResponseProfiles[i];
                if (response == null || response.RuntimeResponseProfileId <= 0 || !responseIds.Add(response.RuntimeResponseProfileId))
                {
                    Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_RESPONSE_ID_INVALID", response?.RuntimeResponseProfileId.ToString(CultureInfo.InvariantCulture), "Runtime response profile ID 必须是唯一正整数。 ");
                    continue;
                }
                response.Curves ??= new List<PhysicalAudioRuntimeResponseCurve>();
                if (response.Curves.Count > MaxResponseParameterCount)
                    Error(report, "AUTH_AUDIO_PHYSICAL_RESPONSE_PARAMETER_LIMIT", response.RuntimeResponseProfileId.ToString(CultureInfo.InvariantCulture), "Runtime response profile 超过参数缓冲区上限。 ");
                var parameterIds = new HashSet<int>();
                for (int c = 0; c < response.Curves.Count; c++)
                {
                    ValidateRuntimeCurve(report, response.RuntimeResponseProfileId.ToString(CultureInfo.InvariantCulture), response.Curves[c]);
                    if (response.Curves[c] != null && response.Curves[c].ParameterId > 0 && !parameterIds.Add(response.Curves[c].ParameterId))
                        Error(report, "AUTH_AUDIO_PHYSICAL_RESPONSE_PARAMETER_DUPLICATE", response.RuntimeResponseProfileId.ToString(CultureInfo.InvariantCulture), "Runtime response profile parameter ID 重复。 ");
                }
            }
            for (int i = 0; i < catalog.Families.Count; i++)
            {
                PhysicalAudioRuntimeFamily family = catalog.Families[i];
                if (family == null || family.RuntimeFamilyId <= 0 || !familyIds.Add(family.RuntimeFamilyId))
                {
                    Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_FAMILY_ID_INVALID", family?.RuntimeFamilyId.ToString(CultureInfo.InvariantCulture), "Runtime family ID 必须是唯一正整数。 ");
                    continue;
                }
                if (!IsRuntimeInteractionKind(family.InteractionKind))
                    Error(report, "AUTH_AUDIO_PHYSICAL_INTERACTION_INVALID", family.RuntimeFamilyId.ToString(CultureInfo.InvariantCulture), "Runtime family interactionKind 无效。 ");
                if (!IsRuntimeInteractionMode(family.InteractionMode))
                    Error(report, "AUTH_AUDIO_PHYSICAL_MODE_INVALID", family.RuntimeFamilyId.ToString(CultureInfo.InvariantCulture), "Runtime family interactionMode 无效。 ");
                if (!IsRuntimeVariantKind(family.VariantKind))
                    Error(report, "AUTH_AUDIO_PHYSICAL_VARIANT_INVALID", family.RuntimeFamilyId.ToString(CultureInfo.InvariantCulture), "Runtime family variantKind 无效。 ");
                if (family.CooldownFrames < 0)
                    Error(report, "AUTH_AUDIO_PHYSICAL_COOLDOWN_INVALID", family.RuntimeFamilyId.ToString(CultureInfo.InvariantCulture), "Runtime family cooldownFrames 不能为负数。 ");
                ValidateWeights(report, family.RuntimeFamilyId.ToString(CultureInfo.InvariantCulture), family.Weights);
            }
            for (int i = 0; i < catalog.BasisSounds.Count; i++)
            {
                PhysicalAudioRuntimeBasisSound basis = catalog.BasisSounds[i];
                if (basis == null || basis.RuntimeBasisId <= 0 || !basisIds.Add(basis.RuntimeBasisId))
                {
                    Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_BASIS_ID_INVALID", basis?.RuntimeBasisId.ToString(CultureInfo.InvariantCulture), "Runtime Basis ID 必须是唯一正整数。 ");
                    continue;
                }
                basisVariantById[basis.RuntimeBasisId] = basis.VariantKind;
                if (basis.RuntimeCueId <= 0)
                    Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_CUE_ID_INVALID", basis.RuntimeBasisId.ToString(CultureInfo.InvariantCulture), "Runtime Basis Cue ID 必须是正整数。 ");
                if (!materialPairIds.Contains(basis.MaterialPairRuntimeId))
                    Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_BASIS_PAIR_MISSING", basis.RuntimeBasisId.ToString(CultureInfo.InvariantCulture), "Runtime Basis 引用了不存在的 Pair。 ");
                if (!IsRuntimeVariantKind(basis.VariantKind))
                    Error(report, "AUTH_AUDIO_PHYSICAL_VARIANT_INVALID", basis.RuntimeBasisId.ToString(CultureInfo.InvariantCulture), "Runtime Basis variantKind 无效。 ");
                ValidateRuntimeBody(report, materialIds, basis.RuntimeBasisId, basis.SideA);
                ValidateRuntimeBody(report, materialIds, basis.RuntimeBasisId, basis.SideB);
                ValidateRange(report, basis.RuntimeBasisId.ToString(CultureInfo.InvariantCulture), "relativeSpeed", basis.RelativeSpeed);
                ValidateRange(report, basis.RuntimeBasisId.ToString(CultureInfo.InvariantCulture), "intensity", basis.Intensity);
                ValidateRange(report, basis.RuntimeBasisId.ToString(CultureInfo.InvariantCulture), "angleDegrees", basis.AngleDegrees);
            }
            for (int i = 0; i < catalog.Families.Count; i++)
            {
                PhysicalAudioRuntimeFamily family = catalog.Families[i];
                if (family == null || family.RuntimeFamilyId <= 0) continue;
                string id = family.RuntimeFamilyId.ToString(CultureInfo.InvariantCulture);
                if (family.FallbackFamilyRuntimeId > 0 && !familyIds.Contains(family.FallbackFamilyRuntimeId))
                    Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_FALLBACK_FAMILY_MISSING", id, "Runtime family fallback 引用了不存在的 family。 ");
                if (family.FallbackBasisRuntimeId > 0
                    && (!basisVariantById.TryGetValue(family.FallbackBasisRuntimeId, out int basisVariant) || basisVariant != family.VariantKind))
                    Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_FALLBACK_BASIS_INVALID", id, "Runtime fallback Basis 缺失或未实现当前 Family Variant。 ");
                if (family.ResponseProfileRuntimeId > 0 && !responseIds.Contains(family.ResponseProfileRuntimeId))
                    Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_RESPONSE_MISSING", id, "Runtime family 引用了不存在的 response profile。 ");
                if (HasRuntimeFallbackCycle(catalog.Families, family.RuntimeFamilyId))
                    Error(report, "AUTH_AUDIO_PHYSICAL_FALLBACK_CYCLE", id, "Runtime family fallback 存在环。 ");
            }
            report.HasErrors = report.Diagnostics.Exists(item => item != null && item.Severity == CharacterAuthoringValidationSeverity.Error);
            return report;
        }

        /// <remarks>AllocByDesign. Workbench trace and parameter list are diagnostic snapshots.</remarks>
    }
}
