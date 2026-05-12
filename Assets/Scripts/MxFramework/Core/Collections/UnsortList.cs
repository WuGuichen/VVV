// Source: WGame/Client/Assets/Scripts/Common/WUnsortList.cs
// Migrated: 2026-05-05 — Batch 1: Core utilities

namespace MxFramework.Core.Collections
{
    public interface IUnsortListItem
    {
        int Index { get; set; }
        bool MarkDelete { get; set; }
    }

    public class UnsortList<T> where T : class, IUnsortListItem
    {
        private int _count;
        private int _capacity;
        public int Count => _count;

        private T[] _items;
        private readonly T _default;

        private bool _needOptimize;

        public UnsortList(int initialCapacity = 4)
        {
            if (initialCapacity <= 0)
                initialCapacity = 4;

            _capacity = initialCapacity;
            _count = 0;
            _default = default;
            _items = new T[initialCapacity];
        }

        public void Replace(int index, T value)
        {
            value.Index = index;
            value.MarkDelete = false;
            _items[index] = value;
        }

        public void Add(T value)
        {
            if (_count == _capacity)
            {
                _capacity *= 2;
                var tmp = new T[_capacity];
                for (int i = 0; i < _count; i++)
                    tmp[i] = _items[i];
                _items = tmp;
            }
            value.Index = _count;
            value.MarkDelete = false;
            _count++;
            _items[value.Index] = value;
        }

        public void RemoveDelayed(T value)
        {
            if (value == null) return;

            value.MarkDelete = true;
            _needOptimize = true;
        }

        public void Remove(T value)
        {
            if (value == null || value.Index < 0 || value.Index >= _count)
                return;

            int lastIdx = _count - 1;
            if (value.Index != lastIdx)
            {
                var lastVal = _items[lastIdx];
                lastVal.Index = value.Index;
                _items[value.Index] = lastVal;
            }
            else
            {
                _items[lastIdx] = _default;
            }
            value.Index = -1;
            value.MarkDelete = false;
            _count--;
        }

        public void Optimize()
        {
            if (!_needOptimize) return;

            int writeIdx = 0;
            for (int i = 0; i < _count; i++)
            {
                if (!_items[i].MarkDelete)
                {
                    _items[i].Index = writeIdx;
                    _items[writeIdx++] = _items[i];
                }
                else
                {
                    _items[i].Index = -1;
                    _items[i].MarkDelete = false;
                }
            }

            for (int i = writeIdx; i < _count; i++)
                _items[i] = _default;

            _count = writeIdx;
            _needOptimize = false;
        }

        public T this[int index] => _items[index];

        public void Clear()
        {
            for (int i = 0; i < _count; i++)
            {
                _items[i].Index = -1;
                _items[i].MarkDelete = false;
                _items[i] = _default;
            }
            _count = 0;
        }
    }
}
