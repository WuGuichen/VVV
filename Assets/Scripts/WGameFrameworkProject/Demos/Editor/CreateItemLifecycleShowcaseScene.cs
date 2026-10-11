using System;
using System.Reflection;
using MxFramework.Demo.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    /// <summary>
    /// Creates the item lifecycle showcase scene through Unity serialization.
    /// Run via: Unity -batchmode -quit -projectPath . -executeMethod MxFramework.Demo.CreateItemLifecycleShowcaseScene.Create
    /// </summary>
    public static class CreateItemLifecycleShowcaseScene
    {
        private static readonly FieldInfo DisableNoThemeWarningField =
            typeof(PanelSettings).GetField("m_DisableNoThemeWarning", BindingFlags.NonPublic | BindingFlags.Instance);

        private const string ScenePath = "Assets/Scenes/ItemLifecycleShowcase.unity";
        private const string UiFolderPath = "Assets/UI/MxFramework/ItemLifecycle";
        private const string PanelSettingsPath = UiFolderPath + "/ItemLifecycleShowcasePanelSettings.asset";
        private const string UxmlPath = UiFolderPath + "/ItemLifecycleShowcase.uxml";
        private const string UssPath = UiFolderPath + "/ItemLifecycleShowcase.uss";
        private const string IconFolderPath = UiFolderPath + "/Icons";
        private const string IronSwordIconPath = IconFolderPath + "/iron_sword_icon.png";

        [MenuItem("MxFramework/Items/Create Item Lifecycle Showcase Scene")]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/UI", "MxFramework");
            EnsureFolder("Assets/UI/MxFramework", "ItemLifecycle");
            EnsureFolder(UiFolderPath, "Icons");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            Sprite ironSwordIcon = LoadRequiredSprite(IronSwordIconPath);
            if (panelSettings == null)
                throw new InvalidOperationException("Item lifecycle showcase PanelSettings could not be loaded or created.");
            if (visualTree == null)
                throw new InvalidOperationException("Item lifecycle showcase UXML could not be loaded: " + UxmlPath);
            if (styleSheet == null)
                throw new InvalidOperationException("Item lifecycle showcase USS could not be loaded: " + UssPath);
            if (ironSwordIcon == null)
                throw new InvalidOperationException("Item lifecycle showcase iron sword icon could not be loaded: " + IronSwordIconPath);

            RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.52f);
            CreateCamera();
            CreateLight();
            CreateStage();
            CreateShowcaseRoot(panelSettings, visualTree, styleSheet, ironSwordIcon);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Item Lifecycle showcase scene created: " + ScenePath);
        }

        private static void CreateShowcaseRoot(
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Sprite ironSwordIcon)
        {
            var root = new GameObject("ItemLifecycleShowcaseRoot");
            var document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);

            var runner = root.AddComponent<ItemLifecycleShowcaseRunner>();
            runner.ConfigureAssets(document, panelSettings, visualTree, styleSheet, ironSwordIcon);
            SetRunnerReferences(runner, document, panelSettings, visualTree, styleSheet, ironSwordIcon);
            EditorUtility.SetDirty(runner);
        }

        private static void SetRunnerReferences(
            ItemLifecycleShowcaseRunner runner,
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Sprite ironSwordIcon)
        {
            var serialized = new SerializedObject(runner);
            serialized.Update();
            SetRequired(serialized, "_document", document);
            SetRequired(serialized, "_panelSettings", panelSettings);
            SetRequired(serialized, "_visualTree", visualTree);
            SetRequired(serialized, "_styleSheet", styleSheet);
            SetRequired(serialized, "_ironSwordIcon", ironSwordIcon);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();
            EnsureReference(serialized, "_document", document);
            EnsureReference(serialized, "_panelSettings", panelSettings);
            EnsureReference(serialized, "_visualTree", visualTree);
            EnsureReference(serialized, "_styleSheet", styleSheet);
            EnsureReference(serialized, "_ironSwordIcon", ironSwordIcon);
            EditorUtility.SetDirty(runner);
            EditorUtility.SetDirty(runner.gameObject);
            EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
        }

        private static void ConfigureDocument(
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree)
        {
            document.visualTreeAsset = visualTree;
            document.panelSettings = panelSettings;

            var serialized = new SerializedObject(document);
            serialized.Update();
            SetRequired(serialized, "m_PanelSettings", panelSettings);
            SetRequired(serialized, "sourceAsset", visualTree);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();
            EnsureReference(serialized, "m_PanelSettings", panelSettings);
            EnsureReference(serialized, "sourceAsset", visualTree);
            document.visualTreeAsset = visualTree;
            document.panelSettings = panelSettings;
            EditorUtility.SetDirty(document);
            EditorUtility.SetDirty(document.gameObject);
            EditorSceneManager.MarkSceneDirty(document.gameObject.scene);
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.10f);
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 5f, -8f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        private static void CreateStage()
        {
            GameObject stage = new GameObject("ItemLifecycleShowcaseStage");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "LifecycleGridFloor";
            floor.transform.SetParent(stage.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            Renderer renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = CreateStageMaterial();
        }

        private static Material CreateStageMaterial()
        {
            return WGameDemoMaterialFactory.CreateEnvironment(
                "ItemLifecycleShowcaseStageMaterial",
                new Color(0.15f, 0.18f, 0.22f));
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "ItemLifecycleShowcasePanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
                AssetDatabase.ImportAsset(PanelSettingsPath, ImportAssetOptions.ForceUpdate);
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
                serialized.Update();
                Set(serialized, "themeUss", runtimeTheme);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            DisableNoThemeWarningField?.SetValue(settings, true);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static Sprite LoadRequiredSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void Set(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void SetRequired(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException("Missing serialized property: " + propertyName + " on " + serialized.targetObject);

            property.objectReferenceValue = value;
        }

        private static void EnsureReference(SerializedObject serialized, string propertyName, UnityEngine.Object expected)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != expected)
                throw new InvalidOperationException("Failed to assign serialized reference: " + propertyName + " on " + serialized.targetObject);
        }
    }
}
