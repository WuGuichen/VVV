using MxFramework.Editor;

namespace MxFramework.Demo
{
    public static class TempImportedResourceCatalogGenerator
    {
        public const string CatalogPath = SampleResourceCatalogBuilder.CatalogPath;

        public static void Generate()
        {
            SampleResourceCatalogBuilder.Generate();
        }

        public static string WriteCatalogJson(MxFramework.Resources.ResourceCatalog catalog)
        {
            return SampleResourceCatalogBuilder.WriteCatalogJson(catalog);
        }
    }
}
