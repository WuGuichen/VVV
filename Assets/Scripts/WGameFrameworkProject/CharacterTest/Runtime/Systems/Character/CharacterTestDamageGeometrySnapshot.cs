using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestDamageGeometryStatus
    {
        Uninitialized = 0,
        Available = 1,
        ExplicitLeavesInactive = 2,
        InvalidBodyPartBinding = 3
    }

    /// <summary>Read-only metadata for one deterministic hurtbox shape.</summary>
    internal readonly struct CharacterTestDamageGeometryColliderMetadata
    {
        internal CharacterTestDamageGeometryColliderMetadata(
            int logicalColliderId,
            string logicalColliderLabel,
            CombatColliderId combatColliderId,
            int partId,
            string partLabel,
            int hitZoneId,
            string hitZoneLabel,
            int priority,
            int layer,
            CombatPhysicsShape localShape,
            FixVector3 worldMin,
            FixVector3 worldMax)
        {
            LogicalColliderId = logicalColliderId;
            LogicalColliderLabel = logicalColliderLabel ?? string.Empty;
            CombatColliderId = combatColliderId;
            PartId = partId;
            PartLabel = partLabel ?? string.Empty;
            HitZoneId = hitZoneId;
            HitZoneLabel = hitZoneLabel ?? string.Empty;
            Priority = priority;
            Layer = layer;
            HasLayer = layer >= 0 && layer <= 31;
            LocalShape = localShape;
            WorldMin = worldMin;
            WorldMax = worldMax;
        }

        public int LogicalColliderId { get; }
        public string LogicalColliderLabel { get; }
        public CombatColliderId CombatColliderId { get; }
        public int PartId { get; }
        public string PartLabel { get; }
        public int HitZoneId { get; }
        public string HitZoneLabel { get; }
        public int Priority { get; }
        /// <summary>
        /// Layer captured from the same authoritative hurtbox registration that
        /// produced the geometry. Compound leaves are not addressable through
        /// the standalone collider accessors, so this metadata is the layer
        /// authority for preview compatibility checks.
        /// </summary>
        public int Layer { get; }
        public bool HasLayer { get; }
        public CombatPhysicsShape LocalShape { get; }
        public FixVector3 WorldMin { get; }
        public FixVector3 WorldMax { get; }
    }

    /// <summary>
    /// Versioned, read-only-after-commit view of one actor's damage geometry.
    /// The owning hurtbox system reuses the storage after the view has become stale;
    /// consumers must retain and validate the revision through the attack snapshot.
    /// </summary>
    internal sealed class CharacterTestDamageGeometrySnapshot
    {
        private CharacterTestDamageGeometryColliderMetadata[] _colliders;
        private int _colliderCount;

        internal CharacterTestDamageGeometrySnapshot(int initialColliderCapacity)
        {
            _colliders = initialColliderCapacity > 0
                ? new CharacterTestDamageGeometryColliderMetadata[initialColliderCapacity]
                : Array.Empty<CharacterTestDamageGeometryColliderMetadata>();
            Invalidate();
        }

        public int EntityId { get; private set; }
        public GameplayEntityId GameplayEntityId { get; private set; }
        public CombatEntityId CombatEntityId { get; private set; }
        public RuntimeFrame Frame { get; private set; }
        public int WorldRevision { get; private set; }
        public int HurtboxRevision { get; private set; }
        /// <summary>
        /// Witnesses the Combat Physics topology independently from the per-frame
        /// animated Hurtbox revision. Stable pose refits do not advance it.
        /// </summary>
        public int TopologyRevision { get; private set; }
        public CharacterTestDamageGeometryStatus Status { get; private set; }
        public bool IsCommitted => Status != CharacterTestDamageGeometryStatus.Uninitialized;
        public bool IsValid { get; private set; }
        public int ColliderCount => _colliderCount;
        public FixVector3 WorldMin { get; private set; }
        public FixVector3 WorldMax { get; private set; }

        internal void BeginWrite(
            int entityId,
            GameplayEntityId gameplayEntityId,
            CombatEntityId combatEntityId)
        {
            EntityId = entityId;
            GameplayEntityId = gameplayEntityId;
            CombatEntityId = combatEntityId;
            Frame = default;
            WorldRevision = 0;
            HurtboxRevision = 0;
            TopologyRevision = 0;
            Status = CharacterTestDamageGeometryStatus.Uninitialized;
            _colliderCount = 0;
            WorldMin = FixVector3.Zero;
            WorldMax = FixVector3.Zero;
            IsValid = false;
        }

        internal void AddCollider(
            int logicalColliderId,
            string logicalColliderLabel,
            CombatColliderId combatColliderId,
            int partId,
            string partLabel,
            int hitZoneId,
            string hitZoneLabel,
            int priority,
            CombatPhysicsShape localShape,
            FixVector3 worldMin,
            FixVector3 worldMax)
        {
            AddCollider(
                logicalColliderId,
                logicalColliderLabel,
                combatColliderId,
                partId,
                partLabel,
                hitZoneId,
                hitZoneLabel,
                priority,
                -1,
                localShape,
                worldMin,
                worldMax);
        }

        internal void AddCollider(
            int logicalColliderId,
            string logicalColliderLabel,
            CombatColliderId combatColliderId,
            int partId,
            string partLabel,
            int hitZoneId,
            string hitZoneLabel,
            int priority,
            int layer,
            CombatPhysicsShape localShape,
            FixVector3 worldMin,
            FixVector3 worldMax)
        {
            EnsureCapacity(_colliderCount + 1);
            _colliders[_colliderCount++] = new CharacterTestDamageGeometryColliderMetadata(
                logicalColliderId,
                logicalColliderLabel,
                combatColliderId,
                partId,
                partLabel,
                hitZoneId,
                hitZoneLabel,
                priority,
                layer,
                localShape,
                worldMin,
                worldMax);

            if (_colliderCount == 1)
            {
                WorldMin = worldMin;
                WorldMax = worldMax;
            }
            else
            {
                WorldMin = Min(WorldMin, worldMin);
                WorldMax = Max(WorldMax, worldMax);
            }
        }

        internal void SortCollidersByStableLogicalId()
        {
            // Insertion sort keeps the warmed path allocation-free and makes the
            // view independent of package/dictionary enumeration order.
            for (int i = 1; i < _colliderCount; i++)
            {
                CharacterTestDamageGeometryColliderMetadata value = _colliders[i];
                int j = i - 1;
                while (j >= 0 && CompareStable(_colliders[j], value) > 0)
                {
                    _colliders[j + 1] = _colliders[j];
                    j--;
                }
                _colliders[j + 1] = value;
            }
        }

        internal void Commit(RuntimeFrame frame, int worldRevision, int hurtboxRevision)
        {
            // Compatibility seam for existing isolated fixtures that do not own a
            // CombatPhysicsWorld. Production commits always supply the real witness.
            Commit(frame, worldRevision, hurtboxRevision, topologyRevision: 0);
        }

        internal void Commit(
            RuntimeFrame frame,
            int worldRevision,
            int hurtboxRevision,
            int topologyRevision)
        {
            Frame = frame;
            WorldRevision = worldRevision;
            HurtboxRevision = hurtboxRevision;
            TopologyRevision = topologyRevision;
            Status = _colliderCount > 0
                ? CharacterTestDamageGeometryStatus.Available
                : CharacterTestDamageGeometryStatus.ExplicitLeavesInactive;
            IsValid = _colliderCount > 0;
        }

        internal void CommitUnavailable(
            RuntimeFrame frame,
            int worldRevision,
            int hurtboxRevision,
            int topologyRevision,
            CharacterTestDamageGeometryStatus status)
        {
            if (status != CharacterTestDamageGeometryStatus.ExplicitLeavesInactive
                && status != CharacterTestDamageGeometryStatus.InvalidBodyPartBinding)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Frame = frame;
            WorldRevision = worldRevision;
            HurtboxRevision = hurtboxRevision;
            TopologyRevision = topologyRevision;
            Status = status;
            IsValid = false;
            _colliderCount = 0;
            WorldMin = FixVector3.Zero;
            WorldMax = FixVector3.Zero;
        }

        internal void SealWorldRevision(RuntimeFrame frame, int worldRevision)
        {
            SealWorldRevision(frame, worldRevision, TopologyRevision);
        }

        internal void SealWorldRevision(
            RuntimeFrame frame,
            int worldRevision,
            int topologyRevision)
        {
            if (IsCommitted && Frame == frame)
            {
                WorldRevision = worldRevision;
                TopologyRevision = topologyRevision;
            }
        }

        internal void Invalidate()
        {
            EntityId = 0;
            GameplayEntityId = default;
            CombatEntityId = default;
            Frame = default;
            WorldRevision = 0;
            HurtboxRevision = 0;
            TopologyRevision = 0;
            Status = CharacterTestDamageGeometryStatus.Uninitialized;
            IsValid = false;
            _colliderCount = 0;
            WorldMin = FixVector3.Zero;
            WorldMax = FixVector3.Zero;
        }

        public CharacterTestDamageGeometryColliderMetadata GetCollider(int index)
        {
            if ((uint)index >= (uint)_colliderCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _colliders[index];
        }

        public bool TryGetCollider(
            int logicalColliderId,
            out CharacterTestDamageGeometryColliderMetadata metadata)
        {
            for (int i = 0; i < _colliderCount; i++)
            {
                if (_colliders[i].LogicalColliderId == logicalColliderId)
                {
                    metadata = _colliders[i];
                    return true;
                }
            }

            metadata = default;
            return false;
        }

        /// <summary>Finds metadata by the original physics collider id without allocating.</summary>
        public bool TryGetCollider(
            CombatColliderId combatColliderId,
            out CharacterTestDamageGeometryColliderMetadata metadata)
        {
            for (int i = 0; i < _colliderCount; i++)
            {
                if (_colliders[i].CombatColliderId.Equals(combatColliderId))
                {
                    metadata = _colliders[i];
                    return true;
                }
            }

            metadata = default;
            return false;
        }

        private void EnsureCapacity(int required)
        {
            if (_colliders.Length >= required)
                return;

            int capacity = _colliders.Length == 0 ? 1 : _colliders.Length;
            while (capacity < required)
                capacity = Math.Max(required, Math.Min(32, capacity * 2));
            Array.Resize(ref _colliders, capacity);
        }

        private static int CompareStable(
            CharacterTestDamageGeometryColliderMetadata left,
            CharacterTestDamageGeometryColliderMetadata right)
        {
            int result = CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, left.LogicalColliderId)
                .CompareTo(CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, right.LogicalColliderId));
            if (result != 0)
                return result;
            result = left.LogicalColliderId.CompareTo(right.LogicalColliderId);
            if (result != 0)
                return result;
            result = CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, left.PartId)
                .CompareTo(CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, right.PartId));
            if (result != 0)
                return result;
            result = left.PartId.CompareTo(right.PartId);
            if (result != 0)
                return result;
            result = CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, left.HitZoneId)
                .CompareTo(CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, right.HitZoneId));
            if (result != 0)
                return result;
            result = left.HitZoneId.CompareTo(right.HitZoneId);
            if (result != 0)
                return result;
            result = left.Priority.CompareTo(right.Priority);
            if (result != 0)
                return result;
            result = left.CombatColliderId.Value.CompareTo(right.CombatColliderId.Value);
            if (result != 0)
                return result;
            return left.LocalShape.GetHashCode().CompareTo(right.LocalShape.GetHashCode());
        }

        private static FixVector3 Min(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Min(left.X, right.X),
                Fix64.Min(left.Y, right.Y),
                Fix64.Min(left.Z, right.Z));
        }

        private static FixVector3 Max(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Max(left.X, right.X),
                Fix64.Max(left.Y, right.Y),
                Fix64.Max(left.Z, right.Z));
        }
    }
}
