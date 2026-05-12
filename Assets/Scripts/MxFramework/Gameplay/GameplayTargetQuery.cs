using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Pure C# target query over entity identity, team relation, tags, statuses, and count limit.</summary>
    public sealed class GameplayTargetQuery
    {
        private readonly int[] _requiredTags;
        private readonly int[] _blockedStatuses;

        public GameplayTargetQuery(
            int casterEntityId,
            int casterTeamId,
            bool requireAlive = true,
            GameplayTargetRelationFilter relationFilter = GameplayTargetRelationFilter.Any,
            IReadOnlyList<int> requiredTags = null,
            IReadOnlyList<int> blockedStatuses = null,
            int maxTargets = 0)
        {
            if (maxTargets < 0)
                throw new ArgumentOutOfRangeException(nameof(maxTargets), "Max target count cannot be negative.");

            CasterEntityId = casterEntityId;
            CasterTeamId = casterTeamId;
            RequireAlive = requireAlive;
            RelationFilter = relationFilter;
            MaxTargets = maxTargets;
            _requiredTags = CopyIds(requiredTags);
            _blockedStatuses = CopyIds(blockedStatuses);
        }

        public GameplayTargetQuery(
            int casterEntityId,
            int casterTeamId,
            bool requireAlive,
            GameplayTargetRelationFilter relationFilter,
            IReadOnlyList<GameplayTagId> requiredTags,
            IReadOnlyList<GameplayStatusId> blockedStatuses,
            int maxTargets = 0)
        {
            if (maxTargets < 0)
                throw new ArgumentOutOfRangeException(nameof(maxTargets), "Max target count cannot be negative.");

            CasterEntityId = casterEntityId;
            CasterTeamId = casterTeamId;
            RequireAlive = requireAlive;
            RelationFilter = relationFilter;
            MaxTargets = maxTargets;
            _requiredTags = CopyTagIds(requiredTags);
            _blockedStatuses = CopyStatusIds(blockedStatuses);
        }

        public GameplayTargetQuery(
            int casterEntityId,
            int casterTeamId,
            bool requireAlive,
            GameplayTargetRelationFilter relationFilter,
            GameplayTagSet requiredTags,
            GameplayStatusSet blockedStatuses,
            int maxTargets = 0)
        {
            if (maxTargets < 0)
                throw new ArgumentOutOfRangeException(nameof(maxTargets), "Max target count cannot be negative.");

            CasterEntityId = casterEntityId;
            CasterTeamId = casterTeamId;
            RequireAlive = requireAlive;
            RelationFilter = relationFilter;
            MaxTargets = maxTargets;
            _requiredTags = CopyTagIds(requiredTags);
            _blockedStatuses = CopyStatusIds(blockedStatuses);
        }

        public int CasterEntityId { get; }

        public int CasterTeamId { get; }

        public bool RequireAlive { get; }

        public GameplayTargetRelationFilter RelationFilter { get; }

        public IReadOnlyList<int> RequiredTags => _requiredTags;

        public IReadOnlyList<int> BlockedStatuses => _blockedStatuses;

        /// <summary>Maximum selected targets. A value of 0 means no limit.</summary>
        public int MaxTargets { get; }

        public bool HasMaxTargets => MaxTargets > 0;

        private static int[] CopyIds(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<int>();

            var copy = new int[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                copy[i] = ids[i];

            return copy;
        }

        private static int[] CopyTagIds(IReadOnlyList<GameplayTagId> ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<int>();

            var copy = new int[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                copy[i] = ids[i].Value;

            return copy;
        }

        private static int[] CopyStatusIds(IReadOnlyList<GameplayStatusId> ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<int>();

            var copy = new int[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                copy[i] = ids[i].Value;

            return copy;
        }

        private static int[] CopyTagIds(GameplayTagSet ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<int>();

            var copy = new int[ids.Count];
            int index = 0;
            foreach (GameplayTagId id in ids)
                copy[index++] = id.Value;

            return copy;
        }

        private static int[] CopyStatusIds(GameplayStatusSet ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<int>();

            var copy = new int[ids.Count];
            int index = 0;
            foreach (GameplayStatusId id in ids)
                copy[index++] = id.Value;

            return copy;
        }
    }
}
