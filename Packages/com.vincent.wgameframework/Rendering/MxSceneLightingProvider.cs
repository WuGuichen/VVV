using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MxFramework.Rendering
{
    /// <summary>Opts a game preview camera into its scene's lighting pass.</summary>
    public sealed class MxSceneLightingPreviewCamera : MonoBehaviour
    {
    }

    /// <summary>Borrowed handles valid only during the current camera RenderGraph. No export copies.</summary>
    public sealed class MxSceneLightingFrameData : ContextItem
    {
        public TextureHandle Irradiance, SceneColor, Depth, Normals, Motion;
        public Camera Camera;
        public RenderTextureDescriptor ColorDescriptor;
        public ColorSpace ColorSpace;
        public float PreExposure;
        public int HistoryGeneration;
        public override void Reset()
        { Irradiance=SceneColor=Depth=Normals=Motion=TextureHandle.nullHandle;Camera=null;ColorDescriptor=default;ColorSpace=ColorSpace.Linear;PreExposure=1;HistoryGeneration=0; }
    }
    /// <summary>Scene-owned, camera-independent radiance cache. No runtime ray queries.</summary>
    public sealed class MxSceneLightingProvider : IMxRenderPassProvider, IDisposable
    {
        private readonly LightingPass _pass;
        private readonly FrameOutputPass _outputs;
        public string DebugName => "SceneLighting";
        public int LightingUpdates => _pass.LightingUpdates;
        public int VisibilityUpdates => _pass.VisibilityUpdates;
        public int WorldUpdatePassesRecorded => _pass.WorldUpdatePassesRecorded;
        public int SunDepthTexturesCreated => _pass.SunDepthTexturesCreated;
        public long PersistentBytes => _pass.PersistentBytes;
        public MxSceneLightingProvider(MxSceneLightingData data, MxSceneLightingLook look, Light sun,
            Camera camera, ComputeShader compute, Shader capture, Shader resolve, bool previewInSceneView)
            : this(data,look,sun,camera,compute,capture,resolve,previewInSceneView,null) { }
        /// <summary>Accepts an already validated transport snapshot for editor preview reuse.</summary>
        public MxSceneLightingProvider(MxSceneLightingData data, MxSceneLightingLook look, Light sun,
            Camera camera, ComputeShader compute, Shader capture, Shader resolve, bool previewInSceneView,
            MxSceneLightingPayload decodedPayload)
        { _pass=new LightingPass(data,look,sun,camera,compute,capture,resolve,previewInSceneView,decodedPayload);_outputs=new FrameOutputPass(_pass,look); }
        public void CollectPasses(IMxRenderPassRegistry registry, in MxCameraRenderContextDescriptor cameraContext)
        { if(_pass.Targets(cameraContext)) {registry.RegisterPass(_pass);registry.RegisterPass(_outputs);} }
        public void SetCapsules(Vector4[] starts, Vector4[] ends, int count) => _pass.SetCapsules(starts,ends,count);
        public void SetHistoryGeneration(int generation)=>_pass.HistoryGeneration=generation;
        /// <summary>Retargets camera work without reallocating or invalidating world lighting.</summary>
        public void SetTargetCamera(Camera camera, bool previewInSceneView) => _pass.SetTargetCamera(camera, previewInSceneView);
        /// <summary>Requests motion input for an actual consumer. The motion debug view also requests it.</summary>
        public void SetMotionVectorsRequired(bool required) => _pass.SetMotionVectorsRequired(required);
        public bool MotionVectorsRequired => _pass.MotionVectorsRequired;
        public void Dispose() => _pass.Dispose();

        private sealed class FrameOutputPass : IMxRenderGraphPass
        {
            private readonly LightingPass _owner;
            private readonly MxSceneLightingLook _look;
            public FrameOutputPass(LightingPass owner,MxSceneLightingLook look){_owner=owner;_look=look;}
            public string DebugName=>"SceneLighting.FrameOutputs";
            public MxRenderPhase Phase=>MxRenderPhase.BeforeRenderingPostProcessing;
            public int Order=>-100;
            public bool IsEnabled=>_owner.IsEnabled;
            public IReadOnlyList<SharedRenderTextureKey> Reads=>Array.Empty<SharedRenderTextureKey>();
            public IReadOnlyList<SharedRenderTextureKey> Writes=>Array.Empty<SharedRenderTextureKey>();
            public ScriptableRenderPassInput InputRequirements => _owner.MotionVectorsRequired
                ? ScriptableRenderPassInput.Motion : ScriptableRenderPassInput.None;
            public void Configure(in MxRenderPassConfigureContext context){}
            public void Execute(in MxRenderPassExecuteContext context){}
            public void RecordRenderGraph(RenderGraph graph,ContextContainer frameData,in MxRenderPassConfigureContext context)
            {
                var resources=frameData.Get<UniversalResourceData>();
                var output=frameData.GetOrCreate<MxSceneLightingFrameData>();
                var cameraData=frameData.Get<UniversalCameraData>();
                output.Camera=cameraData.camera;output.ColorDescriptor=cameraData.cameraTargetDescriptor;
                output.ColorSpace=QualitySettings.activeColorSpace;output.PreExposure=1;
                output.HistoryGeneration=_owner.HistoryGeneration;
                output.SceneColor=resources.activeColorTexture;output.Depth=resources.cameraDepthTexture;
                output.Normals=resources.cameraNormalsTexture;output.Motion=resources.motionVectorColor;
                if(_look.DebugView==0)return;
                TextureHandle source=_look.DebugView==1?output.Irradiance:_look.DebugView==2?output.Normals:output.Motion;
                if(!source.IsValid())return;
                using(var builder=graph.AddRasterRenderPass<DebugData>("SceneLighting.DebugFrameInput",out var p))
                {
                    p.Source=source;p.Material=_owner.ResolveMaterial;p.Mode=_look.DebugView;
                    builder.UseTexture(source,AccessFlags.Read);builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.Write);
                    builder.SetRenderFunc((DebugData d,RasterGraphContext ctx)=>
                    {d.Material.SetInt("_MxSceneDebugMode",d.Mode);Blitter.BlitTexture(ctx.cmd,d.Source,new Vector4(1,1,0,0),d.Material,1);});
                }
            }
            private sealed class DebugData {public TextureHandle Source;public Material Material;public int Mode;}
        }

        private sealed class LightingPass : IMxRenderGraphPass, IDisposable
        {
            private static readonly SharedRenderTextureKey[] NoSharedTextures=Array.Empty<SharedRenderTextureKey>();
            private readonly MxSceneLightingData _data;
            private readonly MxSceneLightingLook _look;
            private readonly Light _sun;
            private Camera _camera;
            private bool _preview, _motionVectorsRequired;
            private readonly ComputeShader _compute;
            private readonly Material _capture, _resolve;
            private readonly GraphicsBuffer _nodes,_rows,_edges,_probes,_probeRows,_probeLinks,_visibility,_direct,_bounce,_coefficients;
            private readonly GraphicsBuffer _cellBricks,_brickIndices,_clearDistances;
            private readonly bool _qualityEnabled;
            private readonly bool _hasDilation;
            private readonly GraphicsBuffer _rawCoefficients,_dilationRows,_dilationLinks;
            private readonly int _dilateKernel;
            private readonly RTHandle _sunVisibility;
            private readonly int _injectKernel,_reduceKernel,_bounceKernel,_projectKernel,_capsuleTilesKernel;
            private readonly int _surfaceCount,_nodeCount,_probeCount;
            private readonly Vector4[] _capsules=new Vector4[32],_capsuleEnds=new Vector4[32];
            private readonly Vector4[] _capsuleRanges=new Vector4[32];
            private int _capsuleCount;
            private bool _worldCacheValid;
            private LightingInputs _appliedInputs;
            private Matrix4x4 _sunView, _sunViewProjection;
            private Vector4 _sunRange;
            private bool _disposed;
            public int LightingUpdates { get; private set; }
            public int VisibilityUpdates { get; private set; }
            public int WorldUpdatePassesRecorded { get; private set; }
            public int SunDepthTexturesCreated { get; private set; }
            public long PersistentBytes { get; private set; }
            public Material ResolveMaterial=>_resolve;
            public int HistoryGeneration { get; set; }
            public string DebugName => "SceneLighting.Resolve";
            public MxRenderPhase Phase => MxRenderPhase.AfterRenderingPrePasses;
            public int Order => 20;
            public bool IsEnabled => !_disposed;
            public IReadOnlyList<SharedRenderTextureKey> Reads => NoSharedTextures;
            public IReadOnlyList<SharedRenderTextureKey> Writes => NoSharedTextures;
            public ScriptableRenderPassInput InputRequirements => ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            public bool MotionVectorsRequired => _motionVectorsRequired || _look.DebugView == 3;
            public void Configure(in MxRenderPassConfigureContext context) { }
            public void Execute(in MxRenderPassExecuteContext context) { }

            public LightingPass(MxSceneLightingData data,MxSceneLightingLook look,Light sun,Camera camera,
                ComputeShader compute,Shader capture,Shader resolve,bool preview,MxSceneLightingPayload decodedPayload)
            {
                if(data==null || look==null || sun==null || camera==null || compute==null || capture==null || resolve==null)
                    throw new ArgumentException("Scene lighting requires data, look, sun, camera and all shader references.");
                if(!SystemInfo.supportsComputeShaders) throw new NotSupportedException("Scene lighting needs compute shaders.");
                var payload=decodedPayload ?? data.ReadPayload();
                _data=data;_look=look;_sun=sun;_camera=camera;_compute=compute;_preview=preview;
                _surfaceCount=payload.SurfaceCount;_nodeCount=payload.Nodes.Length;_probeCount=payload.Probes.Length;
                _qualityEnabled=payload.Version==2;
                _hasDilation=_qualityEnabled && (payload.DilationLinks?.Length??0)>0;
                try
                {
                    _capture=CoreUtils.CreateEngineMaterial(capture);_resolve=CoreUtils.CreateEngineMaterial(resolve);
                    _nodes=Buffer(payload.Nodes,80);_rows=Buffer(payload.Rows,8);_edges=Buffer(payload.Edges,8);
                    _probes=Buffer(payload.Probes,64);_probeRows=Buffer(payload.ProbeRows,8);_probeLinks=Buffer(payload.ProbeLinks,32);
                    _visibility=Buffer(payload.ProbeVisibility,8);
                    _cellBricks=Buffer(payload.Topology?.CellBricks ?? new[]{-1},4);
                    _brickIndices=Buffer(payload.Topology?.BrickIndices ?? new[]{0},4);
                    _clearDistances=Buffer(payload.ProbeClearDistances ?? new[]{0f},4);
                    _direct=Buffer(new Vector4[_nodeCount],16);_bounce=Buffer(new Vector4[_nodeCount],16);
                    _coefficients=Buffer(new Coefficients[_probeCount],48);
                    if(_hasDilation)
                    {
                        _rawCoefficients=Buffer(new Coefficients[_probeCount],48);
                        _dilationRows=Buffer(payload.DilationRows,8);_dilationLinks=Buffer(payload.DilationLinks,8);
                        _dilateKernel=_compute.FindKernel("DilateProbes");
                    }
                    var texture=new RenderTexture(2048,2048,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear)
                    { name="MxSceneLighting.SunVisibility",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp };
                    texture.Create();_sunVisibility=RTHandles.Alloc(texture);
                    PersistentBytes+=2048L*2048*4;
                    _injectKernel=_compute.FindKernel("Inject");_reduceKernel=_compute.FindKernel("Reduce");
                    _bounceKernel=_compute.FindKernel("Bounce");_projectKernel=_compute.FindKernel("Project");
                    _capsuleTilesKernel=_compute.FindKernel("BuildCapsuleTiles");
                }
                catch { Dispose(); throw; }
            }
            private GraphicsBuffer Buffer<T>(T[] values,int stride) where T:struct
            {
                var b=new GraphicsBuffer(GraphicsBuffer.Target.Structured,Math.Max(1,values.Length),stride);
                if(values.Length>0)b.SetData(values);PersistentBytes+=(long)b.count*stride;return b;
            }
            public bool Targets(in MxCameraRenderContextDescriptor context)
            {
                if(_disposed || _camera==null || !_camera.gameObject.scene.isLoaded) return false;
                return context.Camera==_camera || MxSceneRenderingAuxiliaryCamera.Matches(context.Camera, _camera) || (_preview && context.CameraKind==MxCameraRenderKind.SceneView &&
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene()==_camera.gameObject.scene) ||
                    (context.CameraKind==MxCameraRenderKind.Game && context.Camera!=null &&
                    context.Camera.gameObject.scene==_camera.gameObject.scene &&
                    context.Camera.TryGetComponent<MxSceneLightingPreviewCamera>(out _));
            }
            public void SetTargetCamera(Camera camera, bool previewInSceneView)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MxSceneLightingProvider));
                _camera = camera;
                _preview = previewInSceneView;
            }
            public void SetMotionVectorsRequired(bool required)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MxSceneLightingProvider));
                _motionVectorsRequired = required;
            }
            public void SetCapsules(Vector4[] starts,Vector4[] ends,int count)
            { _capsuleCount=Mathf.Clamp(count,0,32);Array.Copy(starts,_capsules,_capsuleCount);Array.Copy(ends,_capsuleEnds,_capsuleCount); }

            public void RecordRenderGraph(RenderGraph graph, ContextContainer frameData, in MxRenderPassConfigureContext context)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (!resources.cameraDepthTexture.IsValid() || !resources.cameraNormalsTexture.IsValid()) return;
                var inputs = CaptureInputs();
                if (!_worldCacheValid || !inputs.SameAs(_appliedInputs))
                    RecordWorldUpdate(graph, inputs);

                // These are the only world buffers the stable camera path needs.
                BufferHandle probes = graph.ImportBuffer(_probes), coefficients = graph.ImportBuffer(_coefficients);
                BufferHandle visibility = graph.ImportBuffer(_visibility);
                var field = frameData.GetOrCreate<MxSceneLightFieldData>();
                field.Probes = probes; field.Coefficients = coefficients; field.Visibility = visibility;
                field.CellBricks=graph.ImportBuffer(_cellBricks);field.BrickIndices=graph.ImportBuffer(_brickIndices);field.ClearDistances=graph.ImportBuffer(_clearDistances);
                field.SunVisibility = graph.ImportTexture(_sunVisibility);
                Vector3 fieldOrigin = _data.ProbeOrigin;
                Vector3Int fieldSize = _data.ProbeDimensions;
                field.GridOrigin = new Vector4(fieldOrigin.x, fieldOrigin.y, fieldOrigin.z, 1 / _data.ProbeSpacing);
                field.GridSize = new Vector4(fieldSize.x, fieldSize.y, fieldSize.z, _qualityEnabled ? 1 : 0);
                field.SunView = _sunView; field.SunViewProjection = _sunViewProjection; field.SunRange = _sunRange;
                field.SunDirection = inputs.Direction; field.SunColor = inputs.Sun;
                field.IndirectStrength = Mathf.Max(0, _look.IndirectStrength);
                int width = Mathf.Max(1, (cameraData.cameraTargetDescriptor.width + 1) / 2);
                int height = Mathf.Max(1, (cameraData.cameraTargetDescriptor.height + 1) / 2);
                // Irradiance intentionally uses half-resolution, single-sample HDR, independent of color MSAA.
                var descriptor = new TextureDesc(width, height) { name = "MxSceneLighting.CameraIrradiance",
                    colorFormat = GraphicsFormat.R16G16B16A16_SFloat, clearBuffer = true,
                    clearColor = Color.clear, filterMode = FilterMode.Point };
                TextureHandle result = graph.CreateTexture(descriptor);
                frameData.GetOrCreate<MxSceneLightingFrameData>().Irradiance = result;
                float capsuleStrength = Mathf.Clamp01(_look.CapsuleOcclusion);
                bool useCapsules = _capsuleCount > 0 && capsuleStrength > 0;
                int tilesX = (width + MxSceneLightingCapsuleTiles.TileSize - 1) / MxSceneLightingCapsuleTiles.TileSize;
                int tilesY = (height + MxSceneLightingCapsuleTiles.TileSize - 1) / MxSceneLightingCapsuleTiles.TileSize;
                BufferHandle tileMasks = useCapsules ? RecordCapsuleTiles(graph, cameraData, width, height, tilesX, tilesY, capsuleStrength) : BufferHandle.nullHandle;
                using (var builder = graph.AddRasterRenderPass<ResolveData>("SceneLighting.ResolveCamera", out var d))
                {
                    d.Material = _resolve; d.TileMasks = tileMasks; d.UseCapsules = useCapsules;
                    d.Look = new Vector4(_look.DirectStrength, _look.IndirectStrength, _look.MicroOcclusion, _look.BandStrength);
                    d.SurfaceHighlights = new Vector4(Mathf.Max(0, _look.SpecularStrength), Mathf.Max(0, _look.LightRimStrength), 0, 0);
                    d.Tint = _look.ShadowTint.linear; d.Size = new Vector4(1f / width, 1f / height, width, height);
                    d.BoundsMin = _data.ProbeOrigin;
                    d.BoundsMax = _data.ProbeOrigin + (Vector3)(_data.ProbeDimensions - Vector3Int.one) * _data.ProbeSpacing;
                    var block = d.Properties; block.Clear();
                    block.SetBuffer("_MxLightingProbes", _probes); block.SetBuffer("_MxLightingCoefficients", _coefficients);
                    block.SetBuffer("_MxLightingVisibility", _visibility);
                    block.SetBuffer("_MxLightingCellBricks",_cellBricks);block.SetBuffer("_MxLightingBrickIndices",_brickIndices);block.SetBuffer("_MxLightingClearDistances",_clearDistances);
                    Vector3 origin = _data.ProbeOrigin; Vector3Int size = _data.ProbeDimensions;
                    block.SetVector("_MxLightingGridOrigin", new Vector4(origin.x, origin.y, origin.z, 1 / _data.ProbeSpacing));
                    block.SetVector("_MxLightingGridSize", new Vector4(size.x, size.y, size.z, _qualityEnabled ? 1 : 0));
                    block.SetInt("_MxLightingCapsuleCount", useCapsules ? _capsuleCount : 0);
                    block.SetFloat("_MxLightingCapsuleStrength", capsuleStrength);
                    block.SetVectorArray("_MxLightingCapsules", _capsules); block.SetVectorArray("_MxLightingCapsuleEnds", _capsuleEnds);
                    block.SetVectorArray("_MxLightingCapsuleRanges", _capsuleRanges);
                    block.SetVector("_MxLightingCapsuleTileGrid", new Vector4(tilesX, tilesY, 0, 0));
                    builder.UseBuffer(probes, AccessFlags.Read); builder.UseBuffer(coefficients, AccessFlags.Read);
                    builder.UseBuffer(visibility, AccessFlags.Read);
                    builder.UseBuffer(field.CellBricks,AccessFlags.Read);builder.UseBuffer(field.BrickIndices,AccessFlags.Read);builder.UseBuffer(field.ClearDistances,AccessFlags.Read);
                    if (useCapsules) builder.UseBuffer(tileMasks, AccessFlags.Read);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(result, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetGlobalTextureAfterPass(result, MxSceneLightingShaderIds.Screen);
                    builder.SetRenderFunc(static (ResolveData p, RasterGraphContext ctx) =>
                    {
                        CoreUtils.SetKeyword(p.Material, "MX_CAPSULE_TILES", p.UseCapsules);
                        if (p.UseCapsules) p.Properties.SetBuffer("_MxLightingCapsuleTileMasks", (GraphicsBuffer)p.TileMasks);
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, p.Material, 0, MeshTopology.Triangles, 3, 1, p.Properties);
                        ctx.cmd.SetGlobalFloat(MxSceneLightingShaderIds.Enabled, 1);
                        ctx.cmd.SetGlobalVector(MxSceneLightingShaderIds.Look, p.Look);
                        ctx.cmd.SetGlobalVector(MxSceneLightingShaderIds.ShadowTint, p.Tint);
                        ctx.cmd.SetGlobalVector(MxSceneLightingShaderIds.SurfaceHighlights, p.SurfaceHighlights);
                        ctx.cmd.SetGlobalVector(MxSceneLightingShaderIds.TexelSize, p.Size);
                        ctx.cmd.SetGlobalVector(MxSceneLightingShaderIds.BoundsMin, p.BoundsMin);
                        ctx.cmd.SetGlobalVector(MxSceneLightingShaderIds.BoundsMax, p.BoundsMax);
                    });
                }
            }

            private BufferHandle RecordCapsuleTiles(RenderGraph graph, UniversalCameraData cameraData,
                int width, int height, int tilesX, int tilesY, float strength)
            {
                var mask = graph.CreateBuffer(new BufferDesc(tilesX * tilesY, sizeof(uint)) { name = "MxSceneLighting.CapsuleTiles" });
                using (var builder = graph.AddComputePass<TileData>("SceneLighting.BuildCapsuleTiles", out var d))
                {
                    d.Shader = _compute; d.Kernel = _capsuleTilesKernel; d.Masks = mask;
                    d.Count = _capsuleCount; d.TilesX = tilesX; d.TilesY = tilesY;
                    Matrix4x4 view = cameraData.GetViewMatrix();
                    Matrix4x4 projection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true);
                    for (int i = 0; i < _capsuleCount; i++)
                    {
                        Vector4 range = MxSceneLightingCapsuleTiles.Range(_capsules[i].w, strength, _capsuleCount);
                        _capsuleRanges[i] = range;
                        // Stereo uses conservative full coverage rather than culling from only one eye.
                        d.Rects[i] = cameraData.camera.stereoEnabled ? new Vector4(0, 0, width, height) :
                            MxSceneLightingCapsuleTiles.ScreenRect(_capsules[i], _capsuleEnds[i], Mathf.Sqrt(range.z),
                                view, projection, cameraData.camera.nearClipPlane, width, height, SystemInfo.graphicsUVStartsAtTop);
                    }
                    builder.UseBuffer(mask, AccessFlags.Write);
                    builder.SetRenderFunc(static (TileData p, ComputeGraphContext ctx) =>
                    {
                        ctx.cmd.SetComputeIntParam(p.Shader, "_CapsuleCount", p.Count);
                        ctx.cmd.SetComputeIntParam(p.Shader, "_CapsuleTilesX", p.TilesX);
                        ctx.cmd.SetComputeIntParam(p.Shader, "_CapsuleTilesY", p.TilesY);
                        ctx.cmd.SetComputeVectorArrayParam(p.Shader, "_CapsuleScreenRects", p.Rects);
                        ctx.cmd.SetComputeBufferParam(p.Shader, p.Kernel, "_CapsuleTileMasks", (GraphicsBuffer)p.Masks);
                        ctx.cmd.DispatchCompute(p.Shader, p.Kernel, (p.TilesX + 7) / 8, (p.TilesY + 7) / 8, 1);
                    });
                }
                return mask;
            }

            private LightingInputs CaptureInputs()
            {
                Color sunlight = _sun.enabled ? _sun.color.linear * _sun.intensity : Color.black;
                if (_sun.useColorTemperature) sunlight *= Mathf.CorrelatedColorTemperatureToRGB(_sun.colorTemperature);
                return new LightingInputs(-_sun.transform.forward, sunlight, _look.Sky.linear, _look.Equator.linear, _look.Ground.linear);
            }

            private void RecordWorldUpdate(RenderGraph graph, LightingInputs inputs)
            {
                bool updateSun = !_worldCacheValid || !inputs.Direction.Equals(_appliedInputs.Direction);
                float radius = _data.Bounds.extents.magnitude + 2, near = 0.1f, far = radius * 2 + 4;
                Vector3 up = Mathf.Abs(Vector3.Dot(inputs.Direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
                var rotation = Quaternion.LookRotation(-inputs.Direction, up);
                Matrix4x4 view = Matrix4x4.Scale(new Vector3(1, 1, -1)) *
                    Matrix4x4.TRS(_data.Bounds.center + inputs.Direction * (radius + 2), rotation, Vector3.one).inverse;
                Matrix4x4 vp = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-radius, radius, -radius, radius, near, far), true) * view;
                Vector4 range = new Vector4(near, 1 / (far - near), SystemInfo.graphicsUVStartsAtTop ? 1 : 0, 0);
                _sunView = view; _sunViewProjection = vp; _sunRange = range;
                TextureHandle sun = graph.ImportTexture(_sunVisibility);
                if (updateSun)
                {
                    // A fixed-size world shadow scratch attachment, not a camera-sized color/depth texture.
                    TextureHandle depth = graph.CreateTexture(new TextureDesc(2048, 2048)
                        { name = "MxSceneLighting.SunDepth.Transient", depthBufferBits = DepthBits.Depth24, clearBuffer = true });
                    SunDepthTexturesCreated++;
                    using (var builder = graph.AddRasterRenderPass<SunData>("SceneLighting.UpdateSunVisibility", out var d))
                    {
                        d.Owner = this; d.Mesh = _data.Occluders; d.Material = _capture;
                        d.Properties.Clear(); d.Properties.SetMatrix("_CaptureVP", vp); d.Properties.SetMatrix("_CaptureView", view);
                        d.Properties.SetVector("_CaptureRange", new Vector4(near, range.y, far, 0));
                        builder.SetRenderAttachment(sun, 0, AccessFlags.Write);
                        builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                        builder.AllowPassCulling(false);
                        builder.SetRenderFunc(static (SunData p, RasterGraphContext ctx) =>
                        {
                            ctx.cmd.ClearRenderTarget(true, true, Color.white, 1);
                            ctx.cmd.DrawMesh(p.Mesh, Matrix4x4.identity, p.Material, 0, 2, p.Properties);
                            p.Owner.VisibilityUpdates++;
                        });
                    }
                }
                using (var builder = graph.AddComputePass<UpdateData>("SceneLighting.UpdateWorldCache", out var d))
                {
                    d.Owner = this; d.Inputs = inputs; d.View = view; d.VP = vp; d.Range = range; d.Sun = sun;
                    builder.UseBuffer(graph.ImportBuffer(_nodes), AccessFlags.Read);
                    builder.UseBuffer(graph.ImportBuffer(_rows), AccessFlags.Read); builder.UseBuffer(graph.ImportBuffer(_edges), AccessFlags.Read);
                    builder.UseBuffer(graph.ImportBuffer(_probes), AccessFlags.Read); builder.UseBuffer(graph.ImportBuffer(_probeRows), AccessFlags.Read);
                    builder.UseBuffer(graph.ImportBuffer(_probeLinks), AccessFlags.Read);
                    builder.UseBuffer(graph.ImportBuffer(_direct), AccessFlags.ReadWrite);
                    builder.UseBuffer(graph.ImportBuffer(_bounce), AccessFlags.ReadWrite);
                    builder.UseBuffer(graph.ImportBuffer(_coefficients), AccessFlags.Write);
                    if(_hasDilation)
                    {
                        builder.UseBuffer(graph.ImportBuffer(_rawCoefficients),AccessFlags.ReadWrite);
                        builder.UseBuffer(graph.ImportBuffer(_dilationRows),AccessFlags.Read);builder.UseBuffer(graph.ImportBuffer(_dilationLinks),AccessFlags.Read);
                    }
                    builder.UseTexture(sun, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (UpdateData p, ComputeGraphContext ctx) => p.Owner.UpdateWorldCache(ctx.cmd, p));
                }
                WorldUpdatePassesRecorded++;
            }

            private void UpdateWorldCache(ComputeCommandBuffer cmd, UpdateData d)
            {
                cmd.SetComputeIntParam(_compute,"_SurfaceCount",_surfaceCount);cmd.SetComputeIntParam(_compute,"_NodeCount",_nodeCount);
                cmd.SetComputeIntParam(_compute,"_ProbeCount",_probeCount);
                cmd.SetComputeVectorParam(_compute,"_SunDirection",d.Inputs.Direction);cmd.SetComputeVectorParam(_compute,"_SunColor",d.Inputs.Sun);
                cmd.SetComputeVectorParam(_compute,"_SkyUp",d.Inputs.Sky);cmd.SetComputeVectorParam(_compute,"_SkyEquator",d.Inputs.Equator);cmd.SetComputeVectorParam(_compute,"_SkyGround",d.Inputs.Ground);
                cmd.SetComputeMatrixParam(_compute,"_SunVP",d.VP);cmd.SetComputeMatrixParam(_compute,"_SunView",d.View);
                cmd.SetComputeVectorParam(_compute,"_SunRange",d.Range);
                cmd.SetComputeBufferParam(_compute,_injectKernel,"_Nodes",_nodes);cmd.SetComputeBufferParam(_compute,_injectKernel,"_Destination",_direct);
                cmd.SetComputeTextureParam(_compute,_injectKernel,"_SunVisibility",d.Sun);
                cmd.DispatchCompute(_compute,_injectKernel,(_surfaceCount+63)/64,1,1);
                Reduce(cmd,_direct);
                cmd.SetComputeBufferParam(_compute,_bounceKernel,"_Nodes",_nodes);cmd.SetComputeBufferParam(_compute,_bounceKernel,"_Rows",_rows);
                cmd.SetComputeBufferParam(_compute,_bounceKernel,"_Edges",_edges);cmd.SetComputeBufferParam(_compute,_bounceKernel,"_Source",_direct);
                cmd.SetComputeBufferParam(_compute,_bounceKernel,"_Direct",_direct);cmd.SetComputeBufferParam(_compute,_bounceKernel,"_Destination",_bounce);
                cmd.DispatchCompute(_compute,_bounceKernel,(_surfaceCount+63)/64,1,1);Reduce(cmd,_bounce);
                cmd.SetComputeBufferParam(_compute,_projectKernel,"_Probes",_probes);cmd.SetComputeBufferParam(_compute,_projectKernel,"_ProbeRows",_probeRows);
                cmd.SetComputeBufferParam(_compute,_projectKernel,"_ProbeLinks",_probeLinks);cmd.SetComputeBufferParam(_compute,_projectKernel,"_Source",_bounce);
                cmd.SetComputeBufferParam(_compute,_projectKernel,"_Coefficients",_hasDilation ? _rawCoefficients : _coefficients);
                cmd.DispatchCompute(_compute,_projectKernel,(_probeCount+63)/64,1,1);
                if(_hasDilation)
                {
                    cmd.SetComputeBufferParam(_compute,_dilateKernel,"_RawProbeCoefficients",_rawCoefficients);
                    cmd.SetComputeBufferParam(_compute,_dilateKernel,"_DilationRows",_dilationRows);cmd.SetComputeBufferParam(_compute,_dilateKernel,"_DilationLinks",_dilationLinks);
                    cmd.SetComputeBufferParam(_compute,_dilateKernel,"_Coefficients",_coefficients);
                    cmd.DispatchCompute(_compute,_dilateKernel,(_probeCount+63)/64,1,1);
                }
                // Commit only from execution. Recording a graph that is discarded cannot validate this cache.
                _appliedInputs = d.Inputs; _worldCacheValid = true; LightingUpdates++;
            }
            private void Reduce(ComputeCommandBuffer cmd,GraphicsBuffer buffer)
            {
                if(_nodeCount==_surfaceCount)return;
                cmd.SetComputeBufferParam(_compute,_reduceKernel,"_Rows",_rows);cmd.SetComputeBufferParam(_compute,_reduceKernel,"_Edges",_edges);
                cmd.SetComputeBufferParam(_compute,_reduceKernel,"_Destination",buffer);
                cmd.DispatchCompute(_compute,_reduceKernel,(_nodeCount-_surfaceCount+63)/64,1,1);
            }
            public void Dispose()
            {
                if(_disposed)return;_disposed=true;
                _nodes?.Dispose();_rows?.Dispose();_edges?.Dispose();_probes?.Dispose();_probeRows?.Dispose();_probeLinks?.Dispose();
                _visibility?.Dispose();_direct?.Dispose();_bounce?.Dispose();_coefficients?.Dispose();
                _cellBricks?.Dispose();_brickIndices?.Dispose();_clearDistances?.Dispose();
                _rawCoefficients?.Dispose();_dilationRows?.Dispose();_dilationLinks?.Dispose();
                if(_sunVisibility!=null) { var rt=_sunVisibility.rt;_sunVisibility.Release();CoreUtils.Destroy(rt); }
                CoreUtils.Destroy(_capture);CoreUtils.Destroy(_resolve);
            }
            private readonly struct LightingInputs
            {
                public readonly Vector3 Direction;
                public readonly Color Sun, Sky, Equator, Ground;
                public LightingInputs(Vector3 direction, Color sun, Color sky, Color equator, Color ground)
                { Direction=direction;Sun=sun;Sky=sky;Equator=equator;Ground=ground; }
                public bool SameAs(LightingInputs other) => Direction.Equals(other.Direction) && Sun.Equals(other.Sun) &&
                    Sky.Equals(other.Sky) && Equator.Equals(other.Equator) && Ground.Equals(other.Ground);
            }
            private sealed class UpdateData
            { public LightingPass Owner; public LightingInputs Inputs; public Matrix4x4 View, VP; public Vector4 Range; public TextureHandle Sun; }
            private sealed class SunData
            { public LightingPass Owner; public Mesh Mesh; public Material Material; public readonly MaterialPropertyBlock Properties=new MaterialPropertyBlock(); }
            private sealed class TileData
            {
                public ComputeShader Shader; public int Kernel, Count, TilesX, TilesY; public BufferHandle Masks;
                public readonly Vector4[] Rects=new Vector4[32];
            }
            private struct Coefficients { public Vector4 R,G,B; }
            private sealed class ResolveData
            {
                public Material Material; public Vector4 Look,Tint,Size,BoundsMin,BoundsMax,SurfaceHighlights;
                public BufferHandle TileMasks; public bool UseCapsules;
                public readonly MaterialPropertyBlock Properties=new MaterialPropertyBlock();
            }
        }
    }
}
