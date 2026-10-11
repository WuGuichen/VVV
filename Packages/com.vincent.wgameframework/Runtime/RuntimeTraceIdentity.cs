using System;
using System.Globalization;

namespace MxFramework.Runtime
{
    /// <summary>A numerical trace identity. KindId is source-defined; execution/sequence identify runtime instances.</summary>
    public readonly struct RuntimeTraceIdentity : IEquatable<RuntimeTraceIdentity>
    {
        public static readonly RuntimeTraceIdentity None = default;
        public const int HashSchemaId = RuntimeHashSchemaIds.TraceIdentity;
        public const int HashSchemaVersion = 1;
        private const int KindKey = 1, SourceKey = 2, ExecutionKey = 3, SequenceKey = 4;

        public RuntimeTraceIdentity(int kindId, int sourceId = 0, long executionId = 0, long sequence = 0)
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
        public bool Equals(RuntimeTraceIdentity other) => KindId == other.KindId && SourceId == other.SourceId
            && ExecutionId == other.ExecutionId && Sequence == other.Sequence;
        public override bool Equals(object obj) => obj is RuntimeTraceIdentity other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { int hash = KindId; hash = hash * 397 ^ SourceId; hash = hash * 397 ^ ExecutionId.GetHashCode(); return hash * 397 ^ Sequence.GetHashCode(); }
        }
        public static bool operator ==(RuntimeTraceIdentity left, RuntimeTraceIdentity right) => left.Equals(right);
        public static bool operator !=(RuntimeTraceIdentity left, RuntimeTraceIdentity right) => !left.Equals(right);

        public void Contribute(RuntimeHashAccumulator hash, int key)
        {
            hash.BeginObject(key, HashSchemaId, HashSchemaVersion, !IsNone);
            if (!IsNone)
            {
                hash.AddInt(KindKey, KindId); hash.AddInt(SourceKey, SourceId);
                hash.AddLong(ExecutionKey, ExecutionId); hash.AddLong(SequenceKey, Sequence);
            }
            hash.EndObject();
        }

        /// <remarks>Diagnostics only; never use formatted text for identity or hashing.</remarks>
        public override string ToString() => IsNone ? string.Empty : string.Concat(
            KindId.ToString(CultureInfo.InvariantCulture), ":", SourceId.ToString(CultureInfo.InvariantCulture), ":",
            ExecutionId.ToString(CultureInfo.InvariantCulture), ":", Sequence.ToString(CultureInfo.InvariantCulture));
    }
}
