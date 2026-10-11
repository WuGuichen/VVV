using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Runtime
{
    public sealed class RuntimeContextMap
    {
        private readonly Dictionary<ContextStorageKey, object> _values = new Dictionary<ContextStorageKey, object>();

        public int Count => _values.Count;

        public void Set<T>(ContextKey<T> key, T value)
        {
            ValidateKey(key);
            _values[new ContextStorageKey(key.Id, typeof(T))] = value;
        }

        public bool TryGet<T>(ContextKey<T> key, out T value)
        {
            ValidateKey(key);

            object stored;
            if (_values.TryGetValue(new ContextStorageKey(key.Id, typeof(T)), out stored))
            {
                value = (T)stored;
                return true;
            }

            value = default(T);
            return false;
        }

        public bool Contains<T>(ContextKey<T> key)
        {
            ValidateKey(key);
            return _values.ContainsKey(new ContextStorageKey(key.Id, typeof(T)));
        }

        public bool Remove<T>(ContextKey<T> key)
        {
            ValidateKey(key);
            return _values.Remove(new ContextStorageKey(key.Id, typeof(T)));
        }

        public void Clear()
        {
            _values.Clear();
        }

        public RuntimeContextMapSnapshot CreateSnapshot()
        {
            if (_values.Count == 0)
            {
                return RuntimeContextMapSnapshot.Empty;
            }

            var entries = new List<RuntimeContextMapSnapshotEntry>(_values.Count);
            foreach (KeyValuePair<ContextStorageKey, object> pair in _values)
            {
                entries.Add(new RuntimeContextMapSnapshotEntry(
                    pair.Key.Id,
                    pair.Key.ValueType.Name,
                    pair.Key.ValueType.FullName ?? pair.Key.ValueType.Name));
            }

            entries.Sort(CompareSnapshotEntries);
            return new RuntimeContextMapSnapshot(entries);
        }

        private static void ValidateKey<T>(ContextKey<T> key)
        {
            if (string.IsNullOrWhiteSpace(key.Id))
            {
                throw new ArgumentException("Context key id cannot be null, empty, or whitespace.", nameof(key));
            }
        }

        private static int CompareSnapshotEntries(RuntimeContextMapSnapshotEntry left, RuntimeContextMapSnapshotEntry right)
        {
            int id = string.Compare(left.Id, right.Id, StringComparison.Ordinal);
            if (id != 0)
            {
                return id;
            }

            return string.Compare(left.ValueTypeFullName, right.ValueTypeFullName, StringComparison.Ordinal);
        }

        private readonly struct ContextStorageKey : IEquatable<ContextStorageKey>
        {
            public ContextStorageKey(string id, Type valueType)
            {
                Id = id;
                ValueType = valueType;
            }

            public string Id { get; }
            public Type ValueType { get; }

            public bool Equals(ContextStorageKey other)
            {
                return string.Equals(Id, other.Id, StringComparison.Ordinal) && ValueType == other.ValueType;
            }

            public override bool Equals(object obj)
            {
                return obj is ContextStorageKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (StringComparer.Ordinal.GetHashCode(Id) * 397) ^ ValueType.GetHashCode();
                }
            }
        }
    }

    public readonly struct RuntimeContextMapSnapshot
    {
        public static readonly RuntimeContextMapSnapshot Empty =
            new RuntimeContextMapSnapshot(new ReadOnlyCollection<RuntimeContextMapSnapshotEntry>(Array.Empty<RuntimeContextMapSnapshotEntry>()));

        public RuntimeContextMapSnapshot(IReadOnlyList<RuntimeContextMapSnapshotEntry> entries)
        {
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        public IReadOnlyList<RuntimeContextMapSnapshotEntry> Entries { get; }
    }

    public readonly struct RuntimeContextMapSnapshotEntry
    {
        public RuntimeContextMapSnapshotEntry(string id, string valueTypeName, string valueTypeFullName)
        {
            Id = id ?? string.Empty;
            ValueTypeName = valueTypeName ?? string.Empty;
            ValueTypeFullName = valueTypeFullName ?? string.Empty;
        }

        public string Id { get; }
        public string ValueTypeName { get; }
        public string ValueTypeFullName { get; }

        public string Summary => Id + " (" + ValueTypeName + ")";
    }
}
