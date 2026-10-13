using System;
using System.Collections.Generic;
using MxFramework.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestShallowWaterSession
    {
        private sealed partial class SurfacePass : IMxRenderPassProvider, IMxRenderGraphPass
        {
            internal const string Name = "CharacterTest.ShallowWater";
            private readonly CharacterTestShallowWaterSession _owner;
            private static readonly int SceneColorId = Shader.PropertyToID("_WgWaterSceneColor");
            private static readonly int ReflectionId = Shader.PropertyToID("_WgWaterReflection");
            private static readonly int ReflectionVpId = Shader.PropertyToID("_WgWaterReflectionVP");
            private static readonly int SurfaceId = Shader.PropertyToID("_WgWaterSurface");
            private static readonly int ResponseId = Shader.PropertyToID("_WgWaterResponse");
            private static readonly int DetailId = Shader.PropertyToID("_WgWaterDetail");
            private static readonly int WaveId = Shader.PropertyToID("_WgWaterWave");
            private static readonly int TintId = Shader.PropertyToID("_WgWaterTint");
            private static readonly int SizeId = Shader.PropertyToID("_WgWaterSize");
            private static readonly int TimeId = Shader.PropertyToID("_WgWaterTime");
            private static readonly int DepthId = Shader.PropertyToID("_CameraDepthTexture");
            private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
            private readonly Plane[] _planes = new Plane[6];
            internal SurfacePass(CharacterTestShallowWaterSession owner) { _owner=owner; }
            public string DebugName => Name;
            public MxRenderPhase Phase => MxRenderPhase.BeforeRenderingTransparents;
            public int Order => -30;
            public bool IsEnabled => _owner != null && _owner.IsActive && _owner.SurfaceEnabled;
            public IReadOnlyList<SharedRenderTextureKey> Reads => Array.Empty<SharedRenderTextureKey>();
            public IReadOnlyList<SharedRenderTextureKey> Writes => Array.Empty<SharedRenderTextureKey>();
            public ScriptableRenderPassInput InputRequirements => ScriptableRenderPassInput.Depth
                | (WantsSspr ? ScriptableRenderPassInput.Color : ScriptableRenderPassInput.None);
            public void CollectPasses(IMxRenderPassRegistry registry, in MxCameraRenderContextDescriptor camera)
            {
                if (!IsEnabled || !_owner.SurfaceVisible(camera.Camera, _planes)) return;
                if ((camera.Camera == _owner._source && camera.CameraKind == MxCameraRenderKind.Game)
                    || (camera.CameraKind == MxCameraRenderKind.SceneView && _owner.TryGetScenePreview(camera.Camera, out _)))
                    registry.RegisterPass(this);
            }
            public void Configure(in MxRenderPassConfigureContext context) { }
            public void Execute(in MxRenderPassExecuteContext context) { }
            public void RecordRenderGraph(RenderGraph graph, ContextContainer frameData, in MxRenderPassConfigureContext context)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.activeColorTexture.IsValid() || !resources.cameraDepthTexture.IsValid() || _owner._material == null
                    || !_owner.SurfaceVisible(context.CameraContext.Camera, _planes)) return;
                ScenePreviewView scene = null;
                if (context.CameraContext.CameraKind == MxCameraRenderKind.SceneView
                    && !_owner.TryGetScenePreview(context.CameraContext.Camera, out scene)) return;
                TextureHandle sspr = RecordSspr(graph, frameData, resources);
                TextureHandle rippleField = RecordRippleField(graph, context.CameraContext.Camera,
                    scene != null ? scene.RippleField : _owner._gameRippleField, out Vector4 rippleRect);
                bool refract = _owner.Settings.UseRefraction && _owner.Settings.RefractionStrength > 0;
                TextureHandle color = TextureHandle.nullHandle;
                if (refract)
                {
                    var desc = graph.GetTextureDesc(resources.activeColorTexture); desc.name = Name + ".SceneColor"; desc.clearBuffer=false;
                    color = graph.CreateTexture(desc);
                    using (var builder=graph.AddRasterRenderPass<CopyData>(Name+".CopyOpaque", out var d))
                    {
                        d.Owner=_owner; d.Source=resources.activeColorTexture; builder.UseTexture(d.Source,AccessFlags.Read);
                        builder.SetRenderAttachment(color,0,AccessFlags.Write);
                        builder.SetRenderFunc(static (CopyData p,RasterGraphContext ctx)=>
                        {
                            Blitter.BlitTexture(ctx.cmd,p.Source,new Vector4(1,1,0,0),0,false);
                            p.Owner.SurfaceColorCopies++;
                        });
                    }
                }
                using (var builder=graph.AddRasterRenderPass<DrawData>(Name+".Surface",out var d))
                {
                    d.Color=color; d.Owner=_owner; d.Material=_owner._material; d.Mesh=_owner._mesh; d.Refract=refract;
                    d.Properties=scene != null ? scene.Properties : _properties; d.ScenePreview=scene != null;
                    d.Matrix=_owner.LocalToWorld * Matrix4x4.Scale(new Vector3(_owner.Settings.Size.x,1,_owner.Settings.Size.y));
                    d.Vp=scene != null ? scene.ViewProjection : _owner._reflectionViewProjection;
                    d.Sspr = sspr.IsValid();
                    d.RippleField = rippleField; d.RippleRect = rippleRect;
                    bool reflection = _owner.ReflectionEnabled && (scene != null ? scene.Wanted && scene.Ready : _owner._reflectionWanted && _owner.ReflectionReady);
                    if (reflection)
                    {
                        var rt = scene != null ? scene.Texture : _owner.ReflectionTexture;
                        var info = new RenderTargetInfo { width=rt.width, height=rt.height, volumeDepth=rt.volumeDepth,
                            msaaSamples=rt.antiAliasing, format=rt.graphicsFormat, bindMS=rt.bindTextureMS };
                        d.Reflection=graph.ImportTexture(scene != null ? scene.ColorView : _owner._reflectionColorView,info);
                        d.ReflectionTexture=rt;
                    }
                    else d.Reflection=sspr;
                    d.Depth=resources.cameraDepthTexture;
                    if (refract) builder.UseTexture(color,AccessFlags.Read);
                    builder.UseTexture(d.Depth,AccessFlags.Read);
                    if(d.Reflection.IsValid()) builder.UseTexture(d.Reflection,AccessFlags.Read);
                    if (rippleField.IsValid()) builder.UseTexture(rippleField, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture,AccessFlags.Read);
                    builder.SetRenderFunc(static (DrawData p,RasterGraphContext ctx)=>
                    {
                        var s=p.Owner.Settings; var b=p.Properties; b.Clear();
                        if (p.Refract) b.SetTexture(SceneColorId,p.Color);
                        b.SetTexture(DepthId,p.Depth);
                        if (p.Sspr) b.SetTexture(ReflectionId,p.Reflection);
                        else b.SetTexture(ReflectionId,p.ReflectionTexture!=null?(Texture)p.ReflectionTexture:Texture2D.blackTexture);
                        b.SetMatrix(ReflectionVpId,p.Vp);
                        bool environment = !s.UsePlanarReflection && p.Owner.Configuration.ReflectionEnabled;
                        b.SetVector(SurfaceId,new Vector4(p.Reflection.IsValid() || environment ? s.ReflectionStrength:0,s.BaseReflectivity,s.TintOpacity,s.ContactSoftness));
                        b.SetVector(ResponseId,new Vector4(s.RefractionStrength,s.EdgeSoftness,s.HighlightStrength,s.MaximumWaterDepth));
                        b.SetVector(DetailId,new Vector4(s.MinimumReflection,s.ReflectionDistortion,s.RippleSheen,p.Sspr ? 2 : environment ? 1 : 0));
                        b.SetVector(WaveId,new Vector4(s.BackgroundSlope,s.RippleSpeed,s.RippleLifetime,0));
                        b.SetVector(SizeId,new Vector4(s.Size.x,s.Size.y,0,0)); b.SetColor(TintId,s.Tint.linear);
                        b.SetFloat(TimeId,(float)p.Owner.Clock);
                        b.SetVector(FieldRectId,p.RippleRect);
                        if (p.RippleField.IsValid()) b.SetTexture(FieldTextureId,p.RippleField);
                        else b.SetTexture(FieldTextureId,Texture2D.blackTexture);
                        ctx.cmd.DrawMesh(p.Mesh,p.Matrix,p.Material,0,p.Refract ? 0 : 1,b);
                        if (p.ScenePreview) p.Owner.SceneSurfaceDraws++; else p.Owner.SurfaceDraws++;
                    });
                }
            }
            private sealed class CopyData { internal TextureHandle Source; internal CharacterTestShallowWaterSession Owner; }
            private sealed class DrawData
            {
                internal CharacterTestShallowWaterSession Owner; internal TextureHandle Color,Reflection,Depth;
                internal Material Material; internal Mesh Mesh; internal Matrix4x4 Matrix,Vp;
                internal RenderTexture ReflectionTexture;
                internal MaterialPropertyBlock Properties;
                internal bool ScenePreview;
                internal bool Refract;
                internal bool Sspr;
                internal TextureHandle RippleField;
                internal Vector4 RippleRect;
            }
        }
    }
}
