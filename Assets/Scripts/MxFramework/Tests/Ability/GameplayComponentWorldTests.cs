using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentWorldTests
    {
        [Test]
        public void DestroyEntity_RemovesRegisteredComponentsAndSnapshotReportsCounts()
        {
            var world = new GameplayComponentWorld();
            GameplayComponentStore<TestStatComponent> stats = world.CreateStore<TestStatComponent>();
            GameplayEntityId entity = world.CreateEntity();
            stats.Set(entity, new TestStatComponent(10));

            GameplayComponentWorldSnapshot before = world.CreateSnapshot();

            Assert.AreEqual(1, before.AliveEntityCount);
            Assert.AreEqual(1, before.ComponentStoreCount);
            Assert.IsTrue(world.DestroyEntity(entity));

            GameplayComponentWorldSnapshot after = world.CreateSnapshot();

            Assert.AreEqual(0, after.AliveEntityCount);
            Assert.AreEqual(1, after.ComponentStoreCount);
            Assert.IsFalse(stats.Contains(entity));
        }

        [Test]
        public void Events_DrainThroughWorldAndClearWithComponentState()
        {
            var world = new GameplayComponentWorld();
            GameplayComponentStore<TestStatComponent> stats = world.CreateStore<TestStatComponent>();
            GameplayEntityId entity = world.CreateEntity();
            stats.Set(entity, new TestStatComponent(10));
            world.EnqueueEvent(new GameplayRuntimeEvent(
                RuntimeFrame.Zero,
                GameplayRuntimeEventType.WorldTicked,
                commandId: 0,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: 0,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: string.Empty,
                traceId: "component-world"));

            Assert.AreEqual(1, world.PendingEventCount);
            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, world.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual("component-world", events[0].TraceId);

            world.EnqueueEvent(events[0]);
            world.Clear();

            Assert.AreEqual(0, world.CountAlive);
            Assert.AreEqual(0, stats.Count);
            Assert.AreEqual(0, world.PendingEventCount);
        }

        [Test]
        public void RuntimeModule_PassesComponentWorldToSystemsAndSharesEventQueue()
        {
            var componentWorld = new GameplayComponentWorld();
            var pipeline = new GameplaySystemPipeline();
            var observed = new List<GameplayComponentWorld>();
            pipeline.Add(new ComponentWorldRecordingSystem(observed));
            var module = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                new RuntimeCommandBuffer(),
                tickWorldAutomatically: false,
                systemPipeline: pipeline,
                componentWorld: componentWorld);

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(1, observed.Count);
            Assert.AreSame(componentWorld, observed[0]);
            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual("component-system", events[0].TraceId);
            Assert.AreEqual(0, componentWorld.PendingEventCount);
        }

        [Test]
        public void DiagnosticSnapshot_CapturesEntitiesStoresAndPendingEventsInStableOrder()
        {
            var world = new GameplayComponentWorld();
            GameplayComponentStore<TestFlagComponent> flags = world.CreateStore<TestFlagComponent>();
            GameplayComponentStore<TestStatComponent> stats = world.CreateStore<TestStatComponent>();
            GameplayEntityId first = world.CreateEntity();
            GameplayEntityId second = world.CreateEntity();
            flags.Set(second, new TestFlagComponent(true));
            stats.Set(first, new TestStatComponent(10));
            stats.Set(second, new TestStatComponent(20));
            world.EnqueueEvent(new GameplayRuntimeEvent(
                new RuntimeFrame(3),
                GameplayRuntimeEventType.WorldTicked,
                commandId: 0,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: 0,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: string.Empty,
                traceId: "pending"));

            GameplayComponentWorldDiagnosticSnapshot snapshot = world.CreateDiagnosticSnapshot();

            Assert.AreEqual(2, snapshot.AliveEntityCount);
            Assert.AreEqual(first, snapshot.Entities[0]);
            Assert.AreEqual(second, snapshot.Entities[1]);
            Assert.AreEqual(2, snapshot.ComponentStoreCount);
            Assert.Less(
                string.CompareOrdinal(snapshot.Stores[0].ComponentTypeName, snapshot.Stores[1].ComponentTypeName),
                0);
            Assert.AreEqual(1, FindStore(snapshot, typeof(TestFlagComponent).FullName).ComponentCount);
            Assert.AreEqual(2, FindStore(snapshot, typeof(TestStatComponent).FullName).ComponentCount);
            Assert.AreEqual(1, snapshot.PendingEventCount);
            Assert.AreEqual(new RuntimeFrame(3), snapshot.EventQueue.OldestFrame);
            Assert.AreEqual(new RuntimeFrame(3), snapshot.EventQueue.NewestFrame);
        }

        [Test]
        public void DiagnosticSnapshot_CopiesEntityAndStoreCollections()
        {
            var world = new GameplayComponentWorld();
            GameplayComponentStore<TestStatComponent> stats = world.CreateStore<TestStatComponent>();
            GameplayEntityId entity = world.CreateEntity();
            stats.Set(entity, new TestStatComponent(10));

            GameplayComponentWorldDiagnosticSnapshot snapshot = new GameplayComponentWorldDiagnostics().BuildSnapshot(world);

            Assert.IsTrue(world.DestroyEntity(entity));
            stats.Clear();

            Assert.AreEqual(1, snapshot.AliveEntityCount);
            Assert.AreEqual(entity, snapshot.Entities[0]);
            Assert.AreEqual(1, snapshot.ComponentStoreCount);
            Assert.AreEqual(1, snapshot.Stores[0].ComponentCount);
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

        private static GameplayComponentStoreDiagnosticSnapshot FindStore(
            GameplayComponentWorldDiagnosticSnapshot snapshot,
            string componentTypeName)
        {
            for (int i = 0; i < snapshot.Stores.Count; i++)
            {
                GameplayComponentStoreDiagnosticSnapshot store = snapshot.Stores[i];
                if (store.ComponentTypeName == componentTypeName)
                    return store;
            }

            Assert.Fail("Missing component store diagnostic snapshot: " + componentTypeName);
            return default;
        }

        private sealed class ComponentWorldRecordingSystem : IGameplaySystem
        {
            private readonly List<GameplayComponentWorld> _observed;

            public ComponentWorldRecordingSystem(List<GameplayComponentWorld> observed)
            {
                _observed = observed;
            }

            public string SystemId => "test.component.world";
            public GameplaySystemPhase Phase => GameplaySystemPhase.Simulation;
            public int Priority => 0;
            public bool IsEnabled => true;

            public void Tick(GameplaySystemContext context)
            {
                _observed.Add(context.ComponentWorld);
                context.ComponentWorld.EnqueueEvent(new GameplayRuntimeEvent(
                    context.Frame,
                    GameplayRuntimeEventType.WorldTicked,
                    commandId: 0,
                    casterEntityId: 0,
                    abilityId: 0,
                    targetEntityId: 0,
                    failureCode: GameplayAbilityRuntimeFailureCode.None,
                    reason: string.Empty,
                    traceId: "component-system"));
            }
        }
    }
}
