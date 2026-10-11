using System;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestCharacterActionConfigSourceKind
    {
        EmbeddedFallback = 0,
        ExternalJson = 1,
    }

    public sealed class CharacterTestCharacterActionConfigSourceOptions
    {
        public CharacterTestCharacterActionConfigSourceOptions(
            string[] candidateDirectories = null,
            bool allowEmbeddedFallback = false)
        {
            CandidateDirectories = Copy(candidateDirectories);
            AllowEmbeddedFallback = allowEmbeddedFallback;
        }

        public string[] CandidateDirectories { get; }
        public bool AllowEmbeddedFallback { get; }

        public static CharacterTestCharacterActionConfigSourceOptions CreateDefault()
        {
            return new CharacterTestCharacterActionConfigSourceOptions(
                CharacterTestCharacterActionConfigSource.CreateDefaultCandidateDirectories());
        }

        /// <summary>Creates an explicitly legacy-only source policy for samples and test fixtures.</summary>
        public static CharacterTestCharacterActionConfigSourceOptions CreateLegacyFixtureFallback()
        {
            return new CharacterTestCharacterActionConfigSourceOptions(
                Array.Empty<string>(),
                allowEmbeddedFallback: true);
        }

        private static string[] Copy(string[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<string>();

            var copy = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                copy[i] = values[i] ?? string.Empty;
            return copy;
        }
    }

    public readonly struct CharacterTestCharacterActionConfigSourceDiagnostic
    {
        public CharacterTestCharacterActionConfigSourceDiagnostic(
            CharacterActionDiagnosticSeverity severity,
            string code,
            string sourcePath,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CharacterActionDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string SourcePath { get; }
        public string Message { get; }

        public override string ToString()
        {
            return Severity + " " + Code + " " + SourcePath + " - " + Message;
        }
    }

    internal static class CharacterTestActionSlotIds
    {
        public const int AttackPrimary = (int)CharacterCancelInputMask.AttackPrimary;
        public const int AttackSecondary = (int)CharacterCancelInputMask.AttackSecondary;
        public const int Dodge = (int)CharacterCancelInputMask.Dodge;
        public const int Jump = (int)CharacterCancelInputMask.Jump;
        public const int Skill = (int)CharacterCancelInputMask.Skill;
        public const int Guard = (int)CharacterCancelInputMask.Guard;
        public const int Interact = (int)CharacterCancelInputMask.Interact;
        public const int LightLanding = (int)CharacterCancelInputMask.LightLanding;
        public const int HeavyLanding = (int)CharacterCancelInputMask.HeavyLanding;

        public const int DefaultActionSlotMask =
            AttackPrimary
            | AttackSecondary
            | Dodge
            | Jump
            | Skill
            | LightLanding
            | HeavyLanding;
    }

    public static partial class CharacterTestCharacterActionConfigSource
    {
        public const string ExternalJsonLoadedCode = "CHARACTER_TEST_ACTION_EXTERNAL_JSON_LOADED";
        public const string ExternalJsonIncompleteCode = "CHARACTER_TEST_ACTION_EXTERNAL_JSON_INCOMPLETE";
        public const string ExternalJsonMissingCode = "CHARACTER_TEST_ACTION_EXTERNAL_JSON_MISSING";
        public const string EmbeddedFallbackCode = "CHARACTER_TEST_ACTION_EMBEDDED_FALLBACK";

        private const string ActionPackageSegment = "codex_flow_test";

        public const string ActionSetStableId = "codex_flow_test.sword_empty";
        public const int JumpIntentId = 58;
        public const string JumpIntentLabel = "Jump";
        public const int LightAttackIntentId = 59;
        public const int LightAttackIntentNumericId = LightAttackIntentId;
        public const string LightAttackIntentLabel = "LightAttack";
        public const int DodgeIntentId = 60;
        public const string DodgeIntentLabel = "intent.dodge";
        public static readonly CharacterActionId DefaultEvadeActionId = new CharacterActionId(930100);
        public const int FireballIntentId = 2;
        public const string FireballIntentLabel = "Fireball";
        public const int FireballBurstIntentId = 4;
        public const string FireballBurstIntentLabel = "FireballBurst";
        public const int FlameLanceIntentId = 6;
        public const string FlameLanceIntentLabel = "FlameLance";
        public const int FireballLobIntentId = 8;
        public const string FireballLobIntentLabel = "FireballLob";
        public const int FireballSurgeIntentId = 10;
        public const string FireballSurgeIntentLabel = "FireballSurge";
        public const int FireballReturnIntentId = 12;
        public const string FireballReturnIntentLabel = "FireballReturn";
        public const int FireballSwayIntentId = 14;
        public const string FireballSwayIntentLabel = "FireballSway";
        public const int GuardAuraIntentId = 30;
        public const string GuardAuraIntentLabel = "GuardAura";
        public const int PhysicalShieldIntentId = 32;
        public const string PhysicalShieldIntentLabel = "PhysicalShield";
        public const int FireballHomingIntentId = 34;
        public const string FireballHomingIntentLabel = "FireballHoming";
        public const int FireballBezierIntentId = 36;
        public const string FireballBezierIntentLabel = "FireballBezier";
        public const int FireballExplosionIntentId = 38;
        public const string FireballExplosionIntentLabel = "FireballExplosion";
        public const int TornadoIntentId = 16;
        public const string TornadoIntentLabel = "Tornado";
        public const int AreaConfinementIntentId = 40;
        public const string AreaConfinementIntentLabel = "AreaConfinement";
        public const int BeamLanceIntentId = 18;
        public const string BeamLanceIntentLabel = "BeamLance";
        public const int ChannelBeamIntentId = 20;
        public const string ChannelBeamIntentLabel = "ChannelBeam";
        public const int LaserLanceIntentId = 22;
        public const string LaserLanceIntentLabel = "LaserLance";
        public const int ChainLightningIntentId = 24;
        public const string ChainLightningIntentLabel = "ChainLightning";
        public const int PoisonBottleIntentId = 26;
        public const string PoisonBottleIntentLabel = "PoisonBottle";
        public const int PoisonDartIntentId = PoisonBottleIntentId;
        public const int StunBoltIntentId = 28;
        public const string StunBoltIntentLabel = "StunBolt";
        public const int MeteorBarrageIntentId = 42;
        public const string MeteorBarrageIntentLabel = "MeteorBarrage";
        public const int SwordWaveIntentId = 44;
        public const string SwordWaveIntentLabel = "SwordWave";
        public const int DashStrikeIntentId = 46;
        public const string DashStrikeIntentLabel = "DashStrike";
        public const int HookChainIntentId = 48;
        public const string HookChainIntentLabel = "HookChain";
        public const int ArcMineIntentId = 50;
        public const string ArcMineIntentLabel = "ArcMine";
        public const int RuneTurretIntentId = 52;
        public const string RuneTurretIntentLabel = "RuneTurret";
        public static readonly CharacterActionId KatanaCohesionBreakReactionActionId = new CharacterActionId(330);
        public static readonly CharacterActionId KatanaHitHighReactionActionId = new CharacterActionId(331);
        public static readonly CharacterActionId KatanaHitMidReactionActionId = new CharacterActionId(332);
        public static readonly CharacterActionId KatanaHitLowReactionActionId = new CharacterActionId(333);
        public static readonly CharacterActionId KatanaHitHighStrongReactionActionId = new CharacterActionId(334);
        public static readonly CharacterActionId KatanaHitMidStrongReactionActionId = new CharacterActionId(335);
        public static readonly CharacterActionId KatanaHitLowStrongReactionActionId = new CharacterActionId(336);
        public static readonly CharacterActionId KatanaLaunchReactionActionId = new CharacterActionId(363);
        public static readonly CharacterActionId KatanaDeathReactionActionId = new CharacterActionId(361);
        public static readonly CharacterActionId KatanaReviveReactionActionId = new CharacterActionId(362);
        public const string CohesionBreakReactionAnimationActionKey = "anim.reaction.cohesion_break";
        public const string CohesionBreakReactionAnimationClipResourceKey = "art.character.skeleton.animation.damage_stun01_start";
        public const string HitHighReactionAnimationActionKey = "anim.reaction.hit_high";
        public const string HitMidReactionAnimationActionKey = "anim.reaction.hit_mid";
        public const string HitLowReactionAnimationActionKey = "anim.reaction.hit_low";
        public const string HitHighStrongReactionAnimationActionKey = "anim.reaction.hit_high_strong";
        public const string HitMidStrongReactionAnimationActionKey = "anim.reaction.hit_mid_strong";
        public const string HitLowStrongReactionAnimationActionKey = "anim.reaction.hit_low_strong";
        public const string LaunchReactionAnimationActionKey = "anim.reaction.launch";
        public const string DeathReactionAnimationActionKey = "anim.reaction.death";
        public const string HitHighReactionAnimationClipResourceKey = "art.character.skeleton.animation.damage_high_f";
        public const string HitMidReactionAnimationClipResourceKey = "art.character.skeleton.animation.damage_mid_f";
        public const string HitLowReactionAnimationClipResourceKey = "art.character.skeleton.animation.damage_low_l1";
        public const string HitHighStrongReactionAnimationClipResourceKey = "art.character.skeleton.animation.damage_strong_l_head";
        public const string HitMidStrongReactionAnimationClipResourceKey = "art.character.skeleton.animation.damage_strong_l_chest";
        public const string HitLowStrongReactionAnimationClipResourceKey = "art.character.skeleton.animation.damage_strong_l_foot";
        public const string LaunchReactionAnimationClipResourceKey = HitMidStrongReactionAnimationClipResourceKey;
        public const string DeathReactionAnimationClipResourceKey = "art.character.skeleton.animation.damage_ko_mid_2";
        public const string KatanaDefaultReactionProfileId = "reaction.codex_flow_test.default";
        public const string LegacyDefaultReactionProfileId = "reaction.codex_flow_test.compat_default";
        public static readonly CharacterActionId JumpActionId = new CharacterActionId(300);
        public static readonly CharacterActionId LightAttackActionId = new CharacterActionId(301);
        public static readonly CharacterActionId FireballActionId = new CharacterActionId(302);
        public static readonly CharacterActionId FireballBurstActionId = new CharacterActionId(303);
        public static readonly CharacterActionId FlameLanceActionId = new CharacterActionId(304);
        public static readonly CharacterActionId FireballLobActionId = new CharacterActionId(305);
        public static readonly CharacterActionId FireballSurgeActionId = new CharacterActionId(306);
        public static readonly CharacterActionId FireballReturnActionId = new CharacterActionId(307);
        public static readonly CharacterActionId FireballSwayActionId = new CharacterActionId(308);
        public static readonly CharacterActionId GuardAuraActionId = new CharacterActionId(309);
        public static readonly CharacterActionId PhysicalShieldActionId = new CharacterActionId(320);
        public static readonly CharacterActionId FireballHomingActionId = new CharacterActionId(310);
        public static readonly CharacterActionId FireballBezierActionId = new CharacterActionId(311);
        public static readonly CharacterActionId FireballExplosionActionId = new CharacterActionId(312);
        public static readonly CharacterActionId TornadoActionId = new CharacterActionId(313);
        public static readonly CharacterActionId AreaConfinementActionId = new CharacterActionId(314);
        public static readonly CharacterActionId BeamLanceActionId = new CharacterActionId(315);
        public static readonly CharacterActionId ChannelBeamActionId = new CharacterActionId(316);
        public static readonly CharacterActionId LaserLanceActionId = new CharacterActionId(317);
        public static readonly CharacterActionId ChainLightningActionId = new CharacterActionId(318);
        public static readonly CharacterActionId PoisonBottleActionId = new CharacterActionId(319);
        public static readonly CharacterActionId PoisonDartActionId = PoisonBottleActionId;
        public static readonly CharacterActionId StunBoltActionId = new CharacterActionId(321);
        public static readonly CharacterActionId MeteorBarrageActionId = new CharacterActionId(322);
        public static readonly CharacterActionId SwordWaveActionId = new CharacterActionId(323);
        public static readonly CharacterActionId DashStrikeActionId = new CharacterActionId(327);
        public static readonly CharacterActionId HookChainActionId = new CharacterActionId(326);
        public static readonly CharacterActionId ArcMineActionId = new CharacterActionId(328);
        public static readonly CharacterActionId RuneTurretActionId = new CharacterActionId(329);
        public const int FireballProjectileRequestId = 17;
        public const string FireballProjectileRequestLabel = "character_test.fireball.projectile";
        public const int FireballProjectileImpactStableEventId = 18;
        public const string FireballProjectileImpactStableEventLabel = "character_test.fireball.projectile.impact";
        public const int FireballWindupVfxStableEventId = 86;
        public const string FireballWindupVfxStableEventLabel = "character_test.fireball.windup_vfx";
        public const int FireballBurstProjectileRequestId = 19;
        public const string FireballBurstProjectileRequestLabel = "character_test.fireball_burst.projectile";
        public const int FireballBurstProjectileImpactStableEventId = 20;
        public const string FireballBurstProjectileImpactStableEventLabel = "character_test.fireball_burst.projectile.impact";
        public const int FireballBurstWindupVfxStableEventId = 87;
        public const string FireballBurstWindupVfxStableEventLabel = "character_test.fireball_burst.windup_vfx";
        public const int FlameLanceProjectileRequestId = 21;
        public const string FlameLanceProjectileRequestLabel = "character_test.flame_lance.projectile";
        public const int FlameLanceProjectileImpactStableEventId = 22;
        public const string FlameLanceProjectileImpactStableEventLabel = "character_test.flame_lance.projectile.impact";
        public const int FireballLobProjectileRequestId = 23;
        public const string FireballLobProjectileRequestLabel = "character_test.fireball_lob.projectile";
        public const int FireballLobProjectileImpactStableEventId = 24;
        public const string FireballLobProjectileImpactStableEventLabel = "character_test.fireball_lob.projectile.impact";
        public const int FireballSurgeProjectileRequestId = 25;
        public const string FireballSurgeProjectileRequestLabel = "character_test.fireball_surge.projectile";
        public const int FireballSurgeProjectileImpactStableEventId = 26;
        public const string FireballSurgeProjectileImpactStableEventLabel = "character_test.fireball_surge.projectile.impact";
        public const int FireballReturnProjectileRequestId = 27;
        public const string FireballReturnProjectileRequestLabel = "character_test.fireball_return.projectile";
        public const int FireballReturnProjectileImpactStableEventId = 28;
        public const string FireballReturnProjectileImpactStableEventLabel = "character_test.fireball_return.projectile.impact";
        public const int FireballSwayProjectileRequestId = 29;
        public const string FireballSwayProjectileRequestLabel = "character_test.fireball_sway.projectile";
        public const int FireballSwayProjectileImpactStableEventId = 30;
        public const string FireballSwayProjectileImpactStableEventLabel = "character_test.fireball_sway.projectile.impact";
        public const int GuardAuraGameplayRequestId = 77;
        public const string GuardAuraGameplayRequestLabel = "character_test.guard_aura.component_ability";
        public const int GuardAuraStableEventId = 88;
        public const string GuardAuraStableEventLabel = "character_test.guard_aura.cast";
        public const int GuardAuraVfxStableEventId = 89;
        public const string GuardAuraVfxStableEventLabel = "character_test.guard_aura.aura_vfx";
        public const int PhysicalShieldGameplayRequestId = 79;
        public const string PhysicalShieldGameplayRequestLabel = "character_test.physical_shield.create";
        public const int PhysicalShieldStableEventId = 90;
        public const string PhysicalShieldStableEventLabel = "character_test.physical_shield.cast";
        public const int PhysicalShieldVfxStableEventId = 91;
        public const string PhysicalShieldVfxStableEventLabel = "character_test.physical_shield.vfx";
        public const int FireballHomingProjectileRequestId = 31;
        public const string FireballHomingProjectileRequestLabel = "character_test.fireball_homing.projectile";
        public const int FireballHomingProjectileImpactStableEventId = 32;
        public const string FireballHomingProjectileImpactStableEventLabel = "character_test.fireball_homing.projectile.impact";
        public const int FireballBezierProjectileRequestId = 33;
        public const string FireballBezierProjectileRequestLabel = "character_test.fireball_bezier.projectile";
        public const int FireballBezierProjectileImpactStableEventId = 34;
        public const string FireballBezierProjectileImpactStableEventLabel = "character_test.fireball_bezier.projectile.impact";
        public const int FireballExplosionProjectileRequestId = 35;
        public const string FireballExplosionProjectileRequestLabel = "character_test.fireball_explosion.projectile";
        public const int FireballExplosionProjectileImpactStableEventId = 36;
        public const string FireballExplosionProjectileImpactStableEventLabel = "character_test.fireball_explosion.projectile.impact";
        public const int FireballExplosionTimelineRepeatStableEventId = 5;
        public const string FireballExplosionTimelineRepeatStableEventLabel = "character_test.fireball_explosion.repeat";
        public const int FireballExplosionWindupVfxStableEventId = 92;
        public const string FireballExplosionWindupVfxStableEventLabel = "character_test.fireball_explosion.windup_vfx";
        public const int TornadoProjectileRequestId = 37;
        public const string TornadoProjectileRequestLabel = "character_test.tornado.projectile";
        public const int TornadoProjectileImpactStableEventId = 38;
        public const string TornadoProjectileImpactStableEventLabel = "character_test.tornado.projectile.impact";
        public const int TornadoProjectileTickStableEventId = 39;
        public const string TornadoProjectileTickStableEventLabel = "character_test.tornado.projectile.tick";
        public const int TornadoAttractionStableEventId = 75;
        public const string TornadoAttractionStableEventLabel = "character_test.tornado.projectile.attract";
        public const int TornadoWindupVfxStableEventId = 93;
        public const string TornadoWindupVfxStableEventLabel = "character_test.tornado.windup_vfx";
        public const int AreaConfinementGameplayRequestId = 78;
        public const string AreaConfinementGameplayRequestLabel = "character_test.area_confinement.zone";
        public const int AreaConfinementStableEventId = 94;
        public const string AreaConfinementStableEventLabel = "character_test.area_confinement.cast";
        public const int AreaConfinementControlLockStableEventId = 95;
        public const string AreaConfinementControlLockStableEventLabel = "area_confinement.control_lock";
        public const int AreaConfinementChannelControlStableEventId = 96;
        public const string AreaConfinementChannelControlStableEventLabel = "area_confinement.channel_movement_lock";
        public const int AreaConfinementControlReleaseStableEventId = 97;
        public const string AreaConfinementControlReleaseStableEventLabel = "area_confinement.control_release";
        public const int AreaConfinementRootedStableEventId = 98;
        public const string AreaConfinementRootedStableEventLabel = "character_test.area_confinement.rooted";
        public const int AreaConfinementTelegraphVfxStableEventId = 81;
        public const string AreaConfinementTelegraphVfxStableEventLabel = "character_test.area_confinement.telegraph.vfx";
        public const int AreaConfinementTelegraphCancelVfxStableEventId = 80;
        public const string AreaConfinementTelegraphCancelVfxStableEventLabel = "character_test.area_confinement.telegraph_cancel.vfx";
        public const int AreaConfinementZoneVfxStableEventId = 99;
        public const string AreaConfinementZoneVfxStableEventLabel = "character_test.area_confinement.zone.vfx";
        public const int AreaConfinementCharacterReleaseVfxStableEventId = 71;
        public const string AreaConfinementCharacterReleaseVfxStableEventLabel = "character_test.area_confinement.character_release.vfx";
        public const int BeamLanceProjectileRequestId = 40;
        public const string BeamLanceProjectileRequestLabel = "character_test.beam_lance.projectile";
        public const int BeamLanceProjectileImpactStableEventId = 41;
        public const string BeamLanceProjectileImpactStableEventLabel = "character_test.beam_lance.projectile.impact";
        public const int BeamLanceChargeVfxStableEventId = 100;
        public const string BeamLanceChargeVfxStableEventLabel = "character_test.beam_lance.charge_vfx";
        public const int BeamLanceBeamVfxStableEventId = 101;
        public const string BeamLanceBeamVfxStableEventLabel = "character_test.beam_lance.beam_vfx";
        public const int BeamLanceWindupVfxStableEventId = BeamLanceChargeVfxStableEventId;
        public const string BeamLanceWindupVfxStableEventLabel = BeamLanceChargeVfxStableEventLabel;
        public const int ChannelBeamProjectileRequestId = 42;
        public const string ChannelBeamProjectileRequestLabel = "character_test.channel_beam.tick_projectile";
        public const int ChannelBeamProjectileImpactStableEventId = 43;
        public const string ChannelBeamProjectileImpactStableEventLabel = "character_test.channel_beam.tick_projectile.impact";
        public const int ChannelBeamChargeVfxStableEventId = 102;
        public const string ChannelBeamChargeVfxStableEventLabel = "character_test.channel_beam.charge_vfx";
        public const int ChannelBeamBeamVfxStableEventId = 103;
        public const string ChannelBeamBeamVfxStableEventLabel = "character_test.channel_beam.beam_vfx";
        public const int LaserLanceProjectileRequestId = 44;
        public const string LaserLanceProjectileRequestLabel = "character_test.laser_lance.projectile";
        public const int LaserLanceProjectileImpactStableEventId = 45;
        public const string LaserLanceProjectileImpactStableEventLabel = "character_test.laser_lance.projectile.impact";
        public const int LaserLanceChargeVfxStableEventId = 104;
        public const string LaserLanceChargeVfxStableEventLabel = "character_test.laser_lance.charge_vfx";
        public const int LaserLancePathVfxStableEventId = 68;
        public const string LaserLancePathVfxStableEventLabel = "character_test.laser_lance.path_vfx";
        public const int ChainLightningGameplayRequestId = 105;
        public const string ChainLightningGameplayRequestLabel = "character_test.chain_lightning.cast";
        public const int ChainLightningImpactStableEventId = 106;
        public const string ChainLightningImpactStableEventLabel = "character_test.chain_lightning.hit";
        public const int ChainLightningChargeVfxStableEventId = 107;
        public const string ChainLightningChargeVfxStableEventLabel = "character_test.chain_lightning.charge_vfx";
        public const int ChainLightningArcVfxStableEventId = 69;
        public const string ChainLightningArcVfxStableEventLabel = "character_test.chain_lightning.arc_vfx";
        public const int ChainLightningImpactVfxStableEventId = 108;
        public const string ChainLightningImpactVfxStableEventLabel = "character_test.chain_lightning.impact_vfx";
        public const int PoisonBottleProjectileRequestId = 46;
        public const string PoisonBottleProjectileRequestLabel = "character_test.poison_bottle.projectile";
        public const int PoisonBottleProjectileImpactStableEventId = 47;
        public const string PoisonBottleProjectileImpactStableEventLabel = "character_test.poison_bottle.projectile.shatter";
        public const int PoisonBottleChargeVfxStableEventId = 109;
        public const string PoisonBottleChargeVfxStableEventLabel = "character_test.poison_bottle.charge_vfx";
        public const int PoisonBottlePoolVfxStableEventId = 74;
        public const string PoisonBottlePoolVfxStableEventLabel = "character_test.poison_bottle.pool_vfx";
        public const int PoisonDartProjectileRequestId = PoisonBottleProjectileRequestId;
        public const string PoisonDartProjectileRequestLabel = PoisonBottleProjectileRequestLabel;
        public const int PoisonDartProjectileImpactStableEventId = PoisonBottleProjectileImpactStableEventId;
        public const string PoisonDartProjectileImpactStableEventLabel = PoisonBottleProjectileImpactStableEventLabel;
        public const int PoisonDartChargeVfxStableEventId = PoisonBottleChargeVfxStableEventId;
        public const string PoisonDartChargeVfxStableEventLabel = PoisonBottleChargeVfxStableEventLabel;
        public const int StunBoltProjectileRequestId = 48;
        public const string StunBoltProjectileRequestLabel = "character_test.stun_bolt.projectile";
        public const int StunBoltProjectileImpactStableEventId = 49;
        public const string StunBoltProjectileImpactStableEventLabel = "character_test.stun_bolt.projectile.impact";
        public const int StunBoltChargeVfxStableEventId = 110;
        public const string StunBoltChargeVfxStableEventLabel = "character_test.stun_bolt.charge_vfx";
        public const int MeteorBarrageTelegraphGameplayRequestId = 111;
        public const string MeteorBarrageTelegraphGameplayRequestLabel = "character_test.meteor_barrage.telegraph";
        public const int MeteorBarrageProjectileRequestId0 = 50;
        public const int MeteorBarrageProjectileRequestId1 = 51;
        public const int MeteorBarrageProjectileRequestId2 = 52;
        public const int MeteorBarrageProjectileRequestId3 = 53;
        public const int MeteorBarrageProjectileRequestId4 = 54;
        public const int MeteorBarrageProjectileRequestId5 = 55;
        public const string MeteorBarrageProjectileRequestLabel0 = "character_test.meteor_barrage.projectile.0";
        public const string MeteorBarrageProjectileRequestLabel1 = "character_test.meteor_barrage.projectile.1";
        public const string MeteorBarrageProjectileRequestLabel2 = "character_test.meteor_barrage.projectile.2";
        public const string MeteorBarrageProjectileRequestLabel3 = "character_test.meteor_barrage.projectile.3";
        public const string MeteorBarrageProjectileRequestLabel4 = "character_test.meteor_barrage.projectile.4";
        public const string MeteorBarrageProjectileRequestLabel5 = "character_test.meteor_barrage.projectile.5";
        public const int MeteorBarrageProjectileImpactStableEventId = 16;
        public const string MeteorBarrageProjectileImpactStableEventLabel = "character_test.meteor_barrage.projectile.impact";
        public const int MeteorBarrageChargeVfxStableEventId = 112;
        public const string MeteorBarrageChargeVfxStableEventLabel = "character_test.meteor_barrage.charge_vfx";
        public const int MeteorBarrageTelegraphVfxStableEventId = 76;
        public const string MeteorBarrageTelegraphVfxStableEventLabel = "character_test.meteor_barrage.telegraph_vfx";
        public const int SwordWaveProjectileRequestId = 56;
        public const string SwordWaveProjectileRequestLabel = "character_test.sword_wave.projectile";
        public const int SwordWaveProjectileImpactStableEventId = 57;
        public const string SwordWaveProjectileImpactStableEventLabel = "character_test.sword_wave.projectile.impact";
        public const int SwordWaveChargeVfxStableEventId = 113;
        public const string SwordWaveChargeVfxStableEventLabel = "character_test.sword_wave.charge_vfx";
        public const int SwordWaveProjectileCount = 3;
        public const int SwordWaveProjectileFirstFrame = 13;
        public const int SwordWaveProjectileIntervalFrames = 60;
        public const int DashStrikeHitWindowStableEventId = 114;
        public const string DashStrikeHitWindowStableEventLabel = "dash_strike.hit_window";
        public const int DashStrikeHitStartFrame = 6;
        public const int DashStrikeHitStopFrame = 20;
        public const string DashStrikeEquipmentSlotId = "";
        public const int DashStrikeControlLockStableEventId = 115;
        public const string DashStrikeControlLockStableEventLabel = "dash_strike.control_lock";
        public const int DashStrikeControlReleaseStableEventId = 116;
        public const string DashStrikeControlReleaseStableEventLabel = "dash_strike.control_release";
        public const int DashStrikeAnimationStableEventId = 117;
        public const string DashStrikeAnimationStableEventLabel = "dash_strike.animation";
        public const int DashStrikeChargeVfxStableEventId = 118;
        public const string DashStrikeChargeVfxStableEventLabel = "character_test.dash_strike.charge_vfx";
        public const int HookChainProjectileRequestId = 58;
        public const string HookChainProjectileRequestLabel = "character_test.hook_chain.projectile";
        public const int HookChainProjectileImpactStableEventId = 59;
        public const string HookChainProjectileImpactStableEventLabel = "character_test.hook_chain.projectile.impact";
        public const int HookChainChargeVfxStableEventId = 119;
        public const string HookChainChargeVfxStableEventLabel = "character_test.hook_chain.charge_vfx";
        public const int ArcMineGameplayRequestId = 60;
        public const string ArcMineGameplayRequestLabel = "character_test.arc_mine.deploy";
        public const int ArcMineDeployStableEventId = 60;
        public const string ArcMineDeployStableEventLabel = "character_test.arc_mine.deploy";
        public const int ArcMineProjectileImpactStableEventId = 62;
        public const string ArcMineProjectileImpactStableEventLabel = "character_test.arc_mine.projectile.impact";
        public const int ArcMineDropProjectileImpactStableEventId = 63;
        public const string ArcMineDropProjectileImpactStableEventLabel = "character_test.arc_mine.drop_projectile.impact";
        public const int ArcMineTriggerStableEventId = 14;
        public const string ArcMineTriggerStableEventLabel = "character_test.arc_mine.trigger";
        public const int ArcMineDeployVfxStableEventId = 82;
        public const string ArcMineDeployVfxStableEventLabel = "character_test.arc_mine.deploy_vfx";
        public const int ArcMineWaitingVfxStableEventId = 83;
        public const string ArcMineWaitingVfxStableEventLabel = "character_test.arc_mine.waiting_vfx";
        public const int ArcMineFuseVfxStableEventId = 84;
        public const string ArcMineFuseVfxStableEventLabel = "character_test.arc_mine.fuse_vfx";
        public const int ArcMineTriggerVfxStableEventId = 85;
        public const string ArcMineTriggerVfxStableEventLabel = "character_test.arc_mine.trigger_vfx";
        public const int ArcMineAnimationStableEventId = 120;
        public const string ArcMineAnimationStableEventLabel = "arc_mine.animation";
        public const int ArcMineControlLockStableEventId = 121;
        public const string ArcMineControlLockStableEventLabel = "arc_mine.control_lock";
        public const int ArcMineControlReleaseStableEventId = 122;
        public const string ArcMineControlReleaseStableEventLabel = "arc_mine.control_release";
        public const int RuneTurretGameplayRequestId = 64;
        public const string RuneTurretGameplayRequestLabel = "character_test.rune_turret.deploy";
        public const int RuneTurretDeployStableEventId = 64;
        public const string RuneTurretDeployStableEventLabel = "character_test.rune_turret.deploy";
        public const int RuneTurretDeployProjectileImpactStableEventId = 66;
        public const string RuneTurretDeployProjectileImpactStableEventLabel = "character_test.rune_turret.deploy_projectile.impact";
        public const int RuneTurretShotProjectileImpactStableEventId = 67;
        public const string RuneTurretShotProjectileImpactStableEventLabel = "character_test.rune_turret.shot_projectile.impact";
        public const int RuneTurretDeployVfxStableEventId = 123;
        public const string RuneTurretDeployVfxStableEventLabel = "character_test.rune_turret.deploy_vfx";
        public const int RuneTurretIdleVfxStableEventId = 124;
        public const string RuneTurretIdleVfxStableEventLabel = "character_test.rune_turret.idle_vfx";
        public const int RuneTurretFireVfxStableEventId = 125;
        public const string RuneTurretFireVfxStableEventLabel = "character_test.rune_turret.fire_vfx";
        public const int RuneTurretExpireVfxStableEventId = 126;
        public const string RuneTurretExpireVfxStableEventLabel = "character_test.rune_turret.expire_vfx";
        public const int RuneTurretAnimationStableEventId = 127;
        public const string RuneTurretAnimationStableEventLabel = "rune_turret.animation";
        public const int RuneTurretControlLockStableEventId = 128;
        public const string RuneTurretControlLockStableEventLabel = "rune_turret.control_lock";
        public const int RuneTurretControlReleaseStableEventId = 129;
        public const string RuneTurretControlReleaseStableEventLabel = "rune_turret.control_release";
        public const int PoisonTickStableEventId = 15;
        public const string PoisonTickStableEventLabel = "character_test.poisoned.tick";
        public const string JumpAnimationActionKey = "anim.basic_jump";
        public const string JumpAnimationClipResourceKey = "art.character.skeleton.animation.standing_jump";
        public static readonly CharacterActionId LandingLightActionId = new CharacterActionId(398);
        public static readonly CharacterActionId LandingHeavyActionId = new CharacterActionId(399);
        public const string LightLandingAnimationActionKey = "anim.landing.light";
        public const string HeavyLandingAnimationActionKey = "anim.landing.heavy";
        public const string LandingAnimationClipResourceKey =
            "art.character.skeleton.animation.standing_jump_running_landing";
        public const string LightAttackAnimationActionKey = "combat.attack.light";
        public const string LightAttackAnimationClipResourceKey = "art.character.skeleton.animation.katana_atk1";
        public const int FireballPresentationProfileId = 1;
        public const string FireballPresentationProfileLabel = "character_test.fireball.impact";
        public const int FireballBurstPresentationProfileId = 2;
        public const string FireballBurstPresentationProfileLabel = "character_test.fireball_burst.impact";
        public const int FlameLancePresentationProfileId = 3;
        public const string FlameLancePresentationProfileLabel = "character_test.flame_lance.impact";
        public const int TornadoPresentationProfileId = 4;
        public const string TornadoPresentationProfileLabel = "character_test.tornado.impact";
        public const int TornadoTickPresentationProfileId = 17;
        public const string TornadoTickPresentationProfileLabel = "character_test.tornado.tick";
        public const int BeamLancePresentationProfileId = 5;
        public const string BeamLancePresentationProfileLabel = "character_test.beam_lance.impact";
        public const int ChannelBeamPresentationProfileId = 6;
        public const string ChannelBeamPresentationProfileLabel = "character_test.channel_beam.impact";
        public const int LaserLancePresentationProfileId = 7;
        public const string LaserLancePresentationProfileLabel = "character_test.laser_lance.impact";
        public const int ChainLightningPresentationProfileId = 8;
        public const string ChainLightningPresentationProfileLabel = "character_test.chain_lightning.impact";
        public const int PoisonBottlePresentationProfileId = 9;
        public const string PoisonBottlePresentationProfileLabel = "character_test.poison_bottle.shatter";
        public const int PoisonDartPresentationProfileId = PoisonBottlePresentationProfileId;
        public const string PoisonDartPresentationProfileLabel = PoisonBottlePresentationProfileLabel;
        public const int StunBoltPresentationProfileId = 10;
        public const string StunBoltPresentationProfileLabel = "character_test.stun_bolt.impact";
        public const int MeteorBarragePresentationProfileId = 11;
        public const string MeteorBarragePresentationProfileLabel = "character_test.meteor_barrage.impact";
        public const int SwordWavePresentationProfileId = 12;
        public const string SwordWavePresentationProfileLabel = "character_test.sword_wave.impact";
        public const int DashStrikePresentationProfileId = 13;
        public const string DashStrikePresentationProfileLabel = "character_test.dash_strike.impact";
        public const int HookChainPresentationProfileId = 14;
        public const string HookChainPresentationProfileLabel = "character_test.hook_chain.impact";
        public const int ArcMinePresentationProfileId = 15;
        public const string ArcMinePresentationProfileLabel = "character_test.arc_mine.blast";
        public const int RuneTurretPresentationProfileId = 16;
        public const string RuneTurretPresentationProfileLabel = "character_test.rune_turret.impact";
        public const int PoisonTickPresentationProfileId = 18;
        public const string PoisonTickPresentationProfileLabel = "character_test.poison.tick";

        public const string ActionSetsFileName = "character_action_sets.json";
        public const string ActionsFileName = "character_actions.json";
        public const string ReactionProfilesFileName = "character_reaction_profiles.json";
        public const string LandingProfilesFileName = "character_landing_profiles.json";
        public const string ProfilesFileName = "character_action_profile.json";
        internal const string GeneratedActionsRoot = "Assets/MxFrameworkGenerated/CharacterActions";
        internal const string PlayerActionsRelativeRoot = "MxFramework/CharacterActions";

        private static CharacterTestCharacterActionRuntime _inspectionCatalog;
        /// <summary>Read-only authoring/bake inspection data. Never bind this runtime to a world or execute actions on it.</summary>
        /// <remarks>LowFreqAlloc once per imported-content generation. Runtime Load remains independently owned.</remarks>
        internal static CharacterTestCharacterActionRuntime LoadInspectionCatalog()
            => _inspectionCatalog ??= Load();
        internal static void InvalidateInspectionCatalog() => _inspectionCatalog = null;

        public static CharacterTestCharacterActionCatalog LoadCatalog()
        {
            return LoadCatalog(CharacterTestCharacterActionConfigSourceOptions.CreateDefault());
        }

        public static CharacterTestCharacterActionCatalog LoadCatalog(CharacterTestCharacterActionConfigSourceOptions options)
        {
            using var startupProfile = CharacterTestStartupProfiler.LoadActions.Auto();
            CharacterTestCharacterActionConfigSourceOptions safeOptions =
                options ?? CharacterTestCharacterActionConfigSourceOptions.CreateDefault();
            CharacterTestCharacterActionConfigSourcePayload payload = ResolvePayload(safeOptions);
            return BuildCatalog(payload);
        }

        public static CharacterTestCharacterActionRuntime Load()
        {
            return Load(CharacterTestCharacterActionConfigSourceOptions.CreateDefault());
        }

        public static CharacterTestCharacterActionRuntime Load(CharacterTestCharacterActionConfigSourceOptions options)
        {
            return new CharacterTestCharacterActionRuntime(LoadCatalog(options));
        }


        public static string[] CreateDefaultCandidateDirectories()
        {
            return CreateCandidateDirectories(GeneratedActionsRoot,
                UnityEngine.Application.streamingAssetsPath, UnityEngine.Application.isEditor);
        }

        internal static string[] CreateCandidateDirectories(
            string generatedRoot, string streamingAssetsRoot, bool isEditor)
        {
            var directories = new System.Collections.Generic.List<string>();
            if (isEditor && !string.IsNullOrWhiteSpace(generatedRoot))
                AddDiscoveredActionDirectories(directories, generatedRoot);
            if (!string.IsNullOrWhiteSpace(streamingAssetsRoot))
                AddDiscoveredActionDirectories(directories,
                    System.IO.Path.Combine(streamingAssetsRoot, PlayerActionsRelativeRoot));
            return directories.ToArray();
        }

        private static void AddDiscoveredActionDirectories(
            System.Collections.Generic.List<string> directories,
            string root)
        {
            AddChildDirectories(directories, root + "/Profiles");
            AddChildDirectories(directories, root + "/ActionSets");
            AddChildDirectories(directories, root + "/Libraries");
        }

        private static void AddChildDirectories(
            System.Collections.Generic.List<string> directories,
            string root)
        {
            if (!System.IO.Directory.Exists(root))
                return;

            string[] children = System.IO.Directory.GetDirectories(root);
            Array.Sort(children, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < children.Length; i++)
            {
                string normalized = NormalizePath(children[i]);
                if (!directories.Contains(normalized))
                    directories.Add(normalized);
            }
        }
    }
}
