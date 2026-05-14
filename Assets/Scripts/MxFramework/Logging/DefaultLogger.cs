using System;
using System.Collections.Generic;

namespace MxFramework.Logging
{
    /// <summary>
    /// Default logger with minimum level filtering, exact category allowlist filtering, and sink fan-out.
    /// </summary>
    /// <remarks>NoAlloc after initialization on the write path.</remarks>
    public sealed class DefaultLogger : ILogger
    {
        private readonly ILogSink[] _sinks;
        private readonly HashSet<string> _allowedCategories;

        public DefaultLogger(LogLevel minLevel, IReadOnlyList<ILogSink> sinks)
            : this(minLevel, sinks, Array.Empty<string>())
        {
        }

        public DefaultLogger(
            LogLevel minLevel,
            IReadOnlyList<ILogSink> sinks,
            IEnumerable<string> allowedCategories)
        {
            MinLevel = minLevel;
            _sinks = CopySinks(sinks);
            _allowedCategories = BuildAllowlist(allowedCategories);
        }

        public LogLevel MinLevel { get; }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel level, string category)
        {
            if (level < MinLevel)
                return false;

            if (_allowedCategories.Count == 0)
                return true;

            return _allowedCategories.Contains(category ?? string.Empty);
        }

        /// <inheritdoc />
        public void Log(in LogEntry entry)
        {
            if (!IsEnabled(entry.Level, entry.Category))
                return;

            for (int i = 0; i < _sinks.Length; i++)
                _sinks[i].Write(in entry);
        }

        private static ILogSink[] CopySinks(IReadOnlyList<ILogSink> sinks)
        {
            if (sinks == null || sinks.Count == 0)
                return Array.Empty<ILogSink>();

            var copy = new ILogSink[sinks.Count];
            for (int i = 0; i < sinks.Count; i++)
            {
                if (sinks[i] == null)
                    throw new ArgumentException("DefaultLogger sinks cannot contain null entries.", nameof(sinks));

                copy[i] = sinks[i];
            }

            return copy;
        }

        private static HashSet<string> BuildAllowlist(IEnumerable<string> allowedCategories)
        {
            var allowlist = new HashSet<string>(StringComparer.Ordinal);
            if (allowedCategories == null)
                return allowlist;

            foreach (string category in allowedCategories)
                allowlist.Add(category ?? string.Empty);

            return allowlist;
        }
    }
}
