using System.Reflection;
using MxFramework.Demo.CameraUi;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Demo
{
    /// <summary>
    /// Creates the UI camera 3D validation scene through Unity serialization.
    /// Run via: Unity -batchmode -quit -projectPath . -executeMethod MxFramework.Demo.CreateUiCamera3DValidationScene.Create
    /// </summary>
    public static class CreateUiCamera3DValidationScene
    {
        private static readonly FieldInfo DisableNoThemeWarningField =
            typeof(PanelSettings).GetField("m_DisableNoThemeWarning", BindingFlags.NonPublic | BindingFlags.Instance);

        private const string ScenePath = "Assets/Scenes/UiCamera3DValidation.unity";
        private const string UiFolderPath = "Assets/UI/MxFramework/CameraUi3DValidation";
        private const string MaterialFolderPath = "Assets/Materials/MxFramework/CameraUi3DValidation";
        private const string PanelSettingsPath = UiFolderPath + "/UiCamera3DValidationPanelSettings.asset";
        private const string UxmlPath = UiFolderPath + "/UiCamera3DValidation.uxml";
        private const string UssPath = UiFolderPath + "/UiCamera3DValidation.uss";
        private const string WorldMaterialPath = MaterialFolderPath + "/UiCameraWorld.mat";
        private const string UiMaterialPath = MaterialFolderPath + "/UiCameraOverlay.mat";

        [MenuItem("MxFramework/Camera UI/Create 3D Validation Scene")]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/UI", "MxFramework");
            EnsureFolder("Assets/UI/MxFramework", "CameraUi3DValidation");
            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets/Materials", "MxFramework");
            EnsureFolder("Assets/Materials/MxFramework", "CameraUi3DValidation");

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            Material worldMaterial = LoadOrCreateMaterial(WorldMaterialPath, new Color(0.25f, 0.42f, 0.55f));
            Material uiMaterial = LoadOrCreateMaterial(UiMaterialPath, new Color(0.1f, 0.92f, 0.64f));
            if (panelSettings == null)
                throw new System.InvalidOperationException("UI camera validation PanelSettings could not be loaded or created.");
            if (visualTree == null)
                throw new System.InvalidOperationException("UI camera validation UXML could not be loaded: " + UxmlPath);
            if (styleSheet == null)
                throw new System.InvalidOperationException("UI camera validation USS could not be loaded: " + UssPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);

            UnityCamera baseCamera = CreateBaseCamera();
            UnityCamera overlayCamera = CreateOverlayCamera();
            Transform ui3dRoot = CreateVisibleObjects(worldMaterial, uiMaterial);
            CreateLight();
            CreateCompositionRoot(panelSettings, visualTree, styleSheet, baseCamera, overlayCamera, ui3dRoot);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AssignSceneAssetReferences();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("UI camera 3D validation scene created: " + ScenePath);
        }

        private static void AssignSceneAssetReferences()
        {
            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            GameObject root = GameObject.Find("UiCamera3DValidationRoot");
            GameObject ui3dRoot = GameObject.Find("UI 3D Overlay Root");
            UnityCamera baseCamera = GameObject.Find("Main Camera")?.GetComponent<UnityCamera>();
            UnityCamera overlayCamera = GameObject.Find("UI 3D Overlay Camera")?.GetComponent<UnityCamera>();
            if (root == null || ui3dRoot == null || baseCamera == null || overlayCamera == null)
                throw new System.InvalidOperationException("UI camera validation scene references could not be resolved after save.");

            UIDocument document = root.GetComponent<UIDocument>();
            UiCamera3DValidationDemo runner = root.GetComponent<UiCamera3DValidationDemo>();
            ConfigureDocument(document, panelSettings, visualTree);
            SetRunnerReferences(runner, document, panelSettings, visualTree, styleSheet, baseCamera, overlayCamera, ui3dRoot.transform);
        }

        private static UnityCamera CreateBaseCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0.45f, -6.5f);
            cameraObject.transform.rotation = Quaternion.Euler(3f, 0f, 0f);

            UnityCamera camera = cameraObject.AddComponent<UnityCamera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.095f, 0.105f);
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.cullingMask &= ~(1 << ResolveUiLayer());

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Base;
            cameraData.renderPostProcessing = false;
            return camera;
        }

        private static UnityCamera CreateOverlayCamera()
        {
            var cameraObject = new GameObject("UI 3D Overlay Camera");
            cameraObject.transform.position = new Vector3(0f, 0f, -4f);
            cameraObject.transform.rotation = Quaternion.identity;

            UnityCamera camera = cameraObject.AddComponent<UnityCamera>();
            camera.clearFlags = CameraClearFlags.Depth;
            camera.orthographic = true;
            camera.orthographicSize = 1.75f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 20f;
            camera.cullingMask = 1 << ResolveUiLayer();

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Overlay;
            cameraData.renderPostProcessing = false;
            return camera;
        }

        private static Transform CreateVisibleObjects(Material worldMaterial, Material uiMaterial)
        {
            GameObject worldCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            worldCube.name = "World Reference Cube";
            worldCube.transform.position = new Vector3(0f, -0.2f, 1.8f);
            worldCube.transform.localScale = new Vector3(2.7f, 1.4f, 1f);
            worldCube.layer = 0;
            worldCube.GetComponent<Renderer>().sharedMaterial = worldMaterial;

            GameObject uiRoot = new GameObject("UI 3D Overlay Root");
            uiRoot.transform.position = Vector3.zero;
            uiRoot.layer = ResolveUiLayer();

            GameObject diamond = GameObject.CreatePrimitive(PrimitiveType.Cube);
            diamond.name = "UI Overlay Diamond";
            diamond.transform.SetParent(uiRoot.transform, false);
            diamond.transform.localPosition = new Vector3(0.95f, 0.05f, 0f);
            diamond.transform.localRotation = Quaternion.Euler(24f, 36f, 45f);
            diamond.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
            diamond.layer = ResolveUiLayer();
            diamond.GetComponent<Renderer>().sharedMaterial = uiMaterial;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "UI Overlay Marker";
            marker.transform.SetParent(uiRoot.transform, false);
            marker.transform.localPosition = new Vector3(0.95f, -0.62f, 0f);
            marker.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
            marker.layer = ResolveUiLayer();
            marker.GetComponent<Renderer>().sharedMaterial = uiMaterial;

            return uiRoot.transform;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private static void CreateCompositionRoot(
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            UnityCamera baseCamera,
            UnityCamera overlayCamera,
            Transform ui3dRoot)
        {
            var root = new GameObject("UiCamera3DValidationRoot");
            var document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);

            var runner = root.AddComponent<UiCamera3DValidationDemo>();
            runner.ConfigureAssets(document, panelSettings, visualTree, styleSheet, baseCamera, overlayCamera, ui3dRoot);
            SetRunnerReferences(runner, document, panelSettings, visualTree, styleSheet, baseCamera, overlayCamera, ui3dRoot);
            EditorUtility.SetDirty(runner);
        }

        private static void ConfigureDocument(
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree)
        {
            var serialized = new SerializedObject(document);
            serialized.UpdateIfRequiredOrScript();
            SetRequired(serialized, "m_PanelSettings", panelSettings);
            SetRequired(serialized, "sourceAsset", visualTree);
            serialized.ApplyModifiedProperties();
            serialized.Update();
            EnsureReference(serialized, "m_PanelSettings", panelSettings);
            EnsureReference(serialized, "sourceAsset", visualTree);
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            EditorUtility.SetDirty(document);
            EditorSceneManager.MarkSceneDirty(document.gameObject.scene);
        }

        private static void SetRunnerReferences(
            UiCamera3DValidationDemo runner,
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            UnityCamera baseCamera,
            UnityCamera overlayCamera,
            Transform ui3dRoot)
        {
            var serialized = new SerializedObject(runner);
            serialized.UpdateIfRequiredOrScript();
            SetRequired(serialized, "_document", document);
            SetRequired(serialized, "_panelSettings", panelSettings);
            SetRequired(serialized, "_visualTree", visualTree);
            SetRequired(serialized, "_styleSheet", styleSheet);
            SetRequired(serialized, "_baseCamera", baseCamera);
            SetRequired(serialized, "_overlayCamera", overlayCamera);
            SetRequired(serialized, "_ui3dRoot", ui3dRoot);
            serialized.ApplyModifiedProperties();
            serialized.Update();
            EnsureReference(serialized, "_document", document);
            EnsureReference(serialized, "_panelSettings", panelSettings);
            EnsureReference(serialized, "_visualTree", visualTree);
            EnsureReference(serialized, "_styleSheet", styleSheet);
            EnsureReference(serialized, "_baseCamera", baseCamera);
            EnsureReference(serialized, "_overlayCamera", overlayCamera);
            EnsureReference(serialized, "_ui3dRoot", ui3dRoot);
            EditorUtility.SetDirty(runner);
            EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "UiCamera3DValidationPanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            AssetDatabase.ImportAsset(PanelSettingsPath);
            settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
                throw new System.InvalidOperationException("UI camera validation PanelSettings could not be imported: " + PanelSettingsPath);

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 120f;
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

        private static Material LoadOrCreateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                                Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    throw new System.InvalidOperationException("No URP Lit or Unlit shader is available for UI camera validation materials.");

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static int ResolveUiLayer()
        {
            int layer = LayerMask.NameToLayer("UI");
            return layer >= 0 ? layer : 5;
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
