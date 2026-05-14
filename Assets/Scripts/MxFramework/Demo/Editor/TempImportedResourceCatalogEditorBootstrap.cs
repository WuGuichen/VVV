using MxFramework.Resources;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Demo
{
    public static class TempImportedResourceCatalogEditorBootstrap
    {
        public static MemoryResourceProvider CreateMemoryProvider(ResourceCatalog catalog = null)
        {
            return TempImportedResourceCatalog.CreateMemoryProvider(
                catalog ?? TempImportedResourceCatalog.CreateCatalog(),
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>);
        }
    }
}
