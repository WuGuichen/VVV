using System;

namespace MxFramework.UI
{
    public readonly struct MxUiTextKey : IEquatable<MxUiTextKey>
    {
        public MxUiTextKey(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(MxUiTextKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MxUiTextKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public static bool operator ==(MxUiTextKey left, MxUiTextKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxUiTextKey left, MxUiTextKey right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public readonly struct MxUiLocaleId : IEquatable<MxUiLocaleId>
    {
        public MxUiLocaleId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(MxUiLocaleId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MxUiLocaleId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public static bool operator ==(MxUiLocaleId left, MxUiLocaleId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxUiLocaleId left, MxUiLocaleId right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public readonly struct MxUiLocalizedTextRequest
    {
        public MxUiLocalizedTextRequest(MxUiTextKey key, string fallbackText = "")
        {
            Key = key;
            FallbackText = fallbackText ?? string.Empty;
        }

        public MxUiTextKey Key { get; }
        public string FallbackText { get; }
        public bool IsValid => Key.IsValid;
    }

    public interface IMxUiTextProvider
    {
        MxUiLocaleId CurrentLocale { get; }
        long Revision { get; }
        bool TryGetText(MxUiLocalizedTextRequest request, out string text);
    }

    public sealed class MxUiNullTextProvider : IMxUiTextProvider
    {
        public static readonly MxUiNullTextProvider Instance = new MxUiNullTextProvider();

        private MxUiNullTextProvider()
        {
        }

        public MxUiLocaleId CurrentLocale => default;
        public long Revision => 0L;

        public bool TryGetText(MxUiLocalizedTextRequest request, out string text)
        {
            text = request.FallbackText ?? string.Empty;
            return !string.IsNullOrEmpty(text);
        }
    }
}
