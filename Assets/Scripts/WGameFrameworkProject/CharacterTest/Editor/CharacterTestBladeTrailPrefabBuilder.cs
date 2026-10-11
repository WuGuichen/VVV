using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Explicit asset authority. Run only after the parent grants this task the Editor.</summary>
    public static class CharacterTestBladeTrailPrefabBuilder
    {
        public const string PrefabPath = "Assets/VFX/MxFramework/Samples/BladeTrail/BladeTrail.prefab";
        private const string MaterialPath = "Assets/VFX/MxFramework/Samples/BladeTrail/BladeTrail.mat";
        private const string ProfilePath = "Assets/Config/MxFramework/CharacterTest/CharacterTestSceneProfile.asset";
        [MenuItem("MxFramework/CharacterTest/VFX/Create Blade Trail Assets")]
        public static void CreateAssets()
        {
            EnsureFolder("Assets/VFX/MxFramework/Samples/BladeTrail");
            Shader shader = Shader.Find("MxFramework/Vfx/BladeTrailRibbon");
            if (shader == null) throw new InvalidOperationException("BladeTrailRibbon shader must be imported first.");
            Type backendType = Type.GetType("MxFramework.Vfx.Unity.UnityBladeTrailBackend, MxFramework.Vfx.Unity", throwOnError: true);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "BladeTrail" };
                material.SetColor("_Tint", Color.white); AssetDatabase.CreateAsset(material, MaterialPath);
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var root = new GameObject("BladeTrail");
                var ownedScene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                try
                {
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, ownedScene);
                    root.SetActive(false);
                    root.AddComponent<MeshFilter>();
                    MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false; renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    Component backend = root.AddComponent(backendType);
                    var backendSettings = new SerializedObject(backend);
                    backendSettings.FindProperty("_exitFadeSeconds").floatValue = 0.25f;
                    backendSettings.ApplyModifiedPropertiesWithoutUndo();
                    root.GetComponent<MeshFilter>().sharedMesh = null;
                    root.SetActive(true);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(ownedScene);
                }
            }
            if (prefab.GetComponent(backendType) == null)
                throw new InvalidOperationException("An existing asset at BladeTrail.prefab does not belong to this generator; it was left unchanged.");
            ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ProfilePath);
            if (profile == null) throw new InvalidOperationException("The existing CharacterTest scene profile is required.");
            var serialized = new SerializedObject(profile);
            SerializedProperty property = serialized.FindProperty("_bladeTrailVfxPrefab");
            if (property == null) throw new InvalidOperationException("Candidate scene-profile source must compile before creating assets.");
            property.objectReferenceValue = prefab; serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
        }
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
