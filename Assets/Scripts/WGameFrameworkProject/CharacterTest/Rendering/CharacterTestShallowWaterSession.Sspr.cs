using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestShallowWaterSession
    {
        public int SsprRenders { get; private set; }
        public Vector2Int SsprResolution { get; private set; }
        public string SsprIssue { get; private set; } = string.Empty;

        // Preserve 18/14 packing through 512 squared; high-resolution sources require 20/12.
        internal static Vector2Int SsprSize(int width, int height, float scale, int maximumEdge)
        {
            float factor = Mathf.Min(Mathf.Clamp(scale, .1f, 1), Mathf.Clamp(maximumEdge, 64, 1024) / (float)Mathf.Max(1, Mathf.Max(width, height)));
            return new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(width * factor), 1, 1024),
                Mathf.Clamp(Mathf.RoundToInt(height * factor), 1, 1024));
        }
        internal static int SsprSourceIndexBits(int width, int height) => (long)width * height > 262144 ? 20 : 18;
        internal static uint SsprProjectionKey(float depth01, uint sourceIndex, int sourceBits = 18)
        {
            sourceBits = sourceBits == 20 ? 20 : 18;
            uint levels = (1u << (32 - sourceBits)) - 2;
            return ((uint)Mathf.Min(levels, Mathf.FloorToInt(Mathf.Clamp01(depth01) * levels)) << sourceBits)
                | (sourceIndex & ((1u << sourceBits) - 1));
        }

        private sealed partial class SurfacePass
        {
            private ComputeShader _ssprCompute;
            private int _ssprClear, _ssprProject, _ssprResolveSoft;
            private bool _ssprSupported;
            private static readonly int SsprSizeId = Shader.PropertyToID("_SsprSize");
            private static readonly int SsprWaterId = Shader.PropertyToID("_SsprWater");
            private static readonly int SsprBoundsId = Shader.PropertyToID("_SsprBounds");
            private static readonly int SsprViewId = Shader.PropertyToID("_SsprView");
            private static readonly int SsprVpId = Shader.PropertyToID("_SsprVP");
            private static readonly int SsprInvVpId = Shader.PropertyToID("_SsprInvVP");
            private static readonly int SsprLocalId = Shader.PropertyToID("_SsprWorldToWater");
            private static readonly int SsprDepthId = Shader.PropertyToID("_SsprDepth");
            private static readonly int SsprColorId = Shader.PropertyToID("_SsprColor");
            private static readonly int SsprWriteId = Shader.PropertyToID("_SsprKeyWrite");
            private static readonly int SsprReadId = Shader.PropertyToID("_SsprKeyRead");
            private static readonly int SsprOutputId = Shader.PropertyToID("_SsprOutput");
            private static readonly int SsprIndexBitsId = Shader.PropertyToID("_SsprIndexBits");
            private static readonly int SsprFilterStrengthId = Shader.PropertyToID("_SsprFilterStrength");
            private bool WantsSspr => _owner.Settings.UseSspr && _owner.Configuration.ReflectionEnabled
                && _owner.Settings.ReflectionStrength > 0 && _owner.Settings.SsprCompute != null;

            private bool SupportsSspr()
            {
                var shader = _owner.Settings.SsprCompute;
                if (shader == _ssprCompute) return _ssprSupported;
                _ssprCompute = shader;
                _ssprSupported = shader != null && SystemInfo.supportsComputeShaders
                    && SystemInfo.IsFormatSupported(GraphicsFormat.R32_UInt, GraphicsFormatUsage.LoadStore)
                    && SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.LoadStore)
                    && SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Sample)
                    && shader.HasKernel("Clear") && shader.HasKernel("Project") && shader.HasKernel("ResolveSoft");
                if (_ssprSupported)
                {
                    _ssprClear = shader.FindKernel("Clear"); _ssprProject = shader.FindKernel("Project"); _ssprResolveSoft = shader.FindKernel("ResolveSoft");
                    // A named kernel can still be unavailable on the active graphics backend.
                    _ssprSupported = shader.IsSupported(_ssprClear) && shader.IsSupported(_ssprProject) && shader.IsSupported(_ssprResolveSoft);
                }
                return _ssprSupported;
            }

            private TextureHandle RecordSspr(RenderGraph graph, ContextContainer frameData, UniversalResourceData resources)
            {
                _owner.SsprResolution = Vector2Int.zero;
                _owner.SsprIssue = string.Empty;
                if (!_owner.Settings.UseSspr || !_owner.Configuration.ReflectionEnabled || _owner.Settings.ReflectionStrength <= 0)
                    return TextureHandle.nullHandle;
                var camera = frameData.Get<UniversalCameraData>();
                if (!SupportsSspr() || camera.camera.stereoEnabled
                    || camera.cameraTargetDescriptor.dimension != TextureDimension.Tex2D || !resources.cameraOpaqueTexture.IsValid())
                {
                    _owner.SsprIssue = "SSPR unavailable (compute/kernel/format, shader binding, stereo or opaque color); using environment reflection.";
                    return TextureHandle.nullHandle;
                }
                var size = SsprSize(camera.cameraTargetDescriptor.width, camera.cameraTargetDescriptor.height,
                    _owner.Settings.EffectiveSsprScale, _owner.Settings.EffectiveSsprEdge);
                _owner.SsprResolution = size;
                var keys = graph.CreateTexture(new TextureDesc(size.x, size.y)
                { name = Name + ".SSPR.Keys", colorFormat = GraphicsFormat.R32_UInt, enableRandomWrite = true, clearBuffer = false, filterMode = FilterMode.Point });
                var output = graph.CreateTexture(new TextureDesc(size.x, size.y)
                { name = Name + ".SSPR.Color", colorFormat = GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite = true, clearBuffer = false, filterMode = FilterMode.Bilinear });
                Matrix4x4 view = camera.GetViewMatrix();
                Matrix4x4 vp = GL.GetGPUProjectionMatrix(camera.GetProjectionMatrix(), true) * view;
                for (int stage = 0; stage < 3; stage++)
                {
                    string name = stage == 0 ? "ShallowWater.SSPR.Clear" : stage == 1 ? "ShallowWater.SSPR.Project" : "ShallowWater.SSPR.Resolve";
                    using (var builder = graph.AddComputePass<SsprData>(name, out var d))
                    {
                        d.Owner = _owner; d.Shader = _ssprCompute; d.Stage = stage;
                        d.Kernel = stage == 0 ? _ssprClear : stage == 1 ? _ssprProject : _ssprResolveSoft;
                        d.IndexBits = SsprSourceIndexBits(size.x, size.y); d.FilterStrength = _owner.Settings.EffectiveSsprFilterStrength;
                        d.Size = new Vector4(size.x, size.y, 1f / size.x, 1f / size.y);
                        d.Water = new Vector4(_owner.Position.y, Mathf.Min(camera.camera.farClipPlane, _owner.Settings.EffectiveReflectionDistance),
                            _owner.Settings.MaximumWaterDepth, _owner.Settings.EffectiveSsprHoleRadius);
                        d.Bounds = new Vector4(_owner.Settings.Size.x * .5f, _owner.Settings.Size.y * .5f, 0, 0);
                        d.View = view; d.Vp = vp; d.InvVp = vp.inverse; d.WorldToWater = _owner.LocalToWorld.inverse;
                        d.Keys = keys; d.Output = output; d.Depth = resources.cameraDepthTexture; d.Color = resources.cameraOpaqueTexture;
                        builder.UseTexture(keys, stage == 0 ? AccessFlags.Write : stage == 1 ? AccessFlags.ReadWrite : AccessFlags.Read);
                        if (stage > 0) builder.UseTexture(d.Depth, AccessFlags.Read);
                        if (stage == 2) { builder.UseTexture(d.Color, AccessFlags.Read); builder.UseTexture(output, AccessFlags.Write); }
                        builder.SetRenderFunc(static (SsprData p, ComputeGraphContext ctx) =>
                        {
                            var cmd = ctx.cmd; var cs = p.Shader; int k = p.Kernel;
                            cmd.SetComputeVectorParam(cs, SsprSizeId, p.Size);
                            cmd.SetComputeIntParam(cs, SsprIndexBitsId, p.IndexBits);
                            cmd.SetComputeFloatParam(cs, SsprFilterStrengthId, p.FilterStrength);
                            cmd.SetComputeVectorParam(cs, SsprWaterId, p.Water);
                            cmd.SetComputeVectorParam(cs, SsprBoundsId, p.Bounds);
                            cmd.SetComputeMatrixParam(cs, SsprViewId, p.View);
                            cmd.SetComputeMatrixParam(cs, SsprVpId, p.Vp);
                            cmd.SetComputeMatrixParam(cs, SsprInvVpId, p.InvVp);
                            cmd.SetComputeMatrixParam(cs, SsprLocalId, p.WorldToWater);
                            cmd.SetComputeTextureParam(cs, k, p.Stage == 2 ? SsprReadId : SsprWriteId, p.Keys);
                            if (p.Stage > 0) cmd.SetComputeTextureParam(cs, k, SsprDepthId, p.Depth);
                            if (p.Stage == 2)
                            { cmd.SetComputeTextureParam(cs, k, SsprColorId, p.Color); cmd.SetComputeTextureParam(cs, k, SsprOutputId, p.Output); }
                            cmd.DispatchCompute(cs, k, ((int)p.Size.x + 7) / 8, ((int)p.Size.y + 7) / 8, 1);
                            if (p.Stage == 2) p.Owner.SsprRenders++;
                        });
                    }
                }
                return output;
            }
            private sealed class SsprData
            {
                internal CharacterTestShallowWaterSession Owner;
                internal ComputeShader Shader;
                internal int Stage, Kernel;
                internal int IndexBits;
                internal float FilterStrength;
                internal TextureHandle Keys, Output, Depth, Color;
                internal Vector4 Size, Water, Bounds;
                internal Matrix4x4 View, Vp, InvVp, WorldToWater;
            }
        }
    }
}
