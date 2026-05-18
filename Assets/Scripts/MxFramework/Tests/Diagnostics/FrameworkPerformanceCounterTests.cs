using MxFramework.Diagnostics;
using NUnit.Framework;

namespace MxFramework.Tests.Diagnostics
{
    public sealed class FrameworkPerformanceCounterTests
    {
        [Test]
        public void Recorder_DefaultsDisabledAndDoesNotStoreCounters()
        {
            var recorder = new FrameworkPerformanceCounterRecorder();

            bool recorded = recorder.AddCounter("runtime.tick", "Tick", "Runtime", 1);

            Assert.IsFalse(recorded);
            Assert.AreEqual(0, recorder.Count);
            Assert.IsFalse(recorder.CreateSnapshot().Enabled);
        }

        [Test]
        public void Recorder_WhenEnabledStoresStableSnapshot()
        {
            var recorder = new FrameworkPerformanceCounterRecorder();
            recorder.SetEnabled(true);
            recorder.AddCounter("runtime.tick", "Tick", "Runtime", 1);
            recorder.AddCounter("runtime.tick", "Tick", "Runtime", 2);
            recorder.SetCounter(new FrameworkPerformanceCounterSample("combat.hits", "Hits", "Combat", 4));

            FrameworkPerformanceCounterSnapshot snapshot = recorder.CreateSnapshot("Counters");

            Assert.IsTrue(snapshot.Enabled);
            Assert.AreEqual(2, snapshot.Count);
            Assert.AreEqual("combat.hits", snapshot.Samples[0].CounterId);
            Assert.AreEqual("runtime.tick", snapshot.Samples[1].CounterId);
            Assert.AreEqual(3, snapshot.Samples[1].Value);
        }

        [Test]
        public void DebugSource_ExportsCounterSections()
        {
            var snapshot = new FrameworkPerformanceCounterSnapshot(
                "Runtime",
                true,
                new[] { new FrameworkPerformanceCounterSample("runtime.tick", "Tick", "Runtime", 10, "ticks") });

            FrameworkDebugSnapshot debug = new FrameworkPerformanceCounterDebugSource(() => snapshot).CreateSnapshot();

            Assert.AreEqual("Performance", debug.SourceName);
            Assert.That(debug.Sections[0].Body, Does.Contain("enabled: true"));
            Assert.That(debug.Sections[1].Body, Does.Contain("runtime.tick"));
        }
    }
}
