using System;
using System.Collections.Generic;
using MxFramework.Resources;
using UnityEngine;

namespace MxFramework.Demo
{
    public static class TempImportedResourceCatalog
    {
        public const string CatalogId = "mxframework.samples";
        public const string PackageId = "mxframework.samples";
        public const string MemoryProviderId = "memory";
        public const string ProviderDataAssetPathKey = "assetPath";
        public const string PackageLabel = "package.mxframework.samples";

        public const string DomainArtLabel = "domain.art";
        public const string DomainUiLabel = "domain.ui";
        public const string DomainVfxLabel = "domain.vfx";
        public const string DomainAudioLabel = "domain.audio";

        public const string SampleKatanaLabel = "sample.katana";
        public const string SampleSkeletonLabel = "sample.skeleton";
        public const string SampleSkeletonAnimationClipLabel = "sample.skeleton.animation_clip";
        public const string SampleStartScreenLabel = "sample.start_screen";
        public const string SampleStatusAurasLabel = "sample.status_auras";
        public const string SampleMagicEffectsLabel = "sample.magic_effects";

        public const string WarmupMxAnimationLabel = "warmup.demo.mxanimation";
        public const string WarmupStartScreenLabel = "warmup.demo.start_screen";
        public const string WarmupCombatLabel = "warmup.demo.combat";
        public const string WarmupStatusEffectsLabel = "warmup.demo.status_effects";
        public const string WarmupMagicEffectsLabel = "warmup.demo.magic_effects";

        public const string SkeletonModelId = "art.character.skeleton.model";
        public const string SkeletonIdleAnimationId = "art.character.skeleton.animation.standing_idle";
        public const string SkeletonWalkForwardAnimationId = "art.character.skeleton.animation.standing_walk_forward";
        public const string SkeletonRunForwardAnimationId = "art.character.skeleton.animation.standing_run_forward";
        public const string SkeletonJumpAnimationId = "art.character.skeleton.animation.standing_jump";
        public const string SkeletonUpperBodyMaskId = "art.character.skeleton.mask.upper_body";
        public const string FireballVfxId = "vfx.magic.fireball";
        public const string BeamLanceBeamVfxId = "vfx.magic.beam_lance.beam";
        public const string BeamLanceImpactVfxId = "vfx.magic.beam_lance.impact";
        public const string BeamLanceChargeVfxId = "vfx.magic.beam_lance.charge";
        public const string LaserLancePathVfxId = "vfx.magic.laser_lance.laser";
        public const string LaserLanceImpactVfxId = "vfx.magic.laser_lance.impact";
        public const string LaserLanceChargeVfxId = "vfx.magic.laser_lance.charge";
        public const string ChannelBeamChargeVfxId = "vfx.magic.channel_beam.charge";
        public const string ChannelBeamBeamVfxId = "vfx.magic.channel_beam.beam";
        public const string ChannelBeamImpactVfxId = "vfx.magic.channel_beam.impact";
        public const string ChainLightningChargeVfxId = "vfx.magic.chain_lightning.charge";
        public const string ChainLightningArcVfxId = "vfx.magic.chain_lightning.arc";
        public const string ChainLightningImpactVfxId = "vfx.magic.chain_lightning.impact";
        public const string PoisonBottleFlightVfxId = "vfx.magic.poison_bottle.flight";
        public const string PoisonBottleShatterVfxId = "vfx.magic.poison_bottle.shatter";
        public const string PoisonBottlePoolVfxId = "vfx.magic.poison_bottle.pool";
        public const string PoisonDartFlightVfxId = PoisonBottleFlightVfxId;
        public const string PoisonDartImpactVfxId = PoisonBottleShatterVfxId;
        public const string PoisonedTickVfxId = "vfx.status.poisoned.tick";
        public const string ArcMineFlightVfxId = "vfx.magic.arc_mine.flight";
        public const string ArcMineDeployVfxId = "vfx.magic.arc_mine.deploy";
        public const string ArcMineFuseVfxId = "vfx.magic.arc_mine.fuse";
        public const string ArcMineTriggerVfxId = "vfx.magic.arc_mine.trigger";
        public const string RuneTurretDeployVfxId = "vfx.magic.rune_turret.deploy";
        public const string RuneTurretIdleVfxId = "vfx.magic.rune_turret.idle";
        public const string RuneTurretFireVfxId = "vfx.magic.rune_turret.fire";
        public const string RuneTurretFlightVfxId = "vfx.magic.rune_turret.flight";
        public const string RuneTurretImpactVfxId = "vfx.magic.rune_turret.impact";
        public const string RuneTurretExpireVfxId = "vfx.magic.rune_turret.expire";
        public const string TornadoFlightVfxId = "vfx.magic.tornado.flight";
        public const string TornadoChargeVfxId = "vfx.magic.tornado.charge";
        public const string TornadoImpactVfxId = "vfx.magic.tornado.impact";
        public const string TornadoTickVfxId = "vfx.magic.tornado.tick";
        public const string AreaConfinementZoneVfxId = "vfx.magic.area_confinement.zone";
        public const string AreaConfinementTelegraphVfxId = "vfx.magic.area_confinement.telegraph";
        public const string AreaConfinementTelegraphCancelVfxId = "vfx.magic.area_confinement.telegraph_cancel";
        public const string AreaConfinementCharacterReleaseVfxId = "vfx.magic.area_confinement.character_release";

