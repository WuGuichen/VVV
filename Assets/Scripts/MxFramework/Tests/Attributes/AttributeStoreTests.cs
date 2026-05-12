using System;
using MxFramework.Attributes;
using NUnit.Framework;

namespace MxFramework.Tests.Attributes
{
    public class AttributeStoreTests
    {
        [Test]
        public void RegisterAttribute_InitializesBaseAndFinalValues()
        {
            var store = new AttributeStore();

            store.RegisterAttribute(100, 25);

            Assert.IsTrue(store.TryGetAttributeValue(100, out AttributeValue value));
            Assert.AreEqual(25, value.BaseValue);
            Assert.AreEqual(25, value.FinalValue);
        }

        [Test]
        public void GetAttribute_WhenMissing_ReturnsZero()
        {
            var store = new AttributeStore();

            Assert.AreEqual(0, store.GetAttribute(999));
            Assert.IsFalse(store.TryGetAttribute(999, out int finalValue));
            Assert.AreEqual(0, finalValue);
        }

        [Test]
        public void SetAttribute_ChangesBaseAndPublishesEvent()
        {
            var store = new AttributeStore();
            store.RegisterAttribute(1, 10);
            AttributeChangedEvent received = default;
            int calls = 0;
            object source = new object();
            store.OnAttributeChanged.Subscribe(e =>
            {
                received = e;
                calls++;
            });

            store.SetAttribute(1, 15, source);

            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, received.AttributeId);
            Assert.AreEqual(15, received.BaseValue);
            Assert.AreEqual(10, received.OldValue);
            Assert.AreEqual(15, received.NewValue);
            Assert.AreEqual(5, received.Delta);
            Assert.AreSame(source, received.Source);
        }

        [Test]
        public void SetAttribute_WhenFinalValueUnchanged_DoesNotPublish()
        {
            var store = new AttributeStore();
            store.RegisterAttribute(1, 10);
            int calls = 0;
            store.OnAttributeChanged.Subscribe(_ => calls++);

            store.SetAttribute(1, 10);

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void AddAttribute_AddsToBaseValue()
        {
            var store = new AttributeStore();
            store.RegisterAttribute(1, 10);

            store.AddAttribute(1, 5);

            Assert.IsTrue(store.TryGetAttributeValue(1, out AttributeValue value));
            Assert.AreEqual(15, value.BaseValue);
            Assert.AreEqual(15, value.FinalValue);
        }

        [Test]
        public void AddModifier_RecomputesFinalValueAndPublishesEvents()
        {
            var store = new AttributeStore();
            store.RegisterAttribute(1, 10);
            var modifier = new TestModifier(10, 1, AttributeModifierPhase.Add, 0, value => value + 5);
            int changedCalls = 0;
            int modifierCalls = 0;
            store.OnAttributeChanged.Subscribe(e =>
            {
                changedCalls++;
                Assert.AreEqual(10, e.OldValue);
                Assert.AreEqual(15, e.NewValue);
                Assert.AreSame(modifier, e.Source);
            });
            store.OnModifierChanged.Subscribe(e =>
            {
                modifierCalls++;
                Assert.AreEqual(10, e.ModifierId);
                Assert.AreEqual(1, e.AttributeId);
                Assert.IsTrue(e.IsAdded);
            });

            store.AddModifier(modifier);

            Assert.AreEqual(15, store.GetAttribute(1));
            Assert.AreEqual(1, changedCalls);
            Assert.AreEqual(1, modifierCalls);
        }

        [Test]
        public void Modifiers_AreAppliedByPhaseThenPriorityThenId()
        {
            var store = new AttributeStore();
            store.RegisterAttribute(1, 10);

            store.AddModifier(new TestModifier(30, 1, AttributeModifierPhase.Multiply, 0, value => value * 2));
            store.AddModifier(new TestModifier(20, 1, AttributeModifierPhase.Add, 20, value => value + 3));
            store.AddModifier(new TestModifier(10, 1, AttributeModifierPhase.Add, 10, value => value + 1));

            Assert.AreEqual(28, store.GetAttribute(1));
        }

        [Test]
        public void RemoveModifier_RecomputesFinalValue()
        {
            var store = new AttributeStore();
            store.RegisterAttribute(1, 10);
            store.AddModifier(new TestModifier(10, 1, AttributeModifierPhase.Add, 0, value => value + 5));
            int removedEvents = 0;
            store.OnModifierChanged.Subscribe(e =>
            {
                if (!e.IsAdded)
                    removedEvents++;
            });

            Assert.IsTrue(store.RemoveModifier(10));

            Assert.AreEqual(10, store.GetAttribute(1));
            Assert.AreEqual(1, removedEvents);
        }

        [Test]
        public void ClearModifiers_RecomputesAffectedAttributes()
        {
            var store = new AttributeStore();
            store.RegisterAttribute(1, 10);
            store.RegisterAttribute(2, 20);
            store.AddModifier(new TestModifier(10, 1, AttributeModifierPhase.Add, 0, value => value + 5));
            store.AddModifier(new TestModifier(20, 2, AttributeModifierPhase.Add, 0, value => value + 5));

            store.ClearModifiers();

            Assert.AreEqual(10, store.GetAttribute(1));
            Assert.AreEqual(20, store.GetAttribute(2));
        }

        [Test]
        public void AddModifier_ReplacesExistingModifierId()
        {
            var store = new AttributeStore();
            store.RegisterAttribute(1, 10);
            store.AddModifier(new TestModifier(10, 1, AttributeModifierPhase.Add, 0, value => value + 5));

            store.AddModifier(new TestModifier(10, 1, AttributeModifierPhase.Add, 0, value => value + 2));

            Assert.AreEqual(12, store.GetAttribute(1));
        }

        [Test]
        public void AddModifier_WhenModifierIsNull_Throws()
        {
            var store = new AttributeStore();

            Assert.Throws<ArgumentNullException>(() => store.AddModifier(null));
        }

        private sealed class TestModifier : IAttributeModifier
        {
            private readonly Func<int, int> _modify;

            public TestModifier(int id, int attributeId, AttributeModifierPhase phase, int priority, Func<int, int> modify)
            {
                Id = id;
                AttributeId = attributeId;
                Phase = phase;
                Priority = priority;
                _modify = modify;
            }

            public int Id { get; }
            public int AttributeId { get; }
            public AttributeModifierPhase Phase { get; }
            public int Priority { get; }

            public int Modify(int currentValue, IAttributeOwner owner)
            {
                return _modify(currentValue);
            }
        }
    }
}
