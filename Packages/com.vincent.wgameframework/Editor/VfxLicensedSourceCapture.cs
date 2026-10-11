using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Captures fixed-seed source/candidate comparison evidence for licensed VFX review.</summary>
    /// <remarks>Editor-only, AllocByDesign. Evidence is rebuildable and remains below Temp.</remarks>
    internal static class VfxLicensedSourceCapture
    {
        private static readonly float[] Fractions = { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 1f };
        private static readonly string[] ViewIds = { "front-three-quarter", "side", "top" };
        private static readonly Vector3[] ViewDirections =
        {
            new Vector3(0.65f, 0.35f, -1f).normalized,
            new Vector3(1f, 0.15f, 0f).normalized,
            new Vector3(0f, 1f, -0.08f).normalized
        };

        public static bool CaptureComparison(string sourceAssetPath, string candidateAssetPath, out string message)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourceAssetPath);
            GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(candidateAssetPath);
            if (source == null || candidate == null)
            {
                message = "请选择既有源效果和对应的已提取/组合 Candidate 后再捕获。";
                return false;
            }

            string root = "Temp/MxFramework/VfxReferencePacks/Captures/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var manifest = new JObject
            {
                ["schemaVersion"] = "mx.vfx.licensed-comparison-capture.v1",
                ["fixedSeed"] = 17,
                ["sourceAssetPath"] = sourceAssetPath,
                ["candidateAssetPath"] = candidateAssetPath,
                ["captures"] = new JArray()
            };

            try
            {
                CaptureSet(source, "source", root, (JArray)manifest["captures"]);
                CaptureSet(candidate, "candidate", root, (JArray)manifest["captures"]);
                Directory.CreateDirectory(root);
                File.WriteAllText(root + "/manifest.json", manifest.ToString());
                message = "已生成固定种子、多时间点、多视角对比证据：" + root.Replace('/', '\\');
                return true;
            }
            catch (Exception exception)
            {
                message = "对比捕获失败：" + exception.Message;
                return false;
            }
        }

        private static void CaptureSet(GameObject prefab, string side, string root, JArray manifest)
        {
            float duration = EstimateDuration(prefab);
            for (int viewIndex = 0; viewIndex < ViewIds.Length; viewIndex++)
            {
                for (int timeIndex = 0; timeIndex < Fractions.Length; timeIndex++)
                {
                    float fraction = Fractions[timeIndex];
                    byte[] png = Capture(prefab, duration * fraction, ViewDirections[viewIndex]);
                    string relative = side + "/" + ViewIds[viewIndex] + "/t" + Mathf.RoundToInt(fraction * 100f).ToString("000") + ".png";
                    string output = root + "/" + relative;
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    File.WriteAllBytes(output, png);
                    manifest.Add(new JObject
                    {
                        ["side"] = side,
                        ["viewId"] = ViewIds[viewIndex],
                        ["timeFraction"] = fraction,
                        ["sampleTimeSeconds"] = duration * fraction,
                        ["path"] = output.Replace('\\', '/'),
                        ["contentHash"] = Hash(png)
                    });
                }
            }
        }

        private static byte[] Capture(GameObject prefab, float sampleTime, Vector3 viewDirection)
        {
            var utility = new PreviewRenderUtility();
            Texture2D texture = null;
            try
            {
                utility.BeginStaticPreview(new Rect(0f, 0f, 640f, 640f));
                GameObject instance = utility.InstantiatePrefabInScene(prefab);
                ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particles.Length; i++)
                {
                    particles[i].useAutoRandomSeed = false;
                    particles[i].randomSeed = unchecked(17u + (uint)i * 7919u);
                    particles[i].Simulate(Mathf.Max(0f, sampleTime), false, true, true);
                }

                Bounds bounds = CalculateParticleBounds(instance, particles);
                float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
                utility.camera.transform.position = bounds.center + viewDirection * radius * 3.2f;
                utility.camera.transform.LookAt(bounds.center);
                utility.camera.nearClipPlane = Mathf.Max(0.01f, radius * 0.01f);
                utility.camera.farClipPlane = radius * 12f + 10f;
                utility.camera.clearFlags = CameraClearFlags.SolidColor;
                utility.camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f, 1f);
                utility.cameraFieldOfView = 32f;
                utility.ambientColor = new Color(0.25f, 0.28f, 0.32f);
                utility.Render(true, true);
                texture = utility.EndStaticPreview();
                if (texture == null) throw new InvalidOperationException("PreviewRenderUtility 未返回图像。");
                return texture.EncodeToPNG();
            }
            finally
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                utility.Cleanup();
            }
        }

        private static float EstimateDuration(GameObject prefab)
        {
            float duration = 0.5f;
            foreach (ParticleSystem particle in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particle.main;
                if (!main.loop)
                    duration = Mathf.Max(duration, main.startDelay.constantMax + Mathf.Min(main.duration, 1f) + main.startLifetime.constantMax);
            }
            return Mathf.Min(duration, 4f);
        }

        private static Bounds CalculateParticleBounds(GameObject instance, ParticleSystem[] systems)
        {
            bool initialized = false;
            Bounds result = default;
            for (int systemIndex = 0; systemIndex < systems.Length; systemIndex++)
            {
                ParticleSystem system = systems[systemIndex];
                int count = system.particleCount;
                if (count == 0) continue;
                var particles = new ParticleSystem.Particle[count];
                int actual = system.GetParticles(particles);
                ParticleSystem.MainModule main = system.main;
                for (int i = 0; i < actual; i++)
                {
                    Vector3 position = main.simulationSpace == ParticleSystemSimulationSpace.World ? particles[i].position : system.transform.TransformPoint(particles[i].position);
                    float size = Mathf.Max(0.02f, particles[i].GetCurrentSize(system));
                    Bounds particleBounds = new Bounds(position, Vector3.one * size);
                    if (!initialized) { result = particleBounds; initialized = true; } else result.Encapsulate(particleBounds);
                }
            }
            if (initialized) return result;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 2f);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static string Hash(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            return "sha256:" + BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
