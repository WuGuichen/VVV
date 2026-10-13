using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Explicit resource preparation owned by the existing shallow-water panel. No scene or global catalog writes.</summary>
    public static class CharacterTestShallowWaterSplashAssetBuilder
    {
        public const string PrefabPath = "Assets/Art/WGame/Prefabs/Environment/ShallowWater/FootSplash.prefab";
        public const string MaterialPath = "Assets/Art/WGame/Materials/Environment/ShallowWaterDroplets.mat";
        private const string ShaderPath = "Assets/Art/WGame/Shaders/Environment/ShallowWaterDroplets.shader";
        private const string TexturePath = "Assets/Piloto Studio/Textures/Water_drop_nomipmap.png";

        public static GameObject EnsurePrefab() => EnsurePrefab(PrefabPath, MaterialPath);

        internal static GameObject EnsurePrefab(string prefabPath, string materialPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在编辑模式准备水花资源。");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (shader == null || texture == null) throw new InvalidOperationException("水花 Shader 或已验证的水滴纹理缺失。");
            EnsureFolder(System.IO.Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
            EnsureFolder(System.IO.Path.GetDirectoryName(materialPath).Replace('\\', '/'));
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool createdMaterial = false;
            if (material == null)
            {
                material = new Material(shader) { name = "ShallowWaterDroplets" };
                material.SetTexture("_BaseMap", texture);
                AssetDatabase.CreateAsset(material, materialPath);
                createdMaterial = true;
            }
            var root = new GameObject("FootSplash");
            try
            {
                root.SetActive(false);
                var particles = root.AddComponent<ParticleSystem>();
                var main = particles.main;
                main.loop = false; main.playOnAwake = false; main.duration = .65f;
                main.startLifetime = .5f; main.startSpeed = 0; main.startSize3D = true;
                main.startSizeX = .04f; main.startSizeY = .08f; main.startSizeZ = .04f;
                main.maxParticles = CharacterTestShallowWaterSplashPolicy.HardMaximumParticlesPerBurst;
                main.gravityModifier = .85f; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Shape;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.stopAction = ParticleSystemStopAction.None; main.useUnscaledTime = false;
                particles.useAutoRandomSeed = false; particles.randomSeed = 1;
                var emission = particles.emission; emission.enabled = false;
                var shape = particles.shape; shape.enabled = false;
                var collision = particles.collision; collision.enabled = false;
                var subEmitters = particles.subEmitters; subEmitters.enabled = false;
                var trails = particles.trails; trails.enabled = false;
                var color = particles.colorOverLifetime; color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(.85f, 0), new GradientAlphaKey(.8f, .3f), new GradientAlphaKey(0, 1) });
                color.color = new ParticleSystem.MinMaxGradient(gradient);
                var size = particles.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(
                    new Keyframe(0, .55f), new Keyframe(.2f, 1), new Keyframe(1, .25f)));
                var renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.maxParticleSize = .1f;
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null) throw new InvalidOperationException("无法保存水花 Prefab。");
                // Reusing a user's dirty material must not commit their unrelated edits.
                if (createdMaterial) AssetDatabase.SaveAssetIfDirty(material);
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, separator));
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }
    }
}
