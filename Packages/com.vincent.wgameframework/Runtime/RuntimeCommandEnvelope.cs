using System;

namespace MxFramework.Runtime
{
    /// <summary>Identifies whether a command is an external input or a derived runtime command.</summary>
    public enum RuntimeCommandProvenanceKind
    {
        Unspecified = 0,
        Exogenous = 1,
        Derived = 2
    }

    /// <summary>
    /// Fixed-size typed command payload that survives command buffering, snapshots, and replay copies.
    /// </summary>
    public readonly struct RuntimeCommandEnvelope : IEquatable<RuntimeCommandEnvelope>
    {
        /// <remarks>NoAlloc.</remarks>
        public RuntimeCommandEnvelope(
            int typeId,
            int schemaVersion,
            RuntimeCommandProvenanceKind provenance,
            long data0 = 0L,
            long data1 = 0L,
            long data2 = 0L,
            long data3 = 0L,
            long data4 = 0L,
            long data5 = 0L,
            long data6 = 0L,
            long data7 = 0L)
        {
            if (typeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(typeId));
            if (schemaVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (!IsDefinedProvenance(provenance))
                throw new ArgumentOutOfRangeException(nameof(provenance));

            TypeId = typeId;
            SchemaVersion = schemaVersion;
            Provenance = provenance;
            Data0 = data0;
            Data1 = data1;
            Data2 = data2;
            Data3 = data3;
            Data4 = data4;
            Data5 = data5;
            Data6 = data6;
            Data7 = data7;
        }

        public int TypeId { get; }
        public int SchemaVersion { get; }
        public RuntimeCommandProvenanceKind Provenance { get; }
        public long Data0 { get; }
        public long Data1 { get; }
        public long Data2 { get; }
        public long Data3 { get; }
        public long Data4 { get; }
        public long Data5 { get; }
        public long Data6 { get; }
        public long Data7 { get; }
        public bool IsPresent => TypeId > 0;

        public bool Equals(RuntimeCommandEnvelope other)
        {
            return TypeId == other.TypeId
                && SchemaVersion == other.SchemaVersion
                && Provenance == other.Provenance
                && Data0 == other.Data0
                && Data1 == other.Data1
                && Data2 == other.Data2
                && Data3 == other.Data3
                && Data4 == other.Data4
                && Data5 == other.Data5
                && Data6 == other.Data6
                && Data7 == other.Data7;
        }

        public override bool Equals(object obj)
        {
            return obj is RuntimeCommandEnvelope other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TypeId;
                hash = (hash * 397) ^ SchemaVersion;
                hash = (hash * 397) ^ (int)Provenance;
                hash = (hash * 397) ^ Data0.GetHashCode();
                hash = (hash * 397) ^ Data1.GetHashCode();
                hash = (hash * 397) ^ Data2.GetHashCode();
                hash = (hash * 397) ^ Data3.GetHashCode();
                hash = (hash * 397) ^ Data4.GetHashCode();
                hash = (hash * 397) ^ Data5.GetHashCode();
                hash = (hash * 397) ^ Data6.GetHashCode();
                hash = (hash * 397) ^ Data7.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(
            RuntimeCommandEnvelope left,
            RuntimeCommandEnvelope right) => left.Equals(right);

        public static bool operator !=(
            RuntimeCommandEnvelope left,
            RuntimeCommandEnvelope right) => !left.Equals(right);

        private static bool IsDefinedProvenance(RuntimeCommandProvenanceKind provenance)
        {
            return provenance == RuntimeCommandProvenanceKind.Unspecified
                || provenance == RuntimeCommandProvenanceKind.Exogenous
                || provenance == RuntimeCommandProvenanceKind.Derived;
        }
    }
}
