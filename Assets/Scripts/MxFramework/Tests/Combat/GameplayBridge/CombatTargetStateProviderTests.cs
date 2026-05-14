using System;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.GameplayBridge
{
    public sealed class CombatTargetStateProviderTests
    {
        private static readonly GameplayStatusId Invincible = new GameplayStatusId(100);
        private static readonly GameplayStatusId Blocking = new GameplayStatusId(200);
        private static readonly GameplayStatusId Parrying = new GameplayStatusId(300);
        private static readonly GameplayStatusId SuperArmor = new GameplayStatusId(400);

        [Test]
        public void Evaluate_NullWorldThrows()
        {
            var provider = new CombatTargetStateProvider();

            Assert.Throws<ArgumentNullException>(() => provider.Evaluate(null, default));
        }

        [Test]
        public void Evaluate_InvalidEntityReturnsNone()
        {
            var provider = new CombatTargetStateProvider();

            Assert.AreEqual(HitTargetStateFlags.None, provider.Evaluate(new GameplayComponentWorld(), default));
        }

        [Test]
        public void Evaluate_MissingLifecycleStoreReturnsNone()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            var provider = new CombatTargetStateProvider();

            Assert.AreEqual(HitTargetStateFlags.None, provider.Evaluate(world, entity));
        }

        [Test]
        public void Evaluate_MissingLifecycleComponentReturnsNone()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayLifecycleComponent>();
            var provider = new CombatTargetStateProvider();

            Assert.AreEqual(HitTargetStateFlags.None, provider.Evaluate(world, entity));
        }

        [Test]
        public void Evaluate_AliveEntityWithoutStatusStoreReturnsAlive()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = CreateEntity(world, GameplayLifecycleComponent.Alive);
            var provider = new CombatTargetStateProvider();

            Assert.AreEqual(HitTargetStateFlags.Alive, provider.Evaluate(world, entity));
        }

        [Test]
        public void Evaluate_DeadEntityDoesNotSetAlive()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = CreateEntity(world, new GameplayLifecycleComponent(GameplayLifecycleState.None));
            var provider = new CombatTargetStateProvider();

            Assert.AreEqual(HitTargetStateFlags.None, provider.Evaluate(world, entity));
        }

        [Test]
        public void Evaluate_PendingDestroyEntityDoesNotSetAlive()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = CreateEntity(world, GameplayLifecycleComponent.PendingDestroy);
            var provider = new CombatTargetStateProvider();

            Assert.AreEqual(HitTargetStateFlags.None, provider.Evaluate(world, entity));
        }

        [Test]
        public void Evaluate_DestroyedEntityDoesNotSetAlive()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = CreateEntity(world, GameplayLifecycleComponent.Destroyed);
            var provider = new CombatTargetStateProvider();

            Assert.AreEqual(HitTargetStateFlags.None, provider.Evaluate(world, entity));
        }

        [Test]
        public void Evaluate_InvincibleStatusMapsToInvincibleWithAlive()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = CreateEntity(world, GameplayLifecycleComponent.Alive, Invincible);
            var provider = CreateProvider();

            Assert.AreEqual(
                HitTargetStateFlags.Alive | HitTargetStateFlags.Invincible,
                provider.Evaluate(world, entity));
        }

        [Test]
        public void Evaluate_BlockingStatusMapsToBlockingWithAlive()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = CreateEntity(world, GameplayLifecycleComponent.Alive, Blocking);
            var provider = CreateProvider();

            Assert.AreEqual(
                HitTargetStateFlags.Alive | HitTargetStateFlags.Blocking,
                provider.Evaluate(world, entity));
        }

        [Test]
        public void Evaluate_ParryingStatusMapsToParryingWithAlive()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = CreateEntity(world, GameplayLifecycleComponent.Alive, Parrying);
            var provider = CreateProvider();

            Assert.AreEqual(
                HitTargetStateFlags.Alive | HitTargetStateFlags.Parrying,
                provider.Evaluate(world, entity));
        }

        [Test]
        public void Evaluate_SuperArmorStatusMapsToSuperArmorWithAlive()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = CreateEntity(world, GameplayLifecycleComponent.Alive, SuperArmor);
            var provider = CreateProvider();

            Assert.AreEqual(
                HitTargetStateFlags.Alive | HitTargetStateFlags.SuperArmor,
                provider.Evaluate(world, entity));
        }

        [Test]
        public void Evaluate_CombinesConfiguredStatuses()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = CreateEntity(world, GameplayLifecycleComponent.Alive, Invincible, Blocking, SuperArmor);
            var provider = CreateProvider();

            Assert.AreEqual(
                HitTargetStateFlags.Alive |
                HitTargetStateFlags.Invincible |
                HitTargetStateFlags.Blocking |
                HitTargetStateFlags.SuperArmor,
                provider.Evaluate(world, entity));
        }

        [Test]
        public void Evaluate_DefaultStatusMappingsAreDisabled()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = CreateEntity(world, GameplayLifecycleComponent.Alive, default, GameplayStatusId.None);
            var provider = new CombatTargetStateProvider(
                invincibleStatusId: default,
                blockingStatusId: Blocking,
                parryingStatusId: default,
                superArmorStatusId: default);

            Assert.AreEqual(HitTargetStateFlags.Alive, provider.Evaluate(world, entity));
        }

        [Test]
        public void Evaluate_DeadEntityCanStillExposeConfiguredStatusFlagsWithoutAlive()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = CreateEntity(world, GameplayLifecycleComponent.PendingDestroy, Invincible, Parrying);
            var provider = CreateProvider();

            Assert.AreEqual(
                HitTargetStateFlags.Invincible | HitTargetStateFlags.Parrying,
                provider.Evaluate(world, entity));
        }

        private static CombatTargetStateProvider CreateProvider()
        {
            return new CombatTargetStateProvider(Invincible, Blocking, Parrying, SuperArmor);
        }

        private static GameplayEntityId CreateEntity(
            GameplayComponentWorld world,
            GameplayLifecycleComponent lifecycle,
            params GameplayStatusId[] statuses)
        {
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayLifecycleComponent>().Set(entity, lifecycle);

            if (statuses != null && statuses.Length > 0)
                world.GetOrCreateStore<GameplayStatusComponent>().Set(entity, new GameplayStatusComponent(statuses));

            return entity;
        }
    }
}
