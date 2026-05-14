using System;
using System.Collections.Generic;
using MxFramework.Logging;
using NUnit.Framework;

namespace MxFramework.Tests.Logging
{
    public class LogBufferTests
    {
        [Test]
        public void Add_GeneratesAscendingBufferLocalSequence()
        {
            var buffer = new LogBuffer(3);

            LogRecord first = buffer.Add(new LogEntry(LogLevel.Info, "Runtime", "boot"));
            LogRecord second = buffer.Add(new LogEntry(LogLevel.Warning, "Runtime", "slow"));

            Assert.AreEqual(1, first.Sequence);
            Assert.AreEqual(2, second.Sequence);
            Assert.AreEqual(2, buffer.LatestSequence);
            Assert.AreEqual(0, buffer.DroppedCount);
        }

        [Test]
        public void Add_WhenCapacityExceeded_DropsOldestAndCountsDrop()
        {
            var buffer = new LogBuffer(2);
            buffer.Add(new LogEntry(LogLevel.Info, "A", "one"));
            buffer.Add(new LogEntry(LogLevel.Info, "A", "two"));
            buffer.Add(new LogEntry(LogLevel.Info, "A", "three"));

            var records = new List<LogRecord>();
            buffer.CopyTo(records);

            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(1, buffer.DroppedCount);
            Assert.AreEqual(3, buffer.LatestSequence);
            CollectionAssert.AreEqual(new long[] { 2, 3 }, new[] { records[0].Sequence, records[1].Sequence });
            Assert.AreEqual("two", records[0].Entry.Message);
            Assert.AreEqual("three", records[1].Entry.Message);
        }

        [Test]
        public void CopyTo_AppendsAndDoesNotClearOutput()
        {
            var buffer = new LogBuffer(1);
            buffer.Add(new LogEntry(LogLevel.Info, "A", "one"));

            var records = new List<LogRecord>
            {
                new LogRecord(99, new LogEntry(LogLevel.Debug, "Existing", "existing"))
            };
            buffer.CopyTo(records);

            Assert.AreEqual(2, records.Count);
            Assert.AreEqual(99, records[0].Sequence);
            Assert.AreEqual(1, records[1].Sequence);
        }

        [Test]
        public void Constructor_WhenCapacityIsInvalid_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LogBuffer(0));
        }
    }
}
