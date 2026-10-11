using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MxFramework.Rendering
{
    internal static class MxHeightFogShaderIds
    {
        internal static readonly int Active = Shader.PropertyToID("_MxPipelineHeightFogEnabled");
    }

    internal sealed class MxHeightFogSceneProvider : IMxRenderPassProvider, IDisposable
    {
        private readonly MxVolumetricFogPassProvider _fog;
        private readonly MaterialFogOverridePass _override = new MaterialFogOverridePass();
        private Camera _camera;
        private bool _disposed;
        internal MxHeightFogSceneProvider(MxVolumetricFogSettings settings, Camera camera, Shader shader)
        { _fog = new MxVolumetricFogPassProvider(settings, shader); _camera = camera; }
        public string DebugName => "SceneHeightFog";
        internal MxVolumetricFogSettings Settings => _fog.Settings;
        internal void SetCamera(Camera camera) => _camera = camera;
        internal void SetSettings(MxVolumetricFogSettings settings) => _fog.SetSettings(settings);
        public void CollectPasses(IMxRenderPassRegistry registry, in MxCameraRenderContextDescriptor context)
        {
            if (_disposed || !Settings.Enabled || _camera == null || !_camera.gameObject.scene.isLoaded) return;
            bool game = context.Camera == _camera &&
                (context.CameraKind == MxCameraRenderKind.Game || context.CameraKind == MxCameraRenderKind.Unknown);
            bool preview = Settings.PreviewInSceneView && context.CameraKind == MxCameraRenderKind.SceneView &&
                UnityEngine.SceneManagement.SceneManager.GetActiveScene() == _camera.gameObject.scene;
            if (!game && !preview) return;
            registry.RegisterPass(_override);
            _fog.CollectPasses(registry, context);
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _fog.Dispose(); }

        private sealed class MaterialFogOverridePass : IMxRenderGraphPass
        {
            public string DebugName => "SceneHeightFog.MaterialOverride";
            public MxRenderPhase Phase => MxRenderPhase.BeforeRenderingOpaques;
            public int Order => -200;
            public bool IsEnabled => true;
            public IReadOnlyList<SharedRenderTextureKey> Reads => Array.Empty<SharedRenderTextureKey>();
            public IReadOnlyList<SharedRenderTextureKey> Writes => Array.Empty<SharedRenderTextureKey>();
            public ScriptableRenderPassInput InputRequirements => ScriptableRenderPassInput.None;
            public void Configure(in MxRenderPassConfigureContext context) { }
            public void Execute(in MxRenderPassExecuteContext context) { }
            public void RecordRenderGraph(RenderGraph graph, ContextContainer frameData, in MxRenderPassConfigureContext context)
            {
                using (var builder = graph.AddUnsafePass<PassData>(DebugName, out var data))
                {
                    builder.AllowGlobalStateModification(true); builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (PassData d, UnsafeGraphContext ctx) =>
                        ctx.cmd.SetGlobalFloat(MxHeightFogShaderIds.Active, 1));
                }
            }
            private sealed class PassData { }
        }
    }
}
