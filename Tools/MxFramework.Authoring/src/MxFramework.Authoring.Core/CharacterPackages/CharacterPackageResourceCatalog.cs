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

    public sealed class CharacterPackageImportHint
    {
        public string TargetPathPolicy { get; set; } = string.Empty;
        public string TargetRelativePath { get; set; } = string.Empty;
        public float Scale { get; set; } = 1f;
        public string MaterialPolicy { get; set; } = string.Empty;
        public string AnimationPolicy { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public List<string> Labels { get; set; } = new List<string>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class CharacterPackageResourceDependency
    {
        public string ResourceKey { get; set; } = string.Empty;
        public bool Required { get; set; } = true;
    }

    public sealed class CharacterPackageResourceEntry
    {
        public string ResourceKey { get; set; } = string.Empty;
        public string TypeId { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public CharacterPackageImportHint ImportHints { get; set; } = new CharacterPackageImportHint();
        public List<CharacterPackageResourceDependency> Dependencies { get; set; } = new List<CharacterPackageResourceDependency>();
        public List<string> Tags { get; set; } = new List<string>();
    }

    public sealed class CharacterPackageResourceCatalog
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<CharacterPackageResourceEntry> Entries { get; set; } = new List<CharacterPackageResourceEntry>();
    }
}
