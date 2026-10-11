using System;
using System.Collections.Generic;
using MxFramework.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MxFramework.Settings.Graphics
{
    /// <summary>Owns only a transient tonemapping override and the application's HDR display request.</summary>
    public sealed class MxUnityHdrOutputRuntime : IMxHdrOutputRuntime, IDisposable
    {
        private MxHdrOutputSettings _settings = MxHdrOutputSettings.Default;
        private GameObject _volumeObject;
        private VolumeProfile _profile;
        private Tonemapping _tonemapping;
        private readonly Dictionary<Camera, CameraVolumeState> _cameraVolumes = new Dictionary<Camera, CameraVolumeState>();
        private readonly List<Camera> _destroyedCameras = new List<Camera>();
        private uint _volumeRevision;
        private bool _disposed, _started, _originalActive, _originalAutomatic, _automaticCaptured, _lastAvailable;
        private bool? _requested;
        private float _requestTime;
        private int _frame = -1;
        private string _failure = string.Empty;

        public MxHdrOutputStatus Status
        {
            get
            {
                var display = HDROutputSettings.main;
                if (!Application.isPlaying)
                    return new MxHdrOutputStatus(MxHdrOutputState.Unavailable, reason: "play_mode_required");
                if (display == null || !display.available)
                    return new MxHdrOutputStatus(MxHdrOutputState.Unavailable, reason: "display_unavailable");
                if (!string.IsNullOrEmpty(_failure))
                    return new MxHdrOutputStatus(MxHdrOutputState.Failed, true, reason: _failure, active: display.active);
                bool pending = display.HDRModeChangeRequested || (_requested.HasValue && display.active != _requested.Value);
                float white = Resolve(_settings.PaperWhiteNits, display.paperWhiteNits, 200, 80, 400);
                float peak = Resolve(_settings.PeakNits, display.maxToneMapLuminance, 1000, 400, 5000);
                return new MxHdrOutputStatus(pending ? MxHdrOutputState.Switching :
                    display.active ? MxHdrOutputState.Hdr : MxHdrOutputState.Sdr, true, white, peak, active: display.active);
            }
        }

        public bool TryApply(MxHdrOutputSettings settings, out string message)
        {
            message = string.Empty;
            if (_disposed) { message = "HDR display adapter has been disposed."; return false; }
            settings = settings ?? MxHdrOutputSettings.Default;
            if (settings.Mode == MxHdrOutputMode.Default)
            { ReleaseOverride(); _settings = settings; return true; }
            if (!Application.isPlaying) { message = "HDR overrides require Play Mode."; return false; }
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
            { message = "HDR settings require the Universal Render Pipeline."; return false; }
            if (!_started)
            {
                var display = HDROutputSettings.main;
                _originalActive = display != null && display.active;
                _automaticCaptured = false;
                _started = true;
                RenderPipelineManager.beginCameraRendering += BeginCamera;
            }
            _settings = settings;
            _requested = null; _failure = string.Empty; _frame = -1;
            UpdateOutput();
            return true; // An unavailable display is a valid SDR fallback; preference remains saved.
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            UpdateOutput();
            RefreshCameraVolume(camera);
        }

        private void RefreshCameraVolume(Camera camera)
        {
            if (!_started || camera == null || camera.cameraType == CameraType.SceneView ||
                !VolumeManager.instance.isInitialized || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset) ||
                !camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) return;
            if (data.requiresVolumeFrameworkUpdate)
            { _cameraVolumes.Remove(camera); return; } // URP owns EveryFrame cameras.
            bool affected = (data.volumeLayerMask.value & (1 << _volumeObject.layer)) != 0;
            if (!affected)
            {
                // A formerly affected camera may have changed its mask while retaining our cached override.
                if (_cameraVolumes.Remove(camera) && data.volumeStack != null) camera.UpdateVolumeStack();
                return;
            }
            if (_cameraVolumes.TryGetValue(camera, out var previous) && previous.Revision == _volumeRevision &&
                ReferenceEquals(previous.Stack, data.volumeStack)) return;

            camera.UpdateVolumeStack();
            PruneDestroyedCameras();
            _cameraVolumes[camera] = new CameraVolumeState(_volumeRevision, data.volumeStack);
        }

        private void PruneDestroyedCameras()
        {
            _destroyedCameras.Clear();
            foreach (var pair in _cameraVolumes) if (pair.Key == null) _destroyedCameras.Add(pair.Key);
            for (int i = 0; i < _destroyedCameras.Count; i++) _cameraVolumes.Remove(_destroyedCameras[i]);
            _destroyedCameras.Clear();
        }

        private readonly struct CameraVolumeState
        {
            internal CameraVolumeState(uint revision, VolumeStack stack) { Revision = revision; Stack = stack; }
            internal uint Revision { get; }
            internal VolumeStack Stack { get; }
        }

        private void UpdateOutput()
        {
            if (_disposed || !_started || _frame == Time.frameCount) return;
            _frame = Time.frameCount;
            var display = HDROutputSettings.main;
            bool available = display != null && display.available;
            bool desired = available && _settings.Mode == MxHdrOutputMode.Enabled;
            if (available != _lastAvailable) { _requested = null; _failure = string.Empty; }
            _lastAvailable = available;
            if (display != null && available)
            {
                if (!_automaticCaptured)
                { _originalAutomatic = display.automaticHDRTonemapping; _automaticCaptured = true; }
                display.automaticHDRTonemapping = false;
                if (_requested != desired)
                {
                    _requested = desired; _requestTime = Time.realtimeSinceStartup;
                    try
                    {
                        if (display.active != desired || display.HDRModeChangeRequested)
                            display.RequestHDRModeChange(desired);
                    }
                    catch (Exception) { _failure = "request_failed"; }
                }
                if (available && display.active != desired && Time.realtimeSinceStartup - _requestTime > 5)
                    _failure = "request_failed";
            }
            EnsureOverride();
            bool hdr = available && display.active;
            var mode = hdr ? TonemappingMode.Neutral : TonemappingMode.ACES;
            float white = Resolve(_settings.PaperWhiteNits, available ? display.paperWhiteNits : 0, 200, 80, 400);
            float peak = Resolve(_settings.PeakNits, available ? display.maxToneMapLuminance : 0, 1000, 400, 5000);
            float minimum = Mathf.Clamp(available ? display.minToneMapLuminance : 0, 0, 50);
            bool changed = !_tonemapping.mode.overrideState || _tonemapping.mode.value != mode ||
                !_tonemapping.neutralHDRRangeReductionMode.overrideState || _tonemapping.neutralHDRRangeReductionMode.value != NeutralRangeReductionMode.BT2390 ||
                !_tonemapping.hueShiftAmount.overrideState || _tonemapping.hueShiftAmount.value != 0 ||
                !_tonemapping.detectPaperWhite.overrideState || _tonemapping.detectPaperWhite.value ||
                !_tonemapping.paperWhite.overrideState || _tonemapping.paperWhite.value != white ||
                !_tonemapping.detectBrightnessLimits.overrideState || _tonemapping.detectBrightnessLimits.value ||
                !_tonemapping.maxNits.overrideState || _tonemapping.maxNits.value != peak ||
                !_tonemapping.minNits.overrideState || _tonemapping.minNits.value != minimum;
            if (!changed) return;
            // Display adaptation follows actual output state, including asynchronous transitions.
            _tonemapping.mode.Override(mode);
            _tonemapping.neutralHDRRangeReductionMode.Override(NeutralRangeReductionMode.BT2390);
            _tonemapping.hueShiftAmount.Override(0);
            _tonemapping.detectPaperWhite.Override(false);
            _tonemapping.paperWhite.Override(white);
            _tonemapping.detectBrightnessLimits.Override(false);
            _tonemapping.maxNits.Override(peak);
            _tonemapping.minNits.Override(minimum);
            _volumeRevision++;
        }

        private void EnsureOverride()
        {
            if (_tonemapping != null) return;
            _volumeObject = new GameObject("HDR display output settings") { hideFlags = HideFlags.HideAndDontSave };
            var volume = _volumeObject.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 10000; volume.weight = 1;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.hideFlags = HideFlags.HideAndDontSave;
            _tonemapping = _profile.Add<Tonemapping>(false);
            volume.sharedProfile = _profile;
        }

        private static float Resolve(float preference, float reported, float fallback, float min, float max)
        {
            float value = preference > 0 ? preference : reported;
            return Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) || value <= 0 ? fallback : value, min, max);
        }

        private void ReleaseOverride()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            if (_started)
            {
                var display = HDROutputSettings.main;
                if (display != null && display.available)
                {
                    try
                    {
                        if (_automaticCaptured) display.automaticHDRTonemapping = _originalAutomatic;
                        if (display.active != _originalActive || display.HDRModeChangeRequested)
                            display.RequestHDRModeChange(_originalActive);
                    }
                    catch (Exception) { /* Display removal must not prevent owned resources being released. */ }
                }
            }
            _started = false; _requested = null; _failure = string.Empty;
            if (_volumeObject != null)
            {
                int mask = 1 << _volumeObject.layer;
                // Destroy is deferred in Play Mode; unregister the owned Volume before recomputing stacks.
                _volumeObject.SetActive(false);
                if (VolumeManager.instance.isInitialized && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                {
                    // Low-frequency teardown only. Include disabled cameras which may retain a stack until re-enabled,
                    // and externally refreshed cameras that consumed the global override before their first render.
#if UNITY_6000_6_OR_NEWER
                    var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
#else
                    var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif
                    for (int i = 0; i < cameras.Length; i++)
                    {
                        var camera = cameras[i];
                        if (camera.cameraType == CameraType.SceneView || !camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) ||
                            data.requiresVolumeFrameworkUpdate || data.volumeStack == null) continue;
                        if ((data.volumeLayerMask.value & mask) != 0 || _cameraVolumes.ContainsKey(camera)) camera.UpdateVolumeStack();
                    }
                }
            }
            _cameraVolumes.Clear(); _destroyedCameras.Clear();
            if (_profile != null) foreach (var component in _profile.components) CoreUtils.Destroy(component);
            CoreUtils.Destroy(_profile); CoreUtils.Destroy(_volumeObject);
            _profile = null; _volumeObject = null; _tonemapping = null;
        }

        public void Dispose() { if (_disposed) return; ReleaseOverride(); _disposed = true; }
    }
}
