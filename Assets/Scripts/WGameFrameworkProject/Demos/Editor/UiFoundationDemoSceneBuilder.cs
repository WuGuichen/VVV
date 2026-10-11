using MxFramework.Demo.FairyGui;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Demo.Editor
{
    public static class UiFoundationDemoSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MxFramework/UiFoundationDemo.unity";
        private const string PackageBytesPath = "Assets/Bundles/FGUI/MxUiFoundationDemo/MxUiFoundationDemo_fui.bytes";
        private const string AtlasPath = "Assets/Bundles/FGUI/MxUiFoundationDemo/MxUiFoundationDemo_atlas0.png";

        [MenuItem("MxFramework/Demos/Create UI Foundation Composer Preview Scene")]
        public static void CreateScene()
        {
            ConfigureAtlasImportSettings();
            TextAsset packageBytes = AssetDatabase.LoadAssetAtPath<TextAsset>(PackageBytesPath);
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (packageBytes == null || atlas == null)
                throw new System.InvalidOperationException("Publish MxUiFoundationDemo before creating its preview scene.");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<UnityEngine.Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);

            var lightObject = new GameObject("Directional Light");
            lightObject.AddComponent<Light>().type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var demoObject = new GameObject("UI Foundation Demo");
            UiFoundationDemoPreviewBootstrap bootstrap = demoObject.AddComponent<UiFoundationDemoPreviewBootstrap>();
            bootstrap.Configure(packageBytes, atlas);

            string directory = System.IO.Path.GetDirectoryName(ScenePath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                    AssetDatabase.CreateFolder("Assets", "Scenes");
                if (!AssetDatabase.IsValidFolder("Assets/Scenes/MxFramework"))
                    AssetDatabase.CreateFolder("Assets/Scenes", "MxFramework");
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = demoObject;
        }

        private static void ConfigureAtlasImportSettings()
        {
            if (AssetImporter.GetAtPath(AtlasPath) is not TextureImporter importer ||
                !importer.mipmapEnabled)
            {
                return;
            }

            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}
