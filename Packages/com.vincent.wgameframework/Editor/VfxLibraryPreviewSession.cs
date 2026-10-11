using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using Object = UnityEngine.Object;

namespace MxFramework.Editor
{
    internal readonly struct VfxPreviewCameraState
    {
        internal VfxPreviewCameraState(Vector2 orbit, float distance, Vector3 target)
        {
            Orbit = orbit;
            Distance = distance;
            Target = target;
        }

        internal Vector2 Orbit { get; }
        internal float Distance { get; }
        internal Vector3 Target { get; }
    }

    internal enum VfxPreviewMotionMode
    {
        Stationary,
        Linear,
        PingPong,
        Orbit,
        Parabola
    }

    /// <summary>Editor-only deterministic root motion used by VFX previews.</summary>
    /// <remarks>AllocByDesign. Evaluated only while an author explicitly previews or scrubs an effect.</remarks>
    internal readonly struct VfxPreviewMotionSettings
    {
        internal VfxPreviewMotionSettings(
            VfxPreviewMotionMode mode,
            float directionDegrees,
            float distance,
            float speed,
            float height,
            bool followCamera)
        {
            Mode = mode;
            DirectionDegrees = Mathf.Repeat(directionDegrees, 360f);
            Distance = Mathf.Clamp(distance, 0.5f, 30f);
            Speed = Mathf.Clamp(speed, 0.1f, 20f);
            Height = Mathf.Clamp(height, 0f, 15f);
            FollowCamera = followCamera;
        }

        internal static VfxPreviewMotionSettings Stationary =>
            new VfxPreviewMotionSettings(VfxPreviewMotionMode.Stationary, 0f, 8f, 4f, 3f, false);

        internal VfxPreviewMotionMode Mode { get; }
        internal float DirectionDegrees { get; }
        internal float Distance { get; }
        internal float Speed { get; }
        internal float Height { get; }
        internal bool FollowCamera { get; }
        internal bool IsMoving => Mode != VfxPreviewMotionMode.Stationary;

        internal Vector3 EvaluatePosition(float elapsedSeconds)
        {
            float radians = DirectionDegrees * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
            float time = Mathf.Max(0f, elapsedSeconds);
            switch (Mode)
            {
                case VfxPreviewMotionMode.Linear:
                {
                    float progress = Mathf.Clamp01(Speed * time / Distance);
                    return direction * (Distance * progress);
                }
                case VfxPreviewMotionMode.PingPong:
                {
                    float progress = Mathf.PingPong(Speed * time / Distance, 1f);
                    return direction * (Distance * progress);
                }
                case VfxPreviewMotionMode.Orbit:
                {
                    float radius = Distance * 0.5f;
                    float angle = radians + Speed * time / Mathf.Max(0.25f, radius);
                    return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                }
                case VfxPreviewMotionMode.Parabola:
                {
                    float progress = Mathf.Clamp01(Speed * time / Distance);
                    return direction * (Distance * progress)
                        + Vector3.up * (4f * Height * progress * (1f - progress));
                }
                default:
                    return Vector3.zero;
            }
        }
    }

    internal enum VfxLibraryPlaybackState { Stopped, Playing, Paused }

    internal sealed class VfxLibraryPreviewSession : IDisposable
    {
        private const int PreviewGridHalfSize = 20;
        private const float PreviewGridStep = 2f;
        private const float PreviewCameraMaximumDistance = 250f;
        internal const string GameplayPostProcessingProfilePath = "Assets/Config/MxFramework/Rendering/Profiles/MxGameplayVfxLook.asset";

        private sealed class CharacterPreviewEntry
        {
            internal GameObject Instance;
            internal Animator Animator;
            internal CharacterBoneRuntimeSync BoneSync;
            internal Renderer[] Renderers;
            internal bool[] RendererDefaults;
        }

