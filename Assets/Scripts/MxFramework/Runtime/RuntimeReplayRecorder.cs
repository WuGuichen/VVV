using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Runtime
{
    public readonly struct RuntimeReplayHeader
    {
        public RuntimeReplayHeader(
            int schemaVersion,
            string frameworkVersion,
            string configHash,
            string resourceCatalogHash,
            RuntimeFrame startFrame)
        {
            if (schemaVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Replay schema version cannot be negative.");
            }

            SchemaVersion = schemaVersion;
            FrameworkVersion = frameworkVersion ?? string.Empty;
            ConfigHash = configHash ?? string.Empty;
            ResourceCatalogHash = resourceCatalogHash ?? string.Empty;
            StartFrame = startFrame;
        }

        public int SchemaVersion { get; }
        public string FrameworkVersion { get; }
        public string ConfigHash { get; }
        public string ResourceCatalogHash { get; }
        public RuntimeFrame StartFrame { get; }
    }

    public sealed class RuntimeReplayFrameRecord
    {
        private readonly ReadOnlyCollection<RuntimeCommand> _commands;

        public RuntimeReplayFrameRecord(
            RuntimeFrame frame,
            IReadOnlyList<RuntimeCommand> commands,
            long resultHash,
            string diagnosticsSummary)
        {
            Frame = frame;
            _commands = CopyCommands(commands);
            ResultHash = resultHash;
            DiagnosticsSummary = diagnosticsSummary ?? string.Empty;
        }

        public RuntimeFrame Frame { get; }
        public IReadOnlyList<RuntimeCommand> Commands => _commands;
        public long ResultHash { get; }
        public string DiagnosticsSummary { get; }

        private static ReadOnlyCollection<RuntimeCommand> CopyCommands(IReadOnlyList<RuntimeCommand> commands)
        {
            if (commands == null || commands.Count == 0)
            {
                return new ReadOnlyCollection<RuntimeCommand>(new List<RuntimeCommand>());
            }

            var copy = new List<RuntimeCommand>(commands.Count);
            for (int i = 0; i < commands.Count; i++)
            {
                copy.Add(commands[i]);
            }

            return new ReadOnlyCollection<RuntimeCommand>(copy);
        }
    }

    public sealed class RuntimeReplaySnapshot
    {
        private readonly ReadOnlyCollection<RuntimeReplayFrameRecord> _records;

        public RuntimeReplaySnapshot(RuntimeReplayHeader header, IReadOnlyList<RuntimeReplayFrameRecord> records)
        {
            Header = header;
            _records = CopyRecords(records);
        }

        public RuntimeReplayHeader Header { get; }
        public IReadOnlyList<RuntimeReplayFrameRecord> Records => _records;
        public int Count => _records.Count;

        private static ReadOnlyCollection<RuntimeReplayFrameRecord> CopyRecords(IReadOnlyList<RuntimeReplayFrameRecord> records)
        {
            if (records == null || records.Count == 0)
            {
                return new ReadOnlyCollection<RuntimeReplayFrameRecord>(new List<RuntimeReplayFrameRecord>());
            }

            var copy = new List<RuntimeReplayFrameRecord>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                RuntimeReplayFrameRecord record = records[i];
                copy.Add(new RuntimeReplayFrameRecord(record.Frame, record.Commands, record.ResultHash, record.DiagnosticsSummary));
            }

            return new ReadOnlyCollection<RuntimeReplayFrameRecord>(copy);
        }
    }

    public sealed class RuntimeReplayRecorder
    {
        private readonly List<RuntimeReplayFrameRecord> _records = new List<RuntimeReplayFrameRecord>();

        public RuntimeReplayRecorder(RuntimeReplayHeader header)
        {
            Header = header;
        }

        public RuntimeReplayHeader Header { get; }
        public int Count => _records.Count;
        public IReadOnlyList<RuntimeReplayFrameRecord> Records => _records;

        public RuntimeReplayFrameRecord RecordFrame(
            RuntimeFrame frame,
            IReadOnlyList<RuntimeCommand> commands,
            long resultHash,
            string diagnosticsSummary = "")
        {
            var record = new RuntimeReplayFrameRecord(frame, commands, resultHash, diagnosticsSummary);
            _records.Add(record);
            return record;
        }

        public RuntimeReplaySnapshot CreateSnapshot()
        {
            return new RuntimeReplaySnapshot(Header, _records);
        }

        public void Clear()
        {
            _records.Clear();
        }
    }
}
