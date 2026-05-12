using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeTimerSchedulerTests
    {
        [Test]
        public void ScheduleFrames_FiresOnTargetFrame()
        {
            var scheduler = new RuntimeTimerScheduler();
            var fired = new List<long>();

            scheduler.ScheduleFrames(2, context => fired.Add(context.Frame.Value), "delay-2");

            Tick(scheduler, 0);
            Tick(scheduler, 1);
            CollectionAssert.IsEmpty(fired);

            Tick(scheduler, 2);

            CollectionAssert.AreEqual(new[] { 2L }, fired);
            Assert.AreEqual(0, scheduler.PendingCount);
        }

        [Test]
        public void ScheduleFrames_ZeroDelayFiresOnNextTick()
        {
            var scheduler = new RuntimeTimerScheduler();
            int fired = 0;

            scheduler.ScheduleFrames(0, _ => fired++, "zero");

            Assert.AreEqual(0, fired);

            Tick(scheduler, 0);

            Assert.AreEqual(1, fired);
        }

        [Test]
        public void ScheduleSeconds_UsesExplicitDeltaTime()
        {
            var scheduler = new RuntimeTimerScheduler();
            var fired = new List<long>();

            scheduler.ScheduleSeconds(0.5d, context => fired.Add(context.Frame.Value), "seconds");

            Tick(scheduler, 0, 0.2d);
            CollectionAssert.IsEmpty(fired);

            Tick(scheduler, 1, 0.29d);
            CollectionAssert.IsEmpty(fired);

            Tick(scheduler, 2, 0.01d);
            CollectionAssert.AreEqual(new[] { 2L }, fired);
        }

        [Test]
        public void ScheduleRepeatingFrames_ReschedulesUntilCancelled()
        {
            var scheduler = new RuntimeTimerScheduler();
            var fired = new List<long>();
            RuntimeTimerHandle handle = RuntimeTimerHandle.Invalid;

            handle = scheduler.ScheduleRepeatingFrames(2, context =>
            {
                fired.Add(context.Frame.Value);
                if (fired.Count == 3)
                {
                    Assert.IsTrue(context.Scheduler.Cancel(handle));
                }
            }, "repeat");

            for (int frame = 0; frame <= 8; frame++)
            {
                Tick(scheduler, frame);
            }

            CollectionAssert.AreEqual(new[] { 2L, 4L, 6L }, fired);
            Assert.AreEqual(0, scheduler.PendingCount);
        }

        [Test]
        public void Cancel_PreventsTimerAndRejectsMissingHandles()
        {
            var scheduler = new RuntimeTimerScheduler();
            int fired = 0;
            RuntimeTimerHandle handle = scheduler.ScheduleFrames(1, _ => fired++, "cancel");

            Assert.IsTrue(scheduler.Cancel(handle));
            Assert.IsFalse(scheduler.Cancel(handle));
            Assert.IsFalse(scheduler.Cancel(RuntimeTimerHandle.Invalid));

            Tick(scheduler, 1);
            Assert.AreEqual(0, fired);
        }

        [Test]
        public void SameFrameTimers_FireInScheduleOrder()
        {
            var scheduler = new RuntimeTimerScheduler();
            var traceIds = new List<string>();

            scheduler.ScheduleFrames(1, context => traceIds.Add(context.TraceId), "a");
            scheduler.ScheduleFrames(1, context => traceIds.Add(context.TraceId), "b");
            scheduler.ScheduleFrames(1, context => traceIds.Add(context.TraceId), "c");

            Tick(scheduler, 1);

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, traceIds);
        }

        [Test]
        public void CallbackCanScheduleTimerWithoutCorruptingCurrentDrain()
        {
            var scheduler = new RuntimeTimerScheduler();
            var traceIds = new List<string>();

            scheduler.ScheduleFrames(0, context =>
            {
                traceIds.Add(context.TraceId);
                context.Scheduler.ScheduleFrames(0, nested => traceIds.Add(nested.TraceId), "nested");
            }, "outer");
            scheduler.ScheduleFrames(0, context => traceIds.Add(context.TraceId), "peer");

            Tick(scheduler, 0);
            CollectionAssert.AreEqual(new[] { "outer", "peer" }, traceIds);

            Tick(scheduler, 1);
            CollectionAssert.AreEqual(new[] { "outer", "peer", "nested" }, traceIds);
        }

        [Test]
        public void ScheduleCommand_DefaultsToNextFrameAndPreservesPayload()
        {
            var scheduler = new RuntimeTimerScheduler();
            var buffer = new RuntimeCommandBuffer();
            var command = new RuntimeCommand(
                RuntimeFrame.Zero,
                sourceId: 7,
                commandId: 9,
                targetId: 11,
                payload0: 13,
                payload1: 15,
                payload2: 17,
                traceId: "command-trace");

            scheduler.ScheduleCommand(2, buffer, command, "timer-trace");

            Tick(scheduler, 1);
            Assert.AreEqual(0, buffer.PendingCount);

            Tick(scheduler, 2);
            Assert.AreEqual(1, buffer.PendingCount);

            IReadOnlyList<RuntimeCommand> drained = buffer.DrainForFrame(new RuntimeFrame(3));
            Assert.AreEqual(1, drained.Count);
            Assert.AreEqual(new RuntimeFrame(3), drained[0].Frame);
            Assert.AreEqual(7, drained[0].SourceId);
            Assert.AreEqual(9, drained[0].CommandId);
            Assert.AreEqual(11, drained[0].TargetId);
            Assert.AreEqual(13, drained[0].Payload0);
            Assert.AreEqual(15, drained[0].Payload1);
            Assert.AreEqual(17, drained[0].Payload2);
            Assert.AreEqual("command-trace", drained[0].TraceId);
        }

        [Test]
        public void ScheduleCommand_DueFramePolicyCanTargetDueFrame()
        {
            var scheduler = new RuntimeTimerScheduler();
            var buffer = new RuntimeCommandBuffer();
            var command = new RuntimeCommand(RuntimeFrame.Zero, sourceId: 1, commandId: 2, targetId: 3);

            scheduler.ScheduleCommand(2, buffer, command, "timer-trace", RuntimeScheduledCommandFramePolicy.DueFrame);

            Tick(scheduler, 2);

            IReadOnlyList<RuntimeCommand> drained = buffer.DrainForFrame(new RuntimeFrame(2));
            Assert.AreEqual(1, drained.Count);
            Assert.AreEqual(new RuntimeFrame(2), drained[0].Frame);
        }

        [Test]
        public void ScheduleCommand_NextFramePolicyAvoidsLateCommandWhenDueFrameWasAlreadyDrained()
        {
            var scheduler = new RuntimeTimerScheduler();
            var buffer = new RuntimeCommandBuffer();
            var command = new RuntimeCommand(RuntimeFrame.Zero, sourceId: 1, commandId: 2, targetId: 3);

            scheduler.ScheduleCommand(2, buffer, command, "timer-trace");

            buffer.DrainForFrame(new RuntimeFrame(2));
            Assert.DoesNotThrow(() => Tick(scheduler, 2));

            IReadOnlyList<RuntimeCommand> drained = buffer.DrainForFrame(new RuntimeFrame(3));
            Assert.AreEqual(1, drained.Count);
            Assert.AreEqual(new RuntimeFrame(3), drained[0].Frame);
        }

        [Test]
        public void Cancel_StaleHandleFailsAfterSlotReuse()
        {
            var scheduler = new RuntimeTimerScheduler();

            RuntimeTimerHandle first = scheduler.ScheduleFrames(10, _ => { }, "first");
            Assert.IsTrue(scheduler.Cancel(first));

            RuntimeTimerHandle second = scheduler.ScheduleFrames(10, _ => { }, "second");

            Assert.AreEqual(first.Index, second.Index);
            Assert.AreNotEqual(first.Generation, second.Generation);
            Assert.IsFalse(scheduler.Cancel(first));
            Assert.IsTrue(scheduler.Cancel(second));
        }

        [Test]
        public void NegativeDelayOrIntervalThrows()
        {
            var scheduler = new RuntimeTimerScheduler();
            var buffer = new RuntimeCommandBuffer();

            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleFrames(-1, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleSeconds(-0.1d, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleSeconds(double.NaN, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleSeconds(double.PositiveInfinity, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleRepeatingFrames(0, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleRepeatingFrames(-1, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleCommand(-1, buffer, new RuntimeCommand(RuntimeFrame.Zero, 1, 1, 1)));
        }

        [Test]
        public void Tick_RejectsInvalidDeltaTime()
        {
            var scheduler = new RuntimeTimerScheduler();

            Assert.Throws<ArgumentOutOfRangeException>(() => Tick(scheduler, 0, -0.01d));
            Assert.Throws<ArgumentOutOfRangeException>(() => Tick(scheduler, 0, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => Tick(scheduler, 0, double.PositiveInfinity));
        }

        [Test]
        public void RuntimeHostTicksSchedulerAsModule()
        {
            var scheduler = new RuntimeTimerScheduler();
            var host = new RuntimeHost();
            int fired = 0;

            scheduler.ScheduleFrames(1, _ => fired++, "host");
            host.RegisterModule(scheduler);
            host.Initialize();
            host.Start();

            host.Tick(1, 0d);

            Assert.AreEqual(1, fired);
        }

        private static void Tick(RuntimeTimerScheduler scheduler, long frame, double deltaTime = 0d)
        {
            scheduler.Tick(new RuntimeTickContext(frame, deltaTime, deltaTime, RuntimeTickStage.Simulation));
        }
    }
}
