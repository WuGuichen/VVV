using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.GameplayBridge
{
    public sealed class CombatGuardArmorApplicationSystemTests
    {
        private const int HpAttributeId = 100;

        [Test]
        public void Tick_BlockedResultEnqueuesGuardPressure()
        {
            var outputCommands = new List<RuntimeCommand>();
            var world = new GameplayComponentWorld();
            GameplayEntityId entityId = world.CreateEntity();
            world.GetOrCreateStore<GameplayGuardPressureComponent>().Set(
                entityId,
                new GameplayGuardPressureComponent
                {
                    MaxPressure = 100,
                    RecoveryRate = 0,
                    RecoveryDelayFrames = 0,
                    CurrentPressure = 90,
                    CurrentBand = PressureBand.Critical,
                    LastPressureFrame = 0L,
                    IsBroken = false
                });
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), entityId);
            var guardSystem = new GameplayGuardPressureSystem();
            var guardBreakEvents = new List<GuardBreakEvent>();
            guardSystem.GuardBreakEvents.Subscribe(evt => guardBreakEvents.Add(evt));
            var system = new CombatGuardArmorApplicationSystem(
                map,
                world,
                guardSystem,
                () => new[] { Result(HitResolveKind.Blocked, targetId: 20, damage: 15, traceId: 901) },
                HpAttributeId,
                outputCommands);

            system.Tick(CreateContext(world, frame: 12));
            guardSystem.Tick(CreateContext(world, frame: 12));

            Assert.AreEqual(0, outputCommands.Count);
            Assert.AreEqual(1, guardBreakEvents.Count);
            Assert.AreEqual(entityId, guardBreakEvents[0].EntityId);
        }

        [Test]
        public void Pipeline_DefaultPriorityAppliesGuardPressureSameFrame()
        {
            var outputCommands = new List<RuntimeCommand>();
            var world = new GameplayComponentWorld();
            GameplayEntityId entityId = world.CreateEntity();
            world.GetOrCreateStore<GameplayGuardPressureComponent>().Set(
                entityId,
                new GameplayGuardPressureComponent
                {
                    MaxPressure = 100,
                    RecoveryRate = 0,
                    RecoveryDelayFrames = 0,
                    CurrentPressure = 90,
                    CurrentBand = PressureBand.Critical,
                    LastPressureFrame = 0L,
                    IsBroken = false
                });
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), entityId);
            var guardSystem = new GameplayGuardPressureSystem();
            var guardBreakEvents = new List<GuardBreakEvent>();
            guardSystem.GuardBreakEvents.Subscribe(evt => guardBreakEvents.Add(evt));
            var system = new CombatGuardArmorApplicationSystem(
                map,
                world,
                guardSystem,
                () => new[] { Result(HitResolveKind.Blocked, targetId: 20, damage: 15, traceId: 901) },
                HpAttributeId,
                outputCommands);
            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(guardSystem);
            pipeline.Add(system);

            pipeline.Tick(CreateContext(world, frame: 12));

            Assert.AreEqual(0, outputCommands.Count);
            Assert.AreEqual(0, guardSystem.PendingRequestCount);
            Assert.AreEqual(1, guardBreakEvents.Count);
            Assert.AreEqual(entityId, guardBreakEvents[0].EntityId);
        }

        [Test]
        public void Tick_DamageResultAbsorbsByCurrentArmorThenDecaysIntegrity()
        {
            var outputCommands = new List<RuntimeCommand>();
            var world = new GameplayComponentWorld();
            GameplayEntityId entityId = world.CreateEntity();
            GameplayComponentStore<GameplayArmorIntegrityComponent> armorStore =
                world.GetOrCreateStore<GameplayArmorIntegrityComponent>();
            armorStore.Set(entityId, new GameplayArmorIntegrityComponent
            {
                MaxIntegrity = 100,
                CurrentIntegrity = 80,
                IsBroken = false
            });
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), entityId);
            var system = new CombatGuardArmorApplicationSystem(
                map,
                world,
                guardPressureSystem: null,
                getHitResults: () => new[] { Result(HitResolveKind.Damage, targetId: 20, damage: 25, traceId: 702) },
                hpAttributeId: HpAttributeId,
                outputCommands: outputCommands);

            system.Tick(CreateContext(world, frame: 7));

            Assert.IsTrue(armorStore.TryGet(entityId, out GameplayArmorIntegrityComponent armor));
            Assert.AreEqual(55, armor.CurrentIntegrity);
            Assert.IsFalse(armor.IsBroken);
            Assert.AreEqual(1, outputCommands.Count);
            AssertCommand(outputCommands[0], new RuntimeFrame(7), entityId, -5, "702");
        }

        [Test]
        public void Tick_DamageResultPublishesArmorBreakAndUsesPreBreakAbsorbRatio()
        {
            var outputCommands = new List<RuntimeCommand>();
            var armorBreakEvents = new List<ArmorBreakEvent>();
            var world = new GameplayComponentWorld();
            GameplayEntityId entityId = world.CreateEntity();
            GameplayComponentStore<GameplayArmorIntegrityComponent> armorStore =
                world.GetOrCreateStore<GameplayArmorIntegrityComponent>();
            armorStore.Set(entityId, new GameplayArmorIntegrityComponent
            {
                MaxIntegrity = 100,
                CurrentIntegrity = 10,
                IsBroken = false
            });
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), entityId);
            var system = new CombatGuardArmorApplicationSystem(
                map,
                world,
                guardPressureSystem: null,
                getHitResults: () => new[] { Result(HitResolveKind.Damage, targetId: 20, damage: 25, traceId: 703) },
                hpAttributeId: HpAttributeId,
                outputCommands: outputCommands,
                publishArmorBreakEvent: evt => armorBreakEvents.Add(evt));

            system.Tick(CreateContext(world, frame: 8));

            Assert.IsTrue(armorStore.TryGet(entityId, out GameplayArmorIntegrityComponent armor));
            Assert.AreEqual(0, armor.CurrentIntegrity);
            Assert.IsTrue(armor.IsBroken);
            Assert.AreEqual(1, armorBreakEvents.Count);
            Assert.AreEqual(entityId, armorBreakEvents[0].EntityId);
            Assert.AreEqual(new RuntimeFrame(8), armorBreakEvents[0].Frame);
            Assert.AreEqual(10, armorBreakEvents[0].PreviousIntegrity);
            Assert.AreEqual(0, armorBreakEvents[0].CurrentIntegrity);
            Assert.AreEqual(100, armorBreakEvents[0].MaxIntegrity);
            Assert.AreEqual(25, armorBreakEvents[0].IncomingDamage);
            Assert.AreEqual("703", armorBreakEvents[0].TraceId);
            Assert.AreEqual(1, outputCommands.Count);
            AssertCommand(outputCommands[0], new RuntimeFrame(8), entityId, -23, "703");
        }

        [Test]
        public void Tick_MissingArmorAppliesFullDamage()
        {
            var outputCommands = new List<RuntimeCommand>();
            var world = new GameplayComponentWorld();
            GameplayEntityId entityId = world.CreateEntity();
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), entityId);
            var system = new CombatGuardArmorApplicationSystem(
                map,
                world,
                guardPressureSystem: null,
                getHitResults: () => new[] { Result(HitResolveKind.Damage, targetId: 20, damage: 9, traceId: 704) },
                hpAttributeId: HpAttributeId,
                outputCommands: outputCommands);

            system.Tick(CreateContext(world, frame: 9));

            Assert.AreEqual(1, outputCommands.Count);
            AssertCommand(outputCommands[0], new RuntimeFrame(9), entityId, -9, "704");
        }

        [Test]
        public void Tick_MissingMapSkipsWithoutHpOrGuardPressure()
        {
            var outputCommands = new List<RuntimeCommand>();
            var world = new GameplayComponentWorld();
            var system = new CombatGuardArmorApplicationSystem(
                new CombatEntityGameplayMap(),
                world,
                new GameplayGuardPressureSystem(),
                () => new[]
                {
                    Result(HitResolveKind.Blocked, targetId: 20, damage: 15, traceId: 801),
                    Result(HitResolveKind.Damage, targetId: 30, damage: 9, traceId: 802),
                },
                HpAttributeId,
                outputCommands);

            Assert.DoesNotThrow(() => system.Tick(CreateContext(world)));
            Assert.AreEqual(0, outputCommands.Count);
        }

        [Test]
        public void SetEnabled_DisabledTickDoesNotReadResults()
        {
            var outputCommands = new List<RuntimeCommand>();
            var world = new GameplayComponentWorld();
            int readCount = 0;
            var system = new CombatGuardArmorApplicationSystem(
                new CombatEntityGameplayMap(),
                world,
                guardPressureSystem: null,
                getHitResults: () =>
                {
                    readCount++;
                    return new[] { Result(HitResolveKind.Damage, targetId: 20, damage: 1) };
                },
                hpAttributeId: HpAttributeId,
                outputCommands: outputCommands);

            system.SetEnabled(false);
            system.Tick(CreateContext(world));

            Assert.IsFalse(system.IsEnabled);
            Assert.AreEqual(0, readCount);
            Assert.AreEqual(0, outputCommands.Count);
        }

        [Test]
        public void Constructor_StoresMetadataDefaults()
        {
            var system = new CombatGuardArmorApplicationSystem(
                new CombatEntityGameplayMap(),
                new GameplayComponentWorld(),
                guardPressureSystem: null,
                getHitResults: () => Array.Empty<HitResolveResult>(),
                hpAttributeId: HpAttributeId,
                outputCommands: new List<RuntimeCommand>());

            Assert.AreEqual("mxframework.bridge.combat.guard_armor_application", CombatGuardArmorApplicationSystem.DefaultSystemId);
            Assert.AreEqual(CombatGuardArmorApplicationSystem.DefaultSystemId, system.SystemId);
            Assert.AreEqual(GameplaySystemPhase.Resolution, system.Phase);
            Assert.AreEqual(60, system.Priority);
            Assert.IsTrue(system.IsEnabled);
        }

        private static void AssertCommand(
            RuntimeCommand command,
            RuntimeFrame frame,
            GameplayEntityId entityId,
            int delta,
            string traceId)
        {
            Assert.AreEqual(frame, command.Frame);
            Assert.AreEqual(0, command.SourceId);
            Assert.AreEqual(GameplayRuntimeCommandIds.AddComponentAttribute, command.CommandId);
            Assert.AreEqual(entityId.Index, command.TargetId);
            Assert.AreEqual(entityId.Generation, command.Payload0);
            Assert.AreEqual(HpAttributeId, command.Payload1);
            Assert.AreEqual(delta, command.Payload2);
            Assert.AreEqual(traceId, command.TraceId);
        }

        private static GameplaySystemContext CreateContext(GameplayComponentWorld world, long frame = 0)
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

        private static HitResolveResult Result(HitResolveKind kind, int targetId, int damage, int traceId = 7)
        {
            return new HitResolveResult(
                Combat(1),
                Combat(targetId),
                actionId: 1001,
                actionInstanceId: 2001,
                traceId: traceId,
                frame: new CombatFrame(10),
                kind,
                damage,
                staggerFrames: kind == HitResolveKind.Damage ? 3 : 0,
                knockback: FixVector3.Zero);
        }

        private static CombatEntityId Combat(int value)
        {
            return new CombatEntityId(value);
        }
    }
}
