using System;
using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Events;
using MxFramework.Modifiers;
using NUnit.Framework;

namespace MxFramework.Tests.Modifiers
{
    public class ModifierPipelineTests
    {
        [Test]
        public void ApplyAll_WhenConditionFails_DoesNotExecuteEffect()
        {
            var owner = new AttributeStore();
            var pipeline = new ModifierPipeline(owner);
            var effect = new CountEffect();
            pipeline.AddModifier(new ModifierBase(
                100,
                new IModifierCondition[] { new ConstantCondition(false) },
                new IModifierEffect[] { effect }));

            ModifierContext context = ModifierContext.Get();
            pipeline.ApplyAll(context);
            ModifierContext.Push(context);

            Assert.AreEqual(0, effect.Calls);
        }

        [Test]
        public void ApplyAll_ExecutesEffectsInOrder()
        {
            var owner = new AttributeStore();
            var pipeline = new ModifierPipeline(owner);
            var effect1 = new AppendEffect("A");
            var effect2 = new AppendEffect("B");
            var context = ModifierContext.Get();
            context.EnsureExtra();
            context.Extra["trace"] = string.Empty;
            pipeline.AddModifier(new ModifierBase(
                100,
                effects: new IModifierEffect[] { effect1, effect2 }));

            pipeline.ApplyAll(context);

            Assert.AreEqual("AB", context.Extra["trace"]);
            ModifierContext.Push(context);
        }

        [Test]
        public void CounterStore_AddSetReset_PublishesEvents()
        {
            var counters = new CounterStore();
            int calls = 0;
            CounterChangedEvent last = default;
            counters.OnCounterChanged.Subscribe(e =>
            {
                calls++;
                last = e;
            });

            counters.AddCounter(1, 2);
            counters.SetCounter(1, 5);
            counters.ResetCounter(1);

            Assert.AreEqual(3, calls);
            Assert.AreEqual(1, last.CounterId);
            Assert.AreEqual(5, last.OldValue);
            Assert.AreEqual(0, last.NewValue);
            Assert.AreEqual(-5, last.Delta);
        }

        [Test]
        public void ModifierCanUseCountersAsCondition()
        {
            var owner = new AttributeStore();
            var pipeline = new ModifierPipeline(owner);
            var effect = new CountEffect();
            pipeline.AddModifier(new ModifierBase(
                100,
                new IModifierCondition[] { new CounterAtLeastCondition(7, 3) },
                new IModifierEffect[] { effect }));

            pipeline.Counters.AddCounter(7, 2);
            pipeline.ApplyAll(null);
            pipeline.Counters.AddCounter(7, 1);
            pipeline.ApplyAll(null);

            Assert.AreEqual(1, effect.Calls);
        }

        [Test]
        public void TryAddModifier_UsesFactory()
        {
            var owner = new AttributeStore();
            var pipeline = new ModifierPipeline(owner, new TestModifierFactory());

            bool added = pipeline.TryAddModifier(200, out IModifier modifier);

            Assert.IsTrue(added);
            Assert.IsNotNull(modifier);
            Assert.AreEqual(200, modifier.Id);
            Assert.IsTrue(pipeline.HasModifier(200));
        }

        [Test]
        public void RemoveModifier_CallsRemoveAndPublishesEvent()
        {
            var owner = new AttributeStore();
            var pipeline = new ModifierPipeline(owner);
            var modifier = new TrackingModifier(100);
            int removedEvents = 0;
            pipeline.OnModifierEvent.Subscribe(e =>
            {
                if (e.Type == ModifierEventType.Removed)
                    removedEvents++;
            });
            pipeline.AddModifier(modifier);

            bool removed = pipeline.RemoveModifier(100);

            Assert.IsTrue(removed);
            Assert.AreEqual(1, modifier.RemoveCalls);
            Assert.AreEqual(1, removedEvents);
            Assert.IsFalse(pipeline.HasModifier(100));
        }

        [Test]
        public void AddModifier_WhenSameId_ReplacesOldModifier()
        {
            var owner = new AttributeStore();
            var pipeline = new ModifierPipeline(owner);
            var oldModifier = new TrackingModifier(100);
            var newModifier = new TrackingModifier(100);

            pipeline.AddModifier(oldModifier);
            pipeline.AddModifier(newModifier);

            Assert.AreEqual(1, oldModifier.RemoveCalls);
            Assert.AreSame(newModifier, pipeline.GetModifier(100));
            Assert.AreEqual(1, pipeline.CreateSnapshot().Length);
        }

