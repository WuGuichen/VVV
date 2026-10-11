using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static class AbilityPackageTests
{
    public static void RunAll()
    {
        AbilityAuthoringCompiler_FireballSample_CompilesRuntimeCatalogs();
        AbilityAuthoringCompiler_Projectiles_DeriveMasksFromTargetTypes();
        AbilityAuthoringCompiler_InvalidReferences_BlockValidation();
        AbilityAuthoringPackageLoader_InvalidProjectileRequestFiles_BlockValidation();
        AbilityPackageCommands_Compile_WritesArtifacts();
        AbilityPackageCommands_ImportUnityDryRun_ReportsProjectileArtifacts();
    }

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static void AbilityAuthoringCompiler_FireballSample_CompilesRuntimeCatalogs()
    {
        string packagePath = FindSamplePath("ability-character-test");
        AbilityAuthoringDocument document = AbilityAuthoringPackageLoader.Load(packagePath, JsonOptions);

        AbilityAuthoringCompileResult result = AbilityAuthoringCompiler.Compile(
            document,
            ReadGlobalVfxDefinitions());

        Require(!result.ValidationReport.HasBlockingIssues, "Fireball ability package should validate.");
        string projectileSourceRoot = Path.Combine(packagePath, "config", "projectiles");
        Require(Directory.Exists(projectileSourceRoot), "Projectile source should be split into per-ID files.");
        string[] projectileSourceFiles = Directory.GetFiles(projectileSourceRoot, "*.json");
        Require(projectileSourceFiles.Length > 0, "Projectile per-ID source files should exist.");
        for (int i = 0; i < projectileSourceFiles.Length; i++)
        {
            string sourceText = File.ReadAllText(projectileSourceFiles[i]);
            Require(!sourceText.Contains("\"layerMask\"", StringComparison.Ordinal), "Projectile source must not contain raw collision layerMask.");
            Require(!sourceText.Contains("\"areaLayerMask\"", StringComparison.Ordinal), "Projectile source must not contain raw areaLayerMask.");
        }
        Require(result.AbilityCatalog.Abilities.Count == 28, $"CharacterTest ability catalog should compile current sample abilities. Actual: {result.AbilityCatalog.Abilities.Count}");
        AbilityDefinitionConfig ability = result.AbilityCatalog.Abilities.Single(a => a.AbilityStableId == "ability.character_test.fireball");
        Require(ability.AbilityStableId == "ability.character_test.fireball", "Fireball ability stable id should compile.");
        Require(ability.Input.InputIntentLabel == "AttackSecondary", "Fireball ability input intent should compile.");
        Require(ability.Cooldown.Frames == 60, "Fireball cooldown frames should compile.");
        Require(ability.Cooldown.GroupLabel == "character_test.fireball", "Fireball cooldown group should compile.");
        Require(ability.Costs.Count == 0, "Fireball sample should not invent a resource cost.");
        Require(ability.ActionBinding.ActionIntentLabel == "Fireball", "Fireball action intent should compile.");
        Require(ability.Damage != null, "Fireball ability damage should compile.");
        Require(ability.Damage.DamageTypeLabel == "fire", "Fireball ability damage type should compile.");
        Require(ability.Damage.BaseDamage == 10, "Fireball ability base damage should compile.");
        Require(ability.Damage.ImpactPower == 8, "Fireball ability impact power should compile.");
        Require(ability.Damage.LaunchPower == 0, "Fireball ability launch power should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.fireball"), "Fireball VFX definition should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.fireball_burst.impact"), "Fireball Burst impact VFX definition should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.fireball_explosion.impact"), "Fireball Explosion impact VFX definition should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.flame_lance.impact"), "Flame Lance impact VFX definition should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.fireball.charge"), "Fireball charge VFX definition should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.fireball_burst.charge"), "Fireball Burst charge VFX definition should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.guard_aura"), "Guard Aura VFX definition should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.area_confinement.zone"), "Area Confinement zone VFX definition should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.area_confinement.character"), "Area Confinement character VFX definition should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.area_confinement.telegraph"), "Area Confinement telegraph VFX definition should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.area_confinement.telegraph_cancel"), "Area Confinement telegraph cancel VFX definition should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.area_confinement.character_release"), "Area Confinement character release VFX definition should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.sword_wave.charge"), "Sword Wave charge VFX definition should compile.");
        Require(result.VfxDefinitionCatalog.Definitions.Exists(definition => definition.StableId == "vfx.magic.sword_wave.impact"), "Sword Wave impact VFX definition should compile.");
        Require(result.CharacterTestMechanicTuningCatalog.Tunings.Count == 4, "CharacterTest mechanic tuning catalog should compile current sample mechanics.");
        CharacterTestMechanicTuningConfig areaTuning = result.CharacterTestMechanicTuningCatalog.Tunings.Single(t => t.StableId == "mechanic_tuning.character_test.area_confinement");
        Require(areaTuning.StableId == "mechanic_tuning.character_test.area_confinement", "Area Confinement tuning stable id should compile.");
        Require(areaTuning.TelegraphFrames == 60, "Area Confinement tuning should compile a 60 frame telegraph.");
        Require(areaTuning.ZoneDurationFrames == 180, "Area Confinement tuning should compile a 180 frame zone.");
        Require(areaTuning.ZoneRadiusCentimeters == 250, "Area Confinement tuning should compile a 2.5m radius.");
        CharacterTestMechanicTuningConfig arcMineTuning = result.CharacterTestMechanicTuningCatalog.Tunings.Single(t => t.StableId == "mechanic_tuning.character_test.arc_mine");
        Require(arcMineTuning.StableId == "mechanic_tuning.character_test.arc_mine", "Arc Mine tuning stable id should compile.");
        Require(arcMineTuning.FuseDelayFrames == 60, "Arc Mine tuning should compile a 60 frame fuse.");
        Require(arcMineTuning.DurationFrames == 180, "Arc Mine tuning should compile a 180 frame duration.");
        Require(arcMineTuning.TriggerRadiusCentimeters == 125, "Arc Mine tuning trigger radius should compile.");
        Require(result.VfxTriggerMap.Mappings.Count == 18, "CharacterTest VFX trigger maps should compile current mappings.");
        Require(result.VfxTriggerMap.Mappings[0].TriggerId == 1 || result.VfxTriggerMap.Mappings[0].TriggerId == AbilityAuthoringCompiler.StablePositiveHash("character_test.fireball.impact"), "Fireball trigger id should be stable.");
        var fireballActionRef = result.ActionReferenceCatalog.ActionReferences.Single(reference => reference.AbilityStableId == "ability.character_test.fireball");
        Require(fireballActionRef.Damage != null, "Action reference should preserve direct damage.");
        Require(fireballActionRef.Damage.BaseDamage == 10, "Action reference direct damage should preserve base damage.");
        Require(fireballActionRef.Damage.ImpactPower == 8, "Action reference direct damage should preserve impact power.");
        Require(fireballActionRef.Damage.LaunchPower == 0, "Action reference direct damage should preserve launch power.");
        Require(result.ActionReferenceCatalog.ActionReferences.Exists(reference => reference.EffectTimelineLabel == "character_test.double_explosion"), "Action reference should preserve effect timeline id.");
        Require(result.ProjectileDefinitionCatalog.Definitions.Count == 23, "Projectile definition catalog should compile current definitions.");
        Require(result.ActionReferenceCatalog.ActionReferences.Exists(reference => reference.AbilityStableId == "ability.character_test.guard_aura"), "Action reference should include Guard Aura.");
        ProjectileDefinitionConfig projectile = FindProjectileDefinition(result, "character_test.fireball.projectile");
        Require(projectile.DefinitionId == 91001, "Fireball projectile definition id should compile.");
        Require(projectile.StableId == "character_test.fireball.projectile", "Fireball projectile stable id should compile.");
        Require(!projectile.PhysicalEventsEnabled, "Fireball projectile physical events should default off.");
        Require(projectile.Motion.Kind == "Linear", "Fireball projectile motion kind should default to Linear.");
        Require(projectile.Motion.SpeedProfile.Kind == "Constant", "Fireball projectile speed profile should default to Constant.");
        Require(Math.Abs(projectile.Motion.SpeedPerFrame - 0.35f) < 0.0001f, "Fireball projectile speed should compile.");
        Require(projectile.Motion.LifetimeFrames == 90, "Fireball projectile lifetime should compile.");
        Require(Math.Abs(projectile.Motion.ArcHeight) < 0.0001f, "Linear Fireball projectile arc height should compile as zero.");
        Require(Math.Abs(projectile.Collision.Radius - 0.25f) < 0.0001f, "Fireball projectile radius should compile.");
        Require(projectile.Collision.LayerMask == 256, "Fireball projectile layer mask should compile.");
        Require(projectile.Collision.StopOnFirstAcceptedImpact, "Fireball projectile stop-on-impact should compile.");
        Require(projectile.Collision.StopOnBlockedImpact, "Fireball projectile blocked stop policy should compile.");
        Require(string.IsNullOrWhiteSpace(projectile.Impact.PresentationProfileLabel), "Fireball projectile should not emit a legacy presentation profile.");
        Require(projectile.Impact.HitEvent.TargetTypes.SequenceEqual(new[] { "HostileCharacter" }), "Fireball projectile hit event target types should compile.");
        Require(projectile.Impact.HitEvent.EnvironmentHitPolicy == "None", "Fireball projectile hit event environment policy should compile.");
        Require(projectile.Impact.HitEvent.StableEventLabel == "character_test.fireball.projectile.impact", "Fireball projectile hit event stable id should compile.");
        Require(projectile.Impact.HitEvent.Damage != null, "Fireball projectile hit event damage should compile.");
        Require(projectile.Impact.HitEvent.Damage.DamageTypeLabel == "fire", "Fireball projectile damage type should compile.");
        Require(projectile.Impact.HitEvent.Damage.BaseDamage == 10, "Fireball projectile base damage should compile.");
        Require(projectile.Impact.HitEvent.Damage.ImpactPower == 8, "Fireball projectile impact power should compile.");
        Require(projectile.Impact.PolicyKind == "DirectHit", "Fireball projectile direct policy should compile.");
        ProjectileDefinitionConfig burstProjectile = FindProjectileDefinition(result, "character_test.fireball_burst.projectile");
        Require(burstProjectile.DefinitionId == 91002, "Fireball Burst projectile definition id should compile.");
        Require(burstProjectile.StableId == "character_test.fireball_burst.projectile", "Fireball Burst projectile stable id should compile.");
        Require(burstProjectile.Impact.HitEvent.Damage.BaseDamage == 8, "Fireball Burst projectile base damage should compile.");
        Require(burstProjectile.Impact.PolicyKind == "AreaPulseOnImpact", "Fireball Burst projectile area policy should compile.");
        Require(Math.Abs(burstProjectile.Impact.AreaRadius - 2.0f) < 0.0001f, "Fireball Burst projectile area radius should compile.");
        Require(burstProjectile.Impact.AreaLayerMask == 256, "Fireball Burst projectile area layer mask should compile.");
        Require(burstProjectile.Impact.AreaHitEvent != null, "Fireball Burst projectile area hit event should compile.");
        Require(burstProjectile.Impact.AreaHitEvent.TargetTypes.SequenceEqual(new[] { "HostileCharacter" }), "Fireball Burst projectile area hit event target types should compile.");
        Require(burstProjectile.Impact.AreaHitEvent.Damage == null, "Fireball Burst area hit event should inherit direct damage in generated JSON.");
        Require(burstProjectile.Impact.TriggerOnExpire, "Fireball Burst projectile trigger-on-expire should compile.");
        Require(burstProjectile.Impact.EffectTimelineLabel == "character_test.double_explosion", "Fireball Burst projectile should preserve effect timeline id.");
        ProjectileDefinitionConfig flameLanceProjectile = FindProjectileDefinition(result, "character_test.flame_lance.projectile");
        Require(flameLanceProjectile.DefinitionId == 91003, "Flame Lance projectile definition id should compile.");
        Require(flameLanceProjectile.StableId == "character_test.flame_lance.projectile", "Flame Lance projectile stable id should compile.");
        Require(Math.Abs(flameLanceProjectile.Motion.SpeedPerFrame - 0.55f) < 0.0001f, "Flame Lance projectile speed should compile.");
        Require(Math.Abs(flameLanceProjectile.Collision.Radius - 0.15f) < 0.0001f, "Flame Lance projectile radius should compile.");
        Require(!flameLanceProjectile.Collision.StopOnFirstAcceptedImpact, "Flame Lance projectile should not stop on first hit.");
        Require(flameLanceProjectile.Collision.MaxAcceptedImpacts == 3, "Flame Lance projectile hit limit should compile.");
        Require(flameLanceProjectile.Collision.HitSameTargetPolicy == "OncePerProjectile", "Flame Lance projectile same-target policy should compile.");
        Require(flameLanceProjectile.Impact.HitEvent.Damage.ImpactPower == 5, "Flame Lance projectile impact power should compile.");
        ProjectileDefinitionConfig lobProjectile = FindProjectileDefinition(result, "character_test.fireball_lob.projectile");
        Require(lobProjectile.DefinitionId == 91004, "Fireball Lob projectile definition id should compile.");
        Require(lobProjectile.StableId == "character_test.fireball_lob.projectile", "Fireball Lob projectile stable id should compile.");
        Require(lobProjectile.Motion.Kind == "ArcToTarget", "Fireball Lob projectile motion kind should compile.");
        Require(lobProjectile.Motion.SpeedProfile.Kind == "Constant", "Fireball Lob projectile speed profile should compile.");
        Require(Math.Abs(lobProjectile.Motion.ArcHeight - 4f) < 0.0001f, "Fireball Lob projectile arc height should compile.");
        Require(lobProjectile.Impact.PolicyKind == "AreaPulseOnImpact", "Fireball Lob projectile area policy should compile.");
        Require(lobProjectile.Impact.HitEvent.Damage.BaseDamage == 8, "Fireball Lob projectile should reuse Burst damage values.");
        ProjectileDefinitionConfig surgeProjectile = FindProjectileDefinition(result, "character_test.fireball_surge.projectile");
        Require(surgeProjectile.DefinitionId == 91005, "Fireball Surge projectile definition id should compile.");
        Require(surgeProjectile.StableId == "character_test.fireball_surge.projectile", "Fireball Surge projectile stable id should compile.");
        Require(surgeProjectile.Motion.Kind == "Linear", "Fireball Surge projectile motion kind should compile.");
        Require(surgeProjectile.Motion.SpeedProfile.Kind == "EaseInQuadratic", "Fireball Surge projectile speed profile should compile.");
        Require(surgeProjectile.Impact.PolicyKind == "DirectHit", "Fireball Surge projectile direct policy should compile.");
        Require(surgeProjectile.Impact.HitEvent.Damage.BaseDamage == 10, "Fireball Surge projectile should reuse Fireball damage values.");
        ProjectileDefinitionConfig returnProjectile = FindProjectileDefinition(result, "character_test.fireball_return.projectile");
        Require(returnProjectile.DefinitionId == 91006, "Fireball Return projectile definition id should compile.");
        Require(returnProjectile.StableId == "character_test.fireball_return.projectile", "Fireball Return projectile stable id should compile.");
        Require(returnProjectile.Motion.Kind == "BoomerangReturn", "Fireball Return projectile motion kind should compile.");
        Require(returnProjectile.Collision.MaxAcceptedImpacts == 2, "Fireball Return projectile hit limit should compile.");
        Require(!returnProjectile.Collision.StopOnFirstAcceptedImpact, "Fireball Return projectile should continue after first accepted hit.");
        Require(returnProjectile.Collision.HitSameTargetPolicy == "OncePerProjectile", "Fireball Return projectile same-target policy should compile.");
        Require(returnProjectile.Impact.HitEvent.Damage.BaseDamage == 10, "Fireball Return projectile should reuse Fireball damage values.");
        ProjectileDefinitionConfig swayProjectile = FindProjectileDefinition(result, "character_test.fireball_sway.projectile");
        Require(swayProjectile.DefinitionId == 91007, "Fireball Sway projectile definition id should compile.");
        Require(swayProjectile.StableId == "character_test.fireball_sway.projectile", "Fireball Sway projectile stable id should compile.");
        Require(swayProjectile.Motion.Kind == "SwayForward", "Fireball Sway projectile motion kind should compile.");
        Require(Math.Abs(swayProjectile.Motion.LateralAmplitude - 0.75f) < 0.0001f, "Fireball Sway projectile lateral amplitude should compile.");
        Require(swayProjectile.Motion.LateralCycles == 2, "Fireball Sway projectile lateral cycles should compile.");
        Require(swayProjectile.Impact.HitEvent.Damage.BaseDamage == 10, "Fireball Sway projectile should reuse Fireball damage values.");
        ProjectileDefinitionConfig homingProjectile = FindProjectileDefinition(result, "character_test.fireball_homing.projectile");
        Require(homingProjectile.DefinitionId == 91008, "Fireball Homing projectile definition id should compile.");
        Require(homingProjectile.StableId == "character_test.fireball_homing.projectile", "Fireball Homing projectile stable id should compile.");
        Require(homingProjectile.Motion.Kind == "HomingToTarget", "Fireball Homing projectile motion kind should compile.");
        Require(Math.Abs(homingProjectile.Motion.Homing.TurnRatePerFrame - 0.15f) < 0.0001f, "Fireball Homing projectile turn rate should compile.");
        Require(homingProjectile.Impact.HitEvent.Damage.BaseDamage == 10, "Fireball Homing projectile should reuse Fireball damage values.");
        ProjectileDefinitionConfig bezierProjectile = FindProjectileDefinition(result, "character_test.fireball_bezier.projectile");
        Require(bezierProjectile.DefinitionId == 91009, "Fireball Bezier projectile definition id should compile.");
        Require(bezierProjectile.StableId == "character_test.fireball_bezier.projectile", "Fireball Bezier projectile stable id should compile.");
        Require(bezierProjectile.Motion.Kind == "BezierToTarget", "Fireball Bezier projectile motion kind should compile.");
        Require(Math.Abs(bezierProjectile.Motion.BezierControlOffsetA.Y - 3f) < 0.0001f, "Fireball Bezier control point A height should compile.");
        Require(Math.Abs(bezierProjectile.Motion.BezierControlOffsetA.Z - 6f) < 0.0001f, "Fireball Bezier control point A forward offset should compile.");
        Require(Math.Abs(bezierProjectile.Motion.BezierControlOffsetB.Y - 2f) < 0.0001f, "Fireball Bezier control point B height should compile.");
        Require(Math.Abs(bezierProjectile.Motion.BezierControlOffsetB.Z + 4f) < 0.0001f, "Fireball Bezier control point B backward offset should compile.");
        Require(bezierProjectile.Impact.HitEvent.Damage.BaseDamage == 10, "Fireball Bezier projectile should reuse Fireball damage values.");
        ProjectileDefinitionConfig explosionProjectile = FindProjectileDefinition(result, "character_test.fireball_explosion.projectile");
        Require(explosionProjectile.DefinitionId == 91010, "Fireball Explosion projectile definition id should compile.");
        Require(explosionProjectile.StableId == "character_test.fireball_explosion.projectile", "Fireball Explosion projectile stable id should compile.");
        Require(explosionProjectile.PhysicalEventsEnabled, "Fireball Explosion projectile should output physical events.");
        Require(explosionProjectile.Impact.PolicyKind == "AreaPulseOnImpact", "Fireball Explosion projectile area policy should compile.");
        Require(Math.Abs(explosionProjectile.Impact.AreaRadius - 2.5f) < 0.0001f, "Fireball Explosion projectile area radius should compile.");
        Require(explosionProjectile.Impact.AreaLayerMask == 256, "Fireball Explosion projectile area layer mask should compile.");
        Require(explosionProjectile.Impact.HitEvent.Damage.BaseDamage == 8, "Fireball Explosion projectile should reuse Burst damage values.");
        Require(explosionProjectile.Impact.EffectTimelineLabel == "character_test.fireball_explosion.afterblast", "Fireball Explosion projectile should preserve afterblast timeline id.");
        ProjectileDefinitionConfig tornadoProjectile = FindProjectileDefinition(result, "character_test.tornado.projectile");
        Require(tornadoProjectile.DefinitionId == 91011, "Tornado projectile definition id should compile.");
        Require(tornadoProjectile.StableId == "character_test.tornado.projectile", "Tornado projectile stable id should compile.");
        Require(tornadoProjectile.PhysicalEventsEnabled, "Tornado projectile should output physical events.");
        Require(Math.Abs(tornadoProjectile.Collision.Radius - 4.5f) < 0.0001f, "Tornado projectile active physical radius should compile.");
        Require(tornadoProjectile.Collision.LayerMask == 0, "Tornado projectile direct sweep should be disabled.");
        ProjectileDefinitionConfig poisonBottleProjectile = FindProjectileDefinition(result, "character_test.poison_bottle.projectile");
        Require(poisonBottleProjectile.DefinitionId == 91015, "Poison Bottle projectile definition id should compile.");
        Require(poisonBottleProjectile.Motion.Kind == "BallisticArcToTarget", "Poison Bottle projectile motion kind should compile.");
        Require(Math.Abs(poisonBottleProjectile.Motion.SpeedPerFrame - 0.3535533906f) < 0.0001f, "Poison Bottle projectile initial speed should compile.");
        Require(poisonBottleProjectile.Motion.LifetimeFrames == 90, "Poison Bottle projectile lifetime should compile.");
        Require(Math.Abs(poisonBottleProjectile.Motion.ArcHeight) < 0.0001f, "Poison Bottle projectile should not use designed arc height.");
        Require(Math.Abs(poisonBottleProjectile.Motion.GravityPerFrameSquared - 0.008333333f) < 0.0001f, "Poison Bottle projectile gravity should compile.");
        ProjectileDefinitionConfig meteorProjectile = FindProjectileDefinition(result, "character_test.meteor_barrage.projectile");
        Require(meteorProjectile.PhysicalEventsEnabled, "Meteor Barrage projectile should output physical events.");
        Require(meteorProjectile.Collision.LayerMask == 258, "Meteor Barrage direct query mask should include characters and static environment.");
        Require(meteorProjectile.Impact.AreaLayerMask == 256, "Meteor Barrage area query mask should only include characters.");
        Require(meteorProjectile.Impact.HitEvent.TargetTypes.SequenceEqual(new[] { "HostileCharacter", "WorldGround", "WorldStatic", "WorldWall" }), "Meteor Barrage direct target types should compile.");
        Require(meteorProjectile.Impact.AreaHitEvent.TargetTypes.SequenceEqual(new[] { "HostileCharacter" }), "Meteor Barrage area target types should compile.");
        ProjectileDefinitionConfig swordWaveProjectile = FindProjectileDefinition(result, "character_test.sword_wave.projectile");
        Require(swordWaveProjectile.DefinitionId == 91018, "Sword Wave projectile definition id should compile.");
        Require(swordWaveProjectile.PhysicalEventsEnabled, "Sword Wave projectile should output physical events.");
        Require(Math.Abs(swordWaveProjectile.Collision.Radius - 0.22f) < 0.0001f, "Sword Wave projectile sample radius should compile.");
        Require(swordWaveProjectile.Collision.LayerMask == 258, "Sword Wave direct query mask should include characters and static environment.");
        Require(!swordWaveProjectile.Collision.StopOnFirstAcceptedImpact, "Sword Wave projectile should continue after hitting enemies.");
        Require(swordWaveProjectile.Collision.MaxAcceptedImpacts == 4, "Sword Wave projectile hit limit should compile.");
        Require(swordWaveProjectile.Impact.PolicyKind == "DirectHit", "Sword Wave projectile direct policy should compile.");
        Require(
            swordWaveProjectile.Impact.HitEvent.TargetTypes.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(new[] { "HostileCharacter", "WorldGround", "WorldStatic", "WorldWall" }),
            "Sword Wave direct target types should compile.");
        Require(swordWaveProjectile.Impact.HitEvent.EnvironmentHitPolicy == "BlockedPresentation", "Sword Wave blocked presentation policy should compile.");
        Require(swordWaveProjectile.Impact.HitEvent.Damage.BaseDamage == 9, "Sword Wave projectile base damage should compile.");
        Require(result.ProjectilePresentationBindings.Bindings.Count == 23, "Projectile presentation bindings should compile current bindings.");
        ProjectilePresentationBindingConfig binding = FindProjectilePresentationBinding(result, 91001);
        Require(binding.ProjectileDefinitionId == 91001, "Fireball projectile presentation definition id should compile.");
        Require(binding.PrefabResource.Id == "vfx.magic.fireball", "Fireball projectile presentation resource id should compile.");
        Require(binding.PrefabResource.TypeId == "GameObject", "Fireball projectile presentation resource type should compile.");
        Require(binding.PrefabResource.PackageId == "mxframework.samples", "Fireball projectile presentation resource package should compile.");
        Require(binding.ViewNamePrefix == "ProjectileVFX", "Fireball projectile presentation view prefix should compile.");
        Require(FindProjectilePresentationBinding(result, 91002).PrefabResource.Id == "vfx.authored.2c98b752-503c-41af-933e-264fe69cceb8" || FindProjectilePresentationBinding(result, 91002).PrefabResource.Id == "vfx.magic.fireball_burst.flight", "Fireball Burst projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91003).PrefabResource.Id == "vfx.magic.flame_lance.flight", "Flame Lance projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91004).PrefabResource.Id == "vfx.magic.fireball_burst.flight", "Fireball Lob projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91005).PrefabResource.Id == "vfx.magic.fireball", "Fireball Surge projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91006).PrefabResource.Id == "vfx.magic.fireball", "Fireball Return projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91007).PrefabResource.Id == "vfx.magic.fireball", "Fireball Sway projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91008).PrefabResource.Id == "vfx.magic.fireball", "Fireball Homing projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91009).PrefabResource.Id == "vfx.magic.fireball", "Fireball Bezier projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91010).PrefabResource.Id == "vfx.magic.fireball_burst.flight", "Fireball Explosion projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91011).PrefabResource.Id == "vfx.magic.tornado.flight", "Tornado projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91012).PrefabResource.Id == "vfx.magic.beam_lance.beam", "Beam Lance projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91013).PrefabResource.Id == "vfx.magic.channel_beam.beam", "Channel Beam projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91014).PrefabResource.Id == "vfx.magic.laser_lance.laser", "Laser Lance projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91015).PrefabResource.Id == "vfx.magic.poison_bottle.flight", "Poison Bottle projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91016).PrefabResource.Id == "vfx.magic.stun_bolt.flight", "Stun Bolt projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91017).PrefabResource.Id == "vfx.magic.meteor_barrage.flight", "Meteor Barrage projectile presentation resource id should compile.");
        Require(FindProjectilePresentationBinding(result, 91018).PrefabResource.Id == "vfx.magic.sword_wave.flight", "Sword Wave projectile presentation resource id should compile.");
        Require(result.ProjectileSpawnProfileCatalog.Profiles.Count == 28, "Projectile spawn profile catalog should compile current profiles.");
        ProjectileSpawnProfileConfig fireballSpawn = FindProjectileSpawnProfile(result, "character_test.fireball.projectile");
        Require(fireballSpawn.StableId == "character_test.fireball.projectile", "Fireball projectile spawn profile stable id should compile.");
        Require(fireballSpawn.ProjectileStableId == "character_test.fireball.projectile", "Fireball projectile spawn profile projectile reference should compile.");
        Require(fireballSpawn.Origin.Kind == "Socket", "Fireball projectile spawn profile origin kind should compile.");
        Require(fireballSpawn.Origin.SocketLabel == "mainHand", "Fireball projectile spawn profile socket should compile.");
        Require(Math.Abs(fireballSpawn.Origin.FallbackRootOffset.Y - 1.15f) < 0.0001f, "Fireball projectile spawn fallback height should compile.");
        Require(Math.Abs(fireballSpawn.Origin.FallbackRootOffset.Z - 0.65f) < 0.0001f, "Fireball projectile spawn fallback forward offset should compile.");
        Require(fireballSpawn.Aim.Kind == "LockedTargetOrCasterForwardRange", "Fireball projectile aim profile should prefer locked target.");
        Require(FindProjectileSpawnProfile(result, "character_test.fireball_burst.projectile").StableId == "character_test.fireball_burst.projectile", "Fireball Burst spawn profile should compile.");
        ProjectileSpawnProfileConfig flameLanceSpawn = FindProjectileSpawnProfile(result, "character_test.flame_lance.projectile");
        Require(flameLanceSpawn.StableId == "character_test.flame_lance.projectile", "Flame Lance spawn profile should compile.");
        Require(flameLanceSpawn.Aim.Kind == "LockedTargetOrCasterForwardRange", "Flame Lance aim should prefer locked target.");
        Require(flameLanceSpawn.Aim.FallbackKind == "CasterForwardRange", "Flame Lance aim fallback should compile.");
        Require(Math.Abs(flameLanceSpawn.Aim.MaxRange - 44f) < 0.0001f, "Flame Lance aim max range should compile.");
        ProjectileSpawnProfileConfig lobSpawn = FindProjectileSpawnProfile(result, "character_test.fireball_lob.projectile");
        Require(lobSpawn.StableId == "character_test.fireball_lob.projectile", "Fireball Lob spawn profile should compile.");
        Require(lobSpawn.ProjectileStableId == "character_test.fireball_lob.projectile", "Fireball Lob spawn projectile reference should compile.");
        Require(lobSpawn.Aim.Kind == "LockedTargetOrCasterForwardRange", "Fireball Lob aim should prefer locked target.");
        ProjectileSpawnProfileConfig surgeSpawn = FindProjectileSpawnProfile(result, "character_test.fireball_surge.projectile");
        Require(surgeSpawn.StableId == "character_test.fireball_surge.projectile", "Fireball Surge spawn profile should compile.");
        Require(surgeSpawn.ProjectileStableId == "character_test.fireball_surge.projectile", "Fireball Surge spawn projectile reference should compile.");
        Require(surgeSpawn.Aim.Kind == "CasterForwardRange", "Fireball Surge aim should use forward range.");
        ProjectileSpawnProfileConfig returnSpawn = FindProjectileSpawnProfile(result, "character_test.fireball_return.projectile");
        Require(returnSpawn.StableId == "character_test.fireball_return.projectile", "Fireball Return spawn profile should compile.");
        Require(returnSpawn.ProjectileStableId == "character_test.fireball_return.projectile", "Fireball Return spawn projectile reference should compile.");
        Require(returnSpawn.Aim.Kind == "CasterForwardRange", "Fireball Return aim should use forward range.");
        ProjectileSpawnProfileConfig swaySpawn = FindProjectileSpawnProfile(result, "character_test.fireball_sway.projectile");
        Require(swaySpawn.StableId == "character_test.fireball_sway.projectile", "Fireball Sway spawn profile should compile.");
        Require(swaySpawn.ProjectileStableId == "character_test.fireball_sway.projectile", "Fireball Sway spawn projectile reference should compile.");
        Require(swaySpawn.Aim.Kind == "CasterForwardRange", "Fireball Sway aim should use forward range.");
        ProjectileSpawnProfileConfig homingSpawn = FindProjectileSpawnProfile(result, "character_test.fireball_homing.projectile");
        Require(homingSpawn.StableId == "character_test.fireball_homing.projectile", "Fireball Homing spawn profile should compile.");
        Require(homingSpawn.ProjectileStableId == "character_test.fireball_homing.projectile", "Fireball Homing spawn projectile reference should compile.");
        Require(homingSpawn.Aim.Kind == "LockedTargetOrCasterForwardRange", "Fireball Homing aim should prefer locked target.");
        ProjectileSpawnProfileConfig bezierSpawn = FindProjectileSpawnProfile(result, "character_test.fireball_bezier.projectile");
        Require(bezierSpawn.StableId == "character_test.fireball_bezier.projectile", "Fireball Bezier spawn profile should compile.");
        Require(bezierSpawn.ProjectileStableId == "character_test.fireball_bezier.projectile", "Fireball Bezier spawn projectile reference should compile.");
        Require(bezierSpawn.Aim.Kind == "LockedTargetOrCasterForwardRange", "Fireball Bezier aim should prefer locked target.");
        ProjectileSpawnProfileConfig explosionSpawn = FindProjectileSpawnProfile(result, "character_test.fireball_explosion.projectile");
        Require(explosionSpawn.StableId == "character_test.fireball_explosion.projectile", "Fireball Explosion spawn profile should compile.");
        Require(explosionSpawn.ProjectileStableId == "character_test.fireball_explosion.projectile", "Fireball Explosion spawn projectile reference should compile.");
        Require(explosionSpawn.Aim.Kind == "CasterForwardRange", "Fireball Explosion aim should use forward range.");
        ProjectileSpawnProfileConfig poisonBottleSpawn = FindProjectileSpawnProfile(result, "character_test.poison_bottle.projectile");
        Require(poisonBottleSpawn.StableId == "character_test.poison_bottle.projectile", "Poison Bottle spawn profile should compile.");
        Require(poisonBottleSpawn.ProjectileStableId == "character_test.poison_bottle.projectile", "Poison Bottle spawn projectile reference should compile.");
        Require(poisonBottleSpawn.Aim.Kind == "LockedTargetOrCasterForwardRange", "Poison Bottle aim should prefer locked target.");
        Require(poisonBottleSpawn.Aim.FallbackKind == "CasterForwardRange", "Poison Bottle aim fallback should compile.");
        Require(Math.Abs(poisonBottleSpawn.Aim.MaxRange) < 0.0001f, "Poison Bottle ballistic aim max range should normalize to zero.");
        ProjectileSpawnProfileConfig swordWaveSpawn = FindProjectileSpawnProfile(result, "character_test.sword_wave.projectile");
        Require(swordWaveSpawn.StableId == "character_test.sword_wave.projectile", "Sword Wave spawn profile should compile.");
        Require(swordWaveSpawn.ProjectileStableId == "character_test.sword_wave.projectile", "Sword Wave spawn projectile reference should compile.");
        Require(swordWaveSpawn.Aim.Kind == "CasterForwardRange", "Sword Wave aim should use forward range.");
        Require(Math.Abs(swordWaveSpawn.Aim.MaxRange - 30f) < 0.0001f, "Sword Wave aim max range should compile.");
        Require(result.EffectTimelineCatalog.Timelines.Count == 2, "Ability effect timeline catalog should compile two sample timelines.");
        AbilityEffectTimelineConfig timeline = result.EffectTimelineCatalog.Timelines[0];
        Require(timeline.TimelineLabel == "character_test.double_explosion", "Double explosion timeline id should compile.");
        Require(timeline.DurationFrames == 121, "Double explosion timeline duration should compile.");
        Require(timeline.Events.Count == 2, "Double explosion timeline should compile two pulses.");
        Require(timeline.Events[0].Frame == 0, "Double explosion initial pulse should start at frame 0.");
        Require(timeline.Events[1].Frame == 120, "Double explosion repeat pulse should start at frame 120.");
        AbilityEffectTimelineConfig explosionTimeline = result.EffectTimelineCatalog.Timelines[1];
        Require(explosionTimeline.TimelineLabel == "character_test.fireball_explosion.afterblast", "Fireball Explosion afterblast timeline id should compile.");
        Require(explosionTimeline.DurationFrames == 121, "Fireball Explosion afterblast timeline duration should compile.");
        Require(explosionTimeline.Events.Count == 3, "Fireball Explosion afterblast timeline should compile VFX, delayed pulse, and repeat VFX.");
        Require(explosionTimeline.Events[0].Frame == 0, "Fireball Explosion initial VFX should start at frame 0.");
        Require(explosionTimeline.Events[1].Frame == 120, "Fireball Explosion repeat pulse should start at frame 120.");
        Require(explosionTimeline.Events[2].Frame == 120, "Fireball Explosion repeat VFX should start at frame 120.");
    }

    private static void AbilityAuthoringCompiler_Projectiles_DeriveMasksFromTargetTypes()
    {
        var document = new AbilityAuthoringDocument
        {
            PackageId = "projectile.semantic.mask"
        };
        document.ProjectileDefinitions.Add(new ProjectileDefinitionConfig
        {
            DefinitionId = 1000,
            StableId = "projectile.semantic.mask",
            Motion = new ProjectileMotionConfig
            {
                SpeedPerFrame = 1f,
                LifetimeFrames = 10
            },
            Collision = new ProjectileCollisionConfig
            {
                Radius = 0.1f,
                MaxAcceptedImpacts = 1,
                HitSameTargetPolicy = "AllowRepeat"
            },
            Impact = new ProjectileImpactConfig
            {
                PolicyKind = "AreaPulseOnImpact",
                AreaRadius = 1.5f,
                HitEvent = new CombatHitEventConfig
                {
                    TargetTypes = new List<string> { "HostileCharacter", "WorldGround" },
                    EnvironmentHitPolicy = "BlockedPresentation",
                    MaxAcceptedHits = 1,
                    HitSameTargetPolicy = "AllowRepeat",
                    Damage = new CombatHitDamageConfig
                    {
                        DamageTypeLabel = "fire",
                        BaseDamage = 1,
                        ImpactPower = 1,
                        AnalysisCategoryLabel = "ProjectileSemanticMaskTest"
                    }
                }
            }
        });

        AbilityAuthoringCompileResult result = AbilityAuthoringCompiler.Compile(document);

        Require(!result.ValidationReport.HasBlockingIssues, "Semantic projectile mask document should validate.");
        ProjectileDefinitionConfig projectile = FindProjectileDefinition(result, "projectile.semantic.mask");
        Require(projectile.Collision.LayerMask == 258, "Direct projectile mask should be derived from character and world target types.");
        Require(projectile.Impact.AreaLayerMask == 258, "Area projectile mask should inherit direct target types when area hit event is missing.");
        Require(projectile.Impact.AreaHitEvent != null, "Area hit event should be generated when AreaPulseOnImpact omits one.");
        Require(projectile.Impact.AreaHitEvent.TargetTypes.SequenceEqual(projectile.Impact.HitEvent.TargetTypes), "Generated area hit event should inherit direct target types.");
    }

    private static void AbilityAuthoringCompiler_InvalidReferences_BlockValidation()
    {
        var document = new AbilityAuthoringDocument
        {
            PackageId = "invalid.ability"
        };
        document.KnownSocketIds.Add("mainHand");
        document.Abilities.Add(new AbilityDefinitionConfig
        {
            AbilityId = 1,
            AbilityStableId = "ability.invalid",
            Input = new AbilityInputBindingConfig { InputIntentLabel = "AttackSecondary" },
            ActionBinding = new AbilityActionBindingConfig(),
            PresentationProfileLabel = "vfx.missing",
            EffectTimelineLabel = "timeline.missing",
            TargetPolicyLabel = "target.unknown"
        });
        document.VfxDefinitions.Add(new VfxDefinitionConfig
        {
            DefinitionId = 100
        });
        document.VfxTriggerMaps.Add(new VfxTriggerMapConfig
        {
            SourceDomain = "CombatHit",
            PresentationProfileLabel = "missing.profile",
            DefinitionId = 999
        });
        document.ProjectileDefinitions.Add(new ProjectileDefinitionConfig
        {
            DefinitionId = 42,
            StableId = "projectile.invalid",
            Motion = new ProjectileMotionConfig
            {
                Kind = "Spline",
                SpeedProfile = new ProjectileSpeedProfileConfig { Kind = "BezierCurve" },
                SpeedPerFrame = 0f,
                LifetimeFrames = 0,
                ArcHeight = -1f
            },
            Collision = new ProjectileCollisionConfig
            {
                Radius = -0.1f,
                LayerMask = 0,
                MaxAcceptedImpacts = 0,
                HitSameTargetPolicy = "EveryOtherFrame"
            },
            Impact = new ProjectileImpactConfig
            {
                PresentationProfileLabel = "vfx.missing",
                EffectTimelineLabel = "timeline.projectile.missing",
                PolicyKind = "ExplodeEverywhere",
                TriggerOnExpire = true,
                HitEvent = new CombatHitEventConfig
                {
                    TargetTypes = new List<string> { "UnknownTarget" },
                    EnvironmentHitPolicy = "Explode",
                    MaxAcceptedHits = 0,
                    HitSameTargetPolicy = "EveryOtherEvent"
                }
            }
        });
        document.ProjectileDefinitions.Add(new ProjectileDefinitionConfig
        {
            DefinitionId = 42,
            StableId = "projectile.invalid",
            Motion = new ProjectileMotionConfig
            {
                Kind = "Linear",
                SpeedPerFrame = 1f,
                LifetimeFrames = 1,
                GravityPerFrameSquared = -1f
            },
            Collision = new ProjectileCollisionConfig
            {
                Radius = 0.1f,
                LayerMask = 1
            },
            Impact = new ProjectileImpactConfig
            {
                PolicyKind = "AreaPulseOnImpact",
                AreaRadius = 0f,
                AreaHitEvent = new CombatHitEventConfig
                {
                    TargetTypes = new List<string> { "UnknownAreaTarget" },
                    EnvironmentHitPolicy = "None"
                }
            }
        });
        document.ProjectileDefinitions.Add(new ProjectileDefinitionConfig
        {
            DefinitionId = 43,
            StableId = "projectile.expire.invalid",
            Motion = new ProjectileMotionConfig
            {
                SpeedPerFrame = 1f,
                LifetimeFrames = 1
            },
            Collision = new ProjectileCollisionConfig
            {
                Radius = 0.1f,
                LayerMask = 1
            },
            Impact = new ProjectileImpactConfig
            {
                PolicyKind = "DirectHit",
                TriggerOnExpire = true
            }
        });
        document.ProjectileDefinitions.Add(new ProjectileDefinitionConfig
        {
            DefinitionId = 45,
            StableId = "projectile.sway.invalid",
            Motion = new ProjectileMotionConfig
            {
                Kind = "SwayForward",
                SpeedProfile = new ProjectileSpeedProfileConfig { Kind = "Constant" },
                SpeedPerFrame = 1f,
                LifetimeFrames = 1,
                LateralAmplitude = 0f,
                LateralCycles = 0
            },
            Collision = new ProjectileCollisionConfig
            {
                Radius = 0.1f,
                LayerMask = 1
            },
            Impact = new ProjectileImpactConfig
            {
                HitEvent = new CombatHitEventConfig
                {
                    TargetTypes = new List<string> { "HostileCharacter" },
                    EnvironmentHitPolicy = "None",
                    Damage = new CombatHitDamageConfig
                    {
                        DamageTypeLabel = "fire",
                        BaseDamage = 1,
                        ImpactPower = 1,
                        AnalysisCategoryLabel = "InvalidTest"
                    }
                }
            }
        });
        document.ProjectileDefinitions.Add(new ProjectileDefinitionConfig
        {
            DefinitionId = 44,
            StableId = "projectile.damage.missing",
            Motion = new ProjectileMotionConfig
            {
                SpeedPerFrame = 1f,
                LifetimeFrames = 1
            },
            Collision = new ProjectileCollisionConfig
            {
                Radius = 0.1f,
                LayerMask = 1
            },
            Impact = new ProjectileImpactConfig
            {
                HitEvent = new CombatHitEventConfig
                {
                    TargetTypes = new List<string> { "HostileCharacter" },
                    EnvironmentHitPolicy = "None",
                    MaxAcceptedHits = 1,
                    HitSameTargetPolicy = "AllowRepeat",
                    OnHitBuffs = new List<CombatHitBuffApplicationConfig>
                    {
                        new CombatHitBuffApplicationConfig { BuffId = 0 },
                        new CombatHitBuffApplicationConfig { BuffId = 90 },
                        new CombatHitBuffApplicationConfig { BuffId = 90 }
                    }
                }
            }
        });
        document.ProjectileDefinitions.Add(new ProjectileDefinitionConfig
        {
            DefinitionId = 46,
            StableId = "projectile.homing.invalid",
            Motion = new ProjectileMotionConfig
            {
                Kind = "HomingToTarget",
                SpeedProfile = new ProjectileSpeedProfileConfig { Kind = "Constant" },
                SpeedPerFrame = 1f,
                LifetimeFrames = 10,
                Homing = new ProjectileHomingConfig { TurnRatePerFrame = 0f }
            },
            Collision = new ProjectileCollisionConfig
            {
                Radius = 0.1f,
                LayerMask = 1
            },
            Impact = new ProjectileImpactConfig
            {
                HitEvent = new CombatHitEventConfig
                {
                    TargetTypes = new List<string> { "HostileCharacter" },
                    EnvironmentHitPolicy = "None",
                    Damage = new CombatHitDamageConfig
                    {
                        DamageTypeLabel = "fire",
                        BaseDamage = 1,
                        ImpactPower = 1,
                        AnalysisCategoryLabel = "InvalidHomingTest"
                    }
                }
            }
        });
        document.EffectTimelines.Add(new AbilityEffectTimelineConfig
        {
            TimelineLabel = "timeline.loop.a",
            DurationFrames = 2,
            Events = new List<AbilityEffectTimelineEventConfig>
            {
                new AbilityEffectTimelineEventConfig
                {
                    EventLabel = "start_b",
                    TrackKind = "Gameplay",
                    EventKind = "StartEffectTimeline",
                    Frame = 0,
                    TargetTimelineLabel = "timeline.loop.b"
                }
            }
        });
        document.EffectTimelines.Add(new AbilityEffectTimelineConfig
        {
            TimelineLabel = "timeline.loop.b",
            DurationFrames = 2,
            Events = new List<AbilityEffectTimelineEventConfig>
            {
                new AbilityEffectTimelineEventConfig
                {
                    EventLabel = "start_a",
                    TrackKind = "Gameplay",
                    EventKind = "StartEffectTimeline",
                    Frame = 0,
                    TargetTimelineLabel = "timeline.loop.a"
                },
                new AbilityEffectTimelineEventConfig
                {
                    EventLabel = "missing_target",
                    TrackKind = "Gameplay",
                    EventKind = "StartEffectTimeline",
                    Frame = 1,
                    TargetTimelineLabel = "timeline.child.missing"
                }
            }
        });
        document.EffectTimelines.Add(new AbilityEffectTimelineConfig
        {
            TimelineLabel = "timeline.loop.b",
            DurationFrames = 0,
            Events = new List<AbilityEffectTimelineEventConfig>
            {
                new AbilityEffectTimelineEventConfig
                {
                    EventLabel = "bad_combat",
                    TrackKind = "Combat",
                    EventKind = "SpawnVfx",
                    Frame = 0,
                    ShapeKind = "Box",
                    AnchorMode = "OwnerOrPoint"
                }
            }
        });
        for (int i = 0; i <= AbilityAuthoringCompiler.MaxEffectTimelineChildDepth + 1; i++)
        {
            document.EffectTimelines.Add(new AbilityEffectTimelineConfig
            {
                TimelineLabel = "timeline.deep." + i,
                DurationFrames = 1,
                Events = i <= AbilityAuthoringCompiler.MaxEffectTimelineChildDepth
                    ? new List<AbilityEffectTimelineEventConfig>
                    {
                        new AbilityEffectTimelineEventConfig
                        {
                            EventLabel = "start_next",
                            TrackKind = "Gameplay",
                            EventKind = "StartEffectTimeline",
                            Frame = 0,
                            TargetTimelineLabel = "timeline.deep." + (i + 1)
                        }
                    }
                    : new List<AbilityEffectTimelineEventConfig>()
            });
        }
        document.ProjectilePresentationBindings.Add(new ProjectilePresentationBindingConfig
        {
            ProjectileDefinitionId = 999,
            PrefabResource = new AbilityResourceKeyConfig()
        });
        document.ProjectileSpawnProfiles.Add(new ProjectileSpawnProfileConfig
        {
            StableId = "projectile.spawn.invalid",
            ProjectileStableId = "projectile.missing",
            Origin = new ProjectileSpawnOriginConfig
            {
                Kind = "Socket",
                SocketLabel = "missingHand"
            },
            Aim = new ProjectileAimConfig
            {
                Kind = "MouseWorldPoint"
            }
        });
        document.ProjectileSpawnProfiles.Add(new ProjectileSpawnProfileConfig
        {
            StableId = "projectile.spawn.invalid",
            ProjectileStableId = "projectile.invalid",
            Origin = new ProjectileSpawnOriginConfig
            {
                Kind = "Bone",
                SocketLabel = "mainHand"
            },
            Aim = new ProjectileAimConfig
            {
                Kind = "LockedTargetOrCasterForwardRange",
                FallbackKind = "LockedTargetCenter",
                MaxRange = -1f
            }
        });

        AbilityAuthoringValidationReport report = AbilityAuthoringCompiler.Validate(document);

        Require(report.HasBlockingIssues, "Invalid ability package should block validation.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_ACTION_MISSING"), "Missing action should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_HIT_DAMAGE_MISSING"), "Missing ability damage should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_VFX_PROFILE_UNKNOWN"), "Missing VFX profile should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_EFFECT_TIMELINE_UNKNOWN"), "Missing ability effect timeline should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_VFX_RESOURCE_KEY_MISSING"), "Missing VFX resource key should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_TARGET_POLICY_UNKNOWN"), "Unknown target policy should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_DEFINITION_ID_DUPLICATE"), "Duplicate projectile definition id should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_STABLE_ID_DUPLICATE"), "Duplicate projectile stable id should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_SPEED_INVALID"), "Invalid projectile speed should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_LIFETIME_INVALID"), "Invalid projectile lifetime should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_MOTION_KIND_INVALID"), "Invalid projectile motion kind should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_SPEED_PROFILE_KIND_INVALID"), "Invalid projectile speed profile should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_ARC_HEIGHT_INVALID"), "Invalid projectile arc height should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_GRAVITY_INVALID"), "Invalid projectile gravity should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_LATERAL_AMPLITUDE_INVALID"), "Invalid projectile lateral amplitude should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_LATERAL_CYCLES_INVALID"), "Invalid projectile lateral cycles should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_HOMING_TURN_RATE_INVALID"), "Invalid projectile homing turn rate should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_RADIUS_INVALID"), "Invalid projectile radius should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_LAYER_MASK_INVALID"), "Invalid projectile layer mask should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_HIT_LIMIT_INVALID"), "Invalid projectile hit limit should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_HIT_SAME_TARGET_POLICY_INVALID"), "Invalid projectile same-target policy should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_POLICY_INVALID"), "Invalid projectile policy should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_AREA_RADIUS_INVALID"), "Invalid projectile area radius should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_AREA_LAYER_MASK_INVALID"), "Invalid projectile area layer mask should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_TRIGGER_ON_EXPIRE_POLICY_INVALID"), "Invalid trigger-on-expire policy should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_HIT_TARGET_TYPE_UNKNOWN"), "Unknown combat hit target type should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_HIT_ENVIRONMENT_POLICY_INVALID"), "Invalid combat hit environment policy should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_HIT_MAX_ACCEPTED_HITS_INVALID"), "Invalid combat hit max accepted hits should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_HIT_SAME_TARGET_POLICY_INVALID"), "Invalid combat hit same-target policy should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_HIT_DAMAGE_MISSING"), "Missing combat hit damage should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_HIT_BUFF_ID_INVALID"), "Invalid on-hit buff id should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_HIT_BUFF_ID_DUPLICATE"), "Duplicate on-hit buff id should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_PRESENTATION_PROFILE_UNKNOWN"), "Unknown projectile impact presentation profile should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_EFFECT_TIMELINE_UNKNOWN"), "Unknown projectile impact effect timeline should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_PRESENTATION_DEFINITION_UNKNOWN"), "Unknown projectile presentation definition should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_SPAWN_PROFILE_DUPLICATE"), "Duplicate projectile spawn profile stable id should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_SPAWN_PROFILE_PROJECTILE_UNKNOWN"), "Unknown projectile spawn profile projectile should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_SPAWN_SOCKET_UNKNOWN"), "Unknown projectile spawn profile socket should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_SPAWN_ORIGIN_KIND_INVALID"), "Invalid projectile spawn origin kind should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_AIM_KIND_INVALID"), "Invalid projectile spawn aim kind should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_AIM_FALLBACK_KIND_INVALID"), "Invalid projectile aim fallback should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_PROJECTILE_AIM_MAX_RANGE_INVALID"), "Invalid projectile aim max range should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_EFFECT_TIMELINE_DUPLICATE"), "Duplicate effect timeline id should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_EFFECT_TIMELINE_DURATION_INVALID"), "Invalid effect timeline duration should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_EFFECT_TIMELINE_EVENT_KIND_INVALID"), "Invalid effect timeline event kind should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_EFFECT_TIMELINE_ANCHOR_MODE_INVALID"), "Invalid effect timeline anchor mode should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_EFFECT_TIMELINE_SHAPE_INVALID"), "Invalid effect timeline shape should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_EFFECT_TIMELINE_TARGET_UNKNOWN"), "Unknown child effect timeline should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_EFFECT_TIMELINE_CYCLE"), "Effect timeline cycle should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "ABILITY_EFFECT_TIMELINE_MAX_DEPTH"), "Effect timeline max child depth should be diagnosed.");
    }

    private static void AbilityAuthoringPackageLoader_InvalidProjectileRequestFiles_BlockValidation()
    {
        string packageDir = Path.Combine(Path.GetTempPath(), "mx-authoring-projectile-requests-" + Guid.NewGuid().ToString("N"));
        try
        {
            string configDir = Path.Combine(packageDir, "config");
            string projectileDir = Path.Combine(configDir, "projectiles");
            Directory.CreateDirectory(projectileDir);
            var root = new AbilityAuthoringDocument
            {
                PackageId = "invalid.projectile.requests"
            };
            root.ProjectileDefinitions.Add(new ProjectileDefinitionConfig
            {
                DefinitionId = 10,
                StableId = "projectile.definition"
            });

            File.WriteAllText(
                Path.Combine(configDir, "ability_authoring.json"),
                JsonSerializer.Serialize(root, JsonOptions));

            var request = new ProjectileRequestAuthoringDocument
            {
                SchemaVersion = "1",
                StableId = "projectile.other",
                Definition = new ProjectileDefinitionConfig
                {
                    DefinitionId = 10,
                    StableId = "projectile.definition",
                    Collision = new ProjectileCollisionConfig
                    {
                        LayerMask = 123
                    },
                    Impact = new ProjectileImpactConfig
                    {
                        PolicyKind = "AreaPulseOnImpact",
                        AreaLayerMask = 456
                    }
                },
                SpawnProfile = new ProjectileSpawnProfileConfig
                {
                    StableId = "projectile.spawn",
                    ProjectileStableId = "projectile.missing",
                    Origin = new ProjectileSpawnOriginConfig
                    {
                        Kind = "Socket",
                        SocketLabel = "mainHand"
                    },
                    Aim = new ProjectileAimConfig
                    {
                        Kind = "CasterForwardRange"
                    }
                },
                PresentationBinding = new ProjectilePresentationBindingConfig
                {
                    ProjectileDefinitionId = 99,
                    PrefabResource = new AbilityResourceKeyConfig
                    {
                        Id = "vfx.test",
                        TypeId = "GameObject",
                        PackageId = "mxframework.samples"
                    }
                }
            };

            File.WriteAllText(
                Path.Combine(projectileDir, "projectile.file.json"),
                JsonSerializer.Serialize(request, JsonOptions));

            AbilityAuthoringDocument loaded = AbilityAuthoringPackageLoader.Load(packageDir, JsonOptions);
            AbilityAuthoringCompileResult result = AbilityAuthoringCompiler.Compile(loaded);

            Require(result.ValidationReport.HasBlockingIssues, "Invalid projectile request files should block validation.");
            Require(HasDiagnostic(result, "ABILITY_PROJECTILE_REQUEST_FILE_STABLE_ID_MISMATCH"), "File name and top-level stableId mismatch should be diagnosed.");
            Require(HasDiagnostic(result, "ABILITY_PROJECTILE_REQUEST_SPAWN_STABLE_ID_MISMATCH"), "Top-level stableId and spawn profile stableId mismatch should be diagnosed.");
            Require(HasDiagnostic(result, "ABILITY_PROJECTILE_REQUEST_PROJECTILE_STABLE_ID_MISMATCH"), "Spawn profile projectileStableId and definition stableId mismatch should be diagnosed.");
            Require(HasDiagnostic(result, "ABILITY_PROJECTILE_REQUEST_PRESENTATION_DEFINITION_MISMATCH"), "Presentation binding and definition id mismatch should be diagnosed.");
            Require(HasDiagnostic(result, "ABILITY_PROJECTILE_REQUEST_COLLISION_LAYER_MASK_RAW"), "Per-ID projectile source raw collision layer mask should be diagnosed.");
            Require(HasDiagnostic(result, "ABILITY_PROJECTILE_REQUEST_AREA_LAYER_MASK_RAW"), "Per-ID projectile source raw area layer mask should be diagnosed.");
            Require(HasDiagnostic(result, "ABILITY_PROJECTILE_DEFINITION_ID_DUPLICATE"), "Root and per-ID duplicate definition ids should be diagnosed.");
            Require(HasDiagnostic(result, "ABILITY_PROJECTILE_STABLE_ID_DUPLICATE"), "Root and per-ID duplicate definition stable ids should be diagnosed.");
        }
        finally
        {
            if (Directory.Exists(packageDir))
                Directory.Delete(packageDir, recursive: true);
        }
    }

    private static void AbilityPackageCommands_Compile_WritesArtifacts()
    {
        string outDir = Path.Combine(Path.GetTempPath(), "mx-authoring-ability-" + Guid.NewGuid().ToString("N"));
        try
        {
            int exit = AbilityPackageCommands.Dispatch(new[]
            {
                "ability",
                "compile",
                "--package",
                FindSamplePath("ability-character-test"),
                "--vfx-definitions",
                FindGlobalVfxDefinitionsPath(),
                "--out",
                outDir
            }, MxFramework.Authoring.Cli.Program.CreateJsonOptions());

            Require(exit == MxFramework.Authoring.Cli.Program.ExitReady, "Ability compile command should return ready.");
            Require(File.Exists(Path.Combine(outDir, "ability_catalog.json")), "Ability compile should write ability catalog.");
            Require(File.Exists(Path.Combine(outDir, "ability_catalog.bytes")), "Ability compile should write ability catalog bytes.");
            Require(!File.Exists(Path.Combine(outDir, "damage_profiles.json")), "Ability compile should not write legacy damage profiles.");
            Require(!File.Exists(Path.Combine(outDir, "damage_profiles.bytes")), "Ability compile should not write legacy damage profile bytes.");
            Require(File.Exists(Path.Combine(outDir, "vfx_definitions.json")), "Ability compile should write VFX definitions.");
            Require(File.Exists(Path.Combine(outDir, "vfx_definitions.bytes")), "Ability compile should write VFX definition bytes.");
            Require(File.Exists(Path.Combine(outDir, "vfx_trigger_maps.json")), "Ability compile should write VFX trigger maps.");
            Require(File.Exists(Path.Combine(outDir, "vfx_trigger_maps.bytes")), "Ability compile should write VFX trigger map bytes.");
            Require(File.Exists(Path.Combine(outDir, "ability_effect_timelines.json")), "Ability compile should write effect timelines.");
            Require(File.Exists(Path.Combine(outDir, "ability_effect_timelines.bytes")), "Ability compile should write effect timeline bytes.");
            Require(File.Exists(Path.Combine(outDir, "projectile_definitions.json")), "Ability compile should write projectile definitions.");
            Require(File.Exists(Path.Combine(outDir, "projectile_definitions.bytes")), "Ability compile should write projectile definition bytes.");
            Require(File.Exists(Path.Combine(outDir, "projectile_spawn_profiles.json")), "Ability compile should write projectile spawn profiles.");
            Require(File.Exists(Path.Combine(outDir, "projectile_spawn_profiles.bytes")), "Ability compile should write projectile spawn profile bytes.");
            Require(File.Exists(Path.Combine(outDir, "projectile_presentation_bindings.json")), "Ability compile should write projectile presentation bindings.");
            Require(File.Exists(Path.Combine(outDir, "projectile_presentation_bindings.bytes")), "Ability compile should write projectile presentation binding bytes.");
            Require(File.Exists(Path.Combine(outDir, "character_test_mechanic_tunings.json")), "Ability compile should write CharacterTest mechanic tuning.");
            string abilityJson = File.ReadAllText(Path.Combine(outDir, "ability_catalog.json"));
            Require(abilityJson.Contains("ability.character_test.fireball", StringComparison.Ordinal), "Ability catalog output should contain Fireball.");
            Require(abilityJson.Contains("ability.character_test.fireball_explosion", StringComparison.Ordinal), "Ability catalog output should contain Fireball Explosion.");
            Require(abilityJson.Contains("\"frames\": 60", StringComparison.Ordinal), "Ability catalog output should contain Fireball cooldown.");
            Require(abilityJson.Contains("character_test.double_explosion", StringComparison.Ordinal), "Ability catalog output should contain effect timeline id.");
            string effectTimelineJson = File.ReadAllText(Path.Combine(outDir, "ability_effect_timelines.json"));
            Require(effectTimelineJson.Contains("character_test.double_explosion", StringComparison.Ordinal), "Effect timeline output should contain Double Explosion.");
            Require(effectTimelineJson.Contains("character_test.fireball_explosion.afterblast", StringComparison.Ordinal), "Effect timeline output should contain Fireball Explosion afterblast.");
            Require(effectTimelineJson.Contains("\"frame\": 120", StringComparison.Ordinal), "Effect timeline output should contain the delayed pulse frame.");
            string vfxJson = File.ReadAllText(Path.Combine(outDir, "vfx_definitions.json"));
            Require(vfxJson.Contains("vfx.magic.fireball.charge", StringComparison.Ordinal), "VFX definition output should contain Fireball charge.");
            Require(vfxJson.Contains("vfx.magic.fireball_burst.charge", StringComparison.Ordinal), "VFX definition output should contain Fireball Burst charge.");
            Require(vfxJson.Contains("vfx.magic.fireball_explosion.impact", StringComparison.Ordinal), "VFX definition output should contain Fireball Explosion impact.");
            Require(vfxJson.Contains("vfx.magic.guard_aura", StringComparison.Ordinal), "VFX definition output should contain Guard Aura.");
            Require(vfxJson.Contains("vfx.magic.area_confinement.character", StringComparison.Ordinal), "VFX definition output should contain Area Confinement character.");
            Require(vfxJson.Contains("vfx.magic.area_confinement.telegraph", StringComparison.Ordinal), "VFX definition output should contain Area Confinement telegraph.");
            Require(vfxJson.Contains("vfx.magic.area_confinement.telegraph_cancel", StringComparison.Ordinal), "VFX definition output should contain Area Confinement telegraph cancel.");
            Require(vfxJson.Contains("vfx.magic.area_confinement.character_release", StringComparison.Ordinal), "VFX definition output should contain Area Confinement character release.");
            string mechanicTuningJson = File.ReadAllText(Path.Combine(outDir, "character_test_mechanic_tunings.json"));
            Require(mechanicTuningJson.Contains("\"telegraphFrames\": 60", StringComparison.Ordinal), "CharacterTest mechanic tuning output should contain the 60 frame telegraph.");
            Require(mechanicTuningJson.Contains("\"interruptPolicy\": \"CancelOnDamageOrActionExit\"", StringComparison.Ordinal), "CharacterTest mechanic tuning output should contain the interrupt policy.");
            string projectileJson = File.ReadAllText(Path.Combine(outDir, "projectile_definitions.json"));
            Require(projectileJson.Contains("character_test.fireball.projectile", StringComparison.Ordinal), "Projectile definition output should contain Fireball projectile.");
            Require(projectileJson.Contains("character_test.fireball_burst.projectile", StringComparison.Ordinal), "Projectile definition output should contain Fireball Burst projectile.");
            Require(projectileJson.Contains("character_test.flame_lance.projectile", StringComparison.Ordinal), "Projectile definition output should contain Flame Lance projectile.");
            Require(projectileJson.Contains("character_test.fireball_lob.projectile", StringComparison.Ordinal), "Projectile definition output should contain Fireball Lob projectile.");
            Require(projectileJson.Contains("character_test.fireball_surge.projectile", StringComparison.Ordinal), "Projectile definition output should contain Fireball Surge projectile.");
            Require(projectileJson.Contains("character_test.fireball_return.projectile", StringComparison.Ordinal), "Projectile definition output should contain Fireball Return projectile.");
            Require(projectileJson.Contains("character_test.fireball_sway.projectile", StringComparison.Ordinal), "Projectile definition output should contain Fireball Sway projectile.");
            Require(projectileJson.Contains("character_test.fireball_homing.projectile", StringComparison.Ordinal), "Projectile definition output should contain Fireball Homing projectile.");
            Require(projectileJson.Contains("character_test.fireball_bezier.projectile", StringComparison.Ordinal), "Projectile definition output should contain Fireball Bezier projectile.");
            Require(projectileJson.Contains("character_test.fireball_explosion.projectile", StringComparison.Ordinal), "Projectile definition output should contain Fireball Explosion projectile.");
            Require(projectileJson.Contains("ArcToTarget", StringComparison.Ordinal), "Projectile definition output should contain ArcToTarget motion.");
            Require(projectileJson.Contains("BallisticArcToTarget", StringComparison.Ordinal), "Projectile definition output should contain BallisticArcToTarget motion.");
            Require(projectileJson.Contains("\"gravityPerFrameSquared\": 0.008333333", StringComparison.Ordinal), "Projectile definition output should contain Poison Bottle gravity.");
            Require(projectileJson.Contains("BoomerangReturn", StringComparison.Ordinal), "Projectile definition output should contain BoomerangReturn motion.");
            Require(projectileJson.Contains("SwayForward", StringComparison.Ordinal), "Projectile definition output should contain SwayForward motion.");
            Require(projectileJson.Contains("HomingToTarget", StringComparison.Ordinal), "Projectile definition output should contain HomingToTarget motion.");
            Require(projectileJson.Contains("BezierToTarget", StringComparison.Ordinal), "Projectile definition output should contain BezierToTarget motion.");
            Require(projectileJson.Contains("EaseInQuadratic", StringComparison.Ordinal), "Projectile definition output should contain EaseInQuadratic speed profile.");
            Require(projectileJson.Contains("AreaPulseOnImpact", StringComparison.Ordinal), "Projectile definition output should contain the area policy.");
            Require(projectileJson.Contains("\"physicalEventsEnabled\": true", StringComparison.Ordinal), "Projectile definition output should contain enabled physical events.");
            Require(projectileJson.Contains("character_test.double_explosion", StringComparison.Ordinal), "Projectile definition output should contain effect timeline id.");
            Require(projectileJson.Contains("character_test.fireball_explosion.afterblast", StringComparison.Ordinal), "Projectile definition output should contain Fireball Explosion effect timeline id.");
            Require(projectileJson.Contains("\"onHitBuffs\"", StringComparison.Ordinal), "Projectile definition output should contain on-hit Buff config.");
            Require(projectileJson.Contains("\"buffId\": 601009", StringComparison.Ordinal), "Projectile definition output should contain on-hit Buff id.");
            Require(projectileJson.Contains("OncePerProjectile", StringComparison.Ordinal), "Projectile definition output should contain the same-target policy.");
            string projectilePresentationJson = File.ReadAllText(Path.Combine(outDir, "projectile_presentation_bindings.json"));
            Require(projectilePresentationJson.Contains("vfx.magic.fireball", StringComparison.Ordinal), "Projectile presentation output should contain Fireball resource.");
            Require(projectilePresentationJson.Contains("vfx.magic.fireball_burst.flight", StringComparison.Ordinal), "Projectile presentation output should contain Fireball Burst flight resource.");
            Require(projectilePresentationJson.Contains("vfx.magic.flame_lance.flight", StringComparison.Ordinal), "Projectile presentation output should contain Flame Lance flight resource.");
            Require(projectilePresentationJson.Contains("91004", StringComparison.Ordinal), "Projectile presentation output should contain Fireball Lob binding.");
            Require(projectilePresentationJson.Contains("91006", StringComparison.Ordinal), "Projectile presentation output should contain Fireball Return binding.");
            Require(projectilePresentationJson.Contains("91007", StringComparison.Ordinal), "Projectile presentation output should contain Fireball Sway binding.");
            Require(projectilePresentationJson.Contains("91008", StringComparison.Ordinal), "Projectile presentation output should contain Fireball Homing binding.");
            Require(projectilePresentationJson.Contains("91009", StringComparison.Ordinal), "Projectile presentation output should contain Fireball Bezier binding.");
            Require(projectilePresentationJson.Contains("91010", StringComparison.Ordinal), "Projectile presentation output should contain Fireball Explosion binding.");
            string projectileSpawnJson = File.ReadAllText(Path.Combine(outDir, "projectile_spawn_profiles.json"));
            Require(projectileSpawnJson.Contains("character_test.fireball.projectile", StringComparison.Ordinal), "Projectile spawn output should contain Fireball spawn profile.");
            Require(projectileSpawnJson.Contains("character_test.fireball_burst.projectile", StringComparison.Ordinal), "Projectile spawn output should contain Fireball Burst spawn profile.");
            Require(projectileSpawnJson.Contains("character_test.flame_lance.projectile", StringComparison.Ordinal), "Projectile spawn output should contain Flame Lance spawn profile.");
            Require(projectileSpawnJson.Contains("character_test.fireball_lob.projectile", StringComparison.Ordinal), "Projectile spawn output should contain Fireball Lob spawn profile.");
            Require(projectileSpawnJson.Contains("character_test.fireball_surge.projectile", StringComparison.Ordinal), "Projectile spawn output should contain Fireball Surge spawn profile.");
            Require(projectileSpawnJson.Contains("character_test.fireball_return.projectile", StringComparison.Ordinal), "Projectile spawn output should contain Fireball Return spawn profile.");
            Require(projectileSpawnJson.Contains("character_test.fireball_sway.projectile", StringComparison.Ordinal), "Projectile spawn output should contain Fireball Sway spawn profile.");
            Require(projectileSpawnJson.Contains("character_test.fireball_homing.projectile", StringComparison.Ordinal), "Projectile spawn output should contain Fireball Homing spawn profile.");
            Require(projectileSpawnJson.Contains("character_test.fireball_bezier.projectile", StringComparison.Ordinal), "Projectile spawn output should contain Fireball Bezier spawn profile.");
            Require(projectileSpawnJson.Contains("character_test.fireball_explosion.projectile", StringComparison.Ordinal), "Projectile spawn output should contain Fireball Explosion spawn profile.");
            Require(projectileSpawnJson.Contains("mainHand", StringComparison.Ordinal), "Projectile spawn output should contain mainHand socket.");
            Require(projectileSpawnJson.Contains("CasterForwardRange", StringComparison.Ordinal), "Projectile spawn output should contain the forward range aim policy.");
        }
        finally
        {
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, recursive: true);
        }
    }

    private static void AbilityPackageCommands_ImportUnityDryRun_ReportsProjectileArtifacts()
    {
        string reportDir = Path.Combine(Path.GetTempPath(), "mx-authoring-ability-import-" + Guid.NewGuid().ToString("N"));
        try
        {
            int exit = AbilityPackageCommands.Dispatch(new[]
            {
                "ability",
                "import-unity",
                "--package",
                FindSamplePath("ability-character-test"),
                "--project-root",
                FindRepoRoot(),
                "--vfx-definitions",
                FindGlobalVfxDefinitionsPath(),
                "--dry-run",
                "--report-out",
                reportDir
            }, MxFramework.Authoring.Cli.Program.CreateJsonOptions());

            Require(exit == MxFramework.Authoring.Cli.Program.ExitReady, "Ability import-unity dry-run should return ready.");
            string reportPath = Path.Combine(reportDir, "ability_import_report.json");
            Require(File.Exists(reportPath), "Ability import-unity dry-run should write a report.");
            string reportJson = File.ReadAllText(reportPath);
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_definitions.json", StringComparison.Ordinal), "Ability import-unity report should include projectile definitions.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_definitions.bytes", StringComparison.Ordinal), "Ability import-unity report should include projectile definition bytes.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Abilities/ability_effect_timelines.json", StringComparison.Ordinal), "Ability import-unity report should include effect timelines.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Abilities/ability_effect_timelines.bytes", StringComparison.Ordinal), "Ability import-unity report should include effect timeline bytes.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_spawn_profiles.json", StringComparison.Ordinal), "Ability import-unity report should include projectile spawn profiles.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_spawn_profiles.bytes", StringComparison.Ordinal), "Ability import-unity report should include projectile spawn profile bytes.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_presentation_bindings.json", StringComparison.Ordinal), "Ability import-unity report should include projectile presentation bindings.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_presentation_bindings.bytes", StringComparison.Ordinal), "Ability import-unity report should include projectile presentation binding bytes.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Abilities/character_test_mechanic_tunings.json", StringComparison.Ordinal), "Ability import-unity report should include CharacterTest mechanic tuning.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Abilities/ability_catalog.bytes", StringComparison.Ordinal), "Ability import-unity report should include ability catalog bytes.");
            Require(!reportJson.Contains("Assets/MxFrameworkGenerated/Combat/Damage/damage_profiles.bytes", StringComparison.Ordinal), "Ability import-unity report should not include damage profile bytes.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Vfx/vfx_definitions.bytes", StringComparison.Ordinal), "Ability import-unity report should include VFX definition bytes.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Vfx/vfx_trigger_maps.bytes", StringComparison.Ordinal), "Ability import-unity report should include VFX trigger map bytes.");
        }
        finally
        {
            if (Directory.Exists(reportDir))
                Directory.Delete(reportDir, recursive: true);
        }
    }

    private static string FindGlobalVfxDefinitionsPath() =>
        Path.Combine(FindRepoRoot(), "Assets", "MxFrameworkGenerated", "Vfx", "vfx_definitions.json");

    private static IReadOnlyList<VfxDefinitionConfig> ReadGlobalVfxDefinitions()
    {
        AbilityVfxDefinitionCatalogDocument catalog = JsonSerializer.Deserialize<AbilityVfxDefinitionCatalogDocument>(
            File.ReadAllText(FindGlobalVfxDefinitionsPath()),
            JsonOptions);
        return catalog?.Definitions ?? new List<VfxDefinitionConfig>();
    }

    private static string FindSamplePath(string sampleName)
    {
        string root = FindRepoRoot();
        return Path.Combine(root, "Tools", "MxFramework.Authoring", "samples", sampleName);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Assets")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Tools")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static bool HasDiagnostic(AbilityAuthoringCompileResult result, string code)
    {
        return result.ValidationReport.Diagnostics.Exists(diagnostic => diagnostic.Code == code);
    }

    private static ProjectileDefinitionConfig FindProjectileDefinition(
        AbilityAuthoringCompileResult result,
        string stableId)
    {
        ProjectileDefinitionConfig definition =
            result.ProjectileDefinitionCatalog.Definitions.Find(value => value.StableId == stableId);
        Require(definition != null, "Missing projectile definition: " + stableId);
        return definition;
    }

    private static ProjectileSpawnProfileConfig FindProjectileSpawnProfile(
        AbilityAuthoringCompileResult result,
        string stableId)
    {
        ProjectileSpawnProfileConfig profile =
            result.ProjectileSpawnProfileCatalog.Profiles.Find(value => value.StableId == stableId);
        Require(profile != null, "Missing projectile spawn profile: " + stableId);
        return profile;
    }

    private static ProjectilePresentationBindingConfig FindProjectilePresentationBinding(
        AbilityAuthoringCompileResult result,
        int definitionId)
    {
        ProjectilePresentationBindingConfig binding =
            result.ProjectilePresentationBindings.Bindings.Find(value => value.ProjectileDefinitionId == definitionId);
        Require(binding != null, "Missing projectile presentation binding: " + definitionId);
        return binding;
    }
}
