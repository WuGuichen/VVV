using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Vfx;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestAbilityHotbarTests
    {
        [Test]
        public void Hotbar_DefaultBindings_MapNineSlotsIncludingFireballExplosion()
        {
            CharacterTestAbilityHotbarRuntime hotbar = CreateHotbar();

            CharacterTestAbilityHotbarSlotBinding[] slots = hotbar.CreateBindingSnapshot();

            Assert.AreEqual(CharacterTestAbilityHotbarRuntime.SlotCount, slots.Length);
            Assert.AreEqual(CharacterTestAbilityIds.FireballAbilityId, slots[0].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballBurstAbilityId, slots[1].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.FlameLanceAbilityId, slots[2].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballLobAbilityId, slots[3].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballSurgeAbilityId, slots[4].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballReturnAbilityId, slots[5].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.TornadoAbilityId, slots[6].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.GuardAuraAbilityId, slots[7].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballExplosionAbilityId, slots[8].AbilityId);
        }

        [Test]
        public void Hotbar_AllowsDuplicateBindingsAndStableSortedAbilityChoices()
        {
            CharacterTestAbilityHotbarRuntime hotbar = CreateHotbar();

            Assert.IsTrue(hotbar.AssignSlot(
                8,
                CharacterTestAbilityIds.FireballAbilityId,
                out string message),
                message);

            CharacterTestAbilityHotbarSlotBinding[] slots = hotbar.CreateBindingSnapshot();
            Assert.AreEqual(CharacterTestAbilityIds.FireballAbilityId, slots[0].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballAbilityId, slots[7].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballAbilityId, hotbar.AvailableAbilities[0].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.GuardAuraAbilityId, hotbar.AvailableAbilities[7].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballExplosionAbilityId, hotbar.AvailableAbilities[8].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.PhysicalShieldAbilityId, hotbar.AvailableAbilities[9].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.AreaConfinementAbilityId, hotbar.AvailableAbilities[10].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.BeamLanceAbilityId, hotbar.AvailableAbilities[11].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.ChannelBeamAbilityId, hotbar.AvailableAbilities[12].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.LaserLanceAbilityId, hotbar.AvailableAbilities[13].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.ChainLightningAbilityId, hotbar.AvailableAbilities[14].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.PoisonBottleAbilityId, hotbar.AvailableAbilities[15].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.StunBoltAbilityId, hotbar.AvailableAbilities[16].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.MeteorBarrageAbilityId, hotbar.AvailableAbilities[17].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.SwordWaveAbilityId, hotbar.AvailableAbilities[18].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.DashStrikeAbilityId, hotbar.AvailableAbilities[19].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.HookChainAbilityId, hotbar.AvailableAbilities[20].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.ArcMineAbilityId, hotbar.AvailableAbilities[21].AbilityId);
            Assert.AreEqual(CharacterTestAbilityIds.RuneTurretAbilityId, hotbar.AvailableAbilities[22].AbilityId);
            Assert.IsTrue(hotbar.AssignSlot(
                7,
                CharacterTestAbilityIds.FireballSwayAbilityId,
                out string swayMessage),
                swayMessage);
            Assert.AreEqual(CharacterTestAbilityIds.FireballSwayAbilityId, hotbar.GetAbilityIdForSlot(7));
        }

        [Test]
        public void Hotbar_LoadUnknownAbilityIds_ClearsOnlyUnknownSlots()
        {
            string directory = CreateTempDirectory();
            string path = Path.Combine(directory, "ability_hotbar.json");
            try
            {
                File.WriteAllText(
                    path,
                    "{\"schemaVersion\":1,\"slots\":[{\"slotNumber\":1,\"abilityId\":999999},{\"slotNumber\":2,\"abilityId\":910003}]}");
                CharacterTestAbilityHotbarRuntime hotbar = CreateHotbar();

                CharacterTestAbilityHotbarPersistenceResult result = hotbar.LoadFromFile(path);

                CharacterTestAbilityHotbarSlotBinding[] slots = hotbar.CreateBindingSnapshot();
                Assert.IsTrue(result.Success);
                Assert.AreEqual(CharacterTestAbilityHotbarRuntime.EmptyAbilityId, slots[0].AbilityId);
                Assert.AreEqual(CharacterTestAbilityIds.FlameLanceAbilityId, slots[1].AbilityId);
                Assert.AreEqual(CharacterTestAbilityHotbarRuntime.EmptyAbilityId, slots[2].AbilityId);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void Hotbar_SaveThenLoad_RestoresBindings()
        {
            string directory = CreateTempDirectory();
            string path = Path.Combine(directory, "ability_hotbar.json");
            try
            {
                CharacterTestAbilityHotbarRuntime hotbar = CreateHotbar(path);
                Assert.IsTrue(hotbar.ClearSlot(1, out string clearMessage), clearMessage);
                Assert.IsTrue(hotbar.AssignSlot(
                    9,
                    CharacterTestAbilityIds.FireballReturnAbilityId,
                    out string assignMessage),
                    assignMessage);

                CharacterTestAbilityHotbarPersistenceResult save = hotbar.SaveToFile();
                CharacterTestAbilityHotbarRuntime loaded = CreateHotbar();
                CharacterTestAbilityHotbarPersistenceResult load = loaded.LoadFromFile(path);

                CharacterTestAbilityHotbarSlotBinding[] slots = loaded.CreateBindingSnapshot();
                Assert.IsTrue(save.Success, save.Message);
                Assert.IsTrue(load.Success, load.Message);
                StringAssert.Contains(
                    "\"defaultLayoutVersion\": 3",
                    File.ReadAllText(path));
                Assert.AreEqual(CharacterTestAbilityHotbarRuntime.EmptyAbilityId, slots[0].AbilityId);
                Assert.AreEqual(CharacterTestAbilityIds.FireballReturnAbilityId, slots[8].AbilityId);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void Hotbar_LoadLegacyLayout_FillsEmptyNineWithFireballExplosionOnce()
        {
            string directory = CreateTempDirectory();
            string path = Path.Combine(directory, "ability_hotbar.json");
            try
            {
                File.WriteAllText(
                    path,
                    "{\"schemaVersion\":1,\"slots\":[{\"slotNumber\":9,\"abilityId\":0}]}");
                CharacterTestAbilityHotbarRuntime hotbar = CreateHotbar();

                CharacterTestAbilityHotbarPersistenceResult result = hotbar.LoadFromFile(path);

                CharacterTestAbilityHotbarSlotBinding[] slots = hotbar.CreateBindingSnapshot();
                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(CharacterTestAbilityIds.FireballExplosionAbilityId, slots[8].AbilityId);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void Hotbar_LoadCurrentLayout_PreservesUserClearedNine()
        {
            string directory = CreateTempDirectory();
            string path = Path.Combine(directory, "ability_hotbar.json");
            try
            {
                File.WriteAllText(
                    path,
                    "{\"schemaVersion\":1,\"defaultLayoutVersion\":3,\"slots\":[{\"slotNumber\":9,\"abilityId\":0}]}");
                CharacterTestAbilityHotbarRuntime hotbar = CreateHotbar();

                CharacterTestAbilityHotbarPersistenceResult result = hotbar.LoadFromFile(path);

                CharacterTestAbilityHotbarSlotBinding[] slots = hotbar.CreateBindingSnapshot();
                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(CharacterTestAbilityHotbarRuntime.EmptyAbilityId, slots[8].AbilityId);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void Hotbar_LoadVersionTwoLayout_MigratesDefaultSevenToTornado()
        {
            string directory = CreateTempDirectory();
            string path = Path.Combine(directory, "ability_hotbar.json");
            try
            {
                File.WriteAllText(
                    path,
                    "{\"schemaVersion\":1,\"defaultLayoutVersion\":2,\"slots\":[{\"slotNumber\":7,\"abilityId\":910007}]}");
                CharacterTestAbilityHotbarRuntime hotbar = CreateHotbar();

                CharacterTestAbilityHotbarPersistenceResult result = hotbar.LoadFromFile(path);

                CharacterTestAbilityHotbarSlotBinding[] slots = hotbar.CreateBindingSnapshot();
                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(CharacterTestAbilityIds.TornadoAbilityId, slots[6].AbilityId);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void Hotbar_AbilityBindingViewModel_ShowsTornadoPhysicalShieldAreaConfinementLaserLanceChannelBeamChainLightningPoisonBottleStunBoltMeteorBarrageSwordWaveDashStrikeHookChainArcMineAndRuneTurretChoices()
        {
            CharacterTestAbilityHotbarRuntime hotbar = CreateHotbar();

            CharacterTestFairyGuiAbilityBindingPanelViewModel viewModel =
                CharacterTestFairyGuiAbilityBindingPanelViewModelBuilder.Build(
                    hotbar,
                    null,
                    selectedSlot: 9,
                    dirty: false);

            Assert.AreEqual(hotbar.AvailableAbilities.Count, viewModel.Abilities.Count);
            CharacterTestFairyGuiAbilityChoiceViewModel tornadoChoice = viewModel.Abilities[6];
            Assert.IsTrue(tornadoChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.TornadoAbilityId, tornadoChoice.AbilityId);
            StringAssert.Contains("Tornado", tornadoChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel explosionChoice = viewModel.Abilities[8];
            Assert.IsTrue(explosionChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.FireballExplosionAbilityId, explosionChoice.AbilityId);
            StringAssert.Contains("Explosion", explosionChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel physicalShieldChoice = viewModel.Abilities[9];
            Assert.IsTrue(physicalShieldChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.PhysicalShieldAbilityId, physicalShieldChoice.AbilityId);
            StringAssert.Contains("Physical Shield", physicalShieldChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel areaConfinementChoice = viewModel.Abilities[10];
            Assert.IsTrue(areaConfinementChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.AreaConfinementAbilityId, areaConfinementChoice.AbilityId);
            StringAssert.Contains("Area Confinement", areaConfinementChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel beamLanceChoice = viewModel.Abilities[11];
            Assert.IsTrue(beamLanceChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.BeamLanceAbilityId, beamLanceChoice.AbilityId);
            StringAssert.Contains("Beam Lance", beamLanceChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel channelBeamChoice = viewModel.Abilities[12];
            Assert.IsTrue(channelBeamChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.ChannelBeamAbilityId, channelBeamChoice.AbilityId);
            StringAssert.Contains("Channel Beam", channelBeamChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel laserLanceChoice = viewModel.Abilities[13];
            Assert.IsTrue(laserLanceChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.LaserLanceAbilityId, laserLanceChoice.AbilityId);
            StringAssert.Contains("Laser Lance", laserLanceChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel chainLightningChoice = viewModel.Abilities[14];
            Assert.IsTrue(chainLightningChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.ChainLightningAbilityId, chainLightningChoice.AbilityId);
            StringAssert.Contains("Chain Lightning", chainLightningChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel poisonBottleChoice = viewModel.Abilities[15];
            Assert.IsTrue(poisonBottleChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.PoisonBottleAbilityId, poisonBottleChoice.AbilityId);
            StringAssert.Contains("Poison Bottle", poisonBottleChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel stunBoltChoice = viewModel.Abilities[16];
            Assert.IsTrue(stunBoltChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.StunBoltAbilityId, stunBoltChoice.AbilityId);
            StringAssert.Contains("Stun Bolt", stunBoltChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel meteorBarrageChoice = viewModel.Abilities[17];
            Assert.IsTrue(meteorBarrageChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.MeteorBarrageAbilityId, meteorBarrageChoice.AbilityId);
            StringAssert.Contains("Meteor Barrage", meteorBarrageChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel swordWaveChoice = viewModel.Abilities[18];
            Assert.IsTrue(swordWaveChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.SwordWaveAbilityId, swordWaveChoice.AbilityId);
            StringAssert.Contains("Sword Wave", swordWaveChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel dashStrikeChoice = viewModel.Abilities[19];
            Assert.IsTrue(dashStrikeChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.DashStrikeAbilityId, dashStrikeChoice.AbilityId);
            StringAssert.Contains("Dash Strike", dashStrikeChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel hookChainChoice = viewModel.Abilities[20];
            Assert.IsTrue(hookChainChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.HookChainAbilityId, hookChainChoice.AbilityId);
            StringAssert.Contains("Hook Chain", hookChainChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel arcMineChoice = viewModel.Abilities[21];
            Assert.IsTrue(arcMineChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.ArcMineAbilityId, arcMineChoice.AbilityId);
            StringAssert.Contains("Arc Mine", arcMineChoice.Label);
            CharacterTestFairyGuiAbilityChoiceViewModel runeTurretChoice = viewModel.Abilities[22];
            Assert.IsTrue(runeTurretChoice.Visible);
            Assert.AreEqual(CharacterTestAbilityIds.RuneTurretAbilityId, runeTurretChoice.AbilityId);
            StringAssert.Contains("Rune Turret", runeTurretChoice.Label);
        }

        [Test]
        public void DashStrike_AuthoringData_BindsAbilityActionDamageAndVfx()
        {
            AbilityDefinitionRegistry abilityRegistry = CharacterTestAbilityCatalogSource.CreateRegistry();
            Assert.IsTrue(abilityRegistry.TryGet(
                CharacterTestAbilityIds.DashStrikeAbilityId,
                out AbilityDefinitionConfig ability));
            Assert.AreEqual(CharacterTestAbilityIds.DashStrikeAbilityStableId, ability.AbilityStableId);
            Assert.AreEqual(CharacterTestAbilityIds.DashStrikeCooldownFrames, ability.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D406_Id7, ability.TargetPolicyId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id14, ability.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.DashStrikePresentationProfileId,
                ability.PresentationProfileId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.DashStrikeActionId,
                ability.ActionBinding.ActionId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.DashStrikeIntentId,
                ability.ActionBinding.ActionIntentId);

            CharacterTestCharacterActionRuntime actionRuntime =
                CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.DashStrikeActionId,
                out CharacterActionConfig action));
            Assert.AreEqual(90, action.DurationFrames);
            Assert.IsTrue(action.MotionTrack.UsesRootMotion);
            CharacterActionRootMotionConfig rootMotion = action.MotionTrack.RootMotion;
            Assert.IsNotNull(rootMotion);
            Assert.AreEqual(CharacterActionRootMotionSpace.Local, rootMotion.Space);
            Assert.AreEqual(CharacterActionRootMotionObstaclePolicy.StaticOnly, rootMotion.ObstaclePolicy);
            Assert.AreEqual(14, rootMotion.Frames.Length);
            float totalForward = 0f;
            for (int i = 0; i < rootMotion.Frames.Length; i++)
            {
                CharacterActionRootMotionFrame frame = rootMotion.Frames[i];
                Assert.AreEqual(CharacterTestCharacterActionConfigSource.DashStrikeHitStartFrame + i, frame.Frame);
                Assert.AreEqual(0f, frame.X);
                Assert.AreEqual(0f, frame.Y);
                Assert.AreEqual(0.3f, frame.Z);
                totalForward += frame.Z;
            }

            Assert.AreEqual(4.2f, totalForward, 0.0001f);
            Assert.AreEqual(0, action.GameplayTrack.Events.Length);
            CombatTrackEvent[] combatEvents = action.CombatTrack.Events;
            Assert.AreEqual(1, combatEvents.Length);
            CombatTrackEvent hitWindow = combatEvents[0];
            Assert.AreEqual(CharacterActionTrackEventKind.HitTraceWindow, hitWindow.Kind);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.DashStrikeHitStartFrame, hitWindow.Frame);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.DashStrikeHitStopFrame, hitWindow.EndFrame);
            Assert.AreEqual(0, hitWindow.TraceProfileId);
            Assert.AreEqual(0, hitWindow.EquipmentSlotId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id14, hitWindow.DamagePayload.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.DashStrikeHitWindowStableEventId,
                hitWindow.StableEventId);
            Assert.IsTrue(hitWindow.HitVolume.HasVolume);
            Assert.AreEqual(CharacterActionHitVolumeSourceKind.CasterBody, hitWindow.HitVolume.SourceKind);
            Assert.AreEqual(CharacterActionHitVolumeShapeKind.Capsule, hitWindow.HitVolume.ShapeKind);
            Assert.AreEqual(0f, hitWindow.HitVolume.LocalPointAX);
            Assert.AreEqual(0f, hitWindow.HitVolume.LocalPointAY);
            Assert.AreEqual(0f, hitWindow.HitVolume.LocalPointAZ);
            Assert.AreEqual(0f, hitWindow.HitVolume.LocalPointBX);
            Assert.AreEqual(0f, hitWindow.HitVolume.LocalPointBY);
            Assert.AreEqual(0f, hitWindow.HitVolume.LocalPointBZ);
            Assert.AreEqual(0f, hitWindow.HitVolume.Radius);
            Assert.AreEqual(0f, hitWindow.HitVolume.BodyOffsetX);
            Assert.AreEqual(0f, hitWindow.HitVolume.BodyOffsetY);
            Assert.AreEqual(0f, hitWindow.HitVolume.BodyOffsetZ);
            Assert.AreEqual(1f, hitWindow.HitVolume.BodyRadiusScale);
            Assert.AreEqual(0f, hitWindow.HitVolume.BodyRadiusAdd);
            Assert.AreEqual(1f, hitWindow.HitVolume.BodyHeightScale);
            Assert.AreEqual(0f, hitWindow.HitVolume.BodyHeightAdd);
            Assert.AreEqual(CombatHitSameTargetPolicy.OncePerEvent, hitWindow.HitEvent.HitSameTargetPolicy);
            Assert.AreEqual(CombatHitTargetTypes.HostileCharacter, hitWindow.HitEvent.EffectiveTargetTypes);

            VfxTriggerMapResolver resolver = CharacterTestVfxDefinitionSource.CreateTriggerResolver();
            int trigger = MxFramework.Vfx.CombatBridge.CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.DashStrikePresentationProfileId);
            var output = new List<VfxSpawnRequest>();
            int count = resolver.Resolve(new VfxTriggerContext(
                RuntimeFrame.Zero,
                VfxSourceDomains.CombatHit,
                trigger,
                sourceId: 1,
                targetId: 2,
                sourceFrame: CharacterTestCharacterActionConfigSource.DashStrikeHitStartFrame,
                localFrame: CharacterTestCharacterActionConfigSource.DashStrikeHitStartFrame,
                sourceOrder: 0,
                VfxAnchor.Target(1, 2, VfxPose.Identity),
                VfxParameterBlock.Empty,
                traceId: new RuntimeTraceIdentity(996046)), output);
            Assert.AreEqual(1, count);
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.SwordWaveImpactDefinitionId), output[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.SwordWaveImpactLifetimeFrames, output[0].LifetimeFramesOverride);
        }

        [Test]
        public void PhysicalShield_AuthoringData_BindsAbilityActionAndVfx()
        {
            AbilityDefinitionRegistry abilityRegistry = CharacterTestAbilityCatalogSource.CreateRegistry();
            Assert.IsTrue(abilityRegistry.TryGet(
                CharacterTestAbilityIds.PhysicalShieldAbilityId,
                out AbilityDefinitionConfig ability));
            Assert.AreEqual(
                CharacterTestAbilityIds.PhysicalShieldAbilityStableId,
                ability.AbilityStableId);
            Assert.AreEqual(
                CharacterTestAbilityIds.PhysicalShieldCooldownFrames,
                ability.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D406_Id7, ability.TargetPolicyId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.PhysicalShieldActionId,
                ability.ActionBinding.ActionId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.PhysicalShieldIntentId,
                ability.ActionBinding.ActionIntentId);

            CharacterTestCharacterActionRuntime actionRuntime =
                CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.PhysicalShieldActionId,
                out CharacterActionConfig action));
            Assert.AreEqual(90, action.DurationFrames);
            GameplayTrackEvent shieldEvent = FindGameplayRequestEvent(
                action,
                CharacterTestCharacterActionConfigSource.PhysicalShieldGameplayRequestId);
            Assert.AreEqual(6, shieldEvent.Frame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.PhysicalShieldStableEventId,
                shieldEvent.StableEventId);

            PresentationTrackEvent vfxEvent = FindPresentationVfxEvent(
                action,
                CharacterTestVfxIds.PhysicalShieldResourceKey.ToString());
            Assert.AreEqual(0, vfxEvent.Frame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.PhysicalShieldVfxStableEventId,
                vfxEvent.StableEventId);

            VfxDefinitionRegistry definitions = CharacterTestVfxDefinitionSource.CreateDefinitions();
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.PhysicalShieldDefinitionId),
                out VfxDefinition definition));
            Assert.AreEqual(CharacterTestVfxIds.PhysicalShieldResourceKey, definition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.PhysicalShieldLifetimeFrames,
                definition.DefaultLifetimeFrames);

            ResourceCatalog catalog = CharacterTestVfxDefinitionSource.CreateResourceCatalog();
            AssertEntry(
                catalog,
                CharacterTestVfxIds.PhysicalShieldResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "PhysicalShield");
        }

        [Test]
        public void StunBolt_AuthoringData_BindsAbilityActionProjectileAndVfx()
        {
            AbilityDefinitionRegistry abilityRegistry = CharacterTestAbilityCatalogSource.CreateRegistry();
            Assert.IsTrue(abilityRegistry.TryGet(
                CharacterTestAbilityIds.StunBoltAbilityId,
                out AbilityDefinitionConfig ability));
            Assert.AreEqual(CharacterTestAbilityIds.StunBoltAbilityStableId, ability.AbilityStableId);
            Assert.AreEqual(CharacterTestAbilityIds.StunBoltCooldownFrames, ability.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D406_Id6, ability.TargetPolicyId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id11, ability.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.StunBoltPresentationProfileId,
                ability.PresentationProfileId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.StunBoltActionId,
                ability.ActionBinding.ActionId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.StunBoltIntentId,
                ability.ActionBinding.ActionIntentId);

            CharacterTestCharacterActionRuntime actionRuntime =
                CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.StunBoltActionId,
                out CharacterActionConfig action));
            Assert.AreEqual(90, action.DurationFrames);
            GameplayTrackEvent projectileEvent = FindGameplayRequestEvent(
                action,
                CharacterTestCharacterActionConfigSource.StunBoltProjectileRequestId);
            Assert.AreEqual(30, projectileEvent.Frame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.StunBoltProjectileImpactStableEventId,
                projectileEvent.StableEventId);
            PresentationTrackEvent chargeVfxEvent = FindPresentationVfxEvent(
                action,
                CharacterTestVfxIds.StunBoltChargeResourceKey.ToString());
            Assert.AreEqual(0, chargeVfxEvent.Frame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.StunBoltChargeVfxStableEventId,
                chargeVfxEvent.StableEventId);

            CombatProjectileDefinitionRegistry projectileRegistry =
                CharacterTestFireballProjectileAdapter.CreateRegistry();
            Assert.IsTrue(projectileRegistry.TryGet(
                CharacterTestFireballProjectileAdapter.StunBoltDefinitionId,
                out CombatProjectileDefinition projectile));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.StunBoltStableId, projectile.StableId);
            Assert.AreEqual(CombatProjectileMotionKind.Linear, projectile.Motion.Kind);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.StunBoltSpeedPerFrame, projectile.Motion.SpeedPerFrame);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.StunBoltLifetimeFrames, projectile.Motion.LifetimeFrames);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.StunBoltRadius, projectile.Collision.Radius);
            Assert.IsTrue(projectile.Collision.StopOnFirstAcceptedImpact);
            Assert.AreEqual(1, projectile.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.StunBoltProjectileImpactStableEventId,
                projectile.Impact.HitEvent.StableEventId);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.DirectHit, projectile.Impact.PolicyKind);
            Assert.AreEqual(1, projectile.Impact.HitEvent.OnHitBuffCount);
            Assert.AreEqual(CharacterTestStunIds.StunBuffId, projectile.Impact.HitEvent.OnHitBuffs[0].BuffId);
            Assert.AreEqual(0, projectile.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id11, projectile.Impact.HitEvent.Damage.DamageTypeId);

            CombatProjectileSpawnProfileRegistry spawnProfiles =
                CharacterTestFireballProjectileAdapter.CreateSpawnProfileRegistry();
            Assert.IsTrue(spawnProfiles.TryGet(
                CharacterTestCharacterActionConfigSource.StunBoltProjectileRequestId,
                out CombatProjectileSpawnProfile spawnProfile));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.StunBoltStableId, spawnProfile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileAimKind.LockedTargetOrCasterForwardRange, spawnProfile.Aim.Kind);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, spawnProfile.Aim.FallbackKind);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.StunBoltRange, spawnProfile.Aim.MaxRange);

            VfxDefinitionRegistry definitions = CharacterTestVfxDefinitionSource.CreateDefinitions();
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.StunBoltChargeDefinitionId),
                out VfxDefinition chargeDefinition));
            Assert.AreEqual(CharacterTestVfxIds.StunBoltChargeResourceKey, chargeDefinition.PrefabResource);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.StunBoltImpactDefinitionId),
                out VfxDefinition impactDefinition));
            Assert.AreEqual(CharacterTestVfxIds.StunBoltImpactResourceKey, impactDefinition.PrefabResource);

            VfxTriggerMapResolver resolver = CharacterTestVfxDefinitionSource.CreateTriggerResolver();
            int trigger = MxFramework.Vfx.CombatBridge.CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.StunBoltPresentationProfileId);
            var output = new List<VfxSpawnRequest>();
            int count = resolver.Resolve(new VfxTriggerContext(
                RuntimeFrame.Zero,
                VfxSourceDomains.CombatHit,
                trigger,
                sourceId: 1,
                targetId: 2,
                sourceFrame: 30,
                localFrame: 30,
                sourceOrder: 0,
                VfxAnchor.Target(1, 2, VfxPose.Identity),
                VfxParameterBlock.Empty,
                traceId: new RuntimeTraceIdentity(918281)), output);
            Assert.AreEqual(1, count);
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.StunBoltImpactDefinitionId), output[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.StunBoltImpactLifetimeFrames, output[0].LifetimeFramesOverride);

            CombatProjectileUnityPresentationRegistry presentationRegistry =
                CharacterTestProjectilePresentationSource.CreateRegistry();
            Assert.IsTrue(presentationRegistry.TryGetV2(
                CharacterTestFireballProjectileAdapter.StunBoltDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 binding));
            Assert.AreEqual(CharacterTestVfxIds.StunBoltFlightResourceKey, binding.FlightBodyResource);
            Assert.AreEqual(CharacterTestProjectilePresentationSource.FireballViewNamePrefix, binding.ViewNamePrefix);
        }

        [Test]
        public void HookChain_AuthoringData_BindsAbilityActionProjectileAndVfx()
        {
            AbilityDefinitionRegistry abilityRegistry = CharacterTestAbilityCatalogSource.CreateRegistry();
            Assert.IsTrue(abilityRegistry.TryGet(
                CharacterTestAbilityIds.HookChainAbilityId,
                out AbilityDefinitionConfig ability));
            Assert.AreEqual(CharacterTestAbilityIds.HookChainAbilityStableId, ability.AbilityStableId);
            Assert.AreEqual(CharacterTestAbilityIds.HookChainCooldownFrames, ability.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D406_Id10, ability.TargetPolicyId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id15, ability.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.HookChainPresentationProfileId,
                ability.PresentationProfileId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.HookChainActionId,
                ability.ActionBinding.ActionId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.HookChainIntentId,
                ability.ActionBinding.ActionIntentId);

            CharacterTestCharacterActionRuntime actionRuntime =
                CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.HookChainActionId,
                out CharacterActionConfig action));
            Assert.AreEqual(90, action.DurationFrames);
            GameplayTrackEvent projectileEvent = FindGameplayRequestEvent(
                action,
                CharacterTestCharacterActionConfigSource.HookChainProjectileRequestId);
            Assert.AreEqual(12, projectileEvent.Frame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.HookChainProjectileImpactStableEventId,
                projectileEvent.StableEventId);
            PresentationTrackEvent chargeVfxEvent = FindPresentationVfxEvent(
                action,
                CharacterTestVfxIds.StunBoltChargeResourceKey.ToString());
            Assert.AreEqual(0, chargeVfxEvent.Frame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.HookChainChargeVfxStableEventId,
                chargeVfxEvent.StableEventId);

            CombatProjectileDefinitionRegistry projectileRegistry =
                CharacterTestFireballProjectileAdapter.CreateRegistry();
            Assert.IsTrue(projectileRegistry.TryGet(
                CharacterTestFireballProjectileAdapter.HookChainDefinitionId,
                out CombatProjectileDefinition projectile));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainStableId, projectile.StableId);
            Assert.AreEqual(CombatProjectileMotionKind.Linear, projectile.Motion.Kind);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainSpeedPerFrame, projectile.Motion.SpeedPerFrame);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainLifetimeFrames, projectile.Motion.LifetimeFrames);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainRadius, projectile.Collision.Radius);
            Assert.IsTrue(projectile.Collision.StopOnFirstAcceptedImpact);
            Assert.IsTrue(projectile.Collision.StopOnBlockedImpact);
            Assert.AreEqual(1, projectile.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.HookChainProjectileImpactStableEventId,
                projectile.Impact.HitEvent.StableEventId);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.DirectHit, projectile.Impact.PolicyKind);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainImpactPower, projectile.Impact.HitEvent.Damage.ImpactPower);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id11, projectile.Impact.HitEvent.Damage.DamageTypeId);

            CombatProjectileSpawnProfileRegistry spawnProfiles =
                CharacterTestFireballProjectileAdapter.CreateSpawnProfileRegistry();
            Assert.IsTrue(spawnProfiles.TryGet(
                CharacterTestCharacterActionConfigSource.HookChainProjectileRequestId,
                out CombatProjectileSpawnProfile spawnProfile));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainStableId, spawnProfile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileAimKind.LockedTargetOrCasterForwardRange, spawnProfile.Aim.Kind);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, spawnProfile.Aim.FallbackKind);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.HookChainRange, spawnProfile.Aim.MaxRange);

            VfxTriggerMapResolver resolver = CharacterTestVfxDefinitionSource.CreateTriggerResolver();
            int trigger = MxFramework.Vfx.CombatBridge.CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.HookChainPresentationProfileId);
            var output = new List<VfxSpawnRequest>();
            int count = resolver.Resolve(new VfxTriggerContext(
                RuntimeFrame.Zero,
                VfxSourceDomains.CombatHit,
                trigger,
                sourceId: 1,
                targetId: 2,
                sourceFrame: 12,
                localFrame: 12,
                sourceOrder: 0,
                VfxAnchor.Target(1, 2, VfxPose.Identity),
                VfxParameterBlock.Empty,
                traceId: new RuntimeTraceIdentity(992110)), output);
            Assert.AreEqual(1, count);
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.StunBoltImpactDefinitionId), output[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.StunBoltImpactLifetimeFrames, output[0].LifetimeFramesOverride);

            CombatProjectileUnityPresentationRegistry presentationRegistry =
                CharacterTestProjectilePresentationSource.CreateRegistry();
            Assert.IsTrue(presentationRegistry.TryGetV2(
                CharacterTestFireballProjectileAdapter.HookChainDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 binding));
            Assert.AreEqual(CharacterTestVfxIds.StunBoltFlightResourceKey, binding.FlightBodyResource);
            Assert.AreEqual(CharacterTestProjectilePresentationSource.FireballViewNamePrefix, binding.ViewNamePrefix);
        }

        [Test]
        public void ArcMine_AuthoringData_BindsAbilityActionTuningDamageAndVfx()
        {
            AbilityDefinitionRegistry abilityRegistry = CharacterTestAbilityCatalogSource.CreateRegistry();
            Assert.IsTrue(abilityRegistry.TryGet(
                CharacterTestAbilityIds.ArcMineAbilityId,
                out AbilityDefinitionConfig ability));
            Assert.AreEqual(CharacterTestAbilityIds.ArcMineAbilityStableId, ability.AbilityStableId);
            Assert.AreEqual(CharacterTestAbilityIds.ArcMineCooldownFrames, ability.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D406_Id7, ability.TargetPolicyId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id16, ability.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.ArcMinePresentationProfileId,
                ability.PresentationProfileId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.ArcMineActionId,
                ability.ActionBinding.ActionId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.ArcMineIntentId,
                ability.ActionBinding.ActionIntentId);

            CharacterTestCharacterActionRuntime actionRuntime =
                CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.ArcMineActionId,
                out CharacterActionConfig action));
            Assert.AreEqual(328, action.ActionId.Value);
            Assert.AreEqual(90, action.DurationFrames);
            GameplayTrackEvent deployEvent = FindGameplayRequestEvent(
                action,
                CharacterTestCharacterActionConfigSource.ArcMineGameplayRequestId);
            Assert.AreEqual(12, deployEvent.Frame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.ArcMineDeployStableEventId,
                deployEvent.StableEventId);
            Assert.AreEqual(
                CharacterTestAbilityIds.ArcMineAbilityId,
                deployEvent.AbilityStableId);

            CharacterTestArcMineTuning tuning = CharacterTestArcMineTuningSource.LoadDefault();
            Assert.AreEqual(CharacterTestArcMineIds.TuningStableId, tuning.StableId);
            Assert.AreEqual(CharacterTestAbilityIds.ArcMineAbilityStableId, tuning.AbilityStableId);
            Assert.AreEqual(CharacterTestArcMineIds.SourceStableId, tuning.SourceStableId);
            Assert.AreEqual(CharacterTestArcMineIds.DeployRangeMeters, tuning.DeployRangeMeters);
            Assert.AreEqual(CharacterTestArcMineIds.FallbackForwardMeters, tuning.FallbackForwardMeters);
            Assert.AreEqual(CharacterTestArcMineIds.FuseDelayFrames, tuning.FuseDelayFrames);
            Assert.AreEqual(CharacterTestArcMineIds.DurationFrames, tuning.DurationFrames);
            Assert.AreEqual(CharacterTestArcMineIds.TriggerRadiusCentimeters, tuning.TriggerRadiusCentimeters);
            Assert.AreEqual(CharacterTestArcMineIds.BlastRadiusCentimeters, tuning.BlastRadiusCentimeters);
            Assert.AreEqual(CharacterTestArcMineIds.MaxBlastTargets, tuning.MaxBlastTargets);
            Assert.AreEqual(CharacterTestArcMineIds.DeployVfxDefinitionId, tuning.DeployVfxDefinitionId);
            Assert.AreEqual(CharacterTestArcMineIds.FuseVfxDefinitionId, tuning.FuseVfxDefinitionId);
            Assert.AreEqual(CharacterTestArcMineIds.TriggerVfxDefinitionId, tuning.TriggerVfxDefinitionId);

            VfxDefinitionRegistry definitions = CharacterTestVfxDefinitionSource.CreateDefinitions();
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ArcMineDeployDefinitionId),
                out VfxDefinition deployDefinition));
            Assert.AreEqual(CharacterTestVfxIds.ArcMineDeployResourceKey, deployDefinition.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.ArcMineDeployLifetimeFrames, deployDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ArcMineTriggerDefinitionId),
                out VfxDefinition triggerDefinition));
            Assert.AreEqual(CharacterTestVfxIds.ArcMineTriggerResourceKey, triggerDefinition.PrefabResource);
            Assert.AreEqual(CharacterTestVfxIds.ArcMineTriggerLifetimeFrames, triggerDefinition.DefaultLifetimeFrames);
            ResourceCatalog resourceCatalog = CharacterTestVfxDefinitionSource.CreateResourceCatalog();
            AssertEntry(
                resourceCatalog,
                CharacterTestVfxIds.ArcMineDeployResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "Deploy");
            AssertEntry(
                resourceCatalog,
                CharacterTestVfxIds.ArcMineTriggerResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "Trigger");

            VfxTriggerMapResolver resolver = CharacterTestVfxDefinitionSource.CreateTriggerResolver();
            int trigger = MxFramework.Vfx.CombatBridge.CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.ArcMinePresentationProfileId);
            var output = new List<VfxSpawnRequest>();
            int count = resolver.Resolve(new VfxTriggerContext(
                RuntimeFrame.Zero,
                VfxSourceDomains.CombatHit,
                trigger,
                sourceId: 1,
                targetId: 2,
                sourceFrame: 30,
                localFrame: 30,
                sourceOrder: 0,
                VfxAnchor.Target(1, 2, VfxPose.Identity),
                VfxParameterBlock.Empty,
                traceId: new RuntimeTraceIdentity(930489)), output);
            Assert.AreEqual(1, count);
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.ArcMineTriggerDefinitionId), output[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.ArcMineTriggerLifetimeFrames, output[0].LifetimeFramesOverride);
        }

        [Test]
        public void MeteorBarrage_AuthoringData_BindsAbilityActionProjectileAndVfx()
        {
            AbilityDefinitionRegistry abilityRegistry = CharacterTestAbilityCatalogSource.CreateRegistry();
            Assert.IsTrue(abilityRegistry.TryGet(
                CharacterTestAbilityIds.MeteorBarrageAbilityId,
                out AbilityDefinitionConfig ability));
            Assert.AreEqual(CharacterTestAbilityIds.MeteorBarrageAbilityStableId, ability.AbilityStableId);
            Assert.AreEqual(CharacterTestAbilityIds.MeteorBarrageCooldownFrames, ability.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D406_Id2, ability.TargetPolicyId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id12, ability.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.MeteorBarragePresentationProfileId,
                ability.PresentationProfileId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.MeteorBarrageActionId,
                ability.ActionBinding.ActionId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.MeteorBarrageIntentId,
                ability.ActionBinding.ActionIntentId);

            CharacterTestCharacterActionRuntime actionRuntime =
                CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.MeteorBarrageActionId,
                out CharacterActionConfig action));
            Assert.AreEqual(90, action.DurationFrames);
            Assert.AreEqual(3, action.Phases.Length);
            Assert.AreEqual(0, action.Phases[0].StartFrame);
            Assert.AreEqual(23, action.Phases[0].EndFrame);
            Assert.AreEqual(24, action.Phases[1].StartFrame);
            Assert.AreEqual(60, action.Phases[1].EndFrame);
            Assert.AreEqual(61, action.Phases[2].StartFrame);
            Assert.AreEqual(89, action.Phases[2].EndFrame);

            GameplayTrackEvent telegraphEvent = FindGameplayRequestEvent(
                action,
                CharacterTestCharacterActionConfigSource.MeteorBarrageTelegraphGameplayRequestId);
            Assert.AreEqual(0, telegraphEvent.Frame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.MeteorBarrageTelegraphVfxStableEventId,
                telegraphEvent.StableEventId);
            int[] projectileRequestIds =
            {
                CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileRequestId0,
                CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileRequestId1,
                CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileRequestId2,
                CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileRequestId3,
                CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileRequestId4,
                CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileRequestId5
            };
            int[] projectileFrames = { 24, 30, 36, 42, 48, 54 };
            for (int i = 0; i < projectileRequestIds.Length; i++)
            {
                GameplayTrackEvent projectileEvent = FindGameplayRequestEvent(action, projectileRequestIds[i]);
                Assert.AreEqual(projectileFrames[i], projectileEvent.Frame);
                Assert.AreEqual(
                    CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileImpactStableEventId,
                    projectileEvent.StableEventId);
            }

            PresentationTrackEvent chargeVfxEvent = FindPresentationVfxEvent(
                action,
                CharacterTestVfxIds.MeteorBarrageChargeResourceKey.ToString());
            Assert.AreEqual(0, chargeVfxEvent.Frame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.MeteorBarrageChargeVfxStableEventId,
                chargeVfxEvent.StableEventId);

            CombatProjectileDefinitionRegistry projectileRegistry =
                CharacterTestFireballProjectileAdapter.CreateRegistry();
            Assert.IsTrue(projectileRegistry.TryGet(
                CharacterTestFireballProjectileAdapter.MeteorBarrageDefinitionId,
                out CombatProjectileDefinition projectile));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.MeteorBarrageStableId, projectile.StableId);
            Assert.AreEqual(CombatProjectileMotionKind.Linear, projectile.Motion.Kind);
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.MeteorBarrageSpeedPerFrame,
                projectile.Motion.SpeedPerFrame);
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.MeteorBarrageLifetimeFrames,
                projectile.Motion.LifetimeFrames);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.MeteorBarrageRadius, projectile.Collision.Radius);
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.MeteorBarrageCollisionLayerMask,
                projectile.Collision.LayerMask);
            Assert.IsTrue(projectile.Collision.LayerMask.ContainsLayer(
                CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer));
            Assert.IsTrue(projectile.Collision.LayerMask.ContainsLayer(
                CharacterTestFireballProjectileAdapter.MeteorBarrageStaticEnvironmentLayer));
            Assert.IsTrue(projectile.Collision.StopOnFirstAcceptedImpact);
            Assert.IsTrue(projectile.Collision.StopOnBlockedImpact);
            Assert.AreEqual(1, projectile.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(
                CombatProjectileHitSameTargetPolicy.OncePerProjectile,
                projectile.Collision.HitSameTargetPolicy);
            Assert.AreEqual(CombatProjectileImpactPolicyKind.AreaPulseOnImpact, projectile.Impact.PolicyKind);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.MeteorBarrageAreaRadius, projectile.Impact.AreaRadius);
            Assert.AreEqual(
                CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer),
                projectile.Impact.AreaLayerMask);
            Assert.IsTrue(projectile.Impact.TriggerOnExpire);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileImpactStableEventId,
                projectile.Impact.HitEvent.StableEventId);
            Assert.AreEqual(
                CombatHitTargetTypes.HostileCharacter
                | CombatHitTargetTypes.WorldGround
                | CombatHitTargetTypes.WorldWall
                | CombatHitTargetTypes.WorldStatic,
                projectile.Impact.HitEvent.EffectiveTargetTypes);
            Assert.AreEqual(
                CombatEnvironmentHitPolicy.BlockedPresentation,
                projectile.Impact.HitEvent.EnvironmentHitPolicy);
            Assert.AreEqual(
                CombatHitTargetTypes.HostileCharacter,
                projectile.Impact.AreaHitEvent.EffectiveTargetTypes);
            Assert.AreEqual(
                CombatEnvironmentHitPolicy.None,
                projectile.Impact.AreaHitEvent.EnvironmentHitPolicy);
            Assert.AreEqual(8, projectile.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(12, projectile.Impact.HitEvent.Damage.ImpactPower);

            CombatProjectileSpawnProfileRegistry spawnProfiles =
                CharacterTestFireballProjectileAdapter.CreateSpawnProfileRegistry();
            for (int i = 0; i < projectileRequestIds.Length; i++)
            {
                Assert.IsTrue(spawnProfiles.TryGet(
                    projectileRequestIds[i],
                    out CombatProjectileSpawnProfile spawnProfile));
                Assert.AreEqual(CharacterTestFireballProjectileAdapter.MeteorBarrageStableId, spawnProfile.ProjectileStableId);
                Assert.AreEqual(CombatProjectileAimKind.LockedTargetOrCasterForwardRange, spawnProfile.Aim.Kind);
                Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, spawnProfile.Aim.FallbackKind);
                Assert.AreEqual(CharacterTestFireballProjectileAdapter.MeteorBarrageRange, spawnProfile.Aim.MaxRange);
            }

            VfxDefinitionRegistry definitions = CharacterTestVfxDefinitionSource.CreateDefinitions();
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.MeteorBarrageChargeDefinitionId),
                out VfxDefinition chargeDefinition));
            Assert.AreEqual(CharacterTestVfxIds.MeteorBarrageChargeResourceKey, chargeDefinition.PrefabResource);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.MeteorBarrageTelegraphDefinitionId),
                out VfxDefinition telegraphDefinition));
            Assert.AreEqual(CharacterTestVfxIds.MeteorBarrageTelegraphResourceKey, telegraphDefinition.PrefabResource);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.MeteorBarrageImpactDefinitionId),
                out VfxDefinition impactDefinition));
            Assert.AreEqual(CharacterTestVfxIds.MeteorBarrageImpactResourceKey, impactDefinition.PrefabResource);

            VfxTriggerMapResolver resolver = CharacterTestVfxDefinitionSource.CreateTriggerResolver();
            int trigger = MxFramework.Vfx.CombatBridge.CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.MeteorBarragePresentationProfileId);
            var output = new List<VfxSpawnRequest>();
            int count = resolver.Resolve(new VfxTriggerContext(
                RuntimeFrame.Zero,
                VfxSourceDomains.CombatHit,
                trigger,
                sourceId: 1,
                targetId: 2,
                sourceFrame: 30,
                localFrame: 30,
                sourceOrder: 0,
                VfxAnchor.Target(1, 2, VfxPose.Identity),
                VfxParameterBlock.Empty,
                traceId: new RuntimeTraceIdentity(902981)), output);
            Assert.AreEqual(1, count);
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.MeteorBarrageImpactDefinitionId), output[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.MeteorBarrageImpactLifetimeFrames, output[0].LifetimeFramesOverride);

            CombatProjectileUnityPresentationRegistry presentationRegistry =
                CharacterTestProjectilePresentationSource.CreateRegistry();
            Assert.IsTrue(presentationRegistry.TryGetV2(
                CharacterTestFireballProjectileAdapter.MeteorBarrageDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 binding));
            Assert.AreEqual(CharacterTestVfxIds.MeteorBarrageFlightResourceKey, binding.FlightBodyResource);
            Assert.AreEqual(CharacterTestProjectilePresentationSource.FireballViewNamePrefix, binding.ViewNamePrefix);
        }

        [Test]
        public void MeteorBarrage_MemoryVfxResources_UseDedicatedRuntimeViews()
        {
            var fireballPrefab = new GameObject("TestFireballPlaceholder");
            ResourceManager manager = null;
            try
            {
                manager = new ResourceManager();
                manager.RegisterProvider(CharacterTestVfxDefinitionSource.CreateMemoryProvider(fireballPrefab));
                manager.AddCatalog(CharacterTestVfxDefinitionSource.CreateResourceCatalog());

                AssertMeteorBarrageVfxMode(
                    manager,
                    CharacterTestVfxIds.MeteorBarrageFlightResourceKey,
                    CharacterTestMeteorBarrageVfxMode.Flight);
                AssertMeteorBarrageVfxMode(
                    manager,
                    CharacterTestVfxIds.MeteorBarrageChargeResourceKey,
                    CharacterTestMeteorBarrageVfxMode.Charge);
                AssertMeteorBarrageVfxMode(
                    manager,
                    CharacterTestVfxIds.MeteorBarrageTelegraphResourceKey,
                    CharacterTestMeteorBarrageVfxMode.Telegraph);
                AssertMeteorBarrageVfxMode(
                    manager,
                    CharacterTestVfxIds.MeteorBarrageImpactResourceKey,
                    CharacterTestMeteorBarrageVfxMode.Impact);
            }
            finally
            {
                DestroyUnityObject(fireballPrefab);
            }
        }

        [Test]
        public void SwordWave_AuthoringData_BindsAbilityActionProjectileAndVfx()
        {
            AbilityDefinitionRegistry abilityRegistry = CharacterTestAbilityCatalogSource.CreateRegistry();
            Assert.IsTrue(abilityRegistry.TryGet(
                CharacterTestAbilityIds.SwordWaveAbilityId,
                out AbilityDefinitionConfig ability));
            Assert.AreEqual(CharacterTestAbilityIds.SwordWaveAbilityStableId, ability.AbilityStableId);
            Assert.AreEqual(CharacterTestAbilityIds.SwordWaveCooldownFrames, ability.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D406_Id9, ability.TargetPolicyId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id13, ability.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.SwordWavePresentationProfileId,
                ability.PresentationProfileId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.SwordWaveActionId,
                ability.ActionBinding.ActionId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.SwordWaveIntentId,
                ability.ActionBinding.ActionIntentId);

            CharacterTestCharacterActionRuntime actionRuntime =
                CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.SwordWaveActionId,
                out CharacterActionConfig action));
            Assert.AreEqual(155, action.DurationFrames);
            Assert.AreEqual(3, action.Phases.Length);
            Assert.AreEqual(12, action.Phases[0].EndFrame);
            Assert.AreEqual(13, action.Phases[1].StartFrame);
            Assert.AreEqual(133, action.Phases[1].EndFrame);
            Assert.AreEqual(134, action.Phases[2].StartFrame);
            Assert.AreEqual(154, action.Phases[2].EndFrame);
            int[] projectileFrames = new int[CharacterTestCharacterActionConfigSource.SwordWaveProjectileCount];
            int projectileEventCount = CopyGameplayRequestEventFrames(
                action,
                CharacterTestCharacterActionConfigSource.SwordWaveProjectileImpactStableEventId,
                CharacterTestCharacterActionConfigSource.SwordWaveProjectileRequestId,
                projectileFrames);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.SwordWaveProjectileCount, projectileEventCount);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.SwordWaveProjectileFirstFrame, projectileFrames[0]);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.SwordWaveProjectileFirstFrame
                + CharacterTestCharacterActionConfigSource.SwordWaveProjectileIntervalFrames,
                projectileFrames[1]);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.SwordWaveProjectileFirstFrame
                + CharacterTestCharacterActionConfigSource.SwordWaveProjectileIntervalFrames * 2,
                projectileFrames[2]);
            PresentationTrackEvent chargeVfxEvent = FindPresentationVfxEvent(
                action,
                CharacterTestVfxIds.SwordWaveChargeResourceKey.ToString());
            Assert.AreEqual(0, chargeVfxEvent.Frame);
            Assert.AreEqual(12, chargeVfxEvent.EndFrame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.SwordWaveChargeVfxStableEventId,
                chargeVfxEvent.StableEventId);

            CombatProjectileDefinitionRegistry projectileRegistry =
                CharacterTestFireballProjectileAdapter.CreateRegistry();
            Assert.IsTrue(projectileRegistry.TryGet(
                CharacterTestFireballProjectileAdapter.SwordWaveDefinitionId,
                out CombatProjectileDefinition projectile));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.SwordWaveStableId, projectile.StableId);
            Assert.AreEqual(CombatProjectileMotionKind.Linear, projectile.Motion.Kind);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.SwordWaveSpeedPerFrame, projectile.Motion.SpeedPerFrame);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.SwordWaveLifetimeFrames, projectile.Motion.LifetimeFrames);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.SwordWaveRadius, projectile.Collision.Radius);
            Assert.IsFalse(projectile.Collision.StopOnFirstAcceptedImpact);
            Assert.IsTrue(projectile.Collision.StopOnBlockedImpact);
            Assert.AreEqual(4, projectile.Collision.MaxAcceptedImpacts);
            Assert.AreEqual(
                CombatProjectileHitSameTargetPolicy.OncePerProjectile,
                projectile.Collision.HitSameTargetPolicy);
            Assert.IsTrue(projectile.Collision.LayerMask.ContainsLayer(
                CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer));
            Assert.IsTrue(projectile.Collision.LayerMask.ContainsLayer(
                CharacterTestFireballProjectileAdapter.MeteorBarrageStaticEnvironmentLayer));
            Assert.AreEqual(CombatProjectileImpactPolicyKind.DirectHit, projectile.Impact.PolicyKind);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.SwordWaveProjectileImpactStableEventId,
                projectile.Impact.HitEvent.StableEventId);
            Assert.AreEqual(
                CombatHitTargetTypes.HostileCharacter
                | CombatHitTargetTypes.WorldWall
                | CombatHitTargetTypes.WorldGround
                | CombatHitTargetTypes.WorldStatic,
                projectile.Impact.HitEvent.EffectiveTargetTypes);
            Assert.AreEqual(
                CombatEnvironmentHitPolicy.BlockedPresentation,
                projectile.Impact.HitEvent.EnvironmentHitPolicy);
            Assert.AreEqual(9, projectile.Impact.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.SwordWaveImpactPower,
                projectile.Impact.HitEvent.Damage.ImpactPower);

            CombatProjectileSpawnProfileRegistry spawnProfiles =
                CharacterTestFireballProjectileAdapter.CreateSpawnProfileRegistry();
            Assert.IsTrue(spawnProfiles.TryGet(
                CharacterTestCharacterActionConfigSource.SwordWaveProjectileRequestId,
                out CombatProjectileSpawnProfile spawnProfile));
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.SwordWaveStableId, spawnProfile.ProjectileStableId);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, spawnProfile.Aim.Kind);
            Assert.AreEqual(CombatProjectileAimKind.CasterForwardRange, spawnProfile.Aim.FallbackKind);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.SwordWaveRange, spawnProfile.Aim.MaxRange);

            VfxDefinitionRegistry definitions = CharacterTestVfxDefinitionSource.CreateDefinitions();
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.SwordWaveChargeDefinitionId),
                out VfxDefinition chargeDefinition));
            Assert.AreEqual(CharacterTestVfxIds.SwordWaveChargeResourceKey, chargeDefinition.PrefabResource);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.SwordWaveImpactDefinitionId),
                out VfxDefinition impactDefinition));
            Assert.AreEqual(CharacterTestVfxIds.SwordWaveImpactResourceKey, impactDefinition.PrefabResource);

            VfxTriggerMapResolver resolver = CharacterTestVfxDefinitionSource.CreateTriggerResolver();
            int trigger = MxFramework.Vfx.CombatBridge.CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.SwordWavePresentationProfileId);
            var output = new List<VfxSpawnRequest>();
            int count = resolver.Resolve(new VfxTriggerContext(
                RuntimeFrame.Zero,
                VfxSourceDomains.CombatHit,
                trigger,
                sourceId: 1,
                targetId: 2,
                sourceFrame: 13,
                localFrame: 13,
                sourceOrder: 0,
                VfxAnchor.Target(1, 2, VfxPose.Identity),
                VfxParameterBlock.Empty,
                traceId: new RuntimeTraceIdentity(972993)), output);
            Assert.AreEqual(1, count);
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.SwordWaveImpactDefinitionId), output[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.SwordWaveImpactLifetimeFrames, output[0].LifetimeFramesOverride);

            CombatProjectileUnityPresentationRegistry presentationRegistry =
                CharacterTestProjectilePresentationSource.CreateRegistry();
            Assert.IsTrue(presentationRegistry.TryGetV2(
                CharacterTestFireballProjectileAdapter.SwordWaveDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 binding));
            Assert.AreEqual(CharacterTestVfxIds.SwordWaveFlightResourceKey, binding.FlightBodyResource);
            Assert.AreEqual(CharacterTestProjectilePresentationSource.FireballViewNamePrefix, binding.ViewNamePrefix);
        }

        [Test]
        public void SwordWave_MemoryVfxResources_UseDedicatedRuntimeViews()
        {
            var fireballPrefab = new GameObject("TestFireballPlaceholder");
            ResourceManager manager = null;
            try
            {
                manager = new ResourceManager();
                manager.RegisterProvider(CharacterTestVfxDefinitionSource.CreateMemoryProvider(fireballPrefab));
                manager.AddCatalog(CharacterTestVfxDefinitionSource.CreateResourceCatalog());

                AssertSwordWaveVfxMode(
                    manager,
                    CharacterTestVfxIds.SwordWaveFlightResourceKey,
                    CharacterTestSwordWaveVfxMode.Flight);
                AssertSwordWaveVfxMode(
                    manager,
                    CharacterTestVfxIds.SwordWaveChargeResourceKey,
                    CharacterTestSwordWaveVfxMode.Charge);
                AssertSwordWaveVfxMode(
                    manager,
                    CharacterTestVfxIds.SwordWaveImpactResourceKey,
                    CharacterTestSwordWaveVfxMode.Impact);
            }
            finally
            {
                DestroyUnityObject(fireballPrefab);
            }
        }

        [Test]
        public void SwordWaveFlightVfx_UsesProjectileLocalFrameForRollVariants()
        {
            GameObject prefab = CharacterTestSwordWaveVfxView.CreateRuntimePrefab(
                CharacterTestSwordWaveVfxMode.Flight);
            try
            {
                CharacterTestSwordWaveVfxView view =
                    prefab.GetComponent<CharacterTestSwordWaveVfxView>();
                Assert.IsNotNull(view);

                view.OnCombatProjectilePresentationUpdated(CreateProjectilePresentationUpdate(
                    CharacterTestCharacterActionConfigSource.SwordWaveProjectileFirstFrame));
                Assert.AreEqual(0f, view.FlightRollDegrees, 0.001f);
                Assert.AreEqual(
                    CharacterTestFireballProjectileAdapter.ResolveSwordWaveRollDegrees(
                        CharacterTestCharacterActionConfigSource.SwordWaveProjectileFirstFrame),
                    view.FlightRollDegrees,
                    0.001f);

                view.OnCombatProjectilePresentationUpdated(CreateProjectilePresentationUpdate(
                    CharacterTestCharacterActionConfigSource.SwordWaveProjectileFirstFrame
                    + CharacterTestCharacterActionConfigSource.SwordWaveProjectileIntervalFrames));
                Assert.AreEqual(-55f, view.FlightRollDegrees, 0.001f);

                view.OnCombatProjectilePresentationUpdated(CreateProjectilePresentationUpdate(
                    CharacterTestCharacterActionConfigSource.SwordWaveProjectileFirstFrame
                    + CharacterTestCharacterActionConfigSource.SwordWaveProjectileIntervalFrames * 2));
                Assert.AreEqual(55f, view.FlightRollDegrees, 0.001f);
            }
            finally
            {
                DestroyUnityObject(prefab);
            }
        }

        [Test]
        public void SwordWaveDirectSweepProvider_UsesVisualRollForFiveOffsetCapsules()
        {
            CombatProjectileDefinitionRegistry registry = CharacterTestFireballProjectileAdapter.CreateRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestFireballProjectileAdapter.SwordWaveDefinitionId,
                out CombatProjectileDefinition definition));

            var queries = new List<CombatCapsuleQuery>(CharacterTestFireballProjectileAdapter.SwordWaveCollisionSampleCount);
            var header = new CombatQueryHeader(
                queryId: 1,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: new CombatEntityId(1),
                traceId: 30,
                actionId: 10,
                sourceOrder: 0,
                layerMask: definition.Collision.LayerMask);
            FixVector3 oldPosition = FixVector3.Zero;
            FixVector3 newPosition = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

            CharacterTestSwordWaveDirectSweepProvider.Instance.CollectDirectSweepQueries(
                new CombatFrame(1),
                CreateSwordWaveProjectileSnapshot(CharacterTestCharacterActionConfigSource.SwordWaveProjectileFirstFrame),
                definition,
                header,
                oldPosition,
                newPosition,
                queries);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.SwordWaveCollisionSampleCount, queries.Count);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.SwordWaveRadius, queries[0].Radius);
            Assert.AreEqual(oldPosition, queries[2].PointA);
            Assert.Less(queries[0].PointA.X.RawValue, Fix64.Zero.RawValue);
            Assert.Greater(queries[4].PointA.X.RawValue, Fix64.Zero.RawValue);

            queries.Clear();
            CharacterTestSwordWaveDirectSweepProvider.Instance.CollectDirectSweepQueries(
                new CombatFrame(1),
                CreateSwordWaveProjectileSnapshot(
                    CharacterTestCharacterActionConfigSource.SwordWaveProjectileFirstFrame
                    + CharacterTestCharacterActionConfigSource.SwordWaveProjectileIntervalFrames),
                definition,
                header,
                oldPosition,
                newPosition,
                queries);
            Assert.Greater(queries[0].PointA.Y.RawValue, Fix64.Zero.RawValue);
            Assert.Less(queries[4].PointA.Y.RawValue, Fix64.Zero.RawValue);

            queries.Clear();
            CharacterTestSwordWaveDirectSweepProvider.Instance.CollectDirectSweepQueries(
                new CombatFrame(1),
                CreateSwordWaveProjectileSnapshot(
                    CharacterTestCharacterActionConfigSource.SwordWaveProjectileFirstFrame
                    + CharacterTestCharacterActionConfigSource.SwordWaveProjectileIntervalFrames * 2),
                definition,
                header,
                oldPosition,
                newPosition,
                queries);
            Assert.Less(queries[0].PointA.Y.RawValue, Fix64.Zero.RawValue);
            Assert.Greater(queries[4].PointA.Y.RawValue, Fix64.Zero.RawValue);
        }

        [Test]
        public void SwordWaveDirectSweepProvider_MergesRuntimeStaticObstacleMask()
        {
            CombatProjectileDefinitionRegistry registry = CharacterTestFireballProjectileAdapter.CreateRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestFireballProjectileAdapter.SwordWaveDefinitionId,
                out CombatProjectileDefinition definition));
            var physicsRuntime = new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(7),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.Zero,
                Fix64.Zero,
                staticColliderCount: 1,
                "sword wave static mask test");
            var provider = new CharacterTestSwordWaveDirectSweepProvider(physicsRuntime);
            var queries = new List<CombatCapsuleQuery>(
                CharacterTestFireballProjectileAdapter.SwordWaveCollisionSampleCount);
            var header = new CombatQueryHeader(
                queryId: 1,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: new CombatEntityId(1),
                traceId: 30,
                actionId: 10,
                sourceOrder: 0,
                layerMask: CombatPhysicsLayerMask.FromLayer(
                    CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer));

            int count = provider.CollectDirectSweepQueries(
                new CombatFrame(1),
                CreateSwordWaveProjectileSnapshot(CharacterTestCharacterActionConfigSource.SwordWaveProjectileFirstFrame),
                definition,
                header,
                FixVector3.Zero,
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                queries);

            Assert.AreEqual(CharacterTestFireballProjectileAdapter.SwordWaveCollisionSampleCount, count);
            for (int i = 0; i < queries.Count; i++)
            {
                Assert.IsTrue(queries[i].Header.LayerMask.ContainsLayer(
                    CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer));
                Assert.IsTrue(queries[i].Header.LayerMask.ContainsLayer(7));
            }
        }

        [Test]
        public void MeteorBarrage_RebuildVisuals_RemovesLegacyLineChildren()
        {
            GameObject flightPrefab = CharacterTestMeteorBarrageVfxView.CreateRuntimePrefab(
                CharacterTestMeteorBarrageVfxMode.Flight);
            GameObject telegraphPrefab = CharacterTestMeteorBarrageVfxView.CreateRuntimePrefab(
                CharacterTestMeteorBarrageVfxMode.Telegraph);
            GameObject impactPrefab = CharacterTestMeteorBarrageVfxView.CreateRuntimePrefab(
                CharacterTestMeteorBarrageVfxMode.Impact);
            try
            {
                CreateLegacyLineChild(flightPrefab.transform.Find("FallingTrail"), "FireTrail03");
                CreateLegacyLineChild(telegraphPrefab.transform.Find("MeteorTelegraph"), "WarningRay00");
                CreateLegacyLineChild(impactPrefab.transform.Find("MeteorImpact"), "ImpactRay00");

                flightPrefab.GetComponent<CharacterTestMeteorBarrageVfxView>().RebuildVisuals();
                telegraphPrefab.GetComponent<CharacterTestMeteorBarrageVfxView>().RebuildVisuals();
                impactPrefab.GetComponent<CharacterTestMeteorBarrageVfxView>().RebuildVisuals();

                Assert.AreEqual(3, CountChildrenWithNamePrefix(flightPrefab.transform, "FireTrail"));
                Assert.AreEqual(0, CountChildrenWithNamePrefix(telegraphPrefab.transform, "WarningRay"));
                Assert.AreEqual(0, CountChildrenWithNamePrefix(impactPrefab.transform, "ImpactRay"));
            }
            finally
            {
                DestroyUnityObject(flightPrefab);
                DestroyUnityObject(telegraphPrefab);
                DestroyUnityObject(impactPrefab);
            }
        }

        [Test]
        public void MeteorBarrage_DisableRuntimePrefab_KeepsRendererMaterials()
        {
            GameObject flightPrefab = CharacterTestMeteorBarrageVfxView.CreateRuntimePrefab(
                CharacterTestMeteorBarrageVfxMode.Flight);
            GameObject chargePrefab = CharacterTestMeteorBarrageVfxView.CreateRuntimePrefab(
                CharacterTestMeteorBarrageVfxMode.Charge);
            GameObject telegraphPrefab = CharacterTestMeteorBarrageVfxView.CreateRuntimePrefab(
                CharacterTestMeteorBarrageVfxMode.Telegraph);
            GameObject impactPrefab = CharacterTestMeteorBarrageVfxView.CreateRuntimePrefab(
                CharacterTestMeteorBarrageVfxMode.Impact);
            try
            {
                AssertMeteorBarrageRendererMaterials(flightPrefab);
                AssertMeteorBarrageRendererMaterials(chargePrefab);
                AssertMeteorBarrageRendererMaterials(telegraphPrefab);
                AssertMeteorBarrageRendererMaterials(impactPrefab);

                flightPrefab.SetActive(true);
                chargePrefab.SetActive(true);
                telegraphPrefab.SetActive(true);
                impactPrefab.SetActive(true);
                AssertMeteorBarrageRendererMaterials(flightPrefab);
                AssertMeteorBarrageRendererMaterials(chargePrefab);
                AssertMeteorBarrageRendererMaterials(telegraphPrefab);
                AssertMeteorBarrageRendererMaterials(impactPrefab);

                flightPrefab.SetActive(false);
                chargePrefab.SetActive(false);
                telegraphPrefab.SetActive(false);
                impactPrefab.SetActive(false);
                AssertMeteorBarrageRendererMaterials(flightPrefab);
                AssertMeteorBarrageRendererMaterials(chargePrefab);
                AssertMeteorBarrageRendererMaterials(telegraphPrefab);
                AssertMeteorBarrageRendererMaterials(impactPrefab);
            }
            finally
            {
                DestroyUnityObject(flightPrefab);
                DestroyUnityObject(chargePrefab);
                DestroyUnityObject(telegraphPrefab);
                DestroyUnityObject(impactPrefab);
            }
        }

        [Test]
        public void AreaConfinement_AuthoringData_BindsAbilityActionAndRootBuff()
        {
            AbilityDefinitionRegistry abilityRegistry = CharacterTestAbilityCatalogSource.CreateRegistry();
            Assert.IsTrue(abilityRegistry.TryGet(
                CharacterTestAbilityIds.AreaConfinementAbilityId,
                out AbilityDefinitionConfig ability));
            Assert.AreEqual(
                CharacterTestAbilityIds.AreaConfinementAbilityStableId,
                ability.AbilityStableId);
            Assert.AreEqual(
                CharacterTestAbilityIds.AreaConfinementCooldownFrames,
                ability.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D406_Id8, ability.TargetPolicyId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.AreaConfinementActionId,
                ability.ActionBinding.ActionId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.AreaConfinementIntentId,
                ability.ActionBinding.ActionIntentId);

            CharacterTestCharacterActionRuntime actionRuntime =
                CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(actionRuntime.IsReady, actionRuntime.ConfigSourceDescription);
            Assert.AreEqual(0, actionRuntime.ImportResult.Diagnostics.Length);
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.AreaConfinementActionId,
                out CharacterActionConfig action));
            Assert.AreEqual(
                CharacterTestAreaConfinementTuningSource.LoadDefault().ActionDurationFrames,
                action.DurationFrames);
            Assert.AreEqual(3, action.Phases.Length);
            Assert.AreEqual(5, action.Phases[0].EndFrame);
            Assert.AreEqual(6, action.Phases[1].StartFrame);
            Assert.AreEqual(65, action.Phases[1].EndFrame);
            Assert.AreEqual(66, action.Phases[2].StartFrame);
            Assert.AreEqual(71, action.Phases[2].EndFrame);
            GameplayTrackEvent areaEvent = FindGameplayRequestEvent(
                action,
                CharacterTestCharacterActionConfigSource.AreaConfinementGameplayRequestId);
            Assert.AreEqual(6, areaEvent.Frame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.AreaConfinementStableEventId,
                areaEvent.StableEventId);
            Assert.AreEqual(3, action.MotionTrack.Events.Length);
            Assert.AreEqual(0, action.MotionTrack.Events[0].Frame);
            Assert.AreEqual(11, action.MotionTrack.Events[0].EndFrame);
            Assert.AreEqual(CharacterMovementMode.ControlLocked, action.MotionTrack.Events[0].MovementMode);
            Assert.AreEqual(0f, action.MotionTrack.Events[0].MovementControlScale);
            Assert.AreEqual(0f, action.MotionTrack.Events[0].RotationControlScale);
            Assert.AreEqual(12, action.MotionTrack.Events[1].Frame);
            Assert.AreEqual(65, action.MotionTrack.Events[1].EndFrame);
            Assert.AreEqual(CharacterMovementMode.ControlLocked, action.MotionTrack.Events[1].MovementMode);
            Assert.AreEqual(0f, action.MotionTrack.Events[1].MovementControlScale);
            Assert.AreEqual(1f, action.MotionTrack.Events[1].RotationControlScale);
            Assert.AreEqual(66, action.MotionTrack.Events[2].Frame);
            Assert.AreEqual(71, action.MotionTrack.Events[2].EndFrame);
            Assert.AreEqual(CharacterMovementMode.Run, action.MotionTrack.Events[2].MovementMode);
            Assert.AreEqual(1f, action.MotionTrack.Events[2].MovementControlScale);
            Assert.AreEqual(1f, action.MotionTrack.Events[2].RotationControlScale);

            GameplayBuffDefinitionRegistry buffRegistry =
                CharacterTestGuardAuraRuntime.CreateBuffDefinitions();
            Assert.IsTrue(buffRegistry.TryGet(
                CharacterTestAreaConfinementIds.RootedBuffId,
                out GameplayBuffDefinition buff));
            Assert.AreEqual(CharacterTestAreaConfinementIds.RootedBuffDurationFrames, buff.DefaultDurationFrames);
            GameplayBuffEffectDefinition[] effects = buff.GetEffects();
            Assert.AreEqual(1, effects.Length);
            Assert.AreEqual(GameplayBuffEffectKind.Status, effects[0].Kind);
            Assert.AreEqual(
                new GameplayStatusId(CharacterTestAreaConfinementIds.AreaConfinedStatusId),
                effects[0].StatusId);
        }

        [Test]
        public void CastAbilitySlotCommand_ValidatesFixedPayloadSchema()
        {
            var validator = new RuntimeCommandRegistryValidator(CharacterTestRuntimeCommandRegistry.Create());
            RuntimeCommand valid = CharacterTestRuntimeCommandFactory.CastAbilitySlot(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.PlayerInput,
                targetEntityId: 7,
                slotNumber: 3,
                abilityId: CharacterTestAbilityIds.FlameLanceAbilityId,
                traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidSlot = CharacterTestRuntimeCommandFactory.CastAbilitySlot(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.PlayerInput,
                targetEntityId: 7,
                slotNumber: 10,
                abilityId: CharacterTestAbilityIds.FlameLanceAbilityId,
                traceId: new RuntimeTraceIdentity(997818));

            RuntimeCommandValidationResult accepted = validator.Validate(valid);
            RuntimeCommandValidationResult rejected = validator.Validate(invalidSlot);
            CharacterTestAbilitySlotCastRequest request =
                CharacterTestRuntimeCommandFactory.DecodeAbilitySlotCast(valid);

            Assert.IsTrue(accepted.Success, accepted.Error.Message);
            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(7, request.TargetEntityId);
            Assert.AreEqual(3, request.SlotNumber);
            Assert.AreEqual(CharacterTestAbilityIds.FlameLanceAbilityId, request.AbilityId);
            Assert.AreEqual(CharacterTestRuntimeCommandFactory.AbilitySlotCastSchemaVersion, request.SchemaVersion);
        }

        [Test]
        public void SurfaceProbeCommand_ValidatesFixedPayloadSchema()
        {
            var validator = new RuntimeCommandRegistryValidator(CharacterTestRuntimeCommandRegistry.Create());
            RuntimeCommand valid = CharacterTestRuntimeCommandFactory.SurfaceProbe(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.SurfaceProbePanel,
                targetEntityId: 7,
                operation: CharacterTestSurfaceProbeOperation.ApplyPush,
                traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidOperation = CharacterTestRuntimeCommandFactory.SurfaceProbe(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.SurfaceProbePanel,
                targetEntityId: 7,
                operation: CharacterTestSurfaceProbeOperation.None,
                traceId: new RuntimeTraceIdentity(997818));

            RuntimeCommandValidationResult accepted = validator.Validate(valid);
            RuntimeCommandValidationResult rejected = validator.Validate(invalidOperation);
            CharacterTestSurfaceProbeRequest request =
                CharacterTestRuntimeCommandFactory.DecodeSurfaceProbe(valid);

            Assert.IsTrue(accepted.Success, accepted.Error.Message);
            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(7, request.TargetEntityId);
            Assert.AreEqual(CharacterTestSurfaceProbeOperation.ApplyPush, request.Operation);
            Assert.AreEqual(CharacterTestRuntimeCommandFactory.SurfaceProbeSchemaVersion, request.SchemaVersion);
        }

        [Test]
        public void GenesisMovableObjectProbeCommand_ValidatesFixedPayloadSchema()
        {
            var validator = new RuntimeCommandRegistryValidator(CharacterTestRuntimeCommandRegistry.Create());
            RuntimeCommand valid = CharacterTestRuntimeCommandFactory.GenesisMovableObjectProbe(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.GenesisProbe,
                CharacterTestGenesisMovableObjectProbeKind.ValidationSuite,
                CharacterTestGenesisForceOriginKind.InstantImpulse,
                traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidScenario = CharacterTestRuntimeCommandFactory.GenesisMovableObjectProbe(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.GenesisProbe,
                CharacterTestGenesisMovableObjectProbeKind.None,
                CharacterTestGenesisForceOriginKind.InstantImpulse,
                traceId: new RuntimeTraceIdentity(997818));

            RuntimeCommandValidationResult accepted = validator.Validate(valid);
            RuntimeCommandValidationResult rejected = validator.Validate(invalidScenario);
            CharacterTestGenesisMovableObjectProbeRequest request =
                CharacterTestRuntimeCommandFactory.DecodeGenesisMovableObjectProbe(valid);

            Assert.IsTrue(accepted.Success, accepted.Error.Message);
            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(CharacterTestGenesisMovableObjectProbeKind.ValidationSuite, request.Scenario);
            Assert.AreEqual(CharacterTestGenesisForceOriginKind.InstantImpulse, request.ForceOrigin);
            Assert.AreEqual(
                CharacterTestRuntimeCommandFactory.GenesisMovableObjectProbeSchemaVersion,
                request.SchemaVersion);
        }

        [Test]
        public void GenesisPotencyMechanicsProbeCommand_ValidatesFixedPayloadSchema()
        {
            var validator = new RuntimeCommandRegistryValidator(CharacterTestRuntimeCommandRegistry.Create());
            RuntimeCommand valid = CharacterTestRuntimeCommandFactory.GenesisPotencyMechanicsProbe(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.GenesisProbe,
                CharacterTestGenesisPotencyMechanicsProbeKind.ValidationSuite,
                traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidScenario = CharacterTestRuntimeCommandFactory.GenesisPotencyMechanicsProbe(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.GenesisProbe,
                CharacterTestGenesisPotencyMechanicsProbeKind.None,
                traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidSchema = new RuntimeCommand(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.GenesisProbe,
                CharacterTestRuntimeCommandIds.GenesisPotencyMechanicsProbe,
                targetId: 0,
                payload0: (int)CharacterTestGenesisPotencyMechanicsProbeKind.ValidationSuite,
                payload1: 0,
                payload2: CharacterTestRuntimeCommandFactory.GenesisPotencyMechanicsProbeSchemaVersion + 1,
                traceId: new RuntimeTraceIdentity(997818));

            RuntimeCommandValidationResult accepted = validator.Validate(valid);
            RuntimeCommandValidationResult rejectedScenario = validator.Validate(invalidScenario);
            RuntimeCommandValidationResult rejectedSchema = validator.Validate(invalidSchema);
            CharacterTestGenesisPotencyMechanicsProbeRequest request =
                CharacterTestRuntimeCommandFactory.DecodeGenesisPotencyMechanicsProbe(valid);

            Assert.IsTrue(accepted.Success, accepted.Error.Message);
            Assert.IsFalse(rejectedScenario.Success);
            Assert.IsFalse(rejectedSchema.Success);
            Assert.AreEqual(CharacterTestGenesisPotencyMechanicsProbeKind.ValidationSuite, request.Scenario);
            Assert.AreEqual(
                CharacterTestRuntimeCommandFactory.GenesisPotencyMechanicsProbeSchemaVersion,
                request.SchemaVersion);
        }

        [Test]
        public void GenesisLivingBeingProbeCommand_ValidatesFixedPayloadSchema()
        {
            var validator = new RuntimeCommandRegistryValidator(CharacterTestRuntimeCommandRegistry.Create());
            RuntimeCommand valid = CharacterTestRuntimeCommandFactory.GenesisLivingBeingProbe(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.GenesisProbe,
                CharacterTestGenesisLivingBeingProbeKind.ValidationSuite,
                traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidScenario = CharacterTestRuntimeCommandFactory.GenesisLivingBeingProbe(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.GenesisProbe,
                CharacterTestGenesisLivingBeingProbeKind.None,
                traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidSchema = new RuntimeCommand(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.GenesisProbe,
                CharacterTestRuntimeCommandIds.GenesisLivingBeingProbe,
                targetId: 0,
                payload0: (int)CharacterTestGenesisLivingBeingProbeKind.ValidationSuite,
                payload1: 0,
                payload2: CharacterTestRuntimeCommandFactory.GenesisLivingBeingProbeSchemaVersion + 1,
                traceId: new RuntimeTraceIdentity(997818));

            RuntimeCommandValidationResult accepted = validator.Validate(valid);
            RuntimeCommandValidationResult rejectedScenario = validator.Validate(invalidScenario);
            RuntimeCommandValidationResult rejectedSchema = validator.Validate(invalidSchema);
            CharacterTestGenesisLivingBeingProbeRequest request =
                CharacterTestRuntimeCommandFactory.DecodeGenesisLivingBeingProbe(valid);

            Assert.IsTrue(accepted.Success, accepted.Error.Message);
            Assert.IsFalse(rejectedScenario.Success);
            Assert.IsFalse(rejectedSchema.Success);
            Assert.AreEqual(CharacterTestGenesisLivingBeingProbeKind.ValidationSuite, request.Scenario);
            Assert.AreEqual(
                CharacterTestRuntimeCommandFactory.GenesisLivingBeingProbeSchemaVersion,
                request.SchemaVersion);
        }

        [Test]
        public void GenesisLivingNaturalAttributesProbeCommand_ValidatesFixedPayloadSchema()
        {
            var validator = new RuntimeCommandRegistryValidator(CharacterTestRuntimeCommandRegistry.Create());
            RuntimeCommand valid = CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesProbe(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.GenesisProbe,
                CharacterTestGenesisLivingNaturalAttributesProbeKind.ValidationSuite,
                traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidScenario = CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesProbe(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.GenesisProbe,
                CharacterTestGenesisLivingNaturalAttributesProbeKind.None,
                traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidSchema = new RuntimeCommand(
                new RuntimeFrame(12),
                CharacterTestRuntimeCommandSources.GenesisProbe,
                CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesProbe,
                targetId: 0,
                payload0: (int)CharacterTestGenesisLivingNaturalAttributesProbeKind.ValidationSuite,
                payload1: 0,
                payload2: CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesProbeSchemaVersion + 1,
                traceId: new RuntimeTraceIdentity(997818));

            RuntimeCommandValidationResult accepted = validator.Validate(valid);
            RuntimeCommandValidationResult rejectedScenario = validator.Validate(invalidScenario);
            RuntimeCommandValidationResult rejectedSchema = validator.Validate(invalidSchema);
            CharacterTestGenesisLivingNaturalAttributesProbeRequest request =
                CharacterTestRuntimeCommandFactory.DecodeGenesisLivingNaturalAttributesProbe(valid);

            Assert.IsTrue(accepted.Success, accepted.Error.Message);
            Assert.IsFalse(rejectedScenario.Success);
            Assert.IsFalse(rejectedSchema.Success);
            Assert.AreEqual(CharacterTestGenesisLivingNaturalAttributesProbeKind.ValidationSuite, request.Scenario);
            Assert.AreEqual(
                CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesProbeSchemaVersion,
                request.SchemaVersion);
        }

        [Test]
        public void CharacterActionSystem_AbilitySlotThreeStartsFlameLance()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity attacker);
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime);
            Assert.IsTrue(abilityRuntime.Definitions.TryGet(
                CharacterTestAbilityIds.FlameLanceAbilityId,
                out AbilityDefinitionConfig flameLance));
            abilityRuntime.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PreSimulation));
            attacker.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.zero,
                jumpPressed: false,
                abilitySlotPressed: 3,
                abilityIdPressed: CharacterTestAbilityIds.FlameLanceAbilityId));
            attacker.GameplayIntentBuffer.AddOrRefresh(
                CharacterTestGameplayIntentKind.AbilitySlotCast,
                attacker.Id.Value,
                RuntimeFrame.Zero.Value,
                abilitySlotPressed: 3,
                abilityIdPressed: CharacterTestAbilityIds.FlameLanceAbilityId);

            system.Tick(world, RuntimeFrame.Zero);

            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FlameLanceActionId,
                attacker.Action.ActiveActionId);
            Assert.AreEqual(
                CharacterTestAbilityIds.FlameLanceCooldownFrames,
                abilityRuntime.GetRemainingCooldownFrames(attacker.Id.Value, flameLance, RuntimeFrame.Zero));
            Assert.IsFalse(attacker.Input.HasAbilitySlotCast);
        }

        [Test]
        public void CharacterActionSystem_AbilitySlotEightStartsGuardAura()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity attacker);
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime);
            Assert.IsTrue(abilityRuntime.Definitions.TryGet(
                CharacterTestAbilityIds.GuardAuraAbilityId,
                out AbilityDefinitionConfig guardAura));
            abilityRuntime.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PreSimulation));
            attacker.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.zero,
                jumpPressed: false,
                abilitySlotPressed: 8,
                abilityIdPressed: CharacterTestAbilityIds.GuardAuraAbilityId));
            attacker.GameplayIntentBuffer.AddOrRefresh(
                CharacterTestGameplayIntentKind.AbilitySlotCast,
                attacker.Id.Value,
                RuntimeFrame.Zero.Value,
                abilitySlotPressed: 8,
                abilityIdPressed: CharacterTestAbilityIds.GuardAuraAbilityId);

            system.Tick(world, RuntimeFrame.Zero);

            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.GuardAuraActionId,
                attacker.Action.ActiveActionId);
            Assert.AreEqual(
                CharacterTestAbilityIds.GuardAuraCooldownFrames,
                abilityRuntime.GetRemainingCooldownFrames(attacker.Id.Value, guardAura, RuntimeFrame.Zero));
            Assert.IsFalse(attacker.Input.HasAbilitySlotCast);
        }

        [Test]
        public void CombatTraceSystem_GuardAuraRequestCreatesAuraSourceAndRefreshesBuff()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity caster);
            var physicsWorld = new CombatPhysicsWorld();
            RegisterPhysicsBody(physicsWorld, caster);
            var physicsRuntime = new CharacterTestCharacterPhysicsRuntime(
                physicsWorld,
                CombatPhysicsLayerMask.None,
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.Zero,
                Fix64.Zero,
                staticColliderCount: 0,
                sourceDescription: "test");
            var system = new CharacterTestCharacterCombatTraceSystem(NullRuntimeLogger.Instance);
            system.SetPhysicsRuntime(physicsRuntime);
            caster.EnqueueGameplayRequest(new CharacterActionGameplayRequest(
                new CharacterActionAdapterDispatchMetadata(
                    new RuntimeFrame(6),
                    instanceId: 1,
                    planId: 1,
                    actionId: CharacterTestCharacterActionConfigSource.GuardAuraActionId,
                    localFrame: 6,
                    stableEventId: CharacterTestCharacterActionConfigSource.GuardAuraStableEventId,
                    sourceId: caster.Id.Value,
                    traceId: new RuntimeTraceIdentity(933015)),
                caster.GameplayEntityId,
                CharacterActionTrackEventKind.SendGameplayRequest,
                CharacterTestCharacterActionConfigSource.GuardAuraGameplayRequestId,
                CharacterTestAbilityIds.GuardAuraAbilityId));

            system.Tick(world, null, new RuntimeFrame(6));

            Assert.IsTrue(world.GameplayComponentWorld.TryGetStore(
                out GameplayComponentStore<GameplayComponentAuraSourceSetComponent> auraStore));
            Assert.IsTrue(auraStore.TryGet(caster.GameplayEntityId, out GameplayComponentAuraSourceSetComponent sources));
            Assert.IsTrue(sources.TryGet(CharacterTestGuardAuraIds.AuraId, out GameplayComponentAuraSourceEntry source));
            Assert.AreEqual(306, source.EndFrame);
            Assert.AreEqual(CharacterTestAbilityIds.GuardAuraAbilityId, source.SourceId);
            Assert.IsTrue(world.GameplayComponentWorld.TryGetStore(
                out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore));
            Assert.IsTrue(buffStore.TryGet(caster.GameplayEntityId, out GameplayComponentBuffSetComponent buffs));
            Assert.IsTrue(buffs.TryGet(CharacterTestGuardAuraIds.BuffId, out GameplayComponentBuffEntry buff));
            Assert.AreEqual(8, buff.EndFrame);
            Assert.AreEqual(CharacterTestGuardAuraIds.AuraId, buff.SourceId);
            Assert.AreEqual(1, system.GuardAuraLastTickSummary.AppliedOrRefreshedCount);
        }

        [Test]
        public void CombatTraceSystem_PhysicalShieldRequestCreatesShieldRuntimeAndCollider()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity caster);
            var physicsWorld = new CombatPhysicsWorld();
            RegisterPhysicsBody(physicsWorld, caster);
            var physicsRuntime = new CharacterTestCharacterPhysicsRuntime(
                physicsWorld,
                CombatPhysicsLayerMask.None,
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.Zero,
                Fix64.Zero,
                staticColliderCount: 0,
                sourceDescription: "test");
            var system = new CharacterTestCharacterCombatTraceSystem(NullRuntimeLogger.Instance);
            system.SetPhysicsRuntime(physicsRuntime);
            caster.EnqueueGameplayRequest(new CharacterActionGameplayRequest(
                new CharacterActionAdapterDispatchMetadata(
                    new RuntimeFrame(6),
                    instanceId: 1,
                    planId: 1,
                    actionId: CharacterTestCharacterActionConfigSource.PhysicalShieldActionId,
                    localFrame: 6,
                    stableEventId: CharacterTestCharacterActionConfigSource.PhysicalShieldStableEventId,
                    sourceId: caster.Id.Value,
                    traceId: new RuntimeTraceIdentity(915062)),
                caster.GameplayEntityId,
                CharacterActionTrackEventKind.SendGameplayRequest,
                CharacterTestCharacterActionConfigSource.PhysicalShieldGameplayRequestId,
                CharacterTestAbilityIds.PhysicalShieldAbilityId));

            system.Tick(world, null, new RuntimeFrame(6));

            Assert.AreEqual(1, system.ActivePhysicalShieldCount);
            Assert.AreEqual(1, system.PhysicalShieldLastTickSummary.CreatedOrRefreshedCount);
            Assert.AreEqual(1, system.PhysicalShieldLastTickSummary.ActiveCount);
            Assert.NotNull(system.PhysicalShieldRuntime);
            var shieldEntityId = new CombatEntityId(
                CharacterTestPhysicalShieldRuntime.ShieldEntityIdStart + caster.Id.Value);
            Assert.IsTrue(system.PhysicalShieldRuntime.TryGetActiveShield(
                shieldEntityId,
                out CombatShieldPotencyComponent shield));
            Assert.AreEqual(physicsRuntime.GetEntityId(caster), shield.OwnerEntityId);
            Assert.AreEqual(64, shield.CurrentPotency.Cohesion);
            Assert.AreEqual(CharacterTestPhysicalShieldRuntime.BaseCohesionLimit, shield.CurrentCohesionLimit);
            Assert.AreEqual(CombatShieldPressureBand.Stable, shield.CurrentBand);

            Assert.IsTrue(physicsWorld.TryGetAabbCollider(
                new CombatBodyId(CharacterTestPhysicalShieldRuntime.ShieldBodyIdStart + caster.Id.Value),
                new CombatColliderId(CharacterTestPhysicalShieldRuntime.ShieldColliderId),
                out CombatPhysicsAabbCollider collider));
            Assert.AreEqual(CharacterTestPhysicalShieldRuntime.ShieldLayer, collider.Layer);
        }

        [Test]
        public void GuardAuraRuntime_ApplyOnHitBuffsIdleDoesNotAllocateAfterWarmup()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out _);
            var physicsRuntime = CreatePhysicsRuntime(new CombatPhysicsWorld());
            var runtime = new CharacterTestGuardAuraRuntime();
            HitResolveResult[] emptyResults = Array.Empty<HitResolveResult>();
            Func<HitResolveResult, CombatHitBuffApplicationConfig[]> emptyBuffs =
                GetNoOnHitBuffs;

            for (int i = 0; i < 8; i++)
            {
                runtime.ApplyOnHitBuffs(
                    world,
                    physicsRuntime,
                    new RuntimeFrame(i),
                    emptyResults,
                    emptyBuffs);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                runtime.ApplyOnHitBuffs(
                    world,
                    physicsRuntime,
                    new RuntimeFrame(100 + i),
                    emptyResults,
                    emptyBuffs);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(default(CombatOnHitBuffApplicationSummary), runtime.OnHitBuffLastSummary);
        }

        [Test]
        public void CombatTraceSystem_AreaConfinementRequestRootsHostilesInsideLockedTargetZone()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity caster);
            CharacterTestCharacterEntity enemy = SpawnCharacter(world, teamId: 2, x: 0f, z: 3f);
            CharacterTestCharacterEntity friendly = SpawnCharacter(world, teamId: 1, x: 0.5f, z: 3f);
            CharacterTestCharacterEntity farEnemy = SpawnCharacter(world, teamId: 2, x: 0f, z: 7f);
            var physicsWorld = new CombatPhysicsWorld();
            RegisterPhysicsBody(physicsWorld, caster, new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
            RegisterPhysicsBody(physicsWorld, enemy, new FixVector3(Fix64.Zero, Fix64.One, Fix64.FromInt(3)));
            RegisterPhysicsBody(physicsWorld, friendly, new FixVector3(Fix64.Half, Fix64.One, Fix64.FromInt(3)));
            RegisterPhysicsBody(physicsWorld, farEnemy, new FixVector3(Fix64.Zero, Fix64.One, Fix64.FromInt(7)));
            var physicsRuntime = CreatePhysicsRuntime(physicsWorld);
            var system = new CharacterTestCharacterCombatTraceSystem(NullRuntimeLogger.Instance);
            system.SetPhysicsRuntime(physicsRuntime);
            system.SetLockedTargetEntityIdProvider(() => enemy.Id.Value);
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            system.SetVfxRuntime(vfx);
            EnqueueAreaConfinementRequest(caster, frame: 6);

            system.Tick(world, null, new RuntimeFrame(6));
            caster.ClearGameplayRequests();

            Assert.IsFalse(HasAreaConfinedStatus(world, enemy));
            Assert.IsFalse(HasAreaConfinedStatus(world, friendly));
            Assert.IsFalse(HasAreaConfinedStatus(world, farEnemy));
            Assert.AreEqual(0, system.ActiveAreaConfinementZoneCount);
            Assert.AreEqual(1, system.AreaConfinementLastTickSummary.PendingChannelCount);
            Assert.AreEqual(0, system.AreaConfinementLastTickSummary.AppliedOrRefreshedCount);
            var pending = new List<VfxSpawnRequest>();
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pending));
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementTelegraphDefinitionId), pending[0].DefinitionId);
            Assert.AreEqual(VfxSourceDomains.Gameplay, pending[0].SourceDomain);
            Assert.AreEqual(6, pending[0].SourceFrame);
            Assert.AreEqual(6, pending[0].LocalFrame);
            Assert.AreEqual(caster.Id.Value, pending[0].SourceId);
            Assert.AreEqual(0, pending[0].TargetId);
            Assert.AreEqual(CharacterTestVfxIds.AreaConfinementTelegraphLifetimeFrames, pending[0].LifetimeFramesOverride);
            Assert.AreEqual(VfxAnchorKind.World, pending[0].Anchor.Kind);
            Assert.AreEqual(0L, pending[0].Anchor.Pose.Position.RawX);
            Assert.AreEqual(0L, pending[0].Anchor.Pose.Position.RawY);
            Assert.AreEqual(3L * VfxVector3.Scale, pending[0].Anchor.Pose.Position.RawZ);
            Assert.AreEqual(0L, pending[0].Anchor.Pose.Forward.RawX);
            Assert.AreEqual(0L, pending[0].Anchor.Pose.Forward.RawY);
            Assert.AreEqual(VfxVector3.Scale, pending[0].Anchor.Pose.Forward.RawZ);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.AreaConfinementTelegraphVfxStableEventId, pending[0].TraceId);

            vfx.Tick(new RuntimeTickContext(6, 0d, 0d, RuntimeTickStage.PostSimulation));
            system.Tick(world, null, new RuntimeFrame(65));
            Assert.IsFalse(HasAreaConfinedStatus(world, enemy));
            Assert.AreEqual(0, system.ActiveAreaConfinementZoneCount);
            Assert.AreEqual(1, system.AreaConfinementLastTickSummary.PendingChannelCount);

            system.Tick(world, null, new RuntimeFrame(66));

            Assert.IsTrue(HasAreaConfinedStatus(world, enemy));
            Assert.IsFalse(HasAreaConfinedStatus(world, friendly));
            Assert.IsFalse(HasAreaConfinedStatus(world, farEnemy));
            Assert.AreEqual(1, system.ActiveAreaConfinementZoneCount);
            Assert.AreEqual(0, system.AreaConfinementLastTickSummary.PendingChannelCount);
            Assert.AreEqual(1, system.AreaConfinementLastTickSummary.AppliedOrRefreshedCount);
            Assert.GreaterOrEqual(system.AreaConfinementLastTickSummary.RejectedTargetCount, 1);
            pending.Clear();
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pending));
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementZoneDefinitionId), pending[0].DefinitionId);
            Assert.AreEqual(66, pending[0].SourceFrame);
            Assert.AreEqual(66, pending[0].LocalFrame);
            Assert.AreEqual(CharacterTestVfxIds.AreaConfinementZoneLifetimeFrames, pending[0].LifetimeFramesOverride);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.AreaConfinementZoneVfxStableEventId, pending[0].TraceId);
        }

        [Test]
        public void CombatTraceSystem_AreaConfinementBuffExpiresAfterTargetLeavesZone()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity caster);
            CharacterTestCharacterEntity enemy = SpawnCharacter(world, teamId: 2, x: 0f, z: 3f);
            var physicsWorld = new CombatPhysicsWorld();
            RegisterPhysicsBody(physicsWorld, caster, new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
            RegisterPhysicsBody(physicsWorld, enemy, new FixVector3(Fix64.Zero, Fix64.One, Fix64.FromInt(3)));
            var physicsRuntime = CreatePhysicsRuntime(physicsWorld);
            var system = new CharacterTestCharacterCombatTraceSystem(NullRuntimeLogger.Instance);
            system.SetPhysicsRuntime(physicsRuntime);
            system.SetLockedTargetEntityIdProvider(() => enemy.Id.Value);
            EnqueueAreaConfinementRequest(caster, frame: 6);
            system.Tick(world, null, new RuntimeFrame(6));
            caster.ClearGameplayRequests();
            Assert.IsFalse(HasAreaConfinedStatus(world, enemy));
            system.Tick(world, null, new RuntimeFrame(66));
            Assert.IsTrue(HasAreaConfinedStatus(world, enemy));

            RegisterPhysicsBody(physicsWorld, enemy, new FixVector3(Fix64.Zero, Fix64.One, Fix64.FromInt(12)));
            system.Tick(world, null, new RuntimeFrame(68));

            Assert.IsFalse(HasAreaConfinedStatus(world, enemy));
            Assert.AreEqual(0, system.AreaConfinementLastTickSummary.AppliedOrRefreshedCount);
        }

        [Test]
        public void CombatTraceSystem_AreaConfinementChannelCancelDoesNotCreateZone()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity caster);
            CharacterTestCharacterEntity enemy = SpawnCharacter(world, teamId: 2, x: 0f, z: 3f);
            var physicsWorld = new CombatPhysicsWorld();
            RegisterPhysicsBody(physicsWorld, caster, new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
            RegisterPhysicsBody(physicsWorld, enemy, new FixVector3(Fix64.Zero, Fix64.One, Fix64.FromInt(3)));
            var physicsRuntime = CreatePhysicsRuntime(physicsWorld);
            var system = new CharacterTestCharacterCombatTraceSystem(NullRuntimeLogger.Instance);
            system.SetPhysicsRuntime(physicsRuntime);
            system.SetLockedTargetEntityIdProvider(() => enemy.Id.Value);
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            system.SetVfxRuntime(vfx);
            EnqueueAreaConfinementRequest(caster, frame: 6);

            system.Tick(world, null, new RuntimeFrame(6));
            caster.ClearGameplayRequests();
            Assert.AreEqual(1, system.AreaConfinementLastTickSummary.PendingChannelCount);
            vfx.Tick(new RuntimeTickContext(6, 0d, 0d, RuntimeTickStage.PostSimulation));

            caster.SetAction(CharacterTestCharacterActionComponent.None);
            system.Tick(world, null, new RuntimeFrame(7));

            Assert.IsFalse(HasAreaConfinedStatus(world, enemy));
            Assert.AreEqual(0, system.ActiveAreaConfinementZoneCount);
            Assert.AreEqual(0, system.AreaConfinementLastTickSummary.PendingChannelCount);
            Assert.AreEqual(1, system.AreaConfinementLastTickSummary.CancelledChannelCount);
            var pending = new List<VfxSpawnRequest>();
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pending));
            Assert.AreEqual(
                new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementTelegraphCancelDefinitionId),
                pending[0].DefinitionId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.AreaConfinementTelegraphCancelVfxStableEventId,
                pending[0].TraceId);
            Assert.AreEqual(CharacterTestVfxIds.AreaConfinementTelegraphCancelLifetimeFrames, pending[0].LifetimeFramesOverride);
        }

        [Test]
        public void CombatTraceSystem_AreaConfinementZoneExpiresAfterDuration()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity caster);
            CharacterTestCharacterEntity enemy = SpawnCharacter(world, teamId: 2, x: 0f, z: 3f);
            var physicsWorld = new CombatPhysicsWorld();
            RegisterPhysicsBody(physicsWorld, caster, new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
            RegisterPhysicsBody(physicsWorld, enemy, new FixVector3(Fix64.Zero, Fix64.One, Fix64.FromInt(3)));
            var physicsRuntime = CreatePhysicsRuntime(physicsWorld);
            var system = new CharacterTestCharacterCombatTraceSystem(NullRuntimeLogger.Instance);
            system.SetPhysicsRuntime(physicsRuntime);
            system.SetLockedTargetEntityIdProvider(() => enemy.Id.Value);
            EnqueueAreaConfinementRequest(caster, frame: 6);

            system.Tick(world, null, new RuntimeFrame(6));
            caster.ClearGameplayRequests();
            system.Tick(world, null, new RuntimeFrame(66));
            system.Tick(world, null, new RuntimeFrame(245));
            Assert.AreEqual(1, system.ActiveAreaConfinementZoneCount);

            system.Tick(world, null, new RuntimeFrame(246));

            Assert.AreEqual(0, system.ActiveAreaConfinementZoneCount);
            Assert.AreEqual(1, system.AreaConfinementLastTickSummary.ExpiredZoneCount);
        }

        [Test]
        public void MechanicSourceDefinitionRegistry_SortsRejectsDuplicatesAndComputesStableHash()
        {
            var registry = new CharacterTestMechanicSourceDefinitionRegistry(
                CreateTestMechanicSourceDefinition(300, "mechanic.test.b"),
                CreateTestMechanicSourceDefinition(100, "mechanic.test.a"));

            CharacterTestMechanicSourceDefinition[] definitions = registry.ToArray();

            Assert.AreEqual(2, registry.Count);
            Assert.AreEqual(100, definitions[0].SourceId);
            Assert.AreEqual(300, definitions[1].SourceId);
            Assert.AreEqual(registry.ComputeStableHash(), new CharacterTestMechanicSourceDefinitionRegistry(
                CreateTestMechanicSourceDefinition(300, "mechanic.test.b"),
                CreateTestMechanicSourceDefinition(100, "mechanic.test.a")).ComputeStableHash());
            Assert.Throws<ArgumentException>(() => new CharacterTestMechanicSourceDefinitionRegistry(
                CreateTestMechanicSourceDefinition(100, "mechanic.test.a"),
                CreateTestMechanicSourceDefinition(100, "mechanic.test.c")));
            Assert.Throws<ArgumentException>(() => new CharacterTestMechanicSourceDefinitionRegistry(
                CreateTestMechanicSourceDefinition(100, "mechanic.test.a"),
                CreateTestMechanicSourceDefinition(200, "mechanic.test.a")));
        }

        [Test]
        public void AreaConfinement_MechanicSourceDefinition_ContainsBuffAndCreateVfxEffects()
        {
            CharacterTestMechanicSourceDefinitionRegistry registry =
                CharacterTestAreaConfinementRuntime.CreateMechanicSourceDefinitions();

            Assert.IsTrue(registry.TryGet(
                CharacterTestAreaConfinementIds.MechanicSourceId,
                out CharacterTestMechanicSourceDefinition definition));
            Assert.AreEqual(CharacterTestAreaConfinementIds.SourceStableId, definition.StableId);
            Assert.AreEqual(CharacterTestMechanicSourceShape.Sphere, definition.Shape);
            Assert.AreEqual(GameplayTargetRelationFilter.Enemy, definition.RelationFilter);
            Assert.AreEqual(CharacterTestAreaConfinementIds.ZoneDurationFrames, definition.DurationFrames);
            Assert.AreEqual(2, definition.EffectCount);
            CharacterTestMechanicEffectDefinition buffEffect = definition.GetEffect(0);
            Assert.AreEqual(CharacterTestMechanicEffectKind.ApplyBuffOnTick, buffEffect.Kind);
            Assert.AreEqual(CharacterTestAreaConfinementIds.RootedBuffId, buffEffect.BuffId);
            CharacterTestMechanicEffectDefinition vfxEffect = definition.GetEffect(1);
            Assert.AreEqual(CharacterTestMechanicEffectKind.SpawnVfxOnCreate, vfxEffect.Kind);
            Assert.AreEqual(CharacterTestVfxIds.AreaConfinementZoneDefinitionId, vfxEffect.VfxDefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.AreaConfinementZoneLifetimeFrames, vfxEffect.VfxLifetimeFrames);
        }

        [Test]
        public void MechanicMotionEffectBridge_AreaConfinedPolicyConvertsStatusToMovementLockRequest()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity target);
            world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>().Set(
                target.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(
                    CharacterTestAreaConfinementIds.AreaConfinedStatusId)));
            var bridge = new CharacterTestMechanicMotionEffectBridge();

            bridge.Tick(world);

            Assert.AreEqual(1, bridge.LastLockedCount);
            Assert.AreEqual(1, bridge.LastAppliedPolicyCount);
            Assert.AreEqual(1, target.PendingMotionRequestCount);
            CharacterTestCharacterMotionRequest request = target.MotionRequests[0];
            Assert.AreEqual(CharacterTestCharacterMotionRequestKind.LockMovement, request.Kind);
            Assert.IsTrue(request.LocksMovement);
            Assert.AreEqual(0f, request.MovementControlScale);
            Assert.AreEqual(1f, request.RotationControlScale);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.AreaConfinementRootedStableEventId,
                request.StableEventId);
        }

        [Test]
        public void MechanicMotionEffectBridge_ControlScalePolicySlowsWithoutLockingRotation()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity target);
            var slowStatus = new GameplayStatusId(990101);
            world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>().Set(
                target.GameplayEntityId,
                new GameplayStatusComponent(slowStatus));
            var bridge = new CharacterTestMechanicMotionEffectBridge(
                new CharacterTestMechanicMotionPolicyRegistry(
                    CharacterTestMechanicMotionPolicyDefinition.ControlScaleRaw(
                        990101,
                        "mechanic.test.slow",
                        slowStatus,
                        CharacterTestDeterministicUnits.ScaleFactorScale / 2,
                        CharacterTestDeterministicUnits.ScaleFactorScale,
                        CharacterMovementMode.Run,
                        MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test_slow"),
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0049)));

            bridge.Tick(world);

            Assert.AreEqual(0, bridge.LastLockedCount);
            Assert.AreEqual(1, bridge.LastAppliedPolicyCount);
            Assert.AreEqual(1, target.PendingMotionRequestCount);
            CharacterTestCharacterMotionRequest request = target.MotionRequests[0];
            Assert.AreEqual(CharacterTestCharacterMotionRequestKind.ControlScale, request.Kind);
            Assert.IsFalse(request.LocksMovement);
            Assert.AreEqual(0.5f, request.MovementControlScale);
            Assert.AreEqual(1f, request.RotationControlScale);
        }

        [Test]
        public void MechanicMotionEffectBridge_StasisPolicyZerosMovementAndRotationAndFreezesAction()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity target);
            world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>().Set(
                target.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStasisIds.StasisStatusId)));
            var motionBridge = new CharacterTestMechanicMotionEffectBridge();
            var statusBridge = new CharacterTestStatusControlEffectBridge();

            statusBridge.Tick(world);
            motionBridge.Tick(world);

            Assert.AreEqual(1, statusBridge.LastActionFrozenCount);
            Assert.IsTrue(statusBridge.IsActionFrozen(target));
            Assert.AreEqual(0, motionBridge.LastLockedCount);
            Assert.AreEqual(1, motionBridge.LastAppliedPolicyCount);
            Assert.AreEqual(1, target.PendingMotionRequestCount);
            CharacterTestCharacterMotionRequest request = target.MotionRequests[0];
            Assert.AreEqual(CharacterTestCharacterMotionRequestKind.ControlScale, request.Kind);
            Assert.IsFalse(request.LocksMovement);
            Assert.AreEqual(0f, request.MovementControlScale);
            Assert.AreEqual(0f, request.RotationControlScale);
            Assert.AreEqual(CharacterMovementMode.ControlLocked, request.MovementMode);
            Assert.AreEqual(CharacterTestStasisIds.StasisActionFreezeStableEventId, request.StableEventId);
        }

        [Test]
        public void MechanicMotionEffectBridge_RootPolicyWinsOverSlowPolicy()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity target);
            var slowStatus = new GameplayStatusId(990102);
            world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>().Set(
                target.GameplayEntityId,
                new GameplayStatusComponent(
                    new GameplayStatusId(CharacterTestAreaConfinementIds.AreaConfinedStatusId),
                    slowStatus));
            var bridge = new CharacterTestMechanicMotionEffectBridge(
                new CharacterTestMechanicMotionPolicyRegistry(
                    CharacterTestMechanicMotionPolicyDefinition.ControlScaleRaw(
                        990102,
                        "mechanic.test.aaa_slow",
                        slowStatus,
                        CharacterTestDeterministicUnits.ScaleFactorScale / 2,
                        CharacterTestDeterministicUnits.ScaleFactorScale,
                        CharacterMovementMode.Run,
                        MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test_slow"),
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0049),
                    CharacterTestMechanicMotionPolicyDefinition.RootActiveMovement(
                        CharacterTestAreaConfinementIds.MotionPolicyId,
                        CharacterTestAreaConfinementIds.MotionPolicyStableId,
                        new GameplayStatusId(CharacterTestAreaConfinementIds.AreaConfinedStatusId),
                        CharacterTestCharacterActionConfigSource.AreaConfinementActionId,
                        CharacterTestCharacterActionConfigSource.AreaConfinementRootedStableEventId)));

            bridge.Tick(world);

            Assert.AreEqual(1, bridge.LastLockedCount);
            Assert.AreEqual(1, bridge.LastAppliedPolicyCount);
            Assert.AreEqual(1, target.PendingMotionRequestCount);
            CharacterTestCharacterMotionRequest request = target.MotionRequests[0];
            Assert.AreEqual(CharacterTestCharacterMotionRequestKind.LockMovement, request.Kind);
            Assert.IsTrue(request.LocksMovement);
            Assert.AreEqual(0f, request.MovementControlScale);
            Assert.AreEqual(1f, request.RotationControlScale);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.AreaConfinementRootedStableEventId,
                request.StableEventId);
        }

        [Test]
        public void StatusVfxBridge_AreaConfinedSpawnsOnceAndStopsWhenStatusClears()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity target);
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            var bridge = new CharacterTestStatusVfxBridgeModule(() => world, vfx);
            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statusStore.Set(
                target.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(
                    CharacterTestAreaConfinementIds.AreaConfinedStatusId)));

            bridge.Tick(new RuntimeTickContext(10, 0d, 0d, RuntimeTickStage.PostSimulation));

            Assert.AreEqual(1, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(0, bridge.LastFrameStoppedCount);
            Assert.AreEqual(1, bridge.ActivePresentationCount);
            var pendingSpawns = new List<VfxSpawnRequest>();
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pendingSpawns));
            Assert.AreEqual(
                new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementCharacterDefinitionId),
                pendingSpawns[0].DefinitionId);
            Assert.AreEqual(VfxSourceDomains.Gameplay, pendingSpawns[0].SourceDomain);
            Assert.AreEqual(target.Id.Value, pendingSpawns[0].SourceId);
            Assert.AreEqual(target.Id.Value, pendingSpawns[0].TargetId);
            Assert.AreEqual(VfxAnchorKind.TargetEntity, pendingSpawns[0].Anchor.Kind);
            Assert.AreEqual(CharacterTestStatusVfxBridgeModule.AreaConfinementCharacterTraceId, pendingSpawns[0].TraceId);
            Assert.AreEqual(CharacterTestVfxIds.AreaConfinementCharacterLifetimeFrames, pendingSpawns[0].LifetimeFramesOverride);

            vfx.Tick(new RuntimeTickContext(10, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(1, vfx.ActiveCount);

            bridge.Tick(new RuntimeTickContext(11, 0d, 0d, RuntimeTickStage.PostSimulation));

            Assert.AreEqual(0, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(1, bridge.LastFrameResolvedInstanceCount);
            Assert.AreEqual(1, bridge.ActivePresentationCount);
            pendingSpawns.Clear();
            Assert.AreEqual(0, vfx.CopyPendingSpawnRequests(pendingSpawns));
            vfx.Tick(new RuntimeTickContext(11, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(1, vfx.ActiveCount);

            bridge.Tick(new RuntimeTickContext(12, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(0, bridge.LastFrameSpawnedCount);
            vfx.Tick(new RuntimeTickContext(12, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(1, vfx.ActiveCount);

            statusStore.Remove(target.GameplayEntityId);
            bridge.Tick(new RuntimeTickContext(13, 0d, 0d, RuntimeTickStage.PostSimulation));

            Assert.AreEqual(0, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(1, bridge.LastFrameReleaseSpawnedCount);
            Assert.AreEqual(1, bridge.LastFrameStoppedCount);
            Assert.AreEqual(0, bridge.ActivePresentationCount);
            var pendingStops = new List<VfxStopRequest>();
            Assert.AreEqual(1, vfx.CopyPendingStopRequests(pendingStops));
            Assert.AreEqual(CharacterTestStatusVfxBridgeModule.AreaConfinementCharacterTraceId, pendingStops[0].TraceId);
            pendingSpawns.Clear();
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pendingSpawns));
            Assert.AreEqual(
                new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementCharacterReleaseDefinitionId),
                pendingSpawns[0].DefinitionId);
            Assert.AreEqual(
                CharacterTestStatusVfxBridgeModule.AreaConfinementCharacterReleaseTraceId,
                pendingSpawns[0].TraceId);
            Assert.AreEqual(
                CharacterTestVfxIds.AreaConfinementCharacterReleaseLifetimeFrames,
                pendingSpawns[0].LifetimeFramesOverride);

            vfx.Tick(new RuntimeTickContext(13, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(1, vfx.ActiveCount);
            vfx.Tick(new RuntimeTickContext(31, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(0, vfx.ActiveCount);
        }

        [Test]
        public void StatusVfxBridge_StopsControlLoopWhenDeathBegins()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorld(out CharacterTestCharacterEntity target);
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            var bridge = new CharacterTestStatusVfxBridgeModule(() => world, vfx);
            world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>().Set(
                target.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));

            bridge.Tick(new RuntimeTickContext(10, 0d, 0d, RuntimeTickStage.PostSimulation));
            vfx.Tick(new RuntimeTickContext(10, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(1, bridge.ActivePresentationCount);

            target.BeginDeath(frame: 11, sourceEntityId: 2, traceId: 50);
            CharacterTestBattleStateSystem.RefreshEntity(world, target, new RuntimeFrame(11), null);
            bridge.Tick(new RuntimeTickContext(11, 0d, 0d, RuntimeTickStage.PostSimulation));

            Assert.AreEqual(0, bridge.ActivePresentationCount);
            Assert.AreEqual(1, bridge.LastFrameStoppedCount);
            var pendingStops = new List<VfxStopRequest>();
            Assert.AreEqual(1, vfx.CopyPendingStopRequests(pendingStops));
            Assert.AreEqual(CharacterTestStatusVfxBridgeModule.StunnedStatusTraceId, pendingStops[0].TraceId);
        }

        private static CharacterTestAbilityHotbarRuntime CreateHotbar(string path = "")
        {
            return new CharacterTestAbilityHotbarRuntime(
                CharacterTestAbilityCatalogSource.CreateRegistry(),
                path);
        }

        private static CharacterTestCharacterRuntimeWorld CreateRuntimeWorld(
            out CharacterTestCharacterEntity attacker)
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            attacker = SpawnCharacter(
                world,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            return world;
        }

        private static CharacterTestCharacterEntity SpawnCharacter(
            CharacterTestCharacterRuntimeWorld world,
            int teamId,
            float x,
            float z,
            CharacterTestCharacterControlOwner owner = CharacterTestCharacterControlOwner.Scripted)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                out CharacterTestCharacterDefinition definition));
            return world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId,
                    x,
                    y: 0f,
                    z,
                    owner),
                definition);
        }

        private static string CreateTempDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "MxFramework.CharacterTestAbilityHotbarTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void AssertMeteorBarrageVfxMode(
            ResourceManager manager,
            ResourceKey key,
            CharacterTestMeteorBarrageVfxMode expectedMode)
        {
            ResourceLoadResult<ResourceHandle<GameObject>> result = manager.Load<GameObject>(key);
            Assert.IsTrue(result.Success, result.Error.Message);
            try
            {
                CharacterTestMeteorBarrageVfxView view =
                    result.Value.Value.GetComponent<CharacterTestMeteorBarrageVfxView>();
                Assert.IsNotNull(view);
                Assert.AreEqual(expectedMode, view.Mode);
                AssertMeteorBarrageVfxShape(result.Value.Value, expectedMode);
                AssertMeteorBarrageLineMaterials(result.Value.Value);
                AssertMeteorBarrageParticleMaterials(result.Value.Value);
                AssertMeteorBarrageMeshMaterials(result.Value.Value);
            }
            finally
            {
                GameObject loadedPrefab = result.Value.Value;
                manager.Release(result.Value);
                DestroyUnityObject(loadedPrefab);
            }
        }

        private static void AssertSwordWaveVfxMode(
            ResourceManager manager,
            ResourceKey key,
            CharacterTestSwordWaveVfxMode expectedMode)
        {
            ResourceLoadResult<ResourceHandle<GameObject>> result = manager.Load<GameObject>(key);
            Assert.IsTrue(result.Success, result.Error.Message);
            try
            {
                CharacterTestSwordWaveVfxView view =
                    result.Value.Value.GetComponent<CharacterTestSwordWaveVfxView>();
                Assert.IsNotNull(view);
                Assert.AreEqual(expectedMode, view.Mode);
                AssertSwordWaveVfxShape(result.Value.Value, expectedMode);
                AssertSwordWaveLineMaterials(result.Value.Value);
            }
            finally
            {
                GameObject loadedPrefab = result.Value.Value;
                manager.Release(result.Value);
                DestroyUnityObject(loadedPrefab);
            }
        }

        private static void AssertSwordWaveVfxShape(
            GameObject prefab,
            CharacterTestSwordWaveVfxMode expectedMode)
        {
            switch (expectedMode)
            {
                case CharacterTestSwordWaveVfxMode.Flight:
                    Transform coreArc = prefab.transform.Find("CoreArc");
                    Transform haloArc = prefab.transform.Find("HaloArc");
                    Transform trailLeft = prefab.transform.Find("TrailLeft");
                    Transform trailRight = prefab.transform.Find("TrailRight");
                    Assert.IsNotNull(coreArc);
                    Assert.IsNotNull(haloArc);
                    Assert.IsNotNull(trailLeft);
                    Assert.IsNotNull(trailRight);
                    AssertSwordWaveFlightUsesProjectileForward(
                        coreArc,
                        trailLeft,
                        trailRight);
                    break;
                case CharacterTestSwordWaveVfxMode.Charge:
                    Assert.IsNotNull(prefab.transform.Find("CoreArc"));
                    Assert.IsNotNull(prefab.transform.Find("Ring"));
                    break;
                case CharacterTestSwordWaveVfxMode.Impact:
                    Assert.IsNotNull(prefab.transform.Find("Ring"));
                    Assert.IsNotNull(prefab.transform.Find("SlashA"));
                    Assert.IsNotNull(prefab.transform.Find("SlashB"));
                    break;
            }
        }

        private static void AssertSwordWaveFlightUsesProjectileForward(
            Transform coreArc,
            Transform trailLeft,
            Transform trailRight)
        {
            LineRenderer coreLine = coreArc.GetComponent<LineRenderer>();
            Assert.IsNotNull(coreLine);
            Assert.AreEqual(LineAlignment.TransformZ, coreLine.alignment);
            Assert.Greater(coreLine.positionCount, 2);

            int centerIndex = coreLine.positionCount / 2;
            float edgeZ = Mathf.Max(
                coreLine.GetPosition(0).z,
                coreLine.GetPosition(coreLine.positionCount - 1).z);
            Assert.Greater(coreLine.GetPosition(centerIndex).z, edgeZ + 0.45f);
            Assert.Greater(
                Mathf.Abs(coreLine.GetPosition(0).y - coreLine.GetPosition(coreLine.positionCount - 1).y),
                0.55f);

            AssertSwordWaveTrailFallsBehind(trailLeft);
            AssertSwordWaveTrailFallsBehind(trailRight);
        }

        private static void AssertSwordWaveTrailFallsBehind(Transform trail)
        {
            LineRenderer trailLine = trail.GetComponent<LineRenderer>();
            Assert.IsNotNull(trailLine);
            Assert.AreEqual(LineAlignment.TransformZ, trailLine.alignment);
            Assert.AreEqual(3, trailLine.positionCount);
            Assert.Less(trailLine.GetPosition(2).z, trailLine.GetPosition(0).z - 0.7f);
        }

        private static void AssertSwordWaveLineMaterials(GameObject prefab)
        {
            LineRenderer[] renderers = prefab.GetComponentsInChildren<LineRenderer>(includeInactive: true);
            Assert.Greater(renderers.Length, 0);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material material = renderers[i].sharedMaterial;
                Assert.IsNotNull(material);
                Assert.IsNotNull(material.shader);
                Assert.IsTrue(material.shader.isSupported, material.shader.name);
                Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, material.renderQueue);
            }
        }

        private static void AssertMeteorBarrageVfxShape(
            GameObject prefab,
            CharacterTestMeteorBarrageVfxMode expectedMode)
        {
            switch (expectedMode)
            {
                case CharacterTestMeteorBarrageVfxMode.Flight:
                    Assert.IsNotNull(prefab.transform.Find("MoltenRock/RockCore"));
                    Assert.AreEqual(3, CountChildrenWithNamePrefix(prefab.transform, "FireTrail"));
                    break;
                case CharacterTestMeteorBarrageVfxMode.Telegraph:
                    Assert.IsNotNull(prefab.transform.Find("MeteorTelegraph/OuterWarningRing"));
                    Assert.IsNotNull(prefab.transform.Find("MeteorTelegraph/HeatPulseRing"));
                    Assert.AreEqual(0, CountChildrenWithNamePrefix(prefab.transform, "WarningRay"));
                    break;
                case CharacterTestMeteorBarrageVfxMode.Impact:
                    Assert.IsNotNull(prefab.transform.Find("MeteorImpact/ShockRing"));
                    Assert.IsNotNull(prefab.transform.Find("MeteorImpact/SmokeRing"));
                    Assert.AreEqual(0, CountChildrenWithNamePrefix(prefab.transform, "ImpactRay"));
                    break;
            }
        }

        private static void AssertMeteorBarrageLineMaterials(GameObject prefab)
        {
            LineRenderer[] renderers = prefab.GetComponentsInChildren<LineRenderer>(includeInactive: true);
            Assert.Greater(renderers.Length, 0);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material material = renderers[i].sharedMaterial;
                Assert.IsNotNull(material);
                Assert.IsNotNull(material.shader);
                StringAssert.StartsWith("Universal Render Pipeline/", material.shader.name);
                Assert.IsTrue(material.shader.isSupported, material.shader.name);
                Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, material.renderQueue);
            }
        }

        private static void AssertMeteorBarrageRendererMaterials(GameObject prefab)
        {
            AssertMeteorBarrageLineMaterials(prefab);
            AssertMeteorBarrageParticleMaterials(prefab);
            AssertMeteorBarrageMeshMaterials(prefab);
        }

        private static void AssertMeteorBarrageParticleMaterials(GameObject prefab)
        {
            ParticleSystemRenderer[] renderers =
                prefab.GetComponentsInChildren<ParticleSystemRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material material = renderers[i].sharedMaterial;
                Assert.IsNotNull(material);
                Assert.IsNotNull(material.shader);
                StringAssert.StartsWith("Universal Render Pipeline/", material.shader.name);
                Assert.IsTrue(material.shader.isSupported, material.shader.name);
            }
        }

        private static void AssertMeteorBarrageMeshMaterials(GameObject prefab)
        {
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material material = renderers[i].sharedMaterial;
                Assert.IsNotNull(material);
                Assert.IsNotNull(material.shader);
                StringAssert.StartsWith("Universal Render Pipeline/", material.shader.name);
                Assert.IsTrue(material.shader.isSupported, material.shader.name);
            }
        }

        private static int CountChildrenWithNamePrefix(Transform root, string prefix)
        {
            int count = 0;
            CountChildrenWithNamePrefix(root, prefix, ref count);
            return count;
        }

        private static void CountChildrenWithNamePrefix(Transform root, string prefix, ref int count)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.StartsWith(prefix, StringComparison.Ordinal))
                    count++;

                CountChildrenWithNamePrefix(child, prefix, ref count);
            }
        }

        private static void CreateLegacyLineChild(Transform parent, string name)
        {
            Assert.IsNotNull(parent);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.one);
        }

        private static void DestroyUnityObject(UnityEngine.Object value)
        {
            if (value == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }

        private static void RegisterPhysicsBody(
            CombatPhysicsWorld physicsWorld,
            CharacterTestCharacterEntity entity)
        {
            RegisterPhysicsBody(physicsWorld, entity, FixVector3.Zero);
        }

        private static void RegisterPhysicsBody(
            CombatPhysicsWorld physicsWorld,
            CharacterTestCharacterEntity entity,
            FixVector3 position)
        {
            CombatEntityId combatEntityId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value);
            CombatBodyId bodyId = new CombatBodyId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value);
            physicsWorld.UpsertBody(new CombatPhysicsBody(
                combatEntityId,
                bodyId,
                position));
            physicsWorld.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicCharacterColliderId),
                CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer,
                new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half),
                new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half)));
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePhysicsRuntime(CombatPhysicsWorld physicsWorld)
        {
            return new CharacterTestCharacterPhysicsRuntime(
                physicsWorld,
                CombatPhysicsLayerMask.None,
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.Zero,
                Fix64.Zero,
                staticColliderCount: 0,
                sourceDescription: "test");
        }

        private static CombatHitBuffApplicationConfig[] GetNoOnHitBuffs(HitResolveResult result)
        {
            return Array.Empty<CombatHitBuffApplicationConfig>();
        }

        private static void EnqueueAreaConfinementRequest(
            CharacterTestCharacterEntity caster,
            long frame)
        {
            caster.SetAction(new CharacterTestCharacterActionComponent(
                CharacterTestCharacterActionConfigSource.AreaConfinementActionId,
                MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running,
                MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Active,
                CharacterTestAreaConfinementTuning.TelegraphStartLocalFrame,
                default,
                string.Empty,
                string.Empty,
                animationTransitionSeconds: 0f,
                animationHoldSeconds: 0f,
                jumpImpulseY: 0f,
                movementMode: CharacterMovementMode.ControlLocked,
                movementLocked: false,
                movementControlScale: 0f,
                rotationControlScale: 1f));
            caster.EnqueueGameplayRequest(new CharacterActionGameplayRequest(
                new CharacterActionAdapterDispatchMetadata(
                    new RuntimeFrame(frame),
                    instanceId: 1,
                    planId: 1,
                    actionId: CharacterTestCharacterActionConfigSource.AreaConfinementActionId,
                    localFrame: 6,
                    stableEventId: CharacterTestCharacterActionConfigSource.AreaConfinementStableEventId,
                    sourceId: caster.Id.Value,
                    traceId: new RuntimeTraceIdentity(949734)),
                caster.GameplayEntityId,
                CharacterActionTrackEventKind.SendGameplayRequest,
                CharacterTestCharacterActionConfigSource.AreaConfinementGameplayRequestId,
                CharacterTestAbilityIds.AreaConfinementAbilityId));
        }

        private static CharacterTestMechanicSourceDefinition CreateTestMechanicSourceDefinition(
            int sourceId,
            string stableId)
        {
            return new CharacterTestMechanicSourceDefinition(
                sourceId,
                stableId,
                CharacterTestMechanicSourceShape.Sphere,
                Fix64.One,
                CombatPhysicsLayerMask.FromLayer(
                    CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer),
                GameplayTargetRelationFilter.Enemy,
                durationFrames: 30L,
                queryActionId: CharacterActionLegacyFixtureIds.From("mechanic.test.query"),
                fallbackTraceStableId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0050,
                includeSourceEntity: false,
                CharacterTestMechanicEffectDefinition.ApplyBuffOnTick(
                    1,
                    CharacterTestAreaConfinementIds.RootedBuffId));
        }

        private static bool HasAreaConfinedStatus(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity)
        {
            if (!world.GameplayComponentWorld.TryGetStore(
                    out GameplayComponentStore<GameplayStatusComponent> statusStore))
            {
                return false;
            }

            return statusStore.TryGet(entity.GameplayEntityId, out GameplayStatusComponent statuses)
                && statuses.Contains(new GameplayStatusId(
                    CharacterTestAreaConfinementIds.AreaConfinedStatusId));
        }

        private static GameplayTrackEvent FindGameplayRequestEvent(
            CharacterActionConfig action,
            int requestId)
        {
            GameplayTrackEvent[] events = action?.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEvent>();
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Kind == CharacterActionTrackEventKind.SendGameplayRequest
                    && (events[i].RequestId == requestId))
                {
                    return events[i];
                }
            }

            Assert.Fail("Expected gameplay request event was not found: " + requestId);
            return default;
        }

        private static int CopyGameplayRequestEventFrames(
            CharacterActionConfig action,
            int stableEventId,
            int requestId,
            int[] frames)
        {
            GameplayTrackEvent[] events = action?.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEvent>();
            int count = 0;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Kind != CharacterActionTrackEventKind.SendGameplayRequest
                    || !(events[i].RequestId == requestId))
                {
                    continue;
                }

                Assert.AreEqual(stableEventId, events[i].StableEventId);
                if (frames != null && count < frames.Length)
                    frames[count] = events[i].Frame;
                count++;
            }

            return count;
        }

        private static CombatProjectileUnityPresentationUpdate CreateProjectilePresentationUpdate(int localFrame)
        {
            return new CombatProjectileUnityPresentationUpdate(
                projectileId: 1,
                definitionId: CharacterTestFireballProjectileAdapter.SwordWaveDefinitionId,
                actionId: 0,
                actionInstanceId: 0,
                traceId: 0,
                localFrame: localFrame,
                ageFrames: 0);
        }

        private static CombatProjectileSnapshot CreateSwordWaveProjectileSnapshot(int localFrame)
        {
            return new CombatProjectileSnapshot(
                projectileId: 1,
                spawnSequence: 0,
                definitionId: CharacterTestFireballProjectileAdapter.SwordWaveDefinitionId,
                definitionStableId: CharacterTestFireballProjectileAdapter.SwordWaveStableId,
                sourceEntityId: new CombatEntityId(1),
                actionId: 10,
                actionInstanceId: 20,
                traceId: 30,
                localFrame: localFrame,
                spawnFrame: new CombatFrame(0),
                lastFrame: new CombatFrame(1),
                ageFrames: 1,
                position: FixVector3.Zero,
                direction: new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One));
        }

        private static void AssertEntry(
            ResourceCatalog catalog,
            string id,
            string typeId,
            string providerId,
            string expectedLabel)
        {
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (!string.Equals(entry.Id, id, StringComparison.Ordinal))
                    continue;

                Assert.AreEqual(typeId, entry.TypeId);
                Assert.AreEqual(providerId, entry.ProviderId);
                bool hasLabel = false;
                for (int labelIndex = 0; labelIndex < entry.Labels.Count; labelIndex++)
                {
                    if (string.Equals(entry.Labels[labelIndex], expectedLabel, StringComparison.Ordinal))
                    {
                        hasLabel = true;
                        break;
                    }
                }

                Assert.IsTrue(hasLabel, "Expected label was not found: " + expectedLabel);
                return;
            }

            Assert.Fail("Expected resource catalog entry was not found: " + id);
        }

        private static PresentationTrackEvent FindPresentationVfxEvent(
            CharacterActionConfig action,
            string resourceKey)
        {
            PresentationTrackEvent[] events = action?.PresentationTrack?.Events ?? Array.Empty<PresentationTrackEvent>();
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Kind == CharacterActionTrackEventKind.SpawnVisualCue
                    && string.Equals(events[i].ResourceKey, resourceKey, StringComparison.Ordinal))
                {
                    return events[i];
                }
            }

            Assert.Fail("Expected presentation VFX event was not found: " + resourceKey);
            return default;
        }
    }

    public sealed class CharacterTestKeyboardAbilityHotbarInputSourceTests : InputTestFixture
    {
        [Test]
        public void ReadPressedSlot_DoesNotAllocateAfterWarmup()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var source = new CharacterTestKeyboardAbilityHotbarInputSource();

            InputSystem.Update();
            Assert.AreEqual(0, source.ReadPressedSlot());

            for (int i = 0; i < 8; i++)
                Assert.AreEqual(0, source.ReadPressedSlot());

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int pressedSlotSum = 0;
            for (int i = 0; i < 64; i++)
                pressedSlotSum += source.ReadPressedSlot();

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, pressedSlotSum);
            Assert.AreEqual(0L, allocatedBytes);

            Press(keyboard.numpad9Key);
            Assert.AreEqual(9, source.ReadPressedSlot());

            for (int i = 0; i < 8; i++)
                Assert.AreEqual(9, source.ReadPressedSlot());

            _ = GC.GetAllocatedBytesForCurrentThread();
            before = GC.GetAllocatedBytesForCurrentThread();
            pressedSlotSum = 0;
            for (int i = 0; i < 64; i++)
                pressedSlotSum += source.ReadPressedSlot();

            allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(576, pressedSlotSum);
            Assert.AreEqual(0L, allocatedBytes);
        }
    }
}
