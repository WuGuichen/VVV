using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;

namespace MxFramework.Combat.Shields
{
    public sealed class CombatShieldRuntime
    {
        private readonly CombatPhysicsWorld _physicsWorld;
        private readonly List<CombatShieldPotencyComponent> _shields;
        private readonly Dictionary<CombatEntityId, int> _indicesByShieldEntity;
        private readonly List<CombatShieldImpactRequest> _impactRequests;
        private readonly List<CombatShieldImpactRequest> _dueImpactRequests;
        private readonly List<CombatShieldBlockEvent> _blockEvents;
        private readonly List<CombatShieldPotencyChangedEvent> _potencyChangedEvents;
        private readonly List<CombatShieldBreakEvent> _breakEvents;

        public CombatShieldRuntime(CombatPhysicsWorld physicsWorld = null)
        {
            _physicsWorld = physicsWorld;
            _shields = new List<CombatShieldPotencyComponent>();
            _indicesByShieldEntity = new Dictionary<CombatEntityId, int>();
            _impactRequests = new List<CombatShieldImpactRequest>();
            _dueImpactRequests = new List<CombatShieldImpactRequest>();
            _blockEvents = new List<CombatShieldBlockEvent>();
            _potencyChangedEvents = new List<CombatShieldPotencyChangedEvent>();
            _breakEvents = new List<CombatShieldBreakEvent>();
        }

        public IReadOnlyList<CombatShieldBlockEvent> BlockEvents => _blockEvents;
        public IReadOnlyList<CombatShieldPotencyChangedEvent> PotencyChangedEvents => _potencyChangedEvents;
        public IReadOnlyList<CombatShieldBreakEvent> BreakEvents => _breakEvents;
        public int ShieldCount => _shields.Count;

        public void Clear()
        {
            for (int i = 0; i < _shields.Count; i++)
                RemoveColliderIfNeeded(_shields[i]);

            _shields.Clear();
            _indicesByShieldEntity.Clear();
            _impactRequests.Clear();
            _dueImpactRequests.Clear();
            ClearFrameEvents();
        }

        /// <remarks>NoAlloc when replacing an existing shield after initialization and list capacity is sufficient.</remarks>
        public void UpsertShield(CombatShieldPotencyComponent shield)
        {
            if (_indicesByShieldEntity.TryGetValue(shield.ShieldEntityId, out int index))
            {
                CombatShieldPotencyComponent previous = _shields[index];
                if (!previous.BodyId.Equals(shield.BodyId) || !previous.ColliderId.Equals(shield.ColliderId))
                    RemoveColliderIfNeeded(previous);

                _shields[index] = shield;
                if (shield.IsBroken)
                    RemoveColliderIfNeeded(shield);

                return;
            }

            _indicesByShieldEntity[shield.ShieldEntityId] = _shields.Count;
            _shields.Add(shield);
            if (shield.IsBroken)
                RemoveColliderIfNeeded(shield);
        }

        public bool RemoveShield(CombatEntityId shieldEntityId)
        {
            if (!_indicesByShieldEntity.TryGetValue(shieldEntityId, out int index))
                return false;

            RemoveColliderIfNeeded(_shields[index]);
            _shields.RemoveAt(index);
            RebuildIndices();
            return true;
        }

        public bool TryGetShield(CombatEntityId shieldEntityId, out CombatShieldPotencyComponent shield)
        {
            if (_indicesByShieldEntity.TryGetValue(shieldEntityId, out int index))
            {
                shield = _shields[index];
                return true;
            }

            shield = default;
            return false;
        }

        public bool TryGetActiveShield(CombatEntityId shieldEntityId, out CombatShieldPotencyComponent shield)
        {
            if (TryGetShield(shieldEntityId, out shield) && !shield.IsBroken)
                return true;

            shield = default;
            return false;
        }

        public bool TryGetActiveShieldByCollider(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            out CombatShieldPotencyComponent shield)
        {
            for (int i = 0; i < _shields.Count; i++)
            {
                CombatShieldPotencyComponent candidate = _shields[i];
                if (!candidate.IsBroken
                    && candidate.BodyId.Equals(bodyId)
                    && candidate.ColliderId.Equals(colliderId))
                {
                    shield = candidate;
                    return true;
                }
            }

            shield = default;
            return false;
        }

