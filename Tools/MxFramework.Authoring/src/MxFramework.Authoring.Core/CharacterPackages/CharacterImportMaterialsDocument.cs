using System.Collections.Generic;

namespace MxFramework.Authoring
{
    /// <summary>
    /// Compatibility view for raw character material imports.
    /// </summary>
    /// <remarks>LowFreqAlloc. JSON DTO used by CharacterStudio and Authoring CLI, not by runtime loading.</remarks>
    public sealed class CharacterImportMaterialsDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string SourceRoot { get; set; } = string.Empty;
        public List<CharacterImportMaterialItem> Items { get; set; } = new List<CharacterImportMaterialItem>();
    }

    /// <summary>
    /// One raw source file expected by a character package import checklist.
    /// </summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO.</remarks>
    public sealed class CharacterImportMaterialItem
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public CharacterSourceReference Source { get; set; }
        public CharacterSourceTarget Target { get; set; }
        public string PresetId { get; set; } = string.Empty;
        public string Slot { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
        public string BindTarget { get; set; } = string.Empty;
        public string TextureSemantic { get; set; } = CharacterTextureSemanticIds.Unspecified;
        public bool Required { get; set; } = true;
        public string LastImportedHash { get; set; } = string.Empty;
        public string LastImportedUtc { get; set; } = string.Empty;
    }

    /// <summary>
    /// Human-authored source manifest stored beside the character art source files.
    /// </summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO.</remarks>
    public sealed class CharacterSourceManifestDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string CharacterId { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string SourceRoot { get; set; } = ".";
        public List<CharacterSourceManifestItem> Items { get; set; } = new List<CharacterSourceManifestItem>();
    }

    /// <summary>
    /// One source input or explicit external resource reference for a character.
    /// </summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO.</remarks>
    public sealed class CharacterSourceManifestItem
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string PresetId { get; set; } = string.Empty;
        public string Slot { get; set; } = string.Empty;
        public string BindTarget { get; set; } = string.Empty;
        public bool Required { get; set; } = true;
        public CharacterSourceReference Source { get; set; } = new CharacterSourceReference();
        public CharacterSourceTarget Target { get; set; } = new CharacterSourceTarget();
    }

    /// <summary>
    /// Source reference for a character source manifest item.
    /// </summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO.</remarks>
    public sealed class CharacterSourceReference
    {
        public string Kind { get; set; } = "file";
        public string Scope { get; set; } = "package";
        public string Path { get; set; } = string.Empty;
        public AuthoringResourceSelectionRef Selection { get; set; }
    }

    /// <summary>
    /// Target binding metadata for a character source manifest item.
    /// </summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO.</remarks>
    public sealed class CharacterSourceTarget
    {
        public string ResourceKey { get; set; } = string.Empty;
        public string ResourceKind { get; set; } = string.Empty;
        public string ResourceUsage { get; set; } = string.Empty;
        public string BindTarget { get; set; } = string.Empty;
        public string Slot { get; set; } = string.Empty;
        public string TextureSemantic { get; set; } = CharacterTextureSemanticIds.Unspecified;
        public string MaterialSurfaceMode { get; set; } = CharacterMaterialSurfaceModeIds.Unspecified;
        public float AlphaCutoff { get; set; } = 0.5f;
    }

    /// <summary>
    /// Tool-owned import state for a character source manifest.
    /// </summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO.</remarks>
    public sealed class CharacterImportMaterialsStateDocument
    {
        public string SchemaVersion { get; set; } = "2.0";
        public string SourceManifestRelative { get; set; } = string.Empty;
        public List<CharacterImportMaterialItemState> Items { get; set; } = new List<CharacterImportMaterialItemState>();
        public CharacterImportMaterialsIntegrityReceipt IntegrityReceipt { get; set; } = new CharacterImportMaterialsIntegrityReceipt();
    }

    /// <summary>
    /// Tool-owned import state for one source manifest item.
    /// </summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO.</remarks>
    public sealed class CharacterImportMaterialItemState
    {
        public string Id { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
        public string LastImportedHash { get; set; } = string.Empty;
        public string LastImportedUtc { get; set; } = string.Empty;
    }

    /// <summary>
    /// Persistent evidence that raw character files form a usable model-material-texture graph.
    /// </summary>
    /// <remarks>LowFreqAlloc. Authoring-only receipt; Unity renderer-slot proof remains in the Unity import receipt.</remarks>
    public sealed class CharacterImportMaterialsIntegrityReceipt
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string Status { get; set; } = "NotApplicable";
        public string PackageId { get; set; } = string.Empty;
        public string SourceManifestRelative { get; set; } = string.Empty;
        public string EvaluatedUtc { get; set; } = string.Empty;
        public int SourceItemCount { get; set; }
        public int TextureCount { get; set; }
        public int ExpectedMaterialCount { get; set; }
        public int BoundMaterialCount { get; set; }
        public int RendererBindingCount { get; set; }
        public int BlockingIssueCount { get; set; }
        public int WarningCount { get; set; }
        public List<CharacterImportMaterialsIntegrityDiagnostic> Diagnostics { get; set; } = new List<CharacterImportMaterialsIntegrityDiagnostic>();
        public List<CharacterImportMaterialBindingReceipt> Materials { get; set; } = new List<CharacterImportMaterialBindingReceipt>();
    }

    /// <summary>
    /// One structured raw-material import diagnostic.
    /// </summary>
    /// <remarks>LowFreqAlloc. Codes are stable API values consumed by CharacterStudio.</remarks>
    public sealed class CharacterImportMaterialsIntegrityDiagnostic
    {
        public string Code { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info";
        public string ItemId { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SuggestedAction { get; set; } = string.Empty;
    }

    /// <summary>
    /// Material-level portion of the raw-material import receipt.
    /// </summary>
    /// <remarks>LowFreqAlloc. SourceMaterialName is the pre-Unity binding authority.</remarks>
    public sealed class CharacterImportMaterialBindingReceipt
    {
        public string Slot { get; set; } = string.Empty;
        public string SourceMaterialName { get; set; } = string.Empty;
        public string MaterialResourceKey { get; set; } = string.Empty;
        public string DescriptorRelativePath { get; set; } = string.Empty;
        public string ColorTextureResourceKey { get; set; } = string.Empty;
        public string NormalTextureResourceKey { get; set; } = string.Empty;
        public string MaskTextureResourceKey { get; set; } = string.Empty;
        public string SurfaceMode { get; set; } = CharacterMaterialSurfaceModeIds.Opaque;
        public float AlphaCutoff { get; set; } = 0.5f;
        public int DeclaredTextureCount { get; set; }
        public int ResolvedTextureCount { get; set; }
        public bool ModelDependencyDeclared { get; set; }
        public bool SourceMaterialBindingDeclared { get; set; }
    }
}
