using MxFramework.Game.CharacterTest;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class CharacterTestRuneTurretVfxPrefabBuilder
    {
        private const string MenuPath = "MxFramework/CharacterTest/VFX/Rebuild/Rune Turret Prefabs";
        private const string RootPath = "Assets/VFX/MxFramework/CharacterTest/RuneTurret";
        private const string MaterialRootPath = RootPath + "/Materials";
        private const string PrefabRootPath = RootPath + "/Prefabs";
        private const string DeployPrefabPath = PrefabRootPath + "/RuneTurret_Deploy.prefab";
        private const string IdlePrefabPath = PrefabRootPath + "/RuneTurret_Idle.prefab";
        private const string FirePrefabPath = PrefabRootPath + "/RuneTurret_Fire.prefab";
        private const string FlightPrefabPath = PrefabRootPath + "/RuneTurret_Flight.prefab";
        private const string ImpactPrefabPath = PrefabRootPath + "/RuneTurret_Impact.prefab";
        private const string ExpirePrefabPath = PrefabRootPath + "/RuneTurret_Expire.prefab";
        private const string SceneProfilePath =
            "Assets/Config/MxFramework/CharacterTest/CharacterTestSceneProfile.asset";

        [MenuItem(MenuPath)]
        public static void RebuildPrefabs()
        {
            EnsureFolder("Assets/VFX", "MxFramework");
            EnsureFolder("Assets/VFX/MxFramework", "CharacterTest");
            EnsureFolder("Assets/VFX/MxFramework/CharacterTest", "RuneTurret");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Prefabs");

            Material core = LoadOrCreateMaterial(
                MaterialRootPath + "/RuneTurret_Core.mat",
                new Color(0.42f, 1f, 0.78f, 0.9f),
                additive: true);
            Material beam = LoadOrCreateMaterial(
                MaterialRootPath + "/RuneTurret_Beam.mat",
                new Color(0.25f, 0.72f, 1f, 0.74f),
                additive: true);
            Material rune = LoadOrCreateMaterial(
                MaterialRootPath + "/RuneTurret_Rune.mat",
                new Color(0.92f, 0.78f, 0.34f, 0.62f),
                additive: true);

            BuildDeployPrefab(core, beam, rune);
            BuildIdlePrefab(core, beam, rune);
            BuildFirePrefab(core, beam, rune);
            BuildFlightPrefab(core, beam);
            BuildImpactPrefab(core, beam);
            BuildExpirePrefab(beam, rune);
            AssignSceneProfilePrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildDeployPrefab(Material core, Material beam, Material rune)
        {
            GameObject root = CreateRoot("RuneTurret_Deploy");
            CreateBurst(root.transform, "DeployRune", rune, ParticleSystemShapeType.Circle, 82, 0.62f, 0.1f, 1.05f, Vector3.zero, ParticleSystemRenderMode.Billboard, maxParticleSize: 0.34f);
            CreateBurst(root.transform, "ColumnWake", beam, ParticleSystemShapeType.Cone, 54, 0.7f, 0.06f, 0.34f, new Vector3(0f, 0.08f, 0f), ParticleSystemRenderMode.Stretch, lengthScale: 1.45f, velocity: new Vector3(0f, 0.5f, 0f), maxParticleSize: 0.16f);
            CreateBurst(root.transform, "CoreIgnition", core, ParticleSystemShapeType.Sphere, 34, 0.42f, 0.07f, 0.2f, new Vector3(0f, 0.42f, 0f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.18f);
            SavePrefab(root, DeployPrefabPath);
        }

        private static void BuildIdlePrefab(Material core, Material beam, Material rune)
        {
            GameObject root = CreateRoot("RuneTurret_Idle");
            CreateBurst(root.transform, "IdleHalo", rune, ParticleSystemShapeType.Circle, 40, 1.2f, 0.06f, 0.72f, Vector3.zero, ParticleSystemRenderMode.Billboard, loop: true, rateOverTime: 8f, duration: 1.2f, maxParticleSize: 0.22f);
            CreateBurst(root.transform, "AimingCore", core, ParticleSystemShapeType.Sphere, 18, 0.72f, 0.055f, 0.16f, new Vector3(0f, 0.8f, 0f), ParticleSystemRenderMode.Billboard, loop: true, rateOverTime: 12f, duration: 1.2f, maxParticleSize: 0.14f);
            CreateBurst(root.transform, "RuneSparks", beam, ParticleSystemShapeType.Sphere, 24, 0.62f, 0.035f, 0.5f, new Vector3(0f, 0.18f, 0f), ParticleSystemRenderMode.Billboard, loop: true, rateOverTime: 14f, duration: 1.2f, maxParticleSize: 0.1f);
            SavePrefab(root, IdlePrefabPath);
        }

        private static void BuildFirePrefab(Material core, Material beam, Material rune)
        {
            GameObject root = CreateRoot("RuneTurret_Fire");
            CreateBurst(root.transform, "MuzzleFlash", core, ParticleSystemShapeType.Cone, 46, 0.26f, 0.08f, 0.18f, new Vector3(0f, 0.8f, 0.28f), ParticleSystemRenderMode.Stretch, lengthScale: 1.6f, velocity: new Vector3(0f, 0f, 0.42f), maxParticleSize: 0.18f);
            CreateBurst(root.transform, "FireRune", rune, ParticleSystemShapeType.Circle, 32, 0.34f, 0.05f, 0.52f, new Vector3(0f, 0.8f, 0f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.18f);
            CreateBurst(root.transform, "ArcKick", beam, ParticleSystemShapeType.Sphere, 18, 0.28f, 0.045f, 0.2f, new Vector3(0f, 0.78f, 0.16f), ParticleSystemRenderMode.Stretch, lengthScale: 1.25f, velocity: new Vector3(0f, 0f, 0.25f), maxParticleSize: 0.12f);
            SavePrefab(root, FirePrefabPath);
        }

        private static void BuildFlightPrefab(Material core, Material beam)
        {
            GameObject root = CreateRoot("RuneTurret_Flight");
            CreateBurst(root.transform, "ShotCore", core, ParticleSystemShapeType.Sphere, 18, 0.24f, 0.08f, 0.08f, Vector3.zero, ParticleSystemRenderMode.Billboard, loop: true, rateOverTime: 18f, duration: 0.8f, maxParticleSize: 0.14f);
            CreateBurst(root.transform, "ShotTrail", beam, ParticleSystemShapeType.Cone, 38, 0.38f, 0.045f, 0.14f, new Vector3(0f, 0f, -0.16f), ParticleSystemRenderMode.Stretch, lengthScale: 2f, velocity: new Vector3(0f, 0f, -0.55f), loop: true, rateOverTime: 42f, duration: 0.8f, maxParticleSize: 0.12f);
            SavePrefab(root, FlightPrefabPath);
        }

        private static void BuildImpactPrefab(Material core, Material beam)
        {
            GameObject root = CreateRoot("RuneTurret_Impact");
            CreateBurst(root.transform, "ImpactFlash", core, ParticleSystemShapeType.Sphere, 52, 0.28f, 0.13f, 0.22f, Vector3.zero, ParticleSystemRenderMode.Billboard, maxParticleSize: 0.4f);
            CreateBurst(root.transform, "ImpactRing", beam, ParticleSystemShapeType.Circle, 62, 0.42f, 0.075f, 0.86f, Vector3.zero, ParticleSystemRenderMode.Stretch, lengthScale: 1.45f, maxParticleSize: 0.2f);
            SavePrefab(root, ImpactPrefabPath);
        }

        private static void BuildExpirePrefab(Material beam, Material rune)
        {
            GameObject root = CreateRoot("RuneTurret_Expire");
            CreateBurst(root.transform, "ExpireRune", rune, ParticleSystemShapeType.Circle, 46, 0.5f, 0.08f, 0.74f, Vector3.zero, ParticleSystemRenderMode.Billboard, maxParticleSize: 0.24f);
            CreateBurst(root.transform, "DissolveMotes", beam, ParticleSystemShapeType.Sphere, 48, 0.72f, 0.04f, 0.62f, new Vector3(0f, 0.24f, 0f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.12f);
            SavePrefab(root, ExpirePrefabPath);
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
                shape.angle = 10f;
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
                    new GradientColorKey(Color.white, 0.34f),
                    new GradientColorKey(color, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, 0.08f),
                    new GradientAlphaKey(color.a * 0.68f, 0.5f),
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
            ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SceneProfilePath);
            if (profile == null)
                return;

            var serialized = new SerializedObject(profile);
            AssignPrefab(serialized, "_runeTurretDeployVfxPrefab", DeployPrefabPath);
            AssignPrefab(serialized, "_runeTurretIdleVfxPrefab", IdlePrefabPath);
            AssignPrefab(serialized, "_runeTurretFireVfxPrefab", FirePrefabPath);
            AssignPrefab(serialized, "_runeTurretFlightVfxPrefab", FlightPrefabPath);
            AssignPrefab(serialized, "_runeTurretImpactVfxPrefab", ImpactPrefabPath);
            AssignPrefab(serialized, "_runeTurretExpireVfxPrefab", ExpirePrefabPath);
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
