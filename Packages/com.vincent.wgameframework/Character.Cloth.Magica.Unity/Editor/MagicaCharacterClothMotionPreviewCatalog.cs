using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    internal sealed class MagicaCharacterClothMotionPreviewClip
    {
        internal string ClipId { get; set; } = string.Empty;
        internal string DisplayName { get; set; } = string.Empty;
        internal string RuntimeResourceKey { get; set; } = string.Empty;
        internal string AssetPath { get; set; } = string.Empty;
        internal AnimationClip Clip { get; set; }
    }

    internal sealed class MagicaCharacterClothMotionPreviewCatalogResult
    {
        internal List<MagicaCharacterClothMotionPreviewClip> Clips { get; } =
            new List<MagicaCharacterClothMotionPreviewClip>();
        internal string Error { get; set; } = string.Empty;
        internal bool Succeeded => Clips.Count > 0 && string.IsNullOrWhiteSpace(Error);
    }

    internal static class MagicaCharacterClothMotionPreviewCatalog
    {
        private const string GeneratedRoot = "Assets/MxFrameworkGenerated/CharacterPackages/";
        private const string GlobalCatalogPath =
            "Assets/Config/MxFramework/ResourceCatalogs/mxframework_samples_resource_catalog.json";

        internal static MagicaCharacterClothMotionPreviewCatalogResult Load(string packageId)
        {
            var result = new MagicaCharacterClothMotionPreviewCatalogResult();
            if (!IsSafePackageId(packageId))
            {
                result.Error = "运动预览需要合法的角色 Package ID。";
                return result;
            }

            string registryPath = GeneratedRoot + packageId + "/config/animation_clip_registry.json";
            if (!File.Exists(registryPath))
            {
                result.Error = "未找到动画注册表：" + registryPath;
                return result;
            }

            try
            {
                Dictionary<string, string> globalPaths = LoadGlobalAnimationPaths();
                JArray clips = JObject.Parse(File.ReadAllText(registryPath))["clips"] as JArray;
                for (int i = 0; clips != null && i < clips.Count; i++)
                {
                    JObject item = clips[i] as JObject;
                    if (item == null) continue;
                    string runtimeKey = Read(item, "runtimeResourceKey");
                    string assetPath = globalPaths.TryGetValue(runtimeKey, out string mappedPath)
                        ? mappedPath
                        : Read(item["sourceSelection"] as JObject, "unityAssetPath");
                    assetPath = NormalizeAnimationAssetPath(assetPath);
                    if (string.IsNullOrWhiteSpace(assetPath)) continue;
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                    if (clip == null) continue;
                    string clipId = Read(item, "clipId");
                    string displayName = Read(item, "displayName");
                    result.Clips.Add(new MagicaCharacterClothMotionPreviewClip
                    {
                        ClipId = clipId,
                        DisplayName = string.IsNullOrWhiteSpace(displayName) ? clipId : displayName,
                        RuntimeResourceKey = runtimeKey,
                        AssetPath = assetPath,
                        Clip = clip,
                    });
                }
            }
            catch (Exception exception) when (
                exception is IOException || exception is Newtonsoft.Json.JsonException || exception is ArgumentException)
            {
                result.Error = "读取运动预览动画失败：" + exception.Message;
                return result;
            }

            if (result.Clips.Count == 0)
                result.Error = "动画注册表中没有可加载的独立 AnimationClip。";
            return result;
        }

        private static Dictionary<string, string> LoadGlobalAnimationPaths()
        {
            var paths = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(GlobalCatalogPath)) return paths;
            JArray entries = JObject.Parse(File.ReadAllText(GlobalCatalogPath))["entries"] as JArray;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                JObject entry = entries[i] as JObject;
                if (entry == null || !string.Equals(Read(entry, "type"), "AnimationClip", StringComparison.Ordinal))
                    continue;
                string id = Read(entry, "id");
                string assetPath = Read(entry["providerData"] as JObject, "assetPath");
                assetPath = NormalizeAnimationAssetPath(assetPath);
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(assetPath))
                    paths[id] = assetPath;
            }
            return paths;
        }

        private static string NormalizeAnimationAssetPath(string value)
        {
            string path = (value ?? string.Empty).Replace('\\', '/').Trim();
            return path.StartsWith("Assets/", StringComparison.Ordinal)
                && path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)
                    ? path
                    : string.Empty;
        }

        private static bool IsSafePackageId(string value) =>
            !string.IsNullOrWhiteSpace(value)
            && value.IndexOf('/') < 0
            && value.IndexOf('\\') < 0
            && value.IndexOf("..", StringComparison.Ordinal) < 0;

        private static string Read(JObject source, string name) =>
            source == null ? string.Empty : (string)source[name] ?? string.Empty;
    }
}
