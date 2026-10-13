using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.GameplayBridge
{
    public sealed class CombatDamageApplicationSystemTests
    {
        private const int HpAttributeId = 100;

        private sealed class NonLethalBudgetProbe : ICombatHpDebitPolicy
        {
            internal readonly List<int> Hp = new List<int>(), Budgets = new List<int>();
            public int LimitHpDebit(in HitResolveResult hit, GameplayEntityId target, int currentHp, int maximumDebit)
            {
                Hp.Add(currentHp); Budgets.Add(maximumDebit);
                return hit.TraceId == 703 ? maximumDebit : Math.Min(maximumDebit, Math.Max(0, currentHp - 1));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HpPolicyRunsAfterArmorAndBeforeEveryCommandEventAndTelemetry(bool followingUnrestrictedCommit)
        {
            var world = new GameplayComponentWorld(); var target = CreateTarget(world, 10);
            var armor = world.GetOrCreateStore<GameplayArmorIntegrityComponent>();
            armor.Set(target, new GameplayArmorIntegrityComponent { MaxIntegrity = 100, CurrentIntegrity = 80 });
            var map = new CombatEntityGameplayMap(); map.Register(Combat(20), target);
            var hits = new List<HitResolveResult> { Result(HitResolveKind.Damage, 20, 25, 701), Result(HitResolveKind.Damage, 20, 25, 702) };
            if (followingUnrestrictedCommit) hits.Add(Result(HitResolveKind.Damage, 20, 25, 703));
            var commands = new List<RuntimeCommand>(); var rows = new List<DamageTelemetryRow>();
            var events = new List<DamageResolvedEvent>(); var policy = new NonLethalBudgetProbe();
            var system = new CombatDamageApplicationSystem(policy, map, world, null, () => hits,
                hit => Payload(hit, hit.Damage), HpAttributeId, commands, rows, events.Add);
            system.Tick(CreateContext(world, frame: 7));
            new GameplayAttributeCommandSystem().Tick(CreateContext(world, commands, frame: 7));
            Assert.AreEqual(followingUnrestrictedCommit ? 0 : 1, ReadHp(world, target));
            Assert.AreEqual(10, policy.Hp[0]); Assert.AreEqual(5, policy.Budgets[0]);
            Assert.AreEqual(5, policy.Hp[1]); Assert.AreEqual(5, policy.Budgets[1]);
            Assert.AreEqual(5, rows[0].FinalDamage); Assert.AreEqual(4, rows[1].FinalDamage);
            Assert.AreEqual(1, rows[1].HpAfter); Assert.AreEqual(4, events[1].FinalDamage);
            Assert.IsTrue(armor.TryGet(target, out var remainingArmor));
            Assert.AreEqual(followingUnrestrictedCommit ? 5 : 30, remainingArmor.CurrentIntegrity);
            if (followingUnrestrictedCommit) {
                Assert.AreEqual(1, policy.Hp[2]); Assert.AreEqual(1, events[2].FinalDamage);
                Assert.AreEqual(0, rows[2].HpAfter);
            }
        }


        [TestCase(10, 8, 2, 0)]
        [TestCase(100, 12, 12, 76)]
        [TestCase(0, 8, 0, 0)]
        public void Tick_BatchDamageUsesRemainingHpForCommandsEventsAndTelemetry(
            int initialHp, int incomingDamage, int secondDamage, int expectedHp)
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId target = CreateTarget(world, initialHp);
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), target);
            var hits = new[]
            {
                Result(HitResolveKind.Damage, 20, incomingDamage, 701),
                Result(HitResolveKind.Damage, 20, incomingDamage, 702)
            };
            var commands = new List<RuntimeCommand>();
            var rows = new List<DamageTelemetryRow>();
            var events = new List<DamageResolvedEvent>();
            var presentations = new List<HitPresentationEvent>();
            var system = new CombatDamageApplicationSystem(map, world, null,
                () => hits, hit => Payload(hit, hit.Damage), HpAttributeId, commands, rows,
                events.Add, presentations.Add);

            system.Tick(CreateContext(world, frame: 7));

            Assert.AreEqual(initialHp, ReadHp(world, target), "HP writes must remain command-owned.");
            int firstDamage = Math.Min(initialHp, incomingDamage);
            Assert.AreEqual(initialHp, rows[0].HpBefore);
            Assert.AreEqual(initialHp - firstDamage, rows[0].HpAfter);
            Assert.AreEqual(firstDamage, rows[0].FinalDamage);
            Assert.AreEqual(rows[0].HpAfter, rows[1].HpBefore);
            Assert.AreEqual(expectedHp, rows[1].HpAfter);
            Assert.AreEqual(secondDamage, rows[1].FinalDamage);
            Assert.AreEqual(firstDamage, events[0].FinalDamage);
            Assert.AreEqual(secondDamage, events[1].FinalDamage);
            Assert.AreEqual(2, presentations.Count);
            Assert.AreEqual(initialHp > 0 ? 2 : 0, commands.Count);
            if (initialHp > 0)
            {
                AssertCommand(commands[0], new RuntimeFrame(7), target, -firstDamage,
                    new RuntimeTraceIdentity(100, 0, 701, 0));
                AssertCommand(commands[1], new RuntimeFrame(7), target, -secondDamage,
                    new RuntimeTraceIdentity(100, 0, 702, 0));
            }
            new GameplayAttributeCommandSystem().Tick(CreateContext(world, commands, frame: 7));
            Assert.AreEqual(expectedHp, ReadHp(world, target));
        }

        [Test]
        public void Tick_BatchHpKeepsOtherTargetsIndependentAndStopsAtZero()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId target = CreateTarget(world, 10);
            GameplayEntityId other = CreateTarget(world, 20);
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), target);
            map.Register(Combat(30), other);
            var hits = new[]
            {
                Result(HitResolveKind.Damage, 20, 8, 701),
                Result(HitResolveKind.Damage, 30, 9, 702),
                Result(HitResolveKind.Damage, 20, 8, 703),
                Result(HitResolveKind.Damage, 20, 8, 704)
            };
            var commands = new List<RuntimeCommand>();
            var rows = new List<DamageTelemetryRow>();
            var system = new CombatDamageApplicationSystem(map, world, null,
                () => hits, hit => Payload(hit, hit.Damage), HpAttributeId, commands, rows);

            system.Tick(CreateContext(world, frame: 7));
            new GameplayAttributeCommandSystem().Tick(CreateContext(world, commands, frame: 7));

            Assert.AreEqual(0, ReadHp(world, target));
            Assert.AreEqual(11, ReadHp(world, other));
            Assert.AreEqual(3, commands.Count, "No HP command is emitted after the target is exhausted.");
            Assert.AreEqual(2, rows[2].HpBefore);
            Assert.AreEqual(2, rows[2].FinalDamage);
            Assert.AreEqual(0, rows[3].HpBefore);
            Assert.AreEqual(0, rows[3].FinalDamage);
        }

        [Test]
        public void Tick_RejectedAndBlockedRowsObservePriorBatchDamageWithoutConsumingHp()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId target = CreateTarget(world, 10);
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), target);
            var hits = new[]
            {
                Result(HitResolveKind.Damage, 20, 8, 701),
                Result(HitResolveKind.Duplicate, 20, 8, 702),
                Result(HitResolveKind.Friendly, 20, 8, 703),
                Result(HitResolveKind.SelfDamage, 20, 8, 704),
                Result(HitResolveKind.Blocked, 20, 8, 705),
                Result(HitResolveKind.Damage, 20, 8, 706)
            };
            var commands = new List<RuntimeCommand>();
            var rows = new List<DamageTelemetryRow>();
            var system = new CombatDamageApplicationSystem(map, world, null,
                () => hits, hit => Payload(hit, hit.Damage), HpAttributeId, commands, rows);

            system.Tick(CreateContext(world, frame: 7));
            new GameplayAttributeCommandSystem().Tick(CreateContext(world, commands, frame: 7));

            Assert.AreEqual(2, commands.Count);
            Assert.AreEqual(0, ReadHp(world, target));
            for (int i = 1; i < hits.Length - 1; i++)
            {
                Assert.AreEqual(2, rows[i].HpBefore);
                Assert.AreEqual(2, rows[i].HpAfter);
                Assert.AreEqual(0, rows[i].FinalDamage);
            }
        }

        [Test]
        public void Tick_BatchHpClampPreservesSequentialArmorWear()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId target = CreateTarget(world, 10);
            var armorStore = world.GetOrCreateStore<GameplayArmorIntegrityComponent>();
            armorStore.Set(target, new GameplayArmorIntegrityComponent
                { MaxIntegrity = 100, CurrentIntegrity = 80 });
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), target);
            var hits = new[]
            {
                Result(HitResolveKind.Damage, 20, 25, 701),
                Result(HitResolveKind.Damage, 20, 25, 702)
            };
            var commands = new List<RuntimeCommand>();
            var rows = new List<DamageTelemetryRow>();
            var system = new CombatDamageApplicationSystem(map, world, null,
                () => hits, hit => Payload(hit, hit.Damage), HpAttributeId, commands, rows);

            system.Tick(CreateContext(world, frame: 7));
            new GameplayAttributeCommandSystem().Tick(CreateContext(world, commands, frame: 7));

            Assert.AreEqual(0, ReadHp(world, target));
            Assert.AreEqual(5, rows[0].FinalDamage);
            Assert.AreEqual(5, rows[1].HpBefore);
            Assert.AreEqual(5, rows[1].FinalDamage);
            Assert.IsTrue(armorStore.TryGet(target, out GameplayArmorIntegrityComponent armor));
            Assert.AreEqual(30, armor.CurrentIntegrity);
            Assert.IsFalse(armor.IsBroken);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Tick_NextBatchReadsAuthoritativeHpAfterCommitOrDiscard(bool commit)
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId target = CreateTarget(world, 10);
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), target);
            var hits = new[]
            {
                Result(HitResolveKind.Damage, 20, 8, 701),
                Result(HitResolveKind.Damage, 20, 8, 702)
            };
            var commands = new List<RuntimeCommand>();
            var rows = new List<DamageTelemetryRow>();
            var system = new CombatDamageApplicationSystem(map, world, null,
                () => hits, hit => Payload(hit, hit.Damage), HpAttributeId, commands, rows);
            system.Tick(CreateContext(world, frame: 7));
            if (commit)
            {
                new GameplayAttributeCommandSystem().Tick(CreateContext(world, commands, frame: 7));
                world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(target,
                    new GameplayAttributeSetComponent(new GameplayAttributeValue(HpAttributeId, 50, 50)));
            }
            commands.Clear();
            rows.Clear();

            system.Tick(CreateContext(world, frame: 8));
            new GameplayAttributeCommandSystem().Tick(CreateContext(world, commands, frame: 8));

            Assert.AreEqual(commit ? 50 : 10, rows[0].HpBefore);
            Assert.AreEqual(commit ? 42 : 2, rows[1].HpBefore);
            Assert.AreEqual(commit ? 34 : 0, ReadHp(world, target));
        }

        [Test]
        public void Tick_SingleHitCommitBetweenTicksKeepsSequentialHp()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId target = CreateTarget(world, 10);
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), target);
            var hits = new[] { Result(HitResolveKind.Damage, 20, 8, 701) };
            var commands = new List<RuntimeCommand>();
            var rows = new List<DamageTelemetryRow>();
            var system = new CombatDamageApplicationSystem(map, world, null,
                () => hits, hit => Payload(hit, hit.Damage), HpAttributeId, commands, rows);
            for (int i = 0; i < 2; i++)
            {
                commands.Clear();
                hits[0] = Result(HitResolveKind.Damage, 20, 8, 701 + i);
                system.Tick(CreateContext(world, frame: 7 + i));
                new GameplayAttributeCommandSystem().Tick(CreateContext(world, commands, frame: 7 + i));
            }

            Assert.AreEqual(0, ReadHp(world, target));
            Assert.AreEqual(10, rows[0].HpBefore);
            Assert.AreEqual(2, rows[1].HpBefore);
            Assert.AreEqual(2, rows[1].FinalDamage);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Tick_BatchDamageProducerDoesNotAllocateAfterWarmup(bool sameTarget)
        {
            const int Count = 128;
            var world = new GameplayComponentWorld();
            GameplayEntityId first = CreateTarget(world, 100000);
            var map = new CombatEntityGameplayMap();
            var hits = new HitResolveResult[Count];
            var payloads = new DamageProfileResolveResult[Count];
            for (int i = 0; i < Count; i++)
            {
                int targetId = sameTarget ? 20 : 20 + i;
                if (!sameTarget || i == 0)
                    map.Register(Combat(targetId), i == 0 ? first : CreateTarget(world, 100000));
                hits[i] = Result(HitResolveKind.Damage, targetId, 1, 7000 + i);
                payloads[i] = Payload(hits[i], 1);
            }
            var commands = new List<RuntimeCommand>(Count);
            var rows = new List<DamageTelemetryRow>(Count);
            var events = new List<DamageResolvedEvent>(Count);
            var presentations = new List<HitPresentationEvent>(Count);
            var system = new CombatDamageApplicationSystem(map, world, null,
                () => hits, hit => payloads[hit.TraceId - 7000], HpAttributeId, commands, rows,
                events.Add, presentations.Add);
            GameplaySystemContext context = CreateContext(world, frame: 7);
            void RunBatch()
            {
                commands.Clear();
                rows.Clear();
                events.Clear();
                presentations.Clear();
                system.Tick(context);
            }
            for (int i = 0; i < 8; i++)
                RunBatch();
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 16; i++)
                RunBatch();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated, "Measures the damage producer; command consumption is a separate boundary.");
            Assert.AreEqual(Count, commands.Count);
            Assert.AreEqual(sameTarget ? 100000 - Count : 99999, rows[Count - 1].HpAfter);
            Assert.AreEqual(100000, ReadHp(world, first), "The measured batches leave HP commands uncommitted.");
        }

        [Test]
        public void Tick_ExceptionClearsBatchProjectionBeforeTheNextCall()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId target = CreateTarget(world, 10);
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), target);
            var hits = new[]
            {
                Result(HitResolveKind.Damage, 20, 8, 701),
                Result(HitResolveKind.Damage, 20, 8, 702)
            };
            var commands = new List<RuntimeCommand>();
            var rows = new List<DamageTelemetryRow>();
            bool throwOnEvent = true;
            var system = new CombatDamageApplicationSystem(map, world, null,
                () => hits, hit => Payload(hit, hit.Damage), HpAttributeId, commands, rows,
                publishDamageResolvedEvent: _ =>
                {
                    if (throwOnEvent)
                        throw new InvalidOperationException("test event consumer failure");
                });
            Assert.Throws<InvalidOperationException>(() => system.Tick(CreateContext(world, frame: 7)));
            Assert.AreEqual(10, ReadHp(world, target));
            commands.Clear();
            rows.Clear();
            throwOnEvent = false;

            system.Tick(CreateContext(world, frame: 8));
            new GameplayAttributeCommandSystem().Tick(CreateContext(world, commands, frame: 8));

            Assert.AreEqual(10, rows[0].HpBefore);
            Assert.AreEqual(2, rows[1].HpBefore);
            Assert.AreEqual(0, ReadHp(world, target));
        }

        [Test]
        public void Tick_AcceptedDamageAppendsHpDeltaAndTelemetry()
        {
            var outputCommands = new List<RuntimeCommand>();
            var telemetryRows = new List<DamageTelemetryRow>();
            var resolvedEvents = new List<DamageResolvedEvent>();
            var presentationEvents = new List<HitPresentationEvent>();
            var world = new GameplayComponentWorld();
            var map = new CombatEntityGameplayMap();
            GameplayEntityId entityId = CreateTarget(world, hp: 100);
            map.Register(Combat(20), entityId);
            HitResolveResult result = Result(HitResolveKind.Damage, targetId: 20, damage: 12, traceId: 701);
            DamageProfileResolveResult payload = Payload(result, incomingDamage: 12);
            var system = new CombatDamageApplicationSystem(
                map,
                world,
                guardPressureSystem: null,
                getHitResults: () => new[] { result },
                getDamagePayload: _ => payload,
                hpAttributeId: HpAttributeId,
                outputCommands: outputCommands,
                telemetryRows: telemetryRows,
                publishDamageResolvedEvent: evt => resolvedEvents.Add(evt),
                publishHitPresentationEvent: evt => presentationEvents.Add(evt));
            var attributeSystem = new GameplayAttributeCommandSystem();

            system.Tick(CreateContext(world, frame: 7));
            attributeSystem.Tick(CreateContext(world, outputCommands, frame: 7));

            Assert.AreEqual(1, outputCommands.Count);
            AssertCommand(outputCommands[0], new RuntimeFrame(7), entityId, -12, new RuntimeTraceIdentity(100, 0, 701, 0));
            Assert.AreEqual(88, ReadHp(world, entityId));
            Assert.AreEqual(1, telemetryRows.Count);
            Assert.AreEqual(DamageProfileSourceKind.WeaponTrace, telemetryRows[0].SourceKind);
            Assert.AreEqual(new MxFramework.CharacterAction.CharacterActionId(301), telemetryRows[0].ActionId);
            Assert.AreEqual(970022, telemetryRows[0].DamageProfileId);
            Assert.AreEqual(970025, telemetryRows[0].AnalysisCategory);
            DamageTelemetryRow updatedMetrics = telemetryRows[0].WithImpact(3, 4f, 5f, 6f, 7, 8f, 9f);
            Assert.AreEqual(970022, updatedMetrics.DamageProfileId);
            Assert.AreEqual(970025, updatedMetrics.AnalysisCategory);
            Assert.AreEqual(12, telemetryRows[0].IncomingDamage);
            Assert.AreEqual(12, telemetryRows[0].FinalDamage);
            Assert.AreEqual(100, telemetryRows[0].HpBefore);
            Assert.AreEqual(88, telemetryRows[0].HpAfter);
            Assert.AreEqual(1, resolvedEvents.Count);
            Assert.AreEqual(1, presentationEvents.Count);
            Assert.AreEqual(970024, presentationEvents[0].PresentationProfileId);
        }

        [Test]
        public void Tick_BlockedDamageAppliesGuardPressureWithoutHpDelta()
        {
            var outputCommands = new List<RuntimeCommand>();
            var telemetryRows = new List<DamageTelemetryRow>();
            var world = new GameplayComponentWorld();
            GameplayEntityId entityId = CreateTarget(world, hp: 100);
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
            HitResolveResult result = Result(HitResolveKind.Blocked, targetId: 20, damage: 0, traceId: 901);
            DamageProfileResolveResult payload = Payload(result, incomingDamage: 15, guardPressure: 15);
            var system = new CombatDamageApplicationSystem(
                map,
                world,
                guardSystem,
                () => new[] { result },
                _ => payload,
                HpAttributeId,
                outputCommands,
                telemetryRows);

            system.Tick(CreateContext(world, frame: 12));
            guardSystem.Tick(CreateContext(world, frame: 12));

            Assert.AreEqual(0, outputCommands.Count);
            Assert.AreEqual(100, ReadHp(world, entityId));
            Assert.AreEqual(1, guardBreakEvents.Count);
            Assert.AreEqual(entityId, guardBreakEvents[0].EntityId);
            Assert.AreEqual(1, telemetryRows.Count);
            Assert.AreEqual(HitResolveKind.Blocked, telemetryRows[0].ResultKind);
            Assert.AreEqual(15, telemetryRows[0].IncomingDamage);
            Assert.AreEqual(0, telemetryRows[0].FinalDamage);
            Assert.AreEqual(100, telemetryRows[0].HpAfter);
        }

        [Test]
        public void Tick_RejectedHitKindsDoNotGenerateHpDelta()
        {
            HitResolveKind[] rejectedKinds =
            {
                HitResolveKind.Duplicate,
                HitResolveKind.Friendly,
                HitResolveKind.SelfDamage
            };
            var outputCommands = new List<RuntimeCommand>();
            var telemetryRows = new List<DamageTelemetryRow>();
            var world = new GameplayComponentWorld();
            var map = new CombatEntityGameplayMap();
            GameplayEntityId entityId = CreateTarget(world, hp: 100);
            map.Register(Combat(20), entityId);
            var results = new List<HitResolveResult>();
            for (int i = 0; i < rejectedKinds.Length; i++)
                results.Add(Result(rejectedKinds[i], targetId: 20, damage: 0, traceId: 800 + i));
            var system = new CombatDamageApplicationSystem(
                map,
                world,
                guardPressureSystem: null,
                getHitResults: () => results,
                getDamagePayload: result => Payload(result, incomingDamage: 12),
                hpAttributeId: HpAttributeId,
                outputCommands: outputCommands,
                telemetryRows: telemetryRows);

            system.Tick(CreateContext(world, frame: 13));

            Assert.AreEqual(0, outputCommands.Count);
            Assert.AreEqual(100, ReadHp(world, entityId));
            Assert.AreEqual(3, telemetryRows.Count);
            Assert.AreEqual(0, telemetryRows[0].FinalDamage);
            Assert.AreEqual(0, telemetryRows[1].FinalDamage);
            Assert.AreEqual(0, telemetryRows[2].FinalDamage);
        }

        [Test]
        public void Tick_ArmorAbsorbAdjustsFinalDamageBeforeHpCommand()
        {
            var outputCommands = new List<RuntimeCommand>();
            var telemetryRows = new List<DamageTelemetryRow>();
            var armorBreakEvents = new List<ArmorBreakEvent>();
            var world = new GameplayComponentWorld();
            GameplayEntityId entityId = CreateTarget(world, hp: 100);
            world.GetOrCreateStore<GameplayArmorIntegrityComponent>().Set(
                entityId,
                new GameplayArmorIntegrityComponent
                {
                    MaxIntegrity = 100,
                    CurrentIntegrity = 80,
                    IsBroken = false
                });
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(20), entityId);
            HitResolveResult result = Result(HitResolveKind.Damage, targetId: 20, damage: 25, traceId: 702);
            var system = new CombatDamageApplicationSystem(
                map,
                world,
                guardPressureSystem: null,
                getHitResults: () => new[] { result },
                getDamagePayload: _ => Payload(result, incomingDamage: 25),
                hpAttributeId: HpAttributeId,
                outputCommands: outputCommands,
                telemetryRows: telemetryRows,
                publishArmorBreakEvent: evt => armorBreakEvents.Add(evt));
            var attributeSystem = new GameplayAttributeCommandSystem();

            system.Tick(CreateContext(world, frame: 7));
            attributeSystem.Tick(CreateContext(world, outputCommands, frame: 7));

            Assert.AreEqual(1, outputCommands.Count);
            AssertCommand(outputCommands[0], new RuntimeFrame(7), entityId, -5, new RuntimeTraceIdentity(100, 0, 702, 0));
            Assert.AreEqual(95, ReadHp(world, entityId));
            Assert.AreEqual(0, armorBreakEvents.Count);
            Assert.AreEqual(25, telemetryRows[0].IncomingDamage);
            Assert.AreEqual(5, telemetryRows[0].FinalDamage);
            Assert.AreEqual(95, telemetryRows[0].HpAfter);
        }

        [Test]
        public void Tick_MultipleTraceIdsDoNotAllocateAfterWarmup()
        {
            const int ResultCount = 128;
            const int TraceIdBase = 7000;
            var outputCommands = new List<RuntimeCommand>(ResultCount);
            var telemetryRows = new List<DamageTelemetryRow>(ResultCount);
            var resolvedEvents = new List<DamageResolvedEvent>(ResultCount);
            var presentationEvents = new List<HitPresentationEvent>(ResultCount);
            var drainedEvents = new List<GameplayRuntimeEvent>(ResultCount);
            var results = new List<HitResolveResult>(ResultCount);
            var payloads = new DamageProfileResolveResult[ResultCount];
            var world = new GameplayComponentWorld(
                registry: null,
                events: new RuntimeEventQueue<GameplayRuntimeEvent>(ResultCount));
            var map = new CombatEntityGameplayMap();
            for (int i = 0; i < ResultCount; i++)
            {
                int combatEntityId = 20 + i;
                GameplayEntityId entityId = CreateTarget(world, hp: 100000);
                map.Register(Combat(combatEntityId), entityId);
                HitResolveResult result = Result(
                    HitResolveKind.Damage,
                    combatEntityId,
                    damage: 1,
                    traceId: TraceIdBase + i);
                results.Add(result);
                payloads[i] = Payload(result, incomingDamage: 1);
            }

            var system = new CombatDamageApplicationSystem(
                map,
                world,
                guardPressureSystem: null,
                getHitResults: () => results,
                getDamagePayload: result => payloads[result.TraceId - TraceIdBase],
                hpAttributeId: HpAttributeId,
                outputCommands: outputCommands,
                telemetryRows: telemetryRows,
                publishDamageResolvedEvent: evt => resolvedEvents.Add(evt),
                publishHitPresentationEvent: evt => presentationEvents.Add(evt));
            var attributeSystem = new GameplayAttributeCommandSystem();
            var gameplayWorld = new GameplayWorld();
            var resolutionCommandState = new GameplayCommandExecutionState();
            var attributeCommandState = new GameplayCommandExecutionState();
            var frame = new RuntimeFrame(17);
            var resolutionContext = new GameplaySystemContext(
                frame,
                0d,
                0d,
                gameplayWorld,
                Array.Empty<RuntimeCommand>(),
                world.Events,
                resolutionCommandState,
                world);
            var attributeContext = new GameplaySystemContext(
                frame,
                0d,
                0d,
                gameplayWorld,
                outputCommands,
                world.Events,
                attributeCommandState,
                world);

            void ExecutePass()
            {
                outputCommands.Clear();
                telemetryRows.Clear();
                resolvedEvents.Clear();
                presentationEvents.Clear();
                drainedEvents.Clear();
                resolutionCommandState.Clear();
                attributeCommandState.Clear();

                system.Tick(resolutionContext);
                attributeSystem.Tick(attributeContext);
                world.DrainEvents(frame, drainedEvents);
            }

            for (int i = 0; i < 8; i++)
                ExecutePass();

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 16; i++)
                ExecutePass();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(ResultCount, outputCommands.Count);
            AssertCommand(outputCommands[0], frame, new GameplayEntityId(1, 1), -1, new RuntimeTraceIdentity(100, 0, 7000, 0));
            AssertCommand(
                outputCommands[ResultCount - 1],
                frame,
                new GameplayEntityId(ResultCount, 1),
                -1,
                new RuntimeTraceIdentity(100, 0, 7127, 0));
        }

        [Test]
        public void SetEnabled_DisabledTickDoesNotReadResults()
        {
            int readCount = 0;
            var system = new CombatDamageApplicationSystem(
                new CombatEntityGameplayMap(),
                new GameplayComponentWorld(),
                guardPressureSystem: null,
                getHitResults: () =>
                {
                    readCount++;
                    return Array.Empty<HitResolveResult>();
                },
                getDamagePayload: _ => default,
                hpAttributeId: HpAttributeId,
                outputCommands: new List<RuntimeCommand>());

            system.SetEnabled(false);
            system.Tick(CreateContext(new GameplayComponentWorld()));

            Assert.IsFalse(system.IsEnabled);
            Assert.AreEqual(0, readCount);
        }

        [Test]
        public void DamageTrace_SnapshotRestoreContinuesHpAndCommandsAtEveryFrame()
        {
            var source = new GameplayComponentWorld();
            GameplayCoreComponentSchemaDescriptors.RegisterSaveState(source.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(source.Schemas);
            GameplayEntityId entity = CreateTarget(source, hp: 300);
            var sourceMap = new CombatEntityGameplayMap();
            sourceMap.Register(Combat(20), entity);
            var sourceOutput = new List<RuntimeCommand>();
            for (int frame = 0; frame < 3; frame++) ApplyFrame(source, sourceMap, sourceOutput, frame);
            var restored = new GameplayComponentWorld();
            GameplayCoreComponentSchemaDescriptors.RegisterSaveState(restored.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(restored.Schemas);
            RuntimeSaveStateResult<RuntimeSaveState> capture = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState();
            Assert.IsTrue(capture.Success);
            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(restored).RestoreSaveState(capture.Value);
            Assert.IsTrue(restore.Success, restore.Error.ToString());
            var restoredMap = new CombatEntityGameplayMap();
            restoredMap.Register(Combat(20), entity);
            var restoredOutput = new List<RuntimeCommand>();
            for (int frame = 3; frame < 12; frame++)
            {
                ApplyFrame(source, sourceMap, sourceOutput, frame);
                ApplyFrame(restored, restoredMap, restoredOutput, frame);
                Assert.AreEqual(ReadHp(source, entity), ReadHp(restored, entity),
                    "first divergence frame=" + frame + " field=attribute.hp");
                CollectionAssert.AreEqual(sourceOutput, restoredOutput,
                    "first divergence frame=" + frame + " field=damage.outputCommands");
            }
            Assert.Less(ReadHp(source, entity), 300, "Trace must apply real authority damage.");

            void ApplyFrame(GameplayComponentWorld world, CombatEntityGameplayMap map, List<RuntimeCommand> output, int frame)
            {
                output.Clear();
                HitResolveResult hit = Result(HitResolveKind.Damage, 20, 7 + frame % 3, 8000 + frame, frame);
                var system = new CombatDamageApplicationSystem(map, world, null, () => new[] { hit },
                    _ => Payload(hit, 7 + frame % 3), HpAttributeId, output);
                system.Tick(CreateContext(world, frame));
                new GameplayAttributeCommandSystem().Tick(CreateContext(world, output, frame));
            }
        }

        private static DamageProfileResolveResult Payload(
            HitResolveResult result,
            int incomingDamage,
            int guardPressure = 0)
        {
            var usage = new DamageProfileUsage(
                DamageProfileSourceKind.WeaponTrace,
                actionId: new MxFramework.CharacterAction.CharacterActionId(301),
                actionInstanceId: result.ActionInstanceId,
                stableEventId: 970020,
                equipmentSlotId: 1,
                weaponId: 970021,
                sourceEntityId: result.AttackerId.Value,
                targetEntityId: result.TargetId.Value,
                hitZoneId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id55,
                bodyPartId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id10,
                frame: result.Frame.Value,
                localFrame: 23);
            return new DamageProfileResolveResult(
                success: true,
                reason: string.Empty,
                damageProfileId: 970022,
                damageTypeId: 970023,
                formulaId: DamageFormulaIds.Flat,
                incomingDamage: incomingDamage,
                staggerFrames: 3,
                impactForce: 0,
                knockback: FixVector3.Zero,
                impactPower: 0,
                guardPressure: guardPressure,
                presentationProfileId: 970024,
                usage: usage,
                analysisCategory: 970025);
        }

        private static GameplayEntityId CreateTarget(GameplayComponentWorld world, int hp)
        {
            GameplayEntityId entityId = world.CreateEntity();
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entityId,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(HpAttributeId, hp, hp)));
            return entityId;
        }

        private static int ReadHp(GameplayComponentWorld world, GameplayEntityId entityId)
        {
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store));
            Assert.IsTrue(store.TryGet(entityId, out GameplayAttributeSetComponent attributes));
            return attributes.GetCurrentValueOrDefault(HpAttributeId);
        }

        private static void AssertCommand(
            RuntimeCommand command,
            RuntimeFrame frame,
            GameplayEntityId entityId,
            int delta,
            RuntimeTraceIdentity traceId)
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

        private static HitResolveResult Result(HitResolveKind kind, int targetId, int damage, int traceId = 7, int frame = 10)
        {
            return new HitResolveResult(
                Combat(1),
                Combat(targetId),
                actionId: 1001,
                actionInstanceId: 2001,
                traceId: traceId,
                frame: new CombatFrame(frame),
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
