using MxFramework.Demo.CombatAnimation;
using MxFramework.Input;
using MxFramework.Runtime.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    public static class CreateCombatAnimationDemoScene
    {
        private const string ScenePath = "Assets/Scenes/CombatAnimationDemo.unity";
        private const string PanelSettingsPath = "Assets/UI/MxFramework/CombatAnimationPanelSettings.asset";
        private const string UxmlPath = "Assets/UI/MxFramework/CombatAnimationHud.uxml";
        private const string UssPath = "Assets/UI/MxFramework/CombatAnimationHud.uss";
        private const string MappingPath = "Assets/UI/MxFramework/CombatAnimationAnimatorMapping.asset";

        [MenuItem("MxFramework/Combat/Generate Animation Demo Scene")]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/UI", "MxFramework");

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            CombatAnimatorMapping mapping = LoadOrCreateMapping();
            if (visualTree == null)
            {
                throw new System.InvalidOperationException("Combat Animation HUD UXML is missing: " + UxmlPath);
            }

            if (styleSheet == null)
            {
                throw new System.InvalidOperationException("Combat Animation HUD USS is missing: " + UssPath);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.56f, 0.58f, 0.62f);

            CreateCamera();
            CreateLight();
            GameObject root = CreateRoot(panelSettings, visualTree, styleSheet);
            Transform player = CreateActor("Player", PrimitiveType.Capsule, new Vector3(0f, 0.5f, 0f), new Color(0.18f, 0.52f, 0.86f), CombatAnimationDemoIds.PlayerEntityId.Value, mapping);
            Transform dummy = CreateActor("Dummy", PrimitiveType.Cube, new Vector3(1.8f, 0.5f, 0f), new Color(0.84f, 0.34f, 0.28f), CombatAnimationDemoIds.DummyEntityId.Value, mapping);
            CreateArena();

            var input = root.GetComponent<DefaultInputService>();
            var document = root.GetComponent<UIDocument>();
            var hud = root.GetComponent<CombatAnimationHudController>();
            var bootstrap = root.GetComponent<CombatAnimationDemoBootstrap>();
            bootstrap.ConfigureSceneReferences(input, document, visualTree, styleSheet, hud, player, dummy);
            SetBootstrapReferences(bootstrap, input, document, visualTree, styleSheet, hud, player, dummy);
            SetHudReferences(hud, document, visualTree, styleSheet);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Combat Animation demo scene created: " + ScenePath);
        }

        private static GameObject CreateRoot(PanelSettings panelSettings, VisualTreeAsset visualTree, StyleSheet styleSheet)
        {
            var root = new GameObject("CombatAnimationDemoRoot");
            root.AddComponent<DefaultInputService>();
            UIDocument document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);
            CombatAnimationHudController hud = root.AddComponent<CombatAnimationHudController>();
            hud.ConfigureAssets(document, visualTree, styleSheet);
            root.AddComponent<CombatAnimationDemoBootstrap>();
            return root;
        }

        private static Transform CreateActor(
            string name,
            PrimitiveType primitive,
            Vector3 position,
            Color color,
            int entityId,
            CombatAnimatorMapping mapping)
        {
            GameObject actor = GameObject.CreatePrimitive(primitive);
            actor.name = name;
            actor.transform.position = position;
            actor.transform.rotation = entityId == CombatAnimationDemoIds.PlayerEntityId.Value
                ? Quaternion.LookRotation(Vector3.right, Vector3.up)
                : Quaternion.LookRotation(Vector3.left, Vector3.up);
            actor.transform.localScale = primitive == PrimitiveType.Cube
                ? new Vector3(0.9f, 1.2f, 0.9f)
                : new Vector3(0.8f, 1f, 0.8f);
            AssignMaterial(actor, color);

            var animator = actor.AddComponent<DemoCombatAnimatorDriver>();
            animator.EntityId = new MxFramework.Combat.Core.CombatEntityId(entityId);
            animator.Mapping = mapping;

            var transformDriver = actor.AddComponent<CombatTransformDriver>();
            transformDriver.EntityId = new MxFramework.Combat.Core.CombatEntityId(entityId);
            transformDriver.InterpolationSpeed = 18f;
            return actor.transform;
        }

        private static void CreateArena()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "CombatAnimationArena";
            floor.transform.position = new Vector3(0.8f, -0.06f, 0f);
            floor.transform.localScale = new Vector3(7f, 0.1f, 5f);
            AssignMaterial(floor, new Color(0.22f, 0.25f, 0.27f));
            Object.DestroyImmediate(floor.GetComponent<Collider>());
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
            camera.transform.position = new Vector3(3.8f, 4.2f, -6.2f);
            camera.transform.rotation = Quaternion.Euler(55f, -34f, 0f);
            camera.fieldOfView = 45f;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
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

        private static void SetBootstrapReferences(
            CombatAnimationDemoBootstrap bootstrap,
            DefaultInputService input,
            UIDocument document,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            CombatAnimationHudController hud,
            Transform player,
            Transform dummy)
        {
            var serialized = new SerializedObject(bootstrap);
            Set(serialized, "_inputService", input);
            Set(serialized, "_document", document);
            Set(serialized, "_visualTree", visualTree);
            Set(serialized, "_styleSheet", styleSheet);
            Set(serialized, "_hud", hud);
            Set(serialized, "_player", player);
            Set(serialized, "_dummy", dummy);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }

        private static void SetHudReferences(
            CombatAnimationHudController hud,
            UIDocument document,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet)
        {
            var serialized = new SerializedObject(hud);
            Set(serialized, "_document", document);
            Set(serialized, "_visualTree", visualTree);
            Set(serialized, "_styleSheet", styleSheet);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "CombatAnimationPanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 100f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static CombatAnimatorMapping LoadOrCreateMapping()
        {
            CombatAnimatorMapping mapping = AssetDatabase.LoadAssetAtPath<CombatAnimatorMapping>(MappingPath);
            if (mapping == null)
            {
                mapping = ScriptableObject.CreateInstance<CombatAnimatorMapping>();
                mapping.ActionMappings.Add(new ActionAnimMapping { ActionId = CombatAnimationDemoIds.LightAttackActionId, AnimatorStateName = "LightAttack", CrossFadeDuration = 0.15f });
                mapping.ActionMappings.Add(new ActionAnimMapping { ActionId = CombatAnimationDemoIds.HeavyAttackActionId, AnimatorStateName = "HeavyAttack", CrossFadeDuration = 0.2f });
                mapping.ActionMappings.Add(new ActionAnimMapping { ActionId = CombatAnimationDemoIds.DodgeRollActionId, AnimatorStateName = "DodgeRoll", CrossFadeDuration = 0.1f });
                AssetDatabase.CreateAsset(mapping, MappingPath);
            }

            EditorUtility.SetDirty(mapping);
            return mapping;
        }

        private static void AssignMaterial(GameObject gameObject, Color color)
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color = color;
            renderer.sharedMaterial = material;
        }

        private static void Set(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
