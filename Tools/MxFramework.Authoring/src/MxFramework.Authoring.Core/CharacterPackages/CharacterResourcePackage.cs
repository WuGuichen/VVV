namespace MxFramework.Authoring
{
    public sealed class CharacterResourcePackage
    {
        public CharacterPackageManifest Manifest { get; set; } = new CharacterPackageManifest();
        public CharacterPackageResourceCatalog ResourceCatalog { get; set; } = new CharacterPackageResourceCatalog();
        public CharacterAuthoringGeometry Geometry { get; set; } = new CharacterAuthoringGeometry();
        public CharacterAuthoringValidationReport ValidationReport { get; set; } = new CharacterAuthoringValidationReport();
    }
}
