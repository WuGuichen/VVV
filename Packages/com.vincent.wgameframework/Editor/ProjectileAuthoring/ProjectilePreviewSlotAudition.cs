using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Core.Math;
using MxFramework.Editor.Authoring;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.ProjectileAuthoring
{
    /// <summary>Plays one configured projectile VFX slot without requiring a gameplay lifecycle event.</summary>
    /// <remarks>Selection is AllocByDesign. Warm particle advancement is NoAlloc.</remarks>
    internal sealed class ProjectilePreviewSlotAudition : IDisposable
    {
        private readonly AuthoringPreviewRenderScene _scene;
        private readonly ProjectilePreviewUnityObjectHost _host;
        private GameObject _instance;
        private ParticleSystem[] _particles = Array.Empty<ParticleSystem>();
        private Component[] _graphs = Array.Empty<Component>();
        private UnityVfxLifecycleController[] _lifecycles = Array.Empty<UnityVfxLifecycleController>();
        private double _lastTime;
        private float _elapsed;
        private float _duration = 4f;
        private float _speed = 1f;
        private bool _cameraInitialized;
        private bool _playing;
        private bool _disposed;

        internal ProjectilePreviewSlotAudition(AuthoringPreviewRenderScene scene, ProjectilePreviewUnityObjectHost host)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        internal bool IsActive => _instance != null;
        internal bool IsPlaying => _playing;
        internal float ElapsedSeconds => _elapsed;
        internal float FirstParticleTimeForTests => _particles.Length == 0 ? 0f : _particles[0].time;

        internal bool Play(GameObject prefab, in CombatProjectileUnityPresentationSlot slot)
        {
            Stop();
            if (_disposed || prefab == null)
                return false;

            _instance = _host.Instantiate(prefab);
            if (_instance == null)
                return false;
            ApplyPose(_instance.transform, slot);
            _particles = _instance.GetComponentsInChildren<ParticleSystem>(true);
            _graphs = FindVisualEffectGraphs(_instance);
            _lifecycles = _instance.GetComponentsInChildren<UnityVfxLifecycleController>(true);
            _duration = EstimateDuration(slot.LifetimeFramesOverride, _particles);
            for (int index = 0; index < _particles.Length; index++)
            {
                _particles[index].useAutoRandomSeed = false;
                _particles[index].randomSeed = unchecked(31u + (uint)index * 7919u);
            }
            Restart();
            if (!_cameraInitialized)
            {
                _scene.AutoFrame(_instance);
                _cameraInitialized = true;
            }
            EditorApplication.update += OnEditorUpdate;
            return true;
        }

        internal void Toggle()
        {
            if (_instance == null)
                return;
            _playing = !_playing;
            _lastTime = EditorApplication.timeSinceStartup;
        }

        internal void Restart()
        {
            if (_instance == null)
                return;
            _elapsed = 0f;
            if (_lifecycles.Length > 0)
            {
                for (int index = 0; index < _lifecycles.Length; index++)
                    _lifecycles[index].PlayBuild(new VfxInstanceId(index + 1));
            }
            else
            {
                for (int index = 0; index < _particles.Length; index++)
                {
                    ParticleSystem particle = _particles[index];
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particle.Simulate(0f, false, true, true);
                    particle.Play(false);
                }
            }
            InvokeGraphs("Reinit");
            InvokeGraphs("Play");
            _playing = true;
            _lastTime = EditorApplication.timeSinceStartup;
            _scene.RequestRepaint();
        }

        internal void SetSpeed(float speed) => _speed = Mathf.Clamp(speed, .05f, 4f);

        internal void Stop()
        {
            EditorApplication.update -= OnEditorUpdate;
            _playing = false;
            _elapsed = 0f;
            _particles = Array.Empty<ParticleSystem>();
            _graphs = Array.Empty<Component>();
            _lifecycles = Array.Empty<UnityVfxLifecycleController>();
            if (_instance != null)
            {
                _host.Release(_instance);
                _instance = null;
            }
        }

        internal void AdvanceForTests(float deltaSeconds) => Advance(deltaSeconds);

        public void Dispose()
        {
            if (_disposed)
                return;
            Stop();
            _disposed = true;
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = _playing ? Mathf.Clamp((float)(now - _lastTime) * _speed, 0f, .1f) : 0f;
            _lastTime = now;
            Advance(delta);
        }

        private void Advance(float deltaSeconds)
        {
            if (!_playing || _instance == null || deltaSeconds <= 0f)
                return;
            _elapsed += deltaSeconds;
            bool alive = _graphs.Length > 0;
            for (int index = 0; index < _lifecycles.Length; index++)
            {
                UnityVfxLifecycleController lifecycle = _lifecycles[index];
                lifecycle.Advance(deltaSeconds);
                alive |= lifecycle.CurrentPhase != UnityVfxLifecyclePhase.None;
            }
            for (int index = 0; index < _particles.Length; index++)
            {
                ParticleSystem particle = _particles[index];
                particle.Simulate(deltaSeconds, false, false, true);
                alive |= particle.IsAlive(false);
            }
            if (_elapsed >= _duration || (!alive && _particles.Length > 0 && _elapsed > .05f))
                Restart();
            else
                _scene.RequestRepaint();
        }

        private void InvokeGraphs(string methodName)
        {
            for (int index = 0; index < _graphs.Length; index++)
            {
                MethodInfo method = _graphs[index].GetType().GetMethod(methodName, Type.EmptyTypes);
                method?.Invoke(_graphs[index], null);
            }
        }

        private static Component[] FindVisualEffectGraphs(GameObject root)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            var graphs = new List<Component>();
            for (int index = 0; index < components.Length; index++)
                if (string.Equals(components[index]?.GetType().FullName, "UnityEngine.VFX.VisualEffect", StringComparison.Ordinal))
                    graphs.Add(components[index]);
            return graphs.ToArray();
        }

        private static float EstimateDuration(int lifetimeFramesOverride, ParticleSystem[] particles)
        {
            if (lifetimeFramesOverride > 0)
                return Mathf.Clamp(lifetimeFramesOverride / 60f, .25f, 30f);
            float duration = 0f;
            for (int index = 0; index < particles.Length; index++)
            {
                ParticleSystem.MainModule main = particles[index].main;
                float candidate = main.loop ? 4f : main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;
                duration = Mathf.Max(duration, candidate);
            }
            return Mathf.Clamp(duration <= 0f ? 4f : duration, .25f, 30f);
        }

        private static void ApplyPose(Transform transform, in CombatProjectileUnityPresentationSlot slot)
        {
            var forward = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
            var normal = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
            CombatProjectileUnityPresentationPose pose = CombatProjectileUnityPresentationPoseResolver.Resolve(
                slot.Orientation, slot.LocalOffset, FixVector3.Zero, forward, normal);
            Vector3 resolvedForward = ToVector3(pose.Forward);
            transform.SetPositionAndRotation(ToVector3(pose.Position),
                resolvedForward.sqrMagnitude > .000001f
                    ? Quaternion.LookRotation(resolvedForward.normalized, Vector3.up)
                    : Quaternion.identity);
        }

        private static Vector3 ToVector3(FixVector3 value) => new Vector3(
            (float)value.X.RawValue / Fix64.Scale,
            (float)value.Y.RawValue / Fix64.Scale,
            (float)value.Z.RawValue / Fix64.Scale);
    }
}
