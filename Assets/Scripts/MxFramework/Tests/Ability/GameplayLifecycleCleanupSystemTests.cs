using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayLifecycleCleanupSystemTests
    {
        [Test]
        public void LifecycleCleanupSystem_DestroysPendingDestroyEntities()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId alive = world.CreateEntity();
            GameplayEntityId pending = world.CreateEntity();
            GameplayComponentStore<GameplayLifecycleComponent> lifecycle = world.GetOrCreateStore<GameplayLifecycleComponent>();
            lifecycle.Set(alive, GameplayLifecycleComponent.Alive);
            lifecycle.Set(pending, GameplayLifecycleComponent.PendingDestroy);

            new GameplayLifecycleCleanupSystem().Tick(CreateContext(world));

            Assert.IsTrue(world.IsAlive(alive));
            Assert.IsFalse(world.IsAlive(pending));
            Assert.IsTrue(lifecycle.Contains(alive));
            Assert.IsFalse(lifecycle.Contains(pending));
        }

        [Test]
        public void LifecycleCleanupSystem_RemovesComponentsFromAllRegisteredStores()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayLifecycleComponent> lifecycle = world.GetOrCreateStore<GameplayLifecycleComponent>();
            GameplayComponentStore<GameplayTeamComponent> teams = world.GetOrCreateStore<GameplayTeamComponent>();
            GameplayComponentStore<TestStatComponent> stats = world.GetOrCreateStore<TestStatComponent>();
            lifecycle.Set(entity, GameplayLifecycleComponent.PendingDestroy);
            teams.Set(entity, new GameplayTeamComponent(2));
            stats.Set(entity, new TestStatComponent(10));

            new GameplayLifecycleCleanupSystem().Tick(CreateContext(world));

            Assert.IsFalse(world.IsAlive(entity));
            Assert.IsFalse(lifecycle.Contains(entity));
            Assert.IsFalse(teams.Contains(entity));
            Assert.IsFalse(stats.Contains(entity));
        }

        [Test]
        public void LifecycleCleanupSystem_EmitsDestroyedEventsInStableOrder()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId first = world.CreateEntity();
            GameplayEntityId second = world.CreateEntity();
            GameplayEntityId third = world.CreateEntity();
            GameplayComponentStore<GameplayLifecycleComponent> lifecycle = world.GetOrCreateStore<GameplayLifecycleComponent>();
            lifecycle.Set(third, GameplayLifecycleComponent.PendingDestroy);
            lifecycle.Set(first, GameplayLifecycleComponent.PendingDestroy);
            lifecycle.Set(second, GameplayLifecycleComponent.PendingDestroy);
            var context = CreateContext(world);

            new GameplayLifecycleCleanupSystem().Tick(context);

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(3, world.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(first, events[0].ComponentEntityId);
            Assert.AreEqual(second, events[1].ComponentEntityId);
            Assert.AreEqual(third, events[2].ComponentEntityId);
            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreEqual(GameplayRuntimeEventType.ComponentEntityDestroyed, events[i].Type);
                Assert.AreEqual(GameplayLifecycleEvents.PendingDestroyCleanupReason, events[i].Reason);
                Assert.AreEqual(0, events[i].CommandId);
            }
        }

        [Test]
        public void LifecycleCleanupSystem_IgnoresAliveAndDestroyedStates()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId alive = world.CreateEntity();
            GameplayEntityId destroyedMarker = world.CreateEntity();
            GameplayComponentStore<GameplayLifecycleComponent> lifecycle = world.GetOrCreateStore<GameplayLifecycleComponent>();
            lifecycle.Set(alive, GameplayLifecycleComponent.Alive);
            lifecycle.Set(destroyedMarker, GameplayLifecycleComponent.Destroyed);

            new GameplayLifecycleCleanupSystem().Tick(CreateContext(world));

            Assert.IsTrue(world.IsAlive(alive));
            Assert.IsTrue(world.IsAlive(destroyedMarker));
            Assert.AreEqual(0, world.PendingEventCount);
        }

        [Test]
        public void LifecycleCleanupSystem_HandlesMissingComponentWorldDiagnostically()
        {
            var events = new RuntimeEventQueue<GameplayRuntimeEvent>();
            var context = new GameplaySystemContext(
                RuntimeFrame.Zero,
                0d,
                0d,
                new GameplayWorld(),
                new RuntimeCommand[0],
                events);

            new GameplayLifecycleCleanupSystem().Tick(context);

            var drained = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, events.Drain(RuntimeFrame.Zero, drained));
            Assert.AreEqual(GameplayRuntimeEventType.CommandRejected, drained[0].Type);
            Assert.AreEqual(GameplayLifecycleEvents.MissingComponentWorldReason, drained[0].Reason);
        }

        [Test]
        public void LifecycleCleanupSystem_DoesNotThrowWhenLifecycleStoreMissing()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayTeamComponent>().Set(entity, new GameplayTeamComponent(1));

            Assert.DoesNotThrow(() => new GameplayLifecycleCleanupSystem().Tick(CreateContext(world)));
            Assert.IsTrue(world.IsAlive(entity));
            Assert.AreEqual(0, world.PendingEventCount);
        }

        [Test]
        public void LifecycleCleanupSystem_CanRunThroughRuntimeModulePipeline()
        {
            GameplayComponentWorld componentWorld = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = componentWorld.CreateEntity();
            componentWorld.GetOrCreateStore<GameplayLifecycleComponent>().Set(entity, GameplayLifecycleComponent.PendingDestroy);
            var module = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                new RuntimeCommandBuffer(),
                tickWorldAutomatically: false,
                configureDefaultPipeline: pipeline => pipeline.Add(new GameplayLifecycleCleanupSystem()),
                componentWorld: componentWorld);

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.IsFalse(componentWorld.IsAlive(entity));
            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.ComponentEntityDestroyed, events[0].Type);
            Assert.AreEqual(GameplayLifecycleEvents.PendingDestroyCleanupReason, events[0].Reason);
        }

        [Test]
        public void LifecycleCleanupSystem_RestoredPendingDestroyEntityCanBeCleanedUp()
        {
            GameplayComponentWorld source = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = source.CreateEntity();
            source.GetOrCreateStore<GameplayLifecycleComponent>().Set(entity, GameplayLifecycleComponent.PendingDestroy);
            source.GetOrCreateStore<GameplayTeamComponent>().Set(entity, new GameplayTeamComponent(1));
            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);

            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(saveState);
            new GameplayLifecycleCleanupSystem().Tick(CreateContext(target));

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.IsFalse(target.IsAlive(entity));
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayTeamComponent> teams));
            Assert.IsFalse(teams.Contains(entity));
        }

        [Test]
        public void LifecycleCleanupSystem_ChangesComponentWorldHashAfterCleanup()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayLifecycleComponent>().Set(entity, GameplayLifecycleComponent.PendingDestroy);
            world.GetOrCreateStore<GameplayTeamComponent>().Set(entity, new GameplayTeamComponent(1));
            long before = ComputeHash(world);

            new GameplayLifecycleCleanupSystem().Tick(CreateContext(world));
            long after = ComputeHash(world);

            Assert.AreNotEqual(before, after);
        }

        private static GameplayComponentWorld CreateWorld(bool registerSchemas)
        {
            var world = new GameplayComponentWorld();
            if (registerSchemas)
            {
                GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
                GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
                GameplayCoreComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            }

            return world;
        }

        private static GameplaySystemContext CreateContext(GameplayComponentWorld world)
        {
            return new GameplaySystemContext(
                RuntimeFrame.Zero,
                0d,
                0d,
                new GameplayWorld(),
                new RuntimeCommand[0],
                world.Events,
                componentWorld: world);
        }

        private static long ComputeHash(GameplayComponentWorld world)
        {
            return RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new GameplayComponentWorldHashContributor(world) });
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
