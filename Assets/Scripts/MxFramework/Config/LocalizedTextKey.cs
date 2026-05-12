using System;

namespace MxFramework.Config
{
    public readonly struct LocalizedTextKey : IEquatable<LocalizedTextKey>
    {
        public LocalizedTextKey(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(LocalizedTextKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is LocalizedTextKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
