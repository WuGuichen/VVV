using System.Collections.Generic;
using MxFramework.Logging;
using NUnit.Framework;

namespace MxFramework.Tests.Logging
{
    public class NullLoggerTests
    {
        [Test]
        public void Log_DropsEntries()
        {
            var logger = new NullLogger();

            Assert.IsFalse(logger.IsEnabled(LogLevel.Critical, "Runtime"));
            logger.Log(new LogEntry(LogLevel.Critical, "Runtime", "ignored"));
        }

        [Test]
        public void BufferedLogSink_WritesToBuffer()
        {
            var buffer = new LogBuffer(2);
            var sink = new BufferedLogSink(buffer);

            sink.Write(new LogEntry(LogLevel.Info, "Runtime", "stored"));

            var records = new List<LogRecord>();
            buffer.CopyTo(records);
            Assert.AreEqual(1, records.Count);
            Assert.AreEqual("stored", records[0].Entry.Message);
        }
    }
}
