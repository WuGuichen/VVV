using System;
using UnityEngine;

namespace MxFramework.Rendering
{
    /// <summary>Presentation-only scene bindings. The Unity scene and shared Look remain their own authorities.</summary>
    [Serializable]
    public sealed class MxSceneRenderingConfiguration
    {
        // Distinguishes a deliberately created empty configuration from Unity's default
        // instance for a scene that predates this serialized field.
        public bool AuthoringCreated;
        public MxScenePresentationBinding[] Presentations = Array.Empty<MxScenePresentationBinding>();
        public bool IndirectLightingEnabled;
        public bool DirectionalShadowsEnabled;
        public MxDirectionalShadowData DirectionalShadowData;
        public ComputeShader DirectionalShadowCompute;
        public bool DynamicDirectionalShadows = true;
        [NonSerialized] public Func<Renderer,bool> DynamicShadowCasterFilter;
        [NonSerialized] public Func<Renderer,MxDirectionalShadowPosePublication> ReadDirectionalShadowPosePublication;
        // Optional scene-owned inputs (for example instanced vegetation) that have
        // no Renderer. False suppresses the baked field until rebaked or restored.
        [NonSerialized] public Func<bool> StaticDirectionalShadowCastingStateValid;
        public bool CullDirectionalShadowsToReceivers = true;
        // Scene/editor adapter appends every permitted receiver camera. False or
        // absent in Editor uses all casters; no guessed incomplete camera union.
        [NonSerialized] public Func<System.Collections.Generic.List<UnityEngine.Camera>,bool> CollectDirectionalShadowReceiverCameras;
        public bool PreviewDirectionalShadowsInSceneView = true;
        [Min(128)] public int DynamicShadowResolution = 1024;
        /// <summary>Session-only player setting. Zero preserves authored resolution; never changes baked fields.</summary>
        [NonSerialized] public int DynamicShadowResolutionOverride;
        public int EffectiveDynamicShadowResolution => DynamicShadowResolutionOverride > 0
            ? DynamicShadowResolutionOverride : DynamicShadowResolution;
        // Scene-local static detail controls. Zero resolution retains the legacy
        // budget split; changing either input requires an explicit shadow bake.
        [Range(.1f,1f)] public float DirectionalShadowCenterScale = 1f;
        [Min(0)] public int DirectionalShadowCenterResolution;
        [Min(0)] public float DirectionalShadowBias = 0.015f;
        // Zero keeps the minimum 2x2 bilinear PCF kernel; positive world radii can widen it.
        [Min(0)] public float DirectionalShadowFilterRadius = 0f;
        public bool HeightFogEnabled;
        public bool DustEnabled;
        public Shader DustShader;
        public bool PreviewDustInSceneView = true;
        public MxSceneDustSettings Dust = new MxSceneDustSettings();
        public Transform StaticRoot;
        // Optional authoring subset. Null/empty keeps the original whole-root behavior.
        public MeshRenderer[] StaticSourceRenderers;
        public const float DefaultProbeSpacing = 4f;
        // Offline authoring input. Changing it requires an explicit transport rebuild.
        [Min(1)] public float ProbeSpacing = DefaultProbeSpacing;
        // World-space sampling coverage is independent of the captured static geometry.
        // Disabled preserves authored scenes and the existing transport lattice.
        public bool UseProbeCoverageBounds;
        public Bounds ProbeCoverageBounds = new Bounds(Vector3.zero, new Vector3(80, 24, 80));
        public MxSceneLightingProbeQuality ProbeQuality = new MxSceneLightingProbeQuality();
        public Light Sun;
        public Camera TargetCamera;
        public MxRenderingPipelineFeature Feature;
        public MxSceneLightingData Data;
        public MxSceneLightingLook Look;
        public MxSceneLightingSourceLayout SourceLayout = new MxSceneLightingSourceLayout();
        public ComputeShader Compute;
        public Shader CaptureShader;
        public Shader ResolveShader;
        public Shader FogCompositeShader;
        public bool PreviewIndirectInSceneView = true;
        public bool PreviewFogInSceneView = true;
        public bool RequireMotionVectors;
        public Color FogColor = new Color(0.60f, 0.66f, 0.72f, 1f);
        [Range(0, 5)] public float FogDensity = 0.18f;
        public float FogBaseHeight;
        [Range(0, 8)] public float FogHeightFalloff = 0.12f;
        [Min(1)] public float FogMaxDistance = 180f;
        [Range(0, 8)] public float FogScatteringIntensity = 1f;
        [Min(0)] public float FogFocusClearRadius = 3.5f;
        [Min(0.001f)] public float FogFocusClearSoftness = 8f;
    }

    /// <summary>Actual presentation pose identity. It is not an animation time or a gameplay revision.</summary>
    public readonly struct MxDirectionalShadowPosePublication
    {
        public MxDirectionalShadowPosePublication(long version, int frame, bool deformationVersionKnown)
        { Version = version; Frame = frame; DeformationVersionKnown = deformationVersionKnown; Known = version > 0; }
        public bool Known { get; }
        public long Version { get; }
        public int Frame { get; }
        public bool DeformationVersionKnown { get; }
    }
}
