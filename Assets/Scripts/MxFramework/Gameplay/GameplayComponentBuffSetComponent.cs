using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayComponentBuffSetComponent : IGameplayComponent, IEquatable<GameplayComponentBuffSetComponent>
    {
        private readonly GameplayComponentBuffEntry[] _entries;

        public GameplayComponentBuffSetComponent(params GameplayComponentBuffEntry[] entries)
        {
            _entries = CopySorted(entries);
        }

        public int Count => _entries == null ? 0 : _entries.Length;

        public bool TryGet(int buffId, out GameplayComponentBuffEntry entry)
        {
            int index = FindIndex(buffId);
            if (index >= 0)
            {
                entry = _entries[index];
                return true;
            }

            entry = default;
            return false;
        }

        public GameplayComponentBuffSetComponent Upsert(GameplayComponentBuffEntry entry)
        {
            int count = Count;
            if (count == 0)
                return new GameplayComponentBuffSetComponent(entry);

            var entries = new GameplayComponentBuffEntry[count + 1];
            int write = 0;
            bool inserted = false;
            for (int i = 0; i < count; i++)
            {
                GameplayComponentBuffEntry current = _entries[i];
                if (current.BuffId == entry.BuffId)
                {
                    entries[write++] = entry;
                    inserted = true;
                    continue;
                }

                if (!inserted && entry.BuffId < current.BuffId)
                {
                    entries[write++] = entry;
                    inserted = true;
                }

                entries[write++] = current;
            }

            if (!inserted)
                entries[write++] = entry;
            if (write != entries.Length)
                Array.Resize(ref entries, write);

            return new GameplayComponentBuffSetComponent(entries);
        }

        public GameplayComponentBuffSetComponent Remove(int buffId)
        {
            int count = Count;
            if (count == 0)
                return this;

            var entries = new GameplayComponentBuffEntry[count];
            int write = 0;
            for (int i = 0; i < count; i++)
            {
                if (_entries[i].BuffId != buffId)
                    entries[write++] = _entries[i];
            }

            if (write == count)
                return this;
            if (write == 0)
                return default;

            Array.Resize(ref entries, write);
            return new GameplayComponentBuffSetComponent(entries);
        }

        public GameplayComponentBuffSetComponent RemoveExpired(RuntimeFrame frame, out int[] removedBuffIds)
        {
            int count = Count;
            if (count == 0)
            {
                removedBuffIds = Array.Empty<int>();
                return this;
            }

            int kept = 0;
            int removed = 0;
            for (int i = 0; i < count; i++)
            {
                if (_entries[i].IsExpired(frame))
                    removed++;
                else
                    kept++;
            }

            if (removed == 0)
            {
                removedBuffIds = Array.Empty<int>();
                return this;
            }

            removedBuffIds = new int[removed];
            var entries = new GameplayComponentBuffEntry[kept];
            int writeEntry = 0;
            int writeRemoved = 0;
            for (int i = 0; i < count; i++)
            {
                if (_entries[i].IsExpired(frame))
                    removedBuffIds[writeRemoved++] = _entries[i].BuffId;
                else
                    entries[writeEntry++] = _entries[i];
            }

            return kept == 0 ? default : new GameplayComponentBuffSetComponent(entries);
        }

        public GameplayComponentBuffEntry[] ToArray()
        {
            if (_entries == null || _entries.Length == 0)
                return Array.Empty<GameplayComponentBuffEntry>();

            var copy = new GameplayComponentBuffEntry[_entries.Length];
            Array.Copy(_entries, copy, _entries.Length);
            return copy;
        }

        public bool Equals(GameplayComponentBuffSetComponent other)
        {
            int count = Count;
            if (count != other.Count)
                return false;
            for (int i = 0; i < count; i++)
            {
                if (!_entries[i].Equals(other._entries[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayComponentBuffSetComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Count;
                for (int i = 0; i < Count; i++)
                    hash = (hash * 397) ^ _entries[i].GetHashCode();
                return hash;
            }
        }

        private int FindIndex(int buffId)
        {
            if (buffId <= 0 || _entries == null)
                return -1;

            int left = 0;
            int right = _entries.Length - 1;
            while (left <= right)
            {
                int mid = left + ((right - left) / 2);
                int id = _entries[mid].BuffId;
                if (id == buffId)
                    return mid;
                if (id < buffId)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            return -1;
        }

        private static GameplayComponentBuffEntry[] CopySorted(GameplayComponentBuffEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
                return Array.Empty<GameplayComponentBuffEntry>();

            var copy = new GameplayComponentBuffEntry[entries.Length];
            Array.Copy(entries, copy, entries.Length);
            Array.Sort(copy, CompareEntries);
            for (int i = 1; i < copy.Length; i++)
            {
                if (copy[i - 1].BuffId == copy[i].BuffId)
                    throw new ArgumentException("Gameplay component buff set cannot contain duplicate buff ids.", nameof(entries));
            }

            return copy;
        }

        private static int CompareEntries(GameplayComponentBuffEntry left, GameplayComponentBuffEntry right)
        {
            return left.BuffId.CompareTo(right.BuffId);
        }
    }
}
