using System.IO;
using System.Reflection;
using MxFramework.Demo.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    public static class CreateRenderingDemoSlicesShowcaseScene
    {
        private static readonly FieldInfo DisableNoThemeWarningField =
            typeof(PanelSettings).GetField("m_DisableNoThemeWarning", BindingFlags.NonPublic | BindingFlags.Instance);

        private const string ScenePath = "Assets/Scenes/RenderingDemoSlicesShowcase.unity";
        private const string UiFolderPath = "Assets/UI/MxFramework/RenderingDemoSlices";
        private const string PanelSettingsPath = UiFolderPath + "/RenderingDemoSlicesPanelSettings.asset";
        private const string UxmlPath = UiFolderPath + "/RenderingDemoSlicesHud.uxml";
        private const string UssPath = UiFolderPath + "/RenderingDemoSlicesHud.uss";

        [MenuItem("MxFramework/Rendering/Create Demo Slices Showcase Scene")]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/UI", "MxFramework");
            EnsureFolder("Assets/UI/MxFramework", "RenderingDemoSlices");
            EnsureTextAsset(UxmlPath, CreateUxml());
            EnsureTextAsset(UssPath, CreateUss());
            AssetDatabase.ImportAsset(UxmlPath);
            AssetDatabase.ImportAsset(UssPath);

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (visualTree == null)
                throw new System.InvalidOperationException("Rendering demo HUD UXML could not be loaded: " + UxmlPath);
            if (styleSheet == null)
                throw new System.InvalidOperationException("Rendering demo HUD USS could not be loaded: " + UssPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.48f, 0.54f, 0.58f);
            CreateCamera();
            CreateLight();
            CreateArena();
            Renderer subjectRenderer = CreateSubject(out Transform subjectMarker);
            Transform windArrow = CreateWindArrow();
            GameObject root = CreateRoot(panelSettings, visualTree, styleSheet, subjectRenderer, windArrow, subjectMarker);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rendering demo slices showcase scene created: " + ScenePath);
        }

        public static void ValidateSmoke()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new System.InvalidOperationException("Rendering demo scene did not load: " + ScenePath);

            Camera camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null || !camera.CompareTag("MainCamera"))
                throw new System.InvalidOperationException("Rendering demo scene must contain a Main Camera tagged MainCamera.");

            Light light = Object.FindFirstObjectByType<Light>();
            if (light == null || light.type != LightType.Directional)
                throw new System.InvalidOperationException("Rendering demo scene must contain a main Directional Light.");

            RenderingDemoSlicesShowcaseRoot root = Object.FindFirstObjectByType<RenderingDemoSlicesShowcaseRoot>();
            RenderingDemoSlicesHudController hud = Object.FindFirstObjectByType<RenderingDemoSlicesHudController>();
            UIDocument document = Object.FindFirstObjectByType<UIDocument>();
            if (root == null || hud == null || document == null)
                throw new System.InvalidOperationException("Rendering demo scene is missing root, HUD, or UIDocument.");
            if (document.panelSettings == null || document.visualTreeAsset == null)
                throw new System.InvalidOperationException("Rendering demo UIDocument must bind PanelSettings and UXML.");

            VisualElement tree = document.visualTreeAsset.CloneTree();
            RequireLabel(tree, "title");
            RequireLabel(tree, "context-value");
            RequireLabel(tree, "sharedrt-value");
            RequireLabel(tree, "material-value");
            RequireLabel(tree, "publisher-value");
            RequireLabel(tree, "volume-value");
            RequireButton(tree, "wind-button");
            RequireButton(tree, "material-button");
            RequireButton(tree, "publisher-button");
            RequireButton(tree, "volume-button");
            RequireButton(tree, "reset-button");

            UnityEngine.Rendering.Universal.ScriptableRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(
                    "Assets/Config/MxFramework/Rendering/MxFrameworkUniversalRenderer.asset");
            if (rendererData == null)
                throw new System.InvalidOperationException("MxFrameworkUniversalRenderer.asset is missing.");

            string assetText = File.ReadAllText("Assets/Config/MxFramework/Rendering/MxFrameworkUniversalRenderer.asset");
            int featureCount = CountOccurrences(assetText, "m_Name: MxRenderingPipelineFeature");
            if (featureCount != 1)
                throw new System.InvalidOperationException("MxFrameworkUniversalRenderer.asset must contain exactly one MxRenderingPipelineFeature entry.");

            Debug.Log("Rendering demo slices showcase smoke validation passed.");
        }

        private static GameObject CreateRoot(
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Renderer subjectRenderer,
            Transform windArrow,
            Transform subjectMarker)
        {
            var root = new GameObject("RenderingDemoSlicesShowcaseRoot");
            UIDocument document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);
            RenderingDemoSlicesHudController hud = root.AddComponent<RenderingDemoSlicesHudController>();
            RenderingDemoSlicesShowcaseRoot showcase = root.AddComponent<RenderingDemoSlicesShowcaseRoot>();
            hud.Configure(showcase, document, visualTree, styleSheet);
            showcase.ConfigureSceneReferences(document, visualTree, styleSheet, hud, subjectRenderer, windArrow, subjectMarker);
            SetHudReferences(hud, showcase, document, visualTree, styleSheet);
            SetShowcaseReferences(showcase, document, visualTree, styleSheet, hud, subjectRenderer, windArrow, subjectMarker);
            return root;
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.085f);
            camera.fieldOfView = 48f;
            cameraObject.transform.position = new Vector3(0f, 3.2f, -6f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.55f, 0f), Vector3.up);
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private static void CreateArena()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "RenderingDemoArena";
            floor.transform.position = new Vector3(0f, -0.06f, 0f);
            floor.transform.localScale = new Vector3(7f, 0.1f, 4.5f);
            AssignMaterial(floor, new Color(0.16f, 0.19f, 0.2f));
            Object.DestroyImmediate(floor.GetComponent<Collider>());
        }

        private static Renderer CreateSubject(out Transform subjectMarker)
        {
            GameObject subject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            subject.name = "RenderingDemo_GenericSubject";
            subject.transform.position = new Vector3(0f, 0.55f, 0f);
            subject.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            AssignMaterial(subject, new Color(0.15f, 0.55f, 0.95f));
            subjectMarker = subject.transform;
            return subject.GetComponent<Renderer>();
        }

        private static Transform CreateWindArrow()
        {
            GameObject arrow = new GameObject("RenderingDemo_WindDirection");
            arrow.transform.position = new Vector3(-2.2f, 0.75f, 0f);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "WindArrowShaft";
            shaft.transform.SetParent(arrow.transform, false);
            shaft.transform.localPosition = new Vector3(0f, 0f, 0.38f);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shaft.transform.localScale = new Vector3(0.06f, 0.38f, 0.06f);
            AssignMaterial(shaft, new Color(0.2f, 0.95f, 0.78f));
            Object.DestroyImmediate(shaft.GetComponent<Collider>());

            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "WindArrowTip";
            tip.transform.SetParent(arrow.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0f, 0.88f);
            tip.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
            AssignMaterial(tip, new Color(1f, 0.83f, 0.42f));
            Object.DestroyImmediate(tip.GetComponent<Collider>());
            return arrow.transform;
        }

        private static void AssignMaterial(GameObject gameObject, Color color)
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.name = gameObject.name + "_RuntimeGeneratedMaterial";
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            renderer.sharedMaterial = material;
        }

        private static void ConfigureDocument(UIDocument document, PanelSettings panelSettings, VisualTreeAsset visualTree)
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

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "RenderingDemoSlicesPanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

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
            return settings;
        }

        private static void SetHudReferences(
            RenderingDemoSlicesHudController hud,
            RenderingDemoSlicesShowcaseRoot root,
            UIDocument document,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet)
        {
            var serialized = new SerializedObject(hud);
            SetRequired(serialized, "_root", root);
            SetRequired(serialized, "_document", document);
            SetRequired(serialized, "_visualTree", visualTree);
            SetRequired(serialized, "_styleSheet", styleSheet);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);
        }

        private static void SetShowcaseReferences(
            RenderingDemoSlicesShowcaseRoot showcase,
            UIDocument document,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            RenderingDemoSlicesHudController hud,
            Renderer subjectRenderer,
            Transform windArrow,
            Transform subjectMarker)
        {
            var serialized = new SerializedObject(showcase);
            SetRequired(serialized, "_document", document);
            SetRequired(serialized, "_visualTree", visualTree);
            SetRequired(serialized, "_styleSheet", styleSheet);
            SetRequired(serialized, "_hud", hud);
            SetRequired(serialized, "_subjectRenderer", subjectRenderer);
            SetRequired(serialized, "_windArrow", windArrow);
            SetRequired(serialized, "_subjectMarker", subjectMarker);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(showcase);
        }

        private static void EnsureTextAsset(string path, string contents)
        {
            string absolute = Path.Combine(Directory.GetCurrentDirectory(), path);
            File.WriteAllText(absolute, contents);
        }

        private static string CreateUxml()
        {
            return @"<ui:UXML xmlns:ui=""UnityEngine.UIElements"">
  <ui:VisualElement name=""rendering-demo-hud"" class=""hud"">
    <ui:Label name=""title"" text=""Rendering Demo Slices Showcase"" />
    <ui:Label name=""controls-value"" text=""1 Wind  2 Material  3 Publisher  4 Volume  R Reset"" />
    <ui:VisualElement class=""button-row"">
      <ui:Button name=""wind-button"" text=""Wind"" />
      <ui:Button name=""material-button"" text=""Material"" />
      <ui:Button name=""publisher-button"" text=""Publisher"" />
      <ui:Button name=""volume-button"" text=""Volume"" />
      <ui:Button name=""reset-button"" text=""Reset"" />
    </ui:VisualElement>
    <ui:VisualElement class=""slice-row""><ui:Label class=""slice-title"" text=""Context"" /><ui:Label name=""context-value"" text=""wind -"" /></ui:VisualElement>
    <ui:VisualElement class=""slice-row""><ui:Label class=""slice-title"" text=""SharedRT / FeaturePipeline"" /><ui:Label name=""sharedrt-value"" text=""passes -"" /></ui:VisualElement>
    <ui:VisualElement class=""slice-row""><ui:Label class=""slice-title"" text=""MaterialBindingHub"" /><ui:Label name=""material-value"" text=""bindings -"" /></ui:VisualElement>
    <ui:VisualElement class=""slice-row""><ui:Label class=""slice-title"" text=""RenderDataPublisher"" /><ui:Label name=""publisher-value"" text=""events -"" /></ui:VisualElement>
    <ui:VisualElement class=""slice-row""><ui:Label class=""slice-title"" text=""VolumeBlender Diagnostics"" /><ui:Label name=""volume-value"" text=""requests -"" /></ui:VisualElement>
    <ui:Label class=""slice-title"" text=""Event Log"" />
    <ui:VisualElement name=""event-list"" class=""event-list"" />
  </ui:VisualElement>
</ui:UXML>
";
        }

        private static string CreateUss()
        {
            return @".hud {
    position: absolute;
    left: 18px;
    top: 18px;
    width: 610px;
    padding: 14px;
    background-color: rgba(15, 19, 20, 0.94);
    border-left-width: 4px;
    border-left-color: rgb(51, 242, 199);
}

.button-row {
    flex-direction: row;
    margin-top: 8px;
    margin-bottom: 10px;
}

.slice-row {
    margin-top: 5px;
    margin-bottom: 5px;
}

.slice-title {
    color: rgb(255, 212, 107);
    -unity-font-style: bold;
    font-size: 13px;
}

.event-list {
    margin-top: 4px;
}
";
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

        private static void RequireLabel(VisualElement root, string name)
        {
            Label label = root.Q<Label>(name);
            if (label == null || string.IsNullOrWhiteSpace(label.text))
                throw new System.InvalidOperationException("HUD label missing or empty: " + name);
        }

        private static void RequireButton(VisualElement root, string name)
        {
            Button button = root.Q<Button>(name);
            if (button == null || string.IsNullOrWhiteSpace(button.text))
                throw new System.InvalidOperationException("HUD button missing or empty: " + name);
        }

        private static int CountOccurrences(string value, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = value.IndexOf(pattern, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += pattern.Length;
            }

            return count;
        }
    }
}
