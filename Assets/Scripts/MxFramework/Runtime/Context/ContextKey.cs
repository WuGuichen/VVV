using System;

namespace MxFramework.Runtime
{
    public readonly struct ContextKey<T> : IEquatable<ContextKey<T>>
    {
        public ContextKey(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Context key id cannot be null, empty, or whitespace.", nameof(id));
            }

            Id = id;
        }

        public string Id { get; }

        public Type ValueType => typeof(T);

        public bool Equals(ContextKey<T> other)
        {
            return string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ContextKey<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Id != null ? StringComparer.Ordinal.GetHashCode(Id) : 0) * 397) ^ typeof(T).GetHashCode();
            }
        }

        public static bool operator ==(ContextKey<T> left, ContextKey<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContextKey<T> left, ContextKey<T> right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return Id ?? string.Empty;
        }
    }
}
