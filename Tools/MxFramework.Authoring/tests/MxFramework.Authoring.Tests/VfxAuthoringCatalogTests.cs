using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static class VfxAuthoringCatalogTests
{
    public static void RunAll()
    {
        ValidCatalog_PassesAndRoundTripsStableCamelCaseJson();
        LegacyPlaceholder_CannotBecomeReadyOrFeedProduction();
        InvalidJson_ReturnsRecoverableSchemaDiagnostic();
        DuplicateStableId_IsRejectedAcrossEntryKinds();
        RecipeCycleAndInvalidParameterBinding_AreRejected();
        ParameterTypeAndRangeMismatch_AreRejected();
        RecipeAndEffectCompatibilityMismatch_AreRejected();
        VfxGraphReadyWithoutSupportOrFallback_IsBlocked();
        StrictCoverage_ChangesMissingEffectFromWarningToError();
        StaleReadyEffect_FailsReadyGateAndSortsDiagnostics();
        BakeHashAndPlan_AreStableAndDetectStaleEvidence();
        CanonicalReadyRecipe_RequiresBoundTwoVariantQualification();
        RepositorySeedCatalog_ValidatesAgainstCurrentEvidence();
        ProjectScanner_BuildsStableReportWithoutAbsolutePaths();
        CoverageProposal_ImportsAllDirectResourcesAndCanEnableStrictGate();
        SearchAndImpact_PrioritizeReuseAndExposeDependencies();
        SavePlan_UsesCatalogHashAndNeverWritesUnityAssets();
        SavePlan_BlocksV1MigrationInput();
        CliValidate_WritesStableJsonAndMarkdownReports();
    }

    private static void LegacyPlaceholder_CannotBecomeReadyOrFeedProduction()
    {
        VfxAuthoringCatalogDocument catalog = CreateValidCatalog();
        VfxAuthoringCatalogEvidence evidence = CreateValidEvidence(catalog);
        catalog.Primitives[0].VisualMaturity = VfxAuthoringVisualMaturities.LegacyPlaceholder;
        Require(HasCode(VfxAuthoringCatalogValidator.Validate(catalog, evidence).Diagnostics, VfxAuthoringDiagnosticCodes.VisualMaturityInvalid), "LegacyPlaceholder Primitive must never remain Ready or feed a Production Recipe.");

        catalog = CreateValidCatalog();
        evidence = CreateValidEvidence(catalog);
        catalog.Effects[0].VisualMaturity = VfxAuthoringVisualMaturities.LegacyPlaceholder;
        Require(HasCode(VfxAuthoringCatalogValidator.Validate(catalog, evidence).Diagnostics, VfxAuthoringDiagnosticCodes.VisualMaturityInvalid), "LegacyPlaceholder Effect must never become Ready.");
    }

    private static void CanonicalReadyRecipe_RequiresBoundTwoVariantQualification()
    {
        VfxAuthoringCatalogDocument catalog = CreateValidCatalog();
        VfxAuthoringCatalogEvidence evidence = CreateValidEvidence(catalog);
        VfxAuthoringRecipe recipe = catalog.Recipes[0];
        recipe.Canonical = true;
        VfxAuthoringCatalogValidationReport missing = VfxAuthoringCatalogValidator.Validate(catalog, evidence);
        Require(HasCode(missing.Diagnostics, VfxAuthoringDiagnosticCodes.RecipeReuseQualificationInvalid), "Canonical Ready Recipe must require immutable reuse qualification.");

        recipe.ReuseQualification = new VfxAuthoringRecipeReuseQualification
        {
            QualificationId = "vfx.qualification.test.impact",
            SpecializedBaselineId = "vfx.specialized.test.impact"
        };
        recipe.ReuseQualification.Variants.Add(new VfxAuthoringRecipeVariantQualification { VariantId = "vfx.variant.test.fire", ThemeId = "fire", ScaleProfileId = "medium", QualityDegradationPercent = 4f, PerformanceDegradationPercent = 5f, EvaluationReportHash = "sha256:" + new string('d', 64) });
        recipe.ReuseQualification.Variants.Add(new VfxAuthoringRecipeVariantQualification { VariantId = "vfx.variant.test.ice", ThemeId = "ice", ScaleProfileId = "medium", QualityDegradationPercent = 5.1f, PerformanceDegradationPercent = 2f, EvaluationReportHash = "sha256:" + new string('e', 64) });
        recipe.ReuseQualification.QualificationHash = VfxAuthoringApprovalHasher.ComputeRecipeReuseQualificationHash(recipe.ReuseQualification);
        recipe.Approval.RecipeReuseQualificationHash = recipe.ReuseQualification.QualificationHash;
        VfxAuthoringCatalogValidationReport overBudget = VfxAuthoringCatalogValidator.Validate(catalog, evidence);
        Require(HasCode(overBudget.Diagnostics, VfxAuthoringDiagnosticCodes.RecipeReuseQualificationInvalid), "Canonical Recipe must reject quality/performance degradation above 5 percent.");

        recipe.ReuseQualification.Variants[1].QualityDegradationPercent = 5f;
        recipe.ReuseQualification.QualificationHash = VfxAuthoringApprovalHasher.ComputeRecipeReuseQualificationHash(recipe.ReuseQualification);
        recipe.Approval.RecipeReuseQualificationHash = recipe.ReuseQualification.QualificationHash;
        VfxAuthoringCatalogValidationReport valid = VfxAuthoringCatalogValidator.Validate(catalog, evidence);
        Require(!HasCode(valid.Diagnostics, VfxAuthoringDiagnosticCodes.RecipeReuseQualificationInvalid), "Two distinct variants within 5 percent and bound by approval should qualify a canonical Recipe.");

        recipe.ReuseQualification.Variants[1].ThemeId = "fire";
        recipe.ReuseQualification.QualificationHash = VfxAuthoringApprovalHasher.ComputeRecipeReuseQualificationHash(recipe.ReuseQualification);
        recipe.Approval.RecipeReuseQualificationHash = recipe.ReuseQualification.QualificationHash;
        Require(HasCode(VfxAuthoringCatalogValidator.Validate(catalog, evidence).Diagnostics, VfxAuthoringDiagnosticCodes.RecipeReuseQualificationInvalid), "Canonical Recipe variants must differ by theme or scale.");
    }

    private static void SearchAndImpact_PrioritizeReuseAndExposeDependencies()
    {
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(FindRepoRoot());
        VfxAuthoringSearchReport search = VfxAuthoringSearchService.Search(scan.Catalog, new VfxAuthoringSearchQuery
        {
            Role = VfxAuthoringRoles.Flight,
            Theme = "fire",
            Backend = VfxAuthoringBackends.ParticleSystem
        });
        Require(search.Results.Count > 0 && string.Equals(search.Results[0].VisualMaturity, VfxAuthoringVisualMaturities.LegacyPlaceholder, StringComparison.Ordinal), "Repository search should expose the existing placeholder as contract evidence.");
        Require(search.Recommendation.Contains("not as visual reuse inputs", StringComparison.Ordinal), "Placeholder-only search must require original visual replacement rather than reuse.");

        VfxAuthoringImpactReport primitive = VfxAuthoringSearchService.AnalyzeImpact(scan.Catalog, "vfx.primitive.projectile.fire_core");
        Require(primitive.Found && !primitive.SafeToDelete, "Shared Primitive impact should block blind deletion.");
        Require(primitive.ReferencedByRecipes.Contains("vfx.recipe.projectile.fire_core.large"), "Primitive impact should expose consuming Recipe.");
        VfxAuthoringImpactReport effect = VfxAuthoringSearchService.AnalyzeImpact(scan.Catalog, "vfx.magic.fireball");
        Require(effect.ResourceAndDefinitionBindings.Count == 1, "Effect impact should expose ResourceKey and Definition binding.");
        Require(effect.ReferencedAsFallbackByEffects.Contains("vfx.magic.fireball_burst.flight"), "Effect impact should expose fallback consumers.");
    }

    private static void ValidCatalog_PassesAndRoundTripsStableCamelCaseJson()
    {
        VfxAuthoringCatalogDocument catalog = CreateValidCatalog();
        VfxAuthoringCatalogEvidence evidence = CreateValidEvidence(catalog);

        VfxAuthoringCatalogValidationReport report = VfxAuthoringCatalogValidator.Validate(catalog, evidence);
        Require(!report.HasErrors, "Valid VFX Authoring Catalog should pass.");
        evidence.ApprovalAuthorityVerifier = null;
        Require(HasCode(VfxAuthoringCatalogValidator.Validate(catalog, evidence).Diagnostics, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid), "Catalog approval fields must fail closed without a trusted authority verifier.");
        evidence.ApprovalAuthorityVerifier = TestReadyApprovalAuthorityVerifier.Instance;

        string json = VfxAuthoringCatalogJson.Serialize(catalog);
        Require(json.Contains("\"schemaVersion\"", StringComparison.Ordinal), "Serialized VFX catalog should use camelCase schemaVersion.");
        Require(json.Contains("\"primitiveId\"", StringComparison.Ordinal), "Serialized VFX catalog should use camelCase primitiveId.");
        Require(json.Contains("\"effectId\"", StringComparison.Ordinal), "Serialized VFX catalog should use camelCase effectId.");

        VfxAuthoringCatalogLoadResult loaded = VfxAuthoringCatalogJson.Load(json);
        Require(loaded.Success && loaded.Catalog != null, "Serialized VFX catalog should load.");
        Require(loaded.Catalog.Primitives.Count == 1, "Primitive should roundtrip.");
        Require(loaded.Catalog.Recipes.Count == 1, "Recipe should roundtrip.");
        Require(loaded.Catalog.Effects.Count == 1, "Effect should roundtrip.");
        Require(loaded.Catalog.Effects[0].ResourceKey.TypeId == "GameObject", "ResourceKey typeId should roundtrip.");
    }

    private static void InvalidJson_ReturnsRecoverableSchemaDiagnostic()
    {
        VfxAuthoringCatalogLoadResult result = VfxAuthoringCatalogJson.Load("{ invalid");

        Require(!result.Success, "Invalid VFX catalog JSON should fail without throwing.");
        Require(result.Catalog == null, "Invalid JSON must not produce a catalog.");
        Require(HasCode(result.Diagnostics, VfxAuthoringDiagnosticCodes.SchemaInvalid), "Invalid JSON should report VFXAUTH001.");
    }

    private static void DuplicateStableId_IsRejectedAcrossEntryKinds()
    {
        VfxAuthoringCatalogDocument catalog = CreateValidCatalog();
        catalog.Recipes[0].RecipeId = catalog.Primitives[0].PrimitiveId;

        VfxAuthoringCatalogValidationReport report = VfxAuthoringCatalogValidator.Validate(catalog);

        Require(report.HasErrors, "Duplicate cross-kind VFX stable ids should fail.");
        Require(HasCode(report.Diagnostics, VfxAuthoringDiagnosticCodes.StableIdDuplicate), "Duplicate stable id should report VFXAUTH002.");
    }

    private static void RecipeCycleAndInvalidParameterBinding_AreRejected()
    {
        VfxAuthoringCatalogDocument catalog = CreateValidCatalog();
        var second = new VfxAuthoringRecipe
        {
            RecipeId = "vfx.recipe.impact.secondary",
            DisplayName = "Secondary impact",
            Description = "Secondary reusable impact recipe.",
            ReviewStatus = VfxAuthoringReviewStatuses.ReviewNeeded
        };
        catalog.Recipes[0].IncludedRecipeIds.Add(second.RecipeId);
        second.IncludedRecipeIds.Add(catalog.Recipes[0].RecipeId);
        catalog.Recipes.Add(second);
        catalog.Recipes[0].Layers[0].Bindings["missing_primitive_parameter"] = "intensity";

        VfxAuthoringCatalogValidationReport report = VfxAuthoringCatalogValidator.Validate(catalog);

        Require(report.HasErrors, "Recipe cycle and invalid binding should fail.");
        Require(HasCode(report.Diagnostics, VfxAuthoringDiagnosticCodes.RecipeInvalid), "Recipe cycle should report VFXAUTH004.");
        Require(HasCode(report.Diagnostics, VfxAuthoringDiagnosticCodes.ParameterInvalid), "Invalid binding should report VFXAUTH005.");
    }

    private static void VfxGraphReadyWithoutSupportOrFallback_IsBlocked()
    {
        VfxAuthoringCatalogDocument catalog = CreateValidCatalog();
        catalog.Effects[0].Backend = VfxAuthoringBackends.VfxGraph;
        catalog.Effects[0].Quality.FallbackPolicy = VfxAuthoringFallbackPolicies.None;
        catalog.Effects[0].Quality.PlatformWhitelist.Clear();
        VfxAuthoringCatalogEvidence evidence = CreateValidEvidence(catalog);
        evidence.VfxGraphAvailable = false;

        VfxAuthoringCatalogValidationReport report = VfxAuthoringCatalogValidator.Validate(catalog, evidence);

        Require(report.HasErrors, "Ready VFX Graph effect without support/fallback should fail.");
        Require(HasCode(report.Diagnostics, VfxAuthoringDiagnosticCodes.BackendFallbackInvalid), "Missing Graph fallback should report VFXAUTH007.");
        Require(HasCode(report.Diagnostics, VfxAuthoringDiagnosticCodes.ReadyGateFailed), "Ready Graph effect with errors should report VFXAUTH012.");
    }

    private static void ParameterTypeAndRangeMismatch_AreRejected()
    {
        VfxAuthoringCatalogDocument catalog = CreateValidCatalog();
        VfxAuthoringParameterDefinition parameter = catalog.Primitives[0].Parameters[0];
        parameter.MinValue = JsonDocument.Parse("0.0").RootElement.Clone();
        parameter.MaxValue = JsonDocument.Parse("1.0").RootElement.Clone();
        catalog.Recipes[0].Layers[0].Overrides[parameter.ParameterId] = JsonDocument.Parse("\"high\"").RootElement.Clone();
        catalog.Effects[0].ParameterOverrides[parameter.ParameterId] = JsonDocument.Parse("2.0").RootElement.Clone();

        VfxAuthoringCatalogValidationReport report = VfxAuthoringCatalogValidator.Validate(catalog);

        Require(report.HasErrors, "Type and range mismatches should fail VFX parameter validation.");
        Require(HasCode(report.Diagnostics, VfxAuthoringDiagnosticCodes.ParameterInvalid), "Parameter mismatch should report VFXAUTH005.");
        Require(report.Diagnostics.Exists(item => item.Message.Contains("does not match", StringComparison.Ordinal)), "Layer override type mismatch should be explicit.");
        Require(report.Diagnostics.Exists(item => item.Message.Contains("exceeds", StringComparison.Ordinal)), "Effect override range mismatch should be explicit.");
    }

    private static void RecipeAndEffectCompatibilityMismatch_AreRejected()
    {
        VfxAuthoringCatalogDocument catalog = CreateValidCatalog();
        var incompatible = new VfxAuthoringPrimitive
        {
            PrimitiveId = "vfx.primitive.impact.line",
            DisplayName = "Impact line",
            Description = "Incompatible test primitive.",
            AssetRef = new VfxAuthoringAssetReference
            {
                Kind = VfxAuthoringAssetReferenceKinds.UnityAsset,
                Path = "Assets/VFX/Test/Primitives/ImpactLine.prefab"
            },
            Backend = VfxAuthoringBackends.LineRenderer,
            ReviewStatus = VfxAuthoringReviewStatuses.ReviewNeeded
        };
        incompatible.Roles.Add(VfxAuthoringRoles.Impact);
        incompatible.CompatibleAnchors.Add(VfxAuthoringAnchors.Ground);
        catalog.Primitives.Add(incompatible);
        catalog.Recipes[0].Layers.Add(new VfxAuthoringRecipeLayer
        {
            LayerId = "line",
            PrimitiveId = incompatible.PrimitiveId,
            Order = 20
        });

        VfxAuthoringCatalogValidationReport report = VfxAuthoringCatalogValidator.Validate(catalog);

        Require(report.HasErrors, "Recipe and Effect backend/Anchor mismatches should fail.");
        Require(HasCode(report.Diagnostics, VfxAuthoringDiagnosticCodes.CompatibilityInvalid), "Compatibility mismatch should report VFXAUTH006.");
        Require(report.Diagnostics.Exists(item => item.Message.Contains("backends", StringComparison.Ordinal)), "Recipe backend mismatch should be explicit.");
        Require(report.Diagnostics.Exists(item => item.Message.Contains("no compatible Anchor", StringComparison.Ordinal)), "Recipe Anchor mismatch should be explicit.");
    }

    private static void StrictCoverage_ChangesMissingEffectFromWarningToError()
    {
        VfxAuthoringCatalogDocument catalog = CreateValidCatalog();
        VfxAuthoringCatalogEvidence evidence = CreateValidEvidence(catalog);
        evidence.DirectVfxResourceKeys.Add("mxframework.samples|vfx.uncovered|GameObject|");

        catalog.StrictCoverage = false;
        VfxAuthoringCatalogValidationReport permissive = VfxAuthoringCatalogValidator.Validate(catalog, evidence);
        Require(!permissive.HasErrors, "Missing coverage should not block while strictCoverage=false.");
        Require(HasSeverity(permissive.Diagnostics, VfxAuthoringDiagnosticCodes.CoverageMissing, CharacterAuthoringValidationSeverity.Warning), "Permissive coverage should warn with VFXAUTH010.");

        catalog.StrictCoverage = true;
        VfxAuthoringCatalogValidationReport strict = VfxAuthoringCatalogValidator.Validate(catalog, evidence);
        Require(strict.HasErrors, "Missing coverage should block while strictCoverage=true.");
        Require(HasSeverity(strict.Diagnostics, VfxAuthoringDiagnosticCodes.CoverageMissing, CharacterAuthoringValidationSeverity.Error), "Strict coverage should error with VFXAUTH010.");
    }

    private static void StaleReadyEffect_FailsReadyGateAndSortsDiagnostics()
    {
        VfxAuthoringCatalogDocument catalog = CreateValidCatalog();
        VfxAuthoringCatalogEvidence evidence = CreateValidEvidence(catalog);
        evidence.StaleEffectIds.Add(catalog.Effects[0].EffectId);

        catalog.Effects[0].ReviewStatus = VfxAuthoringReviewStatuses.ReviewNeeded;
        VfxAuthoringCatalogValidationReport review = VfxAuthoringCatalogValidator.Validate(catalog, evidence);
        Require(!review.HasErrors, "Stale ReviewNeeded effect should remain reviewable.");
        Require(HasSeverity(review.Diagnostics, VfxAuthoringDiagnosticCodes.PublishedEffectStale, CharacterAuthoringValidationSeverity.Warning), "ReviewNeeded stale evidence should keep VFXAUTH009 as a warning.");

        catalog.Effects[0].ReviewStatus = VfxAuthoringReviewStatuses.Ready;
        VfxAuthoringCatalogValidationReport report = VfxAuthoringCatalogValidator.Validate(catalog, evidence);

        Require(report.HasErrors, "Stale Ready effect should fail without an unrelated validation error.");
        Require(HasCode(report.Diagnostics, VfxAuthoringDiagnosticCodes.PublishedEffectStale), "Stale effect should report VFXAUTH009.");
        Require(HasSeverity(report.Diagnostics, VfxAuthoringDiagnosticCodes.PublishedEffectStale, CharacterAuthoringValidationSeverity.Error), "Ready stale evidence should make VFXAUTH009 blocking.");
        Require(HasCode(report.Diagnostics, VfxAuthoringDiagnosticCodes.ReadyGateFailed), "Stale Ready effect should report VFXAUTH012.");
        for (int i = 1; i < report.Diagnostics.Count; i++)
        {
            int order = string.Compare(report.Diagnostics[i - 1].Code, report.Diagnostics[i].Code, StringComparison.Ordinal);
            Require(order <= 0, "VFX diagnostics should be sorted by stable code.");
        }
    }

    private static void BakeHashAndPlan_AreStableAndDetectStaleEvidence()
    {
        VfxAuthoringCatalogDocument catalog = CreateValidCatalog();
        VfxAuthoringEffect effect = catalog.Effects[0];
        VfxAuthoringCatalogEvidence evidence = CreateValidEvidence(catalog);
        string first = VfxAuthoringBakeHasher.ComputeRecipeInputHash(catalog, effect, effect.Published.BakerId, effect.Published.BakerVersion);
        string second = VfxAuthoringBakeHasher.ComputeRecipeInputHash(catalog, effect, effect.Published.BakerId, effect.Published.BakerVersion);
        Require(string.Equals(first, second, StringComparison.Ordinal), "Recipe input hash should be deterministic.");

        VfxAuthoringBakePlan current = VfxAuthoringBakePlanner.Plan(catalog, effect.EffectId, evidence);
        Require(!current.Blocked && !current.WouldWriteUnityPrefab, "Matching bake evidence and prefab path should be up to date.");
        Require(HasCode(current.Diagnostics, VfxAuthoringBakeDiagnosticCodes.UpToDate), "Up-to-date bake should report VFXBAKE005.");

        effect.ParameterOverrides["intensity"] = JsonDocument.Parse("1.25").RootElement.Clone();
        VfxAuthoringBakePlan stale = VfxAuthoringBakePlanner.Plan(catalog, effect.EffectId, evidence);
        Require(!stale.Blocked && stale.WouldWriteUnityPrefab && stale.WouldUpdateCatalogEvidence, "Changed Effect overrides should require a Unity bake and catalog evidence update.");
        Require(!string.Equals(first, stale.ExpectedRecipeInputHash, StringComparison.Ordinal), "Changed Recipe inputs should change the bake hash.");
        Require(HasCode(stale.Diagnostics, VfxAuthoringBakeDiagnosticCodes.UnityWriteRequired), "Stale bake should report VFXBAKE004.");
    }

    private static void RepositorySeedCatalog_ValidatesAgainstCurrentEvidence()
    {
        string root = FindRepoRoot();
        string catalogPath = Path.Combine(root, "Assets", "Config", "MxFramework", "Vfx", "vfx_authoring_catalog.json");
        VfxAuthoringCatalogLoadResult loaded = VfxAuthoringCatalogJson.LoadFile(catalogPath);
        Require(loaded.Success && loaded.Catalog != null, "Repository VFX Authoring Catalog seed should load.");

        VfxAuthoringCatalogEvidence evidence = ReadRepositoryEvidence(root);
        VfxAuthoringCatalogValidationReport report = VfxAuthoringCatalogValidator.Validate(loaded.Catalog, evidence);

        Require(!report.HasErrors, "Repository VFX Authoring Catalog seed should have no blocking diagnostics.");
        Require(loaded.Catalog.Effects.Count == evidence.DirectVfxResourceKeys.Count, "Repository Catalog should cover every direct CharacterTest/Samples VFX effect.");
        Require(loaded.Catalog.Primitives.Count == 1 && loaded.Catalog.Recipes.Count == 1, "Repository seed should include the fire core Primitive and large fire core Recipe.");
        Require(loaded.Catalog.StrictCoverage && !HasCode(report.Diagnostics, VfxAuthoringDiagnosticCodes.CoverageMissing), "Repository Catalog should enforce complete strict coverage.");
    }

    private static void ProjectScanner_BuildsStableReportWithoutAbsolutePaths()
    {
        string root = FindRepoRoot();
        VfxAuthoringProjectScanResult first = VfxAuthoringProjectScanner.Scan(root);
        VfxAuthoringProjectScanResult second = VfxAuthoringProjectScanner.Scan(root);

        Require(!first.Validation.HasErrors, "Repository VFX project scan should have no blocking diagnostics.");
        Require(first.Report.EffectCount == first.Report.DirectVfxResourceCount, "Project scan should report complete semantic coverage.");
        Require(first.Report.PrimitiveCount == 1 && first.Report.RecipeCount == 1, "Project scan should report the fire core Primitive and Recipe.");
        Require(!first.Evidence.StaleEffectIds.Contains("vfx.magic.fireball_burst.flight"), "Unity bake evidence should prove the sample Published Effect is current.");
        Require(first.Report.DirectVfxResourceCount == first.Report.CoveredDirectVfxResourceCount && first.Report.MissingCoverageCount == 0, "Strict project scan should have complete semantic coverage.");
        Require(first.Report.VfxGraphAvailable, "Project scan should detect the installed VFX Graph package.");
        Require(first.Report.FingerprintedPrefabCount > 0, "Project scan should fingerprint VFX prefabs.");
        Require(first.Report.PrefabFingerprints.Count == first.Report.FingerprintedPrefabCount, "Fingerprint summary should match stable evidence rows.");
        Require(first.Report.ReuseCandidateGroupCount > 0, "Current sample VFX should expose structural-profile reuse candidates.");
        Require(first.Report.ReuseCandidateGroups.Count == first.Report.ReuseCandidateGroupCount, "Reuse candidate summary should match stable evidence groups.");
        Require(first.Report.SemanticCoverageRate == 1f, "Strict project report should expose 100% semantic coverage.");
        Require(first.Report.ProductionEffectCount == 0 && first.Report.LegacyPlaceholderEffectCount == 37 && first.Report.ProductionCoverageRate == 0f, "Repository report must separate 37/37 semantic coverage from 0/37 Production coverage.");
        Require(first.Report.RecipeBackedEffectCount == 1 && first.Report.RecipeEffectRate > 0f, "Report should count the first Recipe-backed Effect.");
        Require(first.Report.ReusedPrimitiveCount == 1 && first.Report.PrimitiveReuseRate == 1f, "Report should reflect single primitive reused across recipes and effects.");
        Require(first.Report.DuplicateCandidateAssetCount > 0 && first.Report.DuplicateCandidateAssetRate > 0f, "Report should quantify duplicate-candidate assets.");
        Require(first.Report.ObservedAssetContentHashCount == first.Evidence.AssetContentHashes.Count && first.Report.ObservedDependencyHashCount == first.Evidence.DependencyHashes.Count && first.Report.ObservedAssetContentHashCount > 0 && first.Report.ObservedDependencyHashCount > 0, "Production scan/report should expose rebuildable asset and dependency approval evidence.");
        Require(first.Report.ObservedEvaluationReportHashCount == first.Evidence.EvaluationReportHashes.Count && first.Report.ObservedQualityProfileVersionCount == first.Evidence.QualityProfileVersions.Count, "Production report evidence counters must match observed evaluation/profile evidence without fabricating missing Ready evidence.");
        Require(string.Equals(first.Report.MarkdownReport, second.Report.MarkdownReport, StringComparison.Ordinal), "Repeated VFX project scans should produce stable Markdown.");
        Require(!first.Report.MarkdownReport.Contains(root, StringComparison.OrdinalIgnoreCase), "Stable VFX report must not contain the absolute repository path.");
        Require(!first.Report.MarkdownReport.Contains("generated at", StringComparison.OrdinalIgnoreCase), "Stable VFX report must not contain timestamps.");
    }

    private static void CoverageProposal_ImportsAllDirectResourcesAndCanEnableStrictGate()
    {
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(FindRepoRoot());
        VfxAuthoringCatalogDocument proposal = VfxAuthoringCoverageImporter.CreateProposal(scan, enableStrictCoverage: true);
        VfxAuthoringCatalogValidationReport validation = VfxAuthoringCatalogValidator.Validate(proposal, scan.Evidence);

        Require(proposal.StrictCoverage, "Coverage proposal should enable strict mode when explicitly requested.");
        Require(proposal.Effects.Count == scan.Evidence.DirectVfxResourceKeys.Count, "Coverage proposal should create one semantic Effect per direct VFX resource.");
        Require(!HasCode(validation.Diagnostics, VfxAuthoringDiagnosticCodes.CoverageMissing), "Complete strict proposal should have no coverage diagnostics.");
        Require(!validation.HasErrors, "Inferred ReviewNeeded coverage proposal should pass structural validation without approving artistic quality.");
    }

    private static void SavePlan_UsesCatalogHashAndNeverWritesUnityAssets()
    {
        string root = FindRepoRoot();
        VfxAuthoringProjectScanResult current = VfxAuthoringProjectScanner.Scan(root);
        VfxAuthoringCatalogLoadResult proposedLoad = VfxAuthoringCatalogJson.LoadFile(Path.Combine(root, current.CatalogPath.Replace('/', Path.DirectorySeparatorChar)));
        Require(proposedLoad.Success && proposedLoad.Catalog != null, "Proposed VFX catalog fixture should load.");
        proposedLoad.Catalog.Effects[0].Description += " Planned semantic edit.";

        VfxAuthoringOperationPlan accepted = VfxAuthoringOperationPlanner.PlanSaveCatalog(root, current.CatalogHash, proposedLoad.Catalog);
        Require(!accepted.Blocked, "Current hash plus valid semantics should produce an accepted save plan.");
        Require(accepted.WouldModifyCatalog, "Changed semantics should declare a catalog modification.");
        Require(!accepted.WouldModifyUnityAssets && !accepted.WouldModifyPublishedOutput && !accepted.RequiresUnityAuthoring, "Semantic save plan must not write Unity or published assets.");
        Require(HasCode(accepted.Diagnostics, VfxAuthoringOperationDiagnosticCodes.CatalogOnly), "Accepted semantic save should report catalog-only impact.");

        VfxAuthoringOperationPlan stale = VfxAuthoringOperationPlanner.PlanSaveCatalog(root, "stale", proposedLoad.Catalog);
        Require(stale.Blocked, "Stale expected hash should block VFX catalog save.");
        Require(HasCode(stale.Diagnostics, VfxAuthoringOperationDiagnosticCodes.CatalogConflict), "Stale hash should report VFXOP002.");
    }

    private static void SavePlan_BlocksV1MigrationInput()
    {
        string root = FindRepoRoot();
        VfxAuthoringProjectScanResult current = VfxAuthoringProjectScanner.Scan(root);
        VfxAuthoringCatalogLoadResult proposedLoad = VfxAuthoringCatalogJson.LoadFile(Path.Combine(root, current.CatalogPath.Replace('/', Path.DirectorySeparatorChar)));
        Require(proposedLoad.Success && proposedLoad.Catalog != null, "Proposed VFX catalog fixture should load.");
        proposedLoad.Catalog.SchemaVersion = VfxAuthoringSchemaVersions.V1;

        VfxAuthoringOperationPlan plan = VfxAuthoringOperationPlanner.PlanSaveCatalog(root, current.CatalogHash, proposedLoad.Catalog);

        Require(plan.Blocked, "Catalog v1 must remain read-only migration input and cannot be saved.");
        Require(HasCode(plan.Diagnostics, VfxAuthoringOperationDiagnosticCodes.CatalogV2Required), "Saving a v1 proposal should report VFXOP005 with an explicit migration requirement.");
        Require(!plan.WouldModifyCatalog, "Blocked v1 proposals must not advertise a catalog write.");
    }

    private static void CliValidate_WritesStableJsonAndMarkdownReports()
    {
        string root = FindRepoRoot();
        string outputRoot = Path.Combine(root, "Temp", "MxFrameworkAuthoringTests", "vfx-authoring-report");
        Directory.CreateDirectory(outputRoot);
        string jsonPath = Path.Combine(outputRoot, "vfx_authoring_report.json");
        string markdownPath = Path.Combine(outputRoot, "vfx_authoring_report.md");

        TextWriter previous = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            int exitCode = VfxAuthoringCommands.Dispatch(
                new[]
                {
                    "vfx", "validate",
                    "--project-root", root,
                    "--json-report", jsonPath,
                    "--markdown-report", markdownPath
                },
                MxFramework.Authoring.Cli.Program.CreateJsonOptions());
            Require(exitCode == AuthoringExitCodes.Ready, "VFX validate CLI should succeed with complete strict coverage.");
        }
        finally
        {
            Console.SetOut(previous);
        }

        Require(File.Exists(jsonPath) && File.Exists(markdownPath), "VFX validate CLI should write both requested reports.");
        VfxAuthoringProjectReport report = JsonSerializer.Deserialize<VfxAuthoringProjectReport>(File.ReadAllText(jsonPath), MxFramework.Authoring.Cli.Program.CreateJsonOptions());
        Require(report != null && report.SchemaVersion == "mx.vfx.authoring.report.v1", "VFX JSON report should expose the stable report schema.");
        Require(string.Equals(report.MarkdownReport, File.ReadAllText(markdownPath), StringComparison.Ordinal), "JSON embedded Markdown and .md report should match exactly.");
    }

    private static VfxAuthoringCatalogDocument CreateValidCatalog()
    {
        var primitive = new VfxAuthoringPrimitive
        {
            PrimitiveId = "vfx.primitive.impact.flash",
            DisplayName = "Impact flash",
            Description = "Reusable radial impact flash.",
            AssetRef = new VfxAuthoringAssetReference
            {
                Kind = VfxAuthoringAssetReferenceKinds.UnityAsset,
                Path = "Assets/VFX/MxFramework/Samples/Primitives/Impact/Flash.prefab"
            },
            Backend = VfxAuthoringBackends.ParticleSystem,
            VisualMaturity = VfxAuthoringVisualMaturities.Production,
            ReviewStatus = VfxAuthoringReviewStatuses.Ready
        };
        primitive.Roles.Add(VfxAuthoringRoles.Impact);
        primitive.CompatibleAnchors.Add(VfxAuthoringAnchors.HitPoint);
        primitive.Parameters.Add(new VfxAuthoringParameterDefinition
        {
            ParameterId = "intensity",
            ValueType = VfxAuthoringParameterValueTypes.Float,
            Unit = "Normalized",
            Transport = VfxAuthoringParameterTransports.StaticBake,
            RuntimeScale = 1f
        });

        var recipe = new VfxAuthoringRecipe
        {
            RecipeId = "vfx.recipe.projectile_impact.small",
            DisplayName = "Small projectile impact",
            Description = "Reusable small projectile impact recipe.",
            VisualMaturity = VfxAuthoringVisualMaturities.Production,
            ReviewStatus = VfxAuthoringReviewStatuses.Ready
        };
        recipe.Parameters.Add("intensity");
        var layer = new VfxAuthoringRecipeLayer
        {
            LayerId = "flash",
            PrimitiveId = primitive.PrimitiveId,
            Order = 10
        };
        layer.Bindings.Add("intensity", "intensity");
        recipe.Layers.Add(layer);

        var effect = new VfxAuthoringEffect
        {
            EffectId = "vfx.magic.test.impact",
            DisplayName = "Test impact",
            Description = "Test published effect for VFX authoring validation.",
            ResourceKey = new VfxAuthoringResourceKey
            {
                Id = "vfx.magic.test.impact",
                TypeId = "GameObject",
                PackageId = "mxframework.samples"
            },
            DefinitionId = 12999,
            RecipeId = recipe.RecipeId,
            Backend = VfxAuthoringBackends.ParticleSystem,
            AnchorContract = new VfxAuthoringAnchorContract
            {
                Anchor = VfxAuthoringAnchors.HitPoint,
                Space = VfxAuthoringSpaces.World
            },
            Lifecycle = new VfxAuthoringLifecycle
            {
                Mode = VfxAuthoringLifecycleModes.OneShot,
                DefaultLifetimeFrames = 24,
                StopPolicy = VfxAuthoringStopPolicies.Expire
            },
            Quality = new VfxAuthoringQuality
            {
                CostTier = VfxAuthoringCostTiers.Medium,
                FallbackPolicy = VfxAuthoringFallbackPolicies.None
            },
            Budget = new VfxAuthoringBudget
            {
                PrewarmCount = 1,
                RecommendedMaxConcurrent = 8,
                Priority = VfxAuthoringPriorities.GameplayImportant,
                CullDistanceMeters = 40f
            },
            Preview = new VfxAuthoringPreview
            {
                AnchorPreset = VfxAuthoringAnchors.HitPoint,
                TargetDistanceMeters = 4f,
                DurationSeconds = 1f,
                CameraScaleMeters = 3f
            },
            VisualMaturity = VfxAuthoringVisualMaturities.Production,
            ReviewStatus = VfxAuthoringReviewStatuses.Ready
        };
        effect.Roles.Add(VfxAuthoringRoles.Impact);
        effect.ThemeIds.Add("fire");

        var catalog = new VfxAuthoringCatalogDocument();
        catalog.Primitives.Add(primitive);
        catalog.Recipes.Add(recipe);
        catalog.Effects.Add(effect);
        effect.Published = new VfxAuthoringPublishedEffectEvidence
        {
            AssetPath = "Assets/VFX/Test/Published/TestImpact.prefab",
            BakerId = VfxAuthoringBakerIds.ParticleSystemPrefab,
            BakerVersion = VfxAuthoringBakerIds.ParticleSystemPrefabVersion
        };
        effect.Published.RecipeInputHash = VfxAuthoringBakeHasher.ComputeRecipeInputHash(catalog, effect, effect.Published.BakerId, effect.Published.BakerVersion);
        primitive.Approval = CreateApproval('a');
        recipe.Approval = CreateApproval('b');
        effect.Approval = CreateApproval('c');
        effect.Approval.RecipeInputHash = effect.Published.RecipeInputHash;
        effect.Approval.HumanApprovals.Add(new VfxAuthoringHumanApproval { ReviewerId = "gameplay-reviewer", Role = VfxAuthoringApprovalRoles.Gameplay });
        return catalog;
    }

    private static VfxAuthoringApprovalEvidence CreateApproval(char hashDigit)
    {
        string hash = "sha256:" + new string(hashDigit, 64);
        var approval = new VfxAuthoringApprovalEvidence
        {
            ApprovalId = "approval.test." + hashDigit,
            ApprovedBy = "vfx-reviewer",
            ApprovalRole = VfxAuthoringApprovalRoles.VfxArtist,
            AssetContentHash = hash,
            DependencyHash = hash,
            EvaluationReportHash = hash,
            QualityProfileVersion = "1",
            ApprovalAuthorityId = TestReadyApprovalAuthorityVerifier.AuthorityIdValue,
            ApprovalAuthorityVersion = TestReadyApprovalAuthorityVerifier.AuthorityVersionValue,
            AuthorityReceiptHash = TestReadyApprovalAuthorityVerifier.ValidReceiptHash
        };
        approval.HumanApprovals.Add(new VfxAuthoringHumanApproval { ReviewerId = "vfx-reviewer", Role = VfxAuthoringApprovalRoles.VfxArtist });
        return approval;
    }

    private static VfxAuthoringCatalogEvidence CreateValidEvidence(VfxAuthoringCatalogDocument catalog)
    {
        var evidence = new VfxAuthoringCatalogEvidence { VfxGraphAvailable = true, ApprovalAuthorityVerifier = TestReadyApprovalAuthorityVerifier.Instance };
        evidence.UnityAssetPaths.Add(catalog.Primitives[0].AssetRef.Path);
        evidence.UnityAssetPaths.Add(catalog.Effects[0].Published.AssetPath);
        evidence.PublishedRecipeInputHashes[catalog.Effects[0].EffectId] = catalog.Effects[0].Published.RecipeInputHash;
        evidence.PublishedAssetPaths[catalog.Effects[0].EffectId] = catalog.Effects[0].Published.AssetPath;
        evidence.ResourceKeys.Add(catalog.Effects[0].ResourceKey.ToStableKey());
        evidence.DirectVfxResourceKeys.Add(catalog.Effects[0].ResourceKey.ToStableKey());
        evidence.DefinitionIds.Add(catalog.Effects[0].DefinitionId);
        ObserveApproval(evidence, catalog.Primitives[0].PrimitiveId, catalog.Primitives[0].Approval);
        ObserveApproval(evidence, catalog.Recipes[0].RecipeId, catalog.Recipes[0].Approval);
        ObserveApproval(evidence, catalog.Effects[0].EffectId, catalog.Effects[0].Approval);
        return evidence;
    }

    private static void ObserveApproval(VfxAuthoringCatalogEvidence evidence, string stableId, VfxAuthoringApprovalEvidence approval)
    {
        evidence.AssetContentHashes[stableId] = approval.AssetContentHash;
        evidence.DependencyHashes[stableId] = approval.DependencyHash;
        evidence.EvaluationReportHashes[stableId] = approval.EvaluationReportHash;
        evidence.QualityProfileVersions[stableId] = approval.QualityProfileVersion;
    }

    private static VfxAuthoringCatalogEvidence ReadRepositoryEvidence(string root)
    {
        var evidence = new VfxAuthoringCatalogEvidence { VfxGraphAvailable = true };
        string resourceCatalogPath = Path.Combine(root, "Assets", "Config", "MxFramework", "ResourceCatalogs", "mxframework_samples_resource_catalog.json");
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(resourceCatalogPath)))
        {
            foreach (JsonElement entry in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                string id = entry.GetProperty("id").GetString() ?? string.Empty;
                string type = entry.GetProperty("type").GetString() ?? string.Empty;
                string variant = entry.GetProperty("variant").GetString() ?? string.Empty;
                string packageId = entry.GetProperty("packageId").GetString() ?? string.Empty;
                string key = packageId + "|" + id + "|" + type + "|" + variant;
                evidence.ResourceKeys.Add(key);

                bool isDomainVfx = false;
                foreach (JsonElement label in entry.GetProperty("labels").EnumerateArray())
                    if (string.Equals(label.GetString(), "domain.vfx", StringComparison.Ordinal))
                        isDomainVfx = true;
                if (isDomainVfx && string.Equals(type, "GameObject", StringComparison.Ordinal))
                    evidence.DirectVfxResourceKeys.Add(key);
            }
        }

        string definitionPath = Path.Combine(root, "Assets", "MxFrameworkGenerated", "Vfx", "vfx_definitions.json");
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(definitionPath)))
            foreach (JsonElement definition in document.RootElement.GetProperty("definitions").EnumerateArray())
                evidence.DefinitionIds.Add(definition.GetProperty("definitionId").GetInt32());

        return evidence;
    }

    private static bool HasCode(List<AuthoringResourceDiagnostic> diagnostics, string code)
    {
        for (int i = 0; i < diagnostics.Count; i++)
            if (string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                return true;
        return false;
    }

    private sealed class TestReadyApprovalAuthorityVerifier : IVfxReadyApprovalAuthorityVerifier
    {
        public const string AuthorityIdValue = "test.vfx.ready-authority";
        public const string AuthorityVersionValue = "1";
        public static readonly string ValidReceiptHash = "sha256:" + new string('9', 64);
        public static readonly TestReadyApprovalAuthorityVerifier Instance = new TestReadyApprovalAuthorityVerifier();

        public string AuthorityId => AuthorityIdValue;
        public string AuthorityVersion => AuthorityVersionValue;

        public bool Verify(VfxReadyApprovalAuthorityRequest request, out string rejectionReason)
        {
            bool valid = request != null && string.Equals(request.AuthorityReceiptHash, ValidReceiptHash, StringComparison.OrdinalIgnoreCase) && VfxCreativeFlywheelHash.IsSha256(request.AssetContentHash) && VfxCreativeFlywheelHash.IsSha256(request.EvaluationReportHash);
            rejectionReason = valid ? string.Empty : "test Ready receipt mismatch";
            return valid;
        }
    }

    private static bool HasSeverity(List<AuthoringResourceDiagnostic> diagnostics, string code, CharacterAuthoringValidationSeverity severity)
    {
        for (int i = 0; i < diagnostics.Count; i++)
            if (string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal) && diagnostics[i].Severity == severity)
                return true;
        return false;
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(directory.FullName, "Assets")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate WGameFramework repo root.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
