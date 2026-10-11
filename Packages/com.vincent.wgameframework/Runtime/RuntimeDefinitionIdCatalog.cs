using System;
using System.Collections.Generic;

namespace MxFramework.Runtime
{
    /// <summary>An identity already allocated by its authoring owner. Zero is the unset identity.</summary>
    public readonly struct RuntimeDefinitionId : IEquatable<RuntimeDefinitionId>, IComparable<RuntimeDefinitionId>
    {
        public static readonly RuntimeDefinitionId None = default;
        public RuntimeDefinitionId(int domainId, int value)
        {
            if (domainId <= 0) throw new ArgumentOutOfRangeException(nameof(domainId));
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            DomainId = domainId; Value = value;
        }
        public int DomainId { get; }
        public int Value { get; }
        public bool IsValid => DomainId > 0 && Value > 0;
        public bool Equals(RuntimeDefinitionId other) => DomainId == other.DomainId && Value == other.Value;
        public override bool Equals(object obj) => obj is RuntimeDefinitionId other && Equals(other);
        public override int GetHashCode() => unchecked((DomainId * 397) ^ Value);
        public int CompareTo(RuntimeDefinitionId other)
        { int domain = DomainId.CompareTo(other.DomainId); return domain != 0 ? domain : Value.CompareTo(other.Value); }
        public static bool operator ==(RuntimeDefinitionId left, RuntimeDefinitionId right) => left.Equals(right);
        public static bool operator !=(RuntimeDefinitionId left, RuntimeDefinitionId right) => !left.Equals(right);
    }

    public readonly struct RuntimeDefinitionIdEntry
    {
        public RuntimeDefinitionIdEntry(RuntimeDefinitionId id, bool retired = false)
        { Id = id; Retired = retired; }
        public RuntimeDefinitionId Id { get; }
        public bool Retired { get; }
    }

    /// <summary>Immutable numeric projection of a validated authoring catalog. Never allocates identities.</summary>
    public sealed class RuntimeDefinitionIdCatalog
    {
        private readonly Dictionary<RuntimeDefinitionId, bool> _entries;
        private readonly RuntimeDefinitionIdEntry[] _ordered;

        public RuntimeDefinitionIdCatalog(long revision, in RuntimeHashDigest contentDigest, IReadOnlyList<RuntimeDefinitionIdEntry> entries)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            Revision = revision; ContentDigest = contentDigest;
            _entries = new Dictionary<RuntimeDefinitionId, bool>(entries.Count);
            _ordered = new RuntimeDefinitionIdEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!entry.Id.IsValid) throw new ArgumentException("Catalog identities must be positive integers.", nameof(entries));
                if (_entries.ContainsKey(entry.Id)) throw new ArgumentException("Duplicate catalog identity.", nameof(entries));
                _entries.Add(entry.Id, entry.Retired);
                _ordered[i] = entry;
            }
            Array.Sort(_ordered, (left, right) => left.Id.CompareTo(right.Id));
        }

        public long Revision { get; }
        public RuntimeHashDigest ContentDigest { get; }
        public int Count => _ordered.Length;
        public RuntimeDefinitionIdEntry GetEntry(int index) => _ordered[index];
        public bool IsActive(RuntimeDefinitionId id) => _entries.TryGetValue(id, out bool retired) && !retired;
        public bool IsRetired(RuntimeDefinitionId id) => _entries.TryGetValue(id, out bool retired) && retired;

        public void ValidateReference(RuntimeDefinitionId id, bool optional = false)
        {
            if (optional && id == RuntimeDefinitionId.None) return;
            if (!IsActive(id)) throw new ArgumentException("Unknown, unset or retired definition identity.", nameof(id));
        }
    }
}
