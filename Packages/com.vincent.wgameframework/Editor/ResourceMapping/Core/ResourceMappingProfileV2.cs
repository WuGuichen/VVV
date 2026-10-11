using System;
using System.Collections.Generic;

namespace MxFramework.ResourceMapping.Authoring
{
    public static class ResourceMappingSchemas
    {
        public const string ProfileV2 = "mx.resource-mapping-profile.v2";
        public const string CutoverWorksheetV1 = "mx.resource-mapping-cutover-worksheet.v1";
    }

    public enum ResourceMappingDeliveryIntent
    {
        Internal,
        External,
        EditorOnly,
        Excluded
    }

    public enum ResourceMappingOverrideIntent
    {
        Forbid,
        Allow
    }

    public enum ResourceMappingCatalogBootstrapKind
    {
        StreamingAssetsCatalog,
        CompositionRoot
    }

    public enum ResourceMappingSourceLocatorKind
    {
        UnityAsset,
        GeneratedUnityAsset,
        DomainSource,
        ImportedPackageFile
    }

    public sealed class ResourceMappingProfileV2
    {
        public string Schema { get; set; } = ResourceMappingSchemas.ProfileV2;
        public string ProfileId { get; set; } = string.Empty;
        public string CatalogId { get; set; } = string.Empty;
        public List<ResourceMappingEntry> Entries { get; set; } = new List<ResourceMappingEntry>();
        public List<ResourceMappingBundleRule> BundleRules { get; set; } = new List<ResourceMappingBundleRule>();
        public List<ResourceMappingCatalogMount> CatalogMounts { get; set; } = new List<ResourceMappingCatalogMount>();
        public List<ResourceMappingPreloadGroup> PreloadGroups { get; set; } = new List<ResourceMappingPreloadGroup>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class ResourceMappingProducerReceipt
    {
        public string ProducerId { get; set; } = string.Empty;
        public string ProducerContractVersion { get; set; } = string.Empty;
        public string SourceRecordFingerprint { get; set; } = string.Empty;
    }

    public sealed class ResourceMappingResourceKey
    {
        public string Id { get; set; } = string.Empty;
        public string TypeId { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;

        /// <remarks>LowFreqAlloc. Returns a deterministic diagnostic identity string.</remarks>
        public string ToIdentityString()
        {
            return (PackageId ?? string.Empty) + "|" + (Id ?? string.Empty) + "|" +
                   (TypeId ?? string.Empty) + "|" + (Variant ?? string.Empty);
        }
    }

    public sealed class ResourceMappingSourceLocator
    {
        public ResourceMappingSourceLocatorKind Kind { get; set; }
        public string UnityGuid { get; set; } = string.Empty;
        public string CachedPath { get; set; } = string.Empty;
        public string GeneratedAssetId { get; set; } = string.Empty;
        public string DomainSourceId { get; set; } = string.Empty;
        public string PackageRelativePath { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>();
    }

    public sealed class ResourceMappingBundleIntent
    {
        public string RuleId { get; set; } = string.Empty;
    }

    public sealed class ResourceMappingExternalCatalogBinding
    {
        public string CatalogMountId { get; set; } = string.Empty;
    }

    public sealed class ResourceMappingEntry
    {
        public ResourceMappingProducerReceipt ProducerReceipt { get; set; } = new ResourceMappingProducerReceipt();
        public string AuthoringRecordId { get; set; } = string.Empty;
        public ResourceMappingResourceKey ResourceKey { get; set; }
        public ResourceMappingSourceLocator SourceLocator { get; set; }
        public ResourceMappingDeliveryIntent DeliveryIntent { get; set; }
        public ResourceMappingOverrideIntent? OverrideIntent { get; set; }
        public ResourceMappingExternalCatalogBinding ExternalCatalogBinding { get; set; }
        public ResourceMappingBundleIntent BundleIntent { get; set; }
        public List<string> Labels { get; set; } = new List<string>();
        public List<string> PreloadGroups { get; set; } = new List<string>();
        public List<ResourceMappingResourceKey> Dependencies { get; set; } = new List<ResourceMappingResourceKey>();
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>();
    }

    public sealed class ResourceMappingBundleRule
    {
        public ResourceMappingProducerReceipt ProducerReceipt { get; set; } = new ResourceMappingProducerReceipt();
        public string AuthoringRecordId { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string BundleName { get; set; } = string.Empty;
        public string Compression { get; set; } = "lz4";
        public string BuildTarget { get; set; } = "ActiveBuildTarget";
        public bool IncludeDependencies { get; set; } = true;
        public bool AllowEmpty { get; set; }
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>();
    }

    public sealed class ResourceMappingCatalogMount
    {
        public ResourceMappingProducerReceipt ProducerReceipt { get; set; } = new ResourceMappingProducerReceipt();
        public string AuthoringRecordId { get; set; } = string.Empty;
        public string CatalogMountId { get; set; } = string.Empty;
        public string CatalogId { get; set; } = string.Empty;
        public ResourceMappingCatalogBootstrapKind BootstrapKind { get; set; }
        public string RelativeCatalogPath { get; set; } = string.Empty;
        public int MountOrder { get; set; }
        public string ExpectedCatalogFingerprint { get; set; } = string.Empty;
    }

    public sealed class ResourceMappingPreloadGroup
    {
        public ResourceMappingProducerReceipt ProducerReceipt { get; set; } = new ResourceMappingProducerReceipt();
        public string AuthoringRecordId { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public List<ResourceMappingResourceKey> ExplicitKeys { get; set; } = new List<ResourceMappingResourceKey>();
        public List<string> Labels { get; set; } = new List<string>();
        public bool FailFast { get; set; } = true;
        public int MaxConcurrentLoads { get; set; } = 4;
        public bool AllowEmpty { get; set; }
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>();
    }

    public sealed class ResourceMappingProducerSnapshot
    {
        public string ProducerId { get; set; } = string.Empty;
        public List<ResourceMappingEntry> Entries { get; set; } = new List<ResourceMappingEntry>();
        public List<ResourceMappingBundleRule> BundleRules { get; set; } = new List<ResourceMappingBundleRule>();
        public List<ResourceMappingCatalogMount> CatalogMounts { get; set; } = new List<ResourceMappingCatalogMount>();
        public List<ResourceMappingPreloadGroup> PreloadGroups { get; set; } = new List<ResourceMappingPreloadGroup>();
    }
}
