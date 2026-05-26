using System;

namespace MxFramework.UI
{
    public readonly struct MxUiViewId : IEquatable<MxUiViewId>
    {
        public MxUiViewId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(MxUiViewId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MxUiViewId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(MxUiViewId left, MxUiViewId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxUiViewId left, MxUiViewId right)
        {
            return !left.Equals(right);
        }
    }
}
