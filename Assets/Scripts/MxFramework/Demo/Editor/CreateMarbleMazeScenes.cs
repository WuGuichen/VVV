using MxFramework.Demo.MarbleMaze;
using MxFramework.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    /// <summary>
    /// Creates playable Marble Maze scenes through Unity serialization.
    /// Run via: Unity -batchmode -quit -projectPath . -executeMethod MxFramework.Demo.CreateMarbleMazeScenes.Create
    /// </summary>
    public static class CreateMarbleMazeScenes
    {
        private const string BootScenePath = "Assets/Scenes/MarbleMazeBoot.unity";
        private const string GameplayScenePath = "Assets/Scenes/MarbleMazeGameplay.unity";
        private const string PanelSettingsPath = "Assets/UI/MxFramework/MarbleMaze/MarbleMazePanelSettings.asset";
        private const string UxmlPath = "Assets/UI/MxFramework/MarbleMaze/MarbleMazePlayableDemo.uxml";
        private const string UssPath = "Assets/UI/MxFramework/MarbleMaze/MarbleMazePlayableDemo.uss";
        private const string MaterialFolderPath = "Assets/Art/MxFramework/MarbleMaze";
        private const string BoardMaterialPath = MaterialFolderPath + "/MarbleMazeBoard.mat";
        private const string WallMaterialPath = MaterialFolderPath + "/MarbleMazeWall.mat";
        private const string BallMaterialPath = MaterialFolderPath + "/MarbleMazeBall.mat";
        private const string CheckpointMaterialPath = MaterialFolderPath + "/MarbleMazeCheckpoint.mat";
        private const string FinishMaterialPath = MaterialFolderPath + "/MarbleMazeFinish.mat";

        [MenuItem("MxFramework/Marble Maze/Create Playable Scenes")]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/UI", "MxFramework");
            EnsureFolder("Assets/UI/MxFramework", "MarbleMaze");
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "MxFramework");
            EnsureFolder("Assets/Art/MxFramework", "MarbleMaze");

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            MarbleMazeMaterials materials = LoadOrCreateMaterials();

            CreateGameplayScene(GameplayScenePath, panelSettings, visualTree, styleSheet, materials);
            CreateGameplayScene(
                BootScenePath,
                AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath),
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath),
                AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath),
                materials);
            AddScenesToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Marble Maze playable scenes created: " + BootScenePath + ", " + GameplayScenePath);
        }

        private static void CreateGameplayScene(
            string scenePath,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            MarbleMazeMaterials materials)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            panelSettings = panelSettings != null
                ? panelSettings
                : AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            visualTree = visualTree != null
                ? visualTree
                : AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            styleSheet = styleSheet != null
                ? styleSheet
                : AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            RenderSettings.ambientLight = new Color(0.58f, 0.60f, 0.64f);

            CreateCamera();
            CreateLight();

            var root = new GameObject("MarbleMazePlayableRoot");
            root.AddComponent<DefaultInputService>();
            var demo = root.AddComponent<MarbleMazePhysicsDemo>();
            root.AddComponent<MarbleMazeAppFlowDemo>();
            UIDocument document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);

            Transform board = CreateBoard(root.transform, materials.Board);
            Transform ball = CreateBall(root.transform, materials.Ball);
            CreateWalls(board, materials.Wall);
            Vector3 spawnPosition = new Vector3(0f, 0.65f, -4f);
            Vector3 checkpoint0 = new Vector3(-2.7f, 0.55f, -1.5f);
            Vector3 checkpoint1 = new Vector3(2.4f, 0.55f, 0.7f);
            Vector3 finish = new Vector3(0.0f, 0.55f, 3.15f);
            CreateRouteLine(board, "Route_StartToGemA", spawnPosition, checkpoint0, materials.Checkpoint);
            CreateRouteLine(board, "Route_GemAToGemB", checkpoint0, checkpoint1, materials.Checkpoint);
            CreateRouteLine(board, "Route_GemBToExit", checkpoint1, finish, materials.Finish);
            CreateCheckpoint(board, 0, checkpoint0, false, materials.Checkpoint);
            CreateCheckpoint(board, 1, checkpoint1, false, materials.Checkpoint);
            CreateCheckpoint(board, 2, finish, true, materials.Finish);

            var serialized = new SerializedObject(demo);
            Set(serialized, "_document", document);
            Set(serialized, "_visualTree", visualTree);
            Set(serialized, "_styleSheet", styleSheet);
            Set(serialized, "_ball", ball);
            Set(serialized, "_board", board);
            Set(serialized, "_checkpointCount", 2);
            Set(serialized, "_targetTimeSeconds", 45f);
            Set(serialized, "_maxTiltDegrees", 7f);
            Set(serialized, "_tiltResponse", 4.5f);
            Set(serialized, "_tiltAcceleration", 7.5f);
            Set(serialized, "_simulationFramesPerSecond", 60f);
            Set(serialized, "_maxRuntimeFramesPerUpdate", 4);
            Set(serialized, "_targetFrameRate", 60);
            Set(serialized, "_ballSpawnPosition", spawnPosition);
            Set(serialized, "_checkpoint0Position", checkpoint0);
            Set(serialized, "_checkpoint1Position", checkpoint1);
            Set(serialized, "_exitPosition", finish);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void ConfigureDocument(
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree)
        {
            var serialized = new SerializedObject(document);
            Set(serialized, "m_PanelSettings", panelSettings);
            Set(serialized, "sourceAsset", visualTree);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            EditorUtility.SetDirty(document);
        }

        private static Transform CreateBoard(Transform parent, Material boardMaterial)
        {
            GameObject pivot = new GameObject("MarbleMazeBoardPivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = Vector3.zero;

            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "MarbleMazeBoard";
            board.transform.SetParent(pivot.transform, false);
            board.transform.localScale = new Vector3(9f, 0.25f, 9f);
            board.transform.localPosition = Vector3.zero;
            AssignMaterial(board, boardMaterial);
            RemoveCollider(board);
            return pivot.transform;
        }

        private static Transform CreateBall(Transform parent, Material ballMaterial)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "MarbleMazeBall";
            ball.transform.SetParent(parent, false);
            ball.transform.localScale = Vector3.one * 0.65f;
            ball.transform.localPosition = new Vector3(0f, 0.65f, -4f);
            AssignMaterial(ball, ballMaterial);
            RemoveCollider(ball);
            return ball.transform;
        }

        private static void CreateWalls(Transform parent, Material wallMaterial)
        {
            CreateWall(parent, "Wall_North", new Vector3(0f, 0.55f, 4.65f), new Vector3(9.6f, 0.8f, 0.3f), wallMaterial);
            CreateWall(parent, "Wall_South", new Vector3(0f, 0.55f, -4.65f), new Vector3(9.6f, 0.8f, 0.3f), wallMaterial);
            CreateWall(parent, "Wall_East", new Vector3(4.65f, 0.55f, 0f), new Vector3(0.3f, 0.8f, 9.6f), wallMaterial);
            CreateWall(parent, "Wall_West", new Vector3(-4.65f, 0.55f, 0f), new Vector3(0.3f, 0.8f, 9.6f), wallMaterial);
        }

        private static void CreateWall(Transform parent, string name, Vector3 position, Vector3 scale, Material wallMaterial)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position;
            wall.transform.localScale = scale;
            AssignMaterial(wall, wallMaterial);
            RemoveCollider(wall);
        }

        private static void CreateCheckpoint(
            Transform parent,
            int index,
            Vector3 position,
            bool finish,
            Material markerMaterial)
        {
            string markerName = finish ? "MarbleMazeExit" : "MarbleMazeGem_" + (char)('A' + index);
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = markerName + "_Pad";
            pad.transform.SetParent(parent, false);
            pad.transform.localPosition = position;
            pad.transform.localScale = new Vector3(0.9f, 0.055f, 0.9f);
            AssignMaterial(pad, markerMaterial);
            RemoveCollider(pad);

            CreateMarkerLabel(parent, finish ? "EXIT" : "GEM " + (char)('A' + index), position + new Vector3(0f, 0.72f, 0f), finish);
        }

        private static void CreateRouteLine(Transform parent, string name, Vector3 from, Vector3 to, Material material)
        {
            Vector3 start = new Vector3(from.x, 0.16f, from.z);
            Vector3 end = new Vector3(to.x, 0.16f, to.z);
            Vector3 delta = end - start;
            if (delta.sqrMagnitude < 0.001f)
                return;

            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = name;
            line.transform.SetParent(parent, false);
            line.transform.localPosition = (start + end) * 0.5f;
            line.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            line.transform.localScale = new Vector3(0.12f, 0.03f, delta.magnitude);
            AssignMaterial(line, material);

            Collider collider = line.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 10.5f, -7.2f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 6.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.42f, 0.40f, 0.36f);
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        private static void CreateMarkerLabel(Transform parent, string text, Vector3 position, bool finish)
        {
            GameObject labelObject = new GameObject("MarbleMazeLabel_" + text);
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = position;
            labelObject.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = finish ? 0.11f : 0.13f;
            label.fontSize = 64;
            label.color = finish ? new Color(0.22f, 0.92f, 0.58f) : new Color(0.10f, 0.16f, 0.22f);
        }

        private static MarbleMazeMaterials LoadOrCreateMaterials()
        {
            return new MarbleMazeMaterials(
                LoadOrCreateMaterial(BoardMaterialPath, new Color(0.64f, 0.72f, 0.68f), 0.28f),
                LoadOrCreateMaterial(WallMaterialPath, new Color(0.18f, 0.30f, 0.36f), 0.45f),
                LoadOrCreateMaterial(BallMaterialPath, new Color(0.92f, 0.95f, 1.0f), 0.75f),
                LoadOrCreateMaterial(CheckpointMaterialPath, new Color(0.94f, 0.70f, 0.18f), 0.35f),
                LoadOrCreateMaterial(FinishMaterialPath, new Color(0.18f, 0.78f, 0.46f), 0.35f));
        }

        private static Material LoadOrCreateMaterial(string path, Color color, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignMaterial(GameObject gameObject, Material material)
        {
            if (material == null)
                return;

            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "MarbleMazePanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
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

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void AddScenesToBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(GameplayScenePath, true)
            };
            EditorBuildSettings.scenes = scenes;
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, Vector3 value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.vector3Value = value;
            }
        }

        private readonly struct MarbleMazeMaterials
        {
            public MarbleMazeMaterials(
                Material board,
                Material wall,
                Material ball,
                Material checkpoint,
                Material finish)
            {
                Board = board;
                Wall = wall;
                Ball = ball;
                Checkpoint = checkpoint;
                Finish = finish;
            }

            public Material Board { get; }
            public Material Wall { get; }
            public Material Ball { get; }
            public Material Checkpoint { get; }
            public Material Finish { get; }
        }
    }
}
