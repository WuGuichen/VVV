using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeEventQueueTests
    {
        [Test]
        public void Drain_SameFrameKeepsEnqueueOrder()
        {
            var queue = new RuntimeEventQueue<TestRuntimeEvent>();
            RuntimeFrame frame = RuntimeFrame.Zero;

            queue.Enqueue(frame, Event("first"));
            queue.Enqueue(frame, Event("second"));
            queue.Enqueue(frame, Event("third"));

            var output = new List<TestRuntimeEvent>();
            int drained = queue.Drain(frame, output);

            Assert.AreEqual(3, drained);
            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, Names(output));
            Assert.AreEqual(0, queue.PendingCount);
        }

        [Test]
        public void Drain_KeepsFutureFramesUntilRequestedFrame()
        {
            var queue = new RuntimeEventQueue<TestRuntimeEvent>();
            RuntimeFrame frame0 = RuntimeFrame.Zero;
            RuntimeFrame frame1 = new RuntimeFrame(1);

            queue.Enqueue(frame1, Event("future"));
            queue.Enqueue(frame0, Event("now"));

            var output = new List<TestRuntimeEvent>();
            int firstDrained = queue.Drain(frame0, output);

            Assert.AreEqual(1, firstDrained);
            CollectionAssert.AreEqual(new[] { "now" }, Names(output));
            Assert.AreEqual(1, queue.PendingCount);

            int secondDrained = queue.Drain(frame1, output);

            Assert.AreEqual(1, secondDrained);
            CollectionAssert.AreEqual(new[] { "now", "future" }, Names(output));
            Assert.AreEqual(0, queue.PendingCount);
        }

        [Test]
        public void Drain_DrainsEarlierFramesWhenFrameAdvances()
        {
            var queue = new RuntimeEventQueue<TestRuntimeEvent>();
            RuntimeFrame frame1 = new RuntimeFrame(1);
            RuntimeFrame frame2 = new RuntimeFrame(2);
            RuntimeFrame frame3 = new RuntimeFrame(3);

            queue.Enqueue(frame3, Event("frame-3"));
            queue.Enqueue(frame1, Event("frame-1"));
            queue.Enqueue(frame2, Event("frame-2"));

            var output = new List<TestRuntimeEvent>();
            int drained = queue.Drain(frame3, output);

            Assert.AreEqual(3, drained);
            CollectionAssert.AreEqual(new[] { "frame-1", "frame-2", "frame-3" }, Names(output));
            Assert.AreEqual(0, queue.PendingCount);
        }

        [Test]
        public void Drain_AppendsToExistingOutput()
        {
            var queue = new RuntimeEventQueue<TestRuntimeEvent>();
            queue.Enqueue(RuntimeFrame.Zero, Event("queued"));
            var output = new List<TestRuntimeEvent> { Event("existing") };

            int drained = queue.Drain(RuntimeFrame.Zero, output);

            Assert.AreEqual(1, drained);
            CollectionAssert.AreEqual(new[] { "existing", "queued" }, Names(output));
        }

        [Test]
        public void CreateSnapshot_ReportsPendingRangeAndNextSequence()
        {
            var queue = new RuntimeEventQueue<TestRuntimeEvent>();
            queue.Enqueue(new RuntimeFrame(5), Event("newest"));
            queue.Enqueue(new RuntimeFrame(2), Event("oldest"));

            RuntimeEventQueueSnapshot snapshot = queue.CreateSnapshot();

            Assert.IsTrue(snapshot.HasPending);
            Assert.AreEqual(2, snapshot.PendingCount);
            Assert.AreEqual(new RuntimeFrame(2), snapshot.OldestFrame);
            Assert.AreEqual(new RuntimeFrame(5), snapshot.NewestFrame);
            Assert.AreEqual(2L, snapshot.NextSequence);
            Assert.AreEqual(typeof(TestRuntimeEvent).FullName, snapshot.EventTypeName);
        }

        [Test]
        public void Drain_RequiresOutputList()
        {
            var queue = new RuntimeEventQueue<TestRuntimeEvent>();

            Assert.Throws<ArgumentNullException>(() => queue.Drain(RuntimeFrame.Zero, null));
        }

        [Test]
        public void Clear_RemovesPendingEventsAndResetsSequence()
        {
            var queue = new RuntimeEventQueue<TestRuntimeEvent>();
            queue.Enqueue(RuntimeFrame.Zero, Event("first"));
            queue.Enqueue(new RuntimeFrame(1), Event("second"));

            queue.Clear();
            RuntimeEventQueueSnapshot snapshot = queue.CreateSnapshot();

            Assert.IsFalse(snapshot.HasPending);
            Assert.AreEqual(0, snapshot.PendingCount);
            Assert.AreEqual(RuntimeFrame.Zero, snapshot.OldestFrame);
            Assert.AreEqual(RuntimeFrame.Zero, snapshot.NewestFrame);
            Assert.AreEqual(0L, snapshot.NextSequence);
        }

        private static TestRuntimeEvent Event(string name)
        {
            return new TestRuntimeEvent(name);
        }

        private static string[] Names(IReadOnlyList<TestRuntimeEvent> events)
        {
            var names = new string[events.Count];
            for (int i = 0; i < events.Count; i++)
            {
                names[i] = events[i].Name;
            }

            return names;
        }

        private readonly struct TestRuntimeEvent
        {
            public TestRuntimeEvent(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }
    }
}
