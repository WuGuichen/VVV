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
    public sealed class CombatHitApplicationSystemTests
    {
        private const int HpAttributeId = 100;

        [Test]
        public void Tick_AcceptedDamageAppendsAddComponentAttributeCommand()
        {
            var outputCommands = new List<RuntimeCommand>();
            var world = new GameplayComponentWorld();
            var map = new CombatEntityGameplayMap();
            GameplayEntityId entityId = world.CreateEntity();
            map.Register(Combat(20), entityId);
            var system = new CombatHitApplicationSystem(
                map,
                world,
                () => new[] { Result(HitResolveKind.Damage, targetId: 20, damage: 12, traceId: 701) },
                HpAttributeId,
                outputCommands);

            system.Tick(CreateContext(world, frame: 7));

            Assert.AreEqual(1, outputCommands.Count);
            RuntimeCommand command = outputCommands[0];
            Assert.AreEqual(new RuntimeFrame(7), command.Frame);
            Assert.AreEqual(0, command.SourceId);
            Assert.AreEqual(GameplayRuntimeCommandIds.AddComponentAttribute, command.CommandId);
            Assert.AreEqual(entityId.Index, command.TargetId);
            Assert.AreEqual(entityId.Generation, command.Payload0);
            Assert.AreEqual(HpAttributeId, command.Payload1);
            Assert.AreEqual(-12, command.Payload2);
            Assert.AreEqual("701", command.TraceId);
        }

        [Test]
        public void Tick_AcceptedDamageCommandCanBeConsumedByAttributeCommandSystem()
        {
            var outputCommands = new List<RuntimeCommand>();
            var world = new GameplayComponentWorld();
            var map = new CombatEntityGameplayMap();
            GameplayEntityId entityId = world.CreateEntity();
            map.Register(Combat(20), entityId);
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entityId,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(HpAttributeId, 100, 100)));
            var hitApplication = new CombatHitApplicationSystem(
                map,
                world,
                () => new[] { Result(HitResolveKind.Damage, targetId: 20, damage: 12, traceId: 701) },
                HpAttributeId,
                outputCommands);
            var attributeSystem = new GameplayAttributeCommandSystem();

            hitApplication.Tick(CreateContext(world, frame: 7));
            attributeSystem.Tick(CreateContext(world, outputCommands, frame: 7));

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store));
            Assert.IsTrue(store.TryGet(entityId, out GameplayAttributeSetComponent attributes));
            Assert.AreEqual(88, attributes.GetCurrentValueOrDefault(HpAttributeId));
            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, world.DrainEvents(new RuntimeFrame(7), events));
            Assert.AreEqual(GameplayRuntimeEventType.ComponentAttributeChanged, events[0].Type);
            Assert.AreEqual(entityId, events[0].ComponentEntityId);
            Assert.AreEqual(HpAttributeId, events[0].AttributeId);
            Assert.AreEqual(100, events[0].OldAttributeValue);
            Assert.AreEqual(88, events[0].NewAttributeValue);
            Assert.AreEqual(-12, events[0].AttributeDelta);
        }

        [Test]
        public void Tick_DamageZeroSkips()
        {
            var outputCommands = new List<RuntimeCommand>();
            var world = new GameplayComponentWorld();
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), world.CreateEntity());
            var system = new CombatHitApplicationSystem(
                map,
                world,
                () => new[] { Result(HitResolveKind.Damage, targetId: 20, damage: 0) },
                HpAttributeId,
                outputCommands);

            system.Tick(CreateContext(world));

            Assert.AreEqual(0, outputCommands.Count);
        }

        [Test]
        public void Tick_NonDamageKindsSkip()
        {
            HitResolveKind[] skippedKinds =
            {
                HitResolveKind.TargetDead,
                HitResolveKind.Invincible,
                HitResolveKind.Parried,
                HitResolveKind.Blocked,
                HitResolveKind.Duplicate,
                HitResolveKind.SelfDamage,
                HitResolveKind.Friendly,
            };
            var outputCommands = new List<RuntimeCommand>();
            var world = new GameplayComponentWorld();
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), world.CreateEntity());
            var results = new List<HitResolveResult>();
            for (int i = 0; i < skippedKinds.Length; i++)
            {
                results.Add(Result(skippedKinds[i], targetId: 20, damage: 0, traceId: 900 + i));
            }

            var system = new CombatHitApplicationSystem(
                map,
                world,
                () => results,
                HpAttributeId,
                outputCommands);

            system.Tick(CreateContext(world));

            Assert.AreEqual(0, outputCommands.Count);
        }

        [Test]
        public void Tick_MissingMapSkipsWithoutThrowing()
        {
            var outputCommands = new List<RuntimeCommand>();
            var world = new GameplayComponentWorld();
            var system = new CombatHitApplicationSystem(
                new CombatEntityGameplayMap(),
                world,
                () => new[] { Result(HitResolveKind.Damage, targetId: 20, damage: 9) },
                HpAttributeId,
                outputCommands);

            Assert.DoesNotThrow(() => system.Tick(CreateContext(world)));
            Assert.AreEqual(0, outputCommands.Count);
        }

        [Test]
        public void Tick_MultipleAcceptedDamageResultsAppendMultipleCommands()
        {
            var outputCommands = new List<RuntimeCommand>();
            var world = new GameplayComponentWorld();
            var map = new CombatEntityGameplayMap();
            GameplayEntityId firstEntity = world.CreateEntity();
            GameplayEntityId secondEntity = world.CreateEntity();
            map.Register(Combat(20), firstEntity);
            map.Register(Combat(30), secondEntity);
            var system = new CombatHitApplicationSystem(
                map,
                world,
                () => new[]
                {
                    Result(HitResolveKind.Damage, targetId: 20, damage: 4, traceId: 101),
                    Result(HitResolveKind.Blocked, targetId: 20, damage: 0, traceId: 102),
                    Result(HitResolveKind.Damage, targetId: 30, damage: 7, traceId: 103),
                },
                HpAttributeId,
                outputCommands);

            system.Tick(CreateContext(world, frame: 12));

            Assert.AreEqual(2, outputCommands.Count);
            AssertCommand(outputCommands[0], new RuntimeFrame(12), firstEntity, -4, "101");
            AssertCommand(outputCommands[1], new RuntimeFrame(12), secondEntity, -7, "103");
        }

        [Test]
        public void Constructor_StoresMetadataDefaults()
        {
            var system = new CombatHitApplicationSystem(
                new CombatEntityGameplayMap(),
                new GameplayComponentWorld(),
                () => Array.Empty<HitResolveResult>(),
                HpAttributeId,
                new List<RuntimeCommand>());

            Assert.AreEqual("mxframework.bridge.combat.hit_application", CombatHitApplicationSystem.DefaultSystemId);
            Assert.AreEqual(CombatHitApplicationSystem.DefaultSystemId, system.SystemId);
            Assert.AreEqual(GameplaySystemPhase.Resolution, system.Phase);
            Assert.AreEqual(80, system.Priority);
            Assert.IsTrue(system.IsEnabled);
        }

        [Test]
        public void Constructor_StoresCustomMetadata()
        {
            var system = new CombatHitApplicationSystem(
                new CombatEntityGameplayMap(),
                new GameplayComponentWorld(),
                () => Array.Empty<HitResolveResult>(),
                HpAttributeId,
                new List<RuntimeCommand>(),
                systemId: "custom.hit.apply",
                phase: GameplaySystemPhase.Diagnostics,
                priority: 140);

            Assert.AreEqual("custom.hit.apply", system.SystemId);
            Assert.AreEqual(GameplaySystemPhase.Diagnostics, system.Phase);
            Assert.AreEqual(140, system.Priority);
            Assert.IsTrue(system.IsEnabled);
        }

        [Test]
        public void SetEnabled_DisabledTickDoesNotReadResultsOrAppendCommands()
        {
            var outputCommands = new List<RuntimeCommand>();
            var world = new GameplayComponentWorld();
            int readCount = 0;
            var system = new CombatHitApplicationSystem(
                new CombatEntityGameplayMap(),
                world,
                () =>
                {
                    readCount++;
                    return new[] { Result(HitResolveKind.Damage, targetId: 20, damage: 1) };
                },
                HpAttributeId,
                outputCommands);

            system.SetEnabled(false);
            system.Tick(CreateContext(world));

            Assert.IsFalse(system.IsEnabled);
            Assert.AreEqual(0, readCount);
            Assert.AreEqual(0, outputCommands.Count);

            system.SetEnabled(true);
            system.Tick(CreateContext(world));

            Assert.IsTrue(system.IsEnabled);
            Assert.AreEqual(1, readCount);
        }

        [Test]
        public void Constructor_NullOutputCommandsThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new CombatHitApplicationSystem(
                new CombatEntityGameplayMap(),
                new GameplayComponentWorld(),
                () => Array.Empty<HitResolveResult>(),
                HpAttributeId,
                null));
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
            return CreateContext(world, Array.Empty<RuntimeCommand>(), frame);
        }

        private static GameplaySystemContext CreateContext(
            GameplayComponentWorld world,
            IReadOnlyList<RuntimeCommand> commands,
            long frame = 0)
        {
            return new GameplaySystemContext(
                new RuntimeFrame(frame),
                0d,
                0d,
                new GameplayWorld(),
                commands,
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
