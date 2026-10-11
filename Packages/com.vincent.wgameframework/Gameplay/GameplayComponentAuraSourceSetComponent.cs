using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Stores a deterministic sorted set of component-native aura source entries.
    /// </summary>
    public readonly struct GameplayComponentAuraSourceSetComponent : IGameplayComponent, IEquatable<GameplayComponentAuraSourceSetComponent>
    {
        private readonly GameplayComponentAuraSourceEntry[] _entries;

        /// <summary>
        /// Creates an aura source set from entries sorted by aura id.
        /// </summary>
        /// <param name="entries">Entries to copy into the set.</param>
        public GameplayComponentAuraSourceSetComponent(params GameplayComponentAuraSourceEntry[] entries)
        {
            _entries = CopySorted(entries);
        }

        /// <summary>
        /// Gets the number of aura source entries in the set.
        /// </summary>
        public int Count => _entries == null ? 0 : _entries.Length;

        /// <summary>
        /// Tries to get an aura source entry by aura id.
        /// </summary>
        /// <param name="auraId">The aura id to find.</param>
        /// <param name="entry">The found entry, or default when not found.</param>
        /// <returns><c>true</c> when the aura id exists in the set.</returns>
        public bool TryGet(int auraId, out GameplayComponentAuraSourceEntry entry)
        {
            int index = FindIndex(auraId);
            if (index >= 0)
            {
                entry = _entries[index];
                return true;
            }

            entry = default;
            return false;
        }

        /// <summary>
        /// Gets an aura source entry by sorted index without copying the backing array.
        /// </summary>
        /// <param name="index">The sorted entry index.</param>
        /// <returns>The aura source entry at the index.</returns>
        public GameplayComponentAuraSourceEntry GetAt(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _entries[index];
        }

        /// <summary>
        /// Adds or replaces an aura source entry by aura id.
        /// </summary>
        /// <param name="entry">The aura source entry to add or replace.</param>
        /// <returns>A new sorted aura source set with the entry applied.</returns>
        public GameplayComponentAuraSourceSetComponent Upsert(GameplayComponentAuraSourceEntry entry)
        {
            int count = Count;
            if (count == 0)
                return new GameplayComponentAuraSourceSetComponent(entry);

            var entries = new GameplayComponentAuraSourceEntry[count + 1];
            int write = 0;
            bool inserted = false;
            for (int i = 0; i < count; i++)
            {
                GameplayComponentAuraSourceEntry current = _entries[i];
                if (current.AuraId == entry.AuraId)
                {
                    entries[write++] = entry;
                    inserted = true;
                    continue;
                }

                if (!inserted && entry.AuraId < current.AuraId)
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

            return new GameplayComponentAuraSourceSetComponent(entries);
        }

        /// <summary>
        /// Removes an aura source entry by aura id.
        /// </summary>
        /// <param name="auraId">The aura id to remove.</param>
        /// <returns>A new aura source set without the entry, or this set when the id is not present.</returns>
        public GameplayComponentAuraSourceSetComponent Remove(int auraId)
        {
            int count = Count;
            if (count == 0)
                return this;

            var entries = new GameplayComponentAuraSourceEntry[count];
            int write = 0;
            for (int i = 0; i < count; i++)
            {
                if (_entries[i].AuraId != auraId)
                    entries[write++] = _entries[i];
            }

            if (write == count)
                return this;
            if (write == 0)
                return default;

            Array.Resize(ref entries, write);
            return new GameplayComponentAuraSourceSetComponent(entries);
        }

        /// <summary>
        /// Removes all aura sources that have expired at the supplied frame.
        /// </summary>
        /// <param name="frame">The runtime frame used for expiration checks.</param>
        /// <param name="removedAuraIds">Receives the ids removed from the set.</param>
        /// <returns>A new aura source set without expired entries, or this set when nothing expired.</returns>
        public GameplayComponentAuraSourceSetComponent RemoveExpired(RuntimeFrame frame, out int[] removedAuraIds)
        {
            int count = Count;
            if (count == 0)
            {
                removedAuraIds = Array.Empty<int>();
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
                removedAuraIds = Array.Empty<int>();
                return this;
            }

            removedAuraIds = new int[removed];
            var entries = new GameplayComponentAuraSourceEntry[kept];
            int writeEntry = 0;
            int writeRemoved = 0;
            for (int i = 0; i < count; i++)
            {
                if (_entries[i].IsExpired(frame))
                    removedAuraIds[writeRemoved++] = _entries[i].AuraId;
                else
                    entries[writeEntry++] = _entries[i];
            }

            return kept == 0 ? default : new GameplayComponentAuraSourceSetComponent(entries);
        }

        /// <summary>
        /// Copies the sorted aura source entries to a new array.
        /// </summary>
        /// <returns>A new array containing the current entries.</returns>
        public GameplayComponentAuraSourceEntry[] ToArray()
        {
            if (_entries == null || _entries.Length == 0)
                return Array.Empty<GameplayComponentAuraSourceEntry>();

            var copy = new GameplayComponentAuraSourceEntry[_entries.Length];
            Array.Copy(_entries, copy, _entries.Length);
            return copy;
        }

        /// <summary>
        /// Compares this set with another set.
        /// </summary>
        /// <param name="other">The set to compare.</param>
        /// <returns><c>true</c> when both sets contain equal entries in the same order.</returns>
        public bool Equals(GameplayComponentAuraSourceSetComponent other)
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
        /// <returns><c>true</c> when the object is an equal aura source set.</returns>
        public override bool Equals(object obj)
        {
            return obj is GameplayComponentAuraSourceSetComponent other && Equals(other);
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

        private int FindIndex(int auraId)
        {
            if (auraId <= 0 || _entries == null)
                return -1;

            int left = 0;
            int right = _entries.Length - 1;
            while (left <= right)
            {
                int mid = left + ((right - left) / 2);
                int id = _entries[mid].AuraId;
                if (id == auraId)
                    return mid;
                if (id < auraId)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            return -1;
        }

        private static GameplayComponentAuraSourceEntry[] CopySorted(GameplayComponentAuraSourceEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
                return Array.Empty<GameplayComponentAuraSourceEntry>();

            var copy = new GameplayComponentAuraSourceEntry[entries.Length];
            Array.Copy(entries, copy, entries.Length);
            Array.Sort(copy, CompareEntries);
            for (int i = 1; i < copy.Length; i++)
            {
                if (copy[i - 1].AuraId == copy[i].AuraId)
                    throw new ArgumentException("Gameplay component aura source set cannot contain duplicate aura ids.", nameof(entries));
            }

            return copy;
        }

        private static int CompareEntries(GameplayComponentAuraSourceEntry left, GameplayComponentAuraSourceEntry right)
        {
            return left.AuraId.CompareTo(right.AuraId);
        }
    }
}
