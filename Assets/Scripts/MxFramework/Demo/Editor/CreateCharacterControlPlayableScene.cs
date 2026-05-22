using System.Reflection;
using MxFramework.Demo.CharacterControl;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    /// <summary>
    /// Creates the Character Control playable scene through Unity serialization.
    /// Run via: Unity -batchmode -quit -projectPath . -executeMethod MxFramework.Demo.CreateCharacterControlPlayableScene.Create
    /// </summary>
    public static class CreateCharacterControlPlayableScene
    {
        private static readonly FieldInfo DisableNoThemeWarningField =
            typeof(PanelSettings).GetField("m_DisableNoThemeWarning", BindingFlags.NonPublic | BindingFlags.Instance);

        private const string ScenePath = "Assets/Scenes/CharacterControlPlayable.unity";
        private const string UiFolderPath = "Assets/UI/MxFramework/CharacterControl";
        private const string PanelSettingsPath = UiFolderPath + "/CharacterControlPlayablePanelSettings.asset";
        private const string UxmlPath = UiFolderPath + "/CharacterControlPlayableDemo.uxml";
        private const string UssPath = UiFolderPath + "/CharacterControlPlayableDemo.uss";

        [MenuItem("MxFramework/Character Control/Create Playable Scene")]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/UI", "MxFramework");
            EnsureFolder("Assets/UI/MxFramework", "CharacterControl");
            AssetDatabase.ImportAsset(UxmlPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(UssPath, ImportAssetOptions.ForceSynchronousImport);

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (panelSettings == null)
                throw new System.InvalidOperationException("Character Control playable PanelSettings could not be loaded or created.");
            if (visualTree == null)
                throw new System.InvalidOperationException("Character Control playable UXML could not be loaded: " + UxmlPath);
            if (styleSheet == null)
                throw new System.InvalidOperationException("Character Control playable USS could not be loaded: " + UssPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.48f, 0.52f, 0.56f);
            CreateCamera();
            CreateLight();
            CreateGround();
            CreatePlayableRoot(panelSettings, visualTree, styleSheet);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Character Control playable scene created: " + ScenePath);
        }

        private static void CreatePlayableRoot(
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet)
        {
            var root = new GameObject("CharacterControlPlayableRoot");
            var document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);

            Transform player = CreatePlayerMarker();
            Transform target = CreateTargetMarker();
            var demo = root.AddComponent<CharacterControlPlayableDemo>();
            demo.ConfigureAssets(document, panelSettings, visualTree, styleSheet, player, target);
            SetDemoReferences(demo, document, panelSettings, visualTree, styleSheet, player, target);
            EditorUtility.SetDirty(demo);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        private static void SetDemoReferences(
            CharacterControlPlayableDemo demo,
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Transform player,
            Transform target)
        {
            var serialized = new SerializedObject(demo);
            SetRequired(serialized, "_document", document);
            SetRequired(serialized, "_panelSettings", panelSettings);
            SetRequired(serialized, "_visualTree", visualTree);
            SetRequired(serialized, "_styleSheet", styleSheet);
            SetRequired(serialized, "_playerMarker", player);
            SetRequired(serialized, "_enemyMarker", target);
            serialized.ApplyModifiedProperties();
            serialized.Update();
            EnsureReference(serialized, "_document", document);
            EnsureReference(serialized, "_panelSettings", panelSettings);
            EnsureReference(serialized, "_visualTree", visualTree);
            EnsureReference(serialized, "_styleSheet", styleSheet);
            EnsureReference(serialized, "_playerMarker", player);
            EnsureReference(serialized, "_enemyMarker", target);
        }

        private static void ConfigureDocument(
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree)
        {
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            var serialized = new SerializedObject(document);
            SetRequired(serialized, "m_PanelSettings", panelSettings);
            SetRequired(serialized, "sourceAsset", visualTree);
            serialized.ApplyModifiedProperties();
            serialized.Update();
            EnsureReference(serialized, "m_PanelSettings", panelSettings);
            EnsureReference(serialized, "sourceAsset", visualTree);
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            EditorUtility.SetDirty(document);
        }

        private static Transform CreatePlayerMarker()
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "CharacterControlPlayer";
            player.transform.position = new Vector3(0f, 0.9f, 0f);
            player.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            SetRendererColor(player, new Color(0.22f, 0.75f, 0.52f));
            return player.transform;
        }

        private static Transform CreateTargetMarker()
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "CharacterControlTarget";
            target.transform.position = new Vector3(3f, 0.5f, 2f);
            target.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            SetRendererColor(target, new Color(0.86f, 0.31f, 0.28f));
            return target.transform;
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "CharacterControlGround";
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(12f, 0.1f, 12f);
            SetRendererColor(ground, new Color(0.18f, 0.20f, 0.23f));
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            cameraObject.transform.position = new Vector3(0f, 7f, -8f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "CharacterControlPlayablePanelSettings";
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
                serialized.ApplyModifiedProperties();
            }

            DisableNoThemeWarningField?.SetValue(settings, true);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void SetRendererColor(GameObject gameObject, Color color)
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer == null)
                return;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (material.shader == null)
                material = new Material(Shader.Find("Standard"));
            material.color = color;
            renderer.sharedMaterial = material;
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
