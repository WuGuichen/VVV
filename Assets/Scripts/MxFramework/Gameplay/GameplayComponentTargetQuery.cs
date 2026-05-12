using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentTargetQuery
    {
        private readonly int[] _requiredTags;
        private readonly int[] _blockedStatuses;

        public GameplayComponentTargetQuery(
            GameplayEntityId casterEntityId,
            int casterTeamId,
            bool requireAlive = true,
            GameplayTargetRelationFilter relationFilter = GameplayTargetRelationFilter.Any,
            IReadOnlyList<int> requiredTags = null,
            IReadOnlyList<int> blockedStatuses = null,
            int maxTargets = 0)
        {
            if (!casterEntityId.IsValid)
                throw new ArgumentException("Component target query caster entity id must be valid.", nameof(casterEntityId));
            if (maxTargets < 0)
                throw new ArgumentOutOfRangeException(nameof(maxTargets), "Max target count cannot be negative.");

            CasterEntityId = casterEntityId;
            CasterTeamId = casterTeamId;
            RequireAlive = requireAlive;
            RelationFilter = relationFilter;
            MaxTargets = maxTargets;
            _requiredTags = CopySortedUnique(requiredTags);
            _blockedStatuses = CopySortedUnique(blockedStatuses);
        }

        public GameplayEntityId CasterEntityId { get; }
        public int CasterTeamId { get; }
        public bool RequireAlive { get; }
        public GameplayTargetRelationFilter RelationFilter { get; }
        public IReadOnlyList<int> RequiredTags => _requiredTags;
        public IReadOnlyList<int> BlockedStatuses => _blockedStatuses;
        public int MaxTargets { get; }
        public bool HasMaxTargets => MaxTargets > 0;

        private static int[] CopySortedUnique(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<int>();

            var copy = new int[ids.Count];
            int count = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] > 0)
                    copy[count++] = ids[i];
            }

            if (count == 0)
                return Array.Empty<int>();

            Array.Resize(ref copy, count);
            Array.Sort(copy);
            int uniqueCount = 1;
            for (int i = 1; i < copy.Length; i++)
            {
                if (copy[i] != copy[uniqueCount - 1])
                    copy[uniqueCount++] = copy[i];
            }

            if (uniqueCount != copy.Length)
                Array.Resize(ref copy, uniqueCount);

            return copy;
        }
    }
}
