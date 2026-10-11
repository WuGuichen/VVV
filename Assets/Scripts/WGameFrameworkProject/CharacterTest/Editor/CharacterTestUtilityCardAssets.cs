using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Rebuilds the archived independent-shell study; current preparation cards use the shared action shell.</summary>
    internal static class CharacterTestUtilityCardAssets
    {
        private const string Root = "Assets/Resources/MxFramework/UI/CardTheme/Utility";
        private static readonly string[] Names = { "UtilityShell", "UtilityGoldShell", "UtilityHolographicShell", "UtilityMomentumRecovery", "UtilityPursuitMark",
            "UtilityReturnMark", "UtilityDefensePreparation", "UtilityHurtCharge" };

        [MenuItem("MxFramework/CharacterTest/Card Shell/Archive/Regenerate Independent Utility Shell Study")]
        internal static void Generate()
        {
            foreach (string name in Names) Configure(Root + "/" + name + ".png", true, name == "UtilityShell");
            var art = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/UtilityShell.png");
            if (art == null) throw new InvalidOperationException("Approved utility shell is missing.");
            var source = art.GetPixels32(); var pixels = new Color32[source.Length];
            for (int y = 0; y < art.height; y++) for (int x = 0; x < art.width; x++)
            {
                float u = (x + .5f) / art.width, v = 1 - (y + .5f) / art.height;
                float rim = Mathf.Min(u, 1 - u) < .078f || Mathf.Min(v, 1 - v) < .045f ? 1 : 0;
                float inset = Mathf.SmoothStep(0, 1,
                    Mathf.Min(Mathf.Min(u - .12f, .88f - u), Mathf.Min(v - .19f, .59f - v)) / .035f);
                float disc = Vector2.Distance(new Vector2(u, v * 1.3968254f), new Vector2(.137f, .103f * 1.3968254f)) < .082f ? 1 : 0;
                int index = y * art.width + x;
                pixels[index] = new Color32((byte)(rim * (1 - disc) * 255), (byte)(inset * 255), (byte)(disc * 255), source[index].a);
            }
            var mask = new Texture2D(art.width, art.height, TextureFormat.RGBA32, false, true);
            try { mask.SetPixels32(pixels); mask.Apply(); File.WriteAllBytes(Root + "/UtilityRegionMask.png", mask.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(mask); }
            Configure(Root + "/UtilityRegionMask.png", false, false);
            Configure(Root + "/UtilityShell.png", true, false);
            var regions = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/UtilityRegionMask.png");
            string[] qualities = { "Silver", "Gold", "Holographic" };
            for (int quality = 0; quality < qualities.Length; quality++)
            {
                var qualityArt = quality == 0 ? art : AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Utility" + qualities[quality] + "Shell.png");
                if (qualityArt == null) throw new InvalidOperationException("Authored utility quality shell is missing: " + qualities[quality]);
                var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/MxFramework/UI/CardTheme/Authored" + qualities[quality] + ".mat");
                if (original == null) throw new InvalidOperationException("Existing authored card material is missing: " + qualities[quality]);
                string path = Root + "/Utility" + qualities[quality] + ".mat";
                var generated = new Material(original) { name = "Utility" + qualities[quality] };
                generated.mainTexture = qualityArt; generated.SetTexture("_RegionTex", regions);
                generated.SetFloat("_Quality", quality); generated.SetFloat("_UtilityCard", 1); generated.SetFloat("_RippleStrength", .18f);
                generated.SetFloat("_CardIllustrationEnabled", 0); generated.SetFloat("_PanelOpacity", 1);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = generated; AssetDatabase.CreateAsset(material, path); }
                else
                {
                    // Copy a complete serialized material so newly added shader properties survive saving.
                    try { EditorUtility.CopySerialized(generated, material); }
                    finally { UnityEngine.Object.DestroyImmediate(generated); }
                }
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            }
        }

        private static void Configure(string path, bool srgb, bool readable)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Utility texture is missing: " + path);
            importer.sRGBTexture = srgb; importer.isReadable = readable; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = srgb; importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }
}
