using System.Reflection;
using MxFramework.Demo.GameplayComponentRuntime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    /// <summary>
    /// Creates the component runtime showcase scene through Unity serialization.
    /// Run via: Unity -batchmode -quit -projectPath . -executeMethod MxFramework.Demo.CreateGameplayComponentRuntimeShowcaseScene.Create
    /// </summary>
    public static class CreateGameplayComponentRuntimeShowcaseScene
    {
        private static readonly FieldInfo DisableNoThemeWarningField =
            typeof(PanelSettings).GetField("m_DisableNoThemeWarning", BindingFlags.NonPublic | BindingFlags.Instance);

        private const string ScenePath = "Assets/Scenes/GameplayComponentRuntimeShowcase.unity";
        private const string UiFolderPath = "Assets/UI/MxFramework/GameplayComponentRuntime";
        private const string PanelSettingsPath = UiFolderPath + "/GameplayComponentRuntimePanelSettings.asset";
        private const string UxmlPath = UiFolderPath + "/GameplayComponentRuntimeShowcase.uxml";
        private const string UssPath = UiFolderPath + "/GameplayComponentRuntimeShowcase.uss";

        [MenuItem("MxFramework/Gameplay Component Runtime/Create Showcase Scene")]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/UI", "MxFramework");
            EnsureFolder("Assets/UI/MxFramework", "GameplayComponentRuntime");

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (panelSettings == null)
                throw new System.InvalidOperationException("Gameplay component runtime showcase PanelSettings could not be loaded or created.");
            if (visualTree == null)
                throw new System.InvalidOperationException("Gameplay component runtime showcase UXML could not be loaded: " + UxmlPath);
            if (styleSheet == null)
                throw new System.InvalidOperationException("Gameplay component runtime showcase USS could not be loaded: " + UssPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.54f, 0.58f, 0.63f);
            CreateCamera();
            CreateLight();
            CreateShowcaseRoot(panelSettings, visualTree, styleSheet);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Gameplay Component Runtime showcase scene created: " + ScenePath);
        }

        private static void CreateShowcaseRoot(
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet)
        {
            var root = new GameObject("GameplayComponentRuntimeShowcaseRoot");
            var document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);

            var runner = root.AddComponent<GameplayComponentRuntimeShowcaseRunner>();
            runner.ConfigureAssets(document, panelSettings, visualTree, styleSheet);
            SetRunnerReferences(runner, document, panelSettings, visualTree, styleSheet);
            EditorUtility.SetDirty(runner);
        }

        private static void SetRunnerReferences(
            GameplayComponentRuntimeShowcaseRunner runner,
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet)
        {
            var serialized = new SerializedObject(runner);
            SetRequired(serialized, "_document", document);
            SetRequired(serialized, "_panelSettings", panelSettings);
            SetRequired(serialized, "_visualTree", visualTree);
            SetRequired(serialized, "_styleSheet", styleSheet);
            SetRequired(serialized, "_spawnOnEnable", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();
            EnsureReference(serialized, "_document", document);
            EnsureReference(serialized, "_panelSettings", panelSettings);
            EnsureReference(serialized, "_visualTree", visualTree);
            EnsureReference(serialized, "_styleSheet", styleSheet);
        }

        private static void ConfigureDocument(
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree)
        {
            var serialized = new SerializedObject(document);
            SetRequired(serialized, "m_PanelSettings", panelSettings);
            SetRequired(serialized, "sourceAsset", visualTree);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();
            EnsureReference(serialized, "m_PanelSettings", panelSettings);
            EnsureReference(serialized, "sourceAsset", visualTree);
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            EditorUtility.SetDirty(document);
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.8f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "GameplayComponentRuntimePanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 100f;
            StyleSheet runtimeTheme = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
            if (runtimeTheme != null)
            {
                var serialized = new SerializedObject(settings);
                Set(serialized, "themeUss", runtimeTheme);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            DisableNoThemeWarningField?.SetValue(settings, true);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void Set(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void Set(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetRequired(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new System.InvalidOperationException("Missing serialized property: " + propertyName + " on " + serialized.targetObject);

            property.objectReferenceValue = value;
        }

        private static void SetRequired(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new System.InvalidOperationException("Missing serialized property: " + propertyName + " on " + serialized.targetObject);

            property.boolValue = value;
        }

        private static void EnsureReference(SerializedObject serialized, string propertyName, Object expected)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != expected)
                throw new System.InvalidOperationException("Failed to assign serialized reference: " + propertyName + " on " + serialized.targetObject);
        }
    }
}
