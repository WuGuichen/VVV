using System;
using System.IO;
using UnityEditor;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed class WGameGeneratedModelMaterialImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            string normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/MxFrameworkGenerated/", StringComparison.OrdinalIgnoreCase))
                return;

            if (assetImporter is ModelImporter importer)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                int resources = normalized.IndexOf("/resources/", StringComparison.Ordinal);
                if (resources > 0 && File.Exists(normalized.Substring(0, resources) + "/config/character_baked_surface_binding.json"))
                {
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.importTangents = ModelImporterTangents.Import;
                    importer.generateSecondaryUV = false;
                    importer.meshCompression = ModelImporterMeshCompression.Off;
                }
            }
        }
    }
}
