using System.Reflection;
using MxFramework.Demo.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    /// <summary>
    /// Creates the Story runtime vertical slice scene through Unity serialization.
    /// Run via: Unity -batchmode -quit -projectPath . -executeMethod MxFramework.Demo.CreateStoryRuntimeVerticalSliceScene.Create
    /// </summary>
    public static class CreateStoryRuntimeVerticalSliceScene
    {
        private static readonly FieldInfo DisableNoThemeWarningField =
            typeof(PanelSettings).GetField("m_DisableNoThemeWarning", BindingFlags.NonPublic | BindingFlags.Instance);

        private const string ScenePath = "Assets/Scenes/StoryRuntimeVerticalSlice.unity";
        private const string UiFolderPath = "Assets/UI/MxFramework/Story";
        private const string PanelSettingsPath = UiFolderPath + "/StoryRuntimeVerticalSlicePanelSettings.asset";
        private const string UxmlPath = UiFolderPath + "/StoryRuntimeVerticalSlice.uxml";
        private const string UssPath = UiFolderPath + "/StoryRuntimeVerticalSlice.uss";

        [MenuItem("MxFramework/Story/Create Runtime Vertical Slice Scene")]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/UI", "MxFramework");
            EnsureFolder("Assets/UI/MxFramework", "Story");

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (panelSettings == null)
                throw new System.InvalidOperationException("Story runtime vertical slice PanelSettings could not be loaded or created.");
            if (visualTree == null)
                throw new System.InvalidOperationException("Story runtime vertical slice UXML could not be loaded: " + UxmlPath);
            if (styleSheet == null)
                throw new System.InvalidOperationException("Story runtime vertical slice USS could not be loaded: " + UssPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.46f, 0.50f, 0.55f);
            CreateCamera();
            CreateLight();
            CreateStoryRoot(panelSettings, visualTree, styleSheet);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Story runtime vertical slice scene created: " + ScenePath);
        }

        private static void CreateStoryRoot(
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet)
        {
            var root = new GameObject("StoryRuntimeVerticalSliceRoot");
            var document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);

            var runner = root.AddComponent<StoryRuntimeVerticalSliceRunner>();
            runner.ConfigureAssets(document, panelSettings, visualTree, styleSheet);
            SetRunnerReferences(runner, document, panelSettings, visualTree, styleSheet);
            EditorUtility.SetDirty(runner);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        private static void ConfigureDocument(
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree)
        {
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            document.enabled = false;
            var serialized = new SerializedObject(document);
            Set(serialized, "m_Enabled", false);
            SetRequired(serialized, "m_PanelSettings", panelSettings);
            SetRequired(serialized, "sourceAsset", visualTree);
            serialized.ApplyModifiedProperties();
            serialized.Update();
            EnsureReference(serialized, "m_PanelSettings", panelSettings);
            EnsureReference(serialized, "sourceAsset", visualTree);
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            document.enabled = false;
            EditorUtility.SetDirty(document);
        }

        private static void SetRunnerReferences(
            StoryRuntimeVerticalSliceRunner runner,
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
            serialized.ApplyModifiedProperties();
            serialized.Update();
            EnsureReference(serialized, "_document", document);
            EnsureReference(serialized, "_panelSettings", panelSettings);
            EnsureReference(serialized, "_visualTree", visualTree);
            EnsureReference(serialized, "_styleSheet", styleSheet);
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.07f);
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
                settings.name = "StoryRuntimeVerticalSlicePanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(PanelSettingsPath, ImportAssetOptions.ForceSynchronousImport);
                settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
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

        private static void EnsureReference(SerializedObject serialized, string propertyName, Object expected)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != expected)
                throw new System.InvalidOperationException("Failed to assign serialized reference: " + propertyName + " on " + serialized.targetObject);
        }
    }
}
