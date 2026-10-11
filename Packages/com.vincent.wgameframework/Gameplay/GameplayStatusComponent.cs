using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayStatusEntry : IComparable<GameplayStatusEntry>, IEquatable<GameplayStatusEntry>
    {
        public GameplayStatusEntry(GameplayStatusId statusId, int sourceBuffId = 0)
        {
            if (!statusId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(statusId), "Gameplay status entry requires a valid status id.");
            if (sourceBuffId < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceBuffId), "Gameplay status entry source buff id cannot be negative.");

            StatusId = statusId;
            SourceBuffId = sourceBuffId;
        }

        public GameplayStatusId StatusId { get; }
        public int SourceBuffId { get; }

        public int CompareTo(GameplayStatusEntry other)
        {
            int status = StatusId.CompareTo(other.StatusId);
            return status != 0 ? status : SourceBuffId.CompareTo(other.SourceBuffId);
        }

        public bool Equals(GameplayStatusEntry other)
        {
            return StatusId.Equals(other.StatusId) && SourceBuffId == other.SourceBuffId;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayStatusEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StatusId.GetHashCode() * 397) ^ SourceBuffId;
            }
        }
    }

    public readonly struct GameplayStatusComponent : IGameplayComponent
    {
        private readonly GameplayStatusEntry[] _entries;

        public GameplayStatusComponent(params GameplayStatusId[] ids)
        {
            _entries = CopyValidSorted(ids);
        }

        public GameplayStatusComponent(GameplayStatusEntry[] entries)
        {
            _entries = CopyValidSorted(entries);
        }

        public int Count => _entries == null ? 0 : _entries.Length;

        public bool Contains(GameplayStatusId id)
        {
            if (!id.IsValid || _entries == null)
                return false;

            for (int i = 0; i < _entries.Length; i++)
            {
                int compare = _entries[i].StatusId.CompareTo(id);
                if (compare == 0)
                    return true;
                if (compare > 0)
                    return false;
            }

            return false;
        }

        public GameplayStatusId[] ToArray()
        {
            if (_entries == null || _entries.Length == 0)
                return Array.Empty<GameplayStatusId>();

            var deduped = new GameplayStatusId[_entries.Length];
            int count = 0;
            for (int i = 0; i < _entries.Length; i++)
            {
                GameplayStatusId id = _entries[i].StatusId;
                if (count > 0 && deduped[count - 1].Equals(id))
                    continue;

                deduped[count++] = id;
            }

            if (count == deduped.Length)
                return deduped;

            var copy = new GameplayStatusId[count];
            Array.Copy(deduped, copy, count);
            return copy;
        }

        public GameplayStatusEntry[] ToEntries()
        {
            if (_entries == null || _entries.Length == 0)
                return Array.Empty<GameplayStatusEntry>();

            var copy = new GameplayStatusEntry[_entries.Length];
            Array.Copy(_entries, copy, _entries.Length);
            return copy;
        }

        public int CopyEntriesTo(List<GameplayStatusEntry> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (_entries == null || _entries.Length == 0)
                return 0;

            for (int i = 0; i < _entries.Length; i++)
                output.Add(_entries[i]);
            return _entries.Length;
        }

        public int CopyEntriesWithoutSourceBuffsTo(List<GameplayStatusEntry> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (_entries == null || _entries.Length == 0)
                return 0;

            int count = 0;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].SourceBuffId != 0)
                    continue;

                output.Add(_entries[i]);
                count++;
            }

            return count;
        }

        public bool ContentEquals(IReadOnlyList<GameplayStatusEntry> entries)
        {
            int count = Count;
            if (entries == null || entries.Count != count)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (!_entries[i].Equals(entries[i]))
                    return false;
            }

            return true;
        }

        private static GameplayStatusEntry[] CopyValidSorted(GameplayStatusId[] ids)
        {
            if (ids == null || ids.Length == 0)
                return Array.Empty<GameplayStatusEntry>();

            var copy = new GameplayStatusEntry[ids.Length];
            int count = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i].IsValid)
                    copy[count++] = new GameplayStatusEntry(ids[i]);
            }

            if (count == 0)
                return Array.Empty<GameplayStatusEntry>();

            Array.Resize(ref copy, count);
            Array.Sort(copy);
            return Deduplicate(copy);
        }

        private static GameplayStatusEntry[] CopyValidSorted(GameplayStatusEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
                return Array.Empty<GameplayStatusEntry>();

            var copy = new GameplayStatusEntry[entries.Length];
            int count = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].StatusId.IsValid)
                    copy[count++] = entries[i];
            }

            if (count == 0)
                return Array.Empty<GameplayStatusEntry>();

            Array.Resize(ref copy, count);
            Array.Sort(copy);
            return Deduplicate(copy);
        }

        private static GameplayStatusEntry[] Deduplicate(GameplayStatusEntry[] sorted)
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
