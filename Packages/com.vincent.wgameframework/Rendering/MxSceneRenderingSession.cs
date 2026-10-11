using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MxFramework.Rendering
{
    /// <summary>Scene presentation lifetime; no GameObject, Editor dependency, or gameplay authority.</summary>
    public sealed class MxSceneRenderingSession : IDisposable
    {
        private readonly Func<MxSceneRenderingConfiguration> _readConfiguration;
        private readonly Func<MxSceneLightingData, MxSceneLightingPayload> _readLightingPayload;
        private readonly Scene _scene;
        private readonly bool _editorPreview;
        private MxSceneRenderingConfiguration _configuration;
        private MxSceneLightingProvider _lighting;
        private MxDirectionalShadowProvider _directionalShadows;
        private IMxRenderPipeline _directionalShadowPipeline;
        private bool _directionalShadowSourceValid = true;
        private string _directionalShadowSourceIssue = string.Empty;
        private MxHeightFogSceneProvider _fog;
        private MxSceneDustProvider _dust;
        private IMxRenderPipeline _dustPipeline;
        private Shader _boundDustShader;
        private IMxRenderPipeline _lightingPipeline, _fogPipeline;
        private Transform _boundRoot;
        private Light _boundSun;
        private Camera _boundCamera, _historyCamera;
        private MxRenderingPipelineFeature _boundFeature;
        private MxSceneLightingData _boundData;
        private MxSceneLightingLook _boundLook, _sourceLook, _previewLook;
        private ComputeShader _boundCompute;
        private Shader _boundCapture, _boundResolve, _boundFogShader;
        private bool _boundPreviewLighting, _boundIndirectEnabled;
        private bool _failedLighting;
        private Vector3 _lastCameraPosition;
        private Quaternion _lastCameraRotation;
        private readonly Vector4[] _capsules = new Vector4[32], _ends = new Vector4[32];
        private readonly Dictionary<SkinnedMeshRenderer, bool> _motionSettings = new Dictionary<SkinnedMeshRenderer, bool>();
        private SkinnedMeshRenderer[] _actors = Array.Empty<SkinnedMeshRenderer>();
        private double _nextActorRefresh;
        private bool _sourceValid = true, _disposed;
        private bool _sceneWasLoaded;
        private readonly List<PresentationEntry> _presentations = new List<PresentationEntry>();
        private sealed class PresentationEntry { internal MxScenePresentationBinding Binding; internal MxScenePresentationData Data; internal IMxScenePresentationConsumer Consumer; }
        public int PresentationCount => _presentations.Count;
        public string PresentationIssue { get; private set; } = string.Empty;
        private string _sourceIssue = string.Empty;
        private int _previewDebugView = -1;

        public int HistoryGeneration { get; private set; }
        public string LightingIssue { get; private set; } = string.Empty;
        public string FogIssue { get; private set; } = string.Empty;
        public string DustIssue { get; private set; } = string.Empty;
        public bool HasDust => _dust != null;
        public int DustDrawsRecorded => _dust?.DrawsRecorded ?? 0;
        public int LightingUpdates => _lighting?.LightingUpdates ?? 0;
        public int VisibilityUpdates => _lighting?.VisibilityUpdates ?? 0;
        public long PersistentBytes => _lighting?.PersistentBytes ?? 0;
        public bool HasLighting => _lighting != null;
        public bool HasFog => _fog != null;
        public bool HasDirectionalShadows => _directionalShadows != null;
        public string DirectionalShadowIssue => _directionalShadows?.Issue ?? _directionalShadowSourceIssue;
        public long DirectionalShadowGpuBytes => _directionalShadows?.OwnedGpuBytes ?? 0;
        public int DynamicShadowCasters => _directionalShadows?.ActiveCasters ?? 0;
        public int DynamicShadowPoseStride => _directionalShadows?.PoseStride ?? 0;

        /// <summary>NoAlloc after receiver warmup. Called after gameplay cameras and before the owning pose traversal.</summary>
        public void PrepareDirectionalShadowPoseDemand() => _directionalShadows?.PreparePresentationPoseDemand();

        /// <summary>NoAlloc: conservatively retains a pose when this session has no validated provider.</summary>
        public bool NeedsCurrentDirectionalShadowPose(Renderer renderer) => _directionalShadows == null
            || _directionalShadows.NeedsCurrentPresentationPose(renderer);

        public MxSceneRenderingSession(Func<MxSceneRenderingConfiguration> readConfiguration,
            Scene scene, bool editorPreview = false, bool sourceValid = true, string sourceIssue = null)
            : this(readConfiguration,scene,editorPreview,sourceValid,sourceIssue,null) { }
        /// <summary>Allows an editor preview to reuse a validated CPU transport snapshot.</summary>
        public MxSceneRenderingSession(Func<MxSceneRenderingConfiguration> readConfiguration,
            Scene scene, bool editorPreview, bool sourceValid, string sourceIssue,
            Func<MxSceneLightingData, MxSceneLightingPayload> readLightingPayload)
        {
            _readConfiguration = readConfiguration ?? throw new ArgumentNullException(nameof(readConfiguration));
            _readLightingPayload = readLightingPayload;
            _scene = scene;
            _editorPreview = editorPreview;
            _sourceValid = sourceValid;
            _sourceIssue = sourceIssue ?? string.Empty;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            Tick();
        }

        public void SetSourceValidation(bool valid, string reason)
        {
            if (_sourceValid == valid && string.Equals(_sourceIssue, reason ?? string.Empty, StringComparison.Ordinal)) return;
            _sourceValid = valid;
            _sourceIssue = reason ?? string.Empty;
            if (!valid) ReleaseLighting();
            Tick();
        }
        public void SetDirectionalShadowSourceValidation(bool valid, string reason)
        {
            _directionalShadowSourceValid = valid; _directionalShadowSourceIssue = reason ?? string.Empty;
            _directionalShadows?.SetSourceValidation(valid, reason);
        }
        public void RebindDirectionalShadows() { ReleaseDirectionalShadows(); Tick(); }

        public void SetPreviewDebugView(int mode)
        {
            if (!_editorPreview) return;
            _previewDebugView = Mathf.Clamp(mode, -1, 3);
            SyncLook();
        }

        public void RebindLighting()
        {
            ReleaseLighting();
            _boundData = null;
            _failedLighting = false;
            Tick();
        }

        public void Tick()
        {
            if (_disposed) return;
            if (!_scene.IsValid()) { Dispose(); return; }
            if (!_scene.isLoaded)
            {
                // OnEnable can create the session before the scene finishes loading.
                // Only release a scene that this session has already seen loaded.
                if (_sceneWasLoaded) Dispose();
                return;
            }
            _sceneWasLoaded = true;
            _configuration = _readConfiguration();
            SyncPresentations();
            SyncLook();
            SyncLighting();
            SyncFog();
            SyncDust();
            SyncDirectionalShadows();
            if (_lighting == null) return;
            Camera camera = _configuration.TargetCamera;
            if (camera != null && (_historyCamera != camera ||
                Vector3.Distance(_lastCameraPosition, camera.transform.position) > 15 ||
                Quaternion.Angle(_lastCameraRotation, camera.transform.rotation) > 45))
                ResetHistory(camera);
            RefreshActors();
            int count = 0;
            for (int i = 0; i < _actors.Length && count < 32; i++)
            {
                var renderer = _actors[i];
                if (renderer == null || !renderer.enabled || renderer.gameObject.scene != _scene) continue;
                Bounds bounds = renderer.bounds;
                if (bounds.size.y < 0.7f || bounds.size.y > 6) continue;
                float radius = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z), 0.12f, 0.6f);
                Vector3 start = new Vector3(bounds.center.x, bounds.min.y + radius, bounds.center.z);
                Vector3 end = new Vector3(bounds.center.x, Mathf.Max(start.y, bounds.max.y - radius), bounds.center.z);
                bool duplicate = false;
                for (int j = 0; j < count; j++)
                    if (Vector3.SqrMagnitude((Vector3)_capsules[j] - start) < 0.15f) { duplicate = true; break; }
                if (duplicate) continue;
                _capsules[count] = new Vector4(start.x, start.y, start.z, radius);
                _ends[count] = new Vector4(end.x, end.y, end.z, 0);
                count++;
            }
            _lighting.SetCapsules(_capsules, _ends, count);
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (_disposed) return;
            Tick();
            for (int i = 0; i < _presentations.Count; i++) _presentations[i].Consumer?.BeginCamera(camera);
        }
        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (_disposed) return;
            for (int i = 0; i < _presentations.Count; i++) _presentations[i].Consumer?.EndCamera(camera);
        }
        private void SyncPresentations()
        {
            var bindings = _configuration?.Presentations;
            for (int i = _presentations.Count - 1; i >= 0; i--)
            {
                var entry = _presentations[i]; bool retained = false;
                if (bindings != null)
                    for (int j = 0; j < bindings.Length; j++)
                        if (ReferenceEquals(bindings[j], entry.Binding) && bindings[j].Enabled && bindings[j].Data != null && bindings[j].Data == entry.Data)
                        { retained = true; break; }
                if (!retained) { entry.Consumer?.Dispose(); _presentations.RemoveAt(i); }
            }
            if (bindings != null)
                for (int i = 0; i < bindings.Length; i++)
                {
                    var binding = bindings[i];
                    if (binding == null || !binding.Enabled || binding.Data == null ||
                        (binding.Anchor != null && binding.Anchor.gameObject.scene != _scene)) continue;
                    bool registered = false;
                    for (int j = 0; j < _presentations.Count; j++)
                        if (ReferenceEquals(_presentations[j].Binding, binding) || (!string.IsNullOrEmpty(binding.OwnerId) && string.Equals(_presentations[j].Binding.OwnerId, binding.OwnerId, StringComparison.Ordinal))) { registered = true; break; }
                    if (registered) continue;
                    var entry = new PresentationEntry { Binding = binding, Data = binding.Data };
                    try { entry.Consumer = binding.Data.CreateConsumer(binding, _scene, _editorPreview); }
                    catch (Exception error) { PresentationIssue = error.Message; }
                    _presentations.Add(entry); // Retry only after rebind, never as a per-frame exception loop.
                }
            for (int i = 0; i < _presentations.Count; i++) _presentations[i].Consumer?.Tick();
        }

        private void SyncLook()
        {
            var current = _configuration?.Look;
            if (!_editorPreview)
            {
                _sourceLook = current;
                return;
            }
            if (_sourceLook != current)
            {
                ReleaseLighting();
                DestroyPreviewLook();
                _sourceLook = current;
                if (current != null)
                    _previewLook = UnityEngine.Object.Instantiate(current);
                if (_previewLook != null) _previewLook.hideFlags = HideFlags.HideAndDontSave;
            }
            if (current == null || _previewLook == null) return;
            _previewLook.DirectStrength = current.DirectStrength;
            _previewLook.IndirectStrength = current.IndirectStrength;
            _previewLook.MicroOcclusion = current.MicroOcclusion;
            _previewLook.CapsuleOcclusion = current.CapsuleOcclusion;
            _previewLook.BandStrength = current.BandStrength;
            _previewLook.SpecularStrength = current.SpecularStrength;
            _previewLook.LightRimStrength = current.LightRimStrength;
            _previewLook.ShadowTint = current.ShadowTint;
            _previewLook.Sky = current.Sky;
            _previewLook.Equator = current.Equator;
            _previewLook.Ground = current.Ground;
            _previewLook.DebugView = _previewDebugView < 0 ? current.DebugView : _previewDebugView;
        }

        private MxSceneLightingLook EffectiveLook => _editorPreview ? _previewLook : _sourceLook;

        private void SyncLighting()
        {
            var c = _configuration;
            if (c == null || !c.IndirectLightingEnabled || !_sourceValid)
            {
                ReleaseLighting();
                LightingIssue = c != null && c.IndirectLightingEnabled ? _sourceIssue : string.Empty;
                _boundIndirectEnabled = false;
                _failedLighting = false;
                return;
            }
            var look = EffectiveLook;
            bool worldChanged = !_boundIndirectEnabled || _boundRoot != c.StaticRoot || _boundSun != c.Sun ||
                _boundData != c.Data || _boundLook != look || _boundCompute != c.Compute ||
                _boundCapture != c.CaptureShader || _boundResolve != c.ResolveShader ||
                (_lighting == null && (_boundCamera != c.TargetCamera || _boundFeature != c.Feature));
            if (worldChanged || (_lighting == null && !_failedLighting))
            {
                ReleaseLighting();
                _failedLighting = true;
                _boundIndirectEnabled = true;
                _boundRoot = c.StaticRoot; _boundSun = c.Sun; _boundData = c.Data;
                _boundLook = look; _boundCompute = c.Compute;
                _boundCapture = c.CaptureShader; _boundResolve = c.ResolveShader;
                _boundCamera = c.TargetCamera; _boundFeature = c.Feature;
                if (c.StaticRoot == null || c.Sun == null || c.Data == null || look == null ||
                    c.Feature == null || c.TargetCamera == null || c.Compute == null ||
                    c.CaptureShader == null || c.ResolveShader == null)
                {
                    LightingIssue = "Assign static root, sun, camera, renderer, Look, transport data and shaders.";
                    return;
                }
                try
                {
                    _lighting = new MxSceneLightingProvider(c.Data, look, c.Sun, c.TargetCamera,
                        c.Compute, c.CaptureShader, c.ResolveShader, c.PreviewIndirectInSceneView,
                        _readLightingPayload?.Invoke(c.Data));
                    _lighting.SetHistoryGeneration(HistoryGeneration);
                    _failedLighting = false;
                    LightingIssue = string.Empty;
                }
                catch (Exception exception) { LightingIssue = exception.Message; ReleaseLighting(); return; }
            }
            if (_lighting == null) return;
            if (_boundCamera != c.TargetCamera || _boundPreviewLighting != c.PreviewIndirectInSceneView)
            {
                _lighting.SetTargetCamera(c.TargetCamera, c.PreviewIndirectInSceneView);
                _boundPreviewLighting = c.PreviewIndirectInSceneView;
                if (_boundCamera != c.TargetCamera) ResetHistory(c.TargetCamera);
                _boundCamera = c.TargetCamera;
            }
            _lighting.SetMotionVectorsRequired(c.RequireMotionVectors || look.DebugView == 3);
            if (c.Feature == null)
            {
                _lightingPipeline?.UnregisterProvider(_lighting.DebugName);
                _lightingPipeline = null;
                LightingIssue = "Assign a rendering feature.";
                return;
            }
            var pipeline = c.Feature.Pipeline;
            if (!ReferenceEquals(pipeline, _lightingPipeline))
            {
                _lightingPipeline?.UnregisterProvider(_lighting.DebugName);
                _lightingPipeline = null;
                if (!pipeline.RegisterProvider(_lighting)) { LightingIssue = "Scene lighting is already registered."; return; }
                _lightingPipeline = pipeline;
            }
            LightingIssue = string.Empty;
        }

        private void SyncFog()
        {
            var c = _configuration;
            if (c == null || !c.HeightFogEnabled)
            {
                ReleaseFog(); FogIssue = string.Empty; return;
            }
            if (c.Feature == null || c.FogCompositeShader == null || !c.FogCompositeShader.isSupported)
            {
                ReleaseFog(); FogIssue = "Assign the rendering feature and a supported fog shader."; return;
            }
            var settings = new MxVolumetricFogSettings(c.FogDensity > 0 && c.FogScatteringIntensity > 0,
                MxVolumetricFogQuality.High, c.FogColor.linear, c.FogDensity, c.FogBaseHeight,
                c.FogHeightFalloff, c.FogMaxDistance, 0, c.FogScatteringIntensity,
                0, 1, 1, 2, 0, c.FogFocusClearRadius, c.FogFocusClearSoftness,
                c.PreviewFogInSceneView);
            if (_fog == null || _boundFogShader != c.FogCompositeShader)
            {
                ReleaseFog();
                _fog = new MxHeightFogSceneProvider(settings, c.TargetCamera, c.FogCompositeShader);
                _boundFogShader = c.FogCompositeShader;
            }
            _fog.SetCamera(c.TargetCamera);
            if (!_fog.Settings.Equals(settings)) _fog.SetSettings(settings);
            var pipeline = c.Feature.Pipeline;
            if (!ReferenceEquals(pipeline, _fogPipeline))
            {
                _fogPipeline?.UnregisterProvider(_fog.DebugName);
                _fogPipeline = null;
                if (!pipeline.RegisterProvider(_fog)) { FogIssue = "Scene height fog is already registered."; return; }
                _fogPipeline = pipeline;
            }
            FogIssue = c.TargetCamera == null ? "Assign a target camera for height fog." : string.Empty;
        }
        private void SyncDirectionalShadows()
        {
            var c = _configuration;
            if (c == null || !c.DirectionalShadowsEnabled)
            { ReleaseDirectionalShadows(); return; }
            if (c.DirectionalShadowData == null || !c.DirectionalShadowData.IsValid || c.DirectionalShadowCompute == null ||
                c.Feature == null || c.TargetCamera == null || c.Sun == null)
            { ReleaseDirectionalShadows(); _directionalShadowSourceIssue = "Assign baked directional-shadow data, compute, fixed sun, camera and renderer."; return; }
            if (_directionalShadows != null && (_directionalShadows.Data != c.DirectionalShadowData || _directionalShadows.Compute != c.DirectionalShadowCompute))
                ReleaseDirectionalShadows();
            if (_directionalShadows == null)
            {
                try { _directionalShadows = new MxDirectionalShadowProvider(c.DirectionalShadowData, c.DirectionalShadowCompute, _scene, _editorPreview); }
                catch (Exception exception) { _directionalShadowSourceIssue = exception.Message; return; }
            }
            _directionalShadows.SetConfiguration(c);
            _directionalShadows.SetSourceValidation(_directionalShadowSourceValid, _directionalShadowSourceIssue);
            var pipeline = c.Feature.Pipeline;
            if (!ReferenceEquals(pipeline, _directionalShadowPipeline))
            {
                _directionalShadowPipeline?.UnregisterProvider(_directionalShadows.DebugName);
                _directionalShadowPipeline = null;
                if (!pipeline.RegisterProvider(_directionalShadows))
                { _directionalShadowSourceIssue = "Directional shadows are already registered."; return; }
                _directionalShadowPipeline = pipeline;
            }
        }
        private void ReleaseDirectionalShadows()
        {
            if (_directionalShadows == null) return;
            _directionalShadowPipeline?.UnregisterProvider(_directionalShadows.DebugName);
            _directionalShadows.Dispose(); _directionalShadows = null; _directionalShadowPipeline = null;
        }

        private void SyncDust()
        {
            var c = _configuration;
            if (c == null || !c.DustEnabled)
            { ReleaseDust(); DustIssue = string.Empty; return; }
            if (c.Feature == null || c.DustShader == null || !c.DustShader.isSupported || c.Dust == null)
            { ReleaseDust(); DustIssue = "Assign the rendering feature, dust settings and a supported dust shader."; return; }
            if (_dust == null || _boundDustShader != c.DustShader)
            {
                ReleaseDust();
                _dust = new MxSceneDustProvider(c.DustShader);
                _boundDustShader = c.DustShader;
            }
            _dust.SetConfiguration(c, _fog?.Settings ?? MxVolumetricFogSettings.Disabled());
            var pipeline = c.Feature.Pipeline;
            if (!ReferenceEquals(pipeline, _dustPipeline))
            {
                _dustPipeline?.UnregisterProvider(_dust.DebugName);
                _dustPipeline = null;
                if (!pipeline.RegisterProvider(_dust)) { DustIssue = "Scene dust is already registered."; return; }
                _dustPipeline = pipeline;
            }
            DustIssue = c.TargetCamera == null ? "Assign a target camera for dust." : string.Empty;
        }

        private void ReleaseDust()
        {
            if (_dust == null) return;
            _dustPipeline?.UnregisterProvider(_dust.DebugName);
            _dust.Dispose(); _dust = null; _dustPipeline = null; _boundDustShader = null;
        }

        private void RefreshActors()
        {
            if (Time.realtimeSinceStartupAsDouble >= _nextActorRefresh)
            {
                _actors = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>();
                _nextActorRefresh = Time.realtimeSinceStartupAsDouble + 1;
            }
            if (_lighting.MotionVectorsRequired)
            {
                foreach (var actor in _actors)
                    if (actor != null && actor.gameObject.scene == _scene && !_motionSettings.ContainsKey(actor))
                    { _motionSettings.Add(actor, actor.skinnedMotionVectors); actor.skinnedMotionVectors = true; }
            }
            else RestoreMotionSettings();
        }

        private void ResetHistory(Camera camera)
        {
            HistoryGeneration++;
            _lighting?.SetHistoryGeneration(HistoryGeneration);
            _historyCamera = camera;
            if (camera == null) return;
            _lastCameraPosition = camera.transform.position;
            _lastCameraRotation = camera.transform.rotation;
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) data.resetHistory = true;
        }

        private void RestoreMotionSettings()
        {
            foreach (var entry in _motionSettings)
                if (entry.Key != null) entry.Key.skinnedMotionVectors = entry.Value;
            _motionSettings.Clear();
        }
        private void ReleaseLighting()
        {
            RestoreMotionSettings();
            if (_lighting == null) return;
            _lightingPipeline?.UnregisterProvider(_lighting.DebugName);
            _lighting.Dispose(); _lighting = null; _lightingPipeline = null;
        }
        private void ReleaseFog()
        {
            if (_fog == null) return;
            _fogPipeline?.UnregisterProvider(_fog.DebugName);
            _fog.Dispose(); _fog = null; _fogPipeline = null; _boundFogShader = null;
        }
        private void DestroyPreviewLook()
        {
            if (_previewLook == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_previewLook);
            else UnityEngine.Object.DestroyImmediate(_previewLook);
            _previewLook = null;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            for (int i = 0; i < _presentations.Count; i++) _presentations[i].Consumer?.Dispose();
            _presentations.Clear();
            ReleaseDirectionalShadows(); ReleaseDust(); ReleaseLighting(); ReleaseFog(); DestroyPreviewLook();
        }
    }
}
