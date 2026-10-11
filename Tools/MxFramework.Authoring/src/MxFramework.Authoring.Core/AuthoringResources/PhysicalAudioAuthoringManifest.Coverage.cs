using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public static partial class PhysicalAudioAuthoringManifest
    {
        private static List<string> BuildCoverageMaterialAxis(
            PhysicalAudioAuthoringManifestDocument manifest,
            PhysicalAudioCoverageRequirementAuthoring requirement,
            bool impactor)
        {
            List<string> configured = impactor ? requirement.ImpactorMaterialIds : requirement.ReceiverMaterialIds;
            if (configured.Count > 0) return configured;
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < requirement.Facts.Count; i++)
            {
                PhysicalAudioBodyFactAuthoring body = impactor ? requirement.Facts[i]?.Impactor : requirement.Facts[i]?.Receiver;
                AddCoverageMaterial(result, seen, body?.MaterialId);
            }
            for (int i = 0; i < requirement.ScenarioPresetIds.Count; i++)
            {
                PhysicalAudioScenarioPresetAuthoring preset = FindScenarioPreset(manifest, requirement.ScenarioPresetIds[i]);
                PhysicalAudioBodyFactAuthoring body = impactor ? preset?.Fact?.Impactor : preset?.Fact?.Receiver;
                AddCoverageMaterial(result, seen, body?.MaterialId);
            }
            if (result.Count == 0) result.Add(string.Empty);
            return result;
        }

        private static void AddCoverageMaterial(List<string> result, HashSet<string> seen, string materialId)
        {
            materialId = materialId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(materialId) && seen.Add(materialId)) result.Add(materialId);
        }

        private static List<PhysicalAudioInteractionFactAuthoring> BuildCoverageFacts(
            PhysicalAudioAuthoringManifestDocument manifest,
            PhysicalAudioCoverageRequirementAuthoring requirement,
            string impactorMaterialId,
            string receiverMaterialId,
            PhysicalAudioFamilyAuthoring preferredFamily = null,
            string materialPairId = "",
            string variantKind = "")
        {
            var facts = new List<PhysicalAudioInteractionFactAuthoring>();
            var factKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < requirement.Facts.Count; i++)
                AddCoverageFact(facts, factKeys, CloneCoverageFact(requirement.Facts[i], impactorMaterialId, receiverMaterialId));
            for (int i = 0; i < requirement.ScenarioPresetIds.Count; i++)
            {
                PhysicalAudioScenarioPresetAuthoring preset = FindScenarioPreset(manifest, requirement.ScenarioPresetIds[i]);
                if (preset != null)
                    AddCoverageFact(facts, factKeys, CloneCoverageFact(preset.Fact, impactorMaterialId, receiverMaterialId));
            }
            if (facts.Count > 0) return facts;

            PhysicalAudioFamilyAuthoring family = preferredFamily ?? FindActiveFamily(manifest, requirement.FamilyId);
            if (family != null)
            {
                for (int i = 0; i < manifest.BasisSounds.Count; i++)
                {
                    PhysicalAudioBasisSoundAuthoring basis = manifest.BasisSounds[i];
                    if (basis == null
                        || !IsActive(basis.Lifecycle)
                        || (!string.IsNullOrEmpty(materialPairId) && !string.Equals(basis.MaterialPairId, materialPairId, StringComparison.Ordinal))
                        || (!string.IsNullOrEmpty(variantKind) && !string.Equals(basis.VariantKind, variantKind, StringComparison.Ordinal))
                        || !CoverageMaterialMatches(basis.SideA.MaterialId, impactorMaterialId)
                        || !CoverageMaterialMatches(basis.SideB.MaterialId, receiverMaterialId))
                        continue;
                    AddAutomaticCoverageProbes(manifest, family, basis, impactorMaterialId, receiverMaterialId, facts, factKeys);
                }
                if (facts.Count == 0)
                {
                    PhysicalAudioBasisSoundAuthoring fallback = FindActiveBasis(manifest, family.FallbackBasisId);
                    if (fallback != null)
                        AddAutomaticCoverageProbes(manifest, family, fallback, impactorMaterialId, receiverMaterialId, facts, factKeys);
                }
                if (facts.Count == 0)
                {
                    string defaultMaterial = FirstActiveMaterialId(manifest);
                    AddCoverageFact(facts, factKeys, new PhysicalAudioInteractionFactAuthoring
                    {
                        InteractionKind = family.InteractionKind,
                        InteractionMode = family.InteractionMode,
                        Phase = "Started",
                        Impactor = new PhysicalAudioBodyFactAuthoring { MaterialId = string.IsNullOrWhiteSpace(impactorMaterialId) ? defaultMaterial : impactorMaterialId },
                        Receiver = new PhysicalAudioBodyFactAuthoring { MaterialId = string.IsNullOrWhiteSpace(receiverMaterialId) ? defaultMaterial : receiverMaterialId },
                        RelativeSpeed = 0f,
                        NormalSpeed = 0f,
                        Intensity = 0f,
                        AngleDegrees = 0f
                    });
                }
            }
            if (facts.Count == 0) facts.Add(new PhysicalAudioInteractionFactAuthoring());
            return facts;
        }

        private static bool CoverageMaterialMatches(string authoredMaterialId, string requiredMaterialId)
        {
            return string.IsNullOrWhiteSpace(requiredMaterialId)
                || string.IsNullOrWhiteSpace(authoredMaterialId)
                || string.Equals(authoredMaterialId, requiredMaterialId, StringComparison.Ordinal);
        }

        private static void AddAutomaticCoverageProbes(
            PhysicalAudioAuthoringManifestDocument manifest,
            PhysicalAudioFamilyAuthoring family,
            PhysicalAudioBasisSoundAuthoring basis,
            string impactorMaterialId,
            string receiverMaterialId,
            List<PhysicalAudioInteractionFactAuthoring> facts,
            HashSet<string> factKeys)
        {
            PhysicalAudioInteractionFactAuthoring anchor = CreateCoverageFact(manifest, family, basis, impactorMaterialId, receiverMaterialId);
            AddCoverageFact(facts, factKeys, anchor);
            AddCoverageAxisProbe(facts, factKeys, anchor, value => value.Impactor.Mass = basis.SideA.Mass.Min);
            AddCoverageAxisProbe(facts, factKeys, anchor, value => value.Impactor.Mass = basis.SideA.Mass.Max);
            AddCoverageAxisProbe(facts, factKeys, anchor, value => value.Impactor.ContactArea = basis.SideA.ContactArea.Min);
            AddCoverageAxisProbe(facts, factKeys, anchor, value => value.Impactor.ContactArea = basis.SideA.ContactArea.Max);
            AddCoverageAxisProbe(facts, factKeys, anchor, value => value.Receiver.Mass = basis.SideB.Mass.Min);
            AddCoverageAxisProbe(facts, factKeys, anchor, value => value.Receiver.Mass = basis.SideB.Mass.Max);
            AddCoverageAxisProbe(facts, factKeys, anchor, value => value.Receiver.ContactArea = basis.SideB.ContactArea.Min);
            AddCoverageAxisProbe(facts, factKeys, anchor, value => value.Receiver.ContactArea = basis.SideB.ContactArea.Max);
            AddCoverageAxisProbe(facts, factKeys, anchor, value => { value.RelativeSpeed = basis.RelativeSpeed.Min; value.NormalSpeed = basis.RelativeSpeed.Min; });
            AddCoverageAxisProbe(facts, factKeys, anchor, value => { value.RelativeSpeed = basis.RelativeSpeed.Max; value.NormalSpeed = basis.RelativeSpeed.Max; });
            AddCoverageAxisProbe(facts, factKeys, anchor, value => value.Intensity = basis.Intensity.Min);
            AddCoverageAxisProbe(facts, factKeys, anchor, value => value.Intensity = basis.Intensity.Max);
            AddCoverageAxisProbe(facts, factKeys, anchor, value => value.AngleDegrees = basis.AngleDegrees.Min);
            AddCoverageAxisProbe(facts, factKeys, anchor, value => value.AngleDegrees = basis.AngleDegrees.Max);
        }

        private static void AddCoverageAxisProbe(
            List<PhysicalAudioInteractionFactAuthoring> facts,
            HashSet<string> factKeys,
            PhysicalAudioInteractionFactAuthoring anchor,
            Action<PhysicalAudioInteractionFactAuthoring> mutate)
        {
            PhysicalAudioInteractionFactAuthoring probe = CloneCoverageFact(anchor, string.Empty, string.Empty);
            mutate(probe);
            AddCoverageFact(facts, factKeys, probe);
        }

        private static PhysicalAudioInteractionFactAuthoring CreateCoverageFact(
            PhysicalAudioAuthoringManifestDocument manifest,
            PhysicalAudioFamilyAuthoring family,
            PhysicalAudioBasisSoundAuthoring basis,
            string impactorMaterialId,
            string receiverMaterialId)
        {
            string defaultMaterial = FirstActiveMaterialId(manifest);
            return new PhysicalAudioInteractionFactAuthoring
            {
                InteractionKind = family.InteractionKind,
                InteractionMode = family.InteractionMode,
                Phase = "Started",
                Impactor = CreateCoverageBody(basis.SideA, impactorMaterialId, defaultMaterial),
                Receiver = CreateCoverageBody(basis.SideB, receiverMaterialId, defaultMaterial),
                RelativeSpeed = basis.RelativeSpeed.Anchor,
                NormalSpeed = basis.RelativeSpeed.Anchor,
                TangentialSpeed = 0f,
                Pressure = basis.Intensity.Anchor,
                Impulse = basis.Intensity.Anchor,
                Intensity = basis.Intensity.Anchor,
                AngleDegrees = basis.AngleDegrees.Anchor,
                ContactNormal = new PhysicalAudioDirectionAuthoring()
            };
        }

        private static PhysicalAudioBodyFactAuthoring CreateCoverageBody(PhysicalAudioBodyAnchorAuthoring anchor, string requiredMaterialId, string defaultMaterialId)
        {
            return new PhysicalAudioBodyFactAuthoring
            {
                MaterialId = !string.IsNullOrWhiteSpace(requiredMaterialId)
                    ? requiredMaterialId
                    : !string.IsNullOrWhiteSpace(anchor.MaterialId) ? anchor.MaterialId : defaultMaterialId,
                Mass = anchor.Mass.Anchor,
                ContactArea = anchor.ContactArea.Anchor,
                Surface = string.Equals(anchor.Surface, "Any", StringComparison.Ordinal) ? "Normal" : anchor.Surface,
                Construction = string.Equals(anchor.Construction, "Any", StringComparison.Ordinal) ? "Solid" : anchor.Construction
            };
        }

        private static PhysicalAudioInteractionFactAuthoring CloneCoverageFact(PhysicalAudioInteractionFactAuthoring source, string impactorMaterialId, string receiverMaterialId)
        {
            source ??= new PhysicalAudioInteractionFactAuthoring();
            var result = new PhysicalAudioInteractionFactAuthoring
            {
                InteractionKind = source.InteractionKind,
                InteractionMode = source.InteractionMode,
                Phase = source.Phase,
                Impactor = CloneCoverageBody(source.Impactor),
                Receiver = CloneCoverageBody(source.Receiver),
                RelativeSpeed = source.RelativeSpeed,
                NormalSpeed = source.NormalSpeed,
                TangentialSpeed = source.TangentialSpeed,
                TangentialRatio = source.TangentialRatio,
                Pressure = source.Pressure,
                Impulse = source.Impulse,
                Intensity = source.Intensity,
                AngleDegrees = source.AngleDegrees,
                ContactNormal = CloneDirection(source.ContactNormal),
                Frame = source.Frame,
                Sequence = source.Sequence
            };
            if (!string.IsNullOrWhiteSpace(impactorMaterialId)) result.Impactor.MaterialId = impactorMaterialId;
            if (!string.IsNullOrWhiteSpace(receiverMaterialId)) result.Receiver.MaterialId = receiverMaterialId;
            return result;
        }

        private static PhysicalAudioBodyFactAuthoring CloneCoverageBody(PhysicalAudioBodyFactAuthoring source)
        {
            source ??= new PhysicalAudioBodyFactAuthoring();
            return new PhysicalAudioBodyFactAuthoring
            {
                MaterialId = source.MaterialId,
                Mass = source.Mass,
                ContactArea = source.ContactArea,
                Surface = source.Surface,
                Construction = source.Construction
            };
        }

        private static void AddCoverageFact(List<PhysicalAudioInteractionFactAuthoring> facts, HashSet<string> keys, PhysicalAudioInteractionFactAuthoring fact)
        {
            if (fact == null) return;
            string key = BuildCoverageFactKey(fact);
            if (keys.Add(key)) facts.Add(fact);
        }

        private static string BuildCoverageFactKey(PhysicalAudioInteractionFactAuthoring fact)
        {
            return string.Join("|", new[]
            {
                fact.InteractionKind ?? string.Empty, fact.InteractionMode ?? string.Empty, fact.Phase ?? string.Empty,
                fact.Impactor?.MaterialId ?? string.Empty, FormatCoverageFloat(fact.Impactor?.Mass ?? 0f), FormatCoverageFloat(fact.Impactor?.ContactArea ?? 0f), fact.Impactor?.Surface ?? string.Empty, fact.Impactor?.Construction ?? string.Empty,
                fact.Receiver?.MaterialId ?? string.Empty, FormatCoverageFloat(fact.Receiver?.Mass ?? 0f), FormatCoverageFloat(fact.Receiver?.ContactArea ?? 0f), fact.Receiver?.Surface ?? string.Empty, fact.Receiver?.Construction ?? string.Empty,
                FormatCoverageFloat(fact.RelativeSpeed), FormatCoverageFloat(fact.NormalSpeed), FormatCoverageFloat(fact.TangentialSpeed), FormatCoverageFloat(fact.Pressure), FormatCoverageFloat(fact.Impulse), FormatCoverageFloat(fact.Intensity), FormatCoverageFloat(fact.AngleDegrees),
                FormatCoverageFloat(fact.ContactNormal?.X ?? 0f), FormatCoverageFloat(fact.ContactNormal?.Y ?? 0f), FormatCoverageFloat(fact.ContactNormal?.Z ?? 0f)
            });
        }

        private static PhysicalAudioDirectionAuthoring CloneDirection(PhysicalAudioDirectionAuthoring source)
        {
            source ??= new PhysicalAudioDirectionAuthoring();
            return new PhysicalAudioDirectionAuthoring { X = source.X, Y = source.Y, Z = source.Z };
        }

        private static string FormatCoverageFloat(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string FirstActiveMaterialId(PhysicalAudioAuthoringManifestDocument manifest)
        {
            for (int i = 0; i < manifest.MaterialProfiles.Count; i++)
                if (manifest.MaterialProfiles[i] != null && IsActive(manifest.MaterialProfiles[i].Lifecycle))
                    return manifest.MaterialProfiles[i].Id;
            return string.Empty;
        }

        private static bool IsFamilyInFallbackChain(PhysicalAudioAuthoringManifestDocument manifest, string requirementFamilyId, string resolvedFamilyId)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            PhysicalAudioFamilyAuthoring family = FindActiveFamily(manifest, requirementFamilyId);
            while (family != null && visited.Add(family.Id))
            {
                if (string.Equals(family.Id, resolvedFamilyId, StringComparison.Ordinal)) return true;
                family = FindActiveFamily(manifest, family.FallbackFamilyId);
            }
            return false;
        }

        private static int CountEquivalentWinners(PhysicalAudioAuthoringManifestDocument manifest, PhysicalAudioInteractionFactAuthoring rawFact)
        {
            PhysicalAudioInteractionFactAuthoring fact = NormalizeFactForRuntime(rawFact);
            List<PhysicalAudioFamilyAuthoring> families = FindFamilyCandidates(manifest, fact.InteractionKind, fact.InteractionMode);
            var candidates = new List<AuthoringResolvedCandidate>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < families.Count; i++)
            {
                var trace = new PhysicalAudioResolveTraceSnapshot();
                AuthoringResolvedCandidate candidate = ResolveFamilyChain(manifest, families[i], fact, trace);
                if (candidate != null && unique.Add(candidate.Family.Id + ":" + candidate.Basis.Id)) candidates.Add(candidate);
            }
            if (candidates.Count == 0) return 0;
            AuthoringResolvedCandidate best = candidates[0];
            for (int i = 1; i < candidates.Count; i++) if (IsBetter(candidates[i], best)) best = candidates[i];
            int count = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                AuthoringResolvedCandidate candidate = candidates[i];
                if (candidate.Family.Priority == best.Family.Priority
                    && candidate.Basis.Priority == best.Basis.Priority
                    && NearlyEqual(candidate.Distance, best.Distance))
                    count += CountEquivalentBasisWinners(manifest, candidate, fact);
            }
            return Math.Max(1, count);
        }

        private static int CountEquivalentBasisWinners(
            PhysicalAudioAuthoringManifestDocument manifest,
            AuthoringResolvedCandidate selected,
            PhysicalAudioInteractionFactAuthoring fact)
        {
            if (selected.UsedFallback && selected.Distance == float.MaxValue) return 1;
            PhysicalAudioMaterialPairAuthoring pair = FindBestPairForFact(
                manifest,
                selected.Basis.VariantKind,
                fact,
                out bool impactorMapsToA,
                out _);
            if (pair == null || !string.Equals(pair.Id, selected.Basis.MaterialPairId, StringComparison.Ordinal)) return 1;
            PhysicalAudioBodyFactAuthoring sideA = impactorMapsToA ? fact.Impactor : fact.Receiver;
            PhysicalAudioBodyFactAuthoring sideB = impactorMapsToA ? fact.Receiver : fact.Impactor;
            PhysicalAudioSelectionWeightsAuthoring weights = BuildRuntimeWeights(selected.Family.Weights);
            int count = 0;
            for (int i = 0; i < manifest.BasisSounds.Count; i++)
            {
                PhysicalAudioBasisSoundAuthoring basis = manifest.BasisSounds[i];
                if (basis == null
                    || !IsActive(basis.Lifecycle)
                    || !string.Equals(basis.MaterialPairId, selected.Basis.MaterialPairId, StringComparison.Ordinal)
                    || !string.Equals(basis.VariantKind, selected.Basis.VariantKind, StringComparison.Ordinal)
                    || basis.Priority != selected.Basis.Priority)
                    continue;
                float distance = ScorePairBasis(weights, basis, sideA, sideB, fact, out _);
                if (distance < float.MaxValue && NearlyEqual(distance, selected.Distance)) count++;
            }
            return Math.Max(1, count);
        }
    }
}
