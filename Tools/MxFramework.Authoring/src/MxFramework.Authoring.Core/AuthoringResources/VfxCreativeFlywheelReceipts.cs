using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Authoring
{
    /// <summary>External state used to validate immutable VFX receipts.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only; file verification is optional for pure planning tests.</remarks>
    public sealed class VfxReceiptValidationContext
    {
        public string RepositoryRoot { get; set; } = string.Empty;
        public string ActualCatalogHash { get; set; } = string.Empty;
        public string ActualGenerationTemplateCatalogHash { get; set; } = string.Empty;
        public bool VerifyAssetFiles { get; set; } = true;
        public VfxGenerationTemplateCatalogDocument GenerationTemplates { get; set; }
        public IVfxPromotionSelectionAuthorityVerifier PromotionSelectionAuthorityVerifier { get; set; }
    }

    /// <summary>Validates candidate and promotion receipts against plans, hashes, provenance, and sandbox boundaries.</summary>
    public static class VfxReceiptValidator
    {
        /// <summary>Validates a Unity candidate-generation receipt without mutating its files.</summary>
        /// <remarks>LowFreqAlloc. May read and hash candidate files when requested.</remarks>
        public static VfxReceiptValidationReport ValidateCandidateReceipt(VfxGenerationPlan plan, VfxCandidateGenerationReceipt receipt, VfxReceiptValidationContext context)
        {
            var report = new VfxReceiptValidationReport();
            context ??= new VfxReceiptValidationContext();
            if (plan == null || receipt == null)
            {
                Add(report, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Candidate plan or receipt is missing.", "Provide both immutable documents.", "receipt");
                return Finish(report);
            }
            report.ExpectedHash = receipt.ReceiptHash ?? string.Empty;
            report.ActualHash = VfxCreativeFlywheelHash.ComputeCandidateReceiptHash(receipt);
            if (!string.Equals(plan.SchemaVersion, VfxCreativeFlywheelSchemaVersions.GenerationPlanV1, StringComparison.Ordinal) || !string.Equals(receipt.SchemaVersion, VfxCreativeFlywheelSchemaVersions.CandidateReceiptV1, StringComparison.Ordinal))
                Add(report, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Candidate plan/receipt schema is unsupported.", "Regenerate both documents with current tools.", "schemaVersion");
            string actualPlanHash = VfxCreativeFlywheelHash.ComputeGenerationPlanHash(plan);
            if (!string.Equals(plan.PlanHash, actualPlanHash, StringComparison.OrdinalIgnoreCase) || plan.Blocked)
                Add(report, VfxFlywheelDiagnosticCodes.PlanTampered, "Generation plan hash is invalid or the plan is blocked.", "Regenerate an unblocked plan.", "planHash");
            if (!string.Equals(receipt.ReceiptHash, report.ActualHash, StringComparison.OrdinalIgnoreCase))
                Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Candidate receipt hash does not match its content.", "Discard the receipt and rerun the formal Unity executor.", "receiptHash");
            if (!string.Equals(receipt.GenerationPlanHash, plan.PlanHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(receipt.SessionId, plan.SessionId, StringComparison.Ordinal))
                Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Candidate receipt is not bound to this generation plan/session.", "Use the receipt emitted for the exact plan.", "generationPlanHash");
            ValidateExpectedHashes(plan.CatalogHash, plan.GenerationTemplateCatalogHash, receipt.CatalogHash, receipt.GenerationTemplateCatalogHash, context, report);
            if (string.IsNullOrWhiteSpace(receipt.ExecutorId) || string.IsNullOrWhiteSpace(receipt.ExecutorVersion))
                Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Candidate receipt requires formal executor id/version.", "Use the Unity candidate executor.", "executor");

            var candidates = new Dictionary<string, VfxGenerationCandidateSpec>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Candidates.Count; i++) if (plan.Candidates[i] != null) candidates[plan.Candidates[i].CandidateId] = plan.Candidates[i];
            var coveredCandidates = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < receipt.Assets.Count; i++)
            {
                VfxCandidateAssetRecord asset = receipt.Assets[i];
                string prefix = "assets/" + i;
                if (asset == null || !candidates.TryGetValue(asset.CandidateId, out VfxGenerationCandidateSpec candidate))
                {
                    Add(report, VfxFlywheelDiagnosticCodes.CandidateInvalid, "Receipt asset references an unknown candidate.", "Emit assets only for candidates in the plan.", prefix + "/candidateId");
                    continue;
                }
                coveredCandidates.Add(asset.CandidateId);
                string path = VfxCandidatePathPolicy.NormalizeProjectPath(asset.AssetPath);
                if (!paths.Add(path) || !VfxCandidatePathPolicy.IsUnderSandbox(path, candidate.OutputRoot))
                    Add(report, VfxFlywheelDiagnosticCodes.SandboxViolation, "Candidate asset is duplicated or escapes its physical MxCandidates sandbox: " + path, "Write only under " + candidate.OutputRoot + ".", prefix + "/assetPath");
                if (!VfxCreativeFlywheelHash.IsSha256(asset.ContentHash))
                    Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Candidate asset content hash is missing or invalid.", "Hash every generated asset after Unity import settings are finalized.", prefix + "/contentHash");
                if (!VfxCreativeFlywheelHash.IsUnityGuid(asset.UnityGuid))
                    Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Candidate asset Unity GUID is missing or invalid.", "Record the imported asset GUID after Unity has finalized its .meta file.", prefix + "/unityGuid");
                if (!VfxCreativeFlywheelHash.IsUnityDependencyHash(asset.DependencyHash))
                    Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Candidate asset dependency hash is missing or invalid.", "Record AssetDatabase.GetAssetDependencyHash after every dependency is imported.", prefix + "/dependencyHash");
                ValidateProvenance(asset, candidate, plan.SessionCapabilitiesHash, context.GenerationTemplates, prefix, report);
                if (context.VerifyAssetFiles)
                    VerifyFile(context.RepositoryRoot, path, asset.ContentHash, prefix, report);
            }
            foreach (string candidateId in candidates.Keys)
                if (!coveredCandidates.Contains(candidateId))
                    Add(report, VfxFlywheelDiagnosticCodes.CandidateInvalid, "Candidate has no generated asset evidence: " + candidateId, "Regenerate or explicitly cancel the candidate before evaluation.", "candidates");
            return Finish(report);
        }

        /// <summary>Validates a Unity promotion receipt against its transactional move plan.</summary>
        /// <remarks>LowFreqAlloc. May read and hash promoted destination files.</remarks>
        public static VfxReceiptValidationReport ValidatePromotionReceipt(VfxPromotionPlan plan, VfxPromotionReceipt receipt, VfxReceiptValidationContext context)
        {
            var report = new VfxReceiptValidationReport();
            context ??= new VfxReceiptValidationContext();
            if (plan == null || receipt == null)
            {
                Add(report, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Promotion plan or receipt is missing.", "Provide both immutable documents.", "promotionReceipt");
                return Finish(report);
            }
            report.ExpectedHash = receipt.ReceiptHash ?? string.Empty;
            report.ActualHash = VfxCreativeFlywheelHash.ComputePromotionReceiptHash(receipt);
            if (!string.Equals(plan.SchemaVersion, VfxCreativeFlywheelSchemaVersions.PromotionPlanV1, StringComparison.Ordinal) || !string.Equals(receipt.SchemaVersion, VfxCreativeFlywheelSchemaVersions.PromotionReceiptV1, StringComparison.Ordinal))
                Add(report, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Promotion plan/receipt schema is unsupported.", "Regenerate both documents.", "schemaVersion");
            if (plan.Blocked || !string.Equals(plan.PlanHash, VfxCreativeFlywheelHash.ComputePromotionPlanHash(plan), StringComparison.OrdinalIgnoreCase))
                Add(report, VfxFlywheelDiagnosticCodes.PlanTampered, "Promotion plan hash is invalid or the plan is blocked.", "Regenerate an unblocked promotion plan.", "planHash");
            if (!string.Equals(receipt.ReceiptHash, report.ActualHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(receipt.PromotionPlanHash, plan.PlanHash, StringComparison.OrdinalIgnoreCase))
                Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Promotion receipt hash/plan binding is invalid.", "Discard it and rerun the Unity promotion executor.", "receiptHash");
            if (!string.Equals(receipt.ResultReviewStatus, VfxAuthoringReviewStatuses.ReviewNeeded, StringComparison.Ordinal) || !string.Equals(plan.ResultReviewStatus, VfxAuthoringReviewStatuses.ReviewNeeded, StringComparison.Ordinal))
                Add(report, VfxFlywheelDiagnosticCodes.ReadyForbidden, "Automated promotion may only produce ReviewNeeded entries.", "Complete separate human approval before Ready.", "resultReviewStatus");
            if (!string.IsNullOrWhiteSpace(context.ActualCatalogHash) && !string.Equals(context.ActualCatalogHash, plan.ExpectedCatalogHash, StringComparison.OrdinalIgnoreCase))
                Add(report, VfxFlywheelDiagnosticCodes.CatalogStale, "Catalog changed before promotion receipt validation.", "Rebase the promotion plan on current Catalog state.", "catalogHash");
            if (!string.Equals(receipt.CatalogHash, plan.ExpectedCatalogHash, StringComparison.OrdinalIgnoreCase))
                Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Promotion receipt Catalog hash differs from the plan.", "Use the exact guarded plan state.", "catalogHash");
            if (string.IsNullOrWhiteSpace(receipt.ExecutorId) || string.IsNullOrWhiteSpace(receipt.ExecutorVersion))
                Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Promotion receipt requires Unity executor id/version.", "Use the formal transactional promotion executor.", "executor");
            if (receipt.Assets.Count != plan.Moves.Count)
                Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Promotion receipt asset count differs from the move plan.", "Rollback and rerun the exact transaction.", "assets");
            for (int i = 0; i < plan.Moves.Count; i++)
            {
                VfxPromotionMove move = plan.Moves[i];
                VfxPromotedAssetRecord asset = FindPromotedAsset(receipt.Assets, move);
                if (asset == null
                    || !string.Equals(asset.ContentHash, move.ExpectedContentHash, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(asset.UnityGuid, move.ExpectedUnityGuid, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(asset.DependencyHash, move.ExpectedDependencyHash, StringComparison.OrdinalIgnoreCase)
                    || !VfxCreativeFlywheelHash.IsUnityGuid(asset.UnityGuid)
                    || !VfxCreativeFlywheelHash.IsUnityDependencyHash(asset.DependencyHash))
                {
                    Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Promotion receipt does not match move content/GUID/dependency evidence: " + move.DestinationAssetPath, "Rollback and rerun the transaction.", "assets/" + i);
                    continue;
                }
                if (!VfxCandidatePathPolicy.IsFormalDestination(asset.DestinationAssetPath) || !VfxCandidatePathPolicy.IsUnderSandbox(asset.SourceAssetPath, VfxCandidatePathPolicy.PhysicalCandidateRoot + "/" + CandidateSessionFromSource(asset.SourceAssetPath)))
                    Add(report, VfxFlywheelDiagnosticCodes.SandboxViolation, "Promotion source/destination path is outside the allowed boundary.", "Move from MxCandidates into a formal Assets/VFX destination.", "assets/" + i);
                if (context.VerifyAssetFiles)
                    VerifyFile(context.RepositoryRoot, asset.DestinationAssetPath, asset.ContentHash, "assets/" + i, report);
            }
            return Finish(report);
        }

        private static void ValidateExpectedHashes(string planCatalog, string planTemplates, string receiptCatalog, string receiptTemplates, VfxReceiptValidationContext context, VfxReceiptValidationReport report)
        {
            if (!string.Equals(receiptCatalog, planCatalog, StringComparison.OrdinalIgnoreCase) || !string.Equals(receiptTemplates, planTemplates, StringComparison.OrdinalIgnoreCase))
                Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Candidate receipt input hashes differ from the generation plan.", "Use the exact plan inputs.", "inputHashes");
            if (!string.IsNullOrWhiteSpace(context.ActualCatalogHash) && !string.Equals(context.ActualCatalogHash, planCatalog, StringComparison.OrdinalIgnoreCase))
                Add(report, VfxFlywheelDiagnosticCodes.CatalogStale, "Catalog changed after generation planning.", "Recreate candidates from current Catalog state.", "catalogHash");
            if (!string.IsNullOrWhiteSpace(context.ActualGenerationTemplateCatalogHash) && !string.Equals(context.ActualGenerationTemplateCatalogHash, planTemplates, StringComparison.OrdinalIgnoreCase))
                Add(report, VfxFlywheelDiagnosticCodes.TemplateCatalogStale, "Generation template Catalog changed after planning.", "Recreate candidates from the current template set.", "generationTemplateCatalogHash");
        }

        private static void ValidateProvenance(VfxCandidateAssetRecord asset, VfxGenerationCandidateSpec candidate, string sessionCapabilitiesHash, VfxGenerationTemplateCatalogDocument templates, string prefix, VfxReceiptValidationReport report)
        {
            VfxAssetProvenance provenance = asset.Provenance;
            if (provenance == null || !string.Equals(provenance.ProviderId, candidate.ProviderId, StringComparison.Ordinal) || !string.Equals(provenance.ProviderVersion, candidate.ProviderVersion, StringComparison.Ordinal) ||
                !string.Equals(provenance.TemplateId, candidate.TemplateId, StringComparison.Ordinal) || !string.Equals(provenance.TemplateVersion, candidate.TemplateVersion, StringComparison.Ordinal) ||
                !string.Equals(provenance.ModelId, candidate.ModelId, StringComparison.Ordinal) || !string.Equals(provenance.ModelVersion, candidate.ModelVersion, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(candidate.ModelId) || string.IsNullOrWhiteSpace(candidate.ModelVersion) ||
                string.IsNullOrWhiteSpace(provenance.LicenseId) || string.IsNullOrWhiteSpace(provenance.SourceUri) || !VfxCreativeFlywheelHash.IsSha256(provenance.SourceContentHash))
            {
                Add(report, VfxFlywheelDiagnosticCodes.ProvenanceInvalid, "Candidate asset has unknown/incomplete provider, license, or source provenance.", "Record exact provider/model versions, license, source URI, and source content hash.", prefix + "/provenance");
                return;
            }
            VfxGenerationProviderDefinition provider = FindProvider(templates, provenance.ProviderId);
            VfxGenerationTemplateDefinition template = FindTemplate(templates, provenance.TemplateId, provenance.TemplateVersion);
            if (provider == null || !string.Equals(provider.ProviderVersion, provenance.ProviderVersion, StringComparison.Ordinal))
                Add(report, VfxFlywheelDiagnosticCodes.ProvenanceInvalid, "Candidate provenance references an unknown provider or mismatched provider version.", "Use the exact provider id/version bound by the generation template Catalog.", prefix + "/provenance/providerVersion");
            else
            {
                bool configuredModel = VfxGenerationTemplateCatalog.HasExactModelBinding(provider);
                if (configuredModel && (!string.Equals(provider.ModelId, candidate.ModelId, StringComparison.Ordinal) || !string.Equals(provider.ModelVersion, candidate.ModelVersion, StringComparison.Ordinal)))
                    Add(report, VfxFlywheelDiagnosticCodes.ProvenanceInvalid, "Candidate model id/version does not match the provider contract.", "Record the exact configured model version.", prefix + "/provenance/modelVersion");
                if (!provider.EnabledByDefault && (!VfxReusePlanner.IsStableId(candidate.ProviderGrantId) || !VfxCreativeFlywheelHash.IsSha256(sessionCapabilitiesHash)))
                    Add(report, VfxFlywheelDiagnosticCodes.ProvenanceInvalid, "Externally enabled provider is not bound to a session capability grant.", "Regenerate the immutable plan from an exact repository-external provider/model grant.", prefix + "/provenance/providerGrantId");
                if (provider.AllowedLicenseIds.Count > 0 && !provider.AllowedLicenseIds.Contains(provenance.LicenseId))
                    Add(report, VfxFlywheelDiagnosticCodes.ProvenanceInvalid, "Candidate asset license is not allowed by the provider contract: " + provenance.LicenseId, "Use an approved license/source or reject the asset.", prefix + "/provenance/licenseId");
            }
            if (template == null || template.AllowedProviderIds.Count > 0 && !template.AllowedProviderIds.Contains(provenance.ProviderId))
                Add(report, VfxFlywheelDiagnosticCodes.ProvenanceInvalid, "Candidate provenance references an unknown template/version or a provider not allowed by that template.", "Use the exact template id/version/provider binding from the generation plan.", prefix + "/provenance/templateVersion");
        }

        private static VfxGenerationProviderDefinition FindProvider(VfxGenerationTemplateCatalogDocument templates, string id)
        {
            if (templates == null) return null;
            for (int i = 0; i < templates.Providers.Count; i++) if (templates.Providers[i] != null && string.Equals(templates.Providers[i].ProviderId, id, StringComparison.Ordinal)) return templates.Providers[i];
            return null;
        }

        private static VfxGenerationTemplateDefinition FindTemplate(VfxGenerationTemplateCatalogDocument templates, string id, string version)
        {
            if (templates == null) return null;
            for (int i = 0; i < templates.Templates.Count; i++)
                if (templates.Templates[i] != null && string.Equals(templates.Templates[i].TemplateId, id, StringComparison.Ordinal) && string.Equals(templates.Templates[i].TemplateVersion, version, StringComparison.Ordinal)) return templates.Templates[i];
            return null;
        }

        private static void VerifyFile(string repositoryRoot, string projectPath, string expectedHash, string field, VfxReceiptValidationReport report)
        {
            if (!VfxCandidatePathPolicy.TryResolveUnderProject(repositoryRoot, projectPath, out string absolute) || !File.Exists(absolute))
            {
                Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Receipt asset file is missing: " + projectPath, "Restore/rerun the Unity executor.", field + "/assetPath");
                return;
            }
            string actual = VfxCreativeFlywheelHash.ComputeFile(absolute);
            if (!string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase))
                Add(report, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Receipt asset content changed after generation: " + projectPath, "Discard stale evidence and regenerate/re-evaluate.", field + "/contentHash");
        }

        private static VfxPromotedAssetRecord FindPromotedAsset(List<VfxPromotedAssetRecord> assets, VfxPromotionMove move)
        {
            for (int i = 0; i < assets.Count; i++)
                if (assets[i] != null && string.Equals(assets[i].SourceAssetPath, move.SourceAssetPath, StringComparison.Ordinal) && string.Equals(assets[i].DestinationAssetPath, move.DestinationAssetPath, StringComparison.Ordinal)) return assets[i];
            return null;
        }

        private static string CandidateSessionFromSource(string path)
        {
            string normalized = VfxCandidatePathPolicy.NormalizeProjectPath(path);
            string prefix = VfxCandidatePathPolicy.PhysicalCandidateRoot + "/";
            if (!normalized.StartsWith(prefix, StringComparison.Ordinal)) return string.Empty;
            int end = normalized.IndexOf('/', prefix.Length);
            return end > prefix.Length ? normalized.Substring(prefix.Length, end - prefix.Length) : normalized.Substring(prefix.Length);
        }

        private static void Add(VfxReceiptValidationReport report, string code, string message, string fix, string field) => report.Diagnostics.Add(VfxAuthoringCatalogValidator.CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, code, string.Empty, string.Empty, message, fix, field));
        private static VfxReceiptValidationReport Finish(VfxReceiptValidationReport report)
        {
            report.Diagnostics.Sort((left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));
            report.Valid = !VfxCreativeFlywheelUtilities.HasErrors(report.Diagnostics);
            return report;
        }
    }

    /// <summary>Builds guarded candidate promotion plans; only a Unity Editor executor may apply them.</summary>
    public static class VfxPromotionPlanner
    {
        /// <summary>Plans GUID-preserving candidate moves after receipt/evaluation/human-selection gates.</summary>
        /// <remarks>AllocByDesign. Performs no asset or Catalog writes.</remarks>
        public static VfxPromotionPlan Plan(VfxPromotionRequest request, VfxGenerationPlan generationPlan, VfxCandidateGenerationReceipt receipt, VfxEvaluationReport evaluation, VfxReceiptValidationContext context, ISet<string> existingProjectAssetPaths = null)
        {
            var plan = new VfxPromotionPlan
            {
                CandidateId = request != null ? request.CandidateId ?? string.Empty : string.Empty,
                TargetKind = request != null ? request.TargetKind ?? string.Empty : string.Empty,
                TargetStableId = request != null ? request.TargetStableId ?? string.Empty : string.Empty,
                ExpectedCatalogHash = generationPlan != null ? generationPlan.CatalogHash : string.Empty,
                GenerationPlanHash = generationPlan != null ? generationPlan.PlanHash : string.Empty,
                CandidateReceiptHash = receipt != null ? receipt.ReceiptHash : string.Empty,
                EvaluationReportHash = evaluation != null ? evaluation.ReportHash : string.Empty,
                SelectedBy = request != null ? request.SelectedBy ?? string.Empty : string.Empty,
                SelectionEvidenceHash = request != null ? request.SelectionEvidenceHash ?? string.Empty : string.Empty,
                SelectionAuthorityId = request != null ? request.SelectionAuthorityId ?? string.Empty : string.Empty,
                SelectionAuthorityVersion = request != null ? request.SelectionAuthorityVersion ?? string.Empty : string.Empty,
                SelectionAuthorityReceiptHash = request != null ? request.SelectionAuthorityReceiptHash ?? string.Empty : string.Empty,
                StrategicFoundationExceptionApproved = request != null && request.StrategicFoundationExceptionApproved
            };
            if (request != null)
            {
                var consumerIds = new HashSet<string>(request.ConsumerRecipeIds ?? new List<string>(), StringComparer.Ordinal);
                consumerIds.Remove(string.Empty);
                plan.ConsumerRecipeIds.AddRange(consumerIds);
                plan.ConsumerRecipeIds.Sort(StringComparer.Ordinal);
            }
            if (request == null || generationPlan == null || receipt == null || evaluation == null)
            {
                Add(plan, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Promotion inputs are incomplete.", "Provide request, generation plan, candidate receipt, and evaluation report.", "promotion");
                return Finish(plan);
            }
            VfxReceiptValidationReport receiptValidation = VfxReceiptValidator.ValidateCandidateReceipt(generationPlan, receipt, context);
            plan.Diagnostics.AddRange(receiptValidation.Diagnostics);
            if (!string.Equals(evaluation.ReportHash, VfxCreativeFlywheelHash.ComputeEvaluationReportHash(evaluation), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(evaluation.GenerationPlanHash, generationPlan.PlanHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(evaluation.CandidateReceiptHash, receipt.ReceiptHash, StringComparison.OrdinalIgnoreCase))
                Add(plan, VfxFlywheelDiagnosticCodes.PlanTampered, "Evaluation report hash or input binding is invalid.", "Re-evaluate the immutable candidate receipt.", "evaluationReportHash");
            if (evaluation.Blocked)
                Add(plan, VfxFlywheelDiagnosticCodes.EvaluationBlocked, "Evaluation report contains blocking diagnostics.", "Resolve hard-gate/evaluation failures.", "evaluation");
            VfxCandidateEvaluationResult result = FindEvaluation(evaluation, request.CandidateId);
            if (result == null || !result.HardGatesPassed || !result.Shortlisted)
                Add(plan, VfxFlywheelDiagnosticCodes.EvaluationBlocked, "Selected candidate did not pass hard gates and shortlist criteria.", "Select a qualified candidate or iterate.", "candidateId");
            if (string.IsNullOrWhiteSpace(request.SelectedBy))
                Add(plan, VfxFlywheelDiagnosticCodes.HumanApprovalRequired, "Promotion requires an explicit human selection.", "Record selectedBy; automation cannot choose promotion alone.", "selectedBy");
            if (!VfxCreativeFlywheelHash.IsSha256(request.SelectionEvidenceHash))
                Add(plan, VfxFlywheelDiagnosticCodes.HumanApprovalRequired, "Promotion requires content-addressed human selection evidence.", "Record an immutable selection evidence hash from the review handoff.", "selectionEvidenceHash");
            ValidateSelectionAuthority(request, generationPlan, receipt, evaluation, context != null ? context.PromotionSelectionAuthorityVerifier : null, plan);
            if (request.StrategicFoundationExceptionApproved && !VfxCreativeFlywheelHash.IsSha256(request.SelectionEvidenceHash))
                Add(plan, VfxFlywheelDiagnosticCodes.HumanApprovalRequired, "Strategic-foundation exception must be bound to immutable human selection evidence.", "Attach the content-addressed exception approval evidence.", "strategicFoundationExceptionApproved");
            if (!string.Equals(request.RequestedReviewStatus, VfxAuthoringReviewStatuses.ReviewNeeded, StringComparison.Ordinal))
                Add(plan, VfxFlywheelDiagnosticCodes.ReadyForbidden, "Promotion may only create ReviewNeeded entries.", "Use separate human Ready approval after promotion.", "requestedReviewStatus");
            if (string.Equals(generationPlan.GenerationMode, VfxGenerationModes.ExperimentalGraph, StringComparison.Ordinal))
                Add(plan, VfxFlywheelDiagnosticCodes.ExperimentalDraftOnly, "Arbitrary Graph topology candidates cannot be promoted directly.", "Rebuild the selected look using certified templates/subgraphs/exposed properties.", "generationMode");
            if (!IsTargetKind(request.TargetKind) || !IsStableId(request.TargetStableId))
                Add(plan, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Promotion target kind/stable id is invalid.", "Use Primitive/Recipe/Effect and a stable lowercase id.", "target");
            for (int consumerIndex = 0; consumerIndex < plan.ConsumerRecipeIds.Count; consumerIndex++)
                if (!IsStableId(plan.ConsumerRecipeIds[consumerIndex]))
                    Add(plan, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Primitive consumer Recipe id is invalid: " + plan.ConsumerRecipeIds[consumerIndex], "Use stable Recipe ids.", "consumerRecipeIds/" + consumerIndex);
            if (string.Equals(request.TargetKind, VfxPromotionTargetKinds.Primitive, StringComparison.Ordinal) && plan.ConsumerRecipeIds.Count < 2 && !request.StrategicFoundationExceptionApproved)
                Add(plan, VfxFlywheelDiagnosticCodes.EvaluationBlocked, "Primitive promotion requires shortlist use in at least two Recipes.", "Provide two distinct consumer Recipe ids or an explicit human strategic-foundation exception.", "consumerRecipeIds");
            string destinationRoot = VfxCandidatePathPolicy.NormalizeProjectPath(request.DestinationRoot).TrimEnd('/');
            if (!VfxCandidatePathPolicy.IsFormalDestination(destinationRoot + "/placeholder.asset"))
                Add(plan, VfxFlywheelDiagnosticCodes.SandboxViolation, "Promotion destination root is invalid or inside MxCandidates.", "Choose a formal Assets/VFX destination.", "destinationRoot");

            var destinations = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < receipt.Assets.Count; i++)
            {
                VfxCandidateAssetRecord asset = receipt.Assets[i];
                if (asset == null || !string.Equals(asset.CandidateId, request.CandidateId, StringComparison.Ordinal)) continue;
                string destination = destinationRoot + "/" + FileName(asset.AssetPath);
                if (!destinations.Add(destination) || existingProjectAssetPaths != null && existingProjectAssetPaths.Contains(destination))
                    Add(plan, VfxFlywheelDiagnosticCodes.PromotionConflict, "Promotion destination already exists or is duplicated: " + destination, "Choose a new stable destination or resolve the existing asset explicitly.", "moves");
                plan.Moves.Add(new VfxPromotionMove
                {
                    SourceAssetPath = asset.AssetPath,
                    DestinationAssetPath = destination,
                    ExpectedContentHash = asset.ContentHash,
                    ExpectedUnityGuid = asset.UnityGuid,
                    ExpectedDependencyHash = asset.DependencyHash
                });
            }
            if (plan.Moves.Count == 0)
                Add(plan, VfxFlywheelDiagnosticCodes.CandidateInvalid, "Selected candidate has no promotable assets.", "Regenerate/validate candidate assets.", "candidateId");
            return Finish(plan);
        }

        private static VfxCandidateEvaluationResult FindEvaluation(VfxEvaluationReport report, string id)
        {
            for (int i = 0; i < report.Candidates.Count; i++) if (report.Candidates[i] != null && string.Equals(report.Candidates[i].CandidateId, id, StringComparison.Ordinal)) return report.Candidates[i];
            return null;
        }

        private static string FileName(string path)
        {
            string normalized = VfxCandidatePathPolicy.NormalizeProjectPath(path);
            int index = normalized.LastIndexOf('/');
            return index >= 0 ? normalized.Substring(index + 1) : normalized;
        }

        private static bool IsTargetKind(string value) => string.Equals(value, VfxPromotionTargetKinds.Primitive, StringComparison.Ordinal) || string.Equals(value, VfxPromotionTargetKinds.Recipe, StringComparison.Ordinal) || string.Equals(value, VfxPromotionTargetKinds.Effect, StringComparison.Ordinal);

        private static void ValidateSelectionAuthority(VfxPromotionRequest request, VfxGenerationPlan generationPlan, VfxCandidateGenerationReceipt receipt, VfxEvaluationReport evaluation, IVfxPromotionSelectionAuthorityVerifier verifier, VfxPromotionPlan plan)
        {
            if (string.IsNullOrWhiteSpace(request.SelectionAuthorityId) || string.IsNullOrWhiteSpace(request.SelectionAuthorityVersion) || !VfxCreativeFlywheelHash.IsSha256(request.SelectionAuthorityReceiptHash))
            {
                Add(plan, VfxFlywheelDiagnosticCodes.HumanApprovalRequired, "Promotion requires a versioned selection-authority receipt.", "Obtain an authenticated receipt bound to the exact candidate, evaluation, selection evidence, and promotion target.", "selectionAuthorityReceiptHash");
                return;
            }
            if (verifier == null)
            {
                Add(plan, VfxFlywheelDiagnosticCodes.HumanApprovalRequired, "Promotion is fail-closed because no trusted selection-authority verifier is configured.", "Configure an authenticated host verifier after explicit human confirmation; selectedBy and arbitrary hashes are not authorization.", "selectionAuthorityReceiptHash");
                return;
            }
            if (!string.Equals(verifier.AuthorityId, request.SelectionAuthorityId, StringComparison.Ordinal) || !string.Equals(verifier.AuthorityVersion, request.SelectionAuthorityVersion, StringComparison.Ordinal))
            {
                Add(plan, VfxFlywheelDiagnosticCodes.HumanApprovalRequired, "Selection authority id/version is not trusted by the configured verifier.", "Use a receipt issued by the configured selection authority.", "selectionAuthorityId");
                return;
            }

            var authorityRequest = new VfxPromotionSelectionAuthorityRequest
            {
                CandidateId = request.CandidateId,
                TargetKind = request.TargetKind,
                TargetStableId = request.TargetStableId,
                GenerationPlanHash = generationPlan.PlanHash,
                CandidateReceiptHash = receipt.ReceiptHash,
                EvaluationReportHash = evaluation.ReportHash,
                SelectedBy = request.SelectedBy,
                SelectionEvidenceHash = request.SelectionEvidenceHash,
                AuthorityReceiptHash = request.SelectionAuthorityReceiptHash,
                StrategicFoundationExceptionApproved = request.StrategicFoundationExceptionApproved,
                ConsumerRecipeIds = plan.ConsumerRecipeIds
            };
            bool verified;
            string rejectionReason;
            try
            {
                verified = verifier.Verify(authorityRequest, out rejectionReason);
            }
            catch (Exception ex)
            {
                verified = false;
                rejectionReason = ex.Message;
            }
            if (!verified)
                Add(plan, VfxFlywheelDiagnosticCodes.HumanApprovalRequired, "Selection authority receipt was rejected" + (string.IsNullOrWhiteSpace(rejectionReason) ? "." : ": " + rejectionReason), "Obtain a new authenticated receipt bound to the exact promotion inputs.", "selectionAuthorityReceiptHash");
        }

        private static bool IsStableId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int i = 0; i < value.Length; i++) { char c = value[i]; if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')) return false; }
            return true;
        }

        private static void Add(VfxPromotionPlan plan, string code, string message, string fix, string field) => plan.Diagnostics.Add(VfxAuthoringCatalogValidator.CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, code, plan.TargetStableId, string.Empty, message, fix, field));
        private static VfxPromotionPlan Finish(VfxPromotionPlan plan)
        {
            plan.Diagnostics.Sort((left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));
            plan.PlanHash = VfxCreativeFlywheelHash.ComputePromotionPlanHash(plan);
            return plan;
        }
    }

    /// <summary>Plans separate guarded registration of a promoted Published Effect.</summary>
    public static class VfxPublishedRegistrationPlanner
    {
        /// <summary>Produces semantic/resource/definition Catalog impact only; it performs no writes.</summary>
        /// <remarks>AllocByDesign.</remarks>
        public static VfxPublishedRegistrationPlan Plan(VfxPublishedRegistrationRequest request, VfxPromotionPlan promotionPlan, VfxPromotionReceipt promotionReceipt, VfxAuthoringCatalogDocument catalog, string actualCatalogHash, string actualResourceCatalogHash, string actualDefinitionCatalogHash)
        {
            var plan = new VfxPublishedRegistrationPlan
            {
                EffectId = request != null ? request.EffectId ?? string.Empty : string.Empty,
                ExpectedCatalogHash = request != null ? request.ExpectedCatalogHash ?? string.Empty : string.Empty,
                ExpectedResourceCatalogHash = request != null ? request.ExpectedResourceCatalogHash ?? string.Empty : string.Empty,
                ExpectedDefinitionCatalogHash = request != null ? request.ExpectedDefinitionCatalogHash ?? string.Empty : string.Empty
            };
            if (request == null || promotionPlan == null || promotionReceipt == null || catalog == null)
            {
                Add(plan, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Registration inputs are incomplete.", "Provide request, promotion plan/receipt, and current Catalog.", "registration");
                return Finish(plan);
            }
            if (!string.Equals(request.ExpectedCatalogHash, actualCatalogHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(request.ExpectedResourceCatalogHash, actualResourceCatalogHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(request.ExpectedDefinitionCatalogHash, actualDefinitionCatalogHash, StringComparison.OrdinalIgnoreCase))
                Add(plan, VfxFlywheelDiagnosticCodes.CatalogStale, "One or more guarded Catalog hashes are stale.", "Reload semantic, Resource, and Definition Catalogs and recreate the registration plan.", "expectedHashes");
            if (!string.Equals(request.PromotionReceiptHash, promotionReceipt.ReceiptHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(promotionReceipt.PromotionPlanHash, promotionPlan.PlanHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(promotionReceipt.ReceiptHash, VfxCreativeFlywheelHash.ComputePromotionReceiptHash(promotionReceipt), StringComparison.OrdinalIgnoreCase))
                Add(plan, VfxFlywheelDiagnosticCodes.ReceiptTampered, "Promotion receipt is tampered or not bound to the requested registration.", "Use the exact validated promotion receipt.", "promotionReceiptHash");
            if (!string.Equals(promotionReceipt.ResultReviewStatus, VfxAuthoringReviewStatuses.ReviewNeeded, StringComparison.Ordinal))
                Add(plan, VfxFlywheelDiagnosticCodes.ReadyForbidden, "Registration may only consume ReviewNeeded promotion output.", "Do not fabricate Ready through registration.", "resultReviewStatus");
            if (!VfxCandidatePathPolicy.IsFormalDestination(request.PublishedPrefabPath) || !ReceiptContainsDestination(promotionReceipt, request.PublishedPrefabPath))
                Add(plan, VfxFlywheelDiagnosticCodes.SandboxViolation, "Published Prefab path is not a promoted formal asset.", "Reference a destination proven by the promotion receipt.", "publishedPrefabPath");
            if (!IsStableId(request.EffectId) || request.ResourceKey == null || !IsStableId(request.ResourceKey.Id) || !string.Equals(request.ResourceKey.TypeId, "GameObject", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(request.ResourceKey.PackageId) || request.DefinitionId < 0)
                Add(plan, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Published Effect identity, ResourceKey, or Definition id is invalid.", "Provide stable ids and a package-scoped GameObject ResourceKey.", "effect");
            for (int i = 0; i < catalog.Effects.Count; i++)
            {
                VfxAuthoringEffect effect = catalog.Effects[i];
                if (effect == null) continue;
                if (string.Equals(effect.EffectId, request.EffectId, StringComparison.Ordinal) || effect.ResourceKey != null && request.ResourceKey != null && string.Equals(effect.ResourceKey.ToStableKey(), request.ResourceKey.ToStableKey(), StringComparison.Ordinal) || request.DefinitionId > 0 && effect.DefinitionId == request.DefinitionId)
                    Add(plan, VfxFlywheelDiagnosticCodes.PromotionConflict, "Effect id, ResourceKey, or Definition id already exists: " + effect.EffectId, "Resolve identity ownership before registration.", "effectId");
            }
            if (!string.IsNullOrWhiteSpace(request.FallbackEffectId) && !ContainsEffect(catalog, request.FallbackEffectId))
                Add(plan, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Fallback Effect is missing from the semantic Catalog.", "Register/use an existing reviewed fallback.", "fallbackEffectId");
            if (!plan.Blocked)
            {
                plan.WouldModifySemanticCatalog = true;
                plan.WouldModifyResourceCatalog = true;
                plan.WouldModifyDefinitionCatalog = request.DefinitionId > 0;
                plan.WouldModifyRuntimeAuthority = false;
                plan.Diagnostics.Add(VfxAuthoringCatalogValidator.CreateDiagnostic(CharacterAuthoringValidationSeverity.Info, VfxFlywheelDiagnosticCodes.RegistrationGuardRequired, request.EffectId, request.ResourceKey.ToStableKey(), "Registration requires three separate guarded Catalog apply operations; this plan wrote nothing.", "Run validation/dry-run/hash guard/confirmation for each authoritative Catalog.", "registration"));
            }
            return Finish(plan);
        }

        private static bool ReceiptContainsDestination(VfxPromotionReceipt receipt, string path)
        {
            for (int i = 0; i < receipt.Assets.Count; i++) if (receipt.Assets[i] != null && string.Equals(receipt.Assets[i].DestinationAssetPath, path, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool ContainsEffect(VfxAuthoringCatalogDocument catalog, string id)
        {
            for (int i = 0; i < catalog.Effects.Count; i++) if (catalog.Effects[i] != null && string.Equals(catalog.Effects[i].EffectId, id, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsStableId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int i = 0; i < value.Length; i++) { char c = value[i]; if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')) return false; }
            return true;
        }

        private static void Add(VfxPublishedRegistrationPlan plan, string code, string message, string fix, string field) => plan.Diagnostics.Add(VfxAuthoringCatalogValidator.CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, code, plan.EffectId, string.Empty, message, fix, field));
        private static VfxPublishedRegistrationPlan Finish(VfxPublishedRegistrationPlan plan)
        {
            plan.Diagnostics.Sort((left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));
            plan.PlanHash = VfxCreativeFlywheelHash.ComputeRegistrationPlanHash(plan);
            return plan;
        }
    }
}
