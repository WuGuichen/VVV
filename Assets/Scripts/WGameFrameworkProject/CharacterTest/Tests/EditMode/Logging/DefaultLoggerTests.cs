using System.Collections.Generic;
using MxFramework.Logging;
using NUnit.Framework;

namespace MxFramework.Tests.Logging
{
    public class DefaultLoggerTests
    {
        [Test]
        public void Log_FiltersByMinLevelAndExactCategoryAllowlist()
        {
            var buffer = new LogBuffer(8);
            var logger = new DefaultLogger(
                LogLevel.Warning,
                new ILogSink[] { new BufferedLogSink(buffer) },
                new[] { "Combat" });

            logger.Log(new LogEntry(LogLevel.Info, "Combat", "too low"));
            logger.Log(new LogEntry(LogLevel.Error, "combat", "wrong case"));
            logger.Log(new LogEntry(LogLevel.Error, "Combat", "accepted"));

            var records = new List<LogRecord>();
            buffer.CopyTo(records);

            Assert.AreEqual(1, records.Count);
            Assert.AreEqual("accepted", records[0].Entry.Message);
        }

        [Test]
        public void Log_WhenAllowlistIsEmpty_AllowsAllCategories()
        {
            var buffer = new LogBuffer(8);
            var logger = new DefaultLogger(
                LogLevel.Debug,
                new ILogSink[] { new BufferedLogSink(buffer) },
                new string[0]);

            logger.Log(new LogEntry(LogLevel.Debug, "A", "a"));
            logger.Log(new LogEntry(LogLevel.Debug, "B", "b"));

            var records = new List<LogRecord>();
            buffer.CopyTo(records);

            Assert.AreEqual(2, records.Count);
        }

        [Test]
        public void Log_FansOutToMultipleSinksWithBufferLocalSequences()
        {
            var first = new LogBuffer(8);
            var second = new LogBuffer(8);
            first.Add(new LogEntry(LogLevel.Info, "Pre", "existing"));

            var logger = new DefaultLogger(
                LogLevel.Trace,
                new ILogSink[] { new BufferedLogSink(first), new BufferedLogSink(second) });

            logger.Log(new LogEntry(LogLevel.Error, "Runtime", "boom"));

            var firstRecords = new List<LogRecord>();
            var secondRecords = new List<LogRecord>();
            first.CopyTo(firstRecords);
            second.CopyTo(secondRecords);

            Assert.AreEqual(2, firstRecords[1].Sequence);
            Assert.AreEqual(1, secondRecords[0].Sequence);
            Assert.AreEqual("boom", firstRecords[1].Entry.Message);
            Assert.AreEqual("boom", secondRecords[0].Entry.Message);
        }

        [Test]
        public void Constructor_WhenSinksContainNull_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new DefaultLogger(LogLevel.Info, new ILogSink[] { null }));
        }
    }
}
