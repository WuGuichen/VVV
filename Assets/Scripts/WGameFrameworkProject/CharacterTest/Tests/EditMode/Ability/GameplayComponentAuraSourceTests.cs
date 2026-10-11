using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentAuraSourceTests
    {
        [Test]
        public void AuraSourceSet_SortsEntriesRejectsDuplicatesAndRemovesExpired()
        {
            var sources = new GameplayComponentAuraSourceSetComponent(
                new GameplayComponentAuraSourceEntry(30, 20),
                new GameplayComponentAuraSourceEntry(10, 15));

            GameplayComponentAuraSourceEntry[] entries = sources.ToArray();
            GameplayComponentAuraSourceSetComponent updated = sources.RemoveExpired(new RuntimeFrame(15), out int[] removed);

            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual(10, entries[0].AuraId);
            Assert.AreEqual(30, entries[1].AuraId);
            CollectionAssert.AreEqual(new[] { 10 }, removed);
            Assert.AreEqual(1, updated.Count);
            Assert.IsTrue(updated.TryGet(30, out _));
            Assert.Throws<ArgumentException>(() => new GameplayComponentAuraSourceSetComponent(
                new GameplayComponentAuraSourceEntry(10, 1),
                new GameplayComponentAuraSourceEntry(10, 2)));
        }

        [Test]
        public void AuraSourceCleanupSystem_RemovesExpiredSources()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayComponentAuraSourceSetComponent>().Set(
                entity,
                new GameplayComponentAuraSourceSetComponent(
                    new GameplayComponentAuraSourceEntry(100, 2),
                    new GameplayComponentAuraSourceEntry(200, 0, isPermanent: true)));

            var system = new GameplayComponentAuraSourceCleanupSystem();
            system.Tick(CreateContext(world, 2));

            Assert.AreEqual(GameplaySystemPhase.Resolution, system.Phase);
            Assert.AreEqual(40, system.Priority);
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayComponentAuraSourceSetComponent> store));
            Assert.IsTrue(store.TryGet(entity, out GameplayComponentAuraSourceSetComponent sources));
            Assert.AreEqual(1, sources.Count);
            Assert.IsFalse(sources.TryGet(100, out _));
            Assert.IsTrue(sources.TryGet(200, out _));
        }

        [Test]
        public void AuraSourceHashAndSaveState_RoundtripRestoresHash()
        {
            GameplayComponentWorld source = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = source.CreateEntity();
            source.GetOrCreateStore<GameplayComponentAuraSourceSetComponent>().Set(
                entity,
                new GameplayComponentAuraSourceSetComponent(
                    new GameplayComponentAuraSourceEntry(100, 10, sourceId: 300),
                    new GameplayComponentAuraSourceEntry(200, 0, isPermanent: true, sourceId: 301)));
            long sourceHash = ComputeHash(source);

            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            string json = RuntimeSaveStateJson.SaveToJson(saveState);
            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(json);
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);
            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(loaded.Value);

            Assert.IsTrue(loaded.Success, loaded.Error.ToString());
            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(sourceHash, ComputeHash(target));
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayComponentAuraSourceSetComponent> store));
            Assert.IsTrue(store.TryGet(entity, out GameplayComponentAuraSourceSetComponent sources));
            Assert.IsTrue(sources.TryGet(100, out GameplayComponentAuraSourceEntry sourceEntry));
            Assert.AreEqual(300, sourceEntry.SourceId);
        }

        [Test]
        public void RestoreSaveState_RejectsInvalidAuraSourcePayloadValue()
        {
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = target.CreateEntity();
            target.GetOrCreateStore<GameplayComponentAuraSourceSetComponent>().Set(entity,
                new GameplayComponentAuraSourceSetComponent(new GameplayComponentAuraSourceEntry(100, 1)));
            var provider = new GameplayComponentWorldSaveStateProvider(target);
            var document = JObject.Parse(provider.CaptureSaveState().Value.ModuleStates[0].CustomState.PayloadJson);
            document["componentStores"][0]["entries"][0]["payload"]["payloadJson"] =
                "{\"auraSources\":[{\"auraId\":-1,\"endFrame\":1,\"isPermanent\":false,\"sourceId\":0}]}";
            long before = ComputeHash(target);
            RuntimeSaveStateResult<bool> result = provider.RestoreSaveState(CreateSaveState(document.ToString()));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
            StringAssert.Contains("invalid value", result.Error.Message);
            Assert.AreEqual(before, ComputeHash(target));
        }

        private static GameplayComponentWorld CreateWorld(bool registerSchemas)
        {
            var world = new GameplayComponentWorld();
            if (!registerSchemas)
                return world;

            GameplayComponentAuraSourceSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayComponentAuraSourceSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayComponentAuraSourceSchemaDescriptors.RegisterSaveState(world.Schemas);
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
            return new RuntimeHashCombiner(new IRuntimeHashContributor[] { new GameplayComponentWorldHashContributor(world) }).ComputeHash(RuntimeFrame.Zero);
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
                        new RuntimeCustomState(3311, GameplayComponentWorldSaveStateProvider.ModuleId,
                            GameplayComponentWorldSaveState.CurrentSchemaVersion,
                            componentPayloadJson))
                },
                null);
        }
    }
}
