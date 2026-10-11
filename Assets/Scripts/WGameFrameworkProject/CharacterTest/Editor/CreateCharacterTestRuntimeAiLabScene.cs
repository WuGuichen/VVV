using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    public static class CreateCharacterTestRuntimeAiLabScene
    {
        public const string ScenePath = "Assets/Scenes/CharacterTestRuntimeAiLabScene.unity";
        public const string StandardArenaRootName = "Runtime AI Lab Standard Arena";
        public const string GroundName = "LabArena_Ground_64x44";
        public const string LosWallName = "LabWall_LOS_01";
        public const string ChokeNorthWallName = "LabWall_Choke_North";
        public const string ChokeSouthWallName = "LabWall_Choke_South";
        public const string HazardZoneName = "LabZone_E_Hazard";
        public const int ExpectedStaticColliderCount = 10;

        private const float ArenaWidth = 64f;
        private const float ArenaDepth = 44f;
        private const float WallHeight = 1.15f;
        private const float WallThickness = 0.25f;
        private const int StaticColliderBodyIdStart = 7200;

        [MenuItem("MxFramework/CharacterTest/Generate Runtime AI Lab Scene", priority = 129)]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");

            CharacterTestSceneProfile profile =
                AssetDatabase.LoadAssetAtPath<CharacterTestSceneProfile>(CharacterTestSceneProfile.DefaultAssetPath);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    "CharacterTest scene profile is missing: " + CharacterTestSceneProfile.DefaultAssetPath);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.35f, 0.39f, 0.42f);

            CreateMainCamera();
            CreateDirectionalLight();
            CreateRuntimeRoot(profile, out GameManager gameManager, out Transform worldRoot);
            CreateStandardArena(gameManager, worldRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[] { ScenePath }, ForceReserializeAssetsOptions.ReserializeAssets);
            Debug.Log("CharacterTest Runtime AI Lab scene created: " + ScenePath);
        }

        [MenuItem("MxFramework/CharacterTest/Validate Runtime AI Lab Scene", priority = 130)]
        public static void ValidateSmoke()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Runtime AI Lab scene did not load: " + ScenePath);

            UnityEngine.Camera camera = GameObject.FindWithTag("MainCamera")?.GetComponent<UnityEngine.Camera>();
            if (camera == null)
                throw new InvalidOperationException("Runtime AI Lab scene must contain a Main Camera.");

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0 && (camera.cullingMask & (1 << uiLayer)) != 0)
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab Main Camera must not render the UI layer; FairyGUI is rendered by StageCamera.");
            }

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
                throw new InvalidOperationException("Runtime AI Lab scene must contain a Directional Light.");

            GameManager gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            CharacterTestRuntimeAiPlannerDevSceneController controller =
                UnityEngine.Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            CharacterTestRuntimeAiPlannerDevPreviewOverlay overlay =
                UnityEngine.Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevPreviewOverlay>();
            CharacterTestRuntimeAiLabSetupOverlay setupOverlay =
                UnityEngine.Object.FindAnyObjectByType<CharacterTestRuntimeAiLabSetupOverlay>();
            CharacterTestRuntimeAiLabSetupCameraController setupCamera =
                UnityEngine.Object.FindAnyObjectByType<CharacterTestRuntimeAiLabSetupCameraController>();
            if (gameManager == null || controller == null)
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab scene must contain GameManager and Runtime AI Lab controller.");
            }

            var gameManagerState = new SerializedObject(gameManager);
            CharacterTestSceneProfile profile =
                gameManagerState.FindProperty("_profile").objectReferenceValue as CharacterTestSceneProfile;
            if (profile == null)
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab GameManager must bind the shared CharacterTest scene profile.");
            }

            if (profile.FireballVfxPrefab == null)
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab CharacterTest scene profile must provide the base VFX resources.");
            }

            if (gameManager.GetComponent<CharacterTestRuntimeAiPlannerDevSceneController>() == null)
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab controller must be on the same GameObject as GameManager.");
            }

            if (overlay == null || gameManager.GetComponent<CharacterTestRuntimeAiPlannerDevPreviewOverlay>() == null)
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab preview overlay must be on the same GameObject as GameManager.");
            }

            if (setupOverlay == null || gameManager.GetComponent<CharacterTestRuntimeAiLabSetupOverlay>() == null)
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab setup overlay must be on the same GameObject as GameManager.");
            }

            if (setupCamera == null || gameManager.GetComponent<CharacterTestRuntimeAiLabSetupCameraController>() == null)
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab setup camera controller must be on the same GameObject as GameManager.");
            }

            ValidateStandardArena(gameManager);

            Debug.Log("CharacterTest Runtime AI Lab scene smoke validation passed.");
        }

        private static void CreateMainCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 38f, -32f),
                Quaternion.Euler(60f, 0f, 0f));

            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.orthographic = true;
            camera.orthographicSize = 30f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                camera.cullingMask &= ~(1 << uiLayer);
        }

        private static void CreateDirectionalLight()
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
        }

        private static void CreateRuntimeRoot(
            CharacterTestSceneProfile profile,
            out GameManager gameManager,
            out Transform worldRootTransform)
        {
            var root = new GameObject("CharacterTest Runtime AI Lab");
            root.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
            root.AddComponent<CharacterTestRuntimeAiPlannerDevPreviewOverlay>();
            root.AddComponent<CharacterTestRuntimeAiLabSetupOverlay>();
            root.AddComponent<CharacterTestRuntimeAiLabSetupCameraController>();
            gameManager = root.AddComponent<GameManager>();

            var worldRoot = new GameObject("Runtime AI Lab Scene Root");
            worldRoot.transform.SetParent(root.transform, worldPositionStays: false);
            worldRootTransform = worldRoot.transform;

            var serializedObject = new SerializedObject(gameManager);
            serializedObject.FindProperty("_profile").objectReferenceValue = profile;
            serializedObject.FindProperty("_worldObjectSceneRoot").objectReferenceValue = worldRoot.transform;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateStandardArena(GameManager gameManager, Transform worldRoot)
        {
            if (gameManager == null)
                throw new ArgumentNullException(nameof(gameManager));
            if (worldRoot == null)
                throw new ArgumentNullException(nameof(worldRoot));

            ArenaMaterials materials = CreateArenaMaterials();
            var arenaRoot = new GameObject(StandardArenaRootName);
            arenaRoot.transform.SetParent(worldRoot, worldPositionStays: false);

            GameObject ground = CreateBox(
                GroundName,
                arenaRoot.transform,
                new Vector3(0f, -0.025f, 0f),
                new Vector3(ArenaWidth, 0.05f, ArenaDepth),
                materials.Ground);
            CreateGrid(arenaRoot.transform, materials);
            CreateZones(arenaRoot.transform, materials);
            CreateSpawnPads(arenaRoot.transform, materials);
            CreateLabels(arenaRoot.transform);

            var staticColliders = new List<CharacterTestWorldObjectSceneSource.StaticColliderInstance>(
                ExpectedStaticColliderCount);
            int bodyId = StaticColliderBodyIdStart;
            AddStaticSurface(staticColliders, ground, ref bodyId);
            AddStaticObstacle(
                staticColliders,
                CreateBox(
                    "LabWall_Bounds_North",
                    arenaRoot.transform,
                    new Vector3(0f, WallHeight * 0.5f, ArenaDepth * 0.5f + WallThickness * 0.5f),
                    new Vector3(ArenaWidth + WallThickness * 2f, WallHeight, WallThickness),
                    materials.Wall),
                ref bodyId);
            AddStaticObstacle(
                staticColliders,
                CreateBox(
                    "LabWall_Bounds_South",
                    arenaRoot.transform,
                    new Vector3(0f, WallHeight * 0.5f, -ArenaDepth * 0.5f - WallThickness * 0.5f),
                    new Vector3(ArenaWidth + WallThickness * 2f, WallHeight, WallThickness),
                    materials.Wall),
                ref bodyId);
            AddStaticObstacle(
                staticColliders,
                CreateBox(
                    "LabWall_Bounds_West",
                    arenaRoot.transform,
                    new Vector3(-ArenaWidth * 0.5f - WallThickness * 0.5f, WallHeight * 0.5f, 0f),
                    new Vector3(WallThickness, WallHeight, ArenaDepth),
                    materials.Wall),
                ref bodyId);
            AddStaticObstacle(
                staticColliders,
                CreateBox(
                    "LabWall_Bounds_East",
                    arenaRoot.transform,
                    new Vector3(ArenaWidth * 0.5f + WallThickness * 0.5f, WallHeight * 0.5f, 0f),
                    new Vector3(WallThickness, WallHeight, ArenaDepth),
                    materials.Wall),
                ref bodyId);
            AddStaticObstacle(
                staticColliders,
                CreateBox(
                    LosWallName,
                    arenaRoot.transform,
                    new Vector3(17f, WallHeight * 0.5f, 14f),
                    new Vector3(13f, WallHeight, 0.45f),
                    materials.Obstacle),
                ref bodyId);
            AddStaticObstacle(
                staticColliders,
                CreateBox(
                    "LabPillar_Path_01",
                    arenaRoot.transform,
                    new Vector3(-22f, WallHeight * 0.5f, 1.8f),
                    new Vector3(1.4f, WallHeight, 1.4f),
                    materials.Obstacle),
                ref bodyId);
            AddStaticObstacle(
                staticColliders,
                CreateBox(
                    "LabPillar_Path_02",
                    arenaRoot.transform,
                    new Vector3(-16f, WallHeight * 0.5f, -2.2f),
                    new Vector3(1.4f, WallHeight, 1.4f),
                    materials.Obstacle),
                ref bodyId);
            AddStaticObstacle(
                staticColliders,
                CreateBox(
                    ChokeNorthWallName,
                    arenaRoot.transform,
                    new Vector3(17f, WallHeight * 0.5f, 2.25f),
                    new Vector3(13.5f, WallHeight, 0.32f),
                    materials.Obstacle),
                ref bodyId);
            AddStaticObstacle(
                staticColliders,
                CreateBox(
                    ChokeSouthWallName,
                    arenaRoot.transform,
                    new Vector3(17f, WallHeight * 0.5f, -2.25f),
                    new Vector3(13.5f, WallHeight, 0.32f),
                    materials.Obstacle),
                ref bodyId);

            gameManager.WorldObjectSceneCache.Configure(
                Array.Empty<CharacterTestWorldObjectSceneSource.MovableInstance>(),
                staticColliders,
                Array.Empty<CharacterTestWorldObjectSceneSource.LevelPointInstance>(),
                CreateRegionInstances(arenaRoot.transform));
        }

        private static void ValidateStandardArena(GameManager gameManager)
        {
            if (GameObject.Find(StandardArenaRootName) == null)
                throw new InvalidOperationException("Runtime AI Lab scene must contain the standard arena root.");
            if (GameObject.Find(GroundName) == null)
                throw new InvalidOperationException("Runtime AI Lab scene must contain the 64m x 44m ground.");
            if (GameObject.Find(LosWallName) == null)
                throw new InvalidOperationException("Runtime AI Lab scene must contain the LOS wall.");
            if (GameObject.Find(ChokeNorthWallName) == null || GameObject.Find(ChokeSouthWallName) == null)
                throw new InvalidOperationException("Runtime AI Lab scene must contain the choke walls.");
            if (GameObject.Find(HazardZoneName) == null)
                throw new InvalidOperationException("Runtime AI Lab scene must contain the hazard zone marker.");

            CharacterTestWorldObjectSceneCache cache = gameManager == null
                ? null
                : gameManager.WorldObjectSceneCache;
            if (cache == null || cache.StaticColliderInstanceCount != ExpectedStaticColliderCount)
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab scene cache must contain "
                    + ExpectedStaticColliderCount.ToString()
                    + " static collider instances.");
            }

            CharacterTestWorldObjectStaticColliderSpec[] specs = cache.CollectStaticColliderSpecs(out string[] diagnostics);
            if (diagnostics.Length > 0)
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab scene cache produced static collider diagnostics: "
                    + string.Join(", ", diagnostics));
            }

            if (specs.Length != ExpectedStaticColliderCount)
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab scene cache produced "
                    + specs.Length.ToString()
                    + " static collider specs.");
            }
        }

        private static void CreateGrid(Transform parent, ArenaMaterials materials)
        {
            var gridRoot = new GameObject("LabGrid_1m");
            gridRoot.transform.SetParent(parent, worldPositionStays: false);

            int halfWidth = Mathf.RoundToInt(ArenaWidth * 0.5f);
            int halfDepth = Mathf.RoundToInt(ArenaDepth * 0.5f);

            for (int x = -halfWidth; x <= halfWidth; x++)
            {
                Material lineMaterial = SelectGridMaterial(x, materials);
                float lineWidth = SelectGridLineWidth(x);
                CreateBox(
                    "LabGrid_X_" + x.ToString(),
                    gridRoot.transform,
                    new Vector3(x, 0.006f, 0f),
                    new Vector3(lineWidth, 0.012f, ArenaDepth),
                    lineMaterial);
            }

            for (int z = -halfDepth; z <= halfDepth; z++)
            {
                Material lineMaterial = SelectGridMaterial(z, materials);
                float lineWidth = SelectGridLineWidth(z);
                CreateBox(
                    "LabGrid_Z_" + z.ToString(),
                    gridRoot.transform,
                    new Vector3(0f, 0.008f, z),
                    new Vector3(ArenaWidth, 0.012f, lineWidth),
                    lineMaterial);
            }
        }

        private static void CreateZones(Transform parent, ArenaMaterials materials)
        {
            CreateZone("LabZone_A_OpenEngage", parent, new Vector3(-17f, 0.012f, 14f), new Vector3(20f, 0.02f, 10f), materials.ZoneA);
            CreateZone("LabZone_B_LosBlock", parent, new Vector3(17f, 0.014f, 14f), new Vector3(20f, 0.02f, 10f), materials.ZoneB);
            CreateZone("LabZone_C_PathObstacle", parent, new Vector3(-17f, 0.016f, 0f), new Vector3(20f, 0.02f, 10f), materials.ZoneC);
            CreateZone("LabZone_D_Choke", parent, new Vector3(17f, 0.018f, 0f), new Vector3(20f, 0.02f, 10f), materials.ZoneD);
            CreateZone(HazardZoneName, parent, new Vector3(-17f, 0.02f, -14f), new Vector3(16f, 0.025f, 9f), materials.Hazard);
            CreateZone("LabZone_F_KitePursuit", parent, new Vector3(17f, 0.022f, -14f), new Vector3(20f, 0.02f, 10f), materials.ZoneF);
        }

        private static void CreateSpawnPads(Transform parent, ArenaMaterials materials)
        {
            CreateBox("LabSpawn_Ally_01", parent, new Vector3(-27f, 0.04f, 14f), new Vector3(1.6f, 0.05f, 1.6f), materials.AllySpawn);
            CreateBox("LabSpawn_Ally_02", parent, new Vector3(-27f, 0.04f, 10.5f), new Vector3(1.1f, 0.04f, 1.1f), materials.AllySpawn);
            CreateBox("LabSpawn_Ally_03", parent, new Vector3(-27f, 0.04f, 17.5f), new Vector3(1.1f, 0.04f, 1.1f), materials.AllySpawn);
            CreateBox("LabSpawn_Enemy_01", parent, new Vector3(27f, 0.04f, 14f), new Vector3(1.6f, 0.05f, 1.6f), materials.EnemySpawn);
            CreateBox("LabSpawn_Enemy_02", parent, new Vector3(27f, 0.04f, 10.5f), new Vector3(1.1f, 0.04f, 1.1f), materials.EnemySpawn);
            CreateBox("LabSpawn_Enemy_03", parent, new Vector3(27f, 0.04f, 17.5f), new Vector3(1.1f, 0.04f, 1.1f), materials.EnemySpawn);
        }

        private static void CreateLabels(Transform parent)
        {
            CreateLabel("LabLabel_A", parent, "A Engage", new Vector3(-25.5f, 0.08f, 18.2f));
            CreateLabel("LabLabel_B", parent, "B LOS", new Vector3(25.5f, 0.08f, 18.2f));
            CreateLabel("LabLabel_C", parent, "C Path", new Vector3(-25.5f, 0.08f, 4.2f));
            CreateLabel("LabLabel_D", parent, "D Choke", new Vector3(25.5f, 0.08f, 4.2f));
            CreateLabel("LabLabel_E", parent, "E Hazard", new Vector3(-25.5f, 0.08f, -10.5f));
            CreateLabel("LabLabel_F", parent, "F Pursuit", new Vector3(25.5f, 0.08f, -10.5f));
            CreateLabel("LabLabel_Size", parent, "64m x 44m | 1m grid", new Vector3(0f, 0.08f, -21.2f));
        }

        private static Material SelectGridMaterial(int coordinate, ArenaMaterials materials)
        {
            if (coordinate % 8 == 0)
                return materials.GridAccent;
            return coordinate % 4 == 0 ? materials.GridMajor : materials.Grid;
        }

        private static float SelectGridLineWidth(int coordinate)
        {
            if (coordinate % 8 == 0)
                return 0.06f;
            return coordinate % 4 == 0 ? 0.035f : 0.014f;
        }

        private static CharacterTestWorldObjectSceneSource.LevelRegionInstance[] CreateRegionInstances(
            Transform arenaRoot)
        {
            return new[]
            {
                CreateRegion("lab.region.open_engage", "LabZone_A_OpenEngage", arenaRoot),
                CreateRegion("lab.region.los_block", "LabZone_B_LosBlock", arenaRoot),
                CreateRegion("lab.region.path_obstacle", "LabZone_C_PathObstacle", arenaRoot),
                CreateRegion("lab.region.choke", "LabZone_D_Choke", arenaRoot),
                CreateRegion("lab.region.hazard_marker", HazardZoneName, arenaRoot),
                CreateRegion("lab.region.kite_pursuit", "LabZone_F_KitePursuit", arenaRoot)
            };
        }

        private static CharacterTestWorldObjectSceneSource.LevelRegionInstance CreateRegion(
            string stableId,
            string childName,
            Transform arenaRoot)
        {
            Transform child = arenaRoot == null ? null : arenaRoot.Find(childName);
            return new CharacterTestWorldObjectSceneSource.LevelRegionInstance(
                enabled: true,
                definitionId: CharacterTestWorldObjectDefinitionIds.RegionTestCourse,
                stableId: stableId,
                transform: child,
                scaleSource: CharacterTestWorldObjectInstanceScaleSource.SceneTransform);
        }

        private static void CreateZone(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            CreateBox(name, parent, position, scale, material);
        }

        private static GameObject CreateBox(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.name = name;
            instance.transform.SetParent(parent, worldPositionStays: false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = scale;
            Collider collider = instance.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            return instance;
        }

        private static void CreateLabel(
            string name,
            Transform parent,
            string text,
            Vector3 position)
        {
            var labelObject = new GameObject(name);
            labelObject.transform.SetParent(parent, worldPositionStays: false);
            labelObject.transform.localPosition = position;
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.14f;
            label.fontSize = 32;
            label.color = new Color(0.82f, 0.9f, 0.95f, 0.42f);
        }

        private static void AddStaticObstacle(
            List<CharacterTestWorldObjectSceneSource.StaticColliderInstance> output,
            GameObject obstacle,
            ref int bodyId)
        {
            AddStaticCollider(
                output,
                obstacle,
                CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                ref bodyId);
        }

        private static void AddStaticSurface(
            List<CharacterTestWorldObjectSceneSource.StaticColliderInstance> output,
            GameObject surface,
            ref int bodyId)
        {
            AddStaticCollider(
                output,
                surface,
                CharacterTestWorldObjectDefinitionIds.StaticRoughSurface,
                ref bodyId);
        }

        private static void AddStaticCollider(
            List<CharacterTestWorldObjectSceneSource.StaticColliderInstance> output,
            GameObject target,
            int definitionId,
            ref int bodyId)
        {
            if (output == null || target == null)
                return;

            output.Add(new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                enabled: true,
                definitionId: definitionId,
                transform: target.transform,
                bodyId: bodyId++,
                colliderId: 1,
                scaleSource: CharacterTestWorldObjectInstanceScaleSource.SceneTransform));
        }

        private static ArenaMaterials CreateArenaMaterials()
        {
            return new ArenaMaterials
            {
                Ground = CharacterTestWGameMaterialFactory.CreateEnvironment("Runtime AI Lab Ground", new Color(0.11f, 0.13f, 0.14f, 1f)),
                Grid = CharacterTestWGameMaterialFactory.CreateProp("Runtime AI Lab Grid", new Color(0.24f, 0.31f, 0.34f, 1f)),
                GridMajor = CharacterTestWGameMaterialFactory.CreateProp("Runtime AI Lab Grid Major", new Color(0.32f, 0.39f, 0.42f, 1f)),
                GridAccent = CharacterTestWGameMaterialFactory.CreateProp("Runtime AI Lab Grid Accent", new Color(0.42f, 0.50f, 0.53f, 1f)),
                Wall = CharacterTestWGameMaterialFactory.CreateEnvironment("Runtime AI Lab Wall", new Color(0.30f, 0.34f, 0.36f, 1f)),
                Obstacle = CharacterTestWGameMaterialFactory.CreateEnvironment("Runtime AI Lab Obstacle", new Color(0.28f, 0.31f, 0.34f, 1f)),
                ZoneA = CharacterTestWGameMaterialFactory.CreateProp("Runtime AI Lab Zone A", new Color(0.07f, 0.17f, 0.24f, 1f)),
                ZoneB = CharacterTestWGameMaterialFactory.CreateProp("Runtime AI Lab Zone B", new Color(0.14f, 0.13f, 0.22f, 1f)),
                ZoneC = CharacterTestWGameMaterialFactory.CreateProp("Runtime AI Lab Zone C", new Color(0.11f, 0.18f, 0.14f, 1f)),
                ZoneD = CharacterTestWGameMaterialFactory.CreateProp("Runtime AI Lab Zone D", new Color(0.17f, 0.16f, 0.09f, 1f)),
                ZoneF = CharacterTestWGameMaterialFactory.CreateProp("Runtime AI Lab Zone F", new Color(0.10f, 0.12f, 0.23f, 1f)),
                Hazard = CharacterTestWGameMaterialFactory.CreateProp("Runtime AI Lab Hazard", new Color(0.47f, 0.19f, 0.06f, 1f), highlighted: true),
                AllySpawn = CharacterTestWGameMaterialFactory.CreateProp("Runtime AI Lab Ally Spawn", new Color(0.10f, 0.28f, 0.58f, 1f), highlighted: true),
                EnemySpawn = CharacterTestWGameMaterialFactory.CreateProp("Runtime AI Lab Enemy Spawn", new Color(0.58f, 0.11f, 0.10f, 1f), highlighted: true),
            };
        }

        private sealed class ArenaMaterials
        {
            public Material Ground;
            public Material Grid;
            public Material GridMajor;
            public Material GridAccent;
            public Material Wall;
            public Material Obstacle;
            public Material ZoneA;
            public Material ZoneB;
            public Material ZoneC;
            public Material ZoneD;
            public Material ZoneF;
            public Material Hazard;
            public Material AllySpawn;
            public Material EnemySpawn;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
