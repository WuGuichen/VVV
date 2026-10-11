using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MxFramework.Editor
{
    /// <summary>Executes only hash-valid Core promotion plans with bound evaluation and human-selection evidence.</summary>
    /// <remarks>Editor-only and AllocByDesign. Recovery reconstructs paths from immutable same-session snapshots.</remarks>
    internal sealed class VfxGuardedPromotionExecutor : IVfxPromotionExecutor
    {
        private const string EvidenceFolder = "PromotionEvidence";
        private readonly IVfxPromotionSelectionAuthorityVerifier _selectionAuthorityVerifier;
        private readonly IVfxPromotionHumanConfirmation _humanConfirmation;
        private readonly Action<string, VfxCorePromotionReceipt> _coreReceiptWriter;
        private readonly Action<string, VfxPromotionTransactionReceipt> _transactionWriter;

        public VfxGuardedPromotionExecutor()
            : this(null, new VfxEditorPromotionHumanConfirmation())
        {
        }

        internal VfxGuardedPromotionExecutor(
            IVfxPromotionSelectionAuthorityVerifier selectionAuthorityVerifier,
            IVfxPromotionHumanConfirmation humanConfirmation,
            Action<string, VfxCorePromotionReceipt> coreReceiptWriter = null,
            Action<string, VfxPromotionTransactionReceipt> transactionWriter = null)
        {
            _selectionAuthorityVerifier = selectionAuthorityVerifier;
            _humanConfirmation = humanConfirmation ?? throw new ArgumentNullException(nameof(humanConfirmation));
            _coreReceiptWriter = coreReceiptWriter ?? WriteCoreReceipt;
            _transactionWriter = transactionWriter ?? WriteTransaction;
        }

        public VfxPromotionResult Promote(VfxAuthorizedPromotionRequest request)
        {
            if (!TryValidateInitialRequest(request, out PromotionEvidence evidence, out string error))
                return Failure(error, string.Empty, "Rejected");
            if (!TryVerifySelectionAuthority(evidence.PromotionPlan, out error))
                return Failure(error, string.Empty, "Rejected");
            try
            {
                if (!_humanConfirmation.Confirm(evidence.PromotionPlan))
                    return Failure("VFX promotion was cancelled by explicit human confirmation.", string.Empty, "Cancelled");
            }
            catch (Exception exception)
            {
                return Failure("VFX promotion confirmation failed: " + exception.Message, string.Empty, "Rejected");
            }
            string confirmedPromotionPlanHash = evidence.PromotionPlan.PlanHash;

            string transactionPath = evidence.SessionRoot + "/promotion_" + request.TransactionId + ".json";
            string snapshotRoot = evidence.SessionRoot + "/" + EvidenceFolder + "/" + request.TransactionId;
            if (File.Exists(transactionPath) || AssetDatabase.IsValidFolder(snapshotRoot))
                return Failure("Promotion transaction already exists: " + request.TransactionId, transactionPath, "Rejected");

            var createdFolders = new List<string>();
            VfxPromotionTransactionReceipt transaction = null;
            try
            {
                WriteSnapshots(request, snapshotRoot);
                if (!TryLoadEvidence(
                        snapshotRoot + "/generation_plan.json",
                        evidence.SessionRoot + "/candidate_receipt.json",
                        snapshotRoot + "/evaluation_report.json",
                        snapshotRoot + "/promotion_plan.json",
                        snapshotRoot + "/human_selection.json",
                        false,
                        out evidence,
                        out error))
                    throw new InvalidOperationException("Promotion evidence snapshot validation failed: " + error);
                if (!string.Equals(evidence.PromotionPlan.PlanHash, confirmedPromotionPlanHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Promotion plan changed after explicit human confirmation.");
                if (!TryVerifySelectionAuthority(evidence.PromotionPlan, out error))
                    throw new InvalidOperationException(error);

                transaction = CreateTransaction(request.TransactionId, evidence);
                _transactionWriter(transactionPath, transaction);
                transaction.Status = "Moving";
                for (int i = 0; i < evidence.PromotionPlan.Moves.Count; i++)
                {
                    VfxCorePromotionMove move = evidence.PromotionPlan.Moves[i];
                    string parent = Path.GetDirectoryName(move.DestinationAssetPath)?.Replace('\\', '/');
                    VfxCandidateSandbox.EnsureAssetFolder(parent, createdFolders);
                    string moveError = AssetDatabase.MoveAsset(move.SourceAssetPath, move.DestinationAssetPath);
                    if (!string.IsNullOrEmpty(moveError))
                        throw new InvalidOperationException("Unity failed to execute authorized VFX promotion move: " + moveError);
                    string movedGuid = AssetDatabase.AssetPathToGUID(move.DestinationAssetPath);
                    string movedDependencyHash = AssetDatabase.GetAssetDependencyHash(move.DestinationAssetPath).ToString();
                    if (!string.Equals(movedGuid, move.ExpectedUnityGuid, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(movedGuid, transaction.Moves[i].ObservedGuid, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(movedDependencyHash, move.ExpectedDependencyHash, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(VfxCandidateSandbox.ComputeAssetFileHash(move.DestinationAssetPath), move.ExpectedContentHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("VFX promotion GUID/content/dependency integrity failed after move: " + move.DestinationAssetPath);
                    transaction.Moves[i].Moved = true;
                    transaction.Message = "Moved " + (i + 1) + "/" + evidence.PromotionPlan.Moves.Count;
                    _transactionWriter(transactionPath, transaction);
                }

                AssetDatabase.SaveAssets();
                VfxCorePromotionReceipt coreReceipt = CreateCorePromotionReceipt(evidence.PromotionPlan);
                string coreReceiptPath = evidence.SessionRoot + "/promotion_receipt_" + request.TransactionId + ".json";
                using (var coreReceiptTransaction = new VfxProvisionalPromotionReceiptTransaction(
                    coreReceiptPath,
                    _coreReceiptWriter))
                {
                    coreReceiptTransaction.Write(coreReceipt);
                    transaction.Status = "Committed";
                    transaction.Message = "Authorized Core promotion committed with GUID preservation. Result remains ReviewNeeded.";
                    _transactionWriter(transactionPath, transaction);
                    coreReceiptTransaction.Complete();
                }
                var result = new VfxPromotionResult
                {
                    Success = true,
                    Message = transaction.Message,
                    TransactionReceiptAssetPath = transactionPath,
                    CorePromotionReceiptAssetPath = coreReceiptPath,
                    CorePromotionReceiptHash = coreReceipt.ReceiptHash,
                    Status = transaction.Status
                };
                for (int i = 0; i < evidence.PromotionPlan.Moves.Count; i++)
                    result.PromotedAssetPaths.Add(evidence.PromotionPlan.Moves[i].DestinationAssetPath);
                return result;
            }
            catch (Exception exception)
            {
                string rollbackError = string.Empty;
                bool rolledBack = transaction != null && TryRollback(evidence, createdFolders, out rollbackError);
                if (transaction == null && AssetDatabase.IsValidFolder(snapshotRoot))
                    AssetDatabase.DeleteAsset(snapshotRoot);
                if (transaction != null)
                {
                    transaction.Status = rolledBack ? "RolledBack" : "RecoveryRequired";
                    transaction.Message = rolledBack
                        ? exception.Message + " Promotion was rolled back."
                        : exception.Message + " Automatic rollback was incomplete: " + rollbackError;
                    TryWriteTransaction(transactionPath, transaction);
                }
                return Failure(
                    transaction?.Message ?? exception.Message,
                    transactionPath,
                    transaction?.Status ?? "Rejected");
            }
        }

        public VfxPromotionResult Recover(string transactionReceiptAssetPath)
        {
            string transactionPath = VfxCandidateSandbox.NormalizeAssetPath(transactionReceiptAssetPath);
            if (!VfxCandidateSandbox.TryGetCandidateSessionRoot(transactionPath, out string sessionRoot, out string sessionId)
                || !TryGetTransactionId(transactionPath, sessionRoot, out string transactionId)
                || !File.Exists(transactionPath))
                return Failure("Promotion recovery requires a direct transaction receipt under one candidate session.", transactionPath, "Rejected");
            try
            {
                JObject transactionDocument = JObject.Parse(File.ReadAllText(transactionPath));
                if (ContainsEditablePathField(transactionDocument))
                    return Failure("Promotion transaction contains editable source/destination/path fields and is untrusted.", transactionPath, "Rejected");
                VfxPromotionTransactionReceipt transaction = transactionDocument.ToObject<VfxPromotionTransactionReceipt>();
                if (transaction == null
                    || !string.Equals(transaction.SchemaVersion, VfxCandidateProtocol.PromotionSchemaVersion, StringComparison.Ordinal)
                    || !string.Equals(transaction.SessionId, sessionId, StringComparison.Ordinal))
                    return Failure("Promotion transaction schema/session binding is invalid.", transactionPath, "Rejected");
                if (string.Equals(transaction.Status, "Committed", StringComparison.Ordinal))
                    return Failure("Committed promotions require a separate audited revert plan; recovery is for interrupted moves only.", transactionPath, "Committed");

                string snapshotRoot = sessionRoot + "/" + EvidenceFolder + "/" + transactionId;
                if (!TryLoadEvidence(
                        snapshotRoot + "/generation_plan.json",
                        sessionRoot + "/candidate_receipt.json",
                        snapshotRoot + "/evaluation_report.json",
                        snapshotRoot + "/promotion_plan.json",
                        snapshotRoot + "/human_selection.json",
                        true,
                        out PromotionEvidence evidence,
                        out string error))
                    return Failure("Promotion recovery evidence is invalid: " + error, transactionPath, "RecoveryRequired");
                if (!TransactionMatchesEvidence(transaction, evidence))
                    return Failure("Promotion transaction hashes do not match its fixed same-session Core evidence snapshots.", transactionPath, "Rejected");

                bool rolledBack = TryRollback(evidence, null, out string rollbackError);
                transaction.Moves = ObserveMoves(evidence);
                transaction.Status = rolledBack ? "RolledBack" : "RecoveryRequired";
                transaction.Message = rolledBack
                    ? "Recovery reconstructed the Core move plan and restored every available asset to its candidate source."
                    : "Recovery remains incomplete: " + rollbackError;
                _transactionWriter(transactionPath, transaction);
                return new VfxPromotionResult
                {
                    Success = rolledBack,
                    Message = transaction.Message,
                    TransactionReceiptAssetPath = transactionPath,
                    Status = transaction.Status
                };
            }
            catch (Exception exception)
            {
                return Failure(exception.Message, transactionPath, "RecoveryRequired");
            }
        }

        private static bool TryValidateInitialRequest(VfxAuthorizedPromotionRequest request, out PromotionEvidence evidence, out string error)
        {
            evidence = null;
            error = string.Empty;
            if (request == null || !IsSafeIdentifier(request.TransactionId))
            {
                error = "Authorized promotion request and stable transactionId are required.";
                return false;
            }
            if (!VfxCandidateSandbox.IsControlledEvidencePath(request.GenerationPlanPath)
                || !VfxCandidateSandbox.IsControlledEvidencePath(request.EvaluationReportPath)
                || !VfxCandidateSandbox.IsControlledEvidencePath(request.PromotionPlanPath)
                || !VfxCandidateSandbox.IsControlledEvidencePath(request.HumanSelectionEvidencePath))
            {
                error = "Core plan, evaluation, promotion, and human-selection inputs must be under Temp/MxFramework/VfxAuthoring.";
                return false;
            }
            if (!VfxCandidateSandbox.TryGetCandidateSessionRoot(request.CandidateReceiptPath, out string sessionRoot, out _)
                || !string.Equals(VfxCandidateSandbox.NormalizeAssetPath(request.CandidateReceiptPath), sessionRoot + "/candidate_receipt.json", StringComparison.Ordinal))
            {
                error = "Core candidate receipt must use the fixed candidate_receipt.json path in its physical session root.";
                return false;
            }
            return TryLoadEvidence(
                request.GenerationPlanPath,
                request.CandidateReceiptPath,
                request.EvaluationReportPath,
                request.PromotionPlanPath,
                request.HumanSelectionEvidencePath,
                false,
                out evidence,
                out error);
        }

        private static bool TryLoadEvidence(
            string generationPlanPath,
            string candidateReceiptPath,
            string evaluationReportPath,
            string promotionPlanPath,
            string selectionEvidencePath,
            bool allowMovedAssets,
            out PromotionEvidence evidence,
            out string error)
        {
            evidence = null;
            error = string.Empty;
            try
            {
                RequireFiles(generationPlanPath, candidateReceiptPath, evaluationReportPath, promotionPlanPath, selectionEvidencePath);
                JObject generationDocument = JObject.Parse(File.ReadAllText(generationPlanPath));
                VfxCoreGenerationPlan generation = generationDocument.ToObject<VfxCoreGenerationPlan>();
                VfxCoreCandidateReceiptAdapter.ValidatePlan(generation, generationDocument);
                if (string.Equals(generation.GenerationMode, "ExperimentalGraph", StringComparison.Ordinal))
                    throw new InvalidOperationException("Experimental Graph generation plans are Draft-only and cannot be promoted.");
                if (!VfxCandidateSandbox.TryGetSessionRoot(generation.SessionId, out string sessionRoot, out string sessionError))
                    throw new InvalidOperationException(sessionError);
                if (!string.Equals(VfxCandidateSandbox.NormalizeAssetPath(generation.SandboxRoot), sessionRoot, StringComparison.Ordinal))
                    throw new InvalidOperationException("Generation plan sandbox/session binding is invalid.");
                if (!string.Equals(VfxCandidateSandbox.NormalizeAssetPath(candidateReceiptPath), sessionRoot + "/candidate_receipt.json", StringComparison.Ordinal))
                    throw new InvalidOperationException("Candidate receipt is outside the generation plan's fixed session location.");

                JObject receiptDocument = JObject.Parse(File.ReadAllText(candidateReceiptPath));
                VfxCoreCandidateReceipt receipt = receiptDocument.ToObject<VfxCoreCandidateReceipt>();
                ValidateCandidateReceipt(generation, receipt, receiptDocument, sessionRoot);

                JObject evaluationDocument = JObject.Parse(File.ReadAllText(evaluationReportPath));
                VfxCoreEvaluationReport evaluation = evaluationDocument.ToObject<VfxCoreEvaluationReport>();
                ValidateEvaluation(generation, receipt, evaluation, evaluationDocument);

                JObject promotionDocument = JObject.Parse(File.ReadAllText(promotionPlanPath));
                VfxCorePromotionPlan promotion = promotionDocument.ToObject<VfxCorePromotionPlan>();
                ValidatePromotionPlan(generation, receipt, evaluation, promotion, promotionDocument);

                VfxHumanSelectionEvidence selection = JsonConvert.DeserializeObject<VfxHumanSelectionEvidence>(File.ReadAllText(selectionEvidencePath));
                ValidateSelection(selection, selectionEvidencePath, promotion);
                if (!string.Equals(VfxCandidateSandbox.ComputeAssetFileHash(VfxCandidateProtocol.AuthoringCatalogPath), promotion.ExpectedCatalogHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Authoring Catalog changed after Core promotion planning.");

                var context = new PromotionEvidence
                {
                    SessionRoot = sessionRoot,
                    SessionId = generation.SessionId,
                    GenerationPlan = generation,
                    CandidateReceipt = receipt,
                    Evaluation = evaluation,
                    PromotionPlan = promotion,
                    Selection = selection
                };
                ValidateMoves(context, allowMovedAssets);
                evidence = context;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static void ValidateCandidateReceipt(VfxCoreGenerationPlan generation, VfxCoreCandidateReceipt receipt, JObject document, string sessionRoot)
        {
            if (receipt == null || !string.Equals(receipt.SchemaVersion, VfxCandidateProtocol.CoreCandidateReceiptSchemaVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("Unsupported Core candidate receipt schema.");
            string actualHash = VfxCandidateSandbox.ComputeCoreDocumentHash(document, "receiptHash");
            if (!VfxCandidateSandbox.IsSha256(receipt.ReceiptHash) || !string.Equals(receipt.ReceiptHash, actualHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Core candidate receipt hash is invalid.");
            if (!string.Equals(receipt.SessionId, generation.SessionId, StringComparison.Ordinal)
                || !string.Equals(receipt.GenerationPlanHash, generation.PlanHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(receipt.CatalogHash, generation.CatalogHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(receipt.GenerationTemplateCatalogHash, generation.GenerationTemplateCatalogHash, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(receipt.ExecutorId) || string.IsNullOrWhiteSpace(receipt.ExecutorVersion))
                throw new InvalidOperationException("Core candidate receipt input/executor bindings are invalid.");
            var candidates = new Dictionary<string, VfxCoreGenerationCandidateSpec>(StringComparer.Ordinal);
            for (int i = 0; i < generation.Candidates.Count; i++)
                if (generation.Candidates[i] != null) candidates[generation.Candidates[i].CandidateId] = generation.Candidates[i];
            for (int i = 0; i < receipt.Assets.Count; i++)
            {
                VfxCoreCandidateAssetRecord asset = receipt.Assets[i];
                if (asset == null || !candidates.TryGetValue(asset.CandidateId, out VfxCoreGenerationCandidateSpec candidate)
                    || !IsAtOrBelow(asset.AssetPath, candidate.OutputRoot) || !IsAtOrBelow(asset.AssetPath, sessionRoot)
                    || !VfxCandidateSandbox.IsSha256(asset.ContentHash)
                    || !VfxCandidateSandbox.IsUnityHash128(asset.UnityGuid)
                    || !VfxCandidateSandbox.IsUnityHash128(asset.DependencyHash))
                    throw new InvalidOperationException("Core candidate receipt contains an unknown, escaped, unhashed, or unbound Unity asset.");
                VfxCoreAssetProvenance provenance = asset.Provenance;
                if (provenance == null
                    || !string.Equals(provenance.ProviderId, candidate.ProviderId, StringComparison.Ordinal)
                    || !string.Equals(provenance.ProviderVersion, candidate.ProviderVersion, StringComparison.Ordinal)
                    || !string.Equals(provenance.TemplateId, candidate.TemplateId, StringComparison.Ordinal)
                    || !string.Equals(provenance.TemplateVersion, candidate.TemplateVersion, StringComparison.Ordinal)
                    || !string.Equals(provenance.ModelId, candidate.ModelId, StringComparison.Ordinal)
                    || !string.Equals(provenance.ModelVersion, candidate.ModelVersion, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(provenance.LicenseId) || string.IsNullOrWhiteSpace(provenance.SourceUri)
                    || !VfxCandidateSandbox.IsSha256(provenance.SourceContentHash))
                    throw new InvalidOperationException("Core candidate receipt provenance is incomplete or differs from the generation plan.");
            }
        }

        private static void ValidateEvaluation(VfxCoreGenerationPlan generation, VfxCoreCandidateReceipt receipt, VfxCoreEvaluationReport evaluation, JObject document)
        {
            if (evaluation == null || !string.Equals(evaluation.SchemaVersion, VfxCandidateProtocol.CoreEvaluationReportSchemaVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("Unsupported Core evaluation report schema.");
            string actualHash = VfxCandidateSandbox.ComputeCoreDocumentHash(document, "reportHash", "diagnostics", "blocked");
            if (!VfxCandidateSandbox.IsSha256(evaluation.ReportHash) || !string.Equals(evaluation.ReportHash, actualHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(evaluation.GenerationPlanHash, generation.PlanHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(evaluation.CandidateReceiptHash, receipt.ReceiptHash, StringComparison.OrdinalIgnoreCase)
                || evaluation.Blocked || VfxCandidateSandbox.HasErrorDiagnostics(evaluation.Diagnostics))
                throw new InvalidOperationException("Core evaluation report is blocked, stale, or hash-invalid.");
        }

        private static void ValidatePromotionPlan(VfxCoreGenerationPlan generation, VfxCoreCandidateReceipt receipt, VfxCoreEvaluationReport evaluation, VfxCorePromotionPlan promotion, JObject document)
        {
            if (promotion == null || !string.Equals(promotion.SchemaVersion, VfxCandidateProtocol.CorePromotionPlanSchemaVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("Unsupported Core promotion plan schema.");
            string actualHash = VfxCandidateSandbox.ComputeCoreDocumentHash(document, "planHash", "diagnostics", "blocked");
            if (!VfxCandidateSandbox.IsSha256(promotion.PlanHash) || !string.Equals(promotion.PlanHash, actualHash, StringComparison.OrdinalIgnoreCase)
                || promotion.Blocked || VfxCandidateSandbox.HasErrorDiagnostics(promotion.Diagnostics))
                throw new InvalidOperationException("Core promotion plan is blocked, stale, or hash-invalid.");
            if (!string.Equals(promotion.GenerationPlanHash, generation.PlanHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(promotion.CandidateReceiptHash, receipt.ReceiptHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(promotion.EvaluationReportHash, evaluation.ReportHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Core promotion plan evidence bindings are invalid.");
            if (!string.Equals(promotion.ResultReviewStatus, "ReviewNeeded", StringComparison.Ordinal)
                || !promotion.RequiresUnityEditor || !promotion.RequiresHumanConfirmation
                || string.IsNullOrWhiteSpace(promotion.SelectedBy) || !VfxCandidateSandbox.IsSha256(promotion.SelectionEvidenceHash))
                throw new InvalidOperationException("Promotion may produce at most ReviewNeeded and requires bound human confirmation.");
            VfxCoreEvaluationCandidate selected = FindEvaluation(evaluation, promotion.CandidateId);
            if (selected == null || !selected.HardGatesPassed || !selected.Shortlisted)
                throw new InvalidOperationException("Selected candidate did not pass authoritative hard gates and shortlist evaluation.");
            if (!IsStableTarget(promotion.TargetKind, promotion.TargetStableId))
                throw new InvalidOperationException("Core promotion target kind/stable id is invalid.");
            if (string.Equals(promotion.TargetKind, "Primitive", StringComparison.Ordinal))
            {
                var consumers = new HashSet<string>(promotion.ConsumerRecipeIds ?? new List<string>(), StringComparer.Ordinal);
                consumers.Remove(string.Empty);
                if (consumers.Count < 2 && !promotion.StrategicFoundationExceptionApproved)
                    throw new InvalidOperationException("Primitive promotion lacks two consumers or its bound strategic-foundation exception.");
            }
        }

        private static void ValidateSelection(VfxHumanSelectionEvidence selection, string path, VfxCorePromotionPlan promotion)
        {
            string evidenceHash = VfxCandidateSandbox.ComputeAssetFileHash(path);
            if (selection == null || !string.Equals(selection.SchemaVersion, VfxCandidateProtocol.SelectionEvidenceSchemaVersion, StringComparison.Ordinal)
                || !string.Equals(evidenceHash, promotion.SelectionEvidenceHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(selection.CandidateId, promotion.CandidateId, StringComparison.Ordinal)
                || !string.Equals(selection.SelectedBy, promotion.SelectedBy, StringComparison.Ordinal)
                || !string.Equals(selection.GenerationPlanHash, promotion.GenerationPlanHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(selection.CandidateReceiptHash, promotion.CandidateReceiptHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(selection.EvaluationReportHash, promotion.EvaluationReportHash, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(selection.SelectionReason))
                throw new InvalidOperationException("Human selection evidence is missing, stale, or not bound to the Core promotion plan.");
        }

        private static void ValidateMoves(PromotionEvidence evidence, bool allowMovedAssets)
        {
            var selectedAssets = new Dictionary<string, VfxCoreCandidateAssetRecord>(StringComparer.Ordinal);
            for (int i = 0; i < evidence.CandidateReceipt.Assets.Count; i++)
            {
                VfxCoreCandidateAssetRecord asset = evidence.CandidateReceipt.Assets[i];
                if (string.Equals(asset.CandidateId, evidence.PromotionPlan.CandidateId, StringComparison.Ordinal))
                    selectedAssets.Add(VfxCandidateSandbox.NormalizeAssetPath(asset.AssetPath), asset);
            }
            if (selectedAssets.Count == 0 || selectedAssets.Count != evidence.PromotionPlan.Moves.Count)
                throw new InvalidOperationException("Core promotion moves do not cover exactly the selected candidate assets.");
            var destinations = new HashSet<string>(StringComparer.Ordinal);
            evidence.ObservedGuids.Clear();
            for (int i = 0; i < evidence.PromotionPlan.Moves.Count; i++)
            {
                VfxCorePromotionMove move = evidence.PromotionPlan.Moves[i];
                string source = VfxCandidateSandbox.NormalizeAssetPath(move.SourceAssetPath);
                string destination = VfxCandidateSandbox.NormalizeAssetPath(move.DestinationAssetPath);
                if (!selectedAssets.TryGetValue(source, out VfxCoreCandidateAssetRecord asset)
                    || !string.Equals(asset.ContentHash, move.ExpectedContentHash, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(asset.UnityGuid, move.ExpectedUnityGuid, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(asset.DependencyHash, move.ExpectedDependencyHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Core promotion move is not backed by the selected candidate receipt: " + source);
                if (!VfxCandidateSandbox.IsFormalVfxDestination(destination) || !destinations.Add(destination))
                    throw new InvalidOperationException("Core promotion destination must be unique under Assets/VFX and outside MxCandidates: " + destination);
                bool sourceExists = AssetDatabase.LoadMainAssetAtPath(source) != null && File.Exists(source);
                bool destinationExists = AssetDatabase.LoadMainAssetAtPath(destination) != null && File.Exists(destination);
                if (!allowMovedAssets && (!sourceExists || destinationExists))
                    throw new InvalidOperationException("Authorized promotion requires every source present and every destination absent: " + source);
                if (allowMovedAssets && sourceExists == destinationExists)
                    throw new InvalidOperationException("Recovery requires exactly one source/destination copy for each move: " + source);
                string observedPath = sourceExists ? source : destination;
                if (!string.Equals(VfxCandidateSandbox.ComputeAssetFileHash(observedPath), move.ExpectedContentHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Promotion move content hash does not match its observed asset: " + observedPath);
                string guid = AssetDatabase.AssetPathToGUID(observedPath);
                string dependencyHash = AssetDatabase.GetAssetDependencyHash(observedPath).ToString();
                if (!string.Equals(guid, move.ExpectedUnityGuid, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Promotion move Unity GUID differs from generation evidence: " + observedPath);
                if (!string.Equals(dependencyHash, move.ExpectedDependencyHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Promotion move dependency hash differs from generation evidence: " + observedPath);
                evidence.ObservedGuids.Add(guid);
            }
        }

        private static void WriteSnapshots(VfxAuthorizedPromotionRequest request, string snapshotRoot)
        {
            VfxCandidateSandbox.EnsureAssetFolder(snapshotRoot);
            CopyEvidence(request.GenerationPlanPath, snapshotRoot + "/generation_plan.json");
            CopyEvidence(request.EvaluationReportPath, snapshotRoot + "/evaluation_report.json");
            CopyEvidence(request.PromotionPlanPath, snapshotRoot + "/promotion_plan.json");
            CopyEvidence(request.HumanSelectionEvidencePath, snapshotRoot + "/human_selection.json");
            AssetDatabase.SaveAssets();
        }

        private static void CopyEvidence(string source, string destination)
        {
            if (File.Exists(destination))
                throw new InvalidOperationException("Promotion evidence snapshot already exists: " + destination);
            File.WriteAllBytes(destination, File.ReadAllBytes(source));
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static VfxPromotionTransactionReceipt CreateTransaction(string id, PromotionEvidence evidence)
        {
            return new VfxPromotionTransactionReceipt
            {
                TransactionId = id,
                SessionId = evidence.SessionId,
                GenerationPlanHash = evidence.GenerationPlan.PlanHash,
                CandidateReceiptHash = evidence.CandidateReceipt.ReceiptHash,
                EvaluationReportHash = evidence.Evaluation.ReportHash,
                PromotionPlanHash = evidence.PromotionPlan.PlanHash,
                SelectionEvidenceHash = evidence.PromotionPlan.SelectionEvidenceHash,
                Moves = ObserveMoves(evidence)
            };
        }

        private static List<VfxPromotionMoveRecord> ObserveMoves(PromotionEvidence evidence)
        {
            var records = new List<VfxPromotionMoveRecord>(evidence.PromotionPlan.Moves.Count);
            for (int i = 0; i < evidence.PromotionPlan.Moves.Count; i++)
            {
                string source = evidence.PromotionPlan.Moves[i].SourceAssetPath;
                bool sourceExists = AssetDatabase.LoadMainAssetAtPath(source) != null && File.Exists(source);
                records.Add(new VfxPromotionMoveRecord
                {
                    MoveIndex = i,
                    ObservedGuid = evidence.ObservedGuids.Count > i ? evidence.ObservedGuids[i] : string.Empty,
                    Moved = !sourceExists
                });
            }
            return records;
        }

        private static bool TryRollback(PromotionEvidence evidence, List<string> createdFolders, out string error)
        {
            error = string.Empty;
            bool success = true;
            for (int i = evidence.PromotionPlan.Moves.Count - 1; i >= 0; i--)
            {
                VfxCorePromotionMove move = evidence.PromotionPlan.Moves[i];
                bool sourceExists = AssetDatabase.LoadMainAssetAtPath(move.SourceAssetPath) != null && File.Exists(move.SourceAssetPath);
                bool destinationExists = AssetDatabase.LoadMainAssetAtPath(move.DestinationAssetPath) != null && File.Exists(move.DestinationAssetPath);
                if (sourceExists && !destinationExists)
                    continue;
                if (!sourceExists && destinationExists)
                {
                    string parent = Path.GetDirectoryName(move.SourceAssetPath)?.Replace('\\', '/');
                    VfxCandidateSandbox.EnsureAssetFolder(parent);
                    string moveError = AssetDatabase.MoveAsset(move.DestinationAssetPath, move.SourceAssetPath);
                    if (string.IsNullOrEmpty(moveError)
                        && string.Equals(AssetDatabase.AssetPathToGUID(move.SourceAssetPath), move.ExpectedUnityGuid, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(AssetDatabase.GetAssetDependencyHash(move.SourceAssetPath).ToString(), move.ExpectedDependencyHash, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(VfxCandidateSandbox.ComputeAssetFileHash(move.SourceAssetPath), move.ExpectedContentHash, StringComparison.OrdinalIgnoreCase))
                        continue;
                    success = false;
                    error += " Could not restore " + move.DestinationAssetPath + ": " + moveError;
                    continue;
                }
                success = false;
                error += sourceExists
                    ? " Both source and destination exist for " + move.SourceAssetPath + "."
                    : " Neither source nor destination exists for " + move.SourceAssetPath + ".";
            }
            AssetDatabase.SaveAssets();
            if (success && createdFolders != null)
            {
                for (int i = createdFolders.Count - 1; i >= 0; i--)
                    DeleteFolderIfEmpty(createdFolders[i]);
            }
            return success;
        }

        private static VfxCorePromotionReceipt CreateCorePromotionReceipt(VfxCorePromotionPlan plan)
        {
            var receipt = new VfxCorePromotionReceipt
            {
                PromotionPlanHash = plan.PlanHash,
                CatalogHash = plan.ExpectedCatalogHash,
                ResultReviewStatus = "ReviewNeeded"
            };
            for (int i = 0; i < plan.Moves.Count; i++)
            {
                VfxCorePromotionMove move = plan.Moves[i];
                receipt.Assets.Add(new VfxCorePromotedAssetRecord
                {
                    SourceAssetPath = move.SourceAssetPath,
                    DestinationAssetPath = move.DestinationAssetPath,
                    ContentHash = move.ExpectedContentHash,
                    UnityGuid = move.ExpectedUnityGuid,
                    DependencyHash = move.ExpectedDependencyHash
                });
            }
            receipt.ReceiptHash = VfxCandidateSandbox.ComputeCoreDocumentHash(receipt, "receiptHash");
            return receipt;
        }

        private static bool TransactionMatchesEvidence(VfxPromotionTransactionReceipt transaction, PromotionEvidence evidence)
        {
            return string.Equals(transaction.GenerationPlanHash, evidence.GenerationPlan.PlanHash, StringComparison.OrdinalIgnoreCase)
                && string.Equals(transaction.CandidateReceiptHash, evidence.CandidateReceipt.ReceiptHash, StringComparison.OrdinalIgnoreCase)
                && string.Equals(transaction.EvaluationReportHash, evidence.Evaluation.ReportHash, StringComparison.OrdinalIgnoreCase)
                && string.Equals(transaction.PromotionPlanHash, evidence.PromotionPlan.PlanHash, StringComparison.OrdinalIgnoreCase)
                && string.Equals(transaction.SelectionEvidenceHash, evidence.PromotionPlan.SelectionEvidenceHash, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsEditablePathField(JObject document)
        {
            foreach (JToken token in document.DescendantsAndSelf())
            {
                if (token is not JProperty property)
                    continue;
                string name = property.Name;
                if (name.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("destination", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static bool TryGetTransactionId(string path, string sessionRoot, out string id)
        {
            id = string.Empty;
            string prefix = sessionRoot + "/promotion_";
            if (!path.StartsWith(prefix, StringComparison.Ordinal) || !path.EndsWith(".json", StringComparison.Ordinal))
                return false;
            id = path.Substring(prefix.Length, path.Length - prefix.Length - ".json".Length);
            return IsSafeIdentifier(id) && path.IndexOf('/', prefix.Length) < 0;
        }

        private static bool IsSafeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_') return false;
            }
            return true;
        }

        private static bool IsStableTarget(string kind, string id)
        {
            if (!string.Equals(kind, "Primitive", StringComparison.Ordinal)
                && !string.Equals(kind, "Recipe", StringComparison.Ordinal)
                && !string.Equals(kind, "Effect", StringComparison.Ordinal)) return false;
            if (string.IsNullOrWhiteSpace(id)) return false;
            for (int i = 0; i < id.Length; i++)
            {
                char character = id[i];
                if (!((character >= 'a' && character <= 'z') || char.IsDigit(character) || character == '.' || character == '_' || character == '-')) return false;
            }
            return true;
        }

        private bool TryVerifySelectionAuthority(VfxCorePromotionPlan plan, out string error)
        {
            error = string.Empty;
            if (plan == null
                || !IsStableIdentifier(plan.SelectionAuthorityId)
                || !IsStableIdentifier(plan.SelectionAuthorityVersion)
                || !VfxCandidateSandbox.IsSha256(plan.SelectionAuthorityReceiptHash))
            {
                error = "Promotion requires a valid versioned selection-authority receipt.";
                return false;
            }
            if (_selectionAuthorityVerifier == null)
            {
                error = "Promotion is fail-closed because no trusted selection-authority verifier is configured.";
                return false;
            }
            if (!string.Equals(_selectionAuthorityVerifier.AuthorityId, plan.SelectionAuthorityId, StringComparison.Ordinal)
                || !string.Equals(_selectionAuthorityVerifier.AuthorityVersion, plan.SelectionAuthorityVersion, StringComparison.Ordinal))
            {
                error = "Promotion selection-authority id/version is not trusted by the configured verifier.";
                return false;
            }
            var claims = new VfxPromotionSelectionAuthorityClaims
            {
                CandidateId = plan.CandidateId,
                TargetKind = plan.TargetKind,
                TargetStableId = plan.TargetStableId,
                GenerationPlanHash = plan.GenerationPlanHash,
                CandidateReceiptHash = plan.CandidateReceiptHash,
                EvaluationReportHash = plan.EvaluationReportHash,
                SelectedBy = plan.SelectedBy,
                SelectionEvidenceHash = plan.SelectionEvidenceHash,
                AuthorityReceiptHash = plan.SelectionAuthorityReceiptHash,
                StrategicFoundationExceptionApproved = plan.StrategicFoundationExceptionApproved,
                ConsumerRecipeIds = new List<string>(plan.ConsumerRecipeIds ?? new List<string>())
            };
            try
            {
                if (_selectionAuthorityVerifier.Verify(claims, out string rejectionReason))
                    return true;
                error = "Promotion selection-authority receipt was rejected"
                    + (string.IsNullOrWhiteSpace(rejectionReason) ? "." : ": " + rejectionReason);
                return false;
            }
            catch (Exception exception)
            {
                error = "Promotion selection-authority verification failed: " + exception.Message;
                return false;
            }
        }

        private static bool IsStableIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!((character >= 'a' && character <= 'z')
                    || (character >= 'A' && character <= 'Z')
                    || char.IsDigit(character)
                    || character == '.'
                    || character == '_'
                    || character == '-'))
                    return false;
            }
            return true;
        }

        private static VfxCoreEvaluationCandidate FindEvaluation(VfxCoreEvaluationReport report, string candidateId)
        {
            for (int i = 0; i < report.Candidates.Count; i++)
                if (string.Equals(report.Candidates[i]?.CandidateId, candidateId, StringComparison.Ordinal)) return report.Candidates[i];
            return null;
        }

        private static bool IsAtOrBelow(string assetPath, string root)
        {
            string path = VfxCandidateSandbox.NormalizeAssetPath(assetPath);
            string normalizedRoot = VfxCandidateSandbox.NormalizeAssetPath(root).TrimEnd('/');
            return VfxCandidateSandbox.IsCandidateRoot(normalizedRoot)
                && (string.Equals(path, normalizedRoot, StringComparison.Ordinal) || path.StartsWith(normalizedRoot + "/", StringComparison.Ordinal));
        }

        private static void RequireFiles(params string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
                if (string.IsNullOrWhiteSpace(paths[i]) || !File.Exists(paths[i])) throw new FileNotFoundException("Promotion evidence file is missing.", paths[i]);
        }

        private static void DeleteFolderIfEmpty(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder) && Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0)
                AssetDatabase.DeleteAsset(folder);
        }

        private static void WriteCoreReceipt(string path, VfxCorePromotionReceipt receipt) => VfxCandidateSandbox.WriteJsonAsset(path, receipt);

        private static void WriteTransaction(string path, VfxPromotionTransactionReceipt transaction) => VfxCandidateSandbox.WriteJsonAsset(path, transaction);

        private void TryWriteTransaction(string path, VfxPromotionTransactionReceipt transaction)
        {
            try { _transactionWriter(path, transaction); }
            catch { /* Preserve the original failure; fixed evidence snapshots remain available for manual recovery. */ }
        }

        private static VfxPromotionResult Failure(string message, string transactionPath, string status)
        {
            return new VfxPromotionResult
            {
                Success = false,
                Message = message ?? string.Empty,
                TransactionReceiptAssetPath = transactionPath ?? string.Empty,
                Status = status ?? string.Empty
            };
        }

        private sealed class PromotionEvidence
        {
            public string SessionRoot = string.Empty;
            public string SessionId = string.Empty;
            public VfxCoreGenerationPlan GenerationPlan;
            public VfxCoreCandidateReceipt CandidateReceipt;
            public VfxCoreEvaluationReport Evaluation;
            public VfxCorePromotionPlan PromotionPlan;
            public VfxHumanSelectionEvidence Selection;
            public readonly List<string> ObservedGuids = new List<string>();
        }
    }

    internal sealed class VfxProvisionalPromotionReceiptTransaction : IDisposable
    {
        private readonly string _assetPath;
        private readonly Action<string, VfxCorePromotionReceipt> _writer;
        private readonly bool _hadExistingAsset;
        private readonly byte[] _originalContents;
        private readonly byte[] _originalMetaContents;
        private readonly string _originalGuid;
        private bool _writeAttempted;
        private bool _written;
        private bool _completed;

        internal VfxProvisionalPromotionReceiptTransaction(
            string assetPath,
            Action<string, VfxCorePromotionReceipt> writer)
        {
            _assetPath = VfxCandidateSandbox.NormalizeAssetPath(assetPath);
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _hadExistingAsset = File.Exists(_assetPath);
            if (_hadExistingAsset)
            {
                _originalContents = File.ReadAllBytes(_assetPath);
                _originalMetaContents = File.Exists(_assetPath + ".meta")
                    ? File.ReadAllBytes(_assetPath + ".meta")
                    : null;
                _originalGuid = AssetDatabase.AssetPathToGUID(_assetPath);
            }
        }

        internal void Write(VfxCorePromotionReceipt receipt)
        {
            _writeAttempted = true;
            try
            {
                _writer(_assetPath, receipt);
                if (!File.Exists(_assetPath))
                    throw new IOException("Core promotion receipt writer did not create the receipt asset.");
                JObject.Parse(File.ReadAllText(_assetPath));
                _written = true;
            }
            catch (Exception exception)
            {
                try
                {
                    RollBack();
                }
                catch (Exception rollbackException)
                {
                    throw new InvalidOperationException(
                        "Core promotion receipt write failed: " + exception.Message
                        + " Receipt rollback also failed: " + rollbackException.Message,
                        exception);
                }
                throw;
            }
        }

        internal void Complete()
        {
            if (!_written) throw new InvalidOperationException("Core promotion receipt has not been written.");
            _completed = true;
        }

        private void RollBack()
        {
            try
            {
                if (_hadExistingAsset)
                {
                    File.WriteAllBytes(_assetPath, _originalContents);
                    if (_originalMetaContents != null) File.WriteAllBytes(_assetPath + ".meta", _originalMetaContents);
                    else if (File.Exists(_assetPath + ".meta")) File.Delete(_assetPath + ".meta");
                    AssetDatabase.ImportAsset(_assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    if (!string.Equals(AssetDatabase.AssetPathToGUID(_assetPath), _originalGuid, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Restored Core promotion receipt GUID/meta did not match its prior state.");
                }
                else
                {
                    AssetDatabase.DeleteAsset(_assetPath);
                    if (File.Exists(_assetPath)) File.Delete(_assetPath);
                    if (File.Exists(_assetPath + ".meta")) File.Delete(_assetPath + ".meta");
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
            }
            finally
            {
                _writeAttempted = false;
                _written = false;
            }
        }

        public void Dispose()
        {
            if (_writeAttempted && !_completed) RollBack();
        }
    }

    internal sealed class VfxEditorPromotionHumanConfirmation : IVfxPromotionHumanConfirmation
    {
        public bool Confirm(VfxCorePromotionPlan plan)
        {
            if (plan == null)
                return false;
            return EditorUtility.DisplayDialog(
                "确认晋升 VFX 候选",
                "候选：" + plan.CandidateId
                + "\n目标：" + plan.TargetKind + " / " + plan.TargetStableId
                + "\n资产移动：" + plan.Moves.Count
                + "\n结果状态：ReviewNeeded"
                + "\n\n此操作将移动 Unity 资产并保留 GUID。是否继续？",
                "确认晋升",
                "取消");
        }
    }
}
