using System;
using UnityEngine;

namespace MxFramework.Vfx.Unity
{
    /// <summary>Optional sole clock for explicitly decorative, simple local-space particles.</summary>
    /// <remarks>Internal isolated candidate. Creation is AllocByDesign; playback is NoAlloc after initialization.</remarks>
    internal sealed class UnityVfxParticleBudgetPlayback
    {
        private const float CleanupLimitSourceFrames = 120f;
        private readonly ParticleSystem[] _systems;
        private readonly bool _hasOtherDrivers;
        private float _exitSourceFrames;
        private float _sourceRate = 1f;

        internal UnityVfxParticleBudgetPlayback(ParticleSystem[] systems, bool hasOtherDrivers)
        {
            _systems = systems ?? Array.Empty<ParticleSystem>();
            _hasOtherDrivers = hasOtherDrivers;
        }

        internal bool IsManual { get; private set; }
        internal bool IsExiting { get; private set; }
        internal bool IsComplete { get; private set; } = true;

        internal bool TryBegin(VfxInstanceSnapshot instance, UnityVfxParticleBudgetImportance importance)
        {
            Reset();
            if (importance != UnityVfxParticleBudgetImportance.Decorative || instance == null
                || instance.SourceId <= 0 || _hasOtherDrivers || _systems.Length == 0)
                return false;
            // Fail closed: no scene-space inputs, distance emission, callbacks, sub-emission,
            // collision, or another phase/animation/backend driver may share this clock.
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem system = _systems[i];
                if (system == null) return false;
                ParticleSystem.MainModule main = system.main;
                ParticleSystem.MinMaxCurve distance = system.emission.rateOverDistance;
                Vector3 scale = system.transform.lossyScale;
                if (!main.loop || scale.x != 1f || scale.y != 1f || scale.z != 1f
                    || main.useUnscaledTime || main.simulationSpace != ParticleSystemSimulationSpace.Local
                    || main.stopAction != ParticleSystemStopAction.None
                    || system.subEmitters.enabled || system.collision.enabled || system.trigger.enabled
                    || system.externalForces.enabled || system.inheritVelocity.enabled
                    || system.velocityOverLifetime.enabled || system.forceOverLifetime.enabled
                    || distance.mode != ParticleSystemCurveMode.Constant || distance.constant != 0f)
                    return false;
            }
            _sourceRate = Mathf.Clamp(instance.PlaybackRatePermille / 1000f, 0.05f, 16f);
            IsManual = true;
            IsComplete = false;
            PauseAll();
            return true;
        }

        internal void Advance(float scaledDeltaSeconds)
        {
            if (!IsManual || IsComplete) return;
            if (IsExiting && !HasLiveParticles())
            {
                IsComplete = true;
                return;
            }
            if (!(scaledDeltaSeconds > 0f)
                || float.IsNaN(scaledDeltaSeconds) || float.IsInfinity(scaledDeltaSeconds)) return;
            // Guard against an external Play: never add a manual step after automatic ownership
            // resumed. Keep native playback full frequency and let release clear it normally.
            for (int i = 0; i < _systems.Length; i++)
            {
                if (_systems[i] != null && _systems[i].isPlaying)
                {
                    for (int other = 0; other < _systems.Length; other++)
                        if (_systems[other] != null && _systems[other].isPaused)
                            _systems[other].Play(false);
                    IsManual = false;
                    IsComplete = true;
                    return;
                }
            }
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem system = _systems[i];
                if (system == null) continue;
                // Existing presenter simulationSpeed already carries the instance multiplier.
                // Each cached system is advanced once; children are never recursively advanced.
                // Variable step consumes the exact admitted duration, with no fixed-step rounding.
                system.Simulate(scaledDeltaSeconds, withChildren: false, restart: false, fixedTimeStep: false);
            }
            if (IsExiting)
            {
                _exitSourceFrames += scaledDeltaSeconds * 60f * _sourceRate;
                IsComplete = !HasLiveParticles() || _exitSourceFrames >= CleanupLimitSourceFrames;
            }
        }

        internal void BeginExit()
        {
            if (!IsManual) return;
            IsExiting = true;
            _exitSourceFrames = 0f;
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem system = _systems[i];
                if (system == null) continue;
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            PauseAll();
            IsComplete = !HasLiveParticles();
        }

        internal void Reset()
        {
            IsManual = false;
            IsExiting = false;
            IsComplete = true;
            _exitSourceFrames = 0f;
            _sourceRate = 1f;
        }

        private void PauseAll()
        {
            for (int i = 0; i < _systems.Length; i++)
                if (_systems[i] != null) _systems[i].Pause(false);
        }

        private bool HasLiveParticles()
        {
            for (int i = 0; i < _systems.Length; i++)
                // IsAlive stays true for a paused looping system even after StopEmitting
                // and the last particle's death. Admission forbids sub-emitters/drivers;
                // exit has stopped emission, so remaining native particles are the tail.
                if (_systems[i] != null && _systems[i].particleCount > 0) return true;
            return false;
        }
    }
}
