using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentSpawnDefinitionTests
    {
        private const int ActorDefinitionId = 1001;

        [Test]
        public void SpawnRegistry_RejectsDuplicateDefinitionId()
        {
            var registry = new GameplayComponentSpawnRegistry();
            registry.Register(CreateDefinition(ActorDefinitionId, "mxframework.gameplay.test.actor"));

            Assert.Throws<System.InvalidOperationException>(() =>
                registry.Register(CreateDefinition(ActorDefinitionId, "mxframework.gameplay.test.other")));
        }

        [Test]
        public void SpawnRegistry_RejectsDuplicateStableId()
        {
            var registry = new GameplayComponentSpawnRegistry();
            registry.Register(CreateDefinition(1001, "mxframework.gameplay.test.actor"));

            Assert.Throws<System.InvalidOperationException>(() =>
                registry.Register(CreateDefinition(1002, "mxframework.gameplay.test.actor")));
        }

        [Test]
        public void SpawnRegistry_CreateSnapshotReturnsStableOrder()
        {
            var registry = new GameplayComponentSpawnRegistry();
            registry.Register(CreateDefinition(3000, "mxframework.gameplay.test.c"));
            registry.Register(CreateDefinition(1000, "mxframework.gameplay.test.a"));
            registry.Register(CreateDefinition(2000, "mxframework.gameplay.test.b"));

            GameplayComponentSpawnDefinition[] snapshot = registry.CreateSnapshot();

            Assert.AreEqual(1000, snapshot[0].DefinitionId);
            Assert.AreEqual(2000, snapshot[1].DefinitionId);
            Assert.AreEqual(3000, snapshot[2].DefinitionId);
        }

        [Test]
        public void SpawnDefinition_RejectsDuplicateInitializerSchemaId()
        {
            Assert.Throws<System.ArgumentException>(() => new GameplayComponentSpawnDefinition(
                ActorDefinitionId,
                "mxframework.gameplay.test.duplicate_initializer",
                1,
                new IGameplayComponentSpawnInitializer[]
                {
                    new GameplayComponentSpawnInitializer<GameplayIdentityComponent>(
                        GameplayCoreComponentSchemaDescriptors.IdentityStableId,
                        new GameplayIdentityComponent(ActorDefinitionId)),
                    new GameplayComponentSpawnInitializer<GameplayTeamComponent>(
                        GameplayCoreComponentSchemaDescriptors.IdentityStableId,
                        new GameplayTeamComponent(1))
                }));
        }

        [Test]
        public void SpawnCommand_CreatesEntityWithInitialComponents()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayComponentSpawnRegistry registry = CreateRegistry();
            GameplayRuntimeModule module = CreateModule(world, registry);

            EnqueueSpawn(module, traceId: "spawn");
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            GameplayEntityId entity = world.CreateEntitySnapshot()[0];
            AssertSpawnedComponents(world, entity);
        }

        [Test]
        public void SpawnCommand_EmitsCreatedEventWithComponentEntityId()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry());

            EnqueueSpawn(module, traceId: "spawn-event");
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.ComponentEntityCreated, events[0].Type);
            Assert.AreEqual(GameplayRuntimeCommandIds.SpawnComponentEntity, events[0].CommandId);
            Assert.AreEqual(GameplayComponentSpawnEvents.SpawnedReason, events[0].Reason);
            Assert.AreEqual("spawn-event", events[0].TraceId);
            Assert.AreEqual(world.CreateEntitySnapshot()[0], events[0].ComponentEntityId);
        }

        [Test]
        public void SpawnCommand_RejectsMissingDefinition()
        {
            GameplayRuntimeModule module = CreateModule(CreateWorld(registerSchemas: false), new GameplayComponentSpawnRegistry());

            EnqueueSpawn(module, traceId: "missing");
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.CommandRejected, events[0].Type);
            Assert.AreEqual(GameplayComponentSpawnEvents.MissingSpawnDefinitionReason, events[0].Reason);
        }

        [Test]
        public void SpawnCommand_RejectsMissingSpawnRegistryDiagnostically()
        {
            GameplayRuntimeModule module = CreateModule(CreateWorld(registerSchemas: false), null);

            EnqueueSpawn(module, traceId: "missing-registry");
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.CommandRejected, events[0].Type);
            Assert.AreEqual(GameplayComponentSpawnEvents.MissingSpawnRegistryReason, events[0].Reason);
        }

        [Test]
        public void SpawnCommand_RejectsMissingComponentWorldDiagnostically()
        {
            var registry = CreateRegistry();
            var events = new RuntimeEventQueue<GameplayRuntimeEvent>();
            RuntimeCommand command = GameplayRuntimeCommandFactory.SpawnComponentEntity(
                RuntimeFrame.Zero,
                ActorDefinitionId,
                traceId: "missing-world");
            var context = new GameplaySystemContext(
                RuntimeFrame.Zero,
                0d,
                0d,
                new GameplayWorld(),
                new[] { command },
                events);

            new GameplayComponentSpawnCommandSystem(registry).Tick(context);

            var drained = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, events.Drain(RuntimeFrame.Zero, drained));
            Assert.AreEqual(GameplayRuntimeEventType.CommandRejected, drained[0].Type);
            Assert.AreEqual(GameplayComponentSpawnEvents.MissingComponentWorldReason, drained[0].Reason);
            Assert.IsTrue(context.CommandState.IsHandled(command));
        }

        [Test]
        public void SpawnCommand_RollsBackEntityWhenInitializerFails()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            var registry = new GameplayComponentSpawnRegistry();
            registry.Register(new GameplayComponentSpawnDefinition(
                ActorDefinitionId,
                "mxframework.gameplay.test.failing_actor",
                1,
                new IGameplayComponentSpawnInitializer[]
                {
                    new GameplayComponentSpawnInitializer<GameplayTeamComponent>(
                        GameplayCoreComponentSchemaDescriptors.TeamStableId,
                        new GameplayTeamComponent(1)),
                    new FailingInitializer()
                }));
            GameplayRuntimeModule module = CreateModule(world, registry);

            EnqueueSpawn(module, traceId: "fail");
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(0, world.CountAlive);
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayTeamComponent> teams));
            Assert.AreEqual(0, teams.Count);
            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayComponentSpawnEvents.SpawnInitializerFailedReason, events[0].Reason);
            Assert.AreEqual(new GameplayEntityId(1, 1), events[0].ComponentEntityId);
        }

        [Test]
        public void SpawnCommand_IsHandledBeforeUnsupportedSystem()
        {
            GameplayRuntimeModule module = CreateModule(CreateWorld(registerSchemas: false), CreateRegistry());

            EnqueueSpawn(module, traceId: "handled");
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreNotEqual(GameplayUnsupportedCommandSystem.UnsupportedReason, events[0].Reason);
        }

        [Test]
        public void SpawnCommand_ChangesComponentWorldHash()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: true);
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry());
            long before = ComputeHash(world);

            EnqueueSpawn(module, traceId: "hash");
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));
            long after = ComputeHash(world);

            Assert.AreNotEqual(before, after);
        }

        [Test]
        public void SpawnCommand_SaveStateRoundtripPreservesSpawnedComponents()
        {
            GameplayComponentWorld source = CreateWorld(registerSchemas: true);
            GameplayRuntimeModule module = CreateModule(source, CreateRegistry());
            EnqueueSpawn(module, traceId: "save");
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));
            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);

            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(saveState);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            GameplayEntityId entity = target.CreateEntitySnapshot()[0];
            AssertSpawnedComponents(target, entity);
            Assert.AreEqual(ComputeHash(source), ComputeHash(target));
        }

        [Test]
        public void SpawnCommand_RestoreDoesNotRequireSpawnDefinitionRegistry()
        {
            GameplayComponentWorld source = CreateWorld(registerSchemas: true);
            GameplayRuntimeModule module = CreateModule(source, CreateRegistry());
            EnqueueSpawn(module, traceId: "save-no-registry");
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));
            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);

            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(saveState);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(1, target.CountAlive);
            AssertSpawnedComponents(target, target.CreateEntitySnapshot()[0]);
        }

        private static GameplayRuntimeModule CreateModule(
            GameplayComponentWorld world,
            GameplayComponentSpawnRegistry spawnRegistry)
        {
            return new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                new RuntimeCommandBuffer(),
                tickWorldAutomatically: false,
                configureDefaultPipeline: pipeline => pipeline.Add(new GameplayComponentSpawnCommandSystem(spawnRegistry)),
                componentWorld: world);
        }

        private static void EnqueueSpawn(GameplayRuntimeModule module, string traceId)
        {
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.SpawnComponentEntity(
                RuntimeFrame.Zero,
                ActorDefinitionId,
                variantId: 2,
                traceId: traceId));
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

        private static GameplayComponentSpawnRegistry CreateRegistry()
        {
            var registry = new GameplayComponentSpawnRegistry();
            registry.Register(CreateDefinition(ActorDefinitionId, "mxframework.gameplay.test.actor"));
            return registry;
        }

        private static GameplayComponentSpawnDefinition CreateDefinition(int definitionId, string stableId)
        {
            return new GameplayComponentSpawnDefinition(
                definitionId,
                stableId,
                1,
                new IGameplayComponentSpawnInitializer[]
                {
                    new GameplayComponentSpawnInitializer<GameplayIdentityComponent>(
                        GameplayCoreComponentSchemaDescriptors.IdentityStableId,
                        new GameplayIdentityComponent(definitionId, 2)),
                    new GameplayComponentSpawnInitializer<GameplayTeamComponent>(
                        GameplayCoreComponentSchemaDescriptors.TeamStableId,
                        new GameplayTeamComponent(1)),
                    new GameplayComponentSpawnInitializer<GameplayLifecycleComponent>(
                        GameplayCoreComponentSchemaDescriptors.LifecycleStableId,
                        GameplayLifecycleComponent.Alive),
                    new GameplayComponentSpawnInitializer<GameplayTagComponent>(
                        GameplayCoreComponentSchemaDescriptors.TagsStableId,
                        new GameplayTagComponent(new GameplayTagId(20), new GameplayTagId(10))),
                    new GameplayComponentSpawnInitializer<GameplayStatusComponent>(
                        GameplayCoreComponentSchemaDescriptors.StatusesStableId,
                        new GameplayStatusComponent(new GameplayStatusId(100)))
                });
        }

        private static void AssertSpawnedComponents(GameplayComponentWorld world, GameplayEntityId entity)
        {
            Assert.IsTrue(world.IsAlive(entity));

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayIdentityComponent> identities));
            Assert.IsTrue(identities.TryGet(entity, out GameplayIdentityComponent identity));
            Assert.AreEqual(ActorDefinitionId, identity.DefinitionId);
            Assert.AreEqual(2, identity.VariantId);

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayTeamComponent> teams));
            Assert.IsTrue(teams.TryGet(entity, out GameplayTeamComponent team));
            Assert.AreEqual(1, team.TeamId);

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayLifecycleComponent> lifecycle));
            Assert.IsTrue(lifecycle.TryGet(entity, out GameplayLifecycleComponent lifecycleComponent));
            Assert.AreEqual(GameplayLifecycleState.Alive, lifecycleComponent.State);

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayTagComponent> tags));
            Assert.IsTrue(tags.TryGet(entity, out GameplayTagComponent tagComponent));
            CollectionAssert.AreEqual(new[] { 10, 20 }, System.Array.ConvertAll(tagComponent.ToArray(), id => id.Value));

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayStatusComponent> statuses));
            Assert.IsTrue(statuses.TryGet(entity, out GameplayStatusComponent statusComponent));
            CollectionAssert.AreEqual(new[] { 100 }, System.Array.ConvertAll(statusComponent.ToArray(), id => id.Value));
        }

        private static long ComputeHash(GameplayComponentWorld world)
        {
            return RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new GameplayComponentWorldHashContributor(world) });
        }

        private sealed class FailingInitializer : IGameplayComponentSpawnInitializer
        {
            public string SchemaId => "mxframework.gameplay.test.fail";

            public RuntimeSaveStateResult<bool> Apply(
                GameplayComponentWorld world,
                GameplayEntityId entityId,
                GameplayComponentSpawnContext context)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "initializer",
                    "Intentional test failure."));
            }
        }
    }
}
