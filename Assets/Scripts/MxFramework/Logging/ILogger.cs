namespace MxFramework.Logging
{
    /// <summary>
    /// Writes framework log entries to one or more sinks.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Returns whether an entry with the supplied level and category would be written.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        bool IsEnabled(LogLevel level, string category);

        /// <summary>
        /// Writes an already constructed log entry.
        /// </summary>
        /// <remarks>NoAlloc after initialization. Expensive message construction should be guarded with <see cref="IsEnabled"/>.</remarks>
        void Log(in LogEntry entry);
    }
}
