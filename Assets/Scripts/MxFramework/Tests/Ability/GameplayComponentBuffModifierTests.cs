using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentBuffModifierTests
    {
        [Test]
        public void BuffSet_SortsEntriesAndRejectsDuplicateBuffIds()
        {
            var buffs = new GameplayComponentBuffSetComponent(
                new GameplayComponentBuffEntry(30, 1, 3, 20),
                new GameplayComponentBuffEntry(10, 2, 3, 15));

            GameplayComponentBuffEntry[] entries = buffs.ToArray();

            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual(10, entries[0].BuffId);
            Assert.AreEqual(30, entries[1].BuffId);
            Assert.Throws<ArgumentException>(() => new GameplayComponentBuffSetComponent(
                new GameplayComponentBuffEntry(10, 1, 1, 1),
                new GameplayComponentBuffEntry(10, 1, 1, 2)));
        }

        [Test]
        public void ModifierSet_SortsEntriesRejectsDuplicatesAndEvaluatesAttribute()
        {
            var attributes = new GameplayAttributeSetComponent(new GameplayAttributeValue(1, 100, 80));
            var modifiers = new GameplayComponentModifierSetComponent(
                new GameplayComponentModifierEntry(30, 1, -5),
                new GameplayComponentModifierEntry(10, 1, 20),
                new GameplayComponentModifierEntry(20, 2, 99));

            GameplayComponentModifierEntry[] entries = modifiers.ToArray();

            Assert.AreEqual(10, entries[0].ModifierId);
            Assert.AreEqual(20, entries[1].ModifierId);
            Assert.AreEqual(30, entries[2].ModifierId);
            Assert.AreEqual(95, GameplayComponentModifierEvaluator.GetModifiedCurrentValue(attributes, modifiers, 1));
            Assert.Throws<ArgumentException>(() => new GameplayComponentModifierSetComponent(
                new GameplayComponentModifierEntry(10, 1, 1),
                new GameplayComponentModifierEntry(10, 1, 2)));
        }

        [Test]
        public void BuffCleanupSystem_RemovesExpiredBuffsAndLinkedModifiers()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayComponentBuffSetComponent>().Set(
                entity,
                new GameplayComponentBuffSetComponent(
                    new GameplayComponentBuffEntry(100, 1, 1, 2),
                    new GameplayComponentBuffEntry(200, 1, 1, 0, isPermanent: true)));
            world.GetOrCreateStore<GameplayComponentModifierSetComponent>().Set(
                entity,
                new GameplayComponentModifierSetComponent(
                    new GameplayComponentModifierEntry(1000, 1, 5, sourceBuffId: 100),
                    new GameplayComponentModifierEntry(2000, 1, 7, sourceBuffId: 200)));

            new GameplayComponentBuffCleanupSystem().Tick(CreateContext(world, 2));

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore));
            Assert.IsTrue(buffStore.TryGet(entity, out GameplayComponentBuffSetComponent buffs));
            Assert.AreEqual(1, buffs.Count);
            Assert.IsFalse(buffs.TryGet(100, out _));
            Assert.IsTrue(buffs.TryGet(200, out _));
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayComponentModifierSetComponent> modifierStore));
            Assert.IsTrue(modifierStore.TryGet(entity, out GameplayComponentModifierSetComponent modifiers));
            Assert.AreEqual(1, modifiers.Count);
            Assert.AreEqual(7, modifiers.GetAdditiveValue(1));
        }

        [Test]
        public void BuffModifierHash_ChangesWhenStateChanges()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = world.CreateEntity();
            long empty = ComputeHash(world);

            world.GetOrCreateStore<GameplayComponentBuffSetComponent>().Set(
                entity,
                new GameplayComponentBuffSetComponent(new GameplayComponentBuffEntry(100, 1, 3, 10)));
            long withBuff = ComputeHash(world);
            world.GetOrCreateStore<GameplayComponentModifierSetComponent>().Set(
                entity,
                new GameplayComponentModifierSetComponent(new GameplayComponentModifierEntry(1000, 1, 5, sourceBuffId: 100)));
            long withModifier = ComputeHash(world);

            Assert.AreNotEqual(empty, withBuff);
            Assert.AreNotEqual(withBuff, withModifier);
        }

        [Test]
        public void BuffModifierSaveState_RoundtripRestoresHash()
        {
            GameplayComponentWorld source = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = source.CreateEntity();
            source.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(1, 100, 90)));
            source.GetOrCreateStore<GameplayComponentBuffSetComponent>().Set(
                entity,
                new GameplayComponentBuffSetComponent(new GameplayComponentBuffEntry(100, 2, 3, 10, sourceId: 77)));
            source.GetOrCreateStore<GameplayComponentModifierSetComponent>().Set(
                entity,
                new GameplayComponentModifierSetComponent(new GameplayComponentModifierEntry(1000, 1, 5, sourceBuffId: 100)));
            long sourceHash = ComputeHash(source);

            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            string json = RuntimeSaveStateJson.SaveToJson(saveState);
            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(json);
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);
            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(loaded.Value);

            Assert.IsTrue(loaded.Success, loaded.Error.ToString());
            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(sourceHash, ComputeHash(target));
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayComponentModifierSetComponent> modifierStore));
            Assert.IsTrue(modifierStore.TryGet(entity, out GameplayComponentModifierSetComponent modifiers));
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> attributeStore));
            Assert.IsTrue(attributeStore.TryGet(entity, out GameplayAttributeSetComponent attributes));
            Assert.AreEqual(95, GameplayComponentModifierEvaluator.GetModifiedCurrentValue(
                attributes,
                modifiers,
                1));
        }

        [Test]
        public void RestoreSaveState_RejectsInvalidBuffPayloadValue()
        {
            RuntimeSaveState saveState = CreateSaveState(
                "{\"schemaVersion\":1,\"entities\":[{\"index\":1,\"generation\":1}],\"componentStores\":[{\"schemaId\":\"mxframework.gameplay.buffs\",\"schemaVersion\":1,\"entries\":[{\"entityIndex\":1,\"entityGeneration\":1,\"payload\":{\"typeId\":\"mxframework.gameplay.buffs\",\"schemaVersion\":1,\"payloadJson\":\"{\\\"buffs\\\":[{\\\"buffId\\\":-1,\\\"stackCount\\\":1,\\\"maxStackCount\\\":1,\\\"endFrame\\\":1,\\\"isPermanent\\\":false,\\\"sourceId\\\":0}]}\"}}]}]}");

            RuntimeSaveStateResult<bool> result = new GameplayComponentWorldSaveStateProvider(CreateWorld(registerSchemas: true)).RestoreSaveState(saveState);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
            StringAssert.Contains("invalid value", result.Error.Message);
        }

        private static GameplayComponentWorld CreateWorld(bool registerSchemas)
        {
            var world = new GameplayComponentWorld();
            if (!registerSchemas)
                return world;

            GameplayAttributeComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            GameplayComponentBuffSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayComponentBuffSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayComponentBuffSchemaDescriptors.RegisterSaveState(world.Schemas);
            GameplayComponentModifierSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayComponentModifierSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayComponentModifierSchemaDescriptors.RegisterSaveState(world.Schemas);
            return world;
        }

        private static GameplaySystemContext CreateContext(GameplayComponentWorld world, long frame)
        {
            return new GameplaySystemContext(
                new RuntimeFrame(frame),
                0d,
                0d,
                new GameplayWorld(),
                Array.Empty<RuntimeCommand>(),
                world.Events,
                componentWorld: world);
        }

        private static long ComputeHash(GameplayComponentWorld world)
        {
            return RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new GameplayComponentWorldHashContributor(world) });
        }

        private static RuntimeSaveState CreateSaveState(string componentPayloadJson)
        {
            return new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                string.Empty,
                string.Empty,
                string.Empty,
                0L,
                null,
                null,
                new[]
                {
                    new RuntimeModuleSaveState(
                        GameplayComponentWorldSaveStateProvider.ModuleId,
                        GameplayComponentWorldSaveState.CurrentSchemaVersion,
                        new RuntimeCustomState(
                            GameplayComponentWorldSaveStateProvider.ModuleId,
                            GameplayComponentWorldSaveState.CurrentSchemaVersion,
                            componentPayloadJson))
                },
                null);
        }
    }
}
