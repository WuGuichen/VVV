using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public static class CreateCharacterTestSkillShowcaseScene
    {
        private static readonly FieldInfo DisableNoThemeWarningField =
            typeof(PanelSettings).GetField("m_DisableNoThemeWarning", BindingFlags.NonPublic | BindingFlags.Instance);

        private const string ScenePath = CharacterTestSkillShowcaseBootstrap.ScenePath;
        private const string UiFolderPath = "Assets/UI/MxFramework/CharacterTestSkillShowcase";
        private const string PanelSettingsPath = UiFolderPath + "/CharacterTestSkillShowcasePanelSettings.asset";
        private const string UxmlPath = UiFolderPath + "/CharacterTestSkillShowcaseHud.uxml";
        private const string UssPath = UiFolderPath + "/CharacterTestSkillShowcaseHud.uss";

        [MenuItem("MxFramework/CharacterTest/Generate Skill Showcase Scene", priority = 128)]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/UI", "MxFramework");
            EnsureFolder("Assets/UI/MxFramework", "CharacterTestSkillShowcase");
            AssetDatabase.ImportAsset(UxmlPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(UssPath, ImportAssetOptions.ForceSynchronousImport);

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = LoadRequired<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = LoadRequired<StyleSheet>(UssPath);
            CharacterTestSceneProfile profile =
                AssetDatabase.LoadAssetAtPath<CharacterTestSceneProfile>(CharacterTestSceneProfile.DefaultAssetPath);
            if (profile == null)
                throw new InvalidOperationException("CharacterTest scene profile is missing: " + CharacterTestSceneProfile.DefaultAssetPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.42f, 0.47f, 0.48f);

            UnityEngine.Camera stageCamera = CreateStageCamera();
            CreateDisplayCamera();
            CreateLights();
            CreateStage(out Transform stageRoot, out Transform floorRoot);
            GameObject root = CreateRoot(panelSettings, stageCamera, visualTree, styleSheet, stageRoot, floorRoot, profile);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[] { ScenePath }, ForceReserializeAssetsOptions.ReserializeAssets);
            RebindPersistedSceneReferences(ScenePath);
            Debug.Log("CharacterTest skill showcase scene created: " + ScenePath);
        }

        public static void ValidateSmoke()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("CharacterTest skill showcase scene did not load: " + ScenePath);

            UnityEngine.Camera camera = GameObject.FindWithTag("MainCamera")?.GetComponent<UnityEngine.Camera>();
            if (camera == null || !camera.CompareTag("MainCamera"))
                throw new InvalidOperationException("Skill showcase scene must contain a Main Camera tagged MainCamera.");

            UnityEngine.Camera displayCamera = GameObject.Find("Skill Showcase Display Camera")
                ?.GetComponent<UnityEngine.Camera>();
            if (displayCamera == null || displayCamera.targetTexture != null)
                throw new InvalidOperationException("Skill showcase scene must contain a Display camera without a render texture.");

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            bool hasDirectionalLight = false;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    hasDirectionalLight = true;
                    break;
                }
            }

            if (!hasDirectionalLight)
                throw new InvalidOperationException("Skill showcase scene must contain a Directional Light.");

            CharacterTestSkillShowcaseBootstrap bootstrap =
                UnityEngine.Object.FindAnyObjectByType<CharacterTestSkillShowcaseBootstrap>();
            UIDocument document = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            if (bootstrap == null || document == null)
                throw new InvalidOperationException("Skill showcase scene is missing bootstrap or UIDocument.");
            if (document.panelSettings == null || document.visualTreeAsset == null)
                throw new InvalidOperationException("Skill showcase UIDocument must bind PanelSettings and UXML.");

            RequireSerializedReference<UIDocument>(bootstrap, "_document");
            RequireSerializedReference<UnityEngine.Camera>(bootstrap, "_stageCamera");
            RequireSerializedReference<VisualTreeAsset>(bootstrap, "_visualTree");
            RequireSerializedReference<StyleSheet>(bootstrap, "_styleSheet");
            RequireSerializedReference<Transform>(bootstrap, "_stageRoot");
            RequireSerializedReference<Transform>(bootstrap, "_floorRoot");
            RequireSerializedReference<CharacterTestSceneProfile>(bootstrap, "_profile");

            if (GameObject.Find("CharacterTestSkillShowcaseFloor") == null)
                throw new InvalidOperationException("Skill showcase scene must contain the preview platform.");

            VisualElement tree = document.visualTreeAsset.CloneTree();
            RequireElement<TextField>(tree, "search-field");
            RequireElement<ScrollView>(tree, "ability-list");
            RequireElement<Image>(tree, "stage-view");
            RequireElement<Button>(tree, "play-button");
            RequireElement<Button>(tree, "replay-button");
            RequireElement<Button>(tree, "stop-button");
            RequireElement<Button>(tree, "step-button");
            RequireElement<Button>(tree, "reset-button");
            RequireElement<Toggle>(tree, "loop-toggle");
            RequireElement<Toggle>(tree, "lock-target-toggle");
            RequireElement<Toggle>(tree, "friendly-target-toggle");
            RequireElement<SliderInt>(tree, "target-count-slider");
            RequireElement<Slider>(tree, "target-distance-slider");
            RequireElement<Slider>(tree, "target-spacing-slider");
            RequireElement<Label>(tree, "telemetry-value");

            Debug.Log("CharacterTest skill showcase smoke validation passed.");
        }

        public static void ValidatePlayableSmoke()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("CharacterTest skill showcase scene did not load: " + ScenePath);

            CharacterTestSkillShowcaseBootstrap bootstrap =
                UnityEngine.Object.FindAnyObjectByType<CharacterTestSkillShowcaseBootstrap>();
            if (bootstrap == null)
                throw new InvalidOperationException("Skill showcase scene is missing bootstrap.");

            bootstrap.InitializeForValidation();
            if (!bootstrap.IsInitialized || bootstrap.AbilityCount <= 0)
                throw new InvalidOperationException("Skill showcase did not initialize ability entries.");

            RequireAbility(bootstrap, CharacterTestAbilityIds.BeamLanceAbilityId);
            RequireAbility(bootstrap, CharacterTestAbilityIds.ChannelBeamAbilityId);
            RequireAbility(bootstrap, CharacterTestAbilityIds.LaserLanceAbilityId);
            RequireAbility(bootstrap, CharacterTestAbilityIds.ChainLightningAbilityId);
            RequireAbility(bootstrap, CharacterTestAbilityIds.PoisonBottleAbilityId);

            if (!bootstrap.SelectAbilityForValidation(CharacterTestAbilityIds.ChainLightningAbilityId))
                throw new InvalidOperationException("Skill showcase could not select Chain Lightning.");
            if (!bootstrap.ReplayForValidation())
                throw new InvalidOperationException("Skill showcase Chain Lightning replay failed.");
            if (!bootstrap.LastReplayUsedCharacterTestHotbarInputForValidation
                || bootstrap.LastCastCommandSourceIdForValidation != CharacterTestRuntimeCommandSources.PlayerInput
                || bootstrap.LastCastCommandSlotNumberForValidation != CharacterTestAbilityHotbarRuntime.LastSlotNumber
                || bootstrap.LastCastCommandAbilityIdForValidation != CharacterTestAbilityIds.ChainLightningAbilityId)
            {
                throw new InvalidOperationException(
                    "Skill showcase replay did not use the CharacterTest hotbar input path.");
            }

            for (int i = 0; i < 90; i++)
                bootstrap.RunFrameForValidation(1f / 60f);

            if (bootstrap.ActiveRuntimeVfxCount <= 0 && string.IsNullOrWhiteSpace(bootstrap.LastStatus))
                throw new InvalidOperationException("Skill showcase replay produced no observable playback state.");

            bootstrap.DisposeForValidation();
            Debug.Log("CharacterTest skill showcase playable validation passed.");
        }

        private static void RequireAbility(CharacterTestSkillShowcaseBootstrap bootstrap, int abilityId)
        {
            if (!bootstrap.ContainsAbilityForValidation(abilityId))
                throw new InvalidOperationException("Skill showcase ability missing: " + abilityId);
        }

        private static GameObject CreateRoot(
            PanelSettings panelSettings,
            UnityEngine.Camera stageCamera,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Transform stageRoot,
            Transform floorRoot,
            CharacterTestSceneProfile profile)
        {
            var root = new GameObject("CharacterTestSkillShowcaseRoot");
            UIDocument document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);
            CharacterTestSkillShowcaseBootstrap bootstrap =
                root.AddComponent<CharacterTestSkillShowcaseBootstrap>();
            bootstrap.ConfigureSceneReferences(
                document,
                stageCamera,
                visualTree,
                styleSheet,
                stageRoot,
                floorRoot,
                profile);
            SetBootstrapReferences(bootstrap, document, stageCamera, visualTree, styleSheet, stageRoot, floorRoot, profile);
            return root;
        }

        private static UnityEngine.Camera CreateStageCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.062f, 0.064f);
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 140f;
            cameraObject.transform.position = new Vector3(0f, 5.2f, -13.5f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.2f, 6.5f), Vector3.up);
            return camera;
        }

        private static UnityEngine.Camera CreateDisplayCamera()
        {
            var cameraObject = new GameObject("Skill Showcase Display Camera");
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.029f, 0.030f);
            camera.cullingMask = 0;
            camera.depth = -100f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1f;
            cameraObject.transform.position = new Vector3(0f, -50f, 0f);
            cameraObject.transform.rotation = Quaternion.identity;
            return camera;
        }

        private static void CreateLights()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            var fillObject = new GameObject("Skill Showcase Fill Light");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.intensity = 1.6f;
            fill.range = 22f;
            fillObject.transform.position = new Vector3(0f, 5.2f, -5f);
        }

        private static void CreateStage(out Transform stageRoot, out Transform floorRoot)
        {
            GameObject stage = new GameObject("CharacterTestSkillShowcaseStage");
            stageRoot = stage.transform;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "CharacterTestSkillShowcaseFloor";
            floor.transform.SetParent(stageRoot, false);
            floor.transform.localPosition = new Vector3(0f, -0.06f, 10f);
            floor.transform.localScale = new Vector3(18f, 0.08f, 30f);
            AssignMaterial(floor, new Color(0.13f, 0.16f, 0.16f));
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            floorRoot = floor.transform;

            GameObject centerLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            centerLine.name = "CharacterTestSkillShowcaseForwardLine";
            centerLine.transform.SetParent(stageRoot, false);
            centerLine.transform.localPosition = new Vector3(0f, 0.01f, 10f);
            centerLine.transform.localScale = new Vector3(0.035f, 0.02f, 25f);
            AssignMaterial(centerLine, new Color(0.18f, 0.55f, 0.50f));
            UnityEngine.Object.DestroyImmediate(centerLine.GetComponent<Collider>());

            CreateRoleRing(stageRoot, "SkillShowcaseCasterRing", new Vector3(0f, 0.014f, 0f), new Color(0.16f, 0.62f, 0.55f, 0.9f));
            for (int i = 0; i < 8; i++)
            {
                CreateRoleRing(
                    stageRoot,
                    "SkillShowcaseTargetRing_" + (i + 1),
                    new Vector3(0f, 0.015f, 7f + i * 2.4f),
                    new Color(0.82f, 0.42f, 0.18f, 0.78f));
            }

            CreateRoleRing(
                stageRoot,
                "SkillShowcaseFriendlyRing",
                new Vector3(3.2f, 0.016f, 9.4f),
                new Color(0.25f, 0.55f, 0.96f, 0.75f));
        }

        private static void CreateRoleRing(Transform parent, string name, Vector3 position, Color color)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = name;
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = position;
            ring.transform.localScale = new Vector3(0.88f, 0.012f, 0.88f);
            AssignMaterial(ring, color);
            UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "CharacterTestSkillShowcasePanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 135f;
            StyleSheet runtimeTheme =
                AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
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

        private static void ConfigureDocument(UIDocument document, PanelSettings panelSettings, VisualTreeAsset visualTree)
        {
            var serialized = new SerializedObject(document);
            serialized.UpdateIfRequiredOrScript();
            SetRequired(serialized, "m_PanelSettings", panelSettings);
            SetRequired(serialized, "sourceAsset", visualTree);
            serialized.ApplyModifiedProperties();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            EditorUtility.SetDirty(document);
        }

        private static void SetBootstrapReferences(
            CharacterTestSkillShowcaseBootstrap bootstrap,
            UIDocument document,
            UnityEngine.Camera stageCamera,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Transform stageRoot,
            Transform floorRoot,
            CharacterTestSceneProfile profile)
        {
            var serialized = new SerializedObject(bootstrap);
            serialized.UpdateIfRequiredOrScript();
            SetRequired(serialized, "_document", document);
            SetRequired(serialized, "_stageCamera", stageCamera);
            SetRequired(serialized, "_visualTree", visualTree);
            SetRequired(serialized, "_styleSheet", styleSheet);
            SetRequired(serialized, "_stageRoot", stageRoot);
            SetRequired(serialized, "_floorRoot", floorRoot);
            SetRequired(serialized, "_profile", profile);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(bootstrap);
        }

        private static void RebindPersistedSceneReferences(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            CharacterTestSkillShowcaseBootstrap bootstrap =
                UnityEngine.Object.FindAnyObjectByType<CharacterTestSkillShowcaseBootstrap>();
            UIDocument document = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            UnityEngine.Camera stageCamera = GameObject.FindWithTag("MainCamera")?.GetComponent<UnityEngine.Camera>();
            Transform stageRoot = GameObject.Find("CharacterTestSkillShowcaseStage")?.transform;
            Transform floorRoot = GameObject.Find("CharacterTestSkillShowcaseFloor")?.transform;
            CharacterTestSceneProfile profile =
                AssetDatabase.LoadAssetAtPath<CharacterTestSceneProfile>(CharacterTestSceneProfile.DefaultAssetPath);
            if (bootstrap == null || document == null || stageCamera == null || stageRoot == null || floorRoot == null || profile == null)
                throw new InvalidOperationException("Skill showcase scene references could not be rebound.");

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = LoadRequired<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = LoadRequired<StyleSheet>(UssPath);
            ConfigureDocument(document, panelSettings, visualTree);
            bootstrap.ConfigureSceneReferences(document, stageCamera, visualTree, styleSheet, stageRoot, floorRoot, profile);
            SetBootstrapReferences(bootstrap, document, stageCamera, visualTree, styleSheet, stageRoot, floorRoot, profile);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void AssignMaterial(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Material material = target.name.IndexOf("Floor", System.StringComparison.Ordinal) >= 0
                ? CharacterTestWGameMaterialFactory.CreateEnvironment(target.name + "_SkillShowcaseMaterial", color)
                : CharacterTestWGameMaterialFactory.CreateMarker(target.name + "_SkillShowcaseMaterial", color);
            renderer.sharedMaterial = material;
        }

        private static T LoadRequired<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException("Required asset could not be loaded: " + path);

            return asset;
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
                throw new InvalidOperationException("Serialized property missing: " + propertyName);

            property.objectReferenceValue = value;
        }

        private static void RequireSerializedReference<T>(UnityEngine.Object owner, string propertyName)
            where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
                throw new InvalidOperationException("Serialized reference missing: " + owner.name + "." + propertyName);
            if (!(property.objectReferenceValue is T))
                throw new InvalidOperationException("Serialized reference has wrong type: " + owner.name + "." + propertyName);
        }

        private static T RequireElement<T>(VisualElement root, string name)
            where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
                throw new InvalidOperationException("Skill showcase UXML element missing: " + name);

            return element;
        }
    }
}
