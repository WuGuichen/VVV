using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Renders the complete quality matrix in isolated PreviewRenderUtility scenes.</summary>
    /// <remarks>Editor-only and AllocByDesign. Every capture owns and releases its Preview Scene, Texture2D, and instantiated Prefab.</remarks>
    internal sealed class VfxUnityQualityCaptureBackend : IVfxQualityCaptureBackend
    {
        private VfxQualityCandidateContext _context;
        private GameObject _prefab;
        private VfxQualityProfileTransport _profile;
        private bool _disposed;

        public string ProviderId => "mx.vfx.unity-preview-quality-capture";
        public string ProviderVersion => "1.0.0";

        public void BeginCandidate(VfxQualityCandidateContext context, string prefabAssetPath, VfxQualityProfileTransport profile)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VfxUnityQualityCaptureBackend));
            EndCandidate();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VfxCandidateSandbox.NormalizeAssetPath(prefabAssetPath));
            if (_prefab == null) throw new InvalidOperationException("Quality capture requires an imported candidate Prefab wrapper: " + prefabAssetPath);
        }

        public VfxQualityFrameObservation Capture(VfxQualityCapturePoint point)
        {
            if (_disposed || _prefab == null || _context == null || _profile == null)
                throw new InvalidOperationException("Quality capture candidate has not been initialized.");
            var utility = new PreviewRenderUtility();
            Texture2D texture = null;
            try
            {
                int width = Mathf.Clamp(_profile.Width, 64, 2048);
                int height = Mathf.Clamp(_profile.Height, 64, 2048);
                utility.BeginStaticPreview(new Rect(0f, 0f, width, height));
                GameObject instance = utility.InstantiatePrefabInScene(_prefab);
                Simulate(instance, point);
                Bounds bounds = CalculateBounds(instance);
                ConfigurePreview(utility, point, _context, bounds);
                utility.Render(true, true);
                texture = utility.EndStaticPreview();
                if (texture == null)
                    return NotMeasured(point, "PreviewRenderUtility returned no frame.");
                Color[] pixels = texture.GetPixels();
                PixelMetrics pixelMetrics = AnalyzePixels(pixels, texture.width, texture.height, point.ViewId, _context);
                byte[] png = texture.EncodeToPNG();
                string outputPath = VfxCandidateSandbox.NormalizeAssetPath(point.OutputPath);
                if (!VfxCandidateSandbox.IsControlledEvidencePath(outputPath))
                    return NotMeasured(point, "Capture output escaped the controlled Authoring evidence root.");
                string parent = Path.GetDirectoryName(outputPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                File.WriteAllBytes(outputPath, png);
                MeasureAnchor(instance, bounds, _context, out bool anchorMeasured, out float anchorError);
                MeasurePath(instance, _context, out bool pathMeasured, out float pathError);
                int shaderErrors = CountUnsupportedShaders(instance);
                return new VfxQualityFrameObservation
                {
                    Status = VfxQualityEvidenceProtocol.Measured,
                    Message = "Fixed-step isolated Preview Scene frame captured; this evidence never grants Ready status.",
                    OutputPath = outputPath,
                    ContentHash = VfxCandidateSandbox.ComputeHash(png),
                    MissingFrameCount = png == null || png.Length == 0 ? 1 : 0,
                    MagentaPixelCount = pixelMetrics.MagentaPixels + shaderErrors,
                    NonFinitePixelCount = pixelMetrics.NonFinitePixels,
                    RenderErrorCount = 0,
                    CropPercent = pixelMetrics.CropPercent,
                    AnchorMeasured = anchorMeasured,
                    AnchorErrorPercent = anchorError,
                    PathMeasured = pathMeasured,
                    PathEndpointErrorPercent = pathError,
                    TelegraphMeasured = pixelMetrics.TelegraphMeasured,
                    TelegraphFootprintIou = pixelMetrics.TelegraphIou,
                    AlphaCoverage = pixelMetrics.AlphaCoverage,
                    VisibleRangeMeters = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z))
                };
            }
            catch (Exception exception)
            {
                return NotMeasured(point, exception.Message);
            }
            finally
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                utility.Cleanup();
            }
        }

        public VfxQualityLifecycleObservation MeasureLifecycle(VfxQualityCandidateContext context, VfxQualityProfileTransport profile)
        {
            if (_disposed || _prefab == null) return new VfxQualityLifecycleObservation { Message = "Quality capture candidate has not been initialized." };
            var utility = new PreviewRenderUtility();
            Texture2D cleanupTexture = null;
            try
            {
                int width = Mathf.Clamp(profile.Width, 64, 1024);
                int height = Mathf.Clamp(profile.Height, 64, 1024);
                utility.BeginStaticPreview(new Rect(0f, 0f, width, height));
                GameObject instance = utility.InstantiatePrefabInScene(_prefab);
                ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int cycle = 0; cycle < profile.GhostCycleCount; cycle++)
                {
                    for (int i = 0; i < particles.Length; i++)
                    {
                        particles[i].useAutoRandomSeed = false;
                        particles[i].randomSeed = unchecked((uint)profile.FixedSeeds[cycle % profile.FixedSeeds.Count] + (uint)i * 7919u);
                        particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        particles[i].Clear(true);
                        particles[i].Play(true);
                        particles[i].Simulate(Mathf.Max((float)profile.FixedStepSeconds, context.DurationSeconds), true, true, true);
                        particles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                        particles[i].Simulate(profile.LoopCleanupSeconds, true, false, true);
                    }
                }
                int active = 0;
                for (int i = 0; i < particles.Length; i++) active += particles[i].particleCount;
                Bounds bounds = CalculateBounds(instance);
                var cleanupPoint = new VfxQualityCapturePoint { ViewId = "Gameplay", BackgroundId = "DarkNeutral" };
                ConfigurePreview(utility, cleanupPoint, context, bounds);
                utility.Render(true, true);
                cleanupTexture = utility.EndStaticPreview();
                PixelMetrics metrics = cleanupTexture != null
                    ? AnalyzePixels(cleanupTexture.GetPixels(), cleanupTexture.width, cleanupTexture.height, "Gameplay", context)
                    : default;
                float brightPercent = cleanupTexture != null ? metrics.BrightPixelPercent : 100f;
                int ghostCount = active + (brightPercent > profile.MaximumBrightPixelPercentAfterCleanup ? 1 : 0);
                return new VfxQualityLifecycleObservation
                {
                    Status = VfxQualityEvidenceProtocol.Measured,
                    Message = "Cleanup and repeated Play/Stop evidence measured in an isolated Preview Scene.",
                    ActiveParticlesAfterCleanup = active,
                    BrightPixelPercentAfterCleanup = brightPercent,
                    GhostCyclesExecuted = profile.GhostCycleCount,
                    GhostCount = ghostCount
                };
            }
            catch (Exception exception)
            {
                return new VfxQualityLifecycleObservation { Status = VfxQualityEvidenceProtocol.NotMeasured, Message = exception.Message };
            }
            finally
            {
                if (cleanupTexture != null) UnityEngine.Object.DestroyImmediate(cleanupTexture);
                utility.Cleanup();
            }
        }

        public void EndCandidate()
        {
            _prefab = null;
            _context = null;
            _profile = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            EndCandidate();
            _disposed = true;
        }

        private static void Simulate(GameObject instance, VfxQualityCapturePoint point)
        {
            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                particle.useAutoRandomSeed = false;
                particle.randomSeed = unchecked((uint)point.Seed + (uint)i * 7919u);
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Clear(true);
                if (string.Equals(point.LifecycleAction, "Play", StringComparison.Ordinal))
                    particle.Simulate(Mathf.Max(0f, point.SimulationSeconds), true, true, true);
                else
                {
                    float cleanup = string.Equals(point.LifecycleAction, "Cleanup", StringComparison.Ordinal) ? 1f : 0f;
                    particle.Simulate(Mathf.Max(0f, point.SimulationSeconds - cleanup), true, true, true);
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    if (cleanup > 0f) particle.Simulate(cleanup, true, false, true);
                }
            }
        }

        private static void ConfigurePreview(PreviewRenderUtility utility, VfxQualityCapturePoint point, VfxQualityCandidateContext context, Bounds bounds)
        {
            Vector3 direction;
            switch (point.ViewId)
            {
                case "Side": direction = new Vector3(1f, 0.2f, 0f); break;
                case "Top": direction = new Vector3(0.001f, 1f, 0.001f); break;
                case "Path": direction = new Vector3(0f, 0.25f, -1f); break;
                case "FrontThreeQuarter": direction = new Vector3(0.7f, 0.45f, -1f); break;
                default: direction = new Vector3(0.65f, 0.3f, -1f); break;
            }
            direction.Normalize();
            Vector3 anchor = Vector3.zero;
            float scale = Mathf.Max(0.25f, context.TargetScaleMeters);
            utility.camera.transform.position = anchor + direction * scale * 3.2f;
            utility.camera.transform.LookAt(anchor);
            utility.camera.nearClipPlane = Mathf.Max(0.01f, scale * 0.01f);
            utility.camera.farClipPlane = scale * 12f + 10f;
            utility.camera.clearFlags = CameraClearFlags.SolidColor;
            utility.camera.backgroundColor = Background(point.BackgroundId);
            utility.cameraFieldOfView = 32f;
            utility.ambientColor = new Color(0.3f, 0.32f, 0.36f, 1f);
            if (utility.lights.Length > 0)
            {
                utility.lights[0].intensity = 1.15f;
                utility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            }
        }

        private static Color Background(string id)
        {
            switch (id)
            {
                case "LightNeutral": return new Color(0.72f, 0.74f, 0.77f, 0f);
                case "GameplayBusy": return new Color(0.18f, 0.24f, 0.19f, 0f);
                case "SameColorConflict": return new Color(0.78f, 0.22f, 0.04f, 0f);
                default: return new Color(0.025f, 0.03f, 0.04f, 0f);
            }
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one * 0.01f);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void MeasureAnchor(GameObject instance, Bounds bounds, VfxQualityCandidateContext context, out bool measured, out float errorPercent)
        {
            measured = bounds.size.sqrMagnitude > 0.0000001f && context.TargetScaleMeters > 0f;
            errorPercent = measured ? Vector3.Distance(bounds.center, instance.transform.position) / context.TargetScaleMeters * 100f : 0f;
        }

        private static void MeasurePath(GameObject instance, VfxQualityCandidateContext context, out bool measured, out float errorPercent)
        {
            measured = false;
            errorPercent = 0f;
            if (!context.RequiresPathMeasurement) return;
            LineRenderer[] lines = instance.GetComponentsInChildren<LineRenderer>(true);
            float expectedLength = Vector3.Distance(context.ExpectedPathStart, context.ExpectedPathEnd);
            if (lines.Length == 0 || lines[0].positionCount < 2 || expectedLength <= 0.0001f) return;
            LineRenderer line = lines[0];
            Vector3 start = line.GetPosition(0);
            Vector3 end = line.GetPosition(line.positionCount - 1);
            if (line.useWorldSpace)
            {
                start = instance.transform.InverseTransformPoint(start);
                end = instance.transform.InverseTransformPoint(end);
            }
            float direct = Mathf.Max(Vector3.Distance(start, context.ExpectedPathStart), Vector3.Distance(end, context.ExpectedPathEnd));
            float reversed = Mathf.Max(Vector3.Distance(end, context.ExpectedPathStart), Vector3.Distance(start, context.ExpectedPathEnd));
            errorPercent = Mathf.Min(direct, reversed) / expectedLength * 100f;
            measured = true;
        }

        private static int CountUnsupportedShaders(GameObject instance)
        {
            int count = 0;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Shader shader = materials[materialIndex] != null ? materials[materialIndex].shader : null;
                    if (shader == null || !shader.isSupported || string.Equals(shader.name, "Hidden/InternalErrorShader", StringComparison.Ordinal)) count++;
                }
            }
            return count;
        }

        private static PixelMetrics AnalyzePixels(Color[] pixels, int width, int height, string viewId, VfxQualityCandidateContext context)
        {
            var result = new PixelMetrics { TelegraphIou = 0f };
            int covered = 0, edgeCovered = 0, bright = 0, intersection = 0, union = 0;
            float radiusPixels = Mathf.Clamp01(context.TelegraphRadiusNormalized) * Mathf.Min(width, height);
            float radiusSquared = radiusPixels * radiusPixels;
            float centerX = (width - 1) * 0.5f;
            float centerY = (height - 1) * 0.5f;
            bool measureTelegraph = context.RequiresTelegraphMeasurement && string.Equals(viewId, "Top", StringComparison.Ordinal);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                if (!Finite(pixel.r) || !Finite(pixel.g) || !Finite(pixel.b) || !Finite(pixel.a)) result.NonFinitePixels++;
                bool foreground = pixel.a > 0.01f;
                if (foreground)
                {
                    covered++;
                    int x = i % width;
                    int y = i / width;
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1) edgeCovered++;
                    if (pixel.r > 0.8f && pixel.b > 0.8f && pixel.g < 0.2f) result.MagentaPixels++;
                    float luminance = pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f;
                    if (luminance > 0.02f) bright++;
                }
                if (measureTelegraph)
                {
                    int x = i % width;
                    int y = i / width;
                    float dx = x - centerX;
                    float dy = y - centerY;
                    bool expected = dx * dx + dy * dy <= radiusSquared;
                    if (foreground && expected) intersection++;
                    if (foreground || expected) union++;
                }
            }
            result.AlphaCoverage = pixels.Length > 0 ? covered / (float)pixels.Length : 0f;
            result.CropPercent = covered > 0 ? edgeCovered / (float)covered * 100f : 0f;
            result.BrightPixelPercent = pixels.Length > 0 ? bright / (float)pixels.Length * 100f : 0f;
            result.TelegraphMeasured = measureTelegraph && union > 0;
            result.TelegraphIou = result.TelegraphMeasured ? intersection / (float)union : 0f;
            return result;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static VfxQualityFrameObservation NotMeasured(VfxQualityCapturePoint point, string message)
        {
            return new VfxQualityFrameObservation
            {
                Status = VfxQualityEvidenceProtocol.NotMeasured,
                Message = message ?? string.Empty,
                OutputPath = point.OutputPath,
                MissingFrameCount = 1
            };
        }

        private struct PixelMetrics
        {
            public int MagentaPixels;
            public int NonFinitePixels;
            public float CropPercent;
            public float AlphaCoverage;
            public float BrightPixelPercent;
            public bool TelegraphMeasured;
            public float TelegraphIou;
        }
    }

    /// <summary>Fail-closed default until a versioned target-platform profiler provider is injected.</summary>
    internal sealed class VfxNotMeasuredQualityMeasurementProvider : IVfxQualityMeasurementProvider
    {
        public string ProviderId => "mx.vfx.performance.not-measured";
        public string ProviderVersion => "1.0.0";

        public VfxQualityPerformanceObservation Measure(VfxQualityCandidateContext context, string prefabAssetPath, VfxQualityProfileTransport profile)
        {
            return new VfxQualityPerformanceObservation
            {
                Status = VfxQualityEvidenceProtocol.NotMeasured,
                Message = "No versioned target-platform CPU/GPU/GC measurement provider was supplied; performance gates fail closed.",
                CpuP95Milliseconds = -1f,
                GpuP95Milliseconds = -1f,
                SteadyStateGcBytesPerFrame = -1L
            };
        }

        public void Dispose() { }
    }
}
