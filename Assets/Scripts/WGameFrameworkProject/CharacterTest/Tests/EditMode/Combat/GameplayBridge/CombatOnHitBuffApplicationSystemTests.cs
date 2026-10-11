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
    public sealed class CombatOnHitBuffApplicationSystemTests
    {
        [Test]
        public void Tick_DamageHitEnqueuesConfiguredBuffRequests()
        {
            var map = new CombatEntityGameplayMap();
            var requests = new GameplayBuffRequestBuffer();
            GameplayEntityId target = new GameplayEntityId(2, 1);
            map.Register(Combat(20), target);
            HitResolveResult hit = Result(HitResolveKind.Damage, damage: 8, traceId: 77);
            var hitEvent = new CombatHitEventConfig(
                CombatHitTargetTypes.HostileCharacter,
                onHitBuffs: new[]
                {
                    new CombatHitBuffApplicationConfig(601011),
                    new CombatHitBuffApplicationConfig(601010)
                });
            var system = new CombatOnHitBuffApplicationSystem(
                map,
                requests,
                () => new[] { hit },
                result => hitEvent.OnHitBuffs);

            system.Tick(CreateContext());

            GameplayBuffApplicationRequest[] snapshot = requests.CreateSnapshot();
            Assert.AreEqual(2, snapshot.Length);
            Assert.AreEqual(601010, snapshot[0].BuffId);
            Assert.AreEqual(77, snapshot[0].SourceId);
            Assert.AreEqual(new RuntimeTraceIdentity(100, 0, 77, 0), snapshot[0].TraceId);
            Assert.AreEqual(601011, snapshot[1].BuffId);
            Assert.AreEqual(77, snapshot[1].SourceId);
            Assert.AreEqual(1, system.LastSummary.AcceptedDamageCount);
            Assert.AreEqual(2, system.LastSummary.RequestedBuffCount);
            Assert.AreEqual(0, system.LastSummary.SkippedMissingGameplayTargetCount);
        }

        [Test]
        public void Tick_NonDamageResultsAreSkippedButZeroHpControlHitAppliesBuff()
        {
            var map = new CombatEntityGameplayMap();
            var requests = new GameplayBuffRequestBuffer();
            GameplayEntityId target = new GameplayEntityId(2, 1);
            map.Register(Combat(20), target);
            var results = new[]
            {
                Result(HitResolveKind.Blocked, damage: 8),
                Result(HitResolveKind.Friendly, damage: 0),
                Result(HitResolveKind.Duplicate, damage: 0),
                Result(HitResolveKind.SelfDamage, damage: 0),
                Result(HitResolveKind.Damage, damage: 0)
            };
            var system = new CombatOnHitBuffApplicationSystem(
                map,
                requests,
                () => results,
                _ => new[] { new CombatHitBuffApplicationConfig(601010) });

            system.Tick(CreateContext());

            Assert.AreEqual(1, requests.Count);
            Assert.AreEqual(601010, requests.CreateSnapshot()[0].BuffId);
            Assert.AreEqual(results.Length, system.LastSummary.ResultCount);
            Assert.AreEqual(0, system.LastSummary.AcceptedDamageCount);
            Assert.AreEqual(results.Length - 1, system.LastSummary.SkippedResultCount);
            Assert.AreEqual(1, system.LastSummary.RequestedBuffCount);
        }

        [Test]
        public void Tick_MissingGameplayTargetSkipsWithoutThrowing()
        {
            var requests = new GameplayBuffRequestBuffer();
            HitResolveResult hit = Result(HitResolveKind.Damage, damage: 8);
            var system = new CombatOnHitBuffApplicationSystem(
                new CombatEntityGameplayMap(),
                requests,
                () => new[] { hit },
                _ => new[] { new CombatHitBuffApplicationConfig(601010) });

            Assert.DoesNotThrow(() => system.Tick(CreateContext()));
            Assert.AreEqual(0, requests.Count);
            Assert.AreEqual(1, system.LastSummary.AcceptedDamageCount);
            Assert.AreEqual(1, system.LastSummary.SkippedMissingGameplayTargetCount);
        }

        [Test]
        public void Tick_DamageHitDoesNotAllocateAfterWarmup()
        {
            var map = new CombatEntityGameplayMap();
            var requests = new GameplayBuffRequestBuffer();
            GameplayEntityId target = new GameplayEntityId(2, 1);
            map.Register(Combat(20), target);
            var results = new[]
            {
                Result(HitResolveKind.Damage, damage: 8, traceId: 77)
            };
            var buffs = new[]
            {
                new CombatHitBuffApplicationConfig(601011),
                new CombatHitBuffApplicationConfig(601010)
            };
            var system = new CombatOnHitBuffApplicationSystem(
                map,
                requests,
                () => results,
                _ => buffs);
            GameplaySystemContext context = CreateContext();

            for (int i = 0; i < 8; i++)
            {
                system.Tick(context);
                requests.Clear();
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                system.Tick(context);
                requests.Clear();
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(1, system.LastSummary.AcceptedDamageCount);
            Assert.AreEqual(2, system.LastSummary.RequestedBuffCount);
        }

        private static HitResolveResult Result(
            HitResolveKind kind,
            int damage,
            int traceId = 77)
        {
            return new HitResolveResult(
                Combat(10),
                Combat(20),
                actionId: 100,
                actionInstanceId: 200,
                traceId,
                new CombatFrame(3),
                kind,
                damage,
                staggerFrames: 0,
                FixVector3.Zero);
        }

        private static GameplaySystemContext CreateContext()
        {
            return new GameplaySystemContext(
                new RuntimeFrame(3),
                0d,
                0d,
                new GameplayWorld(),
                Array.Empty<RuntimeCommand>(),
                new RuntimeEventQueue<GameplayRuntimeEvent>());
        }

        private static CombatEntityId Combat(int value)
        {
            return new CombatEntityId(value);
        }
    }
}
