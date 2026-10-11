using System;

namespace MxFramework.Persistence
{
    /// <summary>Defines immutable protocol limits for Persistence Core v1.</summary>
    public static class SaveGameLimits
    {
        public const int MaximumIdentifierCharacters = 64;
        public const int MaximumDescriptorUtf8Bytes = 128;
        public const int MaximumCompatibilityTags = 32;
        public const int MaximumCompatibilityTagKeyUtf8Bytes = 64;
        public const int MaximumCompatibilityTagValueUtf8Bytes = 128;
        public const int MaximumDisplayMetadataEntries = 32;
        public const int MaximumDisplayMetadataKeyUtf8Bytes = 64;
        public const int MaximumDisplayMetadataValueUtf8Bytes = 256;
        public const int MaximumHeaderBytes = 64 * 1024;
        public const int MaximumPayloadBytes = 64 * 1024 * 1024;
        public const int MaximumEnvelopeBytes = MaximumHeaderBytes + MaximumPayloadBytes + 56;
        public const int MaximumListedSlots = 256;
        public const int MaximumConcurrentOperations = 8;
        public const int MaximumJsonNesting = 16;
        public const int MaximumTransformChainLength = 0;
    }

    internal static class SaveGameIdentifier
    {
        public static bool IsValid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > SaveGameLimits.MaximumIdentifierCharacters)
            {
                return false;
            }

            if (!IsAlphaNumeric(value[0]))
            {
                return false;
            }

            for (var index = 1; index < value.Length; index++)
            {
                var character = value[index];
                if (!IsAlphaNumeric(character) && character != '.' && character != '_' && character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAlphaNumeric(char value)
        {
            return value >= '0' && value <= '9' || value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z';
        }
    }

    /// <summary>Identifies a save-game profile. Value comparison is ordinal and case-sensitive.</summary>
    public readonly struct SaveGameProfileId : IEquatable<SaveGameProfileId>, IComparable<SaveGameProfileId>
    {
        private readonly string _value;
        public SaveGameProfileId(string value)
        {
            if (!SaveGameIdentifier.IsValid(value)) throw new ArgumentException("Profile id is not a valid Persistence identifier.", nameof(value));
            _value = value;
        }
        public string Value => _value ?? string.Empty;
        public bool IsValid => SaveGameIdentifier.IsValid(_value);
        public static bool TryCreate(string value, out SaveGameProfileId id) { if (SaveGameIdentifier.IsValid(value)) { id = new SaveGameProfileId(value); return true; } id = default; return false; }
        public bool Equals(SaveGameProfileId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SaveGameProfileId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public int CompareTo(SaveGameProfileId other) => string.Compare(_value, other._value, StringComparison.Ordinal);
        public override string ToString() => Value;
        public static bool operator ==(SaveGameProfileId left, SaveGameProfileId right) => left.Equals(right);
        public static bool operator !=(SaveGameProfileId left, SaveGameProfileId right) => !left.Equals(right);
    }

    /// <summary>Identifies a save-game slot. Value comparison is ordinal and case-sensitive.</summary>
    public readonly struct SaveGameSlotId : IEquatable<SaveGameSlotId>, IComparable<SaveGameSlotId>
    {
        private readonly string _value;
        public SaveGameSlotId(string value)
        {
            if (!SaveGameIdentifier.IsValid(value)) throw new ArgumentException("Slot id is not a valid Persistence identifier.", nameof(value));
            _value = value;
        }
        public string Value => _value ?? string.Empty;
        public bool IsValid => SaveGameIdentifier.IsValid(_value);
        public static bool TryCreate(string value, out SaveGameSlotId id) { if (SaveGameIdentifier.IsValid(value)) { id = new SaveGameSlotId(value); return true; } id = default; return false; }
        public bool Equals(SaveGameSlotId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SaveGameSlotId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public int CompareTo(SaveGameSlotId other) => string.Compare(_value, other._value, StringComparison.Ordinal);
        public override string ToString() => Value;
        public static bool operator ==(SaveGameSlotId left, SaveGameSlotId right) => left.Equals(right);
        public static bool operator !=(SaveGameSlotId left, SaveGameSlotId right) => !left.Equals(right);
    }

    /// <summary>Identifies one persistence operation. Value comparison is ordinal and case-sensitive.</summary>
    public readonly struct SaveGameOperationId : IEquatable<SaveGameOperationId>
    {
        private readonly string _value;
        public SaveGameOperationId(string value)
        {
            if (!SaveGameIdentifier.IsValid(value)) throw new ArgumentException("Operation id is not a valid Persistence identifier.", nameof(value));
            _value = value;
        }
        public string Value => _value ?? string.Empty;
        public bool IsValid => SaveGameIdentifier.IsValid(_value);
        public static bool TryCreate(string value, out SaveGameOperationId id) { if (SaveGameIdentifier.IsValid(value)) { id = new SaveGameOperationId(value); return true; } id = default; return false; }
        public bool Equals(SaveGameOperationId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SaveGameOperationId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public override string ToString() => Value;
        public static bool operator ==(SaveGameOperationId left, SaveGameOperationId right) => left.Equals(right);
        public static bool operator !=(SaveGameOperationId left, SaveGameOperationId right) => !left.Equals(right);
    }

    /// <summary>Represents a monotonic published slot generation. Zero means no published generation.</summary>
    public readonly struct SaveGameGeneration : IEquatable<SaveGameGeneration>, IComparable<SaveGameGeneration>
    {
        public SaveGameGeneration(ulong value) { Value = value; }
        public ulong Value { get; }
        public bool IsPublished => Value != 0;
        public static SaveGameGeneration None => default;
        public bool TryNext(out SaveGameGeneration next)
        {
            if (Value == ulong.MaxValue) { next = default; return false; }
            next = new SaveGameGeneration(Value + 1UL); return true;
        }
        public bool Equals(SaveGameGeneration other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SaveGameGeneration other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(SaveGameGeneration other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(SaveGameGeneration left, SaveGameGeneration right) => left.Equals(right);
        public static bool operator !=(SaveGameGeneration left, SaveGameGeneration right) => !left.Equals(right);
    }
}
