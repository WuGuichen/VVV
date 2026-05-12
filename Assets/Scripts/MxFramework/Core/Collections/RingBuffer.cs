using System;
using System.Collections.Generic;

namespace MxFramework.Core.Collections
{
    public sealed class RingBuffer<T>
    {
        private readonly T[] _items;
        private int _start;

        public int Capacity { get; }
        public int Count { get; private set; }

        public RingBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "RingBuffer capacity must be positive.");

            Capacity = capacity;
            _items = new T[capacity];
        }

        public void Add(T item)
        {
            if (Count < Capacity)
            {
                _items[(_start + Count) % Capacity] = item;
                Count++;
                return;
            }

            _items[_start] = item;
            _start = (_start + 1) % Capacity;
        }

        public void CopyTo(List<T> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            for (int i = 0; i < Count; i++)
                output.Add(_items[(_start + i) % Capacity]);
        }

        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _start = 0;
            Count = 0;
        }
    }
}
