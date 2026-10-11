using System;
using System.Collections.Generic;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Vfx.Unity
{
    public readonly struct UnityVfxResolvedAnchor
    {
        public UnityVfxResolvedAnchor(Vector3 position, Vector3 forward)
        {
            Position = position;
            Forward = forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector3.forward;
        }

        public Vector3 Position { get; }
        public Vector3 Forward { get; }
    }

    public interface IUnityVfxAnchorResolver
    {
        bool TryResolve(in VfxAnchor anchor, out UnityVfxResolvedAnchor resolvedAnchor);
    }

    public interface IUnityVfxInstanceReceiver
    {
        void OnUnityVfxInstanceSpawned(VfxInstanceSnapshot instance);

        void OnUnityVfxInstanceReleased(VfxInstanceId instanceId);
    }

    /// <summary>Optional Unity presentation backend attached to a Published Effect Prefab.</summary>
    /// <remarks>NoAlloc after initialization. Implementations are collected once per instantiated or prewarmed view.</remarks>
    public interface IUnityVfxBackendController
    {
        bool IsAvailable { get; }
        bool SuppressParticleSystemFallbackWhenAvailable { get; }
        void ReinitializeAndPlay(VfxInstanceSnapshot instance);
        void StopAndClear();
    }

    public sealed class UnityVfxPresenter : IVfxRuntimeEventSink, IUnityVfxLifecycleCompletionSink, IDisposable
    {
        private readonly VfxRuntimeModule _vfx;
        private readonly IResourceManager _resources;
        private readonly bool _usePooling;
        private readonly IUnityVfxAnchorResolver _anchorResolver;
        private readonly IUnityVfxBackendBindingSource _backendBindingSource;
        private readonly IUnityVfxPresentationBudgetSource _presentationBudgetSource;
        private readonly List<VfxRuntimeEvent> _events = new List<VfxRuntimeEvent>();
        private readonly Dictionary<long, ActiveInstance> _active = new Dictionary<long, ActiveInstance>();
        private readonly Dictionary<long, ActiveInstance> _retiring = new Dictionary<long, ActiveInstance>();
        private readonly List<long> _activeIds = new List<long>(32);
        private readonly List<VfxInstanceSnapshot> _runtimeInstances = new List<VfxInstanceSnapshot>(32);
        private readonly Dictionary<ResourceKey, Stack<ActiveInstance>> _pool = new Dictionary<ResourceKey, Stack<ActiveInstance>>();
        private readonly Dictionary<ResourceKey, ResourceHandle<GameObject>> _prefabHandles =
            new Dictionary<ResourceKey, ResourceHandle<GameObject>>();
        private readonly List<string> _diagnostics = new List<string>();
        private bool _isDisposed;

        public UnityVfxPresenter(
            VfxRuntimeModule vfx,
            IResourceManager resources,
            bool usePooling = true,
            IUnityVfxAnchorResolver anchorResolver = null,
            IUnityVfxBackendBindingSource backendBindingSource = null)
            : this(vfx, resources, usePooling, anchorResolver, backendBindingSource, null)
        {
        }

        public UnityVfxPresenter(
            VfxRuntimeModule vfx,
            IResourceManager resources,
            bool usePooling,
            IUnityVfxAnchorResolver anchorResolver,
            IUnityVfxBackendBindingSource backendBindingSource,
            IUnityVfxPresentationBudgetSource presentationBudgetSource)
        {
            _vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _usePooling = usePooling;
            _anchorResolver = anchorResolver;
            _backendBindingSource = backendBindingSource;
            _presentationBudgetSource = presentationBudgetSource;
        }

        public IReadOnlyList<string> Diagnostics => _diagnostics;
        public int ActiveCount => _active.Count;

        public void PrewarmDefinitions(int poolSizePerDefinition = 1)
        {
            ThrowIfDisposed();
            IReadOnlyList<VfxDefinition> definitions = _vfx.Definitions.CreateSnapshot();
            for (int i = 0; i < definitions.Count; i++)
            {
                VfxDefinition definition = definitions[i];
                if (_resources.Contains(definition.PrefabResource))
                    PrewarmDefinition(definition, poolSizePerDefinition);
            }
        }

        public int DrainFrame(RuntimeFrame frame)
        {
            ThrowIfDisposed();
            _events.Clear();
            int drained = _vfx.DrainEvents(frame, _events);
            ApplyEvents(_events);
            _events.Clear();
            return drained;
        }

        public void SyncActiveAnchors()
        {
            ThrowIfDisposed();
            if (_active.Count == 0)
                return;

            _activeIds.Clear();
            foreach (long instanceId in _active.Keys)
                _activeIds.Add(instanceId);

            for (int i = 0; i < _activeIds.Count; i++)
            {
                if (_active.TryGetValue(_activeIds[i], out ActiveInstance active)
                    && ShouldUpdatePresentation(active))
                    ApplyAnchor(active);
            }
        }

        /// <summary>NoAlloc after warmup. Called once after animation presentation, including retiring views.</summary>
        public void TickPresentationBackends(float deltaTime)
        {
            ThrowIfDisposed();
            foreach (ActiveInstance active in _active.Values)
            {
                active.PendingBackendDeltaSeconds += Math.Max(0f, deltaTime);
                if (!ShouldUpdatePresentation(active)) continue;
                AdvancePresentation(active, active.PendingBackendDeltaSeconds);
                active.PendingBackendDeltaSeconds = 0f;
            }
            _activeIds.Clear();
            foreach (long id in _retiring.Keys) _activeIds.Add(id);
            for (int i = 0; i < _activeIds.Count; i++)
            {
                long id = _activeIds[i];
                if (!_retiring.TryGetValue(id, out ActiveInstance active)) continue;
                AdvancePresentation(active, deltaTime);
                if (active.Lifecycle != null && !active.Lifecycle.IsLifecycleComplete) continue;
                if (!AreTimedBackendsComplete(active.BackendControllers)) continue;
                if (!active.ParticleBudget.IsComplete) continue;
                _retiring.Remove(id);
                ReleaseActive(active, destroy: !_usePooling);
            }
        }

        private bool ShouldUpdatePresentation(ActiveInstance active)
        {
            if (active.ParticleSystems.Length > 0)
            {
                // Required, unknown or unsupported ordinary particles stay automatic/full frequency.
                if (!active.ParticleBudget.IsManual
                    || GetParticleImportance(active.Snapshot) != UnityVfxParticleBudgetImportance.Decorative)
                    return true;
            }
            return _presentationBudgetSource == null
                || _presentationBudgetSource.ShouldUpdatePresentation(active.Snapshot);
        }

        private UnityVfxParticleBudgetImportance GetParticleImportance(VfxInstanceSnapshot instance)
        {
            return _presentationBudgetSource is IUnityVfxParticleBudgetAdmissionSource admission
                ? admission.GetParticleBudgetImportance(instance)
                : UnityVfxParticleBudgetImportance.Unknown;
        }

        private static void AdvancePresentation(ActiveInstance active, float deltaTime)
        {
            active.ParticleBudget.Advance(deltaTime);
            TickBackends(active.BackendControllers, deltaTime);
        }

        private static void TickBackends(IUnityVfxBackendController[] controllers, float deltaTime)
        {
            for (int i = 0; i < controllers.Length; i++)
                if (controllers[i] is IUnityVfxTimedBackend timed) timed.TickPresentation(deltaTime);
        }

        private static bool AreTimedBackendsComplete(IUnityVfxBackendController[] controllers)
        {
            for (int i = 0; i < controllers.Length; i++)
                if (controllers[i] is IUnityVfxTimedBackend timed && !timed.IsPresentationComplete) return false;
            return true;
        }

        /// <summary>Reconciles a restore boundary by clearing active/retiring views and rebuilding runtime instances.</summary>
        /// <remarks>LowFreqAlloc; intended for save restore, late presenter attachment, or explicit rebinding.</remarks>
        public void SynchronizeActiveInstances()
        {
            ThrowIfDisposed();
            foreach (ActiveInstance active in _active.Values)
                ReleaseActive(active, destroy: !_usePooling);
            _active.Clear();
            foreach (ActiveInstance active in _retiring.Values)
                ReleaseActive(active, destroy: !_usePooling);
            _retiring.Clear();
            _events.Clear();
            _runtimeInstances.Clear();
            _vfx.CopyActiveInstances(_runtimeInstances);
            for (int i = 0; i < _runtimeInstances.Count; i++)
            {
                VfxInstanceSnapshot instance = _runtimeInstances[i];
                var evt = new VfxRuntimeEvent(
                    RuntimeFrame.Zero,
                    VfxRuntimeEventType.Rebuilt,
                    instance.InstanceId,
                    instance.DefinitionId,
                    instance.Anchor,
                    instance.Parameters,
                    string.Empty,
                    instance.TraceId);
                Spawn(evt, rebuild: true);
            }
            _runtimeInstances.Clear();
        }

        public void OnVfxRuntimeEvents(RuntimeFrame frame, IReadOnlyList<VfxRuntimeEvent> events)
        {
            ThrowIfDisposed();
            ApplyEvents(events);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            foreach (ActiveInstance active in _active.Values)
                ReleaseActive(active, destroy: true);
            _active.Clear();
            foreach (ActiveInstance active in _retiring.Values)
                ReleaseActive(active, destroy: true);
            _retiring.Clear();

            foreach (Stack<ActiveInstance> stack in _pool.Values)
            {
                while (stack.Count > 0)
                    ReleasePooled(stack.Pop());
            }

            _pool.Clear();
            ReleaseCachedHandles();
            _prefabHandles.Clear();
            _events.Clear();
            _isDisposed = true;
        }

        private void ApplyEvents(IReadOnlyList<VfxRuntimeEvent> events)
        {
            if (events == null)
                return;

            for (int i = 0; i < events.Count; i++)
            {
                VfxRuntimeEvent evt = events[i];
                switch (evt.Type)
                {
                    case VfxRuntimeEventType.Spawned:
                        Spawn(evt, rebuild: false);
                        break;
                    case VfxRuntimeEventType.Rebuilt:
                        Rebuild(evt.InstanceId);
                        break;
                    case VfxRuntimeEventType.Stopped:
                        Stop(evt.InstanceId, evt.StopMode);
                        break;
                    case VfxRuntimeEventType.Expired:
                        Stop(evt.InstanceId, evt.StopMode);
                        break;
                }
            }
        }

        private void Spawn(in VfxRuntimeEvent evt, bool rebuild)
        {
            if (!_vfx.TryGetInstance(evt.InstanceId, out VfxInstanceSnapshot instance))
            {
                _diagnostics.Add("VFX instance was not found for presentation: " + evt.InstanceId + ".");
                return;
            }

            if (!instance.PrefabResource.IsValid)
            {
                _diagnostics.Add("VFX prefab resource is invalid for definition " + instance.DefinitionId + ".");
                return;
            }

            // Restore/replayed IDs can collide with either a live view or an exit tail.
            if (_active.TryGetValue(instance.InstanceId.Value, out ActiveInstance previous))
            {
                _active.Remove(instance.InstanceId.Value);
                ReleaseActive(previous, destroy: !_usePooling);
            }
            if (_retiring.TryGetValue(instance.InstanceId.Value, out previous))
            {
                _retiring.Remove(instance.InstanceId.Value);
                ReleaseActive(previous, destroy: !_usePooling);
            }
            ActiveInstance active = TryTakeFromPool(instance.PrefabResource);
            if (active == null)
            {
                if (!TryGetPrefabHandle(instance.PrefabResource, out ResourceHandle<GameObject> handle, out string error))
                {
                    ReportPrefabLoadFailure(
                        "spawn instance=" + instance.InstanceId.Value + " definition=" + instance.DefinitionId.Value,
                        instance.PrefabResource, error);
                    return;
                }
                GameObject created = Object.Instantiate(handle.Value);
                ParticleSystem[] systems = CollectParticleSystems(created);
                active = new ActiveInstance(created, systems, CollectParticleSimulationSpeeds(systems),
                    CollectBackendControllers(created), created.GetComponent<UnityVfxLifecycleController>());
                if (_usePooling)
                    _diagnostics.Add("VFX pool exhausted for definition " + instance.DefinitionId.Value + ".");
            }
            active.Bind(instance);
            GameObject view = active.View;
            view.name = "VFX_" + instance.DefinitionId.Value + "_" + instance.InstanceId.Value;
            ParticleSystem[] particleSystems = active.ParticleSystems;
            IUnityVfxBackendController[] backendControllers = active.BackendControllers;
            UnityVfxLifecycleController lifecycle = active.Lifecycle;
            ApplyAnchor(active);
            ApplyPlaybackRate(active);
            _backendBindingSource?.BindBackend(view, instance);
            NotifySpawned(active.Receivers, instance);
            view.SetActive(true);
            _active[instance.InstanceId.Value] = active;
            if (lifecycle != null)
            {
                lifecycle.SetCompletionSink(this);
                lifecycle.SetPlaybackRate(instance.PlaybackRatePermille / 1000f);
                if (rebuild)
                    lifecycle.PlayRebuild(instance.InstanceId);
                else
                    lifecycle.PlayBuild(instance.InstanceId);
                lifecycle.SeekSourceFrames(instance.SourceStartOffsetFrames);
                PreSimulateParticles(
                    particleSystems,
                    instance.SourceStartOffsetFrames,
                    instance.PlaybackRatePermille);
            }
            else
            {
                PlayBackends(instance, particleSystems, backendControllers);
                active.ParticleBudget.TryBegin(instance, GetParticleImportance(instance));
            }
        }

        private void PrewarmDefinition(VfxDefinition definition, int poolSize)
        {
            if (definition == null || !definition.PrefabResource.IsValid)
                return;

            if (!TryGetPrefabHandle(definition.PrefabResource, out ResourceHandle<GameObject> handle, out string error))
            {
                ReportPrefabLoadFailure(
                    "prewarm definition=" + definition.DefinitionId.Value,
                    definition.PrefabResource,
                    error);
                return;
            }

            if (!_usePooling || poolSize <= 0)
                return;

            if (!_pool.TryGetValue(definition.PrefabResource, out Stack<ActiveInstance> stack))
            {
                stack = new Stack<ActiveInstance>(Math.Max(1, poolSize));
                _pool.Add(definition.PrefabResource, stack);
            }

            while (stack.Count < poolSize)
            {
                GameObject view = Object.Instantiate(handle.Value);
                ParticleSystem[] particleSystems = CollectParticleSystems(view);
                view.SetActive(false);
                stack.Push(new ActiveInstance(
                    view,
                    particleSystems,
                    CollectParticleSimulationSpeeds(particleSystems),
                    CollectBackendControllers(view),
                    view.GetComponent<UnityVfxLifecycleController>()));
            }
        }

        private void Rebuild(VfxInstanceId instanceId)
        {
            if (!_active.TryGetValue(instanceId.Value, out ActiveInstance active))
                return;

            active.PendingBackendDeltaSeconds = 0f;
            active.ParticleBudget.Reset();
            if (active.Lifecycle != null)
                active.Lifecycle.PlayRebuild(instanceId);
            else
            {
                PlayBackends(active.Snapshot, active.ParticleSystems, active.BackendControllers);
                active.ParticleBudget.TryBegin(active.Snapshot, GetParticleImportance(active.Snapshot));
            }
        }

        private void Stop(VfxInstanceId instanceId, VfxStopMode stopMode)
        {
            if (!_active.TryGetValue(instanceId.Value, out ActiveInstance active))
                return;

            _active.Remove(instanceId.Value);
            if (stopMode == VfxStopMode.Immediate)
            {
                ReleaseActive(active, destroy: !_usePooling);
                return;
            }
            // Admit the exit at the current anchor and flush body time before starting its tail.
            // Retiring clocks and completion checks always run every presentation traversal.
            if (_presentationBudgetSource != null)
                ApplyAnchor(active);
            if (active.PendingBackendDeltaSeconds > 0f)
            {
                AdvancePresentation(active, active.PendingBackendDeltaSeconds);
                active.PendingBackendDeltaSeconds = 0f;
            }
            active.ParticleBudget.BeginExit();
            for (int i = 0; i < active.BackendControllers.Length; i++)
                if (active.BackendControllers[i] is IUnityVfxTimedBackend timed) timed.BeginPresentationExit(stopMode);
            if ((active.Lifecycle == null || active.Lifecycle.IsLifecycleComplete)
                && AreTimedBackendsComplete(active.BackendControllers)
                && active.ParticleBudget.IsComplete)
            {
                ReleaseActive(active, destroy: !_usePooling);
                return;
            }

            _retiring[instanceId.Value] = active;
            if (active.Lifecycle != null) active.Lifecycle.PlayExit(stopMode);
        }

        void IUnityVfxLifecycleCompletionSink.OnUnityVfxLifecycleCompleted(
            UnityVfxLifecycleController controller,
            VfxInstanceId instanceId)
        {
            if (!_retiring.TryGetValue(instanceId.Value, out ActiveInstance active)
                || active.Lifecycle != controller)
                return;

            if (!AreTimedBackendsComplete(active.BackendControllers)) return;
            if (!active.ParticleBudget.IsComplete) return;
            _retiring.Remove(instanceId.Value);
            ReleaseActive(active, destroy: !_usePooling);
        }

        private void ReleaseActive(ActiveInstance active, bool destroy)
        {
            if (active.View == null)
            {
                return;
            }

            StopParticles(active.ParticleSystems);
            active.ParticleBudget.Reset();
            StopBackends(active.BackendControllers);
            if (active.Lifecycle != null)
                active.Lifecycle.ForceClear();
            NotifyReleased(active.Receivers, active.InstanceId);
            if (destroy)
            {
                DestroyObject(active.View);
                return;
            }

            active.View.SetActive(false);
            if (!_pool.TryGetValue(active.ResourceKey, out Stack<ActiveInstance> stack))
            {
                stack = new Stack<ActiveInstance>();
                _pool.Add(active.ResourceKey, stack);
            }

            active.ClearBinding();
            stack.Push(active);
        }

        private void ApplyAnchor(ActiveInstance active)
        {
            if (active == null || active.View == null)
                return;

            UnityVfxResolvedAnchor resolved;
            if (!TryResolveAnchor(active.Anchor, out resolved))
            {
                resolved = new UnityVfxResolvedAnchor(
                    ToVector3(active.Anchor.Pose.Position),
                    ToForward(active.Anchor.Pose.Forward));
            }

            Transform transform = active.View.transform;
            transform.position = resolved.Position;
            transform.rotation = Quaternion.LookRotation(resolved.Forward, Vector3.up)
                * Quaternion.Euler(ToVector3(active.Anchor.Pose.RotationEulerDegrees));
            transform.localScale = ToVector3(active.Anchor.Pose.Scale);
        }

        private bool TryResolveAnchor(VfxAnchor anchor, out UnityVfxResolvedAnchor resolved)
        {
            if (anchor.Kind == VfxAnchorKind.World)
            {
                resolved = new UnityVfxResolvedAnchor(
                    ToVector3(anchor.Pose.Position),
                    ToForward(anchor.Pose.Forward));
                return true;
            }

            if (_anchorResolver != null && _anchorResolver.TryResolve(anchor, out resolved))
                return true;

            resolved = default;
            return false;
        }

        private ActiveInstance TryTakeFromPool(ResourceKey key)
        {
            if (!_usePooling || !_pool.TryGetValue(key, out Stack<ActiveInstance> stack))
                return null;

            while (stack.Count > 0)
            {
                ActiveInstance pooled = stack.Pop();
                if (pooled.View != null)
                    return pooled;
            }

            return null;
        }

        private void ReleasePooled(ActiveInstance pooled)
        {
            if (pooled == null)
                return;

            DestroyObject(pooled.View);
        }

        private bool TryGetPrefabHandle(
            ResourceKey key,
            out ResourceHandle<GameObject> handle,
            out string error)
        {
            error = string.Empty;
            if (_prefabHandles.TryGetValue(key, out handle)
                && handle != null
                && !handle.IsReleased
                && handle.Value != null)
            {
                return true;
            }

            ResourceLoadResult<ResourceHandle<GameObject>> load = _resources.Load<GameObject>(key);
            if (!load.Success || load.Value == null || load.Value.Value == null)
            {
                handle = null;
                error = load.Error.ToString();
                return false;
            }

            handle = load.Value;
            _prefabHandles[key] = handle;
            return true;
        }

        private void ReportPrefabLoadFailure(string context, ResourceKey key, string resourceError)
        {
            string message = "[MxFramework VFX] Prefab load failed; "
                + context
                + "; resourceKey=" + key
                + "; resourceError=" + resourceError;
            _diagnostics.Add(message);
            Debug.LogError(message);
        }

        private void ReleaseCachedHandles()
        {
            foreach (ResourceHandle<GameObject> handle in _prefabHandles.Values)
                ReleaseHandle(handle);
        }

        private void ReleaseHandle(ResourceHandle<GameObject> handle)
        {
            if (handle != null && !handle.IsReleased)
                _resources.Release(handle);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(UnityVfxPresenter));
        }

        private static Vector3 ToVector3(VfxVector3 value)
        {
            const float scale = 1f / VfxVector3.Scale;
            return new Vector3(value.RawX * scale, value.RawY * scale, value.RawZ * scale);
        }

        private static Vector3 ToForward(VfxVector3 value)
        {
            Vector3 forward = ToVector3(value);
            return forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector3.forward;
        }

        private static ParticleSystem[] CollectParticleSystems(GameObject view)
        {
            return view != null
                ? view.GetComponentsInChildren<ParticleSystem>(true)
                : Array.Empty<ParticleSystem>();
        }

        private static float[] CollectParticleSimulationSpeeds(ParticleSystem[] systems)
        {
            if (systems == null || systems.Length == 0)
                return Array.Empty<float>();

            var speeds = new float[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                speeds[i] = system != null ? system.main.simulationSpeed : 1f;
            }
            return speeds;
        }

        private static void ApplyPlaybackRate(ActiveInstance active)
        {
            if (active == null)
                return;

            float playbackRate = Mathf.Clamp(active.Snapshot.PlaybackRatePermille / 1000f, 0.05f, 16f);
            ParticleSystem[] systems = active.ParticleSystems;
            float[] baseSpeeds = active.ParticleSimulationSpeeds;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                    continue;
                ParticleSystem.MainModule main = system.main;
                float baseSpeed = i < baseSpeeds.Length ? baseSpeeds[i] : 1f;
                main.simulationSpeed = baseSpeed * playbackRate;
            }
        }

        private static IUnityVfxBackendController[] CollectBackendControllers(GameObject view)
        {
            if (view == null)
                return Array.Empty<IUnityVfxBackendController>();
            MonoBehaviour[] behaviours = view.GetComponentsInChildren<MonoBehaviour>(true);
            var controllers = new List<IUnityVfxBackendController>(behaviours.Length);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IUnityVfxBackendController controller)
                    controllers.Add(controller);
            return controllers.ToArray();
        }

        private static void PlayBackends(VfxInstanceSnapshot instance, ParticleSystem[] particleSystems, IUnityVfxBackendController[] controllers)
        {
            bool suppressParticleFallback = false;
            if (controllers != null)
            {
                for (int i = 0; i < controllers.Length; i++)
                {
                    IUnityVfxBackendController controller = controllers[i];
                    if (controller == null || !controller.IsAvailable)
                        continue;
                    controller.ReinitializeAndPlay(instance);
                    suppressParticleFallback |= controller.SuppressParticleSystemFallbackWhenAvailable;
                }
            }
            if (suppressParticleFallback)
                StopParticles(particleSystems);
            else
            {
                PlayParticles(particleSystems);
                PreSimulateParticles(
                    particleSystems,
                    instance.SourceStartOffsetFrames,
                    instance.PlaybackRatePermille);
            }
        }

        private static void PreSimulateParticles(
            ParticleSystem[] systems,
            int sourceStartOffsetFrames,
            int playbackRatePermille)
        {
            if (systems == null || sourceStartOffsetFrames <= 0)
                return;

            float playbackRate = Mathf.Clamp(playbackRatePermille / 1000f, 0.05f, 16f);
            float wallSeconds = sourceStartOffsetFrames / (60f * playbackRate);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system != null)
                    system.Simulate(wallSeconds, withChildren: false, restart: false, fixedTimeStep: true);
            }
        }

        private static void StopBackends(IUnityVfxBackendController[] controllers)
        {
            if (controllers == null)
                return;
            for (int i = 0; i < controllers.Length; i++)
                controllers[i]?.StopAndClear();
        }

        private static void PlayParticles(ParticleSystem[] systems)
        {
            if (systems == null)
                return;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                    continue;

                system.Clear(true);
                system.Play(true);
            }
        }

        private static void StopParticles(ParticleSystem[] systems)
        {
            if (systems == null)
                return;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system != null)
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static IUnityVfxInstanceReceiver[] CollectReceivers(GameObject view)
        {
            MonoBehaviour[] behaviours = view.GetComponentsInChildren<MonoBehaviour>(true);
            var receivers = new List<IUnityVfxInstanceReceiver>(behaviours.Length);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IUnityVfxInstanceReceiver receiver) receivers.Add(receiver);
            return receivers.ToArray();
        }

        private static void NotifySpawned(IUnityVfxInstanceReceiver[] receivers, VfxInstanceSnapshot instance)
        {
            for (int i = 0; i < receivers.Length; i++) receivers[i].OnUnityVfxInstanceSpawned(instance);
        }

        private static void NotifyReleased(IUnityVfxInstanceReceiver[] receivers, VfxInstanceId instanceId)
        {
            for (int i = 0; i < receivers.Length; i++) receivers[i].OnUnityVfxInstanceReleased(instanceId);
        }
        private static void DestroyObject(Object value)
        {
            if (value == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(value);
            else
                Object.DestroyImmediate(value);
        }

        private sealed class ActiveInstance
        {
            public ActiveInstance(
                GameObject view,
                ParticleSystem[] particleSystems,
                float[] particleSimulationSpeeds,
                IUnityVfxBackendController[] backendControllers,
                UnityVfxLifecycleController lifecycle)
            {
                View = view;
                ParticleSystems = particleSystems ?? Array.Empty<ParticleSystem>();
                ParticleSimulationSpeeds = particleSimulationSpeeds ?? Array.Empty<float>();
                BackendControllers = backendControllers ?? Array.Empty<IUnityVfxBackendController>();
                Lifecycle = lifecycle;
                Receivers = CollectReceivers(view);
                ParticleBudget = new UnityVfxParticleBudgetPlayback(ParticleSystems,
                    Lifecycle != null || BackendControllers.Length != 0 || Receivers.Length != 0
                    || view.GetComponentsInChildren<MonoBehaviour>(true).Length != 0
                    || view.GetComponentsInChildren<Animator>(true).Length != 0
                    || view.GetComponentsInChildren<Animation>(true).Length != 0);
            }

            public VfxInstanceId InstanceId { get; private set; }
            public ResourceKey ResourceKey { get; private set; }
            public VfxAnchor Anchor { get; private set; }
            public VfxInstanceSnapshot Snapshot { get; private set; }
            public float PendingBackendDeltaSeconds { get; set; }
            public IUnityVfxInstanceReceiver[] Receivers { get; }
            public void Bind(VfxInstanceSnapshot instance)
            {
                InstanceId = instance.InstanceId;
                ResourceKey = instance.PrefabResource;
                Anchor = instance.Anchor;
                Snapshot = instance;
                PendingBackendDeltaSeconds = 0f;
            }
            public void ClearBinding()
            {
                InstanceId = default;
                Anchor = default;
                Snapshot = null;
                PendingBackendDeltaSeconds = 0f;
            }
            public GameObject View { get; }
            public ParticleSystem[] ParticleSystems { get; }
            public float[] ParticleSimulationSpeeds { get; }
            public IUnityVfxBackendController[] BackendControllers { get; }
            public UnityVfxLifecycleController Lifecycle { get; }
            public UnityVfxParticleBudgetPlayback ParticleBudget { get; }
        }
    }
}
