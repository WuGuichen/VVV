using MxFramework.Authoring.FairyGuiArtSlices;

namespace MxFramework.NoEngineTests.FairyGUI.Manifest
{
    internal static class FairyGuiArtSliceCatalogTool
    {
        public const string CharacterTestPackageId = FairyGuiArtSliceCatalogService.CharacterTestPackageId;
        public const string CharacterTestPackageSourcePath = FairyGuiArtSliceCatalogService.CharacterTestPackageSourcePath;
        public const string CharacterTestCatalogPath = FairyGuiArtSliceCatalogService.CharacterTestCatalogPath;

        public static FairyGuiArtSliceValidationResult ValidateCharacterTest(string repositoryRoot)
        {
            return FairyGuiArtSliceCatalogService.ValidateCharacterTest(repositoryRoot);
        }

        public static FairyGuiArtSliceValidationResult ValidatePackage(string repositoryRoot, string catalogPath)
        {
            return FairyGuiArtSliceCatalogService.ValidatePackage(repositoryRoot, catalogPath);
        }

        public static string GenerateMarkdownReport(FairyGuiArtSliceValidationResult result)
        {
            return FairyGuiArtSliceCatalogService.GenerateMarkdownReport(result);
        }

        public static string GenerateMarkdownSummary(FairyGuiArtSliceValidationResult result)
        {
            return FairyGuiArtSliceCatalogService.GenerateMarkdownSummary(result);
        }

        public static string GenerateJsonReport(FairyGuiArtSliceValidationResult result)
        {
            return FairyGuiArtSliceCatalogService.GenerateJsonReport(result);
        }
    }
}
