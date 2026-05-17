using MxFramework.Demo.MxAnimationShowcase;
using MxFramework.Editor.Animation;
using MxFramework.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    public static class CreateMxAnimationShowcaseScene
    {
        private const string ScenePath = MxAnimationShowcaseDemoBootstrap.ScenePath;
        private const string PanelSettingsPath = "Assets/UI/MxFramework/MxAnimationShowcase/MxAnimationShowcasePanelSettings.asset";
        private const string UxmlPath = "Assets/UI/MxFramework/MxAnimationShowcase/MxAnimationShowcaseHud.uxml";
        private const string UssPath = "Assets/UI/MxFramework/MxAnimationShowcase/MxAnimationShowcaseHud.uss";
        private const string SkeletonModelPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/Models/Skeleton.fbx";
        private const string IdleClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_idle.anim";
        private const string WalkForwardClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_walk_forward.anim";
        private const string WalkBackClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_walk_back.anim";
        private const string WalkLeftClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_walk_left.anim";
        private const string WalkRightClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_walk_right.anim";
        private const string RunForwardClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_forward.anim";
        private const string RunBackClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_back.anim";
        private const string RunLeftClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_left.anim";
        private const string RunRightClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_right.anim";
        private const string SprintForwardClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_sprint_forward.anim";
        private const string JumpClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_jump.anim";
        private const string JumpRunningClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_jump_running.anim";
        private const string JumpRunningLandingClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_jump_running_landing.anim";
        private const string LandToIdleClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_land_to_standing_idle.anim";
        private const string TurnLeft90ClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_turn_left_90.anim";
        private const string TurnRight90ClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_turn_right_90.anim";
        private const string UpperBodyMaskPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/Masks/SkeletonUpperBody.mask";

        [MenuItem("MxFramework/MxAnimation/Generate System Showcase Scene", priority = 125)]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/UI/MxFramework", "MxAnimationShowcase");

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = LoadRequired<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = LoadRequired<StyleSheet>(UssPath);
            GameObject skeletonModel = LoadRequired<GameObject>(SkeletonModelPath);
            AnimationClip idleClip = LoadRequired<AnimationClip>(IdleClipPath);
            AnimationClip walkForwardClip = LoadRequired<AnimationClip>(WalkForwardClipPath);
            AnimationClip walkBackClip = LoadRequired<AnimationClip>(WalkBackClipPath);
            AnimationClip walkLeftClip = LoadRequired<AnimationClip>(WalkLeftClipPath);
            AnimationClip walkRightClip = LoadRequired<AnimationClip>(WalkRightClipPath);
            AnimationClip runForwardClip = LoadRequired<AnimationClip>(RunForwardClipPath);
            AnimationClip runBackClip = LoadRequired<AnimationClip>(RunBackClipPath);
            AnimationClip runLeftClip = LoadRequired<AnimationClip>(RunLeftClipPath);
            AnimationClip runRightClip = LoadRequired<AnimationClip>(RunRightClipPath);
            AnimationClip sprintForwardClip = LoadRequired<AnimationClip>(SprintForwardClipPath);
            AnimationClip jumpClip = LoadRequired<AnimationClip>(JumpClipPath);
            AnimationClip jumpRunningClip = LoadRequired<AnimationClip>(JumpRunningClipPath);
            AnimationClip jumpRunningLandingClip = LoadRequired<AnimationClip>(JumpRunningLandingClipPath);
            AnimationClip landToIdleClip = LoadRequired<AnimationClip>(LandToIdleClipPath);
            AnimationClip turnLeft90Clip = LoadRequired<AnimationClip>(TurnLeft90ClipPath);
            AnimationClip turnRight90Clip = LoadRequired<AnimationClip>(TurnRight90ClipPath);
            AvatarMask upperBodyMask = LoadRequired<AvatarMask>(UpperBodyMaskPath);
            TextAsset bakeReport = GenerateBakeReport(jumpClip);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.60f);

            CreateCamera();
            CreateLight();
            CreateArena();
            Transform locomotionAnchor = CreateStation("Station_1D_Locomotion", new Vector3(-5.4f, 0f, 0f), "1D Blend");
            Transform directionalAnchor = CreateStation("Station_2D_Directional", new Vector3(-1.8f, 0f, 0f), "2D Blend");
            Transform layerAnchor = CreateStation("Station_Layer_Mask_Bridge", new Vector3(1.8f, 0f, 0f), "Layer/Mask");
            Transform overrideAnchor = CreateStation("Station_Mod_Override", new Vector3(5.4f, 0f, 0f), "Mod/Fallback");

            GameObject root = CreateRoot(panelSettings, visualTree, styleSheet);
            var input = root.GetComponent<DefaultInputService>();
            var document = root.GetComponent<UIDocument>();
            var bootstrap = root.GetComponent<MxAnimationShowcaseDemoBootstrap>();
            bootstrap.ConfigureSceneReferences(
                input,
                document,
                visualTree,
                styleSheet,
                locomotionAnchor,
                directionalAnchor,
                layerAnchor,
                overrideAnchor,
                skeletonModel,
                idleClip,
                walkForwardClip,
                walkBackClip,
                walkLeftClip,
                walkRightClip,
                runForwardClip,
                runBackClip,
                runLeftClip,
                runRightClip,
                sprintForwardClip,
                jumpClip,
                jumpRunningClip,
                jumpRunningLandingClip,
                landToIdleClip,
                turnLeft90Clip,
                turnRight90Clip,
                upperBodyMask,
                bakeReport);
            SetBootstrapReferences(
                bootstrap,
                input,
                document,
                visualTree,
                styleSheet,
                locomotionAnchor,
                directionalAnchor,
                layerAnchor,
                overrideAnchor,
                skeletonModel,
                idleClip,
                walkForwardClip,
                walkBackClip,
                walkLeftClip,
                walkRightClip,
                runForwardClip,
                runBackClip,
                runLeftClip,
                runRightClip,
                sprintForwardClip,
                jumpClip,
                jumpRunningClip,
                jumpRunningLandingClip,
                landToIdleClip,
                turnLeft90Clip,
                turnRight90Clip,
                upperBodyMask,
                bakeReport);

            EditorSceneManager.SaveScene(scene, ScenePath);
            PersistSceneReferencesAfterInitialSave();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MxAnimation system showcase scene created: " + ScenePath);
        }

        private static GameObject CreateRoot(PanelSettings panelSettings, VisualTreeAsset visualTree, StyleSheet styleSheet)
        {
            var root = new GameObject("MxAnimationSystemShowcaseRoot");
            root.AddComponent<DefaultInputService>();
            UIDocument document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);
            MxAnimationShowcaseDemoBootstrap bootstrap = root.AddComponent<MxAnimationShowcaseDemoBootstrap>();
            SetBootstrapUiReferences(bootstrap, document, visualTree, styleSheet);
            return root;
        }

        private static Transform CreateStation(string name, Vector3 position, string label)
        {
            var station = new GameObject(name);
            station.transform.position = position;
            station.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = name + "_Platform";
            platform.transform.SetParent(station.transform, false);
            platform.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            platform.transform.localScale = new Vector3(2.6f, 0.1f, 2.6f);
            AssignMaterial(platform, new Color(0.18f, 0.24f, 0.25f));
            Object.DestroyImmediate(platform.GetComponent<Collider>());

            GameObject text = new GameObject(name + "_Label");
            text.transform.SetParent(station.transform, false);
            text.transform.localPosition = new Vector3(0f, 2.05f, -0.82f);
            text.transform.localRotation = Quaternion.Euler(18f, 180f, 0f);
            TextMesh mesh = text.AddComponent<TextMesh>();
            mesh.text = label;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = 0.085f;
            mesh.fontSize = 36;
            mesh.color = new Color(0.88f, 0.96f, 0.96f);

            return station.transform;
        }

        private static void CreateArena()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "MxAnimationShowcaseArena";
            floor.transform.position = new Vector3(0f, -0.12f, 0f);
            floor.transform.localScale = new Vector3(13.5f, 0.08f, 4.5f);
            AssignMaterial(floor, new Color(0.10f, 0.13f, 0.14f));
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            GameObject rear = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rear.name = "MxAnimationShowcaseBackPanel";
            rear.transform.position = new Vector3(0f, 1.05f, 1.35f);
            rear.transform.localScale = new Vector3(13.6f, 2.1f, 0.08f);
            AssignMaterial(rear, new Color(0.08f, 0.12f, 0.13f));
            Object.DestroyImmediate(rear.GetComponent<Collider>());
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.055f, 0.06f);
            camera.transform.position = new Vector3(0f, 2.15f, -9.2f);
            camera.transform.LookAt(new Vector3(0f, 0.95f, 0f), Vector3.up);
            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.05f;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.18f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var fillObject = new GameObject("Soft Fill Light");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.intensity = 1.6f;
            fill.range = 9f;
            fillObject.transform.position = new Vector3(0f, 2.5f, -3.5f);
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "MxAnimationShowcasePanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 120f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void ConfigureDocument(UIDocument document, PanelSettings panelSettings, VisualTreeAsset visualTree)
        {
            var serialized = new SerializedObject(document);
            Set(serialized, "m_PanelSettings", panelSettings);
            Set(serialized, "sourceAsset", visualTree);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            EditorUtility.SetDirty(document);
        }

        private static void SetBootstrapUiReferences(
            MxAnimationShowcaseDemoBootstrap bootstrap,
            UIDocument document,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet)
        {
            var serialized = new SerializedObject(bootstrap);
            Set(serialized, "_document", document);
            Set(serialized, "_visualTree", visualTree);
            Set(serialized, "_styleSheet", styleSheet);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }

        private static void SetBootstrapReferences(
            MxAnimationShowcaseDemoBootstrap bootstrap,
            DefaultInputService input,
            UIDocument document,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Transform locomotionAnchor,
            Transform directionalAnchor,
            Transform layerAnchor,
            Transform overrideAnchor,
            GameObject skeletonModel,
            AnimationClip idleClip,
            AnimationClip walkForwardClip,
            AnimationClip walkBackClip,
            AnimationClip walkLeftClip,
            AnimationClip walkRightClip,
            AnimationClip runForwardClip,
            AnimationClip runBackClip,
            AnimationClip runLeftClip,
            AnimationClip runRightClip,
            AnimationClip sprintForwardClip,
            AnimationClip jumpClip,
            AnimationClip jumpRunningClip,
            AnimationClip jumpRunningLandingClip,
            AnimationClip landToIdleClip,
            AnimationClip turnLeft90Clip,
            AnimationClip turnRight90Clip,
            AvatarMask upperBodyMask,
            TextAsset bakeReport)
        {
            var serialized = new SerializedObject(bootstrap);
            Set(serialized, "_inputService", input);
            Set(serialized, "_document", document);
            Set(serialized, "_visualTree", visualTree);
            Set(serialized, "_styleSheet", styleSheet);
            Set(serialized, "_locomotionAnchor", locomotionAnchor);
            Set(serialized, "_directionalAnchor", directionalAnchor);
            Set(serialized, "_layerAnchor", layerAnchor);
            Set(serialized, "_overrideAnchor", overrideAnchor);
            Set(serialized, "_skeletonModel", skeletonModel);
            Set(serialized, "_idleClip", idleClip);
            Set(serialized, "_walkForwardClip", walkForwardClip);
            Set(serialized, "_walkBackClip", walkBackClip);
            Set(serialized, "_walkLeftClip", walkLeftClip);
            Set(serialized, "_walkRightClip", walkRightClip);
            Set(serialized, "_runForwardClip", runForwardClip);
            Set(serialized, "_runBackClip", runBackClip);
            Set(serialized, "_runLeftClip", runLeftClip);
            Set(serialized, "_runRightClip", runRightClip);
            Set(serialized, "_sprintForwardClip", sprintForwardClip);
            Set(serialized, "_jumpClip", jumpClip);
            Set(serialized, "_jumpRunningClip", jumpRunningClip);
            Set(serialized, "_jumpRunningLandingClip", jumpRunningLandingClip);
            Set(serialized, "_landToIdleClip", landToIdleClip);
            Set(serialized, "_turnLeft90Clip", turnLeft90Clip);
            Set(serialized, "_turnRight90Clip", turnRight90Clip);
            Set(serialized, "_upperBodyMask", upperBodyMask);
            Set(serialized, "_bakeReport", bakeReport);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }

        private static void PersistSceneReferencesAfterInitialSave()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find("MxAnimationSystemShowcaseRoot");
            if (root == null)
                throw new System.InvalidOperationException("Generated showcase scene is missing root object.");

            var input = root.GetComponent<DefaultInputService>();
            var document = root.GetComponent<UIDocument>();
            var bootstrap = root.GetComponent<MxAnimationShowcaseDemoBootstrap>();
            if (input == null || document == null || bootstrap == null)
                throw new System.InvalidOperationException("Generated showcase scene is missing required root components.");

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = LoadRequired<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = LoadRequired<StyleSheet>(UssPath);
            AnimationClip jumpClip = LoadRequired<AnimationClip>(JumpClipPath);
            TextAsset bakeReport = GenerateBakeReport(jumpClip);
            ConfigureDocument(document, panelSettings, visualTree);
            SetBootstrapReferences(
                bootstrap,
                input,
                document,
                visualTree,
                styleSheet,
                GameObject.Find("Station_1D_Locomotion")?.transform,
                GameObject.Find("Station_2D_Directional")?.transform,
                GameObject.Find("Station_Layer_Mask_Bridge")?.transform,
                GameObject.Find("Station_Mod_Override")?.transform,
                LoadRequired<GameObject>(SkeletonModelPath),
                LoadRequired<AnimationClip>(IdleClipPath),
                LoadRequired<AnimationClip>(WalkForwardClipPath),
                LoadRequired<AnimationClip>(WalkBackClipPath),
                LoadRequired<AnimationClip>(WalkLeftClipPath),
                LoadRequired<AnimationClip>(WalkRightClipPath),
                LoadRequired<AnimationClip>(RunForwardClipPath),
                LoadRequired<AnimationClip>(RunBackClipPath),
                LoadRequired<AnimationClip>(RunLeftClipPath),
                LoadRequired<AnimationClip>(RunRightClipPath),
                LoadRequired<AnimationClip>(SprintForwardClipPath),
                jumpClip,
                LoadRequired<AnimationClip>(JumpRunningClipPath),
                LoadRequired<AnimationClip>(JumpRunningLandingClipPath),
                LoadRequired<AnimationClip>(LandToIdleClipPath),
                LoadRequired<AnimationClip>(TurnLeft90ClipPath),
                LoadRequired<AnimationClip>(TurnRight90ClipPath),
                LoadRequired<AvatarMask>(UpperBodyMaskPath),
                bakeReport);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static TextAsset GenerateBakeReport(AnimationClip clip)
        {
            MxAnimationBakeEditorResult result = MxAnimationBakeEditorTool.BakeClipToFile(
                clip,
                MxAnimationBakeEditorTool.DefaultOutputRoot);
            if (!result.Success)
                Debug.LogWarning(result.ReportText);

            TextAsset report = AssetDatabase.LoadAssetAtPath<TextAsset>(result.OutputPath);
            if (report == null)
                throw new System.InvalidOperationException("MxAnimation bake report was not imported: " + result.OutputPath);

            return report;
        }

        private static T LoadRequired<T>(string path)
            where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new System.InvalidOperationException("Required asset missing: " + path);

            return asset;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void AssignMaterial(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
            var material = new Material(shader);
            material.color = color;
            renderer.sharedMaterial = material;
        }

        private static void Set(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }
    }
}
