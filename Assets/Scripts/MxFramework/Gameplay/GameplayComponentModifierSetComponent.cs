using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Stores a deterministic sorted set of component-native additive modifier entries.
    /// </summary>
    public readonly struct GameplayComponentModifierSetComponent : IGameplayComponent, IEquatable<GameplayComponentModifierSetComponent>
    {
        private readonly GameplayComponentModifierEntry[] _entries;

        /// <summary>
        /// Creates a modifier set from entries sorted by modifier id.
        /// </summary>
        /// <param name="entries">Entries to copy into the set.</param>
        public GameplayComponentModifierSetComponent(params GameplayComponentModifierEntry[] entries)
        {
            _entries = CopySorted(entries);
        }

        /// <summary>
        /// Gets the number of modifier entries in the set.
        /// </summary>
        public int Count => _entries == null ? 0 : _entries.Length;

        /// <summary>
        /// Adds or replaces a modifier entry by modifier id.
        /// </summary>
        /// <param name="entry">The modifier entry to add or replace.</param>
        /// <returns>A new sorted modifier set with the entry applied.</returns>
        public GameplayComponentModifierSetComponent Upsert(GameplayComponentModifierEntry entry)
        {
            int count = Count;
            if (count == 0)
                return new GameplayComponentModifierSetComponent(entry);

            var entries = new GameplayComponentModifierEntry[count + 1];
            int write = 0;
            bool inserted = false;
            for (int i = 0; i < count; i++)
            {
                GameplayComponentModifierEntry current = _entries[i];
                if (current.ModifierId == entry.ModifierId)
                {
                    entries[write++] = entry;
                    inserted = true;
                    continue;
                }

                if (!inserted && entry.ModifierId < current.ModifierId)
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

            return new GameplayComponentModifierSetComponent(entries);
        }

        /// <summary>
        /// Removes modifiers whose source buff id is present in the supplied id array.
        /// </summary>
        /// <param name="buffIds">Buff ids whose linked modifiers should be removed.</param>
        /// <returns>A new modifier set without matching source buff ids, or this set when nothing matched.</returns>
        public GameplayComponentModifierSetComponent RemoveBySourceBuffIds(int[] buffIds)
        {
            if (buffIds == null || buffIds.Length == 0)
                return this;

            return RemoveBySourceBuffIds(new HashSet<int>(buffIds));
        }

        /// <summary>
        /// Removes modifiers whose source buff id is present in the supplied id set.
        /// </summary>
        /// <param name="buffIds">Buff ids whose linked modifiers should be removed.</param>
        /// <returns>A new modifier set without matching source buff ids, or this set when nothing matched.</returns>
        public GameplayComponentModifierSetComponent RemoveBySourceBuffIds(HashSet<int> buffIds)
        {
            if (Count == 0 || buffIds == null || buffIds.Count == 0)
                return this;

            var entries = new GameplayComponentModifierEntry[Count];
            int write = 0;
            for (int i = 0; i < Count; i++)
            {
                if (!buffIds.Contains(_entries[i].SourceBuffId))
                    entries[write++] = _entries[i];
            }

            if (write == Count)
                return this;
            if (write == 0)
                return default;

            Array.Resize(ref entries, write);
            return new GameplayComponentModifierSetComponent(entries);
        }

        /// <summary>
        /// Gets the total additive value for one attribute id.
        /// </summary>
        /// <param name="attributeId">The positive attribute id to evaluate.</param>
        /// <returns>The checked sum of all matching additive modifier values.</returns>
        public int GetAdditiveValue(int attributeId)
        {
            if (attributeId <= 0 || Count == 0)
                return 0;

            int total = 0;
            for (int i = 0; i < Count; i++)
            {
                if (_entries[i].AttributeId == attributeId)
                    total = checked(total + _entries[i].AddValue);
            }

            return total;
        }

        /// <summary>
        /// Copies the sorted modifier entries to a new array.
        /// </summary>
        /// <returns>A new array containing the current entries.</returns>
        public GameplayComponentModifierEntry[] ToArray()
        {
            if (_entries == null || _entries.Length == 0)
                return Array.Empty<GameplayComponentModifierEntry>();

            var copy = new GameplayComponentModifierEntry[_entries.Length];
            Array.Copy(_entries, copy, _entries.Length);
            return copy;
        }

        /// <summary>
        /// Compares this set with another set.
        /// </summary>
        /// <param name="other">The set to compare.</param>
        /// <returns><c>true</c> when both sets contain equal entries in the same order.</returns>
        public bool Equals(GameplayComponentModifierSetComponent other)
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
        /// <returns><c>true</c> when the object is an equal modifier set.</returns>
        public override bool Equals(object obj)
        {
            return obj is GameplayComponentModifierSetComponent other && Equals(other);
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

        private static GameplayComponentModifierEntry[] CopySorted(GameplayComponentModifierEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
                return Array.Empty<GameplayComponentModifierEntry>();

            var copy = new GameplayComponentModifierEntry[entries.Length];
            Array.Copy(entries, copy, entries.Length);
            Array.Sort(copy, CompareEntries);
            for (int i = 1; i < copy.Length; i++)
            {
                if (copy[i - 1].ModifierId == copy[i].ModifierId)
                    throw new ArgumentException("Gameplay component modifier set cannot contain duplicate modifier ids.", nameof(entries));
            }

            return copy;
        }

        private static int CompareEntries(GameplayComponentModifierEntry left, GameplayComponentModifierEntry right)
        {
            return left.ModifierId.CompareTo(right.ModifierId);
        }
    }
}
