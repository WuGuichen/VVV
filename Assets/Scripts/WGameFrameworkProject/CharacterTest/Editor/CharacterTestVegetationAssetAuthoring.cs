using System;
using System.Collections.Generic;
using MxFramework.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Creates and binds the sole vegetation data authority; no scene-instance import.</summary>
    internal static class CharacterTestVegetationAssetAuthoring
    {
        internal const string DataFolder = "Assets/Config/MxFramework/Vegetation";
        private static readonly Dictionary<ulong, CharacterTestVegetationSource> Sources = new Dictionary<ulong, CharacterTestVegetationSource>();
        internal static CharacterTestVegetationSource Find(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            MxScenePresentationBinding selected = null; UnityEngine.Object owner = null;
            var game = CharacterTestSceneRenderingLevelEditorModule.FindUniqueOwner(scene);
            Select(game?.SceneRendering, game, ref selected, ref owner);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var host in root.GetComponentsInChildren<MxSceneRenderingSceneRoot>(true))
                    Select(host.Configuration, host, ref selected, ref owner);
            Sources.TryGetValue(scene.handle.GetRawData(), out var result);
            if (selected == null) { result?.Renderer.Disable(); Sources.Remove(scene.handle.GetRawData()); return null; }
            if (result != null && ReferenceEquals(result.Binding, selected) && result.Data == selected.Data) return result;
            result?.Renderer.Disable();
            result = new CharacterTestVegetationSource((CharacterTestVegetationData)selected.Data, selected, scene, owner);
            Sources[scene.handle.GetRawData()] = result; return result;
        }
        private static void Select(MxSceneRenderingConfiguration configuration, UnityEngine.Object candidate,
            ref MxScenePresentationBinding selected, ref UnityEngine.Object owner)
        {
            if (configuration?.Presentations == null) return;
            foreach (var binding in configuration.Presentations)
            {
                if (!(binding?.Data is CharacterTestVegetationData)) continue;
                if (selected != null) throw new InvalidOperationException("当前 Level Editor 需要唯一植被数据绑定，请移除重复绑定。");
                selected = binding; owner = candidate;
            }
        }
        internal static CharacterTestVegetationSource Create(Scene scene)
        {
            RequireEdit(scene);
            if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("先保存场景，再创建植被数据资产。");
            var current = Find(scene); if (current != null) return current;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterTestVegetationAuthoring.GrassPrefabPath);
            if (prefab == null) throw new InvalidOperationException("基础草 Prefab 缺失。");
            var data = ScriptableObject.CreateInstance<CharacterTestVegetationData>();
            data.EditableTypes.Add(new CharacterTestVegetationType { Prefab = prefab });
            EnsureFolder(DataFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath(DataFolder + "/" + scene.name + ".asset");
            AssetDatabase.CreateAsset(data, path);
            try { return Bind(scene, data); }
            catch { AssetDatabase.DeleteAsset(path); throw; }
        }
        /// <summary>Explicit author action; existing bindings keep their stable coordinate/surface state.</summary>
        internal static CharacterTestVegetationSource Bind(Scene scene, CharacterTestVegetationData data,
            Transform anchor = null, Transform surfaceRoot = null)
        {
            RequireEdit(scene);
            if (data == null || !AssetDatabase.Contains(data)) throw new InvalidOperationException("请选择已保存的植被数据资产。");
            if (!data.TryValidate(out string issue)) throw new InvalidOperationException(issue);
            var current = Find(scene);
            if (anchor == null) anchor = current?.transform;
            if (surfaceRoot == null) surfaceRoot = current?.SurfaceRoot;
            if (anchor != null && anchor.gameObject.scene != scene) throw new InvalidOperationException("植被锚点必须属于当前场景。");
            if (surfaceRoot != null && surfaceRoot.gameObject.scene != scene) throw new InvalidOperationException("地面范围必须属于当前场景。");
            if (anchor != null) CharacterTestVegetationAuthoring.ValidateExclusion(anchor, scene);
            if (anchor != null && surfaceRoot != null && (anchor == surfaceRoot || anchor.IsChildOf(surfaceRoot) || surfaceRoot.IsChildOf(anchor)))
                throw new InvalidOperationException("植被锚点与地面范围必须互相独立。");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("绑定植被数据资产");
            try
            {
                if (anchor == null)
                {
                    var root = new GameObject(CharacterTestVegetationAuthoring.GroupName);
                    SceneManager.MoveGameObjectToScene(root, scene); Undo.RegisterCreatedObjectUndo(root, "创建植被锚点"); anchor = root.transform;
                }
                UnityEngine.Object bindingOwner; MxSceneRenderingConfiguration configuration;
                var game = CharacterTestSceneRenderingLevelEditorModule.FindUniqueOwner(scene);
                if (current != null)
                {
                    bindingOwner = current.BindingOwner;
                    configuration = bindingOwner is GameManager manager ? manager.SceneRendering : ((MxSceneRenderingSceneRoot)bindingOwner).Configuration;
                }
                else if (game != null)
                {
                    Undo.RecordObject(game, "绑定植被数据资产");
                    configuration = game.SceneRendering ?? new MxSceneRenderingConfiguration();
                    game.SetSceneRendering(configuration); bindingOwner = game;
                }
                else
                {
                    var host = anchor.GetComponent<MxSceneRenderingSceneRoot>();
                    if (host == null) host = Undo.AddComponent<MxSceneRenderingSceneRoot>(anchor.gameObject);
                    configuration = host.Configuration; bindingOwner = host;
                }
                Undo.RegisterCompleteObjectUndo(bindingOwner, "绑定植被数据资产");
                if (current != null)
                {
                    current.Binding.Data = data; current.Binding.Anchor = anchor; current.Binding.SurfaceRoot = surfaceRoot;
                }
                else
                {
                    var bindings = new List<MxScenePresentationBinding>(configuration.Presentations ?? Array.Empty<MxScenePresentationBinding>());
                    bindings.Add(new MxScenePresentationBinding { OwnerId = Guid.NewGuid().ToString("N"), Data = data,
                        Anchor = anchor, SurfaceRoot = surfaceRoot, Enabled = true });
                    configuration.Presentations = bindings.ToArray();
                }
                EditorUtility.SetDirty(bindingOwner); EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undo); return Find(scene);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
        private static void RequireEdit(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("植被数据编辑需要 Edit Mode 中已打开的场景。");
        }
        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
        internal static void ReleaseScene(Scene scene)
        { if (Sources.TryGetValue(scene.handle.GetRawData(), out var source)) { source.Renderer.Disable(); Sources.Remove(scene.handle.GetRawData()); } }
        internal static void Release()
        { foreach (var source in Sources.Values) source.Renderer.Disable(); Sources.Clear(); }
    }
}
