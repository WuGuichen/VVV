using System.Collections.Generic;

namespace MxFramework.Authoring
{
    /// <summary>Stable schema identifiers for the Development Agent VFX creative flywheel.</summary>
    public static class VfxCreativeFlywheelSchemaVersions
    {
        public const string GenerationTemplatesV1 = "mx.vfx.generation-templates.v1";
        public const string CreativeBriefV1 = "mx.vfx.creative-brief.v1";
        public const string ReuseDecisionV1 = "mx.vfx.reuse-decision.v1";
        public const string GenerationPlanV1 = "mx.vfx.generation-plan.v1";
        public const string PlanBundleV1 = "mx.vfx.plan-bundle.v1";
        public const string CandidateManifestV1 = "mx.vfx.candidate-manifest.v1";
        public const string CandidateReceiptV1 = "mx.vfx.candidate-receipt.v1";
        public const string EvaluationInputV1 = "mx.vfx.evaluation-input.v1";
        public const string EvaluationReportV1 = "mx.vfx.evaluation-report.v1";
        public const string PromotionRequestV1 = "mx.vfx.promotion-request.v1";
        public const string PromotionPlanV1 = "mx.vfx.promotion-plan.v1";
        public const string PromotionReceiptV1 = "mx.vfx.promotion-receipt.v1";
        public const string RegistrationRequestV1 = "mx.vfx.registration-request.v1";
        public const string RegistrationPlanV1 = "mx.vfx.registration-plan.v1";
        public const string SessionCapabilitiesV1 = "mx.vfx.generation-session-capabilities.v1";
    }

    public static class VfxGenerationProviderKinds
    {
        public const string DevelopmentAgent = "DevelopmentAgent";
        public const string BuiltInProcedural = "BuiltInProcedural";
        public const string LocalCli = "LocalCli";
        public const string LocalHttp = "LocalHttp";
        public const string Cloud = "Cloud";
        public const string UnityTemplate = "UnityTemplate";
        public const string ExperimentalUnityGraph = "ExperimentalUnityGraph";
    }

    public static class VfxGenerationTemplateKinds
    {
        public const string ParticleSystemPrimitive = "ParticleSystemPrimitive";
        public const string LineRendererPrimitive = "LineRendererPrimitive";
        public const string VfxGraph = "VfxGraph";
        public const string ShaderGraph = "ShaderGraph";
        public const string VfxSystemSubgraph = "VfxSystemSubgraph";
        public const string VfxBlockSubgraph = "VfxBlockSubgraph";
        public const string VfxOperatorSubgraph = "VfxOperatorSubgraph";
    }

    public static class VfxReuseDecisionKinds
    {
        public const string ReusePublishedEffect = "ReusePublishedEffect";
        public const string ComposeReadyRecipe = "ComposeReadyRecipe";
        public const string GeneratePrimitive = "GeneratePrimitive";
    }

    public static class VfxGenerationModes
    {
        public const string ReuseOnly = "ReuseOnly";
        public const string ControlledTemplate = "ControlledTemplate";
        public const string ExperimentalGraph = "ExperimentalGraph";
    }

    public static class VfxRoundZeroVariantKinds
    {
        public const string ConservativeReuse = "ConservativeReuse";
        public const string HighReadability = "HighReadability";
        public const string LowBudget = "LowBudget";
        public const string StrongImpact = "StrongImpact";
        public const string ThemeVariant = "ThemeVariant";
        public const string NewPrimitiveExploration = "NewPrimitiveExploration";

        public static readonly string[] Ordered =
        {
            ConservativeReuse,
            HighReadability,
            LowBudget,
            StrongImpact,
            ThemeVariant,
            NewPrimitiveExploration
        };
    }

    public static class VfxPromotionTargetKinds
    {
        public const string Primitive = "Primitive";
        public const string Recipe = "Recipe";
        public const string Effect = "Effect";
    }

