using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MxFramework.Rendering
{
    /// <summary>Cached static depth plus current-pose triangle coverage. No geometry draw commands.</summary>
    public sealed class MxDirectionalShadowProvider : IMxRenderPassProvider, IMxRenderGraphPass, IDisposable
    {
        // Emergency suspension switch. The bounded backend is enabled after its
        // triangle, alpha, camera and animated-pose acceptance on the target platform.
        public static bool DynamicRasterValidationApproved { get; set; } = true;
        // Legacy constant names: limits apply to one complete shadow update,
        // from ClearDepth through every caster's count and raster dispatches.
        // Edit-mode camera redraws can each request an update; these limits do
        // not cap aggregate work across all camera invocations in an Editor tick.
        public const int MaximumCandidatePixelsPerFrame = 8 * 1024 * 1024;
        public const int MaximumTrianglesPerFrame = 262144;
        private readonly MxDirectionalShadowData _data;
        private readonly ComputeShader _compute;
        private readonly Scene _scene;
        private readonly bool _preview;
        private readonly RTHandle _staticDepth;
        private readonly RTHandle _peripheralDepth;
        private readonly GraphicsBuffer _status;
        private double _nextStatusRead;
        private bool _statusReadPending;
        private string _poseIssue=string.Empty;
        private string _casterIssue=string.Empty;
        // Do not start GPU timestamp queries as a side effect of enabling shadows.
        // A distinct CPU-only marker also avoids sharing the old GPU marker flags.
        private readonly ProfilingSampler _sampler=CreateCpuSampler();
        public float GpuMilliseconds=>0;
        public float CpuMilliseconds=>_sampler?.cpuElapsedTime??0;
        private static ProfilingSampler CreateCpuSampler()
        {
            var sampler=ProfilingSampler.Create("DirectionalTriangleShadows.CPU",Unity.Profiling.LowLevel.MarkerFlags.VerbosityAdvanced);
            if(sampler!=null)sampler.enableRecording=true;
            return sampler;
        }
        public uint CandidatePixels {get;private set;}
        private RTHandle _dynamicDepth;
        private readonly List<Actor> _actors = new List<Actor>();
        private readonly Dictionary<Renderer,Actor> _poseActors = new Dictionary<Renderer,Actor>();
        private int _poseDemandPreparedFrame = -1;
        private float _poseDemandPadding;
        private readonly MxDirectionalShadowReceiverScopes _receiverScopes=new MxDirectionalShadowReceiverScopes();
        private readonly List<UnityEngine.Camera> _knownReceiverCameras=new List<UnityEngine.Camera>();
        private sealed class MaterialCoverageEntry { internal long Epoch; internal MxDirectionalShadowCoverage Coverage; }
        private readonly Dictionary<Material,MaterialCoverageEntry> _materialCoverage=new Dictionary<Material,MaterialCoverageEntry>();
        private long _coverageEpoch;
        private MxDirectionalShadowCoverage _lastCoverage;
        private bool _hasLastCoverage;
        public int CandidateCasters {get;private set;}
        public int CulledCasters {get;private set;}
        public int PoseBufferAcquisitions {get;private set;}
        public int CpuPoseFallbacks {get;private set;}
        public int BoneEnvelopeRebuilds {get;private set;}
        public int ReceiverRegionRebuilds {get;private set;}
        public int ConservativeCameraRebuilds {get;private set;}
        public int MaterialCoverageReads {get;private set;}
        public int CoverageOverrideReads {get;private set;}
        public int CoverageBindingChanges {get;private set;}
        public long PosePublicationReads {get;private set;}
        public long ConservativePoseDemands {get;private set;}
        public long ReleasedPoseDemands {get;private set;}
        private readonly Dictionary<Renderer, ShadowCastingMode> _staticModes = new Dictionary<Renderer, ShadowCastingMode>();
        private bool _staticCastingStateCaptured, _staticCastingStateValid = true;
        private MxSceneRenderingConfiguration _configuration;
        private readonly int _clearKernel, _countKernel, _rasterKernel;
        private int _resolution;
        private int _lastComputedFrame=-1;
        private Vector4 _frameRectangle;
        private Light _ownedSun;
        private LightShadows _previousSunShadows;
        private double _nextRefresh;
        private bool _disposed, _sourceValid = true;
        private string _sourceIssue = string.Empty;
        public string DebugName => "DirectionalTriangleShadows";
        public MxRenderPhase Phase => MxRenderPhase.BeforeRenderingOpaques;
        public int Order => -150;
        public bool IsEnabled => !_disposed;
        public IReadOnlyList<SharedRenderTextureKey> Reads => Array.Empty<SharedRenderTextureKey>();
        public IReadOnlyList<SharedRenderTextureKey> Writes => Array.Empty<SharedRenderTextureKey>();
        public ScriptableRenderPassInput InputRequirements => ScriptableRenderPassInput.None;
        public string Issue { get; private set; } = string.Empty;
        public int ActiveCasters { get; private set; }
        public int TriangleDispatches { get; private set; }
        public int ComputedFrames {get;private set;}
        public int GlobalBindings {get;private set;}
        public int PoseStride { get; private set; }
        public long OwnedGpuBytes => (long)_resolution * _resolution * 4 + ActorBytes()+8;
        public MxDirectionalShadowData Data => _data;
        public ComputeShader Compute => _compute;
        private long ActorBytes() { long bytes = 0; foreach (var actor in _actors) bytes += actor.Bytes; return bytes; }
        // count is the number of buffer elements, not necessarily mesh vertices.
        // Validate the exact byte-addressed Load3 range before importing/binding.
        internal static bool IsPoseReadRangeValid(int bufferCount, int bufferStride,
            int vertexStride, int positionOffset, int maximumVertexIndex)
        {
            if (bufferCount <= 0 || bufferStride <= 0 || vertexStride < 12 ||
                (vertexStride & 3) != 0 || positionOffset < 0 || (positionOffset & 3) != 0 ||
                positionOffset > vertexStride - 12 || maximumVertexIndex < 0) return false;
            long capacity = (long)bufferCount * bufferStride;
            long end = (long)maximumVertexIndex * vertexStride + positionOffset + 12;
            // HLSL ByteAddressBuffer uses uint byte addresses. Avoid arithmetic wrap.
            return end <= capacity && end <= (long)uint.MaxValue + 1;
        }
        internal static Vector4 FitCoverageRectangle(Vector2 minimum,Vector2 maximum,int resolution,
            Vector4 previous,float padding)
        {
            if(!MxDirectionalShadowInfluence.Finite(minimum.x)||!MxDirectionalShadowInfluence.Finite(minimum.y)||
                !MxDirectionalShadowInfluence.Finite(maximum.x)||!MxDirectionalShadowInfluence.Finite(maximum.y)||
                maximum.x<minimum.x||maximum.y<minimum.y||resolution<8)
                throw new ArgumentException("Invalid dynamic shadow coverage.");
            padding=Mathf.Max(0,padding);
            float FitAxis(float span,float oldSize)
            {
                span=Mathf.Max(span+2*padding,(resolution-2)*.001f);
                // Sixteenth-octave buckets limit growth slack to about 4.4%.
                float bucket=Mathf.Pow(2,Mathf.Ceil(Mathf.Log(span,2)*16)/16);
                float oldSpan=oldSize*(resolution-2)/resolution;
                // Grow immediately, but do not rescale on every small pose change.
                if(oldSpan>=bucket&&oldSpan<=bucket*1.2f)bucket=oldSpan;
                return bucket*resolution/(resolution-2);
            }
            float width=FitAxis(maximum.x-minimum.x,previous.z),height=FitAxis(maximum.y-minimum.y,previous.w);
            float stepX=width/resolution,stepY=height/resolution;
            float x=Mathf.Floor((minimum.x-padding-stepX)/stepX)*stepX;
            float y=Mathf.Floor((minimum.y-padding-stepY)/stepY)*stepY;
            if(Mathf.Abs(width-previous.z)<stepX*.001f&&minimum.x-padding>=previous.x+stepX&&
                maximum.x+padding<=previous.x+previous.z-stepX)x=previous.x;
            if(Mathf.Abs(height-previous.w)<stepY*.001f&&minimum.y-padding>=previous.y+stepY&&
                maximum.y+padding<=previous.y+previous.w-stepY)y=previous.y;
            return new Vector4(x,y,width,height);
        }

        public MxDirectionalShadowProvider(MxDirectionalShadowData data, ComputeShader compute, Scene scene, bool editorPreview)
        {
            if (data == null || !data.IsValid) throw new ArgumentException("Bake persistent directional-shadow data first.");
            if (compute == null || !SystemInfo.supportsComputeShaders ||
                !SystemInfo.IsFormatSupported(GraphicsFormat.R32_UInt, GraphicsFormatUsage.LoadStore))
                throw new NotSupportedException("Directional triangle shadows require compute and R32_UInt atomics.");
            _data = data; _compute = compute; _scene = scene; _preview = editorPreview;
            _clearKernel = compute.FindKernel("ClearDepth"); _countKernel=compute.FindKernel("CountCoverage"); _rasterKernel = compute.FindKernel("RasterTriangles");
            _staticDepth = RTHandles.Alloc(data.Depth);
            _peripheralDepth = RTHandles.Alloc(data.PeripheralDepth);
            _status = new GraphicsBuffer(GraphicsBuffer.Target.Structured,2,4);
        }
        public void SetConfiguration(MxSceneRenderingConfiguration configuration)
        {
            _configuration=configuration;
            // Bind during the owning session's Update/OnEnable, before URP performs
            // shadow setup for the first camera; CollectPasses also checks invalidation.
            if(!_preview && _sourceValid && configuration!=null && configuration.DirectionalShadowsEnabled &&
                configuration.Sun!=null && configuration.TargetCamera!=null &&
                Vector3.Dot(-configuration.Sun.transform.forward,_data.Direction)>=.999999f && _ownedSun!=configuration.Sun)
            {RestoreSun();_ownedSun=configuration.Sun;_previousSunShadows=_ownedSun.shadows;_ownedSun.shadows=LightShadows.None;}
        }
        public void SetSourceValidation(bool valid, string issue)
        { _sourceValid = valid; _sourceIssue = issue ?? string.Empty; }

        /// <summary>NoAlloc after camera warmup. Captures the existing receiver union before presentation pose evaluation.</summary>
        public void PreparePresentationPoseDemand()
        {
            _poseDemandPreparedFrame = -1;
            if (_disposed || _configuration == null || !_sourceValid || !DynamicRasterValidationApproved
                || _lastComputedFrame < 0 || _resolution != RequestedResolution || _configuration.Sun == null
                || Vector3.Dot(-_configuration.Sun.transform.forward, _data.Direction) < .999999f) return;
            _receiverScopes.Capture(_configuration, _configuration.TargetCamera, _data, _knownReceiverCameras);
            _poseDemandPreparedFrame = Time.frameCount;
        }

        /// <summary>NoAlloc: unknown deformation, changed roots and uncaptured receivers retain current-pose demand.</summary>
        public bool NeedsCurrentPresentationPose(Renderer renderer)
        {
            if (renderer == null) return true;
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.shadowCastingMode == ShadowCastingMode.Off) return false;
            if (_configuration == null || !_configuration.DirectionalShadowsEnabled || !_configuration.DynamicDirectionalShadows) return true;
            if (_poseDemandPreparedFrame != Time.frameCount || !_poseActors.TryGetValue(renderer, out var actor))
            { ConservativePoseDemands++; return true; }
            var current = ReadPosePublication(renderer);
            bool reliable = current.Known && current.DeformationVersionKnown && actor.PosePublication.Known
                && actor.PosePublication.DeformationVersionKnown && current.Version == actor.PosePublication.Version
                && actor.PoseRootMatrix.Equals(renderer.localToWorldMatrix) && actor.PoseRendererBounds.Equals(renderer.bounds);
            if (!reliable) { ConservativePoseDemands++; return true; }
            bool needed = _receiverScopes.CanAffect(actor.WorldBounds, _data, _poseDemandPadding);
            if (!needed) ReleasedPoseDemands++;
            return needed;
        }

        private MxDirectionalShadowPosePublication ReadPosePublication(Renderer renderer)
        {
            PosePublicationReads++;
            try { return _configuration.ReadDirectionalShadowPosePublication != null
                ? _configuration.ReadDirectionalShadowPosePublication(renderer) : default; }
            catch { return default; }
        }
        public void CollectPasses(IMxRenderPassRegistry registry, in MxCameraRenderContextDescriptor context)
        {
            var c = _configuration;
            if (_disposed || c == null || !c.DirectionalShadowsEnabled || c.Sun == null || c.TargetCamera == null) return;
            if (!_data.IsValid) { Issue="Directional-shadow assets are incomplete or invalid. Rebuild both static regions."; return; }
            Issue = !_sourceValid ? _sourceIssue : !string.IsNullOrEmpty(_casterIssue) ? _casterIssue : _poseIssue;
            if(_sourceValid && c.DynamicDirectionalShadows && !DynamicRasterValidationApproved)
                Issue="Dynamic shadows suspended pending bounded raster validation.";
            if (Vector3.Dot(-c.Sun.transform.forward, _data.Direction) < .999999f)
            { Issue = "Fixed sun direction changed. Rebuild directional shadows."; RestoreCasters(); return; }
            bool game = context.Camera == c.TargetCamera && (context.CameraKind == MxCameraRenderKind.Game || context.CameraKind == MxCameraRenderKind.Unknown);
            bool preview = c.PreviewDirectionalShadowsInSceneView && context.CameraKind == MxCameraRenderKind.SceneView && SceneManager.GetActiveScene() == _scene;
            if (game || preview || MxSceneRenderingAuxiliaryCamera.Matches(context.Camera, c.TargetCamera))
            {
                if(!_knownReceiverCameras.Contains(context.Camera))_knownReceiverCameras.Add(context.Camera);
                if (!_preview && _ownedSun != c.Sun)
                {
                    RestoreSun(); _ownedSun=c.Sun; _previousSunShadows=c.Sun.shadows; c.Sun.shadows=LightShadows.None;
                }
                registry.RegisterPass(this);
            }
        }
        public void Configure(in MxRenderPassConfigureContext context) { }
        public void Execute(in MxRenderPassExecuteContext context) { }
        private void RefreshSources()
        {
            if (Time.realtimeSinceStartupAsDouble < _nextRefresh) return;
            _nextRefresh = Time.realtimeSinceStartupAsDouble + .5;
            _casterIssue=string.Empty;
            for (int i = _actors.Count - 1; i >= 0; i--)
                if (_actors[i].Renderer == null || Actor.SharedMesh(_actors[i].Renderer) != _actors[i].Mesh || _actors[i].Renderer.gameObject.scene != _scene)
                { _actors[i].Dispose(); _actors.RemoveAt(i); }
            if (_configuration.DynamicDirectionalShadows && DynamicRasterValidationApproved)
                foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (renderer.gameObject.scene != _scene || Actor.SharedMesh(renderer) == null ||
                        (renderer is MeshRenderer && _configuration.StaticRoot != null && renderer.transform.IsChildOf(_configuration.StaticRoot))) continue;
                    bool eligible=renderer.shadowCastingMode!=ShadowCastingMode.Off;
                    if(!eligible || (_configuration.DynamicShadowCasterFilter!=null && !_configuration.DynamicShadowCasterFilter(renderer)))continue;
                    bool hasCasterMaterial=false;foreach(var material in renderer.sharedMaterials)
                        if(MxDirectionalShadowCoverage.CanCast(material)){hasCasterMaterial=true;break;}
                    if(!hasCasterMaterial)continue;
                    bool found = false; foreach (var actor in _actors) if (actor.Renderer == renderer) { found = true; break; }
                    if (!found)
                    {
                        try { _actors.Add(new Actor(renderer, !_preview)); }
                        catch (Exception exception) { _casterIssue=renderer.name + ": " + exception.Message; Issue=_casterIssue; }
                    }
                }
            else { foreach (var actor in _actors) actor.Dispose(); _actors.Clear(); }
            _poseActors.Clear();
            foreach (var actor in _actors) if (actor.Renderer != null) _poseActors[actor.Renderer] = actor;
            // Snapshot authored cast flags. Never turn Renderers Off on behalf of
            // this path: the owned sun already disables its URP shadow map.
            if (!_staticCastingStateCaptured && _configuration.StaticRoot != null)
            {
                foreach (var renderer in _configuration.StaticRoot.GetComponentsInChildren<MeshRenderer>(false))
                    _staticModes.Add(renderer, renderer.shadowCastingMode);
                _staticCastingStateCaptured = true;
            }
        }
        private int RequestedResolution => Mathf.Clamp(Mathf.NextPowerOfTwo(_configuration.EffectiveDynamicShadowResolution), 128, 4096);
        private void EnsureTarget()
        {
            int requested = RequestedResolution;
            if (_dynamicDepth != null && _resolution == requested) return;
            // Allocate before releasing the current target so allocation failure cannot expose a half-switched resource.
            var texture = new RenderTexture(requested, requested, 0)
            {
                name = "DirectionalShadows.CurrentPoseDepth", graphicsFormat = GraphicsFormat.R32_UInt,
                enableRandomWrite = true, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            RTHandle replacement;
            try
            {
                if (!texture.Create()) throw new InvalidOperationException("Cannot allocate dynamic shadow depth.");
                replacement = RTHandles.Alloc(texture);
            }
            catch { CoreUtils.Destroy(texture); throw; }
            ReleaseTarget(); _dynamicDepth = replacement; _resolution = requested; _lastComputedFrame = -1;
        }
        public void RecordRenderGraph(RenderGraph graph, ContextContainer frameData, in MxRenderPassConfigureContext context)
        {
            if (frameData.Get<UniversalCameraData>().renderType == CameraRenderType.Overlay) return;
            var cameraData=frameData.Get<UniversalCameraData>();
            // Resolution changes also invalidate reuse between cameras in one frame and while paused.
            bool targetChanged = _dynamicDepth == null || _resolution != RequestedResolution;
            bool newFrame=!Application.isPlaying || _lastComputedFrame!=Time.frameCount || targetChanged;
            // A new/moved late camera cannot reuse a result culled for an earlier
            // scope. One conservative full rebuild covers every later camera.
            bool conservativeRebuild=!newFrame && !_receiverScopes.Unbounded &&
                (!_configuration.CullDirectionalShadowsToReceivers || !_receiverScopes.ContainsUnchanged(cameraData.camera));
            bool computeFrame=newFrame || conservativeRebuild;
            bool dynamicEnabled=DynamicRasterValidationApproved && _configuration.DynamicDirectionalShadows;
            Vector4 rectangle=_frameRectangle;
            if(computeFrame)
            {
            for(int i=_knownReceiverCameras.Count-1;i>=0;i--)if(_knownReceiverCameras[i]==null)_knownReceiverCameras.RemoveAt(i);
            RefreshSources(); EnsureTarget();
            for(int i=0;i<_actors.Count;i++)if(!_actors[i].HasSameMesh())
            { _nextRefresh=0;RefreshSources();break; }
            CandidateCasters=0;CulledCasters=0;PoseBufferAcquisitions=0;CpuPoseFallbacks=0;BoneEnvelopeRebuilds=0;
            if(newFrame)
            {
                ConservativeCameraRebuilds=0;
                _receiverScopes.Capture(_configuration,cameraData.camera,_data,_knownReceiverCameras);
                ReceiverRegionRebuilds=_receiverScopes.Rebuilds;
            }
            else {ConservativeCameraRebuilds++;_receiverScopes.UseUnbounded();}
            _staticCastingStateValid = true;
            foreach (var original in _staticModes)
                if (original.Key == null || original.Key.shadowCastingMode != original.Value)
                { _staticCastingStateValid = false; break; }
            if (_configuration.StaticDirectionalShadowCastingStateValid != null &&
                !_configuration.StaticDirectionalShadowCastingStateValid()) _staticCastingStateValid = false;
            if (!_staticCastingStateValid)
                Issue = "Static Cast Shadows changed: baked visibility disabled until rebaked or restored.";
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity), maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            ActiveCasters = 0;
            foreach (var actor in _actors)
            {
                if(newFrame)actor.ReleasePose();
                actor.ResetFrameCounters();actor.CanCastFrame=false;
                if (!dynamicEnabled || actor.Renderer == null || !actor.Renderer.enabled || !actor.Renderer.gameObject.activeInHierarchy ||
                    actor.Renderer.shadowCastingMode == ShadowCastingMode.Off ||
                    (_configuration.DynamicShadowCasterFilter!=null&&!_configuration.DynamicShadowCasterFilter(actor.Renderer)))
                {actor.ReleasePose();continue;}
                actor.CanCastFrame=true;CandidateCasters++;
                actor.UpdateWorldBounds();BoneEnvelopeRebuilds+=actor.BoneEnvelopeRebuilds;
                actor.PosePublication=ReadPosePublication(actor.Renderer);
                actor.PoseRootMatrix=actor.Renderer.localToWorldMatrix;
                actor.PoseRendererBounds=actor.Renderer.bounds;
                ProjectBounds(actor.WorldBounds,_data.BasisX,_data.BasisY,ref minimum,ref maximum);
            }
            rectangle = _data.Rectangle;
            float staticTexel=Mathf.Max(_data.Rectangle.z/_data.Depth.width,_data.Rectangle.w/_data.Depth.height);
            staticTexel=Mathf.Max(staticTexel,Mathf.Max(_data.PeripheralRectangle.z/_data.PeripheralDepth.width,
                _data.PeripheralRectangle.w/_data.PeripheralDepth.height));
            float fitPadding=Mathf.Max(.1f,Mathf.Max(0,_configuration.DirectionalShadowFilterRadius)+1.5f*staticTexel);
            if (CandidateCasters > 0)
                rectangle=FitCoverageRectangle(minimum,maximum,_resolution,_frameRectangle,fitPadding);
            float dynamicTexel=Mathf.Max(rectangle.z,rectangle.w)/_resolution;
            float texel=Mathf.Max(dynamicTexel,staticTexel);
            // Include bilinear's one-texel support plus the other field's nearest
            // depth-texel centre, otherwise edge casters can be culled too early.
            float rayPadding=Mathf.Max(1.5f*texel,Mathf.Max(0,_configuration.DirectionalShadowFilterRadius)+.5f*texel)+.1f;
            _poseDemandPadding=rayPadding;
            // Fit the light rays that can reach any permitted receiver, including
            // offscreen casters. Culling now returns texel coverage as well as work.
            minimum=new Vector2(float.PositiveInfinity,float.PositiveInfinity);maximum=-minimum;
            bool fitted=false;
            foreach(var actor in _actors)
            {
                if(!actor.CanCastFrame)continue;
                if(!_receiverScopes.TryClip(actor.WorldBounds,_data,rayPadding,out var lo,out var hi))
                {actor.ReleasePose();actor.CanCastFrame=false;CulledCasters++;continue;}
                minimum=Vector2.Min(minimum,lo);maximum=Vector2.Max(maximum,hi);fitted=true;
            }
            if(fitted)rectangle=FitCoverageRectangle(minimum,maximum,_resolution,_frameRectangle,fitPadding);
            foreach(var actor in _actors)
            {
                if(!actor.CanCastFrame)continue;
                try
                {
                    if(actor.Pose==null)actor.Pose = actor.GetVertexBuffer();
                    PoseBufferAcquisitions+=actor.PoseBufferAcquisitions;
                    if (actor.Pose == null || !actor.Pose.IsValid())
                        throw new InvalidOperationException("Current-pose buffer is not valid.");
                    // Requested renderer targets do not describe the returned resource:
                    // CPU skinning can return a Vertex-only buffer even after requesting Raw.
                    if((actor.Pose.target&GraphicsBuffer.Target.Raw)==0)
                        throw new NotSupportedException("Current-pose buffer cannot be bound as ByteAddressBuffer (Raw target required).");
                    if (!IsPoseReadRangeValid(actor.Pose.count, actor.Pose.stride, actor.Stride,
                        actor.PositionOffset, actor.MaximumVertexIndex))
                        throw new NotSupportedException("Current-pose float3 reads exceed the buffer capacity or byte-addressed layout.");
                    if(actor.UsesCpuPose)CpuPoseFallbacks++;
                    PoseStride = actor.Stride; ActiveCasters++;
                }
                catch (Exception exception) { actor.ReleasePose(); Issue = actor.Renderer.name + ": " + exception.Message; }
            }
            _frameRectangle=rectangle;_lastComputedFrame=dynamicEnabled?Time.frameCount:-1;
            }
            TextureHandle dynamic = graph.ImportTexture(_dynamicDepth), baked = graph.ImportTexture(_staticDepth);
            TextureHandle peripheral=graph.ImportTexture(_peripheralDepth);
            using (var builder = graph.AddUnsafePass<PassData>(DebugName, out var pass,_sampler))
            {
                pass.Owner = this; pass.Dynamic = dynamic; pass.Static = baked; pass.Rectangle = rectangle; pass.Compute=computeFrame && dynamicEnabled;
                builder.UseBuffer(graph.ImportBuffer(_status),AccessFlags.ReadWrite);
                if(computeFrame)foreach (var actor in _actors)
                    if (actor.Pose != null)
                    {
                        builder.UseBuffer(graph.ImportBuffer(actor.Pose), AccessFlags.Read);
                        builder.UseBuffer(graph.ImportBuffer(actor.Indices), AccessFlags.Read);
                        builder.UseBuffer(graph.ImportBuffer(actor.Attributes), AccessFlags.Read);
                    }
                builder.UseTexture(dynamic, AccessFlags.ReadWrite); builder.UseTexture(baked, AccessFlags.Read);
                builder.UseTexture(peripheral,AccessFlags.Read);
                builder.AllowGlobalStateModification(true); builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData p, UnsafeGraphContext ctx) =>
                    p.Owner.Render(CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd), p));
            }
        }
        private void Render(CommandBuffer cmd, PassData pass)
        {
            Vector4 rectangle = pass.Rectangle;
            Vector4 uvRectangle = new Vector4(rectangle.x, rectangle.y, 1 / rectangle.z, 1 / rectangle.w);
            Vector4 range = _data.DepthRange;
            GlobalBindings++;
            // A recorded pass must also respect a switch changed before execution.
            bool dynamicEnabled=DynamicRasterValidationApproved && _configuration.DynamicDirectionalShadows;
            if(!dynamicEnabled)_lastComputedFrame=-1;
            if(pass.Compute && dynamicEnabled)
            {
            ComputedFrames++;
            _coverageEpoch++;MaterialCoverageReads=0;CoverageOverrideReads=0;CoverageBindingChanges=0;_hasLastCoverage=false;
            if(_materialCoverage.Count>512)_materialCoverage.Clear();
            long totalTriangles=0;
            foreach(var actor in _actors)
            {
                if(actor.Pose==null || actor.Renderer==null)continue;
                // Freeze admission and coverage once. Budgeting and both GPU stages
                // consume these same ranges, even if a material is changed later.
                totalTriangles+=actor.SnapshotCastingSubMeshes();
                bool hasOverrides=actor.Renderer.HasPropertyBlock();
                if(hasOverrides)actor.Renderer.GetPropertyBlock(actor.ObjectProperties);else actor.ObjectProperties.Clear();
                for(int sub=0;sub<actor.Starts.Length;sub++)
                {
                    if(!actor.CastingSubMeshes[sub])continue;
                    if(hasOverrides)actor.Renderer.GetPropertyBlock(actor.SubProperties,sub);else actor.SubProperties.Clear();
                    actor.FrameCoverage[sub]=ReadCoverage(actor.Materials[sub],actor.ObjectProperties,actor.SubProperties);
                }
            }
            bool triangleOverflow=totalTriangles>MaximumTrianglesPerFrame;
            cmd.SetComputeIntParam(_compute, "_Resolution", _resolution);
            cmd.SetComputeIntParam(_compute,"_ExceededTriangleBudget",triangleOverflow?1:0);
            cmd.SetComputeIntParam(_compute,"_PixelBudget",MaximumCandidatePixelsPerFrame);
            cmd.SetComputeTextureParam(_compute, _clearKernel, "_Depth", _dynamicDepth);
            cmd.SetComputeBufferParam(_compute,_clearKernel,"_Status",_status);
            cmd.DispatchCompute(_compute, _clearKernel, (_resolution + 7) / 8, (_resolution + 7) / 8, 1);
            // Count every caster before rasterizing any caster. A failed frame remains
            // completely empty rather than accepting truncated/partial silhouettes.
            if(!triangleOverflow)foreach(var actor in _actors)
            {
                if(actor.Pose==null || actor.Renderer==null)continue;
                cmd.SetComputeBufferParam(_compute,_countKernel,"_Pose",actor.Pose);
                cmd.SetComputeBufferParam(_compute,_countKernel,"_Indices",actor.Indices);
                cmd.SetComputeBufferParam(_compute,_countKernel,"_Status",_status);
                BindPose(cmd,actor,uvRectangle,range);
                for(int sub=0;sub<actor.Starts.Length;sub++)
                {
                    if(!actor.CastingSubMeshes[sub])continue;
                    int triangles=actor.Counts[sub]/3;
                    cmd.SetComputeIntParam(_compute,"_IndexStart",actor.Starts[sub]);
                    cmd.SetComputeIntParam(_compute,"_TriangleCount",triangles);
                    cmd.DispatchCompute(_compute,_countKernel,(triangles+63)/64,1,1);
                }
            }
            TriangleDispatches = 0;
            foreach (var actor in _actors)
            {
                if (triangleOverflow || actor.Pose == null || actor.Renderer == null) continue;
                cmd.SetComputeBufferParam(_compute, _rasterKernel, "_Pose", actor.Pose);
                cmd.SetComputeBufferParam(_compute, _rasterKernel, "_Indices", actor.Indices);
                cmd.SetComputeBufferParam(_compute, _rasterKernel, "_Attributes", actor.Attributes);
                cmd.SetComputeTextureParam(_compute, _rasterKernel, "_Depth", _dynamicDepth);
                cmd.SetComputeBufferParam(_compute,_rasterKernel,"_Status",_status);
                BindPose(cmd,actor,uvRectangle,range);
                for (int sub = 0; sub < actor.Starts.Length; sub++)
                {
                    if(!actor.CastingSubMeshes[sub])continue;
                    BindCoverage(cmd,actor.FrameCoverage[sub]);
                    cmd.SetComputeIntParam(_compute, "_IndexStart", actor.Starts[sub]);
                    cmd.SetComputeIntParam(_compute, "_TriangleCount", actor.Counts[sub] / 3);
                    int triangles=actor.Counts[sub]/3;
                    // Admission for this complete shadow update bounds pixel work.
                    // Split only for D3D11's dimensional group limit.
                    int groupBudget=65535;
                    for(int start=0;start<triangles;start+=groupBudget)
                    {
                        cmd.SetComputeIntParam(_compute,"_TriangleStart",start);
                        cmd.DispatchCompute(_compute,_rasterKernel,Math.Min(groupBudget,triangles-start),1,1);
                        TriangleDispatches++;
                    }
                }
            }
            }
            Vector4 baked = _data.Rectangle;
            cmd.SetGlobalTexture(MxDirectionalShadowIds.StaticDepth, _data.Depth);
            cmd.SetGlobalTexture(MxDirectionalShadowIds.PeripheralDepth,_data.PeripheralDepth);
            cmd.SetGlobalTexture(MxDirectionalShadowIds.DynamicDepth, _dynamicDepth);
            cmd.SetGlobalBuffer("_MxDirectionalShadowStatus",_status);
            cmd.SetGlobalVector(MxDirectionalShadowIds.BasisX, _data.BasisX); cmd.SetGlobalVector(MxDirectionalShadowIds.BasisY, _data.BasisY);
            cmd.SetGlobalVector(MxDirectionalShadowIds.Direction, _data.Direction);
            cmd.SetGlobalVector(MxDirectionalShadowIds.StaticRect, new Vector4(baked.x, baked.y, 1 / baked.z, 1 / baked.w));
            var outer=_data.PeripheralRectangle;
            cmd.SetGlobalVector(MxDirectionalShadowIds.PeripheralRect,new Vector4(outer.x,outer.y,1/outer.z,1/outer.w));
            cmd.SetGlobalVector(MxDirectionalShadowIds.DynamicRect, uvRectangle);
            cmd.SetGlobalVector(MxDirectionalShadowIds.Range, new Vector4(range.x, 1 / range.y,
                Mathf.Max(0, _configuration.DirectionalShadowBias), Mathf.Max(0, _configuration.DirectionalShadowFilterRadius)));
            bool staticValid = _sourceValid && _staticCastingStateValid;
            cmd.SetGlobalVector(MxDirectionalShadowIds.Size, new Vector4(staticValid ? _data.Depth.width : 0,
                staticValid ? _data.Depth.height : 0, _resolution, dynamicEnabled && ActiveCasters > 0 ? 1 : 0));
            cmd.SetGlobalVector(MxDirectionalShadowIds.PeripheralSize,new Vector4(staticValid?_data.PeripheralDepth.width:0,
                staticValid?_data.PeripheralDepth.height:0,_data.TransitionWidth,0));
            cmd.SetGlobalVector(MxDirectionalShadowIds.StaticCoverage, staticValid ? _data.PlaneCoverage : Vector4.zero);
            cmd.SetGlobalFloat(MxDirectionalShadowIds.Enabled, 1);
            if(dynamicEnabled && ActiveCasters>0 && !_statusReadPending && Time.realtimeSinceStartupAsDouble>=_nextStatusRead)
            {
                _statusReadPending=true;_nextStatusRead=Time.realtimeSinceStartupAsDouble+.5;
                cmd.RequestAsyncReadback(_status,request=>
                {
                    _statusReadPending=false;if(_disposed)return;
                    if(request.hasError){_poseIssue="Dynamic shadow status readback failed.";return;}
                    var state=request.GetData<uint>();uint failure=state[0];CandidatePixels=state[1];
                    _poseIssue=(failure&1)!=0?"Invalid/out-of-bounds current pose: dynamic visibility disabled. Validate model bounds and pose buffer.":
                        (failure&2)!=0?"Dynamic coverage exceeds the 8M candidate-pixel shadow-update budget; dynamic visibility disabled, with no partial silhouette.":
                        (failure&4)!=0?"Dynamic casters exceed the 262144-triangle shadow-update budget; dynamic visibility disabled.":string.Empty;
                });
            }
        }
        private void BindPose(CommandBuffer cmd,Actor actor,Vector4 rectangle,Vector4 range)
        {
            Bounds bounds=actor.WorldBounds;bounds.Expand(.2f);
            cmd.SetComputeVectorParam(_compute,"_BoundsMin",bounds.min);cmd.SetComputeVectorParam(_compute,"_BoundsMax",bounds.max);
            cmd.SetComputeIntParam(_compute,"_Stride",actor.Stride);cmd.SetComputeIntParam(_compute,"_PositionOffset",actor.PositionOffset);
            cmd.SetComputeMatrixParam(_compute,"_ObjectToWorld",actor.PoseToWorld);
            cmd.SetComputeVectorParam(_compute,"_BasisX",_data.BasisX);cmd.SetComputeVectorParam(_compute,"_BasisY",_data.BasisY);
            cmd.SetComputeVectorParam(_compute,"_LightDirection",_data.Direction);cmd.SetComputeVectorParam(_compute,"_Rectangle",rectangle);
            cmd.SetComputeVectorParam(_compute,"_Range",new Vector4(range.x,1/range.y,0,0));
        }
        private MxDirectionalShadowCoverage ReadCoverage(Material material, MaterialPropertyBlock objectBlock, MaterialPropertyBlock subBlock)
        {
            if(!_materialCoverage.TryGetValue(material,out var entry))
            {entry=new MaterialCoverageEntry();_materialCoverage.Add(material,entry);}
            // Re-read live values/keywords at every compute epoch: shared material
            // reads are deduplicated, never frozen behind an unverifiable hash.
            if(entry.Epoch!=_coverageEpoch)
            {entry.Coverage=MxDirectionalShadowCoverage.Read(material);entry.Epoch=_coverageEpoch;MaterialCoverageReads++;}
            bool overrides=(objectBlock!=null&&!objectBlock.isEmpty)||(subBlock!=null&&!subBlock.isEmpty);
            var coverage=overrides?MxDirectionalShadowCoverage.ApplyOverrides(material,entry.Coverage,objectBlock,subBlock):entry.Coverage;
            if(overrides)CoverageOverrideReads++;
            return coverage;
        }
        private void BindCoverage(CommandBuffer cmd,MxDirectionalShadowCoverage coverage)
        {
            if(_hasLastCoverage && SameCoverage(_lastCoverage,coverage))return;
            _hasLastCoverage=true;_lastCoverage=coverage;CoverageBindingChanges++;
            cmd.SetComputeIntParam(_compute, "_CoverageMode", coverage.Mode);
            cmd.SetComputeVectorParam(_compute, "_AlphaParameters", new Vector4(coverage.Alpha, coverage.Cutoff,0,0));
            cmd.SetComputeTextureParam(_compute, _rasterKernel, "_CoverageTexture", coverage.Mask);
            cmd.SetComputeTextureParam(_compute, _rasterKernel, "_DissolveTexture", coverage.Noise);
            cmd.SetComputeFloatParam(_compute, "_DissolveProgress", coverage.Dissolve);
            cmd.SetComputeIntParam(_compute, "_DissolveMode", coverage.DissolveMode);
            cmd.SetComputeVectorParam(_compute, "_BaseMapST", coverage.BaseMapST);
        }
        private static bool SameCoverage(MxDirectionalShadowCoverage a,MxDirectionalShadowCoverage b)=>
            a.Mode==b.Mode&&a.DissolveMode==b.DissolveMode&&a.Alpha==b.Alpha&&a.Cutoff==b.Cutoff&&
            a.Dissolve==b.Dissolve&&a.BaseMapST.Equals(b.BaseMapST)&&a.Mask==b.Mask&&a.Noise==b.Noise;
        private static void ProjectBounds(Bounds bounds, Vector3 x, Vector3 y, ref Vector2 minimum, ref Vector2 maximum)
        {
            Vector3 c = bounds.center, e = bounds.extents;
            Vector2 centre = new Vector2(Vector3.Dot(c,x), Vector3.Dot(c,y));
            Vector2 extent = new Vector2(Mathf.Abs(x.x)*e.x+Mathf.Abs(x.y)*e.y+Mathf.Abs(x.z)*e.z, Mathf.Abs(y.x)*e.x+Mathf.Abs(y.y)*e.y+Mathf.Abs(y.z)*e.z);
            minimum = Vector2.Min(minimum, centre-extent); maximum = Vector2.Max(maximum, centre+extent);
        }
        private void ReleaseTarget() { if (_dynamicDepth == null) return; var rt = _dynamicDepth.rt; _dynamicDepth.Release(); CoreUtils.Destroy(rt); _dynamicDepth = null; _resolution = 0; }
        private void RestoreSun()
        { if(_ownedSun!=null)_ownedSun.shadows=_previousSunShadows;_ownedSun=null; }
        private void RestoreCasters()
        {
            RestoreSun();_lastComputedFrame=-1;
            foreach (var actor in _actors) actor.Dispose(); _actors.Clear(); _nextRefresh=0;
            _poseActors.Clear(); _poseDemandPreparedFrame=-1;
            _staticModes.Clear();
            _staticCastingStateCaptured = false;
            _knownReceiverCameras.Clear();_materialCoverage.Clear();
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _sampler?.Dispose();
            RestoreCasters(); ReleaseTarget(); _staticDepth.Release(); _peripheralDepth.Release(); _status.Dispose();
        }
        private sealed class PassData { internal MxDirectionalShadowProvider Owner; internal TextureHandle Dynamic, Static; internal Vector4 Rectangle; internal bool Compute; }
        private sealed class Actor : IDisposable
        {
            internal readonly Renderer Renderer;
            internal readonly SkinnedMeshRenderer Skinned;
            internal readonly Mesh Mesh;
            internal readonly GraphicsBuffer Indices, Attributes;
            internal GraphicsBuffer Pose;
            private GraphicsBuffer _rigidPose;
            private GraphicsBuffer _cpuPose;
            private Mesh _bakedPose;
            private List<Vector3> _posePositions;
            internal bool UsesCpuPose {get;private set;}
            internal Matrix4x4 PoseToWorld;
            private readonly MeshFilter _meshFilter;
            internal bool CanCastFrame;
            internal int PoseBufferAcquisitions {get;private set;}
            internal int BoneEnvelopeRebuilds {get;private set;}
            internal void ResetFrameCounters(){PoseBufferAcquisitions=0;BoneEnvelopeRebuilds=0;}
            internal int Stride, PositionOffset;
            internal Bounds WorldBounds;
            internal MxDirectionalShadowPosePublication PosePublication;
            internal Matrix4x4 PoseRootMatrix;
            internal Bounds PoseRendererBounds;
            private readonly Transform[] _bones;
            private readonly Bounds[] _influenceBounds;
            private readonly bool[] _hasInfluence;
            private readonly Matrix4x4[] _boneMatrices;
            private readonly Bounds[] _boneWorldBounds;
            private readonly bool[] _boneMatrixValid;
            internal readonly int[] Starts, Counts;
            internal readonly bool[] CastingSubMeshes;
            internal readonly MxDirectionalShadowCoverage[] FrameCoverage;
            internal readonly int MaximumVertexIndex;
            internal int SnapshotCastingSubMeshes()
            {
                Renderer.GetSharedMaterials(Materials);
                bool rendererCasts=Renderer.enabled && Renderer.gameObject.activeInHierarchy && Renderer.shadowCastingMode!=ShadowCastingMode.Off;
                int triangles=0;
                for(int sub=0;sub<Starts.Length;sub++)
                {
                    bool casts=rendererCasts && Counts[sub]>0 && sub<Materials.Count && MxDirectionalShadowCoverage.CanCast(Materials[sub]);
                    CastingSubMeshes[sub]=casts;
                    if(casts)triangles+=Counts[sub]/3;
                }
                return triangles;
            }
            internal readonly MaterialPropertyBlock ObjectProperties = new MaterialPropertyBlock(), SubProperties = new MaterialPropertyBlock();
            internal readonly List<Material> Materials = new List<Material>();
            internal void UpdateWorldBounds()
            {
                WorldBounds=Renderer.bounds;
                if(_bones==null)return;
                for(int i=0;i<_bones.Length;i++)
                {
                    if(!_hasInfluence[i] || _bones[i]==null)continue;
                    var matrix=_bones[i].localToWorldMatrix;
                    if(!_boneMatrixValid[i] || !_boneMatrices[i].Equals(matrix))
                    {
                        var local=_influenceBounds[i];Vector3 e=local.extents;
                        Vector3 extent=new Vector3(Mathf.Abs(matrix.m00)*e.x+Mathf.Abs(matrix.m01)*e.y+Mathf.Abs(matrix.m02)*e.z,
                            Mathf.Abs(matrix.m10)*e.x+Mathf.Abs(matrix.m11)*e.y+Mathf.Abs(matrix.m12)*e.z,
                            Mathf.Abs(matrix.m20)*e.x+Mathf.Abs(matrix.m21)*e.y+Mathf.Abs(matrix.m22)*e.z);
                        _boneWorldBounds[i]=new Bounds(matrix.MultiplyPoint3x4(local.center),extent*2);
                        _boneMatrices[i]=matrix;_boneMatrixValid[i]=true;BoneEnvelopeRebuilds++;
                    }
                    WorldBounds.Encapsulate(_boneWorldBounds[i]);
                }
            }
            private readonly GraphicsBuffer.Target _previousTarget;
            private sealed class MeshTargetLease {internal int References;internal GraphicsBuffer.Target Previous;}
            private static readonly Dictionary<Mesh,MeshTargetLease> MeshTargets=new Dictionary<Mesh,MeshTargetLease>();
            private readonly bool _previousOffscreen, _ownsSettings;
            internal long Bytes => (long)Indices.count * 4 + (long)Attributes.count * 16 +
                (_cpuPose==null?0:(long)_cpuPose.count*_cpuPose.stride);
            internal static Mesh SharedMesh(Renderer renderer)
            {
                var skinned=renderer as SkinnedMeshRenderer;if(skinned!=null)return skinned.sharedMesh;
                var filter=renderer is MeshRenderer?renderer.GetComponent<MeshFilter>():null;return filter!=null?filter.sharedMesh:null;
            }
            internal bool HasSameMesh()=>Renderer!=null && (Skinned!=null?Skinned.sharedMesh:_meshFilter!=null?_meshFilter.sharedMesh:null)==Mesh;
            internal GraphicsBuffer GetVertexBuffer()
            {
                UsesCpuPose=false;
                PositionOffset=Mesh.GetVertexAttributeOffset(VertexAttribute.Position);
                PoseToWorld=Skinned!=null&&Skinned.rootBone!=null?Skinned.rootBone.localToWorldMatrix:Renderer.localToWorldMatrix;
                if(Skinned!=null)
                {
                    // Edit-mode deformation and Play/domain transitions do not provide
                    // a stable native pose-buffer contract. Own the preview snapshot.
                    if(!Application.isPlaying)return GetCpuPose();
                    PoseBufferAcquisitions++;
                    var buffer=Skinned.GetVertexBuffer();
                    if(buffer!=null&&buffer.IsValid()&&(buffer.target&GraphicsBuffer.Target.Raw)!=0)
                    {Stride=buffer.stride;return buffer;}
                    buffer?.Dispose();
                    return GetCpuPose();
                }
                // Rigid mesh data is immutable by contract; replacements are
                // detected before use. Never retain a skinned rotating buffer.
                if(_rigidPose==null||!_rigidPose.IsValid())
                {_rigidPose?.Dispose();_rigidPose=Mesh.GetVertexBuffer(Mesh.GetVertexAttributeStream(VertexAttribute.Position));PoseBufferAcquisitions++;}
                if(_rigidPose==null||!_rigidPose.IsValid()||(_rigidPose.target&GraphicsBuffer.Target.Raw)==0)
                    return GetCpuPose();
                Stride=_rigidPose.stride;
                return _rigidPose;
            }
            private GraphicsBuffer GetCpuPose()
            {
                // BakeMesh returns renderer-local positions, unlike the native skinned
                // buffer's root-bone space. Reuse both the snapshot and upload storage.
                bool upload=Skinned!=null||_cpuPose==null;
                if(_posePositions==null)_posePositions=new List<Vector3>(Mesh.vertexCount);
                if(Skinned!=null)
                {
                    if(_bakedPose==null)_bakedPose=new Mesh{name="DirectionalShadows.CpuPose",hideFlags=HideFlags.HideAndDontSave};
                    // Unity 6.6's true option compensates the Transform scale; the
                    // renderer matrix below applies that scale exactly once.
                    Skinned.BakeMesh(_bakedPose,true);
                    _bakedPose.GetVertices(_posePositions);
                }
                else if(upload)Mesh.GetVertices(_posePositions);
                if(_posePositions.Count!=Mesh.vertexCount)
                    throw new NotSupportedException("CPU pose vertex count changed; recreate the shadow caster.");
                if(_cpuPose==null)_cpuPose=new GraphicsBuffer(GraphicsBuffer.Target.Raw,checked(Mesh.vertexCount*3),4);
                if(upload)_cpuPose.SetData(_posePositions);
                UsesCpuPose=true;Stride=12;PositionOffset=0;PoseToWorld=Renderer.localToWorldMatrix;
                return _cpuPose;
            }
            internal Actor(Renderer renderer, bool ownsSettings)
            {
                Renderer = renderer; Skinned=renderer as SkinnedMeshRenderer; Mesh = SharedMesh(renderer); _ownsSettings = ownsSettings;
                _meshFilter=Skinned==null?renderer.GetComponent<MeshFilter>():null;
                // Editor access can succeed without Read/Write, hiding a Player failure.
                if(!Mesh.isReadable)throw new NotSupportedException("Dynamic shadow mesh requires Read/Write Enabled: "+Mesh.name);
                if(Mesh.GetVertexAttributeFormat(VertexAttribute.Position)!=VertexAttributeFormat.Float32 ||
                    Mesh.GetVertexAttributeDimension(VertexAttribute.Position)!=3)
                    throw new NotSupportedException("Current-pose position layout requires float32x3.");
                PositionOffset=Mesh.GetVertexAttributeOffset(VertexAttribute.Position);
                _previousTarget = Skinned!=null?Skinned.vertexBufferTarget:Mesh.vertexBufferTarget; _previousOffscreen = Skinned!=null && Skinned.updateWhenOffscreen;
                if(Skinned!=null)
                {
                    // AllocByDesign during Actor admission only. Cloth is an optional Unity module,
                    // so detecting its attached component must not add a mandatory assembly reference.
                    bool hasVertexCloth=false;
                    foreach(Component component in renderer.GetComponents<Component>())
                        if(component!=null && component.GetType().FullName=="UnityEngine.Cloth")
                        {hasVertexCloth=true;break;}
                    if(Mesh.blendShapeCount>0 || hasVertexCloth)
                        throw new NotSupportedException("Validate blend-shape/vertex-cloth displacement bounds before admitting this caster.");
                    _bones=Skinned.bones;_influenceBounds=new Bounds[_bones.Length];_hasInfluence=new bool[_bones.Length];
                    _boneMatrices=new Matrix4x4[_bones.Length];_boneWorldBounds=new Bounds[_bones.Length];_boneMatrixValid=new bool[_bones.Length];
                    var bindposes=Mesh.bindposes;var positions=Mesh.vertices;
                    var counts=Mesh.GetBonesPerVertex();var weights=Mesh.GetAllBoneWeights();int cursor=0;
                    if(counts.Length!=positions.Length || bindposes.Length!=_bones.Length)
                        throw new NotSupportedException("Caster skin influence layout does not match its bones.");
                    // One-time bind-space influence envelopes. Any normalized positive
                    // skinning blend lies in the union of the transformed bone envelopes.
                    // Bounds only transform these boxes. A non-Raw pose uses a separate
                    // CPU snapshot after receiver culling, before compute dispatch.
                    for(int vertex=0;vertex<positions.Length;vertex++)for(int influence=0;influence<counts[vertex];influence++)
                    {
                        var weight=weights[cursor++];if(weight.weight<=0)continue;int bone=weight.boneIndex;
                        if(bone<0 || bone>=_bones.Length || float.IsNaN(weight.weight) || float.IsInfinity(weight.weight))
                            throw new NotSupportedException("Caster has invalid skin weights.");
                        Vector3 point=bindposes[bone].MultiplyPoint3x4(positions[vertex]);
                        if(!_hasInfluence[bone]){_influenceBounds[bone]=new Bounds(point,Vector3.zero);_hasInfluence[bone]=true;}
                        else _influenceBounds[bone].Encapsulate(point);
                    }
                }
                int vertexCount=Mesh.vertexCount, maximumIndex=-1;
                var indices = new List<uint>(); Starts = new int[Mesh.subMeshCount]; Counts = new int[Mesh.subMeshCount];
                CastingSubMeshes=new bool[Mesh.subMeshCount];FrameCoverage=new MxDirectionalShadowCoverage[Mesh.subMeshCount];
                for (int sub = 0; sub < Starts.Length; sub++)
                {
                    if (Mesh.GetTopology(sub) != MeshTopology.Triangles) throw new NotSupportedException("Shadow caster must use triangle topology.");
                    Starts[sub] = indices.Count; int[] source = Mesh.GetIndices(sub, true); Counts[sub] = source.Length;
                    foreach (int index in source)
                    {
                        if(index<0 || index>=vertexCount)
                            throw new ArgumentException("Shadow caster index is outside its vertex data.");
                        maximumIndex=Math.Max(maximumIndex,index); indices.Add((uint)index);
                    }
                }
                MaximumVertexIndex=maximumIndex;
                Vector2[] uv = Mesh.uv; Color[] colors = Mesh.colors; var attributes = new Vector4[vertexCount];
                for (int i = 0; i < attributes.Length; i++) attributes[i] = new Vector4(i < uv.Length ? uv[i].x : 0, i < uv.Length ? uv[i].y : 0, i < colors.Length ? colors[i].a : 1, 0);
                if(indices.Count==0 || attributes.Length==0)throw new ArgumentException("Shadow caster has no triangle vertices.");
                try
                {
                    Indices = new GraphicsBuffer(GraphicsBuffer.Target.Structured,indices.Count,4); Indices.SetData(indices.ToArray());
                    Attributes = new GraphicsBuffer(GraphicsBuffer.Target.Structured,attributes.Length,16); Attributes.SetData(attributes);
                }
                catch { Indices?.Dispose(); Attributes?.Dispose(); throw; }
                if(Skinned!=null)
                {
                    if(Application.isPlaying)Skinned.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
                }
                else
                {
                    if(!MeshTargets.TryGetValue(Mesh,out var lease)){lease=new MeshTargetLease{Previous=Mesh.vertexBufferTarget};MeshTargets.Add(Mesh,lease);}
                    lease.References++;Mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
                }
                if (ownsSettings && Skinned!=null) Skinned.updateWhenOffscreen = true;
            }
            internal void ReleasePose() { if(Skinned!=null&&Pose!=_cpuPose)Pose?.Dispose(); Pose = null; }
            public void Dispose()
            {
                ReleasePose();_rigidPose?.Dispose();_rigidPose=null;Indices.Dispose(); Attributes.Dispose();
                _cpuPose?.Dispose();_cpuPose=null;CoreUtils.Destroy(_bakedPose);_bakedPose=null;
                if(Skinned!=null)Skinned.vertexBufferTarget=_previousTarget;
                else if(!ReferenceEquals(Mesh,null) && MeshTargets.TryGetValue(Mesh,out var lease))
                {if(--lease.References==0){if(Mesh!=null)Mesh.vertexBufferTarget=lease.Previous;MeshTargets.Remove(Mesh);}}
                if (Renderer != null && _ownsSettings && Skinned!=null) Skinned.updateWhenOffscreen = _previousOffscreen;
            }
        }
    }
}
