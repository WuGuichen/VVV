using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public static class PhysicalAudioAuthoringFormats
    {
        public const string Authoring = "mx.audioPhysicalAuthoringManifest.v2";
        public const string Runtime = "mx.audioPhysicalRuntimeCatalog.v2";
        public const string LegacyAuthoring = "mx.audioPhysicalAuthoringManifest.v1";
        public const string LegacyRuntime = "mx.audioPhysicalRuntimeCatalog.v1";
    }

    public static class PhysicalAudioLifecycles
    {
        public const string Draft = "Draft";
        public const string Active = "Active";
        public const string Archived = "Archived";
    }

    public sealed class PhysicalAudioAuthoringManifestDocument
    {
        public string Format { get; set; } = PhysicalAudioAuthoringFormats.Authoring;
        public int SchemaVersion { get; set; } = 2;
        public string UpdatedAtUtc { get; set; } = string.Empty;
        public PhysicalAudioIdAllocationState IdAllocation { get; set; } = new PhysicalAudioIdAllocationState();
        public List<PhysicalAudioMaterialProfileAuthoring> MaterialProfiles { get; set; } = new List<PhysicalAudioMaterialProfileAuthoring>();
        public List<PhysicalAudioMaterialPairAuthoring> MaterialPairs { get; set; } = new List<PhysicalAudioMaterialPairAuthoring>();
        public List<PhysicalAudioFamilyAuthoring> Families { get; set; } = new List<PhysicalAudioFamilyAuthoring>();
        public List<PhysicalAudioBasisSoundAuthoring> BasisSounds { get; set; } = new List<PhysicalAudioBasisSoundAuthoring>();
        public List<PhysicalAudioResponseProfileAuthoring> ResponseProfiles { get; set; } = new List<PhysicalAudioResponseProfileAuthoring>();
        public List<PhysicalAudioScenarioPresetAuthoring> ScenarioPresets { get; set; } = new List<PhysicalAudioScenarioPresetAuthoring>();
        public List<PhysicalAudioCoverageRequirementAuthoring> CoverageRequirements { get; set; } = new List<PhysicalAudioCoverageRequirementAuthoring>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class PhysicalAudioIdAllocationState
    {
        public int NextMaterialId { get; set; } = 510001;
        public int NextFamilyId { get; set; } = 520001;
        public int NextBasisId { get; set; } = 530001;
        public int NextResponseProfileId { get; set; } = 540001;
        public int NextMaterialPairId { get; set; } = 550001;
        public List<int> RetiredMaterialIds { get; set; } = new List<int>();
        public List<int> RetiredFamilyIds { get; set; } = new List<int>();
        public List<int> RetiredBasisIds { get; set; } = new List<int>();
        public List<int> RetiredResponseProfileIds { get; set; } = new List<int>();
        public List<int> RetiredMaterialPairIds { get; set; } = new List<int>();
    }

    public sealed class PhysicalAudioMaterialProfileAuthoring
    {
        public string Id { get; set; } = string.Empty;
        public int RuntimeMaterialId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public float Hardness { get; set; } = 0.5f;
        public float Density { get; set; } = 0.5f;
        public float Roughness { get; set; } = 0.5f;
        public float Damping { get; set; } = 0.5f;
        public float Resonance { get; set; } = 0.5f;
        public string FallbackMaterialId { get; set; } = string.Empty;
        public string Lifecycle { get; set; } = PhysicalAudioLifecycles.Active;
    }

    public sealed class PhysicalAudioMaterialPairAuthoring
    {
        public string Id { get; set; } = string.Empty;
        public int RuntimeMaterialPairId { get; set; }
        public string MaterialAId { get; set; } = string.Empty;
        public string MaterialBId { get; set; } = string.Empty;
        public string Lifecycle { get; set; } = PhysicalAudioLifecycles.Active;
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class PhysicalAudioSelectionWeightsAuthoring
    {
        public float Material { get; set; } = 1f;
        public float Mass { get; set; } = 0.35f;
        public float ContactArea { get; set; } = 0.25f;
        public float Surface { get; set; } = 0.3f;
        public float Construction { get; set; } = 0.3f;
        public float Speed { get; set; } = 0.5f;
        public float Intensity { get; set; } = 0.5f;
        public float Angle { get; set; } = 0.2f;
    }

    public sealed class PhysicalAudioFamilyAuthoring
    {
        public string Id { get; set; } = string.Empty;
        public int RuntimeFamilyId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string InteractionKind { get; set; } = "ObjectImpact";
        public string InteractionMode { get; set; } = "Impact";
        public string VariantKind { get; set; } = "Impact";
        public string ImpactorRole { get; set; } = "Impactor";
        public string ReceiverRole { get; set; } = "Receiver";
        public string FallbackFamilyId { get; set; } = string.Empty;
        public string FallbackBasisId { get; set; } = string.Empty;
        public string ResponseProfileId { get; set; } = string.Empty;
        public int Priority { get; set; }
        public int CooldownFrames { get; set; }
        public PhysicalAudioSelectionWeightsAuthoring Weights { get; set; } = new PhysicalAudioSelectionWeightsAuthoring();
        public List<int> RequiredParameterIds { get; set; } = new List<int>();
        public string Lifecycle { get; set; } = PhysicalAudioLifecycles.Active;
    }

    public sealed class PhysicalAudioRangeAnchor
    {
        public float Min { get; set; }
        public float Max { get; set; } = 1f;
        public float Anchor { get; set; } = 0.5f;
    }

    public sealed class PhysicalAudioBodyAnchorAuthoring
    {
        public string MaterialId { get; set; } = "audio.material.generic_solid";
        public PhysicalAudioRangeAnchor Mass { get; set; } = new PhysicalAudioRangeAnchor();
        public PhysicalAudioRangeAnchor ContactArea { get; set; } = new PhysicalAudioRangeAnchor();
        public string Surface { get; set; } = "Normal";
        public string Construction { get; set; } = "Solid";
    }

    public sealed class PhysicalAudioBasisSoundAuthoring
    {
        public string Id { get; set; } = string.Empty;
        public int RuntimeBasisId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string FamilyId { get; set; } = string.Empty;
        public string MaterialPairId { get; set; } = string.Empty;
        public string VariantKind { get; set; } = string.Empty;
        public string AudioCueId { get; set; } = string.Empty;
        public string AdoptionSetId { get; set; } = string.Empty;
        public PhysicalAudioBodyAnchorAuthoring Impactor { get; set; } = new PhysicalAudioBodyAnchorAuthoring();
        public PhysicalAudioBodyAnchorAuthoring Receiver { get; set; } = new PhysicalAudioBodyAnchorAuthoring();
        public PhysicalAudioBodyAnchorAuthoring SideA { get; set; }
        public PhysicalAudioBodyAnchorAuthoring SideB { get; set; }
        public PhysicalAudioRangeAnchor RelativeSpeed { get; set; } = new PhysicalAudioRangeAnchor();
        public PhysicalAudioRangeAnchor Intensity { get; set; } = new PhysicalAudioRangeAnchor();
        public PhysicalAudioRangeAnchor AngleDegrees { get; set; } = new PhysicalAudioRangeAnchor { Max = 180f, Anchor = 90f };
        public int Priority { get; set; }
        public string Lifecycle { get; set; } = PhysicalAudioLifecycles.Draft;
        public string AuditionBindingHash { get; set; } = string.Empty;
        public AudioCollisionProfileV1 SourceCollisionProfile { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class PhysicalAudioResponsePointAuthoring
    {
        public float Input { get; set; }
        public float Output { get; set; }
    }

    public sealed class PhysicalAudioResponseCurveAuthoring
    {
        public int ParameterId { get; set; }
        public string Input { get; set; } = "Intensity";
        public float OutputMin { get; set; }
        public float OutputMax { get; set; } = 1f;
        public List<PhysicalAudioResponsePointAuthoring> Points { get; set; } = new List<PhysicalAudioResponsePointAuthoring>();
    }

    public sealed class PhysicalAudioResponseProfileAuthoring
    {
        public string Id { get; set; } = string.Empty;
        public int RuntimeResponseProfileId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public List<PhysicalAudioResponseCurveAuthoring> Curves { get; set; } = new List<PhysicalAudioResponseCurveAuthoring>();
        public string Lifecycle { get; set; } = PhysicalAudioLifecycles.Active;
    }

    public sealed class PhysicalAudioInteractionFactAuthoring
    {
        public string InteractionKind { get; set; } = "ObjectImpact";
        public string InteractionMode { get; set; } = "Impact";
        public string Phase { get; set; } = "Started";
        public PhysicalAudioBodyFactAuthoring Impactor { get; set; } = new PhysicalAudioBodyFactAuthoring();
        public PhysicalAudioBodyFactAuthoring Receiver { get; set; } = new PhysicalAudioBodyFactAuthoring();
        public float RelativeSpeed { get; set; }
        public float NormalSpeed { get; set; }
        public float TangentialSpeed { get; set; }
        // Compatibility input for older Workbench clients. New clients should send TangentialSpeed.
        public float TangentialRatio { get; set; }
        public float Pressure { get; set; }
        public float Impulse { get; set; }
        public float Intensity { get; set; }
        public float AngleDegrees { get; set; }
        public PhysicalAudioDirectionAuthoring ContactNormal { get; set; } = new PhysicalAudioDirectionAuthoring();
        public long Frame { get; set; }
        public long Sequence { get; set; }
    }

    public sealed class PhysicalAudioDirectionAuthoring
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public sealed class PhysicalAudioBodyFactAuthoring
    {
        public string MaterialId { get; set; } = "audio.material.generic_solid";
        public float Mass { get; set; } = 0.5f;
        public float ContactArea { get; set; } = 0.5f;
        public string Surface { get; set; } = "Normal";
        public string Construction { get; set; } = "Solid";
    }

    public sealed class PhysicalAudioScenarioPresetAuthoring
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public PhysicalAudioInteractionFactAuthoring Fact { get; set; } = new PhysicalAudioInteractionFactAuthoring();
    }

    public sealed class PhysicalAudioCoverageRequirementAuthoring
    {
        public string Id { get; set; } = string.Empty;
        public string FamilyId { get; set; } = string.Empty;
        public List<string> MaterialPairIds { get; set; } = new List<string>();
        public List<string> VariantKinds { get; set; } = new List<string>();
        public List<string> ImpactorMaterialIds { get; set; } = new List<string>();
        public List<string> ReceiverMaterialIds { get; set; } = new List<string>();
        /// <summary>
        /// Optional complete authored facts for this requirement. When present, every fact is resolved for
        /// every configured material pair instead of treating material presence as coverage evidence.
        /// </summary>
        public List<PhysicalAudioInteractionFactAuthoring> Facts { get; set; } = new List<PhysicalAudioInteractionFactAuthoring>();
        /// <summary>Optional references to project-level scenario presets used as coverage facts.</summary>
        public List<string> ScenarioPresetIds { get; set; } = new List<string>();
        public bool Required { get; set; } = true;
    }

    public sealed class PhysicalAudioValidationReport
    {
        public bool HasErrors { get; set; }
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
    }

    public sealed class PhysicalAudioCoverageCell
    {
        public string RequirementId { get; set; } = string.Empty;
        public string FamilyId { get; set; } = string.Empty;
        public string MaterialPairId { get; set; } = string.Empty;
        public string VariantKind { get; set; } = string.Empty;
        public string ImpactorMaterialId { get; set; } = string.Empty;
        public string ReceiverMaterialId { get; set; } = string.Empty;
        public int MatchCount { get; set; }
        public int ScenarioCount { get; set; }
        public int ResolvedScenarioCount { get; set; }
        public int AmbiguousScenarioCount { get; set; }
        public List<string> SelectedBasisIds { get; set; } = new List<string>();
        public string FailureCode { get; set; } = string.Empty;
        public string Status { get; set; } = "Missing";
    }

    public sealed class PhysicalAudioCoverageReport
    {
        public int RequiredCellCount { get; set; }
        public int CoveredCellCount { get; set; }
        public int MissingCellCount { get; set; }
        public int AmbiguousCellCount { get; set; }
        public List<PhysicalAudioCoverageCell> Cells { get; set; } = new List<PhysicalAudioCoverageCell>();
    }

    public sealed class PhysicalAudioResolvedParameterAuthoring
    {
        public int ParameterId { get; set; }
        public float Value { get; set; }
    }

    public sealed class PhysicalAudioResolveTraceEntry
    {
        public string BasisId { get; set; } = string.Empty;
        public string MaterialPairId { get; set; } = string.Empty;
        public string VariantKind { get; set; } = string.Empty;
        public bool Accepted { get; set; }
        public float Score { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class PhysicalAudioResolveTraceSnapshot
    {
        public string FamilyId { get; set; } = string.Empty;
        public string SelectedBasisId { get; set; } = string.Empty;
        public string MaterialPairId { get; set; } = string.Empty;
        public string VariantKind { get; set; } = string.Empty;
        public List<string> MaterialFallbackPath { get; set; } = new List<string>();
        public bool UsedFallback { get; set; }
        public List<PhysicalAudioResolveTraceEntry> Candidates { get; set; } = new List<PhysicalAudioResolveTraceEntry>();
    }

    public sealed class PhysicalAudioResolvedPlaybackAuthoring
    {
        public bool Success { get; set; }
        public string Code { get; set; } = string.Empty;
        public string BasisId { get; set; } = string.Empty;
        public string AudioCueId { get; set; } = string.Empty;
        public string AdoptionSetId { get; set; } = string.Empty;
        public float Score { get; set; }
        public List<PhysicalAudioResolvedParameterAuthoring> Parameters { get; set; } = new List<PhysicalAudioResolvedParameterAuthoring>();
        public PhysicalAudioResolveTraceSnapshot Trace { get; set; } = new PhysicalAudioResolveTraceSnapshot();
    }

    public sealed class PhysicalAudioRuntimeCatalogDocument
    {
        public string Format { get; set; } = PhysicalAudioAuthoringFormats.Runtime;
        public int SchemaVersion { get; set; } = 2;
        public string PhysicalManifestHash { get; set; } = string.Empty;
        public string AudioCatalogHash { get; set; } = string.Empty;
        public List<PhysicalAudioRuntimeMaterial> Materials { get; set; } = new List<PhysicalAudioRuntimeMaterial>();
        public List<PhysicalAudioRuntimeMaterialPair> MaterialPairs { get; set; } = new List<PhysicalAudioRuntimeMaterialPair>();
        public List<PhysicalAudioRuntimeFamily> Families { get; set; } = new List<PhysicalAudioRuntimeFamily>();
        public List<PhysicalAudioRuntimeBasisSound> BasisSounds { get; set; } = new List<PhysicalAudioRuntimeBasisSound>();
        public List<PhysicalAudioRuntimeResponseProfile> ResponseProfiles { get; set; } = new List<PhysicalAudioRuntimeResponseProfile>();
    }

    public sealed class PhysicalAudioRuntimeMaterial
    {
        public int RuntimeMaterialId { get; set; }
        public float Hardness { get; set; }
        public float Density { get; set; }
        public float Roughness { get; set; }
        public float Damping { get; set; }
        public float Resonance { get; set; }
        public int FallbackMaterialRuntimeId { get; set; }
    }

    public sealed class PhysicalAudioRuntimeMaterialPair
    {
        public int RuntimeMaterialPairId { get; set; }
        public int MaterialARuntimeId { get; set; }
        public int MaterialBRuntimeId { get; set; }
    }

    public sealed class PhysicalAudioRuntimeFamily
    {
        public int RuntimeFamilyId { get; set; }
        public int InteractionKind { get; set; }
        public int InteractionMode { get; set; }
        public int VariantKind { get; set; }
        public int FallbackFamilyRuntimeId { get; set; }
        public int FallbackBasisRuntimeId { get; set; }
        public int ResponseProfileRuntimeId { get; set; }
        public int Priority { get; set; }
        public int CooldownFrames { get; set; }
        public PhysicalAudioSelectionWeightsAuthoring Weights { get; set; } = new PhysicalAudioSelectionWeightsAuthoring();
    }

    public sealed class PhysicalAudioRuntimeBodyAnchor
    {
        public int MaterialRuntimeId { get; set; }
        public PhysicalAudioRangeAnchor Mass { get; set; } = new PhysicalAudioRangeAnchor();
        public PhysicalAudioRangeAnchor ContactArea { get; set; } = new PhysicalAudioRangeAnchor();
        public int Surface { get; set; }
        public int Construction { get; set; }
    }

    public sealed class PhysicalAudioRuntimeBasisSound
    {
        public int RuntimeBasisId { get; set; }
        public int FamilyRuntimeId { get; set; }
        public int MaterialPairRuntimeId { get; set; }
        public int VariantKind { get; set; }
        public int RuntimeCueId { get; set; }
        public int Priority { get; set; }
        public PhysicalAudioRuntimeBodyAnchor Impactor { get; set; } = new PhysicalAudioRuntimeBodyAnchor();
        public PhysicalAudioRuntimeBodyAnchor Receiver { get; set; } = new PhysicalAudioRuntimeBodyAnchor();
        public PhysicalAudioRuntimeBodyAnchor SideA { get; set; } = new PhysicalAudioRuntimeBodyAnchor();
        public PhysicalAudioRuntimeBodyAnchor SideB { get; set; } = new PhysicalAudioRuntimeBodyAnchor();
        public PhysicalAudioRangeAnchor RelativeSpeed { get; set; } = new PhysicalAudioRangeAnchor();
        public PhysicalAudioRangeAnchor Intensity { get; set; } = new PhysicalAudioRangeAnchor();
        public PhysicalAudioRangeAnchor AngleDegrees { get; set; } = new PhysicalAudioRangeAnchor();
    }

    public sealed class PhysicalAudioRuntimeResponseProfile
    {
        public int RuntimeResponseProfileId { get; set; }
        public List<PhysicalAudioRuntimeResponseCurve> Curves { get; set; } = new List<PhysicalAudioRuntimeResponseCurve>();
    }

    public sealed class PhysicalAudioRuntimeResponseCurve
    {
        public int ParameterId { get; set; }
        public int Input { get; set; }
        public float OutputMin { get; set; }
        public float OutputMax { get; set; } = 1f;
        public List<PhysicalAudioResponsePointAuthoring> Points { get; set; } = new List<PhysicalAudioResponsePointAuthoring>();
    }
}