        private static int s_animationModeUsers;
        private static bool s_startedAnimationMode;
        private PreviewRenderUtility _utility;
        private GameObject _instance;
        private GameObject _characterInstance;
        private Animator _characterAnimator;
        private CharacterBoneRuntimeSync _characterBoneSync;
        private AnimationClip _characterClip;
        private Renderer[] _characterRenderers = Array.Empty<Renderer>();
        private bool[] _characterRendererDefaults = Array.Empty<bool>();
        private Transform _attachmentTransform;
        private string _characterPrefabPath = string.Empty;
        private string _attachmentSocketId = string.Empty;
        private bool _characterAnimationLoop = true;
        private float _characterAnimationSpeed = 1f;
        private bool _characterVisible = true;
        private bool _usesAnimationMode;
        private readonly Dictionary<string, CharacterPreviewEntry> _characterCache = new Dictionary<string, CharacterPreviewEntry>(StringComparer.Ordinal);
        private ParticleSystem[] _particles = Array.Empty<ParticleSystem>();
        private ParticleSystem[] _particleSimulationRoots = Array.Empty<ParticleSystem>();
        private ParticleSystem.Particle[] _particleBoundsBuffer = Array.Empty<ParticleSystem.Particle>();
        private bool _frameOnFirstVisibleParticle;
        private Component[] _graphComponents = Array.Empty<Component>();
        private GameObject _temporaryOverlayInstance;
        private ParticleSystem[] _temporaryOverlayParticles = Array.Empty<ParticleSystem>();
        private ParticleSystem[] _temporaryOverlaySimulationRoots = Array.Empty<ParticleSystem>();
        private Component[] _temporaryOverlayGraphs = Array.Empty<Component>();
        private Renderer[] _compositionRenderers = Array.Empty<Renderer>();
        private bool[] _compositionRendererDefaults = Array.Empty<bool>();
        private bool _temporaryOverlaySolo;
        private UnityVfxLifecycleController _lifecycle;
        private GameObject _gridObject;
        private GameObject _rootAxesObject;
        private GameObject _socketAxesObject;
        private Mesh _gridMesh;
        private Mesh _rootAxesMesh;
        private Mesh _socketAxesMesh;
        private Material _helperMaterial;
        private Renderer _gridRenderer;
        private GameObject _postProcessVolumeObject;
        private VolumeProfile _postProcessProfile;
        private bool _ownsPostProcessProfile;
        private Volume _postProcessVolume;
        private UniversalAdditionalCameraData _cameraData;
        private Vector2 _orbit = new Vector2(25f, 20f);
        private Vector3 _rootPosition;
        private Vector3 _baseRootPosition;
        private Vector3 _previewMotionOffset;
        private VfxPreviewMotionSettings _previewMotion = VfxPreviewMotionSettings.Stationary;
        private Vector3 _cameraTarget = Vector3.up * 0.6f;
        private float _distance = 5f;
        private bool _cameraInitialized;
        private bool _paused;
        private bool _loop = true;
        private bool _stopAtTimelineEnd;
        private float _lifecycleAutoExitSeconds = -1f;
        private VfxStopMode _lifecycleAutoExitMode = VfxStopMode.Complete;
        private bool _lifecycleAutoExitTriggered;
        private float _speed = 1f;
        private Color _background = new Color(0.025f, 0.03f, 0.04f);
        private bool _postProcessingEnabled = true;
        private double _lastTime;
        public VfxLibraryPlaybackState State { get; private set; } = VfxLibraryPlaybackState.Stopped;
        public float ElapsedSeconds { get; private set; }
        public float EstimatedDurationSeconds { get; private set; } = 4f;
        public bool HasInstance => _instance != null;
        public bool HasTemporaryOverlay => _temporaryOverlayInstance != null;
        public bool HasPreviewContent => _instance != null || _temporaryOverlayInstance != null;
        public bool TemporaryOverlaySolo => _temporaryOverlaySolo;
        public bool HasCharacter => _characterInstance != null;
        internal string AttachmentSocketId => _attachmentSocketId;
        internal Vector3 AttachmentWorldPosition => _attachmentTransform != null ? _attachmentTransform.position : Vector3.zero;
        internal Quaternion AttachmentWorldRotation => _attachmentTransform != null ? _attachmentTransform.rotation : Quaternion.identity;
        internal Vector3 EffectWorldPosition => _instance != null ? _instance.transform.position : Vector3.zero;
        internal VfxPreviewMotionSettings PreviewMotion => _previewMotion;
        internal bool HasCharacterBoneSync => _characterBoneSync != null;
        internal GameObject ActiveCharacterInstance => _characterInstance;
        internal int CachedCharacterCount => _characterCache.Count;
        internal float CharacterAnimationSampleSeconds => CharacterAnimationTimeSeconds();
        public Vector3 RootPosition => _rootPosition;
        public bool GridVisible { get; private set; } = true;
        public string CameraMode { get; private set; } = "透视";
        internal Vector2 CameraOrbit => _orbit;
        internal float CameraDistance => _distance;
        internal Vector3 CameraTarget => _cameraTarget;
        internal Vector3 CameraWorldPosition => _utility?.camera != null
            ? _utility.camera.transform.position
            : _cameraTarget + Quaternion.Euler(_orbit.y, _orbit.x, 0f) * new Vector3(0f, 0f, -_distance);
        internal bool PostProcessingEnabled => _postProcessingEnabled;
        internal bool PostProcessingAvailable => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
        internal bool HasPostProcessingProfile => _postProcessProfile != null;
        internal bool UsesSharedPostProcessingProfile => _postProcessProfile != null
            && AssetDatabase.GetAssetPath(_postProcessProfile) == GameplayPostProcessingProfilePath;
        internal bool IsCameraPostProcessingEnabled => _cameraData != null && _cameraData.renderPostProcessing;
        internal int ParticleSimulationRootCount => _particleSimulationRoots.Length;
        internal int ActiveParticleSystemCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _particles.Length; i++)
                    if (_particles[i] != null && _particles[i].particleCount > 0) count++;
                return count;
            }
        }
        internal int TotalParticleCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _particles.Length; i++)
                    if (_particles[i] != null) count += _particles[i].particleCount;
                return count;
            }
        }

        public void Select(string assetPath)
        {
            DisposeEffectInstance();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null) return;
            Initialize(Object.Instantiate(prefab));
        }

        public void SelectAssets(IEnumerable<string> assetPaths)
        {
            DisposeEffectInstance();
            var root = new GameObject("VFX Composite Preview");
            int count = 0;
            foreach (string assetPath in assetPaths ?? Array.Empty<string>())
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;
                GameObject child = Object.Instantiate(prefab, root.transform, false);
                child.name = prefab.name;
                count++;
            }
            if (count == 0) { Object.DestroyImmediate(root); return; }
            Initialize(root);
        }

        /// <summary>Takes ownership of a temporary Editor-only preview root.</summary>
        /// <remarks>Editor-only, AllocByDesign. The instance is destroyed when selection changes or the session is disposed.</remarks>
        public void SelectInstance(GameObject instance)
        {
            DisposeEffectInstance();
            if (instance == null) return;
            Initialize(instance);
        }

        /// <summary>Replaces only the VFX subtree while preserving character context and camera state.</summary>
        /// <remarks>Editor-only, AllocByDesign.</remarks>
        public void ReplaceEffectInstance(GameObject instance)
        {
            DisposeEffectInstance();
            if (instance == null) return;
            Initialize(instance);
        }

        /// <summary>Temporarily overlays an Element on the composition preview without changing the draft.</summary>
        /// <remarks>Editor-only, AllocByDesign. Ownership of <paramref name="instance"/> transfers to this session.</remarks>
        public void SetTemporaryOverlay(GameObject instance)
        {
            ClearTemporaryOverlay();
            if (instance == null) return;
            EnsureUtility();
            _temporaryOverlayInstance = instance;
            _temporaryOverlayInstance.name += " (Temporary Audition)";
            _temporaryOverlayInstance.hideFlags = HideFlags.HideAndDontSave;
            _utility.AddSingleGO(_temporaryOverlayInstance);
            AttachTemporaryOverlay();
            _temporaryOverlayParticles = _temporaryOverlayInstance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < _temporaryOverlayParticles.Length; i++)
            {
                _temporaryOverlayParticles[i].useAutoRandomSeed = false;
                _temporaryOverlayParticles[i].randomSeed = unchecked(17017u + (uint)i * 7919u);
            }
            _temporaryOverlaySimulationRoots = BuildParticleSimulationRoots(_temporaryOverlayParticles);
            _temporaryOverlayGraphs = FindGraphComponents(_temporaryOverlayInstance);
            ApplyTemporaryOverlayMode();
            RestartTemporaryOverlay();
        }

        /// <summary>Controls whether the temporary Element is shown alone or together with the composition.</summary>
        /// <remarks>Editor-only, AllocByDesign. This only changes preview renderer visibility and never changes the draft.</remarks>
        public void SetTemporaryOverlaySolo(bool value)
        {
            _temporaryOverlaySolo = value;
            ApplyTemporaryOverlayMode();
        }

        public void RestartTemporaryOverlay()
        {
            if (_temporaryOverlayInstance == null) return;
            RestartParticleTrees(
                _temporaryOverlayParticles,
                _temporaryOverlaySimulationRoots);
            InvokeGraphs(_temporaryOverlayGraphs, "Reinit");
            SetGraphPaused(_temporaryOverlayGraphs, false);
            InvokeGraphs(_temporaryOverlayGraphs, "Play");
            _paused = false;
            State = VfxLibraryPlaybackState.Playing;
            _lastTime = EditorApplication.timeSinceStartup;
        }

        public void ClearTemporaryOverlay()
        {
            if (_temporaryOverlayInstance != null) Object.DestroyImmediate(_temporaryOverlayInstance);
            _temporaryOverlayInstance = null;
            _temporaryOverlayParticles = Array.Empty<ParticleSystem>();
            _temporaryOverlaySimulationRoots = Array.Empty<ParticleSystem>();
            _temporaryOverlayGraphs = Array.Empty<Component>();
            ApplyTemporaryOverlayMode();
            if (_instance == null)
            {
                _paused = true;
                State = VfxLibraryPlaybackState.Stopped;
                ElapsedSeconds = 0f;
            }
        }

        public bool SetCharacterContext(
            string prefabPath,
            CharacterImportedPackage package,
            string socketId,
            AnimationClip clip,
            bool animationLoop,
            float animationSpeed,
            bool characterVisible,
            bool showSocketAxes,
            out string message)
        {
            EnsureUtility();
            string normalizedPath = (prefabPath ?? string.Empty).Replace('\\', '/');
            if (_characterInstance == null || !string.Equals(_characterPrefabPath, normalizedPath, StringComparison.Ordinal))
            {
                CharacterPreviewEntry entry;
                if (!_characterCache.TryGetValue(normalizedPath, out entry))
                {
                    if (!TryCreateCharacterEntry(normalizedPath, package, clip, out entry, out message)) return false;
                    _characterCache[normalizedPath] = entry;
                }
                if (_characterInstance != null) _characterInstance.SetActive(false);
                _characterInstance = entry.Instance;
                _characterAnimator = entry.Animator;
                _characterBoneSync = entry.BoneSync;
                _characterRenderers = entry.Renderers;
                _characterRendererDefaults = entry.RendererDefaults;
                _characterPrefabPath = normalizedPath;
                _characterInstance.SetActive(true);
            }
            _characterClip = clip;
            _characterAnimationLoop = animationLoop;
            _characterAnimationSpeed = Mathf.Max(0.05f, animationSpeed);
            SetCharacterVisible(characterVisible);
            SetSocketAxesVisible(showSocketAxes);
            if (!SetAttachmentSocket(socketId, out message)) return false;
            SampleCharacterAnimation(CharacterAnimationTimeSeconds());
            return true;
        }

        public void ClearCharacterContext()
        {
            AttachEffectToPreviewRoot();
            if (_characterInstance != null) _characterInstance.SetActive(false);
            _characterInstance = null;
            _characterAnimator = null;
            _characterBoneSync = null;
            _characterClip = null;
            _characterRenderers = Array.Empty<Renderer>();
            _characterRendererDefaults = Array.Empty<bool>();
            _attachmentTransform = null;
            _characterPrefabPath = string.Empty;
            _attachmentSocketId = string.Empty;
            AttachTemporaryOverlay();
            if (_socketAxesObject != null) _socketAxesObject.SetActive(false);
        }

        private bool TryCreateCharacterEntry(string normalizedPath, CharacterImportedPackage package, AnimationClip clip, out CharacterPreviewEntry entry, out string message)
        {
            entry = null;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(normalizedPath);
            Animator prefabAnimator = prefab == null ? null : prefab.GetComponentInChildren<Animator>(true);
            if (prefab == null || prefabAnimator == null || prefabAnimator.avatar == null || !prefabAnimator.avatar.isValid)
            {
                message = "角色预览 Prefab、Animator 或 Avatar 无效。";
                return false;
            }
            GameObject instance = Object.Instantiate(prefab);
            instance.name = prefab.name + " (VFX Preview)";
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(_rootPosition, Quaternion.identity);
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            animator.applyRootMotion = false;
            CharacterRuntimeSpawnResult spawnResult = package != null ? CharacterRuntimeSpawnResolver.Resolve(package, default) : null;
            CharacterRuntimePrefabCompositionResult composition = package != null && spawnResult != null && spawnResult.IsSuccess
                ? CharacterRuntimePrefabComposer.Compose(
                    instance,
                    new CharacterRuntimePrefabCompositionOptions(package, spawnResult)
                    {
                        AnimationClips = clip != null ? new[] { clip } : Array.Empty<AnimationClip>(),
                        InstallDefaultEquipmentBinder = false,
                        InstantiateDefaultWeapons = false,
                        InstallControllerBinding = false,
                        InitializeControllerBinding = false,
                        InstallInputMotionController = false,
                        EnableInputMotion = false,
                        InstallLocomotionController = false,
                        InstallBoneRuntimeSync = true,
                        UseExternalRuntimeTick = true,
                        PlayFirstAnimationOnStart = false
                    })
                : null;
            if (composition == null || !composition.Success || composition.BoneSync == null)
            {
                string issues = composition == null ? "缺少角色包或 Spawn 结果。" : string.Join("; ", composition.Issues);
                Object.DestroyImmediate(instance);
                message = "角色正式挂点同步装配失败：" + issues;
                return false;
            }
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            var defaults = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) defaults[i] = renderers[i].enabled;
            entry = new CharacterPreviewEntry
            {
                Instance = instance,
                Animator = animator,
                BoneSync = composition.BoneSync,
                Renderers = renderers,
                RendererDefaults = defaults
            };
            _utility.AddSingleGO(instance);
            AcquireAnimationMode();
            message = string.Empty;
            return true;
        }

        public bool SetAttachmentSocket(string socketId, out string message)
        {
            if (_characterInstance == null)
            {
                message = "尚未加载角色预览。";
                return false;
            }
            string value = socketId ?? string.Empty;
            Transform target = string.IsNullOrWhiteSpace(value) ? _characterInstance.transform : FindChild(_characterInstance.transform, "Socket_" + value);
            if (target == null)
            {
                message = "角色 Prefab 缺少真实挂点：Socket_" + value;
                return false;
            }
            _attachmentSocketId = value;
            _attachmentTransform = target;
            AttachEffectToSocket();
            UpdateSocketAxesPose();
            message = string.IsNullOrWhiteSpace(value) ? "VFX 已挂到角色根节点。" : "VFX 已挂到 Socket_" + value + "。";
            return true;
        }

        public void SetCharacterVisible(bool value)
        {
            _characterVisible = value;
            for (int i = 0; i < _characterRenderers.Length; i++)
                if (_characterRenderers[i] != null) _characterRenderers[i].enabled = value && _characterRendererDefaults[i];
        }

        public void SetSocketAxesVisible(bool value)
        {
            if (_socketAxesObject != null) _socketAxesObject.SetActive(value && _characterInstance != null);
        }

        public void SelectComponents(IEnumerable<VfxSourceComponentView> components)
        {
            DisposeEffectInstance();
            VfxSourceComponentView[] selected = (components ?? Array.Empty<VfxSourceComponentView>()).Where(value => value != null).ToArray();
            if (selected.Length == 0) return;
            var root = new GameObject("VFX Source Component Preview");
            var selectedParticles = new List<ParticleSystem>();
            foreach (IGrouping<string, VfxSourceComponentView> group in selected.GroupBy(value => value.PrefabPath, StringComparer.Ordinal))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(group.Key);
                if (prefab == null) continue;
                GameObject instance = Object.Instantiate(prefab, root.transform, false);
                if (ApplyComponentSelection(prefab, instance, group.ToArray(), out ParticleSystem[] groupParticles))
                    selectedParticles.AddRange(groupParticles);
            }
            if (root.transform.childCount == 0) { Object.DestroyImmediate(root); return; }
            Initialize(root, selectedParticles.ToArray());
        }

        private void Initialize(GameObject instance, ParticleSystem[] selectedParticles = null)
        {
            EnsureUtility();
            _instance = instance; _instance.hideFlags = HideFlags.HideAndDontSave;
            _utility.AddSingleGO(_instance);
            if (_characterInstance != null) AttachEffectToSocket(); else AttachEffectToPreviewRoot();
            _particles = selectedParticles ?? _instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i].useAutoRandomSeed = false;
                _particles[i].randomSeed = unchecked(17u + (uint)i * 7919u);
            }
            _particleSimulationRoots = BuildParticleSimulationRoots(_particles);
            _frameOnFirstVisibleParticle = _particles.Length > 0;
            _graphComponents = FindGraphComponents(_instance);
            CaptureCompositionRendererDefaults();
            ApplyTemporaryOverlayMode();
            EstimatedDurationSeconds = EstimateDuration();
            _lifecycle = _instance.GetComponent<UnityVfxLifecycleController>();
            if (!_cameraInitialized)
            {
                Frame();
                _cameraInitialized = true;
            }
            else CenterCameraOnContent();
            Restart();
        }

        internal static bool ApplyComponentSelection(
            GameObject prefab,
            GameObject instance,
            IReadOnlyCollection<VfxSourceComponentView> selectedComponents)
        {
            return ApplyComponentSelection(prefab, instance, selectedComponents, out _);
        }

        internal static bool ApplyComponentSelection(
            GameObject prefab,
            GameObject instance,
            IReadOnlyCollection<VfxSourceComponentView> selectedComponents,
            out ParticleSystem[] selectedSystems)
        {
            selectedSystems = Array.Empty<ParticleSystem>();
            if (prefab == null || instance == null) return false;

            ParticleSystem[] prefabSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystem[] instanceSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            if (prefabSystems.Length != instanceSystems.Length)
            {
                SetComponentSelection(instance, instanceSystems, null);
                return false;
            }

            var selectedLocalFileIds = new HashSet<long>();
            var selectedLegacyPaths = new HashSet<string>(StringComparer.Ordinal);
            if (selectedComponents != null)
            {
                foreach (VfxSourceComponentView component in selectedComponents)
                {
                    if (component == null) continue;
                    if (component.LocalFileId != 0) selectedLocalFileIds.Add(component.LocalFileId);
                    else if (!string.IsNullOrWhiteSpace(component.ObjectPath)) selectedLegacyPaths.Add(component.ObjectPath);
                }
            }

            var prefabPaths = new string[prefabSystems.Length];
            var legacyPathCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < prefabSystems.Length; i++)
            {
                string prefabPath = TransformPath(prefabSystems[i].transform, prefab.transform);
                string instancePath = TransformPath(instanceSystems[i].transform, instance.transform);
                if (!string.Equals(prefabPath, instancePath, StringComparison.Ordinal))
                {
                    SetComponentSelection(instance, instanceSystems, null);
                    return false;
                }

                prefabPaths[i] = prefabPath;
                legacyPathCounts.TryGetValue(prefabPath, out int count);
                legacyPathCounts[prefabPath] = count + 1;
            }

            var enabledSystems = new HashSet<ParticleSystem>();
            for (int i = 0; i < prefabSystems.Length; i++)
            {
                bool enabled = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefabSystems[i], out _, out long localFileId)
                    && localFileId != 0
                    && selectedLocalFileIds.Contains(localFileId);
                if (!enabled
                    && selectedLegacyPaths.Contains(prefabPaths[i])
                    && legacyPathCounts[prefabPaths[i]] == 1)
                {
                    enabled = true;
                }

                if (enabled) enabledSystems.Add(instanceSystems[i]);
            }

            SetComponentSelection(instance, instanceSystems, enabledSystems);
            var orderedSelectedSystems = new List<ParticleSystem>(enabledSystems.Count);
            for (int i = 0; i < instanceSystems.Length; i++)
                if (enabledSystems.Contains(instanceSystems[i])) orderedSelectedSystems.Add(instanceSystems[i]);
            selectedSystems = orderedSelectedSystems.ToArray();
            return true;
        }

        private static void SetComponentSelection(
            GameObject instance,
            IReadOnlyList<ParticleSystem> systems,
            ISet<ParticleSystem> enabledSystems)
        {
            var requiredTransforms = new HashSet<Transform>();
            if (enabledSystems != null)
            {
                foreach (ParticleSystem enabledSystem in enabledSystems)
                {
                    Transform current = enabledSystem.transform;
                    while (current != null)
                    {
                        requiredTransforms.Add(current);
                        if (current == instance.transform) break;
                        current = current.parent;
                    }
                }
            }

            foreach (Transform requiredTransform in requiredTransforms)
                requiredTransform.gameObject.SetActive(true);

            for (int i = 0; i < systems.Count; i++)
            {
                ParticleSystem system = systems[i];
                bool enabled = enabledSystems != null && enabledSystems.Contains(system);
                ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.enabled = enabled;
                if (!enabled) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.gameObject.SetActive(enabled || requiredTransforms.Contains(system.transform));
            }
        }

        public void Draw(Rect rect)
        {
            DrawInternal(rect, true);
        }

        internal void DrawSynchronized(Rect rect)
        {
            DrawInternal(rect, false);
        }

        private void DrawInternal(Rect rect, bool updatePlayback)
        {
            if (float.IsNaN(rect.width) || float.IsInfinity(rect.width)
                || float.IsNaN(rect.height) || float.IsInfinity(rect.height)
                || rect.width < 2f || rect.height < 2f)
                return;
            EnsureUtility(); HandleInput(rect);
            SampleCharacterAnimation(CharacterAnimationTimeSeconds());
            UpdateSocketAxesPose();
            _utility.BeginPreview(rect, GUIStyle.none); ConfigureCamera(rect); _utility.camera.Render(); Texture result = _utility.EndPreview(); GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);
            if (updatePlayback) UpdatePlayback();
        }

        internal void UpdatePlayback()
        {
            if (!_paused && HasPreviewContent)
            {
                double now = EditorApplication.timeSinceStartup;
                float dt = Mathf.Min(0.05f, (float)(now - _lastTime)) * _speed;
                if (_stopAtTimelineEnd) dt = Mathf.Min(dt, Mathf.Max(0f, EstimatedDurationSeconds - ElapsedSeconds));
                AdvancePreview(dt);
                if (_stopAtTimelineEnd && ElapsedSeconds >= EstimatedDurationSeconds)
                {
                    ElapsedSeconds = EstimatedDurationSeconds;
                    if (_loop) Restart();
                    else Pause();
                }
                else if (_loop && ElapsedSeconds >= EstimatedDurationSeconds)
                    Restart();
                _lastTime = now;
            }
        }

        internal void SynchronizePlaybackFrom(VfxLibraryPreviewSession master)
        {
            if (master == null || !HasPreviewContent) return;
            float target = Mathf.Clamp(master.ElapsedSeconds, 0f, EstimatedDurationSeconds);
            if (master.State == VfxLibraryPlaybackState.Playing)
            {
                if (target + 0.00001f < ElapsedSeconds) Restart();
                if (target > ElapsedSeconds + 0.00001f)
                    AdvancePreview(target - ElapsedSeconds);
                _paused = false;
                State = VfxLibraryPlaybackState.Playing;
                return;
            }
            if (Mathf.Abs(target - ElapsedSeconds) > 0.00001f)
                Scrub(target / Mathf.Max(0.0001f, EstimatedDurationSeconds));
            Pause();
        }

        /// <summary>Captures the current simulated frame and camera pose without advancing playback.</summary>
        /// <remarks>Editor-only, AllocByDesign. The caller owns and must destroy the returned texture.</remarks>
        public Texture2D CaptureFrame(int width, int height)
        {
            if (!HasPreviewContent) return null;
            width = Mathf.Max(64, width);
            height = Mathf.Max(64, height);
            EnsureUtility();
            SampleCharacterAnimation(CharacterAnimationTimeSeconds());
            UpdateSocketAxesPose();

            Rect captureRect = new Rect(0f, 0f, width, height);
            RenderTexture colorTarget = null;
            RenderTexture previousActive = RenderTexture.active;
            bool previewOpen = false;
            try
            {
                // Use the exact PreviewRenderUtility path used by Draw(). Rendering directly into a
                // separate Camera.targetTexture changes HDR/color-space handling for emissive VFX.
                _utility.BeginPreview(captureRect, GUIStyle.none);
                previewOpen = true;
                ConfigureCamera(captureRect);
                _utility.camera.Render();
                Texture previewTexture = _utility.EndPreview();
                previewOpen = false;
                if (previewTexture == null) return null;

                // The preview target is linear/HDR in a linear-color project. GUI.DrawTexture performs
                // the display conversion implicitly; PNG readback must explicitly resolve to sRGB or
                // the saved icon looks dark and loses the apparent glow of the live preview.
                colorTarget = RenderTexture.GetTemporary(
                    width,
                    height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                Graphics.Blit(previewTexture, colorTarget);
                RenderTexture.active = colorTarget;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                if (previewOpen) _utility.EndPreview();
                RenderTexture.active = previousActive;
                if (colorTarget != null) RenderTexture.ReleaseTemporary(colorTarget);
            }
        }

        public void Play() { if (!HasPreviewContent) return; _paused = false; State = VfxLibraryPlaybackState.Playing; PlayParticleRoots(_particleSimulationRoots); PlayParticleRoots(_temporaryOverlaySimulationRoots); InvokeGraphs(_graphComponents, "Play"); InvokeGraphs(_temporaryOverlayGraphs, "Play"); SetGraphPaused(_graphComponents, false); SetGraphPaused(_temporaryOverlayGraphs, false); _lastTime = EditorApplication.timeSinceStartup; }
        public void Pause() { if (!HasPreviewContent) return; _paused = true; State = VfxLibraryPlaybackState.Paused; PauseParticles(_particles); PauseParticles(_temporaryOverlayParticles); SetGraphPaused(_graphComponents, true); SetGraphPaused(_temporaryOverlayGraphs, true); }
        public void Stop() { _paused = true; State = VfxLibraryPlaybackState.Stopped; ElapsedSeconds = 0f; if (_lifecycle != null) _lifecycle.ForceClear(); else StopParticles(_particles); StopParticles(_temporaryOverlayParticles); InvokeGraphs(_graphComponents, "Stop"); InvokeGraphs(_temporaryOverlayGraphs, "Stop"); }
        public void Restart() { if (!HasPreviewContent) return; ElapsedSeconds = 0f; ApplyPreviewMotionAtTime(0f); _lifecycleAutoExitTriggered = false; SampleCharacterAnimation(0f); if (_lifecycle != null) _lifecycle.PlayBuild(new VfxInstanceId(1)); else RestartParticleTrees(_particles, _particleSimulationRoots); InvokeGraphs(_graphComponents, "Reinit"); SetGraphPaused(_graphComponents, false); InvokeGraphs(_graphComponents, "Play"); RestartTemporaryOverlay(); _paused = false; State = VfxLibraryPlaybackState.Playing; _lastTime = EditorApplication.timeSinceStartup; }
        public void RebuildLifecycle() { if (_lifecycle == null) { Restart(); return; } ElapsedSeconds = 0f; _lifecycleAutoExitTriggered = false; _lifecycle.PlayRebuild(new VfxInstanceId(1)); SampleCharacterAnimation(0f); _paused = false; State = VfxLibraryPlaybackState.Playing; _lastTime = EditorApplication.timeSinceStartup; }
        public void StopLifecycle(VfxStopMode stopMode) { if (_lifecycle == null) { Stop(); return; } _lifecycleAutoExitTriggered = true; _lifecycle.PlayExit(stopMode); SampleCharacterAnimation(0f); _paused = false; State = VfxLibraryPlaybackState.Playing; _lastTime = EditorApplication.timeSinceStartup; }
        public void PlayLifecyclePhase(UnityVfxLifecyclePhase phase)
        {
            if (_lifecycle == null) { Restart(); return; }
            ElapsedSeconds = 0f;
            _lifecycleAutoExitTriggered = false;
            switch (phase)
            {
                case UnityVfxLifecyclePhase.Rebuild:
                    _lifecycle.PlayRebuild(new VfxInstanceId(1));
                    break;
                case UnityVfxLifecyclePhase.Complete:
                    _lifecycle.PlayBuild(new VfxInstanceId(1));
                    _lifecycle.PlayExit(VfxStopMode.Complete);
                    break;
                case UnityVfxLifecyclePhase.Dissipate:
                    _lifecycle.PlayBuild(new VfxInstanceId(1));
                    _lifecycle.PlayExit(VfxStopMode.Dissipate);
                    break;
                default:
                    _lifecycle.PlayBuild(new VfxInstanceId(1));
                    break;
            }
            SampleCharacterAnimation(0f);
            _paused = false;
            State = VfxLibraryPlaybackState.Playing;
            _lastTime = EditorApplication.timeSinceStartup;
        }
        public UnityVfxLifecyclePhase CurrentLifecyclePhase => _lifecycle != null ? _lifecycle.CurrentPhase : UnityVfxLifecyclePhase.None;
        public int CurrentLifecyclePhaseFrame => _lifecycle != null ? _lifecycle.CurrentPhaseFrame : 0;
        public int GetLifecyclePhaseDuration(UnityVfxLifecyclePhase phase) => _lifecycle != null ? _lifecycle.GetConfiguredDurationFrames(phase) : 0;
        public void SetLoop(bool value) { _loop = value; }
        public void SetSpeed(float value) { _speed = Mathf.Max(0.05f, value); }
        public void SetTimelineDuration(float seconds) { EstimatedDurationSeconds = Mathf.Clamp(seconds, 1f / 60f, 30f); }
        public void SetStopAtTimelineEnd(bool value) { _stopAtTimelineEnd = value; }
        public void SetLifecycleAutoExit(float seconds, VfxStopMode stopMode)
        {
            _lifecycleAutoExitSeconds = seconds >= 0f ? seconds : -1f;
            _lifecycleAutoExitMode = stopMode;
            _lifecycleAutoExitTriggered = false;
        }
        public void SetBackground(string value)
        {
            _background = value == "浅色"
                ? new Color(0.72f, 0.74f, 0.77f)
                : value == "中性" || value == "中性灰"
                    ? new Color(0.16f, 0.17f, 0.19f)
                    : new Color(0.025f, 0.03f, 0.04f);
            ApplyGridPalette();
        }
        public void SetPostProcessingMode(string value)
        {
            _postProcessingEnabled = string.Equals(value, "游戏观感", StringComparison.Ordinal);
            ApplyPostProcessingState();
        }
        public void SetRootPosition(Vector3 value)
        {
            _baseRootPosition = value;
            ApplyRootPosition(value + _previewMotionOffset, true);
        }
        internal void SetPreviewMotion(VfxPreviewMotionSettings settings)
        {
            if (_previewMotion.FollowCamera && _previewMotionOffset.sqrMagnitude > 0.000001f)
                _cameraTarget -= _previewMotionOffset;
            _previewMotion = settings;
            _previewMotionOffset = Vector3.zero;
            ApplyRootPosition(_baseRootPosition, false);
            ApplyPreviewMotionAtTime(ElapsedSeconds);
        }
        private void ApplyRootPosition(Vector3 value, bool moveCamera)
        {
            Vector3 offset = value - _rootPosition;
            _rootPosition = value;
            if (moveCamera) _cameraTarget += offset;
            foreach (CharacterPreviewEntry entry in _characterCache.Values)
                if (entry.Instance != null) entry.Instance.transform.position = value;
            if (_characterInstance == null && _instance != null) _instance.transform.position = value;
            if (_characterInstance == null && _temporaryOverlayInstance != null) _temporaryOverlayInstance.transform.position = value;
            if (_rootAxesObject != null) _rootAxesObject.transform.position = value;
        }
        private void ApplyPreviewMotionAtTime(float elapsedSeconds)
        {
            Vector3 nextOffset = _previewMotion.EvaluatePosition(elapsedSeconds);
            Vector3 cameraOffset = nextOffset - _previewMotionOffset;
            _previewMotionOffset = nextOffset;
            ApplyRootPosition(_baseRootPosition + nextOffset, false);
            if (_previewMotion.FollowCamera) _cameraTarget += cameraOffset;
        }
        public void SetGridVisible(bool value)
        {
            GridVisible = value;
            if (_gridRenderer != null) _gridRenderer.enabled = value;
        }
        public void SetCameraMode(string value)
        {
            CameraMode = value;
            if (value == "俯视") _orbit = new Vector2(0f, 78f);
            else if (value == "正面") _orbit = new Vector2(0f, 0f);
            else if (value == "侧面") _orbit = new Vector2(90f, 0f);
            else _orbit = new Vector2(25f, 20f);
        }
        internal void SetCameraPose(Vector2 orbit, float distance)
        {
            _orbit = orbit;
            _distance = Mathf.Clamp(distance, 1f, PreviewCameraMaximumDistance);
            _cameraInitialized = true;
        }
        internal VfxPreviewCameraState CaptureCameraState()
        {
            return new VfxPreviewCameraState(_orbit, _distance, _cameraTarget);
        }
        internal void ApplyCameraState(VfxPreviewCameraState state)
        {
            _orbit = state.Orbit;
            _distance = Mathf.Clamp(state.Distance, 1f, PreviewCameraMaximumDistance);
            _cameraTarget = state.Target;
            _cameraInitialized = true;
            ApplyCameraTransform();
        }
        public void FrameCamera() { _frameOnFirstVisibleParticle = !Frame() && _particles.Length > 0; _cameraInitialized = true; }
        public void CenterCameraOnContent()
        {
            if (TryGetVisibleBounds(out Bounds bounds)) _cameraTarget = bounds.center;
            else _cameraTarget = _rootPosition + Vector3.up * 0.6f;
        }
        /// <summary>Pauses the preview after advancing exactly one 60 Hz authoring frame.</summary>
        /// <remarks>Editor-only, LowFreqAlloc. Invoked by explicit authoring input.</remarks>
        public void StepFrame()
        {
            if (!HasPreviewContent) return;
            const float frameSeconds = 1f / 60f;
            if (State == VfxLibraryPlaybackState.Stopped || ElapsedSeconds >= EstimatedDurationSeconds - 0.00001f)
            {
                if (!_loop && State != VfxLibraryPlaybackState.Stopped)
                {
                    Pause();
                    return;
                }
                Restart();
            }
            Pause();
            AdvancePreview(Mathf.Min(frameSeconds, Mathf.Max(0f, EstimatedDurationSeconds - ElapsedSeconds)));
            InvokeGraphs(_graphComponents, "AdvanceOneFrame");
            InvokeGraphs(_temporaryOverlayGraphs, "AdvanceOneFrame");
            PauseParticles(_particles);
            PauseParticles(_temporaryOverlayParticles);
            SetGraphPaused(_graphComponents, true);
            SetGraphPaused(_temporaryOverlayGraphs, true);
            SampleCharacterAnimation(CharacterAnimationTimeSeconds());
            UpdateSocketAxesPose();
            _paused = true;
            State = VfxLibraryPlaybackState.Paused;
            _lastTime = EditorApplication.timeSinceStartup;
        }
        public void Scrub(float normalizedTime)
        {
            if (_instance == null) return;
            float normalized = Mathf.Clamp01(normalizedTime);
            if (_lifecycle != null || _previewMotion.IsMoving)
            {
                ApplyPreviewMotionAtTime(0f);
                if (_lifecycle != null) _lifecycle.PlayBuild(new VfxInstanceId(1));
                else RestartParticleTrees(_particles, _particleSimulationRoots);
                RestartParticleTrees(_temporaryOverlayParticles, _temporaryOverlaySimulationRoots);
                ElapsedSeconds = 0f;
                _lifecycleAutoExitTriggered = false;
                float remaining = EstimatedDurationSeconds * normalized;
                while (remaining > 0f)
                {
                    float step = Mathf.Min(1f / 60f, remaining);
                    AdvancePreview(step);
                    remaining -= step;
                }
            }
            else
            {
                RestartParticleTrees(_particles, _particleSimulationRoots);
                SimulateParticleTreesToTime(
                    _particleSimulationRoots,
                    _particles,
                    EstimatedDurationSeconds * normalized);
                PauseParticles(_particles);
            }
            if (_lifecycle == null && !_previewMotion.IsMoving && _temporaryOverlayParticles.Length > 0)
            {
                float targetSeconds = EstimatedDurationSeconds * normalized;
                RestartParticleTrees(_temporaryOverlayParticles, _temporaryOverlaySimulationRoots);
                SimulateParticleTreesToTime(
                    _temporaryOverlaySimulationRoots,
                    _temporaryOverlayParticles,
                    targetSeconds);
                PauseParticles(_temporaryOverlayParticles);
            }
            InvokeGraphs(_graphComponents, "Reinit");
            InvokeGraphs(_temporaryOverlayGraphs, "Reinit");
            if (_lifecycle == null) ElapsedSeconds = EstimatedDurationSeconds * normalized;
            SampleCharacterAnimation(CharacterAnimationTimeSeconds());
            UpdateSocketAxesPose();
            _paused = true;
            State = VfxLibraryPlaybackState.Paused;
        }
        public void SetFallbackMode(string value) { bool fallback = value == "Fallback" || value == "降级"; for (int i = 0; i < _graphComponents.Length; i++) if (_graphComponents[i] is Behaviour b) b.enabled = !fallback; for (int i = 0; i < _particles.Length; i++) { var emission = _particles[i].emission; emission.enabled = value != "Graph"; } Restart(); }
        public void Dispose()
        {
            ClearTemporaryOverlay();
            DisposeEffectInstance();
            DisposeCharacterInstance();
            DestroyPreviewHelpers();
            _utility?.Cleanup();
            _utility = null;
        }

        private void EnsureUtility()
        {
            if (_utility != null) return;
            _utility = new PreviewRenderUtility();
            _utility.cameraFieldOfView = 32f;
            _utility.lights[0].intensity = 1.3f;
            _utility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            _utility.lights[1].intensity = 0.7f;
            _utility.ambientColor = new Color(0.12f, 0.14f, 0.17f);
            CreatePreviewHelpers();
            CreatePostProcessingHelpers();
        }
        private void ConfigureCamera(Rect rect)
        {
            ApplyCameraTransform();
            _utility.camera.aspect = rect.width / rect.height;
            _utility.camera.nearClipPlane = Mathf.Max(0.01f, _distance * 0.001f);
            _utility.camera.farClipPlane = Mathf.Max(100f, _distance * 4f);
            _utility.camera.clearFlags = CameraClearFlags.SolidColor;
            _utility.camera.backgroundColor = _background;
            _utility.camera.allowHDR = _postProcessingEnabled;
            ApplyPostProcessingState();
        }

        private void ApplyCameraTransform()
        {
            if (_utility?.camera == null) return;
            Quaternion rotation = Quaternion.Euler(_orbit.y, _orbit.x, 0f);
            _utility.camera.transform.position = _cameraTarget + rotation * new Vector3(0f, 0f, -_distance);
            _utility.camera.transform.LookAt(_cameraTarget);
        }

        private void CreatePostProcessingHelpers()
        {
            if (!PostProcessingAvailable || _utility?.camera == null) return;
            _cameraData = _utility.camera.GetUniversalAdditionalCameraData();
            _cameraData.volumeLayerMask = 1 << 0;
            _cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            _cameraData.dithering = true;

            _postProcessProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GameplayPostProcessingProfilePath);
            _ownsPostProcessProfile = _postProcessProfile == null;
            if (_ownsPostProcessProfile)
            {
                _postProcessProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                _postProcessProfile.name = "VFX Preview Post Processing (Fallback)";
                _postProcessProfile.hideFlags = HideFlags.HideAndDontSave;
                Bloom bloom = _postProcessProfile.Add<Bloom>(true);
                bloom.threshold.Override(0.8f);
                bloom.intensity.Override(0.55f);
                bloom.scatter.Override(0.65f);
                bloom.highQualityFiltering.Override(true);
                Tonemapping tonemapping = _postProcessProfile.Add<Tonemapping>(true);
                tonemapping.mode.Override(TonemappingMode.ACES);
                ColorAdjustments colorAdjustments = _postProcessProfile.Add<ColorAdjustments>(true);
                colorAdjustments.postExposure.Override(0f);
            }

            _postProcessVolumeObject = new GameObject("VFX Preview Post Processing")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = 0
            };
            _postProcessVolume = _postProcessVolumeObject.AddComponent<Volume>();
            _postProcessVolume.isGlobal = true;
            _postProcessVolume.priority = 1000f;
            _postProcessVolume.sharedProfile = _postProcessProfile;
            _utility.AddSingleGO(_postProcessVolumeObject);
            ApplyPostProcessingState();
        }

        private void ApplyPostProcessingState()
        {
            if (_utility?.camera != null) _utility.camera.allowHDR = _postProcessingEnabled;
            if (_cameraData != null) _cameraData.renderPostProcessing = _postProcessingEnabled;
            if (_postProcessVolume != null) _postProcessVolume.weight = _postProcessingEnabled ? 1f : 0f;
        }
        private void CreatePreviewHelpers()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return;
            _helperMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            _gridMesh = CreateGridMesh();
            _gridObject = CreateLineObject("VFX Preview Grid", _gridMesh, out _gridRenderer);
            ApplyGridPalette();
            _gridRenderer.enabled = GridVisible;
            _utility.AddSingleGO(_gridObject);

            _rootAxesMesh = CreateRootAxesMesh();
            _rootAxesObject = CreateLineObject("VFX Preview Root Axes", _rootAxesMesh, out _);
            _rootAxesObject.transform.position = _rootPosition;
            _utility.AddSingleGO(_rootAxesObject);

            _socketAxesMesh = CreateRootAxesMesh();
            _socketAxesObject = CreateLineObject("VFX Preview Socket Axes", _socketAxesMesh, out _);
            _socketAxesObject.transform.localScale = Vector3.one * 0.35f;
            _socketAxesObject.SetActive(false);
            _utility.AddSingleGO(_socketAxesObject);
        }
        private GameObject CreateLineObject(string name, Mesh mesh, out Renderer renderer)
        {
            var value = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            MeshFilter filter = value.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var meshRenderer = value.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _helperMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            renderer = meshRenderer;
            return value;
        }
        private static Mesh CreateGridMesh()
        {
            const int halfSize = PreviewGridHalfSize;
            const float step = PreviewGridStep;
            int lineCount = (halfSize * 2 + 1) * 2;
            var vertices = new Vector3[lineCount * 2];
            var colors = new Color[vertices.Length];
            int cursor = 0;
            for (int i = -halfSize; i <= halfSize; i++)
            {
                Color color = i == 0 ? new Color(0.28f, 0.42f, 0.5f, 0.82f) : i % 4 == 0 ? new Color(0.2f, 0.27f, 0.31f, 0.7f) : new Color(0.12f, 0.16f, 0.19f, 0.55f);
                vertices[cursor] = new Vector3(i * step, 0f, -halfSize * step); colors[cursor++] = color;
                vertices[cursor] = new Vector3(i * step, 0f, halfSize * step); colors[cursor++] = color;
                vertices[cursor] = new Vector3(-halfSize * step, 0f, i * step); colors[cursor++] = color;
                vertices[cursor] = new Vector3(halfSize * step, 0f, i * step); colors[cursor++] = color;
            }
            var mesh = new Mesh { name = "VFX Preview Grid", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices;
            mesh.colors = colors;
            int[] indices = new int[vertices.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetIndices(indices, MeshTopology.Lines, 0, false);
            mesh.RecalculateBounds();
            return mesh;
        }
        private void ApplyGridPalette()
        {
            if (_gridMesh == null) return;
            float backgroundLuminance = _background.r * 0.2126f + _background.g * 0.7152f + _background.b * 0.0722f;
            bool lightBackground = backgroundLuminance > 0.45f;
            Color axis = lightBackground
                ? new Color(0.08f, 0.26f, 0.34f, 0.95f)
                : new Color(0.42f, 0.68f, 0.78f, 0.95f);
            Color major = lightBackground
                ? new Color(0.20f, 0.25f, 0.28f, 0.82f)
                : new Color(0.43f, 0.48f, 0.52f, 0.82f);
            Color minor = lightBackground
                ? new Color(0.34f, 0.37f, 0.40f, 0.62f)
                : new Color(0.30f, 0.34f, 0.37f, 0.68f);
            Color[] colors = _gridMesh.colors;
            int lineIndex = 0;
            for (int i = -PreviewGridHalfSize; i <= PreviewGridHalfSize; i++)
            {
                Color color = i == 0 ? axis : i % 4 == 0 ? major : minor;
                int vertex = lineIndex * 4;
                colors[vertex] = color;
                colors[vertex + 1] = color;
                colors[vertex + 2] = color;
                colors[vertex + 3] = color;
                lineIndex++;
            }
            _gridMesh.colors = colors;
        }
        private static Mesh CreateRootAxesMesh()
        {
            var mesh = new Mesh { name = "VFX Preview Root Axes", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.zero, Vector3.up, Vector3.zero, Vector3.forward };
            mesh.colors = new[] { Color.red, Color.red, Color.green, Color.green, Color.cyan, Color.cyan };
            mesh.SetIndices(new[] { 0, 1, 2, 3, 4, 5 }, MeshTopology.Lines, 0, false);
            mesh.RecalculateBounds();
            return mesh;
        }
        private void DestroyPreviewHelpers()
        {
            if (_gridObject != null) Object.DestroyImmediate(_gridObject);
            if (_rootAxesObject != null) Object.DestroyImmediate(_rootAxesObject);
            if (_socketAxesObject != null) Object.DestroyImmediate(_socketAxesObject);
            if (_postProcessVolumeObject != null) Object.DestroyImmediate(_postProcessVolumeObject);
            if (_ownsPostProcessProfile && _postProcessProfile != null) Object.DestroyImmediate(_postProcessProfile);
            if (_gridMesh != null) Object.DestroyImmediate(_gridMesh);
            if (_rootAxesMesh != null) Object.DestroyImmediate(_rootAxesMesh);
            if (_socketAxesMesh != null) Object.DestroyImmediate(_socketAxesMesh);
            if (_helperMaterial != null) Object.DestroyImmediate(_helperMaterial);
            _gridObject = null;
            _rootAxesObject = null;
            _socketAxesObject = null;
            _postProcessVolumeObject = null;
            _postProcessProfile = null;
            _ownsPostProcessProfile = false;
            _postProcessVolume = null;
            _cameraData = null;
            _gridMesh = null;
            _rootAxesMesh = null;
            _socketAxesMesh = null;
            _helperMaterial = null;
            _gridRenderer = null;
        }
        private void HandleInput(Rect rect) { Event e = Event.current; if (!rect.Contains(e.mousePosition)) return; if (e.type == EventType.MouseDrag && e.button == 0) { _orbit += e.delta * new Vector2(0.5f, -0.5f); e.Use(); } if (e.type == EventType.ScrollWheel) { _distance = Mathf.Clamp(_distance + e.delta.y * Mathf.Max(0.2f, _distance * 0.04f), 1f, PreviewCameraMaximumDistance); e.Use(); } }
        private bool Frame()
        {
            bool framedLiveParticles = TryGetParticleBounds(out Bounds bounds);
            bool hasBounds = framedLiveParticles;
            if (framedLiveParticles)
            {
                EncapsulateNonParticleRenderers(_instance, ref hasBounds, ref bounds);
                if (_characterVisible) EncapsulateRenderers(_characterInstance, ref hasBounds, ref bounds);
            }
            else
            {
                EncapsulateRenderers(_instance, ref hasBounds, ref bounds);
                if (_characterVisible) EncapsulateRenderers(_characterInstance, ref hasBounds, ref bounds);
            }
            EncapsulatePreviewMotion(ref hasBounds, ref bounds);
            if (hasBounds)
            {
                _cameraTarget = bounds.center;
                float halfVerticalFieldOfView = Mathf.Max(1f, _utility.cameraFieldOfView) * Mathf.Deg2Rad * 0.5f;
                float framingDistance = bounds.extents.magnitude / Mathf.Max(0.05f, Mathf.Sin(halfVerticalFieldOfView));
                _distance = Mathf.Clamp(framingDistance * 1.08f, 2f, PreviewCameraMaximumDistance);
            }
            else
            {
                _cameraTarget = _rootPosition + Vector3.up * 0.6f;
                _distance = 5f;
            }
            return framedLiveParticles;
        }

        private void EncapsulatePreviewMotion(ref bool hasBounds, ref Bounds bounds)
        {
            if (!_previewMotion.IsMoving) return;
            const int samples = 32;
            for (int i = 0; i <= samples; i++)
            {
                Vector3 point = _baseRootPosition
                    + _previewMotion.EvaluatePosition(EstimatedDurationSeconds * i / samples);
                if (!hasBounds)
                {
                    bounds = new Bounds(point, Vector3.one * 0.1f);
                    hasBounds = true;
                }
                else bounds.Encapsulate(point);
            }
        }

        private bool TryGetVisibleBounds(out Bounds bounds)
        {
            if (TryGetParticleBounds(out bounds))
            {
                bool particleOrCharacterBounds = true;
                EncapsulateNonParticleRenderers(_instance, ref particleOrCharacterBounds, ref bounds);
                if (_characterVisible) EncapsulateRenderers(_characterInstance, ref particleOrCharacterBounds, ref bounds);
                return true;
            }
            bool hasBounds = false;
            bounds = default;
            EncapsulateRenderers(_instance, ref hasBounds, ref bounds);
            if (_characterVisible) EncapsulateRenderers(_characterInstance, ref hasBounds, ref bounds);
            return hasBounds;
        }

        private bool TryGetParticleBounds(out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = default;
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem particleSystem = _particles[i];
                if (particleSystem == null || particleSystem.particleCount == 0) continue;
                int required = particleSystem.particleCount;
                if (_particleBoundsBuffer.Length < required)
                    Array.Resize(ref _particleBoundsBuffer, Mathf.NextPowerOfTwo(required));
                int count = particleSystem.GetParticles(_particleBoundsBuffer);
                ParticleSystem.MainModule main = particleSystem.main;
                Transform customSpace = main.customSimulationSpace;
                for (int particleIndex = 0; particleIndex < count; particleIndex++)
                {
                    ParticleSystem.Particle particle = _particleBoundsBuffer[particleIndex];
                    Vector3 position = particle.position;
                    if (main.simulationSpace == ParticleSystemSimulationSpace.Local)
                        position = particleSystem.transform.TransformPoint(position);
                    else if (main.simulationSpace == ParticleSystemSimulationSpace.Custom && customSpace != null)
                        position = customSpace.TransformPoint(position);
                    float size = Mathf.Max(0.01f, particle.GetCurrentSize(particleSystem));
                    Bounds particleBounds = new Bounds(position, Vector3.one * size);
                    if (!hasBounds)
                    {
                        bounds = particleBounds;
                        hasBounds = true;
                    }
                    else bounds.Encapsulate(particleBounds);
                }
            }
            return hasBounds;
        }
        private void AdvancePreview(float dt)
        {
            float remaining = Mathf.Max(0f, dt);
            const float fixedStep = 1f / 60f;
            while (remaining > 0.000001f)
            {
                float step = Mathf.Min(fixedStep, remaining);
                AdvancePreviewStep(step);
                remaining -= step;
            }
        }

        private void AdvancePreviewStep(float dt)
        {
            float remaining = Mathf.Max(0f, dt);
            if (_lifecycle != null
                && !_lifecycleAutoExitTriggered
                && _lifecycleAutoExitSeconds >= 0f
                && ElapsedSeconds + remaining >= _lifecycleAutoExitSeconds)
            {
                float beforeExit = Mathf.Max(0f, _lifecycleAutoExitSeconds - ElapsedSeconds);
                if (beforeExit > 0f)
                {
                    ApplyPreviewMotionAtTime(ElapsedSeconds + beforeExit);
                    Simulate(beforeExit);
                    ElapsedSeconds += beforeExit;
                    remaining -= beforeExit;
                }
                _lifecycle.PlayExit(_lifecycleAutoExitMode);
                _lifecycleAutoExitTriggered = true;
            }
            if (remaining <= 0f) return;
            ApplyPreviewMotionAtTime(ElapsedSeconds + remaining);
            Simulate(remaining);
            ElapsedSeconds += remaining;
        }

        private void Simulate(float dt)
        {
            bool alive = SimulateParticleTrees(
                _particleSimulationRoots,
                _particles,
                dt);
            SimulateParticleTrees(
                _temporaryOverlaySimulationRoots,
                _temporaryOverlayParticles,
                dt);
            _lifecycle?.Advance(dt);
            if (_frameOnFirstVisibleParticle && TryGetParticleBounds(out _))
            {
                Frame();
                _frameOnFirstVisibleParticle = false;
            }
            if (_loop && _lifecycle == null && !alive && _particles.Length > 0 && ElapsedSeconds > 0.05f) Restart();
        }

        private static ParticleSystem[] BuildParticleSimulationRoots(ParticleSystem[] particles)
        {
            if (particles == null || particles.Length == 0) return Array.Empty<ParticleSystem>();
            var included = new HashSet<ParticleSystem>(particles);
            var subEmitterTargets = new HashSet<ParticleSystem>();
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle == null) continue;
                ParticleSystem.SubEmittersModule module = particle.subEmitters;
                if (!module.enabled) continue;
                for (int slot = 0; slot < module.subEmittersCount; slot++)
                {
                    ParticleSystem target = module.GetSubEmitterSystem(slot);
                    if (target != null && included.Contains(target)) subEmitterTargets.Add(target);
                }
            }

            var roots = new List<ParticleSystem>(particles.Length);
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle == null || subEmitterTargets.Contains(particle)) continue;
                roots.Add(particle);
            }

            // Malformed cyclic SubEmitter graphs have no natural entry point. Keep the preview usable
            // without individually playing every referenced target.
            if (roots.Count == 0 && particles[0] != null) roots.Add(particles[0]);
            return roots.ToArray();
        }

        private static bool SimulateParticleTrees(
            ParticleSystem[] roots,
            ParticleSystem[] particles,
            float dt)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                ParticleSystem root = roots[i];
                if (root != null) root.Simulate(dt, false, false, true);
            }
            bool alive = false;
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                alive |= particle != null && particle.IsAlive(false);
            }
            return alive;
        }

        private static void SimulateParticleTreesToTime(
            ParticleSystem[] roots,
            ParticleSystem[] particles,
            float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                float step = Mathf.Min(1f / 60f, remaining);
                SimulateParticleTrees(roots, particles, step);
                remaining -= step;
            }
        }

        private static void RestartParticleTrees(ParticleSystem[] particles, ParticleSystem[] roots)
        {
            StopParticles(particles);
            PlayParticleRoots(roots);
        }
        private static void PlayParticleRoots(ParticleSystem[] roots) { for (int i = 0; i < roots.Length; i++) if (roots[i] != null) roots[i].Play(false); }
        private static void PauseParticles(ParticleSystem[] particles) { for (int i = 0; i < particles.Length; i++) particles[i].Pause(false); }
        private static void StopParticles(ParticleSystem[] particles) { for (int i = 0; i < particles.Length; i++) particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); }
        private static void InvokeGraphs(Component[] graphs, string method) { for (int i = 0; i < graphs.Length; i++) graphs[i].GetType().GetMethod(method, Type.EmptyTypes)?.Invoke(graphs[i], null); }
        private static void SetGraphPaused(Component[] graphs, bool value)
        {
            for (int i = 0; i < graphs.Length; i++)
            {
                var property = graphs[i].GetType().GetProperty("pause");
                if (property != null && property.CanWrite) property.SetValue(graphs[i], value);
            }
        }
        private float EstimateDuration()
        {
            float duration = 0f;
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem.MainModule main = _particles[i].main;
                float candidate = main.loop ? 4f : main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;
                duration = Mathf.Max(duration, candidate);
            }
            return Mathf.Clamp(duration <= 0f ? 4f : duration, 0.25f, 30f);
        }
        private static string TransformPath(Transform value, Transform root) { var values = new List<string>(); while (value != null && value != root) { values.Add(value.name); value = value.parent; } values.Reverse(); return string.Join("/", values); }
        private void AttachEffectToSocket()
        {
            if (_instance != null && _attachmentTransform != null)
            {
                _instance.transform.SetParent(_attachmentTransform, false);
                _instance.transform.localPosition = Vector3.zero;
                _instance.transform.localRotation = Quaternion.identity;
                _instance.transform.localScale = Vector3.one;
            }
            AttachTemporaryOverlay();
        }

        private void AttachEffectToPreviewRoot()
        {
            if (_instance != null)
            {
                _instance.transform.SetParent(null, false);
                _instance.transform.SetPositionAndRotation(_rootPosition, Quaternion.identity);
                _instance.transform.localScale = Vector3.one;
            }
            AttachTemporaryOverlay();
        }

        private void AttachTemporaryOverlay()
        {
            if (_temporaryOverlayInstance == null) return;
            if (_attachmentTransform != null)
            {
                _temporaryOverlayInstance.transform.SetParent(_attachmentTransform, false);
                _temporaryOverlayInstance.transform.localPosition = Vector3.zero;
                _temporaryOverlayInstance.transform.localRotation = Quaternion.identity;
                _temporaryOverlayInstance.transform.localScale = Vector3.one;
                return;
            }
            _temporaryOverlayInstance.transform.SetParent(null, false);
            _temporaryOverlayInstance.transform.SetPositionAndRotation(_rootPosition, Quaternion.identity);
            _temporaryOverlayInstance.transform.localScale = Vector3.one;
        }

        private void SampleCharacterAnimation(float elapsedSeconds)
        {
            if (_characterInstance == null || _characterAnimator == null || _characterClip == null) return;
            AcquireAnimationMode();
            float time = Mathf.Max(0f, elapsedSeconds * _characterAnimationSpeed);
            if (_characterAnimationLoop && _characterClip.length > 0f) time %= _characterClip.length;
            else time = Mathf.Min(time, Mathf.Max(0f, _characterClip.length));
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_characterAnimator.gameObject, _characterClip, time);
            AnimationMode.EndSampling();
            _characterInstance.transform.SetPositionAndRotation(_rootPosition, Quaternion.identity);
            _characterBoneSync?.ApplyBindings();
        }

        private float CharacterAnimationTimeSeconds()
        {
            return _lifecycle != null && _lifecycle.CurrentPhase != UnityVfxLifecyclePhase.None
                ? Mathf.Max(0, _lifecycle.CurrentPhaseFrame) / 60f
                : ElapsedSeconds;
        }

        private void AcquireAnimationMode()
        {
            if (_usesAnimationMode) return;
            if (s_animationModeUsers == 0 && !AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
                s_startedAnimationMode = true;
            }
            s_animationModeUsers++;
            _usesAnimationMode = true;
        }

        private void ReleaseAnimationMode()
        {
            if (!_usesAnimationMode) return;
            _usesAnimationMode = false;
            s_animationModeUsers = Mathf.Max(0, s_animationModeUsers - 1);
            if (s_animationModeUsers == 0 && s_startedAnimationMode)
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                s_startedAnimationMode = false;
            }
        }

        private void UpdateSocketAxesPose()
        {
            if (_socketAxesObject == null || !_socketAxesObject.activeSelf || _attachmentTransform == null) return;
            _socketAxesObject.transform.SetPositionAndRotation(_attachmentTransform.position, _attachmentTransform.rotation);
        }

        private void DisposeEffectInstance()
        {
            bool preserveTemporaryPlayback = HasTemporaryOverlay;
            if (_instance != null) Object.DestroyImmediate(_instance);
            _instance = null;
            _particles = Array.Empty<ParticleSystem>();
            _particleSimulationRoots = Array.Empty<ParticleSystem>();
            _graphComponents = Array.Empty<Component>();
            _compositionRenderers = Array.Empty<Renderer>();
            _compositionRendererDefaults = Array.Empty<bool>();
            _lifecycle = null;
            _stopAtTimelineEnd = false;
            _lifecycleAutoExitSeconds = -1f;
            _lifecycleAutoExitTriggered = false;
            if (!preserveTemporaryPlayback)
            {
                State = VfxLibraryPlaybackState.Stopped;
                ElapsedSeconds = 0f;
                EstimatedDurationSeconds = 4f;
                _paused = true;
            }
        }

        private static Component[] FindGraphComponents(GameObject root)
        {
            Component[] all = root.GetComponentsInChildren<Component>(true);
            var graphs = new List<Component>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].GetType().FullName == "UnityEngine.VFX.VisualEffect") graphs.Add(all[i]);
            return graphs.ToArray();
        }

        private void CaptureCompositionRendererDefaults()
        {
            _compositionRenderers = _instance == null ? Array.Empty<Renderer>() : _instance.GetComponentsInChildren<Renderer>(true);
            _compositionRendererDefaults = new bool[_compositionRenderers.Length];
            for (int i = 0; i < _compositionRenderers.Length; i++)
                _compositionRendererDefaults[i] = _compositionRenderers[i] != null && _compositionRenderers[i].enabled;
        }

        private void ApplyTemporaryOverlayMode()
        {
            bool showComposition = !HasTemporaryOverlay || !_temporaryOverlaySolo;
            for (int i = 0; i < _compositionRenderers.Length; i++)
                if (_compositionRenderers[i] != null) _compositionRenderers[i].enabled = showComposition && _compositionRendererDefaults[i];
        }

        private void DisposeCharacterInstance()
        {
            if (_instance != null && _instance.transform.parent != null) AttachEffectToPreviewRoot();
            _attachmentTransform = null;
            AttachTemporaryOverlay();
            foreach (CharacterPreviewEntry entry in _characterCache.Values)
                if (entry.Instance != null) Object.DestroyImmediate(entry.Instance);
            _characterCache.Clear();
            _characterInstance = null;
            _characterAnimator = null;
            _characterBoneSync = null;
            _characterClip = null;
            _characterRenderers = Array.Empty<Renderer>();
            _characterRendererDefaults = Array.Empty<bool>();
            _characterPrefabPath = string.Empty;
            _attachmentSocketId = string.Empty;
            if (_socketAxesObject != null) _socketAxesObject.SetActive(false);
            ReleaseAnimationMode();
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++) if (string.Equals(transforms[i].name, name, StringComparison.Ordinal)) return transforms[i];
            return null;
        }

        private static void EncapsulateRenderers(GameObject root, ref bool hasBounds, ref Bounds bounds)
        {
            if (root == null) return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
        }

        private static void EncapsulateNonParticleRenderers(GameObject root, ref bool hasBounds, ref Bounds bounds)
        {
            if (root == null) return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null
                    || renderer is ParticleSystemRenderer
                    || !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy)
                    continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
        }
    }
}
