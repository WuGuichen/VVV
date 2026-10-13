using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Scene-rendering configuration authoring and explicit legacy migration; saving belongs to the Level Editor.</summary>
    public static class CharacterTestShallowWaterAuthoring
    {
        internal static CharacterTestShallowWaterSample FindLegacy(GameManager owner)
        {
            if (owner == null) return null;
            CharacterTestShallowWaterSample result = null;
            foreach (var root in owner.gameObject.scene.GetRootGameObjects())
                foreach (var sample in root.GetComponentsInChildren<CharacterTestShallowWaterSample>(true))
                {
                    if (sample.Owner != null && sample.Owner != owner) continue;
                    if (result != null) throw new InvalidOperationException("当前场景有多个旧水面组件，请先明确保留哪一个。");
                    result = sample;
                }
            return result;
        }

        internal static void Configure(GameManager owner)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || owner == null) return;
            if (owner.ShallowWater != null) return;
            var legacy = FindLegacy(owner);
            if (legacy != null)
            {
                if (Vector3.Dot(legacy.transform.up, Vector3.up) < .9999f ||
                    (legacy.transform.lossyScale - Vector3.one).sqrMagnitude > .0001f)
                    throw new InvalidOperationException("旧水面不是水平单位缩放，无法无损迁入；请先恢复水平平面。");
                Undo.RecordObject(owner, "迁入场景浅水配置");
                owner.SetShallowWater(new CharacterTestShallowWaterConfiguration
                {
                    Settings = legacy.Settings, Position = legacy.transform.position,
                    Yaw = legacy.transform.eulerAngles.y,
                    SurfaceEnabled = legacy.enabled && legacy.gameObject.activeInHierarchy && legacy.SurfaceEnabled,
                    ReflectionEnabled = legacy.ReflectionEnabled, RipplesEnabled = legacy.RipplesEnabled
                });
                var go = legacy.gameObject;
                // Delete only the sample's empty, non-prefab authoring host. Preserve unrelated components/children.
                if (go.name == "Shallow Water Sample" && go.transform.childCount == 0 &&
                    go.GetComponents<Component>().Length == 2 && !PrefabUtility.IsPartOfAnyPrefab(go))
                    Undo.DestroyObjectImmediate(go);
                else Undo.DestroyObjectImmediate(legacy);
            }
            else
            {
                const string folder = "Assets/Config/MxFramework/Rendering/ShallowWater";
                EnsureFolder(folder);
                string path = folder + "/" + owner.gameObject.scene.name + ".asset";
                var settings = AssetDatabase.LoadAssetAtPath<CharacterTestShallowWaterSettings>(path);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<CharacterTestShallowWaterSettings>();
                    settings.QualityTier = CharacterTestWaterQuality.Performance;
                    settings.SurfaceShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/WGame/Shaders/Environment/ShallowWater.shader");
                    settings.SsprCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Art/WGame/Shaders/Environment/ShallowWaterSSPR.compute");
                    settings.RippleCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Art/WGame/Shaders/Environment/ShallowWaterRipples.compute");
                    AssetDatabase.CreateAsset(settings, path);
                }
                Undo.RecordObject(owner, "创建场景浅水配置");
                owner.SetShallowWater(new CharacterTestShallowWaterConfiguration { Settings = settings });
            }
            EditorUtility.SetDirty(owner);
            EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
            CharacterTestShallowWaterPreview.Invalidate();
        }

        // Retain the old source entry point without creating or automatically saving a MonoBehaviour.
        public static void CreateSample()
        {
            var owner = CharacterTestSceneRenderingLevelEditorModule.FindUniqueOwner(SceneManager.GetActiveScene());
            if (owner == null) throw new InvalidOperationException("当前场景需要唯一 GameManager。");
            Configure(owner); SelectSample();
        }
        public static void SelectSample()
        {
            var owner = CharacterTestSceneRenderingLevelEditorModule.FindUniqueOwner(SceneManager.GetActiveScene());
            if (owner != null) Selection.activeGameObject = owner.gameObject;
            EditorApplication.ExecuteMenuItem("MxFramework/Level Editor");
            var window = EditorWindow.GetWindow<MxFramework.Editor.Levels.MxLevelEditorWindow>();
            window.TrySelectWorkspace("characterTest.sceneRendering");
            CharacterTestShallowWaterPreview.Invalidate();
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/'); string parent = path.Substring(0, split);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }
    }
}
