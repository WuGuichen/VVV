using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public sealed class VersionedValue<T>
    {
        private readonly IEqualityComparer<T> _comparer;
        private T _value;
        private int _version;

        public VersionedValue()
            : this(default(T), null)
        {
        }

        public VersionedValue(T value)
            : this(value, null)
        {
        }

        public VersionedValue(IEqualityComparer<T> comparer)
            : this(default(T), comparer)
        {
        }

        public VersionedValue(T value, IEqualityComparer<T> comparer)
        {
            _value = value;
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public T Value => _value;

        public int Version => _version;

        public bool Set(T value)
        {
            if (_comparer.Equals(_value, value))
            {
                return false;
            }

            _value = value;
            _version++;
            return true;
        }
    }
}
