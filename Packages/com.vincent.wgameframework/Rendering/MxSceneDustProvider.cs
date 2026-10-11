using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MxFramework.Rendering
{
    internal sealed class MxSceneDustProvider : IMxRenderPassProvider, IMxRenderGraphPass, IDisposable
    {
        private readonly Material _material, _fieldMaterial;
        private MxSceneRenderingConfiguration _configuration;
        private MxVolumetricFogSettings _fog;
        private Vector3 _focus;
        private bool _disposed;
        public string DebugName => "SceneDust";
        public MxRenderPhase Phase => MxRenderPhase.BeforeRenderingPostProcessing;
        public int Order => 50; // Height fog is order 40; dust uses its own depth for attenuation.
        public bool IsEnabled => !_disposed;
        public IReadOnlyList<SharedRenderTextureKey> Reads => Array.Empty<SharedRenderTextureKey>();
        public IReadOnlyList<SharedRenderTextureKey> Writes => Array.Empty<SharedRenderTextureKey>();
        public ScriptableRenderPassInput InputRequirements => ScriptableRenderPassInput.Depth;
        internal int DrawsRecorded { get; private set; }

        internal MxSceneDustProvider(Shader shader)
        {
            _material = CoreUtils.CreateEngineMaterial(shader);
            _fieldMaterial = CoreUtils.CreateEngineMaterial(shader);
            _fieldMaterial.EnableKeyword("MX_DUST_LIGHT_FIELD");
        }

        internal void SetConfiguration(MxSceneRenderingConfiguration configuration, MxVolumetricFogSettings fog)
        { _configuration = configuration; _fog = fog; }

        public void CollectPasses(IMxRenderPassRegistry registry, in MxCameraRenderContextDescriptor context)
        {
            var c = _configuration;
            if (_disposed || c == null || !c.DustEnabled || c.Dust == null || c.Dust.ParticleCount <= 0 ||
                c.TargetCamera == null || !c.TargetCamera.gameObject.scene.isLoaded) return;
            bool game = context.Camera == c.TargetCamera &&
                (context.CameraKind == MxCameraRenderKind.Game || context.CameraKind == MxCameraRenderKind.Unknown);
            bool preview = c.PreviewDustInSceneView && context.CameraKind == MxCameraRenderKind.SceneView &&
                SceneManager.GetActiveScene() == c.TargetCamera.gameObject.scene;
            if (game || preview) registry.RegisterPass(this);
        }

        public void Configure(in MxRenderPassConfigureContext context)
        { _focus = context.CameraSnapshot != null ? context.CameraSnapshot.ViewFocusWorldPosition : context.CameraContext.ViewFocusWorldPosition; }
        public void Execute(in MxRenderPassExecuteContext context) { }

        public void RecordRenderGraph(RenderGraph graph, ContextContainer frameData, in MxRenderPassConfigureContext context)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var camera = frameData.Get<UniversalCameraData>();
            if (!resources.cameraDepthTexture.IsValid() || camera.renderType == CameraRenderType.Overlay) return;
            var field = frameData.GetOrCreate<MxSceneLightFieldData>();
            var c = _configuration;
            var settings = c.Dust;
            bool useField = field.IsValid;
            using (var builder = graph.AddRasterRenderPass<PassData>(DebugName, out var data))
            {
                data.Material = useField ? _fieldMaterial : _material;
                data.Count = Mathf.Clamp(settings.ParticleCount, 0, 8192);
                data.Depth = resources.cameraDepthTexture;
                data.UseField = useField;
                var block = data.Properties;
                block.Clear();
                float radius = Finite(settings.Radius, 1, 200, 18);
                float baseHeight = Finite(settings.BaseHeight, -10000, 10000, 0);
                float height = Finite(settings.Height, 0.1f, 100, 5);
                block.SetVector("_MxDustVolume", new Vector4(radius, baseHeight, height, settings.Seed));
                block.SetVector("_MxDustCenter", CalculateVolumeCenter(camera.camera, radius, baseHeight, height));
                block.SetVector("_MxDustAppearance", new Vector4(Finite(settings.Size, 0.001f, 0.3f, 0.025f),
                    Finite(settings.Opacity, 0, 1, 0.24f), Finite(settings.DriftSpeed, 0, 10, 0.08f), Finite(settings.Anisotropy, -0.85f, 0.85f, 0.4f)));
                Color tint = settings.Color.linear;
                block.SetVector("_MxDustColor", new Vector4(tint.r, tint.g, tint.b, Finite(settings.LightResponse, 0, 20, 2)));
                block.SetVector(MxRenderingShaderIds.MxViewFocusWorldPos, _focus);
                Color ambient = c.Look != null ? (c.Look.Sky.linear + c.Look.Equator.linear + c.Look.Ground.linear) / 3 : RenderSettings.ambientLight.linear;
                block.SetColor("_MxDustAmbient", ambient);

                bool fogEnabled = _fog.Enabled && (camera.cameraType != CameraType.SceneView || _fog.PreviewInSceneView);
                block.SetVector(MxRenderingShaderIds.MxVolumetricFogParams, new Vector4(_fog.Density, _fog.MaxDistance, 0, fogEnabled ? 1 : 0));
                block.SetVector(MxRenderingShaderIds.MxVolumetricFogHeightParams, new Vector4(_fog.BaseHeight, _fog.HeightFalloff, 0, 0));
                block.SetVector(MxRenderingShaderIds.MxVolumetricFogScatteringParams, new Vector4(_fog.Anisotropy, _fog.ScatteringIntensity, 0, 0));
                block.SetVector(MxRenderingShaderIds.MxVolumetricFogFocusParams, new Vector4(_fog.FocusClearRadius, _fog.FocusClearSoftness, 0, 0));
                block.SetColor(MxRenderingShaderIds.MxVolumetricFogColor, _fog.Color);
                if (useField)
                {
                    data.Probes = field.Probes; data.Coefficients = field.Coefficients; data.Visibility = field.Visibility;
                    data.CellBricks=field.CellBricks;data.BrickIndices=field.BrickIndices;data.ClearDistances=field.ClearDistances;
                    data.Sun = field.SunVisibility;
                    builder.UseBuffer(data.Probes, AccessFlags.Read);
                    builder.UseBuffer(data.Coefficients, AccessFlags.Read);
                    builder.UseBuffer(data.Visibility, AccessFlags.Read);
                    builder.UseBuffer(data.CellBricks,AccessFlags.Read);builder.UseBuffer(data.BrickIndices,AccessFlags.Read);builder.UseBuffer(data.ClearDistances,AccessFlags.Read);
                    builder.UseTexture(data.Sun, AccessFlags.Read);
                    block.SetVector("_MxLightingGridOrigin", field.GridOrigin);
                    block.SetVector("_MxLightingGridSize", field.GridSize);
                    block.SetMatrix("_MxDustSunVP", field.SunViewProjection);
                    block.SetMatrix("_MxDustSunView", field.SunView);
                    block.SetVector("_MxDustSunRange", field.SunRange);
                    block.SetVector("_MxDustSunDirection", field.SunDirection);
                    block.SetVector("_MxDustSunColor", field.SunColor);
                    block.SetFloat("_MxDustIndirectStrength", field.IndirectStrength);
                }
                builder.UseTexture(data.Depth, AccessFlags.Read);
                builder.UseAllGlobalTextures(true); // URP main-light shadow atlas for dynamic occluders/fallback.
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (PassData p, RasterGraphContext ctx) =>
                {
                    p.Properties.SetTexture("_CameraDepthTexture", p.Depth);
                    if (p.UseField)
                    {
                        p.Properties.SetBuffer("_MxLightingProbes", p.Probes);
                        p.Properties.SetBuffer("_MxLightingCoefficients", p.Coefficients);
                        p.Properties.SetBuffer("_MxLightingVisibility", p.Visibility);
                        p.Properties.SetBuffer("_MxLightingCellBricks",p.CellBricks);p.Properties.SetBuffer("_MxLightingBrickIndices",p.BrickIndices);p.Properties.SetBuffer("_MxLightingClearDistances",p.ClearDistances);
                        p.Properties.SetTexture("_MxDustSunVisibility", p.Sun);
                    }
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, p.Material, 0, MeshTopology.Triangles, 6, p.Count, p.Properties);
                });
            }
            DrawsRecorded++;
        }

        // A high tactical camera can look at a dust layer far from its own XZ position.
        // Anchor coverage to the visible layer; periodic particle positions remain world-stable.
        internal static Vector3 CalculateVolumeCenter(Camera camera, float radius, float baseHeight, float height)
        {
            float middleHeight = baseHeight + height * 0.5f;
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Vector3 center = camera.transform.position;
            if (Mathf.Abs(ray.direction.y) > 0.0001f)
            {
                float distance = (middleHeight - ray.origin.y) / ray.direction.y;
                if (distance >= 0 && distance <= camera.farClipPlane)
                    center = ray.GetPoint(distance);
            }
            else
            {
                Vector3 forward = new Vector3(ray.direction.x, 0, ray.direction.z);
                center += forward * (radius * 0.5f);
            }
            center.y = middleHeight;
            return center;
        }

        private static float Finite(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CoreUtils.Destroy(_material); CoreUtils.Destroy(_fieldMaterial);
        }

        private sealed class PassData
        {
            internal Material Material;
            internal int Count;
            internal bool UseField;
            internal TextureHandle Depth, Sun;
            internal BufferHandle Probes, Coefficients, Visibility;
            internal BufferHandle CellBricks,BrickIndices,ClearDistances;
            internal readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        }
    }
}
