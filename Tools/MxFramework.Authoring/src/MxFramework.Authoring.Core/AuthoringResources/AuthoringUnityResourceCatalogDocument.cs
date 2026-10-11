using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class AuthoringUnityResourceCatalogDocument
    {
        public string Format { get; set; } = "mx.characterUnityResourceCatalog.v1";
        public int SchemaVersion { get; set; } = 1;
        public string CatalogId { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string GeneratedAtUtc { get; set; } = string.Empty;
        public List<AuthoringUnityResourceCatalogOrphan> OrphanedUnityAssets { get; set; } = new List<AuthoringUnityResourceCatalogOrphan>();
        public List<AuthoringUnityResourceCatalogEntry> Entries { get; set; } = new List<AuthoringUnityResourceCatalogEntry>();
    }

    public sealed class AuthoringUnityResourceCatalogEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PackageResourceKey { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
        public string SourceRelativePath { get; set; } = string.Empty;
        public string SourceFormat { get; set; } = string.Empty;
        public string DeclaredContentHash { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string ImportHash { get; set; } = string.Empty;
        public string DependencyHash { get; set; } = string.Empty;
        public string UnityAssetPath { get; set; } = string.Empty;
        public string UnityAssetGuid { get; set; } = string.Empty;
        public string UnityMainObjectType { get; set; } = string.Empty;
        public string ImporterKind { get; set; } = string.Empty;
        public string ImportStatus { get; set; } = string.Empty;
        public List<AuthoringUnityResourceCatalogDiagnostic> Diagnostics { get; set; } = new List<AuthoringUnityResourceCatalogDiagnostic>();
        public List<string> Labels { get; set; } = new List<string>();
        public List<AuthoringUnityResourceKey> Dependencies { get; set; } = new List<AuthoringUnityResourceKey>();
        public List<AuthoringUnityResourceCatalogSubAsset> SubAssets { get; set; } = new List<AuthoringUnityResourceCatalogSubAsset>();
        public string Hash { get; set; } = string.Empty;
        public long Size { get; set; }
        public bool AllowOverride { get; set; }
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AuthoringUnityResourceCatalogSubAsset
    {
        public string SubAssetId { get; set; } = string.Empty;
        public string SubAssetName { get; set; } = string.Empty;
        public string SubAssetType { get; set; } = string.Empty;
        public string UnitySubAssetKey { get; set; } = string.Empty;
        public string UnityLocalFileId { get; set; } = string.Empty;
        public float DurationSeconds { get; set; }
        public bool LoopTime { get; set; }
        public bool HumanMotion { get; set; }
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AuthoringUnityResourceCatalogDiagnostic
    {
        public string Severity { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
    }

    public sealed class AuthoringUnityResourceCatalogOrphan
    {
        public string UnityAssetPath { get; set; } = string.Empty;
        public string ImportStatus { get; set; } = "OrphanedUnityAsset";
        public string Message { get; set; } = string.Empty;
    }

    public sealed class AuthoringUnityResourceKey
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
    }
}
