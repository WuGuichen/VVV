using MxFramework.Attributes;
using MxFramework.Gameplay;
using MxFramework.Modifiers;
using MxFramework.Runtime;
using MxFramework.Story.GameplayBridge;
using NUnit.Framework;

namespace MxFramework.Tests.StoryGameplayBridge
{
    public sealed class StoryModifierConditionAdapterTests
    {
        private const int HpAttributeId = 100;

        [Test]
        public void MissingTargetReturnsFalse()
        {
            StoryGameplayEntityRef targetRef = StoryGameplayEntityRef.LegacyRuntimeEntity(1);
            var resolver = new StoryModifierContextResolver();
            resolver.Register(targetRef, new StoryModifierContextData(target: null));
            var adapter = new StoryModifierConditionAdapter(new AlwaysTrueCondition(), resolver);

            bool result = adapter.Evaluate(new StoryEvaluationContext(1001, 2001, 1, targetRef));

            Assert.IsFalse(result);
            Assert.AreEqual(StoryGameplayBridgeDiagnosticCode.MissingModifierContextTarget, adapter.LastDiagnostic.Code);
        }

        [Test]
        public void BuildsTemporaryModifierContext()
        {
            var entity = new RuntimeEntity(1, 1, HpAttributeId);
            entity.AttributeStore.SetAttribute(HpAttributeId, 75);
            StoryGameplayEntityRef targetRef = StoryGameplayEntityRef.LegacyRuntimeEntity(entity.EntityId);
            var resolver = new StoryModifierContextResolver();
            resolver.Register(targetRef, new StoryModifierContextData(
                entity.AttributeStore,
                entity.BuffPipeline,
                entity.ModifierPipeline.Counters,
                parameters: new[] { HpAttributeId },
                compareId: 9,
                compareValue1: 70,
                compareValue2: 80,
                source: entity));
            var condition = new CapturingCondition();
            var adapter = new StoryModifierConditionAdapter(condition, resolver);

            bool result = adapter.Evaluate(new StoryEvaluationContext(
                1001,
                2001,
                1,
                targetRef,
                currentFrame: new RuntimeFrame(7)));

            Assert.IsTrue(result);
            Assert.IsTrue(condition.WasEvaluated);
            Assert.AreSame(entity.AttributeStore, condition.Target);
            Assert.AreSame(entity.BuffPipeline, condition.Buffs);
            Assert.AreSame(entity.ModifierPipeline.Counters, condition.Counters);
            Assert.AreEqual(HpAttributeId, condition.Parameter0);
            Assert.AreEqual(9, condition.CompareId);
            Assert.AreEqual(70, condition.CompareValue1);
            Assert.AreEqual(80, condition.CompareValue2);
            Assert.AreSame(entity, condition.Source);
            Assert.AreEqual(StoryGameplayBridgeDiagnosticCode.None, adapter.LastDiagnostic.Code);
        }

        private sealed class AlwaysTrueCondition : IModifierCondition
        {
            public bool Evaluate(ModifierContext context)
            {
                return true;
            }
        }

        private sealed class CapturingCondition : IModifierCondition
        {
            public bool WasEvaluated { get; private set; }
            public IAttributeOwner Target { get; private set; }
            public object Buffs { get; private set; }
            public object Counters { get; private set; }
            public int Parameter0 { get; private set; }
            public int CompareId { get; private set; }
            public int CompareValue1 { get; private set; }
            public int CompareValue2 { get; private set; }
            public object Source { get; private set; }

            public bool Evaluate(ModifierContext context)
            {
                WasEvaluated = true;
                Target = context.Target;
                Buffs = context.Buffs;
                Counters = context.Counters;
                Parameter0 = context.Parameters[0];
                CompareId = context.CompareId;
                CompareValue1 = context.CompareValue1;
                CompareValue2 = context.CompareValue2;
                Source = context.Source;
                return context.Target.TryGetAttribute(Parameter0, out int value) && value >= CompareValue1 && value <= CompareValue2;
            }
        }
    }
}
