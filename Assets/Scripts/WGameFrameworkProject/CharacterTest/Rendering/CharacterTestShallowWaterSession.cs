using System;
using System.Collections.Generic;
using MxFramework.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Scene-owned water presentation session; no authored water GameObject or gameplay authority.</summary>
    internal sealed partial class CharacterTestShallowWaterSession : IDisposable
    {
        internal GameManager Owner { get; }
        internal CharacterTestShallowWaterConfiguration Configuration { get; }
        internal CharacterTestShallowWaterSettings Settings => Configuration.RuntimeSettingsOverride != null ? Configuration.RuntimeSettingsOverride : Configuration.Settings;
        internal bool SurfaceEnabled => Configuration.SurfaceEnabled;
        internal bool ReflectionEnabled => Configuration.ReflectionEnabled && Settings.UsePlanarReflection && Settings.ReflectionStrength > 0;
        internal bool RipplesEnabled => Configuration.RipplesEnabled;
        internal bool IsActive => !_disposed && Owner != null && Owner.isActiveAndEnabled;
        internal Vector3 Position => Configuration.Position;
        internal Quaternion Rotation => Quaternion.Euler(0, Configuration.Yaw, 0);
        internal Matrix4x4 LocalToWorld => Matrix4x4.TRS(Position, Rotation, Vector3.one);
        private UnityCamera _source, _reflection;
        private MxSceneRenderingAuxiliaryCamera _auxiliary;
        private UniversalAdditionalCameraData _reflectionData;
        private UniversalAdditionalCameraData _sourceData;
        private RTHandle _reflectionTarget;
        private RTHandle _reflectionColorView;
        private Mesh _mesh;
        private Material _material;
        private SurfacePass _pass;
        private IMxRenderPipeline _pipeline;
        private CharacterTestPresentationLod _lod;
        private int _width, _height;
        private bool _disposed;
        private bool _reflectionWanted, _reflectionValid, _renderRequestInProgress;
        private WaterReflectionSchedule _reflectionSchedule;
        private int _reflectionBudget;
        private int _renderContextDepth;
        public UnityCamera ReflectionCamera => _reflection;
        public RenderTexture ReflectionTexture => _reflectionTarget?.rt;
        public int SurfaceDraws { get; private set; }
        public int SurfaceColorCopies { get; private set; }
        public int ReflectionRenders { get; private set; }
        public bool ReflectionReady => _reflectionValid && ReflectionTexture != null && ReflectionTexture.IsCreated();
        public string Issue { get; private set; } = string.Empty;
        public long ReflectionBytes => ReflectionTexture != null
            ? (long)_width * _height * (8 + GraphicsFormatUtility.GetBlockSize(ReflectionTexture.depthStencilFormat)) : 0;
        private Matrix4x4 _reflectionViewProjection;
        private readonly Plane[] _frustum = new Plane[6];
        private double _clock;
        internal double Clock => Application.isPlaying ? _clock : Time.timeAsDouble;
        internal CharacterTestShallowWaterSession(GameManager owner, CharacterTestShallowWaterConfiguration configuration)
        {
            Owner = owner; Configuration = configuration; _clock = Time.timeAsDouble;
            RenderPipelineManager.beginContextRendering += BeginContext;
            RenderPipelineManager.endContextRendering += EndContext;
            RenderPipelineManager.endCameraRendering += EndCamera;
            PrepareView(); BindInteractions(); TickSplashes();
        }
        internal void Tick()
        {
            if (Application.isPlaying && Owner?.RuntimeContext?.Paused != true) _clock += Time.deltaTime;
            PrepareView(); BindInteractions(); TickSplashes();
        }
        private void BeginContext(ScriptableRenderContext context, List<UnityCamera> cameras) => _renderContextDepth++;
        private void EndContext(ScriptableRenderContext context, List<UnityCamera> cameras) => _renderContextDepth = Mathf.Max(0, _renderContextDepth - 1);
        private void EndCamera(ScriptableRenderContext context, UnityCamera camera)
        {
            if (camera != _reflection) { EndSceneReflection(camera); return; }
            ReflectionRenders++;
            _reflectionValid = ReflectionTexture != null && ReflectionTexture.IsCreated() && camera.targetTexture == ReflectionTexture;
            _reflectionSchedule.Record(_source, Time.realtimeSinceStartupAsDouble);
        }
        /// <summary>NoAlloc after camera/RT warmup. Also called before presentation pose admission.</summary>
        public void PrepareView()
        {
            if (!IsActive) return;
            if (Owner == null || Settings == null) { SuspendView("Assign the scene owner and water settings."); return; }
            _ripples.Expire((float)Clock, Settings.RippleLifetime);
            var config = Owner.SceneRendering;
            if (config?.Feature == null || !config.Feature.isActive || config.TargetCamera == null || Settings.SurfaceShader == null)
            { SuspendView("Assign scene renderer, source camera and the shallow-water shader."); return; }
            if (_source != config.TargetCamera) { ReleaseTarget(); _source = config.TargetCamera; _source.TryGetComponent(out _sourceData); }
            if (_sourceData == null) { SuspendView("Source camera requires its existing URP camera data."); return; }
            if (!_source.isActiveAndEnabled || _sourceData.renderType != CameraRenderType.Base)
            { SuspendView("Source camera must be an active URP Base camera."); return; }
            if (_source.gameObject.scene != Owner.gameObject.scene) { SuspendView("Source camera and water must share one scene."); return; }
            if (_material == null || _material.shader != Settings.SurfaceShader)
            { DestroyOwned(_material); _material = new Material(Settings.SurfaceShader) { hideFlags = HideFlags.HideAndDontSave }; }
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "ShallowWater.UnitPatch", hideFlags = HideFlags.HideAndDontSave };
                _mesh.vertices = new[] { new Vector3(-.5f,0,-.5f), new Vector3(-.5f,0,.5f), new Vector3(.5f,0,.5f), new Vector3(.5f,0,-.5f) };
                _mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                _mesh.triangles = new[] { 0,1,2,0,2,3 }; _mesh.RecalculateBounds();
            }
            var pipeline = config.Feature.Pipeline;
            if (!ReferenceEquals(_pipeline, pipeline))
            { _pipeline?.UnregisterProvider(SurfacePass.Name); _pipeline = pipeline; _pass = new SurfacePass(this); _pipeline.RegisterProvider(_pass); }
            if (_reflection == null)
            {
                var go = new GameObject("Shallow Water Reflection") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, Owner.gameObject.scene);
                _reflection = go.AddComponent<UnityCamera>();
                _reflection.enabled = false;
                _reflectionData = go.AddComponent<UniversalAdditionalCameraData>();
                _auxiliary = go.AddComponent<MxSceneRenderingAuxiliaryCamera>();
                _auxiliary.Kind = MxCameraRenderKind.Reflection; _auxiliary.InvertCulling = true;
            }
            _auxiliary.SourceCamera = _source;
            Vector2 size = Settings.Size;
            GeometryUtility.CalculateFrustumPlanes(_source, _frustum);
            bool visible = _source.transform.position.y > Position.y + .006f
                && GeometryUtility.TestPlanesAABB(_frustum, SurfaceBounds(Position, Rotation, size));
            bool reflect = SurfaceEnabled && ReflectionEnabled && visible;
            if (!visible || !RipplesEnabled || Settings.EffectiveRippleLimit == 0 || _ripples.Count == 0) _gameRippleField.Release();
            _reflectionWanted = reflect;
            // Play cameras use the normal depth-ordered camera loop. Edit preview owns its render request.
            _reflection.enabled = reflect && Application.isPlaying;
            if (!reflect)
            { _lod?.UnregisterGameplayCamera(_reflection); ReleaseTarget(); Issue = string.Empty; RefreshScenePreviews(); return; }
            if (!EnsureTarget()) return;
            int budget = ReflectionBudget;
            if (_reflectionBudget != budget) { _reflectionBudget = budget; _reflectionValid = false; }
            bool render = !Application.isPlaying || _reflectionSchedule.IsDue(_source, Time.realtimeSinceStartupAsDouble,
                ReflectionReady, Settings.EffectiveReflectionRate);
            _reflection.enabled = render && Application.isPlaying;
            // Keep both the texture and its capture projection together on skipped frames.
            if (!render) { RefreshScenePreviews(); return; }
            _reflection.CopyFrom(_source);
            _reflection.enabled = Application.isPlaying; _reflection.targetTexture = _reflectionTarget.rt;
            _reflection.depth = _source.depth - 1;
            _reflection.cullingMask = _source.cullingMask & Settings.ReflectionLayers.value & ~(1 << 5);
            _reflection.farClipPlane = Mathf.Min(_source.farClipPlane, Settings.EffectiveReflectionDistance);
            _reflection.useOcclusionCulling = false; _reflection.allowMSAA = false;
            _reflectionData.renderType = CameraRenderType.Base;
            _reflectionData.renderPostProcessing = false; _reflectionData.renderShadows = Settings.UseReflectionShadows && _sourceData.renderShadows;
            _reflectionData.requiresColorOption = CameraOverrideOption.Off;
            _reflectionData.requiresDepthOption = CameraOverrideOption.On;
            _reflectionData.volumeLayerMask = 0; _reflectionData.antialiasing = AntialiasingMode.None;
            Matrix4x4 mirror = ReflectionMatrix(Position.y);
            Vector3 position = mirror.MultiplyPoint(_source.transform.position);
            Vector3 forward = mirror.MultiplyVector(_source.transform.forward);
            Vector3 up = mirror.MultiplyVector(_source.transform.up);
            _reflection.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
            _reflection.worldToCameraMatrix = _source.worldToCameraMatrix * mirror;
            _reflection.ResetProjectionMatrix();
            Vector3 clipPoint = _reflection.worldToCameraMatrix.MultiplyPoint(Position + Vector3.up * .006f);
            Vector3 clipNormal = _reflection.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
            Vector4 clip = new Vector4(clipNormal.x,clipNormal.y,clipNormal.z,-Vector3.Dot(clipPoint,clipNormal));
            _reflection.projectionMatrix = _reflection.CalculateObliqueMatrix(clip);
            _reflectionViewProjection = GL.GetGPUProjectionMatrix(_reflection.projectionMatrix, true) * _reflection.worldToCameraMatrix;
            Issue = string.Empty;
            RefreshScenePreviews();
        }
        internal static Matrix4x4 ReflectionMatrix(float height)
        { var value = Matrix4x4.identity; value.m11 = -1; value.m13 = 2 * height; return value; }
        internal static Bounds SurfaceBounds(Vector3 position, Quaternion rotation, Vector2 size)
        {
            Vector3 right = rotation * Vector3.right, forward = rotation * Vector3.forward;
            Vector3 extent = new Vector3(Mathf.Abs(right.x)*size.x+Mathf.Abs(forward.x)*size.y,
                .2f, Mathf.Abs(right.z)*size.x+Mathf.Abs(forward.z)*size.y);
            return new Bounds(position, extent);
        }
        private bool EnsureTarget()
        {
            float scale = Settings.EffectiveReflectionScale;
            int width = Mathf.Max(64, Mathf.RoundToInt(_source.pixelWidth * scale));
            int height = Mathf.Max(64, Mathf.RoundToInt(_source.pixelHeight * scale));
            float clamp = Mathf.Min(1, Settings.EffectiveReflectionEdge / (float)Mathf.Max(width,height));
            width = Mathf.Max(64, Mathf.RoundToInt(width*clamp)); height = Mathf.Max(64, Mathf.RoundToInt(height*clamp));
            var cached = _reflectionTarget?.rt;
            if (cached != null && _reflectionColorView != null && cached.IsCreated()
                && SupportedDepth(cached.depthStencilFormat)
                && cached.width == width && cached.height == height
                && _width == width && _height == height) return true;
            if (_reflection != null) _reflection.targetTexture = null;
            ReleaseTarget();
            GraphicsFormat depth = SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil);
            if (!SupportedDepth(depth)) depth = GraphicsFormat.D24_UNorm_S8_UInt;
            if (!SupportedDepth(depth)) depth = GraphicsFormat.D32_SFloat_S8_UInt;
            if (!SupportedDepth(depth)) { SuspendView("No supported depth-stencil format for the reflection camera output."); return false; }
            var descriptor = new RenderTextureDescriptor(width,height)
            { graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat, depthStencilFormat = depth, msaaSamples = 1, sRGB = false };
            var rt = new RenderTexture(descriptor)
            { name = "WGame.ShallowWater.Reflection", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (!rt.Create() || !SupportedDepth(rt.depthStencilFormat))
            { DestroyOwned(rt); SuspendView("Could not create a reflection camera output with supported depth-stencil."); return false; }
            _reflectionTarget = RTHandles.Alloc(rt);
            // Match URP's target wrapper: camera output retains depth; the sampled graph view describes only color.
            _reflectionColorView = RTHandles.Alloc(new RenderTargetIdentifier(rt), "WGame.ShallowWater.Reflection.Color");
            _width = width; _height = height; return true;
        }
        private static bool SupportedDepth(GraphicsFormat format) => format != GraphicsFormat.None
            && SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render);
        private int ReflectionBudget => HashCode.Combine(Settings.QualityTier, Settings.EffectiveReflectionDistance,
            Settings.ReflectionLayers.value, Settings.UseReflectionShadows, Position.y);
        internal bool SurfaceVisible(UnityCamera camera, Plane[] planes)
        {
            if (!SurfaceEnabled || camera == null || camera.transform.position.y <= Position.y + .006f) return false;
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            return GeometryUtility.TestPlanesAABB(planes, SurfaceBounds(Position, Rotation, Settings.Size));
        }
        internal void BindPresentationLod(CharacterTestPresentationLod lod)
        {
            if (!ReferenceEquals(_lod,lod))
            { _lod?.UnregisterGameplayCamera(_reflection); BindScenePreviewLod(_lod, true); _lod = lod; }
            if (_reflection != null && _reflectionWanted) _lod?.RegisterReflectionCamera(_reflection);
            else _lod?.UnregisterGameplayCamera(_reflection);
            BindScenePreviewLod(_lod, false);
        }
        /// <summary>Capture helper: outside rendering only; does not nest another camera inside a RenderGraph.</summary>
        public void RenderReflectionNow()
        {
            if (_renderRequestInProgress || _renderContextDepth != 0) return;
            PrepareView();
            if (_reflection != null && _reflectionWanted && _reflectionTarget != null)
            {
                _renderRequestInProgress = true;
                try
                {
                    RenderPipeline.SubmitRenderRequest(_reflection, new UniversalRenderPipeline.SingleCameraRequest { destination = _reflectionTarget.rt });
                }
                finally { _renderRequestInProgress = false; _renderContextDepth = 0; }
            }
        }
        private void ReleaseTarget()
        {
            _reflectionValid = false;
            if (_reflectionTarget == null) return;
            if (_reflection != null) _reflection.targetTexture = null;
            _reflectionColorView?.Release(); _reflectionColorView = null;
            var rt = _reflectionTarget.rt; _reflectionTarget.Release(); _reflectionTarget = null;
            DestroyOwned(rt); _width = _height = 0;
        }
        private void SuspendView(string issue)
        {
            _gameRippleField.Release();
            ClearSplashParticles();
            ReleaseScenePreviews();
            Issue = issue;
            _reflectionWanted = false;
            if (_reflection != null) _reflection.enabled = false;
            _lod?.UnregisterGameplayCamera(_reflection);
            if (_auxiliary != null) _auxiliary.SourceCamera = null;
            _pipeline?.UnregisterProvider(SurfacePass.Name); _pipeline = null; _pass = null;
            ReleaseTarget();
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _gameRippleField.Release();
            RenderPipelineManager.beginContextRendering -= BeginContext;
            RenderPipelineManager.endContextRendering -= EndContext;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            _reflectionWanted = _reflectionValid = _renderRequestInProgress = false; _renderContextDepth = 0;
            UnbindInteractions();
            ReleaseSplashes();
            ReleaseScenePreviews();
            _lod?.UnregisterGameplayCamera(_reflection); _lod = null;
            _pipeline?.UnregisterProvider(SurfacePass.Name); _pipeline = null; _pass = null;
            if (_reflection != null) { _reflection.enabled = false; _reflection.targetTexture = null; DestroyOwned(_reflection.gameObject); }
            _reflection = null; _auxiliary = null; ReleaseTarget();
            DestroyOwned(_mesh); _mesh = null; DestroyOwned(_material); _material = null;
        }
        private static void DestroyOwned(UnityEngine.Object value)
        { if (value == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }

    /// <summary>Capture-based cadence: repeated preparation never consumes an update before it renders.</summary>
    internal struct WaterReflectionSchedule
    {
        private double _lastRender;
        private Vector3 _position;
        private Quaternion _rotation;
        private Matrix4x4 _projection;
        internal bool IsDue(UnityCamera camera, double now, bool ready, int rate)
        {
            return !ready || rate <= 0 || now < _lastRender || now - _lastRender >= 1d / rate
                || (camera.transform.position - _position).sqrMagnitude > 4
                || Quaternion.Angle(camera.transform.rotation, _rotation) > 12
                || !_projection.Equals(camera.projectionMatrix);
        }
        internal void Record(UnityCamera camera, double now)
        {
            _lastRender = now; _position = camera.transform.position; _rotation = camera.transform.rotation;
            _projection = camera.projectionMatrix;
        }
    }
}