        public int CopyShieldsTo(List<CombatShieldPotencyComponent> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            int startCount = results.Count;
            for (int i = 0; i < _shields.Count; i++)
                results.Add(_shields[i]);

            results.Sort(startCount, _shields.Count, CombatShieldPotencyComponentComparer.Instance);
            return _shields.Count;
        }

        /// <remarks>NoAlloc on the steady-state path after queue capacity is warm.</remarks>
        public void EnqueueImpact(CombatShieldImpactRequest request)
        {
            _impactRequests.Add(request);
        }

        /// <remarks>NoAlloc on the steady-state path after queue/event capacities are warm.</remarks>
        public void Tick(CombatFrame frame)
        {
            if (frame.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(frame), "Shield runtime frame cannot be negative.");

            ClearFrameEvents();
            if (_impactRequests.Count > 0)
            {
                _dueImpactRequests.AddRange(_impactRequests);
                _impactRequests.Clear();
                _dueImpactRequests.Sort(CombatShieldImpactRequestComparer.Instance);
                for (int i = 0; i < _dueImpactRequests.Count; i++)
                    TryApplyImpact(_dueImpactRequests[i], out _);

                _dueImpactRequests.Clear();
            }

            RecoverEligibleShields(frame);
        }

        /// <remarks>NoAlloc on the steady-state path after event capacity is warm.</remarks>
        public bool TryApplyImpact(CombatShieldImpactRequest request, out CombatShieldBlockEvent blockEvent)
        {
            if (!_indicesByShieldEntity.TryGetValue(request.ShieldEntityId, out int index))
            {
                blockEvent = default;
                return false;
            }

            CombatShieldPotencyComponent before = _shields[index];
            if (before.IsBroken)
            {
                blockEvent = default;
                return false;
            }

            CombatShieldPotencyComponent after = before.ApplyDelta(request.Delta, request.Frame.Value);
            _shields[index] = after;
            bool triggeredBreak = !before.IsBroken && after.IsBroken;
            blockEvent = new CombatShieldBlockEvent(
                request,
                before.OwnerEntityId,
                before.CurrentPotency,
                after.CurrentPotency,
                before.ShieldPressure,
                after.ShieldPressure,
                after.CurrentCohesionLimit,
                before.CurrentBand,
                after.CurrentBand,
                triggeredBreak);
            _blockEvents.Add(blockEvent);

            PublishChangeIfNeeded(
                before,
                after,
                request.Frame,
                CombatShieldPotencyChangeReason.Impact,
                request.TraceId);

            if (triggeredBreak)
            {
                RemoveColliderIfNeeded(after);
                _breakEvents.Add(new CombatShieldBreakEvent(
                    after.ShieldEntityId,
                    after.OwnerEntityId,
                    request.SourceEntityId,
                    request.Frame,
                    after.ShieldPressure,
                    after.CurrentCohesionLimit,
                    request.TraceId));
            }

            return true;
        }

        private static readonly Comparison<CombatShieldPotencyComponent> StateViewComparison = CombatShieldPotencyComponentComparer.Instance.Compare;
        private readonly List<CombatShieldPotencyComponent> _stateViewShields = new List<CombatShieldPotencyComponent>(32);
        private CombatShieldRuntimeState _stateView;

        /// <summary>Borrowed sorted state for immediate indexed consumption; never retain it as history.</summary>
        /// <remarks>NoAlloc after initialization and capacity warmup.</remarks>
        public CombatShieldRuntimeState ReadStateView(string moduleId = CombatShieldRuntimeModule.DefaultModuleId, int moduleIdentity = CombatShieldRuntimeModule.DefaultModuleIdentity)
        {
            _stateViewShields.Clear();
            for (int i = 0; i < _shields.Count; i++) _stateViewShields.Add(_shields[i]);
            _stateViewShields.Sort(StateViewComparison);
            _stateView ??= new CombatShieldRuntimeState(CombatShieldRuntimeState.CurrentSchemaVersion, moduleId, _stateViewShields, true, moduleIdentity);
            _stateView.ModuleId = moduleId ?? string.Empty;
            _stateView.ModuleIdentity = moduleIdentity;
            return _stateView;
        }

