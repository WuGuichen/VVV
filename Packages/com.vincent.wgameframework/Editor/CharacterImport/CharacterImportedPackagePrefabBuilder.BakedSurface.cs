using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    public static partial class CharacterImportedPackagePrefabBuilder
    {
        internal const string BakedSurfaceRelativePath = "config/character_baked_surface_binding.json";
        private const string BakedSurfaceTemplate = "wgame.character_baked_stylized_lit";

        private static bool HasBakedSurface(string root) => File.Exists(Path.Combine(root, BakedSurfaceRelativePath));

        private static JObject ReadBakedSurface(string root)
        {
            JObject d = JObject.Parse(File.ReadAllText(Path.Combine(root, BakedSurfaceRelativePath)));
            if ((string)d["format"] != "mx.characterBakedSurfaceBinding.v1" || (string)d["schemaVersion"] != "1.0"
                || (bool?)d["enabled"] != true || (string)d["uvLayout"] != "bakeUv0HairFlowUv1HairMetaUv2"
                || (string)d["surfaceDataLayout"] != "aoRimEmissionProfile16")
                throw new InvalidOperationException("Invalid baked surface binding or mesh layout.");
            int size = (int?)d["textureSize"] ?? 0;
            if (size != 512 && size != 1024 && size != 2048 && size != 4096) throw new InvalidOperationException("Baked surface size must be 512, 1024, 2048 or 4096.");
            if ((string)d["cullMode"] != "Off" && (string)d["cullMode"] != "Front" && (string)d["cullMode"] != "Back")
                throw new InvalidOperationException("Invalid baked cull mode.");
            JObject hair = d["hair"] as JObject ?? throw new InvalidOperationException("Hair settings missing.");
            foreach (string field in new[] { "primaryWidth", "primaryStrength", "secondaryWidth", "secondaryStrength", "primaryShift", "secondaryShift", "rootDarkening", "rimStrength", "aoStrength" })
            {
                float value = (float?)hair[field] ?? float.NaN;
                float minimum = field.EndsWith("Shift", StringComparison.Ordinal) ? -1f : field.EndsWith("Width", StringComparison.Ordinal) ? .02f : 0f;
                float maximum = field.EndsWith("Strength", StringComparison.Ordinal) && field != "aoStrength" ? 2f : 1f;
                if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
                    throw new InvalidOperationException("Invalid hair parameter: " + field);
            }
            return d;
        }

        private static string BakedResourcePath(string root, JObject d, string field)
        {
            string relative = (string)d[field];
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidOperationException("Missing baked resource " + field);
            string full = Path.GetFullPath(Path.Combine(root, relative));
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                throw new InvalidOperationException("Baked resource missing or outside package: " + relative);
            return NormalizeProjectPath(root) + "/" + relative.Replace('\\', '/');
        }

        private static void CheckBakedHash(string path, string expected)
        {
            if (string.IsNullOrEmpty(expected) || WGameSurfacePaletteCatalog.ComputeSha256(File.ReadAllBytes(path)) != expected)
                throw new InvalidOperationException("Baked surface hash mismatch: " + path);
        }

        internal static Color32[] BuildBakedSurfaceMip(Color32[] source, int width, int height)
        {
            int w = Math.Max(1, width / 2), h = Math.Max(1, height / 2);
            var result = new Color32[w * h];
            var counts = new int[16];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Array.Clear(counts, 0, counts.Length);
                int r = 0, g = 0, b = 0;
                for (int j = 0; j < 2; j++)
                for (int i = 0; i < 2; i++)
                {
                    Color32 c = source[Math.Min(height - 1, y * 2 + j) * width + Math.Min(width - 1, x * 2 + i)];
                    r += c.r; g += c.g; b += c.b; counts[Math.Min(15, c.a / 16)]++;
                }
                int profile = 0;
                for (int i = 1; i < 16; i++) if (counts[i] > counts[profile]) profile = i;
                result[y * w + x] = new Color32((byte)((r + 2) / 4), (byte)((g + 2) / 4), (byte)((b + 2) / 4), (byte)(8 + profile * 16));
            }
            return result;
        }

        private static Texture2D CreateBakedControlTexture(string source, string target, int size, Renderer[] renderers)
        {
            var read = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(read, File.ReadAllBytes(source), false))
                throw new InvalidOperationException("Cannot read baked SurfaceData PNG.");
            try
            {
                if (read.width != size || read.height != size) throw new InvalidOperationException("SurfaceData dimensions differ from receipt.");
                Color32[] values = read.GetPixels32();
                foreach (Color32 c in values)
                    if ((c.a & 15) != 8) throw new InvalidOperationException("SurfaceData profile is not a discrete center value.");
                NeutralizeOverlappingHairAo(values, size, renderers);
                Texture2D targetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
                var built = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
                { name = "BakedSurfaceData", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 1 };
                int width = size, height = size;
                for (int mip = 0; mip < built.mipmapCount; mip++)
                {
                    built.SetPixels32(values, mip);
                    if (width > 1 || height > 1)
                    {
                        values = BuildBakedSurfaceMip(values, width, height);
                        width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
                    }
                }
                built.Apply(false, true);
                if (targetTexture == null) { AssetDatabase.CreateAsset(built, target); targetTexture = built; }
                else { EditorUtility.CopySerialized(built, targetTexture); UnityEngine.Object.DestroyImmediate(built); EditorUtility.SetDirty(targetTexture); }
                return targetTexture;
            }
            finally { UnityEngine.Object.DestroyImmediate(read); }
        }

        private static void ValidateBakedMesh(Renderer[] renderers)
        {
            if (renderers.Length == 0) throw new InvalidOperationException("Baked body has no renderers.");
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) throw new InvalidOperationException("Baked renderer has no mesh.");
                var flow = new List<Vector2>(); var meta = new List<Vector2>();
                mesh.GetUVs(1, flow); mesh.GetUVs(2, meta);
                if (mesh.uv.Length != mesh.vertexCount || mesh.tangents.Length != mesh.vertexCount
                    || flow.Count != mesh.vertexCount || meta.Count != mesh.vertexCount)
                    throw new InvalidOperationException("Baked mesh requires UV0, hair flow UV1, hair metadata UV2 and imported tangents.");
                for (int i = 0; i < flow.Count; i++)
                    if (meta[i].y > 0.5f && (Mathf.Abs(flow[i].sqrMagnitude - 1f) > 0.03f || float.IsNaN(flow[i].x)
                        || meta[i].x < 0f || meta[i].x > 1f)) throw new InvalidOperationException("Invalid hair flow at vertex " + i);
            }
        }

        private static CharacterBodyMaterialBindingStatus ApplyBakedBodyMaterialBinding(
            string root, string packageId, Transform body)
        {
            JObject d = ReadBakedSurface(root);
            if ((string)d["packageId"] != packageId) throw new InvalidOperationException("Baked surface belongs to another character.");
            string basePath = BakedResourcePath(root, d, "baseColorFile");
            string dataPath = BakedResourcePath(root, d, "surfaceDataFile");
            string modelPath = BakedResourcePath(root, d, "modelFile");
            CheckBakedHash(basePath, (string)d["baseColorHash"]);
            CheckBakedHash(dataPath, (string)d["surfaceDataHash"]);
            CheckBakedHash(modelPath, (string)d["modelHash"]);
            var importer = AssetImporter.GetAtPath(basePath) as TextureImporter;
            if (importer == null) { AssetDatabase.ImportAsset(basePath, ImportAssetOptions.ForceSynchronousImport); importer = AssetImporter.GetAtPath(basePath) as TextureImporter; }
            if (importer == null) throw new InvalidOperationException("No BaseColor texture importer.");
            int size = (int)d["textureSize"];
            importer.sRGBTexture = true; importer.alphaIsTransparency = false; importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = size; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            string materialPath = GetGeneratedBodyMaterialPath(root, packageId);
            EnsureFolderPath(Path.GetDirectoryName(materialPath)?.Replace('\\', '/'));
            Renderer[] renderers = GetBodyMaterialRenderers(body);
            ValidateBakedMesh(renderers);
            Texture2D data = CreateBakedControlTexture(dataPath, Path.ChangeExtension(materialPath, null) + "_SurfaceData.asset", size, renderers);
            Shader shader = Shader.Find("WGame/CharacterStylizedLit") ?? throw new InvalidOperationException("Character shader is missing.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            material.shader = shader;
            RemoveObsoleteCharacterSurfaceProperties(material, baked: true);
            material.DisableKeyword("_WG_PALETTE_ON"); material.DisableKeyword("_WG_FACE_DETAIL_ON");
            material.DisableKeyword("_WG_BAKED_SURFACE_ON");
            material.SetTexture("_WgBakedBaseColor", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
            material.SetTexture("_WgSurfaceDataTex", data);
            JObject h = d["hair"] as JObject ?? throw new InvalidOperationException("Hair settings missing.");
            material.SetVector("_WgHairLobes", new Vector4((float)h["primaryWidth"], (float)h["primaryStrength"], (float)h["secondaryWidth"], (float)h["secondaryStrength"]));
            material.SetVector("_WgHairShape", new Vector4((float)h["primaryShift"], (float)h["secondaryShift"], (float)h["rootDarkening"], (float)h["rimStrength"]));
            material.SetFloat("_Cull", ResolvePaletteCullMode((string)d["cullMode"]));
            material.doubleSidedGI = (string)d["cullMode"] == "Off";
            EditorUtility.SetDirty(material);
            int assigned = AssignGeneratedBodyMaterial(renderers, material);
            return CharacterBodyMaterialBindingStatus.Create("bodyMaterialBound", BakedSurfaceTemplate, materialPath,
                "char." + packageId + ".texture.baked_base_color", assigned, "Baked BaseColor, categorical surface mips and hair flow are bound.");
        }

        private static void RemoveObsoleteCharacterSurfaceProperties(Material material, bool baked)
        {
            var serialized = new SerializedObject(material);
            string[] obsolete = baked
                ? new[] { "_WgBakedSurfaceEnabled", "_WgPaletteEnabled", "_WgFaceDetailEnabled", "_WgFaceDetailIntensity", "_WgColorPaletteTex", "_WgColorPaletteParams", "_WgFaceDetailTex" }
                : new[] { "_WgBakedSurfaceEnabled", "_WgBakedBaseColor", "_WgSurfaceDataTex", "_WgHairLobes", "_WgHairShape" };
            foreach (string field in new[] { "m_Floats", "m_Colors", "m_TexEnvs" })
            {
                SerializedProperty values = serialized.FindProperty("m_SavedProperties." + field);
                for (int i = values.arraySize - 1; i >= 0; i--)
                    if (Array.IndexOf(obsolete, values.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue) >= 0)
                        values.DeleteArrayElementAtIndex(i);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