    public static class VfxCandidateAssetKinds
    {
        public const string Texture = "Texture";
        public const string Material = "Material";
        public const string Prefab = "Prefab";
        public const string VfxGraph = "VfxGraph";
        public const string ShaderGraph = "ShaderGraph";
        public const string Subgraph = "Subgraph";
        public const string ReceiptEvidence = "ReceiptEvidence";
    }

    public static class VfxHardGateIds
    {
        public const string NoConsoleOrRenderErrors = "NoConsoleOrRenderErrors";
        public const string NoMagentaMaterial = "NoMagentaMaterial";
        public const string NoNanOrMissingFrames = "NoNanOrMissingFrames";
        public const string CropWithinLimit = "CropWithinLimit";
        public const string AnchorWithinLimit = "AnchorWithinLimit";
        public const string PathEndpointWithinLimit = "PathEndpointWithinLimit";
        public const string TelegraphFootprint = "TelegraphFootprint";
        public const string CleanupWithinOneSecond = "CleanupWithinOneSecond";
        public const string NoGhostAfterTwentyCycles = "NoGhostAfterTwentyCycles";
        public const string FallbackTimingAndRange = "FallbackTimingAndRange";
        public const string PerformanceProfilePassed = "PerformanceProfilePassed";
        public const string ZeroSteadyStateGc = "ZeroSteadyStateGc";
    }

    public static class VfxFlywheelDiagnosticCodes
    {
        public const string SchemaInvalid = "VFXFLY001";
        public const string CatalogStale = "VFXFLY002";
        public const string TemplateCatalogStale = "VFXFLY003";
        public const string PlanTampered = "VFXFLY004";
        public const string ReceiptTampered = "VFXFLY005";
        public const string SandboxViolation = "VFXFLY006";
        public const string ProvenanceInvalid = "VFXFLY007";
        public const string CandidateInvalid = "VFXFLY008";
        public const string EvaluationBlocked = "VFXFLY009";
        public const string HumanApprovalRequired = "VFXFLY010";
        public const string PromotionConflict = "VFXFLY011";
        public const string ReadyForbidden = "VFXFLY012";
        public const string ExperimentalDraftOnly = "VFXFLY013";
        public const string RegistrationGuardRequired = "VFXFLY014";
        public const string ProviderUnavailable = "VFXFLY015";
        public const string CapabilityInvalid = "VFXFLY016";
    }

