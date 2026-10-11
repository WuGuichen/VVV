using MxFramework.Game.CharacterTest;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class CharacterTestPoisonBottleVfxPrefabBuilder
    {
        private const string MenuPath = "MxFramework/CharacterTest/VFX/Rebuild/Poison Bottle Prefabs";
        private const string RootPath = "Assets/VFX/MxFramework/Samples/PoisonBottle";
        private const string MaterialRootPath = RootPath + "/Materials";
        private const string PrefabRootPath = RootPath + "/Prefabs";
        private const string FlightPrefabPath = PrefabRootPath + "/PoisonBottle_Flight.prefab";
        private const string ShatterPrefabPath = PrefabRootPath + "/PoisonBottle_Shatter.prefab";
        private const string PoolPrefabPath = PrefabRootPath + "/PoisonBottle_Pool.prefab";
        private const string TickPrefabPath = PrefabRootPath + "/Poisoned_Tick.prefab";
        private const float PoisonBottleMaxFlightSeconds = 1.5f;
        private const string SceneProfilePath =
            "Assets/Config/MxFramework/CharacterTest/CharacterTestSceneProfile.asset";

        [MenuItem(MenuPath)]
        public static void RebuildPrefabs()
        {
            EnsureFolder("Assets/VFX", "MxFramework");
            EnsureFolder("Assets/VFX/MxFramework", "Samples");
            EnsureFolder("Assets/VFX/MxFramework/Samples", "PoisonBottle");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Prefabs");

            Material core = LoadOrCreateMaterial(
                MaterialRootPath + "/PoisonBottle_Core.mat",
                new Color(0.48f, 1f, 0.26f, 0.86f),
                additive: true);
            Material mist = LoadOrCreateMaterial(
                MaterialRootPath + "/PoisonBottle_Mist.mat",
                new Color(0.12f, 0.92f, 0.36f, 0.38f),
                additive: true);
            Material spark = LoadOrCreateMaterial(
                MaterialRootPath + "/PoisonBottle_Spark.mat",
                new Color(0.78f, 1f, 0.46f, 0.82f),
                additive: true);

            BuildFlightPrefab(core, mist, spark);
            BuildShatterPrefab(core, mist, spark);
            BuildPoolPrefab(core, mist, spark);
            BuildTickPrefab(core, mist, spark);
            AssignSceneProfilePrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildFlightPrefab(Material core, Material mist, Material spark)
        {
            GameObject root = CreateRoot("PoisonBottle_Flight");
            CreateBottleBody(root.transform, "BottleBody", core);
            CreateBurst(
                root.transform,
                "BottleCore",
                core,
                ParticleSystemShapeType.Sphere,
                18,
                0.36f,
                0.13f,
                0.1f,
                Vector3.zero,
                new Color(0.62f, 1f, 0.34f, 0.9f),
                ParticleSystemRenderMode.Billboard,
                maxParticleSize: 0.24f,
                loop: true,
                rateOverTime: 16f,
                duration: PoisonBottleMaxFlightSeconds);
            CreateBurst(
                root.transform,
                "BottleTrail",
                mist,
                ParticleSystemShapeType.Cone,
                36,
                0.54f,
                0.055f,
                0.18f,
                new Vector3(0f, 0f, -0.12f),
                new Color(0.12f, 0.86f, 0.28f, 0.48f),
                ParticleSystemRenderMode.Stretch,
                lengthScale: 1.8f,
                velocity: new Vector3(0f, 0.02f, -0.38f),
                maxParticleSize: 0.18f,
                loop: true,
                rateOverTime: 42f,
                duration: PoisonBottleMaxFlightSeconds);
            CreateBurst(
                root.transform,
                "BottleMotes",
                spark,
                ParticleSystemShapeType.Sphere,
                18,
                0.34f,
                0.032f,
                0.16f,
                Vector3.zero,
                new Color(0.82f, 1f, 0.42f, 0.72f),
                ParticleSystemRenderMode.Billboard,
                maxParticleSize: 0.11f,
                loop: true,
                rateOverTime: 14f,
                duration: PoisonBottleMaxFlightSeconds);
            SavePrefab(root, FlightPrefabPath);
        }

        private static void CreateBottleBody(Transform parent, string name, Material material)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = name;
            body.transform.SetParent(parent, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = new Vector3(0.22f, 0.22f, 0.28f);

            Collider collider = body.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            MeshRenderer renderer = body.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void BuildShatterPrefab(Material core, Material mist, Material spark)
        {
            GameObject root = CreateRoot("PoisonBottle_Shatter");
            CreateBurst(root.transform, "ShatterSplash", core, ParticleSystemShapeType.Sphere, 74, 0.32f, 0.13f, 0.5f, Vector3.zero, new Color(0.52f, 1f, 0.24f, 0.84f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.58f);
            CreateBurst(root.transform, "ShatterMistRing", mist, ParticleSystemShapeType.Circle, 62, 0.48f, 0.07f, 0.76f, Vector3.zero, new Color(0.08f, 0.82f, 0.26f, 0.5f), ParticleSystemRenderMode.Stretch, lengthScale: 1.45f, maxParticleSize: 0.2f);
            CreateBurst(root.transform, "BottleSpatter", spark, ParticleSystemShapeType.Sphere, 52, 0.38f, 0.035f, 0.36f, Vector3.zero, new Color(0.78f, 1f, 0.38f, 0.82f), ParticleSystemRenderMode.Stretch, lengthScale: 1.8f, velocity: new Vector3(0f, 0.18f, 0.18f), maxParticleSize: 0.1f);
            SavePrefab(root, ShatterPrefabPath);
        }

        private static void BuildPoolPrefab(Material core, Material mist, Material spark)
        {
            GameObject root = CreateRoot("PoisonBottle_Pool");
            CreateBurst(root.transform, "PoolSurface", mist, ParticleSystemShapeType.Circle, 96, 2.9f, 0.16f, 2.45f, Vector3.zero, new Color(0.05f, 0.72f, 0.18f, 0.46f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.42f);
            CreateBurst(root.transform, "PoolBubbles", core, ParticleSystemShapeType.Circle, 44, 1.6f, 0.08f, 2.0f, Vector3.zero, new Color(0.46f, 1f, 0.22f, 0.66f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.22f);
            CreateBurst(root.transform, "PoolVapor", spark, ParticleSystemShapeType.Circle, 60, 2.6f, 0.05f, 2.35f, Vector3.zero, new Color(0.74f, 1f, 0.36f, 0.38f), ParticleSystemRenderMode.Stretch, lengthScale: 1.1f, velocity: new Vector3(0f, 0.12f, 0f), maxParticleSize: 0.16f);
            SavePrefab(root, PoolPrefabPath);
        }

        private static void BuildTickPrefab(Material core, Material mist, Material spark)
        {
            GameObject root = CreateRoot("Poisoned_Tick");
            CreateBurst(root.transform, "TickPulse", core, ParticleSystemShapeType.Sphere, 48, 0.24f, 0.11f, 0.38f, Vector3.zero, new Color(0.56f, 1f, 0.28f, 0.82f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.48f);
            CreateBurst(root.transform, "TickVapor", mist, ParticleSystemShapeType.Cone, 56, 0.5f, 0.06f, 0.48f, Vector3.zero, new Color(0.08f, 0.76f, 0.2f, 0.5f), ParticleSystemRenderMode.Stretch, lengthScale: 1.15f, velocity: new Vector3(0f, 0.18f, 0f), maxParticleSize: 0.17f);
            CreateBurst(root.transform, "TickMotes", spark, ParticleSystemShapeType.Sphere, 40, 0.34f, 0.032f, 0.3f, Vector3.zero, new Color(0.82f, 1f, 0.46f, 0.78f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.1f);
            SavePrefab(root, TickPrefabPath);
        }

        private static ParticleSystem CreateBurst(
            Transform parent,
            string name,
            Material material,
            ParticleSystemShapeType shapeType,
            int count,
            float lifetime,
            float size,
            float radius,
            Vector3 localPosition,
            Color color,
            ParticleSystemRenderMode renderMode,
            float lengthScale = 1f,
            Vector3 velocity = default,
            float maxParticleSize = 1f,
            bool loop = false,
            float rateOverTime = 0f,
            float duration = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            ParticleSystem system = go.AddComponent<ParticleSystem>();

            var main = system.main;
            main.duration = Mathf.Max(0.1f, duration > 0f ? duration : lifetime);
            main.loop = loop;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = system.emission;
            emission.rateOverTime = Mathf.Max(0f, rateOverTime);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count)) });

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = shapeType;
            shape.radius = radius;
            if (shapeType == ParticleSystemShapeType.Cone)
            {
                shape.angle = 12f;
                shape.length = 0.9f;
            }
            else if (shapeType == ParticleSystemShapeType.Circle)
            {
                shape.arc = 360f;
            }

            var velocityOverLifetime = system.velocityOverLifetime;
            velocityOverLifetime.enabled = velocity.sqrMagnitude > 0.0001f;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.x = velocity.x;
            velocityOverLifetime.y = velocity.y;
            velocityOverLifetime.z = velocity.z;

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(Color.white, 0.35f),
                    new GradientColorKey(color, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, 0.08f),
                    new GradientAlphaKey(color.a * 0.62f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                }
            });

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = renderMode;
            renderer.lengthScale = lengthScale;
            renderer.sortingFudge = 0.2f;
            renderer.minParticleSize = 0.01f;
            renderer.maxParticleSize = maxParticleSize;
            system.Play();
            return system;
        }

        private static GameObject CreateRoot(string name)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static Material LoadOrCreateMaterial(string path, Color color, bool additive)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = CharacterTestUnlitMaterials.CreateVfx(Path.GetFileNameWithoutExtension(path), color, additive);
                AssetDatabase.CreateAsset(material, path);
            }
            else CharacterTestUnlitMaterials.ConfigureVfx(material, color, additive);
            EditorUtility.SetDirty(material);
            return material;
        }



        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void AssignSceneProfilePrefabs()
        {
            ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SceneProfilePath);
            if (profile == null)
                return;

            var serialized = new SerializedObject(profile);
            AssignPrefab(serialized, "_poisonDartFlightVfxPrefab", FlightPrefabPath);
            AssignPrefab(serialized, "_poisonDartImpactVfxPrefab", ShatterPrefabPath);
            AssignPrefab(serialized, "_poisonBottlePoolVfxPrefab", PoolPrefabPath);
            AssignPrefab(serialized, "_poisonedTickVfxPrefab", TickPrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void AssignPrefab(SerializedObject serialized, string propertyName, string prefabPath)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
