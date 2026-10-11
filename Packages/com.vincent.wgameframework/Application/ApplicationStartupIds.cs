using System;

namespace MxFramework.ApplicationStartup
{
    /// <summary>Stable plan identity. Value operations are NoAlloc.</summary>
    public readonly struct ApplicationStartupPlanId : IEquatable<ApplicationStartupPlanId>, IComparable<ApplicationStartupPlanId>
    {
        public ApplicationStartupPlanId(string value)
        {
            Value = Validate(value, nameof(value));
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public int CompareTo(ApplicationStartupPlanId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(ApplicationStartupPlanId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ApplicationStartupPlanId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(ApplicationStartupPlanId left, ApplicationStartupPlanId right) => left.Equals(right);
        public static bool operator !=(ApplicationStartupPlanId left, ApplicationStartupPlanId right) => !left.Equals(right);

        internal static string Validate(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Stable identity must be non-empty.", parameterName);
            }

            if (value.Length > 128)
            {
                throw new ArgumentOutOfRangeException(parameterName, value.Length, "Stable identity cannot exceed 128 characters.");
            }

            return value;
        }
    }

    /// <summary>Stable step identity. Value operations are NoAlloc.</summary>
    public readonly struct ApplicationStartupStepId : IEquatable<ApplicationStartupStepId>, IComparable<ApplicationStartupStepId>
    {
        public ApplicationStartupStepId(string value)
        {
            Value = ApplicationStartupPlanId.Validate(value, nameof(value));
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public int CompareTo(ApplicationStartupStepId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(ApplicationStartupStepId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ApplicationStartupStepId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(ApplicationStartupStepId left, ApplicationStartupStepId right) => left.Equals(right);
        public static bool operator !=(ApplicationStartupStepId left, ApplicationStartupStepId right) => !left.Equals(right);
    }
}
