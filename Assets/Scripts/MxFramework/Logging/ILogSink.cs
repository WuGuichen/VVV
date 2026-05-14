namespace MxFramework.Logging
{
    /// <summary>
    /// Receives framework log entries from a logger.
    /// </summary>
    public interface ILogSink
    {
        /// <summary>
        /// Writes an already constructed entry.
        /// </summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        void Write(in LogEntry entry);
    }
}
