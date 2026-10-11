using System;
using System.Collections.Generic;
using System.Threading;

namespace MxFramework.Runtime
{
    /// <summary>State contributors use permanent integer identities and a declared schema.</summary>
    public interface IRuntimeHashContributor
    {
        int ContributorId { get; }
        int HashSchemaId { get; }
        int HashSchemaVersion { get; }
        void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator);
    }

    public static class RuntimeHashProtocolIds
    {
        public const int CombinedStateSchema = RuntimeHashSchemaIds.CombinedState;
        public const int CombinedStateVersion = RuntimeHashSchemaIds.Version1;
        public const int Frame = RuntimeHashProtocolFields.Frame;
        public const int Contributors = RuntimeHashProtocolFields.Contributors;
        public const int ContributorId = RuntimeHashProtocolFields.ContributorId;
        public const int ContributorState = RuntimeHashProtocolFields.ContributorState;
    }

    /// <summary>Stable numeric contributor ordering with a reusable synchronous workspace.</summary>
    public sealed class RuntimeHashCombiner
    {
        private readonly Entry[] _entries;
        private readonly RuntimeHashAccumulator _accumulator = new RuntimeHashAccumulator(
            RuntimeHashProtocolIds.CombinedStateSchema, RuntimeHashProtocolIds.CombinedStateVersion);
        private int _computing;

        public RuntimeHashCombiner(IEnumerable<IRuntimeHashContributor> contributors)
        {
            if (contributors == null) throw new ArgumentNullException(nameof(contributors));
            var entries = new List<Entry>();
            var ids = new HashSet<int>();
            foreach (IRuntimeHashContributor contributor in contributors)
            {
                if (contributor == null) throw new ArgumentException("A hash contributor cannot be null.", nameof(contributors));
                int id = contributor.ContributorId;
                int schema = contributor.HashSchemaId;
                int version = contributor.HashSchemaVersion;
                if (id <= 0 || schema <= 0 || version <= 0)
                    throw new ArgumentException("Contributor and schema identities must be predeclared positive integers.", nameof(contributors));
                if (!ids.Add(id)) throw new ArgumentException("Duplicate hash contributor identity.", nameof(contributors));
                entries.Add(new Entry(id, schema, version, contributor));
            }
            entries.Sort((left, right) => left.Id.CompareTo(right.Id));
            _entries = entries.ToArray();
        }

        public long LastFieldCount => _accumulator.FieldCount;
        public long LastEncodedByteCount => _accumulator.EncodedByteCount;
        public long ComputeHash(RuntimeFrame frame) => ComputeHash(new RuntimeHashContext(frame));

        public long ComputeHash(RuntimeHashContext context)
        {
            if (Interlocked.CompareExchange(ref _computing, 1, 0) != 0)
                throw new InvalidOperationException("A hash workspace cannot be used recursively or concurrently.");
            try
            {
                _accumulator.Reset();
                _accumulator.AddLong(RuntimeHashProtocolIds.Frame, context.Frame.Value);
                _accumulator.BeginArray(RuntimeHashProtocolIds.Contributors, _entries.Length);
                for (int i = 0; i < _entries.Length; i++)
                {
                    Entry entry = _entries[i];
                    _accumulator.BeginElement(i);
                    _accumulator.AddInt(RuntimeHashProtocolIds.ContributorId, entry.Id);
                    _accumulator.BeginObject(RuntimeHashProtocolIds.ContributorState, entry.SchemaId, entry.SchemaVersion);
                    entry.Contributor.Contribute(context, _accumulator);
                    _accumulator.EndObject();
                    _accumulator.EndElement();
                }
                _accumulator.EndArray();
                return _accumulator.ToHash();
            }
            finally { Volatile.Write(ref _computing, 0); }
        }

        public static long ComputeHash(RuntimeFrame frame, IEnumerable<IRuntimeHashContributor> contributors) =>
            new RuntimeHashCombiner(contributors).ComputeHash(frame);
        public static long ComputeHash(RuntimeHashContext context, IEnumerable<IRuntimeHashContributor> contributors) =>
            new RuntimeHashCombiner(contributors).ComputeHash(context);

        private readonly struct Entry
        {
            internal Entry(int id, int schemaId, int schemaVersion, IRuntimeHashContributor contributor)
            { Id = id; SchemaId = schemaId; SchemaVersion = schemaVersion; Contributor = contributor; }
            internal readonly int Id;
            internal readonly int SchemaId;
            internal readonly int SchemaVersion;
            internal readonly IRuntimeHashContributor Contributor;
        }
    }
}
