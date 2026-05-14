namespace MxFramework.Logging
{
    /// <summary>
    /// Immutable payload supplied by a caller when recording a log entry.
    /// </summary>
    /// <remarks>NoAlloc after initialization. Callers without a frame context must use <see cref="UnknownFrameValue"/>.</remarks>
    public readonly struct LogEntry
    {
        public const long UnknownFrameValue = -1;

        public LogEntry(
            LogLevel level,
            string category,
            string message,
            long frameValue = UnknownFrameValue,
            string traceId = "",
            string code = "")
        {
            Level = level;
            Category = category ?? string.Empty;
            Message = message ?? string.Empty;
            FrameValue = frameValue;
            TraceId = traceId ?? string.Empty;
            Code = code ?? string.Empty;
        }

        public LogLevel Level { get; }
        public string Category { get; }
        public string Message { get; }
        public long FrameValue { get; }
        public string TraceId { get; }
        public string Code { get; }
    }
}
