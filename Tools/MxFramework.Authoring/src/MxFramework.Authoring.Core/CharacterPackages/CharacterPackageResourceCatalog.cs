using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public static class CharacterPackageResourceTypeIds
    {
        public const string Model = "model";
        public const string Texture = "texture";
        public const string Material = "material";
        public const string Animation = "animation";
        public const string Audio = "audio";
        public const string Vfx = "vfx";
        public const string Preview = "preview";
        public const string Config = "config";
        public const string Geometry = "geometry";
    }

    public static class CharacterPackageResourceFormatIds
    {
        public const string Gltf = "gltf";
        public const string Glb = "glb";
        public const string Fbx = "fbx";
        public const string Png = "png";
        public const string Jpeg = "jpeg";
        public const string Jpg = "jpg";
        public const string Tga = "tga";
        public const string Json = "json";
        public const string MaterialJson = "materialJson";
        public const string AnimationGroupJson = "animationGroupJson";
        public const string Wav = "wav";
        public const string Ogg = "ogg";
        public const string VfxJson = "vfxJson";
    }

    public static class CharacterPackageResourceUsageIds
    {
        public const string CharacterModel = "characterModel";
        public const string WeaponModel = "weaponModel";
        public const string Texture = "texture";
        public const string Material = "material";
        public const string AnimationClipGroup = "animationClipGroup";
        public const string AudioCue = "audioCue";
        public const string VfxCue = "vfxCue";
        public const string PreviewThumbnail = "previewThumbnail";
        public const string PreviewMesh = "previewMesh";
        public const string CharacterConfig = "characterConfig";
        public const string GeometryAuthoring = "geometryAuthoring";
    }

    public static class CharacterPackageImportTargetPathPolicies
    {
        public const string GeneratedCharacterPackage = "generatedCharacterPackage";
        public const string PackageRelativeMirror = "packageRelativeMirror";
        public const string ProjectResourceCatalogOnly = "projectResourceCatalogOnly";
    }

    public static class CharacterPackageConflictActionIds
    {
        public const string SkipWhenHashUnchanged = "skipWhenHashUnchanged";
        public const string ReportWhenHashChanged = "reportWhenHashChanged";
        public const string RequireExplicitUpgrade = "requireExplicitUpgrade";
        public const string CreateVariant = "createVariant";
    }

    public sealed class CharacterPackageResourceHashes
    {
        public string Algorithm { get; set; } = "sha256";
        public string ContentHash { get; set; } = string.Empty;
        public string ImportHash { get; set; } = string.Empty;
        public string DependencyHash { get; set; } = string.Empty;
    }

    public sealed class CharacterPackageResourceProvenance
    {
        public string SourceTool { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
        public string AuthoringSchemaVersion { get; set; } = string.Empty;
        public string License { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string ModifiedBy { get; set; } = string.Empty;
        public string CreatedUtc { get; set; } = string.Empty;
        public string ModifiedUtc { get; set; } = string.Empty;
    }

    public sealed class CharacterPackagePreviewMetadata
    {
        public string ThumbnailResourceKey { get; set; } = string.Empty;
        public string PreviewMeshResourceKey { get; set; } = string.Empty;
        public string PlaceholderResourceKey { get; set; } = string.Empty;
        public string PreviewCameraPresetId { get; set; } = string.Empty;
        public bool IsPlaceholder { get; set; }
    }

    public sealed class CharacterPackageConflictPolicy
    {
        public string SameStableIdAction { get; set; } = CharacterPackageConflictActionIds.ReportWhenHashChanged;
        public string HashUnchangedAction { get; set; } = CharacterPackageConflictActionIds.SkipWhenHashUnchanged;
        public string HashChangedAction { get; set; } = CharacterPackageConflictActionIds.ReportWhenHashChanged;
        public bool AllowOverwrite { get; set; }
    }

    public sealed class CharacterPackageImportHint
    {
        public string TargetPathPolicy { get; set; } = string.Empty;
        public string TargetRelativePath { get; set; } = string.Empty;
        public float Scale { get; set; } = 1f;
        public string MaterialPolicy { get; set; } = string.Empty;
        public string AnimationPolicy { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string UpAxis { get; set; } = string.Empty;
        public string ForwardAxis { get; set; } = string.Empty;
        public string CollisionPolicy { get; set; } = string.Empty;
        public string PhysicsDataPolicy { get; set; } = string.Empty;
        public List<string> Labels { get; set; } = new List<string>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class CharacterPackageResourceDependency
    {
        public string ResourceKey { get; set; } = string.Empty;
        public bool Required { get; set; } = true;
        public string Relation { get; set; } = "uses";
        public bool AffectsDependencyHash { get; set; } = true;
    }

    public sealed class CharacterPackageResourceEntry
    {
        public string ResourceKey { get; set; } = string.Empty;
        public string LocalId { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string TypeId { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
        public string SourceFormat { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public CharacterPackageResourceHashes Hashes { get; set; } = new CharacterPackageResourceHashes();
        public CharacterPackageImportHint ImportHints { get; set; } = new CharacterPackageImportHint();
        public List<CharacterPackageResourceDependency> Dependencies { get; set; } = new List<CharacterPackageResourceDependency>();
        public List<string> Tags { get; set; } = new List<string>();
        public CharacterPackageConflictPolicy ConflictPolicy { get; set; } = new CharacterPackageConflictPolicy();
        public CharacterPackagePreviewMetadata Preview { get; set; } = new CharacterPackagePreviewMetadata();
        public CharacterPackageResourceProvenance Provenance { get; set; } = new CharacterPackageResourceProvenance();
    }

    public sealed class CharacterPackageResourceCatalog
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<CharacterPackageResourceEntry> Entries { get; set; } = new List<CharacterPackageResourceEntry>();
    }
}
