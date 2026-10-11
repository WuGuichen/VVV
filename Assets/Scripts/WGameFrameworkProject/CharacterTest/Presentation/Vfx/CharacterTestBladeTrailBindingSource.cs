using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>LowFreqAlloc per spawn. Connects presentation to existing equipment/trace authority only.</summary>
    internal sealed class CharacterTestBladeTrailBindingSource : IUnityVfxBackendBindingSource
    {
        private readonly CharacterTestCharacterRuntimeModule _runtime;
        private readonly UnityBladeTrailBudget _budget = new UnityBladeTrailBudget(8);
        private readonly Dictionary<(int EntityId, int SlotId), StyleOverride> _styleOverrides =
            new Dictionary<(int, int), StyleOverride>();
        private readonly List<(int EntityId, int SlotId)> _staleOverrideKeys = new List<(int, int)>(8);
        private readonly List<StyleBinding> _styleBindings = new List<StyleBinding>(8);
        public CharacterTestBladeTrailBindingSource(CharacterTestCharacterRuntimeModule runtime) { _runtime = runtime; }

        internal bool TrySetStyleOverride(int entityId, int equipmentSlotId, WeaponBladeTrailStyle style)
        {
            if (equipmentSlotId <= 0 || (int)style < 0 || (int)style > 4) return false;
            CharacterTestCharacterEntity entity = FindEntity(entityId);
            if (entity == null) return false;
            PruneBindings();
            _styleOverrides[(entityId, equipmentSlotId)] = new StyleOverride(entity, (int)style);
            ApplyActiveStyles(entityId, equipmentSlotId);
            return true;
        }

        internal bool ClearStyleOverride(int entityId, int equipmentSlotId)
        {
            PruneBindings();
            bool removed = _styleOverrides.Remove((entityId, equipmentSlotId));
            if (removed) ApplyActiveStyles(entityId, equipmentSlotId);
            return removed;
        }

        private CharacterTestCharacterEntity FindEntity(int entityId)
        {
            if (_runtime == null) return null;
            for (int i = 0; i < _runtime.Entities.Count; i++)
                if (_runtime.Entities[i].Id.Value == entityId) return _runtime.Entities[i];
            return null;
        }

        private int ResolveStyle(CharacterTestCharacterEntity entity, int slotId, int defaultStyleId)
            => _styleOverrides.TryGetValue((entity.Id.Value, slotId), out StyleOverride value)
                && ReferenceEquals(value.Entity, entity) ? value.StyleId : defaultStyleId;

        private void ApplyActiveStyles(int entityId, int slotId)
        {
            foreach (StyleBinding binding in _styleBindings)
                if (binding.Entity.Id.Value == entityId && binding.SlotId == slotId)
                    binding.Backend.ConfigureStyle(ResolveStyle(binding.Entity, slotId, binding.DefaultStyleId));
        }

        private void PruneBindings()
        {
            for (int i = _styleBindings.Count - 1; i >= 0; i--)
                if (_styleBindings[i].Backend == null || _styleBindings[i].Source.IsDisposed)
                    _styleBindings.RemoveAt(i);
            _staleOverrideKeys.Clear();
            foreach (var pair in _styleOverrides)
                if (!ReferenceEquals(FindEntity(pair.Key.EntityId), pair.Value.Entity))
                    _staleOverrideKeys.Add(pair.Key);
            foreach (var key in _staleOverrideKeys) _styleOverrides.Remove(key);
        }

        private readonly struct StyleOverride
        {
            internal StyleOverride(CharacterTestCharacterEntity entity, int styleId)
            { Entity = entity; StyleId = styleId; }
            internal CharacterTestCharacterEntity Entity { get; }
            internal int StyleId { get; }
        }

        private readonly struct StyleBinding
        {
            internal StyleBinding(UnityBladeTrailBackend backend, Source source,
                CharacterTestCharacterEntity entity, int slotId, int defaultStyleId)
            { Backend = backend; Source = source; Entity = entity; SlotId = slotId; DefaultStyleId = defaultStyleId; }
            internal UnityBladeTrailBackend Backend { get; }
            internal Source Source { get; }
            internal CharacterTestCharacterEntity Entity { get; }
            internal int SlotId { get; }
            internal int DefaultStyleId { get; }
        }
        public void BindBackend(GameObject view, VfxInstanceSnapshot instance)
        {
            if (view == null) return;
            UnityBladeTrailBackend backend = view.GetComponent<UnityBladeTrailBackend>();
            if (backend == null) return;
            IUnityBladeTrailPoseSource source = null;
            CharacterTestCharacterEntity boundEntity = null;
            int defaultStyleId = 0;
            if (_runtime != null && instance != null && instance.SourceFrame > 0 && instance.Parameters.Value0 > 0
                && _runtime.ViewBridge.TryGetBladeTrailView(instance.SourceId, out CharacterTestRuntimeCharacterView ownerView))
            {
                CharacterTestCharacterEntity entity = null;
                for (int i = 0; i < _runtime.Entities.Count; i++)
                    if (_runtime.Entities[i].Id.Value == instance.SourceId) { entity = _runtime.Entities[i]; break; }
                if (entity != null && entity.Equipment.UseDefaultLoadout
                    && CharacterWeaponTraceBindingResolver.TryResolve(CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity),
                        instance.Parameters.Value0, instance.Parameters.Value1, out CharacterResolvedWeaponTraceBinding trace, out _))
                {
                    CharacterDefaultEquipmentRuntimeBinder equipment = ownerView.Instance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
                    if (equipment != null && equipment.TryGetSpawnedWeapon(trace.WeaponId, trace.EquipmentSlotId, out GameObject weapon)
                        && UnityBladeTrailWeaponLocators.TryResolve(weapon.transform, trace.TraceBinding.StartLocatorPath,
                            trace.TraceBinding.EndLocatorPath, out Transform root, out Transform tip))
                    {
                        source = new Source(_runtime.ActionRuntime, entity, ownerView, equipment, root, tip, instance.SourceFrame);
                        boundEntity = entity;
                        foreach (var configuredWeapon in CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity).Configs.Weapons)
                            if (configuredWeapon != null && configuredWeapon.Id == trace.WeaponId)
                            { defaultStyleId = configuredWeapon.BladeTrailStyleId; break; }
                    }
                }
            }
            backend.BindSource(source, _budget);
            PruneBindings();
            if (source is Source liveSource && !liveSource.IsDisposed)
            {
                backend.ConfigureStyle(ResolveStyle(boundEntity, instance.Parameters.Value0, defaultStyleId));
                _styleBindings.Add(new StyleBinding(backend, liveSource, boundEntity, instance.Parameters.Value0, defaultStyleId));
            }
            else backend.ConfigureStyle(0);
        }
        private sealed class Source : IUnityBladeTrailPoseSource, IUnityBladeTrailAnimationEvaluator
        {
            private readonly CharacterTestCharacterActionRuntime _actions;
            private readonly CharacterTestCharacterEntity _entity;
            private readonly CharacterTestRuntimeCharacterView _view;
            private readonly CharacterDefaultEquipmentRuntimeBinder _equipment;
            private readonly Transform _root;
            private readonly Transform _tip;
            private readonly long _actionInstance;
            private readonly int _equipmentRevision;
            private readonly UnityBladeTrailTransformSampler _sampler;
            private bool _disposed;
            internal bool IsDisposed => _disposed;
            public Source(CharacterTestCharacterActionRuntime actions, CharacterTestCharacterEntity entity,
                CharacterTestRuntimeCharacterView view, CharacterDefaultEquipmentRuntimeBinder equipment,
                Transform root, Transform tip, long actionInstance)
            {
                _actions = actions; _entity = entity; _view = view; _equipment = equipment;
                _root = root; _tip = tip; _actionInstance = actionInstance; _equipmentRevision = equipment.EquipmentRevision;
                _sampler = new UnityBladeTrailTransformSampler(view.Instance.transform, root, tip, this);
            }
            public bool TryGetPresentedPose(out UnityBladeTrailPose pose)
            {
                pose = default;
                if (_disposed || _view.Instance == null || _root == null || _tip == null || _equipment == null
                    || !_entity.Equipment.UseDefaultLoadout || _equipment.EquipmentRevision != _equipmentRevision
                    || !_actions.TryGetActiveActionPresentationState(_entity.Id, out long actionInstance, out _, out _, out _)
                    || actionInstance != _actionInstance || _view.BladeTrailActionTimeSeconds < 0d) return false;
                Transform owner = _view.Instance.transform;
                pose = new UnityBladeTrailPose(_root.position, _tip.position, owner.position, owner.rotation,
                    _view.BladeTrailActionTimeSeconds, _actionInstance);
                return true;
            }
            public bool TrySamplePose(double actionTime, Vector3 ownerPosition, Quaternion ownerRotation,
                out Vector3 root, out Vector3 tip) =>
                _sampler.TrySample(actionTime, ownerPosition, ownerRotation, out root, out tip);
            public bool TryEvaluateBladeTrailPose(double actionTimeSeconds) =>
                !_disposed && _view.Instance != null && _view.TryEvaluateBladeTrailPose(actionTimeSeconds);
            public void Dispose() { _disposed = true; }
        }
    }
}
