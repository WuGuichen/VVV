using System;
using System.Collections.Generic;
using MagicaCloth2;
using MxFramework.CharacterCloth.Magica.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    internal enum MagicaCharacterClothMotionPreset
    {
        InPlace,
        ForwardBackward,
        Strafe,
        StartStop,
        Turn90,
        Turn180,
        ZigZag,
    }

    internal enum MagicaCharacterClothMotionCameraView
    {
        Back,
        Front,
        Left,
        Right,
    }

    internal enum MagicaCharacterClothMotionCameraMode
    {
        FixedWorld,
        FollowActor,
    }

    internal readonly struct MagicaCharacterClothMotionPreviewPose
    {
        internal MagicaCharacterClothMotionPreviewPose(Vector3 position, float yaw)
        {
            Position = position;
            Yaw = yaw;
        }

        internal Vector3 Position { get; }
        internal float Yaw { get; }
    }

    [InitializeOnLoad]
    internal static class MagicaCharacterClothMotionPreviewController
    {
        private const string RequestedKey = "MxFramework.CharacterCloth.MotionPreview.Requested";
        private const string PackageIdKey = "MxFramework.CharacterCloth.MotionPreview.PackageId";
        private const string SelectedClipPreferenceKeyPrefix =
            "MxFramework.CharacterCloth.MotionPreview.SelectedClip.";
        private const int PreviewLayer = 31;
        private const string PreviewEnvironmentName = "[Cloth Motion Preview]";
        private const HideFlags PreviewEnvironmentHideFlags =
            HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        private static MotionPreviewRuntime s_runtime;
        private static string s_status = string.Empty;

        static MagicaCharacterClothMotionPreviewController()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            if (EditorApplication.isPlaying && IsRequested)
                EditorApplication.delayCall += CreateRuntime;
        }

        internal static bool IsRequested => SessionState.GetBool(RequestedKey, false);
        internal static bool IsActive => s_runtime != null && EditorApplication.isPlaying;
        internal static string Status => IsActive ? s_runtime.Status : s_status;
        internal static string[] ClipLabels => IsActive ? s_runtime.ClipLabels : Array.Empty<string>();
        internal static int SelectedClipIndex => IsActive ? s_runtime.SelectedClipIndex : 0;
        internal static MagicaCharacterClothMotionPreset MotionPreset =>
            IsActive ? s_runtime.MotionPreset : MagicaCharacterClothMotionPreset.InPlace;
        internal static MagicaCharacterClothMotionCameraView CameraView =>
            IsActive ? s_runtime.CameraView : MagicaCharacterClothMotionCameraView.Back;
        internal static MagicaCharacterClothMotionCameraMode CameraMode =>
            IsActive ? s_runtime.CameraMode : MagicaCharacterClothMotionCameraMode.FixedWorld;
        internal static bool IsPlaying => IsActive && s_runtime.IsPlaying;
        internal static bool Loop => !IsActive || s_runtime.Loop;
        internal static bool ShowColliders => IsActive && s_runtime.ShowColliders;
        internal static float PlaybackSpeed => IsActive ? s_runtime.PlaybackSpeed : 1f;
        internal static float NormalizedTime => IsActive ? s_runtime.NormalizedTime : 0f;
        internal static int ActiveClothCount => IsActive ? s_runtime.ClothCount : 0;
        internal static int ActiveColliderCount => IsActive ? s_runtime.ColliderCount : 0;
        internal static bool HasActiveCamera => IsActive && s_runtime.HasCamera;
        internal static Transform ActiveBodyModel => IsActive ? s_runtime.BodyModel : null;
        internal static bool HasMotionGuides => IsActive && s_runtime.HasMotionGuides;
        internal static bool ShowMotionGuides => IsActive && s_runtime.ShowMotionGuides;
        internal static float MotionDistance => IsActive ? s_runtime.MotionDistance : 0.8f;
        internal static float MotionPeriod => IsActive ? s_runtime.MotionPeriod : 4f;
        internal static float StopDuration => IsActive ? s_runtime.StopDuration : 1f;
        internal static float TurnSpeed => IsActive ? s_runtime.TurnSpeed : 90f;
        internal static Vector3 MotionPosition => IsActive ? s_runtime.MotionPosition : Vector3.zero;
        internal static Vector3 MotionVelocity => IsActive ? s_runtime.MotionVelocity : Vector3.zero;
        internal static Vector3 MotionAcceleration => IsActive ? s_runtime.MotionAcceleration : Vector3.zero;
        internal static float MotionAngularVelocity => IsActive ? s_runtime.MotionAngularVelocity : 0f;
        internal static bool IsHotApplyPending => IsActive && s_runtime.IsHotApplyPending;
        internal static int HotApplyRevision => IsActive ? s_runtime.HotApplyRevision : 0;
        internal static string HotApplyStatus => IsActive ? s_runtime.HotApplyStatus : string.Empty;
        internal static bool PreviewWindEnabled => IsActive && s_runtime.PreviewWindEnabled;
        internal static float PreviewWindStrength => IsActive ? s_runtime.PreviewWindStrength : 4f;
        internal static float PreviewWindTurbulence => IsActive ? s_runtime.PreviewWindTurbulence : 0.5f;
        internal static float PreviewWindYaw => IsActive ? s_runtime.PreviewWindYaw : 0f;
        internal static bool HasActivePreviewWind => IsActive && s_runtime.HasActivePreviewWind;
        internal static bool IsStressPulseActive => IsActive && s_runtime.IsStressPulseActive;
        internal static string StressPulseStatus => IsActive ? s_runtime.StressPulseStatus : string.Empty;

        internal static bool Start(string packageId, out string error)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "Unity 已处于或正在进入 Play Mode。";
                return false;
            }
            if (!PrepareRequest(packageId, out error)) return false;
            EditorApplication.EnterPlaymode();
            return true;
        }

        internal static bool PrepareRequest(string packageId, out string error)
        {
            error = string.Empty;
            MagicaCharacterClothMotionPreviewCatalogResult catalog =
                MagicaCharacterClothMotionPreviewCatalog.Load(packageId);
            if (!catalog.Succeeded)
            {
                error = catalog.Error;
                return false;
            }

            SessionState.SetString(PackageIdKey, packageId);
            SessionState.SetBool(RequestedKey, true);
            s_status = "正在进入 Cloth 运动预览…";
            return true;
        }

        internal static void Stop()
        {
            SessionState.SetBool(RequestedKey, false);
            s_status = "正在退出 Cloth 运动预览…";
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.ExitPlaymode();
        }

        internal static void SetSelectedClip(int index) => s_runtime?.SelectClip(index);
        internal static void SetMotionPreset(MagicaCharacterClothMotionPreset preset)
        {
            if (s_runtime != null) s_runtime.MotionPreset = preset;
        }
        internal static void SetCameraView(MagicaCharacterClothMotionCameraView view)
        {
            if (s_runtime != null) s_runtime.CameraView = view;
        }
        internal static void SetCameraMode(MagicaCharacterClothMotionCameraMode mode)
        {
            if (s_runtime != null) s_runtime.CameraMode = mode;
        }
        internal static void SetLoop(bool value)
        {
            if (s_runtime != null) s_runtime.Loop = value;
        }
        internal static void SetShowColliders(bool value)
        {
            if (s_runtime != null) s_runtime.ShowColliders = value;
        }
        internal static void SetPlaybackSpeed(float value)
        {
            if (s_runtime != null) s_runtime.PlaybackSpeed = Mathf.Clamp(value, 0.1f, 2f);
        }
        internal static void SetShowMotionGuides(bool value)
        {
            if (s_runtime != null) s_runtime.ShowMotionGuides = value;
        }
        internal static void SetMotionDistance(float value)
        {
            if (s_runtime != null) s_runtime.MotionDistance = Mathf.Clamp(value, 0.1f, 2f);
        }
        internal static void SetMotionPeriod(float value)
        {
            if (s_runtime != null) s_runtime.MotionPeriod = Mathf.Clamp(value, 1f, 10f);
        }
        internal static void SetStopDuration(float value)
        {
            if (s_runtime != null) s_runtime.StopDuration = Mathf.Clamp(value, 0f, 4f);
        }
        internal static void SetTurnSpeed(float value)
        {
            if (s_runtime != null) s_runtime.TurnSpeed = Mathf.Clamp(value, 15f, 360f);
        }
        internal static void RequestHotApply(MagicaCharacterClothAuthoringDocument document)
        {
            s_runtime?.RequestHotApply(document);
        }
        internal static void SetPreviewWindEnabled(bool value) => s_runtime?.SetPreviewWindEnabled(value);
        internal static void SetPreviewWindStrength(float value) => s_runtime?.SetPreviewWindStrength(value);
        internal static void SetPreviewWindTurbulence(float value) => s_runtime?.SetPreviewWindTurbulence(value);
        internal static void SetPreviewWindYaw(float value) => s_runtime?.SetPreviewWindYaw(value);
        internal static void TriggerStressPulse() => s_runtime?.TriggerStressPulse();
        internal static bool TryGetActiveClothData(string clothId, out ClothSerializeData data)
        {
            data = IsActive ? s_runtime.FindClothData(clothId) : null;
            return data != null;
        }
        internal static void TogglePlayback() => s_runtime?.SetPlaying(!s_runtime.IsPlaying);
        internal static void Restart() => s_runtime?.Restart();
        internal static void ResetCloth() => s_runtime?.ResetCloth();

        internal static MagicaCharacterClothMotionPreviewPose EvaluateMotion(
            MagicaCharacterClothMotionPreset preset,
            float time)
        {
            return EvaluateMotion(preset, time, 0.8f, 4f, 1f, 90f);
        }

        internal static MagicaCharacterClothMotionPreviewPose EvaluateMotion(
            MagicaCharacterClothMotionPreset preset,
            float time,
            float distance,
            float period,
            float stopDuration,
            float turnSpeed)
        {
            float t = Mathf.Max(0f, time);
            float travel = Mathf.Clamp(distance, 0.1f, 2f);
            float cycle = Mathf.Clamp(period, 1f, 10f);
            float phase = t * (Mathf.PI * 2f / cycle);
            float angularSpeed = Mathf.Clamp(turnSpeed, 15f, 360f);
            switch (preset)
            {
                case MagicaCharacterClothMotionPreset.ForwardBackward:
                    return new MagicaCharacterClothMotionPreviewPose(
                        new Vector3(0f, 0f, Mathf.Sin(phase) * travel), 0f);
                case MagicaCharacterClothMotionPreset.Strafe:
                    return new MagicaCharacterClothMotionPreviewPose(
                        new Vector3(Mathf.Sin(phase) * travel, 0f, 0f), 0f);
                case MagicaCharacterClothMotionPreset.StartStop:
                    return new MagicaCharacterClothMotionPreviewPose(
                        new Vector3(0f, 0f, EvaluateStartStop(t, cycle, stopDuration) * travel), 0f);
                case MagicaCharacterClothMotionPreset.Turn90:
                    return new MagicaCharacterClothMotionPreviewPose(
                        Vector3.zero,
                        EvaluateTurn(t, 45f, angularSpeed));
                case MagicaCharacterClothMotionPreset.Turn180:
                    return new MagicaCharacterClothMotionPreviewPose(
                        Vector3.zero,
                        EvaluateTurn(t, 90f, angularSpeed));
                case MagicaCharacterClothMotionPreset.ZigZag:
                    return new MagicaCharacterClothMotionPreviewPose(
                        new Vector3(Mathf.Sin(phase * 2f) * travel * 0.7f, 0f, Mathf.Sin(phase) * travel * 0.55f),
                        Mathf.Sin(phase) * Mathf.Min(35f, angularSpeed * cycle * 0.125f));
                default:
                    return new MagicaCharacterClothMotionPreviewPose(Vector3.zero, 0f);
            }
        }

        private static float EvaluateStartStop(float time, float period, float stopDuration)
        {
            float stop = Mathf.Clamp(stopDuration, 0f, period * 0.45f);
            float travel = Mathf.Max(0.05f, (period - stop * 2f) * 0.5f);
            float cycle = travel * 2f + stop * 2f;
            float phase = Mathf.Repeat(time, cycle);
            if (phase < travel) return Mathf.Lerp(-1f, 1f, phase / travel);
            phase -= travel;
            if (phase < stop) return 1f;
            phase -= stop;
            if (phase < travel) return Mathf.Lerp(1f, -1f, phase / travel);
            return -1f;
        }

        private static float EvaluateTurn(float time, float halfAngle, float turnSpeed)
        {
            return Mathf.PingPong(time * turnSpeed + halfAngle, halfAngle * 2f) - halfAngle;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && IsRequested)
                EditorApplication.delayCall += CreateRuntime;
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                DisposeRuntime();
                SessionState.SetBool(RequestedKey, false);
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                DisposeRuntime();
                CleanupOrphanedPreviewEnvironments();
                s_status = string.Empty;
            }
        }

        private static void CreateRuntime()
        {
            EditorApplication.delayCall -= CreateRuntime;
            if (!EditorApplication.isPlaying || !IsRequested || s_runtime != null) return;
            string packageId = SessionState.GetString(PackageIdKey, string.Empty);
            try
            {
                CleanupOrphanedPreviewEnvironments();
                s_runtime = new MotionPreviewRuntime(packageId, PreviewLayer);
                s_status = s_runtime.Status;
                EditorApplication.ExecuteMenuItem("Window/General/Game");
            }
            catch (Exception exception)
            {
                s_status = "运动预览启动失败：" + exception.Message;
                DisposeRuntime();
                CleanupOrphanedPreviewEnvironments();
                SessionState.SetBool(RequestedKey, false);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }

        private static void Update()
        {
            if (s_runtime == null) return;
            if (!EditorApplication.isPlaying)
            {
                DisposeRuntime();
                return;
            }
            s_runtime.Update();
        }

        private static void DisposeRuntime()
        {
            s_runtime?.Dispose();
            s_runtime = null;
        }

        internal static int CleanupOrphanedPreviewEnvironments()
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            int removed = 0;
            for (int objectIndex = 0; objectIndex < objects.Length; objectIndex++)
            {
                GameObject candidate = objects[objectIndex];
                if (!IsOwnedPreviewEnvironment(candidate)) continue;
                candidate.SetActive(false);
                if (EditorApplication.isPlaying) Object.Destroy(candidate);
                else Object.DestroyImmediate(candidate);
                removed++;
            }
            return removed;
        }

        private static bool IsOwnedPreviewEnvironment(GameObject candidate)
        {
            return candidate != null
                && candidate.scene.IsValid()
                && candidate.transform.parent == null
                && candidate.layer == PreviewLayer
                && string.Equals(candidate.name, PreviewEnvironmentName, StringComparison.Ordinal)
                && (candidate.hideFlags & PreviewEnvironmentHideFlags) == PreviewEnvironmentHideFlags;
        }

        internal static int ResolveRememberedClipIndex(
            IReadOnlyList<MagicaCharacterClothMotionPreviewClip> clips,
            string rememberedClipId)
        {
            if (clips == null || clips.Count == 0) return 0;
            if (!string.IsNullOrWhiteSpace(rememberedClipId))
            {
                for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
                    if (string.Equals(
                            StableClipId(clips[clipIndex]),
                            rememberedClipId,
                            StringComparison.Ordinal))
                        return clipIndex;
            }
            return 0;
        }

        private static string StableClipId(MagicaCharacterClothMotionPreviewClip clip)
        {
            if (clip == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(clip.ClipId)) return clip.ClipId;
            if (!string.IsNullOrWhiteSpace(clip.RuntimeResourceKey)) return clip.RuntimeResourceKey;
            return clip.AssetPath ?? string.Empty;
        }

        private static string SelectedClipPreferenceKey(string packageId) =>
            SelectedClipPreferenceKeyPrefix + (packageId ?? string.Empty);

        private sealed class MotionPreviewRuntime : IDisposable
        {
            private readonly IDisposable _budgetPreviewScope;
            private readonly List<MagicaCharacterClothMotionPreviewClip> _clips;
            private readonly string[] _clipLabels;
            private readonly GameObject _environment;
            private readonly GameObject _actor;
            private readonly string _packageId;
            private readonly Animator _animator;
            private readonly Camera _camera;
            private readonly MagicaCharacterClothSystem _clothSystem;
            private readonly Transform _bodyModel;
            private readonly GameObject _windObject;
            private readonly MagicaWindZone _windZone;
            private MagicaCloth[] _cloths;
            private ColliderComponent[] _colliders;
            private ColliderWireRenderer _colliderWires;
            private readonly MotionGuideRenderer _motionGuides;
            private PlayableGraph _graph;
            private AnimationClipPlayable _clipPlayable;
            private double _lastUpdateTime;
            private float _clipTime;
            private float _motionTime;
            private int _selectedClipIndex;
            private bool _disposed;
            private Vector3 _motionPosition;
            private Vector3 _motionVelocity;
            private Vector3 _motionAcceleration;
            private Vector3 _previousMotionPosition;
            private Vector3 _previousMotionVelocity;
            private float _previousMotionYaw;
            private float _motionAngularVelocity;
            private bool _hasMotionSample;
            private MagicaCharacterClothAuthoringDocument _pendingAuthoringDocument;
            private MagicaCharacterClothAuthoringDocument _lastAppliedAuthoringDocument;
            private double _hotApplyAt;
            private int _hotApplyRevision;
            private string _hotApplyStatus = string.Empty;
            private bool _previewWindEnabled;
            private bool _stressPulseActive;
            private int _stressPulsePhase;
            private double _stressPulseStartedAt;
            private MagicaCharacterClothMotionPreset _savedMotionPreset;
            private float _savedMotionDistance;
            private float _savedMotionPeriod;
            private float _savedStopDuration;
            private float _savedTurnSpeed;

            internal MotionPreviewRuntime(string packageId, int layer)
            {
                _packageId = packageId ?? string.Empty;
                MagicaCharacterClothMotionPreviewCatalogResult catalog =
                    MagicaCharacterClothMotionPreviewCatalog.Load(packageId);
                if (!catalog.Succeeded) throw new InvalidOperationException(catalog.Error);
                _clips = catalog.Clips;
                _clipLabels = new string[_clips.Count];
                for (int i = 0; i < _clips.Count; i++)
                    _clipLabels[i] = _clips[i].DisplayName + "  ·  " + _clips[i].ClipId;

                string resourcePath = "MxFrameworkGenerated/CharacterPackages/" + packageId
                    + "/prefabs/" + packageId + "_character_preview";
                GameObject prefab = Resources.Load<GameObject>(resourcePath);
                if (prefab == null)
                    throw new InvalidOperationException("运行时角色 Prefab 不存在：" + resourcePath);

                _environment = new GameObject(PreviewEnvironmentName)
                {
                    hideFlags = PreviewEnvironmentHideFlags,
                    layer = layer,
                };
                _actor = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, _environment.transform);
                _actor.name = "[Cloth Motion Preview] " + packageId;
                SetLayerRecursively(_actor, layer);
                _animator = _actor.GetComponentInChildren<Animator>(true);
                _clothSystem = _actor.GetComponent<MagicaCharacterClothSystem>();
                _bodyModel = FindBodyModel(_actor.transform);
                _cloths = _actor.GetComponentsInChildren<MagicaCloth>(true);
                _colliders = _actor.GetComponentsInChildren<ColliderComponent>(true);
                if (_animator == null) throw new InvalidOperationException("运行时角色缺少 Animator。");
                if (_clothSystem == null) throw new InvalidOperationException("运行时角色缺少 Magica Cloth System。");
                if (_bodyModel == null) throw new InvalidOperationException("运行时角色缺少 BodyModel。");

                _camera = CreateCamera(_environment.transform, layer);
                CreateLight(_environment.transform, layer);
                CreateGround(_environment.transform, layer);
                (_windObject, _windZone) = CreateWind(_environment.transform, layer);
                _colliderWires = new ColliderWireRenderer(
                    _environment.transform,
                    _colliders,
                    layer);
                _motionGuides = new MotionGuideRenderer(_environment.transform, layer);
                SelectClip(ResolveRememberedClipIndex(
                    _clips,
                    EditorPrefs.GetString(SelectedClipPreferenceKey(_packageId), string.Empty)));
                _lastUpdateTime = EditorApplication.timeSinceStartup;
                _budgetPreviewScope = MagicaClothSimulationBudget.SuspendForIndependentPreview();
            }

            internal string[] ClipLabels => _clipLabels;
            internal int SelectedClipIndex => _selectedClipIndex;
            internal bool IsPlaying { get; private set; } = true;
            internal bool Loop { get; set; } = true;
            internal bool ShowColliders { get; set; }
            internal float PlaybackSpeed { get; set; } = 1f;
            internal float MotionDistance { get; set; } = 0.8f;
            internal float MotionPeriod { get; set; } = 4f;
            internal float StopDuration { get; set; } = 1f;
            internal float TurnSpeed { get; set; } = 90f;
            internal bool ShowMotionGuides { get; set; } = true;
            internal MagicaCharacterClothMotionPreset MotionPreset { get; set; } =
                MagicaCharacterClothMotionPreset.InPlace;
            internal MagicaCharacterClothMotionCameraView CameraView { get; set; } =
                MagicaCharacterClothMotionCameraView.Back;
            internal MagicaCharacterClothMotionCameraMode CameraMode { get; set; } =
                MagicaCharacterClothMotionCameraMode.FixedWorld;
            internal float NormalizedTime => CurrentClipLength <= 0f ? 0f : Mathf.Clamp01(_clipTime / CurrentClipLength);
            internal int ClothCount => _cloths.Length;
            internal int ColliderCount => _colliders.Length;
            internal bool HasCamera => _camera != null && _camera.enabled;
            internal bool HasMotionGuides => _motionGuides != null && _motionGuides.IsValid;
            internal Transform BodyModel => _bodyModel;
            internal Vector3 MotionPosition => _motionPosition;
            internal Vector3 MotionVelocity => _motionVelocity;
            internal Vector3 MotionAcceleration => _motionAcceleration;
            internal float MotionAngularVelocity => _motionAngularVelocity;
            internal bool IsHotApplyPending => _pendingAuthoringDocument != null;
            internal int HotApplyRevision => _hotApplyRevision;
            internal string HotApplyStatus => _hotApplyStatus;
            internal bool PreviewWindEnabled => _previewWindEnabled;
            internal float PreviewWindStrength => _windZone != null ? _windZone.main : 4f;
            internal float PreviewWindTurbulence => _windZone != null ? _windZone.turbulence : 0.5f;
            internal float PreviewWindYaw => _windZone != null ? _windZone.directionAngleY : 0f;
            internal bool HasActivePreviewWind => _previewWindEnabled && _windZone != null && _windZone.main > 0.001f;
            internal bool IsStressPulseActive => _stressPulseActive;
            internal string StressPulseStatus => !_stressPulseActive
                ? string.Empty
                : _stressPulsePhase == 0 ? "压力脉冲：急停" : "压力脉冲：转身";
            internal string Status
            {
                get
                {
                    bool ready = true;
                    for (int i = 0; i < _cloths.Length; i++) ready &= _cloths[i] != null && _cloths[i].IsValid();
                    string result = (ready ? "Magica Ready" : "Magica Building")
                        + " · Cloth " + ClothCount
                        + " · Collider " + ColliderCount;
                    return string.IsNullOrWhiteSpace(_hotApplyStatus)
                        ? result
                        : result + " · " + _hotApplyStatus;
                }
            }

            private float CurrentClipLength =>
                _clips.Count == 0 ? 0f : Mathf.Max(0.0001f, _clips[_selectedClipIndex].Clip.length);

            internal void Update()
            {
                if (_disposed) return;
                double now = EditorApplication.timeSinceStartup;
                float delta = Mathf.Clamp((float)(now - _lastUpdateTime), 0f, 0.05f);
                _lastUpdateTime = now;
                ApplyPendingAuthoring(now);
                UpdateStressPulse(now);
                if (IsPlaying)
                {
                    _clipTime += delta * PlaybackSpeed;
                    _motionTime += delta * PlaybackSpeed;
                    if (_clipTime >= CurrentClipLength)
                    {
                        if (Loop) _clipTime = Mathf.Repeat(_clipTime, CurrentClipLength);
                        else
                        {
                            _clipTime = CurrentClipLength;
                            SetPlaying(false);
                        }
                    }
                    ApplyAnimationTime();
                    ApplyMotionPose(delta);
                }
                UpdateCamera();
                _colliderWires.SetVisible(ShowColliders);
                _colliderWires.Update();
                _motionGuides.SetVisible(ShowMotionGuides);
                _motionGuides.Update(
                    MotionPreset,
                    _motionTime,
                    MotionDistance,
                    MotionPeriod,
                    StopDuration,
                    TurnSpeed,
                    _motionPosition);
                _motionGuides.UpdateWind(
                    HasActivePreviewWind,
                    _windZone != null ? _windZone.GetWindDirection() : Vector3.forward,
                    PreviewWindStrength,
                    _motionPosition);
            }

            internal void SelectClip(int index)
            {
                if (_clips.Count == 0) return;
                int selected = Mathf.Clamp(index, 0, _clips.Count - 1);
                try
                {
                    ApplySelectedClip(selected);
                }
                catch (Exception) when (selected != 0)
                {
                    ApplySelectedClip(0);
                }
            }

            private void ApplySelectedClip(int index)
            {
                _selectedClipIndex = index;
                RebuildAnimationGraph(_clips[index].Clip);
                Restart();
                string clipId = StableClipId(_clips[index]);
                string key = SelectedClipPreferenceKey(_packageId);
                if (!string.Equals(EditorPrefs.GetString(key, string.Empty), clipId, StringComparison.Ordinal))
                    EditorPrefs.SetString(key, clipId);
            }

            internal void SetPlaying(bool playing)
            {
                IsPlaying = playing;
                _clothSystem.SetPaused(!playing);
                if (!playing)
                {
                    _motionVelocity = Vector3.zero;
                    _motionAcceleration = Vector3.zero;
                    _motionAngularVelocity = 0f;
                    _hasMotionSample = false;
                }
                _lastUpdateTime = EditorApplication.timeSinceStartup;
            }

            internal void Restart()
            {
                _clipTime = 0f;
                _motionTime = 0f;
                _hasMotionSample = false;
                ApplyAnimationTime();
                ApplyMotionPose(0f);
                ResetCloth();
                SetPlaying(true);
            }

            internal void ResetCloth() => _clothSystem.ResetAll(keepPose: false);

            internal void RequestHotApply(MagicaCharacterClothAuthoringDocument document)
            {
                if (document == null) return;
                _pendingAuthoringDocument = document.Clone();
                _hotApplyAt = EditorApplication.timeSinceStartup + 0.08d;
                _hotApplyStatus = "草稿等待热应用";
            }

            internal void SetPreviewWindEnabled(bool value)
            {
                _previewWindEnabled = value;
                if (_windObject != null) _windObject.SetActive(value);
            }

            internal void SetPreviewWindStrength(float value)
            {
                if (_windZone != null) _windZone.main = Mathf.Clamp(value, 0f, 30f);
            }

            internal void SetPreviewWindTurbulence(float value)
            {
                if (_windZone != null) _windZone.turbulence = Mathf.Clamp01(value);
            }

            internal void SetPreviewWindYaw(float value)
            {
                if (_windZone != null) _windZone.directionAngleY = Mathf.Clamp(value, -180f, 180f);
            }

            internal void TriggerStressPulse()
            {
                if (_stressPulseActive) return;
                _savedMotionPreset = MotionPreset;
                _savedMotionDistance = MotionDistance;
                _savedMotionPeriod = MotionPeriod;
                _savedStopDuration = StopDuration;
                _savedTurnSpeed = TurnSpeed;
                _stressPulseActive = true;
                _stressPulsePhase = 0;
                _stressPulseStartedAt = EditorApplication.timeSinceStartup;
                MotionPreset = MagicaCharacterClothMotionPreset.StartStop;
                MotionDistance = 1.2f;
                MotionPeriod = 2.4f;
                StopDuration = 0.3f;
                TurnSpeed = 180f;
                Restart();
            }

            internal ClothSerializeData FindClothData(string clothId)
            {
                string expected = "MagicaCloth_" + SanitizeName(clothId);
                for (int i = 0; i < _cloths.Length; i++)
                    if (_cloths[i] != null && string.Equals(_cloths[i].name, expected, StringComparison.Ordinal))
                        return _cloths[i].SerializeData;
                return _cloths.Length > 0 && _cloths[0] != null ? _cloths[0].SerializeData : null;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_graph.IsValid()) _graph.Destroy();
                _colliderWires.Dispose();
                _motionGuides.Dispose();
                if (_environment != null) _environment.SetActive(false);
                DestroyObject(_environment);
                _budgetPreviewScope?.Dispose();
            }

            private void ApplyPendingAuthoring(double now)
            {
                if (_pendingAuthoringDocument == null || now < _hotApplyAt) return;
                MagicaCharacterClothAuthoringDocument requested = _pendingAuthoringDocument;
                _pendingAuthoringDocument = null;

                MagicaCharacterClothValidationResult validation =
                    MagicaCharacterClothAuthoringValidator.Validate(requested, _bodyModel);
                if (validation.HasErrors)
                {
                    _hotApplyStatus = "热应用已阻断：" + validation.Summary;
                    return;
                }

                _hotApplyStatus = "正在重建 Cloth…";
                _colliderWires?.Dispose();
                if (MagicaCharacterClothMotionPreviewHotApply.TryApply(
                        _actor,
                        _bodyModel,
                        requested,
                        out string error))
                {
                    RefreshHotAppliedComponents();
                    _lastAppliedAuthoringDocument = requested.Clone();
                    _hotApplyRevision++;
                    _hotApplyStatus = "草稿已实时应用 r" + _hotApplyRevision;
                    return;
                }

                string rollbackError = string.Empty;
                if (_lastAppliedAuthoringDocument != null)
                {
                    MagicaCharacterClothMotionPreviewHotApply.TryApply(
                        _actor,
                        _bodyModel,
                        _lastAppliedAuthoringDocument,
                        out rollbackError);
                }
                RefreshHotAppliedComponents();
                _hotApplyStatus = string.IsNullOrWhiteSpace(rollbackError)
                    ? "热应用失败：" + error
                    : "热应用失败且回滚失败：" + error + " / " + rollbackError;
            }

            private void UpdateStressPulse(double now)
            {
                if (!_stressPulseActive) return;
                double elapsed = now - _stressPulseStartedAt;
                if (_stressPulsePhase == 0 && elapsed >= 2d)
                {
                    _stressPulsePhase = 1;
                    MotionPreset = MagicaCharacterClothMotionPreset.Turn180;
                    MotionDistance = 0f;
                    TurnSpeed = 180f;
                    Restart();
                    return;
                }
                if (elapsed < 4d) return;
                _stressPulseActive = false;
                MotionPreset = _savedMotionPreset;
                MotionDistance = _savedMotionDistance;
                MotionPeriod = _savedMotionPeriod;
                StopDuration = _savedStopDuration;
                TurnSpeed = _savedTurnSpeed;
                Restart();
            }

            private void RefreshHotAppliedComponents()
            {
                _cloths = _actor.GetComponentsInChildren<MagicaCloth>(true);
                _colliders = _actor.GetComponentsInChildren<ColliderComponent>(true);
                _colliderWires = new ColliderWireRenderer(
                    _environment.transform,
                    _colliders,
                    PreviewLayer);
                _colliderWires.SetVisible(ShowColliders);
                _clothSystem.ResetAll(keepPose: false);
            }

            private void RebuildAnimationGraph(AnimationClip clip)
            {
                if (_graph.IsValid()) _graph.Destroy();
                _animator.enabled = true;
                _animator.applyRootMotion = false;
                _animator.Rebind();
                _animator.Update(0f);
                _graph = PlayableGraph.Create("Character Cloth Motion Preview");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                _clipPlayable = AnimationClipPlayable.Create(_graph, clip);
                _clipPlayable.SetApplyFootIK(false);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Character", _animator);
                output.SetSourcePlayable(_clipPlayable);
                _graph.Play();
            }

            private void ApplyAnimationTime()
            {
                if (!_graph.IsValid() || !_clipPlayable.IsValid()) return;
                _clipPlayable.SetTime(_clipTime);
                _graph.Evaluate(0f);
            }

            private void ApplyMotionPose(float deltaTime)
            {
                MagicaCharacterClothMotionPreviewPose pose = EvaluateMotion(
                    MotionPreset,
                    _motionTime,
                    MotionDistance,
                    MotionPeriod,
                    StopDuration,
                    TurnSpeed);
                _actor.transform.SetPositionAndRotation(pose.Position, Quaternion.Euler(0f, pose.Yaw, 0f));
                _motionPosition = pose.Position;
                if (_hasMotionSample && deltaTime > 0.00001f)
                {
                    _motionVelocity = (pose.Position - _previousMotionPosition) / deltaTime;
                    _motionAcceleration = (_motionVelocity - _previousMotionVelocity) / deltaTime;
                    _motionAngularVelocity = Mathf.DeltaAngle(_previousMotionYaw, pose.Yaw) / deltaTime;
                }
                else
                {
                    _motionVelocity = Vector3.zero;
                    _motionAcceleration = Vector3.zero;
                    _motionAngularVelocity = 0f;
                }
                _previousMotionPosition = pose.Position;
                _previousMotionVelocity = _motionVelocity;
                _previousMotionYaw = pose.Yaw;
                _hasMotionSample = true;
            }

            private void UpdateCamera()
            {
                if (_camera == null || _actor == null) return;
                Vector3 localOffset;
                switch (CameraView)
                {
                    case MagicaCharacterClothMotionCameraView.Front: localOffset = new Vector3(0f, 1.3f, 3.2f); break;
                    case MagicaCharacterClothMotionCameraView.Left: localOffset = new Vector3(-3.2f, 1.3f, 0f); break;
                    case MagicaCharacterClothMotionCameraView.Right: localOffset = new Vector3(3.2f, 1.3f, 0f); break;
                    default: localOffset = new Vector3(0f, 1.3f, -3.2f); break;
                }
                Transform actor = _actor.transform;
                bool follow = CameraMode == MagicaCharacterClothMotionCameraMode.FollowActor;
                Vector3 targetPosition = follow ? actor.position : Vector3.zero;
                Quaternion referenceRotation = follow ? actor.rotation : Quaternion.identity;
                float distanceScale = follow ? 1f : 1.45f;
                _camera.transform.position = targetPosition + referenceRotation * (localOffset * distanceScale);
                _camera.transform.rotation = Quaternion.LookRotation(
                    targetPosition + Vector3.up * 1.15f - _camera.transform.position,
                    Vector3.up);
            }

            private static Camera CreateCamera(Transform parent, int layer)
            {
                var gameObject = new GameObject("Cloth Motion Preview Camera") { layer = layer };
                gameObject.transform.SetParent(parent, false);
                Camera camera = gameObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.13f, 0.16f, 0.21f, 1f);
                camera.cullingMask = 1 << layer;
                camera.depth = 1000f;
                camera.fieldOfView = 36f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 100f;
                return camera;
            }

            private static (GameObject, MagicaWindZone) CreateWind(Transform parent, int layer)
            {
                var gameObject = new GameObject("Cloth Motion Preview Wind") { layer = layer };
                gameObject.SetActive(false);
                gameObject.transform.SetParent(parent, false);
                MagicaWindZone wind = gameObject.AddComponent<MagicaWindZone>();
                wind.mode = MagicaWindZone.Mode.GlobalDirection;
                wind.main = 4f;
                wind.turbulence = 0.5f;
                wind.directionAngleX = 0f;
                wind.directionAngleY = 0f;
                wind.isAddition = true;
                return (gameObject, wind);
            }

            private static string SanitizeName(string value)
            {
                char[] chars = (value ?? string.Empty).ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    char ch = chars[i];
                    if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '-') chars[i] = '_';
                }
                return new string(chars);
            }

            private static Transform FindBodyModel(Transform actor)
            {
                Transform[] transforms = actor.GetComponentsInChildren<Transform>(includeInactive: true);
                for (int i = 0; i < transforms.Length; i++)
                    if (string.Equals(transforms[i].name, "BodyModel", StringComparison.Ordinal))
                        return transforms[i];
                return null;
            }

            private static void CreateLight(Transform parent, int layer)
            {
                var gameObject = new GameObject("Cloth Motion Preview Light") { layer = layer };
                gameObject.transform.SetParent(parent, false);
                gameObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
                Light light = gameObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.cullingMask = 1 << layer;
            }

            private static void CreateGround(Transform parent, int layer)
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Cloth Motion Preview Ground";
                ground.layer = layer;
                ground.transform.SetParent(parent, false);
                ground.transform.localScale = Vector3.one * 0.5f;
                Collider collider = ground.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
                Renderer renderer = ground.GetComponent<Renderer>();
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (renderer != null && shader != null)
                {
                    var material = new Material(shader)
                    {
                        hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                        color = new Color(0.18f, 0.21f, 0.25f, 1f),
                    };
                    renderer.sharedMaterial = material;
                }
            }

            private static void SetLayerRecursively(GameObject root, int layer)
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layer;
            }

            private static void DestroyObject(Object value)
            {
                if (value == null) return;
                if (EditorApplication.isPlaying) Object.Destroy(value);
                else Object.DestroyImmediate(value);
            }
        }

        private sealed class MotionGuideRenderer : IDisposable
        {
            private const int PathSamples = 97;
            private const int MarkerSegments = 32;
            private const float GridExtent = 3f;
            private const float GridStep = 0.5f;
            private readonly GameObject _root;
            private readonly Material _gridMaterial;
            private readonly Material _pathMaterial;
            private readonly Material _markerMaterial;
            private readonly LineRenderer _path;
            private readonly LineRenderer _marker;
            private readonly LineRenderer _heading;
            private readonly LineRenderer _wind;
            private readonly LineRenderer _windArrowA;
            private readonly LineRenderer _windArrowB;
            private readonly Vector3[] _pathPoints = new Vector3[PathSamples];
            private readonly Vector3[] _markerPoints = new Vector3[MarkerSegments];
            private bool _visible = true;

            internal MotionGuideRenderer(Transform parent, int layer)
            {
                _root = new GameObject("Cloth Motion Guides") { layer = layer };
                _root.transform.SetParent(parent, false);
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("Hidden/Internal-Colored");
                _gridMaterial = CreateMaterial(shader, new Color(0.35f, 0.42f, 0.50f, 0.7f));
                _pathMaterial = CreateMaterial(shader, new Color(0.12f, 0.75f, 1f, 1f));
                _markerMaterial = CreateMaterial(shader, new Color(1f, 0.72f, 0.12f, 1f));
                CreateGrid(layer);
                CreateDistanceLabels(layer);
                _path = CreateLine("Motion Path", _root.transform, _pathMaterial, layer, PathSamples, false, 0.015f);
                _marker = CreateLine("Motion Position", _root.transform, _markerMaterial, layer, MarkerSegments, true, 0.025f);
                _heading = CreateLine("Motion Heading", _root.transform, _markerMaterial, layer, 2, false, 0.025f);
                _wind = CreateLine("Preview Wind", _root.transform, _markerMaterial, layer, 2, false, 0.035f);
                _windArrowA = CreateLine("Preview Wind Arrow", _root.transform, _markerMaterial, layer, 2, false, 0.035f);
                _windArrowB = CreateLine("Preview Wind Arrow", _root.transform, _markerMaterial, layer, 2, false, 0.035f);
                SetWindVisible(false);
            }

            internal bool IsValid => _root != null && _path != null && _marker != null && _heading != null;

            internal void SetVisible(bool visible)
            {
                if (_visible == visible) return;
                _visible = visible;
                if (_root != null) _root.SetActive(visible);
            }

            internal void Update(
                MagicaCharacterClothMotionPreset preset,
                float time,
                float distance,
                float period,
                float stopDuration,
                float turnSpeed,
                Vector3 position)
            {
                if (!_visible || !IsValid) return;
                float previewDuration = Mathf.Max(1f, period);
                for (int i = 0; i < PathSamples; i++)
                {
                    float sampleTime = previewDuration * i / (PathSamples - 1f);
                    MagicaCharacterClothMotionPreviewPose sample = EvaluateMotion(
                        preset,
                        sampleTime,
                        distance,
                        period,
                        stopDuration,
                        turnSpeed);
                    _pathPoints[i] = sample.Position + Vector3.up * 0.025f;
                }
                _path.SetPositions(_pathPoints);

                MagicaCharacterClothMotionPreviewPose current = EvaluateMotion(
                    preset,
                    time,
                    distance,
                    period,
                    stopDuration,
                    turnSpeed);
                for (int i = 0; i < MarkerSegments; i++)
                {
                    float radians = i * (Mathf.PI * 2f / MarkerSegments);
                    _markerPoints[i] = position
                        + new Vector3(Mathf.Cos(radians) * 0.11f, 0.035f, Mathf.Sin(radians) * 0.11f);
                }
                _marker.SetPositions(_markerPoints);
                Vector3 heading = Quaternion.Euler(0f, current.Yaw, 0f) * Vector3.forward;
                _heading.SetPosition(0, position + Vector3.up * 0.04f);
                _heading.SetPosition(1, position + heading * 0.38f + Vector3.up * 0.04f);
            }

            internal void UpdateWind(
                bool visible,
                Vector3 direction,
                float strength,
                Vector3 targetPosition)
            {
                SetWindVisible(visible);
                if (!visible) return;
                Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
                float length = Mathf.Lerp(0.55f, 1.25f, Mathf.Clamp01(strength / 10f));
                Vector3 center = targetPosition + Vector3.up * 2.05f;
                Vector3 start = center - flat * (length * 0.5f);
                Vector3 end = center + flat * (length * 0.5f);
                Vector3 side = Vector3.Cross(Vector3.up, flat).normalized;
                _wind.SetPosition(0, start);
                _wind.SetPosition(1, end);
                _windArrowA.SetPosition(0, end);
                _windArrowA.SetPosition(1, end - flat * 0.28f + side * 0.18f);
                _windArrowB.SetPosition(0, end);
                _windArrowB.SetPosition(1, end - flat * 0.28f - side * 0.18f);
            }

            public void Dispose()
            {
                if (_root != null) Object.Destroy(_root);
                if (_gridMaterial != null) Object.Destroy(_gridMaterial);
                if (_pathMaterial != null) Object.Destroy(_pathMaterial);
                if (_markerMaterial != null) Object.Destroy(_markerMaterial);
            }

            private void CreateGrid(int layer)
            {
                int lineCount = Mathf.RoundToInt(GridExtent * 2f / GridStep);
                for (int i = 0; i <= lineCount; i++)
                {
                    float coordinate = -GridExtent + i * GridStep;
                    bool axis = Mathf.Abs(coordinate) < 0.001f;
                    float width = axis ? 0.018f : 0.006f;
                    LineRenderer xLine = CreateLine("Grid X", _root.transform, _gridMaterial, layer, 2, false, width);
                    xLine.SetPosition(0, new Vector3(-GridExtent, 0.012f, coordinate));
                    xLine.SetPosition(1, new Vector3(GridExtent, 0.012f, coordinate));
                    LineRenderer zLine = CreateLine("Grid Z", _root.transform, _gridMaterial, layer, 2, false, width);
                    zLine.SetPosition(0, new Vector3(coordinate, 0.012f, -GridExtent));
                    zLine.SetPosition(1, new Vector3(coordinate, 0.012f, GridExtent));
                }
            }

            private void CreateDistanceLabels(int layer)
            {
                for (int value = -3; value <= 3; value++)
                {
                    if (value == 0) continue;
                    CreateDistanceLabel(value + "m", new Vector3(value, 0.018f, 0.16f), layer);
                    CreateDistanceLabel(value + "m", new Vector3(0.16f, 0.018f, value), layer);
                }
            }

            private void CreateDistanceLabel(string text, Vector3 position, int layer)
            {
                var gameObject = new GameObject("Grid Distance " + text) { layer = layer };
                gameObject.transform.SetParent(_root.transform, false);
                gameObject.transform.localPosition = position;
                gameObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                TextMesh label = gameObject.AddComponent<TextMesh>();
                label.text = text;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.fontSize = 32;
                label.characterSize = 0.035f;
                label.color = new Color(0.55f, 0.65f, 0.75f, 0.9f);
            }

            private void SetWindVisible(bool visible)
            {
                if (_wind != null) _wind.gameObject.SetActive(visible);
                if (_windArrowA != null) _windArrowA.gameObject.SetActive(visible);
                if (_windArrowB != null) _windArrowB.gameObject.SetActive(visible);
            }

            private static Material CreateMaterial(Shader shader, Color color)
            {
                if (shader == null) return null;
                var material = new Material(shader)
                {
                    hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                    color = color,
                };
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                return material;
            }

            private static LineRenderer CreateLine(
                string name,
                Transform parent,
                Material material,
                int layer,
                int pointCount,
                bool loop,
                float width)
            {
                var gameObject = new GameObject(name) { layer = layer };
                gameObject.transform.SetParent(parent, false);
                LineRenderer line = gameObject.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.positionCount = pointCount;
                line.loop = loop;
                line.widthMultiplier = width;
                line.numCapVertices = 2;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                return line;
            }
        }

        private sealed class ColliderWireRenderer : IDisposable
        {
            private const int RingSegments = 32;
            private readonly List<ColliderWire> _wires = new List<ColliderWire>();
            private readonly GameObject _root;
            private readonly Material _material;
            private bool _visible;

            internal ColliderWireRenderer(Transform parent, ColliderComponent[] colliders, int layer)
            {
                _root = new GameObject("Cloth Collider Wires") { layer = layer };
                _root.transform.SetParent(parent, false);
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("Hidden/Internal-Colored");
                if (shader != null)
                {
                    _material = new Material(shader)
                    {
                        hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                        color = new Color(0.12f, 1f, 0.45f, 1f),
                    };
                    if (_material.HasProperty("_BaseColor"))
                        _material.SetColor("_BaseColor", new Color(0.12f, 1f, 0.45f, 1f));
                }
                for (int i = 0; colliders != null && i < colliders.Length; i++)
                    if (colliders[i] != null) _wires.Add(new ColliderWire(_root.transform, colliders[i], _material, layer));
                SetVisible(false);
            }

            internal void SetVisible(bool visible)
            {
                if (_visible == visible) return;
                _visible = visible;
                if (_root != null) _root.SetActive(visible);
            }

            internal void Update()
            {
                if (!_visible) return;
                for (int i = 0; i < _wires.Count; i++) _wires[i].Update();
            }

            public void Dispose()
            {
                if (_root != null) Object.Destroy(_root);
                if (_material != null) Object.Destroy(_material);
            }

            private sealed class ColliderWire
            {
                private readonly ColliderComponent _collider;
                private readonly LineRenderer[] _rings;
                private readonly LineRenderer[] _sides;
                private readonly Vector3[] _ringPoints = new Vector3[RingSegments];

                internal ColliderWire(Transform parent, ColliderComponent collider, Material material, int layer)
                {
                    _collider = collider;
                    bool capsule = collider is MagicaCapsuleCollider;
                    _rings = new LineRenderer[capsule ? 6 : 3];
                    for (int i = 0; i < _rings.Length; i++)
                        _rings[i] = CreateLine(parent, material, layer, RingSegments, loop: true);
                    _sides = new LineRenderer[capsule ? 8 : 0];
                    for (int i = 0; i < _sides.Length; i++)
                        _sides[i] = CreateLine(parent, material, layer, 2, loop: false);
                }

                internal void Update()
                {
                    if (_collider == null) return;
                    float scale = Mathf.Max(0.0001f, Mathf.Abs(_collider.GetScale()));
                    Vector3 center = _collider.transform.TransformPoint(_collider.center);
                    if (_collider is MagicaCapsuleCollider capsule)
                    {
                        UpdateCapsule(capsule, center, scale);
                        return;
                    }
                    float radius = Mathf.Max(0.001f, _collider.GetSize().x * scale);
                    Vector3 right = _collider.transform.right.normalized;
                    Vector3 up = _collider.transform.up.normalized;
                    Vector3 forward = _collider.transform.forward.normalized;
                    UpdateRing(_rings[0], center, right, up, radius);
                    UpdateRing(_rings[1], center, right, forward, radius);
                    UpdateRing(_rings[2], center, up, forward, radius);
                }

                private void UpdateCapsule(MagicaCapsuleCollider capsule, Vector3 center, float scale)
                {
                    Vector3 size = capsule.GetSize() * scale;
                    float startRadius = Mathf.Max(0.001f, size.x);
                    float endRadius = Mathf.Max(0.001f, size.y);
                    float length = Mathf.Max(0.001f, size.z);
                    Vector3 axis = capsule.transform.TransformDirection(capsule.GetLocalDir()).normalized;
                    Vector3 sideA = capsule.transform.TransformDirection(capsule.GetLocalUp()).normalized;
                    Vector3 sideB = Vector3.Cross(axis, sideA).normalized;
                    Vector3 start = center + axis * MagicaCharacterClothCapsuleGeometry.StartCenterOffset(length, startRadius);
                    Vector3 end = center - axis * MagicaCharacterClothCapsuleGeometry.EndCenterOffset(length, endRadius);
                    UpdateSphereRings(0, start, axis, sideA, sideB, startRadius);
                    UpdateSphereRings(3, end, axis, sideA, sideB, endRadius);
                    for (int i = 0; i < _sides.Length; i++)
                    {
                        float radians = i * 45f * Mathf.Deg2Rad;
                        Vector3 radial = sideA * Mathf.Cos(radians) + sideB * Mathf.Sin(radians);
                        _sides[i].SetPosition(0, start + radial * startRadius);
                        _sides[i].SetPosition(1, end + radial * endRadius);
                    }
                }

                private void UpdateSphereRings(
                    int startIndex,
                    Vector3 center,
                    Vector3 axis,
                    Vector3 sideA,
                    Vector3 sideB,
                    float radius)
                {
                    UpdateRing(_rings[startIndex], center, sideA, sideB, radius);
                    UpdateRing(_rings[startIndex + 1], center, axis, sideA, radius);
                    UpdateRing(_rings[startIndex + 2], center, axis, sideB, radius);
                }

                private void UpdateRing(LineRenderer line, Vector3 center, Vector3 axisA, Vector3 axisB, float radius)
                {
                    for (int i = 0; i < RingSegments; i++)
                    {
                        float radians = i * (Mathf.PI * 2f / RingSegments);
                        _ringPoints[i] = center
                            + axisA * (Mathf.Cos(radians) * radius)
                            + axisB * (Mathf.Sin(radians) * radius);
                    }
                    line.SetPositions(_ringPoints);
                }

                private static LineRenderer CreateLine(
                    Transform parent,
                    Material material,
                    int layer,
                    int pointCount,
                    bool loop)
                {
                    var gameObject = new GameObject("Collider Wire") { layer = layer };
                    gameObject.transform.SetParent(parent, false);
                    LineRenderer line = gameObject.AddComponent<LineRenderer>();
                    line.sharedMaterial = material;
                    line.useWorldSpace = true;
                    line.positionCount = pointCount;
                    line.loop = loop;
                    line.widthMultiplier = 0.008f;
                    line.startColor = new Color(0.12f, 1f, 0.45f, 1f);
                    line.endColor = line.startColor;
                    line.numCapVertices = 2;
                    line.shadowCastingMode = ShadowCastingMode.Off;
                    line.receiveShadows = false;
                    return line;
                }
            }
        }
    }
}
