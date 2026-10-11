namespace MxFramework.Logging
{
    /// <summary>
    /// Logger implementation that drops every entry.
    /// </summary>
    /// <remarks>NoAlloc after initialization.</remarks>
    public sealed class NullLogger : ILogger
    {
        /// <inheritdoc />
        public bool IsEnabled(LogLevel level, string category)
        {
            return false;
        }

        /// <inheritdoc />
        public void Log(in LogEntry entry)
        {
        }
    }
}
