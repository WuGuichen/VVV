using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public static class AuthoringResourceProviderIds
    {
        public const string UnityAssetDatabase = "unityAssetDatabase";
        public const string RuntimeCatalog = "runtimeCatalog";
        public const string CharacterPackage = "characterPackage";
        public const string Fmod = "fmod";
        public const string ExternalImportStaging = "externalImportStaging";
        public const string GeneratedAssets = "generatedAssets";
    }

    public enum AuthoringResourceSourceKind
    {
        Unknown = 0,
        ExternalFile = 1,
        UnityAsset = 2,
        RuntimeCatalogAsset = 3,
        PackageResource = 4,
        FmodLibrary = 5,
        GeneratedAsset = 6
    }

    public enum AuthoringResourceBindingKind
    {
        None = 0,
        UnityAsset = 1,
        PackageResource = 2,
        ResourceManagerAsset = 3,
        UnityEditorOnlyAsset = 4,
        ExternalSource = 5,
        AudioEventDefinition = 6,
        AudioCue = 7,
        GeneratedPreviewOnly = 8
    }

    public enum AuthoringResourceImportStatus
    {
        New = 0,
        Clean = 1,
        SourceChanged = 2,
        UnityMissing = 3,
        ImportFailed = 4,
        Conflict = 5,
        ManualOverride = 6,
        OrphanCandidate = 7,
        ProviderUnavailable = 8
    }

    public enum AuthoringResourceRuntimeAvailability
    {
        Unknown = 0,
        RuntimeReady = 1,
        RuntimeMissing = 2,
        EditorOnly = 3,
        PreviewOnly = 4,
        AudioCueOnly = 5,
        NotRuntimeLoadable = 6
    }

    public static class AuthoringResourceBindingKeyKinds
    {
        public const string PackageResourceKey = "packageResourceKey";
        public const string PackageRelativePath = "packageRelativePath";
        public const string RuntimeResourceKey = "runtimeResourceKey";
        public const string UnityGuid = "unityGuid";
        public const string UnityAssetPath = "unityAssetPath";
        public const string FmodEventPath = "fmodEventPath";
        public const string FmodEventGuid = "fmodEventGuid";
        public const string ExternalSourcePath = "externalSourcePath";
        public const string Address = "address";
        public const string Hash = "hash";
        public const string Dependency = "dependency";
    }

    public static class AuthoringResourceDiagnosticCodes
    {
        public const string ItemMissing = "AUTH_RES_ITEM_MISSING";
        public const string StableIdDuplicate = "AUTH_RES_STABLE_ID_DUPLICATE";
        public const string ResourceKeyDuplicate = "AUTH_RES_RESOURCE_KEY_DUPLICATE";
        public const string KindUsageMismatch = "AUTH_RES_KIND_USAGE_MISMATCH";
        public const string SourceFileMissing = "AUTH_RES_SOURCE_FILE_MISSING";
        public const string HashMismatch = "AUTH_RES_HASH_MISMATCH";
        public const string UnsupportedFormat = "AUTH_RES_UNSUPPORTED_FORMAT";
        public const string IgnoredImportFile = "AUTH_RES_IMPORT_IGNORED_FILE";
        public const string SourceHashDuplicate = "AUTH_RES_SOURCE_HASH_DUPLICATE";
        public const string SourceFileTooLarge = "AUTH_RES_SOURCE_FILE_TOO_LARGE";
        public const string UnityAssetMissing = "AUTH_RES_UNITY_ASSET_MISSING";
        public const string ProviderUnavailable = "AUTH_RES_PROVIDER_UNAVAILABLE";
        public const string NotRuntimeLoadable = "AUTH_RES_NOT_RUNTIME_LOADABLE";
        public const string EditorOnlySelectedForRuntime = "AUTH_RES_EDITOR_ONLY_SELECTED_FOR_RUNTIME";
        public const string FmodUnavailable = "AUTH_RES_FMOD_UNAVAILABLE";
        public const string FmodSnapshotStale = "AUTH_RES_FMOD_SNAPSHOT_STALE";
        public const string FmodEventMissing = "AUTH_RES_FMOD_EVENT_MISSING";
        public const string FmodGuidPathMismatch = "AUTH_RES_FMOD_GUID_PATH_MISMATCH";
        public const string FmodBankMissing = "AUTH_RES_FMOD_BANK_MISSING";
        public const string FmodParameterMismatch = "AUTH_RES_FMOD_PARAMETER_MISMATCH";
        public const string CompatibilitySkeletonMismatch = "AUTH_RES_COMPAT_SKELETON_MISMATCH";
        public const string CompatibilitySlotMismatch = "AUTH_RES_COMPAT_SLOT_MISMATCH";
        public const string OrphanCandidate = "AUTH_RES_ORPHAN_CANDIDATE";
        public const string ReferenceBroken = "AUTH_RES_REFERENCE_BROKEN";
        public const string PlanRequiredResourceMissing = "AUTH_RES_PLAN_REQUIRED_RESOURCE_MISSING";
    }

    public sealed class AuthoringResourceProviderDescriptor
    {
        public string ProviderId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public AuthoringResourceSourceKind SourceKind { get; set; } = AuthoringResourceSourceKind.Unknown;
        public bool Available { get; set; } = true;
        public string Status { get; set; } = string.Empty;
        public string DiagnosticCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class AuthoringResourceProviderContext
    {
        public string ScopeId { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string PackagePath { get; set; } = string.Empty;
        public string ProjectRootPath { get; set; } = string.Empty;
        public CharacterPackageResourceCatalog PackageResourceCatalog { get; set; }
        public AuthoringUnityResourceCatalogDocument UnityResourceCatalog { get; set; }
        public RuntimeResourceCatalogDocument RuntimeResourceCatalog { get; set; }
        public AuthoringFmodAudioLibrarySnapshotDocument FmodAudioLibrarySnapshot { get; set; }
        public string UnityResourceCatalogPath { get; set; } = string.Empty;
        public string RuntimeResourceCatalogPath { get; set; } = string.Empty;
        public string FmodAudioLibrarySnapshotPath { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public interface IAuthoringResourceProvider
    {
        string ProviderId { get; }
        AuthoringResourceProviderDescriptor Describe(AuthoringResourceProviderContext context);
        AuthoringResourceCollection BuildResourceCollection(AuthoringResourceProviderContext context);
    }

    public sealed class AuthoringResourceProviderBinding
    {
        public string ProviderId { get; set; } = string.Empty;
        public AuthoringResourceBindingKind BindingKind { get; set; } = AuthoringResourceBindingKind.None;
        public string BindingKeyKind { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public string ProviderResourceKey { get; set; } = string.Empty;
        public string RuntimeResourceKey { get; set; } = string.Empty;
        public string PackageResourceKey { get; set; } = string.Empty;
        public string UnityGuid { get; set; } = string.Empty;
        public string UnityAssetPath { get; set; } = string.Empty;
        public string FmodEventPath { get; set; } = string.Empty;
        public string FmodEventGuid { get; set; } = string.Empty;
        public string ExternalSourcePath { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AuthoringResourceCompatibility
    {
        public string SkeletonStableId { get; set; } = string.Empty;
        public string AvatarStableId { get; set; } = string.Empty;
        public string BodyKind { get; set; } = string.Empty;
        public string SlotId { get; set; } = string.Empty;
        public string WeaponClass { get; set; } = string.Empty;
        public string CoordinateConvention { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AuthoringResourcePreview
    {
        public string ThumbnailResourceId { get; set; } = string.Empty;
        public string ThumbnailProviderResourceKey { get; set; } = string.Empty;
        public string PreviewMeshResourceId { get; set; } = string.Empty;
        public string PreviewMeshProviderResourceKey { get; set; } = string.Empty;
        public string PreviewCameraPresetId { get; set; } = string.Empty;
        public string PreviewPoseId { get; set; } = string.Empty;
        public bool IsPlaceholder { get; set; }
    }

    public sealed class AuthoringResourceDiagnostic
    {
        public CharacterAuthoringValidationSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceStableId { get; set; } = string.Empty;
        public string RuntimeResourceKey { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string SourceConfigKind { get; set; } = string.Empty;
        public string SourceStableId { get; set; } = string.Empty;
        public string SourceField { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SuggestedFix { get; set; } = string.Empty;
    }

    public sealed class AuthoringResourceItem
    {
        public string ResourceId { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
        public string SourceProviderId { get; set; } = string.Empty;
        public AuthoringResourceSourceKind SourceKind { get; set; } = AuthoringResourceSourceKind.Unknown;
        public AuthoringResourceBindingKind BindingKind { get; set; } = AuthoringResourceBindingKind.None;
        public AuthoringResourceImportStatus ImportStatus { get; set; } = AuthoringResourceImportStatus.New;
        public AuthoringResourceRuntimeAvailability RuntimeAvailability { get; set; } = AuthoringResourceRuntimeAvailability.Unknown;
        public AuthoringResourceCompatibility Compatibility { get; set; } = new AuthoringResourceCompatibility();
        public AuthoringResourcePreview Preview { get; set; } = new AuthoringResourcePreview();
        public List<AuthoringResourceProviderBinding> ProviderBindings { get; set; } = new List<AuthoringResourceProviderBinding>();
        public List<string> Tags { get; set; } = new List<string>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
    }

    public sealed class AuthoringResourceCollection
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string ScopeId { get; set; } = string.Empty;
        public List<AuthoringResourceProviderDescriptor> Providers { get; set; } = new List<AuthoringResourceProviderDescriptor>();
        public List<AuthoringResourceItem> Items { get; set; } = new List<AuthoringResourceItem>();
        public AuthoringResourceReferenceGraph ReferenceGraph { get; set; } = new AuthoringResourceReferenceGraph();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
    }
}
