using System;
using UnityEngine;

namespace MxFramework.Vfx.Unity
{
    public enum UnityVfxLifecyclePhase
    {
        None = 0,
        Build = 1,
        Rebuild = 2,
        Loop = 3,
        Complete = 4,
        Dissipate = 5
    }

    public enum UnityVfxLoopPlaybackMode
    {
        OneShot = 0,
        Continuous = 1
    }

    internal interface IUnityVfxLifecycleCompletionSink
    {
        void OnUnityVfxLifecycleCompleted(UnityVfxLifecycleController controller, VfxInstanceId instanceId);
    }

    /// <summary>Controls the five presentation-only phases of one baked VFX view.</summary>
    /// <remarks>
    /// Configuration and cache creation are AllocByDesign Editor/initialization operations.
    /// Update and phase transitions are NoAlloc after initialization.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UnityVfxLifecycleController : MonoBehaviour
    {
        private const float FramesPerSecond = 60f;

        [SerializeField] private GameObject _buildRoot;
        [SerializeField] private GameObject _rebuildRoot;
        [SerializeField] private GameObject _loopRoot;
        [SerializeField] private GameObject _completeRoot;
        [SerializeField] private GameObject _dissipateRoot;
        [SerializeField] private int _buildFrames;
        [SerializeField] private int _rebuildFrames;
        [SerializeField] private int _loopFrames = 1;
        [SerializeField] private int _completeFrames;
        [SerializeField] private int _dissipateFrames;
        [SerializeField] private int _cleanupLimitFrames = 120;
        [SerializeField] private UnityVfxLoopPlaybackMode _loopPlaybackMode;

        private ParticleSystem[] _allSystems = Array.Empty<ParticleSystem>();
        private ParticleSystem[] _buildSystems = Array.Empty<ParticleSystem>();
        private ParticleSystem[] _rebuildSystems = Array.Empty<ParticleSystem>();
        private ParticleSystem[] _loopSystems = Array.Empty<ParticleSystem>();
        private ParticleSystem[] _completeSystems = Array.Empty<ParticleSystem>();
        private ParticleSystem[] _dissipateSystems = Array.Empty<ParticleSystem>();
        private UnityVfxBakedLayerScheduler _layerScheduler;
        private UnityVfxBakedPropertyDriver _propertyDriver;
        private IUnityVfxLifecycleCompletionSink _completionSink;
        private VfxInstanceId _instanceId;
        private UnityVfxLifecyclePhase _phase;
        private float _phaseElapsedFrames;
        private float _cleanupElapsedFrames;
        private bool _initialized;
        private bool _waitingForCleanup;
        private bool _lifecycleComplete;
        private float _playbackRate = 1f;

        public UnityVfxLifecyclePhase CurrentPhase => _phase;
        public UnityVfxLoopPlaybackMode LoopPlaybackMode => _loopPlaybackMode;
        public int CurrentPhaseFrame => Mathf.Max(0, Mathf.FloorToInt(_phaseElapsedFrames));
        public bool IsLifecycleComplete => _lifecycleComplete;

        /// <summary>Sets the instance-local phase playback multiplier.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void SetPlaybackRate(float playbackRate)
        {
            _playbackRate = Mathf.Clamp(playbackRate, 0.05f, 16f);
        }

        /// <summary>Advances lifecycle state by a source-frame offset before normal presentation updates begin.</summary>
        /// <remarks>NoAlloc after initialization. Intended for low-frequency spawn/seek operations.</remarks>
        public void SeekSourceFrames(int sourceFrames)
        {
            int safeFrames = Mathf.Max(0, sourceFrames);
            if (safeFrames == 0 || _phase == UnityVfxLifecyclePhase.None)
                return;

            float sourceFrameDeltaSeconds = 1f / (FramesPerSecond * _playbackRate);
            for (int frame = 0; frame < safeFrames && _phase != UnityVfxLifecyclePhase.None; frame++)
                Advance(sourceFrameDeltaSeconds);
        }

        /// <summary>Returns the baked duration contract for one phase in 60 Hz frames.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int GetConfiguredDurationFrames(UnityVfxLifecyclePhase phase)
        {
            return GetDurationFrames(phase);
        }

        /// <summary>Assigns serialized phase roots and timing during Editor baking.</summary>
        /// <remarks>AllocByDesign; call only while authoring or initializing a baked view.</remarks>
        public void Configure(
            GameObject buildRoot,
            GameObject rebuildRoot,
            GameObject loopRoot,
            GameObject completeRoot,
            GameObject dissipateRoot,
            int buildFrames,
            int rebuildFrames,
            int loopFrames,
            int completeFrames,
            int dissipateFrames,
            UnityVfxLoopPlaybackMode loopPlaybackMode,
            int cleanupLimitFrames = 120)
        {
            _buildRoot = buildRoot;
            _rebuildRoot = rebuildRoot;
            _loopRoot = loopRoot;
            _completeRoot = completeRoot;
            _dissipateRoot = dissipateRoot;
            _buildFrames = Mathf.Max(0, buildFrames);
            _rebuildFrames = Mathf.Max(0, rebuildFrames);
            _loopFrames = Mathf.Max(1, loopFrames);
            _completeFrames = Mathf.Max(0, completeFrames);
            _dissipateFrames = Mathf.Max(0, dissipateFrames);
            _loopPlaybackMode = loopPlaybackMode;
            _cleanupLimitFrames = Mathf.Max(1, cleanupLimitFrames);
            CacheSystems();
        }

        internal void SetCompletionSink(IUnityVfxLifecycleCompletionSink sink)
        {
            _completionSink = sink;
        }

        /// <summary>Starts the first-appearance Build phase and then enters Loop.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void PlayBuild(VfxInstanceId instanceId)
        {
            EnsureInitialized();
            _instanceId = instanceId;
            _lifecycleComplete = false;
            StopAll(clear: true);
            EnterPhase(UnityVfxLifecyclePhase.Build);
        }

        /// <summary>Restarts an existing view through Rebuild and then enters Loop.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void PlayRebuild(VfxInstanceId instanceId)
        {
            EnsureInitialized();
            _instanceId = instanceId;
            _lifecycleComplete = false;
            StopAll(clear: true);
            EnterPhase(UnityVfxLifecyclePhase.Rebuild);
        }

        /// <summary>Stops Loop emission and enters the selected exit phase.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void PlayExit(VfxStopMode stopMode)
        {
            EnsureInitialized();
            _lifecycleComplete = false;
            StopAll(clear: false);
            EnterPhase(stopMode == VfxStopMode.Dissipate
                ? UnityVfxLifecyclePhase.Dissipate
                : UnityVfxLifecyclePhase.Complete);
        }

        public void ForceClear()
        {
            EnsureInitialized();
            StopAll(clear: true);
            _phase = UnityVfxLifecyclePhase.None;
            _waitingForCleanup = false;
            _completionSink = null;
            _instanceId = default;
            _lifecycleComplete = false;
        }

        private void Awake()
        {
            EnsureInitialized();
            StopAll(clear: true);
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        /// <summary>Advances presentation timing; Editor preview sessions may call this with their isolated delta.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void Advance(float deltaSeconds)
        {
            if (_phase == UnityVfxLifecyclePhase.None || deltaSeconds <= 0f)
                return;

            float remainingFrames = deltaSeconds * FramesPerSecond * _playbackRate;
            while (remainingFrames > 0f && _phase != UnityVfxLifecyclePhase.None)
            {
                if (_waitingForCleanup)
                {
                    _phaseElapsedFrames += remainingFrames;
                    AdvancePhaseDrivers();
                    _cleanupElapsedFrames += remainingFrames;
                    if (!HasLiveParticles() || _cleanupElapsedFrames >= _cleanupLimitFrames)
                        CompleteLifecycle();
                    return;
                }

                int duration = GetDurationFrames(_phase);
                float stepFrames = Math.Min(remainingFrames, Mathf.Max(0f, duration - _phaseElapsedFrames));
                _phaseElapsedFrames += stepFrames;
                remainingFrames -= stepFrames;
                AdvancePhaseDrivers();
                if (_phaseElapsedFrames < duration)
                    return;

                switch (_phase)
                {
                    case UnityVfxLifecyclePhase.Build:
                    case UnityVfxLifecyclePhase.Rebuild:
                        EnterPhase(UnityVfxLifecyclePhase.Loop);
                        break;
                    case UnityVfxLifecyclePhase.Loop:
                        if (_loopPlaybackMode == UnityVfxLoopPlaybackMode.OneShot)
                        {
                            StopAll(clear: false);
                            if (EnterPhase(UnityVfxLifecyclePhase.Complete))
                                return;
                        }
                        else
                        {
                            // Missed cycles cannot be rendered retroactively. Restart the current
                            // cycle once and apply its exact offset, without per-cycle catch-up work.
                            _phaseElapsedFrames = remainingFrames % Mathf.Max(1, duration);
                            _layerScheduler?.RestartPhase();
                            _propertyDriver?.EnterPhase(UnityVfxLifecyclePhase.Loop);
                            AdvancePhaseDrivers();
                            return;
                        }
                        break;
                    case UnityVfxLifecyclePhase.Complete:
                    case UnityVfxLifecyclePhase.Dissipate:
                        if (BeginCleanup())
                            return;
                        break;
                    default:
                        return;
                }
            }
        }

        private void AdvancePhaseDrivers()
        {
            _layerScheduler?.AdvancePhase(_phaseElapsedFrames);
            _propertyDriver?.AdvancePhase(_phaseElapsedFrames);
        }

        // Returns true when this playback completed, even if its completion sink starts another.
        private bool EnterPhase(UnityVfxLifecyclePhase phase)
        {
            _phase = phase;
            _phaseElapsedFrames = 0f;
            _cleanupElapsedFrames = 0f;
            _waitingForCleanup = false;
            if (_layerScheduler != null)
                _layerScheduler.EnterPhase(phase);
            else
                PlaySystems(GetSystems(phase));
            _propertyDriver?.EnterPhase(phase);

            if ((phase == UnityVfxLifecyclePhase.Build
                    || phase == UnityVfxLifecyclePhase.Rebuild
                    || phase == UnityVfxLifecyclePhase.Complete
                    || phase == UnityVfxLifecyclePhase.Dissipate)
                && GetDurationFrames(phase) == 0)
            {
                if (phase == UnityVfxLifecyclePhase.Build || phase == UnityVfxLifecyclePhase.Rebuild)
                    return EnterPhase(UnityVfxLifecyclePhase.Loop);
                else
                    return BeginCleanup();
            }
            return false;
        }

        private bool BeginCleanup()
        {
            // Legacy prefabs without a layer scheduler also need an emission boundary;
            // StopEmitting preserves existing particles while cleanup waits for their tails.
            StopAll(clear: false);
            _waitingForCleanup = true;
            _cleanupElapsedFrames = 0f;
            if (!HasLiveParticles())
            {
                CompleteLifecycle();
                return true;
            }
            return false;
        }

        private void CompleteLifecycle()
        {
            StopAll(clear: true);
            _phase = UnityVfxLifecyclePhase.None;
            _waitingForCleanup = false;
            _lifecycleComplete = true;
            IUnityVfxLifecycleCompletionSink sink = _completionSink;
            if (sink != null)
                sink.OnUnityVfxLifecycleCompleted(this, _instanceId);
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
                CacheSystems();
        }

        private void CacheSystems()
        {
            _allSystems = GetComponentsInChildren<ParticleSystem>(true);
            _buildSystems = Collect(_buildRoot);
            _rebuildSystems = Collect(_rebuildRoot);
            _loopSystems = Collect(_loopRoot);
            _completeSystems = Collect(_completeRoot);
            _dissipateSystems = Collect(_dissipateRoot);
            _layerScheduler = GetComponent<UnityVfxBakedLayerScheduler>();
            _propertyDriver = GetComponent<UnityVfxBakedPropertyDriver>();
            _initialized = true;
        }

        private static ParticleSystem[] Collect(GameObject root)
        {
            return root != null ? root.GetComponentsInChildren<ParticleSystem>(true) : Array.Empty<ParticleSystem>();
        }

        private ParticleSystem[] GetSystems(UnityVfxLifecyclePhase phase)
        {
            switch (phase)
            {
                case UnityVfxLifecyclePhase.Build: return _buildSystems;
                case UnityVfxLifecyclePhase.Rebuild: return _rebuildSystems;
                case UnityVfxLifecyclePhase.Loop: return _loopSystems;
                case UnityVfxLifecyclePhase.Complete: return _completeSystems;
                case UnityVfxLifecyclePhase.Dissipate: return _dissipateSystems;
                default: return Array.Empty<ParticleSystem>();
            }
        }

        private int GetDurationFrames(UnityVfxLifecyclePhase phase)
        {
            switch (phase)
            {
                case UnityVfxLifecyclePhase.Build: return _buildFrames;
                case UnityVfxLifecyclePhase.Rebuild: return _rebuildFrames;
                case UnityVfxLifecyclePhase.Loop: return _loopFrames;
                case UnityVfxLifecyclePhase.Complete: return _completeFrames;
                case UnityVfxLifecyclePhase.Dissipate: return _dissipateFrames;
                default: return 0;
            }
        }

        private void StopAll(bool clear)
        {
            ParticleSystemStopBehavior behavior = clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;
            for (int i = 0; i < _allSystems.Length; i++)
            {
                ParticleSystem system = _allSystems[i];
                if (system != null)
                    system.Stop(true, behavior);
            }
        }

        private static void PlaySystems(ParticleSystem[] systems)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                    continue;
                system.Clear(true);
                system.Play(true);
            }
        }

        private bool HasLiveParticles()
        {
            for (int i = 0; i < _allSystems.Length; i++)
            {
                ParticleSystem system = _allSystems[i];
                if (system != null && system.IsAlive(true))
                    return true;
            }
            return false;
        }
    }
}
