using MxFramework.Game.CharacterTest;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class CharacterTestArcMineVfxPrefabBuilder
    {
        private const string MenuPath = "MxFramework/CharacterTest/VFX/Rebuild/Arc Mine Prefabs";
        private const string RootPath = "Assets/VFX/MxFramework/CharacterTest/ArcMine";
        private const string MaterialRootPath = RootPath + "/Materials";
        private const string PrefabRootPath = RootPath + "/Prefabs";
        private const string FlightPrefabPath = PrefabRootPath + "/ArcMine_Flight.prefab";
        private const string DeployPrefabPath = PrefabRootPath + "/ArcMine_Deploy.prefab";
        private const string FusePrefabPath = PrefabRootPath + "/ArcMine_Fuse.prefab";
        private const string TriggerPrefabPath = PrefabRootPath + "/ArcMine_Trigger.prefab";
        private const string SceneProfilePath =
            "Assets/Config/MxFramework/CharacterTest/CharacterTestSceneProfile.asset";

        [MenuItem(MenuPath)]
        public static void RebuildPrefabs()
        {
            EnsureFolder("Assets/VFX", "MxFramework");
            EnsureFolder("Assets/VFX/MxFramework", "CharacterTest");
            EnsureFolder("Assets/VFX/MxFramework/CharacterTest", "ArcMine");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Prefabs");

            Material core = LoadOrCreateMaterial(
                MaterialRootPath + "/ArcMine_Core.mat",
                new Color(0.64f, 0.96f, 1f, 0.9f),
                additive: true);
            Material arc = LoadOrCreateMaterial(
                MaterialRootPath + "/ArcMine_ElectricArc.mat",
                new Color(0.34f, 0.48f, 1f, 0.72f),
                additive: true);
            Material rune = LoadOrCreateMaterial(
                MaterialRootPath + "/ArcMine_Rune.mat",
                new Color(0.82f, 0.34f, 1f, 0.58f),
                additive: true);

            BuildFlightPrefab(core, arc, rune);
            BuildDeployPrefab(core, arc, rune);
            BuildFusePrefab(core, arc, rune);
            BuildTriggerPrefab(core, arc, rune);
            AssignSceneProfilePrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildFlightPrefab(Material core, Material arc, Material rune)
        {
            GameObject root = CreateRoot("ArcMine_Flight");
            CreateBurst(root.transform, "RuneCore", core, ParticleSystemShapeType.Sphere, 20, 0.3f, 0.11f, 0.11f, Vector3.zero, ParticleSystemRenderMode.Billboard, loop: true, rateOverTime: 20f, duration: 1.6f);
            CreateBurst(root.transform, "ArcTrail", arc, ParticleSystemShapeType.Cone, 42, 0.52f, 0.055f, 0.2f, new Vector3(0f, 0f, -0.14f), ParticleSystemRenderMode.Stretch, lengthScale: 1.9f, velocity: new Vector3(0f, 0.02f, -0.46f), loop: true, rateOverTime: 46f, duration: 1.6f);
            CreateBurst(root.transform, "RuneMotes", rune, ParticleSystemShapeType.Sphere, 20, 0.42f, 0.04f, 0.32f, Vector3.zero, ParticleSystemRenderMode.Billboard, loop: true, rateOverTime: 18f, duration: 1.6f);
            SavePrefab(root, FlightPrefabPath);
        }

        private static void BuildDeployPrefab(Material core, Material arc, Material rune)
        {
            GameObject root = CreateRoot("ArcMine_Deploy");
            CreateBurst(root.transform, "GroundRune", rune, ParticleSystemShapeType.Circle, 82, 0.72f, 0.11f, 1.25f, Vector3.zero, ParticleSystemRenderMode.Billboard, maxParticleSize: 0.4f);
            CreateBurst(root.transform, "DeployShockRing", arc, ParticleSystemShapeType.Circle, 58, 0.45f, 0.075f, 1.55f, Vector3.zero, ParticleSystemRenderMode.Stretch, lengthScale: 1.45f, maxParticleSize: 0.24f);
            CreateBurst(root.transform, "ArmingSparks", core, ParticleSystemShapeType.Sphere, 50, 0.58f, 0.045f, 0.55f, new Vector3(0f, 0.14f, 0f), ParticleSystemRenderMode.Stretch, lengthScale: 1.5f, velocity: new Vector3(0f, 0.22f, 0f), maxParticleSize: 0.12f);
            SavePrefab(root, DeployPrefabPath);
        }

        private static void BuildFusePrefab(Material core, Material arc, Material rune)
        {
            GameObject root = CreateRoot("ArcMine_Fuse");
            CreateBurst(root.transform, "FuseRunePulse", rune, ParticleSystemShapeType.Circle, 52, 0.85f, 0.09f, 1.18f, Vector3.zero, ParticleSystemRenderMode.Billboard, loop: true, rateOverTime: 8f, duration: 1.0f, maxParticleSize: 0.34f);
            CreateBurst(root.transform, "CountdownArcs", arc, ParticleSystemShapeType.Circle, 38, 0.62f, 0.055f, 1.0f, Vector3.zero, ParticleSystemRenderMode.Stretch, lengthScale: 1.5f, loop: true, rateOverTime: 15f, duration: 1.0f, maxParticleSize: 0.16f);
            CreateBurst(root.transform, "WarningMotes", core, ParticleSystemShapeType.Sphere, 34, 0.52f, 0.04f, 0.52f, new Vector3(0f, 0.18f, 0f), ParticleSystemRenderMode.Billboard, loop: true, rateOverTime: 20f, duration: 1.0f, maxParticleSize: 0.13f);
            SavePrefab(root, FusePrefabPath);
        }

        private static void BuildTriggerPrefab(Material core, Material arc, Material rune)
        {
            GameObject root = CreateRoot("ArcMine_Trigger");
            CreateBurst(root.transform, "RuneFlash", core, ParticleSystemShapeType.Sphere, 88, 0.3f, 0.18f, 0.42f, Vector3.zero, ParticleSystemRenderMode.Billboard, maxParticleSize: 0.7f);
            CreateBurst(root.transform, "ElectricBurstRing", arc, ParticleSystemShapeType.Circle, 110, 0.48f, 0.085f, 2.25f, Vector3.zero, ParticleSystemRenderMode.Stretch, lengthScale: 1.8f, maxParticleSize: 0.28f);
            CreateBurst(root.transform, "AfterglowMotes", rune, ParticleSystemShapeType.Sphere, 66, 0.78f, 0.05f, 1.35f, new Vector3(0f, 0.22f, 0f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.14f);
            SavePrefab(root, TriggerPrefabPath);
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
            main.startColor = Color.white;
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
                shape.angle = 13f;
                shape.length = 0.95f;
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
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateGradient(material.color));

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = renderMode;
            renderer.lengthScale = lengthScale;
            renderer.sortingFudge = 0.25f;
            renderer.minParticleSize = 0.01f;
            renderer.maxParticleSize = maxParticleSize;
            system.Play();
            return system;
        }

        private static Gradient CreateGradient(Color color)
        {
            return new Gradient
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
                    new GradientAlphaKey(color.a * 0.68f, 0.48f),
                    new GradientAlphaKey(0f, 1f)
                }
            };
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
            ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                SceneProfilePath);
            if (profile == null)
                return;

            var serialized = new SerializedObject(profile);
            AssignPrefab(serialized, "_arcMineFlightVfxPrefab", FlightPrefabPath);
            AssignPrefab(serialized, "_arcMineDeployVfxPrefab", DeployPrefabPath);
            AssignPrefab(serialized, "_arcMineFuseVfxPrefab", FusePrefabPath);
            AssignPrefab(serialized, "_arcMineTriggerVfxPrefab", TriggerPrefabPath);
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
