using System;

namespace MxFramework.Core.Pooling
{
    public sealed class ReferencePool<T> where T : class, IReference, new()
    {
        private readonly ObjectPool<T> _pool;

        public ReferencePool(int defaultCapacity = 0, int maxSize = 1024)
        {
            _pool = new ObjectPool<T>(
                () => new T(),
                defaultCapacity: defaultCapacity,
                maxSize: maxSize,
                onRelease: item => item.Clear());
        }

        public int CountInactive => _pool.CountInactive;

        public int CountActive => _pool.CountActive;

        public int CountAll => _pool.CountAll;

        public T Get()
        {
            return _pool.Get();
        }

        public void Release(T item)
        {
            _pool.Release(item);
        }

        public void Prewarm(int count)
        {
            _pool.Prewarm(count);
        }

        public void Clear()
        {
            _pool.Clear();
        }
    }
}
