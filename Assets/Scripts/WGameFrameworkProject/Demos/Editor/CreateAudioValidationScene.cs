using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    public static class CreateAudioValidationScene
    {
        public const string ScenePath = "Assets/Scenes/AudioValidationScene.unity";
        private const string UiFolder = "Assets/UI/MxFramework/AudioValidation";
        private const string UxmlPath = UiFolder + "/AudioValidationHud.uxml";
        private const string UssPath = UiFolder + "/AudioValidationHud.uss";
        private const string PanelSettingsPath = UiFolder + "/AudioValidationPanelSettings.asset";

        [MenuItem("MxFramework/Audio/Generate Validation Scene", priority = 126)]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/UI", "MxFramework");
            EnsureFolder("Assets/UI/MxFramework", "AudioValidation");

            VisualTreeAsset visualTree = LoadRequired<VisualTreeAsset>(UxmlPath);
            LoadRequired<StyleSheet>(UssPath);
            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateLight();
            CreateFloor();
            CreateValidationRoot(panelSettings, visualTree);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Audio validation scene created: " + ScenePath);
        }

        private static void CreateValidationRoot(PanelSettings panelSettings, VisualTreeAsset visualTree)
        {
            var root = new GameObject("AudioValidationRoot");
            root.transform.position = new Vector3(1.5f, 1f, 0f);
            FmodAudioDemoRunner runner = root.AddComponent<FmodAudioDemoRunner>();
            runner.ConfigureValidationDefaults();

            UIDocument document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);

            FmodAudioValidationHud hud = root.AddComponent<FmodAudioValidationHud>();
            hud.Configure(runner, document);
            var hudSerialized = new SerializedObject(hud);
            Set(hudSerialized, "_runner", runner);
            Set(hudSerialized, "_document", document);
            hudSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runner);
            EditorUtility.SetDirty(document);
            EditorUtility.SetDirty(hud);
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f);
            camera.transform.position = new Vector3(0f, 1.6f, -4.5f);
            camera.transform.LookAt(new Vector3(0f, 0.9f, 0f));
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<global::FMODUnity.StudioListener>();
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "AudioValidationFloor";
            floor.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "AudioValidationPanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 100f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void ConfigureDocument(UIDocument document, PanelSettings panelSettings, VisualTreeAsset visualTree)
        {
            var serialized = new SerializedObject(document);
            Set(serialized, "m_PanelSettings", panelSettings);
            Set(serialized, "sourceAsset", visualTree);
            serialized.FindProperty("m_SortingOrder").floatValue = 100f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            document.sortingOrder = 100;
            EditorUtility.SetDirty(document);
        }

        private static void Set(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new System.InvalidOperationException("Serialized property is missing: " + propertyName);
            property.objectReferenceValue = value;
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new System.InvalidOperationException("Required asset is missing: " + path);
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
