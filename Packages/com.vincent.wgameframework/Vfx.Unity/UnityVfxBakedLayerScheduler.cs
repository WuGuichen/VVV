using System;
using UnityEngine;

namespace MxFramework.Vfx.Unity
{
    /// <summary>Defines how an original-speed VFX layer ends at its authored playback-window boundary.</summary>
    public enum UnityVfxLayerWindowEndPolicy
    {
        /// <summary>Stops new emission and lets existing particles finish naturally.</summary>
        StopEmitting,
        /// <summary>Stops emission and immediately clears all existing particles.</summary>
        StopAndClear,
        /// <summary>Stops emission before the boundary, then clears remaining particles at the boundary.</summary>
        DissipateThenClear
    }

    [Serializable]
    public sealed class UnityVfxBakedLayerScheduleEntry
    {
        [SerializeField] private GameObject _layerRoot;
        [SerializeField] private UnityVfxLifecyclePhase _phase;
        [SerializeField] private int _startFrame;
        [SerializeField] private int _artisticEndFrame = 1;
        [SerializeField] private UnityVfxLayerWindowEndPolicy _windowEndPolicy;
        [SerializeField] private int _dissipateFrames;

        public GameObject LayerRoot => _layerRoot;
        public UnityVfxLifecyclePhase Phase => _phase;
        public int StartFrame => _startFrame;
        public int ArtisticEndFrame => _artisticEndFrame;
        public UnityVfxLayerWindowEndPolicy WindowEndPolicy => _windowEndPolicy;
        public int DissipateFrames => _dissipateFrames;
        public int DissipateStartFrame => Mathf.Max(_startFrame, _artisticEndFrame - _dissipateFrames);

        /// <summary>Sets one authoring-baked layer schedule entry.</summary>
        /// <remarks>AllocByDesign; Editor baking only.</remarks>
        public void Configure(GameObject layerRoot, UnityVfxLifecyclePhase phase, int startFrame, int artisticEndFrame)
        {
            Configure(layerRoot, phase, startFrame, artisticEndFrame, UnityVfxLayerWindowEndPolicy.StopEmitting, 0);
        }

        /// <summary>Sets one authoring-baked layer schedule entry and its playback-window ending behavior.</summary>
        /// <remarks>AllocByDesign; Editor baking only.</remarks>
        public void Configure(
            GameObject layerRoot,
            UnityVfxLifecyclePhase phase,
            int startFrame,
            int artisticEndFrame,
            UnityVfxLayerWindowEndPolicy windowEndPolicy,
            int dissipateFrames)
        {
            _layerRoot = layerRoot;
            _phase = phase;
            _startFrame = Mathf.Max(0, startFrame);
            _artisticEndFrame = Mathf.Max(_startFrame + 1, artisticEndFrame);
            _windowEndPolicy = windowEndPolicy;
            _dissipateFrames = Mathf.Clamp(dissipateFrames, 0, _artisticEndFrame - _startFrame);
        }
    }

    /// <summary>Starts and stops baked VFX layers at exact integer-frame boundaries.</summary>
    /// <remarks>Configure/cache are AllocByDesign. EnterPhase, AdvancePhase and RestartPhase are NoAlloc after initialization.</remarks>
    [DisallowMultipleComponent]
    public sealed class UnityVfxBakedLayerScheduler : MonoBehaviour
    {
        [SerializeField] private UnityVfxBakedLayerScheduleEntry[] _entries = Array.Empty<UnityVfxBakedLayerScheduleEntry>();

        private ParticleSystem[][] _systems = Array.Empty<ParticleSystem[]>();
        private bool[] _started = Array.Empty<bool>();
        private bool[] _dissipating = Array.Empty<bool>();
        private bool[] _stopped = Array.Empty<bool>();
        private bool _initialized;
        private UnityVfxLifecyclePhase _phase;

        public int EntryCount => _entries?.Length ?? 0;

        /// <summary>Assigns the complete immutable schedule during Candidate baking.</summary>
        /// <remarks>AllocByDesign; Editor baking only.</remarks>
        public void Configure(UnityVfxBakedLayerScheduleEntry[] entries)
        {
            _entries = entries ?? Array.Empty<UnityVfxBakedLayerScheduleEntry>();
            Cache();
        }

        /// <summary>Resets exact layer activation for a lifecycle phase.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void EnterPhase(UnityVfxLifecyclePhase phase)
        {
            EnsureInitialized();
            _phase = phase;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Phase != phase) continue;
                StopSystems(_systems[i], clear: true);
                _started[i] = false;
                _dissipating[i] = false;
                _stopped[i] = false;
            }
            AdvancePhase(0f);
        }

        /// <summary>Applies the schedule at the shared floating-point phase frame.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void AdvancePhase(float phaseFrame)
        {
            if (!_initialized || _phase == UnityVfxLifecyclePhase.None) return;
            for (int i = 0; i < _entries.Length; i++)
            {
                UnityVfxBakedLayerScheduleEntry entry = _entries[i];
                if (entry.Phase != _phase) continue;
                if (!_started[i] && phaseFrame >= entry.StartFrame)
                {
                    PlaySystems(_systems[i]);
                    _started[i] = true;
                }
                if (_started[i]
                    && !_dissipating[i]
                    && !_stopped[i]
                    && entry.WindowEndPolicy == UnityVfxLayerWindowEndPolicy.DissipateThenClear
                    && entry.DissipateFrames > 0
                    && phaseFrame >= entry.DissipateStartFrame)
                {
                    StopSystems(_systems[i], clear: false);
                    _dissipating[i] = true;
                }
                if (_started[i] && !_stopped[i] && phaseFrame >= entry.ArtisticEndFrame)
                {
                    bool clear = entry.WindowEndPolicy != UnityVfxLayerWindowEndPolicy.StopEmitting;
                    StopSystems(_systems[i], clear);
                    _stopped[i] = true;
                }
            }
        }

        /// <summary>Restarts one continuous Loop schedule without allocating.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void RestartPhase()
        {
            EnterPhase(_phase);
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (!_initialized) Cache();
        }

        private void Cache()
        {
            _entries ??= Array.Empty<UnityVfxBakedLayerScheduleEntry>();
            _systems = new ParticleSystem[_entries.Length][];
            _started = new bool[_entries.Length];
            _dissipating = new bool[_entries.Length];
            _stopped = new bool[_entries.Length];
            for (int i = 0; i < _entries.Length; i++)
            {
                GameObject root = _entries[i]?.LayerRoot;
                _systems[i] = root != null ? root.GetComponentsInChildren<ParticleSystem>(true) : Array.Empty<ParticleSystem>();
            }
            _initialized = true;
        }

        private static void PlaySystems(ParticleSystem[] systems)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null) continue;
                system.Clear(true);
                system.Play(true);
            }
        }

        private static void StopSystems(ParticleSystem[] systems, bool clear)
        {
            ParticleSystemStopBehavior behavior = clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system != null) system.Stop(true, behavior);
            }
        }
    }
}
