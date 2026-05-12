using System;
using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Events;
using NUnit.Framework;

namespace MxFramework.Tests.Buffs
{
    public class BuffPipelineTests
    {
        [Test]
        public void AddBuff_AttachesAndPublishesAddedEvent()
        {
            var target = new TestBuffTarget();
            var pipeline = new BuffPipeline();
            var buff = new TestBuff(100, 5f);
            BuffEvent received = default;
            int calls = 0;
            target.BuffEvents.Subscribe(e =>
            {
                received = e;
                calls++;
            });

            IBuff added = pipeline.AddBuff(buff, target);

            Assert.AreSame(buff, added);
            Assert.AreEqual(1, buff.AttachCalls);
            Assert.IsTrue(pipeline.HasBuff(100));
            Assert.AreEqual(1, calls);
            Assert.AreEqual(BuffEventType.Added, received.Type);
            Assert.AreEqual(1, received.LayerDelta);
        }

        [Test]
        public void AddBuff_WhenSameId_StacksAndRefreshesExisting()
        {
            var target = new TestBuffTarget();
            var pipeline = new BuffPipeline();
            var existing = new TestBuff(100, 5f, maxLayers: 3);
            var incoming = new TestBuff(100, 5f, maxLayers: 3);
            int layerEvents = 0;
            int refreshEvents = 0;
            target.BuffEvents.Subscribe(e =>
            {
                if (e.Type == BuffEventType.LayerChanged)
                    layerEvents++;
                if (e.Type == BuffEventType.DurationRefreshed)
                    refreshEvents++;
            });

            pipeline.AddBuff(existing, target);
            pipeline.TickAll(2f);
            IBuff result = pipeline.AddBuff(incoming, target);

            Assert.AreSame(existing, result);
            Assert.AreEqual(1, existing.AttachCalls);
            Assert.AreEqual(0, incoming.AttachCalls);
            Assert.AreEqual(2, existing.CurrentLayers);
            Assert.AreEqual(5f, existing.RemainingTime);
            Assert.AreEqual(1, layerEvents);
            Assert.AreEqual(1, refreshEvents);
        }

        [Test]
        public void TickAll_WhenTimedBuffExpires_RemovesAndDetaches()
        {
            var target = new TestBuffTarget();
            var pipeline = new BuffPipeline();
            var buff = new TestBuff(100, 1f);
            int removedEvents = 0;
            target.BuffEvents.Subscribe(e =>
            {
                if (e.Type == BuffEventType.Removed)
                    removedEvents++;
            });
            pipeline.AddBuff(buff, target);

            pipeline.TickAll(1.1f);

            Assert.IsFalse(pipeline.HasBuff(100));
            Assert.AreEqual(1, buff.TickCalls);
            Assert.AreEqual(1, buff.DetachCalls);
            Assert.AreEqual(1, removedEvents);
        }

        [Test]
        public void TickAll_WhenPermanent_DoesNotExpire()
        {
            var target = new TestBuffTarget();
            var pipeline = new BuffPipeline();
            var buff = new TestBuff(100, 0f, isPermanent: true);
            pipeline.AddBuff(buff, target);

            pipeline.TickAll(100f);

            Assert.IsTrue(pipeline.HasBuff(100));
            Assert.IsTrue(float.IsPositiveInfinity(buff.RemainingTime));
        }

        [Test]
        public void RemoveAllBuffs_DetachesEveryBuff()
        {
            var target = new TestBuffTarget();
            var pipeline = new BuffPipeline();
            var buff1 = new TestBuff(100, 5f);
            var buff2 = new TestBuff(200, 5f);
            pipeline.AddBuff(buff1, target);
            pipeline.AddBuff(buff2, target);

            pipeline.RemoveAllBuffs();

            Assert.IsFalse(pipeline.HasBuff(100));
            Assert.IsFalse(pipeline.HasBuff(200));
            Assert.AreEqual(1, buff1.DetachCalls);
            Assert.AreEqual(1, buff2.DetachCalls);
        }

