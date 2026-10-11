using MxFramework.Game.CharacterTest;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class CharacterTestBeamLanceVfxPrefabBuilder
    {
        private const string MenuPath = "MxFramework/CharacterTest/VFX/Rebuild/Beam Lance Prefabs";
        private const string RootPath = "Assets/VFX/MxFramework/Samples/BeamLance";
        private const string MaterialRootPath = RootPath + "/Materials";
        private const string PrefabRootPath = RootPath + "/Prefabs";
        private const string BeamPrefabPath = PrefabRootPath + "/BeamLance_Beam.prefab";
        private const string ImpactPrefabPath = PrefabRootPath + "/BeamLance_Impact.prefab";
        private const string ChargePrefabPath = PrefabRootPath + "/BeamLance_Charge.prefab";
        private const string SceneProfilePath =
            "Assets/Config/MxFramework/CharacterTest/CharacterTestSceneProfile.asset";

        [MenuItem(MenuPath)]
        public static void RebuildPrefabs()
        {
            EnsureFolder("Assets/VFX", "MxFramework");
            EnsureFolder("Assets/VFX/MxFramework", "Samples");
            EnsureFolder("Assets/VFX/MxFramework/Samples", "BeamLance");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Prefabs");

            Material core = LoadOrCreateMaterial(
                MaterialRootPath + "/BeamLance_Core.mat",
                new Color(0.86f, 1f, 1f, 0.92f),
                additive: true);
            Material halo = LoadOrCreateMaterial(
                MaterialRootPath + "/BeamLance_Halo.mat",
                new Color(0.18f, 0.72f, 1f, 0.34f),
                additive: true);
            Material sparks = LoadOrCreateMaterial(
                MaterialRootPath + "/BeamLance_Sparks.mat",
                new Color(0.66f, 0.98f, 1f, 0.82f),
                additive: true);
            Material impact = LoadOrCreateMaterial(
                MaterialRootPath + "/BeamLance_Impact.mat",
                new Color(0.9f, 1f, 1f, 0.74f),
                additive: true);

            BuildBeamPrefab(core, halo, sparks, impact);
            BuildImpactPrefab(core, halo, sparks, impact);
            BuildChargePrefab(core, halo, sparks);
            AssignSceneProfilePrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildBeamPrefab(Material core, Material halo, Material sparks, Material impact)
        {
            GameObject root = CreateRoot("BeamLance_Beam");
            CreateBoxBurst(root.transform, "BeamCore", core, 140, 0.18f, 0.07f, new Vector3(0.055f, 0.055f, 43.5f), new Vector3(0f, 0f, 21.75f), Color.white, ParticleSystemRenderMode.Stretch, lengthScale: 3.6f, maxParticleSize: 0.18f);
            CreateBoxBurst(root.transform, "BeamHalo", halo, 120, 0.24f, 0.23f, new Vector3(0.34f, 0.34f, 43.5f), new Vector3(0f, 0f, 21.75f), new Color(0.35f, 0.86f, 1f, 0.38f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.45f);
            CreateBoxBurst(root.transform, "BeamNeedleSparks", sparks, 76, 0.32f, 0.035f, new Vector3(0.28f, 0.28f, 42f), new Vector3(0f, 0f, 21f), new Color(0.72f, 1f, 1f, 0.86f), ParticleSystemRenderMode.Stretch, lengthScale: 2.2f, velocityScale: 0.12f, velocity: new Vector3(0f, 0.08f, 2.2f), maxParticleSize: 0.1f);
            CreateBoxBurst(root.transform, "BeamMotes", halo, 54, 0.42f, 0.085f, new Vector3(0.62f, 0.62f, 39f), new Vector3(0f, 0f, 19.5f), new Color(0.5f, 0.95f, 1f, 0.5f), ParticleSystemRenderMode.Billboard, velocity: new Vector3(0f, 0.2f, 0.6f), maxParticleSize: 0.16f);
            CreateSphereBurst(root.transform, "BeamEndFlash", impact, 46, 0.22f, 0.38f, 0.72f, new Vector3(0f, 0f, 43.75f), new Color(0.82f, 1f, 1f, 0.72f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.95f);
            SavePrefab(root, BeamPrefabPath);
        }

        private static void BuildImpactPrefab(Material core, Material halo, Material sparks, Material impact)
        {
            GameObject root = CreateRoot("BeamLance_Impact");
            CreateSphereBurst(root.transform, "ImpactFlash", impact, 96, 0.26f, 0.18f, 2.1f, new Vector3(0f, 0f, 0f), new Color(0.92f, 1f, 1f, 0.82f), ParticleSystemRenderMode.Billboard, maxParticleSize: 1.4f);
            CreateCircleBurst(root.transform, "ImpactRing", halo, 72, 0.42f, 0.08f, 1.2f, new Vector3(0f, 0f, 0f), new Color(0.36f, 0.9f, 1f, 0.6f), ParticleSystemRenderMode.Stretch, lengthScale: 1.7f, maxParticleSize: 0.36f);
            CreateSphereBurst(root.transform, "ImpactSparks", sparks, 120, 0.45f, 0.045f, 0.48f, new Vector3(0f, 0f, 0f), new Color(0.72f, 1f, 1f, 0.92f), ParticleSystemRenderMode.Stretch, lengthScale: 2.8f, velocityScale: 0.24f, maxParticleSize: 0.12f);
            CreateSphereBurst(root.transform, "ImpactAfterglow", core, 42, 0.6f, 0.28f, 0.75f, new Vector3(0f, 0f, 0f), new Color(0.34f, 0.86f, 1f, 0.32f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.9f);
            SavePrefab(root, ImpactPrefabPath);
        }

        private static void BuildChargePrefab(Material core, Material halo, Material sparks)
        {
            GameObject root = CreateRoot("BeamLance_Charge");
            CreateSphereBurst(root.transform, "ChargeCore", core, 74, 0.24f, 0.14f, 0.28f, new Vector3(0f, 0f, 0.2f), new Color(0.82f, 1f, 1f, 0.88f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.44f);
            CreateConeBurst(root.transform, "ChargeRays", halo, 56, 0.28f, 0.08f, 0.35f, 16f, 1.1f, new Vector3(0f, 0f, 0.2f), new Color(0.28f, 0.86f, 1f, 0.56f), ParticleSystemRenderMode.Stretch, lengthScale: 2.6f, velocityScale: 0.15f, maxParticleSize: 0.18f);
            CreateSphereBurst(root.transform, "ChargeMotes", sparks, 44, 0.36f, 0.045f, 0.45f, new Vector3(0f, 0f, 0.2f), new Color(0.72f, 1f, 1f, 0.78f), ParticleSystemRenderMode.Stretch, lengthScale: 1.6f, velocityScale: 0.18f, maxParticleSize: 0.12f);
            SavePrefab(root, ChargePrefabPath);
        }

        private static GameObject CreateRoot(string name)
        {
            var root = new GameObject(name);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static ParticleSystem CreateBoxBurst(
            Transform parent,
            string name,
            Material material,
            int count,
            float lifetime,
            float size,
            Vector3 shapeScale,
            Vector3 localPosition,
            Color color,
            ParticleSystemRenderMode renderMode,
            float lengthScale = 1f,
            float velocityScale = 0f,
            Vector3 velocity = default,
            float maxParticleSize = 1f)
        {
            ParticleSystem system = CreateBaseBurst(parent, name, material, count, lifetime, size, color, renderMode, lengthScale, velocityScale, velocity, maxParticleSize);
            system.transform.localPosition = localPosition;
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = shapeScale;
            return system;
        }

        private static ParticleSystem CreateSphereBurst(
            Transform parent,
            string name,
            Material material,
            int count,
            float lifetime,
            float size,
            float radius,
            Vector3 localPosition,
            Color color,
            ParticleSystemRenderMode renderMode,
            float lengthScale = 1f,
            float velocityScale = 0f,
            float maxParticleSize = 1f)
        {
            ParticleSystem system = CreateBaseBurst(parent, name, material, count, lifetime, size, color, renderMode, lengthScale, velocityScale, new Vector3(0f, 0.18f, 0.28f), maxParticleSize);
            system.transform.localPosition = localPosition;
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            return system;
        }

        private static ParticleSystem CreateCircleBurst(
            Transform parent,
            string name,
            Material material,
            int count,
            float lifetime,
            float size,
            float radius,
            Vector3 localPosition,
            Color color,
            ParticleSystemRenderMode renderMode,
            float lengthScale = 1f,
            float velocityScale = 0f,
            float maxParticleSize = 1f)
        {
            ParticleSystem system = CreateBaseBurst(parent, name, material, count, lifetime, size, color, renderMode, lengthScale, velocityScale, new Vector3(0f, 0.12f, 0.34f), maxParticleSize);
            system.transform.localPosition = localPosition;
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.arc = 360f;
            return system;
        }

        private static ParticleSystem CreateConeBurst(
            Transform parent,
            string name,
            Material material,
            int count,
            float lifetime,
            float size,
            float radius,
            float angle,
            float length,
            Vector3 localPosition,
            Color color,
            ParticleSystemRenderMode renderMode,
            float lengthScale = 1f,
            float velocityScale = 0f,
            float maxParticleSize = 1f)
        {
            ParticleSystem system = CreateBaseBurst(parent, name, material, count, lifetime, size, color, renderMode, lengthScale, velocityScale, new Vector3(0f, 0.12f, 1.4f), maxParticleSize);
            system.transform.localPosition = localPosition;
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = radius;
            shape.angle = angle;
            shape.length = length;
            return system;
        }

        private static ParticleSystem CreateBaseBurst(
            Transform parent,
            string name,
            Material material,
            int count,
            float lifetime,
            float size,
            Color color,
            ParticleSystemRenderMode renderMode,
            float lengthScale,
            float velocityScale,
            Vector3 velocity,
            float maxParticleSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            var main = system.main;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count)) });

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
                    new GradientColorKey(Color.white, 0.45f),
                    new GradientColorKey(color, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, 0.08f),
                    new GradientAlphaKey(color.a * 0.72f, 0.42f),
                    new GradientAlphaKey(0f, 1f)
                }
            });

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = renderMode;
            renderer.lengthScale = lengthScale;
            renderer.velocityScale = velocityScale;
            renderer.sortingFudge = 0.25f;
            renderer.minParticleSize = 0.01f;
            renderer.maxParticleSize = maxParticleSize;

            system.Play();
            return system;
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
            AssignPrefab(serialized, "_beamLanceBeamVfxPrefab", BeamPrefabPath);
            AssignPrefab(serialized, "_beamLanceImpactVfxPrefab", ImpactPrefabPath);
            AssignPrefab(serialized, "_beamLanceChargeVfxPrefab", ChargePrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void AssignPrefab(SerializedObject serialized, string propertyName, string prefabPath)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                return;

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
