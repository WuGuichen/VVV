using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public static partial class PhysicalAudioAuthoringManifest
    {
        public static PhysicalAudioBasisSoundAuthoring PromoteBasis(PhysicalAudioAuthoringManifestDocument manifest, string audioCueId, string adoptionSetId, string familyId, AudioCollisionProfileV1 collisionProfile)
        {
            manifest = Upgrade(manifest);
            PhysicalAudioFamilyAuthoring family = FindFamily(manifest, familyId) ?? FindFamilyForCollision(manifest, collisionProfile);
            var basis = new PhysicalAudioBasisSoundAuthoring
            {
                Id = "audio.basis." + SanitizeId(audioCueId), RuntimeBasisId = AllocateBasisId(manifest),
                DisplayName = audioCueId ?? string.Empty, FamilyId = family?.Id ?? string.Empty, AudioCueId = audioCueId?.Trim() ?? string.Empty,
                AdoptionSetId = adoptionSetId?.Trim() ?? string.Empty, Lifecycle = PhysicalAudioLifecycles.Draft,
                SourceCollisionProfile = AudioGenerationBriefBuilder.CloneCollisionProfile(collisionProfile)
            };
            ApplyCollisionProfile(manifest, basis, collisionProfile);
            basis.VariantKind = family?.VariantKind ?? VariantFromMode(family?.InteractionMode);
            PhysicalAudioMaterialPairAuthoring pair = EnsureMaterialPair(manifest, basis.Impactor.MaterialId, basis.Receiver.MaterialId);
            basis.MaterialPairId = pair?.Id ?? string.Empty;
            bool impactorIsA = MaterialOrder(manifest, basis.Impactor.MaterialId) <= MaterialOrder(manifest, basis.Receiver.MaterialId);
            basis.SideA = CloneBodyAnchor(impactorIsA ? basis.Impactor : basis.Receiver);
            basis.SideB = CloneBodyAnchor(impactorIsA ? basis.Receiver : basis.Impactor);
            return basis;
        }

        public static PhysicalAudioFamilyAuthoring FindFamily(PhysicalAudioAuthoringManifestDocument manifest, string id)
        {
            if (manifest?.Families == null || string.IsNullOrWhiteSpace(id)) return null;
            return manifest.Families.Find(item => item != null && string.Equals(item.Id, id, StringComparison.Ordinal));
        }

        public static PhysicalAudioBasisSoundAuthoring FindBasis(PhysicalAudioAuthoringManifestDocument manifest, string id)
        {
            if (manifest?.BasisSounds == null || string.IsNullOrWhiteSpace(id)) return null;
            return manifest.BasisSounds.Find(item => item != null && string.Equals(item.Id, id, StringComparison.Ordinal));
        }

        private static void NormalizeMaterials(PhysicalAudioAuthoringManifestDocument manifest)
        {
            for (int i = 0; i < manifest.MaterialProfiles.Count; i++) { PhysicalAudioMaterialProfileAuthoring x = manifest.MaterialProfiles[i]; if (x == null) continue; x.Id = Trim(x.Id); x.DisplayName = Trim(x.DisplayName); x.FallbackMaterialId = Trim(x.FallbackMaterialId); x.Lifecycle = NormalizeLifecycle(x.Lifecycle); if (x.RuntimeMaterialId <= 0) x.RuntimeMaterialId = AllocateMaterialId(manifest); manifest.IdAllocation.NextMaterialId = Advance(manifest.IdAllocation.NextMaterialId, x.RuntimeMaterialId); }
        }

        private static void NormalizeIdAllocation(PhysicalAudioIdAllocationState allocation)
        {
            allocation.RetiredMaterialIds ??= new List<int>();
            allocation.RetiredFamilyIds ??= new List<int>();
            allocation.RetiredBasisIds ??= new List<int>();
            allocation.RetiredResponseProfileIds ??= new List<int>();
            allocation.RetiredMaterialPairIds ??= new List<int>();
            allocation.NextMaterialId = Math.Max(510001, AdvancePastRetired(allocation.NextMaterialId, allocation.RetiredMaterialIds));
            allocation.NextFamilyId = Math.Max(520001, AdvancePastRetired(allocation.NextFamilyId, allocation.RetiredFamilyIds));
            allocation.NextBasisId = Math.Max(530001, AdvancePastRetired(allocation.NextBasisId, allocation.RetiredBasisIds));
            allocation.NextResponseProfileId = Math.Max(540001, AdvancePastRetired(allocation.NextResponseProfileId, allocation.RetiredResponseProfileIds));
            allocation.NextMaterialPairId = Math.Max(550001, AdvancePastRetired(allocation.NextMaterialPairId, allocation.RetiredMaterialPairIds));
        }

        private static int AdvancePastRetired(int next, List<int> retired)
        {
            int value = next;
            for (int i = 0; retired != null && i < retired.Count; i++)
                if (retired[i] >= value)
                    value = retired[i] + 1;
            return value;
        }

        private static void RetireMissing<T>(List<T> previous, List<T> current, List<int> retired, Func<T, int> getRuntimeId)
            where T : class
        {
            var live = new HashSet<int>();
            for (int i = 0; current != null && i < current.Count; i++)
                if (current[i] != null)
                    live.Add(getRuntimeId(current[i]));
            for (int i = 0; previous != null && i < previous.Count; i++)
            {
                T item = previous[i];
                if (item == null)
                    continue;
                int runtimeId = getRuntimeId(item);
                if (runtimeId > 0 && !live.Contains(runtimeId) && !retired.Contains(runtimeId))
                    retired.Add(runtimeId);
            }
            retired.Sort();
        }
        private static void NormalizeFamilies(PhysicalAudioAuthoringManifestDocument manifest)
        {
            for (int i = 0; i < manifest.Families.Count; i++) { PhysicalAudioFamilyAuthoring x = manifest.Families[i]; if (x == null) continue; x.Id = Trim(x.Id); x.DisplayName = Trim(x.DisplayName); x.InteractionKind = Trim(x.InteractionKind); x.InteractionMode = Trim(x.InteractionMode); x.VariantKind = Trim(x.VariantKind); if (string.IsNullOrEmpty(x.VariantKind)) x.VariantKind = VariantFromMode(x.InteractionMode); x.FallbackFamilyId = Trim(x.FallbackFamilyId); x.FallbackBasisId = Trim(x.FallbackBasisId); x.ResponseProfileId = Trim(x.ResponseProfileId); x.Weights ??= new PhysicalAudioSelectionWeightsAuthoring(); x.RequiredParameterIds ??= new List<int>(); x.Lifecycle = NormalizeLifecycle(x.Lifecycle); if (x.RuntimeFamilyId <= 0) x.RuntimeFamilyId = AllocateFamilyId(manifest); manifest.IdAllocation.NextFamilyId = Advance(manifest.IdAllocation.NextFamilyId, x.RuntimeFamilyId); }
        }
        private static void NormalizeBasisSounds(PhysicalAudioAuthoringManifestDocument manifest)
        {
            for (int i = 0; i < manifest.BasisSounds.Count; i++) { PhysicalAudioBasisSoundAuthoring x = manifest.BasisSounds[i]; if (x == null) continue; x.Id = Trim(x.Id); x.FamilyId = Trim(x.FamilyId); x.MaterialPairId = Trim(x.MaterialPairId); x.VariantKind = Trim(x.VariantKind); x.AudioCueId = Trim(x.AudioCueId); x.AdoptionSetId = Trim(x.AdoptionSetId); x.Impactor ??= new PhysicalAudioBodyAnchorAuthoring(); x.Receiver ??= new PhysicalAudioBodyAnchorAuthoring(); x.SideA ??= CloneBodyAnchor(x.Impactor); x.SideB ??= CloneBodyAnchor(x.Receiver); x.RelativeSpeed ??= new PhysicalAudioRangeAnchor(); x.Intensity ??= new PhysicalAudioRangeAnchor(); x.AngleDegrees ??= new PhysicalAudioRangeAnchor { Max = 180f, Anchor = 90f }; x.Metadata ??= new Dictionary<string, string>(); x.Lifecycle = NormalizeLifecycle(x.Lifecycle); if (x.RuntimeBasisId <= 0) x.RuntimeBasisId = AllocateBasisId(manifest); manifest.IdAllocation.NextBasisId = Advance(manifest.IdAllocation.NextBasisId, x.RuntimeBasisId); }
        }

        private static void NormalizeMaterialPairs(PhysicalAudioAuthoringManifestDocument manifest)
        {
            for (int i = 0; i < manifest.MaterialPairs.Count; i++)
            {
                PhysicalAudioMaterialPairAuthoring pair = manifest.MaterialPairs[i];
                if (pair == null) continue;
                pair.Id = Trim(pair.Id);
                pair.MaterialAId = Trim(pair.MaterialAId);
                pair.MaterialBId = Trim(pair.MaterialBId);
                string materialAId = pair.MaterialAId;
                string materialBId = pair.MaterialBId;
                CanonicalizeMaterialIds(manifest, ref materialAId, ref materialBId);
                pair.MaterialAId = materialAId;
                pair.MaterialBId = materialBId;
                pair.Metadata ??= new Dictionary<string, string>();
                pair.Lifecycle = NormalizeLifecycle(pair.Lifecycle);
                if (pair.RuntimeMaterialPairId <= 0) pair.RuntimeMaterialPairId = AllocateMaterialPairId(manifest);
                manifest.IdAllocation.NextMaterialPairId = Advance(manifest.IdAllocation.NextMaterialPairId, pair.RuntimeMaterialPairId);
            }
        }

        private static void MigratePairsAndVariants(PhysicalAudioAuthoringManifestDocument manifest, bool legacy)
        {
            if (legacy)
            {
                for (int i = 0; i < manifest.MaterialProfiles.Count; i++)
                {
                    PhysicalAudioMaterialProfileAuthoring material = manifest.MaterialProfiles[i];
                    if (material == null || !string.IsNullOrWhiteSpace(material.FallbackMaterialId)) continue;
                    if (!string.Equals(material.Id, "audio.material.generic_solid", StringComparison.Ordinal)
                        && !string.Equals(material.Id, "audio.material.air", StringComparison.Ordinal))
                        material.FallbackMaterialId = "audio.material.generic_solid";
                }
            }
            for (int i = 0; i < manifest.Families.Count; i++)
            {
                PhysicalAudioFamilyAuthoring family = manifest.Families[i];
                // v1 did not serialize variantKind. The DTO default is Impact for newly
                // authored v2 families, so a deserialized v1 family cannot be detected by
                // checking for an empty value. During the one-way migration the interaction
                // mode is the authority for the new public Pair variant identity.
                if (family != null && (legacy || string.IsNullOrWhiteSpace(family.VariantKind)))
                    family.VariantKind = VariantFromMode(family.InteractionMode);
            }
            for (int i = 0; i < manifest.BasisSounds.Count; i++)
            {
                PhysicalAudioBasisSoundAuthoring basis = manifest.BasisSounds[i];
                if (basis == null) continue;
                PhysicalAudioFamilyAuthoring family = FindFamily(manifest, basis.FamilyId);
                if (legacy || string.IsNullOrWhiteSpace(basis.VariantKind))
                    basis.VariantKind = family?.VariantKind ?? VariantFromMode(family?.InteractionMode);
                if (legacy && string.IsNullOrWhiteSpace(basis.MaterialPairId))
                {
                    PhysicalAudioMaterialPairAuthoring pair = EnsureMaterialPair(manifest, basis.Impactor?.MaterialId, basis.Receiver?.MaterialId);
                    basis.MaterialPairId = pair?.Id ?? string.Empty;
                }
                PhysicalAudioMaterialPairAuthoring currentPair = FindMaterialPair(manifest, basis.MaterialPairId);
                if (currentPair != null)
                {
                    bool impactorIsA = MaterialOrder(manifest, basis.Impactor?.MaterialId) <= MaterialOrder(manifest, basis.Receiver?.MaterialId);
                    basis.SideA = CloneBodyAnchor(impactorIsA ? basis.Impactor : basis.Receiver);
                    basis.SideB = CloneBodyAnchor(impactorIsA ? basis.Receiver : basis.Impactor);
                }
            }
            for (int i = 0; i < manifest.CoverageRequirements.Count; i++)
            {
                PhysicalAudioCoverageRequirementAuthoring requirement = manifest.CoverageRequirements[i];
                if (requirement == null) continue;
                requirement.MaterialPairIds ??= new List<string>();
                requirement.VariantKinds ??= new List<string>();
                requirement.ImpactorMaterialIds ??= new List<string>();
                requirement.ReceiverMaterialIds ??= new List<string>();
                PhysicalAudioFamilyAuthoring family = FindFamily(manifest, requirement.FamilyId);
                if (legacy && requirement.VariantKinds.Count == 0 && family != null)
                    requirement.VariantKinds.Add(family.VariantKind);
                if (legacy && requirement.MaterialPairIds.Count == 0)
                {
                    for (int a = 0; a < requirement.ImpactorMaterialIds.Count; a++)
                    for (int b = 0; b < requirement.ReceiverMaterialIds.Count; b++)
                    {
                        PhysicalAudioMaterialPairAuthoring pair = EnsureMaterialPair(
                            manifest,
                            requirement.ImpactorMaterialIds[a],
                            requirement.ReceiverMaterialIds[b]);
                        if (pair != null && !requirement.MaterialPairIds.Contains(pair.Id))
                            requirement.MaterialPairIds.Add(pair.Id);
                    }
                }
            }
        }
        private static void NormalizeResponses(PhysicalAudioAuthoringManifestDocument manifest)
        {
            for (int i = 0; i < manifest.ResponseProfiles.Count; i++) { PhysicalAudioResponseProfileAuthoring x = manifest.ResponseProfiles[i]; if (x == null) continue; x.Id = Trim(x.Id); x.Curves ??= new List<PhysicalAudioResponseCurveAuthoring>(); x.Lifecycle = NormalizeLifecycle(x.Lifecycle); if (x.RuntimeResponseProfileId <= 0) x.RuntimeResponseProfileId = AllocateResponseProfileId(manifest); manifest.IdAllocation.NextResponseProfileId = Advance(manifest.IdAllocation.NextResponseProfileId, x.RuntimeResponseProfileId); }
        }

        private static void NormalizeScenariosAndCoverage(PhysicalAudioAuthoringManifestDocument manifest)
        {
            for (int i = 0; i < manifest.ScenarioPresets.Count; i++)
            {
                PhysicalAudioScenarioPresetAuthoring preset = manifest.ScenarioPresets[i];
                if (preset == null) continue;
                preset.Id = Trim(preset.Id);
                preset.DisplayName = Trim(preset.DisplayName);
                preset.Fact ??= new PhysicalAudioInteractionFactAuthoring();
                preset.Fact.ContactNormal = NormalizeDirectionForRuntime(preset.Fact.ContactNormal);
            }
            for (int i = 0; i < manifest.CoverageRequirements.Count; i++)
            {
                PhysicalAudioCoverageRequirementAuthoring requirement = manifest.CoverageRequirements[i];
                if (requirement == null) continue;
                requirement.Id = Trim(requirement.Id);
                requirement.FamilyId = Trim(requirement.FamilyId);
                requirement.MaterialPairIds ??= new List<string>();
                requirement.VariantKinds ??= new List<string>();
                requirement.ImpactorMaterialIds ??= new List<string>();
                requirement.ReceiverMaterialIds ??= new List<string>();
                requirement.Facts ??= new List<PhysicalAudioInteractionFactAuthoring>();
                requirement.ScenarioPresetIds ??= new List<string>();
                for (int m = 0; m < requirement.ImpactorMaterialIds.Count; m++)
                    requirement.ImpactorMaterialIds[m] = Trim(requirement.ImpactorMaterialIds[m]);
                for (int m = 0; m < requirement.ReceiverMaterialIds.Count; m++)
                    requirement.ReceiverMaterialIds[m] = Trim(requirement.ReceiverMaterialIds[m]);
                for (int m = 0; m < requirement.MaterialPairIds.Count; m++)
                    requirement.MaterialPairIds[m] = Trim(requirement.MaterialPairIds[m]);
                for (int v = 0; v < requirement.VariantKinds.Count; v++)
                    requirement.VariantKinds[v] = Trim(requirement.VariantKinds[v]);
                for (int p = 0; p < requirement.ScenarioPresetIds.Count; p++)
                    requirement.ScenarioPresetIds[p] = Trim(requirement.ScenarioPresetIds[p]);
                for (int f = 0; f < requirement.Facts.Count; f++)
                {
                    requirement.Facts[f] ??= new PhysicalAudioInteractionFactAuthoring();
                    requirement.Facts[f].ContactNormal = NormalizeDirectionForRuntime(requirement.Facts[f].ContactNormal);
                }
            }
        }
    }
}
