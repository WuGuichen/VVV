// Source: WGame/Client/Assets/Scripts/Common/IHeapItem.cs + MyHeap.cs
// Migrated: 2026-05-05 — Batch 1: Core utilities

using System;
using System.Collections.Generic;

namespace MxFramework.Core.Collections
{
    public interface IHeapItem<T> : System.IComparable<T>
    {
        int HeapIndex { get; set; }
    }

    public class Heap<T> where T : class, IHeapItem<T>
    {
        public int Count { get; private set; }
        public int Capacity { get; private set; }
        public T Top
        {
            get
            {
                if (IsEmpty)
                    throw new InvalidOperationException("Cannot read Top from an empty heap.");
                return _heap[0];
            }
        }
        public bool IsEmpty => Count == 0;
        public bool IsFull => Count >= Capacity;

        private readonly bool _isMaxHeap;
        private readonly T[] _heap;

        public Heap(int capacity, bool isMaxHeap = false)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Heap capacity must be positive.");

            Count = 0;
            Capacity = capacity;
            _heap = new T[capacity];
            _isMaxHeap = isMaxHeap;
        }

        public T this[int index] => _heap[index];

        public void Push(T value)
        {
            if (Count < Capacity)
            {
                value.HeapIndex = Count;
                _heap[Count] = value;
                Swim(Count);
                ++Count;
            }
        }

        public void Pop()
        {
            if (Count <= 0) return;

            T removed = _heap[0];
            removed.HeapIndex = -1;

            Count--;
            if (Count == 0)
            {
                _heap[0] = null;
                return;
            }

            _heap[0] = _heap[Count];
            _heap[Count] = null;
            _heap[0].HeapIndex = 0;
            Sink(0);
        }

        public bool Contains(T value)
        {
            if (value == null) return false;

            int index = value.HeapIndex;
            return index >= 0
                && index < Count
                && EqualityComparer<T>.Default.Equals(_heap[index], value);
        }

        public void Clear()
        {
            for (int i = 0; i < Count; ++i)
            {
                _heap[i].HeapIndex = -1;
                _heap[i] = null;
            }
            Count = 0;
        }

        private void Swap(T a, T b)
        {
            _heap[a.HeapIndex] = b;
            _heap[b.HeapIndex] = a;
            (b.HeapIndex, a.HeapIndex) = (a.HeapIndex, b.HeapIndex);
        }

        private void Swim(int index)
        {
            while (index > 0)
            {
                int father = (index - 1) >> 1;
                if (IsBetter(_heap[index], _heap[father]))
                {
                    Swap(_heap[father], _heap[index]);
                    index = father;
                }
                else return;
            }
        }

        private void Sink(int index)
        {
            int left = (index << 1) + 1;
            while (left < Count)
            {
                int largest = left + 1 < Count && IsBetter(_heap[left + 1], _heap[left]) ? left + 1 : left;
                if (!IsBetter(_heap[largest], _heap[index]))
                    largest = index;

                if (largest == index) return;
                Swap(_heap[largest], _heap[index]);
                index = largest;
                left = (index << 1) + 1;
            }
        }

        private bool IsBetter(T a, T b) => _isMaxHeap ? b.CompareTo(a) < 0 : a.CompareTo(b) < 0;
    }
}
