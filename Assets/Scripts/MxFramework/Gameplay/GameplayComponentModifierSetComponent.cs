using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayComponentModifierSetComponent : IGameplayComponent, IEquatable<GameplayComponentModifierSetComponent>
    {
        private readonly GameplayComponentModifierEntry[] _entries;

        public GameplayComponentModifierSetComponent(params GameplayComponentModifierEntry[] entries)
        {
            _entries = CopySorted(entries);
        }

        public int Count => _entries == null ? 0 : _entries.Length;

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

        public GameplayComponentModifierSetComponent RemoveBySourceBuffIds(int[] buffIds)
        {
            if (Count == 0 || buffIds == null || buffIds.Length == 0)
                return this;

            var entries = new GameplayComponentModifierEntry[Count];
            int write = 0;
            for (int i = 0; i < Count; i++)
            {
                if (!Contains(buffIds, _entries[i].SourceBuffId))
                    entries[write++] = _entries[i];
            }

            if (write == Count)
                return this;
            if (write == 0)
                return default;

            Array.Resize(ref entries, write);
            return new GameplayComponentModifierSetComponent(entries);
        }

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

        public GameplayComponentModifierEntry[] ToArray()
        {
            if (_entries == null || _entries.Length == 0)
                return Array.Empty<GameplayComponentModifierEntry>();

            var copy = new GameplayComponentModifierEntry[_entries.Length];
            Array.Copy(_entries, copy, _entries.Length);
            return copy;
        }

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

        public override bool Equals(object obj)
        {
            return obj is GameplayComponentModifierSetComponent other && Equals(other);
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

        private static bool Contains(int[] values, int value)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == value)
                    return true;
            }

            return false;
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
