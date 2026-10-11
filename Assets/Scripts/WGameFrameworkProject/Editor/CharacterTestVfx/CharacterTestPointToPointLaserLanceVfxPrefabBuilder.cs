using MxFramework.Game.CharacterTest;
using System.IO;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class CharacterTestLaserLanceVfxPrefabBuilder
    {
        private const string MenuPath = "MxFramework/CharacterTest/VFX/Rebuild/Laser Lance Prefabs";
        private const string RootPath = "Assets/VFX/MxFramework/Samples/LaserLance";
        private const string MaterialRootPath = RootPath + "/Materials";
        private const string PrefabRootPath = RootPath + "/Prefabs";
        private const string ChargePrefabPath = PrefabRootPath + "/LaserLance_Charge.prefab";
        private const string PathPrefabPath = PrefabRootPath + "/LaserLance_Laser.prefab";
        private const string ImpactPrefabPath = PrefabRootPath + "/LaserLance_Impact.prefab";
        private const string SceneProfilePath =
            "Assets/Config/MxFramework/CharacterTest/CharacterTestSceneProfile.asset";

        [MenuItem(MenuPath)]
        public static void RebuildPrefabs()
        {
            EnsureFolder("Assets/VFX", "MxFramework");
            EnsureFolder("Assets/VFX/MxFramework", "Samples");
            EnsureFolder("Assets/VFX/MxFramework/Samples", "LaserLance");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Prefabs");

            Material core = LoadOrCreateMaterial(
                MaterialRootPath + "/LaserLance_Core.mat",
                new Color(0.94f, 1f, 1f, 0.92f),
                additive: true);
            Material halo = LoadOrCreateMaterial(
                MaterialRootPath + "/LaserLance_Halo.mat",
                new Color(0.22f, 0.8f, 1f, 0.34f),
                additive: true);
            Material sparks = LoadOrCreateMaterial(
                MaterialRootPath + "/LaserLance_Sparks.mat",
                new Color(0.76f, 1f, 1f, 0.8f),
                additive: true);

            BuildChargePrefab(core, halo, sparks);
            BuildPathPrefab(core);
            BuildImpactPrefab(core, halo, sparks);
            AssignSceneProfilePrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildChargePrefab(Material core, Material halo, Material sparks)
        {
            GameObject root = CreateRoot("LaserLance_Charge");
            CreateBurst(root.transform, "ChargeCore", core, ParticleSystemShapeType.Sphere, 72, 0.36f, 0.12f, 0.28f, Vector3.zero, new Color(0.9f, 1f, 1f, 0.88f), ParticleSystemRenderMode.Billboard);
            CreateBurst(root.transform, "ChargeRays", halo, ParticleSystemShapeType.Cone, 54, 0.42f, 0.05f, 0.36f, new Vector3(0f, 0f, 0.08f), new Color(0.34f, 0.86f, 1f, 0.54f), ParticleSystemRenderMode.Stretch, lengthScale: 2.4f, velocity: new Vector3(0f, 0.08f, 1.2f));
            CreateBurst(root.transform, "ChargeMotes", sparks, ParticleSystemShapeType.Sphere, 46, 0.48f, 0.04f, 0.46f, Vector3.zero, new Color(0.82f, 1f, 1f, 0.76f), ParticleSystemRenderMode.Stretch, lengthScale: 1.4f, velocity: new Vector3(0f, 0.12f, 0.25f));
            SavePrefab(root, ChargePrefabPath);
        }

        private static void BuildPathPrefab(Material lineMaterial)
        {
            Type viewType = ResolveLaserPathViewType();
            MethodInfo createPrefab = viewType.GetMethod(
                "CreateRuntimePrefab",
                BindingFlags.Public | BindingFlags.Static);
            if (createPrefab == null)
                throw new InvalidOperationException("CharacterTestLaserPathVfxView.CreateRuntimePrefab is missing.");

            GameObject root = (GameObject)createPrefab.Invoke(null, null);
            root.name = "LaserLance_Laser";
            root.hideFlags = HideFlags.None;
            root.SetActive(true);
            Component view = root.GetComponent(viewType);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_lineMaterialOverride").objectReferenceValue = lineMaterial;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            MethodInfo rebuild = viewType.GetMethod(
                "RebuildVisuals",
                BindingFlags.Public | BindingFlags.Instance);
            rebuild?.Invoke(view, null);
            SavePrefab(root, PathPrefabPath);
        }

        private static Type ResolveLaserPathViewType()
        {
            Type viewType = Type.GetType(
                "MxFramework.Game.CharacterTest.CharacterTestLaserPathVfxView, MxFramework.Game.CharacterTest");
            if (viewType != null)
                return viewType;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                viewType = assembly.GetType("MxFramework.Game.CharacterTest.CharacterTestLaserPathVfxView");
                if (viewType != null)
                    return viewType;
            }

            throw new InvalidOperationException("CharacterTestLaserPathVfxView type is not loaded.");
        }

        private static void BuildImpactPrefab(Material core, Material halo, Material sparks)
        {
            GameObject root = CreateRoot("LaserLance_Impact");
            CreateBurst(root.transform, "ImpactFlash", core, ParticleSystemShapeType.Sphere, 84, 0.24f, 0.16f, 1.2f, Vector3.zero, new Color(0.94f, 1f, 1f, 0.84f), ParticleSystemRenderMode.Billboard);
            CreateBurst(root.transform, "ImpactRing", halo, ParticleSystemShapeType.Circle, 62, 0.34f, 0.06f, 0.86f, Vector3.zero, new Color(0.36f, 0.9f, 1f, 0.58f), ParticleSystemRenderMode.Stretch, lengthScale: 1.5f);
            CreateBurst(root.transform, "ImpactNeedles", sparks, ParticleSystemShapeType.Sphere, 94, 0.36f, 0.03f, 0.38f, Vector3.zero, new Color(0.82f, 1f, 1f, 0.88f), ParticleSystemRenderMode.Stretch, lengthScale: 2.2f, velocity: new Vector3(0f, 0.16f, 0.34f));
            SavePrefab(root, ImpactPrefabPath);
        }

        private static GameObject CreateRoot(string name)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return root;
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
            Vector3 velocity = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
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

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = shapeType;
            shape.radius = radius;
            if (shapeType == ParticleSystemShapeType.Cone)
            {
                shape.angle = 14f;
                shape.length = 1.1f;
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
                    new GradientColorKey(Color.white, 0.45f),
                    new GradientColorKey(color, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, 0.08f),
                    new GradientAlphaKey(color.a * 0.65f, 0.42f),
                    new GradientAlphaKey(0f, 1f)
                }
            });

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = renderMode;
            renderer.lengthScale = lengthScale;
            renderer.sortingFudge = 0.25f;
            renderer.minParticleSize = 0.01f;
            renderer.maxParticleSize = 0.9f;
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
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void AssignSceneProfilePrefabs()
        {
            ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SceneProfilePath);
            if (profile == null)
                return;

            var serialized = new SerializedObject(profile);
            AssignPrefab(serialized, "_laserLanceChargeVfxPrefab", ChargePrefabPath);
            AssignPrefab(serialized, "_laserLancePathVfxPrefab", PathPrefabPath);
            AssignPrefab(serialized, "_laserLanceImpactVfxPrefab", ImpactPrefabPath);
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
