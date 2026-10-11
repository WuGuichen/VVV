using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static class VfxCreativeFlywheelTests
{
    public static void RunAll()
    {
        CatalogV1_RemainsReadable_AndV2ReadyDependenciesPropagate();
        GenerationTemplates_NormalizeQualityProfileAndHashDeterministically();
        ReusePriorityAndGenerationPlan_AreStable();
        CandidateReceipt_BlocksStaleTamperedUnlicensedAndEscapedAssets();
        EvaluationAndPromotion_AreDeterministicAndGuarded();
        Scanner_ObservesOnlyPromotionBoundQualifiedEvaluation();
        CliFlywheelCommands_DispatchEndToEndPlansAndReports();
    }

    private static void CatalogV1_RemainsReadable_AndV2ReadyDependenciesPropagate()
    {
        VfxAuthoringCatalogLoadResult v1 = VfxAuthoringCatalogJson.Load("{\"schemaVersion\":\"mx.vfx.authoring.v1\",\"catalogId\":\"test.vfx\",\"taxonomyVersion\":\"mx.vfx.taxonomy.v1\",\"strictCoverage\":false,\"primitives\":[],\"recipes\":[],\"effects\":[]}");
        Require(v1.Success && !VfxAuthoringCatalogValidator.Validate(v1.Catalog).HasErrors, "Catalog v1 must remain readable and valid.");

        VfxAuthoringCatalogDocument catalog = CreateReadyCatalog();
        Require(HasCode(VfxAuthoringCatalogValidator.Validate(catalog).Diagnostics, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid), "Ready entries must not pass when production observations are absent.");
        catalog.Primitives[0].ReviewStatus = VfxAuthoringReviewStatuses.ReviewNeeded;
        catalog.Primitives[0].Approval = null;
        VfxAuthoringCatalogValidationReport dependency = VfxAuthoringCatalogValidator.Validate(catalog);
        Require(HasCode(dependency.Diagnostics, VfxAuthoringDiagnosticCodes.ReadyDependencyInvalid), "Ready Recipe/Effect must reject a transitive non-Ready Primitive.");

        catalog.Primitives[0].ReviewStatus = VfxAuthoringReviewStatuses.Ready;
        catalog.Primitives[0].Approval = CreateApproval('a');
        var evidence = CreateObservedApprovalEvidence(catalog);
        evidence.ApprovalAuthorityVerifier = null;
        Require(HasCode(VfxAuthoringCatalogValidator.Validate(catalog, evidence).Diagnostics, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid), "Self-authored reviewer and authority strings must not unlock Ready without a trusted verifier.");
        evidence.ApprovalAuthorityVerifier = TestAuthorityVerifier.Instance;
        evidence.AssetContentHashes[catalog.Primitives[0].PrimitiveId] = Hash('f');
        VfxAuthoringCatalogValidationReport stale = VfxAuthoringCatalogValidator.Validate(catalog, evidence);
        Require(HasCode(stale.Diagnostics, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid), "Changed approval-bound asset evidence must make Ready stale.");
    }

    private static void GenerationTemplates_NormalizeQualityProfileAndHashDeterministically()
    {
        string root = FindRepoRoot();
        string path = Path.Combine(root, "Assets", "Config", "MxFramework", "Vfx", "vfx_generation_templates.json");
        VfxGenerationTemplateCatalogDocument templates = VfxCreativeFlywheelJson.LoadFile<VfxGenerationTemplateCatalogDocument>(path);
        VfxGenerationTemplateCatalogValidationReport report = VfxGenerationTemplateCatalog.Validate(templates);
        Require(!report.HasErrors, "Repository generation template catalog and versioned quality profile should validate.");
        VfxQualityProfile quality = templates.QualityProfiles[0];
        Require(Math.Abs(quality.FixedStepSeconds - 1d / 60d) < 0.0000001d && quality.FixedSeeds.Count == 3 && quality.GhostCycleCount == 20, "Quality profile must preserve deterministic simulation/capture data.");
        Require(quality.PerformanceTierBudgets.Count == 3 && quality.PerformanceWarmupFrames == 120 && quality.PerformanceSampleFrames == 600, "Quality profile must preserve versioned P95 and sample budgets.");
        string first = VfxGenerationTemplateCatalog.ComputeHash(templates);
        VfxGenerationTemplateCatalogDocument roundtrip = VfxCreativeFlywheelJson.Deserialize<VfxGenerationTemplateCatalogDocument>(VfxCreativeFlywheelJson.Serialize(templates));
        string second = VfxGenerationTemplateCatalog.ComputeHash(roundtrip);
        Require(string.Equals(first, second, StringComparison.Ordinal), "Generation template semantic hash must be deterministic.");
    }

    private static void ReusePriorityAndGenerationPlan_AreStable()
    {
        VfxAuthoringCatalogDocument catalog = CreateReadyCatalog();
        VfxGenerationTemplateCatalogDocument templates = LoadTemplates();
        VfxGenerationProviderDefinition developmentAgent = templates.Providers.Find(item => item.ProviderId == VfxDevelopmentAgentProviderIds.DevelopmentAgent);
        VfxGenerationProviderDefinition proceduralFixture = templates.Providers.Find(item => item.ProviderId == VfxDevelopmentAgentProviderIds.LegacyProcedural);
        Require(developmentAgent != null && developmentAgent.EnabledByDefault && developmentAgent.Kind == VfxGenerationProviderKinds.DevelopmentAgent, "Development Agent must be the enabled production VFX creation provider.");
        Require(proceduralFixture != null && !proceduralFixture.EnabledByDefault, "Legacy procedural provider must remain disabled outside explicit test fixtures.");
        string templateHash = VfxGenerationTemplateCatalog.ComputeHash(templates);
        VfxCreativeBrief directBrief = CreateBrief("catalog-hash", templateHash, "fire");
        VfxReuseDecision direct = VfxReusePlanner.Plan(catalog, directBrief, "catalog-hash");
        Require(!direct.Blocked && direct.DecisionKind == VfxReuseDecisionKinds.ReusePublishedEffect && direct.SelectedEffectId == catalog.Effects[0].EffectId, "Ready Published Effect must win before composition or generation.");

        VfxCreativeBrief composeBrief = CreateBrief("catalog-hash", templateHash, "ice");
        VfxReuseDecision compose = VfxReusePlanner.Plan(catalog, composeBrief, "catalog-hash");
        Require(compose.DecisionKind == VfxReuseDecisionKinds.ComposeReadyRecipe, "Compatible Ready Recipe should be used when no exact themed Effect matches.");
        VfxGenerationPlan first = VfxGenerationPlanner.Plan(composeBrief, compose, catalog, templates, "catalog-hash", templateHash);
        VfxGenerationPlan second = VfxGenerationPlanner.Plan(composeBrief, compose, catalog, templates, "catalog-hash", templateHash);
        Require(!first.Blocked && first.Candidates.Count == 6, "Round zero must emit six isolated controlled-template candidates.");
        Require(string.Equals(first.PlanHash, second.PlanHash, StringComparison.Ordinal), "Generation plan hash must be deterministic.");
        Require(first.SandboxRoot.StartsWith("Assets/VFX/MxCandidates/", StringComparison.Ordinal) && !first.SandboxRoot.Contains(".mx-candidates", StringComparison.Ordinal), "Unity candidates must use the importable physical sandbox.");
        Require(VfxCandidatePathPolicy.IsCandidateSandboxAssetPath(first.SandboxRoot + "/candidate.prefab") && VfxCandidatePathPolicy.IsCandidateSandboxAssetPath("Assets/VFX/.mx-candidates/legacy/candidate.prefab"), "Scanner exclusion policy must recognize physical and legacy logical candidate roots.");
        Require(!VfxCandidatePathPolicy.IsCandidateSandboxAssetPath("Assets/VFX/MxFramework/Published/Fireball.prefab"), "Formal VFX assets must remain visible to scanners.");

        VfxCreativeBrief cloudBrief = Clone(composeBrief);
        cloudBrief.PreferredProviderId = "mx.vfx.provider.cloud";
        VfxReuseDecision cloudReuse = VfxReusePlanner.Plan(catalog, cloudBrief, "catalog-hash");
        VfxGenerationPlan cloudPlan = VfxGenerationPlanner.Plan(cloudBrief, cloudReuse, catalog, templates, "catalog-hash", templateHash);
        Require(cloudPlan.Blocked && cloudPlan.Candidates.Count == 0 && HasCode(cloudPlan.Diagnostics, VfxFlywheelDiagnosticCodes.ProviderUnavailable), "An explicitly requested disabled Cloud Provider must block without silently falling back to procedural generation.");

        VfxCreativeBrief invalidIdBrief = Clone(composeBrief);
        invalidIdBrief.BriefId = "火球 brief/impact";
        VfxReuseDecision invalidId = VfxReusePlanner.Plan(catalog, invalidIdBrief, "catalog-hash");
        Require(invalidId.Blocked && HasCode(invalidId.Diagnostics, VfxFlywheelDiagnosticCodes.SchemaInvalid), "Brief id with CJK, spaces, or slash must produce diagnostics without path normalization exceptions.");

        var capabilities = new VfxGenerationSessionCapabilities { CapabilityId = "local.session.test" };
        capabilities.ProviderGrants.Add(new VfxGenerationProviderCapabilityGrant { GrantId = "grant.local.test", ProviderId = "mx.vfx.provider.local-cli", ProviderVersion = "1.0.0", ModelId = "local.vfx.model", ModelVersion = "2026.07" });
        VfxCreativeBrief localBrief = Clone(composeBrief);
        localBrief.PreferredProviderId = "mx.vfx.provider.local-cli";
        VfxGenerationTemplateCatalogDocument localTemplates = Clone(templates);
        localTemplates.Templates.Find(item => item.TemplateId == "mx.vfx.template.particle.fireball").AllowedProviderIds.Add("mx.vfx.provider.local-cli");
        string localTemplateHash = VfxGenerationTemplateCatalog.ComputeHash(localTemplates);
        localBrief.GenerationTemplateCatalogHash = localTemplateHash;
        VfxReuseDecision localReuse = VfxReusePlanner.Plan(catalog, localBrief, "catalog-hash");
        VfxGenerationPlan localPlan = VfxGenerationPlanner.Plan(localBrief, localReuse, catalog, localTemplates, "catalog-hash", localTemplateHash, capabilities);
        Require(!localPlan.Blocked && localPlan.Candidates.TrueForAll(item => item.ProviderId == "mx.vfx.provider.local-cli" && item.ProviderGrantId == "grant.local.test" && item.ModelId == "local.vfx.model" && item.ModelVersion == "2026.07"), "Repository-external exact provider/model grant must be copied into the immutable plan.");
        VfxCandidateGenerationReceipt localReceipt = CreateReceipt(localPlan);
        Require(VfxReceiptValidator.ValidateCandidateReceipt(localPlan, localReceipt, Context(localPlan, localTemplates)).Valid, "Receipt validation must use immutable candidate model/grant bindings without reloading the external capability file.");
        VfxGenerationPlan unboundLocalPlan = Clone(localPlan);
        unboundLocalPlan.Candidates[0].ProviderGrantId = string.Empty;
        unboundLocalPlan.PlanHash = VfxCreativeFlywheelHash.ComputeGenerationPlanHash(unboundLocalPlan);
        VfxCandidateGenerationReceipt unboundLocalReceipt = Clone(localReceipt);
        unboundLocalReceipt.GenerationPlanHash = unboundLocalPlan.PlanHash;
        unboundLocalReceipt.ReceiptHash = VfxCreativeFlywheelHash.ComputeCandidateReceiptHash(unboundLocalReceipt);
        Require(HasCode(VfxReceiptValidator.ValidateCandidateReceipt(unboundLocalPlan, unboundLocalReceipt, Context(unboundLocalPlan, localTemplates)).Diagnostics, VfxFlywheelDiagnosticCodes.ProvenanceInvalid), "Disabled Provider receipt must remain bound to the external capability grant hash/id.");
        VfxGenerationSessionCapabilities incompleteCapabilities = Clone(capabilities);
        incompleteCapabilities.ProviderGrants[0].ModelVersion = string.Empty;
        VfxGenerationPlan incompleteLocalPlan = VfxGenerationPlanner.Plan(localBrief, localReuse, catalog, localTemplates, "catalog-hash", localTemplateHash, incompleteCapabilities);
        Require(incompleteLocalPlan.Blocked && HasCode(incompleteLocalPlan.Diagnostics, VfxFlywheelDiagnosticCodes.CapabilityInvalid) && HasCode(incompleteLocalPlan.Diagnostics, VfxFlywheelDiagnosticCodes.ProviderUnavailable), "Default-disabled local Provider without an exact external model version must remain unusable.");

        VfxGenerationTemplateCatalogDocument cloudTemplates = Clone(localTemplates);
        cloudTemplates.Templates.Find(item => item.TemplateId == "mx.vfx.template.particle.fireball").AllowedProviderIds.Add("mx.vfx.provider.cloud");
        string cloudTemplateHash = VfxGenerationTemplateCatalog.ComputeHash(cloudTemplates);
        VfxCreativeBrief enabledCloudBrief = Clone(composeBrief);
        enabledCloudBrief.PreferredProviderId = "mx.vfx.provider.cloud";
        enabledCloudBrief.GenerationTemplateCatalogHash = cloudTemplateHash;
        VfxReuseDecision enabledCloudReuse = VfxReusePlanner.Plan(catalog, enabledCloudBrief, "catalog-hash");
        var cloudCapabilities = new VfxGenerationSessionCapabilities { CapabilityId = "cloud.session.test" };
        cloudCapabilities.ProviderGrants.Add(new VfxGenerationProviderCapabilityGrant { GrantId = "grant.cloud.test", ProviderId = "mx.vfx.provider.cloud", ProviderVersion = "1.0.0", ModelId = "cloud.vfx.model", ModelVersion = "2026.07" });
        VfxGenerationPlan cloudDenied = VfxGenerationPlanner.Plan(enabledCloudBrief, enabledCloudReuse, catalog, cloudTemplates, "catalog-hash", cloudTemplateHash, cloudCapabilities);
        Require(cloudDenied.Blocked && HasCode(cloudDenied.Diagnostics, VfxFlywheelDiagnosticCodes.ProviderUnavailable), "Cloud grant must remain disabled without allowCloud=true.");
        cloudCapabilities.AllowCloud = true;
        VfxGenerationPlan cloudEnabled = VfxGenerationPlanner.Plan(enabledCloudBrief, enabledCloudReuse, catalog, cloudTemplates, "catalog-hash", cloudTemplateHash, cloudCapabilities);
        Require(!cloudEnabled.Blocked && cloudEnabled.Candidates.TrueForAll(item => item.ProviderId == "mx.vfx.provider.cloud" && item.ModelVersion == "2026.07"), "Cloud Provider must require both exact external grant and explicit cloud capability.");

        VfxCreativeBrief experimentalBrief = Clone(composeBrief);
        experimentalBrief.PreferredBackend = VfxAuthoringBackends.VfxGraph;
        experimentalBrief.PreferredProviderId = "mx.vfx.provider.graph-experimental-17-5";
        experimentalBrief.AllowExperimentalGraphTopology = true;
        var experimentalCapabilities = new VfxGenerationSessionCapabilities { CapabilityId = "graph.experimental.session" };
        experimentalCapabilities.ProviderGrants.Add(new VfxGenerationProviderCapabilityGrant { GrantId = "grant.graph.experimental", ProviderId = "mx.vfx.provider.graph-experimental-17-5", ProviderVersion = "17.5.0", ModelId = "unity-vfxgraph-17.5-experimental", ModelVersion = "17.5.0" });
        experimentalCapabilities.AllowedExperimentalAdapterIds.Add("unity-vfxgraph-17.5-experimental");
        VfxReuseDecision experimentalReuse = VfxReusePlanner.Plan(catalog, experimentalBrief, "catalog-hash");
        VfxGenerationPlan experimentalPlan = VfxGenerationPlanner.Plan(experimentalBrief, experimentalReuse, catalog, templates, "catalog-hash", templateHash, experimentalCapabilities);
        Require(!experimentalPlan.Blocked && experimentalPlan.GenerationMode == VfxGenerationModes.ExperimentalGraph && experimentalPlan.Candidates.TrueForAll(item => item.DraftOnly), "Version-locked experimental Graph Provider requires exact provider and adapter grants and remains Draft-only.");

        Require(VfxCandidatePathPolicy.IsFormalDestination("Assets/VFX/MxFramework/Published/Test.prefab"), "Formal destinations must be under Assets/VFX.");
        Require(!VfxCandidatePathPolicy.IsFormalDestination("Assets/Other/Test.prefab") && !VfxCandidatePathPolicy.IsFormalDestination("Assets/VFX/MxCandidates/session/Test.prefab") && !VfxCandidatePathPolicy.IsFormalDestination("Assets/VFX/.mx-candidates/session/Test.prefab") && !VfxCandidatePathPolicy.IsFormalDestination("Assets/VFX/Published/../Escape.prefab"), "Formal destination policy must reject non-VFX roots, both candidate roots, and traversal.");
    }

    private static void CandidateReceipt_BlocksStaleTamperedUnlicensedAndEscapedAssets()
    {
        CreateGenerationFixture(out VfxGenerationPlan plan, out VfxGenerationTemplateCatalogDocument templates, out VfxCandidateGenerationReceipt receipt, out VfxQualityProfile _);
        VfxReceiptValidationContext context = Context(plan, templates);
        VfxReceiptValidationReport valid = VfxReceiptValidator.ValidateCandidateReceipt(plan, receipt, context);
        Require(valid.Valid, "Complete candidate receipt should validate without file IO.");

        VfxCandidateGenerationReceipt tampered = Clone(receipt);
        tampered.Assets[0].ContentHash = Hash('d');
        Require(HasCode(VfxReceiptValidator.ValidateCandidateReceipt(plan, tampered, context).Diagnostics, VfxFlywheelDiagnosticCodes.ReceiptTampered), "Content mutation without a new receipt hash must be rejected.");

        VfxReceiptValidationContext staleContext = Context(plan, templates);
        staleContext.ActualCatalogHash = "stale-catalog";
        Require(HasCode(VfxReceiptValidator.ValidateCandidateReceipt(plan, receipt, staleContext).Diagnostics, VfxFlywheelDiagnosticCodes.CatalogStale), "Stale Catalog input must invalidate candidate receipt.");

        VfxCandidateGenerationReceipt unlicensed = Clone(receipt);
        unlicensed.Assets[0].Provenance.LicenseId = "unknown-license";
        unlicensed.ReceiptHash = VfxCreativeFlywheelHash.ComputeCandidateReceiptHash(unlicensed);
        Require(HasCode(VfxReceiptValidator.ValidateCandidateReceipt(plan, unlicensed, context).Diagnostics, VfxFlywheelDiagnosticCodes.ProvenanceInvalid), "Unknown license must block promotion evidence.");

        VfxCandidateGenerationReceipt escaped = Clone(receipt);
        escaped.Assets[0].AssetPath = "Assets/VFX/MxCandidates/../escape.prefab";
        escaped.ReceiptHash = VfxCreativeFlywheelHash.ComputeCandidateReceiptHash(escaped);
        Require(HasCode(VfxReceiptValidator.ValidateCandidateReceipt(plan, escaped, context).Diagnostics, VfxFlywheelDiagnosticCodes.SandboxViolation), "Candidate path traversal must be rejected.");

        VfxCandidateGenerationReceipt wrongTemplate = Clone(receipt);
        wrongTemplate.Assets[0].Provenance.TemplateVersion = "unknown";
        wrongTemplate.ReceiptHash = VfxCreativeFlywheelHash.ComputeCandidateReceiptHash(wrongTemplate);
        Require(HasCode(VfxReceiptValidator.ValidateCandidateReceipt(plan, wrongTemplate, context).Diagnostics, VfxFlywheelDiagnosticCodes.ProvenanceInvalid), "Template id/version mismatch must invalidate provenance.");

        VfxCandidateGenerationReceipt wrongModel = Clone(receipt);
        wrongModel.Assets[0].Provenance.ModelVersion = "unknown";
        wrongModel.ReceiptHash = VfxCreativeFlywheelHash.ComputeCandidateReceiptHash(wrongModel);
        Require(HasCode(VfxReceiptValidator.ValidateCandidateReceipt(plan, wrongModel, context).Diagnostics, VfxFlywheelDiagnosticCodes.ProvenanceInvalid), "Exact model id/version mismatch must invalidate provenance.");

        VfxGenerationPlan unknownProviderPlan = Clone(plan);
        unknownProviderPlan.Candidates[0].ProviderId = "mx.vfx.provider.unknown";
        unknownProviderPlan.PlanHash = VfxCreativeFlywheelHash.ComputeGenerationPlanHash(unknownProviderPlan);
        VfxCandidateGenerationReceipt unknownProviderReceipt = Clone(receipt);
        unknownProviderReceipt.GenerationPlanHash = unknownProviderPlan.PlanHash;
        unknownProviderReceipt.Assets[0].Provenance.ProviderId = "mx.vfx.provider.unknown";
        unknownProviderReceipt.ReceiptHash = VfxCreativeFlywheelHash.ComputeCandidateReceiptHash(unknownProviderReceipt);
        Require(HasCode(VfxReceiptValidator.ValidateCandidateReceipt(unknownProviderPlan, unknownProviderReceipt, context).Diagnostics, VfxFlywheelDiagnosticCodes.ProvenanceInvalid), "Unknown provider must invalidate receipt provenance even when a tampered plan is rehashed.");

        VfxCandidateGenerationReceipt missingGuid = Clone(receipt);
        missingGuid.Assets[0].UnityGuid = string.Empty;
        missingGuid.ReceiptHash = VfxCreativeFlywheelHash.ComputeCandidateReceiptHash(missingGuid);
        Require(HasCode(VfxReceiptValidator.ValidateCandidateReceipt(plan, missingGuid, context).Diagnostics, VfxFlywheelDiagnosticCodes.ReceiptTampered), "Missing Unity GUID evidence must invalidate a candidate receipt even after rehashing.");

        VfxCandidateGenerationReceipt missingDependencyHash = Clone(receipt);
        missingDependencyHash.Assets[0].DependencyHash = string.Empty;
        missingDependencyHash.ReceiptHash = VfxCreativeFlywheelHash.ComputeCandidateReceiptHash(missingDependencyHash);
        Require(HasCode(VfxReceiptValidator.ValidateCandidateReceipt(plan, missingDependencyHash, context).Diagnostics, VfxFlywheelDiagnosticCodes.ReceiptTampered), "Missing Unity dependency evidence must invalidate a candidate receipt even after rehashing.");
    }

    private static void EvaluationAndPromotion_AreDeterministicAndGuarded()
    {
        CreateGenerationFixture(out VfxGenerationPlan generation, out VfxGenerationTemplateCatalogDocument templates, out VfxCandidateGenerationReceipt receipt, out VfxQualityProfile quality);
        VfxCandidateEvaluationInputDocument input = CreateEvaluationInput(generation, receipt, quality);
        VfxEvaluationReport first = VfxVisualEvaluationService.Evaluate(input, generation, receipt, quality);
        VfxEvaluationReport second = VfxVisualEvaluationService.Evaluate(input, generation, receipt, quality);
        Require(!first.Blocked && first.Candidates.Count == 6 && first.Candidates.TrueForAll(item => item.Shortlisted), "Qualified candidates should produce a valid advisory shortlist.");
        Require(string.Equals(first.ReportHash, second.ReportHash, StringComparison.Ordinal), "Evaluation report must be deterministic.");

        VfxCandidateEvaluationInputDocument overBudgetInput = Clone(input);
        overBudgetInput.Candidates[0].CpuP95Milliseconds = 1000f;
        VfxHardGateResult claimedPerformance = overBudgetInput.Candidates[0].HardGates.Find(item => item.GateId == VfxHardGateIds.PerformanceProfilePassed);
        claimedPerformance.Passed = true;
        VfxEvaluationReport overBudget = VfxVisualEvaluationService.Evaluate(overBudgetInput, generation, receipt, quality);
        VfxCandidateEvaluationResult overBudgetResult = overBudget.Candidates.Find(item => item.CandidateId == generation.Candidates[0].CandidateId);
        Require(overBudgetResult != null && !overBudgetResult.Shortlisted && !overBudgetResult.HardGates.Find(item => item.GateId == VfxHardGateIds.PerformanceProfilePassed).Passed, "Evaluator must compute tier performance from measured P95 and reject 1000 ms even when the caller claims the gate passed.");

        var request = new VfxPromotionRequest
        {
            CandidateId = generation.Candidates[0].CandidateId,
            TargetKind = VfxPromotionTargetKinds.Primitive,
            TargetStableId = "vfx.primitive.fireball.impact_flash",
            DestinationRoot = "Assets/VFX/MxFramework/Primitives/Fireball",
            RequestedReviewStatus = VfxAuthoringReviewStatuses.ReviewNeeded,
            SelectedBy = "human-vfx-reviewer",
            SelectionEvidenceHash = Hash('e'),
            SelectionAuthorityId = TestAuthorityVerifier.SelectionAuthorityId,
            SelectionAuthorityVersion = TestAuthorityVerifier.SelectionAuthorityVersion,
            SelectionAuthorityReceiptHash = TestAuthorityVerifier.ValidSelectionReceiptHash
        };
        request.ConsumerRecipeIds.Add("vfx.recipe.fireball.impact");
        request.ConsumerRecipeIds.Add("vfx.recipe.fireball.charge");
        VfxReceiptValidationContext noAuthorityContext = Context(generation, templates);
        noAuthorityContext.PromotionSelectionAuthorityVerifier = null;
        Require(HasCode(VfxPromotionPlanner.Plan(request, generation, receipt, first, noAuthorityContext).Diagnostics, VfxFlywheelDiagnosticCodes.HumanApprovalRequired), "Self-authored selection identity/evidence must not authorize promotion without a trusted verifier.");
        VfxPromotionPlan accepted = VfxPromotionPlanner.Plan(request, generation, receipt, first, Context(generation, templates));
        Require(!accepted.Blocked && accepted.ResultReviewStatus == VfxAuthoringReviewStatuses.ReviewNeeded && accepted.Moves.Count == 1, "Qualified human-selected candidate should produce a ReviewNeeded-only move plan.");
        Require(accepted.SelectedBy == request.SelectedBy && accepted.SelectionEvidenceHash == request.SelectionEvidenceHash, "Promotion plan must bind the human selection identity and evidence hash.");
        Require(accepted.Moves[0].ExpectedUnityGuid == receipt.Assets[0].UnityGuid && accepted.Moves[0].ExpectedDependencyHash == receipt.Assets[0].DependencyHash, "Promotion plan must bind the generation-time Unity GUID and dependency hash.");
        Require(accepted.ConsumerRecipeIds.Count == 2 && accepted.ConsumerRecipeIds[0] == "vfx.recipe.fireball.charge" && accepted.ConsumerRecipeIds[1] == "vfx.recipe.fireball.impact", "Promotion plan must bind sorted distinct Primitive consumers.");

        VfxPromotionPlan tamperedSelection = Clone(accepted);
        tamperedSelection.SelectedBy = "different-reviewer";
        Require(!string.Equals(tamperedSelection.PlanHash, VfxCreativeFlywheelHash.ComputePromotionPlanHash(tamperedSelection), StringComparison.Ordinal), "Changing selectedBy must invalidate the promotion plan hash.");
        tamperedSelection = Clone(accepted);
        tamperedSelection.SelectionEvidenceHash = Hash('f');
        Require(!string.Equals(tamperedSelection.PlanHash, VfxCreativeFlywheelHash.ComputePromotionPlanHash(tamperedSelection), StringComparison.Ordinal), "Changing selection evidence must invalidate the promotion plan hash.");
        VfxPromotionRequest forgedAuthority = Clone(request);
        forgedAuthority.SelectionAuthorityReceiptHash = Hash('a');
        Require(HasCode(VfxPromotionPlanner.Plan(forgedAuthority, generation, receipt, first, Context(generation, templates)).Diagnostics, VfxFlywheelDiagnosticCodes.HumanApprovalRequired), "A syntactically valid but untrusted authority receipt hash must not authorize promotion.");
        tamperedSelection = Clone(accepted);
        tamperedSelection.ConsumerRecipeIds.RemoveAt(0);
        Require(!string.Equals(tamperedSelection.PlanHash, VfxCreativeFlywheelHash.ComputePromotionPlanHash(tamperedSelection), StringComparison.Ordinal), "Changing bound Primitive consumers must invalidate the promotion plan hash.");
        tamperedSelection = Clone(accepted);
        tamperedSelection.Moves[0].ExpectedUnityGuid = new string('f', 32);
        Require(!string.Equals(tamperedSelection.PlanHash, VfxCreativeFlywheelHash.ComputePromotionPlanHash(tamperedSelection), StringComparison.Ordinal), "Changing the bound Unity GUID must invalidate the promotion plan hash.");
        tamperedSelection = Clone(accepted);
        tamperedSelection.Moves[0].ExpectedDependencyHash = new string('e', 32);
        Require(!string.Equals(tamperedSelection.PlanHash, VfxCreativeFlywheelHash.ComputePromotionPlanHash(tamperedSelection), StringComparison.Ordinal), "Changing the bound dependency hash must invalidate the promotion plan hash.");

        VfxPromotionRequest exceptionRequest = Clone(request);
        exceptionRequest.ConsumerRecipeIds.RemoveAt(1);
        exceptionRequest.StrategicFoundationExceptionApproved = true;
        VfxPromotionPlan exceptionPlan = VfxPromotionPlanner.Plan(exceptionRequest, generation, receipt, first, Context(generation, templates));
        Require(!exceptionPlan.Blocked && exceptionPlan.StrategicFoundationExceptionApproved && exceptionPlan.ConsumerRecipeIds.Count == 1, "Strategic Primitive exception must be preserved in the immutable plan.");
        VfxPromotionPlan tamperedException = Clone(exceptionPlan);
        tamperedException.StrategicFoundationExceptionApproved = false;
        Require(!string.Equals(tamperedException.PlanHash, VfxCreativeFlywheelHash.ComputePromotionPlanHash(tamperedException), StringComparison.Ordinal), "Changing the strategic exception must invalidate the plan hash.");
        exceptionRequest.SelectionEvidenceHash = string.Empty;
        Require(HasCode(VfxPromotionPlanner.Plan(exceptionRequest, generation, receipt, first, Context(generation, templates)).Diagnostics, VfxFlywheelDiagnosticCodes.HumanApprovalRequired), "Strategic exception must require immutable selection evidence.");

        var existing = new HashSet<string>(StringComparer.Ordinal) { accepted.Moves[0].DestinationAssetPath };
        VfxPromotionPlan conflict = VfxPromotionPlanner.Plan(request, generation, receipt, first, Context(generation, templates), existing);
        Require(HasCode(conflict.Diagnostics, VfxFlywheelDiagnosticCodes.PromotionConflict), "Existing destination must block promotion.");

        request.RequestedReviewStatus = VfxAuthoringReviewStatuses.Ready;
        VfxPromotionPlan ready = VfxPromotionPlanner.Plan(request, generation, receipt, first, Context(generation, templates));
        Require(HasCode(ready.Diagnostics, VfxFlywheelDiagnosticCodes.ReadyForbidden), "Development Agent must never promote directly to Ready.");
    }

    private static void CliFlywheelCommands_DispatchEndToEndPlansAndReports()
    {
        string root = FindRepoRoot();
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(root);
        VfxGenerationTemplateCatalogDocument templates = LoadTemplates();
        string temp = Path.Combine(root, "Temp", "MxFrameworkAuthoringTests", "vfx-flywheel-cli");
        Directory.CreateDirectory(temp);
        string briefPath = Path.Combine(temp, "brief.json");
        string bundlePath = Path.Combine(temp, "plan_bundle.json");
        File.WriteAllText(briefPath, VfxCreativeFlywheelJson.Serialize(CreateBrief(scan.CatalogHash, VfxGenerationTemplateCatalog.ComputeHash(templates), "fire")));

        string invalidCatalogPath = Path.Combine(temp, "invalid_catalog.json");
        string invalidReportPath = Path.Combine(temp, "invalid_scan_report.json");
        File.WriteAllText(invalidCatalogPath, "{\"schemaVersion\":\"mx.vfx.authoring.v2\",\"catalogId\":\"INVALID CATALOG\",\"taxonomyVersion\":\"mx.vfx.taxonomy.v1\",\"strictCoverage\":false,\"primitives\":[],\"recipes\":[],\"effects\":[]}");
        string invalidCatalogRelative = Path.GetRelativePath(root, invalidCatalogPath).Replace('\\', '/');
        int invalidScanExit = DispatchQuiet(new[] { "vfx", "plan-create", "--project-root", root, "--catalog", invalidCatalogRelative, "--brief", briefPath, "--output", invalidReportPath });
        VfxAuthoringProjectReport invalidScanReport = VfxCreativeFlywheelJson.LoadFile<VfxAuthoringProjectReport>(invalidReportPath);
        Require(invalidScanExit == AuthoringExitCodes.ValidationBlocked && invalidScanReport.ErrorCount > 0 && invalidScanReport.Diagnostics.Count > 0, "plan-create must block scanner validation errors and emit a stable report instead of planning candidates.");

        string repositoryCapabilityPath = Path.Combine(temp, "repository_capability.json");
        string repositoryCapabilityOutput = Path.Combine(temp, "repository_capability_plan.json");
        File.WriteAllText(repositoryCapabilityPath, VfxCreativeFlywheelJson.Serialize(new VfxGenerationSessionCapabilities { CapabilityId = "repo.session.invalid" }));
        int repositoryCapabilityExit = DispatchQuiet(new[] { "vfx", "plan-create", "--project-root", root, "--brief", briefPath, "--capabilities", repositoryCapabilityPath, "--output", repositoryCapabilityOutput });
        VfxCreativePlanBundle repositoryCapabilityBundle = VfxCreativeFlywheelJson.LoadFile<VfxCreativePlanBundle>(repositoryCapabilityOutput);
        Require(repositoryCapabilityExit == AuthoringExitCodes.ValidationBlocked && HasCode(repositoryCapabilityBundle.GenerationPlan.Diagnostics, VfxFlywheelDiagnosticCodes.CapabilityInvalid), "Repository-local capability files must not enable session providers.");

        int createExit = DispatchQuiet(new[] { "vfx", "plan-create", "--project-root", root, "--brief", briefPath, "--output", bundlePath });
        Require(createExit == AuthoringExitCodes.Ready, "Registered vfx plan-create command should produce an unblocked plan.");
        VfxCreativePlanBundle bundle = VfxCreativeFlywheelJson.LoadFile<VfxCreativePlanBundle>(bundlePath);
        Require(bundle.GenerationPlan.Candidates.Count == 6 && VfxCreativeFlywheelHash.IsSha256(bundle.GenerationPlan.PlanHash), "CLI plan-create should emit the real six-candidate plan bundle.");

        VfxGenerationPlan generation = bundle.GenerationPlan;
        VfxCandidateGenerationReceipt candidateReceipt = CreateReceipt(generation);
        string generationPath = Path.Combine(temp, "generation_plan.json");
        string candidateReceiptPath = Path.Combine(temp, "candidate_receipt.json");
        string receiptValidationPath = Path.Combine(temp, "receipt_validation.json");
        File.WriteAllText(generationPath, VfxCreativeFlywheelJson.Serialize(generation));
        File.WriteAllText(candidateReceiptPath, VfxCreativeFlywheelJson.Serialize(candidateReceipt));
        int validateExit = DispatchQuiet(new[] { "vfx", "validate-receipt", "--project-root", root, "--plan", generationPath, "--receipt", candidateReceiptPath, "--skip-file-check", "--output", receiptValidationPath });
        Require(validateExit == AuthoringExitCodes.Ready && VfxCreativeFlywheelJson.LoadFile<VfxReceiptValidationReport>(receiptValidationPath).Valid, "Registered vfx validate-receipt command should deserialize and validate candidate evidence.");

        VfxQualityProfile quality = templates.QualityProfiles.Find(item => item.QualityProfileId == generation.QualityProfileId && item.Version == generation.QualityProfileVersion);
        Require(quality != null, "CLI fixture quality profile should exist.");
        VfxCandidateEvaluationInputDocument evaluationInput = CreateEvaluationInput(generation, candidateReceipt, quality);
        string evaluationInputPath = Path.Combine(temp, "evaluation_input.json");
        string evaluationPath = Path.Combine(temp, "evaluation_report.json");
        File.WriteAllText(evaluationInputPath, VfxCreativeFlywheelJson.Serialize(evaluationInput));
        int evaluateExit = DispatchQuiet(new[] { "vfx", "evaluate-candidates", "--project-root", root, "--plan", generationPath, "--receipt", candidateReceiptPath, "--input", evaluationInputPath, "--skip-file-check", "--output", evaluationPath });
        VfxEvaluationReport evaluation = VfxCreativeFlywheelJson.LoadFile<VfxEvaluationReport>(evaluationPath);
        Require(evaluateExit == AuthoringExitCodes.Ready && !evaluation.Blocked && evaluation.Candidates.Count == 6, "Registered vfx evaluate-candidates command should produce the deterministic advisory report.");

        var promotionRequest = new VfxPromotionRequest
        {
            CandidateId = generation.Candidates[0].CandidateId,
            TargetKind = VfxPromotionTargetKinds.Effect,
            TargetStableId = "vfx.test.cli.flywheel.published",
            DestinationRoot = "Assets/VFX/MxFramework/Published/AuthoringCliFixture",
            RequestedReviewStatus = VfxAuthoringReviewStatuses.ReviewNeeded,
            SelectedBy = "human-cli-test-reviewer",
            SelectionEvidenceHash = Hash('e'),
            SelectionAuthorityId = TestAuthorityVerifier.SelectionAuthorityId,
            SelectionAuthorityVersion = TestAuthorityVerifier.SelectionAuthorityVersion,
            SelectionAuthorityReceiptHash = TestAuthorityVerifier.ValidSelectionReceiptHash
        };
        string promotionRequestPath = Path.Combine(temp, "promotion_request.json");
        string promotionPlanPath = Path.Combine(temp, "promotion_plan.json");
        File.WriteAllText(promotionRequestPath, VfxCreativeFlywheelJson.Serialize(promotionRequest));
        int promoteExit = DispatchQuiet(new[] { "vfx", "plan-promote", "--project-root", root, "--plan", generationPath, "--receipt", candidateReceiptPath, "--evaluation", evaluationPath, "--request", promotionRequestPath, "--skip-file-check", "--output", promotionPlanPath });
        VfxPromotionPlan promotionPlan = VfxCreativeFlywheelJson.LoadFile<VfxPromotionPlan>(promotionPlanPath);
        Require(promoteExit == AuthoringExitCodes.ValidationBlocked && promotionPlan.Blocked && HasCode(promotionPlan.Diagnostics, VfxFlywheelDiagnosticCodes.HumanApprovalRequired), "CLI plan-promote must fail closed because it has no trusted selection-authority integration.");
        promotionPlan = VfxPromotionPlanner.Plan(promotionRequest, generation, candidateReceipt, evaluation, Context(generation, templates));
        Require(!promotionPlan.Blocked && promotionPlan.Moves.Count > 0, "An explicitly injected trusted test verifier should produce the guarded plan used by later registration-plan tests.");
        File.WriteAllText(promotionPlanPath, VfxCreativeFlywheelJson.Serialize(promotionPlan));

        var promotionReceipt = new VfxPromotionReceipt
        {
            PromotionPlanHash = promotionPlan.PlanHash,
            CatalogHash = promotionPlan.ExpectedCatalogHash,
            ExecutorId = "mx.vfx.unity-promotion-executor",
            ExecutorVersion = "1",
            ResultReviewStatus = VfxAuthoringReviewStatuses.ReviewNeeded
        };
        for (int i = 0; i < promotionPlan.Moves.Count; i++)
        {
            VfxPromotionMove move = promotionPlan.Moves[i];
            promotionReceipt.Assets.Add(new VfxPromotedAssetRecord
            {
                SourceAssetPath = move.SourceAssetPath,
                DestinationAssetPath = move.DestinationAssetPath,
                ContentHash = move.ExpectedContentHash,
                UnityGuid = move.ExpectedUnityGuid,
                DependencyHash = move.ExpectedDependencyHash
            });
        }
        promotionReceipt.ReceiptHash = VfxCreativeFlywheelHash.ComputePromotionReceiptHash(promotionReceipt);
        Require(VfxReceiptValidator.ValidatePromotionReceipt(promotionPlan, promotionReceipt, Context(generation, templates)).Valid, "Promotion receipt must preserve the plan-bound content, Unity GUID, and dependency hashes.");
        VfxPromotionReceipt tamperedPromotionReceipt = Clone(promotionReceipt);
        tamperedPromotionReceipt.Assets[0].DependencyHash = new string('f', 32);
        tamperedPromotionReceipt.ReceiptHash = VfxCreativeFlywheelHash.ComputePromotionReceiptHash(tamperedPromotionReceipt);
        Require(HasCode(VfxReceiptValidator.ValidatePromotionReceipt(promotionPlan, tamperedPromotionReceipt, Context(generation, templates)).Diagnostics, VfxFlywheelDiagnosticCodes.ReceiptTampered), "Rehashed promotion receipt dependency substitution must still fail the immutable move binding.");
        string promotionReceiptPath = Path.Combine(temp, "promotion_receipt.json");
        File.WriteAllText(promotionReceiptPath, VfxCreativeFlywheelJson.Serialize(promotionReceipt));

        var scanOptions = new VfxAuthoringProjectScanOptions();
        var registrationRequest = new VfxPublishedRegistrationRequest
        {
            ExpectedCatalogHash = scan.CatalogHash,
            ExpectedResourceCatalogHash = VfxAuthoringProjectScanner.ComputeFileHash(Path.Combine(root, scanOptions.ResourceCatalogPath.Replace('/', Path.DirectorySeparatorChar))),
            ExpectedDefinitionCatalogHash = VfxAuthoringProjectScanner.ComputeFileHash(Path.Combine(root, scanOptions.DefinitionCatalogPath.Replace('/', Path.DirectorySeparatorChar))),
            EffectId = "vfx.test.cli.flywheel.published",
            ResourceKey = new VfxAuthoringResourceKey { Id = "vfx.test.cli.flywheel.published", TypeId = "GameObject", PackageId = "mxframework.tests" },
            DefinitionId = 2147483000,
            PublishedPrefabPath = promotionPlan.Moves[0].DestinationAssetPath,
            PromotionReceiptHash = promotionReceipt.ReceiptHash
        };
        string registrationRequestPath = Path.Combine(temp, "registration_request.json");
        string registrationPlanPath = Path.Combine(temp, "registration_plan.json");
        File.WriteAllText(registrationRequestPath, VfxCreativeFlywheelJson.Serialize(registrationRequest));
        int registerExit = DispatchQuiet(new[] { "vfx", "plan-register-published", "--project-root", root, "--request", registrationRequestPath, "--promotion-plan", promotionPlanPath, "--promotion-receipt", promotionReceiptPath, "--skip-file-check", "--output", registrationPlanPath });
        VfxPublishedRegistrationPlan registrationPlan = VfxCreativeFlywheelJson.LoadFile<VfxPublishedRegistrationPlan>(registrationPlanPath);
        Require(registerExit == AuthoringExitCodes.Ready && !registrationPlan.Blocked && registrationPlan.WouldModifySemanticCatalog && registrationPlan.WouldModifyResourceCatalog && !registrationPlan.WouldModifyRuntimeAuthority, "Registered vfx plan-register-published command should produce separate guarded Catalog plans without Runtime authority changes.");
    }

    private static void Scanner_ObservesOnlyPromotionBoundQualifiedEvaluation()
    {
        CreateGenerationFixture(out VfxGenerationPlan generation, out VfxGenerationTemplateCatalogDocument templates, out VfxCandidateGenerationReceipt receipt, out VfxQualityProfile quality);
        VfxEvaluationReport evaluation = VfxVisualEvaluationService.Evaluate(CreateEvaluationInput(generation, receipt, quality), generation, receipt, quality);
        const string stableId = "vfx.primitive.scan.evidence";
        var request = new VfxPromotionRequest
        {
            CandidateId = generation.Candidates[0].CandidateId,
            TargetKind = VfxPromotionTargetKinds.Primitive,
            TargetStableId = stableId,
            DestinationRoot = "Assets/VFX/MxFramework/Primitives/ScanEvidence",
            RequestedReviewStatus = VfxAuthoringReviewStatuses.ReviewNeeded,
            SelectedBy = "human-scan-reviewer",
            SelectionEvidenceHash = Hash('e'),
            SelectionAuthorityId = TestAuthorityVerifier.SelectionAuthorityId,
            SelectionAuthorityVersion = TestAuthorityVerifier.SelectionAuthorityVersion,
            SelectionAuthorityReceiptHash = TestAuthorityVerifier.ValidSelectionReceiptHash
        };
        request.ConsumerRecipeIds.Add("vfx.recipe.scan.one");
        request.ConsumerRecipeIds.Add("vfx.recipe.scan.two");
        VfxPromotionPlan promotionPlan = VfxPromotionPlanner.Plan(request, generation, receipt, evaluation, Context(generation, templates));
        Require(!promotionPlan.Blocked, "Promotion-bound scanner fixture plan must be valid.");

        string scanRoot = Path.Combine(FindRepoRoot(), "Temp", "MxFrameworkAuthoringTests", "vfx-ready-evidence-scan");
        if (Directory.Exists(scanRoot)) Directory.Delete(scanRoot, true);
        Directory.CreateDirectory(Path.Combine(scanRoot, "Config"));
        Directory.CreateDirectory(Path.Combine(scanRoot, "Evidence"));
        Directory.CreateDirectory(Path.Combine(scanRoot, "Reports"));
        File.WriteAllText(Path.Combine(scanRoot, "Config", "catalog.json"), VfxAuthoringCatalogJson.Serialize(new VfxAuthoringCatalogDocument { CatalogId = "test.vfx.scan-evidence" }));
        File.WriteAllText(Path.Combine(scanRoot, "Evidence", "binding.json"), VfxCreativeFlywheelJson.Serialize(new Dictionary<string, string>
        {
            ["stableId"] = stableId,
            ["evaluationReportPath"] = "Reports/evaluation.json",
            ["evaluationCandidateId"] = request.CandidateId,
            ["promotionPlanPath"] = "Reports/promotion-plan.json",
            ["promotionReceiptPath"] = "Reports/promotion-receipt.json"
        }));

        WritePromotionEvidenceChain(scanRoot, evaluation, promotionPlan);
        var options = new VfxAuthoringProjectScanOptions
        {
            CatalogPath = "Config/catalog.json",
            ResourceCatalogPath = "Config/missing-resources.json",
            DefinitionCatalogPath = "Config/missing-definitions.json",
            VfxAssetRoot = "Assets/VFX",
            PackageManifestPath = "Config/missing-packages.json",
            BakeEvidenceRoot = "Evidence",
            ThumbnailManifestPath = "Temp/missing-thumbnails.json"
        };
        VfxAuthoringProjectScanResult qualified = VfxAuthoringProjectScanner.Scan(scanRoot, options);
        Require(qualified.Evidence.EvaluationReportHashes.TryGetValue(stableId, out string observed) && string.Equals(observed, evaluation.ReportHash, StringComparison.OrdinalIgnoreCase), "Scanner must observe an exact promotion-bound hard-gate-passed shortlist report.");

        VfxEvaluationReport failed = Clone(evaluation);
        VfxCandidateEvaluationResult selected = failed.Candidates.Find(item => item.CandidateId == request.CandidateId);
        selected.HardGatesPassed = false;
        selected.Shortlisted = false;
        failed.ReportHash = VfxCreativeFlywheelHash.ComputeEvaluationReportHash(failed);
        VfxPromotionPlan failedPlan = Clone(promotionPlan);
        failedPlan.EvaluationReportHash = failed.ReportHash;
        failedPlan.PlanHash = VfxCreativeFlywheelHash.ComputePromotionPlanHash(failedPlan);
        WritePromotionEvidenceChain(scanRoot, failed, failedPlan);
        VfxAuthoringProjectScanResult rejected = VfxAuthoringProjectScanner.Scan(scanRoot, options);
        Require(!rejected.Evidence.EvaluationReportHashes.ContainsKey(stableId), "Scanner must reject all-failed or non-shortlisted evaluation reports even when their plan/receipt hashes are self-consistent.");
    }

    private static void WritePromotionEvidenceChain(string scanRoot, VfxEvaluationReport evaluation, VfxPromotionPlan plan)
    {
        File.WriteAllText(Path.Combine(scanRoot, "Reports", "evaluation.json"), VfxCreativeFlywheelJson.Serialize(evaluation));
        File.WriteAllText(Path.Combine(scanRoot, "Reports", "promotion-plan.json"), VfxCreativeFlywheelJson.Serialize(plan));
        var receipt = new VfxPromotionReceipt
        {
            PromotionPlanHash = plan.PlanHash,
            CatalogHash = plan.ExpectedCatalogHash,
            ExecutorId = "test.unity-promotion-executor",
            ExecutorVersion = "1",
            ResultReviewStatus = VfxAuthoringReviewStatuses.ReviewNeeded
        };
        for (int i = 0; i < plan.Moves.Count; i++)
        {
            VfxPromotionMove move = plan.Moves[i];
            receipt.Assets.Add(new VfxPromotedAssetRecord
            {
                SourceAssetPath = move.SourceAssetPath,
                DestinationAssetPath = move.DestinationAssetPath,
                ContentHash = move.ExpectedContentHash,
                UnityGuid = move.ExpectedUnityGuid,
                DependencyHash = move.ExpectedDependencyHash
            });
        }
        receipt.ReceiptHash = VfxCreativeFlywheelHash.ComputePromotionReceiptHash(receipt);
        File.WriteAllText(Path.Combine(scanRoot, "Reports", "promotion-receipt.json"), VfxCreativeFlywheelJson.Serialize(receipt));
    }

    private static void CreateGenerationFixture(out VfxGenerationPlan plan, out VfxGenerationTemplateCatalogDocument templates, out VfxCandidateGenerationReceipt receipt, out VfxQualityProfile quality)
    {
        VfxAuthoringCatalogDocument catalog = CreateReadyCatalog();
        templates = LoadTemplates();
        string templateHash = VfxGenerationTemplateCatalog.ComputeHash(templates);
        VfxCreativeBrief brief = CreateBrief("catalog-hash", templateHash, "ice");
        VfxReuseDecision reuse = VfxReusePlanner.Plan(catalog, brief, "catalog-hash");
        plan = VfxGenerationPlanner.Plan(brief, reuse, catalog, templates, "catalog-hash", templateHash);
        quality = templates.QualityProfiles.Find(item => item.QualityProfileId == brief.QualityProfileId && item.Version == brief.QualityProfileVersion);
        receipt = CreateReceipt(plan);
    }

    private static VfxCandidateGenerationReceipt CreateReceipt(VfxGenerationPlan plan)
    {
        var receipt = new VfxCandidateGenerationReceipt
        {
            SessionId = plan.SessionId,
            GenerationPlanHash = plan.PlanHash,
            CatalogHash = plan.CatalogHash,
            GenerationTemplateCatalogHash = plan.GenerationTemplateCatalogHash,
            ExecutorId = "mx.vfx.unity-candidate-executor",
            ExecutorVersion = "1"
        };
        for (int i = 0; i < plan.Candidates.Count; i++)
        {
            VfxGenerationCandidateSpec candidate = plan.Candidates[i];
            receipt.Assets.Add(new VfxCandidateAssetRecord
            {
                CandidateId = candidate.CandidateId,
                AssetKind = VfxCandidateAssetKinds.Prefab,
                AssetPath = candidate.OutputRoot + "/candidate.prefab",
                ContentHash = Hash((char)('a' + i)),
                UnityGuid = new string((char)('a' + i), 32),
                DependencyHash = new string((char)('0' + i), 32),
                Provenance = new VfxAssetProvenance
                {
                    ProviderId = candidate.ProviderId,
                    ProviderVersion = candidate.ProviderVersion,
                    TemplateId = candidate.TemplateId,
                    TemplateVersion = candidate.TemplateVersion,
                    ModelId = candidate.ModelId,
                    ModelVersion = candidate.ModelVersion,
                    LicenseId = "project-owned",
                    SourceUri = "agent-task://fixture/" + candidate.CandidateId,
                    SourceContentHash = Hash('e')
                }
            });
        }
        receipt.ReceiptHash = VfxCreativeFlywheelHash.ComputeCandidateReceiptHash(receipt);
        return receipt;
    }

    private static VfxCandidateEvaluationInputDocument CreateEvaluationInput(VfxGenerationPlan plan, VfxCandidateGenerationReceipt receipt, VfxQualityProfile quality)
    {
        var input = new VfxCandidateEvaluationInputDocument
        {
            GenerationPlanHash = plan.PlanHash,
            CandidateReceiptHash = receipt.ReceiptHash,
            QualityProfileId = quality.QualityProfileId,
            QualityProfileVersion = quality.Version
        };
        for (int i = 0; i < plan.Candidates.Count; i++)
        {
            var candidate = new VfxCandidateEvaluationInput { CandidateId = plan.Candidates[i].CandidateId, CpuP95Milliseconds = 0.2f + i * 0.01f, GpuP95Milliseconds = 0.4f + i * 0.01f };
            for (int gate = 0; gate < quality.RequiredHardGateIds.Count; gate++)
                candidate.HardGates.Add(new VfxHardGateResult { GateId = quality.RequiredHardGateIds[gate], Passed = true, EvidenceHash = Hash('f') });
            for (int reviewer = 0; reviewer < 3; reviewer++)
                candidate.ReviewerScores.Add(new VfxReviewerScore { ReviewerId = "reviewer-" + reviewer, BriefMatch = 88 + reviewer, Readability = 86 + reviewer, Timing = 84 + reviewer, SpatialMotion = 83 + reviewer, MaterialAndColor = 82 + reviewer, Polish = 81 + reviewer });
            input.Candidates.Add(candidate);
        }
        return input;
    }

    private static VfxReceiptValidationContext Context(VfxGenerationPlan plan, VfxGenerationTemplateCatalogDocument templates) => new VfxReceiptValidationContext
    {
        ActualCatalogHash = plan.CatalogHash,
        ActualGenerationTemplateCatalogHash = plan.GenerationTemplateCatalogHash,
        VerifyAssetFiles = false,
        GenerationTemplates = templates,
        PromotionSelectionAuthorityVerifier = TestAuthorityVerifier.Instance
    };

    private static VfxCreativeBrief CreateBrief(string catalogHash, string templateHash, string theme)
    {
        var brief = new VfxCreativeBrief
        {
            BriefId = "fireball.impact.test",
            CatalogHash = catalogHash,
            GenerationTemplateCatalogHash = templateHash,
            StyleProfileId = "mx.vfx.style.fire-readable",
            QualityProfileId = "windows.d3d12.1080p60",
            QualityProfileVersion = "1",
            PreferredBackend = VfxAuthoringBackends.ParticleSystem,
            PreferredProviderId = VfxDevelopmentAgentProviderIds.DevelopmentAgent,
            TargetCostTier = VfxAuthoringCostTiers.Medium,
            AnchorContract = new VfxAuthoringAnchorContract { Anchor = VfxAuthoringAnchors.HitPoint, Space = VfxAuthoringSpaces.World },
            Lifecycle = new VfxAuthoringLifecycle { Mode = VfxAuthoringLifecycleModes.OneShot, DefaultLifetimeFrames = 30, StopPolicy = VfxAuthoringStopPolicies.Expire },
            Description = "Fireball Impact vertical-slice brief."
        };
        brief.Roles.Add(VfxAuthoringRoles.Impact);
        brief.ThemeIds.Add(theme);
        return brief;
    }

    private static VfxAuthoringCatalogDocument CreateReadyCatalog()
    {
        var primitive = new VfxAuthoringPrimitive
        {
            PrimitiveId = "vfx.primitive.fireball.flash",
            DisplayName = "Fireball flash",
            Description = "Single-responsibility fireball flash.",
            AssetRef = new VfxAuthoringAssetReference { Path = "Assets/VFX/MxFramework/Primitives/Fireball/Flash.prefab" },
            Backend = VfxAuthoringBackends.ParticleSystem,
            VisualMaturity = VfxAuthoringVisualMaturities.Production,
            ReviewStatus = VfxAuthoringReviewStatuses.Ready,
            Approval = CreateApproval('a')
        };
        primitive.Roles.Add(VfxAuthoringRoles.Impact);
        primitive.CompatibleAnchors.Add(VfxAuthoringAnchors.HitPoint);
        var recipe = new VfxAuthoringRecipe
        {
            RecipeId = "vfx.recipe.fireball.impact",
            DisplayName = "Fireball impact",
            Description = "Canonical fireball impact composition.",
            VisualMaturity = VfxAuthoringVisualMaturities.Production,
            ReviewStatus = VfxAuthoringReviewStatuses.Ready,
            Approval = CreateApproval('b')
        };
        recipe.Layers.Add(new VfxAuthoringRecipeLayer { LayerId = "flash", PrimitiveId = primitive.PrimitiveId, Order = 10 });
        var effect = new VfxAuthoringEffect
        {
            EffectId = "vfx.magic.fireball.impact.ready",
            DisplayName = "Ready fireball impact",
            Description = "Approved Published Effect exemplar for reuse tests.",
            ResourceKey = new VfxAuthoringResourceKey { Id = "vfx.magic.fireball.impact.ready", TypeId = "GameObject", PackageId = "mxframework.samples" },
            RecipeId = recipe.RecipeId,
            Backend = VfxAuthoringBackends.ParticleSystem,
            AnchorContract = new VfxAuthoringAnchorContract { Anchor = VfxAuthoringAnchors.HitPoint, Space = VfxAuthoringSpaces.World },
            Lifecycle = new VfxAuthoringLifecycle { Mode = VfxAuthoringLifecycleModes.OneShot, DefaultLifetimeFrames = 30, StopPolicy = VfxAuthoringStopPolicies.Expire },
            Quality = new VfxAuthoringQuality { CostTier = VfxAuthoringCostTiers.Medium, FallbackPolicy = VfxAuthoringFallbackPolicies.None },
            Budget = new VfxAuthoringBudget { Priority = VfxAuthoringPriorities.Cosmetic, RecommendedMaxConcurrent = 8, CullDistanceMeters = 40f },
            Preview = new VfxAuthoringPreview { AnchorPreset = VfxAuthoringAnchors.HitPoint, DurationSeconds = 1f, CameraScaleMeters = 3f },
            Published = new VfxAuthoringPublishedEffectEvidence { AssetPath = "Assets/VFX/MxFramework/Published/FireballImpact.prefab", BakerId = VfxAuthoringBakerIds.ParticleSystemPrefab, BakerVersion = VfxAuthoringBakerIds.ParticleSystemPrefabVersion },
            VisualMaturity = VfxAuthoringVisualMaturities.Production,
            ReviewStatus = VfxAuthoringReviewStatuses.Ready,
            Approval = CreateApproval('c'),
            Exemplar = true
        };
        effect.Roles.Add(VfxAuthoringRoles.Impact);
        effect.ThemeIds.Add("fire");
        var catalog = new VfxAuthoringCatalogDocument { CatalogId = "test.vfx.flywheel" };
        catalog.Primitives.Add(primitive);
        catalog.Recipes.Add(recipe);
        catalog.Effects.Add(effect);
        effect.Published.RecipeInputHash = VfxAuthoringBakeHasher.ComputeRecipeInputHash(catalog, effect, effect.Published.BakerId, effect.Published.BakerVersion);
        effect.Approval.RecipeInputHash = effect.Published.RecipeInputHash;
        return catalog;
    }

    private static VfxAuthoringApprovalEvidence CreateApproval(char digit)
    {
        var value = new VfxAuthoringApprovalEvidence
        {
            ApprovalId = "approval." + digit,
            ApprovedBy = "vfx-reviewer",
            ApprovalRole = VfxAuthoringApprovalRoles.VfxArtist,
            AssetContentHash = Hash(digit),
            DependencyHash = Hash(digit),
            EvaluationReportHash = Hash(digit),
            QualityProfileVersion = "1",
            ApprovalAuthorityId = TestAuthorityVerifier.ReadyAuthorityId,
            ApprovalAuthorityVersion = TestAuthorityVerifier.ReadyAuthorityVersion,
            AuthorityReceiptHash = TestAuthorityVerifier.ValidReadyReceiptHash
        };
        value.HumanApprovals.Add(new VfxAuthoringHumanApproval { ReviewerId = "vfx-reviewer", Role = VfxAuthoringApprovalRoles.VfxArtist });
        return value;
    }

    private static VfxAuthoringCatalogEvidence CreateObservedApprovalEvidence(VfxAuthoringCatalogDocument catalog)
    {
        var evidence = new VfxAuthoringCatalogEvidence { ApprovalAuthorityVerifier = TestAuthorityVerifier.Instance };
        for (int i = 0; i < catalog.Primitives.Count; i++) Observe(catalog.Primitives[i].PrimitiveId, catalog.Primitives[i].Approval, evidence);
        for (int i = 0; i < catalog.Recipes.Count; i++) Observe(catalog.Recipes[i].RecipeId, catalog.Recipes[i].Approval, evidence);
        for (int i = 0; i < catalog.Effects.Count; i++) Observe(catalog.Effects[i].EffectId, catalog.Effects[i].Approval, evidence);
        return evidence;
    }

    private static void Observe(string stableId, VfxAuthoringApprovalEvidence approval, VfxAuthoringCatalogEvidence evidence)
    {
        evidence.AssetContentHashes[stableId] = approval.AssetContentHash;
        evidence.DependencyHashes[stableId] = approval.DependencyHash;
        evidence.EvaluationReportHashes[stableId] = approval.EvaluationReportHash;
        evidence.QualityProfileVersions[stableId] = approval.QualityProfileVersion;
    }

    private static VfxGenerationTemplateCatalogDocument LoadTemplates()
    {
        string path = Path.Combine(FindRepoRoot(), "Assets", "Config", "MxFramework", "Vfx", "vfx_generation_templates.json");
        return VfxCreativeFlywheelJson.LoadFile<VfxGenerationTemplateCatalogDocument>(path);
    }

    private static T Clone<T>(T value) => VfxCreativeFlywheelJson.Deserialize<T>(VfxCreativeFlywheelJson.Serialize(value));

    private sealed class TestAuthorityVerifier : IVfxReadyApprovalAuthorityVerifier, IVfxPromotionSelectionAuthorityVerifier
    {
        public const string ReadyAuthorityId = "test.vfx.ready-authority";
        public const string ReadyAuthorityVersion = "1";
        public const string SelectionAuthorityId = "test.vfx.selection-authority";
        public const string SelectionAuthorityVersion = "1";
        public static readonly string ValidReadyReceiptHash = Hash('9');
        public static readonly string ValidSelectionReceiptHash = Hash('8');
        public static readonly TestAuthorityVerifier Instance = new TestAuthorityVerifier();

        string IVfxReadyApprovalAuthorityVerifier.AuthorityId => ReadyAuthorityId;
        string IVfxReadyApprovalAuthorityVerifier.AuthorityVersion => ReadyAuthorityVersion;
        string IVfxPromotionSelectionAuthorityVerifier.AuthorityId => SelectionAuthorityId;
        string IVfxPromotionSelectionAuthorityVerifier.AuthorityVersion => SelectionAuthorityVersion;

        public bool Verify(VfxReadyApprovalAuthorityRequest request, out string rejectionReason)
        {
            bool valid = request != null && string.Equals(request.AuthorityReceiptHash, ValidReadyReceiptHash, StringComparison.OrdinalIgnoreCase) && VfxCreativeFlywheelHash.IsSha256(request.AssetContentHash) && VfxCreativeFlywheelHash.IsSha256(request.EvaluationReportHash);
            rejectionReason = valid ? string.Empty : "test Ready receipt mismatch";
            return valid;
        }

        public bool Verify(VfxPromotionSelectionAuthorityRequest request, out string rejectionReason)
        {
            bool valid = request != null && string.Equals(request.AuthorityReceiptHash, ValidSelectionReceiptHash, StringComparison.OrdinalIgnoreCase) && VfxCreativeFlywheelHash.IsSha256(request.SelectionEvidenceHash) && VfxCreativeFlywheelHash.IsSha256(request.EvaluationReportHash);
            rejectionReason = valid ? string.Empty : "test selection receipt mismatch";
            return valid;
        }
    }
    private static string Hash(char digit) => "sha256:" + new string(digit, 64);

    private static int DispatchQuiet(string[] args)
    {
        TextWriter previous = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            return VfxAuthoringCommands.Dispatch(args, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
        }
        finally
        {
            Console.SetOut(previous);
        }
    }

    private static bool HasCode(List<AuthoringResourceDiagnostic> diagnostics, string code)
    {
        for (int i = 0; i < diagnostics.Count; i++) if (string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal)) return true;
        return false;
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(directory.FullName, "Assets"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate WGameFramework repo root.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