        [Test]
        public void TryAddBuff_UsesFactory()
        {
            var target = new TestBuffTarget();
            var factory = new TestBuffFactory();
            var pipeline = new BuffPipeline(factory);

            bool added = pipeline.TryAddBuff(300, target, out IBuff buff);

            Assert.IsTrue(added);
            Assert.IsNotNull(buff);
            Assert.AreEqual(300, buff.Id);
            Assert.IsTrue(pipeline.HasBuff(300));
        }

        [Test]
        public void CreateSnapshot_ReturnsStableDebugValues()
        {
            var target = new TestBuffTarget();
            var pipeline = new BuffPipeline();
            var buff = new TestBuff(100, 5f, maxLayers: 3);
            pipeline.AddBuff(buff, target);
            pipeline.AddBuff(new TestBuff(100, 5f, maxLayers: 3), target);
            pipeline.TickAll(1f);

            BuffSnapshot[] snapshots = pipeline.CreateSnapshot();

            Assert.AreEqual(1, snapshots.Length);
            Assert.AreEqual(100, snapshots[0].Id);
            Assert.AreEqual(2, snapshots[0].CurrentLayers);
            Assert.AreEqual(3, snapshots[0].MaxLayers);
            Assert.AreEqual(4f, snapshots[0].RemainingTime);
        }

        [Test]
        public void BuffCanCleanupAttributeModifierOnDetach()
        {
            var target = new TestBuffTarget();
            target.Attributes.RegisterAttribute(1, 10);
            var pipeline = new BuffPipeline();
            var buff = new AttributeBuff(100, 10, 1, 5);

            pipeline.AddBuff(buff, target);
            Assert.AreEqual(15, target.Attributes.GetAttribute(1));

            pipeline.RemoveBuff(100);

            Assert.AreEqual(10, target.Attributes.GetAttribute(1));
        }

        private sealed class TestBuffTarget : IBuffTarget
        {
            private readonly EventBus<BuffEvent> _events = new EventBus<BuffEvent>();

            public TestBuffTarget()
            {
                Attributes = new AttributeStore();
                AttributeModifiers = (IAttributeModifierOwner)Attributes;
            }

            public AttributeStore Attributes { get; }

            IAttributeOwner IBuffTarget.Attributes => Attributes;

            public IAttributeModifierOwner AttributeModifiers { get; }

            public IEventBus<BuffEvent> BuffEvents => _events;
        }

        private sealed class TestBuff : BuffBase
        {
            public TestBuff(int id, float duration, int maxLayers = 1, bool isPermanent = false)
                : base(id, duration, maxLayers, isPermanent)
            {
            }

            public int AttachCalls { get; private set; }
            public int TickCalls { get; private set; }
            public int DetachCalls { get; private set; }

            public override void OnAttach(IBuffTarget target)
            {
                AttachCalls++;
            }

            public override void OnTick(float deltaTime, IBuffTarget target)
            {
                TickCalls++;
                base.OnTick(deltaTime, target);
            }

            public override void OnDetach(IBuffTarget target)
            {
                DetachCalls++;
            }
        }

        private sealed class TestBuffFactory : IBuffFactory
        {
            public bool TryCreate(int buffId, out IBuff buff)
            {
                buff = new TestBuff(buffId, 5f);
                return true;
            }
        }

        private sealed class AttributeBuff : BuffBase
        {
            private readonly TestModifier _modifier;

            public AttributeBuff(int id, int modifierId, int attributeId, int addValue)
                : base(id, 5f)
            {
                _modifier = new TestModifier(modifierId, attributeId, addValue);
            }

            public override void OnAttach(IBuffTarget target)
            {
                target.AttributeModifiers.AddModifier(_modifier);
            }

            public override void OnDetach(IBuffTarget target)
            {
                target.AttributeModifiers.RemoveModifier(_modifier.Id);
            }
        }

        private sealed class TestModifier : IAttributeModifier
        {
            private readonly int _addValue;

            public TestModifier(int id, int attributeId, int addValue)
            {
                Id = id;
                AttributeId = attributeId;
                _addValue = addValue;
            }

            public int Id { get; }
            public int AttributeId { get; }
            public AttributeModifierPhase Phase => AttributeModifierPhase.Add;
            public int Priority => 0;

            public int Modify(int currentValue, IAttributeOwner owner)
            {
                return currentValue + _addValue;
            }
        }
    }
}
