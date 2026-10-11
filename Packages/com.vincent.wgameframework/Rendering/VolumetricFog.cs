using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MxFramework.Rendering
{
    public enum MxVolumetricFogQuality
    {
        Disabled = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public readonly struct MxVolumetricFogSettings : IEquatable<MxVolumetricFogSettings>
    {
        public MxVolumetricFogSettings(
            bool enabled,
            MxVolumetricFogQuality quality,
            Color color,
            float density,
            float baseHeight,
            float heightFalloff,
            float maxDistance,
            float anisotropy,
            float scatteringIntensity,
            float noiseStrength,
            float noiseScale,
            int qualitySampleBudget,
            int diagnosticDownsample,
            float temporalStabilityIntent,
            float focusClearRadius = 2.5f,
            float focusClearSoftness = 3f,
            bool previewInSceneView = false)
        {
            bool isEnabled = enabled && quality != MxVolumetricFogQuality.Disabled;
            Enabled = isEnabled;
            Quality = isEnabled ? NormalizeQuality(quality) : MxVolumetricFogQuality.Disabled;
            Color = color;
            Density = Clamp(density, 0f, 5f);
            BaseHeight = baseHeight;
            HeightFalloff = Clamp(heightFalloff, 0f, 8f);
            MaxDistance = Clamp(maxDistance, 1f, 5000f);
            Anisotropy = Clamp(anisotropy, -0.95f, 0.95f);
            ScatteringIntensity = Clamp(scatteringIntensity, 0f, 8f);
            NoiseStrength = Clamp(noiseStrength, 0f, 1f);
            NoiseScale = Clamp(noiseScale, 0.001f, 512f);
            QualitySampleBudget = Clamp(qualitySampleBudget, 1, 64);
            DiagnosticDownsample = Clamp(diagnosticDownsample, 1, 4);
            TemporalStabilityIntent = Clamp(temporalStabilityIntent, 0f, 0.98f);
            FocusClearRadius = Clamp(focusClearRadius, 0f, 100f);
            FocusClearSoftness = Clamp(focusClearSoftness, 0.001f, 100f);
            PreviewInSceneView = previewInSceneView;
        }

        public bool Enabled { get; }
        public MxVolumetricFogQuality Quality { get; }
        public Color Color { get; }
        public float Density { get; }
        public float BaseHeight { get; }
        public float HeightFalloff { get; }
        public float MaxDistance { get; }
        public float Anisotropy { get; }
        public float ScatteringIntensity { get; }
        public float NoiseStrength { get; }
        public float NoiseScale { get; }
        public int QualitySampleBudget { get; }
        public int DiagnosticDownsample { get; }
        public float TemporalStabilityIntent { get; }
        public float FocusClearRadius { get; }
        public float FocusClearSoftness { get; }
        public bool PreviewInSceneView { get; }
        public bool HasTemporalStabilityIntent => Enabled && TemporalStabilityIntent > 0.001f;

        public static MxVolumetricFogSettings Disabled()
        {
            return new MxVolumetricFogSettings(
                false,
                MxVolumetricFogQuality.Disabled,
                Color.clear,
                0f,
                0f,
                0f,
                1f,
                0f,
                0f,
                0f,
                1f,
                1,
                1,
                0f);
        }

        public static MxVolumetricFogSettings Default()
        {
            return ForQuality(MxVolumetricFogQuality.Medium);
        }

        public static MxVolumetricFogSettings ForQuality(MxVolumetricFogQuality quality)
        {
            switch (NormalizeQuality(quality))
            {
                case MxVolumetricFogQuality.Low:
                    return new MxVolumetricFogSettings(true, MxVolumetricFogQuality.Low, new Color(0.56f, 0.64f, 0.72f, 1f), 0.18f, 0f, 0.22f, 72f, 0.18f, 0.58f, 0.22f, 58f, 6, 4, 0f, 3f, 4f);
                case MxVolumetricFogQuality.High:
                    return new MxVolumetricFogSettings(true, MxVolumetricFogQuality.High, new Color(0.62f, 0.70f, 0.78f, 1f), 0.24f, 0f, 0.18f, 150f, 0.42f, 0.82f, 0.3f, 92f, 20, 2, 0.86f, 4f, 6f);
                case MxVolumetricFogQuality.Medium:
                default:
                    return new MxVolumetricFogSettings(true, MxVolumetricFogQuality.Medium, new Color(0.60f, 0.68f, 0.76f, 1f), 0.2f, 0f, 0.2f, 110f, 0.3f, 0.72f, 0.25f, 72f, 12, 2, 0.72f, 3.5f, 5f);
            }
        }

        public MxVolumetricFogSettings WithQuality(MxVolumetricFogQuality quality)
        {
            MxVolumetricFogSettings preset = ForQuality(quality);
            return new MxVolumetricFogSettings(
                preset.Enabled,
                preset.Quality,
                Color,
                Density,
                BaseHeight,
                HeightFalloff,
                MaxDistance,
                Anisotropy,
                ScatteringIntensity,
                NoiseStrength,
                NoiseScale,
                preset.QualitySampleBudget,
                preset.DiagnosticDownsample,
                preset.TemporalStabilityIntent,
                preset.FocusClearRadius,
                preset.FocusClearSoftness,
                PreviewInSceneView);
        }

        public MxVolumetricFogSettings WithEnabled(bool enabled)
        {
            return new MxVolumetricFogSettings(
                enabled,
                enabled ? NormalizeQuality(Quality) : MxVolumetricFogQuality.Disabled,
                Color,
                Density,
                BaseHeight,
                HeightFalloff,
                MaxDistance,
                Anisotropy,
                ScatteringIntensity,
                NoiseStrength,
                NoiseScale,
                QualitySampleBudget,
                DiagnosticDownsample,
                TemporalStabilityIntent,
                FocusClearRadius,
                FocusClearSoftness,
                PreviewInSceneView);
        }

        public MxVolumetricFogSettings WithSceneViewPreview(bool previewInSceneView)
        {
            return new MxVolumetricFogSettings(
                Enabled,
                Quality,
                Color,
                Density,
                BaseHeight,
                HeightFalloff,
                MaxDistance,
                Anisotropy,
                ScatteringIntensity,
                NoiseStrength,
                NoiseScale,
                QualitySampleBudget,
                DiagnosticDownsample,
                TemporalStabilityIntent,
                FocusClearRadius,
                FocusClearSoftness,
                previewInSceneView);
        }

        public bool Equals(MxVolumetricFogSettings other)
        {
            return Enabled == other.Enabled &&
                Quality == other.Quality &&
                Color.Equals(other.Color) &&
                Density.Equals(other.Density) &&
                BaseHeight.Equals(other.BaseHeight) &&
                HeightFalloff.Equals(other.HeightFalloff) &&
                MaxDistance.Equals(other.MaxDistance) &&
                Anisotropy.Equals(other.Anisotropy) &&
                ScatteringIntensity.Equals(other.ScatteringIntensity) &&
                NoiseStrength.Equals(other.NoiseStrength) &&
                NoiseScale.Equals(other.NoiseScale) &&
                QualitySampleBudget == other.QualitySampleBudget &&
                DiagnosticDownsample == other.DiagnosticDownsample &&
                TemporalStabilityIntent.Equals(other.TemporalStabilityIntent) &&
                FocusClearRadius.Equals(other.FocusClearRadius) &&
                FocusClearSoftness.Equals(other.FocusClearSoftness) &&
                PreviewInSceneView == other.PreviewInSceneView;
        }

        public override bool Equals(object obj)
        {
            return obj is MxVolumetricFogSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Enabled ? 1 : 0;
                hash = (hash * 397) ^ (int)Quality;
                hash = (hash * 397) ^ Color.GetHashCode();
                hash = (hash * 397) ^ Density.GetHashCode();
                hash = (hash * 397) ^ BaseHeight.GetHashCode();
                hash = (hash * 397) ^ HeightFalloff.GetHashCode();
                hash = (hash * 397) ^ MaxDistance.GetHashCode();
                hash = (hash * 397) ^ Anisotropy.GetHashCode();
                hash = (hash * 397) ^ ScatteringIntensity.GetHashCode();
                hash = (hash * 397) ^ NoiseStrength.GetHashCode();
                hash = (hash * 397) ^ NoiseScale.GetHashCode();
                hash = (hash * 397) ^ QualitySampleBudget;
                hash = (hash * 397) ^ DiagnosticDownsample;
                hash = (hash * 397) ^ TemporalStabilityIntent.GetHashCode();
                hash = (hash * 397) ^ FocusClearRadius.GetHashCode();
                hash = (hash * 397) ^ FocusClearSoftness.GetHashCode();
                hash = (hash * 397) ^ (PreviewInSceneView ? 1 : 0);
                return hash;
            }
        }

        private static MxVolumetricFogQuality NormalizeQuality(MxVolumetricFogQuality quality)
        {
            if (quality == MxVolumetricFogQuality.Low || quality == MxVolumetricFogQuality.Medium || quality == MxVolumetricFogQuality.High)
                return quality;
            return MxVolumetricFogQuality.Medium;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }
    }

    public readonly struct VolumetricFogDiagnosticsSnapshot
    {
        public VolumetricFogDiagnosticsSnapshot(
            bool enabled,
            MxVolumetricFogQuality quality,
            MxCameraRenderKind lastCameraKind,
            SharedRTSize accumulationSize,
            int diagnosticDownsample,
            int qualitySampleBudget,
            float temporalStabilityIntent,
            bool temporalStabilityPlanned,
            float focusClearRadius,
            float focusClearSoftness,
            bool previewInSceneView,
            long estimatedMemoryBytes,
            int executedFrameCount,
            int skippedFrameCount,
            string lastSkipReason,
            bool materialAvailable)
        {
            Enabled = enabled;
            Quality = quality;
            LastCameraKind = lastCameraKind;
            AccumulationSize = accumulationSize;
            DiagnosticDownsample = diagnosticDownsample;
            QualitySampleBudget = qualitySampleBudget;
            TemporalStabilityIntent = temporalStabilityIntent;
            TemporalStabilityPlanned = temporalStabilityPlanned;
            FocusClearRadius = focusClearRadius;
            FocusClearSoftness = focusClearSoftness;
            PreviewInSceneView = previewInSceneView;
            EstimatedMemoryBytes = estimatedMemoryBytes;
            ExecutedFrameCount = executedFrameCount;
            SkippedFrameCount = skippedFrameCount;
            LastSkipReason = lastSkipReason ?? string.Empty;
            MaterialAvailable = materialAvailable;
        }

        public bool Enabled { get; }
        public MxVolumetricFogQuality Quality { get; }
        public MxCameraRenderKind LastCameraKind { get; }
        /// <summary>Hypothetical accumulation dimensions for diagnostics; v1 does not allocate this texture.</summary>
        public SharedRTSize AccumulationSize { get; }
        public int DiagnosticDownsample { get; }
        public int QualitySampleBudget { get; }
        public float TemporalStabilityIntent { get; }
        public bool TemporalStabilityPlanned { get; }
        public float FocusClearRadius { get; }
        public float FocusClearSoftness { get; }
        public bool PreviewInSceneView { get; }
        /// <summary>Hypothetical ARGBHalf accumulation budget, not allocated GPU memory.</summary>
        public long EstimatedMemoryBytes { get; }
        public int ExecutedFrameCount { get; }
        public int SkippedFrameCount { get; }
        public string LastSkipReason { get; }
        public bool MaterialAvailable { get; }
    }

    public sealed class MxVolumetricFogPassProvider : IMxRenderPassProvider, IDisposable
    {
        private readonly MxVolumetricFogPass _pass;
        private bool _disposed;

        public MxVolumetricFogPassProvider()
            : this(MxVolumetricFogSettings.Default())
        {
        }

        public MxVolumetricFogPassProvider(MxVolumetricFogSettings settings)
            : this(settings, null)
        {
        }

        public MxVolumetricFogPassProvider(MxVolumetricFogSettings settings, Shader compositeShader)
        {
            _pass = new MxVolumetricFogPass(settings, compositeShader);
        }

        public string DebugName => "MxVolumetricFogPassProvider";
        public MxVolumetricFogSettings Settings => _pass.Settings;

        public void SetSettings(MxVolumetricFogSettings settings)
        {
            if (_disposed)
                return;

            _pass.SetSettings(settings);
        }

        public VolumetricFogDiagnosticsSnapshot CaptureDiagnostics()
        {
            return _pass.CaptureDiagnostics();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _pass.Dispose();
        }

        public void CollectPasses(IMxRenderPassRegistry registry, in MxCameraRenderContextDescriptor cameraContext)
        {
            if (_disposed)
            {
                _pass.RecordSkip(cameraContext.CameraKind, "disposed");
                return;
            }

            if (!Settings.Enabled)
            {
                _pass.RecordSkip(cameraContext.CameraKind, "disabled");
                return;
            }

            if (cameraContext.CameraKind != MxCameraRenderKind.Game &&
                cameraContext.CameraKind != MxCameraRenderKind.Unknown &&
                (cameraContext.CameraKind != MxCameraRenderKind.SceneView || !Settings.PreviewInSceneView))
            {
                _pass.RecordSkip(cameraContext.CameraKind, "camera kind not targeted");
                return;
            }

            registry.RegisterPass(_pass);
        }
    }

    internal sealed class MxVolumetricFogPass : IMxRenderPass, IMxFullscreenRenderPass
    {
        public const string ShaderName = "Hidden/MxFramework/Rendering/VolumetricFogComposite";
        public static readonly SharedRTId AccumulationId = new SharedRTId("mxframework.rendering.volumetric-fog.accumulation");
        public static readonly SharedRTId ReservedTemporalHistoryId = new SharedRTId("mxframework.rendering.volumetric-fog.history");

        private static readonly SharedRenderTextureKey[] EmptyKeys = Array.Empty<SharedRenderTextureKey>();

        private Material _material;
        private readonly Shader _compositeShader;
        private MxVolumetricFogSettings _settings;
        private SharedRTSize _accumulationSize = new SharedRTSize(1, 1);
        private MxCameraRenderKind _lastCameraKind = MxCameraRenderKind.Unknown;
        private int _executedFrameCount;
        private int _skippedFrameCount;
        private string _lastSkipReason = string.Empty;
        private bool _disposed;

        public MxVolumetricFogPass(MxVolumetricFogSettings settings, Shader compositeShader = null)
        {
            _compositeShader = compositeShader;
            SetSettings(settings);
        }

        public string DebugName => "MxVolumetricFog.Composite";
        public MxRenderPhase Phase => MxRenderPhase.BeforeRenderingPostProcessing;
        public int Order => 40;
        public bool IsEnabled => !_disposed && _settings.Enabled;
        public IReadOnlyList<SharedRenderTextureKey> Reads => EmptyKeys;
        public IReadOnlyList<SharedRenderTextureKey> Writes => EmptyKeys;
        public ScriptableRenderPassInput InputRequirements => ScriptableRenderPassInput.Depth;
        public bool FetchActiveColor => true;
        public bool BindDepthStencilAttachment => false;
        public MxVolumetricFogSettings Settings => _settings;

        public void SetSettings(MxVolumetricFogSettings settings)
        {
            if (_disposed)
                return;

            _settings = settings;
        }

        public void RecordSkip(MxCameraRenderKind cameraKind, string reason)
        {
            _lastCameraKind = cameraKind;
            _lastSkipReason = reason ?? string.Empty;
            _skippedFrameCount++;
        }

        public VolumetricFogDiagnosticsSnapshot CaptureDiagnostics()
        {
            return new VolumetricFogDiagnosticsSnapshot(
                !_disposed && _settings.Enabled,
                _disposed ? MxVolumetricFogQuality.Disabled : _settings.Quality,
                _lastCameraKind,
                _accumulationSize,
                _settings.DiagnosticDownsample,
                _settings.QualitySampleBudget,
                _settings.TemporalStabilityIntent,
                _settings.HasTemporalStabilityIntent,
                _settings.FocusClearRadius,
                _settings.FocusClearSoftness,
                _settings.PreviewInSceneView,
                EstimateMemoryBytes(_accumulationSize),
                _executedFrameCount,
                _skippedFrameCount,
                _lastSkipReason,
                !_disposed && _material != null);
        }

        public void Configure(in MxRenderPassConfigureContext context)
        {
            if (_disposed)
                return;

            _lastCameraKind = context.CameraContext.CameraKind;
            Camera camera = context.CameraContext.Camera;
            int pixelWidth = camera != null ? camera.pixelWidth : 1280;
            int pixelHeight = camera != null ? camera.pixelHeight : 720;
            int width = Math.Max(1, pixelWidth / Math.Max(1, _settings.DiagnosticDownsample));
            int height = Math.Max(1, pixelHeight / Math.Max(1, _settings.DiagnosticDownsample));
            // v1 composites camera color/depth directly. Retain the hypothetical accumulation budget
            // without allocating unconsumed storage or advertising fictitious SharedRT readers/writers.
            _accumulationSize = new SharedRTSize(width, height);
        }

        /// <remarks>NoAlloc on the steady-state execution path; invalid inputs and failure diagnostics may allocate.</remarks>
        public void Execute(in MxRenderPassExecuteContext context)
        {
            if (_disposed)
            {
                RecordSkip(context.CameraContext.CameraKind, "disposed");
                return;
            }

            Material material = TryEnsureMaterial();
            if (material == null)
            {
                RecordSkip(context.CameraContext.CameraKind, "shader missing");
                return;
            }

            _lastCameraKind = context.CameraContext.CameraKind;
            Vector3 focus = context.CameraSnapshot != null ? context.CameraSnapshot.ViewFocusWorldPosition : context.CameraContext.ViewFocusWorldPosition;
            material.SetColor(MxRenderingShaderIds.MxVolumetricFogColor, _settings.Color);
            material.SetVector(
                MxRenderingShaderIds.MxVolumetricFogParams,
                new Vector4(_settings.Enabled ? _settings.Density : 0f, _settings.MaxDistance, _settings.NoiseStrength, _settings.Enabled ? 1f : 0f));
            material.SetVector(
                MxRenderingShaderIds.MxVolumetricFogHeightParams,
                new Vector4(_settings.BaseHeight, _settings.HeightFalloff, _settings.DiagnosticDownsample, _settings.QualitySampleBudget));
            material.SetVector(
                MxRenderingShaderIds.MxVolumetricFogScatteringParams,
                new Vector4(_settings.Anisotropy, _settings.ScatteringIntensity, _settings.NoiseScale, Time.realtimeSinceStartup));
            material.SetVector(
                MxRenderingShaderIds.MxVolumetricFogFocusParams,
                new Vector4(_settings.FocusClearRadius, _settings.FocusClearSoftness, 0f, 0f));
            material.SetVector(MxRenderingShaderIds.MxViewFocusWorldPos, new Vector4(focus.x, focus.y, focus.z, 1f));
            _executedFrameCount++;
            _lastSkipReason = string.Empty;
        }

        public bool TryGetFullscreenMaterial(out Material material, out int passIndex)
        {
            if (_disposed)
            {
                material = null;
                passIndex = 0;
                return false;
            }

            material = TryEnsureMaterial();
            passIndex = 0;
            return material != null && _settings.Enabled;
        }

        private Material TryEnsureMaterial()
        {
            if (_disposed)
                return null;

            if (_material != null)
                return _material;

            Shader shader = _compositeShader != null ? _compositeShader : Shader.Find(ShaderName);
            if (shader == null)
                return null;

            _material = new Material(shader)
            {
                name = "MxVolumetricFogComposite_RuntimeMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            return _material;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_material == null)
                return;

            Material material = _material;
            _material = null;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(material);
            else
                UnityEngine.Object.DestroyImmediate(material);
        }

        private static long EstimateMemoryBytes(SharedRTSize size)
        {
            return (long)size.Width * size.Height * 8L;
        }
    }

    public sealed class VolumetricFogDebugSource : IRenderingDebugSource
    {
        private readonly MxVolumetricFogPassProvider _provider;

        public VolumetricFogDebugSource(MxVolumetricFogPassProvider provider, string name = "Rendering")
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Name = string.IsNullOrWhiteSpace(name) ? "Rendering" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => true;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[] { new FrameworkDebugSection(RenderingDebugSectionNames.VolumetricFog, Format(_provider.CaptureDiagnostics())) });
        }

        private static string Format(VolumetricFogDiagnosticsSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append("enabled: ").Append(snapshot.Enabled).Append('\n');
            builder.Append("quality: ").Append(snapshot.Quality).Append('\n');
            builder.Append("lastCameraKind: ").Append(snapshot.LastCameraKind).Append('\n');
            builder.Append("diagnosticDownsample: ").Append(snapshot.DiagnosticDownsample).Append('\n');
            builder.Append("qualitySampleBudget: ").Append(snapshot.QualitySampleBudget).Append('\n');
            builder.Append("temporalStabilityIntent: ").Append(snapshot.TemporalStabilityIntent.ToString("0.00")).Append('\n');
            builder.Append("temporalStabilityPlanned: ").Append(snapshot.TemporalStabilityPlanned).Append('\n');
            builder.Append("accumulationSize: ").Append(snapshot.AccumulationSize).Append('\n');
            builder.Append("accumulationAllocation: none (v1 diagnostic budget only)").Append('\n');
            builder.Append("focusClearRadius: ").Append(snapshot.FocusClearRadius.ToString("0.00")).Append('\n');
            builder.Append("focusClearSoftness: ").Append(snapshot.FocusClearSoftness.ToString("0.00")).Append('\n');
            builder.Append("previewInSceneView: ").Append(snapshot.PreviewInSceneView).Append('\n');
            builder.Append("estimatedMemoryBytes: ").Append(snapshot.EstimatedMemoryBytes).Append('\n');
            builder.Append("executedFrameCount: ").Append(snapshot.ExecutedFrameCount).Append('\n');
            builder.Append("skippedFrameCount: ").Append(snapshot.SkippedFrameCount).Append('\n');
            builder.Append("materialAvailable: ").Append(snapshot.MaterialAvailable).Append('\n');
            builder.Append("lastSkipReason: ").Append(snapshot.LastSkipReason);
            return builder.ToString();
        }
    }
}
