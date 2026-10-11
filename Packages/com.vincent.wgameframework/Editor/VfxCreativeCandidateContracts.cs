using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace MxFramework.Editor
{
    internal static class VfxCandidateProtocol
    {
        public const string PlanSchemaVersion = "mx.vfx.candidate.plan.v1";
        public const string EditorReceiptSchemaVersion = "mx.vfx.editor-candidate-receipt.v1";
        public const string CoreGenerationPlanSchemaVersion = "mx.vfx.generation-plan.v1";
        public const string CoreCandidateManifestSchemaVersion = "mx.vfx.candidate-manifest.v1";
        public const string CoreCandidateReceiptSchemaVersion = "mx.vfx.candidate-receipt.v1";
        public const string CoreEvaluationReportSchemaVersion = "mx.vfx.evaluation-report.v1";
        public const string CorePromotionPlanSchemaVersion = "mx.vfx.promotion-plan.v1";
        public const string CorePromotionReceiptSchemaVersion = "mx.vfx.promotion-receipt.v1";
        public const string SelectionEvidenceSchemaVersion = "mx.vfx.human-selection-evidence.v1";
        public const string PromotionSchemaVersion = "mx.vfx.unity-promotion-transaction.v2";
        public const string LogicalSandboxRoot = "Assets/VFX/.mx-candidates";
        public const string AuthoritativeEvidenceRoot = "Temp/MxFramework/VfxAuthoring";
        public const string AuthoringCatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        public const string GenerationTemplateCatalogPath = "Assets/Config/MxFramework/Vfx/vfx_generation_templates.json";

        // Unity ignores Assets folders whose names begin with '.', including their .meta files.
        // The protocol retains .mx-candidates as the logical label while all imported assets live here.
        public const string ResolvedSandboxRoot = "Assets/VFX/MxCandidates";
    }

    [JsonConverter(typeof(StringEnumConverter))]
    internal enum VfxCandidateKind
    {
        ParticleSystemPrimitive,
        LineRendererPrimitive,
        GraphTemplateClone
    }

    [JsonConverter(typeof(StringEnumConverter))]
    internal enum VfxProceduralTextureKind
    {
        SoftCircle,
        Ring,
        Noise,
        Sdf,
        Flow,
        RadialMask,
        Flipbook
    }

    internal sealed class VfxColorSpec
    {
        [JsonProperty("r")] public float R { get; set; } = 1f;
        [JsonProperty("g")] public float G { get; set; } = 1f;
        [JsonProperty("b")] public float B { get; set; } = 1f;
        [JsonProperty("a")] public float A { get; set; } = 1f;
    }

    internal sealed class VfxVector3Spec
    {
        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }
        [JsonProperty("z")] public float Z { get; set; }
    }

    internal sealed class VfxBurstSpec
    {
        [JsonProperty("time")] public float Time { get; set; }
        [JsonProperty("count")] public short Count { get; set; } = 1;
        [JsonProperty("cycles")] public int Cycles { get; set; } = 1;
        [JsonProperty("interval")] public float Interval { get; set; } = 0.01f;
    }

    internal sealed class ParticleSystemPrimitiveSpec
    {
        [JsonProperty("durationSeconds")] public float DurationSeconds { get; set; } = 1f;
        [JsonProperty("looping")] public bool Looping { get; set; }
        [JsonProperty("startLifetimeSeconds")] public float StartLifetimeSeconds { get; set; } = 0.8f;
        [JsonProperty("startSpeed")] public float StartSpeed { get; set; } = 2f;
        [JsonProperty("startSize")] public float StartSize { get; set; } = 0.25f;
        [JsonProperty("startColor")] public VfxColorSpec StartColor { get; set; } = new VfxColorSpec();
        [JsonProperty("endColor")] public VfxColorSpec EndColor { get; set; } = new VfxColorSpec { A = 0f };
        [JsonProperty("maxParticles")] public int MaxParticles { get; set; } = 128;
        [JsonProperty("emissionRatePerSecond")] public float EmissionRatePerSecond { get; set; }
        [JsonProperty("bursts")] public List<VfxBurstSpec> Bursts { get; set; } = new List<VfxBurstSpec>();
        [JsonProperty("shape")] public string Shape { get; set; } = "Sphere";
        [JsonProperty("shapeRadius")] public float ShapeRadius { get; set; } = 0.2f;
        [JsonProperty("shapeAngleDegrees")] public float ShapeAngleDegrees { get; set; } = 25f;
        [JsonProperty("gravityModifier")] public float GravityModifier { get; set; }
        [JsonProperty("noiseStrength")] public float NoiseStrength { get; set; }
        [JsonProperty("noiseFrequency")] public float NoiseFrequency { get; set; } = 0.5f;
        [JsonProperty("materialId")] public string MaterialId { get; set; } = string.Empty;
    }

    internal sealed class LineRendererPrimitiveSpec
    {
        [JsonProperty("positions")] public List<VfxVector3Spec> Positions { get; set; } = new List<VfxVector3Spec>();
        [JsonProperty("startWidth")] public float StartWidth { get; set; } = 0.08f;
        [JsonProperty("endWidth")] public float EndWidth { get; set; } = 0.01f;
        [JsonProperty("startColor")] public VfxColorSpec StartColor { get; set; } = new VfxColorSpec();
        [JsonProperty("endColor")] public VfxColorSpec EndColor { get; set; } = new VfxColorSpec { A = 0f };
        [JsonProperty("loop")] public bool Loop { get; set; }
        [JsonProperty("cornerVertices")] public int CornerVertices { get; set; } = 2;
        [JsonProperty("capVertices")] public int CapVertices { get; set; } = 2;
        [JsonProperty("materialId")] public string MaterialId { get; set; } = string.Empty;
    }

    internal sealed class VfxProceduralTextureSpec
    {
        [JsonProperty("textureId")] public string TextureId { get; set; } = string.Empty;
        [JsonProperty("assetName")] public string AssetName { get; set; } = string.Empty;
        [JsonProperty("kind")] public VfxProceduralTextureKind Kind { get; set; } = VfxProceduralTextureKind.SoftCircle;
        [JsonProperty("width")] public int Width { get; set; } = 64;
        [JsonProperty("height")] public int Height { get; set; } = 64;
        [JsonProperty("color")] public VfxColorSpec Color { get; set; } = new VfxColorSpec();
        [JsonProperty("linear")] public bool Linear { get; set; }
        [JsonProperty("frameColumns")] public int FrameColumns { get; set; } = 1;
        [JsonProperty("frameRows")] public int FrameRows { get; set; } = 1;
        [JsonProperty("frameCount")] public int FrameCount { get; set; } = 1;
    }

    internal sealed class VfxMaterialSpec
    {
        [JsonProperty("materialId")] public string MaterialId { get; set; } = string.Empty;
        [JsonProperty("assetName")] public string AssetName { get; set; } = string.Empty;
        [JsonProperty("shaderName")] public string ShaderName { get; set; } = "Universal Render Pipeline/Particles/Unlit";
        [JsonProperty("textureId")] public string TextureId { get; set; } = string.Empty;
        [JsonProperty("color")] public VfxColorSpec Color { get; set; } = new VfxColorSpec();
    }

    internal sealed class VfxGraphParameterMetadata
    {
        [JsonProperty("name")] public string Name { get; set; } = string.Empty;
        [JsonProperty("valueType")] public string ValueType { get; set; } = string.Empty;
        [JsonProperty("transport")] public string Transport { get; set; } = "PreviewOnly";
        [JsonProperty("defaultValue")] public JToken DefaultValue { get; set; }
    }

    internal sealed class VfxTopologyAdapterPolicy
    {
        [JsonProperty("adapterId")] public string AdapterId { get; set; } = "unity-vfxgraph-template-clone";
        [JsonProperty("mode")] public string Mode { get; set; } = "TemplateCloneOnly";
        [JsonProperty("versionLocked")] public bool VersionLocked { get; set; } = true;
        [JsonProperty("lockedPackageVersion")] public string LockedPackageVersion { get; set; } = "17.5.0";
        [JsonProperty("draftOnly")] public bool DraftOnly { get; set; } = true;
    }

    internal sealed class VfxGraphTemplateCloneSpec
    {
        [JsonProperty("templateId")] public string TemplateId { get; set; } = string.Empty;
        [JsonProperty("parameters")] public List<VfxGraphParameterMetadata> Parameters { get; set; } = new List<VfxGraphParameterMetadata>();
        [JsonProperty("topologyAdapter")] public VfxTopologyAdapterPolicy TopologyAdapter { get; set; } = new VfxTopologyAdapterPolicy();
    }

    internal sealed class VfxCandidateProvenanceEntry
    {
        [JsonProperty("key")] public string Key { get; set; } = string.Empty;
        [JsonProperty("value")] public string Value { get; set; } = string.Empty;
    }

    internal sealed class VfxCandidatePlan
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion { get; set; } = VfxCandidateProtocol.PlanSchemaVersion;
        [JsonProperty("planId")] public string PlanId { get; set; } = string.Empty;
        [JsonProperty("sessionId")] public string SessionId { get; set; } = string.Empty;
        [JsonProperty("generatorId")] public string GeneratorId { get; set; } = "mx.vfx.candidate.editor";
        [JsonProperty("generatorVersion")] public string GeneratorVersion { get; set; } = "1.0.0";
        [JsonProperty("candidateKind")] public VfxCandidateKind CandidateKind { get; set; }
        [JsonProperty("coreCandidateId")] public string CoreCandidateId { get; set; } = string.Empty;
        [JsonProperty("candidateOutputRoot")] public string CandidateOutputRoot { get; set; } = string.Empty;
        [JsonProperty("displayName")] public string DisplayName { get; set; } = string.Empty;
        [JsonProperty("outputAssetName")] public string OutputAssetName { get; set; } = string.Empty;
        [JsonProperty("fixedSeed")] public uint FixedSeed { get; set; } = 1u;
        [JsonProperty("particleSystem")] public ParticleSystemPrimitiveSpec ParticleSystem { get; set; }
        [JsonProperty("lineRenderer")] public LineRendererPrimitiveSpec LineRenderer { get; set; }
        [JsonProperty("graphTemplate")] public VfxGraphTemplateCloneSpec GraphTemplate { get; set; }
        [JsonProperty("textures")] public List<VfxProceduralTextureSpec> Textures { get; set; } = new List<VfxProceduralTextureSpec>();
        [JsonProperty("materials")] public List<VfxMaterialSpec> Materials { get; set; } = new List<VfxMaterialSpec>();
        [JsonProperty("provenance")] public List<VfxCandidateProvenanceEntry> Provenance { get; set; } = new List<VfxCandidateProvenanceEntry>();
    }

    internal sealed class VfxCandidateAssetReceipt
    {
        [JsonProperty("kind")] public string Kind { get; set; } = string.Empty;
        [JsonProperty("assetPath")] public string AssetPath { get; set; } = string.Empty;
        [JsonProperty("relativePath")] public string RelativePath { get; set; } = string.Empty;
        [JsonProperty("guid")] public string Guid { get; set; } = string.Empty;
        [JsonProperty("contentHash")] public string ContentHash { get; set; } = string.Empty;
        [JsonProperty("dependencyHash")] public string DependencyHash { get; set; } = string.Empty;
    }

    internal sealed class VfxCandidateReceipt
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion { get; set; } = VfxCandidateProtocol.EditorReceiptSchemaVersion;
        [JsonProperty("planId")] public string PlanId { get; set; } = string.Empty;
        [JsonProperty("planHash")] public string PlanHash { get; set; } = string.Empty;
        [JsonProperty("sessionId")] public string SessionId { get; set; } = string.Empty;
        [JsonProperty("generatorId")] public string GeneratorId { get; set; } = string.Empty;
        [JsonProperty("generatorVersion")] public string GeneratorVersion { get; set; } = string.Empty;
        [JsonProperty("candidateKind")] public VfxCandidateKind CandidateKind { get; set; }
        [JsonProperty("coreCandidateId")] public string CoreCandidateId { get; set; } = string.Empty;
        [JsonProperty("reviewStatus")] public string ReviewStatus { get; set; } = "Draft";
        [JsonProperty("fixedSeed")] public uint FixedSeed { get; set; }
        [JsonProperty("requestedSandboxRoot")] public string RequestedSandboxRoot { get; set; } = VfxCandidateProtocol.LogicalSandboxRoot;
        [JsonProperty("resolvedAssetRoot")] public string ResolvedAssetRoot { get; set; } = string.Empty;
        [JsonProperty("topologyPolicy")] public string TopologyPolicy { get; set; } = "NoGraphTopologyMutation";
        [JsonProperty("assets")] public List<VfxCandidateAssetReceipt> Assets { get; set; } = new List<VfxCandidateAssetReceipt>();
        [JsonProperty("graphParameters")] public List<VfxGraphParameterMetadata> GraphParameters { get; set; } = new List<VfxGraphParameterMetadata>();
        [JsonProperty("provenance")] public List<VfxCandidateProvenanceEntry> Provenance { get; set; } = new List<VfxCandidateProvenanceEntry>();
    }

    internal sealed class VfxCandidateGenerationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string SessionRoot { get; set; } = string.Empty;
        public string ReceiptAssetPath { get; set; } = string.Empty;
        public VfxCandidateReceipt Receipt { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCoreGenerationCandidateSpec
    {
        [JsonProperty("candidateId", Order = 1)] public string CandidateId { get; set; } = string.Empty;
        [JsonProperty("variantKind", Order = 2)] public string VariantKind { get; set; } = string.Empty;
        [JsonProperty("seed", Order = 3)] public int Seed { get; set; }
        [JsonProperty("providerId", Order = 4)] public string ProviderId { get; set; } = string.Empty;
        [JsonProperty("providerVersion", Order = 5)] public string ProviderVersion { get; set; } = string.Empty;
        [JsonProperty("providerGrantId", Order = 6)] public string ProviderGrantId { get; set; } = string.Empty;
        [JsonProperty("modelId", Order = 7)] public string ModelId { get; set; } = string.Empty;
        [JsonProperty("modelVersion", Order = 8)] public string ModelVersion { get; set; } = string.Empty;
        [JsonProperty("templateId", Order = 9)] public string TemplateId { get; set; } = string.Empty;
        [JsonProperty("templateVersion", Order = 10)] public string TemplateVersion { get; set; } = string.Empty;
        [JsonProperty("costTier", Order = 11)] public string CostTier { get; set; } = "Medium";
        [JsonProperty("outputRoot", Order = 12)] public string OutputRoot { get; set; } = string.Empty;
        [JsonProperty("draftOnly", Order = 13)] public bool DraftOnly { get; set; } = true;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCoreGenerationPlan
    {
        [JsonProperty("schemaVersion", Order = 1)] public string SchemaVersion { get; set; } = VfxCandidateProtocol.CoreGenerationPlanSchemaVersion;
        [JsonProperty("planId", Order = 2)] public string PlanId { get; set; } = string.Empty;
        [JsonProperty("sessionId", Order = 3)] public string SessionId { get; set; } = string.Empty;
        [JsonProperty("briefHash", Order = 4)] public string BriefHash { get; set; } = string.Empty;
        [JsonProperty("reuseDecisionHash", Order = 5)] public string ReuseDecisionHash { get; set; } = string.Empty;
        [JsonProperty("catalogHash", Order = 6)] public string CatalogHash { get; set; } = string.Empty;
        [JsonProperty("generationTemplateCatalogHash", Order = 7)] public string GenerationTemplateCatalogHash { get; set; } = string.Empty;
        [JsonProperty("sessionCapabilitiesHash", Order = 8)] public string SessionCapabilitiesHash { get; set; } = string.Empty;
        [JsonProperty("generationMode", Order = 9)] public string GenerationMode { get; set; } = "ControlledTemplate";
        [JsonProperty("round", Order = 10)] public int Round { get; set; }
        [JsonProperty("qualityProfileId", Order = 11)] public string QualityProfileId { get; set; } = string.Empty;
        [JsonProperty("qualityProfileVersion", Order = 12)] public string QualityProfileVersion { get; set; } = string.Empty;
        [JsonProperty("targetCostTier", Order = 13)] public string TargetCostTier { get; set; } = "Medium";
        [JsonProperty("sandboxRoot", Order = 14)] public string SandboxRoot { get; set; } = string.Empty;
        [JsonProperty("fixedSeeds", Order = 15)] public List<int> FixedSeeds { get; set; } = new List<int>();
        [JsonProperty("candidates", Order = 16)] public List<VfxCoreGenerationCandidateSpec> Candidates { get; set; } = new List<VfxCoreGenerationCandidateSpec>();
        [JsonProperty("planHash", Order = 17)] public string PlanHash { get; set; } = string.Empty;
        [JsonProperty("diagnostics", Order = 18)] public JArray Diagnostics { get; set; } = new JArray();
        [JsonProperty("blocked", Order = 19)] public bool Blocked { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCoreAssetProvenance
    {
        [JsonProperty("providerId", Order = 1)] public string ProviderId { get; set; } = string.Empty;
        [JsonProperty("providerVersion", Order = 2)] public string ProviderVersion { get; set; } = string.Empty;
        [JsonProperty("templateId", Order = 3)] public string TemplateId { get; set; } = string.Empty;
        [JsonProperty("templateVersion", Order = 4)] public string TemplateVersion { get; set; } = string.Empty;
        [JsonProperty("modelId", Order = 5)] public string ModelId { get; set; } = string.Empty;
        [JsonProperty("modelVersion", Order = 6)] public string ModelVersion { get; set; } = string.Empty;
        [JsonProperty("licenseId", Order = 7)] public string LicenseId { get; set; } = string.Empty;
        [JsonProperty("sourceUri", Order = 8)] public string SourceUri { get; set; } = string.Empty;
        [JsonProperty("sourceContentHash", Order = 9)] public string SourceContentHash { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCoreCandidateAssetRecord
    {
        [JsonProperty("candidateId", Order = 1)] public string CandidateId { get; set; } = string.Empty;
        [JsonProperty("assetKind", Order = 2)] public string AssetKind { get; set; } = string.Empty;
        [JsonProperty("assetPath", Order = 3)] public string AssetPath { get; set; } = string.Empty;
        [JsonProperty("contentHash", Order = 4)] public string ContentHash { get; set; } = string.Empty;
        [JsonProperty("unityGuid", Order = 5)] public string UnityGuid { get; set; } = string.Empty;
        [JsonProperty("dependencyHash", Order = 6)] public string DependencyHash { get; set; } = string.Empty;
        [JsonProperty("provenance", Order = 7)] public VfxCoreAssetProvenance Provenance { get; set; } = new VfxCoreAssetProvenance();
    }

    /// <summary>Exact Editor transport mirror of Core mx.vfx.candidate-receipt.v1.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCoreCandidateReceipt
    {
        [JsonProperty("schemaVersion", Order = 1)] public string SchemaVersion { get; set; } = VfxCandidateProtocol.CoreCandidateReceiptSchemaVersion;
        [JsonProperty("sessionId", Order = 2)] public string SessionId { get; set; } = string.Empty;
        [JsonProperty("generationPlanHash", Order = 3)] public string GenerationPlanHash { get; set; } = string.Empty;
        [JsonProperty("catalogHash", Order = 4)] public string CatalogHash { get; set; } = string.Empty;
        [JsonProperty("generationTemplateCatalogHash", Order = 5)] public string GenerationTemplateCatalogHash { get; set; } = string.Empty;
        [JsonProperty("executorId", Order = 6)] public string ExecutorId { get; set; } = "mx.vfx.unity-candidate-executor";
        [JsonProperty("executorVersion", Order = 7)] public string ExecutorVersion { get; set; } = "1.0.0";
        [JsonProperty("assets", Order = 8)] public List<VfxCoreCandidateAssetRecord> Assets { get; set; } = new List<VfxCoreCandidateAssetRecord>();
        [JsonProperty("receiptHash", Order = 9)] public string ReceiptHash { get; set; } = string.Empty;
    }

    /// <summary>Exact Editor transport mirror of Core mx.vfx.candidate-manifest.v1.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCoreCandidateManifest
    {
        [JsonProperty("schemaVersion", Order = 1)] public string SchemaVersion { get; set; } = VfxCandidateProtocol.CoreCandidateManifestSchemaVersion;
        [JsonProperty("sessionId", Order = 2)] public string SessionId { get; set; } = string.Empty;
        [JsonProperty("generationPlanHash", Order = 3)] public string GenerationPlanHash { get; set; } = string.Empty;
        [JsonProperty("catalogHash", Order = 4)] public string CatalogHash { get; set; } = string.Empty;
        [JsonProperty("generationTemplateCatalogHash", Order = 5)] public string GenerationTemplateCatalogHash { get; set; } = string.Empty;
        [JsonProperty("reviewStatus", Order = 6)] public string ReviewStatus { get; set; } = "Draft";
        [JsonProperty("assets", Order = 7)] public List<VfxCoreCandidateAssetRecord> Assets { get; set; } = new List<VfxCoreCandidateAssetRecord>();
        [JsonProperty("manifestHash", Order = 8)] public string ManifestHash { get; set; } = string.Empty;
    }

    internal sealed class VfxCoreReceiptExportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ManifestAssetPath { get; set; } = string.Empty;
        public string ReceiptAssetPath { get; set; } = string.Empty;
        public VfxCoreCandidateManifest Manifest { get; set; }
        public VfxCoreCandidateReceipt Receipt { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCoreEvaluationCandidate
    {
        [JsonProperty("candidateId")] public string CandidateId { get; set; } = string.Empty;
        [JsonProperty("hardGatesPassed")] public bool HardGatesPassed { get; set; }
        [JsonProperty("shortlisted")] public bool Shortlisted { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCoreEvaluationReport
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion { get; set; } = VfxCandidateProtocol.CoreEvaluationReportSchemaVersion;
        [JsonProperty("generationPlanHash")] public string GenerationPlanHash { get; set; } = string.Empty;
        [JsonProperty("candidateReceiptHash")] public string CandidateReceiptHash { get; set; } = string.Empty;
        [JsonProperty("qualityProfileId")] public string QualityProfileId { get; set; } = string.Empty;
        [JsonProperty("qualityProfileVersion")] public string QualityProfileVersion { get; set; } = string.Empty;
        [JsonProperty("candidates")] public List<VfxCoreEvaluationCandidate> Candidates { get; set; } = new List<VfxCoreEvaluationCandidate>();
        [JsonProperty("reportHash")] public string ReportHash { get; set; } = string.Empty;
        [JsonProperty("diagnostics")] public JArray Diagnostics { get; set; } = new JArray();
        [JsonProperty("blocked")] public bool Blocked { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCorePromotionMove
    {
        [JsonProperty("sourceAssetPath", Order = 1)] public string SourceAssetPath { get; set; } = string.Empty;
        [JsonProperty("destinationAssetPath", Order = 2)] public string DestinationAssetPath { get; set; } = string.Empty;
        [JsonProperty("expectedContentHash", Order = 3)] public string ExpectedContentHash { get; set; } = string.Empty;
        [JsonProperty("expectedUnityGuid", Order = 4)] public string ExpectedUnityGuid { get; set; } = string.Empty;
        [JsonProperty("expectedDependencyHash", Order = 5)] public string ExpectedDependencyHash { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCorePromotionPlan
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion { get; set; } = VfxCandidateProtocol.CorePromotionPlanSchemaVersion;
        [JsonProperty("candidateId")] public string CandidateId { get; set; } = string.Empty;
        [JsonProperty("targetKind")] public string TargetKind { get; set; } = string.Empty;
        [JsonProperty("targetStableId")] public string TargetStableId { get; set; } = string.Empty;
        [JsonProperty("expectedCatalogHash")] public string ExpectedCatalogHash { get; set; } = string.Empty;
        [JsonProperty("generationPlanHash")] public string GenerationPlanHash { get; set; } = string.Empty;
        [JsonProperty("candidateReceiptHash")] public string CandidateReceiptHash { get; set; } = string.Empty;
        [JsonProperty("evaluationReportHash")] public string EvaluationReportHash { get; set; } = string.Empty;
        [JsonProperty("selectedBy")] public string SelectedBy { get; set; } = string.Empty;
        [JsonProperty("selectionEvidenceHash")] public string SelectionEvidenceHash { get; set; } = string.Empty;
        [JsonProperty("selectionAuthorityId")] public string SelectionAuthorityId { get; set; } = string.Empty;
        [JsonProperty("selectionAuthorityVersion")] public string SelectionAuthorityVersion { get; set; } = string.Empty;
        [JsonProperty("selectionAuthorityReceiptHash")] public string SelectionAuthorityReceiptHash { get; set; } = string.Empty;
        [JsonProperty("strategicFoundationExceptionApproved")] public bool StrategicFoundationExceptionApproved { get; set; }
        [JsonProperty("consumerRecipeIds")] public List<string> ConsumerRecipeIds { get; set; } = new List<string>();
        [JsonProperty("resultReviewStatus")] public string ResultReviewStatus { get; set; } = "ReviewNeeded";
        [JsonProperty("moves")] public List<VfxCorePromotionMove> Moves { get; set; } = new List<VfxCorePromotionMove>();
        [JsonProperty("requiresUnityEditor")] public bool RequiresUnityEditor { get; set; } = true;
        [JsonProperty("requiresHumanConfirmation")] public bool RequiresHumanConfirmation { get; set; } = true;
        [JsonProperty("planHash")] public string PlanHash { get; set; } = string.Empty;
        [JsonProperty("diagnostics")] public JArray Diagnostics { get; set; } = new JArray();
        [JsonProperty("blocked")] public bool Blocked { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxHumanSelectionEvidence
    {
        [JsonProperty("schemaVersion", Order = 1)] public string SchemaVersion { get; set; } = VfxCandidateProtocol.SelectionEvidenceSchemaVersion;
        [JsonProperty("candidateId", Order = 2)] public string CandidateId { get; set; } = string.Empty;
        [JsonProperty("selectedBy", Order = 3)] public string SelectedBy { get; set; } = string.Empty;
        [JsonProperty("generationPlanHash", Order = 4)] public string GenerationPlanHash { get; set; } = string.Empty;
        [JsonProperty("candidateReceiptHash", Order = 5)] public string CandidateReceiptHash { get; set; } = string.Empty;
        [JsonProperty("evaluationReportHash", Order = 6)] public string EvaluationReportHash { get; set; } = string.Empty;
        [JsonProperty("selectionReason", Order = 7)] public string SelectionReason { get; set; } = string.Empty;
    }

    internal sealed class VfxSupportAssetSet
    {
        public Dictionary<string, string> TexturePaths { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, string> MaterialPaths { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<string> CreatedAssetPaths { get; } = new List<string>();
    }

    internal sealed class VfxPreviewCaptureRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public string CaptureId { get; set; } = string.Empty;
        public string ViewId { get; set; } = "Gameplay";
        public string BackgroundId { get; set; } = "DarkNeutral";
        public string TimeSliceId { get; set; } = "single";
        public string PrefabAssetPath { get; set; } = string.Empty;
        public uint FixedSeed { get; set; } = 1u;
        public float SampleTimeSeconds { get; set; } = 0.5f;
        public int Width { get; set; } = 256;
        public int Height { get; set; } = 256;
    }

    internal sealed class VfxPreviewCaptureResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public string CaptureId { get; set; } = string.Empty;
        public uint FixedSeed { get; set; }
        public string ViewId { get; set; } = string.Empty;
        public string BackgroundId { get; set; } = string.Empty;
        public string TimeSliceId { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public float AlphaCoverage { get; set; }
        public float MeanLuminance { get; set; }
        public float PeakLuminance { get; set; }
    }

    internal sealed class VfxVisualEvaluationResult
    {
        public bool PassedHardGates { get; set; }
        public string ReviewStatus { get; set; } = "ReviewNeeded";
        public int Score { get; set; }
        public List<string> Diagnostics { get; } = new List<string>();
    }

    /// <summary>Evidence-only request for executing an already authorized Core promotion plan.</summary>
    internal sealed class VfxAuthorizedPromotionRequest
    {
        public string GenerationPlanPath { get; set; } = string.Empty;
        public string CandidateReceiptPath { get; set; } = string.Empty;
        public string EvaluationReportPath { get; set; } = string.Empty;
        public string PromotionPlanPath { get; set; } = string.Empty;
        public string HumanSelectionEvidencePath { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxPromotionMoveRecord
    {
        [JsonProperty("moveIndex", Order = 1)] public int MoveIndex { get; set; }
        [JsonProperty("observedGuid", Order = 2)] public string ObservedGuid { get; set; } = string.Empty;
        [JsonProperty("moved", Order = 3)] public bool Moved { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxPromotionTransactionReceipt
    {
        [JsonProperty("schemaVersion", Order = 1)] public string SchemaVersion { get; set; } = VfxCandidateProtocol.PromotionSchemaVersion;
        [JsonProperty("transactionId", Order = 2)] public string TransactionId { get; set; } = string.Empty;
        [JsonProperty("sessionId", Order = 3)] public string SessionId { get; set; } = string.Empty;
        [JsonProperty("generationPlanHash", Order = 4)] public string GenerationPlanHash { get; set; } = string.Empty;
        [JsonProperty("candidateReceiptHash", Order = 5)] public string CandidateReceiptHash { get; set; } = string.Empty;
        [JsonProperty("evaluationReportHash", Order = 6)] public string EvaluationReportHash { get; set; } = string.Empty;
        [JsonProperty("promotionPlanHash", Order = 7)] public string PromotionPlanHash { get; set; } = string.Empty;
        [JsonProperty("selectionEvidenceHash", Order = 8)] public string SelectionEvidenceHash { get; set; } = string.Empty;
        [JsonProperty("status", Order = 9)] public string Status { get; set; } = "Planned";
        [JsonProperty("message", Order = 10)] public string Message { get; set; } = string.Empty;
        [JsonProperty("moves", Order = 11)] public List<VfxPromotionMoveRecord> Moves { get; set; } = new List<VfxPromotionMoveRecord>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCorePromotedAssetRecord
    {
        [JsonProperty("sourceAssetPath", Order = 1)] public string SourceAssetPath { get; set; } = string.Empty;
        [JsonProperty("destinationAssetPath", Order = 2)] public string DestinationAssetPath { get; set; } = string.Empty;
        [JsonProperty("contentHash", Order = 3)] public string ContentHash { get; set; } = string.Empty;
        [JsonProperty("unityGuid", Order = 4)] public string UnityGuid { get; set; } = string.Empty;
        [JsonProperty("dependencyHash", Order = 5)] public string DependencyHash { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCorePromotionReceipt
    {
        [JsonProperty("schemaVersion", Order = 1)] public string SchemaVersion { get; set; } = VfxCandidateProtocol.CorePromotionReceiptSchemaVersion;
        [JsonProperty("promotionPlanHash", Order = 2)] public string PromotionPlanHash { get; set; } = string.Empty;
        [JsonProperty("catalogHash", Order = 3)] public string CatalogHash { get; set; } = string.Empty;
        [JsonProperty("executorId", Order = 4)] public string ExecutorId { get; set; } = "mx.vfx.unity-promotion-executor";
        [JsonProperty("executorVersion", Order = 5)] public string ExecutorVersion { get; set; } = "1.0.0";
        [JsonProperty("resultReviewStatus", Order = 6)] public string ResultReviewStatus { get; set; } = "ReviewNeeded";
        [JsonProperty("assets", Order = 7)] public List<VfxCorePromotedAssetRecord> Assets { get; set; } = new List<VfxCorePromotedAssetRecord>();
        [JsonProperty("receiptHash", Order = 8)] public string ReceiptHash { get; set; } = string.Empty;
    }

    internal sealed class VfxPromotionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string TransactionReceiptAssetPath { get; set; } = string.Empty;
        public string CorePromotionReceiptAssetPath { get; set; } = string.Empty;
        public string CorePromotionReceiptHash { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<string> PromotedAssetPaths { get; } = new List<string>();
    }

    internal sealed class VfxPromotionSelectionAuthorityClaims
    {
        public string CandidateId { get; set; } = string.Empty;
        public string TargetKind { get; set; } = string.Empty;
        public string TargetStableId { get; set; } = string.Empty;
        public string GenerationPlanHash { get; set; } = string.Empty;
        public string CandidateReceiptHash { get; set; } = string.Empty;
        public string EvaluationReportHash { get; set; } = string.Empty;
        public string SelectedBy { get; set; } = string.Empty;
        public string SelectionEvidenceHash { get; set; } = string.Empty;
        public string AuthorityReceiptHash { get; set; } = string.Empty;
        public bool StrategicFoundationExceptionApproved { get; set; }
        public List<string> ConsumerRecipeIds { get; set; } = new List<string>();
    }

    /// <summary>Trusted Editor-host boundary for verifying a plan-bound human selection authority receipt.</summary>
    /// <remarks>LowFreqAlloc. A missing verifier deliberately blocks promotion.</remarks>
    internal interface IVfxPromotionSelectionAuthorityVerifier
    {
        string AuthorityId { get; }
        string AuthorityVersion { get; }
        bool Verify(VfxPromotionSelectionAuthorityClaims claims, out string rejectionReason);
    }

    /// <summary>Final explicit human confirmation performed after every immutable evidence gate.</summary>
    /// <remarks>LowFreqAlloc. Production uses a modal Editor confirmation; tests inject a deterministic fake.</remarks>
    internal interface IVfxPromotionHumanConfirmation
    {
        bool Confirm(VfxCorePromotionPlan plan);
    }

    /// <summary>Generates one Editor-only VFX candidate from a data-only whitelist plan.</summary>
    /// <remarks>AllocByDesign: creates Unity assets and a receipt in an importable sandbox.</remarks>
    internal interface IVfxCandidateGenerator
    {
        VfxCandidateGenerationResult Generate(VfxCandidatePlan plan);
    }

    /// <summary>Creates support textures and materials for a candidate plan.</summary>
    /// <remarks>AllocByDesign: creates Editor assets under the supplied session root.</remarks>
    internal interface IVfxSupportAssetProvider
    {
        bool TryCreateAssets(VfxCandidatePlan plan, string sessionRoot, out VfxSupportAssetSet assets, out string error);
    }

    /// <summary>Captures deterministic visual evidence without opening or modifying the active Scene.</summary>
    /// <remarks>AllocByDesign: renders and writes rebuildable evidence under Temp.</remarks>
    internal interface IVfxPreviewCapture
    {
        VfxPreviewCaptureResult Capture(VfxPreviewCaptureRequest request);
    }

    /// <summary>Applies hard visual gates and returns ReviewNeeded evidence, never Ready approval.</summary>
    /// <remarks>AllocByDesign: creates diagnostic strings.</remarks>
    internal interface IVfxVisualEvaluator
    {
        VfxVisualEvaluationResult Evaluate(VfxCandidatePlan plan, VfxPreviewCaptureResult capture);
    }

    /// <summary>Promotes hash-guarded candidate assets while preserving their Unity GUIDs.</summary>
    /// <remarks>AllocByDesign: performs low-frequency AssetDatabase moves and transaction receipt writes.</remarks>
    internal interface IVfxPromotionExecutor
    {
        VfxPromotionResult Promote(VfxAuthorizedPromotionRequest request);
        VfxPromotionResult Recover(string transactionReceiptAssetPath);
    }
}
