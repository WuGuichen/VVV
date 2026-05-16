using MxFramework.Demo.MxAnimationSmoke;
using MxFramework.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    public static class CreateMxAnimationSmokeScene
    {
        private const string ScenePath = MxAnimationSmokeDemoBootstrap.ScenePath;
        private const string PanelSettingsPath = "Assets/UI/MxFramework/MxAnimationSmoke/MxAnimationSmokePanelSettings.asset";
        private const string UxmlPath = "Assets/UI/MxFramework/MxAnimationSmoke/MxAnimationSmokeHud.uxml";
        private const string UssPath = "Assets/UI/MxFramework/MxAnimationSmoke/MxAnimationSmokeHud.uss";
        private const string SkeletonModelPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/Models/Skeleton.fbx";
        private const string IdleClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_idle.anim";
        private const string WalkClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_walk_forward.anim";
        private const string RunClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_forward.anim";
        private const string JumpClipPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_jump.anim";
        private const string UpperBodyMaskPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/Masks/SkeletonUpperBody.mask";

        [MenuItem("MxFramework/MxAnimation/Generate Play Mode Smoke Scene", priority = 124)]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/UI/MxFramework", "MxAnimationSmoke");
            EnsureFolder("Assets/Art/MxFramework/Samples/Characters/Skeleton", "Masks");

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = LoadRequired<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = LoadRequired<StyleSheet>(UssPath);
            GameObject skeletonModel = LoadRequired<GameObject>(SkeletonModelPath);
            AnimationClip idleClip = LoadRequired<AnimationClip>(IdleClipPath);
            AnimationClip walkClip = LoadRequired<AnimationClip>(WalkClipPath);
            AnimationClip runClip = LoadRequired<AnimationClip>(RunClipPath);
            AnimationClip jumpClip = LoadRequired<AnimationClip>(JumpClipPath);
            AvatarMask upperBodyMask = LoadOrCreateUpperBodyMask();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.56f, 0.58f, 0.62f);

            CreateCamera();
            CreateLight();
            CreateArena();
            Transform actorParent = CreateActorParent();
            GameObject root = CreateRoot(panelSettings, visualTree, styleSheet);

            var input = root.GetComponent<DefaultInputService>();
            var document = root.GetComponent<UIDocument>();
            var bootstrap = root.GetComponent<MxAnimationSmokeDemoBootstrap>();
            bootstrap.ConfigureSceneReferences(
                input,
                document,
                visualTree,
                styleSheet,
                actorParent,
                skeletonModel,
                idleClip,
                walkClip,
                runClip,
                jumpClip,
                upperBodyMask);
            SetBootstrapReferences(
                bootstrap,
                input,
                document,
                visualTree,
                styleSheet,
                actorParent,
                skeletonModel,
                idleClip,
                walkClip,
                runClip,
                jumpClip,
                upperBodyMask);

            EditorSceneManager.SaveScene(scene, ScenePath);
            PersistSceneReferencesAfterInitialSave();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MxAnimation Play Mode smoke scene created: " + ScenePath);
        }

        private static GameObject CreateRoot(PanelSettings panelSettings, VisualTreeAsset visualTree, StyleSheet styleSheet)
        {
            var root = new GameObject("MxAnimationSmokeRoot");
            root.AddComponent<DefaultInputService>();
            UIDocument document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);
            MxAnimationSmokeDemoBootstrap bootstrap = root.AddComponent<MxAnimationSmokeDemoBootstrap>();
            SetBootstrapUiReferences(bootstrap, document, visualTree, styleSheet);
            return root;
        }

        private static Transform CreateActorParent()
        {
            var parent = new GameObject("SkeletonActor");
            parent.transform.position = Vector3.zero;
            parent.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            parent.transform.localScale = Vector3.one;
            return parent.transform;
        }

        private static void CreateArena()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "MxAnimationSmokeArena";
            floor.transform.position = new Vector3(0f, -0.06f, 0f);
            floor.transform.localScale = new Vector3(4.5f, 0.1f, 4.5f);
            AssignMaterial(floor, new Color(0.22f, 0.25f, 0.26f));
            Object.DestroyImmediate(floor.GetComponent<Collider>());
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
            camera.transform.position = new Vector3(0f, 1.45f, -3.35f);
            camera.transform.LookAt(new Vector3(0f, 0.75f, 0f), Vector3.up);
            camera.fieldOfView = 44f;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "MxAnimationSmokePanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 110f;
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
            MxAnimationSmokeDemoBootstrap bootstrap,
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
            MxAnimationSmokeDemoBootstrap bootstrap,
            DefaultInputService input,
            UIDocument document,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Transform actorParent,
            GameObject skeletonModel,
            AnimationClip idleClip,
            AnimationClip walkClip,
            AnimationClip runClip,
            AnimationClip jumpClip,
            AvatarMask upperBodyMask)
        {
            var serialized = new SerializedObject(bootstrap);
            Set(serialized, "_inputService", input);
            Set(serialized, "_document", document);
            Set(serialized, "_visualTree", visualTree);
            Set(serialized, "_styleSheet", styleSheet);
            Set(serialized, "_actorParent", actorParent);
            Set(serialized, "_skeletonModel", skeletonModel);
            Set(serialized, "_idleClip", idleClip);
            Set(serialized, "_walkForwardClip", walkClip);
            Set(serialized, "_runForwardClip", runClip);
            Set(serialized, "_jumpClip", jumpClip);
            Set(serialized, "_upperBodyMask", upperBodyMask);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }

        private static void PersistSceneReferencesAfterInitialSave()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find("MxAnimationSmokeRoot");
            GameObject actor = GameObject.Find("SkeletonActor");
            if (root == null || actor == null)
                throw new System.InvalidOperationException("Generated smoke scene is missing required root objects.");

            var input = root.GetComponent<DefaultInputService>();
            var document = root.GetComponent<UIDocument>();
            var bootstrap = root.GetComponent<MxAnimationSmokeDemoBootstrap>();
            if (input == null || document == null || bootstrap == null)
                throw new System.InvalidOperationException("Generated smoke scene is missing required root components.");

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = LoadRequired<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = LoadRequired<StyleSheet>(UssPath);
            GameObject skeletonModel = LoadRequired<GameObject>(SkeletonModelPath);
            AnimationClip idleClip = LoadRequired<AnimationClip>(IdleClipPath);
            AnimationClip walkClip = LoadRequired<AnimationClip>(WalkClipPath);
            AnimationClip runClip = LoadRequired<AnimationClip>(RunClipPath);
            AnimationClip jumpClip = LoadRequired<AnimationClip>(JumpClipPath);
            AvatarMask upperBodyMask = LoadOrCreateUpperBodyMask();

            ConfigureDocument(document, panelSettings, visualTree);
            SetBootstrapReferences(
                bootstrap,
                input,
                document,
                visualTree,
                styleSheet,
                actor.transform,
                skeletonModel,
                idleClip,
                walkClip,
                runClip,
                jumpClip,
                upperBodyMask);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static AvatarMask LoadOrCreateUpperBodyMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask { name = "SkeletonUpperBody" };
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }

            mask.name = "SkeletonUpperBody";
            SetBodyPart(mask, AvatarMaskBodyPart.Root, false);
            SetBodyPart(mask, AvatarMaskBodyPart.Body, true);
            SetBodyPart(mask, AvatarMaskBodyPart.Head, true);
            SetBodyPart(mask, AvatarMaskBodyPart.LeftArm, true);
            SetBodyPart(mask, AvatarMaskBodyPart.RightArm, true);
            SetBodyPart(mask, AvatarMaskBodyPart.LeftFingers, true);
            SetBodyPart(mask, AvatarMaskBodyPart.RightFingers, true);
            SetBodyPart(mask, AvatarMaskBodyPart.LeftLeg, false);
            SetBodyPart(mask, AvatarMaskBodyPart.RightLeg, false);
            SetBodyPart(mask, AvatarMaskBodyPart.LeftFootIK, false);
            SetBodyPart(mask, AvatarMaskBodyPart.RightFootIK, false);
            SetBodyPart(mask, AvatarMaskBodyPart.LeftHandIK, true);
            SetBodyPart(mask, AvatarMaskBodyPart.RightHandIK, true);
            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssets();
            return mask;
        }

        private static void SetBodyPart(AvatarMask mask, AvatarMaskBodyPart part, bool active)
        {
            if (part >= AvatarMaskBodyPart.LastBodyPart)
                return;

            mask.SetHumanoidBodyPartActive(part, active);
        }

        private static T LoadRequired<T>(string path)
            where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new System.InvalidOperationException("Required asset is missing: " + path);
            return asset;
        }

        private static void AssignMaterial(GameObject gameObject, Color color)
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer == null)
                return;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color = color;
            renderer.sharedMaterial = material;
        }

        private static void Set(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
