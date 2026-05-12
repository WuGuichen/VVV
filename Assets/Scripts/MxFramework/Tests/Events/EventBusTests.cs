using System;
using MxFramework.Events;
using NUnit.Framework;

namespace MxFramework.Tests.Events
{
    public readonly struct TestEvent
    {
        public readonly int Value;

        public TestEvent(int value)
        {
            Value = value;
        }
    }

    public class EventBusTests
    {
        [Test]
        public void Publish_InvokesSubscribersInOrder()
        {
            var bus = new EventBus<TestEvent>();
            int result = 0;

            bus.Subscribe(e => result = result * 10 + e.Value);
            bus.Subscribe(e => result = result * 10 + e.Value + 1);

            bus.Publish(new TestEvent(2));

            Assert.AreEqual(23, result);
        }

        [Test]
        public void Dispose_RemovesSubscription()
        {
            var bus = new EventBus<TestEvent>();
            int calls = 0;
            IDisposable subscription = bus.Subscribe(_ => calls++);

            subscription.Dispose();
            bus.Publish(new TestEvent(1));

            Assert.AreEqual(0, calls);
            Assert.AreEqual(0, bus.Count);
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var bus = new EventBus<TestEvent>();
            IDisposable subscription = bus.Subscribe(_ => { });

            subscription.Dispose();
            subscription.Dispose();

            Assert.AreEqual(0, bus.Count);
        }

        [Test]
        public void Unsubscribe_RemovesFirstMatchingHandler()
        {
            var bus = new EventBus<TestEvent>();
            int calls = 0;
            Action<TestEvent> handler = _ => calls++;
            bus.Subscribe(handler);
            bus.Subscribe(handler);

            Assert.IsTrue(bus.Unsubscribe(handler));
            bus.Publish(new TestEvent(1));

            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, bus.Count);
        }

        [Test]
        public void Unsubscribe_WhenHandlerMissing_ReturnsFalse()
        {
            var bus = new EventBus<TestEvent>();

            Assert.IsFalse(bus.Unsubscribe(_ => { }));
        }

        [Test]
        public void Subscribe_WhenHandlerIsNull_Throws()
        {
            var bus = new EventBus<TestEvent>();

            Assert.Throws<ArgumentNullException>(() => bus.Subscribe(null));
        }

        [Test]
        public void Subscribe_DuringPublish_RunsOnNextPublish()
        {
            var bus = new EventBus<TestEvent>();
            int firstCalls = 0;
            int secondCalls = 0;

            bus.Subscribe(_ =>
            {
                firstCalls++;
                bus.Subscribe(__ => secondCalls++);
            });

            bus.Publish(new TestEvent(1));
            Assert.AreEqual(1, firstCalls);
            Assert.AreEqual(0, secondCalls);

            bus.Publish(new TestEvent(1));
            Assert.AreEqual(2, firstCalls);
            Assert.AreEqual(1, secondCalls);
        }

        [Test]
        public void Dispose_DuringPublish_SkipsPendingHandler()
        {
            var bus = new EventBus<TestEvent>();
            int firstCalls = 0;
            int secondCalls = 0;
            IDisposable second = null;

            bus.Subscribe(_ =>
            {
                firstCalls++;
                second.Dispose();
            });
            second = bus.Subscribe(_ => secondCalls++);

            bus.Publish(new TestEvent(1));

            Assert.AreEqual(1, firstCalls);
            Assert.AreEqual(0, secondCalls);
            Assert.AreEqual(1, bus.Count);
        }

        [Test]
        public void Publish_WhenHandlerThrows_PropagatesAndStops()
        {
            var bus = new EventBus<TestEvent>();
            int laterCalls = 0;
            bus.Subscribe(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe(_ => laterCalls++);

            Assert.Throws<InvalidOperationException>(() => bus.Publish(new TestEvent(1)));
            Assert.AreEqual(0, laterCalls);
        }
    }
}
