using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay
{
    public sealed class GameplayGuardArmorTests
    {
        private static readonly GameplayStatusId GuardBrokenStatus = new GameplayStatusId(9001);
        private static readonly GameplayStatusId BlockingStatus = new GameplayStatusId(100);

        [Test]
        public void GuardPressureSystem_AppliesRequestAndEmitsBandChanged()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayGuardPressureComponent> store =
                world.GetOrCreateStore<GameplayGuardPressureComponent>();
            store.Set(entity, new GameplayGuardPressureComponent(100, recoveryRate: 5, recoveryDelayFrames: 2));
            var system = new GameplayGuardPressureSystem();
            var bandEvents = new List<PressureBandChangedEvent>();
            system.BandChangedEvents.Subscribe(evt => bandEvents.Add(evt));

            system.Enqueue(new GameplayGuardPressureRequest(entity, 30, isBlocking: true, traceId: "block-1"));
            system.Tick(CreateContext(world, 5));

            Assert.IsTrue(store.TryGet(entity, out GameplayGuardPressureComponent updated));
            Assert.AreEqual(30, updated.CurrentPressure);
            Assert.AreEqual(PressureBand.Pressed, updated.CurrentBand);
            Assert.AreEqual(5L, updated.LastPressureFrame);
            Assert.IsFalse(updated.IsBroken);
            Assert.AreEqual(1, bandEvents.Count);
            Assert.AreEqual(entity, bandEvents[0].EntityId);
            Assert.AreEqual(PressureBand.Stable, bandEvents[0].OldBand);
            Assert.AreEqual(PressureBand.Pressed, bandEvents[0].NewBand);
            Assert.AreEqual("block-1", bandEvents[0].TraceId);
        }

        [Test]
        public void GuardPressureSystem_EmitsBreakOnceAndWritesGuardBrokenStatus()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayGuardPressureComponent>().Set(entity, new GameplayGuardPressureComponent(100));
            world.GetOrCreateStore<GameplayStatusComponent>().Set(entity, new GameplayStatusComponent(BlockingStatus));
            var system = new GameplayGuardPressureSystem(GuardBrokenStatus);
            var breakEvents = new List<GuardBreakEvent>();
            var eventOrder = new List<string>();
            system.BandChangedEvents.Subscribe(_ => eventOrder.Add("band"));
            system.GuardBreakEvents.Subscribe(evt =>
            {
                eventOrder.Add("break");
                breakEvents.Add(evt);
            });

            system.Enqueue(new GameplayGuardPressureRequest(entity, 100, isBlocking: true, traceId: "guard-break"));
            system.Tick(CreateContext(world, 0));
            system.Enqueue(new GameplayGuardPressureRequest(entity, 10, isBlocking: true, traceId: "repeat"));
            system.Tick(CreateContext(world, 1));

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayGuardPressureComponent> store));
            Assert.IsTrue(store.TryGet(entity, out GameplayGuardPressureComponent updated));
            Assert.AreEqual(PressureBand.Broken, updated.CurrentBand);
            Assert.IsTrue(updated.IsBroken);
            CollectionAssert.AreEqual(new[] { "band", "break" }, eventOrder);
            Assert.AreEqual(1, breakEvents.Count);
            Assert.AreEqual(entity, breakEvents[0].EntityId);
            Assert.AreEqual(100, breakEvents[0].CurrentPressure);
            Assert.AreEqual("guard-break", breakEvents[0].TraceId);

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayStatusComponent> statusStore));
            Assert.IsTrue(statusStore.TryGet(entity, out GameplayStatusComponent statuses));
            Assert.IsTrue(statuses.Contains(BlockingStatus));
            Assert.IsTrue(statuses.Contains(GuardBrokenStatus));
        }

        [Test]
        public void GuardPressureSystem_RecoversAfterBlockingDelay()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayGuardPressureComponent> store =
                world.GetOrCreateStore<GameplayGuardPressureComponent>();
            store.Set(
                entity,
                new GameplayGuardPressureComponent
                {
                    MaxPressure = 100,
                    RecoveryRate = 10,
                    RecoveryDelayFrames = 1,
                    CurrentPressure = 50,
                    CurrentBand = PressureBand.Cracked,
                    LastPressureFrame = 0L,
                    IsBroken = false
                });
            var system = new GameplayGuardPressureSystem();

            system.Enqueue(new GameplayGuardPressureRequest(entity, 0, isBlocking: true, traceId: "hold"));
            system.Tick(CreateContext(world, 1));
            Assert.IsTrue(store.TryGet(entity, out GameplayGuardPressureComponent held));
            Assert.AreEqual(50, held.CurrentPressure);
            Assert.AreEqual(1L, held.LastPressureFrame);

            system.Tick(CreateContext(world, 2));
            Assert.IsTrue(store.TryGet(entity, out GameplayGuardPressureComponent delayed));
            Assert.AreEqual(50, delayed.CurrentPressure);

            system.Tick(CreateContext(world, 3));
            Assert.IsTrue(store.TryGet(entity, out GameplayGuardPressureComponent recovered));
            Assert.AreEqual(40, recovered.CurrentPressure);
            Assert.AreEqual(PressureBand.Pressed, recovered.CurrentBand);
        }

        [Test]
        public void ArmorIntegrityComponent_ValidatesAndDoesNotRecover()
        {
            var armor = new GameplayArmorIntegrityComponent(100, currentIntegrity: 25);

            GameplayArmorIntegrityComponent updated = armor.ApplyDamage(10, out bool broke);

            Assert.IsFalse(broke);
            Assert.AreEqual(15, updated.CurrentIntegrity);
            Assert.IsFalse(updated.IsBroken);
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplayArmorIntegrityComponent(0));
            Assert.IsFalse(new GameplayArmorIntegrityComponent
            {
                MaxIntegrity = 100,
                CurrentIntegrity = 0,
                IsBroken = false
            }.HasValidState());
        }

        [Test]
        public void ArmorIntegrityComponent_ApplyDamageBreaksOnceAtZero()
        {
            var armor = new GameplayArmorIntegrityComponent(100, currentIntegrity: 10);

            GameplayArmorIntegrityComponent broken = armor.ApplyDamage(20, out bool broke);
            GameplayArmorIntegrityComponent repeated = broken.ApplyDamage(20, out bool repeatedBreak);

            Assert.IsTrue(broke);
            Assert.AreEqual(0, broken.CurrentIntegrity);
            Assert.IsTrue(broken.IsBroken);
            Assert.IsFalse(repeatedBreak);
            Assert.AreEqual(broken, repeated);
        }

        [Test]
        public void GuardArmorSchemas_HashChangesAndSaveStateRoundtrips()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: true);
            GameplayEntityId entity = world.CreateEntity();
            GameplayComponentStore<GameplayGuardPressureComponent> guardStore =
                world.GetOrCreateStore<GameplayGuardPressureComponent>();
            GameplayComponentStore<GameplayArmorIntegrityComponent> armorStore =
                world.GetOrCreateStore<GameplayArmorIntegrityComponent>();
            var guard = new GameplayGuardPressureComponent(100, recoveryRate: 3, recoveryDelayFrames: 1, currentPressure: 55, lastPressureFrame: 8);
            var armor = new GameplayArmorIntegrityComponent(120, currentIntegrity: 70);
            guardStore.Set(entity, guard);
            armorStore.Set(entity, armor);
            long before = ComputeHash(world);
            armor.CurrentIntegrity = 60;
            armorStore.Set(entity, armor);
            long after = ComputeHash(world);

            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(world).CaptureSaveState().Value;
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);
            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(saveState);

            Assert.AreNotEqual(before, after);
            StringAssert.Contains(GameplayCoreComponentSchemaDescriptors.GuardPressureStableId, saveState.ModuleStates[0].CustomState.PayloadJson);
            StringAssert.Contains(GameplayCoreComponentSchemaDescriptors.ArmorIntegrityStableId, saveState.ModuleStates[0].CustomState.PayloadJson);
            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(after, ComputeHash(target));
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayGuardPressureComponent> restoredGuardStore));
            Assert.IsTrue(restoredGuardStore.TryGet(entity, out GameplayGuardPressureComponent restoredGuard));
            Assert.AreEqual(guard, restoredGuard);
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayArmorIntegrityComponent> restoredArmorStore));
            Assert.IsTrue(restoredArmorStore.TryGet(entity, out GameplayArmorIntegrityComponent restoredArmor));
            Assert.AreEqual(armor, restoredArmor);
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
    }
}
