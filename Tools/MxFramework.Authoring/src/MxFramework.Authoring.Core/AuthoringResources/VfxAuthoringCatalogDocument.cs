using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    /// <summary>Authoring-only semantic catalog for reusable VFX assets and published effects.</summary>
    /// <remarks>LowFreqAlloc. JSON DTO loaded and saved by Authoring tools; never consumed by the VFX runtime tick path.</remarks>
    public sealed class VfxAuthoringCatalogDocument
    {
        public string SchemaVersion { get; set; } = VfxAuthoringSchemaVersions.V2;
        public string CatalogId { get; set; } = "mxframework.vfx.authoring";
        public string TaxonomyVersion { get; set; } = VfxAuthoringTaxonomyVersions.V1;
        public bool StrictCoverage { get; set; }
        public List<VfxAuthoringPrimitive> Primitives { get; set; } = new List<VfxAuthoringPrimitive>();
        public List<VfxAuthoringRecipe> Recipes { get; set; } = new List<VfxAuthoringRecipe>();
        public List<VfxAuthoringEffect> Effects { get; set; } = new List<VfxAuthoringEffect>();
    }

    /// <summary>One reusable authoring building block such as a flash, ring, smoke layer, or graph subgraph.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO.</remarks>
    public sealed class VfxAuthoringPrimitive
    {
        public string PrimitiveId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public VfxAuthoringAssetReference AssetRef { get; set; } = new VfxAuthoringAssetReference();
        public VfxAuthoringSourceEvidence Source { get; set; }
        public string Backend { get; set; } = VfxAuthoringBackends.ParticleSystem;
        public List<string> Roles { get; set; } = new List<string>();
        public List<string> CompatibleAnchors { get; set; } = new List<string>();
        public List<VfxAuthoringParameterDefinition> Parameters { get; set; } = new List<VfxAuthoringParameterDefinition>();
        public string VisualMaturity { get; set; } = VfxAuthoringVisualMaturities.LegacyPlaceholder;
        public string ReviewStatus { get; set; } = VfxAuthoringReviewStatuses.Draft;
        public bool Exemplar { get; set; }
        public VfxAuthoringApprovalEvidence Approval { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    public sealed class VfxAuthoringAssetReference
    {
        public string Kind { get; set; } = VfxAuthoringAssetReferenceKinds.UnityAsset;
        public string Path { get; set; } = string.Empty;
    }

    /// <summary>Immutable provenance for a Primitive extracted from an explicitly licensed source package.</summary>
    /// <remarks>LowFreqAlloc. Authoring governance only; not consumed by Runtime, SaveState, networking, or gameplay hashes.</remarks>
    public sealed class VfxAuthoringSourceEvidence
    {
        public string SourceKind { get; set; } = string.Empty;
        public string SourcePackageId { get; set; } = string.Empty;
        public string SourceLibraryId { get; set; } = string.Empty;
        public string SourceEffectId { get; set; } = string.Empty;
        public List<string> SourceComponentIds { get; set; } = new List<string>();
        public string SourcePrefabGuid { get; set; } = string.Empty;
        public string SourceObjectPath { get; set; } = string.Empty;
        public string SourceContentHash { get; set; } = string.Empty;
        public string SourceDependencyHash { get; set; } = string.Empty;
        public string DerivationMode { get; set; } = string.Empty;
        public string DependencyPolicy { get; set; } = VfxSourceDependencyPolicies.DirectReference;
        public string ExtractorId { get; set; } = string.Empty;
        public string ExtractorVersion { get; set; } = string.Empty;
        public string LicenseId { get; set; } = string.Empty;
        public bool PublicRedistributionAllowed { get; set; }
    }

    /// <summary>Ordered authoring composition of reusable VFX primitives.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO; recipes are baked before runtime.</remarks>
    public sealed class VfxAuthoringRecipe
    {
        public string RecipeId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Parameters { get; set; } = new List<string>();
        public List<string> IncludedRecipeIds { get; set; } = new List<string>();
        public List<VfxAuthoringRecipeLayer> Layers { get; set; } = new List<VfxAuthoringRecipeLayer>();
        public string VisualMaturity { get; set; } = VfxAuthoringVisualMaturities.LegacyPlaceholder;
        public string ReviewStatus { get; set; } = VfxAuthoringReviewStatuses.Draft;
        public bool Canonical { get; set; }
        public VfxAuthoringRecipeReuseQualification ReuseQualification { get; set; }
        public bool Exemplar { get; set; }
        public VfxAuthoringApprovalEvidence Approval { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    public sealed class VfxAuthoringRecipeLayer
    {
        public string LayerId { get; set; } = string.Empty;
        public string PrimitiveId { get; set; } = string.Empty;
        public int Order { get; set; }
        public Dictionary<string, string> Bindings { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, JsonElement> Overrides { get; set; } = new Dictionary<string, JsonElement>();
    }

    /// <summary>Immutable comparison evidence required before a reusable Recipe can be declared canonical and Ready.</summary>
    /// <remarks>LowFreqAlloc. Authoring governance only; it does not enter Runtime or gameplay hashes.</remarks>
    public sealed class VfxAuthoringRecipeReuseQualification
    {
        public string SchemaVersion { get; set; } = "mx.vfx.recipe-reuse-qualification.v1";
        public string QualificationId { get; set; } = string.Empty;
        public string SpecializedBaselineId { get; set; } = string.Empty;
        public List<VfxAuthoringRecipeVariantQualification> Variants { get; set; } = new List<VfxAuthoringRecipeVariantQualification>();
        public string QualificationHash { get; set; } = string.Empty;
    }

    public sealed class VfxAuthoringRecipeVariantQualification
    {
        public string VariantId { get; set; } = string.Empty;
        public string ThemeId { get; set; } = string.Empty;
        public string ScaleProfileId { get; set; } = string.Empty;
        public float QualityDegradationPercent { get; set; }
        public float PerformanceDegradationPercent { get; set; }
        public string EvaluationReportHash { get; set; } = string.Empty;
    }

    /// <summary>One runtime-loadable or catalog-only published VFX effect.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO; runtime consumes only the compiled ResourceKey/definition/trigger outputs.</remarks>
    public sealed class VfxAuthoringEffect
    {
        public string EffectId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public VfxAuthoringResourceKey ResourceKey { get; set; } = new VfxAuthoringResourceKey();
        public int DefinitionId { get; set; }
        public string RecipeId { get; set; } = string.Empty;
        public string Backend { get; set; } = VfxAuthoringBackends.ParticleSystem;
        public List<string> Roles { get; set; } = new List<string>();
        public List<string> ThemeIds { get; set; } = new List<string>();
        public VfxAuthoringAnchorContract AnchorContract { get; set; } = new VfxAuthoringAnchorContract();
        public VfxAuthoringLifecycle Lifecycle { get; set; } = new VfxAuthoringLifecycle();
        public Dictionary<string, JsonElement> ParameterOverrides { get; set; } = new Dictionary<string, JsonElement>();
        public VfxAuthoringQuality Quality { get; set; } = new VfxAuthoringQuality();
        public VfxAuthoringBudget Budget { get; set; } = new VfxAuthoringBudget();
        public VfxAuthoringPreview Preview { get; set; } = new VfxAuthoringPreview();
        public VfxAuthoringPublishedEffectEvidence Published { get; set; }
        public string VisualMaturity { get; set; } = VfxAuthoringVisualMaturities.LegacyPlaceholder;
        public string ReviewStatus { get; set; } = VfxAuthoringReviewStatuses.Draft;
        public bool Exemplar { get; set; }
        public VfxAuthoringApprovalEvidence Approval { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    /// <summary>Binds a human Ready approval to immutable authoring and evaluation evidence.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only governance evidence; never enters Runtime hashes, SaveState, networking, or gameplay authority.</remarks>
    public sealed class VfxAuthoringApprovalEvidence
    {
        public string ApprovalId { get; set; } = string.Empty;
        public string ApprovedBy { get; set; } = string.Empty;
        public string ApprovalRole { get; set; } = string.Empty;
        public string AssetContentHash { get; set; } = string.Empty;
        public string DependencyHash { get; set; } = string.Empty;
        public string RecipeInputHash { get; set; } = string.Empty;
        public string EvaluationReportHash { get; set; } = string.Empty;
        public string QualityProfileVersion { get; set; } = string.Empty;
        public string RecipeReuseQualificationHash { get; set; } = string.Empty;
        public string ApprovalAuthorityId { get; set; } = string.Empty;
        public string ApprovalAuthorityVersion { get; set; } = string.Empty;
        public string AuthorityReceiptHash { get; set; } = string.Empty;
        public List<VfxAuthoringHumanApproval> HumanApprovals { get; set; } = new List<VfxAuthoringHumanApproval>();
    }

    /// <summary>One explicit human approval attached to immutable VFX evidence.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only audit metadata.</remarks>
    public sealed class VfxAuthoringHumanApproval
    {
        public string ReviewerId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    /// <summary>Authoring-time evidence for a Recipe-baked Published Effect prefab.</summary>
    /// <remarks>LowFreqAlloc. Does not enter runtime config, hashes, SaveState, or networking.</remarks>
    public sealed class VfxAuthoringPublishedEffectEvidence
    {
        public string AssetPath { get; set; } = string.Empty;
        public string RecipeInputHash { get; set; } = string.Empty;
        public string BakerId { get; set; } = string.Empty;
        public string BakerVersion { get; set; } = string.Empty;
    }

    public sealed class VfxAuthoringResourceKey
    {
        public string Id { get; set; } = string.Empty;
        public string TypeId { get; set; } = "GameObject";
        public string Variant { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;

        /// <summary>Builds a stable comparison key for authoring evidence and coverage checks.</summary>
        /// <remarks>LowFreqAlloc. Concatenates four authoring strings.</remarks>
        public string ToStableKey()
        {
            return (PackageId ?? string.Empty) + "|" +
                   (Id ?? string.Empty) + "|" +
                   (TypeId ?? string.Empty) + "|" +
                   (Variant ?? string.Empty);
        }
    }

    public sealed class VfxAuthoringAnchorContract
    {
        public string Anchor { get; set; } = VfxAuthoringAnchors.World;
        public string Space { get; set; } = VfxAuthoringSpaces.World;
    }

    public sealed class VfxAuthoringLifecycle
    {
        public string Mode { get; set; } = VfxAuthoringLifecycleModes.OneShot;
        public int DefaultLifetimeFrames { get; set; }
        public string StopPolicy { get; set; } = VfxAuthoringStopPolicies.Expire;
    }

    public sealed class VfxAuthoringQuality
    {
        public string CostTier { get; set; } = VfxAuthoringCostTiers.Medium;
        public string FallbackPolicy { get; set; } = VfxAuthoringFallbackPolicies.None;
        public string FallbackEffectId { get; set; } = string.Empty;
        public List<string> PlatformWhitelist { get; set; } = new List<string>();
    }

    public sealed class VfxAuthoringBudget
    {
        public int PrewarmCount { get; set; }
        public int RecommendedMaxConcurrent { get; set; }
        public string Priority { get; set; } = VfxAuthoringPriorities.Normal;
        public float CullDistanceMeters { get; set; }
    }

    public sealed class VfxAuthoringPreview
    {
        public string AnchorPreset { get; set; } = string.Empty;
        public float TargetDistanceMeters { get; set; }
        public float DurationSeconds { get; set; }
        public float CameraScaleMeters { get; set; }
    }

    public sealed class VfxAuthoringParameterDefinition
    {
        public string ParameterId { get; set; } = string.Empty;
        public string ValueType { get; set; } = VfxAuthoringParameterValueTypes.Float;
        public string Unit { get; set; } = string.Empty;
        public JsonElement? DefaultValue { get; set; }
        public JsonElement? MinValue { get; set; }
        public JsonElement? MaxValue { get; set; }
        public string Transport { get; set; } = VfxAuthoringParameterTransports.StaticBake;
        public string BackendProperty { get; set; } = string.Empty;
        public float RuntimeScale { get; set; } = 1f;
    }

    /// <summary>Optional observed evidence used to validate catalog bindings and strict coverage.</summary>
    /// <remarks>LowFreqAlloc. Authoring scanners populate these sets outside runtime.</remarks>
    public sealed class VfxAuthoringCatalogEvidence
    {
        public HashSet<string> UnityAssetPaths { get; set; } = new HashSet<string>();
        public HashSet<string> ResourceKeys { get; set; } = new HashSet<string>();
        public HashSet<int> DefinitionIds { get; set; } = new HashSet<int>();
        public HashSet<string> DirectVfxResourceKeys { get; set; } = new HashSet<string>();
        public Dictionary<string, string> ResourceAssetPaths { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, int> DefinitionIdsByResourceKey { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> DefinitionLifetimesByResourceKey { get; set; } = new Dictionary<string, int>();
        public HashSet<string> StaleEffectIds { get; set; } = new HashSet<string>();
        public Dictionary<string, string> PublishedRecipeInputHashes { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> PublishedAssetPaths { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> PreviewHashes { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> ThumbnailPaths { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> PerceptualHashes { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> AssetContentHashes { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> DependencyHashes { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> EvaluationReportHashes { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> QualityProfileVersions { get; set; } = new Dictionary<string, string>();
        public string QualityProfileVersion { get; set; } = string.Empty;
        public bool VfxGraphAvailable { get; set; }

        /// <summary>Optional authenticated host integration used to verify Ready authority receipts.</summary>
        /// <remarks>LowFreqAlloc. Not serialized; production scanners deliberately leave it unset until a trusted approval service is configured.</remarks>
        [JsonIgnore]
        public IVfxReadyApprovalAuthorityVerifier ApprovalAuthorityVerifier { get; set; }
    }

    /// <summary>Validation result for one VFX Authoring Catalog.</summary>
    /// <remarks>LowFreqAlloc. Owns a newly allocated, stably sorted diagnostics list.</remarks>
    public sealed class VfxAuthoringCatalogValidationReport
    {
        public bool HasErrors { get; set; }
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
    }

    /// <summary>Recoverable JSON load result for one VFX Authoring Catalog.</summary>
    /// <remarks>LowFreqAlloc. Contains a catalog or parse diagnostics.</remarks>
    public sealed class VfxAuthoringCatalogLoadResult
    {
        public bool Success { get; set; }
        public VfxAuthoringCatalogDocument Catalog { get; set; }
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
    }

    public static class VfxAuthoringSchemaVersions
    {
        public const string V1 = "mx.vfx.authoring.v1";
        public const string V2 = "mx.vfx.authoring.v2";
    }

    public static class VfxAuthoringTaxonomyVersions
    {
        public const string V1 = "mx.vfx.taxonomy.v1";
    }

    public static class VfxAuthoringAssetReferenceKinds
    {
        public const string UnityAsset = "UnityAsset";
    }

    public static class VfxAuthoringSourceKinds
    {
        public const string LicensedThirdParty = "LicensedThirdParty";
    }

    public static class VfxAuthoringDerivationModes
    {
        public const string ExtractedLayer = "ExtractedLayer";
        public const string ParameterizedVariant = "ParameterizedVariant";
    }

    public static class VfxAuthoringBackends
    {
        public const string ParticleSystem = "ParticleSystem";
        public const string LineRenderer = "LineRenderer";
        public const string CustomView = "CustomView";
        public const string VfxGraph = "VfxGraph";
    }

    public static class VfxAuthoringRoles
    {
        public const string Charge = "Charge";
        public const string Cast = "Cast";
        public const string Flight = "Flight";
        public const string Impact = "Impact";
        public const string Telegraph = "Telegraph";
        public const string Beam = "Beam";
        public const string Aura = "Aura";
        public const string Status = "Status";
        public const string Zone = "Zone";
        public const string Environment = "Environment";
        public const string Transition = "Transition";
    }

    public static class VfxAuthoringAnchors
    {
        public const string World = "World";
        public const string Caster = "Caster";
        public const string Target = "Target";
        public const string Socket = "Socket";
        public const string HitPoint = "HitPoint";
        public const string Path = "Path";
        public const string Ground = "Ground";
    }

    public static class VfxAuthoringSpaces
    {
        public const string Local = "Local";
        public const string World = "World";
    }

    public static class VfxAuthoringLifecycleModes
    {
        public const string OneShot = "OneShot";
        public const string Loop = "Loop";
        public const string Persistent = "Persistent";
        public const string ManualStop = "ManualStop";
    }

    public static class VfxAuthoringStopPolicies
    {
        public const string Expire = "Expire";
        public const string Stop = "Stop";
        public const string Manual = "Manual";
    }

    public static class VfxAuthoringReviewStatuses
    {
        public const string Draft = "Draft";
        public const string ReviewNeeded = "ReviewNeeded";
        public const string Ready = "Ready";
        public const string Deprecated = "Deprecated";
    }

    /// <summary>Artistic production maturity, kept separate from review workflow state.</summary>
    public static class VfxAuthoringVisualMaturities
    {
        public const string LegacyPlaceholder = "LegacyPlaceholder";
        public const string Candidate = "Candidate";
        public const string Production = "Production";
    }

    public static class VfxAuthoringApprovalRoles
    {
        public const string VfxArtist = "VfxArtist";
        public const string TechnicalArtist = "TechnicalArtist";
        public const string Gameplay = "Gameplay";
        public const string UserExperience = "UserExperience";
    }

    public static class VfxAuthoringPriorities
    {
        public const string Cosmetic = "Cosmetic";
        public const string Normal = "Normal";
        public const string GameplayImportant = "GameplayImportant";
        public const string CriticalTelegraph = "CriticalTelegraph";
    }

    public static class VfxAuthoringCostTiers
    {
        public const string Low = "Low";
        public const string Medium = "Medium";
        public const string High = "High";
    }

    public static class VfxAuthoringFallbackPolicies
    {
        public const string None = "None";
        public const string UseResourceVariant = "UseResourceVariant";
        public const string UseFallbackEffect = "UseFallbackEffect";
    }

    public static class VfxAuthoringParameterValueTypes
    {
        public const string Float = "Float";
        public const string Int = "Int";
        public const string Bool = "Bool";
        public const string Color = "Color";
        public const string Vector3 = "Vector3";
        public const string Event = "Event";
    }

    public static class VfxAuthoringParameterTransports
    {
        public const string RuntimeSlot0 = "RuntimeSlot0";
        public const string RuntimeSlot1 = "RuntimeSlot1";
        public const string RuntimeSlot2 = "RuntimeSlot2";
        public const string RuntimeSlot3 = "RuntimeSlot3";
        public const string StaticBake = "StaticBake";
        public const string PreviewOnly = "PreviewOnly";
        public const string BackendDefault = "BackendDefault";
    }
}
