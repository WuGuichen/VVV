using System;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    internal sealed class MxFairyGuiAtlasImportProcessor : AssetPostprocessor
    {
        private const string FairyGuiBundleRoot = "Assets/Bundles/FGUI/";

        private void OnPreprocessTexture()
        {
            if (!IsFairyGuiAtlas(assetPath))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
        }

        private static bool IsFairyGuiAtlas(string path)
        {
            if (string.IsNullOrEmpty(path)
                || !path.StartsWith(FairyGuiBundleRoot, StringComparison.Ordinal))
            {
                return false;
            }

            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            return fileName.IndexOf("_atlas", StringComparison.Ordinal) >= 0;
        }
    }
}
