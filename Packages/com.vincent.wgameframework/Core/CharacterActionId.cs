using System;
using System.Globalization;
using Newtonsoft.Json;

namespace MxFramework.CharacterAction
{
    /// <summary>
    /// Globally unique numeric identity for a CharacterAction definition.
    /// Zero is reserved for an unset action reference and is never a registered action.
    /// This type lives in the no-engine Core assembly so CharacterAction references can stay
    /// strongly typed without reversing the CharacterAction-to-Gameplay dependency.
    /// </summary>
    [JsonConverter(typeof(CharacterActionIdJsonConverter))]
    public readonly struct CharacterActionId :
        IEquatable<CharacterActionId>,
        IComparable<CharacterActionId>,
        IFormattable
    {
        public static readonly CharacterActionId None = default;

        public CharacterActionId(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Character action id cannot be negative.");
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;

        public int CompareTo(CharacterActionId other) => Value.CompareTo(other.Value);
        public bool Equals(CharacterActionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CharacterActionId other && Equals(other);
        public override int GetHashCode() => Value;

        public override string ToString() => IsValid
            ? "#" + Value.ToString(CultureInfo.InvariantCulture)
            : "None";

        public string ToString(string format, IFormatProvider formatProvider) =>
            Value.ToString(format, formatProvider ?? CultureInfo.InvariantCulture);

        public static bool TryCreate(int value, out CharacterActionId actionId)
        {
            if (value <= 0)
            {
                actionId = None;
                return false;
            }
            actionId = new CharacterActionId(value);
            return true;
        }

        public static bool TryParse(string value, out CharacterActionId actionId)
        {
            actionId = None;
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.StartsWith("#", StringComparison.Ordinal))
                normalized = normalized.Substring(1);
            return int.TryParse(
                    normalized,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int parsed)
                && TryCreate(parsed, out actionId);
        }

        public static bool operator ==(CharacterActionId left, CharacterActionId right) => left.Equals(right);
        public static bool operator !=(CharacterActionId left, CharacterActionId right) => !left.Equals(right);
        public static bool operator <(CharacterActionId left, CharacterActionId right) => left.Value < right.Value;
        public static bool operator >(CharacterActionId left, CharacterActionId right) => left.Value > right.Value;
        public static bool operator <=(CharacterActionId left, CharacterActionId right) => left.Value <= right.Value;
        public static bool operator >=(CharacterActionId left, CharacterActionId right) => left.Value >= right.Value;
    }

    internal sealed class CharacterActionIdJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(CharacterActionId);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value is CharacterActionId actionId ? actionId.Value : 0);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Integer)
                return new CharacterActionId(Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture));
            if (reader.TokenType == JsonToken.Null)
                return CharacterActionId.None;
            throw new JsonSerializationException("Character action id must be an integer.");
        }
    }
}
