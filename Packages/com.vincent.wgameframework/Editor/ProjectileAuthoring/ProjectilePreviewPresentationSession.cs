using System;
using System.Collections.Generic;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Core.Math;
using MxFramework.Editor.Authoring;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Vfx;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.ProjectileAuthoring
{
    /// <summary>Owns the isolated M5 projectile presentation route used by the authoring viewport.</summary>
    /// <remarks>Construction/rebinding is LowFreqAlloc. Warm sequential routing is NoAlloc after pools stabilize.</remarks>
    internal sealed class ProjectilePreviewPresentationSession : IDisposable
    {
        private readonly AuthoringPreviewRenderScene _scene;
        private readonly ProjectilePreviewUnityObjectHost _host;
        private readonly CombatProjectileUnityPresentationRegistry _registry;
        private readonly VfxDefinitionRegistry _definitions;
        private readonly ProjectileAuthoringVfxSelectionBridge _vfxSelection = new ProjectileAuthoringVfxSelectionBridge();
        private readonly CombatProjectileUnityPresenter _presenter;
        private readonly Dictionary<ResourceKey, GameObject> _prefabs;
        private readonly CombatProjectileUnityPresentationSlot[] _slots = new CombatProjectileUnityPresentationSlot[7];
        private readonly bool[] _hasSlots = new bool[7];
        private readonly Dictionary<long, GameObject> _oneShotObjects = new Dictionary<long, GameObject>();
        private readonly List<VfxRuntimeEvent> _events = new List<VfxRuntimeEvent>(8);
        private VfxRuntimeModule _vfx;
        private RuntimeCommandBuffer _vfxCommands;
        private CombatProjectileVfxBridge _bridge;
        private ProjectilePreviewSlotAudition _audition;
        private bool _disposed;

        internal ProjectilePreviewPresentationSession(
            AuthoringPreviewRenderScene scene,
            ProjectilePresentationBindingListItem binding)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _host = new ProjectilePreviewUnityObjectHost(scene);
            _definitions = new VfxDefinitionRegistry();
            _prefabs = new Dictionary<ResourceKey, GameObject>();
            try
            {
                var slots = new List<CombatProjectileUnityPresentationSlot>(7);
                var catalogEntries = new List<ResourceCatalogEntry>(7);
                var provider = new MemoryResourceProvider();
                int missing = 0;
                if (binding?.detail != null)
                {
                    AddSlot("spawn", binding.detail.slots?.spawn, CombatProjectileUnityPresentationSlotKind.Spawn, slots, catalogEntries, provider, ref missing);
                    AddSlot("flightBody", binding.detail.slots?.flightBody, CombatProjectileUnityPresentationSlotKind.FlightBody, slots, catalogEntries, provider, ref missing);
                    AddSlot("trail", binding.detail.slots?.trail, CombatProjectileUnityPresentationSlotKind.Trail, slots, catalogEntries, provider, ref missing);
                    AddSlot("directImpact", binding.detail.slots?.directImpact, CombatProjectileUnityPresentationSlotKind.DirectImpact, slots, catalogEntries, provider, ref missing);
                    AddSlot("areaImpact", binding.detail.slots?.areaImpact, CombatProjectileUnityPresentationSlotKind.AreaImpact, slots, catalogEntries, provider, ref missing);
                    AddSlot("blockedImpact", binding.detail.slots?.blockedImpact, CombatProjectileUnityPresentationSlotKind.BlockedImpact, slots, catalogEntries, provider, ref missing);
                    AddSlot("expire", binding.detail.slots?.expire, CombatProjectileUnityPresentationSlotKind.Expire, slots, catalogEntries, provider, ref missing);
                    _registry = new CombatProjectileUnityPresentationRegistry(new[]
                    {
                        new CombatProjectileUnityPresentationBindingV2(
                            binding.projectileDefinitionId,
                            binding.detail.viewNamePrefix,
                            CreateExitPolicy(binding.detail.exitPolicy),
                            slots.ToArray())
                    });
                }
                else
                {
                    _registry = new CombatProjectileUnityPresentationRegistry();
                    missing++;
                }

                MissingResourceCount = missing;
                var resources = (ResourceManager)new ResourceManager().RegisterProvider(provider);
                resources.AddCatalog(new ResourceCatalog("projectile.preview.presentation", string.Empty, catalogEntries));
                _presenter = new CombatProjectileUnityPresenter(resources, _registry, true, _host);
                _presenter.PrewarmBindings(1);
                RecreateRoute();
                _audition = new ProjectilePreviewSlotAudition(_scene, _host);
            }
            catch
            {
                _presenter?.Dispose();
                _host.Dispose();
                throw;
            }
        }

        internal int MissingResourceCount { get; }
        internal int OwnedObjectCount => _host.OwnedCount;
        internal int ActiveVfxCount => _vfx?.ActiveCount ?? 0;
        internal bool TryGetVfxInstance(long instanceId, out VfxInstanceSnapshot snapshot)
            => _vfx.TryGetInstance(new VfxInstanceId(instanceId), out snapshot);
        internal CombatProjectileVfxBridgeDiagnostics Diagnostics => _bridge.GetDiagnostics();
        internal bool IsSlotAuditionActive => _audition?.IsActive == true;
        internal bool IsSlotAuditionPlaying => _audition?.IsPlaying == true;

        internal bool TryAuditionSlot(CombatProjectileUnityPresentationSlotKind kind)
        {
            int index = (int)kind;
            if (_disposed || (uint)index >= (uint)_hasSlots.Length || !_hasSlots[index])
                return false;
            CombatProjectileUnityPresentationSlot slot = _slots[index];
            if (!_prefabs.TryGetValue(slot.Resource, out GameObject prefab) || prefab == null)
                return false;
            ClearOneShots();
            _bridge?.Reset(RuntimeFrame.Zero);
            _vfx?.ResetTransientState(RuntimeFrame.Zero);
            return _audition.Play(prefab, in slot);
        }

        internal void ToggleSlotAudition() => _audition?.Toggle();
        internal void RestartSlotAudition() => _audition?.Restart();
        internal void SetSlotAuditionSpeed(float speed) => _audition?.SetSpeed(speed);
        internal void StopSlotAudition() => _audition?.Stop();

        internal void RouteFrame(ProjectilePreviewSandbox sandbox, int frame, bool allowOneShots)
        {
            if (_disposed || sandbox == null) return;
            _audition?.Stop();
            if (!allowOneShots)
            {
                ClearOneShots();
                _vfx.ResetTransientState(new RuntimeFrame(frame));
            }
            sandbox.RoutePresentationFrame(frame, allowOneShots, _bridge, TickAndPresentOneShots);
        }

        internal void Restart(ProjectilePreviewSandbox sandbox)
        {
            if (_disposed || sandbox == null) return;
            _audition?.Stop();
            ClearOneShots();
            _vfx.ResetTransientState(RuntimeFrame.Zero);
            sandbox.ResetPresentationReplay(_bridge);
            sandbox.RoutePresentationFrame(0, true, _bridge, TickAndPresentOneShots);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _audition?.Dispose();
            _audition = null;
            ClearOneShots();
            _bridge?.Reset(RuntimeFrame.Zero);
            _bridge = null;
            _presenter.Dispose();
            _host.Dispose();
            _events.Clear();
        }

        private void RecreateRoute()
        {
            if (_bridge != null)
                _bridge.Reset(RuntimeFrame.Zero);
            ClearOneShots();
            _vfxCommands = new RuntimeCommandBuffer();
            _vfx = new VfxRuntimeModule(_definitions, _vfxCommands);
            _bridge = new CombatProjectileVfxBridge(_registry, _vfx, _presenter, trackingCapacity: 8, correlationCapacity: 64);
        }

        private void TickAndPresentOneShots(RuntimeFrame frame)
        {
            _vfx.Tick(new RuntimeTickContext(frame.Value, 1d / 60d, frame.Value / 60d, RuntimeTickStage.PostSimulation));
            _events.Clear();
            _vfx.DrainEvents(frame, _events);
            for (int i = 0; i < _events.Count; i++)
            {
                VfxRuntimeEvent evt = _events[i];
                if (evt.Type == VfxRuntimeEventType.Spawned)
                {
                    if (!_definitions.TryGet(evt.DefinitionId, out VfxDefinition definition)
                        || !_prefabs.TryGetValue(definition.PrefabResource, out GameObject prefab)
                        || prefab == null)
                        continue;
                    GameObject instance = _host.Instantiate(prefab);
                    ApplyPose(instance.transform, evt.Anchor.Pose);
                    if (_oneShotObjects.TryGetValue(evt.InstanceId.Value, out GameObject existing))
                        _host.Release(existing);
                    _oneShotObjects[evt.InstanceId.Value] = instance;
                }
                else if ((evt.Type == VfxRuntimeEventType.Stopped || evt.Type == VfxRuntimeEventType.Expired)
                    && _oneShotObjects.TryGetValue(evt.InstanceId.Value, out GameObject instance))
                {
                    _oneShotObjects.Remove(evt.InstanceId.Value);
                    _host.Release(instance);
                }
            }
            _events.Clear();
        }

        private void AddSlot(
            string slotName,
            ProjectilePresentationSlotView view,
            CombatProjectileUnityPresentationSlotKind kind,
            List<CombatProjectileUnityPresentationSlot> slots,
            List<ResourceCatalogEntry> entries,
            MemoryResourceProvider provider,
            ref int missing)
        {
            if (view?.resource == null || string.IsNullOrWhiteSpace(view.resource.id))
                return;
            if (!TryCreateSlot(view, kind, out CombatProjectileUnityPresentationSlot formalSlot))
            {
                missing++;
                return;
            }
            slots.Add(formalSlot);
            _slots[(int)kind] = formalSlot;
            _hasSlots[(int)kind] = true;
            ResourceKey key = formalSlot.Resource;

            if (!_prefabs.ContainsKey(key))
            {
                ProjectileAuthoringVfxTransientView resolved = _vfxSelection.Resolve(key.ToString());
                GameObject prefab = string.IsNullOrWhiteSpace(resolved.ActualPrefabPath)
                    ? null : AssetDatabase.LoadAssetAtPath<GameObject>(resolved.ActualPrefabPath);
                if (prefab == null)
                {
                    missing++;
                }
                else
                {
                    string address = "slot/" + slotName;
                    _prefabs.Add(key, prefab);
                    provider.Register(address, prefab);
                    entries.Add(new ResourceCatalogEntry(key.Id, key.TypeId, MemoryResourceProvider.Id, address,
                        key.Variant, key.PackageId));
                }
            }
            if (view.vfxDefinitionId > 0 && !_definitions.TryGet(new VfxDefinitionId(view.vfxDefinitionId), out _))
                _definitions.TryRegister(new VfxDefinition(new VfxDefinitionId(view.vfxDefinitionId), slotName,
                    key, Math.Max(1, view.lifetimeFramesOverride)), out _);
        }

        internal static bool TryCreateSlot(ProjectilePresentationSlotView view,
            CombatProjectileUnityPresentationSlotKind kind,
            out CombatProjectileUnityPresentationSlot slot)
        {
            slot = default;
            if (view?.resource == null || string.IsNullOrWhiteSpace(view.resource.id))
                return false;
            var key = new ResourceKey(view.resource.id,
                string.IsNullOrWhiteSpace(view.resource.typeId) ? ResourceTypeIds.GameObject : view.resource.typeId,
                view.resource.variant,
                view.resource.packageId);
            if (!key.IsValid)
                return false;
            if (!Enum.TryParse(view.orientation, out CombatProjectileUnityPresentationOrientation orientation))
                orientation = DefaultOrientation(kind);
            try
            {
                slot = new CombatProjectileUnityPresentationSlot(kind, key, orientation, ToFix(view.localOffset),
                    Math.Max(0, view.lifetimeFramesOverride), view.vfxDefinitionId);
                return true;
            }
            catch
            {
                slot = default;
                return false;
            }
        }

        private void ClearOneShots()
        {
            foreach (GameObject instance in _oneShotObjects.Values)
                _host.Release(instance);
            _oneShotObjects.Clear();
        }

        private static void ApplyPose(Transform transform, VfxPose pose)
        {
            Vector3 position = ToVector3(pose.Position);
            Vector3 forward = ToVector3(pose.Forward);
            transform.SetPositionAndRotation(position,
                forward.sqrMagnitude > .000001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity);
        }

        private static Vector3 ToVector3(VfxVector3 value) => new Vector3(
            (float)value.RawX / VfxVector3.Scale,
            (float)value.RawY / VfxVector3.Scale,
            (float)value.RawZ / VfxVector3.Scale);

        private static FixVector3 ToFix(ProjectileVector3View value) => value == null ? FixVector3.Zero : new FixVector3(
            Fix64.FromRaw((long)Math.Round(value.x * Fix64.Scale)),
            Fix64.FromRaw((long)Math.Round(value.y * Fix64.Scale)),
            Fix64.FromRaw((long)Math.Round(value.z * Fix64.Scale)));

        private static CombatProjectileUnityPresentationOrientation DefaultOrientation(CombatProjectileUnityPresentationSlotKind kind)
        {
            if (kind == CombatProjectileUnityPresentationSlotKind.DirectImpact || kind == CombatProjectileUnityPresentationSlotKind.BlockedImpact)
                return CombatProjectileUnityPresentationOrientation.SurfaceNormal;
            if (kind == CombatProjectileUnityPresentationSlotKind.AreaImpact)
                return CombatProjectileUnityPresentationOrientation.ImpactNormalOrWorldUp;
            return CombatProjectileUnityPresentationOrientation.ProjectileForward;
        }

        internal static CombatProjectileUnityPresentationExitPolicy CreateExitPolicy(ProjectilePresentationExitPolicyView view) =>
            new CombatProjectileUnityPresentationExitPolicy(
                ParseExit(view?.acceptedImpact, CombatProjectileUnityPresentationExitMode.Complete),
                ParseExit(view?.blockedImpact, CombatProjectileUnityPresentationExitMode.Dissipate),
                ParseExit(view?.expiration, CombatProjectileUnityPresentationExitMode.Complete),
                ParseExit(view?.externalRemoval, CombatProjectileUnityPresentationExitMode.Dissipate),
                CombatProjectileUnityPresentationExitMode.Immediate);

        private static CombatProjectileUnityPresentationExitMode ParseExit(string value, CombatProjectileUnityPresentationExitMode fallback) =>
            Enum.TryParse(value, out CombatProjectileUnityPresentationExitMode parsed)
                && parsed != CombatProjectileUnityPresentationExitMode.Immediate ? parsed : fallback;
    }
}
