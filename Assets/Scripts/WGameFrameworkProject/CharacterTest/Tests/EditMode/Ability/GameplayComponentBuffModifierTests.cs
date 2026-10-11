using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentBuffModifierTests
    {
        private const int BuffId = 100;
        private const int StackBuffId = 200;
        private const int ReplaceBuffId = 300;
        private const int AttributeId = 1;
        private static readonly GameplayStatusId StatusId = new GameplayStatusId(9001);

        [Test]
        public void BuffDefinitionRegistry_SortsRejectsDuplicatesAndComputesStableHash()
        {
            var registry = new GameplayBuffDefinitionRegistry(
                Definition(300),
                Definition(100));

            GameplayBuffDefinition[] definitions = registry.ToArray();

            Assert.AreEqual(2, registry.Count);
            Assert.AreEqual(100, definitions[0].BuffId);
            Assert.AreEqual(300, definitions[1].BuffId);
            Assert.AreEqual(registry.ComputeStableHash(), new GameplayBuffDefinitionRegistry(
                Definition(300),
                Definition(100)).ComputeStableHash());
            Assert.Throws<ArgumentException>(() => new GameplayBuffDefinitionRegistry(
                Definition(100),
                Definition(100)));
            Assert.Throws<ArgumentException>(() => new GameplayBuffDefinition(
                400,
                "bad.duplicate.effects",
                2,
                1,
                GameplayBuffStackPolicy.RefreshDuration,
                new[]
                {
                    GameplayBuffEffectDefinition.AttributeModifier(1, AttributeId, 1),
                    GameplayBuffEffectDefinition.Status(1, StatusId)
                }));
        }

        [Test]
        public void BuffSet_SortsEntriesAndRejectsDuplicateBuffIds()
        {
            var buffs = new GameplayComponentBuffSetComponent(
                new GameplayComponentBuffEntry(30, 1, 3, 20),
                new GameplayComponentBuffEntry(10, 2, 1, 15));

            GameplayComponentBuffEntry[] entries = buffs.ToArray();

            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual(10, entries[0].BuffId);
            Assert.AreEqual(1, entries[0].StartFrame);
            Assert.AreEqual(30, entries[1].BuffId);
            Assert.Throws<ArgumentException>(() => new GameplayComponentBuffSetComponent(
                new GameplayComponentBuffEntry(10, 1, 0, 1),
                new GameplayComponentBuffEntry(10, 1, 0, 2)));
        }

        [Test]
        public void BuffSet_RepeatedSingleEntryRefreshUsesInlineStorage()
        {
            var buffs = new GameplayComponentBuffSetComponent(
                new GameplayComponentBuffEntry(BuffId, 1, 1, 10, sourceId: 7));
            buffs = buffs.Upsert(
                new GameplayComponentBuffEntry(BuffId, 1, 1, 11, sourceId: 7));

            Assert.IsTrue(buffs.StoresSingleEntryInline);
            for (int frame = 12; frame < 76; frame++)
            {
                buffs = buffs.Upsert(
                    new GameplayComponentBuffEntry(BuffId, 1, 1, frame, sourceId: 7));
            }

            Assert.AreEqual(1, buffs.Count);
            Assert.IsTrue(buffs.StoresSingleEntryInline);
            Assert.IsTrue(buffs.TryGet(BuffId, out GameplayComponentBuffEntry buff));
            Assert.AreEqual(75L, buff.EndFrame);
            Assert.AreEqual(7, buff.SourceId);
        }

        [Test]
        public void BuffApplicationSystem_AppliesRefreshesStacksReplacesAndRemoves()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = CreateEntity(world);
            var requests = new GameplayBuffRequestBuffer();
            var system = new GameplayBuffApplicationSystem(Definitions(), requests);

            requests.EnqueueApply(entity, BuffId, sourceId: 11, traceId: new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_GameplayComponentBuffModifierTests.Id_apply, 0));
            system.Tick(CreateContext(world, 10));
            AssertBuff(world, entity, BuffId, stack: 1, startFrame: 10, endFrame: 12, sourceId: 11);

            requests.EnqueueApply(entity, BuffId, sourceId: 22, traceId: new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_GameplayComponentBuffModifierTests.Id_refresh, 0));
            system.Tick(CreateContext(world, 11));
            AssertBuff(world, entity, BuffId, stack: 1, startFrame: 10, endFrame: 13, sourceId: 22);

            requests.EnqueueApply(entity, StackBuffId, traceId: new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_GameplayComponentBuffModifierTests.Id_stack1, 0));
            system.Tick(CreateContext(world, 12));
            requests.EnqueueApply(entity, StackBuffId, traceId: new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_GameplayComponentBuffModifierTests.Id_stack2, 0));
            system.Tick(CreateContext(world, 13));
            AssertBuff(world, entity, StackBuffId, stack: 2, startFrame: 12, endFrame: 16);
            requests.EnqueueApply(entity, StackBuffId, traceId: new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_GameplayComponentBuffModifierTests.Id_stack3, 0));
            system.Tick(CreateContext(world, 14));
            AssertBuff(world, entity, StackBuffId, stack: 3, startFrame: 12, endFrame: 17);
            requests.EnqueueApply(entity, StackBuffId, traceId: new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_GameplayComponentBuffModifierTests.Id_stack4, 0));
            system.Tick(CreateContext(world, 15));
            AssertBuff(world, entity, StackBuffId, stack: 3, startFrame: 12, endFrame: 18);

            requests.EnqueueApply(entity, ReplaceBuffId, traceId: new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_GameplayComponentBuffModifierTests.Id_replace1, 0));
            system.Tick(CreateContext(world, 16));
            requests.EnqueueApply(entity, ReplaceBuffId, traceId: new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_GameplayComponentBuffModifierTests.Id_replace2, 0));
            system.Tick(CreateContext(world, 17));
            AssertBuff(world, entity, ReplaceBuffId, stack: 1, startFrame: 17, endFrame: 21);

            requests.EnqueueRemove(entity, BuffId, traceId: new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_GameplayComponentBuffModifierTests.Id_remove, 0));
            system.Tick(CreateContext(world, 18));
            AssertNoBuff(world, entity, BuffId);
        }

        [Test]
        public void BuffCommandSystem_ValidatesPayloadAndWritesRequestBuffer()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = CreateEntity(world);
            var requests = new GameplayBuffRequestBuffer();
            var system = new GameplayBuffCommandSystem(Definitions(), requests);
            RuntimeCommand apply = GameplayRuntimeCommandFactory.ApplyComponentBuff(
                new RuntimeFrame(5),
                entity,
                BuffId,
                sourceId: 77,
                traceId: new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_GameplayComponentBuffModifierTests.Id_cmd, 0));

            system.Tick(CreateContext(world, 5, apply));

            GameplayBuffApplicationRequest[] snapshot = requests.CreateSnapshot();
            Assert.AreEqual(1, snapshot.Length);
            Assert.AreEqual(GameplayBuffApplicationRequestKind.Apply, snapshot[0].Kind);
            Assert.AreEqual(entity, snapshot[0].TargetEntityId);
            Assert.AreEqual(BuffId, snapshot[0].BuffId);
            Assert.AreEqual(77, snapshot[0].SourceId);

            RuntimeCommand invalid = GameplayRuntimeCommandFactory.ApplyComponentBuff(
                new RuntimeFrame(5),
                entity,
                999);
            system.Tick(CreateContext(world, 5, invalid));
            var events = new List<GameplayRuntimeEvent>();
            world.DrainEvents(new RuntimeFrame(5), events);
            Assert.IsTrue(events.Exists(e =>
                e.Type == GameplayRuntimeEventType.ComponentBuffRejected
                && e.BuffId == 999
                && e.Reason == GameplayComponentBuffEvents.MissingBuffDefinitionReason));

            RuntimeCommand reservedPayload = new RuntimeCommand(
                new RuntimeFrame(5),
                0,
                GameplayRuntimeCommandIds.ApplyComponentBuff,
                targetId: entity.Index,
                payload0: entity.Generation,
                payload1: BuffId,
                payload2: 7,
                traceId: new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_GameplayComponentBuffModifierTests.Id_reserved, 0));
            system.Tick(CreateContext(world, 5, reservedPayload));
            events.Clear();
            world.DrainEvents(new RuntimeFrame(5), events);
            Assert.IsTrue(events.Exists(e =>
                e.Type == GameplayRuntimeEventType.ComponentBuffRejected
                && e.BuffId == BuffId
                && e.Reason == GameplayComponentBuffEvents.InvalidCommandPayloadReason));
        }

        [Test]
        public void BuffEffectProjection_CreatesAndCleansSourceOwnedModifiersAndStatuses()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = CreateEntity(world);
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(AttributeId, 10, 10)));
            world.GetOrCreateStore<GameplayComponentBuffSetComponent>().Set(
                entity,
                new GameplayComponentBuffSetComponent(new GameplayComponentBuffEntry(BuffId, 1, 1, 10)));
            world.GetOrCreateStore<GameplayComponentModifierSetComponent>().Set(
                entity,
                new GameplayComponentModifierSetComponent(new GameplayComponentModifierEntry(9, AttributeId, 2)));
            world.GetOrCreateStore<GameplayStatusComponent>().Set(
                entity,
                new GameplayStatusComponent(new GameplayStatusId(7001)));
            var projection = new GameplayBuffEffectProjectionSystem(Definitions());

            projection.Tick(CreateContext(world, 1));

            Assert.AreEqual(17, GameplayComponentAttributeQuery.GetCurrentValueOrDefault(world, entity, AttributeId));
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayStatusComponent> statusStore));
            Assert.IsTrue(statusStore.TryGet(entity, out GameplayStatusComponent statuses));
            Assert.IsTrue(statuses.Contains(StatusId));
            Assert.IsTrue(statuses.Contains(new GameplayStatusId(7001)));

            world.GetOrCreateStore<GameplayComponentBuffSetComponent>().Remove(entity);
            projection.Tick(CreateContext(world, 2));

            Assert.AreEqual(12, GameplayComponentAttributeQuery.GetCurrentValueOrDefault(world, entity, AttributeId));
            Assert.IsTrue(statusStore.TryGet(entity, out statuses));
            Assert.IsFalse(statuses.Contains(StatusId));
            Assert.IsTrue(statuses.Contains(new GameplayStatusId(7001)));
        }

        [Test]
        public void BuffEffectProjection_StableTickDoesNotAllocateAfterWarmup()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = CreateEntity(world);
            world.GetOrCreateStore<GameplayComponentBuffSetComponent>().Set(
                entity,
                new GameplayComponentBuffSetComponent(new GameplayComponentBuffEntry(BuffId, 1, 1, 10)));
            var projection = new GameplayBuffEffectProjectionSystem(Definitions());
            GameplaySystemContext context = CreateContext(world, 1);
            projection.Tick(context);
            projection.Tick(context);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            projection.Tick(context);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void BuffCleanupSystem_RemovesExpiredBuffsBeforeProjectionCleansEffects()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = CreateEntity(world);
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(AttributeId, 10, 10)));
            world.GetOrCreateStore<GameplayComponentBuffSetComponent>().Set(
                entity,
                new GameplayComponentBuffSetComponent(
                    new GameplayComponentBuffEntry(BuffId, 1, 1, 2),
                    new GameplayComponentBuffEntry(ReplaceBuffId, 1, 1, 0, isPermanent: true)));
            var projection = new GameplayBuffEffectProjectionSystem(Definitions());
            projection.Tick(CreateContext(world, 1));

            new GameplayComponentBuffCleanupSystem().Tick(CreateContext(world, 2));
            projection.Tick(CreateContext(world, 2));

            AssertNoBuff(world, entity, BuffId);
            AssertBuff(world, entity, ReplaceBuffId, stack: 1, startFrame: 1, endFrame: 0);
            Assert.AreEqual(8, GameplayComponentAttributeQuery.GetCurrentValueOrDefault(world, entity, AttributeId));
        }

        [Test]
        public void BuffCleanupSystem_NoExpiredBuffsDoesNotAllocateAfterWarmup()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = CreateEntity(world);
            world.GetOrCreateStore<GameplayComponentBuffSetComponent>().Set(
                entity,
                new GameplayComponentBuffSetComponent(new GameplayComponentBuffEntry(BuffId, 1, 1, 10)));
            var cleanup = new GameplayComponentBuffCleanupSystem();
            GameplaySystemContext context = CreateContext(world, 2);
            cleanup.Tick(context);
            cleanup.Tick(context);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            cleanup.Tick(context);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void BuffModifierHashAndSaveState_RoundtripRestoresHash()
        {
            GameplayComponentWorld source = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = CreateEntity(source);
            source.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(AttributeId, 100, 90)));
            source.GetOrCreateStore<GameplayComponentBuffSetComponent>().Set(
                entity,
                new GameplayComponentBuffSetComponent(new GameplayComponentBuffEntry(BuffId, 2, 3, 10, sourceId: 77)));
            new GameplayBuffEffectProjectionSystem(Definitions()).Tick(CreateContext(source, 3));
            long sourceHash = ComputeHash(source);

            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            string json = RuntimeSaveStateJson.SaveToJson(saveState);
            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(json);
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);
            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(loaded.Value);

            Assert.IsTrue(loaded.Success, loaded.Error.ToString());
            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(sourceHash, ComputeHash(target));
            Assert.AreEqual(100, GameplayComponentAttributeQuery.GetCurrentValueOrDefault(target, entity, AttributeId));
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayStatusComponent> statusStore));
            Assert.IsTrue(statusStore.TryGet(entity, out GameplayStatusComponent statuses));
            Assert.IsTrue(statuses.Contains(StatusId));
            // Restore must preserve subsequent expiry/projection, not only the capture-time hash.
            var cleanup = new GameplayComponentBuffCleanupSystem();
            var sourceProjection = new GameplayBuffEffectProjectionSystem(Definitions());
            var restoredProjection = new GameplayBuffEffectProjectionSystem(Definitions());
            for (long frame = 4; frame <= 12; frame++)
            {
                cleanup.Tick(CreateContext(source, frame));
                cleanup.Tick(CreateContext(target, frame));
                sourceProjection.Tick(CreateContext(source, frame));
                restoredProjection.Tick(CreateContext(target, frame));
                Assert.AreEqual(ComputeHash(source), ComputeHash(target), "first divergence frame=" + frame + " field=gameplay.buff-modifier.hash");
                Assert.AreEqual(GameplayComponentAttributeQuery.GetCurrentValueOrDefault(source, entity, AttributeId),
                    GameplayComponentAttributeQuery.GetCurrentValueOrDefault(target, entity, AttributeId),
                    "first divergence frame=" + frame + " field=attribute.current");
            }
            AssertNoBuff(source, entity, BuffId);
            AssertNoBuff(target, entity, BuffId);
        }

        [Test]
        public void RestoreSaveState_RejectsInvalidBuffPayloadValue()
        {
            GameplayComponentWorld valid = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = CreateEntity(valid);
            valid.GetOrCreateStore<GameplayComponentBuffSetComponent>().Set(entity,
                new GameplayComponentBuffSetComponent(new GameplayComponentBuffEntry(BuffId, 1, 0, 1)));
            RuntimeSaveState captured = new GameplayComponentWorldSaveStateProvider(valid).CaptureSaveState().Value;
            var component = Newtonsoft.Json.Linq.JObject.Parse(captured.ModuleStates[0].CustomState.PayloadJson);
            Newtonsoft.Json.Linq.JToken payload = component["componentStores"][0]["entries"][0]["payload"];
            var buffPayload = Newtonsoft.Json.Linq.JObject.Parse((string)payload["payloadJson"]);
            var buff = (Newtonsoft.Json.Linq.JObject)buffPayload.GetValue("buffs", StringComparison.OrdinalIgnoreCase)[0];
            var idProperty = (Newtonsoft.Json.Linq.JProperty)buff.GetValue("buffId", StringComparison.OrdinalIgnoreCase).Parent;
            idProperty.Value = -1;
            payload["payloadJson"] = buffPayload.ToString(Newtonsoft.Json.Formatting.None);
            RuntimeSaveState saveState = CreateSaveState(component.ToString(Newtonsoft.Json.Formatting.None));

            RuntimeSaveStateResult<bool> result = new GameplayComponentWorldSaveStateProvider(CreateWorld(registerSchemas: true)).RestoreSaveState(saveState);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
            StringAssert.Contains("invalid value", result.Error.Message);
        }

        [Test]
        public void BuffPresentationSnapshotBuilder_UsesDefinitionsFallbackAndStablePriority()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = CreateEntity(world);
            world.GetOrCreateStore<GameplayComponentBuffSetComponent>().Set(
                entity,
                new GameplayComponentBuffSetComponent(
                    new GameplayComponentBuffEntry(StackBuffId, 3, 8, 13),
                    new GameplayComponentBuffEntry(BuffId, 2, 9, 12)));
            var presentations = new GameplayBuffPresentationDefinitionRegistry(
                new GameplayBuffPresentationDefinition(
                    BuffId,
                    "Guard Aura Defense",
                    "DEF+",
                    "Defense bonus.",
                    string.Empty,
                    GameplayBuffPresentationPolarity.Aura,
                    150,
                    showOnPlayerHud: true,
                    showOnTargetHud: true,
                    showOnHeadPad: true,
                    "HP(1) +5"));

            GameplayBuffPresentationItem[] items = GameplayBuffPresentationSnapshotBuilder.Build(
                world,
                entity,
                Definitions(),
                presentations,
                new RuntimeFrame(10),
                GameplayBuffPresentationSurface.PlayerHud);

            Assert.AreEqual(2, items.Length);
            Assert.AreEqual(BuffId, items[0].BuffId);
            Assert.AreEqual("DEF+", items[0].ShortName);
            Assert.AreEqual(2, items[0].StackCount);
            Assert.AreEqual(2, items[0].RemainingFrames);
            Assert.AreEqual(1f, items[0].Remaining01, 0.0001f);
            Assert.AreEqual(GameplayBuffPresentationPolarity.Aura, items[0].Polarity);
            Assert.AreEqual(StackBuffId, items[1].BuffId);
            Assert.AreEqual(GameplayBuffPresentationPolarity.Buff, items[1].Polarity);
            StringAssert.Contains("HP(1) +1", items[1].EffectSummary);

            GameplayBuffPresentationItem[] headPad = GameplayBuffPresentationSnapshotBuilder.Build(
                world,
                entity,
                Definitions(),
                presentations,
                new RuntimeFrame(12),
                GameplayBuffPresentationSurface.HeadPad,
                maxItems: 1);
            Assert.AreEqual(1, headPad.Length);
            Assert.AreEqual(BuffId, headPad[0].BuffId);
            Assert.AreEqual(0L, headPad[0].RemainingFrames);
        }

        [Test]
        public void BuffPresentationSnapshotBuilder_BuildIntoMatchesBuildAndClearsCallerBuffers()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = CreateEntity(world);
            world.GetOrCreateStore<GameplayComponentBuffSetComponent>().Set(
                entity,
                new GameplayComponentBuffSetComponent(
                    new GameplayComponentBuffEntry(StackBuffId, 3, 8, 13),
                    new GameplayComponentBuffEntry(BuffId, 2, 9, 12)));
            var presentations = new GameplayBuffPresentationDefinitionRegistry(
                new GameplayBuffPresentationDefinition(
                    BuffId,
                    "Guard Aura Defense",
                    "DEF+",
                    "Defense bonus.",
                    string.Empty,
                    GameplayBuffPresentationPolarity.Aura,
                    150,
                    showOnPlayerHud: true,
                    showOnTargetHud: true,
                    showOnHeadPad: true,
                    "HP(1) +5"));
            GameplayBuffPresentationItem[] expected = GameplayBuffPresentationSnapshotBuilder.Build(
                world,
                entity,
                Definitions(),
                presentations,
                new RuntimeFrame(12),
                GameplayBuffPresentationSurface.HeadPad,
                maxItems: 1);
            var output = new List<GameplayBuffPresentationItem>(4) { default };
            var scratch = new List<GameplayComponentBuffEntry>(4) { default };

            int count = GameplayBuffPresentationSnapshotBuilder.BuildInto(
                world,
                entity,
                Definitions(),
                presentations,
                new RuntimeFrame(12),
                GameplayBuffPresentationSurface.HeadPad,
                output,
                scratch,
                maxItems: 1);

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, output.Count);
            Assert.AreEqual(expected[0], output[0]);
            Assert.AreEqual(2, scratch.Count);

            count = GameplayBuffPresentationSnapshotBuilder.BuildInto(
                null,
                default,
                Definitions(),
                presentations,
                new RuntimeFrame(12),
                GameplayBuffPresentationSurface.HeadPad,
                output,
                scratch,
                maxItems: 1);

            Assert.AreEqual(0, count);
            Assert.AreEqual(0, output.Count);
            Assert.AreEqual(0, scratch.Count);
        }

        [Test]
        public void BuffPresentationDefinitionJsonImporter_ImportsGeneratedCatalog()
        {
            const string json = @"{
  ""schemaVersion"": ""1"",
  ""definitions"": [
    {
      ""buffId"": 601009,
      ""displayName"": ""CharacterTest On-Hit Defense Break"",
      ""shortName"": ""DEF-"",
      ""description"": ""Defense penalty."",
      ""iconResourceKey"": """",
      ""polarity"": ""Debuff"",
      ""priority"": 200,
      ""showOnPlayerHud"": false,
      ""showOnTargetHud"": true,
      ""showOnHeadPad"": true,
      ""effectSummary"": ""Defense(3) -3""
    }
  ]
}";

            GameplayBuffPresentationDefinitionRegistry registry =
                GameplayBuffPresentationDefinitionJsonImporter.Import(json);

            Assert.AreEqual(1, registry.Count);
            Assert.IsTrue(registry.TryGet(601009, out GameplayBuffPresentationDefinition definition));
            Assert.AreEqual("DEF-", definition.ShortName);
            Assert.AreEqual(string.Empty, definition.NameKey);
            Assert.AreEqual("Manual", definition.DescriptionMode);
            Assert.AreEqual("Defense penalty.", definition.Description);
            Assert.AreEqual(GameplayBuffPresentationPolarity.Debuff, definition.Polarity);
            Assert.IsFalse(definition.ShowOnPlayerHud);
            Assert.IsTrue(definition.ShowOnTargetHud);
            StringAssert.Contains("Defense(3) -3", definition.EffectSummary);

            const string v5Json = @"{
  ""schemaVersion"": ""1"",
  ""definitions"": [
    {
      ""buffId"": 601008,
      ""displayName"": ""CharacterTest Guard Aura Defense"",
      ""nameKey"": ""buff.character_test.guard_aura.defense.name"",
      ""shortName"": ""DEF+"",
      ""description"": ""防御+5，瞬时，不可叠加"",
      ""descriptionMode"": ""Auto"",
      ""iconResourceKey"": """",
      ""polarity"": ""Aura"",
      ""priority"": 150,
      ""showOnPlayerHud"": true,
      ""showOnTargetHud"": true,
      ""showOnHeadPad"": true,
      ""effectSummary"": ""Defense(3) +5""
    }
  ]
}";

            GameplayBuffPresentationDefinitionRegistry v5Registry =
                GameplayBuffPresentationDefinitionJsonImporter.Import(v5Json);
            Assert.IsTrue(v5Registry.TryGet(601008, out GameplayBuffPresentationDefinition v5Definition));
            Assert.AreEqual("buff.character_test.guard_aura.defense.name", v5Definition.NameKey);
            Assert.AreEqual("Auto", v5Definition.DescriptionMode);
            Assert.AreEqual("防御+5，瞬时，不可叠加", v5Definition.Description);
            Assert.Throws<ArgumentException>(() => new GameplayBuffPresentationDefinitionRegistry(
                definition,
                definition));
        }

        private static GameplayBuffDefinitionRegistry Definitions()
        {
            return new GameplayBuffDefinitionRegistry(
                Definition(
                    BuffId,
                    defaultDurationFrames: 2,
                    maxStackCount: 1,
                    policy: GameplayBuffStackPolicy.RefreshDuration,
                    addValue: 5,
                    status: StatusId),
                Definition(
                    StackBuffId,
                    defaultDurationFrames: 3,
                    maxStackCount: 3,
                    policy: GameplayBuffStackPolicy.Stack,
                    addValue: 1),
                Definition(
                    ReplaceBuffId,
                    defaultDurationFrames: 4,
                    maxStackCount: 1,
                    policy: GameplayBuffStackPolicy.Replace,
                    addValue: -2,
                    isPermanent: false));
        }

        private static GameplayBuffDefinition Definition(
            int buffId,
            long defaultDurationFrames = 2,
            int maxStackCount = 1,
            GameplayBuffStackPolicy policy = GameplayBuffStackPolicy.RefreshDuration,
            int addValue = 0,
            GameplayStatusId status = default,
            bool isPermanent = false)
        {
            var effects = new List<GameplayBuffEffectDefinition>();
            if (addValue != 0)
                effects.Add(GameplayBuffEffectDefinition.AttributeModifier(1, AttributeId, addValue));
            if (status.IsValid)
                effects.Add(GameplayBuffEffectDefinition.Status(2, status));

            return new GameplayBuffDefinition(
                buffId,
                "buff.test." + buffId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                defaultDurationFrames,
                maxStackCount,
                policy,
                effects.ToArray(),
                isPermanent: isPermanent);
        }

        private static GameplayEntityId CreateEntity(GameplayComponentWorld world)
        {
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayLifecycleComponent>().Set(entity, GameplayLifecycleComponent.Alive);
            return entity;
        }

        private static GameplayComponentWorld CreateWorld(bool registerSchemas)
        {
            var world = new GameplayComponentWorld();
            if (!registerSchemas)
                return world;

            GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
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

        private static GameplaySystemContext CreateContext(
            GameplayComponentWorld world,
            long frame,
            params RuntimeCommand[] commands)
        {
            return new GameplaySystemContext(
                new RuntimeFrame(frame),
                0d,
                0d,
                new GameplayWorld(),
                commands ?? Array.Empty<RuntimeCommand>(),
                world.Events,
                componentWorld: world);
        }

        private static void AssertBuff(
            GameplayComponentWorld world,
            GameplayEntityId entity,
            int buffId,
            int stack,
            long startFrame,
            long endFrame,
            int sourceId = 0)
        {
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore));
            Assert.IsTrue(buffStore.TryGet(entity, out GameplayComponentBuffSetComponent buffs));
            Assert.IsTrue(buffs.TryGet(buffId, out GameplayComponentBuffEntry buff));
            Assert.AreEqual(stack, buff.StackCount);
            Assert.AreEqual(startFrame, buff.StartFrame);
            Assert.AreEqual(endFrame, buff.EndFrame);
            Assert.AreEqual(sourceId, buff.SourceId);
        }

        private static void AssertNoBuff(GameplayComponentWorld world, GameplayEntityId entity, int buffId)
        {
            if (!world.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore))
                return;
            if (!buffStore.TryGet(entity, out GameplayComponentBuffSetComponent buffs))
                return;

            Assert.IsFalse(buffs.TryGet(buffId, out _));
        }

        private static long ComputeHash(GameplayComponentWorld world)
        {
            return new RuntimeHashCombiner(new IRuntimeHashContributor[]
                {
                    new GameplayBuffDefinitionRegistryHashContributor(Definitions()),
                    new GameplayComponentWorldHashContributor(world)
                }).ComputeHash(RuntimeFrame.Zero);
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

    internal static class FixtureIds_GameplayComponentBuffModifierTests
    {
        public const int Id_apply = 1;
        public const int Id_cmd = 2;
        public const int Id_refresh = 3;
        public const int Id_remove = 4;
        public const int Id_replace1 = 5;
        public const int Id_replace2 = 6;
        public const int Id_reserved = 7;
        public const int Id_stack1 = 8;
        public const int Id_stack2 = 9;
        public const int Id_stack3 = 10;
        public const int Id_stack4 = 11;
    }
}
