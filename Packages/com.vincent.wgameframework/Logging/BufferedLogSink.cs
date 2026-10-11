using System;

namespace MxFramework.Logging
{
    /// <summary>
    /// Sink that writes entries into a <see cref="LogBuffer"/>.
    /// </summary>
    /// <remarks>NoAlloc after initialization.</remarks>
    public sealed class BufferedLogSink : ILogSink
    {
        public BufferedLogSink(LogBuffer buffer)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public LogBuffer Buffer { get; }

        /// <inheritdoc />
        public void Write(in LogEntry entry)
        {
            Buffer.Add(in entry);
        }
    }
}