        private static readonly TempImportedResourceCatalogAsset[] Assets =
        {
            new TempImportedResourceCatalogAsset(
                "art.weapon.katana.generic_01",
                ResourceTypeIds.GameObject,
                "mxframework.samples/art/weapon/katana/generic_01",
                "Assets/Art/MxFramework/Samples/Weapons/Katana/Prefabs/Katana_Generic01.prefab",
                new[] { PackageLabel, DomainArtLabel, SampleKatanaLabel, WarmupCombatLabel }),

            new TempImportedResourceCatalogAsset(
                SkeletonModelId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/art/character/skeleton/model",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/Models/Skeleton.fbx",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                SkeletonIdleAnimationId,
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_idle",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_idle.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                SkeletonWalkForwardAnimationId,
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_walk_forward",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_walk_forward.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                SkeletonRunForwardAnimationId,
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_run_forward",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_forward.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                SkeletonJumpAnimationId,
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_jump",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_jump.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                SkeletonUpperBodyMaskId,
                ResourceTypeIds.AvatarMask,
                "mxframework.samples/art/character/skeleton/mask/upper_body",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/Masks/SkeletonUpperBody.mask",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.standing_jump_running",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_jump_running",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_jump_running.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.standing_jump_running_landing",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_jump_running_landing",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_jump_running_landing.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.standing_land_to_standing_idle",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_land_to_standing_idle",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_land_to_standing_idle.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.standing_run_back",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_run_back",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_back.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.standing_run_left",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_run_left",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_left.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.standing_run_right",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_run_right",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_right.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.standing_sprint_forward",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_sprint_forward",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_sprint_forward.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.standing_turn_left_90",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_turn_left_90",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_turn_left_90.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.standing_turn_right_90",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_turn_right_90",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_turn_right_90.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.standing_walk_back",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_walk_back",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_walk_back.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.standing_walk_left",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_walk_left",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_walk_left.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.standing_walk_right",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/standing_walk_right",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_walk_right.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.katana_atk1",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/katana_atk1",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/katana_atk1.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.katana_atk2",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/katana_atk2",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/katana_atk2.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.katana_atk3",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/katana_atk3",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/katana_atk3.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.katana_block",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/katana_block",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/katana_block.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.katana_evade_f",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/katana_evade_f",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/katana_evade_f.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),
            new TempImportedResourceCatalogAsset(
                "art.character.skeleton.animation.katana_skill1",
                ResourceTypeIds.AnimationClip,
                "mxframework.samples/art/character/skeleton/animation/katana_skill1",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/katana_skill1.anim",
                new[] { PackageLabel, DomainArtLabel, SampleSkeletonLabel, SampleSkeletonAnimationClipLabel, WarmupMxAnimationLabel }),

            new TempImportedResourceCatalogAsset(
                "vfx.status_aura.burn",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/status_aura/burn",
                "Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Burn.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleStatusAurasLabel, WarmupCombatLabel, WarmupStatusEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "vfx.status_aura.lightning",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/status_aura/lightning",
                "Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Lightning.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleStatusAurasLabel, WarmupCombatLabel, WarmupStatusEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "vfx.status_aura.smoke",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/status_aura/smoke",
                "Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Smoke.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleStatusAurasLabel, WarmupCombatLabel, WarmupStatusEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "vfx.status_aura.stun",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/status_aura/stun",
                "Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Stun.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleStatusAurasLabel, WarmupCombatLabel, WarmupStatusEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                FireballVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/fireball",
                "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "vfx.magic.fireball.charge",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/fireball/charge",
                "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "vfx.magic.fireball_burst.flight",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/fireball_burst/flight",
                "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball_Burst.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "vfx.magic.fireball_burst.impact",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/fireball_burst/impact",
                "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball_BurstImpact.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "vfx.magic.fireball_explosion.impact",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/fireball_explosion/impact",
                "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball_ExplosionImpact.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "vfx.magic.fireball_burst.charge",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/fireball_burst/charge",
                "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball_Burst.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "vfx.magic.flame_lance.flight",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/flame_lance/flight",
                "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Flame_Lance.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "vfx.magic.flame_lance.impact",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/flame_lance/impact",
                "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Flame_LanceImpact.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                BeamLanceBeamVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/beam_lance/beam",
                "Assets/VFX/MxFramework/Samples/BeamLance/Prefabs/BeamLance_Beam.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                BeamLanceImpactVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/beam_lance/impact",
                "Assets/VFX/MxFramework/Samples/BeamLance/Prefabs/BeamLance_Impact.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                BeamLanceChargeVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/beam_lance/charge",
                "Assets/VFX/MxFramework/Samples/BeamLance/Prefabs/BeamLance_Charge.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                LaserLanceChargeVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/laser_lance/charge",
                "Assets/VFX/MxFramework/Samples/LaserLance/Prefabs/LaserLance_Charge.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                LaserLancePathVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/laser_lance/laser",
                "Assets/VFX/MxFramework/Samples/LaserLance/Prefabs/LaserLance_Laser.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                LaserLanceImpactVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/laser_lance/impact",
                "Assets/VFX/MxFramework/Samples/LaserLance/Prefabs/LaserLance_Impact.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                ChannelBeamChargeVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/channel_beam/charge",
                "Assets/VFX/MxFramework/Samples/ChannelBeam/Prefabs/ChannelBeam_Charge.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                ChannelBeamBeamVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/channel_beam/beam",
                "Assets/VFX/MxFramework/Samples/ChannelBeam/Prefabs/ChannelBeam_Beam.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                ChannelBeamImpactVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/channel_beam/impact",
                "Assets/VFX/MxFramework/Samples/ChannelBeam/Prefabs/ChannelBeam_Impact.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                ChainLightningChargeVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/chain_lightning/charge",
                "Assets/VFX/MxFramework/Samples/ChainLightning/Prefabs/ChainLightning_Charge.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                ChainLightningArcVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/chain_lightning/arc",
                "Assets/VFX/MxFramework/Samples/ChainLightning/Prefabs/ChainLightning_Arc.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                ChainLightningImpactVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/chain_lightning/impact",
                "Assets/VFX/MxFramework/Samples/ChainLightning/Prefabs/ChainLightning_Impact.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                PoisonBottleFlightVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/poison_bottle/flight",
                "Assets/VFX/MxFramework/Samples/PoisonBottle/Prefabs/PoisonBottle_Flight.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                PoisonBottleShatterVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/poison_bottle/shatter",
                "Assets/VFX/MxFramework/Samples/PoisonBottle/Prefabs/PoisonBottle_Shatter.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                PoisonBottlePoolVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/poison_bottle/pool",
                "Assets/VFX/MxFramework/Samples/PoisonBottle/Prefabs/PoisonBottle_Pool.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                PoisonedTickVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/status/poisoned/tick",
                "Assets/VFX/MxFramework/Samples/PoisonBottle/Prefabs/Poisoned_Tick.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                ArcMineFlightVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.character_test/vfx/magic/arc_mine/flight",
                "Assets/VFX/MxFramework/CharacterTest/ArcMine/Prefabs/ArcMine_Flight.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                ArcMineDeployVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.character_test/vfx/magic/arc_mine/deploy",
                "Assets/VFX/MxFramework/CharacterTest/ArcMine/Prefabs/ArcMine_Deploy.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                ArcMineFuseVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.character_test/vfx/magic/arc_mine/fuse",
                "Assets/VFX/MxFramework/CharacterTest/ArcMine/Prefabs/ArcMine_Fuse.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                ArcMineTriggerVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.character_test/vfx/magic/arc_mine/trigger",
                "Assets/VFX/MxFramework/CharacterTest/ArcMine/Prefabs/ArcMine_Trigger.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                RuneTurretDeployVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.character_test/vfx/magic/rune_turret/deploy",
                "Assets/VFX/MxFramework/CharacterTest/RuneTurret/Prefabs/RuneTurret_Deploy.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                RuneTurretIdleVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.character_test/vfx/magic/rune_turret/idle",
                "Assets/VFX/MxFramework/CharacterTest/RuneTurret/Prefabs/RuneTurret_Idle.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                RuneTurretFireVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.character_test/vfx/magic/rune_turret/fire",
                "Assets/VFX/MxFramework/CharacterTest/RuneTurret/Prefabs/RuneTurret_Fire.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                RuneTurretFlightVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.character_test/vfx/magic/rune_turret/flight",
                "Assets/VFX/MxFramework/CharacterTest/RuneTurret/Prefabs/RuneTurret_Flight.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                RuneTurretImpactVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.character_test/vfx/magic/rune_turret/impact",
                "Assets/VFX/MxFramework/CharacterTest/RuneTurret/Prefabs/RuneTurret_Impact.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                RuneTurretExpireVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.character_test/vfx/magic/rune_turret/expire",
                "Assets/VFX/MxFramework/CharacterTest/RuneTurret/Prefabs/RuneTurret_Expire.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                TornadoFlightVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/tornado/flight",
                "Assets/VFX/MxFramework/Samples/Tornado/Prefabs/Tornado_Flight.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                TornadoChargeVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/tornado/charge",
                "Assets/VFX/MxFramework/Samples/Tornado/Prefabs/Tornado_Charge.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                TornadoImpactVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/tornado/impact",
                "Assets/VFX/MxFramework/Samples/Tornado/Prefabs/Tornado_Impact.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                TornadoTickVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/tornado/tick",
                "Assets/VFX/MxFramework/Samples/Tornado/Prefabs/Tornado_Tick.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                AreaConfinementZoneVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/area_confinement/zone",
                "Assets/VFX/MxFramework/Samples/AreaConfinement/Prefabs/AreaConfinement_Zone.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                AreaConfinementTelegraphVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/area_confinement/telegraph",
                "Assets/VFX/MxFramework/Samples/AreaConfinement/Prefabs/AreaConfinement_Zone.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                AreaConfinementTelegraphCancelVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/area_confinement/telegraph_cancel",
                "Assets/VFX/MxFramework/Samples/AreaConfinement/Prefabs/AreaConfinement_Zone.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                AreaConfinementCharacterReleaseVfxId,
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/magic/area_confinement/character_release",
                "Assets/VFX/MxFramework/Samples/AreaConfinement/Prefabs/AreaConfinement_Zone.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),

            new TempImportedResourceCatalogAsset(
                "ui.start_screen.button.normal",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/button/normal",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/button_normal.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),
            new TempImportedResourceCatalogAsset(
                "ui.start_screen.button.hover",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/button/hover",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/button_hover.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),
            new TempImportedResourceCatalogAsset(
                "ui.start_screen.separator.diamond_line",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/separator/diamond_line",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/separator_diamond_line.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),
            new TempImportedResourceCatalogAsset(
                "ui.start_screen.icon.archive_book",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/icon/archive_book",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_archive_book.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),
            new TempImportedResourceCatalogAsset(
                "ui.start_screen.icon.continue_hourglass",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/icon/continue_hourglass",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_continue_hourglass.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),
            new TempImportedResourceCatalogAsset(
                "ui.start_screen.icon.exit_door",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/icon/exit_door",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_exit_door.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),
            new TempImportedResourceCatalogAsset(
                "ui.start_screen.icon.settings_cog",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/icon/settings_cog",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_settings_cog.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),

            new TempImportedResourceCatalogAsset(
                "audio.magic_effect.explosion_fire_1",
                ResourceTypeIds.AudioClip,
                "mxframework.samples/audio/magic_effect/explosion_fire_1",
                "Assets/Audio/MxFramework/Samples/MagicEffects/explosion_fire_1.ogg",
                new[] { PackageLabel, DomainAudioLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "audio.magic_effect.explosion_lightning_3",
                ResourceTypeIds.AudioClip,
                "mxframework.samples/audio/magic_effect/explosion_lightning_3",
                "Assets/Audio/MxFramework/Samples/MagicEffects/explosion_lightning_3.wav",
                new[] { PackageLabel, DomainAudioLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "audio.magic_effect.loop_fire_2",
                ResourceTypeIds.AudioClip,
                "mxframework.samples/audio/magic_effect/loop_fire_2",
                "Assets/Audio/MxFramework/Samples/MagicEffects/loop_fire_2.ogg",
                new[] { PackageLabel, DomainAudioLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "audio.magic_effect.wind",
                ResourceTypeIds.AudioClip,
                "mxframework.samples/audio/magic_effect/wind",
                "Assets/Audio/MxFramework/Samples/MagicEffects/wind.wav",
                new[] { PackageLabel, DomainAudioLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel })
        };

        public static IReadOnlyList<TempImportedResourceCatalogAsset> GetAssets()
        {
            return Assets;
        }

        public static ResourceCatalog CreateCatalog()
        {
            var entries = new List<ResourceCatalogEntry>(Assets.Length);
            for (int i = 0; i < Assets.Length; i++)
                entries.Add(Assets[i].CreateEntry());

            return new ResourceCatalog(CatalogId, PackageId, entries);
        }

        public static MemoryResourceProvider CreateMemoryProvider(ResourceCatalog catalog, Func<string, UnityEngine.Object> loadAssetAtPath)
        {
            if (loadAssetAtPath == null)
                throw new ArgumentNullException(nameof(loadAssetAtPath));

            var provider = new MemoryResourceProvider();
            if (catalog == null)
                return provider;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null || !string.Equals(entry.ProviderId, MemoryProviderId, StringComparison.Ordinal))
                    continue;
                if (!entry.ProviderData.TryGetValue(ProviderDataAssetPathKey, out string assetPath) || string.IsNullOrWhiteSpace(assetPath))
                    continue;

                UnityEngine.Object asset = loadAssetAtPath(assetPath);
                if (asset != null)
                    provider.Register(entry.Address, asset);
            }

            return provider;
        }

        public static ResourceCatalogValidationReport ValidateCatalogBootstrap(
            ResourceCatalog catalog,
            IEnumerable<string> registeredMemoryAddresses = null)
        {
            var report = new ResourceCatalogValidationReport();
            if (catalog == null)
            {
                report.AddError("CatalogMissing", default, "Temp imported resource catalog is null.");
                return report;
            }

            HashSet<string> registeredAddresses = CreateRegisteredAddressSet(registeredMemoryAddresses);
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                ResourceKey key = entry.CreateKey(catalog.PackageId);
                if (!IsSupportedDirectType(entry.TypeId))
                    report.AddError("UnsupportedDirectAssetType", key, "Sample catalog direct entry type is not supported: " + entry.TypeId + ".");

                if (!string.Equals(entry.ProviderId, MemoryProviderId, StringComparison.Ordinal))
                    report.AddError("UnexpectedProvider", key, "Sample catalog entry must use the memory provider for the editor bootstrap slice.");

                if (!entry.ProviderData.TryGetValue(ProviderDataAssetPathKey, out string assetPath) || string.IsNullOrWhiteSpace(assetPath))
                {
                    report.AddError("ProviderDataAssetPathMissing", key, "Memory sample catalog entry is missing providerData.assetPath.");
                }
                else
                {
                    if (assetPath.IndexOf("_TempImportedResources", StringComparison.Ordinal) >= 0)
                        report.AddError("TempImportedResourcePathForbidden", key, "Sample catalog entry must not point at _TempImportedResources: " + assetPath + ".");
                    if (IsFmodCatalogValue(assetPath))
                        report.AddError("FmodCatalogEntryForbidden", key, "FMOD bank/event values must not be ordinary Resource Catalog entries: " + assetPath + ".");
                }

                if (entry.Address.IndexOf("_TempImportedResources", StringComparison.Ordinal) >= 0)
                    report.AddError("TempImportedResourceAddressForbidden", key, "Sample catalog address must not point at _TempImportedResources.");
                if (IsFmodCatalogValue(entry.Address))
                    report.AddError("FmodCatalogEntryForbidden", key, "FMOD bank/event values must not be ordinary Resource Catalog entries: " + entry.Address + ".");

                if (registeredAddresses != null && !registeredAddresses.Contains(entry.Address))
                    report.AddError("MemoryAddressUnregistered", key, "Memory provider address is not registered for editor bootstrap: " + entry.Address + ".");
            }

            return report;
        }

        private static bool IsSupportedDirectType(string typeId)
        {
            return string.Equals(typeId, ResourceTypeIds.GameObject, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.Texture2D, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.AudioClip, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.AvatarMask, StringComparison.Ordinal);
        }

        private static bool IsFmodCatalogValue(string value)
        {
            return !string.IsNullOrEmpty(value)
                && (value.EndsWith(".bank", StringComparison.OrdinalIgnoreCase)
                    || value.IndexOf("bank:/", StringComparison.OrdinalIgnoreCase) >= 0
                    || value.IndexOf("event:/", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static HashSet<string> CreateRegisteredAddressSet(IEnumerable<string> registeredMemoryAddresses)
        {
            if (registeredMemoryAddresses == null)
                return null;

            var addresses = new HashSet<string>(StringComparer.Ordinal);
            foreach (string address in registeredMemoryAddresses)
            {
                if (!string.IsNullOrWhiteSpace(address))
                    addresses.Add(address);
            }

            return addresses;
        }
    }

    public readonly struct TempImportedResourceCatalogAsset
    {
        public TempImportedResourceCatalogAsset(
            string id,
            string typeId,
            string address,
            string assetPath,
            IEnumerable<string> labels)
        {
            Id = id ?? string.Empty;
            TypeId = typeId ?? ResourceTypeIds.Object;
            Address = address ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            Labels = labels != null ? new List<string>(labels) : new List<string>();
        }

        public string Id { get; }
        public string TypeId { get; }
        public string Address { get; }
        public string AssetPath { get; }
        public IReadOnlyList<string> Labels { get; }

        public ResourceCatalogEntry CreateEntry()
        {
            return new ResourceCatalogEntry(
                Id,
                TypeId,
                TempImportedResourceCatalog.MemoryProviderId,
                Address,
                variant: string.Empty,
                packageId: TempImportedResourceCatalog.PackageId,
                dependencies: Array.Empty<ResourceKey>(),
                labels: Labels,
                hash: string.Empty,
                size: 0,
                allowOverride: false,
                providerData: new Dictionary<string, string>
                {
                    { TempImportedResourceCatalog.ProviderDataAssetPathKey, AssetPath }
                });
        }
    }
}
