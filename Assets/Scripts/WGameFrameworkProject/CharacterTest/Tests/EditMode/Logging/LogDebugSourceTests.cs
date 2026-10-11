using MxFramework.Diagnostics;
using MxFramework.Logging;
using MxFramework.Logging.Diagnostics;
using NUnit.Framework;

namespace MxFramework.Tests.Logging
{
    public class LogDebugSourceTests
    {
        [Test]
        public void CreateSnapshot_ExportsLoggingRuntimeSnapshotWithSingleLogsSection()
        {
            var buffer = new LogBuffer(4);
            buffer.Add(new LogEntry(LogLevel.Info, "Runtime", "started"));
            buffer.Add(new LogEntry(LogLevel.Error, "Combat", "failed", 12));

            var source = new LogDebugSource(buffer);
            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

            Assert.AreEqual("Logging", source.Name);
            Assert.AreEqual("Logging", snapshot.SourceName);
            Assert.AreEqual(FrameworkDebugMode.Runtime, source.Mode);
            Assert.AreEqual(FrameworkDebugMode.Runtime, snapshot.Mode);
            Assert.IsTrue(source.IsAvailable);
            Assert.AreEqual(1, snapshot.Sections.Count);
            Assert.AreEqual("Logs", snapshot.Sections[0].Title);
            StringAssert.Contains("[Info] [Runtime] started", snapshot.Sections[0].Body);
            StringAssert.Contains("[Error] [Combat] failed frame=12", snapshot.Sections[0].Body);
        }
    }
}
