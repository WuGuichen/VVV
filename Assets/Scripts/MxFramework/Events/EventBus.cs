using System;

namespace MxFramework.Events
{
    /// <summary>
    /// Default synchronous event bus.
    /// </summary>
    /// <remarks>
    /// Publish captures the current table length. Handlers added during publish run on the next publish.
    /// Handlers removed during publish are skipped if they have not run yet. Exceptions propagate immediately.
    /// </remarks>
    public sealed class EventBus<T> : IEventBus<T> where T : struct
    {
        private const int DefaultCapacity = 4;

        private Entry[] _entries;
        private int _count;
        private int _publishDepth;
        private bool _needsCompaction;

        public EventBus(int initialCapacity = DefaultCapacity)
        {
            if (initialCapacity <= 0)
                initialCapacity = DefaultCapacity;

            _entries = new Entry[initialCapacity];
        }

        public int Count => _count;

        public IDisposable Subscribe(Action<T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            EnsureCapacity(_count + 1);

            var subscription = new Subscription(this);
            _entries[_count++] = new Entry(handler, subscription);
            return subscription;
        }

        public bool Unsubscribe(Action<T> handler)
        {
            if (handler == null)
                return false;

            for (int i = 0; i < _count; i++)
            {
                if (_entries[i].Handler == handler)
                {
                    RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public void Publish(in T args)
        {
            int publishCount = _count;
            _publishDepth++;

            try
            {
                for (int i = 0; i < publishCount; i++)
                {
                    Action<T> handler = _entries[i].Handler;
                    if (handler != null)
                        handler(args);
                }
            }
            finally
            {
                _publishDepth--;
                if (_publishDepth == 0 && _needsCompaction)
                    Compact();
            }
        }

        private void Unsubscribe(Subscription subscription)
        {
            if (subscription == null || subscription.Bus != this)
                return;

            for (int i = 0; i < _count; i++)
            {
                if (_entries[i].Subscription == subscription)
                {
                    RemoveAt(i);
                    return;
                }
            }
        }

        private void RemoveAt(int index)
        {
            _entries[index].Subscription.Detach();

            if (_publishDepth > 0)
            {
                _entries[index] = default;
                _needsCompaction = true;
                return;
            }

            int last = _count - 1;
            if (index != last)
                Array.Copy(_entries, index + 1, _entries, index, last - index);

            _entries[last] = default;
            _count--;
        }

        private void Compact()
        {
            int write = 0;
            for (int read = 0; read < _count; read++)
            {
                if (_entries[read].Handler != null)
                    _entries[write++] = _entries[read];
            }

            for (int i = write; i < _count; i++)
                _entries[i] = default;

            _count = write;
            _needsCompaction = false;
        }

        private void EnsureCapacity(int required)
        {
            if (required <= _entries.Length)
                return;

            int capacity = _entries.Length == 0 ? DefaultCapacity : _entries.Length * 2;
            while (capacity < required)
                capacity *= 2;

            Array.Resize(ref _entries, capacity);
        }

        private readonly struct Entry
        {
            public readonly Action<T> Handler;
            public readonly Subscription Subscription;

            public Entry(Action<T> handler, Subscription subscription)
            {
                Handler = handler;
                Subscription = subscription;
            }
        }

        private sealed class Subscription : IDisposable
        {
            private EventBus<T> _bus;

            public Subscription(EventBus<T> bus)
            {
                _bus = bus;
            }

            public EventBus<T> Bus => _bus;

            public void Dispose()
            {
                EventBus<T> bus = _bus;
                if (bus == null)
                    return;

                bus.Unsubscribe(this);
            }

            public void Detach()
            {
                _bus = null;
            }
        }
    }
}
