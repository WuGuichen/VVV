using System.Collections.Generic;

namespace MxFramework.ResourceMapping.Authoring
{
    public static class GlobalResourceBuildPlanContracts
    {
        public const string Schema = "mx.global-resource-build-plan.v2";
        public const string CompilerContractVersion = "2";
    }

    public sealed class GlobalResourceBuildPlan
    {
        public string Schema { get; set; } = GlobalResourceBuildPlanContracts.Schema;
        public string CompilerContractVersion { get; set; } = GlobalResourceBuildPlanContracts.CompilerContractVersion;
        public string ProfileId { get; set; } = string.Empty;
        public string CatalogId { get; set; } = string.Empty;
        public string BuildTarget { get; set; } = string.Empty;
        public string MappingInputFingerprint { get; set; } = string.Empty;
        public string BuildPlanFingerprint { get; set; } = string.Empty;
        public bool Success { get; set; }
        public GlobalResourceBuildPlanCounts Counts { get; set; } = new GlobalResourceBuildPlanCounts();
        public List<GlobalResourceBuildPlanEntry> Entries { get; set; } = new List<GlobalResourceBuildPlanEntry>();
        public List<GlobalResourceBuildPlanBundle> Bundles { get; set; } = new List<GlobalResourceBuildPlanBundle>();
        public List<GlobalResourceBuildPlanCatalogMount> CatalogMounts { get; set; } = new List<GlobalResourceBuildPlanCatalogMount>();
        public List<GlobalResourceBuildPlanPreloadGroup> PreloadGroups { get; set; } = new List<GlobalResourceBuildPlanPreloadGroup>();
        public List<ResourceMappingDiagnostic> Diagnostics { get; set; } = new List<ResourceMappingDiagnostic>();
    }

    public sealed class GlobalResourceBuildPlanCounts
    {
        public int Internal { get; set; }
        public int External { get; set; }
        public int EditorOnly { get; set; }
        public int Excluded { get; set; }
    }

    public sealed class GlobalResourceBuildPlanEntry
    {
        public ResourceMappingProducerReceipt ProducerReceipt { get; set; } = new ResourceMappingProducerReceipt();
        public string AuthoringRecordId { get; set; } = string.Empty;
        public ResourceMappingResourceKey ResourceKey { get; set; }
        public ResourceMappingSourceLocator SourceLocator { get; set; }
        public ResourceMappingDeliveryIntent DeliveryIntent { get; set; }
        public ResourceMappingOverrideIntent? OverrideIntent { get; set; }
        public string BundleRuleId { get; set; } = string.Empty;
        public string BundleId { get; set; } = string.Empty;
        public string CatalogMountId { get; set; } = string.Empty;
        public int EffectiveMountOrder { get; set; }
        public List<string> Labels { get; set; } = new List<string>();
        public List<string> PreloadGroups { get; set; } = new List<string>();
        public List<ResourceMappingResourceKey> Dependencies { get; set; } = new List<ResourceMappingResourceKey>();
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>();
    }

    public sealed class GlobalResourceBuildPlanBundle
    {
        public string BundleId { get; set; } = string.Empty;
        public string BundleName { get; set; } = string.Empty;
        public string Compression { get; set; } = string.Empty;
        public string BuildTarget { get; set; } = string.Empty;
        public bool IncludeDependencies { get; set; }
        public List<ResourceMappingResourceKey> ResourceKeys { get; set; } = new List<ResourceMappingResourceKey>();
    }

    public sealed class GlobalResourceBuildPlanCatalogMount
    {
        public string CatalogMountId { get; set; } = string.Empty;
        public string CatalogId { get; set; } = string.Empty;
        public ResourceMappingCatalogBootstrapKind BootstrapKind { get; set; }
        public string RelativeCatalogPath { get; set; } = string.Empty;
        public int MountOrder { get; set; }
        public string ExpectedCatalogFingerprint { get; set; } = string.Empty;
        public List<GlobalResourceBuildPlanCatalogAllowlistedKey> AllowlistedKeys { get; set; } = new List<GlobalResourceBuildPlanCatalogAllowlistedKey>();
    }

    public sealed class GlobalResourceBuildPlanCatalogAllowlistedKey
    {
        public ResourceMappingResourceKey ResourceKey { get; set; } = new ResourceMappingResourceKey();
        public ResourceMappingOverrideIntent OverrideIntent { get; set; }
    }

    public sealed class GlobalResourceBuildPlanPreloadGroup
    {
        public string Id { get; set; } = string.Empty;
        public bool FailFast { get; set; }
        public int MaxConcurrentLoads { get; set; }
        public List<ResourceMappingResourceKey> ResourceKeys { get; set; } = new List<ResourceMappingResourceKey>();
    }

    public sealed class GlobalResourceBuildPlanCompilation
    {
        public GlobalResourceBuildPlan Plan { get; set; } = new GlobalResourceBuildPlan();
        public string CanonicalPlanJson { get; set; } = string.Empty;
    }
}
