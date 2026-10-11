using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Runtime
{
    public readonly struct RuntimeSnapshotValue
    {
        public RuntimeSnapshotValue(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Snapshot value key cannot be null or empty.", nameof(key));
            }

            Key = key;
            Value = value ?? string.Empty;
        }

        public string Key { get; }
        public string Value { get; }
    }

    public enum RuntimeChangeKind
    {
        Added = 0,
        Removed = 1,
        Modified = 2
    }

    public readonly struct RuntimeChange
    {
        public RuntimeChange(RuntimeChangeKind kind, string key, string beforeValue, string afterValue)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Change key cannot be null or empty.", nameof(key));
            }

            Kind = kind;
            Key = key;
            BeforeValue = beforeValue ?? string.Empty;
            AfterValue = afterValue ?? string.Empty;
        }

        public RuntimeChangeKind Kind { get; }
        public string Key { get; }
        public string BeforeValue { get; }
        public string AfterValue { get; }
    }

    public sealed class RuntimeChangeSet
    {
        private readonly ReadOnlyCollection<RuntimeChange> _changes;

        public RuntimeChangeSet(IReadOnlyList<RuntimeChange> changes)
        {
            _changes = new ReadOnlyCollection<RuntimeChange>(
                changes != null ? new List<RuntimeChange>(changes) : new List<RuntimeChange>());
        }

        public int Count => _changes.Count;
        public bool HasChanges => _changes.Count > 0;
        public IReadOnlyList<RuntimeChange> Changes => _changes;
    }

    public static class RuntimeSnapshotDiff
    {
        public static RuntimeChangeSet Compare(
            IReadOnlyList<RuntimeSnapshotValue> before,
            IReadOnlyList<RuntimeSnapshotValue> after)
        {
            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            if (after == null)
            {
                throw new ArgumentNullException(nameof(after));
            }

            Dictionary<string, string> beforeValues = ToMap(before, nameof(before));
            Dictionary<string, string> afterValues = ToMap(after, nameof(after));

            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string key in beforeValues.Keys)
            {
                keys.Add(key);
            }

            foreach (string key in afterValues.Keys)
            {
                keys.Add(key);
            }

            var changes = new List<RuntimeChange>();
            foreach (string key in keys)
            {
                bool hadBefore = beforeValues.TryGetValue(key, out string beforeValue);
                bool hasAfter = afterValues.TryGetValue(key, out string afterValue);

                if (!hadBefore && hasAfter)
                {
                    changes.Add(new RuntimeChange(RuntimeChangeKind.Added, key, string.Empty, afterValue));
                }
                else if (hadBefore && !hasAfter)
                {
                    changes.Add(new RuntimeChange(RuntimeChangeKind.Removed, key, beforeValue, string.Empty));
                }
                else if (!string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
                {
                    changes.Add(new RuntimeChange(RuntimeChangeKind.Modified, key, beforeValue, afterValue));
                }
            }

            return new RuntimeChangeSet(changes);
        }

        private static Dictionary<string, string> ToMap(IReadOnlyList<RuntimeSnapshotValue> values, string parameterName)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                RuntimeSnapshotValue value = values[i];
                if (string.IsNullOrWhiteSpace(value.Key))
                {
                    throw new ArgumentException("Snapshot value key cannot be null or empty.", parameterName);
                }

                if (map.ContainsKey(value.Key))
                {
                    throw new ArgumentException("Snapshot contains duplicate keys.", parameterName);
                }

                map.Add(value.Key, value.Value ?? string.Empty);
            }

            return map;
        }
    }
}