    /// <summary>Authoring-only registry of controlled generation providers, styles, templates, quality profiles, and exemplars.</summary>
    /// <remarks>LowFreqAlloc. JSON DTO used by planning and validation; not referenced by Runtime.</remarks>
    public sealed class VfxGenerationTemplateCatalogDocument
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.GenerationTemplatesV1;
        public string CatalogId { get; set; } = "mxframework.vfx.generation-templates";
        public string CatalogVersion { get; set; } = "1";
        public List<VfxGenerationProviderDefinition> Providers { get; set; } = new List<VfxGenerationProviderDefinition>();
        public List<VfxStyleProfile> StyleProfiles { get; set; } = new List<VfxStyleProfile>();
        public List<VfxGenerationTemplateDefinition> Templates { get; set; } = new List<VfxGenerationTemplateDefinition>();
        public List<VfxQualityProfile> QualityProfiles { get; set; } = new List<VfxQualityProfile>();
    }

    public sealed class VfxGenerationTemplateCatalogValidationReport
    {
        public bool HasErrors { get; set; }
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
    }

    /// <summary>One local-first or explicitly enabled generation provider.</summary>
    /// <remarks>LowFreqAlloc. Credentials and machine-local endpoints are intentionally excluded.</remarks>
    public sealed class VfxGenerationProviderDefinition
    {
        public string ProviderId { get; set; } = string.Empty;
        public string ProviderVersion { get; set; } = string.Empty;
        public string Kind { get; set; } = VfxGenerationProviderKinds.BuiltInProcedural;
        public bool EnabledByDefault { get; set; } = true;
        public bool AllowsCloud { get; set; }
        public string ModelId { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public List<string> AllowedLicenseIds { get; set; } = new List<string>();
    }

    /// <summary>Machine-local allowlist for one generation session.</summary>
    /// <remarks>LowFreqAlloc. This capability document is supplied outside the repository; credentials and endpoints are never represented.</remarks>
    public sealed class VfxGenerationSessionCapabilities
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.SessionCapabilitiesV1;
        public string CapabilityId { get; set; } = string.Empty;
        public bool AllowCloud { get; set; }
        public List<VfxGenerationProviderCapabilityGrant> ProviderGrants { get; set; } = new List<VfxGenerationProviderCapabilityGrant>();
        public List<string> AllowedExperimentalAdapterIds { get; set; } = new List<string>();
    }

    /// <summary>Exact machine-local provider/model grant copied into the immutable generation plan without credentials or endpoints.</summary>
    public sealed class VfxGenerationProviderCapabilityGrant
    {
        public string GrantId { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string ProviderVersion { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
    }

    public sealed class VfxStyleProfile
    {
        public string StyleProfileId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public List<string> ThemeIds { get; set; } = new List<string>();
        public List<string> Keywords { get; set; } = new List<string>();
        public List<string> ExemplarIds { get; set; } = new List<string>();
    }

    public sealed class VfxGenerationTemplateDefinition
    {
        public string TemplateId { get; set; } = string.Empty;
        public string TemplateVersion { get; set; } = string.Empty;
        public string Kind { get; set; } = VfxGenerationTemplateKinds.ParticleSystemPrimitive;
        public string Backend { get; set; } = VfxAuthoringBackends.ParticleSystem;
        public string AssetPath { get; set; } = string.Empty;
        public bool Certified { get; set; }
        public bool ArbitraryTopology { get; set; }
        public string ExperimentalAdapterId { get; set; } = string.Empty;
        public string ExperimentalAdapterVersion { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public List<string> CompatibleAnchors { get; set; } = new List<string>();
        public List<string> AllowedProviderIds { get; set; } = new List<string>();
        public List<VfxAuthoringParameterDefinition> ParameterContracts { get; set; } = new List<VfxAuthoringParameterDefinition>();
        public VfxGenerationBudgetRange BudgetRange { get; set; } = new VfxGenerationBudgetRange();
        public List<string> ExemplarIds { get; set; } = new List<string>();
    }

    public sealed class VfxGenerationBudgetRange
    {
        public string MinimumCostTier { get; set; } = VfxAuthoringCostTiers.Low;
        public string MaximumCostTier { get; set; } = VfxAuthoringCostTiers.High;
        public int MaximumParticleCount { get; set; }
        public int MaximumRendererCount { get; set; }
    }

    /// <summary>Versioned platform quality contract consumed by deterministic Unity evaluation.</summary>
    /// <remarks>LowFreqAlloc. Thresholds are data, not hardcoded validator policy.</remarks>
    public sealed class VfxQualityProfile
    {
        public string QualityProfileId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Platform { get; set; } = "Windows";
        public string GraphicsApi { get; set; } = "D3D12";
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public int FramesPerSecond { get; set; } = 60;
        public double FixedStepSeconds { get; set; } = 1d / 60d;
        public List<int> FixedSeeds { get; set; } = new List<int> { 17, 73, 211 };
        public List<int> OneShotSlicePercents { get; set; } = new List<int> { 0, 10, 25, 50, 75, 100 };
        public float LoopWarmupSeconds { get; set; } = 1f;
        public float LoopSteadySeconds { get; set; } = 2f;
        public float LoopCleanupSeconds { get; set; } = 1f;
        public List<string> CaptureViewIds { get; set; } = new List<string> { "Gameplay", "FrontThreeQuarter", "Side" };
        public List<string> CaptureBackgroundIds { get; set; } = new List<string> { "DarkNeutral", "GameplayBusy", "LightNeutral", "SameColorConflict" };
        public float MaximumCropPercent { get; set; } = 0.5f;
        public float MaximumAnchorErrorPercent { get; set; } = 5f;
        public float MaximumPathEndpointErrorPercent { get; set; } = 2f;
        public float MinimumTelegraphFootprintIou { get; set; } = 0.90f;
        public float MaximumFallbackPeakTimeDeltaPercent { get; set; } = 10f;
        public float MaximumFallbackVisibleRangeDeltaPercent { get; set; } = 15f;
        public int MaximumActiveParticlesAfterCleanup { get; set; }
        public float MaximumBrightPixelPercentAfterCleanup { get; set; }
        public int GhostCycleCount { get; set; } = 20;
        public float CriticalTelegraphContrastRatio { get; set; } = 3f;
        public float GameplayImportantContrastRatio { get; set; } = 2f;
        public float MaximumOverexposedPixelPercent { get; set; }
        public int MaximumConsecutiveOverexposedFrames { get; set; }
        public int MaximumSteadyStateGcBytesPerFrame { get; set; }
        public int PerformanceWarmupFrames { get; set; } = 120;
        public int PerformanceSampleFrames { get; set; } = 600;
        public List<VfxPerformanceTierBudget> PerformanceTierBudgets { get; set; } = new List<VfxPerformanceTierBudget>
        {
            new VfxPerformanceTierBudget { CostTier = VfxAuthoringCostTiers.Low, CpuP95Milliseconds = 0.25f, GpuP95Milliseconds = 0.5f },
            new VfxPerformanceTierBudget { CostTier = VfxAuthoringCostTiers.Medium, CpuP95Milliseconds = 0.5f, GpuP95Milliseconds = 1f },
            new VfxPerformanceTierBudget { CostTier = VfxAuthoringCostTiers.High, CpuP95Milliseconds = 1f, GpuP95Milliseconds = 2f }
        };
        public int MinimumTotalScore { get; set; } = 80;
        public int MinimumDimensionScore { get; set; } = 60;
        public int MaximumReviewerSpread { get; set; } = 15;
        public int MaximumRounds { get; set; } = 3;
        public int MinimumRoundImprovement { get; set; } = 3;
        public List<string> RequiredHardGateIds { get; set; } = new List<string>();
    }

    public sealed class VfxPerformanceTierBudget
    {
        public string CostTier { get; set; } = VfxAuthoringCostTiers.Medium;
        public float CpuP95Milliseconds { get; set; }
        public float GpuP95Milliseconds { get; set; }
    }

    /// <summary>Intent contract for one isolated VFX creation session.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO.</remarks>
    public sealed class VfxCreativeBrief
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.CreativeBriefV1;
        public string BriefId { get; set; } = string.Empty;
        public string CatalogHash { get; set; } = string.Empty;
        public string GenerationTemplateCatalogHash { get; set; } = string.Empty;
        public string StyleProfileId { get; set; } = string.Empty;
        public string QualityProfileId { get; set; } = string.Empty;
        public string QualityProfileVersion { get; set; } = string.Empty;
        public string PreferredBackend { get; set; } = VfxAuthoringBackends.ParticleSystem;
        public string PreferredProviderId { get; set; } = string.Empty;
        public string TargetCostTier { get; set; } = VfxAuthoringCostTiers.Medium;
        public List<string> Roles { get; set; } = new List<string>();
        public List<string> ThemeIds { get; set; } = new List<string>();
        public VfxAuthoringAnchorContract AnchorContract { get; set; } = new VfxAuthoringAnchorContract();
        public VfxAuthoringLifecycle Lifecycle { get; set; } = new VfxAuthoringLifecycle();
        public List<string> RequiredParameterIds { get; set; } = new List<string>();
        public List<string> ReferenceEffectIds { get; set; } = new List<string>();
        public string Description { get; set; } = string.Empty;
        public int CandidateCount { get; set; } = 6;
        public int Round { get; set; }
        public List<string> ParentCandidateIds { get; set; } = new List<string>();
        public List<int> FixedSeeds { get; set; } = new List<int> { 17, 73, 211 };
        public bool AllowExperimentalGraphTopology { get; set; }
    }

    public sealed class VfxReuseCandidate
    {
        public string StableId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public int CompatibilityScore { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
    }

    /// <summary>Deterministic reuse-first decision produced before any candidate generation.</summary>
    /// <remarks>AllocByDesign. Owns stable candidate and rationale lists.</remarks>
    public sealed class VfxReuseDecision
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.ReuseDecisionV1;
        public string BriefId { get; set; } = string.Empty;
        public string CatalogHash { get; set; } = string.Empty;
        public string DecisionKind { get; set; } = VfxReuseDecisionKinds.GeneratePrimitive;
        public string SelectedEffectId { get; set; } = string.Empty;
        public List<string> SelectedRecipeIds { get; set; } = new List<string>();
        public List<string> SelectedPrimitiveIds { get; set; } = new List<string>();
        public List<string> PrimitiveBriefs { get; set; } = new List<string>();
        public List<VfxReuseCandidate> Candidates { get; set; } = new List<VfxReuseCandidate>();
        public string DecisionHash { get; set; } = string.Empty;
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();

        public bool Blocked => VfxCreativeFlywheelUtilities.HasErrors(Diagnostics);
    }

    public sealed class VfxGenerationCandidateSpec
    {
        public string CandidateId { get; set; } = string.Empty;
        public string VariantKind { get; set; } = string.Empty;
        public int Seed { get; set; }
        public string ProviderId { get; set; } = string.Empty;
        public string ProviderVersion { get; set; } = string.Empty;
        public string ProviderGrantId { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public string TemplateId { get; set; } = string.Empty;
        public string TemplateVersion { get; set; } = string.Empty;
        public string CostTier { get; set; } = VfxAuthoringCostTiers.Medium;
        public string OutputRoot { get; set; } = string.Empty;
        public bool DraftOnly { get; set; } = true;
    }

    /// <summary>Read-only reuse and generation decisions emitted by vfx plan-create.</summary>
    /// <remarks>AllocByDesign.</remarks>
    public sealed class VfxCreativePlanBundle
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.PlanBundleV1;
        public VfxReuseDecision ReuseDecision { get; set; } = new VfxReuseDecision();
        public VfxGenerationPlan GenerationPlan { get; set; } = new VfxGenerationPlan();
    }

    /// <summary>Read-only plan for one candidate generation session.</summary>
    /// <remarks>AllocByDesign. Planning may allocate DTOs, hashes, and diagnostics; it never writes Unity assets.</remarks>
    public sealed class VfxGenerationPlan
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.GenerationPlanV1;
        public string PlanId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string BriefHash { get; set; } = string.Empty;
        public string ReuseDecisionHash { get; set; } = string.Empty;
        public string CatalogHash { get; set; } = string.Empty;
        public string GenerationTemplateCatalogHash { get; set; } = string.Empty;
        public string SessionCapabilitiesHash { get; set; } = string.Empty;
        public string GenerationMode { get; set; } = VfxGenerationModes.ControlledTemplate;
        public int Round { get; set; }
        public string QualityProfileId { get; set; } = string.Empty;
        public string QualityProfileVersion { get; set; } = string.Empty;
        public string TargetCostTier { get; set; } = VfxAuthoringCostTiers.Medium;
        public string SandboxRoot { get; set; } = string.Empty;
        public List<int> FixedSeeds { get; set; } = new List<int>();
        public List<VfxGenerationCandidateSpec> Candidates { get; set; } = new List<VfxGenerationCandidateSpec>();
        public string PlanHash { get; set; } = string.Empty;
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();

        public bool Blocked => VfxCreativeFlywheelUtilities.HasErrors(Diagnostics);
    }

    public sealed class VfxAssetProvenance
    {
        public string ProviderId { get; set; } = string.Empty;
        public string ProviderVersion { get; set; } = string.Empty;
        public string TemplateId { get; set; } = string.Empty;
        public string TemplateVersion { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public string LicenseId { get; set; } = string.Empty;
        public string SourceUri { get; set; } = string.Empty;
        public string SourceContentHash { get; set; } = string.Empty;
    }

    public sealed class VfxCandidateAssetRecord
    {
        public string CandidateId { get; set; } = string.Empty;
        public string AssetKind { get; set; } = string.Empty;
        public string AssetPath { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string UnityGuid { get; set; } = string.Empty;
        public string DependencyHash { get; set; } = string.Empty;
        public VfxAssetProvenance Provenance { get; set; } = new VfxAssetProvenance();
    }

    /// <summary>Candidate inventory produced inside one Unity-importable sandbox.</summary>
    /// <remarks>LowFreqAlloc. Candidate data is disposable evidence and never grants Ready status.</remarks>
    public sealed class VfxCandidateManifest
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.CandidateManifestV1;
        public string SessionId { get; set; } = string.Empty;
        public string GenerationPlanHash { get; set; } = string.Empty;
        public string CatalogHash { get; set; } = string.Empty;
        public string GenerationTemplateCatalogHash { get; set; } = string.Empty;
        public string ReviewStatus { get; set; } = VfxAuthoringReviewStatuses.Draft;
        public List<VfxCandidateAssetRecord> Assets { get; set; } = new List<VfxCandidateAssetRecord>();
        public string ManifestHash { get; set; } = string.Empty;
    }

    /// <summary>Signed-by-content evidence returned by the formal Unity candidate executor.</summary>
    /// <remarks>LowFreqAlloc. Receipt validation may read and hash generated files.</remarks>
    public sealed class VfxCandidateGenerationReceipt
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.CandidateReceiptV1;
        public string SessionId { get; set; } = string.Empty;
        public string GenerationPlanHash { get; set; } = string.Empty;
        public string CatalogHash { get; set; } = string.Empty;
        public string GenerationTemplateCatalogHash { get; set; } = string.Empty;
        public string ExecutorId { get; set; } = string.Empty;
        public string ExecutorVersion { get; set; } = string.Empty;
        public List<VfxCandidateAssetRecord> Assets { get; set; } = new List<VfxCandidateAssetRecord>();
        public string ReceiptHash { get; set; } = string.Empty;
    }

    public sealed class VfxHardGateResult
    {
        public string GateId { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public string EvidenceHash { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class VfxReviewerScore
    {
        public string ReviewerId { get; set; } = string.Empty;
        public int BriefMatch { get; set; }
        public int Readability { get; set; }
        public int Timing { get; set; }
        public int SpatialMotion { get; set; }
        public int MaterialAndColor { get; set; }
        public int Polish { get; set; }
    }

    public sealed class VfxCandidateEvaluationInput
    {
        public string CandidateId { get; set; } = string.Empty;
        public float CpuP95Milliseconds { get; set; }
        public float GpuP95Milliseconds { get; set; }
        public List<VfxHardGateResult> HardGates { get; set; } = new List<VfxHardGateResult>();
        public List<VfxReviewerScore> ReviewerScores { get; set; } = new List<VfxReviewerScore>();
    }

    public sealed class VfxCandidateEvaluationInputDocument
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.EvaluationInputV1;
        public string GenerationPlanHash { get; set; } = string.Empty;
        public string CandidateReceiptHash { get; set; } = string.Empty;
        public string QualityProfileId { get; set; } = string.Empty;
        public string QualityProfileVersion { get; set; } = string.Empty;
        public int PreviousMedianScore { get; set; }
        public int ConsecutiveLowImprovementRounds { get; set; }
        public List<VfxCandidateEvaluationInput> Candidates { get; set; } = new List<VfxCandidateEvaluationInput>();
    }

    public sealed class VfxVisualScoreSummary
    {
        public int BriefMatch { get; set; }
        public int Readability { get; set; }
        public int Timing { get; set; }
        public int SpatialMotion { get; set; }
        public int MaterialAndColor { get; set; }
        public int Polish { get; set; }
        public int Total { get; set; }
        public int ReviewerSpread { get; set; }
    }

    public sealed class VfxCandidateEvaluationResult
    {
        public string CandidateId { get; set; } = string.Empty;
        public string CostTier { get; set; } = string.Empty;
        public bool HardGatesPassed { get; set; }
        public bool Shortlisted { get; set; }
        public float CpuP95Milliseconds { get; set; }
        public float GpuP95Milliseconds { get; set; }
        public VfxVisualScoreSummary Score { get; set; } = new VfxVisualScoreSummary();
        public List<VfxHardGateResult> HardGates { get; set; } = new List<VfxHardGateResult>();
        public List<string> Reasons { get; set; } = new List<string>();
    }

    /// <summary>Deterministic advisory evaluation report; it cannot approve Ready.</summary>
    /// <remarks>AllocByDesign. Aggregates fixed-profile gates and human/model reviewer medians.</remarks>
    public sealed class VfxEvaluationReport
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.EvaluationReportV1;
        public string GenerationPlanHash { get; set; } = string.Empty;
        public string CandidateReceiptHash { get; set; } = string.Empty;
        public string QualityProfileId { get; set; } = string.Empty;
        public string QualityProfileVersion { get; set; } = string.Empty;
        public int Round { get; set; }
        public List<VfxCandidateEvaluationResult> Candidates { get; set; } = new List<VfxCandidateEvaluationResult>();
        public List<string> ParetoFrontierCandidateIds { get; set; } = new List<string>();
        public int MedianShortlistScore { get; set; }
        public int ImprovementFromPreviousRound { get; set; }
        public int ConsecutiveLowImprovementRounds { get; set; }
        public bool StopEvolution { get; set; }
        public List<string> PrimitiveBriefs { get; set; } = new List<string>();
        public string ReportHash { get; set; } = string.Empty;
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();

        public bool Blocked => VfxCreativeFlywheelUtilities.HasErrors(Diagnostics);
    }

    public sealed class VfxPromotionRequest
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.PromotionRequestV1;
        public string CandidateId { get; set; } = string.Empty;
        public string TargetKind { get; set; } = VfxPromotionTargetKinds.Primitive;
        public string TargetStableId { get; set; } = string.Empty;
        public string DestinationRoot { get; set; } = string.Empty;
        public string RequestedReviewStatus { get; set; } = VfxAuthoringReviewStatuses.ReviewNeeded;
        public string SelectedBy { get; set; } = string.Empty;
        public string SelectionEvidenceHash { get; set; } = string.Empty;
        public string SelectionAuthorityId { get; set; } = string.Empty;
        public string SelectionAuthorityVersion { get; set; } = string.Empty;
        public string SelectionAuthorityReceiptHash { get; set; } = string.Empty;
        public bool StrategicFoundationExceptionApproved { get; set; }
        public List<string> ConsumerRecipeIds { get; set; } = new List<string>();
    }

    public sealed class VfxPromotionMove
    {
        public string SourceAssetPath { get; set; } = string.Empty;
        public string DestinationAssetPath { get; set; } = string.Empty;
        public string ExpectedContentHash { get; set; } = string.Empty;
        public string ExpectedUnityGuid { get; set; } = string.Empty;
        public string ExpectedDependencyHash { get; set; } = string.Empty;
    }

    /// <summary>Transactional move plan consumed only by a formal Unity Editor promotion executor.</summary>
    /// <remarks>AllocByDesign. The Core planner performs no asset moves or writes.</remarks>
    public sealed class VfxPromotionPlan
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.PromotionPlanV1;
        public string CandidateId { get; set; } = string.Empty;
        public string TargetKind { get; set; } = string.Empty;
        public string TargetStableId { get; set; } = string.Empty;
        public string ExpectedCatalogHash { get; set; } = string.Empty;
        public string GenerationPlanHash { get; set; } = string.Empty;
        public string CandidateReceiptHash { get; set; } = string.Empty;
        public string EvaluationReportHash { get; set; } = string.Empty;
        public string SelectedBy { get; set; } = string.Empty;
        public string SelectionEvidenceHash { get; set; } = string.Empty;
        public string SelectionAuthorityId { get; set; } = string.Empty;
        public string SelectionAuthorityVersion { get; set; } = string.Empty;
        public string SelectionAuthorityReceiptHash { get; set; } = string.Empty;
        public bool StrategicFoundationExceptionApproved { get; set; }
        public List<string> ConsumerRecipeIds { get; set; } = new List<string>();
        public string ResultReviewStatus { get; set; } = VfxAuthoringReviewStatuses.ReviewNeeded;
        public List<VfxPromotionMove> Moves { get; set; } = new List<VfxPromotionMove>();
        public bool RequiresUnityEditor { get; set; } = true;
        public bool RequiresHumanConfirmation { get; set; } = true;
        public string PlanHash { get; set; } = string.Empty;
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();

        public bool Blocked => VfxCreativeFlywheelUtilities.HasErrors(Diagnostics);
    }

    public sealed class VfxPromotedAssetRecord
    {
        public string SourceAssetPath { get; set; } = string.Empty;
        public string DestinationAssetPath { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string UnityGuid { get; set; } = string.Empty;
        public string DependencyHash { get; set; } = string.Empty;
    }

    public sealed class VfxPromotionReceipt
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.PromotionReceiptV1;
        public string PromotionPlanHash { get; set; } = string.Empty;
        public string CatalogHash { get; set; } = string.Empty;
        public string ExecutorId { get; set; } = string.Empty;
        public string ExecutorVersion { get; set; } = string.Empty;
        public string ResultReviewStatus { get; set; } = VfxAuthoringReviewStatuses.ReviewNeeded;
        public List<VfxPromotedAssetRecord> Assets { get; set; } = new List<VfxPromotedAssetRecord>();
        public string ReceiptHash { get; set; } = string.Empty;
    }

    public sealed class VfxPublishedRegistrationRequest
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.RegistrationRequestV1;
        public string ExpectedCatalogHash { get; set; } = string.Empty;
        public string ExpectedResourceCatalogHash { get; set; } = string.Empty;
        public string ExpectedDefinitionCatalogHash { get; set; } = string.Empty;
        public string EffectId { get; set; } = string.Empty;
        public VfxAuthoringResourceKey ResourceKey { get; set; } = new VfxAuthoringResourceKey();
        public int DefinitionId { get; set; }
        public string PublishedPrefabPath { get; set; } = string.Empty;
        public string FallbackEffectId { get; set; } = string.Empty;
        public string PromotionReceiptHash { get; set; } = string.Empty;
    }

    /// <summary>Separate guarded plan for semantic Catalog, Resource Catalog, and VfxDefinition registration.</summary>
    /// <remarks>AllocByDesign. This plan reports intended writes but does not mutate any catalog.</remarks>
    public sealed class VfxPublishedRegistrationPlan
    {
        public string SchemaVersion { get; set; } = VfxCreativeFlywheelSchemaVersions.RegistrationPlanV1;
        public string EffectId { get; set; } = string.Empty;
        public string ExpectedCatalogHash { get; set; } = string.Empty;
        public string ExpectedResourceCatalogHash { get; set; } = string.Empty;
        public string ExpectedDefinitionCatalogHash { get; set; } = string.Empty;
        public bool WouldModifySemanticCatalog { get; set; }
        public bool WouldModifyResourceCatalog { get; set; }
        public bool WouldModifyDefinitionCatalog { get; set; }
        public bool RequiresSeparateGuardedApply { get; set; } = true;
        public bool WouldModifyRuntimeAuthority { get; set; }
        public string PlanHash { get; set; } = string.Empty;
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();

        public bool Blocked => VfxCreativeFlywheelUtilities.HasErrors(Diagnostics);
    }

    /// <summary>Result of validating an immutable candidate or promotion receipt.</summary>
    /// <remarks>LowFreqAlloc. Contains stable diagnostics only.</remarks>
    public sealed class VfxReceiptValidationReport
    {
        public bool Valid { get; set; }
        public string ExpectedHash { get; set; } = string.Empty;
        public string ActualHash { get; set; } = string.Empty;
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
    }

    public static class VfxCreativeFlywheelUtilities
    {
        /// <summary>Returns true when an authoring diagnostic list contains at least one error.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static bool HasErrors(List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (diagnostics == null)
                return false;
            for (int i = 0; i < diagnostics.Count; i++)
                if (diagnostics[i] != null && diagnostics[i].Severity == CharacterAuthoringValidationSeverity.Error)
                    return true;
            return false;
        }
    }
}
