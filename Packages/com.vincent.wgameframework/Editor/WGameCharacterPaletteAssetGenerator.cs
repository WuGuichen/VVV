using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class WGameCharacterPaletteAssetGenerator
    {
        private const int ColumnCount = WGameSurfacePaletteCatalog.Width;
        private const int RowCount = WGameSurfacePaletteCatalog.Height;
        private const string TextureFolder = "Assets/Art/WGame/Textures/Character";
        internal const string TexturePath = TextureFolder + "/WGameColorPaletteAtlas.png";
        internal const string ReceiptPath = TextureFolder + "/WGameColorPaletteAtlas.receipt.json";
        internal const string LegacyTexturePath = TextureFolder + "/WGameCharacterPaletteAtlas.png";
        internal const string LegacyReceiptPath = TextureFolder + "/WGameCharacterPaletteAtlas.receipt.json";
        private const string CharacterMaterialPath = "Assets/Art/WGame/Materials/Character/CharacterPaletteShared.mat";
        private const string PropMaterialPath = "Assets/Art/WGame/Materials/Props/PropPaletteShared.mat";

        [InitializeOnLoadMethod]
        private static void EnsureGeneratedAssetExists()
        {
            EnsureGeneratedAssetExists(WGameSurfacePaletteCatalog.CatalogProjectPath, TexturePath, ReceiptPath);
        }

        // Automatic discovery is optional; an explicit Generate command still validates and reports missing/invalid input.
        internal static void EnsureGeneratedAssetExists(string catalogPath, string texturePath, string receiptPath)
        {
            if (!File.Exists(catalogPath))
                return;
            if (!File.Exists(texturePath) || !File.Exists(receiptPath))
                EditorApplication.delayCall += Generate;
        }

        [MenuItem("MxFramework/WGame/Generate Color Palette + Surface Profiles")]
        public static void Generate()
        {
            WGameSurfacePaletteCatalogDocument catalog = WGameSurfacePaletteCatalog.LoadAndValidate(out string catalogIssue);
            if (catalog == null)
                throw new InvalidOperationException(catalogIssue);

            EnsureFolder(TextureFolder);
            Color32[] pixels = WGameSurfacePaletteCatalog.BuildPixels(catalog);

            var texture = new Texture2D(ColumnCount, RowCount, TextureFormat.RGB24, mipChain: false, linear: false);
            byte[] pngBytes;
            try
            {
                texture.name = "WGameColorPaletteAtlas";
                texture.SetPixels32(pixels);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                pngBytes = texture.EncodeToPNG();
                File.WriteAllBytes(TexturePath, pngBytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporter();
            Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            CreateOrUpdateMaterial(CharacterMaterialPath, "WGame/CharacterPaletteLegacy", imported);
            CreateOrUpdateMaterial(PropMaterialPath, "WGame/PropStylizedLit", imported);
            CleanupGeneratedCharacterMaterials();
            WGameSurfacePaletteCatalog.WriteReceipt(
                ReceiptPath,
                new WGameColorPaletteAtlasReceipt
                {
                    CatalogHash = catalog.CatalogHash,
                    AtlasHash = WGameSurfacePaletteCatalog.ComputeSha256(pngBytes)
                });
            AssetDatabase.ImportAsset(ReceiptPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            if (!string.Equals(LegacyTexturePath, TexturePath, StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(LegacyTexturePath);
            if (!string.Equals(LegacyReceiptPath, ReceiptPath, StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(LegacyReceiptPath);
            AssetDatabase.Refresh();
            Debug.Log("Generated 256x256 WGame RGB565 Color Palette atlas and v3 receipt from catalog "
                + catalog.CatalogHash + ".");
        }

        private static void ConfigureTextureImporter()
        {
            if (!(AssetImporter.GetAtPath(TexturePath) is TextureImporter importer))
                return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.isReadable = false;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
        }

        private static void CreateOrUpdateMaterial(string path, string shaderName, Texture2D palette)
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException("Shader was not found: " + shaderName);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.EnableKeyword("_WG_PALETTE_ON");
            material.SetFloat("_WgPaletteEnabled", 1f);
            material.SetTexture("_WgColorPaletteTex", palette);
            material.SetVector("_WgColorPaletteParams", new Vector4(256f, 256f, 1f / 256f, 1f / 256f));
            RemoveSavedMaterialProperty(material, "m_SavedProperties.m_Floats", "_WgPaletteRow");
            RemoveSavedMaterialProperty(material, "m_SavedProperties.m_TexEnvs", "_WgPaletteTex");
            RemoveSavedMaterialProperty(material, "m_SavedProperties.m_Colors", "_WgPaletteParams");
            foreach (string legacy in new[]
            {
                "_WgPaletteClassShadowFloor", "_WgPaletteClassWrap", "_WgPaletteClassAo",
                "_WgPaletteDetailScale", "_WgPaletteDetailStrength", "_WgPaletteClassSpecular",
                "_WgPaletteClassRim", "_WgPaletteClassPower"
            })
            {
                RemoveSavedMaterialProperty(material, "m_SavedProperties.m_Colors", legacy);
            }
            if (string.Equals(shaderName, "WGame/CharacterPaletteLegacy", StringComparison.Ordinal))
            {
                material.SetVector("_WgPaletteLightingParams", new Vector4(0.30f, 0.35f, 0.68f, 0.055f));
                material.SetVector("_WgPaletteValueParams", new Vector4(0.76f, 1.02f, 1.18f, 0.52f));
                material.SetColor("_WgPaletteShadowTint", new Color(0.84f, 0.88f, 0.96f, 1f));
                material.SetVector("_WgPaletteIndirectParams", new Vector4(0.27f, 0.11f, 0.08f, 0.50f));
                material.SetFloat("_WgPaletteIndirectNormalInfluence", 0.15f);
                material.SetColor("_WgPaletteEdgeWarmTint", new Color(1.00f, 0.80f, 0.52f, 1f));
                material.SetColor("_WgPaletteEdgeCoolTint", new Color(0.38f, 0.70f, 0.86f, 1f));
                material.SetVector("_WgPaletteEdgeParams", new Vector4(0.085f, 4.25f, 0.24f, 0.28f));
                material.SetVector("_WgPaletteDetailParams", new Vector4(0.65f, 3f, 12f, 1f));
                material.SetFloat("_WgPaletteRimPower", 2.6f);
            }
            if (string.Equals(shaderName, "WGame/CharacterPaletteLegacy", StringComparison.Ordinal))
            {
                RemoveTraditionalSurfaceState(material);
                RemoveLegacyFaceDetailState(material);
                material.DisableKeyword("_WG_FACE_DETAIL_ON");
                material.SetFloat("_WgFaceDetailEnabled", 0f);
                material.SetTexture("_WgFaceDetailTex", null);
                material.SetFloat("_WgFaceDetailIntensity", 1f);
            }
            EditorUtility.SetDirty(material);
        }

        private static void CleanupGeneratedCharacterMaterials()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/MxFrameworkGenerated/CharacterPackages" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null
                    || !string.Equals(material.shader.name, "WGame/CharacterPaletteLegacy", StringComparison.Ordinal)
                    || (!material.IsKeywordEnabled("_WG_PALETTE_ON")
                        && (!material.HasProperty("_WgPaletteEnabled") || material.GetFloat("_WgPaletteEnabled") <= 0.5f)))
                    continue;
                RemoveTraditionalSurfaceState(material);
                RemoveLegacyFaceDetailState(material);
                material.SetTexture("_WgFaceDetailTex", null);
                EditorUtility.SetDirty(material);
            }
        }

        private static void RemoveLegacyFaceDetailState(Material material)
        {
            material.DisableKeyword("_WG_FACE_DETAIL_COLOR_ON");
            RemoveSavedMaterialProperty(material, "m_SavedProperties.m_Floats", "_WgFaceDetailColorEnabled");
            RemoveSavedMaterialProperty(material, "m_SavedProperties.m_Floats", "_WgFacePreset");
            RemoveSavedMaterialProperty(material, "m_SavedProperties.m_Colors", "_WgFaceDetailParams");
            RemoveSavedMaterialProperty(material, "m_SavedProperties.m_Colors", "_WgFaceFeatureColors");
            RemoveSavedMaterialProperty(material, "m_SavedProperties.m_Colors", "_WgFaceFeatureTokens");
        }

        private static void RemoveTraditionalSurfaceState(Material material)
        {
            foreach ((string arrayPath, string propertyName) in new[]
            {
                ("m_SavedProperties.m_TexEnvs", "_BaseMap"),
                ("m_SavedProperties.m_TexEnvs", "_WgNormalTex"),
                ("m_SavedProperties.m_TexEnvs", "_WgMaskTex"),
                ("m_SavedProperties.m_TexEnvs", "_WgEmissionMaskTex"),
                ("m_SavedProperties.m_Floats", "_AlphaClip"),
                ("m_SavedProperties.m_Floats", "_Cutoff")
            })
            {
                RemoveSavedMaterialProperty(material, arrayPath, propertyName);
            }
        }

        private static void RemoveSavedMaterialProperty(Material material, string arrayPath, string propertyName)
        {
            if (material == null || string.IsNullOrWhiteSpace(arrayPath) || string.IsNullOrWhiteSpace(propertyName))
                return;

            var serialized = new SerializedObject(material);
            SerializedProperty entries = serialized.FindProperty(arrayPath);
            if (entries == null || !entries.isArray)
                return;

            for (int index = entries.arraySize - 1; index >= 0; index--)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                SerializedProperty key = entry?.FindPropertyRelative("first");
                if (key == null || !string.Equals(key.stringValue, propertyName, StringComparison.Ordinal))
                    continue;

                entries.DeleteArrayElementAtIndex(index);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folder)
        {
            string normalized = (folder ?? string.Empty).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalized) || !normalized.StartsWith("Assets/", StringComparison.Ordinal))
                return;

            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
