using System;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayCoreComponentTests
    {
        [Test]
        public void IdentityComponent_StoresDefinitionAndVariant()
        {
            var identity = new GameplayIdentityComponent(1001, 2);

            Assert.AreEqual(1001, identity.DefinitionId);
            Assert.AreEqual(2, identity.VariantId);
            Assert.IsFalse(identity.IsNone);
            Assert.IsTrue(default(GameplayIdentityComponent).IsNone);
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplayIdentityComponent(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplayIdentityComponent(1, -1));
        }

        [Test]
        public void TeamComponent_UsesGameplayTeamRelations()
        {
            var first = new GameplayTeamComponent(1);
            var second = new GameplayTeamComponent(2);
            var neutral = new GameplayTeamComponent(0);

            Assert.AreEqual(GameplayTeamRelation.SameTeam, first.RelationTo(first));
            Assert.AreEqual(GameplayTeamRelation.Enemy, first.RelationTo(second));
            Assert.AreEqual(GameplayTeamRelation.Neutral, first.RelationTo(neutral));
            Assert.IsTrue(neutral.IsNeutral);
        }

        [Test]
        public void LifecycleComponent_ExposesCommonStates()
        {
            Assert.IsTrue(GameplayLifecycleComponent.Alive.IsAlive);
            Assert.IsFalse(GameplayLifecycleComponent.PendingDestroy.IsAlive);
            Assert.IsTrue(GameplayLifecycleComponent.Destroyed.IsTerminal);
            Assert.AreEqual(GameplayLifecycleState.None, default(GameplayLifecycleComponent).State);
        }

        [Test]
        public void TagComponent_CopiesSortsDeduplicatesAndFiltersInvalidIds()
        {
            var ids = new[] { new GameplayTagId(30), GameplayTagId.None, new GameplayTagId(10), new GameplayTagId(30) };
            var component = new GameplayTagComponent(ids);
            ids[0] = new GameplayTagId(99);

            GameplayTagId[] snapshot = component.ToArray();

            Assert.AreEqual(2, component.Count);
            Assert.AreEqual(10, snapshot[0].Value);
            Assert.AreEqual(30, snapshot[1].Value);
            Assert.IsTrue(component.Contains(new GameplayTagId(10)));
            Assert.IsFalse(component.Contains(GameplayTagId.None));

            snapshot[0] = new GameplayTagId(99);
            Assert.AreEqual(10, component.ToArray()[0].Value);
        }

        [Test]
        public void StatusComponent_CopiesSortsDeduplicatesAndFiltersInvalidIds()
        {
            var ids = new[] { new GameplayStatusId(200), GameplayStatusId.None, new GameplayStatusId(100), new GameplayStatusId(200) };
            var component = new GameplayStatusComponent(ids);
            ids[0] = new GameplayStatusId(999);

            GameplayStatusId[] snapshot = component.ToArray();

            Assert.AreEqual(2, component.Count);
            Assert.AreEqual(100, snapshot[0].Value);
            Assert.AreEqual(200, snapshot[1].Value);
            Assert.IsTrue(component.Contains(new GameplayStatusId(200)));
            Assert.IsFalse(component.Contains(GameplayStatusId.None));

            snapshot[0] = new GameplayStatusId(999);
            Assert.AreEqual(100, component.ToArray()[0].Value);
        }

        [Test]
        public void ComponentRegistry_GetOrCreateStoreReturnsExistingStore()
        {
            var registry = new GameplayComponentRegistry();

            GameplayComponentStore<GameplayTeamComponent> first = registry.GetOrCreateStore<GameplayTeamComponent>();
            GameplayComponentStore<GameplayTeamComponent> second = registry.GetOrCreateStore<GameplayTeamComponent>();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, registry.StoreCount);
        }
    }
}
