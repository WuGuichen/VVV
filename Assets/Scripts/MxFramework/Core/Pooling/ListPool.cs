using System;
using System.Collections.Generic;

namespace MxFramework.Core.Pooling
{
    public static class ListPool<T>
    {
        private static readonly ObjectPool<List<T>> Pool = new ObjectPool<List<T>>(
            () => new List<T>(),
            onRelease: list => list.Clear());

        public static PooledList<T> Get()
        {
            return new PooledList<T>(Pool.Get());
        }

        public static PooledList<T> Get(out List<T> list)
        {
            PooledList<T> pooled = Get();
            list = pooled.List;
            return pooled;
        }

        internal static void Release(List<T> list)
        {
            Pool.Release(list);
        }
    }

    public sealed class PooledList<T> : IDisposable
    {
        private List<T> _list;

        internal PooledList(List<T> list)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
        }

        public List<T> List
        {
            get
            {
                if (_list == null)
                {
                    throw new ObjectDisposedException(nameof(PooledList<T>));
                }

                return _list;
            }
        }

        public void Dispose()
        {
            if (_list == null)
            {
                return;
            }

            List<T> list = _list;
            _list = null;
            ListPool<T>.Release(list);
        }
    }
}