        public CombatShieldRuntimeState CreateStateSnapshot(string moduleId = CombatShieldRuntimeModule.DefaultModuleId, int moduleIdentity = CombatShieldRuntimeModule.DefaultModuleIdentity)
        {
            var shields = new List<CombatShieldPotencyComponent>(_shields.Count);
            CopyShieldsTo(shields);
            return new CombatShieldRuntimeState(
                CombatShieldRuntimeState.CurrentSchemaVersion,
                moduleId,
                shields, moduleIdentity);
        }

        public CombatShieldDebugSnapshot CreateDebugSnapshot(string moduleId = CombatShieldRuntimeModule.DefaultModuleId)
        {
            var shields = new List<CombatShieldPotencyComponent>(_shields.Count);
            CopyShieldsTo(shields);
            return new CombatShieldDebugSnapshot(
                moduleId,
                shields,
                _blockEvents,
                _potencyChangedEvents,
                _breakEvents);
        }

        public void RestoreRuntimeState(CombatShieldRuntimeState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (state.SchemaVersion != CombatShieldRuntimeState.CurrentSchemaVersion)
                throw new ArgumentException("Unsupported Combat shield runtime state schema.", nameof(state));

            for (int i = 0; i < _shields.Count; i++)
            {
                CombatShieldPotencyComponent oldShield = _shields[i];
                if (!ContainsUnbrokenShieldWithCollider(state.Shields, oldShield.BodyId, oldShield.ColliderId))
                    RemoveColliderIfNeeded(oldShield);
            }

            _shields.Clear();
            _indicesByShieldEntity.Clear();
            _impactRequests.Clear();
            _dueImpactRequests.Clear();
            ClearFrameEvents();

            for (int i = 0; i < state.Shields.Count; i++)
                UpsertShield(state.Shields[i]);
        }

        private void RecoverEligibleShields(CombatFrame frame)
        {
            for (int i = 0; i < _shields.Count; i++)
            {
                CombatShieldPotencyComponent before = _shields[i];
                if (before.IsBroken || before.RecoveryRate <= 0 || before.ShieldPressure <= 0)
                    continue;
                if (frame.Value - before.LastImpactFrame <= before.RecoveryDelayFrames)
                    continue;

                CombatShieldPotencyComponent after = before.Recover(before.RecoveryRate);
                _shields[i] = after;
                PublishChangeIfNeeded(
                    before,
                    after,
                    frame,
                    CombatShieldPotencyChangeReason.Recovery,
                    traceId: 0);
            }
        }

        private void PublishChangeIfNeeded(
            CombatShieldPotencyComponent before,
            CombatShieldPotencyComponent after,
            CombatFrame frame,
            CombatShieldPotencyChangeReason reason,
            int traceId)
        {
            if (before.Equals(after))
                return;

            _potencyChangedEvents.Add(new CombatShieldPotencyChangedEvent(
                after.ShieldEntityId,
                after.OwnerEntityId,
                frame,
                reason,
                before.CurrentPotency,
                after.CurrentPotency,
                before.ShieldPressure,
                after.ShieldPressure,
                before.CurrentBand,
                after.CurrentBand,
                traceId));
        }

        private void RemoveColliderIfNeeded(CombatShieldPotencyComponent shield)
        {
            _physicsWorld?.RemoveCollider(shield.BodyId, shield.ColliderId);
        }

        private void ClearFrameEvents()
        {
            _blockEvents.Clear();
            _potencyChangedEvents.Clear();
            _breakEvents.Clear();
        }

        private void RebuildIndices()
        {
            _indicesByShieldEntity.Clear();
            for (int i = 0; i < _shields.Count; i++)
                _indicesByShieldEntity[_shields[i].ShieldEntityId] = i;
        }

