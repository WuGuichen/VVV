using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentQueryTests
    {
        [Test]
        public void CopyEntriesEntitiesAndComponentsUseStableStoreOrder()
        {
            var registry = new GameplayComponentRegistry();
            GameplayEntityId first = registry.CreateEntity();
            GameplayEntityId second = registry.CreateEntity();
            GameplayEntityId third = registry.CreateEntity();
            GameplayComponentStore<TestHealthComponent> health = registry.CreateStore<TestHealthComponent>();
            health.Set(third, new TestHealthComponent(30));
            health.Set(first, new TestHealthComponent(10));
            health.Set(second, new TestHealthComponent(20));

            var entries = new List<GameplayComponentSnapshot<TestHealthComponent>>();
            var entities = new List<GameplayEntityId>();
            var components = new List<TestHealthComponent>();

            Assert.AreEqual(3, GameplayComponentQuery.CopyEntries(health, entries));
            Assert.AreEqual(3, GameplayComponentQuery.CopyEntities(health, entities));
            Assert.AreEqual(3, GameplayComponentQuery.CopyComponents(health, components));

            Assert.AreEqual(first, entries[0].EntityId);
            Assert.AreEqual(second, entries[1].EntityId);
            Assert.AreEqual(third, entries[2].EntityId);
            CollectionAssert.AreEqual(new[] { first, second, third }, entities);
            Assert.AreEqual(10, components[0].Value);
            Assert.AreEqual(20, components[1].Value);
            Assert.AreEqual(30, components[2].Value);
        }

        [Test]
        public void CopyPairsReturnsIntersectionInPrimaryStoreOrder()
        {
            var registry = new GameplayComponentRegistry();
            GameplayEntityId first = registry.CreateEntity();
            GameplayEntityId second = registry.CreateEntity();
            GameplayEntityId third = registry.CreateEntity();
            GameplayComponentStore<TestHealthComponent> health = registry.CreateStore<TestHealthComponent>();
            GameplayComponentStore<TestTeamComponent> team = registry.CreateStore<TestTeamComponent>();
            health.Set(third, new TestHealthComponent(30));
            health.Set(first, new TestHealthComponent(10));
            health.Set(second, new TestHealthComponent(20));
            team.Set(third, new TestTeamComponent(3));
            team.Set(first, new TestTeamComponent(1));

            var pairs = new List<GameplayComponentPair<TestHealthComponent, TestTeamComponent>>();

            Assert.AreEqual(2, GameplayComponentQuery.CopyPairs(health, team, pairs));

            Assert.AreEqual(first, pairs[0].EntityId);
            Assert.AreEqual(10, pairs[0].Primary.Value);
            Assert.AreEqual(1, pairs[0].Secondary.Value);
            Assert.AreEqual(third, pairs[1].EntityId);
            Assert.AreEqual(30, pairs[1].Primary.Value);
            Assert.AreEqual(3, pairs[1].Secondary.Value);
        }

        [Test]
        public void CopyPairsDoesNotReturnDestroyedEntityAfterRegistryCleanup()
        {
            var registry = new GameplayComponentRegistry();
            GameplayEntityId first = registry.CreateEntity();
            GameplayEntityId second = registry.CreateEntity();
            GameplayComponentStore<TestHealthComponent> health = registry.CreateStore<TestHealthComponent>();
            GameplayComponentStore<TestTeamComponent> team = registry.CreateStore<TestTeamComponent>();
            health.Set(first, new TestHealthComponent(10));
            team.Set(first, new TestTeamComponent(1));
            health.Set(second, new TestHealthComponent(20));
            team.Set(second, new TestTeamComponent(2));

            Assert.IsTrue(registry.DestroyEntity(first));
            var pairs = new List<GameplayComponentPair<TestHealthComponent, TestTeamComponent>>();

            Assert.AreEqual(1, GameplayComponentQuery.CopyPairs(health, team, pairs));

            Assert.AreEqual(second, pairs[0].EntityId);
            Assert.AreEqual(20, pairs[0].Primary.Value);
        }

        [Test]
        public void QueryMethodsRejectNullInputs()
        {
            var store = new GameplayComponentStore<TestHealthComponent>();
            var entries = new List<GameplayComponentSnapshot<TestHealthComponent>>();
            var entities = new List<GameplayEntityId>();
            var components = new List<TestHealthComponent>();
            var pairs = new List<GameplayComponentPair<TestHealthComponent, TestTeamComponent>>();
            var team = new GameplayComponentStore<TestTeamComponent>();

            Assert.Throws<ArgumentNullException>(() => GameplayComponentQuery.CopyEntries<TestHealthComponent>(null, entries));
            Assert.Throws<ArgumentNullException>(() => GameplayComponentQuery.CopyEntries(store, null));
            Assert.Throws<ArgumentNullException>(() => GameplayComponentQuery.CopyEntities<TestHealthComponent>(null, entities));
            Assert.Throws<ArgumentNullException>(() => GameplayComponentQuery.CopyEntities(store, null));
            Assert.Throws<ArgumentNullException>(() => GameplayComponentQuery.CopyComponents<TestHealthComponent>(null, components));
            Assert.Throws<ArgumentNullException>(() => GameplayComponentQuery.CopyComponents(store, null));
            Assert.Throws<ArgumentNullException>(() => GameplayComponentQuery.CopyPairs<TestHealthComponent, TestTeamComponent>(null, team, pairs));
            Assert.Throws<ArgumentNullException>(() => GameplayComponentQuery.CopyPairs(store, null, pairs));
            Assert.Throws<ArgumentNullException>(() => GameplayComponentQuery.CopyPairs(store, team, null));
        }

        private readonly struct TestHealthComponent : IGameplayComponent
        {
            public TestHealthComponent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private readonly struct TestTeamComponent : IGameplayComponent
        {
            public TestTeamComponent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }
}
