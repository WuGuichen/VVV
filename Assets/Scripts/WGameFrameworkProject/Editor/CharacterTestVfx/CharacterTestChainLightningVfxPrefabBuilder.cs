using MxFramework.Game.CharacterTest;
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class CharacterTestChainLightningVfxPrefabBuilder
    {
        private const string MenuPath = "MxFramework/CharacterTest/VFX/Rebuild/Chain Lightning Prefabs";
        private const string RootPath = "Assets/VFX/MxFramework/Samples/ChainLightning";
        private const string MaterialRootPath = RootPath + "/Materials";
        private const string PrefabRootPath = RootPath + "/Prefabs";
        private const string ChargePrefabPath = PrefabRootPath + "/ChainLightning_Charge.prefab";
        private const string ArcPrefabPath = PrefabRootPath + "/ChainLightning_Arc.prefab";
        private const string ImpactPrefabPath = PrefabRootPath + "/ChainLightning_Impact.prefab";
        private const string SceneProfilePath =
            "Assets/Config/MxFramework/CharacterTest/CharacterTestSceneProfile.asset";

        [MenuItem(MenuPath)]
        public static void RebuildPrefabs()
        {
            EnsureFolder("Assets/VFX", "MxFramework");
            EnsureFolder("Assets/VFX/MxFramework", "Samples");
            EnsureFolder("Assets/VFX/MxFramework/Samples", "ChainLightning");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Prefabs");

            Material core = LoadOrCreateMaterial(
                MaterialRootPath + "/ChainLightning_Core.mat",
                new Color(0.9f, 1f, 1f, 0.94f),
                additive: true);
            Material halo = LoadOrCreateMaterial(
                MaterialRootPath + "/ChainLightning_Halo.mat",
                new Color(0.25f, 0.62f, 1f, 0.42f),
                additive: true);
            Material sparks = LoadOrCreateMaterial(
                MaterialRootPath + "/ChainLightning_Sparks.mat",
                new Color(0.7f, 0.92f, 1f, 0.84f),
                additive: true);

            BuildChargePrefab(core, halo, sparks);
            BuildArcPrefab(core);
            BuildImpactPrefab(core, halo, sparks);
            AssignSceneProfilePrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildChargePrefab(Material core, Material halo, Material sparks)
        {
            GameObject root = CreateRoot("ChainLightning_Charge");
            CreateBurst(root.transform, "ChargeCore", core, ParticleSystemShapeType.Sphere, 90, 0.5f, 0.1f, 0.24f, Vector3.zero, new Color(0.86f, 1f, 1f, 0.86f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.36f);
            CreateBurst(root.transform, "ChargeForks", halo, ParticleSystemShapeType.Cone, 72, 0.48f, 0.045f, 0.32f, new Vector3(0f, 0f, 0.1f), new Color(0.36f, 0.74f, 1f, 0.56f), ParticleSystemRenderMode.Stretch, lengthScale: 2.4f, velocity: new Vector3(0f, 0.08f, 1.05f), maxParticleSize: 0.15f);
            CreateBurst(root.transform, "ChargeSparks", sparks, ParticleSystemShapeType.Sphere, 64, 0.6f, 0.035f, 0.46f, Vector3.zero, new Color(0.72f, 0.94f, 1f, 0.78f), ParticleSystemRenderMode.Stretch, lengthScale: 1.8f, velocity: new Vector3(0f, 0.14f, 0.22f), maxParticleSize: 0.1f);
            SavePrefab(root, ChargePrefabPath);
        }

        private static void BuildArcPrefab(Material lineMaterial)
        {
            Type viewType = ResolveArcViewType();
            MethodInfo createPrefab = viewType.GetMethod(
                "CreateRuntimePrefab",
                BindingFlags.Public | BindingFlags.Static);
            if (createPrefab == null)
                throw new InvalidOperationException("CharacterTestChainLightningArcVfxView.CreateRuntimePrefab is missing.");

            GameObject root = (GameObject)createPrefab.Invoke(null, null);
            root.name = "ChainLightning_Arc";
            root.hideFlags = HideFlags.None;
            root.SetActive(true);
            Component view = root.GetComponent(viewType);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_lineMaterialOverride").objectReferenceValue = lineMaterial;
            serialized.FindProperty("_lengthMeters").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            MethodInfo rebuild = viewType.GetMethod(
                "RebuildVisuals",
                BindingFlags.Public | BindingFlags.Instance);
            rebuild?.Invoke(view, null);
            SavePrefab(root, ArcPrefabPath);
        }

        private static Type ResolveArcViewType()
        {
            Type viewType = Type.GetType(
                "MxFramework.Game.CharacterTest.CharacterTestChainLightningArcVfxView, MxFramework.Game.CharacterTest");
            if (viewType != null)
                return viewType;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                viewType = assembly.GetType("MxFramework.Game.CharacterTest.CharacterTestChainLightningArcVfxView");
                if (viewType != null)
                    return viewType;
            }

            throw new InvalidOperationException("CharacterTestChainLightningArcVfxView type is not loaded.");
        }

        private static void BuildImpactPrefab(Material core, Material halo, Material sparks)
        {
            GameObject root = CreateRoot("ChainLightning_Impact");
            CreateBurst(root.transform, "ImpactFlash", core, ParticleSystemShapeType.Sphere, 72, 0.24f, 0.15f, 0.9f, Vector3.zero, new Color(0.9f, 1f, 1f, 0.86f), ParticleSystemRenderMode.Billboard, maxParticleSize: 0.78f);
            CreateBurst(root.transform, "ImpactForkRing", halo, ParticleSystemShapeType.Circle, 58, 0.34f, 0.055f, 0.72f, Vector3.zero, new Color(0.32f, 0.72f, 1f, 0.58f), ParticleSystemRenderMode.Stretch, lengthScale: 1.7f, maxParticleSize: 0.2f);
            CreateBurst(root.transform, "ImpactSparks", sparks, ParticleSystemShapeType.Sphere, 82, 0.42f, 0.032f, 0.36f, Vector3.zero, new Color(0.76f, 0.96f, 1f, 0.88f), ParticleSystemRenderMode.Stretch, lengthScale: 2.2f, velocity: new Vector3(0f, 0.16f, 0.28f), maxParticleSize: 0.1f);
            SavePrefab(root, ImpactPrefabPath);
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
            float maxParticleSize = 1f)
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
                shape.angle = 18f;
                shape.length = 1.05f;
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
                    new GradientColorKey(Color.white, 0.4f),
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
            renderer.sortingFudge = 0.25f;
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
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void AssignSceneProfilePrefabs()
        {
            ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SceneProfilePath);
            if (profile == null)
                return;

            var serialized = new SerializedObject(profile);
            AssignPrefab(serialized, "_chainLightningChargeVfxPrefab", ChargePrefabPath);
            AssignPrefab(serialized, "_chainLightningArcVfxPrefab", ArcPrefabPath);
            AssignPrefab(serialized, "_chainLightningImpactVfxPrefab", ImpactPrefabPath);
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
