using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Editor;
using MxFramework.Editor.Animation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Project.Editor.AnimationDesigner
{
    internal static class BasicMovementAnimationCompression
    {
        private const string SourceId = "wgameframework.sword_staff_locomotion";
        private const string SourceName = "剑与法杖基础移动配置";
        private const string OpenMenu = "MxFramework/Assets/基础移动动画压缩";
        private const string QuickMenu = "MxFramework/Assets/一键压缩剑与法杖基础移动动画";
        private static readonly string[] CombinationIds =
        {
            "combat_girl_sword.sword_empty",
            "combined.000002.staff_empty"
        };

        [InitializeOnLoadMethod]
        private static void Register()
        {
            MxAnimationClipCompressionWindow.RegisterSource(SourceId, SourceName, ReadClipPaths);
            FrameworkManagerToolRegistry.Register(new FrameworkManagerToolInfo(
                SourceId, "基础移动动画压缩", "资源工具", "读取剑与法杖的基础移动配置，无需选择动画。",
                "可用", OpenMenu, 130, Open));
        }

        [MenuItem(OpenMenu, priority = 129)]
        private static void Open()
        {
            MxAnimationClipCompressionWindow.OpenForSource(SourceId);
        }

        [MenuItem(QuickMenu, priority = 130)]
        private static void Compress()
        {
            MxAnimationClipCompressionWindow.OpenForSource(SourceId, true);
        }

        [MenuItem(OpenMenu, true)]
        [MenuItem(QuickMenu, true)]
        private static bool CanOpen()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }

        private static IReadOnlyList<string> ReadClipPaths()
        {
            string libraryPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "WeaponSource", "global_weapon_library.json");
            JObject library = JObject.Parse(File.ReadAllText(libraryPath));
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string combinationId in CombinationIds)
            {
                JObject combination = FindUnique(library["combinedWeaponConfigurations"] as JArray,
                    "combinedWeaponConfigurationId", combinationId);
                string animationId = (string)combination["animationConfigurationId"];
                JObject animation = FindUnique(library["animationConfigurations"] as JArray,
                    "animationConfigurationId", animationId);
                bool foundLocomotion = false;
                foreach (JObject profile in animation["profiles"] as JArray ?? new JArray())
                {
                    foreach (JObject slot in profile["slots"] as JArray ?? new JArray())
                    {
                        if (!string.Equals((string)slot["slotId"], "locomotion", StringComparison.Ordinal))
                            continue;
                        foundLocomotion = true;
                        JObject set = FindUnique(animation["sets"] as JArray, "setId", (string)slot["setId"]);
                        JObject group = FindUnique(set["groups"] as JArray, "groupId", (string)slot["groupId"]);
                        JArray clips = group["clips"] as JArray;
                        if (clips == null || clips.Count == 0)
                            throw new InvalidOperationException(animationId + " 的基础移动组没有片段。");
                        foreach (JObject clip in clips)
                            paths.Add(ResolveClipPath(clip));
                    }
                }
                if (!foundLocomotion)
                    throw new InvalidOperationException(animationId + " 没有 locomotion 槽位。");
            }
            var result = new List<string>(paths);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static JObject FindUnique(JArray entries, string idField, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("配置缺少 " + idField + "。");
            JObject match = null;
            foreach (JObject entry in entries ?? new JArray())
            {
                if (!string.Equals((string)entry[idField], id, StringComparison.Ordinal))
                    continue;
                if (match != null)
                    throw new InvalidOperationException("配置 ID 重复：" + id);
                match = entry;
            }
            return match ?? throw new InvalidOperationException("配置不存在：" + id);
        }

        private static string ResolveClipPath(JObject clip)
        {
            JObject selection = clip["sourceSelection"] as JObject;
            string guid = (string)selection?["unityGuid"];
            string path = !string.IsNullOrEmpty(guid)
                ? AssetDatabase.GUIDToAssetPath(guid)
                : (string)selection?["unityAssetPath"];
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                || !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("基础移动片段不是独立 .anim：" + (string)clip["clipId"]);
            AnimationClip asset = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (asset == null || !AssetDatabase.IsMainAsset(asset))
                throw new InvalidOperationException("基础移动片段无法加载：" + path);
            return path;
        }
    }
}
