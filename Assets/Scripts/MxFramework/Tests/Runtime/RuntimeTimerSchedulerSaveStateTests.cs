using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeTimerSchedulerSaveStateTests
    {
        [Test]
        public void Snapshot_IncludesTraceRangeKindAndRepeating()
        {
            var scheduler = new RuntimeTimerScheduler();

            RuntimeTimerHandle frameHandle = scheduler.ScheduleFrames(5, _ => { }, "frame-trace");
            RuntimeTimerHandle repeatHandle = scheduler.ScheduleRepeatingFrames(3, _ => { }, "repeat-trace");
            RuntimeTimerHandle secondsHandle = scheduler.ScheduleSeconds(1.5d, _ => { }, "seconds-trace");

            Tick(scheduler, 1, 0.25d);

            RuntimeTimerSchedulerSnapshot snapshot = scheduler.CreateSnapshot();

            Assert.AreEqual(new RuntimeFrame(1), snapshot.CurrentFrame);
            Assert.AreEqual(3, snapshot.PendingCount);

            RuntimeTimerSnapshotEntry repeat = Find(snapshot, repeatHandle);
            Assert.AreEqual(RuntimeTimerKind.Frames, repeat.Kind);
            Assert.AreEqual(new RuntimeFrame(3), repeat.TargetFrame);
            Assert.AreEqual(2, repeat.RemainingFrames);
            Assert.AreEqual(3, repeat.IntervalFrames);
            Assert.IsTrue(repeat.IsRepeating);
            Assert.AreEqual("repeat-trace", repeat.TraceId);

            RuntimeTimerSnapshotEntry frame = Find(snapshot, frameHandle);
            Assert.AreEqual(new RuntimeFrame(5), frame.TargetFrame);
            Assert.AreEqual(4, frame.RemainingFrames);
            Assert.IsFalse(frame.IsRepeating);
            Assert.AreEqual("frame-trace", frame.TraceId);

            RuntimeTimerSnapshotEntry seconds = Find(snapshot, secondsHandle);
            Assert.AreEqual(RuntimeTimerKind.Seconds, seconds.Kind);
            Assert.AreEqual(0, seconds.RemainingFrames);
            Assert.AreEqual(1.25d, seconds.RemainingSeconds, 0.000001d);
            Assert.AreEqual("seconds-trace", seconds.TraceId);
        }

        [Test]
        public void CreateStateSummary_ExposesDiagnosticTimerSummaries()
        {
            var scheduler = new RuntimeTimerScheduler();
            var buffer = new RuntimeCommandBuffer();
            var command = new RuntimeCommand(
                RuntimeFrame.Zero,
                sourceId: 1,
                commandId: 2,
                targetId: 3,
                payload0: 4,
                payload1: 5,
                payload2: 6,
                traceId: "command-trace");

            scheduler.ScheduleFrames(4, _ => { }, "frame-trace");
            scheduler.ScheduleSeconds(2d, _ => { }, "seconds-trace");
            scheduler.ScheduleCommand(6, buffer, command, "command-timer");

            Tick(scheduler, 1, 0.5d);

            RuntimeTimerSchedulerStateSummary state = scheduler.CreateStateSummary();

            Assert.AreEqual(RuntimeTimerScheduler.StateSchemaVersion, state.SchemaVersion);
            Assert.IsFalse(state.IsRestorable);
            Assert.AreEqual(1, state.CurrentFrame);
            Assert.AreEqual(3, state.Timers.Count);
            Assert.GreaterOrEqual(state.NextTimerId, 3);
            Assert.GreaterOrEqual(state.NextSequence, 3);

            Assert.AreEqual("seconds-trace", state.Timers[0].TraceId);
            Assert.AreEqual(RuntimeTimerKind.Seconds, state.Timers[0].Kind);
            Assert.AreEqual(1.5d, state.Timers[0].RemainingSeconds, 0.000001d);

            Assert.AreEqual("frame-trace", state.Timers[1].TraceId);
            Assert.AreEqual(3, state.Timers[1].RemainingFrames);

            Assert.AreEqual("command-timer", state.Timers[2].TraceId);
            Assert.AreEqual(RuntimeTimerKind.Command, state.Timers[2].Kind);
            Assert.IsFalse(state.Timers[2].IsRestorable);
            StringAssert.Contains("Frame=7", state.Timers[2].CommandSummary);
            StringAssert.Contains("CommandId=2", state.Timers[2].CommandSummary);
            StringAssert.Contains("TraceId=command-trace", state.Timers[2].CommandSummary);
        }

        [Test]
        public void CreateState_ReturnsBackwardCompatibleSummaryAlias()
        {
            var scheduler = new RuntimeTimerScheduler();

            RuntimeTimerSchedulerState state = scheduler.CreateState();

            Assert.IsFalse(state.IsRestorable);
            Assert.AreEqual(0, state.Timers.Count);
        }

        private static RuntimeTimerSnapshotEntry Find(RuntimeTimerSchedulerSnapshot snapshot, RuntimeTimerHandle handle)
        {
            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                if (snapshot.Entries[i].Handle == handle)
                {
                    return snapshot.Entries[i];
                }
            }

            Assert.Fail("Snapshot entry was not found for handle " + handle + ".");
            return default;
        }

        private static void Tick(RuntimeTimerScheduler scheduler, long frame, double deltaTime = 0d)
        {
            scheduler.Tick(new RuntimeTickContext(frame, deltaTime, deltaTime, RuntimeTickStage.Simulation));
        }
    }
}
