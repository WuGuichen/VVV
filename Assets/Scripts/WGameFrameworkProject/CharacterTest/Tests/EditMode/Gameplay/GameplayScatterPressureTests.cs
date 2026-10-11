using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace MxFramework.Tests.Gameplay
{
    public sealed class GameplayScatterPressureTests
    {
        [Test]
        public void ScatterPressureSystem_AppliesRequestAndEmitsBandChanged()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayScatterPressureComponent> store =
                world.GetOrCreateStore<GameplayScatterPressureComponent>();
            store.Set(entity, new GameplayScatterPressureComponent(100, recoveryRate: 5, recoveryDelayFrames: 2));
            var system = new GameplayScatterPressureSystem();
            var bandEvents = new List<PressureBandChangedEvent>();
            system.BandChangedEvents.Subscribe(evt => bandEvents.Add(evt));

            system.Enqueue(new GameplayScatterPressureRequest(entity, 40, CharacterTestRuntimeTraceIds.Hit1));
            system.Tick(CreateContext(world, 5));

            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent updated));
            Assert.AreEqual(40, updated.CurrentScatterPressure);
            Assert.AreEqual(PressureBand.Pressed, updated.CurrentBand);
            Assert.AreEqual(5L, updated.LastPressureFrame);
            Assert.IsFalse(updated.IsBroken);
            Assert.AreEqual(1, bandEvents.Count);
            Assert.AreEqual(entity, bandEvents[0].EntityId);
            Assert.AreEqual(PressureBand.Stable, bandEvents[0].OldBand);
            Assert.AreEqual(PressureBand.Pressed, bandEvents[0].NewBand);
            Assert.AreEqual(40, bandEvents[0].CurrentPressure);
            Assert.AreEqual(100, bandEvents[0].CurrentLimit);
            Assert.AreEqual(CharacterTestRuntimeTraceIds.Hit1, bandEvents[0].TraceId);
        }

        [Test]
        public void ScatterPressureSystem_EmitsBreakOnceAfterBandChanged()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayScatterPressureComponent> store =
                world.GetOrCreateStore<GameplayScatterPressureComponent>();
            store.Set(entity, new GameplayScatterPressureComponent(100));
            var system = new GameplayScatterPressureSystem();
            var breakEvents = new List<CohesionBreakEvent>();
            var eventOrder = new List<string>();
            system.BandChangedEvents.Subscribe(_ => eventOrder.Add("band"));
            system.CohesionBreakEvents.Subscribe(evt =>
            {
                eventOrder.Add("break");
                breakEvents.Add(evt);
            });

            system.Enqueue(new GameplayScatterPressureRequest(entity, 100, CharacterTestRuntimeTraceIds.Break));
            system.Tick(CreateContext(world, 0));
            system.Enqueue(new GameplayScatterPressureRequest(entity, 10, CharacterTestRuntimeTraceIds.Repeat));
            system.Tick(CreateContext(world, 1));

            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent updated));
            Assert.AreEqual(PressureBand.Broken, updated.CurrentBand);
            Assert.IsTrue(updated.IsBroken);
            CollectionAssert.AreEqual(new[] { "band", "break" }, eventOrder);
            Assert.AreEqual(1, breakEvents.Count);
            Assert.AreEqual(entity, breakEvents[0].EntityId);
            Assert.AreEqual(100, breakEvents[0].CurrentScatterPressure);
            Assert.AreEqual(100, breakEvents[0].MaxScatterPressure);
            Assert.AreEqual(100, breakEvents[0].CurrentCohesionLimit);
        }

        [Test]
        public void ScatterPressureSystem_UsesCurrentCohesionLimitForBandAndBreak()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayScatterPressureComponent> store =
                world.GetOrCreateStore<GameplayScatterPressureComponent>();
            store.Set(entity, new GameplayScatterPressureComponent(
                100,
                currentCohesionLimit: 150,
                maxScatterDebt: 150));
            var system = new GameplayScatterPressureSystem();
            var impacts = new List<ScatterImpactResolvedEvent>();
            system.ScatterImpactResolvedEvents.Subscribe(evt => impacts.Add(evt));

            system.Enqueue(new GameplayScatterPressureRequest(entity, 120, CharacterTestRuntimeTraceIds.Hit1));
            system.Tick(CreateContext(world, 0));

            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent updated));
            Assert.AreEqual(120, updated.CurrentScatterPressure);
            Assert.AreEqual(150, updated.CurrentCohesionLimit);
            Assert.AreEqual(PressureBand.Cracked, updated.CurrentBand);
            Assert.IsFalse(updated.IsBroken);
            Assert.AreEqual(1, impacts.Count);
            Assert.IsTrue(impacts[0].WasSupported);
            Assert.IsFalse(impacts[0].TriggeredBreak);
        }

        [Test]
        public void ScatterPressureSystem_LoweringCohesionLimitCanTriggerBreak()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayScatterPressureComponent> store =
                world.GetOrCreateStore<GameplayScatterPressureComponent>();
            store.Set(entity, new GameplayScatterPressureComponent(
                100,
                currentScatterPressure: 120,
                currentCohesionLimit: 150,
                maxScatterDebt: 150));
            var system = new GameplayScatterPressureSystem();
            var breakEvents = new List<CohesionBreakEvent>();
            system.CohesionBreakEvents.Subscribe(evt => breakEvents.Add(evt));

            system.Enqueue(new GameplayCohesionLimitRequest(entity, 100, reason: new GameplayScatterPressureReason(1, 0)));
            system.Tick(CreateContext(world, 1));

            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent updated));
            Assert.AreEqual(120, updated.CurrentScatterPressure);
            Assert.AreEqual(100, updated.CurrentCohesionLimit);
            Assert.AreEqual(PressureBand.Broken, updated.CurrentBand);
            Assert.IsTrue(updated.IsBroken);
            Assert.AreEqual(1, breakEvents.Count);
            Assert.AreEqual(100, breakEvents[0].CurrentCohesionLimit);
        }

        [Test]
        public void ScatterPressureSystem_CohesionLimitRequestDoesNotAllocateAfterWarmup()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayScatterPressureComponent> store =
                world.GetOrCreateStore<GameplayScatterPressureComponent>();
            store.Set(entity, new GameplayScatterPressureComponent(
                100,
                currentScatterPressure: 80,
                currentCohesionLimit: 120,
                maxScatterDebt: 120));
            var system = new GameplayScatterPressureSystem();
            var context = new GameplaySystemContext(
                RuntimeFrame.Zero,
                0d,
                0d,
                new GameplayWorld(),
                Array.Empty<RuntimeCommand>(),
                world.Events,
                new GameplayCommandExecutionState(),
                world);
            var narrowCohesionLimit = new GameplayCohesionLimitRequest(entity, 90);
            var wideCohesionLimit = new GameplayCohesionLimitRequest(entity, 120);

            system.Enqueue(narrowCohesionLimit);
            system.Tick(context);
            system.Enqueue(wideCohesionLimit);
            system.Tick(context);
            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent warmed));
            Assert.AreEqual(PressureBand.Cracked, warmed.CurrentBand);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                system.Enqueue((i & 1) == 0 ? narrowCohesionLimit : wideCohesionLimit);
                system.Tick(context);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent updated));
            Assert.AreEqual(PressureBand.Cracked, updated.CurrentBand);
        }

        [Test]
        public void ScatterPressureSystem_RecoversAfterDelayWithHysteresis()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayScatterPressureComponent> store =
                world.GetOrCreateStore<GameplayScatterPressureComponent>();
            store.Set(
                entity,
                new GameplayScatterPressureComponent
                {
                    MaxScatterPressure = 100,
                    RecoveryRate = 10,
                    RecoveryDelayFrames = 1,
                    CurrentScatterPressure = 80,
                    CurrentCohesionLimit = 100,
                    MaxScatterDebt = 100,
                    CurrentBand = PressureBand.Critical,
                    LastPressureFrame = 0L,
                    IsBroken = false
                });
            var system = new GameplayScatterPressureSystem();
            var bandEvents = new List<PressureBandChangedEvent>();
            system.BandChangedEvents.Subscribe(evt => bandEvents.Add(evt));

            system.Tick(CreateContext(world, 1));
            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent delayed));
            Assert.AreEqual(80, delayed.CurrentScatterPressure);
            Assert.AreEqual(PressureBand.Critical, delayed.CurrentBand);

            system.Tick(CreateContext(world, 2));
            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent held));
            Assert.AreEqual(70, held.CurrentScatterPressure);
            Assert.AreEqual(PressureBand.Cracked, held.CurrentBand);

            system.Tick(CreateContext(world, 3));
            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent recovered));
            Assert.AreEqual(60, recovered.CurrentScatterPressure);
            Assert.AreEqual(PressureBand.Cracked, recovered.CurrentBand);
            Assert.AreEqual(1, bandEvents.Count);
            Assert.AreEqual(PressureBand.Critical, bandEvents[0].OldBand);
            Assert.AreEqual(PressureBand.Cracked, bandEvents[0].NewBand);
            Assert.AreEqual(GameplayScatterPressureEvents.RecoveryReason, bandEvents[0].Reason);
        }

        [Test]
        public void ScatterPressureSystem_TimedBrokenRecoveryIgnoresPressureAndClearsAtDelay()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayScatterPressureComponent> store =
                world.GetOrCreateStore<GameplayScatterPressureComponent>();
            store.Set(entity, new GameplayScatterPressureComponent(
                100,
                recoveryRate: 2,
                recoveryDelayFrames: 360,
                brokenRecoveryDelayFrames: 270));
            var system = new GameplayScatterPressureSystem();
            var pressureEvents = new List<ScatterPressureChangedEvent>();
            var impactEvents = new List<ScatterImpactResolvedEvent>();
            var bandEvents = new List<PressureBandChangedEvent>();
            var breakEvents = new List<CohesionBreakEvent>();
            system.PressureChangedEvents.Subscribe(evt => pressureEvents.Add(evt));
            system.ScatterImpactResolvedEvents.Subscribe(evt => impactEvents.Add(evt));
            system.BandChangedEvents.Subscribe(evt => bandEvents.Add(evt));
            system.CohesionBreakEvents.Subscribe(evt => breakEvents.Add(evt));

            system.Enqueue(new GameplayScatterPressureRequest(entity, 100, 7, new GameplayScatterPressureReason(2, 0), CharacterTestRuntimeTraceIds.Break));
            system.Tick(CreateContext(world, 10));

            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent broken));
            Assert.AreEqual(100, broken.ScatterDebt);
            Assert.AreEqual(PressureBand.Broken, broken.CurrentBand);
            Assert.IsTrue(broken.IsBroken);
            Assert.AreEqual(10L, broken.LastPressureFrame);
            Assert.AreEqual(10L, broken.LastCohesionBreakFrame);
            Assert.AreEqual(1, impactEvents.Count);
            Assert.AreEqual(1, breakEvents.Count);

            pressureEvents.Clear();
            impactEvents.Clear();
            bandEvents.Clear();
            breakEvents.Clear();

            system.Enqueue(new GameplayScatterPressureRequest(entity, 12, 7, new GameplayScatterPressureReason(3, 0), CharacterTestRuntimeTraceIds.Repeat));
            system.Tick(CreateContext(world, 20));

            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent ignored));
            Assert.AreEqual(100, ignored.ScatterDebt);
            Assert.AreEqual(PressureBand.Broken, ignored.CurrentBand);
            Assert.AreEqual(10L, ignored.LastPressureFrame);
            Assert.AreEqual(10L, ignored.LastCohesionBreakFrame);
            Assert.AreEqual(0, pressureEvents.Count);
            Assert.AreEqual(0, impactEvents.Count);
            Assert.AreEqual(0, bandEvents.Count);
            Assert.AreEqual(0, breakEvents.Count);

            system.Tick(CreateContext(world, 279));

            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent held));
            Assert.AreEqual(100, held.ScatterDebt);
            Assert.AreEqual(PressureBand.Broken, held.CurrentBand);
            Assert.IsTrue(held.IsBroken);

            system.Tick(CreateContext(world, 280));

            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent recovered));
            Assert.AreEqual(0, recovered.ScatterDebt);
            Assert.AreEqual(PressureBand.Stable, recovered.CurrentBand);
            Assert.IsFalse(recovered.IsBroken);
            Assert.AreEqual(1, pressureEvents.Count);
            Assert.AreEqual(-100, pressureEvents[0].Delta);
            Assert.AreEqual(GameplayScatterPressureEvents.RecoveryReason, pressureEvents[0].Reason);
            Assert.AreEqual(1, bandEvents.Count);
            Assert.AreEqual(PressureBand.Broken, bandEvents[0].OldBand);
            Assert.AreEqual(PressureBand.Stable, bandEvents[0].NewBand);
            Assert.AreEqual(0, impactEvents.Count);
            Assert.AreEqual(0, breakEvents.Count);
        }

        [Test]
        public void ScatterPressureSystem_TimedBrokenRecoveryKeepsBrokenAcrossCohesionLimitChanges()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayScatterPressureComponent> store =
                world.GetOrCreateStore<GameplayScatterPressureComponent>();
            store.Set(entity, new GameplayScatterPressureComponent(
                100,
                recoveryRate: 2,
                recoveryDelayFrames: 360,
                brokenRecoveryDelayFrames: 270));
            var system = new GameplayScatterPressureSystem();
            var bandEvents = new List<PressureBandChangedEvent>();
            var breakEvents = new List<CohesionBreakEvent>();
            system.BandChangedEvents.Subscribe(evt => bandEvents.Add(evt));
            system.CohesionBreakEvents.Subscribe(evt => breakEvents.Add(evt));

            system.Enqueue(new GameplayScatterPressureRequest(entity, 100, traceId: CharacterTestRuntimeTraceIds.Break));
            system.Tick(CreateContext(world, 5));
            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent broken));
            Assert.AreEqual(PressureBand.Broken, broken.CurrentBand);
            Assert.AreEqual(5L, broken.LastCohesionBreakFrame);

            bandEvents.Clear();
            breakEvents.Clear();

            system.Enqueue(new GameplayCohesionLimitRequest(entity, 150, reason: new GameplayScatterPressureReason(4, 0)));
            system.Tick(CreateContext(world, 6));

            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent supported));
            Assert.AreEqual(100, supported.ScatterDebt);
            Assert.AreEqual(150, supported.CurrentCohesionLimit);
            Assert.AreEqual(PressureBand.Broken, supported.CurrentBand);
            Assert.IsTrue(supported.IsBroken);
            Assert.AreEqual(5L, supported.LastCohesionBreakFrame);
            Assert.AreEqual(0, bandEvents.Count);
            Assert.AreEqual(0, breakEvents.Count);

            system.Enqueue(new GameplayCohesionLimitRequest(entity, 80, reason: new GameplayScatterPressureReason(5, 0)));
            system.Tick(CreateContext(world, 7));

            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent lowered));
            Assert.AreEqual(100, lowered.ScatterDebt);
            Assert.AreEqual(80, lowered.CurrentCohesionLimit);
            Assert.AreEqual(PressureBand.Broken, lowered.CurrentBand);
            Assert.IsTrue(lowered.IsBroken);
            Assert.AreEqual(5L, lowered.LastCohesionBreakFrame);
            Assert.AreEqual(0, bandEvents.Count);
            Assert.AreEqual(0, breakEvents.Count);
        }

        [Test]
        public void ScatterPressureSystem_DefaultBrokenRecoveryUsesGradualCompatibility()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayScatterPressureComponent> store =
                world.GetOrCreateStore<GameplayScatterPressureComponent>();
            store.Set(entity, new GameplayScatterPressureComponent(
                100,
                recoveryRate: 2,
                recoveryDelayFrames: 1,
                currentScatterPressure: 100,
                lastPressureFrame: 0));
            var system = new GameplayScatterPressureSystem();

            system.Tick(CreateContext(world, 1));
            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent delayed));
            Assert.AreEqual(100, delayed.ScatterDebt);
            Assert.AreEqual(PressureBand.Broken, delayed.CurrentBand);

            system.Tick(CreateContext(world, 2));

            Assert.IsTrue(store.TryGet(entity, out GameplayScatterPressureComponent recovered));
            Assert.AreEqual(98, recovered.ScatterDebt);
            Assert.AreEqual(PressureBand.Broken, recovered.CurrentBand);
            Assert.IsTrue(recovered.IsBroken);
            Assert.AreEqual(0, recovered.BrokenRecoveryDelayFrames);
        }

        [Test]
        public void ScatterPressureSchema_HashChangesAndSaveStateRoundtrips()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayScatterPressureComponent> store =
                world.GetOrCreateStore<GameplayScatterPressureComponent>();
            var component = new GameplayScatterPressureComponent(100, recoveryRate: 3, recoveryDelayFrames: 1, currentScatterPressure: 55, lastPressureFrame: 8);
            store.Set(entity, component);
            long before = ComputeHash(world);
            component.CurrentScatterPressure = 65;
            store.Set(entity, component);
            long after = ComputeHash(world);

            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(world).CaptureSaveState().Value;
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);
            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(saveState);

            Assert.AreNotEqual(before, after);
            StringAssert.Contains(GameplayCoreComponentSchemaDescriptors.ScatterPressureStableId, saveState.ModuleStates[0].CustomState.PayloadJson);
            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(after, ComputeHash(target));
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayScatterPressureComponent> restoredStore));
            Assert.IsTrue(restoredStore.TryGet(entity, out GameplayScatterPressureComponent restored));
            Assert.AreEqual(component, restored);
        }

        [Test]
        public void ScatterPressureSchema_RejectsInvalidSavedBand()
        {
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = target.CreateEntity();
            target.GetOrCreateStore<GameplayScatterPressureComponent>().Set(entity,
                new GameplayScatterPressureComponent(100, recoveryRate: 1, currentScatterPressure: 50));
            var provider = new GameplayComponentWorldSaveStateProvider(target);
            var document = JObject.Parse(provider.CaptureSaveState().Value.ModuleStates[0].CustomState.PayloadJson);
            JToken custom = document["componentStores"][0]["entries"][0]["payload"];
            var payload = JObject.Parse((string)custom["payloadJson"]);
            payload["currentBand"] = 999;
            custom["payloadJson"] = payload.ToString();
            long before = ComputeHash(target);
            RuntimeSaveStateResult<bool> result = provider.RestoreSaveState(CreateSaveState(document.ToString()));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
            StringAssert.Contains("band", result.Error.Message);
            Assert.AreEqual(before, ComputeHash(target));
        }

        [Test]
        public void GameplayAssembly_StaysPureRuntimeBoundary()
        {
            string asmdef = System.IO.File.ReadAllText("Packages/com.vincent.wgameframework/Gameplay/MxFramework.Gameplay.asmdef");

            StringAssert.Contains("\"noEngineReferences\": true", asmdef);
            StringAssert.DoesNotContain("UnityEngine", asmdef);
            StringAssert.DoesNotContain("MxFramework.Combat", asmdef);
            StringAssert.DoesNotContain("MxFramework.AI", asmdef);
            StringAssert.DoesNotContain("MxFramework.UI", asmdef);
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

        private static GameplaySystemContext CreateContext(GameplayComponentWorld world, long frame)
        {
            return new GameplaySystemContext(
                new RuntimeFrame(frame),
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
                            GameplayComponentWorldSaveStateProvider.NumericModuleTypeId,
                            GameplayComponentWorldSaveStateProvider.ModuleId,
                            GameplayComponentWorldSaveState.CurrentSchemaVersion,
                            payloadJson))
                },
                null);
        }
    }
}
