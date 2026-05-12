using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MxFramework.Demo
{
    /// <summary>
    /// Editor utility to create the Runtime Vertical Slice scene.
    /// Run via: Unity -batchmode -quit -executeMethod MxFramework.Demo.CreateVerticalSliceScene
    /// </summary>
    public static class CreateVerticalSliceScene
    {
        [MenuItem("MxFramework/Create Runtime Vertical Slice")]
        public static void Create()
        {
            string scenePath = "Assets/Scenes/RuntimeVerticalSlice.unity";

            // Check if scene already exists
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            {
                Debug.Log($"Scene already exists at {scenePath}. Delete it first if you want to recreate.");
                return;
            }

            // Create directory if needed
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
            var go = new GameObject("RuntimeSliceRunner");
            go.AddComponent<RuntimeVerticalSliceRunner>();

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"Scene created at {scenePath}");
            Debug.Log($"Don't forget to SVN add: svn add Assets/Scenes/");
        }
    }
}
