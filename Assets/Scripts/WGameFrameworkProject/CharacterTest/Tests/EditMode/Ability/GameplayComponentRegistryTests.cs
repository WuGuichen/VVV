using System;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentRegistryTests
    {
        [Test]
        public void DestroyEntity_RemovesComponentsFromRegisteredStores()
        {
            var registry = new GameplayComponentRegistry();
            GameplayComponentStore<TestStatComponent> stats = registry.CreateStore<TestStatComponent>();
            GameplayComponentStore<TestFlagComponent> flags = registry.CreateStore<TestFlagComponent>();
            GameplayEntityId entity = registry.CreateEntity();

            stats.Set(entity, new TestStatComponent(10));
            flags.Set(entity, new TestFlagComponent(true));

            Assert.IsTrue(registry.DestroyEntity(entity));

            Assert.IsFalse(registry.IsAlive(entity));
            Assert.IsFalse(stats.Contains(entity));
            Assert.IsFalse(flags.Contains(entity));
            Assert.AreEqual(0, stats.Count);
            Assert.AreEqual(0, flags.Count);
        }

        [Test]
        public void DestroyedEntity_DoesNotAppearInRegistrySnapshot()
        {
            var registry = new GameplayComponentRegistry();
            GameplayEntityId first = registry.CreateEntity();
            GameplayEntityId second = registry.CreateEntity();
            GameplayEntityId third = registry.CreateEntity();

            Assert.IsTrue(registry.DestroyEntity(second));

            GameplayEntityId[] snapshot = registry.CreateEntitySnapshot();

            Assert.AreEqual(2, snapshot.Length);
            Assert.AreEqual(first, snapshot[0]);
            Assert.AreEqual(third, snapshot[1]);
        }

        [Test]
        public void DestroyEntity_StaleOrInvalidIdDoesNotClearStores()
        {
            var registry = new GameplayComponentRegistry();
            GameplayComponentStore<TestStatComponent> stats = registry.CreateStore<TestStatComponent>();
            GameplayEntityId entity = registry.CreateEntity();
            stats.Set(entity, new TestStatComponent(10));

            Assert.IsTrue(registry.DestroyEntity(entity));
            GameplayEntityId reused = registry.CreateEntity();
            stats.Set(reused, new TestStatComponent(20));

            Assert.IsFalse(registry.DestroyEntity(entity));
            Assert.IsFalse(registry.DestroyEntity(default));
            Assert.IsTrue(stats.Contains(reused));
            Assert.AreEqual(1, stats.Count);
        }

        [Test]
        public void RegisterStore_RejectsDuplicateComponentTypeAndCanLookupStore()
        {
            var registry = new GameplayComponentRegistry();
            GameplayComponentStore<TestStatComponent> stats = registry.CreateStore<TestStatComponent>();

            Assert.AreEqual(1, registry.StoreCount);
            Assert.IsTrue(registry.TryGetStore(out GameplayComponentStore<TestStatComponent> found));
            Assert.AreSame(stats, found);
            Assert.IsFalse(registry.TryGetStore(out GameplayComponentStore<TestFlagComponent> missing));
            Assert.IsNull(missing);

            Assert.Throws<InvalidOperationException>(() => registry.RegisterStore(new GameplayComponentStore<TestStatComponent>()));
            Assert.Throws<ArgumentNullException>(() => registry.RegisterStore<TestFlagComponent>(null));
        }

        [Test]
        public void Clear_InvalidatesEntitiesAndClearsRegisteredStores()
        {
            var registry = new GameplayComponentRegistry();
            GameplayComponentStore<TestStatComponent> stats = registry.CreateStore<TestStatComponent>();
            GameplayEntityId entity = registry.CreateEntity();
            stats.Set(entity, new TestStatComponent(10));

            registry.Clear();

            Assert.AreEqual(0, registry.CountAlive);
            Assert.AreEqual(0, stats.Count);
            Assert.IsFalse(registry.IsAlive(entity));
        }

        private readonly struct TestStatComponent : IGameplayComponent
        {
            public TestStatComponent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private readonly struct TestFlagComponent : IGameplayComponent
        {
            public TestFlagComponent(bool value)
            {
                Value = value;
            }

            public bool Value { get; }
        }
    }
}
