using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class GlobalResourceBuildProfile
    {
        public int SchemaVersion { get; set; } = 1;
        public string ProfileId { get; set; } = string.Empty;
        public string CatalogId { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public List<GlobalResourceBuildProfileEntry> Entries { get; set; } = new List<GlobalResourceBuildProfileEntry>();
        public List<GlobalResourceBuildProfileBundleRule> BundleRules { get; set; } = new List<GlobalResourceBuildProfileBundleRule>();
        public List<GlobalResourceBuildProfilePreloadGroup> PreloadGroups { get; set; } = new List<GlobalResourceBuildProfilePreloadGroup>();
        public List<GlobalResourceBuildProfileResourceKey> RequiredDomainPlanKeys { get; set; } = new List<GlobalResourceBuildProfileResourceKey>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class GlobalResourceBuildProfileEntry
    {
        public GlobalResourceBuildProfileResourceKey ResourceKey { get; set; } = new GlobalResourceBuildProfileResourceKey();
        public GlobalResourceBuildProfileEntrySource Source { get; set; } = new GlobalResourceBuildProfileEntrySource();
        public List<string> Labels { get; set; } = new List<string>();
        public string BundleRule { get; set; } = string.Empty;
        public string DeliveryMode { get; set; } = GlobalResourceBuildProfileDeliveryModes.Internal;
        public string BundleOverrideMode { get; set; } = GlobalResourceBuildProfileBundleOverrideModes.None;
        public string BundleOverrideValue { get; set; } = string.Empty;
        public string BundleGroupHint { get; set; } = string.Empty;
        public List<string> PreloadGroups { get; set; } = new List<string>();
        public List<GlobalResourceBuildProfileResourceKey> Dependencies { get; set; } = new List<GlobalResourceBuildProfileResourceKey>();
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>();
        public bool RuntimeLoadable { get; set; } = true;
        public bool EditorOnly { get; set; }
    }

    public sealed class GlobalResourceBuildProfileResourceKey
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string TypeId { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
    }

    public sealed class GlobalResourceBuildProfileEntrySource
    {
        public string ProviderId { get; set; } = string.Empty;
        public string UnityAssetPath { get; set; } = string.Empty;
        public string UnityGuid { get; set; } = string.Empty;
        public string RuntimeCatalogId { get; set; } = string.Empty;
        public string RuntimeResourceKey { get; set; } = string.Empty;
        public string ExternalSourcePath { get; set; } = string.Empty;
        public string GeneratedAssetId { get; set; } = string.Empty;
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>();
    }

    public sealed class GlobalResourceBuildProfileBundleRule
    {
        public string Id { get; set; } = string.Empty;
        public string BundleName { get; set; } = string.Empty;
        public List<GlobalResourceBuildProfileResourceKey> ExplicitKeys { get; set; } = new List<GlobalResourceBuildProfileResourceKey>();
        public List<string> MatchLabels { get; set; } = new List<string>();
        public List<string> MatchDomains { get; set; } = new List<string>();
        public List<string> MatchPackageIds { get; set; } = new List<string>();
        public string Compression { get; set; } = "lz4";
        public string BuildTarget { get; set; } = "ActiveBuildTarget";
        public bool IncludeDependencies { get; set; } = true;
        public bool AllowEmpty { get; set; }
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>();
    }

    public sealed class GlobalResourceBuildProfilePreloadGroup
    {
        public string Id { get; set; } = string.Empty;
        public List<GlobalResourceBuildProfileResourceKey> ExplicitKeys { get; set; } = new List<GlobalResourceBuildProfileResourceKey>();
        public List<string> Labels { get; set; } = new List<string>();
        public bool FailFast { get; set; } = true;
        public int MaxConcurrentLoads { get; set; } = 4;
        public bool AllowEmpty { get; set; }
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>();
    }

    public static class GlobalResourceBuildProfileDeliveryModes
    {
        public const string Internal = "internal";
        public const string External = "external";
        public const string EditorOnly = "editorOnly";
        public const string Excluded = "excluded";
    }

    public static class GlobalResourceBuildProfileBundleOverrideModes
    {
        public const string None = "none";
        public const string ForceBundle = "forceBundle";
        public const string ForceStandalone = "forceStandalone";
        public const string ForceExternal = "forceExternal";
        public const string Exclude = "exclude";
    }
}
