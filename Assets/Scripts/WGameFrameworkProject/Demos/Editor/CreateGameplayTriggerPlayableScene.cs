using System;
using MxFramework.Demo.GameplayTriggers;
using MxFramework.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Demo
{
    /// <summary>
    /// Materializes the Gameplay Trigger playable scene through Unity serialization.
    /// Run via Unity -batchmode -quit -projectPath . -executeMethod
    /// MxFramework.Demo.CreateGameplayTriggerPlayableScene.Create.
    /// </summary>
    internal static class CreateGameplayTriggerPlayableScene
    {
        private const string ScenePath =
            "Assets/Scenes/GameplayTriggerPlayable.unity";
        private const string UiFolderPath =
            "Assets/UI/MxFramework/GameplayTriggers";
        private const string PanelSettingsPath =
            UiFolderPath + "/GameplayTriggerPlayablePanelSettings.asset";
        private const string UxmlPath =
            UiFolderPath + "/GameplayTriggerPlayable.uxml";
        private const string UssPath =
            UiFolderPath + "/GameplayTriggerPlayable.uss";
        private const string MaterialFolderPath =
            "Assets/Materials/MxFramework/GameplayTriggers";
        private const string PlayerMaterialPath =
            MaterialFolderPath + "/GameplayTriggerPlayer.mat";
        private const string CheckpointMaterialPath =
            MaterialFolderPath + "/GameplayTriggerCheckpoint.mat";
        private const string DoorMaterialPath =
            MaterialFolderPath + "/GameplayTriggerDoor.mat";
        private const string TrapMaterialPath =
            MaterialFolderPath + "/GameplayTriggerTrap.mat";
        private const string WorldMaterialPath =
            MaterialFolderPath + "/GameplayTriggerWorld.mat";
        private const string RuntimeThemePath =
            "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss";
        private const string InputActionsPath =
            "Assets/Input/MxFramework/Config/MxFrameworkInputActions.inputactions";
        private const float ShowcaseCameraDepth = 2f;

        [MenuItem("MxFramework/Gameplay Triggers/Create Playable Scene")]
        public static void Create()
        {
            EnsureFolders();
            AssetDatabase.ImportAsset(UxmlPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(UssPath, ImportAssetOptions.ForceSynchronousImport);

            LoadOrCreatePanelSettings();
            LoadOrCreateMaterial(
                PlayerMaterialPath,
                new Color(0.18f, 0.78f, 0.62f));
            LoadOrCreateMaterial(
                CheckpointMaterialPath,
                new Color(0.18f, 0.58f, 0.92f));
            LoadOrCreateMaterial(
                DoorMaterialPath,
                new Color(0.92f, 0.58f, 0.16f));
            LoadOrCreateMaterial(
                TrapMaterialPath,
                new Color(0.90f, 0.24f, 0.28f));
            LoadOrCreateMaterial(
                WorldMaterialPath,
                new Color(0.16f, 0.19f, 0.23f));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            RequireAllPersistentAssets("reload-after-refresh");

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            PanelSettings panelSettings = RequirePersistentAsset<PanelSettings>(
                "reload-after-new-scene",
                PanelSettingsPath);
            VisualTreeAsset visualTree = RequirePersistentAsset<VisualTreeAsset>(
                "reload-after-new-scene",
                UxmlPath);
            StyleSheet styleSheet = RequirePersistentAsset<StyleSheet>(
                "reload-after-new-scene",
                UssPath);
            InputActionAsset inputActions = RequirePersistentAsset<InputActionAsset>(
                "reload-after-new-scene",
                InputActionsPath);
            Material playerMaterial = RequirePersistentAsset<Material>(
                "reload-after-new-scene",
                PlayerMaterialPath);
            Material checkpointMaterial = RequirePersistentAsset<Material>(
                "reload-after-new-scene",
                CheckpointMaterialPath);
            Material doorMaterial = RequirePersistentAsset<Material>(
                "reload-after-new-scene",
                DoorMaterialPath);
            Material trapMaterial = RequirePersistentAsset<Material>(
                "reload-after-new-scene",
                TrapMaterialPath);
            Material worldMaterial = RequirePersistentAsset<Material>(
                "reload-after-new-scene",
                WorldMaterialPath);
            RenderSettings.ambientLight = new Color(0.47f, 0.51f, 0.57f);
            CreateCamera();
            CreateLight();
            CreateWorld(worldMaterial);

            Transform player = CreateView(
                "GameplayTriggerPlayer",
                PrimitiveType.Capsule,
                new Vector3(-4f, 0.55f, 0f),
                new Vector3(0.65f, 0.55f, 0.65f),
                playerMaterial);
            Transform checkpoint = CreateView(
                "GameplayTriggerCheckpoint",
                PrimitiveType.Cube,
                new Vector3(-2f, 0.04f, 0f),
                new Vector3(1f, 0.08f, 2f),
                checkpointMaterial);
            Transform door = CreateView(
                "GameplayTriggerDoor",
                PrimitiveType.Cube,
                new Vector3(0f, 1f, 0f),
                new Vector3(0.4f, 2f, 6f),
                doorMaterial);
            Transform trap = CreateView(
                "GameplayTriggerTrap",
                PrimitiveType.Cube,
                new Vector3(3f, 0.04f, 0f),
                new Vector3(1f, 0.08f, 2f),
                trapMaterial);
            CreatePlayableRoot(
                panelSettings,
                visualTree,
                styleSheet,
                inputActions,
                player,
                checkpoint,
                door,
                trap);
            LogAllPersistentAssets(
                "post-assignment",
                panelSettings,
                visualTree,
                styleSheet,
                inputActions,
                playerMaterial,
                checkpointMaterial,
                doorMaterial,
                trapMaterial,
                worldMaterial);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException(
                    "Gameplay Trigger playable scene could not be saved: " + ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            ValidateSavedScene(
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single));
            Debug.Log("Gameplay Trigger playable scene created: " + ScenePath);
        }

        private static void CreatePlayableRoot(
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            InputActionAsset inputActions,
            Transform player,
            Transform checkpoint,
            Transform door,
            Transform trap)
        {
            var root = new GameObject("GameplayTriggerPlayableRoot");
            var inputService = root.AddComponent<InputService>();
            ConfigureInputService(inputService, inputActions);
            var document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);

            var showcase = root.AddComponent<GameplayTriggerPlayableShowcaseRoot>();
            var serialized = new SerializedObject(showcase);
            SetRequired(serialized, "_document", document);
            SetRequired(serialized, "_panelSettings", panelSettings);
            SetRequired(serialized, "_visualTree", visualTree);
            SetRequired(serialized, "_styleSheet", styleSheet);
            SetRequired(serialized, "_inputService", inputService);
            SetRequired(serialized, "_playerView", player);
            SetRequired(serialized, "_checkpointView", checkpoint);
            SetRequired(serialized, "_doorView", door);
            SetRequired(serialized, "_trapView", trap);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();
            EnsureReference(serialized, "_document", document);
            EnsureReference(serialized, "_panelSettings", panelSettings);
            EnsureReference(serialized, "_visualTree", visualTree);
            EnsureReference(serialized, "_styleSheet", styleSheet);
            EnsureReference(serialized, "_inputService", inputService);
            EnsureReference(serialized, "_playerView", player);
            EnsureReference(serialized, "_checkpointView", checkpoint);
            EnsureReference(serialized, "_doorView", door);
            EnsureReference(serialized, "_trapView", trap);
            EditorUtility.SetDirty(showcase);
            EditorUtility.SetDirty(root);
        }

        private static void ConfigureInputService(
            InputService inputService,
            InputActionAsset inputActions)
        {
            var serialized = new SerializedObject(inputService);
            SetRequired(serialized, "_inputActions", inputActions);
            SetRequired(serialized, "_cloneActionAsset", true);
            SetRequired(serialized, "_initialContext", (int)InputContext.Gameplay);
            SetRequired(serialized, "_enqueuePressedCommands", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();
            EnsureReference(serialized, "_inputActions", inputActions);
            EnsureBool(serialized, "_cloneActionAsset", true);
            EnsureInt(serialized, "_initialContext", (int)InputContext.Gameplay);
            EnsureBool(serialized, "_enqueuePressedCommands", false);
            EditorUtility.SetDirty(inputService);
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
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();
            EnsureReference(serialized, "m_PanelSettings", panelSettings);
            EnsureReference(serialized, "sourceAsset", visualTree);
            EditorUtility.SetDirty(document);
        }

        private static void CreateWorld(Material material)
        {
            CreateView(
                "GameplayTriggerWorld",
                PrimitiveType.Cube,
                new Vector3(0f, -0.12f, 0f),
                new Vector3(12f, 0.2f, 8f),
                material);
            CreateView(
                "GameplayTriggerNorthBoundary",
                PrimitiveType.Cube,
                new Vector3(0f, 0.18f, 3.5f),
                new Vector3(12f, 0.5f, 0.15f),
                material);
            CreateView(
                "GameplayTriggerSouthBoundary",
                PrimitiveType.Cube,
                new Vector3(0f, 0.18f, -3.5f),
                new Vector3(12f, 0.5f, 0.15f),
                material);
        }

        private static Transform CreateView(
            string name,
            PrimitiveType primitive,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject view = GameObject.CreatePrimitive(primitive);
            view.name = name;
            view.transform.position = position;
            view.transform.localScale = scale;
            Collider collider = view.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            Renderer renderer = view.GetComponent<Renderer>();
            if (renderer == null)
                throw new InvalidOperationException(name + " has no Renderer.");
            renderer.sharedMaterial = material;
            return view.transform;
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            UnityEngine.Camera camera =
                cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.085f);
            camera.depth = ShowcaseCameraDepth;
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            cameraObject.transform.position = new Vector3(0f, 7.2f, -8.4f);
            cameraObject.transform.rotation = Quaternion.Euler(42f, 0f, 0f);
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            lightObject.transform.rotation = Quaternion.Euler(52f, -34f, 0f);
        }

        private static void ValidateSavedScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                throw new InvalidOperationException(
                    "Gameplay Trigger playable scene did not reopen for validation.");

            PanelSettings panelSettings = RequirePersistentAsset<PanelSettings>(
                "reopen-validation",
                PanelSettingsPath);
            VisualTreeAsset visualTree = RequirePersistentAsset<VisualTreeAsset>(
                "reopen-validation",
                UxmlPath);
            StyleSheet styleSheet = RequirePersistentAsset<StyleSheet>(
                "reopen-validation",
                UssPath);
            InputActionAsset inputActions =
                RequirePersistentAsset<InputActionAsset>(
                    "reopen-validation",
                    InputActionsPath);
            Material playerMaterial = RequirePersistentAsset<Material>(
                "reopen-validation",
                PlayerMaterialPath);
            Material checkpointMaterial =
                RequirePersistentAsset<Material>(
                    "reopen-validation",
                    CheckpointMaterialPath);
            Material doorMaterial = RequirePersistentAsset<Material>(
                "reopen-validation",
                DoorMaterialPath);
            Material trapMaterial = RequirePersistentAsset<Material>(
                "reopen-validation",
                TrapMaterialPath);
            Material worldMaterial = RequirePersistentAsset<Material>(
                "reopen-validation",
                WorldMaterialPath);

            GameplayTriggerPlayableShowcaseRoot[] showcases =
                Object.FindObjectsByType<GameplayTriggerPlayableShowcaseRoot>(
                    FindObjectsInactive.Include);
            if (showcases.Length != 1)
                throw new InvalidOperationException(
                    "Gameplay Trigger playable scene must contain exactly one showcase root.");

            GameplayTriggerPlayableShowcaseRoot showcase = showcases[0];
            UIDocument document = showcase.GetComponent<UIDocument>();
            InputService inputService = showcase.GetComponent<InputService>();
            if (document == null || inputService == null)
                throw new InvalidOperationException(
                    "Gameplay Trigger playable root is missing UIDocument or InputService.");
            if (showcase.GetComponent<DefaultInputService>() != null)
                throw new InvalidOperationException(
                    "Gameplay Trigger playable must use the explicit InputService asset path.");
            if (document.panelSettings != panelSettings ||
                document.visualTreeAsset != visualTree)
            {
                throw new InvalidOperationException(
                    "Gameplay Trigger playable UIDocument references did not round-trip.");
            }

            Transform player = RequireStage("GameplayTriggerPlayer");
            Transform checkpoint = RequireStage("GameplayTriggerCheckpoint");
            Transform door = RequireStage("GameplayTriggerDoor");
            Transform trap = RequireStage("GameplayTriggerTrap");
            Transform world = RequireStage("GameplayTriggerWorld");
            Transform northBoundary = RequireStage("GameplayTriggerNorthBoundary");
            Transform southBoundary = RequireStage("GameplayTriggerSouthBoundary");
            EnsureStageMaterial(player, playerMaterial);
            EnsureStageMaterial(checkpoint, checkpointMaterial);
            EnsureStageMaterial(door, doorMaterial);
            EnsureStageMaterial(trap, trapMaterial);
            EnsureStageMaterial(world, worldMaterial);
            EnsureStageMaterial(northBoundary, worldMaterial);
            EnsureStageMaterial(southBoundary, worldMaterial);

            var rootSerialized = new SerializedObject(showcase);
            EnsureReference(rootSerialized, "_document", document);
            EnsureReference(rootSerialized, "_panelSettings", panelSettings);
            EnsureReference(rootSerialized, "_visualTree", visualTree);
            EnsureReference(rootSerialized, "_styleSheet", styleSheet);
            EnsureReference(rootSerialized, "_inputService", inputService);
            EnsureReference(rootSerialized, "_playerView", player);
            EnsureReference(rootSerialized, "_checkpointView", checkpoint);
            EnsureReference(rootSerialized, "_doorView", door);
            EnsureReference(rootSerialized, "_trapView", trap);

            var inputSerialized = new SerializedObject(inputService);
            EnsureReference(inputSerialized, "_inputActions", inputActions);
            EnsureBool(inputSerialized, "_cloneActionAsset", true);
            EnsureInt(inputSerialized, "_initialContext", (int)InputContext.Gameplay);
            EnsureBool(inputSerialized, "_enqueuePressedCommands", false);

            Collider[] colliders = Object.FindObjectsByType<Collider>(
                FindObjectsInactive.Include);
            Rigidbody[] rigidbodies = Object.FindObjectsByType<Rigidbody>(
                FindObjectsInactive.Include);
            if (colliders.Length != 0 || rigidbodies.Length != 0)
                throw new InvalidOperationException(
                    "Gameplay Trigger stage views must not contain Collider or Rigidbody components.");
        }

        private static Transform RequireStage(string name)
        {
            GameObject stage = GameObject.Find(name);
            if (stage == null)
                throw new InvalidOperationException(
                    "Gameplay Trigger playable stage object is missing: " + name);
            return stage.transform;
        }

        private static void RequireAllPersistentAssets(string stage)
        {
            RequirePersistentAsset<PanelSettings>(stage, PanelSettingsPath);
            RequirePersistentAsset<VisualTreeAsset>(stage, UxmlPath);
            RequirePersistentAsset<StyleSheet>(stage, UssPath);
            RequirePersistentAsset<InputActionAsset>(stage, InputActionsPath);
            RequirePersistentAsset<Material>(stage, PlayerMaterialPath);
            RequirePersistentAsset<Material>(stage, CheckpointMaterialPath);
            RequirePersistentAsset<Material>(stage, DoorMaterialPath);
            RequirePersistentAsset<Material>(stage, TrapMaterialPath);
            RequirePersistentAsset<Material>(stage, WorldMaterialPath);
        }

        private static T RequirePersistentAsset<T>(string stage, string path)
            where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException(
                    "Gameplay Trigger playable expected asset is unavailable at " +
                    stage + ": " + path);
            LogPersistentAsset(stage, path, asset);
            return asset;
        }

        private static void LogAllPersistentAssets(
            string stage,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            InputActionAsset inputActions,
            Material playerMaterial,
            Material checkpointMaterial,
            Material doorMaterial,
            Material trapMaterial,
            Material worldMaterial)
        {
            LogPersistentAsset(stage, PanelSettingsPath, panelSettings);
            LogPersistentAsset(stage, UxmlPath, visualTree);
            LogPersistentAsset(stage, UssPath, styleSheet);
            LogPersistentAsset(stage, InputActionsPath, inputActions);
            LogPersistentAsset(stage, PlayerMaterialPath, playerMaterial);
            LogPersistentAsset(stage, CheckpointMaterialPath, checkpointMaterial);
            LogPersistentAsset(stage, DoorMaterialPath, doorMaterial);
            LogPersistentAsset(stage, TrapMaterialPath, trapMaterial);
            LogPersistentAsset(stage, WorldMaterialPath, worldMaterial);
        }

        private static void LogPersistentAsset(
            string stage,
            string path,
            Object asset)
        {
            RequirePersistentIdentity(
                stage,
                path,
                asset,
                out string guid,
                out long localId);
            Debug.Log(
                "Gameplay Trigger asset [" + stage + "] " + path +
                " guid=" + guid + " localId=" + localId);
        }

        private static void RequirePersistentIdentity(
            string stage,
            string path,
            Object asset,
            out string guid,
            out long localId)
        {
            if (asset == null || !EditorUtility.IsPersistent(asset) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    asset,
                    out guid,
                    out localId) ||
                string.IsNullOrEmpty(guid) || localId == 0L)
            {
                throw new InvalidOperationException(
                    "Gameplay Trigger playable asset identity is invalid at " +
                    stage + ": " + path);
            }
        }

        private static void EnsureStageMaterial(
            Transform stage,
            Material expected)
        {
            Renderer renderer = stage.GetComponent<Renderer>();
            if (renderer == null)
                throw new InvalidOperationException(
                    "Gameplay Trigger playable stage has no Renderer: " + stage.name);
            EnsureSamePersistentAsset(
                "renderer-" + stage.name,
                AssetDatabase.GetAssetPath(expected),
                expected,
                renderer.sharedMaterial);
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings =
                AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "GameplayTriggerPlayablePanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            if (settings == null)
                return null;

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 100f;
            StyleSheet runtimeTheme =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(RuntimeThemePath);
            if (runtimeTheme != null)
            {
                var serialized = new SerializedObject(settings);
                SetRequired(serialized, "themeUss", runtimeTheme);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static Material LoadOrCreateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (path == WorldMaterialPath)
                    material = WGameDemoMaterialFactory.CreateEnvironment(name, color);
                else if (path == PlayerMaterialPath)
                    material = WGameDemoMaterialFactory.CreateCharacter(name, color);
                else
                    material = WGameDemoMaterialFactory.CreateProp(name, color, highlighted: path == CheckpointMaterialPath);
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, path);
            }

            if (path == WorldMaterialPath)
                WGameDemoMaterialFactory.ConfigureEnvironment(material, color);
            else if (path == PlayerMaterialPath)
            {
                material.shader = WGameDemoMaterialFactory.RequireShader(WGameDemoMaterialFactory.CharacterShaderName);
                material.SetColor("_Color", color);
            }
            else
                WGameDemoMaterialFactory.ConfigureProp(material, color, highlighted: path == CheckpointMaterialPath);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets/Materials", "MxFramework");
            EnsureFolder("Assets/Materials/MxFramework", "GameplayTriggers");
            EnsureFolder("Assets", "UI");
            EnsureFolder("Assets/UI", "MxFramework");
            EnsureFolder("Assets/UI/MxFramework", "GameplayTriggers");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void SetRequired(
            SerializedObject serialized,
            string propertyName,
            Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(
                    "Missing serialized property: " + propertyName +
                    " on " + serialized.targetObject);
            property.objectReferenceValue = value;
        }

        private static void SetRequired(
            SerializedObject serialized,
            string propertyName,
            bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(
                    "Missing serialized property: " + propertyName +
                    " on " + serialized.targetObject);
            property.boolValue = value;
        }

        private static void SetRequired(
            SerializedObject serialized,
            string propertyName,
            int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(
                    "Missing serialized property: " + propertyName +
                    " on " + serialized.targetObject);
            property.intValue = value;
        }

        private static void EnsureReference(
            SerializedObject serialized,
            string propertyName,
            Object expected)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(
                    "Missing serialized reference: " + propertyName +
                    " on " + serialized.targetObject);

            if (expected == null)
                throw new InvalidOperationException(
                    "Expected serialized reference is unavailable: " + propertyName +
                    " on " + serialized.targetObject);
            Object actual = property.objectReferenceValue;
            if (actual == null)
                throw new InvalidOperationException(
                    "Actual serialized reference is unavailable: " + propertyName +
                    " on " + serialized.targetObject);

            if (EditorUtility.IsPersistent(expected))
            {
                EnsureSamePersistentAsset(
                    "serialized-" + propertyName,
                    AssetDatabase.GetAssetPath(expected),
                    expected,
                    actual);
                return;
            }

            if (EditorUtility.IsPersistent(actual) ||
                !actual.GetEntityId().Equals(expected.GetEntityId()))
            {
                throw new InvalidOperationException(
                    "Serialized scene reference does not match: " + propertyName +
                    " on " + serialized.targetObject);
            }
        }

        private static void EnsureSamePersistentAsset(
            string stage,
            string path,
            Object expected,
            Object actual)
        {
            if (expected == null || actual == null)
                throw new InvalidOperationException(
                    "Gameplay Trigger playable persistent reference is unavailable at " +
                    stage + ": " + path);

            RequirePersistentIdentity(
                stage + "-expected",
                path,
                expected,
                out string expectedGuid,
                out long expectedLocalId);
            RequirePersistentIdentity(
                stage + "-actual",
                path,
                actual,
                out string actualGuid,
                out long actualLocalId);
            if (!string.Equals(expectedGuid, actualGuid, StringComparison.Ordinal) ||
                expectedLocalId != actualLocalId)
            {
                throw new InvalidOperationException(
                    "Gameplay Trigger playable persistent reference does not match at " +
                    stage + ": " + path);
            }
        }

        private static void EnsureBool(
            SerializedObject serialized,
            string propertyName,
            bool expected)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.boolValue != expected)
                throw new InvalidOperationException(
                    "Failed to assign serialized bool: " + propertyName +
                    " on " + serialized.targetObject);
        }

        private static void EnsureInt(
            SerializedObject serialized,
            string propertyName,
            int expected)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.intValue != expected)
                throw new InvalidOperationException(
                    "Failed to assign serialized integer: " + propertyName +
                    " on " + serialized.targetObject);
        }
    }
}
