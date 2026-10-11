using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Project.Editor
{
    /// <summary>Projects Blender's published meshes into Unity-owned material and module prefabs.</summary>
    public static class EnvironmentBasePrefabImporter
    {
        private const string Published = "Assets/Art/WGame/Environment/Base/Published";
        private const string Generated = "Assets/Art/WGame/Environment/Base/Generated";
        internal const string ManifestPath = Published + "/environment_build.json";
        private const string PublicationQueueKey = "MxEnvironment.PublicationUpdateQueued";
        private static bool _publicationUpdateQueued;

        [InitializeOnLoadMethod]
        private static void ResumeQueuedPublication()
        {
            if (SessionState.GetBool(PublicationQueueKey, false)) QueuePublishedUpdate();
        }

        internal static void QueuePublishedUpdate()
        {
            if (_publicationUpdateQueued) return;
            _publicationUpdateQueued = true;
            SessionState.SetBool(PublicationQueueKey, true);
            EditorApplication.update += ApplyQueuedPublication;
        }

        private static void ApplyQueuedPublication()
        {
            if (!_publicationUpdateQueued) return;
            // Run after the asset import batch; never rebuild prefabs inside an import callback.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            try { UpdateFromPublished(); }
            catch (Exception error)
            {
                Debug.LogError("Environment publication could not update its module prefabs: "
                    + error.Message + "\nRetry with WGame > Environment > Update Base Module Prefabs after fixing the publication.");
            }
        }

        [MenuItem("WGame/Environment/Update Base Module Prefabs")]
        public static void UpdateFromPublished()
        {
            _publicationUpdateQueued = false;
            SessionState.SetBool(PublicationQueueKey, false);
            EditorApplication.update -= ApplyQueuedPublication;
            TextAsset manifestAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
            if (manifestAsset == null)
                throw new InvalidOperationException("The Blender environment publication has not been synchronized to Unity.");
            Manifest manifest = JsonUtility.FromJson<Manifest>(manifestAsset.text);
            if (manifest == null || manifest.format != "mx.environmentSharedBakedSurface.v2"
                || manifest.groups == null || manifest.groups.Length == 0
                || manifest.assets == null || manifest.assets.Length == 0)
                throw new InvalidDataException("Environment publication manifest is invalid.");
            ValidatePublishedFiles(manifest.files);
            Shader shader = Shader.Find("WGame/EnvironmentStylizedLit");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("WGame/EnvironmentStylizedLit is unavailable.");

            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (Group group in manifest.groups)
            {
                if (group.surfaceDataLayout != "aoRimEmissionProfile16" || materials.ContainsKey(group.id))
                    throw new InvalidDataException("Unsupported or duplicate baked surface group: " + group.id);
                float emissionIntensity = ResolveEmissionIntensity(group.emissionIntensity);
                string basePath = Published + "/" + group.baseColor;
                string surfacePath = Published + "/" + group.surfaceData;
                ConfigureTexture(basePath, true, FilterMode.Bilinear, true);
                ConfigureTexture(surfacePath, false, FilterMode.Point, false);
                Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
                Texture2D surfaceData = AssetDatabase.LoadAssetAtPath<Texture2D>(surfacePath);
                if (baseColor == null || surfaceData == null)
                    throw new InvalidDataException("Baked environment textures are missing for " + group.id);
                string materialPath = Generated + "/Materials/EnvironmentShared_" + group.id + ".mat";
                EnsureFolder(Path.GetDirectoryName(materialPath).Replace('\\', '/'));
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader) { name = "EnvironmentShared_" + group.id };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.shader = shader;
                material.SetFloat("_WgBakedSurface", 1f);
                material.EnableKeyword("_WG_BAKED_SURFACE_ON");
                material.SetTexture("_WgBakedBaseColor", baseColor);
                material.SetTexture("_WgSurfaceDataTex", surfaceData);
                material.SetFloat("_WgEmissionIntensity", emissionIntensity);
                EditorUtility.SetDirty(material);
                materials.Add(group.id, material);
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (PublishedAsset asset in manifest.assets)
            {
                if (!ids.Add(asset.id) || asset.uv != "UV0" || !materials.TryGetValue(asset.group, out Material material))
                    throw new InvalidDataException("Invalid published environment asset: " + asset.id);
                string modelPath = Published + "/" + asset.model;
                if (Path.GetFileNameWithoutExtension(modelPath) != asset.id)
                    throw new InvalidDataException("Environment model name does not match its stable ID: " + asset.id);
                ConfigureModel(modelPath);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null) throw new InvalidDataException("Environment FBX is missing: " + modelPath);
                string category = Category(asset.id);
                string prefabPath = Generated + "/Modules/" + category + "/" + asset.id + ".prefab";
                EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
                SaveModule(model, material, asset.id, prefabPath);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Environment base modules updated: " + ids.Count + " prefab assets in " + Generated);
        }

        private static void SaveModule(GameObject model, Material material, string name, string path)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && existing.transform.childCount == 1
                && existing.transform.GetChild(0).name == model.name)
            {
                bool matches = true;
                foreach (Renderer renderer in existing.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material slot in renderer.sharedMaterials)
                        if (slot != material) matches = false;
                }
                if (matches) return;
            }
            var root = new GameObject(name);
            try
            {
                GameObject child = (GameObject)PrefabUtility.InstantiatePrefab(model);
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = Vector3.zero;
                bool hasRenderer = false;
                foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] slots = renderer.sharedMaterials;
                    if (slots.Length == 0) slots = new Material[1];
                    for (int i = 0; i < slots.Length; i++) slots[i] = material;
                    renderer.sharedMaterials = slots;
                    hasRenderer = true;
                }
                if (!hasRenderer) throw new InvalidDataException("Environment model has no renderer: " + name);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new IOException("Could not save environment module: " + path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static float ResolveEmissionIntensity(float value)
        {
            // Older v2 manifests omit this field and deserialize it as zero.
            if (value == 0f) return 1f;
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 1f || value > 64f)
                throw new InvalidDataException("Environment emission intensity must be between 1 and 64.");
            return value;
        }

        private static void ConfigureTexture(string path, bool srgb, FilterMode filter, bool mipmaps)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidDataException("Texture importer is missing: " + path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            int maxSize = Mathf.NextPowerOfTwo(Mathf.Max(width, height));
            if (importer.sRGBTexture == srgb && importer.filterMode == filter
                && importer.mipmapEnabled == mipmaps
                && importer.maxTextureSize == maxSize
                && importer.textureCompression == TextureImporterCompression.Uncompressed)
                return;
            importer.sRGBTexture = srgb;
            importer.filterMode = filter;
            importer.mipmapEnabled = mipmaps;
            // Keep the baked texel layout: resizing also blends discrete Surface Profile IDs.
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void ConfigureModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidDataException("Model importer is missing: " + path);
            if (!importer.importAnimation && importer.materialImportMode == ModelImporterMaterialImportMode.None)
                return;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        private static void ValidatePublishedFiles(PublishedFile[] files)
        {
            if (files == null || files.Length == 0)
                throw new InvalidDataException("Environment publication has no file hashes.");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + Published));
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (PublishedFile file in files)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.path) || !paths.Add(file.path))
                    throw new InvalidDataException("Environment publication contains a duplicate or empty file path.");
                string full = Path.GetFullPath(Path.Combine(root, file.path));
                if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(full))
                    throw new InvalidDataException("Environment publication file is missing: " + file.path);
                using (var stream = File.OpenRead(full))
                using (var sha = SHA256.Create())
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
                    if (!string.Equals(actual, file.sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Environment publication hash mismatch: " + file.path);
                }
            }
        }

        private static string Category(string id)
        {
            int separator = id.IndexOf('_');
            string prefix = separator < 0 ? id : id.Substring(0, separator);
            switch (prefix)
            {
                case "Arch": return "Architecture";
                case "Ground": return "Ground";
                case "Sec": return "Security";
                case "Veh": return "Vehicles";
                case "Tool": return "Tools";
                case "Prop": return "Props";
                default: throw new InvalidDataException("Unknown environment model category: " + id);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [Serializable] private sealed class Manifest
        {
            public string format;
            public Group[] groups;
            public PublishedAsset[] assets;
            public PublishedFile[] files;
        }

        [Serializable] private sealed class Group
        {
            public string id;
            public string baseColor;
            public string surfaceData;
            public string surfaceDataLayout;
            public float emissionIntensity;
        }

        [Serializable] private sealed class PublishedAsset
        {
            public string id;
            public string group;
            public string model;
            public string uv;
        }

        [Serializable] private sealed class PublishedFile
        {
            public string path;
            public string sha256;
        }
    }

    internal sealed class EnvironmentPublicationPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Blender publishes the manifest last. Its import is the completion signal,
            // and the existing generator verifies every referenced file before writing.
            foreach (string path in importedAssets)
            {
                if (!string.Equals(path, EnvironmentBasePrefabImporter.ManifestPath, StringComparison.Ordinal)) continue;
                EnvironmentBasePrefabImporter.QueuePublishedUpdate();
                break;
            }
        }
    }
}
