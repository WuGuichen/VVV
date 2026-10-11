using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Runtime
{
    public enum RuntimeReplayCommandCaptureMode
    {
        /// <summary>Compatibility mode that copies every command supplied by the caller.</summary>
        LegacyUnfiltered = 0,
        /// <summary>Records only trusted exogenous commands accepted by a sealed source registry.</summary>
        ExogenousFiltered = 1
    }

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
        private readonly List<RuntimeCommand> _ownedCommands;
        private readonly ReadOnlyCollection<RuntimeCommand> _commands;
        private string _diagnosticsSummary;
        private Func<RuntimeReplayFrameRecord, string> _diagnosticsFormatter;
        private bool _initialized;

        public RuntimeReplayFrameRecord(RuntimeFrame frame, IReadOnlyList<RuntimeCommand> commands,
            long resultHash, string diagnosticsSummary)
            : this(commands?.Count ?? 0)
        {
            Initialize(frame, commands, resultHash, diagnosticsSummary, null, null);
        }

        internal RuntimeReplayFrameRecord(int commandCapacity)
        {
            _ownedCommands = new List<RuntimeCommand>(commandCapacity);
            _commands = _ownedCommands.AsReadOnly();
        }

        internal void Initialize(RuntimeFrame frame, IReadOnlyList<RuntimeCommand> commands, long resultHash,
            string diagnosticsSummary, Func<RuntimeReplayFrameRecord, string> formatter,
            RuntimeCommandSourceRegistry sourceRegistry)
        {
            if (_initialized) throw new InvalidOperationException("A published replay frame cannot be overwritten.");
            _initialized = true;
            Frame = frame; ResultHash = resultHash;
            int count = commands?.Count ?? 0;
            for (int i = 0; i < count; i++)
                if (sourceRegistry == null || sourceRegistry.IsAuthorizedExogenous(commands[i])) _ownedCommands.Add(commands[i]);
            _diagnosticsSummary = formatter == null ? diagnosticsSummary ?? string.Empty : null;
            _diagnosticsFormatter = formatter;
        }

        public RuntimeFrame Frame { get; private set; }
        public IReadOnlyList<RuntimeCommand> Commands => _commands;
        public long ResultHash { get; private set; }
        /// <summary>Formats a deferred diagnostic only when a consumer requests it; recording itself does not format text.</summary>
        public string DiagnosticsSummary
        {
            get
            {
                if (_diagnosticsSummary == null)
                {
                    _diagnosticsSummary = _diagnosticsFormatter?.Invoke(this) ?? string.Empty;
                    _diagnosticsFormatter = null;
                }
                return _diagnosticsSummary;
            }
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
        private readonly RuntimeCommandSourceRegistry _sourceRegistry;

        /// <summary>Creates the legacy recorder that copies every command supplied by the caller.</summary>
        /// <remarks>
        /// AllocByDesign. This constructor is intentionally unfiltered and is not an exogenous replay stream.
        /// Use CreateExogenousStream for trusted source filtering.
        /// </remarks>
        public RuntimeReplayRecorder(RuntimeReplayHeader header)
            : this(header, null, RuntimeReplayCommandCaptureMode.LegacyUnfiltered)
        {
        }

        public RuntimeReplayHeader Header { get; }
        public RuntimeReplayCommandCaptureMode CaptureMode { get; }
        public int Count => _records.Count;
        public IReadOnlyList<RuntimeReplayFrameRecord> Records => _records;

        /// <summary>Creates a recorder whose frame command list is the trusted exogenous input stream.</summary>
        /// <remarks>
        /// AllocByDesign. The registry is sealed before recording; only registered Exogenous sources with
        /// matching envelope provenance and an explicit command allowlist entry are copied.
        /// </remarks>
        public static RuntimeReplayRecorder CreateExogenousStream(
            RuntimeReplayHeader header,
            RuntimeCommandSourceRegistry sourceRegistry)
        {
            if (sourceRegistry == null)
                throw new ArgumentNullException(nameof(sourceRegistry));

            sourceRegistry.Seal();
            return new RuntimeReplayRecorder(
                header,
                sourceRegistry,
                RuntimeReplayCommandCaptureMode.ExogenousFiltered);
        }

        /// <remarks>AllocByDesign; commands are value-copied into the frame record.</remarks>
        public RuntimeReplayFrameRecord RecordFrame(
            RuntimeFrame frame, IReadOnlyList<RuntimeCommand> commands, long resultHash, string diagnosticsSummary = "")
            => RecordFrameCore(frame, commands, resultHash, diagnosticsSummary, null);

        /// <summary>Records immutable command data and delays diagnostic formatting until inspection or export.</summary>
        public RuntimeReplayFrameRecord RecordFrameDeferred(RuntimeFrame frame, IReadOnlyList<RuntimeCommand> commands,
            long resultHash, Func<RuntimeReplayFrameRecord, string> diagnosticsFormatter)
            => RecordFrameCore(frame, commands, resultHash, null, diagnosticsFormatter);

        private RuntimeReplayFrameRecord[] _preparedFrames = Array.Empty<RuntimeReplayFrameRecord>();
        private int _nextPreparedFrame;

        /// <summary>Preallocates independently owned future records. Existing published frames are never reused.</summary>
        /// <remarks>Call during recording setup. Appending within these bounds does not allocate; overflow retains legacy growth behavior.</remarks>
        public void PrepareCapacity(int frameCapacity, int commandCapacity)
        {
            if (frameCapacity < 0) throw new ArgumentOutOfRangeException(nameof(frameCapacity));
            if (commandCapacity < 0) throw new ArgumentOutOfRangeException(nameof(commandCapacity));
            _preparedFrames = new RuntimeReplayFrameRecord[frameCapacity];
            for (int i = 0; i < frameCapacity; i++) _preparedFrames[i] = new RuntimeReplayFrameRecord(commandCapacity);
            _nextPreparedFrame = 0;
            if (_records.Capacity < _records.Count + frameCapacity) _records.Capacity = _records.Count + frameCapacity;
        }

        private RuntimeReplayFrameRecord RecordFrameCore(RuntimeFrame frame, IReadOnlyList<RuntimeCommand> commands,
            long resultHash, string diagnosticsSummary, Func<RuntimeReplayFrameRecord, string> formatter)
        {
            var record = _nextPreparedFrame < _preparedFrames.Length
                ? _preparedFrames[_nextPreparedFrame++] : new RuntimeReplayFrameRecord(commands?.Count ?? 0);
            record.Initialize(frame, commands, resultHash, diagnosticsSummary, formatter,
                CaptureMode == RuntimeReplayCommandCaptureMode.ExogenousFiltered ? _sourceRegistry : null);
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

        private RuntimeReplayRecorder(
            RuntimeReplayHeader header,
            RuntimeCommandSourceRegistry sourceRegistry,
            RuntimeReplayCommandCaptureMode captureMode)
        {
            Header = header;
            _sourceRegistry = sourceRegistry;
            CaptureMode = captureMode;
        }
    }
}
