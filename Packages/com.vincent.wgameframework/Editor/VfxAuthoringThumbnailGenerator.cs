using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Generates rebuildable deterministic VFX preview thumbnails and a content-hash manifest.</summary>
    /// <remarks>Editor-only and AllocByDesign. Outputs only under Temp and never edits Authoring truth.</remarks>
    public static class VfxAuthoringThumbnailGenerator
    {
        public const string CatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        public const string ResourceCatalogPath = "Assets/Config/MxFramework/ResourceCatalogs/mxframework_samples_resource_catalog.json";
        public const string OutputRoot = "Temp/MxFramework/VfxAuthoring/Thumbnails";
        private const int ThumbnailSize = 256;

        [MenuItem("MxFramework/VFX/Generate Authoring Thumbnails")]
        public static void GenerateAll()
        {
            JObject authoring = JObject.Parse(File.ReadAllText(CatalogPath));
            JObject resources = JObject.Parse(File.ReadAllText(ResourceCatalogPath));
            Dictionary<string, string> assetPaths = BuildResourceAssetPaths(resources);
            Directory.CreateDirectory(OutputRoot);
            var entries = new JArray();
            foreach (JObject effect in (authoring["effects"] as JArray ?? new JArray()).Children<JObject>())
            {
                string effectId = effect.Value<string>("effectId") ?? string.Empty;
                JObject key = effect["resourceKey"] as JObject;
                string stableKey = BuildStableKey(key);
                var entry = new JObject
                {
                    ["effectId"] = effectId,
                    ["resourceKey"] = stableKey,
                    ["assetPath"] = assetPaths.TryGetValue(stableKey, out string assetPath) ? assetPath : string.Empty,
                    ["thumbnailPath"] = OutputRoot.Replace('\\', '/') + "/" + effectId.Replace('.', '_') + ".png",
                    ["width"] = ThumbnailSize,
                    ["height"] = ThumbnailSize
                };
                if (!assetPaths.TryGetValue(stableKey, out assetPath) || AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) is not GameObject prefab)
                {
                    entry["status"] = "MissingPrefab";
                    entry["previewHash"] = string.Empty;
                    entry["perceptualHash"] = string.Empty;
                    entries.Add(entry);
                    continue;
                }
                VfxThumbnailRenderResult render = Render(prefab);
                string outputPath = entry.Value<string>("thumbnailPath");
                File.WriteAllBytes(outputPath, render.Png);
                entry["status"] = "Generated";
                entry["previewHash"] = ComputeHash(render.Png);
                entry["perceptualHash"] = render.PerceptualHash;
                entries.Add(entry);
            }
            var manifest = new JObject
            {
                ["schemaVersion"] = "mx.vfx.authoring.thumbnail-manifest.v1",
                ["width"] = ThumbnailSize,
                ["height"] = ThumbnailSize,
                ["entries"] = entries
            };
            File.WriteAllText(OutputRoot + "/thumbnail_manifest.json", manifest.ToString(Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));
            Debug.Log("VFX Authoring thumbnails generated. count=" + entries.Count + " output=" + OutputRoot);
        }

        private static VfxThumbnailRenderResult Render(GameObject prefab)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                preview.BeginStaticPreview(new Rect(0f, 0f, ThumbnailSize, ThumbnailSize));
                GameObject instance = preview.InstantiatePrefabInScene(prefab);
                ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particles.Length; i++)
                    particles[i].Simulate(0.65f, true, true, true);
                Bounds bounds = CalculateBounds(instance);
                float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
                Vector3 direction = new Vector3(0.65f, 0.35f, -1f).normalized;
                preview.camera.transform.position = bounds.center + direction * radius * 3.2f;
                preview.camera.transform.LookAt(bounds.center);
                preview.camera.nearClipPlane = Mathf.Max(0.01f, radius * 0.01f);
                preview.camera.farClipPlane = radius * 10f + 10f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.035f, 0.04f, 0.05f, 1f);
                preview.cameraFieldOfView = 32f;
                preview.ambientColor = new Color(0.35f, 0.35f, 0.38f, 1f);
                if (preview.lights.Length > 0)
                {
                    preview.lights[0].intensity = 1.2f;
                    preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
                }
                preview.Render(true, true);
                Texture2D texture = preview.EndStaticPreview();
                return new VfxThumbnailRenderResult(texture.EncodeToPNG(), ComputeDifferenceHash(texture));
            }
            finally
            {
                preview.Cleanup();
            }
        }

        private static string ComputeDifferenceHash(Texture2D source)
        {
            const int width = 9;
            const int height = 8;
            Color[] pixels = source.GetPixels();
            ulong hash = 0UL;
            int bit = 0;
            for (int y = 0; y < height; y++)
            {
                int sourceY = Mathf.Clamp(Mathf.RoundToInt((y + 0.5f) * source.height / height), 0, source.height - 1);
                for (int x = 0; x < width - 1; x++)
                {
                    int leftX = Mathf.Clamp(Mathf.RoundToInt((x + 0.5f) * source.width / width), 0, source.width - 1);
                    int rightX = Mathf.Clamp(Mathf.RoundToInt((x + 1.5f) * source.width / width), 0, source.width - 1);
                    float left = Luminance(pixels[sourceY * source.width + leftX]);
                    float right = Luminance(pixels[sourceY * source.width + rightX]);
                    if (left > right)
                        hash |= 1UL << bit;
                    bit++;
                }
            }
            return hash.ToString("x16");
        }

        private static float Luminance(Color value) => value.r * 0.2126f + value.g * 0.7152f + value.b * 0.0722f;

        private readonly struct VfxThumbnailRenderResult
        {
            public VfxThumbnailRenderResult(byte[] png, string perceptualHash)
            {
                Png = png;
                PerceptualHash = perceptualHash;
            }

            public byte[] Png { get; }
            public string PerceptualHash { get; }
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.one * 2f);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            if (bounds.extents.sqrMagnitude < 0.0001f)
                bounds.extents = Vector3.one;
            return bounds;
        }

        private static Dictionary<string, string> BuildResourceAssetPaths(JObject catalog)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            string defaultPackage = catalog.Value<string>("packageId") ?? string.Empty;
            foreach (JObject entry in (catalog["entries"] as JArray ?? new JArray()).Children<JObject>())
            {
                string packageId = entry.Value<string>("packageId") ?? defaultPackage;
                string key = packageId + "|" + (entry.Value<string>("id") ?? string.Empty) + "|" + (entry.Value<string>("type") ?? string.Empty) + "|" + (entry.Value<string>("variant") ?? string.Empty);
                string path = (entry["providerData"] as JObject)?.Value<string>("assetPath") ?? string.Empty;
                result[key] = path.Replace('\\', '/');
            }
            return result;
        }

        private static string BuildStableKey(JObject key)
        {
            if (key == null)
                return string.Empty;
            return (key.Value<string>("packageId") ?? string.Empty) + "|" + (key.Value<string>("id") ?? string.Empty) + "|" + (key.Value<string>("typeId") ?? string.Empty) + "|" + (key.Value<string>("variant") ?? string.Empty);
        }

        private static string ComputeHash(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2"));
            return "sha256:" + builder;
        }
    }
}
