using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public static partial class PhysicalAudioAuthoringManifest
    {
        private static PhysicalAudioRuntimeBodyAnchor BuildRuntimeBody(PhysicalAudioAuthoringManifestDocument manifest, PhysicalAudioBodyAnchorAuthoring body)
        {
            body ??= new PhysicalAudioBodyAnchorAuthoring();
            return new PhysicalAudioRuntimeBodyAnchor
            {
                MaterialRuntimeId = FindActiveMaterial(manifest, body.MaterialId)?.RuntimeMaterialId ?? 0,
                Mass = CloneRange(body.Mass),
                ContactArea = CloneRange(body.ContactArea),
                Surface = RuntimeSurface(body.Surface),
                Construction = RuntimeConstruction(body.Construction)
            };
        }
        private static PhysicalAudioMaterialProfileAuthoring FindMaterial(PhysicalAudioAuthoringManifestDocument manifest, string id) => manifest?.MaterialProfiles?.Find(item => item != null && string.Equals(item.Id, id, StringComparison.Ordinal));
        private static PhysicalAudioMaterialProfileAuthoring FindActiveMaterial(PhysicalAudioAuthoringManifestDocument manifest, string id) { PhysicalAudioMaterialProfileAuthoring item = FindMaterial(manifest, id); return item != null && IsActive(item.Lifecycle) ? item : null; }
        public static PhysicalAudioMaterialPairAuthoring FindMaterialPair(PhysicalAudioAuthoringManifestDocument manifest, string id) => manifest?.MaterialPairs?.Find(item => item != null && string.Equals(item.Id, id, StringComparison.Ordinal));

        public static PhysicalAudioMaterialPairAuthoring EnsureMaterialPair(
            PhysicalAudioAuthoringManifestDocument manifest,
            string firstMaterialId,
            string secondMaterialId)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(firstMaterialId) || string.IsNullOrWhiteSpace(secondMaterialId))
                return null;
            string materialAId = Trim(firstMaterialId);
            string materialBId = Trim(secondMaterialId);
            CanonicalizeMaterialIds(manifest, ref materialAId, ref materialBId);
            for (int i = 0; i < manifest.MaterialPairs.Count; i++)
            {
                PhysicalAudioMaterialPairAuthoring pair = manifest.MaterialPairs[i];
                if (pair != null
                    && string.Equals(pair.MaterialAId, materialAId, StringComparison.Ordinal)
                    && string.Equals(pair.MaterialBId, materialBId, StringComparison.Ordinal))
                    return pair;
            }
            var created = new PhysicalAudioMaterialPairAuthoring
            {
                Id = "audio.pair." + SanitizeId(MaterialIdToken(materialAId)) + "--" + SanitizeId(MaterialIdToken(materialBId)),
                RuntimeMaterialPairId = AllocateMaterialPairId(manifest),
                MaterialAId = materialAId,
                MaterialBId = materialBId,
                Lifecycle = PhysicalAudioLifecycles.Active
            };
            manifest.MaterialPairs.Add(created);
            return created;
        }

        private static string MaterialIdToken(string value)
        {
            const string prefix = "audio.material.";
            return value != null && value.StartsWith(prefix, StringComparison.Ordinal)
                ? value.Substring(prefix.Length)
                : value ?? string.Empty;
        }

        private static void CanonicalizeMaterialIds(PhysicalAudioAuthoringManifestDocument manifest, ref string materialAId, ref string materialBId)
        {
            int orderA = MaterialOrder(manifest, materialAId);
            int orderB = MaterialOrder(manifest, materialBId);
            if (orderA < orderB || (orderA == orderB && string.CompareOrdinal(materialAId, materialBId) <= 0))
                return;
            string swap = materialAId;
            materialAId = materialBId;
            materialBId = swap;
        }

        private static int MaterialOrder(PhysicalAudioAuthoringManifestDocument manifest, string materialId)
        {
            PhysicalAudioMaterialProfileAuthoring material = FindMaterial(manifest, materialId);
            return material?.RuntimeMaterialId > 0 ? material.RuntimeMaterialId : int.MaxValue;
        }

        private static PhysicalAudioBodyAnchorAuthoring CloneBodyAnchor(PhysicalAudioBodyAnchorAuthoring source)
        {
            source ??= new PhysicalAudioBodyAnchorAuthoring();
            return new PhysicalAudioBodyAnchorAuthoring
            {
                MaterialId = source.MaterialId ?? string.Empty,
                Mass = CloneRange(source.Mass),
                ContactArea = CloneRange(source.ContactArea),
                Surface = source.Surface ?? "Any",
                Construction = source.Construction ?? "Any"
            };
        }

        private static string VariantFromMode(string mode)
        {
            switch (mode)
            {
                case "Step": return "Step";
                case "Landing": return "Landing";
                case "Hit": return "Hit";
                case "Block": return "Block";
                case "Sweep": return "Sweep";
                case "Scrape": return "Scrape";
                case "Roll": return "Roll";
                case "Compression": return "Compression";
                default: return "Impact";
            }
        }
        private static PhysicalAudioFamilyAuthoring FindActiveFamily(PhysicalAudioAuthoringManifestDocument manifest, string id) { PhysicalAudioFamilyAuthoring item = FindFamily(manifest, id); return item != null && IsActive(item.Lifecycle) ? item : null; }
        private static PhysicalAudioFamilyAuthoring FindActiveFamilyForVariant(PhysicalAudioAuthoringManifestDocument manifest, string variantKind)
        {
            PhysicalAudioFamilyAuthoring selected = null;
            for (int i = 0; i < manifest.Families.Count; i++)
            {
                PhysicalAudioFamilyAuthoring family = manifest.Families[i];
                if (family == null || !IsActive(family.Lifecycle) || !string.Equals(family.VariantKind, variantKind, StringComparison.Ordinal))
                    continue;
                if (selected == null || CompareRuntimeFamilyOrder(family, selected) < 0)
                    selected = family;
            }
            return selected;
        }
        private static PhysicalAudioFamilyAuthoring FindFamilyForBasis(PhysicalAudioAuthoringManifestDocument manifest, PhysicalAudioBasisSoundAuthoring basis)
        {
            if (basis == null) return null;
            PhysicalAudioFamilyAuthoring legacyFamily = FindActiveFamily(manifest, basis.FamilyId);
            return legacyFamily != null && string.Equals(legacyFamily.VariantKind, basis.VariantKind, StringComparison.Ordinal)
                ? legacyFamily
                : FindActiveFamilyForVariant(manifest, basis.VariantKind);
        }
        private static PhysicalAudioMaterialPairAuthoring FindActiveMaterialPair(PhysicalAudioAuthoringManifestDocument manifest, string id) { PhysicalAudioMaterialPairAuthoring item = FindMaterialPair(manifest, id); return item != null && IsActive(item.Lifecycle) ? item : null; }
        private static PhysicalAudioBasisSoundAuthoring FindActiveBasis(PhysicalAudioAuthoringManifestDocument manifest, string id) { PhysicalAudioBasisSoundAuthoring item = FindBasis(manifest, id); return item != null && IsActive(item.Lifecycle) ? item : null; }
        private static bool FindResponse(PhysicalAudioAuthoringManifestDocument manifest, string id, out PhysicalAudioResponseProfileAuthoring response) { response = manifest?.ResponseProfiles?.Find(item => item != null && string.Equals(item.Id, id, StringComparison.Ordinal)); return response != null; }
        private static PhysicalAudioResponseProfileAuthoring FindActiveResponse(PhysicalAudioAuthoringManifestDocument manifest, string id) { return FindResponse(manifest, id, out PhysicalAudioResponseProfileAuthoring item) && IsActive(item.Lifecycle) ? item : null; }
        private static bool HasFallbackCycle(PhysicalAudioAuthoringManifestDocument manifest, string start) { var seen = new HashSet<string>(StringComparer.Ordinal); string current = start; while (!string.IsNullOrWhiteSpace(current)) { if (!seen.Add(current)) return true; current = FindFamily(manifest, current)?.FallbackFamilyId; } return false; }
        private static bool HasMaterialFallbackCycle(PhysicalAudioAuthoringManifestDocument manifest, string start) { var seen = new HashSet<string>(StringComparer.Ordinal); string current = start; while (!string.IsNullOrWhiteSpace(current)) { if (!seen.Add(current)) return true; current = FindMaterial(manifest, current)?.FallbackMaterialId; } return false; }
        private static bool IsActive(string value) => string.Equals(value, PhysicalAudioLifecycles.Active, StringComparison.Ordinal);
        private static string NormalizeLifecycle(string value) => string.IsNullOrWhiteSpace(value) ? PhysicalAudioLifecycles.Draft : value.Trim();
        private static string Trim(string value) => value?.Trim() ?? string.Empty;
        private static int AllocateMaterialId(PhysicalAudioAuthoringManifestDocument manifest) { int next = Allocate(manifest.IdAllocation.NextMaterialId, manifest.IdAllocation.RetiredMaterialIds); manifest.IdAllocation.NextMaterialId = next + 1; return next; }
        private static int AllocateFamilyId(PhysicalAudioAuthoringManifestDocument manifest) { int next = Allocate(manifest.IdAllocation.NextFamilyId, manifest.IdAllocation.RetiredFamilyIds); manifest.IdAllocation.NextFamilyId = next + 1; return next; }
        private static int AllocateBasisId(PhysicalAudioAuthoringManifestDocument manifest) { int next = Allocate(manifest.IdAllocation.NextBasisId, manifest.IdAllocation.RetiredBasisIds); manifest.IdAllocation.NextBasisId = next + 1; return next; }
        private static int AllocateResponseProfileId(PhysicalAudioAuthoringManifestDocument manifest) { int next = Allocate(manifest.IdAllocation.NextResponseProfileId, manifest.IdAllocation.RetiredResponseProfileIds); manifest.IdAllocation.NextResponseProfileId = next + 1; return next; }
        private static int AllocateMaterialPairId(PhysicalAudioAuthoringManifestDocument manifest) { int next = Allocate(manifest.IdAllocation.NextMaterialPairId, manifest.IdAllocation.RetiredMaterialPairIds); manifest.IdAllocation.NextMaterialPairId = next + 1; return next; }
        private static int Allocate(int next, List<int> retired) { if (next <= 0) next = 500001; while (retired != null && retired.Contains(next)) next++; return next; }
        private static int Advance(int next, int used) => used >= next ? used + 1 : next;
        private static PhysicalAudioRangeAnchor CloneRange(PhysicalAudioRangeAnchor value) => value == null ? new PhysicalAudioRangeAnchor() : new PhysicalAudioRangeAnchor { Min = value.Min, Max = value.Max, Anchor = value.Anchor };
        private static PhysicalAudioSelectionWeightsAuthoring CloneWeights(PhysicalAudioSelectionWeightsAuthoring x) => x == null ? new PhysicalAudioSelectionWeightsAuthoring() : new PhysicalAudioSelectionWeightsAuthoring { Material = x.Material, Mass = x.Mass, ContactArea = x.ContactArea, Surface = x.Surface, Construction = x.Construction, Speed = x.Speed, Intensity = x.Intensity, Angle = x.Angle };
        private static List<PhysicalAudioRuntimeResponseCurve> BuildRuntimeCurves(List<PhysicalAudioResponseCurveAuthoring> curves)
        {
            var result = new List<PhysicalAudioRuntimeResponseCurve>();
            for (int i = 0; curves != null && i < curves.Count; i++)
            {
                PhysicalAudioResponseCurveAuthoring source = curves[i];
                if (source == null) continue;
                var runtime = new PhysicalAudioRuntimeResponseCurve
                {
                    ParameterId = source.ParameterId,
                    Input = RuntimeResponseInput(source.Input),
                    OutputMin = source.OutputMin,
                    OutputMax = source.OutputMax
                };
                for (int p = 0; source.Points != null && p < source.Points.Count; p++)
                {
                    PhysicalAudioResponsePointAuthoring point = source.Points[p];
                    if (point != null)
                        runtime.Points.Add(new PhysicalAudioResponsePointAuthoring { Input = point.Input, Output = point.Output });
                }
                result.Add(runtime);
            }
            return result;
        }
        private static List<PhysicalAudioFamilyAuthoring> FindFamilyCandidates(PhysicalAudioAuthoringManifestDocument manifest, string kind, string mode)
        {
            var exact = new List<PhysicalAudioFamilyAuthoring>();
            var fallback = new List<PhysicalAudioFamilyAuthoring>();
            for (int i = 0; i < manifest.Families.Count; i++)
            {
                PhysicalAudioFamilyAuthoring item = manifest.Families[i];
                if (item == null || !IsActive(item.Lifecycle) || !string.Equals(item.InteractionKind, kind, StringComparison.Ordinal)) continue;
                if (string.Equals(item.InteractionMode, mode, StringComparison.Ordinal)) exact.Add(item);
                else if (string.Equals(item.InteractionMode, "Any", StringComparison.Ordinal)) fallback.Add(item);
            }
            exact.Sort(CompareRuntimeFamilyOrder);
            fallback.Sort(CompareRuntimeFamilyOrder);
            return exact.Count > 0 ? exact : fallback;
        }

        private static int CompareRuntimeFamilyOrder(PhysicalAudioFamilyAuthoring left, PhysicalAudioFamilyAuthoring right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : left.RuntimeFamilyId.CompareTo(right.RuntimeFamilyId);
        }

        private static AuthoringResolvedCandidate ResolveFamilyChain(
            PhysicalAudioAuthoringManifestDocument manifest,
            PhysicalAudioFamilyAuthoring initialFamily,
            PhysicalAudioInteractionFactAuthoring fact,
            PhysicalAudioResolveTraceSnapshot trace)
        {
            PhysicalAudioFamilyAuthoring family = initialFamily;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            int fallbackDepth = 0;
            while (family != null && visited.Add(family.Id) && visited.Count <= manifest.Families.Count)
            {
                AuthoringResolvedCandidate selected = ResolveFamilyBasis(manifest, family, fact, trace);
                if (selected != null)
                    return fallbackDepth == 0
                        ? selected
                        : new AuthoringResolvedCandidate(selected.Family, selected.Basis, selected.Distance, true);
                PhysicalAudioBasisSoundAuthoring fallbackBasis = FindActiveBasis(manifest, family.FallbackBasisId);
                if (fallbackBasis != null && string.Equals(fallbackBasis.VariantKind, family.VariantKind, StringComparison.Ordinal))
                {
                    trace.Candidates.Add(new PhysicalAudioResolveTraceEntry { BasisId = fallbackBasis.Id, Accepted = true, Score = float.MaxValue, Reason = "forced family fallback basis" });
                    return new AuthoringResolvedCandidate(family, fallbackBasis, float.MaxValue, true);
                }
                family = FindActiveFamily(manifest, family.FallbackFamilyId);
                fallbackDepth++;
            }
            return null;
        }

        private static AuthoringResolvedCandidate ResolveFamilyBasis(
            PhysicalAudioAuthoringManifestDocument manifest,
            PhysicalAudioFamilyAuthoring family,
            PhysicalAudioInteractionFactAuthoring fact,
            PhysicalAudioResolveTraceSnapshot trace)
        {
            PhysicalAudioMaterialPairAuthoring pair = FindBestPairForFact(
                manifest,
                family.VariantKind,
                fact,
                out bool impactorMapsToA,
                out List<string> materialFallbackPath);
            if (pair == null)
                return null;
            trace.MaterialPairId = pair.Id;
            trace.VariantKind = family.VariantKind;
            trace.MaterialFallbackPath = materialFallbackPath;
            PhysicalAudioBasisSoundAuthoring selected = null;
            float selectedDistance = float.MaxValue;
            var basisCandidates = new List<PhysicalAudioBasisSoundAuthoring>();
            for (int i = 0; i < manifest.BasisSounds.Count; i++)
            {
                PhysicalAudioBasisSoundAuthoring basis = manifest.BasisSounds[i];
                if (basis != null
                    && IsActive(basis.Lifecycle)
                    && string.Equals(basis.MaterialPairId, pair.Id, StringComparison.Ordinal)
                    && string.Equals(basis.VariantKind, family.VariantKind, StringComparison.Ordinal))
                    basisCandidates.Add(basis);
            }
            basisCandidates.Sort(CompareRuntimeBasisOrder);
            PhysicalAudioSelectionWeightsAuthoring runtimeWeights = BuildRuntimeWeights(family.Weights);
            for (int i = 0; i < basisCandidates.Count; i++)
            {
                PhysicalAudioBasisSoundAuthoring basis = basisCandidates[i];
                PhysicalAudioBodyFactAuthoring sideA = impactorMapsToA ? fact.Impactor : fact.Receiver;
                PhysicalAudioBodyFactAuthoring sideB = impactorMapsToA ? fact.Receiver : fact.Impactor;
                float distance = ScorePairBasis(runtimeWeights, basis, sideA, sideB, fact, out string reason);
                bool accepted = distance < float.MaxValue;
                trace.Candidates.Add(new PhysicalAudioResolveTraceEntry { BasisId = basis.Id, MaterialPairId = pair.Id, VariantKind = family.VariantKind, Accepted = accepted, Score = accepted ? distance : 0f, Reason = reason });
                if (!accepted) continue;
                if (selected == null
                    || basis.Priority > selected.Priority
                    || (basis.Priority == selected.Priority && distance < selectedDistance)
                    || (basis.Priority == selected.Priority && NearlyEqual(distance, selectedDistance) && basis.RuntimeBasisId < selected.RuntimeBasisId))
                {
                    selected = basis;
                    selectedDistance = distance;
                }
            }
            return selected == null ? null : new AuthoringResolvedCandidate(family, selected, selectedDistance, materialFallbackPath.Count > 2);
        }

        private static PhysicalAudioMaterialPairAuthoring FindBestPairForFact(
            PhysicalAudioAuthoringManifestDocument manifest,
            string variantKind,
            PhysicalAudioInteractionFactAuthoring fact,
            out bool impactorMapsToA,
            out List<string> fallbackPath)
        {
            impactorMapsToA = true;
            fallbackPath = new List<string>();
            List<string> impactorChain = BuildMaterialFallbackChain(manifest, fact.Impactor?.MaterialId);
            List<string> receiverChain = BuildMaterialFallbackChain(manifest, fact.Receiver?.MaterialId);
            PhysicalAudioMaterialPairAuthoring selected = null;
            int selectedTotal = int.MaxValue;
            int selectedMaximum = int.MaxValue;
            bool selectedMapsToA = true;
            int selectedA = 0;
            int selectedB = 0;
            for (int a = 0; a < impactorChain.Count; a++)
            for (int b = 0; b < receiverChain.Count; b++)
            {
                PhysicalAudioMaterialPairAuthoring pair = FindPairByMaterials(manifest, impactorChain[a], receiverChain[b]);
                if (pair == null || !IsActive(pair.Lifecycle) || !HasActivePairVariantBasis(manifest, pair.Id, variantKind))
                    continue;
                int total = a + b;
                int maximum = Math.Max(a, b);
                if (selected == null
                    || total < selectedTotal
                    || (total == selectedTotal && maximum < selectedMaximum)
                    || (total == selectedTotal && maximum == selectedMaximum && pair.RuntimeMaterialPairId < selected.RuntimeMaterialPairId))
                {
                    selected = pair;
                    selectedTotal = total;
                    selectedMaximum = maximum;
                    selectedMapsToA = MaterialOrder(manifest, impactorChain[a]) <= MaterialOrder(manifest, receiverChain[b]);
                    selectedA = a;
                    selectedB = b;
                }
            }
            if (selected != null)
            {
                impactorMapsToA = selectedMapsToA;
                for (int i = 0; i <= selectedA; i++) fallbackPath.Add("impactor:" + impactorChain[i]);
                for (int i = 0; i <= selectedB; i++) fallbackPath.Add("receiver:" + receiverChain[i]);
            }
            return selected;
        }

        private static List<string> BuildMaterialFallbackChain(PhysicalAudioAuthoringManifestDocument manifest, string materialId)
        {
            var result = new List<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string current = Trim(materialId);
            while (!string.IsNullOrEmpty(current) && visited.Add(current) && result.Count <= manifest.MaterialProfiles.Count)
            {
                result.Add(current);
                current = FindActiveMaterial(manifest, current)?.FallbackMaterialId ?? string.Empty;
            }
            return result;
        }

        private static PhysicalAudioMaterialPairAuthoring FindPairByMaterials(
            PhysicalAudioAuthoringManifestDocument manifest,
            string firstMaterialId,
            string secondMaterialId)
        {
            string materialAId = Trim(firstMaterialId);
            string materialBId = Trim(secondMaterialId);
            CanonicalizeMaterialIds(manifest, ref materialAId, ref materialBId);
            return manifest.MaterialPairs.Find(item => item != null
                && string.Equals(item.MaterialAId, materialAId, StringComparison.Ordinal)
                && string.Equals(item.MaterialBId, materialBId, StringComparison.Ordinal));
        }

        private static bool HasActivePairVariantBasis(PhysicalAudioAuthoringManifestDocument manifest, string pairId, string variantKind)
        {
            for (int i = 0; i < manifest.BasisSounds.Count; i++)
            {
                PhysicalAudioBasisSoundAuthoring basis = manifest.BasisSounds[i];
                if (basis != null
                    && IsActive(basis.Lifecycle)
                    && string.Equals(basis.MaterialPairId, pairId, StringComparison.Ordinal)
                    && string.Equals(basis.VariantKind, variantKind, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static float ScorePairBasis(
            PhysicalAudioSelectionWeightsAuthoring weights,
            PhysicalAudioBasisSoundAuthoring basis,
            PhysicalAudioBodyFactAuthoring sideA,
            PhysicalAudioBodyFactAuthoring sideB,
            PhysicalAudioInteractionFactAuthoring fact,
            out string reason)
        {
            float score = 0f;
            if (!ScoreBody(null, basis.SideA, sideA, weights, "sideA", false, ref score, out reason)) return float.MaxValue;
            if (!ScoreBody(null, basis.SideB, sideB, weights, "sideB", false, ref score, out reason)) return float.MaxValue;
            if (!ScoreRange(basis.RelativeSpeed, fact.RelativeSpeed, weights.Speed, ref score)) { reason = "relativeSpeed outside authored range"; return float.MaxValue; }
            if (!ScoreRange(basis.Intensity, fact.Intensity, weights.Intensity, ref score)) { reason = "intensity outside authored range"; return float.MaxValue; }
            if (!ScoreRange(basis.AngleDegrees, fact.AngleDegrees, weights.Angle, ref score)) { reason = "angleDegrees outside authored range"; return float.MaxValue; }
            reason = "matched Pair Variant ranges";
            return score;
        }

        private static int CompareRuntimeBasisOrder(PhysicalAudioBasisSoundAuthoring left, PhysicalAudioBasisSoundAuthoring right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : left.RuntimeBasisId.CompareTo(right.RuntimeBasisId);
        }

        private static bool IsBetter(AuthoringResolvedCandidate candidate, AuthoringResolvedCandidate best)
        {
            if (candidate.Family.Priority != best.Family.Priority) return candidate.Family.Priority > best.Family.Priority;
            if (candidate.Basis.Priority != best.Basis.Priority) return candidate.Basis.Priority > best.Basis.Priority;
            if (!NearlyEqual(candidate.Distance, best.Distance)) return candidate.Distance < best.Distance;
            return candidate.Basis.RuntimeBasisId < best.Basis.RuntimeBasisId;
        }

        private static float ScoreBasis(PhysicalAudioAuthoringManifestDocument manifest, PhysicalAudioFamilyAuthoring family, PhysicalAudioBasisSoundAuthoring basis, PhysicalAudioInteractionFactAuthoring fact, out string reason)
        {
            return ScoreBasis(manifest, BuildRuntimeWeights(family.Weights), basis, fact, out reason);
        }
        private static float ScoreBasis(PhysicalAudioAuthoringManifestDocument manifest, PhysicalAudioSelectionWeightsAuthoring weights, PhysicalAudioBasisSoundAuthoring basis, PhysicalAudioInteractionFactAuthoring fact, out string reason)
        {
            float score = 0f;
            if (!ScoreBody(manifest, basis.Impactor, fact.Impactor, weights, "impactor", true, ref score, out reason)) return float.MaxValue;
            if (!ScoreBody(manifest, basis.Receiver, fact.Receiver, weights, "receiver", true, ref score, out reason)) return float.MaxValue;
            if (!ScoreRange(basis.RelativeSpeed, fact.RelativeSpeed, weights.Speed, ref score)) { reason = "relativeSpeed outside authored range"; return float.MaxValue; }
            if (!ScoreRange(basis.Intensity, fact.Intensity, weights.Intensity, ref score)) { reason = "intensity outside authored range"; return float.MaxValue; }
            if (!ScoreRange(basis.AngleDegrees, fact.AngleDegrees, weights.Angle, ref score)) { reason = "angleDegrees outside authored range"; return float.MaxValue; }
            reason = "matched strict anchors and authored ranges";
            return score;
        }
        private static bool ScoreBody(PhysicalAudioAuthoringManifestDocument manifest, PhysicalAudioBodyAnchorAuthoring anchor, PhysicalAudioBodyFactAuthoring fact, PhysicalAudioSelectionWeightsAuthoring weights, string label, bool checkMaterial, ref float score, out string reason)
        {
            if (anchor == null || fact == null) { reason = label + " body missing"; return false; }
            bool wildcardMaterial = string.IsNullOrWhiteSpace(anchor.MaterialId);
            if (checkMaterial && !wildcardMaterial && !string.Equals(anchor.MaterialId, fact.MaterialId, StringComparison.Ordinal)) { reason = label + " material mismatch"; return false; }
            if (!ScoreRange(anchor.Mass, fact.Mass, weights.Mass, ref score)) { reason = label + " mass outside authored range"; return false; }
            if (!ScoreRange(anchor.ContactArea, fact.ContactArea, weights.ContactArea, ref score)) { reason = label + " contactArea outside authored range"; return false; }
            bool wildcardSurface = string.Equals(anchor.Surface, "Any", StringComparison.Ordinal);
            if (!wildcardSurface && !string.Equals(anchor.Surface, fact.Surface, StringComparison.Ordinal)) { reason = label + " surface mismatch"; return false; }
            bool wildcardConstruction = string.Equals(anchor.Construction, "Any", StringComparison.Ordinal);
            if (!wildcardConstruction && !string.Equals(anchor.Construction, fact.Construction, StringComparison.Ordinal)) { reason = label + " construction mismatch"; return false; }
            score += (wildcardMaterial ? 0.5f : 0f) * weights.Material;
            score += (wildcardSurface ? 0.5f : 0f) * weights.Surface;
            score += (wildcardConstruction ? 0.5f : 0f) * weights.Construction;
            reason = string.Empty;
            return true;
        }
        private static bool ScoreRange(PhysicalAudioRangeAnchor range, float value, float weight, ref float score)
        {
            if (range == null || !IsFinite(value) || value < range.Min || value > range.Max) return false;
            float span = value <= range.Anchor ? range.Anchor - range.Min : range.Max - range.Anchor;
            float distance = span <= SelectionTieEpsilon ? 0f : Math.Abs(value - range.Anchor) / span;
            score += distance * weight;
            return true;
        }
        private static PhysicalAudioSelectionWeightsAuthoring BuildRuntimeWeights(PhysicalAudioSelectionWeightsAuthoring source)
        {
            source ??= new PhysicalAudioSelectionWeightsAuthoring();
            var result = new PhysicalAudioSelectionWeightsAuthoring
            {
                Material = RuntimeWeight(source.Material),
                Mass = RuntimeWeight(source.Mass),
                ContactArea = RuntimeWeight(source.ContactArea),
                Surface = RuntimeWeight(source.Surface),
                Construction = RuntimeWeight(source.Construction),
                Speed = RuntimeWeight(source.Speed),
                Intensity = RuntimeWeight(source.Intensity),
                Angle = RuntimeWeight(source.Angle)
            };
            float total = result.Material + result.Mass + result.ContactArea + result.Surface
                + result.Construction + result.Speed + result.Intensity + result.Angle;
            return total > 0f
                ? result
                : new PhysicalAudioSelectionWeightsAuthoring
                {
                    Material = 4f,
                    Mass = 1f,
                    ContactArea = 1f,
                    Surface = 0.5f,
                    Construction = 0.5f,
                    Speed = 1f,
                    Intensity = 1f,
                    Angle = 0.5f
                };
        }
        private static float RuntimeWeight(float value) => float.IsNaN(value) || value < 0f ? 0f : value;
        private static PhysicalAudioInteractionFactAuthoring NormalizeFactForRuntime(PhysicalAudioInteractionFactAuthoring source)
        {
            source ??= new PhysicalAudioInteractionFactAuthoring();
            float relativeSpeed = RuntimeNonNegative(source.RelativeSpeed);
            float tangentialSpeed = source.TangentialSpeed > 0f
                ? source.TangentialSpeed
                : source.TangentialRatio * source.RelativeSpeed;
            return new PhysicalAudioInteractionFactAuthoring
            {
                InteractionKind = source.InteractionKind ?? string.Empty,
                InteractionMode = source.InteractionMode ?? string.Empty,
                Phase = source.Phase ?? string.Empty,
                Impactor = NormalizeBodyForRuntime(source.Impactor),
                Receiver = NormalizeBodyForRuntime(source.Receiver),
                RelativeSpeed = relativeSpeed,
                NormalSpeed = RuntimeNonNegative(source.NormalSpeed),
                TangentialSpeed = RuntimeNonNegative(tangentialSpeed),
                TangentialRatio = source.TangentialRatio,
                Pressure = RuntimeNonNegative(source.Pressure),
                Impulse = RuntimeNonNegative(source.Impulse),
                Intensity = RuntimeClamp(source.Intensity, 0f, 1f),
                AngleDegrees = RuntimeClamp(source.AngleDegrees, 0f, 180f),
                ContactNormal = NormalizeDirectionForRuntime(source.ContactNormal),
                Frame = source.Frame,
                Sequence = source.Sequence
            };
        }
        private static PhysicalAudioDirectionAuthoring NormalizeDirectionForRuntime(PhysicalAudioDirectionAuthoring source)
        {
            source ??= new PhysicalAudioDirectionAuthoring();
            float lengthSquared = (source.X * source.X) + (source.Y * source.Y) + (source.Z * source.Z);
            if (float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared) || lengthSquared <= 0.000000000001f)
                return new PhysicalAudioDirectionAuthoring();
            float inverseLength = 1f / (float)Math.Sqrt(lengthSquared);
            return new PhysicalAudioDirectionAuthoring
            {
                X = source.X * inverseLength,
                Y = source.Y * inverseLength,
                Z = source.Z * inverseLength
            };
        }
        private static PhysicalAudioBodyFactAuthoring NormalizeBodyForRuntime(PhysicalAudioBodyFactAuthoring source)
        {
            source ??= new PhysicalAudioBodyFactAuthoring();
            return new PhysicalAudioBodyFactAuthoring
            {
                MaterialId = source.MaterialId ?? string.Empty,
                Mass = source.Mass < 0f ? 0f : source.Mass,
                ContactArea = source.ContactArea < 0f ? 0f : source.ContactArea,
                Surface = source.Surface ?? string.Empty,
                Construction = source.Construction ?? string.Empty
            };
        }
        private static float RuntimeNonNegative(float value) => value < 0f || float.IsNaN(value) ? 0f : value;
        private static float RuntimeClamp(float value, float min, float max)
        {
            if (float.IsNaN(value) || value <= min) return min;
            return value >= max ? max : value;
        }
        private static bool NearlyEqual(float left, float right)
        {
            float delta = left - right;
            return delta < 0f ? -delta <= SelectionTieEpsilon : delta <= SelectionTieEpsilon;
        }
        private static float EvaluateCurve(PhysicalAudioAuthoringManifestDocument manifest, PhysicalAudioResponseCurveAuthoring curve, PhysicalAudioInteractionFactAuthoring fact) { float input = ReadInput(manifest, curve.Input, fact); if (curve.Points == null || curve.Points.Count == 0) return Clamp(input, curve.OutputMin, curve.OutputMax); if (input <= curve.Points[0].Input) return Clamp(curve.Points[0].Output, curve.OutputMin, curve.OutputMax); for (int i = 1; i < curve.Points.Count; i++) { if (input > curve.Points[i].Input) continue; float width = curve.Points[i].Input - curve.Points[i - 1].Input; float t = width <= 0f ? 0f : (input - curve.Points[i - 1].Input) / width; return Clamp(curve.Points[i - 1].Output + (curve.Points[i].Output - curve.Points[i - 1].Output) * t, curve.OutputMin, curve.OutputMax); } return Clamp(curve.Points[curve.Points.Count - 1].Output, curve.OutputMin, curve.OutputMax); }
        private static float ReadInput(PhysicalAudioAuthoringManifestDocument manifest, string input, PhysicalAudioInteractionFactAuthoring fact)
        {
            switch (input)
            {
                case "EffectiveEnergy":
                    float impactorMass = Math.Max(0f, fact.Impactor.Mass);
                    float receiverMass = Math.Max(0f, fact.Receiver.Mass);
                    float reducedMass = receiverMass > 0f ? (impactorMass * receiverMass) / (impactorMass + receiverMass) : impactorMass;
                    return 0.5f * reducedMass * fact.RelativeSpeed * fact.RelativeSpeed;
                case "RelativeSpeed": return fact.RelativeSpeed;
                case "NormalSpeed": return fact.NormalSpeed;
                case "TangentialRatio":
                    float sum = fact.NormalSpeed + fact.TangentialSpeed;
                    return sum <= SelectionTieEpsilon ? 0f : fact.TangentialSpeed / sum;
                case "Pressure": return fact.Pressure;
                case "Impulse": return fact.Impulse;
                case "AngleDegrees": return fact.AngleDegrees;
                case "ImpactorHardness": return FindActiveMaterial(manifest, fact.Impactor.MaterialId)?.Hardness ?? 0f;
                case "ReceiverHardness": return FindActiveMaterial(manifest, fact.Receiver.MaterialId)?.Hardness ?? 0f;
                case "CombinedDamping": return ((FindActiveMaterial(manifest, fact.Impactor.MaterialId)?.Damping ?? 0f) + (FindActiveMaterial(manifest, fact.Receiver.MaterialId)?.Damping ?? 0f)) * 0.5f;
                default: return fact.Intensity;
            }
        }
        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
