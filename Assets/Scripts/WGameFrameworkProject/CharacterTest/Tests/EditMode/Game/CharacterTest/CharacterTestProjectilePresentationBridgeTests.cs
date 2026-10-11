using System.Collections.Generic;
using System.IO;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using MxFramework.Vfx;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestProjectilePresentationBridgeTests
    {
        [Test]
        public void CharacterTestPresentationTickDoesNotBypassProjectileBridgeAuthority()
        {
            const string path =
                "Assets/Scripts/WGameFrameworkProject/CharacterTest/Composition/CharacterTestSliceServices.cs";
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("_modules.ProjectilePresenter?.TickRetiringViews();"));
            Assert.That(source, Does.Not.Contain("_modules.ProjectilePresenter?.Sync("),
                "CharacterTest must not rebuild persistent views from an unfiltered runtime snapshot");
            Assert.That(source, Does.Not.Contain("ShouldSyncProjectiles"));
            Assert.That(source, Does.Not.Contain("_projectilePresentationViews"));
        }

        [Test]
        public void FireballBurstAreaPulseOwnsOneSourceExplosionAndGenericHitBridgeDoesNotReplayPerTarget()
        {
            int[] targetCounts = { 0, 1, 3 };
            for (int caseIndex = 0; caseIndex < targetCounts.Length; caseIndex++)
            {
                CombatProjectileSnapshot projectile = CreateSnapshot(700 + caseIndex, 80 + caseIndex);
                var impacts = new CombatProjectileImpactEvent[targetCounts[caseIndex]];
                for (int i = 0; i < impacts.Length; i++)
                    impacts[i] = CreateAreaTargetImpact(projectile, 900 + i);
                var events = new CombatProjectileFrameEventBuffer();
                SetEvents(events,
                    new[] { new CombatProjectileAreaPulseTriggerEvent(projectile, CombatProjectileAreaPulseTriggerKind.DirectSweep,
                        projectile.Position, true, false, default, default, default) }, impacts);
                var vfx = new VfxRuntimeModule(CharacterTestVfxDefinitionSource.CreateDefinitions(), new RuntimeCommandBuffer());
                var bridge = new CombatProjectileVfxBridge(CharacterTestProjectilePresentationSource.CreateRegistry(), vfx);

                bridge.ProcessFrame(new RuntimeFrame(80 + caseIndex), events, null);
                for (int i = 0; i < impacts.Length; i++)
                {
                    HitPresentationEvent targetEvent = CreatePresentationEvent(
                        CharacterTestCharacterActionConfigSource.FireballBurstPresentationProfileId, 900 + i);
                    Assert.That(CharacterTestCombatVfxBridgeModule.ShouldPublish(in targetEvent), Is.False,
                        "per-target generic CombatHit mapping must not replay the source explosion");
                }

                var pending = new List<VfxSpawnRequest>();
                vfx.CopyPendingSpawnRequests(pending);
                Assert.That(pending, Has.Count.EqualTo(1), "Area 0/1/N targets must emit exactly one source explosion");
                Assert.That(pending[0].SourceDomain, Is.EqualTo(VfxSourceDomains.ProjectilePresentation));
                Assert.That(pending[0].DefinitionId.Value, Is.EqualTo(CharacterTestVfxIds.FireballBurstImpactDefinitionId));
            }

            HitPresentationEvent ordinaryTargetReaction = CreatePresentationEvent(
                CharacterTestCharacterActionConfigSource.FireballPresentationProfileId, 999);
            Assert.That(CharacterTestCombatVfxBridgeModule.ShouldPublish(in ordinaryTargetReaction), Is.True,
                "non-migrated target reaction profiles remain independently publishable");
        }

        private static CombatProjectileSnapshot CreateSnapshot(int projectileId, int frame) =>
            new CombatProjectileSnapshot(projectileId, projectileId - 1,
                CharacterTestFireballProjectileAdapter.BurstDefinitionId,
                CharacterTestFireballProjectileAdapter.BurstStableId,
                new CombatEntityId(1), 2, 3, 4, 5, new CombatFrame(frame - 1), new CombatFrame(frame), 1,
                FixVector3.Zero, new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One));

        private static CombatProjectileImpactEvent CreateAreaTargetImpact(CombatProjectileSnapshot projectile, int targetId)
        {
            var result = new HitResolveResult(projectile.SourceEntityId, new CombatEntityId(targetId), projectile.ActionId,
                projectile.ActionInstanceId, projectile.TraceId, projectile.LastFrame, HitResolveKind.Damage, 10, 0,
                FixVector3.Zero, projectile.Position, new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
            return new CombatProjectileImpactEvent(projectile, default, result, default,
                new CombatProjectileImpactContext(CombatProjectileImpactSourceKind.AreaPulse), false);
        }

        private static void SetEvents(CombatProjectileFrameEventBuffer buffer,
            IReadOnlyList<CombatProjectileAreaPulseTriggerEvent> areas,
            IReadOnlyList<CombatProjectileImpactEvent> impacts)
        {
            typeof(CombatProjectileFrameEventBuffer).GetMethod("CopyFrom",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[]
                {
                    typeof(IReadOnlyList<CombatProjectileSpawnEvent>),
                    typeof(IReadOnlyList<CombatProjectileAreaPulseTriggerEvent>),
                    typeof(IReadOnlyList<CombatProjectileImpactEvent>),
                    typeof(IReadOnlyList<CombatProjectileExpiredEvent>),
                    typeof(IReadOnlyList<CombatProjectileRejectedEvent>),
                    typeof(IReadOnlyList<CombatProjectilePhysicalEvent>)
                }, null).Invoke(buffer, new object[] { null, areas, impacts, null, null, null });
        }

        private static HitPresentationEvent CreatePresentationEvent(int presentationProfileId, int targetId)
        {
            var usage = new DamageProfileUsage(
                DamageProfileSourceKind.AreaPulse,
                actionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("projectile"),
                actionInstanceId: 3,
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0027,
                sourceEntityId: 1,
                targetEntityId: targetId,
                frame: 12,
                localFrame: 4);
            var request = new DamageRequest(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0021, usage);
            var profile = new DamageProfileResolveResult(true, string.Empty, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0021, CharacterTestAuthoredContentIds.D409_Id3, DamageFormulaIds.Flat,
                20, 0, 0, FixVector3.Zero, 0, 0, presentationProfileId, usage);
            var hit = new HitResolveResult(new CombatEntityId(1), new CombatEntityId(targetId), 2, 3, 4,
                new CombatFrame(12), HitResolveKind.Damage, 20, 0, FixVector3.Zero, FixVector3.Zero,
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
            return new HitPresentationEvent(new DamageResolvedEvent(request, profile, hit, 17), presentationProfileId);
        }
    }
}
