using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MxFramework.Authoring
{
    /// <summary>Project-relative inputs used to scan VFX authoring and runtime evidence.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only scan options.</remarks>
    public sealed class VfxAuthoringProjectScanOptions
    {
        public string CatalogPath { get; set; } = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        public string ResourceCatalogPath { get; set; } = "Assets/Config/MxFramework/ResourceCatalogs/mxframework_samples_resource_catalog.json";
        public string DefinitionCatalogPath { get; set; } = "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json";
        public string VfxAssetRoot { get; set; } = "Assets/VFX";
        public string PackageManifestPath { get; set; } = "Packages/manifest.json";
        public string BakeEvidenceRoot { get; set; } = "Assets/MxFrameworkGenerated/Vfx/AuthoringBakeEvidence";
        public string ThumbnailManifestPath { get; set; } = "Temp/MxFramework/VfxAuthoring/Thumbnails/thumbnail_manifest.json";
    }

    /// <summary>Stable project scan result for VFX authoring diagnostics and reporting.</summary>
    /// <remarks>LowFreqAlloc. Owns catalog, evidence, validation, and report objects.</remarks>
    public sealed class VfxAuthoringProjectScanResult
    {
        public string CatalogPath { get; set; } = string.Empty;
        public string CatalogHash { get; set; } = string.Empty;
        public VfxAuthoringCatalogDocument Catalog { get; set; }
        public VfxAuthoringCatalogEvidence Evidence { get; set; } = new VfxAuthoringCatalogEvidence();
        public VfxAuthoringCatalogValidationReport Validation { get; set; } = new VfxAuthoringCatalogValidationReport();
        public VfxAuthoringProjectReport Report { get; set; } = new VfxAuthoringProjectReport();
    }

    /// <summary>Stable JSON/Markdown report generated from one VFX project scan.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only report DTO.</remarks>
    public sealed class VfxAuthoringProjectReport
    {
        public string SchemaVersion { get; set; } = "mx.vfx.authoring.report.v1";
        public string CatalogId { get; set; } = string.Empty;
        public string CatalogHash { get; set; } = string.Empty;
        public bool StrictCoverage { get; set; }
        public bool VfxGraphAvailable { get; set; }
        public int PrimitiveCount { get; set; }
        public int RecipeCount { get; set; }
        public int EffectCount { get; set; }
        public int DirectVfxResourceCount { get; set; }
        public int CoveredDirectVfxResourceCount { get; set; }
        public int MissingCoverageCount { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
        public int FingerprintedPrefabCount { get; set; }
        public int ReuseCandidateGroupCount { get; set; }
        public float SemanticCoverageRate { get; set; }
        public int LegacyPlaceholderEffectCount { get; set; }
        public int CandidateEffectCount { get; set; }
        public int ProductionEffectCount { get; set; }
        public float ProductionCoverageRate { get; set; }
        public float RecipeEffectRate { get; set; }
        public float PrimitiveReuseRate { get; set; }
        public float DuplicateCandidateAssetRate { get; set; }
        public int RecipeBackedEffectCount { get; set; }
        public int ReusedPrimitiveCount { get; set; }
        public int DuplicateCandidateAssetCount { get; set; }
        public int ThumbnailCount { get; set; }
        public int MissingThumbnailCount { get; set; }
        public int VisualDuplicateGroupCount { get; set; }
        public int PerceptualCandidateGroupCount { get; set; }
        public bool BudgetGatePassed { get; set; } = true;
        public int BudgetFindingCount { get; set; }
        public int ObservedAssetContentHashCount { get; set; }
        public int ObservedDependencyHashCount { get; set; }
        public int ObservedEvaluationReportHashCount { get; set; }
        public int ObservedQualityProfileVersionCount { get; set; }
        public List<VfxAuthoringBudgetFinding> BudgetFindings { get; set; } = new List<VfxAuthoringBudgetFinding>();
        public List<VfxAuthoringVisualDuplicateGroup> VisualDuplicateGroups { get; set; } = new List<VfxAuthoringVisualDuplicateGroup>();
        public List<VfxAuthoringPerceptualCandidateGroup> PerceptualCandidateGroups { get; set; } = new List<VfxAuthoringPerceptualCandidateGroup>();
        public List<VfxAuthoringPrefabFingerprint> PrefabFingerprints { get; set; } = new List<VfxAuthoringPrefabFingerprint>();
        public List<VfxAuthoringReuseCandidateGroup> ReuseCandidateGroups { get; set; } = new List<VfxAuthoringReuseCandidateGroup>();
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
        public string MarkdownReport { get; set; } = string.Empty;
    }

    public sealed class VfxAuthoringPerceptualCandidateGroup
    {
        public int MaximumHammingDistance { get; set; }
        public List<string> EffectIds { get; set; } = new List<string>();
    }

    public sealed class VfxAuthoringBudgetFinding
    {
        public string EffectId { get; set; } = string.Empty;
        public string AssetPath { get; set; } = string.Empty;
        public string CostTier { get; set; } = string.Empty;
        public int EstimatedInstanceCost { get; set; }
        public int RecommendedMaxConcurrent { get; set; }
        public int EstimatedConcurrentCost { get; set; }
        public int TierLimit { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class VfxAuthoringVisualDuplicateGroup
    {
        public string PreviewHash { get; set; } = string.Empty;
        public List<string> EffectIds { get; set; } = new List<string>();
    }

    /// <summary>Rebuildable structural evidence extracted from one Unity VFX prefab.</summary>
    /// <remarks>LowFreqAlloc. Contains no Unity object ids or absolute paths.</remarks>
    public sealed class VfxAuthoringPrefabFingerprint
    {
        public string AssetPath { get; set; } = string.Empty;
        public string StructureHash { get; set; } = string.Empty;
        public int GameObjectCount { get; set; }
        public int ParticleSystemCount { get; set; }
        public int ParticleRendererCount { get; set; }
        public int LineRendererCount { get; set; }
        public int VisualEffectCount { get; set; }
        public int CustomBehaviourCount { get; set; }
        public int MaterialReferenceCount { get; set; }
        public List<string> ComponentClassIds { get; set; } = new List<string>();
    }

    /// <summary>Matching structural-profile group proposed for human reuse review.</summary>
    /// <remarks>LowFreqAlloc. Evidence only; never merges or deletes Unity assets.</remarks>
    public sealed class VfxAuthoringReuseCandidateGroup
    {
        public string StructureHash { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public List<string> AssetPaths { get; set; } = new List<string>();
    }

    /// <summary>Reads repository catalogs and Unity asset paths into VFX authoring evidence.</summary>
    public static class VfxAuthoringProjectScanner
    {
        /// <summary>Scans one repository and validates its VFX Authoring Catalog against observed evidence.</summary>
        /// <remarks>LowFreqAlloc. Performs bounded authoring-time file IO, JSON parsing, hashing, and directory enumeration.</remarks>
        public static VfxAuthoringProjectScanResult Scan(string repositoryRoot, VfxAuthoringProjectScanOptions options = null)
        {
            string root = RequireRepositoryRoot(repositoryRoot);
            options ??= new VfxAuthoringProjectScanOptions();
            NormalizeOptions(options);

            var result = new VfxAuthoringProjectScanResult { CatalogPath = options.CatalogPath };
            string catalogAbsolutePath = ResolveUnderRoot(root, options.CatalogPath);
            result.CatalogHash = ComputeFileHash(catalogAbsolutePath);

            VfxAuthoringCatalogLoadResult load = VfxAuthoringCatalogJson.LoadFile(catalogAbsolutePath);
            if (!load.Success || load.Catalog == null)
            {
                result.Validation.Diagnostics.AddRange(load.Diagnostics);
                result.Validation.HasErrors = true;
                result.Report = BuildReport(null, result.CatalogHash, result.Evidence, result.Validation);
                return result;
            }

            result.Catalog = load.Catalog;
            var scanDiagnostics = new List<AuthoringResourceDiagnostic>();
            ScanUnityAssetPaths(root, options.VfxAssetRoot, result.Evidence, scanDiagnostics);
            ScanResourceCatalog(root, options.ResourceCatalogPath, result.Evidence, scanDiagnostics);
            ScanDefinitions(root, options.DefinitionCatalogPath, result.Evidence, scanDiagnostics);
            ScanBakeEvidence(root, options.BakeEvidenceRoot, result.Evidence, scanDiagnostics);
            ScanThumbnailManifest(root, options.ThumbnailManifestPath, result.Evidence, scanDiagnostics);
            result.Evidence.VfxGraphAvailable = ReadVfxGraphAvailability(root, options.PackageManifestPath, scanDiagnostics);
            DetectStalePublishedEffects(result.Catalog, result.Evidence);
            PopulateRebuildableApprovalEvidence(root, result.Catalog, result.Evidence);

            List<VfxAuthoringPrefabFingerprint> fingerprints = ScanPrefabFingerprints(root, options.VfxAssetRoot, scanDiagnostics);
            result.Validation = VfxAuthoringCatalogValidator.Validate(result.Catalog, result.Evidence);
            List<VfxAuthoringBudgetFinding> budgetFindings = BuildBudgetFindings(result.Catalog, result.Evidence, fingerprints);
            AddBudgetDiagnostics(result.Catalog, budgetFindings, scanDiagnostics);
            result.Validation.Diagnostics.AddRange(scanDiagnostics);
            SortDiagnostics(result.Validation.Diagnostics);
            result.Validation.HasErrors = ContainsErrors(result.Validation.Diagnostics);
            result.Report = BuildReport(result.Catalog, result.CatalogHash, result.Evidence, result.Validation, fingerprints);
            result.Report.BudgetFindings.AddRange(budgetFindings);
            result.Report.BudgetFindingCount = budgetFindings.Count;
            result.Report.BudgetGatePassed = budgetFindings.Count == 0;
            result.Report.MarkdownReport = BuildMarkdownReport(result.Report);
            return result;
        }

        private static List<VfxAuthoringBudgetFinding> BuildBudgetFindings(VfxAuthoringCatalogDocument catalog, VfxAuthoringCatalogEvidence evidence, List<VfxAuthoringPrefabFingerprint> fingerprints)
        {
            var result = new List<VfxAuthoringBudgetFinding>();
            var byPath = new Dictionary<string, VfxAuthoringPrefabFingerprint>(StringComparer.Ordinal);
            for (int i = 0; i < fingerprints.Count; i++)
                byPath[fingerprints[i].AssetPath] = fingerprints[i];
            for (int i = 0; i < catalog.Effects.Count; i++)
            {
                VfxAuthoringEffect effect = catalog.Effects[i];
                if (effect == null || effect.Budget == null || effect.Quality == null || effect.ResourceKey == null ||
                    !evidence.ResourceAssetPaths.TryGetValue(effect.ResourceKey.ToStableKey(), out string path) ||
                    !byPath.TryGetValue(path, out VfxAuthoringPrefabFingerprint fingerprint))
                    continue;
                int instanceCost = fingerprint.ParticleSystemCount + fingerprint.ParticleRendererCount +
                                   fingerprint.LineRendererCount * 2 + fingerprint.VisualEffectCount * 4 +
                                   fingerprint.CustomBehaviourCount * 2;
                int concurrentCost = instanceCost * effect.Budget.RecommendedMaxConcurrent;
                int limit = GetTierLimit(effect.Quality.CostTier);
                if (concurrentCost <= limit)
                    continue;
                result.Add(new VfxAuthoringBudgetFinding
                {
                    EffectId = effect.EffectId,
                    AssetPath = path,
                    CostTier = effect.Quality.CostTier,
                    EstimatedInstanceCost = instanceCost,
                    RecommendedMaxConcurrent = effect.Budget.RecommendedMaxConcurrent,
                    EstimatedConcurrentCost = concurrentCost,
                    TierLimit = limit,
                    Message = "Estimated component concurrency cost exceeds the declared quality tier; profile and reduce concurrency, raise the reviewed tier, or add fallback."
                });
            }
            result.Sort((left, right) => string.Compare(left.EffectId, right.EffectId, StringComparison.Ordinal));
            return result;
        }

        private static int GetTierLimit(string costTier)
        {
            if (string.Equals(costTier, VfxAuthoringCostTiers.Low, StringComparison.Ordinal)) return 48;
            if (string.Equals(costTier, VfxAuthoringCostTiers.Medium, StringComparison.Ordinal)) return 160;
            return 512;
        }

        private static void AddBudgetDiagnostics(VfxAuthoringCatalogDocument catalog, List<VfxAuthoringBudgetFinding> findings, List<AuthoringResourceDiagnostic> diagnostics)
        {
            for (int i = 0; i < findings.Count; i++)
            {
                VfxAuthoringBudgetFinding finding = findings[i];
                VfxAuthoringEffect effect = null;
                for (int effectIndex = 0; effectIndex < catalog.Effects.Count; effectIndex++)
                    if (catalog.Effects[effectIndex] != null && string.Equals(catalog.Effects[effectIndex].EffectId, finding.EffectId, StringComparison.Ordinal))
                        effect = catalog.Effects[effectIndex];
                CharacterAuthoringValidationSeverity severity = effect != null && string.Equals(effect.ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal)
                    ? CharacterAuthoringValidationSeverity.Error
                    : CharacterAuthoringValidationSeverity.Warning;
                diagnostics.Add(VfxAuthoringCatalogValidator.CreateDiagnostic(severity, VfxAuthoringDiagnosticCodes.SchemaInvalid, finding.EffectId, string.Empty, finding.Message, "Profile the Effect and adjust budget, quality tier, or fallback before Ready approval.", "budget"));
            }
        }

        private static void ScanThumbnailManifest(string root, string relativePath, VfxAuthoringCatalogEvidence evidence, List<AuthoringResourceDiagnostic> diagnostics)
        {
            string absolutePath = ResolveUnderRoot(root, relativePath);
            if (!File.Exists(absolutePath))
                return;
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(absolutePath));
                if (!document.RootElement.TryGetProperty("entries", out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
                    return;
                foreach (JsonElement entry in entries.EnumerateArray())
                {
                    string effectId = entry.TryGetProperty("effectId", out JsonElement id) ? id.GetString() : string.Empty;
                    string status = entry.TryGetProperty("status", out JsonElement state) ? state.GetString() : string.Empty;
                    string hash = entry.TryGetProperty("previewHash", out JsonElement previewHash) ? previewHash.GetString() : string.Empty;
                    string path = entry.TryGetProperty("thumbnailPath", out JsonElement thumbnailPath) ? NormalizePath(thumbnailPath.GetString()) : string.Empty;
                    string perceptualHash = entry.TryGetProperty("perceptualHash", out JsonElement perceptual) ? perceptual.GetString() : string.Empty;
                    if (!string.IsNullOrWhiteSpace(effectId) && string.Equals(status, "Generated", StringComparison.Ordinal))
                    {
                        evidence.PreviewHashes[effectId] = hash ?? string.Empty;
                        evidence.ThumbnailPaths[effectId] = path;
                        if (!string.IsNullOrWhiteSpace(perceptualHash))
                            evidence.PerceptualHashes[effectId] = perceptualHash;
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Warning, VfxAuthoringDiagnosticCodes.PreviewInvalid, relativePath, "VFX thumbnail manifest is invalid: " + ex.Message, "Regenerate thumbnails through the Unity Editor menu."));
            }
        }

        private static void ScanBakeEvidence(string root, string relativeRoot, VfxAuthoringCatalogEvidence evidence, List<AuthoringResourceDiagnostic> diagnostics)
        {
            string absoluteRoot = ResolveUnderRoot(root, relativeRoot);
            if (!Directory.Exists(absoluteRoot))
                return;
            try
            {
                foreach (string file in Directory.EnumerateFiles(absoluteRoot, "*.json", SearchOption.TopDirectoryOnly))
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
                    JsonElement rootElement = document.RootElement;
                    string effectId = rootElement.TryGetProperty("effectId", out JsonElement effect) ? effect.GetString() : string.Empty;
                    string stableId = rootElement.TryGetProperty("stableId", out JsonElement stable) ? stable.GetString() : effectId;
                    string recipeInputHash = rootElement.TryGetProperty("recipeInputHash", out JsonElement hash) ? hash.GetString() : string.Empty;
                    string outputAssetPath = rootElement.TryGetProperty("outputAssetPath", out JsonElement path) ? NormalizePath(path.GetString()) : string.Empty;
                    if (!string.IsNullOrWhiteSpace(effectId))
                    {
                        evidence.PublishedRecipeInputHashes[effectId] = recipeInputHash ?? string.Empty;
                        evidence.PublishedAssetPaths[effectId] = outputAssetPath;
                    }
                    string evaluationReportPath = rootElement.TryGetProperty("evaluationReportPath", out JsonElement evaluationPath) ? NormalizePath(evaluationPath.GetString()) : string.Empty;
                    string evaluationCandidateId = rootElement.TryGetProperty("evaluationCandidateId", out JsonElement candidateId) ? candidateId.GetString() : string.Empty;
                    string promotionPlanPath = rootElement.TryGetProperty("promotionPlanPath", out JsonElement promotionPlan) ? NormalizePath(promotionPlan.GetString()) : string.Empty;
                    string promotionReceiptPath = rootElement.TryGetProperty("promotionReceiptPath", out JsonElement promotionReceipt) ? NormalizePath(promotionReceipt.GetString()) : string.Empty;
                    if (!string.IsNullOrWhiteSpace(stableId) && !string.IsNullOrWhiteSpace(evaluationReportPath))
                        ObserveEvaluationReport(root, stableId, evaluationReportPath, evaluationCandidateId, promotionPlanPath, promotionReceiptPath, evidence, diagnostics);
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Warning, VfxAuthoringDiagnosticCodes.PublishedEffectStale, relativeRoot, "VFX bake evidence scan was incomplete: " + ex.Message, "Regenerate the affected Published Effect evidence through the Unity baker."));
            }
        }

        private static void ObserveEvaluationReport(string root, string stableId, string relativePath, string candidateId, string promotionPlanPath, string promotionReceiptPath, VfxAuthoringCatalogEvidence evidence, List<AuthoringResourceDiagnostic> diagnostics)
        {
            string absolutePath;
            string absolutePromotionPlanPath;
            string absolutePromotionReceiptPath;
            try
            {
                absolutePath = ResolveUnderRoot(root, relativePath);
                absolutePromotionPlanPath = ResolveUnderRoot(root, promotionPlanPath);
                absolutePromotionReceiptPath = ResolveUnderRoot(root, promotionReceiptPath);
            }
            catch (ArgumentException ex)
            {
                diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Warning, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, relativePath, "VFX evaluation evidence path is invalid: " + ex.Message, "Regenerate immutable evaluation evidence under the repository root."));
                return;
            }
            if (string.IsNullOrWhiteSpace(candidateId) || string.IsNullOrWhiteSpace(promotionPlanPath) || string.IsNullOrWhiteSpace(promotionReceiptPath) ||
                !File.Exists(absolutePath) || !File.Exists(absolutePromotionPlanPath) || !File.Exists(absolutePromotionReceiptPath))
            {
                diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Warning, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, relativePath, "VFX evaluation report is not bound to a durable promotion target/candidate receipt chain.", "Record evaluationCandidateId, promotionPlanPath, and promotionReceiptPath in durable evidence before Ready approval."));
                return;
            }
            try
            {
                VfxEvaluationReport report = VfxCreativeFlywheelJson.LoadFile<VfxEvaluationReport>(absolutePath);
                string observedHash = VfxCreativeFlywheelHash.ComputeEvaluationReportHash(report);
                if (!string.Equals(report.ReportHash, observedHash, StringComparison.OrdinalIgnoreCase) || report.Blocked)
                {
                    diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Warning, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, relativePath, "VFX evaluation report is stale, tampered, or blocked.", "Regenerate the evaluation report before Ready approval."));
                    return;
                }
                VfxCandidateEvaluationResult selected = null;
                for (int i = 0; i < report.Candidates.Count; i++)
                {
                    VfxCandidateEvaluationResult candidate = report.Candidates[i];
                    if (candidate != null && string.Equals(candidate.CandidateId, candidateId, StringComparison.Ordinal))
                    {
                        selected = candidate;
                        break;
                    }
                }
                if (selected == null || !selected.HardGatesPassed || !selected.Shortlisted)
                {
                    diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Warning, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, relativePath, "VFX evaluation evidence does not contain the promotion candidate as a hard-gate-passed shortlist result.", "Evaluate and shortlist the exact promoted candidate before Ready approval."));
                    return;
                }

                VfxPromotionPlan plan = VfxCreativeFlywheelJson.LoadFile<VfxPromotionPlan>(absolutePromotionPlanPath);
                VfxPromotionReceipt receipt = VfxCreativeFlywheelJson.LoadFile<VfxPromotionReceipt>(absolutePromotionReceiptPath);
                if (plan == null || receipt == null || plan.Blocked ||
                    !string.Equals(plan.PlanHash, VfxCreativeFlywheelHash.ComputePromotionPlanHash(plan), StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(plan.TargetStableId, stableId, StringComparison.Ordinal) ||
                    !string.Equals(plan.CandidateId, candidateId, StringComparison.Ordinal) ||
                    !string.Equals(plan.EvaluationReportHash, observedHash, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(plan.CandidateReceiptHash, report.CandidateReceiptHash, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Warning, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, promotionPlanPath, "VFX evaluation evidence is not bound to the exact immutable promotion target, candidate, and evaluation report.", "Regenerate the promotion plan for the exact target/candidate/report chain."));
                    return;
                }
                VfxReceiptValidationReport promotionValidation = VfxReceiptValidator.ValidatePromotionReceipt(plan, receipt, new VfxReceiptValidationContext { RepositoryRoot = root, VerifyAssetFiles = false });
                if (!promotionValidation.Valid)
                {
                    diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Warning, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, promotionReceiptPath, "VFX promotion receipt is invalid or not bound to the observed promotion plan.", "Regenerate or restore the immutable promotion receipt before Ready approval."));
                    return;
                }
                evidence.EvaluationReportHashes[stableId] = observedHash;
                evidence.QualityProfileVersions[stableId] = report.QualityProfileVersion ?? string.Empty;
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)
            {
                diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Warning, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, relativePath, "VFX evaluation report could not be observed: " + ex.Message, "Regenerate valid immutable evaluation evidence."));
            }
        }

        private static void PopulateRebuildableApprovalEvidence(string root, VfxAuthoringCatalogDocument catalog, VfxAuthoringCatalogEvidence evidence)
        {
            var primitiveDependencies = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Primitives.Count; i++)
            {
                VfxAuthoringPrimitive primitive = catalog.Primitives[i];
                if (primitive == null) continue;
                string assetHash = ComputeProjectAssetHash(root, primitive.AssetRef != null ? primitive.AssetRef.Path : string.Empty);
                if (VfxCreativeFlywheelHash.IsSha256(assetHash)) evidence.AssetContentHashes[primitive.PrimitiveId] = assetHash;
                string semanticHash = VfxCreativeFlywheelHash.Compute(primitive, "approval", "reviewStatus", "exemplar");
                string dependencyHash = VfxCreativeFlywheelHash.ComputeText("primitive|" + semanticHash + "|" + assetHash);
                primitiveDependencies[primitive.PrimitiveId] = dependencyHash;
                evidence.DependencyHashes[primitive.PrimitiveId] = dependencyHash;
            }

            var recipes = new Dictionary<string, VfxAuthoringRecipe>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Recipes.Count; i++)
            {
                VfxAuthoringRecipe recipe = catalog.Recipes[i];
                if (recipe != null) recipes[recipe.RecipeId] = recipe;
            }
            var recipeDependencies = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, VfxAuthoringRecipe> pair in recipes)
            {
                string semanticHash = VfxCreativeFlywheelHash.Compute(pair.Value, "approval", "reviewStatus", "exemplar");
                evidence.AssetContentHashes[pair.Key] = semanticHash;
                ComputeRecipeDependencyHash(pair.Key, recipes, primitiveDependencies, recipeDependencies, new HashSet<string>(StringComparer.Ordinal));
            }
            foreach (KeyValuePair<string, string> pair in recipeDependencies) evidence.DependencyHashes[pair.Key] = pair.Value;

            for (int i = 0; i < catalog.Effects.Count; i++)
            {
                VfxAuthoringEffect effect = catalog.Effects[i];
                if (effect == null) continue;
                string assetPath = effect.Published != null ? effect.Published.AssetPath : string.Empty;
                if (string.IsNullOrWhiteSpace(assetPath) && effect.ResourceKey != null)
                    evidence.ResourceAssetPaths.TryGetValue(effect.ResourceKey.ToStableKey(), out assetPath);
                string assetHash = ComputeProjectAssetHash(root, assetPath);
                if (VfxCreativeFlywheelHash.IsSha256(assetHash)) evidence.AssetContentHashes[effect.EffectId] = assetHash;
                recipeDependencies.TryGetValue(effect.RecipeId ?? string.Empty, out string recipeHash);
                string fallbackHash = string.Empty;
                if (effect.Quality != null && !string.IsNullOrWhiteSpace(effect.Quality.FallbackEffectId))
                {
                    for (int fallbackIndex = 0; fallbackIndex < catalog.Effects.Count; fallbackIndex++)
                    {
                        VfxAuthoringEffect fallback = catalog.Effects[fallbackIndex];
                        if (fallback != null && string.Equals(fallback.EffectId, effect.Quality.FallbackEffectId, StringComparison.Ordinal))
                        {
                            fallbackHash = VfxCreativeFlywheelHash.Compute(fallback, "approval", "reviewStatus", "exemplar");
                            break;
                        }
                    }
                }
                string semanticHash = VfxCreativeFlywheelHash.Compute(effect, "approval", "reviewStatus", "exemplar");
                evidence.DependencyHashes[effect.EffectId] = VfxCreativeFlywheelHash.ComputeText("effect|" + semanticHash + "|" + recipeHash + "|" + fallbackHash + "|" + assetHash);
            }
        }

        private static string ComputeRecipeDependencyHash(string recipeId, Dictionary<string, VfxAuthoringRecipe> recipes, Dictionary<string, string> primitiveDependencies, Dictionary<string, string> cache, HashSet<string> visiting)
        {
            if (cache.TryGetValue(recipeId, out string cached)) return cached;
            if (!recipes.TryGetValue(recipeId, out VfxAuthoringRecipe recipe) || !visiting.Add(recipeId)) return string.Empty;
            var dependencies = new List<string>();
            for (int i = 0; i < recipe.IncludedRecipeIds.Count; i++) dependencies.Add(ComputeRecipeDependencyHash(recipe.IncludedRecipeIds[i], recipes, primitiveDependencies, cache, visiting));
            for (int i = 0; i < recipe.Layers.Count; i++)
                if (recipe.Layers[i] != null && primitiveDependencies.TryGetValue(recipe.Layers[i].PrimitiveId, out string primitiveHash)) dependencies.Add(primitiveHash);
            dependencies.Sort(StringComparer.Ordinal);
            visiting.Remove(recipeId);
            string semanticHash = VfxCreativeFlywheelHash.Compute(recipe, "approval", "reviewStatus", "exemplar");
            string result = VfxCreativeFlywheelHash.ComputeText("recipe|" + semanticHash + "|" + string.Join("|", dependencies));
            cache[recipeId] = result;
            return result;
        }

        private static string ComputeProjectAssetHash(string root, string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || VfxCandidatePathPolicy.IsCandidateSandboxAssetPath(projectPath)) return string.Empty;
            try
            {
                return VfxCreativeFlywheelHash.ComputeFile(ResolveUnderRoot(root, NormalizePath(projectPath)));
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }

        private static void DetectStalePublishedEffects(VfxAuthoringCatalogDocument catalog, VfxAuthoringCatalogEvidence evidence)
        {
            for (int i = 0; i < catalog.Effects.Count; i++)
            {
                VfxAuthoringEffect effect = catalog.Effects[i];
                if (effect == null || string.IsNullOrWhiteSpace(effect.RecipeId) || effect.Published == null)
                    continue;
                string expected = VfxAuthoringBakeHasher.ComputeRecipeInputHash(catalog, effect, effect.Published.BakerId, effect.Published.BakerVersion);
                evidence.PublishedRecipeInputHashes.TryGetValue(effect.EffectId, out string observedHash);
                evidence.PublishedAssetPaths.TryGetValue(effect.EffectId, out string observedPath);
                if (string.IsNullOrWhiteSpace(expected) ||
                    !string.Equals(expected, effect.Published.RecipeInputHash, StringComparison.Ordinal) ||
                    !string.Equals(expected, observedHash, StringComparison.Ordinal) ||
                    !string.Equals(effect.Published.AssetPath, observedPath, StringComparison.Ordinal) ||
                    !evidence.UnityAssetPaths.Contains(effect.Published.AssetPath))
                {
                    evidence.StaleEffectIds.Add(effect.EffectId);
                }
            }
        }

        private static List<VfxAuthoringPrefabFingerprint> ScanPrefabFingerprints(string root, string relativeRoot, List<AuthoringResourceDiagnostic> diagnostics)
        {
            var results = new List<VfxAuthoringPrefabFingerprint>();
            string absoluteRoot = ResolveUnderRoot(root, relativeRoot);
            if (!Directory.Exists(absoluteRoot))
                return results;

            try
            {
                foreach (string file in Directory.EnumerateFiles(absoluteRoot, "*.prefab", SearchOption.AllDirectories))
                {
                    if (VfxCandidatePathPolicy.IsCandidateSandboxAssetPath(ToProjectRelativePath(root, file)))
                        continue;
                    results.Add(BuildPrefabFingerprint(root, file));
                }
                results.Sort((left, right) => string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Warning, VfxAuthoringDiagnosticCodes.ReferenceMissing, relativeRoot, "VFX prefab fingerprint scan was incomplete: " + ex.Message, "Restore read access and rerun the scanner."));
            }
            return results;
        }

        private static VfxAuthoringPrefabFingerprint BuildPrefabFingerprint(string root, string file)
        {
            var classCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var materialGuids = new HashSet<string>(StringComparer.Ordinal);
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    int start = 7;
                    int end = line.IndexOf(' ', start);
                    string classId = end > start ? line.Substring(start, end - start) : line.Substring(start);
                    classCounts.TryGetValue(classId, out int count);
                    classCounts[classId] = count + 1;
                }
                if (line.StartsWith("- {fileID:", StringComparison.Ordinal) && line.IndexOf("guid:", StringComparison.Ordinal) >= 0)
                {
                    int guidStart = line.IndexOf("guid:", StringComparison.Ordinal) + 5;
                    int guidEnd = line.IndexOf(',', guidStart);
                    if (guidEnd > guidStart)
                        materialGuids.Add(line.Substring(guidStart, guidEnd - guidStart).Trim());
                }
            }

            var canonical = new StringBuilder();
            var classIds = new List<string>(classCounts.Count);
            foreach (KeyValuePair<string, int> pair in classCounts)
            {
                classIds.Add(pair.Key + "x" + pair.Value);
                canonical.Append(pair.Key).Append(':').Append(pair.Value).Append(';');
            }
            canonical.Append("materials:").Append(materialGuids.Count);
            return new VfxAuthoringPrefabFingerprint
            {
                AssetPath = ToProjectRelativePath(root, file),
                StructureHash = ComputeTextHash(canonical.ToString()),
                GameObjectCount = GetCount(classCounts, "1"),
                ParticleSystemCount = GetCount(classCounts, "198"),
                ParticleRendererCount = GetCount(classCounts, "199"),
                LineRendererCount = GetCount(classCounts, "120"),
                VisualEffectCount = GetCount(classCounts, "73398921"),
                CustomBehaviourCount = GetCount(classCounts, "114"),
                MaterialReferenceCount = materialGuids.Count,
                ComponentClassIds = classIds
            };
        }

        private static int GetCount(SortedDictionary<string, int> counts, string classId)
        {
            return counts.TryGetValue(classId, out int count) ? count : 0;
        }

        private static string ComputeTextHash(string value)
        {
            using SHA256 sha256 = SHA256.Create();
            return "sha256:" + ToLowerHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
        }

        /// <summary>Computes a lowercase SHA-256 hash for optimistic authoring concurrency.</summary>
        /// <remarks>LowFreqAlloc. Reads the target authoring file once.</remarks>
        public static string ComputeFileHash(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return string.Empty;
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return ToLowerHex(sha256.ComputeHash(stream));
        }

        private static void ScanUnityAssetPaths(string root, string relativeRoot, VfxAuthoringCatalogEvidence evidence, List<AuthoringResourceDiagnostic> diagnostics)
        {
            string absoluteRoot = ResolveUnderRoot(root, relativeRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReferenceMissing, relativeRoot, "VFX Unity asset root is missing: " + relativeRoot, "Restore the VFX asset root or update scan options."));
                return;
            }

            foreach (string file in Directory.EnumerateFiles(absoluteRoot, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;
                string projectPath = ToProjectRelativePath(root, file);
                if (VfxCandidatePathPolicy.IsCandidateSandboxAssetPath(projectPath))
                    continue;
                evidence.UnityAssetPaths.Add(projectPath);
            }
        }

        private static void ScanResourceCatalog(string root, string relativePath, VfxAuthoringCatalogEvidence evidence, List<AuthoringResourceDiagnostic> diagnostics)
        {
            string absolutePath = ResolveUnderRoot(root, relativePath);
            if (!File.Exists(absolutePath))
            {
                diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReferenceMissing, relativePath, "VFX Resource Catalog evidence is missing: " + relativePath, "Restore the Resource Catalog or update scan options."));
                return;
            }

            try
            {
                RuntimeResourceCatalogDocument document = JsonSerializer.Deserialize<RuntimeResourceCatalogDocument>(File.ReadAllText(absolutePath), CreateJsonOptions());
                if (document == null)
                    throw new JsonException("Resource Catalog deserialized to null.");
                document.Entries ??= new List<RuntimeResourceCatalogEntryDocument>();
                for (int i = 0; i < document.Entries.Count; i++)
                {
                    RuntimeResourceCatalogEntryDocument entry = document.Entries[i];
                    if (entry == null)
                        continue;
                    string packageId = string.IsNullOrWhiteSpace(entry.PackageId) ? document.PackageId : entry.PackageId;
                    string key = BuildStableKey(packageId, entry.Id, entry.Type, entry.Variant);
                    evidence.ResourceKeys.Add(key);
                    if (string.Equals(entry.Type, "GameObject", StringComparison.Ordinal) && Contains(entry.Labels, "domain.vfx"))
                        evidence.DirectVfxResourceKeys.Add(key);
                    if (entry.ProviderData != null && entry.ProviderData.TryGetValue("assetPath", out string assetPath) && !string.IsNullOrWhiteSpace(assetPath))
                    {
                        evidence.UnityAssetPaths.Add(NormalizePath(assetPath));
                        evidence.ResourceAssetPaths[key] = NormalizePath(assetPath);
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, relativePath, "VFX Resource Catalog JSON is invalid: " + ex.Message, "Repair the Resource Catalog before validating VFX authoring semantics."));
            }
        }

        private static void ScanDefinitions(string root, string relativePath, VfxAuthoringCatalogEvidence evidence, List<AuthoringResourceDiagnostic> diagnostics)
        {
            string absolutePath = ResolveUnderRoot(root, relativePath);
            if (!File.Exists(absolutePath))
            {
                diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReferenceMissing, relativePath, "VFX Definition evidence is missing: " + relativePath, "Regenerate VFX definitions or update scan options."));
                return;
            }

            try
            {
                AbilityVfxDefinitionCatalogDocument document = JsonSerializer.Deserialize<AbilityVfxDefinitionCatalogDocument>(File.ReadAllText(absolutePath), CreateJsonOptions());
                if (document == null)
                    throw new JsonException("VFX Definition Catalog deserialized to null.");
                document.Definitions ??= new List<VfxDefinitionConfig>();
                for (int i = 0; i < document.Definitions.Count; i++)
                {
                    VfxDefinitionConfig definition = document.Definitions[i];
                    if (definition == null || definition.DefinitionId <= 0)
                        continue;
                    evidence.DefinitionIds.Add(definition.DefinitionId);
                    if (definition.PrefabResource != null)
                    {
                        string key = BuildStableKey(definition.PrefabResource.PackageId, definition.PrefabResource.Id, definition.PrefabResource.TypeId, definition.PrefabResource.Variant);
                        evidence.DefinitionIdsByResourceKey[key] = definition.DefinitionId;
                        evidence.DefinitionLifetimesByResourceKey[key] = definition.DefaultLifetimeFrames;
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, relativePath, "VFX Definition Catalog JSON is invalid: " + ex.Message, "Regenerate or repair VFX definitions."));
            }
        }

        private static bool ReadVfxGraphAvailability(string root, string relativePath, List<AuthoringResourceDiagnostic> diagnostics)
        {
            string absolutePath = ResolveUnderRoot(root, relativePath);
            if (!File.Exists(absolutePath))
            {
                diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Warning, VfxAuthoringDiagnosticCodes.BackendFallbackInvalid, relativePath, "Unity package manifest is missing; VFX Graph availability is unknown.", "Restore Packages/manifest.json or configure scan options."));
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(absolutePath));
                return document.RootElement.TryGetProperty("dependencies", out JsonElement dependencies) &&
                       dependencies.TryGetProperty("com.unity.visualeffectgraph", out JsonElement version) &&
                       !string.IsNullOrWhiteSpace(version.GetString());
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                diagnostics.Add(CreateScanDiagnostic(CharacterAuthoringValidationSeverity.Warning, VfxAuthoringDiagnosticCodes.BackendFallbackInvalid, relativePath, "Unity package manifest is invalid; VFX Graph availability is unknown: " + ex.Message, "Repair Packages/manifest.json."));
                return false;
            }
        }

        private static VfxAuthoringProjectReport BuildReport(VfxAuthoringCatalogDocument catalog, string catalogHash, VfxAuthoringCatalogEvidence evidence, VfxAuthoringCatalogValidationReport validation, List<VfxAuthoringPrefabFingerprint> fingerprints = null)
        {
            var report = new VfxAuthoringProjectReport
            {
                CatalogId = catalog != null ? catalog.CatalogId : string.Empty,
                CatalogHash = catalogHash ?? string.Empty,
                StrictCoverage = catalog != null && catalog.StrictCoverage,
                VfxGraphAvailable = evidence != null && evidence.VfxGraphAvailable,
                PrimitiveCount = catalog != null ? catalog.Primitives.Count : 0,
                RecipeCount = catalog != null ? catalog.Recipes.Count : 0,
                EffectCount = catalog != null ? catalog.Effects.Count : 0,
                DirectVfxResourceCount = evidence != null ? evidence.DirectVfxResourceKeys.Count : 0,
                ObservedAssetContentHashCount = evidence != null ? evidence.AssetContentHashes.Count : 0,
                ObservedDependencyHashCount = evidence != null ? evidence.DependencyHashes.Count : 0,
                ObservedEvaluationReportHashCount = evidence != null ? evidence.EvaluationReportHashes.Count : 0,
                ObservedQualityProfileVersionCount = evidence != null ? evidence.QualityProfileVersions.Count : 0
            };
            if (validation != null)
                report.Diagnostics.AddRange(validation.Diagnostics);
            if (fingerprints != null)
            {
                report.PrefabFingerprints.AddRange(fingerprints);
                report.FingerprintedPrefabCount = fingerprints.Count;
                BuildReuseCandidateGroups(fingerprints, report.ReuseCandidateGroups);
                report.ReuseCandidateGroupCount = report.ReuseCandidateGroups.Count;
            }

            var covered = new HashSet<string>(StringComparer.Ordinal);
            if (catalog != null)
                for (int i = 0; i < catalog.Effects.Count; i++)
                    if (catalog.Effects[i] != null && catalog.Effects[i].ResourceKey != null)
                        covered.Add(catalog.Effects[i].ResourceKey.ToStableKey());
            if (evidence != null)
                foreach (string key in evidence.DirectVfxResourceKeys)
                    if (covered.Contains(key))
                        report.CoveredDirectVfxResourceCount++;
            report.MissingCoverageCount = Math.Max(0, report.DirectVfxResourceCount - report.CoveredDirectVfxResourceCount);
            report.SemanticCoverageRate = Ratio(report.CoveredDirectVfxResourceCount, report.DirectVfxResourceCount);
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Effects.Count; i++)
                {
                    VfxAuthoringEffect effect = catalog.Effects[i];
                    if (effect == null) continue;
                    if (string.Equals(effect.VisualMaturity, VfxAuthoringVisualMaturities.Production, StringComparison.Ordinal)) report.ProductionEffectCount++;
                    else if (string.Equals(effect.VisualMaturity, VfxAuthoringVisualMaturities.Candidate, StringComparison.Ordinal)) report.CandidateEffectCount++;
                    else report.LegacyPlaceholderEffectCount++;
                }
            }
            report.ProductionCoverageRate = Ratio(report.ProductionEffectCount, report.DirectVfxResourceCount);
            ComputeReuseMetrics(catalog, evidence, report);
            ComputeThumbnailMetrics(catalog, evidence, report);

            for (int i = 0; i < report.Diagnostics.Count; i++)
            {
                switch (report.Diagnostics[i].Severity)
                {
                    case CharacterAuthoringValidationSeverity.Error: report.ErrorCount++; break;
                    case CharacterAuthoringValidationSeverity.Warning: report.WarningCount++; break;
                    default: report.InfoCount++; break;
                }
            }
            report.MarkdownReport = BuildMarkdownReport(report);
            return report;
        }

        private static void ComputeThumbnailMetrics(VfxAuthoringCatalogDocument catalog, VfxAuthoringCatalogEvidence evidence, VfxAuthoringProjectReport report)
        {
            report.ThumbnailCount = evidence != null ? evidence.PreviewHashes.Count : 0;
            report.MissingThumbnailCount = Math.Max(0, (catalog != null ? catalog.Effects.Count : 0) - report.ThumbnailCount);
            var groups = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            if (evidence != null)
            {
                foreach (KeyValuePair<string, string> pair in evidence.PreviewHashes)
                {
                    if (string.IsNullOrWhiteSpace(pair.Value))
                        continue;
                    if (!groups.TryGetValue(pair.Value, out List<string> ids))
                    {
                        ids = new List<string>();
                        groups.Add(pair.Value, ids);
                    }
                    ids.Add(pair.Key);
                }
            }
            foreach (KeyValuePair<string, List<string>> pair in groups)
            {
                if (pair.Value.Count < 2)
                    continue;
                pair.Value.Sort(StringComparer.Ordinal);
                report.VisualDuplicateGroups.Add(new VfxAuthoringVisualDuplicateGroup { PreviewHash = pair.Key, EffectIds = pair.Value });
            }
            report.VisualDuplicateGroupCount = report.VisualDuplicateGroups.Count;
            BuildPerceptualCandidateGroups(evidence, report);
        }

        private static void BuildPerceptualCandidateGroups(VfxAuthoringCatalogEvidence evidence, VfxAuthoringProjectReport report)
        {
            const int threshold = 8;
            var ids = new List<string>(evidence.PerceptualHashes.Keys);
            ids.Sort(StringComparer.Ordinal);
            var parents = new int[ids.Count];
            for (int i = 0; i < parents.Length; i++) parents[i] = i;
            for (int left = 0; left < ids.Count; left++)
            {
                if (!TryParseHash(evidence.PerceptualHashes[ids[left]], out ulong leftHash)) continue;
                for (int right = left + 1; right < ids.Count; right++)
                {
                    if (!TryParseHash(evidence.PerceptualHashes[ids[right]], out ulong rightHash)) continue;
                    if (CountBits(leftHash ^ rightHash) <= threshold) Union(parents, left, right);
                }
            }
            var groups = new SortedDictionary<int, List<string>>();
            for (int i = 0; i < ids.Count; i++)
            {
                int root = Find(parents, i);
                if (!groups.TryGetValue(root, out List<string> group)) { group = new List<string>(); groups.Add(root, group); }
                group.Add(ids[i]);
            }
            foreach (List<string> group in groups.Values)
            {
                if (group.Count < 2) continue;
                int maximum = 0;
                for (int left = 0; left < group.Count; left++)
                    for (int right = left + 1; right < group.Count; right++)
                        if (TryParseHash(evidence.PerceptualHashes[group[left]], out ulong leftHash) && TryParseHash(evidence.PerceptualHashes[group[right]], out ulong rightHash))
                            maximum = Math.Max(maximum, CountBits(leftHash ^ rightHash));
                report.PerceptualCandidateGroups.Add(new VfxAuthoringPerceptualCandidateGroup { MaximumHammingDistance = maximum, EffectIds = group });
            }
            report.PerceptualCandidateGroupCount = report.PerceptualCandidateGroups.Count;
        }

        private static bool TryParseHash(string value, out ulong hash) => ulong.TryParse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out hash);
        private static int CountBits(ulong value) { int count = 0; while (value != 0UL) { value &= value - 1UL; count++; } return count; }
        private static int Find(int[] parents, int index) { while (parents[index] != index) { parents[index] = parents[parents[index]]; index = parents[index]; } return index; }
        private static void Union(int[] parents, int left, int right) { int leftRoot = Find(parents, left); int rightRoot = Find(parents, right); if (leftRoot != rightRoot) parents[rightRoot] = leftRoot; }

        private static void ComputeReuseMetrics(VfxAuthoringCatalogDocument catalog, VfxAuthoringCatalogEvidence evidence, VfxAuthoringProjectReport report)
        {
            if (catalog != null)
            {
                var primitiveRecipeUsage = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < catalog.Recipes.Count; i++)
                {
                    VfxAuthoringRecipe recipe = catalog.Recipes[i];
                    if (recipe == null)
                        continue;
                    for (int layerIndex = 0; layerIndex < recipe.Layers.Count; layerIndex++)
                    {
                        VfxAuthoringRecipeLayer layer = recipe.Layers[layerIndex];
                        if (layer == null)
                            continue;
                        primitiveRecipeUsage.TryGetValue(layer.PrimitiveId, out int usage);
                        primitiveRecipeUsage[layer.PrimitiveId] = usage + 1;
                    }
                }
                for (int i = 0; i < catalog.Effects.Count; i++)
                    if (catalog.Effects[i] != null && !string.IsNullOrWhiteSpace(catalog.Effects[i].RecipeId))
                        report.RecipeBackedEffectCount++;
                if (evidence != null)
                {
                    for (int primitiveIndex = 0; primitiveIndex < catalog.Primitives.Count; primitiveIndex++)
                    {
                        VfxAuthoringPrimitive primitive = catalog.Primitives[primitiveIndex];
                        if (primitive == null || primitive.AssetRef == null)
                            continue;
                        for (int effectIndex = 0; effectIndex < catalog.Effects.Count; effectIndex++)
                        {
                            VfxAuthoringEffect effect = catalog.Effects[effectIndex];
                            if (effect == null || effect.ResourceKey == null || !string.IsNullOrWhiteSpace(effect.RecipeId))
                                continue;
                            if (evidence.ResourceAssetPaths.TryGetValue(effect.ResourceKey.ToStableKey(), out string effectAssetPath) &&
                                string.Equals(effectAssetPath, primitive.AssetRef.Path, StringComparison.Ordinal))
                            {
                                primitiveRecipeUsage.TryGetValue(primitive.PrimitiveId, out int usage);
                                primitiveRecipeUsage[primitive.PrimitiveId] = usage + 1;
                            }
                        }
                    }
                }
                foreach (int usage in primitiveRecipeUsage.Values)
                    if (usage >= 2)
                        report.ReusedPrimitiveCount++;
                report.RecipeEffectRate = Ratio(report.RecipeBackedEffectCount, catalog.Effects.Count);
                report.PrimitiveReuseRate = Ratio(report.ReusedPrimitiveCount, catalog.Primitives.Count);
            }
            var candidateAssets = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < report.ReuseCandidateGroups.Count; i++)
                for (int pathIndex = 0; pathIndex < report.ReuseCandidateGroups[i].AssetPaths.Count; pathIndex++)
                    candidateAssets.Add(report.ReuseCandidateGroups[i].AssetPaths[pathIndex]);
            report.DuplicateCandidateAssetCount = candidateAssets.Count;
            report.DuplicateCandidateAssetRate = Ratio(report.DuplicateCandidateAssetCount, report.FingerprintedPrefabCount);
        }

        private static float Ratio(int numerator, int denominator)
        {
            return denominator > 0 ? (float)numerator / denominator : 0f;
        }

        private static void BuildReuseCandidateGroups(List<VfxAuthoringPrefabFingerprint> fingerprints, List<VfxAuthoringReuseCandidateGroup> output)
        {
            var byHash = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < fingerprints.Count; i++)
            {
                VfxAuthoringPrefabFingerprint fingerprint = fingerprints[i];
                if (!byHash.TryGetValue(fingerprint.StructureHash, out List<string> paths))
                {
                    paths = new List<string>();
                    byHash.Add(fingerprint.StructureHash, paths);
                }
                paths.Add(fingerprint.AssetPath);
            }
            foreach (KeyValuePair<string, List<string>> pair in byHash)
            {
                if (pair.Value.Count < 2)
                    continue;
                pair.Value.Sort(StringComparer.Ordinal);
                output.Add(new VfxAuthoringReuseCandidateGroup
                {
                    StructureHash = pair.Key,
                    Reason = "Structural similarity only; LegacyPlaceholder groups are replacement/contract evidence, not visual reuse inputs. Production candidates still require semantic, dependency, quality, and human review.",
                    AssetPaths = pair.Value
                });
            }
        }

        private static string BuildMarkdownReport(VfxAuthoringProjectReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# VFX Authoring Report");
            builder.AppendLine();
            builder.AppendLine("- Schema: `" + report.SchemaVersion + "`");
            builder.AppendLine("- Catalog: `" + Escape(report.CatalogId) + "`");
            builder.AppendLine("- Catalog hash: `" + Escape(report.CatalogHash) + "`");
            builder.AppendLine("- Strict coverage: `" + (report.StrictCoverage ? "true" : "false") + "`");
            builder.AppendLine("- VFX Graph available: `" + (report.VfxGraphAvailable ? "true" : "false") + "`");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine("| Primitive | Recipe | Effect | Direct VFX | Covered | Missing | Error | Warning | Info |");
            builder.AppendLine("| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
            builder.AppendLine("| " + report.PrimitiveCount + " | " + report.RecipeCount + " | " + report.EffectCount + " | " + report.DirectVfxResourceCount + " | " + report.CoveredDirectVfxResourceCount + " | " + report.MissingCoverageCount + " | " + report.ErrorCount + " | " + report.WarningCount + " | " + report.InfoCount + " |");
            builder.AppendLine();
            builder.AppendLine("- Fingerprinted prefabs: `" + report.FingerprintedPrefabCount + "`");
            builder.AppendLine("- Structural-profile reuse candidate groups: `" + report.ReuseCandidateGroupCount + "`");
            builder.AppendLine("- Semantic coverage rate: `" + FormatPercent(report.SemanticCoverageRate) + "`");
            builder.AppendLine("- Production coverage: `" + report.ProductionEffectCount + "/" + report.DirectVfxResourceCount + "` (`" + FormatPercent(report.ProductionCoverageRate) + "`)");
            builder.AppendLine("- Visual maturity (LegacyPlaceholder/Candidate/Production): `" + report.LegacyPlaceholderEffectCount + "/" + report.CandidateEffectCount + "/" + report.ProductionEffectCount + "`");
            builder.AppendLine("- Recipe-backed Effect rate: `" + FormatPercent(report.RecipeEffectRate) + "`");
            builder.AppendLine("- Reused Primitive rate: `" + FormatPercent(report.PrimitiveReuseRate) + "`");
            builder.AppendLine("- Duplicate-candidate asset rate: `" + FormatPercent(report.DuplicateCandidateAssetRate) + "`");
            builder.AppendLine("- Ready evidence observed (asset/dependency/evaluation/quality): `" + report.ObservedAssetContentHashCount + "/" + report.ObservedDependencyHashCount + "/" + report.ObservedEvaluationReportHashCount + "/" + report.ObservedQualityProfileVersionCount + "`");
            builder.AppendLine("- Generated thumbnails: `" + report.ThumbnailCount + "`");
            builder.AppendLine("- Missing thumbnails: `" + report.MissingThumbnailCount + "`");
            builder.AppendLine("- Exact preview-hash duplicate groups: `" + report.VisualDuplicateGroupCount + "`");
            builder.AppendLine("- Perceptual candidate groups (dHash distance <= 8): `" + report.PerceptualCandidateGroupCount + "`");
            builder.AppendLine("- Budget gate: `" + (report.BudgetGatePassed ? "passed" : "findings") + "`");
            builder.AppendLine("- Budget findings: `" + report.BudgetFindingCount + "`");
            builder.AppendLine();
            builder.AppendLine("## Reuse Candidates");
            builder.AppendLine();
            if (report.ReuseCandidateGroups.Count == 0)
            {
                builder.AppendLine("No exact-structure candidates.");
            }
            else
            {
                builder.AppendLine("| Structure Hash | Assets | Review Reason |");
                builder.AppendLine("| --- | --- | --- |");
                for (int i = 0; i < report.ReuseCandidateGroups.Count; i++)
                {
                    VfxAuthoringReuseCandidateGroup candidate = report.ReuseCandidateGroups[i];
                    builder.Append("| `").Append(Escape(candidate.StructureHash)).Append("` | ")
                        .Append(Escape(string.Join("<br>", candidate.AssetPaths))).Append(" | ")
                        .Append(Escape(candidate.Reason)).AppendLine(" |");
                }
            }
            builder.AppendLine();
            builder.AppendLine("## Diagnostics");
            builder.AppendLine();
            if (report.Diagnostics.Count == 0)
            {
                builder.AppendLine("No diagnostics.");
                return builder.ToString();
            }
            builder.AppendLine("| Severity | Code | Stable Id | Runtime ResourceKey | Field | Message | Suggested Fix |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            for (int i = 0; i < report.Diagnostics.Count; i++)
            {
                AuthoringResourceDiagnostic diagnostic = report.Diagnostics[i];
                builder.Append("| ").Append(diagnostic.Severity).Append(" | `").Append(Escape(diagnostic.Code)).Append("` | `")
                    .Append(Escape(diagnostic.SourceStableId)).Append("` | `").Append(Escape(diagnostic.RuntimeResourceKey)).Append("` | `")
                    .Append(Escape(diagnostic.SourceField)).Append("` | ").Append(Escape(diagnostic.Message)).Append(" | ")
                    .Append(Escape(diagnostic.SuggestedFix)).AppendLine(" |");
            }
            return builder.ToString();
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            return new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
        }

        private static void NormalizeOptions(VfxAuthoringProjectScanOptions options)
        {
            options.CatalogPath = NormalizePath(options.CatalogPath);
            options.ResourceCatalogPath = NormalizePath(options.ResourceCatalogPath);
            options.DefinitionCatalogPath = NormalizePath(options.DefinitionCatalogPath);
            options.VfxAssetRoot = NormalizePath(options.VfxAssetRoot);
            options.PackageManifestPath = NormalizePath(options.PackageManifestPath);
            options.BakeEvidenceRoot = NormalizePath(options.BakeEvidenceRoot);
            options.ThumbnailManifestPath = NormalizePath(options.ThumbnailManifestPath);
        }

        private static string RequireRepositoryRoot(string repositoryRoot)
        {
            if (string.IsNullOrWhiteSpace(repositoryRoot))
                throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));
            string root = Path.GetFullPath(repositoryRoot);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException("Repository root does not exist: " + repositoryRoot);
            return root;
        }

        private static string ResolveUnderRoot(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new ArgumentException("VFX scan path must be project-relative: " + (relativePath ?? string.Empty));
            string fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("VFX scan path escapes the repository root: " + relativePath);
            return fullPath;
        }

        private static string ToProjectRelativePath(string root, string fullPath)
        {
            string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string absolutePath = Path.GetFullPath(fullPath);
            return NormalizePath(absolutePath.Substring(rootPrefix.Length));
        }

        private static string BuildStableKey(string packageId, string id, string type, string variant)
        {
            return (packageId ?? string.Empty) + "|" + (id ?? string.Empty) + "|" + (type ?? string.Empty) + "|" + (variant ?? string.Empty);
        }

        private static bool Contains(List<string> values, string expected)
        {
            if (values == null)
                return false;
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], expected, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static AuthoringResourceDiagnostic CreateScanDiagnostic(CharacterAuthoringValidationSeverity severity, string code, string sourceField, string message, string suggestedFix)
        {
            return VfxAuthoringCatalogValidator.CreateDiagnostic(severity, code, string.Empty, string.Empty, message, suggestedFix, NormalizePath(sourceField));
        }

        private static bool ContainsErrors(List<AuthoringResourceDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
                if (diagnostics[i].Severity == CharacterAuthoringValidationSeverity.Error)
                    return true;
            return false;
        }

        private static void SortDiagnostics(List<AuthoringResourceDiagnostic> diagnostics)
        {
            diagnostics.Sort((left, right) =>
            {
                int value = string.Compare(left.Code, right.Code, StringComparison.Ordinal);
                if (value != 0) return value;
                value = string.Compare(left.SourceStableId, right.SourceStableId, StringComparison.Ordinal);
                if (value != 0) return value;
                value = string.Compare(left.SourceField, right.SourceField, StringComparison.Ordinal);
                if (value != 0) return value;
                return string.Compare(left.RuntimeResourceKey, right.RuntimeResourceKey, StringComparison.Ordinal);
            });
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        private static string FormatPercent(float value)
        {
            return (value * 100f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        private static string NormalizePath(string value)
        {
            return (value ?? string.Empty).Trim().Replace('\\', '/');
        }
    }

    public static class VfxAuthoringOperationKinds
    {
        public const string SaveCatalog = "SaveCatalog";
    }

    public static class VfxAuthoringOperationDiagnosticCodes
    {
        public const string ExpectedHashRequired = "VFXOP001";
        public const string CatalogConflict = "VFXOP002";
        public const string ValidationBlocked = "VFXOP003";
        public const string CatalogOnly = "VFXOP004";
        public const string CatalogV2Required = "VFXOP005";
    }

    /// <summary>Dry-run impact plan for a VFX authoring catalog operation.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only immutable plan result.</remarks>
    public sealed class VfxAuthoringOperationPlan
    {
        public string OperationKind { get; set; } = string.Empty;
        public string CatalogPath { get; set; } = string.Empty;
        public string ExpectedCatalogHash { get; set; } = string.Empty;
        public string ActualCatalogHash { get; set; } = string.Empty;
        public bool WouldModifyCatalog { get; set; }
        public bool WouldModifyUnityAssets { get; set; }
        public bool WouldModifyPublishedOutput { get; set; }
        public bool RequiresUnityAuthoring { get; set; }
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();

        public bool Blocked
        {
            get
            {
                for (int i = 0; i < Diagnostics.Count; i++)
                    if (Diagnostics[i].Severity == CharacterAuthoringValidationSeverity.Error)
                        return true;
                return false;
            }
        }
    }

    /// <summary>Builds bounded dry-run plans without writing VFX catalog or Unity assets.</summary>
    public static class VfxAuthoringOperationPlanner
    {
        /// <summary>Plans a semantic catalog save with optimistic hash and current project evidence validation.</summary>
        /// <remarks>LowFreqAlloc. Reads current project evidence but performs no writes.</remarks>
        public static VfxAuthoringOperationPlan PlanSaveCatalog(
            string repositoryRoot,
            string expectedCatalogHash,
            VfxAuthoringCatalogDocument proposedCatalog,
            VfxAuthoringProjectScanOptions options = null)
        {
            VfxAuthoringProjectScanResult current = VfxAuthoringProjectScanner.Scan(repositoryRoot, options);
            var plan = new VfxAuthoringOperationPlan
            {
                OperationKind = VfxAuthoringOperationKinds.SaveCatalog,
                CatalogPath = current.CatalogPath,
                ExpectedCatalogHash = expectedCatalogHash ?? string.Empty,
                ActualCatalogHash = current.CatalogHash
            };

            if (current.Catalog == null)
            {
                plan.Diagnostics.AddRange(current.Validation.Diagnostics);
                plan.Diagnostics.Add(OperationDiagnostic(CharacterAuthoringValidationSeverity.Error, VfxAuthoringOperationDiagnosticCodes.ValidationBlocked, "Current VFX Authoring Catalog could not be loaded.", "Repair or restore the current catalog before planning a save."));
                SortPlanDiagnostics(plan.Diagnostics);
                return plan;
            }

            if (string.IsNullOrWhiteSpace(expectedCatalogHash))
                plan.Diagnostics.Add(OperationDiagnostic(CharacterAuthoringValidationSeverity.Error, VfxAuthoringOperationDiagnosticCodes.ExpectedHashRequired, "Expected catalog hash is required.", "Reload the catalog state and include its current hash."));
            else if (!string.Equals(expectedCatalogHash, current.CatalogHash, StringComparison.OrdinalIgnoreCase))
                plan.Diagnostics.Add(OperationDiagnostic(CharacterAuthoringValidationSeverity.Error, VfxAuthoringOperationDiagnosticCodes.CatalogConflict, "VFX Authoring Catalog changed since the draft was loaded.", "Reload, reapply the semantic edit, and regenerate the plan."));

            if (proposedCatalog != null && !string.Equals(proposedCatalog.SchemaVersion, VfxAuthoringSchemaVersions.V2, StringComparison.Ordinal))
                plan.Diagnostics.Add(OperationDiagnostic(CharacterAuthoringValidationSeverity.Error, VfxAuthoringOperationDiagnosticCodes.CatalogV2Required, "Only VFX Authoring Catalog v2 can be saved.", "Treat v1 as read-only migration input, explicitly migrate it to v2, provide approval evidence for every Ready entry, and regenerate the save plan."));

            VfxAuthoringCatalogValidationReport proposedValidation = VfxAuthoringCatalogValidator.Validate(proposedCatalog, current.Evidence);
            plan.Diagnostics.AddRange(proposedValidation.Diagnostics);
            if (proposedValidation.HasErrors)
                plan.Diagnostics.Add(OperationDiagnostic(CharacterAuthoringValidationSeverity.Error, VfxAuthoringOperationDiagnosticCodes.ValidationBlocked, "Proposed VFX Authoring Catalog has blocking diagnostics.", "Resolve validation errors before saving."));

            if (!plan.Blocked)
            {
                plan.WouldModifyCatalog = !string.Equals(
                    VfxAuthoringCatalogJson.Serialize(current.Catalog),
                    VfxAuthoringCatalogJson.Serialize(proposedCatalog),
                    StringComparison.Ordinal);
                plan.Diagnostics.Add(OperationDiagnostic(CharacterAuthoringValidationSeverity.Info, VfxAuthoringOperationDiagnosticCodes.CatalogOnly, "The operation may update only VFX authoring semantics.", "Unity prefabs, scenes, VFX Graph assets, runtime definitions, and published outputs remain unchanged."));
            }
            SortPlanDiagnostics(plan.Diagnostics);
            return plan;
        }

        private static void SortPlanDiagnostics(List<AuthoringResourceDiagnostic> diagnostics)
        {
            diagnostics.Sort((left, right) =>
            {
                int value = string.Compare(left.Code, right.Code, StringComparison.Ordinal);
                if (value != 0) return value;
                value = string.Compare(left.SourceStableId, right.SourceStableId, StringComparison.Ordinal);
                if (value != 0) return value;
                return string.Compare(left.SourceField, right.SourceField, StringComparison.Ordinal);
            });
        }

        private static AuthoringResourceDiagnostic OperationDiagnostic(CharacterAuthoringValidationSeverity severity, string code, string message, string suggestedFix)
        {
            return VfxAuthoringCatalogValidator.CreateDiagnostic(severity, code, string.Empty, string.Empty, message, suggestedFix, "operation");
        }
    }
}
