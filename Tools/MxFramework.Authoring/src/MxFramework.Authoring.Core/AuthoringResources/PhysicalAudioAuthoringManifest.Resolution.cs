using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public static partial class PhysicalAudioAuthoringManifest
    {
        public static PhysicalAudioResolvedPlaybackAuthoring Resolve(PhysicalAudioAuthoringManifestDocument manifest, PhysicalAudioInteractionFactAuthoring fact)
        {
            manifest = Upgrade(manifest);
            fact = NormalizeFactForRuntime(fact);
            var result = new PhysicalAudioResolvedPlaybackAuthoring();
            if (!string.Equals(fact.Phase, "Started", StringComparison.Ordinal)) { result.Code = "AUTH_AUDIO_PHYSICAL_INTERACTION_INVALID"; return result; }
            List<PhysicalAudioFamilyAuthoring> families = FindFamilyCandidates(manifest, fact.InteractionKind, fact.InteractionMode);
            if (families.Count == 0) { result.Code = "AUTH_AUDIO_PHYSICAL_FAMILY_NOT_FOUND"; return result; }
            AuthoringResolvedCandidate selected = null;
            for (int i = 0; i < families.Count; i++)
            {
                AuthoringResolvedCandidate candidate = ResolveFamilyChain(manifest, families[i], fact, result.Trace);
                if (candidate != null && (selected == null || IsBetter(candidate, selected)))
                    selected = candidate;
            }
            if (selected == null) { result.Code = "AUTH_AUDIO_PHYSICAL_BASIS_NOT_FOUND"; return result; }
            result.Success = true;
            result.Code = "OK";
            result.BasisId = selected.Basis.Id;
            result.AudioCueId = selected.Basis.AudioCueId;
            result.AdoptionSetId = selected.Basis.AdoptionSetId;
            result.Score = selected.Distance;
            result.Trace.FamilyId = selected.Family.Id;
            result.Trace.SelectedBasisId = selected.Basis.Id;
            result.Trace.UsedFallback = selected.UsedFallback;
            PhysicalAudioResponseProfileAuthoring response = FindResponse(manifest, selected.Family.ResponseProfileId, out PhysicalAudioResponseProfileAuthoring value) ? value : null;
            if (response != null && IsActive(response.Lifecycle))
                for (int i = 0; i < response.Curves.Count; i++) result.Parameters.Add(new PhysicalAudioResolvedParameterAuthoring { ParameterId = response.Curves[i].ParameterId, Value = EvaluateCurve(manifest, response.Curves[i], fact) });
            return result;
        }

        /// <remarks>AllocByDesign. Coverage produces a complete matrix for Workbench diagnostics.</remarks>
        public static PhysicalAudioCoverageReport AnalyzeCoverage(PhysicalAudioAuthoringManifestDocument manifest)
        {
            manifest = Upgrade(manifest);
            var result = new PhysicalAudioCoverageReport();
            for (int r = 0; r < manifest.CoverageRequirements.Count; r++)
            {
                PhysicalAudioCoverageRequirementAuthoring requirement = manifest.CoverageRequirements[r];
                if (requirement == null || !requirement.Required) continue;
                for (int p = 0; p < requirement.MaterialPairIds.Count; p++)
                for (int v = 0; v < requirement.VariantKinds.Count; v++)
                {
                    PhysicalAudioMaterialPairAuthoring pair = FindActiveMaterialPair(manifest, requirement.MaterialPairIds[p]);
                    string variantKind = requirement.VariantKinds[v];
                    PhysicalAudioFamilyAuthoring family = FindActiveFamilyForVariant(manifest, variantKind);
                    if (pair == null || family == null)
                    {
                        AddCoverageCell(result, new PhysicalAudioCoverageCell
                        {
                            RequirementId = requirement.Id,
                            FamilyId = family?.Id ?? string.Empty,
                            MaterialPairId = pair?.Id ?? requirement.MaterialPairIds[p],
                            VariantKind = variantKind,
                            Status = "Missing",
                            FailureCode = pair == null ? "AUTH_AUDIO_PHYSICAL_PAIR_MISSING" : "AUTH_AUDIO_PHYSICAL_FAMILY_NOT_FOUND"
                        });
                        continue;
                    }
                    List<PhysicalAudioInteractionFactAuthoring> facts = BuildCoverageFacts(manifest, requirement, pair.MaterialAId, pair.MaterialBId, family, pair.Id, variantKind);
                    var selectedBasisIds = new HashSet<string>(StringComparer.Ordinal);
                    int resolvedCount = 0;
                    int ambiguousCount = 0;
                    int matchCount = 0;
                    string failureCode = string.Empty;
                    for (int f = 0; f < facts.Count; f++)
                    {
                        facts[f].InteractionKind = family.InteractionKind;
                        facts[f].InteractionMode = family.InteractionMode;
                        PhysicalAudioResolvedPlaybackAuthoring resolved = Resolve(manifest, facts[f]);
                        if (!resolved.Success
                            || !string.Equals(resolved.Trace.MaterialPairId, pair.Id, StringComparison.Ordinal)
                            || !string.Equals(resolved.Trace.VariantKind, variantKind, StringComparison.Ordinal))
                        {
                            if (string.IsNullOrEmpty(failureCode))
                                failureCode = resolved.Success ? "AUTH_AUDIO_PHYSICAL_COVERAGE_FALLBACK_ONLY" : resolved.Code;
                            continue;
                        }
                        resolvedCount++;
                        selectedBasisIds.Add(resolved.BasisId);
                        PhysicalAudioBasisSoundAuthoring selectedBasis = FindBasis(manifest, resolved.BasisId);
                        int tiedWinners = selectedBasis == null
                            ? CountAcceptedWinners(resolved)
                            : CountEquivalentBasisWinners(
                                manifest,
                                new AuthoringResolvedCandidate(family, selectedBasis, resolved.Score, resolved.Trace.UsedFallback),
                                facts[f]);
                        if (tiedWinners > 1) ambiguousCount++;
                        if (tiedWinners > matchCount) matchCount = tiedWinners;
                    }
                    string status = resolvedCount < facts.Count ? "Missing" : ambiguousCount > 0 ? "Ambiguous" : "Covered";
                    var cell = new PhysicalAudioCoverageCell
                    {
                        RequirementId = requirement.Id,
                        FamilyId = family.Id,
                        MaterialPairId = pair.Id,
                        VariantKind = variantKind,
                        ImpactorMaterialId = pair.MaterialAId,
                        ReceiverMaterialId = pair.MaterialBId,
                        MatchCount = matchCount,
                        ScenarioCount = facts.Count,
                        ResolvedScenarioCount = resolvedCount,
                        AmbiguousScenarioCount = ambiguousCount,
                        FailureCode = failureCode,
                        Status = status,
                        SelectedBasisIds = new List<string>(selectedBasisIds)
                    };
                    cell.SelectedBasisIds.Sort(StringComparer.Ordinal);
                    AddCoverageCell(result, cell);
                }
            }
            return result;
        }

        private static void AddCoverageCell(PhysicalAudioCoverageReport report, PhysicalAudioCoverageCell cell)
        {
            report.Cells.Add(cell);
            report.RequiredCellCount++;
            if (cell.Status == "Missing") report.MissingCellCount++;
            else if (cell.Status == "Ambiguous") report.AmbiguousCellCount++;
            else report.CoveredCellCount++;
        }

        private static int CountAcceptedWinners(PhysicalAudioResolvedPlaybackAuthoring resolved)
        {
            int count = 0;
            for (int i = 0; i < resolved.Trace.Candidates.Count; i++)
            {
                PhysicalAudioResolveTraceEntry entry = resolved.Trace.Candidates[i];
                if (entry.Accepted && NearlyEqual(entry.Score, resolved.Score))
                    count++;
            }
            return count;
        }
    }
}
