using System.Collections.Generic;

namespace MxFramework.Config
{
    public sealed class ConfigEnumDomain
    {
        private readonly List<ConfigEnumValue> _values = new List<ConfigEnumValue>();

        public ConfigEnumDomain(string enumId, bool isFlags = false, string displayName = "", string description = "")
        {
            EnumId = enumId ?? string.Empty;
            IsFlags = isFlags;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public string EnumId { get; }
        public bool IsFlags { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public IReadOnlyList<ConfigEnumValue> Values => _values;
        public bool IsValid => !string.IsNullOrWhiteSpace(EnumId);

        public ConfigEnumDomain AddValue(ConfigEnumValue value)
        {
            if (value.IsValid)
                _values.Add(value);
            return this;
        }

        public bool ContainsValue(int value)
        {
            for (int i = 0; i < _values.Count; i++)
            {
                if (_values[i].Value == value)
                    return true;
            }

            return false;
        }

        public bool TryGetValue(int value, out ConfigEnumValue enumValue)
        {
            for (int i = 0; i < _values.Count; i++)
            {
                if (_values[i].Value == value)
                {
                    enumValue = _values[i];
                    return true;
                }
            }

            enumValue = default;
            return false;
        }

        public bool TryDecomposeFlags(int value, out IReadOnlyList<ConfigEnumValue> parts, out int unknownBits)
        {
            var result = new List<ConfigEnumValue>();
            unknownBits = value;

            if (!IsFlags)
            {
                if (TryGetValue(value, out ConfigEnumValue single))
                {
                    result.Add(single);
                    unknownBits = 0;
                    parts = result;
                    return true;
                }

                parts = result;
                return false;
            }

            for (int i = 0; i < _values.Count; i++)
            {
                ConfigEnumValue item = _values[i];
                if (item.Value <= 0)
                    continue;

                if ((value & item.Value) == item.Value)
                {
                    result.Add(item);
                    unknownBits &= ~item.Value;
                }
            }

            if (value == 0 && TryGetValue(0, out ConfigEnumValue zero))
                result.Add(zero);

            parts = result;
            return unknownBits == 0;
        }

        public string FormatValue(int value)
        {
            if (!IsFlags)
                return TryGetValue(value, out ConfigEnumValue enumValue) ? Format(enumValue) : value.ToString();

            if (!TryDecomposeFlags(value, out IReadOnlyList<ConfigEnumValue> parts, out int unknownBits))
                return value + " (unknownBits=" + unknownBits + ")";

            if (parts.Count == 0)
                return value.ToString();

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                    builder.Append("|");
                builder.Append(Format(parts[i]));
            }

            return builder.ToString();
        }

        private static string Format(ConfigEnumValue value)
        {
            return string.IsNullOrEmpty(value.DisplayName)
                ? value.Name + "(" + value.Value + ")"
                : value.DisplayName + " " + value.Name + "(" + value.Value + ")";
        }
    }
}
