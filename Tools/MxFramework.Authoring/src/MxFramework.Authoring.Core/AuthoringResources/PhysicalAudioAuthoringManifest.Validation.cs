using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public static partial class PhysicalAudioAuthoringManifest
    {
        public static PhysicalAudioValidationReport Validate(PhysicalAudioAuthoringManifestDocument manifest, AudioSemanticManifestDocument semanticManifest)
        {
            manifest = Upgrade(manifest);
            var report = new PhysicalAudioValidationReport();
            var materialIds = new HashSet<string>(StringComparer.Ordinal);
            var materialRuntimeIds = new HashSet<int>();
            var materialPairIds = new HashSet<string>(StringComparer.Ordinal);
            var materialPairRuntimeIds = new HashSet<int>();
            var materialPairKeys = new HashSet<string>(StringComparer.Ordinal);
            var familyIds = new HashSet<string>(StringComparer.Ordinal);
            var familyRuntimeIds = new HashSet<int>();
            var basisIds = new HashSet<string>(StringComparer.Ordinal);
            var basisRuntimeIds = new HashSet<int>();
            var basisCueIds = new HashSet<string>(StringComparer.Ordinal);
            var responseIds = new HashSet<string>(StringComparer.Ordinal);
            var responseRuntimeIds = new HashSet<int>();
            var cueIds = new HashSet<string>(StringComparer.Ordinal);
            if (semanticManifest?.Cues != null)
                for (int i = 0; i < semanticManifest.Cues.Count; i++)
                    if (semanticManifest.Cues[i] != null) cueIds.Add(semanticManifest.Cues[i].AudioCueId ?? string.Empty);

            for (int i = 0; i < manifest.MaterialProfiles.Count; i++)
            {
                PhysicalAudioMaterialProfileAuthoring item = manifest.MaterialProfiles[i];
                if (item == null) continue;
                RequireId(report, item.Id, item.RuntimeMaterialId, materialIds, materialRuntimeIds, "AUTH_AUDIO_PHYSICAL_MATERIAL_ID");
                ValidateLifecycle(report, item.Id, item.Lifecycle);
                Validate01(report, item.Id, "acoustic features", item.Hardness, item.Density, item.Roughness, item.Damping, item.Resonance);
                if (!string.IsNullOrWhiteSpace(item.FallbackMaterialId) && FindMaterial(manifest, item.FallbackMaterialId) == null)
                    Error(report, "AUTH_AUDIO_PHYSICAL_MATERIAL_FALLBACK_MISSING", item.Id, "Material fallback 不存在。");
                if (HasMaterialFallbackCycle(manifest, item.Id))
                    Error(report, "AUTH_AUDIO_PHYSICAL_MATERIAL_FALLBACK_CYCLE", item.Id, "Material fallback 存在环。");
            }
            for (int i = 0; i < manifest.MaterialPairs.Count; i++)
            {
                PhysicalAudioMaterialPairAuthoring item = manifest.MaterialPairs[i];
                if (item == null) continue;
                RequireId(report, item.Id, item.RuntimeMaterialPairId, materialPairIds, materialPairRuntimeIds, "AUTH_AUDIO_PHYSICAL_PAIR_ID");
                ValidateLifecycle(report, item.Id, item.Lifecycle);
                PhysicalAudioMaterialProfileAuthoring materialA = FindMaterial(manifest, item.MaterialAId);
                PhysicalAudioMaterialProfileAuthoring materialB = FindMaterial(manifest, item.MaterialBId);
                if (materialA == null || materialB == null)
                    Error(report, "AUTH_AUDIO_PHYSICAL_PAIR_MATERIAL_MISSING", item.Id, "Pair 引用了不存在的 material。");
                else
                {
                    if (materialA.RuntimeMaterialId > materialB.RuntimeMaterialId)
                        Error(report, "AUTH_AUDIO_PHYSICAL_PAIR_NOT_CANONICAL", item.Id, "Pair 材质必须按稳定 Material ID 规范化排序。");
                    string key = materialA.RuntimeMaterialId.ToString(CultureInfo.InvariantCulture) + ":" + materialB.RuntimeMaterialId.ToString(CultureInfo.InvariantCulture);
                    if (!materialPairKeys.Add(key))
                        Error(report, "AUTH_AUDIO_PHYSICAL_PAIR_DUPLICATE", item.Id, "同一个无序材质对只能存在一个公共 Pair。");
                }
            }
            for (int i = 0; i < manifest.ResponseProfiles.Count; i++)
            {
                PhysicalAudioResponseProfileAuthoring item = manifest.ResponseProfiles[i];
                if (item == null) continue;
                RequireId(report, item.Id, item.RuntimeResponseProfileId, responseIds, responseRuntimeIds, "AUTH_AUDIO_PHYSICAL_RESPONSE_ID");
                ValidateLifecycle(report, item.Id, item.Lifecycle);
                if (IsActive(item.Lifecycle) && item.Curves.Count > MaxResponseParameterCount)
                    Error(report, "AUTH_AUDIO_PHYSICAL_RESPONSE_PARAMETER_LIMIT", item.Id, "Active response profile 最多允许 " + MaxResponseParameterCount.ToString(CultureInfo.InvariantCulture) + " 条曲线，以匹配 CharacterTest AudioIntent 参数缓冲区。 ");
                var parameterIds = new HashSet<int>();
                for (int c = 0; c < item.Curves.Count; c++)
                {
                    ValidateCurve(report, item.Id, item.Curves[c]);
                    if (item.Curves[c] != null && item.Curves[c].ParameterId > 0 && !parameterIds.Add(item.Curves[c].ParameterId))
                        Error(report, "AUTH_AUDIO_PHYSICAL_RESPONSE_PARAMETER_DUPLICATE", item.Id, "Response profile 不允许重复输出同一个 parameter runtime ID。 ");
                }
            }
            for (int i = 0; i < manifest.Families.Count; i++)
            {
                PhysicalAudioFamilyAuthoring item = manifest.Families[i];
                if (item == null) continue;
                RequireId(report, item.Id, item.RuntimeFamilyId, familyIds, familyRuntimeIds, "AUTH_AUDIO_PHYSICAL_FAMILY_ID");
                ValidateLifecycle(report, item.Id, item.Lifecycle);
                if (!InteractionKinds.Contains(item.InteractionKind)) Error(report, "AUTH_AUDIO_PHYSICAL_INTERACTION_INVALID", item.Id, "未知 interactionKind：" + item.InteractionKind);
                if (!InteractionModes.Contains(item.InteractionMode)) Error(report, "AUTH_AUDIO_PHYSICAL_MODE_INVALID", item.Id, "未知 interactionMode：" + item.InteractionMode);
                if (!VariantKinds.Contains(item.VariantKind)) Error(report, "AUTH_AUDIO_PHYSICAL_VARIANT_INVALID", item.Id, "未知 variantKind：" + item.VariantKind);
                if (item.CooldownFrames < 0)
                    Error(report, "AUTH_AUDIO_PHYSICAL_COOLDOWN_INVALID", item.Id, "Family cooldownFrames 不能为负数。 ");
                ValidateWeights(report, item.Id, item.Weights);
                if (!string.IsNullOrWhiteSpace(item.ResponseProfileId))
                {
                    PhysicalAudioResponseProfileAuthoring response = FindResponse(manifest, item.ResponseProfileId, out PhysicalAudioResponseProfileAuthoring foundResponse) ? foundResponse : null;
                    if (response == null)
                        Error(report, "AUTH_AUDIO_PHYSICAL_RESPONSE_MISSING", item.Id, "Family 引用了不存在的 response profile。");
                    else if (IsActive(item.Lifecycle) && !IsActive(response.Lifecycle))
                        Error(report, "AUTH_AUDIO_PHYSICAL_RESPONSE_INACTIVE", item.Id, "Active Family 只能引用 Active response profile。 ");
                }
                if (!string.IsNullOrWhiteSpace(item.FallbackFamilyId))
                {
                    PhysicalAudioFamilyAuthoring fallbackFamily = FindFamily(manifest, item.FallbackFamilyId);
                    if (fallbackFamily == null)
                        Error(report, "AUTH_AUDIO_PHYSICAL_FALLBACK_MISSING", item.Id, "Family fallback 不存在。");
                    else if (IsActive(item.Lifecycle) && !IsActive(fallbackFamily.Lifecycle))
                        Error(report, "AUTH_AUDIO_PHYSICAL_FALLBACK_INACTIVE", item.Id, "Active Family 只能引用 Active fallback family。 ");
                }
                if (HasFallbackCycle(manifest, item.Id)) Error(report, "AUTH_AUDIO_PHYSICAL_FALLBACK_CYCLE", item.Id, "Family fallback 存在环。");
                ValidateRequiredParameterList(report, item);
            }
            for (int i = 0; i < manifest.BasisSounds.Count; i++)
            {
                PhysicalAudioBasisSoundAuthoring item = manifest.BasisSounds[i];
                if (item == null) continue;
                RequireId(report, item.Id, item.RuntimeBasisId, basisIds, basisRuntimeIds, "AUTH_AUDIO_PHYSICAL_BASIS_ID");
                ValidateLifecycle(report, item.Id, item.Lifecycle);
                if (!basisCueIds.Add(item.AudioCueId ?? string.Empty)) Error(report, "AUTH_AUDIO_PHYSICAL_CUE_DUPLICATE", item.Id, "一个 Audio Cue 只能属于一个 Basis Sound。");
                PhysicalAudioFamilyAuthoring basisFamily = string.IsNullOrWhiteSpace(item.FamilyId)
                    ? FindActiveFamilyForVariant(manifest, item.VariantKind)
                    : FindFamily(manifest, item.FamilyId);
                PhysicalAudioMaterialPairAuthoring basisPair = FindMaterialPair(manifest, item.MaterialPairId);
                if (basisFamily == null)
                    Error(report, "AUTH_AUDIO_PHYSICAL_VARIANT_FAMILY_MISSING", item.Id, "没有 Active Family 选择该 Basis 的动作 Variant。");
                else if (IsActive(item.Lifecycle) && !IsActive(basisFamily.Lifecycle))
                    Error(report, "AUTH_AUDIO_PHYSICAL_FAMILY_INACTIVE", item.Id, "Active Basis 只能引用 Active family。 ");
                if (basisPair == null)
                    Error(report, "AUTH_AUDIO_PHYSICAL_PAIR_MISSING", item.Id, "Basis 引用了不存在的公共 Pair。");
                else if (IsActive(item.Lifecycle) && !IsActive(basisPair.Lifecycle))
                    Error(report, "AUTH_AUDIO_PHYSICAL_PAIR_INACTIVE", item.Id, "Active Basis 只能引用 Active Pair。 ");
                if (!VariantKinds.Contains(item.VariantKind))
                    Error(report, "AUTH_AUDIO_PHYSICAL_VARIANT_INVALID", item.Id, "Basis variantKind 无效。");
                else if (basisFamily != null && !string.Equals(item.VariantKind, basisFamily.VariantKind, StringComparison.Ordinal))
                    Error(report, "AUTH_AUDIO_PHYSICAL_BASIS_VARIANT_MISMATCH", item.Id, "Basis Variant 必须与选择它的 Family Variant 一致。");
                if (!cueIds.Contains(item.AudioCueId)) Error(report, "AUTH_AUDIO_PHYSICAL_CUE_MISSING", item.Id, "Basis 引用了不存在的 Audio Cue。");
                else if (IsActive(item.Lifecycle))
                {
                    AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(semanticManifest, item.AudioCueId);
                    if (cue != null && !string.Equals(cue.Lifecycle, AudioCueLifecycles.Active, StringComparison.Ordinal))
                        Error(report, "AUTH_AUDIO_PHYSICAL_CUE_INACTIVE", item.Id, "Active Basis 只能引用 Active Audio Cue。 ");
                }
                ValidateRequiredParameters(report, manifest, semanticManifest, item);
                ValidateBody(report, manifest, item.Id, item.SideA, IsActive(item.Lifecycle));
                ValidateBody(report, manifest, item.Id, item.SideB, IsActive(item.Lifecycle));
                ValidateRange(report, item.Id, "relativeSpeed", item.RelativeSpeed);
                ValidateRange(report, item.Id, "intensity", item.Intensity);
                ValidateRange(report, item.Id, "angleDegrees", item.AngleDegrees);
            }
            for (int i = 0; i < manifest.Families.Count; i++)
            {
                PhysicalAudioFamilyAuthoring family = manifest.Families[i];
                if (family == null || string.IsNullOrWhiteSpace(family.FallbackBasisId)) continue;
                PhysicalAudioBasisSoundAuthoring fallbackBasis = FindBasis(manifest, family.FallbackBasisId);
                if (fallbackBasis == null)
                    Error(report, "AUTH_AUDIO_PHYSICAL_FALLBACK_BASIS_MISSING", family.Id, "Family fallback basis 不存在。");
                else if (!string.Equals(fallbackBasis.VariantKind, family.VariantKind, StringComparison.Ordinal))
                    Error(report, "AUTH_AUDIO_PHYSICAL_FALLBACK_BASIS_VARIANT_MISMATCH", family.Id, "Family fallback basis 必须实现该 Family 的动作 Variant。");
                else if (IsActive(family.Lifecycle) && !IsActive(fallbackBasis.Lifecycle))
                    Error(report, "AUTH_AUDIO_PHYSICAL_FALLBACK_BASIS_INACTIVE", family.Id, "Active Family 只能引用 Active fallback Basis。 ");
            }
            ValidateScenariosAndCoverage(report, manifest);
            ValidateIdAllocation(report, manifest, materialRuntimeIds, materialPairRuntimeIds, familyRuntimeIds, basisRuntimeIds, responseRuntimeIds);
            report.HasErrors = report.Diagnostics.Exists(item => item != null && item.Severity == CharacterAuthoringValidationSeverity.Error);
            return report;
        }

        /// <summary>Requires current FMOD snapshot evidence for every physical parameter declared by an Active Basis family.</summary>
        /// <remarks>LowFreqAlloc. A semantic parameter declaration alone is not evidence that the FMOD Event exposes it.</remarks>
        public static void ValidateSnapshotParameterEvidence(
            PhysicalAudioAuthoringManifestDocument manifest,
            AudioSemanticManifestDocument semanticManifest,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot,
            PhysicalAudioValidationReport report)
        {
            if (manifest == null || report == null)
                return;
            manifest = Upgrade(manifest);
            var checkedParameters = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.BasisSounds.Count; i++)
            {
                PhysicalAudioBasisSoundAuthoring basis = manifest.BasisSounds[i];
                if (basis == null || !IsActive(basis.Lifecycle))
                    continue;
                PhysicalAudioFamilyAuthoring family = FindFamilyForBasis(manifest, basis);
                if (family == null || !IsActive(family.Lifecycle) || family.RequiredParameterIds == null || family.RequiredParameterIds.Count == 0)
                    continue;
                AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(semanticManifest, basis.AudioCueId);
                AudioEventAuthoringDefinition audioEvent = cue == null
                    ? null
                    : AudioAuthoringManifestV2.FindEventDefinition(semanticManifest, cue.EventDefinitionId);
                if (audioEvent == null)
                    continue;
                AuthoringFmodAudioLibraryEvent snapshotEvent = FindSnapshotEvent(snapshot, audioEvent);
                for (int p = 0; p < family.RequiredParameterIds.Count; p++)
                {
                    int parameterId = family.RequiredParameterIds[p];
                    if (parameterId == NativeVolumeParameterId || parameterId == NativePitchParameterId)
                        continue;
                    string checkKey = audioEvent.Id + ":" + parameterId.ToString(CultureInfo.InvariantCulture);
                    if (!checkedParameters.Add(checkKey))
                        continue;
                    AudioParameterAuthoringDefinition parameter = audioEvent.Parameters?.Find(item => item != null && item.RuntimeParameterId == parameterId);
                    if (parameter == null)
                        continue; // ValidateRequiredParameters reports the semantic contract error.
                    bool evidenced = snapshotEvent?.Parameters != null
                        && snapshotEvent.Parameters.Exists(item => item != null && string.Equals(item.Name, parameter.Name, StringComparison.Ordinal));
                    if (!evidenced)
                    {
                        Error(
                            report,
                            "AUTH_AUDIO_PHYSICAL_FMOD_PARAMETER_EVIDENCE_MISSING",
                            basis.Id,
                            "FMOD Snapshot 未证明 Event " + audioEvent.FmodEventPath
                                + " 含必需参数 " + parameter.Name + " (runtime ID "
                                + parameterId.ToString(CultureInfo.InvariantCulture)
                                + ")。Semantic 声明不能替代 FMOD 参数与 automation 证据。");
                    }
                }
            }
            report.HasErrors = report.Diagnostics.Exists(item => item != null
                && item.Severity == CharacterAuthoringValidationSeverity.Error);
        }

        /// <remarks>LowFreqAlloc. Publication builds the immutable runtime DTO graph.</remarks>
    }
}
