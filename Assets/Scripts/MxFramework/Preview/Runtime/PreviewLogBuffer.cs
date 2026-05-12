using System.Collections.Generic;

namespace MxFramework.Preview
{
    /// <summary>
    /// Ring buffer of structured log entries since the last reset, served by preview.getLogs.
    /// </summary>
    public sealed class PreviewLogBuffer
    {
        private readonly List<LogEntry> _entries = new List<LogEntry>();
        private readonly int _capacity;
        private long _seq;
        private long _resetMs;
        public bool DroppedOldest { get; private set; }

        public PreviewLogBuffer(int capacity = 4096) { _capacity = capacity > 0 ? capacity : 4096; }

        public void Reset()
        {
            _entries.Clear();
            _seq = 0;
            _resetMs = NowMs();
            DroppedOldest = false;
        }

        public LogEntry Append(string level, string message)
        {
            LogEntry e = new LogEntry
            {
                Seq = ++_seq,
                Level = level ?? "info",
                Message = message ?? string.Empty,
                AtMs = NowMs() - _resetMs,
            };
            _entries.Add(e);
            if (_entries.Count > _capacity)
            {
                _entries.RemoveAt(0);
                DroppedOldest = true;
            }
            return e;
        }

        public IReadOnlyList<LogEntry> GetSince(long afterSeq, int max, out long lastSeq)
        {
            List<LogEntry> result = new List<LogEntry>();
            lastSeq = _seq;
            if (max <= 0) max = 200;
            for (int i = 0; i < _entries.Count && result.Count < max; i++)
            {
                LogEntry e = _entries[i];
                if (e.Seq <= afterSeq) continue;
                result.Add(e);
            }
            return result;
        }

        public IReadOnlyList<LogEntry> GetAll() => _entries;

        private static long NowMs()
        {
            return (long)(System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        }
    }
}
