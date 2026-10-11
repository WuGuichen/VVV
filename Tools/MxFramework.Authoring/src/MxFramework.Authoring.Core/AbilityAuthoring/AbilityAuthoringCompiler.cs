using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MxFramework.Authoring
{
    public static class AbilityAuthoringCompiler
    {
        public const string AbilityAuthoringDocumentRelativePath = "config/ability_authoring.json";
        public const int VfxSourceDomainCombatHit = 21;
        public const int VfxSourceDomainActionPresentation = 30;
        public const int MaxEffectTimelineChildDepth = 8;

        private const int CharacterTestDynamicCharacterLayerMask = 1 << 8;
        private const int CharacterTestStaticEnvironmentLayerMask = 1 << 1;

        private static readonly HashSet<string> KnownFormulaIds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Flat",
                "AttackMinusDefense",
                "AttackScaledMinusDefense",
                "ct.soft_defense_v1"
            };

        private static readonly HashSet<string> KnownCombatHitTargetTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "None",
                "HostileCharacter",
                "FriendlyCharacter",
                "Self",
                "ThirdPartyCharacter",
                "NeutralCharacter",
                "DamageableObject",
                "WorldWall",
                "WorldGround",
                "WorldStatic"
            };

        private static readonly HashSet<string> KnownCombatEnvironmentHitPolicies =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "None",
                "BlockedPresentation"
            };

        private static readonly HashSet<string> KnownCombatHitSameTargetPolicies =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "AllowRepeat",
                "OncePerEvent"
            };

        public static AbilityAuthoringCompileResult Compile(
            AbilityAuthoringDocument document,
            IReadOnlyList<VfxDefinitionConfig> resolutionVfxDefinitions = null)
        {
            document ??= new AbilityAuthoringDocument();
            Normalize(document, resolutionVfxDefinitions);
            AbilityAuthoringValidationReport validation = Validate(document, resolutionVfxDefinitions);

            var result = new AbilityAuthoringCompileResult
            {
                PackageId = document.PackageId ?? string.Empty,
                ValidationReport = validation,
                AbilityCatalog = BuildAbilityCatalog(document),
                VfxDefinitionCatalog = BuildVfxDefinitionCatalog(document),
                VfxTriggerMap = BuildVfxTriggerMap(document),
                ActionReferenceCatalog = BuildActionReferenceCatalog(document),
                ProjectileDefinitionCatalog = BuildProjectileDefinitionCatalog(document),
                ProjectileSpawnProfileCatalog = BuildProjectileSpawnProfileCatalog(document),
                ProjectilePresentationBindings = BuildProjectilePresentationBindings(document),
                EffectTimelineCatalog = BuildEffectTimelineCatalog(document),
                CharacterTestMechanicTuningCatalog = BuildCharacterTestMechanicTuningCatalog(document)
            };

            return result;
        }

        public static AbilityAuthoringValidationReport Validate(
            AbilityAuthoringDocument document,
            IReadOnlyList<VfxDefinitionConfig> resolutionVfxDefinitions = null)
        {
            document ??= new AbilityAuthoringDocument();
            Normalize(document, resolutionVfxDefinitions);

            var report = new AbilityAuthoringValidationReport
            {
                PackageId = document.PackageId ?? string.Empty
            };
            for (int i = 0; i < document.SourceDiagnostics.Count; i++)
            {
                AbilityAuthoringDiagnostic diagnostic = document.SourceDiagnostics[i];
                if (diagnostic != null)
                    report.Diagnostics.Add(diagnostic);
            }

            if (!string.Equals(document.SchemaVersion, "1", StringComparison.Ordinal))
            {
                Error(report, "ABILITY_SCHEMA_UNSUPPORTED", "schemaVersion", "Ability authoring schemaVersion must be 1.");
            }

            var abilityIds = new HashSet<int>();
            var abilityStableIds = new HashSet<string>(StringComparer.Ordinal);
            var actionIds = new HashSet<int>();
            var vfxDefinitionIds = new HashSet<int>();
            var presentationProfileIds = new HashSet<int>();
            var projectileDefinitionIds = new HashSet<int>();
            var projectileStableIds = new HashSet<string>(StringComparer.Ordinal);
            var projectileSpawnProfileIds = new HashSet<string>(StringComparer.Ordinal);
            var projectileSpawnProfileNumericIds = new HashSet<int>();
            var projectilePresentationDefinitionIds = new HashSet<int>();
            var effectTimelineIds = new HashSet<int>();
            var mechanicTuningStableIds = new HashSet<string>(StringComparer.Ordinal);
            var resourceKeys = BuildResourceKeySet(document);
            var knownSocketIds = BuildKnownSocketIdSet(document);

            for (int i = 0; i < document.VfxDefinitions.Count; i++)
            {
                VfxDefinitionConfig definition = document.VfxDefinitions[i];
                string path = "vfxDefinitions[" + i + "]";
                if (definition == null)
                {
                    Error(report, "ABILITY_VFX_DEFINITION_NULL", path, "VFX definition entry is null.");
                    continue;
                }

                if (definition.DefinitionId <= 0)
                    Error(report, "ABILITY_VFX_DEFINITION_ID_INVALID", path + ".definitionId", "VFX definition id must be positive.");
                else if (!vfxDefinitionIds.Add(definition.DefinitionId))
                    Error(report, "ABILITY_VFX_DEFINITION_ID_DUPLICATE", path + ".definitionId", "Duplicate VFX definition id: " + definition.DefinitionId + ".");
                RequireId(report, definition.StableId, path + ".stableId", "VFX definition stable id is required.");
                if (definition.PrefabResource == null ||
                    string.IsNullOrWhiteSpace(definition.PrefabResource.Id) ||
                    string.IsNullOrWhiteSpace(definition.PrefabResource.TypeId))
                {
                    Error(report, "ABILITY_VFX_RESOURCE_KEY_MISSING", path + ".prefabResource", "VFX definition prefab resource key is required.");
                }
                else if (resourceKeys.Count > 0 && !resourceKeys.Contains(ToResourceKey(definition.PrefabResource)))
                {
                    Error(report, "ABILITY_VFX_RESOURCE_KEY_UNKNOWN", path + ".prefabResource", "VFX definition references a resource key that is not declared in resourceRefs: " + ToResourceKey(definition.PrefabResource) + ".");
                }
            }

            for (int i = 0; i < document.CharacterTestMechanicTunings.Count; i++)
            {
                CharacterTestMechanicTuningConfig tuning = document.CharacterTestMechanicTunings[i];
                string path = "characterTestMechanicTunings[" + i + "]";
                if (tuning == null)
                {
                    Error(report, "ABILITY_CT_MECHANIC_TUNING_NULL", path, "CharacterTest mechanic tuning entry is null.");
                    continue;
                }

                RequireId(report, tuning.StableId, path + ".stableId", "CharacterTest mechanic tuning stable id is required.");
                if (!string.IsNullOrWhiteSpace(tuning.StableId) && !mechanicTuningStableIds.Add(tuning.StableId))
                    Error(report, "ABILITY_CT_MECHANIC_TUNING_DUPLICATE", path + ".stableId", "Duplicate CharacterTest mechanic tuning stable id: " + tuning.StableId + ".");
                RequireId(report, tuning.AbilityStableId, path + ".abilityStableId", "CharacterTest mechanic tuning ability stable id is required.");
                RequireId(report, tuning.SourceStableId, path + ".sourceStableId", "CharacterTest mechanic tuning source stable id is required.");
                if (string.Equals(tuning.StableId, "mechanic_tuning.character_test.combat_evade", StringComparison.Ordinal))
                {
                    if (tuning.MaxStamina <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_STAMINA_MAX_INVALID", path + ".maxStamina", "Combat evade max stamina must be positive.");
                    if (tuning.RegenerationDelayFrames < 0)
                        Error(report, "ABILITY_CT_MECHANIC_REGEN_DELAY_INVALID", path + ".regenerationDelayFrames", "Combat evade regeneration delay cannot be negative.");
                    if (tuning.RegenerationPerSecond <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_REGEN_RATE_INVALID", path + ".regenerationPerSecond", "Combat evade regeneration per second must be positive.");
                }
                else if (string.Equals(tuning.StableId, "mechanic_tuning.character_test.arc_mine", StringComparison.Ordinal))
                {
                    if (tuning.DeployRangeMeters <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_DEPLOY_RANGE_INVALID", path + ".deployRangeMeters", "Arc Mine deploy range meters must be positive.");
                    if (tuning.FallbackForwardMeters <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_FALLBACK_RANGE_INVALID", path + ".fallbackForwardMeters", "Arc Mine fallback forward meters must be positive.");
                    int fuseDelayFrames = tuning.FuseDelayFrames > 0
                        ? tuning.FuseDelayFrames
                        : tuning.ArmDelayFrames;
                    if (fuseDelayFrames <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_FUSE_DELAY_INVALID", path + ".fuseDelayFrames", "Arc Mine fuse delay frames must be positive.");
                    if (tuning.DurationFrames <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_DURATION_INVALID", path + ".durationFrames", "Arc Mine duration frames must be positive.");
                    if (tuning.TriggerRadiusCentimeters <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_TRIGGER_RADIUS_INVALID", path + ".triggerRadiusCentimeters", "Arc Mine trigger radius centimeters must be positive.");
                    if (tuning.BlastRadiusCentimeters <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_BLAST_RADIUS_INVALID", path + ".blastRadiusCentimeters", "Arc Mine blast radius centimeters must be positive.");
                    if (tuning.MaxBlastTargets <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_MAX_TARGETS_INVALID", path + ".maxBlastTargets", "Arc Mine max blast targets must be positive.");
                    if (tuning.FuseVfxDefinitionId < 0)
                        Error(report, "ABILITY_CT_MECHANIC_FUSE_VFX_INVALID", path + ".fuseVfxDefinitionId", "Arc Mine fuse VFX definition id cannot be negative.");
                    if (tuning.DeployVfxDefinitionId <= 0 || tuning.TriggerVfxDefinitionId <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_VFX_INVALID", path, "Arc Mine tuning must reference positive deploy and trigger VFX definition ids.");
                }
                else if (string.Equals(tuning.StableId, "mechanic_tuning.character_test.rune_turret", StringComparison.Ordinal))
                {
                    if (tuning.DeployRangeMeters <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_DEPLOY_RANGE_INVALID", path + ".deployRangeMeters", "Rune Turret deploy range meters must be positive.");
                    if (tuning.FallbackForwardMeters <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_FALLBACK_RANGE_INVALID", path + ".fallbackForwardMeters", "Rune Turret fallback forward meters must be positive.");
                    if (tuning.WarmupFrames < 0)
                        Error(report, "ABILITY_CT_MECHANIC_WARMUP_INVALID", path + ".warmupFrames", "Rune Turret warmup frames cannot be negative.");
                    if (tuning.DurationFrames <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_DURATION_INVALID", path + ".durationFrames", "Rune Turret duration frames must be positive.");
                    if (tuning.ScanRadiusCentimeters <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_SCAN_RADIUS_INVALID", path + ".scanRadiusCentimeters", "Rune Turret scan radius centimeters must be positive.");
                    if (tuning.ScanIntervalFrames <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_SCAN_INTERVAL_INVALID", path + ".scanIntervalFrames", "Rune Turret scan interval frames must be positive.");
                    if (tuning.FireIntervalFrames <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_FIRE_INTERVAL_INVALID", path + ".fireIntervalFrames", "Rune Turret fire interval frames must be positive.");
                    if (tuning.MaxShots <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_MAX_SHOTS_INVALID", path + ".maxShots", "Rune Turret max shots must be positive.");
                    if (tuning.MuzzleHeightCentimeters <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_MUZZLE_HEIGHT_INVALID", path + ".muzzleHeightCentimeters", "Rune Turret muzzle height centimeters must be positive.");
                    if (tuning.DeployVfxDefinitionId <= 0
                        || tuning.IdleVfxDefinitionId <= 0
                        || tuning.FireVfxDefinitionId <= 0
                        || tuning.ExpireVfxDefinitionId <= 0)
                    {
                        Error(report, "ABILITY_CT_MECHANIC_VFX_INVALID", path, "Rune Turret tuning must reference positive deploy, idle, fire, and expire VFX definition ids.");
                    }
                }
                else
                {
                    if (tuning.TelegraphFrames < 0)
                        Error(report, "ABILITY_CT_MECHANIC_TELEGRAPH_INVALID", path + ".telegraphFrames", "Telegraph frames cannot be negative.");
                    if (tuning.ZoneDurationFrames <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_ZONE_DURATION_INVALID", path + ".zoneDurationFrames", "Zone duration frames must be positive.");
                    if (tuning.ZoneRadiusCentimeters <= 0)
                        Error(report, "ABILITY_CT_MECHANIC_RADIUS_INVALID", path + ".zoneRadiusCentimeters", "Zone radius centimeters must be positive.");
                    if (tuning.TelegraphVfxDefinitionId <= 0
                        || tuning.ZoneVfxDefinitionId <= 0
                        || tuning.CharacterVfxDefinitionId <= 0)
                    {
                        Error(report, "ABILITY_CT_MECHANIC_VFX_INVALID", path, "CharacterTest mechanic tuning must reference positive VFX definition ids.");
                    }
                }
            }

            presentationProfileIds.Clear();
            for (int i = 0; i < document.VfxPresentationProfiles.Count; i++)
            {
                VfxPresentationProfileConfig profile = document.VfxPresentationProfiles[i];
                string path = "vfxPresentationProfiles[" + i + "]";
                if (profile == null)
                {
                    Error(report, "ABILITY_VFX_PROFILE_NULL", path, "VFX presentation profile entry is null.");
                    continue;
                }

                RequireNumericId(report, profile.PresentationProfileId, path + ".presentationProfileId", "VFX presentation profile id is required.");
                if (profile.PresentationProfileId > 0 && !presentationProfileIds.Add(profile.PresentationProfileId))
                    Error(report, "ABILITY_VFX_PROFILE_DUPLICATE", path + ".presentationProfileId", "Duplicate VFX presentation profile id: " + profile.PresentationProfileId + ".");
                if (!vfxDefinitionIds.Contains(profile.DefinitionId))
                    Error(report, "ABILITY_VFX_PROFILE_DEFINITION_UNKNOWN", path + ".definitionId", "VFX presentation profile references an unknown definition id: " + profile.DefinitionId + ".");
            }

            for (int i = 0; i < document.VfxTriggerMaps.Count; i++)
            {
                VfxTriggerMapConfig map = document.VfxTriggerMaps[i];
                string path = "vfxTriggerMaps[" + i + "]";
                if (map == null)
                {
                    Error(report, "ABILITY_VFX_TRIGGER_MAP_NULL", path, "VFX trigger map entry is null.");
                    continue;
                }

                if (map.PresentationProfileId <= 0 || !presentationProfileIds.Contains(map.PresentationProfileId))
                    Error(report, "ABILITY_VFX_TRIGGER_PROFILE_UNKNOWN", path + ".presentationProfileId", "VFX trigger map references an unknown presentation profile: " + map.PresentationProfileId + ".");
                if (!vfxDefinitionIds.Contains(map.DefinitionId))
                    Error(report, "ABILITY_VFX_TRIGGER_DEFINITION_UNKNOWN", path + ".definitionId", "VFX trigger map references an unknown definition id: " + map.DefinitionId + ".");
            }

            for (int i = 0; i < document.ProjectileDefinitions.Count; i++)
            {
                ProjectileDefinitionConfig definition = document.ProjectileDefinitions[i];
                string path = "projectileDefinitions[" + i + "]";
                if (definition == null)
                {
                    Error(report, "ABILITY_PROJECTILE_DEFINITION_NULL", path, "Projectile definition entry is null.");
                    continue;
                }

                if (definition.DefinitionId <= 0)
                    Error(report, "ABILITY_PROJECTILE_DEFINITION_ID_INVALID", path + ".definitionId", "Projectile definition id must be positive.");
                else if (!projectileDefinitionIds.Add(definition.DefinitionId))
                    Error(report, "ABILITY_PROJECTILE_DEFINITION_ID_DUPLICATE", path + ".definitionId", "Duplicate projectile definition id: " + definition.DefinitionId + ".");
                RequireId(report, definition.StableId, path + ".stableId", "Projectile stable id is required.");
                if (!string.IsNullOrWhiteSpace(definition.StableId) && !projectileStableIds.Add(definition.StableId))
                    Error(report, "ABILITY_PROJECTILE_STABLE_ID_DUPLICATE", path + ".stableId", "Duplicate projectile stable id: " + definition.StableId + ".");
                string motionKind = "Linear";
                if (definition.Motion == null || !TryResolveProjectileMotionKind(definition.Motion.Kind, out motionKind))
                    Error(report, "ABILITY_PROJECTILE_MOTION_KIND_INVALID", path + ".motion.kind", "Projectile motion kind is unsupported: " + (definition.Motion?.Kind ?? string.Empty) + ".");
                if (definition.Motion == null
                    || definition.Motion.SpeedProfile == null
                    || !TryResolveProjectileSpeedProfileKind(definition.Motion.SpeedProfile.Kind, out _))
                {
                    Error(
                        report,
                        "ABILITY_PROJECTILE_SPEED_PROFILE_KIND_INVALID",
                        path + ".motion.speedProfile.kind",
                        "Projectile speed profile kind is unsupported: " + (definition.Motion?.SpeedProfile?.Kind ?? string.Empty) + ".");
                }
                if (definition.Motion == null || definition.Motion.SpeedPerFrame <= 0f)
                    Error(report, "ABILITY_PROJECTILE_SPEED_INVALID", path + ".motion.speedPerFrame", "Projectile speedPerFrame must be positive.");
                if (definition.Motion == null || definition.Motion.LifetimeFrames <= 0)
                    Error(report, "ABILITY_PROJECTILE_LIFETIME_INVALID", path + ".motion.lifetimeFrames", "Projectile lifetimeFrames must be positive.");
                if (definition.Motion != null
                    && string.Equals(motionKind, "ArcToTarget", StringComparison.Ordinal)
                    && definition.Motion.ArcHeight <= 0f)
                {
                    Error(report, "ABILITY_PROJECTILE_ARC_HEIGHT_INVALID", path + ".motion.arcHeight", "Arc projectile arcHeight must be positive.");
                }

                if (definition.Motion != null
                    && string.Equals(motionKind, "BallisticArcToTarget", StringComparison.Ordinal)
                    && definition.Motion.GravityPerFrameSquared <= 0f)
                {
                    Error(report, "ABILITY_PROJECTILE_GRAVITY_INVALID", path + ".motion.gravityPerFrameSquared", "Ballistic projectile gravityPerFrameSquared must be positive.");
                }

                if (definition.Motion != null
                    && !string.Equals(motionKind, "ArcToTarget", StringComparison.Ordinal)
                    && definition.Motion.ArcHeight < 0f)
                {
                    Error(report, "ABILITY_PROJECTILE_ARC_HEIGHT_INVALID", path + ".motion.arcHeight", "Projectile arcHeight cannot be negative.");
                }
                if (definition.Motion != null
                    && !string.Equals(motionKind, "BallisticArcToTarget", StringComparison.Ordinal)
                    && definition.Motion.GravityPerFrameSquared < 0f)
                {
                    Error(report, "ABILITY_PROJECTILE_GRAVITY_INVALID", path + ".motion.gravityPerFrameSquared", "Projectile gravityPerFrameSquared cannot be negative.");
                }
                if (definition.Motion != null
                    && string.Equals(motionKind, "BallisticArcToTarget", StringComparison.Ordinal)
                    && (definition.Motion.MaxLaunchAngleDegrees < 0f || definition.Motion.MaxLaunchAngleDegrees >= 90f))
                {
                    Error(report, "ABILITY_PROJECTILE_MAX_LAUNCH_ANGLE_INVALID", path + ".motion.maxLaunchAngleDegrees", "Ballistic projectile maxLaunchAngleDegrees must be 0 or greater than 0 and less than 90.");
                }
                if (definition.Motion != null
                    && definition.Motion.MaxLaunchAngleTangent < 0f)
                {
                    Error(report, "ABILITY_PROJECTILE_MAX_LAUNCH_ANGLE_INVALID", path + ".motion.maxLaunchAngleTangent", "Projectile maxLaunchAngleTangent cannot be negative.");
                }
                if (definition.Motion != null
                    && string.Equals(motionKind, "SwayForward", StringComparison.Ordinal))
                {
                    if (definition.Motion.LateralAmplitude <= 0f)
                        Error(report, "ABILITY_PROJECTILE_LATERAL_AMPLITUDE_INVALID", path + ".motion.lateralAmplitude", "Sway projectile lateralAmplitude must be positive.");
                    if (definition.Motion.LateralCycles <= 0)
                        Error(report, "ABILITY_PROJECTILE_LATERAL_CYCLES_INVALID", path + ".motion.lateralCycles", "Sway projectile lateralCycles must be positive.");
                    if (definition.Motion.LateralCycles > 1000)
                        Error(report, "ABILITY_PROJECTILE_LATERAL_CYCLES_INVALID", path + ".motion.lateralCycles", "Sway projectile lateralCycles are too high.");
                }
                else if (definition.Motion != null)
                {
                    if (definition.Motion.LateralAmplitude < 0f)
                        Error(report, "ABILITY_PROJECTILE_LATERAL_AMPLITUDE_INVALID", path + ".motion.lateralAmplitude", "Projectile lateralAmplitude cannot be negative.");
                    if (definition.Motion.LateralCycles < 0)
                        Error(report, "ABILITY_PROJECTILE_LATERAL_CYCLES_INVALID", path + ".motion.lateralCycles", "Projectile lateralCycles cannot be negative.");
                }
                if (definition.Motion != null
                    && string.Equals(motionKind, "BezierToTarget", StringComparison.Ordinal)
                    && IsZero(definition.Motion.BezierControlOffsetA)
                    && IsZero(definition.Motion.BezierControlOffsetB))
                {
                    Error(report, "ABILITY_PROJECTILE_BEZIER_CONTROL_INVALID", path + ".motion.bezierControlOffsetA", "Bezier projectile requires at least one non-zero control offset.");
                }
                if (definition.Motion != null
                    && string.Equals(motionKind, "HomingToTarget", StringComparison.Ordinal)
                    && (definition.Motion.Homing == null
                        || definition.Motion.Homing.TurnRatePerFrame <= 0f
                        || definition.Motion.Homing.TurnRatePerFrame > 1f))
                {
                    Error(report, "ABILITY_PROJECTILE_HOMING_TURN_RATE_INVALID", path + ".motion.homing.turnRatePerFrame", "Homing projectile turnRatePerFrame must be greater than 0 and less than or equal to 1.");
                }
                if (definition.Collision == null || definition.Collision.Radius < 0f)
                    Error(report, "ABILITY_PROJECTILE_RADIUS_INVALID", path + ".collision.radius", "Projectile radius cannot be negative.");
                bool allowNoDirectSweep = AllowsProjectileNoDirectSweep(definition);
                if ((definition.Collision == null || definition.Collision.LayerMask == 0)
                    && !allowNoDirectSweep)
                {
                    Error(report, "ABILITY_PROJECTILE_LAYER_MASK_INVALID", path + ".impact.hitEvent.targetTypes", "Projectile hitEvent.targetTypes must resolve to a non-zero physics layer mask.");
                }
                if (definition.Collision == null || definition.Collision.MaxAcceptedImpacts <= 0)
                    Error(report, "ABILITY_PROJECTILE_HIT_LIMIT_INVALID", path + ".collision.maxAcceptedImpacts", "Projectile maxAcceptedImpacts must be positive.");
                if (definition.Collision == null
                    || !TryResolveProjectileHitSameTargetPolicy(definition.Collision.HitSameTargetPolicy, out _))
                {
                    Error(report, "ABILITY_PROJECTILE_HIT_SAME_TARGET_POLICY_INVALID", path + ".collision.hitSameTargetPolicy", "Projectile hitSameTargetPolicy is unsupported: " + (definition.Collision?.HitSameTargetPolicy ?? string.Empty) + ".");
                }
                string policyKind = "DirectHit";
                if (definition.Impact == null
                    || !TryResolveProjectilePolicy(definition.Impact.PolicyKind, out policyKind))
                {
                    Error(report, "ABILITY_PROJECTILE_POLICY_INVALID", path + ".impact.policyKind", "Projectile impact policyKind is unsupported: " + (definition.Impact?.PolicyKind ?? string.Empty) + ".");
                }
                else if (string.Equals(policyKind, "AreaPulseOnImpact", StringComparison.Ordinal))
                {
                    if (definition.Impact.AreaRadius <= 0f)
                        Error(report, "ABILITY_PROJECTILE_AREA_RADIUS_INVALID", path + ".impact.areaRadius", "Area projectile impact areaRadius must be positive.");
                    if (definition.Impact.AreaLayerMask == 0)
                        Error(report, "ABILITY_PROJECTILE_AREA_LAYER_MASK_INVALID", path + ".impact.areaHitEvent.targetTypes", "Area projectile areaHitEvent.targetTypes must resolve to a non-zero physics layer mask.");
                }
                else if (definition.Impact.TriggerOnExpire)
                {
                    Error(report, "ABILITY_PROJECTILE_TRIGGER_ON_EXPIRE_POLICY_INVALID", path + ".impact.triggerOnExpire", "Projectile triggerOnExpire requires AreaPulseOnImpact policy.");
                }

                if (definition.Impact != null
                    && ((definition.Impact.PresentationProfileId > 0 && !presentationProfileIds.Contains(definition.Impact.PresentationProfileId))
                        || (definition.Impact.PresentationProfileId <= 0 && !string.IsNullOrWhiteSpace(definition.Impact.PresentationProfileLabel))))
                {
                    Error(report, "ABILITY_PROJECTILE_PRESENTATION_PROFILE_UNKNOWN", path + ".impact.presentationProfileId", "Projectile impact references an unknown presentation profile: " + (definition.Impact.PresentationProfileId > 0 ? definition.Impact.PresentationProfileId.ToString() : definition.Impact.PresentationProfileLabel) + ".");
                }

                if (definition.Impact != null)
                {
                    if (definition.Impact.EffectTimelineId > 0)
                        effectTimelineIds.Add(definition.Impact.EffectTimelineId);
                    else if (!string.IsNullOrWhiteSpace(definition.Impact.EffectTimelineLabel))
                        Error(report, "ABILITY_PROJECTILE_EFFECT_TIMELINE_UNKNOWN", path + ".impact.effectTimelineId", "Projectile impact references an unknown effect timeline: " + definition.Impact.EffectTimelineLabel + ".");
                }

                ValidateCombatHitEvent(
                    report,
                    path + ".impact.hitEvent",
                    definition.Impact?.HitEvent,
                    null,
                    true,
                    allowEmptyTargetTypes: allowNoDirectSweep);
                if (definition.Impact != null
                    && string.Equals(policyKind, "AreaPulseOnImpact", StringComparison.Ordinal))
                {
                    ValidateCombatHitEvent(
                        report,
                        path + ".impact.areaHitEvent",
                        definition.Impact.AreaHitEvent,
                        definition.Impact.HitEvent?.Damage,
                        true);
                }
            }

            var definedEffectTimelineIds = new HashSet<int>();
            for (int i = 0; i < document.EffectTimelines.Count; i++)
            {
                AbilityEffectTimelineConfig timeline = document.EffectTimelines[i];
                string path = "effectTimelines[" + i + "]";
                if (timeline == null)
                {
                    Error(report, "ABILITY_EFFECT_TIMELINE_NULL", path, "Effect timeline entry is null.");
                    continue;
                }

                RequireNumericId(report, timeline.TimelineId, path + ".timelineId", "Effect timeline id is required.");
                if (timeline.TimelineId > 0 && !definedEffectTimelineIds.Add(timeline.TimelineId))
                    Error(report, "ABILITY_EFFECT_TIMELINE_DUPLICATE", path + ".timelineId", "Duplicate effect timeline id: " + timeline.TimelineId + ".");
                if (timeline.DurationFrames <= 0)
                    Error(report, "ABILITY_EFFECT_TIMELINE_DURATION_INVALID", path + ".durationFrames", "Effect timeline durationFrames must be positive.");

                var eventIds = new HashSet<int>();
                for (int eventIndex = 0; timeline.Events != null && eventIndex < timeline.Events.Count; eventIndex++)
                {
                    AbilityEffectTimelineEventConfig evt = timeline.Events[eventIndex];
                    string eventPath = path + ".events[" + eventIndex + "]";
                    if (evt == null)
                    {
                        Error(report, "ABILITY_EFFECT_TIMELINE_EVENT_NULL", eventPath, "Effect timeline event entry is null.");
                        continue;
                    }

                    RequireNumericId(report, evt.EventId, eventPath + ".eventId", "Effect timeline event id is required.");
                    if (evt.EventId > 0 && !eventIds.Add(evt.EventId))
                        Error(report, "ABILITY_EFFECT_TIMELINE_EVENT_DUPLICATE", eventPath + ".eventId", "Duplicate effect timeline event id: " + evt.EventId + ".");
                    if (evt.Frame < 0 || evt.Frame >= timeline.DurationFrames)
                        Error(report, "ABILITY_EFFECT_TIMELINE_EVENT_FRAME_INVALID", eventPath + ".frame", "Effect timeline event frame is outside duration.");
                    int endFrame = evt.EndFrame < 0 ? evt.Frame : evt.EndFrame;
                    if (endFrame < evt.Frame || endFrame >= timeline.DurationFrames)
                        Error(report, "ABILITY_EFFECT_TIMELINE_EVENT_RANGE_INVALID", eventPath + ".endFrame", "Effect timeline event endFrame is outside duration or before frame.");
                    if (!IsKnownEffectTrackKind(evt.TrackKind))
                        Error(report, "ABILITY_EFFECT_TIMELINE_TRACK_KIND_INVALID", eventPath + ".trackKind", "Effect timeline trackKind is unsupported: " + (evt.TrackKind ?? string.Empty) + ".");
                    if (!IsKnownEffectEventKind(evt.TrackKind, evt.EventKind))
                        Error(report, "ABILITY_EFFECT_TIMELINE_EVENT_KIND_INVALID", eventPath + ".eventKind", "Effect timeline eventKind is unsupported for its track: " + (evt.EventKind ?? string.Empty) + ".");
                    if (!IsKnownEffectAnchorMode(evt.AnchorMode))
                        Error(report, "ABILITY_EFFECT_TIMELINE_ANCHOR_MODE_INVALID", eventPath + ".anchorMode", "Effect timeline anchorMode is unsupported: " + (evt.AnchorMode ?? string.Empty) + ".");
                    ValidateEffectTimelineShape(report, eventPath, evt);
                    if (string.Equals(evt.EventKind, "StartEffectTimeline", StringComparison.Ordinal)
                        && evt.TargetTimelineId <= 0)
                    {
                        Error(report, "ABILITY_EFFECT_TIMELINE_TARGET_MISSING", eventPath + ".targetTimelineId", "StartEffectTimeline event requires targetTimelineId.");
                    }
                }
            }

            ValidateEffectTimelineReferences(report, document.EffectTimelines, definedEffectTimelineIds);

            for (int i = 0; i < document.ProjectilePresentationBindings.Count; i++)
            {
                ProjectilePresentationBindingConfig binding = document.ProjectilePresentationBindings[i];
                string path = "projectilePresentationBindings[" + i + "]";
                if (binding == null)
                {
                    Error(report, "ABILITY_PROJECTILE_PRESENTATION_BINDING_NULL", path, "Projectile presentation binding entry is null.");
                    continue;
                }

                if (binding.ProjectileDefinitionId <= 0 || !projectileDefinitionIds.Contains(binding.ProjectileDefinitionId))
                    Error(report, "ABILITY_PROJECTILE_PRESENTATION_DEFINITION_UNKNOWN", path + ".projectileDefinitionId", "Projectile presentation binding references an unknown definition id: " + binding.ProjectileDefinitionId + ".");
                else if (!projectilePresentationDefinitionIds.Add(binding.ProjectileDefinitionId))
                    Error(report, "ABILITY_PROJECTILE_PRESENTATION_DEFINITION_DUPLICATE", path + ".projectileDefinitionId", "Duplicate projectile presentation binding definition id: " + binding.ProjectileDefinitionId + ".");
                ValidateProjectilePresentationBinding(
                    report,
                    path,
                    binding,
                    resourceKeys,
                    resolutionVfxDefinitions ?? document.VfxDefinitions);
            }

            for (int i = 0; i < document.ProjectileSpawnProfiles.Count; i++)
            {
                ProjectileSpawnProfileConfig profile = document.ProjectileSpawnProfiles[i];
                string path = "projectileSpawnProfiles[" + i + "]";
                if (profile == null)
                {
                    Error(report, "ABILITY_PROJECTILE_SPAWN_PROFILE_NULL", path, "Projectile spawn profile entry is null.");
                    continue;
                }

                RequireId(report, profile.StableId, path + ".stableId", "Projectile spawn profile stable id is required.");
                if (!string.IsNullOrWhiteSpace(profile.StableId) && !projectileSpawnProfileIds.Add(profile.StableId))
                    Error(report, "ABILITY_PROJECTILE_SPAWN_PROFILE_DUPLICATE", path + ".stableId", "Duplicate projectile spawn profile stable id: " + profile.StableId + ".");
                if (profile.ProfileId <= 0)
                    Error(report, "ABILITY_PROJECTILE_SPAWN_PROFILE_ID_INVALID", path + ".profileId", "Projectile spawn profile id must be positive.");
                else if (!projectileSpawnProfileNumericIds.Add(profile.ProfileId))
                    Error(report, "ABILITY_PROJECTILE_SPAWN_PROFILE_ID_DUPLICATE", path + ".profileId", "Duplicate projectile spawn profile id: " + profile.ProfileId + ".");
                RequireId(report, profile.ProjectileStableId, path + ".projectileStableId", "Projectile spawn profile projectileStableId is required.");
                if (!string.IsNullOrWhiteSpace(profile.ProjectileStableId) && !projectileStableIds.Contains(profile.ProjectileStableId))
                    Error(report, "ABILITY_PROJECTILE_SPAWN_PROFILE_PROJECTILE_UNKNOWN", path + ".projectileStableId", "Projectile spawn profile references an unknown projectile stable id: " + profile.ProjectileStableId + ".");
                if (profile.ProjectileDefinitionId <= 0 || !projectileDefinitionIds.Contains(profile.ProjectileDefinitionId))
                    Error(report, "ABILITY_PROJECTILE_SPAWN_PROFILE_PROJECTILE_DEFINITION_UNKNOWN", path + ".projectileDefinitionId", "Projectile spawn profile references an unknown projectile definition id: " + profile.ProjectileDefinitionId + ".");

                if (profile.Origin == null || !TryResolveProjectileOriginKind(profile.Origin.Kind, out string originKind))
                {
                    Error(report, "ABILITY_PROJECTILE_SPAWN_ORIGIN_KIND_INVALID", path + ".origin.kind", "Projectile spawn origin kind is unsupported: " + (profile.Origin?.Kind ?? string.Empty) + ".");
                }
                else if (string.Equals(originKind, "Socket", StringComparison.Ordinal))
                {
                    if (profile.Origin.SocketId <= 0 && string.IsNullOrWhiteSpace(profile.Origin.SocketLabel))
                    {
                        Error(report, "ABILITY_PROJECTILE_SPAWN_SOCKET_MISSING", path + ".origin.socketId", "Projectile socket origin requires socketId.");
                    }
                    else if (knownSocketIds.Count > 0
                        && !string.IsNullOrWhiteSpace(profile.Origin.SocketLabel)
                        && !knownSocketIds.Contains(profile.Origin.SocketLabel))
                    {
                        Error(report, "ABILITY_PROJECTILE_SPAWN_SOCKET_UNKNOWN", path + ".origin.socketId", "Projectile socket origin references an unknown socket id: " + profile.Origin.SocketLabel + ".");
                    }
                }

                if (profile.Aim == null || !TryResolveProjectileAimKind(profile.Aim.Kind, out string aimKind))
                {
                    Error(report, "ABILITY_PROJECTILE_AIM_KIND_INVALID", path + ".aim.kind", "Projectile aim kind is unsupported: " + (profile.Aim?.Kind ?? string.Empty) + ".");
                }
                else
                {
                    string fallback = string.IsNullOrWhiteSpace(profile.Aim.FallbackKind)
                        ? "CasterForwardRange"
                        : profile.Aim.FallbackKind;
                    if (!TryResolveProjectileAimKind(fallback, out string fallbackKind))
                    {
                        Error(report, "ABILITY_PROJECTILE_AIM_FALLBACK_KIND_INVALID", path + ".aim.fallbackKind", "Projectile aim fallback kind is unsupported: " + fallback + ".");
                    }
                    else if (!string.Equals(aimKind, "CasterForwardRange", StringComparison.Ordinal)
                        && !string.Equals(fallbackKind, "CasterForwardRange", StringComparison.Ordinal))
                    {
                        Error(report, "ABILITY_PROJECTILE_AIM_FALLBACK_KIND_INVALID", path + ".aim.fallbackKind", "Locked-target projectile aim currently only supports CasterForwardRange fallback.");
                    }

                    if (profile.Aim.MaxRange < 0f)
                        Error(report, "ABILITY_PROJECTILE_AIM_MAX_RANGE_INVALID", path + ".aim.maxRange", "Projectile aim maxRange cannot be negative.");
                }
            }

            for (int i = 0; i < document.Abilities.Count; i++)
            {
                AbilityDefinitionConfig ability = document.Abilities[i];
                string path = "abilities[" + i + "]";
                if (ability == null)
                {
                    Error(report, "ABILITY_NULL", path, "Ability entry is null.");
                    continue;
                }

                if (ability.AbilityId <= 0)
                    Error(report, "ABILITY_ID_INVALID", path + ".abilityId", "Ability id must be positive.");
                else if (!abilityIds.Add(ability.AbilityId))
                    Error(report, "ABILITY_ID_DUPLICATE", path + ".abilityId", "Duplicate ability id: " + ability.AbilityId + ".");
                RequireId(report, ability.AbilityStableId, path + ".abilityStableId", "Ability stable id is required.");
                if (!string.IsNullOrWhiteSpace(ability.AbilityStableId) && !abilityStableIds.Add(ability.AbilityStableId))
                    Error(report, "ABILITY_STABLE_ID_DUPLICATE", path + ".abilityStableId", "Duplicate ability stable id: " + ability.AbilityStableId + ".");
                if (ability.Input == null || ability.Input.InputIntentId <= 0)
                    Error(report, "ABILITY_INPUT_MISSING", path + ".input.inputIntentId", "Ability input intent is required.");
                if (ability.ActionBinding == null || ability.ActionBinding.ActionId <= 0)
                    Error(report, "ABILITY_ACTION_MISSING", path + ".actionBinding.actionId", "Ability action binding actionId is required.");
                else
                    actionIds.Add(ability.ActionBinding.ActionId);
                if (ability.ActionBinding == null || ability.ActionBinding.ActionIntentId <= 0)
                    Error(report, "ABILITY_ACTION_INTENT_MISSING", path + ".actionBinding.actionIntentId", "Ability action binding actionIntentId is required.");
                ValidateCombatHitDamage(report, path + ".damage", ability.Damage);
                if ((ability.PresentationProfileId > 0 && !presentationProfileIds.Contains(ability.PresentationProfileId))
                    || (ability.PresentationProfileId <= 0 && !string.IsNullOrWhiteSpace(ability.PresentationProfileLabel)))
                    Error(report, "ABILITY_VFX_PROFILE_UNKNOWN", path + ".presentationProfileId", "Ability references an unknown VFX presentation profile: " + (ability.PresentationProfileId > 0 ? ability.PresentationProfileId.ToString() : ability.PresentationProfileLabel) + ".");
                if ((ability.EffectTimelineId > 0 && !effectTimelineIds.Contains(ability.EffectTimelineId))
                    || (ability.EffectTimelineId <= 0 && !string.IsNullOrWhiteSpace(ability.EffectTimelineLabel)))
                    Error(report, "ABILITY_EFFECT_TIMELINE_UNKNOWN", path + ".effectTimelineId", "Ability references an unknown effect timeline: " + (ability.EffectTimelineId > 0 ? ability.EffectTimelineId.ToString() : ability.EffectTimelineLabel) + ".");
                if ((ability.TargetPolicyId > 0 && !IsKnownTargetPolicy(ability.TargetPolicyId))
                    || (ability.TargetPolicyId <= 0 && !string.IsNullOrWhiteSpace(ability.TargetPolicyLabel)))
                    Error(report, "ABILITY_TARGET_POLICY_UNKNOWN", path + ".targetPolicyId", "Unknown target policy: " + (ability.TargetPolicyId > 0 ? ability.TargetPolicyId.ToString() : ability.TargetPolicyLabel) + ".");
            }

            foreach (int effectTimelineId in effectTimelineIds)
            {
                if (!definedEffectTimelineIds.Contains(effectTimelineId))
                    Error(report, "ABILITY_PROJECTILE_EFFECT_TIMELINE_UNKNOWN", "projectileDefinitions.impact.effectTimelineId", "Projectile impact references an unknown effect timeline: " + effectTimelineId + ".");
            }

            report.HasBlockingIssues = report.Diagnostics.Exists(diagnostic =>
                string.Equals(diagnostic.Severity, "Error", StringComparison.Ordinal));
            report.Status = report.HasBlockingIssues ? "Blocked" : "Ready";
            return report;
        }

        public static string ResolveDocumentPath(string packageOrDocumentPath)
        {
            string resolved = Path.GetFullPath(packageOrDocumentPath ?? string.Empty);
            if (File.Exists(resolved) || string.Equals(Path.GetExtension(resolved), ".json", StringComparison.OrdinalIgnoreCase))
                return resolved;

            return Path.Combine(resolved, AbilityAuthoringDocumentRelativePath);
        }

        public static string ResolvePackageRootPath(string packageOrDocumentPath)
        {
            string documentPath = ResolveDocumentPath(packageOrDocumentPath);
            string full = Path.GetFullPath(documentPath);
            if (string.Equals(Path.GetFileName(full), Path.GetFileName(AbilityAuthoringDocumentRelativePath), StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(Path.GetDirectoryName(full));

            return Path.GetDirectoryName(full);
        }

        public static int StablePositiveHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261U;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619U;
                }

                int result = (int)(hash & 0x7FFFFFFF);
                return result == 0 ? 1 : result;
            }
        }

        private static void Normalize(
            AbilityAuthoringDocument document,
            IReadOnlyList<VfxDefinitionConfig> resolutionVfxDefinitions = null)
        {
            document.SchemaVersion = string.IsNullOrWhiteSpace(document.SchemaVersion) ? "1" : document.SchemaVersion;
            document.PackageId ??= string.Empty;
            document.Abilities ??= new List<AbilityDefinitionConfig>();
            document.VfxDefinitions ??= new List<VfxDefinitionConfig>();
            document.VfxPresentationProfiles ??= new List<VfxPresentationProfileConfig>();
            document.VfxTriggerMaps ??= new List<VfxTriggerMapConfig>();
            document.SourceDiagnostics ??= new List<AbilityAuthoringDiagnostic>();
            document.ProjectileDefinitions ??= new List<ProjectileDefinitionConfig>();
            document.ProjectileSpawnProfiles ??= new List<ProjectileSpawnProfileConfig>();
            document.ProjectilePresentationBindings ??= new List<ProjectilePresentationBindingConfig>();
            document.EffectTimelines ??= new List<AbilityEffectTimelineConfig>();
            document.CharacterTestMechanicTunings ??= new List<CharacterTestMechanicTuningConfig>();
            document.KnownSocketIds ??= new List<string>();
            document.ResourceRefs ??= new List<AbilityResourceReferenceConfig>();

            var presentationProfileLabelToId = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < document.VfxPresentationProfiles.Count; i++)
            {
                VfxPresentationProfileConfig profile = document.VfxPresentationProfiles[i];
                if (profile == null)
                    continue;

                profile.AnchorStrategy = string.IsNullOrWhiteSpace(profile.AnchorStrategy) ? "HitPoint" : profile.AnchorStrategy;
                if (profile.PresentationProfileId <= 0 && !string.IsNullOrWhiteSpace(profile.PresentationProfileLabel))
                    profile.PresentationProfileId = StablePositiveHash(profile.PresentationProfileLabel);

                if (!string.IsNullOrWhiteSpace(profile.PresentationProfileLabel) && !presentationProfileLabelToId.ContainsKey(profile.PresentationProfileLabel))
                    presentationProfileLabelToId[profile.PresentationProfileLabel] = profile.PresentationProfileId;
            }

            var timelineLabelToId = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < document.EffectTimelines.Count; i++)
            {
                AbilityEffectTimelineConfig timeline = document.EffectTimelines[i];
                if (timeline == null)
                    continue;

                if (timeline.TimelineId <= 0 && !string.IsNullOrWhiteSpace(timeline.TimelineLabel))
                    timeline.TimelineId = StablePositiveHash(timeline.TimelineLabel);

                if (!string.IsNullOrWhiteSpace(timeline.TimelineLabel) && !timelineLabelToId.ContainsKey(timeline.TimelineLabel))
                    timelineLabelToId[timeline.TimelineLabel] = timeline.TimelineId;
            }

            for (int i = 0; i < document.Abilities.Count; i++)
            {
                AbilityDefinitionConfig ability = document.Abilities[i];
                if (ability == null)
                    continue;

                ability.AbilityStableId ??= string.Empty;
                ability.DisplayName ??= string.Empty;
                ability.Input ??= new AbilityInputBindingConfig();
                ability.Cooldown ??= new AbilityCooldownConfig();
                ability.Costs ??= new List<AbilityCostConfig>();
                ability.ActionBinding ??= new AbilityActionBindingConfig();
                if (ability.PresentationProfileId <= 0 && !string.IsNullOrWhiteSpace(ability.PresentationProfileLabel))
                {
                    if (presentationProfileLabelToId.TryGetValue(ability.PresentationProfileLabel, out int pid))
                        ability.PresentationProfileId = pid;
                }
                if (ability.EffectTimelineId <= 0 && !string.IsNullOrWhiteSpace(ability.EffectTimelineLabel))
                {
                    if (timelineLabelToId.TryGetValue(ability.EffectTimelineLabel, out int tid))
                        ability.EffectTimelineId = tid;
                }
                if (ability.Damage != null)
                    NormalizeCombatHitDamage(ability.Damage);
            }

            for (int i = 0; i < document.VfxDefinitions.Count; i++)
            {
                VfxDefinitionConfig definition = document.VfxDefinitions[i];
                if (definition == null)
                    continue;

                definition.StableId ??= string.Empty;
                definition.PrefabResource ??= new AbilityResourceKeyConfig();
                definition.PrefabResource.Id ??= string.Empty;
                definition.PrefabResource.TypeId ??= string.Empty;
                definition.PrefabResource.Variant ??= string.Empty;
                definition.PrefabResource.PackageId ??= string.Empty;
                definition.DefinitionHash = string.IsNullOrWhiteSpace(definition.DefinitionHash)
                    ? definition.DefinitionId + ":" + definition.StableId + ":" + ToResourceKey(definition.PrefabResource)
                    : definition.DefinitionHash;
            }

            for (int i = 0; i < document.CharacterTestMechanicTunings.Count; i++)
            {
                CharacterTestMechanicTuningConfig tuning = document.CharacterTestMechanicTunings[i];
                if (tuning == null)
                    continue;

                tuning.StableId ??= string.Empty;
                tuning.AbilityStableId ??= string.Empty;
                tuning.SourceStableId ??= string.Empty;
                tuning.InterruptPolicy = string.IsNullOrWhiteSpace(tuning.InterruptPolicy)
                    ? "CancelOnDamageOrActionExit"
                    : tuning.InterruptPolicy;
                if (string.Equals(tuning.StableId, "mechanic_tuning.character_test.arc_mine", StringComparison.Ordinal)
                    && tuning.FuseDelayFrames <= 0
                    && tuning.ArmDelayFrames > 0)
                {
                    tuning.FuseDelayFrames = tuning.ArmDelayFrames;
                    tuning.ArmDelayFrames = 0;
                }
            }

            for (int i = 0; i < document.VfxPresentationProfiles.Count; i++)
            {
                VfxPresentationProfileConfig profile = document.VfxPresentationProfiles[i];
                if (profile == null)
                    continue;

                profile.AnchorStrategy = string.IsNullOrWhiteSpace(profile.AnchorStrategy) ? "HitPoint" : profile.AnchorStrategy;
            }

            for (int i = 0; i < document.VfxTriggerMaps.Count; i++)
            {
                VfxTriggerMapConfig map = document.VfxTriggerMaps[i];
                if (map == null)
                    continue;

                map.SourceDomain = string.IsNullOrWhiteSpace(map.SourceDomain) ? "CombatHit" : map.SourceDomain;
                map.SourceDomainId = map.SourceDomainId <= 0 ? ResolveSourceDomainId(map.SourceDomain) : map.SourceDomainId;
                if (map.TriggerId <= 0 && map.SourceDomainId == VfxSourceDomainCombatHit)
                    map.TriggerId = map.PresentationProfileId;
            }

            for (int i = 0; i < document.ProjectileDefinitions.Count; i++)
            {
                ProjectileDefinitionConfig definition = document.ProjectileDefinitions[i];
                if (definition == null)
                    continue;

                definition.StableId ??= string.Empty;
                definition.Motion ??= new ProjectileMotionConfig();
                definition.Motion.Kind = string.IsNullOrWhiteSpace(definition.Motion.Kind)
                    ? "Linear"
                    : definition.Motion.Kind;
                definition.Motion.SpeedProfile ??= new ProjectileSpeedProfileConfig();
                definition.Motion.SpeedProfile.Kind = string.IsNullOrWhiteSpace(definition.Motion.SpeedProfile.Kind)
                    ? "Constant"
                    : definition.Motion.SpeedProfile.Kind;
                if (string.Equals(definition.Motion.Kind, "HomingToTarget", StringComparison.Ordinal))
                    definition.Motion.Homing ??= new ProjectileHomingConfig();
                else
                    definition.Motion.Homing = null;
                if (!string.Equals(definition.Motion.Kind, "BallisticArcToTarget", StringComparison.Ordinal)
                    && definition.Motion.GravityPerFrameSquared >= 0f)
                {
                    definition.Motion.GravityPerFrameSquared = 0f;
                    definition.Motion.MaxLaunchAngleDegrees = 0f;
                    definition.Motion.MaxLaunchAngleTangent = 0f;
                }
                else if (string.Equals(definition.Motion.Kind, "BallisticArcToTarget", StringComparison.Ordinal))
                {
                    if (definition.Motion.MaxLaunchAngleDegrees <= 0f)
                    {
                        definition.Motion.MaxLaunchAngleDegrees = 0f;
                        definition.Motion.MaxLaunchAngleTangent = 0f;
                    }
                    else
                    {
                        definition.Motion.MaxLaunchAngleTangent = CalculateLaunchAngleTangent(
                            definition.Motion.MaxLaunchAngleDegrees);
                    }
                }
                definition.Collision ??= new ProjectileCollisionConfig();
                definition.Collision.HitSameTargetPolicy = string.IsNullOrWhiteSpace(definition.Collision.HitSameTargetPolicy)
                    ? "AllowRepeat"
                    : definition.Collision.HitSameTargetPolicy;
                definition.Impact ??= new ProjectileImpactConfig();
                definition.Impact.PolicyKind = string.IsNullOrWhiteSpace(definition.Impact.PolicyKind)
                    ? "DirectHit"
                    : definition.Impact.PolicyKind;
                int impactStableEventId = definition.Impact.StableEventId;
                definition.Impact.HitEvent = NormalizeCombatHitEvent(
                    definition.Impact.HitEvent,
                    impactStableEventId);
                definition.Collision.LayerMask = ResolveProjectileQueryLayerMask(definition.Impact.HitEvent?.TargetTypes);
                if (string.Equals(definition.Impact.PolicyKind, "AreaPulseOnImpact", StringComparison.Ordinal))
                {
                    bool inheritAreaTargetTypes = definition.Impact.AreaHitEvent == null;
                    definition.Impact.AreaHitEvent = NormalizeCombatHitEvent(
                        definition.Impact.AreaHitEvent,
                        impactStableEventId);
                    if (inheritAreaTargetTypes)
                        definition.Impact.AreaHitEvent.TargetTypes = CopyStringList(definition.Impact.HitEvent.TargetTypes);
                    definition.Impact.AreaLayerMask = ResolveProjectileQueryLayerMask(definition.Impact.AreaHitEvent?.TargetTypes);
                }
                else if (definition.Impact.AreaHitEvent != null)
                {
                    definition.Impact.AreaHitEvent = NormalizeCombatHitEvent(
                        definition.Impact.AreaHitEvent,
                        impactStableEventId);
                    definition.Impact.AreaLayerMask = 0;
                }
                else
                {
                    definition.Impact.AreaLayerMask = 0;
                }

                if (definition.Impact != null)
                {
                    if (definition.Impact.PresentationProfileId <= 0 && !string.IsNullOrWhiteSpace(definition.Impact.PresentationProfileLabel))
                    {
                        if (presentationProfileLabelToId.TryGetValue(definition.Impact.PresentationProfileLabel, out int pid))
                            definition.Impact.PresentationProfileId = pid;
                    }
                    if (definition.Impact.EffectTimelineId <= 0 && !string.IsNullOrWhiteSpace(definition.Impact.EffectTimelineLabel))
                    {
                        if (timelineLabelToId.TryGetValue(definition.Impact.EffectTimelineLabel, out int tid))
                            definition.Impact.EffectTimelineId = tid;
                    }
                }
            }

            for (int i = 0; i < document.EffectTimelines.Count; i++)
            {
                AbilityEffectTimelineConfig timeline = document.EffectTimelines[i];
                if (timeline == null)
                    continue;

                timeline.Events ??= new List<AbilityEffectTimelineEventConfig>();
                for (int eventIndex = 0; eventIndex < timeline.Events.Count; eventIndex++)
                {
                    AbilityEffectTimelineEventConfig evt = timeline.Events[eventIndex];
                    if (evt == null)
                        continue;

                    if (evt.EventId <= 0 && !string.IsNullOrWhiteSpace(evt.EventLabel))
                        evt.EventId = StablePositiveHash(evt.EventLabel);

                    if (evt.TargetTimelineId <= 0 && !string.IsNullOrWhiteSpace(evt.TargetTimelineLabel))
                    {
                        if (timelineLabelToId.TryGetValue(evt.TargetTimelineLabel, out int targetId))
                            evt.TargetTimelineId = targetId;
                        else
                            evt.TargetTimelineId = StablePositiveHash(evt.TargetTimelineLabel);
                    }

                    evt.TrackKind = string.IsNullOrWhiteSpace(evt.TrackKind) ? "Gameplay" : evt.TrackKind;
                    evt.EventKind = string.IsNullOrWhiteSpace(evt.EventKind) ? "EmitGameplayEvent" : evt.EventKind;
                    evt.ShapeKind = string.IsNullOrWhiteSpace(evt.ShapeKind) ? "None" : evt.ShapeKind;
                    evt.AnchorMode = string.IsNullOrWhiteSpace(evt.AnchorMode) ? "CapturedAnchor" : evt.AnchorMode;
                    evt.LocalOffset ??= new ProjectileVector3Config();
                    evt.LocalPointB ??= new ProjectileVector3Config();
                    if (evt.PresentationProfileId < 0) evt.PresentationProfileId = 0;
                }
            }

            for (int i = 0; i < document.ProjectilePresentationBindings.Count; i++)
            {
                ProjectilePresentationBindingConfig binding = document.ProjectilePresentationBindings[i];
                if (binding == null)
                    continue;

                binding.ViewNamePrefix = string.IsNullOrWhiteSpace(binding.ViewNamePrefix)
                    ? "ProjectileVFX"
                    : binding.ViewNamePrefix;
                if (binding.PrefabResource != null)
                    NormalizeResourceKey(binding.PrefabResource);
                binding.Slots ??= new ProjectilePresentationSlotSetConfig();
                if (binding.Slots.FlightBody == null && IsResourceKeyValid(binding.PrefabResource))
                {
                    binding.Slots.FlightBody = new ProjectilePresentationSlotConfig
                    {
                        Resource = CopyResourceKey(binding.PrefabResource),
                        Orientation = "ProjectileForward"
                    };
                }
                NormalizePresentationSlot(binding.Slots.Spawn, "ProjectileForward");
                NormalizePresentationSlot(binding.Slots.FlightBody, "ProjectileForward");
                NormalizePresentationSlot(binding.Slots.Trail, "ProjectileForward");
                NormalizePresentationSlot(binding.Slots.DirectImpact, "SurfaceNormal");
                NormalizePresentationSlot(binding.Slots.AreaImpact, "ImpactNormalOrWorldUp");
                NormalizePresentationSlot(binding.Slots.BlockedImpact, "SurfaceNormal");
                NormalizePresentationSlot(binding.Slots.Expire, "ProjectileForward");
                ResolvePresentationSlotIds(binding, resolutionVfxDefinitions ?? document.VfxDefinitions);
                binding.ExitPolicy ??= new ProjectilePresentationExitPolicyConfig();
                NormalizeExitPolicy(binding.ExitPolicy);
            }

            var ballisticProjectileStableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < document.ProjectileDefinitions.Count; i++)
            {
                ProjectileDefinitionConfig definition = document.ProjectileDefinitions[i];
                if (definition == null)
                    continue;
                if (string.Equals(definition.Motion?.Kind, "BallisticArcToTarget", StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(definition.StableId))
                {
                    ballisticProjectileStableIds.Add(definition.StableId);
                }
            }

            for (int i = 0; i < document.ProjectileSpawnProfiles.Count; i++)
            {
                ProjectileSpawnProfileConfig profile = document.ProjectileSpawnProfiles[i];
                if (profile == null)
                    continue;

                profile.StableId ??= string.Empty;
                profile.ProjectileStableId ??= string.Empty;
                profile.Origin ??= new ProjectileSpawnOriginConfig();
                profile.Origin.Kind = string.IsNullOrWhiteSpace(profile.Origin.Kind)
                    ? "Socket"
                    : profile.Origin.Kind;
                profile.Origin.SocketLabel ??= string.Empty;
                profile.Origin.LocalOffset ??= new ProjectileVector3Config();
                profile.Origin.FallbackRootOffset ??= new ProjectileVector3Config { Y = 1.15f, Z = 0.65f };
                profile.Aim ??= new ProjectileAimConfig();
                profile.Aim.Kind = string.IsNullOrWhiteSpace(profile.Aim.Kind)
                    ? "CasterForwardRange"
                    : profile.Aim.Kind;
                profile.Aim.FallbackKind = string.IsNullOrWhiteSpace(profile.Aim.FallbackKind)
                    ? "CasterForwardRange"
                    : profile.Aim.FallbackKind;
                string projectileStableId = string.IsNullOrWhiteSpace(profile.ProjectileStableId)
                    ? profile.StableId
                    : profile.ProjectileStableId;
                if (ballisticProjectileStableIds.Contains(projectileStableId))
                    profile.Aim.MaxRange = 0f;
            }
        }

        private static AbilityRuntimeCatalogDocument BuildAbilityCatalog(AbilityAuthoringDocument document)
        {
            return new AbilityRuntimeCatalogDocument
            {
                SchemaVersion = "3",
                PackageId = document.PackageId ?? string.Empty,
                Abilities = Copy(document.Abilities)
            };
        }

        private static AbilityVfxDefinitionCatalogDocument BuildVfxDefinitionCatalog(AbilityAuthoringDocument document)
        {
            return new AbilityVfxDefinitionCatalogDocument
            {
                SchemaVersion = "1",
                PackageId = document.PackageId ?? string.Empty,
                Definitions = Copy(document.VfxDefinitions)
            };
        }

        private static CharacterTestMechanicTuningCatalogDocument BuildCharacterTestMechanicTuningCatalog(AbilityAuthoringDocument document)
        {
            return new CharacterTestMechanicTuningCatalogDocument
            {
                SchemaVersion = "1",
                PackageId = document.PackageId ?? string.Empty,
                Tunings = Copy(document.CharacterTestMechanicTunings)
            };
        }

        private static AbilityVfxTriggerMapDocument BuildVfxTriggerMap(AbilityAuthoringDocument document)
        {
            return new AbilityVfxTriggerMapDocument
            {
                SchemaVersion = "2",
                PackageId = document.PackageId ?? string.Empty,
                PresentationProfiles = Copy(document.VfxPresentationProfiles),
                Mappings = Copy(document.VfxTriggerMaps)
            };
        }

        private static AbilityActionReferenceCatalogDocument BuildActionReferenceCatalog(AbilityAuthoringDocument document)
        {
            var output = new AbilityActionReferenceCatalogDocument
            {
                SchemaVersion = "1",
                PackageId = document.PackageId ?? string.Empty
            };
            for (int i = 0; i < document.Abilities.Count; i++)
            {
                AbilityDefinitionConfig ability = document.Abilities[i];
                if (ability == null || ability.ActionBinding == null)
                    continue;

                output.ActionReferences.Add(new AbilityActionReferenceConfig
                {
                    AbilityStableId = ability.AbilityStableId ?? string.Empty,
                    ActionId = ability.ActionBinding.ActionId,
                    ActionIntentId = ability.ActionBinding.ActionIntentId,
                    ActionIntentLabel = ability.ActionBinding.ActionIntentLabel ?? string.Empty,
                    Damage = CloneCombatHitDamage(ability.Damage),
                    TargetPolicyId = ability.TargetPolicyId,
                    TargetPolicyLabel = ability.TargetPolicyLabel ?? string.Empty,
                    PresentationProfileId = ability.PresentationProfileId,
                    PresentationProfileLabel = ability.PresentationProfileLabel ?? string.Empty,
                    EffectTimelineId = ability.EffectTimelineId,
                    EffectTimelineLabel = ability.EffectTimelineLabel ?? string.Empty
                });
            }

            return output;
        }

        private static AbilityProjectileDefinitionCatalogDocument BuildProjectileDefinitionCatalog(AbilityAuthoringDocument document)
        {
            List<ProjectileDefinitionConfig> definitions = Copy(document.ProjectileDefinitions);
            definitions.Sort(CompareProjectileDefinitions);
            return new AbilityProjectileDefinitionCatalogDocument
            {
                SchemaVersion = "5",
                PackageId = document.PackageId ?? string.Empty,
                Definitions = definitions
            };
        }

        private static AbilityProjectileSpawnProfileCatalogDocument BuildProjectileSpawnProfileCatalog(AbilityAuthoringDocument document)
        {
            Dictionary<string, int> definitionOrder = BuildProjectileDefinitionOrder(document.ProjectileDefinitions);
            List<ProjectileSpawnProfileConfig> profiles = Copy(document.ProjectileSpawnProfiles);
            profiles.Sort((a, b) => CompareProjectileSpawnProfiles(a, b, definitionOrder));
            return new AbilityProjectileSpawnProfileCatalogDocument
            {
                SchemaVersion = "2",
                PackageId = document.PackageId ?? string.Empty,
                Profiles = profiles
            };
        }

        private static void ValidateProjectilePresentationBinding(
            AbilityAuthoringValidationReport report,
            string path,
            ProjectilePresentationBindingConfig binding,
            HashSet<string> resourceKeys,
            IReadOnlyList<VfxDefinitionConfig> vfxDefinitions)
        {
            if (IsResourceKeyValid(binding.PrefabResource))
                Warning(report, "PROJAUTH013", path + ".prefabResource", "Legacy projectile Presentation Binding v1 is mapped to slots.flightBody until explicitly migrated.");

            ProjectilePresentationSlotSetConfig slots = binding.Slots;
            if (slots == null)
            {
                Error(report, "PROJAUTH012", path + ".slots", "Projectile presentation binding must configure at least one lifecycle slot.");
                return;
            }

            ValidatePresentationSlot(report, path + ".slots.spawn", slots.Spawn, true, "ProjectileForward", "WorldUp", resourceKeys, vfxDefinitions);
            ValidatePresentationSlot(report, path + ".slots.flightBody", slots.FlightBody, false, "ProjectileForward", null, resourceKeys, vfxDefinitions);
            ValidatePresentationSlot(report, path + ".slots.trail", slots.Trail, false, "ProjectileForward", null, resourceKeys, vfxDefinitions);
            ValidatePresentationSlot(report, path + ".slots.directImpact", slots.DirectImpact, true, "SurfaceNormal", "ProjectileForward", resourceKeys, vfxDefinitions);
            ValidatePresentationSlot(report, path + ".slots.areaImpact", slots.AreaImpact, true, "ImpactNormalOrWorldUp", "WorldUp", resourceKeys, vfxDefinitions);
            ValidatePresentationSlot(report, path + ".slots.blockedImpact", slots.BlockedImpact, true, "SurfaceNormal", null, resourceKeys, vfxDefinitions);
            ValidatePresentationSlot(report, path + ".slots.expire", slots.Expire, true, "ProjectileForward", "WorldUp", resourceKeys, vfxDefinitions);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            ValidateDuplicateSlotResource(report, path, slots.Spawn, seen);
            ValidateDuplicateSlotResource(report, path, slots.DirectImpact, seen);
            ValidateDuplicateSlotResource(report, path, slots.AreaImpact, seen);
            ValidateDuplicateSlotResource(report, path, slots.BlockedImpact, seen);
            ValidateDuplicateSlotResource(report, path, slots.Expire, seen);

            ProjectilePresentationExitPolicyConfig exit = binding.ExitPolicy;
            if (exit == null
                || !IsExitMode(exit.AcceptedImpact)
                || !IsExitMode(exit.BlockedImpact)
                || !IsExitMode(exit.Expiration)
                || !IsExitMode(exit.ExternalRemoval)
                || !string.Equals(exit.RuntimeReset, "Immediate", StringComparison.Ordinal))
            {
                Error(report, "PROJAUTH012", path + ".exitPolicy", "Projectile presentation exit policy is invalid; runtimeReset must be Immediate and all other exits must be Complete or Dissipate.");
            }
        }

        private static void ValidatePresentationSlot(
            AbilityAuthoringValidationReport report,
            string path,
            ProjectilePresentationSlotConfig slot,
            bool oneShot,
            string allowedOrientationA,
            string allowedOrientationB,
            HashSet<string> resourceKeys,
            IReadOnlyList<VfxDefinitionConfig> vfxDefinitions)
        {
            if (slot == null)
                return;
            if (!IsResourceKeyValid(slot.Resource)
                || !string.Equals(slot.Resource.TypeId, "GameObject", StringComparison.Ordinal))
            {
                Error(report, "PROJAUTH012", path + ".resource", "Configured projectile presentation slots require a valid GameObject ResourceKey.");
                return;
            }
            string key = ToResourceKey(slot.Resource);
            if (resourceKeys.Count > 0
                && !resourceKeys.Contains(key)
                && !ContainsVfxResource(vfxDefinitions, key))
                Error(report, "PROJAUTH012", path + ".resource", "Projectile presentation slot references an unknown ResourceKey: " + key + ".");
            if (!string.Equals(slot.Orientation, allowedOrientationA, StringComparison.Ordinal)
                && !string.Equals(slot.Orientation, allowedOrientationB, StringComparison.Ordinal))
                Error(report, "PROJAUTH012", path + ".orientation", "Projectile presentation slot orientation is incompatible with its fixed consumer contract.");
            if (slot.LifetimeFramesOverride < 0 || (!oneShot && slot.LifetimeFramesOverride != 0))
                Error(report, "PROJAUTH012", path + ".lifetimeFramesOverride", "Lifetime override must be non-negative and may be non-zero only for one-shot slots.");
            if (oneShot && slot.VfxDefinitionId <= 0 && !ContainsVfxResource(vfxDefinitions, key))
                Error(report, "PROJAUTH011", path + ".resource", "One-shot projectile presentation ResourceKey cannot be resolved to a compact VfxDefinitionId.");
        }

        private static bool ContainsVfxResource(IReadOnlyList<VfxDefinitionConfig> definitions, string key)
        {
            for (int i = 0; definitions != null && i < definitions.Count; i++)
                if (definitions[i] != null && string.Equals(ToResourceKey(definitions[i].PrefabResource), key, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static void ValidateDuplicateSlotResource(
            AbilityAuthoringValidationReport report,
            string path,
            ProjectilePresentationSlotConfig slot,
            HashSet<string> seen)
        {
            if (!IsResourceKeyValid(slot?.Resource))
                return;
            string key = ToResourceKey(slot.Resource);
            if (!seen.Add(key))
                Warning(report, "PROJAUTH015", path + ".slots", "The same ResourceKey is configured in multiple one-shot slots that may fire on the same frame: " + key + ".");
        }

        private static bool IsExitMode(string value)
            => string.Equals(value, "Complete", StringComparison.Ordinal)
                || string.Equals(value, "Dissipate", StringComparison.Ordinal);

        private static bool IsResourceKeyValid(AbilityResourceKeyConfig value)
            => value != null && !string.IsNullOrWhiteSpace(value.Id) && !string.IsNullOrWhiteSpace(value.TypeId);

        private static void NormalizeResourceKey(AbilityResourceKeyConfig value)
        {
            value.Id ??= string.Empty;
            value.TypeId ??= string.Empty;
            value.Variant ??= string.Empty;
            value.PackageId ??= string.Empty;
        }

        private static AbilityResourceKeyConfig CopyResourceKey(AbilityResourceKeyConfig value)
            => value == null ? null : new AbilityResourceKeyConfig
            {
                Id = value.Id ?? string.Empty,
                TypeId = value.TypeId ?? string.Empty,
                Variant = value.Variant ?? string.Empty,
                PackageId = value.PackageId ?? string.Empty
            };

        private static void NormalizePresentationSlot(ProjectilePresentationSlotConfig slot, string defaultOrientation)
        {
            if (slot == null)
                return;
            slot.Resource ??= new AbilityResourceKeyConfig();
            NormalizeResourceKey(slot.Resource);
            slot.Orientation = string.IsNullOrWhiteSpace(slot.Orientation) ? defaultOrientation : slot.Orientation;
            slot.LocalOffset ??= new ProjectileVector3Config();
        }

        private static void ResolvePresentationSlotId(ProjectilePresentationSlotConfig slot, IReadOnlyList<VfxDefinitionConfig> definitions)
        {
            if (slot == null)
                return;
            // The selected resource owns the compact ID. A cached positive ID may
            // belong to the previously selected resource and must never bypass resolution.
            slot.VfxDefinitionId = 0;
            string key = IsResourceKeyValid(slot?.Resource) ? ToResourceKey(slot.Resource) : string.Empty;
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                VfxDefinitionConfig definition = definitions[i];
                if (definition != null && string.Equals(ToResourceKey(definition.PrefabResource), key, StringComparison.Ordinal))
                {
                    slot.VfxDefinitionId = definition.DefinitionId;
                    return;
                }
            }
        }

        internal static void ResolvePresentationSlotIds(
            ProjectilePresentationBindingConfig binding,
            IReadOnlyList<VfxDefinitionConfig> definitions)
        {
            if (binding?.Slots == null)
                return;

            ResolvePresentationSlotId(binding.Slots.Spawn, definitions);
            ResolvePresentationSlotId(binding.Slots.DirectImpact, definitions);
            ResolvePresentationSlotId(binding.Slots.AreaImpact, definitions);
            ResolvePresentationSlotId(binding.Slots.BlockedImpact, definitions);
            ResolvePresentationSlotId(binding.Slots.Expire, definitions);
        }

        private static void NormalizeExitPolicy(ProjectilePresentationExitPolicyConfig value)
        {
            value.AcceptedImpact = string.IsNullOrWhiteSpace(value.AcceptedImpact) ? "Complete" : value.AcceptedImpact;
            value.BlockedImpact = string.IsNullOrWhiteSpace(value.BlockedImpact) ? "Dissipate" : value.BlockedImpact;
            value.Expiration = string.IsNullOrWhiteSpace(value.Expiration) ? "Complete" : value.Expiration;
            value.ExternalRemoval = string.IsNullOrWhiteSpace(value.ExternalRemoval) ? "Dissipate" : value.ExternalRemoval;
            value.RuntimeReset = string.IsNullOrWhiteSpace(value.RuntimeReset) ? "Immediate" : value.RuntimeReset;
        }

        private static AbilityProjectilePresentationBindingDocument BuildProjectilePresentationBindings(AbilityAuthoringDocument document)
        {
            var bindings = new List<ProjectilePresentationBindingConfig>();
            for (int i = 0; i < document.ProjectilePresentationBindings.Count; i++)
            {
                ProjectilePresentationBindingConfig value = document.ProjectilePresentationBindings[i];
                if (value == null)
                    continue;
                bindings.Add(new ProjectilePresentationBindingConfig
                {
                    ProjectileDefinitionId = value.ProjectileDefinitionId,
                    ViewNamePrefix = value.ViewNamePrefix,
                    Slots = value.Slots,
                    ExitPolicy = value.ExitPolicy,
                    PrefabResource = CopyResourceKey(value.Slots?.FlightBody?.Resource)
                });
            }
            bindings.Sort(CompareProjectilePresentationBindings);
            return new AbilityProjectilePresentationBindingDocument
            {
                SchemaVersion = "2",
                PackageId = document.PackageId ?? string.Empty,
                Bindings = bindings
            };
        }

        private static AbilityEffectTimelineCatalogDocument BuildEffectTimelineCatalog(AbilityAuthoringDocument document)
        {
            return new AbilityEffectTimelineCatalogDocument
            {
                SchemaVersion = "2",
                PackageId = document.PackageId ?? string.Empty,
                Timelines = Copy(document.EffectTimelines)
            };
        }

        private static List<T> Copy<T>(List<T> source)
        {
            return source == null ? new List<T>() : new List<T>(source);
        }

        private static Dictionary<string, int> BuildProjectileDefinitionOrder(List<ProjectileDefinitionConfig> definitions)
        {
            Dictionary<string, int> order = new Dictionary<string, int>(StringComparer.Ordinal);
            if (definitions == null)
                return order;

            for (int i = 0; i < definitions.Count; i++)
            {
                ProjectileDefinitionConfig definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.StableId))
                    continue;

                order[definition.StableId] = definition.DefinitionId;
            }

            return order;
        }

        private static int CompareProjectileDefinitions(ProjectileDefinitionConfig left, ProjectileDefinitionConfig right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            int byDefinitionId = left.DefinitionId.CompareTo(right.DefinitionId);
            return byDefinitionId != 0
                ? byDefinitionId
                : string.Compare(left.StableId ?? string.Empty, right.StableId ?? string.Empty, StringComparison.Ordinal);
        }

        private static int CompareProjectilePresentationBindings(ProjectilePresentationBindingConfig left, ProjectilePresentationBindingConfig right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            return left.ProjectileDefinitionId.CompareTo(right.ProjectileDefinitionId);
        }

        private static int CompareProjectileSpawnProfiles(
            ProjectileSpawnProfileConfig left,
            ProjectileSpawnProfileConfig right,
            Dictionary<string, int> definitionOrder)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            int leftOrder = ResolveProjectileDefinitionOrder(left.ProjectileStableId, definitionOrder);
            int rightOrder = ResolveProjectileDefinitionOrder(right.ProjectileStableId, definitionOrder);
            int byDefinitionOrder = leftOrder.CompareTo(rightOrder);
            return byDefinitionOrder != 0
                ? byDefinitionOrder
                : string.Compare(left.StableId ?? string.Empty, right.StableId ?? string.Empty, StringComparison.Ordinal);
        }

        private static int ResolveProjectileDefinitionOrder(string projectileStableId, Dictionary<string, int> definitionOrder)
        {
            if (string.IsNullOrWhiteSpace(projectileStableId) || definitionOrder == null)
                return int.MaxValue;

            return definitionOrder.TryGetValue(projectileStableId, out int order) ? order : int.MaxValue;
        }

        private static void RequireId(AbilityAuthoringValidationReport report, string value, string path, string message)
        {
            if (string.IsNullOrWhiteSpace(value))
                Error(report, "ABILITY_ID_REQUIRED", path, message);
        }

        private static void RequireNumericId(AbilityAuthoringValidationReport report, int value, string path, string message)
        {
            if (value <= 0)
                Error(report, "ABILITY_ID_REQUIRED", path, message);
        }

        private static void Error(AbilityAuthoringValidationReport report, string code, string path, string message)
        {
            report.Diagnostics.Add(new AbilityAuthoringDiagnostic
            {
                Severity = "Error",
                Code = code ?? string.Empty,
                Path = path ?? string.Empty,
                Message = message ?? string.Empty
            });
        }

        private static void Warning(AbilityAuthoringValidationReport report, string code, string path, string message)
        {
            report.Diagnostics.Add(new AbilityAuthoringDiagnostic
            {
                Severity = "Warning",
                Code = code ?? string.Empty,
                Path = path ?? string.Empty,
                Message = message ?? string.Empty
            });
        }

        private static CombatHitEventConfig NormalizeCombatHitEvent(
            CombatHitEventConfig config,
            int legacyStableEventId)
        {
            config ??= new CombatHitEventConfig();
            config.TargetTypes = NormalizeCombatHitTargetTypes(config.TargetTypes);
            config.EnvironmentHitPolicy = string.IsNullOrWhiteSpace(config.EnvironmentHitPolicy)
                ? "None"
                : config.EnvironmentHitPolicy;
            config.StableEventId = config.StableEventId <= 0
                ? legacyStableEventId
                : config.StableEventId;
            config.HitSameTargetPolicy = string.IsNullOrWhiteSpace(config.HitSameTargetPolicy)
                ? "AllowRepeat"
                : config.HitSameTargetPolicy;
            if (config.Damage != null)
                NormalizeCombatHitDamage(config.Damage);
            config.OnHitBuffs = NormalizeCombatHitBuffs(config.OnHitBuffs);
            return config;
        }

        private static List<CombatHitBuffApplicationConfig> NormalizeCombatHitBuffs(
            List<CombatHitBuffApplicationConfig> onHitBuffs)
        {
            if (onHitBuffs == null || onHitBuffs.Count == 0)
                return null;

            var values = new List<CombatHitBuffApplicationConfig>(onHitBuffs.Count);
            for (int i = 0; i < onHitBuffs.Count; i++)
            {
                CombatHitBuffApplicationConfig value = onHitBuffs[i];
                if (value != null)
                    values.Add(value);
            }

            values.Sort((left, right) =>
            {
                return left.BuffId.CompareTo(right.BuffId);
            });
            return values.Count == 0 ? null : values;
        }

        private static void NormalizeCombatHitDamage(CombatHitDamageConfig damage)
        {
            if (damage == null)
                return;

            if (damage.Tags == null) damage.Tags = new List<int>();
            if (damage.TagLabels == null) damage.TagLabels = new List<string>();
            if (damage.SourceAttributeScales == null) damage.SourceAttributeScales = new List<CombatDamageAttributeScaleConfig>();

            if (damage.DamageTypeId <= 0 && !string.IsNullOrWhiteSpace(damage.DamageTypeLabel))
            {
                if (string.Equals(damage.DamageTypeLabel, "fire", StringComparison.OrdinalIgnoreCase))
                    damage.DamageTypeId = 3;
                else
                    damage.DamageTypeId = StablePositiveHash(damage.DamageTypeLabel);
            }

            if (damage.AnalysisCategory <= 0 && !string.IsNullOrWhiteSpace(damage.AnalysisCategoryLabel))
            {
                damage.AnalysisCategory = StablePositiveHash(damage.AnalysisCategoryLabel);
            }
        }

        private static CombatHitDamageConfig CloneCombatHitDamage(CombatHitDamageConfig damage)
        {
            if (damage == null)
                return null;

            return new CombatHitDamageConfig
            {
                DamageTypeId = damage.DamageTypeId,
                BaseDamage = damage.BaseDamage,
                SourceAttributeScales = damage.SourceAttributeScales == null
                    ? new List<CombatDamageAttributeScaleConfig>()
                    : damage.SourceAttributeScales.Select(scale => new CombatDamageAttributeScaleConfig
                    {
                        AttributeId = scale.AttributeId,
                        Scale = scale.Scale
                    }).ToList(),
                ImpactPower = damage.ImpactPower,
                LaunchPower = damage.LaunchPower,
                Tags = damage.Tags == null ? new List<int>() : new List<int>(damage.Tags),
                TagLabels = damage.TagLabels == null ? new List<string>() : new List<string>(damage.TagLabels),
                AnalysisCategory = damage.AnalysisCategory,
                DamageTypeLabel = damage.DamageTypeLabel ?? string.Empty,
                AnalysisCategoryLabel = damage.AnalysisCategoryLabel ?? string.Empty
            };
        }

        private static List<string> NormalizeCombatHitTargetTypes(List<string> targetTypes)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; targetTypes != null && i < targetTypes.Count; i++)
            {
                string value = targetTypes[i];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    value = value.Trim();
                    set.Add(string.Equals(value, "NeutralCharacter", StringComparison.Ordinal)
                        ? "ThirdPartyCharacter"
                        : value);
                }
            }

            if (set.Count == 0)
                set.Add("HostileCharacter");
            return new List<string>(set);
        }

        private static List<string> CopyStringList(List<string> values)
        {
            return values == null ? new List<string>() : new List<string>(values);
        }

        private static int ResolveProjectileQueryLayerMask(List<string> targetTypes)
        {
            int mask = 0;
            for (int i = 0; targetTypes != null && i < targetTypes.Count; i++)
            {
                string targetType = targetTypes[i] ?? string.Empty;
                if (IsCharacterProjectileTarget(targetType))
                    mask |= CharacterTestDynamicCharacterLayerMask;
                else if (IsWorldProjectileTarget(targetType))
                    mask |= CharacterTestStaticEnvironmentLayerMask;
            }

            return mask;
        }

        private static bool IsCharacterProjectileTarget(string targetType)
        {
            return string.Equals(targetType, "HostileCharacter", StringComparison.Ordinal)
                || string.Equals(targetType, "FriendlyCharacter", StringComparison.Ordinal)
                || string.Equals(targetType, "Self", StringComparison.Ordinal)
                || string.Equals(targetType, "ThirdPartyCharacter", StringComparison.Ordinal)
                || string.Equals(targetType, "NeutralCharacter", StringComparison.Ordinal)
                || string.Equals(targetType, "DamageableObject", StringComparison.Ordinal);
        }

        private static bool IsWorldProjectileTarget(string targetType)
        {
            return string.Equals(targetType, "WorldWall", StringComparison.Ordinal)
                || string.Equals(targetType, "WorldGround", StringComparison.Ordinal)
                || string.Equals(targetType, "WorldStatic", StringComparison.Ordinal);
        }

        private static List<string> NormalizeStringList(List<string> values)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; values != null && i < values.Count; i++)
            {
                string value = values[i];
                if (!string.IsNullOrWhiteSpace(value))
                    set.Add(value.Trim());
            }

            return new List<string>(set);
        }

        private static void ValidateCombatHitEvent(
            AbilityAuthoringValidationReport report,
            string path,
            CombatHitEventConfig config,
            CombatHitDamageConfig inheritedDamage = null,
            bool requireDamage = false,
            bool allowEmptyTargetTypes = false)
        {
            if (config == null)
            {
                Error(report, "ABILITY_HIT_EVENT_MISSING", path, "Combat hit event config is required.");
                return;
            }

            if (config.TargetTypes == null || config.TargetTypes.Count == 0)
            {
                if (!allowEmptyTargetTypes)
                    Error(report, "ABILITY_HIT_TARGET_TYPES_MISSING", path + ".targetTypes", "Combat hit event targetTypes is required.");
            }
            else
            {
                for (int i = 0; i < config.TargetTypes.Count; i++)
                {
                    string targetType = config.TargetTypes[i] ?? string.Empty;
                    if (!KnownCombatHitTargetTypes.Contains(targetType))
                    {
                        Error(report, "ABILITY_HIT_TARGET_TYPE_UNKNOWN", path + ".targetTypes[" + i + "]", "Combat hit event target type is unsupported: " + targetType + ".");
                    }
                }
            }

            if (!KnownCombatEnvironmentHitPolicies.Contains(config.EnvironmentHitPolicy ?? string.Empty))
            {
                Error(report, "ABILITY_HIT_ENVIRONMENT_POLICY_INVALID", path + ".environmentHitPolicy", "Combat hit event environment policy is unsupported: " + (config.EnvironmentHitPolicy ?? string.Empty) + ".");
            }

            if (config.MaxAcceptedHits <= 0)
                Error(report, "ABILITY_HIT_MAX_ACCEPTED_HITS_INVALID", path + ".maxAcceptedHits", "Combat hit event maxAcceptedHits must be positive.");

            if (!KnownCombatHitSameTargetPolicies.Contains(config.HitSameTargetPolicy ?? string.Empty))
            {
                Error(report, "ABILITY_HIT_SAME_TARGET_POLICY_INVALID", path + ".hitSameTargetPolicy", "Combat hit event hitSameTargetPolicy is unsupported: " + (config.HitSameTargetPolicy ?? string.Empty) + ".");
            }

            CombatHitDamageConfig effectiveDamage = HasDamage(config.Damage) ? config.Damage : inheritedDamage;
            if (requireDamage && !HasDamage(effectiveDamage))
                Error(report, "ABILITY_HIT_DAMAGE_MISSING", path + ".damage", "Combat hit event damage is required.");
            ValidateCombatHitDamage(report, path + ".damage", config.Damage);
            ValidateCombatHitBuffs(report, path + ".onHitBuffs", config.OnHitBuffs);
        }

        private static void ValidateCombatHitBuffs(
            AbilityAuthoringValidationReport report,
            string path,
            List<CombatHitBuffApplicationConfig> onHitBuffs)
        {
            if (onHitBuffs == null || onHitBuffs.Count == 0)
                return;

            var seenBuffIds = new HashSet<int>();
            for (int i = 0; i < onHitBuffs.Count; i++)
            {
                CombatHitBuffApplicationConfig entry = onHitBuffs[i];
                if (entry == null)
                {
                    Error(report, "ABILITY_HIT_BUFF_INVALID", path + "[" + i + "]", "Combat hit on-hit buff entry is required.");
                    continue;
                }

                if (entry.BuffId <= 0)
                    Error(report, "ABILITY_HIT_BUFF_ID_INVALID", path + "[" + i + "].buffId", "Combat hit on-hit buffId must be positive.");
                else if (!seenBuffIds.Add(entry.BuffId))
                    Error(report, "ABILITY_HIT_BUFF_ID_DUPLICATE", path + "[" + i + "].buffId", "Combat hit on-hit buffId cannot repeat in one hitEvent.");

            }
        }

        private static void ValidateCombatHitDamage(
            AbilityAuthoringValidationReport report,
            string path,
            CombatHitDamageConfig damage)
        {
            if (damage == null)
                return;

            if (damage.DamageTypeId <= 0)
                Error(report, "ABILITY_HIT_DAMAGE_TYPE_MISSING", path + ".damageTypeId", "Combat hit event damageTypeId is required.");
            if (damage.BaseDamage < 0)
                Error(report, "ABILITY_HIT_DAMAGE_BASE_INVALID", path + ".baseDamage", "Combat hit event baseDamage cannot be negative.");
            if (damage.ImpactPower < 0)
                Error(report, "ABILITY_HIT_DAMAGE_IMPACT_INVALID", path + ".impactPower", "Combat hit event impactPower cannot be negative.");
            if (damage.LaunchPower < 0)
                Error(report, "ABILITY_HIT_DAMAGE_LAUNCH_INVALID", path + ".launchPower", "Combat hit event launchPower cannot be negative.");
            for (int i = 0; damage.SourceAttributeScales != null && i < damage.SourceAttributeScales.Count; i++)
            {
                CombatDamageAttributeScaleConfig scale = damage.SourceAttributeScales[i];
                if (scale == null || scale.AttributeId <= 0 || scale.Scale < 0f || float.IsNaN(scale.Scale) || float.IsInfinity(scale.Scale))
                    Error(report, "ABILITY_HIT_DAMAGE_SCALE_INVALID", path + ".sourceAttributeScales[" + i + "]", "Source attribute scale must have a valid attribute and nonnegative finite coefficient.");
            }
            for (int i = 0; damage.Tags != null && i < damage.Tags.Count; i++)
            {
                if (damage.Tags[i] <= 0)
                    Error(report, "ABILITY_HIT_DAMAGE_TAG_INVALID", path + ".tags[" + i + "]", "Combat hit event damage tag cannot be empty.");
            }
        }

        private static bool HasDamage(CombatHitDamageConfig damage)
        {
            return damage != null
                && (damage.DamageTypeId > 0
                    || damage.BaseDamage > 0
                    || damage.ImpactPower > 0
                    || damage.LaunchPower > 0
                    || (damage.SourceAttributeScales != null && damage.SourceAttributeScales.Count > 0)
                    || (damage.Tags != null && damage.Tags.Count > 0)
                    || damage.AnalysisCategory > 0);
        }

        private static bool AllowsProjectileNoDirectSweep(ProjectileDefinitionConfig definition)
        {
            return definition != null
                && definition.PhysicalEventsEnabled
                && definition.Collision != null
                && definition.Collision.Radius > 0f
                && definition.Impact != null
                && definition.Impact.HitEvent != null
                && IsNoDirectSweepTargetTypes(definition.Impact.HitEvent.TargetTypes);
        }

        private static bool IsNoDirectSweepTargetTypes(List<string> targetTypes)
        {
            if (targetTypes == null || targetTypes.Count == 0)
                return true;

            return targetTypes.Count == 1
                && string.Equals(targetTypes[0], "None", StringComparison.Ordinal);
        }

        private static bool IsKnownTargetPolicy(int targetPolicyId)
        {
            return targetPolicyId >= 1 && targetPolicyId <= 12;
        }

        private static bool IsKnownRangedTargetPolicy(string targetPolicyId, string prefix)
        {
            if (!targetPolicyId.StartsWith(prefix, StringComparison.Ordinal)
                || !targetPolicyId.EndsWith("m", StringComparison.Ordinal))
            {
                return false;
            }

            string number = targetPolicyId.Substring(prefix.Length, targetPolicyId.Length - prefix.Length - 1);
            number = number.Replace('_', '.');
            return float.TryParse(
                    number,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float range)
                && range > 0f;
        }

        private static bool IsKnownEffectTrackKind(string trackKind)
        {
            return string.Equals(trackKind, "Combat", StringComparison.Ordinal)
                || string.Equals(trackKind, "Gameplay", StringComparison.Ordinal)
                || string.Equals(trackKind, "Presentation", StringComparison.Ordinal);
        }

        private static bool IsKnownEffectEventKind(string trackKind, string eventKind)
        {
            if (string.Equals(trackKind, "Combat", StringComparison.Ordinal))
            {
                return string.Equals(eventKind, "AreaPulse", StringComparison.Ordinal)
                    || string.Equals(eventKind, "ShapeQueryWindow", StringComparison.Ordinal);
            }

            if (string.Equals(trackKind, "Gameplay", StringComparison.Ordinal))
            {
                return string.Equals(eventKind, "ApplyEffect", StringComparison.Ordinal)
                    || string.Equals(eventKind, "SpawnProjectile", StringComparison.Ordinal)
                    || string.Equals(eventKind, "StartEffectTimeline", StringComparison.Ordinal)
                    || string.Equals(eventKind, "EmitGameplayEvent", StringComparison.Ordinal);
            }

            if (string.Equals(trackKind, "Presentation", StringComparison.Ordinal))
            {
                return string.Equals(eventKind, "SpawnVfx", StringComparison.Ordinal)
                    || string.Equals(eventKind, "PlayAudioCue", StringComparison.Ordinal)
                    || string.Equals(eventKind, "CameraImpulse", StringComparison.Ordinal)
                    || string.Equals(eventKind, "UiFeedback", StringComparison.Ordinal);
            }

            return false;
        }

        private static bool IsKnownEffectAnchorMode(string anchorMode)
        {
            return string.Equals(anchorMode, "CapturedAnchor", StringComparison.Ordinal)
                || string.Equals(anchorMode, "OwnerCurrent", StringComparison.Ordinal)
                || string.Equals(anchorMode, "TriggerPoint", StringComparison.Ordinal);
        }

        private static void ValidateEffectTimelineShape(
            AbilityAuthoringValidationReport report,
            string path,
            AbilityEffectTimelineEventConfig evt)
        {
            string shapeKind = evt.ShapeKind ?? string.Empty;
            bool isCombat = string.Equals(evt.TrackKind, "Combat", StringComparison.Ordinal);
            if (!isCombat)
            {
                if (!string.Equals(shapeKind, "None", StringComparison.Ordinal))
                    Error(report, "ABILITY_EFFECT_TIMELINE_SHAPE_INVALID", path + ".shapeKind", "Non-combat effect timeline events must use shapeKind None.");
                if (evt.Radius != 0f)
                    Error(report, "ABILITY_EFFECT_TIMELINE_RADIUS_INVALID", path + ".radius", "Non-combat effect timeline events cannot define a radius.");
                if (evt.LayerMask != 0)
                    Error(report, "ABILITY_EFFECT_TIMELINE_LAYER_MASK_INVALID", path + ".layerMask", "Non-combat effect timeline events cannot define a layerMask.");
                return;
            }

            if (!string.Equals(shapeKind, "Sphere", StringComparison.Ordinal)
                && !string.Equals(shapeKind, "Capsule", StringComparison.Ordinal))
            {
                Error(report, "ABILITY_EFFECT_TIMELINE_SHAPE_INVALID", path + ".shapeKind", "Combat effect timeline events only support Sphere or Capsule.");
            }

            if (evt.Radius <= 0f)
                Error(report, "ABILITY_EFFECT_TIMELINE_RADIUS_INVALID", path + ".radius", "Combat effect timeline event radius must be positive.");
            if (evt.LayerMask == 0)
                Error(report, "ABILITY_EFFECT_TIMELINE_LAYER_MASK_INVALID", path + ".layerMask", "Combat effect timeline event layerMask must be non-zero.");
        }

        private static void ValidateEffectTimelineReferences(
            AbilityAuthoringValidationReport report,
            List<AbilityEffectTimelineConfig> timelines,
            HashSet<int> definedEffectTimelineIds)
        {
            if (timelines == null || timelines.Count == 0)
                return;

            var byId = new Dictionary<int, AbilityEffectTimelineConfig>();
            var byIdPath = new Dictionary<int, string>();
            for (int i = 0; i < timelines.Count; i++)
            {
                AbilityEffectTimelineConfig timeline = timelines[i];
                if (timeline == null || timeline.TimelineId <= 0)
                    continue;
                if (byId.ContainsKey(timeline.TimelineId))
                    continue;

                byId.Add(timeline.TimelineId, timeline);
                byIdPath.Add(timeline.TimelineId, "effectTimelines[" + i + "]");
            }

            for (int i = 0; i < timelines.Count; i++)
            {
                AbilityEffectTimelineConfig timeline = timelines[i];
                if (timeline?.Events == null)
                    continue;

                for (int eventIndex = 0; eventIndex < timeline.Events.Count; eventIndex++)
                {
                    AbilityEffectTimelineEventConfig evt = timeline.Events[eventIndex];
                    if (evt == null || !string.Equals(evt.EventKind, "StartEffectTimeline", StringComparison.Ordinal))
                        continue;
                    if (evt.TargetTimelineId <= 0)
                        continue;
                    if (!definedEffectTimelineIds.Contains(evt.TargetTimelineId))
                    {
                        Error(
                            report,
                            "ABILITY_EFFECT_TIMELINE_TARGET_UNKNOWN",
                            "effectTimelines[" + i + "].events[" + eventIndex + "].targetTimelineId",
                            "StartEffectTimeline references an unknown effect timeline: " + evt.TargetTimelineId + ".");
                    }
                }
            }

            var visiting = new HashSet<int>();
            var visited = new HashSet<int>();
            foreach (int timelineId in byId.Keys)
            {
                if (ValidateEffectTimelineGraph(
                    report,
                    timelineId,
                    byId,
                    byIdPath,
                    visiting,
                    visited,
                    depth: 0))
                {
                    visiting.Clear();
                }
            }
        }

        private static bool ValidateEffectTimelineGraph(
            AbilityAuthoringValidationReport report,
            int timelineId,
            Dictionary<int, AbilityEffectTimelineConfig> byId,
            Dictionary<int, string> byIdPath,
            HashSet<int> visiting,
            HashSet<int> visited,
            int depth)
        {
            if (visited.Contains(timelineId))
                return false;
            if (visiting.Contains(timelineId))
            {
                Error(report, "ABILITY_EFFECT_TIMELINE_CYCLE", byIdPath[timelineId] + ".timelineId", "Effect timeline child graph contains a cycle at: " + timelineId + ".");
                return true;
            }
            if (depth > MaxEffectTimelineChildDepth)
            {
                Error(report, "ABILITY_EFFECT_TIMELINE_MAX_DEPTH", byIdPath[timelineId] + ".timelineId", "Effect timeline child graph exceeds max child depth: " + MaxEffectTimelineChildDepth + ".");
                return true;
            }
            if (!byId.TryGetValue(timelineId, out AbilityEffectTimelineConfig timeline))
                return false;

            visiting.Add(timelineId);
            for (int i = 0; timeline.Events != null && i < timeline.Events.Count; i++)
            {
                AbilityEffectTimelineEventConfig evt = timeline.Events[i];
                if (evt == null
                    || !string.Equals(evt.EventKind, "StartEffectTimeline", StringComparison.Ordinal)
                    || evt.TargetTimelineId <= 0
                    || !byId.ContainsKey(evt.TargetTimelineId))
                {
                    continue;
                }

                if (ValidateEffectTimelineGraph(report, evt.TargetTimelineId, byId, byIdPath, visiting, visited, depth + 1))
                    return true;
            }

            visiting.Remove(timelineId);
            visited.Add(timelineId);
            return false;
        }

        private static bool TryResolveProjectilePolicy(string policyKind, out string normalized)
        {
            normalized = string.IsNullOrWhiteSpace(policyKind) ? "DirectHit" : policyKind;
            if (string.Equals(normalized, "DirectHit", StringComparison.Ordinal))
            {
                normalized = "DirectHit";
                return true;
            }

            if (string.Equals(normalized, "AreaPulseOnImpact", StringComparison.Ordinal))
            {
                normalized = "AreaPulseOnImpact";
                return true;
            }

            return false;
        }

        private static bool TryResolveProjectileMotionKind(string kind, out string normalized)
        {
            normalized = string.IsNullOrWhiteSpace(kind) ? "Linear" : kind;
            if (string.Equals(normalized, "Linear", StringComparison.Ordinal))
            {
                normalized = "Linear";
                return true;
            }

            if (string.Equals(normalized, "ArcToTarget", StringComparison.Ordinal))
            {
                normalized = "ArcToTarget";
                return true;
            }

            if (string.Equals(normalized, "BoomerangReturn", StringComparison.Ordinal))
            {
                normalized = "BoomerangReturn";
                return true;
            }

            if (string.Equals(normalized, "SwayForward", StringComparison.Ordinal))
            {
                normalized = "SwayForward";
                return true;
            }

            if (string.Equals(normalized, "HomingToTarget", StringComparison.Ordinal))
            {
                normalized = "HomingToTarget";
                return true;
            }

            if (string.Equals(normalized, "BezierToTarget", StringComparison.Ordinal))
            {
                normalized = "BezierToTarget";
                return true;
            }

            if (string.Equals(normalized, "BallisticArcToTarget", StringComparison.Ordinal))
            {
                normalized = "BallisticArcToTarget";
                return true;
            }

            return false;
        }

        private static bool IsZero(ProjectileVector3Config value)
        {
            if (value == null)
                return true;

            return Math.Abs(value.X) < 0.0001f
                && Math.Abs(value.Y) < 0.0001f
                && Math.Abs(value.Z) < 0.0001f;
        }

        private static float CalculateLaunchAngleTangent(float degrees)
        {
            return (float)Math.Tan(degrees * Math.PI / 180.0);
        }

        private static bool TryResolveProjectileSpeedProfileKind(string kind, out string normalized)
        {
            normalized = string.IsNullOrWhiteSpace(kind) ? "Constant" : kind;
            if (string.Equals(normalized, "Constant", StringComparison.Ordinal))
            {
                normalized = "Constant";
                return true;
            }

            if (string.Equals(normalized, "EaseInQuadratic", StringComparison.Ordinal))
            {
                normalized = "EaseInQuadratic";
                return true;
            }

            if (string.Equals(normalized, "EaseOutQuadratic", StringComparison.Ordinal))
            {
                normalized = "EaseOutQuadratic";
                return true;
            }

            if (string.Equals(normalized, "EaseInOutSmoothstep", StringComparison.Ordinal))
            {
                normalized = "EaseInOutSmoothstep";
                return true;
            }

            return false;
        }

        private static bool TryResolveProjectileHitSameTargetPolicy(string policy, out string normalized)
        {
            normalized = string.IsNullOrWhiteSpace(policy) ? "AllowRepeat" : policy;
            if (string.Equals(normalized, "AllowRepeat", StringComparison.Ordinal))
            {
                normalized = "AllowRepeat";
                return true;
            }

            if (string.Equals(normalized, "OncePerProjectile", StringComparison.Ordinal))
            {
                normalized = "OncePerProjectile";
                return true;
            }

            return false;
        }

        private static bool TryResolveProjectileOriginKind(string kind, out string normalized)
        {
            normalized = string.IsNullOrWhiteSpace(kind) ? "Socket" : kind;
            if (string.Equals(normalized, "Socket", StringComparison.Ordinal))
            {
                normalized = "Socket";
                return true;
            }

            return false;
        }

        private static bool TryResolveProjectileAimKind(string kind, out string normalized)
        {
            normalized = string.IsNullOrWhiteSpace(kind) ? "CasterForwardRange" : kind;
            if (string.Equals(normalized, "CasterForwardRange", StringComparison.Ordinal))
            {
                normalized = "CasterForwardRange";
                return true;
            }

            if (string.Equals(normalized, "LockedTargetCenter", StringComparison.Ordinal))
            {
                normalized = "LockedTargetCenter";
                return true;
            }

            if (string.Equals(normalized, "LockedTargetOrCasterForwardRange", StringComparison.Ordinal))
            {
                normalized = "LockedTargetOrCasterForwardRange";
                return true;
            }

            return false;
        }

        private static HashSet<string> BuildResourceKeySet(AbilityAuthoringDocument document)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; document.ResourceRefs != null && i < document.ResourceRefs.Count; i++)
            {
                AbilityResourceReferenceConfig reference = document.ResourceRefs[i];
                if (reference?.ResourceKey == null)
                    continue;

                string key = ToResourceKey(reference.ResourceKey);
                if (!string.IsNullOrWhiteSpace(key))
                    set.Add(key);
            }

            return set;
        }

        private static HashSet<string> BuildKnownSocketIdSet(AbilityAuthoringDocument document)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; document.KnownSocketIds != null && i < document.KnownSocketIds.Count; i++)
            {
                string socketId = document.KnownSocketIds[i];
                if (!string.IsNullOrWhiteSpace(socketId))
                    set.Add(socketId);
            }

            return set;
        }

        private static string ToResourceKey(AbilityResourceKeyConfig key)
        {
            if (key == null)
                return string.Empty;

            string variant = string.IsNullOrWhiteSpace(key.Variant) ? string.Empty : "#" + key.Variant;
            string package = string.IsNullOrWhiteSpace(key.PackageId) ? string.Empty : "@" + key.PackageId;
            return (key.Id ?? string.Empty) + ":" + (key.TypeId ?? string.Empty) + variant + package;
        }

        private static int ResolveSourceDomainId(string sourceDomain)
        {
            if (string.Equals(sourceDomain, "CombatHit", StringComparison.OrdinalIgnoreCase))
                return VfxSourceDomainCombatHit;
            if (string.Equals(sourceDomain, "CombatAction", StringComparison.OrdinalIgnoreCase))
                return 20;
            if (string.Equals(sourceDomain, "ActionPresentation", StringComparison.OrdinalIgnoreCase))
                return VfxSourceDomainActionPresentation;
            if (string.Equals(sourceDomain, "Gameplay", StringComparison.OrdinalIgnoreCase))
                return 10;
            if (string.Equals(sourceDomain, "Manual", StringComparison.OrdinalIgnoreCase))
                return 1;

            return 0;
        }
    }
}
