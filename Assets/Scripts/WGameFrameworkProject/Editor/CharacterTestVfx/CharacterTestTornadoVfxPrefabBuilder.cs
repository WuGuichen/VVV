using MxFramework.Game.CharacterTest;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class CharacterTestTornadoVfxPrefabBuilder
    {
        private const string MenuPath = "MxFramework/CharacterTest/VFX/Rebuild/Tornado Prefabs";
        private const string RootPath = "Assets/VFX/MxFramework/Samples/Tornado";
        private const string MaterialRootPath = RootPath + "/Materials";
        private const string PrefabRootPath = RootPath + "/Prefabs";
        private const string FlightPrefabPath = PrefabRootPath + "/Tornado_Flight.prefab";
        private const string ChargePrefabPath = PrefabRootPath + "/Tornado_Charge.prefab";
        private const string ImpactPrefabPath = PrefabRootPath + "/Tornado_Impact.prefab";
        private const string TickPrefabPath = PrefabRootPath + "/Tornado_Tick.prefab";
        private const string SceneProfilePath =
            "Assets/Config/MxFramework/CharacterTest/CharacterTestSceneProfile.asset";

        [MenuItem(MenuPath)]
        public static void RebuildPrefabs()
        {
            EnsureFolder("Assets/VFX", "MxFramework");
            EnsureFolder("Assets/VFX/MxFramework", "Samples");
            EnsureFolder("Assets/VFX/MxFramework/Samples", "Tornado");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Prefabs");

            Material airColumn = LoadOrCreateMaterial(
                MaterialRootPath + "/Tornado_AirColumn.mat",
                new Color(0.72f, 0.92f, 1f, 0.18f));
            Material ribbons = LoadOrCreateMaterial(
                MaterialRootPath + "/Tornado_WindRibbons.mat",
                new Color(0.92f, 0.98f, 1f, 0.48f));
            Material dust = LoadOrCreateMaterial(
                MaterialRootPath + "/Tornado_GroundDust.mat",
                new Color(0.62f, 0.55f, 0.42f, 0.34f));
            Material debris = LoadOrCreateMaterial(
                MaterialRootPath + "/Tornado_Debris.mat",
                new Color(0.38f, 0.33f, 0.25f, 0.8f));
            Material impactWind = LoadOrCreateMaterial(
                MaterialRootPath + "/Tornado_ImpactWind.mat",
                new Color(0.72f, 0.94f, 1f, 0.46f));

            BuildFlightPrefab(airColumn, ribbons, dust, debris, impactWind);
            BuildChargePrefab(airColumn, ribbons, dust);
            BuildImpactPrefab(impactWind, dust, debris, airColumn);
            BuildTickPrefab(impactWind, ribbons);
            AssignSceneProfilePrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildFlightPrefab(
            Material airColumn,
            Material ribbons,
            Material dust,
            Material debris,
            Material impactWind)
        {
            GameObject root = CreateRoot("Tornado_Flight", duration: 3f);
            CreateParticleSystem(root.transform, "CoreColumn", airColumn, 34, 1.4f, 0.24f, 0.34f, 18f, -0.03f, 0.85f, new Color(0.82f, 0.96f, 1f, 0.18f), ParticleSystemShapeType.Cone, 0.18f, 7f, 3.1f, maxParticleSize: 0.62f);
            CreateParticleSystem(root.transform, "InnerVortex", impactWind, 58, 1.45f, 0.42f, 0.15f, 32f, -0.08f, 1.05f, new Color(0.76f, 0.94f, 1f, 0.34f), ParticleSystemShapeType.Cone, 0.34f, 22f, 2.9f, renderMode: ParticleSystemRenderMode.Stretch, lengthScale: 2.25f, velocityScale: 0.18f, maxParticleSize: 0.38f);
            CreateParticleSystem(root.transform, "GroundDustRing", dust, 32, 0.9f, 0.15f, 0.16f, 12f, 0.18f, 0.0f, new Color(0.66f, 0.58f, 0.42f, 0.36f), ParticleSystemShapeType.Circle, 0.9f, 0f, 0f, maxParticleSize: 0.3f);
            CreateParticleSystem(root.transform, "DebrisSpiral", debris, 16, 1.35f, 0.38f, 0.07f, 28f, -0.03f, 1.0f, new Color(0.46f, 0.37f, 0.24f, 0.78f), ParticleSystemShapeType.Cone, 0.62f, 28f, 2.4f, renderMode: ParticleSystemRenderMode.Stretch, lengthScale: 1.35f, velocityScale: 0.1f, maxParticleSize: 0.2f);
            CreateParticleSystem(root.transform, "WindRibbons", ribbons, 30, 1.55f, 0.35f, 0.12f, -38f, -0.1f, 1.15f, new Color(0.92f, 0.98f, 1f, 0.46f), ParticleSystemShapeType.Cone, 0.7f, 32f, 3.15f, renderMode: ParticleSystemRenderMode.Stretch, lengthScale: 3.2f, velocityScale: 0.22f, maxParticleSize: 0.34f);
            CreateParticleSystem(root.transform, "AttractionFieldRing", impactWind, 14, 0.8f, 0.05f, 0.045f, 5f, -0.18f, 0.0f, new Color(0.58f, 0.86f, 1f, 0.16f), ParticleSystemShapeType.Circle, 1.45f, 0f, 0f, maxParticleSize: 0.12f);
            SavePrefab(root, FlightPrefabPath);
        }

        private static void BuildChargePrefab(Material airColumn, Material ribbons, Material dust)
        {
            GameObject root = CreateRoot("Tornado_Charge", duration: 0.45f);
            CreateParticleSystem(root.transform, "ChargeGroundSpiral", dust, 90, 0.8f, 0.25f, 0.36f, 9f, 0.18f, 0f, new Color(0.72f, 0.65f, 0.52f, 0.65f), ParticleSystemShapeType.Circle, 1.1f, 0f, 0f);
            CreateParticleSystem(root.transform, "ChargeRisingWisps", airColumn, 70, 1.1f, 0.65f, 0.45f, 10f, -0.05f, 1.3f, new Color(0.66f, 0.9f, 1f, 0.55f), ParticleSystemShapeType.Cone, 0.38f, 16f, 1.7f);
            CreateParticleSystem(root.transform, "ChargeDustLift", ribbons, 42, 0.9f, 0.5f, 0.2f, 8f, 0.02f, 0.7f, new Color(0.9f, 0.96f, 1f, 0.62f), ParticleSystemShapeType.Cone, 0.7f, 22f, 1.5f);
            SavePrefab(root, ChargePrefabPath);
        }

        private static void BuildImpactPrefab(
            Material impactWind,
            Material dust,
            Material debris,
            Material airColumn)
        {
            GameObject root = CreateRoot("Tornado_Impact", duration: 1.2f);
            CreateParticleSystem(root.transform, "ImpactWindBurst", impactWind, 150, 0.9f, 3.4f, 0.42f, 3f, 0.8f, 0.15f, new Color(0.72f, 0.94f, 1f, 0.78f), ParticleSystemShapeType.Circle, 0.35f, 0f, 0f, loop: false);
            CreateParticleSystem(root.transform, "ImpactDustShockRing", dust, 100, 0.9f, 2.5f, 0.5f, 2.5f, 1.3f, 0f, new Color(0.66f, 0.58f, 0.44f, 0.7f), ParticleSystemShapeType.Circle, 0.55f, 0f, 0f, loop: false);
            CreateParticleSystem(root.transform, "ImpactDebrisLift", debris, 72, 1.15f, 2f, 0.22f, 5f, 0.4f, 1.6f, new Color(0.44f, 0.35f, 0.24f, 0.9f), ParticleSystemShapeType.Cone, 0.75f, 28f, 1.8f, loop: false);
            CreateParticleSystem(root.transform, "ImpactAirColumnCollapse", airColumn, 90, 1f, 0.75f, 1.25f, 8f, -0.4f, -0.25f, new Color(0.78f, 0.95f, 1f, 0.48f), ParticleSystemShapeType.Cone, 0.95f, 10f, 2.4f, loop: false);
            SavePrefab(root, ImpactPrefabPath);
        }

        private static void BuildTickPrefab(Material impactWind, Material ribbons)
        {
            GameObject root = CreateRoot("Tornado_Tick", duration: 0.55f);
            CreateParticleSystem(root.transform, "TickPulseRing", impactWind, 54, 0.55f, 1.6f, 0.2f, 4f, 0.65f, 0f, new Color(0.62f, 0.9f, 1f, 0.58f), ParticleSystemShapeType.Circle, 0.55f, 0f, 0f, loop: false);
            CreateParticleSystem(root.transform, "TickWindWisps", ribbons, 44, 0.65f, 1.1f, 0.18f, 8f, 0.1f, 0.35f, new Color(0.86f, 0.98f, 1f, 0.62f), ParticleSystemShapeType.Cone, 0.85f, 18f, 1.0f, loop: false);
            SavePrefab(root, TickPrefabPath);
        }

        private static GameObject CreateRoot(string name, float duration)
        {
            var root = new GameObject(name);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            _ = duration;
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static ParticleSystem CreateParticleSystem(
            Transform parent,
            string name,
            Material material,
            float rate,
            float lifetime,
            float speed,
            float size,
            float orbitalY,
            float radial,
            float yVelocity,
            Color color,
            ParticleSystemShapeType shapeType,
            float radius,
            float angle,
            float length,
            bool loop = true,
            ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard,
            float lengthScale = 1f,
            float velocityScale = 0f,
            float maxParticleSize = 1.5f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            var main = system.main;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.loop = loop;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = system.emission;
            emission.rateOverTime = loop ? rate : 0f;
            emission.SetBursts(loop
                ? new ParticleSystem.Burst[0]
                : new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, Mathf.RoundToInt(rate))) });

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = shapeType;
            shape.radius = radius;
            shape.angle = angle;
            shape.length = length;
            shape.arc = 360f;

            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalY = orbitalY;
            velocity.radial = radial;
            velocity.y = yVelocity;

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
                    new GradientAlphaKey(color.a, 0.18f),
                    new GradientAlphaKey(0f, 1f)
                }
            });

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = renderMode;
            renderer.lengthScale = lengthScale;
            renderer.velocityScale = velocityScale;
            renderer.sortingFudge = 0.1f;
            renderer.minParticleSize = 0.01f;
            renderer.maxParticleSize = maxParticleSize;

            system.Play();
            return system;
        }

        private static Material LoadOrCreateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = CharacterTestUnlitMaterials.CreateVfx(Path.GetFileNameWithoutExtension(path), color, false);
                AssetDatabase.CreateAsset(material, path);
            }
            else CharacterTestUnlitMaterials.ConfigureVfx(material, color, false);
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
            AssignPrefab(serialized, "_tornadoFlightVfxPrefab", FlightPrefabPath);
            AssignPrefab(serialized, "_tornadoChargeVfxPrefab", ChargePrefabPath);
            AssignPrefab(serialized, "_tornadoImpactVfxPrefab", ImpactPrefabPath);
            AssignPrefab(serialized, "_tornadoTickVfxPrefab", TickPrefabPath);
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
