using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Diagnostics;

namespace MxFramework.Logging.Diagnostics
{
    /// <summary>
    /// Diagnostics debug source that exports recent log records from a buffer.
    /// </summary>
    public sealed class LogDebugSource : IFrameworkDebugSource
    {
        private readonly LogBuffer _buffer;

        public LogDebugSource(LogBuffer buffer)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public string Name => "Logging";
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => true;

        /// <summary>
        /// Creates a snapshot containing one "Logs" section with recent records in ascending sequence order.
        /// </summary>
        /// <remarks>AllocByDesign.</remarks>
        public FrameworkDebugSnapshot CreateSnapshot()
        {
            var records = new List<LogRecord>(_buffer.Count);
            _buffer.CopyTo(records);

            var builder = new StringBuilder();
            for (int i = 0; i < records.Count; i++)
            {
                LogEntry entry = records[i].Entry;
                builder.Append('[').Append(entry.Level).Append("] [").Append(entry.Category).Append("] ").Append(entry.Message);
                if (entry.FrameValue != LogEntry.UnknownFrameValue)
                    builder.Append(" frame=").Append(entry.FrameValue);

                if (i < records.Count - 1)
                    builder.Append('\n');
            }

            var sections = new[] { new FrameworkDebugSection("Logs", builder.ToString()) };
            return new FrameworkDebugSnapshot(Name, Mode, sections);
        }
    }
}
