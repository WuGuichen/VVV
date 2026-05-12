using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayAbilityCooldownComponent : IGameplayComponent, IEquatable<GameplayAbilityCooldownComponent>
    {
        private readonly GameplayAbilityCooldownEntry[] _entries;

        public GameplayAbilityCooldownComponent(params GameplayAbilityCooldownEntry[] entries)
        {
            _entries = CopySorted(entries);
        }

        public int Count => _entries == null ? 0 : _entries.Length;

        public bool TryGetEndFrame(int abilityId, out long endFrame)
        {
            int index = FindIndex(abilityId);
            if (index >= 0)
            {
                endFrame = _entries[index].EndFrame;
                return true;
            }

            endFrame = 0L;
            return false;
        }

        public long GetRemainingFrames(int abilityId, RuntimeFrame frame)
        {
            return TryGetEndFrame(abilityId, out long endFrame)
                ? Math.Max(0L, endFrame - frame.Value)
                : 0L;
        }

        public GameplayAbilityCooldownComponent Start(
            int abilityId,
            RuntimeFrame frame,
            long durationFrames)
        {
            if (abilityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(abilityId), "Gameplay ability cooldown ability id must be greater than zero.");
            if (durationFrames < 0L)
                throw new ArgumentOutOfRangeException(nameof(durationFrames), "Gameplay ability cooldown duration cannot be negative.");

            if (durationFrames == 0L)
                return Remove(abilityId);

            long endFrame = checked(frame.Value + durationFrames);
            return Upsert(new GameplayAbilityCooldownEntry(abilityId, endFrame));
        }

        public GameplayAbilityCooldownComponent RemoveExpired(RuntimeFrame frame)
        {
            int count = Count;
            if (count == 0)
                return this;

            int kept = 0;
            for (int i = 0; i < count; i++)
            {
                if (_entries[i].EndFrame > frame.Value)
                    kept++;
            }

            if (kept == count)
                return this;
            if (kept == 0)
                return default;

            var entries = new GameplayAbilityCooldownEntry[kept];
            int write = 0;
            for (int i = 0; i < count; i++)
            {
                if (_entries[i].EndFrame > frame.Value)
                    entries[write++] = _entries[i];
            }

            return new GameplayAbilityCooldownComponent(entries);
        }

        public GameplayAbilityCooldownEntry[] ToArray()
        {
            if (_entries == null || _entries.Length == 0)
                return Array.Empty<GameplayAbilityCooldownEntry>();

            var copy = new GameplayAbilityCooldownEntry[_entries.Length];
            Array.Copy(_entries, copy, _entries.Length);
            return copy;
        }

        public bool Equals(GameplayAbilityCooldownComponent other)
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
            return obj is GameplayAbilityCooldownComponent other && Equals(other);
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

        private GameplayAbilityCooldownComponent Remove(int abilityId)
        {
            int count = Count;
            if (count == 0)
                return this;

            var entries = new GameplayAbilityCooldownEntry[count];
            int write = 0;
            for (int i = 0; i < count; i++)
            {
                if (_entries[i].AbilityId != abilityId)
                    entries[write++] = _entries[i];
            }

            if (write == count)
                return this;
            if (write == 0)
                return default;

            Array.Resize(ref entries, write);
            return new GameplayAbilityCooldownComponent(entries);
        }

        private GameplayAbilityCooldownComponent Upsert(GameplayAbilityCooldownEntry entry)
        {
            int count = Count;
            if (count == 0)
                return new GameplayAbilityCooldownComponent(entry);

            var entries = new GameplayAbilityCooldownEntry[count + 1];
            int write = 0;
            bool inserted = false;
            for (int i = 0; i < count; i++)
            {
                GameplayAbilityCooldownEntry current = _entries[i];
                if (current.AbilityId == entry.AbilityId)
                {
                    entries[write++] = entry;
                    inserted = true;
                    continue;
                }

                if (!inserted && entry.AbilityId < current.AbilityId)
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

            return new GameplayAbilityCooldownComponent(entries);
        }

        private int FindIndex(int abilityId)
        {
            if (abilityId <= 0 || _entries == null)
                return -1;

            int left = 0;
            int right = _entries.Length - 1;
            while (left <= right)
            {
                int mid = left + ((right - left) / 2);
                int id = _entries[mid].AbilityId;
                if (id == abilityId)
                    return mid;
                if (id < abilityId)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            return -1;
        }

        private static GameplayAbilityCooldownEntry[] CopySorted(GameplayAbilityCooldownEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
                return Array.Empty<GameplayAbilityCooldownEntry>();

            var copy = new GameplayAbilityCooldownEntry[entries.Length];
            Array.Copy(entries, copy, entries.Length);
            Array.Sort(copy, CompareEntries);
            for (int i = 0; i < copy.Length; i++)
            {
                if (i > 0 && copy[i - 1].AbilityId == copy[i].AbilityId)
                    throw new ArgumentException("Gameplay ability cooldown cannot contain duplicate ability ids.", nameof(entries));
            }

            return copy;
        }

        private static int CompareEntries(GameplayAbilityCooldownEntry left, GameplayAbilityCooldownEntry right)
        {
            return left.AbilityId.CompareTo(right.AbilityId);
        }
    }
}
