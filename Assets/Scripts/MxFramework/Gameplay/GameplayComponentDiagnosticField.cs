using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayComponentDiagnosticField : IEquatable<GameplayComponentDiagnosticField>
    {
        public GameplayComponentDiagnosticField(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Gameplay component diagnostic field key cannot be null or empty.", nameof(key));

            Key = key;
            Value = value ?? string.Empty;
        }

        public string Key { get; }
        public string Value { get; }

        public bool Equals(GameplayComponentDiagnosticField other)
        {
            return string.Equals(Key, other.Key, StringComparison.Ordinal)
                && string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayComponentDiagnosticField other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Key == null ? 0 : Key.GetHashCode()) * 397) ^ (Value == null ? 0 : Value.GetHashCode());
            }
        }
    }
}
