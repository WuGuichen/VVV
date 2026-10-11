using System;
using System.Collections.Generic;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using MxFramework.Resources;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Combat.Projectiles.Unity
{
    /// <summary>
    /// Immutable context delivered to Unity projectile presentation receivers during presenter sync.
    /// </summary>
    /// <remarks>NoAlloc after initialization. The value type is passed by readonly reference on high-frequency sync paths.</remarks>
    public readonly struct CombatProjectileUnityPresentationUpdate
    {
        public CombatProjectileUnityPresentationUpdate(
            int projectileId,
            int definitionId,
            int actionId,
            int actionInstanceId,
            int traceId,
            int localFrame,
            int ageFrames)
        {
            ProjectileId = projectileId < 0 ? 0 : projectileId;
            DefinitionId = definitionId < 0 ? 0 : definitionId;
            ActionId = actionId < 0 ? 0 : actionId;
            ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
            TraceId = traceId < 0 ? 0 : traceId;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
            AgeFrames = ageFrames < 0 ? 0 : ageFrames;
        }

        public int ProjectileId { get; }
        public int DefinitionId { get; }
        public int ActionId { get; }
        public int ActionInstanceId { get; }
        public int TraceId { get; }
        public int LocalFrame { get; }
        public int AgeFrames { get; }
    }

    /// <summary>
    /// Receives projectile presentation context from <see cref="CombatProjectileUnityPresenter"/>.
    /// </summary>
    /// <remarks>NoAlloc after initialization. Receiver lookup is cached when the projectile view is created or prewarmed.</remarks>
    public interface ICombatProjectileUnityPresentationReceiver
    {
        /// <summary>
        /// Receives projectile presentation context during presenter sync.
        /// </summary>
        /// <remarks>NoAlloc after initialization. Implementations must not allocate in this high-frequency callback.</remarks>
        void OnCombatProjectilePresentationUpdated(in CombatProjectileUnityPresentationUpdate update);
    }

    [Obsolete("Use CombatProjectileUnityPresentationBindingV2. This adapter maps the legacy prefab to flightBody only.")]
    public sealed class CombatProjectileUnityPresentationBinding : CombatProjectileUnityPresentationBindingV2
    {
        public CombatProjectileUnityPresentationBinding(
            int projectileDefinitionId,
            ResourceKey prefabResource,
            string viewNamePrefix = DefaultViewNamePrefix)
            : base(
                projectileDefinitionId,
                viewNamePrefix,
                CombatProjectileUnityPresentationExitPolicy.Default,
                new CombatProjectileUnityPresentationSlot(
                    CombatProjectileUnityPresentationSlotKind.FlightBody,
                    prefabResource,
                    CombatProjectileUnityPresentationOrientation.ProjectileForward,
                    FixVector3.Zero,
                    0))
        {
            PrefabResource = prefabResource;
        }

        public ResourceKey PrefabResource { get; }
    }

#pragma warning disable CS0618 // Legacy flight adapter is intentionally cached for binary-compatible callers.
    /// <summary>Registry of immutable projectile Presentation Binding v2 records.</summary>
    /// <remarks>Registration is LowFreqAlloc; warmed lookup is NoAlloc.</remarks>
    public sealed class CombatProjectileUnityPresentationRegistry
    {
        private readonly Dictionary<int, CombatProjectileUnityPresentationBindingV2> _bindings =
            new Dictionary<int, CombatProjectileUnityPresentationBindingV2>();
        private readonly Dictionary<int, CombatProjectileUnityPresentationBinding> _legacyFlightAdapters =
            new Dictionary<int, CombatProjectileUnityPresentationBinding>();

        /// <summary>Creates an empty registry.</summary>
        /// <remarks>LowFreqAlloc: creates registry dictionaries.</remarks>
        public CombatProjectileUnityPresentationRegistry()
        {
        }

        /// <summary>Creates a registry from a binding sequence.</summary>
        /// <remarks>LowFreqAlloc: enumerates and registers the supplied bindings.</remarks>
        public CombatProjectileUnityPresentationRegistry(
            IEnumerable<CombatProjectileUnityPresentationBindingV2> bindings)
        {
            if (bindings == null)
                return;

            foreach (CombatProjectileUnityPresentationBindingV2 binding in bindings)
            {
                if (!TryRegister(binding, out string failure))
                    throw new ArgumentException(failure, nameof(bindings));
            }
        }

        /// <summary>Gets the registered v2 binding count.</summary>
        public int Count => _bindings.Count;

        /// <summary>Registers one unique binding.</summary>
        /// <remarks>LowFreqAlloc: dictionary growth and the legacy adapter may allocate during initialization.</remarks>
        public bool TryRegister(CombatProjectileUnityPresentationBindingV2 binding, out string failure)
        {
            failure = string.Empty;
            if (binding == null)
            {
                failure = "Projectile presentation binding cannot be null.";
                return false;
            }

            if (_bindings.ContainsKey(binding.ProjectileDefinitionId))
            {
                failure = "Duplicate projectile presentation binding id: "
                    + binding.ProjectileDefinitionId
                    + ".";
                return false;
            }

            _bindings.Add(binding.ProjectileDefinitionId, binding);
            if (binding.TryGetSlot(CombatProjectileUnityPresentationSlotKind.FlightBody, out CombatProjectileUnityPresentationSlot flight))
                _legacyFlightAdapters.Add(binding.ProjectileDefinitionId, new CombatProjectileUnityPresentationBinding(binding.ProjectileDefinitionId, flight.Resource, binding.ViewNamePrefix));
            return true;
        }

        /// <summary>Looks up a known v2 binding by projectile definition id.</summary>
        /// <remarks>NoAlloc after registry initialization.</remarks>
        public bool TryGetV2(int projectileDefinitionId, out CombatProjectileUnityPresentationBindingV2 binding)
        {
            return _bindings.TryGetValue(projectileDefinitionId, out binding);
        }

        [Obsolete("Use TryGetV2. This adapter exposes only the legacy flightBody view.")]
        public bool TryGet(int projectileDefinitionId, out CombatProjectileUnityPresentationBinding binding)
        {
            return _legacyFlightAdapters.TryGetValue(projectileDefinitionId, out binding);
        }

        /// <summary>Appends every registered v2 binding to caller-owned storage.</summary>
        /// <remarks>LowFreqAlloc when the caller-owned list grows; intended for initialization and prewarm.</remarks>
        public int CopyBindingsV2To(List<CombatProjectileUnityPresentationBindingV2> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int start = output.Count;
            foreach (CombatProjectileUnityPresentationBindingV2 binding in _bindings.Values)
                output.Add(binding);

            return output.Count - start;
        }

        [Obsolete("Use CopyBindingsV2To. This adapter exposes only legacy flightBody views.")]
        public int CopyBindingsTo(List<CombatProjectileUnityPresentationBinding> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            int start = output.Count;
            foreach (CombatProjectileUnityPresentationBinding binding in _legacyFlightAdapters.Values) output.Add(binding);
            return output.Count - start;
        }
    }
#pragma warning restore CS0618

    /// <summary>Owns Unity object creation and release for projectile presentation views.</summary>
    /// <remarks>
    /// <see cref="Instantiate"/> is LowFreqAlloc and is only used during prewarm or pool growth.
    /// <see cref="Release"/> is NoAlloc from the caller's perspective. Implementations must release every
    /// successfully created instance symmetrically, including instances created before a later setup failure.
    /// </remarks>
    public interface ICombatProjectileUnityObjectHost
    {
        /// <summary>Creates one presentation instance in the host-owned Unity scene.</summary>
        /// <remarks>LowFreqAlloc.</remarks>
        GameObject Instantiate(GameObject prefab);

        /// <summary>Releases one instance previously returned by <see cref="Instantiate"/>.</summary>
        /// <remarks>NoAlloc from the caller's perspective.</remarks>
        void Release(GameObject instance);
    }

    internal sealed class CombatProjectileDefaultUnityObjectHost : ICombatProjectileUnityObjectHost
    {
        public static readonly CombatProjectileDefaultUnityObjectHost Instance =
            new CombatProjectileDefaultUnityObjectHost();

        private CombatProjectileDefaultUnityObjectHost()
        {
        }

        public GameObject Instantiate(GameObject prefab) => Object.Instantiate(prefab);

        public void Release(GameObject instance)
        {
            if (instance == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(instance);
            else
                Object.DestroyImmediate(instance);
        }
    }

    /// <summary>Synchronizes view-only projectile prefabs from deterministic projectile state.</summary>
    /// <remarks>
    /// Initialization and pool growth are LowFreqAlloc. Sync is NoAlloc after prewarm and stable capacity;
    /// lifecycle completion polling does not perform component lookup or allocate collections.
    /// </remarks>
    public sealed class CombatProjectileUnityPresenter : IDisposable
    {
        private readonly IResourceManager _resources;
        private readonly CombatProjectileUnityPresentationRegistry _registry;
        private readonly ICombatProjectileUnityObjectHost _objectHost;
        private readonly bool _usePooling;
        private readonly Dictionary<int, ActiveView> _activeByProjectileId = new Dictionary<int, ActiveView>();
        private readonly Dictionary<int, ActiveView> _trailByProjectileId = new Dictionary<int, ActiveView>();
        private readonly Dictionary<ResourceKey, Stack<ActiveView>> _pool =
            new Dictionary<ResourceKey, Stack<ActiveView>>();
        private readonly Dictionary<ResourceKey, ResourceHandle<GameObject>> _prefabHandles =
            new Dictionary<ResourceKey, ResourceHandle<GameObject>>();
        private readonly HashSet<int> _seenProjectileIds = new HashSet<int>();
        private readonly List<int> _removeBuffer = new List<int>();
        private readonly List<ActiveView> _retiringViews = new List<ActiveView>(16);
        private readonly List<CombatProjectileUnityPresentationBindingV2> _bindingBuffer =
            new List<CombatProjectileUnityPresentationBindingV2>();
        private readonly List<string> _diagnostics = new List<string>();
        private bool _isDisposed;

        public CombatProjectileUnityPresenter(
            IResourceManager resources,
            CombatProjectileUnityPresentationRegistry registry,
            bool usePooling = true,
            ICombatProjectileUnityObjectHost objectHost = null)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _usePooling = usePooling;
            _objectHost = objectHost ?? CombatProjectileDefaultUnityObjectHost.Instance;
        }

        public IReadOnlyList<string> Diagnostics => _diagnostics;
        public int ActiveCount => Math.Max(_activeByProjectileId.Count, _trailByProjectileId.Count);

        public void PrewarmBindings(int poolSizePerBinding = 1)
        {
            ThrowIfDisposed();
            _bindingBuffer.Clear();
            _registry.CopyBindingsV2To(_bindingBuffer);
            for (int i = 0; i < _bindingBuffer.Count; i++)
                PrewarmBinding(_bindingBuffer[i], poolSizePerBinding);
            _bindingBuffer.Clear();
        }

        /// <summary>Releases persistent views whose configured retirement lifecycle has completed.</summary>
        /// <remarks>NoAlloc after initialization. Does not consume projectile state or build, update, or exit active views.</remarks>
        public void TickRetiringViews()
        {
            ThrowIfDisposed();
            ReleaseCompletedRetiringViews();
        }

        public void Sync(CombatProjectileRuntimeState state)
        {
            ThrowIfDisposed();
            ReleaseCompletedRetiringViews();
            _seenProjectileIds.Clear();

            IReadOnlyList<CombatProjectileSnapshot> activeProjectiles = state != null
                ? state.ActiveProjectiles
                : null;
            if (activeProjectiles != null)
            {
                for (int i = 0; i < activeProjectiles.Count; i++)
                {
                    CombatProjectileSnapshot projectile = activeProjectiles[i];
                    _seenProjectileIds.Add(projectile.ProjectileId);
                    SyncProjectile(projectile);
                }
            }

            _removeBuffer.Clear();
            foreach (KeyValuePair<int, ActiveView> pair in _activeByProjectileId)
            {
                if (!_seenProjectileIds.Contains(pair.Key))
                    _removeBuffer.Add(pair.Key);
            }
            foreach (KeyValuePair<int, ActiveView> pair in _trailByProjectileId)
            {
                if (!_seenProjectileIds.Contains(pair.Key) && !_removeBuffer.Contains(pair.Key))
                    _removeBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
                Stop(_removeBuffer[i]);

            _removeBuffer.Clear();
            _seenProjectileIds.Clear();
        }

        public void Sync(IReadOnlyList<CombatProjectileActiveView> activeProjectiles)
        {
            ThrowIfDisposed();
            ReleaseCompletedRetiringViews();
            _seenProjectileIds.Clear();

            if (activeProjectiles != null)
            {
                for (int i = 0; i < activeProjectiles.Count; i++)
                {
                    CombatProjectileActiveView projectile = activeProjectiles[i];
                    _seenProjectileIds.Add(projectile.ProjectileId);
                    SyncProjectile(projectile);
                }
            }

            _removeBuffer.Clear();
            foreach (KeyValuePair<int, ActiveView> pair in _activeByProjectileId)
            {
                if (!_seenProjectileIds.Contains(pair.Key))
                    _removeBuffer.Add(pair.Key);
            }
            foreach (KeyValuePair<int, ActiveView> pair in _trailByProjectileId)
            {
                if (!_seenProjectileIds.Contains(pair.Key) && !_removeBuffer.Contains(pair.Key))
                    _removeBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
                Stop(_removeBuffer[i]);

            _removeBuffer.Clear();
            _seenProjectileIds.Clear();
        }

        /// <summary>Builds configured flight and trail views for an accepted spawn.</summary>
        /// <remarks>NoAlloc after prewarm and stable dictionary capacity.</remarks>
        public void BuildPersistent(CombatProjectileSnapshot projectile)
        {
            ThrowIfDisposed();
            BuildPersistentSlot(projectile, CombatProjectileUnityPresentationSlotKind.FlightBody, _activeByProjectileId, rebuild: false);
            BuildPersistentSlot(projectile, CombatProjectileUnityPresentationSlotKind.Trail, _trailByProjectileId, rebuild: false);
        }

        /// <summary>Synchronizes active views after event routing; new aged projectiles use Rebuild and disappeared views use external-removal policy.</summary>
        /// <remarks>NoAlloc after prewarm and stable collection capacity.</remarks>
        public void SyncFromBridge(IReadOnlyList<CombatProjectileActiveView> activeProjectiles)
        {
            ThrowIfDisposed();
            ReleaseCompletedRetiringViews();
            _seenProjectileIds.Clear();
            if (activeProjectiles != null)
            {
                for (int i = 0; i < activeProjectiles.Count; i++)
                {
                    CombatProjectileActiveView projectile = activeProjectiles[i];
                    _seenProjectileIds.Add(projectile.ProjectileId);
                    SyncPersistentSlot(projectile, CombatProjectileUnityPresentationSlotKind.FlightBody, _activeByProjectileId);
                    SyncPersistentSlot(projectile, CombatProjectileUnityPresentationSlotKind.Trail, _trailByProjectileId);
                }
            }
            CollectExternalRemovals(_activeByProjectileId);
            CollectExternalRemovals(_trailByProjectileId);
            for (int i = 0; i < _removeBuffer.Count; i++)
                ExitPersistent(_removeBuffer[i], CombatProjectilePresentationEventKind.ExternalRemoval);
            _removeBuffer.Clear();
            _seenProjectileIds.Clear();
        }

        internal void SyncTrackedFromBridge(CombatProjectileActiveView[] trackedProjectiles, int trackedCount)
        {
            ThrowIfDisposed();
            ReleaseCompletedRetiringViews();
            if (trackedProjectiles == null)
                return;
            int count = Math.Min(trackedCount, trackedProjectiles.Length);
            for (int i = 0; i < count; i++)
            {
                CombatProjectileActiveView projectile = trackedProjectiles[i];
                SyncPersistentSlot(projectile, CombatProjectileUnityPresentationSlotKind.FlightBody, _activeByProjectileId);
                SyncPersistentSlot(projectile, CombatProjectileUnityPresentationSlotKind.Trail, _trailByProjectileId);
            }
        }

        /// <summary>Applies the binding exit policy to both persistent slots.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void ExitPersistent(int projectileId, CombatProjectilePresentationEventKind reason)
        {
            ThrowIfDisposed();
            CombatProjectileUnityPresentationExitMode mode = CombatProjectileUnityPresentationExitMode.Immediate;
            ActiveView known = null;
            if (!_activeByProjectileId.TryGetValue(projectileId, out known))
                _trailByProjectileId.TryGetValue(projectileId, out known);
            if (known != null && _registry.TryGetV2(known.DefinitionId, out CombatProjectileUnityPresentationBindingV2 binding))
            {
                switch (reason)
                {
                    case CombatProjectilePresentationEventKind.DirectImpact: mode = binding.ExitPolicy.AcceptedImpact; break;
                    case CombatProjectilePresentationEventKind.BlockedImpact: mode = binding.ExitPolicy.BlockedImpact; break;
                    case CombatProjectilePresentationEventKind.Expire: mode = binding.ExitPolicy.Expiration; break;
                    case CombatProjectilePresentationEventKind.ExternalRemoval: mode = binding.ExitPolicy.ExternalRemoval; break;
                    default: mode = binding.ExitPolicy.RuntimeReset; break;
                }
            }
            StopSlot(projectileId, _activeByProjectileId, mode);
            StopSlot(projectileId, _trailByProjectileId, mode);
        }

        /// <summary>Immediately clears every persistent view for reset/shutdown without one-shots.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void ResetPersistent()
        {
            ThrowIfDisposed();
            ClearDictionaryImmediate(_activeByProjectileId);
            ClearDictionaryImmediate(_trailByProjectileId);
            ReleaseAllRetiring(destroy: !_usePooling);
        }

        public void Clear()
        {
            ThrowIfDisposed();
            foreach (ActiveView active in _activeByProjectileId.Values)
                ReleaseActive(active, destroy: !_usePooling);

            _activeByProjectileId.Clear();
            foreach (ActiveView active in _trailByProjectileId.Values)
                ReleaseActive(active, destroy: !_usePooling);
            _trailByProjectileId.Clear();
            ReleaseAllRetiring(destroy: !_usePooling);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            foreach (ActiveView active in _activeByProjectileId.Values)
                ReleaseActive(active, destroy: true);

            _activeByProjectileId.Clear();
            foreach (ActiveView active in _trailByProjectileId.Values)
                ReleaseActive(active, destroy: true);
            _trailByProjectileId.Clear();
            ReleaseAllRetiring(destroy: true);
            foreach (Stack<ActiveView> stack in _pool.Values)
            {
                while (stack.Count > 0)
                    ReleasePooled(stack.Pop());
            }

            _pool.Clear();
            ReleaseCachedHandles();
            _prefabHandles.Clear();
            _removeBuffer.Clear();
            _seenProjectileIds.Clear();
            _isDisposed = true;
        }

        private void SyncProjectile(CombatProjectileSnapshot projectile)
        {
            SyncPersistentSlot(projectile, CombatProjectileUnityPresentationSlotKind.FlightBody, _activeByProjectileId);
            SyncPersistentSlot(projectile, CombatProjectileUnityPresentationSlotKind.Trail, _trailByProjectileId);
        }

        private void SyncProjectile(CombatProjectileActiveView projectile)
        {
            SyncPersistentSlot(projectile, CombatProjectileUnityPresentationSlotKind.FlightBody, _activeByProjectileId);
            SyncPersistentSlot(projectile, CombatProjectileUnityPresentationSlotKind.Trail, _trailByProjectileId);
        }

        private void BuildPersistentSlot(CombatProjectileSnapshot projectile, CombatProjectileUnityPresentationSlotKind kind,
            Dictionary<int, ActiveView> storage, bool rebuild)
        {
            if (storage.ContainsKey(projectile.ProjectileId))
                return;
            if (!_registry.TryGetV2(projectile.DefinitionId, out CombatProjectileUnityPresentationBindingV2 binding))
            {
                AddDiagnosticOnce("Projectile presentation binding is missing for definition " + projectile.DefinitionId + ".");
                return;
            }
            if (!binding.TryGetSlot(kind, out CombatProjectileUnityPresentationSlot slot))
                return;
            ActiveView active = CreateActiveView(projectile.ProjectileId, projectile.DefinitionId, binding, slot, rebuild);
            if (active == null)
                return;
            storage.Add(projectile.ProjectileId, active);
            ApplyTransform(active.View, projectile, active.LocalOffset);
        }

        private void SyncPersistentSlot(CombatProjectileSnapshot projectile, CombatProjectileUnityPresentationSlotKind kind,
            Dictionary<int, ActiveView> storage)
        {
            BuildPersistentSlot(projectile, kind, storage, projectile.AgeFrames > 0);
            if (!storage.TryGetValue(projectile.ProjectileId, out ActiveView active))
                return;
            ApplyTransform(active.View, projectile, active.LocalOffset);
            NotifyReceivers(active.Receivers, CreateUpdate(projectile));
        }

        private void SyncPersistentSlot(CombatProjectileActiveView projectile, CombatProjectileUnityPresentationSlotKind kind,
            Dictionary<int, ActiveView> storage)
        {
            if (!storage.TryGetValue(projectile.ProjectileId, out ActiveView active))
            {
                if (!_registry.TryGetV2(projectile.DefinitionId, out CombatProjectileUnityPresentationBindingV2 binding)
                    || !binding.TryGetSlot(kind, out CombatProjectileUnityPresentationSlot slot))
                    return;
                active = CreateActiveView(projectile.ProjectileId, projectile.DefinitionId, binding, slot, projectile.AgeFrames > 0);
                if (active == null)
                    return;
                storage.Add(projectile.ProjectileId, active);
            }
            ApplyTransform(active.View, projectile, active.LocalOffset);
            NotifyReceivers(active.Receivers, CreateUpdate(projectile));
        }

        private void CollectExternalRemovals(Dictionary<int, ActiveView> storage)
        {
            foreach (KeyValuePair<int, ActiveView> pair in storage)
            {
                if (!_seenProjectileIds.Contains(pair.Key) && !_removeBuffer.Contains(pair.Key))
                    _removeBuffer.Add(pair.Key);
            }
        }

        private static CombatProjectileUnityPresentationUpdate CreateUpdate(CombatProjectileSnapshot projectile) =>
            new CombatProjectileUnityPresentationUpdate(projectile.ProjectileId, projectile.DefinitionId, projectile.ActionId,
                projectile.ActionInstanceId, projectile.TraceId, projectile.LocalFrame, projectile.AgeFrames);

        private static CombatProjectileUnityPresentationUpdate CreateUpdate(CombatProjectileActiveView projectile) =>
            new CombatProjectileUnityPresentationUpdate(projectile.ProjectileId, projectile.DefinitionId, projectile.ActionId,
                projectile.ActionInstanceId, projectile.TraceId, projectile.LocalFrame, projectile.AgeFrames);

        private ActiveView CreateActiveView(
            int projectileId,
            int definitionId,
            CombatProjectileUnityPresentationBindingV2 binding,
            CombatProjectileUnityPresentationSlot slot,
            bool rebuild)
        {
            ActiveView pooled = TryTakeFromPool(slot.Resource);
            GameObject view = pooled != null ? pooled.View : null;
            ParticleSystem[] particleSystems = pooled != null ? pooled.ParticleSystems : null;
            ICombatProjectileUnityPresentationReceiver[] receivers =
                pooled != null ? pooled.Receivers : null;
            UnityVfxLifecycleController lifecycle = pooled != null ? pooled.Lifecycle : null;
            if (view == null)
            {
                if (!TryGetPrefabHandle(slot.Resource, out ResourceHandle<GameObject> handle, out string error))
                {
                    AddDiagnosticOnce("Projectile presentation prefab load failed for definition "
                        + definitionId
                        + ": "
                        + error);
                    return null;
                }

                view = _objectHost.Instantiate(handle.Value);
                try
                {
                    particleSystems = CollectParticleSystems(view);
                    receivers = CollectPresentationReceivers(view);
                    lifecycle = view.GetComponent<UnityVfxLifecycleController>();
                }
                catch
                {
                    _objectHost.Release(view);
                    throw;
                }
                if (_usePooling)
                {
                    AddDiagnosticOnce("Projectile presentation pool exhausted for definition "
                        + definitionId
                        + ".");
                }
            }

            view.name = binding.ViewNamePrefix + "_" + definitionId + "_" + projectileId
                + (slot.Kind == CombatProjectileUnityPresentationSlotKind.Trail ? "_Trail" : string.Empty);
            view.SetActive(true);
            if (lifecycle != null)
            {
                var instanceId = new VfxInstanceId(projectileId);
                if (rebuild)
                    lifecycle.PlayRebuild(instanceId);
                else
                    lifecycle.PlayBuild(instanceId);
            }
            else
            {
                PlayParticles(particleSystems);
            }

            ActiveView active = pooled ?? new ActiveView(view, particleSystems, receivers, lifecycle);
            active.Bind(projectileId, definitionId, slot.Resource, slot.LocalOffset);
            return active;
        }
        private void PrewarmBinding(
            CombatProjectileUnityPresentationBindingV2 binding,
            int poolSize)
        {
            if (binding == null)
                return;

            PrewarmSlot(binding, CombatProjectileUnityPresentationSlotKind.FlightBody, poolSize);
            PrewarmSlot(binding, CombatProjectileUnityPresentationSlotKind.Trail, poolSize);
        }

        private void PrewarmSlot(CombatProjectileUnityPresentationBindingV2 binding, CombatProjectileUnityPresentationSlotKind kind, int poolSize)
        {
            if (!binding.TryGetSlot(kind, out CombatProjectileUnityPresentationSlot slot))
                return;

            if (!TryGetPrefabHandle(slot.Resource, out ResourceHandle<GameObject> handle, out string error))
            {
                AddDiagnosticOnce("Projectile presentation prefab load failed for definition "
                    + binding.ProjectileDefinitionId
                    + ": "
                    + error);
                return;
            }

            if (!_usePooling || poolSize <= 0)
                return;

            if (!_pool.TryGetValue(slot.Resource, out Stack<ActiveView> stack))
            {
                stack = new Stack<ActiveView>(Math.Max(1, poolSize));
                _pool.Add(slot.Resource, stack);
            }

            while (stack.Count < poolSize)
            {
                GameObject view = _objectHost.Instantiate(handle.Value);
                try
                {
                    view.SetActive(false);
                    stack.Push(new ActiveView(
                        view,
                        CollectParticleSystems(view),
                        CollectPresentationReceivers(view),
                        view.GetComponent<UnityVfxLifecycleController>()));
                }
                catch
                {
                    _objectHost.Release(view);
                    throw;
                }
            }
        }

        private void Stop(int projectileId)
        {
            StopSlot(projectileId, _activeByProjectileId, CombatProjectileUnityPresentationExitMode.Complete);
            StopSlot(projectileId, _trailByProjectileId, CombatProjectileUnityPresentationExitMode.Complete);
        }

        private void StopSlot(int projectileId, Dictionary<int, ActiveView> storage, CombatProjectileUnityPresentationExitMode mode)
        {
            if (!storage.TryGetValue(projectileId, out ActiveView active))
                return;
            storage.Remove(projectileId);
            if (mode == CombatProjectileUnityPresentationExitMode.Immediate || active.Lifecycle == null)
            {
                ReleaseActive(active, destroy: !_usePooling);
                return;
            }
            active.Lifecycle.PlayExit(mode == CombatProjectileUnityPresentationExitMode.Dissipate
                ? VfxStopMode.Dissipate
                : VfxStopMode.Complete);
            _retiringViews.Add(active);
        }

        private void ClearDictionaryImmediate(Dictionary<int, ActiveView> storage)
        {
            foreach (ActiveView active in storage.Values)
                ReleaseActive(active, destroy: !_usePooling);
            storage.Clear();
        }

        private void ReleaseActive(ActiveView active, bool destroy)
        {
            if (active.View == null)
            {
                return;
            }

            if (active.Lifecycle != null)
                active.Lifecycle.ForceClear();
            else
                StopParticles(active.ParticleSystems);
            if (destroy)
            {
                _objectHost.Release(active.View);
                return;
            }

            active.View.SetActive(false);
            if (!_pool.TryGetValue(active.ResourceKey, out Stack<ActiveView> stack))
            {
                stack = new Stack<ActiveView>();
                _pool.Add(active.ResourceKey, stack);
            }

            stack.Push(active);
        }

        private void ReleaseCompletedRetiringViews()
        {
            for (int i = _retiringViews.Count - 1; i >= 0; i--)
            {
                ActiveView active = _retiringViews[i];
                if (active.View != null
                    && active.Lifecycle != null
                    && !active.Lifecycle.IsLifecycleComplete)
                {
                    continue;
                }

                _retiringViews.RemoveAt(i);
                ReleaseActive(active, destroy: !_usePooling);
            }
        }

        private void ReleaseAllRetiring(bool destroy)
        {
            for (int i = 0; i < _retiringViews.Count; i++)
                ReleaseActive(_retiringViews[i], destroy);

            _retiringViews.Clear();
        }

        private ActiveView TryTakeFromPool(ResourceKey key)
        {
            if (!_usePooling || !_pool.TryGetValue(key, out Stack<ActiveView> stack))
                return null;

            while (stack.Count > 0)
            {
                ActiveView pooled = stack.Pop();
                if (pooled.View != null)
                    return pooled;
            }

            return null;
        }

        private void ReleasePooled(ActiveView pooled)
        {
            if (pooled == null)
                return;

            _objectHost.Release(pooled.View);
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

        private void AddDiagnosticOnce(string message)
        {
            string safeMessage = string.IsNullOrWhiteSpace(message) ? "Unknown projectile presentation error." : message;
            if (_diagnostics.Contains(safeMessage))
                return;

            _diagnostics.Add(safeMessage);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CombatProjectileUnityPresenter));
        }

        private static void ApplyTransform(GameObject view, CombatProjectileSnapshot projectile, FixVector3 localOffset)
        {
            if (view == null)
                return;
            ApplyResolvedPose(view, CombatProjectileUnityPresentationPoseResolver.Resolve(
                CombatProjectileUnityPresentationOrientation.ProjectileForward,
                localOffset,
                projectile.Position,
                projectile.Direction,
                FixVector3.Zero));
        }

        private static void ApplyTransform(GameObject view, CombatProjectileActiveView projectile, FixVector3 localOffset)
        {
            if (view == null)
                return;

            ApplyResolvedPose(view, CombatProjectileUnityPresentationPoseResolver.Resolve(
                CombatProjectileUnityPresentationOrientation.ProjectileForward,
                localOffset,
                projectile.Position,
                projectile.Direction,
                FixVector3.Zero));
        }

        private static void ApplyResolvedPose(GameObject view, CombatProjectileUnityPresentationPose pose)
        {
            Vector3 forward = ToVector3(pose.Forward);
            if (forward.sqrMagnitude > 0.000001f)
                view.transform.forward = forward.normalized;
            view.transform.position = ToVector3(pose.Position);
        }

        private static Vector3 ToVector3(FixVector3 value)
        {
            return new Vector3(ToFloat(value.X), ToFloat(value.Y), ToFloat(value.Z));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)value.RawValue / Fix64.Scale;
        }

        private static ParticleSystem[] CollectParticleSystems(GameObject view)
        {
            return view != null
                ? view.GetComponentsInChildren<ParticleSystem>(true)
                : Array.Empty<ParticleSystem>();
        }

        private static ICombatProjectileUnityPresentationReceiver[] CollectPresentationReceivers(GameObject view)
        {
            if (view == null)
                return Array.Empty<ICombatProjectileUnityPresentationReceiver>();

            MonoBehaviour[] behaviours = view.GetComponentsInChildren<MonoBehaviour>(true);
            int count = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatProjectileUnityPresentationReceiver)
                    count++;
            }

            if (count == 0)
                return Array.Empty<ICombatProjectileUnityPresentationReceiver>();

            var receivers = new ICombatProjectileUnityPresentationReceiver[count];
            int receiverIndex = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatProjectileUnityPresentationReceiver receiver)
                    receivers[receiverIndex++] = receiver;
            }

            return receivers;
        }

        private static void NotifyReceivers(
            ICombatProjectileUnityPresentationReceiver[] receivers,
            in CombatProjectileUnityPresentationUpdate update)
        {
            if (receivers == null)
                return;

            for (int i = 0; i < receivers.Length; i++)
            {
                ICombatProjectileUnityPresentationReceiver receiver = receivers[i];
                if (receiver != null)
                    receiver.OnCombatProjectilePresentationUpdated(in update);
            }
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

        private sealed class ActiveView
        {
            public ActiveView(
                GameObject view,
                ParticleSystem[] particleSystems,
                ICombatProjectileUnityPresentationReceiver[] receivers,
                UnityVfxLifecycleController lifecycle)
            {
                View = view;
                ParticleSystems = particleSystems ?? Array.Empty<ParticleSystem>();
                Receivers = receivers ?? Array.Empty<ICombatProjectileUnityPresentationReceiver>();
                Lifecycle = lifecycle;
            }

            public int ProjectileId { get; private set; }
            public int DefinitionId { get; private set; }
            public ResourceKey ResourceKey { get; private set; }
            public FixVector3 LocalOffset { get; private set; }
            public void Bind(int projectileId, int definitionId, ResourceKey resourceKey, FixVector3 localOffset)
            {
                ProjectileId = projectileId;
                DefinitionId = definitionId;
                ResourceKey = resourceKey;
                LocalOffset = localOffset;
            }
            public GameObject View { get; }
            public ParticleSystem[] ParticleSystems { get; }
            public ICombatProjectileUnityPresentationReceiver[] Receivers { get; }
            public UnityVfxLifecycleController Lifecycle { get; }
        }
    }
}
