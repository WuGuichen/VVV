using System;
using System.Globalization;

namespace MxFramework.Input
{
    /// <summary>BCL-only numerical input correlation identity; adapters copy its four integer fields directly.</summary>
    public readonly struct InputTraceIdentity : IEquatable<InputTraceIdentity>
    {
        public static readonly InputTraceIdentity None = default;
        public InputTraceIdentity(int kindId, int sourceId = 0, long executionId = 0, long sequence = 0)
        {
            if (kindId < 0) throw new ArgumentOutOfRangeException(nameof(kindId));
            if (sourceId < 0) throw new ArgumentOutOfRangeException(nameof(sourceId));
            if (executionId < 0) throw new ArgumentOutOfRangeException(nameof(executionId));
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (kindId == 0 && (sourceId != 0 || executionId != 0 || sequence != 0))
                throw new ArgumentException("An absent trace cannot contain an instance identity.", nameof(kindId));
            KindId = kindId; SourceId = sourceId; ExecutionId = executionId; Sequence = sequence;
        }

        public int KindId { get; }
        public int SourceId { get; }
        public long ExecutionId { get; }
        public long Sequence { get; }
        public bool IsValid => KindId > 0;
        public bool IsNone => KindId == 0;
        public bool Equals(InputTraceIdentity other) => KindId == other.KindId && SourceId == other.SourceId
            && ExecutionId == other.ExecutionId && Sequence == other.Sequence;
        public override bool Equals(object obj) => obj is InputTraceIdentity other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { int hash = KindId; hash = hash * 397 ^ SourceId; hash = hash * 397 ^ ExecutionId.GetHashCode(); return hash * 397 ^ Sequence.GetHashCode(); }
        }
        public static bool operator ==(InputTraceIdentity left, InputTraceIdentity right) => left.Equals(right);
        public static bool operator !=(InputTraceIdentity left, InputTraceIdentity right) => !left.Equals(right);

        /// <remarks>Diagnostics only; never use formatted text for identity or hashing.</remarks>
        public override string ToString() => IsNone ? string.Empty : string.Concat(
            KindId.ToString(CultureInfo.InvariantCulture), ":", SourceId.ToString(CultureInfo.InvariantCulture), ":",
            ExecutionId.ToString(CultureInfo.InvariantCulture), ":", Sequence.ToString(CultureInfo.InvariantCulture));
    }
}
