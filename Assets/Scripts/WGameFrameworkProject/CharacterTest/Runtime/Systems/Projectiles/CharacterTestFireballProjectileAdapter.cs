using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestFireballProjectileAdapter
    {
        public const int DefinitionId = 91001;
        public const int BurstDefinitionId = 91002;
        public const int FlameLanceDefinitionId = 91003;
        public const int FireballLobDefinitionId = 91004;
        public const int FireballSurgeDefinitionId = 91005;
        public const int FireballReturnDefinitionId = 91006;
        public const int FireballSwayDefinitionId = 91007;
        public const int FireballHomingDefinitionId = 91008;
        public const int FireballBezierDefinitionId = 91009;
        public const int FireballExplosionDefinitionId = 91010;
        public const int TornadoDefinitionId = 91011;
        public const int BeamLanceDefinitionId = 91012;
        public const int ChannelBeamDefinitionId = 91013;
        public const int LaserLanceDefinitionId = 91014;
        public const int PoisonBottleDefinitionId = 91015;
        public const int PoisonDartDefinitionId = PoisonBottleDefinitionId;
        public const int StunBoltDefinitionId = 91016;
        public const int MeteorBarrageDefinitionId = 91017;
        public const int SwordWaveDefinitionId = 91018;
        public const int HookChainDefinitionId = 91019;
        public const int ArcMineDefinitionId = 91020;
        public const int ArcMineDropDefinitionId = 91021;
        public const int RuneTurretDeployDefinitionId = 91022;
        public const int RuneTurretShotDefinitionId = 91023;
        public const int MeteorBarrageProjectileCount = 6;
        public const int RequestId = CharacterTestAuthoredContentIds.D401_Id17;
        public const string RequestLabel = "character_test.fireball.projectile";
        public const string StableId = "character_test.fireball.projectile";
        public const int ImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id18;
        public const string ImpactStableEventLabel = "character_test.fireball.projectile.impact";
        public const int BurstRequestId = CharacterTestAuthoredContentIds.D401_Id19;
        public const string BurstRequestLabel = "character_test.fireball_burst.projectile";
        public const string BurstStableId = "character_test.fireball_burst.projectile";
        public const int BurstImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id20;
        public const string BurstImpactStableEventLabel = "character_test.fireball_burst.projectile.impact";
        public const int FlameLanceRequestId = CharacterTestAuthoredContentIds.D401_Id21;
        public const string FlameLanceRequestLabel = "character_test.flame_lance.projectile";
        public const string FlameLanceStableId = "character_test.flame_lance.projectile";
        public const int FlameLanceImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id22;
        public const string FlameLanceImpactStableEventLabel = "character_test.flame_lance.projectile.impact";
        public const int FireballLobRequestId = CharacterTestAuthoredContentIds.D401_Id23;
        public const string FireballLobRequestLabel = "character_test.fireball_lob.projectile";
        public const string FireballLobStableId = "character_test.fireball_lob.projectile";
        public const int FireballLobImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id24;
        public const string FireballLobImpactStableEventLabel = "character_test.fireball_lob.projectile.impact";
        public const int FireballSurgeRequestId = CharacterTestAuthoredContentIds.D401_Id25;
        public const string FireballSurgeRequestLabel = "character_test.fireball_surge.projectile";
        public const string FireballSurgeStableId = "character_test.fireball_surge.projectile";
        public const int FireballSurgeImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id26;
        public const string FireballSurgeImpactStableEventLabel = "character_test.fireball_surge.projectile.impact";
        public const int FireballReturnRequestId = CharacterTestAuthoredContentIds.D401_Id27;
        public const string FireballReturnRequestLabel = "character_test.fireball_return.projectile";
        public const string FireballReturnStableId = "character_test.fireball_return.projectile";
        public const int FireballReturnImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id28;
        public const string FireballReturnImpactStableEventLabel = "character_test.fireball_return.projectile.impact";
        public const int FireballSwayRequestId = CharacterTestAuthoredContentIds.D401_Id29;
        public const string FireballSwayRequestLabel = "character_test.fireball_sway.projectile";
        public const string FireballSwayStableId = "character_test.fireball_sway.projectile";
        public const int FireballSwayImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id30;
        public const string FireballSwayImpactStableEventLabel = "character_test.fireball_sway.projectile.impact";
        public const int FireballHomingRequestId = CharacterTestAuthoredContentIds.D401_Id31;
        public const string FireballHomingRequestLabel = "character_test.fireball_homing.projectile";
        public const string FireballHomingStableId = "character_test.fireball_homing.projectile";
        public const int FireballHomingImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id32;
        public const string FireballHomingImpactStableEventLabel = "character_test.fireball_homing.projectile.impact";
        public const int FireballBezierRequestId = CharacterTestAuthoredContentIds.D401_Id33;
        public const string FireballBezierRequestLabel = "character_test.fireball_bezier.projectile";
        public const string FireballBezierStableId = "character_test.fireball_bezier.projectile";
        public const int FireballBezierImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id34;
        public const string FireballBezierImpactStableEventLabel = "character_test.fireball_bezier.projectile.impact";
        public const int FireballExplosionRequestId = CharacterTestAuthoredContentIds.D401_Id35;
        public const string FireballExplosionRequestLabel = "character_test.fireball_explosion.projectile";
        public const string FireballExplosionStableId = "character_test.fireball_explosion.projectile";
        public const int FireballExplosionImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id36;
        public const string FireballExplosionImpactStableEventLabel = "character_test.fireball_explosion.projectile.impact";
        public const int TornadoRequestId = CharacterTestAuthoredContentIds.D401_Id37;
        public const string TornadoRequestLabel = "character_test.tornado.projectile";
        public const string TornadoStableId = "character_test.tornado.projectile";
        public const int TornadoImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id38;
        public const string TornadoImpactStableEventLabel = "character_test.tornado.projectile.impact";
        public const int TornadoTickStableEventId = CharacterTestAuthoredContentIds.D401_Id39;
        public const string TornadoTickStableEventLabel = "character_test.tornado.projectile.tick";
        public const int BeamLanceRequestId = CharacterTestAuthoredContentIds.D401_Id40;
        public const string BeamLanceRequestLabel = "character_test.beam_lance.projectile";
        public const string BeamLanceStableId = "character_test.beam_lance.projectile";
        public const int BeamLanceImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id41;
        public const string BeamLanceImpactStableEventLabel = "character_test.beam_lance.projectile.impact";
        public const int ChannelBeamRequestId = CharacterTestAuthoredContentIds.D401_Id42;
        public const string ChannelBeamRequestLabel = "character_test.channel_beam.tick_projectile";
        public const string ChannelBeamStableId = "character_test.channel_beam.tick_projectile";
        public const int ChannelBeamImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id43;
        public const string ChannelBeamImpactStableEventLabel = "character_test.channel_beam.tick_projectile.impact";
        public const int LaserLanceRequestId = CharacterTestAuthoredContentIds.D401_Id44;
        public const string LaserLanceRequestLabel = "character_test.laser_lance.projectile";
        public const string LaserLanceStableId = "character_test.laser_lance.projectile";
        public const int LaserLanceImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id45;
        public const string LaserLanceImpactStableEventLabel = "character_test.laser_lance.projectile.impact";
        public const int PoisonBottleRequestId = CharacterTestAuthoredContentIds.D401_Id46;
        public const string PoisonBottleRequestLabel = "character_test.poison_bottle.projectile";
        public const string PoisonBottleStableId = "character_test.poison_bottle.projectile";
        public const int PoisonBottleImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id47;
        public const string PoisonBottleImpactStableEventLabel = "character_test.poison_bottle.projectile.shatter";
        public const int PoisonDartRequestId = CharacterTestAuthoredContentIds.D401_Id46;
        public const string PoisonDartRequestLabel = "character_test.poison_bottle.projectile";
        public const string PoisonDartStableId = "character_test.poison_bottle.projectile";
        public const int PoisonDartImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id47;
        public const string PoisonDartImpactStableEventLabel = "character_test.poison_bottle.projectile.shatter";
        public const int StunBoltRequestId = CharacterTestAuthoredContentIds.D401_Id48;
        public const string StunBoltRequestLabel = "character_test.stun_bolt.projectile";
        public const string StunBoltStableId = "character_test.stun_bolt.projectile";
        public const int StunBoltImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id49;
        public const string StunBoltImpactStableEventLabel = "character_test.stun_bolt.projectile.impact";
        public const int MeteorBarrageRequestId0 = CharacterTestAuthoredContentIds.D401_Id50;
        public const string MeteorBarrageRequestLabel0 = "character_test.meteor_barrage.projectile.0";
        public const int MeteorBarrageRequestId1 = CharacterTestAuthoredContentIds.D401_Id51;
        public const string MeteorBarrageRequestLabel1 = "character_test.meteor_barrage.projectile.1";
        public const int MeteorBarrageRequestId2 = CharacterTestAuthoredContentIds.D401_Id52;
        public const string MeteorBarrageRequestLabel2 = "character_test.meteor_barrage.projectile.2";
        public const int MeteorBarrageRequestId3 = CharacterTestAuthoredContentIds.D401_Id53;
        public const string MeteorBarrageRequestLabel3 = "character_test.meteor_barrage.projectile.3";
        public const int MeteorBarrageRequestId4 = CharacterTestAuthoredContentIds.D401_Id54;
        public const string MeteorBarrageRequestLabel4 = "character_test.meteor_barrage.projectile.4";
        public const int MeteorBarrageRequestId5 = CharacterTestAuthoredContentIds.D401_Id55;
        public const string MeteorBarrageRequestLabel5 = "character_test.meteor_barrage.projectile.5";
        public const string MeteorBarrageStableId = "character_test.meteor_barrage.projectile";
        public const int MeteorBarrageImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id16;
        public const string MeteorBarrageImpactStableEventLabel = "character_test.meteor_barrage.projectile.impact";
        public const int SwordWaveRequestId = CharacterTestAuthoredContentIds.D401_Id56;
        public const string SwordWaveRequestLabel = "character_test.sword_wave.projectile";
        public const string SwordWaveStableId = "character_test.sword_wave.projectile";
        public const int SwordWaveImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id57;
        public const string SwordWaveImpactStableEventLabel = "character_test.sword_wave.projectile.impact";
        public const int HookChainRequestId = CharacterTestAuthoredContentIds.D401_Id58;
        public const string HookChainRequestLabel = "character_test.hook_chain.projectile";
        public const string HookChainStableId = "character_test.hook_chain.projectile";
        public const int HookChainImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id59;
        public const string HookChainImpactStableEventLabel = "character_test.hook_chain.projectile.impact";
        public const int ArcMineRequestId = CharacterTestAuthoredContentIds.D401_Id60;
        public const string ArcMineRequestLabel = "character_test.arc_mine.deploy";
        public const string ArcMineStableId = "character_test.arc_mine.projectile";
        public const int ArcMineDropRequestId = CharacterTestAuthoredContentIds.D401_Id61;
        public const string ArcMineDropRequestLabel = "character_test.arc_mine.drop_projectile";
        public const string ArcMineDropStableId = "character_test.arc_mine.drop_projectile";
        public const int ArcMineImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id62;
        public const string ArcMineImpactStableEventLabel = "character_test.arc_mine.projectile.impact";
        public const int ArcMineDropImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id63;
        public const string ArcMineDropImpactStableEventLabel = "character_test.arc_mine.drop_projectile.impact";
        public const int RuneTurretDeployRequestId = CharacterTestAuthoredContentIds.D401_Id64;
        public const string RuneTurretDeployRequestLabel = "character_test.rune_turret.deploy";
        public const string RuneTurretDeployStableId = "character_test.rune_turret.deploy_projectile";
        public const int RuneTurretShotRequestId = CharacterTestAuthoredContentIds.D401_Id65;
        public const string RuneTurretShotRequestLabel = "character_test.rune_turret.shot_projectile";
        public const string RuneTurretShotStableId = "character_test.rune_turret.shot_projectile";
        public const int RuneTurretDeployImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id66;
        public const string RuneTurretDeployImpactStableEventLabel = "character_test.rune_turret.deploy_projectile.impact";
        public const int RuneTurretShotImpactStableEventId = CharacterTestAuthoredContentIds.D401_Id67;
        public const string RuneTurretShotImpactStableEventLabel = "character_test.rune_turret.shot_projectile.impact";
        public const int TornadoLifetimeFrames = 180;
        public const int TornadoLateralCycles = 3;
        public const int TornadoDamageTickIntervalFrames = 30;
        public const string DefaultGeneratedDefinitionsPath =
            "Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_definitions.json";
        public const string DefaultGeneratedSpawnProfilesPath =
            "Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_spawn_profiles.json";

        public static readonly Fix64 TornadoSpeedPerFrame = Fix64.FromRatio(5, 100);
        public static readonly Fix64 TornadoLateralAmplitude = Fix64.FromRatio(9, 10);
        public static readonly Fix64 TornadoDamageTickRadius = Fix64.FromRatio(5, 2);
        public static readonly Fix64 TornadoColumnBottomOffset = Fix64.One;
        public static readonly Fix64 TornadoColumnTopOffset = Fix64.FromRatio(5, 2);
        public static readonly Fix64 ChannelBeamRange = Fix64.FromInt(32);
        public static readonly Fix64 ChannelBeamRadius = Fix64.FromRatio(18, 100);
        public static readonly Fix64 LaserLanceRange = Fix64.FromInt(44);
        public static readonly Fix64 LaserLanceRadius = Fix64.FromRatio(6, 100);
        public static readonly Fix64 PoisonBottleRadius = Fix64.FromRatio(20, 100);
        public const int PoisonBottleLifetimeFrames = 90;
        public static readonly Fix64 PoisonBottleSpeedPerFrame = Fix64.FromRatio(3535533906L, 10000000000L);
        public static readonly Fix64 PoisonBottleGravityPerFrameSquared = Fix64.FromRatio(1, 120);
        public static readonly Fix64 PoisonBottleRange =
            CombatProjectileBallistics.CalculateSameHeightMaxRange(
                PoisonBottleSpeedPerFrame,
                PoisonBottleGravityPerFrameSquared,
                Fix64.Zero);
        public static readonly Fix64 PoisonBottleArcHeight = Fix64.FromRatio(135, 16);
        public static readonly Fix64 PoisonDartRange = PoisonBottleRange;
        public static readonly Fix64 PoisonDartRadius = PoisonBottleRadius;
        public static readonly Fix64 StunBoltRange = Fix64.FromInt(32);
        public static readonly Fix64 StunBoltRadius = Fix64.FromRatio(18, 100);
        public static readonly Fix64 StunBoltSpeedPerFrame = Fix64.FromRatio(45, 100);
        public const int StunBoltLifetimeFrames = 90;
        public static readonly Fix64 MeteorBarrageRange = Fix64.FromInt(18);
        public static readonly Fix64 MeteorBarrageRadius = Fix64.FromRatio(25, 100);
        public static readonly Fix64 MeteorBarrageSpeedPerFrame = Fix64.FromRatio(28, 100);
        public static readonly Fix64 MeteorBarrageAreaRadius = Fix64.FromInt(2);
        public static readonly Fix64 MeteorBarrageSkyHeight = Fix64.FromInt(8);
        public static readonly Fix64 MeteorBarrageSkyBackstep = Fix64.FromInt(2);
        public const int MeteorBarrageLifetimeFrames = 150;
        public static readonly Fix64 SwordWaveRange = Fix64.FromInt(30);
        public const int SwordWaveCollisionSampleCount = 5;
        public static readonly Fix64 SwordWaveCollisionHalfWidth = Fix64.FromRatio(85, 100);
        public static readonly Fix64 SwordWaveRadius = Fix64.FromRatio(22, 100);
        public static readonly Fix64 SwordWaveSpeedPerFrame = Fix64.FromRatio(75, 100);
        public const int SwordWaveLifetimeFrames = 40;
        public const int SwordWaveImpactPower = 8;
        public static readonly Fix64 HookChainRange = Fix64.FromInt(12);
        public static readonly Fix64 HookChainRadius = Fix64.FromRatio(18, 100);
        public static readonly Fix64 HookChainSpeedPerFrame = Fix64.FromRatio(75, 100);
        public const int HookChainLifetimeFrames = 16;
        public const int HookChainImpactPower = 4;
        public static readonly Fix64 ArcMineRadius = Fix64.FromRatio(18, 100);
        public static readonly Fix64 ArcMineSpeedPerFrame = PoisonBottleSpeedPerFrame;
        public static readonly Fix64 ArcMineGravityPerFrameSquared = PoisonBottleGravityPerFrameSquared;
        public static readonly Fix64 ArcMineDropHorizontalCarry = Fix64.FromRatio(25, 100);
        public const int ArcMineLifetimeFrames = 90;
        public static readonly Fix64 ArcMineRange =
            CombatProjectileBallistics.CalculateSameHeightMaxRange(
                ArcMineSpeedPerFrame,
                ArcMineGravityPerFrameSquared,
                Fix64.Zero);
        public static readonly Fix64 RuneTurretDeployRadius = Fix64.FromRatio(18, 100);
        public static readonly Fix64 RuneTurretDeploySpeedPerFrame = ArcMineSpeedPerFrame;
        public static readonly Fix64 RuneTurretDeployGravityPerFrameSquared = ArcMineGravityPerFrameSquared;
        public const int RuneTurretDeployLifetimeFrames = 90;
        public static readonly Fix64 RuneTurretDeployRange =
            CombatProjectileBallistics.CalculateSameHeightMaxRange(
                RuneTurretDeploySpeedPerFrame,
                RuneTurretDeployGravityPerFrameSquared,
                Fix64.Zero);
        public static readonly Fix64 RuneTurretShotRange = Fix64.FromInt(12);
        public static readonly Fix64 RuneTurretShotRadius = Fix64.FromRatio(16, 100);
        public static readonly Fix64 RuneTurretShotSpeedPerFrame = Fix64.FromRatio(60, 100);
        public const int RuneTurretShotLifetimeFrames = 20;
        public const int RuneTurretShotImpactPower = 4;
        public const float SwordWaveFirstRollDegrees = 0f;
        public const float SwordWaveSecondRollDegrees = -55f;
        public const float SwordWaveThirdRollDegrees = 55f;
        public const int MeteorBarrageStaticEnvironmentLayer = 1;
        public static readonly CombatPhysicsLayerMask MeteorBarrageCollisionLayerMask =
            CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer)
                .AddLayer(MeteorBarrageStaticEnvironmentLayer);

        private static readonly CombatProjectileImpactConfig Impact = CreateDirectImpact(
            ImpactStableEventId,
            CreateFireballDamage(),
            applyOnHitDefenseBreak: true,
            stableEventLabel: ImpactStableEventLabel);
        private static readonly CombatProjectileImpactConfig BurstImpact = CreateAreaImpact(
            BurstImpactStableEventId,
            CreateFireballBurstDamage(),
            applyOnHitDefenseBreak: true);
        private static readonly CombatProjectileImpactConfig FlameLanceImpact = CreateDirectImpact(
            FlameLanceImpactStableEventId,
            CreateFlameLanceDamage(),
            maxAcceptedHits: 3,
            hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent);
        private static readonly CombatProjectileImpactConfig FireballLobImpact = CreateAreaImpact(
            FireballLobImpactStableEventId,
            CreateFireballBurstDamage());
        private static readonly CombatProjectileImpactConfig FireballSurgeImpact = CreateDirectImpact(
            FireballSurgeImpactStableEventId,
            CreateFireballDamage());
        private static readonly CombatProjectileImpactConfig FireballReturnImpact = CreateDirectImpact(
            FireballReturnImpactStableEventId,
            CreateFireballDamage());
        private static readonly CombatProjectileImpactConfig FireballSwayImpact = CreateDirectImpact(
            FireballSwayImpactStableEventId,
            CreateFireballDamage());
        private static readonly CombatProjectileImpactConfig FireballHomingImpact = CreateDirectImpact(
            FireballHomingImpactStableEventId,
            CreateFireballDamage());
        private static readonly CombatProjectileImpactConfig FireballBezierImpact = CreateDirectImpact(
            FireballBezierImpactStableEventId,
            CreateFireballDamage());
        private static readonly CombatProjectileImpactConfig FireballExplosionImpact = CreateAreaImpact(
            FireballExplosionImpactStableEventId,
            CreateFireballBurstDamage(),
            areaRadius: Fix64.FromRatio(5, 2),
            effectTimelineId: CharacterTestAuthoredContentIds.D401_Id1);
        private static readonly CombatProjectileImpactConfig TornadoImpact = CreateAreaImpact(
            TornadoImpactStableEventId,
            CreateTornadoDamage(),
            areaRadius: Fix64.FromRatio(5, 2));
        private static readonly CombatProjectileImpactConfig TornadoTickImpact = CreateAreaImpact(
            TornadoTickStableEventId,
            CreateTornadoTickDamage(),
            areaRadius: TornadoDamageTickRadius);
        private static readonly CombatProjectileImpactConfig BeamLanceImpact = CreateDirectImpact(
            BeamLanceImpactStableEventId,
            CreateBeamLanceDamage(),
            maxAcceptedHits: 5,
            hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent,
            onHitBuffs: CreateBeamLanceStasisBuffs());
        private static readonly CombatProjectileImpactConfig ChannelBeamTickImpact = CreateDirectImpact(
            ChannelBeamImpactStableEventId,
            CreateChannelBeamTickDamage(),
            maxAcceptedHits: 3,
            hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent);
        private static readonly CombatProjectileImpactConfig LaserLanceImpact = CreateDirectImpact(
            LaserLanceImpactStableEventId,
            CreateLaserLanceDamage(),
            maxAcceptedHits: 1,
            hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent);
        private static readonly CombatProjectileImpactConfig PoisonBottleImpact = CreateDirectImpact(
            PoisonBottleImpactStableEventId,
            CreatePoisonBottleShatterDamage(),
            maxAcceptedHits: 1,
            hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent);
        private static readonly CombatProjectileImpactConfig PoisonDartImpact = PoisonBottleImpact;
        private static readonly CombatProjectileImpactConfig StunBoltImpact = CreateDirectImpact(
            StunBoltImpactStableEventId,
            CreateStunBoltDamage(),
            maxAcceptedHits: 1,
            hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent,
            onHitBuffs: CreateStunBoltBuffs());
        private static readonly CombatProjectileImpactConfig MeteorBarrageImpact = CreateMeteorBarrageImpact();
        private static readonly CombatProjectileImpactConfig SwordWaveImpact = CreateDirectImpact(
            SwordWaveImpactStableEventId,
            CreateSwordWaveDamage(),
            maxAcceptedHits: 4,
            hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent,
            targetTypes: CombatHitTargetTypes.HostileCharacter
                | CombatHitTargetTypes.WorldWall
                | CombatHitTargetTypes.WorldGround
                | CombatHitTargetTypes.WorldStatic,
            environmentHitPolicy: CombatEnvironmentHitPolicy.BlockedPresentation);
        private static readonly CombatProjectileImpactConfig HookChainImpact = CreateDirectImpact(
            HookChainImpactStableEventId,
            CreateHookChainDamage(),
            maxAcceptedHits: 1,
            hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent,
            targetTypes: CombatHitTargetTypes.HostileCharacter
                | CombatHitTargetTypes.WorldWall
                | CombatHitTargetTypes.WorldGround
                | CombatHitTargetTypes.WorldStatic,
            environmentHitPolicy: CombatEnvironmentHitPolicy.BlockedPresentation);
        private static readonly CombatProjectileImpactConfig ArcMineImpact = CreateDirectImpact(
            ArcMineImpactStableEventId,
            CreateArcMineCarrierDamage(),
            maxAcceptedHits: 1,
            hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent,
            targetTypes: CombatHitTargetTypes.HostileCharacter
                | CombatHitTargetTypes.WorldWall
                | CombatHitTargetTypes.WorldGround
                | CombatHitTargetTypes.WorldStatic,
            environmentHitPolicy: CombatEnvironmentHitPolicy.BlockedPresentation);
        private static readonly CombatProjectileImpactConfig ArcMineDropImpact = CreateDirectImpact(
            ArcMineDropImpactStableEventId,
            CreateArcMineDropDamage(),
            maxAcceptedHits: 1,
            hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent,
            targetTypes: CombatHitTargetTypes.WorldWall
                | CombatHitTargetTypes.WorldGround
                | CombatHitTargetTypes.WorldStatic,
            environmentHitPolicy: CombatEnvironmentHitPolicy.BlockedPresentation);
        private static readonly CombatProjectileImpactConfig RuneTurretDeployImpact = CreateDirectImpact(
            RuneTurretDeployImpactStableEventId,
            CreateRuneTurretDeployDamage(),
            maxAcceptedHits: 1,
            hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent,
            targetTypes: CombatHitTargetTypes.WorldWall
                | CombatHitTargetTypes.WorldGround
                | CombatHitTargetTypes.WorldStatic,
            environmentHitPolicy: CombatEnvironmentHitPolicy.BlockedPresentation);
        private static readonly CombatProjectileImpactConfig RuneTurretShotImpact = CreateDirectImpact(
            RuneTurretShotImpactStableEventId,
            CreateRuneTurretShotDamage(),
            maxAcceptedHits: 1,
            hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent,
            targetTypes: CombatHitTargetTypes.HostileCharacter
                | CombatHitTargetTypes.WorldWall
                | CombatHitTargetTypes.WorldGround
                | CombatHitTargetTypes.WorldStatic,
            environmentHitPolicy: CombatEnvironmentHitPolicy.BlockedPresentation);

        public static CombatProjectileImpactConfig ImpactConfig => Impact;
        public static CombatProjectileImpactConfig BurstImpactConfig => BurstImpact;
        public static CombatProjectileImpactConfig FlameLanceImpactConfig => FlameLanceImpact;
        public static CombatProjectileImpactConfig FireballLobImpactConfig => FireballLobImpact;
        public static CombatProjectileImpactConfig FireballSurgeImpactConfig => FireballSurgeImpact;
        public static CombatProjectileImpactConfig FireballReturnImpactConfig => FireballReturnImpact;
        public static CombatProjectileImpactConfig FireballSwayImpactConfig => FireballSwayImpact;
        public static CombatProjectileImpactConfig FireballHomingImpactConfig => FireballHomingImpact;
        public static CombatProjectileImpactConfig FireballBezierImpactConfig => FireballBezierImpact;
        public static CombatProjectileImpactConfig FireballExplosionImpactConfig => FireballExplosionImpact;
        public static CombatProjectileImpactConfig TornadoImpactConfig => TornadoImpact;
        public static CombatProjectileImpactConfig TornadoTickImpactConfig => TornadoTickImpact;
        public static CombatProjectileImpactConfig BeamLanceImpactConfig => BeamLanceImpact;
        public static CombatProjectileImpactConfig ChannelBeamTickImpactConfig => ChannelBeamTickImpact;
        public static CombatProjectileImpactConfig LaserLanceImpactConfig => LaserLanceImpact;
        public static CombatProjectileImpactConfig PoisonDartImpactConfig => PoisonDartImpact;
        public static CombatProjectileImpactConfig StunBoltImpactConfig => StunBoltImpact;
        public static CombatProjectileImpactConfig MeteorBarrageImpactConfig => MeteorBarrageImpact;
        public static CombatProjectileImpactConfig SwordWaveImpactConfig => SwordWaveImpact;
        public static CombatProjectileImpactConfig HookChainImpactConfig => HookChainImpact;
        public static CombatProjectileImpactConfig ArcMineImpactConfig => ArcMineImpact;
        public static CombatProjectileImpactConfig ArcMineDropImpactConfig => ArcMineDropImpact;
        public static CombatProjectileImpactConfig RuneTurretDeployImpactConfig => RuneTurretDeployImpact;
        public static CombatProjectileImpactConfig RuneTurretShotImpactConfig => RuneTurretShotImpact;

        private static CombatProjectileImpactConfig CreateDirectImpact(
            int stableEventId,
            CombatHitDamageConfig damage,
            int maxAcceptedHits = 1,
            CombatHitSameTargetPolicy hitSameTargetPolicy = CombatHitSameTargetPolicy.AllowRepeat,
            bool applyOnHitDefenseBreak = false,
            CombatHitBuffApplicationConfig[] onHitBuffs = null,
            string stableEventLabel = "")
        {
            return new CombatProjectileImpactConfig(
                CreateHostileHitEvent(
                    stableEventId,
                    damage,
                    maxAcceptedHits,
                    hitSameTargetPolicy,
                    applyOnHitDefenseBreak,
                    onHitBuffs,
                    stableEventLabel: stableEventLabel),
                stableEventId,
                stableEventLabel: stableEventLabel);
        }

        private static CombatProjectileImpactConfig CreateDirectImpact(
            int stableEventId,
            CombatHitDamageConfig damage,
            int maxAcceptedHits,
            CombatHitSameTargetPolicy hitSameTargetPolicy,
            CombatHitTargetTypes targetTypes,
            CombatEnvironmentHitPolicy environmentHitPolicy)
        {
            return new CombatProjectileImpactConfig(
                CreateHostileHitEvent(
                    stableEventId,
                    damage,
                    maxAcceptedHits,
                    hitSameTargetPolicy,
                    targetTypes: targetTypes,
                    environmentHitPolicy: environmentHitPolicy),
                stableEventId);
        }

        private static CombatProjectileImpactConfig CreateAreaImpact(
            int stableEventId,
            CombatHitDamageConfig damage,
            bool applyOnHitDefenseBreak = false,
            Fix64 areaRadius = default,
            int effectTimelineId = 0,
            int maxAcceptedHits = 1,
            CombatHitSameTargetPolicy hitSameTargetPolicy = CombatHitSameTargetPolicy.AllowRepeat)
        {
            CombatHitEventConfig hitEvent = CreateHostileHitEvent(
                stableEventId,
                damage,
                maxAcceptedHits,
                hitSameTargetPolicy,
                applyOnHitDefenseBreak: applyOnHitDefenseBreak);
            Fix64 effectiveRadius = areaRadius > Fix64.Zero ? areaRadius : Fix64.FromInt(2);
            return new CombatProjectileImpactConfig(
                0,
                stableEventId,
                CombatProjectileImpactPolicyKind.AreaPulseOnImpact,
                effectiveRadius,
                CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                triggerOnExpire: true,
                hitEvent,
                hitEvent,
                effectTimelineId);
        }

        private static CombatProjectileImpactConfig CreateMeteorBarrageImpact()
        {
            CombatHitDamageConfig damage = CreateMeteorBarrageDamage();
            CombatHitEventConfig directHitEvent = CreateHostileHitEvent(
                MeteorBarrageImpactStableEventId,
                damage,
                maxAcceptedHits: 8,
                hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent,
                targetTypes: CombatHitTargetTypes.HostileCharacter
                    | CombatHitTargetTypes.WorldGround
                    | CombatHitTargetTypes.WorldWall
                    | CombatHitTargetTypes.WorldStatic,
                environmentHitPolicy: CombatEnvironmentHitPolicy.BlockedPresentation);
            CombatHitEventConfig areaHitEvent = CreateHostileHitEvent(
                MeteorBarrageImpactStableEventId,
                damage,
                maxAcceptedHits: 8,
                hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent);
            return new CombatProjectileImpactConfig(
                0,
                MeteorBarrageImpactStableEventId,
                CombatProjectileImpactPolicyKind.AreaPulseOnImpact,
                MeteorBarrageAreaRadius,
                CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                triggerOnExpire: true,
                directHitEvent,
                areaHitEvent,
                0);
        }

        private static CombatHitEventConfig CreateHostileHitEvent(
            int stableEventId,
            CombatHitDamageConfig damage,
            int maxAcceptedHits = 1,
            CombatHitSameTargetPolicy hitSameTargetPolicy = CombatHitSameTargetPolicy.AllowRepeat,
            bool applyOnHitDefenseBreak = false,
            CombatHitBuffApplicationConfig[] onHitBuffs = null,
            CombatHitTargetTypes targetTypes = CombatHitTargetTypes.HostileCharacter,
            CombatEnvironmentHitPolicy environmentHitPolicy = CombatEnvironmentHitPolicy.None,
            string stableEventLabel = "")
        {
            return new CombatHitEventConfig(
                targetTypes,
                environmentHitPolicy,
                stableEventId,
                maxAcceptedHits,
                hitSameTargetPolicy,
                damage,
                onHitBuffs: onHitBuffs ?? (applyOnHitDefenseBreak ? CreateOnHitDefenseBreakBuffs() : null),
                stableEventLabel: stableEventLabel);
        }

        private static CombatHitBuffApplicationConfig[] CreateOnHitDefenseBreakBuffs()
        {
            return new[]
            {
                new CombatHitBuffApplicationConfig(CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId)
            };
        }

        private static CombatHitBuffApplicationConfig[] CreateBeamLanceStasisBuffs()
        {
            return new[]
            {
                new CombatHitBuffApplicationConfig(CharacterTestStasisIds.StasisBuffId)
            };
        }

        private static CombatHitBuffApplicationConfig[] CreatePoisonedBuffs()
        {
            return new[]
            {
                new CombatHitBuffApplicationConfig(CharacterTestPoisonIds.PoisonedBuffId)
            };
        }

        private static CombatHitBuffApplicationConfig[] CreateStunBoltBuffs()
        {
            return new[]
            {
                new CombatHitBuffApplicationConfig(CharacterTestStunIds.StunBuffId)
            };
        }

        private static CombatHitDamageConfig CreateFireballDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id3,
                10,
                8,
                new[] { CharacterTestAuthoredContentIds.D410_Id3, CharacterTestAuthoredContentIds.D410_Id4, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id6 },
                CharacterTestAuthoredContentIds.D411_Id2,
                launchPower: 25,
                damageTypeLabel: "fire",
                analysisCategoryLabel: "FireballImpact",
                tagLabels: new[] { "area", "fireball", "magic", "scatter" });
        }

        private static CombatHitDamageConfig CreateFireballBurstDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id3,
                8,
                6,
                new[] { CharacterTestAuthoredContentIds.D410_Id3, CharacterTestAuthoredContentIds.D410_Id7, CharacterTestAuthoredContentIds.D410_Id4, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id6 },
                CharacterTestAuthoredContentIds.D411_Id3);
        }

        private static CombatHitDamageConfig CreateFlameLanceDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id3,
                7,
                5,
                new[] { CharacterTestAuthoredContentIds.D410_Id8, CharacterTestAuthoredContentIds.D410_Id9, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id10, CharacterTestAuthoredContentIds.D410_Id6 },
                CharacterTestAuthoredContentIds.D411_Id4);
        }

        private static CombatHitDamageConfig CreateTornadoDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id8,
                7,
                10,
                new[] { CharacterTestAuthoredContentIds.D410_Id3, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id6, CharacterTestAuthoredContentIds.D410_Id11, CharacterTestAuthoredContentIds.D410_Id12 },
                CharacterTestAuthoredContentIds.D411_Id5);
        }

        private static CombatHitDamageConfig CreateTornadoTickDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id8,
                2,
                3,
                new[] { CharacterTestAuthoredContentIds.D410_Id3, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id6, CharacterTestAuthoredContentIds.D410_Id17, CharacterTestAuthoredContentIds.D410_Id11, CharacterTestAuthoredContentIds.D410_Id12 },
                CharacterTestAuthoredContentIds.D411_Id18);
        }

        private static CombatHitDamageConfig CreateBeamLanceDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id9,
                8,
                4,
                new[] { CharacterTestAuthoredContentIds.D410_Id13, CharacterTestAuthoredContentIds.D410_Id14, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id10, CharacterTestAuthoredContentIds.D410_Id6 },
                CharacterTestAuthoredContentIds.D411_Id6);
        }

        private static CombatHitDamageConfig CreateChannelBeamTickDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id9,
                2,
                1,
                new[] { CharacterTestAuthoredContentIds.D410_Id15, CharacterTestAuthoredContentIds.D410_Id16, CharacterTestAuthoredContentIds.D410_Id14, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id17 },
                CharacterTestAuthoredContentIds.D411_Id7);
        }

        private static CombatHitDamageConfig CreateLaserLanceDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id9,
                8,
                4,
                new[] { CharacterTestAuthoredContentIds.D410_Id16, CharacterTestAuthoredContentIds.D410_Id14, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id18, CharacterTestAuthoredContentIds.D410_Id6 },
                CharacterTestAuthoredContentIds.D411_Id8);
        }

        private static CombatHitDamageConfig CreatePoisonBottleShatterDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id10,
                0,
                0,
                new[] { CharacterTestAuthoredContentIds.D410_Id21, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id22, CharacterTestAuthoredContentIds.D410_Id23, CharacterTestAuthoredContentIds.D410_Id24 },
                CharacterTestAuthoredContentIds.D411_Id10);
        }

        private static CombatHitDamageConfig CreateStunBoltDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id11,
                0,
                0,
                new[] { CharacterTestAuthoredContentIds.D410_Id25, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id18, CharacterTestAuthoredContentIds.D410_Id26, CharacterTestAuthoredContentIds.D410_Id27 },
                CharacterTestAuthoredContentIds.D411_Id11);
        }

        private static CombatHitDamageConfig CreateMeteorBarrageDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id3,
                8,
                12,
                new[] { CharacterTestAuthoredContentIds.D410_Id3, CharacterTestAuthoredContentIds.D410_Id8, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id28, CharacterTestAuthoredContentIds.D410_Id29, CharacterTestAuthoredContentIds.D410_Id6 },
                CharacterTestAuthoredContentIds.D411_Id12);
        }

        private static CombatHitDamageConfig CreateSwordWaveDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id8,
                9,
                SwordWaveImpactPower,
                new[] { CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id30, CharacterTestAuthoredContentIds.D410_Id10, CharacterTestAuthoredContentIds.D410_Id31, CharacterTestAuthoredContentIds.D410_Id32, CharacterTestAuthoredContentIds.D410_Id12 },
                CharacterTestAuthoredContentIds.D411_Id13);
        }

        private static CombatHitDamageConfig CreateHookChainDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id11,
                1,
                HookChainImpactPower,
                new[] { CharacterTestAuthoredContentIds.D410_Id19, CharacterTestAuthoredContentIds.D410_Id25, CharacterTestAuthoredContentIds.D410_Id37, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id38, CharacterTestAuthoredContentIds.D410_Id18 },
                CharacterTestAuthoredContentIds.D411_Id15);
        }

        private static CombatHitDamageConfig CreateArcMineCarrierDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id12,
                0,
                0,
                new[] { CharacterTestAuthoredContentIds.D410_Id39, CharacterTestAuthoredContentIds.D410_Id44, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id45, CharacterTestAuthoredContentIds.D410_Id40 },
                CharacterTestAuthoredContentIds.D411_Id19);
        }

        private static CombatHitDamageConfig CreateArcMineDropDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id12,
                0,
                0,
                new[] { CharacterTestAuthoredContentIds.D410_Id39, CharacterTestAuthoredContentIds.D410_Id46, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id45, CharacterTestAuthoredContentIds.D410_Id40 },
                CharacterTestAuthoredContentIds.D411_Id20);
        }

        private static CombatHitDamageConfig CreateRuneTurretDeployDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id12,
                0,
                0,
                new[] { CharacterTestAuthoredContentIds.D410_Id44, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id42, CharacterTestAuthoredContentIds.D410_Id43 },
                CharacterTestAuthoredContentIds.D411_Id21);
        }

        private static CombatHitDamageConfig CreateRuneTurretShotDamage()
        {
            return new CombatHitDamageConfig(
                CharacterTestAuthoredContentIds.D409_Id9,
                4,
                RuneTurretShotImpactPower,
                new[] { CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id41, CharacterTestAuthoredContentIds.D410_Id42, CharacterTestAuthoredContentIds.D410_Id18, CharacterTestAuthoredContentIds.D410_Id43 },
                CharacterTestAuthoredContentIds.D411_Id17);
        }

        public static CombatProjectileDefinitionRegistry CreateRegistry()
        {
            CombatProjectileDefinitionRegistry registry = LoadGeneratedOrEmpty(DefaultGeneratedDefinitionsPath);
            return registry.Count > 0 ? registry : CreateEmbeddedFallbackRegistry();
        }

        public static CombatProjectileSpawnProfileRegistry CreateSpawnProfileRegistry()
        {
            CombatProjectileSpawnProfileRegistry registry =
                LoadGeneratedSpawnProfilesOrEmpty(DefaultGeneratedSpawnProfilesPath);
            return registry.Count > 0 ? registry : CreateEmbeddedFallbackSpawnProfileRegistry();
        }

        public static CombatProjectileDefinitionRegistry LoadGeneratedOrEmpty(string path)
        {
            return CharacterTestRuntimeCatalogBinaryFallback.LoadBinaryThenJsonOrEmpty(
                path,
                CombatProjectileConfigBinaryImporter.CatalogKind,
                CombatProjectileConfigBinaryImporter.ImportFile,
                CombatProjectileConfigJsonImporter.ImportFile,
                registry => registry?.Count ?? 0,
                () => new CombatProjectileDefinitionRegistry());
        }

        public static CombatProjectileSpawnProfileRegistry LoadGeneratedSpawnProfilesOrEmpty(string path)
        {
            return CharacterTestRuntimeCatalogBinaryFallback.LoadBinaryThenJsonOrEmpty(
                path,
                CombatProjectileSpawnProfileConfigBinaryImporter.CatalogKind,
                CombatProjectileSpawnProfileConfigBinaryImporter.ImportFile,
                CombatProjectileSpawnProfileConfigJsonImporter.ImportFile,
                registry => registry?.Count ?? 0,
                () => new CombatProjectileSpawnProfileRegistry());
        }

        public static CombatProjectileDefinitionRegistry CreateEmbeddedFallbackRegistry()
        {
            var registry = new CombatProjectileDefinitionRegistry(new[]
            {
                new CombatProjectileDefinition(
                    DefinitionId,
                    StableId,
                    new CombatProjectileMotionConfig(
                        Fix64.FromRatio(35, 100),
                        lifetimeFrames: 90),
                    new CombatProjectileCollisionConfig(
                        Fix64.FromRatio(25, 100),
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true),
                    Impact),
                new CombatProjectileDefinition(
                    BurstDefinitionId,
                    BurstStableId,
                    new CombatProjectileMotionConfig(
                        Fix64.FromRatio(35, 100),
                        lifetimeFrames: 90),
                    new CombatProjectileCollisionConfig(
                        Fix64.FromRatio(25, 100),
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true),
                    BurstImpact),
                new CombatProjectileDefinition(
                    FlameLanceDefinitionId,
                    FlameLanceStableId,
                    new CombatProjectileMotionConfig(
                        Fix64.FromRatio(55, 100),
                        lifetimeFrames: 80),
                    new CombatProjectileCollisionConfig(
                        Fix64.FromRatio(15, 100),
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: false,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 3,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    FlameLanceImpact),
                new CombatProjectileDefinition(
                    FireballLobDefinitionId,
                    FireballLobStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.ArcToTarget,
                        Fix64.FromRatio(35, 100),
                        lifetimeFrames: 90,
                        arcHeight: Fix64.FromInt(4)),
                    new CombatProjectileCollisionConfig(
                        Fix64.FromRatio(25, 100),
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true),
                    FireballLobImpact),
                new CombatProjectileDefinition(
                    FireballSurgeDefinitionId,
                    FireballSurgeStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.Linear,
                        Fix64.FromRatio(35, 100),
                        lifetimeFrames: 90,
                        arcHeight: Fix64.Zero,
                        speedProfileKind: CombatProjectileSpeedProfileKind.EaseInQuadratic),
                    new CombatProjectileCollisionConfig(
                        Fix64.FromRatio(25, 100),
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true),
                    FireballSurgeImpact),
                new CombatProjectileDefinition(
                    FireballReturnDefinitionId,
                    FireballReturnStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.BoomerangReturn,
                        Fix64.FromRatio(35, 100),
                        lifetimeFrames: 90,
                        arcHeight: Fix64.Zero,
                        speedProfileKind: CombatProjectileSpeedProfileKind.Constant),
                    new CombatProjectileCollisionConfig(
                        Fix64.FromRatio(25, 100),
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: false,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 2,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    FireballReturnImpact),
                new CombatProjectileDefinition(
                    FireballSwayDefinitionId,
                    FireballSwayStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.SwayForward,
                        Fix64.FromRatio(35, 100),
                        lifetimeFrames: 90,
                        arcHeight: Fix64.Zero,
                        speedProfileKind: CombatProjectileSpeedProfileKind.Constant,
                        lateralAmplitude: Fix64.FromRatio(75, 100),
                        lateralCycles: 2),
                    new CombatProjectileCollisionConfig(
                        Fix64.FromRatio(25, 100),
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true),
                    FireballSwayImpact),
                new CombatProjectileDefinition(
                    FireballHomingDefinitionId,
                    FireballHomingStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.HomingToTarget,
                        Fix64.FromRatio(35, 100),
                        lifetimeFrames: 90,
                        arcHeight: Fix64.Zero,
                        speedProfileKind: CombatProjectileSpeedProfileKind.Constant,
                        lateralAmplitude: Fix64.Zero,
                        lateralCycles: 0,
                        homingTurnRatePerFrame: Fix64.FromRatio(15, 100)),
                    new CombatProjectileCollisionConfig(
                        Fix64.FromRatio(25, 100),
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true),
                    FireballHomingImpact),
                new CombatProjectileDefinition(
                    FireballBezierDefinitionId,
                    FireballBezierStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.BezierToTarget,
                        Fix64.FromRatio(35, 100),
                        lifetimeFrames: 90,
                        arcHeight: Fix64.Zero,
                        speedProfileKind: CombatProjectileSpeedProfileKind.Constant,
                        lateralAmplitude: Fix64.Zero,
                        lateralCycles: 0,
                        bezierControlOffsetA: new FixVector3(Fix64.Zero, Fix64.FromInt(3), Fix64.FromInt(6)),
                        bezierControlOffsetB: new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero - Fix64.FromInt(4))),
                    new CombatProjectileCollisionConfig(
                        Fix64.FromRatio(25, 100),
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true),
                    FireballBezierImpact),
                new CombatProjectileDefinition(
                    FireballExplosionDefinitionId,
                    FireballExplosionStableId,
                    new CombatProjectileMotionConfig(
                        Fix64.FromRatio(35, 100),
                        lifetimeFrames: 90),
                    new CombatProjectileCollisionConfig(
                        Fix64.FromRatio(25, 100),
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true),
                    FireballExplosionImpact,
                    physicalEventsEnabled: true),
                new CombatProjectileDefinition(
                    TornadoDefinitionId,
                    TornadoStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.SwayForward,
                        TornadoSpeedPerFrame,
                        lifetimeFrames: TornadoLifetimeFrames,
                        arcHeight: Fix64.Zero,
                        speedProfileKind: CombatProjectileSpeedProfileKind.Constant,
                        lateralAmplitude: TornadoLateralAmplitude,
                        lateralCycles: TornadoLateralCycles),
                    new CombatProjectileCollisionConfig(
                        CharacterTestInteractionFieldProfiles.TornadoAttractionRadius,
                        CombatPhysicsLayerMask.None,
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true),
                    TornadoImpact,
                    physicalEventsEnabled: true),
                new CombatProjectileDefinition(
                    BeamLanceDefinitionId,
                    BeamLanceStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.Linear,
                        Fix64.FromInt(44),
                        lifetimeFrames: 1),
                    new CombatProjectileCollisionConfig(
                        Fix64.FromRatio(20, 100),
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: false,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 5,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    BeamLanceImpact),
                new CombatProjectileDefinition(
                    ChannelBeamDefinitionId,
                    ChannelBeamStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.Linear,
                        ChannelBeamRange,
                        lifetimeFrames: 1),
                    new CombatProjectileCollisionConfig(
                        ChannelBeamRadius,
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: false,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 3,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    ChannelBeamTickImpact),
                new CombatProjectileDefinition(
                    LaserLanceDefinitionId,
                    LaserLanceStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.Linear,
                        LaserLanceRange,
                        lifetimeFrames: 1),
                    new CombatProjectileCollisionConfig(
                        LaserLanceRadius,
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 1,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    LaserLanceImpact),
                new CombatProjectileDefinition(
                    PoisonBottleDefinitionId,
                    PoisonBottleStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.BallisticArcToTarget,
                        PoisonBottleSpeedPerFrame,
                        lifetimeFrames: PoisonBottleLifetimeFrames,
                        arcHeight: Fix64.Zero,
                        speedProfileKind: CombatProjectileSpeedProfileKind.Constant,
                        lateralAmplitude: Fix64.Zero,
                        lateralCycles: 0,
                        gravityPerFrameSquared: PoisonBottleGravityPerFrameSquared,
                        maxLaunchAngleTangent: Fix64.Zero),
                    new CombatProjectileCollisionConfig(
                        PoisonBottleRadius,
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 1,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    PoisonBottleImpact),
                new CombatProjectileDefinition(
                    StunBoltDefinitionId,
                    StunBoltStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.Linear,
                        StunBoltSpeedPerFrame,
                        lifetimeFrames: StunBoltLifetimeFrames),
                    new CombatProjectileCollisionConfig(
                        StunBoltRadius,
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 1,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    StunBoltImpact),
                new CombatProjectileDefinition(
                    MeteorBarrageDefinitionId,
                    MeteorBarrageStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.Linear,
                        MeteorBarrageSpeedPerFrame,
                        lifetimeFrames: MeteorBarrageLifetimeFrames),
                    new CombatProjectileCollisionConfig(
                        MeteorBarrageRadius,
                        MeteorBarrageCollisionLayerMask,
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 1,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    MeteorBarrageImpact,
                    physicalEventsEnabled: true),
                new CombatProjectileDefinition(
                    SwordWaveDefinitionId,
                    SwordWaveStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.Linear,
                        SwordWaveSpeedPerFrame,
                        lifetimeFrames: SwordWaveLifetimeFrames),
                    new CombatProjectileCollisionConfig(
                        SwordWaveRadius,
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer)
                            .AddLayer(MeteorBarrageStaticEnvironmentLayer),
                        stopOnFirstAcceptedImpact: false,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 4,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    SwordWaveImpact,
                    physicalEventsEnabled: true),
                new CombatProjectileDefinition(
                    HookChainDefinitionId,
                    HookChainStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.Linear,
                        HookChainSpeedPerFrame,
                        lifetimeFrames: HookChainLifetimeFrames),
                    new CombatProjectileCollisionConfig(
                        HookChainRadius,
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer)
                            .AddLayer(MeteorBarrageStaticEnvironmentLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 1,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    HookChainImpact),
                new CombatProjectileDefinition(
                    ArcMineDefinitionId,
                    ArcMineStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.BallisticArcToTarget,
                        ArcMineSpeedPerFrame,
                        lifetimeFrames: ArcMineLifetimeFrames,
                        arcHeight: Fix64.Zero,
                        speedProfileKind: CombatProjectileSpeedProfileKind.Constant,
                        lateralAmplitude: Fix64.Zero,
                        lateralCycles: 0,
                        gravityPerFrameSquared: ArcMineGravityPerFrameSquared,
                        maxLaunchAngleTangent: Fix64.Zero),
                    new CombatProjectileCollisionConfig(
                        ArcMineRadius,
                        CombatPhysicsLayerMask.FromLayer(MeteorBarrageStaticEnvironmentLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 1,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    ArcMineImpact),
                new CombatProjectileDefinition(
                    ArcMineDropDefinitionId,
                    ArcMineDropStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.BallisticArcToTarget,
                        ArcMineSpeedPerFrame,
                        lifetimeFrames: ArcMineLifetimeFrames,
                        arcHeight: Fix64.Zero,
                        speedProfileKind: CombatProjectileSpeedProfileKind.Constant,
                        lateralAmplitude: Fix64.Zero,
                        lateralCycles: 0,
                        gravityPerFrameSquared: ArcMineGravityPerFrameSquared,
                        maxLaunchAngleTangent: Fix64.Zero),
                    new CombatProjectileCollisionConfig(
                        ArcMineRadius,
                        CombatPhysicsLayerMask.FromLayer(MeteorBarrageStaticEnvironmentLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 1,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    ArcMineDropImpact),
                new CombatProjectileDefinition(
                    RuneTurretDeployDefinitionId,
                    RuneTurretDeployStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.BallisticArcToTarget,
                        RuneTurretDeploySpeedPerFrame,
                        lifetimeFrames: RuneTurretDeployLifetimeFrames,
                        arcHeight: Fix64.Zero,
                        speedProfileKind: CombatProjectileSpeedProfileKind.Constant,
                        lateralAmplitude: Fix64.Zero,
                        lateralCycles: 0,
                        gravityPerFrameSquared: RuneTurretDeployGravityPerFrameSquared,
                        maxLaunchAngleTangent: Fix64.Zero),
                    new CombatProjectileCollisionConfig(
                        RuneTurretDeployRadius,
                        CombatPhysicsLayerMask.FromLayer(MeteorBarrageStaticEnvironmentLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 1,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    RuneTurretDeployImpact),
                new CombatProjectileDefinition(
                    RuneTurretShotDefinitionId,
                    RuneTurretShotStableId,
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.Linear,
                        RuneTurretShotSpeedPerFrame,
                        lifetimeFrames: RuneTurretShotLifetimeFrames),
                    new CombatProjectileCollisionConfig(
                        RuneTurretShotRadius,
                        CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer)
                            .AddLayer(MeteorBarrageStaticEnvironmentLayer),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 1,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                    RuneTurretShotImpact)
            });
            CharacterTestRuntimeCatalogLoadDiagnostics.RecordEmbedded(
                CombatProjectileConfigBinaryImporter.CatalogKind,
                registry.Count,
                "Generated projectile catalog was empty or missing.");
            return registry;
        }

        public static CombatProjectileSpawnProfileRegistry CreateEmbeddedFallbackSpawnProfileRegistry()
        {
            var registry = new CombatProjectileSpawnProfileRegistry(new[]
            {
                CreateSocketSpawnProfile(
                    RequestId,
                    StableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange),
                CreateSocketSpawnProfile(BurstRequestId, BurstStableId),
                CreateSocketSpawnProfile(
                    FlameLanceRequestId,
                    FlameLanceStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    Fix64.FromInt(44)),
                CreateSocketSpawnProfile(
                    FireballLobRequestId,
                    FireballLobStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange),
                CreateSocketSpawnProfile(FireballSurgeRequestId, FireballSurgeStableId),
                CreateSocketSpawnProfile(FireballReturnRequestId, FireballReturnStableId),
                CreateSocketSpawnProfile(FireballSwayRequestId, FireballSwayStableId),
                CreateSocketSpawnProfile(
                    FireballHomingRequestId,
                    FireballHomingStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange),
                CreateSocketSpawnProfile(FireballBezierRequestId, FireballBezierStableId),
                CreateSocketSpawnProfile(FireballExplosionRequestId, FireballExplosionStableId),
                CreateSocketSpawnProfile(TornadoRequestId, TornadoStableId),
                CreateSocketSpawnProfile(
                    BeamLanceRequestId,
                    BeamLanceStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    Fix64.FromInt(44)),
                CreateSocketSpawnProfile(
                    ChannelBeamRequestId,
                    ChannelBeamStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    ChannelBeamRange),
                CreateSocketSpawnProfile(
                    LaserLanceRequestId,
                    LaserLanceStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    LaserLanceRange),
                CreateSocketSpawnProfile(
                    PoisonBottleRequestId,
                    PoisonBottleStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    PoisonBottleRange),
                CreateSocketSpawnProfile(
                    StunBoltRequestId,
                    StunBoltStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    StunBoltRange),
                CreateSocketSpawnProfile(
                    MeteorBarrageRequestId0,
                    MeteorBarrageStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    MeteorBarrageRange),
                CreateSocketSpawnProfile(
                    MeteorBarrageRequestId1,
                    MeteorBarrageStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    MeteorBarrageRange),
                CreateSocketSpawnProfile(
                    MeteorBarrageRequestId2,
                    MeteorBarrageStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    MeteorBarrageRange),
                CreateSocketSpawnProfile(
                    MeteorBarrageRequestId3,
                    MeteorBarrageStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    MeteorBarrageRange),
                CreateSocketSpawnProfile(
                    MeteorBarrageRequestId4,
                    MeteorBarrageStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    MeteorBarrageRange),
                CreateSocketSpawnProfile(
                    MeteorBarrageRequestId5,
                    MeteorBarrageStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    MeteorBarrageRange),
                CreateSocketSpawnProfile(
                    SwordWaveRequestId,
                    SwordWaveStableId,
                    CombatProjectileAimKind.CasterForwardRange,
                    SwordWaveRange),
                CreateSocketSpawnProfile(
                    HookChainRequestId,
                    HookChainStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    HookChainRange),
                CreateSocketSpawnProfile(
                    ArcMineRequestId,
                    ArcMineStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    ArcMineRange),
                CreateSocketSpawnProfile(
                    ArcMineDropRequestId,
                    ArcMineDropStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    ArcMineRange),
                CreateSocketSpawnProfile(
                    RuneTurretDeployRequestId,
                    RuneTurretDeployStableId,
                    CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                    RuneTurretDeployRange),
                CreateSocketSpawnProfile(
                    RuneTurretShotRequestId,
                    RuneTurretShotStableId,
                    CombatProjectileAimKind.CasterForwardRange,
                    RuneTurretShotRange)
            });
            CharacterTestRuntimeCatalogLoadDiagnostics.RecordEmbedded(
                CombatProjectileSpawnProfileConfigBinaryImporter.CatalogKind,
                registry.Count,
                "Generated projectile spawn profile catalog was empty or missing.");
            return registry;
        }

        #if false
        public static bool TryResolveDefinition(
            CombatProjectileDefinitionRegistry registry,
            string requestId,
            out CombatProjectileDefinition definition)
        {
            if (registry == null)
            {
                definition = null;
                return false;
            }

            if (IsMeteorBarrageRequestId(requestId, out _))
                return registry.TryGetByStableId(MeteorBarrageStableId, out definition);
            if (string.Equals(requestId, SwordWaveRequestId, StringComparison.Ordinal))
                return registry.TryGetByStableId(SwordWaveStableId, out definition);
            if (string.Equals(requestId, ArcMineRequestId, StringComparison.Ordinal))
                return registry.TryGetByStableId(ArcMineStableId, out definition);
            if (string.Equals(requestId, RuneTurretDeployRequestId, StringComparison.Ordinal))
                return registry.TryGetByStableId(RuneTurretDeployStableId, out definition);

            return registry.TryGetByStableId(requestId, out definition);
        }

        public static CharacterActionId ResolveCharacterActionId(CombatProjectileDefinition definition)
        {
            if (definition != null
                && string.Equals(definition.StableId, BurstStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.FireballBurstActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FlameLanceStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.FlameLanceActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballLobStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.FireballLobActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballSurgeStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.FireballSurgeActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballReturnStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.FireballReturnActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballSwayStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.FireballSwayActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballHomingStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.FireballHomingActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballBezierStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.FireballBezierActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballExplosionStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.FireballExplosionActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, TornadoStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.TornadoActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, BeamLanceStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.BeamLanceActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, ChannelBeamStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.ChannelBeamActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, LaserLanceStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.LaserLanceActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, PoisonDartStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.PoisonDartActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, StunBoltStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.StunBoltActionId;
            }

            if (IsMeteorBarrageProjectile(definition))
                return CharacterTestCharacterActionConfigSource.MeteorBarrageActionId;

            if (definition != null
                && string.Equals(definition.StableId, SwordWaveStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.SwordWaveActionId;
            }

            if (definition != null
                && string.Equals(definition.StableId, HookChainStableId, StringComparison.Ordinal))
            {
                return CharacterTestCharacterActionConfigSource.HookChainActionId;
            }

            if (definition != null
                && (string.Equals(definition.StableId, ArcMineStableId, StringComparison.Ordinal)
                    || string.Equals(definition.StableId, ArcMineDropStableId, StringComparison.Ordinal)))
            {
                return CharacterTestCharacterActionConfigSource.ArcMineActionId;
            }

            if (definition != null
                && (string.Equals(definition.StableId, RuneTurretDeployStableId, StringComparison.Ordinal)
                    || string.Equals(definition.StableId, RuneTurretShotStableId, StringComparison.Ordinal)))
            {
                return CharacterTestCharacterActionConfigSource.RuneTurretActionId;
            }

            return CharacterTestCharacterActionConfigSource.FireballActionId;
        }

        public static int ResolveAbilityId(CombatProjectileDefinition definition)
        {
            if (definition != null
                && string.Equals(definition.StableId, BurstStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.FireballBurstAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FlameLanceStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.FlameLanceAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballLobStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.FireballLobAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballSurgeStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.FireballSurgeAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballReturnStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.FireballReturnAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballSwayStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.FireballSwayAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballHomingStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.FireballHomingAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballBezierStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.FireballBezierAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, FireballExplosionStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.FireballExplosionAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, TornadoStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.TornadoAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, BeamLanceStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.BeamLanceAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, ChannelBeamStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.ChannelBeamAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, LaserLanceStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.LaserLanceAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, PoisonDartStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.PoisonDartAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, StunBoltStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.StunBoltAbilityId;
            }

            if (IsMeteorBarrageProjectile(definition))
                return CharacterTestAbilityIds.MeteorBarrageAbilityId;

            if (definition != null
                && string.Equals(definition.StableId, SwordWaveStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.SwordWaveAbilityId;
            }

            if (definition != null
                && string.Equals(definition.StableId, HookChainStableId, StringComparison.Ordinal))
            {
                return CharacterTestAbilityIds.HookChainAbilityId;
            }

            if (definition != null
                && (string.Equals(definition.StableId, ArcMineStableId, StringComparison.Ordinal)
                    || string.Equals(definition.StableId, ArcMineDropStableId, StringComparison.Ordinal)))
            {
                return CharacterTestAbilityIds.ArcMineAbilityId;
            }

            if (definition != null
                && (string.Equals(definition.StableId, RuneTurretDeployStableId, StringComparison.Ordinal)
                    || string.Equals(definition.StableId, RuneTurretShotStableId, StringComparison.Ordinal)))
            {
                return CharacterTestAbilityIds.RuneTurretAbilityId;
            }

            return CharacterTestAbilityIds.FireballAbilityId;
        }

        public static bool IsPoisonBottleProjectile(CombatProjectileDefinition definition)
        {
            return definition != null
                && definition.DefinitionId == PoisonBottleDefinitionId
                && string.Equals(definition.StableId, PoisonBottleStableId, StringComparison.Ordinal);
        }

        public static bool IsPoisonBottleProjectile(CombatProjectileSnapshot projectile)
        {
            return projectile.DefinitionId == PoisonBottleDefinitionId
                && string.Equals(projectile.DefinitionStableId, PoisonBottleStableId, StringComparison.Ordinal);
        }

        public static bool IsMeteorBarrageProjectile(CombatProjectileDefinition definition)
        {
            return definition != null
                && definition.DefinitionId == MeteorBarrageDefinitionId
                && string.Equals(definition.StableId, MeteorBarrageStableId, StringComparison.Ordinal);
        }

        public static bool IsMeteorBarrageProjectile(CombatProjectileSnapshot projectile)
        {
            return projectile.DefinitionId == MeteorBarrageDefinitionId
                && string.Equals(projectile.DefinitionStableId, MeteorBarrageStableId, StringComparison.Ordinal);
        }

        public static bool IsHookChainProjectile(CombatProjectileDefinition definition)
        {
            return definition != null
                && definition.DefinitionId == HookChainDefinitionId
                && string.Equals(definition.StableId, HookChainStableId, StringComparison.Ordinal);
        }

        public static bool IsHookChainProjectile(CombatProjectileSnapshot projectile)
        {
            return projectile.DefinitionId == HookChainDefinitionId
                && string.Equals(projectile.DefinitionStableId, HookChainStableId, StringComparison.Ordinal);
        }

        public static bool IsArcMineCarrierProjectile(CombatProjectileDefinition definition)
        {
            return definition != null
                && definition.DefinitionId == ArcMineDefinitionId
                && string.Equals(definition.StableId, ArcMineStableId, StringComparison.Ordinal);
        }

        public static bool IsArcMineCarrierProjectile(CombatProjectileSnapshot projectile)
        {
            return projectile.DefinitionId == ArcMineDefinitionId
                && string.Equals(projectile.DefinitionStableId, ArcMineStableId, StringComparison.Ordinal);
        }

        public static bool IsArcMineDropProjectile(CombatProjectileDefinition definition)
        {
            return definition != null
                && definition.DefinitionId == ArcMineDropDefinitionId
                && string.Equals(definition.StableId, ArcMineDropStableId, StringComparison.Ordinal);
        }

        public static bool IsArcMineDropProjectile(CombatProjectileSnapshot projectile)
        {
            return projectile.DefinitionId == ArcMineDropDefinitionId
                && string.Equals(projectile.DefinitionStableId, ArcMineDropStableId, StringComparison.Ordinal);
        }

        public static bool IsArcMineProjectile(CombatProjectileDefinition definition)
        {
            return IsArcMineCarrierProjectile(definition) || IsArcMineDropProjectile(definition);
        }

        public static bool IsArcMineProjectile(CombatProjectileSnapshot projectile)
        {
            return IsArcMineCarrierProjectile(projectile) || IsArcMineDropProjectile(projectile);
        }

        public static bool IsRuneTurretDeployProjectile(CombatProjectileDefinition definition)
        {
            return definition != null
                && definition.DefinitionId == RuneTurretDeployDefinitionId
                && string.Equals(definition.StableId, RuneTurretDeployStableId, StringComparison.Ordinal);
        }

        public static bool IsRuneTurretDeployProjectile(CombatProjectileSnapshot projectile)
        {
            return projectile.DefinitionId == RuneTurretDeployDefinitionId
                && string.Equals(projectile.DefinitionStableId, RuneTurretDeployStableId, StringComparison.Ordinal);
        }

        public static bool IsRuneTurretShotProjectile(CombatProjectileDefinition definition)
        {
            return definition != null
                && definition.DefinitionId == RuneTurretShotDefinitionId
                && string.Equals(definition.StableId, RuneTurretShotStableId, StringComparison.Ordinal);
        }

        public static bool IsRuneTurretShotProjectile(CombatProjectileSnapshot projectile)
        {
            return projectile.DefinitionId == RuneTurretShotDefinitionId
                && string.Equals(projectile.DefinitionStableId, RuneTurretShotStableId, StringComparison.Ordinal);
        }

        public static bool IsMeteorBarrageRequestId(string requestId, out int meteorIndex)
        {
            if (string.Equals(requestId, MeteorBarrageRequestId0, StringComparison.Ordinal))
            {
                meteorIndex = 0;
                return true;
            }

            if (string.Equals(requestId, MeteorBarrageRequestId1, StringComparison.Ordinal))
            {
                meteorIndex = 1;
                return true;
            }

            if (string.Equals(requestId, MeteorBarrageRequestId2, StringComparison.Ordinal))
            {
                meteorIndex = 2;
                return true;
            }

            if (string.Equals(requestId, MeteorBarrageRequestId3, StringComparison.Ordinal))
            {
                meteorIndex = 3;
                return true;
            }

            if (string.Equals(requestId, MeteorBarrageRequestId4, StringComparison.Ordinal))
            {
                meteorIndex = 4;
                return true;
            }

            if (string.Equals(requestId, MeteorBarrageRequestId5, StringComparison.Ordinal))
            {
                meteorIndex = 5;
                return true;
            }

            meteorIndex = -1;
            return false;
        }

        #endif

        public static bool TryResolveDefinition(
            CombatProjectileDefinitionRegistry registry,
            int requestId,
            out CombatProjectileDefinition definition)
        {
            definition = null;
            if (registry == null)
                return false;

            int definitionId;
            switch (requestId)
            {
                case RequestId: definitionId = DefinitionId; break;
                case BurstRequestId: definitionId = BurstDefinitionId; break;
                case FlameLanceRequestId: definitionId = FlameLanceDefinitionId; break;
                case FireballLobRequestId: definitionId = FireballLobDefinitionId; break;
                case FireballSurgeRequestId: definitionId = FireballSurgeDefinitionId; break;
                case FireballReturnRequestId: definitionId = FireballReturnDefinitionId; break;
                case FireballSwayRequestId: definitionId = FireballSwayDefinitionId; break;
                case FireballHomingRequestId: definitionId = FireballHomingDefinitionId; break;
                case FireballBezierRequestId: definitionId = FireballBezierDefinitionId; break;
                case FireballExplosionRequestId: definitionId = FireballExplosionDefinitionId; break;
                case TornadoRequestId: definitionId = TornadoDefinitionId; break;
                case BeamLanceRequestId: definitionId = BeamLanceDefinitionId; break;
                case ChannelBeamRequestId: definitionId = ChannelBeamDefinitionId; break;
                case LaserLanceRequestId: definitionId = LaserLanceDefinitionId; break;
                case PoisonBottleRequestId: definitionId = PoisonBottleDefinitionId; break;
                case StunBoltRequestId: definitionId = StunBoltDefinitionId; break;
                case MeteorBarrageRequestId0:
                case MeteorBarrageRequestId1:
                case MeteorBarrageRequestId2:
                case MeteorBarrageRequestId3:
                case MeteorBarrageRequestId4:
                case MeteorBarrageRequestId5: definitionId = MeteorBarrageDefinitionId; break;
                case SwordWaveRequestId: definitionId = SwordWaveDefinitionId; break;
                case HookChainRequestId: definitionId = HookChainDefinitionId; break;
                case ArcMineRequestId: definitionId = ArcMineDefinitionId; break;
                case ArcMineDropRequestId: definitionId = ArcMineDropDefinitionId; break;
                case RuneTurretDeployRequestId: definitionId = RuneTurretDeployDefinitionId; break;
                case RuneTurretShotRequestId: definitionId = RuneTurretShotDefinitionId; break;
                default: return false;
            }
            return registry.TryGet(definitionId, out definition);
        }

        public static bool TryResolveSpawnProfile(
            CombatProjectileSpawnProfileRegistry registry,
            int requestId,
            out CombatProjectileSpawnProfile profile)
        {
            profile = null;
            return registry != null && registry.TryGet(requestId, out profile);
        }

        public static CharacterActionId ResolveCharacterActionId(CombatProjectileDefinition definition)
        {
            if (definition == null)
                return CharacterTestCharacterActionConfigSource.FireballActionId;
            switch (definition.DefinitionId)
            {
                case BurstDefinitionId: return CharacterTestCharacterActionConfigSource.FireballBurstActionId;
                case FlameLanceDefinitionId: return CharacterTestCharacterActionConfigSource.FlameLanceActionId;
                case FireballLobDefinitionId: return CharacterTestCharacterActionConfigSource.FireballLobActionId;
                case FireballSurgeDefinitionId: return CharacterTestCharacterActionConfigSource.FireballSurgeActionId;
                case FireballReturnDefinitionId: return CharacterTestCharacterActionConfigSource.FireballReturnActionId;
                case FireballSwayDefinitionId: return CharacterTestCharacterActionConfigSource.FireballSwayActionId;
                case FireballHomingDefinitionId: return CharacterTestCharacterActionConfigSource.FireballHomingActionId;
                case FireballBezierDefinitionId: return CharacterTestCharacterActionConfigSource.FireballBezierActionId;
                case FireballExplosionDefinitionId: return CharacterTestCharacterActionConfigSource.FireballExplosionActionId;
                case TornadoDefinitionId: return CharacterTestCharacterActionConfigSource.TornadoActionId;
                case BeamLanceDefinitionId: return CharacterTestCharacterActionConfigSource.BeamLanceActionId;
                case ChannelBeamDefinitionId: return CharacterTestCharacterActionConfigSource.ChannelBeamActionId;
                case LaserLanceDefinitionId: return CharacterTestCharacterActionConfigSource.LaserLanceActionId;
                case PoisonBottleDefinitionId: return CharacterTestCharacterActionConfigSource.PoisonDartActionId;
                case StunBoltDefinitionId: return CharacterTestCharacterActionConfigSource.StunBoltActionId;
                case MeteorBarrageDefinitionId: return CharacterTestCharacterActionConfigSource.MeteorBarrageActionId;
                case SwordWaveDefinitionId: return CharacterTestCharacterActionConfigSource.SwordWaveActionId;
                case HookChainDefinitionId: return CharacterTestCharacterActionConfigSource.HookChainActionId;
                case ArcMineDefinitionId:
                case ArcMineDropDefinitionId: return CharacterTestCharacterActionConfigSource.ArcMineActionId;
                case RuneTurretDeployDefinitionId:
                case RuneTurretShotDefinitionId: return CharacterTestCharacterActionConfigSource.RuneTurretActionId;
                default: return CharacterTestCharacterActionConfigSource.FireballActionId;
            }
        }

        public static int ResolveAbilityId(CombatProjectileDefinition definition)
        {
            if (definition == null)
                return CharacterTestAbilityIds.FireballAbilityId;
            switch (definition.DefinitionId)
            {
                case BurstDefinitionId: return CharacterTestAbilityIds.FireballBurstAbilityId;
                case FlameLanceDefinitionId: return CharacterTestAbilityIds.FlameLanceAbilityId;
                case FireballLobDefinitionId: return CharacterTestAbilityIds.FireballLobAbilityId;
                case FireballSurgeDefinitionId: return CharacterTestAbilityIds.FireballSurgeAbilityId;
                case FireballReturnDefinitionId: return CharacterTestAbilityIds.FireballReturnAbilityId;
                case FireballSwayDefinitionId: return CharacterTestAbilityIds.FireballSwayAbilityId;
                case FireballHomingDefinitionId: return CharacterTestAbilityIds.FireballHomingAbilityId;
                case FireballBezierDefinitionId: return CharacterTestAbilityIds.FireballBezierAbilityId;
                case FireballExplosionDefinitionId: return CharacterTestAbilityIds.FireballExplosionAbilityId;
                case TornadoDefinitionId: return CharacterTestAbilityIds.TornadoAbilityId;
                case BeamLanceDefinitionId: return CharacterTestAbilityIds.BeamLanceAbilityId;
                case ChannelBeamDefinitionId: return CharacterTestAbilityIds.ChannelBeamAbilityId;
                case LaserLanceDefinitionId: return CharacterTestAbilityIds.LaserLanceAbilityId;
                case PoisonBottleDefinitionId: return CharacterTestAbilityIds.PoisonDartAbilityId;
                case StunBoltDefinitionId: return CharacterTestAbilityIds.StunBoltAbilityId;
                case MeteorBarrageDefinitionId: return CharacterTestAbilityIds.MeteorBarrageAbilityId;
                case SwordWaveDefinitionId: return CharacterTestAbilityIds.SwordWaveAbilityId;
                case HookChainDefinitionId: return CharacterTestAbilityIds.HookChainAbilityId;
                case ArcMineDefinitionId:
                case ArcMineDropDefinitionId: return CharacterTestAbilityIds.ArcMineAbilityId;
                case RuneTurretDeployDefinitionId:
                case RuneTurretShotDefinitionId: return CharacterTestAbilityIds.RuneTurretAbilityId;
                default: return CharacterTestAbilityIds.FireballAbilityId;
            }
        }

        public static bool IsPoisonBottleProjectile(CombatProjectileDefinition definition) =>
            definition != null && definition.DefinitionId == PoisonBottleDefinitionId;
        public static bool IsPoisonBottleProjectile(CombatProjectileSnapshot projectile) =>
            projectile.DefinitionId == PoisonBottleDefinitionId;
        public static bool IsMeteorBarrageProjectile(CombatProjectileDefinition definition) =>
            definition != null && definition.DefinitionId == MeteorBarrageDefinitionId;
        public static bool IsMeteorBarrageProjectile(CombatProjectileSnapshot projectile) =>
            projectile.DefinitionId == MeteorBarrageDefinitionId;
        public static bool IsHookChainProjectile(CombatProjectileDefinition definition) =>
            definition != null && definition.DefinitionId == HookChainDefinitionId;
        public static bool IsHookChainProjectile(CombatProjectileSnapshot projectile) =>
            projectile.DefinitionId == HookChainDefinitionId;
        public static bool IsArcMineCarrierProjectile(CombatProjectileDefinition definition) =>
            definition != null && definition.DefinitionId == ArcMineDefinitionId;
        public static bool IsArcMineCarrierProjectile(CombatProjectileSnapshot projectile) =>
            projectile.DefinitionId == ArcMineDefinitionId;
        public static bool IsArcMineDropProjectile(CombatProjectileDefinition definition) =>
            definition != null && definition.DefinitionId == ArcMineDropDefinitionId;
        public static bool IsArcMineDropProjectile(CombatProjectileSnapshot projectile) =>
            projectile.DefinitionId == ArcMineDropDefinitionId;
        public static bool IsArcMineProjectile(CombatProjectileDefinition definition) =>
            IsArcMineCarrierProjectile(definition) || IsArcMineDropProjectile(definition);
        public static bool IsArcMineProjectile(CombatProjectileSnapshot projectile) =>
            IsArcMineCarrierProjectile(projectile) || IsArcMineDropProjectile(projectile);
        public static bool IsRuneTurretDeployProjectile(CombatProjectileDefinition definition) =>
            definition != null && definition.DefinitionId == RuneTurretDeployDefinitionId;
        public static bool IsRuneTurretDeployProjectile(CombatProjectileSnapshot projectile) =>
            projectile.DefinitionId == RuneTurretDeployDefinitionId;
        public static bool IsRuneTurretShotProjectile(CombatProjectileDefinition definition) =>
            definition != null && definition.DefinitionId == RuneTurretShotDefinitionId;
        public static bool IsRuneTurretShotProjectile(CombatProjectileSnapshot projectile) =>
            projectile.DefinitionId == RuneTurretShotDefinitionId;
        public static bool IsMeteorBarrageRequestId(int requestId, out int meteorIndex)
        {
            meteorIndex = requestId - MeteorBarrageRequestId0;
            return meteorIndex >= 0 && meteorIndex < MeteorBarrageProjectileCount;
        }

        public static void ResolveMeteorBarrageLocalOffset(
            int meteorIndex,
            out Fix64 rightOffset,
            out Fix64 forwardOffset)
        {
            switch (meteorIndex)
            {
                case 1:
                    rightOffset = Fix64.FromRatio(6, 5);
                    forwardOffset = Fix64.FromRatio(3, 5);
                    return;
                case 2:
                    rightOffset = Fix64.Zero - Fix64.FromRatio(11, 10);
                    forwardOffset = Fix64.FromRatio(7, 10);
                    return;
                case 3:
                    rightOffset = Fix64.FromRatio(1, 2);
                    forwardOffset = Fix64.Zero - Fix64.FromRatio(6, 5);
                    return;
                case 4:
                    rightOffset = Fix64.Zero - Fix64.FromRatio(4, 5);
                    forwardOffset = Fix64.Zero - Fix64.FromRatio(9, 10);
                    return;
                case 5:
                    rightOffset = Fix64.FromRatio(7, 5);
                    forwardOffset = Fix64.Zero - Fix64.FromRatio(3, 10);
                    return;
                default:
                    rightOffset = Fix64.Zero;
                    forwardOffset = Fix64.Zero;
                    return;
            }
        }

        public static float ResolveSwordWaveRollDegrees(int localFrame)
        {
            int firstFrame = CharacterTestCharacterActionConfigSource.SwordWaveProjectileFirstFrame;
            int interval = CharacterTestCharacterActionConfigSource.SwordWaveProjectileIntervalFrames;
            if (interval <= 0)
                return SwordWaveFirstRollDegrees;

            int delta = localFrame - firstFrame;
            if (delta == interval)
                return SwordWaveSecondRollDegrees;
            if (delta == interval * 2)
                return SwordWaveThirdRollDegrees;

            return SwordWaveFirstRollDegrees;
        }

        public static int ResolveSwordWaveRollMilliDegrees(int localFrame)
        {
            return CharacterTestDeterministicUnits.DegreesToMilliDegreesUnnormalized(
                ResolveSwordWaveRollDegrees(localFrame));
        }

        public static FixVector3 ResolveDirection(CharacterTestCharacterEntity source)
        {
            float yawDegrees = source != null ? source.Transform.YawDegrees : 0f;
            double radians = yawDegrees * Math.PI / 180.0;
            var direction = new FixVector3(
                ToFix64(Math.Sin(radians)),
                Fix64.Zero,
                ToFix64(Math.Cos(radians)));
            return direction.TryNormalize(out FixVector3 normalized) ? normalized : new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
        }

        private static Fix64 ToFix64(double value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static CombatProjectileSpawnProfile CreateSocketSpawnProfile(
            int requestId,
            string projectileStableId,
            CombatProjectileAimKind aimKind = CombatProjectileAimKind.CasterForwardRange,
            Fix64 maxRange = default)
        {
            return new CombatProjectileSpawnProfile(
                requestId,
                RequestLabelFor(requestId),
                DefinitionIdFor(requestId),
                projectileStableId,
                new CombatProjectileSpawnOriginConfig(
                    CombatProjectileSpawnOriginKind.Socket,
                    12,
                    FixVector3.Zero,
                    new FixVector3(
                        Fix64.Zero,
                        Fix64.FromRatio(115, 100),
                        Fix64.FromRatio(65, 100)),
                    "mainHand"),
                new CombatProjectileAimConfig(
                    aimKind,
                    Fix64.Zero,
                    CombatProjectileAimKind.CasterForwardRange,
                    maxRange));
        }

        private static int DefinitionIdFor(int requestId)
        {
            switch (requestId)
            {
                case RequestId: return DefinitionId;
                case BurstRequestId: return BurstDefinitionId;
                case FlameLanceRequestId: return FlameLanceDefinitionId;
                case FireballLobRequestId: return FireballLobDefinitionId;
                case FireballSurgeRequestId: return FireballSurgeDefinitionId;
                case FireballReturnRequestId: return FireballReturnDefinitionId;
                case FireballSwayRequestId: return FireballSwayDefinitionId;
                case FireballHomingRequestId: return FireballHomingDefinitionId;
                case FireballBezierRequestId: return FireballBezierDefinitionId;
                case FireballExplosionRequestId: return FireballExplosionDefinitionId;
                case TornadoRequestId: return TornadoDefinitionId;
                case BeamLanceRequestId: return BeamLanceDefinitionId;
                case ChannelBeamRequestId: return ChannelBeamDefinitionId;
                case LaserLanceRequestId: return LaserLanceDefinitionId;
                case PoisonBottleRequestId: return PoisonBottleDefinitionId;
                case StunBoltRequestId: return StunBoltDefinitionId;
                case MeteorBarrageRequestId0:
                case MeteorBarrageRequestId1:
                case MeteorBarrageRequestId2:
                case MeteorBarrageRequestId3:
                case MeteorBarrageRequestId4:
                case MeteorBarrageRequestId5: return MeteorBarrageDefinitionId;
                case SwordWaveRequestId: return SwordWaveDefinitionId;
                case HookChainRequestId: return HookChainDefinitionId;
                case ArcMineRequestId: return ArcMineDefinitionId;
                case ArcMineDropRequestId: return ArcMineDropDefinitionId;
                case RuneTurretDeployRequestId: return RuneTurretDeployDefinitionId;
                case RuneTurretShotRequestId: return RuneTurretShotDefinitionId;
                default: throw new ArgumentOutOfRangeException(nameof(requestId));
            }
        }


        private static string RequestLabelFor(int requestId)
        {
            switch (requestId)
            {
                case RequestId: return RequestLabel;
                case BurstRequestId: return BurstRequestLabel;
                case FlameLanceRequestId: return FlameLanceRequestLabel;
                case FireballLobRequestId: return FireballLobRequestLabel;
                case FireballSurgeRequestId: return FireballSurgeRequestLabel;
                case FireballReturnRequestId: return FireballReturnRequestLabel;
                case FireballSwayRequestId: return FireballSwayRequestLabel;
                case FireballHomingRequestId: return FireballHomingRequestLabel;
                case FireballBezierRequestId: return FireballBezierRequestLabel;
                case FireballExplosionRequestId: return FireballExplosionRequestLabel;
                case TornadoRequestId: return TornadoRequestLabel;
                case BeamLanceRequestId: return BeamLanceRequestLabel;
                case ChannelBeamRequestId: return ChannelBeamRequestLabel;
                case LaserLanceRequestId: return LaserLanceRequestLabel;
                case PoisonBottleRequestId: return PoisonBottleRequestLabel;
                case StunBoltRequestId: return StunBoltRequestLabel;
                case MeteorBarrageRequestId0: return MeteorBarrageRequestLabel0;
                case MeteorBarrageRequestId1: return MeteorBarrageRequestLabel1;
                case MeteorBarrageRequestId2: return MeteorBarrageRequestLabel2;
                case MeteorBarrageRequestId3: return MeteorBarrageRequestLabel3;
                case MeteorBarrageRequestId4: return MeteorBarrageRequestLabel4;
                case MeteorBarrageRequestId5: return MeteorBarrageRequestLabel5;
                case SwordWaveRequestId: return SwordWaveRequestLabel;
                case HookChainRequestId: return HookChainRequestLabel;
                case ArcMineRequestId: return ArcMineRequestLabel;
                case ArcMineDropRequestId: return ArcMineDropRequestLabel;
                case RuneTurretDeployRequestId: return RuneTurretDeployRequestLabel;
                case RuneTurretShotRequestId: return RuneTurretShotRequestLabel;
                default: return string.Empty;
            }
        }

    }

    internal sealed class CharacterTestSwordWaveDirectSweepProvider : ICombatProjectileDirectSweepProvider
    {
        public static readonly CharacterTestSwordWaveDirectSweepProvider Instance =
            new CharacterTestSwordWaveDirectSweepProvider();

        private static readonly FixVector3 WorldUp =
            new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);

        private readonly CharacterTestCharacterPhysicsRuntime _physicsRuntime;

        public CharacterTestSwordWaveDirectSweepProvider()
            : this(null)
        {
        }

        public CharacterTestSwordWaveDirectSweepProvider(CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            _physicsRuntime = physicsRuntime;
        }

        public int CollectDirectSweepQueries(
            CombatFrame frame,
            CombatProjectileSnapshot projectile,
            CombatProjectileDefinition definition,
            CombatQueryHeader header,
            FixVector3 previousPosition,
            FixVector3 currentPosition,
            List<CombatCapsuleQuery> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (definition == null
                || definition.DefinitionId != CharacterTestFireballProjectileAdapter.SwordWaveDefinitionId)
            {
                return 0;
            }

            CombatQueryHeader sweepHeader = MergeStaticObstacleLayerMask(header);
            FixVector3 axis = ResolveSwordWaveSampleAxis(projectile.Direction, projectile.LocalFrame);
            int halfIndex = CharacterTestFireballProjectileAdapter.SwordWaveCollisionSampleCount / 2;
            Fix64 spacing = CharacterTestFireballProjectileAdapter.SwordWaveCollisionHalfWidth
                / Fix64.FromInt(halfIndex);
            for (int i = 0; i < CharacterTestFireballProjectileAdapter.SwordWaveCollisionSampleCount; i++)
            {
                int sampleIndex = i - halfIndex;
                FixVector3 offset = axis * spacing * Fix64.FromInt(sampleIndex);
                output.Add(new CombatCapsuleQuery(
                    sweepHeader,
                    previousPosition + offset,
                    currentPosition + offset,
                    CharacterTestFireballProjectileAdapter.SwordWaveRadius));
            }

            return CharacterTestFireballProjectileAdapter.SwordWaveCollisionSampleCount;
        }

        private CombatQueryHeader MergeStaticObstacleLayerMask(CombatQueryHeader header)
        {
            CombatPhysicsLayerMask staticMask = _physicsRuntime == null
                ? CombatPhysicsLayerMask.None
                : _physicsRuntime.StaticObstacleLayerMask;
            int layerMaskValue = header.LayerMask.Value | staticMask.Value;
            if (layerMaskValue == header.LayerMask.Value)
                return header;

            return new CombatQueryHeader(
                header.QueryId,
                header.Kind,
                header.SourceEntityId,
                header.TraceId,
                header.ActionId,
                header.SourceOrder,
                new CombatPhysicsLayerMask(layerMaskValue));
        }

        internal static FixVector3 ResolveSwordWaveSampleAxis(FixVector3 direction, int localFrame)
        {
            FixVector3 forward = direction.TryNormalize(out FixVector3 normalizedForward)
                ? normalizedForward
                : new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
            FixVector3 right = Cross(WorldUp, forward);
            if (!right.TryNormalize(out right))
                right = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);

            FixVector3 up = Cross(forward, right);
            if (!up.TryNormalize(out up))
                up = WorldUp;

            CharacterTestDeterministicMath.SinCosMilliDegrees(
                CharacterTestFireballProjectileAdapter.ResolveSwordWaveRollMilliDegrees(localFrame),
                out int sinScaled,
                out int cosScaled);
            Fix64 sin = TrigToFix64(sinScaled);
            Fix64 cos = TrigToFix64(cosScaled);
            FixVector3 axis = right * cos + up * sin;
            return axis.TryNormalize(out FixVector3 normalizedAxis) ? normalizedAxis : right;
        }

        private static FixVector3 Cross(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                left.Y * right.Z - left.Z * right.Y,
                left.Z * right.X - left.X * right.Z,
                left.X * right.Y - left.Y * right.X);
        }

        private static Fix64 TrigToFix64(int scaled)
        {
            return Fix64.FromRaw(((long)scaled * Fix64.Scale) / CharacterTestDeterministicMath.TrigScale);
        }
    }

    internal sealed class CharacterTestFireballProjectileImpactResolver : ICombatProjectileImpactResolver
    {
        private readonly CharacterTestCombatDamageResolver _damageResolver;
        private readonly CharacterTestCombatEntityLookup _lookup;
        private readonly CombatProjectileDefinitionRegistry _definitions;

        public CharacterTestFireballProjectileImpactResolver(
            CharacterTestCombatDamageResolver damageResolver,
            CharacterTestCombatEntityLookup lookup,
            CombatProjectileDefinitionRegistry definitions)
        {
            _damageResolver = damageResolver ?? throw new ArgumentNullException(nameof(damageResolver));
            _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        }

        public bool TryResolveImpact(
            CombatProjectileSnapshot projectile,
            CombatQueryResult hit,
            CombatFrame frame,
            CombatProjectileImpactContext context,
            out CombatProjectileImpactPayload payload)
        {
            payload = default;
            CharacterTestCharacterEntity source = _lookup.FindEntityByCombatId(projectile.SourceEntityId);
            if (source == null)
                return false;
            if (!_definitions.TryGet(projectile.DefinitionId, out CombatProjectileDefinition definition))
                return false;

            CombatProjectileImpactConfig impact = definition.Impact;
            var runtimeFrame = new RuntimeFrame(frame.Value);
            if (!_damageResolver.TryResolveProjectileDamage(
                    projectile,
                    impact,
                    context,
                    source,
                    hit.TargetEntityId,
                    hit.TargetColliderId,
                    _lookup,
                    runtimeFrame,
                    CharacterTestFireballProjectileAdapter.ResolveCharacterActionId(definition),
                    out DamageProfileResolveResult damage,
                    out _))
            {
                return false;
            }

            int incomingDamage = CharacterTestFireballProjectileAdapter.IsPoisonBottleProjectile(definition)
                ? Math.Max(1, damage.IncomingDamage)
                : damage.IncomingDamage;
            payload = new CombatProjectileImpactPayload(
                incomingDamage,
                CharacterTestHitReactionResolver.CreateHitResolveReactionGate(damage),
                CharacterTestHitReactionResolver.CreateHitResolveKnockbackGate(damage),
                _lookup.ResolveTargetState(hit.TargetEntityId),
                resolvePriority: 0,
                presentationProfileId: damage.PresentationProfileId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                stableEventId: damage.Usage.StableEventId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }
    }
}
