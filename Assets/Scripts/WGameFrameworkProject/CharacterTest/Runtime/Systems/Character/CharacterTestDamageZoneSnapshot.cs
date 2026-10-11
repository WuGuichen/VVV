using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestDamageZoneMember
    {
        internal CharacterTestDamageZoneMember(
            int hitZoneId,
            int logicalHurtboxId,
            CombatColliderId combatColliderId)
        {
            HitZoneId = hitZoneId;
            LogicalHurtboxId = logicalHurtboxId;
            CombatColliderId = combatColliderId;
        }

        public int HitZoneId { get; }
        public int LogicalHurtboxId { get; }
        public CombatColliderId CombatColliderId { get; }
    }

    internal readonly struct CharacterTestDamageZoneAggregate
    {
        internal CharacterTestDamageZoneAggregate(
            int zoneId,
            string displayName,
            bool hittable,
            bool aimable,
            bool approachable,
            int authoredPriority,
            int memberStart,
            int memberCount,
            bool isAvailable,
            FixVector3 worldMin,
            FixVector3 worldMax)
        {
            ZoneId = zoneId;
            DisplayName = displayName ?? string.Empty;
            Hittable = hittable;
            Aimable = aimable;
            Approachable = approachable;
            AuthoredPriority = authoredPriority;
            MemberStart = memberStart;
            MemberCount = memberCount;
            IsAvailable = isAvailable;
            WorldMin = worldMin;
            WorldMax = worldMax;
        }

        public int ZoneId { get; }
        public string DisplayName { get; }
        public bool Hittable { get; }
        public bool Aimable { get; }
        public bool Approachable { get; }
        public int AuthoredPriority { get; }
        public int MemberStart { get; }
        public int MemberCount { get; }
        /// <summary>
        /// True only when at least one currently active Hurtbox leaf supports
        /// this authored Zone. The authored Zone identity remains present when
        /// false so topology rebuilds never renumber or delete definitions.
        /// </summary>
        public bool IsAvailable { get; }
        public FixVector3 WorldMin { get; }
        public FixVector3 WorldMax { get; }
    }

    /// <summary>Reusable sealed DamageZone view built exclusively from imported binding and hurtbox authority.</summary>
    internal sealed class CharacterTestDamageZoneSnapshot
    {
        private CharacterTestDamageZoneAggregate[] _zones = Array.Empty<CharacterTestDamageZoneAggregate>();
        private CharacterTestDamageZoneMember[] _members = Array.Empty<CharacterTestDamageZoneMember>();
        private int _zoneCount;
        private int _memberCount;
        private int _availableZoneCount;

        public int EntityId { get; private set; }
        public GameplayEntityId GameplayEntityId { get; private set; }
        public CombatEntityId CombatEntityId { get; private set; }
        public RuntimeFrame Frame { get; private set; }
        public int WorldRevision { get; private set; }
        public int HurtboxRevision { get; private set; }
        /// <summary>
        /// Combat Physics topology witness captured from the active geometry
        /// used to build this Zone view. Pose-only refits remain represented by
        /// <see cref="HurtboxRevision"/> and do not change Zone availability.
        /// </summary>
        public int TopologyRevision { get; private set; }
        public int BindingRevision { get; private set; }
        public int ZoneRevision { get; private set; }
        public bool IsValid { get; private set; }
        public int ZoneCount => _zoneCount;
        public int AvailableZoneCount => _availableZoneCount;
        public int MemberCount => _memberCount;

        internal void Build(
            CharacterTestDamageGeometrySnapshot geometry,
            CharacterDamageZoneRuntimeBinding[] bindings)
        {
            Invalidate();
            if (geometry == null
                || !geometry.IsCommitted
                || geometry.Status == CharacterTestDamageGeometryStatus.InvalidBodyPartBinding
                || bindings == null
                || bindings.Length == 0)
            {
                return;
            }

            EnsureZoneCapacity(bindings.Length);
            int bindingRevision = 17;
            for (int zoneIndex = 0; zoneIndex < bindings.Length; zoneIndex++)
            {
                CharacterDamageZoneRuntimeBinding binding = bindings[zoneIndex];
                bindingRevision = AddStableHash(bindingRevision, binding);
                int memberStart = _memberCount;
                FixVector3 worldMin = FixVector3.Zero;
                FixVector3 worldMax = FixVector3.Zero;
                bool hasBounds = false;
                for (int colliderIndex = 0; colliderIndex < geometry.ColliderCount; colliderIndex++)
                {
                    CharacterTestDamageGeometryColliderMetadata collider = geometry.GetCollider(colliderIndex);
                    if (!Contains(binding.HitZoneIds, collider.HitZoneId))
                        continue;
                    EnsureMemberCapacity(_memberCount + 1);
                    _members[_memberCount++] = new CharacterTestDamageZoneMember(
                        collider.HitZoneId,
                        collider.LogicalColliderId,
                        collider.CombatColliderId);
                    if (!hasBounds)
                    {
                        worldMin = collider.WorldMin;
                        worldMax = collider.WorldMax;
                        hasBounds = true;
                    }
                    else
                    {
                        worldMin = Min(worldMin, collider.WorldMin);
                        worldMax = Max(worldMax, collider.WorldMax);
                    }
                }

                _zones[_zoneCount++] = new CharacterTestDamageZoneAggregate(
                    binding.ZoneId,
                    binding.DisplayName,
                    binding.Hittable,
                    binding.Aimable,
                    binding.Approachable,
                    binding.AuthoredPriority,
                    memberStart,
                    _memberCount - memberStart,
                    hasBounds,
                    worldMin,
                    worldMax);
                if (hasBounds)
                    _availableZoneCount++;
            }

            EntityId = geometry.EntityId;
            GameplayEntityId = geometry.GameplayEntityId;
            CombatEntityId = geometry.CombatEntityId;
            Frame = geometry.Frame;
            WorldRevision = geometry.WorldRevision;
            HurtboxRevision = geometry.HurtboxRevision;
            TopologyRevision = geometry.TopologyRevision;
            BindingRevision = bindingRevision;
            int zoneRevision = AddStableHash(bindingRevision, _zoneCount);
            zoneRevision = AddStableHash(zoneRevision, _availableZoneCount);
            for (int i = 0; i < _zoneCount; i++)
            {
                CharacterTestDamageZoneAggregate zone = _zones[i];
                zoneRevision = AddStableHash(zoneRevision, zone.ZoneId);
                zoneRevision = AddStableHash(zoneRevision, zone.IsAvailable ? 1 : 0);
                zoneRevision = AddStableHash(zoneRevision, zone.MemberCount);
                int end = zone.MemberStart + zone.MemberCount;
                for (int memberIndex = zone.MemberStart; memberIndex < end; memberIndex++)
                {
                    zoneRevision = AddStableHash(zoneRevision, _members[memberIndex].HitZoneId);
                    zoneRevision = AddStableHash(zoneRevision, _members[memberIndex].LogicalHurtboxId);
                    zoneRevision = AddStableHash(zoneRevision, _members[memberIndex].CombatColliderId.Value);
                }
            }
            ZoneRevision = zoneRevision;
            IsValid = _zoneCount > 0;
        }

        internal void SealWorldRevision(RuntimeFrame frame, int worldRevision)
        {
            if (IsValid && Frame == frame)
                WorldRevision = worldRevision;
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
            BindingRevision = 0;
            ZoneRevision = 0;
            IsValid = false;
            _zoneCount = 0;
            _memberCount = 0;
            _availableZoneCount = 0;
        }

        public CharacterTestDamageZoneAggregate GetZone(int index)
        {
            if ((uint)index >= (uint)_zoneCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _zones[index];
        }

        public CharacterTestDamageZoneMember GetMember(int index)
        {
            if ((uint)index >= (uint)_memberCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _members[index];
        }

        public bool TryGetZone(int zoneId, out CharacterTestDamageZoneAggregate zone)
        {
            for (int i = 0; i < _zoneCount; i++)
            {
                if ((_zones[i].ZoneId == zoneId))
                {
                    zone = _zones[i];
                    return true;
                }
            }
            zone = default;
            return false;
        }

        public bool TryGetAvailableZone(int zoneId, out CharacterTestDamageZoneAggregate zone)
        {
            if (TryGetZone(zoneId, out zone) && zone.IsAvailable)
                return true;
            zone = default;
            return false;
        }

        public bool ContainsHitZone(int zoneId, int hitZoneId)
        {
            if (!TryGetAvailableZone(zoneId, out CharacterTestDamageZoneAggregate zone))
                return false;
            int end = zone.MemberStart + zone.MemberCount;
            for (int i = zone.MemberStart; i < end; i++)
            {
                if ((_members[i].HitZoneId == hitZoneId))
                    return true;
            }
            return false;
        }

        public bool ContainsMemberWitness(
            int zoneId,
            int hitZoneId,
            int logicalHurtboxId)
        {
            if (!TryGetAvailableZone(zoneId, out CharacterTestDamageZoneAggregate zone))
                return false;
            int end = zone.MemberStart + zone.MemberCount;
            for (int i = zone.MemberStart; i < end; i++)
            {
                CharacterTestDamageZoneMember member = _members[i];
                if (((hitZoneId <= 0)
                        || (member.HitZoneId == hitZoneId))
                    && ((logicalHurtboxId <= 0)
                        || (member.LogicalHurtboxId == logicalHurtboxId)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Contains(int[] values, int value)
        {
            for (int i = 0; values != null && i < values.Length; i++)
            {
                if (values[i] == value)
                    return true;
            }
            return false;
        }

        private void EnsureZoneCapacity(int required)
        {
            if (_zones.Length < required)
                Array.Resize(ref _zones, required);
        }

        private void EnsureMemberCapacity(int required)
        {
            if (_members.Length >= required)
                return;
            int capacity = _members.Length == 0 ? 4 : _members.Length * 2;
            if (capacity < required) capacity = required;
            Array.Resize(ref _members, capacity);
        }

        private static int AddStableHash(int hash, CharacterDamageZoneRuntimeBinding binding)
        {
            hash = AddStableHash(hash, binding.ZoneId);
            hash = AddStableHash(hash, binding.Hittable ? 1 : 0);
            hash = AddStableHash(hash, binding.Aimable ? 1 : 0);
            hash = AddStableHash(hash, binding.Approachable ? 1 : 0);
            hash = AddStableHash(hash, binding.AuthoredPriority);
            for (int i = 0; i < binding.HitZoneIds.Length; i++)
                hash = AddStableHash(hash, binding.HitZoneIds[i]);
            return hash;
        }

        private static int AddStableHash(int hash, int value)
        {
            unchecked { return hash * 31 + value; }
        }

        private static FixVector3 Min(FixVector3 left, FixVector3 right) => new FixVector3(
            Fix64.Min(left.X, right.X), Fix64.Min(left.Y, right.Y), Fix64.Min(left.Z, right.Z));

        private static FixVector3 Max(FixVector3 left, FixVector3 right) => new FixVector3(
            Fix64.Max(left.X, right.X), Fix64.Max(left.Y, right.Y), Fix64.Max(left.Z, right.Z));
    }
}
