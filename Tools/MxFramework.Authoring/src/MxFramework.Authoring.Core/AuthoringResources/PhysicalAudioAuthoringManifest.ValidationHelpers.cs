using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public static partial class PhysicalAudioAuthoringManifest
    {
        private static void ValidateRequiredParameters(
            PhysicalAudioValidationReport report,
            PhysicalAudioAuthoringManifestDocument manifest,
            AudioSemanticManifestDocument semanticManifest,
            PhysicalAudioBasisSoundAuthoring basis)
        {
            PhysicalAudioFamilyAuthoring family = FindFamilyForBasis(manifest, basis);
            AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(semanticManifest, basis.AudioCueId);
            AudioEventAuthoringDefinition audioEvent = cue == null
                ? null
                : AudioAuthoringManifestV2.FindEventDefinition(semanticManifest, cue.EventDefinitionId);
            if (family?.RequiredParameterIds == null || audioEvent?.Parameters == null)
                return;
            for (int r = 0; r < family.RequiredParameterIds.Count; r++)
            {
                int requiredId = family.RequiredParameterIds[r];
                if (requiredId == NativeVolumeParameterId || requiredId == NativePitchParameterId)
                    continue;
                bool found = false;
                for (int p = 0; p < audioEvent.Parameters.Count; p++)
                {
                    if (audioEvent.Parameters[p] != null && audioEvent.Parameters[p].RuntimeParameterId == requiredId)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    Error(report, "AUTH_AUDIO_PHYSICAL_REQUIRED_PARAMETER_MISSING", basis.Id, "Basis Event 缺少必需 FMOD parameter runtime ID：" + requiredId.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void ValidateIdAllocation(
            PhysicalAudioValidationReport report,
            PhysicalAudioAuthoringManifestDocument manifest,
            HashSet<int> materialRuntimeIds,
            HashSet<int> materialPairRuntimeIds,
            HashSet<int> familyRuntimeIds,
            HashSet<int> basisRuntimeIds,
            HashSet<int> responseRuntimeIds)
        {
            ValidateTypedIds(report, "material", materialRuntimeIds, manifest.IdAllocation.RetiredMaterialIds, manifest.IdAllocation.NextMaterialId, 510000, 519999);
            ValidateTypedIds(report, "family", familyRuntimeIds, manifest.IdAllocation.RetiredFamilyIds, manifest.IdAllocation.NextFamilyId, 520000, 529999);
            ValidateTypedIds(report, "basis", basisRuntimeIds, manifest.IdAllocation.RetiredBasisIds, manifest.IdAllocation.NextBasisId, 530000, 539999);
            ValidateTypedIds(report, "response", responseRuntimeIds, manifest.IdAllocation.RetiredResponseProfileIds, manifest.IdAllocation.NextResponseProfileId, 540000, 549999);
            ValidateTypedIds(report, "materialPair", materialPairRuntimeIds, manifest.IdAllocation.RetiredMaterialPairIds, manifest.IdAllocation.NextMaterialPairId, 550000, 559999);
        }

        private static void ValidateTypedIds(
            PhysicalAudioValidationReport report,
            string kind,
            HashSet<int> live,
            List<int> retired,
            int next,
            int minimum,
            int maximum)
        {
            var retiredSet = new HashSet<int>();
            int greatest = minimum - 1;
            foreach (int runtimeId in live)
            {
                greatest = Math.Max(greatest, runtimeId);
                if (runtimeId < minimum || runtimeId > maximum)
                    Error(report, "AUTH_AUDIO_PHYSICAL_TYPED_ID_RANGE_INVALID", kind, kind + " runtime ID 不在独立类型区间内：" + runtimeId.ToString(CultureInfo.InvariantCulture));
            }
            for (int i = 0; retired != null && i < retired.Count; i++)
            {
                int runtimeId = retired[i];
                greatest = Math.Max(greatest, runtimeId);
                if (!retiredSet.Add(runtimeId))
                    Error(report, "AUTH_AUDIO_PHYSICAL_RETIRED_ID_DUPLICATE", kind, kind + " retired runtime ID 重复：" + runtimeId.ToString(CultureInfo.InvariantCulture));
                if (live.Contains(runtimeId))
                    Error(report, "AUTH_AUDIO_PHYSICAL_RETIRED_ID_REUSED", kind, kind + " runtime ID 已退休，不允许重新使用：" + runtimeId.ToString(CultureInfo.InvariantCulture));
                if (runtimeId < minimum || runtimeId > maximum)
                    Error(report, "AUTH_AUDIO_PHYSICAL_TYPED_ID_RANGE_INVALID", kind, kind + " retired runtime ID 不在独立类型区间内：" + runtimeId.ToString(CultureInfo.InvariantCulture));
            }
            if (next <= greatest || next < minimum || next > maximum)
                Error(report, "AUTH_AUDIO_PHYSICAL_NEXT_ID_INVALID", kind, kind + " next runtime ID 必须严格大于所有已用及退休 ID，并保持在类型区间内。");
        }

        private static AuthoringFmodAudioLibraryEvent FindSnapshotEvent(
            AuthoringFmodAudioLibrarySnapshotDocument snapshot,
            AudioEventAuthoringDefinition audioEvent)
        {
            if (snapshot?.Events == null || audioEvent == null)
                return null;
            if (!string.IsNullOrWhiteSpace(audioEvent.FmodEventGuid))
            {
                AuthoringFmodAudioLibraryEvent byGuid = snapshot.Events.Find(item => item != null
                    && string.Equals(item.Guid, audioEvent.FmodEventGuid, StringComparison.OrdinalIgnoreCase));
                if (byGuid != null)
                    return byGuid;
            }
            return snapshot.Events.Find(item => item != null
                && string.Equals(item.Path, audioEvent.FmodEventPath, StringComparison.Ordinal));
        }

        private static void ValidateBody(PhysicalAudioValidationReport report, PhysicalAudioAuthoringManifestDocument manifest, string id, PhysicalAudioBodyAnchorAuthoring body, bool requireActiveMaterial)
        {
            if (body == null) { Error(report, "AUTH_AUDIO_PHYSICAL_BODY_MISSING", id, "Basis 缺少双方声学锚点。"); return; }
            if (!string.IsNullOrWhiteSpace(body.MaterialId))
            {
                PhysicalAudioMaterialProfileAuthoring material = FindMaterial(manifest, body.MaterialId);
                if (material == null)
                    Error(report, "AUTH_AUDIO_PHYSICAL_MATERIAL_MISSING", id, "Basis 引用了不存在的声学材质：" + body.MaterialId);
                else if (requireActiveMaterial && !IsActive(material.Lifecycle))
                    Error(report, "AUTH_AUDIO_PHYSICAL_MATERIAL_INACTIVE", id, "Active Basis 只能引用 Active 声学材质：" + body.MaterialId);
            }
            if (!Surfaces.Contains(body.Surface)) Error(report, "AUTH_AUDIO_PHYSICAL_SURFACE_INVALID", id, "未知 surface：" + body.Surface);
            if (!Constructions.Contains(body.Construction)) Error(report, "AUTH_AUDIO_PHYSICAL_CONSTRUCTION_INVALID", id, "未知 construction：" + body.Construction);
            ValidateRange(report, id, "mass", body.Mass); ValidateRange(report, id, "contactArea", body.ContactArea);
        }
        private static void ValidateCurve(PhysicalAudioValidationReport report, string id, PhysicalAudioResponseCurveAuthoring curve)
        {
            if (curve == null || curve.ParameterId <= 0) { Error(report, "AUTH_AUDIO_PHYSICAL_CURVE_INVALID", id, "Response curve 缺少 parameterId。"); return; }
            if (!ResponseInputs.Contains(curve.Input)) Error(report, "AUTH_AUDIO_PHYSICAL_CURVE_INPUT_INVALID", id, "未知 response input：" + curve.Input);
            if (!IsFinite(curve.OutputMin) || !IsFinite(curve.OutputMax) || curve.OutputMax < curve.OutputMin) Error(report, "AUTH_AUDIO_PHYSICAL_CURVE_RANGE_INVALID", id, "Response curve 输出范围必须是有限数且 min <= max。");
            if (curve.Points == null || curve.Points.Count == 0) Error(report, "AUTH_AUDIO_PHYSICAL_CURVE_EMPTY", id, "Response curve 至少需要一个点。");
            float last = float.MinValue;
            for (int i = 0; curve.Points != null && i < curve.Points.Count; i++)
            {
                PhysicalAudioResponsePointAuthoring point = curve.Points[i];
                if (point == null || !IsFinite(point.Input) || !IsFinite(point.Output))
                {
                    Error(report, "AUTH_AUDIO_PHYSICAL_CURVE_POINT_INVALID", id, "Response curve 点必须包含有限 input/output。 ");
                    continue;
                }
                if (i > 0 && point.Input <= last) Error(report, "AUTH_AUDIO_PHYSICAL_CURVE_ORDER_INVALID", id, "Response curve 输入点必须严格递增。");
                last = point.Input;
            }
        }
        private static void ValidateRuntimeCurve(PhysicalAudioValidationReport report, string id, PhysicalAudioRuntimeResponseCurve curve)
        {
            if (curve == null || curve.ParameterId <= 0)
            {
                Error(report, "AUTH_AUDIO_PHYSICAL_CURVE_INVALID", id, "Runtime response curve 缺少 parameterId。 ");
                return;
            }
            if (!IsRuntimeResponseInput(curve.Input))
                Error(report, "AUTH_AUDIO_PHYSICAL_CURVE_INPUT_INVALID", id, "Runtime response input 数字枚举无效：" + curve.Input.ToString(CultureInfo.InvariantCulture));
            if (!IsFinite(curve.OutputMin) || !IsFinite(curve.OutputMax) || curve.OutputMax < curve.OutputMin)
                Error(report, "AUTH_AUDIO_PHYSICAL_CURVE_RANGE_INVALID", id, "Runtime response curve 输出范围必须是有限数且 min <= max。 ");
            if (curve.Points == null || curve.Points.Count == 0)
                Error(report, "AUTH_AUDIO_PHYSICAL_CURVE_EMPTY", id, "Runtime response curve 至少需要一个点。 ");
            float last = float.MinValue;
            for (int i = 0; curve.Points != null && i < curve.Points.Count; i++)
            {
                PhysicalAudioResponsePointAuthoring point = curve.Points[i];
                if (point == null || !IsFinite(point.Input) || !IsFinite(point.Output))
                {
                    Error(report, "AUTH_AUDIO_PHYSICAL_CURVE_POINT_INVALID", id, "Runtime response curve 点必须包含有限 input/output。 ");
                    continue;
                }
                if (i > 0 && point.Input <= last)
                    Error(report, "AUTH_AUDIO_PHYSICAL_CURVE_ORDER_INVALID", id, "Runtime response curve 输入点必须严格递增。 ");
                last = point.Input;
            }
        }
        private static void ValidateRange(PhysicalAudioValidationReport report, string id, string field, PhysicalAudioRangeAnchor range)
        {
            if (range == null
                || !IsFinite(range.Min)
                || !IsFinite(range.Max)
                || !IsFinite(range.Anchor)
                || range.Max < range.Min
                || range.Anchor < range.Min
                || range.Anchor > range.Max)
                Error(report, "AUTH_AUDIO_PHYSICAL_RANGE_INVALID", id, field + " 的 min/anchor/max 必须是有限数并满足 min <= anchor <= max。");
        }
        private static void Validate01(PhysicalAudioValidationReport report, string id, string field, params float[] values) { for (int i = 0; i < values.Length; i++) if (!IsFinite(values[i]) || values[i] < 0f || values[i] > 1f) { Error(report, "AUTH_AUDIO_PHYSICAL_NORMALIZED_VALUE_INVALID", id, field + " 必须是 0..1 的有限数。"); return; } }
        private static void ValidateLifecycle(PhysicalAudioValidationReport report, string id, string lifecycle)
        {
            if (!Lifecycles.Contains(lifecycle ?? string.Empty))
                Error(report, "AUTH_AUDIO_PHYSICAL_LIFECYCLE_INVALID", id, "Lifecycle 必须是 Draft、Active 或 Archived。 ");
        }
        private static void ValidateWeights(PhysicalAudioValidationReport report, string id, PhysicalAudioSelectionWeightsAuthoring weights)
        {
            if (weights == null)
            {
                Error(report, "AUTH_AUDIO_PHYSICAL_WEIGHTS_INVALID", id, "Family selection weights 缺失。 ");
                return;
            }
            float[] values = { weights.Material, weights.Mass, weights.ContactArea, weights.Surface, weights.Construction, weights.Speed, weights.Intensity, weights.Angle };
            float total = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                if (!IsFinite(values[i]) || values[i] < 0f)
                {
                    Error(report, "AUTH_AUDIO_PHYSICAL_WEIGHTS_INVALID", id, "Family selection weights 必须是有限且非负的数值。 ");
                    return;
                }
                total += values[i];
            }
            if (!IsFinite(total) || total <= 0f)
                Error(report, "AUTH_AUDIO_PHYSICAL_WEIGHTS_TOTAL_INVALID", id, "Family selection weights 总和必须是有限正数，不能依赖 Runtime 隐式默认值。 ");
        }
        private static void ValidateRequiredParameterList(PhysicalAudioValidationReport report, PhysicalAudioFamilyAuthoring family)
        {
            var ids = new HashSet<int>();
            for (int i = 0; family.RequiredParameterIds != null && i < family.RequiredParameterIds.Count; i++)
            {
                int parameterId = family.RequiredParameterIds[i];
                if (parameterId <= 0 || !ids.Add(parameterId))
                    Error(report, "AUTH_AUDIO_PHYSICAL_REQUIRED_PARAMETER_INVALID", family.Id, "requiredParameterIds 必须是唯一的正整数 runtime ID。 ");
            }
        }
        private static void ValidateScenariosAndCoverage(PhysicalAudioValidationReport report, PhysicalAudioAuthoringManifestDocument manifest)
        {
            var presetIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.ScenarioPresets.Count; i++)
            {
                PhysicalAudioScenarioPresetAuthoring preset = manifest.ScenarioPresets[i];
                if (preset == null) continue;
                if (string.IsNullOrWhiteSpace(preset.Id) || !presetIds.Add(preset.Id))
                    Error(report, "AUTH_AUDIO_PHYSICAL_SCENARIO_ID_INVALID", preset.Id, "Scenario preset stable ID 必填且不可重复。 ");
                ValidateFact(report, manifest, preset.Id, preset.Fact, null, false);
            }

            var requirementIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.CoverageRequirements.Count; i++)
            {
                PhysicalAudioCoverageRequirementAuthoring requirement = manifest.CoverageRequirements[i];
                if (requirement == null) continue;
                if (string.IsNullOrWhiteSpace(requirement.Id) || !requirementIds.Add(requirement.Id))
                    Error(report, "AUTH_AUDIO_PHYSICAL_COVERAGE_ID_INVALID", requirement.Id, "Coverage requirement stable ID 必填且不可重复。 ");
                if (requirement.Required && (requirement.MaterialPairIds.Count == 0 || requirement.VariantKinds.Count == 0))
                    Error(report, "AUTH_AUDIO_PHYSICAL_REQUIRED_COVERAGE_EMPTY", requirement.Id, "Required coverage 必须声明公共 Pair 与动作 Variant，空契约不可视为覆盖。 ");
                var pairIds = new HashSet<string>(StringComparer.Ordinal);
                for (int p = 0; p < requirement.MaterialPairIds.Count; p++)
                {
                    string pairId = requirement.MaterialPairIds[p];
                    PhysicalAudioMaterialPairAuthoring pair = FindMaterialPair(manifest, pairId);
                    if (!pairIds.Add(pairId) || pair == null)
                        Error(report, "AUTH_AUDIO_PHYSICAL_COVERAGE_PAIR_INVALID", requirement.Id, "Coverage requirement Pair 必须存在且不可重复：" + pairId);
                    else if (requirement.Required && !IsActive(pair.Lifecycle))
                        Error(report, "AUTH_AUDIO_PHYSICAL_COVERAGE_PAIR_INACTIVE", requirement.Id, "Required coverage 只能引用 Active Pair：" + pairId);
                }
                var variants = new HashSet<string>(StringComparer.Ordinal);
                for (int v = 0; v < requirement.VariantKinds.Count; v++)
                {
                    string variant = requirement.VariantKinds[v];
                    if (!variants.Add(variant) || !VariantKinds.Contains(variant))
                        Error(report, "AUTH_AUDIO_PHYSICAL_COVERAGE_VARIANT_INVALID", requirement.Id, "Coverage requirement Variant 必须有效且不可重复：" + variant);
                }
                PhysicalAudioFamilyAuthoring family = requirement.VariantKinds.Count > 0
                    ? FindActiveFamilyForVariant(manifest, requirement.VariantKinds[0])
                    : null;
                for (int f = 0; f < requirement.Facts.Count; f++)
                    ValidateFact(report, manifest, requirement.Id, requirement.Facts[f], family, requirement.Required);
                for (int p = 0; p < requirement.ScenarioPresetIds.Count; p++)
                {
                    string presetId = requirement.ScenarioPresetIds[p];
                    PhysicalAudioScenarioPresetAuthoring preset = FindScenarioPreset(manifest, presetId);
                    if (preset == null)
                        Error(report, "AUTH_AUDIO_PHYSICAL_COVERAGE_SCENARIO_MISSING", requirement.Id, "Coverage requirement 引用了不存在的 scenario preset：" + presetId);
                    else
                        ValidateFact(report, manifest, requirement.Id, preset.Fact, family, requirement.Required);
                }
            }
        }

        private static void ValidateCoverageMaterials(
            PhysicalAudioValidationReport report,
            PhysicalAudioAuthoringManifestDocument manifest,
            string requirementId,
            List<string> materialIds,
            bool requireActive)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; materialIds != null && i < materialIds.Count; i++)
            {
                string materialId = materialIds[i] ?? string.Empty;
                PhysicalAudioMaterialProfileAuthoring material = FindMaterial(manifest, materialId);
                if (string.IsNullOrWhiteSpace(materialId) || material == null)
                    Error(report, "AUTH_AUDIO_PHYSICAL_COVERAGE_MATERIAL_MISSING", requirementId, "Coverage requirement 引用了不存在的材质：" + materialId);
                else if (!unique.Add(materialId))
                    Error(report, "AUTH_AUDIO_PHYSICAL_COVERAGE_MATERIAL_DUPLICATE", requirementId, "Coverage requirement 材质列表不可重复：" + materialId);
                else if (requireActive && !IsActive(material.Lifecycle))
                    Error(report, "AUTH_AUDIO_PHYSICAL_COVERAGE_MATERIAL_INACTIVE", requirementId, "Required coverage 只能引用 Active 材质：" + materialId);
            }
        }

        private static void ValidateFact(
            PhysicalAudioValidationReport report,
            PhysicalAudioAuthoringManifestDocument manifest,
            string resourceId,
            PhysicalAudioInteractionFactAuthoring fact,
            PhysicalAudioFamilyAuthoring expectedFamily,
            bool requireActiveMaterials)
        {
            if (fact == null)
            {
                Error(report, "AUTH_AUDIO_PHYSICAL_FACT_MISSING", resourceId, "Physical audio scenario fact 缺失。 ");
                return;
            }
            if (!InteractionKinds.Contains(fact.InteractionKind))
                Error(report, "AUTH_AUDIO_PHYSICAL_FACT_INTERACTION_INVALID", resourceId, "Scenario fact interactionKind 无效：" + fact.InteractionKind);
            if (!InteractionModes.Contains(fact.InteractionMode))
                Error(report, "AUTH_AUDIO_PHYSICAL_FACT_MODE_INVALID", resourceId, "Scenario fact interactionMode 无效：" + fact.InteractionMode);
            if (!InteractionPhases.Contains(fact.Phase))
                Error(report, "AUTH_AUDIO_PHYSICAL_FACT_PHASE_INVALID", resourceId, "Scenario fact phase 无效：" + fact.Phase);
            if (expectedFamily != null
                && (!string.Equals(fact.InteractionKind, expectedFamily.InteractionKind, StringComparison.Ordinal)
                    || (!string.Equals(fact.InteractionMode, expectedFamily.InteractionMode, StringComparison.Ordinal)
                        && !string.Equals(expectedFamily.InteractionMode, "Any", StringComparison.Ordinal))))
                Error(report, "AUTH_AUDIO_PHYSICAL_COVERAGE_FACT_FAMILY_MISMATCH", resourceId, "Coverage fact 的 interaction kind/mode 与 family 不一致。 ");
            ValidateFactBody(report, manifest, resourceId, fact.Impactor, requireActiveMaterials);
            ValidateFactBody(report, manifest, resourceId, fact.Receiver, requireActiveMaterials);
            float[] values = { fact.RelativeSpeed, fact.NormalSpeed, fact.TangentialSpeed, fact.TangentialRatio, fact.Pressure, fact.Impulse, fact.Intensity, fact.AngleDegrees };
            for (int i = 0; i < values.Length; i++)
                if (!IsFinite(values[i]))
                {
                    Error(report, "AUTH_AUDIO_PHYSICAL_FACT_VALUE_INVALID", resourceId, "Scenario fact 数值必须是有限数。 ");
                    break;
                }
        }

        private static void ValidateFactBody(
            PhysicalAudioValidationReport report,
            PhysicalAudioAuthoringManifestDocument manifest,
            string resourceId,
            PhysicalAudioBodyFactAuthoring body,
            bool requireActiveMaterial)
        {
            if (body == null)
            {
                Error(report, "AUTH_AUDIO_PHYSICAL_FACT_BODY_MISSING", resourceId, "Scenario fact 缺少 impactor/receiver body。 ");
                return;
            }
            PhysicalAudioMaterialProfileAuthoring material = FindMaterial(manifest, body.MaterialId);
            if (material == null)
                Error(report, "AUTH_AUDIO_PHYSICAL_FACT_MATERIAL_MISSING", resourceId, "Scenario fact 引用了不存在的材质：" + body.MaterialId);
            else if (requireActiveMaterial && !IsActive(material.Lifecycle))
                Error(report, "AUTH_AUDIO_PHYSICAL_FACT_MATERIAL_INACTIVE", resourceId, "Required coverage fact 只能引用 Active 材质：" + body.MaterialId);
            if (!IsFinite(body.Mass) || !IsFinite(body.ContactArea))
                Error(report, "AUTH_AUDIO_PHYSICAL_FACT_BODY_VALUE_INVALID", resourceId, "Scenario body mass/contactArea 必须是有限数。 ");
            if (!Surfaces.Contains(body.Surface))
                Error(report, "AUTH_AUDIO_PHYSICAL_FACT_SURFACE_INVALID", resourceId, "Scenario body surface 无效：" + body.Surface);
            if (!Constructions.Contains(body.Construction))
                Error(report, "AUTH_AUDIO_PHYSICAL_FACT_CONSTRUCTION_INVALID", resourceId, "Scenario body construction 无效：" + body.Construction);
        }

        private static void ValidateRuntimeBody(
            PhysicalAudioValidationReport report,
            HashSet<int> materialIds,
            int basisId,
            PhysicalAudioRuntimeBodyAnchor body)
        {
            string id = basisId.ToString(CultureInfo.InvariantCulture);
            if (body == null)
            {
                Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_BODY_MISSING", id, "Runtime Basis 缺少 body anchor。 ");
                return;
            }
            if (body.MaterialRuntimeId > 0 && !materialIds.Contains(body.MaterialRuntimeId))
                Error(report, "AUTH_AUDIO_PHYSICAL_RUNTIME_MATERIAL_MISSING", id, "Runtime Basis 引用了不存在的 material。 ");
            if (!IsRuntimeSurface(body.Surface))
                Error(report, "AUTH_AUDIO_PHYSICAL_SURFACE_INVALID", id, "Runtime Basis surface 无效。 ");
            if (!IsRuntimeConstruction(body.Construction))
                Error(report, "AUTH_AUDIO_PHYSICAL_CONSTRUCTION_INVALID", id, "Runtime Basis construction 无效。 ");
            ValidateRange(report, id, "mass", body.Mass);
            ValidateRange(report, id, "contactArea", body.ContactArea);
        }

        private static bool HasRuntimeFallbackCycle(List<PhysicalAudioRuntimeFamily> families, int startRuntimeId)
        {
            var seen = new HashSet<int>();
            int current = startRuntimeId;
            while (current > 0)
            {
                if (!seen.Add(current)) return true;
                PhysicalAudioRuntimeFamily family = families.Find(item => item != null && item.RuntimeFamilyId == current);
                if (family == null) return false;
                current = family.FallbackFamilyRuntimeId;
            }
            return false;
        }

        private static PhysicalAudioScenarioPresetAuthoring FindScenarioPreset(PhysicalAudioAuthoringManifestDocument manifest, string id)
        {
            return manifest?.ScenarioPresets?.Find(item => item != null && string.Equals(item.Id, id, StringComparison.Ordinal));
        }
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsRuntimeInteractionKind(int value) => value >= 1 && value <= 9;
        private static bool IsRuntimeInteractionMode(int value) => value >= 0 && value <= 9;
        private static bool IsRuntimeVariantKind(int value) => value >= 1 && value <= 9;
        private static bool IsRuntimeSurface(int value) => value >= 0 && value <= 5;
        private static bool IsRuntimeConstruction(int value) => value >= 0 && value <= 5;
        private static bool IsRuntimeResponseInput(int value) => value >= 0 && value <= 10;
        private static int RuntimeInteractionKind(string value)
        {
            switch (value)
            {
                case "Footstep": return 1;
                case "Landing": return 2;
                case "ObjectImpact": return 3;
                case "WeaponHit": return 4;
                case "WeaponBlock": return 5;
                case "AirDisplacement": return 6;
                case "Scrape": return 7;
                case "Roll": return 8;
                case "Compression": return 9;
                default: throw new InvalidOperationException("Unknown physical audio interaction kind: " + (value ?? string.Empty) + ".");
            }
        }
        private static int RuntimeInteractionMode(string value)
        {
            switch (value)
            {
                case "Any": return 0;
                case "Step": return 1;
                case "Landing": return 2;
                case "Impact": return 3;
                case "Hit": return 4;
                case "Block": return 5;
                case "Sweep": return 6;
                case "Scrape": return 7;
                case "Roll": return 8;
                case "Compression": return 9;
                default: throw new InvalidOperationException("Unknown physical audio interaction mode: " + (value ?? string.Empty) + ".");
            }
        }
        private static int RuntimeVariantKind(string value)
        {
            switch (value)
            {
                case "Impact": return 1;
                case "Step": return 2;
                case "Landing": return 3;
                case "Hit": return 4;
                case "Block": return 5;
                case "Sweep": return 6;
                case "Scrape": return 7;
                case "Roll": return 8;
                case "Compression": return 9;
                default: throw new InvalidOperationException("Unknown physical audio variant kind: " + (value ?? string.Empty) + ".");
            }
        }
        private static int RuntimeSurface(string value)
        {
            switch (value)
            {
                case "Any": return 0;
                case "Polished": return 1;
                case "Smooth": return 2;
                case "Normal": return 3;
                case "Rough": return 4;
                case "Granular": return 5;
                default: throw new InvalidOperationException("Unknown physical audio surface: " + (value ?? string.Empty) + ".");
            }
        }
        private static int RuntimeConstruction(string value)
        {
            switch (value)
            {
                case "Any": return 0;
                case "Solid": return 1;
                case "Hollow": return 2;
                case "ThinShell": return 3;
                case "SoftFilled": return 4;
                case "Gas": return 5;
                default: throw new InvalidOperationException("Unknown physical audio construction: " + (value ?? string.Empty) + ".");
            }
        }
        private static int RuntimeResponseInput(string value)
        {
            switch (value)
            {
                case "EffectiveEnergy": return 0;
                case "RelativeSpeed": return 1;
                case "NormalSpeed": return 2;
                case "TangentialRatio": return 3;
                case "Pressure": return 4;
                case "Impulse": return 5;
                case "Intensity": return 6;
                case "AngleDegrees": return 7;
                case "ImpactorHardness": return 8;
                case "ReceiverHardness": return 9;
                case "CombinedDamping": return 10;
                default: throw new InvalidOperationException("Unknown physical audio response input: " + (value ?? string.Empty) + ".");
            }
        }
        private static void RequireId(PhysicalAudioValidationReport report, string id, int runtimeId, HashSet<string> ids, HashSet<int> runtimeIds, string code) { if (string.IsNullOrWhiteSpace(id) || runtimeId <= 0) Error(report, code, id, "稳定 ID 与 runtime ID 均为必填。"); else { if (!ids.Add(id)) Error(report, code + "_DUPLICATE", id, "稳定 ID 重复。"); if (!runtimeIds.Add(runtimeId)) Error(report, code + "_RUNTIME_DUPLICATE", id, "runtime ID 重复。"); } }
        private static void Error(PhysicalAudioValidationReport report, string code, string resourceId, string message) => report.Diagnostics.Add(new AuthoringResourceDiagnostic { Severity = CharacterAuthoringValidationSeverity.Error, Code = code, ResourceId = resourceId ?? string.Empty, ResourceStableId = resourceId ?? string.Empty, SourceConfigKind = PhysicalAudioAuthoringFormats.Authoring, Message = message, SuggestedFix = "在 Audio Workbench 的基础音效配置中修正后重新校验。" });
    }
}
