using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentStoreTests
    {
        [Test]
        public void GameplayEntityId_DefaultIsInvalidAndOrdersByIndexThenGeneration()
        {
            Assert.IsFalse(default(GameplayEntityId).IsValid);

            var first = new GameplayEntityId(1, 2);
            var second = new GameplayEntityId(2, 1);
            var sameIndexOlder = new GameplayEntityId(1, 1);
            var ids = new[] { second, first, sameIndexOlder };

            Array.Sort(ids);

            Assert.AreEqual(sameIndexOlder, ids[0]);
            Assert.AreEqual(first, ids[1]);
            Assert.AreEqual(second, ids[2]);
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplayEntityId(-1, 1));
            Assert.Throws<ArgumentException>(() => new GameplayEntityId(1, 0));
        }

        [Test]
        public void GameplayEntityLifecycle_ReusesIndexWithNewGenerationAndRejectsStaleId()
        {
            var lifecycle = new GameplayEntityLifecycle();
            GameplayEntityId first = lifecycle.Create();

            Assert.AreEqual(1, first.Index);
            Assert.AreEqual(1, first.Generation);
            Assert.IsTrue(lifecycle.IsAlive(first));
            Assert.AreEqual(1, lifecycle.CountAlive);

            Assert.IsTrue(lifecycle.Destroy(first));
            Assert.IsFalse(lifecycle.IsAlive(first));
            Assert.IsFalse(lifecycle.Destroy(first));

            GameplayEntityId second = lifecycle.Create();

            Assert.AreEqual(first.Index, second.Index);
            Assert.AreNotEqual(first.Generation, second.Generation);
            Assert.IsFalse(lifecycle.IsAlive(first));
            Assert.IsTrue(lifecycle.IsAlive(second));
        }

        [Test]
        public void GameplayEntityLifecycle_SnapshotIsStableAndClearInvalidatesAliveIds()
        {
            var lifecycle = new GameplayEntityLifecycle();
            GameplayEntityId first = lifecycle.Create();
            GameplayEntityId second = lifecycle.Create();
            GameplayEntityId third = lifecycle.Create();

            Assert.IsTrue(lifecycle.Destroy(second));

            GameplayEntityId[] snapshot = lifecycle.CreateSnapshot();

            Assert.AreEqual(2, snapshot.Length);
            Assert.AreEqual(first, snapshot[0]);
            Assert.AreEqual(third, snapshot[1]);

            lifecycle.Clear();

            Assert.AreEqual(0, lifecycle.CountAlive);
            Assert.IsFalse(lifecycle.IsAlive(first));
            Assert.IsFalse(lifecycle.IsAlive(third));

            GameplayEntityId reused = lifecycle.Create();
            Assert.AreEqual(2, reused.Generation);
            Assert.IsFalse(lifecycle.IsAlive(first));
        }

        [Test]
        public void GameplayComponentStore_AddGetSetRemoveAndClear()
        {
            var lifecycle = new GameplayEntityLifecycle();
            GameplayEntityId entityId = lifecycle.Create();
            var store = new GameplayComponentStore<TestStatComponent>();

            Assert.IsTrue(store.TryAdd(entityId, new TestStatComponent(10)));
            Assert.IsFalse(store.TryAdd(entityId, new TestStatComponent(20)));
            Assert.IsTrue(store.TryGet(entityId, out TestStatComponent component));
            Assert.AreEqual(10, component.Value);

            store.Set(entityId, new TestStatComponent(30));

            Assert.IsTrue(store.Contains(entityId));
            Assert.IsTrue(store.TryGet(entityId, out component));
            Assert.AreEqual(30, component.Value);

            Assert.IsTrue(store.Remove(entityId));
            Assert.IsFalse(store.Remove(entityId));
            Assert.IsFalse(store.TryGet(entityId, out _));

            store.Set(entityId, new TestStatComponent(40));
            Assert.AreEqual(1, store.Count);
            store.Clear();
            Assert.AreEqual(0, store.Count);
        }

        [Test]
        public void GameplayComponentStore_RejectsInvalidEntityId()
        {
            var store = new GameplayComponentStore<TestStatComponent>();

            Assert.Throws<ArgumentException>(() => store.TryAdd(default, new TestStatComponent(1)));
            Assert.Throws<ArgumentException>(() => store.Set(default, new TestStatComponent(1)));
            Assert.IsFalse(store.Contains(default));
            Assert.IsFalse(store.TryGet(default, out _));
            Assert.IsFalse(store.Remove(default));
        }

        [Test]
        public void GameplayComponentStore_SnapshotAndCopyUseStableEntityOrdering()
        {
            var lifecycle = new GameplayEntityLifecycle();
            GameplayEntityId first = lifecycle.Create();
            GameplayEntityId second = lifecycle.Create();
            GameplayEntityId third = lifecycle.Create();
            var store = new GameplayComponentStore<TestStatComponent>();

            Assert.IsTrue(store.TryAdd(third, new TestStatComponent(30)));
            Assert.IsTrue(store.TryAdd(first, new TestStatComponent(10)));
            Assert.IsTrue(store.TryAdd(second, new TestStatComponent(20)));

            GameplayComponentSnapshot<TestStatComponent>[] snapshot = store.CreateSnapshot();

            Assert.AreEqual(first, snapshot[0].EntityId);
            Assert.AreEqual(10, snapshot[0].Component.Value);
            Assert.AreEqual(second, snapshot[1].EntityId);
            Assert.AreEqual(20, snapshot[1].Component.Value);
            Assert.AreEqual(third, snapshot[2].EntityId);
            Assert.AreEqual(30, snapshot[2].Component.Value);

            var copied = new List<GameplayComponentSnapshot<TestStatComponent>>();
            Assert.AreEqual(3, store.CopyTo(copied));
            Assert.AreEqual(first, copied[0].EntityId);
            Assert.AreEqual(second, copied[1].EntityId);
            Assert.AreEqual(third, copied[2].EntityId);
        }

        private readonly struct TestStatComponent : IGameplayComponent
        {
            public TestStatComponent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }
}