        private static bool ContainsUnbrokenShieldWithCollider(
            IReadOnlyList<CombatShieldPotencyComponent> shields,
            CombatBodyId bodyId,
            CombatColliderId colliderId)
        {
            for (int i = 0; i < shields.Count; i++)
            {
                CombatShieldPotencyComponent shield = shields[i];
                if (!shield.IsBroken
                    && shield.BodyId.Equals(bodyId)
                    && shield.ColliderId.Equals(colliderId))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class CombatShieldRuntimeState
    {
        public const int CurrentSchemaVersion = 2;

        public CombatShieldRuntimeState(
            int schemaVersion,
            string moduleId,
            IReadOnlyList<CombatShieldPotencyComponent> shields, int moduleIdentity = CombatShieldRuntimeModule.DefaultModuleIdentity)
            : this(schemaVersion, moduleId, shields, false, moduleIdentity)
        { }

        internal CombatShieldRuntimeState(
            int schemaVersion,
            string moduleId,
            IReadOnlyList<CombatShieldPotencyComponent> shields,
            bool borrow, int moduleIdentity = CombatShieldRuntimeModule.DefaultModuleIdentity)
{
            if (moduleIdentity <= 0) throw new ArgumentOutOfRangeException(nameof(moduleIdentity));
            ModuleIdentity = moduleIdentity;
            SchemaVersion = schemaVersion;
            ModuleId = moduleId ?? string.Empty;
            Shields = borrow ? shields : Copy(shields);
        }

        public int SchemaVersion { get; }
        public int ModuleIdentity { get; internal set; }
        public string ModuleId { get; internal set; }
        public IReadOnlyList<CombatShieldPotencyComponent> Shields { get; }

        private static ReadOnlyCollection<CombatShieldPotencyComponent> Copy(
            IReadOnlyList<CombatShieldPotencyComponent> source)
        {
            if (source == null || source.Count == 0)
                return new ReadOnlyCollection<CombatShieldPotencyComponent>(new List<CombatShieldPotencyComponent>());

            var copy = new List<CombatShieldPotencyComponent>(source.Count);
            for (int i = 0; i < source.Count; i++)
                copy.Add(source[i]);

            copy.Sort(CombatShieldPotencyComponentComparer.Instance);
            return new ReadOnlyCollection<CombatShieldPotencyComponent>(copy);
        }
    }

    public sealed class CombatShieldDebugSnapshot
    {
        public CombatShieldDebugSnapshot(
            string moduleId,
            IReadOnlyList<CombatShieldPotencyComponent> shields,
            IReadOnlyList<CombatShieldBlockEvent> blockEvents,
            IReadOnlyList<CombatShieldPotencyChangedEvent> potencyChangedEvents,
            IReadOnlyList<CombatShieldBreakEvent> breakEvents)
        {
            ModuleId = moduleId ?? string.Empty;
            Shields = Copy(shields);
            BlockEvents = Copy(blockEvents);
            PotencyChangedEvents = Copy(potencyChangedEvents);
            BreakEvents = Copy(breakEvents);
        }

        public string ModuleId { get; }
        public IReadOnlyList<CombatShieldPotencyComponent> Shields { get; }
        public IReadOnlyList<CombatShieldBlockEvent> BlockEvents { get; }
        public IReadOnlyList<CombatShieldPotencyChangedEvent> PotencyChangedEvents { get; }
        public IReadOnlyList<CombatShieldBreakEvent> BreakEvents { get; }

        private static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
                return new ReadOnlyCollection<T>(new List<T>());

            var copy = new List<T>(source.Count);
            for (int i = 0; i < source.Count; i++)
                copy.Add(source[i]);

            return new ReadOnlyCollection<T>(copy);
        }
    }

    public sealed class CombatShieldPotencyComponentComparer : IComparer<CombatShieldPotencyComponent>
    {
        public static readonly CombatShieldPotencyComponentComparer Instance = new CombatShieldPotencyComponentComparer();

        private CombatShieldPotencyComponentComparer()
        {
        }

        public int Compare(CombatShieldPotencyComponent left, CombatShieldPotencyComponent right)
        {
            return left.ShieldEntityId.CompareTo(right.ShieldEntityId);
        }
    }

    internal sealed class CombatShieldImpactRequestComparer : IComparer<CombatShieldImpactRequest>
    {
        public static readonly CombatShieldImpactRequestComparer Instance = new CombatShieldImpactRequestComparer();

        private CombatShieldImpactRequestComparer()
        {
        }

        public int Compare(CombatShieldImpactRequest left, CombatShieldImpactRequest right)
        {
            int compare = left.Frame.CompareTo(right.Frame);
            if (compare != 0)
                return compare;

            compare = left.ActionInstanceId.CompareTo(right.ActionInstanceId);
            if (compare != 0)
                return compare;

            compare = left.TraceId.CompareTo(right.TraceId);
            if (compare != 0)
                return compare;

            return left.ShieldEntityId.CompareTo(right.ShieldEntityId);
        }
    }
}


