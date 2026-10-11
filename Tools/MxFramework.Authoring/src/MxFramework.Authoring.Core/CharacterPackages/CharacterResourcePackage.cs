using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    public sealed class CharacterResourcePackage
    {
        public CharacterPackageManifest Manifest { get; set; } = new CharacterPackageManifest();
        public CharacterPackageResourceCatalog ResourceCatalog { get; set; } = new CharacterPackageResourceCatalog();
        public CharacterAuthoringGeometry Geometry { get; set; } = new CharacterAuthoringGeometry();
        public CharacterApplicationAuthoringSummary ApplicationConfig { get; set; } = new CharacterApplicationAuthoringSummary();
        public CharacterBaseAttributesAuthoring BaseAttributes { get; set; }
        public MxFramework.CharacterApplication.CharacterTacticalDefaults TacticalDefaults { get; set; }
        public AnimationAuthoringPackage AnimationAuthoring { get; set; } = new AnimationAuthoringPackage();
        public CharacterAuthoringValidationReport ValidationReport { get; set; } = new CharacterAuthoringValidationReport();

        [JsonIgnore]
        public CharacterActionSourceLibrary CharacterActionLibrary { get; set; } = new CharacterActionSourceLibrary();

        [JsonIgnore]
        public CharacterActionSourceLibrary CommonCharacterActionLibrary { get; set; } = new CharacterActionSourceLibrary();

        [JsonIgnore]
        public string PackagePath { get; set; } = string.Empty;

        [JsonIgnore]
        public string CharacterActionLibraryPath { get; set; } = string.Empty;

        [JsonIgnore]
        public string CommonCharacterActionLibraryPath { get; set; } = string.Empty;

        [JsonIgnore]
        public GlobalWeaponLibrary GlobalWeaponLibrary { get; set; }

        [JsonIgnore]
        public Dictionary<string, string> PackageConfigFileContents { get; set; } =
            new Dictionary<string, string>(System.StringComparer.Ordinal);
    }
}
