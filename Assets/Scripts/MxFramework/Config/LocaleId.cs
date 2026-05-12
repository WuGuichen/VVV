using System;

namespace MxFramework.Config
{
    public readonly struct LocaleId : IEquatable<LocaleId>
    {
        public LocaleId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(LocaleId other)
        {
            return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is LocaleId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static readonly LocaleId ZhCN = new LocaleId("zh-CN");
        public static readonly LocaleId EnUS = new LocaleId("en-US");
    }
}
