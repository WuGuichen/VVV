using MxFramework.Game.CharacterTest;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class CharacterTestChannelBeamVfxPrefabBuilder
    {
        private const string MenuPath = "MxFramework/CharacterTest/VFX/Rebuild/Channel Beam Prefabs";
        private const string RootPath = "Assets/VFX/MxFramework/Samples/ChannelBeam";
        private const string MaterialRootPath = RootPath + "/Materials";
        private const string PrefabRootPath = RootPath + "/Prefabs";
        private const string ChargePrefabPath = PrefabRootPath + "/ChannelBeam_Charge.prefab";
        private const string BeamPrefabPath = PrefabRootPath + "/ChannelBeam_Beam.prefab";
        private const string ImpactPrefabPath = PrefabRootPath + "/ChannelBeam_Impact.prefab";
        private const string SceneProfilePath =
            "Assets/Config/MxFramework/CharacterTest/CharacterTestSceneProfile.asset";

        [MenuItem(MenuPath)]
        public static void RebuildPrefabs()
        {
            EnsureFolder("Assets/VFX", "MxFramework");
            EnsureFolder("Assets/VFX/MxFramework", "Samples");
            EnsureFolder("Assets/VFX/MxFramework/Samples", "ChannelBeam");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Prefabs");

            Material core = LoadOrCreateMaterial(
                MaterialRootPath + "/ChannelBeam_Core.mat",
                new Color(0.58f, 0.95f, 1f, 0.9f),
                additive: true);
            Material halo = LoadOrCreateMaterial(
                MaterialRootPath + "/ChannelBeam_Halo.mat",
                new Color(0.12f, 0.56f, 1f, 0.34f),
                additive: true);
            Material sparks = LoadOrCreateMaterial(
                MaterialRootPath + "/ChannelBeam_Sparks.mat",
                new Color(0.82f, 1f, 1f, 0.76f),
                additive: true);
            Material impact = LoadOrCreateMaterial(
                MaterialRootPath + "/ChannelBeam_Impact.mat",
                new Color(0.7f, 0.96f, 1f, 0.72f),
                additive: true);

            BuildChargePrefab(core, halo, sparks);
            BuildBeamPrefab(core, halo, sparks, impact);
            BuildImpactPrefab(core, halo, sparks, impact);
            AssignSceneProfilePrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildChargePrefab(Material core, Material halo, Material sparks)
        {
            GameObject root = CreateRoot("ChannelBeam_Charge");
            CreateSphereBurst(root.transform, "ChargeCore", core, 110, 0.52f, 0.12f, 0.26f, Vector3.zero, new Color(0.76f, 1f, 1f, 0.88f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.42f);
            CreateConeBurst(root.transform, "ChargeGatherRays", halo, 90, 0.58f, 0.06f, 0.34f, 14f, 1.15f, new Vector3(0f, 0f, 0.12f), new Color(0.3f, 0.85f, 1f, 0.54f), ParticleSystemRenderMode.Stretch, lengthScale: 2.8f, velocityScale: 0.12f, maxParticleSize: 0.16f);
            CreateSphereBurst(root.transform, "ChargeMotes", sparks, 70, 0.68f, 0.045f, 0.5f, Vector3.zero, new Color(0.82f, 1f, 1f, 0.72f), ParticleSystemRenderMode.Stretch, lengthScale: 1.6f, velocityScale: 0.16f, maxParticleSize: 0.11f);
            SavePrefab(root, ChargePrefabPath);
        }

        private static void BuildBeamPrefab(Material core, Material halo, Material sparks, Material impact)
        {
            GameObject root = CreateRoot("ChannelBeam_Beam");
            CreateBoxBurst(root.transform, "BeamCore", core, 180, 1.48f, 0.055f, new Vector3(0.06f, 0.06f, 31.6f), new Vector3(0f, 0f, 15.8f), Color.white, ParticleSystemRenderMode.Stretch, lengthScale: 3.2f, maxParticleSize: 0.15f);
            CreateBoxBurst(root.transform, "BeamHalo", halo, 160, 1.5f, 0.2f, new Vector3(0.3f, 0.3f, 31.6f), new Vector3(0f, 0f, 15.8f), new Color(0.24f, 0.78f, 1f, 0.34f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.38f);
            CreateBoxBurst(root.transform, "BeamPulseMotes", halo, 92, 1.25f, 0.08f, new Vector3(0.52f, 0.52f, 30f), new Vector3(0f, 0f, 15f), new Color(0.54f, 0.96f, 1f, 0.44f), ParticleSystemRenderMode.Billboard, velocity: new Vector3(0f, 0.12f, 0.45f), maxParticleSize: 0.14f);
            CreateBoxBurst(root.transform, "BeamNeedleSparks", sparks, 96, 0.72f, 0.032f, new Vector3(0.26f, 0.26f, 31.2f), new Vector3(0f, 0f, 15.6f), new Color(0.82f, 1f, 1f, 0.82f), ParticleSystemRenderMode.Stretch, lengthScale: 2.1f, velocityScale: 0.1f, velocity: new Vector3(0f, 0.08f, 1.6f), maxParticleSize: 0.08f);
            CreateSphereBurst(root.transform, "BeamEndFlash", impact, 56, 0.38f, 0.28f, 0.52f, new Vector3(0f, 0f, 31.8f), new Color(0.78f, 1f, 1f, 0.62f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.78f);
            SavePrefab(root, BeamPrefabPath);
        }

        private static void BuildImpactPrefab(Material core, Material halo, Material sparks, Material impact)
        {
            GameObject root = CreateRoot("ChannelBeam_Impact");
            CreateSphereBurst(root.transform, "ImpactFlash", impact, 74, 0.24f, 0.14f, 1.25f, Vector3.zero, new Color(0.82f, 1f, 1f, 0.78f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.9f);
            CreateCircleBurst(root.transform, "ImpactRing", halo, 52, 0.36f, 0.055f, 0.78f, Vector3.zero, new Color(0.28f, 0.86f, 1f, 0.5f), ParticleSystemRenderMode.Stretch, lengthScale: 1.4f, maxParticleSize: 0.22f);
            CreateSphereBurst(root.transform, "ImpactNeedles", sparks, 86, 0.38f, 0.032f, 0.34f, Vector3.zero, new Color(0.82f, 1f, 1f, 0.88f), ParticleSystemRenderMode.Stretch, lengthScale: 2.2f, velocityScale: 0.2f, maxParticleSize: 0.09f);
            CreateSphereBurst(root.transform, "ImpactAfterglow", core, 32, 0.48f, 0.2f, 0.48f, Vector3.zero, new Color(0.28f, 0.82f, 1f, 0.28f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.62f);
            SavePrefab(root, ImpactPrefabPath);
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
            ParticleSystem system = CreateBaseBurst(parent, name, material, count, lifetime, size, color, renderMode, lengthScale, velocityScale, new Vector3(0f, 0.14f, 0.28f), maxParticleSize);
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
            ParticleSystem system = CreateBaseBurst(parent, name, material, count, lifetime, size, color, renderMode, lengthScale, velocityScale, new Vector3(0f, 0.08f, 0.24f), maxParticleSize);
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
            ParticleSystem system = CreateBaseBurst(parent, name, material, count, lifetime, size, color, renderMode, lengthScale, velocityScale, new Vector3(0f, 0.08f, 1.1f), maxParticleSize);
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
                    new GradientColorKey(Color.white, 0.42f),
                    new GradientColorKey(color, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, 0.08f),
                    new GradientAlphaKey(color.a * 0.68f, 0.48f),
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
            AssignPrefab(serialized, "_channelBeamChargeVfxPrefab", ChargePrefabPath);
            AssignPrefab(serialized, "_channelBeamBeamVfxPrefab", BeamPrefabPath);
            AssignPrefab(serialized, "_channelBeamImpactVfxPrefab", ImpactPrefabPath);
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
