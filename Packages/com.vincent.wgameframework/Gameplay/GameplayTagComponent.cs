using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayTagComponent : IGameplayComponent
    {
        private readonly GameplayTagId[] _ids;

        public GameplayTagComponent(params GameplayTagId[] ids)
        {
            _ids = CopyValidSorted(ids);
        }

        public int Count => _ids == null ? 0 : _ids.Length;

        public bool Contains(GameplayTagId id)
        {
            return id.IsValid && Array.BinarySearch(_ids ?? Array.Empty<GameplayTagId>(), id) >= 0;
        }

        public GameplayTagId[] ToArray()
        {
            if (_ids == null || _ids.Length == 0)
                return Array.Empty<GameplayTagId>();

            var copy = new GameplayTagId[_ids.Length];
            Array.Copy(_ids, copy, _ids.Length);
            return copy;
        }

        private static GameplayTagId[] CopyValidSorted(GameplayTagId[] ids)
        {
            if (ids == null || ids.Length == 0)
                return Array.Empty<GameplayTagId>();

            var copy = new GameplayTagId[ids.Length];
            int count = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i].IsValid)
                    copy[count++] = ids[i];
            }

            if (count == 0)
                return Array.Empty<GameplayTagId>();

            Array.Resize(ref copy, count);
            Array.Sort(copy);
            return Deduplicate(copy);
        }

        private static GameplayTagId[] Deduplicate(GameplayTagId[] sorted)
        {
            int uniqueCount = 1;
            for (int i = 1; i < sorted.Length; i++)
            {
                if (!sorted[i].Equals(sorted[uniqueCount - 1]))
                    sorted[uniqueCount++] = sorted[i];
            }

            if (uniqueCount == sorted.Length)
                return sorted;

            Array.Resize(ref sorted, uniqueCount);
            return sorted;
        }
    }
}
