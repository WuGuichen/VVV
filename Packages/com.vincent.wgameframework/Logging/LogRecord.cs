namespace MxFramework.Logging
{
    /// <summary>
    /// Buffer-local log record with a generated sequence number.
    /// </summary>
    /// <remarks>NoAlloc after initialization.</remarks>
    public readonly struct LogRecord
    {
        public LogRecord(long sequence, in LogEntry entry)
        {
            Sequence = sequence;
            Entry = entry;
        }

        public long Sequence { get; }
        public LogEntry Entry { get; }
    }
}
