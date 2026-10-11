using System;
using Newtonsoft.Json;

namespace MxFramework.Config.Runtime
{
    /// <summary>
    /// DTO for mod.json manifest, parsed at runtime.
    /// Kept minimal — only fields relevant to runtime loading.
    /// Does not reference Authoring Core DTOs.
    /// </summary>
    public sealed class RuntimeModPackageManifest
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("packageId")]
        public string PackageId { get; set; } = string.Empty;

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("author")]
        public string Author { get; set; } = string.Empty;

        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;

        [JsonProperty("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonProperty("gameVersionRange")]
        public string GameVersionRange { get; set; } = "*";

        [JsonProperty("runtimePatch")]
        public string RuntimePatch { get; set; } = string.Empty;

        [JsonProperty("resourceCatalog")]
        public string ResourceCatalog { get; set; } = string.Empty;
    }
}
