using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Hit
{
    /// <summary>Owns range-scoped hit-target state contributions without allowing one source to clear another.</summary>
    /// <remarks>LowFreqAlloc when source/entity high-water marks grow; steady-state updates are NoAlloc.</remarks>
    public sealed class HitTargetStateContributionStore
    {
        public const HitTargetStateFlags AllowedTransientFlags =
            HitTargetStateFlags.Invincible
            | HitTargetStateFlags.Parrying
            | HitTargetStateFlags.Blocking
            | HitTargetStateFlags.SuperArmor
            | HitTargetStateFlags.Unhittable;

        private readonly Dictionary<ContributionKey, HitTargetStateFlags> _contributions =
            new Dictionary<ContributionKey, HitTargetStateFlags>();
        private readonly Dictionary<CombatEntityId, HitTargetStateFlags> _resolved =
            new Dictionary<CombatEntityId, HitTargetStateFlags>();

        public int Count => _contributions.Count;

        public void Upsert(
            CombatEntityId entityId,
            long actionInstanceId,
            int stableEventId,
            HitTargetStateFlags flags)
        {
            ValidateSource(entityId, actionInstanceId, stableEventId);
            ValidateFlags(flags);
            var key = new ContributionKey(entityId, actionInstanceId, stableEventId);
            if (_contributions.TryGetValue(key, out HitTargetStateFlags current) && current == flags)
                return;
            _contributions[key] = flags;
            RebuildResolved(entityId);
        }

        public bool Remove(CombatEntityId entityId, long actionInstanceId, int stableEventId)
        {
            if (entityId.IsNone || actionInstanceId <= 0 || stableEventId <= 0)
                return false;
            bool removed = _contributions.Remove(new ContributionKey(entityId, actionInstanceId, stableEventId));
            if (removed)
                RebuildResolved(entityId);
            return removed;
        }

        public int RemoveInstance(CombatEntityId entityId, long actionInstanceId)
        {
            if (entityId.IsNone || actionInstanceId <= 0)
                return 0;
            int removed = RemoveWhere(key => key.EntityId.Equals(entityId) && key.ActionInstanceId == actionInstanceId);
            if (removed > 0)
                RebuildResolved(entityId);
            return removed;
        }

        public int ClearEntity(CombatEntityId entityId)
        {
            if (entityId.IsNone)
                return 0;
            int removed = RemoveWhere(key => key.EntityId.Equals(entityId));
            _resolved.Remove(entityId);
            return removed;
        }

        public void Clear()
        {
            _contributions.Clear();
            _resolved.Clear();
        }

        public HitTargetStateFlags ResolveFlags(CombatEntityId entityId)
        {
            return !entityId.IsNone && _resolved.TryGetValue(entityId, out HitTargetStateFlags flags)
                ? flags
                : HitTargetStateFlags.None;
        }

        public int CopyEntityIds(List<CombatEntityId> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            int startCount = output.Count;
            foreach (CombatEntityId entityId in _resolved.Keys)
                output.Add(entityId);
            return output.Count - startCount;
        }

        private int RemoveWhere(Predicate<ContributionKey> predicate)
        {
            if (_contributions.Count == 0)
                return 0;
            var keys = new List<ContributionKey>();
            foreach (ContributionKey key in _contributions.Keys)
                if (predicate(key))
                    keys.Add(key);
            for (int i = 0; i < keys.Count; i++)
                _contributions.Remove(keys[i]);
            return keys.Count;
        }

        private void RebuildResolved(CombatEntityId entityId)
        {
            HitTargetStateFlags flags = HitTargetStateFlags.None;
            foreach (KeyValuePair<ContributionKey, HitTargetStateFlags> pair in _contributions)
                if (pair.Key.EntityId.Equals(entityId))
                    flags |= pair.Value;
            if (flags == HitTargetStateFlags.None)
                _resolved.Remove(entityId);
            else
                _resolved[entityId] = flags;
        }

        private static void ValidateSource(CombatEntityId entityId, long actionInstanceId, int stableEventId)
        {
            if (entityId.IsNone)
                throw new ArgumentException("Contribution requires a valid combat entity.", nameof(entityId));
            if (actionInstanceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(actionInstanceId));
            if (stableEventId <= 0)
                throw new ArgumentException("Contribution requires a stable event id.", nameof(stableEventId));
        }

        public static void ValidateFlags(HitTargetStateFlags flags)
        {
            if (flags == HitTargetStateFlags.None || (flags & ~AllowedTransientFlags) != 0)
                throw new ArgumentOutOfRangeException(nameof(flags), "Only non-zero transient hit-target state flags are allowed; Alive is lifecycle-owned.");
        }

        private readonly struct ContributionKey : IEquatable<ContributionKey>
        {
            public ContributionKey(CombatEntityId entityId, long actionInstanceId, int stableEventId)
            {
                EntityId = entityId;
                ActionInstanceId = actionInstanceId;
                StableEventId = stableEventId;
            }

            public CombatEntityId EntityId { get; }
            public long ActionInstanceId { get; }
            public int StableEventId { get; }

            public bool Equals(ContributionKey other) =>
                EntityId.Equals(other.EntityId)
                && ActionInstanceId == other.ActionInstanceId
                && StableEventId == other.StableEventId;

            public override bool Equals(object obj) => obj is ContributionKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = EntityId.GetHashCode();
                    hash = (hash * 397) ^ ActionInstanceId.GetHashCode();
                    hash = (hash * 397) ^ StableEventId;
                    return hash;
                }
            }
        }
    }
}
