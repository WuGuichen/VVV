using System;
using System.Collections.Generic;
using MxFramework.Core.Collections;

namespace MxFramework.Logging
{
    /// <summary>
    /// Fixed-capacity buffer that stores recent log records in ascending sequence order.
    /// </summary>
    /// <remarks>NoAlloc after initialization. This type is not thread-safe.</remarks>
    public sealed class LogBuffer
    {
        private readonly RingBuffer<LogRecord> _records;
        private long _nextSequence = 1;

        public LogBuffer(int capacity)
        {
            _records = new RingBuffer<LogRecord>(capacity);
        }

        public int Capacity => _records.Capacity;
        public int Count => _records.Count;
        public long DroppedCount { get; private set; }
        public long LatestSequence { get; private set; }

        /// <summary>
        /// Adds an entry and returns the generated buffer-local record.
        /// </summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public LogRecord Add(in LogEntry entry)
        {
            var record = new LogRecord(_nextSequence++, in entry);
            if (_records.Count == _records.Capacity)
                DroppedCount++;

            _records.Add(record);
            LatestSequence = record.Sequence;
            return record;
        }

        /// <summary>
        /// Adds an entry to the buffer.
        /// </summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void Write(in LogEntry entry)
        {
            Add(in entry);
        }

        /// <summary>
        /// Appends buffered records to <paramref name="output"/> in ascending sequence order.
        /// </summary>
        /// <remarks>NoAlloc. The output list is not cleared.</remarks>
        public void CopyTo(List<LogRecord> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            _records.CopyTo(output);
        }

        /// <summary>
        /// Clears buffered records and resets generated sequence and drop counters.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public void Clear()
        {
            _records.Clear();
            DroppedCount = 0;
            LatestSequence = 0;
            _nextSequence = 1;
        }
    }
}
