using MxFramework.Resources;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Demo
{
    [InitializeOnLoad]
    public static class TempImportedResourceCatalogEditorBootstrap
    {
        static TempImportedResourceCatalogEditorBootstrap()
        {
            RuntimeVerticalSliceSampleResourceTest.AssetPathLoader = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>;
        }

        public static MemoryResourceProvider CreateMemoryProvider(ResourceCatalog catalog = null)
        {
            return TempImportedResourceCatalog.CreateMemoryProvider(
                catalog ?? TempImportedResourceCatalog.CreateCatalog(),
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>);
        }
    }
}
