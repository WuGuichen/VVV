using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Stores a deterministic sorted set of component-native buff entries.
    /// </summary>
    public readonly struct GameplayComponentBuffSetComponent : IGameplayComponent, IEquatable<GameplayComponentBuffSetComponent>
    {
        private readonly GameplayComponentBuffEntry _singleEntry;
        private readonly GameplayComponentBuffEntry[] _entries;
        private readonly int _count;

        /// <summary>
        /// Creates a buff set from entries sorted by buff id.
        /// </summary>
        /// <param name="entries">Entries to copy into the set.</param>
        /// <remarks>
        /// AllocByDesign for sets containing multiple entries. Empty and single-entry sets are stored inline.
        /// </remarks>
        public GameplayComponentBuffSetComponent(params GameplayComponentBuffEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                _singleEntry = default;
                _entries = null;
                _count = 0;
                return;
            }

            if (entries.Length == 1)
            {
                _singleEntry = entries[0];
                _entries = null;
                _count = 1;
                return;
            }

            _singleEntry = default;
            _entries = CopySorted(entries);
            _count = _entries.Length;
        }

        private GameplayComponentBuffSetComponent(GameplayComponentBuffEntry singleEntry)
        {
            _singleEntry = singleEntry;
            _entries = null;
            _count = 1;
        }

        private GameplayComponentBuffSetComponent(
            GameplayComponentBuffEntry[] sortedEntries,
            int count)
        {
            _singleEntry = default;
            _entries = sortedEntries;
            _count = count;
        }

        /// <summary>
        /// Gets the number of buff entries in the set.
        /// </summary>
        public int Count => _count;

        internal bool StoresSingleEntryInline => _count == 1 && _entries == null;

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
                entry = GetEntryAt(index);
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
        /// <remarks>
        /// NoAlloc for empty and single-entry sets. Larger sets create one replacement array.
        /// </remarks>
        public GameplayComponentBuffSetComponent Upsert(GameplayComponentBuffEntry entry)
        {
            int count = Count;
            if (count == 0)
                return new GameplayComponentBuffSetComponent(entry);

            int existingIndex = FindIndex(entry.BuffId);
            if (count == 1)
            {
                if (existingIndex == 0)
                    return new GameplayComponentBuffSetComponent(entry);

                var pair = new GameplayComponentBuffEntry[2];
                if (entry.BuffId < _singleEntry.BuffId)
                {
                    pair[0] = entry;
                    pair[1] = _singleEntry;
                }
                else
                {
                    pair[0] = _singleEntry;
                    pair[1] = entry;
                }

                return FromOwnedSorted(pair);
            }

            if (existingIndex >= 0)
            {
                var replaced = new GameplayComponentBuffEntry[count];
                Array.Copy(_entries, replaced, count);
                replaced[existingIndex] = entry;
                return FromOwnedSorted(replaced);
            }

            var entries = new GameplayComponentBuffEntry[count + 1];
            int insertIndex = 0;
            while (insertIndex < count && _entries[insertIndex].BuffId < entry.BuffId)
                insertIndex++;
            if (insertIndex > 0)
                Array.Copy(_entries, 0, entries, 0, insertIndex);
            entries[insertIndex] = entry;
            if (insertIndex < count)
                Array.Copy(_entries, insertIndex, entries, insertIndex + 1, count - insertIndex);
            return FromOwnedSorted(entries);
        }

        /// <summary>
        /// Removes a buff entry by buff id.
        /// </summary>
        /// <param name="buffId">The buff id to remove.</param>
        /// <returns>A new buff set without the entry, or this set when the id is not present.</returns>
        /// <remarks>
        /// NoAlloc when removing from an empty, single-entry, or two-entry set. Larger changed sets create
        /// one replacement array.
        /// </remarks>
        public GameplayComponentBuffSetComponent Remove(int buffId)
        {
            int count = Count;
            if (count == 0)
                return this;

            int removeIndex = FindIndex(buffId);
            if (removeIndex < 0)
                return this;
            if (count == 1)
                return default;
            if (count == 2)
                return new GameplayComponentBuffSetComponent(_entries[removeIndex == 0 ? 1 : 0]);

            var entries = new GameplayComponentBuffEntry[count - 1];
            if (removeIndex > 0)
                Array.Copy(_entries, 0, entries, 0, removeIndex);
            if (removeIndex < count - 1)
                Array.Copy(_entries, removeIndex + 1, entries, removeIndex, count - removeIndex - 1);
            return FromOwnedSorted(entries);
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
                if (GetEntryAt(i).IsExpired(frame))
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
            GameplayComponentBuffEntry singleEntry = default;
            GameplayComponentBuffEntry[] entries =
                kept > 1 ? new GameplayComponentBuffEntry[kept] : null;
            int writeEntry = 0;
            int writeRemoved = 0;
            for (int i = 0; i < count; i++)
            {
                GameplayComponentBuffEntry entry = GetEntryAt(i);
                if (entry.IsExpired(frame))
                {
                    removedBuffIds[writeRemoved++] = entry.BuffId;
                }
                else if (kept == 1)
                {
                    singleEntry = entry;
                }
                else
                {
                    entries[writeEntry++] = entry;
                }
            }

            if (kept == 0)
                return default;
            return kept == 1
                ? new GameplayComponentBuffSetComponent(singleEntry)
                : FromOwnedSorted(entries);
        }

        /// <summary>
        /// Copies the sorted buff entries to a new array.
        /// </summary>
        /// <returns>A new array containing the current entries.</returns>
        public GameplayComponentBuffEntry[] ToArray()
        {
            int count = Count;
            if (count == 0)
                return Array.Empty<GameplayComponentBuffEntry>();
            if (count == 1)
                return new[] { _singleEntry };

            var copy = new GameplayComponentBuffEntry[count];
            Array.Copy(_entries, copy, count);
            return copy;
        }

        public int CopyTo(List<GameplayComponentBuffEntry> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int count = Count;
            for (int i = 0; i < count; i++)
                output.Add(GetEntryAt(i));
            return count;
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
                if (!GetEntryAt(i).Equals(other.GetEntryAt(i)))
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
                int count = Count;
                int hash = count;
                for (int i = 0; i < count; i++)
                    hash = (hash * 397) ^ GetEntryAt(i).GetHashCode();
                return hash;
            }
        }

        private int FindIndex(int buffId)
        {
            int count = Count;
            if (buffId <= 0 || count == 0)
                return -1;
            if (count == 1)
                return _singleEntry.BuffId == buffId ? 0 : -1;

            int left = 0;
            int right = count - 1;
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

        private GameplayComponentBuffEntry GetEntryAt(int index)
        {
            return _count == 1 ? _singleEntry : _entries[index];
        }

        private static GameplayComponentBuffSetComponent FromOwnedSorted(
            GameplayComponentBuffEntry[] sortedEntries)
        {
            return sortedEntries.Length == 1
                ? new GameplayComponentBuffSetComponent(sortedEntries[0])
                : new GameplayComponentBuffSetComponent(sortedEntries, sortedEntries.Length);
        }

        private static GameplayComponentBuffEntry[] CopySorted(GameplayComponentBuffEntry[] entries)
        {
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
