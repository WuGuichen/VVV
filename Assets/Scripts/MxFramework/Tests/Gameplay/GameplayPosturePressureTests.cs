using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay
{
    public sealed class GameplayPosturePressureTests
    {
        [Test]
        public void PosturePressureSystem_AppliesRequestAndEmitsBandChanged()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayPosturePressureComponent> store =
                world.GetOrCreateStore<GameplayPosturePressureComponent>();
            store.Set(entity, new GameplayPosturePressureComponent(100, recoveryRate: 5, recoveryDelayFrames: 2));
            var system = new GameplayPosturePressureSystem();
            var bandEvents = new List<PressureBandChangedEvent>();
            system.BandChangedEvents.Subscribe(evt => bandEvents.Add(evt));

            system.Enqueue(new GameplayPosturePressureRequest(entity, 30, "hit-1"));
            system.Tick(CreateContext(world, 5));

            Assert.IsTrue(store.TryGet(entity, out GameplayPosturePressureComponent updated));
            Assert.AreEqual(30, updated.CurrentPressure);
            Assert.AreEqual(PressureBand.Pressed, updated.CurrentBand);
            Assert.AreEqual(5L, updated.LastPressureFrame);
            Assert.IsFalse(updated.IsBroken);
            Assert.AreEqual(1, bandEvents.Count);
            Assert.AreEqual(entity, bandEvents[0].EntityId);
            Assert.AreEqual(PressureBand.Stable, bandEvents[0].OldBand);
            Assert.AreEqual(PressureBand.Pressed, bandEvents[0].NewBand);
            Assert.AreEqual(30, bandEvents[0].CurrentPressure);
            Assert.AreEqual("hit-1", bandEvents[0].TraceId);
        }

        [Test]
        public void PosturePressureSystem_EmitsBreakOnceAfterBandChanged()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayPosturePressureComponent> store =
                world.GetOrCreateStore<GameplayPosturePressureComponent>();
            store.Set(entity, new GameplayPosturePressureComponent(100));
            var system = new GameplayPosturePressureSystem();
            var breakEvents = new List<PostureBreakEvent>();
            var eventOrder = new List<string>();
            system.BandChangedEvents.Subscribe(_ => eventOrder.Add("band"));
            system.PostureBreakEvents.Subscribe(evt =>
            {
                eventOrder.Add("break");
                breakEvents.Add(evt);
            });

            system.Enqueue(new GameplayPosturePressureRequest(entity, 100, "break"));
            system.Tick(CreateContext(world, 0));
            system.Enqueue(new GameplayPosturePressureRequest(entity, 10, "repeat"));
            system.Tick(CreateContext(world, 1));

            Assert.IsTrue(store.TryGet(entity, out GameplayPosturePressureComponent updated));
            Assert.AreEqual(PressureBand.Broken, updated.CurrentBand);
            Assert.IsTrue(updated.IsBroken);
            CollectionAssert.AreEqual(new[] { "band", "break" }, eventOrder);
            Assert.AreEqual(1, breakEvents.Count);
            Assert.AreEqual(entity, breakEvents[0].EntityId);
            Assert.AreEqual(100, breakEvents[0].CurrentPressure);
            Assert.AreEqual(100, breakEvents[0].MaxPressure);
        }

        [Test]
        public void PosturePressureSystem_RecoversAfterDelayWithHysteresis()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayPosturePressureComponent> store =
                world.GetOrCreateStore<GameplayPosturePressureComponent>();
            store.Set(
                entity,
                new GameplayPosturePressureComponent
                {
                    MaxPressure = 100,
                    RecoveryRate = 10,
                    RecoveryDelayFrames = 1,
                    CurrentPressure = 80,
                    CurrentBand = PressureBand.Critical,
                    LastPressureFrame = 0L,
                    IsBroken = false
                });
            var system = new GameplayPosturePressureSystem();
            var bandEvents = new List<PressureBandChangedEvent>();
            system.BandChangedEvents.Subscribe(evt => bandEvents.Add(evt));

            system.Tick(CreateContext(world, 1));
            Assert.IsTrue(store.TryGet(entity, out GameplayPosturePressureComponent delayed));
            Assert.AreEqual(80, delayed.CurrentPressure);
            Assert.AreEqual(PressureBand.Critical, delayed.CurrentBand);

            system.Tick(CreateContext(world, 2));
            Assert.IsTrue(store.TryGet(entity, out GameplayPosturePressureComponent held));
            Assert.AreEqual(70, held.CurrentPressure);
            Assert.AreEqual(PressureBand.Critical, held.CurrentBand);

            system.Tick(CreateContext(world, 3));
            Assert.IsTrue(store.TryGet(entity, out GameplayPosturePressureComponent recovered));
            Assert.AreEqual(60, recovered.CurrentPressure);
            Assert.AreEqual(PressureBand.Cracked, recovered.CurrentBand);
            Assert.AreEqual(1, bandEvents.Count);
            Assert.AreEqual(PressureBand.Critical, bandEvents[0].OldBand);
            Assert.AreEqual(PressureBand.Cracked, bandEvents[0].NewBand);
            Assert.AreEqual(GameplayPosturePressureEvents.RecoveryReason, bandEvents[0].Reason);
        }

        [Test]
        public void PosturePressureSchema_HashChangesAndSaveStateRoundtrips()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayPosturePressureComponent> store =
                world.GetOrCreateStore<GameplayPosturePressureComponent>();
            var component = new GameplayPosturePressureComponent(100, recoveryRate: 3, recoveryDelayFrames: 1, currentPressure: 55, lastPressureFrame: 8);
            store.Set(entity, component);
            long before = ComputeHash(world);
            component.CurrentPressure = 65;
            store.Set(entity, component);
            long after = ComputeHash(world);

            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(world).CaptureSaveState().Value;
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);
            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(saveState);

            Assert.AreNotEqual(before, after);
            StringAssert.Contains(GameplayCoreComponentSchemaDescriptors.PosturePressureStableId, saveState.ModuleStates[0].CustomState.PayloadJson);
            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(after, ComputeHash(target));
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayPosturePressureComponent> restoredStore));
            Assert.IsTrue(restoredStore.TryGet(entity, out GameplayPosturePressureComponent restored));
            Assert.AreEqual(component, restored);
        }

        [Test]
        public void PosturePressureSchema_RejectsInvalidSavedBand()
        {
            RuntimeSaveState saveState = CreateSaveState(
                "{\"schemaVersion\":1,\"entities\":[{\"index\":1,\"generation\":1}],\"componentStores\":[{\"schemaId\":\"mxframework.gameplay.posture_pressure\",\"schemaVersion\":1,\"entries\":[{\"entityIndex\":1,\"entityGeneration\":1,\"payload\":{\"typeId\":\"mxframework.gameplay.posture_pressure\",\"schemaVersion\":1,\"payloadJson\":\"{\\\"maxPressure\\\":100,\\\"recoveryRate\\\":1,\\\"recoveryDelayFrames\\\":0,\\\"currentPressure\\\":50,\\\"currentBand\\\":999,\\\"lastPressureFrame\\\":0,\\\"isBroken\\\":false}\"}}]}]}");

            RuntimeSaveStateResult<bool> result = new GameplayComponentWorldSaveStateProvider(CreateWorld(registerSchemas: true)).RestoreSaveState(saveState);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
            StringAssert.Contains("band", result.Error.Message);
        }

        [Test]
        public void GameplayAssembly_StaysPureRuntimeBoundary()
        {
            string asmdef = System.IO.File.ReadAllText("Assets/Scripts/MxFramework/Gameplay/MxFramework.Gameplay.asmdef");

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
                            GameplayComponentWorldSaveStateProvider.ModuleId,
                            GameplayComponentWorldSaveState.CurrentSchemaVersion,
                            payloadJson))
                },
                null);
        }
    }
}
