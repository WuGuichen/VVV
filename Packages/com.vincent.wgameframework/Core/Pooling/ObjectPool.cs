using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MxFramework.Core.Pooling
{
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _create;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Stack<T> _inactive;
        private readonly Dictionary<T, ulong> _active;
        private ulong _nextLeaseId;
        private readonly int _maxSize;

        public ObjectPool(
            Func<T> create,
            Action<T> onGet = null,
            Action<T> onRelease = null,
            int defaultCapacity = 0,
            int maxSize = 1024)
        {
            if (create == null)
                throw new ArgumentNullException(nameof(create));
            if (defaultCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(defaultCapacity), "Default capacity cannot be negative.");
            if (maxSize < 0)
                throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size cannot be negative.");

            _create = create;
            _onGet = onGet;
            _onRelease = onRelease;
            _inactive = new Stack<T>(defaultCapacity);
            _active = new Dictionary<T, ulong>(ReferenceEqualityComparer<T>.Instance);
            _maxSize = maxSize;
        }

        public int CountInactive => _inactive.Count;

        public int CountActive => _active.Count;

        public int CountAll => CountInactive + CountActive;

        /// <summary>Gets an item and invokes onGet after registering it as active.</summary>
        /// <remarks>
        /// If onGet throws, only this Get's still-active lease is removed and the original exception is rethrown.
        /// The pool does not cache, release or dispose the failed lease.
        /// An explicit Release or a later lease created by a reentrant callback is preserved.
        /// </remarks>
        public T Get()
        {
            T item = _inactive.Count > 0 ? _inactive.Pop() : _create();
            if (item == null)
                throw new InvalidOperationException("Pool create function returned null.");

            if (_active.ContainsKey(item))
                throw new InvalidOperationException("Pool create function returned an item that is already active.");

            ulong leaseId = unchecked(++_nextLeaseId);
            _active.Add(item, leaseId);

            try
            {
                _onGet?.Invoke(item);
            }
            catch
            {
                if (_active.TryGetValue(item, out ulong activeLeaseId) && activeLeaseId == leaseId)
                    _active.Remove(item);
                throw;
            }

            return item;
        }

        public void Release(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (!_active.Remove(item))
                throw new InvalidOperationException("Cannot release an item that is not active in this pool.");

            _onRelease?.Invoke(item);

            if (_inactive.Count < _maxSize)
                _inactive.Push(item);
        }

        public void Prewarm(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Prewarm count cannot be negative.");
            if (count > _maxSize)
                throw new ArgumentOutOfRangeException(nameof(count), "Prewarm count cannot exceed max size.");

            while (_inactive.Count < count)
            {
                T item = _create();
                if (item == null)
                    throw new InvalidOperationException("Pool create function returned null.");

                _onRelease?.Invoke(item);
                _inactive.Push(item);
            }
        }

        public void Clear()
        {
            _inactive.Clear();
        }

        private sealed class ReferenceEqualityComparer<TItem> : IEqualityComparer<TItem>
            where TItem : class
        {
            public static readonly ReferenceEqualityComparer<TItem> Instance = new ReferenceEqualityComparer<TItem>();

            public bool Equals(TItem x, TItem y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(TItem obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
