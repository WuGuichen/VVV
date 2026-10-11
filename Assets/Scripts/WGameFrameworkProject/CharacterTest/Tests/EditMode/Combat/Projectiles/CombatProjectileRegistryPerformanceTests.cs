using System;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Projectiles
{
    public sealed class CombatProjectileRegistryPerformanceTests
    {
        private const int DamageTypeFire = 930001;
        private const int DamageTypeIce = 930002;
        private const int DamageTypeProjectile = 930003;
        private const int AnalysisRegistry = 930004;
        private const int ImpactEventBase = 930101;
        [Test]
        public void DefinitionRegistry_ComputeStableHashDoesNotAllocateAfterWarmup()
        {
            var registry = new CombatProjectileDefinitionRegistry(new[]
            {
                CreateDefinition(1, "projectile.test.a", "fire"),
                CreateDefinition(2, "projectile.test.b", "ice")
            });
            long expected = registry.ComputeStableHash();

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            long actual = 0L;
            for (int i = 0; i < 128; i++)
                actual = registry.ComputeStableHash();

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void DefinitionRegistry_RegisterInvalidatesStableHashCache()
        {
            var registry = new CombatProjectileDefinitionRegistry(new[]
            {
                CreateDefinition(1, "projectile.test.a", "fire")
            });
            long first = registry.ComputeStableHash();

            Assert.IsTrue(registry.TryRegister(
                CreateDefinition(2, "projectile.test.b", "ice"),
                out string failure),
                failure);
            long second = registry.ComputeStableHash();

            Assert.AreNotEqual(first, second);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            long actual = 0L;
            for (int i = 0; i < 128; i++)
                actual = registry.ComputeStableHash();

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(second, actual);
        }

        [Test]
        public void SpawnProfileRegistry_ComputeStableHashDoesNotAllocateAfterWarmup()
        {
            var registry = new CombatProjectileSpawnProfileRegistry(new[]
            {
                CreateSpawnProfile("spawn.test.a", "projectile.test.a"),
                CreateSpawnProfile("spawn.test.b", "projectile.test.b")
            });
            long expected = registry.ComputeStableHash();

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            long actual = 0L;
            for (int i = 0; i < 128; i++)
                actual = registry.ComputeStableHash();

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void SpawnProfileRegistry_RegisterInvalidatesStableHashCache()
        {
            var registry = new CombatProjectileSpawnProfileRegistry(new[]
            {
                CreateSpawnProfile("spawn.test.a", "projectile.test.a")
            });
            long first = registry.ComputeStableHash();

            Assert.IsTrue(registry.TryRegister(
                CreateSpawnProfile("spawn.test.b", "projectile.test.b"),
                out string failure),
                failure);
            long second = registry.ComputeStableHash();

            Assert.AreNotEqual(first, second);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            long actual = 0L;
            for (int i = 0; i < 128; i++)
                actual = registry.ComputeStableHash();

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(second, actual);
        }

        private static CombatProjectileDefinition CreateDefinition(
            int definitionId,
            string stableId,
            string damageTypeId)
        {
            return new CombatProjectileDefinition(
                definitionId,
                stableId,
                new CombatProjectileMotionConfig(Fix64.One, lifetimeFrames: 30),
                new CombatProjectileCollisionConfig(
                    Fix64.FromRatio(1, 10),
                    new CombatPhysicsLayerMask(1)),
                new CombatProjectileImpactConfig(
                    new CombatHitEventConfig(
                        CombatHitTargetTypes.HostileCharacter,
                        stableEventId: ImpactEventBase + definitionId,
                        damage: new CombatHitDamageConfig(
                            damageTypeId == "fire" ? DamageTypeFire : DamageTypeIce,
                            baseDamage: definitionId,
                            impactPower: definitionId + 1,
                            tags: new[] { DamageTypeProjectile, damageTypeId == "fire" ? DamageTypeFire : DamageTypeIce },
                            analysisCategory: AnalysisRegistry,
                            damageTypeLabel: damageTypeId,
                            analysisCategoryLabel: "RegistryPerformance",
                            tagLabels: new[] { "projectile", damageTypeId }),
                        onHitBuffs: new[]
                        {
                            new CombatHitBuffApplicationConfig(600000 + definitionId)
                        }),
                    stableEventId: ImpactEventBase + definitionId,
                    stableEventLabel: stableId + ".impact"));
        }

        private static CombatProjectileSpawnProfile CreateSpawnProfile(
            string stableId,
            string projectileStableId)
        {
            return new CombatProjectileSpawnProfile(
                stableId == "spawn.test.a" ? 930201 : 930202,
                stableId,
                projectileStableId == "projectile.test.a" ? 1 : 2,
                projectileStableId,
                new CombatProjectileSpawnOriginConfig(
                    CombatProjectileSpawnOriginKind.Socket,
                    1,
                    FixVector3.Zero,
                    FixVector3.Zero),
                new CombatProjectileAimConfig(
                    CombatProjectileAimKind.CasterForwardRange,
                    Fix64.Zero,
                    CombatProjectileAimKind.CasterForwardRange,
                    Fix64.FromInt(10)));
        }
    }
}
