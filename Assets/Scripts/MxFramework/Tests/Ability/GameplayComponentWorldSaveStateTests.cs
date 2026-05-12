using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentWorldSaveStateTests
    {
        [Test]
        public void CaptureSaveState_WritesEntitiesAndComponentStoresInStableOrder()
        {
            GameplayComponentWorld world = CreateWorld(registerSave: true);
            PopulateWorld(world);

            RuntimeSaveStateResult<RuntimeSaveState> result = new GameplayComponentWorldSaveStateProvider(world, () => 17L).CaptureSaveState();

            Assert.IsTrue(result.Success, result.Error.ToString());
            Assert.AreEqual(17L, result.Value.Frame);
            Assert.AreEqual(1, result.Value.ModuleStates.Count);
            RuntimeCustomState custom = result.Value.ModuleStates[0].CustomState;
            RuntimeSaveStateResult<RuntimeSaveState> roundtrip = RuntimeSaveStateJson.LoadFromJson(RuntimeSaveStateJson.SaveToJson(result.Value));

            Assert.IsTrue(roundtrip.Success, roundtrip.Error.ToString());
            StringAssert.Contains("\"schemaId\":\"mxframework.gameplay.identity\"", custom.PayloadJson);
            StringAssert.Contains("\"schemaId\":\"mxframework.gameplay.lifecycle\"", custom.PayloadJson);
            StringAssert.Contains("\"schemaId\":\"mxframework.gameplay.statuses\"", custom.PayloadJson);
            StringAssert.Contains("\"schemaId\":\"mxframework.gameplay.tags\"", custom.PayloadJson);
            StringAssert.Contains("\"schemaId\":\"mxframework.gameplay.team\"", custom.PayloadJson);
        }

        [Test]
        public void RestoreSaveState_RecreatesEntitiesComponentsAndHash()
        {
            GameplayComponentWorld source = CreateWorld(registerSave: true);
            PopulateWorld(source);
            long sourceHash = ComputeHash(source);

            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            GameplayComponentWorld target = CreateWorld(registerSave: true);
            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(saveState);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(sourceHash, ComputeHash(target));
        }

        [Test]
        public void CaptureSaveState_SkipsUnsupportedComponentStores()
        {
            GameplayComponentWorld baseline = CreateWorld(registerSave: true);
            GameplayComponentWorld withUnsupported = CreateWorld(registerSave: true);
            GameplayEntityId a = baseline.CreateEntity();
            baseline.GetOrCreateStore<GameplayTeamComponent>().Set(a, new GameplayTeamComponent(1));
            GameplayEntityId b = withUnsupported.CreateEntity();
            withUnsupported.GetOrCreateStore<GameplayTeamComponent>().Set(b, new GameplayTeamComponent(1));
            withUnsupported.GetOrCreateStore<UnsupportedComponent>().Set(b, new UnsupportedComponent(9));

            string baselineJson = CapturePayload(baseline);
            string unsupportedJson = CapturePayload(withUnsupported);

            Assert.AreEqual(baselineJson, unsupportedJson);
            Assert.IsFalse(unsupportedJson.Contains("unsupported"));
        }

        [Test]
        public void RestoreSaveState_RejectsMissingSchema()
        {
            GameplayComponentWorld source = CreateWorld(registerSave: true);
            PopulateWorld(source);
            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            GameplayComponentWorld target = new GameplayComponentWorld();

            RuntimeSaveStateResult<bool> result = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(saveState);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
            StringAssert.Contains("schemaId", result.Error.Path);
        }

        [Test]
        public void RestoreSaveState_RejectsMissingSaveAdapter()
        {
            GameplayComponentWorld source = CreateWorld(registerSave: true);
            PopulateWorld(source);
            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            GameplayComponentWorld target = CreateWorld(registerSave: false);

            RuntimeSaveStateResult<bool> result = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(saveState);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
            StringAssert.Contains("SaveState adapter", result.Error.Message);
        }

        [Test]
        public void RestoreSaveState_RejectsUnsupportedSchemaVersion()
        {
            GameplayComponentWorld source = CreateWorld(registerSave: true);
            PopulateWorld(source);
            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            RuntimeModuleSaveState module = saveState.ModuleStates[0];
            var broken = new RuntimeSaveState(
                saveState.SchemaVersion,
                saveState.CreatedAtUtc,
                saveState.FrameworkVersion,
                saveState.ConfigVersion,
                saveState.ResourceCatalogVersion,
                saveState.Frame,
                saveState.Entities,
                saveState.GlobalCounters,
                new[]
                {
                    new RuntimeModuleSaveState(
                        module.ModuleId,
                        999,
                        module.CustomState)
                },
                saveState.Metadata);

            RuntimeSaveStateResult<bool> result = new GameplayComponentWorldSaveStateProvider(CreateWorld(registerSave: true)).RestoreSaveState(broken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.UnsupportedVersion, result.Error.Code);
        }

        [Test]
        public void RestoreSaveState_RejectsInvalidOrStaleEntityId()
        {
            RuntimeSaveState saveState = CreateSaveState(
                "{\"schemaVersion\":1,\"entities\":[{\"index\":1,\"generation\":1}],\"componentStores\":[{\"schemaId\":\"mxframework.gameplay.team\",\"schemaVersion\":1,\"entries\":[{\"entityIndex\":2,\"entityGeneration\":1,\"payload\":{\"typeId\":\"mxframework.gameplay.team\",\"schemaVersion\":1,\"payloadJson\":\"{\\\"teamId\\\":1}\"}}]}]}");

            RuntimeSaveStateResult<bool> result = new GameplayComponentWorldSaveStateProvider(CreateWorld(registerSave: true)).RestoreSaveState(saveState);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.UnknownEntity, result.Error.Code);
        }

        [Test]
        public void RestoreSaveState_RejectsDuplicateEntityIndex()
        {
            RuntimeSaveState saveState = CreateSaveState(
                "{\"schemaVersion\":1,\"entities\":[{\"index\":1,\"generation\":1},{\"index\":1,\"generation\":2}],\"componentStores\":[]}");

            RuntimeSaveStateResult<bool> result = new GameplayComponentWorldSaveStateProvider(CreateWorld(registerSave: true)).RestoreSaveState(saveState);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.UnknownEntity, result.Error.Code);
            StringAssert.Contains("index is duplicated", result.Error.Message);
        }

        [Test]
        public void RestoreSaveState_RejectsInvalidCorePayloadValue()
        {
            RuntimeSaveState saveState = CreateSaveState(
                "{\"schemaVersion\":1,\"entities\":[{\"index\":1,\"generation\":1}],\"componentStores\":[{\"schemaId\":\"mxframework.gameplay.tags\",\"schemaVersion\":1,\"entries\":[{\"entityIndex\":1,\"entityGeneration\":1,\"payload\":{\"typeId\":\"mxframework.gameplay.tags\",\"schemaVersion\":1,\"payloadJson\":\"{\\\"ids\\\":[-1]}\"}}]}]}");

            RuntimeSaveStateResult<bool> result = new GameplayComponentWorldSaveStateProvider(CreateWorld(registerSave: true)).RestoreSaveState(saveState);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
            StringAssert.Contains("invalid value", result.Error.Message);
        }

        [Test]
        public void RestoreSaveState_RejectsInvalidLifecycleState()
        {
            RuntimeSaveState saveState = CreateSaveState(
                "{\"schemaVersion\":1,\"entities\":[{\"index\":1,\"generation\":1}],\"componentStores\":[{\"schemaId\":\"mxframework.gameplay.lifecycle\",\"schemaVersion\":1,\"entries\":[{\"entityIndex\":1,\"entityGeneration\":1,\"payload\":{\"typeId\":\"mxframework.gameplay.lifecycle\",\"schemaVersion\":1,\"payloadJson\":\"{\\\"state\\\":999}\"}}]}]}");

            RuntimeSaveStateResult<bool> result = new GameplayComponentWorldSaveStateProvider(CreateWorld(registerSave: true)).RestoreSaveState(saveState);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
            StringAssert.Contains("Lifecycle state", result.Error.Message);
        }

        [Test]
        public void RuntimeSaveStateJson_RoundtripRestoresComponentWorld()
        {
            GameplayComponentWorld source = CreateWorld(registerSave: true);
            PopulateWorld(source);
            long sourceHash = ComputeHash(source);
            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            string json = RuntimeSaveStateJson.SaveToJson(saveState);
            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(json);
            GameplayComponentWorld target = CreateWorld(registerSave: true);

            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(loaded.Value);

            Assert.IsTrue(loaded.Success, loaded.Error.ToString());
            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(sourceHash, ComputeHash(target));
        }

        [Test]
        public void TagStatusSaveAdapters_PreserveSortedIdsAndRestoreEmptySets()
        {
            GameplayComponentWorld source = CreateWorld(registerSave: true);
            GameplayEntityId entity = source.CreateEntity();
            source.GetOrCreateStore<GameplayTagComponent>().Set(
                entity,
                new GameplayTagComponent(new GameplayTagId(30), new GameplayTagId(10), new GameplayTagId(10)));
            source.GetOrCreateStore<GameplayStatusComponent>().Set(entity, new GameplayStatusComponent());
            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            GameplayComponentWorld target = CreateWorld(registerSave: true);

            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(saveState);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayTagComponent> tags));
            Assert.IsTrue(tags.TryGet(entity, out GameplayTagComponent tagComponent));
            CollectionAssert.AreEqual(new[] { 10, 30 }, Array.ConvertAll(tagComponent.ToArray(), id => id.Value));
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayStatusComponent> statuses));
            Assert.IsTrue(statuses.TryGet(entity, out GameplayStatusComponent statusComponent));
            Assert.AreEqual(0, statusComponent.Count);
        }

        private static GameplayComponentWorld CreateWorld(bool registerSave)
        {
            var world = new GameplayComponentWorld();
            GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            if (registerSave)
                GameplayCoreComponentSchemaDescriptors.RegisterSaveState(world.Schemas);

            return world;
        }

        private static void PopulateWorld(GameplayComponentWorld world)
        {
            GameplayEntityId player = world.CreateEntity();
            GameplayEntityId enemy = world.CreateEntity();
            world.GetOrCreateStore<GameplayIdentityComponent>().Set(player, new GameplayIdentityComponent(1001, 2));
            world.GetOrCreateStore<GameplayTeamComponent>().Set(player, new GameplayTeamComponent(1));
            world.GetOrCreateStore<GameplayLifecycleComponent>().Set(player, GameplayLifecycleComponent.Alive);
            world.GetOrCreateStore<GameplayTagComponent>().Set(player, new GameplayTagComponent(new GameplayTagId(20), new GameplayTagId(10)));
            world.GetOrCreateStore<GameplayStatusComponent>().Set(player, new GameplayStatusComponent(new GameplayStatusId(200), new GameplayStatusId(100)));
            world.GetOrCreateStore<GameplayIdentityComponent>().Set(enemy, new GameplayIdentityComponent(2001));
            world.GetOrCreateStore<GameplayTeamComponent>().Set(enemy, new GameplayTeamComponent(2));
            world.GetOrCreateStore<GameplayLifecycleComponent>().Set(enemy, GameplayLifecycleComponent.Alive);
        }

        private static long ComputeHash(GameplayComponentWorld world)
        {
            return RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new GameplayComponentWorldHashContributor(world) });
        }

        private static string CapturePayload(GameplayComponentWorld world)
        {
            return new GameplayComponentWorldSaveStateProvider(world).CaptureSaveState().Value.ModuleStates[0].CustomState.PayloadJson;
        }

        private static RuntimeSaveState CreateSaveState(string payloadJson)
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
                            payloadJson))
                },
                null);
        }

        private readonly struct UnsupportedComponent : IGameplayComponent
        {
            public UnsupportedComponent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }
}
