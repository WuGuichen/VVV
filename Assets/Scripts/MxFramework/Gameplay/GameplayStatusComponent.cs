using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayStatusComponent : IGameplayComponent
    {
        private readonly GameplayStatusId[] _ids;

        public GameplayStatusComponent(params GameplayStatusId[] ids)
        {
            _ids = CopyValidSorted(ids);
        }

        public int Count => _ids == null ? 0 : _ids.Length;

        public bool Contains(GameplayStatusId id)
        {
            return id.IsValid && Array.BinarySearch(_ids ?? Array.Empty<GameplayStatusId>(), id) >= 0;
        }

        public GameplayStatusId[] ToArray()
        {
            if (_ids == null || _ids.Length == 0)
                return Array.Empty<GameplayStatusId>();

            var copy = new GameplayStatusId[_ids.Length];
            Array.Copy(_ids, copy, _ids.Length);
            return copy;
        }

        private static GameplayStatusId[] CopyValidSorted(GameplayStatusId[] ids)
        {
            if (ids == null || ids.Length == 0)
                return Array.Empty<GameplayStatusId>();

            var copy = new GameplayStatusId[ids.Length];
            int count = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i].IsValid)
                    copy[count++] = ids[i];
            }

            if (count == 0)
                return Array.Empty<GameplayStatusId>();

            Array.Resize(ref copy, count);
            Array.Sort(copy);
            return Deduplicate(copy);
        }

        private static GameplayStatusId[] Deduplicate(GameplayStatusId[] sorted)
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
