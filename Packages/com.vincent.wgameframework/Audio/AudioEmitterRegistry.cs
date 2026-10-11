using System;

namespace MxFramework.Audio
{
    /// <summary>Fixed-capacity noEngine registry of cached emitter transforms.</summary>
    public sealed class AudioEmitterRegistry
    {
        private readonly Entry[] _entries;
        private int _count;

        /// <remarks>AllocByDesign during initialization; all registry operations are NoAlloc.</remarks>
        public AudioEmitterRegistry(int capacity = 128)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _entries = new Entry[capacity];
        }

        public int Count => _count;
        public int Capacity => _entries.Length;

        public bool Register(int emitterId, in AudioTransform transform)
        {
            if (emitterId <= 0 || Find(emitterId) >= 0 || _count == _entries.Length)
                return false;
            _entries[_count++] = new Entry(emitterId, transform);
            return true;
        }

        /// <remarks>NoAlloc.</remarks>
        public bool Update(int emitterId, in AudioTransform transform)
        {
            int index = Find(emitterId);
            if (index < 0)
                return false;
            _entries[index] = new Entry(emitterId, transform);
            return true;
        }

        public bool Unregister(int emitterId)
        {
            int index = Find(emitterId);
            if (index < 0)
                return false;
            int last = --_count;
            if (index != last)
                _entries[index] = _entries[last];
            _entries[last] = default;
            return true;
        }

        public bool TryGet(int emitterId, out AudioTransform transform)
        {
            int index = Find(emitterId);
            if (index >= 0)
            {
                transform = _entries[index].Transform;
                return true;
            }
            transform = default;
            return false;
        }

        private int Find(int emitterId)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_entries[i].EmitterId == emitterId)
                    return i;
            }
            return -1;
        }

        private readonly struct Entry
        {
            public Entry(int emitterId, in AudioTransform transform)
            {
                EmitterId = emitterId;
                Transform = transform;
            }

            public int EmitterId { get; }
            public AudioTransform Transform { get; }
        }
    }
}
