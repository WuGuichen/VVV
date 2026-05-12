using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayAttributeRuntimeTests
    {
        private const int Hp = 1;
        private const int Attack = 2;
        private const int Defense = 3;
        private const int ActorDefinitionId = 2001;

        [Test]
        public void AttributeSetComponent_SortsAttributesById()
        {
            var attributes = new GameplayAttributeSetComponent(
                new GameplayAttributeValue(Defense, 30, 25),
                new GameplayAttributeValue(Hp, 100, 80),
                new GameplayAttributeValue(Attack, 10, 12));

            GameplayAttributeValue[] values = attributes.ToArray();

            Assert.AreEqual(Hp, values[0].AttributeId);
            Assert.AreEqual(Attack, values[1].AttributeId);
            Assert.AreEqual(Defense, values[2].AttributeId);
        }

        [Test]
        public void AttributeSetComponent_RejectsDuplicateAttributeIds()
        {
            Assert.Throws<System.ArgumentException>(() => new GameplayAttributeSetComponent(
                new GameplayAttributeValue(Hp, 100, 100),
                new GameplayAttributeValue(Hp, 120, 120)));
        }

        [Test]
        public void AttributeSetComponent_SetBaseValueUpdatesStableValue()
        {
            var attributes = new GameplayAttributeSetComponent(new GameplayAttributeValue(Hp, 100, 80));

            GameplayAttributeSetComponent updated = attributes.SetBaseValue(Hp, 120);

            Assert.IsTrue(updated.TryGet(Hp, out GameplayAttributeValue value));
            Assert.AreEqual(120, value.BaseValue);
            Assert.AreEqual(80, value.CurrentValue);
        }

        [Test]
        public void AttributeSetComponent_AddCurrentValueReturnsNewComponent()
        {
            var attributes = new GameplayAttributeSetComponent(new GameplayAttributeValue(Hp, 100, 80));

            GameplayAttributeSetComponent updated = attributes.AddCurrentValue(Hp, -15);

            Assert.AreEqual(80, attributes.GetCurrentValueOrDefault(Hp));
            Assert.AreEqual(65, updated.GetCurrentValueOrDefault(Hp));
        }

        [Test]
        public void AttributeSchema_DiagnosticsWritesStableFields()
        {
            var registry = new GameplayComponentSchemaRegistry();
            GameplayAttributeComponentSchemaDescriptors.RegisterDiagnostics(registry);

            Assert.IsTrue(registry.TryGetDiagnosticWriter(out IGameplayComponentDiagnosticWriter<GameplayAttributeSetComponent> writer));
            var diagnosticWriter = new GameplayComponentDiagnosticWriter();
            writer.WriteDiagnostics(
                new GameplayEntityId(3, 2),
                new GameplayAttributeSetComponent(
                    new GameplayAttributeValue(Attack, 10, 11),
                    new GameplayAttributeValue(Hp, 100, 80)),
                diagnosticWriter);

            GameplayComponentDiagnosticField[] fields = diagnosticWriter.CreateSnapshot();

            Assert.AreEqual("entity.index", fields[0].Key);
            Assert.AreEqual("3", fields[0].Value);
            Assert.AreEqual("entity.generation", fields[1].Key);
            Assert.AreEqual("2", fields[1].Value);
            Assert.AreEqual("count", fields[2].Key);
            Assert.AreEqual("2", fields[2].Value);
            Assert.AreEqual("attribute.0.id", fields[3].Key);
            Assert.AreEqual("1", fields[3].Value);
            Assert.AreEqual("attribute.0.base", fields[4].Key);
            Assert.AreEqual("100", fields[4].Value);
            Assert.AreEqual("attribute.0.current", fields[5].Key);
            Assert.AreEqual("80", fields[5].Value);
        }

        [Test]
        public void AttributeSchema_HashChangesWhenAttributeChanges()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(Hp, 100, 80)));
            long before = ComputeHash(world);

            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(Hp, 100, 70)));
            long after = ComputeHash(world);

            Assert.AreNotEqual(before, after);
        }

        [Test]
        public void AttributeSchema_SaveStateRoundtripRestoresAttributes()
        {
            GameplayComponentWorld source = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = source.CreateEntity();
            source.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(
                    new GameplayAttributeValue(Hp, 100, 80),
                    new GameplayAttributeValue(Attack, 10, 12)));
            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);

            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(saveState);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store));
            Assert.IsTrue(store.TryGet(entity, out GameplayAttributeSetComponent attributes));
            Assert.AreEqual(80, attributes.GetCurrentValueOrDefault(Hp));
            Assert.AreEqual(12, attributes.GetCurrentValueOrDefault(Attack));
            Assert.AreEqual(ComputeHash(source), ComputeHash(target));
        }

        [Test]
        public void AttributeSchema_RejectsInvalidPayloadValues()
        {
            var registry = new GameplayComponentSchemaRegistry();
            GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(registry);
            Assert.IsTrue(registry.TryGetSaveStateAdapter(out IGameplayComponentSaveStateAdapter<GameplayAttributeSetComponent> adapter));
            var payload = new RuntimeCustomState(
                GameplayAttributeComponentSchemaDescriptors.AttributesStableId,
                1,
                "{\"attributes\":[{\"attributeId\":1,\"baseValue\":100,\"currentValue\":100},{\"attributeId\":1,\"baseValue\":120,\"currentValue\":120}]}");

            RuntimeSaveStateResult<GameplayAttributeSetComponent> result =
                adapter.ReadSaveState(new GameplayEntityId(1, 1), payload);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
        }

        [Test]
        public void SetAttributeCommand_UpdatesExistingAttributeAndEmitsEvent()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(Hp, 100, 80)));
            GameplayRuntimeModule module = CreateModule(world);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.SetComponentAttribute(
                RuntimeFrame.Zero,
                entity,
                Hp,
                70,
                traceId: "set-hp"));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store));
            Assert.IsTrue(store.TryGet(entity, out GameplayAttributeSetComponent attributes));
            Assert.AreEqual(70, attributes.GetCurrentValueOrDefault(Hp));
            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.ComponentAttributeChanged, events[0].Type);
            Assert.AreEqual(GameplayAttributeEvents.SetAttributeReason, events[0].Reason);
            Assert.AreEqual(entity, events[0].ComponentEntityId);
            Assert.AreEqual(Hp, events[0].AttributeId);
            Assert.AreEqual(80, events[0].OldAttributeValue);
            Assert.AreEqual(70, events[0].NewAttributeValue);
            Assert.AreEqual(-10, events[0].AttributeDelta);
            Assert.AreEqual("set-hp", events[0].TraceId);
        }

        [Test]
        public void SetAttributeCommand_CreatesAttributeSetWhenMissing()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayRuntimeModule module = CreateModule(world);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.SetComponentAttribute(
                RuntimeFrame.Zero,
                entity,
                Hp,
                50));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store));
            Assert.IsTrue(store.TryGet(entity, out GameplayAttributeSetComponent attributes));
            Assert.IsTrue(attributes.TryGet(Hp, out GameplayAttributeValue value));
            Assert.AreEqual(50, value.BaseValue);
            Assert.AreEqual(50, value.CurrentValue);
        }

        [Test]
        public void AddAttributeCommand_RejectsMissingAttributeSet()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayRuntimeModule module = CreateModule(world);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.AddComponentAttribute(
                RuntimeFrame.Zero,
                entity,
                Hp,
                -10));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.CommandRejected, events[0].Type);
            Assert.AreEqual(GameplayAttributeEvents.MissingAttributeSetReason, events[0].Reason);
        }

        [Test]
        public void AddAttributeCommand_RejectsMissingAttribute()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(Hp, 100, 100)));
            GameplayRuntimeModule module = CreateModule(world);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.AddComponentAttribute(
                RuntimeFrame.Zero,
                entity,
                Defense,
                5));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.CommandRejected, events[0].Type);
            Assert.AreEqual(GameplayAttributeEvents.MissingAttributeReason, events[0].Reason);
        }

        [Test]
        public void AddAttributeCommand_RejectsOverflow()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(Hp, 100, int.MaxValue)));
            GameplayRuntimeModule module = CreateModule(world);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.AddComponentAttribute(
                RuntimeFrame.Zero,
                entity,
                Hp,
                1));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.CommandRejected, events[0].Type);
            Assert.AreEqual(GameplayAttributeEvents.AttributeUpdateFailedReason, events[0].Reason);
        }

        [Test]
        public void AttributeCommand_RejectsStaleComponentEntity()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            Assert.IsTrue(world.DestroyEntity(entity));
            GameplayRuntimeModule module = CreateModule(world);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.SetComponentAttribute(
                RuntimeFrame.Zero,
                entity,
                Hp,
                50));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.CommandRejected, events[0].Type);
            Assert.AreEqual(GameplayAttributeEvents.MissingComponentEntityReason, events[0].Reason);
            Assert.AreEqual(entity, events[0].ComponentEntityId);
        }

        [Test]
        public void AttributeCommand_IsHandledBeforeUnsupportedSystem()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayRuntimeModule module = CreateModule(world);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.SetComponentAttribute(
                RuntimeFrame.Zero,
                entity,
                Hp,
                50));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreNotEqual(GameplayUnsupportedCommandSystem.UnsupportedReason, events[0].Reason);
        }

        [Test]
        public void SpawnDefinition_CanInitializeAttributeSetComponent()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: true);
            var spawnRegistry = new GameplayComponentSpawnRegistry();
            spawnRegistry.Register(new GameplayComponentSpawnDefinition(
                ActorDefinitionId,
                "mxframework.gameplay.test.attribute_actor",
                1,
                new IGameplayComponentSpawnInitializer[]
                {
                    new GameplayComponentSpawnInitializer<GameplayAttributeSetComponent>(
                        GameplayAttributeComponentSchemaDescriptors.AttributesStableId,
                        new GameplayAttributeSetComponent(new GameplayAttributeValue(Hp, 100, 100)))
                }));
            GameplayRuntimeModule module = CreateModule(world, spawnRegistry);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.SpawnComponentEntity(
                RuntimeFrame.Zero,
                ActorDefinitionId));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            GameplayEntityId entity = world.CreateEntitySnapshot()[0];
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store));
            Assert.IsTrue(store.TryGet(entity, out GameplayAttributeSetComponent attributes));
            Assert.AreEqual(100, attributes.GetCurrentValueOrDefault(Hp));
        }

        private static GameplayComponentWorld CreateWorld(bool registerSchemas)
        {
            var world = new GameplayComponentWorld();
            if (registerSchemas)
                RegisterAttributeSchemas(world);

            return world;
        }

        private static void RegisterAttributeSchemas(GameplayComponentWorld world)
        {
            GameplayAttributeComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
        }

        private static GameplayRuntimeModule CreateModule(
            GameplayComponentWorld world,
            GameplayComponentSpawnRegistry spawnRegistry = null)
        {
            return new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                new RuntimeCommandBuffer(),
                tickWorldAutomatically: false,
                configureDefaultPipeline: pipeline =>
                {
                    if (spawnRegistry != null)
                        pipeline.Add(new GameplayComponentSpawnCommandSystem(spawnRegistry));
                    pipeline.Add(new GameplayAttributeCommandSystem());
                },
                componentWorld: world);
        }

        private static long ComputeHash(GameplayComponentWorld world)
        {
            return RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new GameplayComponentWorldHashContributor(world) });
        }
    }
}
