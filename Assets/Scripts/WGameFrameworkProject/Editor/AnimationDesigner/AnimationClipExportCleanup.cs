using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MxFramework.Project.Editor.AnimationDesigner
{
    /// <summary>A saved export may be consumed only while its content, identity and references remain unchanged.</summary>
    internal sealed class AnimationClipExportCleanup
    {
        internal string Path { get; private set; }
        internal string Guid { get; private set; }
        internal string ReferenceDiagnostic { get; private set; }
        private string _contentHash;
        private string _metaHash;

        internal static AnimationClipExportCleanup Capture(AnimationClip source, bool allowReferenced = false)
        {
            string path = AssetDatabase.GetAssetPath(source);
            if (source == null || AssetDatabase.IsSubAsset(source) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                || !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("只能清理独立的新版 .anim 导出副本。");
            if (EditorUtility.IsDirty(source)) throw new InvalidOperationException("新版动画尚未保存，不能清理。");
            foreach (var e in AnimationUtility.GetAnimationEvents(source))
                if (e.objectReferenceParameter == source) throw new InvalidOperationException("新版动画事件引用了自己，不能作为临时副本删除。");
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
                foreach (var key in AnimationUtility.GetObjectReferenceCurve(source, binding))
                    if (key.value == source) throw new InvalidOperationException("新版动画曲线引用了自己，不能作为临时副本删除。");
            string referenceDiagnostic = FindReference(source, path);
            if (!allowReferenced && referenceDiagnostic != null) throw new InvalidOperationException(referenceDiagnostic);
            return new AnimationClipExportCleanup
            {
                Path = path, Guid = AssetDatabase.AssetPathToGUID(path), ReferenceDiagnostic = referenceDiagnostic,
                _contentHash = Hash(path), _metaHash = Hash(path + ".meta")
            };
        }

        internal void DeleteAfterReplacement()
        {
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(Path);
            if (source == null || AssetDatabase.AssetPathToGUID(Path) != Guid || EditorUtility.IsDirty(source)
                || Hash(Path) != _contentHash || Hash(Path + ".meta") != _metaHash)
                throw new InvalidOperationException("新版副本在操作期间发生变化，已保留该文件：" + Path);
            AssertUnreferenced(source, Path);
            if (!AssetDatabase.DeleteAsset(Path)) throw new IOException("无法删除已完成替换的新版副本：" + Path);
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(stream));
        }

        private static void AssertUnreferenced(AnimationClip source, string path)
        {
            string diagnostic = FindReference(source, path);
            if (diagnostic != null) throw new InvalidOperationException(diagnostic);
        }

        private static string FindReference(AnimationClip source, string path)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            foreach (string asset in AssetDatabase.GetAllAssetPaths())
            {
                if (string.Equals(asset, path, StringComparison.OrdinalIgnoreCase) || !System.IO.Path.HasExtension(asset)) continue;
                // Reading serialized GUIDs avoids asking Unity to expand the dependency graph of every imported asset.
                string extension = System.IO.Path.GetExtension(asset).ToLowerInvariant();
                if (File.Exists(asset + ".meta") && ContainsReference(asset + ".meta", guid, path))
                    return "新版副本被导入配置引用，不能自动删除：" + asset + ".meta";
                bool serialized = extension == ".asset" || extension == ".prefab" || extension == ".unity"
                    || extension == ".controller" || extension == ".overridecontroller" || extension == ".anim" || extension == ".playable";
                bool textConfig = extension == ".json" || extension == ".vfx" || extension == ".shadergraph" || extension == ".shadersubgraph";
                if (!serialized && !textConfig) continue;
                if (!File.Exists(asset)) continue;
                if (textConfig || IsYaml(asset))
                {
                    if (ContainsReference(asset, guid, path))
                        return "新版副本被资产或配置引用，不能自动删除：" + asset;
                }
                else if (Array.IndexOf(AssetDatabase.GetDependencies(asset, false), path) >= 0)
                    return "新版副本被二进制资产引用，不能自动删除：" + asset;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && Array.IndexOf(EditorUtility.CollectDependencies(scene.GetRootGameObjects()), source) >= 0)
                    return "新版副本被已加载场景引用，不能自动删除：" + scene.name;
            }
            foreach (var asset in UnityEngine.Resources.FindObjectsOfTypeAll<ScriptableObject>())
                if (IsDirtyAssetReferencing(asset, source)) return "新版副本被尚未保存的资产引用，不能自动删除：" + AssetDatabase.GetAssetPath(asset);
            foreach (var asset in UnityEngine.Resources.FindObjectsOfTypeAll<RuntimeAnimatorController>())
                if (IsDirtyAssetReferencing(asset, source)) return "新版副本被尚未保存的资产引用，不能自动删除：" + AssetDatabase.GetAssetPath(asset);
            return null;
        }

        private static bool IsYaml(string path)
        {
            using (var reader = new StreamReader(path))
            {
                var header = new char[5];
                return reader.Read(header, 0, header.Length) == header.Length && new string(header) == "%YAML";
            }
        }

        private static bool ContainsReference(string file, string guid, string path)
        {
            string text = File.ReadAllText(file);
            return text.IndexOf(guid, StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf(path, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDirtyAssetReferencing(Object asset, AnimationClip source)
            => asset != null && EditorUtility.IsPersistent(asset) && EditorUtility.IsDirty(asset)
                && Array.IndexOf(EditorUtility.CollectDependencies(new[] { asset }), source) >= 0;
    }
}
