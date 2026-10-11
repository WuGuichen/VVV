using System;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Projectiles
{
    public sealed class CombatProjectileConfigJsonImporterTests
    {
        [Test]
        public void Import_DefinitionsJsonCreatesRegistryWithStableLookup()
        {
            CombatProjectileDefinitionRegistry registry = CombatProjectileConfigJsonImporter.Import(
                FireballDefinitionsJson(),
                "memory://projectile_definitions.json");

            Assert.AreEqual(1, registry.Count);
            Assert.IsTrue(registry.TryGetByStableId(
                CharacterTestCharacterActionConfigSource.FireballProjectileRequestLabel,
                out CombatProjectileDefinition definition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.DefinitionId, definition.DefinitionId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballProjectileRequestLabel,
                definition.StableId);
            Assert.AreEqual(CombatProjectileMotionKind.Linear, definition.Motion.Kind);
            Assert.IsFalse(definition.PhysicalEventsEnabled);
            Assert.AreEqual(CombatProjectileSpeedProfileKind.Constant, definition.Motion.SpeedProfileKind);
            Assert.AreEqual(Fix64.FromRatio(35, 100), definition.Motion.SpeedPerFrame);
            Assert.AreEqual(90, definition.Motion.LifetimeFrames);
            Assert.AreEqual(Fix64.Zero, definition.Motion.ArcHeight);
            Assert.AreEqual(Fix64.FromRatio(25, 100), definition.Collision.Radius);
            Assert.AreEqual(CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer), definition.Collision.LayerMask);
            Assert.IsTrue(definition.Collision.StopOnFirstAcceptedImpact);
            Assert.IsTrue(definition.Collision.StopOnBlockedImpact);
            Assert.AreEqual(1, definition.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(CombatProjectileHitSameTargetPolicy.AllowRepeat, definition.Collision.HitSameTargetPolicy);
            Assert.AreEqual(0, definition.Impact.PresentationProfileId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballProjectileImpactStableEventId,
                definition.Impact.StableEventId);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.DirectHit, definition.Impact.PolicyKind);
            Assert.AreEqual(Fix64.Zero, definition.Impact.AreaRadius);
            Assert.AreEqual(new CombatPhysicsLayerMask(0), definition.Impact.AreaLayerMask);
            Assert.IsFalse(definition.Impact.TriggerOnExpire);
            Assert.AreEqual(CombatHitTargetTypes.HostileCharacter, definition.Impact.HitEvent.TargetTypes);
            Assert.AreEqual(CombatEnvironmentHitPolicy.None, definition.Impact.HitEvent.EnvironmentHitPolicy);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballProjectileImpactStableEventId,
                definition.Impact.HitEvent.StableEventId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id3, definition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(10, definition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(8, definition.Impact.HitEvent.Damage.ImpactPower);
            Assert.AreEqual(25, definition.Impact.HitEvent.Damage.LaunchPower);
            CollectionAssert.AreEqual(
                new[] { CharacterTestAuthoredContentIds.D410_Id3, CharacterTestAuthoredContentIds.D410_Id4, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id6 },
                definition.Impact.HitEvent.Damage.Tags);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id2, definition.Impact.HitEvent.Damage.AnalysisCategory);
        }

        [Test]
        public void Import_HitEventAndAreaHitEventPreserveCommonCombatHitConfig()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    {
      ""definitionId"": 5,
      ""stableId"": ""projectile.hit-event"",
      ""physicalEventsEnabled"": true,
      ""motion"": { ""speedPerFrame"": 1, ""lifetimeFrames"": 30 },
      ""collision"": { ""radius"": 0.1, ""layerMask"": 256 },
      ""impact"": {
        ""presentationProfileId"": 0,
        ""presentationProfileLabel"": ""presentation.legacy"",
        ""stableEventId"": 791001,
        ""stableEventLabel"": ""projectile.hit-event.impact"",
        ""policyKind"": ""AreaPulseOnImpact"",
        ""areaRadius"": 2,
        ""areaLayerMask"": 256,
          ""hitEvent"": {
          ""targetTypes"": [""HostileCharacter"", ""WorldWall""],
          ""environmentHitPolicy"": ""BlockedPresentation"",
          ""stableEventId"": 791002,
          ""stableEventLabel"": ""projectile.hit-event.direct"",
          ""maxAcceptedHits"": 2,
          ""hitSameTargetPolicy"": ""OncePerEvent"",
          ""damage"": {
            ""damageTypeId"": 3,
            ""damageTypeLabel"": ""fire"",
            ""baseDamage"": 11,
            ""impactPower"": 7,
            ""launchPower"": 40,
            ""tags"": [41, 8],
            ""tagLabels"": [""projectile"", ""fire""],
            ""analysisCategory"": 791004,
            ""analysisCategoryLabel"": ""ImporterTest""
          },
          ""onHitBuffs"": [
            { ""buffId"": 601009 }
          ]
        },
        ""areaHitEvent"": {
          ""targetTypes"": [""HostileCharacter"", ""DamageableObject""],
          ""stableEventId"": 791003,
          ""stableEventLabel"": ""projectile.hit-event.area"",
          ""onHitBuffs"": [
            { ""buffId"": 601010 }
          ]
        }
      }
    }
  ]
}";

            CombatProjectileDefinitionRegistry registry = CombatProjectileConfigJsonImporter.Import(
                json,
                "memory://hit-event-projectile.json");

            Assert.IsTrue(registry.TryGetByStableId("projectile.hit-event", out CombatProjectileDefinition definition));
            Assert.IsTrue(definition.PhysicalEventsEnabled);
            Assert.AreEqual(
                CombatHitTargetTypes.HostileCharacter | CombatHitTargetTypes.WorldWall,
                definition.Impact.HitEvent.TargetTypes);
            Assert.AreEqual(CombatEnvironmentHitPolicy.BlockedPresentation, definition.Impact.HitEvent.EnvironmentHitPolicy);
            Assert.AreEqual(791002, definition.Impact.HitEvent.StableEventId);
            Assert.AreEqual(2, definition.Impact.HitEvent.MaxAcceptedHits);
            Assert.AreEqual(CombatHitSameTargetPolicy.OncePerEvent, definition.Impact.HitEvent.HitSameTargetPolicy);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id3, definition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(11, definition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(7, definition.Impact.HitEvent.Damage.ImpactPower);
            Assert.AreEqual(40, definition.Impact.HitEvent.Damage.LaunchPower);
            CollectionAssert.AreEqual(new[] { CharacterTestAuthoredContentIds.D410_Id8, CharacterTestAuthoredContentIds.D410_Id41 }, definition.Impact.HitEvent.Damage.Tags);
            Assert.AreEqual(791004, definition.Impact.HitEvent.Damage.AnalysisCategory);
            Assert.AreEqual(1, definition.Impact.HitEvent.OnHitBuffCount);
            Assert.AreEqual(601009, definition.Impact.HitEvent.OnHitBuffs[0].BuffId);
            Assert.AreEqual(
                CombatHitTargetTypes.HostileCharacter | CombatHitTargetTypes.DamageableObject,
                definition.Impact.AreaHitEvent.TargetTypes);
            Assert.AreEqual(791003, definition.Impact.AreaHitEvent.StableEventId);
            Assert.AreEqual(definition.Impact.HitEvent.Damage, definition.Impact.AreaHitEvent.Damage);
            Assert.AreEqual(1, definition.Impact.AreaHitEvent.OnHitBuffCount);
            Assert.AreEqual(601010, definition.Impact.AreaHitEvent.OnHitBuffs[0].BuffId);
        }

        [Test]
        public void Import_AreaPulsePolicyPreservesAreaConfig()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    {
      ""definitionId"": 2,
      ""stableId"": ""projectile.area"",
      ""motion"": { ""speedPerFrame"": 1, ""lifetimeFrames"": 30 },
      ""collision"": { ""radius"": 0.1, ""layerMask"": 256 },
      ""impact"": {
        ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } },
        ""presentationProfileId"": 0,
        ""presentationProfileLabel"": ""presentation.area"",
        ""stableEventId"": 791005,
        ""stableEventLabel"": ""projectile.area.impact"",
        ""policyKind"": ""AreaPulseOnImpact"",
        ""areaRadius"": 2.0,
        ""areaLayerMask"": 256,
        ""triggerOnExpire"": true
      }
    }
  ]
}";

            CombatProjectileDefinitionRegistry registry = CombatProjectileConfigJsonImporter.Import(
                json,
                "memory://area-projectile.json");

            Assert.IsTrue(registry.TryGetByStableId("projectile.area", out CombatProjectileDefinition definition));
            Assert.AreEqual(CombatProjectileImpactPolicyKind.AreaPulseOnImpact, definition.Impact.PolicyKind);
            Assert.AreEqual(Fix64.FromInt(2), definition.Impact.AreaRadius);
            Assert.AreEqual(new CombatPhysicsLayerMask(256), definition.Impact.AreaLayerMask);
            Assert.IsTrue(definition.Impact.TriggerOnExpire);
        }

        [Test]
        public void Import_PiercePolicyPreservesHitLimitAndSameTargetPolicy()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    {
      ""definitionId"": 3,
      ""stableId"": ""projectile.pierce"",
      ""motion"": { ""speedPerFrame"": 1, ""lifetimeFrames"": 30 },
      ""collision"": {
        ""radius"": 0.1,
        ""layerMask"": 256,
        ""stopOnFirstAcceptedImpact"": false,
        ""stopOnBlockedImpact"": true,
        ""maxAcceptedImpacts"": 3,
        ""hitSameTargetPolicy"": ""OncePerProjectile""
      },
      ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } }
    }
  ]
}";

            CombatProjectileDefinitionRegistry registry = CombatProjectileConfigJsonImporter.Import(
                json,
                "memory://pierce-projectile.json");

            Assert.IsTrue(registry.TryGetByStableId("projectile.pierce", out CombatProjectileDefinition definition));
            Assert.IsFalse(definition.Collision.StopOnFirstAcceptedImpact);
            Assert.IsTrue(definition.Collision.StopOnBlockedImpact);
            Assert.AreEqual(3, definition.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(CombatProjectileHitSameTargetPolicy.OncePerProjectile, definition.Collision.HitSameTargetPolicy);
        }

        [Test]
        public void Import_GeneratedBeamLanceConfigPreservesOneFrameSweepAndBindings()
        {
            CombatProjectileDefinitionRegistry definitions =
                CombatProjectileConfigJsonImporter.ImportFile(
                    CharacterTestFireballProjectileAdapter.DefaultGeneratedDefinitionsPath);

            Assert.IsTrue(definitions.TryGetByStableId(
                CharacterTestFireballProjectileAdapter.BeamLanceStableId,
                out CombatProjectileDefinition definition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.BeamLanceDefinitionId, definition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.Linear, definition.Motion.Kind);
            Assert.AreEqual(Fix64.FromInt(44), definition.Motion.SpeedPerFrame);
            Assert.AreEqual(1, definition.Motion.LifetimeFrames);
            Assert.AreEqual(Fix64.FromRatio(20, 100), definition.Collision.Radius);
            Assert.AreEqual(new CombatPhysicsLayerMask(256), definition.Collision.LayerMask);
            Assert.IsFalse(definition.Collision.StopOnFirstAcceptedImpact);
            Assert.IsTrue(definition.Collision.StopOnBlockedImpact);
            Assert.AreEqual(5, definition.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(
                CombatProjectileHitSameTargetPolicy.OncePerProjectile,
                definition.Collision.HitSameTargetPolicy);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.DirectHit, definition.Impact.PolicyKind);
            Assert.AreEqual(5, definition.Impact.HitEvent.MaxAcceptedHits);
            Assert.AreEqual(
                CombatHitSameTargetPolicy.OncePerEvent,
                definition.Impact.HitEvent.HitSameTargetPolicy);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id9, definition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(8, definition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(4, definition.Impact.HitEvent.Damage.ImpactPower);
            CollectionAssert.AreEqual(
                new[] { CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id6, CharacterTestAuthoredContentIds.D410_Id10, CharacterTestAuthoredContentIds.D410_Id13, CharacterTestAuthoredContentIds.D410_Id14 },
                definition.Impact.HitEvent.Damage.Tags);
            Assert.AreEqual(1, definition.Impact.HitEvent.OnHitBuffCount);
            Assert.AreEqual(
                CharacterTestStasisIds.StasisBuffId,
                definition.Impact.HitEvent.OnHitBuffs[0].BuffId);

            CombatProjectileSpawnProfileRegistry spawnProfiles =
                CombatProjectileSpawnProfileConfigJsonImporter.ImportFile(
                    CharacterTestFireballProjectileAdapter.DefaultGeneratedSpawnProfilesPath);
            Assert.IsTrue(spawnProfiles.TryGet(40,
                out CombatProjectileSpawnProfile spawnProfile));
            Assert.AreEqual(CombatProjectileAimKind.LockedTargetOrCasterForwardRange, spawnProfile.Aim.Kind);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, spawnProfile.Aim.FallbackKind);
            Assert.AreEqual(Fix64.FromInt(44), spawnProfile.Aim.MaxRange);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id12, spawnProfile.Origin.SocketId);

            CombatProjectileUnityPresentationRegistry presentation =
                CharacterTestProjectilePresentationSource.LoadGeneratedBindingsOrEmpty(
                    CharacterTestProjectilePresentationSource.DefaultGeneratedBindingsPath);
            Assert.IsTrue(presentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.BeamLanceDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 binding));
            Assert.AreEqual(CharacterTestVfxIds.BeamLanceBeamResourceKey, binding.FlightBodyResource);
            Assert.AreEqual(CharacterTestProjectilePresentationSource.FireballViewNamePrefix, binding.ViewNamePrefix);
        }

        [Test]
        public void Import_GeneratedLaserLanceConfigPreservesPointToPointSingleTargetBindings()
        {
            CombatProjectileDefinitionRegistry definitions =
                CombatProjectileConfigJsonImporter.ImportFile(
                    CharacterTestFireballProjectileAdapter.DefaultGeneratedDefinitionsPath);

            Assert.IsTrue(definitions.TryGetByStableId(
                CharacterTestFireballProjectileAdapter.LaserLanceStableId,
                out CombatProjectileDefinition definition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.LaserLanceDefinitionId, definition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.Linear, definition.Motion.Kind);
            Assert.AreEqual(Fix64.FromInt(44), definition.Motion.SpeedPerFrame);
            Assert.AreEqual(1, definition.Motion.LifetimeFrames);
            Assert.AreEqual(Fix64.FromRatio(6, 100), definition.Collision.Radius);
            Assert.IsTrue(definition.Collision.StopOnFirstAcceptedImpact);
            Assert.AreEqual(1, definition.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(1, definition.Impact.HitEvent.MaxAcceptedHits);
            Assert.AreEqual(0, definition.Impact.HitEvent.OnHitBuffCount);
            CollectionAssert.AreEqual(
                new[] { CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id6, CharacterTestAuthoredContentIds.D410_Id14, CharacterTestAuthoredContentIds.D410_Id16, CharacterTestAuthoredContentIds.D410_Id18 },
                definition.Impact.HitEvent.Damage.Tags);

            CombatProjectileUnityPresentationRegistry presentation =
                CharacterTestProjectilePresentationSource.LoadGeneratedBindingsOrEmpty(
                    CharacterTestProjectilePresentationSource.DefaultGeneratedBindingsPath);
            Assert.IsTrue(presentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.LaserLanceDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 binding));
            Assert.AreEqual(CharacterTestVfxIds.LaserLancePathResourceKey, binding.FlightBodyResource);
        }

        [Test]
        public void Import_GeneratedChannelBeamConfigPreservesTickSweepAndNoBuff()
        {
            CombatProjectileDefinitionRegistry definitions =
                CombatProjectileConfigJsonImporter.ImportFile(
                    CharacterTestFireballProjectileAdapter.DefaultGeneratedDefinitionsPath);

            Assert.IsTrue(definitions.TryGetByStableId(
                CharacterTestFireballProjectileAdapter.ChannelBeamStableId,
                out CombatProjectileDefinition definition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.ChannelBeamDefinitionId, definition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.Linear, definition.Motion.Kind);
            Assert.AreEqual(Fix64.FromInt(32), definition.Motion.SpeedPerFrame);
            Assert.AreEqual(1, definition.Motion.LifetimeFrames);
            Assert.AreEqual(Fix64.FromRatio(18, 100), definition.Collision.Radius);
            Assert.AreEqual(new CombatPhysicsLayerMask(256), definition.Collision.LayerMask);
            Assert.IsFalse(definition.Collision.StopOnFirstAcceptedImpact);
            Assert.IsTrue(definition.Collision.StopOnBlockedImpact);
            Assert.AreEqual(3, definition.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(
                CombatProjectileHitSameTargetPolicy.OncePerProjectile,
                definition.Collision.HitSameTargetPolicy);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.DirectHit, definition.Impact.PolicyKind);
            Assert.AreEqual(3, definition.Impact.HitEvent.MaxAcceptedHits);
            Assert.AreEqual(
                CombatHitSameTargetPolicy.OncePerEvent,
                definition.Impact.HitEvent.HitSameTargetPolicy);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id9, definition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(2, definition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(1, definition.Impact.HitEvent.Damage.ImpactPower);
            CollectionAssert.AreEqual(
                new[] { CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id14, CharacterTestAuthoredContentIds.D410_Id15, CharacterTestAuthoredContentIds.D410_Id16, CharacterTestAuthoredContentIds.D410_Id17 },
                definition.Impact.HitEvent.Damage.Tags);
            Assert.AreEqual(0, definition.Impact.HitEvent.OnHitBuffCount);

            CombatProjectileSpawnProfileRegistry spawnProfiles =
                CombatProjectileSpawnProfileConfigJsonImporter.ImportFile(
                    CharacterTestFireballProjectileAdapter.DefaultGeneratedSpawnProfilesPath);
            Assert.IsTrue(spawnProfiles.TryGet(42,
                out CombatProjectileSpawnProfile spawnProfile));
            Assert.AreEqual(CombatProjectileAimKind.LockedTargetOrCasterForwardRange, spawnProfile.Aim.Kind);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, spawnProfile.Aim.FallbackKind);
            Assert.AreEqual(Fix64.FromInt(32), spawnProfile.Aim.MaxRange);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id12, spawnProfile.Origin.SocketId);

            CombatProjectileUnityPresentationRegistry presentation =
                CharacterTestProjectilePresentationSource.LoadGeneratedBindingsOrEmpty(
                    CharacterTestProjectilePresentationSource.DefaultGeneratedBindingsPath);
            Assert.IsTrue(presentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.ChannelBeamDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 binding));
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamBeamResourceKey, binding.FlightBodyResource);
            Assert.AreEqual(CharacterTestProjectilePresentationSource.FireballViewNamePrefix, binding.ViewNamePrefix);
        }

        [Test]
        public void Import_GeneratedPoisonBottleConfigPreservesArcBottlePoolRules()
        {
            CombatProjectileDefinitionRegistry definitions =
                CombatProjectileConfigJsonImporter.ImportFile(
                    CharacterTestFireballProjectileAdapter.DefaultGeneratedDefinitionsPath);

            Assert.IsTrue(definitions.TryGetByStableId(
                CharacterTestFireballProjectileAdapter.PoisonBottleStableId,
                out CombatProjectileDefinition definition));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.PoisonBottleDefinitionId, definition.DefinitionId);
            Assert.AreEqual(CombatProjectileMotionKind.BallisticArcToTarget, definition.Motion.Kind);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.PoisonBottleSpeedPerFrame, definition.Motion.SpeedPerFrame);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.PoisonBottleLifetimeFrames, definition.Motion.LifetimeFrames);
            Assert.AreEqual(Fix64.Zero, definition.Motion.ArcHeight);
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.PoisonBottleGravityPerFrameSquared,
                definition.Motion.GravityPerFrameSquared);
            Assert.AreEqual(Fix64.Zero, definition.Motion.MaxLaunchAngleTangent);
            Assert.AreEqual(Fix64.FromRatio(20, 100), definition.Collision.Radius);
            Assert.AreEqual(new CombatPhysicsLayerMask(256), definition.Collision.LayerMask);
            Assert.IsTrue(definition.Collision.StopOnFirstAcceptedImpact);
            Assert.IsTrue(definition.Collision.StopOnBlockedImpact);
            Assert.AreEqual(1, definition.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(
                CombatProjectileHitSameTargetPolicy.OncePerProjectile,
                definition.Collision.HitSameTargetPolicy);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.DirectHit, definition.Impact.PolicyKind);
            Assert.AreEqual(1, definition.Impact.HitEvent.MaxAcceptedHits);
            Assert.AreEqual(
                CombatHitSameTargetPolicy.OncePerEvent,
                definition.Impact.HitEvent.HitSameTargetPolicy);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id10, definition.Impact.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(0, definition.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(0, definition.Impact.HitEvent.Damage.ImpactPower);
            CollectionAssert.AreEqual(
                new[] { CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id21, CharacterTestAuthoredContentIds.D410_Id22, CharacterTestAuthoredContentIds.D410_Id23, CharacterTestAuthoredContentIds.D410_Id24 },
                definition.Impact.HitEvent.Damage.Tags);
            Assert.AreEqual(0, definition.Impact.HitEvent.OnHitBuffCount);

            CombatProjectileSpawnProfileRegistry spawnProfiles =
                CombatProjectileSpawnProfileConfigJsonImporter.ImportFile(
                    CharacterTestFireballProjectileAdapter.DefaultGeneratedSpawnProfilesPath);
            Assert.IsTrue(spawnProfiles.TryGet(46,
                out CombatProjectileSpawnProfile spawnProfile));
            Assert.AreEqual(CombatProjectileAimKind.LockedTargetOrCasterForwardRange, spawnProfile.Aim.Kind);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, spawnProfile.Aim.FallbackKind);
            Assert.AreEqual(Fix64.Zero, spawnProfile.Aim.MaxRange);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id12, spawnProfile.Origin.SocketId);

            CombatProjectileUnityPresentationRegistry presentation =
                CharacterTestProjectilePresentationSource.LoadGeneratedBindingsOrEmpty(
                    CharacterTestProjectilePresentationSource.DefaultGeneratedBindingsPath);
            Assert.IsTrue(presentation.TryGetV2(
                CharacterTestFireballProjectileAdapter.PoisonBottleDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 binding));
            Assert.AreEqual(CharacterTestVfxIds.PoisonBottleFlightResourceKey, binding.FlightBodyResource);
            Assert.AreEqual(CharacterTestProjectilePresentationSource.FireballViewNamePrefix, binding.ViewNamePrefix);
        }

        [Test]
        public void Import_ArcMotionPreservesConfig()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    {
      ""definitionId"": 4,
      ""stableId"": ""projectile.arc"",
      ""motion"": { ""kind"": ""ArcToTarget"", ""speedPerFrame"": 0.35, ""lifetimeFrames"": 90, ""arcHeight"": 4 },
      ""collision"": { ""radius"": 0.25, ""layerMask"": 256 },
      ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } }
    }
  ]
}";

            CombatProjectileDefinitionRegistry registry = CombatProjectileConfigJsonImporter.Import(
                json,
                "memory://arc-projectile.json");

            Assert.IsTrue(registry.TryGetByStableId("projectile.arc", out CombatProjectileDefinition definition));
            Assert.AreEqual(CombatProjectileMotionKind.ArcToTarget, definition.Motion.Kind);
            Assert.AreEqual(CombatProjectileSpeedProfileKind.Constant, definition.Motion.SpeedProfileKind);
            Assert.AreEqual(Fix64.FromRatio(35, 100), definition.Motion.SpeedPerFrame);
            Assert.AreEqual(90, definition.Motion.LifetimeFrames);
            Assert.AreEqual(Fix64.FromInt(4), definition.Motion.ArcHeight);
            Assert.AreEqual(Fix64.Zero, definition.Motion.GravityPerFrameSquared);
        }

        [Test]
        public void Import_BallisticArcMotionPreservesGravityConfig()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    {
      ""definitionId"": 5,
      ""stableId"": ""projectile.ballistic"",
      ""motion"": {
        ""kind"": ""BallisticArcToTarget"",
        ""speedPerFrame"": 0.35,
        ""lifetimeFrames"": 90,
        ""gravityPerFrameSquared"": 0.008333333
      },
      ""collision"": { ""radius"": 0.25, ""layerMask"": 256 },
      ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } }
    }
  ]
}";

            CombatProjectileDefinitionRegistry registry = CombatProjectileConfigJsonImporter.Import(
                json,
                "memory://ballistic-projectile.json");

            Assert.IsTrue(registry.TryGetByStableId("projectile.ballistic", out CombatProjectileDefinition definition));
            Assert.AreEqual(CombatProjectileMotionKind.BallisticArcToTarget, definition.Motion.Kind);
            Assert.AreEqual(CombatProjectileSpeedProfileKind.Constant, definition.Motion.SpeedProfileKind);
            Assert.AreEqual(Fix64.FromRatio(35, 100), definition.Motion.SpeedPerFrame);
            Assert.AreEqual(90, definition.Motion.LifetimeFrames);
            Assert.AreEqual(Fix64.Zero, definition.Motion.ArcHeight);
            Assert.AreEqual(Fix64.FromRatio(8333333, 1000000000), definition.Motion.GravityPerFrameSquared);
            Assert.AreEqual(Fix64.Zero, definition.Motion.MaxLaunchAngleTangent);
        }

        [Test]
        public void Import_BallisticArcMotionPreservesMaxLaunchAngleConfig()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    {
      ""definitionId"": 55,
      ""stableId"": ""projectile.ballistic-angle"",
      ""motion"": {
        ""kind"": ""BallisticArcToTarget"",
        ""speedPerFrame"": 1,
        ""lifetimeFrames"": 90,
        ""gravityPerFrameSquared"": 0.1,
        ""maxLaunchAngleDegrees"": 30
      },
      ""collision"": { ""radius"": 0.25, ""layerMask"": 256 },
      ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } }
    }
  ]
}";

            CombatProjectileDefinitionRegistry registry = CombatProjectileConfigJsonImporter.Import(
                json,
                "memory://ballistic-projectile-angle.json");

            Assert.IsTrue(registry.TryGetByStableId("projectile.ballistic-angle", out CombatProjectileDefinition definition));
            Assert.AreEqual(CombatProjectileMotionKind.BallisticArcToTarget, definition.Motion.Kind);
            Assert.AreEqual(Fix64.FromRatio(577350, 1000000), definition.Motion.MaxLaunchAngleTangent);
        }

        [Test]
        public void Import_SpeedProfilePreservesConfig()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    {
      ""definitionId"": 6,
      ""stableId"": ""projectile.ease-in"",
      ""motion"": {
        ""kind"": ""Linear"",
        ""speedPerFrame"": 0.35,
        ""lifetimeFrames"": 90,
        ""speedProfile"": { ""kind"": ""EaseInQuadratic"" }
      },
      ""collision"": { ""radius"": 0.25, ""layerMask"": 256 },
      ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } }
    }
  ]
}";

            CombatProjectileDefinitionRegistry registry = CombatProjectileConfigJsonImporter.Import(
                json,
                "memory://speed-profile-projectile.json");

            Assert.IsTrue(registry.TryGetByStableId("projectile.ease-in", out CombatProjectileDefinition definition));
            Assert.AreEqual(CombatProjectileSpeedProfileKind.EaseInQuadratic, definition.Motion.SpeedProfileKind);
        }

        [Test]
        public void Import_BoomerangSwayHomingAndBezierMotionPreserveConfig()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    {
      ""definitionId"": 7,
      ""stableId"": ""projectile.return"",
      ""motion"": { ""kind"": ""BoomerangReturn"", ""speedPerFrame"": 0.35, ""lifetimeFrames"": 90 },
      ""collision"": { ""radius"": 0.25, ""layerMask"": 256 },
      ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } }
    },
    {
      ""definitionId"": 8,
      ""stableId"": ""projectile.sway"",
      ""motion"": {
        ""kind"": ""SwayForward"",
        ""speedPerFrame"": 0.35,
        ""lifetimeFrames"": 90,
        ""lateralAmplitude"": 0.75,
        ""lateralCycles"": 2
      },
      ""collision"": { ""radius"": 0.25, ""layerMask"": 256 },
      ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } }
    },
    {
      ""definitionId"": 9,
      ""stableId"": ""projectile.homing"",
      ""motion"": {
        ""kind"": ""HomingToTarget"",
        ""speedPerFrame"": 0.35,
        ""lifetimeFrames"": 90,
        ""homing"": { ""turnRatePerFrame"": 0.25 }
      },
      ""collision"": { ""radius"": 0.25, ""layerMask"": 256 },
      ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } }
    },
    {
      ""definitionId"": 10,
      ""stableId"": ""projectile.bezier"",
      ""motion"": {
        ""kind"": ""BezierToTarget"",
        ""speedPerFrame"": 0.35,
        ""lifetimeFrames"": 90,
        ""bezierControlOffsetA"": { ""x"": 0, ""y"": 3, ""z"": 6 },
        ""bezierControlOffsetB"": { ""x"": 0, ""y"": 2, ""z"": -4 }
      },
      ""collision"": { ""radius"": 0.25, ""layerMask"": 256 },
      ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } }
    }
  ]
}";

            CombatProjectileDefinitionRegistry registry = CombatProjectileConfigJsonImporter.Import(
                json,
                "memory://return-sway-projectiles.json");

            Assert.IsTrue(registry.TryGetByStableId("projectile.return", out CombatProjectileDefinition returnDefinition));
            Assert.AreEqual(CombatProjectileMotionKind.BoomerangReturn, returnDefinition.Motion.Kind);
            Assert.AreEqual(Fix64.Zero, returnDefinition.Motion.LateralAmplitude);
            Assert.AreEqual(0, returnDefinition.Motion.LateralCycles);
            Assert.IsTrue(registry.TryGetByStableId("projectile.sway", out CombatProjectileDefinition swayDefinition));
            Assert.AreEqual(CombatProjectileMotionKind.SwayForward, swayDefinition.Motion.Kind);
            Assert.AreEqual(Fix64.FromRatio(75, 100), swayDefinition.Motion.LateralAmplitude);
            Assert.AreEqual(2, swayDefinition.Motion.LateralCycles);
            Assert.IsTrue(registry.TryGetByStableId("projectile.homing", out CombatProjectileDefinition homingDefinition));
            Assert.AreEqual(CombatProjectileMotionKind.HomingToTarget, homingDefinition.Motion.Kind);
            Assert.AreEqual(Fix64.FromRatio(25, 100), homingDefinition.Motion.HomingTurnRatePerFrame);
            Assert.IsTrue(registry.TryGetByStableId("projectile.bezier", out CombatProjectileDefinition bezierDefinition));
            Assert.AreEqual(CombatProjectileMotionKind.BezierToTarget, bezierDefinition.Motion.Kind);
            Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.FromInt(3), Fix64.FromInt(6)), bezierDefinition.Motion.BezierControlOffsetA);
            Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero - Fix64.FromInt(4)), bezierDefinition.Motion.BezierControlOffsetB);
        }

        [Test]
        public void Import_InvalidSwayConfigThrowsWithSourcePath()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    {
      ""definitionId"": 9,
      ""stableId"": ""projectile.bad-sway"",
      ""motion"": {
        ""kind"": ""SwayForward"",
        ""speedPerFrame"": 0.35,
        ""lifetimeFrames"": 90,
        ""lateralAmplitude"": 0,
        ""lateralCycles"": 0
      },
      ""collision"": { ""radius"": 0.25, ""layerMask"": 256 },
      ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } }
    }
  ]
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileConfigJsonImporter.Import(json, "memory://bad-sway-projectile.json"));

            StringAssert.Contains("memory://bad-sway-projectile.json", ex.Message);
            StringAssert.Contains("Sway projectile lateralAmplitude must be positive", ex.Message);
        }

        [Test]
        public void Import_InvalidBezierConfigThrowsWithSourcePath()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    {
      ""definitionId"": 11,
      ""stableId"": ""projectile.bad-bezier"",
      ""motion"": {
        ""kind"": ""BezierToTarget"",
        ""speedPerFrame"": 0.35,
        ""lifetimeFrames"": 90
      },
      ""collision"": { ""radius"": 0.25, ""layerMask"": 256 },
      ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } }
    }
  ]
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileConfigJsonImporter.Import(json, "memory://bad-bezier-projectile.json"));

            StringAssert.Contains("memory://bad-bezier-projectile.json", ex.Message);
            StringAssert.Contains("Bezier projectile requires at least one non-zero control offset", ex.Message);
        }

        [Test]
        public void Import_EquivalentFireballFixtureMatchesEmbeddedFallbackHash()
        {
            CombatProjectileDefinitionRegistry generated = CombatProjectileConfigJsonImporter.Import(
                FireballDefinitionsJson(),
                "memory://projectile_definitions.json");
            CombatProjectileDefinitionRegistry fallback =
                CharacterTestFireballProjectileAdapter.CreateEmbeddedFallbackRegistry();
            Assert.IsTrue(fallback.TryGet(
                CharacterTestFireballProjectileAdapter.DefinitionId,
                out CombatProjectileDefinition fireballFallback));
            var expected = new CombatProjectileDefinitionRegistry(new[] { fireballFallback });

            Assert.AreEqual(expected.ComputeStableHash(), generated.ComputeStableHash());
        }

        [Test]
        public void Import_DuplicateDefinitionIdThrowsWithSourcePath()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    { ""definitionId"": 1, ""stableId"": ""projectile.a"", ""motion"": { ""speedPerFrame"": 1, ""lifetimeFrames"": 1 }, ""collision"": { ""radius"": 0.1, ""layerMask"": 1 }, ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } } },
    { ""definitionId"": 1, ""stableId"": ""projectile.b"", ""motion"": { ""speedPerFrame"": 1, ""lifetimeFrames"": 1 }, ""collision"": { ""radius"": 0.1, ""layerMask"": 1 }, ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } } }
  ]
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileConfigJsonImporter.Import(json, "memory://duplicate-id.json"));

            StringAssert.Contains("memory://duplicate-id.json", ex.Message);
            StringAssert.Contains("Duplicate projectile definition id", ex.Message);
        }

        [Test]
        public void Import_DuplicateStableIdThrowsWithSourcePath()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    { ""definitionId"": 1, ""stableId"": ""projectile.same"", ""motion"": { ""speedPerFrame"": 1, ""lifetimeFrames"": 1 }, ""collision"": { ""radius"": 0.1, ""layerMask"": 1 }, ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } } },
    { ""definitionId"": 2, ""stableId"": ""projectile.same"", ""motion"": { ""speedPerFrame"": 1, ""lifetimeFrames"": 1 }, ""collision"": { ""radius"": 0.1, ""layerMask"": 1 }, ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } } }
  ]
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileConfigJsonImporter.Import(json, "memory://duplicate-stable.json"));

            StringAssert.Contains("memory://duplicate-stable.json", ex.Message);
            StringAssert.Contains("Duplicate projectile stable id", ex.Message);
        }

        [Test]
        public void Import_InvalidPolicyThrowsWithSourcePath()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    { ""definitionId"": 1, ""stableId"": ""projectile.bad-policy"", ""motion"": { ""speedPerFrame"": 1, ""lifetimeFrames"": 1 }, ""collision"": { ""radius"": 0.1, ""layerMask"": 1 }, ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } }, ""policyKind"": ""ExplodeEverywhere"" } }
  ]
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileConfigJsonImporter.Import(json, "memory://bad-policy.json"));

            StringAssert.Contains("memory://bad-policy.json", ex.Message);
            StringAssert.Contains("Unsupported projectile impact policy kind", ex.Message);
        }

        [Test]
        public void Import_InvalidHitSameTargetPolicyThrowsWithSourcePath()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    { ""definitionId"": 1, ""stableId"": ""projectile.bad-hit-policy"", ""motion"": { ""speedPerFrame"": 1, ""lifetimeFrames"": 1 }, ""collision"": { ""radius"": 0.1, ""layerMask"": 1, ""hitSameTargetPolicy"": ""EveryOtherFrame"" }, ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } } }
  ]
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileConfigJsonImporter.Import(json, "memory://bad-hit-policy.json"));

            StringAssert.Contains("memory://bad-hit-policy.json", ex.Message);
            StringAssert.Contains("Unsupported projectile hit same target policy", ex.Message);
        }

        [Test]
        public void Import_DuplicateOnHitBuffIdThrowsWithSourcePath()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    {
      ""definitionId"": 1,
      ""stableId"": ""projectile.duplicate-on-hit-buff"",
      ""motion"": { ""speedPerFrame"": 1, ""lifetimeFrames"": 1 },
      ""collision"": { ""radius"": 0.1, ""layerMask"": 1 },
      ""impact"": {
        ""stableEventId"": 18,
        ""stableEventLabel"": ""character_test.fireball.projectile.impact"",
        ""hitEvent"": {
          ""damage"": {
            ""damageTypeId"": 2,
            ""damageTypeLabel"": ""force"",
            ""baseDamage"": 1,
            ""impactPower"": 1
          },
          ""onHitBuffs"": [
            { ""buffId"": 601009 },
            { ""buffId"": 601009 }
          ]
        }
      }
    }
  ]
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileConfigJsonImporter.Import(json, "memory://duplicate-on-hit-buff.json"));

            StringAssert.Contains("memory://duplicate-on-hit-buff.json", ex.Message);
            StringAssert.Contains("onHitBuffs cannot repeat the same buffId", ex.Message);
        }

        [Test]
        public void Import_InvalidMotionKindThrowsWithSourcePath()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    { ""definitionId"": 1, ""stableId"": ""projectile.bad-motion"", ""motion"": { ""kind"": ""Spline"", ""speedPerFrame"": 1, ""lifetimeFrames"": 1 }, ""collision"": { ""radius"": 0.1, ""layerMask"": 1 }, ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } } }
  ]
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileConfigJsonImporter.Import(json, "memory://bad-motion.json"));

            StringAssert.Contains("memory://bad-motion.json", ex.Message);
            StringAssert.Contains("Unsupported projectile motion kind", ex.Message);
        }

        [Test]
        public void Import_InvalidSpeedProfileKindThrowsWithSourcePath()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    { ""definitionId"": 1, ""stableId"": ""projectile.bad-speed-profile"", ""motion"": { ""speedPerFrame"": 1, ""lifetimeFrames"": 1, ""speedProfile"": { ""kind"": ""BezierCurve"" } }, ""collision"": { ""radius"": 0.1, ""layerMask"": 1 }, ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } } }
  ]
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileConfigJsonImporter.Import(json, "memory://bad-speed-profile.json"));

            StringAssert.Contains("memory://bad-speed-profile.json", ex.Message);
            StringAssert.Contains("Unsupported projectile speed profile kind", ex.Message);
        }

        [Test]
        public void Import_InvalidArcHeightThrowsWithSourcePath()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    { ""definitionId"": 1, ""stableId"": ""projectile.bad-arc"", ""motion"": { ""kind"": ""ArcToTarget"", ""speedPerFrame"": 1, ""lifetimeFrames"": 1, ""arcHeight"": 0 }, ""collision"": { ""radius"": 0.1, ""layerMask"": 1 }, ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } } }
  ]
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileConfigJsonImporter.Import(json, "memory://bad-arc.json"));

            StringAssert.Contains("memory://bad-arc.json", ex.Message);
            StringAssert.Contains("Arc projectile height must be positive", ex.Message);
        }

        [Test]
        public void Import_InvalidBallisticGravityThrowsWithSourcePath()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    { ""definitionId"": 1, ""stableId"": ""projectile.bad-ballistic"", ""motion"": { ""kind"": ""BallisticArcToTarget"", ""speedPerFrame"": 1, ""lifetimeFrames"": 1, ""gravityPerFrameSquared"": 0 }, ""collision"": { ""radius"": 0.1, ""layerMask"": 1 }, ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } } }
  ]
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileConfigJsonImporter.Import(json, "memory://bad-ballistic.json"));

            StringAssert.Contains("memory://bad-ballistic.json", ex.Message);
            StringAssert.Contains("Ballistic projectile gravity must be positive", ex.Message);
        }

        [Test]
        public void Import_InvalidBallisticMaxLaunchAngleThrowsWithSourcePath()
        {
            string json = @"{
  ""schemaVersion"": ""5"",
  ""definitions"": [
    { ""definitionId"": 1, ""stableId"": ""projectile.bad-ballistic-angle"", ""motion"": { ""kind"": ""BallisticArcToTarget"", ""speedPerFrame"": 1, ""lifetimeFrames"": 1, ""gravityPerFrameSquared"": 0.1, ""maxLaunchAngleDegrees"": 90 }, ""collision"": { ""radius"": 0.1, ""layerMask"": 1 }, ""impact"": { ""hitEvent"": { ""damage"": { ""damageTypeId"": 2, ""damageTypeLabel"": ""force"", ""baseDamage"": 1, ""impactPower"": 1 } } } }
  ]
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileConfigJsonImporter.Import(json, "memory://bad-ballistic-angle.json"));

            StringAssert.Contains("memory://bad-ballistic-angle.json", ex.Message);
            StringAssert.Contains("maxLaunchAngleDegrees", ex.Message);
        }

        [Test]
        public void Import_LegacySchemaVersionRejectsBeforeReadingDefinitions()
        {
            const string json = @"{
  ""schemaVersion"": ""1"",
  ""definitions"": []
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileConfigJsonImporter.Import(json, "memory://legacy-projectile.json"));

            StringAssert.Contains("memory://legacy-projectile.json", ex.Message);
            StringAssert.Contains("expected 5", ex.Message);
        }


        [Test]
        public void Import_PresentationBindingsJsonPreservesResourceKeyAndViewPrefix()
        {
            CombatProjectileUnityPresentationRegistry registry =
                CombatProjectileUnityPresentationConfigJsonImporter.Import(
                    FireballPresentationBindingsJson(),
                    "memory://projectile_presentation_bindings.json");

            Assert.AreEqual(1, registry.Count);
            Assert.IsTrue(registry.TryGetV2(
                CharacterTestFireballProjectileAdapter.DefinitionId,
                out CombatProjectileUnityPresentationBindingV2 binding));
            Assert.AreEqual(
                new ResourceKey(
                    CharacterTestVfxIds.FireballResourceKey.Id,
                    CharacterTestVfxIds.FireballResourceKey.TypeId,
                    CharacterTestVfxIds.FireballResourceKey.Variant,
                    CharacterTestVfxIds.FireballResourceKey.PackageId),
                binding.FlightBodyResource);
            Assert.AreEqual(CharacterTestProjectilePresentationSource.FireballViewNamePrefix, binding.ViewNamePrefix);
        }

        [Test]
        public void Import_SpawnProfilesJsonCreatesRegistryWithSocketOriginAndForwardAim()
        {
            CombatProjectileSpawnProfileRegistry registry =
                CombatProjectileSpawnProfileConfigJsonImporter.Import(
                    FireballSpawnProfilesJson(),
                    "memory://projectile_spawn_profiles.json");

            Assert.AreEqual(1, registry.Count);
            Assert.IsTrue(registry.TryGet(1,
                out CombatProjectileSpawnProfile profile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballProjectileRequestLabel,
                profile.StableId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballProjectileRequestLabel,
                profile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileSpawnOriginKind.Socket, profile.Origin.Kind);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id12, profile.Origin.SocketId);
            Assert.AreEqual(FixVector3.Zero, profile.Origin.LocalOffset);
            Assert.AreEqual(
                new FixVector3(Fix64.Zero, Fix64.FromRatio(115, 100), Fix64.FromRatio(65, 100)),
                profile.Origin.FallbackRootOffset);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, profile.Aim.Kind);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, profile.Aim.FallbackKind);
            Assert.AreEqual(Fix64.Zero, profile.Aim.TargetHeightOffset);
            Assert.AreEqual(Fix64.Zero, profile.Aim.MaxRange);
        }

        [Test]
        public void Import_SpawnProfilesJsonParsesLockedTargetAim()
        {
            string json = @"{
  ""profiles"": [
    {
      ""profileId"": 1,
      ""stableId"": ""character_test.flame_lance.projectile"",
      ""projectileDefinitionId"": 91003,
      ""projectileStableId"": ""character_test.flame_lance.projectile"",
      ""origin"": { ""kind"": ""Socket"", ""socketId"": 12, ""socketLabel"": ""mainHand"" },
      ""aim"": {
        ""kind"": ""LockedTargetOrCasterForwardRange"",
        ""targetHeightOffset"": 0.2,
        ""fallbackKind"": ""CasterForwardRange"",
        ""maxRange"": 44
      }
    }
  ]
}";

            CombatProjectileSpawnProfileRegistry registry =
                CombatProjectileSpawnProfileConfigJsonImporter.Import(
                    json,
                    "memory://projectile_spawn_profiles.json");

            Assert.IsTrue(registry.TryGet(1,
                out CombatProjectileSpawnProfile profile));
            Assert.AreEqual(CombatProjectileAimKind.LockedTargetOrCasterForwardRange, profile.Aim.Kind);
            Assert.AreEqual(Fix64.FromRatio(2, 10), profile.Aim.TargetHeightOffset);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, profile.Aim.FallbackKind);
            Assert.AreEqual(Fix64.FromInt(44), profile.Aim.MaxRange);
        }

        [Test]
        public void Import_DuplicateSpawnProfileIdThrowsWithSourcePath()
        {
            string json = @"{
  ""profiles"": [
    { ""profileId"": 1, ""stableId"": ""projectile.spawn"", ""projectileDefinitionId"": 1, ""projectileStableId"": ""projectile.a"", ""origin"": { ""kind"": ""Socket"", ""socketId"": 12, ""socketLabel"": ""mainHand"" }, ""aim"": { ""kind"": ""CasterForwardRange"" } },
    { ""profileId"": 1, ""stableId"": ""projectile.spawn"", ""projectileDefinitionId"": 2, ""projectileStableId"": ""projectile.b"", ""origin"": { ""kind"": ""Socket"", ""socketId"": 12, ""socketLabel"": ""mainHand"" }, ""aim"": { ""kind"": ""CasterForwardRange"" } }
  ]
}";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                CombatProjectileSpawnProfileConfigJsonImporter.Import(json, "memory://duplicate-spawn.json"));

            StringAssert.Contains("memory://duplicate-spawn.json", ex.Message);
            StringAssert.Contains("Duplicate projectile spawn profile id", ex.Message);
        }

        private static string FireballDefinitionsJson()
        {
            return @"{
  ""schemaVersion"": ""5"",
  ""packageId"": ""character_test.fireball"",
  ""definitions"": [
    {
      ""definitionId"": 91001,
      ""stableId"": ""character_test.fireball.projectile"",
      ""motion"": { ""speedPerFrame"": 0.35, ""lifetimeFrames"": 90 },
      ""collision"": {
        ""radius"": 0.25,
        ""layerMask"": 1024,
        ""stopOnFirstAcceptedImpact"": true,
        ""stopOnBlockedImpact"": true
      },
      ""impact"": {
        ""hitEvent"": {
          ""targetTypes"": [""HostileCharacter""],
          ""environmentHitPolicy"": ""None"",
          ""stableEventId"": 18,
          ""stableEventLabel"": ""character_test.fireball.projectile.impact"",
          ""maxAcceptedHits"": 1,
          ""hitSameTargetPolicy"": ""AllowRepeat"",
          ""damage"": {
            ""damageTypeId"": 3,
            ""damageTypeLabel"": ""fire"",
            ""baseDamage"": 10,
            ""impactPower"": 8,
            ""launchPower"": 25,
            ""tags"": [3, 4, 5, 6],
            ""tagLabels"": [""area"", ""fireball"", ""magic"", ""scatter""],
            ""analysisCategory"": 2,
            ""analysisCategoryLabel"": ""FireballImpact""
          },
          ""onHitBuffs"": [
            { ""buffId"": 601009 }
          ]
        }
      }
    }
  ]
}";
        }

        private static string FireballPresentationBindingsJson()
        {
            return @"{
  ""schemaVersion"": ""2"",
  ""packageId"": ""character_test.fireball"",
  ""bindings"": [
    {
      ""projectileDefinitionId"": 91001,
      ""viewNamePrefix"": ""ProjectileVFX"",
      ""slots"": {
        ""flightBody"": {
          ""resource"": {
            ""id"": ""vfx.magic.fireball"",
            ""typeId"": ""GameObject"",
            ""variant"": """",
            ""packageId"": ""mxframework.samples""
          }
        }
      }
    }
  ]
}";
        }

        private static string FireballSpawnProfilesJson()
        {
            return @"{
  ""schemaVersion"": ""2"",
  ""packageId"": ""character_test.fireball"",
  ""profiles"": [
    {
      ""profileId"": 1,
      ""stableId"": ""character_test.fireball.projectile"",
      ""projectileDefinitionId"": 91001,
      ""projectileStableId"": ""character_test.fireball.projectile"",
      ""origin"": {
        ""kind"": ""Socket"",
        ""socketId"": 12,
        ""socketLabel"": ""mainHand"",
        ""localOffset"": { ""x"": 0, ""y"": 0, ""z"": 0 },
        ""fallbackRootOffset"": { ""x"": 0, ""y"": 1.15, ""z"": 0.65 }
      },
      ""aim"": { ""kind"": ""CasterForwardRange"" }
    }
  ]
}";
        }
    }
}
