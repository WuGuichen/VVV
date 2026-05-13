using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Stores a deterministic sorted set of component-native buff entries.
    /// </summary>
    public readonly struct GameplayComponentBuffSetComponent : IGameplayComponent, IEquatable<GameplayComponentBuffSetComponent>
    {
        private readonly GameplayComponentBuffEntry[] _entries;

        /// <summary>
        /// Creates a buff set from entries sorted by buff id.
        /// </summary>
        /// <param name="entries">Entries to copy into the set.</param>
        public GameplayComponentBuffSetComponent(params GameplayComponentBuffEntry[] entries)
        {
            _entries = CopySorted(entries);
        }

        /// <summary>
        /// Gets the number of buff entries in the set.
        /// </summary>
        public int Count => _entries == null ? 0 : _entries.Length;

        /// <summary>
        /// Tries to get a buff entry by buff id.
        /// </summary>
        /// <param name="buffId">The buff id to find.</param>
        /// <param name="entry">The found entry, or default when not found.</param>
        /// <returns><c>true</c> when the buff id exists in the set.</returns>
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

        /// <summary>
        /// Adds or replaces a buff entry by buff id.
        /// </summary>
        /// <param name="entry">The buff entry to add or replace.</param>
        /// <returns>A new sorted buff set with the entry applied.</returns>
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

        /// <summary>
        /// Removes a buff entry by buff id.
        /// </summary>
        /// <param name="buffId">The buff id to remove.</param>
        /// <returns>A new buff set without the entry, or this set when the id is not present.</returns>
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

        /// <summary>
        /// Removes all buffs that have expired at the supplied frame.
        /// </summary>
        /// <param name="frame">The runtime frame used for expiration checks.</param>
        /// <param name="removedBuffIds">Receives the ids removed from the set.</param>
        /// <returns>A new buff set without expired entries, or this set when nothing expired.</returns>
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

        /// <summary>
        /// Copies the sorted buff entries to a new array.
        /// </summary>
        /// <returns>A new array containing the current entries.</returns>
        public GameplayComponentBuffEntry[] ToArray()
        {
            if (_entries == null || _entries.Length == 0)
                return Array.Empty<GameplayComponentBuffEntry>();

            var copy = new GameplayComponentBuffEntry[_entries.Length];
            Array.Copy(_entries, copy, _entries.Length);
            return copy;
        }

        /// <summary>
        /// Compares this set with another set.
        /// </summary>
        /// <param name="other">The set to compare.</param>
        /// <returns><c>true</c> when both sets contain equal entries in the same order.</returns>
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

        /// <summary>
        /// Compares this set with another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns><c>true</c> when the object is an equal buff set.</returns>
        public override bool Equals(object obj)
        {
            return obj is GameplayComponentBuffSetComponent other && Equals(other);
        }

        /// <summary>
        /// Returns a hash code for this set.
        /// </summary>
        /// <returns>A hash code built from the ordered entries.</returns>
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
