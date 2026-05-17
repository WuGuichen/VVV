using MxFramework.Editor;
using UnityEditor;

namespace MxFramework.Demo
{
    public static class TempImportedResourceCatalogGenerator
    {
        public const string CatalogPath = SampleResourceCatalogBuilder.CatalogPath;
        private const string MenuPath = "MxFramework/Samples/Generate Resource Catalog";

        [MenuItem(MenuPath, priority = 122)]
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
