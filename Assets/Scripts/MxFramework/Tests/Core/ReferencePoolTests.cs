using System;
using MxFramework.Core.Pooling;
using MxFramework.Modifiers;
using NUnit.Framework;

namespace MxFramework.Tests.Core.Pooling
{
    public class ReferencePoolTests
    {
        [Test]
        public void Get_CreatesReferenceAndUpdatesCounts()
        {
            var pool = new ReferencePool<TestReference>();

            TestReference item = pool.Get();

            Assert.IsNotNull(item);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
        }

        [Test]
        public void Release_CallsClearAndMovesReferenceToInactive()
        {
            var pool = new ReferencePool<TestReference>();
            TestReference item = pool.Get();
            item.Value = 42;

            pool.Release(item);

            Assert.AreEqual(1, item.ClearCalls);
            Assert.AreEqual(0, item.Value);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
        }

        [Test]
        public void Prewarm_AddsInactiveWithoutActiveReferences()
        {
            var pool = new ReferencePool<TestReference>();

            pool.Prewarm(3);

            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(3, pool.CountInactive);
            Assert.AreEqual(3, pool.CountAll);
        }

        [Test]
        public void Prewarm_WhenCountBelowInactive_DoesNothing()
        {
            var pool = new ReferencePool<TestReference>();
            pool.Prewarm(3);

            pool.Prewarm(1);

            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(3, pool.CountInactive);
            Assert.AreEqual(3, pool.CountAll);
        }

        [Test]
        public void Prewarm_WhenCountNegative_Throws()
        {
            var pool = new ReferencePool<TestReference>();

            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Prewarm(-1));
        }

        [Test]
        public void Clear_RemovesInactiveOnly()
        {
            var pool = new ReferencePool<TestReference>();
            TestReference active = pool.Get();
            TestReference inactive = pool.Get();
            pool.Release(inactive);

            pool.Clear();

            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
            pool.Release(active);
        }

        [Test]
        public void Release_WhenNull_Throws()
        {
            var pool = new ReferencePool<TestReference>();

            Assert.Throws<ArgumentNullException>(() => pool.Release(null));
        }

        [Test]
        public void Release_WhenDuplicate_ThrowsAndDoesNotCorruptCounts()
        {
            var pool = new ReferencePool<TestReference>();
            TestReference item = pool.Get();
            pool.Release(item);

            Assert.Throws<InvalidOperationException>(() => pool.Release(item));
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
        }

        [Test]
        public void ModifierContextPush_ClearsFieldsAndKeepsExtraDictionary()
        {
            ModifierContext context = ModifierContext.Get();
            context.CompareId = 10;
            context.CompareValue1 = 20;
            context.CompareValue2 = 30;
            context.Source = new object();
            context.Parameters = new[] { 1, 2, 3 };
            context.EnsureExtra();
            context.Extra["key"] = "value";
            var extra = context.Extra;

            ModifierContext.Push(context);

            ModifierContext reused = ModifierContext.Get();
            Assert.AreSame(context, reused);
            Assert.IsNull(reused.Target);
            Assert.IsNull(reused.Buffs);
            Assert.IsNull(reused.Counters);
            Assert.IsNull(reused.Parameters);
            Assert.IsNull(reused.Source);
            Assert.AreEqual(0, reused.CompareId);
            Assert.AreEqual(0, reused.CompareValue1);
            Assert.AreEqual(0, reused.CompareValue2);
            Assert.AreSame(extra, reused.Extra);
            Assert.AreEqual(0, reused.Extra.Count);
            ModifierContext.Push(reused);
        }

        private sealed class TestReference : IReference
        {
            public TestReference()
            {
            }

            public int ClearCalls { get; private set; }
            public int Value { get; set; }

            public void Clear()
            {
                ClearCalls++;
                Value = 0;
            }
        }
    }
}
