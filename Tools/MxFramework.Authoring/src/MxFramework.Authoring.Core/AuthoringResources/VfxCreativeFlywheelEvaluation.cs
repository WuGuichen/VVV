using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    /// <summary>Aggregates fixed-profile hard gates and three-reviewer advisory scores.</summary>
    public static class VfxVisualEvaluationService
    {
        /// <summary>Creates a deterministic evaluation report without approving or mutating any candidate.</summary>
        /// <remarks>AllocByDesign. Sorts evidence, computes medians/Pareto frontier, and hashes the report.</remarks>
        public static VfxEvaluationReport Evaluate(VfxCandidateEvaluationInputDocument input, VfxGenerationPlan plan, VfxCandidateGenerationReceipt receipt, VfxQualityProfile profile)
        {
            var report = new VfxEvaluationReport
            {
                GenerationPlanHash = plan != null ? plan.PlanHash : string.Empty,
                CandidateReceiptHash = receipt != null ? receipt.ReceiptHash : string.Empty,
                QualityProfileId = profile != null ? profile.QualityProfileId : string.Empty,
                QualityProfileVersion = profile != null ? profile.Version : string.Empty,
                Round = plan != null ? plan.Round : 0
            };
            if (input == null || plan == null || receipt == null || profile == null)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Evaluation inputs are incomplete.", "Provide input, immutable plan/receipt, and versioned quality profile.", "evaluation");
                return Finish(report);
            }
            if (!string.Equals(input.SchemaVersion, VfxCreativeFlywheelSchemaVersions.EvaluationInputV1, StringComparison.Ordinal) ||
                !string.Equals(input.GenerationPlanHash, plan.PlanHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(input.CandidateReceiptHash, receipt.ReceiptHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(input.QualityProfileId, profile.QualityProfileId, StringComparison.Ordinal) ||
                !string.Equals(input.QualityProfileVersion, profile.Version, StringComparison.Ordinal))
                Add(report, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.PlanTampered, "Evaluation input is stale or bound to different plan/receipt/profile evidence.", "Regenerate captures and scores from the exact immutable inputs.", "evaluation/inputHashes");

            var planned = new Dictionary<string, VfxGenerationCandidateSpec>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Candidates.Count; i++) if (plan.Candidates[i] != null) planned[plan.Candidates[i].CandidateId] = plan.Candidates[i];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < input.Candidates.Count; i++)
            {
                VfxCandidateEvaluationInput candidate = input.Candidates[i];
                if (candidate == null || !planned.TryGetValue(candidate.CandidateId, out VfxGenerationCandidateSpec candidateSpec) || !seen.Add(candidate.CandidateId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.CandidateInvalid, "Evaluation references an unknown/duplicate candidate.", "Evaluate each planned candidate once.", "candidates/" + i);
                    continue;
                }
                report.Candidates.Add(EvaluateCandidate(candidate, candidateSpec, profile, report));
            }
            foreach (string candidateId in planned.Keys)
                if (!seen.Contains(candidateId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.CandidateInvalid, "Planned candidate has no evaluation input: " + candidateId, "Capture and evaluate every generated candidate.", "candidates");

            report.Candidates.Sort((left, right) => string.Compare(left.CandidateId, right.CandidateId, StringComparison.Ordinal));
            BuildParetoFrontier(report);
            BuildEvolutionState(report, input, profile);
            return Finish(report);
        }

        private static VfxCandidateEvaluationResult EvaluateCandidate(VfxCandidateEvaluationInput input, VfxGenerationCandidateSpec candidateSpec, VfxQualityProfile profile, VfxEvaluationReport report)
        {
            var result = new VfxCandidateEvaluationResult
            {
                CandidateId = input.CandidateId,
                CostTier = candidateSpec != null ? candidateSpec.CostTier : string.Empty,
                CpuP95Milliseconds = input.CpuP95Milliseconds,
                GpuP95Milliseconds = input.GpuP95Milliseconds
            };
            if (input.HardGates != null)
                for (int i = 0; i < input.HardGates.Count; i++)
                {
                    VfxHardGateResult gate = input.HardGates[i];
                    if (gate != null && !string.Equals(gate.GateId, VfxHardGateIds.PerformanceProfilePassed, StringComparison.Ordinal)) result.HardGates.Add(gate);
                }
            result.HardGates.Add(BuildPerformanceGate(input, candidateSpec, profile));
            result.HardGates.Sort((left, right) => string.Compare(left != null ? left.GateId : string.Empty, right != null ? right.GateId : string.Empty, StringComparison.Ordinal));
            result.HardGatesPassed = ValidateHardGates(result, profile);
            bool reviewersValid = TryAggregateReviewers(input.ReviewerScores, result.Score, result.Reasons);
            bool dimensionsPass = result.Score.BriefMatch >= profile.MinimumDimensionScore && result.Score.Readability >= profile.MinimumDimensionScore &&
                                  result.Score.Timing >= profile.MinimumDimensionScore && result.Score.SpatialMotion >= profile.MinimumDimensionScore &&
                                  result.Score.MaterialAndColor >= profile.MinimumDimensionScore && result.Score.Polish >= profile.MinimumDimensionScore;
            result.Shortlisted = result.HardGatesPassed && reviewersValid && dimensionsPass && result.Score.Total >= profile.MinimumTotalScore && result.Score.ReviewerSpread <= profile.MaximumReviewerSpread;
            if (!dimensionsPass) result.Reasons.Add("one or more visual dimensions are below " + profile.MinimumDimensionScore);
            if (result.Score.Total < profile.MinimumTotalScore) result.Reasons.Add("total score is below " + profile.MinimumTotalScore);
            if (result.Score.ReviewerSpread > profile.MaximumReviewerSpread) result.Reasons.Add("reviewer spread exceeds " + profile.MaximumReviewerSpread);
            if (!result.Shortlisted)
                Add(report, CharacterAuthoringValidationSeverity.Warning, VfxFlywheelDiagnosticCodes.EvaluationBlocked, "Candidate is not shortlisted: " + input.CandidateId, "Inspect failed gates/scores and iterate or reject it.", "candidates/" + input.CandidateId);
            return result;
        }

        private static VfxHardGateResult BuildPerformanceGate(VfxCandidateEvaluationInput input, VfxGenerationCandidateSpec candidateSpec, VfxQualityProfile profile)
        {
            string costTier = candidateSpec != null ? candidateSpec.CostTier : string.Empty;
            VfxPerformanceTierBudget budget = FindPerformanceBudget(profile.PerformanceTierBudgets, costTier);
            bool passed = budget != null && input.CpuP95Milliseconds >= 0f && input.GpuP95Milliseconds >= 0f && input.CpuP95Milliseconds <= budget.CpuP95Milliseconds && input.GpuP95Milliseconds <= budget.GpuP95Milliseconds;
            string evidence = string.Join("|", profile.QualityProfileId, profile.Version, costTier,
                input.CpuP95Milliseconds.ToString("R", CultureInfo.InvariantCulture), input.GpuP95Milliseconds.ToString("R", CultureInfo.InvariantCulture),
                budget != null ? budget.CpuP95Milliseconds.ToString("R", CultureInfo.InvariantCulture) : "missing",
                budget != null ? budget.GpuP95Milliseconds.ToString("R", CultureInfo.InvariantCulture) : "missing");
            return new VfxHardGateResult
            {
                GateId = VfxHardGateIds.PerformanceProfilePassed,
                Passed = passed,
                EvidenceHash = VfxCreativeFlywheelHash.ComputeText(evidence),
                Message = passed ? "Measured CPU/GPU P95 is within the versioned " + costTier + " tier budget." : "Measured CPU/GPU P95 exceeds or lacks the versioned " + costTier + " tier budget."
            };
        }

        private static VfxPerformanceTierBudget FindPerformanceBudget(List<VfxPerformanceTierBudget> budgets, string costTier)
        {
            if (budgets == null) return null;
            for (int i = 0; i < budgets.Count; i++)
                if (budgets[i] != null && string.Equals(budgets[i].CostTier, costTier, StringComparison.Ordinal)) return budgets[i];
            return null;
        }

        private static bool ValidateHardGates(VfxCandidateEvaluationResult result, VfxQualityProfile profile)
        {
            var gates = new Dictionary<string, VfxHardGateResult>(StringComparer.Ordinal);
            for (int i = 0; i < result.HardGates.Count; i++)
            {
                VfxHardGateResult gate = result.HardGates[i];
                if (gate == null || string.IsNullOrWhiteSpace(gate.GateId) || gates.ContainsKey(gate.GateId))
                {
                    result.Reasons.Add("hard gate identity is missing or duplicated");
                    return false;
                }
                gates.Add(gate.GateId, gate);
            }
            bool passed = true;
            for (int i = 0; i < profile.RequiredHardGateIds.Count; i++)
            {
                string id = profile.RequiredHardGateIds[i];
                if (!gates.TryGetValue(id, out VfxHardGateResult gate) || !gate.Passed || !VfxCreativeFlywheelHash.IsSha256(gate.EvidenceHash))
                {
                    passed = false;
                    result.Reasons.Add("required hard gate failed/missing/stale: " + id);
                }
            }
            return passed;
        }

        private static bool TryAggregateReviewers(List<VfxReviewerScore> reviewers, VfxVisualScoreSummary score, List<string> reasons)
        {
            if (reviewers == null || reviewers.Count != 3)
            {
                reasons.Add("exactly three reviewer scores are required");
                return false;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var totals = new List<int>(3);
            var brief = new List<int>(3);
            var readability = new List<int>(3);
            var timing = new List<int>(3);
            var motion = new List<int>(3);
            var material = new List<int>(3);
            var polish = new List<int>(3);
            for (int i = 0; i < reviewers.Count; i++)
            {
                VfxReviewerScore reviewer = reviewers[i];
                if (reviewer == null || string.IsNullOrWhiteSpace(reviewer.ReviewerId) || !ids.Add(reviewer.ReviewerId) || !AllScoresValid(reviewer))
                {
                    reasons.Add("reviewer ids must be unique and all dimension scores must be 0..100");
                    return false;
                }
                brief.Add(reviewer.BriefMatch);
                readability.Add(reviewer.Readability);
                timing.Add(reviewer.Timing);
                motion.Add(reviewer.SpatialMotion);
                material.Add(reviewer.MaterialAndColor);
                polish.Add(reviewer.Polish);
                totals.Add(WeightedTotal(reviewer.BriefMatch, reviewer.Readability, reviewer.Timing, reviewer.SpatialMotion, reviewer.MaterialAndColor, reviewer.Polish));
            }
            score.BriefMatch = Median(brief);
            score.Readability = Median(readability);
            score.Timing = Median(timing);
            score.SpatialMotion = Median(motion);
            score.MaterialAndColor = Median(material);
            score.Polish = Median(polish);
            score.Total = WeightedTotal(score.BriefMatch, score.Readability, score.Timing, score.SpatialMotion, score.MaterialAndColor, score.Polish);
            totals.Sort();
            score.ReviewerSpread = totals[2] - totals[0];
            return true;
        }

        private static void BuildParetoFrontier(VfxEvaluationReport report)
        {
            for (int i = 0; i < report.Candidates.Count; i++)
            {
                VfxCandidateEvaluationResult candidate = report.Candidates[i];
                if (!candidate.Shortlisted) continue;
                bool dominated = false;
                for (int j = 0; j < report.Candidates.Count; j++)
                {
                    if (i == j || !report.Candidates[j].Shortlisted) continue;
                    VfxCandidateEvaluationResult other = report.Candidates[j];
                    bool noWorse = other.Score.Total >= candidate.Score.Total && other.CpuP95Milliseconds <= candidate.CpuP95Milliseconds && other.GpuP95Milliseconds <= candidate.GpuP95Milliseconds;
                    bool better = other.Score.Total > candidate.Score.Total || other.CpuP95Milliseconds < candidate.CpuP95Milliseconds || other.GpuP95Milliseconds < candidate.GpuP95Milliseconds;
                    if (noWorse && better) { dominated = true; break; }
                }
                if (!dominated) report.ParetoFrontierCandidateIds.Add(candidate.CandidateId);
            }
            report.ParetoFrontierCandidateIds.Sort(StringComparer.Ordinal);
        }

        private static void BuildEvolutionState(VfxEvaluationReport report, VfxCandidateEvaluationInputDocument input, VfxQualityProfile profile)
        {
            var scores = new List<int>();
            for (int i = 0; i < report.Candidates.Count; i++) if (report.Candidates[i].Shortlisted) scores.Add(report.Candidates[i].Score.Total);
            if (scores.Count > 0)
            {
                scores.Sort();
                report.MedianShortlistScore = scores.Count % 2 == 1 ? scores[scores.Count / 2] : (scores[scores.Count / 2 - 1] + scores[scores.Count / 2]) / 2;
            }
            report.ImprovementFromPreviousRound = input.PreviousMedianScore > 0 ? report.MedianShortlistScore - input.PreviousMedianScore : report.MedianShortlistScore;
            report.ConsecutiveLowImprovementRounds = report.Round > 0 && report.ImprovementFromPreviousRound < profile.MinimumRoundImprovement ? input.ConsecutiveLowImprovementRounds + 1 : 0;
            report.StopEvolution = report.Round + 1 >= profile.MaximumRounds || report.ConsecutiveLowImprovementRounds >= 2;
            if (report.StopEvolution)
            {
                List<string> commonFailures = CommonGateFailures(report.Candidates);
                for (int i = 0; i < commonFailures.Count; i++) report.PrimitiveBriefs.Add("Shared candidate capability gap: " + commonFailures[i] + ". Create or improve a single-responsibility Primitive.");
            }
        }

        private static List<string> CommonGateFailures(List<VfxCandidateEvaluationResult> candidates)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
                for (int gateIndex = 0; gateIndex < candidates[i].HardGates.Count; gateIndex++)
                {
                    VfxHardGateResult gate = candidates[i].HardGates[gateIndex];
                    if (gate != null && !gate.Passed) { counts.TryGetValue(gate.GateId, out int count); counts[gate.GateId] = count + 1; }
                }
            var result = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts) if (pair.Value == candidates.Count && candidates.Count > 0) result.Add(pair.Key);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static bool AllScoresValid(VfxReviewerScore score) => InRange(score.BriefMatch) && InRange(score.Readability) && InRange(score.Timing) && InRange(score.SpatialMotion) && InRange(score.MaterialAndColor) && InRange(score.Polish);
        private static bool InRange(int value) => value >= 0 && value <= 100;
        private static int WeightedTotal(int brief, int readability, int timing, int motion, int material, int polish) => (brief * 25 + readability * 20 + timing * 20 + motion * 15 + material * 10 + polish * 10 + 50) / 100;
        private static int Median(List<int> values) { values.Sort(); return values[1]; }

        private static void Add(VfxEvaluationReport report, CharacterAuthoringValidationSeverity severity, string code, string message, string fix, string field) => report.Diagnostics.Add(VfxAuthoringCatalogValidator.CreateDiagnostic(severity, code, string.Empty, string.Empty, message, fix, field));
        private static VfxEvaluationReport Finish(VfxEvaluationReport report)
        {
            report.Diagnostics.Sort((left, right) =>
            {
                int code = string.Compare(left.Code, right.Code, StringComparison.Ordinal);
                return code != 0 ? code : string.Compare(left.SourceField, right.SourceField, StringComparison.Ordinal);
            });
            report.ReportHash = VfxCreativeFlywheelHash.ComputeEvaluationReportHash(report);
            return report;
        }
    }
}
