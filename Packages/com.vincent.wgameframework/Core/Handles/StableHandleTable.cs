using System;
using System.Collections.Generic;

namespace MxFramework.Core.Handles
{
    public sealed class StableHandleTable<T>
    {
        private const int InvalidIndex = -1;
        private const int InitialGeneration = 1;

        private readonly List<Slot> _slots;
        private int _freeHead;
        private int _activeCount;
        private int _freeCount;

        public int Capacity => _slots.Count;
        public int ActiveCount => _activeCount;
        public int FreeCount => _freeCount;

        public StableHandleTable(int initialCapacity = 0)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), initialCapacity, "Initial capacity cannot be negative.");

            _slots = initialCapacity > 0 ? new List<Slot>(initialCapacity) : new List<Slot>();
            _freeHead = InvalidIndex;
        }

        public StableHandle Add(T value)
        {
            int index;
            Slot slot;

            if (_freeHead != InvalidIndex)
            {
                index = _freeHead;
                slot = _slots[index];
                _freeHead = slot.NextFree;
                _freeCount--;
            }
            else
            {
                index = _slots.Count;
                slot = new Slot { Generation = InitialGeneration, NextFree = InvalidIndex };
                _slots.Add(slot);
            }

            slot.Value = value;
            slot.Active = true;
            slot.NextFree = InvalidIndex;
            _slots[index] = slot;
            _activeCount++;

            return new StableHandle(index, slot.Generation);
        }

        public bool TryGet(StableHandle handle, out T value)
        {
            if (!IsCurrent(handle, out Slot slot))
            {
                value = default;
                return false;
            }

            value = slot.Value;
            return true;
        }

        public bool Remove(StableHandle handle)
        {
            if (!IsCurrent(handle, out Slot slot))
                return false;

            if (slot.Generation == int.MaxValue)
                throw new InvalidOperationException("StableHandle generation overflow.");

            slot.Value = default;
            slot.Active = false;
            slot.Generation++;
            slot.NextFree = _freeHead;
            _slots[handle.Index] = slot;

            _freeHead = handle.Index;
            _activeCount--;
            _freeCount++;
            return true;
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];
                if (slot.Active && slot.Generation == int.MaxValue)
                    throw new InvalidOperationException("StableHandle generation overflow.");
            }

            _freeHead = InvalidIndex;
            _activeCount = 0;
            _freeCount = _slots.Count;

            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];
                slot.Value = default;
                if (slot.Active)
                    slot.Generation++;
                slot.Active = false;
                slot.NextFree = _freeHead;
                _slots[i] = slot;
                _freeHead = i;
            }
        }

        public StableHandleTableSnapshot GetSnapshot()
        {
            return new StableHandleTableSnapshot(Capacity, ActiveCount, FreeCount);
        }

        private bool IsCurrent(StableHandle handle, out Slot slot)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _slots.Count)
            {
                slot = default;
                return false;
            }

            slot = _slots[handle.Index];
            return slot.Active && slot.Generation == handle.Generation;
        }

        private struct Slot
        {
            public T Value;
            public int Generation;
            public int NextFree;
            public bool Active;
        }
    }

    public readonly struct StableHandleTableSnapshot : IEquatable<StableHandleTableSnapshot>
    {
        public int Capacity { get; }
        public int ActiveCount { get; }
        public int FreeCount { get; }

        public StableHandleTableSnapshot(int capacity, int activeCount, int freeCount)
        {
            Capacity = capacity;
            ActiveCount = activeCount;
            FreeCount = freeCount;
        }

        public bool Equals(StableHandleTableSnapshot other)
        {
            return Capacity == other.Capacity
                && ActiveCount == other.ActiveCount
                && FreeCount == other.FreeCount;
        }

        public override bool Equals(object obj) => obj is StableHandleTableSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Capacity;
                hashCode = (hashCode * 397) ^ ActiveCount;
                hashCode = (hashCode * 397) ^ FreeCount;
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"StableHandleTableSnapshot(Capacity={Capacity}, ActiveCount={ActiveCount}, FreeCount={FreeCount})";
        }

        public static bool operator ==(StableHandleTableSnapshot left, StableHandleTableSnapshot right) => left.Equals(right);

        public static bool operator !=(StableHandleTableSnapshot left, StableHandleTableSnapshot right) => !left.Equals(right);
    }
}