        [Test]
        public void ModifierCanAccessAttributesAndBuffsThroughContext()
        {
            var owner = new AttributeStore();
            owner.RegisterAttribute(1, 10);
            var buffs = new BuffPipeline();
            var target = new TestBuffTarget(owner);
            buffs.AddBuff(new TestBuff(300, 5f), target);
            var pipeline = new ModifierPipeline(owner, buffs: buffs);
            pipeline.AddModifier(new ModifierBase(
                100,
                effects: new IModifierEffect[] { new AttributeAndBuffEffect(1, 5, 300) }));

            pipeline.ApplyAll(null);

            Assert.AreEqual(15, owner.GetAttribute(1));
            Assert.AreEqual(300, owner.GetAttribute(2));
        }

        [Test]
        public void UpdateAll_CallsModifierUpdate()
        {
            var owner = new AttributeStore();
            var pipeline = new ModifierPipeline(owner);
            var modifier = new TrackingModifier(100);
            pipeline.AddModifier(modifier);

            pipeline.UpdateAll(0.5f);

            Assert.AreEqual(1, modifier.UpdateCalls);
            Assert.AreEqual(0.5f, modifier.LastDeltaTime);
        }

        private sealed class ConstantCondition : IModifierCondition
        {
            private readonly bool _value;

            public ConstantCondition(bool value)
            {
                _value = value;
            }

            public bool Evaluate(ModifierContext context)
            {
                return _value;
            }
        }

        private sealed class CounterAtLeastCondition : IModifierCondition
        {
            private readonly int _counterId;
            private readonly int _required;

            public CounterAtLeastCondition(int counterId, int required)
            {
                _counterId = counterId;
                _required = required;
            }

            public bool Evaluate(ModifierContext context)
            {
                return context.Counters.GetCounter(_counterId) >= _required;
            }
        }

        private sealed class CountEffect : IModifierEffect
        {
            public int Calls { get; private set; }

            public void Execute(ModifierContext context)
            {
                Calls++;
            }
        }

        private sealed class AppendEffect : IModifierEffect
        {
            private readonly string _value;

            public AppendEffect(string value)
            {
                _value = value;
            }

            public void Execute(ModifierContext context)
            {
                context.EnsureExtra();
                context.Extra["trace"] = (string)context.Extra["trace"] + _value;
            }
        }

        private sealed class AttributeAndBuffEffect : IModifierEffect
        {
            private readonly int _attributeId;
            private readonly int _delta;
            private readonly int _buffId;

            public AttributeAndBuffEffect(int attributeId, int delta, int buffId)
            {
                _attributeId = attributeId;
                _delta = delta;
                _buffId = buffId;
            }

            public void Execute(ModifierContext context)
            {
                context.Target.AddAttribute(_attributeId, _delta, this);
                if (context.Buffs != null && context.Buffs.HasBuff(_buffId))
                    context.Target.SetAttribute(2, _buffId, this);
            }
        }

        private sealed class TrackingModifier : IModifier
        {
            public TrackingModifier(int id)
            {
                Id = id;
            }

            public int Id { get; }
            public int ParamIndex { get; private set; }
            public int RemoveCalls { get; private set; }
            public int UpdateCalls { get; private set; }
            public float LastDeltaTime { get; private set; }

            public void SetParamIndex(int index)
            {
                ParamIndex = index;
            }

            public void Apply(ModifierContext context)
            {
            }

            public void Update(float deltaTime, ModifierContext context)
            {
                UpdateCalls++;
                LastDeltaTime = deltaTime;
            }

            public void Remove(ModifierContext context)
            {
                RemoveCalls++;
            }
        }

        private sealed class TestModifierFactory : IModifierFactory
        {
            public bool TryCreate(int modifierId, out IModifier modifier)
            {
                modifier = new TrackingModifier(modifierId);
                return true;
            }
        }

        private sealed class TestBuff : BuffBase
        {
            public TestBuff(int id, float duration)
                : base(id, duration)
            {
            }
        }

        private sealed class TestBuffTarget : IBuffTarget
        {
            private readonly EventBus<BuffEvent> _events = new EventBus<BuffEvent>();

            public TestBuffTarget(AttributeStore attributes)
            {
                Attributes = attributes;
                AttributeModifiers = attributes;
            }

            public AttributeStore Attributes { get; }

            IAttributeOwner IBuffTarget.Attributes => Attributes;

            public IAttributeModifierOwner AttributeModifiers { get; }

            public IEventBus<BuffEvent> BuffEvents => _events;
        }
    }
}
