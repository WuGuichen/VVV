using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Immutable generation-safe candidate snapshot used by component targeting.
    /// </summary>
    /// <remarks>
    /// AllocByDesign. Construction copies and canonicalizes caller-owned tag and
    /// status collections; identity and capability checks are NoAlloc.
    /// </remarks>
    public readonly struct GameplayComponentTargetCandidate
    {
        private readonly int[] _tags;
        private readonly int[] _statuses;

        public GameplayComponentTargetCandidate(
            GameplayEntityId entityId,
            int teamId,
            GameplayLifecycleState lifecycleState,
            IReadOnlyList<int> tags = null,
            IReadOnlyList<int> statuses = null,
            GameplayTargetClassificationComponent classification = default)
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Component target candidate entity id must be valid.", nameof(entityId));

            EntityId = entityId;
            TeamId = teamId;
            LifecycleState = lifecycleState;
            Classification = classification;
            _tags = CopySortedUnique(tags);
            _statuses = CopySortedUnique(statuses);
        }

        public GameplayEntityId EntityId { get; }
        public int TeamId { get; }
        public GameplayLifecycleState LifecycleState { get; }
        public GameplayTargetClassificationComponent Classification { get; }
        public IReadOnlyList<int> Tags => _tags ?? Array.Empty<int>();
        public IReadOnlyList<int> Statuses => _statuses ?? Array.Empty<int>();
        public bool IsAlive => LifecycleState == GameplayLifecycleState.Alive;

        public bool HasTag(int tagId)
        {
            return Contains(_tags, tagId);
        }

        public bool HasStatus(int statusId)
        {
            return Contains(_statuses, statusId);
        }

        private static bool Contains(int[] ids, int id)
        {
            return id > 0 && ids != null && Array.BinarySearch(ids, id) >= 0;
        }

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
