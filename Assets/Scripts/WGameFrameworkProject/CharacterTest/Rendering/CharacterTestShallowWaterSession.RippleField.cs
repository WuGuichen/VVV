using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestShallowWaterSession
    {
        private readonly RippleFieldView _gameRippleField = new RippleFieldView();
        public int RippleFieldUpdates { get; private set; }
        public string RippleFieldIssue { get; private set; } = string.Empty;
        public RenderTexture RippleFieldTexture => _gameRippleField.Target?.rt;

        internal sealed class RippleFieldView
        {
            internal RTHandle Target;
            internal Vector2 Center;
            internal int Resolution, Emission, Budget;
            internal float Extent, CapturedTime;
            internal bool Ready;
            internal readonly Vector4[] Sources = new Vector4[32], Shapes = new Vector4[32], Sheen = new Vector4[8], Tiles = new Vector4[256];
            internal void Release()
            {
                Ready = false;
                var texture = Target?.rt; Target?.Release(); Target = null;
                DestroyOwned(texture);
            }
        }

        internal static Vector2 SnapRippleCenter(Vector2 focus, float extent, int resolution)
        {
            float texel = extent / resolution;
            return new Vector2(Mathf.Round(focus.x / texel), Mathf.Round(focus.y / texel)) * texel;
        }
        internal static bool RippleFieldDue(bool ready, float now, float captured, int rate, bool changed)
        { return !ready || changed || now < captured || now - captured + .00001f >= 1f / Mathf.Max(1, rate); }

        private Vector2 RippleFocus(UnityEngine.Camera camera)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            float distance = Mathf.Abs(ray.direction.y) > .0001f ? (Position.y - ray.origin.y) / ray.direction.y : -1;
            Vector3 point = distance >= 0 ? ray.GetPoint(Mathf.Min(distance, 200)) : camera.transform.position;
            Vector3 local = LocalToWorld.inverse.MultiplyPoint3x4(point);
            local.x = Mathf.Clamp(local.x, -Settings.Size.x * .5f, Settings.Size.x * .5f);
            local.z = Mathf.Clamp(local.z, -Settings.Size.y * .5f, Settings.Size.y * .5f); local.y = 0;
            point = LocalToWorld.MultiplyPoint3x4(local);
            return new Vector2(point.x, point.z);
        }

        private sealed partial class SurfacePass
        {
            private ComputeShader _rippleCompute;
            private int _rippleKernel;
            private bool _rippleSupported;
            private static readonly int FieldTextureId = Shader.PropertyToID("_WgWaterRippleField");
            private static readonly int FieldRectId = Shader.PropertyToID("_WgWaterRippleRect");
            private static readonly int FieldOutputId = Shader.PropertyToID("_RippleOutput");
            private static readonly int FieldResolutionId = Shader.PropertyToID("_RippleResolution");

            private TextureHandle RecordRippleField(RenderGraph graph, UnityEngine.Camera camera, RippleFieldView field, out Vector4 rectangle)
            {
                rectangle = Vector4.zero; _owner.RippleFieldIssue = string.Empty;
                var settings = _owner.Settings; var waves = _owner._ripples;
                float now = (float)_owner.Clock;
                waves.SetLimit(settings.EffectiveRippleLimit); waves.Expire(now, settings.RippleLifetime);
                if (!_owner.RipplesEnabled || waves.Count == 0 || settings.EffectiveRippleLimit == 0)
                { field.Release(); return TextureHandle.nullHandle; }
                if (_rippleCompute != settings.RippleCompute)
                {
                    _rippleCompute = settings.RippleCompute;
                    _rippleSupported = _rippleCompute != null && SystemInfo.supportsComputeShaders
                        && SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.LoadStore)
                        && SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Sample)
                        && _rippleCompute.HasKernel("BuildRippleField");
                    if (_rippleSupported) _rippleKernel = _rippleCompute.FindKernel("BuildRippleField");
                    field.Ready = false;
                }
                if (!_rippleSupported)
                {
                    field.Release(); _owner.RippleFieldIssue = "交互涟漪纹理不可用：请检查计算资源及设备格式支持。背景水纹保持可用。";
                    return TextureHandle.nullHandle;
                }
                int resolution = settings.EffectiveRippleResolution;
                float extent = settings.EffectiveRippleExtent;
                bool recreate = field.Target == null || !field.Target.rt.IsCreated() || field.Resolution != resolution;
                Vector2 focus = _owner.RippleFocus(camera);
                bool recenter = recreate || !field.Ready || field.Extent != extent || (focus - field.Center).sqrMagnitude > extent * extent / 64;
                Vector2 center = recenter ? SnapRippleCenter(focus, extent, resolution) : field.Center;
                if (!waves.IntersectsField(center, extent, now, settings.RippleSpeed))
                { field.Release(); return TextureHandle.nullHandle; }
                if (recreate)
                {
                    field.Release();
                    var texture = new RenderTexture(new RenderTextureDescriptor(resolution, resolution)
                    { graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite = true, msaaSamples = 1, depthStencilFormat = GraphicsFormat.None })
                    { name = "WGame.ShallowWater.RippleField", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                    if (!texture.Create()) { DestroyOwned(texture); _owner.RippleFieldIssue = "交互涟漪纹理创建失败。"; return TextureHandle.nullHandle; }
                    field.Target = RTHandles.Alloc(texture); field.Resolution = resolution;
                }
                int budget = HashCode.Combine(settings.RippleLifetime, settings.RippleSpeed, settings.RippleQuality, settings.EffectiveRippleLimit, _rippleCompute);
                bool update = RippleFieldDue(field.Ready, now, field.CapturedTime, settings.EffectiveRippleRate,
                    recenter || field.Emission != waves.EmissionVersion || field.Budget != budget);
                if (!update) { center = field.Center; extent = field.Extent; }
                rectangle = new Vector4(center.x - extent * .5f, center.y - extent * .5f, 1 / extent, 16);
                TextureHandle target = graph.ImportTexture(field.Target);
                if (!update) return target;
                waves.BuildDrawData(now, settings.RippleLifetime, settings.RippleSpeed,
                    Matrix4x4.Translate(new Vector3(-center.x, 0, -center.y)), new Vector2(extent, extent));
                // Each view owns its upload snapshot; another camera cannot overwrite queued graph data.
                Array.Copy(waves.DrawSources, field.Sources, 32); Array.Copy(waves.DrawShapes, field.Shapes, 32);
                Array.Copy(waves.DrawSheen, field.Sheen, 8); Array.Copy(waves.TileMasks, field.Tiles, 256);
                using (var builder = graph.AddComputePass<RippleFieldData>("ShallowWater.RippleField", out var d))
                {
                    d.Owner = _owner; d.Field = field; d.Shader = _rippleCompute; d.Kernel = _rippleKernel;
                    d.Target = target; d.Rect = rectangle; d.Center = center; d.Extent = extent; d.Time = now;
                    d.Emission = waves.EmissionVersion; d.Budget = budget;
                    builder.UseTexture(target, AccessFlags.Write);
                    builder.SetRenderFunc(static (RippleFieldData p, ComputeGraphContext ctx) =>
                    {
                        var cmd = ctx.cmd; var cs = p.Shader; var f = p.Field;
                        cmd.SetComputeIntParam(cs, FieldResolutionId, f.Resolution);
                        cmd.SetComputeVectorParam(cs, FieldRectId, p.Rect);
                        cmd.SetComputeVectorArrayParam(cs, RipplesId, f.Sources); cmd.SetComputeVectorArrayParam(cs, ShapesId, f.Shapes);
                        cmd.SetComputeVectorArrayParam(cs, SheenId, f.Sheen); cmd.SetComputeVectorArrayParam(cs, TilesId, f.Tiles);
                        cmd.SetComputeTextureParam(cs, p.Kernel, FieldOutputId, p.Target);
                        cmd.DispatchCompute(cs, p.Kernel, (f.Resolution + 7) / 8, (f.Resolution + 7) / 8, 1);
                        f.Center = p.Center; f.Extent = p.Extent; f.CapturedTime = p.Time;
                        f.Emission = p.Emission; f.Budget = p.Budget; f.Ready = true; p.Owner.RippleFieldUpdates++;
                    });
                }
                return target;
            }
            private sealed class RippleFieldData
            {
                internal CharacterTestShallowWaterSession Owner;
                internal RippleFieldView Field;
                internal ComputeShader Shader;
                internal TextureHandle Target;
                internal int Kernel, Emission, Budget;
                internal Vector4 Rect;
                internal Vector2 Center;
                internal float Extent, Time;
            }
        }
    }
}
