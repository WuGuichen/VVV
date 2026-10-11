using System;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Unity time gate. Custom physics/Runtime drivers must also poll this gate before stepping.</summary>
    public sealed class UnityTacticalTimeAdapter : ITacticalTimeAdapter, ITacticalTimeTransitionAdapter, IDisposable
    {
        private float _simulationTimeScale;
        private bool _ownsGlobalTime;
        private float _detachedScale;
        private float WorldScale {
            get => _ownsGlobalTime ? Time.timeScale : _detachedScale;
            set { _detachedScale = value; if (_ownsGlobalTime) Time.timeScale = value; }
        }
        internal bool IsDetached => !_ownsGlobalTime;
        internal void RelinquishGlobalTime() { _detachedScale = WorldScale; _ownsGlobalTime = false; }
        internal float ShutdownRestoreScale => _entryTimeScale;
        internal void ClaimGlobalTime(float restoreScale) { if (_disposed) throw new ObjectDisposedException(nameof(UnityTacticalTimeAdapter)); _entryTimeScale = restoreScale; _ownsGlobalTime = true; Time.timeScale = _detachedScale; }

        private float _entryTimeScale;
        private bool _disposed;
        private readonly float _accelerationSeconds, _decelerationSeconds;
        private float _transitionFrom, _transitionTo, _transitionElapsed, _transitionDuration;
        private bool _transitioning;
        private bool _firstTransitionTick;
        private double _transitionRequestedAt;
        internal bool IsTransitioning => _transitioning;
        float ITacticalTimeTransitionAdapter.WorldRateFraction => Mathf.Clamp01(WorldScale / _simulationTimeScale);
        float ITacticalTimeTransitionAdapter.TransitionProgress => _transitioning
            ? Mathf.Clamp01(_transitionElapsed / _transitionDuration) : 1f;
        bool ITacticalTimeTransitionAdapter.IsAccelerating => _transitioning && _transitionTo > 0f;
        bool ITacticalTimeTransitionAdapter.IsDecelerating => _transitioning && _transitionTo == 0f;
        public bool IsSimulationPaused => WorldScale == 0f;
        /// <summary>Polling marker for external drivers; no callbacks can re-enter the coordinator.</summary>
        public uint PauseRevision { get; private set; }
        public float SimulationTimeScale => _simulationTimeScale;

        public UnityTacticalTimeAdapter(float simulationTimeScale = 1f)
            : this(simulationTimeScale, 0f, 0f) { }

        internal UnityTacticalTimeAdapter(float simulationTimeScale, float accelerationSeconds, float decelerationSeconds)
            : this(simulationTimeScale, accelerationSeconds, decelerationSeconds, false) { }

        internal UnityTacticalTimeAdapter(float simulationTimeScale, float accelerationSeconds, float decelerationSeconds, bool detached)
        {
            ValidateScale(simulationTimeScale);
            if (float.IsNaN(accelerationSeconds) || float.IsInfinity(accelerationSeconds) || accelerationSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(accelerationSeconds));
            if (float.IsNaN(decelerationSeconds) || float.IsInfinity(decelerationSeconds) || decelerationSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(decelerationSeconds));
            _entryTimeScale = Time.timeScale;
            _detachedScale = _entryTimeScale;
            _ownsGlobalTime = !detached;
            _simulationTimeScale = simulationTimeScale;
            _accelerationSeconds = accelerationSeconds;
            _decelerationSeconds = decelerationSeconds;
        }

        /// <remarks>NoAlloc. Call at a completed world tick boundary. Does not change fixedDeltaTime.</remarks>
        public void SetSimulationPaused(bool paused)
        {
            if (_disposed) return;
            _transitioning = false;
            float scale = paused ? 0f : _simulationTimeScale;
            if (WorldScale == scale) return;
            WorldScale = scale;
            unchecked { PauseRevision++; }
        }

        void ITacticalTimeTransitionAdapter.SetSimulationPauseTarget(bool paused, bool immediate)
        {
            if (_disposed) return;
            float target = paused ? 0f : _simulationTimeScale;
            float duration = paused ? _decelerationSeconds : _accelerationSeconds;
            if (immediate || duration <= 0f) { SetSimulationPaused(paused); return; }
            if (_transitioning && _transitionTo == target) return;
            if (WorldScale == target) { _transitioning = false; return; }
            _transitionFrom = WorldScale;
            _transitionTo = target;
            _transitionDuration = duration;
            _transitionElapsed = 0f;
            _transitionRequestedAt = Time.realtimeSinceStartupAsDouble;
            _firstTransitionTick = true;
            _transitioning = true;
        }

        /// <summary>NoAlloc. Advanced once per rendered frame using real time, including while scale is zero.</summary>
        internal void TickTransition(float unscaledDeltaTime)
        {
            if (_disposed || !_transitioning || !(unscaledDeltaTime > 0f) || float.IsInfinity(unscaledDeltaTime)) return;
            if (_firstTransitionTick)
            {
                _firstTransitionTick = false;
                // The preceding frame may include synchronous card preparation before this request.
                // That time must not consume the new acceleration/deceleration envelope.
                if (Application.isPlaying)
                    unscaledDeltaTime = Mathf.Min(unscaledDeltaTime,
                        (float)Math.Max(0d, Time.realtimeSinceStartupAsDouble - _transitionRequestedAt));
            }
            _transitionElapsed = Mathf.Min(_transitionElapsed + unscaledDeltaTime, _transitionDuration);
            float t = _transitionElapsed / _transitionDuration;
            // Match the filter envelope: build momentum through the end instead of easing out.
            float eased = t * t * t;
            bool wasPaused = IsSimulationPaused;
            WorldScale = _transitionElapsed >= _transitionDuration
                ? _transitionTo : Mathf.Lerp(_transitionFrom, _transitionTo, eased);
            if (wasPaused != IsSimulationPaused) unchecked { PauseRevision++; }
            if (_transitionElapsed >= _transitionDuration) _transitioning = false;
        }

        /// <summary>Changes the running rate while preserving an existing pause.</summary>
        public void SetSimulationTimeScale(float scale)
        {
            if (_disposed) return;
            ValidateScale(scale);
            _simulationTimeScale = scale;
            if (_transitioning)
            {
                bool pausing = _transitionTo == 0f;
                _transitioning = false;
                ((ITacticalTimeTransitionAdapter)this).SetSimulationPauseTarget(pausing, false);
                return;
            }
            if (!IsSimulationPaused) SetSimulationPaused(false);
        }

        /// <summary>NoAlloc. The session's time owner calls this after releasing its bridges and menu ownership on shutdown.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _transitioning = false;
            if (!_ownsGlobalTime || WorldScale == _entryTimeScale) return;
            WorldScale = _entryTimeScale;
            unchecked { PauseRevision++; }
        }

        private static void ValidateScale(float scale)
        {
            if (!(scale > 0f) || float.IsInfinity(scale)) throw new ArgumentOutOfRangeException(nameof(scale));
        }
    }
}
